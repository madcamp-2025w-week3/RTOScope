/*
 * FlightControlTask.cs - 비행 제어 태스크
 *
 * [역할] 항공기 자세 및 비행 경로 제어 - 최상위 우선순위
 * [위치] RTOS Layer > Tasks (Unity API 사용 금지)
 * [우선순위] Critical (Hard Deadline) - 위반 시 시스템 실패
 *
 * [상태 머신 설계]
 * Step 0: 센서 데이터 읽기
 * Step 1: PID 계산 (Pitch)
 * Step 2: PID 계산 (Roll)
 * Step 3: PID 계산 (Yaw)
 * Step 4: 액추에이터 명령 출력
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
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_READ_SENSORS = 0;
        private const int STEP_PID_PITCH = 1;
        private const int STEP_PID_ROLL = 2;
        private const int STEP_PID_YAW = 3;
        private const int STEP_OUTPUT_COMMANDS = 4;
        private const int TOTAL_STEPS = 5;

        // 각 Step의 WCET (초 단위)
        private static readonly float[] _stepWCETs = {
            0.0005f,  // Step 0: 센서 읽기 (0.5ms)
            0.0008f,  // Step 1: PID Pitch (0.8ms)
            0.0008f,  // Step 2: PID Roll (0.8ms)
            0.0005f,  // Step 3: PID Yaw (0.5ms)
            0.0004f   // Step 4: 출력 (0.4ms)
        };                // 총 WCET: 3.0ms

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;

        // 센서 데이터 (가상의 값)
        private float _currentPitch;
        private float _currentRoll;
        private float _currentYaw;

        // 제어 명령 출력
        private float _pitchCommand;
        private float _rollCommand;
        private float _yawCommand;
        private float _throttleCommand;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "FlightControl";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        // 제어 명령 (외부에서 읽기용)
        public float PitchCommand => _pitchCommand;
        public float RollCommand => _rollCommand;
        public float YawCommand => _yawCommand;
        public float ThrottleCommand => _throttleCommand;

        // =====================================================================
        // 생성자
        // =====================================================================

        public FlightControlTask()
        {
            _currentStep = 0;
            _pitchCommand = 0f;
            _rollCommand = 0f;
            _yawCommand = 0f;
            _throttleCommand = 0.5f;
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
            _throttleCommand = 0.5f;
        }

        public void ExecuteStep()
        {
            switch (_currentStep)
            {
                case STEP_READ_SENSORS:
                    ReadSensors();
                    _currentStep++;
                    break;

                case STEP_PID_PITCH:
                    ComputePitchPID();
                    _currentStep++;
                    break;

                case STEP_PID_ROLL:
                    ComputeRollPID();
                    _currentStep++;
                    break;

                case STEP_PID_YAW:
                    ComputeYawPID();
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
            _throttleCommand = 0f;
        }

        public void OnDeadlineMiss()
        {
            // Hard Deadline 미스 시 비상 처리
            // 현재 명령 유지 (안전 조치)
        }

        // =====================================================================
        // 비공개 메서드 (각 Step의 실제 로직)
        // =====================================================================

        private void ReadSensors()
        {
            // TODO: AircraftState에서 실제 센서 데이터 읽기
            // 현재는 더미 값
            _currentPitch = 0f;
            _currentRoll = 0f;
            _currentYaw = 0f;
        }

        private void ComputePitchPID()
        {
            // TODO: 실제 PID 계산
            float targetPitch = 0f;
            float error = targetPitch - _currentPitch;
            _pitchCommand = error * 0.1f; // 단순 P 제어
        }

        private void ComputeRollPID()
        {
            // TODO: 실제 PID 계산
            float targetRoll = 0f;
            float error = targetRoll - _currentRoll;
            _rollCommand = error * 0.1f;
        }

        private void ComputeYawPID()
        {
            // TODO: 실제 PID 계산
            float targetYaw = 0f;
            float error = targetYaw - _currentYaw;
            _yawCommand = error * 0.1f;
        }

        private void OutputCommands()
        {
            // TODO: AircraftState에 명령 출력
            // 현재는 내부 변수에만 저장
        }
    }
}
