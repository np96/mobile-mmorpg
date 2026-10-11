using System;
using NUnit.Framework;
using SessionRpg.Unity.Performance;

namespace SessionRpg.Tests.Performance
{
    public sealed class MobileQualityContractTests
    {
        [Test]
        public void Contracts_DefaultToHighAndTargetSixtyFramesPerSecond()
        {
            Assert.That(
                MobileQualityContracts.DefaultTier,
                Is.EqualTo(MobileQualityTier.High));

            foreach (MobileQualityTier tier in Enum.GetValues(typeof(MobileQualityTier)))
            {
                MobileQualityContract contract = MobileQualityContracts.Get(tier);
                Assert.That(contract.Tier, Is.EqualTo(tier));
                Assert.That(
                    contract.Performance.TargetFrameRate,
                    Is.EqualTo(MobileQualityContracts.TargetFrameRate));
                Assert.That(contract.Performance.TargetFrameRate, Is.EqualTo(60));
                Assert.That(
                    contract.Rendering.ShadowCascadeCount,
                    tier == MobileQualityTier.Performance
                        ? Is.EqualTo(1)
                        : Is.EqualTo(2));
            }

            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.Performance)
                    .Rendering.ShadowDistance,
                Is.EqualTo(24f));
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.Balanced)
                .Rendering.ShadowDistance,
                Is.EqualTo(32f),
                "The balanced strategy camera needs the center wardstones and all " +
                "four opening combatants inside its two soft-shadow cascades.");
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.High)
                    .Rendering.ShadowDistance,
                Is.EqualTo(40f));
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.Balanced)
                    .Rendering.MainLightShadowmapResolution,
                Is.EqualTo(2048));
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.Balanced)
                    .Rendering.ShadowCascade2Split,
                Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.High)
                    .Rendering.ShadowCascade2Split,
                Is.EqualTo(0.55f).Within(0.0001f));
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.High)
                    .Rendering.MsaaSampleCount,
                Is.EqualTo(4));
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.High)
                    .Rendering.MainLightShadowmapResolution,
                Is.EqualTo(4096));
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.Performance)
                    .Rendering.SoftShadows,
                Is.False);
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.Balanced)
                    .Rendering.SoftShadows,
                Is.True);
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.Balanced)
                    .Rendering.AdditionalLightsMode,
                Is.EqualTo(MobileAdditionalLightsMode.PerPixel));
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.High)
                    .Rendering.AdditionalLightsMode,
                Is.EqualTo(MobileAdditionalLightsMode.PerPixel));
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.High)
                    .Rendering.MaxAdditionalLightsPerObject,
                Is.EqualTo(4));
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.Performance)
                    .Rendering.SupportsHdr,
                Is.False,
                "The low-bandwidth Performance tier must retain its LDR path.");
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.Balanced)
                    .Rendering.SupportsHdr,
                Is.True,
                "Balanced retains HDR for emissive ambience, bloom, and ACES.");
            Assert.That(
                MobileQualityContracts.Get(MobileQualityTier.High)
                    .Rendering.SupportsHdr,
                Is.True);
        }

        [Test]
        public void Contracts_ScalePresentationCostMonotonically()
        {
            MobileQualityContract performance =
                MobileQualityContracts.Get(MobileQualityTier.Performance);
            MobileQualityContract balanced =
                MobileQualityContracts.Get(MobileQualityTier.Balanced);
            MobileQualityContract high =
                MobileQualityContracts.Get(MobileQualityTier.High);

            Assert.That(
                performance.Rendering.RenderScale,
                Is.LessThan(balanced.Rendering.RenderScale));
            Assert.That(
                balanced.Rendering.RenderScale,
                Is.LessThan(high.Rendering.RenderScale));
            Assert.That(
                performance.Rendering.VisualEffectDensity,
                Is.LessThan(balanced.Rendering.VisualEffectDensity));
            Assert.That(
                balanced.Rendering.VisualEffectDensity,
                Is.LessThan(high.Rendering.VisualEffectDensity));
            Assert.That(
                performance.Rendering.ShadowDistance,
                Is.LessThan(balanced.Rendering.ShadowDistance));
            Assert.That(
                balanced.Rendering.ShadowDistance,
                Is.LessThan(high.Rendering.ShadowDistance));
            Assert.That(
                performance.Performance.MaximumBurstParticleCount,
                Is.LessThan(balanced.Performance.MaximumBurstParticleCount));
            Assert.That(
                balanced.Performance.MaximumBurstParticleCount,
                Is.LessThan(high.Performance.MaximumBurstParticleCount));
        }

        [TestCase(
            MobileQualityStartupOverride.UseCatalogDefault,
            MobileQualityTier.Balanced)]
        [TestCase(
            MobileQualityStartupOverride.Performance,
            MobileQualityTier.Performance)]
        [TestCase(
            MobileQualityStartupOverride.Balanced,
            MobileQualityTier.Balanced)]
        [TestCase(
            MobileQualityStartupOverride.High,
            MobileQualityTier.High)]
        public void Resolve_HonorsExplicitStartupOverride(
            MobileQualityStartupOverride startupOverride,
            MobileQualityTier expected)
        {
            MobileQualityTier actual = MobileQualityContracts.Resolve(
                MobileQualityTier.Balanced,
                startupOverride);

            Assert.That(actual, Is.EqualTo(expected));
        }
    }
}
