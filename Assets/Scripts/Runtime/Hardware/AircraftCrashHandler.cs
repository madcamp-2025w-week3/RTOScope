using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.SceneManagement;
using RTOScope.Runtime.Aircraft;

namespace RTOScope.Runtime.Hardware
{
    /// <summary>
    /// Detects ground impact and turns the aircraft into a break-apart crash.
    /// </summary>
    public class AircraftCrashHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Rigidbody aircraftRigidbody;
        [SerializeField] private Transform partsRoot;

        [Header("Crash Detection")]
        [SerializeField] private LayerMask groundLayers;
        [SerializeField] private float crashSpeedThreshold = 40f;

        [Header("Explosion/Fire")]
        [SerializeField] private GameObject explosionPrefab;
        [SerializeField] private GameObject firePrefab;
        [SerializeField] private float explosionForce = 1500f;
        [SerializeField] private float explosionRadius = 12f;
        [SerializeField] private float explosionLifetime = 2f;
        [SerializeField] private float debrisLifetime = 12f;

        [Header("Camera")]
        [SerializeField] private CameraSwitchController cameraSwitchController;
        [SerializeField] private bool forceExternalCameraOnCrash = true;
        [SerializeField] private Camera mainCamera;
        [SerializeField] private Camera cockpitCamera;

        [Header("Game Over")]
        [SerializeField] private float gameOverDelay = 5f;
        [SerializeField] private GameObject gameOverRoot;
        [SerializeField] private TMP_Text deadText;
        [SerializeField] private TMP_Text gameOverText;
        [SerializeField] private string deadMessage = "DEAD";
        [SerializeField] private string gameOverMessage = "GAME OVER";
        [SerializeField] private bool returnToMenuAfterGameOver = true;
        [SerializeField] private float returnToMenuDelay = 3f;
        [SerializeField] private string menuSceneName = "StartMenu";

        [Header("Disable On Crash")]
        [SerializeField] private bool disableAircraftPhysics = true;
        [SerializeField] private Behaviour[] disableBehaviours;

        [Header("Audio")]
        [SerializeField] private AudioSource engineAudioSource;
        [SerializeField] private AudioClip engineLoopClip;
        [SerializeField] private AudioSource explosionAudioSource;
        [SerializeField] private AudioClip explosionClip;

        [Header("Parts")]
        [SerializeField] private bool autoCollectParts = true;
        [SerializeField] private List<Transform> parts = new List<Transform>();

        [Header("Crash Optimization")]
        [Tooltip("충돌 전에 파츠 목록을 미리 캐시")]
        [SerializeField] private bool precacheParts = true;
        [Tooltip("최대 분리 파츠 수 (0이면 제한 없음)")]
        [SerializeField] private int maxPartsToBreak = 24;
        [Tooltip("MeshCollider 대신 BoxCollider로 단순화 (런타임 Convex Cook 방지)")]
        [SerializeField] private bool useSimpleColliders = true;
        [Tooltip("BoxCollider 강제 크기(0이면 Renderer bounds로 자동 계산)")]
        [SerializeField] private Vector3 boxColliderSizeOverride = Vector3.zero;
        [Tooltip("파츠 분리를 코루틴으로 분산 처리")]
        [SerializeField] private bool breakApartAsync = true;
        [Tooltip("코루틴 1프레임당 처리 파츠 수")]
        [SerializeField] private int partsPerFrame = 8;

        private bool _crashed;

        private void Awake()
        {
            if (aircraftRigidbody == null)
                aircraftRigidbody = GetComponent<Rigidbody>();
            if (partsRoot == null)
                partsRoot = transform;
            if (cameraSwitchController == null)
                cameraSwitchController = FindObjectOfType<CameraSwitchController>();
            if (mainCamera == null)
            {
                var mainGo = GameObject.Find("Main Camera");
                if (mainGo != null) mainCamera = mainGo.GetComponent<Camera>();
            }
            if (cockpitCamera == null)
            {
                var cockpitGo = GameObject.Find("Cockpit Camera");
                if (cockpitGo != null) cockpitCamera = cockpitGo.GetComponent<Camera>();
            }

            if (autoCollectParts && precacheParts)
            {
                CollectParts();
            }
        }

        private void Start()
        {
            if (engineAudioSource != null && engineLoopClip != null)
            {
                engineAudioSource.clip = engineLoopClip;
                engineAudioSource.loop = true;
                if (!engineAudioSource.isPlaying)
                    engineAudioSource.Play();
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_crashed) return;
            if (!IsGround(collision.gameObject)) return;

            float speed = aircraftRigidbody != null
                ? aircraftRigidbody.velocity.magnitude
                : collision.relativeVelocity.magnitude;

            if (speed < crashSpeedThreshold) return;

            Crash(collision);
        }

        private bool IsGround(GameObject other)
        {
            return (groundLayers.value & (1 << other.layer)) != 0;
        }

        private void Crash(Collision collision)
        {
            _crashed = true;

            Vector3 hitPoint = collision.GetContact(0).point;

            if (explosionPrefab != null)
            {
                var explosion = Instantiate(explosionPrefab, hitPoint, Quaternion.identity);
                if (explosionLifetime > 0f)
                    Destroy(explosion, explosionLifetime);
            }
            if (firePrefab != null)
                Instantiate(firePrefab, hitPoint, Quaternion.identity);

            if (disableBehaviours != null)
            {
                foreach (var b in disableBehaviours)
                {
                    if (b != null) b.enabled = false;
                }
            }

            if (disableAircraftPhysics && aircraftRigidbody != null)
            {
                aircraftRigidbody.velocity = Vector3.zero;
                aircraftRigidbody.angularVelocity = Vector3.zero;
                aircraftRigidbody.isKinematic = true;
            }

            if (engineAudioSource != null)
            {
                engineAudioSource.Stop();
            }
            if (explosionAudioSource != null && explosionClip != null)
            {
                explosionAudioSource.PlayOneShot(explosionClip);
            }

            if (breakApartAsync)
                StartCoroutine(BreakApartCoroutine(hitPoint));
            else
                BreakApart(hitPoint);

            if (forceExternalCameraOnCrash && cameraSwitchController != null)
                cameraSwitchController.ForceExternalView();

            if (forceExternalCameraOnCrash && mainCamera != null && cockpitCamera != null)
            {
                mainCamera.enabled = true;
                cockpitCamera.enabled = false;
                if (mainCamera.TryGetComponent(out AudioListener mainAudio))
                    mainAudio.enabled = true;
                if (cockpitCamera.TryGetComponent(out AudioListener cockpitAudio))
                    cockpitAudio.enabled = false;
            }

            if (gameOverRoot != null)
                StartCoroutine(ShowGameOverAfterDelay());
        }

        private void BreakApart(Vector3 explosionCenter)
        {
            List<Transform> targets = parts;
            if (autoCollectParts && (!precacheParts || targets == null || targets.Count == 0))
            {
                CollectParts();
                targets = parts;
            }

            if (targets == null || targets.Count == 0) return;

            int limit = maxPartsToBreak > 0 ? Mathf.Min(maxPartsToBreak, targets.Count) : targets.Count;

            for (int i = 0; i < limit; i++)
            {
                Transform part = targets[i];
                if (part == null) continue;

                BreakPart(part, explosionCenter);
            }
        }

        private IEnumerator BreakApartCoroutine(Vector3 explosionCenter)
        {
            List<Transform> targets = parts;
            if (autoCollectParts && (!precacheParts || targets == null || targets.Count == 0))
            {
                CollectParts();
                targets = parts;
            }

            if (targets == null || targets.Count == 0) yield break;

            int limit = maxPartsToBreak > 0 ? Mathf.Min(maxPartsToBreak, targets.Count) : targets.Count;
            int perFrame = Mathf.Max(1, partsPerFrame);

            for (int i = 0; i < limit; i++)
            {
                Transform part = targets[i];
                if (part != null)
                {
                    BreakPart(part, explosionCenter);
                }

                if ((i + 1) % perFrame == 0)
                    yield return null;
            }
        }

        private void BreakPart(Transform part, Vector3 explosionCenter)
        {
            part.SetParent(null, true);

            if (part.TryGetComponent<Rigidbody>(out var rb) == false)
            {
                rb = part.gameObject.AddComponent<Rigidbody>();
            }

            if (useSimpleColliders)
            {
                Collider existing = part.GetComponent<Collider>();
                if (existing is MeshCollider meshCol)
                {
                    Destroy(meshCol);
                    existing = null;
                }

                if (existing == null)
                {
                    BoxCollider box = part.gameObject.AddComponent<BoxCollider>();
                    ApplyBoxSize(part, box);
                }
            }

            rb.AddExplosionForce(explosionForce, explosionCenter, explosionRadius, 0.5f, ForceMode.Impulse);
            Destroy(part.gameObject, debrisLifetime);
        }

        private void CollectParts()
        {
            parts.Clear();
            foreach (Transform t in partsRoot.GetComponentsInChildren<Transform>())
            {
                if (t == partsRoot) continue;
                parts.Add(t);
            }
        }

        private void ApplyBoxSize(Transform part, BoxCollider box)
        {
            if (boxColliderSizeOverride != Vector3.zero)
            {
                box.size = boxColliderSizeOverride;
                box.center = Vector3.zero;
                return;
            }

            Renderer[] renderers = part.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
            {
                box.size = Vector3.one * 0.5f;
                box.center = Vector3.zero;
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            box.center = part.InverseTransformPoint(bounds.center);
            box.size = bounds.size;
        }

        private IEnumerator ShowGameOverAfterDelay()
        {
            yield return new WaitForSeconds(gameOverDelay);

            if (deadText != null) deadText.text = deadMessage;
            if (gameOverText != null) gameOverText.text = gameOverMessage;
            gameOverRoot.SetActive(true);

            if (returnToMenuAfterGameOver && !string.IsNullOrEmpty(menuSceneName))
            {
                if (returnToMenuDelay > 0f)
                    yield return new WaitForSeconds(returnToMenuDelay);
                SceneManager.LoadScene(menuSceneName, LoadSceneMode.Single);
            }
        }
    }
}
