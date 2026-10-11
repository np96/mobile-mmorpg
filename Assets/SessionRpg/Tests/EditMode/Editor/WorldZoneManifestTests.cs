using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Editor.World;
using SessionRpg.Unity.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Tests
{
    public sealed class WorldZoneManifestTests
    {
        [Test]
        public void DefaultCatalog_LoadsValidatedAshenCausewayLongRoute()
        {
            WorldZoneCatalog catalog = WorldZoneCatalog.LoadDefault();
            WorldZoneManifest[] zones = catalog.LoadValidatedZones();
            WorldZoneManifest zone = zones[0];

            Assert.That(catalog.SchemaVersion, Is.EqualTo(1));
            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(zones, Has.Length.EqualTo(2));
            Assert.That(zone.WorldId, Is.EqualTo(1u));
            Assert.That(zone.ZoneId, Is.EqualTo(1u));
            Assert.That(zone.ZoneKey, Is.EqualTo("ashen_causeway"));
            Assert.That(zone.DisplayName, Is.EqualTo("Ashen Causeway"));
            Assert.That(zone.ContentVersion, Is.EqualTo(3));
            Assert.That(zone.ChunkSizeMeters, Is.EqualTo(64));
            Assert.That(zone.LoadRadius, Is.EqualTo(1));
            Assert.That(zone.RetainRadius, Is.EqualTo(2));
            Assert.That(zone.MaxLoadedChunks, Is.EqualTo(7));
            Assert.That(zone.ChunkCount, Is.EqualTo(7));

            for (int index = 0; index < zone.ChunkCount; index++)
            {
                WorldZoneChunkDefinition chunk = zone.GetChunk(index);
                int expectedX = index - 3;
                Assert.That(chunk.Coordinate, Is.EqualTo(
                    new WorldChunkCoordinate(expectedX, 0)));
                Assert.That(
                    chunk.ScenePath,
                    Is.EqualTo(
                        "Assets/SessionRpg/Content/Worlds/AshenCauseway/Chunks/" +
                        "Chunk_" + expectedX + "_0.unity"));
                Assert.That(chunk.GenerationSeed, Is.GreaterThan(0));
                Assert.That(
                    chunk.BiomeRecipeId,
                    Is.EqualTo("ashen_causeway_volcanic"));
                Assert.That(chunk.PoiRecipeCount, Is.EqualTo(1));
            }
        }

        [Test]
        public void DefaultCatalog_AddsSeparateSunbreakBasinNineChunkSquare()
        {
            WorldZoneCatalog catalog = WorldZoneCatalog.LoadDefault();
            WorldZoneManifest[] zones = catalog.LoadValidatedZones();
            Assert.That(zones, Has.Length.EqualTo(2));
            Assert.That(catalog.TryGetZone(1u, 2u, out WorldZoneCatalogEntry entry), Is.True);
            Assert.That(entry.ZoneKey, Is.EqualTo("sunbreak_basin"));
            Assert.That(entry.DefinitionPath,
                Is.EqualTo("Tools/Agent/Worlds/SunbreakBasin/zone.json"));
            WorldZoneManifest zone = zones[1];
            Assert.That(zone.WorldId, Is.EqualTo(1u));
            Assert.That(zone.ZoneId, Is.EqualTo(2u));
            Assert.That(zone.ZoneKey, Is.EqualTo("sunbreak_basin"));
            Assert.That(zone.DisplayName, Is.EqualTo("Sunbreak Basin"));
            Assert.That(zone.ContentVersion, Is.EqualTo(2));
            Assert.That(zone.ChunkSizeMeters, Is.EqualTo(64));
            Assert.That(zone.LoadRadius, Is.EqualTo(1));
            Assert.That(zone.RetainRadius, Is.EqualTo(2));
            Assert.That(zone.MaxLoadedChunks, Is.EqualTo(9));
            Assert.That(zone.ChunkCount, Is.EqualTo(9));
            var seeds = new HashSet<int>();
            for (int index = 0; index < zone.ChunkCount; index++)
            {
                WorldZoneChunkDefinition chunk = zone.GetChunk(index);
                int x = index / 3 - 1;
                int z = index % 3 - 1;
                Assert.That(chunk.Coordinate, Is.EqualTo(new WorldChunkCoordinate(x, z)));
                Assert.That(chunk.ScenePath, Is.EqualTo(
                    "Assets/SessionRpg/Content/Worlds/SunbreakBasin/Chunks/Chunk_" +
                    x + "_" + z + ".unity"));
                Assert.That(chunk.GenerationSeed, Is.GreaterThan(0));
                Assert.That(seeds.Add(chunk.GenerationSeed), Is.True);
                Assert.That(chunk.BiomeRecipeId, Is.EqualTo("sunbreak_basin"));
                Assert.That(chunk.PoiRecipeCount, Is.EqualTo(1));
                Assert.That(chunk.GetPoiRecipeId(0), Is.EqualTo("sunbreak_basin_clearing"));
            }
        }

        [Test]
        public void SunbreakRevisionTwo_InvalidatesEveryPriorChunkFingerprintWithoutChangingIdentity()
        {
            const string path = "Tools/Agent/Worlds/SunbreakBasin/zone.json";
            string currentJson = File.ReadAllText(WorldZoneManifest.ResolveProjectRelativePath(path));
            string previousJson = System.Text.RegularExpressions.Regex.Replace(currentJson,
                "\"contentVersion\"\\s*:\\s*2\\b", "\"contentVersion\": 1");
            Assert.That(previousJson, Is.Not.EqualTo(currentJson));
            WorldZoneManifest current = WorldZoneManifest.Parse(currentJson, path);
            WorldZoneManifest previous = WorldZoneManifest.Parse(previousJson, "sunbreak-version-one-fingerprint");
            Assert.That(current.ContentVersion, Is.EqualTo(2));
            Assert.That(previous.ContentVersion, Is.EqualTo(1));
            Assert.That(current.WorldId, Is.EqualTo(previous.WorldId));
            Assert.That(current.ZoneId, Is.EqualTo(previous.ZoneId));
            Assert.That(current.ChunkCount, Is.EqualTo(previous.ChunkCount));
            for (int index = 0; index < current.ChunkCount; index++)
            {
                Assert.That(current.GetChunk(index).Coordinate, Is.EqualTo(previous.GetChunk(index).Coordinate));
                Assert.That(current.GetChunk(index).ScenePath, Is.EqualTo(previous.GetChunk(index).ScenePath));
                Assert.That(current.GetChunkSourceFingerprint(index),
                    Is.Not.EqualTo(previous.GetChunkSourceFingerprint(index)),
                    "The new compound topology must not accept the previous generated chunk marker.");
            }
        }

        [Test]
        public void GeneratedBuildScenes_PreserveTrainingAndIncludeAllNineBasinChunks()
        {
            WorldZoneManifest[] zones = WorldZoneCatalog.LoadDefault().LoadValidatedZones();
            var expected = new List<string> { "Assets/SessionRpg/Scenes/Bootstrap.unity" };
            foreach (WorldZoneManifest zone in zones)
            {
                for (int index = 0; index < zone.ChunkCount; index++)
                {
                    expected.Add(zone.GetChunk(index).ScenePath);
                }
            }

            Assert.That(expected, Has.Count.EqualTo(17));
            CollectionAssert.AreEqual(expected, EditorBuildSettings.scenes
                .Where(value => value.enabled).Select(value => value.path).ToArray());
        }

        [TestCase(-1, -1)]
        [TestCase(-1, 0)]
        [TestCase(-1, 1)]
        [TestCase(0, -1)]
        [TestCase(0, 0)]
        [TestCase(0, 1)]
        [TestCase(1, -1)]
        [TestCase(1, 0)]
        [TestCase(1, 1)]
        public void GeneratedBasinChunk_HasSeparateIdentityAndUnstretchedCellOrigin(int x, int z)
        {
            WorldZoneManifest zone = WorldZoneCatalog.LoadDefault().LoadZone(1);
            int index = (x + 1) * 3 + z + 1;
            string path = zone.GetChunk(index).ScenePath;
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                GameObject[] roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(1));
                WorldChunkPresentation marker = roots[0].GetComponent<WorldChunkPresentation>();
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.Matches(1u, 2u, new WorldChunkCoordinate(x, z)), Is.True);
                Assert.That(marker.SourceFingerprint, Is.EqualTo(zone.GetChunkSourceFingerprint(index)));
                Assert.That(marker.transform.position, Is.EqualTo(new Vector3(x * 64f, 0f, z * 64f)));
                Transform visuals = marker.VisualRoot;
                Assert.That(visuals, Is.Not.Null);
                Assert.That(visuals.name, Is.EqualTo("SunbreakBasin"));
                Assert.That(visuals.parent, Is.SameAs(marker.transform));
                Assert.That(visuals.localPosition, Is.EqualTo(Vector3.zero));
                Assert.That(visuals.localScale, Is.EqualTo(Vector3.one));
                Assert.That(visuals.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(visuals.Find("AshenCausewayFloor"), Is.Null,
                    "PvE must use its two-dimensional basin, not the Training corridor.");
                Assert.That(roots[0].GetComponentsInChildren<WorldChunkPresentation>(true),
                    Has.Length.EqualTo(1));
                Assert.That(roots[0].GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(roots[0].GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(roots[0].GetComponentsInChildren<Camera>(true), Is.Empty);
                Assert.That(roots[0].GetComponentsInChildren<AudioListener>(true), Is.Empty);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [Test]
        public void GeneratedRuntimeCatalog_MatchesEverySourceFingerprint()
        {
            WorldZoneCatalog sourceCatalog = WorldZoneCatalog.LoadDefault();
            WorldZoneCatalogAsset runtimeCatalog =
                AssetDatabase.LoadAssetAtPath<WorldZoneCatalogAsset>(
                    "Assets/SessionRpg/Settings/WorldZoneCatalog.asset");

            Assert.That(runtimeCatalog, Is.Not.Null);
            Assert.That(runtimeCatalog.Count, Is.EqualTo(sourceCatalog.Count));
            for (int zoneIndex = 0;
                 zoneIndex < sourceCatalog.Count;
                 zoneIndex++)
            {
                WorldZoneManifest sourceZone = sourceCatalog.LoadZone(zoneIndex);
                WorldZoneEntry runtimeZone = runtimeCatalog.GetZone(zoneIndex);
                Assert.That(runtimeZone.WorldId, Is.EqualTo(sourceZone.WorldId));
                Assert.That(runtimeZone.ZoneId, Is.EqualTo(sourceZone.ZoneId));
                Assert.That(runtimeZone.ZoneKey, Is.EqualTo(sourceZone.ZoneKey));
                Assert.That(runtimeZone.ChunkSizeMeters, Is.EqualTo(sourceZone.ChunkSizeMeters));
                Assert.That(runtimeZone.LoadRadius, Is.EqualTo(sourceZone.LoadRadius));
                Assert.That(runtimeZone.RetainRadius, Is.EqualTo(sourceZone.RetainRadius));
                Assert.That(runtimeZone.MaxLoadedChunks, Is.EqualTo(sourceZone.MaxLoadedChunks));
                Assert.That(runtimeZone.ChunkCount, Is.EqualTo(sourceZone.ChunkCount));
                Assert.That(runtimeCatalog.TryGetZone(sourceZone.WorldId, sourceZone.ZoneId,
                    out WorldZoneEntry resolved), Is.True);
                Assert.That(resolved, Is.SameAs(runtimeZone));
                for (int chunkIndex = 0;
                     chunkIndex < sourceZone.ChunkCount;
                     chunkIndex++)
                {
                    Assert.That(runtimeZone.GetChunk(chunkIndex).Coordinate,
                        Is.EqualTo(sourceZone.GetChunk(chunkIndex).Coordinate));
                    Assert.That(runtimeZone.GetChunk(chunkIndex).ScenePath,
                        Is.EqualTo(sourceZone.GetChunk(chunkIndex).ScenePath));
                    Assert.That(
                        runtimeZone.GetChunk(chunkIndex).SourceFingerprint,
                        Is.EqualTo(
                            sourceZone.GetChunkSourceFingerprint(chunkIndex)),
                        sourceZone.GetChunk(chunkIndex).ScenePath);
                }
            }
        }

        [Test]
        public void ChunkFingerprint_ChangesWithVersionSeedBiomeAndPoiSource()
        {
            string baselineJson = Zone(1, Chunk(0, 0, 101, "origin"));
            string baseline = WorldZoneManifest.Parse(
                baselineJson,
                "fingerprint-baseline").GetChunkSourceFingerprint(0);

            Assert.That(baseline, Has.Length.EqualTo(64));
            Assert.That(
                WorldZoneManifest.Parse(
                    baselineJson.Replace(
                        "\"contentVersion\":1",
                        "\"contentVersion\":2"),
                    "fingerprint-version").GetChunkSourceFingerprint(0),
                Is.Not.EqualTo(baseline));
            Assert.That(
                WorldZoneManifest.Parse(
                    baselineJson.Replace(
                        "\"generationSeed\":101",
                        "\"generationSeed\":102"),
                    "fingerprint-seed").GetChunkSourceFingerprint(0),
                Is.Not.EqualTo(baseline));
            Assert.That(
                WorldZoneManifest.Parse(
                    baselineJson.Replace("test_biome", "other_biome"),
                    "fingerprint-biome").GetChunkSourceFingerprint(0),
                Is.Not.EqualTo(baseline));
            Assert.That(
                WorldZoneManifest.Parse(
                    baselineJson.Replace("\"origin\"", "\"other\""),
                    "fingerprint-poi").GetChunkSourceFingerprint(0),
                Is.Not.EqualTo(baseline));
        }

        [Test]
        public void BiomeAndPointOfInterestRecipeCatalogs_FailClosedForUnknownTokens()
        {
            Assert.That(
                WorldBiomeRecipeCatalog.GetSupport("ashen_causeway_volcanic"),
                Is.EqualTo(WorldBiomeRecipeSupport.Authored));
            Assert.That(
                WorldBiomeRecipeCatalog.GetSupport("ashen_causeway_volcanicc"),
                Is.EqualTo(WorldBiomeRecipeSupport.Unknown));
            Assert.That(
                WorldBiomeRecipeCatalog.GetSupport("sunbreak_basin"),
                Is.EqualTo(WorldBiomeRecipeSupport.Authored));
            Assert.That(
                WorldBiomeRecipeCatalog.GetSupport("sunbreak_basinn"),
                Is.EqualTo(WorldBiomeRecipeSupport.Unknown));
            Assert.That(
                WorldPoiRecipeCatalog.GetSupport("sunbreak_basin_clearing"),
                Is.EqualTo(WorldPoiRecipeSupport.Authored));
            Assert.That(
                WorldPoiRecipeCatalog.GetSupport("sunbreak_basin_clearng"),
                Is.EqualTo(WorldPoiRecipeSupport.Unknown));
            Assert.That(
                WorldPoiRecipeCatalog.GetSupport("west_refuge"),
                Is.EqualTo(WorldPoiRecipeSupport.Authored));
            Assert.That(
                WorldPoiRecipeCatalog.GetSupport("east_watch"),
                Is.EqualTo(WorldPoiRecipeSupport.Authored));
            Assert.That(
                WorldPoiRecipeCatalog.GetSupport("cinder_approach"),
                Is.EqualTo(WorldPoiRecipeSupport.Authored));
            Assert.That(
                WorldPoiRecipeCatalog.GetSupport("ossuary_bend"),
                Is.EqualTo(WorldPoiRecipeSupport.Authored));
            Assert.That(
                WorldPoiRecipeCatalog.GetSupport("west_refguge"),
                Is.EqualTo(WorldPoiRecipeSupport.Unknown));
        }

        [TestCase("ashen_causeway_volcanic", "west_refuge", true)]
        [TestCase("ashen_causeway_volcanic", "cinder_approach", true)]
        [TestCase("ashen_causeway_volcanic", "broken_gate", true)]
        [TestCase("ashen_causeway_volcanic", "collapsed_court", true)]
        [TestCase("ashen_causeway_volcanic", "ossuary_bend", true)]
        [TestCase("ashen_causeway_volcanic", "reliquary_yard", true)]
        [TestCase("ashen_causeway_volcanic", "east_watch", true)]
        [TestCase("sunbreak_basin", "sunbreak_basin_clearing", true)]
        [TestCase("ashen_causeway_volcanic", "sunbreak_basin_clearing", false)]
        [TestCase("sunbreak_basin", "west_refuge", false)]
        [TestCase("sunbreak_basin", "unknown", false)]
        [TestCase("unknown", "sunbreak_basin_clearing", false)]
        public void RecipePairs_AcceptOnlyTheExplicitBiomeImplementation(
            string biome, string poi, bool supported)
        {
            Assert.That(WorldPoiRecipeCatalog.IsSupportedForBiome(biome, poi), Is.EqualTo(supported));
        }

        [TestCase("ashen_causeway_volcanic", "sunbreak_basin_clearing")]
        [TestCase("sunbreak_basin", "west_refuge")]
        public void RecipeValidation_RejectsIndividuallyKnownButMismatchedBiomeAndPoi(
            string biome, string poi)
        {
            WorldZoneManifest zone = WorldZoneManifest.Parse(
                Zone(1, ChunkWithRecipe("Test", 0, 0, 101, biome, poi)), "mismatched-recipes");
            StringAssert.Contains("unsupported for biome",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneRecipeValidator.ValidateOrThrow(zone)).Message);
        }

        [Test]
        public void RecipeValidation_BasinRequiresItsExplicitClearingRecipe()
        {
            WorldZoneManifest missing = WorldZoneManifest.Parse(
                Zone(1, ChunkWithRecipe("Test", 0, 0, 101, "sunbreak_basin")), "missing-clearing");
            StringAssert.Contains("require exactly one sunbreak_basin_clearing",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneRecipeValidator.ValidateOrThrow(missing)).Message);
            WorldZoneManifest legacy = WorldZoneManifest.Parse(
                Zone(1, ChunkWithRecipe("Test", 0, 0, 101, "ashen_causeway_volcanic")), "legacy-empty");
            Assert.DoesNotThrow(() => WorldZoneRecipeValidator.ValidateOrThrow(legacy));
        }

        [Test]
        public void CatalogValidation_AcceptsMultipleSortedBoundedZones()
        {
            WorldZoneCatalog catalog = WorldZoneCatalog.Parse(
                Catalog(
                    CatalogEntry(2, 1, "second_world_zone", "Second"),
                    CatalogEntry(1, 2, "first_world_zone", "First")),
                "multi-zone");
            WorldZoneManifest first = WorldZoneManifest.Parse(
                ZoneWithIdentity(
                    1,
                    2,
                    "first_world_zone",
                    1,
                    ChunkWithRecipe(
                        "First",
                        0,
                        0,
                        101,
                        "ashen_causeway_volcanic",
                        "west_refuge")),
                "first-zone");
            WorldZoneManifest second = WorldZoneManifest.Parse(
                ZoneWithIdentity(
                    2,
                    1,
                    "second_world_zone",
                    1,
                    ChunkWithRecipe(
                        "Second",
                        0,
                        0,
                        201,
                        "ashen_causeway_volcanic",
                        "east_watch")),
                "second-zone");

            catalog.ValidateLoadedZones(new[] { first, second });

            Assert.That(catalog.Count, Is.EqualTo(2));
            Assert.That(catalog.GetZone(0).WorldId, Is.EqualTo(1u));
            Assert.That(catalog.GetZone(0).ZoneId, Is.EqualTo(2u));
            Assert.That(catalog.GetZone(1).WorldId, Is.EqualTo(2u));
            Assert.That(first.MaxLoadedChunks, Is.EqualTo(1));
            Assert.That(second.MaxLoadedChunks, Is.EqualTo(1));
            Assert.That(
                first.GetChunkSourceFingerprint(0),
                Has.Length.EqualTo(64));
            Assert.That(
                first.GetChunkSourceFingerprint(0),
                Is.Not.EqualTo(second.GetChunkSourceFingerprint(0)));
        }

        [Test]
        public void CatalogValidation_RejectsUnsupportedBiomeAndPoiRecipes()
        {
            WorldZoneCatalog catalog = WorldZoneCatalog.Parse(
                Catalog(CatalogEntry(1, 1, "test_zone", "Test")),
                "recipe-catalog");
            WorldZoneManifest unknownBiome = WorldZoneManifest.Parse(
                ZoneWithIdentity(
                    1,
                    1,
                    "test_zone",
                    1,
                    ChunkWithRecipe(
                        "Test",
                        0,
                        0,
                        101,
                        "unknown_biome")),
                "unknown-biome");
            WorldZoneManifest unknownPoi = WorldZoneManifest.Parse(
                ZoneWithIdentity(
                    1,
                    1,
                    "test_zone",
                    1,
                    ChunkWithRecipe(
                        "Test",
                        0,
                        0,
                        101,
                        "ashen_causeway_volcanic",
                        "unknown_poi")),
                "unknown-poi");

            StringAssert.Contains(
                "unsupported biome recipe 'unknown_biome'",
                Assert.Throws<WorldZoneManifestException>(
                    () => catalog.ValidateLoadedZones(
                        new[] { unknownBiome })).Message);
            StringAssert.Contains(
                "unsupported POI recipe 'unknown_poi'",
                Assert.Throws<WorldZoneManifestException>(
                    () => catalog.ValidateLoadedZones(
                        new[] { unknownPoi })).Message);
        }

        [Test]
        public void CatalogValidation_RejectsCrossZoneSceneCollisionAndWrongOrder()
        {
            WorldZoneCatalog catalog = WorldZoneCatalog.Parse(
                Catalog(
                    CatalogEntry(1, 1, "first_zone", "First"),
                    CatalogEntry(1, 2, "second_zone", "Second")),
                "cross-zone");
            WorldZoneManifest first = WorldZoneManifest.Parse(
                ZoneWithIdentity(
                    1,
                    1,
                    "first_zone",
                    1,
                    ChunkWithRecipe(
                        "Shared",
                        0,
                        0,
                        101,
                        "ashen_causeway_volcanic",
                        "west_refuge")),
                "first-zone");
            WorldZoneManifest second = WorldZoneManifest.Parse(
                ZoneWithIdentity(
                    1,
                    2,
                    "second_zone",
                    1,
                    ChunkWithRecipe(
                        "Shared",
                        0,
                        0,
                        201,
                        "ashen_causeway_volcanic",
                        "east_watch")),
                "second-zone");

            StringAssert.Contains(
                "already used by another zone",
                Assert.Throws<WorldZoneManifestException>(
                    () => catalog.ValidateLoadedZones(
                        new[] { first, second })).Message);
            StringAssert.Contains(
                "but the loaded manifest identifies",
                Assert.Throws<WorldZoneManifestException>(
                    () => catalog.ValidateLoadedZones(
                        new[] { second, first })).Message);
        }

        [Test]
        public void CatalogParse_RejectsZoneCountAboveBoundedCeiling()
        {
            var entries = new string[WorldZoneCatalog.MaximumZoneCount + 1];
            for (int index = 0; index < entries.Length; index++)
            {
                entries[index] = CatalogEntry(
                    1,
                    index + 1,
                    "zone_" + (index + 1),
                    "Zone" + (index + 1));
            }

            StringAssert.Contains(
                "bounded catalog ceiling of " +
                WorldZoneCatalog.MaximumZoneCount,
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneCatalog.Parse(
                        Catalog(entries),
                        "too-many-zones")).Message);
        }

        [Test]
        public void ChunkFingerprint_IsIndependentOfPoiDeclarationOrder()
        {
            WorldZoneManifest first = WorldZoneManifest.Parse(
                Zone(
                    1,
                    Chunk(0, 0, 101, "west_refuge", "east_watch")),
                "poi-order-first");
            WorldZoneManifest second = WorldZoneManifest.Parse(
                Zone(
                    1,
                    Chunk(0, 0, 101, "east_watch", "west_refuge")),
                "poi-order-second");

            Assert.That(
                first.GetChunkSourceFingerprint(0),
                Is.EqualTo(second.GetChunkSourceFingerprint(0)));
        }

        [Test]
        public void CatalogParse_SortsByStableNumericIdentity()
        {
            WorldZoneCatalog catalog = WorldZoneCatalog.Parse(
                Catalog(
                    CatalogEntry(2, 2, "second_world_second_zone", "Second"),
                    CatalogEntry(1, 3, "first_world_third_zone", "Third"),
                    CatalogEntry(1, 1, "first_world_first_zone", "First")),
                "catalog-sort");

            Assert.That(catalog.GetZone(0).WorldId, Is.EqualTo(1u));
            Assert.That(catalog.GetZone(0).ZoneId, Is.EqualTo(1u));
            Assert.That(catalog.GetZone(1).ZoneId, Is.EqualTo(3u));
            Assert.That(catalog.GetZone(2).WorldId, Is.EqualTo(2u));
        }

        [Test]
        public void RuntimeCatalog_LooksUpTheCompleteWorldAndZoneIdentity()
        {
            WorldZoneCatalogAsset catalog =
                ScriptableObject.CreateInstance<WorldZoneCatalogAsset>();
            try
            {
                WorldZoneEntry firstWorld = RuntimeZone(1u, 7u, "first_world");
                WorldZoneEntry secondWorld = RuntimeZone(2u, 7u, "second_world");
                catalog.ConfigureForAuthoring(new[] { firstWorld, secondWorld });

                Assert.That(
                    catalog.TryGetZone(2u, 7u, out WorldZoneEntry resolved),
                    Is.True);
                Assert.That(resolved, Is.SameAs(secondWorld));
                Assert.That(catalog.TryGetZone(3u, 7u, out _), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void Parse_RejectsUnsupportedSchemaAndNonPositiveIdentities()
        {
            string catalogSchema = Catalog(
                CatalogEntry(1, 1, "test_zone", "Test")).Replace(
                    "\"schemaVersion\":1",
                    "\"schemaVersion\":2");
            string catalogIdentity = Catalog(
                CatalogEntry(0, 1, "test_zone", "Test"));
            string zoneIdentity = Zone(
                1,
                Chunk(0, 0, 101, "origin")).Replace(
                    "\"zoneId\":1",
                    "\"zoneId\":-1");

            StringAssert.Contains(
                "schemaVersion must be 1",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneCatalog.Parse(
                        catalogSchema,
                        "unsupported-schema")).Message);
            StringAssert.Contains(
                "worldId must be a positive integer",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneCatalog.Parse(
                        catalogIdentity,
                        "invalid-world-id")).Message);
            StringAssert.Contains(
                "zoneId must be a positive integer",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneManifest.Parse(
                        zoneIdentity,
                        "invalid-zone-id")).Message);
        }

        [Test]
        public void ZoneParse_SortsChunksAndPoiRecipesDeterministically()
        {
            WorldZoneManifest zone = WorldZoneManifest.Parse(
                Zone(
                    3,
                    Chunk(1, 0, 103, "zeta", "alpha"),
                    Chunk(0, 0, 101, "refuge"),
                    Chunk(-1, 0, 102, "west")),
                "chunk-sort");

            Assert.That(zone.GetChunk(0).Coordinate.X, Is.EqualTo(-1));
            Assert.That(zone.GetChunk(1).Coordinate.X, Is.EqualTo(0));
            Assert.That(zone.GetChunk(2).Coordinate.X, Is.EqualTo(1));
            Assert.That(zone.GetChunk(2).GetPoiRecipeId(0), Is.EqualTo("alpha"));
            Assert.That(zone.GetChunk(2).GetPoiRecipeId(1), Is.EqualTo("zeta"));
        }

        [Test]
        public void CatalogParse_RejectsDuplicateIdentitiesKeysAndPaths()
        {
            string first = CatalogEntry(1, 1, "ashen_causeway", "AshenCauseway");
            string duplicateIdentity = CatalogEntry(
                1,
                1,
                "other_zone",
                "OtherZone");
            string duplicateKey = CatalogEntry(
                2,
                2,
                "ashen_causeway",
                "OtherZone");
            string duplicatePath = CatalogEntry(
                2,
                2,
                "other_zone",
                "ashencauseway");

            StringAssert.Contains(
                "duplicate another zone identity",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneCatalog.Parse(
                        Catalog(first, duplicateIdentity),
                        "duplicate-identity")).Message);
            StringAssert.Contains(
                "zoneKey 'ashen_causeway' is duplicated",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneCatalog.Parse(
                        Catalog(first, duplicateKey),
                        "duplicate-key")).Message);
            StringAssert.Contains(
                "including a case-only difference",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneCatalog.Parse(
                        Catalog(first, duplicatePath),
                        "duplicate-path")).Message);
        }

        [Test]
        public void ZoneParse_RejectsDuplicateCoordinateSceneSeedAndPoi()
        {
            string first = Chunk(0, 0, 101, "refuge");

            StringAssert.Contains(
                "coordinate (0, 0) is duplicated",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneManifest.Parse(
                        Zone(2, first, Chunk(0, 0, 102, "other")),
                        "duplicate-coordinate")).Message);
            StringAssert.Contains(
                "scenePath",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneManifest.Parse(
                        Zone(
                            2,
                            first,
                            Chunk(1, 0, 102, "other").Replace(
                                "Chunk_1_0.unity",
                                "Chunk_0_0.unity")),
                        "duplicate-scene")).Message);
            StringAssert.Contains(
                "generationSeed 101 is duplicated",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneManifest.Parse(
                        Zone(2, first, Chunk(1, 0, 101, "other")),
                        "duplicate-seed")).Message);
            StringAssert.Contains(
                "poiRecipeId 'refuge' is duplicated",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneManifest.Parse(
                        Zone(1, Chunk(0, 0, 101, "refuge", "refuge")),
                        "duplicate-poi")).Message);
        }

        [TestCase("../outside/zone.json")]
        [TestCase("Tools/Agent/Worlds/../Outside/zone.json")]
        [TestCase("Tools\\Agent\\Worlds\\AshenCauseway\\zone.json")]
        [TestCase("Tools/Agent/Worlds/AshenCauseway/zone.asset")]
        public void CatalogParse_RejectsUnsafeDefinitionPaths(string definitionPath)
        {
            string entry = "{" +
                "\"worldId\":1," +
                "\"zoneId\":1," +
                "\"zoneKey\":\"ashen_causeway\"," +
                "\"definitionPath\":\"" + Escape(definitionPath) + "\"" +
                "}";

            WorldZoneManifestException exception = Assert.Throws<
                WorldZoneManifestException>(
                () => WorldZoneCatalog.Parse(Catalog(entry), "unsafe-definition"));

            StringAssert.Contains("definitionPath", exception.Message);
        }

        [TestCase("../Chunk_0_0.unity")]
        [TestCase("Assets/SessionRpg/Content/Worlds/../Chunk_0_0.unity")]
        [TestCase("Assets\\SessionRpg\\Content\\Worlds\\Chunk_0_0.unity")]
        [TestCase("Assets/SessionRpg/Content/Worlds/Test/Chunk_0_0.prefab")]
        public void ZoneParse_RejectsUnsafeScenePaths(string scenePath)
        {
            string chunk = Chunk(0, 0, 101, "origin").Replace(
                "Assets/SessionRpg/Content/Worlds/Test/Chunks/Chunk_0_0.unity",
                Escape(scenePath));

            WorldZoneManifestException exception = Assert.Throws<
                WorldZoneManifestException>(
                () => WorldZoneManifest.Parse(Zone(1, chunk), "unsafe-scene"));

            StringAssert.Contains("scenePath", exception.Message);
        }

        [TestCase("\"chunkSizeMeters\":64", "\"chunkSizeMeters\":32", "chunkSizeMeters must be exactly 64")]
        [TestCase("\"loadRadius\":1", "\"loadRadius\":-1", "loadRadius cannot be negative")]
        [TestCase("\"retainRadius\":2", "\"retainRadius\":1", "retainRadius must be greater")]
        [TestCase("\"retainRadius\":2", "\"retainRadius\":9", "must not exceed 8")]
        [TestCase("\"maxLoadedChunks\":3", "\"maxLoadedChunks\":16", "between 1 and 15")]
        public void ZoneParse_RejectsStreamingInvariantViolations(
            string before,
            string after,
            string expectedMessage)
        {
            string json = Zone(
                3,
                Chunk(-1, 0, 101, "west"),
                Chunk(0, 0, 102, "origin"),
                Chunk(1, 0, 103, "east")).Replace(before, after);

            WorldZoneManifestException exception = Assert.Throws<
                WorldZoneManifestException>(
                () => WorldZoneManifest.Parse(json, "streaming-invariant"));

            StringAssert.Contains(expectedMessage, exception.Message);
        }

        [Test]
        public void ZoneParse_RejectsDisconnectedFootprintAndMissingOrigin()
        {
            WorldZoneManifestException disconnected = Assert.Throws<
                WorldZoneManifestException>(
                () => WorldZoneManifest.Parse(
                    Zone(
                        3,
                        Chunk(0, 0, 101, "origin"),
                        Chunk(1, 0, 102, "east"),
                        Chunk(3, 0, 103, "island")),
                    "disconnected"));
            WorldZoneManifestException missingOrigin = Assert.Throws<
                WorldZoneManifestException>(
                () => WorldZoneManifest.Parse(
                    Zone(
                        2,
                        Chunk(1, 0, 101, "one"),
                        Chunk(2, 0, 102, "two")),
                    "missing-origin"));

            StringAssert.Contains("four-neighbor-connected", disconnected.Message);
            StringAssert.Contains("coordinate (0, 0)", missingOrigin.Message);
        }

        [Test]
        public void ZoneParse_RejectsCapBelowMustLoadNeighborhood()
        {
            string json = Zone(
                2,
                Chunk(-1, 0, 101, "west"),
                Chunk(0, 0, 102, "origin"),
                Chunk(1, 0, 103, "east")).Replace(
                    "\"maxLoadedChunks\":3",
                    "\"maxLoadedChunks\":2");

            WorldZoneManifestException exception = Assert.Throws<
                WorldZoneManifestException>(
                () => WorldZoneManifest.Parse(json, "must-load-budget"));

            StringAssert.Contains("largest authored loadRadius neighborhood", exception.Message);
        }

        [Test]
        public void StrictSchema_RejectsMissingUnknownAndWrongTypeProperties()
        {
            string valid = Zone(1, Chunk(0, 0, 101, "origin"));
            string missing = valid.Replace("\"displayName\":\"Test Zone\",", string.Empty);
            string unknown = valid.Replace(
                "\"contentVersion\":1,",
                "\"unexpected\":1,\"contentVersion\":1,");
            string wrongType = valid.Replace(
                "\"chunkSizeMeters\":64",
                "\"chunkSizeMeters\":\"64\"");

            StringAssert.Contains(
                "missing required property 'displayName'",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneManifest.Parse(missing, "missing-field")).Message);
            StringAssert.Contains(
                "unknown property 'unexpected'",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneManifest.Parse(unknown, "unknown-field")).Message);
            StringAssert.Contains(
                "chunkSizeMeters must be a JSON integer",
                Assert.Throws<WorldZoneManifestException>(
                    () => WorldZoneManifest.Parse(wrongType, "wrong-type")).Message);
        }

        [Test]
        public void CatalogPath_ResolvesFromProjectRootAndRejectsTraversal()
        {
            string resolved = WorldZoneCatalog.ResolveProjectRelativePath(
                WorldZoneCatalog.DefaultProjectRelativePath);

            StringAssert.EndsWith(
                Path.Combine("Tools", "Agent", "world-zones.json"),
                resolved);
            Assert.Throws<WorldZoneManifestException>(
                () => WorldZoneCatalog.LoadProjectRelative("../world-zones.json"));
        }

        private static string Catalog(params string[] entries)
        {
            return "{\"schemaVersion\":1,\"zones\":[" +
                string.Join(",", entries) + "]}";
        }

        private static WorldZoneEntry RuntimeZone(
            uint worldId,
            uint zoneId,
            string zoneKey)
        {
            var zone = new WorldZoneEntry();
            zone.ConfigureForAuthoring(
                worldId,
                zoneId,
                zoneKey,
                64f,
                1,
                2,
                1,
                Array.Empty<WorldZoneChunkEntry>());
            return zone;
        }

        private static string CatalogEntry(
            int worldId,
            int zoneId,
            string zoneKey,
            string folder)
        {
            return "{" +
                "\"worldId\":" + worldId + "," +
                "\"zoneId\":" + zoneId + "," +
                "\"zoneKey\":\"" + Escape(zoneKey) + "\"," +
                "\"definitionPath\":\"Tools/Agent/Worlds/" +
                Escape(folder) + "/zone.json\"" +
                "}";
        }

        private static string Zone(int maxLoadedChunks, params string[] chunks)
        {
            return ZoneWithIdentity(
                1,
                1,
                "test_zone",
                maxLoadedChunks,
                chunks);
        }

        private static string ZoneWithIdentity(
            int worldId,
            int zoneId,
            string zoneKey,
            int maxLoadedChunks,
            params string[] chunks)
        {
            return "{" +
                "\"schemaVersion\":1," +
                "\"worldId\":" + worldId + "," +
                "\"zoneId\":" + zoneId + "," +
                "\"zoneKey\":\"" + Escape(zoneKey) + "\"," +
                "\"displayName\":\"Test Zone\"," +
                "\"contentVersion\":1," +
                "\"chunkSizeMeters\":64," +
                "\"loadRadius\":1," +
                "\"retainRadius\":2," +
                "\"maxLoadedChunks\":" + maxLoadedChunks + "," +
                "\"chunks\":[" + string.Join(",", chunks) + "]" +
                "}";
        }

        private static string Chunk(
            int x,
            int z,
            int generationSeed,
            params string[] poiRecipeIds)
        {
            return ChunkWithRecipe(
                "Test",
                x,
                z,
                generationSeed,
                "test_biome",
                poiRecipeIds);
        }

        private static string ChunkWithRecipe(
            string folder,
            int x,
            int z,
            int generationSeed,
            string biomeRecipeId,
            params string[] poiRecipeIds)
        {
            string[] escapedPoiIds = new string[poiRecipeIds.Length];
            for (int index = 0; index < poiRecipeIds.Length; index++)
            {
                escapedPoiIds[index] = "\"" + Escape(poiRecipeIds[index]) + "\"";
            }

            return "{" +
                "\"x\":" + x + "," +
                "\"z\":" + z + "," +
                "\"scenePath\":\"Assets/SessionRpg/Content/Worlds/" +
                Escape(folder) + "/Chunks/" +
                "Chunk_" + x + "_" + z + ".unity\"," +
                "\"generationSeed\":" + generationSeed + "," +
                "\"biomeRecipeId\":\"" + Escape(biomeRecipeId) + "\"," +
                "\"poiRecipeIds\":[" + string.Join(",", escapedPoiIds) + "]" +
                "}";
        }

        private static string Escape(string value)
        {
            return (value ?? string.Empty)
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"");
        }

    }
}
