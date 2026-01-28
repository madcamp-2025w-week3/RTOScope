/*
 * MenuAircraftAnimator.cs - 메뉴 전투기 UI 애니메이션
 *
 * [역할]
 * - 시작 메뉴에서 전투기 UI Image에 생동감 있는 모션 추가
 * - 부드러운 상하 움직임 (bobbing)
 * - 살짝 기울어지는 회전
 * - RectTransform 기반 (UI Image 지원)
 */

using UnityEngine;

namespace RTOScope.Runtime.UI
{
    [RequireComponent(typeof(RectTransform))]
    public class MenuAircraftAnimator : MonoBehaviour
    {
        [Header("상하 움직임 (Bobbing)")]
        [Tooltip("상하로 움직이는 높이 (픽셀)")]
        [SerializeField] private float _bobHeight = 15f;
        
        [Tooltip("상하 움직임 속도")]
        [SerializeField] private float _bobSpeed = 1.5f;

        [Header("기울기 (Tilt)")]
        [Tooltip("좌우로 기울어지는 각도")]
        [SerializeField] private float _tiltAngle = 3f;
        
        [Tooltip("기울기 속도")]
        [SerializeField] private float _tiltSpeed = 0.8f;

        [Header("좌우 흔들림 (Sway)")]
        [Tooltip("좌우로 흔들리는 거리 (픽셀)")]
        [SerializeField] private float _swayAmount = 8f;
        
        [Tooltip("좌우 흔들림 속도")]
        [SerializeField] private float _swaySpeed = 1.2f;

        [Header("크기 변화 (Pulse)")]
        [Tooltip("크기 변화량 (0.02 = 2%)")]
        [SerializeField] private float _pulseAmount = 0.02f;
        
        [Tooltip("크기 변화 속도")]
        [SerializeField] private float _pulseSpeed = 2f;

        [Header("타이밍 오프셋")]
        [Tooltip("각 전투기마다 다른 값을 설정하면 서로 다르게 움직임 (0~10)")]
        [SerializeField] private float _timeOffset = 0f;
        
        [Tooltip("시작 시 랜덤 오프셋 자동 적용")]
        [SerializeField] private bool _randomOffset = true;

        // RectTransform 참조
        private RectTransform _rectTransform;
        
        // 초기값 저장
        private Vector2 _initialPosition;
        private Quaternion _initialRotation;
        private Vector3 _initialScale;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
        }

        private void Start()
        {
            _initialPosition = _rectTransform.anchoredPosition;
            _initialRotation = _rectTransform.localRotation;
            _initialScale = _rectTransform.localScale;

            // 랜덤 오프셋 자동 적용
            if (_randomOffset)
            {
                _timeOffset = Random.Range(0f, 10f);
            }
        }

        private void Update()
        {
            // 오프셋이 적용된 시간
            float time = Time.time + _timeOffset;

            // ===== 위치 애니메이션 =====
            
            // 상하 움직임 (Bobbing)
            float bobOffset = Mathf.Sin(time * _bobSpeed) * _bobHeight;

            // 좌우 흔들림 (Sway)
            float swayOffset = Mathf.Sin(time * _swaySpeed + 0.5f) * _swayAmount;

            // 위치 적용
            Vector2 newPosition = _initialPosition;
            newPosition.y += bobOffset;
            newPosition.x += swayOffset;
            _rectTransform.anchoredPosition = newPosition;

            // ===== 회전 애니메이션 =====
            
            // 기울기 (Roll)
            float rollAngle = Mathf.Sin(time * _tiltSpeed) * _tiltAngle;
            
            _rectTransform.localRotation = _initialRotation * Quaternion.Euler(0, 0, rollAngle);

            // ===== 크기 애니메이션 =====
            
            // 미세한 크기 변화 (숨쉬는 듯한 효과)
            float pulseScale = 1f + Mathf.Sin(time * _pulseSpeed) * _pulseAmount;
            _rectTransform.localScale = _initialScale * pulseScale;
        }

        /// <summary>
        /// 에디터에서 리셋
        /// </summary>
        public void ResetToInitial()
        {
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _initialPosition;
                _rectTransform.localRotation = _initialRotation;
                _rectTransform.localScale = _initialScale;
            }
        }
    }
}
