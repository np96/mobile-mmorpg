using System;
using System.IO;
using NUnit.Framework;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakVegetationAssetsTests
    {
        [Test]
        public void OriginalAndDerivedInputs_RetainLockedManifestsProcedureAtlasAndGeometryHashes()
        {
            Assert.DoesNotThrow(SunbreakVegetationAssets.ValidateOriginalSources);
            foreach (string folder in new[] { "TreeSmall02", "GrassMedium01", "Fern02" })
            {
                string source = "Assets/ThirdParty/PolyHaven/" + folder;
                Assert.That(File.ReadAllText(source + "/PROVENANCE.md"), Does.Contain("CC0"));
                Assert.That(File.ReadAllText(source + "/CC0-1.0.txt"), Does.Contain("CC0 1.0 Universal"));
            }
        }

        [TestCase("TreeSmall02", 2, 9328, 2.942012f, 4.575849f, 4.319017f)]
        [TestCase("GrassTinyA", 1, 79, 0.067348f, 0.112453f, 0.073769f)]
        [TestCase("GrassTinyB", 1, 28, 0.023570f, 0.060208f, 0.034399f)]
        [TestCase("GrassTinyC", 1, 40, 0.016031f, 0.082389f, 0.029004f)]
        [TestCase("GrassTinyD", 1, 32, 0.043040f, 0.047827f, 0.088582f)]
        [TestCase("GrassTinyE", 1, 62, 0.085265f, 0.049451f, 0.074152f)]
        [TestCase("GrassTinyF", 1, 60, 0.055196f, 0.047075f, 0.068890f)]
        [TestCase("Fern02a", 1, 784, 0.551941f, 0.286804f, 0.613421f)]
        public void GeneratedParts_KeepStableSingleSubmeshBudgetsAndGroundedMeterBounds(string selection,
            int partCount, int triangles, float x, float y, float z)
        {
            SunbreakVegetationAssets.Part[] parts = SunbreakVegetationAssets.GetParts(selection);
            Assert.That(parts, Has.Length.EqualTo(partCount));
            var bounds = new Bounds();
            bool first = true;
            int total = 0;
            foreach (SunbreakVegetationAssets.Part part in parts)
            {
                Mesh mesh = part.Mesh;
                Assert.That(AssetDatabase.GetAssetPath(mesh), Does.StartWith(SunbreakVegetationAssets.MeshFolder + "/"));
                Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                Assert.That(mesh.GetTopology(0), Is.EqualTo(MeshTopology.Triangles));
                Assert.That(mesh.normals, Has.Length.EqualTo(mesh.vertexCount));
                Assert.That(mesh.uv, Has.Length.EqualTo(mesh.vertexCount));
                Assert.That(mesh.tangents, Has.Length.EqualTo(mesh.vertexCount));
                total += (int)mesh.GetIndexCount(0) / 3;
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Assert.That(float.IsNaN(vertex.x) || float.IsNaN(vertex.y) || float.IsNaN(vertex.z) ||
                        float.IsInfinity(vertex.x) || float.IsInfinity(vertex.y) || float.IsInfinity(vertex.z), Is.False);
                    if (first) { bounds = new Bounds(vertex, Vector3.zero); first = false; }
                    else { bounds.Encapsulate(vertex); }
                }
                foreach (int index in mesh.triangles) { Assert.That(index, Is.InRange(0, mesh.vertexCount - 1)); }
                Assert.That(part.Material, Is.Not.Null);
            }
            Assert.That(total, Is.EqualTo(triangles));
            Assert.That(bounds.center.x, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(bounds.center.z, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(bounds.min.y, Is.EqualTo(0f).Within(0.00001f));
            Assert.That(bounds.size.x, Is.EqualTo(x).Within(0.00001f));
            Assert.That(bounds.size.y, Is.EqualTo(y).Within(0.00001f));
            Assert.That(bounds.size.z, Is.EqualTo(z).Within(0.00001f));
        }

        [TestCase("TreeTrunk", false)]
        [TestCase("TreeLeaf", true)]
        [TestCase("Grass", true)]
        [TestCase("Fern", true)]
        public void Materials_UseActualMappedNormalsRoughnessAndExplicitOpaqueOrCutoutRoles(string role, bool cutout)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SunbreakVegetationAssets.MaterialPath(role));
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo(cutout ? SunbreakVegetationAssets.VegetationShader : SunbreakVegetationAssets.OpaqueShader));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white));
            Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo(cutout ? 0f : 2f));
            Assert.That(material.renderQueue, Is.EqualTo((int)(cutout ? RenderQueue.AlphaTest : RenderQueue.Geometry)));
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(Vector2.one));
            Assert.That(material.GetTextureOffset("_BaseMap"), Is.EqualTo(Vector2.zero));
            Assert.That(material.enableInstancing, Is.True);
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_RoughnessMap" })
            {
                Assert.That(material.GetTexture(property), Is.InstanceOf<Texture2D>());
                Assert.That(AssetDatabase.GetAssetPath(material.GetTexture(property)), Does.StartWith("Assets/ThirdParty/PolyHaven/"));
            }
            if (cutout)
            {
                Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.4f));
                Assert.That(material.GetFloat("_AlphaFromBase"), Is.EqualTo(role == "TreeLeaf" ? 1f : 0f));
            }
            else { Assert.That(material.GetFloat("_Surface"), Is.Zero); }
        }

        [TestCase("TreeSmall02/Derived/TreeClusterAtlas.png", true)]
        [TestCase("GrassMedium01/grass_medium_01_alpha_2k.png", false)]
        [TestCase("Fern02/fern_02_alpha_2k.png", false)]
        public void MeasuredCoverage_KeepsMatchingCutoffAndPreservesAlphaAcrossMipmaps(string relativePath, bool rgba)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath("Assets/ThirdParty/PolyHaven/" + relativePath);
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.alphaSource, Is.EqualTo(rgba ? TextureImporterAlphaSource.FromInput : TextureImporterAlphaSource.FromGrayScale));
            Assert.That(importer.alphaIsTransparency, Is.EqualTo(rgba));
            Assert.That(importer.sRGBTexture, Is.EqualTo(rgba));
            Assert.That(importer.mipmapEnabled && importer.streamingMipmaps && importer.mipMapsPreserveCoverage, Is.True);
            Assert.That(importer.alphaTestReferenceValue, Is.EqualTo(0.4f));
            Assert.That(importer.maxTextureSize, Is.EqualTo(2048));
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            Assert.That(android.overridden, Is.True);
            Assert.That(android.maxTextureSize, Is.EqualTo(1024));
            Assert.That(android.format, Is.EqualTo(TextureImporterFormat.ASTC_4x4));
        }

        [Test]
        public void CutoutShader_UsesTheSameMeasuredCoverageInColorDepthNormalsAndShadowPasses()
        {
            string shader = File.ReadAllText("Assets/SessionRpg/Shaders/SunbreakVegetation.shader");
            Assert.That(shader, Does.Contain("SAMPLE_TEXTURE2D(_AlphaMap, sampler_AlphaMap, uv).a"));
            Assert.That(shader, Does.Contain("SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).a"));
            Assert.That(shader.Split(new[] { "ClipVegetation(input.uv);" }, StringSplitOptions.None).Length - 1, Is.EqualTo(3));
            Assert.That(shader.Split(new[] { "ClipVegetationShadow(input.uv);" }, StringSplitOptions.None).Length - 1, Is.EqualTo(1));
            Assert.That(shader, Does.Contain("AlphaClip(VegetationCoverage(uv), _Cutoff)"));
            Assert.That(shader, Does.Contain("clip(coverage - _Cutoff)"));
        }

        [Test]
        public void Validation_IsReadOnlyAndRequiresExistingGeneratedAssets()
        {
            string meshPath = SunbreakVegetationAssets.MeshPath("GrassTinyA", "Grass");
            string materialPath = SunbreakVegetationAssets.MaterialPath("TreeLeaf");
            byte[] meshBefore = File.ReadAllBytes(meshPath);
            string materialBefore = File.ReadAllText(materialPath);
            SunbreakVegetationAssets.Validate();
            Assert.That(File.ReadAllBytes(meshPath), Is.EqualTo(meshBefore));
            Assert.That(File.ReadAllText(materialPath), Is.EqualTo(materialBefore));
            Assert.That(EditorUtility.IsDirty(AssetDatabase.LoadAssetAtPath<Material>(materialPath)), Is.False);
        }

        [Test]
        public void UnknownSelection_IsRejectedWithoutCreatingAFallback()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => SunbreakVegetationAssets.GetParts("Tree"));
            Assert.Throws<ArgumentOutOfRangeException>(() => SunbreakVegetationAssets.GetParts(null));
        }
    }
}
