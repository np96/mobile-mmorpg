using System;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakGroundEcotoneTests
    {
        [Test]
        public void Ground_MeasuredGrassAndEarthHaveIndependentModerateTints()
        {
            Material ground = Material("Meadow");
            Color grass = ground.GetColor("_BaseColor");
            Color earth = ground.GetColor("_LayerColor");
            // Material serialization/color conversion may change a float by one
            // ULP. Verify each linear component without requiring bit identity.
            AssertTint(grass, new Color(0.62f, 0.70f, 0.59f));
            AssertTint(earth, new Color(0.68f, 0.68f, 0.66f));
            Assert.That(grass.g, Is.GreaterThan(grass.r));
            Assert.That(earth.r, Is.EqualTo(earth.g).Within(0.00001f),
                "Bare earth must not inherit the green meadow multiplier.");
            Assert.That(ground.GetFloat("_LayerSaturation"), Is.EqualTo(0.28f).Within(0.00001f),
                "The orange source is made neutral by local chroma reduction, not by reducing exposure.");
            Assert.That(ground.GetFloat("_BumpScale"), Is.EqualTo(1f),
                "Use the retained scan's real normal strength, not fabricated relief from color.");
            Assert.That(ground.GetTextureScale("_BaseMap"), Is.EqualTo(Vector2.one * 96f));
            Assert.That(ground.GetTextureScale("_LayerMap"), Is.EqualTo(Vector2.one * (192f / 2.07f)));
            Assert.That(ground.GetFloat("_OcclusionStrength"), Is.Zero);
            Assert.That(ground.IsKeywordEnabled("_PACKED_MASONRY"), Is.False);
        }

        [TestCase("Rock")]
        [TestCase("Bark")]
        [TestCase("Limestone")]
        [TestCase("Paving")]
        public void SolidMaterials_DoNotEnableOrTintWithTheGroundLayer(string name)
        {
            Material material = Material(name);
            Assert.That(material.GetFloat("_SecondLayer"), Is.Zero);
            Assert.That(material.IsKeywordEnabled("_SECOND_LAYER"), Is.False);
            Assert.That(material.GetColor("_LayerColor"), Is.EqualTo(Color.white));
            Assert.That(material.GetFloat("_LayerSaturation"), Is.EqualTo(1f));
            Assert.That(material.GetTexture("_LayerMap"), Is.Null);
            Assert.That(material.GetTexture("_LayerNormal"), Is.Null);
            Assert.That(material.GetTexture("_LayerRoughness"), Is.Null);
            Assert.That(material.GetTexture("_BlendMap"), Is.Null);
        }

        [Test]
        public void GroundBlend_RetainsDistinctGrassAndEarthPatchesRatherThanAMostlyMixedLawn()
        {
            Func<float, float, Vector2> sample = Sampler();
            int grass = 0;
            int earth = 0;
            int count = 0;
            for (int z = -56; z <= 120; z += 4)
            {
                for (int x = -56; x <= 120; x += 4)
                {
                    Vector2 mask = sample(x, z);
                    if (mask.x < 0.15f) { grass++; }
                    if (mask.x > 0.85f) { earth++; }
                    count++;
                }
            }
            Assert.That(grass, Is.GreaterThan(count / 10), "Keep readable areas of the grass scan.");
            Assert.That(earth, Is.GreaterThan(count / 30),
                "The old 0.65 clearing/0.72 scree caps never exposed the earth scan cleanly.");
        }

        [Test]
        public void EveryCamp_HasExposedEarthAndIrregularRatherThanRadiallyUniformMargins()
        {
            Func<float, float, Vector2> sample = Sampler();
            float combinedAngularVariation = 0f;
            for (int camp = -1; camp < PveRegionLayout.CampCount; camp++)
            {
                Float3 center = camp < 0 ? PveRegionLayout.Refuge : PveRegionLayout.GetCampCenter(camp);
                Assert.That(sample(center.X, center.Z).x, Is.GreaterThan(0.85f), "Clearing " + camp);
                float minimum = 1f;
                float maximum = 0f;
                for (int angle = 0; angle < 16; angle++)
                {
                    float radians = angle * Mathf.PI / 8f;
                    float soil = sample(center.X + Mathf.Cos(radians) * 9f,
                        center.Z + Mathf.Sin(radians) * 9f).x;
                    minimum = Mathf.Min(minimum, soil);
                    maximum = Mathf.Max(maximum, soil);
                }
                combinedAngularVariation += maximum - minimum;
            }
            Assert.That(combinedAngularVariation, Is.GreaterThan(1.2f),
                "Camp wear should meet vegetation through broken edges, not eight soft circles.");
        }

        [Test]
        public void GroundShader_UsesExistingMeasuredSamplesWithoutAnExtraTextureOrInventedAo()
        {
            string shader = Regex.Replace(File.ReadAllText("Assets/SessionRpg/Shaders/SunbreakTerrain.shader"),
                @"//[^\r\n]*|/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            int start = shader.IndexOf("void ReadTerrain", StringComparison.Ordinal);
            int end = shader.IndexOf("ENDHLSL", start, StringComparison.Ordinal);
            string surface = shader.Substring(start, end - start);
            StringAssert.Contains("#if defined(_VERTEX_LAYER)", surface);
            StringAssert.Contains("ReadLandscape(input, surface, normalWS);", surface);
            int layerStart = surface.IndexOf("#if defined(_SECOND_LAYER)", StringComparison.Ordinal);
            // ReadTerrain keeps all eight existing near-path source callsites;
            // the separately compiled Vista helper owns its explicit 18 reads.
            int layerEnd = surface.IndexOf("surface.albedo =", layerStart, StringComparison.Ordinal);
            string layer = surface.Substring(layerStart, layerEnd - layerStart);
            Assert.That(Regex.Matches(layer, @"\bSAMPLE_TEXTURE2D\s*\(").Count, Is.EqualTo(4),
                "Ground adds its existing three measured layer maps and one world mask, not extra samples.");
            Assert.That(Regex.Matches(surface, @"\bSAMPLE_TEXTURE2D\s*\(").Count, Is.EqualTo(8),
                "Near path only: three primary reads, four ground reads, and the mutually exclusive masonry-mask alternative.");
            StringAssert.Contains("* _BaseColor.rgb", surface);
            StringAssert.Contains("* _LayerColor.rgb", layer);
            StringAssert.Contains("dot(earth, half3(0.2126h, 0.7152h, 0.0722h))", layer);
            StringAssert.Contains("earth = lerp(earthLuminance.xxx, earth, saturate(_LayerSaturation));", layer);
            StringAssert.Contains("smoothstep(0.28h, 0.72h", layer);
            StringAssert.Contains("(soilRough - roughness) * 0.12h", layer);
            StringAssert.Contains("(input.positionWS.xz + 64.0) / 192.0", layer);
            StringAssert.DoesNotContain("occlusion =", layer);
            StringAssert.DoesNotContain("surface.smoothness =", layer);
            StringAssert.Contains("UnpackNormalScale", layer);
            StringAssert.Contains("surface.albedo = albedo;", surface);
            StringAssert.Contains("surface.smoothness = saturate(1.0h - roughness) * _Smoothness;", surface);
        }

        private static Material Material(string name)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/Sunbreak_" + name + ".mat");
            Assert.That(material, Is.Not.Null, name);
            return material;
        }

        private static void AssertTint(Color actual, Color expected)
        {
            for (int channel = 0; channel < 4; channel++)
            {
                Assert.That(actual[channel], Is.EqualTo(expected[channel]).Within(0.000001f));
            }
        }

        private static Func<float, float, Vector2> Sampler()
        {
            Type authoring = typeof(SessionRpgAuthoringGenerator).Assembly.GetType(
                "SessionRpg.Editor.Authoring.SunbreakScannedSurfaceAuthoring");
            Assert.That(authoring, Is.Not.Null);
            MethodInfo method = authoring.GetMethod("SampleGroundBlend", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            return (Func<float, float, Vector2>)Delegate.CreateDelegate(typeof(Func<float, float, Vector2>), method);
        }
    }
}
