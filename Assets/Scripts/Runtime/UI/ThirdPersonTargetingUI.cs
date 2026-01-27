/*
 * ThirdPersonTargetingUI.cs - 3인칭 시점 타게팅 UI 컨트롤러
 *
 * [역할]
 * - 3인칭 시점에서 화면 중앙에 십자선(+) 표시
 * - 타겟 감지 시 조준원을 타겟 방향으로 이동
 * - 1인칭과 동일한 색상 변경 (녹색→노란색→빨간색)
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
    /// 3인칭 시점용 타게팅 UI 컨트롤러
    /// Screen Space - Overlay Canvas에서 작동
    /// </summary>
    public class ThirdPersonTargetingUI : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("UI References")]
        [Tooltip("중앙 십자선 이미지 (항상 화면 중앙에 표시)")]
        [SerializeField] private Image crosshairImage;

        [Tooltip("타겟 추적 조준원 이미지")]
        [SerializeField] private Image targetingCircleImage;

        [Tooltip("조준원 RectTransform")]
        [SerializeField] private RectTransform targetingCircleRect;

        [Tooltip("Canvas RectTransform")]
        [SerializeField] private RectTransform canvasRect;

        [Header("Camera")]
        [Tooltip("현재 활성 카메라 (자동 감지 가능)")]
        [SerializeField] private Camera activeCamera;

        [Tooltip("자동으로 Main Camera 사용")]
        [SerializeField] private bool autoDetectCamera = true;

        [Header("RTOS")]
        [Tooltip("RTOS Runner")]
        [SerializeField] private RTOSRunner rtosRunner;

        [Header("Lock-on Settings")]
        [Tooltip("락온 가능 거리 (m)")]
        [SerializeField] private float lockOnRange = 1500f;

        [Tooltip("락온 가능 FOV (도, 절반값)")]
        [SerializeField] private float lockOnFovHalf = 20f;

        [Header("Colors")]
        [Tooltip("기본 색상 (타겟 없음/감지만)")]
        [SerializeField] private Color normalColor = new Color(0f, 1f, 0f, 0.9f); // 녹색

        [Tooltip("락온 가능 색상 (사거리 내)")]
        [SerializeField] private Color lockableColor = new Color(1f, 1f, 0f, 0.9f); // 노란색

        [Tooltip("락온 완료 색상")]
        [SerializeField] private Color lockedColor = new Color(1f, 0f, 0f, 0.9f); // 빨간색

        [Header("Movement")]
        [Tooltip("조준원 이동 부드러움")]
        [SerializeField] private float smoothSpeed = 15f;

        [Tooltip("화면 가장자리 패딩")]
        [SerializeField] private float edgePadding = 50f;

        // =====================================================================
        // 내부 상태
        // =====================================================================

        private AircraftState _state;
        private Vector2 _currentReticlePos;
        private Vector2 _centerPos;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Awake()
        {
            AutoAssignReferences();
        }

        private void Start()
        {
            // 화면 중앙 위치 계산
            _centerPos = Vector2.zero; // anchoredPosition 기준

            // 십자선은 항상 중앙에
            if (crosshairImage != null)
            {
                var crosshairRect = crosshairImage.GetComponent<RectTransform>();
                if (crosshairRect != null)
                {
                    crosshairRect.anchoredPosition = Vector2.zero;
                }
            }

            _currentReticlePos = Vector2.zero;
        }

        private void LateUpdate()
        {
            UpdateCamera();
            UpdateState();
            UpdateTargetingCircle();
            UpdateColors();
        }

        // =====================================================================
        // 초기화
        // =====================================================================

        private void AutoAssignReferences()
        {
            if (rtosRunner == null)
                rtosRunner = FindObjectOfType<RTOSRunner>();

            if (canvasRect == null)
            {
                var canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                    canvasRect = canvas.GetComponent<RectTransform>();
            }

            if (targetingCircleRect == null && targetingCircleImage != null)
                targetingCircleRect = targetingCircleImage.GetComponent<RectTransform>();
        }

        private void UpdateCamera()
        {
            if (autoDetectCamera)
            {
                activeCamera = Camera.main;
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
        // 조준원 위치 업데이트
        // =====================================================================

        private void UpdateTargetingCircle()
        {
            if (targetingCircleRect == null || activeCamera == null || _state == null)
                return;

            bool hasTarget = _state.TargetCandidateAvailable || _state.LockedTargetValid;

            if (!hasTarget)
            {
                // 타겟 없으면 중앙으로 부드럽게 이동
                _currentReticlePos = Vector2.Lerp(_currentReticlePos, _centerPos, Time.deltaTime * smoothSpeed);
            }
            else
            {
                // 타겟 위치 가져오기 (락온된 타겟 우선)
                Vector3 targetWorldPos = _state.LockedTargetValid
                    ? _state.LockedTargetPosition
                    : _state.TargetCandidatePosition;

                // 월드 좌표 → 스크린 좌표 변환
                Vector3 screenPos = activeCamera.WorldToScreenPoint(targetWorldPos);

                // 타겟이 카메라 뒤에 있는지 확인
                bool isBehindCamera = screenPos.z < 0;

                if (isBehindCamera)
                {
                    // 카메라 뒤면 중앙으로
                    _currentReticlePos = Vector2.Lerp(_currentReticlePos, _centerPos, Time.deltaTime * smoothSpeed);
                }
                else
                {
                    // Canvas 로컬 좌표로 변환
                    Vector2 targetCanvasPos;
                    if (canvasRect != null)
                    {
                        Canvas canvas = canvasRect.GetComponent<Canvas>();
                        Camera canvasCamera = null;
                        if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        {
                            canvasCamera = canvas.worldCamera;
                        }

                        RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            canvasRect,
                            new Vector2(screenPos.x, screenPos.y),
                            canvasCamera,
                            out targetCanvasPos
                        );
                    }
                    else
                    {
                        targetCanvasPos = new Vector2(
                            screenPos.x - Screen.width / 2f,
                            screenPos.y - Screen.height / 2f
                        );
                    }

                    // 화면 밖이면 가장자리에 클램프
                    targetCanvasPos = ClampToScreen(targetCanvasPos);

                    // 부드러운 이동
                    _currentReticlePos = Vector2.Lerp(_currentReticlePos, targetCanvasPos, Time.deltaTime * smoothSpeed);
                }
            }

            // 위치 적용
            targetingCircleRect.anchoredPosition = _currentReticlePos;
        }

        // =====================================================================
        // 색상 업데이트
        // =====================================================================

        private void UpdateColors()
        {
            if (_state == null) return;

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
                    targetColor = lockableColor;
                }
            }

            // 색상 적용 (부드럽게)
            if (crosshairImage != null)
                crosshairImage.color = Color.Lerp(crosshairImage.color, targetColor, Time.deltaTime * 10f);

            if (targetingCircleImage != null)
                targetingCircleImage.color = Color.Lerp(targetingCircleImage.color, targetColor, Time.deltaTime * 10f);
        }

        // =====================================================================
        // 유틸리티
        // =====================================================================

        private Vector2 ClampToScreen(Vector2 pos)
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
    }
}
