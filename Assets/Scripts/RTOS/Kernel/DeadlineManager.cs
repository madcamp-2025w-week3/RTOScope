/*
 * DeadlineManager.cs - RTOS 데드라인 관리자
 * 
 * [역할] Hard/Soft Deadline 위반 감시 및 예외 처리
 * [위치] RTOS Layer > Kernel (Unity API 사용 금지)
 * 
 * [미구현] 데드라인 예측, 적응형 조정
 */

using System;
using System.Collections.Generic;

namespace RTOScope.RTOS.Kernel
{
    public enum DeadlineEventType { Warning, Miss, Critical }

    public class DeadlineEvent
    {
        public DeadlineEventType Type { get; set; }
        public TCB Tcb { get; set; }
        public float Deadline { get; set; }
        public float OverrunAmount { get; set; }
        public DateTime Timestamp { get; set; }
        public DeadlineType DeadlineType { get; set; }
    }

    public class DeadlineManager
    {
        private readonly List<DeadlineEvent> _eventHistory = new List<DeadlineEvent>();
        private readonly int _criticalMissThreshold;
        
        public event Action<DeadlineEvent> OnDeadlineEvent;
        public int TotalMissCount { get; private set; }
        public int HardDeadlineMissCount { get; private set; }

        public DeadlineManager(int criticalMissThreshold = 3)
        {
            _criticalMissThreshold = criticalMissThreshold;
        }

        /// <summary>데드라인 미스 기록</summary>
        public void RecordDeadlineMiss(TCB tcb, float deadline, float actualTime)
        {
            if (tcb == null) return;
            
            TotalMissCount++;
            if (tcb.DeadlineType == DeadlineType.Hard)
                HardDeadlineMissCount++;

            var eventType = tcb.DeadlineMissCount >= _criticalMissThreshold 
                ? DeadlineEventType.Critical 
                : DeadlineEventType.Miss;

            var evt = new DeadlineEvent
            {
                Type = eventType,
                Tcb = tcb,
                Deadline = deadline,
                OverrunAmount = actualTime - deadline,
                Timestamp = DateTime.UtcNow,
                DeadlineType = tcb.DeadlineType
            };
            _eventHistory.Add(evt);
            
            // TODO: Hard Deadline 미스 시 비상 처리
            if (tcb.DeadlineType == DeadlineType.Hard)
            {
                // 비상 모드 진입 등
            }
            
            OnDeadlineEvent?.Invoke(evt);
        }

        public List<DeadlineEvent> GetEventHistory() => new List<DeadlineEvent>(_eventHistory);
        
        public void Reset()
        {
            _eventHistory.Clear();
            TotalMissCount = 0;
            HardDeadlineMissCount = 0;
        }
    }
}
