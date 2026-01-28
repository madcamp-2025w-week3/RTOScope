/*
 * EngineHealthTask.cs - 엔진 상태 모니터링 (RTOS)
 *
 * [역할]
 * - 스로틀 사용량에 따른 엔진 온도 시뮬레이션
 * - 과열 경고 및 위험 상태 관리
 * - 과열 시 추력 제한 적용
 *
 * [상태 머신 단계]
 * Step 0: Temperature Calculation (온도 계산)
 * Step 1: Overheat Detection (과열 감지)
 * Step 2: Thrust Limiting (추력 제한)
 * Step 3: Cooling Management (냉각 관리)
 *
 * [위치] RTOS Layer > Tasks
 */

using RTOScope.RTOS.Kernel;
using RTOScope.Runtime.Aircraft;
using UnityEngine;

namespace RTOScope.RTOS.Tasks
{
    public class EngineHealthTask : IRTOSTask
    {
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_TEMPERATURE_CALC = 0;
        private const int STEP_OVERHEAT_DETECTION = 1;
        private const int STEP_THRUST_LIMITING = 2;
        private const int STEP_COOLING_MANAGEMENT = 3;
        private const int TOTAL_STEPS = 4;

        // 각 Step의 WCET - 스케줄러 비교용 증가
        private static readonly float[] _stepWCETs =
        {
            0.003f,  // Step 0: Temperature Calc (3ms)
            0.002f,  // Step 1: Overheat Detection (2ms)
            0.002f,  // Step 2: Thrust Limiting (2ms)
            0.003f   // Step 3: Cooling (3ms)
        };           // 총 WCET: 10ms

        // =====================================================================
        // 엔진 온도 상수
        // =====================================================================

        // 온도 범위 (F100-PW-229 터보팬 엔진 기준, 단순화)
        private const float AMBIENT_TEMP = 20f;          // 외기 온도 (°C)
        private const float IDLE_TEMP = 200f;            // 공회전 온도 (°C)
        private const float NORMAL_MAX_TEMP = 600f;      // 정상 최대 온도 (°C)
        private const float WARNING_TEMP = 700f;         // 과열 경고 온도 (°C)
        private const float CRITICAL_TEMP = 850f;        // 과열 위험 온도 (°C)
        private const float MAX_TEMP = 1000f;            // 엔진 손상 온도 (°C)

        // 온도 변화율 (더 현실적인 속도로 조정)
        private const float HEAT_RATE_FACTOR = 50f;      // 가열 계수 (°C/s at 100% throttle) - 기존 150에서 조정
        private const float COOL_RATE_FACTOR = 15f;      // 냉각 계수 (°C/s) - 기존 30에서 조정
        private const float AFTERBURNER_HEAT_MULT = 2.0f; // 애프터버너 배수 (90% 이상 스로틀)

        // 추력 제한
        private const float THRUST_LIMIT_START = 0.8f;   // 과열 시 추력 제한 시작 (80%)
        private const float THRUST_LIMIT_MIN = 0.4f;     // 최대 추력 제한 (40%)

        private const float DELTA_TIME = 0.1f; // 10Hz 기준

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;
        private AircraftState _state;

        private float _targetTemp;
        private float _heatInput;
        private float _coolOutput;

        private bool _log = true;
        private float _logTimer = 0f;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "EngineHealth";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        // =====================================================================
        // 생성자
        // =====================================================================

        public EngineHealthTask(AircraftState state)
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
            if (_state != null)
            {
                _state.EngineTemp = IDLE_TEMP + 200f; // 시작 온도
                _state.ThrustLimitScale = 1f;
            }
            Log("[EngineHealth] 초기화 완료");
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
            Log("[EngineHealth] 데드라인 미스!");
        }

        public void ExecuteStep()
        {
            if (_state == null) return;

            switch (_currentStep)
            {
                case STEP_TEMPERATURE_CALC:
                    ExecuteTemperatureCalc();
                    break;
                case STEP_OVERHEAT_DETECTION:
                    ExecuteOverheatDetection();
                    break;
                case STEP_THRUST_LIMITING:
                    ExecuteThrustLimiting();
                    break;
                case STEP_COOLING_MANAGEMENT:
                    ExecuteCoolingManagement();
                    break;
            }

            _currentStep++;
        }

        // =====================================================================
        // Step 0: 온도 계산
        // =====================================================================

        private void ExecuteTemperatureCalc()
        {
            float throttle = _state.ThrottleCommand;

            // 목표 온도 계산 (스로틀에 비례)
            // 0% throttle -> IDLE_TEMP
            // 100% throttle -> NORMAL_MAX_TEMP (+ 애프터버너 보너스)
            _targetTemp = Mathf.Lerp(IDLE_TEMP, NORMAL_MAX_TEMP, throttle);

            // 애프터버너 영역 (90% 이상)
            if (throttle > 0.9f)
            {
                float afterburnerFactor = (throttle - 0.9f) / 0.1f; // 0~1
                _targetTemp += (CRITICAL_TEMP - NORMAL_MAX_TEMP) * afterburnerFactor * 0.5f;
            }

            // 가열량 계산
            float heatRate = HEAT_RATE_FACTOR * throttle;
            if (throttle > 0.9f)
            {
                heatRate *= AFTERBURNER_HEAT_MULT;
            }

            _heatInput = heatRate * DELTA_TIME;
        }

        // =====================================================================
        // Step 1: 과열 감지
        // =====================================================================

        private void ExecuteOverheatDetection()
        {
            float currentTemp = _state.EngineTemp;

            // 과열 경고 (700°C 이상)
            bool prevWarning = _state.OverheatWarning;
            _state.OverheatWarning = currentTemp >= WARNING_TEMP;

            // 과열 위험 (850°C 이상)
            bool prevCritical = _state.OverheatCritical;
            _state.OverheatCritical = currentTemp >= CRITICAL_TEMP;

            // 상태 변화 시 로그
            if (_state.OverheatWarning && !prevWarning)
            {
                Log($"[EngineHealth] ⚠️ 과열 경고! 온도: {currentTemp:F0}°C");
            }
            if (_state.OverheatCritical && !prevCritical)
            {
                Log($"[EngineHealth] 🔥 과열 위험! 온도: {currentTemp:F0}°C - 추력 제한 적용");
            }
            if (!_state.OverheatWarning && prevWarning)
            {
                Log($"[EngineHealth] ✅ 온도 정상화: {currentTemp:F0}°C");
            }
        }

        // =====================================================================
        // Step 2: 추력 제한
        // =====================================================================

        private void ExecuteThrustLimiting()
        {
            float currentTemp = _state.EngineTemp;

            if (currentTemp >= WARNING_TEMP)
            {
                // 경고 온도 이상: 점진적 추력 제한
                // WARNING_TEMP(700) -> 1.0 (제한 없음)
                // CRITICAL_TEMP(850) -> THRUST_LIMIT_START (0.8)
                // MAX_TEMP(1000) -> THRUST_LIMIT_MIN (0.4)

                float t = Mathf.InverseLerp(WARNING_TEMP, MAX_TEMP, currentTemp);
                _state.ThrustLimitScale = Mathf.Lerp(1f, THRUST_LIMIT_MIN, t);
            }
            else
            {
                // 정상 온도: 제한 없음
                _state.ThrustLimitScale = 1f;
            }
        }

        // =====================================================================
        // Step 3: 냉각 관리
        // =====================================================================

        private void ExecuteCoolingManagement()
        {
            float currentTemp = _state.EngineTemp;
            float throttle = _state.ThrottleCommand;

            // 냉각량 계산 (스로틀이 낮을수록 냉각 효과 증가)
            float coolFactor = 1f - throttle; // 0~1 (낮은 스로틀 = 높은 냉각)
            coolFactor = Mathf.Max(0.1f, coolFactor); // 최소 10% 냉각
            _coolOutput = COOL_RATE_FACTOR * coolFactor * DELTA_TIME;

            // 고도에 따른 냉각 보너스 (높은 고도 = 차가운 공기)
            float altitudeFactor = Mathf.Clamp01(_state.Altitude / 10000f);
            _coolOutput *= (1f + altitudeFactor * 0.5f);

            // 속도에 따른 냉각 보너스 (빠른 속도 = 더 많은 공기 흐름)
            float speedFactor = Mathf.Clamp01(_state.Velocity / 300f);
            _coolOutput *= (1f + speedFactor * 0.3f);

            // 온도 업데이트
            float tempChange = _heatInput - _coolOutput;
            currentTemp += tempChange;

            // 온도 범위 제한
            currentTemp = Mathf.Clamp(currentTemp, AMBIENT_TEMP, MAX_TEMP);

            _state.EngineTemp = currentTemp;

            // 주기적 로그 (5초마다)
            _logTimer += DELTA_TIME;
            if (_logTimer >= 5f)
            {
                _logTimer = 0f;
                if (_log && (_state.OverheatWarning || _state.ThrottleCommand > 0.8f))
                {
                    Log($"[EngineHealth] 온도: {currentTemp:F0}°C, 추력제한: {_state.ThrustLimitScale:P0}");
                }
            }
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        private void Log(string msg)
        {
            if (_log)
                RTOSDebug.Log(msg);
        }
    }
}
