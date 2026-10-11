using System.Collections;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Unity.Presentation;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class CombatEffectsPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
        }

        [Test]
        public void MajorSpellFamilies_UseDifferentGeometricProfiles()
        {
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.Fireball),
                Is.EqualTo(SpellVisualProfile.Fire));
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.Frostbolt),
                Is.EqualTo(SpellVisualProfile.Frost));
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.Penance),
                Is.EqualTo(SpellVisualProfile.Radiant));
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.ShadowWordDeath),
                Is.EqualTo(SpellVisualProfile.Shadow));
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.ArcaneBolt),
                Is.EqualTo(SpellVisualProfile.Arcane));
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.Envenom),
                Is.EqualTo(SpellVisualProfile.Venom));
        }

        [TestCase(AbilityId.BladeTempest, SpellVisualProfile.Physical, EffectMotion.BladeStorm)]
        [TestCase(AbilityId.DawnNova, SpellVisualProfile.Radiant, EffectMotion.Nova)]
        [TestCase(AbilityId.UmbralCollapse, SpellVisualProfile.Shadow, EffectMotion.Nova)]
        [TestCase(AbilityId.FlameSurge, SpellVisualProfile.Fire, EffectMotion.Nova)]
        [TestCase(AbilityId.Blizzard, SpellVisualProfile.Frost, EffectMotion.Nova)]
        [TestCase(AbilityId.ArrowRain, SpellVisualProfile.Physical, EffectMotion.Volley)]
        public void HeroSiegeAreas_HaveExplicitClassReadablePresentation(
            AbilityId ability,
            SpellVisualProfile expectedProfile,
            EffectMotion expectedMotion)
        {
            Assert.That(
                AbilityPresentationCatalog.TryGet(
                    ability,
                    out AbilityPresentation presentation),
                Is.True,
                ability + " needs stable Hero Siege presentation metadata.");
            Assert.That(presentation.Ability, Is.EqualTo(ability));
            Assert.That(presentation.Motion, Is.EqualTo(expectedMotion));
            Assert.That(presentation.Size, Is.GreaterThan(2.4f));
            Assert.That(string.IsNullOrWhiteSpace(presentation.Style), Is.False);
            Assert.That(
                SpellVisualLanguage.TryResolveProfile(
                    ability,
                    out SpellVisualProfile profile),
                Is.True,
                ability + " must not use the physical fallback implicitly.");
            Assert.That(profile, Is.EqualTo(expectedProfile));
        }

        [TestCase(
            AbilityId.Penance,
            CombatSfxCue.CastReleaseRadiant,
            CombatSfxCue.ImpactRadiant)]
        [TestCase(
            AbilityId.Fireball,
            CombatSfxCue.CastReleaseFire,
            CombatSfxCue.ImpactFire)]
        [TestCase(
            AbilityId.Frostbolt,
            CombatSfxCue.CastReleaseFrost,
            CombatSfxCue.ImpactFrost)]
        [TestCase(
            AbilityId.MindBlast,
            CombatSfxCue.CastReleaseShadow,
            CombatSfxCue.ImpactShadow)]
        [TestCase(
            AbilityId.ArcaneBolt,
            CombatSfxCue.CastReleaseArcane,
            CombatSfxCue.ImpactArcane)]
        [TestCase(
            AbilityId.Envenom,
            CombatSfxCue.CastReleaseVenom,
            CombatSfxCue.ImpactVenom)]
        [TestCase(
            AbilityId.BladeTempest,
            CombatSfxCue.CastReleasePhysical,
            CombatSfxCue.ImpactPhysical)]
        public void MajorSpellFamilies_UseDistinctReleaseAndImpactCues(
            AbilityId ability,
            CombatSfxCue releaseCue,
            CombatSfxCue impactCue)
        {
            Assert.That(
                CombatSoundLanguage.ResolveReleaseCue(ability),
                Is.EqualTo(releaseCue));
            Assert.That(
                CombatSoundLanguage.ResolveImpactCue(ability),
                Is.EqualTo(impactCue));
        }

        [Test]
        public void HazardSound_StaysDistinctFromAbilityImpactFamilies()
        {
            Assert.That(
                CombatSoundLanguage.ResolveImpactCue(AbilityId.None),
                Is.EqualTo(CombatSfxCue.Hazard));
        }

        [Test]
        public void UtilityActions_PreserveTheirClassVisualLanguage()
        {
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.ArenaTrinket),
                Is.EqualTo(SpellVisualProfile.Arcane));
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.PowerInfusion),
                Is.EqualTo(SpellVisualProfile.Radiant));
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.Combustion),
                Is.EqualTo(SpellVisualProfile.Fire));
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.IcyVeins),
                Is.EqualTo(SpellVisualProfile.Frost));
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.Evasion),
                Is.EqualTo(SpellVisualProfile.Shadow));
            Assert.That(
                SpellVisualLanguage.ResolveProfile(AbilityId.ColdBlood),
                Is.EqualTo(SpellVisualProfile.Venom));
        }

        [Test]
        public void HunterPrecisionShots_HaveExplicitStrategyCameraSilhouettes()
        {
            AssertHunterShotProfile(
                AbilityId.SteadyShot,
                HunterShotVisualProfile.Precision);
            AssertHunterShotProfile(
                AbilityId.AimedShot,
                HunterShotVisualProfile.Heavy);
            AssertHunterShotProfile(
                AbilityId.ChimeraShot,
                HunterShotVisualProfile.TwinSerpent);
            Assert.That(
                SpellVisualLanguage.TryResolveHunterShotProfile(
                    AbilityId.Fireball,
                    out HunterShotVisualProfile fallback),
                Is.False);
            Assert.That(fallback, Is.EqualTo(HunterShotVisualProfile.None));
        }

        [TestCase(AbilityId.QuickStrike)]
        [TestCase(AbilityId.Kick)]
        [TestCase(AbilityId.Anchor)]
        [TestCase(AbilityId.Hemorrhage)]
        [TestCase(AbilityId.Eviscerate)]
        [TestCase(AbilityId.KidneyShot)]
        [TestCase(AbilityId.Blind)]
        public void RoguePhysicalAbilities_HaveExplicitVisualProfiles(AbilityId ability)
        {
            Assert.That(
                SpellVisualLanguage.TryResolveProfile(
                    ability,
                    out SpellVisualProfile profile),
                Is.True,
                ability + " must not inherit the generic physical fallback.");
            Assert.That(profile, Is.EqualTo(SpellVisualProfile.Physical));
        }

        [Test]
        public void UnknownAbility_UsesFallbackWithoutClaimingExplicitMapping()
        {
            Assert.That(
                SpellVisualLanguage.TryResolveProfile(
                    AbilityId.None,
                    out SpellVisualProfile profile),
                Is.False);
            Assert.That(profile, Is.EqualTo(SpellVisualProfile.Physical));
        }

        [TestCase(AbilityId.Mutilate, SpellBurstSilhouette.MartialContact)]
        [TestCase(AbilityId.Blind, SpellBurstSilhouette.PhysicalPulse)]
        [TestCase(AbilityId.ColdBlood, SpellBurstSilhouette.VenomSplash)]
        [TestCase(AbilityId.Fireball, SpellBurstSilhouette.FireEruption)]
        [TestCase(AbilityId.Frostbolt, SpellBurstSilhouette.FrostShatter)]
        [TestCase(AbilityId.Penance, SpellBurstSilhouette.RadiantJudgment)]
        [TestCase(AbilityId.MindBlast, SpellBurstSilhouette.ShadowCollapse)]
        [TestCase(AbilityId.ArcaneBolt, SpellBurstSilhouette.ArcaneFracture)]
        public void BurstSilhouettes_AreExplicitAndGeometryFirst(
            AbilityId ability,
            SpellBurstSilhouette expected)
        {
            Assert.That(
                SpellVisualLanguage.ResolveBurstSilhouette(ability),
                Is.EqualTo(expected));
        }

        [Test]
        public void Bootstrap_EffectPoolUsesAuthoredMultiPartRigs()
        {
            CombatEffectsPresenter presenter =
                Object.FindAnyObjectByType<CombatEffectsPresenter>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(presenter.EffectPoolCount, Is.EqualTo(24));
            for (int index = 0; index < presenter.EffectPoolCount; index++)
            {
                CombatEffectView effect = presenter.GetEffect(index);
                Assert.That(effect, Is.Not.Null);
                Assert.That(effect.HasAuthoredSpellRig, Is.True);
                Assert.That(effect.MotifCount, Is.GreaterThanOrEqualTo(10));
                Assert.That(effect.HaloCount, Is.GreaterThanOrEqualTo(3));
                Assert.That(effect.TrailCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(
                    effect.HasProfileMeshes,
                    Is.True,
                    "Every pooled spell rig must include its fire and crystal silhouettes.");
                Assert.That(
                    effect.HasParticleLayer,
                    Is.True,
                    "Every pooled spell rig must include its reusable micro-particle layer.");
                Assert.That(
                    effect.HasGroundParticleLayer,
                    Is.True,
                    "Every pooled spell rig must include bounded ground dust/spark depth.");
                Assert.That(
                    effect.HasPulseLight,
                    Is.True,
                    "Every pooled spell rig must include its disabled-by-default light pulse.");
            }
        }

        [Test]
        public void SpatialVisibilityEnvelope_IsReadableButStillTranslucent()
        {
            Assert.That(CombatEffectView.CoreAlphaCap, Is.EqualTo(0.85f));
            Assert.That(CombatEffectView.ShellAlphaCap, Is.EqualTo(0.56f));
            Assert.That(CombatEffectView.MotifAlphaCap, Is.EqualTo(0.7f));
            Assert.That(CombatEffectView.TrailAlphaCap, Is.EqualTo(0.68f));
            Assert.That(
                CombatEffectView.ProjectileParticleMinimumSize,
                Is.GreaterThanOrEqualTo(0.22f));
            Assert.That(
                CombatEffectView.ProjectileParticleMaximumSize,
                Is.GreaterThanOrEqualTo(0.6f));
            Assert.That(CombatEffectView.SpatialParticleMinimumSize, Is.EqualTo(0.3f));
            Assert.That(CombatEffectView.SpatialParticleMaximumSize, Is.EqualTo(0.9f));
            Assert.That(CombatEffectView.ProjectileCoreAlphaCap, Is.EqualTo(0.68f));
            Assert.That(CombatEffectView.ProjectileShellAlphaCap, Is.EqualTo(0.24f));
        }

        [UnityTest]
        public IEnumerator ProjectileDamage_SchedulesArrivalAfterVisibleTravel()
        {
            CombatEffectsPresenter presenter =
                Object.FindAnyObjectByType<CombatEffectsPresenter>();
            GameObject caster = new GameObject("VfxTestCaster");
            GameObject target = new GameObject("VfxTestTarget");
            GameObject aim = new GameObject("AimPoint");
            aim.transform.SetParent(target.transform, false);
            caster.transform.position = new Vector3(-4f, 1f, 0f);
            target.transform.position = new Vector3(4f, 0f, 0f);
            aim.transform.localPosition = Vector3.up;

            presenter.PresentCastCompleted(
                AbilityId.Fireball,
                caster.transform,
                aim.transform,
                aim.transform.position);
            CombatEffectView projectile = FindActiveEffect(
                presenter,
                AbilityId.Fireball,
                EffectVisualPhase.Primary);
            Assert.That(projectile, Is.Not.Null);
            Assert.That(projectile.VisualProfile, Is.EqualTo(SpellVisualProfile.Fire));
            Assert.That(projectile.TravelProgress, Is.LessThan(0.2f));
            Assert.That(
                projectile.GroundParticlesPlaying,
                Is.False,
                "Travelling projectiles must not drag a ground decal/dust layer through space.");

            presenter.PresentDamage(
                AbilityId.Fireball,
                caster.transform,
                target.transform,
                25f);
            Assert.That(presenter.PendingProjectileArrivalCount, Is.EqualTo(1));
            Assert.That(
                FindActiveEffect(presenter, AbilityId.Fireball, EffectVisualPhase.Impact),
                Is.Null,
                "The target burst must not appear in the launch frame.");

            yield return new WaitForSeconds(0.47f);

            Assert.That(presenter.PendingProjectileArrivalCount, Is.Zero);
            CombatEffectView impact = FindActiveEffect(
                presenter,
                AbilityId.Fireball,
                EffectVisualPhase.Impact);
            Assert.That(
                impact,
                Is.Not.Null,
                "Fireball should resolve with a dedicated arrival burst.");
            Assert.That(
                impact.VisualSize,
                Is.GreaterThanOrEqualTo(0.72f),
                "Impact feedback must remain readable at the full-arena camera distance.");
            Assert.That(impact.VisualDuration, Is.GreaterThanOrEqualTo(0.24f));

            Object.Destroy(caster);
            Object.Destroy(target);
        }

        [TestCase(EffectMotion.Nova, true)]
        [TestCase(EffectMotion.Burst, true)]
        [TestCase(EffectMotion.Trap, true)]
        [TestCase(EffectMotion.BladeStorm, true)]
        [TestCase(EffectMotion.Projectile, false)]
        [TestCase(EffectMotion.Tether, false)]
        [TestCase(EffectMotion.Shield, false)]
        [TestCase(EffectMotion.Volley, false)]
        public void PrimaryDestinationPolicy_GroundsOnlyAreaMotions(
            EffectMotion motion,
            bool expectedGrounded)
        {
            Assert.That(
                CombatEffectsPresenter.UsesGroundedPrimaryDestination(motion),
                Is.EqualTo(expectedGrounded));
        }

        [UnityTest]
        public IEnumerator BladeTempest_UsesGroundedCounterRotatingKnifeStorm()
        {
            CombatEffectsPresenter presenter =
                Object.FindAnyObjectByType<CombatEffectsPresenter>();
            presenter.enabled = false;
            GameObject caster = new GameObject("BladeTempestVfxCaster");
            caster.transform.position = new Vector3(1.5f, 0f, -0.75f);
            int poolCount = presenter.EffectPoolCount;

            presenter.PresentCastCompleted(
                AbilityId.BladeTempest,
                caster.transform,
                null,
                caster.transform.position);
            CombatEffectView storm = FindActiveEffect(
                presenter,
                AbilityId.BladeTempest,
                EffectVisualPhase.Primary);

            Assert.That(storm, Is.Not.Null);
            Assert.That(storm.VisualMotion, Is.EqualTo(EffectMotion.BladeStorm));
            Assert.That(storm.VisualTarget, Is.EqualTo(caster.transform.position));
            Assert.That(storm.GroundParticlesPlaying, Is.True);
            storm.Tick(0.17f);
            Assert.That(
                storm.ActiveMotifCount,
                Is.GreaterThanOrEqualTo(10),
                "Blade Tempest needs a full radial knife silhouette, not one generic burst.");
            Assert.That(
                storm.ActiveMotifVerticalSpan,
                Is.GreaterThan(0.12f),
                "The knife storm needs two height lanes at the strategy camera angle.");
            Assert.That(
                CountActiveNamedChildren(storm.transform, "SpellRibbon_"),
                Is.EqualTo(2),
                "Counter-rotating sweep trails communicate the two blade lanes.");
            Assert.That(storm.ActiveHaloCount, Is.Zero,
                "A physical knife storm must not read as a generic magic rune.");
            Assert.That(presenter.EffectPoolCount, Is.EqualTo(poolCount));

            Object.Destroy(caster);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CastCompletion_UsesGroundPointForAreas_AndAimPointForTravel()
        {
            CombatEffectsPresenter presenter =
                Object.FindAnyObjectByType<CombatEffectsPresenter>();
            GameObject caster = new GameObject("GroundingTestCaster");
            GameObject target = new GameObject("GroundingTestTarget");
            GameObject aim = new GameObject("AimPoint");
            aim.transform.SetParent(target.transform, false);
            caster.transform.position = new Vector3(-2f, 0f, 1f);
            target.transform.position = new Vector3(3f, 0f, -1f);
            aim.transform.localPosition = new Vector3(0f, 1.35f, 0f);

            presenter.PresentCastCompleted(
                AbilityId.BlastWave,
                caster.transform,
                aim.transform,
                caster.transform.position);
            CombatEffectView burst = FindActiveEffect(
                presenter,
                AbilityId.BlastWave,
                EffectVisualPhase.Primary);
            Assert.That(burst, Is.Not.Null);
            Assert.That(burst.VisualTarget, Is.EqualTo(caster.transform.position));
            burst.Tick(0.16f);
            Assert.That(burst.ActiveHaloCount, Is.Zero,
                "Grounded area spells must not fall back to flat rune rings.");
            Assert.That(burst.ActiveMotifCount, Is.EqualTo(3),
                "Blast Wave keeps only three depth-writing embers; particles carry the body.");
            Assert.That(burst.LiveParticleCount, Is.GreaterThanOrEqualTo(30),
                "Spatial spells must start with a dense billboard volume.");
            Assert.That(burst.ActiveMotifVerticalSpan, Is.GreaterThan(0.16f),
                "Area silhouettes need visible world-space height, not radial floor spokes.");
            Assert.That(
                burst.MaximumActiveMotifRadialAlignment(caster.transform.position),
                Is.LessThan(0.92f),
                "Area fragments must not share a center-pointing spoke alignment.");

            Vector3 arrowRainPoint = new Vector3(4.5f, 0f, 2.25f);
            presenter.PresentCastCompleted(
                AbilityId.ArrowRain,
                caster.transform,
                aim.transform,
                arrowRainPoint);
            CombatEffectView arrowRain = FindActiveEffect(
                presenter,
                AbilityId.ArrowRain,
                EffectVisualPhase.Primary);
            Assert.That(arrowRain, Is.Not.Null);
            Assert.That(
                arrowRain.VisualTarget,
                Is.EqualTo(arrowRainPoint),
                "Arrow Rain must land on the selected area instead of following one unit's aim transform.");

            presenter.PresentCastCompleted(
                AbilityId.Fireball,
                caster.transform,
                aim.transform,
                target.transform.position);
            CombatEffectView projectile = FindActiveEffect(
                presenter,
                AbilityId.Fireball,
                EffectVisualPhase.Primary);
            Assert.That(projectile, Is.Not.Null);
            Assert.That(projectile.VisualTarget, Is.EqualTo(aim.transform.position));
            Assert.That(projectile.UsesRoundedProjectileBody, Is.False,
                "Fireball must use a directional ember head rather than a clean sphere.");
            Assert.That(projectile.LiveParticleCount, Is.GreaterThanOrEqualTo(20));
            Assert.That(projectile.ParticleEmitterRadius, Is.InRange(0.05f, 0.06f),
                "Fireball needs a coherent moving cone rather than a cloud centered on the caster.");
            Assert.That(projectile.ActiveMotifCount, Is.Zero,
                "Fireball volume must come from wisps, not flat mesh splinters.");

            presenter.PresentCastCompleted(
                AbilityId.Penance,
                caster.transform,
                aim.transform,
                target.transform.position);
            CombatEffectView penance = FindActiveEffect(
                presenter,
                AbilityId.Penance,
                EffectVisualPhase.Primary);
            Assert.That(penance, Is.Not.Null);
            penance.Tick(0.08f);
            Assert.That(penance.UsesRoundedProjectileBody, Is.False);
            Assert.That(penance.LiveParticleCount, Is.GreaterThanOrEqualTo(20));
            Assert.That(penance.ParticleEmitterRadius, Is.GreaterThanOrEqualTo(0.1f),
                "Penance beads need a volumetric wisp envelope.");
            Assert.That(penance.ActiveMotifCount, Is.EqualTo(4),
                "Penance must reveal its first two time-offset bolt lanes, not one fused shell.");
            penance.Tick(0.04f);
            Assert.That(penance.ActiveMotifCount, Is.EqualTo(6),
                "Penance must resolve into three major beads plus three spatial wake flecks.");
            Assert.That(penance.ActiveHaloCount, Is.Zero,
                "Penance readability must come from beads and its ribbon, not a tiny rune.");
            Assert.That(
                CountActiveNamedChildren(penance.transform, "SpellRibbon_"),
                Is.Zero,
                "Penance must not use view-facing ribbon panels.");

            presenter.PresentImpact(
                AbilityId.Penance,
                null,
                target.transform.position);
            CombatEffectView endpoint = FindActiveEffect(
                presenter,
                AbilityId.Penance,
                EffectVisualPhase.Impact);
            Assert.That(endpoint, Is.Not.Null);
            Assert.That(endpoint.VisualTarget, Is.EqualTo(target.transform.position));
            Assert.That(endpoint.GroundParticlesPlaying, Is.True,
                "Projectile endpoint response must remain grounded at the target root.");

            Object.Destroy(caster);
            Object.Destroy(target);
            yield return null;
        }

        [UnityTest]
        public IEnumerator BlastWaveAndFrostNova_UseStaggeredVolumetricAreaRecipes()
        {
            CombatEffectsPresenter presenter =
                Object.FindAnyObjectByType<CombatEffectsPresenter>();
            GameObject caster = new GameObject("VolumetricAreaCaster");
            caster.transform.position = new Vector3(1.25f, 0f, -0.75f);

            AssertVolumetricAreaRecipe(
                presenter,
                caster.transform,
                AbilityId.BlastWave,
                SpellVisualProfile.Fire,
                2.65f,
                0.46f,
                0.42f,
                0.72f,
                1.5f,
                3.2f,
                18f,
                56,
                0.65f,
                0.28f);
            AssertVolumetricAreaRecipe(
                presenter,
                caster.transform,
                AbilityId.FrostNova,
                SpellVisualProfile.Frost,
                2.1f,
                0.42f,
                0.45f,
                0.8f,
                0.7f,
                1.7f,
                10f,
                48,
                0.8f,
                0.05f);

            Object.Destroy(caster);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Mutilate_UsesTwinCloseContactBladeTrails()
        {
            CombatEffectsPresenter presenter =
                Object.FindAnyObjectByType<CombatEffectsPresenter>();
            GameObject caster = new GameObject("VfxRogue");
            GameObject target = new GameObject("VfxContactTarget");
            caster.transform.position = new Vector3(-0.8f, 0f, 0f);
            target.transform.position = Vector3.zero;

            presenter.PresentCastCompleted(
                AbilityId.Mutilate,
                caster.transform,
                target.transform,
                target.transform.position);
            yield return null;

            CombatEffectView effect = FindActiveEffect(
                presenter,
                AbilityId.Mutilate,
                EffectVisualPhase.Primary);
            Assert.That(effect, Is.Not.Null);
            Assert.That(effect.VisualProfile, Is.EqualTo(SpellVisualProfile.Venom));
            Assert.That(effect.UsesCloseContactVisual, Is.True);
            Assert.That(effect.TrailCount, Is.EqualTo(2));

            Object.Destroy(caster);
            Object.Destroy(target);
        }

        [UnityTest]
        public IEnumerator CastStarts_UseProfileSpecificSilhouettesWithoutGrowingPool()
        {
            CombatEffectsPresenter presenter =
                Object.FindAnyObjectByType<CombatEffectsPresenter>();
            Assert.That(presenter, Is.Not.Null);
            int poolCount = presenter.EffectPoolCount;
            presenter.enabled = false;
            GameObject caster = new GameObject("VfxCastStartCaster");
            caster.transform.position = new Vector3(1f, 1.1f, -2f);

            AssertCastStartSignature(
                presenter,
                caster.transform,
                AbilityId.Fireball,
                shellActive: true,
                discActive: false,
                activeMotifs: 12,
                activeHalos: 1);
            AssertCastStartSignature(
                presenter,
                caster.transform,
                AbilityId.Frostbolt,
                shellActive: false,
                discActive: false,
                activeMotifs: 12,
                activeHalos: 1);
            AssertCastStartSignature(
                presenter,
                caster.transform,
                AbilityId.Penance,
                shellActive: false,
                discActive: false,
                activeMotifs: 12,
                activeHalos: 1);
            AssertCastStartSignature(
                presenter,
                caster.transform,
                AbilityId.ArcaneBolt,
                shellActive: true,
                discActive: false,
                activeMotifs: 12,
                activeHalos: 1);
            AssertCastStartSignature(
                presenter,
                caster.transform,
                AbilityId.MindBlast,
                shellActive: true,
                discActive: false,
                activeMotifs: 12,
                activeHalos: 1);
            AssertCastStartSignature(
                presenter,
                caster.transform,
                AbilityId.Kick,
                shellActive: false,
                discActive: false,
                activeMotifs: 2,
                activeHalos: 0);

            Assert.That(
                presenter.EffectPoolCount,
                Is.EqualTo(poolCount),
                "Class-specific cast silhouettes must reuse the existing mobile VFX pool.");
            Object.Destroy(caster);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Impacts_UseClassDistinctPooledGeometry()
        {
            CombatEffectsPresenter presenter =
                Object.FindAnyObjectByType<CombatEffectsPresenter>();
            Assert.That(presenter, Is.Not.Null);
            int poolCount = presenter.EffectPoolCount;
            presenter.enabled = false;
            GameObject target = new GameObject("VfxImpactTarget");
            target.transform.position = new Vector3(0.6f, 0.9f, -0.4f);

            AssertBurstSignature(presenter, target.transform, AbilityId.Fireball,
                SpellBurstSilhouette.FireEruption, false, false, 12, 1);
            AssertBurstSignature(presenter, target.transform, AbilityId.Frostbolt,
                SpellBurstSilhouette.FrostShatter, false, false, 12, 1);
            AssertBurstSignature(presenter, target.transform, AbilityId.Penance,
                SpellBurstSilhouette.RadiantJudgment, true, false, 4, 1);
            AssertBurstSignature(presenter, target.transform, AbilityId.MindBlast,
                SpellBurstSilhouette.ShadowCollapse, true, false, 12, 1);
            AssertBurstSignature(presenter, target.transform, AbilityId.ArcaneBolt,
                SpellBurstSilhouette.ArcaneFracture, true, false, 12, 1);
            AssertBurstSignature(presenter, target.transform, AbilityId.Blind,
                SpellBurstSilhouette.PhysicalPulse, false, false, 4, 1);

            Assert.That(presenter.EffectPoolCount, Is.EqualTo(poolCount));
            Object.Destroy(target);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HunterPrecisionShots_UseLongShaftsAndPooledLineTrails()
        {
            CombatEffectsPresenter presenter =
                Object.FindAnyObjectByType<CombatEffectsPresenter>();
            Assert.That(presenter, Is.Not.Null);
            int poolCount = presenter.EffectPoolCount;
            presenter.enabled = false;

            GameObject caster = new GameObject("VfxHunter");
            GameObject target = new GameObject("VfxHunterTarget");
            caster.transform.position = new Vector3(-1.65f, 1.05f, -0.55f);
            target.transform.position = new Vector3(1.35f, 1.05f, 0.7f);

            CombatEffectView steady = StageHunterShot(
                presenter,
                caster.transform,
                target.transform,
                AbilityId.SteadyShot,
                HunterShotVisualProfile.Precision);
            Transform steadyCore = FindDescendant(steady.transform, "LuminousCore");
            Transform steadyHead = FindDescendant(steady.transform, "EnergyShell");
            LineRenderer steadyPrimaryTrail = FindDescendant(
                steady.transform,
                "SpellRibbon_0").GetComponent<LineRenderer>();
            LineRenderer steadySecondaryTrail = FindDescendant(
                steady.transform,
                "SpellRibbon_1").GetComponent<LineRenderer>();
            float steadyShaftLength = steadyCore.localScale.z;
            float steadyShaftThickness = steadyCore.localScale.x;
            Assert.That(
                steadyShaftLength,
                Is.GreaterThanOrEqualTo(1.3f),
                "Steady Shot needs a continuous, strategy-readable shaft at the " +
                "3.25 metre showcase separation.");
            Assert.That(
                steadyShaftThickness,
                Is.GreaterThanOrEqualTo(0.08f),
                "The precision shaft must not collapse into a one-pixel kink.");
            Assert.That(
                steadyHead.localScale.z,
                Is.GreaterThan(steadyHead.localScale.x * 2f),
                "Steady Shot needs an elongated arrowhead rather than a glowing ball.");
            Assert.That(
                steadyHead.localScale.x,
                Is.GreaterThanOrEqualTo(0.2f),
                "The Steady Shot arrowhead must remain obvious at strategy zoom.");
            Assert.That(
                steadyPrimaryTrail.widthMultiplier,
                Is.GreaterThanOrEqualTo(0.075f),
                "The primary Steady Shot flight line is too thin for the strategy camera.");
            Assert.That(
                steadySecondaryTrail.widthMultiplier,
                Is.GreaterThanOrEqualTo(0.035f),
                "The secondary Steady Shot flight line must reinforce continuity.");
            AssertReferenceStrategyFlightSpan(
                caster.transform.position,
                steadyCore,
                steadyHead,
                steadyPrimaryTrail);
            steady.Tick(2f);

            CombatEffectView aimed = StageHunterShot(
                presenter,
                caster.transform,
                target.transform,
                AbilityId.AimedShot,
                HunterShotVisualProfile.Heavy);
            float aimedShaftLength = FindDescendant(
                aimed.transform,
                "LuminousCore").localScale.z;
            float aimedShaftThickness = FindDescendant(
                aimed.transform,
                "LuminousCore").localScale.x;
            Assert.That(
                aimedShaftLength,
                Is.GreaterThan(steadyShaftLength * 1.2f),
                "Aimed Shot needs a materially heavier shaft at strategy zoom.");
            Assert.That(
                aimedShaftThickness,
                Is.GreaterThan(steadyShaftThickness * 1.15f),
                "Steady Shot readability must not erase Aimed Shot's heavy profile.");
            aimed.Tick(2f);

            CombatEffectView chimera = StageHunterShot(
                presenter,
                caster.transform,
                target.transform,
                AbilityId.ChimeraShot,
                HunterShotVisualProfile.TwinSerpent);
            Transform chimeraCore = FindDescendant(chimera.transform, "LuminousCore");
            Transform chimeraShell = FindDescendant(chimera.transform, "EnergyShell");
            Assert.That(
                Vector3.Distance(chimeraCore.position, chimeraShell.position),
                Is.GreaterThan(0.22f),
                "Chimera Shot must read as two separated serpent shafts, not one ball.");
            chimera.Tick(2f);

            presenter.PresentImpact(
                AbilityId.ChimeraShot,
                target.transform,
                target.transform.position);
            CombatEffectView impact = FindActiveEffect(
                presenter,
                AbilityId.ChimeraShot,
                EffectVisualPhase.Impact);
            Assert.That(impact, Is.Not.Null);
            impact.Tick(0.08f);
            Assert.That(
                CountActiveNamedChildren(impact.transform, "SpellRibbon_"),
                Is.EqualTo(2),
                "Chimera impact must preserve its twin-line signature.");
            Assert.That(
                presenter.EffectPoolCount,
                Is.EqualTo(poolCount),
                "Hunter readability must reuse the existing mobile VFX pool.");

            Object.Destroy(caster);
            Object.Destroy(target);
            yield return null;
        }

        private static CombatEffectView StageHunterShot(
            CombatEffectsPresenter presenter,
            Transform caster,
            Transform target,
            AbilityId ability,
            HunterShotVisualProfile expectedProfile)
        {
            presenter.PresentCastCompleted(
                ability,
                caster,
                target,
                target.position);
            CombatEffectView effect = FindActiveEffect(
                presenter,
                ability,
                EffectVisualPhase.Primary);
            Assert.That(effect, Is.Not.Null);
            effect.Tick(0.16f);
            Assert.That(effect.HunterShotProfile, Is.EqualTo(expectedProfile));
            Assert.That(
                FindDescendant(effect.transform, "LuminousCore").gameObject.activeSelf,
                Is.True);
            Assert.That(
                FindDescendant(effect.transform, "EnergyShell").gameObject.activeSelf,
                Is.True);
            Assert.That(
                CountActiveNamedChildren(effect.transform, "SpellRibbon_"),
                Is.EqualTo(2));
            Assert.That(
                FindDescendant(effect.transform, "LuminousCore").localScale.z,
                Is.GreaterThan(
                    FindDescendant(effect.transform, "LuminousCore").localScale.x * 10f),
                ability + " must present a long arrow shaft instead of a flying ball.");
            return effect;
        }

        private static void AssertReferenceStrategyFlightSpan(
            Vector3 actorEffectOrigin,
            Transform shaft,
            Transform arrowhead,
            LineRenderer primaryTrail)
        {
            UnityEngine.Camera strategyCamera = UnityEngine.Camera.main;
            Assert.That(strategyCamera, Is.Not.Null);
            SessionRpg.Unity.Camera.StrategyFollowCamera cameraRig =
                strategyCamera.GetComponent<
                    SessionRpg.Unity.Camera.StrategyFollowCamera>();
            Assert.That(cameraRig, Is.Not.Null);

            cameraRig.enabled = false;
            Quaternion rotation = Quaternion.Euler(
                cameraRig.Pitch,
                cameraRig.Yaw,
                0f);
            Vector3 planarForward = rotation * Vector3.forward;
            planarForward.y = 0f;
            planarForward.Normalize();
            Vector3 planarRight = rotation * Vector3.right;
            planarRight.y = 0f;
            planarRight.Normalize();
            Vector3 actorRoot = new Vector3(
                actorEffectOrigin.x,
                0f,
                actorEffectOrigin.z);
            Vector3 focus = actorRoot +
                Vector3.up * 0.9f +
                planarForward * cameraRig.ForwardFocusOffset +
                planarRight * cameraRig.RightFocusOffset;
            strategyCamera.transform.SetPositionAndRotation(
                focus + rotation * Vector3.back * cameraRig.Distance,
                rotation);
            strategyCamera.fieldOfView = cameraRig.FieldOfView;

            Vector3 trailHead = primaryTrail.GetPosition(0);
            Assert.That(
                Vector3.Distance(trailHead, shaft.position),
                Is.LessThan(0.001f),
                "The Steady Shot trail must meet its shaft without a visible gap.");
            Vector3 trailTail = primaryTrail.GetPosition(
                primaryTrail.positionCount - 1);
            Vector3 arrowheadTip = arrowhead.position +
                arrowhead.forward * arrowhead.localScale.z * 0.5f;
            Vector3 tailViewport = strategyCamera.WorldToViewportPoint(trailTail);
            Vector3 tipViewport = strategyCamera.WorldToViewportPoint(arrowheadTip);
            Assert.That(tailViewport.z, Is.GreaterThan(0f));
            Assert.That(tipViewport.z, Is.GreaterThan(0f));

            const float ReferenceWidth = 1920f;
            const float ReferenceHeight = 1080f;
            float horizontalPixels =
                (tipViewport.x - tailViewport.x) * ReferenceWidth;
            float verticalPixels =
                (tipViewport.y - tailViewport.y) * ReferenceHeight;
            float referencePixelSpan = Mathf.Sqrt(
                horizontalPixels * horizontalPixels +
                verticalPixels * verticalPixels);
            Assert.That(
                referencePixelSpan,
                Is.GreaterThanOrEqualTo(80f),
                "Steady Shot must keep at least an 80 px continuous flight line " +
                "in the 1920x1080 strategy-camera reference framing.");
        }

        private static void AssertHunterShotProfile(
            AbilityId ability,
            HunterShotVisualProfile expected)
        {
            Assert.That(
                SpellVisualLanguage.TryResolveHunterShotProfile(
                    ability,
                    out HunterShotVisualProfile actual),
                Is.True,
                ability + " must have an explicit hunter-shot silhouette.");
            Assert.That(actual, Is.EqualTo(expected));
        }

        private static void AssertCastStartSignature(
            CombatEffectsPresenter presenter,
            Transform caster,
            AbilityId ability,
            bool shellActive,
            bool discActive,
            int activeMotifs,
            int activeHalos)
        {
            presenter.PresentCastStarted(ability, caster);
            CombatEffectView effect = FindActiveEffect(
                presenter,
                ability,
                EffectVisualPhase.CastStart);
            Assert.That(effect, Is.Not.Null, ability + " did not start a cast effect.");
            Assert.That(
                effect.VisualSize,
                Is.GreaterThanOrEqualTo(0.62f),
                ability + " cast anticipation is too small for the strategy camera.");
            Assert.That(
                effect.VisualDuration,
                Is.GreaterThanOrEqualTo(0.3f),
                ability + " cast anticipation disappears before it can be read.");
            effect.Tick(0.09f);

            Assert.That(
                FindDescendant(effect.transform, "EnergyShell").gameObject.activeSelf,
                Is.EqualTo(shellActive),
                ability + " reused the wrong cast-start shell silhouette.");
            Assert.That(
                FindDescendant(effect.transform, "GroundSigil").gameObject.activeSelf,
                Is.EqualTo(discActive),
                ability + " reused the wrong cast-start sigil silhouette.");
            Assert.That(
                CountActiveNamedChildren(effect.transform, "Motif_"),
                Is.EqualTo(activeMotifs),
                ability + " reused the wrong cast-start motif density.");
            Assert.That(
                CountActiveNamedChildren(effect.transform, "RuneHalo_"),
                Is.EqualTo(activeHalos),
                ability + " reused the wrong cast-start halo silhouette.");

            effect.Tick(1f);
        }

        private static void AssertBurstSignature(
            CombatEffectsPresenter presenter,
            Transform target,
            AbilityId ability,
            SpellBurstSilhouette silhouette,
            bool shellActive,
            bool discActive,
            int activeMotifs,
            int activeHalos)
        {
            presenter.PresentImpact(ability, target, target.position);
            CombatEffectView effect = FindActiveEffect(
                presenter,
                ability,
                EffectVisualPhase.Impact);
            Assert.That(effect, Is.Not.Null, ability + " did not stage an impact.");
            Assert.That(effect.BurstSilhouette, Is.EqualTo(silhouette));
            effect.Tick(0.07f);
            Assert.That(
                effect.GroundParticlesPlaying,
                Is.True,
                ability + " impact is missing its bounded ground-depth layer.");
            Assert.That(
                effect.PulseLightEnabled,
                Is.True,
                ability + " impact is missing its short spatial light pulse.");
            Assert.That(
                FindDescendant(effect.transform, "EnergyShell").gameObject.activeSelf,
                Is.EqualTo(shellActive),
                ability + " reused the wrong impact shell.");
            Assert.That(
                FindDescendant(effect.transform, "GroundSigil").gameObject.activeSelf,
                Is.EqualTo(discActive),
                ability + " reused the wrong impact sigil.");
            Assert.That(
                CountActiveNamedChildren(effect.transform, "Motif_"),
                Is.EqualTo(activeMotifs),
                ability + " reused the wrong impact motif density.");
            Assert.That(
                CountActiveNamedChildren(effect.transform, "RuneHalo_"),
                Is.EqualTo(activeHalos),
                ability + " reused the wrong impact halo structure.");
            AssertRendererAlphaAtMost(
                FindDescendant(effect.transform, "LuminousCore").GetComponent<Renderer>(),
                CombatEffectView.CoreAlphaCap);
            AssertRendererAlphaAtMost(
                FindDescendant(effect.transform, "EnergyShell").GetComponent<Renderer>(),
                CombatEffectView.ShellAlphaCap);
            AssertRendererAlphaAtMost(
                FindDescendant(effect.transform, "GroundSigil").GetComponent<Renderer>(),
                CombatEffectView.DiscAlphaCap);
            AssertRendererAlphaAtMost(
                FindDescendant(effect.transform, "Motif_0").GetComponent<Renderer>(),
                CombatEffectView.MotifAlphaCap);
            if (activeHalos > 0)
            {
                Assert.That(
                    FindDescendant(effect.transform, "RuneHalo_0")
                        .GetComponent<LineRenderer>().startColor.a,
                    Is.LessThanOrEqualTo(CombatEffectView.HaloAlphaCap + 0.002f));
            }
            effect.Tick(1f);
        }

        private static void AssertRendererAlphaAtMost(
            Renderer renderer,
            float maximum)
        {
            MaterialPropertyBlock properties = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(properties);
            Assert.That(
                properties.GetColor("_BaseColor").a,
                Is.LessThanOrEqualTo(maximum + 0.001f));
        }

        private static void AssertVolumetricAreaRecipe(
            CombatEffectsPresenter presenter,
            Transform caster,
            AbilityId ability,
            SpellVisualProfile expectedProfile,
            float expectedSize,
            float expectedDuration,
            float minimumLifetime,
            float maximumLifetime,
            float minimumSpeed,
            float maximumSpeed,
            float emissionRate,
            int launchParticleCount,
            float radiusThickness,
            float noiseStrength)
        {
            presenter.PresentCastCompleted(
                ability,
                caster,
                caster,
                caster.position);
            CombatEffectView effect = FindActiveEffect(
                presenter,
                ability,
                EffectVisualPhase.Primary);
            Assert.That(effect, Is.Not.Null, ability + " did not stage its area view.");
            Assert.That(effect.VisualProfile, Is.EqualTo(expectedProfile));
            Assert.That(effect.VisualTarget, Is.EqualTo(caster.position));
            Assert.That(effect.VisualSize, Is.EqualTo(expectedSize).Within(0.001f));
            Assert.That(effect.VisualDuration, Is.EqualTo(expectedDuration).Within(0.001f));

            ParticleSystem particles = FindDescendant(
                effect.transform,
                "SpellParticles").GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            Assert.That(
                main.startLifetime.constantMin,
                Is.EqualTo(minimumLifetime).Within(0.001f));
            Assert.That(
                main.startLifetime.constantMax,
                Is.EqualTo(maximumLifetime).Within(0.001f));
            Assert.That(
                main.startSpeed.constantMin,
                Is.EqualTo(minimumSpeed).Within(0.001f));
            Assert.That(
                main.startSpeed.constantMax,
                Is.EqualTo(maximumSpeed).Within(0.001f));
            Assert.That(
                particles.emission.rateOverTime.constant,
                Is.EqualTo(emissionRate).Within(0.001f));
            Assert.That(
                particles.shape.shapeType,
                Is.EqualTo(ParticleSystemShapeType.Hemisphere),
                ability + " must emit through a three-dimensional upper volume.");
            Assert.That(
                particles.shape.radiusThickness,
                Is.EqualTo(radiusThickness).Within(0.001f));
            Assert.That(particles.noise.enabled, Is.True);
            Assert.That(
                particles.noise.strength.constant,
                Is.EqualTo(noiseStrength).Within(0.001f));
            Assert.That(
                particles.GetComponent<ParticleSystemRenderer>().renderMode,
                Is.EqualTo(ParticleSystemRenderMode.Billboard));
            Assert.That(
                effect.LiveParticleCount,
                Is.GreaterThanOrEqualTo(launchParticleCount));

            ParticleSystem groundParticles = FindDescendant(
                effect.transform,
                "GroundDustParticles").GetComponent<ParticleSystem>();
            Assert.That(groundParticles.isPlaying, Is.True);
            Assert.That(
                groundParticles.shape.radiusThickness,
                Is.EqualTo(1f).Within(0.001f),
                ability + " ground haze must fill an irregular footprint, not an edge ring.");
            Assert.That(
                groundParticles.GetComponent<ParticleSystemRenderer>().renderMode,
                Is.EqualTo(ParticleSystemRenderMode.Billboard));

            effect.Tick(0.02f);
            Assert.That(
                effect.ActiveMotifCount,
                Is.InRange(1, 2),
                ability + " must launch in local groups instead of one synchronized starburst.");
            effect.Tick(0.14f);

            int expectedPrimaryFragmentCount = ability == AbilityId.BlastWave
                ? 3
                : 6;
            Assert.That(
                effect.ActiveMotifCount,
                Is.EqualTo(expectedPrimaryFragmentCount));
            Assert.That(effect.ActiveHaloCount, Is.Zero);
            Assert.That(
                CountActiveNamedChildren(effect.transform, "SpellRibbon_"),
                Is.Zero,
                ability + " must not use view-facing panels or radial line ribbons.");
            Assert.That(
                FindDescendant(effect.transform, "GroundSigil").gameObject.activeSelf,
                Is.False,
                ability + " must not use a flat disc body.");
            Assert.That(
                FindDescendant(effect.transform, "LuminousCore").gameObject.activeSelf,
                Is.False,
                ability + " must not hide the volume behind one dominant primitive.");
            Assert.That(
                FindDescendant(effect.transform, "EnergyShell").gameObject.activeSelf,
                Is.False,
                ability + " must not hide the volume behind a fused shell.");
            Assert.That(
                effect.ActiveMotifVerticalSpan,
                Is.GreaterThan(0.16f),
                ability + " needs visible height at the strategy camera.");
            Assert.That(
                ActiveMotifScaleSpan(effect.transform),
                Is.GreaterThan(0.025f),
                ability + " fragments must retain staggered size phases.");
            Assert.That(
                ActiveMotifRadiusSpan(effect.transform, caster.position),
                Is.GreaterThan(ability == AbilityId.BlastWave ? 0.05f : 0.18f),
                ability + " needs a broken footprint rather than an even perimeter.");
            Assert.That(
                effect.MaximumActiveMotifRadialAlignment(caster.position),
                Is.LessThan(0.92f),
                ability + " fragments must rise and shear rather than form spokes.");

            Light pulse = FindDescendant(
                effect.transform,
                "SpellPulseLight").GetComponent<Light>();
            Assert.That(pulse.enabled, Is.True);
            Assert.That(pulse.intensity, Is.GreaterThan(0f));
            Assert.That(
                pulse.transform.position.y - caster.position.y,
                Is.InRange(0.09f, 0.19f),
                ability + " light must remain grounded under the volume.");

            effect.Tick(1f);

            presenter.PresentImpact(ability, null, caster.position);
            CombatEffectView impact = FindActiveEffect(
                presenter,
                ability,
                EffectVisualPhase.Impact);
            Assert.That(impact, Is.Not.Null);
            impact.Tick(0.07f);
            Assert.That(
                impact.ActiveMotifCount,
                Is.EqualTo(ability == AbilityId.BlastWave ? 2 : 4));
            Assert.That(impact.ActiveHaloCount, Is.Zero);
            Assert.That(
                CountActiveNamedChildren(impact.transform, "SpellRibbon_"),
                Is.Zero);
            Assert.That(
                FindDescendant(impact.transform, "GroundSigil").gameObject.activeSelf,
                Is.False);
            Assert.That(
                FindDescendant(impact.transform, "LuminousCore").gameObject.activeSelf,
                Is.False);
            Assert.That(
                FindDescendant(impact.transform, "EnergyShell").gameObject.activeSelf,
                Is.False);
            ParticleSystem impactParticles = FindDescendant(
                impact.transform,
                "SpellParticles").GetComponent<ParticleSystem>();
            Assert.That(
                impactParticles.shape.shapeType,
                Is.EqualTo(ParticleSystemShapeType.Hemisphere));
            Assert.That(
                impactParticles.GetComponent<ParticleSystemRenderer>().renderMode,
                Is.EqualTo(ParticleSystemRenderMode.Billboard));
            Assert.That(
                FindDescendant(
                    impact.transform,
                    "GroundDustParticles").GetComponent<ParticleSystem>()
                    .shape.radiusThickness,
                Is.EqualTo(1f).Within(0.001f));
            impact.Tick(1f);
        }

        private static float ActiveMotifScaleSpan(Transform root)
        {
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                Transform candidate = descendants[index];
                if (!candidate.name.StartsWith("Motif_") ||
                    !candidate.gameObject.activeSelf)
                {
                    continue;
                }

                float magnitude = candidate.localScale.magnitude;
                minimum = Mathf.Min(minimum, magnitude);
                maximum = Mathf.Max(maximum, magnitude);
            }

            return minimum <= maximum ? maximum - minimum : 0f;
        }

        private static float ActiveMotifRadiusSpan(Transform root, Vector3 center)
        {
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                Transform candidate = descendants[index];
                if (!candidate.name.StartsWith("Motif_") ||
                    !candidate.gameObject.activeSelf)
                {
                    continue;
                }

                Vector3 offset = candidate.position - center;
                offset.y = 0f;
                float radius = offset.magnitude;
                minimum = Mathf.Min(minimum, radius);
                maximum = Mathf.Max(maximum, radius);
            }

            return minimum <= maximum ? maximum - minimum : 0f;
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (descendants[index].name == name)
                {
                    return descendants[index];
                }
            }

            Assert.Fail("Missing authored VFX child '" + name + "'.");
            return null;
        }

        private static int CountActiveNamedChildren(Transform root, string prefix)
        {
            int count = 0;
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (descendants[index].name.StartsWith(prefix) &&
                    descendants[index].gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }

        private static CombatEffectView FindActiveEffect(
            CombatEffectsPresenter presenter,
            AbilityId ability,
            EffectVisualPhase phase)
        {
            for (int index = 0; index < presenter.EffectPoolCount; index++)
            {
                CombatEffectView effect = presenter.GetEffect(index);
                if (effect != null &&
                    effect.IsPlaying &&
                    effect.ActiveAbility == ability &&
                    effect.VisualPhase == phase)
                {
                    return effect;
                }
            }

            return null;
        }
    }
}
