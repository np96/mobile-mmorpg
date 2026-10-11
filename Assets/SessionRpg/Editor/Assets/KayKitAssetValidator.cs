using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SessionRpg.Editor.Assets
{
    public static class KayKitAssetValidator
    {
        public const string ConfigureMenuPath =
            "Tools/Session RPG/Assets/Configure KayKit Rig Compatibility";

        public const string ValidateMenuPath =
            "Tools/Session RPG/Assets/Validate KayKit Assets";

        private const string RigFixInstruction =
            "Run Tools > Session RPG > Assets > Configure KayKit Rig Compatibility.";

        private const float MinimumSharedRigCoverage = 0.7f;
        private const int MinimumSharedRigTransforms = 20;

        private static readonly RequiredAsset[] RequiredAssets =
        {
            RequiredAsset.Model(
                "skeleton boss",
                "Assets/ThirdParty/KayKit/Skeletons/Characters/Skeleton_Warrior.fbx",
                "KayKit Skeletons Free 1.1"),
            RequiredAsset.Texture(
                "dungeon atlas texture",
                "Assets/ThirdParty/KayKit/Dungeon/Environment/dungeon_texture.png",
                "KayKit Dungeon Pack Free 1.1"),
            RequiredAsset.Model(
                "dungeon floor module",
                "Assets/ThirdParty/KayKit/Dungeon/Environment/floor_tile_large.fbx",
                "KayKit Dungeon Pack Free 1.1"),
            RequiredAsset.Model(
                "dungeon wall module",
                "Assets/ThirdParty/KayKit/Dungeon/Environment/wall.fbx",
                "KayKit Dungeon Pack Free 1.1"),
            RequiredAsset.Model(
                "dungeon stair module",
                "Assets/ThirdParty/KayKit/Dungeon/Environment/stairs.fbx",
                "KayKit Dungeon Pack Free 1.1"),
            RequiredAsset.Model(
                "dungeon pillar module",
                "Assets/ThirdParty/KayKit/Dungeon/Environment/pillar.fbx",
                "KayKit Dungeon Pack Free 1.1"),
            RequiredAsset.License(
                "Adventurers license",
                "Assets/ThirdParty/KayKit/Adventurers/License.txt",
                "KayKit Adventurers Free 2.0"),
            RequiredAsset.License(
                "Character Animations license",
                "Assets/ThirdParty/KayKit/CharacterAnimations/License.txt",
                "KayKit Character Animations Free 1.1"),
            RequiredAsset.License(
                "Dungeon license",
                "Assets/ThirdParty/KayKit/Dungeon/License.txt",
                "KayKit Dungeon Pack Free 1.1"),
            RequiredAsset.License(
                "Skeletons license",
                "Assets/ThirdParty/KayKit/Skeletons/License.txt",
                "KayKit Skeletons Free 1.1"),
        };

        private static readonly string[] CharacterModelPaths =
        {
            "Assets/ThirdParty/KayKit/Skeletons/Characters/Skeleton_Warrior.fbx",
        };

        private static readonly string[] AnimationModelPaths =
        {
            "Assets/ThirdParty/KayKit/CharacterAnimations/Rig_Medium/Rig_Medium_General.fbx",
            "Assets/ThirdParty/KayKit/CharacterAnimations/Rig_Medium/Rig_Medium_MovementBasic.fbx",
            "Assets/ThirdParty/KayKit/CharacterAnimations/Rig_Medium/Rig_Medium_CombatMelee.fbx",
            "Assets/ThirdParty/KayKit/CharacterAnimations/Rig_Medium/Rig_Medium_CombatRanged.fbx",
        };

        [MenuItem(ValidateMenuPath, priority = 2100)]
        public static void ValidateFromMenu()
        {
            try
            {
                ValidateOrThrow();
                const string message = "KayKit assets, licenses, and rig settings are valid.";
                Debug.Log(message);
                ShowDialog("KayKit validation", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("KayKit validation failed", exception.Message);
            }
        }

        [MenuItem(ConfigureMenuPath, priority = 2101)]
        public static void ConfigureRigCompatibilityFromMenu()
        {
            try
            {
                int changedCount = ConfigureRigCompatibility();
                ValidateOrThrow();

                string message = changedCount == 0
                    ? "KayKit rig import settings were already correct."
                    : $"Configured and reimported {changedCount} KayKit rig asset(s).";

                Debug.Log(message);
                ShowDialog("KayKit rig compatibility", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("KayKit rig configuration failed", exception.Message);
            }
        }

        public static void ValidateForCommandLine()
        {
            ValidateOrThrow();
            Debug.Log("KayKit command-line validation passed.");
        }

        public static void ConfigureRigCompatibilityForCommandLine()
        {
            int changedCount = ConfigureRigCompatibility();
            ValidateOrThrow();
            Debug.Log($"KayKit command-line rig configuration passed; changed {changedCount} asset(s).");
        }

        public static void ValidateOrThrow()
        {
            List<string> errors = new List<string>();
            ValidateRequiredAssets(errors);
            ValidateRigSettings(errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(errors));
            }
        }

        public static int ConfigureRigCompatibility()
        {
            List<string> errors = new List<string>();
            ValidateRequiredAssets(errors);
            ValidateRigSources(errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(errors));
            }

            int changedCount = 0;

            for (int index = 0; index < CharacterModelPaths.Length; index++)
            {
                if (ConfigureGenericRig(CharacterModelPaths[index]))
                {
                    changedCount++;
                }
            }

            for (int index = 0; index < AnimationModelPaths.Length; index++)
            {
                if (ConfigureGenericRig(AnimationModelPaths[index]))
                {
                    changedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            return changedCount;
        }

        private static bool ConfigureGenericRig(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"KayKit rig configuration could not find a ModelImporter at '{assetPath}'. " +
                    "Restore the FBX from the verified free KayKit archive and reimport it.");
            }

            bool changed = false;

            if (importer.animationType != ModelImporterAnimationType.Generic)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }

            if (importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel)
            {
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                changed = true;
            }

            if (!importer.importAnimation)
            {
                importer.importAnimation = true;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static void ValidateRequiredAssets(List<string> errors)
        {
            for (int index = 0; index < RequiredAssets.Length; index++)
            {
                RequiredAsset requiredAsset = RequiredAssets[index];
                UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(requiredAsset.Path);

                if (asset == null)
                {
                    errors.Add(
                        $"Missing {requiredAsset.Label} at '{requiredAsset.Path}'. " +
                        $"Restore it from {requiredAsset.SourcePack}.");
                    continue;
                }

                switch (requiredAsset.Kind)
                {
                    case RequiredAssetKind.Model:
                        if (!(asset is GameObject) ||
                            !(AssetImporter.GetAtPath(requiredAsset.Path) is ModelImporter))
                        {
                            errors.Add(
                                $"The {requiredAsset.Label} at '{requiredAsset.Path}' is not a valid imported FBX. " +
                                $"Restore the original FBX from {requiredAsset.SourcePack} and reimport it.");
                        }

                        break;

                    case RequiredAssetKind.Texture:
                        if (!(asset is Texture2D) ||
                            !(AssetImporter.GetAtPath(requiredAsset.Path) is TextureImporter))
                        {
                            errors.Add(
                                $"The {requiredAsset.Label} at '{requiredAsset.Path}' is not a valid texture. " +
                                $"Restore the original PNG from {requiredAsset.SourcePack} and reimport it.");
                        }

                        break;

                    case RequiredAssetKind.License:
                        ValidateLicense(requiredAsset, asset, errors);
                        break;

                    default:
                        errors.Add($"Unknown validation kind for '{requiredAsset.Path}'.");
                        break;
                }
            }

            for (int index = 0; index < AnimationModelPaths.Length; index++)
            {
                string path = AnimationModelPaths[index];
                if (!(AssetDatabase.LoadMainAssetAtPath(path) is GameObject) ||
                    !(AssetImporter.GetAtPath(path) is ModelImporter))
                {
                    errors.Add(
                        $"Missing or invalid Rig_Medium animation FBX at '{path}'. " +
                        "Restore it from KayKit Character Animations Free 1.1.");
                }
            }
        }

        private static void ValidateLicense(
            RequiredAsset requiredAsset,
            UnityEngine.Object asset,
            List<string> errors)
        {
            TextAsset license = asset as TextAsset;
            if (license == null)
            {
                errors.Add(
                    $"The retained license at '{requiredAsset.Path}' is not readable as text. " +
                    $"Restore License.txt from {requiredAsset.SourcePack}.");
                return;
            }

            string text = license.text;
            bool identifiesCc0 =
                text.IndexOf("Creative Commons Zero", StringComparison.OrdinalIgnoreCase) >= 0 &&
                text.IndexOf("publicdomain/zero/1.0", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!identifiesCc0)
            {
                errors.Add(
                    $"The retained license at '{requiredAsset.Path}' does not contain the expected CC0 declaration. " +
                    $"Restore the unmodified License.txt from {requiredAsset.SourcePack}.");
            }
        }

        private static void ValidateRigSources(List<string> errors)
        {
            ModelImporter referenceImporter =
                AssetImporter.GetAtPath(CharacterModelPaths[0]) as ModelImporter;

            if (referenceImporter == null)
            {
                errors.Add(
                    $"Cannot inspect the reference boss rig at '{CharacterModelPaths[0]}'. " +
                    "Restore the FBX before configuring rigs.");
                return;
            }

            HashSet<string> referenceNames = GetTransformNames(referenceImporter);
            if (referenceNames.Count < 12)
            {
                errors.Add(
                    $"The reference Mage FBX exposes only {referenceNames.Count} rig transforms. " +
                    "Do not rewrite its rig automatically; restore and reimport the original KayKit FBX.");
                return;
            }

            for (int index = 0; index < CharacterModelPaths.Length; index++)
            {
                ValidateCompatibleTransformSet(
                    CharacterModelPaths[index],
                    referenceNames,
                    errors);
            }

            for (int index = 0; index < AnimationModelPaths.Length; index++)
            {
                ValidateCompatibleTransformSet(
                    AnimationModelPaths[index],
                    referenceNames,
                    errors);
            }
        }

        private static void ValidateCompatibleTransformSet(
            string assetPath,
            HashSet<string> referenceNames,
            List<string> errors)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            HashSet<string> candidateNames = GetTransformNames(importer);
            int smallerSetCount = Math.Min(referenceNames.Count, candidateNames.Count);

            if (smallerSetCount < 12)
            {
                errors.Add(
                    $"Rig compatibility for '{assetPath}' is not safely determinable: " +
                    $"only {candidateNames.Count} transform names were imported. " +
                    "Leave its import settings unchanged and restore the original KayKit FBX.");
                return;
            }

            int sharedCount = 0;
            foreach (string name in candidateNames)
            {
                if (referenceNames.Contains(name))
                {
                    sharedCount++;
                }
            }

            float sharedCoverage = (float)sharedCount / smallerSetCount;
            if (sharedCount < MinimumSharedRigTransforms ||
                sharedCoverage < MinimumSharedRigCoverage)
            {
                errors.Add(
                    $"Rig compatibility for '{assetPath}' is not safely determinable: " +
                    $"only {sharedCount} of {smallerSetCount} expected transform names match the Mage rig. " +
                    "Do not auto-configure this file; verify that it came from a Rig_Medium KayKit pack.");
            }
        }

        private static HashSet<string> GetTransformNames(ModelImporter importer)
        {
            HashSet<string> names = new HashSet<string>(StringComparer.Ordinal);
            string[] transformPaths = importer.transformPaths;

            for (int index = 0; index < transformPaths.Length; index++)
            {
                string path = transformPaths[index];
                int separatorIndex = path.LastIndexOf('/');
                string name = separatorIndex >= 0 ? path.Substring(separatorIndex + 1) : path;

                if (!string.IsNullOrEmpty(name))
                {
                    names.Add(name);
                }
            }

            return names;
        }

        private static void ValidateRigSettings(List<string> errors)
        {
            for (int index = 0; index < CharacterModelPaths.Length; index++)
            {
                ValidateRigImporter(CharacterModelPaths[index], false, errors);
            }

            for (int index = 0; index < AnimationModelPaths.Length; index++)
            {
                ValidateRigImporter(AnimationModelPaths[index], true, errors);
            }
        }

        private static void ValidateRigImporter(
            string assetPath,
            bool requireAnimationClip,
            List<string> errors)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath) as ModelImporter;
            if (importer == null)
            {
                return;
            }

            if (importer.animationType != ModelImporterAnimationType.Generic ||
                importer.avatarSetup != ModelImporterAvatarSetup.CreateFromThisModel ||
                !importer.importAnimation)
            {
                errors.Add(
                    $"Rig settings for '{assetPath}' are incompatible with the shared KayKit Rig_Medium setup. " +
                    RigFixInstruction);
                return;
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            Animator animator = model != null ? model.GetComponent<Animator>() : null;
            if (animator == null || animator.avatar == null || !animator.avatar.isValid)
            {
                errors.Add(
                    $"Unity did not generate a valid Generic Avatar for '{assetPath}'. " +
                    "Select the FBX, verify Rig > Animation Type is Generic and Avatar Definition is Create From This Model, then reimport it.");
            }

            if (requireAnimationClip && !ContainsAnimationClip(assetPath))
            {
                errors.Add(
                    $"The Rig_Medium animation source '{assetPath}' contains no imported AnimationClip. " +
                    "Restore it from KayKit Character Animations Free 1.1 and enable Import Animation.");
            }
        }

        private static bool ContainsAnimationClip(string assetPath)
        {
            UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            for (int index = 0; index < assets.Length; index++)
            {
                if (assets[index] is AnimationClip clip &&
                    !clip.name.StartsWith("__preview__", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static string BuildErrorMessage(List<string> errors)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("KayKit asset validation failed with ");
            builder.Append(errors.Count);
            builder.AppendLine(errors.Count == 1 ? " issue:" : " issues:");

            for (int index = 0; index < errors.Count; index++)
            {
                builder.Append(index + 1);
                builder.Append(". ");
                builder.AppendLine(errors[index]);
            }

            builder.Append(
                "Expected provenance is documented in THIRD_PARTY_NOTICES.md. " +
                "Do not substitute paid EXTRA/SOURCE-tier files.");
            return builder.ToString();
        }

        private static void ShowDialog(string title, string message)
        {
            if (!UnityEngine.Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(title, message, "OK");
            }
        }

        private enum RequiredAssetKind
        {
            Model,
            Texture,
            License,
        }

        private readonly struct RequiredAsset
        {
            private RequiredAsset(
                string label,
                string path,
                string sourcePack,
                RequiredAssetKind kind)
            {
                Label = label;
                Path = path;
                SourcePack = sourcePack;
                Kind = kind;
            }

            public RequiredAssetKind Kind { get; }

            public string Label { get; }

            public string Path { get; }

            public string SourcePack { get; }

            public static RequiredAsset License(string label, string path, string sourcePack)
            {
                return new RequiredAsset(label, path, sourcePack, RequiredAssetKind.License);
            }

            public static RequiredAsset Model(string label, string path, string sourcePack)
            {
                return new RequiredAsset(label, path, sourcePack, RequiredAssetKind.Model);
            }

            public static RequiredAsset Texture(string label, string path, string sourcePack)
            {
                return new RequiredAsset(label, path, sourcePack, RequiredAssetKind.Texture);
            }
        }
    }

    internal sealed class KayKitBuildValidator : IPreprocessBuildWithReport
    {
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            try
            {
                KayKitAssetValidator.ValidateOrThrow();
                QuaterniusAssetValidator.ValidateOrThrow();
            }
            catch (InvalidOperationException exception)
            {
                throw new BuildFailedException(exception.Message);
            }
        }
    }
}
