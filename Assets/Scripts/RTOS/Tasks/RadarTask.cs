/*
 * RadarTask.cs - 레이더 처리 태스크
 *
 * [역할] 레이더 신호 처리 및 표적 추적 - 중위권 우선순위
 * [위치] RTOS Layer > Tasks (Unity API 사용 금지)
 * [우선순위] High (Soft Deadline) - 위반 시 성능 저하만 발생
 *
 * [상태 머신 설계]
 * Step 0: 레이더 스윕 업데이트
 * Step 1: Raw 데이터 수집
 * Step 2: 노이즈 필터링
 * Step 3: 표적 식별
 * Step 4: 추적 데이터 업데이트
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
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_SWEEP_UPDATE = 0;
        private const int STEP_COLLECT_DATA = 1;
        private const int STEP_FILTER_NOISE = 2;
        private const int STEP_IDENTIFY_TARGETS = 3;
        private const int STEP_UPDATE_TRACKING = 4;
        private const int TOTAL_STEPS = 5;

        // 각 Step의 WCET (초 단위)
        private static readonly float[] _stepWCETs = {
            0.002f,   // Step 0: 스윕 (2ms)
            0.003f,   // Step 1: 데이터 수집 (3ms)
            0.005f,   // Step 2: 필터링 (5ms)
            0.008f,   // Step 3: 표적 식별 (8ms)
            0.002f    // Step 4: 추적 업데이트 (2ms)
        };                // 총 WCET: 20ms

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;
        private readonly List<RadarTarget> _detectedTargets;
        private float _currentSweepAngle;
        private readonly float _sweepSpeed = 360f;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "Radar";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;
        public IReadOnlyList<RadarTarget> DetectedTargets => _detectedTargets;
        public float CurrentSweepAngle => _currentSweepAngle;

        // =====================================================================
        // 생성자
        // =====================================================================

        public RadarTask()
        {
            _detectedTargets = new List<RadarTarget>();
            _currentStep = 0;
            _currentSweepAngle = 0f;
        }

        // =====================================================================
        // IRTOSTask 구현
        // =====================================================================

        public void Initialize()
        {
            _detectedTargets.Clear();
            _currentStep = 0;
            _currentSweepAngle = 0f;
        }

        public void ExecuteStep()
        {
            switch (_currentStep)
            {
                case STEP_SWEEP_UPDATE:
                    UpdateSweep();
                    _currentStep++;
                    break;

                case STEP_COLLECT_DATA:
                    CollectRawData();
                    _currentStep++;
                    break;

                case STEP_FILTER_NOISE:
                    FilterNoise();
                    _currentStep++;
                    break;

                case STEP_IDENTIFY_TARGETS:
                    IdentifyTargets();
                    _currentStep++;
                    break;

                case STEP_UPDATE_TRACKING:
                    UpdateTracking();
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
            _detectedTargets.Clear();
        }

        public void OnDeadlineMiss()
        {
            // Soft Deadline 미스: 레이더 갱신 지연됨
        }

        // =====================================================================
        // 비공개 메서드
        // =====================================================================

        private void UpdateSweep()
        {
            // 0.05초(50ms) 주기 기준 스윕 각도 업데이트
            _currentSweepAngle += _sweepSpeed * 0.05f;
            if (_currentSweepAngle >= 360f)
                _currentSweepAngle -= 360f;
        }

        private void CollectRawData()
        {
            // TODO: 레이더 반사 신호 수집 시뮬레이션
        }

        private void FilterNoise()
        {
            // TODO: 노이즈 필터링 알고리즘
        }

        private void IdentifyTargets()
        {
            // TODO: 표적 식별 알고리즘 (IFF 등)
        }

        private void UpdateTracking()
        {
            // TODO: 칼만 필터 기반 추적 업데이트
        }
    }
}
