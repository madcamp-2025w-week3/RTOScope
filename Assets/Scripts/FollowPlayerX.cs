using UnityEngine;

public class FollowPlayerX : MonoBehaviour {
    public GameObject plane;
    [SerializeField] private Vector3 offset = new Vector3(0, 2, -7); // 약간 더 뒤로 배치
    [SerializeField] private float positionSmoothTime = 0.2f;      // 위치 부드러움 (조금 늘림)
    [SerializeField] private float rotationSmoothTime = 5.0f;      // 회전 부드러움 (추가)

    private Vector3 velocity = Vector3.zero;

    void LateUpdate() {
        if (plane == null) return;

        // 1. 목표 위치 계산
        Vector3 targetPosition = plane.transform.position + (plane.transform.rotation * offset);

        // 2. 위치 추종 (SmoothDamp 유지하되 수치 조절)
        transform.position = Vector3.SmoothDamp(
            transform.position,
            targetPosition,
            ref velocity,
            positionSmoothTime
        );

        // 3. 회전 추종 (LookAt 대신 Slerp로 부드럽게 방향 전환)
        // 비행기의 현재 방향을 바라보게 함
        Quaternion targetRotation = Quaternion.LookRotation(plane.transform.position - transform.position, plane.transform.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSmoothTime);
    }
}