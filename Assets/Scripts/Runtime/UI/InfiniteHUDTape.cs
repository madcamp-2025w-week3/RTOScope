using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RTOScope.Runtime.Hardware;

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

        public enum DataSourceMode
        {
            Manual,
            Altitude,
            Speed
        }

        [Header("Data")]
        public float currentValue;
        [SerializeField] private DataSourceMode dataSource = DataSourceMode.Altitude;

        [Header("Altitude Source")]
        [SerializeField] private Transform aircraftTransform;
        [SerializeField] private bool convertMetersToFeet = true;

        [Header("Speed Source")]
        [SerializeField] private Rigidbody aircraftRigidbody;
        [SerializeField] private PlayerControllerX playerControllerX;
        [SerializeField] private FlightActuator flightActuator;
        [Tooltip("Speed unit conversion (e.g., 3.6 for m/s->km/h, 1.94384 for m/s->kts)")]
        [SerializeField] private float speedMultiplier = 1f;

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

            if (dataSource == DataSourceMode.Altitude && aircraftTransform != null)
            {
                float altMeters = aircraftTransform.position.y;
                currentValue = convertMetersToFeet ? altMeters * 3.28084f : altMeters;
            }
            else if (dataSource == DataSourceMode.Speed)
            {
                float speed = 0f;
                if (playerControllerX != null && playerControllerX.currentSpeed > 0.01f)
                {
                    speed = playerControllerX.currentSpeed;
                }
                else if (flightActuator != null && flightActuator.CurrentSpeed > 0.01f)
                {
                    speed = flightActuator.CurrentSpeed;
                }
                else if (aircraftRigidbody != null)
                {
                    speed = aircraftRigidbody.velocity.magnitude;
                }

                currentValue = speed * speedMultiplier;
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
