using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Editor.World
{
    public sealed class WorldZoneManifestException : Exception
    {
        public WorldZoneManifestException(string message)
            : base(message)
        {
        }

        public WorldZoneManifestException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public enum WorldPoiRecipeSupport
    {
        Unknown = 0,
        MetadataOnly = 1,
        Authored = 2,
    }

    public enum WorldBiomeRecipeSupport
    {
        Unknown = 0,
        MetadataOnly = 1,
        Authored = 2,
    }

    /// <summary>
    /// Explicit biome recipe registry. New manifest tokens remain unsupported
    /// until a matching authoring and validation recipe is implemented.
    /// </summary>
    public static class WorldBiomeRecipeCatalog
    {
        public static WorldBiomeRecipeSupport GetSupport(string recipeId)
        {
            switch (recipeId)
            {
                case "ashen_causeway_volcanic":
                case "sunbreak_basin":
                    return WorldBiomeRecipeSupport.Authored;
                default:
                    return WorldBiomeRecipeSupport.Unknown;
            }
        }
    }

    /// <summary>
    /// Explicit recipe registry used by the current world generator. Every token
    /// accepted by the current manifest is authored; MetadataOnly remains reserved
    /// for a future schema that can safely declare a stable identity without
    /// claiming bespoke geometry.
    /// </summary>
    public static class WorldPoiRecipeCatalog
    {
        public static WorldPoiRecipeSupport GetSupport(string recipeId)
        {
            switch (recipeId)
            {
                case "west_refuge":
                case "cinder_approach":
                case "broken_gate":
                case "collapsed_court":
                case "ossuary_bend":
                case "reliquary_yard":
                case "east_watch":
                case "sunbreak_basin_clearing":
                    return WorldPoiRecipeSupport.Authored;
                default:
                    return WorldPoiRecipeSupport.Unknown;
            }
        }

        public static bool IsSupportedForBiome(string biomeRecipeId, string recipeId)
        {
            if (GetSupport(recipeId) != WorldPoiRecipeSupport.Authored)
            {
                return false;
            }

            switch (biomeRecipeId)
            {
                case "ashen_causeway_volcanic":
                    return recipeId == "west_refuge" ||
                        recipeId == "cinder_approach" ||
                        recipeId == "broken_gate" ||
                        recipeId == "collapsed_court" ||
                        recipeId == "ossuary_bend" ||
                        recipeId == "reliquary_yard" ||
                        recipeId == "east_watch";
                case "sunbreak_basin":
                    return recipeId == "sunbreak_basin_clearing";
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Fail-closed bridge between schema-valid manifest tokens and recipes that
    /// the current source authoring pipeline can actually build and validate.
    /// </summary>
    public static class WorldZoneRecipeValidator
    {
        public static void ValidateOrThrow(WorldZoneManifest zone)
        {
            if (zone == null)
            {
                throw new ArgumentNullException(nameof(zone));
            }

            for (int chunkIndex = 0;
                chunkIndex < zone.ChunkCount;
                chunkIndex++)
            {
                WorldZoneChunkDefinition chunk = zone.GetChunk(chunkIndex);
                if (WorldBiomeRecipeCatalog.GetSupport(chunk.BiomeRecipeId) !=
                    WorldBiomeRecipeSupport.Authored)
                {
                    throw new WorldZoneManifestException(
                        "World zone '" + zone.ZoneKey + "' chunk " +
                        chunk.Coordinate + " requests unsupported biome recipe '" +
                        chunk.BiomeRecipeId + "'.");
                }

                for (int recipeIndex = 0;
                    recipeIndex < chunk.PoiRecipeCount;
                    recipeIndex++)
                {
                    string recipeId = chunk.GetPoiRecipeId(recipeIndex);
                    if (WorldPoiRecipeCatalog.GetSupport(recipeId) !=
                        WorldPoiRecipeSupport.Authored)
                    {
                        throw new WorldZoneManifestException(
                            "World zone '" + zone.ZoneKey + "' chunk " +
                            chunk.Coordinate + " requests unsupported POI recipe '" +
                            recipeId + "'.");
                    }

                    if (!WorldPoiRecipeCatalog.IsSupportedForBiome(
                        chunk.BiomeRecipeId, recipeId))
                    {
                        throw new WorldZoneManifestException(
                            "World zone '" + zone.ZoneKey + "' chunk " +
                            chunk.Coordinate + " requests POI recipe '" + recipeId +
                            "' unsupported for biome '" + chunk.BiomeRecipeId + "'.");
                    }
                }

                if (chunk.BiomeRecipeId == "sunbreak_basin" &&
                    chunk.PoiRecipeCount != 1)
                {
                    throw new WorldZoneManifestException(
                        "Sunbreak Basin chunks require exactly one " +
                        "sunbreak_basin_clearing POI recipe.");
                }
            }
        }
    }

    public sealed class WorldZoneCatalogEntry
    {
        internal WorldZoneCatalogEntry(
            uint worldId,
            uint zoneId,
            string zoneKey,
            string definitionPath)
        {
            WorldId = worldId;
            ZoneId = zoneId;
            ZoneKey = zoneKey;
            DefinitionPath = definitionPath;
        }

        public uint WorldId { get; }

        public uint ZoneId { get; }

        public string ZoneKey { get; }

        public string DefinitionPath { get; }
    }

    /// <summary>
    /// Strict Editor-only catalog of stable world and zone identities. The catalog
    /// points at per-zone content manifests; it does not load scenes or own runtime
    /// simulation state.
    /// </summary>
    public sealed class WorldZoneCatalog
    {
        public const int CurrentSchemaVersion = 1;
        public const int MaximumZoneCount = 64;
        public const string DefaultProjectRelativePath =
            "Tools/Agent/world-zones.json";

        private const string DefinitionRoot = "Tools/Agent/Worlds/";

        private readonly WorldZoneCatalogEntry[] _zones;
        private readonly string _sourceName;

        private WorldZoneCatalog(
            WorldZoneCatalogEntry[] zones,
            string sourceName)
        {
            _zones = zones;
            _sourceName = sourceName;
        }

        public int SchemaVersion => CurrentSchemaVersion;

        public int Count => _zones.Length;

        public IReadOnlyList<WorldZoneCatalogEntry> Zones => _zones;

        public WorldZoneCatalogEntry GetZone(int index)
        {
            if (index < 0 || index >= _zones.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _zones[index];
        }

        public bool TryGetZone(
            uint worldId,
            uint zoneId,
            out WorldZoneCatalogEntry entry)
        {
            for (int index = 0; index < _zones.Length; index++)
            {
                WorldZoneCatalogEntry candidate = _zones[index];
                if (candidate.WorldId == worldId && candidate.ZoneId == zoneId)
                {
                    entry = candidate;
                    return true;
                }
            }

            entry = null;
            return false;
        }

        public WorldZoneManifest LoadZone(int index)
        {
            WorldZoneCatalogEntry entry = GetZone(index);
            WorldZoneManifest manifest = WorldZoneManifest.LoadProjectRelative(
                entry.DefinitionPath);
            if (manifest.WorldId != entry.WorldId ||
                manifest.ZoneId != entry.ZoneId ||
                !string.Equals(
                    manifest.ZoneKey,
                    entry.ZoneKey,
                    StringComparison.Ordinal))
            {
                throw new WorldZoneManifestException(
                    "World zone catalog '" + _sourceName +
                    "': zone entry '" + entry.ZoneKey + "' identifies world " +
                    entry.WorldId + ", zone " + entry.ZoneId + ", but '" +
                    entry.DefinitionPath + "' identifies world " + manifest.WorldId +
                    ", zone " + manifest.ZoneId + " ('" + manifest.ZoneKey + "').");
            }

            WorldZoneRecipeValidator.ValidateOrThrow(manifest);
            return manifest;
        }

        /// <summary>
        /// Loads the complete sorted catalog and validates constraints that span
        /// zone files before any scene or runtime catalog authoring begins.
        /// </summary>
        public WorldZoneManifest[] LoadValidatedZones()
        {
            var zones = new WorldZoneManifest[_zones.Length];
            for (int index = 0; index < zones.Length; index++)
            {
                zones[index] = LoadZone(index);
            }

            ValidateLoadedZones(zones);
            return zones;
        }

        public void ValidateLoadedZones(WorldZoneManifest[] zones)
        {
            if (zones == null)
            {
                throw new ArgumentNullException(nameof(zones));
            }

            if (zones.Length != _zones.Length)
            {
                throw WorldManifestValidation.CatalogError(
                    _sourceName,
                    "loaded zone count " + zones.Length +
                    " does not match catalog count " + _zones.Length + ".");
            }

            var scenePaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            for (int zoneIndex = 0; zoneIndex < zones.Length; zoneIndex++)
            {
                WorldZoneManifest zone = zones[zoneIndex];
                if (zone == null)
                {
                    throw WorldManifestValidation.CatalogEntryError(
                        _sourceName,
                        zoneIndex,
                        "loaded manifest is null.");
                }

                ValidateIdentity(GetZone(zoneIndex), zone, zoneIndex);
                WorldZoneRecipeValidator.ValidateOrThrow(zone);
                for (int chunkIndex = 0;
                    chunkIndex < zone.ChunkCount;
                    chunkIndex++)
                {
                    string scenePath = zone.GetChunk(chunkIndex).ScenePath;
                    if (!scenePaths.Add(scenePath))
                    {
                        throw WorldManifestValidation.CatalogEntryError(
                            _sourceName,
                            zoneIndex,
                            "chunk scenePath '" + scenePath +
                            "' is already used by another zone, including a " +
                            "case-only difference.");
                    }
                }
            }
        }

        public static WorldZoneCatalog LoadDefault()
        {
            return LoadProjectRelative(DefaultProjectRelativePath);
        }

        public static WorldZoneCatalog LoadProjectRelative(
            string projectRelativePath)
        {
            string absolutePath = ResolveProjectRelativePath(projectRelativePath);
            return Parse(
                WorldManifestPath.ReadAllText(
                    absolutePath,
                    projectRelativePath,
                    "World zone catalog"),
                projectRelativePath);
        }

        public static string ResolveProjectRelativePath(
            string projectRelativePath)
        {
            return WorldManifestPath.ResolveProjectRelativeJson(
                projectRelativePath,
                "World zone catalog");
        }

        public static WorldZoneCatalog Parse(
            string json,
            string sourceName = "<memory>")
        {
            string source = WorldManifestValidation.NormalizeSource(sourceName);
            StrictWorldJson.ValidateCatalog(json, source);

            CatalogJson document;
            try
            {
                document = JsonUtility.FromJson<CatalogJson>(json);
            }
            catch (Exception exception)
            {
                throw new WorldZoneManifestException(
                    "World zone catalog '" + source +
                    "' could not be deserialized. " + exception.Message,
                    exception);
            }

            if (document == null)
            {
                throw WorldManifestValidation.CatalogError(
                    source,
                    "could not be parsed as a JSON object.");
            }

            if (document.schemaVersion != CurrentSchemaVersion)
            {
                throw WorldManifestValidation.CatalogError(
                    source,
                    "schemaVersion must be " + CurrentSchemaVersion +
                    ", but was " + document.schemaVersion + ".");
            }

            if (document.zones == null || document.zones.Length == 0)
            {
                throw WorldManifestValidation.CatalogError(
                    source,
                    "zones must contain at least one zone entry.");
            }

            if (document.zones.Length > MaximumZoneCount)
            {
                throw WorldManifestValidation.CatalogError(
                    source,
                    "zones must not exceed the bounded catalog ceiling of " +
                    MaximumZoneCount + ", but contained " +
                    document.zones.Length + ".");
            }

            var identities = new HashSet<ulong>();
            var zoneKeys = new HashSet<string>(StringComparer.Ordinal);
            var definitionPaths = new HashSet<string>(
                StringComparer.OrdinalIgnoreCase);
            var entries = new WorldZoneCatalogEntry[document.zones.Length];
            for (int index = 0; index < document.zones.Length; index++)
            {
                CatalogZoneJson zone = document.zones[index];
                if (zone == null)
                {
                    throw WorldManifestValidation.CatalogEntryError(
                        source,
                        index,
                        "entry must be a JSON object.");
                }

                WorldManifestValidation.RequirePositiveId(
                    zone.worldId,
                    "worldId",
                    source,
                    index,
                    true);
                WorldManifestValidation.RequirePositiveId(
                    zone.zoneId,
                    "zoneId",
                    source,
                    index,
                    true);
                WorldManifestValidation.RequireStableToken(
                    zone.zoneKey,
                    "zoneKey",
                    source,
                    index,
                    true);
                WorldManifestValidation.ValidateProjectPath(
                    zone.definitionPath,
                    DefinitionRoot,
                    "/zone.json",
                    "definitionPath",
                    source,
                    index,
                    true);

                uint worldId = checked((uint)zone.worldId);
                uint zoneId = checked((uint)zone.zoneId);
                ulong identity = ((ulong)worldId << 32) | zoneId;
                if (!identities.Add(identity))
                {
                    throw WorldManifestValidation.CatalogEntryError(
                        source,
                        index,
                        "worldId " + zone.worldId + " and zoneId " + zone.zoneId +
                        " duplicate another zone identity.");
                }

                if (!zoneKeys.Add(zone.zoneKey))
                {
                    throw WorldManifestValidation.CatalogEntryError(
                        source,
                        index,
                        "zoneKey '" + zone.zoneKey + "' is duplicated.");
                }

                if (!definitionPaths.Add(zone.definitionPath))
                {
                    throw WorldManifestValidation.CatalogEntryError(
                        source,
                        index,
                        "definitionPath '" + zone.definitionPath +
                        "' is already used by another entry, including a case-only " +
                        "difference.");
                }

                entries[index] = new WorldZoneCatalogEntry(
                    worldId,
                    zoneId,
                    zone.zoneKey,
                    zone.definitionPath);
            }

            Array.Sort(entries, CompareEntries);
            return new WorldZoneCatalog(entries, source);
        }

        private void ValidateIdentity(
            WorldZoneCatalogEntry entry,
            WorldZoneManifest manifest,
            int index)
        {
            if (manifest.WorldId == entry.WorldId &&
                manifest.ZoneId == entry.ZoneId &&
                string.Equals(
                    manifest.ZoneKey,
                    entry.ZoneKey,
                    StringComparison.Ordinal))
            {
                return;
            }

            throw WorldManifestValidation.CatalogEntryError(
                _sourceName,
                index,
                "entry identifies world " + entry.WorldId + ", zone " +
                entry.ZoneId + " ('" + entry.ZoneKey + "'), but the loaded " +
                "manifest identifies world " + manifest.WorldId + ", zone " +
                manifest.ZoneId + " ('" + manifest.ZoneKey + "').");
        }

        private static int CompareEntries(
            WorldZoneCatalogEntry left,
            WorldZoneCatalogEntry right)
        {
            int worldComparison = left.WorldId.CompareTo(right.WorldId);
            if (worldComparison != 0)
            {
                return worldComparison;
            }

            int zoneComparison = left.ZoneId.CompareTo(right.ZoneId);
            return zoneComparison != 0
                ? zoneComparison
                : string.CompareOrdinal(left.ZoneKey, right.ZoneKey);
        }

        [Serializable]
        private sealed class CatalogJson
        {
            public int schemaVersion;
            public CatalogZoneJson[] zones;
        }

        [Serializable]
        private sealed class CatalogZoneJson
        {
            public int worldId;
            public int zoneId;
            public string zoneKey;
            public string definitionPath;
        }
    }

    public sealed class WorldZoneChunkDefinition
    {
        private readonly string[] _poiRecipeIds;

        internal WorldZoneChunkDefinition(
            WorldChunkCoordinate coordinate,
            string scenePath,
            int generationSeed,
            string biomeRecipeId,
            string[] poiRecipeIds)
        {
            Coordinate = coordinate;
            ScenePath = scenePath;
            GenerationSeed = generationSeed;
            BiomeRecipeId = biomeRecipeId;
            _poiRecipeIds = poiRecipeIds;
        }

        public WorldChunkCoordinate Coordinate { get; }

        public string ScenePath { get; }

        public int GenerationSeed { get; }

        public string BiomeRecipeId { get; }

        public int PoiRecipeCount => _poiRecipeIds.Length;

        public IReadOnlyList<string> PoiRecipeIds => _poiRecipeIds;

        public string GetPoiRecipeId(int index)
        {
            if (index < 0 || index >= _poiRecipeIds.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _poiRecipeIds[index];
        }
    }

    /// <summary>
    /// Immutable, validated Editor representation of a zone's visual streaming
    /// content. Encounter and spawn authority deliberately live outside this file.
    /// </summary>
    public sealed class WorldZoneManifest
    {
        public const int CurrentSchemaVersion = 1;
        public const int RequiredChunkSizeMeters = 64;
        public const int MaximumLoadedChunkBudget = 15;
        public const int MaximumChunkCount = 1024;
        public const int MaximumStreamingRadius = 8;

        private const string SceneRoot =
            "Assets/SessionRpg/Content/Worlds/";
        private const int MaximumAbsoluteCoordinate = 1000000;

        private readonly WorldZoneChunkDefinition[] _chunks;

        private WorldZoneManifest(
            uint worldId,
            uint zoneId,
            string zoneKey,
            string displayName,
            int contentVersion,
            int chunkSizeMeters,
            int loadRadius,
            int retainRadius,
            int maxLoadedChunks,
            WorldZoneChunkDefinition[] chunks)
        {
            WorldId = worldId;
            ZoneId = zoneId;
            ZoneKey = zoneKey;
            DisplayName = displayName;
            ContentVersion = contentVersion;
            ChunkSizeMeters = chunkSizeMeters;
            LoadRadius = loadRadius;
            RetainRadius = retainRadius;
            MaxLoadedChunks = maxLoadedChunks;
            _chunks = chunks;
        }

        public int SchemaVersion => CurrentSchemaVersion;

        public uint WorldId { get; }

        public uint ZoneId { get; }

        public string ZoneKey { get; }

        public string DisplayName { get; }

        public int ContentVersion { get; }

        public int ChunkSizeMeters { get; }

        public int LoadRadius { get; }

        public int RetainRadius { get; }

        public int MaxLoadedChunks { get; }

        public int ChunkCount => _chunks.Length;

        public IReadOnlyList<WorldZoneChunkDefinition> Chunks => _chunks;

        public WorldZoneChunkDefinition GetChunk(int index)
        {
            if (index < 0 || index >= _chunks.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _chunks[index];
        }

        public bool TryGetChunk(
            WorldChunkCoordinate coordinate,
            out WorldZoneChunkDefinition chunk)
        {
            for (int index = 0; index < _chunks.Length; index++)
            {
                WorldZoneChunkDefinition candidate = _chunks[index];
                if (candidate.Coordinate.Equals(coordinate))
                {
                    chunk = candidate;
                    return true;
                }
            }

            chunk = null;
            return false;
        }

        /// <summary>
        /// Fingerprints every source field that can affect a generated chunk or
        /// its zone composition. Validators compare this value with both the
        /// generated runtime catalog and scene marker so stale content fails closed.
        /// </summary>
        public string GetChunkSourceFingerprint(int index)
        {
            WorldZoneChunkDefinition chunk = GetChunk(index);
            var canonical = new StringBuilder(320);
            canonical.Append("session-rpg-world-chunk-v1|");
            canonical.Append(SchemaVersion).Append('|');
            canonical.Append(WorldId).Append('|');
            canonical.Append(ZoneId).Append('|');
            AppendFingerprintText(canonical, ZoneKey);
            AppendFingerprintText(canonical, DisplayName);
            canonical.Append(ContentVersion).Append('|');
            canonical.Append(ChunkSizeMeters).Append('|');
            canonical.Append(LoadRadius).Append('|');
            canonical.Append(RetainRadius).Append('|');
            canonical.Append(MaxLoadedChunks).Append('|');
            canonical.Append(chunk.Coordinate.X).Append('|');
            canonical.Append(chunk.Coordinate.Z).Append('|');
            AppendFingerprintText(canonical, chunk.ScenePath);
            canonical.Append(chunk.GenerationSeed).Append('|');
            AppendFingerprintText(canonical, chunk.BiomeRecipeId);
            canonical.Append(chunk.PoiRecipeCount).Append('|');
            for (int poiIndex = 0;
                 poiIndex < chunk.PoiRecipeCount;
                 poiIndex++)
            {
                AppendFingerprintText(
                    canonical,
                    chunk.GetPoiRecipeId(poiIndex));
            }

            byte[] source = Encoding.UTF8.GetBytes(canonical.ToString());
            byte[] digest;
            using (SHA256 sha = SHA256.Create())
            {
                digest = sha.ComputeHash(source);
            }

            var hex = new StringBuilder(digest.Length * 2);
            for (int digestIndex = 0;
                 digestIndex < digest.Length;
                 digestIndex++)
            {
                hex.Append(digest[digestIndex].ToString("x2"));
            }

            return hex.ToString();
        }

        public static WorldZoneManifest LoadProjectRelative(
            string projectRelativePath)
        {
            string absolutePath = ResolveProjectRelativePath(projectRelativePath);
            return Parse(
                WorldManifestPath.ReadAllText(
                    absolutePath,
                    projectRelativePath,
                    "World zone manifest"),
                projectRelativePath);
        }

        public static string ResolveProjectRelativePath(
            string projectRelativePath)
        {
            return WorldManifestPath.ResolveProjectRelativeJson(
                projectRelativePath,
                "World zone manifest");
        }

        public static WorldZoneManifest Parse(
            string json,
            string sourceName = "<memory>")
        {
            string source = WorldManifestValidation.NormalizeSource(sourceName);
            StrictWorldJson.ValidateZone(json, source);

            ZoneJson document;
            try
            {
                document = JsonUtility.FromJson<ZoneJson>(json);
            }
            catch (Exception exception)
            {
                throw new WorldZoneManifestException(
                    "World zone manifest '" + source +
                    "' could not be deserialized. " + exception.Message,
                    exception);
            }

            if (document == null)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "could not be parsed as a JSON object.");
            }

            if (document.schemaVersion != CurrentSchemaVersion)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "schemaVersion must be " + CurrentSchemaVersion +
                    ", but was " + document.schemaVersion + ".");
            }

            WorldManifestValidation.RequirePositiveId(
                document.worldId,
                "worldId",
                source,
                -1,
                false);
            WorldManifestValidation.RequirePositiveId(
                document.zoneId,
                "zoneId",
                source,
                -1,
                false);
            WorldManifestValidation.RequireStableToken(
                document.zoneKey,
                "zoneKey",
                source,
                -1,
                false);
            WorldManifestValidation.RequireTrimmedText(
                document.displayName,
                "displayName",
                source,
                -1,
                false);

            if (document.contentVersion < 1)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "contentVersion must be at least 1, but was " +
                    document.contentVersion + ".");
            }

            if (document.chunkSizeMeters != RequiredChunkSizeMeters)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "chunkSizeMeters must be exactly " + RequiredChunkSizeMeters +
                    " to match the Core world-coordinate contract, but was " +
                    document.chunkSizeMeters + ".");
            }

            if (document.loadRadius < 0)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "loadRadius cannot be negative, but was " +
                    document.loadRadius + ".");
            }

            if (document.retainRadius <= document.loadRadius)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "retainRadius must be greater than loadRadius to provide " +
                    "streaming hysteresis, but values were " + document.retainRadius +
                    " and " + document.loadRadius + ".");
            }

            if (document.loadRadius > MaximumStreamingRadius ||
                document.retainRadius > MaximumStreamingRadius)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "loadRadius and retainRadius must not exceed " +
                    MaximumStreamingRadius + "; partition larger residency " +
                    "requirements into bounded zones or regions.");
            }

            if (document.maxLoadedChunks < 1 ||
                document.maxLoadedChunks > MaximumLoadedChunkBudget)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "maxLoadedChunks must be between 1 and " +
                    MaximumLoadedChunkBudget + ", but was " +
                    document.maxLoadedChunks + ".");
            }

            if (document.chunks == null || document.chunks.Length == 0)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "chunks must contain at least one visual chunk.");
            }

            if (document.chunks.Length > MaximumChunkCount)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "chunks must not exceed the bounded per-zone ceiling of " +
                    MaximumChunkCount + "; partition larger worlds into zones " +
                    "or indexed regions.");
            }

            if (document.maxLoadedChunks > document.chunks.Length)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "maxLoadedChunks cannot exceed the authored chunk count " +
                    document.chunks.Length + ", but was " +
                    document.maxLoadedChunks + ".");
            }

            var coordinates = new HashSet<WorldChunkCoordinate>();
            var scenePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var generationSeeds = new HashSet<int>();
            var chunks = new WorldZoneChunkDefinition[document.chunks.Length];
            bool containsOrigin = false;
            for (int index = 0; index < document.chunks.Length; index++)
            {
                ZoneChunkJson sourceChunk = document.chunks[index];
                if (sourceChunk == null)
                {
                    throw WorldManifestValidation.ChunkError(
                        source,
                        index,
                        "entry must be a JSON object.");
                }

                if (sourceChunk.x < -MaximumAbsoluteCoordinate ||
                    sourceChunk.x > MaximumAbsoluteCoordinate ||
                    sourceChunk.z < -MaximumAbsoluteCoordinate ||
                    sourceChunk.z > MaximumAbsoluteCoordinate)
                {
                    throw WorldManifestValidation.ChunkError(
                        source,
                        index,
                        "x and z must stay within +/-" + MaximumAbsoluteCoordinate +
                        " to keep distance arithmetic bounded.");
                }

                var coordinate = new WorldChunkCoordinate(
                    sourceChunk.x,
                    sourceChunk.z);
                if (!coordinates.Add(coordinate))
                {
                    throw WorldManifestValidation.ChunkError(
                        source,
                        index,
                        "coordinate (" + sourceChunk.x + ", " + sourceChunk.z +
                        ") is duplicated.");
                }

                containsOrigin |= sourceChunk.x == 0 && sourceChunk.z == 0;
                ValidateScenePath(
                    sourceChunk.scenePath,
                    sourceChunk.x,
                    sourceChunk.z,
                    source,
                    index);
                if (!scenePaths.Add(sourceChunk.scenePath))
                {
                    throw WorldManifestValidation.ChunkError(
                        source,
                        index,
                        "scenePath '" + sourceChunk.scenePath +
                        "' is already used by another chunk, including a case-only " +
                        "difference.");
                }

                if (sourceChunk.generationSeed <= 0)
                {
                    throw WorldManifestValidation.ChunkError(
                        source,
                        index,
                        "generationSeed must be a positive non-zero integer, but was " +
                        sourceChunk.generationSeed + ".");
                }

                if (!generationSeeds.Add(sourceChunk.generationSeed))
                {
                    throw WorldManifestValidation.ChunkError(
                        source,
                        index,
                        "generationSeed " + sourceChunk.generationSeed +
                        " is duplicated; each chunk needs a stable distinct seed.");
                }

                WorldManifestValidation.RequireStableToken(
                    sourceChunk.biomeRecipeId,
                    "biomeRecipeId",
                    source,
                    index,
                    false,
                    true);
                string[] poiRecipeIds = ValidatePoiRecipeIds(
                    sourceChunk.poiRecipeIds,
                    source,
                    index);

                chunks[index] = new WorldZoneChunkDefinition(
                    coordinate,
                    sourceChunk.scenePath,
                    sourceChunk.generationSeed,
                    sourceChunk.biomeRecipeId,
                    poiRecipeIds);
            }

            if (!containsOrigin)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "chunks must contain coordinate (0, 0) as the stable local " +
                    "zone anchor.");
            }

            ValidateConnectedFootprint(coordinates, source);
            ValidateMustLoadBudget(
                coordinates,
                document.loadRadius,
                document.maxLoadedChunks,
                source);

            Array.Sort(chunks, CompareChunks);
            return new WorldZoneManifest(
                checked((uint)document.worldId),
                checked((uint)document.zoneId),
                document.zoneKey,
                document.displayName,
                document.contentVersion,
                document.chunkSizeMeters,
                document.loadRadius,
                document.retainRadius,
                document.maxLoadedChunks,
                chunks);
        }

        private static string[] ValidatePoiRecipeIds(
            string[] values,
            string source,
            int chunkIndex)
        {
            if (values == null)
            {
                throw WorldManifestValidation.ChunkError(
                    source,
                    chunkIndex,
                    "poiRecipeIds must be a JSON array; use [] for no POIs.");
            }

            var unique = new HashSet<string>(StringComparer.Ordinal);
            var result = new string[values.Length];
            for (int index = 0; index < values.Length; index++)
            {
                string value = values[index];
                if (!WorldManifestValidation.IsStableToken(value))
                {
                    throw WorldManifestValidation.ChunkError(
                        source,
                        chunkIndex,
                        "poiRecipeIds[" + index + "] '" + (value ?? string.Empty) +
                        "' must be a stable lowercase ASCII token.");
                }

                if (!unique.Add(value))
                {
                    throw WorldManifestValidation.ChunkError(
                        source,
                        chunkIndex,
                        "poiRecipeId '" + value + "' is duplicated.");
                }

                result[index] = value;
            }

            Array.Sort(result, StringComparer.Ordinal);
            return result;
        }

        private static void AppendFingerprintText(
            StringBuilder builder,
            string value)
        {
            string resolved = value ?? string.Empty;
            builder.Append(resolved.Length).Append(':').Append(resolved).Append('|');
        }

        private static void ValidateScenePath(
            string value,
            int x,
            int z,
            string source,
            int index)
        {
            WorldManifestValidation.ValidateProjectPath(
                value,
                SceneRoot,
                ".unity",
                "scenePath",
                source,
                index,
                false,
                true);
            string expectedFileName = "Chunk_" + x + "_" + z + ".unity";
            if (!string.Equals(
                    Path.GetFileName(value),
                    expectedFileName,
                    StringComparison.Ordinal))
            {
                throw WorldManifestValidation.ChunkError(
                    source,
                    index,
                    "scenePath must end with coordinate-derived filename '" +
                    expectedFileName + "', but was '" + value + "'.");
            }
        }

        private static void ValidateConnectedFootprint(
            HashSet<WorldChunkCoordinate> coordinates,
            string source)
        {
            var pending = new Queue<WorldChunkCoordinate>();
            var visited = new HashSet<WorldChunkCoordinate>();
            var origin = new WorldChunkCoordinate(0, 0);
            pending.Enqueue(origin);
            visited.Add(origin);

            while (pending.Count > 0)
            {
                WorldChunkCoordinate coordinate = pending.Dequeue();
                VisitNeighbor(coordinates, visited, pending, coordinate.X - 1, coordinate.Z);
                VisitNeighbor(coordinates, visited, pending, coordinate.X + 1, coordinate.Z);
                VisitNeighbor(coordinates, visited, pending, coordinate.X, coordinate.Z - 1);
                VisitNeighbor(coordinates, visited, pending, coordinate.X, coordinate.Z + 1);
            }

            if (visited.Count != coordinates.Count)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "chunks must form one four-neighbor-connected footprint; " +
                    (coordinates.Count - visited.Count) + " chunk(s) are disconnected.");
            }
        }

        private static void VisitNeighbor(
            HashSet<WorldChunkCoordinate> coordinates,
            HashSet<WorldChunkCoordinate> visited,
            Queue<WorldChunkCoordinate> pending,
            int x,
            int z)
        {
            var neighbor = new WorldChunkCoordinate(x, z);
            if (coordinates.Contains(neighbor) && visited.Add(neighbor))
            {
                pending.Enqueue(neighbor);
            }
        }

        private static void ValidateMustLoadBudget(
            HashSet<WorldChunkCoordinate> coordinates,
            int loadRadius,
            int maxLoadedChunks,
            string source)
        {
            int largestNeighborhood = 0;
            foreach (WorldChunkCoordinate focus in coordinates)
            {
                int count = 0;
                for (int xOffset = -loadRadius;
                     xOffset <= loadRadius;
                     xOffset++)
                {
                    for (int zOffset = -loadRadius;
                         zOffset <= loadRadius;
                         zOffset++)
                    {
                        if (coordinates.Contains(
                                new WorldChunkCoordinate(
                                    focus.X + xOffset,
                                    focus.Z + zOffset)))
                        {
                            count++;
                        }
                    }
                }

                largestNeighborhood = Math.Max(largestNeighborhood, count);
            }

            if (maxLoadedChunks < largestNeighborhood)
            {
                throw WorldManifestValidation.ZoneError(
                    source,
                    "maxLoadedChunks " + maxLoadedChunks +
                    " cannot hold the largest authored loadRadius neighborhood of " +
                    largestNeighborhood + " chunks.");
            }
        }

        private static int CompareChunks(
            WorldZoneChunkDefinition left,
            WorldZoneChunkDefinition right)
        {
            int xComparison = left.Coordinate.X.CompareTo(right.Coordinate.X);
            return xComparison != 0
                ? xComparison
                : left.Coordinate.Z.CompareTo(right.Coordinate.Z);
        }

        [Serializable]
        private sealed class ZoneJson
        {
            public int schemaVersion;
            public int worldId;
            public int zoneId;
            public string zoneKey;
            public string displayName;
            public int contentVersion;
            public int chunkSizeMeters;
            public int loadRadius;
            public int retainRadius;
            public int maxLoadedChunks;
            public ZoneChunkJson[] chunks;
        }

        [Serializable]
        private sealed class ZoneChunkJson
        {
            public int x;
            public int z;
            public string scenePath;
            public int generationSeed;
            public string biomeRecipeId;
            public string[] poiRecipeIds;
        }
    }

    internal static class WorldManifestValidation
    {
        public static string NormalizeSource(string sourceName)
        {
            return string.IsNullOrWhiteSpace(sourceName)
                ? "<memory>"
                : sourceName;
        }

        public static void RequirePositiveId(
            int value,
            string fieldName,
            string source,
            int index,
            bool catalogEntry)
        {
            if (value <= 0)
            {
                throw FieldError(
                    source,
                    index,
                    catalogEntry,
                    false,
                    fieldName + " must be a positive integer, but was " + value + ".");
            }
        }

        public static void RequireStableToken(
            string value,
            string fieldName,
            string source,
            int index,
            bool catalogEntry,
            bool chunkEntry = false)
        {
            if (!IsStableToken(value))
            {
                throw FieldError(
                    source,
                    index,
                    catalogEntry,
                    chunkEntry,
                    fieldName + " '" + (value ?? string.Empty) +
                    "' must be a stable lowercase ASCII token using letters, " +
                    "numbers and underscores, beginning with a letter.");
            }
        }

        public static bool IsStableToken(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value[0] < 'a' ||
                value[0] > 'z')
            {
                return false;
            }

            for (int index = 1; index < value.Length; index++)
            {
                char character = value[index];
                bool valid =
                    (character >= 'a' && character <= 'z') ||
                    (character >= '0' && character <= '9') ||
                    character == '_';
                if (!valid)
                {
                    return false;
                }
            }

            return true;
        }

        public static void RequireTrimmedText(
            string value,
            string fieldName,
            string source,
            int index,
            bool catalogEntry)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw FieldError(
                    source,
                    index,
                    catalogEntry,
                    false,
                    fieldName + " must be non-empty text.");
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw FieldError(
                    source,
                    index,
                    catalogEntry,
                    false,
                    fieldName + " cannot begin or end with whitespace.");
            }
        }

        public static void ValidateProjectPath(
            string value,
            string requiredRoot,
            string requiredSuffix,
            string fieldName,
            string source,
            int index,
            bool catalogEntry,
            bool chunkEntry = false)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                value.IndexOf('\\') >= 0 ||
                Path.IsPathRooted(value) ||
                !value.StartsWith(requiredRoot, StringComparison.Ordinal) ||
                !value.EndsWith(requiredSuffix, StringComparison.Ordinal))
            {
                throw FieldError(
                    source,
                    index,
                    catalogEntry,
                    chunkEntry,
                    fieldName + " '" + (value ?? string.Empty) +
                    "' must be a normalized project-relative path below '" +
                    requiredRoot + "' ending in '" + requiredSuffix +
                    "' and using '/'.");
            }

            ValidatePathSegments(
                value,
                FieldLabel(source, index, catalogEntry, chunkEntry, fieldName));
        }

        public static void ValidatePathSegments(string value, string label)
        {
            string[] segments = value.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                if (string.IsNullOrEmpty(segment) ||
                    segment == "." ||
                    segment == "..")
                {
                    throw new WorldZoneManifestException(
                        label + " contains an empty, '.' or '..' path segment.");
                }

                for (int characterIndex = 0;
                    characterIndex < segment.Length;
                    characterIndex++)
                {
                    char character = segment[characterIndex];
                    if (char.IsControl(character) ||
                        character == ':' ||
                        character == '*' ||
                        character == '?' ||
                        character == '"' ||
                        character == '<' ||
                        character == '>' ||
                        character == '|')
                    {
                        throw new WorldZoneManifestException(
                            label + " contains invalid path character '" +
                            character + "'.");
                    }
                }
            }
        }

        public static WorldZoneManifestException CatalogError(
            string source,
            string message)
        {
            return new WorldZoneManifestException(
                "World zone catalog '" + source + "': " + message);
        }

        public static WorldZoneManifestException CatalogEntryError(
            string source,
            int index,
            string message)
        {
            return CatalogError(source, "zones[" + index + "]." + message);
        }

        public static WorldZoneManifestException ZoneError(
            string source,
            string message)
        {
            return new WorldZoneManifestException(
                "World zone manifest '" + source + "': " + message);
        }

        public static WorldZoneManifestException ChunkError(
            string source,
            int index,
            string message)
        {
            return ZoneError(source, "chunks[" + index + "]." + message);
        }

        private static WorldZoneManifestException FieldError(
            string source,
            int index,
            bool catalogEntry,
            bool chunkEntry,
            string message)
        {
            if (catalogEntry)
            {
                return CatalogEntryError(source, index, message);
            }

            return chunkEntry
                ? ChunkError(source, index, message)
                : ZoneError(source, message);
        }

        private static string FieldLabel(
            string source,
            int index,
            bool catalogEntry,
            bool chunkEntry,
            string fieldName)
        {
            if (catalogEntry)
            {
                return "World zone catalog '" + source + "': zones[" + index +
                    "]." + fieldName;
            }

            if (chunkEntry)
            {
                return "World zone manifest '" + source + "': chunks[" + index +
                    "]." + fieldName;
            }

            return "World zone manifest '" + source + "': " + fieldName;
        }
    }

    internal static class WorldManifestPath
    {
        public static string ResolveProjectRelativeJson(
            string projectRelativePath,
            string documentLabel)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath) ||
                !string.Equals(
                    projectRelativePath,
                    projectRelativePath.Trim(),
                    StringComparison.Ordinal) ||
                projectRelativePath.IndexOf('\\') >= 0 ||
                Path.IsPathRooted(projectRelativePath) ||
                !projectRelativePath.EndsWith(
                    ".json",
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new WorldZoneManifestException(
                    documentLabel + " path '" + (projectRelativePath ?? string.Empty) +
                    "' must be a normalized project-relative .json path using '/'.");
            }

            WorldManifestValidation.ValidatePathSegments(
                projectRelativePath,
                documentLabel + " path '" + projectRelativePath + "'");

            string projectRoot = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, ".."));
            string absolutePath = Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    projectRelativePath.Replace(
                        '/',
                        Path.DirectorySeparatorChar)));
            string rootPrefix = projectRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            StringComparison comparison =
                UnityEngine.Application.platform == RuntimePlatform.WindowsEditor
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal;
            if (!absolutePath.StartsWith(rootPrefix, comparison))
            {
                throw new WorldZoneManifestException(
                    documentLabel + " path '" + projectRelativePath +
                    "' resolves outside the Unity project root '" + projectRoot + "'.");
            }

            return absolutePath;
        }

        public static string ReadAllText(
            string absolutePath,
            string projectRelativePath,
            string documentLabel)
        {
            try
            {
                return File.ReadAllText(absolutePath, Encoding.UTF8);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                throw new WorldZoneManifestException(
                    "Could not read " + documentLabel.ToLowerInvariant() + " '" +
                    projectRelativePath + "' at '" + absolutePath + "'. " +
                    exception.Message,
                    exception);
            }
        }
    }

    /// <summary>
    /// JsonUtility intentionally ignores unknown and missing fields. This small
    /// schema reader runs first so manifest typos fail closed while the data model
    /// remains JsonUtility-friendly and dependency-free.
    /// </summary>
    internal static class StrictWorldJson
    {
        private static readonly ObjectContract CatalogContract =
            new ObjectContract(
                new FieldContract("schemaVersion", ValueShape.Integer),
                new FieldContract(
                    "zones",
                    ValueShape.ObjectArray,
                    new ObjectContract(
                        new FieldContract("worldId", ValueShape.Integer),
                        new FieldContract("zoneId", ValueShape.Integer),
                        new FieldContract("zoneKey", ValueShape.String),
                        new FieldContract("definitionPath", ValueShape.String))));

        private static readonly ObjectContract ZoneContract =
            new ObjectContract(
                new FieldContract("schemaVersion", ValueShape.Integer),
                new FieldContract("worldId", ValueShape.Integer),
                new FieldContract("zoneId", ValueShape.Integer),
                new FieldContract("zoneKey", ValueShape.String),
                new FieldContract("displayName", ValueShape.String),
                new FieldContract("contentVersion", ValueShape.Integer),
                new FieldContract("chunkSizeMeters", ValueShape.Integer),
                new FieldContract("loadRadius", ValueShape.Integer),
                new FieldContract("retainRadius", ValueShape.Integer),
                new FieldContract("maxLoadedChunks", ValueShape.Integer),
                new FieldContract(
                    "chunks",
                    ValueShape.ObjectArray,
                    new ObjectContract(
                        new FieldContract("x", ValueShape.Integer),
                        new FieldContract("z", ValueShape.Integer),
                        new FieldContract("scenePath", ValueShape.String),
                        new FieldContract("generationSeed", ValueShape.Integer),
                        new FieldContract("biomeRecipeId", ValueShape.String),
                        new FieldContract(
                            "poiRecipeIds",
                            ValueShape.StringArray))));

        public static void ValidateCatalog(string json, string source)
        {
            Validate(json, source, "World zone catalog", CatalogContract);
        }

        public static void ValidateZone(string json, string source)
        {
            Validate(json, source, "World zone manifest", ZoneContract);
        }

        private static void Validate(
            string json,
            string source,
            string label,
            ObjectContract contract)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                throw new WorldZoneManifestException(
                    label + " '" + source +
                    "': file is empty; expected a schema-1 JSON object.");
            }

            var parser = new Parser(json, source, label);
            parser.Parse(contract);
        }

        private enum ValueShape
        {
            Integer,
            String,
            StringArray,
            ObjectArray,
        }

        private sealed class FieldContract
        {
            public FieldContract(
                string name,
                ValueShape shape,
                ObjectContract elementContract = null)
            {
                Name = name;
                Shape = shape;
                ElementContract = elementContract;
            }

            public string Name { get; }

            public ValueShape Shape { get; }

            public ObjectContract ElementContract { get; }
        }

        private sealed class ObjectContract
        {
            private readonly FieldContract[] _fields;

            public ObjectContract(params FieldContract[] fields)
            {
                _fields = fields;
            }

            public int Count => _fields.Length;

            public FieldContract GetField(int index)
            {
                return _fields[index];
            }

            public bool TryGetField(string name, out FieldContract field)
            {
                for (int index = 0; index < _fields.Length; index++)
                {
                    FieldContract candidate = _fields[index];
                    if (string.Equals(
                            candidate.Name,
                            name,
                            StringComparison.Ordinal))
                    {
                        field = candidate;
                        return true;
                    }
                }

                field = null;
                return false;
            }
        }

        private sealed class Parser
        {
            private readonly string _json;
            private readonly string _source;
            private readonly string _label;
            private int _index;

            public Parser(string json, string source, string label)
            {
                _json = json;
                _source = source;
                _label = label;
            }

            public void Parse(ObjectContract rootContract)
            {
                SkipWhitespace();
                ParseObject(rootContract, "root", 0);
                SkipWhitespace();
                if (_index != _json.Length)
                {
                    throw Error("contains content after the root object.");
                }
            }

            private void ParseObject(
                ObjectContract contract,
                string location,
                int depth)
            {
                if (depth > 16)
                {
                    throw Error(location + " exceeds the supported nesting depth.");
                }

                Expect('{', location + " must be a JSON object.");
                SkipWhitespace();
                var seen = new HashSet<string>(StringComparer.Ordinal);
                if (Consume('}'))
                {
                    ValidateRequiredFields(contract, seen, location);
                    return;
                }

                while (true)
                {
                    if (Peek() != '"')
                    {
                        throw Error(location + " property names must be JSON strings.");
                    }

                    string propertyName = ParseString();
                    if (!seen.Add(propertyName))
                    {
                        throw Error(
                            location + " contains duplicate property '" +
                            propertyName + "'.");
                    }

                    if (!contract.TryGetField(
                            propertyName,
                            out FieldContract field))
                    {
                        throw Error(
                            location + " contains unknown property '" +
                            propertyName + "'.");
                    }

                    SkipWhitespace();
                    Expect(':', location + "." + propertyName + " is missing ':'.");
                    SkipWhitespace();
                    ParseValue(field, location + "." + propertyName, depth + 1);
                    SkipWhitespace();
                    if (Consume('}'))
                    {
                        break;
                    }

                    Expect(',', location + " properties must be separated by ','.");
                    SkipWhitespace();
                    if (Peek() == '}')
                    {
                        throw Error(location + " contains a trailing comma.");
                    }
                }

                ValidateRequiredFields(contract, seen, location);
            }

            private void ParseValue(
                FieldContract field,
                string location,
                int depth)
            {
                switch (field.Shape)
                {
                    case ValueShape.Integer:
                        ParseInteger(location);
                        return;
                    case ValueShape.String:
                        if (Peek() != '"')
                        {
                            throw Error(location + " must be a JSON string.");
                        }

                        ParseString();
                        return;
                    case ValueShape.StringArray:
                        ParseStringArray(location);
                        return;
                    case ValueShape.ObjectArray:
                        ParseObjectArray(field.ElementContract, location, depth);
                        return;
                    default:
                        throw Error(location + " has an unsupported schema shape.");
                }
            }

            private void ParseStringArray(string location)
            {
                Expect('[', location + " must be a JSON array.");
                SkipWhitespace();
                if (Consume(']'))
                {
                    return;
                }

                int itemIndex = 0;
                while (true)
                {
                    if (Peek() != '"')
                    {
                        throw Error(
                            location + "[" + itemIndex + "] must be a JSON string.");
                    }

                    ParseString();
                    itemIndex++;
                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        return;
                    }

                    Expect(',', location + " items must be separated by ','.");
                    SkipWhitespace();
                    if (Peek() == ']')
                    {
                        throw Error(location + " contains a trailing comma.");
                    }
                }
            }

            private void ParseObjectArray(
                ObjectContract elementContract,
                string location,
                int depth)
            {
                Expect('[', location + " must be a JSON array.");
                SkipWhitespace();
                if (Consume(']'))
                {
                    return;
                }

                int itemIndex = 0;
                while (true)
                {
                    ParseObject(
                        elementContract,
                        location + "[" + itemIndex + "]",
                        depth);
                    itemIndex++;
                    SkipWhitespace();
                    if (Consume(']'))
                    {
                        return;
                    }

                    Expect(',', location + " items must be separated by ','.");
                    SkipWhitespace();
                    if (Peek() == ']')
                    {
                        throw Error(location + " contains a trailing comma.");
                    }
                }
            }

            private void ParseInteger(string location)
            {
                int start = _index;
                Consume('-');
                if (_index >= _json.Length ||
                    _json[_index] < '0' ||
                    _json[_index] > '9')
                {
                    throw Error(location + " must be a JSON integer.");
                }

                if (_json[_index] == '0')
                {
                    _index++;
                    if (_index < _json.Length &&
                        _json[_index] >= '0' &&
                        _json[_index] <= '9')
                    {
                        throw Error(location + " contains a leading-zero integer.");
                    }
                }
                else
                {
                    while (_index < _json.Length &&
                        _json[_index] >= '0' &&
                        _json[_index] <= '9')
                    {
                        _index++;
                    }
                }

                if (_index == start ||
                    (_index == start + 1 && _json[start] == '-'))
                {
                    throw Error(location + " must be a JSON integer.");
                }

                if (_index < _json.Length &&
                    (_json[_index] == '.' ||
                     _json[_index] == 'e' ||
                     _json[_index] == 'E'))
                {
                    throw Error(location + " must be an integer, not a decimal.");
                }
            }

            private string ParseString()
            {
                Expect('"', "expected a JSON string.");
                var builder = new StringBuilder();
                while (_index < _json.Length)
                {
                    char character = _json[_index++];
                    if (character == '"')
                    {
                        return builder.ToString();
                    }

                    if (character == '\\')
                    {
                        if (_index >= _json.Length)
                        {
                            break;
                        }

                        char escaped = _json[_index++];
                        switch (escaped)
                        {
                            case '"':
                            case '\\':
                            case '/':
                                builder.Append(escaped);
                                break;
                            case 'b':
                                builder.Append('\b');
                                break;
                            case 'f':
                                builder.Append('\f');
                                break;
                            case 'n':
                                builder.Append('\n');
                                break;
                            case 'r':
                                builder.Append('\r');
                                break;
                            case 't':
                                builder.Append('\t');
                                break;
                            case 'u':
                                builder.Append(ParseUnicodeEscape());
                                break;
                            default:
                                throw Error(
                                    "contains invalid JSON escape '\\" + escaped + "'.");
                        }

                        continue;
                    }

                    if (char.IsControl(character))
                    {
                        throw Error("contains an unescaped control character.");
                    }

                    builder.Append(character);
                }

                throw Error("contains an unterminated JSON string.");
            }

            private char ParseUnicodeEscape()
            {
                if (_index + 4 > _json.Length)
                {
                    throw Error("contains an incomplete Unicode escape.");
                }

                int value = 0;
                for (int offset = 0; offset < 4; offset++)
                {
                    char character = _json[_index + offset];
                    int digit = character >= '0' && character <= '9'
                        ? character - '0'
                        : character >= 'a' && character <= 'f'
                            ? character - 'a' + 10
                            : character >= 'A' && character <= 'F'
                                ? character - 'A' + 10
                                : -1;
                    if (digit < 0)
                    {
                        throw Error("contains an invalid Unicode escape.");
                    }

                    value = (value << 4) | digit;
                }

                _index += 4;
                return (char)value;
            }

            private void ValidateRequiredFields(
                ObjectContract contract,
                HashSet<string> seen,
                string location)
            {
                for (int index = 0; index < contract.Count; index++)
                {
                    string name = contract.GetField(index).Name;
                    if (!seen.Contains(name))
                    {
                        throw Error(
                            location + " is missing required property '" + name + "'.");
                    }
                }
            }

            private void SkipWhitespace()
            {
                while (_index < _json.Length && char.IsWhiteSpace(_json[_index]))
                {
                    _index++;
                }
            }

            private char Peek()
            {
                return _index < _json.Length ? _json[_index] : '\0';
            }

            private bool Consume(char expected)
            {
                if (Peek() != expected)
                {
                    return false;
                }

                _index++;
                return true;
            }

            private void Expect(char expected, string message)
            {
                if (!Consume(expected))
                {
                    throw Error(message);
                }
            }

            private WorldZoneManifestException Error(string message)
            {
                return new WorldZoneManifestException(
                    _label + " '" + _source + "': " + message);
            }
        }
    }
}
