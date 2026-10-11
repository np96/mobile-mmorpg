using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Assets
{
    public enum MixamoTrialTextureUsage
    {
        BaseColor,
        Normal,
        Specular,
        Emission,
    }

    public sealed class MixamoTrialTextureSpec
    {
        internal MixamoTrialTextureSpec(
            string key,
            string assetPath,
            string expectedSha256,
            long expectedBytes,
            int expectedWidth,
            int expectedHeight,
            MixamoTrialTextureUsage usage,
            bool usesAlpha)
        {
            Key = key;
            AssetPath = assetPath;
            ExpectedSha256 = expectedSha256;
            ExpectedBytes = expectedBytes;
            ExpectedWidth = expectedWidth;
            ExpectedHeight = expectedHeight;
            Usage = usage;
            UsesAlpha = usesAlpha;
        }

        public string Key { get; }

        public string AssetPath { get; }

        public string ExpectedSha256 { get; }

        public long ExpectedBytes { get; }

        public int ExpectedWidth { get; }

        public int ExpectedHeight { get; }

        public MixamoTrialTextureUsage Usage { get; }

        public bool UsesAlpha { get; }

        public bool IsNormal => Usage == MixamoTrialTextureUsage.Normal;

        public bool IsSrgb =>
            Usage == MixamoTrialTextureUsage.BaseColor ||
            Usage == MixamoTrialTextureUsage.Emission;
    }

    public sealed class MixamoTrialMaterialSpec
    {
        internal MixamoTrialMaterialSpec(
            string key,
            string sourceMaterialName,
            string generatedMaterialAssetPath,
            string baseColorAssetPath,
            string normalAssetPath,
            string specularAssetPath,
            string emissionAssetPath,
            bool alphaClip,
            bool doubleSided,
            Color tint,
            float metallic,
            float smoothness)
        {
            Key = key;
            SourceMaterialName = sourceMaterialName;
            GeneratedMaterialAssetPath = generatedMaterialAssetPath;
            BaseColorAssetPath = baseColorAssetPath;
            NormalAssetPath = normalAssetPath;
            SpecularAssetPath = specularAssetPath;
            EmissionAssetPath = emissionAssetPath;
            AlphaClip = alphaClip;
            DoubleSided = doubleSided;
            Tint = tint;
            Metallic = metallic;
            Smoothness = smoothness;
        }

        public string Key { get; }

        public string SourceMaterialName { get; }

        public string GeneratedMaterialAssetPath { get; }

        public string BaseColorAssetPath { get; }

        public string NormalAssetPath { get; }

        public string SpecularAssetPath { get; }

        public string EmissionAssetPath { get; }

        public bool AlphaClip { get; }

        public bool DoubleSided { get; }

        public Color Tint { get; }

        public float Metallic { get; }

        public float Smoothness { get; }
    }

    public sealed class MixamoTrialRendererSpec
    {
        private readonly string[] _materialKeysBySlot;

        internal MixamoTrialRendererSpec(
            string rendererName,
            params string[] materialKeysBySlot)
        {
            RendererName = rendererName;
            _materialKeysBySlot = materialKeysBySlot ?? Array.Empty<string>();
        }

        public string RendererName { get; }

        public IReadOnlyList<string> MaterialKeysBySlot => _materialKeysBySlot;
    }

    public sealed class MixamoPlayableClassTrialSpec
    {
        private readonly MixamoTrialTextureSpec[] _textures;
        private readonly MixamoTrialMaterialSpec[] _materials;
        private readonly MixamoTrialRendererSpec[] _renderers;

        internal MixamoPlayableClassTrialSpec(
            ClassId classId,
            string manifestClassId,
            string displayName,
            string assetFolder,
            string modelAssetPath,
            string immutableOriginalPath,
            string modelRootName,
            string expectedModelSha256,
            long expectedModelBytes,
            float visualScale,
            float groundOffset,
            int expectedRendererCount,
            int expectedTriangleCount,
            int expectedVertexCount,
            int expectedWeightedBoneCount,
            int expectedMaterialCount,
            float expectedModelHeight,
            MixamoTrialTextureSpec[] textures,
            MixamoTrialMaterialSpec[] materials,
            MixamoTrialRendererSpec[] renderers)
        {
            ClassId = classId;
            ManifestClassId = manifestClassId;
            DisplayName = displayName;
            AssetFolder = assetFolder;
            ModelAssetPath = modelAssetPath;
            ImmutableOriginalPath = immutableOriginalPath;
            ModelRootName = modelRootName;
            ExpectedModelSha256 = expectedModelSha256;
            ExpectedModelBytes = expectedModelBytes;
            VisualScale = visualScale;
            GroundOffset = groundOffset;
            ExpectedRendererCount = expectedRendererCount;
            ExpectedTriangleCount = expectedTriangleCount;
            ExpectedVertexCount = expectedVertexCount;
            ExpectedWeightedBoneCount = expectedWeightedBoneCount;
            ExpectedMaterialCount = expectedMaterialCount;
            ExpectedModelHeight = expectedModelHeight;
            _textures = textures ?? Array.Empty<MixamoTrialTextureSpec>();
            _materials = materials ?? Array.Empty<MixamoTrialMaterialSpec>();
            _renderers = renderers ?? Array.Empty<MixamoTrialRendererSpec>();
        }

        public ClassId ClassId { get; }

        public string ManifestClassId { get; }

        public string DisplayName { get; }

        public string AssetFolder { get; }

        public string ModelAssetPath { get; }

        public string ImmutableOriginalPath { get; }

        public string ExtractedTextureFolder => AssetFolder + "/ExtractedTextures";

        public string ModelRootName { get; }

        public string ExpectedModelSha256 { get; }

        public long ExpectedModelBytes { get; }

        public float VisualScale { get; }

        public float GroundOffset { get; }

        public int ExpectedRendererCount { get; }

        public int ExpectedTriangleCount { get; }

        public int ExpectedVertexCount { get; }

        public int ExpectedWeightedBoneCount { get; }

        public int ExpectedMaterialCount { get; }

        public float ExpectedModelHeight { get; }

        public IReadOnlyList<MixamoTrialTextureSpec> Textures => _textures;

        public IReadOnlyList<MixamoTrialMaterialSpec> Materials => _materials;

        public IReadOnlyList<MixamoTrialRendererSpec> Renderers => _renderers;

        public MixamoTrialMaterialSpec GetMaterialOrThrow(string key)
        {
            MixamoTrialMaterialSpec material = _materials.SingleOrDefault(value =>
                string.Equals(value.Key, key, StringComparison.Ordinal));
            if (material == null)
            {
                throw new InvalidOperationException(
                    "Mixamo trial '" + DisplayName +
                    "' has no authored material contract with key '" + key + "'.");
            }

            return material;
        }

        public bool TryGetRenderer(
            string rendererName,
            out MixamoTrialRendererSpec renderer)
        {
            renderer = _renderers.SingleOrDefault(value =>
                string.Equals(
                    value.RendererName,
                    rendererName,
                    StringComparison.Ordinal));
            return renderer != null;
        }
    }

    /// <summary>
    /// Shared import, integrity, generated-hierarchy, and build contract for the
    /// three reversible Mixamo playable-class licensed game bases. Historical
    /// Trial type/path names remain to preserve authored identities.
    /// </summary>
    public static class MixamoPlayableClassTrialAssetValidator
    {
        public const string AssetRoot =
            "Assets/ThirdParty/Trials/AdobeMixamo/PlayableClasses";

        public const string SourceRoot =
            "SourceArt/Trials/AdobeMixamo/PlayableClasses";

        public const string SourceManifestPath = SourceRoot + "/manifest.json";

        public const string ConfigureMenuPath =
            "Tools/Session RPG/Assets/Configure Mixamo Playable-Class Game-Base Imports";

        public const string ValidateMenuPath =
            "Tools/Session RPG/Assets/Validate Mixamo Playable-Class Game Bases";

        private const int ImportedMaximumTextureSize = 2048;
        private const float TransformTolerance = 0.0001f;
        private const float ModelHeightTolerance = 0.02f;

        private const string ConfigureInstruction =
            "Run Tools > Session RPG > Assets > Configure Mixamo Playable-Class Game-Base Imports once, then perform one authored rebuild.";

        private static readonly HumanBodyBones[] RequiredHumanoidBones =
        {
            HumanBodyBones.Hips,
            HumanBodyBones.Spine,
            HumanBodyBones.Chest,
            HumanBodyBones.Neck,
            HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm,
            HumanBodyBones.LeftLowerArm,
            HumanBodyBones.LeftHand,
            HumanBodyBones.RightUpperArm,
            HumanBodyBones.RightLowerArm,
            HumanBodyBones.RightHand,
            HumanBodyBones.LeftUpperLeg,
            HumanBodyBones.LeftLowerLeg,
            HumanBodyBones.LeftFoot,
            HumanBodyBones.RightUpperLeg,
            HumanBodyBones.RightLowerLeg,
            HumanBodyBones.RightFoot,
        };

        private static readonly MixamoPlayableClassTrialSpec[] TrialSpecs =
        {
            CreatePriestSpec(),
            CreateRogueSpec(),
            CreateMageSpec(),
        };

        public static IReadOnlyList<MixamoPlayableClassTrialSpec> Specs => TrialSpecs;

        [MenuItem(ValidateMenuPath, priority = 2088)]
        public static void ValidateFromMenu()
        {
            try
            {
                ValidateOrThrow();
                const string message =
                    "Mixamo playable-class evaluation sources, hashes, Humanoid rigs, textures, and geometry are valid.";
                Debug.Log(message);
                ShowDialog("Mixamo playable-class trial validation", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Mixamo playable-class trial validation failed", exception.Message);
            }
        }

        [MenuItem(ConfigureMenuPath, priority = 2089)]
        public static void ConfigureFromMenu()
        {
            try
            {
                int changedCount = ConfigureImport();
                ValidateOrThrow();
                string message = changedCount == 0
                    ? "Mixamo playable-class trial imports were already correct."
                    : "Configured and reimported " + changedCount +
                      " Mixamo playable-class trial asset(s).";
                Debug.Log(message);
                ShowDialog("Mixamo playable-class trial imports", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Mixamo playable-class trial import failed", exception.Message);
            }
        }

        public static void ValidateForCommandLine()
        {
            ValidateOrThrow();
            Debug.Log("Mixamo playable-class trial command-line validation passed.");
        }

        public static void ConfigureForCommandLine()
        {
            int changedCount = ConfigureImport();
            ValidateOrThrow();
            Debug.Log(
                "Mixamo playable-class trial import configuration passed; changed " +
                changedCount + " asset(s). Evaluation-only status remains enforced.");
        }

        // Temporary source inspection hook used only while locking a newly staged
        // model contract. It deliberately does not alter or accept runtime motion.
        public static void PrepareInspectionForCommandLine()
        {
            var errors = new List<string>();
            ValidateTrialBoundary(errors);
            ValidateImmutableFiles(errors);
            ThrowIfErrors(errors);

            int changedCount = 0;
            for (int index = 0; index < TrialSpecs.Length; index++)
            {
                MixamoPlayableClassTrialSpec spec = TrialSpecs[index];
                changedCount += ConfigureModelAndExtractTextures(spec);
                LogImportedStructure(spec);
            }

            AssetDatabase.SaveAssets();
            Debug.Log(
                "Mixamo playable-class inspection preparation complete; changed " +
                changedCount + " asset(s). No source was promoted.");
        }

        public static bool TryGetSpec(
            ClassId classId,
            out MixamoPlayableClassTrialSpec spec)
        {
            spec = TrialSpecs.SingleOrDefault(value => value.ClassId == classId);
            return spec != null;
        }

        public static MixamoPlayableClassTrialSpec GetSpecOrThrow(ClassId classId)
        {
            if (TryGetSpec(classId, out MixamoPlayableClassTrialSpec spec))
            {
                return spec;
            }

            throw new ArgumentOutOfRangeException(
                nameof(classId),
                classId,
                "Only Priest, Rogue, and Mage have Mixamo playable-class trial specifications.");
        }

        public static bool IsSelected(ClassId classId)
        {
            return TryGetSpec(classId, out _) &&
                PlayableClassTrialSourceSelection.UsesMixamoTrial(classId);
        }

        public static bool IsSelected(MixamoPlayableClassTrialSpec spec)
        {
            if (spec == null)
            {
                throw new ArgumentNullException(nameof(spec));
            }

            return IsSelected(spec.ClassId);
        }

        public static bool RequiresLicensedIntake(PlayableClassVisualSource source)
        {
            switch (source)
            {
                case PlayableClassVisualSource.QuaterniusPrototype:
                    return false;
                case PlayableClassVisualSource.MixamoLicensedGameBase:
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(source),
                        source,
                        "Unsupported playable-class visual source.");
            }
        }

        public static void ValidateSelectedSourcesOrThrow()
        {
            // A source-clean public checkout intentionally has no licensed
            // originals or staged Mixamo files. Only the explicit CC0 selection
            // bypasses this intake; licensed and unknown selections stay gated.
            bool requiresLicensedIntake = false;
            for (int index = 0; index < TrialSpecs.Length; index++)
            {
                // Inspect every selection, even after finding a licensed one,
                // so an unsupported value can never hide in a mixed selection.
                requiresLicensedIntake |= RequiresLicensedIntake(
                    PlayableClassTrialSourceSelection.Get(TrialSpecs[index].ClassId));
            }

            if (!requiresLicensedIntake)
            {
                return;
            }

            var errors = new List<string>();
            ValidateTrialBoundary(errors);
            ValidateImmutableFiles(errors);
            for (int index = 0; index < TrialSpecs.Length; index++)
            {
                MixamoPlayableClassTrialSpec spec = TrialSpecs[index];
                if (IsSelected(spec.ClassId))
                {
                    ValidateSpec(spec, errors);
                }
            }

            ThrowIfErrors(errors);
        }

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            ValidateTrialBoundary(errors);
            ValidateImmutableFiles(errors);
            for (int index = 0; index < TrialSpecs.Length; index++)
            {
                ValidateSpec(TrialSpecs[index], errors);
            }

            ValidateNoAnimationFbxInsideAssets(errors);
            ThrowIfErrors(errors);
        }

        public static int ConfigureImport()
        {
            var errors = new List<string>();
            ValidateTrialBoundary(errors);
            ValidateImmutableFiles(errors);
            ThrowIfErrors(errors);

            int changedCount = 0;
            for (int index = 0; index < TrialSpecs.Length; index++)
            {
                changedCount += ConfigureModelAndExtractTextures(TrialSpecs[index]);
            }

            AssetDatabase.SaveAssets();
            return changedCount;
        }

        public static GameObject LoadValidatedModelOrThrow(ClassId classId)
        {
            MixamoPlayableClassTrialSpec spec = GetSpecOrThrow(classId);
            var errors = new List<string>();
            ValidateTrialBoundary(errors);
            ValidateImmutableFiles(spec, errors);
            ValidateSpec(spec, errors);
            ThrowIfErrors(errors);

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                spec.ModelAssetPath);
            if (model == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the validated Mixamo trial model at '" +
                    spec.ModelAssetPath + "'.");
            }

            return model;
        }

        public static void ValidateGeneratedRootOrThrow(
            ClassId classId,
            GameObject trialRoot)
        {
            MixamoPlayableClassTrialSpec spec = GetSpecOrThrow(classId);
            var errors = new List<string>();
            if (trialRoot == null)
            {
                errors.Add(
                    "Generated " + spec.DisplayName + " Mixamo trial root is missing.");
                ThrowIfErrors(errors);
            }

            if (!string.Equals(trialRoot.name, spec.ModelRootName, StringComparison.Ordinal))
            {
                errors.Add(
                    "Generated " + spec.DisplayName + " root is named '" +
                    trialRoot.name + "'; expected '" + spec.ModelRootName + "'.");
            }

            Animator animator = trialRoot.GetComponent<Animator>();
            ValidateGeneratedAnimator(spec, animator, errors);

            Renderer[] renderers = trialRoot.GetComponentsInChildren<Renderer>(true);
            var rendererByName = BuildRendererMap(spec, renderers, errors, "generated");
            for (int rendererIndex = 0;
                 rendererIndex < spec.Renderers.Count;
                 rendererIndex++)
            {
                MixamoTrialRendererSpec rendererSpec = spec.Renderers[rendererIndex];
                if (!rendererByName.TryGetValue(
                        rendererSpec.RendererName,
                        out Renderer renderer))
                {
                    continue;
                }

                if (renderer.shadowCastingMode != ShadowCastingMode.On ||
                    !renderer.receiveShadows)
                {
                    errors.Add(
                        "Generated " + spec.DisplayName + " renderer '" +
                        renderer.name + "' must cast and receive shadows.");
                }

                if (renderer is SkinnedMeshRenderer skinned)
                {
                    if (skinned.sharedMesh == null || skinned.quality != SkinQuality.Bone4)
                    {
                        errors.Add(
                            "Generated " + spec.DisplayName + " renderer '" +
                            renderer.name + "' must retain its mesh and four-bone skinning.");
                    }
                }
                else if (renderer is MeshRenderer)
                {
                    MeshFilter filter = renderer.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null)
                    {
                        errors.Add(
                            "Generated " + spec.DisplayName + " rigid renderer '" +
                            renderer.name + "' must retain its source MeshFilter geometry.");
                    }
                }
                else
                {
                    errors.Add(
                        "Generated " + spec.DisplayName + " renderer '" +
                        renderer.name + "' has an unsupported renderer type.");
                }

                Material[] materials = renderer.sharedMaterials;
                if (materials.Length != rendererSpec.MaterialKeysBySlot.Count)
                {
                    errors.Add(
                        "Generated " + spec.DisplayName + " renderer '" +
                        renderer.name + "' exposes " + materials.Length +
                        " material slots; expected " +
                        rendererSpec.MaterialKeysBySlot.Count + ".");
                    continue;
                }

                for (int slot = 0; slot < materials.Length; slot++)
                {
                    MixamoTrialMaterialSpec materialSpec = spec.GetMaterialOrThrow(
                        rendererSpec.MaterialKeysBySlot[slot]);
                    Material material = materials[slot];
                    if (material == null || material.shader == null ||
                        !string.Equals(
                            material.shader.name,
                            "Universal Render Pipeline/Lit",
                            StringComparison.Ordinal))
                    {
                        errors.Add(
                            "Generated " + spec.DisplayName + " renderer '" +
                            renderer.name + "' slot " + slot +
                            " must use a non-null URP/Lit material.");
                        continue;
                    }

                    string materialPath = AssetDatabase.GetAssetPath(material);
                    if (!string.Equals(
                            materialPath,
                            materialSpec.GeneratedMaterialAssetPath,
                            StringComparison.Ordinal))
                    {
                        errors.Add(
                            "Generated " + spec.DisplayName + " renderer '" +
                            renderer.name + "' slot " + slot + " uses '" +
                            materialPath + "'; expected '" +
                            materialSpec.GeneratedMaterialAssetPath + "'.");
                    }
                }
            }

            var contractedRendererNames = new HashSet<string>(
                spec.Renderers.Select(value => value.RendererName),
                StringComparer.Ordinal);
            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                Renderer renderer = renderers[rendererIndex];
                if (contractedRendererNames.Contains(renderer.name))
                {
                    continue;
                }

                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                bool emptyRigidPlaceholder =
                    renderer is MeshRenderer &&
                    filter != null &&
                    filter.sharedMesh != null &&
                    filter.sharedMesh.vertexCount == 0;
                if (!emptyRigidPlaceholder || renderer.enabled)
                {
                    errors.Add(
                        "Generated " + spec.DisplayName + " has unexpected active renderer '" +
                        renderer.name + "'. Only disabled zero-vertex source placeholders may " +
                        "remain outside the locked visible renderer contract.");
                }
            }

            ThrowIfErrors(errors);
        }

        public static void ValidateGeneratedPrefabOrThrow(GameObject prefab)
        {
            var errors = new List<string>();
            if (prefab == null)
            {
                errors.Add("Generated Combatant prefab is missing.");
                ThrowIfErrors(errors);
            }

            CombatantView view = prefab.GetComponent<CombatantView>();
            CombatantAnimationPresenter presenter =
                prefab.GetComponent<CombatantAnimationPresenter>();
            if (view == null || presenter == null)
            {
                errors.Add(
                    "Generated Combatant prefab requires CombatantView and CombatantAnimationPresenter.");
                ThrowIfErrors(errors);
            }

            SerializedObject viewObject = new SerializedObject(view);
            SerializedObject presenterObject = new SerializedObject(presenter);
            for (int index = 0; index < TrialSpecs.Length; index++)
            {
                MixamoPlayableClassTrialSpec spec = TrialSpecs[index];
                string fallbackName = GetFallbackRootName(spec.ClassId);
                Transform trial = FindDirectChild(prefab.transform, spec.ModelRootName);
                Transform fallback = FindDirectChild(prefab.transform, fallbackName);
                int sourceCount = (trial != null ? 1 : 0) + (fallback != null ? 1 : 0);
                if (sourceCount != 1)
                {
                    errors.Add(
                        "Generated Combatant must contain exactly one " +
                        spec.DisplayName + " source root; found " + sourceCount + ".");
                    continue;
                }

                bool usesTrial = IsSelected(spec.ClassId);
                Transform expected = usesTrial ? trial : fallback;
                if (expected == null)
                {
                    errors.Add(
                        "Generated Combatant is missing selected " + spec.DisplayName +
                        " source root '" +
                        (usesTrial ? spec.ModelRootName : fallbackName) + "'.");
                    continue;
                }

                if (usesTrial)
                {
                    try
                    {
                        ValidateGeneratedRootOrThrow(spec.ClassId, expected.gameObject);
                    }
                    catch (InvalidOperationException exception)
                    {
                        errors.Add(exception.Message);
                    }
                }

                string modelField = GetViewModelField(spec.ClassId);
                SerializedProperty modelProperty = viewObject.FindProperty(modelField);
                if (modelProperty == null ||
                    modelProperty.objectReferenceValue != expected.gameObject)
                {
                    errors.Add(
                        "CombatantView." + modelField + " must reference selected root '" +
                        expected.name + "'.");
                }

                string animatorField = GetPresenterAnimatorField(spec.ClassId);
                SerializedProperty animatorProperty =
                    presenterObject.FindProperty(animatorField);
                Animator expectedAnimator = expected.GetComponent<Animator>();
                if (animatorProperty == null ||
                    animatorProperty.objectReferenceValue != expectedAnimator)
                {
                    errors.Add(
                        "CombatantAnimationPresenter." + animatorField +
                        " must reference the Animator on selected root '" +
                        expected.name + "'.");
                }
            }

            ThrowIfErrors(errors);
        }

        public static bool IsAnySelectedModelReferencedByEnabledBuildScenes()
        {
            string[] selectedPaths = TrialSpecs
                .Where(value => IsSelected(value.ClassId))
                .Select(value => value.ModelAssetPath)
                .ToArray();
            if (selectedPaths.Length == 0)
            {
                return false;
            }

            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int index = 0; index < scenes.Length; index++)
            {
                EditorBuildSettingsScene scene = scenes[index];
                if (!scene.enabled || string.IsNullOrEmpty(scene.path))
                {
                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(scene.path, true);
                if (dependencies.Any(value => selectedPaths.Contains(
                        value,
                        StringComparer.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }

        private static MixamoPlayableClassTrialSpec CreatePriestSpec()
        {
            const string folder = AssetRoot + "/Priest";
            string diffuse = folder + "/ExtractedTextures/Uriel_diffuse.png";
            string specular = folder + "/ExtractedTextures/Uriel_specular.png";
            return new MixamoPlayableClassTrialSpec(
                ClassId.Priest,
                "Healer",
                "Uriel A Plotexia",
                folder,
                folder + "/UrielAPlotexia.fbx",
                SourceRoot + "/Priest/Original/UrielAPlotexia.fbx",
                "PriestModel_MixamoUrielTrial",
                "aa42d2a3f3117a58105e6d0a357d017d94260e2d3c7d216d36ad103b0a47dbd1",
                13619504L,
                0.881121f,
                0.03f,
                22,
                11026,
                8096,
                72,
                1,
                2.179042f,
                new[]
                {
                    Texture(
                        folder,
                        "urielDiffuse",
                        "Uriel_diffuse.png",
                        "fc323839bb73dc7e86129a4f2ff01de5ce4abc657abdc60dbcf28ddbd78f93d2",
                        3921956L,
                        2048,
                        2048,
                        MixamoTrialTextureUsage.BaseColor),
                    Texture(
                        folder,
                        "urielSpecular",
                        "Uriel_specular.png",
                        "3a29b122996da6af823c83ca8316b180802728434372cfcfb3ecad97bc050d87",
                        2603273L,
                        2048,
                        2048,
                        MixamoTrialTextureUsage.Specular),
                },
                new[]
                {
                    Material(
                        "uriel",
                        "Uriel_MAT",
                        "Priest_MixamoUrielTrial_Uriel.mat",
                        diffuse,
                        string.Empty,
                        specular,
                        string.Empty,
                        false,
                        false,
                        Color.white,
                        0f,
                        0.38f),
                },
                new[]
                {
                    Renderer("polySurface1", "uriel"),
                    Renderer("polySurface3", "uriel"),
                    Renderer("polySurface4", "uriel"),
                    Renderer("polySurface5", "uriel"),
                    Renderer("polySurface6", "uriel"),
                    Renderer("polySurface7", "uriel"),
                    Renderer("polySurface8", "uriel"),
                    Renderer("polySurface9", "uriel"),
                    Renderer("polySurface12", "uriel"),
                    Renderer("polySurface13", "uriel"),
                    Renderer("polySurface14", "uriel"),
                    Renderer("polySurface15", "uriel"),
                    Renderer("polySurface16", "uriel"),
                    Renderer("polySurface17", "uriel"),
                    Renderer("polySurface18", "uriel"),
                    Renderer("polySurface19", "uriel"),
                    Renderer("polySurface20", "uriel"),
                    Renderer("polySurface21", "uriel"),
                    Renderer("polySurface22", "uriel"),
                    Renderer("polySurface23", "uriel"),
                    Renderer("polySurface24", "uriel"),
                    Renderer("polySurface26", "uriel"),
                });
        }

        private static MixamoPlayableClassTrialSpec CreateRogueSpec()
        {
            const string folder = AssetRoot + "/Rogue";
            string diffuse = folder + "/ExtractedTextures/Vampire_diffuse.png";
            string transparent =
                folder + "/ExtractedTextures/Vampire_diffuse_transparent.png";
            string emission = folder + "/ExtractedTextures/Vampire_emission.png";
            string normal = folder + "/ExtractedTextures/Vampire_normal.png";
            return new MixamoPlayableClassTrialSpec(
                ClassId.Rogue,
                "Rogue",
                "Vampire A Lusth",
                folder,
                folder + "/VampireALusth.fbx",
                SourceRoot + "/Rogue/Original/VampireALusth.fbx",
                "RogueModel_MixamoVampireTrial",
                "897280db79781384369a3cb031c2049a0227811d286eb344de7647f5a16d99ab",
                16130944L,
                0.855019f,
                0.03f,
                1,
                15022,
                9073,
                99,
                2,
                2.245564f,
                new[]
                {
                    Texture(
                        folder,
                        "vampireDiffuse",
                        "Vampire_diffuse.png",
                        "0797ea5d7e046f5730ae54465466eb3aea8308b146096fa76fa5e6d3598ac0b3",
                        3568311L,
                        2048,
                        2048,
                        MixamoTrialTextureUsage.BaseColor),
                    Texture(
                        folder,
                        "vampireTransparent",
                        "Vampire_diffuse_transparent.png",
                        "0a7c94ff68a44247fc1909a9dc6fc3d5bab1acb4616bd6c0bc6fc80933601940",
                        4185184L,
                        2048,
                        2048,
                        MixamoTrialTextureUsage.BaseColor,
                        true),
                    Texture(
                        folder,
                        "vampireEmission",
                        "Vampire_emission.png",
                        "944b8d6a29f861838c56d6b9ee470d11274cbc73adba87d522bc88d859fa4d55",
                        197675L,
                        512,
                        512,
                        MixamoTrialTextureUsage.Emission),
                    Texture(
                        folder,
                        "vampireNormal",
                        "Vampire_normal.png",
                        "54a1b8013ffb91e409f7543ca71f850a7442e9ef26958e9490e81aa0c10a1049",
                        3938425L,
                        2048,
                        2048,
                        MixamoTrialTextureUsage.Normal),
                    Texture(
                        folder,
                        "vampireSpecular",
                        "Vampire_specular.png",
                        "6c470a4e4a7656d95e28988f7c7ff3bd2dcb123e3a6aaaea273176d5fae16a3a",
                        3033423L,
                        2048,
                        2048,
                        MixamoTrialTextureUsage.Specular),
                },
                new[]
                {
                    Material(
                        "vampire",
                        "Vampire_MAT1",
                        "Rogue_MixamoVampireTrial_Vampire.mat",
                        diffuse,
                        normal,
                        string.Empty,
                        emission,
                        false,
                        false,
                        Color.white,
                        0f,
                        0.28f),
                    Material(
                        "vampireTransparent",
                        "Vampire_MAT_Transparent",
                        "Rogue_MixamoVampireTrial_Transparent.mat",
                        transparent,
                        normal,
                        string.Empty,
                        string.Empty,
                        true,
                        true,
                        Color.white,
                        0f,
                        0.22f),
                },
                new[]
                {
                    Renderer("Vampire", "vampire", "vampireTransparent"),
                });
        }

        private static MixamoPlayableClassTrialSpec CreateMageSpec()
        {
            const string folder = AssetRoot + "/Mage";
            string diffuse = folder + "/ExtractedTextures/Demon__diffuse.png";
            string normal = folder + "/ExtractedTextures/Demon__normal.png";
            return new MixamoPlayableClassTrialSpec(
                ClassId.Mage,
                "Mage",
                "Demon T Wiezzorek",
                folder,
                folder + "/DemonTWiezzorek.fbx",
                SourceRoot + "/Mage/Original/DemonTWiezzorek.fbx",
                "MageModel_MixamoDemonTrial",
                "dec695008c03b22508973b5e387caaae56dfcbc0c6274429cfef3b8d2e89913a",
                11329840L,
                0.811476f,
                0.03f,
                8,
                16314,
                9530,
                79,
                1,
                2.366058f,
                new[]
                {
                    Texture(
                        folder,
                        "demonDiffuse",
                        "Demon__diffuse.png",
                        "b52ee1d675cb2e83fbe20de3d8e32ee89cd4e5d64c8465fa6691baa3e6103548",
                        3936279L,
                        2048,
                        2048,
                        MixamoTrialTextureUsage.BaseColor),
                    Texture(
                        folder,
                        "demonNormal",
                        "Demon__normal.png",
                        "65918a89e2102326dd8916477d06c09fa44daad60977fed87dbbed4484c22865",
                        3393800L,
                        2048,
                        2048,
                        MixamoTrialTextureUsage.Normal),
                    Texture(
                        folder,
                        "demonSpecular",
                        "Demon__specular.png",
                        "f7366560cdcd483228683b7c2638a63e35ddae076ca0eeaa8d122f7ebb1b232b",
                        2408320L,
                        2048,
                        2048,
                        MixamoTrialTextureUsage.Specular),
                },
                new[]
                {
                    Material(
                        "demon",
                        "Demon_MAT",
                        "Mage_MixamoDemonTrial_Demon.mat",
                        diffuse,
                        normal,
                        string.Empty,
                        string.Empty,
                        false,
                        false,
                        Color.white,
                        0f,
                        0.22f),
                },
                new[]
                {
                    Renderer("LP_Eyes_ring", "demon"),
                    Renderer("LP_lower_armor_horns", "demon"),
                    Renderer("LP_Lower_shoes", "demon"),
                    Renderer("LP_shoulder1", "demon"),
                    Renderer("LP_shoulder2", "demon"),
                    Renderer("LP_shoulder_rings", "demon"),
                    Renderer("LP_Upper_body", "demon"),
                    Renderer("LP_upper_cloth", "demon"),
                });
        }

        private static MixamoTrialTextureSpec Texture(
            string folder,
            string key,
            string filename,
            string expectedSha256,
            long expectedBytes,
            int expectedWidth,
            int expectedHeight,
            MixamoTrialTextureUsage usage,
            bool usesAlpha = false)
        {
            return new MixamoTrialTextureSpec(
                key,
                folder + "/ExtractedTextures/" + filename,
                expectedSha256,
                expectedBytes,
                expectedWidth,
                expectedHeight,
                usage,
                usesAlpha);
        }

        private static MixamoTrialMaterialSpec Material(
            string key,
            string sourceMaterialName,
            string generatedFilename,
            string baseColorAssetPath,
            string normalAssetPath,
            string specularAssetPath,
            string emissionAssetPath,
            bool alphaClip,
            bool doubleSided,
            Color tint,
            float metallic,
            float smoothness)
        {
            return new MixamoTrialMaterialSpec(
                key,
                sourceMaterialName,
                "Assets/SessionRpg/Art/Characters/" + generatedFilename,
                baseColorAssetPath,
                normalAssetPath,
                specularAssetPath,
                emissionAssetPath,
                alphaClip,
                doubleSided,
                tint,
                metallic,
                smoothness);
        }

        private static MixamoTrialRendererSpec Renderer(
            string rendererName,
            params string[] materialKeysBySlot)
        {
            return new MixamoTrialRendererSpec(rendererName, materialKeysBySlot);
        }

        private static int ConfigureModelAndExtractTextures(
            MixamoPlayableClassTrialSpec spec)
        {
            ModelImporter importer = AssetImporter.GetAtPath(spec.ModelAssetPath)
                as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Missing or invalid staged Mixamo trial FBX at '" +
                    spec.ModelAssetPath + "'.");
            }

            int changedCount = 0;
            if (ConfigureModelImporter(importer))
            {
                changedCount++;
            }

            string absoluteTextureFolder = GetAbsoluteProjectPath(
                spec.ExtractedTextureFolder);
            Directory.CreateDirectory(absoluteTextureFolder);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            bool hasExtractedFiles = Directory
                .EnumerateFiles(absoluteTextureFolder, "*", SearchOption.TopDirectoryOnly)
                .Any(value =>
                    !value.EndsWith(".meta", StringComparison.OrdinalIgnoreCase));
            if (!hasExtractedFiles)
            {
                importer = AssetImporter.GetAtPath(spec.ModelAssetPath) as ModelImporter;
                if (importer == null || !importer.ExtractTextures(spec.ExtractedTextureFolder))
                {
                    throw new InvalidOperationException(
                        "Unity could not extract embedded textures for '" +
                        spec.DisplayName + "'.");
                }

                changedCount++;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(spec.ModelAssetPath) as ModelImporter;
                importer?.SaveAndReimport();
            }

            for (int textureIndex = 0;
                 textureIndex < spec.Textures.Count;
                 textureIndex++)
            {
                MixamoTrialTextureSpec textureSpec = spec.Textures[textureIndex];
                TextureImporter textureImporter =
                    AssetImporter.GetAtPath(textureSpec.AssetPath) as TextureImporter;
                if (textureImporter == null)
                {
                    throw new InvalidOperationException(
                        "Expected extracted texture is missing at '" +
                        textureSpec.AssetPath + "'.");
                }

                if (ConfigureTextureImporter(textureImporter, textureSpec))
                {
                    changedCount++;
                }
            }

            return changedCount;
        }

        private static bool ConfigureModelImporter(ModelImporter importer)
        {
            bool changed = false;
            changed |= SetValue(importer.globalScale, 1f, value => importer.globalScale = value);
            changed |= SetValue(importer.useFileScale, true, value => importer.useFileScale = value);
            changed |= SetValue(importer.bakeAxisConversion, true, value => importer.bakeAxisConversion = value);
            changed |= SetValue(importer.animationType, ModelImporterAnimationType.Human, value => importer.animationType = value);
            changed |= SetValue(importer.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, value => importer.avatarSetup = value);
            changed |= SetValue(importer.importAnimation, false, value => importer.importAnimation = value);
            changed |= SetValue(importer.optimizeGameObjects, false, value => importer.optimizeGameObjects = value);
            changed |= SetValue(importer.importAnimatedCustomProperties, false, value => importer.importAnimatedCustomProperties = value);
            changed |= SetValue(importer.importBlendShapes, true, value => importer.importBlendShapes = value);
            changed |= SetValue(importer.importCameras, false, value => importer.importCameras = value);
            changed |= SetValue(importer.importLights, false, value => importer.importLights = value);
            changed |= SetValue(importer.importVisibility, false, value => importer.importVisibility = value);
            changed |= SetValue(importer.importConstraints, false, value => importer.importConstraints = value);
            changed |= SetValue(importer.materialImportMode, ModelImporterMaterialImportMode.ImportViaMaterialDescription, value => importer.materialImportMode = value);
            changed |= SetValue(importer.isReadable, false, value => importer.isReadable = value);
            changed |= SetValue(importer.addCollider, false, value => importer.addCollider = value);
            changed |= SetValue(importer.keepQuads, false, value => importer.keepQuads = value);
            changed |= SetValue(importer.weldVertices, true, value => importer.weldVertices = value);
            changed |= SetValue(importer.optimizeMeshPolygons, true, value => importer.optimizeMeshPolygons = value);
            changed |= SetValue(importer.optimizeMeshVertices, true, value => importer.optimizeMeshVertices = value);
            changed |= SetValue(importer.meshCompression, ModelImporterMeshCompression.Off, value => importer.meshCompression = value);
            changed |= SetValue(importer.generateSecondaryUV, false, value => importer.generateSecondaryUV = value);
            changed |= SetValue(importer.importNormals, ModelImporterNormals.Import, value => importer.importNormals = value);
            changed |= SetValue(importer.importTangents, ModelImporterTangents.CalculateMikk, value => importer.importTangents = value);
            changed |= SetValue(importer.indexFormat, ModelImporterIndexFormat.Auto, value => importer.indexFormat = value);
            changed |= SetValue(importer.preserveHierarchy, true, value => importer.preserveHierarchy = value);
            changed |= SetValue(importer.sortHierarchyByName, true, value => importer.sortHierarchyByName = value);
            changed |= SetValue(importer.skinWeights, ModelImporterSkinWeights.Custom, value => importer.skinWeights = value);
            changed |= SetValue(importer.maxBonesPerVertex, 4, value => importer.maxBonesPerVertex = value);
            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static bool ConfigureTextureImporter(
            TextureImporter importer,
            MixamoTrialTextureSpec spec)
        {
            TextureImporterType expectedType = spec.IsNormal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            TextureImporterAlphaSource expectedAlpha = spec.UsesAlpha
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            bool changed = false;
            changed |= SetValue(importer.textureType, expectedType, value => importer.textureType = value);
            changed |= SetValue(importer.sRGBTexture, spec.IsSrgb, value => importer.sRGBTexture = value);
            changed |= SetValue(importer.alphaSource, expectedAlpha, value => importer.alphaSource = value);
            changed |= SetValue(importer.alphaIsTransparency, spec.UsesAlpha, value => importer.alphaIsTransparency = value);
            changed |= SetValue(importer.mipmapEnabled, true, value => importer.mipmapEnabled = value);
            changed |= SetValue(importer.wrapMode, TextureWrapMode.Repeat, value => importer.wrapMode = value);
            changed |= SetValue(importer.filterMode, FilterMode.Bilinear, value => importer.filterMode = value);
            changed |= SetValue(importer.anisoLevel, 4, value => importer.anisoLevel = value);
            changed |= SetValue(importer.maxTextureSize, ImportedMaximumTextureSize, value => importer.maxTextureSize = value);
            changed |= SetValue(importer.npotScale, TextureImporterNPOTScale.ToNearest, value => importer.npotScale = value);
            changed |= SetValue(importer.isReadable, false, value => importer.isReadable = value);
            changed |= SetValue(importer.textureCompression, TextureImporterCompression.CompressedHQ, value => importer.textureCompression = value);

            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            if (!android.overridden ||
                android.maxTextureSize != ImportedMaximumTextureSize ||
                android.format != TextureImporterFormat.Automatic ||
                android.textureCompression != TextureImporterCompression.Compressed ||
                android.compressionQuality != 65 ||
                android.crunchedCompression)
            {
                android.name = "Android";
                android.overridden = true;
                android.maxTextureSize = ImportedMaximumTextureSize;
                android.format = TextureImporterFormat.Automatic;
                android.textureCompression = TextureImporterCompression.Compressed;
                android.compressionQuality = 65;
                android.crunchedCompression = false;
                importer.SetPlatformTextureSettings(android);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static void ValidateSpec(
            MixamoPlayableClassTrialSpec spec,
            List<string> errors)
        {
            ValidateImporter(spec, errors);
            ValidateTextures(spec, errors);
            ValidateModel(spec, errors);
            ValidateNoImportedAnimations(spec, errors);
        }

        private static void ValidateTrialBoundary(List<string> errors)
        {
            string manifestAbsolutePath = GetAbsoluteProjectPath(SourceManifestPath);
            if (!File.Exists(manifestAbsolutePath))
            {
                errors.Add("Missing Mixamo playable-class trial manifest at '" + SourceManifestPath + "'.");
                return;
            }

            TrialManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<TrialManifest>(
                    File.ReadAllText(manifestAbsolutePath));
            }
            catch (Exception exception)
            {
                errors.Add("Could not parse Mixamo playable-class trial manifest: " + exception.Message);
                return;
            }

            if (manifest == null ||
                manifest.schemaVersion != 2 ||
                !string.Equals(manifest.status, "licensed-game-base", StringComparison.Ordinal) ||
                !manifest.promotionAllowed ||
                !string.Equals(
                    manifest.distributionPolicy,
                    "embedded-end-use-only",
                    StringComparison.Ordinal) ||
                !string.Equals(manifest.runtimeAnimationPolicy, "project-controller-only", StringComparison.Ordinal) ||
                manifest.characters == null ||
                manifest.characters.Length != TrialSpecs.Length)
            {
                errors.Add(
                    "Mixamo playable-class manifest must remain schema 2, licensed for embedded-game use, model-only, and enumerate exactly three characters.");
                return;
            }

            for (int index = 0; index < TrialSpecs.Length; index++)
            {
                MixamoPlayableClassTrialSpec spec = TrialSpecs[index];
                TrialManifestCharacter character = manifest.characters.SingleOrDefault(value =>
                    string.Equals(value.stagedPath, spec.ModelAssetPath, StringComparison.Ordinal));
                string expectedOriginalRelative = spec.ImmutableOriginalPath.Substring(
                    (SourceRoot + "/").Length);
                if (character == null ||
                    !string.Equals(character.classId, spec.ManifestClassId, StringComparison.Ordinal) ||
                    !string.Equals(character.originalPath, expectedOriginalRelative, StringComparison.Ordinal) ||
                    character.bytes != spec.ExpectedModelBytes ||
                    !string.Equals(character.sha256, spec.ExpectedModelSha256, StringComparison.Ordinal) ||
                    !string.Equals(character.productionPromotion, "accepted-customizable-base", StringComparison.Ordinal))
                {
                    errors.Add(
                        "Manifest entry for '" + spec.ModelAssetPath +
                        "' no longer matches its locked licensed game-base source contract.");
                }

                ValidateProvenanceText(spec.AssetFolder + "/PROVENANCE.md", errors);
                ValidateProvenanceText(
                    spec.ImmutableOriginalPath.Substring(
                        0,
                        spec.ImmutableOriginalPath.LastIndexOf("/Original/", StringComparison.Ordinal)) +
                    "/PROVENANCE.md",
                    errors);
            }

            ValidateProvenanceText(AssetRoot + "/PROVENANCE.md", errors);
            ValidateProvenanceText(SourceRoot + "/PROVENANCE.md", errors);
        }

        private static void ValidateProvenanceText(
            string relativePath,
            List<string> errors)
        {
            string absolutePath = GetAbsoluteProjectPath(relativePath);
            if (!File.Exists(absolutePath))
            {
                errors.Add("Missing Mixamo playable-class provenance at '" + relativePath + "'.");
                return;
            }

            string text = File.ReadAllText(absolutePath);
            if (text.IndexOf("Adobe", StringComparison.OrdinalIgnoreCase) < 0 ||
                text.IndexOf("embedded", StringComparison.OrdinalIgnoreCase) < 0 ||
                text.IndexOf("standalone", StringComparison.OrdinalIgnoreCase) < 0)
            {
                errors.Add(
                    "Mixamo playable-class provenance at '" + relativePath +
                    "' must preserve the Adobe, embedded-game, and standalone-file boundaries.");
            }
        }

        private static void ValidateImmutableFiles(List<string> errors)
        {
            for (int index = 0; index < TrialSpecs.Length; index++)
            {
                ValidateImmutableFiles(TrialSpecs[index], errors);
            }
        }

        private static void ValidateImmutableFiles(
            MixamoPlayableClassTrialSpec spec,
            List<string> errors)
        {
            ValidateFile(
                spec.ModelAssetPath,
                spec.ExpectedModelBytes,
                spec.ExpectedModelSha256,
                errors);
            ValidateFile(
                spec.ImmutableOriginalPath,
                spec.ExpectedModelBytes,
                spec.ExpectedModelSha256,
                errors);
        }

        private static void ValidateFile(
            string relativePath,
            long expectedBytes,
            string expectedSha256,
            List<string> errors)
        {
            string absolutePath = GetAbsoluteProjectPath(relativePath);
            if (!File.Exists(absolutePath))
            {
                errors.Add("Missing immutable Mixamo trial file at '" + relativePath + "'.");
                return;
            }

            long bytes = new FileInfo(absolutePath).Length;
            if (bytes != expectedBytes)
            {
                errors.Add(
                    "Immutable Mixamo trial file '" + relativePath + "' has " +
                    bytes + " bytes; expected " + expectedBytes + ".");
            }

            string sha256 = ComputeSha256(absolutePath);
            if (!string.Equals(sha256, expectedSha256, StringComparison.Ordinal))
            {
                errors.Add(
                    "Immutable Mixamo trial file '" + relativePath +
                    "' changed SHA-256; found " + sha256 + ".");
            }
        }

        private static void ValidateImporter(
            MixamoPlayableClassTrialSpec spec,
            List<string> errors)
        {
            ModelImporter importer = AssetImporter.GetAtPath(spec.ModelAssetPath)
                as ModelImporter;
            if (importer == null)
            {
                errors.Add("Missing ModelImporter for '" + spec.ModelAssetPath + "'.");
                return;
            }

            if (!Mathf.Approximately(importer.globalScale, 1f) ||
                !importer.useFileScale ||
                !importer.bakeAxisConversion ||
                importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                importer.importAnimation ||
                importer.optimizeGameObjects ||
                importer.importAnimatedCustomProperties ||
                !importer.importBlendShapes ||
                importer.importCameras ||
                importer.importLights ||
                importer.importVisibility ||
                importer.importConstraints ||
                importer.materialImportMode != ModelImporterMaterialImportMode.ImportViaMaterialDescription ||
                importer.isReadable ||
                importer.addCollider ||
                importer.keepQuads ||
                !importer.weldVertices ||
                !importer.optimizeMeshPolygons ||
                !importer.optimizeMeshVertices ||
                importer.meshCompression != ModelImporterMeshCompression.Off ||
                importer.generateSecondaryUV ||
                importer.importNormals != ModelImporterNormals.Import ||
                importer.importTangents != ModelImporterTangents.CalculateMikk ||
                importer.indexFormat != ModelImporterIndexFormat.Auto ||
                !importer.preserveHierarchy ||
                !importer.sortHierarchyByName ||
                importer.skinWeights != ModelImporterSkinWeights.Custom ||
                importer.maxBonesPerVertex != 4)
            {
                errors.Add(
                    "Mixamo trial FBX '" + spec.ModelAssetPath +
                    "' is not using the audited unoptimized, animation-free, four-weight Humanoid contract. " +
                    ConfigureInstruction);
            }
        }

        private static void ValidateTextures(
            MixamoPlayableClassTrialSpec spec,
            List<string> errors)
        {
            for (int index = 0; index < spec.Textures.Count; index++)
            {
                MixamoTrialTextureSpec textureSpec = spec.Textures[index];
                ValidateFile(
                    textureSpec.AssetPath,
                    textureSpec.ExpectedBytes,
                    textureSpec.ExpectedSha256,
                    errors);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    textureSpec.AssetPath);
                TextureImporter importer = AssetImporter.GetAtPath(textureSpec.AssetPath)
                    as TextureImporter;
                if (texture == null || importer == null)
                {
                    errors.Add("Extracted texture is not loadable at '" + textureSpec.AssetPath + "'.");
                    continue;
                }

                TextureImporterType expectedType = textureSpec.IsNormal
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;
                TextureImporterAlphaSource expectedAlpha = textureSpec.UsesAlpha
                    ? TextureImporterAlphaSource.FromInput
                    : TextureImporterAlphaSource.None;
                TextureImporterPlatformSettings android =
                    importer.GetPlatformTextureSettings("Android");
                if (texture.width != textureSpec.ExpectedWidth ||
                    texture.height != textureSpec.ExpectedHeight ||
                    importer.textureType != expectedType ||
                    importer.sRGBTexture != textureSpec.IsSrgb ||
                    importer.alphaSource != expectedAlpha ||
                    importer.alphaIsTransparency != textureSpec.UsesAlpha ||
                    !importer.mipmapEnabled ||
                    importer.wrapMode != TextureWrapMode.Repeat ||
                    importer.filterMode != FilterMode.Bilinear ||
                    importer.anisoLevel != 4 ||
                    importer.maxTextureSize != ImportedMaximumTextureSize ||
                    importer.npotScale != TextureImporterNPOTScale.ToNearest ||
                    importer.isReadable ||
                    importer.textureCompression != TextureImporterCompression.CompressedHQ ||
                    !android.overridden ||
                    android.maxTextureSize != ImportedMaximumTextureSize ||
                    android.format != TextureImporterFormat.Automatic ||
                    android.textureCompression != TextureImporterCompression.Compressed ||
                    android.compressionQuality != 65 ||
                    android.crunchedCompression)
                {
                    errors.Add(
                        "Extracted texture '" + textureSpec.AssetPath +
                        "' is not using the audited playable-class trial texture policy. " +
                        ConfigureInstruction);
                }
            }
        }

        private static void ValidateModel(
            MixamoPlayableClassTrialSpec spec,
            List<string> errors)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelAssetPath);
            if (model == null)
            {
                errors.Add("Mixamo trial FBX is not a loadable model at '" + spec.ModelAssetPath + "'.");
                return;
            }

            if (model.transform.localPosition.sqrMagnitude > TransformTolerance ||
                Quaternion.Angle(model.transform.localRotation, Quaternion.identity) > TransformTolerance ||
                (model.transform.localScale - Vector3.one).sqrMagnitude > TransformTolerance)
            {
                errors.Add(
                    spec.DisplayName +
                    " model root must remain identity; generated hierarchy owns authored scaling.");
            }

            if (model.GetComponentsInChildren<Collider>(true).Length != 0 ||
                model.GetComponentsInChildren<Camera>(true).Length != 0 ||
                model.GetComponentsInChildren<Light>(true).Length != 0 ||
                model.GetComponentsInChildren<Animation>(true).Length != 0)
            {
                errors.Add(
                    spec.DisplayName +
                    " source may contain only its Humanoid rig and visual renderers.");
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            SkinnedMeshRenderer[] skinnedRenderers =
                model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            MeshRenderer[] rigidRenderers =
                model.GetComponentsInChildren<MeshRenderer>(true);
            bool hasUnexpectedRigidRenderer = rigidRenderers.Any(renderer =>
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                return filter == null ||
                    filter.sharedMesh == null ||
                    filter.sharedMesh.vertexCount != 0;
            });
            if (skinnedRenderers.Length != spec.ExpectedRendererCount ||
                spec.Renderers.Count != spec.ExpectedRendererCount ||
                hasUnexpectedRigidRenderer)
            {
                errors.Add(
                    spec.DisplayName + " exposes " + renderers.Length +
                    " total / " + skinnedRenderers.Length + " skinned / " +
                    rigidRenderers.Length + " rigid renderers; expected " +
                    spec.ExpectedRendererCount +
                    " explicitly locked skinned renderers plus only zero-vertex rigid placeholders.");
            }

            var rendererByName = BuildRendererMap(spec, renderers, errors, "source");
            int triangles = 0;
            int vertices = 0;
            var weightedBones = new HashSet<Transform>();
            var materials = new HashSet<Material>();
            for (int index = 0; index < skinnedRenderers.Length; index++)
            {
                SkinnedMeshRenderer renderer = skinnedRenderers[index];
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null || renderer.rootBone == null ||
                    renderer.bones == null || renderer.bones.Length == 0 ||
                    renderer.bones.Any(value => value == null))
                {
                    errors.Add(
                        spec.DisplayName + " renderer '" + renderer.name +
                        "' has an incomplete skin binding.");
                    continue;
                }

                if (!mesh.HasVertexAttribute(VertexAttribute.Normal) ||
                    !mesh.HasVertexAttribute(VertexAttribute.Tangent) ||
                    !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                {
                    errors.Add(
                        spec.DisplayName + " renderer '" + renderer.name +
                        "' must retain normals, tangents, and UV0.");
                }

                vertices += mesh.vertexCount;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    triangles += (int)(mesh.GetIndexCount(submesh) / 3L);
                }

                for (int bone = 0; bone < renderer.bones.Length; bone++)
                {
                    weightedBones.Add(renderer.bones[bone]);
                }

                Material[] rendererMaterials = renderer.sharedMaterials;
                for (int material = 0; material < rendererMaterials.Length; material++)
                {
                    if (rendererMaterials[material] != null)
                    {
                        materials.Add(rendererMaterials[material]);
                    }
                }
            }

            if (triangles != spec.ExpectedTriangleCount ||
                vertices != spec.ExpectedVertexCount ||
                weightedBones.Count != spec.ExpectedWeightedBoneCount ||
                materials.Count != spec.ExpectedMaterialCount)
            {
                errors.Add(
                    spec.DisplayName + " imported structure changed: found " +
                    triangles + " triangles, " + vertices + " vertices, " +
                    weightedBones.Count + " weighted bones, and " +
                    materials.Count + " materials; expected " +
                    spec.ExpectedTriangleCount + ", " + spec.ExpectedVertexCount +
                    ", " + spec.ExpectedWeightedBoneCount + ", and " +
                    spec.ExpectedMaterialCount + ".");
            }

            for (int rendererIndex = 0;
                 rendererIndex < spec.Renderers.Count;
                 rendererIndex++)
            {
                MixamoTrialRendererSpec rendererSpec = spec.Renderers[rendererIndex];
                if (!rendererByName.TryGetValue(
                        rendererSpec.RendererName,
                        out Renderer renderer))
                {
                    continue;
                }

                Material[] rendererMaterials = renderer.sharedMaterials;
                if (rendererMaterials.Length != rendererSpec.MaterialKeysBySlot.Count)
                {
                    errors.Add(
                        spec.DisplayName + " source renderer '" + renderer.name +
                        "' material-slot count changed.");
                    continue;
                }

                for (int slot = 0; slot < rendererMaterials.Length; slot++)
                {
                    Material material = rendererMaterials[slot];
                    MixamoTrialMaterialSpec materialSpec = spec.GetMaterialOrThrow(
                        rendererSpec.MaterialKeysBySlot[slot]);
                    if (material == null ||
                        !string.Equals(
                            material.name,
                            materialSpec.SourceMaterialName,
                            StringComparison.Ordinal))
                    {
                        errors.Add(
                            spec.DisplayName + " source renderer '" + renderer.name +
                            "' slot " + slot + " material changed.");
                    }
                }
            }

            ValidateAvatarAndBounds(spec, model, errors);
        }

        private static Dictionary<string, Renderer> BuildRendererMap(
            MixamoPlayableClassTrialSpec spec,
            Renderer[] renderers,
            List<string> errors,
            string label)
        {
            var result = new Dictionary<string, Renderer>(StringComparer.Ordinal);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!result.TryAdd(renderer.name, renderer))
                {
                    errors.Add(
                        spec.DisplayName + " " + label +
                        " contains duplicate renderer name '" + renderer.name + "'.");
                }
            }

            for (int index = 0; index < spec.Renderers.Count; index++)
            {
                string rendererName = spec.Renderers[index].RendererName;
                if (!result.ContainsKey(rendererName))
                {
                    errors.Add(
                        spec.DisplayName + " " + label + " is missing renderer '" +
                        rendererName + "'.");
                }
            }

            return result;
        }

        private static void ValidateAvatarAndBounds(
            MixamoPlayableClassTrialSpec spec,
            GameObject model,
            List<string> errors)
        {
            Avatar[] validAvatars = AssetDatabase.LoadAllAssetsAtPath(spec.ModelAssetPath)
                .OfType<Avatar>()
                .Where(value => value.isValid && value.isHuman)
                .ToArray();
            if (validAvatars.Length != 1)
            {
                errors.Add(
                    spec.DisplayName + " exposes " + validAvatars.Length +
                    " valid Humanoid Avatars; expected exactly one. " +
                    ConfigureInstruction);
                return;
            }

            Animator[] animators = model.GetComponentsInChildren<Animator>(true);
            if (animators.Length != 1 || animators[0].transform != model.transform)
            {
                errors.Add(
                    spec.DisplayName +
                    " source must expose exactly one Animator on the model root.");
            }

            GameObject instance = UnityEngine.Object.Instantiate(model);
            try
            {
                Animator animator = instance.GetComponent<Animator>();
                if (animator == null)
                {
                    errors.Add(spec.DisplayName + " instantiated source has no root Animator.");
                    return;
                }

                animator.avatar = validAvatars[0];
                for (int index = 0; index < RequiredHumanoidBones.Length; index++)
                {
                    HumanBodyBones bone = RequiredHumanoidBones[index];
                    if (animator.GetBoneTransform(bone) == null)
                    {
                        errors.Add(
                            spec.DisplayName + " Humanoid mapping is missing '" + bone + "'.");
                    }
                }

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    errors.Add(spec.DisplayName + " source has no render bounds.");
                    return;
                }

                Bounds bounds = renderers[0].bounds;
                for (int index = 1; index < renderers.Length; index++)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }

                if (Mathf.Abs(bounds.size.y - spec.ExpectedModelHeight) >
                    ModelHeightTolerance)
                {
                    errors.Add(
                        spec.DisplayName + " renders " + bounds.size.y.ToString("F6") +
                        " m tall at identity; expected approximately " +
                        spec.ExpectedModelHeight.ToString("F6") + " m.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateGeneratedAnimator(
            MixamoPlayableClassTrialSpec spec,
            Animator animator,
            List<string> errors)
        {
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                errors.Add(
                    "Generated " + spec.DisplayName +
                    " trial root requires one valid Humanoid Animator.");
                return;
            }

            if (animator.runtimeAnimatorController == null)
            {
                errors.Add(
                    "Generated " + spec.DisplayName +
                    " Animator must retain the accepted project controller.");
            }

            if (animator.applyRootMotion)
            {
                errors.Add(
                    "Generated " + spec.DisplayName +
                    " Animator must keep applyRootMotion disabled.");
            }
        }

        private static void ValidateNoImportedAnimations(
            MixamoPlayableClassTrialSpec spec,
            List<string> errors)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(spec.ModelAssetPath)
                .OfType<AnimationClip>()
                .Where(value =>
                    !value.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
            if (clips.Length != 0)
            {
                errors.Add(
                    spec.DisplayName + " imports " + clips.Length +
                    " animation clip(s); Mixamo motion is not admitted by this model-only trial.");
            }
        }

        private static void ValidateNoAnimationFbxInsideAssets(List<string> errors)
        {
            string absoluteRoot = GetAbsoluteProjectPath(AssetRoot);
            if (!Directory.Exists(absoluteRoot))
            {
                errors.Add("Missing Mixamo playable-class Assets root at '" + AssetRoot + "'.");
                return;
            }

            string[] actualFbx = Directory
                .EnumerateFiles(absoluteRoot, "*", SearchOption.AllDirectories)
                .Where(value => string.Equals(
                    Path.GetExtension(value),
                    ".fbx",
                    StringComparison.OrdinalIgnoreCase))
                .Select(Path.GetFullPath)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] expectedFbx = TrialSpecs
                .Select(value => Path.GetFullPath(GetAbsoluteProjectPath(value.ModelAssetPath)))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!actualFbx.SequenceEqual(expectedFbx, StringComparer.Ordinal))
            {
                errors.Add(
                    "Mixamo playable-class Assets root must contain only the three audited model FBXs; downloaded animation FBXs remain outside Assets.");
            }
        }

        private static void LogImportedStructure(MixamoPlayableClassTrialSpec spec)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelAssetPath);
            if (model == null)
            {
                Debug.LogError("[MixamoClassInspect] Could not load " + spec.ModelAssetPath);
                return;
            }

            SkinnedMeshRenderer[] renderers =
                model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            MeshRenderer[] rigidRenderers =
                model.GetComponentsInChildren<MeshRenderer>(true);
            for (int index = 0; index < rigidRenderers.Length; index++)
            {
                MeshFilter filter = rigidRenderers[index].GetComponent<MeshFilter>();
                Mesh mesh = filter != null ? filter.sharedMesh : null;
                Material[] slots = rigidRenderers[index].sharedMaterials;
                Debug.Log(
                    "[MixamoClassInspect] class=" + spec.ClassId +
                    " rigidRenderer=" + rigidRenderers[index].name +
                    " mesh=" +
                    (mesh != null
                        ? mesh.name
                        : "<none>") +
                    " vertices=" + (mesh != null ? mesh.vertexCount : 0) +
                    " normals=" + (mesh != null && mesh.HasVertexAttribute(VertexAttribute.Normal)) +
                    " tangents=" + (mesh != null && mesh.HasVertexAttribute(VertexAttribute.Tangent)) +
                    " uv0=" + (mesh != null && mesh.HasVertexAttribute(VertexAttribute.TexCoord0)) +
                    " slots=" + string.Join(",", slots.Select(value =>
                        value != null ? value.name : "<null>")) +
                    " enabled=" + rigidRenderers[index].enabled);
            }
            int triangles = 0;
            int vertices = 0;
            var bones = new HashSet<Transform>();
            var materials = new HashSet<Material>();
            for (int index = 0; index < renderers.Length; index++)
            {
                SkinnedMeshRenderer renderer = renderers[index];
                Mesh mesh = renderer.sharedMesh;
                if (mesh != null)
                {
                    vertices += mesh.vertexCount;
                    for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    {
                        triangles += (int)(mesh.GetIndexCount(submesh) / 3L);
                    }
                }

                for (int boneIndex = 0; boneIndex < renderer.bones.Length; boneIndex++)
                {
                    if (renderer.bones[boneIndex] != null)
                    {
                        bones.Add(renderer.bones[boneIndex]);
                    }
                }

                Material[] slots = renderer.sharedMaterials;
                for (int slot = 0; slot < slots.Length; slot++)
                {
                    if (slots[slot] != null)
                    {
                        materials.Add(slots[slot]);
                    }
                }

                string slotDescription = string.Join(
                    ",",
                    slots.Select(value => value != null ? value.name : "<null>"));
                Debug.Log(
                    "[MixamoClassInspect] class=" + spec.ClassId +
                    " renderer=" + renderer.name +
                    " slots=" + slotDescription);
            }

            float height = 0f;
            Vector3 boundsCenter = Vector3.zero;
            Vector3 boundsMinimum = Vector3.zero;
            Vector3 boundsMaximum = Vector3.zero;
            GameObject instance = UnityEngine.Object.Instantiate(model);
            try
            {
                Renderer[] instanceRenderers =
                    instance.GetComponentsInChildren<Renderer>(true);
                if (instanceRenderers.Length > 0)
                {
                    Bounds bounds = instanceRenderers[0].bounds;
                    for (int index = 1; index < instanceRenderers.Length; index++)
                    {
                        bounds.Encapsulate(instanceRenderers[index].bounds);
                    }

                    height = bounds.size.y;
                    boundsCenter = bounds.center;
                    boundsMinimum = bounds.min;
                    boundsMaximum = bounds.max;
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            Debug.Log(
                "[MixamoClassInspect] class=" + spec.ClassId +
                " renderers=" + renderers.Length +
                " triangles=" + triangles +
                " vertices=" + vertices +
                " weightedBones=" + bones.Count +
                " materials=" + materials.Count +
                " materialNames=" + string.Join(",", materials.Select(value => value.name).OrderBy(value => value, StringComparer.Ordinal)) +
                " height=" + height.ToString("F6") +
                " center=" + boundsCenter.ToString("F6") +
                " min=" + boundsMinimum.ToString("F6") +
                " max=" + boundsMaximum.ToString("F6"));

            for (int index = 0; index < spec.Textures.Count; index++)
            {
                MixamoTrialTextureSpec textureSpec = spec.Textures[index];
                string absolutePath = GetAbsoluteProjectPath(textureSpec.AssetPath);
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureSpec.AssetPath);
                if (!File.Exists(absolutePath) || texture == null)
                {
                    Debug.LogError("[MixamoClassInspect] Missing texture " + textureSpec.AssetPath);
                    continue;
                }

                Debug.Log(
                    "[MixamoClassInspect] class=" + spec.ClassId +
                    " texture=" + textureSpec.AssetPath +
                    " bytes=" + new FileInfo(absolutePath).Length +
                    " sha256=" + ComputeSha256(absolutePath) +
                    " imported=" + texture.width + "x" + texture.height);
            }

            foreach (Material material in materials.OrderBy(value => value.name, StringComparer.Ordinal))
            {
                var bindings = new List<string>();
                string[] propertyNames = material.GetTexturePropertyNames();
                for (int index = 0; index < propertyNames.Length; index++)
                {
                    Texture texture = material.GetTexture(propertyNames[index]);
                    if (texture != null)
                    {
                        bindings.Add(
                            propertyNames[index] + "=" +
                            AssetDatabase.GetAssetPath(texture));
                    }
                }

                Debug.Log(
                    "[MixamoClassInspect] class=" + spec.ClassId +
                    " material=" + material.name +
                    " shader=" + (material.shader != null ? material.shader.name : "<null>") +
                    " textures=" + string.Join(",", bindings));
            }
        }

        private static Transform FindDirectChild(Transform root, string name)
        {
            for (int index = 0; index < root.childCount; index++)
            {
                Transform child = root.GetChild(index);
                if (string.Equals(child.name, name, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static string GetFallbackRootName(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Priest:
                    return "PriestModel_Quaternius";
                case ClassId.Rogue:
                    return "RogueModel_Quaternius";
                case ClassId.Mage:
                    return "MageModel_Quaternius";
                default:
                    throw new ArgumentOutOfRangeException(nameof(classId), classId, null);
            }
        }

        private static string GetViewModelField(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Priest:
                    return "_healerModel";
                case ClassId.Rogue:
                    return "_rogueModel";
                case ClassId.Mage:
                    return "_mageModel";
                default:
                    throw new ArgumentOutOfRangeException(nameof(classId), classId, null);
            }
        }

        private static string GetPresenterAnimatorField(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Priest:
                    return "_healerAnimator";
                case ClassId.Rogue:
                    return "_rogueAnimator";
                case ClassId.Mage:
                    return "_mageAnimator";
                default:
                    throw new ArgumentOutOfRangeException(nameof(classId), classId, null);
            }
        }

        private static string GetAbsoluteProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException(
                    "Unity did not expose a valid project root for Mixamo playable-class trial validation.");
            }

            return Path.GetFullPath(Path.Combine(projectRoot, relativePath));
        }

        private static string ComputeSha256(string absolutePath)
        {
            using (FileStream stream = File.OpenRead(absolutePath))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash)
                    .Replace("-", string.Empty)
                    .ToLowerInvariant();
            }
        }

        private static bool SetValue<T>(T current, T expected, Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(current, expected))
            {
                return false;
            }

            setter(expected);
            return true;
        }

        private static void ThrowIfErrors(List<string> errors)
        {
            if (errors.Count == 0)
            {
                return;
            }

            var builder = new StringBuilder(
                "Mixamo playable-class licensed game-base validation failed:");
            for (int index = 0; index < errors.Count; index++)
            {
                builder.Append("\n - ");
                builder.Append(errors[index]);
            }

            builder.Append(
                "\nKeep these hash-locked models embedded in the game and do not distribute " +
                "the original or modified Content Files standalone.");
            throw new InvalidOperationException(builder.ToString());
        }

        private static void ShowDialog(string title, string message)
        {
            if (!UnityEngine.Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(title, message, "OK");
            }
        }

        [Serializable]
        private sealed class TrialManifest
        {
            public int schemaVersion;
            public string status;
            public bool promotionAllowed;
            public string runtimeAnimationPolicy;
            public string distributionPolicy;
            public TrialManifestCharacter[] characters;
        }

        [Serializable]
        private sealed class TrialManifestCharacter
        {
            public string classId;
            public string originalPath;
            public string stagedPath;
            public long bytes;
            public string sha256;
            public string productionPromotion;
        }
    }

    internal sealed class MixamoPlayableClassTrialBuildValidator :
        IPreprocessBuildWithReport
    {
        public int callbackOrder => -1099;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!MixamoPlayableClassTrialAssetValidator
                    .IsAnySelectedModelReferencedByEnabledBuildScenes())
            {
                return;
            }

            try
            {
                MixamoPlayableClassTrialAssetValidator.ValidateSelectedSourcesOrThrow();
            }
            catch (InvalidOperationException exception)
            {
                throw new BuildFailedException(exception.Message);
            }
        }
    }
}
