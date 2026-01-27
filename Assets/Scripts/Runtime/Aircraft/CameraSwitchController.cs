using UnityEngine;

namespace RTOScope.Runtime.Aircraft {
    /// <summary>
    /// 외부 카메라 ↔ 콕핏 카메라 전환 컨트롤러
    /// </summary>
    public class CameraSwitchController : MonoBehaviour {
        [Header("Cameras")]
        public Camera mainCamera;
        public Camera cockpitCamera;

        private bool isCockpitView = false;

        void Start() {
            SetCockpitView(false);
        }

        void Update() {
            if (Input.GetKeyDown(KeyCode.V)) {
                ToggleView();
            }
        }

        private void ToggleView() {
            isCockpitView = !isCockpitView;
            SetCockpitView(isCockpitView);
        }

        public void ForceExternalView() {
            isCockpitView = false;
            SetCockpitView(false);
        }

        private void SetCockpitView(bool cockpit) {
            mainCamera.enabled = !cockpit;
            cockpitCamera.enabled = cockpit;

            // Audio Listener 중복 방지
            if (mainCamera.TryGetComponent(out AudioListener mainAudio))
                mainAudio.enabled = !cockpit;

            if (cockpitCamera.TryGetComponent(out AudioListener cockpitAudio))
                cockpitAudio.enabled = cockpit;
        }
    }
}
