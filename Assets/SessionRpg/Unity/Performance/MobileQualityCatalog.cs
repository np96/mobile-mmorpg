using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Unity.Performance
{
    [CreateAssetMenu(
        fileName = "SessionMobileQualityCatalog",
        menuName = "Session RPG/Performance/Mobile Quality Catalog")]
    public sealed class MobileQualityCatalog : ScriptableObject
    {
        [SerializeField] private MobileQualityTier _defaultTier = MobileQualityTier.High;
        [SerializeField] private RenderPipelineAsset _performancePipeline;
        [SerializeField] private RenderPipelineAsset _balancedPipeline;
        [SerializeField] private RenderPipelineAsset _highPipeline;

        public MobileQualityTier DefaultTier => MobileQualityContracts.IsDefined(_defaultTier)
            ? _defaultTier
            : MobileQualityContracts.DefaultTier;

        public RenderPipelineAsset PerformancePipeline => _performancePipeline;

        public RenderPipelineAsset BalancedPipeline => _balancedPipeline;

        public RenderPipelineAsset HighPipeline => _highPipeline;

        public bool TryGetPipeline(
            MobileQualityTier tier,
            out RenderPipelineAsset pipeline)
        {
            switch (tier)
            {
                case MobileQualityTier.Performance:
                    pipeline = _performancePipeline;
                    break;
                case MobileQualityTier.Balanced:
                    pipeline = _balancedPipeline;
                    break;
                case MobileQualityTier.High:
                    pipeline = _highPipeline;
                    break;
                default:
                    pipeline = null;
                    return false;
            }

            return pipeline != null;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            RenderPipelineAsset performancePipeline,
            RenderPipelineAsset balancedPipeline,
            RenderPipelineAsset highPipeline,
            MobileQualityTier defaultTier = MobileQualityTier.High)
        {
            _performancePipeline = performancePipeline;
            _balancedPipeline = balancedPipeline;
            _highPipeline = highPipeline;
            _defaultTier = MobileQualityContracts.IsDefined(defaultTier)
                ? defaultTier
                : MobileQualityContracts.DefaultTier;
        }
#endif
    }
}
