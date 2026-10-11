using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Editor.Assets;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class MixamoPlayableClassTrialContractTests
    {
        [TestCase(PlayableClassVisualSource.QuaterniusPrototype, false)]
        [TestCase(PlayableClassVisualSource.MixamoLicensedGameBase, true)]
        public void SourcePolicy_RequiresLicensedIntakeOnlyForSupportedLicensedSource(
            PlayableClassVisualSource source,
            bool expected)
        {
            Assert.That(
                MixamoPlayableClassTrialAssetValidator.RequiresLicensedIntake(source),
                Is.EqualTo(expected));
        }

        [TestCase((PlayableClassVisualSource)(-1))]
        [TestCase((PlayableClassVisualSource)2)]
        [TestCase((PlayableClassVisualSource)int.MaxValue)]
        public void SourcePolicy_RejectsUnknownSourceBeforeAnyFileValidation(
            PlayableClassVisualSource source)
        {
            ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
                () => MixamoPlayableClassTrialAssetValidator.RequiresLicensedIntake(source));
            Assert.That(exception.ParamName, Is.EqualTo("source"));
            Assert.That(exception.ActualValue, Is.EqualTo(source));
        }

        [TestCase(
            ClassId.Healer,
            "Uriel A Plotexia",
            "Assets/ThirdParty/Trials/AdobeMixamo/PlayableClasses/Priest/UrielAPlotexia.fbx",
            "SourceArt/Trials/AdobeMixamo/PlayableClasses/Priest/Original/UrielAPlotexia.fbx",
            "PriestModel_MixamoUrielTrial",
            13619504L,
            "aa42d2a3f3117a58105e6d0a357d017d94260e2d3c7d216d36ad103b0a47dbd1")]
        [TestCase(
            ClassId.Rogue,
            "Vampire A Lusth",
            "Assets/ThirdParty/Trials/AdobeMixamo/PlayableClasses/Rogue/VampireALusth.fbx",
            "SourceArt/Trials/AdobeMixamo/PlayableClasses/Rogue/Original/VampireALusth.fbx",
            "RogueModel_MixamoVampireTrial",
            16130944L,
            "897280db79781384369a3cb031c2049a0227811d286eb344de7647f5a16d99ab")]
        [TestCase(
            ClassId.Mage,
            "Demon T Wiezzorek",
            "Assets/ThirdParty/Trials/AdobeMixamo/PlayableClasses/Mage/DemonTWiezzorek.fbx",
            "SourceArt/Trials/AdobeMixamo/PlayableClasses/Mage/Original/DemonTWiezzorek.fbx",
            "MageModel_MixamoDemonTrial",
            11329840L,
            "dec695008c03b22508973b5e387caaae56dfcbc0c6274429cfef3b8d2e89913a")]
        public void SelectedLicensedBase_IsExplicitHashLockedHumanoidAndAnimationFree(
            ClassId classId,
            string displayName,
            string modelAssetPath,
            string immutableOriginalPath,
            string modelRootName,
            long expectedBytes,
            string expectedSha256)
        {
            if (PlayableClassTrialSourceSelection.UsesQuaterniusPrototype(classId))
            {
                Assert.That(
                    PlayableClassTrialSourceSelection.UsesMixamoLicensedGameBase(classId),
                    Is.False);
                Assert.That(
                    MixamoPlayableClassTrialAssetValidator.IsSelected(classId),
                    Is.False);
                Assert.Ignore(classId + " uses the redistributable CC0 fallback, not a licensed Mixamo base.");
            }

            Assert.That(
                PlayableClassTrialSourceSelection.Get(classId),
                Is.EqualTo(PlayableClassVisualSource.MixamoLicensedGameBase));
            Assert.That(
                PlayableClassTrialSourceSelection.UsesMixamoLicensedGameBase(classId),
                Is.True);
            Assert.That(
                PlayableClassTrialSourceSelection.UsesMixamoTrial(classId),
                Is.True);
            Assert.That(
                PlayableClassTrialSourceSelection.UsesQuaterniusPrototype(classId),
                Is.False);

            MixamoPlayableClassTrialSpec spec =
                MixamoPlayableClassTrialAssetValidator.GetSpecOrThrow(classId);
            Assert.That(spec.ClassId, Is.EqualTo(classId));
            Assert.That(spec.DisplayName, Is.EqualTo(displayName));
            Assert.That(spec.ModelAssetPath, Is.EqualTo(modelAssetPath));
            Assert.That(spec.ImmutableOriginalPath, Is.EqualTo(immutableOriginalPath));
            Assert.That(spec.ModelRootName, Is.EqualTo(modelRootName));
            Assert.That(spec.ExpectedModelBytes, Is.EqualTo(expectedBytes));
            Assert.That(spec.ExpectedModelSha256, Is.EqualTo(expectedSha256));
            Assert.DoesNotThrow(
                MixamoPlayableClassTrialAssetValidator.ValidateSelectedSourcesOrThrow);

            AssertHashLockedFile(modelAssetPath, expectedBytes, expectedSha256);
            AssertHashLockedFile(immutableOriginalPath, expectedBytes, expectedSha256);

            ModelImporter importer = AssetImporter.GetAtPath(modelAssetPath)
                as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(
                importer.avatarSetup,
                Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel));
            Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.optimizeGameObjects, Is.False);
            Assert.That(importer.maxBonesPerVertex, Is.EqualTo(4));

            Avatar[] avatars = AssetDatabase.LoadAllAssetsAtPath(modelAssetPath)
                .OfType<Avatar>()
                .Where(value => value.isValid && value.isHuman)
                .ToArray();
            Assert.That(avatars, Has.Length.EqualTo(1));

            AnimationClip[] importedClips = AssetDatabase
                .LoadAllAssetsAtPath(modelAssetPath)
                .OfType<AnimationClip>()
                .Where(value =>
                    !value.name.StartsWith(
                        "__preview__",
                        StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.That(
                importedClips,
                Is.Empty,
                "Mixamo class bases are model-only; generated project controllers " +
                "and Humanoid choreography remain authoritative.");
        }

        [TestCase(ClassId.Healer, QuaterniusAssetValidator.HealerModelPath, "PriestModel_Quaternius")]
        [TestCase(ClassId.Rogue, QuaterniusAssetValidator.RogueModelPath, "RogueModel_Quaternius")]
        [TestCase(ClassId.Mage, QuaterniusAssetValidator.MageModelPath, "MageModel_Quaternius")]
        public void SelectedFallback_IsHumanoidAndOwnsExactlyOneGeneratedClassRoot(
            ClassId classId,
            string modelAssetPath,
            string rootName)
        {
            if (!PlayableClassTrialSourceSelection.UsesQuaterniusPrototype(classId))
            {
                Assert.Ignore(classId + " does not select the CC0 fallback.");
            }

            Assert.That(
                PlayableClassTrialSourceSelection.Get(classId),
                Is.EqualTo(PlayableClassVisualSource.QuaterniusPrototype));
            Assert.That(PlayableClassTrialSourceSelection.UsesMixamoTrial(classId), Is.False);
            Assert.That(MixamoPlayableClassTrialAssetValidator.IsSelected(classId), Is.False);

            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(modelAssetPath);
            Assert.That(source, Is.Not.Null, "The public fallback source must be included.");
            Animator sourceAnimator = source.GetComponent<Animator>();
            Assert.That(sourceAnimator, Is.Not.Null);
            Assert.That(sourceAnimator.avatar, Is.Not.Null);
            Assert.That(sourceAnimator.avatar.isValid, Is.True);
            Assert.That(sourceAnimator.avatar.isHuman, Is.True);

            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/SessionRpg/Prefabs/Combatant.prefab");
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.transform.Find(rootName), Is.Not.Null);
            string licensedRootName = MixamoPlayableClassTrialAssetValidator
                .GetSpecOrThrow(classId).ModelRootName;
            Assert.That(prefab.transform.Find(licensedRootName), Is.Null);
            Assert.DoesNotThrow(
                () => MixamoPlayableClassTrialAssetValidator.ValidateGeneratedPrefabOrThrow(prefab));
        }

        [Test]
        public void AllFallbackSelections_DoNotRequireLicensedSourceFiles()
        {
            if (MixamoPlayableClassTrialAssetValidator.Specs.Any(value =>
                    !PlayableClassTrialSourceSelection.UsesQuaterniusPrototype(value.ClassId)))
            {
                Assert.Ignore("A licensed class source is selected; its immutable intake remains required.");
            }

            Assert.That(
                MixamoPlayableClassTrialAssetValidator.Specs.All(value =>
                    !MixamoPlayableClassTrialAssetValidator.IsSelected(value.ClassId)),
                Is.True);
            Assert.DoesNotThrow(
                MixamoPlayableClassTrialAssetValidator.ValidateSelectedSourcesOrThrow,
                "The public CC0 checkout must validate without a local licensed Mixamo intake.");
        }

        [Test]
        public void SelectedTrialRegistry_CoversExactlyTheThreeNonHunterClasses()
        {
            Assert.That(
                MixamoPlayableClassTrialAssetValidator.Specs
                    .Select(value => value.ClassId),
                Is.EquivalentTo(new[]
                {
                    ClassId.Healer,
                    ClassId.Rogue,
                    ClassId.Mage,
                }));
            Assert.That(
                MixamoPlayableClassTrialAssetValidator.Specs
                    .Select(value => value.ModelRootName),
                Is.EquivalentTo(new[]
                {
                    "PriestModel_MixamoUrielTrial",
                    "RogueModel_MixamoVampireTrial",
                    "MageModel_MixamoDemonTrial",
                }));
        }

        private static void AssertHashLockedFile(
            string projectRelativePath,
            long expectedBytes,
            string expectedSha256)
        {
            string projectRoot = Directory
                .GetParent(UnityEngine.Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty);
            string absolutePath = Path.GetFullPath(
                Path.Combine(projectRoot, projectRelativePath));
            Assert.That(File.Exists(absolutePath), Is.True, projectRelativePath);
            Assert.That(
                new FileInfo(absolutePath).Length,
                Is.EqualTo(expectedBytes),
                projectRelativePath);

            using FileStream stream = File.OpenRead(absolutePath);
            using SHA256 sha256 = SHA256.Create();
            string actualSha256 = BitConverter.ToString(sha256.ComputeHash(stream))
                .Replace("-", string.Empty)
                .ToLowerInvariant();
            Assert.That(actualSha256, Is.EqualTo(expectedSha256), projectRelativePath);
        }
    }
}
