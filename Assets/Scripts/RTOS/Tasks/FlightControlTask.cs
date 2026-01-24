/*
 * FlightControlTask.cs - 비행 제어 태스크
 * 
 * [역할] 항공기 자세 및 비행 경로 제어 - 최상위 우선순위
 * [위치] RTOS Layer > Tasks (Unity API 사용 금지)
 * [우선순위] Critical (Hard Deadline) - 위반 시 시스템 실패
 * 
 * [구현 예정]
 * - PID 제어 알고리즘 호출
 * - AircraftState에서 센서 데이터 읽기
 * - 액추에이터 명령 생성
 */

using RTOScope.RTOS.Kernel;

namespace RTOScope.RTOS.Tasks
{
    /// <summary>
    /// 비행 제어 태스크 (Flight Control System)
    /// 주기: 10ms (100Hz) - 고빈도 제어 루프
    /// Hard Deadline - 실시간으로 기체의 자세를 계산하고 안정화
    /// </summary>
    public class FlightControlTask : IRTOSTask
    {
        // TODO: AircraftState 참조 추가
        // private readonly AircraftState _state;
        
        // PID 제어 파라미터 (추후 PIDController에서 관리)
        private float _pitchCommand;
        private float _rollCommand;
        private float _yawCommand;
        private float _throttleCommand;

        public string Name => "FlightControl";
        public float PitchCommand => _pitchCommand;
        public float RollCommand => _rollCommand;
        public float YawCommand => _yawCommand;
        public float ThrottleCommand => _throttleCommand;

        public FlightControlTask()
        {
            // 초기 제어 상태 설정
            _pitchCommand = 0f;
            _rollCommand = 0f;
            _yawCommand = 0f;
            _throttleCommand = 0.5f;  // 중간 스로틀
        }

        public void Initialize()
        {
            // TODO: 초기화 로직 구현
            // - 센서 캘리브레이션
            // - PID 게인 로드
            // - 안전 상태 설정
            
            _pitchCommand = 0f;
            _rollCommand = 0f;
            _yawCommand = 0f;
            _throttleCommand = 0.5f;
        }

        public void Execute(float deltaTime)
        {
            // TODO: 실제 비행 제어 로직 구현
            // 1. AircraftState에서 현재 자세/속도 읽기
            // 2. 목표 자세와 비교
            // 3. PID 제어 계산
            // 4. 액추에이터 명령 생성
            
            ComputeControlCommands(deltaTime);
        }

        public void Cleanup()
        {
            // TODO: 정리 로직 구현
            // - 안전 모드로 명령 설정
            // - 리소스 해제
            
            _throttleCommand = 0f;  // 엔진 정지
        }

        public void OnDeadlineMiss()
        {
            // TODO: Hard Deadline 미스 시 비상 처리
            // - 현재 명령 유지 (안전 조치)
            // - 비상 로그 기록
            // - 안전 모드 진입 신호 전송
        }

        private void ComputeControlCommands(float deltaTime)
        {
            // TODO: PID 제어 알고리즘 연결
            // - 현재 상태와 목표 상태의 오차 계산
            // - P, I, D 항 계산
            // - 출력 명령 생성
        }
    }
}
