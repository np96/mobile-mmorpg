using System;
using System.Collections.Generic;
using System.Linq;
using SessionRpg.Editor.Assets;
using SessionRpg.Editor.Arenas;
using SessionRpg.Editor.Performance;
using SessionRpg.Editor.World;
using SessionRpg.Networking;
using SessionRpg.Unity.Camera;
using SessionRpg.Unity.Arenas;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.Input;
using SessionRpg.Unity.Performance;
using SessionRpg.Unity.Presentation;
using SessionRpg.Unity.Product;
using SessionRpg.Unity.UI;
using SessionRpg.Unity.World;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Authoring
{
    public static class SessionRpgAuthoringGenerator
    {
        public const string ScenePath = "Assets/SessionRpg/Scenes/Bootstrap.unity";
        public const string MenuPath = "Tools/Session RPG/Rebuild Playable Foundation";

        private const string ArenaCatalogPath =
            "Assets/SessionRpg/Settings/ArenaCatalog.asset";

        private const string ArenaLayoutRecipe = "arena_default_v1";
        private const string BossLayoutRecipe = "boss_default_v1";
        private const string SiegeLayoutRecipe = "siege_default_v1";
        private const string RiftCrucibleRecipe = "rift_crucible_v1";
        private const string EmberVaultRecipe = "ember_vault_v1";
        private const string AshenStrongholdRecipe = "ashen_stronghold_v1";

        private const float SiegeCaptureYaw = 45f;
        private const float SiegeCapturePitch = 58f;
        private const float SiegeCaptureDistance = 25f;
        private const float SiegeCaptureFieldOfView = 52f;

        [MenuItem(MenuPath, priority = 100)]
        public static void RebuildFromMenu()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "Session RPG",
                    "Exit Play Mode before rebuilding the playable foundation.",
                    "OK");
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Execute();
        }

        public static void RebuildForCommandLine()
        {
            Execute();
        }

        public static void ValidateForCommandLine()
        {
            ValidateTrackedFoundation();
            Debug.Log("Session RPG authored foundation validation passed.");
        }

        public static void Execute()
        {
            KayKitAssetValidator.ValidateOrThrow();
            QuaterniusAssetValidator.ValidateOrThrow();
            FortressSourceArtValidator.ValidateOrThrow();
            ArenaFloorSourceArtValidator.ValidateOrThrow();
            MixamoPlayableClassTrialAssetValidator.ValidateSelectedSourcesOrThrow();
            ValidateSelectedHunterSource();
            ConfigureLayers();
            AuthoringAssets assets = AuthoringAssets.Build();
            CombatantAnimationControllers animationControllers = AnimationAuthoring.Build();
            GameObject combatantPrefab = CharacterAuthoring.BuildCombatantPrefab(
                assets.Blue,
                assets.Gold,
                assets.Gold,
                assets.Health,
                assets.Barrier,
                assets.Cast,
                assets.ContactShadow,
                assets.CombatEffect,
                animationControllers);
            AssetDatabase.SaveAssets();
            WorldZoneCatalogAsset worldCatalog = BuildWorldContent(assets);
            CombatSfxPalette audioPalette = CombatAudioAuthoring.BuildPalette();
            BuildScene(assets, combatantPrefab, audioPalette, worldCatalog);
            ConfigureProject(assets);
            AssetDatabase.SaveAssets();
            ValidateTrackedFoundation();
            Debug.Log("Session RPG playable foundation rebuilt: " + ScenePath);
        }

        public static void ValidateTrackedFoundation()
        {
            KayKitAssetValidator.ValidateOrThrow();
            QuaterniusAssetValidator.ValidateOrThrow();
            FortressSourceArtValidator.ValidateOrThrow();
            ArenaFloorSourceArtValidator.ValidateOrThrow();
            MixamoPlayableClassTrialAssetValidator.ValidateSelectedSourcesOrThrow();
            ValidateSelectedHunterSource();
            AbilityIconAuthoring.ValidateOrThrow();
            HudMedallionFrameAuthoring.ValidateOrThrow();
            CombatSfxPalette audioPalette = AssetDatabase.LoadAssetAtPath<CombatSfxPalette>(
                CombatAudioAuthoring.PalettePath);
            if (audioPalette == null || !audioPalette.HasEveryRequiredClip())
            {
                throw new BuildFailedException(
                    "Missing or incomplete authored CombatSfxPalette. Rebuild the playable foundation.");
            }
            ValidateRenderPipelineConfiguration();
            MobileQualityAssetAuthoring.ValidateDefaultAssetsOrThrow();
            ValidateArenaContentFoundation();
            ValidateWorldContentFoundation();
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                throw new BuildFailedException(
                    "Missing Bootstrap scene. Run Tools > Session RPG > Rebuild Playable Foundation.");
            }

            GameObject combatantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                AuthoringAssets.PrefabFolder + "/Combatant.prefab");
            if (combatantPrefab == null)
            {
                throw new BuildFailedException(
                    "Missing Combatant prefab. Rebuild the Session RPG playable foundation.");
            }

            ValidateCombatantAnimation(combatantPrefab);

            Scene scene = SceneManager.GetSceneByPath(ScenePath);
            bool opened = false;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                    opened = true;
                }

                RequireExactlyOne<PrototypeGameController>(scene, "PrototypeGameController");
                RequireExactlyOne<MobileQualityController>(scene, "MobileQualityController");
                RequireExactlyOne<UnityEngine.Camera>(scene, "gameplay Camera");
                RequireExactlyOne<StrategyFollowCamera>(scene, "StrategyFollowCamera");
                RequireExactlyOne<AudioListener>(scene, "AudioListener");
                RequireExactlyOne<EventSystem>(scene, "EventSystem");
                RequireExactlyOne<NetworkManager>(scene, "NetworkManager");
                RequireExactlyOne<CombatEffectsPresenter>(scene, "CombatEffectsPresenter");
                RequireExactlyOne<CombatAudioPresenter>(scene, "CombatAudioPresenter");
                RequireExactlyOne<OffscreenThreatPresenter>(scene, "OffscreenThreatPresenter");
                RequireExactlyOne<PrototypeHud>(scene, "PrototypeHud");
                RequireExactlyOne<KitViewerPanel>(scene, "KitViewerPanel");
                RequireExactlyOne<PauseSettingsPanel>(scene, "PauseSettingsPanel");
                RequireExactlyOne<LocalProductServices>(scene, "LocalProductServices");
                RequireExactlyOne<WorldZoneStreamController>(
                    scene,
                    "WorldZoneStreamController");
                RequireExactlyOne<WorldLightingPresenter>(scene, "WorldLightingPresenter");
                WorldLightingPresenter worldLighting =
                    GetSceneComponents<WorldLightingPresenter>(scene)[0];
                if (!worldLighting.HasAuthoredBindings ||
                    !worldLighting.HasSunbreakVistaBindings ||
                    worldLighting.KeyLight.gameObject.scene != scene ||
                    worldLighting.SunbreakVista.scene != scene)
                {
                    throw new BuildFailedException(
                        "WorldLightingPresenter must reference the Bootstrap directional key, " +
                        "Sunbreak sky, and resident distant vista.");
                }
                SunbreakVistaAuthoring.Validate(worldLighting.SunbreakVista);

                UnityEngine.Camera gameplayCamera =
                    GetSceneComponents<UnityEngine.Camera>(scene)[0];
                StrategyFollowCamera strategyCamera =
                    GetSceneComponents<StrategyFollowCamera>(scene)[0];
                if (gameplayCamera.gameObject != strategyCamera.gameObject ||
                    gameplayCamera.orthographic)
                {
                    throw new BuildFailedException(
                        "Bootstrap gameplay Camera and perspective StrategyFollowCamera " +
                        "must share one GameObject.");
                }

                if (Mathf.Abs(Mathf.DeltaAngle(
                        strategyCamera.OrbitCenterYaw,
                        StrategyFollowCamera.ArenaYaw)) > 0.001f ||
                    Mathf.Abs(
                        strategyCamera.MaximumOrbitOffset -
                        StrategyFollowCamera.ArenaMaximumOrbitOffset) > 0.001f)
                {
                    throw new BuildFailedException(
                        "Bootstrap StrategyFollowCamera must serialize the reviewed " +
                        "Arena orbit center and maximum offset.");
                }

                CombatAudioPresenter audio = GetSceneComponents<CombatAudioPresenter>(scene)[0];
                if (audio.Palette == null || !audio.Palette.HasEveryRequiredClip())
                {
                    throw new BuildFailedException(
                        "Bootstrap CombatAudioPresenter requires a complete serialized SFX palette.");
                }

                if (audio.WorldSourceCount != CombatAudioAuthoring.WorldSourcePoolSize ||
                    audio.UiSourceCount != CombatAudioAuthoring.UiSourcePoolSize)
                {
                    throw new BuildFailedException(
                        "Bootstrap combat audio pools must contain exactly " +
                        CombatAudioAuthoring.WorldSourcePoolSize + " world and " +
                        CombatAudioAuthoring.UiSourcePoolSize + " UI sources.");
                }

                CombatEffectsPresenter combatEffects =
                    GetSceneComponents<CombatEffectsPresenter>(scene)[0];
                if (combatEffects.EffectPoolCount != CombatEffectsAuthoring.EffectPoolSize)
                {
                    throw new BuildFailedException(
                        "Bootstrap combat effects require exactly " +
                        CombatEffectsAuthoring.EffectPoolSize + " pooled views.");
                }

                if (combatEffects.GroundEffectPoolCount !=
                        CombatEffectsAuthoring.GroundEffectPoolSize ||
                    !combatEffects.HasAuthoredGroundEffectRig)
                {
                    throw new BuildFailedException(
                        "Bootstrap Hunter ground effects require exactly " +
                        CombatEffectsAuthoring.GroundEffectPoolSize +
                        " pooled trap views.");
                }

                for (int index = 0; index < combatEffects.EffectPoolCount; index++)
                {
                    CombatEffectView effect = combatEffects.GetEffect(index);
                    if (effect == null || !effect.HasAuthoredSpellRig)
                    {
                        throw new BuildFailedException(
                            "Bootstrap combat effect " + index +
                            " is missing its multi-part spell rig.");
                    }
                }

                OffscreenThreatPresenter offscreenThreats =
                    GetSceneComponents<OffscreenThreatPresenter>(scene)[0];
                if (offscreenThreats.IndicatorCount !=
                        OffscreenThreatPresenter.MaximumIndicatorCount ||
                    offscreenThreats.GetIndicator(0) == null ||
                    offscreenThreats.GetIndicator(1) == null)
                {
                    throw new BuildFailedException(
                        "Bootstrap off-screen threat awareness requires exactly two serialized indicators.");
                }

                PrototypeGameController controller =
                    GetSceneComponents<PrototypeGameController>(scene)[0];
                PrototypeHud hud = GetSceneComponents<PrototypeHud>(scene)[0];
                if (!hud.HasAuthoredPveLevelingContract)
                {
                    throw new BuildFailedException("Bootstrap requires the separate PvE Leveling home card and selection marker.");
                }
                if (!hud.HasAuthoredLevelingContract)
                {
                    throw new BuildFailedException(
                        "Bootstrap PrototypeHud requires serialized Leveling mode, " +
                        "selection-marker, and XP progression references.");
                }

                MobileQualityController qualityController =
                    GetSceneComponents<MobileQualityController>(scene)[0];
                if (qualityController == null)
                {
                    throw new BuildFailedException(
                        "Bootstrap requires its serialized mobile quality controller.");
                }
                SerializedObject serializedController = new SerializedObject(controller);
                if (serializedController.FindProperty("_worldLighting")
                        ?.objectReferenceValue != worldLighting)
                {
                    throw new BuildFailedException(
                        "Bootstrap PrototypeGameController requires its world lighting presenter.");
                }
                if (serializedController.FindProperty("_offscreenThreats")?.objectReferenceValue !=
                    offscreenThreats)
                {
                    throw new BuildFailedException(
                        "Bootstrap PrototypeGameController requires its serialized off-screen threat presenter.");
                }

                WorldZoneStreamController worldStreamer =
                    GetSceneComponents<WorldZoneStreamController>(scene)[0];
                if (serializedController.FindProperty("_worldStreamer")
                        ?.objectReferenceValue != worldStreamer ||
                    worldStreamer.Catalog == null)
                {
                    throw new BuildFailedException(
                        "Bootstrap PrototypeGameController requires its serialized " +
                        "world streamer and generated world-zone catalog.");
                }

                if (serializedController.FindProperty("_kitViewer")
                        ?.objectReferenceValue == null ||
                    serializedController.FindProperty("_pauseSettings")
                        ?.objectReferenceValue == null ||
                    serializedController.FindProperty("_productServices")
                        ?.objectReferenceValue == null ||
                    serializedController.FindProperty("_arenaContentLoader")
                        ?.objectReferenceValue == null)
                {
                    throw new BuildFailedException(
                        "Bootstrap PrototypeGameController requires serialized kit, " +
                        "pause/settings, local product services, and runtime content-loader references.");
                }

                CombatantView[] views = GetSceneComponents<CombatantView>(scene);
                if (views.Length != 12)
                {
                    throw new BuildFailedException(
                        "Bootstrap must contain exactly twelve reusable CombatantView " +
                        "instances (four heroes plus up to eight hostiles); found " +
                        views.Length + ".");
                }

                for (int index = 0; index < views.Length; index++)
                {
                    CombatantView view = views[index];
                    if (view.AuraVisuals == null ||
                        !view.AuraVisuals.HasAuthoredPersistentMotifs)
                    {
                        throw new BuildFailedException(
                            "CombatantView " + index +
                            " is missing the specialized Shadow/Hunter aura visual rig.");
                    }

                    if (view.CompanionVisuals == null ||
                        !view.CompanionVisuals.HasAuthoredOrderLanguage)
                    {
                        throw new BuildFailedException(
                            "CombatantView " + index +
                            " is missing the Hunter companion order visual language.");
                    }
                }
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateSelectedHunterSource()
        {
            if (HunterCharacterSourceSelection.UsesProductionV1)
            {
                HunterProductionAssetValidator.ValidateSelectedSourceOrThrow();
                return;
            }

            if (HunterCharacterSourceSelection.UsesMixamoErikaTrial)
            {
                MixamoErikaTrialAssetValidator.ValidateSelectedSourceOrThrow();
                return;
            }

            HunterOutfitAlbedoValidator.ValidateOrThrow();
        }

        private static WorldZoneCatalogAsset BuildWorldContent(
            AuthoringAssets assets)
        {
            WorldZoneCatalog sourceCatalog = WorldZoneCatalog.LoadDefault();
            WorldZoneManifest[] sourceZones =
                sourceCatalog.LoadValidatedZones();
            var zones = new WorldZoneAuthoringEntry[sourceZones.Length];
            for (int zoneIndex = 0; zoneIndex < sourceZones.Length; zoneIndex++)
            {
                WorldZoneManifest zone = sourceZones[zoneIndex];
                var chunks = new WorldChunkAuthoringEntry[zone.ChunkCount];
                for (int chunkIndex = 0;
                    chunkIndex < zone.ChunkCount;
                    chunkIndex++)
                {
                    WorldZoneChunkDefinition chunk = zone.GetChunk(chunkIndex);
                    ValidateWorldChunkRecipes(chunk);

                    var pointOfInterestRecipes =
                        new string[chunk.PoiRecipeCount];
                    for (int recipeIndex = 0;
                        recipeIndex < pointOfInterestRecipes.Length;
                        recipeIndex++)
                    {
                        pointOfInterestRecipes[recipeIndex] =
                            chunk.GetPoiRecipeId(recipeIndex);
                    }

                    chunks[chunkIndex] = new WorldChunkAuthoringEntry(
                        chunk.Coordinate,
                        chunk.ScenePath,
                        chunk.GenerationSeed,
                        zone.GetChunkSourceFingerprint(chunkIndex),
                        chunk.BiomeRecipeId,
                        pointOfInterestRecipes);
                }

                zones[zoneIndex] = new WorldZoneAuthoringEntry(
                    zone.WorldId,
                    zone.ZoneId,
                    zone.ZoneKey,
                    zone.ChunkSizeMeters,
                    zone.LoadRadius,
                    zone.RetainRadius,
                    zone.MaxLoadedChunks,
                    chunks);
            }

            return WorldChunkAuthoring.BuildCatalog(assets, zones);
        }

        private static void ValidateWorldContentFoundation()
        {
            WorldZoneCatalog sourceCatalog = WorldZoneCatalog.LoadDefault();
            WorldZoneManifest[] sourceZones =
                sourceCatalog.LoadValidatedZones();
            WorldZoneCatalogAsset runtimeCatalog =
                AssetDatabase.LoadAssetAtPath<WorldZoneCatalogAsset>(
                    WorldChunkAuthoring.CatalogPath);
            if (runtimeCatalog == null || runtimeCatalog.Count != sourceCatalog.Count)
            {
                throw new BuildFailedException(
                    "Generated WorldZoneCatalog.asset does not match " +
                    WorldZoneCatalog.DefaultProjectRelativePath +
                    ". Rebuild the playable foundation.");
            }

            var expectedBuildScenes = new List<string> { ScenePath };
            for (int zoneIndex = 0;
                 zoneIndex < sourceCatalog.Count;
                 zoneIndex++)
            {
                WorldZoneManifest manifest = sourceZones[zoneIndex];
                WorldZoneEntry runtimeZone = runtimeCatalog.GetZone(zoneIndex);
                if (runtimeZone == null ||
                    runtimeZone.WorldId != manifest.WorldId ||
                    runtimeZone.ZoneId != manifest.ZoneId ||
                    !string.Equals(
                        runtimeZone.ZoneKey,
                        manifest.ZoneKey,
                        StringComparison.Ordinal) ||
                    Math.Abs(runtimeZone.ChunkSizeMeters -
                        manifest.ChunkSizeMeters) > 0.001f ||
                    runtimeZone.LoadRadius != manifest.LoadRadius ||
                    runtimeZone.RetainRadius != manifest.RetainRadius ||
                    runtimeZone.MaxLoadedChunks != manifest.MaxLoadedChunks ||
                    runtimeZone.ChunkCount != manifest.ChunkCount)
                {
                    throw new BuildFailedException(
                        "Generated runtime zone '" + manifest.ZoneKey +
                        "' does not match its authoring manifest.");
                }

                for (int chunkIndex = 0;
                     chunkIndex < manifest.ChunkCount;
                     chunkIndex++)
                {
                    WorldZoneChunkDefinition definition =
                        manifest.GetChunk(chunkIndex);
                    ValidateWorldChunkRecipes(definition);
                    string expectedFingerprint =
                        manifest.GetChunkSourceFingerprint(chunkIndex);
                    WorldZoneChunkEntry runtimeChunk =
                        runtimeZone.GetChunk(chunkIndex);
                    if (runtimeChunk == null ||
                        runtimeChunk.Coordinate != definition.Coordinate ||
                        !string.Equals(
                            runtimeChunk.ScenePath,
                            definition.ScenePath,
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            runtimeChunk.SourceFingerprint,
                            expectedFingerprint,
                            StringComparison.Ordinal))
                    {
                        throw new BuildFailedException(
                            "Generated chunk catalog entry " + chunkIndex +
                            " does not match '" + definition.ScenePath + "'.");
                    }

                    expectedBuildScenes.Add(definition.ScenePath);
                    ValidateWorldChunkScene(
                        manifest,
                        definition,
                        expectedFingerprint);
                }
            }

            string[] actualBuildScenes = EditorBuildSettings.scenes
                .Where(value => value.enabled)
                .Select(value => value.path)
                .ToArray();
            if (!actualBuildScenes.SequenceEqual(expectedBuildScenes))
            {
                throw new BuildFailedException(
                    "Enabled build scenes must be Bootstrap followed by the exact " +
                    "manifest-declared world chunks. Rebuild the playable foundation.");
            }
        }

        private static void ValidateWorldChunkScene(
            WorldZoneManifest zone,
            WorldZoneChunkDefinition definition,
            string expectedFingerprint)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(definition.ScenePath) == null)
            {
                throw new BuildFailedException(
                    "Missing generated world chunk scene '" +
                    definition.ScenePath + "'.");
            }

            Scene scene = SceneManager.GetSceneByPath(definition.ScenePath);
            bool opened = false;
            try
            {
                if (!scene.IsValid() || !scene.isLoaded)
                {
                    scene = EditorSceneManager.OpenScene(
                        definition.ScenePath,
                        OpenSceneMode.Additive);
                    opened = true;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                WorldChunkPresentation[] presentations =
                    GetSceneComponents<WorldChunkPresentation>(scene);
                if (roots.Length != 1 || presentations.Length != 1 ||
                    presentations[0].gameObject != roots[0] ||
                    presentations[0].PresentationVersion != WorldChunkAuthoring.ChunkPresentationVersion ||
                    !presentations[0].Matches(
                        zone.WorldId,
                        zone.ZoneId,
                        definition.Coordinate) ||
                    !string.Equals(
                        presentations[0].SourceFingerprint,
                        expectedFingerprint,
                        StringComparison.Ordinal))
                {
                    throw new BuildFailedException(
                        "World chunk '" + definition.ScenePath +
                        "' requires exactly one root with the manifest identity.");
                }

                if (GetSceneComponents<UnityEngine.Camera>(scene).Length != 0 ||
                    GetSceneComponents<AudioListener>(scene).Length != 0 ||
                    GetSceneComponents<EventSystem>(scene).Length != 0 ||
                    GetSceneComponents<PrototypeGameController>(scene).Length != 0 ||
                    GetSceneComponents<NetworkManager>(scene).Length != 0 ||
                    GetSceneComponents<Collider>(scene).Length != 0 ||
                    GetSceneComponents<Rigidbody>(scene).Length != 0)
                {
                    throw new BuildFailedException(
                        "World chunk '" + definition.ScenePath +
                        "' must remain presentation-only and cannot contain global " +
                        "systems or gameplay physics.");
                }

                Transform visualRoot = presentations[0].VisualRoot;
                if (definition.BiomeRecipeId == "sunbreak_basin")
                {
                    SunbreakBasinAuthoring.Validate(visualRoot);
                }
                else
                {
                    ValidateLevelingEnvironment(
                        visualRoot != null ? visualRoot.gameObject : null);
                }
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateWorldChunkRecipes(
            WorldZoneChunkDefinition chunk)
        {
            if (WorldBiomeRecipeCatalog.GetSupport(chunk.BiomeRecipeId) !=
                WorldBiomeRecipeSupport.Authored)
            {
                throw new BuildFailedException(
                    "World chunk " + chunk.Coordinate +
                    " requests unsupported biome " +
                    "recipe '" + chunk.BiomeRecipeId + "'.");
            }

            for (int recipeIndex = 0;
                 recipeIndex < chunk.PoiRecipeCount;
                 recipeIndex++)
            {
                string recipeId = chunk.GetPoiRecipeId(recipeIndex);
                if (WorldPoiRecipeCatalog.GetSupport(recipeId) !=
                    WorldPoiRecipeSupport.Authored)
                {
                    throw new BuildFailedException(
                        "World chunk " + chunk.Coordinate +
                        " requests unsupported POI recipe '" + recipeId + "'.");
                }
            }
        }

        private static void ValidateLevelingEnvironment(GameObject environment)
        {
            if (environment == null || environment.name != "LevelingEnvironment")
            {
                throw new BuildFailedException(
                    "A generated world chunk requires the authored " +
                    "LevelingEnvironment visual root.");
            }

            string[] requiredRoots =
            {
                "AshenCausewayFloor",
                "AshenCausewayCinderShoulders",
                "AshenCausewayEdgeDressing",
                "AshenCausewayLavaGullies",
                "AshenCausewayAtmosphere",
                "AshenCausewayLandmarks",
            };
            for (int rootIndex = 0; rootIndex < requiredRoots.Length; rootIndex++)
            {
                if (environment.transform.Find(requiredRoots[rootIndex]) == null)
                {
                    throw new BuildFailedException(
                        "LevelingEnvironment is missing authored root '" +
                        requiredRoots[rootIndex] + "'.");
                }
            }

            string[] landmarks =
            {
                "WestRefuge",
                "CinderApproach",
                "BrokenGate",
                "CollapsedCourt",
                "OssuaryBend",
                "ReliquaryYard",
                "EastWatch",
            };
            for (int index = 0; index < landmarks.Length; index++)
            {
                if (environment.transform.Find(
                        "AshenCausewayLandmarks/" + landmarks[index]) == null)
                {
                    throw new BuildFailedException(
                        "LevelingEnvironment is missing authored landmark '" +
                        landmarks[index] + "'.");
                }
            }

            if (environment.GetComponentsInChildren<Collider>(true).Length != 0)
            {
                throw new BuildFailedException(
                    "LevelingEnvironment must remain presentation-only because " +
                    "Core owns the route collision envelope.");
            }

            if (environment.GetComponentsInChildren<Rigidbody>(true).Length != 0)
            {
                throw new BuildFailedException(
                    "LevelingEnvironment must not contain presentation-owned rigidbodies.");
            }

            long triangleCount = 0;
            MeshFilter[] filters = environment.GetComponentsInChildren<MeshFilter>(true);
            for (int filterIndex = 0; filterIndex < filters.Length; filterIndex++)
            {
                MeshFilter filter = filters[filterIndex];
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    throw new BuildFailedException(
                        "LevelingEnvironment MeshFilter '" + filter.name +
                        "' is missing its authored mesh.");
                }

                Renderer renderer = filter.GetComponent<Renderer>();
                if (renderer == null)
                {
                    throw new BuildFailedException(
                        "LevelingEnvironment MeshFilter '" + filter.name +
                        "' requires a renderer on the same object.");
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials.Length != mesh.subMeshCount)
                {
                    throw new BuildFailedException(
                        "LevelingEnvironment MeshFilter '" + filter.name +
                        "' requires one material per submesh; found " +
                        materials.Length + " materials for " + mesh.subMeshCount +
                        " submeshes.");
                }

                for (int materialIndex = 0;
                     materialIndex < materials.Length;
                     materialIndex++)
                {
                    if (materials[materialIndex] == null)
                    {
                        throw new BuildFailedException(
                            "LevelingEnvironment renderer '" + renderer.name +
                            "' has a null material at slot " + materialIndex + ".");
                    }
                }

                for (int subMesh = 0; subMesh < mesh.subMeshCount; subMesh++)
                {
                    if (mesh.GetTopology(subMesh) != MeshTopology.Triangles)
                    {
                        throw new BuildFailedException(
                            "LevelingEnvironment mesh '" + mesh.name +
                            "' must use triangle topology in every submesh.");
                    }

                    triangleCount += (long)mesh.GetIndexCount(subMesh) / 3L;
                }
            }

            if (triangleCount <= 0 ||
                triangleCount > EnvironmentAuthoring.LevelingStaticTriangleBudget)
            {
                throw new BuildFailedException(
                    "LevelingEnvironment static triangle count must be in (0, " +
                    EnvironmentAuthoring.LevelingStaticTriangleBudget + "]; found " +
                    triangleCount + ".");
            }

            Light[] pointLights = environment
                .GetComponentsInChildren<Light>(true)
                .Where(light => light.type == LightType.Point)
                .ToArray();
            if (pointLights.Length > 4)
            {
                throw new BuildFailedException(
                    "LevelingEnvironment supports at most four authored point lights; found " +
                    pointLights.Length + ".");
            }

            for (int lightIndex = 0; lightIndex < pointLights.Length; lightIndex++)
            {
                if (pointLights[lightIndex].shadows != LightShadows.None)
                {
                    throw new BuildFailedException(
                        "LevelingEnvironment point light '" +
                        pointLights[lightIndex].name +
                        "' must keep realtime shadows disabled.");
                }
            }

            Transform floorRoot = environment.transform.Find("AshenCausewayFloor");
            Renderer floorRenderer = floorRoot.GetComponent<Renderer>();
            if (floorRenderer == null || floorRenderer.sharedMaterials.Length == 0)
            {
                throw new BuildFailedException(
                    "AshenCausewayFloor requires its authored renderer and materials.");
            }

            Material[] floorMaterials = floorRenderer.sharedMaterials;
            for (int materialIndex = 0;
                 materialIndex < floorMaterials.Length;
                 materialIndex++)
            {
                Material material = floorMaterials[materialIndex];
                bool isOpaqueLit =
                    material.shader != null &&
                    material.shader.name == "Universal Render Pipeline/Lit" &&
                    material.HasProperty("_Surface") &&
                    material.GetFloat("_Surface") == 0f;
                bool hasSurfaceMaps =
                    material.HasProperty("_BaseMap") &&
                    material.GetTexture("_BaseMap") != null &&
                    material.HasProperty("_BumpMap") &&
                    material.GetTexture("_BumpMap") != null;
                if (!isOpaqueLit || !hasSurfaceMaps)
                {
                    throw new BuildFailedException(
                        "AshenCausewayFloor material '" + material.name +
                        "' must be opaque URP Lit with authored BaseMap and BumpMap textures.");
                }
            }

            Transform lavaRoot = environment.transform.Find(
                "AshenCausewayLavaGullies");
            if (lavaRoot.GetComponentInChildren<HazardSurfaceAnimator>(true) == null)
            {
                throw new BuildFailedException(
                    "AshenCausewayLavaGullies requires its low-cost surface animator.");
            }

            Transform atmosphereRoot = environment.transform.Find(
                "AshenCausewayAtmosphere");
            ParticleSystem[] atmosphereParticles = atmosphereRoot
                .GetComponentsInChildren<ParticleSystem>(true);
            if (atmosphereParticles.Length == 0)
            {
                throw new BuildFailedException(
                    "AshenCausewayAtmosphere requires an authored particle system.");
            }

            for (int particleIndex = 0;
                 particleIndex < atmosphereParticles.Length;
                 particleIndex++)
            {
                ParticleSystem particles = atmosphereParticles[particleIndex];
                if (particles.main.maxParticles > 72)
                {
                    throw new BuildFailedException(
                        "AshenCauseway atmosphere particle system '" +
                        particles.name + "' exceeds the 72-particle mobile cap.");
                }

                if (particles.collision.enabled)
                {
                    throw new BuildFailedException(
                        "AshenCauseway atmosphere particle system '" +
                        particles.name + "' must keep collision disabled.");
                }
            }
        }

        private static void BuildScene(
            AuthoringAssets assets,
            GameObject combatantPrefab,
            CombatSfxPalette audioPalette,
            WorldZoneCatalogAsset worldCatalog)
        {
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Bootstrap";

            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.14f, 0.15f, 0.19f, 1f);
            RenderSettings.ambientEquatorColor = new Color(0.22f, 0.13f, 0.09f, 1f);
            RenderSettings.ambientGroundColor = new Color(0.04f, 0.045f, 0.06f, 1f);
            RenderSettings.ambientIntensity = 0.8f;
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.042f, 0.024f, 0.029f, 1f);
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0065f;
            RenderSettings.subtractiveShadowColor = new Color(0.045f, 0.05f, 0.075f, 1f);
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
            RenderSettings.customReflection = assets.EnvironmentReflection;
            RenderSettings.reflectionIntensity = 0.5f;
            RenderSettings.reflectionBounces = 1;

            CreateEnvironmentLookVolume(assets.EnvironmentLookProfile);

            CameraComposition camera = CreateCamera();
            ArenaVariantManifest arenaManifest = ArenaVariantManifest.LoadDefault();
            ArenaCatalogAsset arenaCatalog = BuildArenaCatalog(
                arenaManifest,
                assets,
                out GameObject arena,
                out GameObject boss);
            Light environmentKey = CreateLights(arena.transform);
            Transform arenaContentRoot = new GameObject("RuntimeArenaContent").transform;
            ArenaContentRuntimeLoader arenaContentLoader = arenaContentRoot.gameObject
                .AddComponent<ArenaContentRuntimeLoader>();
            arenaContentLoader.ConfigureForAuthoring(
                arenaCatalog,
                arena.GetComponent<ArenaPresentation>(),
                boss.GetComponent<ArenaPresentation>(),
                arenaContentRoot);
            CombatantView[] views = CreateCombatantPool(combatantPrefab);
            TelegraphPresenter[] telegraphs = new TelegraphPresenter[4];
            for (int index = 0; index < telegraphs.Length; index++)
            {
                telegraphs[index] = CreateTelegraph("BossTelegraph_" + index, assets.Telegraph);
            }

            CombatEffectsPresenter combatEffects = CombatEffectsAuthoring.Build(
                assets.CombatEffect,
                assets.CombatFragment,
                assets.CombatRibbon,
                assets.CombatParticle,
                assets.Font,
                audioPalette);
            combatEffects.SetGameCamera(camera.Camera);

            AuthoredHud hud = HudAuthoring.Build(assets.Font);
            KitViewerPanel kitViewer = hud.Root.GetComponentInChildren<KitViewerPanel>(true);
            PauseSettingsPanel pauseSettings =
                hud.Root.GetComponentInChildren<PauseSettingsPanel>(true);
            CreateEventSystem();
            SoftTargetSelectionService targeting = new GameObject(
                "SoftTargetSelection",
                typeof(SoftTargetSelectionService)).GetComponent<SoftTargetSelectionService>();
            SerializedObject serializedTargeting = new SerializedObject(targeting);
            serializedTargeting.FindProperty("_targetLayers").intValue = 1 << 9;
            serializedTargeting.FindProperty("_occlusionLayers").intValue = 1 << 8;
            serializedTargeting.ApplyModifiedPropertiesWithoutUndo();
            CreateNetworking();

            GameObject compositionRoot = new GameObject(
                "SessionRpgComposition",
                typeof(PrototypeGameController),
                typeof(MobileQualityController),
                typeof(WorldZoneStreamController),
                typeof(WorldLightingPresenter),
                typeof(LocalProductServices));
            PrototypeGameController controller = compositionRoot.GetComponent<PrototypeGameController>();
            MobileQualityController qualityController =
                compositionRoot.GetComponent<MobileQualityController>();
            WorldZoneStreamController worldStreamer =
                compositionRoot.GetComponent<WorldZoneStreamController>();
            WorldLightingPresenter worldLighting =
                compositionRoot.GetComponent<WorldLightingPresenter>();
            LocalProductServices productServices =
                compositionRoot.GetComponent<LocalProductServices>();
            qualityController.ConfigureForAuthoring(assets.MobileQuality.Catalog);
            worldStreamer.ConfigureForAuthoring(worldCatalog);
            GameObject sunbreakVista = SunbreakVistaAuthoring.Build(compositionRoot.transform,
                assets.SunbreakMeadow, SunbreakLandscapeSurfaceAuthoring.Configure(assets.SunbreakRock, assets.SunbreakMeadow));
            worldLighting.ConfigureForAuthoring(environmentKey, SunbreakVistaAuthoring.BuildSky(), sunbreakVista);
            SerializedObject serialized = new SerializedObject(controller);
            SetObject(serialized, "_gameCamera", camera.Camera);
            SetObject(serialized, "_cameraRig", camera.Rig);
            SetObject(serialized, "_inputSource", hud.InputSource);
            SetObject(serialized, "_targeting", targeting);
            SetObject(serialized, "_hud", hud.Hud);
            SetObject(
                serialized,
                "_offscreenThreats",
                hud.OffscreenThreatPresenter);
            SetObject(serialized, "_kitViewer", kitViewer);
            SetObject(serialized, "_pauseSettings", pauseSettings);
            SetObject(serialized, "_productServices", productServices);
            SetArray(serialized, "_combatantViews", views.Cast<UnityEngine.Object>().ToArray());
            SetArray(serialized, "_telegraphs", telegraphs.Cast<UnityEngine.Object>().ToArray());
            SetObject(serialized, "_combatEffects", combatEffects);
            SetObject(serialized, "_arenaEnvironment", arena);
            SetObject(serialized, "_bossEnvironment", boss);
            SetObject(serialized, "_arenaContentLoader", arenaContentLoader);
            SetObject(serialized, "_worldStreamer", worldStreamer);
            SetObject(serialized, "_worldLighting", worldLighting);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            if (arenaCatalog == null)
            {
                throw new InvalidOperationException("Arena catalog authoring returned no asset.");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                throw new InvalidOperationException("Unity could not save " + ScenePath + ".");
            }
        }

        private static GameObject BuildArenaPresentationPrefab(
            GameObject source,
            string prefabPath,
            ArenaContentId contentId,
            ArenaContentMode mode,
            bool hasAuthoritativeCover,
            int presentationVersion)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            EnsureAssetFolder(System.IO.Path.GetDirectoryName(prefabPath)?.Replace('\\', '/'));

            Transform visualRoot = new GameObject("Visuals").transform;
            visualRoot.SetParent(source.transform, false);
            Transform proxyRoot = new GameObject("GameplayProxies").transform;
            proxyRoot.SetParent(source.transform, false);
            Transform groundRoot = new GameObject("FlatGround").transform;
            groundRoot.SetParent(source.transform, false);
            Transform hazardSurfaceRoot = new GameObject("HazardSurfaces").transform;
            hazardSurfaceRoot.SetParent(source.transform, false);

            MoveArenaChildrenIntoContractRoots(
                source.transform,
                visualRoot,
                proxyRoot,
                groundRoot,
                hazardSurfaceRoot);

            Transform captureRoot = new GameObject("CaptureAnchors").transform;
            captureRoot.SetParent(source.transform, false);
            float strategyYaw;
            float strategyPitch;
            float strategyDistance;
            float strategyFieldOfView;
            Vector3 strategyFocus;
            Vector3 overviewPosition;
            switch (mode)
            {
                case ArenaContentMode.Arena:
                    strategyYaw = 90f;
                    strategyPitch = StrategyFollowCamera.ArenaPitch;
                    strategyDistance = StrategyFollowCamera.ArenaDistance;
                    strategyFieldOfView = StrategyFollowCamera.ArenaFieldOfView;
                    strategyFocus = (Vector3.right * 2.3f) + (Vector3.up * 0.9f);
                    overviewPosition = new Vector3(0f, 20f, -18f);
                    break;

                case ArenaContentMode.Boss:
                    strategyYaw = 0f;
                    strategyPitch = StrategyFollowCamera.BossPitch;
                    strategyDistance = StrategyFollowCamera.BossDistance;
                    strategyFieldOfView = StrategyFollowCamera.BossFieldOfView;
                    strategyFocus = (Vector3.forward * 2.3f) + (Vector3.up * 0.9f);
                    overviewPosition = new Vector3(0f, 20f, -18f);
                    break;

                case ArenaContentMode.Siege:
                    strategyYaw = SiegeCaptureYaw;
                    strategyPitch = SiegeCapturePitch;
                    strategyDistance = SiegeCaptureDistance;
                    strategyFieldOfView = SiegeCaptureFieldOfView;
                    strategyFocus = Vector3.up * 0.9f;
                    overviewPosition = new Vector3(-22f, 28f, -22f);
                    break;

                default:
                    throw new InvalidOperationException(
                        "Cannot author capture metadata for arena content mode '" +
                        mode + "'.");
            }

            Quaternion strategyRotation = Quaternion.Euler(
                strategyPitch,
                strategyYaw,
                0f);
            Vector3 strategyPosition =
                strategyFocus +
                (strategyRotation * Vector3.back * strategyDistance);
            ArenaCaptureAnchor[] captureAnchors =
            {
                CreateArenaCaptureAnchor(
                    captureRoot,
                    ArenaCaptureAnchorKind.Overview,
                    overviewPosition,
                    new Vector3(0f, 0f, 0f),
                    mode == ArenaContentMode.Siege ? SiegeCaptureFieldOfView : 45f,
                    false),
                CreateArenaCaptureAnchor(
                    captureRoot,
                    ArenaCaptureAnchorKind.Gameplay,
                    strategyPosition,
                    strategyFocus,
                    strategyFieldOfView,
                    true),
                CreateArenaCaptureAnchor(
                    captureRoot,
                    ArenaCaptureAnchorKind.DenseCombat,
                    strategyPosition,
                    strategyFocus,
                    strategyFieldOfView,
                    true),
            };

            ArenaPresentationCapabilities capabilities =
                ArenaPresentationCapabilities.FlatCombatGround |
                ArenaPresentationCapabilities.HazardBoundary;
            if (hasAuthoritativeCover)
            {
                capabilities |= ArenaPresentationCapabilities.AuthoritativeCover;
            }

            ArenaContentAuthoring.ConfigurePresentation(
                source,
                contentId,
                mode,
                capabilities,
                visualRoot,
                proxyRoot,
                groundRoot,
                0f,
                presentationVersion,
                captureAnchors);

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            if (prefab == null)
            {
                throw new InvalidOperationException(
                    "Unity could not save arena presentation prefab at '" + prefabPath + "'.");
            }

            UnityEngine.Object.DestroyImmediate(source);
            GameObject connectedInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (connectedInstance == null)
            {
                throw new InvalidOperationException(
                    "Unity could not instantiate arena presentation prefab from '" +
                    prefabPath + "'.");
            }

            return connectedInstance;
        }

        private static void MoveArenaChildrenIntoContractRoots(
            Transform presentationRoot,
            Transform visualRoot,
            Transform proxyRoot,
            Transform groundRoot,
            Transform hazardSurfaceRoot)
        {
            const string arenaSouthPillarName = "ArenaPillar_South";
            const string arenaNorthPillarName = "ArenaPillar_North";
            Transform[] children = new Transform[presentationRoot.childCount];
            for (int index = 0; index < children.Length; index++)
            {
                children[index] = presentationRoot.GetChild(index);
            }

            for (int index = 0; index < children.Length; index++)
            {
                Transform child = children[index];
                if (child == visualRoot || child == proxyRoot || child == groundRoot ||
                    child == hazardSurfaceRoot)
                {
                    continue;
                }

                bool isGround = child.gameObject.layer == ArenaContentValidator.GroundSurfaceLayer &&
                    (child.name == "SafeStone" || child.name == "RaidFloor");
                bool isHazardSurface = child.name == "Lava";
                bool isCover = child.name == arenaSouthPillarName ||
                    child.name == arenaNorthPillarName;
                child.SetParent(
                    isGround
                        ? groundRoot
                        : isHazardSurface
                            ? hazardSurfaceRoot
                            : visualRoot,
                    true);

                if (isCover && child.TryGetComponent(out CapsuleCollider sourceCollider))
                {
                    GameObject proxy = new GameObject(
                        child.name,
                        typeof(CapsuleCollider));
                    proxy.layer = ArenaContentValidator.WorldObstacleLayer;
                    proxy.transform.SetParent(proxyRoot, false);
                    proxy.transform.position = child.position;
                    CapsuleCollider collider = proxy.GetComponent<CapsuleCollider>();
                    collider.direction = sourceCollider.direction;
                    collider.radius = sourceCollider.radius;
                    collider.height = sourceCollider.height;
                    collider.center = sourceCollider.center;
                    UnityEngine.Object.DestroyImmediate(sourceCollider);
                    child.name += "_Visual";
                }
            }

            SetLayerRecursively(visualRoot.gameObject, 2);
            SetLayerRecursively(groundRoot.gameObject, ArenaContentValidator.GroundSurfaceLayer);
            SetLayerRecursively(
                hazardSurfaceRoot.gameObject,
                ArenaContentValidator.GroundSurfaceLayer);
        }

        private static ArenaCaptureAnchor CreateArenaCaptureAnchor(
            Transform parent,
            ArenaCaptureAnchorKind kind,
            Vector3 position,
            Vector3 lookAt,
            float fieldOfView,
            bool showHud)
        {
            GameObject root = new GameObject(kind + "CaptureAnchor", typeof(ArenaCaptureAnchor));
            root.transform.SetParent(parent, false);
            root.transform.position = position;
            root.transform.rotation = Quaternion.LookRotation(
                (lookAt - position).normalized,
                Vector3.up);
            ArenaCaptureAnchor anchor = root.GetComponent<ArenaCaptureAnchor>();
            ArenaContentAuthoring.ConfigureCaptureAnchor(
                anchor,
                kind,
                fieldOfView,
                showHud);
            return anchor;
        }

        private static ArenaCatalogAsset BuildArenaCatalog(
            ArenaVariantManifest manifest,
            AuthoringAssets assets,
            out GameObject defaultArena,
            out GameObject defaultBoss)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            var entries = new List<ArenaCatalogEntryAuthoring>(manifest.Count);
            defaultArena = null;
            defaultBoss = null;
            for (int index = 0; index < manifest.Count; index++)
            {
                ArenaVariantDefinition variant = manifest.GetEntry(index);
                GameObject source = BuildArenaVariantSource(variant, assets);
                GameObject instance = BuildArenaPresentationPrefab(
                    source,
                    variant.OutputPrefabPath,
                    variant.ContentId,
                    variant.Mode,
                    variant.Mode == ArenaContentMode.Arena,
                    variant.PresentationVersion);
                ArenaPresentation prefab = AssetDatabase
                    .LoadAssetAtPath<GameObject>(variant.OutputPrefabPath)
                    ?.GetComponent<ArenaPresentation>();
                if (prefab == null)
                {
                    throw new InvalidOperationException(
                        "Authored arena presentation prefab is missing at '" +
                        variant.OutputPrefabPath + "'.");
                }

                entries.Add(ArenaCatalogEntryAuthoring.ForPrefab(
                    variant.ContentId,
                    variant.Mode,
                    prefab,
                    variant.IsDefaultForMode));
                if (variant.IsDefaultForMode && variant.Mode == ArenaContentMode.Arena)
                {
                    defaultArena = instance;
                }
                else if (variant.IsDefaultForMode && variant.Mode == ArenaContentMode.Boss)
                {
                    defaultBoss = instance;
                }
                else
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            if (defaultArena == null || defaultBoss == null)
            {
                throw new InvalidOperationException(
                    "Arena variant manifest must provide one embedded default for Arena and Boss.");
            }

            ArenaCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ArenaCatalogAsset>(
                ArenaCatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<ArenaCatalogAsset>();
                catalog.name = "ArenaCatalog";
                AssetDatabase.CreateAsset(catalog, ArenaCatalogPath);
            }

            ArenaContentAuthoring.ReplaceCatalogEntries(catalog, entries.ToArray());
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        private static GameObject BuildArenaVariantSource(
            ArenaVariantDefinition variant,
            AuthoringAssets assets)
        {
            bool arenaRecipe =
                variant.GameplayLayoutId == ArenaLayoutRecipe &&
                variant.GeneratorRecipe == RiftCrucibleRecipe &&
                variant.Mode == ArenaContentMode.Arena;
            if (arenaRecipe)
            {
                return EnvironmentAuthoring.BuildArena(
                    assets.FortressSourceArt,
                    assets.ArenaFloorSourceArt,
                    assets.Stone,
                    assets.ArenaFloorStone,
                    assets.ArenaFloorTrim,
                    assets.ArenaFloorGrout,
                    assets.RuinStone,
                    assets.Lava,
                    assets.LavaSeam,
                    assets.Abyss,
                    assets.Blue,
                    assets.Red,
                    assets.FloorAccent,
                    assets.CombatParticle);
            }

            bool bossRecipe =
                variant.GameplayLayoutId == BossLayoutRecipe &&
                variant.GeneratorRecipe == EmberVaultRecipe &&
                variant.Mode == ArenaContentMode.Boss;
            if (bossRecipe)
            {
                return EnvironmentAuthoring.BuildBoss(
                    assets.FortressSourceArt,
                    assets.ArenaFloorSourceArt,
                    assets.Stone,
                    assets.ArenaFloorStone,
                    assets.ArenaFloorTrim,
                    assets.ArenaFloorGrout,
                    assets.RuinStone,
                    assets.Lava,
                    assets.LavaSeam,
                    assets.Abyss,
                    assets.Blue,
                    assets.Red,
                    assets.CombatParticle);
            }

            bool siegeRecipe =
                variant.GameplayLayoutId == SiegeLayoutRecipe &&
                variant.GeneratorRecipe == AshenStrongholdRecipe &&
                variant.Mode == ArenaContentMode.Siege;
            if (siegeRecipe)
            {
                return EnvironmentAuthoring.BuildHeroSiege(
                    assets.FortressSourceArt,
                    assets.ArenaFloorSourceArt,
                    assets.Stone,
                    assets.ArenaFloorStone,
                    assets.ArenaFloorTrim,
                    assets.ArenaFloorGrout,
                    assets.RuinStone,
                    assets.Lava,
                    assets.LavaSeam,
                    assets.Abyss,
                    assets.Blue,
                    assets.Red,
                    assets.FloorAccent,
                    assets.CombatParticle);
            }

            throw new InvalidOperationException(
                "Arena variant '" + variant.ContentId.Value + "' requests unsupported " +
                "layout/recipe '" + variant.GameplayLayoutId + "/" +
                variant.GeneratorRecipe + "'. Add an explicit generator recipe before " +
                "cataloging the battleground.");
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder) || AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] segments = folder.Split('/');
            string parent = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string path = parent + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(path))
                {
                    AssetDatabase.CreateFolder(parent, segments[index]);
                }

                parent = path;
            }
        }

        private static void SetLayerRecursively(GameObject value, int layer)
        {
            value.layer = layer;
            for (int index = 0; index < value.transform.childCount; index++)
            {
                SetLayerRecursively(value.transform.GetChild(index).gameObject, layer);
            }
        }

        private static CameraComposition CreateCamera()
        {
            Quaternion arenaRotation = Quaternion.Euler(
                StrategyFollowCamera.ArenaPitch,
                StrategyFollowCamera.ArenaYaw,
                0f);
            Vector3 arenaForward = arenaRotation * Vector3.forward;
            arenaForward.y = 0f;
            arenaForward.Normalize();
            Vector3 arenaFocus =
                (arenaForward * StrategyFollowCamera.ArenaForwardFocusOffset) +
                (Vector3.up * 0.9f);
            Vector3 arenaPosition = arenaFocus +
                (arenaRotation * Vector3.back * StrategyFollowCamera.ArenaDistance);
            GameObject root = new GameObject(
                "GameplayCamera",
                typeof(UnityEngine.Camera),
                typeof(AudioListener),
                typeof(UniversalAdditionalCameraData),
                typeof(StrategyFollowCamera));
            root.tag = "MainCamera";
            root.transform.SetPositionAndRotation(
                arenaPosition,
                arenaRotation);
            UnityEngine.Camera camera = root.GetComponent<UnityEngine.Camera>();
            camera.orthographic = false;
            camera.fieldOfView = StrategyFollowCamera.ArenaFieldOfView;
            camera.nearClipPlane = 0.2f;
            camera.farClipPlane = 100f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.003f, 0.0045f, 0.01f, 1f);
            camera.allowHDR = true;
            camera.allowMSAA = true;
            camera.allowDynamicResolution = false;
            UniversalAdditionalCameraData cameraData =
                root.GetComponent<UniversalAdditionalCameraData>();
            cameraData.renderPostProcessing = true;
            cameraData.stopNaN = true;
            cameraData.dithering = true;
            cameraData.renderShadows = true;
            cameraData.volumeLayerMask = ~0;
            StrategyFollowCamera rig = root.GetComponent<StrategyFollowCamera>();
            SerializedObject serializedRig = new SerializedObject(rig);
            serializedRig.FindProperty("_yaw").floatValue = StrategyFollowCamera.ArenaYaw;
            serializedRig.FindProperty("_orbitCenterYaw").floatValue =
                StrategyFollowCamera.ArenaYaw;
            serializedRig.FindProperty("_maximumOrbitOffset").floatValue =
                StrategyFollowCamera.ArenaMaximumOrbitOffset;
            serializedRig.FindProperty("_pitch").floatValue = StrategyFollowCamera.ArenaPitch;
            serializedRig.FindProperty("_followDistance").floatValue =
                StrategyFollowCamera.ArenaDistance;
            serializedRig.FindProperty("_pivotHeight").floatValue = 0.9f;
            serializedRig.FindProperty("_forwardFocusOffset").floatValue =
                StrategyFollowCamera.ArenaForwardFocusOffset;
            serializedRig.FindProperty("_rightFocusOffset").floatValue =
                StrategyFollowCamera.ArenaRightFocusOffset;
            serializedRig.FindProperty("_fieldOfView").floatValue =
                StrategyFollowCamera.ArenaFieldOfView;
            serializedRig.FindProperty("_followSmoothTime").floatValue = 0.1f;
            serializedRig.FindProperty("_maximumImpulseDistance").floatValue = 0.16f;
            serializedRig.ApplyModifiedPropertiesWithoutUndo();
            return new CameraComposition(camera, rig);
        }

        private static void CreateEnvironmentLookVolume(VolumeProfile profile)
        {
            if (profile == null)
            {
                throw new ArgumentNullException(nameof(profile));
            }

            GameObject root = new GameObject("ArenaEnvironmentLook", typeof(Volume));
            Volume volume = root.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        private static Light CreateLights(Transform arenaParent)
        {
            if (arenaParent == null)
            {
                throw new ArgumentNullException(nameof(arenaParent));
            }

            GameObject sunRoot = new GameObject("MoonKey", typeof(Light));
            sunRoot.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
            Light sun = sunRoot.GetComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(0.86f, 0.88f, 0.94f, 1f);
            sun.intensity = 1.95f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.86f;
            sun.shadowBias = 0.045f;
            sun.shadowNormalBias = 0.18f;
            RenderSettings.sun = sun;

            string[] lightNames =
            {
                "FortressGlowLeftFlank",
                "FortressGlowLeftShoulder",
                "FortressGlowRightShoulder",
                "FortressGlowRightFlank",
            };
            for (int index = 0; index < lightNames.Length; index++)
            {
                CreatePointLight(
                    lightNames[index],
                    EnvironmentAuthoring.GetArenaFireLightPosition(index),
                    arenaParent);
            }

            return sun;
        }

        private static void CreatePointLight(
            string name,
            Vector3 position,
            Transform parent)
        {
            GameObject root = new GameObject(name, typeof(Light));
            root.transform.SetParent(parent, true);
            root.transform.position = position;
            Light light = root.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.36f, 0.08f, 1f);
            light.intensity = 9.5f;
            light.range = 8.25f;
            light.shadows = LightShadows.None;
        }

        private static CombatantView[] CreateCombatantPool(GameObject prefab)
        {
            Transform root = new GameObject("Combatants").transform;
            CombatantView[] values = new CombatantView[12];
            for (int index = 0; index < values.Length; index++)
            {
                GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                instance.name = "Combatant_" + (index + 1);
                instance.transform.SetParent(root, false);
                values[index] = instance.GetComponent<CombatantView>();
                instance.SetActive(false);
            }

            return values;
        }

        private static TelegraphPresenter CreateTelegraph(string name, Material material)
        {
            GameObject root = new GameObject(name, typeof(TelegraphPresenter));
            GameObject shape = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            shape.name = "SubtleFill";
            shape.transform.SetParent(root.transform, false);
            shape.transform.localScale = new Vector3(1f, 0.03f, 1f);
            UnityEngine.Object.DestroyImmediate(shape.GetComponent<Collider>());
            Renderer renderer = shape.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            ConfigureTelegraphRenderer(renderer);

            Transform accentRoot = new GameObject("ReadableMotif").transform;
            accentRoot.SetParent(root.transform, false);
            var accentRenderers = new List<Renderer>(32);

            GameObject circle = CreateTelegraphMotifRoot("Circle", accentRoot);
            accentRenderers.Add(CreateTelegraphRing(
                "Boundary", circle.transform, material, 0.49f, 0.027f, 48));

            GameObject shockwave = CreateTelegraphMotifRoot("Shockwave", accentRoot);
            accentRenderers.Add(CreateTelegraphRing(
                "InnerWave", shockwave.transform, material, 0.29f, 0.025f, 48));
            accentRenderers.Add(CreateTelegraphRing(
                "OuterWave", shockwave.transform, material, 0.49f, 0.04f, 48));

            GameObject frost = CreateTelegraphMotifRoot("FrostBurst", accentRoot);
            accentRenderers.Add(CreateTelegraphRing(
                "FrozenBoundary", frost.transform, material, 0.48f, 0.027f, 12));
            for (int index = 0; index < 6; index++)
            {
                accentRenderers.Add(CreateTelegraphBar(
                    "IceRay_" + index,
                    frost.transform,
                    material,
                    new Vector3(0f, 0.045f, 0.23f),
                    Quaternion.Euler(0f, index * 60f, 0f),
                    new Vector3(0.025f, 0.012f, 0.42f)));
            }

            GameObject flame = CreateTelegraphMotifRoot("FlameBurst", accentRoot);
            accentRenderers.Add(CreateTelegraphJaggedRing(
                "FlameEdge", flame.transform, material, 0.4f, 0.5f, 0.038f, 16));
            accentRenderers.Add(CreateTelegraphRing(
                "FlameCore", flame.transform, material, 0.22f, 0.023f, 8));

            GameObject psychic = CreateTelegraphMotifRoot("PsychicBurst", accentRoot);
            for (int index = 0; index < 3; index++)
            {
                LineRenderer spiral = CreateTelegraphSpiral(
                    "MindSpiral_" + index,
                    psychic.transform,
                    material,
                    index * 120f);
                accentRenderers.Add(spiral);
            }

            GameObject targetLock = CreateTelegraphMotifRoot("TargetLock", accentRoot);
            accentRenderers.Add(CreateTelegraphRing(
                "TargetDiamond", targetLock.transform, material, 0.46f, 0.04f, 4));
            accentRenderers.Add(CreateTelegraphRing(
                "TargetCore", targetLock.transform, material, 0.18f, 0.023f, 4));

            GameObject meteor = CreateTelegraphMotifRoot("MeteorImpact", accentRoot);
            accentRenderers.Add(CreateTelegraphRing(
                "ImpactBoundary", meteor.transform, material, 0.48f, 0.04f, 32));
            accentRenderers.Add(CreateTelegraphRing(
                "ImpactCore", meteor.transform, material, 0.2f, 0.026f, 32));
            accentRenderers.Add(CreateTelegraphBar(
                "CrossEastWest",
                meteor.transform,
                material,
                new Vector3(0f, 0.045f, 0f),
                Quaternion.identity,
                new Vector3(0.72f, 0.014f, 0.025f)));
            accentRenderers.Add(CreateTelegraphBar(
                "CrossNorthSouth",
                meteor.transform,
                material,
                new Vector3(0f, 0.045f, 0f),
                Quaternion.identity,
                new Vector3(0.025f, 0.014f, 0.72f)));

            TelegraphPresenter presenter = root.GetComponent<TelegraphPresenter>();
            SerializedObject serialized = new SerializedObject(presenter);
            SetObject(serialized, "_shape", shape.transform);
            SetObject(serialized, "_renderer", renderer);
            SetObject(serialized, "_accentRoot", accentRoot);
            SetObject(serialized, "_circleMotif", circle);
            SetObject(serialized, "_shockwaveMotif", shockwave);
            SetObject(serialized, "_frostMotif", frost);
            SetObject(serialized, "_flameMotif", flame);
            SetObject(serialized, "_psychicMotif", psychic);
            SetObject(serialized, "_targetLockMotif", targetLock);
            SetObject(serialized, "_meteorMotif", meteor);
            SetArray(
                serialized,
                "_accentRenderers",
                accentRenderers.Cast<UnityEngine.Object>().ToArray());
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return presenter;
        }

        private static GameObject CreateTelegraphMotifRoot(string name, Transform parent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.SetActive(false);
            return root;
        }

        private static LineRenderer CreateTelegraphRing(
            string name,
            Transform parent,
            Material material,
            float radius,
            float width,
            int corners)
        {
            GameObject root = new GameObject(name, typeof(LineRenderer));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            LineRenderer line = root.GetComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.positionCount = corners;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            for (int index = 0; index < corners; index++)
            {
                float angle = (index / (float)corners) * Mathf.PI * 2f;
                line.SetPosition(index, new Vector3(
                    Mathf.Sin(angle) * radius,
                    0f,
                    Mathf.Cos(angle) * radius));
            }

            line.sharedMaterial = material;
            ConfigureTelegraphRenderer(line);
            return line;
        }

        private static LineRenderer CreateTelegraphJaggedRing(
            string name,
            Transform parent,
            Material material,
            float innerRadius,
            float outerRadius,
            float width,
            int points)
        {
            LineRenderer line = CreateTelegraphRing(
                name,
                parent,
                material,
                innerRadius,
                width,
                points);
            for (int index = 0; index < points; index++)
            {
                float angle = (index / (float)points) * Mathf.PI * 2f;
                float radius = index % 2 == 0 ? outerRadius : innerRadius;
                line.SetPosition(index, new Vector3(
                    Mathf.Sin(angle) * radius,
                    0f,
                    Mathf.Cos(angle) * radius));
            }

            return line;
        }

        private static LineRenderer CreateTelegraphSpiral(
            string name,
            Transform parent,
            Material material,
            float rotationDegrees)
        {
            const int points = 26;
            GameObject root = new GameObject(name, typeof(LineRenderer));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0.045f, 0f);
            root.transform.localRotation = Quaternion.Euler(0f, rotationDegrees, 0f);
            LineRenderer line = root.GetComponent<LineRenderer>();
            line.loop = false;
            line.useWorldSpace = false;
            line.alignment = LineAlignment.View;
            line.positionCount = points;
            line.widthMultiplier = 0.027f;
            line.numCapVertices = 3;
            line.numCornerVertices = 2;
            for (int index = 0; index < points; index++)
            {
                float progress = index / (float)(points - 1);
                float angle = progress * Mathf.PI * 1.55f;
                float radius = Mathf.Lerp(0.08f, 0.48f, progress);
                line.SetPosition(index, new Vector3(
                    Mathf.Sin(angle) * radius,
                    0f,
                    Mathf.Cos(angle) * radius));
            }

            line.sharedMaterial = material;
            ConfigureTelegraphRenderer(line);
            return line;
        }

        private static Renderer CreateTelegraphBar(
            string name,
            Transform parent,
            Material material,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale)
        {
            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            bar.transform.SetParent(parent, false);
            bar.transform.localPosition = localPosition;
            bar.transform.localRotation = localRotation;
            bar.transform.localScale = localScale;
            UnityEngine.Object.DestroyImmediate(bar.GetComponent<Collider>());
            Renderer renderer = bar.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            ConfigureTelegraphRenderer(renderer);
            return renderer;
        }

        private static void ConfigureTelegraphRenderer(Renderer renderer)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void CreateEventSystem()
        {
            GameObject root = new GameObject(
                "EventSystem",
                typeof(EventSystem),
                typeof(InputSystemUIInputModule));
            root.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private static void CreateNetworking()
        {
            GameObject root = new GameObject(
                "Networking",
                typeof(NetworkManager),
                typeof(UnityTransport),
                typeof(NetworkSessionController));
            NetworkManager manager = root.GetComponent<NetworkManager>();
            UnityTransport transport = root.GetComponent<UnityTransport>();
            manager.NetworkConfig.NetworkTransport = transport;
            manager.NetworkConfig.EnableSceneManagement = false;
            manager.NetworkConfig.TickRate = 30;

            NetworkSessionController controller = root.GetComponent<NetworkSessionController>();
            SerializedObject serialized = new SerializedObject(controller);
            SetObject(serialized, "_networkManager", manager);
            SetObject(serialized, "_transport", transport);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ConfigureProject(AuthoringAssets assets)
        {
            PlayerSettings.companyName = "Session RPG Prototype";
            PlayerSettings.productName = "Session RPG Prototype";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.runInBackground = false;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;

            PlayerSettings.SetApplicationIdentifier(
                UnityEditor.Build.NamedBuildTarget.Android,
                "com.sessionrpg.prototype");
            PlayerSettings.SetScriptingBackend(
                UnityEditor.Build.NamedBuildTarget.Android,
                ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            QualitySettings.antiAliasing = 2;
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = 60;

            UnityEngine.Object[] settingsAssets =
                AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settingsAssets.Length > 0)
            {
                SerializedObject settings = new SerializedObject(settingsAssets[0]);
                SerializedProperty inputHandler = settings.FindProperty("activeInputHandler");
                if (inputHandler != null)
                {
                    inputHandler.intValue = 1;
                }

                settings.ApplyModifiedPropertiesWithoutUndo();
            }

            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
            {
                new EditorBuildSettingsScene(ScenePath, true),
            };
            WorldZoneCatalog worldCatalog = WorldZoneCatalog.LoadDefault();
            WorldZoneManifest[] worldZones =
                worldCatalog.LoadValidatedZones();
            for (int zoneIndex = 0; zoneIndex < worldZones.Length; zoneIndex++)
            {
                WorldZoneManifest zone = worldZones[zoneIndex];
                for (int chunkIndex = 0;
                     chunkIndex < zone.ChunkCount;
                     chunkIndex++)
                {
                    scenes.Add(new EditorBuildSettingsScene(
                        zone.GetChunk(chunkIndex).ScenePath,
                        true));
                }
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static void ValidateArenaContentFoundation()
        {
            ArenaVariantManifest manifest = ArenaVariantManifest.LoadDefault();
            ArenaCatalogAsset catalog = AssetDatabase.LoadAssetAtPath<ArenaCatalogAsset>(
                ArenaCatalogPath);
            if (catalog == null)
            {
                throw new BuildFailedException(
                    "Missing ArenaCatalog.asset. Rebuild the playable foundation.");
            }

            ArenaContentValidator.ValidateCatalogOrThrow(catalog);
            if (catalog.Count != manifest.Count)
            {
                throw new BuildFailedException(
                    "Arena catalog has " + catalog.Count + " entries, but " +
                    ArenaVariantManifest.DefaultProjectRelativePath + " declares " +
                    manifest.Count + ". Rebuild the playable foundation.");
            }

            for (int index = 0; index < manifest.Count; index++)
            {
                ArenaVariantDefinition variant = manifest.GetEntry(index);
                if (!catalog.TryGet(variant.ContentId, out ArenaCatalogEntry entry) ||
                    entry.Mode != variant.Mode ||
                    entry.IsDefaultForMode != variant.IsDefaultForMode ||
                    !entry.HasPrefab ||
                    AssetDatabase.GetAssetPath(entry.PresentationPrefab) !=
                    variant.OutputPrefabPath ||
                    entry.PresentationPrefab.PresentationVersion !=
                    variant.PresentationVersion)
                {
                    throw new BuildFailedException(
                        "Arena catalog entry '" + variant.ContentId.Value +
                        "' does not match " +
                        ArenaVariantManifest.DefaultProjectRelativePath +
                        ". Rebuild the playable foundation.");
                }
            }
        }

        private static void ValidateRenderPipelineConfiguration()
        {
            UnityEngine.Rendering.RenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.RenderPipelineAsset>(
                    MobileQualityAssetAuthoring.BalancedPipelinePath);
            if (pipeline == null)
            {
                throw new BuildFailedException(
                    "Missing balanced mobile URP asset. Rebuild the playable foundation.");
            }

            MobileQualityCatalog catalog =
                AssetDatabase.LoadAssetAtPath<MobileQualityCatalog>(
                    MobileQualityAssetAuthoring.DefaultCatalogPath);
            if (catalog == null || !catalog.TryGetPipeline(
                    MobileQualityTier.Balanced,
                    out UnityEngine.Rendering.RenderPipelineAsset balancedPipeline) ||
                balancedPipeline != pipeline)
            {
                throw new BuildFailedException(
                    "Balanced URP is not assigned in the mobile quality catalog. " +
                    "Rebuild the playable foundation.");
            }
        }

        private static void ConfigureLayers()
        {
            UnityEngine.Object[] tagAssets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagAssets.Length == 0)
            {
                return;
            }

            SerializedObject tags = new SerializedObject(tagAssets[0]);
            SerializedProperty layers = tags.FindProperty("layers");
            if (layers != null && layers.arraySize > 10)
            {
                layers.GetArrayElementAtIndex(8).stringValue = "WorldObstacle";
                layers.GetArrayElementAtIndex(9).stringValue = "Combatant";
                layers.GetArrayElementAtIndex(10).stringValue = "GroundSurface";
                tags.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ValidateCombatantAnimation(GameObject prefab)
        {
            CombatantAnimationPresenter presenter =
                prefab.GetComponent<CombatantAnimationPresenter>();
            if (presenter == null)
            {
                throw new BuildFailedException(
                    "Combatant prefab has no locomotion presenter. Rebuild the playable foundation.");
            }

            HunterProductionAssetValidator.ValidateGeneratedPrefabOrThrow(prefab);
            MixamoPlayableClassTrialAssetValidator.ValidateGeneratedPrefabOrThrow(prefab);

            Animator[] animators = prefab.GetComponentsInChildren<Animator>(true);
            const int expectedAnimatorCount = 5;
            if (animators.Length != expectedAnimatorCount)
            {
                throw new BuildFailedException(
                    "Combatant prefab must contain four playable-class Animators " +
                    "and one boss Animator; found " + animators.Length + ".");
            }

            for (int index = 0; index < animators.Length; index++)
            {
                if (animators[index].runtimeAnimatorController == null)
                {
                    throw new BuildFailedException(
                        "Combatant Animator '" + animators[index].name + "' has no controller.");
                }

                if (animators[index].applyRootMotion)
                {
                    throw new BuildFailedException(
                        "Combatant Animator '" + animators[index].name +
                        "' must not apply animation root motion.");
                }

                bool boss = animators[index].name.IndexOf(
                    "BossModel",
                    StringComparison.Ordinal) >= 0;
                Avatar avatar = animators[index].avatar;
                if (avatar == null || !avatar.isValid || avatar.isHuman == boss)
                {
                    throw new BuildFailedException(
                        "Combatant Animator '" + animators[index].name + "' requires a valid " +
                        (boss ? "Generic" : "Humanoid") + " Avatar.");
                }

                if (!boss)
                {
                    CombatantAnimatorIkRelay relay =
                        animators[index].GetComponent<CombatantAnimatorIkRelay>();
                    if (relay == null || relay.Presenter != presenter)
                    {
                        throw new BuildFailedException(
                            "Combatant Animator '" + animators[index].name +
                            "' is not bound to its authored hand-animation relay.");
                    }

                    UnityEditor.Animations.AnimatorController controller =
                        animators[index].runtimeAnimatorController as
                            UnityEditor.Animations.AnimatorController;
                    if (controller == null ||
                        controller.layers.Length == 0 ||
                        !controller.layers[0].iKPass)
                    {
                        throw new BuildFailedException(
                            "Combatant Animator '" + animators[index].name +
                            "' requires IK on its base layer for authored hand choreography.");
                    }
                }
            }
        }

        private static void RequireExactlyOne<T>(Scene scene, string label)
            where T : Component
        {
            T[] values = GetSceneComponents<T>(scene);
            if (values.Length != 1)
            {
                throw new BuildFailedException(
                    "Bootstrap must contain exactly one " + label + "; found " + values.Length + ".");
            }
        }

        private static T[] GetSceneComponents<T>(Scene scene)
            where T : Component
        {
            List<T> values = new List<T>();
            GameObject[] roots = scene.GetRootGameObjects();
            for (int index = 0; index < roots.Length; index++)
            {
                values.AddRange(roots[index].GetComponentsInChildren<T>(true));
            }

            return values.ToArray();
        }

        private static void SetObject(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name + " is missing serialized field " + propertyName + ".");
            }

            property.objectReferenceValue = value;
        }

        private static void SetArray(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object[] values)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    serialized.targetObject.GetType().Name + " is missing serialized field " + propertyName + ".");
            }

            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private readonly struct CameraComposition
        {
            public CameraComposition(UnityEngine.Camera camera, StrategyFollowCamera rig)
            {
                Camera = camera;
                Rig = rig;
            }

            public UnityEngine.Camera Camera { get; }

            public StrategyFollowCamera Rig { get; }
        }
    }
}
