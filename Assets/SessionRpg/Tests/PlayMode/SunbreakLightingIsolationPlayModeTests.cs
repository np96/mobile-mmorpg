using System.Collections;
using NUnit.Framework;
using SessionRpg.Unity.Presentation;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class SunbreakLightingIsolationPlayModeTests
    {
        private GameObject _root;
        private WorldLightingPresenter _lighting;
        private Light _key;
        private Cubemap _baselineReflection;
        private DefaultReflectionMode _baselineMode;
        private float _baselineIntensity;
        private int _baselineBounces;

        [SetUp]
        public void CreateIsolatedPresenter()
        {
            _baselineReflection = RenderSettings.customReflection;
            _baselineMode = RenderSettings.defaultReflectionMode;
            _baselineIntensity = RenderSettings.reflectionIntensity;
            _baselineBounces = RenderSettings.reflectionBounces;
            _root = new GameObject("SunbreakLightingIsolation", typeof(Light), typeof(WorldLightingPresenter));
            _key = _root.GetComponent<Light>();
            _key.type = LightType.Directional;
            _lighting = _root.GetComponent<WorldLightingPresenter>();
            _lighting.ConfigureForAuthoring(_key);
        }

        [UnityTearDown]
        public IEnumerator ReleasePresenter()
        {
            if (_root != null) { Object.Destroy(_root); }
            yield return null;
            AssertReflectionRestored();
        }

        [Test]
        public void SunbreakThenTraining_RestoresLegacyLookAndReusesOneDaylightReflection()
        {
            _lighting.SetWorldLighting(true, true);
            Cubemap sunbreak = RenderSettings.customReflection;
            Assert.That(sunbreak, Is.Not.Null);
            Assert.That(sunbreak, Is.Not.SameAs(_baselineReflection));
            Assert.That(sunbreak.width, Is.EqualTo(32));
            Assert.That(sunbreak.mipmapCount, Is.GreaterThan(1));
            Assert.That(_key.intensity, Is.EqualTo(WorldLightingPresenter.SunbreakKeyIntensity));
            Assert.That(_key.shadowStrength, Is.EqualTo(WorldLightingPresenter.SunbreakShadowStrength));
            Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(WorldLightingPresenter.SunbreakReflectionIntensity));
            _lighting.SetWorldLighting(true);
            Assert.That(_key.intensity, Is.EqualTo(WorldLightingPresenter.WorldKeyIntensity));
            Assert.That(_key.shadowStrength, Is.EqualTo(WorldLightingPresenter.WorldShadowStrength));
            AssertReflectionRestored();
            _lighting.SetWorldLighting(true, true);
            Assert.That(RenderSettings.customReflection, Is.SameAs(sunbreak));
            _lighting.SetWorldLighting(false);
            AssertReflectionRestored();
            _lighting.SetWorldLighting(true, true);
            Assert.That(RenderSettings.customReflection, Is.SameAs(sunbreak),
                "Leave/re-entry and route transitions must not keep allocating reflection textures.");
        }

        [UnityTest]
        public IEnumerator DestroyingOwner_ReleasesCachedReflectionAfterRestoringEnvironment()
        {
            _lighting.SetWorldLighting(true, true);
            Cubemap sunbreak = RenderSettings.customReflection;
            Object.Destroy(_root);
            yield return null;
            yield return null;
            Assert.That(sunbreak == null, Is.True, "The presenter-owned GPU texture must not leak across scene reloads.");
            AssertReflectionRestored();
        }

        [Test]
        public void PackedMasonry_NativeShaderVariantKeepsTheCompletePassContract()
        {
            new SunbreakTerrainShaderPlayModeTests().InitializeNativeRenderPipeline();
            Shader shader = Shader.Find("SessionRpg/Sunbreak Terrain");
            Assert.That(shader.isSupported, Is.True);
            var material = new Material(shader);
            try
            {
                material.EnableKeyword("_PACKED_MASONRY");
                material.SetFloat("_PackedMasonry", 1f);
                foreach (string pass in new[] { "ForwardLit", "DepthOnly", "DepthNormals", "ShadowCaster" })
                {
                    Assert.That(material.FindPass(pass), Is.GreaterThanOrEqualTo(0));
                }
                Assert.That(material.IsKeywordEnabled("_PACKED_MASONRY"), Is.True);
                Assert.That(material.IsKeywordEnabled("_SECOND_LAYER"), Is.False);
            }
            finally { Object.DestroyImmediate(material); }
        }

        private void AssertReflectionRestored()
        {
            Assert.That(RenderSettings.defaultReflectionMode, Is.EqualTo(_baselineMode));
            Assert.That(RenderSettings.customReflection, Is.SameAs(_baselineReflection));
            Assert.That(RenderSettings.reflectionIntensity, Is.EqualTo(_baselineIntensity));
            Assert.That(RenderSettings.reflectionBounces, Is.EqualTo(_baselineBounces));
        }
    }
}
