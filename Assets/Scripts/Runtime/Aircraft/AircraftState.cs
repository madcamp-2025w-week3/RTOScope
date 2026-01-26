/*
 * AircraftState.cs - 항공기 상태 공유 메모리
 * 
 * [역할] RTOS와 Unity Runtime 간의 공유 메모리
 * [위치] Runtime Layer > Aircraft (Unity API 최소화)
 * 
 * [설계 의도]
 * - RTOS 태스크는 이 클래스를 통해 센서 데이터를 읽음
 * - FlightActuator는 RTOS 명령을 이 클래스에서 읽어 Rigidbody에 적용
 * - SensorArray는 Unity Rigidbody 상태를 이 클래스에 기록
 * - 스레드 안전성 고려 필요
 * 
 * [v2.0 업데이트]
 * - Rigidbody 기반 물리 데이터 추가
 * - 공기역학 명령 벡터 추가
 * - 공기역학 계수 저장 필드 추가
 */

using UnityEngine;

namespace RTOScope.Runtime.Aircraft
{
    /// <summary>
    /// 항공기의 현재 상태를 저장하는 공유 메모리
    /// RTOS ↔ Unity 간 데이터 교환 지점
    /// </summary>
    public class AircraftState
    {
        // =====================================================================
        // 자세 정보 (Attitude)
        // =====================================================================

        /// <summary>피치 각도 (도, 양수 = 기수 상승)</summary>
        public float Pitch { get; set; }

        /// <summary>롤 각도 (도, 양수 = 우측 날개 하강)</summary>
        public float Roll { get; set; }

        /// <summary>요 각도 / 헤딩 (도, 0~360)</summary>
        public float Yaw { get; set; }

        // =====================================================================
        // 운동 정보 (Motion) - 스칼라
        // =====================================================================

        /// <summary>현재 속력 (m/s) - 속도 벡터의 크기</summary>
        public float Velocity { get; set; }

        /// <summary>고도 (m)</summary>
        public float Altitude { get; set; }

        /// <summary>수직 속도 (m/s, 양수 = 상승)</summary>
        public float VerticalSpeed { get; set; }

        /// <summary>현재 위치</summary>
        public Vector3 Position { get; set; }

        // =====================================================================
        // Rigidbody 센서 데이터 (SensorArray에서 기록)
        // =====================================================================

        /// <summary>속도 벡터 (월드 좌표, m/s)</summary>
        public Vector3 VelocityVector { get; set; }

        /// <summary>로컬 속도 벡터 (기체 좌표계, m/s)</summary>
        public Vector3 LocalVelocity { get; set; }

        /// <summary>각속도 벡터 (rad/s)</summary>
        public Vector3 AngularVelocity { get; set; }

        /// <summary>G-Force (중력 가속도 배수)</summary>
        public float GForce { get; set; }

        /// <summary>받음각 (Angle of Attack, 도)</summary>
        public float AngleOfAttack { get; set; }

        /// <summary>옆미끄럼각 (Sideslip Angle, 도)</summary>
        public float SideslipAngle { get; set; }

        /// <summary>대기 밀도 (kg/m³)</summary>
        public float AirDensity { get; set; }

        /// <summary>동압 (Dynamic Pressure, Pa)</summary>
        public float DynamicPressure { get; set; }

        // =====================================================================
        // 사용자 입력 (Keyboard/Joystick → SensorArray → 여기)
        // =====================================================================

        /// <summary>피치 입력 (-1 ~ 1, 위/아래 키)</summary>
        public float PitchInput { get; set; }

        /// <summary>롤 입력 (-1 ~ 1, 좌/우 키)</summary>
        public float RollInput { get; set; }

        /// <summary>요 입력 (-1 ~ 1, Q/E 키)</summary>
        public float YawInput { get; set; }

        /// <summary>스로틀 입력 (0 ~ 1, Shift/Ctrl 키로 증감)</summary>
        public float ThrottleInput { get; set; }

        // =====================================================================
        // 중간 계산값 (FlightControlTask 내부에서 사용)
        // =====================================================================

        /// <summary>양력 계수 (CL)</summary>
        public float LiftCoefficient { get; set; }

        /// <summary>항력 계수 (CD)</summary>
        public float DragCoefficient { get; set; }

        /// <summary>양력 (N)</summary>
        public float LiftForce { get; set; }

        /// <summary>항력 (N)</summary>
        public float DragForce { get; set; }

        // =====================================================================
        // 제어 명령 - 레거시 (하위 호환성 유지)
        // =====================================================================

        /// <summary>피치 명령 (-1 ~ 1) - 레거시</summary>
        public float PitchCommand { get; set; }

        /// <summary>롤 명령 (-1 ~ 1) - 레거시</summary>
        public float RollCommand { get; set; }

        /// <summary>요 명령 (-1 ~ 1) - 레거시</summary>
        public float YawCommand { get; set; }

        /// <summary>스로틀 명령 (0 ~ 1) - 레거시</summary>
        public float ThrottleCommand { get; set; }

        // =====================================================================
        // 공기역학 명령 (FlightControlTask에서 계산 → FlightActuator에서 적용)
        // =====================================================================

        /// <summary>추력 벡터 (기체 좌표계, N) - 엔진 추력</summary>
        public Vector3 ThrustForceCommand { get; set; }

        /// <summary>공기역학력 벡터 (기체 좌표계, N) - 양력 + 항력 합성</summary>
        public Vector3 AeroForceCommand { get; set; }

        /// <summary>토크 벡터 (기체 좌표계, N·m) - 피치/롤/요 제어</summary>
        public Vector3 TorqueCommand { get; set; }

        // =====================================================================
        // 엔진/시스템 상태
        // =====================================================================

        /// <summary>엔진 RPM</summary>
        public float EngineRPM { get; set; }

        /// <summary>연료량 (%)</summary>
        public float FuelLevel { get; set; }

        // =====================================================================
        // 생성자
        // =====================================================================

        public AircraftState()
        {
            // 초기값 설정 - 시작 시 중간 스로틀로 비행
            ThrottleInput = 0.5f;
            ThrottleCommand = 0.5f;
            FuelLevel = 100f;
            AirDensity = 1.225f; // 해수면 표준 대기 밀도
            GForce = 1f;
        }

        /// <summary>
        /// 상태를 초기화한다.
        /// </summary>
        public void Reset()
        {
            // 자세 초기화
            Pitch = Roll = Yaw = 0f;

            // 운동 상태 초기화
            Velocity = 0f;
            Altitude = 0f;
            VerticalSpeed = 0f;
            Position = Vector3.zero;
            VelocityVector = Vector3.zero;
            LocalVelocity = Vector3.zero;
            AngularVelocity = Vector3.zero;

            // 공기역학 데이터 초기화
            AngleOfAttack = 0f;
            SideslipAngle = 0f;
            GForce = 1f;
            AirDensity = 1.225f;
            DynamicPressure = 0f;
            LiftCoefficient = 0f;
            DragCoefficient = 0f;
            LiftForce = 0f;
            DragForce = 0f;

            // 명령 초기화
            PitchCommand = RollCommand = YawCommand = 0f;
            ThrottleCommand = 0.5f;
            ThrustForceCommand = Vector3.zero;
            AeroForceCommand = Vector3.zero;
            TorqueCommand = Vector3.zero;
        }
    }
}
