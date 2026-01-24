/*
 * FollowPlayerX.cs - 카메라 추적 스크립트
 * 
 * [역할] 플레이어 항공기를 따라가는 카메라 제어
 * [위치] Runtime Layer > Aircraft (Unity MonoBehaviour)
 * 
 * ⚠️ 이 파일은 기존 파일을 이동한 것입니다.
 * ⚠️ RTOS 로직과는 무관한 순수 시각화/카메라 스크립트입니다.
 * ⚠️ 추후 RTOS 기반 제어로 대체되지 않습니다.
 */

using UnityEngine;

namespace RTOScope.Runtime.Aircraft
{
    /// <summary>
    /// 카메라 추적 스크립트
    /// RTOS와 무관한 뷰 전용 컴포넌트
    /// </summary>
    public class FollowPlayerX : MonoBehaviour
    {
        public GameObject plane;
        [SerializeField] private Vector3 offset = new Vector3(0, 2, -7);
        [SerializeField] private float positionSmoothTime = 0.2f;
        [SerializeField] private float rotationSmoothTime = 5.0f;

        private Vector3 velocity = Vector3.zero;

        void LateUpdate()
        {
            if (plane == null) return;

            // 1. 목표 위치 계산
            Vector3 targetPosition = plane.transform.position + (plane.transform.rotation * offset);

            // 2. 위치 추종
            transform.position = Vector3.SmoothDamp(
                transform.position,
                targetPosition,
                ref velocity,
                positionSmoothTime
            );

            // 3. 회전 추종
            Quaternion targetRotation = Quaternion.LookRotation(
                plane.transform.position - transform.position, 
                plane.transform.up
            );
            transform.rotation = Quaternion.Slerp(
                transform.rotation, 
                targetRotation, 
                Time.deltaTime * rotationSmoothTime
            );
        }
    }
}
