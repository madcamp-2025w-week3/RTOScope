using UnityEngine;

public class PlayerControllerX : MonoBehaviour {
    [Header("Engine Power")]
    public float maxSpeed = 200f;         // 최대 속도
    public float minSpeed = 25f;          // 최소 속도 (실속 속도)
    public float acceleration = 20f;      // 스로틀 반응 속도

    [Header("Control Sensitivity")]
    public float pitchSpeed = 50f;        // 상하 회전 민감도
    public float rollSpeed = 50f;         // 좌우 롤 민감도
    public float yawSpeed = 20f;          // 좌우 요 민감도

    [Tooltip("체크하면 '아래 방향키'를 눌렀을 때 비행기가 상승합니다. (비행 시뮬레이터 국룰)")]
    public bool invertPitch = true;

    [Header("Status Info (Read Only)")]
    public float currentSpeed = 0f;
    public float targetThrottle = 0f;     // 0.0 ~ 1.0 (0% ~ 100%)

    void Start() {
        // 시작 시 중간 속도로 비행
        targetThrottle = 0.5f;
        currentSpeed = maxSpeed * 0.5f;
    }

    void Update() {
        HandleThrottle(); // 속도 제어 (Shift/Ctrl)
        HandleMovement(); // 방향 제어 (방향키 + QE)
    }

    void HandleThrottle() {
        // Left Shift: 가속
        if (Input.GetKey(KeyCode.LeftShift)) {
            targetThrottle += Time.deltaTime * 0.5f;
        }
        // Left Control: 감속
        else if (Input.GetKey(KeyCode.LeftControl)) {
            targetThrottle -= Time.deltaTime * 0.5f;
        }

        targetThrottle = Mathf.Clamp01(targetThrottle);

        // 목표 속도까지 부드럽게 도달
        float targetSpeedVal = Mathf.Lerp(minSpeed, maxSpeed, targetThrottle);
        currentSpeed = Mathf.Lerp(currentSpeed, targetSpeedVal, Time.deltaTime * acceleration);
    }

    void HandleMovement() {
        // 1. Pitch (상하 회전) - 방향키 위/아래
        float pitchInput = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) pitchInput = 1f;
        else if (Input.GetKey(KeyCode.DownArrow)) pitchInput = -1f;

        // 시뮬레이터 방식: '아래 키'를 누르면 상승(Pitch Up)해야 하므로 부호 반전
        // invertPitch가 켜져 있으면 Down(-1)일 때 위로 솟아야 함 -> 양수(+) 회전 필요
        float pitchDir = invertPitch ? -pitchInput : pitchInput;

        // 2. Roll (좌우 기울기) - 방향키 좌/우
        float rollInput = 0f;
        if (Input.GetKey(KeyCode.RightArrow)) rollInput = 1f;
        else if (Input.GetKey(KeyCode.LeftArrow)) rollInput = -1f;

        // 3. Yaw (수평 회전) - Q / E
        float yawInput = 0f;
        if (Input.GetKey(KeyCode.E)) yawInput = 1f;
        else if (Input.GetKey(KeyCode.Q)) yawInput = -1f;

        // 4. 회전 적용 (Transform 직접 회전)
        // Pitch(X축), Yaw(Y축), Roll(Z축)
        transform.Rotate(Vector3.right * pitchDir * pitchSpeed * Time.deltaTime);
        transform.Rotate(Vector3.up * yawInput * yawSpeed * Time.deltaTime);
        transform.Rotate(Vector3.forward * -rollInput * rollSpeed * Time.deltaTime);

        // 5. 전진 이동
        transform.position += transform.forward * currentSpeed * Time.deltaTime;
    }

    void OnGUI() {
        // 조종석 HUD 정보 표시
        GUI.Box(new Rect(20, 20, 200, 60), "FLIGHT SYSTEM");
        GUI.Label(new Rect(30, 40, 180, 20), $"THR: {(int)(targetThrottle * 100)}%  |  SPD: {(int)currentSpeed} km/h");
        GUI.Label(new Rect(30, 60, 180, 20), $"ALT: {(int)transform.position.y} m");
    }
}