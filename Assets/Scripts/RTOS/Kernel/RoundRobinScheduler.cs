/*
 * RoundRobinScheduler.cs - 라운드 로빈 스케줄러
 *
 * [알고리즘]
 * 모든 태스크에 동일한 타임 슬라이스 부여
 * 순환 방식으로 공평하게 실행
 * 타임 슬라이스 만료 시 다음 태스크로 전환
 */

using System.Collections.Generic;
using UnityEngine;

namespace RTOScope.RTOS.Kernel
{
    public class RoundRobinScheduler : IScheduler
    {
        private const float DEFAULT_TIME_SLICE = 0.01f; // 10ms

        private int _currentIndex;
        private float _timeSlice;
        private float _currentSliceRemaining;
        private TCB _lastTask;

        public string Name => "Round Robin";
        public SchedulerType Type => SchedulerType.RoundRobin;
        public float TimeSlice => _timeSlice;

        public RoundRobinScheduler(float timeSlice = DEFAULT_TIME_SLICE)
        {
            _timeSlice = Mathf.Max(0.001f, timeSlice);
            _currentIndex = 0;
            _currentSliceRemaining = _timeSlice;
            _lastTask = null;
        }

        public TCB SelectNext(IReadOnlyList<TCB> readyTasks, TCB currentTask)
        {
            if (readyTasks == null || readyTasks.Count == 0)
                return null;

            // Ready 상태인 태스크만 필터링
            var ready = new List<TCB>();
            foreach (var tcb in readyTasks)
            {
                if (tcb.State == TaskState.Ready)
                    ready.Add(tcb);
            }

            if (ready.Count == 0)
                return null;

            // 현재 태스크가 아직 타임 슬라이스 남아있고 Ready 상태면 계속
            if (currentTask != null && 
                currentTask.State == TaskState.Running && 
                _currentSliceRemaining > 0 &&
                _lastTask == currentTask)
            {
                return currentTask;
            }

            // 다음 태스크 선택 (순환)
            _currentIndex = _currentIndex % ready.Count;
            TCB selected = ready[_currentIndex];
            
            // 새 태스크면 타임 슬라이스 리셋
            if (selected != _lastTask)
            {
                _currentSliceRemaining = _timeSlice;
                _lastTask = selected;
            }

            return selected;
        }

        public void OnTimeSliceExpired(TCB task)
        {
            if (task == _lastTask)
            {
                _currentSliceRemaining = 0;
                _currentIndex++;
                _lastTask = null; // 다음 틱에서 새 태스크 선택하도록
            }
        }

        public void OnTaskCompleted(TCB task)
        {
            // 완료된 태스크면 다음으로 이동
            if (task == _lastTask)
            {
                _currentIndex++;
                _lastTask = null;
                _currentSliceRemaining = _timeSlice;
            }
        }

        public void Reset()
        {
            _currentIndex = 0;
            _currentSliceRemaining = _timeSlice;
            _lastTask = null;
        }

        /// <summary>
        /// 틱당 시간 소비 (외부에서 호출)
        /// </summary>
        public void ConsumeTime(float deltaTime)
        {
            _currentSliceRemaining -= deltaTime;
        }
    }
}
