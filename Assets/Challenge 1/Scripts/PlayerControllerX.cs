using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class PlayerControllerX : MonoBehaviour {
    [Header("Engine Settings")]
    public float maxThrust = 100f;       // 최대 추력
    public float throttleSpeed = 50f;     // 스로틀 증가 속도
    private float currentThrottle = 0f;    // 현재 스로틀 (0~100)

    [Header("Maneuver Settings")]
    public float pitchSpeed = 80f;        // 상하 회전
    public float rollSpeed = 100f;         // 좌우 기울기
    public float yawSpeed = 40f;          // 좌우 평면 회전
    public float liftForce = 1.5f;        // 양력 계수 (속도에 비례)

    private Rigidbody rb;
    private float pitchInput;
    private float rollInput;
    private float yawInput;

    void Start() {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = true;
        rb.mass = 1.5f;
        rb.drag = 0.5f;      // 공기 저항
        rb.angularDrag = 1f; // 회전 저항
    }

    void Update() {
        // 1. 입력 받기 (Input Manager 설정 필요)
        pitchInput = Input.GetAxis("Vertical");     // W, S or 방향키
        rollInput = Input.GetAxis("Horizontal");    // A, D
        yawInput = (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0);

        // 2. 스로틀 조절 (Shift/Ctrl 혹은 추가 키 설정)
        if (Input.GetKey(KeyCode.LeftShift)) currentThrottle += throttleSpeed * Time.deltaTime;
        if (Input.GetKey(KeyCode.LeftControl)) currentThrottle -= throttleSpeed * Time.deltaTime;
        currentThrottle = Mathf.Clamp(currentThrottle, 0f, 100f);
    }

    void FixedUpdate() {
        ApplyFlightPhysics();
    }

    void ApplyFlightPhysics() {
        // 1. 전진 추력 (Forward Thrust)
        rb.AddRelativeForce(Vector3.forward * currentThrottle * maxThrust);

        // 2. 회전 로직 (Pitch, Roll, Yaw)
        // 실제 전투기처럼 조종하기 위해 로컬 좌표계 기준으로 힘을 줌
        rb.AddRelativeTorque(Vector3.right * pitchInput * pitchSpeed);
        rb.AddRelativeTorque(Vector3.forward * -rollInput * rollSpeed);
        rb.AddRelativeTorque(Vector3.up * yawInput * yawSpeed);

        // 3. 양력(Lift) 시뮬레이션
        // 속도가 빠를수록 위로 떠오르는 힘을 줌 (날개의 수직 방향)
        float forwardSpeed = Vector3.Dot(rb.velocity, transform.forward);
        Vector3 lift = transform.up * forwardSpeed * liftForce;
        rb.AddForce(lift);
    }

    // 현재 스로틀 값을 확인하기 위한 GUI (선택 사항)
    void OnGUI() {
        GUI.Box(new Rect(10, 10, 150, 30), "Throttle: " + (int)currentThrottle + "%");
    }
}