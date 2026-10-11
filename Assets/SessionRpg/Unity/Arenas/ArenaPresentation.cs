using System;
using UnityEngine;

namespace SessionRpg.Unity.Arenas
{
    public enum ArenaContentMode
    {
        None = 0,
        Arena = 1,
        Boss = 2,
        Siege = 3,
    }

    [Flags]
    public enum ArenaPresentationCapabilities
    {
        None = 0,
        FlatCombatGround = 1 << 0,
        AuthoritativeCover = 1 << 1,
        HazardBoundary = 1 << 2,
        BakedLighting = 1 << 3,
    }

    /// <summary>
    /// Presentation-only contract that can live on a current embedded scene
    /// root, a prefab root, or the root of a future additive content scene.
    /// None of its values are authoritative combat rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ArenaPresentation : MonoBehaviour
    {
        private static readonly ArenaCaptureAnchor[] EmptyAnchors =
            Array.Empty<ArenaCaptureAnchor>();

        [Header("Identity")]
        [SerializeField] private ArenaContentId _contentId;
        [SerializeField] private ArenaContentMode _mode;
        [SerializeField, Min(1)] private int _presentationVersion = 1;
        [SerializeField] private ArenaPresentationCapabilities _capabilities =
            ArenaPresentationCapabilities.FlatCombatGround;

        [Header("Composition")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Transform _gameplayProxyRoot;
        [SerializeField] private Transform _groundRoot;
        [SerializeField] private float _combatGroundHeight;

        [Header("Reference captures")]
        [SerializeField] private ArenaCaptureAnchor[] _captureAnchors = EmptyAnchors;

        public ArenaContentId ContentId => _contentId;

        public ArenaContentMode Mode => _mode;

        public int PresentationVersion => _presentationVersion;

        public ArenaPresentationCapabilities Capabilities => _capabilities;

        public Transform PresentationRoot => transform;

        public Transform VisualRoot => _visualRoot;

        public Transform GameplayProxyRoot => _gameplayProxyRoot;

        public Transform GroundRoot => _groundRoot;

        public float CombatGroundHeight => _combatGroundHeight;

        public int CaptureAnchorCount => _captureAnchors != null
            ? _captureAnchors.Length
            : 0;

        public bool HasCapability(ArenaPresentationCapabilities capability)
        {
            return (_capabilities & capability) == capability;
        }

        public ArenaCaptureAnchor GetCaptureAnchor(int index)
        {
            if (_captureAnchors == null || index < 0 || index >= _captureAnchors.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _captureAnchors[index];
        }

        public bool TryGetCaptureAnchor(
            ArenaCaptureAnchorKind kind,
            out ArenaCaptureAnchor anchor)
        {
            int count = CaptureAnchorCount;
            for (int index = 0; index < count; index++)
            {
                ArenaCaptureAnchor candidate = _captureAnchors[index];
                if (candidate != null && candidate.Kind == kind)
                {
                    anchor = candidate;
                    return true;
                }
            }

            anchor = null;
            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _presentationVersion = Mathf.Max(1, _presentationVersion);
            _captureAnchors ??= EmptyAnchors;
        }
#endif
    }
}
