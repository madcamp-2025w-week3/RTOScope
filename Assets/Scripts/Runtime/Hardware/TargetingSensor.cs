/*
 * TargetingSensor.cs - 타겟 탐지 센서 (HAL 입력 레이어)
 *
 * [역할]
 * - 카메라 FOV/거리 기반으로 타겟 후보 탐지
 * - Enemy 레이어 또는 Target 태그만 탐지
 * - RTOS에서 사용할 후보/락온 정보 계산 후 AircraftState에 기록
 *
 * [주의]
 * - Unity API 사용 가능 (HAL 레이어)
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

        [Header("Debug")]
        [SerializeField] private bool _drawDebug = false;

        public AircraftState State { get; set; }

        private readonly Dictionary<int, Transform> _candidateMap = new Dictionary<int, Transform>();
        private Transform _lastCandidate;
        private int _lastCandidateId = -1;

        private int _lockedIdCache = -1;
        private Transform _lockedTransform;

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
        }

        private void FixedUpdate()
        {
            if (State == null || _targetCamera == null) return;

            UpdateCandidates();
            UpdateLockedInfo();
        }

        public bool TryGetLockedTarget(out Transform target)
        {
            if (State == null || !State.LockedTargetValid)
            {
                target = null;
                return false;
            }

            if (_lockedTransform == null && _candidateMap.TryGetValue(State.LockedTargetId, out Transform t))
            {
                _lockedTransform = t;
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

            Collider[] hits = Physics.OverlapSphere(camPos, _maxRange, ~0, QueryTriggerInteraction.Ignore);

            float bestScore = float.MaxValue;
            float halfFov = _lockFov * 0.5f;

            foreach (Collider hit in hits)
            {
                if (hit == null) continue;
                Transform t = hit.transform;
                if (!IsValidTarget(t)) continue;

                int id = t.gameObject.GetInstanceID();
                if (!_candidateMap.ContainsKey(id))
                {
                    _candidateMap.Add(id, t);
                }

                Vector3 toTarget = t.position - camPos;
                float distance = toTarget.magnitude;
                if (distance < 0.001f) continue;

                float angle = Vector3.Angle(camForward, toTarget);
                if (angle > halfFov) continue;

                float score = angle + distance * 0.001f;
                if (score < bestScore)
                {
                    bestScore = score;
                    _lastCandidate = t;
                    _lastCandidateId = id;
                }
            }

            if (_lastCandidate != null)
            {
                Vector3 toTarget = _lastCandidate.position - camPos;
                State.TargetCandidateAvailable = true;
                State.TargetCandidateId = _lastCandidateId;
                State.TargetCandidatePosition = _lastCandidate.position;
                State.TargetCandidateDistance = toTarget.magnitude;
                State.TargetCandidateAngle = Vector3.Angle(camForward, toTarget);
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
                    Debug.DrawLine(camPos, _lastCandidate.position, Color.green);
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
                if (_candidateMap.TryGetValue(_lockedIdCache, out Transform t))
                {
                    _lockedTransform = t;
                }
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

        private bool IsValidTarget(Transform t)
        {
            if (t == null) return false;
            if (_selfRoot != null && t.root == _selfRoot) return false;

            bool isEnemyLayer = (_enemyLayerMask.value & (1 << t.gameObject.layer)) != 0;
            bool isTargetTag = !string.IsNullOrEmpty(_targetTag) && t.CompareTag(_targetTag);

            if (!isEnemyLayer && !isTargetTag) return false;

            return true;
        }
    }
}
