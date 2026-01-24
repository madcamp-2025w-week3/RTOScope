/*
 * PlayerControllerX.cs - 임시 수동 제어 스크립트
 * 
 * [역할] 키보드 입력을 통한 수동 비행 제어
 * [위치] Runtime Layer > Hardware (Unity MonoBehaviour)
 * 
 * ⚠️ 이 파일은 기존 파일을 이동한 것입니다.
 * ⚠️ 임시 수동 제어 스크립트로, 추후 RTOS 기반 제어로 대체될 예정입니다.
 * ⚠️ RTOS 모드에서는 이 컴포넌트를 비활성화하고 
 *    FlightActuator가 RTOS 명령을 처리합니다.
 */

using UnityEngine;

namespace RTOScope.Runtime.Hardware
{
    /// <summary>
    /// 수동 비행 제어 (키보드 입력)
    /// TODO: RTOS 제어 모드와 수동 제어 모드 전환 기능 추가
    /// </summary>
    public class PlayerControllerX : MonoBehaviour
    {
        [Header("Engine Power")]
        public float maxSpeed = 200f;
        public float minSpeed = 25f;
        public float acceleration = 20f;

        [Header("Control Sensitivity")]
        public float pitchSpeed = 50f;
        public float rollSpeed = 50f;
        public float yawSpeed = 20f;

        [Tooltip("아래 방향키를 눌렀을 때 상승 (비행 시뮬레이터 관례)")]
        public bool invertPitch = true;

        [Header("Status Info (Read Only)")]
        public float currentSpeed = 0f;
        public float targetThrottle = 0f;

        void Start()
        {
            targetThrottle = 0.5f;
            currentSpeed = maxSpeed * 0.5f;
        }

        void Update()
        {
            HandleThrottle();
            HandleMovement();
        }

        void HandleThrottle()
        {
            if (Input.GetKey(KeyCode.LeftShift))
                targetThrottle += Time.deltaTime * 0.5f;
            else if (Input.GetKey(KeyCode.LeftControl))
                targetThrottle -= Time.deltaTime * 0.5f;

            targetThrottle = Mathf.Clamp01(targetThrottle);
            float targetSpeedVal = Mathf.Lerp(minSpeed, maxSpeed, targetThrottle);
            currentSpeed = Mathf.Lerp(currentSpeed, targetSpeedVal, Time.deltaTime * acceleration);
        }

        void HandleMovement()
        {
            // Pitch
            float pitchInput = 0f;
            if (Input.GetKey(KeyCode.UpArrow)) pitchInput = 1f;
            else if (Input.GetKey(KeyCode.DownArrow)) pitchInput = -1f;
            float pitchDir = invertPitch ? -pitchInput : pitchInput;

            // Roll
            float rollInput = 0f;
            if (Input.GetKey(KeyCode.RightArrow)) rollInput = 1f;
            else if (Input.GetKey(KeyCode.LeftArrow)) rollInput = -1f;

            // Yaw
            float yawInput = 0f;
            if (Input.GetKey(KeyCode.E)) yawInput = 1f;
            else if (Input.GetKey(KeyCode.Q)) yawInput = -1f;

            // Apply rotation
            transform.Rotate(Vector3.right * pitchDir * pitchSpeed * Time.deltaTime);
            transform.Rotate(Vector3.up * yawInput * yawSpeed * Time.deltaTime);
            transform.Rotate(Vector3.forward * -rollInput * rollSpeed * Time.deltaTime);

            // Forward movement
            transform.position += transform.forward * currentSpeed * Time.deltaTime;
        }

        void OnGUI()
        {
            GUI.Box(new Rect(20, 20, 200, 60), "MANUAL CONTROL");
            GUI.Label(new Rect(30, 40, 180, 20), 
                $"THR: {(int)(targetThrottle * 100)}%  |  SPD: {(int)currentSpeed} km/h");
            GUI.Label(new Rect(30, 60, 180, 20), 
                $"ALT: {(int)transform.position.y} m");
        }
    }
}
