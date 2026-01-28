/*
 * StoresManagementTask.cs - 무장 관리 시스템 (RTOS)
 *
 * [역할]
 * - 하드포인트별 무장 상태 관리
 * - 탄약 수량 추적
 * - 무장 잼(고장) 상태 관리
 * - WeaponControlTask에 상태 정보 제공
 *
 * [상태 머신 단계]
 * Step 0: Inventory Check (재고 확인)
 * Step 1: Status Update (상태 업데이트)
 * Step 2: Jam Detection (잼 감지)
 * Step 3: Ready State (준비 상태 계산)
 *
 * [위치] RTOS Layer > Tasks
 */

using RTOScope.RTOS.Kernel;
using RTOScope.Runtime.Aircraft;
using UnityEngine;

namespace RTOScope.RTOS.Tasks
{
    public class StoresManagementTask : IRTOSTask
    {
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_INVENTORY_CHECK = 0;
        private const int STEP_STATUS_UPDATE = 1;
        private const int STEP_JAM_DETECTION = 2;
        private const int STEP_READY_STATE = 3;
        private const int TOTAL_STEPS = 4;

        // 각 Step의 WCET - 스케줄러 비교용 증가
        private static readonly float[] _stepWCETs =
        {
            0.002f,  // Step 0: Inventory Check (2ms)
            0.002f,  // Step 1: Status Update (2ms)
            0.003f,  // Step 2: Jam Detection (3ms)
            0.002f   // Step 3: Ready State (2ms)
        };           // 총 WCET: 9ms

        // =====================================================================
        // 무장 타입 상수
        // =====================================================================

        public const int WEAPON_NONE = 0;
        public const int WEAPON_AIM9 = 1;      // AIM-9 Sidewinder (단거리)
        public const int WEAPON_AIM120 = 2;    // AIM-120 AMRAAM (중거리)

        private static readonly string[] WEAPON_NAMES = { "Empty", "AIM-9", "AIM-120" };

        // 잼 확률 (발사 시도당)
        private const float JAM_PROBABILITY = 0f; // 잼 비활성화

        private const float DELTA_TIME = 0.1f; // 10Hz 기준

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;
        private AircraftState _state;

        private int _totalMissiles;
        private int _readyCount;
        private int _jammedCount;

        private bool _log = true;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "StoresManagement";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        // =====================================================================
        // 생성자
        // =====================================================================

        public StoresManagementTask(AircraftState state)
        {
            _state = state;
            _currentStep = 0;
        }

        // =====================================================================
        // IRTOSTask 구현
        // =====================================================================

        public void Initialize()
        {
            _currentStep = 0;
            Log("[StoresManagement] 초기화 완료");
            LogWeaponStatus();
        }

        public void ResetForNextPeriod()
        {
            _currentStep = 0;
        }

        public void Cleanup()
        {
            // 정리 작업 없음
        }

        public void OnDeadlineMiss()
        {
            Log("[StoresManagement] 데드라인 미스!");
        }

        public void ExecuteStep()
        {
            if (_state == null) return;

            switch (_currentStep)
            {
                case STEP_INVENTORY_CHECK:
                    ExecuteInventoryCheck();
                    break;
                case STEP_STATUS_UPDATE:
                    ExecuteStatusUpdate();
                    break;
                case STEP_JAM_DETECTION:
                    ExecuteJamDetection();
                    break;
                case STEP_READY_STATE:
                    ExecuteReadyState();
                    break;
            }

            _currentStep++;
        }

        // =====================================================================
        // Step 0: 재고 확인
        // =====================================================================

        private void ExecuteInventoryCheck()
        {
            if (_state.HardpointAmmoCount == null) return;

            _totalMissiles = 0;
            _readyCount = 0;
            _jammedCount = 0;

            for (int i = 0; i < _state.TotalHardpoints; i++)
            {
                if (_state.HardpointAmmoCount[i] > 0)
                {
                    _totalMissiles += _state.HardpointAmmoCount[i];

                    if (_state.HardpointReady[i] && !_state.HardpointJammed[i])
                        _readyCount++;

                    if (_state.HardpointJammed[i])
                        _jammedCount++;
                }
            }

            _state.MissileCount = _totalMissiles;
        }

        // =====================================================================
        // Step 1: 상태 업데이트
        // =====================================================================

        private void ExecuteStatusUpdate()
        {
            // 발사 감지 (WeaponFireAck) - 잼 확률만 체크
            // 탄약 차감은 WeaponActuator에서 이미 처리
            if (_state.WeaponFireAck)
            {
                int hp = _state.SelectedHardpointIndex;
                if (hp >= 0 && hp < _state.TotalHardpoints)
                {
                    string weaponName = GetWeaponName(hp);
                    Log($"[StoresManagement] HP{hp + 1} {weaponName} 발사됨, 남은: {_state.HardpointAmmoCount[hp]}");

                    // 발사 시 잼 확률 체크 (다음 발사에 영향)
                    if (Random.value < JAM_PROBABILITY)
                    {
                        _state.HardpointJammed[hp] = true;
                        Log($"[StoresManagement] ⚠️ HP{hp + 1} 잼 발생!");
                    }
                }
            }
        }

        // =====================================================================
        // Step 2: 잼 감지
        // =====================================================================

        private void ExecuteJamDetection()
        {
            // 현재 선택된 하드포인트 잼 상태 확인
            int hp = _state.SelectedHardpointIndex;

            if (hp >= 0 && hp < _state.TotalHardpoints)
            {
                _state.WeaponJammed = _state.HardpointJammed[hp];

                if (_state.WeaponJammed)
                {
                    string weaponName = GetWeaponName(hp);
                    _state.WeaponJamMessage = $"HP{hp + 1} {weaponName} JAM";
                }
                else
                {
                    _state.WeaponJamMessage = "";
                }
            }
        }

        // =====================================================================
        // Step 3: 준비 상태 계산
        // =====================================================================

        private void ExecuteReadyState()
        {
            int hp = _state.SelectedHardpointIndex;

            if (hp >= 0 && hp < _state.TotalHardpoints)
            {
                bool hasAmmo = _state.HardpointAmmoCount[hp] > 0;
                bool isReady = _state.HardpointReady[hp];
                bool notJammed = !_state.HardpointJammed[hp];

                _state.WeaponReady = hasAmmo && isReady && notJammed;

                // 잼된 무장이면 다음 사용 가능한 하드포인트 자동 선택
                if (!_state.WeaponReady && _jammedCount < _state.TotalHardpoints)
                {
                    SelectNextAvailableHardpoint();
                }
            }
            else
            {
                _state.WeaponReady = false;
            }
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        private void SelectNextAvailableHardpoint()
        {
            for (int i = 0; i < _state.TotalHardpoints; i++)
            {
                int hp = (_state.SelectedHardpointIndex + i + 1) % _state.TotalHardpoints;

                if (_state.HardpointAmmoCount[hp] > 0 &&
                    _state.HardpointReady[hp] &&
                    !_state.HardpointJammed[hp])
                {
                    _state.SelectedHardpointIndex = hp;
                    Log($"[StoresManagement] 다음 무장 선택: HP{hp + 1} {GetWeaponName(hp)}");
                    break;
                }
            }
        }

        private string GetWeaponName(int hardpointIndex)
        {
            if (_state.HardpointWeaponType == null) return "Unknown";
            if (hardpointIndex < 0 || hardpointIndex >= _state.TotalHardpoints) return "Unknown";

            int type = _state.HardpointWeaponType[hardpointIndex];
            return type < WEAPON_NAMES.Length ? WEAPON_NAMES[type] : "Unknown";
        }

        private void LogWeaponStatus()
        {
            if (!_log || _state.HardpointWeaponType == null) return;

            string status = "[StoresManagement] 무장 현황:\n";
            for (int i = 0; i < _state.TotalHardpoints; i++)
            {
                string name = GetWeaponName(i);
                int ammo = _state.HardpointAmmoCount[i];
                string jammed = _state.HardpointJammed[i] ? " [JAM]" : "";
                status += $"  HP{i + 1}: {name} x{ammo}{jammed}\n";
            }
            RTOSDebug.Log(status);
        }

        private void Log(string msg)
        {
            if (_log)
                RTOSDebug.Log(msg);
        }
    }
}
