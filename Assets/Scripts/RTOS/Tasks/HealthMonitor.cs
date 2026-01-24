/*
 * HealthMonitor.cs - 시스템 상태 모니터링 태스크
 *
 * [역할] 워치독(Watchdog) - 시스템 결함 감시 및 비상 안전 모드 전환
 * [위치] RTOS Layer > Tasks (Unity API 사용 금지)
 * [우선순위] Medium (Soft Deadline)
 *
 * [상태 머신 설계]
 * Step 0: CPU 사용률 수집
 * Step 1: 메모리 사용률 수집
 * Step 2: 데드라인 미스 체크
 * Step 3: 상태 판정 및 경고
 */

using RTOScope.RTOS.Kernel;

namespace RTOScope.RTOS.Tasks
{
    public enum HealthStatus { Normal, Warning, Critical, Emergency }

    /// <summary>
    /// 시스템 상태 모니터링 태스크 (Watchdog)
    /// 주기: 100ms (10Hz)
    /// 시스템 결함을 감시하고 비상시 안전 모드로 전환
    /// </summary>
    public class HealthMonitor : IRTOSTask
    {
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_CHECK_CPU = 0;
        private const int STEP_CHECK_MEMORY = 1;
        private const int STEP_CHECK_DEADLINES = 2;
        private const int STEP_EVALUATE_STATUS = 3;
        private const int TOTAL_STEPS = 4;

        // 각 Step의 WCET (초 단위)
        private static readonly float[] _stepWCETs = {
            0.001f,   // Step 0: CPU 체크 (1ms)
            0.001f,   // Step 1: 메모리 체크 (1ms)
            0.002f,   // Step 2: 데드라인 체크 (2ms)
            0.001f    // Step 3: 상태 판정 (1ms)
        };                // 총 WCET: 5ms

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;
        private HealthStatus _systemStatus;
        private float _cpuUsage;
        private float _memoryUsage;
        private int _deadlineMissCount;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "HealthMonitor";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        public HealthStatus SystemStatus => _systemStatus;
        public float CpuUsage => _cpuUsage;
        public float MemoryUsage => _memoryUsage;

        // =====================================================================
        // 생성자
        // =====================================================================

        public HealthMonitor()
        {
            _currentStep = 0;
            _systemStatus = HealthStatus.Normal;
            _cpuUsage = 0f;
            _memoryUsage = 0f;
            _deadlineMissCount = 0;
        }

        // =====================================================================
        // IRTOSTask 구현
        // =====================================================================

        public void Initialize()
        {
            _currentStep = 0;
            _systemStatus = HealthStatus.Normal;
            _cpuUsage = 0f;
            _memoryUsage = 0f;
            _deadlineMissCount = 0;
        }

        public void ExecuteStep()
        {
            switch (_currentStep)
            {
                case STEP_CHECK_CPU:
                    CheckCpuUsage();
                    _currentStep++;
                    break;

                case STEP_CHECK_MEMORY:
                    CheckMemoryUsage();
                    _currentStep++;
                    break;

                case STEP_CHECK_DEADLINES:
                    CheckDeadlineMisses();
                    _currentStep++;
                    break;

                case STEP_EVALUATE_STATUS:
                    EvaluateSystemStatus();
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
            // 정리
        }

        public void OnDeadlineMiss()
        {
            // 워치독 자체의 데드라인 미스는 시스템 과부하 징후
            _systemStatus = HealthStatus.Warning;
        }

        // =====================================================================
        // 비공개 메서드
        // =====================================================================

        private void CheckCpuUsage()
        {
            // TODO: TaskStatistics에서 CPU 사용률 조회
            // 현재는 더미 값
            _cpuUsage = 50f;
        }

        private void CheckMemoryUsage()
        {
            // TODO: MemoryManager에서 메모리 사용률 조회
            _memoryUsage = 30f;
        }

        private void CheckDeadlineMisses()
        {
            // TODO: DeadlineManager에서 미스 카운트 조회
            _deadlineMissCount = 0;
        }

        private void EvaluateSystemStatus()
        {
            // 임계값 기반 상태 판단
            if (_cpuUsage > 95f || _memoryUsage > 95f || _deadlineMissCount > 5)
                _systemStatus = HealthStatus.Emergency;
            else if (_cpuUsage > 90f || _memoryUsage > 90f)
                _systemStatus = HealthStatus.Critical;
            else if (_cpuUsage > 70f || _memoryUsage > 70f)
                _systemStatus = HealthStatus.Warning;
            else
                _systemStatus = HealthStatus.Normal;
        }
    }
}
