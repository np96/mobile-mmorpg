using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SessionRpg.Editor.Authoring
{
    internal static class HudMedallionFrameAuthoring
    {
        internal const string SpritePath =
            "Assets/SessionRpg/Art/UI/Hud/AbilityMedallionFrame.png";

        internal static Sprite Build()
        {
            ConfigureImporter();
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (sprite == null)
            {
                throw new InvalidOperationException(
                    "Missing imported HUD medallion frame at '" + SpritePath + "'.");
            }

            return sprite;
        }

        internal static void ValidateOrThrow()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(SpritePath);
            if (importer == null || sprite == null)
            {
                throw new BuildFailedException(
                    "Missing authored HUD medallion frame. Run " +
                    "Tools/Art/extract_hud_medallion_alpha.py and rebuild the foundation.");
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (!importer.DoesSourceTextureHaveAlpha() ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                settings.spriteMeshType != SpriteMeshType.FullRect ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                !importer.alphaIsTransparency ||
                !importer.mipmapEnabled ||
                importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter ||
                !importer.borderMipmap ||
                importer.mipMapsPreserveCoverage ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.npotScale != TextureImporterNPOTScale.None ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 100f) ||
                importer.maxTextureSize != 512 ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.isReadable)
            {
                throw new BuildFailedException(
                    "HUD medallion frame import settings are stale. Rebuild the foundation.");
            }
        }

        private static void ConfigureImporter()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SpritePath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "HUD medallion source was not imported at '" + SpritePath + "'.");
            }

            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                settings.spriteMeshType != SpriteMeshType.FullRect ||
                importer.alphaSource != TextureImporterAlphaSource.FromInput ||
                !importer.alphaIsTransparency ||
                !importer.mipmapEnabled ||
                importer.mipmapFilter != TextureImporterMipFilter.KaiserFilter ||
                !importer.borderMipmap ||
                importer.mipMapsPreserveCoverage ||
                importer.wrapMode != TextureWrapMode.Clamp ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.npotScale != TextureImporterNPOTScale.None ||
                importer.maxTextureSize != 512 ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                importer.isReadable;
            if (!changed)
            {
                return;
            }

            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.mipmapFilter = TextureImporterMipFilter.KaiserFilter;
            importer.borderMipmap = true;
            importer.mipMapsPreserveCoverage = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.isReadable = false;
            importer.SaveAndReimport();
        }
    }
}
