/*
 * ============================================================================
 * TCB.cs - Task Control Block
 * ============================================================================
 * 
 * [모듈 역할]
 * 태스크 제어 블록 - 태스크의 관리 정보 저장소
 * "이 태스크는 현재 Ready 상태다", "우선순위는 1이다", 
 * "다음 실행 시간은 10.5ms다" 같은 관리 정보를 보유
 * 
 * [아키텍처 위치]
 * RTOS Layer > Kernel > TCB
 * - 순수 C# 코드로 작성 (Unity API 사용 금지)
 * - 스케줄러(RTOSKernel)가 TCB를 통해 태스크를 관리
 * - IRTOSTask를 래핑하여 실행 관리
 * 
 * [설계 철학]
 * - 실제 임베디드 RTOS의 TCB 개념 구현
 * - 태스크 로직(IRTOSTask)과 관리 정보(TCB) 분리
 * - 스케줄링 결정에 필요한 모든 메타데이터 보유
 * 
 * [관리 정보]
 * - 태스크 ID, 이름, 상태
 * - 우선순위 (기본/현재 - 우선순위 상속 지원)
 * - 주기, 데드라인 (상대적/절대적)
 * - 다음 활성화 시간
 * - 실행 통계 (실행 횟수, 총 실행 시간, WCET 등)
 * ============================================================================
 */

using System;

namespace RTOScope.RTOS.Kernel
{
    /// <summary>
    /// Task Control Block - 태스크 관리 정보 블록
    /// 스케줄러가 태스크를 관리하는 데 필요한 모든 정보를 담음
    /// </summary>
    public class TCB
    {
        // =====================================================================
        // 식별 정보
        // =====================================================================

        private static int _nextTaskId = 0;

        /// <summary>태스크 고유 ID</summary>
        public int TaskId { get; }

        /// <summary>실제 태스크 로직 (IRTOSTask 구현체)</summary>
        public IRTOSTask Task { get; }

        // =====================================================================
        // 상태 정보
        // =====================================================================

        /// <summary>현재 태스크 상태</summary>
        public TaskState State { get; private set; }

        /// <summary>기본 우선순위 (생성 시 설정됨)</summary>
        public TaskPriority BasePriority { get; }

        /// <summary>현재 우선순위 (우선순위 상속 시 변경될 수 있음)</summary>
        public TaskPriority CurrentPriority { get; private set; }

        /// <summary>데드라인 타입 (Hard/Soft)</summary>
        public DeadlineType DeadlineType { get; }

        // =====================================================================
        // 타이밍 정보
        // =====================================================================

        /// <summary>실행 주기 (초), 0이면 비주기적(이벤트 기반)</summary>
        public float Period { get; }

        /// <summary>상대적 데드라인 (초) - 활성화 후 완료까지 허용 시간</summary>
        public float RelativeDeadline { get; }

        /// <summary>다음 활성화 시간 (시스템 시간 기준)</summary>
        public float NextActivationTime { get; private set; }

        /// <summary>절대 데드라인 (현재 실행 인스턴스)</summary>
        public float AbsoluteDeadline { get; private set; }

        /// <summary>마지막 실행 시작 시간</summary>
        public float LastExecutionStart { get; private set; }

        // =====================================================================
        // 실행 통계
        // =====================================================================

        /// <summary>총 실행 횟수</summary>
        public int ExecutionCount { get; private set; }

        /// <summary>총 실행 시간 (초)</summary>
        public float TotalExecutionTime { get; private set; }

        /// <summary>최악의 경우 실행 시간 (WCET - Worst Case Execution Time)</summary>
        public float WorstCaseExecutionTime { get; private set; }

        /// <summary>평균 실행 시간</summary>
        public float AverageExecutionTime => ExecutionCount > 0
            ? TotalExecutionTime / ExecutionCount
            : 0f;

        /// <summary>데드라인 미스 횟수</summary>
        public int DeadlineMissCount { get; private set; }

        /// <summary>지터 (Jitter) - 실행 시간 편차</summary>
        public float Jitter { get; private set; }

        // =====================================================================
        // 생성자
        // =====================================================================

        /// <summary>
        /// TCB 생성자
        /// </summary>
        /// <param name="task">관리할 태스크 (IRTOSTask 구현체)</param>
        /// <param name="priority">우선순위</param>
        /// <param name="period">실행 주기 (초), 0이면 비주기적</param>
        /// <param name="deadline">상대적 데드라인 (초), 0이면 주기와 동일</param>
        /// <param name="deadlineType">데드라인 타입 (Hard/Soft)</param>
        public TCB(
            IRTOSTask task,
            TaskPriority priority,
            float period = 0f,
            float deadline = 0f,
            DeadlineType deadlineType = DeadlineType.Soft)
        {
            if (task == null)
                throw new ArgumentNullException(nameof(task));

            TaskId = _nextTaskId++;
            Task = task;

            BasePriority = priority;
            CurrentPriority = priority;
            DeadlineType = deadlineType;

            Period = period;
            RelativeDeadline = deadline > 0f ? deadline : period;

            State = TaskState.Created;
            ExecutionCount = 0;
            TotalExecutionTime = 0f;
            WorstCaseExecutionTime = 0f;
            DeadlineMissCount = 0;
            Jitter = 0f;
        }

        // =====================================================================
        // 상태 관리 메서드
        // =====================================================================

        /// <summary>
        /// 태스크 상태를 변경한다.
        /// </summary>
        /// <param name="newState">새로운 상태</param>
        public void SetState(TaskState newState)
        {
            // 상태 전이 유효성 검사 구현
            // - Created -> Ready: 초기화 완료 후
            // - Ready -> Running: 스케줄러가 선택 시
            // - Running -> Ready: 선점 또는 실행 완료 시
            // - Running -> Blocked: 리소스 대기 시
            // - Blocked -> Ready: 리소스 획득 시
            // - * -> Suspended: 일시 정지 요청 시

            State = newState;
        }

        /// <summary>
        /// 태스크를 활성화한다 (주기적 태스크용)
        /// </summary>
        /// <param name="currentTime">현재 시스템 시간</param>
        public void Activate(float currentTime)
        {
            // 주기적 태스크 활성화 로직 개선
            // - 드리프트 보정
            // - 오버런 감지

            NextActivationTime = currentTime + Period;
            AbsoluteDeadline = currentTime + RelativeDeadline;
            SetState(TaskState.Ready);
        }

        /// <summary>
        /// 우선순위를 상속받는다 (우선순위 역전 방지용)
        /// </summary>
        /// <param name="inheritedPriority">상속받을 우선순위</param>
        public void InheritPriority(TaskPriority inheritedPriority)
        {
            // 우선순위 상속 프로토콜 구현
            // - 현재보다 높은 우선순위만 상속
            // - 상속 체인 관리

            if (inheritedPriority < CurrentPriority)
            {
                CurrentPriority = inheritedPriority;
            }
        }

        /// <summary>
        /// 우선순위를 원래대로 복원한다
        /// </summary>
        public void RestorePriority()
        {
            CurrentPriority = BasePriority;
        }

        // =====================================================================
        // 실행 통계 메서드
        // =====================================================================

        /// <summary>
        /// 실행 시작을 기록한다
        /// </summary>
        /// <param name="startTime">시작 시간</param>
        public void RecordExecutionStart(float startTime)
        {
            LastExecutionStart = startTime;
        }

        /// <summary>
        /// 실행 완료를 기록한다
        /// </summary>
        /// <param name="executionTime">실행 소요 시간</param>
        public void RecordExecutionComplete(float executionTime)
        {
            ExecutionCount++;
            TotalExecutionTime += executionTime;

            // WCET 갱신
            if (executionTime > WorstCaseExecutionTime)
            {
                WorstCaseExecutionTime = executionTime;
            }

            // TODO: 지터(Jitter) 계산 구현
            // - 실행 시간의 표준 편차 계산
            // - 이동 평균 기반 계산
        }

        /// <summary>
        /// 데드라인 미스를 기록한다
        /// </summary>
        public void RecordDeadlineMiss()
        {
            DeadlineMissCount++;

            // 태스크에 미스 알림 (복구 동작 수행)
            Task.OnDeadlineMiss();
        }

        /// <summary>
        /// 통계를 리셋한다
        /// </summary>
        public void ResetStatistics()
        {
            ExecutionCount = 0;
            TotalExecutionTime = 0f;
            WorstCaseExecutionTime = 0f;
            DeadlineMissCount = 0;
            Jitter = 0f;
        }

        // =====================================================================
        // 유틸리티 메서드
        // =====================================================================

        /// <summary>
        /// TCB 정보 문자열 반환 (디버깅용)
        /// </summary>
        public override string ToString()
        {
            return $"[TCB] ID:{TaskId} Name:{Task.Name} State:{State} " +
                   $"Pri:{CurrentPriority} Period:{Period:F3}s " +
                   $"Exec:{ExecutionCount} WCET:{WorstCaseExecutionTime:F6}s";
        }
    }
}
