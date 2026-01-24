/*
 * SensorArray.cs - 센서 어레이
 * 
 * [역할] Unity 상태를 읽어 AircraftState에 기록
 * [위치] Runtime Layer > Hardware (Unity MonoBehaviour)
 * 
 * [설계 의도]
 * - HAL(Hardware Abstraction Layer)의 입력 부분
 * - Unity Transform/Rigidbody에서 상태를 읽음
 * - RTOS 태스크는 AircraftState를 통해 간접적으로 접근
 */

using UnityEngine;

namespace RTOScope.Runtime.Hardware
{
    /// <summary>
    /// Unity 상태를 읽어 AircraftState에 기록하는 센서 어레이
    /// </summary>
    public class SensorArray : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _aircraftTransform;

        /// <summary>AircraftState 참조 (RTOSRunner에서 주입)</summary>
        public Aircraft.AircraftState State { get; set; }

        private Vector3 _previousPosition;
        private float _previousAltitude;

        private void Start()
        {
            if (_aircraftTransform != null)
            {
                _previousPosition = _aircraftTransform.position;
                _previousAltitude = _aircraftTransform.position.y;
            }
        }

        private void Update()
        {
            if (State == null || _aircraftTransform == null) return;

            ReadSensors();
        }

        private void ReadSensors()
        {
            // 자세 정보
            Vector3 euler = _aircraftTransform.eulerAngles;
            State.Pitch = NormalizeAngle(euler.x);
            State.Roll = NormalizeAngle(euler.z);
            State.Yaw = euler.y;

            // 위치 정보
            State.Position = _aircraftTransform.position;
            State.Altitude = _aircraftTransform.position.y;

            // 수직 속도 계산
            float dt = Time.deltaTime;
            if (dt > 0)
            {
                State.VerticalSpeed = (_aircraftTransform.position.y - _previousAltitude) / dt;
            }

            _previousPosition = _aircraftTransform.position;
            _previousAltitude = _aircraftTransform.position.y;

            // 키보드 입력 읽기
            ReadKeyboardInput();
        }

        /// <summary>키보드 입력을 읽어 AircraftState에 저장</summary>
        private void ReadKeyboardInput()
        {
            // 피치: 위/아래 화살표
            // 비행 시뮬레이터 국룰: '아래 키'를 누르면 기수가 상승 (invertPitch)
            float pitchInput = 0f;
            if (Input.GetKey(KeyCode.UpArrow))
                pitchInput = 1f;  // 기수 하강 (Pitch Down)
            else if (Input.GetKey(KeyCode.DownArrow))
                pitchInput = -1f; // 기수 상승 (Pitch Up)
            State.PitchInput = pitchInput;

            // 롤: 좌/우 화살표
            float rollInput = 0f;
            if (Input.GetKey(KeyCode.LeftArrow))
                rollInput = -1f; // 좌측 롤
            else if (Input.GetKey(KeyCode.RightArrow))
                rollInput = 1f;  // 우측 롤
            State.RollInput = rollInput;

            // 요: Q/E
            float yawInput = 0f;
            if (Input.GetKey(KeyCode.Q))
                yawInput = -1f; // 좌회전
            else if (Input.GetKey(KeyCode.E))
                yawInput = 1f;  // 우회전
            State.YawInput = yawInput;

            // 스로틀: Left Shift (가속) / Left Ctrl (감속)
            float throttleChange = 0f;
            if (Input.GetKey(KeyCode.LeftShift))
                throttleChange = 0.5f * Time.deltaTime; // 초당 50% 증가
            else if (Input.GetKey(KeyCode.LeftControl))
                throttleChange = -0.5f * Time.deltaTime; // 초당 50% 감소

            State.ThrottleInput = Mathf.Clamp(State.ThrottleInput + throttleChange, 0f, 1f);
        }

        /// <summary>각도를 -180 ~ 180 범위로 정규화</summary>
        private float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
