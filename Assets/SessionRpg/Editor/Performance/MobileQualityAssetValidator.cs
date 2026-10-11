using System;
using System.Collections.Generic;
using System.Text;
using SessionRpg.Unity.Performance;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SessionRpg.Editor.Performance
{
    public static class MobileQualityAssetValidator
    {
        public static void ValidateOrThrow(MobileQualityCatalog catalog)
        {
            IReadOnlyList<string> errors = CollectErrors(catalog);
            if (errors.Count == 0)
            {
                return;
            }

            StringBuilder message = new StringBuilder(
                "Mobile quality foundation validation failed:");
            for (int index = 0; index < errors.Count; index++)
            {
                message.Append("\n - ");
                message.Append(errors[index]);
            }

            throw new InvalidOperationException(message.ToString());
        }

        public static IReadOnlyList<string> CollectErrors(MobileQualityCatalog catalog)
        {
            List<string> errors = new List<string>();
            if (catalog == null)
            {
                errors.Add("MobileQualityCatalog is missing.");
                return errors;
            }

            if (catalog.DefaultTier != MobileQualityContracts.DefaultTier)
            {
                errors.Add(
                    "Catalog default tier must be " +
                    MobileQualityContracts.DefaultTier + ", but is " +
                    catalog.DefaultTier + ".");
            }

            RenderPipelineAsset performanceReference = catalog.PerformancePipeline;
            RenderPipelineAsset balancedReference = catalog.BalancedPipeline;
            RenderPipelineAsset highReference = catalog.HighPipeline;
            ValidateDistinctReferences(
                performanceReference,
                balancedReference,
                highReference,
                errors);

            UniversalRendererData performanceRenderer = ValidateTier(
                MobileQualityTier.Performance,
                performanceReference,
                errors);
            UniversalRendererData balancedRenderer = ValidateTier(
                MobileQualityTier.Balanced,
                balancedReference,
                errors);
            UniversalRendererData highRenderer = ValidateTier(
                MobileQualityTier.High,
                highReference,
                errors);

            if (performanceRenderer != null && balancedRenderer != null &&
                performanceRenderer != balancedRenderer)
            {
                errors.Add("Performance and Balanced pipelines must share one renderer asset.");
            }

            if (balancedRenderer != null && highRenderer != null &&
                balancedRenderer != highRenderer)
            {
                errors.Add("Balanced and High pipelines must share one renderer asset.");
            }

            ValidateRenderer(performanceRenderer ?? balancedRenderer ?? highRenderer, errors);
            return errors;
        }

        private static void ValidateDistinctReferences(
            RenderPipelineAsset performance,
            RenderPipelineAsset balanced,
            RenderPipelineAsset high,
            List<string> errors)
        {
            if (performance == null)
            {
                errors.Add("Performance pipeline is not assigned.");
            }

            if (balanced == null)
            {
                errors.Add("Balanced pipeline is not assigned.");
            }

            if (high == null)
            {
                errors.Add("High pipeline is not assigned.");
            }

            if (performance != null &&
                (performance == balanced || performance == high))
            {
                errors.Add("Performance pipeline must be a distinct URP asset.");
            }

            if (balanced != null && balanced == high)
            {
                errors.Add("Balanced and High pipelines must be distinct URP assets.");
            }
        }

        private static UniversalRendererData ValidateTier(
            MobileQualityTier tier,
            RenderPipelineAsset pipelineReference,
            List<string> errors)
        {
            if (pipelineReference == null)
            {
                return null;
            }

            UniversalRenderPipelineAsset pipeline =
                pipelineReference as UniversalRenderPipelineAsset;
            if (pipeline == null)
            {
                errors.Add(tier + " pipeline is not a UniversalRenderPipelineAsset.");
                return null;
            }

            MobileRenderSettings expected = MobileQualityContracts.Get(tier).Rendering;
            RequireApproximately(
                tier,
                "render scale",
                pipeline.renderScale,
                expected.RenderScale,
                errors);
            RequireEqual(
                tier,
                "MSAA sample count",
                pipeline.msaaSampleCount,
                expected.MsaaSampleCount,
                errors);
            RequireEqual(
                tier,
                "main shadow resolution",
                pipeline.mainLightShadowmapResolution,
                expected.MainLightShadowmapResolution,
                errors);
            RequireApproximately(
                tier,
                "shadow distance",
                pipeline.shadowDistance,
                expected.ShadowDistance,
                errors);
            RequireEqual(
                tier,
                "shadow cascade count",
                pipeline.shadowCascadeCount,
                expected.ShadowCascadeCount,
                errors);
            RequireApproximately(
                tier,
                "two-cascade split",
                pipeline.cascade2Split,
                expected.ShadowCascade2Split,
                errors);
            RequireEqual(
                tier,
                "additional light mode",
                pipeline.additionalLightsRenderingMode,
                ToUrpLightMode(expected.AdditionalLightsMode),
                errors);
            RequireEqual(
                tier,
                "additional lights per object",
                pipeline.maxAdditionalLightsCount,
                expected.MaxAdditionalLightsPerObject,
                errors);

            RequireEqual(
                tier,
                "HDR support",
                pipeline.supportsHDR,
                expected.SupportsHdr,
                errors);
            RequireFalse(
                tier,
                "camera depth texture",
                pipeline.supportsCameraDepthTexture,
                errors);
            RequireFalse(
                tier,
                "camera opaque texture",
                pipeline.supportsCameraOpaqueTexture,
                errors);
            RequireTrue(tier, "main light shadows", pipeline.supportsMainLightShadows, errors);
            RequireFalse(
                tier,
                "additional light shadows",
                pipeline.supportsAdditionalLightShadows,
                errors);
            RequireEqual(
                tier,
                "soft shadows",
                pipeline.supportsSoftShadows,
                expected.SoftShadows,
                errors);
            RequireFalse(tier, "mixed lighting", pipeline.supportsMixedLighting, errors);
            RequireFalse(tier, "light cookies", pipeline.supportsLightCookies, errors);
            RequireFalse(
                tier,
                "adaptive performance without a provider",
                pipeline.useAdaptivePerformance,
                errors);
            RequireTrue(tier, "SRP Batcher", pipeline.useSRPBatcher, errors);
            RequireEqual(
                tier,
                "upscaling filter",
                pipeline.upscalingFilter,
                expected.RenderScale < 0.999f
                    ? UpscalingFilterSelection.FSR
                    : UpscalingFilterSelection.Auto,
                errors);
            RequireEqual(
                tier,
                "FSR sharpening override",
                pipeline.fsrOverrideSharpness,
                expected.RenderScale < 0.999f,
                errors);
            RequireApproximately(
                tier,
                "FSR sharpness",
                pipeline.fsrSharpness,
                0.72f,
                errors);

            SerializedObject serializedPipeline = new SerializedObject(pipeline);
            SerializedProperty rendererList =
                serializedPipeline.FindProperty("m_RendererDataList");
            if (rendererList == null || rendererList.arraySize != 1)
            {
                errors.Add(tier + " pipeline must reference exactly one renderer asset.");
                return null;
            }

            UniversalRendererData renderer = rendererList
                .GetArrayElementAtIndex(0)
                .objectReferenceValue as UniversalRendererData;
            if (renderer == null)
            {
                errors.Add(tier + " pipeline renderer must be UniversalRendererData.");
            }

            return renderer;
        }

        private static void ValidateRenderer(
            UniversalRendererData renderer,
            List<string> errors)
        {
            if (renderer == null)
            {
                return;
            }

            if (renderer.intermediateTextureMode != IntermediateTextureMode.Auto)
            {
                errors.Add("Shared mobile renderer intermediate texture mode must be Auto.");
            }

            if (renderer.postProcessData == null)
            {
                errors.Add(
                    "Shared mobile renderer must retain URP post-process resources.");
            }

            if (renderer.rendererFeatures.Count > 0)
            {
                errors.Add(
                    "Shared mobile renderer must not contain renderer features before they are " +
                    "individually profiled and budgeted.");
            }
        }

        private static LightRenderingMode ToUrpLightMode(
            MobileAdditionalLightsMode mode)
        {
            switch (mode)
            {
                case MobileAdditionalLightsMode.Disabled:
                    return LightRenderingMode.Disabled;
                case MobileAdditionalLightsMode.PerVertex:
                    return LightRenderingMode.PerVertex;
                case MobileAdditionalLightsMode.PerPixel:
                    return LightRenderingMode.PerPixel;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }
        }

        private static void RequireApproximately(
            MobileQualityTier tier,
            string label,
            float actual,
            float expected,
            List<string> errors)
        {
            if (!Mathf.Approximately(actual, expected))
            {
                errors.Add(
                    tier + " " + label + " must be " + expected +
                    ", but is " + actual + ".");
            }
        }

        private static void RequireEqual<T>(
            MobileQualityTier tier,
            string label,
            T actual,
            T expected,
            List<string> errors)
        {
            if (!EqualityComparer<T>.Default.Equals(actual, expected))
            {
                errors.Add(
                    tier + " " + label + " must be " + expected +
                    ", but is " + actual + ".");
            }
        }

        private static void RequireTrue(
            MobileQualityTier tier,
            string label,
            bool value,
            List<string> errors)
        {
            if (!value)
            {
                errors.Add(tier + " pipeline must enable " + label + ".");
            }
        }

        private static void RequireFalse(
            MobileQualityTier tier,
            string label,
            bool value,
            List<string> errors)
        {
            if (value)
            {
                errors.Add(tier + " pipeline must disable " + label + ".");
            }
        }
    }
}
