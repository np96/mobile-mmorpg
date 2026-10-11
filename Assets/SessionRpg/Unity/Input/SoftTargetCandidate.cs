using UnityEngine;

namespace SessionRpg.Unity.Input
{
    [DisallowMultipleComponent]
    public sealed class SoftTargetCandidate : MonoBehaviour
    {
        [SerializeField] private int _targetId;
        [SerializeField] private int _teamId;
        [SerializeField] private Transform _aimPoint;
        [SerializeField] private bool _isTargetable = true;

        public int TargetId => _targetId;

        public int TeamId => _teamId;

        public bool IsTargetable => _isTargetable && isActiveAndEnabled;

        public Vector3 AimPosition => _aimPoint != null ? _aimPoint.position : transform.position;

        public void Configure(int targetId, int teamId, Transform aimPoint = null)
        {
            _targetId = targetId;
            _teamId = teamId;
            _aimPoint = aimPoint;
        }

        public void SetTargetable(bool isTargetable)
        {
            _isTargetable = isTargetable;
        }
    }
}
