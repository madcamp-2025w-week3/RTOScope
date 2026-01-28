/*
 * ============================================================================
 * HUDTask.cs - HUD 처리 태스크
 * ============================================================================
 *
 * [모듈 역할]
 * HUD(Head-Up Display) 데이터 처리 및 갱신
 *
 * [아키텍처 위치]
 * RTOS Layer > Tasks > HUDTask
 * - 순수 C# 코드 (Unity API 사용 금지)
 * - AircraftState에서 데이터 읽어 HUD 표시 데이터 계산
 *
 * [우선순위]
 * Low - 시각적 표시이므로 비행 제어보다 낮은 우선순위
 * Soft Deadline - 몇 프레임 지연되어도 안전에 영향 없음
 *
 * [상태 머신 설계]
 * Step 0: 센서 데이터 수집
 * Step 1: 속도/고도 계산
 * Step 2: 자세 데이터 처리
 * Step 3: HUD 데이터 출력
 * ============================================================================
 */

using RTOScope.RTOS.Kernel;

namespace RTOScope.RTOS.Tasks
{
    /// <summary>
    /// HUD 표시용 데이터 구조체 (HUDRenderer가 읽음)
    /// </summary>
    public class HUDData
    {
        // 속도/고도
        public float Airspeed { get; set; }       // 대기속도 (knots)
        public float Altitude { get; set; }       // 고도 (feet)
        public float VerticalSpeed { get; set; } // 수직속도 (ft/min)

        // 자세
        public float Pitch { get; set; }          // 피치 (도)
        public float Roll { get; set; }           // 롤 (도)
        public float Heading { get; set; }        // 방위각 (0-360)

        // 엔진/시스템
        public float Throttle { get; set; }       // 스로틀 (0-100%)
        public float GForce { get; set; }         // G-Force
        public float FuelPercent { get; set; }    // 연료 (%)

        // 엔진 온도
        public float EngineTemp { get; set; }     // 온도 (°C)
        public bool OverheatWarning { get; set; } // 과열 경고
        public bool OverheatCritical { get; set; }// 과열 위험
        public float ThrustLimitScale { get; set; }// 추력 제한 (0~1)

        // 갱신 타임스탬프
        public float LastUpdateTime { get; set; }
    }

    /// <summary>
    /// HUD 처리 태스크
    /// 주기: 33ms (30Hz) - 화면 갱신에 충분한 주기
    /// </summary>
    public class HUDTask : IRTOSTask
    {
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_READ_SENSORS = 0;
        private const int STEP_CALC_SPEED_ALT = 1;
        private const int STEP_CALC_ATTITUDE = 2;
        private const int STEP_OUTPUT_DATA = 3;
        private const int TOTAL_STEPS = 4;

        // 각 Step의 WCET (초) - 스케줄러 비교용 증가
        private static readonly float[] _stepWCETs = {
            0.002f,   // Step 0: 센서 읽기 (2ms)
            0.003f,   // Step 1: 속도/고도 계산 (3ms)
            0.002f,   // Step 2: 자세 계산 (2ms)
            0.002f    // Step 3: 출력 (2ms)
        };            // 총 WCET: 9ms

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;
        private Runtime.Aircraft.AircraftState _state;
        private readonly HUDData _hudData;

        // 임시 계산용
        private float _rawSpeed;
        private float _rawAltitude;
        private float _rawPitch;
        private float _rawRoll;
        private float _rawYaw;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "HUD";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        /// <summary>HUDRenderer가 읽을 데이터</summary>
        public HUDData Data => _hudData;

        // =====================================================================
        // 생성자
        // =====================================================================

        public HUDTask()
        {
            _currentStep = 0;
            _hudData = new HUDData();
        }

        /// <summary>AircraftState 참조 설정</summary>
        public void SetState(Runtime.Aircraft.AircraftState state)
        {
            _state = state;
        }

        // =====================================================================
        // IRTOSTask 구현
        // =====================================================================

        public void Initialize()
        {
            _currentStep = 0;
        }

        public void ExecuteStep()
        {
            switch (_currentStep)
            {
                case STEP_READ_SENSORS:
                    ReadSensorData();
                    _currentStep++;
                    break;

                case STEP_CALC_SPEED_ALT:
                    CalculateSpeedAltitude();
                    _currentStep++;
                    break;

                case STEP_CALC_ATTITUDE:
                    CalculateAttitude();
                    _currentStep++;
                    break;

                case STEP_OUTPUT_DATA:
                    OutputHUDData();
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
            // 정리할 것 없음
        }

        public void OnDeadlineMiss()
        {
            // Soft Deadline - HUD 갱신 지연됨 (시각적 끊김만 발생)
        }

        // =====================================================================
        // 비공개 메서드
        // =====================================================================

        private void ReadSensorData()
        {
            if (_state == null) return;

            _rawSpeed = _state.Velocity;
            _rawAltitude = _state.Altitude;
            _rawPitch = _state.Pitch;
            _rawRoll = _state.Roll;
            _rawYaw = _state.Yaw;
        }

        private void CalculateSpeedAltitude()
        {
            // m/s → knots 변환 (1 m/s ≈ 1.944 knots)
            _hudData.Airspeed = _rawSpeed * 1.944f;

            // m → feet 변환 (1 m ≈ 3.281 feet)
            _hudData.Altitude = _rawAltitude * 3.281f;

            // 수직속도 (m/s → ft/min)
            if (_state != null)
            {
                _hudData.VerticalSpeed = _state.VerticalSpeed * 196.85f;
            }
        }

        private void CalculateAttitude()
        {
            _hudData.Pitch = _rawPitch;
            _hudData.Roll = _rawRoll;
            _hudData.Heading = _rawYaw;

            // 헤딩 정규화 (0-360)
            while (_hudData.Heading < 0) _hudData.Heading += 360f;
            while (_hudData.Heading >= 360) _hudData.Heading -= 360f;
        }

        private void OutputHUDData()
        {
            if (_state == null) return;

            _hudData.Throttle = _state.ThrottleCommand * 100f;
            _hudData.FuelPercent = _state.FuelLevel;
            _hudData.GForce = _state.GForce;

            // 엔진 온도 정보
            _hudData.EngineTemp = _state.EngineTemp;
            _hudData.OverheatWarning = _state.OverheatWarning;
            _hudData.OverheatCritical = _state.OverheatCritical;
            _hudData.ThrustLimitScale = _state.ThrustLimitScale;

            // 갱신 시간 기록 (커널에서 가상 시간을 전달받으면 더 정확)
            _hudData.LastUpdateTime = UnityEngine.Time.time;
        }
    }
}
