/*
 * HUDController.cs - HUD 텍스트 업데이트 컨트롤러
 *
 * [역할] HUD 배경 이미지 위에 텍스트를 갱신
 * [위치] Runtime Layer > UI (Unity MonoBehaviour)
 *
 * [설계 의도]
 * - Screen Space - Camera Canvas에서 동작
 * - 전투기 상태를 읽어 SPD/ALT/HDG/MSL 표시
 * - 누락된 참조는 안전하게 대체값 사용
 */

using TMPro;
using UnityEngine;
using RTOScope.Runtime.Hardware;
using RTOScope.Runtime.Bootstrap;
using RTOScope.Runtime.Aircraft;

namespace RTOScope.Runtime.UI
{
    /// <summary>
    /// HUD 숫자 텍스트 업데이트 컨트롤러
    /// </summary>
    public class HUDController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera cockpitCamera;
        [SerializeField] private Transform aircraftTransform;
        [SerializeField] private Rigidbody aircraftRigidbody;
        [SerializeField] private PlayerControllerX playerControllerX;
        [SerializeField] private FlightActuator flightActuator;
        [SerializeField] private RTOSRunner rtosRunner;

        [Header("HUD Anchor")]
        [SerializeField] private bool lockHUDToCockpit = true;
        [SerializeField] private Transform cockpitAnchor;

        [Header("HUD Text")]
        [SerializeField] private TMP_Text spdText;
        [SerializeField] private TMP_Text altText;
        [SerializeField] private TMP_Text hdgText;
        [SerializeField] private TMP_Text mslText;
        [SerializeField] private TMP_Text distText;

        [Header("Display Options")]
        [SerializeField] private bool padHeadingTo3Digits = true;

        [Tooltip("0이면 매 프레임 갱신, 0.05~0.1 권장")]
        [SerializeField] private float updateIntervalSeconds = 0f;

        [Header("Speed Fallback")]
        [Tooltip("Rigidbody.velocity.magnitude(m/s) -> knots 변환에 사용")]
        [SerializeField] private float metersPerSecondToKnots = 1.94384f;
        [Tooltip("표시용 속도 배수 (예: m/s -> knots = 1.94384, km/h -> knots = 0.539957)")]
        [SerializeField] private float speedDisplayMultiplier = 1.94384f;

        [Header("Weapons")]
        [SerializeField] public int missileCount = 2;

        [Header("Target Distance Display")]
        [Tooltip("타겟 미감지 시 표시할 텍스트")]
        [SerializeField] private string noTargetText = "----";
        [Tooltip("거리 단위 (m 또는 km)")]
        [SerializeField] private bool useKilometers = false;

        private float _nextUpdateTime;
        private bool _warnedMissingRefs;
        private Canvas _canvas;
        private Transform _canvasTransform;
        private Vector3 _initialLocalPosition;
        private Quaternion _initialLocalRotation;
        private bool _anchorInitialized;
        private AircraftState _aircraftState;

        private void Awake()
        {
            AutoAssignReferences();
            TryConfigureCanvas();
            WarnIfMissingReferences();
        }

        private void LateUpdate()
        {
            if (aircraftTransform == null)
            {
                // Aircraft destroyed after crash; stop updating to avoid MissingReference spam.
                enabled = false;
                return;
            }

            if (updateIntervalSeconds > 0f)
            {
                if (Time.time < _nextUpdateTime) return;
                _nextUpdateTime = Time.time + updateIntervalSeconds;
            }

            // 참조가 파괴됐을 수 있으니 필요 시 재할당
            if (aircraftTransform == null || aircraftRigidbody == null || playerControllerX == null || flightActuator == null)
            {
                AutoAssignReferences();
            }

            if (_canvas == null)
            {
                TryConfigureCanvas();
            }

            if (lockHUDToCockpit)
            {
                UpdateHUDAnchor();
            }

            UpdateHUDText();
        }

        private void AutoAssignReferences()
        {
            if (cockpitCamera == null)
            {
                var camGo = GameObject.Find("Cockpit Camera");
                if (camGo != null) cockpitCamera = camGo.GetComponent<Camera>();
            }

            if (aircraftTransform == null)
            {
                var aircraftGo = GameObject.Find("F-16");
                if (aircraftGo != null) aircraftTransform = aircraftGo.transform;
            }

            if (aircraftTransform != null)
            {
                if (playerControllerX == null)
                    playerControllerX = aircraftTransform.GetComponent<PlayerControllerX>();

                if (aircraftRigidbody == null)
                    aircraftRigidbody = aircraftTransform.GetComponent<Rigidbody>();

                if (flightActuator == null)
                    flightActuator = aircraftTransform.GetComponent<FlightActuator>();
            }

            if (playerControllerX == null)
                playerControllerX = FindObjectOfType<PlayerControllerX>();

            if (flightActuator == null)
                flightActuator = FindObjectOfType<FlightActuator>();

            if (aircraftRigidbody == null && aircraftTransform == null)
                aircraftRigidbody = FindObjectOfType<Rigidbody>();

            if (rtosRunner == null)
                rtosRunner = FindObjectOfType<RTOSRunner>();

            if (rtosRunner != null)
                _aircraftState = rtosRunner.State;
        }

        private void TryConfigureCanvas()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _canvas = canvas;
            _canvasTransform = canvas.transform;

            if (lockHUDToCockpit)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                if (cockpitCamera != null)
                    canvas.worldCamera = cockpitCamera;

                InitializeCockpitAnchor();
                return;
            }

            if (cockpitCamera == null) return;

            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cockpitCamera;
        }

        private void InitializeCockpitAnchor()
        {
            if (_anchorInitialized) return;
            if (_canvasTransform == null) return;

            if (cockpitAnchor == null)
            {
                if (cockpitCamera != null && cockpitCamera.transform.parent != null)
                    cockpitAnchor = cockpitCamera.transform.parent;
                else if (aircraftTransform != null)
                    cockpitAnchor = aircraftTransform;
            }

            if (cockpitAnchor == null) return;

            _initialLocalPosition = cockpitAnchor.InverseTransformPoint(_canvasTransform.position);
            _initialLocalRotation = Quaternion.Inverse(cockpitAnchor.rotation) * _canvasTransform.rotation;
            _anchorInitialized = true;
        }

        private void UpdateHUDAnchor()
        {
            if (!lockHUDToCockpit) return;

            if (_canvasTransform == null)
            {
                TryConfigureCanvas();
            }

            if (!_anchorInitialized)
            {
                InitializeCockpitAnchor();
            }

            if (!_anchorInitialized || cockpitAnchor == null) return;

            _canvasTransform.position = cockpitAnchor.TransformPoint(_initialLocalPosition);
            _canvasTransform.rotation = cockpitAnchor.rotation * _initialLocalRotation;
        }

        private void WarnIfMissingReferences()
        {
            if (_warnedMissingRefs) return;

            if (spdText == null || altText == null || hdgText == null || mslText == null)
            {
                Debug.LogWarning("[HUDController] TMP_Text 참조가 누락되었습니다. HUD 텍스트가 표시되지 않을 수 있습니다.");
            }

            if (aircraftTransform == null)
                Debug.LogWarning("[HUDController] aircraftTransform이 비어있습니다. F-16 자동 할당 실패.");

            if (playerControllerX == null && aircraftRigidbody == null)
                Debug.LogWarning("[HUDController] PlayerControllerX와 Rigidbody가 모두 비어있습니다. 속도 대체값이 0으로 표시됩니다.");

            if (!lockHUDToCockpit && cockpitCamera == null)
                Debug.LogWarning("[HUDController] cockpitCamera가 비어있습니다. Canvas Render Camera를 수동으로 지정하세요.");

            if (lockHUDToCockpit && cockpitAnchor == null)
                Debug.LogWarning("[HUDController] cockpitAnchor is null. HUD anchor may drift.");

            _warnedMissingRefs = true;
        }

        private void UpdateHUDText()
        {
            if (aircraftTransform == null)
            {
                enabled = false;
                return;
            }

            try
            {
                float speedValue = GetSpeedValue() * speedDisplayMultiplier;
                int speedInt = Mathf.RoundToInt(speedValue);

                float altFeet = aircraftTransform.position.y * 3.28084f;
                int altInt = Mathf.RoundToInt(altFeet);

                int headingInt = NormalizeHeadingToInt(aircraftTransform.eulerAngles.y);

                if (spdText != null)
                {
                    spdText.text = speedInt.ToString();
                }

                if (altText != null)
                {
                    altText.text = altInt.ToString();
                }

                if (hdgText != null)
                {
                    string headingText = padHeadingTo3Digits ? headingInt.ToString("000") : headingInt.ToString();
                    hdgText.text = headingText;
                }

                if (mslText != null)
                {
                    // AircraftState에서 미사일 개수 가져오기
                    int count = 0;
                    if (_aircraftState != null)
                        count = _aircraftState.MissileCount;
                    else
                        count = Mathf.Max(0, missileCount);
                    mslText.text = count.ToString();
                }

                UpdateTargetDistance();
            }
            catch (MissingReferenceException)
            {
                AutoAssignReferences();
            }
        }

        /// <summary>타겟 거리 업데이트</summary>
        private void UpdateTargetDistance()
        {
            if (distText == null) return;

            // RTOSRunner에서 AircraftState 가져오기
            if (_aircraftState == null && rtosRunner != null)
            {
                _aircraftState = rtosRunner.State;
            }

            if (_aircraftState == null)
            {
                distText.text = noTargetText;
                return;
            }

            // 타겟 후보가 있으면 거리 표시
            if (_aircraftState.TargetCandidateAvailable && _aircraftState.TargetCandidateDistance > 0f)
            {
                float distance = _aircraftState.TargetCandidateDistance;

                if (useKilometers && distance >= 1000f)
                {
                    // km 단위로 표시 (소수점 1자리)
                    distText.text = $"{distance / 1000f:F1}";
                }
                else
                {
                    // m 단위로 표시 (정수)
                    distText.text = $"{Mathf.RoundToInt(distance)}";
                }
            }
            else
            {
                distText.text = noTargetText;
            }
        }

        private float GetSpeedValue()
        {
            if (playerControllerX != null)
            {
                float speed = playerControllerX.currentSpeed;
                if (speed > 0.01f) return speed;
            }

            if (flightActuator != null)
            {
                float speed = flightActuator.CurrentSpeed;
                if (speed > 0.01f) return speed;
            }

            if (aircraftRigidbody != null)
            {
                return aircraftRigidbody.velocity.magnitude * metersPerSecondToKnots;
            }

            return 0f;
        }

        private int NormalizeHeadingToInt(float headingDegrees)
        {
            float h = headingDegrees % 360f;
            if (h < 0f) h += 360f;
            return Mathf.RoundToInt(h);
        }

        /// <summary>미사일 수 감소 (추후 발사 이벤트에 연결)</summary>
        public void ConsumeMissile(int amount = 1)
        {
            missileCount = Mathf.Max(0, missileCount - Mathf.Max(0, amount));
        }
    }
}
