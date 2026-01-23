using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FollowPlayerX : MonoBehaviour {
    public GameObject plane;
    // 비행기 뒤쪽 상단에 위치하도록 오프셋 설정 (필요에 따라 조절해)
    [SerializeField] private Vector3 offset = new Vector3(0, 3, -10);
    // 카메라가 따라오는 부드러움 정도 (0에 가까울수록 부드러움)
    [SerializeField] private float smoothSpeed = 0.005f;

    void LateUpdate() {
        if (plane == null) return;

        // 1. 목표 위치 계산 (이전과 동일)
        Vector3 targetPosition = plane.transform.position + (plane.transform.rotation * offset);
        transform.position = Vector3.Lerp(transform.position, targetPosition, smoothSpeed);

        // 2. 핵심 수정: 비행기의 방향(Forward)과 위쪽(Up)을 카메라와 동기화
        // LookAt의 두 번째 인자로 비행기의 transform.up을 전달함
        transform.LookAt(plane.transform.position, plane.transform.up);
    }
}