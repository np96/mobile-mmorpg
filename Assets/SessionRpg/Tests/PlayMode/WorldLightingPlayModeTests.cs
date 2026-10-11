using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.Presentation;
using SessionRpg.Unity.World;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class WorldLightingPlayModeTests
    {
        private PrototypeGameController _controller;
        private WorldLightingPresenter _lighting;
        private Light _key;
        private Quaternion _baselineRotation;
        private Color _baselineKeyColor;
        private float _baselineKeyIntensity;
        private float _baselineShadowStrength;
        private Color _baselineSky;
        private Color _baselineEquator;
        private Color _baselineGround;
        private Color _baselineFogColor;
        private float _baselineFogDensity;
        private Material _baselineSkybox;
        private SphericalHarmonicsL2 _baselineProbe;
        private string _saveDirectory;

        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
            _controller = Object.FindAnyObjectByType<PrototypeGameController>();
            _lighting = Object.FindAnyObjectByType<WorldLightingPresenter>();
            Assert.That(_lighting, Is.Not.Null);
            Assert.That(_lighting.HasAuthoredBindings, Is.True);
            Assert.That(_lighting.HasSunbreakVistaBindings, Is.True);
            _controller.enabled = false;
            _saveDirectory = Path.Combine(Path.GetTempPath(),
                "session-rpg-lighting-test-" + Guid.NewGuid().ToString("N"));
            SetField(_controller, "_grindingSaveDirectory", _saveDirectory);
            _key = _lighting.KeyLight;
            _baselineRotation = _key.transform.rotation;
            _baselineKeyColor = _key.color;
            _baselineKeyIntensity = _key.intensity;
            _baselineShadowStrength = _key.shadowStrength;
            _baselineSky = RenderSettings.ambientSkyColor;
            _baselineEquator = RenderSettings.ambientEquatorColor;
            _baselineGround = RenderSettings.ambientGroundColor;
            _baselineFogColor = RenderSettings.fogColor;
            _baselineFogDensity = RenderSettings.fogDensity;
            _baselineSkybox = RenderSettings.skybox;
            _baselineProbe = RenderSettings.ambientProbe;
        }

        [UnityTearDown]
        public IEnumerator RestoreBootstrap()
        {
            _controller?.ReturnHome();
            _lighting?.Restore();
            yield return null;
            if (Directory.Exists(_saveDirectory))
            {
                Directory.Delete(_saveDirectory, true);
            }
        }

        [Test]
        public void PveEntry_UsesPhysicalDaylightWithoutAnotherLightOrProfileChange()
        {
            Light[] lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include);
            Volume volume = Object.FindAnyObjectByType<Volume>();
            VolumeProfile profile = volume.sharedProfile;
            bool hadInstantiatedProfile = volume.HasInstantiatedProfile();
            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);

            Assert.That(_lighting.IsWorldLightingEnabled, Is.True);
            Assert.That(RenderSettings.sun, Is.SameAs(_key));
            Assert.That(_key.intensity, Is.EqualTo(WorldLightingPresenter.SunbreakKeyIntensity));
            Assert.That(_key.shadowStrength, Is.EqualTo(WorldLightingPresenter.SunbreakShadowStrength));
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(WorldLightingPresenter.WorldSkyColor));
            Assert.That(RenderSettings.ambientEquatorColor, Is.EqualTo(WorldLightingPresenter.WorldEquatorColor));
            Assert.That(RenderSettings.ambientGroundColor, Is.EqualTo(WorldLightingPresenter.WorldGroundColor));
            Assert.That(RenderSettings.ambientEquatorColor.grayscale,
                Is.GreaterThan(_baselineEquator.grayscale * 2f));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(WorldLightingPresenter.SunbreakFogDensity));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(WorldLightingPresenter.SunbreakFogColor));
            AssertSunbreakVistaEnabled();
            Assert.That(Object.FindObjectsByType<Light>(FindObjectsInactive.Include),
                Is.EquivalentTo(lights));
            Assert.That(volume.sharedProfile, Is.SameAs(profile));
            Assert.That(volume.HasInstantiatedProfile(), Is.EqualTo(hadInstantiatedProfile),
                "Daylight must not clone or mutate the global exposure/quality profile.");
        }

        [Test]
        public void SunbreakFog_RetainsDistantReliefInsteadOfFlatteningTheSkyline()
        {
            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
            Assert.That(RenderSettings.fogMode, Is.EqualTo(FogMode.ExponentialSquared));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.0035f));
            float density = RenderSettings.fogDensity;
            Assert.That(Mathf.Exp(-density * density * 200f * 200f), Is.GreaterThan(0.5f));
            Assert.That(Mathf.Exp(-density * density * 350f * 350f), Is.GreaterThan(0.2f),
                "At the distant ridge, some surface contrast must survive the authored Exp2 atmosphere.");
        }

        [UnityTest]
        public IEnumerator AdditiveWorldLoading_KeepsTheWorldAmbientFill()
        {
            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
            WorldZoneStreamController streamer = Object.FindAnyObjectByType<WorldZoneStreamController>();
            float deadline = Time.realtimeSinceStartup + 15f;
            while (!streamer.IsInitialWindowReady && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }

            Assert.That(streamer.IsInitialWindowReady, Is.True);
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Bootstrap"));
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(WorldLightingPresenter.WorldSkyColor));
            Assert.That(RenderSettings.ambientProbe[0, 0], Is.GreaterThan(0f));
            Assert.That(RenderSettings.ambientProbe[1, 0], Is.GreaterThan(0f));
            Assert.That(RenderSettings.ambientProbe[2, 0], Is.GreaterThan(0f));
            Assert.That(_key.intensity, Is.EqualTo(WorldLightingPresenter.SunbreakKeyIntensity));
            AssertSunbreakVistaEnabled();
            AssertReadableDirectionalFill();
        }

        [Test]
        public void TrainingWorld_RestoresItsOwnSkyAndNeverKeepsThePveVista()
        {
            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
            AssertSunbreakVistaEnabled();
            _controller.StartLocalSession(GameModeId.Leveling, ClassId.Mage, SpecializationId.Fire);
            Assert.That(_lighting.IsWorldLightingEnabled, Is.True);
            Assert.That(_lighting.SunbreakVista.activeSelf, Is.False);
            Assert.That(RenderSettings.skybox, Is.SameAs(_baselineSkybox));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(WorldLightingPresenter.WorldFogDensity));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(WorldLightingPresenter.WorldFogColor));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void EverySunbreakExit_RestoresTheExactNullOrMaterialBaselineSky(bool hasBaselineSky)
        {
            Material originalSky = RenderSettings.skybox;
            Material baseline = hasBaselineSky
                ? new Material(Shader.Find("Skybox/Procedural")) { name = "TestOriginalSky" }
                : null;
            try
            {
                RenderSettings.skybox = baseline;
                _lighting.SetWorldLighting(true, true);
                AssertSunbreakVistaEnabled();
                _lighting.SetWorldLighting(true);
                Assert.That(_lighting.IsWorldLightingEnabled, Is.True);
                AssertVistaAndSkyRestored(baseline);
                _lighting.SetWorldLighting(true, true);
                AssertSunbreakVistaEnabled();
                _lighting.Restore();
                AssertVistaAndSkyRestored(baseline);
                _lighting.Restore();
                AssertVistaAndSkyRestored(baseline);

                _lighting.SetWorldLighting(true, true);
                AssertSunbreakVistaEnabled();
                _lighting.enabled = false;
                AssertVistaAndSkyRestored(baseline);
                _lighting.enabled = true;
                _lighting.SetWorldLighting(true, true);
                AssertSunbreakVistaEnabled();
                _lighting.SetWorldLighting(false);
                AssertVistaAndSkyRestored(baseline);
            }
            finally
            {
                _lighting.Restore();
                _lighting.enabled = true;
                RenderSettings.skybox = originalSky;
                if (baseline != null) { Object.DestroyImmediate(baseline); }
            }
        }

        [Test]
        public void RestoreWithoutActiveWorld_StillHidesTheResidentVista()
        {
            Assert.That(_lighting.IsWorldLightingEnabled, Is.False);
            _lighting.SunbreakVista.SetActive(true);
            _lighting.Restore();
            AssertVistaAndSkyRestored(_baselineSkybox);
        }

        [UnityTest]
        public IEnumerator WarmedFramesAndRepeatedModeEntries_ReuseTheSharedSkyWithoutMaterialClones()
        {
            _lighting.SetWorldLighting(true, true);
            Material sky = GetField<Material>(_lighting, "_sunbreakSky");
            Cubemap reflection = RenderSettings.customReflection;
            yield return null;
            yield return null;
            int materials = CountSkyMaterials(sky.name);
            Assert.That(materials, Is.GreaterThanOrEqualTo(1));
            for (int frame = 0; frame < 5; frame++)
            {
                _lighting.SetWorldLighting(true, true);
                Assert.That(RenderSettings.skybox, Is.SameAs(sky));
                Assert.That(RenderSettings.customReflection, Is.SameAs(reflection));
                yield return null;
            }
            for (int entry = 0; entry < 20; entry++)
            {
                _lighting.SetWorldLighting(true);
                _lighting.SetWorldLighting(true, true);
            }
            AssertSunbreakVistaEnabled();
            Assert.That(RenderSettings.customReflection, Is.SameAs(reflection));
            Assert.That(CountSkyMaterials(sky.name), Is.EqualTo(materials),
                "Frame updates and mode switches must keep the authored sky shared, never create material instances.");
        }

        [Test]
        public void DaylightProbe_LiftsCharacterFrontsWithoutFlatteningVerticalContrast()
        {
            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
            AssertReadableDirectionalFill();
        }

        [TestCase(GameModeId.Arena)]
        [TestCase(GameModeId.Boss)]
        [TestCase(GameModeId.HeroSiege)]
        public void SwitchingToBoundedModes_RestoresTheirOriginalLighting(GameModeId mode)
        {
            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
            _controller.StartLocalSession(mode, ClassId.Mage, SpecializationId.Fire);
            AssertBaselineRestored();
        }

        [Test]
        public void ReturnHome_RestoresOriginalLightingAfterWorldModeSwitches()
        {
            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
            _controller.StartLocalSession(GameModeId.Leveling, ClassId.Mage, SpecializationId.Fire);
            Assert.That(_lighting.IsWorldLightingEnabled, Is.True);
            _controller.ReturnHome();
            AssertBaselineRestored();
        }

        [Test]
        public void DisablingLightingOwner_RestoresOriginalLighting()
        {
            _controller.StartLocalSession(GameModeId.PveLeveling, ClassId.Mage, SpecializationId.Fire);
            _lighting.enabled = false;
            AssertBaselineRestored();
        }

        [Test]
        public void DungeonTransition_LeavesDaylightAfterTheTraversalFloor()
        {
            _controller.StartLocalSession(GameModeId.Dungeon, ClassId.Mage, SpecializationId.Fire);
            Assert.That(_lighting.IsWorldLightingEnabled, Is.True);
            DungeonRunController run = GetField<DungeonRunController>(_controller, "_dungeonRun");
            EncounterDefinition floor = run.Definition.GetEncounter(0);
            for (int index = 0; index < floor.SpawnCount; index++)
            {
                run.ObserveCombatEvent(CombatEvent.Create(index + 1, CombatEventKind.Death,
                    run.Hero, floor.GetSpawn(index).Participant));
            }

            Assert.That(run.TryAdvanceToNextFloor(100, out EncounterDefinition secondFloor), Is.True);
            Invoke(_controller, "SetupRunEncounter", secondFloor);
            Assert.That(run.State.CurrentEncounterIndex, Is.EqualTo(1));
            AssertBaselineRestored();
        }

        private void AssertBaselineRestored()
        {
            Assert.That(_lighting.IsWorldLightingEnabled, Is.False);
            Assert.That(Quaternion.Angle(_key.transform.rotation, _baselineRotation), Is.LessThan(0.001f));
            Assert.That(_key.color, Is.EqualTo(_baselineKeyColor));
            Assert.That(_key.intensity, Is.EqualTo(_baselineKeyIntensity));
            Assert.That(_key.shadowStrength, Is.EqualTo(_baselineShadowStrength));
            Assert.That(RenderSettings.ambientSkyColor, Is.EqualTo(_baselineSky));
            Assert.That(RenderSettings.ambientEquatorColor, Is.EqualTo(_baselineEquator));
            Assert.That(RenderSettings.ambientGroundColor, Is.EqualTo(_baselineGround));
            Assert.That(RenderSettings.fogColor, Is.EqualTo(_baselineFogColor));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(_baselineFogDensity));
            AssertVistaAndSkyRestored(_baselineSkybox);
            SphericalHarmonicsL2 probe = RenderSettings.ambientProbe;
            for (int channel = 0; channel < 3; channel++)
            {
                for (int coefficient = 0; coefficient < 9; coefficient++)
                {
                    Assert.That(probe[channel, coefficient],
                        Is.EqualTo(_baselineProbe[channel, coefficient]).Within(0.00001f));
                }
            }
        }

        private void AssertSunbreakVistaEnabled()
        {
            Assert.That(_lighting.SunbreakVista.activeSelf, Is.True);
            Assert.That(_lighting.SunbreakVista.activeInHierarchy, Is.True);
            Material sky = GetField<Material>(_lighting, "_sunbreakSky");
            Assert.That(sky, Is.Not.Null);
            Assert.That(sky.shader.name, Is.EqualTo("Skybox/Panoramic"));
            Assert.That(RenderSettings.skybox, Is.SameAs(sky));
        }

        private void AssertVistaAndSkyRestored(Material sky)
        {
            Assert.That(_lighting.SunbreakVista.activeSelf, Is.False);
            Assert.That(RenderSettings.skybox, Is.SameAs(sky));
        }

        private static int CountSkyMaterials(string authoredName)
        {
            int count = 0;
            foreach (Material material in Resources.FindObjectsOfTypeAll<Material>())
            {
                if (material.name.StartsWith(authoredName, StringComparison.Ordinal)) { count++; }
            }
            return count;
        }

        private static void AssertReadableDirectionalFill()
        {
            // The first daylight capture still lost character fronts in shadow.
            // Guard the actual linear probe, not only its sRGB color settings.
            SphericalHarmonicsL2 earlyDaylight = new SphericalHarmonicsL2();
            earlyDaylight.AddAmbientLight(new Color(0.43f, 0.46f, 0.50f).linear);
            earlyDaylight.AddDirectionalLight(Vector3.up,
                WorldLightingPresenter.WorldSkyColor.linear, 0.5f);
            earlyDaylight.AddDirectionalLight(Vector3.down,
                new Color(0.25f, 0.24f, 0.22f).linear, 0.18f);
            Vector3[] directions = { Vector3.forward, Vector3.right, Vector3.up, Vector3.down };
            Color[] previousFill = new Color[directions.Length];
            Color[] currentFill = new Color[directions.Length];
            earlyDaylight.Evaluate(directions, previousFill);
            RenderSettings.ambientProbe.Evaluate(directions, currentFill);

            // The unchanged directional sky also contributes to horizontal SH
            // samples, so their total uplift is smaller than the neutral term's.
            // Unity's evaluated probe measured 1.589x after the daylight tuning;
            // require at least 1.50x while retaining sky/ground contrast below.
            for (int direction = 0; direction < 2; direction++)
            {
                for (int channel = 0; channel < 3; channel++)
                {
                    Assert.That(currentFill[direction][channel],
                        Is.GreaterThan(previousFill[direction][channel] * 1.50f),
                        "Linear fill must lift vertical character surfaces: direction " +
                        direction + ", channel " + channel + ".");
                }
            }

            Assert.That(currentFill[2].grayscale, Is.GreaterThan(currentFill[0].grayscale),
                "The sky should remain directional rather than flattening the daylight look.");
            Assert.That(currentFill[0].grayscale, Is.GreaterThan(currentFill[3].grayscale),
                "Ground bounce should lift undersides without overpowering the sky.");
        }

        private static T GetField<T>(object target, string field)
        {
            return (T)target.GetType().GetField(field,
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        }

        private static void SetField(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(target, value);
        }

        private static void Invoke(object target, string method, object value)
        {
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(target, new[] { value });
        }
    }
}
