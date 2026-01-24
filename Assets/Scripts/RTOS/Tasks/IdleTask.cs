/*
 * ============================================================================
 * IdleTask.cs - Idle 태스크
 * ============================================================================
 *
 * [모듈 역할]
 * 시스템에 실행할 Ready 태스크가 없을 때 실행되는 기본 태스크
 *
 * [아키텍처 위치]
 * RTOS Layer > Tasks > IdleTask
 * - 가장 낮은 우선순위 (TaskPriority.Idle = 255)
 * - 항상 Ready 상태로 유지
 * - 다른 태스크가 없을 때만 실행됨
 *
 * [설계 철학]
 * - 실제 RTOS의 Idle Task 개념 구현
 * - CPU가 "아무것도 안 함"을 시뮬레이션
 * - Idle 시간 측정에 활용
 * ============================================================================
 */

using RTOScope.RTOS.Kernel;

namespace RTOScope.RTOS.Tasks
{
    /// <summary>
    /// Idle 태스크 - 시스템이 유휴 상태일 때 실행
    /// 가장 낮은 우선순위로 항상 Ready 상태 유지
    /// </summary>
    public class IdleTask : IRTOSTask
    {
        // Step 정의: Idle은 단 하나의 Step만 가짐
        private const int STEP_IDLE = 0;
        private const int TOTAL_STEPS = 1;

        // 각 Step의 WCET (Idle은 거의 0에 가깝게)
        private static readonly float[] _stepWCETs = { 0.0001f }; // 0.1ms

        private int _currentStep;
        private float _totalIdleTime;

        public string Name => "Idle";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _stepWCETs[_currentStep];
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;
        public float TotalIdleTime => _totalIdleTime;

        public IdleTask()
        {
            _currentStep = 0;
            _totalIdleTime = 0f;
        }

        public void Initialize()
        {
            _currentStep = 0;
            _totalIdleTime = 0f;
        }

        public void ExecuteStep()
        {
            switch (_currentStep)
            {
                case STEP_IDLE:
                    // Idle 상태: 아무것도 하지 않음
                    // 실제로는 CPU가 저전력 모드에 진입할 수 있음
                    _totalIdleTime += _stepWCETs[STEP_IDLE];
                    _currentStep++;
                    break;
            }
        }

        public void ResetForNextPeriod()
        {
            // Idle은 항상 Ready 상태를 유지해야 하므로 즉시 리셋
            _currentStep = 0;
        }

        public void Cleanup()
        {
            // 정리할 것 없음
        }

        public void OnDeadlineMiss()
        {
            // Idle 태스크는 데드라인이 없음
        }
    }
}
