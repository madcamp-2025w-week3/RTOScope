using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace HomingMissile
{
    public class homing_missile : MonoBehaviour
    {
        public int speed = 200;
        public int downspeed = 30;
        public int damage = 35;

        public bool fully_active = false;

        public int timebeforeactivition = 20;
        public int timebeforebursting = 40;
        public int timebeforedestruction = 450;

        public int timealive;
        public GameObject target;
        public GameObject shooter;
        public Rigidbody projectilerb;

        public bool isactive = false;
        public Vector3 sleepposition;

        public GameObject targetpointer;

        // 기존 turnSpeed는 "Slerp의 t"로 쓰기엔 dt에 종속적이라,
        // 아래 turnRateDegPerSec로 바꿔서 물리적으로 일정하게 만듦
        public float turnRateDegPerSec = 240f;

        public Transform launchPoint;

        public Vector3 inheritedVelocity = Vector3.zero;
        public Vector3 launchForward = Vector3.forward;
        public Vector3 launchUp = Vector3.up;
        public float launchYawOffset = 0f;

        public float maxSpeed = 850f;

        // ✅ "가속도"를 실제로 앞으로 미는 추진(Acceleration)로 사용
        public float acceleration = 80f;

        public float activationDelaySeconds = 0.4f;
        public float burstDelaySeconds = 0.8f;
        public float lifetimeSeconds = 8f;

        // ✅ epsilon(도착/근접) 반경: 이미 있던 proximity fuse
        public float proximityFuseRadius = 8f;

        // (선택) 속도가 거의 0일 때만 스냅/정지 같은 로직을 하고 싶으면 사용
        public float stopSpeedEpsilon = 0.5f;

        public AudioSource launch_sound;
        public AudioSource thrust_sound;

        public GameObject smoke_obj;
        public ParticleSystem smoke;
        public GameObject smoke_position;

        public GameObject destroy_effect;

        private float timeAliveSeconds;
        private bool smokeSpawned;

        private void Start()
        {
            projectilerb = GetComponent<Rigidbody>();
        }

        public void call_destroy_effects()
        {
            Instantiate(destroy_effect, transform.position, transform.rotation);
        }

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

            // ✅ 물리 초기화
            if (projectilerb != null)
            {
                projectilerb.velocity = inheritedVelocity;   // 발사체가 가진 초기 속도 상속
                projectilerb.angularVelocity = Vector3.zero;
                projectilerb.useGravity = true;              // ✅ 중력 사용!
            }
        }

        public void SetInheritedVelocity(Vector3 velocity) => inheritedVelocity = velocity;

        public void SetLifeTimeSeconds(float seconds) => lifetimeSeconds = Mathf.Max(0.1f, seconds);

        public void DestroyMe()
        {
            isactive = false;
            fully_active = false;
            timealive = 0;
            timeAliveSeconds = 0f;

            // ✅ smoke 널 방어 (burst 이전 충돌 시 NRE 방지)
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

        public void usemissile()
        {
            if (launch_sound != null) launch_sound.Play();
            isactive = true;
            setmissile();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!isactive) return;

            if (shooter != null && other.transform.root == shooter.transform.root) return;
            if (other.transform.root == transform.root) return;

            DestroyMe();
        }

        void FixedUpdate()
        {
            if (!isactive) return;

            if (target == null || !target.activeInHierarchy)
            {
                DestroyMe();
                return;
            }

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

            // ✅ 연기 생성(기존 유지)
            if (timeAliveSeconds >= burstDelay && !smokeSpawned)
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

            if (timeAliveSeconds >= lifeLimit)
            {
                DestroyMe();
                return;
            }

            // ✅ epsilon 반경(근접신관)
            if (proximityFuseRadius > 0f)
            {
                // 타겟 콜라이더가 있으면 ClosestPoint를 써서 더 정확하게
                Collider col = target.GetComponent<Collider>();
                Vector3 targetPoint = col ? col.ClosestPoint(transform.position) : target.transform.position;

                float sqrDist = (targetPoint - transform.position).sqrMagnitude;
                if (sqrDist <= proximityFuseRadius * proximityFuseRadius)
                {
                    DestroyMe();
                    return;
                }
            }

            // ---------- 1) 조향(회전) ----------
            Quaternion desiredRotation = transform.rotation;
            bool hasPointerRotation = false;

            if (targetpointer != null && targetpointer.activeInHierarchy)
            {
                var pointer = targetpointer.GetComponent<homing_missile_pointer>();
                if (pointer != null && pointer.target != null)
                {
                    desiredRotation = targetpointer.transform.rotation;
                    hasPointerRotation = true;
                }
            }

            if (!hasPointerRotation)
            {
                Vector3 toTarget = target.transform.position - transform.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Vector3 up = (launchUp.sqrMagnitude > 0.001f) ? launchUp : transform.up;
                    desiredRotation = Quaternion.LookRotation(toTarget.normalized, up);
                }
            }

            // ✅ dt-안정적인 회전: RotateTowards 사용
            float maxRadians = Mathf.Deg2Rad * turnRateDegPerSec * Time.fixedDeltaTime;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, desiredRotation, maxRadians * Mathf.Rad2Deg);

            // ---------- 2) 추진/중력 물리 ----------
            // burstDelay 이전에는 "하강" 연출을 AddForce로 처리 (중력과 함께 작동)
            if (timeAliveSeconds < burstDelay)
            {
                // 기존 코드의 '강제 속도 세팅' 대신 아래로 가속을 줌
                projectilerb.AddForce(-transform.up * downspeed, ForceMode.Acceleration);

                // (선택) 초반엔 추진 없이 중력+하강만 주고 싶으면 여기서 return
                return;
            }

            // fully_active 전에는 추진을 약하게 하거나 0으로 둘 수도 있음 (취향)
            if (fully_active)
            {
                // ✅ 앞으로 미는 추진(가속)
                projectilerb.AddForce(transform.forward * acceleration, ForceMode.Acceleration);
            }

            // ✅ 최대 속도 제한 (중력 포함한 전체 속도 magnitude 기준)
            Vector3 v = projectilerb.velocity;
            float spd = v.magnitude;
            if (spd > maxSpeed)
            {
                projectilerb.velocity = v / spd * maxSpeed;
            }

            // (선택) 아주 가까우면 “정지/스냅” 같은 효과를 주고 싶을 때
            // 단, 미사일은 보통 도착하면 폭발이라 기본은 꺼둠.
            // if ((target.transform.position - transform.position).sqrMagnitude < 0.01f && spd < stopSpeedEpsilon) { … }
        }
    }
}
