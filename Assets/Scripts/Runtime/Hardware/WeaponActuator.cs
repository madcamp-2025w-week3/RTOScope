/*
 * WeaponActuator.cs - 무장 발사 액추에이터 (HAL 출력 레이어)
 *
 * [역할]
 * - RTOS의 발사 명령을 받아 미사일 프리팹을 생성/발사
 * - 하드포인트 위치/방향에서 발사
 * - 항공기 속도 상속 적용
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
            LogMissilePrefabInfo();
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

            // 첫 초기화 시에만 MissileCount 설정 (기존 값이 없을 때)
            // AircraftState 생성자에서 이미 초기화되므로 여기서는 건드리지 않음
            // 하드포인트 배열이 없으면 기본값 사용
            if (State.HardpointAmmoCount == null || State.HardpointAmmoCount.Length == 0)
            {
                State.MissileCount = _initialMissileCount;
            }

            _stateInitialized = true;
        }

        private void LogMissilePrefabInfo()
        {
            if (!_log) return;

            if (_missilePrefab == null)
            {
                Log("[WeaponActuator] 미사일 프리팹이 할당되지 않았습니다.");
                return;
            }

            homing_missile root = _missilePrefab.GetComponent<homing_missile>();
            homing_missile child = _missilePrefab.GetComponentInChildren<homing_missile>(true);

            string rootState = root != null ? "있음" : "없음";
            string childState = child != null ? "있음" : "없음";
            Log($"[WeaponActuator] 미사일 프리팹 확인: {_missilePrefab.name}, 루트 컴포넌트 {rootState}, 자식 포함 {childState}");
        }

        private void ProcessFireRequest()
        {
            if (!State.WeaponFireRequest)
                return;

            State.WeaponFireAck = false;

            if (_missilePrefab == null || _hardpoints == null || _hardpoints.Length == 0)
            {
                Log("[WeaponActuator] 미사일 프리팹 또는 하드포인트가 없습니다.");
                State.WeaponFireRequest = false;
                return;
            }

            int index = Mathf.Clamp(State.SelectedHardpointIndex, 0, _hardpoints.Length - 1);
            Transform launchPoint = _hardpoints[index];
            if (launchPoint == null)
            {
                Log("[WeaponActuator] 하드포인트 Transform이 비어있습니다.");
                State.WeaponFireRequest = false;
                return;
            }

            Transform targetTransform = null;
            bool hasLock = State.LockedTargetValid;
            if (hasLock && _targetingSensor != null)
            {
                _targetingSensor.TryGetLockedTarget(out targetTransform);
            }
            if (hasLock)
            {
                Log("[WeaponActuator] 락온 상태로 발사");
            }

            GameObject missileObj = Instantiate(_missilePrefab, launchPoint.position, launchPoint.rotation);
            homing_missile missile = missileObj.GetComponent<homing_missile>();
            if (missile == null)
            {
                missile = missileObj.GetComponentInChildren<homing_missile>(true);
            }
            if (missile == null)
            {
                string prefabName = _missilePrefab != null ? _missilePrefab.name : "(null)";
                Log($"[WeaponActuator] homing_missile 컴포넌트를 찾을 수 없습니다. 미사일 프리팹 확인 필요: {prefabName}");
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
            if (hasLock && targetTransform != null)
            {
                targetObject = targetTransform.gameObject;
                Log($"[WeaponActuator] 타겟 참조 전달: {targetObject.name}, 거리 {State.LockedTargetDistance:F1}m, 각도 {State.LockedTargetAngle:F1}°");
            }
            else if (hasLock && targetTransform == null)
            {
                Vector3 fallbackPos = State.LockedTargetPosition != Vector3.zero
                    ? State.LockedTargetPosition
                    : launchPoint.position + launchPoint.forward * 5000f;

                GameObject dummy = new GameObject("Missile_LockFallback");
                dummy.transform.position = fallbackPos;
                dummy.transform.rotation = launchPoint.rotation;
                dummy.transform.SetParent(missileObj.transform, true);
                targetObject = dummy;

                Log("[WeaponActuator] 타겟 참조 전달(대체): Transform 없음, 위치 기반으로 고정");
            }
            else
            {
                Log("[WeaponActuator] 락온 없음: 정면 직진 발사");
            }

            missile.homingEnabled = hasLock;
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

            // 탄약 차감: MissileCount와 HardpointAmmoCount 모두 차감
            State.MissileCount = Mathf.Max(0, State.MissileCount - 1);

            // 하드포인트별 탄약도 차감 (StoresManagementTask와 동기화)
            if (State.HardpointAmmoCount != null && index < State.HardpointAmmoCount.Length)
            {
                State.HardpointAmmoCount[index] = Mathf.Max(0, State.HardpointAmmoCount[index] - 1);
            }

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

        private void Log(string message)
        {
            if (!_log) return;
            Debug.Log(message);
        }
    }
}
