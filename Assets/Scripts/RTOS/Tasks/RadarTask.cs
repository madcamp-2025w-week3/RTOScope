/*
 * RadarTask.cs - 레이더 처리 태스크
 * 
 * [역할] 레이더 신호 처리 및 표적 추적 - 중위권 우선순위
 * [위치] RTOS Layer > Tasks (Unity API 사용 금지)
 * [우선순위] High (Soft Deadline) - 위반 시 성능 저하만 발생
 * 
 * [구현 예정]
 * - 레이더 스윕 시뮬레이션
 * - 표적 탐지 및 추적
 * - 위협 평가
 */

using System.Collections.Generic;
using RTOScope.RTOS.Kernel;

namespace RTOScope.RTOS.Tasks
{
    /// <summary>
    /// 레이더 표적 정보
    /// </summary>
    public struct RadarTarget
    {
        public int Id;
        public float Distance;
        public float Bearing;
        public float Altitude;
        public float Velocity;
        public bool IsHostile;
    }

    /// <summary>
    /// 레이더 처리 태스크
    /// 주기: 50ms (20Hz)
    /// Soft Deadline - 적기 탐지 및 타겟 정보 갱신
    /// </summary>
    public class RadarTask : IRTOSTask
    {
        private readonly List<RadarTarget> _detectedTargets;
        private float _currentSweepAngle;
        private readonly float _sweepSpeed = 360f;  // degrees per second

        public string Name => "Radar";
        public IReadOnlyList<RadarTarget> DetectedTargets => _detectedTargets;
        public float CurrentSweepAngle => _currentSweepAngle;

        public RadarTask()
        {
            _detectedTargets = new List<RadarTarget>();
            _currentSweepAngle = 0f;
        }

        public void Initialize()
        {
            // TODO: 초기화 로직 구현
            // - 레이더 파라미터 설정
            // - 표적 추적 알고리즘 초기화
            
            _detectedTargets.Clear();
            _currentSweepAngle = 0f;
        }

        public void Execute(float deltaTime)
        {
            // TODO: 레이더 처리 로직 구현
            // 1. 스윕 각도 업데이트
            // 2. 현재 스윕 영역의 표적 탐지
            // 3. 기존 표적 추적 업데이트
            // 4. 표적 목록 갱신
            
            UpdateSweep(deltaTime);
            ProcessRadarReturns();
            UpdateTracking();
        }

        public void Cleanup()
        {
            // TODO: 정리 로직 구현
            _detectedTargets.Clear();
        }

        public void OnDeadlineMiss()
        {
            // TODO: Soft Deadline 미스 처리
            // - 레이더 갱신 지연됨 (성능 저하)
            // - 로그 기록
        }

        private void UpdateSweep(float deltaTime)
        {
            _currentSweepAngle += _sweepSpeed * deltaTime;
            if (_currentSweepAngle >= 360f)
                _currentSweepAngle -= 360f;
        }

        private void ProcessRadarReturns()
        {
            // TODO: 레이더 반사 신호 처리
            // - 노이즈 필터링
            // - 표적 식별
        }

        private void UpdateTracking()
        {
            // TODO: 표적 추적 알고리즘
            // - 칼만 필터 등
        }
    }
}
