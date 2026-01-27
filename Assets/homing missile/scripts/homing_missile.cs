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
    public float turnSpeed = 0.035f;
    public Transform launchPoint;
    public Vector3 inheritedVelocity = Vector3.zero;
    public Vector3 launchForward = Vector3.forward;
    public Vector3 launchUp = Vector3.up;
    public float launchYawOffset = 0f;
    public float maxSpeed = 850f;
    public float acceleration = 80f;
    public float activationDelaySeconds = 0.4f;
    public float burstDelaySeconds = 0.8f;
    public float lifetimeSeconds = 8f;
    public float proximityFuseRadius = 8f;
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
        projectilerb = this.GetComponent<Rigidbody>();
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
        Transform source = launchPoint != null ? launchPoint : (shooter != null ? shooter.transform : transform);
        Vector3 forward = launchForward.sqrMagnitude > 0.001f ? launchForward : source.forward;
        Vector3 up = launchUp.sqrMagnitude > 0.001f ? launchUp : source.up;
        transform.rotation = Quaternion.LookRotation(forward, up) * Quaternion.Euler(0f, launchYawOffset, 0f);
        transform.position = source.position;
    }
    public void SetInheritedVelocity(Vector3 velocity)
    {
        inheritedVelocity = velocity;
    }
    public void SetLifeTimeSeconds(float seconds)
    {
        lifetimeSeconds = Mathf.Max(0.1f, seconds);
    }
    public void DestroyMe()
    {
        isactive = false;
        fully_active = false;
        timealive = 0;
        timeAliveSeconds = 0f;
        smoke.transform.SetParent(null);
        smoke.Pause();
        smoke.transform.position =sleepposition;
        smoke.Play();
        projectilerb.velocity = Vector3.zero;
        inheritedVelocity = Vector3.zero;
        thrust_sound.Pause();
        call_destroy_effects();
        transform.position = sleepposition;
        Destroy(smoke.gameObject,3);
        Destroy(this.gameObject);
    }
    public void usemissile()
    {
        launch_sound.Play();
        isactive = true;
        setmissile();

    }
    private void OnTriggerEnter(Collider other)
    {
        if (isactive)
        {
            if (shooter != null && other.transform.root == shooter.transform.root)
                return;
            if (other.transform.root == transform.root)
                return;
            DestroyMe();
        }
    }
    void FixedUpdate()
    {
        if (isactive)
        {
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
                thrust_sound.Play();
            }

            if (timeAliveSeconds < burstDelay)
            {
                projectilerb.velocity = inheritedVelocity + (transform.up * -1 * downspeed);
                return;
            }

            if (!smokeSpawned)
            {
                smoke = (Instantiate(smoke_obj, smoke_position.transform.position, smoke_position.transform.rotation)).GetComponent<ParticleSystem>();
                smoke.Play();
                smoke.transform.SetParent(this.transform);
                smokeSpawned = true;
            }

            if (timeAliveSeconds >= lifeLimit)
            {
                DestroyMe();
                return;
            }

            if (proximityFuseRadius > 0f)
            {
                float sqrDist = (target.transform.position - transform.position).sqrMagnitude;
                if (sqrDist <= proximityFuseRadius * proximityFuseRadius)
                {
                    DestroyMe();
                    return;
                }
            }

            Quaternion desiredRotation = transform.rotation;
            bool hasPointerRotation = false;
            if (targetpointer != null && targetpointer.activeInHierarchy)
            {
                homing_missile_pointer pointer = targetpointer.GetComponent<homing_missile_pointer>();
                if (pointer != null && pointer.target != null)
                {
                    desiredRotation = targetpointer.transform.rotation;
                    hasPointerRotation = true;
                }
            }

            if (!hasPointerRotation && target != null)
            {
                Vector3 toTarget = target.transform.position - transform.position;
                if (toTarget.sqrMagnitude > 0.0001f)
                {
                    Vector3 up = launchUp.sqrMagnitude > 0.001f ? launchUp : transform.up;
                    desiredRotation = Quaternion.LookRotation(toTarget.normalized, up);
                }
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, turnSpeed);

            Vector3 forward = transform.forward;
            Vector3 currentVelocity = projectilerb.velocity;
            float forwardSpeed = Vector3.Dot(currentVelocity, forward);
            float desiredForward = Mathf.Min(maxSpeed, forwardSpeed + acceleration * Time.fixedDeltaTime);
            Vector3 lateral = currentVelocity - forward * forwardSpeed;
            projectilerb.velocity = (forward * desiredForward) + lateral;
        }
    }
}
}
