using UnityEngine;

namespace RTOScope.Runtime.Aircraft {
    /// <summary>
    /// 콕핏 카메라 고정 추적
    /// - 인스펙터에서 잡은 초기 위치/회전 유지
    /// - 콕핏 기준 상대 트랜스폼만 따라감
    /// </summary>
    public class CockpitCameraFollow : MonoBehaviour {
        [Header("Target")]
        public Transform cockpit;

        // 초기 기준값 (에디터에서 세팅된 값)
        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;

        void Start() {
            if (cockpit == null) {
                Debug.LogError("CockpitCameraFollow: cockpit is null");
                enabled = false;
                return;
            }

            // 에디터에서 잡아둔 상대 위치/회전 저장
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
        }

        void LateUpdate() {
            // 콕핏 기준으로만 이동
            transform.position = cockpit.TransformPoint(initialLocalPosition);

            // 시선은 고정 (콕핏 회전 + 초기 카메라 회전)
            transform.rotation = cockpit.rotation * initialLocalRotation;
        }
    }
}
