using System.Collections.Generic;
using UnityEngine;

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

        [Header("Disable On Crash")]
        [SerializeField] private bool disableAircraftPhysics = true;
        [SerializeField] private Behaviour[] disableBehaviours;

        [Header("Parts")]
        [SerializeField] private bool autoCollectParts = true;
        [SerializeField] private List<Transform> parts = new List<Transform>();

        private bool _crashed;

        private void Awake()
        {
            if (aircraftRigidbody == null)
                aircraftRigidbody = GetComponent<Rigidbody>();
            if (partsRoot == null)
                partsRoot = transform;
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

            BreakApart(hitPoint);
        }

        private void BreakApart(Vector3 explosionCenter)
        {
            List<Transform> targets = parts;
            if (autoCollectParts)
            {
                targets = new List<Transform>();
                foreach (Transform t in partsRoot.GetComponentsInChildren<Transform>())
                {
                    if (t == partsRoot) continue;
                    targets.Add(t);
                }
            }

            foreach (Transform part in targets)
            {
                if (part == null) continue;

                part.SetParent(null, true);

                var rb = part.GetComponent<Rigidbody>();
                if (rb == null) rb = part.gameObject.AddComponent<Rigidbody>();

                var col = part.GetComponent<Collider>();
                if (col == null)
                {
                    var meshCol = part.gameObject.AddComponent<MeshCollider>();
                    meshCol.convex = true;
                    col = meshCol;
                }

                rb.AddExplosionForce(explosionForce, explosionCenter, explosionRadius, 0.5f, ForceMode.Impulse);
                Destroy(part.gameObject, debrisLifetime);
            }
        }
    }
}
