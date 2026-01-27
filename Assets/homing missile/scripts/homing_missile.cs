using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HomingMissile
{
    public class homing_missile : MonoBehaviour
    {
        // --- Legacy / gameplay ---
        public int speed = 200;
        public int downspeed = 30;
        public int damage = 35;

        public bool fully_active = false;

        public int timebeforeactivition = 20;
        public int timebeforebursting = 40;
        public int timebeforedestruction = 450;

        public int timealive;
        public GameObject target;     // 락온 시 실시간 추적 대상
        public GameObject shooter;

        public Rigidbody projectilerb;
        public bool isactive = false;

        public Vector3 sleepposition;
        public GameObject targetpointer; // (현재 로직에서는 사용하지 않음)

        // --- Steering / launch ---
        public float turnRateDegPerSec = 240f;

        public Transform launchPoint;
        public Vector3 inheritedVelocity = Vector3.zero;

        public Vector3 launchForward = Vector3.forward;
        public Vector3 launchUp = Vector3.up;
        public float launchYawOffset = 0f;

        // --- Thrust / physics ---
        public float maxSpeed = 850f;
        public float acceleration = 80f;

        public float activationDelaySeconds = 0.4f;
        public float burstDelaySeconds = 0.8f;
        public float lifetimeSeconds = 8f;

        // --- Guidance / fuse ---
        public bool homingEnabled = true; // WeaponActuator에서 락온 여부로 세팅
        public float proximityFuseRadius = 8f;
        public float stopSpeedEpsilon = 0.5f; // 필요 시 사용

        // --- Debug ---
        public bool logTracking = true;
        public float logIntervalSeconds = 0.2f;

        // --- Audio / VFX ---
        public AudioSource launch_sound;
        public AudioSource thrust_sound;

        public GameObject smoke_obj;
        public ParticleSystem smoke;
        public GameObject smoke_position;

        public GameObject destroy_effect;

        // --- Internal state ---
        private float timeAliveSeconds;
        private bool smokeSpawned;
        private float nextLogTime;

        private Vector3 launchDirection = Vector3.forward;
        private Vector3 lastKnownTargetPos;
        private bool hasLastTargetPos;

        private void Start()
        {
            projectilerb = GetComponent<Rigidbody>();
        }

        public void call_destroy_effects()
        {
            if (destroy_effect != null)
                Instantiate(destroy_effect, transform.position, transform.rotation);
        }

        /// <summary>
        /// 발사체 초기화(위치/회전/물리 상태)
        /// </summary>
        public void setmissile()
        {
            timealive = 0;
            timeAliveSeconds = 0f;
            smokeSpawned = false;
            fully_active = false;

            Transform source = launchPoint != null ? launchPoint : (shooter != null ? shooter.transform : transform);

            Vector3 forward = launchForward.sqrMagnitude > 0.001f ? launchForward : source.forward;
            Vector3 up = launchUp.sqrMagnitude > 0.001f ? launchUp : source.up;

            transform.rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, launchYawOffset, 0f);
            transform.position = source.position;

            launchDirection = transform.forward;

            if (projectilerb != null)
            {
                projectilerb.velocity = inheritedVelocity;
                projectilerb.angularVelocity = Vector3.zero;
                projectilerb.useGravity = true; // 중력 ON
            }
        }

        public void SetInheritedVelocity(Vector3 velocity)
        {
            inheritedVelocity = velocity;
        }

        public void SetLifeTimeSeconds(float seconds)
        {
            lifetimeSeconds = Mathf.Max(0.1f, seconds);
        }

        public void DestroyMe(string reason = "Unknown")
        {
            if (logTracking)
            {
                Vector3 missilePos = transform.position;
                string targetStr = hasLastTargetPos ? Vec3ToString(lastKnownTargetPos) : "(none)";
                float dist = hasLastTargetPos ? Vector3.Distance(missilePos, lastKnownTargetPos) : -1f;
                Debug.Log($"[HomingMissile] 폭발({reason}): 미사일 {Vec3ToString(missilePos)} / 타겟 {targetStr} / 거리 {dist:F1}m");
            }

            isactive = false;
            fully_active = false;
            timealive = 0;
            timeAliveSeconds = 0f;

            // smoke null 방어
            if (smoke != null)
            {
                smoke.transform.SetParent(null);
                smoke.Pause();
                smoke.transform.position = sleepposition;
                smoke.Play();
                Destroy(smoke.gameObject, 3);
            }

            if (projectilerb != null)
            {
                projectilerb.velocity = Vector3.zero;
                projectilerb.angularVelocity = Vector3.zero;
                projectilerb.useGravity = false;
            }

            inheritedVelocity = Vector3.zero;

            if (thrust_sound != null) thrust_sound.Pause();

            call_destroy_effects();

            transform.position = sleepposition;

            Destroy(gameObject);
        }

        /// <summary>
        /// 발사
        /// </summary>
        public void usemissile()
        {
            if (launch_sound != null) launch_sound.Play();

            hasLastTargetPos = false;
            if (target != null)
            {
                lastKnownTargetPos = target.transform.position;
                hasLastTargetPos = true;
            }

            isactive = true;
            setmissile();

            if (logTracking)
            {
                string targetStr = hasLastTargetPos ? Vec3ToString(lastKnownTargetPos) : "(none)";
                Debug.Log($"[HomingMissile] 발사: 미사일 {Vec3ToString(transform.position)} / 타겟 {targetStr} / homing={homingEnabled}");
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isactive) return;

            if (shooter != null && other.transform.root == shooter.transform.root)
                return;

            if (other.transform.root == transform.root)
                return;

            DestroyMe("Collision");
        }

        void FixedUpdate()
        {
            if (!isactive) return;

            timeAliveSeconds += Time.fixedDeltaTime;

            float activationDelay = activationDelaySeconds > 0f
                ? activationDelaySeconds
                : timebeforeactivition * Time.fixedDeltaTime;

            float burstDelay = burstDelaySeconds > 0f
                ? burstDelaySeconds
                : timebeforebursting * Time.fixedDeltaTime;

            float lifeLimit = lifetimeSeconds > 0f
                ? lifetimeSeconds
                : timebeforedestruction * Time.fixedDeltaTime;

            if (!fully_active && timeAliveSeconds >= activationDelay)
            {
                fully_active = true;
                if (thrust_sound != null) thrust_sound.Play();
            }

            // 로그: 절대 좌표(월드) 비교
            if (logTracking && Time.time >= nextLogTime)
            {
                Vector3 missilePos = transform.position;
                Vector3 liveTargetPos = target != null ? target.transform.position : (hasLastTargetPos ? lastKnownTargetPos : missilePos);
                float dist = Vector3.Distance(missilePos, liveTargetPos);

                Debug.Log($"[HomingMissile] 위치 M {Vec3ToString(missilePos)} / 타겟(실제) {Vec3ToString(liveTargetPos)} / 거리 {dist:F1}m");
                nextLogTime = Time.time + Mathf.Max(0.02f, logIntervalSeconds);
            }

            // ---- (초반) 버스트 전: 하강 연출 ----
            if (timeAliveSeconds < burstDelay)
            {
                if (projectilerb != null)
                    projectilerb.AddForce(-transform.up * downspeed, ForceMode.Acceleration);

                return;
            }

            // ---- Smoke spawn ----
            if (!smokeSpawned)
            {
                if (smoke_obj != null && smoke_position != null)
                {
                    smoke = Instantiate(smoke_obj, smoke_position.transform.position, smoke_position.transform.rotation)
                        .GetComponent<ParticleSystem>();

                    if (smoke != null)
                    {
                        smoke.Play();
                        smoke.transform.SetParent(transform);
                    }
                }
                smokeSpawned = true;
            }

            // ---- lifetime ----
            if (timeAliveSeconds >= lifeLimit)
            {
                DestroyMe("LifeLimit");
                return;
            }

            // ---- 타겟 위치 갱신 ----
            Vector3 targetPoint = Vector3.zero;
            bool hasTargetPoint = false;
            if (homingEnabled && target != null)
            {
                targetPoint = target.transform.position;
                lastKnownTargetPos = targetPoint;
                hasLastTargetPos = true;
                hasTargetPoint = true;
            }
            else if (homingEnabled && hasLastTargetPos)
            {
                targetPoint = lastKnownTargetPos;
                hasTargetPoint = true;
            }

            // ---- proximity fuse ----
            if (homingEnabled && hasTargetPoint && proximityFuseRadius > 0f)
            {
                float sqrDist = (targetPoint - transform.position).sqrMagnitude;
                if (sqrDist <= proximityFuseRadius * proximityFuseRadius)
                {
                    DestroyMe("ProximityFuse");
                    return;
                }
            }

            // ---- desired direction ----
            Vector3 desiredDir = launchDirection;
            if (homingEnabled && hasTargetPoint)
            {
                Vector3 toTarget = targetPoint - transform.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                    desiredDir = toTarget.normalized;
            }

            // ---- steer (rotation) ----
            float maxDeg = turnRateDegPerSec * Time.fixedDeltaTime;
            Quaternion desiredRotation = Quaternion.LookRotation(desiredDir, (launchUp.sqrMagnitude > 0.001f) ? launchUp : transform.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, maxDeg);

            // ---- thrust + velocity guidance ----
            if (projectilerb != null)
            {
                Vector3 v = projectilerb.velocity;
                float spd = v.magnitude;
                float maxRad = Mathf.Deg2Rad * turnRateDegPerSec * Time.fixedDeltaTime;
                Vector3 newDir = (spd > 0.1f) ? Vector3.RotateTowards(v.normalized, desiredDir, maxRad, 0f) : desiredDir;
                float newSpeed = Mathf.Min(maxSpeed, spd + (fully_active ? acceleration : 0f) * Time.fixedDeltaTime);

                projectilerb.velocity = newDir * Mathf.Max(newSpeed, 0.1f);
            }
        }

        private static string Vec3ToString(Vector3 v)
        {
            return $"({v.x:F1}, {v.y:F1}, {v.z:F1})";
        }
    }
}
