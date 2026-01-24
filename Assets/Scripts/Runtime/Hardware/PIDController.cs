/*
 * PIDController.cs - PID 제어기
 * 
 * [역할] PID 제어 알고리즘 구현
 * [위치] Runtime Layer > Hardware
 * 
 * [설계 의도]
 * - PID 제어는 임베디드 시스템의 핵심 제어 알고리즘
 * - 비행 제어, 자세 안정화 등에 사용
 * - RTOS 태스크에서 호출 가능 (순수 C#)
 */

namespace RTOScope.Runtime.Hardware
{
    /// <summary>
    /// PID 제어기 (Proportional-Integral-Derivative Controller)
    /// </summary>
    public class PIDController
    {
        // PID 게인
        private readonly float _kp;  // 비례 게인
        private readonly float _ki;  // 적분 게인
        private readonly float _kd;  // 미분 게인

        // 내부 상태
        private float _integral;
        private float _previousError;
        private float _outputMin;
        private float _outputMax;

        public float Kp => _kp;
        public float Ki => _ki;
        public float Kd => _kd;

        /// <summary>
        /// PID 컨트롤러 생성
        /// </summary>
        /// <param name="kp">비례 게인</param>
        /// <param name="ki">적분 게인</param>
        /// <param name="kd">미분 게인</param>
        /// <param name="outputMin">최소 출력값</param>
        /// <param name="outputMax">최대 출력값</param>
        public PIDController(float kp, float ki, float kd, 
            float outputMin = -1f, float outputMax = 1f)
        {
            _kp = kp;
            _ki = ki;
            _kd = kd;
            _outputMin = outputMin;
            _outputMax = outputMax;
            Reset();
        }

        /// <summary>
        /// PID 계산 수행
        /// </summary>
        /// <param name="setpoint">목표값</param>
        /// <param name="measured">현재 측정값</param>
        /// <param name="deltaTime">경과 시간</param>
        /// <returns>제어 출력</returns>
        public float Compute(float setpoint, float measured, float deltaTime)
        {
            if (deltaTime <= 0f)
                return 0f;

            // 오차 계산
            float error = setpoint - measured;

            // P 항: 현재 오차에 비례
            float pTerm = _kp * error;

            // I 항: 오차의 적분 (누적)
            _integral += error * deltaTime;
            // Anti-windup: 적분 포화 방지
            float maxIntegral = (_outputMax - _outputMin) / 2f;
            _integral = Clamp(_integral, -maxIntegral, maxIntegral);
            float iTerm = _ki * _integral;

            // D 항: 오차의 변화율
            float derivative = (error - _previousError) / deltaTime;
            float dTerm = _kd * derivative;

            _previousError = error;

            // 출력 계산 및 제한
            float output = pTerm + iTerm + dTerm;
            return Clamp(output, _outputMin, _outputMax);
        }

        /// <summary>내부 상태 리셋</summary>
        public void Reset()
        {
            _integral = 0f;
            _previousError = 0f;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }
    }
}
