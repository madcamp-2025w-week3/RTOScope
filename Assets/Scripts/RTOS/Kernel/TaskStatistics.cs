/*
 * TaskStatistics.cs - RTOS 태스크 실행 통계 수집
 * 
 * [역할] 태스크별 CPU 점유율, 지터(Jitter), 실행 횟수 등 통계 수집
 * [위치] RTOS Layer > Kernel (Unity API 사용 금지)
 * 
 * [미구현] 히스토그램 분석, 이상 탐지
 */

using System.Collections.Generic;

namespace RTOScope.RTOS.Kernel
{
    public class TaskStats
    {
        public int TaskId { get; set; }
        public string TaskName { get; set; }
        public float TotalExecutionTime { get; set; }
        public float MinExecutionTime { get; set; } = float.MaxValue;
        public float MaxExecutionTime { get; set; }
        public float AvgExecutionTime { get; set; }
        public int ExecutionCount { get; set; }
        public int DeadlineMissCount { get; set; }
        public float CpuUtilization { get; set; }
    }

    public class SystemStats
    {
        public float TotalCpuUtilization { get; set; }
        public int TotalTaskCount { get; set; }
        public int TotalDeadlineMisses { get; set; }
        public float SystemUptime { get; set; }
        public ulong TotalContextSwitches { get; set; }
    }

    public class TaskStatistics
    {
        private readonly Dictionary<int, TaskStats> _taskStats = new Dictionary<int, TaskStats>();
        private float _totalSystemTime;
        private ulong _contextSwitchCount;
        private readonly object _lock = new object();

        public int TrackedTaskCount => _taskStats.Count;

        /// <summary>TCB 기반 실행 기록</summary>
        public void RecordExecution(TCB tcb, float executionTime)
        {
            if (tcb == null) return;

            lock (_lock)
            {
                if (!_taskStats.TryGetValue(tcb.TaskId, out TaskStats stats))
                {
                    stats = new TaskStats
                    {
                        TaskId = tcb.TaskId,
                        TaskName = tcb.Task.Name
                    };
                    _taskStats[tcb.TaskId] = stats;
                }

                stats.TotalExecutionTime += executionTime;
                stats.ExecutionCount++;
                if (executionTime < stats.MinExecutionTime) stats.MinExecutionTime = executionTime;
                if (executionTime > stats.MaxExecutionTime) stats.MaxExecutionTime = executionTime;
                stats.AvgExecutionTime = stats.TotalExecutionTime / stats.ExecutionCount;
            }
        }

        /// <summary>데드라인 미스 기록</summary>
        public void RecordDeadlineMiss(TCB tcb)
        {
            if (tcb == null) return;
            lock (_lock)
            {
                if (_taskStats.TryGetValue(tcb.TaskId, out TaskStats stats))
                    stats.DeadlineMissCount++;
            }
        }

        /// <summary>컨텍스트 스위칭 기록</summary>
        public void RecordContextSwitch()
        {
            lock (_lock) { _contextSwitchCount++; }
        }

        /// <summary>시스템 시간 업데이트</summary>
        public void UpdateSystemTime(float deltaTime)
        {
            lock (_lock) { _totalSystemTime += deltaTime; }
        }

        /// <summary>특정 태스크 통계</summary>
        public TaskStats GetTaskStats(int taskId)
        {
            lock (_lock)
            {
                return _taskStats.TryGetValue(taskId, out TaskStats stats) ? stats : null;
            }
        }

        /// <summary>모든 태스크 통계</summary>
        public List<TaskStats> GetAllTaskStats()
        {
            lock (_lock) { return new List<TaskStats>(_taskStats.Values); }
        }

        /// <summary>시스템 전체 통계</summary>
        public SystemStats GetSystemStats()
        {
            lock (_lock)
            {
                float totalCpuTime = 0f;
                int totalMisses = 0;
                foreach (var s in _taskStats.Values)
                {
                    totalCpuTime += s.TotalExecutionTime;
                    totalMisses += s.DeadlineMissCount;
                }
                return new SystemStats
                {
                    TotalCpuUtilization = _totalSystemTime > 0 ? (totalCpuTime / _totalSystemTime) * 100f : 0f,
                    TotalTaskCount = _taskStats.Count,
                    TotalDeadlineMisses = totalMisses,
                    SystemUptime = _totalSystemTime,
                    TotalContextSwitches = _contextSwitchCount
                };
            }
        }

        public void Reset()
        {
            lock (_lock)
            {
                _taskStats.Clear();
                _totalSystemTime = 0f;
                _contextSwitchCount = 0;
            }
        }
    }
}
