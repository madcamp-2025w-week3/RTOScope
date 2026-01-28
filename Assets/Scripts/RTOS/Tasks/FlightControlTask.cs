/*
 * FlightControlTask.cs - 비행 제어 태스크 (공기역학 버전)
 *
 * [역할] 항공기 공기역학 계산 및 제어 - 최상위 우선순위
 * [위치] RTOS Layer > Tasks
 * [우선순위] Critical (Hard Deadline) - 위반 시 시스템 실패
 *
 * [데이터 흐름]
 * SensorArray → AircraftState(센서) → FlightControlTask → AircraftState(명령) → FlightActuator
 *
 * [상태 머신 설계 (Atomic State Machine)]
 * Step 0: 센서/입력 데이터 읽기
 * Step 1: 대기 데이터 계산 (밀도, 동압)
 * Step 2: 공기역학 계수 계산 (CL, CD)
 * Step 3: 양력/항력 계산
 * Step 4: 추력 계산
 * Step 5: 피치 토크 계산 (선형 공력)
 * Step 6: 롤 토크 계산 (선형 공력)
 * Step 7: 요 토크 계산 (선형 공력)
 * Step 8: 공기역학력 합성
 * Step 9: 명령 출력
 *
 * [v2.0 업데이트]
 * - Rigidbody 기반 공기역학 계산 구현
 * - 10단계 Atomic State Machine으로 확장
 * - 선형 공력 모델 기반 토크 계산
 * - 양력/항력/추력/토크 계산
 */

using RTOScope.RTOS.Kernel;
using RTOScope.Runtime.Aircraft;
using UnityEngine;

namespace RTOScope.RTOS.Tasks
{
    /// <summary>
    /// 비행 제어 태스크 (Flight Control System)
    /// 주기: 10ms (100Hz) - 고빈도 제어 루프
    /// Hard Deadline - 실시간으로 공기역학을 계산하고 기체를 제어
    /// </summary>
    public class FlightControlTask : IRTOSTask
    {
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_READ_INPUTS = 0;
        private const int STEP_COMPUTE_AIR_DATA = 1;
        private const int STEP_COMPUTE_AERO_COEFFS = 2;
        private const int STEP_COMPUTE_LIFT_DRAG = 3;
        private const int STEP_COMPUTE_THRUST = 4;
        private const int STEP_COMPUTE_PITCH_TORQUE = 5;
        private const int STEP_COMPUTE_ROLL_TORQUE = 6;
        private const int STEP_COMPUTE_YAW_TORQUE = 7;
        private const int STEP_COMBINE_FORCES = 8;
        private const int STEP_OUTPUT_COMMANDS = 9;
        private const int TOTAL_STEPS = 10;

        // 각 Step의 WCET (초 단위)
        private static readonly float[] _stepWCETs = {
            0.0003f,  // Step 0: 입력 읽기 (0.3ms)
            0.0004f,  // Step 1: 대기 데이터 (0.4ms)
            0.0005f,  // Step 2: 공기역학 계수 (0.5ms)
            0.0005f,  // Step 3: 양력/항력 (0.5ms)
            0.0002f,  // Step 4: 추력 (0.2ms)
            0.0006f,  // Step 5: 피치 토크 (0.6ms)
            0.0006f,  // Step 6: 롤 토크 (0.6ms)
            0.0005f,  // Step 7: 요 토크 (0.5ms)
            0.0004f,  // Step 8: 힘 합성 (0.4ms)
            0.0002f   // Step 9: 출력 (0.2ms)
        };            // 총 WCET: 4.2ms (10ms 예산의 42%)

        // =====================================================================
        // 항공기 물리 상수 (F-16 기준, Inspector에서 조절 가능하도록 추후 설정 클래스 분리 권장)
        // =====================================================================

        // 기하학적 상수
        private const float WING_AREA = 27.87f;           // 날개 면적 (m²)
        private const float WING_SPAN = 9.96f;            // 날개 폭 (m)
        private const float MEAN_CHORD = 3.45f;           // 평균 시위 (m)
        private const float ASPECT_RATIO = 3.0f;          // 가로세로비
        private const float OSWALD_EFFICIENCY = 0.85f;    // 오스왈드 효율 계수

        // 공기역학 상수
        private const float CL_SLOPE = 6.28f;             // 양력 기울기 (rad⁻¹)
        private const float CL_MAX = 1.6f;                // 최대 양력 계수
        private const float CL_MIN = -1.0f;               // 최소 양력 계수
        private const float CD_0 = 0.04f;                 // 기본 항력 계수 (Parasitic Drag) - 현실적 감속을 위해 상향
        private const float STALL_ANGLE = 15f;            // 실속각 (도)

        // 엔진 상수
        private const float MAX_THRUST = 129000f;         // 최대 추력 (N) - F-16 애프터버너
        private const float IDLE_THRUST = 0f;             // 공회전 추력 (N) - 0으로 설정하여 스로틀 반영 명확화

        // 조종/안정성 상수 (선형 공력 모델)
        private const float MIN_AIRSPEED = 15f; // m/s
        private const float MIN_DYNAMIC_PRESSURE = 20f; // Pa, 수치 안정용
        private const float INPUT_RESPONSE = 6f; // 1/s, 입력 완만화 (Roll/Yaw)
        private const float PITCH_INPUT_RESPONSE = 3f; // 1/s, 피치 입력 완만화
        private const float CONTROL_Q_REF = 6000f; // Pa, 고속 조종 민감도 완화 기준

        private const float MAX_ELEVATOR_DEFLECTION = 20f; // deg
        private const float MAX_AILERON_DEFLECTION = 21f;  // deg
        private const float MAX_RUDDER_DEFLECTION = 30f;   // deg

        // 안정성/조종성 미분 계수 (선형 근사)
        private const float CM_ALPHA = -0.8f;
        private const float CM_Q = -16f;
        private const float CM_DE = 0.9f;

        private const float CL_BETA = -0.12f;
        private const float CL_P = -0.5f;
        private const float CL_R = 0.14f;
        private const float CL_DA = 0.08f;
        private const float CL_DR = 0.02f;

        private const float CN_BETA = 0.25f;
        private const float CN_R = -0.35f;
        private const float CN_P = -0.05f;
        private const float CN_DA = 0.02f;
        private const float CN_DR = 0.1f;

        private const float MAX_TORQUE = 300000f; // 토크 상한

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;

        // AircraftState 참조 (외부에서 주입)
        private AircraftState _state;

        // 읽어온 입력 값 (캐시)
        private float _pitchInput;
        private float _rollInput;
        private float _yawInput;
        private float _throttleInput;
        private float _pitchInputSmooth;
        private float _rollInputSmooth;
        private float _yawInputSmooth;

        // 센서 데이터 (캐시)
        private float _velocity;
        private float _dynamicPressure;
        private float _angleOfAttack;
        private float _sideslipAngle;
        private float _airDensity;
        private Vector3 _localVelocity;
        private Vector3 _angularVelocity;

        // 중간 계산값
        private float _liftCoefficient;
        private float _dragCoefficient;
        private float _liftForce;
        private float _dragForce;
        private float _thrustForce;
        private float _pitchTorque;
        private float _rollTorque;
        private float _yawTorque;

        // 최종 명령
        private Vector3 _thrustCommand;
        private Vector3 _aeroForceCommand;
        private Vector3 _torqueCommand;

        // 델타 타임 (RTOS 주기)
        private const float DELTA_TIME = 0.01f; // 10ms (100Hz)

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "FlightControl";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        // =====================================================================
        // 생성자
        // =====================================================================

        public FlightControlTask()
        {
            _currentStep = 0;
        }

        /// <summary>
        /// AircraftState 참조 설정 (RTOSRunner에서 호출)
        /// </summary>
        public void SetState(AircraftState state)
        {
            _state = state;
        }

        // =====================================================================
        // IRTOSTask 구현
        // =====================================================================

        public void Initialize()
        {
            _currentStep = 0;

            // 중간값 초기화
            _liftCoefficient = 0f;
            _dragCoefficient = CD_0;
            _liftForce = 0f;
            _dragForce = 0f;
            _thrustForce = 0f;
            _pitchTorque = 0f;
            _rollTorque = 0f;
            _yawTorque = 0f;

            _thrustCommand = Vector3.zero;
            _aeroForceCommand = Vector3.zero;
            _torqueCommand = Vector3.zero;

            _pitchInputSmooth = 0f;
            _rollInputSmooth = 0f;
            _yawInputSmooth = 0f;
        }

        public void ExecuteStep()
        {
            switch (_currentStep)
            {
                case STEP_READ_INPUTS:
                    ReadInputs();
                    _currentStep++;
                    break;

                case STEP_COMPUTE_AIR_DATA:
                    ComputeAirData();
                    _currentStep++;
                    break;

                case STEP_COMPUTE_AERO_COEFFS:
                    ComputeAeroCoefficients();
                    _currentStep++;
                    break;

                case STEP_COMPUTE_LIFT_DRAG:
                    ComputeLiftAndDrag();
                    _currentStep++;
                    break;

                case STEP_COMPUTE_THRUST:
                    ComputeThrust();
                    _currentStep++;
                    break;

                case STEP_COMPUTE_PITCH_TORQUE:
                    ComputePitchTorque();
                    _currentStep++;
                    break;

                case STEP_COMPUTE_ROLL_TORQUE:
                    ComputeRollTorque();
                    _currentStep++;
                    break;

                case STEP_COMPUTE_YAW_TORQUE:
                    ComputeYawTorque();
                    _currentStep++;
                    break;

                case STEP_COMBINE_FORCES:
                    CombineForces();
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
            if (_state != null)
            {
                _state.ThrottleCommand = 0f;
                _state.PitchCommand = 0f;
                _state.RollCommand = 0f;
                _state.YawCommand = 0f;
                _state.ThrustForceCommand = Vector3.zero;
                _state.AeroForceCommand = Vector3.zero;
                _state.TorqueCommand = Vector3.zero;
            }
        }

        public void OnDeadlineMiss()
        {
            // Hard Deadline 미스 시 비상 처리
            // 현재 명령 유지 (안전 조치)
            // TODO: 비상 모드 전환 로직 추가 가능
        }

        // =====================================================================
        // Step 구현: 각 단계의 원자적 계산 로직
        // =====================================================================

        /// <summary>
        /// Step 0: 센서 및 입력 데이터 읽기
        /// </summary>
        private void ReadInputs()
        {
            if (_state == null) return;

            // 사용자 입력 읽기
            _pitchInput = _state.PitchInput;
            _rollInput = _state.RollInput;
            _yawInput = _state.YawInput;
            _throttleInput = _state.ThrottleInput;
            float throttleLimit = _state.ThrottleLimit > 0f ? _state.ThrottleLimit : 1f;
            _throttleInput = Mathf.Min(_throttleInput, Mathf.Clamp01(throttleLimit));

            float smoothPitch = 1f - Mathf.Exp(-PITCH_INPUT_RESPONSE * DELTA_TIME);
            float smooth = 1f - Mathf.Exp(-INPUT_RESPONSE * DELTA_TIME);
            _pitchInputSmooth = Mathf.Lerp(_pitchInputSmooth, _pitchInput, smoothPitch);
            _rollInputSmooth = Mathf.Lerp(_rollInputSmooth, _rollInput, smooth);
            _yawInputSmooth = Mathf.Lerp(_yawInputSmooth, _yawInput, smooth);

            // 센서 데이터 읽기
            _velocity = _state.Velocity;
            _dynamicPressure = _state.DynamicPressure;
            _angleOfAttack = _state.AngleOfAttack;
            _sideslipAngle = _state.SideslipAngle;
            _airDensity = _state.AirDensity;
            _localVelocity = _state.LocalVelocity;
            _angularVelocity = _state.AngularVelocity;
        }

        /// <summary>
        /// Step 1: 대기 데이터 계산 (이미 SensorArray에서 계산됨, 검증용)
        /// </summary>
        private void ComputeAirData()
        {
            // SensorArray에서 이미 계산됨
            // 여기서는 유효성 검사 및 보정만 수행

            // 최소 동압 보정 (수치 안정용)
            if (_dynamicPressure < MIN_DYNAMIC_PRESSURE)
            {
                _dynamicPressure = MIN_DYNAMIC_PRESSURE;
            }
        }

        /// <summary>
        /// Step 2: 공기역학 계수 계산 (CL, CD)
        /// </summary>
        private void ComputeAeroCoefficients()
        {
            // -----------------------------------------------------------------
            // 양력 계수 (CL) 계산
            // 선형 영역: CL = CL_slope * α
            // 실속 영역: CL 제한
            // -----------------------------------------------------------------
            float aoaRad = _angleOfAttack * Mathf.Deg2Rad;

            if (Mathf.Abs(_angleOfAttack) < STALL_ANGLE)
            {
                // 선형 영역
                _liftCoefficient = CL_SLOPE * aoaRad;
            }
            else
            {
                // 실속 영역 - 양력 감소 모델 (단순화)
                float stallFactor = 1f - (Mathf.Abs(_angleOfAttack) - STALL_ANGLE) / 15f;
                stallFactor = Mathf.Clamp01(stallFactor);
                _liftCoefficient = CL_SLOPE * Mathf.Sign(aoaRad) * STALL_ANGLE * Mathf.Deg2Rad * stallFactor;
            }

            // 양력 계수 제한
            _liftCoefficient = Mathf.Clamp(_liftCoefficient, CL_MIN, CL_MAX);

            // -----------------------------------------------------------------
            // 항력 계수 (CD) 계산
            // CD = CD_0 + CL² / (π * AR * e) (유도항력 포함)
            // -----------------------------------------------------------------
            float inducedDrag = (_liftCoefficient * _liftCoefficient) /
                                (Mathf.PI * ASPECT_RATIO * OSWALD_EFFICIENCY);
            _dragCoefficient = CD_0 + inducedDrag;

            // AircraftState에 기록 (디버그용)
            if (_state != null)
            {
                _state.LiftCoefficient = _liftCoefficient;
                _state.DragCoefficient = _dragCoefficient;
            }
        }

        /// <summary>
        /// Step 3: 양력 및 항력 계산
        /// L = 0.5 * ρ * V² * S * CL
        /// D = 0.5 * ρ * V² * S * CD
        /// </summary>
        private void ComputeLiftAndDrag()
        {
            // 양력: 위쪽 방향 (로컬 Y+)
            _liftForce = _dynamicPressure * WING_AREA * _liftCoefficient;

            // 항력: 속도 반대 방향 (로컬 Z-)
            _dragForce = _dynamicPressure * WING_AREA * _dragCoefficient;

            // 속도 방향 기준으로 힘 벡터 구성 (Airplane_Tutorial 방식에 맞춤)
            if (_localVelocity.sqrMagnitude > 0.01f)
            {
                Vector3 dragDir = -_localVelocity.normalized;
                Vector3 liftDir = Vector3.Cross(Vector3.right, dragDir);
                if (liftDir.sqrMagnitude < 1e-4f)
                {
                    liftDir = Vector3.up;
                }
                liftDir.Normalize();
                _aeroForceCommand = liftDir * _liftForce + dragDir * _dragForce;
            }
            else
            {
                _aeroForceCommand = new Vector3(0f, _liftForce, -_dragForce);
            }

            // AircraftState에 기록 (디버그용)
            if (_state != null)
            {
                _state.LiftForce = _liftForce;
                _state.DragForce = _dragForce;
            }
        }

        /// <summary>
        /// Step 4: 추력 계산
        /// 현실적인 스로틀-추력 관계:
        /// - 0~40%: 강한 감속 영역
        /// - 40~60%: 약한 감속~순항 영역
        /// - 60~100%: 가속 영역
        /// </summary>
        private void ComputeThrust()
        {
            // 현실적인 추력 곡선:
            // 저스로틀(0~40%)에서는 추력이 거의 없어 감속
            // 중스로틀(40~60%)에서 순항 유지
            // 고스로틀(60~100%)에서 가속
            
            const float LOW_THROTTLE = 0.40f;   // 감속 영역 상한
            const float CRUISE_THROTTLE = 0.55f; // 순항 스로틀
            
            float effectiveThrottle;
            
            if (_throttleInput < LOW_THROTTLE)
            {
                // 0~40% → 0~20% 추력 (강한 감속)
                effectiveThrottle = (_throttleInput / LOW_THROTTLE) * 0.20f;
            }
            else if (_throttleInput < CRUISE_THROTTLE)
            {
                // 40~55% → 20~40% 추력 (약한 감속~순항)
                float t = (_throttleInput - LOW_THROTTLE) / (CRUISE_THROTTLE - LOW_THROTTLE);
                effectiveThrottle = 0.20f + t * 0.20f;
            }
            else
            {
                // 55~100% → 40~100% 추력 (순항~가속)
                float t = (_throttleInput - CRUISE_THROTTLE) / (1f - CRUISE_THROTTLE);
                effectiveThrottle = 0.40f + t * 0.60f;
            }
            
            _thrustForce = Mathf.Lerp(IDLE_THRUST, MAX_THRUST, effectiveThrottle);
        }

        /// <summary>
        /// Step 5: 피치 토크 계산 (선형 공력 모델)
        /// </summary>
        private void ComputePitchTorque()
        {
            float v = Mathf.Max(_velocity, MIN_AIRSPEED);
            float qBar = _dynamicPressure;

            float alpha = _angleOfAttack * Mathf.Deg2Rad;
            float qRate = _angularVelocity.x; // pitch rate
            float qHat = qRate * MEAN_CHORD / (2f * v);

            float deflectionScale = GetDeflectionScale(qBar);
            float deltaE = _pitchInputSmooth * MAX_ELEVATOR_DEFLECTION * Mathf.Deg2Rad * deflectionScale;

            float cm = CM_ALPHA * alpha + CM_Q * qHat + CM_DE * deltaE;
            _pitchTorque = Mathf.Clamp(qBar * WING_AREA * MEAN_CHORD * cm, -MAX_TORQUE, MAX_TORQUE);
        }

        /// <summary>
        /// Step 6: 롤 토크 계산 (선형 공력 모델)
        /// </summary>
        private void ComputeRollTorque()
        {
            float v = Mathf.Max(_velocity, MIN_AIRSPEED);
            float qBar = _dynamicPressure;

            float beta = _sideslipAngle * Mathf.Deg2Rad;
            float pRate = _angularVelocity.z; // roll rate
            float rRate = _angularVelocity.y; // yaw rate
            float pHat = pRate * WING_SPAN / (2f * v);
            float rHat = rRate * WING_SPAN / (2f * v);

            float deflectionScale = GetDeflectionScale(qBar);
            float deltaA = -_rollInputSmooth * MAX_AILERON_DEFLECTION * Mathf.Deg2Rad * deflectionScale;
            float deltaR = _yawInputSmooth * MAX_RUDDER_DEFLECTION * Mathf.Deg2Rad * deflectionScale;

            float cl = CL_BETA * beta + CL_P * pHat + CL_R * rHat + CL_DA * deltaA + CL_DR * deltaR;
            _rollTorque = Mathf.Clamp(qBar * WING_AREA * WING_SPAN * cl, -MAX_TORQUE, MAX_TORQUE);
        }

        /// <summary>
        /// Step 7: 요 토크 계산 (선형 공력 모델)
        /// </summary>
        private void ComputeYawTorque()
        {
            float v = Mathf.Max(_velocity, MIN_AIRSPEED);
            float qBar = _dynamicPressure;

            float beta = _sideslipAngle * Mathf.Deg2Rad;
            float pRate = _angularVelocity.z; // roll rate
            float rRate = _angularVelocity.y; // yaw rate
            float pHat = pRate * WING_SPAN / (2f * v);
            float rHat = rRate * WING_SPAN / (2f * v);

            float deflectionScale = GetDeflectionScale(qBar);
            float deltaA = -_rollInputSmooth * MAX_AILERON_DEFLECTION * Mathf.Deg2Rad * deflectionScale;
            float deltaR = _yawInputSmooth * MAX_RUDDER_DEFLECTION * Mathf.Deg2Rad * deflectionScale;

            float cn = CN_BETA * beta + CN_R * rHat + CN_P * pHat + CN_DA * deltaA + CN_DR * deltaR;
            _yawTorque = Mathf.Clamp(qBar * WING_AREA * WING_SPAN * cn, -MAX_TORQUE, MAX_TORQUE);
        }

        private float GetDeflectionScale(float dynamicPressure)
        {
            float qScale = dynamicPressure / (dynamicPressure + CONTROL_Q_REF);
            return Mathf.Lerp(1.1f, 0.5f, qScale);
        }

        /// <summary>
        /// Step 8: 모든 힘과 토크를 벡터로 합성
        /// </summary>
        private void CombineForces()
        {
            // -----------------------------------------------------------------
            // 추력 벡터 (기체 전방, 로컬 Z+)
            // -----------------------------------------------------------------
            _thrustCommand = new Vector3(0f, 0f, _thrustForce);

            // -----------------------------------------------------------------
            // 공기역학력 벡터 (로컬 좌표계)
            // Step 3에서 속도 방향 기준으로 계산됨
            // -----------------------------------------------------------------
            // _aeroForceCommand 유지

            // -----------------------------------------------------------------
            // 토크 벡터 (로컬 좌표계)
            // X: 피치 (Pitch)
            // Y: 요 (Yaw)
            // Z: 롤 (Roll)
            // -----------------------------------------------------------------
            _torqueCommand = new Vector3(_pitchTorque, _yawTorque, _rollTorque);
        }

        /// <summary>
        /// Step 9: 최종 명령을 AircraftState에 출력
        /// </summary>
        private void OutputCommands()
        {
            if (_state == null) return;

            // 벡터 명령 출력
            _state.ThrustForceCommand = _thrustCommand;
            _state.AeroForceCommand = _aeroForceCommand;
            _state.TorqueCommand = _torqueCommand;

            // 레거시 명령 유지 (하위 호환성)
            _state.PitchCommand = _pitchInput;
            _state.RollCommand = _rollInput;
            _state.YawCommand = _yawInput;
            _state.ThrottleCommand = _throttleInput;
        }
    }
}
