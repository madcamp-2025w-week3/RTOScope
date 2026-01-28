/*
 * RangeEstimatorTask.cs - 비행 가능 범위 계산 (RTOS)
 *
 * [역할]
 * - 현재 연료 소모율로 남은 비행 가능 시간/거리 계산
 * - Bingo Fuel (귀환 필요) / Joker Fuel (연료 주의) 경고
 *
 * [상태 머신 단계]
 * Step 0: Calculate Endurance (비행 가능 시간 계산)
 * Step 1: Calculate Range (비행 가능 거리 계산)
 * Step 2: Fuel Warning Check (연료 경고 체크)
 *
 * [위치] RTOS Layer > Tasks
 */

using RTOScope.RTOS.Kernel;
using RTOScope.Runtime.Aircraft;
using UnityEngine;

namespace RTOScope.RTOS.Tasks
{
    public class RangeEstimatorTask : IRTOSTask
    {
        // =====================================================================
        // 상태 머신 정의
        // =====================================================================

        private const int STEP_CALC_ENDURANCE = 0;
        private const int STEP_CALC_RANGE = 1;
        private const int STEP_FUEL_WARNING = 2;
        private const int TOTAL_STEPS = 3;

        // 각 Step의 WCET - 스케줄러 비교용 증가
        private static readonly float[] _stepWCETs =
        {
            0.005f,  // Step 0: Endurance 계산 (5ms)
            0.005f,  // Step 1: Range 계산 (5ms)
            0.005f   // Step 2: 연료 경고 (5ms)
        };           // 총 WCET: 15ms

        // =====================================================================
        // 연료 경고 임계값
        // =====================================================================

        // Joker Fuel: 작전 중단 고려 필요 (연료 1분 이하)
        private const float JOKER_FUEL_MINUTES = 1f;

        // Bingo Fuel: 즉시 귀환 필요 (연료 30초 이하)
        private const float BINGO_FUEL_MINUTES = 0.5f;

        // 최소 소모율 (0 division 방지)
        private const float MIN_CONSUMPTION_RATE = 0.01f;

        // =====================================================================
        // 필드
        // =====================================================================

        private int _currentStep;
        private AircraftState _state;

        private float _enduranceMinutes;
        private float _rangeKm;
        private float _averageSpeed; // m/s

        private bool _log = true;

        // =====================================================================
        // 프로퍼티
        // =====================================================================

        public string Name => "RangeEstimator";
        public int CurrentStep => _currentStep;
        public int TotalSteps => TOTAL_STEPS;
        public float CurrentStepWCET => _currentStep < TOTAL_STEPS ? _stepWCETs[_currentStep] : 0f;
        public bool IsWorkComplete => _currentStep >= TOTAL_STEPS;

        // =====================================================================
        // 생성자
        // =====================================================================

        public RangeEstimatorTask(AircraftState state)
        {
            _state = state;
            _currentStep = 0;
        }

        // =====================================================================
        // IRTOSTask 구현
        // =====================================================================

        public void Initialize()
        {
            _currentStep = 0;
            Log("[RangeEstimator] 초기화 완료");
        }

        public void ResetForNextPeriod()
        {
            _currentStep = 0;
        }

        public void Cleanup()
        {
            // 정리 작업 없음
        }

        public void OnDeadlineMiss()
        {
            // Soft Deadline - 범위 계산 지연 (치명적이지 않음)
        }

        public void ExecuteStep()
        {
            if (_state == null) return;

            switch (_currentStep)
            {
                case STEP_CALC_ENDURANCE:
                    ExecuteCalcEndurance();
                    break;
                case STEP_CALC_RANGE:
                    ExecuteCalcRange();
                    break;
                case STEP_FUEL_WARNING:
                    ExecuteFuelWarning();
                    break;
            }

            _currentStep++;
        }

        // =====================================================================
        // Step 0: 비행 가능 시간 계산
        // =====================================================================

        private void ExecuteCalcEndurance()
        {
            // 연료 잔량 (리터)
            float fuelRemaining = _state.FuelRemainingLiters;

            // 연료 소모율 (L/s) - FuelConsumptionRate는 L/s 단위
            float consumptionRate = Mathf.Max(_state.FuelConsumptionRate, MIN_CONSUMPTION_RATE);

            // 비행 가능 시간 (초)
            float enduranceSeconds = fuelRemaining / consumptionRate;

            // 분 단위로 변환
            _enduranceMinutes = enduranceSeconds / 60f;

            // AircraftState 업데이트
            _state.EnduranceMinutes = _enduranceMinutes;
        }

        // =====================================================================
        // Step 1: 비행 가능 거리 계산
        // =====================================================================

        private void ExecuteCalcRange()
        {
            // 현재 속도 (m/s)
            _averageSpeed = _state.Velocity;

            // 비행 가능 거리 (m) = 속도 * 시간
            float rangeMeters = _averageSpeed * (_enduranceMinutes * 60f);

            // km 단위로 변환
            _rangeKm = rangeMeters / 1000f;

            // AircraftState 업데이트
            _state.RangeKm = _rangeKm;
        }

        // =====================================================================
        // Step 2: 연료 경고 체크
        // =====================================================================

        private void ExecuteFuelWarning()
        {
            bool prevBingo = _state.BingoFuel;
            bool prevJoker = _state.JokerFuel;

            // Bingo Fuel: 15분 이하
            _state.BingoFuel = _enduranceMinutes <= BINGO_FUEL_MINUTES;

            // Joker Fuel: 30분 이하 (Bingo 아닐 때만)
            _state.JokerFuel = !_state.BingoFuel && _enduranceMinutes <= JOKER_FUEL_MINUTES;

            // 경고 상태 변경 시 로그
            if (_state.BingoFuel && !prevBingo)
            {
                Log($"[RangeEstimator] ⚠️ BINGO FUEL! 즉시 귀환 필요! (남은 시간: {_enduranceMinutes:F1}분, 거리: {_rangeKm:F1}km)");
            }
            else if (_state.JokerFuel && !prevJoker)
            {
                Log($"[RangeEstimator] ⚠️ JOKER FUEL! 연료 주의! (남은 시간: {_enduranceMinutes:F1}분, 거리: {_rangeKm:F1}km)");
            }
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        private void Log(string msg)
        {
            if (_log)
                Debug.Log(msg);
        }
    }
}
