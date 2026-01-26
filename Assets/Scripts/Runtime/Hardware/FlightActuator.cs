/*
 * FlightActuator.cs - 비행 제어 액추에이터
 *
 * [역할] RTOS 명령을 Unity Transform에 적용
 * [위치] Runtime Layer > Hardware (Unity MonoBehaviour)
 *
 * [설계 의도]
 * - HAL(Hardware Abstraction Layer)의 출력 부분
 * - AircraftState의 명령을 읽어 실제 물리 적용
 * - RTOS 태스크는 이 클래스를 직접 호출하지 않음
 */

using UnityEngine;

namespace RTOScope.Runtime.Hardware
{
    /// <summary>
    /// RTOS 제어 명령을 Unity Transform에 적용하는 액추에이터
    /// </summary>
    public class FlightActuator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _aircraftTransform;

        [Header("Engine Power")]
        [SerializeField] private float _maxSpeed = 200f;      // 최대 속도
        [SerializeField] private float _minSpeed = 30f;       // 최소 속도 (실속 속도)
        [SerializeField] private float _acceleration = 20f;   // 스로틀 반응 속도

        [Header("Control Sensitivity")]
        [SerializeField] private float _pitchRate = 50f;      // 상하 회전 민감도
        [SerializeField] private float _rollRate = 50f;       // 좌우 롤 민감도
        [SerializeField] private float _yawRate = 20f;        // 좌우 요 민감도

        /// <summary>AircraftState 참조 (RTOSRunner에서 주입)</summary>
        public Aircraft.AircraftState State { get; set; }

        private float _currentSpeed;

        public float CurrentSpeed => _currentSpeed;

        private void Start()
        {
            // 시작 시 중간 속도로 비행
            _currentSpeed = _maxSpeed * 0.5f;
        }

        private void Update()
        {
            if (State == null || _aircraftTransform == null) return;

            ApplyControlInputs();
        }

        private void ApplyControlInputs()
        {
            float dt = Time.deltaTime;

            // 1. 속도 계산 (부드러운 가속/감속)
            float targetSpeed = Mathf.Lerp(_minSpeed, _maxSpeed, State.ThrottleCommand);
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, dt * _acceleration);

            // 2. 회전 적용 (RTOS FlightControlTask에서 계산된 명령)
            _aircraftTransform.Rotate(Vector3.right * State.PitchCommand * _pitchRate * dt);
            _aircraftTransform.Rotate(Vector3.up * State.YawCommand * _yawRate * dt);
            _aircraftTransform.Rotate(Vector3.forward * -State.RollCommand * _rollRate * dt);

            // 3. 전진 이동
            _aircraftTransform.position += _aircraftTransform.forward * _currentSpeed * dt;

            // 4. 상태 업데이트 (Sensor가 읽을 수 있도록)
            State.Velocity = _currentSpeed;
            State.Altitude = _aircraftTransform.position.y;
        }

        private void OnGUI()
        {
            if (State == null) return;

            // 조종석 HUD 정보 표시
            GUI.Box(new Rect(20, 20, 220, 80), "RTOS FLIGHT SYSTEM");
            GUI.Label(new Rect(30, 45, 200, 20),
                $"THR: {(int)(State.ThrottleCommand * 100)}%  |  SPD: {(int)_currentSpeed} km/h");
            GUI.Label(new Rect(30, 65, 200, 20),
                $"ALT: {(int)State.Altitude} m  |  VS: {(int)State.VerticalSpeed} m/s");
            GUI.Label(new Rect(30, 85, 200, 20),
                $"Pitch: {State.Pitch:F1}° | Roll: {State.Roll:F1}°");
        }
    }
}
