using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakFortAssetsTests
    {
        [Test]
        public void OriginalDelivery_PreservesOfficialHashesAndCc0Evidence()
        {
            SunbreakFortAssets.ValidateSourceIntegrity();
            string folder = SunbreakFortAssets.SourceFolder;
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(folder + "/manifest.json"));
            Assert.That(manifest.assetId, Is.EqualTo("modular_fort_01"));
            Assert.That(manifest.intakeId, Is.EqualTo("POLYHAVEN-2026-004"));
            Assert.That(manifest.author, Is.EqualTo("Rico Cilliers"));
            Assert.That(manifest.license, Is.EqualTo("CC0-1.0"));
            Assert.That(manifest.status, Is.EqualTo("TECH_REVIEW"));
            Assert.That(manifest.maximumCompositionTriangles, Is.EqualTo(SunbreakFortAssets.MaximumCompositionTriangles));
            Assert.That(manifest.files, Has.Length.EqualTo(7));
            long bytes = 0;
            foreach (SourceFile file in manifest.files)
            {
                string path = folder + "/" + file.path;
                Assert.That(file.url, Does.StartWith("https://dl.polyhaven.org/file/ph-assets/Models/"));
                Assert.That(new FileInfo(path).Length, Is.EqualTo(file.bytes));
                using (MD5 md5 = MD5.Create())
                using (FileStream stream = File.OpenRead(path)) { Assert.That(Hex(md5.ComputeHash(stream)), Is.EqualTo(file.md5)); }
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(path)) { Assert.That(Hex(sha.ComputeHash(stream)), Is.EqualTo(file.sha256)); }
                bytes += file.bytes;
            }
            Assert.That(bytes, Is.EqualTo(32523603L));
            Assert.That(manifest.totalSourceBytes, Is.EqualTo(bytes));
            Assert.That(File.ReadAllText(folder + "/PROVENANCE.md"), Does.Contain("Rico Cilliers"));
            Assert.That(File.ReadAllText(folder + "/CC0-1.0.txt"), Does.Contain("CC0 1.0 Universal"));
        }

        [TestCase(SunbreakFortAssets.Module.RoundTower, 4772, 15.838445f, 13.498152f, 15.838445f)]
        [TestCase(SunbreakFortAssets.Module.ThinGate, 1368, 2.671970f, 8.616226f, 7.410237f)]
        [TestCase(SunbreakFortAssets.Module.LowWall, 406, 2.549778f, 7.793655f, 7.410239f)]
        [TestCase(SunbreakFortAssets.Module.BattlementWall, 622, 2.549778f, 8.605536f, 7.410239f)]
        [TestCase(SunbreakFortAssets.Module.ThickEnd, 442, 4.172073f, 7.793655f, 4.849300f)]
        [TestCase(SunbreakFortAssets.Module.ThinCorner, 654, 4.563530f, 8.605457f, 4.557765f)]
        public void StableNodes_PreserveTwoMaterialMeshesAndVerifiedMeterDimensions(SunbreakFortAssets.Module module,
            int triangles, float x, float y, float z)
        {
            MeshFilter source = SunbreakFortAssets.RequireSource(module);
            Mesh mesh = source.sharedMesh;
            Assert.That(source.name, Does.EndWith(SunbreakFortAssets.SourceNodeName(module)));
            Assert.That(mesh.subMeshCount, Is.EqualTo(2));
            Assert.That(mesh.GetIndexCount(0) + mesh.GetIndexCount(1), Is.EqualTo(triangles * 3u));
            Assert.That(SunbreakFortAssets.TriangleCount(module), Is.EqualTo(triangles));
            Assert.That(mesh.uv, Has.Length.EqualTo(mesh.vertexCount));
            Assert.That(mesh.normals, Has.Length.EqualTo(mesh.vertexCount));
            Assert.That(mesh.tangents, Has.Length.EqualTo(mesh.vertexCount));
            foreach (Vector2 uv in mesh.uv)
            {
                Assert.That(float.IsNaN(uv.x) || float.IsNaN(uv.y) || float.IsInfinity(uv.x) || float.IsInfinity(uv.y), Is.False);
            }
            Bounds bounds = SunbreakFortAssets.GroundedSourceBounds(module);
            Assert.That(bounds.size.x, Is.EqualTo(x).Within(0.035f));
            Assert.That(bounds.size.y, Is.EqualTo(y).Within(0.035f));
            Assert.That(bounds.size.z, Is.EqualTo(z).Within(0.035f));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(bounds.center.x, Is.Zero);
            Assert.That(bounds.center.z, Is.Zero);
            Vector3[] vertices = mesh.vertices;
            var rawUnityBounds = new Bounds(source.transform.TransformPoint(vertices[0]), Vector3.zero);
            foreach (Vector3 point in vertices) { rawUnityBounds.Encapsulate(source.transform.TransformPoint(point)); }
            Assert.That(rawUnityBounds.size.x, Is.EqualTo(z).Within(0.035f), "Record raw Unity FBX orientation separately from canonical wall layout.");
            Assert.That(rawUnityBounds.size.y, Is.EqualTo(y).Within(0.035f));
            Assert.That(rawUnityBounds.size.z, Is.EqualTo(x).Within(0.035f));
            Assert.That(SunbreakFortAssets.WallSubmesh(module), Is.EqualTo(module == SunbreakFortAssets.Module.ThinGate ? 0 : 1));
            Assert.That(SunbreakFortAssets.TrimSubmesh(module), Is.EqualTo(module == SunbreakFortAssets.Module.ThinGate ? 1 : 0));
        }

        [TestCase("wall", "diff", "jpg", true, false)]
        [TestCase("wall", "nor_gl", "exr", false, true)]
        [TestCase("wall", "rough", "jpg", false, false)]
        [TestCase("trim", "diff", "jpg", true, false)]
        [TestCase("trim", "nor_gl", "exr", false, true)]
        [TestCase("trim", "rough", "jpg", false, false)]
        public void Textures_AreMeasuredTwoKilopixelSourcesWithOneKilopixelMobileCaps(string family, string channel,
            string extension, bool color, bool normal)
        {
            string path = SunbreakFortAssets.SourceFolder + "/modular_fort_01_" + family + "_" + channel + "_2k." + extension;
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            Assert.That(importer, Is.Not.Null);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Assert.That(width, Is.EqualTo(2048));
            Assert.That(height, Is.EqualTo(2048));
            Assert.That(importer.sRGBTexture, Is.EqualTo(color));
            Assert.That(importer.textureType, Is.EqualTo(normal ? TextureImporterType.NormalMap : TextureImporterType.Default));
            Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D));
            Assert.That(importer.convertToNormalmap || importer.flipGreenChannel || importer.isReadable, Is.False);
            Assert.That(importer.mipmapEnabled && importer.streamingMipmaps, Is.True);
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            Assert.That(android.overridden, Is.True);
            Assert.That(android.maxTextureSize, Is.EqualTo(1024));
            Assert.That(android.format, Is.EqualTo(normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6));
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(path).dimension, Is.EqualTo(TextureDimension.Tex2D));
        }

        [TestCase(SunbreakFortAssets.Module.RoundTower, 0f)]
        [TestCase(SunbreakFortAssets.Module.LowWall, 90f)]
        [TestCase(SunbreakFortAssets.Module.BattlementWall, 37f)]
        [TestCase(SunbreakFortAssets.Module.ThickEnd, 135f)]
        [TestCase(SunbreakFortAssets.Module.ThinCorner, 0f)]
        [TestCase(SunbreakFortAssets.Module.ThinCorner, 90f)]
        public void Placement_PreservesUvGeometryAndGroundsWholeMeshUnderTranslatedRotatedParent(SunbreakFortAssets.Module module, float yaw)
        {
            var parent = new GameObject("FortPlacementTestOwner");
            try
            {
                parent.transform.SetPositionAndRotation(new Vector3(-64f, 3f, 5f), Quaternion.Euler(0f, 13f, 0f));
                var ground = new Vector3(-16f, 0.08f, 5f);
                const float scale = 0.65f;
                Transform instance = SunbreakFortAssets.Create(parent.transform, "FortModuleTest", module, ground, yaw, scale);
                MeshFilter source = SunbreakFortAssets.RequireSource(module);
                Mesh mesh = instance.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(mesh, Is.SameAs(SunbreakFortAssets.RequireRenderMesh(module)));
                Assert.That(mesh.vertices, Is.EqualTo(source.sharedMesh.vertices));
                Assert.That(mesh.normals, Is.EqualTo(source.sharedMesh.normals));
                Assert.That(mesh.uv, Is.EqualTo(source.sharedMesh.uv));
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                { Assert.That(mesh.GetIndices(submesh), Is.EqualTo(source.sharedMesh.GetIndices(submesh))); }
                Vector3[] sourceNormals = mesh.normals;
                Vector4[] renderTangents = mesh.tangents;
                Vector4[] sourceTangents = source.sharedMesh.tangents;
                for (int i = 0; i < renderTangents.Length; i++)
                {
                    Vector3 tangent = renderTangents[i];
                    Assert.That(tangent.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));
                    Assert.That(Vector3.Dot(sourceNormals[i], tangent), Is.EqualTo(0f).Within(0.001f));
                    Assert.That(renderTangents[i].w, Is.EqualTo(sourceTangents[i].w < 0f ? -1f : 1f));
                }
                Assert.That(instance.childCount, Is.Zero);
                Assert.That(instance.GetComponents<Component>(), Has.Length.EqualTo(3));
                Assert.That(instance.gameObject.layer, Is.EqualTo(2));
                Assert.That(instance.lossyScale.x / source.transform.lossyScale.x, Is.EqualTo(scale).Within(0.00001f));
                Assert.That(instance.lossyScale.y / instance.lossyScale.x, Is.EqualTo(1f).Within(0.00001f));
                Assert.That(instance.lossyScale.z / instance.lossyScale.x, Is.EqualTo(1f).Within(0.00001f));
                MeshRenderer renderer = instance.GetComponent<MeshRenderer>();
                Assert.That(renderer.sharedMaterials, Has.Length.EqualTo(2));
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterials[SunbreakFortAssets.WallSubmesh(module)]), Is.EqualTo(SunbreakFortAssets.WallMaterialPath));
                Assert.That(AssetDatabase.GetAssetPath(renderer.sharedMaterials[SunbreakFortAssets.TrimSubmesh(module)]), Is.EqualTo(SunbreakFortAssets.TrimMaterialPath));
                Assert.That(Quaternion.Angle(instance.rotation, Quaternion.Euler(0f, yaw + 90f, 0f) * source.transform.rotation), Is.LessThan(0.02f));
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
                Vector3[] vertices = mesh.vertices;
                var worldBounds = new Bounds(instance.TransformPoint(vertices[0]), Vector3.zero);
                // Undo the requested yaw when checking centered placement, since
                // an irregular rotated wall need not have a symmetric world AABB.
                Quaternion undoYaw = Quaternion.Euler(0f, -yaw, 0f);
                var unrotatedBounds = new Bounds(undoYaw * (instance.TransformPoint(vertices[0]) - ground), Vector3.zero);
                foreach (Vector3 point in vertices)
                {
                    Vector3 world = instance.TransformPoint(point);
                    worldBounds.Encapsulate(world);
                    unrotatedBounds.Encapsulate(undoYaw * (world - ground));
                }
                Assert.That(worldBounds.min.y, Is.EqualTo(ground.y).Within(0.0002f));
                Assert.That(unrotatedBounds.center.x, Is.EqualTo(0f).Within(0.0002f));
                Assert.That(unrotatedBounds.center.z, Is.EqualTo(0f).Within(0.0002f));
                Assert.That(worldBounds.size.y, Is.EqualTo(SunbreakFortAssets.GroundedSourceBounds(module).size.y * scale).Within(0.001f));
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        [Test]
        public void Validation_IsReadOnlyAndImportsNoEmbeddedMaterialAnimationOrPhysics()
        {
            var paths = new[] { SunbreakFortAssets.ModelPath + ".meta", SunbreakFortAssets.WallMaterialPath, SunbreakFortAssets.TrimMaterialPath };
            var before = new string[paths.Length];
            for (int i = 0; i < paths.Length; i++) { before[i] = File.ReadAllText(paths[i]); }
            SunbreakFortAssets.Validate();
            for (int i = 0; i < paths.Length; i++) { Assert.That(File.ReadAllText(paths[i]), Is.EqualTo(before[i])); }
            var importer = (ModelImporter)AssetImporter.GetAtPath(SunbreakFortAssets.ModelPath);
            Assert.That(importer.materialImportMode, Is.EqualTo(ModelImporterMaterialImportMode.None));
            Assert.That(importer.importAnimation || importer.addCollider || importer.importCameras || importer.importLights || importer.importBlendShapes, Is.False);
            Assert.That(importer.swapUVChannels || importer.generateSecondaryUV, Is.False);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(SunbreakFortAssets.ModelPath))
            {
                Assert.That(asset, Is.Not.InstanceOf<Material>());
                Assert.That(asset, Is.Not.InstanceOf<AnimationClip>());
            }
        }

        [Test]
        public void AdmittedSolidArchitecture_CanFitOneTowerAndFourWallsBelowTenThousandTriangles()
        {
            int triangles = SunbreakFortAssets.TriangleCount(SunbreakFortAssets.Module.RoundTower) +
                2 * SunbreakFortAssets.TriangleCount(SunbreakFortAssets.Module.LowWall) +
                SunbreakFortAssets.TriangleCount(SunbreakFortAssets.Module.BattlementWall) +
                SunbreakFortAssets.TriangleCount(SunbreakFortAssets.Module.ThickEnd);
            Assert.That(triangles, Is.EqualTo(6648));
            Assert.That(triangles, Is.LessThanOrEqualTo(SunbreakFortAssets.MaximumCompositionTriangles));
        }

        [Test]
        public void GenuineCorner_IsAnExplicitBoundedAdmissionFromTheUnchangedDelivery()
        {
            Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(SunbreakFortAssets.SourceFolder + "/manifest.json"));
            Assert.That(manifest.runtimeModules, Has.Length.EqualTo(6));
            foreach (SunbreakFortAssets.Module module in Enum.GetValues(typeof(SunbreakFortAssets.Module)))
            {
                ModuleRecord selected = Array.Find(manifest.runtimeModules, item => item.node == SunbreakFortAssets.SourceNodeName(module));
                Assert.That(selected, Is.Not.Null);
                Assert.That(selected.triangles, Is.EqualTo(SunbreakFortAssets.TriangleCount(module)));
                Assert.That(selected.submeshTriangles, Has.Length.EqualTo(2));
                Assert.That(selected.submeshTriangles[0] + selected.submeshTriangles[1], Is.EqualTo(selected.triangles));
            }
            ModuleRecord corner = Array.Find(manifest.runtimeModules,
                item => item.node == "modular_fort_01_wall_thin_corner_01");
            Assert.That(corner.submeshTriangles, Is.EqualTo(new[] { 64, 590 }),
                "The source manifest records wall/trim roles; Unity orders those material sections by first use.");
        }

        [Test]
        public void InvalidPlacements_AreRejectedBeforeCreatingObjects()
        {
            Assert.Throws<ArgumentNullException>(() => SunbreakFortAssets.Create(null, "Test", SunbreakFortAssets.Module.LowWall, Vector3.zero, 0f, 1f));
            Assert.Throws<ArgumentOutOfRangeException>(() => SunbreakFortAssets.TriangleCount((SunbreakFortAssets.Module)999));
            var parent = new GameObject("InvalidFortPlacementTest");
            try
            {
                Assert.Throws<ArgumentOutOfRangeException>(() => SunbreakFortAssets.Create(parent.transform, "Test", SunbreakFortAssets.Module.LowWall, Vector3.zero, 0f, -1f));
                Assert.Throws<ArgumentOutOfRangeException>(() => SunbreakFortAssets.Create(parent.transform, "Test", SunbreakFortAssets.Module.LowWall, Vector3.zero, float.NaN, 1f));
                parent.transform.localScale = new Vector3(1f, 2f, 1f);
                Assert.Throws<ArgumentException>(() => SunbreakFortAssets.Create(parent.transform, "Test", SunbreakFortAssets.Module.LowWall, Vector3.zero, 0f, 1f));
                Assert.That(parent.transform.childCount, Is.Zero);
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }

        private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", string.Empty).ToLowerInvariant();
        [Serializable] private sealed class Manifest
        {
            public string intakeId;
            public string assetId;
            public string author;
            public string license;
            public string status;
            public int maximumCompositionTriangles;
            public long totalSourceBytes;
            public SourceFile[] files;
            public ModuleRecord[] runtimeModules;
        }
        [Serializable] private sealed class ModuleRecord
        {
            public string node;
            public int triangles;
            public int[] submeshTriangles;
        }
        [Serializable] private sealed class SourceFile
        {
            public string path;
            public long bytes;
            public string md5;
            public string sha256;
            public string url;
        }
    }
}
