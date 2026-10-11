using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakMasonryMaterialTests
    {
        private const string Source = "Assets/ThirdParty/PolyHaven/StoneBrickWall001/stone_brick_wall_001_";
        private const string Mask = "Assets/SessionRpg/Art/Environment/Textures/FortressMasonryMask.png";

        [TestCase("Limestone")]
        [TestCase("Paving")]
        public void Masonry_UsesMeasuredStoneAndExistingLinearPackedMask(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/Sunbreak_" + name + ".mat");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("SessionRpg/Sunbreak Terrain"));
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")),
                Is.EqualTo(Source + "diff_2k.png"));
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")),
                Is.EqualTo(Source + "nor_gl_2k.png"));
            Assert.That(AssetDatabase.GetAssetPath(material.GetTexture("_PackedMasonryMap")), Is.EqualTo(Mask));
            Assert.That(material.GetTexture("_RoughnessMap"), Is.Null,
                "The packed variant replaces the roughness sample, not an extra fourth surface texture.");
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(Vector2.one),
                "Mesh UVs at 0.4 repeats/m preserve the measured 2.5 m masonry scan.");
            Assert.That(material.GetFloat("_PackedMasonry"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_OcclusionStrength"), Is.EqualTo(0.85f));
            Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(0.92f));
            Assert.That(material.IsKeywordEnabled("_PACKED_MASONRY"), Is.True);
            Assert.That(material.IsKeywordEnabled("_SECOND_LAYER"), Is.False);
            Assert.That(material.GetFloat("_Surface"), Is.Zero);
            Assert.That(material.GetFloat("_AlphaClip"), Is.Zero);
            Assert.That(material.IsKeywordEnabled("_EMISSION"), Is.False);

            var maskImporter = (TextureImporter)AssetImporter.GetAtPath(Mask);
            Assert.That(maskImporter.sRGBTexture, Is.False);
            Assert.That(maskImporter.isReadable, Is.False);
            Assert.That(maskImporter.mipmapEnabled, Is.True);
            Assert.That(maskImporter.GetPlatformTextureSettings("Android").maxTextureSize, Is.EqualTo(1024));
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(Source + "diff_2k.png")).sRGBTexture, Is.True);
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(Source + "nor_gl_2k.png")).textureType,
                Is.EqualTo(TextureImporterType.NormalMap));
        }

        [Test]
        public void PackedMasonry_ReusesTheVerifiedDerivativeWithoutInventingAoOrDisplacement()
        {
            using (SHA256 digest = SHA256.Create())
            using (FileStream stream = File.OpenRead(Mask))
            {
                string hash = System.BitConverter.ToString(digest.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
                Assert.That(hash, Is.EqualTo("fddbbf69b2d67b54ef381e8af01bfe3665bbccc560caec4e3487de4b0b246ca2"));
            }
            string shader = File.ReadAllText("Assets/SessionRpg/Shaders/SunbreakTerrain.shader");
            StringAssert.Contains("roughness = 1.0h - masonry.a;", shader);
            StringAssert.Contains("occlusion = lerp(1.0h, masonry.g, _OcclusionStrength);", shader);
            StringAssert.Contains("surface.occlusion = occlusion;", shader);
            StringAssert.DoesNotContain("masonry.b", shader);
        }

        [TestCase("Meadow")]
        [TestCase("Rock")]
        [TestCase("Bark")]
        public void NaturalSurfaces_DoNotPayForOrReceiveMasonryOcclusion(string name)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/Sunbreak_" + name + ".mat");
            Assert.That(material.IsKeywordEnabled("_PACKED_MASONRY"), Is.False);
            Assert.That(material.GetTexture("_PackedMasonryMap"), Is.Null);
            Assert.That(material.GetFloat("_OcclusionStrength"), Is.Zero);
        }
    }
}
