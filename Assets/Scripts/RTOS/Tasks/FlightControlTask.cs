/*
 * FlightControlTask.cs - 비행 제어 태스크
 *
 * [역할] 항공기 자세 및 비행 경로 제어 - 최상위 우선순위
 * [위치] RTOS Layer > Tasks
 * [우선순위] Critical (Hard Deadline) - 위반 시 시스템 실패
 *
 * [데이터 흐름]
 * SensorArray → AircraftState.Input → FlightControlTask → AircraftState.Command → FlightActuator
 *
 * [상태 머신 설계]
 * Step 0: 센서/입력 데이터 읽기
 * Step 1: 피치 명령 계산
 * Step 2: 롤 명령 계산
 * Step 3: 요 명령 계산
 * Step 4: 스로틀 및 명령 출력
 */

using RTOScope.RTOS.Kernel;
using RTOScope.Runtime.Aircraft;

namespace RTOScope.RTOS.Tasks
{
    /// <summary>
    /// 비행 제어 태스크 (Flight Control System)
    /// 주기: 10ms (100Hz) - 고빈도 제어 루프
    /// Hard Deadline - 실시간으로 기체의 자세를 계산하고 안정화
    /// </summary>
    public class FlightControlTask : IRTOSTask
    {
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_READ_INPUTS = 0;
        private const int STEP_COMPUTE_PITCH = 1;
        private const int STEP_COMPUTE_ROLL = 2;
        private const int STEP_COMPUTE_YAW = 3;
        private const int STEP_OUTPUT_COMMANDS = 4;
        private const int TOTAL_STEPS = 5;

        // 각 Step의 WCET (초 단위)
        private static readonly float[] _stepWCETs = {
            0.0005f,  // Step 0: 입력 읽기 (0.5ms)
            0.0008f,  // Step 1: 피치 계산 (0.8ms)
            0.0008f,  // Step 2: 롤 계산 (0.8ms)
            0.0005f,  // Step 3: 요 계산 (0.5ms)
            0.0004f   // Step 4: 출력 (0.4ms)
        };            // 총 WCET: 3.0ms

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;

        // AircraftState 참조 (외부에서 주입)
        private AircraftState _state;

        // 읽어온 입력 값 (캐시)
        private float _pitchInput;
        private float _rollInput;
        private float _yawInput;
        private float _throttleInput;

        // 계산된 명령 값
        private float _pitchCommand;
        private float _rollCommand;
        private float _yawCommand;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "FlightControl";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        // =====================================================================
        // 생성자
        // =====================================================================

        public FlightControlTask()
        {
            _currentStep = 0;
        }

        /// <summary>
        /// AircraftState 참조 설정 (RTOSRunner에서 호출)
        /// </summary>
        public void SetState(AircraftState state)
        {
            _state = state;
        }

        // =====================================================================
        // IRTOSTask 구현
        // =====================================================================

        public void Initialize()
        {
            _currentStep = 0;
            _pitchCommand = 0f;
            _rollCommand = 0f;
            _yawCommand = 0f;
        }

        public void ExecuteStep()
        {
            switch (_currentStep)
            {
                case STEP_READ_INPUTS:
                    ReadInputs();
                    _currentStep++;
                    break;

                case STEP_COMPUTE_PITCH:
                    ComputePitchCommand();
                    _currentStep++;
                    break;

                case STEP_COMPUTE_ROLL:
                    ComputeRollCommand();
                    _currentStep++;
                    break;

                case STEP_COMPUTE_YAW:
                    ComputeYawCommand();
                    _currentStep++;
                    break;

                case STEP_OUTPUT_COMMANDS:
                    OutputCommands();
                    _currentStep++;
                    break;
            }
        }

        public void ResetForNextPeriod()
        {
            _currentStep = 0;
        }

        public void Cleanup()
        {
            if (_state != null)
            {
                _state.ThrottleCommand = 0f;
                _state.PitchCommand = 0f;
                _state.RollCommand = 0f;
                _state.YawCommand = 0f;
            }
        }

        public void OnDeadlineMiss()
        {
            // Hard Deadline 미스 시 비상 처리
            // 현재 명령 유지 (안전 조치)
        }

        // =====================================================================
        // 비공개 메서드 (각 Step의 실제 로직)
        // =====================================================================

        private void ReadInputs()
        {
            if (_state == null) return;

            // AircraftState에서 사용자 입력 읽기
            _pitchInput = _state.PitchInput;
            _rollInput = _state.RollInput;
            _yawInput = _state.YawInput;
            _throttleInput = _state.ThrottleInput;
        }

        private void ComputePitchCommand()
        {
            // 사용자 입력을 그대로 명령으로 전달 (Direct Control)
            // 향후 PID 제어로 확장 가능
            _pitchCommand = _pitchInput;
        }

        private void ComputeRollCommand()
        {
            _rollCommand = _rollInput;
        }

        private void ComputeYawCommand()
        {
            _yawCommand = _yawInput;
        }

        private void OutputCommands()
        {
            if (_state == null) return;

            // AircraftState에 제어 명령 출력
            _state.PitchCommand = _pitchCommand;
            _state.RollCommand = _rollCommand;
            _state.YawCommand = _yawCommand;
            _state.ThrottleCommand = _throttleInput; // 스로틀은 입력값 그대로
        }
    }
}
