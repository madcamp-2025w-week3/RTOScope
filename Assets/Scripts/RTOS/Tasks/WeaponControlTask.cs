/*
 * WeaponControlTask.cs - 무장 제어 시스템 (RTOS)
 *
 * [역할]
 * - 락온/브레이크락/발사 로직 (Unity API 금지)
 * - 타겟 후보를 기반으로 수동 락온 수행
 * - 발사 명령을 HAL(WeaponActuator)로 전달
 *
 * [상태 머신 단계]
 * Step 0: Target Acquisition (Lock-on logic)
 * Step 1: Weapon Selection
 * Step 2: Launch Sequence
 * Step 3: Post-Launch Tracking Management
 */

using RTOScope.RTOS.Kernel;
using RTOScope.Runtime.Aircraft;
using UnityEngine;

namespace RTOScope.RTOS.Tasks
{
    public class WeaponControlTask : IRTOSTask
    {
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_TARGET_ACQUISITION = 0;
        private const int STEP_WEAPON_SELECTION = 1;
        private const int STEP_LAUNCH_SEQUENCE = 2;
        private const int STEP_TRACKING_MANAGEMENT = 3;
        private const int TOTAL_STEPS = 4;

        private static readonly float[] _stepWCETs =
        {
            0.0004f, // Step 0
            0.0003f, // Step 1
            0.0004f, // Step 2
            0.0005f  // Step 3
        };

        // =====================================================================
        // 무장 제어 상수
        // =====================================================================

        private const float MAX_RANGE = 1500f;
        private const float LOCK_FOV = 40f;  // 전체 각도
        private const float BREAK_FOV = 100f; // 전체 각도 (추적 유지 강화)
        private const float BREAK_GRACE_TIME = 1.2f; // FOV 이탈 허용 시간 (추적 유지 강화)
        private const float MISSILE_LIFETIME = 8f; // 5~10초 권장

        private const float DELTA_TIME = 0.02f; // 50Hz 기준

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;
        private AircraftState _state;

        private bool _prevLockInput;
        private bool _prevBreakInput;
        private bool _prevFireInput;

        private float _outOfFovTimer;
        private int _nextHardpointIndex;
        private bool _pendingFire;

        private bool _log = true;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "WeaponControl";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        // =====================================================================
        // 생성자
        // =====================================================================

        public WeaponControlTask()
        {
            _currentStep = 0;
            _nextHardpointIndex = 0;
            _outOfFovTimer = 0f;
        }

        public void SetState(AircraftState state)
        {
            _state = state;
        }

        // =====================================================================
        // IRTOSTask 구현
        // =====================================================================

        public void Initialize()
        {
            _currentStep = 0;
            _outOfFovTimer = 0f;
            _prevLockInput = false;
            _prevBreakInput = false;
            _prevFireInput = false;
            _pendingFire = false;
            _nextHardpointIndex = 0;
            if (_state != null)
            {
                _state.MissileLifeTimeSeconds = MISSILE_LIFETIME;
            }
        }

        public void ExecuteStep()
        {
            switch (_currentStep)
            {
                case STEP_TARGET_ACQUISITION:
                    StepTargetAcquisition();
                    _currentStep++;
                    break;

                case STEP_WEAPON_SELECTION:
                    StepWeaponSelection();
                    _currentStep++;
                    break;

                case STEP_LAUNCH_SEQUENCE:
                    StepLaunchSequence();
                    _currentStep++;
                    break;

                case STEP_TRACKING_MANAGEMENT:
                    StepTrackingManagement();
                    _currentStep++;
                    break;
            }
        }

        public void ResetForNextPeriod()
        {
            _currentStep = 0;
        }

        public void Cleanup()
        {
        }

        public void OnDeadlineMiss()
        {
            // Soft/Hard 마감에 따라 확장 가능
        }

        // =====================================================================
        // Step 구현
        // =====================================================================

        private void StepTargetAcquisition()
        {
            if (_state == null) return;

            bool lockPressed = IsRisingEdge(_state.LockOnInput, ref _prevLockInput);
            if (!lockPressed) return;

            Log("[WeaponControlTask] R 입력 수신");

            if (!_state.TargetCandidateAvailable)
            {
                Log("[WeaponControlTask] 락온 실패: 타겟 후보 없음");
                return;
            }

            float distance = _state.TargetCandidateDistance;
            float angle = _state.TargetCandidateAngle;

            bool inRange = distance <= MAX_RANGE;
            bool inFov = angle <= LOCK_FOV * 0.5f;

            if (inRange && inFov)
            {
                _state.LockedTargetValid = true;
                _state.LockedTargetId = _state.TargetCandidateId;
                _state.LockedTargetPosition = _state.TargetCandidatePosition;
                _state.LockedTargetDistance = _state.TargetCandidateDistance;
                _state.LockedTargetAngle = _state.TargetCandidateAngle;
                _outOfFovTimer = 0f;

                Vector3 pos = _state.LockedTargetPosition;
                Log($"[WeaponControlTask] 락온 성공: 거리 {distance:F1}m, 각도 {angle:F1}°, 위치 ({pos.x:F1}, {pos.y:F1}, {pos.z:F1})");
            }
            else
            {
                Log($"[WeaponControlTask] 락온 실패: 거리 {distance:F1}m, 각도 {angle:F1}° (조건: {MAX_RANGE:F0}m, {LOCK_FOV * 0.5f:F1}°)");
            }
        }

        private void StepWeaponSelection()
        {
            if (_state == null) return;

            bool firePressed = IsRisingEdge(_state.FireInput, ref _prevFireInput);
            if (!firePressed) return;

            if (_state.MissileCount <= 0) return;

            int total = Mathf.Max(1, _state.TotalHardpoints);
            _state.SelectedHardpointIndex = Mathf.Clamp(_nextHardpointIndex, 0, total - 1);
            _state.MissileLifeTimeSeconds = MISSILE_LIFETIME;
            _pendingFire = true;
        }

        private void StepLaunchSequence()
        {
            if (_state == null) return;

            if (!_pendingFire) return;

            if (_state.MissileCount > 0)
                _state.WeaponFireRequest = true;

            _pendingFire = false;
        }

        private void StepTrackingManagement()
        {
            if (_state == null) return;

            bool breakPressed = IsRisingEdge(_state.BreakLockInput, ref _prevBreakInput);
            if (breakPressed)
            {
                BreakLock("브레이크락 입력");
                return;
            }

            if (_state.LockedTargetValid)
            {
                if (_state.LockedTargetDistance > MAX_RANGE || _state.LockedTargetDistance == float.MaxValue)
                {
                    BreakLock("사거리 초과");
                    return;
                }

                if (_state.LockedTargetAngle > BREAK_FOV * 0.5f)
                {
                    _outOfFovTimer += DELTA_TIME;
                }
                else
                {
                    _outOfFovTimer = 0f;
                }

                if (_outOfFovTimer >= BREAK_GRACE_TIME)
                {
                    BreakLock($"FOV 이탈 ({_state.LockedTargetAngle:F1}°)");
                    return;
                }
            }

            if (_state.WeaponFireAck)
            {
                _state.WeaponFireAck = false;
                int total = Mathf.Max(1, _state.TotalHardpoints);
                _nextHardpointIndex = (_nextHardpointIndex + 1) % total;
            }
        }

        private void BreakLock(string reason)
        {
            _state.LockedTargetValid = false;
            _state.LockedTargetId = -1;
            _state.LockedTargetDistance = 0f;
            _state.LockedTargetAngle = 0f;
            _outOfFovTimer = 0f;
            _state.WeaponFireRequest = false;

            Log($"[WeaponControlTask] 락온 해제: {reason}");
        }

        private static bool IsRisingEdge(bool current, ref bool previous)
        {
            bool rising = current && !previous;
            previous = current;
            return rising;
        }

        private void Log(string message)
        {
            if (!_log) return;
            Debug.Log(message);
        }
    }
}
