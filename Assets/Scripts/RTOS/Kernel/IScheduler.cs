/*
 * IScheduler.cs - 스케줄러 인터페이스
 *
 * [역할]
 * 다양한 스케줄링 알고리즘을 교체 가능하게 하는 Strategy Pattern
 *
 * [구현체]
 * - PriorityScheduler: 우선순위 기반 선점형
 * - RoundRobinScheduler: 순환 방식
 * - FCFSScheduler: 선입선출
 * - SJFScheduler: 최단 작업 우선
 */

using System.Collections.Generic;

namespace RTOScope.RTOS.Kernel
{
    /// <summary>
    /// 스케줄러 타입 열거형
    /// </summary>
    public enum SchedulerType
    {
        Priority,       // 우선순위 기반 선점형 (기본)
        RoundRobin,     // 라운드 로빈
        FCFS,           // First Come First Served
        SJF             // Shortest Job First
    }

    /// <summary>
    /// 스케줄러 인터페이스
    /// </summary>
    public interface IScheduler
    {
        /// <summary>스케줄러 이름</summary>
        string Name { get; }

        /// <summary>스케줄러 타입</summary>
        SchedulerType Type { get; }

        /// <summary>
        /// Ready 태스크 목록에서 다음 실행할 태스크를 선택
        /// </summary>
        /// <param name="readyTasks">Ready 상태의 태스크 목록</param>
        /// <param name="currentTask">현재 실행 중인 태스크 (null 가능)</param>
        /// <returns>다음 실행할 TCB (없으면 null)</returns>
        TCB SelectNext(IReadOnlyList<TCB> readyTasks, TCB currentTask);

        /// <summary>
        /// 타임 슬라이스 만료 처리 (RR 등에서 사용)
        /// </summary>
        void OnTimeSliceExpired(TCB task);

        /// <summary>
        /// 태스크 완료 시 호출
        /// </summary>
        void OnTaskCompleted(TCB task);

        /// <summary>
        /// 스케줄러 상태 초기화
        /// </summary>
        void Reset();
    }
}
