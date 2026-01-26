using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RTOScope.Runtime.UI
{
    /// <summary>
    /// Infinite scrolling HUD tape using RawImage UV offset.
    /// Texture Wrap Mode must be set to Repeat for tiling to work.
    /// </summary>
    public class InfiniteHUDTape : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RawImage tapeRawImage;
        [SerializeField] private TextMeshProUGUI indicatorText;

        [Header("Data")]
        public float currentValue;

        [Header("Data Source (Optional)")]
        [SerializeField] private Transform aircraftTransform;
        [SerializeField] private bool useAltitudeFromTransform = true;
        [SerializeField] private bool convertMetersToFeet = true;

        [Header("Scrolling")]
        [Tooltip("UV shift per value unit (e.g., 0.001 = 1000 units per UV).")]
        [SerializeField] private float valueScale = 0.001f;
        [Tooltip("Higher = faster smoothing.")]
        [SerializeField] private float lerpSpeed = 8f;
        [SerializeField] private bool invertDirection = false;

        private float _currentUVY;

        private void Update()
        {
            if (tapeRawImage == null) return;

            if (useAltitudeFromTransform && aircraftTransform != null)
            {
                float altMeters = aircraftTransform.position.y;
                currentValue = convertMetersToFeet ? altMeters * 3.28084f : altMeters;
            }

            float dir = invertDirection ? -1f : 1f;
            float targetUVY = currentValue * valueScale * dir;

            _currentUVY = Mathf.Lerp(_currentUVY, targetUVY, 1f - Mathf.Exp(-lerpSpeed * Time.deltaTime));

            Rect uv = tapeRawImage.uvRect;
            uv.y = _currentUVY;
            tapeRawImage.uvRect = uv;

            if (indicatorText != null)
            {
                indicatorText.text = Mathf.RoundToInt(currentValue).ToString();
            }
        }
    }
}
