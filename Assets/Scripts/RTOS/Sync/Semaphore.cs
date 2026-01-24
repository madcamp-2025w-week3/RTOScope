/*
 * Semaphore.cs - RTOS 세마포어
 * 
 * [역할] 카운팅 세마포어 - 리소스 풀 관리, 생산자-소비자 패턴
 * [위치] RTOS Layer > Sync (Unity API 사용 금지)
 * 
 * [미구현] 타임아웃, 대기 큐 관리
 */

using System;
using System.Collections.Generic;

namespace RTOScope.RTOS.Sync
{
    /// <summary>
    /// RTOS 카운팅 세마포어
    /// </summary>
    public class Semaphore
    {
        private readonly string _name;
        private readonly int _maxCount;
        private int _currentCount;
        private readonly Queue<int> _waitQueue;  // 대기 중인 태스크 ID

        public string Name => _name;
        public int MaxCount => _maxCount;
        public int CurrentCount => _currentCount;
        public int WaitingTaskCount => _waitQueue.Count;

        public Semaphore(string name, int initialCount, int maxCount)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            if (maxCount <= 0) throw new ArgumentException("maxCount must be positive");
            if (initialCount < 0 || initialCount > maxCount)
                throw new ArgumentException("initialCount out of range");

            _maxCount = maxCount;
            _currentCount = initialCount;
            _waitQueue = new Queue<int>();
        }

        /// <summary>
        /// 세마포어 대기 (P 연산 / Wait)
        /// </summary>
        /// <param name="taskId">요청 태스크 ID</param>
        /// <returns>즉시 획득 성공 여부</returns>
        public bool Wait(int taskId)
        {
            // TODO: 블로킹 대기 및 타임아웃 구현
            
            if (_currentCount > 0)
            {
                _currentCount--;
                return true;
            }
            
            // 대기 큐에 추가
            _waitQueue.Enqueue(taskId);
            return false;
        }

        /// <summary>
        /// 세마포어 시그널 (V 연산 / Signal)
        /// </summary>
        /// <returns>대기 중이던 태스크 ID, 없으면 -1</returns>
        public int Signal()
        {
            if (_waitQueue.Count > 0)
            {
                // 대기 중인 태스크에게 바로 전달
                return _waitQueue.Dequeue();
            }
            
            if (_currentCount < _maxCount)
            {
                _currentCount++;
            }
            return -1;
        }
    }
}
