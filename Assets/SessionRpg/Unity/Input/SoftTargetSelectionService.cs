using UnityEngine;

namespace SessionRpg.Unity.Input
{
    public enum TargetRelationFilter
    {
        Enemy,
        Friendly,
        Any,
    }

    [DisallowMultipleComponent]
    public sealed class SoftTargetSelectionService : MonoBehaviour
    {
        private const int CandidateCapacity = 64;

        [SerializeField] private Transform _origin;
        [SerializeField] private UnityEngine.Camera _viewCamera;
        [SerializeField] private int _localTeamId;
        [SerializeField] private TargetRelationFilter _relationFilter = TargetRelationFilter.Enemy;
        [SerializeField] private LayerMask _targetLayers = ~0;
        [SerializeField] private LayerMask _occlusionLayers = ~0;
        [SerializeField, Min(0.1f)] private float _maxDistance = 28f;
        [SerializeField, Range(1f, 180f)] private float _acquireViewAngle = 70f;
        [SerializeField, Range(1f, 180f)] private float _retainViewAngle = 125f;
        [SerializeField, Min(0.01f)] private float _refreshInterval = 0.1f;
        [SerializeField] private bool _requireLineOfSight = true;

        private readonly Collider[] _colliderBuffer = new Collider[CandidateCapacity];
        private readonly SoftTargetCandidate[] _candidateBuffer = new SoftTargetCandidate[CandidateCapacity];
        private readonly RaycastHit[] _screenHitBuffer = new RaycastHit[CandidateCapacity];
        private SoftTargetCandidate _currentTarget;
        private int _candidateCount;
        private float _nextRefreshTime;
        private bool _bufferSaturated;
        private bool _currentTargetWasExplicitlySelected;

        public SoftTargetCandidate CurrentTarget => _currentTarget;

        public int CandidateCount => _candidateCount;

        public bool BufferSaturated => _bufferSaturated;

        public int TargetLayerMask => _targetLayers.value;

        public int OcclusionLayerMask => _occlusionLayers.value;

        public bool CurrentTargetWasExplicitlySelected =>
            _currentTargetWasExplicitlySelected;

        private void Update()
        {
            if (Time.unscaledTime >= _nextRefreshTime)
            {
                RefreshNow();
                _nextRefreshTime = Time.unscaledTime + _refreshInterval;
            }
        }

        private void OnDisable()
        {
            _candidateCount = 0;
            SetCurrentTarget(null, false);
        }

        public void SetContext(Transform origin, UnityEngine.Camera viewCamera, int localTeamId)
        {
            _origin = origin;
            _viewCamera = viewCamera;
            _localTeamId = localTeamId;
            RefreshNow();
        }

        public void SetRelationFilter(TargetRelationFilter relationFilter)
        {
            if (_relationFilter == relationFilter)
            {
                return;
            }

            _relationFilter = relationFilter;
            RefreshNow();
        }

        public void RefreshNow()
        {
            _candidateCount = 0;
            _bufferSaturated = false;

            if (_origin == null || _viewCamera == null)
            {
                SetCurrentTarget(null, false);
                return;
            }

            int colliderCount = Physics.OverlapSphereNonAlloc(
                _origin.position,
                _maxDistance,
                _colliderBuffer,
                _targetLayers,
                QueryTriggerInteraction.Collide);
            _bufferSaturated = colliderCount == _colliderBuffer.Length;

            bool currentIsValid = false;

            for (int i = 0; i < colliderCount; i++)
            {
                Collider targetCollider = _colliderBuffer[i];
                if (targetCollider == null)
                {
                    continue;
                }

                SoftTargetCandidate candidate = targetCollider.GetComponentInParent<SoftTargetCandidate>();
                if (candidate == null || ContainsCandidate(candidate) || !MatchesRelation(candidate))
                {
                    continue;
                }

                bool isCurrent = candidate == _currentTarget;
                float permittedAngle = isCurrent ? _retainViewAngle : _acquireViewAngle;
                if (!IsCandidateValid(candidate, permittedAngle))
                {
                    continue;
                }

                _candidateBuffer[_candidateCount] = candidate;
                _candidateCount++;

                if (isCurrent)
                {
                    currentIsValid = true;
                }

            }

            if (!currentIsValid &&
                _currentTargetWasExplicitlySelected &&
                _currentTarget != null &&
                MatchesRelation(_currentTarget))
            {
                // Range, view angle and cover may block a cast, but they must
                // never replace the hero explicitly chosen by the player.
                currentIsValid = true;
            }

            if (currentIsValid)
            {
                SetCurrentTarget(_currentTarget, _currentTargetWasExplicitlySelected);
            }
            else
            {
                SetCurrentTarget(null, false);
            }
        }

        public bool TrySetExplicit(SoftTargetCandidate candidate)
        {
            if (candidate == null)
            {
                SetCurrentTarget(null, false);
                return true;
            }

            if (!MatchesRelation(candidate) ||
                !IsCandidateSpatiallyValid(candidate, _retainViewAngle))
            {
                SetCurrentTarget(null, false);
                return false;
            }

            SetCurrentTarget(candidate, true);
            return true;
        }

        public bool TryGetScreenCandidate(
            Vector2 screenPosition,
            out SoftTargetCandidate candidate)
        {
            candidate = null;
            if (_viewCamera == null)
            {
                return false;
            }

            Physics.SyncTransforms();
            Ray screenRay = _viewCamera.ScreenPointToRay(screenPosition);
            int hitCount = Physics.RaycastNonAlloc(
                screenRay,
                _screenHitBuffer,
                _viewCamera.farClipPlane,
                _targetLayers | _occlusionLayers,
                QueryTriggerInteraction.Collide);
            RaycastHit nearestHit = default;
            float nearestDistance = float.PositiveInfinity;

            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _screenHitBuffer[index];
                if (hit.collider != null && hit.distance < nearestDistance)
                {
                    nearestHit = hit;
                    nearestDistance = hit.distance;
                }
            }

            if (nearestHit.collider == null)
            {
                return false;
            }

            candidate = nearestHit.collider.GetComponentInParent<SoftTargetCandidate>();
            if (candidate == null || !candidate.IsTargetable)
            {
                candidate = null;
                return false;
            }

            return true;
        }

        public void ClearSelection()
        {
            SetCurrentTarget(null, false);
        }

        private bool ContainsCandidate(SoftTargetCandidate candidate)
        {
            for (int i = 0; i < _candidateCount; i++)
            {
                if (_candidateBuffer[i] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private bool MatchesRelation(SoftTargetCandidate candidate)
        {
            if (!candidate.IsTargetable)
            {
                return false;
            }

            if (_origin == candidate.transform ||
                candidate.transform.IsChildOf(_origin) ||
                _origin.IsChildOf(candidate.transform))
            {
                return false;
            }

            switch (_relationFilter)
            {
                case TargetRelationFilter.Enemy:
                    return candidate.TeamId != _localTeamId;
                case TargetRelationFilter.Friendly:
                    return candidate.TeamId == _localTeamId;
                default:
                    return true;
            }
        }

        private bool IsCandidateValid(SoftTargetCandidate candidate, float permittedAngle)
        {
            if (!IsCandidateSpatiallyValid(candidate, permittedAngle))
            {
                return false;
            }

            Vector3 originPosition = _origin.position;
            if (_requireLineOfSight && Physics.Linecast(
                    originPosition,
                    candidate.AimPosition,
                    out RaycastHit hit,
                    _occlusionLayers,
                    QueryTriggerInteraction.Ignore))
            {
                SoftTargetCandidate hitCandidate = hit.collider.GetComponentInParent<SoftTargetCandidate>();
                if (hitCandidate != candidate)
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsCandidateSpatiallyValid(
            SoftTargetCandidate candidate,
            float permittedAngle)
        {
            if (!candidate.IsTargetable)
            {
                return false;
            }

            Vector3 originPosition = _origin.position;
            Vector3 toTarget = candidate.AimPosition - originPosition;
            float sqrDistance = toTarget.sqrMagnitude;
            if (sqrDistance <= 0.0001f || sqrDistance > _maxDistance * _maxDistance)
            {
                return false;
            }

            Vector3 viewForward = _viewCamera.transform.forward;
            viewForward.y = 0f;
            Vector3 planarToTarget = toTarget;
            planarToTarget.y = 0f;
            if (viewForward.sqrMagnitude <= 0.0001f ||
                planarToTarget.sqrMagnitude <= 0.0001f)
            {
                return false;
            }

            float viewAngle = Vector3.Angle(viewForward, planarToTarget);
            return viewAngle <= permittedAngle;
        }

        private void SetCurrentTarget(
            SoftTargetCandidate candidate,
            bool explicitlySelected)
        {
            if (_currentTarget == candidate)
            {
                _currentTargetWasExplicitlySelected =
                    candidate != null && explicitlySelected;
                return;
            }

            _currentTarget = candidate;
            _currentTargetWasExplicitlySelected =
                candidate != null && explicitlySelected;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _maxDistance = Mathf.Max(0.1f, _maxDistance);
            _refreshInterval = Mathf.Max(0.01f, _refreshInterval);
            _retainViewAngle = Mathf.Max(_acquireViewAngle, _retainViewAngle);
        }
#endif
    }
}
