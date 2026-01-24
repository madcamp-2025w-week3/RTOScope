/*
 * PriorityQueue.cs - 우선순위 기반 태스크 큐
 * 
 * [역할] 실행 대기 중인 태스크(TCB)를 우선순위에 따라 정렬
 * [위치] RTOS Layer > Kernel (Unity API 사용 금지)
 * [복잡도] O(log N) 삽입/추출 목표
 * 
 * [미구현] 힙 기반 최적화, 우선순위 상속 지원
 */

using System;
using System.Collections.Generic;

namespace RTOScope.RTOS.Kernel
{
    /// <summary>
    /// 우선순위 기반 TCB 큐
    /// 낮은 우선순위 값 = 높은 우선순위 (먼저 실행됨)
    /// </summary>
    public class PriorityQueue
    {
        // TODO: 힙 기반 구현으로 변경하여 성능 최적화
        private readonly List<TCB> _queue;
        private readonly object _lock = new object();

        public int Count
        {
            get
            {
                lock (_lock) { return _queue.Count; }
            }
        }

        public bool IsEmpty => Count == 0;

        public PriorityQueue()
        {
            _queue = new List<TCB>();
        }

        /// <summary>TCB를 큐에 추가</summary>
        public void Enqueue(TCB tcb)
        {
            if (tcb == null)
                throw new ArgumentNullException(nameof(tcb));

            lock (_lock)
            {
                _queue.Add(tcb);
                SortByPriority();
            }
        }

        /// <summary>가장 높은 우선순위의 TCB를 꺼냄</summary>
        public TCB Dequeue()
        {
            lock (_lock)
            {
                if (_queue.Count == 0) return null;
                TCB tcb = _queue[0];
                _queue.RemoveAt(0);
                return tcb;
            }
        }

        /// <summary>가장 높은 우선순위의 TCB 확인 (제거 안함)</summary>
        public TCB Peek()
        {
            lock (_lock)
            {
                return _queue.Count > 0 ? _queue[0] : null;
            }
        }

        /// <summary>특정 TCB 제거</summary>
        public bool Remove(TCB tcb)
        {
            lock (_lock) { return _queue.Remove(tcb); }
        }

        /// <summary>큐 비우기</summary>
        public void Clear()
        {
            lock (_lock) { _queue.Clear(); }
        }

        /// <summary>TCB 포함 여부</summary>
        public bool Contains(TCB tcb)
        {
            lock (_lock) { return _queue.Contains(tcb); }
        }

        private void SortByPriority()
        {
            // TODO: 힙 구조로 O(log N) 최적화
            _queue.Sort((a, b) =>
            {
                int priorityCompare = ((int)a.CurrentPriority).CompareTo((int)b.CurrentPriority);
                if (priorityCompare != 0) return priorityCompare;
                // EDF: 동일 우선순위면 데드라인이 빠른 순
                return a.AbsoluteDeadline.CompareTo(b.AbsoluteDeadline);
            });
        }
    }
}
