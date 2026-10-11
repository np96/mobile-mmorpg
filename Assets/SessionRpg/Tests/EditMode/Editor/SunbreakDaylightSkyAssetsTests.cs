using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using NUnit.Framework;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakDaylightSkyAssetsTests
    {
        [Test]
        public void ImmutableSkySource_PreservesOfficialOneKilopixelHdrAndCc0Evidence()
        {
            SunbreakDaylightSkyAssets.ValidateSourceIntegrity();
            using (MD5 hash = MD5.Create())
            using (FileStream stream = File.OpenRead(SunbreakDaylightSkyAssets.SourcePath))
            {
                string actual = BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                Assert.That(actual, Is.EqualTo(SunbreakDaylightSkyAssets.SourceMd5));
            }
            string manifest = File.ReadAllText(SunbreakDaylightSkyAssets.SourceFolder + "/manifest.json");
            Assert.That(manifest, Does.Contain("POLYHAVEN-2026-006"));
            Assert.That(manifest, Does.Contain("kloofendal_48d_partly_cloudy_puresky"));
            Assert.That(manifest, Does.Contain("CC0-1.0"));
            Assert.That(manifest, Does.Contain("TECH_REVIEW"));
            Assert.That(File.ReadAllText(SunbreakDaylightSkyAssets.SourceFolder + "/PROVENANCE.md"), Does.Contain("Jarod Guest"));
        }

        [Test]
        public void SkyTexture_IsLinearLatLongHdrWithBoundedDesktopAndMobileResolution()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SunbreakDaylightSkyAssets.SourcePath);
            Assert.That(importer, Is.Not.Null);
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            Assert.That(width, Is.EqualTo(1024));
            Assert.That(height, Is.EqualTo(512));
            Assert.That(importer.textureShape, Is.EqualTo(TextureImporterShape.Texture2D));
            Assert.That(importer.sRGBTexture || importer.isReadable || importer.streamingMipmaps, Is.False);
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.wrapModeU, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.wrapModeV, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.GetDefaultPlatformTextureSettings().format, Is.EqualTo(TextureImporterFormat.RGB9E5));
            Assert.That(importer.GetPlatformTextureSettings("Standalone").maxTextureSize, Is.EqualTo(1024));
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            Assert.That(android.overridden, Is.True);
            Assert.That(android.maxTextureSize, Is.EqualTo(512));
            Assert.That(android.format, Is.EqualTo(TextureImporterFormat.RGB9E5));
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(SunbreakDaylightSkyAssets.SourcePath).dimension, Is.EqualTo(TextureDimension.Tex2D));
            Assert.That(AssetDatabase.LoadAssetAtPath<Texture2D>(SunbreakDaylightSkyAssets.SourcePath).format, Is.EqualTo(TextureFormat.RGB9e5Float));
        }

        [Test]
        public void PanoramicMaterial_UsesNeutralOneExposureAndCompleteNonStereoLatitudeLongitude()
        {
            SunbreakDaylightSkyAssets.Validate();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SunbreakDaylightSkyAssets.MaterialPath);
            Assert.That(material.shader.name, Is.EqualTo("Skybox/Panoramic"));
            Assert.That(material.GetTexture("_MainTex"), Is.InstanceOf<Texture2D>());
            Assert.That(material.GetColor("_Tint"), Is.EqualTo(SunbreakDaylightSkyAssets.NeutralTint));
            Assert.That(material.GetFloat("_Exposure"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_Rotation"), Is.Zero);
            Assert.That(material.GetFloat("_Mapping"), Is.EqualTo(1f));
            Assert.That(material.GetFloat("_ImageType"), Is.Zero);
            Assert.That(material.GetFloat("_Layout"), Is.Zero);
            Assert.That(material.shaderKeywords, Is.EqualTo(new[] { SunbreakDaylightSkyAssets.LatitudeLongitudeKeyword }));
            Assert.That(material.IsKeywordEnabled("_MAPPING_6_FRAMES_LAYOUT"), Is.False);
            Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.Background));
        }

        [Test]
        public void ShaderPropertyDrawer_PreservesTheSameExplicitLatitudeLongitudeKeyword()
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SunbreakDaylightSkyAssets.MaterialPath);
            var copy = new Material(source);
            try
            {
                MaterialEditor.ApplyMaterialPropertyDrawers(copy);
                Assert.That(copy.shaderKeywords, Is.EqualTo(new[] { SunbreakDaylightSkyAssets.LatitudeLongitudeKeyword }));
                Assert.That(MaterialDrift(copy), Is.Null,
                    "Fresh-load KeywordEnum normalization must agree with authoring, not cause a false drift report.");
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        [TestCase("")]
        [TestCase("_MAPPING_6_FRAMES_LAYOUT")]
        [TestCase("_UNEXPECTED_SKY_KEYWORD")]
        public void MaterialGate_RejectsEmptyWrongOrAdditionalMappingKeywords(string invalidKeyword)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SunbreakDaylightSkyAssets.MaterialPath);
            var copy = new Material(source);
            try
            {
                copy.shaderKeywords = string.IsNullOrEmpty(invalidKeyword) ? Array.Empty<string>() :
                    new[] { SunbreakDaylightSkyAssets.LatitudeLongitudeKeyword, invalidKeyword };
                Assert.That(MaterialDrift(copy), Does.StartWith("keywords="));
            }
            finally { UnityEngine.Object.DestroyImmediate(copy); }
        }

        [Test]
        public void Validation_IsReadOnlyAndNeverBindsOrChangesGlobalLighting()
        {
            Material sky = RenderSettings.skybox;
            Color ambient = RenderSettings.ambientLight;
            float reflection = RenderSettings.reflectionIntensity;
            string metaBefore = File.ReadAllText(SunbreakDaylightSkyAssets.SourcePath + ".meta");
            string materialBefore = File.ReadAllText(SunbreakDaylightSkyAssets.MaterialPath);
            SunbreakDaylightSkyAssets.Validate();
            Assert.That(File.ReadAllText(SunbreakDaylightSkyAssets.SourcePath + ".meta"), Is.EqualTo(metaBefore));
            Assert.That(File.ReadAllText(SunbreakDaylightSkyAssets.MaterialPath), Is.EqualTo(materialBefore));
            Assert.That(RenderSettings.skybox, Is.SameAs(sky));
            Assert.That(RenderSettings.ambientLight, Is.EqualTo(ambient));
            Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(reflection));
        }

        private static string MaterialDrift(Material material)
        {
            MethodInfo method = typeof(SunbreakDaylightSkyAssets).GetMethod("MaterialSettingsDrift", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return (string)method.Invoke(null, new object[] { material });
        }
    }
}
