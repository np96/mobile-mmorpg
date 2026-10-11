using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class CombatVfxAssetContractTests
    {
        private const string WispPath =
            "Assets/SessionRpg/Art/Vfx/SpellParticleWisp.png";

        [Test]
        public void SpellParticleWisp_UsesLinearMipmappedClampImport()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(WispPath);
            TextureImporter importer = AssetImporter.GetAtPath(WispPath)
                as TextureImporter;

            Assert.That(texture, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(512));
            Assert.That(texture.height, Is.EqualTo(512));
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.sRGBTexture, Is.False);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.mipMapBias, Is.EqualTo(-0.25f).Within(0.001f));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(
                importer.textureCompression,
                Is.EqualTo(TextureImporterCompression.Uncompressed));

            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            Assert.That(android.overridden, Is.True);
            Assert.That(android.maxTextureSize, Is.EqualTo(512));
            Assert.That(android.format, Is.EqualTo(TextureImporterFormat.ASTC_6x6));
        }

        [Test]
        public void CombatParticleMaterial_UsesAdditiveParticleShaderAndWisp()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/CombatParticle.mat");
            Texture2D wisp = AssetDatabase.LoadAssetAtPath<Texture2D>(WispPath);

            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Does.Contain("Particles/Unlit"));
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(wisp));
            Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(0f));
            Assert.That(
                material.GetFloat("_DstBlend"),
                Is.EqualTo((float)UnityEngine.Rendering.BlendMode.One));
        }

        [Test]
        public void CombatFragmentMaterial_WritesDepthAsOpaqueLitGeometry()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/CombatFragment.mat");

            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo("Universal Render Pipeline/Lit"));
            Assert.That(material.GetFloat("_Surface"), Is.EqualTo(0f));
            Assert.That(material.GetFloat("_ZWrite"), Is.EqualTo(1f));
            Assert.That(
                material.GetFloat("_Cull"),
                Is.EqualTo((float)UnityEngine.Rendering.CullMode.Back));
        }
    }
}
