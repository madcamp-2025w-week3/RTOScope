/*
 * SensorArray.cs - 센서 어레이 (HAL 입력 레이어)
 * 
 * [역할] Unity Rigidbody 상태를 읽어 AircraftState에 기록
 * [위치] Runtime Layer > Hardware (Unity MonoBehaviour)
 * 
 * [설계 의도]
 * - HAL(Hardware Abstraction Layer)의 입력 부분
 * - Unity Rigidbody에서 물리 상태를 읽음
 * - RTOS 태스크는 AircraftState를 통해 간접적으로 접근
 * - 모든 Unity API 호출은 이 레이어에서만 수행
 * 
 * [v2.0 업데이트]
 * - Rigidbody 참조 추가
 * - 속도 벡터, 각속도, G-Force 읽기 구현
 * - 받음각(AoA), 옆미끄럼각(Beta) 계산
 * - 대기 밀도, 동압 계산
 */

using UnityEngine;

namespace RTOScope.Runtime.Hardware
{
    /// <summary>
    /// Unity Rigidbody 상태를 읽어 AircraftState에 기록하는 센서 어레이
    /// HAL Layer: RTOS와 Unity 물리 엔진 사이의 입력 인터페이스
    /// </summary>
    public class SensorArray : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("References")]
        [SerializeField] private Transform _aircraftTransform;
        [SerializeField] private Rigidbody _rigidbody;

        [Header("물리 상수")]
        [Tooltip("해수면 표준 대기 밀도 (kg/m³)")]
        [SerializeField] private float _seaLevelDensity = 1.225f;

        [Tooltip("대기 스케일 높이 (m) - 밀도 감소율 결정")]
        [SerializeField] private float _scaleHeight = 8500f;

        [Tooltip("최소 속도 임계값 (m/s) - AoA 계산 발산 방지")]
        [SerializeField] private float _minVelocityThreshold = 1f;

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private Vector3 _previousVelocity;
        private float _previousAltitude;

        /// <summary>AircraftState 참조 (RTOSRunner에서 주입)</summary>
        public Aircraft.AircraftState State { get; set; }

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Start()
        {
            if (_aircraftTransform != null)
            {
                _previousAltitude = _aircraftTransform.position.y;
            }

            if (_rigidbody != null)
            {
                _previousVelocity = _rigidbody.velocity;
            }
        }

        private void FixedUpdate()
        {
            // FixedUpdate에서 물리 데이터를 읽어 동기화 보장
            if (State == null) return;

            ReadRigidbodySensors();
            ReadAttitudeSensors();
            ReadKeyboardInput();
        }

        // =====================================================================
        // 센서 읽기 메서드
        // =====================================================================

        /// <summary>
        /// Rigidbody에서 물리 데이터를 읽어 AircraftState에 기록
        /// </summary>
        private void ReadRigidbodySensors()
        {
            if (_rigidbody == null || _aircraftTransform == null) return;

            float dt = Time.fixedDeltaTime;

            // -----------------------------------------------------------------
            // 속도 데이터
            // -----------------------------------------------------------------
            State.VelocityVector = _rigidbody.velocity;
            State.Velocity = _rigidbody.velocity.magnitude;

            // 로컬 좌표계 속도 (기체 기준)
            State.LocalVelocity = _aircraftTransform.InverseTransformDirection(_rigidbody.velocity);

            // -----------------------------------------------------------------
            // 각속도 (로컬 좌표계로 변환 필수!)
            // Unity의 angularVelocity는 월드 좌표계이므로 로컬로 변환
            // -----------------------------------------------------------------
            State.AngularVelocity = _aircraftTransform.InverseTransformDirection(_rigidbody.angularVelocity);

            // -----------------------------------------------------------------
            // G-Force 계산
            // 가속도 = (현재속도 - 이전속도) / dt
            // G-Force = (가속도 + 중력) / 9.81
            // -----------------------------------------------------------------
            if (dt > 0.0001f)
            {
                Vector3 acceleration = (_rigidbody.velocity - _previousVelocity) / dt;
                Vector3 totalAcceleration = acceleration - Physics.gravity; // 중력 상쇄
                State.GForce = totalAcceleration.magnitude / 9.81f + 1f; // +1 for 1G baseline
            }
            _previousVelocity = _rigidbody.velocity;

            // -----------------------------------------------------------------
            // 받음각 (Angle of Attack) 및 옆미끄럼각 (Sideslip Angle)
            // 저속에서 발산 방지를 위해 최소 속도 임계값 적용
            // -----------------------------------------------------------------
            if (State.Velocity > _minVelocityThreshold)
            {
                // 받음각: 로컬 Z축(전방)과 속도 벡터 사이의 수직 성분 각도
                // AoA = atan2(-Vy, Vz) (기체 좌표계)
                State.AngleOfAttack = Mathf.Atan2(-State.LocalVelocity.y, State.LocalVelocity.z) * Mathf.Rad2Deg;

                // 옆미끄럼각: 로컬 Z축과 속도 벡터 사이의 수평 성분 각도
                // Beta = atan2(Vx, Vz)
                State.SideslipAngle = Mathf.Atan2(State.LocalVelocity.x, State.LocalVelocity.z) * Mathf.Rad2Deg;
            }
            else
            {
                // 저속에서는 0으로 고정
                State.AngleOfAttack = 0f;
                State.SideslipAngle = 0f;
            }

            // -----------------------------------------------------------------
            // 대기 밀도 계산 (국제 표준 대기 모델 단순화)
            // ρ = ρ0 * exp(-h / H)
            // -----------------------------------------------------------------
            State.AirDensity = CalculateAirDensity(State.Altitude);

            // -----------------------------------------------------------------
            // 동압 계산 (Dynamic Pressure)
            // q = 0.5 * ρ * V²
            // -----------------------------------------------------------------
            State.DynamicPressure = 0.5f * State.AirDensity * State.Velocity * State.Velocity;
        }

        /// <summary>
        /// Transform에서 자세 데이터를 읽어 AircraftState에 기록
        /// </summary>
        private void ReadAttitudeSensors()
        {
            if (_aircraftTransform == null) return;

            // 자세 정보 (Euler 각도)
            Vector3 euler = _aircraftTransform.eulerAngles;
            State.Pitch = NormalizeAngle(euler.x);
            State.Roll = NormalizeAngle(euler.z);
            State.Yaw = euler.y;

            // 위치 정보
            State.Position = _aircraftTransform.position;
            State.Altitude = _aircraftTransform.position.y;

            // 수직 속도 계산
            float dt = Time.fixedDeltaTime;
            if (dt > 0.0001f)
            {
                State.VerticalSpeed = (_aircraftTransform.position.y - _previousAltitude) / dt;
            }
            _previousAltitude = _aircraftTransform.position.y;
        }

        /// <summary>
        /// 키보드 입력을 읽어 AircraftState에 저장
        /// </summary>
        private void ReadKeyboardInput()
        {
            // -----------------------------------------------------------------
            // 피치: 위/아래 화살표
            // 비행 시뮬레이터 관례: '아래 키'를 누르면 기수가 상승 (조종간 당김)
            // -----------------------------------------------------------------
            float pitchInput = 0f;
            if (Input.GetKey(KeyCode.UpArrow))
                pitchInput = 1f;  // 기수 하강 (조종간 밀기)
            else if (Input.GetKey(KeyCode.DownArrow))
                pitchInput = -1f; // 기수 상승 (조종간 당기기)
            State.PitchInput = pitchInput;

            // -----------------------------------------------------------------
            // 롤: 좌/우 화살표
            // -----------------------------------------------------------------
            float rollInput = 0f;
            if (Input.GetKey(KeyCode.LeftArrow))
                rollInput = -1f; // 좌측 롤
            else if (Input.GetKey(KeyCode.RightArrow))
                rollInput = 1f;  // 우측 롤
            State.RollInput = rollInput;

            // -----------------------------------------------------------------
            // 요: Q/E
            // -----------------------------------------------------------------
            float yawInput = 0f;
            if (Input.GetKey(KeyCode.Q))
                yawInput = -1f; // 좌회전 (Left Rudder)
            else if (Input.GetKey(KeyCode.E))
                yawInput = 1f;  // 우회전 (Right Rudder)
            State.YawInput = yawInput;

            // -----------------------------------------------------------------
            // 스로틀: Left Shift (가속) / Left Ctrl (감속)
            // -----------------------------------------------------------------
            float throttleChange = 0f;
            if (Input.GetKey(KeyCode.LeftShift))
                throttleChange = 1.0f * Time.deltaTime; // 초당 100% 증가 (더 빠른 응답)
            else if (Input.GetKey(KeyCode.LeftControl))
                throttleChange = -1.0f * Time.deltaTime; // 초당 100% 감소

            State.ThrottleInput = Mathf.Clamp(State.ThrottleInput + throttleChange, 0f, 1f);
        }

        // =====================================================================
        // 유틸리티 메서드
        // =====================================================================

        /// <summary>
        /// 고도에 따른 대기 밀도 계산 (ISA 단순화 모델)
        /// </summary>
        /// <param name="altitude">고도 (m)</param>
        /// <returns>대기 밀도 (kg/m³)</returns>
        private float CalculateAirDensity(float altitude)
        {
            // 음수 고도 방지
            float h = Mathf.Max(0f, altitude);
            // 지수 감소 모델: ρ = ρ0 * exp(-h/H)
            return _seaLevelDensity * Mathf.Exp(-h / _scaleHeight);
        }

        /// <summary>
        /// 각도를 -180 ~ 180 범위로 정규화
        /// </summary>
        private float NormalizeAngle(float angle)
        {
            while (angle > 180f) angle -= 360f;
            while (angle < -180f) angle += 360f;
            return angle;
        }
    }
}
