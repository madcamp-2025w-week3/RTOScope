using UnityEngine;

namespace RTOScope.Runtime.Aircraft
{
    /// <summary>
    /// 콕핏 카메라 팔로우 스크립트
    /// - 조종사의 시선 처리(Pilot Look) 및 G-Force 효과 구현
    /// - 기체 기동(회전)에 따라 카메라가 자연스럽게 움직이도록 함
    /// - 롤/요 기동 시 시선을 회전 방향으로 돌리고, 피치 기동 시 고개를 젖히거나 숙임
    /// </summary>
    public class CockpitCameraFollow : MonoBehaviour
    {
        [Header("Target")]
        public Transform cockpit;

        [Header("Pilot Look")]
        [Tooltip("항공기 Rigidbody (기체 움직임을 읽기 위함)")]
        [SerializeField] private Rigidbody aircraftRigidbody;
        [Tooltip("좌/우(요) 시선 변경 최대각 (deg)")]
        [SerializeField] private float maxYawOffset = 10f;
        [Tooltip("상/하(피치) 시선 변경 최대각 (deg)")]
        [SerializeField] private float maxPitchOffset = 6f;
        [Tooltip("요 회전 속도 -> 시선 변경 변환 비율")]
        [SerializeField] private float yawRateToOffset = 0.15f;
        [Tooltip("롤 회전 속도 -> 시선 변경 변환 비율")]
        [SerializeField] private float rollRateToOffset = 0.08f;
        [Tooltip("피치 회전 속도 -> 시선 변경 변환 비율")]
        [SerializeField] private float pitchRateToOffset = 0.08f;
        [Tooltip("시선 반응 속도 (값이 클수록 빠름)")]
        [SerializeField] private float lookResponse = 6f;
        [Tooltip("시선 변경이 작동하기 시작하는 회전 속도 임계값 (deg/s)")]
        [SerializeField] private float lookRateThreshold = 5f;
        [Tooltip("롤 회전 시 시선 변경 사용 여부")]
        [SerializeField] private bool useRollRate = true;
        [Tooltip("요 회전 시 시선 변경 사용 여부")]
        [SerializeField] private bool useYawRate = true;
        [Tooltip("피치 회전 시 시선 변경 사용 여부")]
        [SerializeField] private bool usePitchRate = true;
        [Tooltip("좌/우 시선 방향 반전")]
        [SerializeField] private bool invertLook = true;
        [Tooltip("상/하 시선 방향 반전")]
        [SerializeField] private bool invertPitchLook = true;

        // 초기 오프셋 (조종석 내부 위치)
        private Vector3 initialLocalPosition;
        private Quaternion initialLocalRotation;
        private float currentYawOffset;
        private float currentPitchOffset;

        void Start()
        {
            if (cockpit == null)
            {
                Debug.LogError("CockpitCameraFollow: cockpit is null");
                enabled = false;
                return;
            }

            if (aircraftRigidbody == null)
            {
                aircraftRigidbody = cockpit.GetComponentInParent<Rigidbody>();
            }

            // 조종석 기준 초기 위치/회전 저장
            initialLocalPosition = transform.localPosition;
            initialLocalRotation = transform.localRotation;
            currentYawOffset = 0f;
            currentPitchOffset = 0f;
        }

        void LateUpdate()
        {
            // 기본 위치는 콕핏에 고정
            transform.position = cockpit.TransformPoint(initialLocalPosition);

            // 회전 계산 (기본 회전 + 기동에 따른 시선 처리)
            Quaternion baseRotation = cockpit.rotation * initialLocalRotation;
            float targetYaw = GetTargetYawOffset();
            float targetPitch = GetTargetPitchOffset();
            float t = 1f - Mathf.Exp(-lookResponse * Time.deltaTime);
            currentYawOffset = Mathf.Lerp(currentYawOffset, targetYaw, t);
            currentPitchOffset = Mathf.Lerp(currentPitchOffset, targetPitch, t);
            transform.rotation = baseRotation * Quaternion.Euler(currentPitchOffset, currentYawOffset, 0f);
        }

        private float GetTargetYawOffset()
        {
            if (aircraftRigidbody == null) return 0f;

            Vector3 localAngular = cockpit.InverseTransformDirection(aircraftRigidbody.angularVelocity);
            float yawRateDeg = localAngular.y * Mathf.Rad2Deg;
            float rollRateDeg = localAngular.z * Mathf.Rad2Deg;

            bool yawing = useYawRate && Mathf.Abs(yawRateDeg) > lookRateThreshold;
            bool rolling = useRollRate && Mathf.Abs(rollRateDeg) > lookRateThreshold;

            float target = 0f;
            if (yawing) target += yawRateDeg * yawRateToOffset;
            if (rolling) target += rollRateDeg * rollRateToOffset;

            if (!yawing && !rolling) target = 0f;

            if (invertLook) target = -target;

            return Mathf.Clamp(target, -maxYawOffset, maxYawOffset);
        }

        private float GetTargetPitchOffset()
        {
            if (aircraftRigidbody == null) return 0f;

            Vector3 localAngular = cockpit.InverseTransformDirection(aircraftRigidbody.angularVelocity);
            float pitchRateDeg = localAngular.x * Mathf.Rad2Deg;

            bool pitchingUp = usePitchRate && pitchRateDeg > lookRateThreshold;

            float target = 0f;
            if (pitchingUp) target = pitchRateDeg * pitchRateToOffset;

            if (invertPitchLook) target = -target;

            return Mathf.Clamp(target, -maxPitchOffset, maxPitchOffset);
        }
    }
}
