/*
 * FuelManagementTask.cs - 연료 관리 태스크
 *
 * [역할] 연료 소모/잔량 계산, 경고 및 스로틀 제한
 * [위치] RTOS Layer > Tasks (Unity API 최소 사용)
 * [우선순위] Medium (Soft Deadline)
 *
 * [상태 머신 설계]
 * Step 0: 입력 읽기 (스로틀, 속도)
 * Step 1: 연료 소모 계산
 * Step 2: 경고/리밋 적용
 */

using RTOScope.RTOS.Kernel;
using RTOScope.Runtime.Aircraft;
using UnityEngine;

namespace RTOScope.RTOS.Tasks
{
    /// <summary>
    /// 연료 관리 태스크
    /// 주기: 200~500ms 권장
    /// </summary>
    public class FuelManagementTask : IRTOSTask
    {
        private const int STEP_READ = 0;
        private const int STEP_BURN = 1;
        private const int STEP_LIMITS = 2;
        private const int TOTAL_STEPS = 3;

        // 각 Step의 WCET - 스케줄러 비교용 증가
        private static readonly float[] _stepWCETs = {
            0.005f,  // Read (5ms)
            0.010f,  // Burn (10ms)
            0.005f   // Limits (5ms)
        };           // 총 WCET: 20ms

        // 소모 모델 (단위: L/s)
        // 요구사항: 스로틀 0% => 10 L/s, 100% => 40 L/s
        private const float BURN_RATE_MIN = 10f;
        private const float BURN_RATE_MAX = 40f;

        // 경고 기준 (%)
        private const float LOW_FUEL = 20f;
        private const float CRITICAL_FUEL = 10f;

        private int _currentStep;
        private AircraftState _state;
        private float _periodSeconds;

        // 캐시
        private float _throttle;
        private float _fuelLevel;

        public string Name => "FuelManagement";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        public FuelManagementTask(float periodSeconds = 0.25f)
        {
            _currentStep = 0;
            _periodSeconds = Mathf.Max(0.1f, periodSeconds);
        }

        public void SetState(AircraftState state)
        {
            _state = state;
        }

        public void Initialize()
        {
            _currentStep = 0;
        }

        public void ExecuteStep()
        {
            switch (_currentStep)
            {
                case STEP_READ:
                    ReadInputs();
                    _currentStep++;
                    break;
                case STEP_BURN:
                    BurnFuel();
                    _currentStep++;
                    break;
                case STEP_LIMITS:
                    ApplyLimits();
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
            // Soft deadline - no hard action
        }

        private void ReadInputs()
        {
            if (_state == null) return;
            _throttle = Mathf.Clamp01(_state.ThrottleInput);
            _fuelLevel = _state.FuelLevel;
        }

        private void BurnFuel()
        {
            if (_state == null) return;

            float burnPerSec = Mathf.Lerp(BURN_RATE_MIN, BURN_RATE_MAX, _throttle);
            float burnThisPeriod = burnPerSec * _periodSeconds;

            float capacity = Mathf.Max(1f, _state.FuelCapacityLiters);
            float remaining = Mathf.Max(0f, _state.FuelRemainingLiters - burnThisPeriod);

            _state.FuelRemainingLiters = remaining;
            _fuelLevel = (remaining / capacity) * 100f;
            _state.FuelLevel = _fuelLevel;
            _state.FuelConsumptionRate = burnPerSec;
        }

        private void ApplyLimits()
        {
            if (_state == null) return;

            _state.FuelLowWarning = _fuelLevel <= LOW_FUEL;
            _state.FuelCriticalWarning = _fuelLevel <= CRITICAL_FUEL;

            if (_state.FuelRemainingLiters <= 0.01f)
            {
                _state.ThrottleLimit = 0f;
            }
            else if (_state.FuelCriticalWarning)
            {
                _state.ThrottleLimit = 0.6f;
            }
            else if (_state.FuelLowWarning)
            {
                _state.ThrottleLimit = 0.8f;
            }
            else
            {
                _state.ThrottleLimit = 1f;
            }
        }
    }
}
