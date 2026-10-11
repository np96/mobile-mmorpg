using System;
using System.Collections.Generic;
using SessionRpg.Core;
using SessionRpg.Unity.Presentation;
using SessionRpg.Unity.UI;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SessionRpg.Editor.Authoring
{
    internal static class AbilityIconAuthoring
    {
        internal const string IconFolder =
            AuthoringAssets.ArtFolder + "/UI/AbilityIcons";

        internal const string CatalogPath =
            AuthoringAssets.SettingsFolder + "/AbilityIconCatalog.asset";

        internal const string TransparentIconSuffix = "_Transparent";

        private static readonly SpecializationId[] PlayerSpecializations =
        {
            SpecializationId.Discipline,
            SpecializationId.Shadow,
            SpecializationId.Fire,
            SpecializationId.Frost,
            SpecializationId.ShadowDance,
            SpecializationId.Mutilate,
            SpecializationId.Marksmanship,
        };

        private static readonly AbilitySlotDirection[] SlotDirections =
        {
            AbilitySlotDirection.None,
            AbilitySlotDirection.Up,
            AbilitySlotDirection.Down,
            AbilitySlotDirection.Left,
            AbilitySlotDirection.Right,
        };

        // These actions are intentionally outside the compact live HUD kit, but
        // their stable AbilityIds and authored art remain valid migration and
        // iteration inputs. Keep them in the catalog without making them castable.
        private static readonly AbilityId[] RetainedSpriteAbilities =
        {
            AbilityId.AspectOfTheDragonhawk,
            AbilityId.AspectOfTheViper,
            AbilityId.Dismantle,
            AbilityId.FeignDeath,
            AbilityId.Flare,
            AbilityId.MastersCall,
            AbilityId.Readiness,
            AbilityId.RoarOfSacrifice,
            AbilityId.SpiderWeb,
            AbilityId.WingClip,
        };

        private static readonly AbilityId[] RequiredSpriteAbilities =
            BuildRequiredSpriteAbilities();

        private static AbilityId[] BuildRequiredSpriteAbilities()
        {
            HashSet<AbilityId> required = new HashSet<AbilityId>();
            for (int specializationIndex = 0;
                 specializationIndex < PlayerSpecializations.Length;
                 specializationIndex++)
            {
                SpecializationId specialization =
                    PlayerSpecializations[specializationIndex];
                AbilityKit arenaKit = SpecializationCatalog.GetKit(specialization);
                for (int slot = 0; slot < AbilityKit.Capacity; slot++)
                {
                    AbilitySlotBinding binding = arenaKit.GetSlotBinding(slot);
                    for (int directionIndex = 0;
                         directionIndex < SlotDirections.Length;
                         directionIndex++)
                    {
                        AbilityId abilityId = binding.GetAbility(
                            SlotDirections[directionIndex]);
                        if (abilityId != AbilityId.None)
                        {
                            required.Add(abilityId);
                        }
                    }
                }

                AbilityKit siegeKit = ModeAbilityKitCatalog.GetKit(
                    PlayableModeId.HeroSiege,
                    specialization);
                for (int slot = 0; slot < AbilityKit.Capacity; slot++)
                {
                    AbilitySlotBinding binding = siegeKit.GetSlotBinding(slot);
                    for (int directionIndex = 0;
                         directionIndex < SlotDirections.Length;
                         directionIndex++)
                    {
                        AbilityId abilityId = binding.GetAbility(
                            SlotDirections[directionIndex]);
                        if (abilityId != AbilityId.None)
                        {
                            required.Add(abilityId);
                        }
                    }
                }

                for (int levelingSlot = 0;
                     levelingSlot < LevelingAbilityUnlockCatalog.TotalAbilityCount;
                     levelingSlot++)
                {
                    AbilityId abilityId = LevelingAbilityUnlockCatalog.GetAbility(
                        specialization,
                        levelingSlot);
                    if (abilityId != AbilityId.None)
                    {
                        required.Add(abilityId);
                    }
                }
            }

            for (int retainedIndex = 0;
                 retainedIndex < RetainedSpriteAbilities.Length;
                 retainedIndex++)
            {
                required.Add(RetainedSpriteAbilities[retainedIndex]);
            }

            AbilityId[] abilities = new AbilityId[required.Count];
            required.CopyTo(abilities);
            Array.Sort(
                abilities,
                (left, right) => ((int)left).CompareTo((int)right));
            return abilities;
        }

        internal static AbilityIconCatalog Build()
        {
            EnsureFolder(AuthoringAssets.ArtFolder, "UI");
            EnsureFolder(AuthoringAssets.ArtFolder + "/UI", "AbilityIcons");
            EnsureFolder("Assets/SessionRpg", "Settings");

            Sprite[] icons = new Sprite[RequiredSpriteAbilities.Length];
            for (int index = 0; index < RequiredSpriteAbilities.Length; index++)
            {
                AbilityId abilityId = RequiredSpriteAbilities[index];
                string path = TransparentIconPath(abilityId);
                ConfigureImporter(path);
                icons[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (icons[index] == null)
                {
                    throw new InvalidOperationException(
                        "Missing imported ability icon for " + abilityId + " at '" + path + "'.");
                }
            }

            AbilityIconCatalog catalog =
                AssetDatabase.LoadAssetAtPath<AbilityIconCatalog>(CatalogPath);
            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<AbilityIconCatalog>();
                AssetDatabase.CreateAsset(catalog, CatalogPath);
            }

            SerializedObject serialized = new SerializedObject(catalog);
            SerializedProperty entries = serialized.FindProperty("_entries");
            entries.arraySize = RequiredSpriteAbilities.Length;
            for (int index = 0; index < RequiredSpriteAbilities.Length; index++)
            {
                SerializedProperty entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_abilityId").intValue =
                    (int)RequiredSpriteAbilities[index];
                entry.FindPropertyRelative("_icon").objectReferenceValue = icons[index];
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        internal static void ValidateOrThrow()
        {
            AbilityIconCatalog catalog =
                AssetDatabase.LoadAssetAtPath<AbilityIconCatalog>(CatalogPath);
            if (catalog == null)
            {
                throw new BuildFailedException(
                    "Missing AbilityIconCatalog. Run Tools > Session RPG > Rebuild Playable Foundation.");
            }

            for (int index = 0; index < RequiredSpriteAbilities.Length; index++)
            {
                AbilityId abilityId = RequiredSpriteAbilities[index];
                if (!catalog.TryGetIcon(abilityId, out Sprite icon) || icon == null)
                {
                    throw new BuildFailedException(
                        "AbilityIconCatalog has no icon for " + abilityId + ". " +
                        "Rebuild the Session RPG playable foundation.");
                }

                string sourcePath = SourceIconPath(abilityId);
                if (AssetImporter.GetAtPath(sourcePath) == null)
                {
                    throw new BuildFailedException(
                        "Missing immutable opaque ability-icon source for " + abilityId +
                        " at '" + sourcePath + "'.");
                }

                string expectedPath = TransparentIconPath(abilityId);
                string actualPath = AssetDatabase.GetAssetPath(icon);
                TextureImporter importer = AssetImporter.GetAtPath(expectedPath) as TextureImporter;
                if (!string.Equals(actualPath, expectedPath, StringComparison.Ordinal) ||
                    importer == null ||
                    !importer.DoesSourceTextureHaveAlpha() ||
                    !importer.alphaIsTransparency)
                {
                    throw new BuildFailedException(
                        "AbilityIconCatalog icon for " + abilityId +
                        " must use the transparent RGBA derivative at '" +
                        expectedPath + "'. Run Tools/Art/extract_ability_icon_alpha.py " +
                        "and rebuild the playable foundation.");
                }
            }

            for (int abilityIndex = 1;
                 abilityIndex < AbilityCatalog.StorageCapacity;
                 abilityIndex++)
            {
                AbilityId abilityId = (AbilityId)abilityIndex;
                bool hasSprite = catalog.TryGetIcon(abilityId, out Sprite icon) &&
                    icon != null;
                if (!hasSprite && !AbilityGlyphGraphic.HasExplicitGlyph(abilityId))
                {
                    throw new BuildFailedException(
                        "Ability " + abilityId + " has neither an authored sprite " +
                        "nor an explicit vector glyph. Add one before rebuilding.");
                }
            }
        }

        internal static string SourceIconPath(AbilityId abilityId)
        {
            return IconFolder + "/" + abilityId + ".png";
        }

        internal static string TransparentIconPath(AbilityId abilityId)
        {
            return IconFolder + "/" + abilityId + TransparentIconSuffix + ".png";
        }

        private static void ConfigureImporter(string path)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Ability icon source was not imported at '" + path + "'.");
            }

            TextureImporterSettings textureSettings = new TextureImporterSettings();
            importer.ReadTextureSettings(textureSettings);

            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                !importer.mipmapEnabled ||
                importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter ||
                !importer.borderMipmap ||
                importer.mipMapsPreserveCoverage ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.maxTextureSize != 256 ||
                textureSettings.spriteMeshType != SpriteMeshType.FullRect ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                !importer.alphaIsTransparency ||
                !importer.sRGBTexture ||
                importer.isReadable ||
                importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed)
            {
                return;
            }

            textureSettings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(textureSettings);
            // Set the high-level sprite properties after SetTextureSettings.
            // The settings snapshot was read from a newly imported Default
            // texture and otherwise restores that old texture type.
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            // The 256 px sources are displayed at several smaller HUD sizes.
            // High-quality mipmaps smooth the authored alpha fringe during
            // minification; an uncompressed import avoids blocky alpha edges.
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.borderMipmap = true;
            importer.mipMapsPreserveCoverage = false;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.sRGBTexture = true;
            importer.isReadable = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 256;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void EnsureFolder(string parent, string name)
        {
            string path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
