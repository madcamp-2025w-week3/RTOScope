/*
 * HUDTapeController.cs - Moving tape controller for HUD
 *
 * [역할] 고정 인디케이터 옆에서 테이프(눈금 이미지)를 값에 따라 상하 이동
 * [위치] Runtime Layer > UI (Unity MonoBehaviour)
 *
 * 사용법:
 * 1) tapeRect에 움직일 테이프 이미지(RectTransform) 연결
 * 2) valueText에 고정 표시 텍스트(TMP) 연결
 * 3) (선택) aircraftTransform을 연결하면 altitude 기반으로 자동 업데이트
 */

using TMPro;
using UnityEngine;

namespace RTOScope.Runtime.UI
{
    /// <summary>
    /// Moving Tape + Fixed Indicator HUD 테이프 컨트롤러
    /// </summary>
    public class HUDTapeController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RectTransform tapeRect;
        [SerializeField] private TextMeshProUGUI valueText;

        [Header("Data Source (Optional)")]
        [SerializeField] private Transform aircraftTransform;
        [SerializeField] private bool useAltitudeFromTransform = true;
        [SerializeField] private bool convertMetersToFeet = true;

        [Header("Tape Settings")]
        [Tooltip("값 1 단위당 테이프가 이동할 픽셀 수")]
        [SerializeField] private float pixelsPerUnit = 2f;
        [SerializeField] private bool invertDirection = false;
        [SerializeField] private float smoothSpeed = 8f;
        [SerializeField] private bool useSmoothDamp = true;
        [SerializeField] private bool useInitialValueAsReference = true;
        [SerializeField] private float referenceValue = 0f;

        [Header("Runtime Value")]
        public float currentValue;

        private float _velocityY;
        private int _lastDisplayValue;
        private bool _referenceInitialized;

        private void Update()
        {
            if (useAltitudeFromTransform && aircraftTransform != null)
            {
                float altMeters = aircraftTransform.position.y;
                currentValue = convertMetersToFeet ? altMeters * 3.28084f : altMeters;
            }

            if (useInitialValueAsReference && !_referenceInitialized)
            {
                referenceValue = currentValue;
                _referenceInitialized = true;
            }

            UpdateTapePosition();
            UpdateValueText();
        }

        private void UpdateTapePosition()
        {
            if (tapeRect == null) return;

            float dir = invertDirection ? -1f : 1f;
            float targetY = (currentValue - referenceValue) * pixelsPerUnit * dir;

            Vector2 pos = tapeRect.anchoredPosition;

            if (useSmoothDamp)
            {
                // smoothSpeed가 높을수록 더 빠르게 따라감
                float smoothTime = smoothSpeed > 0f ? 1f / smoothSpeed : 0.1f;
                pos.y = Mathf.SmoothDamp(pos.y, targetY, ref _velocityY, smoothTime);
            }
            else
            {
                float t = 1f - Mathf.Exp(-smoothSpeed * Time.deltaTime);
                pos.y = Mathf.Lerp(pos.y, targetY, t);
            }

            tapeRect.anchoredPosition = pos;
        }

        private void UpdateValueText()
        {
            if (valueText == null) return;

            int displayValue = Mathf.RoundToInt(currentValue);
            if (displayValue == _lastDisplayValue) return;

            _lastDisplayValue = displayValue;
            valueText.text = displayValue.ToString();
        }
    }
}
