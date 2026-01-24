/*
 * HealthMonitor.cs - 시스템 상태 모니터링 태스크
 * 
 * [역할] 워치독(Watchdog) - 시스템 결함 감시 및 비상 안전 모드 전환
 * [위치] RTOS Layer > Tasks (Unity API 사용 금지)
 * [우선순위] Medium (Soft Deadline)
 * 
 * [구현 예정]
 * - CPU/메모리 사용률 모니터링
 * - 데드라인 미스 추적
 * - 센서 이상 감지
 * - 자원 임계치 감시
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
        private HealthStatus _systemStatus;
        private float _cpuUsage;
        private float _memoryUsage;
        private int _deadlineMissCount;

        public string Name => "HealthMonitor";
        public HealthStatus SystemStatus => _systemStatus;
        public float CpuUsage => _cpuUsage;
        public float MemoryUsage => _memoryUsage;

        public HealthMonitor()
        {
            _systemStatus = HealthStatus.Normal;
            _cpuUsage = 0f;
            _memoryUsage = 0f;
            _deadlineMissCount = 0;
        }

        public void Initialize()
        {
            // TODO: 초기화 로직 구현
            // - 임계값 설정
            // - 모니터링 대상 등록
            
            _systemStatus = HealthStatus.Normal;
            _cpuUsage = 0f;
            _memoryUsage = 0f;
            _deadlineMissCount = 0;
        }

        public void Execute(float deltaTime)
        {
            // TODO: 시스템 건강 상태 점검 로직 구현
            // 1. TaskStatistics에서 CPU 사용률 조회
            // 2. MemoryManager에서 메모리 사용률 조회
            // 3. DeadlineManager에서 미스 카운트 조회
            // 4. 임계값 기반 상태 판단
            // 5. 비상 상황 시 안전 모드 전환
            
            CheckSystemHealth();
        }

        public void Cleanup()
        {
            // TODO: 정리 로직 구현
        }

        public void OnDeadlineMiss()
        {
            // TODO: 워치독 데드라인 미스 처리
            // - 이는 시스템 과부하 징후일 수 있음
            // - 로그 기록
        }

        private void CheckSystemHealth()
        {
            // TODO: 실제 모니터링 로직 구현
            
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
