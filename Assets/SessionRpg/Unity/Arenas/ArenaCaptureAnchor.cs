using UnityEngine;

namespace SessionRpg.Unity.Arenas
{
    public enum ArenaCaptureAnchorKind
    {
        None = 0,
        Overview = 1,
        Gameplay = 2,
        DenseCombat = 3,
        TeamA = 4,
        TeamB = 5,
        Cover = 6,
        HazardEdge = 7,
    }

    [DisallowMultipleComponent]
    public sealed class ArenaCaptureAnchor : MonoBehaviour
    {
        [SerializeField] private ArenaCaptureAnchorKind _kind;
        [SerializeField, Range(15f, 100f)] private float _fieldOfView = 50f;
        [SerializeField] private bool _showHud = true;

        public ArenaCaptureAnchorKind Kind => _kind;

        public float FieldOfView => _fieldOfView;

        public bool ShowHud => _showHud;

        public Transform Pose => transform;

#if UNITY_EDITOR
        private void OnValidate()
        {
            _fieldOfView = Mathf.Clamp(_fieldOfView, 15f, 100f);
        }
#endif
    }
}
