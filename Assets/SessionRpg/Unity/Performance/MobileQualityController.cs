using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Unity.Performance
{
    /// <summary>
    /// Selects rendering quality exactly once. It does not change simulation timing or outcomes.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    [DisallowMultipleComponent]
    public sealed class MobileQualityController : MonoBehaviour
    {
        [SerializeField] private MobileQualityCatalog _catalog;
        [SerializeField] private MobileQualityStartupOverride _startupOverride =
            MobileQualityStartupOverride.UseCatalogDefault;

        private bool _hasProgrammaticOverride;
        private MobileQualityTier _programmaticOverride;

        public event Action<MobileQualityContract> QualityApplied;

        public bool IsApplied { get; private set; }

        public MobileQualityTier ActiveTier { get; private set; } =
            MobileQualityContracts.DefaultTier;

        public MobileQualityContract ActiveContract { get; private set; } =
            MobileQualityContracts.Get(MobileQualityContracts.DefaultTier);

        private void Awake()
        {
            if (!TryApplyAtStartup(out string error))
            {
                Debug.LogError(error, this);
            }
        }

        /// <summary>
        /// Sets a one-session override before startup application. Quality cannot change mid-session.
        /// </summary>
        public bool TrySetExplicitOverride(MobileQualityTier tier)
        {
            if (IsApplied || !MobileQualityContracts.IsDefined(tier))
            {
                return false;
            }

            _programmaticOverride = tier;
            _hasProgrammaticOverride = true;
            return true;
        }

        public bool TryApplyAtStartup(out string error)
        {
            if (IsApplied)
            {
                error = string.Empty;
                return true;
            }

            if (_catalog == null)
            {
                error = "Mobile quality catalog is not assigned.";
                return false;
            }

            MobileQualityTier tier = _hasProgrammaticOverride
                ? _programmaticOverride
                : MobileQualityContracts.Resolve(_catalog.DefaultTier, _startupOverride);
            if (!_catalog.TryGetPipeline(tier, out RenderPipelineAsset pipeline))
            {
                error = $"Mobile quality catalog has no render pipeline for tier '{tier}'.";
                return false;
            }

            MobileQualityContract contract = MobileQualityContracts.Get(tier);
            QualitySettings.renderPipeline = pipeline;
            QualitySettings.vSyncCount = 0;
            UnityEngine.Application.targetFrameRate = contract.Performance.TargetFrameRate;

            ActiveTier = tier;
            ActiveContract = contract;
            IsApplied = true;
            error = string.Empty;
            QualityApplied?.Invoke(contract);
            return true;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            MobileQualityCatalog catalog,
            MobileQualityStartupOverride startupOverride =
                MobileQualityStartupOverride.UseCatalogDefault)
        {
            _catalog = catalog;
            _startupOverride = startupOverride;
        }
#endif
    }
}
