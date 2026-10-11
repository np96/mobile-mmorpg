using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SessionRpg.Unity.Gameplay;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Assets
{
    /// <summary>
    /// Import, provenance, and integrity contract for the licensed Erika Archer
    /// game base. Historical Trial names remain to preserve authored references.
    /// This remains separate from the dormant first-party Hunter validator because
    /// its hashes, import rules, and standalone-distribution boundary differ.
    /// </summary>
    public static class MixamoErikaTrialAssetValidator
    {
        public const string AssetFolder =
            "Assets/ThirdParty/Trials/AdobeMixamo/ErikaArcher";

        public const string ModelAssetPath =
            AssetFolder + "/ErikaArcher_WithBow.fbx";

        public const string ExtractedTextureFolder =
            AssetFolder + "/ExtractedTextures";

        public const string AssetsProvenancePath =
            AssetFolder + "/PROVENANCE.md";

        public const string SourceManifestPath =
            "SourceArt/Trials/AdobeMixamo/ErikaArcher/manifest.json";

        public const string SourceProvenancePath =
            "SourceArt/Trials/AdobeMixamo/ErikaArcher/PROVENANCE.md";

        public const string ImmutableOriginalPath =
            "SourceArt/Trials/AdobeMixamo/ErikaArcher/Original/ErikaArcher_WithBow.fbx";

        public const string ModelRootName = "HunterModel_MixamoErikaTrial";

        public const string ClothesRendererName = "Erika_Archer_Arrow_Mesh";

        public const string EyelashesRendererName = "Erika_Archer_Clothes_Mesh";

        // The FBX's renderer object names do not match their visible geometry.
        // These semantic aliases were verified from the imported Unity meshes;
        // do not "correct" them from the raw FBX strings.
        public const string EmbeddedBowRendererName = "Erika_Archer_Body_Mesh";

        public const string BodyRendererName = "Erika_Archer_Eyelashes_Mesh";

        public const string EyesRendererName = "Erika_Archer_Bow_Mesh";

        public const string EmbeddedArrowRendererName = "Erika_Archer_Eyes_Mesh";

        public const string ClothesBaseColorAssetPath =
            ExtractedTextureFolder + "/Erika_Archer_Clothes_diffuse.png";

        public const string ClothesNormalAssetPath =
            ExtractedTextureFolder + "/Erika_Archer_Clothes_normal.png";

        public const string BodyBaseColorAssetPath =
            ExtractedTextureFolder + "/FemaleFitA_Body_diffuse.png";

        public const string BodyNormalAssetPath =
            ExtractedTextureFolder + "/FemaleFitA_StdNM.png";

        public const string EyelashesBaseColorAssetPath =
            ExtractedTextureFolder + "/FemaleFitA_eyelash_diffuse.png";

        public const string BowBaseColorAssetPath =
            ExtractedTextureFolder + "/Bow_DIFF.jpg";

        public const string BowNormalAssetPath =
            ExtractedTextureFolder + "/Bow_NM.jpg";

        public const string ArrowBaseColorAssetPath =
            ExtractedTextureFolder + "/Arrow_DIFF.png";

        public const string ArrowNormalAssetPath =
            ExtractedTextureFolder + "/Arrow_NM.jpg";

        public const string ConfigureMenuPath =
            "Tools/Session RPG/Assets/Configure Mixamo Erika Game-Base Import";

        public const string ValidateMenuPath =
            "Tools/Session RPG/Assets/Validate Mixamo Erika Game Base";

        public const string ExpectedModelSha256 =
            "ccf2ee0330c85888a6725f0a8fec0ffc270cc2938d8a3281402988ea2f556818";

        public const long ExpectedModelBytes = 25094672L;

        public const int ExpectedRendererCount = 6;

        public const int ExpectedTriangleCount = 21006;

        public const int ExpectedVertexCount = 12424;

        public const int ExpectedWeightedBoneCount = 70;

        public const int ExpectedMaterialCount = 6;

        private const int ImportedMaximumTextureSize = 2048;
        private const float ExpectedModelHeight = 1.918772f;
        private const float ModelHeightTolerance = 0.015f;
        private const float TransformTolerance = 0.0001f;

        private const string ConfigureInstruction =
            "Run Tools > Session RPG > Assets > Configure Mixamo Erika Game-Base Import once, then perform one authored rebuild.";

        private static readonly string[] ExpectedRendererNames =
        {
            ClothesRendererName,
            EyelashesRendererName,
            EmbeddedBowRendererName,
            BodyRendererName,
            EyesRendererName,
            EmbeddedArrowRendererName,
        };

        private static readonly string[] ExpectedMaterialNames =
        {
            "Akai_MAT1",
            "Bow_MAT",
            "EyeSpec_MAT1",
            "phong1",
            "Body_MAT1",
            "Arrow_MAT",
        };

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

        private static readonly TextureContract[] TextureContracts =
        {
            new TextureContract(
                ArrowBaseColorAssetPath,
                "3709bcd5d15e8f8b6995d74cd0d3d00ed7136d127135b161d9f39f769488c2ed",
                285302L,
                512,
                false,
                true),
            new TextureContract(
                ArrowNormalAssetPath,
                "fb9ca72c8196b6019ba9dfe5307ee2dda3aa070409d8dcf70f1aa0a045c02286",
                153280L,
                512,
                true,
                false),
            new TextureContract(
                BowBaseColorAssetPath,
                "f0ebd921b1efab2faa55c13d0d875653c02f512bb709e310ea3f1c9401f3fff6",
                2620735L,
                2048,
                false,
                false),
            new TextureContract(
                BowNormalAssetPath,
                "d319a25cd4394bf3c44c35a605f4d1a44b50ad58050b63c4d4da066a1d27fe21",
                2270513L,
                2048,
                true,
                false),
            new TextureContract(
                ClothesBaseColorAssetPath,
                "905929a19cb5f97f29b086547f6ee75478499649abd6265bc169c357f5044b26",
                3479930L,
                2048,
                false,
                false),
            new TextureContract(
                ClothesNormalAssetPath,
                "abd22b8da833589f100a8a443a3f940ecf55e028b0ae3b5a72ec0bebc393909a",
                3065830L,
                2048,
                true,
                false),
            new TextureContract(
                BodyBaseColorAssetPath,
                "1ab45d0b4edac7af1a32f8a558571b9a09b3760ed75c86cfad8b81733c1a6e38",
                4176224L,
                2048,
                false,
                false),
            new TextureContract(
                BodyNormalAssetPath,
                "b444e207b74056602e18291a25c91588c03a69f60e5bcf00303dbe5671b80363",
                2706425L,
                2048,
                true,
                false),
            new TextureContract(
                EyelashesBaseColorAssetPath,
                "f745c9f1510f8f1f91c774bed02679cb170d43e66fb23e280eed2a83d5dc2611",
                4902721L,
                2048,
                false,
                true),
        };

        [MenuItem(ValidateMenuPath, priority = 2086)]
        public static void ValidateFromMenu()
        {
            try
            {
                ValidateOrThrow();
                const string message =
                    "Mixamo Erika evaluation source, hashes, Humanoid rig, materials, and geometry are valid.";
                Debug.Log(message);
                ShowDialog("Mixamo Erika trial validation", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Mixamo Erika trial validation failed", exception.Message);
            }
        }

        [MenuItem(ConfigureMenuPath, priority = 2087)]
        public static void ConfigureFromMenu()
        {
            try
            {
                int changedCount = ConfigureImport();
                ValidateOrThrow();
                string message = changedCount == 0
                    ? "Mixamo Erika trial import settings were already correct."
                    : "Configured and reimported " + changedCount +
                      " Mixamo Erika trial asset(s).";
                Debug.Log(message);
                ShowDialog("Mixamo Erika trial import", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Mixamo Erika trial import failed", exception.Message);
            }
        }

        public static void ValidateForCommandLine()
        {
            ValidateOrThrow();
            Debug.Log("Mixamo Erika trial command-line validation passed.");
        }

        public static void ConfigureForCommandLine()
        {
            int changedCount = ConfigureImport();
            ValidateOrThrow();
            Debug.Log(
                "Mixamo Erika trial import configuration passed; changed " +
                changedCount + " asset(s). Evaluation-only status remains enforced.");
        }

        public static void ValidateSelectedSourceOrThrow()
        {
            ValidateOrThrow();
        }

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            ValidateTrialBoundary(errors);
            ValidateImmutableFiles(errors);
            ValidateImporter(errors);
            ValidateTextures(errors);
            ValidateModel(errors);
            ValidateRejectedAnimationsRemainOutsideAssets(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(errors));
            }
        }

        public static int ConfigureImport()
        {
            var sourceErrors = new List<string>();
            ValidateTrialBoundary(sourceErrors);
            ValidateImmutableFiles(sourceErrors);
            if (sourceErrors.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(sourceErrors));
            }

            ModelImporter modelImporter = AssetImporter.GetAtPath(ModelAssetPath)
                as ModelImporter;
            if (modelImporter == null)
            {
                throw new InvalidOperationException(
                    "Missing or invalid staged Mixamo Erika FBX at '" +
                    ModelAssetPath + "'. Restore the hash-locked trial copy before configuring it.");
            }

            int changedCount = 0;
            if (ConfigureModelImporter(modelImporter))
            {
                changedCount++;
            }

            for (int index = 0; index < TextureContracts.Length; index++)
            {
                TextureContract contract = TextureContracts[index];
                TextureImporter textureImporter = AssetImporter.GetAtPath(contract.AssetPath)
                    as TextureImporter;
                if (textureImporter == null)
                {
                    throw new InvalidOperationException(
                        "Missing or invalid extracted Mixamo texture at '" +
                        contract.AssetPath + "'. Restore the audited extracted copy.");
                }

                if (ConfigureTextureImporter(textureImporter, contract))
                {
                    changedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            return changedCount;
        }

        public static GameObject LoadValidatedModelOrThrow()
        {
            ValidateOrThrow();
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAssetPath);
            if (model == null)
            {
                throw new InvalidOperationException(
                    "Unity could not load the validated Mixamo Erika trial model at '" +
                    ModelAssetPath + "'.");
            }

            return model;
        }

        public static void ValidateGeneratedRootOrThrow(GameObject trialRoot)
        {
            var errors = new List<string>();
            if (trialRoot == null)
            {
                errors.Add("Generated Mixamo Erika trial root is missing.");
                throw new InvalidOperationException(BuildErrorMessage(errors));
            }

            if (!string.Equals(trialRoot.name, ModelRootName, StringComparison.Ordinal))
            {
                errors.Add(
                    "Generated Mixamo Erika root is named '" + trialRoot.name +
                    "'; expected the explicit trial name '" + ModelRootName + "'.");
            }

            Animator animator = trialRoot.GetComponent<Animator>();
            if (animator == null || animator.avatar == null ||
                !animator.avatar.isValid || !animator.avatar.isHuman)
            {
                errors.Add("Generated Mixamo Erika root requires one valid Humanoid Animator.");
            }
            else
            {
                if (animator.runtimeAnimatorController == null)
                {
                    errors.Add(
                        "Generated Mixamo Erika Animator must retain the accepted project controller.");
                }

                if (animator.applyRootMotion)
                {
                    errors.Add(
                        "Generated Mixamo Erika Animator must keep applyRootMotion disabled.");
                }
            }

            Renderer[] renderers = trialRoot.GetComponentsInChildren<Renderer>(true);
            for (int index = 0; index < ExpectedRendererNames.Length; index++)
            {
                string rendererName = ExpectedRendererNames[index];
                Renderer renderer = renderers.SingleOrDefault(value =>
                    string.Equals(value.name, rendererName, StringComparison.Ordinal));
                bool embeddedWeapon =
                    rendererName == EmbeddedBowRendererName ||
                    rendererName == EmbeddedArrowRendererName;
                if (embeddedWeapon)
                {
                    if (renderer != null && renderer.enabled && renderer.gameObject.activeSelf)
                    {
                        errors.Add(
                            "Generated Mixamo Erika retains active rejected embedded prop renderer '" +
                            rendererName + "'. Keep the source skin inactive and use only its baked rigid geometry.");
                    }

                    continue;
                }

                if (renderer == null)
                {
                    errors.Add(
                        "Generated Mixamo Erika is missing source renderer '" +
                        rendererName + "'.");
                    continue;
                }

                if (!renderer.enabled || !renderer.gameObject.activeSelf)
                {
                    errors.Add(
                        "Generated Mixamo Erika source renderer '" + rendererName +
                        "' must remain enabled on its local hierarchy.");
                }

                if (renderer is not SkinnedMeshRenderer skinned ||
                    skinned.sharedMesh == null)
                {
                    errors.Add(
                        "Generated Mixamo Erika source renderer '" + rendererName +
                        "' must remain skinned.");
                    continue;
                }

                if (skinned.quality != SkinQuality.Bone4)
                {
                    errors.Add(
                        "Generated Mixamo Erika source renderer '" + rendererName +
                        "' must use four-bone skinning.");
                }

                if (renderer.shadowCastingMode != ShadowCastingMode.On ||
                    !renderer.receiveShadows)
                {
                    errors.Add(
                        "Generated Mixamo Erika source renderer '" + rendererName +
                        "' must cast and receive strategy-view shadows.");
                }

                ValidateLitMaterials(renderer, "generated renderer '" + rendererName + "'", errors);
            }

            Transform bow = FindDescendant(trialRoot.transform, "HunterBow_Hand");
            Transform arrow = FindDescendant(trialRoot.transform, "HunterArrow_Hand");
            if (bow == null || arrow == null)
            {
                errors.Add(
                    "Generated Mixamo Erika must use the accepted baked trial bow and nocked-arrow hierarchy.");
            }
            else
            {
                ValidateBakedTrialProp(bow, 1.30f, 1.42f, "bow", errors);
                ValidateBakedTrialProp(arrow, 0.72f, 0.80f, "arrow", errors);
                if (animator != null && animator.avatar != null && animator.avatar.isHuman)
                {
                    Transform leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
                    Transform rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                    if (leftHand == null || !bow.IsChildOf(leftHand))
                    {
                        errors.Add("Generated Mixamo Erika baked bow must remain under Humanoid LeftHand.");
                    }

                    if (rightHand == null || !arrow.IsChildOf(rightHand))
                    {
                        errors.Add("Generated Mixamo Erika nocked arrow must remain under Humanoid RightHand.");
                    }
                }

                HunterTrialWeaponPose pose = trialRoot.GetComponent<HunterTrialWeaponPose>();
                if (pose == null ||
                    pose.OrientationFrame != trialRoot.transform ||
                    pose.Bow != bow ||
                    pose.NockedArrow != arrow)
                {
                    errors.Add(
                        "Generated Mixamo Erika requires one bound HunterTrialWeaponPose for its baked props.");
                }
            }

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(errors));
            }
        }

        private static void ValidateBakedTrialProp(
            Transform prop,
            float minimumLength,
            float maximumLength,
            string label,
            List<string> errors)
        {
            MeshFilter filter = prop.GetComponent<MeshFilter>();
            MeshRenderer renderer = prop.GetComponent<MeshRenderer>();
            if (filter == null || filter.sharedMesh == null || renderer == null)
            {
                errors.Add(
                    "Generated Mixamo Erika " + label +
                    " must be a rigid MeshFilter/MeshRenderer prop.");
                return;
            }

            float length = filter.sharedMesh.bounds.size.z;
            if (length < minimumLength || length > maximumLength)
            {
                errors.Add(
                    "Generated Mixamo Erika " + label + " is " + length.ToString("F3") +
                    " m long on its authored axis; expected " + minimumLength.ToString("F2") +
                    "-" + maximumLength.ToString("F2") + " m.");
            }

            if (prop.GetComponent<LineRenderer>() != null)
            {
                errors.Add(
                    "Generated Mixamo Erika " + label +
                    " regressed to the rejected procedural line-renderer weapon.");
            }

            ValidateLitMaterials(renderer, "baked trial " + label, errors);
        }

        public static bool IsReferencedByEnabledBuildScenes()
        {
            EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
            for (int index = 0; index < scenes.Length; index++)
            {
                EditorBuildSettingsScene scene = scenes[index];
                if (!scene.enabled || string.IsNullOrEmpty(scene.path))
                {
                    continue;
                }

                string[] dependencies = AssetDatabase.GetDependencies(scene.path, true);
                if (dependencies.Any(value =>
                        string.Equals(value, ModelAssetPath, StringComparison.Ordinal)))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ConfigureModelImporter(ModelImporter importer)
        {
            bool changed = false;
            changed |= SetValue(importer.globalScale, 1f, value => importer.globalScale = value);
            changed |= SetValue(importer.useFileScale, true, value => importer.useFileScale = value);
            changed |= SetValue(
                importer.bakeAxisConversion,
                true,
                value => importer.bakeAxisConversion = value);
            changed |= SetValue(
                importer.animationType,
                ModelImporterAnimationType.Human,
                value => importer.animationType = value);
            changed |= SetValue(
                importer.avatarSetup,
                ModelImporterAvatarSetup.CreateFromThisModel,
                value => importer.avatarSetup = value);
            changed |= SetValue(
                importer.importAnimation,
                false,
                value => importer.importAnimation = value);
            changed |= SetValue(
                importer.optimizeGameObjects,
                false,
                value => importer.optimizeGameObjects = value);
            changed |= SetValue(
                importer.importAnimatedCustomProperties,
                false,
                value => importer.importAnimatedCustomProperties = value);
            changed |= SetValue(
                importer.importBlendShapes,
                true,
                value => importer.importBlendShapes = value);
            changed |= SetValue(importer.importCameras, false, value => importer.importCameras = value);
            changed |= SetValue(importer.importLights, false, value => importer.importLights = value);
            changed |= SetValue(
                importer.importVisibility,
                false,
                value => importer.importVisibility = value);
            changed |= SetValue(
                importer.importConstraints,
                false,
                value => importer.importConstraints = value);
            changed |= SetValue(
                importer.materialImportMode,
                ModelImporterMaterialImportMode.ImportViaMaterialDescription,
                value => importer.materialImportMode = value);
            changed |= SetValue(importer.isReadable, false, value => importer.isReadable = value);
            changed |= SetValue(importer.addCollider, false, value => importer.addCollider = value);
            changed |= SetValue(importer.keepQuads, false, value => importer.keepQuads = value);
            changed |= SetValue(importer.weldVertices, true, value => importer.weldVertices = value);
            changed |= SetValue(
                importer.optimizeMeshPolygons,
                true,
                value => importer.optimizeMeshPolygons = value);
            changed |= SetValue(
                importer.optimizeMeshVertices,
                true,
                value => importer.optimizeMeshVertices = value);
            changed |= SetValue(
                importer.meshCompression,
                ModelImporterMeshCompression.Off,
                value => importer.meshCompression = value);
            changed |= SetValue(
                importer.generateSecondaryUV,
                false,
                value => importer.generateSecondaryUV = value);
            changed |= SetValue(
                importer.importNormals,
                ModelImporterNormals.Import,
                value => importer.importNormals = value);
            changed |= SetValue(
                importer.importTangents,
                ModelImporterTangents.CalculateMikk,
                value => importer.importTangents = value);
            changed |= SetValue(
                importer.indexFormat,
                ModelImporterIndexFormat.Auto,
                value => importer.indexFormat = value);
            changed |= SetValue(
                importer.preserveHierarchy,
                true,
                value => importer.preserveHierarchy = value);
            changed |= SetValue(
                importer.sortHierarchyByName,
                true,
                value => importer.sortHierarchyByName = value);
            changed |= SetValue(
                importer.skinWeights,
                ModelImporterSkinWeights.Custom,
                value => importer.skinWeights = value);
            changed |= SetValue(
                importer.maxBonesPerVertex,
                4,
                value => importer.maxBonesPerVertex = value);

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static bool ConfigureTextureImporter(
            TextureImporter importer,
            TextureContract contract)
        {
            TextureImporterType expectedType = contract.IsNormal
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            TextureImporterAlphaSource expectedAlpha = contract.UsesAlpha
                ? TextureImporterAlphaSource.FromInput
                : TextureImporterAlphaSource.None;
            bool changed = false;
            changed |= SetValue(importer.textureType, expectedType, value => importer.textureType = value);
            changed |= SetValue(
                importer.sRGBTexture,
                !contract.IsNormal,
                value => importer.sRGBTexture = value);
            changed |= SetValue(importer.alphaSource, expectedAlpha, value => importer.alphaSource = value);
            changed |= SetValue(
                importer.alphaIsTransparency,
                contract.UsesAlpha,
                value => importer.alphaIsTransparency = value);
            changed |= SetValue(importer.mipmapEnabled, true, value => importer.mipmapEnabled = value);
            changed |= SetValue(importer.wrapMode, TextureWrapMode.Repeat, value => importer.wrapMode = value);
            changed |= SetValue(importer.filterMode, FilterMode.Bilinear, value => importer.filterMode = value);
            changed |= SetValue(importer.anisoLevel, 4, value => importer.anisoLevel = value);
            changed |= SetValue(
                importer.maxTextureSize,
                ImportedMaximumTextureSize,
                value => importer.maxTextureSize = value);
            changed |= SetValue(
                importer.npotScale,
                TextureImporterNPOTScale.ToNearest,
                value => importer.npotScale = value);
            changed |= SetValue(importer.isReadable, false, value => importer.isReadable = value);
            changed |= SetValue(
                importer.textureCompression,
                TextureImporterCompression.CompressedHQ,
                value => importer.textureCompression = value);

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

        private static void ValidateTrialBoundary(List<string> errors)
        {
            string manifestAbsolutePath = GetAbsoluteProjectPath(SourceManifestPath);
            if (!File.Exists(manifestAbsolutePath))
            {
                errors.Add(
                    "Missing audited Mixamo trial manifest at '" +
                    SourceManifestPath + "'.");
                return;
            }

            TrialManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<TrialManifest>(
                    File.ReadAllText(manifestAbsolutePath, Encoding.UTF8));
            }
            catch (Exception exception)
            {
                errors.Add(
                    "Mixamo trial manifest is not valid JSON: " + exception.Message);
                return;
            }

            if (manifest == null ||
                manifest.schemaVersion != 3 ||
                !string.Equals(manifest.status, "licensed-game-base", StringComparison.Ordinal) ||
                !manifest.promotionAllowed ||
                !string.Equals(
                    manifest.distributionPolicy,
                    "embedded-end-use-only",
                    StringComparison.Ordinal) ||
                manifest.characterExport == null ||
                manifest.characterExport.bytes != ExpectedModelBytes ||
                !string.Equals(
                    manifest.characterExport.sha256,
                    ExpectedModelSha256,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.characterExport.path,
                    "Original/ErikaArcher_WithBow.fbx",
                    StringComparison.Ordinal) ||
                manifest.acceptance == null ||
                !string.Equals(
                    manifest.acceptance.trialDecision,
                    "model-useful-animation-set-rejected",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.acceptance.productionPromotion,
                    "accepted-model-base",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.acceptance.drawGateResult,
                    "failed",
                    StringComparison.Ordinal) ||
                !string.Equals(
                    manifest.acceptance.gateResult,
                    "rejected-raw-animation-set",
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Mixamo Erika manifest no longer records the licensed, embedded-game " +
                    "model-base decision while keeping the downloaded animation set rejected.");
            }

            ValidateBoundaryText(SourceProvenancePath, errors);
            ValidateBoundaryText(AssetsProvenancePath, errors);
        }

        private static void ValidateBoundaryText(string relativePath, List<string> errors)
        {
            string absolutePath = GetAbsoluteProjectPath(relativePath);
            if (!File.Exists(absolutePath))
            {
                errors.Add("Missing Mixamo trial provenance at '" + relativePath + "'.");
                return;
            }

            string text = File.ReadAllText(absolutePath, Encoding.UTF8);
            if (text.IndexOf("Adobe", StringComparison.OrdinalIgnoreCase) < 0 ||
                text.IndexOf("embedded", StringComparison.OrdinalIgnoreCase) < 0 ||
                text.IndexOf("standalone", StringComparison.OrdinalIgnoreCase) < 0)
            {
                errors.Add(
                    "Mixamo Erika provenance at '" + relativePath +
                    "' must preserve the Adobe, embedded-game, and standalone-file boundaries.");
            }
        }

        private static void ValidateImmutableFiles(List<string> errors)
        {
            ValidateFile(
                ImmutableOriginalPath,
                ExpectedModelBytes,
                ExpectedModelSha256,
                errors);
            ValidateFile(
                ModelAssetPath,
                ExpectedModelBytes,
                ExpectedModelSha256,
                errors);
            for (int index = 0; index < TextureContracts.Length; index++)
            {
                TextureContract contract = TextureContracts[index];
                ValidateFile(
                    contract.AssetPath,
                    contract.ExpectedBytes,
                    contract.ExpectedSha256,
                    errors);
            }
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

        private static void ValidateImporter(List<string> errors)
        {
            ModelImporter importer = AssetImporter.GetAtPath(ModelAssetPath)
                as ModelImporter;
            if (importer == null)
            {
                errors.Add(
                    "Missing ModelImporter for staged Mixamo Erika FBX at '" +
                    ModelAssetPath + "'.");
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
                importer.materialImportMode !=
                    ModelImporterMaterialImportMode.ImportViaMaterialDescription ||
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
                    "Mixamo Erika FBX is not using the audited unoptimized, " +
                    "animation-free, four-weight Humanoid trial import contract. " +
                    ConfigureInstruction);
            }
        }

        private static void ValidateTextures(List<string> errors)
        {
            for (int index = 0; index < TextureContracts.Length; index++)
            {
                TextureContract contract = TextureContracts[index];
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                    contract.AssetPath);
                TextureImporter importer = AssetImporter.GetAtPath(contract.AssetPath)
                    as TextureImporter;
                if (texture == null || importer == null)
                {
                    errors.Add(
                        "Extracted Mixamo texture is not loadable at '" +
                        contract.AssetPath + "'.");
                    continue;
                }

                TextureImporterType expectedType = contract.IsNormal
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;
                TextureImporterAlphaSource expectedAlpha = contract.UsesAlpha
                    ? TextureImporterAlphaSource.FromInput
                    : TextureImporterAlphaSource.None;
                TextureImporterPlatformSettings android =
                    importer.GetPlatformTextureSettings("Android");
                if (texture.width != contract.ExpectedDimension ||
                    texture.height != contract.ExpectedDimension ||
                    importer.textureType != expectedType ||
                    importer.sRGBTexture == contract.IsNormal ||
                    importer.alphaSource != expectedAlpha ||
                    importer.alphaIsTransparency != contract.UsesAlpha ||
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
                        "Extracted Mixamo texture '" + contract.AssetPath +
                        "' is not using the audited trial texture policy. " +
                        ConfigureInstruction);
                }
            }
        }

        private static void ValidateModel(List<string> errors)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelAssetPath);
            if (model == null)
            {
                errors.Add(
                    "Staged Mixamo Erika FBX is not a loadable model at '" +
                    ModelAssetPath + "'.");
                return;
            }

            if (model.transform.localPosition.sqrMagnitude > TransformTolerance ||
                Quaternion.Angle(model.transform.localRotation, Quaternion.identity) >
                    TransformTolerance ||
                (model.transform.localScale - Vector3.one).sqrMagnitude >
                    TransformTolerance)
            {
                errors.Add(
                    "Mixamo Erika model root must remain identity; authored scaling belongs to the generated trial hierarchy.");
            }

            if (model.GetComponentsInChildren<Collider>(true).Length != 0 ||
                model.GetComponentsInChildren<Camera>(true).Length != 0 ||
                model.GetComponentsInChildren<Light>(true).Length != 0 ||
                model.GetComponentsInChildren<Animation>(true).Length != 0)
            {
                errors.Add(
                    "Mixamo Erika source may contain only its Humanoid rig and visual renderers; Collider, Camera, Light, and legacy Animation components are rejected.");
            }

            Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
            SkinnedMeshRenderer[] skinnedRenderers =
                model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            MeshRenderer[] rigidRenderers =
                model.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != ExpectedRendererCount ||
                skinnedRenderers.Length != ExpectedRendererCount ||
                rigidRenderers.Length != 0)
            {
                errors.Add(
                    "Mixamo Erika source exposes " + renderers.Length +
                    " total / " + skinnedRenderers.Length + " skinned / " +
                    rigidRenderers.Length + " rigid renderers; expected six skinned renderers only.");
            }

            var rendererByName = new Dictionary<string, Renderer>(StringComparer.Ordinal);
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (!rendererByName.TryAdd(renderer.name, renderer))
                {
                    errors.Add(
                        "Mixamo Erika source contains duplicate renderer name '" +
                        renderer.name + "'.");
                }
            }

            for (int index = 0; index < ExpectedRendererNames.Length; index++)
            {
                if (!rendererByName.ContainsKey(ExpectedRendererNames[index]))
                {
                    errors.Add(
                        "Mixamo Erika source is missing renderer '" +
                        ExpectedRendererNames[index] + "'.");
                }
            }

            int triangles = 0;
            int vertices = 0;
            var weightedBones = new HashSet<Transform>();
            var materials = new HashSet<Material>();
            for (int rendererIndex = 0;
                 rendererIndex < skinnedRenderers.Length;
                 rendererIndex++)
            {
                SkinnedMeshRenderer renderer = skinnedRenderers[rendererIndex];
                Mesh mesh = renderer.sharedMesh;
                if (mesh == null ||
                    renderer.rootBone == null ||
                    renderer.bones == null ||
                    renderer.bones.Length == 0 ||
                    renderer.bones.Any(value => value == null))
                {
                    errors.Add(
                        "Mixamo Erika renderer '" + renderer.name +
                        "' has an incomplete skin binding.");
                    continue;
                }

                if (!mesh.HasVertexAttribute(VertexAttribute.Normal) ||
                    !mesh.HasVertexAttribute(VertexAttribute.Tangent) ||
                    !mesh.HasVertexAttribute(VertexAttribute.TexCoord0))
                {
                    errors.Add(
                        "Mixamo Erika renderer '" + renderer.name +
                        "' must retain normals, MikkTSpace tangents, and UV0.");
                }

                vertices += mesh.vertexCount;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    triangles += (int)(mesh.GetIndexCount(submesh) / 3L);
                }

                for (int boneIndex = 0; boneIndex < renderer.bones.Length; boneIndex++)
                {
                    weightedBones.Add(renderer.bones[boneIndex]);
                }

                Material[] sharedMaterials = renderer.sharedMaterials;
                if (sharedMaterials.Length == 0 || sharedMaterials.Any(value => value == null))
                {
                    errors.Add(
                        "Mixamo Erika renderer '" + renderer.name +
                        "' has an incomplete material binding.");
                }

                for (int materialIndex = 0;
                     materialIndex < sharedMaterials.Length;
                     materialIndex++)
                {
                    if (sharedMaterials[materialIndex] != null)
                    {
                        materials.Add(sharedMaterials[materialIndex]);
                    }
                }
            }

            if (triangles != ExpectedTriangleCount ||
                vertices != ExpectedVertexCount ||
                weightedBones.Count != ExpectedWeightedBoneCount ||
                materials.Count != ExpectedMaterialCount)
            {
                errors.Add(
                    "Mixamo Erika imported structure changed: found " +
                    triangles + " triangles, " + vertices + " vertices, " +
                    weightedBones.Count + " weighted bones, and " +
                    materials.Count + " materials; expected " +
                    ExpectedTriangleCount + ", " + ExpectedVertexCount + ", " +
                    ExpectedWeightedBoneCount + ", and " + ExpectedMaterialCount + ".");
            }

            string[] materialNames = materials
                .Select(value => value.name)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            string[] expectedMaterialNames = ExpectedMaterialNames
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            if (!materialNames.SequenceEqual(expectedMaterialNames, StringComparer.Ordinal))
            {
                errors.Add(
                    "Mixamo Erika imported material names changed; found: " +
                    string.Join(", ", materialNames) + ".");
            }

            for (int index = 0; index < renderers.Length; index++)
            {
                ValidateLitMaterials(renderers[index], "source renderer '" + renderers[index].name + "'", errors);
            }

            ValidateAvatarAndBounds(model, errors);
        }

        private static void ValidateAvatarAndBounds(
            GameObject model,
            List<string> errors)
        {
            Avatar[] validAvatars = AssetDatabase.LoadAllAssetsAtPath(ModelAssetPath)
                .OfType<Avatar>()
                .Where(value => value.isValid && value.isHuman)
                .ToArray();
            if (validAvatars.Length != 1)
            {
                errors.Add(
                    "Mixamo Erika source exposes " + validAvatars.Length +
                    " valid Humanoid Avatars; expected exactly one. " +
                    ConfigureInstruction);
                return;
            }

            Animator[] sourceAnimators = model.GetComponentsInChildren<Animator>(true);
            if (sourceAnimators.Length != 1 ||
                sourceAnimators[0].transform != model.transform)
            {
                errors.Add(
                    "Mixamo Erika source must expose exactly one Animator on the model root.");
            }

            GameObject instance = UnityEngine.Object.Instantiate(model);
            try
            {
                Animator animator = instance.GetComponent<Animator>();
                if (animator == null)
                {
                    errors.Add("Instantiated Mixamo Erika source has no root Animator.");
                    return;
                }

                animator.avatar = validAvatars[0];
                for (int index = 0; index < RequiredHumanoidBones.Length; index++)
                {
                    HumanBodyBones bone = RequiredHumanoidBones[index];
                    if (animator.GetBoneTransform(bone) == null)
                    {
                        errors.Add(
                            "Mixamo Erika Humanoid mapping is missing '" + bone + "'.");
                    }
                }

                Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    errors.Add("Instantiated Mixamo Erika source has no render bounds.");
                    return;
                }

                Bounds bounds = renderers[0].bounds;
                for (int index = 1; index < renderers.Length; index++)
                {
                    bounds.Encapsulate(renderers[index].bounds);
                }

                if (Mathf.Abs(bounds.size.y - ExpectedModelHeight) >
                    ModelHeightTolerance)
                {
                    errors.Add(
                        "Mixamo Erika source renders " + bounds.size.y.ToString("F6") +
                        " m tall at identity; expected approximately " +
                        ExpectedModelHeight.ToString("F6") + " m.");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateLitMaterials(
            Renderer renderer,
            string label,
            List<string> errors)
        {
            Material[] sharedMaterials = renderer.sharedMaterials;
            for (int index = 0; index < sharedMaterials.Length; index++)
            {
                Material material = sharedMaterials[index];
                if (material == null || material.shader == null ||
                    !string.Equals(
                        material.shader.name,
                        "Universal Render Pipeline/Lit",
                        StringComparison.Ordinal))
                {
                    errors.Add(
                        "Mixamo Erika " + label +
                        " must use non-null URP/Lit materials.");
                    return;
                }
            }
        }

        private static void ValidateRejectedAnimationsRemainOutsideAssets(
            List<string> errors)
        {
            AnimationClip[] importedClips = AssetDatabase.LoadAllAssetsAtPath(ModelAssetPath)
                .OfType<AnimationClip>()
                .Where(value =>
                    !value.name.StartsWith("__preview__", StringComparison.Ordinal))
                .ToArray();
            if (importedClips.Length != 0)
            {
                errors.Add(
                    "Mixamo Erika trial model imported " + importedClips.Length +
                    " animation clip(s); raw Mixamo motion was rejected and must remain outside Assets.");
            }

            string absoluteFolder = GetAbsoluteProjectPath(AssetFolder);
            if (!Directory.Exists(absoluteFolder))
            {
                return;
            }

            string[] fbxFiles = Directory
                .EnumerateFiles(absoluteFolder, "*", SearchOption.AllDirectories)
                .Where(value =>
                    string.Equals(
                        Path.GetExtension(value),
                        ".fbx",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (fbxFiles.Length != 1 ||
                !string.Equals(
                    Path.GetFullPath(fbxFiles[0]),
                    Path.GetFullPath(GetAbsoluteProjectPath(ModelAssetPath)),
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Mixamo Erika Assets folder must contain only the audited model FBX; rejected animation FBXs stay in SourceArt/Trials.");
            }
        }

        private static Transform FindDescendant(Transform root, string name)
        {
            Transform[] descendants = root.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < descendants.Length; index++)
            {
                if (string.Equals(descendants[index].name, name, StringComparison.Ordinal))
                {
                    return descendants[index];
                }
            }

            return null;
        }

        private static string GetAbsoluteProjectPath(string relativePath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException(
                    "Unity did not expose a valid project root for Mixamo trial validation.");
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

        private static bool SetValue<T>(
            T current,
            T expected,
            Action<T> setter)
        {
            if (EqualityComparer<T>.Default.Equals(current, expected))
            {
                return false;
            }

            setter(expected);
            return true;
        }

        private static string BuildErrorMessage(IReadOnlyList<string> errors)
        {
            var builder = new StringBuilder(
                "Mixamo Erika licensed game-base validation failed:");
            for (int index = 0; index < errors.Count; index++)
            {
                builder.Append("\n - ");
                builder.Append(errors[index]);
            }

            builder.Append(
                "\nKeep the hash-locked model embedded in the game, do not distribute it " +
                "standalone, and do not admit the rejected downloaded animations.");
            return builder.ToString();
        }

        private static void ShowDialog(string title, string message)
        {
            if (!UnityEngine.Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(title, message, "OK");
            }
        }

        private readonly struct TextureContract
        {
            public TextureContract(
                string assetPath,
                string expectedSha256,
                long expectedBytes,
                int expectedDimension,
                bool isNormal,
                bool usesAlpha)
            {
                AssetPath = assetPath;
                ExpectedSha256 = expectedSha256;
                ExpectedBytes = expectedBytes;
                ExpectedDimension = expectedDimension;
                IsNormal = isNormal;
                UsesAlpha = usesAlpha;
            }

            public string AssetPath { get; }

            public string ExpectedSha256 { get; }

            public long ExpectedBytes { get; }

            public int ExpectedDimension { get; }

            public bool IsNormal { get; }

            public bool UsesAlpha { get; }
        }

        [Serializable]
        private sealed class TrialManifest
        {
            public int schemaVersion;
            public string status;
            public bool promotionAllowed;
            public string distributionPolicy;
            public TrialCharacterExport characterExport;
            public TrialAcceptance acceptance;
        }

        [Serializable]
        private sealed class TrialCharacterExport
        {
            public string path;
            public long bytes;
            public string sha256;
        }

        [Serializable]
        private sealed class TrialAcceptance
        {
            public string trialDecision;
            public string productionPromotion;
            public string drawGateResult;
            public string gateResult;
        }
    }

    internal sealed class MixamoErikaTrialBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1100;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!MixamoErikaTrialAssetValidator.IsReferencedByEnabledBuildScenes())
            {
                return;
            }

            try
            {
                MixamoErikaTrialAssetValidator.ValidateOrThrow();
            }
            catch (InvalidOperationException exception)
            {
                throw new BuildFailedException(exception.Message);
            }
        }
    }
}
