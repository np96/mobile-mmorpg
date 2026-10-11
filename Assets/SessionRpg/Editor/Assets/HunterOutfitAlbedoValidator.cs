using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Assets
{
    public static class HunterOutfitAlbedoValidator
    {
        public const string SourceAssetPath =
            "Assets/SessionRpg/Art/Characters/HunterOutfitAlbedo.png";

        public const string ExpectedSourceSha256 =
            "633e51d4f07f2192be91d9a92ec7d27ac257ef5cd00816728bbb071573a01aca";

        public const int ExpectedSourceWidth = 1254;

        public const int ExpectedSourceHeight = 1254;

        public const int ImportedMaximumSize = 1024;

        public const string ConfigureMenuPath =
            "Tools/Session RPG/Assets/Configure Original Hunter Outfit Albedo";

        public const string ValidateMenuPath =
            "Tools/Session RPG/Assets/Validate Original Hunter Outfit Albedo";

        private const string ConfigureInstruction =
            "Run Tools > Session RPG > Assets > Configure Original Hunter Outfit Albedo once, then rebuild the playable foundation.";

        [MenuItem(ValidateMenuPath, priority = 2084)]
        public static void ValidateFromMenu()
        {
            try
            {
                ValidateOrThrow();
                const string message =
                    "Original Hunter outfit albedo and importer settings are valid.";
                Debug.Log(message);
                ShowDialog("Hunter outfit albedo validation", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Hunter outfit albedo validation failed", exception.Message);
            }
        }

        [MenuItem(ConfigureMenuPath, priority = 2085)]
        public static void ConfigureFromMenu()
        {
            try
            {
                bool changed = ConfigureImport();
                ValidateOrThrow();
                string message = changed
                    ? "Configured and reimported the original Hunter outfit albedo."
                    : "Original Hunter outfit albedo import settings were already correct.";
                Debug.Log(message);
                ShowDialog("Hunter outfit albedo import", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Hunter outfit albedo import failed", exception.Message);
            }
        }

        public static void ValidateForCommandLine()
        {
            ValidateOrThrow();
            Debug.Log("Original Hunter outfit albedo validation passed.");
        }

        public static void ConfigureForCommandLine()
        {
            bool changed = ConfigureImport();
            ValidateOrThrow();
            Debug.Log(
                "Original Hunter outfit albedo import configuration passed; changed " +
                (changed ? "1" : "0") + " asset(s).");
        }

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            ValidateSourceFile(errors);
            ValidateImporter(errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(errors));
            }
        }

        public static bool ConfigureImport()
        {
            TextureImporter importer = AssetImporter.GetAtPath(SourceAssetPath)
                as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Missing or invalid original Hunter outfit albedo at '" +
                    SourceAssetPath + "'. Restore the tracked source PNG before " +
                    "configuring its import.");
            }

            bool changed = false;
            changed |= SetValue(
                importer.textureType,
                TextureImporterType.Default,
                value => importer.textureType = value);
            changed |= SetValue(
                importer.sRGBTexture,
                true,
                value => importer.sRGBTexture = value);
            changed |= SetValue(
                importer.alphaSource,
                TextureImporterAlphaSource.None,
                value => importer.alphaSource = value);
            changed |= SetValue(
                importer.alphaIsTransparency,
                false,
                value => importer.alphaIsTransparency = value);
            changed |= SetValue(
                importer.mipmapEnabled,
                true,
                value => importer.mipmapEnabled = value);
            changed |= SetValue(
                importer.wrapMode,
                TextureWrapMode.Repeat,
                value => importer.wrapMode = value);
            changed |= SetValue(
                importer.filterMode,
                FilterMode.Bilinear,
                value => importer.filterMode = value);
            changed |= SetValue(
                importer.anisoLevel,
                4,
                value => importer.anisoLevel = value);
            changed |= SetValue(
                importer.maxTextureSize,
                ImportedMaximumSize,
                value => importer.maxTextureSize = value);
            changed |= SetValue(
                importer.npotScale,
                TextureImporterNPOTScale.ToNearest,
                value => importer.npotScale = value);
            changed |= SetValue(
                importer.isReadable,
                false,
                value => importer.isReadable = value);
            changed |= SetValue(
                importer.textureCompression,
                TextureImporterCompression.CompressedHQ,
                value => importer.textureCompression = value);

            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            if (!android.overridden ||
                android.maxTextureSize != ImportedMaximumSize ||
                android.format != TextureImporterFormat.Automatic ||
                android.textureCompression != TextureImporterCompression.Compressed ||
                android.compressionQuality != 65 ||
                android.crunchedCompression)
            {
                android.name = "Android";
                android.overridden = true;
                android.maxTextureSize = ImportedMaximumSize;
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
                AssetDatabase.SaveAssets();
            }

            return changed;
        }

        private static void ValidateSourceFile(List<string> errors)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                errors.Add("Unity did not expose a valid project root for Hunter source validation.");
                return;
            }

            string absolutePath = Path.Combine(projectRoot, SourceAssetPath);
            if (!File.Exists(absolutePath))
            {
                errors.Add("Missing tracked Hunter albedo PNG at '" + SourceAssetPath + "'.");
                return;
            }

            byte[] bytes = File.ReadAllBytes(absolutePath);
            if (!TryReadPngDimensions(bytes, out int width, out int height))
            {
                errors.Add("Hunter albedo source is not a valid PNG with an IHDR header.");
                return;
            }

            if (width != ExpectedSourceWidth || height != ExpectedSourceHeight)
            {
                errors.Add(
                    "Hunter albedo source must remain " + ExpectedSourceWidth + "x" +
                    ExpectedSourceHeight + " pixels before Unity import; found " + width + "x" +
                    height + ".");
            }

            string hash = ComputeSha256(bytes);
            if (!string.Equals(hash, ExpectedSourceSha256, StringComparison.Ordinal))
            {
                errors.Add(
                    "Hunter albedo source SHA-256 changed. Review the UV-layout and " +
                    "provenance deliberately before updating the audited hash; found " + hash + ".");
            }
        }

        private static void ValidateImporter(List<string> errors)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SourceAssetPath);
            TextureImporter importer = AssetImporter.GetAtPath(SourceAssetPath)
                as TextureImporter;
            if (texture == null || importer == null)
            {
                errors.Add(
                    "Hunter albedo is not a loadable Texture2D at '" + SourceAssetPath + "'.");
                return;
            }

            TextureImporterPlatformSettings android =
                importer.GetPlatformTextureSettings("Android");
            if (importer.textureType != TextureImporterType.Default ||
                !importer.sRGBTexture ||
                importer.alphaSource != TextureImporterAlphaSource.None ||
                importer.alphaIsTransparency ||
                !importer.mipmapEnabled ||
                importer.wrapMode != TextureWrapMode.Repeat ||
                importer.filterMode != FilterMode.Bilinear ||
                importer.anisoLevel != 4 ||
                importer.maxTextureSize != ImportedMaximumSize ||
                importer.npotScale != TextureImporterNPOTScale.ToNearest ||
                importer.isReadable ||
                importer.textureCompression != TextureImporterCompression.CompressedHQ ||
                !android.overridden ||
                android.maxTextureSize != ImportedMaximumSize ||
                android.format != TextureImporterFormat.Automatic ||
                android.textureCompression != TextureImporterCompression.Compressed ||
                android.compressionQuality != 65 ||
                android.crunchedCompression)
            {
                errors.Add(
                    "Hunter albedo is not using the audited 1K mobile texture policy. " +
                    ConfigureInstruction);
            }

            if (texture.width != ImportedMaximumSize ||
                texture.height != ImportedMaximumSize)
            {
                errors.Add(
                    "Hunter albedo must import to a deterministic " + ImportedMaximumSize +
                    "x" + ImportedMaximumSize + " Texture2D; found " + texture.width + "x" +
                    texture.height + ". " + ConfigureInstruction);
            }
        }

        private static bool TryReadPngDimensions(
            byte[] bytes,
            out int width,
            out int height)
        {
            width = 0;
            height = 0;
            if (bytes == null || bytes.Length < 24 ||
                bytes[0] != 0x89 ||
                bytes[1] != 0x50 ||
                bytes[2] != 0x4E ||
                bytes[3] != 0x47 ||
                bytes[4] != 0x0D ||
                bytes[5] != 0x0A ||
                bytes[6] != 0x1A ||
                bytes[7] != 0x0A)
            {
                return false;
            }

            width = ReadBigEndianInt32(bytes, 16);
            height = ReadBigEndianInt32(bytes, 20);
            return width > 0 && height > 0;
        }

        private static int ReadBigEndianInt32(byte[] bytes, int offset)
        {
            return
                (bytes[offset] << 24) |
                (bytes[offset + 1] << 16) |
                (bytes[offset + 2] << 8) |
                bytes[offset + 3];
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(bytes);
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

        private static string BuildErrorMessage(List<string> errors)
        {
            return "Hunter outfit albedo validation failed:\n- " +
                string.Join("\n- ", errors);
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
