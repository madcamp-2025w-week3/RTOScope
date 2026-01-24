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
        
        [Header("Settings")]
        [SerializeField] private float _maxSpeed = 200f;
        [SerializeField] private float _minSpeed = 25f;
        [SerializeField] private float _pitchRate = 50f;
        [SerializeField] private float _rollRate = 50f;
        [SerializeField] private float _yawRate = 20f;

        /// <summary>AircraftState 참조 (RTOSRunner에서 주입)</summary>
        public Aircraft.AircraftState State { get; set; }

        private float _currentSpeed;

        private void Update()
        {
            if (State == null || _aircraftTransform == null) return;

            ApplyControlInputs();
        }

        private void ApplyControlInputs()
        {
            // TODO: RTOS 명령을 Transform에 적용
            // 1. State.ThrottleCommand → 속도 계산
            // 2. State.PitchCommand → X축 회전
            // 3. State.RollCommand → Z축 회전
            // 4. State.YawCommand → Y축 회전
            // 5. 전진 이동

            float dt = Time.deltaTime;
            
            // 속도 계산
            float targetSpeed = Mathf.Lerp(_minSpeed, _maxSpeed, State.ThrottleCommand);
            _currentSpeed = Mathf.Lerp(_currentSpeed, targetSpeed, dt * 5f);

            // 회전 적용
            _aircraftTransform.Rotate(Vector3.right * State.PitchCommand * _pitchRate * dt);
            _aircraftTransform.Rotate(Vector3.up * State.YawCommand * _yawRate * dt);
            _aircraftTransform.Rotate(Vector3.forward * -State.RollCommand * _rollRate * dt);

            // 이동 적용
            _aircraftTransform.position += _aircraftTransform.forward * _currentSpeed * dt;

            // 상태 업데이트
            State.Velocity = _currentSpeed;
            State.Altitude = _aircraftTransform.position.y;
        }
    }
}
