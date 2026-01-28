/*
 * PriorityScheduler.cs - 우선순위 기반 스케줄러
 *
 * [알고리즘]
 * 가장 높은 우선순위(낮은 숫자)를 가진 태스크 선택
 * 선점형: 더 높은 우선순위 태스크가 Ready 되면 즉시 전환
 */

using System.Collections.Generic;

namespace RTOScope.RTOS.Kernel
{
    public class PriorityScheduler : IScheduler
    {
        public string Name => "Priority (Preemptive)";
        public SchedulerType Type => SchedulerType.Priority;

        public TCB SelectNext(IReadOnlyList<TCB> readyTasks, TCB currentTask)
        {
            if (readyTasks == null || readyTasks.Count == 0)
                return null;

            // 가장 높은 우선순위(가장 낮은 CurrentPriority 값) 찾기
            TCB highest = null;
            foreach (var tcb in readyTasks)
            {
                if (tcb.State != TaskState.Ready) continue;

                if (highest == null || tcb.CurrentPriority < highest.CurrentPriority)
                {
                    highest = tcb;
                }
            }

            // 현재 실행 중인 태스크와 비교 (선점 확인)
            if (currentTask != null && currentTask.State == TaskState.Running)
            {
                if (highest == null || currentTask.CurrentPriority <= highest.CurrentPriority)
                {
                    // 현재 태스크가 같거나 높은 우선순위 -> 계속 실행
                    return currentTask;
                }
            }

            return highest;
        }

        public void OnTimeSliceExpired(TCB task)
        {
            // Priority 스케줄러는 타임 슬라이스 무시 (선점형)
        }

        public void OnTaskCompleted(TCB task)
        {
            // 특별한 처리 없음
        }

        public void Reset()
        {
            // 상태 없음
        }
    }
}
