/*
 * CountermeasureActuator.cs - 대응책 실행기 (HAL)
 *
 * [역할]
 * - RTOS에서 요청한 플레어/채프 발사 실행
 * - 파티클/이펙트 생성
 * - Unity 물리와 통합
 *
 * [위치] Runtime Layer > Hardware (Unity MonoBehaviour)
 */

using UnityEngine;
using RTOScope.Runtime.Aircraft;

namespace RTOScope.Runtime.Hardware
{
    /// <summary>
    /// 플레어/채프 발사 실행기
    /// RTOS CountermeasureControlTask의 명령을 Unity에서 실행
    /// </summary>
    public class CountermeasureActuator : MonoBehaviour
    {
        // =====================================================================
        // Inspector 설정
        // =====================================================================

        [Header("References")]
        [Tooltip("AircraftState 참조")]
        [SerializeField] private AircraftState _state;

        [Header("Flare Settings")]
        [Tooltip("플레어 프리팹 (파티클 또는 오브젝트)")]
        [SerializeField] private GameObject _flarePrefab;

        [Tooltip("플레어 발사 위치 (항공기 후방)")]
        [SerializeField] private Transform _flareSpawnPoint;

        [Tooltip("플레어 발사 속도")]
        [SerializeField] private float _flareEjectSpeed = 30f;

        [Tooltip("플레어 수명 (초)")]
        [SerializeField] private float _flareLifetime = 5f;

        [Header("Chaff Settings")]
        [Tooltip("채프 프리팹 (파티클 또는 오브젝트)")]
        [SerializeField] private GameObject _chaffPrefab;

        [Tooltip("채프 발사 위치")]
        [SerializeField] private Transform _chaffSpawnPoint;

        [Tooltip("채프 발사 속도")]
        [SerializeField] private float _chaffEjectSpeed = 20f;

        [Tooltip("채프 수명 (초)")]
        [SerializeField] private float _chaffLifetime = 8f;

        [Header("Audio")]
        [Tooltip("플레어 발사 사운드")]
        [SerializeField] private AudioClip _flareSound;

        [Tooltip("채프 발사 사운드")]
        [SerializeField] private AudioClip _chaffSound;

        [SerializeField] private AudioSource _audioSource;

        [Header("Debug")]
        [SerializeField] private bool _logDeployments = true;

        // =====================================================================
        // Unity 생명주기
        // =====================================================================

        private void Start()
        {
            if (_audioSource == null)
                _audioSource = GetComponent<AudioSource>();

            // 발사 위치 기본값 설정
            if (_flareSpawnPoint == null)
                _flareSpawnPoint = transform;
            if (_chaffSpawnPoint == null)
                _chaffSpawnPoint = transform;
        }

        private void Update()
        {
            if (_state == null) return;

            ProcessFlareRequest();
            ProcessChaffRequest();
        }

        // =====================================================================
        // 플레어 처리
        // =====================================================================

        private void ProcessFlareRequest()
        {
            if (_state.FlareFireRequest)
            {
                DeployFlare();
                _state.FlareFireRequest = false; // 요청 처리 완료
            }
        }

        private void DeployFlare()
        {
            if (_flarePrefab != null)
            {
                // 플레어 생성
                GameObject flare = Instantiate(
                    _flarePrefab,
                    _flareSpawnPoint.position,
                    _flareSpawnPoint.rotation
                );

                // 후방으로 발사
                Rigidbody rb = flare.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // 항공기 속도 + 후방 발사
                    Vector3 ejectDir = -transform.forward + Vector3.down * 0.3f;
                    rb.velocity = ejectDir.normalized * _flareEjectSpeed;

                    // 항공기 현재 속도 상속 (선택적)
                    rb.velocity += _state.VelocityVector * 0.5f;
                }

                // 자동 삭제
                Destroy(flare, _flareLifetime);

                if (_logDeployments)
                    Debug.Log($"[CountermeasureActuator] 플레어 발사! 남은: {_state.FlareCount}");
            }
            else
            {
                // 프리팹 없으면 파티클만 재생 (임시)
                if (_logDeployments)
                    Debug.Log($"[CountermeasureActuator] 플레어 발사 (프리팹 없음)! 남은: {_state.FlareCount}");
            }

            // 사운드 재생
            if (_flareSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_flareSound);
            }
        }

        // =====================================================================
        // 채프 처리
        // =====================================================================

        private void ProcessChaffRequest()
        {
            if (_state.ChaffFireRequest)
            {
                DeployChaff();
                _state.ChaffFireRequest = false; // 요청 처리 완료
            }
        }

        private void DeployChaff()
        {
            if (_chaffPrefab != null)
            {
                // 채프 생성
                GameObject chaff = Instantiate(
                    _chaffPrefab,
                    _chaffSpawnPoint.position,
                    _chaffSpawnPoint.rotation
                );

                // 후방으로 발사
                Rigidbody rb = chaff.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    Vector3 ejectDir = -transform.forward + Vector3.down * 0.2f;
                    rb.velocity = ejectDir.normalized * _chaffEjectSpeed;
                    rb.velocity += _state.VelocityVector * 0.3f;
                }

                // 자동 삭제
                Destroy(chaff, _chaffLifetime);

                if (_logDeployments)
                    Debug.Log($"[CountermeasureActuator] 채프 발사! 남은: {_state.ChaffCount}");
            }
            else
            {
                if (_logDeployments)
                    Debug.Log($"[CountermeasureActuator] 채프 발사 (프리팹 없음)! 남은: {_state.ChaffCount}");
            }

            // 사운드 재생
            if (_chaffSound != null && _audioSource != null)
            {
                _audioSource.PlayOneShot(_chaffSound);
            }
        }

        // =====================================================================
        // 공개 메서드
        // =====================================================================

        /// <summary>AircraftState 주입</summary>
        public void SetState(AircraftState state)
        {
            _state = state;
        }
    }
}
