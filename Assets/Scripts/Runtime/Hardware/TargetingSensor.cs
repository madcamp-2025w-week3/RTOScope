/*
 * TargetingSensor.cs - 타겟 탐지 센서 (HAL 입력 레이어)
 *
 * [역할]
 * - 카메라 FOV/거리 기반으로 타겟 후보 탐지
 * - Enemy 레이어 또는 Target 태그만 후보로 인정
 * - RTOS에서 사용하는 타겟 후보/락온 정보를 AircraftState에 기록
 *
 * [주의]
 * - Unity API 사용은 HAL 레이어에서만 수행
 * - RTOS Task에서는 Unity API 호출 금지
 */

using System.Collections.Generic;
using RTOScope.Runtime.Aircraft;
using UnityEngine;

namespace RTOScope.Runtime.Hardware
{
    public class TargetingSensor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private Transform _selfRoot;

        [Header("Detection Settings")]
        [Tooltip("최대 탐지 거리 (m)")]
        [SerializeField] private float _maxRange = 3000f;

        [Tooltip("락온 후보 FOV (deg, 전체 각도)")]
        [SerializeField] private float _lockFov = 40f;

        [Tooltip("브레이크락 FOV (deg, 전체 각도)")]
        [SerializeField] private float _breakFov = 60f;

        [Tooltip("Enemy 레이어 마스크")]
        [SerializeField] private LayerMask _enemyLayerMask;

        [Tooltip("Target 태그 (선택)")]
        [SerializeField] private string _targetTag = "Target";

        [Header("Trigger 포함 여부")]
        [SerializeField] private bool _includeTriggerColliders = true;

        [Header("Line of Sight (Optional)")]
        [SerializeField] private bool _useLineOfSight = false;
        [SerializeField] private LayerMask _occlusionMask = ~0;
        [SerializeField] private LayerMask _occlusionIgnoreMask;

        [Header("Debug")]
        [SerializeField] private bool _drawDebug = false;
        [SerializeField] private bool _log = true;
        [SerializeField] private float _logInterval = 0.5f;

        public AircraftState State { get; set; }

        private readonly Dictionary<int, Transform> _candidateMap = new Dictionary<int, Transform>();
        private readonly Dictionary<int, Transform> _knownTargets = new Dictionary<int, Transform>();
        private Transform _lastCandidate;
        private int _lastCandidateId = -1;
        private Vector3 _lastCandidatePosition;
        private float _lastCandidateDistance;
        private float _lastCandidateAngle;

        private int _lastLoggedCandidateId = -1;
        private float _lastDetectLogTime;

        private int _lockedIdCache = -1;
        private Transform _lockedTransform;
        private Transform _selfRootRoot;

        private void Awake()
        {
            if (_targetCamera == null)
            {
                _targetCamera = GetComponentInChildren<Camera>();
                if (_targetCamera == null)
                {
                    _targetCamera = Camera.main;
                }
            }

            if (_selfRoot == null)
            {
                _selfRoot = transform;
            }

            _selfRootRoot = _selfRoot != null ? _selfRoot.root : null;

            if (_occlusionIgnoreMask.value == 0)
            {
                int groundLayer = LayerMask.NameToLayer("Ground");
                if (groundLayer >= 0)
                {
                    _occlusionIgnoreMask = 1 << groundLayer;
                }
            }
        }

        private void FixedUpdate()
        {
            if (State == null || _targetCamera == null) return;

            UpdateCandidates();
            UpdateLockedInfo();
            CleanupKnownTargets();
        }

        public bool TryGetLockedTarget(out Transform target)
        {
            if (State == null || !State.LockedTargetValid)
            {
                target = null;
                return false;
            }

            if (_lockedTransform == null && _knownTargets.TryGetValue(State.LockedTargetId, out Transform t))
            {
                _lockedTransform = t;
            }

            if (_lockedTransform == null && _candidateMap.TryGetValue(State.LockedTargetId, out Transform c))
            {
                _lockedTransform = c;
            }

            target = _lockedTransform;
            return target != null;
        }

        private void UpdateCandidates()
        {
            _candidateMap.Clear();
            _lastCandidate = null;
            _lastCandidateId = -1;

            Vector3 camPos = _targetCamera.transform.position;
            Vector3 camForward = _targetCamera.transform.forward;

            QueryTriggerInteraction triggerMode = _includeTriggerColliders
                ? QueryTriggerInteraction.Collide
                : QueryTriggerInteraction.Ignore;
            Collider[] hits = Physics.OverlapSphere(camPos, _maxRange, ~0, triggerMode);

            float bestScore = float.MaxValue;
            float halfFov = _lockFov * 0.5f;

            foreach (Collider hit in hits)
            {
                if (hit == null) continue;

                Transform candidate = ResolveTargetTransform(hit.transform);
                if (candidate == null) continue;

                Vector3 targetPos = GetTargetPosition(candidate, hit);
                Vector3 toTarget = targetPos - camPos;
                float distance = toTarget.magnitude;
                if (distance < 0.001f || distance > _maxRange) continue;

                float angle = Vector3.Angle(camForward, toTarget);
                if (angle > halfFov) continue;

                if (_useLineOfSight && !HasLineOfSight(camPos, targetPos, candidate, distance))
                {
                    continue;
                }

                int id = candidate.gameObject.GetInstanceID();
                if (!_candidateMap.ContainsKey(id))
                {
                    _candidateMap.Add(id, candidate);
                }

                if (!_knownTargets.ContainsKey(id))
                {
                    _knownTargets.Add(id, candidate);
                }

                float score = angle + distance * 0.001f;
                if (score < bestScore)
                {
                    bestScore = score;
                    _lastCandidate = candidate;
                    _lastCandidateId = id;
                    _lastCandidatePosition = targetPos;
                    _lastCandidateDistance = distance;
                    _lastCandidateAngle = angle;
                }
            }

            if (_lastCandidate != null)
            {
                State.TargetCandidateAvailable = true;
                State.TargetCandidateId = _lastCandidateId;
                State.TargetCandidatePosition = _lastCandidatePosition;
                State.TargetCandidateDistance = _lastCandidateDistance;
                State.TargetCandidateAngle = _lastCandidateAngle;

                LogTargetDetected();
            }
            else
            {
                State.TargetCandidateAvailable = false;
                State.TargetCandidateId = 0;
                State.TargetCandidatePosition = Vector3.zero;
                State.TargetCandidateDistance = 0f;
                State.TargetCandidateAngle = 0f;
            }

            if (_drawDebug)
            {
                Debug.DrawRay(camPos, camForward * 100f, Color.cyan);
                if (_lastCandidate != null)
                {
                    Debug.DrawLine(camPos, _lastCandidatePosition, Color.green);
                }
            }
        }

        private void UpdateLockedInfo()
        {
            if (!State.LockedTargetValid)
            {
                _lockedIdCache = -1;
                _lockedTransform = null;
                State.LockedTargetDistance = 0f;
                State.LockedTargetAngle = 0f;
                State.LockedTargetPosition = Vector3.zero;
                return;
            }

            if (State.LockedTargetId != _lockedIdCache)
            {
                _lockedIdCache = State.LockedTargetId;
                _lockedTransform = null;
            }

            if (_lockedTransform == null && _knownTargets.TryGetValue(State.LockedTargetId, out Transform known))
            {
                _lockedTransform = known;
            }

            if (_lockedTransform == null && _candidateMap.TryGetValue(State.LockedTargetId, out Transform fallback))
            {
                _lockedTransform = fallback;
            }

            if (_lockedTransform != null)
            {
                Vector3 camPos = _targetCamera.transform.position;
                Vector3 camForward = _targetCamera.transform.forward;
                Vector3 toTarget = _lockedTransform.position - camPos;

                State.LockedTargetPosition = _lockedTransform.position;
                State.LockedTargetDistance = toTarget.magnitude;
                State.LockedTargetAngle = Vector3.Angle(camForward, toTarget);

                float halfBreakFov = _breakFov * 0.5f;
                if (_drawDebug)
                {
                    Color c = State.LockedTargetAngle <= halfBreakFov ? Color.yellow : Color.red;
                    Debug.DrawLine(camPos, _lockedTransform.position, c);
                }
            }
            else
            {
                State.LockedTargetDistance = float.MaxValue;
                State.LockedTargetAngle = 180f;
            }
        }

        private void LogTargetDetected()
        {
            if (!_log || _lastCandidate == null) return;

            bool shouldLog = _lastCandidateId != _lastLoggedCandidateId ||
                             Time.time - _lastDetectLogTime >= _logInterval;

            if (!shouldLog) return;

            _lastLoggedCandidateId = _lastCandidateId;
            _lastDetectLogTime = Time.time;

            Debug.Log($"[TargetingSensor] 타겟 감지: {_lastCandidate.name}, 거리 {_lastCandidateDistance:F1}m, 각도 {_lastCandidateAngle:F1}°");
        }

        private Transform ResolveTargetTransform(Transform t)
        {
            if (t == null) return null;

            Transform candidate = null;
            if (IsTagOrLayerMatch(t))
                candidate = t;
            else if (t.root != null && IsTagOrLayerMatch(t.root))
                candidate = t.root;

            if (candidate == null) return null;

            if (_selfRootRoot != null && candidate.root == _selfRootRoot)
                return null;

            return candidate;
        }

        private bool IsTagOrLayerMatch(Transform t)
        {
            if (t == null) return false;

            bool isEnemyLayer = (_enemyLayerMask.value & (1 << t.gameObject.layer)) != 0;
            bool isTargetTag = !string.IsNullOrEmpty(_targetTag) && t.CompareTag(_targetTag);

            return isEnemyLayer || isTargetTag;
        }

        private Vector3 GetTargetPosition(Transform target, Collider hit)
        {
            return target.position;
        }

        private bool HasLineOfSight(Vector3 camPos, Vector3 targetPos, Transform target, float distance)
        {
            Vector3 dir = (targetPos - camPos).normalized;
            int mask = _occlusionMask.value & ~_occlusionIgnoreMask.value;

            if (Physics.Raycast(camPos, dir, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
            {
                Transform hitRoot = hit.transform != null ? hit.transform.root : null;
                return hit.transform == target || hitRoot == target;
            }

            return true;
        }

        private void CleanupKnownTargets()
        {
            if (_knownTargets.Count == 0) return;

            List<int> removeKeys = null;
            foreach (KeyValuePair<int, Transform> pair in _knownTargets)
            {
                if (pair.Value != null) continue;

                if (removeKeys == null)
                {
                    removeKeys = new List<int>();
                }
                removeKeys.Add(pair.Key);
            }

            if (removeKeys == null) return;

            foreach (int key in removeKeys)
            {
                _knownTargets.Remove(key);
            }
        }
    }
}
