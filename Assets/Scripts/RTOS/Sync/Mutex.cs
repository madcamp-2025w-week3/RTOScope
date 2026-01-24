/*
 * Mutex.cs - RTOS 뮤텍스
 * 
 * [역할] 상호 배제 락 - 공유 자원 접근 제어
 * [위치] RTOS Layer > Sync (Unity API 사용 금지)
 * 
 * [미구현] 우선순위 상속, 데드락 감지
 */

using System;

namespace RTOScope.RTOS.Sync
{
    /// <summary>
    /// RTOS 뮤텍스 - 태스크 간 상호 배제
    /// </summary>
    public class Mutex
    {
        private readonly string _name;
        private bool _isLocked;
        private int _ownerTaskId;
        private int _lockCount;  // 재귀적 락 지원

        public string Name => _name;
        public bool IsLocked => _isLocked;
        public int OwnerTaskId => _ownerTaskId;

        public Mutex(string name)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _isLocked = false;
            _ownerTaskId = -1;
            _lockCount = 0;
        }

        /// <summary>
        /// 뮤텍스를 획득 시도
        /// </summary>
        /// <param name="taskId">요청 태스크 ID</param>
        /// <returns>성공 여부</returns>
        public bool TryAcquire(int taskId)
        {
            // TODO: 우선순위 상속(Priority Inheritance) 구현
            // - 우선순위 역전 방지
            
            if (!_isLocked)
            {
                _isLocked = true;
                _ownerTaskId = taskId;
                _lockCount = 1;
                return true;
            }
            
            // 재귀적 락: 같은 태스크가 이미 소유 중이면 카운트 증가
            if (_ownerTaskId == taskId)
            {
                _lockCount++;
                return true;
            }
            
            return false;  // 다른 태스크가 소유 중
        }

        /// <summary>
        /// 뮤텍스 해제
        /// </summary>
        public bool Release(int taskId)
        {
            if (!_isLocked || _ownerTaskId != taskId)
                return false;

            _lockCount--;
            if (_lockCount == 0)
            {
                _isLocked = false;
                _ownerTaskId = -1;
            }
            return true;
        }
    }
}
