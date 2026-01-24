/*
 * ============================================================================
 * TimeManager.cs
 * ============================================================================
 * 
 * [모듈 역할]
 * RTOS 시스템 시간 관리
 * 틱 기반 시간 추적 및 타이머 서비스 제공
 * 
 * [아키텍처 위치]
 * RTOS Layer > Kernel > TimeManager
 * - 순수 C# 코드로 작성 (Unity API 사용 금지)
 * - Unity의 Time.deltaTime은 RTOSRunner에서 전달받음
 * 
 * [주요 책임]
 * - 시스템 시간 추적
 * - 타이머 콜백 관리
 * - 주기적 태스크의 활성화 시점 계산
 * 
 * [미구현 사항]
 * - 하드웨어 타이머 시뮬레이션
 * - 고정밀 시간 측정
 * - 타이머 인터럽트 시뮬레이션
 * ============================================================================
 */

using System;
using System.Collections.Generic;

namespace RTOScope.RTOS.Kernel
{
    /// <summary>
    /// 타이머 콜백 정보
    /// </summary>
    public class TimerCallback
    {
        public int Id { get; set; }
        public float TriggerTime { get; set; }
        public float Period { get; set; }       // 0이면 일회성
        public Action Callback { get; set; }
        public bool IsActive { get; set; }
    }

    /// <summary>
    /// RTOS 시간 관리자
    /// 시스템 전역 시간을 관리하고 타이머 서비스를 제공한다
    /// </summary>
    public class TimeManager
    {
        // =====================================================================
        // 필드
        // =====================================================================
        
        private float _currentTime;         // 현재 시스템 시간 (초)
        private ulong _tickCount;           // 총 틱 카운트
        private float _tickInterval;        // 틱 간격 (초)
        
        private readonly List<TimerCallback> _timers;
        private int _nextTimerId;
        private readonly object _lock = new object();

        // =====================================================================
        // 프로퍼티
        // =====================================================================
        
        public float CurrentTime => _currentTime;
        public ulong TickCount => _tickCount;
        public float TickInterval => _tickInterval;

        // =====================================================================
        // 생성자
        // =====================================================================
        
        public TimeManager(float tickInterval = 0.001f) // 기본 1ms 틱
        {
            _currentTime = 0f;
            _tickCount = 0;
            _tickInterval = tickInterval;
            _timers = new List<TimerCallback>();
            _nextTimerId = 0;
        }

        // =====================================================================
        // 공개 메서드
        // =====================================================================
        
        /// <summary>
        /// 시간을 업데이트한다. 매 프레임 커널에서 호출됨.
        /// </summary>
        /// <param name="deltaTime">경과 시간 (초)</param>
        public void Update(float deltaTime)
        {
            _currentTime += deltaTime;
            _tickCount++;
            
            // TODO: 타이머 인터럽트 시뮬레이션
            // - 고정 간격으로 틱 발생
            // - 정확한 시간 기반 스케줄링
            
            ProcessTimers();
        }

        /// <summary>
        /// 일회성 타이머를 등록한다.
        /// </summary>
        /// <param name="delay">지연 시간 (초)</param>
        /// <param name="callback">콜백 함수</param>
        /// <returns>타이머 ID</returns>
        public int SetTimeout(float delay, Action callback)
        {
            return RegisterTimer(delay, 0f, callback);
        }

        /// <summary>
        /// 주기적 타이머를 등록한다.
        /// </summary>
        /// <param name="period">주기 (초)</param>
        /// <param name="callback">콜백 함수</param>
        /// <returns>타이머 ID</returns>
        public int SetInterval(float period, Action callback)
        {
            return RegisterTimer(period, period, callback);
        }

        /// <summary>
        /// 타이머를 취소한다.
        /// </summary>
        /// <param name="timerId">타이머 ID</param>
        public void CancelTimer(int timerId)
        {
            lock (_lock)
            {
                var timer = _timers.Find(t => t.Id == timerId);
                if (timer != null)
                {
                    timer.IsActive = false;
                }
            }
        }

        /// <summary>
        /// 시스템 시간을 리셋한다.
        /// </summary>
        public void Reset()
        {
            lock (_lock)
            {
                _currentTime = 0f;
                _tickCount = 0;
                _timers.Clear();
            }
        }

        /// <summary>
        /// 특정 시간까지의 남은 시간을 계산한다.
        /// </summary>
        public float GetTimeRemaining(float targetTime)
        {
            float remaining = targetTime - _currentTime;
            return remaining > 0 ? remaining : 0f;
        }

        // =====================================================================
        // 비공개 메서드
        // =====================================================================
        
        private int RegisterTimer(float delay, float period, Action callback)
        {
            lock (_lock)
            {
                int id = _nextTimerId++;
                _timers.Add(new TimerCallback
                {
                    Id = id,
                    TriggerTime = _currentTime + delay,
                    Period = period,
                    Callback = callback,
                    IsActive = true
                });
                return id;
            }
        }

        private void ProcessTimers()
        {
            // TODO: 타이머 처리 최적화
            // - 힙 구조로 다음 만료 타이머 빠르게 찾기
            // - 배치 처리
            
            lock (_lock)
            {
                var expiredTimers = new List<TimerCallback>();
                
                foreach (var timer in _timers)
                {
                    if (timer.IsActive && _currentTime >= timer.TriggerTime)
                    {
                        expiredTimers.Add(timer);
                    }
                }

                foreach (var timer in expiredTimers)
                {
                    // 콜백 실행
                    timer.Callback?.Invoke();

                    if (timer.Period > 0)
                    {
                        // 주기적 타이머: 다음 트리거 시간 설정
                        timer.TriggerTime = _currentTime + timer.Period;
                    }
                    else
                    {
                        // 일회성 타이머: 비활성화
                        timer.IsActive = false;
                    }
                }

                // 비활성화된 타이머 제거
                _timers.RemoveAll(t => !t.IsActive);
            }
        }
    }
}
