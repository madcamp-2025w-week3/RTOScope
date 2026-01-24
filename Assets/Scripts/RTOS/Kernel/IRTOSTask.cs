/*
 * ============================================================================
 * IRTOSTask.cs
 * ============================================================================
 * 
 * [모듈 역할]
 * RTOS 태스크 인터페이스 - 순수 실행 로직(Execute)만 담당
 * 
 * [아키텍처 위치]
 * RTOS Layer > Kernel > IRTOSTask
 * - 순수 C# 코드로 작성 (Unity API 사용 금지)
 * - 태스크의 "할 일"만 정의 (상태 관리는 TCB에서)
 * 
 * [설계 철학]
 * - 인터페이스 분리 원칙 (ISP): 태스크 로직과 관리 정보 분리
 * - TCB(Task Control Block)가 이 인터페이스를 참조하여 관리
 * - "나는 비행 제어를 한다", "나는 레이더를 돌린다" 같은 역할만 정의
 * 
 * [사용 예]
 * - FlightControlTask : IRTOSTask (비행 제어 로직)
 * - RadarTask : IRTOSTask (레이더 처리 로직)
 * - HealthMonitor : IRTOSTask (시스템 모니터링 로직)
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
        Suspended   // 일시 정지됨
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
        Idle = 4        // 가장 낮은 우선순위 (백그라운드 작업)
    }

    /// <summary>
    /// 데드라인 타입
    /// </summary>
    public enum DeadlineType
    {
        Hard,   // 위반 시 시스템 실패 (비행 제어 등)
        Soft    // 위반 시 성능 저하만 발생 (레이더 등)
    }

    /// <summary>
    /// RTOS 태스크 인터페이스
    /// 모든 태스크가 구현해야 할 계약
    /// </summary>
    public interface IRTOSTask
    {
        /// <summary>
        /// 태스크 이름 (식별용)
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 태스크 초기화
        /// 시스템 시작 시 한 번 호출됨
        /// </summary>
        void Initialize();

        /// <summary>
        /// 태스크 메인 실행 로직
        /// 스케줄러에 의해 매 실행 시점마다 호출됨
        /// </summary>
        /// <param name="deltaTime">이전 실행으로부터 경과 시간 (초)</param>
        void Execute(float deltaTime);

        /// <summary>
        /// 태스크 정리
        /// 시스템 종료 시 또는 태스크 제거 시 호출됨
        /// </summary>
        void Cleanup();

        /// <summary>
        /// 데드라인 미스 발생 시 호출되는 핸들러
        /// Hard Deadline 태스크는 여기서 안전 모드 진입 등 처리
        /// </summary>
        void OnDeadlineMiss();
    }
}
