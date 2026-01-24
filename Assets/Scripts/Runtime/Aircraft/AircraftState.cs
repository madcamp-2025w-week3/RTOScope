/*
 * AircraftState.cs - 항공기 상태 공유 메모리
 * 
 * [역할] RTOS와 Unity Runtime 간의 공유 메모리
 * [위치] Runtime Layer > Aircraft (Unity API 최소화)
 * 
 * [설계 의도]
 * - RTOS 태스크는 이 클래스를 통해 센서 데이터를 읽음
 * - FlightActuator는 RTOS 명령을 이 클래스에 기록
 * - SensorArray는 Unity 상태를 이 클래스에 기록
 * - 스레드 안전성 고려 필요
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
        // 운동 정보 (Motion)
        // =====================================================================

        /// <summary>현재 속도 (m/s)</summary>
        public float Velocity { get; set; }

        /// <summary>고도 (m)</summary>
        public float Altitude { get; set; }

        /// <summary>수직 속도 (m/s, 양수 = 상승)</summary>
        public float VerticalSpeed { get; set; }

        /// <summary>현재 위치</summary>
        public Vector3 Position { get; set; }

        // =====================================================================
        // 사용자 입력 (Keyboard/Joystick → SensorArray → 여기)
        // =====================================================================

        /// <summary>피치 입력 (-1 ~ 1, 위/아래 키)</summary>
        public float PitchInput { get; set; }

        /// <summary>롤 입력 (-1 ~ 1, 좌/우 키)</summary>
        public float RollInput { get; set; }

        /// <summary>요 입력 (-1 ~ 1, Q/E 키)</summary>
        public float YawInput { get; set; }

        /// <summary>스로틀 입력 (-1 ~ 1, W/S 키로 증감)</summary>
        public float ThrottleInput { get; set; }

        // =====================================================================
        // 제어 명령 (RTOS FlightControlTask → Actuator)
        // =====================================================================

        /// <summary>피치 명령 (-1 ~ 1)</summary>
        public float PitchCommand { get; set; }

        /// <summary>롤 명령 (-1 ~ 1)</summary>
        public float RollCommand { get; set; }

        /// <summary>요 명령 (-1 ~ 1)</summary>
        public float YawCommand { get; set; }

        /// <summary>스로틀 명령 (0 ~ 1)</summary>
        public float ThrottleCommand { get; set; }

        // =====================================================================
        // 엔진/시스템 상태
        // =====================================================================

        /// <summary>엔진 RPM</summary>
        public float EngineRPM { get; set; }

        /// <summary>연료량 (%)</summary>
        public float FuelLevel { get; set; }

        public AircraftState()
        {
            // 초기값 설정 - 시작 시 중간 스로틀로 비행
            ThrottleInput = 0.5f;
            ThrottleCommand = 0.5f;
            FuelLevel = 100f;
        }

        /// <summary>
        /// 상태를 초기화한다.
        /// </summary>
        public void Reset()
        {
            Pitch = Roll = Yaw = 0f;
            Velocity = 0f;
            Altitude = 0f;
            VerticalSpeed = 0f;
            Position = Vector3.zero;
            PitchCommand = RollCommand = YawCommand = 0f;
            ThrottleCommand = 0.5f;
        }
    }
}
