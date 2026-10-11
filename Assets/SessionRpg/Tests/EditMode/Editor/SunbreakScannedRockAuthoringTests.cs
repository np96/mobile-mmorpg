using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakScannedRockAuthoringTests
    {
        [Test]
        public void OriginalDelivery_KeepsAllFourOfficialHashesAndCc0Provenance()
        {
            SunbreakScannedRockAuthoring.ValidateSourceIntegrity();
            string folder = SunbreakScannedRockAuthoring.SourceFolder;
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(folder + "/manifest.json"));
            Assert.That(manifest.assetId, Is.EqualTo("rock_moss_set_01"));
            Assert.That(manifest.author, Is.EqualTo("Kless Gyzen"));
            Assert.That(manifest.license, Is.EqualTo("CC0-1.0"));
            Assert.That(manifest.status, Is.EqualTo("TECH_REVIEW"));
            Assert.That(manifest.maximumInstances, Is.EqualTo(1));
            Assert.That(manifest.runtimeTriangles, Is.EqualTo(5000));
            Assert.That(manifest.sourceTriangles, Is.EqualTo(63127));
            Assert.That(manifest.publishedLods, Is.False);
            Assert.That(manifest.files, Has.Length.EqualTo(4));
            long bytes = 0;
            foreach (SourceFile file in manifest.files)
            {
                string path = folder + "/" + file.path;
                Assert.That(file.url, Does.StartWith("https://dl.polyhaven.org/file/ph-assets/Models/"));
                Assert.That(new FileInfo(path).Length, Is.EqualTo(file.bytes));
                using (MD5 md5 = MD5.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    Assert.That(Hex(md5.ComputeHash(stream)), Is.EqualTo(file.md5));
                }
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path))
                {
                    Assert.That(Hex(sha.ComputeHash(stream)), Is.EqualTo(file.sha256));
                }
                bytes += file.bytes;
            }
            Assert.That(bytes, Is.EqualTo(4475602L));
            Assert.That(manifest.totalSourceBytes, Is.EqualTo(bytes));
            Assert.That(File.ReadAllText(folder + "/PROVENANCE.md"), Does.Contain("Kless Gyzen"));
            Assert.That(File.ReadAllText(folder + "/CC0-1.0.txt"), Does.Contain("CC0 1.0 Universal"));
        }

        [Test]
        public void ImportedBenchmark_RetainsOneScannedMeshAndNaturalUpAxis()
        {
            SunbreakScannedRockAuthoring.Validate();
            var importer = (ModelImporter)AssetImporter.GetAtPath(SunbreakScannedRockAuthoring.ModelPath);
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));
            Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.None));
            Assert.That(importer.addCollider || importer.importCameras || importer.importLights || importer.importBlendShapes,
                Is.False);
            Assert.That(importer.importNormals, Is.EqualTo(ModelImporterNormals.Import));
            Assert.That(importer.importTangents, Is.EqualTo(ModelImporterTangents.CalculateMikk));
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(SunbreakScannedRockAuthoring.ModelPath))
            {
                Assert.That(asset, Is.Not.InstanceOf<Material>(), "Source import must not manufacture embedded materials.");
                Assert.That(asset, Is.Not.InstanceOf<AnimationClip>());
            }
            MeshFilter source = SunbreakScannedRockAuthoring.RequireSourceRock();
            Mesh mesh = source.sharedMesh;
            Assert.That(mesh.subMeshCount, Is.EqualTo(1));
            Assert.That(mesh.GetIndexCount(0), Is.EqualTo(15000u));
            Assert.That(mesh.normals, Has.Length.EqualTo(mesh.vertexCount));
            Assert.That(mesh.tangents, Has.Length.EqualTo(mesh.vertexCount));
            Assert.That(mesh.uv, Has.Length.EqualTo(mesh.vertexCount));
            Vector3[] vertices = mesh.vertices;
            var sourceBounds = new Bounds(source.transform.TransformPoint(vertices[0]), Vector3.zero);
            foreach (Vector3 point in vertices) { sourceBounds.Encapsulate(source.transform.TransformPoint(point)); }
            // Official glTF position bounds establish the physical dimensions.
            // A Blender/FBX axis conversion error would swap height with depth.
            Assert.That(sourceBounds.size.x, Is.EqualTo(2.10858f).Within(0.035f));
            Assert.That(sourceBounds.size.y, Is.EqualTo(1.09969f).Within(0.035f));
            Assert.That(sourceBounds.size.z, Is.EqualTo(2.07958f).Within(0.035f));
            TestContext.WriteLine("Selected imported source node {0}, mesh {1}, size {2}.",
                source.name, mesh.name, sourceBounds.size);
        }

        [TestCase("rock_moss_set_01_diff_1k.jpg", true, false)]
        [TestCase("rock_moss_set_01_nor_gl_1k.exr", false, true)]
        [TestCase("rock_moss_set_01_rough_1k.jpg", false, false)]
        public void Textures_PreserveMeasuredChannelsAndBoundedMobileImports(string file, bool color, bool normal)
        {
            string path = SunbreakScannedRockAuthoring.SourceFolder + "/" + file;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer.sRGBTexture, Is.EqualTo(color));
            Assert.That(importer.textureType, Is.EqualTo(normal ? TextureImporterType.NormalMap : TextureImporterType.Default));
            Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D));
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            Assert.That(texture, Is.Not.Null, "A cubemap import must never silently yield a null Texture2D material binding.");
            Assert.That(texture.dimension, Is.EqualTo(TextureDimension.Tex2D));
            Assert.That(texture.width, Is.EqualTo(1024));
            Assert.That(texture.height, Is.EqualTo(1024));
            Assert.That(importer.convertToNormalmap || importer.flipGreenChannel || importer.isReadable, Is.False);
            Assert.That(importer.mipmapEnabled && importer.streamingMipmaps, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Trilinear));
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            Assert.That(android.overridden, Is.True);
            Assert.That(android.maxTextureSize, Is.EqualTo(1024));
            Assert.That(android.format, Is.EqualTo(normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6));
            Assert.That(android.compressionQuality, Is.EqualTo(100));
        }

        [Test]
        public void Material_PersistsItsMeasuredBindingsAcrossAnImportRefresh()
        {
            SunbreakScannedRockAuthoring.Validate();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SunbreakScannedRockAuthoring.MaterialPath);
            Assert.That(EditorUtility.IsDirty(material), Is.False,
                "The benchmark must save its fully configured material before later chunk import refreshes.");
            AssetDatabase.ImportAsset(SunbreakScannedRockAuthoring.MaterialPath,
                ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            SunbreakScannedRockAuthoring.Validate();
            material = AssetDatabase.LoadAssetAtPath<Material>(SunbreakScannedRockAuthoring.MaterialPath);
            Assert.That(material.GetTexture("_BaseMap"), Is.InstanceOf<Texture2D>());
            Assert.That(material.GetTexture("_BumpMap"), Is.InstanceOf<Texture2D>());
            Assert.That(material.GetTexture("_RoughnessMap"), Is.InstanceOf<Texture2D>());
            Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Geometry));
            Assert.That(material.enableInstancing, Is.True);
        }

        [Test]
        public void GeneratedBasin_ContainsExactlyOneGroundedFiveThousandTriangleBenchmark()
        {
            int total = 0;
            ArenaCircleObstacle landmark = PveRegionLayout.GetLandmark(7);
            Vector3 center = new Vector3(landmark.Center.X + 1.2f, 0f, landmark.Center.Z - 1.4f);
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    string path = "Assets/SessionRpg/Content/Worlds/SunbreakBasin/Chunks/Chunk_" + x + "_" + z + ".unity";
                    Scene scene = SceneManager.GetSceneByPath(path);
                    bool opened = !scene.IsValid() || !scene.isLoaded;
                    try
                    {
                        if (opened) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
                        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
                        {
                            foreach (Transform root in sceneRoot.GetComponentsInChildren<Transform>(true))
                            {
                                if (root.name != SunbreakScannedRockAuthoring.InstanceName) { continue; }
                                total++;
                                Assert.That(root.parent.name, Is.EqualTo("Blocker_7"));
                                Assert.That(root.gameObject.layer, Is.EqualTo(2));
                                Assert.That(root.childCount, Is.Zero, "Never instantiate the six-rock source prefab.");
                                Assert.That(root.GetComponents<Component>(), Has.Length.EqualTo(3));
                                MeshFilter filter = root.GetComponent<MeshFilter>();
                                MeshRenderer renderer = root.GetComponent<MeshRenderer>();
                                Assert.That(filter, Is.Not.Null);
                                Assert.That(renderer, Is.Not.Null);
                                Assert.That(filter.sharedMesh, Is.SameAs(SunbreakScannedRockAuthoring.RequireSourceRock().sharedMesh));
                                Assert.That(filter.sharedMesh.GetIndexCount(0), Is.EqualTo(15000u));
                                Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(1));
                                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterial),
                                    Is.EqualTo(SunbreakScannedRockAuthoring.MaterialPath));
                                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
                                // FBX centimeters use a ~100 import scale. Matrix
                                // decomposition may differ by a few float ULPs;
                                // compare relative proportions, not absolute units.
                                Assert.That(root.lossyScale.x, Is.GreaterThan(0f));
                                Assert.That(root.lossyScale.y / root.lossyScale.x,
                                    Is.EqualTo(1f).Within(0.000001f));
                                Assert.That(root.lossyScale.z / root.lossyScale.x,
                                    Is.EqualTo(1f).Within(0.000001f));
                                var worldBounds = new Bounds(root.TransformPoint(filter.sharedMesh.vertices[0]), Vector3.zero);
                                foreach (Vector3 point in filter.sharedMesh.vertices)
                                {
                                    Vector3 world = root.TransformPoint(point);
                                    worldBounds.Encapsulate(world);
                                    Assert.That(new Vector2(world.x - center.x, world.z - center.z).magnitude,
                                        Is.LessThanOrEqualTo(SunbreakScannedRockAuthoring.MaximumHorizontalRadius + 0.0001f));
                                    Assert.That(new Vector2(world.x - landmark.Center.X, world.z - landmark.Center.Z).magnitude,
                                        Is.LessThan(landmark.Radius), "The entire scanned silhouette must remain inside Core cover.");
                                }
                                Assert.That(worldBounds.center.x, Is.EqualTo(center.x).Within(0.0001f));
                                Assert.That(worldBounds.center.z, Is.EqualTo(center.z).Within(0.0001f));
                                Assert.That(worldBounds.size.y, Is.InRange(0.95f, 1.13f));
                                Assert.That(worldBounds.min.y, Is.InRange(0f, 0.7f));
                            }
                        }
                    }
                    finally
                    {
                        if (opened && scene.IsValid() && scene.isLoaded) { EditorSceneManager.CloseScene(scene, true); }
                    }
                }
            }
            Assert.That(total, Is.EqualTo(1), "This is one camp benchmark, not blanket high-poly replacement.");
        }

        [Test]
        public void InvalidParent_IsRejectedBeforeImportOrObjectCreation()
        {
            Assert.Throws<ArgumentNullException>(() =>
                SunbreakScannedRockAuthoring.CreateBenchmarkRock(null, Vector3.zero, 0f));
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();

        [Serializable]
        private sealed class Manifest
        {
            public string assetId;
            public string author;
            public string license;
            public string status;
            public int maximumInstances;
            public int runtimeTriangles;
            public int sourceTriangles;
            public bool publishedLods;
            public long totalSourceBytes;
            public SourceFile[] files;
        }

        [Serializable]
        private sealed class SourceFile
        {
            public string path;
            public long bytes;
            public string md5;
            public string sha256;
            public string url;
        }
    }
}
