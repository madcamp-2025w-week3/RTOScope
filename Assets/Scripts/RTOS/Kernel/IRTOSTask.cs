/*
 * ============================================================================
 * IRTOSTask.cs
 * ============================================================================
 *
 * [모듈 역할]
 * RTOS 태스크 인터페이스 - 상태 머신 기반 실행 로직
 *
 * [아키텍처 위치]
 * RTOS Layer > Kernel > IRTOSTask
 * - 순수 C# 코드로 작성 (Unity API 사용 금지)
 * - 태스크의 "할 일"만 정의 (상태 관리는 TCB에서)
 *
 * [설계 철학]
 * - 상태 머신(State Machine) 패턴: 태스크를 원자적 단계(Step)로 분할
 * - 각 Step은 짧은 시간 내에 완료되어야 함
 * - 커널이 Step 사이에서 선점 가능
 * ============================================================================
 */

namespace RTOScope.RTOS.Kernel
{
    /// <summary>
    /// 태스크의 상태를 나타내는 열거형
    /// 표준 RTOS 태스크 상태 모델을 따름
    /// </summary>
    public enum TaskState
    {
        Created,    // 생성됨, 아직 스케줄러에 등록되지 않음
        Ready,      // 실행 준비 완료, CPU 할당 대기 중
        Running,    // 현재 실행 중
        Blocked,    // 리소스/이벤트 대기 중
        Suspended,  // 일시 정지됨
        Waiting     // 다음 주기까지 대기 중 (주기적 태스크)
    }

    /// <summary>
    /// 태스크 우선순위 레벨
    /// 낮은 숫자 = 높은 우선순위 (임베디드 시스템 관례)
    /// </summary>
    public enum TaskPriority
    {
        Critical = 0,   // 가장 높은 우선순위 (비행 제어 - Hard Deadline)
        High = 1,       // 높은 우선순위 (센서 읽기, 레이더 등)
        Medium = 2,     // 중간 우선순위 (상태 모니터링)
        Low = 3,        // 낮은 우선순위 (로깅, UI 갱신 등)
        Idle = 255      // 가장 낮은 우선순위 (IdleTask 전용)
    }

    /// <summary>
    /// 데드라인 타입
    /// </summary>
    public enum DeadlineType
    {
        Hard,   // 위반 시 시스템 실패 (비행 제어 등)
        Soft,   // 위반 시 성능 저하만 발생 (레이더 등)
        None    // 데드라인 없음 (IdleTask)
    }

    /// <summary>
    /// RTOS 태스크 인터페이스
    /// 모든 태스크가 구현해야 할 계약 (상태 머신 기반)
    /// </summary>
    public interface IRTOSTask
    {
        /// <summary>
        /// 태스크 이름 (식별용)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 현재 진행 중인 Step 인덱스 (상태 머신의 가상 PC)
        /// </summary>
        int CurrentStep { get; }

        /// <summary>
        /// 총 Step 개수
        /// </summary>
        int TotalSteps { get; }

        /// <summary>
        /// 현재 Step의 예상 실행 시간 (WCET, 초 단위)
        /// 커널이 시간 예산 계산에 사용
        /// </summary>
        float CurrentStepWCET { get; }

        /// <summary>
        /// 모든 Step이 완료되었는지 여부
        /// true면 태스크가 Waiting 상태로 전환됨
        /// </summary>
        bool IsWorkComplete { get; }

        /// <summary>
        /// 태스크 초기화
        /// 시스템 시작 시 한 번 호출됨
        /// </summary>
        void Initialize();

        /// <summary>
        /// 현재 Step 하나만 실행
        /// 커널에 의해 호출되며, 한 Step 실행 후 즉시 반환해야 함
        /// </summary>
        void ExecuteStep();

        /// <summary>
        /// 태스크를 처음 상태로 리셋 (다음 주기 준비)
        /// </summary>
        void ResetForNextPeriod();

        /// <summary>
        /// 태스크 정리
        /// 시스템 종료 시 또는 태스크 제거 시 호출됨
        /// </summary>
        void Cleanup();

        /// <summary>
        /// 데드라인 미스 발생 시 호출되는 핸들러
        /// </summary>
        void OnDeadlineMiss();
    }
}
