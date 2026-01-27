/*
 * TargetingReticleController.cs - 타겟 추적 조준점 컨트롤러
 *
 * [역할]
 * - 타겟 감지 시 조준 원(circle)을 타겟 방향으로 이동
 * - 락온 가능 거리(1500m) 이내 진입 시 빨간색으로 변경
 * - 전투기 HUD 스타일 타겟팅 UI
 *
 * [위치] Runtime Layer > UI (Unity MonoBehaviour)
 */

using UnityEngine;
using UnityEngine.UI;
using RTOScope.Runtime.Bootstrap;
using RTOScope.Runtime.Aircraft;

namespace RTOScope.Runtime.UI
{
    /// <summary>
    /// 타겟 추적 조준점 컨트롤러
    /// 타겟 위치를 화면에 표시하고 락온 상태에 따라 색상 변경
    /// </summary>
    public class TargetingReticleController : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("References")]
        [Tooltip("타겟 추적 조준점 이미지 (RectTransform)")]
        [SerializeField] private RectTransform targetReticle;

        [Tooltip("조준점 이미지 컴포넌트 (색상 변경용)")]
        [SerializeField] private Image reticleImage;

        [Tooltip("메인 카메라 (조종석 카메라)")]
        [SerializeField] private Camera targetCamera;

        [Tooltip("RTOS Runner (AircraftState 접근)")]
        [SerializeField] private RTOSRunner rtosRunner;

        [Tooltip("HUD Canvas의 RectTransform")]
        [SerializeField] private RectTransform canvasRect;

        [Header("Lock-on Settings")]
        [Tooltip("락온 가능 거리 (m)")]
        [SerializeField] private float lockOnRange = 1500f;

        [Tooltip("락온 가능 FOV (도, 절반값)")]
        [SerializeField] private float lockOnFovHalf = 20f;

        [Header("Colors")]
        [Tooltip("기본 색상 (타겟 감지, 락온 불가)")]
        [SerializeField] private Color normalColor = new Color(0f, 1f, 0f, 0.9f); // 녹색

        [Tooltip("락온 가능 색상 (사거리 내)")]
        [SerializeField] private Color lockableColor = new Color(1f, 1f, 0f, 0.9f); // 노란색

        [Tooltip("락온 완료 색상")]
        [SerializeField] private Color lockedColor = new Color(1f, 0f, 0f, 0.9f); // 빨간색

        [Header("Movement")]
        [Tooltip("조준점 이동 부드러움 (값이 클수록 빠름)")]
        [SerializeField] private float smoothSpeed = 15f;

        [Tooltip("화면 가장자리 패딩 (px)")]
        [SerializeField] private float edgePadding = 50f;

        [Header("Display Options")]
        [Tooltip("타겟 없을 때 조준점 숨기기")]
        [SerializeField] private bool hideWhenNoTarget = true;

        [Tooltip("화면 밖 타겟도 표시 (가장자리에 고정)")]
        [SerializeField] private bool showOffScreenTargets = true;

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private AircraftState _state;
        private Vector3 _targetScreenPos;
        private Vector2 _currentReticlePos;
        private Vector2 _centerPos;
        private Vector2 _homePosition; // 초기 위치 (에디터에서 설정한 위치)
        private bool _isVisible;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void Start()
        {
            if (canvasRect != null)
            {
                _centerPos = new Vector2(canvasRect.rect.width / 2f, canvasRect.rect.height / 2f);
            }
            else
            {
                _centerPos = new Vector2(Screen.width / 2f, Screen.height / 2f);
            }

            // 초기 위치 저장 (에디터에서 설정한 위치)
            if (targetReticle != null)
            {
                _homePosition = targetReticle.anchoredPosition;
            }
            else
            {
                _homePosition = Vector2.zero;
            }

            _currentReticlePos = _homePosition;
        }

        private void LateUpdate()
        {
            UpdateState();
            UpdateReticlePosition();
            UpdateReticleColor();
            UpdateVisibility();
        }

        // =====================================================================
        // 초기화
        // =====================================================================

        private void AutoAssignReferences()
        {
            if (targetCamera == null)
            {
                var cockpitCamGo = GameObject.Find("Cockpit Camera");
                if (cockpitCamGo != null)
                    targetCamera = cockpitCamGo.GetComponent<Camera>();
                else
                    targetCamera = Camera.main;
            }

            if (rtosRunner == null)
                rtosRunner = FindObjectOfType<RTOSRunner>();

            if (targetReticle == null)
                targetReticle = GetComponent<RectTransform>();

            if (reticleImage == null && targetReticle != null)
                reticleImage = targetReticle.GetComponent<Image>();

            if (canvasRect == null && targetReticle != null)
            {
                var canvas = targetReticle.GetComponentInParent<Canvas>();
                if (canvas != null)
                    canvasRect = canvas.GetComponent<RectTransform>();
            }
        }

        private void UpdateState()
        {
            if (_state == null && rtosRunner != null)
            {
                _state = rtosRunner.State;
            }
        }

        // =====================================================================
        // 조준점 위치 업데이트
        // =====================================================================

        private void UpdateReticlePosition()
        {
            if (targetReticle == null || targetCamera == null || _state == null)
            {
                // 참조가 없어도 조준원은 중앙에 표시
                _isVisible = true;
                return;
            }

            // 타겟이 감지되었는지 확인
            bool hasTarget = _state.TargetCandidateAvailable || _state.LockedTargetValid;

            if (!hasTarget)
            {
                // 타겟 없으면 초기 위치(에디터에서 설정한 위치)로 부드럽게 이동
                _currentReticlePos = Vector2.Lerp(_currentReticlePos, _homePosition, Time.deltaTime * smoothSpeed);
                _isVisible = true; // 항상 표시
            }
            else
            {
                // 타겟 위치 가져오기 (락온된 타겟 우선)
                Vector3 targetWorldPos = _state.LockedTargetValid
                    ? _state.LockedTargetPosition
                    : _state.TargetCandidatePosition;

                // 월드 좌표 → Viewport 좌표 변환 (0~1 범위)
                Vector3 viewportPos = targetCamera.WorldToViewportPoint(targetWorldPos);

                // 타겟이 카메라 뒤에 있는지 확인
                bool isBehindCamera = viewportPos.z < 0;

                if (isBehindCamera)
                {
                    // 카메라 뒤면 초기 위치로 이동 (타겟을 볼 수 없으므로)
                    _currentReticlePos = Vector2.Lerp(_currentReticlePos, _homePosition, Time.deltaTime * smoothSpeed);
                    _isVisible = true;
                }
                else
                {
                    // 월드 좌표 → 스크린 좌표 변환
                    Vector3 screenPos = targetCamera.WorldToScreenPoint(targetWorldPos);

                    // Canvas 로컬 좌표로 변환 (Canvas 스케일링 자동 처리)
                    Vector2 targetCanvasPos;
                    if (canvasRect != null)
                    {
                        // Canvas의 렌더 카메라 가져오기
                        Canvas canvas = canvasRect.GetComponent<Canvas>();
                        Camera canvasCamera = null;
                        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        {
                            canvasCamera = canvas.worldCamera;
                        }

                        // 스크린 좌표 → Canvas 로컬 좌표 변환
                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            canvasRect,
                            new Vector2(screenPos.x, screenPos.y),
                            canvasCamera,
                            out targetCanvasPos
                        );
                    }
                    else
                    {
                        // 폴백: 스크린 좌표 사용 (중앙 기준)
                        targetCanvasPos = new Vector2(
                            screenPos.x - Screen.width / 2f,
                            screenPos.y - Screen.height / 2f
                        );
                    }

                    // 화면 밖 타겟 처리 - 가장자리에 클램프
                    bool isOnScreen = IsOnScreenLocal(targetCanvasPos);

                    if (!isOnScreen)
                    {
                        // 화면 가장자리에 고정
                        targetCanvasPos = ClampToScreenLocal(targetCanvasPos);
                    }

                    // 부드러운 이동
                    _currentReticlePos = Vector2.Lerp(_currentReticlePos, targetCanvasPos, Time.deltaTime * smoothSpeed);
                    _isVisible = true; // 타겟이 있으면 항상 표시
                }
            }

            // 위치 적용 (anchoredPosition은 중앙 기준)
            targetReticle.anchoredPosition = _currentReticlePos;
        }

        // =====================================================================
        // 조준점 색상 업데이트
        // =====================================================================

        private void UpdateReticleColor()
        {
            if (reticleImage == null || _state == null)
                return;

            Color targetColor = normalColor;

            // 락온되어 있으면 빨간색
            if (_state.LockedTargetValid)
            {
                targetColor = lockedColor;
            }
            // 타겟이 있고 사거리/FOV 내면 노란색
            else if (_state.TargetCandidateAvailable)
            {
                float distance = _state.TargetCandidateDistance;
                float angle = _state.TargetCandidateAngle;

                bool inRange = distance <= lockOnRange;
                bool inFov = angle <= lockOnFovHalf;

                if (inRange && inFov)
                {
                    targetColor = lockableColor; // 노란색 - 락온 가능
                }
                else
                {
                    targetColor = normalColor; // 녹색 - 감지만 됨
                }
            }

            // 색상 부드럽게 전환
            reticleImage.color = Color.Lerp(reticleImage.color, targetColor, Time.deltaTime * 10f);
        }

        // =====================================================================
        // 가시성 업데이트
        // =====================================================================

        private void UpdateVisibility()
        {
            if (targetReticle == null)
                return;

            targetReticle.gameObject.SetActive(_isVisible);
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        /// <summary>중앙 기준 로컬 좌표가 화면 내에 있는지 확인</summary>
        private bool IsOnScreenLocal(Vector2 pos)
        {
            float halfWidth, halfHeight;

            if (canvasRect != null)
            {
                halfWidth = canvasRect.rect.width / 2f - edgePadding;
                halfHeight = canvasRect.rect.height / 2f - edgePadding;
            }
            else
            {
                halfWidth = Screen.width / 2f - edgePadding;
                halfHeight = Screen.height / 2f - edgePadding;
            }

            return pos.x >= -halfWidth && pos.x <= halfWidth &&
                   pos.y >= -halfHeight && pos.y <= halfHeight;
        }

        /// <summary>중앙 기준 로컬 좌표를 화면 가장자리로 클램프</summary>
        private Vector2 ClampToScreenLocal(Vector2 pos)
        {
            float halfWidth, halfHeight;

            if (canvasRect != null)
            {
                halfWidth = canvasRect.rect.width / 2f - edgePadding;
                halfHeight = canvasRect.rect.height / 2f - edgePadding;
            }
            else
            {
                halfWidth = Screen.width / 2f - edgePadding;
                halfHeight = Screen.height / 2f - edgePadding;
            }

            return new Vector2(
                Mathf.Clamp(pos.x, -halfWidth, halfWidth),
                Mathf.Clamp(pos.y, -halfHeight, halfHeight)
            );
        }

        private bool IsOnScreen(Vector2 pos)
        {
            if (canvasRect != null)
            {
                return pos.x >= edgePadding &&
                       pos.x <= canvasRect.rect.width - edgePadding &&
                       pos.y >= edgePadding &&
                       pos.y <= canvasRect.rect.height - edgePadding;
            }

            return pos.x >= edgePadding &&
                   pos.x <= Screen.width - edgePadding &&
                   pos.y >= edgePadding &&
                   pos.y <= Screen.height - edgePadding;
        }

        private Vector2 ClampToScreen(Vector2 pos)
        {
            float maxX, maxY;

            if (canvasRect != null)
            {
                maxX = canvasRect.rect.width - edgePadding;
                maxY = canvasRect.rect.height - edgePadding;
            }
            else
            {
                maxX = Screen.width - edgePadding;
                maxY = Screen.height - edgePadding;
            }

            return new Vector2(
                Mathf.Clamp(pos.x, edgePadding, maxX),
                Mathf.Clamp(pos.y, edgePadding, maxY)
            );
        }

        // =====================================================================
        // 공개 메서드
        // =====================================================================

        /// <summary>조준점 표시/숨김 토글</summary>
        public void ToggleReticle()
        {
            _isVisible = !_isVisible;
        }

        /// <summary>락온 가능 거리 설정</summary>
        public void SetLockOnRange(float range)
        {
            lockOnRange = range;
        }
    }
}
