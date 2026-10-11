using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakLandscapeSurfaceTests
    {
        [Test]
        public void LandscapeUsesExistingMeasuredMapsAtPhysicalScaleWithoutNewTextures()
        {
            Material terrain = AssetDatabase.LoadAssetAtPath<Material>(SunbreakLandscapeSurfaceAuthoring.MaterialPath);
            Material rock = Load("Rock"), meadow = Load("Meadow");
            Assert.That(terrain, Is.Not.Null);
            Assert.That(terrain.shader, Is.SameAs(rock.shader));
            Assert.That(ShaderUtil.ShaderHasError(terrain.shader), Is.False);
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_RoughnessMap" })
            { Assert.That(terrain.GetTexture(property), Is.SameAs(rock.GetTexture(property))); }
            Assert.That(terrain.GetTexture("_LayerMap"), Is.SameAs(meadow.GetTexture("_BaseMap")));
            Assert.That(terrain.GetTexture("_LayerNormal"), Is.SameAs(meadow.GetTexture("_BumpMap")));
            Assert.That(terrain.GetTexture("_LayerRoughness"), Is.SameAs(meadow.GetTexture("_RoughnessMap")));
            Assert.That(terrain.GetTextureScale("_BaseMap"), Is.EqualTo(Vector2.one / 1.5f));
            Assert.That(terrain.GetTextureScale("_LayerMap"), Is.EqualTo(Vector2.one / 2f));
            Assert.That(terrain.GetTexture("_BlendMap"), Is.Null);
            Assert.That(terrain.GetTexture("_PackedMasonryMap"), Is.Null);
            Assert.That(terrain.GetFloat("_OcclusionStrength"), Is.Zero);
            Assert.That(terrain.GetFloat("_Surface"), Is.Zero);
            Assert.That(terrain.GetFloat("_AlphaClip"), Is.Zero);
            Assert.That(terrain.IsKeywordEnabled("_SECOND_LAYER"), Is.True);
            Assert.That(terrain.IsKeywordEnabled("_VERTEX_LAYER"), Is.True);
        }

        [Test]
        public void AuthoredCoverageIsDeterministicAndRecedesOnHighOrSteepExposures()
        {
            Random.State random = Random.state;
            float minimum = 1f, maximum = 0f;
            for (int z = -300; z <= 350; z += 31)
            for (int x = -300; x <= 350; x += 29)
            {
                var point = new Vector3(x, 8f, z);
                Color low = SunbreakLandscapeSurfaceAuthoring.VertexColor(point, Vector3.up);
                Assert.That(low, Is.EqualTo(SunbreakLandscapeSurfaceAuthoring.VertexColor(point, Vector3.up)));
                Assert.That(low.r, Is.InRange(0.07f, 0.98f));
                Assert.That(low.g, Is.InRange(0.62f, 0.95f));
                Assert.That(low.a, Is.EqualTo(1f));
                Assert.That(SunbreakLandscapeSurfaceAuthoring.VertexColor(new Vector3(x, 55f, z), Vector3.up).r, Is.Zero);
                Assert.That(SunbreakLandscapeSurfaceAuthoring.VertexColor(point, Vector3.right).r, Is.Zero);
                minimum = Mathf.Min(minimum, low.r); maximum = Mathf.Max(maximum, low.r);
            }
            Assert.That(maximum - minimum, Is.GreaterThan(0.65f),
                "Distant rock and grass must form distinct exposures, not the rejected monochrome green slopes.");
            Assert.That(Random.state, Is.EqualTo(random));
        }

        [TestCase("Meadow")]
        [TestCase("Rock")]
        [TestCase("Bark")]
        [TestCase("Paving")]
        [TestCase("Limestone")]
        public void NearMaterialsNeverUseDistantVertexCoverage(string name)
        {
            Material material = Load(name);
            Assert.That(material.GetFloat("_VertexLayer"), Is.Zero);
            Assert.That(material.IsKeywordEnabled("_VERTEX_LAYER"), Is.False);
        }

        [Test]
        public void ColorAndDepthNormalPassesUseTheSameLayerCoverage()
        {
            string shader = File.ReadAllText("Assets/SessionRpg/Shaders/SunbreakTerrain.shader");
            Assert.That(Regex.Matches(shader, @"#pragma shader_feature_local _VERTEX_LAYER\b").Count, Is.EqualTo(2));
            StringAssert.Contains("half2 mask = saturate(input.layerMask)", shader);
            StringAssert.Contains("output.layerMask = input.color.rg", shader);
            StringAssert.Contains("input.positionWS.xz + 64.0", shader,
                "Near ground must retain its existing world-aligned measured mask path.");
            StringAssert.Contains("ReadTerrain(input, surface, worldNormal);", shader);
            StringAssert.Contains("data.normalWS = worldNormal;", shader);
            StringAssert.Contains("ReadTerrain(input, surface, normal);", shader);
            StringAssert.DoesNotContain("half3 normal = TerrainNormal(input, surface.normalTS);", shader,
                "DepthNormals must not reinterpret Vista's blended world normal in an arbitrary mesh tangent frame.");
        }

        [Test]
        public void VistaTriplanar_HasContinuousSignedPhysicalProjectionAndAnExplicitEighteenFetchBudget()
        {
            string shader = Regex.Replace(File.ReadAllText("Assets/SessionRpg/Shaders/SunbreakTerrain.shader"),
                @"//[^\r\n]*|/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            int sampleStart = shader.IndexOf("void SampleLandscapeLayer", System.StringComparison.Ordinal);
            int sampleEnd = shader.IndexOf("void ReadLandscape", sampleStart, System.StringComparison.Ordinal);
            string sampling = shader.Substring(sampleStart, sampleEnd - sampleStart);
            int landscapeEnd = shader.IndexOf("void ReadTerrain", sampleEnd, System.StringComparison.Ordinal);
            string landscape = shader.Substring(sampleEnd, landscapeEnd - sampleEnd);
            Assert.That(Regex.Matches(sampling, @"\bSAMPLE_TEXTURE2D\s*\(").Count, Is.EqualTo(9));
            Assert.That(Regex.Matches(landscape, @"\bSampleLandscapeLayer\s*\(").Count, Is.EqualTo(2),
                "Vista deliberately costs two measured layers times three planes times three maps = 18 fetches.");
            StringAssert.Contains("float2(-positionWS.z * signs.x, positionWS.y)", sampling);
            StringAssert.Contains("float2(positionWS.x, -positionWS.z * signs.y)", sampling);
            StringAssert.Contains("float2(positionWS.x * signs.z, positionWS.y)", sampling);
            StringAssert.DoesNotContain("input.uv", landscape);
            StringAssert.DoesNotContain("_BlendMap", landscape);
            StringAssert.DoesNotContain("_PackedMasonryMap", landscape);
            StringAssert.Contains("#if defined(_VERTEX_LAYER)\n            half3 LandscapeWeights", shader);
            StringAssert.Contains("tangentX.xy + normalX.xy, tangentX.z * normalX.z", shader);
            StringAssert.Contains("tangentY.xy + normalY.xy, tangentY.z * normalY.z", shader);
            StringAssert.Contains("tangentZ.xy + normalZ.xy, tangentZ.z * normalZ.z", shader);
            StringAssert.Contains("half3(blendedX.z * signs.x, blendedX.y, -blendedX.x * signs.x)", shader);
            StringAssert.Contains("half3(blendedY.x, blendedY.z * signs.y, -blendedY.y * signs.y)", shader);
            StringAssert.Contains("half3(blendedZ.x * signs.z, blendedZ.y, blendedZ.z * signs.z)", shader);
        }

        [TestCase(1f, 0f, 0f)]
        [TestCase(-1f, 0f, 0f)]
        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f)]
        [TestCase(0f, 0f, 1f)]
        [TestCase(0f, 0f, -1f)]
        [TestCase(0.3f, 0.7f, 0.6f)]
        [TestCase(-0.6f, 0.2f, 0.7f)]
        [TestCase(0.8f, -0.4f, -0.3f)]
        [TestCase(-0.3f, -0.8f, -0.4f)]
        public void NeutralTriplanarMaps_PreserveTheGeometricNormalInEverySignedOctant(float x, float y, float z)
        {
            Vector3 geometric = new Vector3(x, y, z).normalized;
            Vector3 result = WhiteoutReference(geometric, Vector3.forward, Vector3.forward, Vector3.forward);
            Assert.That(Vector3.Distance(result, geometric), Is.LessThan(0.000001f),
                "Neutral measured normals must not turn the geometric surface into a weighted projection-axis normal.");
        }

        [Test]
        public void TriplanarWhiteout_RemainsContinuousAcrossTheFormerDominantAxisBoundary()
        {
            Vector3 normalX = new Vector3(0.4f, 0.2f, 0.89f).normalized;
            Vector3 normalY = new Vector3(-0.1f, 0.3f, 0.94f).normalized;
            Vector3 normalZ = new Vector3(0.2f, -0.4f, 0.89f).normalized;
            foreach (float sign in new[] { -1f, 1f })
            {
                Vector3 first = new Vector3(0.701f * sign, 0.699f, 0.1f).normalized;
                Vector3 second = new Vector3(0.699f * sign, 0.701f, 0.1f).normalized;
                Assert.That(Vector3.Distance(WhiteoutReference(first, normalX, normalY, normalZ),
                    WhiteoutReference(second, normalX, normalY, normalZ)), Is.LessThan(0.006f),
                    "Crossing the former per-triangle dominant-axis boundary must not switch normal-map basis abruptly.");
            }
        }

        // Independent CPU reference for the shader's signed right-handed
        // whiteout equations. Shader-source assertions above bind the same
        // projection and reconstruction terms; native rendering is a separate gate.
        private static Vector3 WhiteoutReference(Vector3 geometric, Vector3 x, Vector3 y, Vector3 z)
        {
            Vector3 signs = new Vector3(geometric.x < 0f ? -1f : 1f,
                geometric.y < 0f ? -1f : 1f, geometric.z < 0f ? -1f : 1f);
            Vector3 weights = new Vector3(Mathf.Pow(Mathf.Abs(geometric.x), 4f),
                Mathf.Pow(Mathf.Abs(geometric.y), 4f), Mathf.Pow(Mathf.Abs(geometric.z), 4f));
            weights /= weights.x + weights.y + weights.z;
            Vector3 tangentX = new Vector3(-geometric.z * signs.x, geometric.y, Mathf.Abs(geometric.x));
            Vector3 tangentY = new Vector3(geometric.x, -geometric.z * signs.y, Mathf.Abs(geometric.y));
            Vector3 tangentZ = new Vector3(geometric.x * signs.z, geometric.y, Mathf.Abs(geometric.z));
            Vector3 bx = new Vector3(tangentX.x + x.x, tangentX.y + x.y, tangentX.z * x.z);
            Vector3 by = new Vector3(tangentY.x + y.x, tangentY.y + y.y, tangentY.z * y.z);
            Vector3 bz = new Vector3(tangentZ.x + z.x, tangentZ.y + z.y, tangentZ.z * z.z);
            return (new Vector3(bx.z * signs.x, bx.y, -bx.x * signs.x) * weights.x +
                new Vector3(by.x, by.z * signs.y, -by.y * signs.y) * weights.y +
                new Vector3(bz.x * signs.z, bz.y, bz.z * signs.z) * weights.z).normalized;
        }

        private static Material Load(string name)
        {
            Material result = AssetDatabase.LoadAssetAtPath<Material>("Assets/SessionRpg/Art/Materials/Sunbreak_" + name + ".mat");
            Assert.That(result, Is.Not.Null);
            return result;
        }
    }
}
