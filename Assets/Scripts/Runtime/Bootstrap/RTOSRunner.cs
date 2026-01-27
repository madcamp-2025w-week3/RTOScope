/*
 * RTOSRunner.cs - RTOS 부트스트랩 및 실행기 (하트비트)
 * 
 * [역할] Unity와 RTOS 커널 사이의 브릿지
 * [위치] Runtime Layer > Bootstrap (Unity MonoBehaviour)
 * 
 * [설계 의도]
 * - FixedUpdate를 RTOS 커널 틱으로 연결 (정밀 타이밍)
 * - RTOS 커널 및 태스크 초기화
 * - 시스템 컴포넌트 연결 (State, Actuator, Sensor)
 * - 이것이 RTOS 시스템의 진입점
 * 
 * [v2.0 업데이트]
 * - Rigidbody 기반 물리 시스템 지원
 * - SensorArray와 FlightActuator에 Rigidbody 연결
 */

using RTOScope.RTOS.Kernel;
using RTOScope.RTOS.Tasks;
using RTOScope.Runtime.Aircraft;
using RTOScope.Runtime.Hardware;
using UnityEngine;

namespace RTOScope.Runtime.Bootstrap
{
    /// <summary>
    /// RTOS 시스템 부트스트랩 및 실행기
    /// Unity MonoBehaviour로서 RTOS 커널과 Unity를 연결
    /// </summary>
    public class RTOSRunner : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("항공기 참조")]
        [Tooltip("항공기 Transform (센서 데이터 읽기용)")]
        [SerializeField] private Transform _aircraftTransform;

        [Tooltip("항공기 Rigidbody (물리 시뮬레이션용)")]
        [SerializeField] private Rigidbody _aircraftRigidbody;

        [Header("HAL 컴포넌트")]
        [Tooltip("비행 액추에이터 (물리력 적용)")]
        [SerializeField] private FlightActuator _actuator;

        [Tooltip("센서 어레이 (물리 상태 읽기)")]
        [SerializeField] private SensorArray _sensor;

        [Tooltip("항공기 뷰 (시각화)")]
        [SerializeField] private AircraftView _view;
        [SerializeField] private WeaponActuator _weaponActuator;
        [SerializeField] private TargetingSensor _targetingSensor;

        [Header("RTOS 설정")]
        [Tooltip("자동 시작 여부")]
        [SerializeField] private bool _autoStart = true;

        [Tooltip("FixedUpdate 사용 (물리 동기화 권장)")]
        [SerializeField] private bool _useFixedUpdate = true;

        [Header("디버그")]
        [SerializeField] private bool _logInitialization = true;

        // =====================================================================
        // RTOS 컴포넌트
        // =====================================================================

        private RTOSKernel _kernel;
        private AircraftState _state;

        // 태스크 인스턴스
        private FlightControlTask _flightControlTask;
        private WeaponControlTask _weaponControlTask;
        private RadarTask _radarTask;
        private HealthMonitor _healthMonitor;
        private CollisionAvoidanceTask _collisionAvoidanceTask;
        private FuelManagementTask _fuelManagementTask;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        /// <summary>RTOS 실행 상태</summary>
        public bool IsRunning => _kernel?.State == KernelState.Running;

        /// <summary>RTOS 커널 참조</summary>
        public RTOSKernel Kernel => _kernel;

        /// <summary>공유 상태 참조</summary>
        public AircraftState State => _state;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            InitializeRTOS();
        }

        private void Start()
        {
            if (_autoStart)
                StartRTOS();
        }

        private void Update()
        {
            // FixedUpdate 사용 안할 때만 여기서 틱
            if (!_useFixedUpdate && _kernel != null && _kernel.State == KernelState.Running)
            {
                _kernel.Tick(Time.deltaTime);
            }
        }

        private void FixedUpdate()
        {
            // 정밀 타이밍: FixedUpdate 사용 (기본 50Hz, Project Settings에서 조절)
            // 물리 시뮬레이션과 동기화되어 안정적
            if (_useFixedUpdate && _kernel != null && _kernel.State == KernelState.Running)
            {
                _kernel.Tick(Time.fixedDeltaTime);
            }
        }

        private void OnDestroy()
        {
            StopRTOS();
        }

        // =====================================================================
        // RTOS 초기화/제어
        // =====================================================================

        /// <summary>RTOS 시스템 초기화</summary>
        private void InitializeRTOS()
        {
            // -----------------------------------------------------------------
            // 1. 공유 상태 생성
            // -----------------------------------------------------------------
            _state = new AircraftState();

            // -----------------------------------------------------------------
            // 2. 커널 생성
            // -----------------------------------------------------------------
            _kernel = new RTOSKernel();

            // -----------------------------------------------------------------
            // 3. 태스크 생성
            // -----------------------------------------------------------------
            _flightControlTask = new FlightControlTask();
            _weaponControlTask = new WeaponControlTask();
            _radarTask = new RadarTask();
            _healthMonitor = new HealthMonitor();
            _collisionAvoidanceTask = new CollisionAvoidanceTask();
            _fuelManagementTask = new FuelManagementTask(0.25f);

            // -----------------------------------------------------------------
            // 4. 태스크 등록 (TCB 기반 - 우선순위, 주기, 데드라인 설정)
            // -----------------------------------------------------------------

            // FlightControlTask: 최고 우선순위, 100Hz, Hard Deadline
            _kernel.RegisterTask(
                task: _flightControlTask,
                priority: TaskPriority.Critical,
                period: 0.01f,          // 10ms (100Hz)
                deadline: 0.01f,
                deadlineType: DeadlineType.Hard
            );

            // WeaponControlTask: 높은 우선순위, 50Hz, Soft Deadline
            _kernel.RegisterTask(
                task: _weaponControlTask,
                priority: TaskPriority.High,
                period: 0.02f,          // 20ms (50Hz)
                deadline: 0.02f,
                deadlineType: DeadlineType.Soft
            );

            // RadarTask: 높은 우선순위, 20Hz, Soft Deadline
            _kernel.RegisterTask(
                task: _radarTask,
                priority: TaskPriority.High,
                period: 0.05f,          // 50ms (20Hz)
                deadline: 0.05f,
                deadlineType: DeadlineType.Soft
            );

            // HealthMonitor: 중간 우선순위, 10Hz, Soft Deadline
            _kernel.RegisterTask(
                task: _healthMonitor,
                priority: TaskPriority.Medium,
                period: 0.1f,           // 100ms (10Hz)
                deadline: 0.1f,
                deadlineType: DeadlineType.Soft
            );

            // CollisionAvoidanceTask: 높은 우선순위, 33Hz, Soft Deadline
            _kernel.RegisterTask(
                task: _collisionAvoidanceTask,
                priority: TaskPriority.High,
                period: 0.03f,          // 30ms (~33Hz)
                deadline: 0.03f,
                deadlineType: DeadlineType.Soft
            );

            // FuelManagementTask: 중간 우선순위, 4Hz, Soft Deadline
            _kernel.RegisterTask(
                task: _fuelManagementTask,
                priority: TaskPriority.Medium,
                period: 0.25f,          // 250ms (4Hz)
                deadline: 0.25f,
                deadlineType: DeadlineType.Soft
            );

            // -----------------------------------------------------------------
            // 5. HAL 연결 (AircraftState 주입)
            // -----------------------------------------------------------------
            if (_actuator != null)
            {
                _actuator.State = _state;
            }
            else
            {
                Debug.LogWarning("[RTOSRunner] FlightActuator가 할당되지 않았습니다!");
            }

            if (_sensor != null)
            {
                _sensor.State = _state;
            }
            else
            {
                Debug.LogWarning("[RTOSRunner] SensorArray가 할당되지 않았습니다!");
            }

            if (_view != null)
            {
                _view.State = _state;
            }

            if (_weaponActuator != null)
            {
                _weaponActuator.State = _state;
            }
            else
            {
                Debug.LogWarning("[RTOSRunner] WeaponActuator가 할당되지 않았습니다.");
            }

            if (_targetingSensor != null)
            {
                _targetingSensor.State = _state;
            }
            else
            {
                Debug.LogWarning("[RTOSRunner] TargetingSensor가 할당되지 않았습니다.");
            }

            // -----------------------------------------------------------------
            // 6. RTOS 태스크에 AircraftState 연결
            // -----------------------------------------------------------------
            _flightControlTask.SetState(_state);
            _weaponControlTask.SetState(_state);
            _collisionAvoidanceTask.SetState(_state);
            _fuelManagementTask.SetState(_state);

            // -----------------------------------------------------------------
            // 7. 초기 비행 상태 설정 (비행 중 시작)
            // -----------------------------------------------------------------
            if (_aircraftRigidbody != null)
            {
                // 초기 속도 설정 (전방으로 100 m/s)
                Vector3 initialVelocity = _aircraftTransform.forward * 100f;
                _aircraftRigidbody.velocity = initialVelocity;

                if (_logInitialization)
                {
                    Debug.Log($"[RTOSRunner] 초기 속도 설정: {initialVelocity.magnitude:F1} m/s");
                }
            }

            if (_logInitialization)
            {
                Debug.Log("[RTOSRunner] RTOS 초기화 완료 - 5개 태스크 등록됨 (+ IdleTask)");
                Debug.Log($"[RTOSRunner] FixedUpdate 사용: {_useFixedUpdate}, Fixed Timestep: {Time.fixedDeltaTime * 1000f:F1}ms");
            }
        }

        /// <summary>RTOS 시작</summary>
        public void StartRTOS()
        {
            if (_kernel == null) return;

            _kernel.Start();

            if (_logInitialization)
            {
                Debug.Log("[RTOSRunner] RTOS 시작");
            }
        }

        /// <summary>RTOS 정지</summary>
        public void StopRTOS()
        {
            _kernel?.Stop();

            if (_logInitialization)
            {
                Debug.Log("[RTOSRunner] RTOS 정지");
            }
        }

        // =====================================================================
        // 에디터 유틸리티
        // =====================================================================

#if UNITY_EDITOR
        private void OnValidate()
        {
            // Rigidbody 자동 탐색 (Transform이 설정된 경우)
            if (_aircraftTransform != null && _aircraftRigidbody == null)
            {
                _aircraftRigidbody = _aircraftTransform.GetComponent<Rigidbody>();
            }

            // HAL 컴포넌트 자동 탐색
            if (_aircraftTransform != null)
            {
                if (_actuator == null)
                    _actuator = _aircraftTransform.GetComponent<FlightActuator>();
                if (_sensor == null)
                    _sensor = _aircraftTransform.GetComponent<SensorArray>();
                if (_view == null)
                    _view = _aircraftTransform.GetComponent<AircraftView>();
                if (_weaponActuator == null)
                    _weaponActuator = _aircraftTransform.GetComponent<WeaponActuator>();
                if (_targetingSensor == null)
                    _targetingSensor = _aircraftTransform.GetComponent<TargetingSensor>();
            }
        }
#endif
    }
}
