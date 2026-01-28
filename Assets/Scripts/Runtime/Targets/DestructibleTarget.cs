/*
 * DestructibleTarget.cs - 파괴 가능한 과녁
 *
 * [역할]
 * - 미사일과 충돌 시 파괴
 * - 점수 추가 (ScoreManager 통해)
 * - 시각적 효과 (폭발)
 */

using System.Collections.Generic;
using RTOScope.Runtime.Game;
using UnityEngine;

namespace RTOScope.Runtime.Targets
{
    public class DestructibleTarget : MonoBehaviour
    {
        [Header("과녁 설정")]
        [Tooltip("폭발 이펙트 프리팹 (선택)")]
        [SerializeField] private GameObject _explosionEffectPrefab;

        [Tooltip("파괴 시 사운드 (선택)")]
        [SerializeField] private AudioClip _destructionSound;

        [Tooltip("점수")]
        [SerializeField] private int _pointValue = 50;

        [Header("디버그")]
        [SerializeField] private bool _logCollisions = true;

        private bool _isDestroyed = false;
        private readonly HashSet<int> _handledMissiles = new HashSet<int>();

        private void Start()
        {
            // Collider가 Trigger인지 확인
            var col = GetComponent<Collider>();
            if (col != null && !col.isTrigger)
            {
                Debug.LogWarning($"[DestructibleTarget] {gameObject.name}: Collider가 Trigger로 설정되지 않았습니다. OnTriggerEnter가 작동하지 않을 수 있습니다.");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isDestroyed) return;

            // 미사일인지 확인 (HomingMissile 또는 "Missile" 태그)
            if (IsMissile(other.gameObject))
            {
                HandleDestruction(other.gameObject);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_isDestroyed) return;

            // 미사일인지 확인
            if (IsMissile(collision.gameObject))
            {
                HandleDestruction(collision.gameObject);
            }
        }

        private bool IsMissile(GameObject obj)
        {
            // 방법 1: 태그 확인 (태그가 존재할 때만)
            try
            {
                if (obj.tag == "Missile")
                    return true;
            }
            catch { /* 태그 미정의 무시 */ }

            // 방법 2: homing_missile 컴포넌트 확인
            if (obj.GetComponent<HomingMissile.homing_missile>() != null)
                return true;

            // 방법 3: 부모에서 확인
            if (obj.GetComponentInParent<HomingMissile.homing_missile>() != null)
                return true;

            // 방법 4: 이름에 "Missile" 포함
            if (obj.name.ToLower().Contains("missile"))
                return true;

            return false;
        }

        private void HandleDestruction(GameObject missile)
        {
            bool tutorialMode = GameSettings.Instance != null && GameSettings.Instance.TutorialMode;
            if (tutorialMode)
            {
                int id = missile != null ? missile.GetInstanceID() : 0;
                if (id != 0 && !_handledMissiles.Add(id))
                    return;
            }
            else
            {
                _isDestroyed = true;
            }

            if (_logCollisions)
            {
                Debug.Log($"[DestructibleTarget] {gameObject.name} 파괴됨! 미사일: {missile.name}");
            }

            // 점수 추가
            if (ScoreManager.Instance != null)
            {
                ScoreManager.Instance.AddTargetScore();
            }
            else
            {
                Debug.LogWarning("[DestructibleTarget] ScoreManager가 없습니다!");
            }

            // 폭발 이펙트
            if (_explosionEffectPrefab != null)
            {
                Instantiate(_explosionEffectPrefab, transform.position, Quaternion.identity);
            }

            // 사운드
            if (_destructionSound != null)
            {
                AudioSource.PlayClipAtPoint(_destructionSound, transform.position);
            }

            // 부모 오브젝트 파괴 (ArcheryTarget 전체 삭제)
            if (!tutorialMode)
            {
                if (transform.parent != null)
                {
                    Destroy(transform.parent.gameObject);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }

        // 추가: 매 프레임 주변 미사일 확인 (Trigger 실패 시 백업)
        private void Update()
        {
            if (_isDestroyed) return;

            // 주변 5m 내 미사일 검색
            Collider[] nearbyObjects = Physics.OverlapSphere(transform.position, 5f);
            foreach (var col in nearbyObjects)
            {
                if (IsMissile(col.gameObject))
                {
                    HandleDestruction(col.gameObject);
                    return;
                }
            }
        }
    }
}
