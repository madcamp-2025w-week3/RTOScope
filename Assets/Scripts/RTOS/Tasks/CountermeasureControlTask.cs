/*
 * CountermeasureControlTask.cs - 대응책 제어 시스템 (RTOS)
 *
 * [역할]
 * - 미사일 경보/위협 감지 처리
 * - 플레어/채프 발사 시퀀스 관리
 * - 쿨다운 및 재고 관리
 * - 자동/수동 대응 모드 지원
 *
 * [상태 머신 단계]
 * Step 0: Threat Detection (위협 감지)
 * Step 1: Countermeasure Selection (대응책 선택)
 * Step 2: Deploy Sequence (발사 시퀀스)
 * Step 3: Cooldown Management (쿨다운 관리)
 *
 * [위치] RTOS Layer > Tasks
 */

using RTOScope.RTOS.Kernel;
using RTOScope.Runtime.Aircraft;
using UnityEngine;

namespace RTOScope.RTOS.Tasks
{
    public class CountermeasureControlTask : IRTOSTask
    {
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_THREAT_DETECTION = 0;
        private const int STEP_COUNTERMEASURE_SELECTION = 1;
        private const int STEP_DEPLOY_SEQUENCE = 2;
        private const int STEP_COOLDOWN_MANAGEMENT = 3;
        private const int TOTAL_STEPS = 4;

        // 각 Step의 WCET - 스케줄러 비교용 증가
        private static readonly float[] _stepWCETs =
        {
            0.002f,  // Step 0: Threat Detection (2ms)
            0.001f,  // Step 1: Selection (1ms)
            0.002f,  // Step 2: Deploy (2ms)
            0.001f   // Step 3: Cooldown (1ms)
        };           // 총 WCET: 6ms

        // =====================================================================
        // 대응책 상수
        // =====================================================================

        private const float FLARE_COOLDOWN_TIME = 0.5f;  // 플레어 쿨다운 (초)
        private const float CHAFF_COOLDOWN_TIME = 0.3f;  // 채프 쿨다운 (초)
        private const float AUTO_DEPLOY_DISTANCE = 800f; // 자동 발사 거리 (m)
        private const int BURST_COUNT = 2;               // 한 번에 발사할 개수

        private const float DELTA_TIME = 0.02f; // 50Hz 기준

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;
        private AircraftState _state;

        // 쿨다운 타이머
        private float _flareCooldownTimer;
        private float _chaffCooldownTimer;

        // 버스트 카운터 (연속 발사)
        private int _flareBurstRemaining;
        private int _chaffBurstRemaining;

        // 이전 입력 상태 (엣지 감지용)
        private bool _prevFlareInput;
        private bool _prevChaffInput;

        private bool _log = true;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "CountermeasureControl";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        // =====================================================================
        // 생성자
        // =====================================================================

        public CountermeasureControlTask(AircraftState state)
        {
            _state = state;
            _currentStep = 0;
            _flareCooldownTimer = 0f;
            _chaffCooldownTimer = 0f;
            _flareBurstRemaining = 0;
            _chaffBurstRemaining = 0;
        }

        // =====================================================================
        // IRTOSTask 구현
        // =====================================================================

        public void Initialize()
        {
            _currentStep = 0;
            _flareCooldownTimer = 0f;
            _chaffCooldownTimer = 0f;
            Log("[CountermeasureControl] 초기화 완료");
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
            Log("[CountermeasureControl] 데드라인 미스!");
        }

        public void ExecuteStep()
        {
            if (_state == null) return;

            switch (_currentStep)
            {
                case STEP_THREAT_DETECTION:
                    ExecuteThreatDetection();
                    break;
                case STEP_COUNTERMEASURE_SELECTION:
                    ExecuteCountermeasureSelection();
                    break;
                case STEP_DEPLOY_SEQUENCE:
                    ExecuteDeploySequence();
                    break;
                case STEP_COOLDOWN_MANAGEMENT:
                    ExecuteCooldownManagement();
                    break;
            }

            _currentStep++;
        }

        // =====================================================================
        // Step 0: 위협 감지
        // =====================================================================

        private void ExecuteThreatDetection()
        {
            // 위협 상태는 HAL(MissileThreatSensor)에서 업데이트
            // 여기서는 자동 대응 트리거 확인

            if (_state.MissileThreatDetected && _state.AutoCountermeasureEnabled)
            {
                // 자동 대응: 거리가 가까우면 플레어 발사
                if (_state.MissileThreatDistance < AUTO_DEPLOY_DISTANCE)
                {
                    if (_flareBurstRemaining == 0 && !_state.FlareCooldownActive)
                    {
                        _flareBurstRemaining = BURST_COUNT;
                        Log($"[CountermeasureControl] 자동 대응: 미사일 위협 {_state.MissileThreatDistance:F0}m, 플레어 발사");
                    }
                }
            }
        }

        // =====================================================================
        // Step 1: 대응책 선택 (수동 입력 처리)
        // =====================================================================

        private void ExecuteCountermeasureSelection()
        {
            // 플레어 입력 (엣지 감지)
            bool flarePressed = _state.FlareInput && !_prevFlareInput;
            _prevFlareInput = _state.FlareInput;

            if (flarePressed && _flareBurstRemaining == 0 && !_state.FlareCooldownActive)
            {
                _flareBurstRemaining = BURST_COUNT;
                Log("[CountermeasureControl] 수동 플레어 발사 요청");
            }

            // 채프 입력 (엣지 감지)
            bool chaffPressed = _state.ChaffInput && !_prevChaffInput;
            _prevChaffInput = _state.ChaffInput;

            if (chaffPressed && _chaffBurstRemaining == 0 && !_state.ChaffCooldownActive)
            {
                _chaffBurstRemaining = BURST_COUNT;
                Log("[CountermeasureControl] 수동 채프 발사 요청");
            }
        }

        // =====================================================================
        // Step 2: 발사 시퀀스
        // =====================================================================

        private void ExecuteDeploySequence()
        {
            // 플레어 발사
            if (_flareBurstRemaining > 0 && !_state.FlareCooldownActive)
            {
                if (_state.FlareCount > 0)
                {
                    _state.FlareFireRequest = true;
                    _state.FlareCount--;
                    _flareBurstRemaining--;
                    _flareCooldownTimer = FLARE_COOLDOWN_TIME;
                    _state.FlareCooldownActive = true;

                    Log($"[CountermeasureControl] 플레어 발사! 남은: {_state.FlareCount}");
                }
                else
                {
                    _flareBurstRemaining = 0;
                    Log("[CountermeasureControl] 플레어 소진!");
                }
            }
            else
            {
                _state.FlareFireRequest = false;
            }

            // 채프 발사
            if (_chaffBurstRemaining > 0 && !_state.ChaffCooldownActive)
            {
                if (_state.ChaffCount > 0)
                {
                    _state.ChaffFireRequest = true;
                    _state.ChaffCount--;
                    _chaffBurstRemaining--;
                    _chaffCooldownTimer = CHAFF_COOLDOWN_TIME;
                    _state.ChaffCooldownActive = true;

                    Log($"[CountermeasureControl] 채프 발사! 남은: {_state.ChaffCount}");
                }
                else
                {
                    _chaffBurstRemaining = 0;
                    Log("[CountermeasureControl] 채프 소진!");
                }
            }
            else
            {
                _state.ChaffFireRequest = false;
            }
        }

        // =====================================================================
        // Step 3: 쿨다운 관리
        // =====================================================================

        private void ExecuteCooldownManagement()
        {
            // 플레어 쿨다운
            if (_state.FlareCooldownActive)
            {
                _flareCooldownTimer -= DELTA_TIME;
                if (_flareCooldownTimer <= 0f)
                {
                    _state.FlareCooldownActive = false;
                    _flareCooldownTimer = 0f;
                }
            }

            // 채프 쿨다운
            if (_state.ChaffCooldownActive)
            {
                _chaffCooldownTimer -= DELTA_TIME;
                if (_chaffCooldownTimer <= 0f)
                {
                    _state.ChaffCooldownActive = false;
                    _chaffCooldownTimer = 0f;
                }
            }
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        private void Log(string msg)
        {
            if (_log)
                Debug.Log(msg);
        }
    }
}
