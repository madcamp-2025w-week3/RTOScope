/*
 * CollisionAvoidanceTask.cs - 충돌 회피 태스크
 *
 * [역할] 지형/장애물/적기 충돌 위험 분석 및 회피 벡터 생성
 * [위치] RTOS Layer > Tasks (Unity API 최소 사용)
 * [우선순위] High (Soft Deadline)
 *
 * [상태 머신 설계]
 * Step 0: 기본 운동 정보 읽기
 * Step 1: 충돌 위험 평가
 * Step 2: 회피 벡터 생성
 */

using RTOScope.RTOS.Kernel;
using RTOScope.Runtime.Aircraft;
using UnityEngine;

namespace RTOScope.RTOS.Tasks
{
    /// <summary>
    /// 충돌 회피 태스크
    /// 주기: 20~50ms 권장 (High)
    /// </summary>
    public class CollisionAvoidanceTask : IRTOSTask
    {
        private const int STEP_READ = 0;
        private const int STEP_EVALUATE = 1;
        private const int STEP_OUTPUT = 2;
        private const int TOTAL_STEPS = 3;

        private static readonly float[] _stepWCETs = {
            0.0003f, // Read
            0.0006f, // Evaluate
            0.0003f  // Output
        };

        // 설정값 (단순 지면 충돌 기준)
        private const float MIN_SAFE_ALTITUDE = 120f; // meters
        private const float TIME_TO_IMPACT_LIMIT = 2.0f; // seconds

        private int _currentStep;
        private AircraftState _state;

        // 캐시
        private float _altitude;
        private float _verticalSpeed;
        private float _timeToImpact;
        private float _risk;

        public string Name => "CollisionAvoid";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        public CollisionAvoidanceTask()
        {
            _currentStep = 0;
        }

        public void SetState(AircraftState state)
        {
            _state = state;
        }

        public void Initialize()
        {
            _currentStep = 0;
            _altitude = 0f;
            _verticalSpeed = 0f;
            _timeToImpact = float.PositiveInfinity;
            _risk = 0f;
        }

        public void ExecuteStep()
        {
            switch (_currentStep)
            {
                case STEP_READ:
                    ReadInputs();
                    _currentStep++;
                    break;
                case STEP_EVALUATE:
                    EvaluateRisk();
                    _currentStep++;
                    break;
                case STEP_OUTPUT:
                    WriteOutputs();
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
            // Soft deadline - no hard safety action here
        }

        private void ReadInputs()
        {
            if (_state == null) return;
            _altitude = _state.Altitude;
            _verticalSpeed = _state.VerticalSpeed;
        }

        private void EvaluateRisk()
        {
            // 단순 지면 충돌 위험 모델: 고도가 낮고 하강 중일 때 위험 증가
            if (_verticalSpeed < -0.1f)
            {
                float closingSpeed = -_verticalSpeed;
                _timeToImpact = _altitude / Mathf.Max(0.1f, closingSpeed);
            }
            else
            {
                _timeToImpact = float.PositiveInfinity;
            }

            if (_altitude < MIN_SAFE_ALTITUDE && _timeToImpact < TIME_TO_IMPACT_LIMIT)
            {
                float normalized = 1f - Mathf.Clamp01(_timeToImpact / TIME_TO_IMPACT_LIMIT);
                _risk = normalized;
            }
            else
            {
                _risk = 0f;
            }
        }

        private void WriteOutputs()
        {
            if (_state == null) return;

            _state.CollisionRisk = _risk;
            if (_risk > 0.01f)
            {
                _state.CollisionAvoidanceActive = true;
                _state.AvoidanceVector = Vector3.up * (1f + _risk);
            }
            else
            {
                _state.CollisionAvoidanceActive = false;
                _state.AvoidanceVector = Vector3.zero;
            }
        }
    }
}
