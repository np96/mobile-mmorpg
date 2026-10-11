using NUnit.Framework;
using SessionRpg.Editor.Assets;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class HunterOutfitAlbedoContractTests
    {
        private const string GeneratedMaterialPath =
            "Assets/SessionRpg/Art/Characters/Hunter_Outfit.mat";

        private const string CompatibleNormalMapPath =
            "Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Ranger_Normal.png";

        private const string CombatantPrefabPath =
            "Assets/SessionRpg/Prefabs/Combatant.prefab";

        [Test]
        public void FirstPartyAlbedo_MeetsAuditedMobileImportContract()
        {
            if (!HunterCharacterSourceSelection.UsesQuaterniusPrototype)
            {
                Assert.That(
                    HunterCharacterSourceSelection.Selected,
                    Is.Not.EqualTo(HunterCharacterSource.QuaterniusPrototype));
                return;
            }

            Assert.DoesNotThrow(HunterOutfitAlbedoValidator.ValidateOrThrow);

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(
                HunterOutfitAlbedoValidator.SourceAssetPath);
            TextureImporter importer = AssetImporter.GetAtPath(
                HunterOutfitAlbedoValidator.SourceAssetPath) as TextureImporter;
            Assert.That(texture, Is.Not.Null);
            Assert.That(importer, Is.Not.Null);
            Assert.That(texture.width, Is.EqualTo(1024));
            Assert.That(texture.height, Is.EqualTo(1024));
            Assert.That(importer.textureType, Is.EqualTo(TextureImporterType.Default));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.alphaSource, Is.EqualTo(TextureImporterAlphaSource.None));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.anisoLevel, Is.EqualTo(4));
            Assert.That(importer.maxTextureSize, Is.EqualTo(1024));
            Assert.That(importer.npotScale, Is.EqualTo(TextureImporterNPOTScale.ToNearest));
            Assert.That(importer.isReadable, Is.False);

            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            Assert.That(android.overridden, Is.True);
            Assert.That(android.maxTextureSize, Is.EqualTo(1024));
            Assert.That(android.format, Is.EqualTo(TextureImporterFormat.Automatic));
            Assert.That(
                android.textureCompression,
                Is.EqualTo(TextureImporterCompression.Compressed));
            Assert.That(android.compressionQuality, Is.EqualTo(65));
            Assert.That(android.crunchedCompression, Is.False);
        }

        [Test]
        public void GeneratedHunterMaterial_UsesFirstPartyAlbedoAndCompatibleNormal()
        {
            if (!HunterCharacterSourceSelection.UsesQuaterniusPrototype)
            {
                Assert.That(
                    HunterCharacterSourceSelection.Selected,
                    Is.Not.EqualTo(HunterCharacterSource.QuaterniusPrototype));
                return;
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                GeneratedMaterialPath);
            Assert.That(
                material,
                Is.Not.Null,
                "Rebuild the playable foundation after changing Hunter art inputs.");

            Texture baseMap = material.GetTexture("_BaseMap");
            Texture normalMap = material.GetTexture("_BumpMap");
            Assert.That(baseMap, Is.Not.Null);
            Assert.That(normalMap, Is.Not.Null);
            Assert.That(
                AssetDatabase.GetAssetPath(baseMap),
                Is.EqualTo(HunterOutfitAlbedoValidator.SourceAssetPath));
            Assert.That(
                AssetDatabase.GetAssetPath(normalMap),
                Is.EqualTo(CompatibleNormalMapPath),
                "The first-party repaint preserves the Ranger UV islands, so the " +
                "existing tangent-space normal remains compatible.");
            Assert.That(material.IsKeywordEnabled("_NORMALMAP"), Is.True);
            Assert.That(
                material.GetTexture("_MetallicGlossMap"),
                Is.Null,
                "Do not bind the Quaternius ORM directly: its occlusion/roughness/" +
                "metallic channels do not match the URP Lit metallic/smoothness packing.");
        }

        [Test]
        public void SelectedHunterSource_ValidatesOnlyItsActiveAssetContract()
        {
            Assert.DoesNotThrow(
                HunterProductionAssetValidator.ValidateSelectedSourceOrThrow,
                "The dormant production validator must not query missing files while another Hunter source is selected.");
            if (HunterCharacterSourceSelection.UsesMixamoErikaTrial)
            {
                Assert.DoesNotThrow(
                    MixamoErikaTrialAssetValidator.ValidateSelectedSourceOrThrow,
                    "The selected Mixamo game base must satisfy its independent source contract.");
            }

            GameObject combatantPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                CombatantPrefabPath);
            Assert.That(combatantPrefab, Is.Not.Null);
            Assert.DoesNotThrow(
                () => HunterProductionAssetValidator.ValidateGeneratedPrefabOrThrow(
                    combatantPrefab),
                "The tracked Combatant.prefab must match the source-level Hunter selection; switch, configure, and rebuild cannot be separated.");

            if (HunterCharacterSourceSelection.UsesProductionV1)
            {
                Assert.That(
                    HunterCharacterSourceSelection.Selected,
                    Is.EqualTo(HunterCharacterSource.ProductionV1));
                Assert.That(
                    HunterProductionAssetValidator.ModelAssetPath,
                    Does.EndWith("/HunterProductionV1/HunterProductionV1.fbx"));
            }
            else if (HunterCharacterSourceSelection.UsesMixamoErikaTrial)
            {
                Assert.That(
                    HunterCharacterSourceSelection.Selected,
                    Is.EqualTo(HunterCharacterSource.MixamoErikaLicensedGameBase));
                Assert.That(
                    MixamoErikaTrialAssetValidator.ModelAssetPath,
                    Does.EndWith("/ErikaArcher_WithBow.fbx"));
            }
            else
            {
                Assert.That(
                    HunterCharacterSourceSelection.Selected,
                    Is.EqualTo(HunterCharacterSource.QuaterniusPrototype));
            }
        }
    }
}
