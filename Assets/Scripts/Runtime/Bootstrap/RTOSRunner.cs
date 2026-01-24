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
 */

using UnityEngine;
using RTOScope.RTOS.Kernel;
using RTOScope.RTOS.Tasks;
using RTOScope.Runtime.Aircraft;
using RTOScope.Runtime.Hardware;

namespace RTOScope.Runtime.Bootstrap
{
    /// <summary>
    /// RTOS 시스템 부트스트랩 및 실행기
    /// Unity MonoBehaviour로서 RTOS 커널과 Unity를 연결
    /// </summary>
    public class RTOSRunner : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _aircraftTransform;
        [SerializeField] private FlightActuator _actuator;
        [SerializeField] private SensorArray _sensor;
        [SerializeField] private AircraftView _view;

        [Header("RTOS Settings")]
        [SerializeField] private bool _autoStart = true;
        [SerializeField] private bool _useFixedUpdate = true;  // 정밀 타이밍용

        // RTOS 컴포넌트
        private RTOSKernel _kernel;
        private AircraftState _state;
        
        // 태스크 인스턴스
        private FlightControlTask _flightControlTask;
        private RadarTask _radarTask;
        private HealthMonitor _healthMonitor;

        // 상태
        public bool IsRunning => _kernel?.State == KernelState.Running;
        public RTOSKernel Kernel => _kernel;
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
            // 정밀 타이밍: FixedUpdate 사용 (기본 50Hz, 조절 가능)
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
            // 1. 공유 상태 생성
            _state = new AircraftState();

            // 2. 커널 생성
            _kernel = new RTOSKernel();

            // 3. 태스크 생성
            _flightControlTask = new FlightControlTask();
            _radarTask = new RadarTask();
            _healthMonitor = new HealthMonitor();

            // 4. 태스크 등록 (TCB 기반 - 우선순위, 주기, 데드라인 설정)
            _kernel.RegisterTask(
                task: _flightControlTask,
                priority: TaskPriority.Critical,
                period: 0.01f,          // 10ms (100Hz)
                deadline: 0.01f,
                deadlineType: DeadlineType.Hard
            );
            
            _kernel.RegisterTask(
                task: _radarTask,
                priority: TaskPriority.High,
                period: 0.05f,          // 50ms (20Hz)
                deadline: 0.05f,
                deadlineType: DeadlineType.Soft
            );
            
            _kernel.RegisterTask(
                task: _healthMonitor,
                priority: TaskPriority.Medium,
                period: 0.1f,           // 100ms (10Hz)
                deadline: 0.1f,
                deadlineType: DeadlineType.Soft
            );

            // 5. HAL 연결
            if (_actuator != null) _actuator.State = _state;
            if (_sensor != null) _sensor.State = _state;
            if (_view != null) _view.State = _state;

            Debug.Log("[RTOSRunner] RTOS 초기화 완료 - 3개 태스크 등록됨");
        }

        /// <summary>RTOS 시작</summary>
        public void StartRTOS()
        {
            if (_kernel == null) return;
            
            // 커널이 모든 태스크 초기화 수행
            _kernel.Start();
            Debug.Log("[RTOSRunner] RTOS 시작됨");
        }

        /// <summary>RTOS 정지</summary>
        public void StopRTOS()
        {
            _kernel?.Stop();
            Debug.Log("[RTOSRunner] RTOS 정지됨");
        }
    }
}
