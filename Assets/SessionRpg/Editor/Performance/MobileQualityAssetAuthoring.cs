using System;
using SessionRpg.Unity.Performance;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace SessionRpg.Editor.Performance
{
    public readonly struct MobileQualityAssetSet
    {
        public MobileQualityAssetSet(
            UniversalRendererData renderer,
            UniversalRenderPipelineAsset performancePipeline,
            UniversalRenderPipelineAsset balancedPipeline,
            UniversalRenderPipelineAsset highPipeline,
            MobileQualityCatalog catalog)
        {
            Renderer = renderer;
            PerformancePipeline = performancePipeline;
            BalancedPipeline = balancedPipeline;
            HighPipeline = highPipeline;
            Catalog = catalog;
        }

        public UniversalRendererData Renderer { get; }

        public UniversalRenderPipelineAsset PerformancePipeline { get; }

        public UniversalRenderPipelineAsset BalancedPipeline { get; }

        public UniversalRenderPipelineAsset HighPipeline { get; }

        public MobileQualityCatalog Catalog { get; }
    }

    public static class MobileQualityAssetAuthoring
    {
        public const string DefaultSettingsFolder =
            "Assets/SessionRpg/Settings/MobileQuality";

        public const string DefaultCatalogPath =
            DefaultSettingsFolder + "/SessionMobileQualityCatalog.asset";

        public const string DefaultRendererPath =
            DefaultSettingsFolder + "/SessionMobileRenderer.asset";

        public const string PerformancePipelinePath =
            DefaultSettingsFolder + "/SessionMobilePipeline-Performance.asset";

        public const string BalancedPipelinePath =
            DefaultSettingsFolder + "/SessionMobilePipeline-Balanced.asset";

        public const string HighPipelinePath =
            DefaultSettingsFolder + "/SessionMobilePipeline-High.asset";

        private const string DefaultPostProcessDataPath =
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/" +
            "PostProcessData.asset";

        public const string CreateMenuPath =
            "Tools/Session RPG/Performance/Create or Update Mobile Quality Assets";

        public const string ValidateMenuPath =
            "Tools/Session RPG/Performance/Validate Mobile Quality Assets";

        [MenuItem(CreateMenuPath, priority = 2150)]
        public static void CreateOrUpdateFromMenu()
        {
            MobileQualityAssetSet assets = CreateOrUpdateDefaultAssets();
            MobileQualityAssetValidator.ValidateOrThrow(assets.Catalog);
            Selection.activeObject = assets.Catalog;
            Debug.Log("Session RPG mobile quality assets are authored and valid.");
        }

        [MenuItem(ValidateMenuPath, priority = 2151)]
        public static void ValidateFromMenu()
        {
            ValidateDefaultAssetsOrThrow();
            Debug.Log("Session RPG mobile quality assets are valid.");
        }

        /// <summary>
        /// Idempotently creates the dedicated renderer, three distinct URP assets, and catalog.
        /// </summary>
        public static MobileQualityAssetSet CreateOrUpdateDefaultAssets()
        {
            return CreateOrUpdateAssets(DefaultSettingsFolder);
        }

        public static MobileQualityAssetSet CreateOrUpdateAssets(string settingsFolder)
        {
            string normalizedFolder = NormalizeAssetFolder(settingsFolder);
            EnsureAssetFolder(normalizedFolder);

            string rendererPath = normalizedFolder + "/SessionMobileRenderer.asset";
            string performancePath =
                normalizedFolder + "/SessionMobilePipeline-Performance.asset";
            string balancedPath =
                normalizedFolder + "/SessionMobilePipeline-Balanced.asset";
            string highPath = normalizedFolder + "/SessionMobilePipeline-High.asset";
            string catalogPath =
                normalizedFolder + "/SessionMobileQualityCatalog.asset";

            UniversalRendererData renderer = EnsureRenderer(rendererPath);
            UniversalRenderPipelineAsset performancePipeline = EnsurePipeline(
                performancePath,
                "SessionMobilePipeline-Performance",
                renderer,
                MobileQualityTier.Performance);
            UniversalRenderPipelineAsset balancedPipeline = EnsurePipeline(
                balancedPath,
                "SessionMobilePipeline-Balanced",
                renderer,
                MobileQualityTier.Balanced);
            UniversalRenderPipelineAsset highPipeline = EnsurePipeline(
                highPath,
                "SessionMobilePipeline-High",
                renderer,
                MobileQualityTier.High);
            MobileQualityCatalog catalog = EnsureCatalog(catalogPath);
            catalog.ConfigureForAuthoring(
                performancePipeline,
                balancedPipeline,
                highPipeline,
                MobileQualityContracts.DefaultTier);

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();

            MobileQualityAssetSet result = new MobileQualityAssetSet(
                renderer,
                performancePipeline,
                balancedPipeline,
                highPipeline,
                catalog);
            MobileQualityAssetValidator.ValidateOrThrow(catalog);
            return result;
        }

        public static void ConfigurePipelineForTier(
            UniversalRenderPipelineAsset pipeline,
            UniversalRendererData renderer,
            MobileQualityTier tier)
        {
            if (pipeline == null)
            {
                throw new ArgumentNullException(nameof(pipeline));
            }

            if (renderer == null)
            {
                throw new ArgumentNullException(nameof(renderer));
            }

            MobileQualityContract contract = MobileQualityContracts.Get(tier);
            MobileRenderSettings rendering = contract.Rendering;

            renderer.intermediateTextureMode = IntermediateTextureMode.Auto;
            pipeline.supportsCameraDepthTexture = false;
            pipeline.supportsCameraOpaqueTexture = false;
            pipeline.supportsHDR = rendering.SupportsHdr;
            pipeline.msaaSampleCount = rendering.MsaaSampleCount;
            pipeline.renderScale = rendering.RenderScale;
            pipeline.mainLightShadowmapResolution =
                rendering.MainLightShadowmapResolution;
            pipeline.shadowDistance = rendering.ShadowDistance;
            pipeline.shadowCascadeCount = rendering.ShadowCascadeCount;
            pipeline.cascade2Split = rendering.ShadowCascade2Split;
            pipeline.maxAdditionalLightsCount =
                rendering.MaxAdditionalLightsPerObject;
            pipeline.upscalingFilter = rendering.RenderScale < 0.999f
                ? UpscalingFilterSelection.FSR
                : UpscalingFilterSelection.Auto;
            pipeline.fsrOverrideSharpness = rendering.RenderScale < 0.999f;
            pipeline.fsrSharpness = 0.72f;
            pipeline.useSRPBatcher = true;
            pipeline.useAdaptivePerformance = false;

            SerializedObject serializedPipeline = new SerializedObject(pipeline);
            SetBoolean(serializedPipeline, "m_MainLightShadowsSupported", true);
            SetInteger(
                serializedPipeline,
                "m_MainLightRenderingMode",
                (int)LightRenderingMode.PerPixel);
            SetInteger(
                serializedPipeline,
                "m_AdditionalLightsRenderingMode",
                (int)ToUrpLightMode(rendering.AdditionalLightsMode));
            SetBoolean(serializedPipeline, "m_AdditionalLightShadowsSupported", false);
            SetBoolean(
                serializedPipeline,
                "m_SoftShadowsSupported",
                rendering.SoftShadows);
            SetBoolean(serializedPipeline, "m_MixedLightingSupported", false);
            SetBoolean(serializedPipeline, "m_SupportsLightCookies", false);
            SetBoolean(serializedPipeline, "m_SupportsDynamicBatching", false);
            SetBoolean(serializedPipeline, "m_SupportsLightLayers", false);
            SetRenderer(serializedPipeline, renderer);
            serializedPipeline.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
        }

        public static void ValidateDefaultAssetsOrThrow()
        {
            MobileQualityCatalog catalog =
                AssetDatabase.LoadAssetAtPath<MobileQualityCatalog>(DefaultCatalogPath);
            if (catalog == null)
            {
                throw new InvalidOperationException(
                    "Mobile quality catalog is missing at '" + DefaultCatalogPath +
                    "'. Run " + CreateMenuPath + ".");
            }

            MobileQualityAssetValidator.ValidateOrThrow(catalog);
        }

        private static UniversalRendererData EnsureRenderer(string path)
        {
            UniversalRendererData renderer =
                AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (renderer == null)
            {
                RequirePathAvailable(path);
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.name = "SessionMobileRenderer";
                renderer.intermediateTextureMode = IntermediateTextureMode.Auto;
                AssetDatabase.CreateAsset(renderer, path);
            }

            renderer.intermediateTextureMode = IntermediateTextureMode.Auto;
            renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(
                DefaultPostProcessDataPath);
            if (renderer.postProcessData == null)
            {
                throw new InvalidOperationException(
                    "URP post-process resources are missing at '" +
                    DefaultPostProcessDataPath + "'.");
            }
            EditorUtility.SetDirty(renderer);
            return renderer;
        }

        private static UniversalRenderPipelineAsset EnsurePipeline(
            string path,
            string assetName,
            UniversalRendererData renderer,
            MobileQualityTier tier)
        {
            UniversalRenderPipelineAsset pipeline =
                AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
            if (pipeline == null)
            {
                RequirePathAvailable(path);
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = assetName;
                AssetDatabase.CreateAsset(pipeline, path);
            }

            ConfigurePipelineForTier(pipeline, renderer, tier);
            return pipeline;
        }

        private static MobileQualityCatalog EnsureCatalog(string path)
        {
            MobileQualityCatalog catalog =
                AssetDatabase.LoadAssetAtPath<MobileQualityCatalog>(path);
            if (catalog != null)
            {
                return catalog;
            }

            RequirePathAvailable(path);
            catalog = ScriptableObject.CreateInstance<MobileQualityCatalog>();
            catalog.name = "SessionMobileQualityCatalog";
            AssetDatabase.CreateAsset(catalog, path);
            return catalog;
        }

        private static void RequirePathAvailable(string path)
        {
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(path);
            if (existing != null)
            {
                throw new InvalidOperationException(
                    "Asset path '" + path + "' contains " + existing.GetType().Name +
                    ", but the mobile quality foundation requires another asset type.");
            }
        }

        private static string NormalizeAssetFolder(string settingsFolder)
        {
            if (string.IsNullOrWhiteSpace(settingsFolder))
            {
                throw new ArgumentException(
                    "A non-empty project-relative settings folder is required.",
                    nameof(settingsFolder));
            }

            string normalized = settingsFolder.Replace('\\', '/').TrimEnd('/');
            if (!normalized.Equals("Assets", StringComparison.Ordinal) &&
                !normalized.StartsWith("Assets/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "Mobile quality assets must be authored below the Assets folder.",
                    nameof(settingsFolder));
            }

            return normalized;
        }

        private static void EnsureAssetFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] segments = folder.Split('/');
            string parent = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string childPath = parent + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(childPath))
                {
                    AssetDatabase.CreateFolder(parent, segments[index]);
                }

                parent = childPath;
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

        private static void SetRenderer(
            SerializedObject serializedPipeline,
            UniversalRendererData renderer)
        {
            SerializedProperty rendererList = RequireProperty(
                serializedPipeline,
                "m_RendererDataList");
            rendererList.arraySize = 1;
            rendererList.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            SetInteger(serializedPipeline, "m_DefaultRendererIndex", 0);
        }

        private static void SetBoolean(
            SerializedObject serializedObject,
            string propertyName,
            bool value)
        {
            RequireProperty(serializedObject, propertyName).boolValue = value;
        }

        private static void SetInteger(
            SerializedObject serializedObject,
            string propertyName,
            int value)
        {
            RequireProperty(serializedObject, propertyName).intValue = value;
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serializedObject,
            string propertyName)
        {
            SerializedProperty property = serializedObject.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    "URP " + serializedObject.targetObject.GetType().Name +
                    " no longer exposes serialized property '" + propertyName +
                    "'. Update the mobile quality authoring adapter for this URP version.");
            }

            return property;
        }
    }
}
