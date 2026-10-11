using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.World
{
    /// <summary>
    /// Presentation-only identity carried by the root of an additive world chunk scene.
    /// This component does not own simulation, spawning, navigation, or collision rules.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldChunkPresentation : MonoBehaviour
    {
        [Header("Stable identity")]
        [SerializeField] private uint _worldId;
        [SerializeField] private uint _zoneId;
        [SerializeField] private int _chunkX;
        [SerializeField] private int _chunkZ;
        [SerializeField, Min(1)] private int _presentationVersion = 1;
        [SerializeField] private string _sourceFingerprint = string.Empty;

        [Header("Presentation")]
        [SerializeField] private Transform _visualRoot;

        public uint WorldId => _worldId;

        public uint ZoneId => _zoneId;

        public WorldChunkCoordinate Coordinate =>
            new WorldChunkCoordinate(_chunkX, _chunkZ);

        public int PresentationVersion => _presentationVersion;

        public string SourceFingerprint => _sourceFingerprint ?? string.Empty;

        public Transform PresentationRoot => transform;

        public Transform VisualRoot => _visualRoot != null ? _visualRoot : transform;

        public bool Matches(
            uint worldId,
            uint zoneId,
            WorldChunkCoordinate coordinate)
        {
            return _worldId == worldId &&
                   _zoneId == zoneId &&
                   _chunkX == coordinate.X &&
                   _chunkZ == coordinate.Z;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            uint worldId,
            uint zoneId,
            WorldChunkCoordinate coordinate,
            Transform visualRoot,
            string sourceFingerprint,
            int presentationVersion = 1)
        {
            _worldId = worldId;
            _zoneId = zoneId;
            _chunkX = coordinate.X;
            _chunkZ = coordinate.Z;
            _visualRoot = visualRoot;
            _sourceFingerprint = sourceFingerprint ?? string.Empty;
            _presentationVersion = Mathf.Max(1, presentationVersion);
        }

        private void OnValidate()
        {
            _presentationVersion = Mathf.Max(1, _presentationVersion);
        }
#endif
    }
}
