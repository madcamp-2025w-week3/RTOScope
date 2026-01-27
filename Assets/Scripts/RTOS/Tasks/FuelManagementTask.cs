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

        private static readonly float[] _stepWCETs = {
            0.0003f, // Read
            0.0004f, // Burn
            0.0003f  // Limits
        };

        // 소모 모델 (단위: %/s)
        private const float BASE_BURN_RATE = 0.02f;
        private const float THROTTLE_BURN_RATE = 0.18f;

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

            float burnPerSec = BASE_BURN_RATE + (_throttle * THROTTLE_BURN_RATE);
            float burnThisPeriod = burnPerSec * _periodSeconds;
            _fuelLevel = Mathf.Max(0f, _fuelLevel - burnThisPeriod);

            _state.FuelLevel = _fuelLevel;
            _state.FuelConsumptionRate = burnPerSec;
        }

        private void ApplyLimits()
        {
            if (_state == null) return;

            _state.FuelLowWarning = _fuelLevel <= LOW_FUEL;
            _state.FuelCriticalWarning = _fuelLevel <= CRITICAL_FUEL;

            if (_state.FuelCriticalWarning)
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
