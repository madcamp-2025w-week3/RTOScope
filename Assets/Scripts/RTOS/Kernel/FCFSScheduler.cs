/*
 * FCFSScheduler.cs - First Come First Served 스케줄러
 *
 * [알고리즘]
 * 먼저 Ready 상태가 된 태스크를 먼저 실행
 * 비선점형: 현재 태스크가 완료될 때까지 대기
 * Convoy Effect 발생 가능
 */

using System.Collections.Generic;

namespace RTOScope.RTOS.Kernel
{
    public class FCFSScheduler : IScheduler
    {
        private Queue<TCB> _arrivalQueue;
        private TCB _currentlyRunning;

        public string Name => "FCFS (Non-Preemptive)";
        public SchedulerType Type => SchedulerType.FCFS;

        public FCFSScheduler()
        {
            _arrivalQueue = new Queue<TCB>();
            _currentlyRunning = null;
        }

        public TCB SelectNext(IReadOnlyList<TCB> readyTasks, TCB currentTask)
        {
            if (readyTasks == null)
                return null;

            // 새로 Ready 된 태스크를 큐에 추가
            foreach (var tcb in readyTasks)
            {
                if (tcb.State == TaskState.Ready && !_arrivalQueue.Contains(tcb) && tcb != _currentlyRunning)
                {
                    _arrivalQueue.Enqueue(tcb);
                }
            }

            // 비선점형: 현재 태스크가 Running이면 계속
            if (currentTask != null && currentTask.State == TaskState.Running)
            {
                _currentlyRunning = currentTask;
                return currentTask;
            }

            // 큐에서 다음 태스크 선택
            while (_arrivalQueue.Count > 0)
            {
                TCB next = _arrivalQueue.Peek();
                if (next.State == TaskState.Ready)
                {
                    _currentlyRunning = next;
                    return next;
                }
                // Ready가 아니면 큐에서 제거
                _arrivalQueue.Dequeue();
            }

            // Ready 태스크 중 아무거나 (fallback)
            foreach (var tcb in readyTasks)
            {
                if (tcb.State == TaskState.Ready)
                {
                    _currentlyRunning = tcb;
                    return tcb;
                }
            }

            return null;
        }

        public void OnTimeSliceExpired(TCB task)
        {
            // FCFS는 타임 슬라이스 없음 (비선점형)
        }

        public void OnTaskCompleted(TCB task)
        {
            // 완료된 태스크가 큐의 앞에 있으면 제거
            if (_arrivalQueue.Count > 0 && _arrivalQueue.Peek() == task)
            {
                _arrivalQueue.Dequeue();
            }
            if (_currentlyRunning == task)
            {
                _currentlyRunning = null;
            }
        }

        public void Reset()
        {
            _arrivalQueue.Clear();
            _currentlyRunning = null;
        }
    }
}
