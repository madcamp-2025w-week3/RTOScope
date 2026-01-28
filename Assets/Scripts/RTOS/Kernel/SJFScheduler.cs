/*
 * SJFScheduler.cs - Shortest Job First 스케줄러
 *
 * [알고리즘]
 * 가장 짧은 실행 시간(WCET)을 가진 태스크를 우선 실행
 * 비선점형 버전 구현
 * 긴 태스크는 Starvation 발생 가능
 */

using System.Collections.Generic;

namespace RTOScope.RTOS.Kernel
{
    public class SJFScheduler : IScheduler
    {
        private TCB _currentlyRunning;

        public string Name => "SJF (Shortest Job First)";
        public SchedulerType Type => SchedulerType.SJF;

        public SJFScheduler()
        {
            _currentlyRunning = null;
        }

        public TCB SelectNext(IReadOnlyList<TCB> readyTasks, TCB currentTask)
        {
            if (readyTasks == null || readyTasks.Count == 0)
                return null;

            // 비선점형: 현재 태스크가 Running이면 계속
            if (currentTask != null && currentTask.State == TaskState.Running)
            {
                _currentlyRunning = currentTask;
                return currentTask;
            }

            // Ready 상태인 태스크 중 가장 짧은 WCET 찾기
            TCB shortest = null;
            float minWcet = float.MaxValue;

            foreach (var tcb in readyTasks)
            {
                if (tcb.State != TaskState.Ready) continue;

                // 남은 작업량 계산 (현재 step부터 끝까지의 WCET 합)
                float remainingWcet = GetRemainingWcet(tcb);

                if (remainingWcet < minWcet)
                {
                    minWcet = remainingWcet;
                    shortest = tcb;
                }
            }

            _currentlyRunning = shortest;
            return shortest;
        }

        /// <summary>
        /// 태스크의 남은 실행 시간 계산
        /// </summary>
        private float GetRemainingWcet(TCB tcb)
        {
            if (tcb?.Task == null) return float.MaxValue;

            float total = 0f;
            int currentStep = tcb.Task.CurrentStep;
            int totalSteps = tcb.Task.TotalSteps;

            for (int i = currentStep; i < totalSteps; i++)
            {
                total += tcb.Task.CurrentStepWCET;
            }

            // 최소값 보장 (0 방지)
            return total > 0 ? total : 0.0001f;
        }

        public void OnTimeSliceExpired(TCB task)
        {
            // SJF는 타임 슬라이스 없음 (비선점형)
        }

        public void OnTaskCompleted(TCB task)
        {
            if (_currentlyRunning == task)
            {
                _currentlyRunning = null;
            }
        }

        public void Reset()
        {
            _currentlyRunning = null;
        }
    }
}
