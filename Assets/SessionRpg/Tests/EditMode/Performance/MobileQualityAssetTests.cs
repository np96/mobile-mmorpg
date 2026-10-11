using System;
using NUnit.Framework;
using SessionRpg.Editor.Performance;
using SessionRpg.Unity.Performance;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SessionRpg.Tests.Performance
{
    public sealed class MobileQualityAssetTests
    {
        [Test]
        public void ConfigurePipelineForTier_ProducesValidDistinctCatalog()
        {
            MobileQualityTestAssets assets = MobileQualityTestAssets.Create();
            try
            {
                Assert.DoesNotThrow(() =>
                    MobileQualityAssetValidator.ValidateOrThrow(assets.Catalog));
                Assert.That(
                    assets.Performance.renderScale,
                    Is.EqualTo(0.78f).Within(0.0001f));
                Assert.That(
                    assets.Balanced.additionalLightsRenderingMode,
                    Is.EqualTo(LightRenderingMode.PerPixel));
                Assert.That(
                    assets.High.additionalLightsRenderingMode,
                    Is.EqualTo(LightRenderingMode.PerPixel));
                Assert.That(assets.Performance.supportsSoftShadows, Is.False);
                Assert.That(assets.Balanced.supportsSoftShadows, Is.True);
                Assert.That(assets.High.supportsSoftShadows, Is.True);
                Assert.That(assets.Performance.supportsHDR, Is.False);
                Assert.That(assets.Balanced.supportsHDR, Is.True);
                Assert.That(assets.High.supportsHDR, Is.True);
                Assert.That(assets.Performance.shadowDistance, Is.EqualTo(24f));
                Assert.That(assets.Balanced.shadowDistance, Is.EqualTo(32f));
                Assert.That(assets.High.shadowDistance, Is.EqualTo(40f));
                Assert.That(assets.Balanced.shadowCascadeCount, Is.EqualTo(2));
                Assert.That(assets.High.shadowCascadeCount, Is.EqualTo(2));
                Assert.That(assets.Balanced.cascade2Split, Is.EqualTo(0.6f));
                Assert.That(assets.High.cascade2Split, Is.EqualTo(0.55f));
                Assert.That(
                    assets.Balanced.mainLightShadowmapResolution,
                    Is.EqualTo(2048));
                Assert.That(assets.High.msaaSampleCount, Is.EqualTo(4));
                Assert.That(
                    assets.High.mainLightShadowmapResolution,
                    Is.EqualTo(4096));
                Assert.That(assets.High.maxAdditionalLightsCount, Is.EqualTo(4));
                Assert.That(
                    assets.Balanced.upscalingFilter,
                    Is.EqualTo(UpscalingFilterSelection.FSR));
                Assert.That(assets.Balanced.fsrOverrideSharpness, Is.True);
                Assert.That(assets.Balanced.fsrSharpness, Is.EqualTo(0.72f));
                Assert.That(assets.Renderer.postProcessData, Is.Not.Null);
            }
            finally
            {
                assets.Dispose();
            }
        }

        [Test]
        public void Validator_RejectsPipelineReuseAcrossTiers()
        {
            MobileQualityTestAssets assets = MobileQualityTestAssets.Create();
            try
            {
                assets.Catalog.ConfigureForAuthoring(
                    assets.Performance,
                    assets.Performance,
                    assets.High,
                    MobileQualityTier.High);

                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => MobileQualityAssetValidator.ValidateOrThrow(assets.Catalog));
                StringAssert.Contains("distinct URP asset", exception.Message);
            }
            finally
            {
                assets.Dispose();
            }
        }

        [Test]
        public void Controller_AppliesExplicitOverrideOnce()
        {
            MobileQualityTestAssets assets = MobileQualityTestAssets.Create();
            RenderPipelineAsset previousPipeline = QualitySettings.renderPipeline;
            int previousVSync = QualitySettings.vSyncCount;
            int previousFrameRate = UnityEngine.Application.targetFrameRate;
            GameObject root = new GameObject("MobileQualityControllerTest");
            root.SetActive(false);
            MobileQualityController controller = root.AddComponent<MobileQualityController>();

            try
            {
                controller.ConfigureForAuthoring(assets.Catalog);
                Assert.That(
                    controller.TrySetExplicitOverride(MobileQualityTier.High),
                    Is.True);
                Assert.That(controller.TryApplyAtStartup(out string error), Is.True, error);
                Assert.That(controller.ActiveTier, Is.EqualTo(MobileQualityTier.High));
                Assert.That(QualitySettings.renderPipeline, Is.SameAs(assets.High));
                Assert.That(UnityEngine.Application.targetFrameRate, Is.EqualTo(60));
                Assert.That(QualitySettings.vSyncCount, Is.Zero);
                Assert.That(
                    controller.TrySetExplicitOverride(MobileQualityTier.Performance),
                    Is.False);
                Assert.That(controller.TryApplyAtStartup(out error), Is.True, error);
                Assert.That(controller.ActiveTier, Is.EqualTo(MobileQualityTier.High));
            }
            finally
            {
                QualitySettings.renderPipeline = previousPipeline;
                QualitySettings.vSyncCount = previousVSync;
                UnityEngine.Application.targetFrameRate = previousFrameRate;
                UnityEngine.Object.DestroyImmediate(root);
                assets.Dispose();
            }
        }

        private sealed class MobileQualityTestAssets : IDisposable
        {
            private MobileQualityTestAssets()
            {
                Renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                PostProcess = ScriptableObject.CreateInstance<PostProcessData>();
                Renderer.postProcessData = PostProcess;
                Performance = UniversalRenderPipelineAsset.Create(Renderer);
                Balanced = UniversalRenderPipelineAsset.Create(Renderer);
                High = UniversalRenderPipelineAsset.Create(Renderer);
                Catalog = ScriptableObject.CreateInstance<MobileQualityCatalog>();

                MobileQualityAssetAuthoring.ConfigurePipelineForTier(
                    Performance,
                    Renderer,
                    MobileQualityTier.Performance);
                MobileQualityAssetAuthoring.ConfigurePipelineForTier(
                    Balanced,
                    Renderer,
                    MobileQualityTier.Balanced);
                MobileQualityAssetAuthoring.ConfigurePipelineForTier(
                    High,
                    Renderer,
                    MobileQualityTier.High);
                Catalog.ConfigureForAuthoring(
                    Performance,
                    Balanced,
                    High,
                    MobileQualityTier.High);
            }

            public UniversalRendererData Renderer { get; }

            public PostProcessData PostProcess { get; }

            public UniversalRenderPipelineAsset Performance { get; }

            public UniversalRenderPipelineAsset Balanced { get; }

            public UniversalRenderPipelineAsset High { get; }

            public MobileQualityCatalog Catalog { get; }

            public static MobileQualityTestAssets Create()
            {
                return new MobileQualityTestAssets();
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(Catalog);
                UnityEngine.Object.DestroyImmediate(Performance);
                UnityEngine.Object.DestroyImmediate(Balanced);
                UnityEngine.Object.DestroyImmediate(High);
                UnityEngine.Object.DestroyImmediate(Renderer);
                UnityEngine.Object.DestroyImmediate(PostProcess);
            }
        }
    }
}
