/*
 * WeaponActuator.cs - 무장 발사 액추에이터 (HAL 출력 레이어)
 *
 * [역할]
 * - RTOS의 무장 명령을 받아 미사일 프리팹을 생성/발사
 * - 하드포인트 위치/방향에서 발사
 * - 기체 속도 상속 적용
 */

using HomingMissile;
using RTOScope.Runtime.Aircraft;
using RTOScope.Runtime.UI;
using UnityEngine;

namespace RTOScope.Runtime.Hardware
{
    [RequireComponent(typeof(Rigidbody))]
    public class WeaponActuator : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Transform _aircraftRoot;
        [SerializeField] private Rigidbody _aircraftRigidbody;
        [SerializeField] private TargetingSensor _targetingSensor;
        [SerializeField] private HUDController _hudController;

        [Header("Missile Settings")]
        [SerializeField] private GameObject _missilePrefab;
        [SerializeField] private Transform[] _hardpoints;
        [SerializeField] private int _initialMissileCount = 6;
        [SerializeField] private bool _forceMissileVisible = true;
        [SerializeField] private int _missileLayer = 0; // Default

        [Header("Debug")]
        [SerializeField] private bool _log = true;

        public AircraftState State { get; set; }
        private bool _stateInitialized;

        private void Awake()
        {
            if (_aircraftRoot == null)
                _aircraftRoot = transform;

            if (_aircraftRigidbody == null)
                _aircraftRigidbody = GetComponent<Rigidbody>();

            if (_targetingSensor == null)
                _targetingSensor = GetComponent<TargetingSensor>();
        }

        private void Start()
        {
            TryInitializeState();
        }

        private void FixedUpdate()
        {
            if (State == null) return;

            TryInitializeState();
            ProcessFireRequest();
        }

        private void TryInitializeState()
        {
            if (_stateInitialized || State == null) return;
            State.TotalHardpoints = _hardpoints != null ? _hardpoints.Length : 0;
            if (State.MissileCount <= 0)
                State.MissileCount = _initialMissileCount;
            _stateInitialized = true;
        }

        private void ProcessFireRequest()
        {
            if (!State.WeaponFireRequest)
                return;

            State.WeaponFireAck = false;

            if (_missilePrefab == null || _hardpoints == null || _hardpoints.Length == 0)
            {
                if (_log)
                    Debug.LogWarning("[WeaponActuator] 미사일 프리팹 또는 하드포인트가 없습니다.");
                State.WeaponFireRequest = false;
                return;
            }

            int index = Mathf.Clamp(State.SelectedHardpointIndex, 0, _hardpoints.Length - 1);
            Transform launchPoint = _hardpoints[index];
            if (launchPoint == null)
            {
                if (_log)
                    Debug.LogWarning("[WeaponActuator] 하드포인트 Transform이 비어있습니다.");
                State.WeaponFireRequest = false;
                return;
            }

            Transform targetTransform = null;
            if (_targetingSensor != null)
            {
                _targetingSensor.TryGetLockedTarget(out targetTransform);
            }

            GameObject missileObj = Instantiate(_missilePrefab, launchPoint.position, launchPoint.rotation);
            homing_missile missile = missileObj.GetComponent<homing_missile>();
            if (missile == null)
            {
                if (_log)
                    Debug.LogWarning("[WeaponActuator] homing_missile 컴포넌트를 찾을 수 없습니다.");
                Destroy(missileObj);
                State.WeaponFireRequest = false;
                return;
            }

            if (_forceMissileVisible)
            {
                SetLayerRecursively(missileObj, _missileLayer);
                EnableRenderers(missileObj);
            }

            Vector3 launchForward = _aircraftRoot != null ? _aircraftRoot.forward : launchPoint.forward;
            Vector3 launchUp = _aircraftRoot != null ? _aircraftRoot.up : launchPoint.up;

            GameObject targetObject = null;
            if (targetTransform != null)
            {
                targetObject = targetTransform.gameObject;
            }
            else
            {
                // 락온이 없으면 전방으로 직진하도록 더미 타겟 생성
                GameObject dummy = new GameObject("Missile_DumbTarget");
                dummy.transform.position = launchPoint.position + launchPoint.forward * 5000f;
                dummy.transform.rotation = launchPoint.rotation;
                dummy.transform.SetParent(missileObj.transform, true);
                targetObject = dummy;
            }

            missile.target = targetObject;
            if (missile.targetpointer != null)
            {
                homing_missile_pointer pointer = missile.targetpointer.GetComponent<homing_missile_pointer>();
                if (pointer != null)
                    pointer.target = targetObject;
            }

            missile.shooter = _aircraftRoot.gameObject;
            missile.launchPoint = launchPoint;
            missile.launchForward = launchForward;
            missile.launchUp = launchUp;
            missile.SetInheritedVelocity(State.VelocityVector);
            missile.SetLifeTimeSeconds(State.MissileLifeTimeSeconds);

            missile.usemissile();

            DisableHardpointVisual(launchPoint);

            State.MissileCount = Mathf.Max(0, State.MissileCount - 1);
            if (_hudController != null)
            {
                _hudController.ConsumeMissile(1);
            }

            State.WeaponFireAck = true;
            State.WeaponFireRequest = false;
        }

        private static void DisableHardpointVisual(Transform hardpoint)
        {
            if (hardpoint == null) return;
            Renderer[] renderers = hardpoint.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                r.enabled = false;
            }
        }

        private static void EnableRenderers(GameObject obj)
        {
            Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer r in renderers)
            {
                r.enabled = true;
            }
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform)
            {
                SetLayerRecursively(child.gameObject, layer);
            }
        }
    }
}
