using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Assets
{
    public static class QuaterniusAssetValidator
    {
        public const string ConfigureMenuPath =
            "Tools/Session RPG/Assets/Configure Quaternius Humanoid Imports";

        public const string ValidateMenuPath =
            "Tools/Session RPG/Assets/Validate Quaternius Assets";

        public const string HealerOutfitPath =
            "Assets/ThirdParty/Quaternius/FantasyOutfits/Characters/Female_Peasant.fbx";

        public const string RogueOutfitPath =
            "Assets/ThirdParty/Quaternius/FantasyOutfits/Characters/Male_Ranger.fbx";

        public const string MageOutfitPath =
            "Assets/ThirdParty/Quaternius/FantasyOutfits/Characters/Female_Ranger.fbx";

        public const string AnimationSourcePath =
            "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/UAL1_Standard.fbx";

        public const string FemaleBaseModelPath =
            "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Characters/Superhero_Female_FullBody.fbx";

        public const string MaleBaseModelPath =
            "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Characters/Superhero_Male_FullBody.fbx";

        public const string HealerModelPath = FemaleBaseModelPath;

        public const string RogueModelPath = MaleBaseModelPath;

        public const string MageModelPath = FemaleBaseModelPath;

        public const string OutfitLicensePath =
            "Assets/ThirdParty/Quaternius/FantasyOutfits/License_Standard.txt";

        public const string AnimationLicensePath =
            "Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/License.txt";

        public const string BaseCharacterLicensePath =
            "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/License_Standard.txt";

        private const string ConfigureInstruction =
            "Run Tools > Session RPG > Assets > Configure Quaternius Humanoid Imports once, then rebuild the playable foundation.";

        private static readonly string[] HumanoidModelPaths =
        {
            FemaleBaseModelPath,
            MaleBaseModelPath,
        };

        private static readonly string[] OutfitModelPaths =
        {
            HealerOutfitPath,
            RogueOutfitPath,
            MageOutfitPath,
        };

        private static readonly string[] TextureFolders =
        {
            "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures",
            "Assets/ThirdParty/Quaternius/UniversalBaseCharacters/Textures",
        };

        private static readonly string[] ExpectedAnimationClips =
        {
            "Death01",
            "Hit_Chest",
            "Hit_Head",
            "Idle_Loop",
            "Interact",
            "Jog_Fwd_Loop",
            "Punch_Cross",
            "Punch_Jab",
            "Roll",
            "Spell_Simple_Enter",
            "Spell_Simple_Exit",
            "Spell_Simple_Idle_Loop",
            "Spell_Simple_Shoot",
            "Sprint_Loop",
            "Sword_Attack",
            "Sword_Idle",
            "Walk_Loop",
        };

        private static readonly HumanBodyBones[] RequiredBones =
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

        [MenuItem(ConfigureMenuPath, priority = 2090)]
        public static void ConfigureFromMenu()
        {
            try
            {
                int changedCount = ConfigureImports();
                ValidateOrThrow();
                string message = changedCount == 0
                    ? "Quaternius Humanoid import settings were already correct."
                    : $"Configured and reimported {changedCount} Quaternius asset(s).";
                Debug.Log(message);
                ShowDialog("Quaternius imports", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Quaternius import failed", exception.Message);
            }
        }

        [MenuItem(ValidateMenuPath, priority = 2091)]
        public static void ValidateFromMenu()
        {
            try
            {
                ValidateOrThrow();
                const string message =
                    "Quaternius models, animations, licenses, and Humanoid Avatars are valid.";
                Debug.Log(message);
                ShowDialog("Quaternius validation", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Quaternius validation failed", exception.Message);
            }
        }

        public static void ConfigureForCommandLine()
        {
            int changedCount = ConfigureImports();
            ValidateOrThrow();
            Debug.Log($"Quaternius import configuration passed; changed {changedCount} asset(s).");
        }

        public static void ValidateForCommandLine()
        {
            ValidateOrThrow();
            Debug.Log("Quaternius command-line validation passed.");
        }

        public static void InspectForCommandLine()
        {
            string[] inspectedPaths = HumanoidModelPaths.Concat(OutfitModelPaths).ToArray();
            for (int pathIndex = 0; pathIndex < inspectedPaths.Length; pathIndex++)
            {
                string path = inspectedPaths[pathIndex];
                Avatar avatar = FindAvatar(path);
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Debug.Log(
                    $"QUATERNIUS_MODEL path={path} avatar={avatar?.name ?? "<none>"} " +
                    $"valid={avatar != null && avatar.isValid} human={avatar != null && avatar.isHuman} " +
                    $"root={model?.name ?? "<none>"}");

                if (model == null)
                {
                    continue;
                }

                GameObject instance = UnityEngine.Object.Instantiate(model);
                try
                {
                    Animator animator = instance.GetComponentInChildren<Animator>(true);
                    if (animator != null && animator.avatar == null)
                    {
                        animator.avatar = avatar;
                    }

                    Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
                    for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                    {
                        Renderer renderer = renderers[rendererIndex];
                        string materialNames = string.Join(
                            ",",
                            renderer.sharedMaterials.Select(value => value != null ? value.name : "<null>"));
                        Debug.Log(
                            $"QUATERNIUS_RENDERER model={model.name} renderer={renderer.name} " +
                            $"boundsCenter={renderer.bounds.center} boundsSize={renderer.bounds.size} " +
                            $"materials={materialNames}");
                    }

                    if (animator != null && animator.avatar != null && animator.avatar.isHuman)
                    {
                        for (int boneIndex = 0; boneIndex < RequiredBones.Length; boneIndex++)
                        {
                            HumanBodyBones bone = RequiredBones[boneIndex];
                            Transform transform = animator.GetBoneTransform(bone);
                            Debug.Log(
                                $"QUATERNIUS_BONE model={model.name} bone={bone} " +
                                $"transform={transform?.name ?? "<none>"}");
                        }
                    }
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(instance);
                }
            }

            AnimationClip[] clips = LoadAnimationClips(AnimationSourcePath).ToArray();
            for (int index = 0; index < clips.Length; index++)
            {
                AnimationClip clip = clips[index];
                Debug.Log(
                    $"QUATERNIUS_CLIP name={clip.name} length={clip.length:F3} " +
                    $"humanMotion={clip.humanMotion} loop={clip.isLooping}");
            }
        }

        public static void ValidateOrThrow()
        {
            List<string> errors = new List<string>();
            ValidateLicense(OutfitLicensePath, errors);
            ValidateLicense(AnimationLicensePath, errors);
            ValidateLicense(BaseCharacterLicensePath, errors);

            for (int index = 0; index < HumanoidModelPaths.Length; index++)
            {
                ValidateHumanoidModel(HumanoidModelPaths[index], errors);
            }

            for (int index = 0; index < OutfitModelPaths.Length; index++)
            {
                ValidateOutfitModel(OutfitModelPaths[index], errors);
            }

            ValidateTextures(errors);
            ValidateAnimationSource(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(errors));
            }
        }

        private static int ConfigureImports()
        {
            List<string> missingPaths = new List<string>();
            for (int index = 0; index < HumanoidModelPaths.Length; index++)
            {
                RequireModelImporter(HumanoidModelPaths[index], missingPaths);
            }

            for (int index = 0; index < OutfitModelPaths.Length; index++)
            {
                RequireModelImporter(OutfitModelPaths[index], missingPaths);
            }

            RequireModelImporter(AnimationSourcePath, missingPaths);
            RequireTextAsset(OutfitLicensePath, missingPaths);
            RequireTextAsset(AnimationLicensePath, missingPaths);
            RequireTextAsset(BaseCharacterLicensePath, missingPaths);
            if (missingPaths.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(missingPaths));
            }

            int changedCount = 0;
            for (int index = 0; index < HumanoidModelPaths.Length; index++)
            {
                if (ConfigureHumanoidImporter(HumanoidModelPaths[index], false))
                {
                    changedCount++;
                }
            }

            for (int index = 0; index < OutfitModelPaths.Length; index++)
            {
                if (ConfigureOutfitImporter(OutfitModelPaths[index]))
                {
                    changedCount++;
                }
            }

            if (ConfigureHumanoidImporter(AnimationSourcePath, true))
            {
                changedCount++;
            }

            if (ConfigureAnimationClips(AnimationSourcePath))
            {
                changedCount++;
            }

            string[] texturePaths = FindTexturePaths();
            for (int index = 0; index < texturePaths.Length; index++)
            {
                if (ConfigureTextureImporter(texturePaths[index]))
                {
                    changedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            return changedCount;
        }

        private static bool ConfigureTextureImporter(string assetPath)
        {
            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"No TextureImporter exists for Quaternius texture '{assetPath}'.");
            }

            bool normalMap = assetPath.EndsWith("_Normal.png", StringComparison.OrdinalIgnoreCase);
            TextureImporterType expectedType = normalMap
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;
            bool changed = false;
            changed |= SetValue(importer.textureType, expectedType, value => importer.textureType = value);
            changed |= SetValue(importer.maxTextureSize, 1024, value => importer.maxTextureSize = value);
            changed |= SetValue(importer.mipmapEnabled, true, value => importer.mipmapEnabled = value);
            changed |= SetValue(
                importer.textureCompression,
                TextureImporterCompression.CompressedHQ,
                value => importer.textureCompression = value);

            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            if (!android.overridden || android.maxTextureSize != 1024)
            {
                android.name = "Android";
                android.overridden = true;
                android.maxTextureSize = 1024;
                android.format = TextureImporterFormat.Automatic;
                android.compressionQuality = 65;
                importer.SetPlatformTextureSettings(android);
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static bool ConfigureHumanoidImporter(string assetPath, bool importAnimation)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"No ModelImporter exists for Quaternius FBX '{assetPath}'.");
            }

            bool changed = false;
            changed |= SetValue(importer.animationType, ModelImporterAnimationType.Human, value =>
                importer.animationType = value);
            changed |= SetValue(importer.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, value =>
                importer.avatarSetup = value);
            changed |= SetValue(importer.importAnimation, importAnimation, value =>
                importer.importAnimation = value);
            changed |= SetValue(importer.optimizeGameObjects, false, value =>
                importer.optimizeGameObjects = value);
            changed |= SetValue(importer.bakeAxisConversion, true, value =>
                importer.bakeAxisConversion = value);
            if (importAnimation)
            {
                changed |= SetValue(importer.motionNodeName, "root", value =>
                    importer.motionNodeName = value);
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static bool ConfigureOutfitImporter(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"No ModelImporter exists for Quaternius outfit FBX '{assetPath}'.");
            }

            bool changed = false;
            changed |= SetValue(importer.animationType, ModelImporterAnimationType.Generic, value =>
                importer.animationType = value);
            changed |= SetValue(importer.avatarSetup, ModelImporterAvatarSetup.CreateFromThisModel, value =>
                importer.avatarSetup = value);
            changed |= SetValue(importer.importAnimation, false, value =>
                importer.importAnimation = value);
            changed |= SetValue(importer.optimizeGameObjects, false, value =>
                importer.optimizeGameObjects = value);
            changed |= SetValue(importer.bakeAxisConversion, true, value =>
                importer.bakeAxisConversion = value);

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static bool ConfigureAnimationClips(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"No ModelImporter exists for Quaternius animation FBX '{assetPath}'.");
            }

            ModelImporterClipAnimation[] clips = importer.clipAnimations;
            if (clips == null || clips.Length == 0)
            {
                clips = importer.defaultClipAnimations;
            }

            if (clips == null || clips.Length == 0)
            {
                throw new InvalidOperationException(
                    $"Quaternius animation FBX '{assetPath}' exposes no animation takes.");
            }

            bool changed = false;
            HashSet<string> cleanNames = new HashSet<string>(StringComparer.Ordinal);
            for (int index = 0; index < clips.Length; index++)
            {
                ModelImporterClipAnimation clip = clips[index];
                string cleanName = CleanClipName(clip.name);
                if (!cleanNames.Add(cleanName))
                {
                    throw new InvalidOperationException(
                        $"Quaternius animation FBX contains duplicate clip name '{cleanName}'.");
                }

                bool loop = cleanName.EndsWith("_Loop", StringComparison.Ordinal);
                changed |= SetValue(clip.name, cleanName, value => clip.name = value);
                changed |= SetValue(clip.loopTime, loop, value => clip.loopTime = value);
                changed |= SetValue(clip.loopPose, loop, value => clip.loopPose = value);
                changed |= SetValue(clip.lockRootRotation, true, value => clip.lockRootRotation = value);
                changed |= SetValue(clip.lockRootHeightY, true, value => clip.lockRootHeightY = value);
                changed |= SetValue(clip.lockRootPositionXZ, true, value => clip.lockRootPositionXZ = value);
                changed |= SetValue(clip.keepOriginalOrientation, true, value =>
                    clip.keepOriginalOrientation = value);
                changed |= SetValue(clip.keepOriginalPositionY, false, value =>
                    clip.keepOriginalPositionY = value);
                changed |= SetValue(clip.heightFromFeet, true, value =>
                    clip.heightFromFeet = value);
                changed |= SetValue(clip.keepOriginalPositionXZ, true, value =>
                    clip.keepOriginalPositionXZ = value);
            }

            if (!changed && importer.clipAnimations.Length > 0)
            {
                return false;
            }

            importer.clipAnimations = clips;
            importer.SaveAndReimport();
            return true;
        }

        private static void ValidateHumanoidModel(string assetPath, List<string> errors)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                errors.Add($"Missing Quaternius base-character FBX at '{assetPath}'.");
                return;
            }

            if (importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                importer.optimizeGameObjects ||
                !importer.bakeAxisConversion)
            {
                errors.Add(
                    $"Base character '{assetPath}' is not configured as an unoptimized Humanoid. " +
                    ConfigureInstruction);
                return;
            }

            Avatar avatar = FindAvatar(assetPath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                errors.Add(
                    $"Unity did not create a valid Human Avatar for base character '{assetPath}'. " +
                    "Open Rig > Configure to inspect the bone mapping; configuration will not retry automatically.");
                return;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
            {
                errors.Add($"Quaternius base character '{assetPath}' is not a loadable model asset.");
                return;
            }

            GameObject instance = UnityEngine.Object.Instantiate(model);
            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>(true);
                if (animator == null)
                {
                    errors.Add($"Quaternius base character '{assetPath}' has no Animator component.");
                    return;
                }

                animator.avatar = avatar;
                List<string> missingBones = new List<string>();
                for (int index = 0; index < RequiredBones.Length; index++)
                {
                    HumanBodyBones bone = RequiredBones[index];
                    if (animator.GetBoneTransform(bone) == null)
                    {
                        missingBones.Add(bone.ToString());
                    }
                }

                if (missingBones.Count > 0)
                {
                    errors.Add(
                        $"Quaternius base character '{assetPath}' is missing Humanoid bones: " +
                        string.Join(", ", missingBones) + ".");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        private static void ValidateOutfitModel(string assetPath, List<string> errors)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                errors.Add($"Missing Quaternius outfit FBX at '{assetPath}'.");
                return;
            }

            if (importer.animationType != ModelImporterAnimationType.Generic ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                importer.importAnimation ||
                importer.optimizeGameObjects ||
                !importer.bakeAxisConversion)
            {
                errors.Add(
                    $"Outfit '{assetPath}' is not configured as an unoptimized mesh-only Generic rig. " +
                    ConfigureInstruction);
                return;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
            {
                errors.Add($"Quaternius outfit '{assetPath}' is not a loadable model asset.");
                return;
            }

            SkinnedMeshRenderer[] renderers = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (renderers.Length == 0)
            {
                errors.Add($"Quaternius outfit '{assetPath}' contains no skinned meshes.");
                return;
            }

            for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
            {
                SkinnedMeshRenderer renderer = renderers[rendererIndex];
                if (renderer.sharedMesh == null || renderer.rootBone == null ||
                    renderer.bones == null || renderer.bones.Length == 0 ||
                    renderer.bones.Any(value => value == null))
                {
                    errors.Add(
                        $"Outfit renderer '{renderer.name}' in '{assetPath}' has an incomplete skin binding.");
                }
            }
        }

        private static void ValidateAnimationSource(List<string> errors)
        {
            ModelImporter importer = AssetImporter.GetAtPath(AnimationSourcePath) as ModelImporter;
            if (importer == null)
            {
                errors.Add($"Missing Quaternius animation FBX at '{AnimationSourcePath}'.");
                return;
            }

            if (importer.animationType != ModelImporterAnimationType.Human ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                !importer.importAnimation ||
                importer.optimizeGameObjects ||
                !importer.bakeAxisConversion)
            {
                errors.Add(
                    $"Animation library '{AnimationSourcePath}' is not configured as an unoptimized Humanoid. " +
                    ConfigureInstruction);
                return;
            }

            Avatar avatar = FindAvatar(AnimationSourcePath);
            if (avatar == null || !avatar.isValid || !avatar.isHuman)
            {
                errors.Add(
                    $"Unity did not create a valid Human Avatar for '{AnimationSourcePath}'. " +
                    "The in-place library cannot be used until its Rig import succeeds.");
            }

            ModelImporterClipAnimation[] importedClipSettings = importer.clipAnimations;
            if (importedClipSettings == null || importedClipSettings.Length == 0)
            {
                errors.Add(
                    $"Animation library '{AnimationSourcePath}' has no authored clip import settings. " +
                    ConfigureInstruction);
            }
            else
            {
                for (int index = 0; index < importedClipSettings.Length; index++)
                {
                    ModelImporterClipAnimation clip = importedClipSettings[index];
                    bool expectedLoop = clip.name.EndsWith("_Loop", StringComparison.Ordinal);
                    if (clip.loopTime != expectedLoop ||
                        clip.loopPose != expectedLoop ||
                        !clip.lockRootRotation ||
                        !clip.lockRootHeightY ||
                        !clip.lockRootPositionXZ ||
                        !clip.keepOriginalOrientation ||
                        clip.keepOriginalPositionY ||
                        !clip.heightFromFeet ||
                        !clip.keepOriginalPositionXZ)
                    {
                        errors.Add(
                            $"Animation clip '{clip.name}' is not using the in-place, feet-grounded " +
                            "Quaternius import policy. " + ConfigureInstruction);
                        break;
                    }
                }
            }

            Dictionary<string, AnimationClip> clips = LoadAnimationClips(AnimationSourcePath)
                .ToDictionary(value => value.name, StringComparer.Ordinal);
            for (int index = 0; index < ExpectedAnimationClips.Length; index++)
            {
                string clipName = ExpectedAnimationClips[index];
                if (!clips.TryGetValue(clipName, out AnimationClip clip))
                {
                    errors.Add($"Animation library is missing required clip '{clipName}'.");
                }
                else if (!clip.humanMotion)
                {
                    errors.Add($"Animation clip '{clipName}' did not import as Humanoid motion.");
                }
            }
        }

        private static void ValidateTextures(List<string> errors)
        {
            string[] texturePaths = FindTexturePaths();
            if (texturePaths.Length == 0)
            {
                errors.Add("No Quaternius textures were imported from the verified Standard archives.");
                return;
            }

            for (int index = 0; index < texturePaths.Length; index++)
            {
                string path = texturePaths[index];
                TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    errors.Add($"Quaternius texture '{path}' has no TextureImporter.");
                    continue;
                }

                bool normalMap = path.EndsWith("_Normal.png", StringComparison.OrdinalIgnoreCase);
                TextureImporterType expectedType = normalMap
                    ? TextureImporterType.NormalMap
                    : TextureImporterType.Default;
                TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
                if (importer.textureType != expectedType ||
                    importer.maxTextureSize > 1024 ||
                    !android.overridden ||
                    android.maxTextureSize > 1024)
                {
                    errors.Add(
                        $"Texture '{path}' is not using the mobile Quaternius import policy. " +
                        ConfigureInstruction);
                }
            }
        }

        private static string[] FindTexturePaths()
        {
            return AssetDatabase.FindAssets("t:Texture2D", TextureFolders)
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(value => value.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }

        private static IEnumerable<AnimationClip> LoadAnimationClips(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Where(value => !value.name.StartsWith("__preview__", StringComparison.Ordinal));
        }

        private static Avatar FindAvatar(string assetPath)
        {
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<Avatar>()
                .SingleOrDefault(value => value.isValid && value.isHuman);
        }

        private static void ValidateLicense(string assetPath, List<string> errors)
        {
            TextAsset license = AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath);
            if (license == null)
            {
                errors.Add($"Missing retained Quaternius license at '{assetPath}'.");
                return;
            }

            string text = license.text;
            if (text.IndexOf("CC0 1.0", StringComparison.OrdinalIgnoreCase) < 0 ||
                text.IndexOf("publicdomain/zero/1.0", StringComparison.OrdinalIgnoreCase) < 0)
            {
                errors.Add($"Retained license '{assetPath}' does not contain the expected CC0 declaration.");
            }
        }

        private static void RequireModelImporter(string assetPath, List<string> errors)
        {
            if (!(AssetImporter.GetAtPath(assetPath) is ModelImporter))
            {
                errors.Add($"Missing or unimported Quaternius FBX at '{assetPath}'.");
            }
        }

        private static void RequireTextAsset(string assetPath, List<string> errors)
        {
            if (AssetDatabase.LoadAssetAtPath<TextAsset>(assetPath) == null)
            {
                errors.Add($"Missing retained Quaternius license at '{assetPath}'.");
            }
        }

        private static string CleanClipName(string value)
        {
            int separator = value.LastIndexOf('|');
            return separator >= 0 ? value.Substring(separator + 1) : value;
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

        private static string BuildErrorMessage(List<string> errors)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("Quaternius asset validation failed with ");
            builder.Append(errors.Count);
            builder.AppendLine(errors.Count == 1 ? " issue:" : " issues:");
            for (int index = 0; index < errors.Count; index++)
            {
                builder.Append(index + 1);
                builder.Append(". ");
                builder.AppendLine(errors[index]);
            }

            builder.Append(
                "Only the verified CC0 Standard archives are supported. " +
                "Configuration performs one explicit import pass and never schedules retries.");
            return builder.ToString();
        }

        private static void ShowDialog(string title, string message)
        {
            if (!UnityEngine.Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(title, message, "OK");
            }
        }
    }
}
