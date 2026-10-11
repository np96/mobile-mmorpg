using System;
using SessionRpg.Core;
using SessionRpg.Editor.World;
using SessionRpg.Unity.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Authoring
{
    internal readonly struct WorldChunkAuthoringEntry
    {
        public WorldChunkAuthoringEntry(
            WorldChunkCoordinate coordinate,
            string scenePath,
            int seed,
            string sourceFingerprint,
            string biomeRecipeId,
            string[] pointOfInterestRecipeIds)
        {
            Coordinate = coordinate;
            ScenePath = scenePath ?? string.Empty;
            Seed = seed;
            SourceFingerprint = sourceFingerprint ?? string.Empty;
            BiomeRecipeId = biomeRecipeId ?? string.Empty;
            PointOfInterestRecipeIds = pointOfInterestRecipeIds ?? Array.Empty<string>();
        }

        public WorldChunkCoordinate Coordinate { get; }

        public string ScenePath { get; }

        public int Seed { get; }

        public string SourceFingerprint { get; }

        public string BiomeRecipeId { get; }

        public string[] PointOfInterestRecipeIds { get; }
    }

    internal readonly struct WorldZoneAuthoringEntry
    {
        public WorldZoneAuthoringEntry(
            uint worldId,
            uint zoneId,
            string zoneKey,
            float chunkSizeMeters,
            int loadRadius,
            int retainRadius,
            int maximumLoadedChunks,
            WorldChunkAuthoringEntry[] chunks)
        {
            WorldId = worldId;
            ZoneId = zoneId;
            ZoneKey = zoneKey ?? string.Empty;
            ChunkSizeMeters = chunkSizeMeters;
            LoadRadius = loadRadius;
            RetainRadius = retainRadius;
            MaximumLoadedChunks = maximumLoadedChunks;
            Chunks = chunks ?? Array.Empty<WorldChunkAuthoringEntry>();
        }

        public uint WorldId { get; }

        public uint ZoneId { get; }

        public string ZoneKey { get; }

        public float ChunkSizeMeters { get; }

        public int LoadRadius { get; }

        public int RetainRadius { get; }

        public int MaximumLoadedChunks { get; }

        public WorldChunkAuthoringEntry[] Chunks { get; }
    }

    /// <summary>
    /// Generates presentation-only additive scenes for manifest-declared world
    /// chunks. Authoritative collision and encounters remain in Core/Application.
    /// </summary>
    internal static class WorldChunkAuthoring
    {
        public const string CatalogPath =
            "Assets/SessionRpg/Settings/WorldZoneCatalog.asset";

        private const float SourceSegmentLength = 56f;
        internal const int ChunkPresentationVersion = 3;

        public static WorldZoneCatalogAsset BuildCatalog(
            AuthoringAssets assets,
            WorldZoneAuthoringEntry[] zones)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            if (zones == null || zones.Length == 0)
            {
                throw new ArgumentException(
                    "World authoring requires at least one validated zone.",
                    nameof(zones));
            }

            if (zones.Length > WorldZoneCatalog.MaximumZoneCount)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(zones),
                    "World authoring exceeds the bounded zone catalog ceiling.");
            }

            var catalogZones = new WorldZoneEntry[zones.Length];
            for (int zoneIndex = 0; zoneIndex < zones.Length; zoneIndex++)
            {
                WorldZoneAuthoringEntry zoneDefinition = zones[zoneIndex];
                if (zoneDefinition.WorldId == 0u ||
                    zoneDefinition.ZoneId == 0u)
                {
                    throw new ArgumentException(
                        "World and zone IDs must be explicit non-zero manifest values.");
                }

                if (zoneDefinition.Chunks.Length == 0)
                {
                    throw new ArgumentException(
                        "World zone '" + zoneDefinition.ZoneKey +
                        "' requires at least one manifest chunk.",
                        nameof(zones));
                }

                var catalogChunks =
                    new WorldZoneChunkEntry[zoneDefinition.Chunks.Length];
                for (int chunkIndex = 0;
                    chunkIndex < zoneDefinition.Chunks.Length;
                    chunkIndex++)
                {
                    WorldChunkAuthoringEntry chunkDefinition =
                        zoneDefinition.Chunks[chunkIndex];
                    BuildChunkScene(
                        assets,
                        zoneDefinition.WorldId,
                        zoneDefinition.ZoneId,
                        zoneDefinition.ZoneKey,
                        zoneDefinition.ChunkSizeMeters,
                        chunkDefinition);

                    var catalogChunk = new WorldZoneChunkEntry();
                    catalogChunk.ConfigureForAuthoring(
                        chunkDefinition.Coordinate,
                        chunkDefinition.ScenePath,
                        chunkDefinition.SourceFingerprint);
                    catalogChunks[chunkIndex] = catalogChunk;
                }

                var catalogZone = new WorldZoneEntry();
                catalogZone.ConfigureForAuthoring(
                    zoneDefinition.WorldId,
                    zoneDefinition.ZoneId,
                    zoneDefinition.ZoneKey,
                    zoneDefinition.ChunkSizeMeters,
                    zoneDefinition.LoadRadius,
                    zoneDefinition.RetainRadius,
                    zoneDefinition.MaximumLoadedChunks,
                    catalogChunks);
                catalogZones[zoneIndex] = catalogZone;
            }

            WorldZoneCatalogAsset catalog =
                AssetDatabase.LoadAssetAtPath<WorldZoneCatalogAsset>(CatalogPath);
            if (catalog == null)
            {
                EnsureAssetFolder(System.IO.Path.GetDirectoryName(CatalogPath));
                catalog = ScriptableObject.CreateInstance<WorldZoneCatalogAsset>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            catalog.ConfigureForAuthoring(catalogZones);
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static void BuildChunkScene(
            AuthoringAssets assets,
            uint worldId,
            uint zoneId,
            string zoneKey,
            float chunkSizeMeters,
            WorldChunkAuthoringEntry definition)
        {
            ValidateChunkRecipes(definition);
            string presentationName = GetPresentationName(zoneKey);
            EnsureAssetFolder(System.IO.Path.GetDirectoryName(definition.ScenePath));
            Scene scene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Single);
            scene.name = presentationName + "_" + definition.Coordinate.X + "_" +
                definition.Coordinate.Z;

            GameObject chunkRoot = new GameObject(
                "WorldChunk_" + presentationName + "_" +
                definition.Coordinate.X + "_" + definition.Coordinate.Z,
                typeof(WorldChunkPresentation));
            chunkRoot.transform.position = new Vector3(
                definition.Coordinate.X * chunkSizeMeters,
                0f,
                definition.Coordinate.Z * chunkSizeMeters);

            GameObject visuals;
            if (definition.BiomeRecipeId == "sunbreak_basin")
            {
                visuals = new GameObject("SunbreakBasin");
                visuals.transform.SetParent(chunkRoot.transform, false);
                SunbreakBasinAuthoring.Build(visuals.transform, assets, definition);
            }
            else
            {
                visuals = BuildBiomeVisuals(assets, definition.BiomeRecipeId);
                visuals.transform.SetParent(chunkRoot.transform, false);
                visuals.transform.localPosition = new Vector3(
                    chunkSizeMeters * 0.5f,
                    0f,
                    chunkSizeMeters * 0.5f);
                visuals.transform.localScale = new Vector3(
                    chunkSizeMeters / SourceSegmentLength,
                    1f,
                    1f);

                ApplyChunkRecipe(
                    visuals,
                    definition.Seed,
                    definition.PointOfInterestRecipeIds);
                WorldPavingDetailAuthoring.Build(visuals.transform, assets, definition.Seed);
                WorldRegionDetailAuthoring.Build(visuals.transform, assets, definition.Seed,
                    definition.PointOfInterestRecipeIds);
            }

            EnvironmentAuthoring.EnforceLevelingStaticTriangleBudget(visuals);

            WorldChunkPresentation presentation =
                chunkRoot.GetComponent<WorldChunkPresentation>();
            presentation.ConfigureForAuthoring(
                worldId,
                zoneId,
                definition.Coordinate,
                visuals.transform,
                definition.SourceFingerprint,
                ChunkPresentationVersion);

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, definition.ScenePath))
            {
                throw new InvalidOperationException(
                    "Unity could not save generated world chunk '" +
                    definition.ScenePath + "'.");
            }
        }

        private static void ValidateChunkRecipes(WorldChunkAuthoringEntry definition)
        {
            if (WorldBiomeRecipeCatalog.GetSupport(definition.BiomeRecipeId) !=
                WorldBiomeRecipeSupport.Authored)
            {
                throw new InvalidOperationException(
                    "World authoring has no implemented biome recipe '" +
                    definition.BiomeRecipeId + "'.");
            }

            foreach (string recipeId in definition.PointOfInterestRecipeIds)
            {
                if (!WorldPoiRecipeCatalog.IsSupportedForBiome(
                    definition.BiomeRecipeId, recipeId))
                {
                    throw new InvalidOperationException(
                        "World authoring POI recipe '" + recipeId +
                        "' is unsupported for biome '" + definition.BiomeRecipeId + "'.");
                }
            }

            if (definition.BiomeRecipeId == "sunbreak_basin" &&
                definition.PointOfInterestRecipeIds.Length != 1)
            {
                throw new InvalidOperationException(
                    "Sunbreak Basin chunks require exactly one " +
                    "sunbreak_basin_clearing POI recipe.");
            }
        }

        private static GameObject BuildBiomeVisuals(
            AuthoringAssets assets,
            string biomeRecipeId)
        {
            if (WorldBiomeRecipeCatalog.GetSupport(biomeRecipeId) !=
                WorldBiomeRecipeSupport.Authored ||
                !string.Equals(
                    biomeRecipeId,
                    "ashen_causeway_volcanic",
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "World authoring has no implemented biome recipe '" +
                    biomeRecipeId + "'.");
            }

            return EnvironmentAuthoring.BuildLeveling(
                assets.FortressSourceArt,
                assets.AshenCausewayStone,
                assets.AshenCausewayStoneVariation,
                assets.AshenCausewayGrout,
                assets.AshenCausewayCinder,
                assets.AshenCausewaySoot,
                assets.AshenCausewayMasonry,
                assets.AshenCausewayLavaCrust,
                assets.AshenCausewayLavaCore,
                assets.AshenCausewayDistantHaze,
                assets.FloorAccent,
                assets.CombatParticle);
        }

        private static void ApplyChunkRecipe(
            GameObject visuals,
            int seed,
            string[] pointOfInterestRecipeIds)
        {
            if (pointOfInterestRecipeIds == null)
            {
                throw new ArgumentNullException(nameof(pointOfInterestRecipeIds));
            }

            for (int recipeIndex = 0;
                recipeIndex < pointOfInterestRecipeIds.Length;
                recipeIndex++)
            {
                string recipeId = pointOfInterestRecipeIds[recipeIndex];
                if (!WorldPoiRecipeCatalog.IsSupportedForBiome(
                    "ashen_causeway_volcanic", recipeId))
                {
                    throw new InvalidOperationException(
                        "World authoring has no implemented POI recipe '" +
                        recipeId + "'.");
                }
            }

            Transform landmarks = visuals.transform.Find("AshenCausewayLandmarks");
            if (landmarks != null)
            {
                RetainLandmark(
                    landmarks,
                    "WestRefuge",
                    ContainsRecipe(pointOfInterestRecipeIds, "west_refuge"));
                RetainLandmark(
                    landmarks,
                    "CinderApproach",
                    ContainsRecipe(pointOfInterestRecipeIds, "cinder_approach"));
                RetainLandmark(
                    landmarks,
                    "BrokenGate",
                    ContainsRecipe(pointOfInterestRecipeIds, "broken_gate"));
                RetainLandmark(
                    landmarks,
                    "CollapsedCourt",
                    ContainsRecipe(pointOfInterestRecipeIds, "collapsed_court"));
                RetainLandmark(
                    landmarks,
                    "OssuaryBend",
                    ContainsRecipe(pointOfInterestRecipeIds, "ossuary_bend"));
                RetainLandmark(
                    landmarks,
                    "ReliquaryYard",
                    ContainsRecipe(pointOfInterestRecipeIds, "reliquary_yard"));
                RetainLandmark(
                    landmarks,
                    "EastWatch",
                    ContainsRecipe(pointOfInterestRecipeIds, "east_watch"));
            }

            // At most one unshadowed local key is retained per chunk. Emissive
            // brazier meshes remain visible when their point light is disabled.
            Light[] pointLights = visuals.GetComponentsInChildren<Light>(true);
            bool retainedLight = false;
            for (int index = 0; index < pointLights.Length; index++)
            {
                Light light = pointLights[index];
                if (light.type != LightType.Point)
                {
                    continue;
                }

                light.shadows = LightShadows.None;
                light.enabled = !retainedLight;
                retainedLight |= light.enabled;
            }

            ParticleSystem[] particles =
                visuals.GetComponentsInChildren<ParticleSystem>(true);
            for (int index = 0; index < particles.Length; index++)
            {
                particles[index].useAutoRandomSeed = false;
                particles[index].randomSeed = unchecked(
                    (uint)(seed + (index * 486187739)));
                ParticleSystem.MainModule main = particles[index].main;
                main.maxParticles = Mathf.Min(main.maxParticles, 32);
            }
        }

        private static bool ContainsRecipe(string[] recipes, string expected)
        {
            if (recipes == null)
            {
                return false;
            }

            for (int index = 0; index < recipes.Length; index++)
            {
                if (string.Equals(recipes[index], expected, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string GetPresentationName(string zoneKey)
        {
            if (string.IsNullOrEmpty(zoneKey))
            {
                throw new ArgumentException(
                    "A world zone requires a stable non-empty zone key.",
                    nameof(zoneKey));
            }

            var name = new System.Text.StringBuilder(zoneKey.Length);
            bool capitalize = true;
            for (int index = 0; index < zoneKey.Length; index++)
            {
                char value = zoneKey[index];
                if (value == '_')
                {
                    capitalize = true;
                    continue;
                }

                name.Append(capitalize
                    ? char.ToUpperInvariant(value)
                    : value);
                capitalize = false;
            }

            return name.ToString();
        }

        private static void RetainLandmark(
            Transform landmarks,
            string name,
            bool retain)
        {
            Transform landmark = landmarks.Find(name);
            if (landmark == null || retain)
            {
                return;
            }

            // Keep the stable named authoring anchor while stripping unused
            // geometry from this chunk scene.
            for (int child = landmark.childCount - 1; child >= 0; child--)
            {
                UnityEngine.Object.DestroyImmediate(landmark.GetChild(child).gameObject);
            }
        }

        private static void EnsureAssetFolder(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(folderPath)
                ?.Replace('\\', '/');
            if (string.IsNullOrEmpty(parent))
            {
                throw new InvalidOperationException(
                    "Could not resolve parent folder for '" + folderPath + "'.");
            }

            EnsureAssetFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(folderPath));
        }
    }
}
