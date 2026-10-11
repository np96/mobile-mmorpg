using System;
using System.IO;
using NUnit.Framework;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakVegetationCoverageTests
    {
        private const string ShaderPath = "Assets/SessionRpg/Shaders/SunbreakVegetation.shader";

        [TestCase("TreeLeaf")]
        [TestCase("Grass")]
        [TestCase("Fern")]
        public void CutoutMaterials_DeclareOpaqueSurfaceAndFixedAlphaClip(string role)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(SunbreakVegetationAssets.MaterialPath(role));
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader.name, Is.EqualTo(SunbreakVegetationAssets.VegetationShader));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            Assert.That(material.HasProperty("_Surface") && material.HasProperty("_AlphaClip"), Is.True);
            Assert.That(material.GetFloat("_Surface"), Is.Zero);
            Assert.That(material.GetFloat("_AlphaClip"), Is.EqualTo(1f));
            Assert.That(material.renderQueue, Is.EqualTo((int)RenderQueue.AlphaTest));
            Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.4f));
        }

        [TestCase("ForwardLit")]
        [TestCase("DepthOnly")]
        [TestCase("DepthNormals")]
        public void CameraPasses_ExportSharpenedCoverageToMsaaSampleMask(string pass)
        {
            string source = File.ReadAllText(ShaderPath);
            string body = Pass(source, pass);
            Assert.That(body, Does.Contain("AlphaToMask On"));
            Assert.That(body, Does.Contain("half coverage = ClipVegetation(input.uv)"));
            Assert.That(body, Does.Contain("OutputAlpha(coverage, false)"));
            Assert.That(body, Does.Contain("ZWrite On"));
            Assert.That(source.IndexOf("#define _ALPHATEST_ON 1", StringComparison.Ordinal),
                Is.LessThan(source.IndexOf("ShaderLibrary/Core.hlsl", StringComparison.Ordinal)));
            Assert.That(source, Does.Contain("AlphaClip(VegetationCoverage(uv), _Cutoff)"));
        }

        [Test]
        public void Shadows_UseHardMeasuredCutoutWithoutCameraSampleCoverage()
        {
            string source = File.ReadAllText(ShaderPath);
            string shadow = Pass(source, "ShadowCaster");
            Assert.That(shadow, Does.Not.Contain("AlphaToMask"));
            Assert.That(shadow, Does.Contain("ClipVegetationShadow(input.uv)"));
            Assert.That(source, Does.Contain("half coverage = VegetationCoverage(uv)"));
            Assert.That(source, Does.Contain("clip(coverage - _Cutoff)"));
        }

        [Test]
        public void InstalledUrpAlphaClip_DefinesTheOneSampleHardCutoffFallback()
        {
            // Source-contract evidence, not a GPU or physical-device claim.
            string path = "Packages/com.unity.render-pipelines.universal/ShaderLibrary/ShaderVariablesFunctions.hlsl";
            string resolved = Path.GetFullPath(path);
            if (!File.Exists(resolved))
            {
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(path);
                Assert.That(package, Is.Not.Null);
                resolved = Path.Combine(package.resolvedPath, "ShaderLibrary/ShaderVariablesFunctions.hlsl");
            }
            string urp = File.ReadAllText(resolved);
            Assert.That(urp, Does.Contain("bool a2c = IsAlphaToMaskAvailable()"));
            Assert.That(urp, Does.Contain(": (alpha - cutoff)"));
            Assert.That(urp, Does.Contain("clip(clipVal)"));
            Assert.That(urp, Does.Contain("return IsAlphaToMaskAvailable() ? alpha : 1.0"));
        }

        private static string Pass(string source, string name)
        {
            int start = source.IndexOf("Name \"" + name + "\"", StringComparison.Ordinal);
            Assert.That(start, Is.GreaterThanOrEqualTo(0));
            int end = source.IndexOf("\n        Pass", start, StringComparison.Ordinal);
            return end < 0 ? source.Substring(start) : source.Substring(start, end - start);
        }
    }
}
