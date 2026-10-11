using System;
using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.World
{
    [Serializable]
    public sealed class WorldZoneChunkEntry
    {
        [SerializeField] private int _chunkX;
        [SerializeField] private int _chunkZ;
        [SerializeField] private string _scenePath = string.Empty;
        [SerializeField] private string _sourceFingerprint = string.Empty;

        public WorldChunkCoordinate Coordinate =>
            new WorldChunkCoordinate(_chunkX, _chunkZ);

        /// <summary>
        /// Project-relative scene asset path, for example
        /// Assets/SessionRpg/Scenes/World/AshenCauseway_0_0.unity.
        /// </summary>
        public string ScenePath => _scenePath ?? string.Empty;

        public bool HasScenePath => !string.IsNullOrWhiteSpace(ScenePath);

        public string SourceFingerprint => _sourceFingerprint ?? string.Empty;

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            WorldChunkCoordinate coordinate,
            string scenePath,
            string sourceFingerprint)
        {
            _chunkX = coordinate.X;
            _chunkZ = coordinate.Z;
            _scenePath = scenePath ?? string.Empty;
            _sourceFingerprint = sourceFingerprint ?? string.Empty;
        }
#endif
    }

    [Serializable]
    public sealed class WorldZoneEntry
    {
        private static readonly WorldZoneChunkEntry[] EmptyChunks =
            Array.Empty<WorldZoneChunkEntry>();

        [Header("Stable identity")]
        [SerializeField] private uint _worldId;
        [SerializeField] private uint _zoneId;
        [SerializeField] private string _zoneKey = string.Empty;

        [Header("Streaming contract")]
        [SerializeField, Min(1f)] private float _chunkSizeMeters = 64f;
        [SerializeField, Min(0)] private int _loadRadius = 1;
        [SerializeField, Min(0)] private int _retainRadius = 2;
        [SerializeField, Min(1)] private int _maxLoadedChunks = 25;
        [SerializeField] private WorldZoneChunkEntry[] _chunks = EmptyChunks;

        public uint WorldId => _worldId;

        public uint ZoneId => _zoneId;

        public string ZoneKey => _zoneKey ?? string.Empty;

        public float ChunkSizeMeters => Mathf.Max(1f, _chunkSizeMeters);

        public int LoadRadius => Mathf.Max(0, _loadRadius);

        public int RetainRadius => Mathf.Max(LoadRadius, _retainRadius);

        public int MaxLoadedChunks => Mathf.Max(1, _maxLoadedChunks);

        public int ChunkCount => _chunks != null ? _chunks.Length : 0;

        public WorldZoneChunkEntry GetChunk(int index)
        {
            if (_chunks == null || index < 0 || index >= _chunks.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _chunks[index];
        }

        public bool TryGetChunk(
            WorldChunkCoordinate coordinate,
            out WorldZoneChunkEntry chunk)
        {
            int count = ChunkCount;
            for (int index = 0; index < count; index++)
            {
                WorldZoneChunkEntry candidate = _chunks[index];
                if (candidate != null && candidate.Coordinate.Equals(coordinate))
                {
                    chunk = candidate;
                    return true;
                }
            }

            chunk = null;
            return false;
        }

        public WorldChunkCoordinate WorldToChunk(Vector3 worldPosition)
        {
            float chunkSize = ChunkSizeMeters;
            return new WorldChunkCoordinate(
                Mathf.FloorToInt(worldPosition.x / chunkSize),
                Mathf.FloorToInt(worldPosition.z / chunkSize));
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            uint worldId,
            uint zoneId,
            string zoneKey,
            float chunkSizeMeters,
            int loadRadius,
            int retainRadius,
            int maxLoadedChunks,
            WorldZoneChunkEntry[] chunks)
        {
            _worldId = worldId;
            _zoneId = zoneId;
            _zoneKey = zoneKey ?? string.Empty;
            _chunkSizeMeters = Mathf.Max(1f, chunkSizeMeters);
            _loadRadius = Mathf.Max(0, loadRadius);
            _retainRadius = Mathf.Max(_loadRadius, retainRadius);
            _maxLoadedChunks = Mathf.Max(1, maxLoadedChunks);
            _chunks = chunks ?? EmptyChunks;
        }
#endif
    }

    [CreateAssetMenu(
        fileName = "WorldZoneCatalog",
        menuName = "Session RPG/World/Zone Catalog")]
    public sealed class WorldZoneCatalogAsset : ScriptableObject
    {
        private static readonly WorldZoneEntry[] EmptyZones =
            Array.Empty<WorldZoneEntry>();

        [SerializeField] private WorldZoneEntry[] _zones = EmptyZones;

        public int Count => _zones != null ? _zones.Length : 0;

        public WorldZoneEntry GetZone(int index)
        {
            if (_zones == null || index < 0 || index >= _zones.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _zones[index];
        }

        public bool TryGetZone(
            uint worldId,
            uint zoneId,
            out WorldZoneEntry zone)
        {
            int count = Count;
            for (int index = 0; index < count; index++)
            {
                WorldZoneEntry candidate = _zones[index];
                if (candidate != null &&
                    candidate.WorldId == worldId &&
                    candidate.ZoneId == zoneId)
                {
                    zone = candidate;
                    return true;
                }
            }

            zone = null;
            return false;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(WorldZoneEntry[] zones)
        {
            _zones = zones ?? EmptyZones;
        }

        private void OnValidate()
        {
            _zones ??= EmptyZones;
        }
#endif
    }
}
