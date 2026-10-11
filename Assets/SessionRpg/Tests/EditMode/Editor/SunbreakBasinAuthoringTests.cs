using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Editor.Authoring;
using SessionRpg.Unity.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakBasinAuthoringTests
    {
        private const string ScanFolder = "Assets/ThirdParty/PolyHaven/SunbreakSurfaces/";
        private const string TerrainShader = "SessionRpg/Sunbreak Terrain";

        [TestCase(-1, -1)]
        [TestCase(-1, 0)]
        [TestCase(-1, 1)]
        [TestCase(0, -1)]
        [TestCase(0, 0)]
        [TestCase(0, 1)]
        [TestCase(1, -1)]
        [TestCase(1, 0)]
        [TestCase(1, 1)]
        public void GeneratedBasinChunk_KeepsContinuousGroundAndCoreOwnedSolids(int x, int z)
        {
            string path = "Assets/SessionRpg/Content/Worlds/SunbreakBasin/Chunks/Chunk_" + x + "_" + z + ".unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
                GameObject[] roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(1));
                WorldChunkPresentation presentation = roots[0].GetComponent<WorldChunkPresentation>();
                Assert.That(presentation, Is.Not.Null);
                Transform visual = presentation.VisualRoot;
                Assert.That(visual, Is.Not.Null);
                Assert.That(visual.name, Is.EqualTo("SunbreakBasin"));
                Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
                Assert.That(visual.position, Is.EqualTo(new Vector3(x * 64f, 0f, z * 64f)));
                Assert.That(visual.childCount, Is.EqualTo(5));
                string[] groups = { "Ground", "MeadowDetail", "Encampments", "BlockerIslands", "OuterRidges" };
                foreach (string group in groups) { Assert.That(visual.Find(group), Is.Not.Null, group); }
                Assert.That(visual.Find("AshenCausewayLandmarks"), Is.Null,
                    "The basin must be a separate authored location, not a retained causeway segment.");

                MeshFilter floor = visual.Find("Ground/ContinuousMeadow").GetComponent<MeshFilter>();
                Assert.That(floor.sharedMesh.bounds.min, Is.EqualTo(Vector3.zero));
                Assert.That(floor.sharedMesh.bounds.max, Is.EqualTo(new Vector3(64f, 0f, 64f)));
                Vector3[] floorVertices = floor.sharedMesh.vertices;
                Vector2[] floorUvs = floor.sharedMesh.uv;
                for (int index = 0; index < floorVertices.Length; index++)
                {
                    Vector3 world = floor.transform.TransformPoint(floorVertices[index]);
                    Assert.That(world.y, Is.Zero);
                    Assert.That(floorUvs[index].x, Is.EqualTo((world.x + 64f) / 192f).Within(0.00001f));
                    Assert.That(floorUvs[index].y, Is.EqualTo((world.z + 64f) / 192f).Within(0.00001f),
                        "World UVs must retain the exact same macro phase across every seam.");
                }

                MeshRenderer[] renderers = visual.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(renderers.Length, Is.InRange(3, 80));
                long triangles = 0;
                foreach (MeshFilter filter in visual.GetComponentsInChildren<MeshFilter>(true))
                {
                    Assert.That(filter.sharedMesh, Is.Not.Null, filter.name);
                    AssertSurfaceFrameData(filter);
                    Mesh mesh = filter.sharedMesh;
                    for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    {
                        triangles += mesh.GetIndexCount(submesh) / 3;
                    }
                    foreach (Vector3 local in mesh.vertices)
                    {
                        Vector3 point = filter.transform.TransformPoint(local);
                        if (point.y <= 0.181f || OutsideBounds(point)) { continue; }
                        Assert.That(IsCoreBlocked(point), Is.True,
                            filter.name + " must not suggest false cover on traversable ground: " + point);
                    }
                }
                Assert.That(triangles, Is.InRange(1L, 65000L));
                TestContext.WriteLine("Sunbreak chunk ({0},{1}): {2} triangles, {3} renderers (caps 65000/80).",
                    x, z, triangles, renderers.Length);
                foreach (MeshRenderer renderer in renderers)
                {
                    Assert.That(renderer.gameObject.layer, Is.EqualTo(2));
                    Assert.That(renderer.sharedMaterial, Is.Not.Null, renderer.name);
                    Assert.That(renderer.sharedMaterial.shader, Is.Not.Null, renderer.name);
                    Assert.That(renderer.sharedMaterial.GetFloat("_Surface"), Is.Zero,
                        "The region uses bounded opaque geometry rather than transparent foliage layers.");
                    Assert.That(renderer.sharedMaterial.GetFloat("_AlphaClip"),
                        Is.EqualTo(renderer.sharedMaterial.shader.name == SunbreakVegetationAssets.VegetationShader ? 1f : 0f),
                        "Only the admitted source foliage uses coverage cutout; other basin materials remain opaque.");
                    Assert.That(renderer.sharedMaterial.IsKeywordEnabled("_ALPHATEST_ON"), Is.False);
                    Assert.That(renderer.sharedMaterial.renderQueue, Is.LessThanOrEqualTo(2500));
                    Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial),
                        Does.StartWith("Assets/SessionRpg/Art/Materials/Sunbreak_"),
                        "Basin surface changes must not retint a shared Arena or Training material.");
                }

                int expectedOwners = 0;
                for (int landmarkIndex = 0; landmarkIndex < PveRegionLayout.LandmarkCount; landmarkIndex++)
                {
                    ArenaCircleObstacle landmark = PveRegionLayout.GetLandmark(landmarkIndex);
                    bool owns = Mathf.FloorToInt(landmark.Center.X / 64f) == x &&
                        Mathf.FloorToInt(landmark.Center.Z / 64f) == z;
                    Transform island = visual.Find("BlockerIslands/Blocker_" + landmarkIndex);
                    Assert.That(island != null, Is.EqualTo(owns),
                        "A landmark has one owner; its overlapping Core components are not separate islands: " + landmarkIndex);
                    if (owns)
                    {
                        expectedOwners++;
                        Transform shelf = island.Find("RockShelf");
                        Assert.That(shelf, Is.Not.Null);
                        AssertUnionShelf(shelf.GetComponent<MeshFilter>(), landmarkIndex);
                        bool measuredIsland = landmarkIndex == 0 || landmarkIndex == 7 || landmarkIndex == 2 || landmarkIndex == 9;
                        Assert.That(island.Find("EmbeddedBoulders") != null, Is.EqualTo(!measuredIsland));
                        if (landmarkIndex >= PveRegionLayout.MajorObstacleCount)
                        {
                            Assert.That(AssetDatabase.GetAssetPath(shelf.GetComponent<MeshRenderer>().sharedMaterial),
                                Is.EqualTo("Assets/SessionRpg/Art/Materials/Sunbreak_Meadow.mat"),
                                "Camp groves grow out of low earth banks, not bright stone model bases.");
                            Vector3[] shelfVertices = shelf.GetComponent<MeshFilter>().sharedMesh.vertices;
                            Vector2[] shelfUvs = shelf.GetComponent<MeshFilter>().sharedMesh.uv;
                            for (int vertex = 0; vertex < shelfVertices.Length; vertex++)
                            {
                                Vector3 world = shelf.TransformPoint(shelfVertices[vertex]);
                                Assert.That(shelfUvs[vertex].x, Is.EqualTo((world.x + 64f) / 192f).Within(0.00001f));
                                Assert.That(shelfUvs[vertex].y, Is.EqualTo((world.z + 64f) / 192f).Within(0.00001f));
                            }
                        }
                        if (measuredIsland)
                        {
                            Assert.That(island.Find("LeafCanopy"), Is.Null);
                            Assert.That(island.Find("BranchingWood"), Is.Null);
                            for (int batch = 0; batch < 3; batch++)
                            {
                                Transform plant = island.Find("MeasuredVegetation_" + batch);
                                Assert.That(plant, Is.Not.Null);
                                AssertSurfaceFrameData(plant.GetComponent<MeshFilter>());
                            }
                        }
                        else
                        {
                            AssertIndividualLeafSurface(island.Find("LeafCanopy").GetComponent<MeshFilter>());
                            AssertIndividualLeafSurface(island.Find("SunlitLeafEdges").GetComponent<MeshFilter>());
                            AssertSurfaceFrameData(island.Find("BranchingWood").GetComponent<MeshFilter>());
                            AssertSurfaceFrameData(island.Find("RockFernUnderstory").GetComponent<MeshFilter>());
                        }
                        Assert.That(island.GetComponentsInChildren<MeshRenderer>(true).Length,
                            Is.GreaterThanOrEqualTo(3), "Islands need dimensional art, not a flat collision diagram.");
                    }
                }
                Assert.That(visual.Find("BlockerIslands").childCount, Is.EqualTo(expectedOwners));

                Assert.That(visual.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(visual.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                Assert.That(visual.GetComponentsInChildren<Camera>(true), Is.Empty);
                Assert.That(visual.GetComponentsInChildren<Light>(true), Is.Empty);
                Assert.That(visual.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
                foreach (MeshRenderer detail in visual.Find("MeadowDetail").GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.That(detail.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                    Assert.That(detail.bounds.max.y, Is.LessThanOrEqualTo(0.181f));
                }
                if (x != 0 || z != 0)
                {
                    Assert.That(visual.Find("OuterRidges/UnbrokenDistantMeadow"), Is.Not.Null,
                        "Boundary chunks need background ground beyond the playable rectangle.");
                    AssertBoundaryBank(visual, x, z);
                }
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) { EditorSceneManager.CloseScene(scene, true); }
            }
        }

        [Test]
        public void MeadowMaterial_BlendsMeasuredPbrLayersWithIndependentLinearWorldMask()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/Sunbreak_Meadow.mat");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo(TerrainShader));
            AssertBoundTexture(material, "_BaseMap", "leafy_grass", "diff");
            AssertBoundTexture(material, "_BumpMap", "leafy_grass", "nor_gl");
            AssertBoundTexture(material, "_RoughnessMap", "leafy_grass", "rough");
            AssertBoundTexture(material, "_LayerMap", "dirt_floor", "diff");
            AssertBoundTexture(material, "_LayerNormal", "dirt_floor", "nor_gl");
            AssertBoundTexture(material, "_LayerRoughness", "dirt_floor", "rough");
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(Vector2.one * 96f),
                "The measured two-meter scan repeats 96 times across the 192-meter world.");
            Assert.That(material.GetTextureScale("_LayerMap"), Is.EqualTo(Vector2.one * (192f / 2.07f)));
            Texture2D macro = material.GetTexture("_BlendMap") as Texture2D;
            Assert.That(macro, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(macro), Is.EqualTo(
                "Assets/SessionRpg/Art/Environment/SunbreakBasin/SurfaceBlend.asset"));
            Assert.That(macro.width, Is.EqualTo(512));
            Assert.That(macro.height, Is.EqualTo(512));
            Assert.That(macro.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(macro.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(macro.mipmapCount, Is.GreaterThan(1));
            Assert.That(macro.isReadable, Is.False);
            Assert.That(UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(macro.graphicsFormat),
                Is.False, "Soil weight and vegetation variation are linear RG data, not sRGB color.");
            Assert.That(macro.format, Is.EqualTo(TextureFormat.RGBA32));
            Assert.That(material.GetTextureOffset("_BaseMap"), Is.EqualTo(Vector2.zero));
            Assert.That(material.GetTextureOffset("_LayerMap"), Is.EqualTo(Vector2.zero));
            Assert.That(material.GetFloat("_BumpScale"), Is.InRange(0.1f, 1f));
            Assert.That(material.GetFloat("_SecondLayer"), Is.EqualTo(1f));
            Assert.That(material.IsKeywordEnabled("_SECOND_LAYER"), Is.True);
            Assert.That(material.IsKeywordEnabled("_DETAIL_MULX2"), Is.False);
            Assert.That(material.IsKeywordEnabled("_DETAIL_SCALED"), Is.False);
            Assert.That(material.GetFloat("_Smoothness"), Is.InRange(0.1f, 1f),
                "The shader multiplies inverse scanned roughness; this value is not a flat material gloss.");
            Assert.That(material.GetFloat("_Surface"), Is.Zero);
            Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.False);
        }

        [TestCase("MeadowAlbedoV1", 1024)]
        [TestCase("LimestoneAlbedoV1", 1024)]
        [TestCase("BarkAlbedoV1", 1024)]
        [TestCase("LeafAlbedoV1", 512)]
        public void NaturalSurfaceSources_UseBoundedMippedNonReadableColorImports(string name, int maximumSize)
        {
            string path = "Assets/SessionRpg/SourceArt/Environment/SunbreakNaturalV1/" + name + ".png";
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.convertToNormalmap, Is.False,
                "A generated color image must not be relabeled as a physically meaningful normal map.");
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(importer.anisoLevel, Is.EqualTo(8));
            Assert.That(importer.maxTextureSize, Is.EqualTo(maximumSize));
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.InRange(1, maximumSize));
            Assert.That(texture.height, Is.InRange(1, maximumSize));
            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(texture.isReadable, Is.False);
        }

        [TestCase("Foliage", "LeafAlbedoV1")]
        [TestCase("FoliageLight", "LeafAlbedoV1")]
        public void OrganicMaterials_UseDedicatedTexturedOpaqueSurfaces(string materialName, string textureName)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/Sunbreak_" + materialName + ".mat");
            Assert.That(material, Is.Not.Null);
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")), Is.EqualTo(
                "Assets/SessionRpg/SourceArt/Environment/SunbreakNaturalV1/" + textureName + ".png"));
            Assert.That(material.GetFloat("_Surface"), Is.Zero);
            Assert.That(material.GetFloat("_AlphaClip"), Is.Zero);
            Assert.That(material.IsKeywordEnabled("_ALPHATEST_ON"), Is.False);
            Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.False);
            Assert.That(material.GetFloat("_Smoothness"), Is.LessThanOrEqualTo(0.2f));
        }

        [TestCase("Rock", "rock_08", 1.6666667f)]
        [TestCase("Bark", "bark_brown_02", 1.25f)]
        public void ScannedSolidMaterials_UseMeasuredChannelsWithoutGroundLayerCost(
            string materialName, string sourceName, float tiling)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/Sunbreak_" + materialName + ".mat");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo(TerrainShader));
            AssertBoundTexture(material, "_BaseMap", sourceName, "diff");
            AssertBoundTexture(material, "_BumpMap", sourceName, "nor_gl");
            AssertBoundTexture(material, "_RoughnessMap", sourceName, "rough");
            Assert.That(material.GetTextureScale("_BaseMap").x, Is.EqualTo(tiling).Within(0.00001f));
            Assert.That(material.GetTextureScale("_BaseMap").y, Is.EqualTo(tiling).Within(0.00001f));
            Assert.That(material.GetFloat("_BumpScale"), Is.InRange(0.1f, 1f));
            Assert.That(material.GetFloat("_SecondLayer"), Is.Zero);
            Assert.That(material.IsKeywordEnabled("_SECOND_LAYER"), Is.False,
                "Only the meadow needs the additional three scanned layer samples and world mask.");
            Assert.That(material.GetFloat("_Surface"), Is.Zero);
            Assert.That(material.GetFloat("_AlphaClip"), Is.Zero);
            Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));
            Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.False);
        }

        [TestCase("sparse_grass", "diff", "3b655de96161164bf2324e136b3409ad8417c9f94750bc008e9c93ee507c2fac")]
        [TestCase("sparse_grass", "nor_gl", "5539a5913653a106a4e8a699a1d288f0b41907775b5fb36156ef5be2e937718b")]
        [TestCase("sparse_grass", "rough", "b023f187999956449b5f25df19f4eaae1c891f474e0a599b23d986118fe706d7")]
        [TestCase("rocks_ground_02", "col", "a2a4fc4e10a307ca6e345e0df50d6115ba1058174de915bfcfdd5ff3f49dc2fa")]
        [TestCase("rocks_ground_02", "nor_gl", "bde35636a0c548e57006917287e067d11abf3bfac2d70373162deb171ca9a4ef")]
        [TestCase("rocks_ground_02", "rough", "c5b54993717852963382876bd0558c831af389b00a2dbd40317c51ff8b9c0817")]
        [TestCase("rock_08", "diff", "1d55cc7c6357ad30b9486c38f280c6da095a0a07246b2659403a3a36006db521")]
        [TestCase("rock_08", "nor_gl", "5577d9766de427110c43b4d50be021389599421fd3f0249b1ee07e40bf27738e")]
        [TestCase("rock_08", "rough", "7605d6f529bed165c52486d221f2778632e34012f9cb06645377e374cad0b647")]
        [TestCase("bark_brown_02", "diff", "285051e34826ace29f4ab438ad35cc1201b2aae0a6157a5f3c0bf6667bbc539e")]
        [TestCase("bark_brown_02", "nor_gl", "aaba4529c3167a7811b4dbf5b4327d054a166c2fc8ea08ebd249a2692adace41")]
        [TestCase("bark_brown_02", "rough", "adafcab150c1ca99150c77198329e251437dc00fa1f66019f75624366926996e")]
        [TestCase("leafy_grass", "diff", "cfa40bc9d9417d1852db8753a8d5917f110c40101179f63543c382e39bc05e4a")]
        [TestCase("leafy_grass", "nor_gl", "832328216adc0a7e1f70a31d5ee48c9ab7f2152d83816122736cf42ac4b2ebd6")]
        [TestCase("leafy_grass", "rough", "0c2a1d91aacb8e54a496051b59854e51e456fe858abeade778e308359ef16463")]
        [TestCase("dirt_floor", "diff", "6c2e7f1c5eab8239fec561f255c607d0e3a86046925ba3a8b9340805efc1ab62")]
        [TestCase("dirt_floor", "nor_gl", "9481dc2cee97b240ca10dc1cd60fec834a85e455aaa7d2a584396ea10210c2c0")]
        [TestCase("dirt_floor", "rough", "093166ffda063b8fcf04ec645e1e12c24811f614468ba378437a3535c3c53f57")]
        public void ScannedSources_PreserveDeliveredBytesAndCorrectBoundedDataImports(
            string sourceName, string channel, string expectedHash)
        {
            string path = ScanPath(sourceName, channel);
            Assert.That(File.Exists(path), Is.True, path);
            using (SHA256 hash = SHA256.Create())
            {
                Assert.That(BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(path)))
                    .Replace("-", string.Empty).ToLowerInvariant(), Is.EqualTo(expectedHash),
                    "Scanned masters must not be repainted, packed or resaved by material authoring.");
            }
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null);
            bool normal = channel == "nor_gl";
            bool color = channel == "diff" || channel == "col";
            Assert.That(importer.textureType, Is.EqualTo(normal ? TextureImporterType.NormalMap : TextureImporterType.Default));
            Assert.That(importer.convertToNormalmap, Is.False);
            Assert.That(importer.flipGreenChannel, Is.False,
                "These are already OpenGL/+Y normals; a DirectX green-channel flip would invert relief.");
            Assert.That(importer.sRGBTexture, Is.EqualTo(color),
                "Only color is sRGB. Normal and roughness are linear data.");
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.None));
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.streamingMipmaps, Is.True);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            Assert.That(importer.anisoLevel, Is.EqualTo(8));
            int sourceSize = IsOneKSource(sourceName) ? 1024 : 2048;
            Assert.That(importer.maxTextureSize, Is.EqualTo(sourceSize));
            Assert.That(importer.textureCompression, Is.EqualTo(TextureImporterCompression.CompressedHQ));
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            Assert.That(android.overridden, Is.True);
            Assert.That(android.maxTextureSize, Is.EqualTo(1024));
            Assert.That(android.format, Is.EqualTo(normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6));
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.InRange(1, sourceSize));
            Assert.That(texture.height, Is.InRange(1, sourceSize));
            Assert.That(texture.mipmapCount, Is.GreaterThan(1));
            Assert.That(texture.isReadable, Is.False);
            Assert.That(UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(texture.graphicsFormat),
                Is.EqualTo(color));
        }

        [Test]
        public void ScannedShader_SourceHasNoReportedErrorsAndDefinesRequiredPasses()
        {
            Shader shader = Shader.Find(TerrainShader);
            Assert.That(shader, Is.Not.Null);
            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False);
            // The Null renderer selects a fallback subshader, so FindPass cannot
            // prove GPU support in -nographics EditMode. Native support and pass
            // lookup are mandatory in SunbreakTerrainShaderPlayModeTests instead.
            string source = ShaderSourceWithoutComments();
            string[] names = { "ForwardLit", "DepthOnly", "DepthNormals", "ShadowCaster" };
            string[] modes = { "UniversalForwardOnly", "DepthOnly", "DepthNormalsOnly", "ShadowCaster" };
            for (int index = 0; index < names.Length; index++)
            {
                string declaration = @"\bName\s+""" + names[index] + @"""\s*Tags\s*\{\s*""LightMode""\s*=\s*""" +
                    modes[index] + @"""\s*\}";
                Assert.That(source, Does.Match(declaration), names[index]);
            }
            Assert.That(Regex.Matches(source, @"\bPass\s*\{").Count, Is.EqualTo(4));
        }

        [Test]
        public void ScannedShader_SelectsShadowCascadePerPixelForLargeGroundTriangles()
        {
            string fragment = ShaderFunctionBody("TerrainFragment");
            Assert.That(fragment, Does.Match(@"(?s)#if\s+defined\s*\(\s*REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR\s*\)\s*" +
                @"data\.shadowCoord\s*=\s*input\.shadowCoord\s*;\s*" +
                @"#elif\s+defined\s*\(\s*MAIN_LIGHT_CALCULATE_SHADOWS\s*\)\s*" +
                @"data\.shadowCoord\s*=\s*TransformWorldToShadowCoord\s*\(\s*input\.positionWS\s*\)\s*;"),
                "High/Balanced use cascades. Interpolating per-vertex atlas coordinates across a 64m ground quad is invalid.");
        }

        [Test]
        public void ScannedShader_UsesUrpDirectionalAwareHomogeneousShadowClamping()
        {
            string vertex = ShaderFunctionBody("ShadowVertex");
            Assert.That(vertex, Does.Contain("ApplyShadowBias(positionWS, normalWS, direction)"));
            Assert.That(vertex, Does.Match(@"return\s+ApplyShadowClamping\s*\(\s*positionCS\s*\)\s*;"),
                "URP owns clip.w-aware clamping and its directional/punctual-light distinction.");
            Assert.That(vertex, Does.Not.Contain("UNITY_NEAR_CLIP_VALUE"),
                "Do not restore the old clamp that omits clip.w and also clips punctual-light shadows.");
        }

        [Test]
        public void ScannedShader_PerformanceTierCalculatesAdditionalVertexLightOnlyInVertexStage()
        {
            string vertex = ShaderFunctionBody("TerrainVertex");
            string fragment = ShaderFunctionBody("TerrainFragment");
            Assert.That(vertex, Does.Match(@"(?s)#ifdef\s+_ADDITIONAL_LIGHTS_VERTEX\s*" +
                @"output\.vertexLighting\s*=\s*VertexLighting\s*\(.*?\)\s*;\s*#endif"));
            Assert.That(fragment, Does.Not.Match(@"\bVertexLighting\s*\("),
                "The lower-cost Performance tier must not execute the vertex-light loop for every terrain pixel.");
            Assert.That(fragment, Does.Match(@"(?s)#ifdef\s+_ADDITIONAL_LIGHTS_VERTEX\s*" +
                @"data\.vertexLighting\s*=\s*input\.vertexLighting\s*;\s*#endif"));
        }

        [Test]
        public void GroundBlend_IsDeterministicVariedTwoDimensionalDataWithContinuousChunkPhase()
        {
            Func<float, float, Vector2> sample = GroundBlendSampler();
            Vector2 minimum = Vector2.one;
            Vector2 maximum = Vector2.zero;
            float variationX = 0f;
            float variationZ = 0f;
            float channelDifference = 0f;
            int grassDominant = 0;
            int soilDominant = 0;
            for (int z = -56; z <= 120; z += 4)
            {
                for (int x = -56; x <= 120; x += 4)
                {
                    Vector2 value = sample(x, z);
                    Assert.That(value, Is.EqualTo(sample(x, z)), "Surface generation must be seed/order independent.");
                    Assert.That(value.x, Is.InRange(0f, 1f));
                    Assert.That(value.y, Is.InRange(0f, 1f));
                    minimum = Vector2.Min(minimum, value);
                    maximum = Vector2.Max(maximum, value);
                    variationX += Mathf.Abs(value.x - sample(x + 4f, z).x);
                    variationZ += Mathf.Abs(value.x - sample(x, z + 4f).x);
                    channelDifference += Mathf.Abs(value.x - value.y);
                    if (value.x < 0.2f) { grassDominant++; }
                    if (value.x > 0.65f) { soilDominant++; }
                }
            }
            Assert.That(maximum.x - minimum.x, Is.GreaterThan(0.65f));
            Assert.That(maximum.y - minimum.y, Is.GreaterThan(0.3f));
            Assert.That(grassDominant, Is.GreaterThan(30), "Retain substantial grass regions, not an all-soil replacement.");
            Assert.That(soilDominant, Is.GreaterThan(30), "Exposed earth should form visible patches, not a uniform color tint.");
            Assert.That(variationX, Is.GreaterThan(20f));
            Assert.That(variationZ, Is.GreaterThan(20f), "Wear must vary in both axes, not reproduce a one-road layout.");
            Assert.That(channelDifference, Is.GreaterThan(50f), "Soil weight and vegetation variation have separate roles.");

            foreach (float seam in new[] { 0f, 64f })
            {
                for (float other = -56f; other <= 120f; other += 4f)
                {
                    Assert.That(Vector2.Distance(sample(seam - 0.001f, other), sample(seam + 0.001f, other)),
                        Is.LessThan(0.005f), "No west/east chunk-local blend restart.");
                    Assert.That(Vector2.Distance(sample(other, seam - 0.001f), sample(other, seam + 0.001f)),
                        Is.LessThan(0.005f), "No south/north chunk-local blend restart.");
                }
            }
            for (int camp = -1; camp < PveRegionLayout.CampCount; camp++)
            {
                Float3 center = camp < 0 ? PveRegionLayout.Refuge : PveRegionLayout.GetCampCenter(camp);
                Assert.That(sample(center.X, center.Z).x, Is.GreaterThan(0.5f),
                    "Every combat clearing/refuge retains ground wear while allowing grass to soften its edges.");
            }
        }

        [Test]
        public void ClearingBlend_UsesDistanceFalloffAroundAllAuthoritativeCamps()
        {
            Type authoring = typeof(SessionRpgAuthoringGenerator).Assembly.GetType(
                "SessionRpg.Editor.Authoring.SunbreakBasinAuthoring");
            Assert.That(authoring, Is.Not.Null);
            MethodInfo method = authoring.GetMethod("ClearingWeight", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            for (int index = -1; index < PveRegionLayout.CampCount; index++)
            {
                Float3 center = index < 0 ? PveRegionLayout.Refuge : PveRegionLayout.GetCampCenter(index);
                float weight = (float)method.Invoke(null, new object[] { center.X, center.Z, 0.5f });
                Assert.That(weight, Is.GreaterThan(0.8f),
                    "Every camp/refuge must wear into the meadow; Unity SmoothStep takes a normalized t.");
            }

            Float3 first = PveRegionLayout.GetCampCenter(0);
            float middle = (float)method.Invoke(null, new object[] { first.X + 10.5f, first.Z, 0.5f });
            float outside = (float)method.Invoke(null, new object[] { first.X + 17f, first.Z, 0.5f });
            Assert.That(middle, Is.InRange(0.2f, 0.65f));
            Assert.That(outside, Is.LessThan(0.05f));
        }

        private static bool OutsideBounds(Vector3 point) =>
            point.x <= PveRegionLayout.MinimumX || point.x >= PveRegionLayout.MaximumX ||
            point.z <= PveRegionLayout.MinimumZ || point.z >= PveRegionLayout.MaximumZ;

        private static string ScanPath(string sourceName, string channel) =>
            ScanFolder + sourceName + "/" + sourceName + "_" + channel +
            (IsOneKSource(sourceName) ? "_1k.jpg" : "_2k.jpg");

        private static bool IsOneKSource(string sourceName) => sourceName == "leafy_grass" || sourceName == "dirt_floor";

        private static void AssertBoundTexture(Material material, string property, string sourceName, string channel)
        {
            Texture texture = material.GetTexture(property);
            Assert.That(texture, Is.Not.Null, property);
            Assert.That(AssetDatabase.GetAssetPath(texture), Is.EqualTo(ScanPath(sourceName, channel)), property);
        }

        private static Func<float, float, Vector2> GroundBlendSampler()
        {
            Type authoring = typeof(SessionRpgAuthoringGenerator).Assembly.GetType(
                "SessionRpg.Editor.Authoring.SunbreakScannedSurfaceAuthoring");
            Assert.That(authoring, Is.Not.Null);
            MethodInfo method = authoring.GetMethod("SampleGroundBlend", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Func<float, float, Vector2>)Delegate.CreateDelegate(typeof(Func<float, float, Vector2>), method);
        }

        private static string ShaderFunctionBody(string functionName)
        {
            string source = ShaderSourceWithoutComments();
            Match signature = Regex.Match(source, @"\b" + Regex.Escape(functionName) + @"\s*\(");
            Assert.That(signature.Success, Is.True, functionName);
            int start = source.IndexOf('{', signature.Index + signature.Length);
            Assert.That(start, Is.GreaterThanOrEqualTo(0), functionName);
            int depth = 1;
            for (int index = start + 1; index < source.Length; index++)
            {
                if (source[index] == '{') { depth++; }
                else if (source[index] == '}' && --depth == 0)
                {
                    return source.Substring(start + 1, index - start - 1);
                }
            }
            Assert.Fail("Unclosed shader function: " + functionName);
            return string.Empty;
        }

        private static string ShaderSourceWithoutComments() => Regex.Replace(
            File.ReadAllText("Assets/SessionRpg/Shaders/SunbreakTerrain.shader"),
            @"//[^\r\n]*|/\*.*?\*/", string.Empty, RegexOptions.Singleline);

        private static void AssertUnionShelf(MeshFilter filter, int landmarkIndex)
        {
            Assert.That(filter, Is.Not.Null);
            AssertSurfaceFrameData(filter);
            Vector3[] vertices = filter.sharedMesh.vertices;
            ArenaCircleObstacle landmark = PveRegionLayout.GetLandmark(landmarkIndex);
            float minimumUpperHeight = float.PositiveInfinity;
            float maximumUpperHeight = float.NegativeInfinity;
            float minimumToeRadius = float.PositiveInfinity;
            float maximumToeRadius = 0f;
            for (int index = 0; index < vertices.Length; index++)
            {
                Vector3 point = filter.transform.TransformPoint(vertices[index]);
                vertices[index] = point;
                if (point.y > 0.3f)
                {
                    minimumUpperHeight = Mathf.Min(minimumUpperHeight, point.y);
                    maximumUpperHeight = Mathf.Max(maximumUpperHeight, point.y);
                }
                if (point.y <= 0.18f)
                {
                    float dx = point.x - landmark.Center.X;
                    float dz = point.z - landmark.Center.Z;
                    float radius = Mathf.Sqrt(dx * dx + dz * dz);
                    minimumToeRadius = Mathf.Min(minimumToeRadius, radius);
                    maximumToeRadius = Mathf.Max(maximumToeRadius, radius);
                }
            }
            Assert.That(maximumToeRadius - minimumToeRadius, Is.GreaterThan(0.75f),
                "The continuous shelf must follow the irregular union, not a circular model-base perimeter.");
            Assert.That(maximumUpperHeight - minimumUpperHeight,
                Is.GreaterThan(landmarkIndex < PveRegionLayout.MajorObstacleCount ? 0.25f : 0.12f),
                "The geological shelf requires relief rather than a flat plinth.");

            int start = PveRegionLayout.GetLandmarkObstacleStart(landmarkIndex);
            int count = PveRegionLayout.GetLandmarkObstacleCount(landmarkIndex);
            int exposedSamples = 0;
            for (int component = start; component < start + count; component++)
            {
                ArenaCircleObstacle circle = PveRegionLayout.GetObstacle(component);
                for (int arc = 0; arc < 64; arc++)
                {
                    float angle = arc * Mathf.PI * 2f / 64f;
                    Vector3 boundary = new Vector3(circle.Center.X + Mathf.Cos(angle) * circle.Radius,
                        0f, circle.Center.Z + Mathf.Sin(angle) * circle.Radius);
                    bool internalArc = false;
                    for (int other = start; other < start + count; other++)
                    {
                        if (other == component) { continue; }
                        ArenaCircleObstacle neighbor = PveRegionLayout.GetObstacle(other);
                        float dx = boundary.x - neighbor.Center.X;
                        float dz = boundary.z - neighbor.Center.Z;
                        internalArc |= dx * dx + dz * dz < neighbor.Radius * neighbor.Radius;
                    }
                    if (internalArc) { continue; }
                    exposedSamples++;
                    float nearestToeSquared = float.PositiveInfinity;
                    foreach (Vector3 point in vertices)
                    {
                        if (point.y > 0.18f) { continue; }
                        float dx = point.x - boundary.x;
                        float dz = point.z - boundary.z;
                        nearestToeSquared = Mathf.Min(nearestToeSquared, dx * dx + dz * dz);
                    }
                    Assert.That(nearestToeSquared, Is.LessThan(1.3f * 1.3f),
                        "Exposed union boundary needs visible ground-level rock within one clipped grid cell: landmark " +
                        landmarkIndex + ", component " + component + ", arc " + arc);
                }
            }
            Assert.That(exposedSamples, Is.GreaterThan(24), "The test must sample the exterior union, not hidden overlapping arcs.");

            int[] indices = filter.sharedMesh.triangles;
            for (int index = 0; index < indices.Length; index += 3)
            {
                Vector3 centroid = (vertices[indices[index]] + vertices[indices[index + 1]] + vertices[indices[index + 2]]) / 3f;
                if (centroid.y <= 0.181f) { continue; }
                Assert.That(IsCoreBlocked(centroid), Is.True,
                    "Shelf triangles must not bridge traversable concavities between circle components.");
            }
        }

        private static void AssertIndividualLeafSurface(MeshFilter filter)
        {
            Assert.That(filter, Is.Not.Null);
            AssertSurfaceFrameData(filter);
            Mesh mesh = filter.sharedMesh;
            Vector2[] uvs = mesh.uv;
            Vector2 minimum = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
            Vector2 maximum = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
            foreach (Vector2 uv in uvs)
            {
                Assert.That(uv.x, Is.InRange(0f, 1f),
                    "Each leaf uses its own authored texture coordinates rather than a projected canopy blob.");
                Assert.That(uv.y, Is.InRange(0f, 1f));
                minimum = Vector2.Min(minimum, uv);
                maximum = Vector2.Max(maximum, uv);
            }
            Assert.That(minimum, Is.EqualTo(Vector2.zero));
            Assert.That(maximum, Is.EqualTo(Vector2.one));
            Assert.That(mesh.vertexCount, Is.GreaterThanOrEqualTo(180),
                "The canopy needs individually articulated leaves, not a handful of solid polyhedra.");
        }

        private static void AssertSurfaceFrameData(MeshFilter filter)
        {
            Assert.That(filter, Is.Not.Null);
            Mesh mesh = filter.sharedMesh;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            Vector2[] uvs = mesh.uv;
            Assert.That(normals, Has.Length.EqualTo(vertices.Length), filter.name);
            Assert.That(tangents, Has.Length.EqualTo(vertices.Length), filter.name);
            Assert.That(uvs, Has.Length.EqualTo(vertices.Length), filter.name);
            for (int index = 0; index < vertices.Length; index++)
            {
                Assert.That(IsFinite(vertices[index].x) && IsFinite(vertices[index].y) &&
                    IsFinite(vertices[index].z), Is.True, filter.name + " vertex");
                Assert.That(IsFinite(normals[index].x) && IsFinite(normals[index].y) &&
                    IsFinite(normals[index].z), Is.True, filter.name + " normal");
                Assert.That(IsFinite(tangents[index].x) && IsFinite(tangents[index].y) &&
                    IsFinite(tangents[index].z) && IsFinite(tangents[index].w), Is.True, filter.name + " tangent");
                Assert.That(IsFinite(uvs[index].x) && IsFinite(uvs[index].y), Is.True, filter.name + " UV");
            }

            int[] indices = mesh.triangles;
            for (int index = 0; index < indices.Length; index += 3)
            {
                Vector3 edgeA = vertices[indices[index + 1]] - vertices[indices[index]];
                Vector3 edgeB = vertices[indices[index + 2]] - vertices[indices[index]];
                if (Vector3.Cross(edgeA, edgeB).sqrMagnitude < 0.0000000001f) { continue; }
                for (int corner = 0; corner < 3; corner++)
                {
                    int vertex = indices[index + corner];
                    Vector3 tangent = tangents[vertex];
                    Assert.That(normals[vertex].sqrMagnitude, Is.EqualTo(1f).Within(0.001f), filter.name);
                    Assert.That(tangent.sqrMagnitude, Is.EqualTo(1f).Within(0.001f), filter.name);
                    Assert.That(Vector3.Dot(normals[vertex], tangent), Is.EqualTo(0f).Within(0.001f),
                        filter.name + " normal mapping needs an orthogonal tangent frame after footprint clamping.");
                }
            }
        }

        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static void AssertBoundaryBank(Transform visual, int x, int z)
        {
            Transform bank = visual.Find("OuterRidges/CoreBoundaryRockBank");
            Assert.That(bank, Is.Not.Null,
                "A natural rock bank must reveal the authoritative boundary before the distant ridges.");
            Mesh mesh = bank.GetComponent<MeshFilter>().sharedMesh;
            bool hasWest = false;
            bool hasEast = false;
            bool hasSouth = false;
            bool hasNorth = false;
            foreach (Vector3 vertex in mesh.vertices)
            {
                Vector3 point = bank.TransformPoint(vertex);
                Assert.That(OutsideBounds(point), Is.True,
                    "Every bank vertex, not merely its origin, must stay outside playable ground.");
                if (Mathf.Abs(point.y - 0.04f) > 0.0001f) { continue; }
                hasWest |= Mathf.Abs(point.x - PveRegionLayout.MinimumX) < 0.0001f;
                hasEast |= Mathf.Abs(point.x - PveRegionLayout.MaximumX) < 0.0001f;
                hasSouth |= Mathf.Abs(point.z - PveRegionLayout.MinimumZ) < 0.0001f;
                hasNorth |= Mathf.Abs(point.z - PveRegionLayout.MaximumZ) < 0.0001f;
            }
            if (x == -1) { Assert.That(hasWest, Is.True); }
            if (x == 1) { Assert.That(hasEast, Is.True); }
            if (z == -1) { Assert.That(hasSouth, Is.True); }
            if (z == 1) { Assert.That(hasNorth, Is.True); }
            Assert.That(bank.GetComponent<MeshRenderer>().bounds.max.y, Is.GreaterThan(1.8f));
            foreach (Vector3 normal in mesh.normals)
            {
                Assert.That(normal.y, Is.GreaterThanOrEqualTo(-0.001f),
                    "The bank's upper surface must not be backface-culled from the gameplay camera.");
            }
            var frames = new Dictionary<Vector3, Vector3>();
            Vector3[] points = mesh.vertices, normals = mesh.normals;
            int duplicateCount = 0;
            for (int vertex = 0; vertex < points.Length; vertex++)
            {
                if (frames.TryGetValue(points[vertex], out Vector3 shared))
                {
                    Assert.That(Vector3.Distance(normals[vertex], shared), Is.LessThan(0.00001f),
                        "Continuous bank lighting must not reveal each triangle as a separate flat face.");
                    duplicateCount++;
                }
                else { frames.Add(points[vertex], normals[vertex]); }
            }
            Assert.That(duplicateCount, Is.GreaterThan(points.Length / 2));
        }

        private static bool IsCoreBlocked(Vector3 point)
        {
            for (int index = 0; index < PveRegionLayout.ObstacleCount; index++)
            {
                ArenaCircleObstacle obstacle = PveRegionLayout.GetObstacle(index);
                float dx = point.x - obstacle.Center.X;
                float dz = point.z - obstacle.Center.Z;
                if (dx * dx + dz * dz <= (obstacle.Radius + 0.005f) * (obstacle.Radius + 0.005f)) { return true; }
            }
            return false;
        }
    }
}
