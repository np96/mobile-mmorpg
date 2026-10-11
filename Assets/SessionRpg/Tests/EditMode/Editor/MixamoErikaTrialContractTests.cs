using System;
using System.Linq;
using NUnit.Framework;
using SessionRpg.Editor.Assets;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class MixamoErikaTrialContractTests
    {
        [Test]
        public void SelectedLicensedBase_IsExplicitHumanoidAndAnimationFree()
        {
            if (!HunterCharacterSourceSelection.UsesMixamoErikaTrial)
            {
                Assert.Ignore("MixamoErikaTrial is not the selected Hunter source.");
            }

            Assert.That(
                HunterCharacterSourceSelection.Selected,
                Is.EqualTo(HunterCharacterSource.MixamoErikaLicensedGameBase));
            Assert.That(
                HunterCharacterSourceSelection.UsesMixamoErikaLicensedGameBase,
                Is.True);
            Assert.That(
                MixamoErikaTrialAssetValidator.ModelAssetPath,
                Does.Contain("/ThirdParty/Trials/AdobeMixamo/ErikaArcher/"));
            Assert.DoesNotThrow(
                MixamoErikaTrialAssetValidator.ValidateSelectedSourceOrThrow);

            ModelImporter importer = AssetImporter.GetAtPath(
                MixamoErikaTrialAssetValidator.ModelAssetPath) as ModelImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.animationType, Is.EqualTo(ModelImporterAnimationType.Human));
            Assert.That(
                importer.avatarSetup,
                Is.EqualTo(ModelImporterAvatarSetup.CreateFromThisModel));
            Assert.That(importer.importAnimation, Is.False);
            Assert.That(importer.optimizeGameObjects, Is.False);
            Assert.That(importer.maxBonesPerVertex, Is.EqualTo(4));

            Avatar[] avatars = AssetDatabase.LoadAllAssetsAtPath(
                    MixamoErikaTrialAssetValidator.ModelAssetPath)
                .OfType<Avatar>()
                .Where(value => value.isValid && value.isHuman)
                .ToArray();
            Assert.That(avatars, Has.Length.EqualTo(1));

            AnimationClip[] importedClips = AssetDatabase.LoadAllAssetsAtPath(
                    MixamoErikaTrialAssetValidator.ModelAssetPath)
                .OfType<AnimationClip>()
                .Where(value =>
                    !value.name.StartsWith("__preview__", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.That(
                importedClips,
                Is.Empty,
                "The rejected Mixamo motion set must not enter the playable base; " +
                "the project controller and Humanoid IK remain authoritative.");
        }

        [Test]
        public void SelectedLicensedBase_PreservesAuditedSemanticRendererMapping()
        {
            if (!HunterCharacterSourceSelection.UsesMixamoErikaTrial)
            {
                Assert.Ignore("MixamoErikaTrial is not the selected Hunter source.");
            }

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                MixamoErikaTrialAssetValidator.ModelAssetPath);
            Assert.That(model, Is.Not.Null);
            string[] rendererNames = model
                .GetComponentsInChildren<SkinnedMeshRenderer>(true)
                .Select(value => value.name)
                .ToArray();
            string[] expectedNames =
            {
                MixamoErikaTrialAssetValidator.ClothesRendererName,
                MixamoErikaTrialAssetValidator.EyelashesRendererName,
                MixamoErikaTrialAssetValidator.EmbeddedBowRendererName,
                MixamoErikaTrialAssetValidator.BodyRendererName,
                MixamoErikaTrialAssetValidator.EyesRendererName,
                MixamoErikaTrialAssetValidator.EmbeddedArrowRendererName,
            };

            Assert.That(rendererNames, Is.EquivalentTo(expectedNames));
            Assert.That(
                MixamoErikaTrialAssetValidator.EmbeddedBowRendererName,
                Is.EqualTo("Erika_Archer_Body_Mesh"));
            Assert.That(
                MixamoErikaTrialAssetValidator.EmbeddedArrowRendererName,
                Is.EqualTo("Erika_Archer_Eyes_Mesh"));
        }
    }
}
