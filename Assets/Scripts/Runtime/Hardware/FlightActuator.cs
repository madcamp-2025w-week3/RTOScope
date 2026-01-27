/*
 * FlightActuator.cs - 비행 제어 액추에이터 (HAL 출력 레이어)
 *
 * [역할] RTOS 명령을 Unity Rigidbody에 적용
 * [위치] Runtime Layer > Hardware (Unity MonoBehaviour)
 *
 * [설계 의도]
 * - HAL(Hardware Abstraction Layer)의 출력 부분
 * - AircraftState의 명령을 읽어 Rigidbody에 물리력 적용
 * - RTOS 태스크는 이 클래스를 직접 호출하지 않음
 * - 모든 Unity Physics API 호출은 이 레이어에서만 수행
 *
 * [v2.0 업데이트]
 * - Rigidbody.AddRelativeForce/AddRelativeTorque 기반으로 전환
 * - Transform 직접 조작 제거
 * - 명령 벡터 기반 물리 적용
 */

using UnityEngine;

namespace RTOScope.Runtime.Hardware
{
    /// <summary>
    /// RTOS 제어 명령을 Unity Rigidbody에 적용하는 액추에이터
    /// HAL Layer: RTOS와 Unity 물리 엔진 사이의 출력 인터페이스
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class FlightActuator : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("References")]
        [SerializeField] private Rigidbody _rigidbody;

        [Header("물리 제한")]
        [Tooltip("최대 속도 제한 (m/s)")]
        [SerializeField] private float _maxSpeed = 600f;

        [Tooltip("최소 속도 (실속 속도, m/s)")]
        [SerializeField] private float _minSpeed = 50f;

        [Tooltip("최대 각속도 제한 (rad/s)")]
        [SerializeField] private float _maxAngularSpeed = 3f;

        [Header("디버그")]
        [SerializeField] private bool _showDebugInfo = true;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        /// <summary>AircraftState 참조 (RTOSRunner에서 주입)</summary>
        public Aircraft.AircraftState State { get; set; }

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private float _currentSpeed;

        public float CurrentSpeed => _currentSpeed;

        private void Start()
        {
            // Rigidbody 자동 참조
            if (_rigidbody == null)
            {
                _rigidbody = GetComponent<Rigidbody>();
            }

            if (_rigidbody != null)
            {
                // 초기 속도 설정 (비행 중 시작)
                _currentSpeed = _rigidbody.velocity.magnitude;

                // 물리 설정 강제 (안정성 확보)
                _rigidbody.mass = 1000f;
                _rigidbody.angularDrag = 0f; // 드래그를 다시 낮춤 (조작성 확보)
                _rigidbody.drag = 0f;
                _rigidbody.constraints = RigidbodyConstraints.None;

                // 관성 텐서 수동 설정 (매우 중요: 떨림 방지 및 회전축 안정화)
                // X(Pitch), Y(Yaw)는 크고 Z(Roll)는 작게 설정하여 전투기 특성 반영
                // 값이 너무 작으면 떨림(Jitter) 발생
                _rigidbody.inertiaTensor = new Vector3(6000f, 6000f, 3000f);
                _rigidbody.inertiaTensorRotation = Quaternion.identity;

                // Rigidbody 설정 확인
                ValidateRigidbodySettings();
            }
        }

        private void FixedUpdate()
        {
            // FixedUpdate에서 물리력 적용 (Unity 물리 엔진과 동기화)
            if (State == null || _rigidbody == null) return;

            ApplyPhysicsCommands();
            EnforceSpeedLimits();
        }

        // =====================================================================
        // 물리 적용 메서드
        // =====================================================================

        /// <summary>
        /// RTOS에서 계산된 명령을 Rigidbody에 적용
        /// HAL 레이어: Unity Physics API 호출만 담당
        /// </summary>
        private void ApplyPhysicsCommands()
        {
            // -----------------------------------------------------------------
            // 1. 추력 적용 (기체 전방 방향)
            // ThrustForceCommand: Vector3(0, 0, thrust) in local space
            // ThrustLimitScale: 과열로 인한 추력 제한 (0~1)
            // -----------------------------------------------------------------
            Vector3 thrustCommand = State.ThrustForceCommand * State.ThrustLimitScale;
            _rigidbody.AddRelativeForce(thrustCommand, ForceMode.Force);

            // -----------------------------------------------------------------
            // 2. 공기역학력 적용 (양력 + 항력)
            // AeroForceCommand: Vector3(0, lift, -drag) in local space
            // -----------------------------------------------------------------
            _rigidbody.AddRelativeForce(State.AeroForceCommand, ForceMode.Force);

            // -----------------------------------------------------------------
            // 3. 토크 적용 (피치, 롤, 요)
            // TorqueCommand: Vector3(pitch, yaw, roll) in local space
            // 임계값 없이 항상 적용!
            // -----------------------------------------------------------------
            _rigidbody.AddRelativeTorque(State.TorqueCommand, ForceMode.Force);
        }

        /// <summary>
        /// 속도 제한 적용 (안전 장치)
        /// </summary>
        private void EnforceSpeedLimits()
        {
            // 최대 속도 제한
            if (_rigidbody.velocity.magnitude > _maxSpeed)
            {
                _rigidbody.velocity = _rigidbody.velocity.normalized * _maxSpeed;
            }

            // 최대 각속도 제한
            if (_rigidbody.angularVelocity.magnitude > _maxAngularSpeed)
            {
                _rigidbody.angularVelocity = _rigidbody.angularVelocity.normalized * _maxAngularSpeed;
            }

            // 현재 속도 캐시
            _currentSpeed = _rigidbody.velocity.magnitude;

            // State에 현재 속도 업데이트 (백업용)
            if (State != null)
            {
                State.Velocity = _currentSpeed;
            }
        }

        /// <summary>
        /// Rigidbody 설정 유효성 검사 및 권장 설정 적용
        /// </summary>
        private void ValidateRigidbodySettings()
        {
            if (_rigidbody == null) return;

            // 권장 설정 확인 (경고 출력)
            if (_rigidbody.useGravity == false)
            {
                Debug.LogWarning("[FlightActuator] Rigidbody.useGravity가 false입니다. 중력 적용을 권장합니다.");
            }

            if (_rigidbody.isKinematic)
            {
                Debug.LogError("[FlightActuator] Rigidbody.isKinematic이 true입니다! 물리 시뮬레이션이 작동하지 않습니다.");
            }

            // Drag는 직접 계산하므로 0으로 설정 권장
            if (_rigidbody.drag > 0.01f || _rigidbody.angularDrag > 0.01f)
            {
                Debug.LogWarning("[FlightActuator] Rigidbody Drag가 0이 아닙니다. " +
                    "공기역학 계산과 중복될 수 있습니다. Drag=0, Angular Drag=0 권장.");
            }
        }

        // =====================================================================
        // 디버그 UI
        // =====================================================================

        private void OnGUI()
        {
            if (!_showDebugInfo || State == null) return;

            // 조종석 HUD 정보 표시
            int boxHeight = 220; // 높이 증가 (온도 추가)
            GUI.Box(new Rect(20, 20, 320, boxHeight), "RTOS FLIGHT SYSTEM v2.1");

            int y = 45;
            int lineHeight = 18;

            // 스로틀 및 속도 (과열 시 실효 스로틀 표시)
            int thrInput = (int)(State.ThrottleCommand * 100);
            int thrEffective = (int)(State.ThrottleCommand * State.ThrustLimitScale * 100);
            string thrStr = State.ThrustLimitScale < 1f
                ? $"THR: {thrInput}% → {thrEffective}%"
                : $"THR: {thrInput}%";
            GUI.Label(new Rect(30, y, 300, lineHeight),
                $"{thrStr}  |  SPD: {(int)_currentSpeed} m/s");
            y += lineHeight;

            // 고도 및 수직속도
            GUI.Label(new Rect(30, y, 300, lineHeight),
                $"ALT: {(int)State.Altitude} m  |  VS: {State.VerticalSpeed:F1} m/s");
            y += lineHeight;

            // 자세
            GUI.Label(new Rect(30, y, 300, lineHeight),
                $"Pitch: {State.Pitch:F1}°  |  Roll: {State.Roll:F1}°  |  Yaw: {State.Yaw:F0}°");
            y += lineHeight;

            // 입력 (디버그용)
            GUI.Label(new Rect(30, y, 300, lineHeight),
                $"INPUT - P:{State.PitchInput:F1} R:{State.RollInput:F1} Y:{State.YawInput:F1}");
            y += lineHeight;

            // 토크 (디버그용)
            Vector3 t = State.TorqueCommand;
            GUI.Label(new Rect(30, y, 300, lineHeight),
                $"TORQUE - X:{t.x / 1000:F0}k Y:{t.y / 1000:F0}k Z:{t.z / 1000:F0}k");
            y += lineHeight;

            // 공기역학 데이터
            GUI.Label(new Rect(30, y, 300, lineHeight),
                $"AoA: {State.AngleOfAttack:F1}°  |  G: {State.GForce:F1}");
            y += lineHeight;

            // 연료 상태
            GUI.Label(new Rect(30, y, 300, lineHeight),
                $"FUEL: {State.FuelRemainingLiters:F0} L ({State.FuelLevel:F0}%)  |  Burn: {State.FuelConsumptionRate:F1} L/s");
            y += lineHeight;

            // 힘 정보
            float thrust = State.ThrustForceCommand.z;
            GUI.Label(new Rect(30, y, 300, lineHeight),
                $"Thrust: {thrust / 1000f:F1} kN  |  Lift: {State.LiftForce / 1000f:F1} kN");
            y += lineHeight;

            // 엔진 온도 (과열 시 색상 변경)
            Color prevColor = GUI.color;
            if (State.OverheatCritical)
                GUI.color = Color.red;
            else if (State.OverheatWarning)
                GUI.color = Color.yellow;

            string tempStr = $"EGT: {State.EngineTemp:F0}°C";
            if (State.ThrustLimitScale < 1f)
                tempStr += $"  |  Limit: {State.ThrustLimitScale:P0}";
            GUI.Label(new Rect(30, y, 300, lineHeight), tempStr);

            GUI.color = prevColor;
        }
    }
}
