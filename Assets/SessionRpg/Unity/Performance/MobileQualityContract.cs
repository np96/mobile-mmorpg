using System;

namespace SessionRpg.Unity.Performance
{
    public enum MobileQualityTier
    {
        Performance = 0,
        Balanced = 1,
        High = 2,
    }

    public enum MobileAdditionalLightsMode
    {
        Disabled = 0,
        PerVertex = 1,
        PerPixel = 2,
    }

    public enum MobileQualityStartupOverride
    {
        UseCatalogDefault = 0,
        Performance = 1,
        Balanced = 2,
        High = 3,
    }

    public readonly struct MobileRenderSettings
    {
        public MobileRenderSettings(
            float renderScale,
            int msaaSampleCount,
            int mainLightShadowmapResolution,
            float shadowDistance,
            int shadowCascadeCount,
            float shadowCascade2Split,
            bool softShadows,
            MobileAdditionalLightsMode additionalLightsMode,
            int maxAdditionalLightsPerObject,
            bool supportsHdr,
            float visualEffectDensity)
        {
            RenderScale = renderScale;
            MsaaSampleCount = msaaSampleCount;
            MainLightShadowmapResolution = mainLightShadowmapResolution;
            ShadowDistance = shadowDistance;
            ShadowCascadeCount = shadowCascadeCount;
            ShadowCascade2Split = shadowCascade2Split;
            SoftShadows = softShadows;
            AdditionalLightsMode = additionalLightsMode;
            MaxAdditionalLightsPerObject = maxAdditionalLightsPerObject;
            SupportsHdr = supportsHdr;
            VisualEffectDensity = visualEffectDensity;
        }

        public float RenderScale { get; }

        public int MsaaSampleCount { get; }

        public int MainLightShadowmapResolution { get; }

        public float ShadowDistance { get; }

        public int ShadowCascadeCount { get; }

        public float ShadowCascade2Split { get; }

        public bool SoftShadows { get; }

        public MobileAdditionalLightsMode AdditionalLightsMode { get; }

        public int MaxAdditionalLightsPerObject { get; }

        /// <summary>
        /// Enables the HDR lighting buffer used by emissive environment art,
        /// bloom, and filmic tone mapping. The performance tier keeps the
        /// lower-bandwidth LDR path.
        /// </summary>
        public bool SupportsHdr { get; }

        /// <summary>
        /// Presentation-only density multiplier. It must never affect combat events or telegraphs.
        /// </summary>
        public float VisualEffectDensity { get; }
    }

    public readonly struct MobilePerformanceBudget
    {
        public MobilePerformanceBudget(
            int targetFrameRate,
            float maximumFrameTimeMilliseconds,
            float maximumGpuTimeMilliseconds,
            float maximumMainThreadTimeMilliseconds,
            float maximumRenderThreadTimeMilliseconds,
            int maximumSteadyParticleCount,
            int maximumBurstParticleCount,
            int maximumNormalBatchCount,
            int maximumBurstBatchCount,
            int maximumNormalSetPassCount,
            int maximumBurstSetPassCount,
            int maximumFourPlayerTriangleCount,
            int maximumTenPlayerTriangleCount,
            int maximumFourPlayerSkinnedVertexCount,
            int maximumTenPlayerSkinnedVertexCount,
            int maximumResidentMemoryMiB,
            int maximumMatchMemoryGrowthMiB)
        {
            TargetFrameRate = targetFrameRate;
            MaximumFrameTimeMilliseconds = maximumFrameTimeMilliseconds;
            MaximumGpuTimeMilliseconds = maximumGpuTimeMilliseconds;
            MaximumMainThreadTimeMilliseconds = maximumMainThreadTimeMilliseconds;
            MaximumRenderThreadTimeMilliseconds = maximumRenderThreadTimeMilliseconds;
            MaximumSteadyParticleCount = maximumSteadyParticleCount;
            MaximumBurstParticleCount = maximumBurstParticleCount;
            MaximumNormalBatchCount = maximumNormalBatchCount;
            MaximumBurstBatchCount = maximumBurstBatchCount;
            MaximumNormalSetPassCount = maximumNormalSetPassCount;
            MaximumBurstSetPassCount = maximumBurstSetPassCount;
            MaximumFourPlayerTriangleCount = maximumFourPlayerTriangleCount;
            MaximumTenPlayerTriangleCount = maximumTenPlayerTriangleCount;
            MaximumFourPlayerSkinnedVertexCount = maximumFourPlayerSkinnedVertexCount;
            MaximumTenPlayerSkinnedVertexCount = maximumTenPlayerSkinnedVertexCount;
            MaximumResidentMemoryMiB = maximumResidentMemoryMiB;
            MaximumMatchMemoryGrowthMiB = maximumMatchMemoryGrowthMiB;
        }

        public int TargetFrameRate { get; }

        public float MaximumFrameTimeMilliseconds { get; }

        public float MaximumGpuTimeMilliseconds { get; }

        public float MaximumMainThreadTimeMilliseconds { get; }

        public float MaximumRenderThreadTimeMilliseconds { get; }

        public int MaximumSteadyParticleCount { get; }

        public int MaximumBurstParticleCount { get; }

        public int MaximumNormalBatchCount { get; }

        public int MaximumBurstBatchCount { get; }

        public int MaximumNormalSetPassCount { get; }

        public int MaximumBurstSetPassCount { get; }

        public int MaximumFourPlayerTriangleCount { get; }

        public int MaximumTenPlayerTriangleCount { get; }

        public int MaximumFourPlayerSkinnedVertexCount { get; }

        public int MaximumTenPlayerSkinnedVertexCount { get; }

        public int MaximumResidentMemoryMiB { get; }

        public int MaximumMatchMemoryGrowthMiB { get; }
    }

    public readonly struct MobileQualityContract
    {
        public MobileQualityContract(
            MobileQualityTier tier,
            MobileRenderSettings rendering,
            MobilePerformanceBudget performance)
        {
            Tier = tier;
            Rendering = rendering;
            Performance = performance;
        }

        public MobileQualityTier Tier { get; }

        public MobileRenderSettings Rendering { get; }

        public MobilePerformanceBudget Performance { get; }
    }

    /// <summary>
    /// Immutable mobile rendering contracts. Gameplay and simulation assemblies do not reference this type.
    /// </summary>
    public static class MobileQualityContracts
    {
        public const MobileQualityTier DefaultTier = MobileQualityTier.High;
        public const int TargetFrameRate = 60;

        private static readonly MobileQualityContract Performance = new MobileQualityContract(
            MobileQualityTier.Performance,
            new MobileRenderSettings(
                0.78f,
                1,
                1024,
                24f,
                1,
                0.25f,
                false,
                MobileAdditionalLightsMode.PerVertex,
                1,
                false,
                0.6f),
            new MobilePerformanceBudget(
                TargetFrameRate,
                16.67f,
                11f,
                8f,
                6f,
                450,
                900,
                120,
                180,
                80,
                120,
                160000,
                280000,
                65000,
                140000,
                350,
                5));

        private static readonly MobileQualityContract Balanced = new MobileQualityContract(
            MobileQualityTier.Balanced,
            new MobileRenderSettings(
                0.85f,
                2,
                2048,
                32f,
                2,
                0.6f,
                true,
                MobileAdditionalLightsMode.PerPixel,
                2,
                true,
                0.8f),
            new MobilePerformanceBudget(
                TargetFrameRate,
                16.67f,
                12f,
                8f,
                6f,
                600,
                1200,
                140,
                220,
                100,
                150,
                200000,
                350000,
                80000,
                180000,
                400,
                5));

        private static readonly MobileQualityContract High = new MobileQualityContract(
            MobileQualityTier.High,
            new MobileRenderSettings(
                1f,
                4,
                4096,
                40f,
                2,
                0.55f,
                true,
                MobileAdditionalLightsMode.PerPixel,
                4,
                true,
                1f),
            new MobilePerformanceBudget(
                TargetFrameRate,
                16.67f,
                13.5f,
                8f,
                6f,
                800,
                1500,
                160,
                240,
                110,
                165,
                240000,
                420000,
                100000,
                220000,
                450,
                5));

        public static bool IsDefined(MobileQualityTier tier)
        {
            return tier == MobileQualityTier.Performance ||
                tier == MobileQualityTier.Balanced ||
                tier == MobileQualityTier.High;
        }

        public static MobileQualityContract Get(MobileQualityTier tier)
        {
            switch (tier)
            {
                case MobileQualityTier.Performance:
                    return Performance;
                case MobileQualityTier.Balanced:
                    return Balanced;
                case MobileQualityTier.High:
                    return High;
                default:
                    throw new ArgumentOutOfRangeException(nameof(tier), tier, null);
            }
        }

        public static MobileQualityTier Resolve(
            MobileQualityTier catalogDefault,
            MobileQualityStartupOverride startupOverride)
        {
            switch (startupOverride)
            {
                case MobileQualityStartupOverride.UseCatalogDefault:
                    return IsDefined(catalogDefault) ? catalogDefault : DefaultTier;
                case MobileQualityStartupOverride.Performance:
                    return MobileQualityTier.Performance;
                case MobileQualityStartupOverride.Balanced:
                    return MobileQualityTier.Balanced;
                case MobileQualityStartupOverride.High:
                    return MobileQualityTier.High;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(startupOverride),
                        startupOverride,
                        null);
            }
        }
    }
}
