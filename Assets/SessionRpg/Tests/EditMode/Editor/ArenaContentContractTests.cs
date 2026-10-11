using System.IO;
using System.Linq;
using NUnit.Framework;
using SessionRpg.Editor.Arenas;
using SessionRpg.Editor.Assets;
using SessionRpg.Unity.Arenas;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Tests
{
    public sealed class ArenaContentContractTests
    {
        private const string TemporaryFolder =
            "Assets/SessionRpg/Tests/EditMode/Editor/GeneratedArenaContentTests";
        private const string TemporaryPrefabPath = TemporaryFolder + "/Arena.prefab";
        private const string TemporaryScenePath = TemporaryFolder + "/Arena.unity";
        private const string TemporaryHostScenePath = TemporaryFolder + "/Host.unity";
        private const string ArenaFloorTextureFolder =
            "Assets/SessionRpg/Art/Environment/Textures/";
        private const string MaterialFolder =
            "Assets/SessionRpg/Art/Materials/";
        private const string FortressMasonrySourceFolder =
            "Assets/ThirdParty/PolyHaven/StoneBrickWall001/";

        private GameObject _presentationRoot;
        private Transform _visualRoot;
        private Transform _proxyRoot;
        private Transform _groundRoot;
        private ArenaCaptureAnchor[] _anchors;
        private ArenaPresentation _presentation;

        [SetUp]
        public void SetUp()
        {
            _presentationRoot = new GameObject("Arena Presentation");
            _visualRoot = CreateChild(_presentationRoot.transform, "Visuals");
            _proxyRoot = CreateChild(_presentationRoot.transform, "Gameplay Proxies");
            _groundRoot = CreateChild(_proxyRoot, "Ground");
            _groundRoot.gameObject.layer = ArenaContentValidator.GroundSurfaceLayer;

            BoxCollider groundCollider =
                _groundRoot.gameObject.AddComponent<BoxCollider>();
            groundCollider.center = new Vector3(0f, -0.1f, 0f);
            groundCollider.size = new Vector3(20f, 0.2f, 20f);

            Transform captureRoot =
                CreateChild(_presentationRoot.transform, "Capture Anchors");
            _anchors = new[]
            {
                CreateAnchor(captureRoot, ArenaCaptureAnchorKind.Overview),
                CreateAnchor(captureRoot, ArenaCaptureAnchorKind.Gameplay),
                CreateAnchor(captureRoot, ArenaCaptureAnchorKind.DenseCombat),
            };

            _presentation = Configure(_anchors);
        }

        [TearDown]
        public void TearDown()
        {
            if (_presentationRoot != null)
            {
                Object.DestroyImmediate(_presentationRoot);
            }

            if (AssetDatabase.IsValidFolder(TemporaryFolder))
            {
                AssetDatabase.DeleteAsset(TemporaryFolder);
                AssetDatabase.Refresh();
            }
        }

        [TestCase("legacy_arena", true)]
        [TestCase("arena_2v2", true)]
        [TestCase("a", true)]
        [TestCase("", false)]
        [TestCase("Arena", false)]
        [TestCase("2v2_arena", false)]
        [TestCase("arena-hall", false)]
        [TestCase("arena hall", false)]
        [TestCase(" legacy_arena ", false)]
        public void ArenaContentId_UsesStableLowercaseAsciiContract(
            string value,
            bool expectedValid)
        {
            var contentId = new ArenaContentId(value);

            Assert.That(contentId.IsValid, Is.EqualTo(expectedValid));
            Assert.That(ArenaContentId.IsWellFormed(value), Is.EqualTo(expectedValid));
        }

        [Test]
        public void EmbeddedFlatPresentation_WithRequiredAnchors_IsValid()
        {
            ArenaContentValidationReport report =
                ArenaContentValidator.ValidatePresentation(_presentation);

            Assert.That(report.IsValid, Is.True, report.ToString());
        }

        [Test]
        public void PresentationNestedUnderAnotherObject_IsRejected()
        {
            var container = new GameObject("Scene Container");
            try
            {
                _presentationRoot.transform.SetParent(container.transform, false);

                ArenaContentValidationReport report =
                    ArenaContentValidator.ValidatePresentation(_presentation);

                Assert.That(
                    report.Contains(ArenaContentValidationCode.PresentationIsNotRoot),
                    Is.True,
                    report.ToString());
            }
            finally
            {
                Object.DestroyImmediate(container);
                _presentationRoot = null;
            }
        }

        [Test]
        public void MissingDenseCombatAnchor_IsRejected()
        {
            _presentation = Configure(new[] { _anchors[0], _anchors[1] });

            ArenaContentValidationReport report =
                ArenaContentValidator.ValidatePresentation(_presentation);

            Assert.That(
                report.Contains(ArenaContentValidationCode.MissingDenseCombatAnchor),
                Is.True,
                report.ToString());
        }

        [Test]
        public void VisualOnAuthoritativeGameplayLayer_IsRejected()
        {
            _visualRoot.gameObject.layer = ArenaContentValidator.WorldObstacleLayer;

            ArenaContentValidationReport report =
                ArenaContentValidator.ValidatePresentation(_presentation);

            Assert.That(
                report.Contains(ArenaContentValidationCode.VisualUsesAuthoritativeLayer),
                Is.True,
                report.ToString());
        }

        [Test]
        public void TiltedGroundCollider_IsRejected()
        {
            _groundRoot.localRotation = Quaternion.Euler(1f, 0f, 0f);

            ArenaContentValidationReport report =
                ArenaContentValidator.ValidatePresentation(_presentation);

            Assert.That(
                report.Contains(ArenaContentValidationCode.TiltedGroundCollider),
                Is.True,
                report.ToString());
        }

        [Test]
        public void CatalogAuthoring_SortsStableIdsAndReassignsModeDefault()
        {
            var catalog = ScriptableObject.CreateInstance<ArenaCatalogAsset>();
            try
            {
                ArenaContentAuthoring.ReplaceCatalogEntries(
                    catalog,
                    ArenaCatalogEntryAuthoring.ForScene(
                        new ArenaContentId("second_arena"),
                        ArenaContentMode.Arena,
                        "Assets/SecondArena.unity",
                        true),
                    ArenaCatalogEntryAuthoring.ForScene(
                        new ArenaContentId("first_arena"),
                        ArenaContentMode.Arena,
                        "Assets/FirstArena.unity"));

                Assert.That(catalog.Count, Is.EqualTo(2));
                Assert.That(catalog.GetEntry(0).ContentId.Value, Is.EqualTo("first_arena"));
                Assert.That(catalog.GetEntry(1).ContentId.Value, Is.EqualTo("second_arena"));

                ArenaContentAuthoring.UpsertCatalogEntry(
                    catalog,
                    ArenaCatalogEntryAuthoring.ForScene(
                        new ArenaContentId("first_arena"),
                        ArenaContentMode.Arena,
                        "Assets/FirstArena.unity",
                        true));

                Assert.That(
                    catalog.TryGetDefault(ArenaContentMode.Arena, out ArenaCatalogEntry entry),
                    Is.True);
                Assert.That(entry.ContentId.Value, Is.EqualTo("first_arena"));
                Assert.That(catalog.GetEntry(1).IsDefaultForMode, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void CatalogValidator_LoadsAndValidatesPresentationPrefab()
        {
            EnsureTemporaryFolder();
            GameObject saved =
                PrefabUtility.SaveAsPrefabAsset(_presentationRoot, TemporaryPrefabPath);
            Assert.That(saved, Is.Not.Null);

            ArenaPresentation prefab = saved.GetComponent<ArenaPresentation>();
            var catalog = ScriptableObject.CreateInstance<ArenaCatalogAsset>();
            try
            {
                ArenaContentAuthoring.ReplaceCatalogEntries(
                    catalog,
                    ArenaCatalogEntryAuthoring.ForPrefab(
                        new ArenaContentId("legacy_arena"),
                        ArenaContentMode.Arena,
                        prefab,
                        true));

                ArenaContentValidationReport report =
                    ArenaContentValidator.ValidateCatalog(catalog);

                Assert.That(report.IsValid, Is.True, report.ToString());
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void CatalogValidator_OpensAndValidatesAdditivePresentationScene()
        {
            EnsureTemporaryFolder();
            Scene originalScene = SceneManager.GetActiveScene();
            Assert.That(
                EditorSceneManager.SaveScene(originalScene, TemporaryHostScenePath),
                Is.True);
            Scene arenaScene = EditorSceneManager.NewScene(
                NewSceneSetup.EmptyScene,
                NewSceneMode.Additive);
            SceneManager.MoveGameObjectToScene(_presentationRoot, arenaScene);
            Assert.That(EditorSceneManager.SaveScene(originalScene), Is.True);
            Assert.That(
                EditorSceneManager.SaveScene(arenaScene, TemporaryScenePath),
                Is.True);
            SceneManager.SetActiveScene(originalScene);
            Assert.That(EditorSceneManager.CloseScene(arenaScene, true), Is.True);
            _presentationRoot = null;

            SceneAsset sceneAsset =
                AssetDatabase.LoadAssetAtPath<SceneAsset>(TemporaryScenePath);
            Assert.That(sceneAsset, Is.Not.Null);

            var catalog = ScriptableObject.CreateInstance<ArenaCatalogAsset>();
            try
            {
                ArenaContentAuthoring.ReplaceCatalogEntries(
                    catalog,
                    ArenaCatalogEntryAuthoring.ForScene(
                        new ArenaContentId("legacy_arena"),
                        ArenaContentMode.Arena,
                        sceneAsset,
                        true));

                ArenaContentValidationReport report =
                    ArenaContentValidator.ValidateCatalog(catalog);

                Assert.That(report.IsValid, Is.True, report.ToString());
                Assert.That(
                    SceneManager.GetSceneByPath(TemporaryScenePath).isLoaded,
                    Is.False,
                    "Validator must unload scenes that it opens for inspection.");
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                EditorSceneManager.NewScene(
                    NewSceneSetup.EmptyScene,
                    NewSceneMode.Single);
            }
        }

        [Test]
        public void CatalogValidator_RejectsDuplicateStableIds()
        {
            var catalog = ScriptableObject.CreateInstance<ArenaCatalogAsset>();
            try
            {
                ArenaCatalogEntryAuthoring entry =
                    ArenaCatalogEntryAuthoring.ForScene(
                        new ArenaContentId("legacy_arena"),
                        ArenaContentMode.Arena,
                        "Assets/MissingArena.unity",
                        true);
                ArenaContentAuthoring.ReplaceCatalogEntries(catalog, entry, entry);

                ArenaContentValidationReport report =
                    ArenaContentValidator.ValidateCatalog(catalog);

                Assert.That(
                    report.Contains(ArenaContentValidationCode.DuplicateCatalogId),
                    Is.True,
                    report.ToString());
            }
            finally
            {
                Object.DestroyImmediate(catalog);
            }
        }

        [Test]
        public void OriginalFortressSourceModels_MeetStaticMobileImportContract()
        {
            Assert.DoesNotThrow(FortressSourceArtValidator.ValidateOrThrow);
            Assert.That(FortressSourceArtValidator.ContractCount, Is.EqualTo(6));

            for (int index = 0;
                 index < FortressSourceArtValidator.ContractCount;
                 index++)
            {
                FortressSourceModelContract contract =
                    FortressSourceArtValidator.GetContract(index);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                    contract.AssetPath);
                ModelImporter importer = AssetImporter.GetAtPath(contract.AssetPath)
                    as ModelImporter;

                Assert.That(model, Is.Not.Null, contract.AssetPath);
                Assert.That(importer, Is.Not.Null, contract.AssetPath);
                Assert.That(model.name, Is.EqualTo(contract.ModelName));
                Assert.That(
                    model.GetComponentsInChildren<MeshRenderer>(true),
                    Has.Length.EqualTo(1));
                Assert.That(
                    model.GetComponentsInChildren<MeshFilter>(true),
                    Has.Length.EqualTo(1));
                Assert.That(
                    model.GetComponentsInChildren<Collider>(true),
                    Is.Empty);
                Assert.That(
                    model.GetComponentsInChildren<Animator>(true),
                    Is.Empty);
                Assert.That(
                    model.GetComponentsInChildren<Camera>(true),
                    Is.Empty);
                Assert.That(
                    model.GetComponentsInChildren<Light>(true),
                    Is.Empty);

                Mesh mesh = model.GetComponentInChildren<MeshFilter>(true)
                    .sharedMesh;
                Assert.That(mesh, Is.Not.Null);
                Assert.That(
                    mesh.subMeshCount,
                    Is.EqualTo(contract.ExpectedSubmeshCount),
                    contract.ModelName);
                Assert.That(
                    model.GetComponentInChildren<MeshRenderer>(true)
                        .sharedMaterials,
                    Has.Length.EqualTo(contract.ExpectedSubmeshCount),
                    contract.ModelName);
                Assert.That(
                    CountTriangles(mesh),
                    Is.InRange(1, contract.MaximumTriangles),
                    contract.ModelName);
                Assert.That(importer.isReadable, Is.False, contract.ModelName);
                Assert.That(importer.addCollider, Is.False, contract.ModelName);
                Assert.That(
                    importer.importTangents,
                    Is.EqualTo(ModelImporterTangents.CalculateMikk),
                    contract.ModelName);
                Assert.That(
                    mesh.HasVertexAttribute(
                        UnityEngine.Rendering.VertexAttribute.Tangent),
                    Is.True,
                    contract.ModelName);
            }

            FortressSourceModelContract foundation =
                FortressSourceArtValidator.GetContract(5);
            Assert.That(
                foundation.Id,
                Is.EqualTo(FortressSourceModelId.FoundationRing));
            Assert.That(
                foundation.AssetPath,
                Is.EqualTo(
                    "Assets/SessionRpg/SourceArt/Environment/" +
                    "FortressFoundationRing.fbx"));
            Assert.That(foundation.ExpectedSubmeshCount, Is.EqualTo(1));
            Assert.That(foundation.MaximumTriangles, Is.EqualTo(8000));
            Assert.That(foundation.RequiresGroundPivot, Is.False);
            Assert.That(
                foundation.ExpectedMinimumY,
                Is.EqualTo(FortressSourceArtValidator.FoundationMinimumY));
            Assert.That(
                foundation.ExpectedMaximumY,
                Is.EqualTo(FortressSourceArtValidator.FoundationMaximumY));
            Assert.That(
                FortressSourceArtValidator.FoundationMinimumPlanarRadius,
                Is.EqualTo(21.25f));
            Assert.That(
                FortressSourceArtValidator.FoundationMaximumPlanarRadius,
                Is.EqualTo(25.5f));
        }

        [Test]
        public void OriginalArenaFloorSourceModel_MeetsStaticMobileImportContract()
        {
            Assert.DoesNotThrow(ArenaFloorSourceArtValidator.ValidateOrThrow);
            Assert.That(
                ArenaFloorSourceArtValidator.SourceAssetPath,
                Is.EqualTo(
                    "Assets/SessionRpg/SourceArt/Environment/ArenaFloorVisual.fbx"));
            Assert.That(
                ArenaFloorSourceArtValidator.GetExpectedMaterialSlotName(0),
                Is.EqualTo("ArenaFloor_Stone"));
            Assert.That(
                ArenaFloorSourceArtValidator.GetExpectedMaterialSlotName(1),
                Is.EqualTo("ArenaFloor_Trim"));
            Assert.That(
                ArenaFloorSourceArtValidator.GetExpectedMaterialSlotName(2),
                Is.EqualTo("ArenaFloor_Grout"));

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                ArenaFloorSourceArtValidator.SourceAssetPath);
            ModelImporter importer = AssetImporter.GetAtPath(
                ArenaFloorSourceArtValidator.SourceAssetPath) as ModelImporter;
            Assert.That(model, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(
                model.name,
                Is.EqualTo(ArenaFloorSourceArtValidator.SourceModelName));
            Assert.That(
                model.GetComponentsInChildren<MeshRenderer>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                model.GetComponentsInChildren<MeshFilter>(true),
                Has.Length.EqualTo(1));
            Assert.That(
                model.GetComponentsInChildren<Collider>(true),
                Is.Empty);
            Assert.That(
                model.GetComponentsInChildren<Animator>(true),
                Is.Empty);
            Assert.That(
                model.GetComponentsInChildren<Camera>(true),
                Is.Empty);
            Assert.That(
                model.GetComponentsInChildren<Light>(true),
                Is.Empty);

            MeshRenderer renderer =
                model.GetComponentInChildren<MeshRenderer>(true);
            Mesh mesh = model.GetComponentInChildren<MeshFilter>(true).sharedMesh;
            Assert.That(mesh, Is.Not.Null);
            Assert.That(
                mesh.subMeshCount,
                Is.EqualTo(ArenaFloorSourceArtValidator.ExpectedSubmeshCount));
            Assert.That(
                renderer.sharedMaterials,
                Has.Length.EqualTo(
                    ArenaFloorSourceArtValidator.ExpectedSubmeshCount));
            Assert.That(
                CountTriangles(mesh),
                Is.InRange(1, ArenaFloorSourceArtValidator.MaximumTriangles));
            Assert.That(
                mesh.vertexCount,
                Is.LessThanOrEqualTo(
                    ArenaFloorSourceArtValidator.MaximumVertices));
            Assert.That(
                importer.materialImportMode,
                Is.EqualTo(ModelImporterMaterialImportMode.None));
            Assert.That(importer.addCollider, Is.False);
            Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.importCameras, Is.False);
            Assert.That(importer.importLights, Is.False);
            Assert.That(
                importer.importTangents,
                Is.EqualTo(ModelImporterTangents.CalculateMikk));
            Assert.That(
                mesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.Tangent),
                Is.True);
        }

        [Test]
        public void ArenaFloorSurfaceMaps_MeetUrpMobileImportContract()
        {
            string macroAlbedoPath =
                ArenaFloorTextureFolder + "ArenaFloorMacroAlbedo.png";
            string macroNormalPath =
                ArenaFloorTextureFolder + "ArenaFloorMacroNormal.png";
            string macroMaskPath =
                ArenaFloorTextureFolder + "ArenaFloorMacroMask.png";
            AssertSurfaceTextureImport(
                macroAlbedoPath,
                TextureImporterType.Default,
                true,
                TextureImporterAlphaSource.None);
            AssertSurfaceTextureImport(
                macroNormalPath,
                TextureImporterType.NormalMap,
                false,
                TextureImporterAlphaSource.None);
            AssertSurfaceTextureImport(
                macroMaskPath,
                TextureImporterType.Default,
                false,
                TextureImporterAlphaSource.FromInput);
            Assert.That(
                File.ReadAllBytes(macroAlbedoPath).SequenceEqual(
                    File.ReadAllBytes(macroNormalPath)),
                Is.False,
                "The macro normal must be an explicit tangent normal, not an " +
                "albedo copy imported through Unity's height conversion.");
            Assert.That(
                File.ReadAllBytes(macroMaskPath).SequenceEqual(
                    File.ReadAllBytes(
                        ArenaFloorTextureFolder + "ArenaFloorSurfaceMask.png")),
                Is.False,
                "The macro mask must contain the full-floor phase bake, not the " +
                "single-repeat micro source mask.");
            AssertSurfaceTextureImport(
                ArenaFloorTextureFolder + "ArenaFloorSurfaceAlbedo.png",
                TextureImporterType.Default,
                false,
                TextureImporterAlphaSource.None);
            AssertSurfaceTextureImport(
                ArenaFloorTextureFolder + "ArenaFloorSurfaceNormal.png",
                TextureImporterType.NormalMap,
                false,
                TextureImporterAlphaSource.None);
            AssertSurfaceTextureImport(
                ArenaFloorTextureFolder + "ArenaFloorSurfaceMask.png",
                TextureImporterType.Default,
                false,
                TextureImporterAlphaSource.FromInput);
        }

        [Test]
        public void ArenaFloorMacroMask_RecordsPhaseAlignedMacroCavityBake()
        {
            const string ManifestPath =
                "Assets/SessionRpg/SourceArt/Environment/" +
                "ArenaFloorVisual.manifest.json";
            string manifest = File.ReadAllText(ManifestPath);

            Assert.That(
                manifest,
                Does.Contain(
                    "\"phase_baked_uv_formula\": \"fract(uv * 4.6875 + " +
                    "vec2(0.47125, 0.64125))\""));
            Assert.That(
                manifest,
                Does.Contain(
                    "\"ao_and_smoothness_are_direct_phase_baked_source_channels\": " +
                    "false"));
            Assert.That(
                manifest,
                Does.Contain(
                    "\"macro_cavity_ao\": \"phase_micro_ao_times_dark_joint_" +
                    "signal_clamped_0_48_to_0_985\""));
            Assert.That(
                manifest,
                Does.Contain(
                    "\"macro_smoothness_remap\": " +
                    "\"phase_micro_roughness_to_0_14_0_32\""));
            Assert.That(manifest, Does.Contain("\"macro_normal\""));
            Assert.That(manifest, Does.Contain("\"macro_packed_mask\""));
        }

        [Test]
        public void FortressMasonryMaps_MeetSharedLitSurfaceContract()
        {
            string albedoPath =
                FortressMasonrySourceFolder +
                "stone_brick_wall_001_diff_2k.png";
            string normalPath =
                FortressMasonrySourceFolder +
                "stone_brick_wall_001_nor_gl_2k.png";
            string maskPath =
                ArenaFloorTextureFolder + "FortressMasonryMask.png";

            AssertSurfaceTextureImport(
                albedoPath,
                TextureImporterType.Default,
                true,
                TextureImporterAlphaSource.None,
                expectedMaxTextureSize: 2048);
            AssertSurfaceTextureImport(
                normalPath,
                TextureImporterType.NormalMap,
                false,
                TextureImporterAlphaSource.None,
                expectedMaxTextureSize: 2048);
            AssertSurfaceTextureImport(
                maskPath,
                TextureImporterType.Default,
                false,
                TextureImporterAlphaSource.FromInput);

            Assert.That(
                File.ReadAllBytes(albedoPath).SequenceEqual(
                    File.ReadAllBytes(normalPath)),
                Is.False,
                "The audited tangent-space normal must remain distinct from the " +
                "Poly Haven diffuse source.");

            string sourceManifest = File.ReadAllText(
                FortressMasonrySourceFolder + "manifest.json");
            Assert.That(sourceManifest, Does.Contain("\"intakeId\": \"POLYHAVEN-2026-001\""));
            Assert.That(sourceManifest, Does.Contain("\"license\": \"CC0-1.0\""));
            Assert.That(
                sourceManifest,
                Does.Contain("\"normalConvention\": \"OpenGL +Y\""));

            const string DerivedManifestPath =
                "SourceArt/Blender/Environment/Textures/" +
                "FortressMasonryPbr.manifest.json";
            string derivedManifest = File.ReadAllText(DerivedManifestPath);
            Assert.That(
                derivedManifest,
                Does.Contain("\"sourceIntakeId\": \"POLYHAVEN-2026-001\""));
            Assert.That(
                derivedManifest,
                Does.Contain("\"path\": \"Assets/SessionRpg/Art/Environment/" +
                    "Textures/FortressMasonryMask.png\""));
            Assert.That(
                derivedManifest,
                Does.Contain("\"R\": \"metallic=0\""));
            Assert.That(
                derivedManifest,
                Does.Contain("\"G\": \"ambient_occlusion\""));
            Assert.That(
                derivedManifest,
                Does.Contain("\"A\": \"smoothness=clamp(1-roughness,0.02,0.42)\""));
        }

        [TestCase("Stone", 2.25f, 0.11f, 0.27f, 0.55f)]
        [TestCase("StoneVariation", 2.35f, 0.47f, 0.08f, 0.48f)]
        public void SharedStoneMaterial_UsesAuthoredNormalMap(
            string materialName,
            float tiling,
            float offsetX,
            float offsetY,
            float bumpScale)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialFolder + materialName + ".mat");
            Assert.That(material, Is.Not.Null, materialName);
            Assert.That(
                material.shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"));
            Texture baseMap = material.GetTexture("_BaseMap");
            Assert.That(baseMap, Is.Not.Null);
            Assert.That(baseMap.name, Does.Contain("ArenaFlagstoneAlbedo"));
            Assert.That(
                material.GetTextureScale("_BaseMap"),
                Is.EqualTo(Vector2.one * tiling));
            Assert.That(
                material.GetTextureOffset("_BaseMap"),
                Is.EqualTo(new Vector2(offsetX, offsetY)));
            Texture normal = material.GetTexture("_BumpMap");
            Assert.That(normal, Is.Not.Null);
            Assert.That(normal.name, Does.Contain("ArenaFloorSurfaceNormal"));
            Assert.That(
                material.GetTextureScale("_BumpMap"),
                Is.EqualTo(material.GetTextureScale("_BaseMap")));
            Assert.That(
                material.GetTextureOffset("_BumpMap"),
                Is.EqualTo(material.GetTextureOffset("_BaseMap")));
            Assert.That(
                material.GetFloat("_BumpScale"),
                Is.EqualTo(bumpScale).Within(0.0001f));
            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
        }

        [Test]
        public void LavaMaterials_UseLitEmissionContract()
        {
            Material lava = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialFolder + "Lava.mat");
            Material seam = AssetDatabase.LoadAssetAtPath<Material>(
                MaterialFolder + "LavaSeam.mat");
            Assert.That(lava, Is.Not.Null);
            Assert.That(seam, Is.Not.Null);
            Assert.That(
                lava.shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(lava.IsKeywordEnabled("_EMISSION"), Is.True);
            Assert.That(
                lava.globalIlluminationFlags &
                    MaterialGlobalIlluminationFlags.EmissiveIsBlack,
                Is.EqualTo(MaterialGlobalIlluminationFlags.None));
            Assert.That(
                lava.globalIlluminationFlags &
                    MaterialGlobalIlluminationFlags.AnyEmissive,
                Is.Not.EqualTo(MaterialGlobalIlluminationFlags.None));
            Texture lavaBaseMap = lava.GetTexture("_BaseMap");
            Assert.That(lavaBaseMap, Is.Not.Null);
            Assert.That(lavaBaseMap.name, Does.Contain("ArenaLavaAlbedo"));
            Assert.That(
                lava.GetTexture("_EmissionMap"),
                Is.SameAs(lavaBaseMap));
            Assert.That(
                lava.GetTextureScale("_EmissionMap"),
                Is.EqualTo(lava.GetTextureScale("_BaseMap")));
            Assert.That(
                lava.GetTextureOffset("_EmissionMap"),
                Is.EqualTo(lava.GetTextureOffset("_BaseMap")));
            Assert.That(
                seam.shader.name,
                Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(seam.IsKeywordEnabled("_EMISSION"), Is.True);
            Assert.That(
                seam.globalIlluminationFlags &
                    MaterialGlobalIlluminationFlags.EmissiveIsBlack,
                Is.EqualTo(MaterialGlobalIlluminationFlags.None));
            Assert.That(
                seam.globalIlluminationFlags &
                    MaterialGlobalIlluminationFlags.AnyEmissive,
                Is.Not.EqualTo(MaterialGlobalIlluminationFlags.None));
            Color seamColor = seam.color;
            Assert.That(seamColor.r, Is.EqualTo(0.92f).Within(0.0001f));
            Assert.That(seamColor.g, Is.EqualTo(0.16f).Within(0.0001f));
            Assert.That(seamColor.b, Is.EqualTo(0.018f).Within(0.0001f));
            Assert.That(seamColor.a, Is.EqualTo(1f).Within(0.0001f));
            Color seamEmission = seam.GetColor("_EmissionColor");
            Assert.That(
                seamEmission.r,
                Is.EqualTo(0.92f * 1.45f).Within(0.0001f));
            Assert.That(
                seamEmission.g,
                Is.EqualTo(0.16f * 1.45f).Within(0.0001f));
            Assert.That(
                seamEmission.b,
                Is.EqualTo(0.018f * 1.45f).Within(0.0001f));
        }

        [TestCase(
            "Assets/SessionRpg/Content/Arenas/RiftCrucible/Prefabs/RiftCrucible.prefab",
            "PerimeterEmberDrift",
            "ArenaCinderVeil")]
        [TestCase(
            "Assets/SessionRpg/Content/Arenas/EmberVault/Prefabs/EmberVault.prefab",
            "RiftCinderDrift",
            "RiftCinderVeil")]
        public void GeneratedBattlegroundAtmosphere_IsBoundedAndPresentationOnly(
            string prefabPath,
            string driftName,
            string veilName)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            Assert.That(prefab, Is.Not.Null, prefabPath);
            ParticleSystem drift = prefab
                .GetComponentsInChildren<ParticleSystem>(true)
                .SingleOrDefault(value => value.name == driftName);
            ParticleSystem veil = prefab
                .GetComponentsInChildren<ParticleSystem>(true)
                .SingleOrDefault(value => value.name == veilName);

            Assert.That(drift, Is.Not.Null, driftName);
            Assert.That(veil, Is.Not.Null, veilName);
            Assert.That(veil.transform.parent, Is.SameAs(drift.transform.parent));
            ParticleSystem[] atmosphere = drift.transform.parent
                .GetComponentsInChildren<ParticleSystem>(true);
            Assert.That(atmosphere, Has.Length.EqualTo(2));
            Assert.That(drift.main.loop, Is.True);
            Assert.That(drift.main.playOnAwake, Is.True);
            Assert.That(drift.main.maxParticles, Is.EqualTo(32));
            Assert.That(
                drift.emission.rateOverTime.constant,
                Is.InRange(6f, 8f));
            Assert.That(
                drift.shape.shapeType,
                Is.EqualTo(ParticleSystemShapeType.Circle));
            Assert.That(drift.shape.radius, Is.GreaterThanOrEqualTo(18.5f));
            Assert.That(drift.collision.enabled, Is.False);
            Assert.That(
                drift.GetComponent<ParticleSystemRenderer>().renderMode,
                Is.EqualTo(ParticleSystemRenderMode.Billboard));
            Assert.That(veil.main.loop, Is.True);
            Assert.That(veil.main.playOnAwake, Is.True);
            Assert.That(veil.main.maxParticles, Is.EqualTo(24));
            Assert.That(veil.shape.shapeType, Is.EqualTo(ParticleSystemShapeType.Circle));
            Assert.That(veil.shape.radius, Is.GreaterThanOrEqualTo(18.2f));
            Assert.That(veil.collision.enabled, Is.False);
            Assert.That(
                veil.GetComponent<ParticleSystemRenderer>().renderMode,
                Is.EqualTo(ParticleSystemRenderMode.Billboard));
            Assert.That(
                drift.GetComponentsInParent<Collider>(true),
                Is.Empty,
                "Atmosphere may not add presentation-owned gameplay collision.");
        }

        private ArenaPresentation Configure(ArenaCaptureAnchor[] anchors)
        {
            return ArenaContentAuthoring.ConfigurePresentation(
                _presentationRoot,
                new ArenaContentId("legacy_arena"),
                ArenaContentMode.Arena,
                ArenaPresentationCapabilities.FlatCombatGround,
                _visualRoot,
                _proxyRoot,
                _groundRoot,
                0f,
                1,
                anchors);
        }

        private static void AssertSurfaceTextureImport(
            string assetPath,
            TextureImporterType expectedType,
            bool expectedSrgb,
            TextureImporterAlphaSource expectedAlphaSource,
            bool expectedNormalConversion = false,
            float expectedHeightMapScale = 0.25f,
            int expectedMaxTextureSize = 1024)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                assetPath);
            TextureImporter importer = AssetImporter.GetAtPath(assetPath)
                as TextureImporter;

            Assert.That(texture, Is.Not.Null, assetPath);
            Assert.That(importer, Is.Not.Null, assetPath);
            Assert.That(importer.textureType, Is.EqualTo(expectedType), assetPath);
            Assert.That(importer.sRGBTexture, Is.EqualTo(expectedSrgb), assetPath);
            Assert.That(
                importer.alphaSource,
                Is.EqualTo(expectedAlphaSource),
                assetPath);
            Assert.That(importer.alphaIsTransparency, Is.False, assetPath);
            Assert.That(
                importer.convertToNormalmap,
                Is.EqualTo(expectedNormalConversion),
                assetPath);
            Assert.That(
                importer.heightmapScale,
                Is.EqualTo(expectedHeightMapScale).Within(0.0001f),
                assetPath);
            Assert.That(
                importer.flipGreenChannel,
                Is.False,
                assetPath + " uses the authored OpenGL/+Y convention.");
            Assert.That(importer.mipmapEnabled, Is.True, assetPath);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat), assetPath);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear), assetPath);
            Assert.That(importer.anisoLevel, Is.EqualTo(8), assetPath);
            Assert.That(
                importer.maxTextureSize,
                Is.EqualTo(expectedMaxTextureSize),
                assetPath);
            Assert.That(
                importer.npotScale,
                Is.EqualTo(TextureImporterNPOTScale.ToNearest),
                assetPath);
            Assert.That(importer.isReadable, Is.False, assetPath);

            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            Assert.That(android.overridden, Is.True, assetPath);
            Assert.That(
                android.maxTextureSize,
                Is.EqualTo(expectedMaxTextureSize),
                assetPath);
            Assert.That(
                android.format,
                Is.EqualTo(TextureImporterFormat.ASTC_4x4),
                assetPath);
            Assert.That(
                android.textureCompression,
                Is.EqualTo(TextureImporterCompression.Compressed),
                assetPath);
            Assert.That(android.compressionQuality, Is.EqualTo(100), assetPath);
            Assert.That(android.crunchedCompression, Is.False, assetPath);
        }

        private static Transform CreateChild(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static ArenaCaptureAnchor CreateAnchor(
            Transform parent,
            ArenaCaptureAnchorKind kind)
        {
            var anchorObject = new GameObject(kind + " Capture Anchor");
            anchorObject.transform.SetParent(parent, false);
            ArenaCaptureAnchor anchor =
                anchorObject.AddComponent<ArenaCaptureAnchor>();
            ArenaContentAuthoring.ConfigureCaptureAnchor(anchor, kind);
            return anchor;
        }

        private static void EnsureTemporaryFolder()
        {
            if (AssetDatabase.IsValidFolder(TemporaryFolder))
            {
                return;
            }

            AssetDatabase.CreateFolder(
                "Assets/SessionRpg/Tests/EditMode/Editor",
                "GeneratedArenaContentTests");
        }

        private static int CountTriangles(Mesh mesh)
        {
            int triangles = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                Assert.That(
                    mesh.GetTopology(submesh),
                    Is.EqualTo(MeshTopology.Triangles));
                triangles += (int)(mesh.GetIndexCount(submesh) / 3);
            }

            return triangles;
        }
    }
}
