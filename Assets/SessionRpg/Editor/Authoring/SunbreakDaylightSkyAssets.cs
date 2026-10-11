using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>One original CC0 daylight panorama. Authoring only; never changes global lighting or cameras.</summary>
    public static class SunbreakDaylightSkyAssets
    {
        public const string SourceFolder = "Assets/ThirdParty/PolyHaven/SunbreakDaylightSky";
        public const string SourcePath = SourceFolder + "/kloofendal_48d_partly_cloudy_puresky_1k.hdr";
        public const string MaterialPath = "Assets/SessionRpg/Art/Materials/Sunbreak_DaylightSky.mat";
        public const string ShaderName = "Skybox/Panoramic";
        public const string LatitudeLongitudeKeyword = "_MAPPING_LATITUDE_LONGITUDE_LAYOUT";
        public const long SourceBytes = 1435119L;
        public const string SourceSha256 = "fd94c84997b8a3c353b62c2125a9b44e19509956986a126e472684432a02d798";
        public const string SourceMd5 = "c69498687e876bf68d2a8b8a62234eb9";
        public const int DesktopMaximumSize = 1024;
        public const int AndroidMaximumSize = 512;
        public static readonly Color NeutralTint = new Color(0.5f, 0.5f, 0.5f, 1f);

        /// <summary>Creates/repairs only this source import and material, returning a saved skybox material.</summary>
        public static Material ConfigureSky()
        {
            ValidateSourceIntegrity();
            var importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(SourcePath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
            }
            if (importer == null) { throw new InvalidDataException("Missing daylight HDR texture importer: " + SourcePath); }
            if (!ImportSettingsMatch(importer))
            {
                importer.textureType = TextureImporterType.Default;
                importer.textureShape = TextureImporterShape.Texture2D;
                importer.sRGBTexture = false;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.alphaIsTransparency = false;
                importer.isReadable = false;
                importer.mipmapEnabled = true;
                importer.streamingMipmaps = false;
                importer.mipMapsPreserveCoverage = false;
                importer.npotScale = TextureImporterNPOTScale.None;
                importer.maxTextureSize = DesktopMaximumSize;
                importer.wrapModeU = TextureWrapMode.Repeat;
                importer.wrapModeV = TextureWrapMode.Clamp;
                importer.wrapModeW = TextureWrapMode.Clamp;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 1;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                TextureImporterPlatformSettings defaults = importer.GetDefaultPlatformTextureSettings();
                defaults.maxTextureSize = DesktopMaximumSize;
                defaults.format = TextureImporterFormat.RGB9E5;
                defaults.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SetPlatformTextureSettings(defaults);
                ConfigurePlatform(importer, "Standalone", DesktopMaximumSize);
                ConfigurePlatform(importer, "Android", AndroidMaximumSize);
                importer.SaveAndReimport();
            }
            Texture2D texture = RequireTexture();
            Shader shader = Shader.Find(ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) { throw new InvalidDataException("Panoramic daylight shader is missing or invalid."); }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (MaterialSettingsMatch(material)) { return material; }
            bool create = material == null;
            if (create) { material = new Material(shader) { name = "Sunbreak_DaylightSky" }; }
            material.shader = shader;
            // The shader's KeywordEnum drawer restores this on a fresh material
            // load. Author the same canonical keyword instead of requiring an
            // empty list that only survives the current editor session.
            material.shaderKeywords = new[] { LatitudeLongitudeKeyword };
            material.SetTexture("_MainTex", texture);
            material.SetTextureScale("_MainTex", Vector2.one);
            material.SetTextureOffset("_MainTex", Vector2.zero);
            // Panoramic's neutral tint is 0.5 (the shader applies the color-space
            // doubling factor). White would brighten the original by another stop.
            material.SetColor("_Tint", NeutralTint);
            material.SetFloat("_Exposure", 1f);
            material.SetFloat("_Rotation", 0f);
            material.SetFloat("_Mapping", 1f);
            material.SetFloat("_ImageType", 0f);
            material.SetFloat("_MirrorOnBack", 0f);
            material.SetFloat("_Layout", 0f);
            material.renderQueue = (int)RenderQueue.Background;
            if (create) { AssetDatabase.CreateAsset(material, MaterialPath); }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        /// <summary>Read-only gate. Does not import, repair, save or bind RenderSettings.skybox.</summary>
        public static void Validate()
        {
            ValidateSourceIntegrity();
            var importer = AssetImporter.GetAtPath(SourcePath) as TextureImporter;
            if (importer == null || !ImportSettingsMatch(importer)) { throw new InvalidDataException("Daylight HDR import contract drifted: " + SourcePath); }
            importer.GetSourceTextureWidthAndHeight(out int width, out int height);
            if (width != 1024 || height != 512) { throw new InvalidDataException("Daylight HDR must preserve the original 1024x512 panorama."); }
            RequireTexture();
            string materialDrift = MaterialSettingsDrift(AssetDatabase.LoadAssetAtPath<Material>(MaterialPath));
            if (materialDrift != null) { throw new InvalidDataException("Daylight panoramic material contract drifted: " + MaterialPath + ": " + materialDrift); }
        }

        public static void ValidateSourceIntegrity()
        {
            RequireHash(SourcePath, SourceBytes, SourceSha256);
            RequireHash(SourceFolder + "/CC0-1.0.txt", 7048L,
                "a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499");
        }

        private static Texture2D RequireTexture()
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(SourcePath);
            if (texture == null || texture.dimension != TextureDimension.Tex2D || texture.format != TextureFormat.RGB9e5Float)
            {
                throw new InvalidDataException("Daylight HDR must import as RGB9E5 Texture2D, never a cubemap or LDR preview.");
            }
            return texture;
        }

        private static bool MaterialSettingsMatch(Material material) => MaterialSettingsDrift(material) == null;

        private static string MaterialSettingsDrift(Material material)
        {
            if (material == null) { return "material missing"; }
            if (material.shader == null) { return "shader missing"; }
            if (material.shader.name != ShaderName) { return "shader=" + material.shader.name; }
            if (ShaderUtil.ShaderHasError(material.shader))
            {
                var messages = ShaderUtil.GetShaderMessages(material.shader);
                return "ShaderHasError=true" + (messages.Length == 0 ? string.Empty : ": " + messages[0].message);
            }
            Texture texture = material.GetTexture("_MainTex");
            if (!(texture is Texture2D)) { return "_MainTex type=" + (texture == null ? "null" : texture.GetType().Name); }
            if (AssetDatabase.GetAssetPath(texture) != SourcePath) { return "_MainTex path=" + AssetDatabase.GetAssetPath(texture); }
            if (material.GetTextureScale("_MainTex") != Vector2.one) { return "_MainTex scale=" + material.GetTextureScale("_MainTex"); }
            if (material.GetTextureOffset("_MainTex") != Vector2.zero) { return "_MainTex offset=" + material.GetTextureOffset("_MainTex"); }
            if (material.GetColor("_Tint") != NeutralTint) { return "_Tint=" + material.GetColor("_Tint") + ", expected=" + NeutralTint; }
            string[] names = { "_Exposure", "_Rotation", "_Mapping", "_ImageType", "_MirrorOnBack", "_Layout" };
            float[] values = { 1f, 0f, 1f, 0f, 0f, 0f };
            for (int i = 0; i < names.Length; i++)
            {
                float actual = material.GetFloat(names[i]);
                if (actual != values[i]) { return names[i] + "=" + actual + ", expected=" + values[i]; }
            }
            string[] keywords = material.shaderKeywords;
            if (keywords.Length != 1 || keywords[0] != LatitudeLongitudeKeyword)
            {
                return "keywords=" + string.Join(",", keywords) + ", expected=" + LatitudeLongitudeKeyword;
            }
            if (material.renderQueue != (int)RenderQueue.Background) { return "renderQueue=" + material.renderQueue; }
            return null;
        }

        private static bool ImportSettingsMatch(TextureImporter importer)
        {
            TextureImporterPlatformSettings defaults = importer.GetDefaultPlatformTextureSettings();
            return importer.textureType == TextureImporterType.Default && importer.textureShape == TextureImporterShape.Texture2D &&
                !importer.sRGBTexture && importer.alphaSource == TextureImporterAlphaSource.None && !importer.alphaIsTransparency && !importer.isReadable &&
                importer.mipmapEnabled && !importer.streamingMipmaps && !importer.mipMapsPreserveCoverage && importer.npotScale == TextureImporterNPOTScale.None &&
                importer.maxTextureSize == DesktopMaximumSize && importer.wrapModeU == TextureWrapMode.Repeat && importer.wrapModeV == TextureWrapMode.Clamp &&
                importer.wrapModeW == TextureWrapMode.Clamp && importer.filterMode == FilterMode.Trilinear && importer.anisoLevel == 1 &&
                importer.textureCompression == TextureImporterCompression.Uncompressed && defaults.maxTextureSize == DesktopMaximumSize &&
                defaults.format == TextureImporterFormat.RGB9E5 && PlatformMatches(importer, "Standalone", DesktopMaximumSize) && PlatformMatches(importer, "Android", AndroidMaximumSize);
        }

        private static void ConfigurePlatform(TextureImporter importer, string name, int maximumSize)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(name);
            settings.name = name;
            settings.overridden = true;
            settings.maxTextureSize = maximumSize;
            // Shared-exponent native HDR keeps the verified positive range
            // (source maximum 61,184), with a bounded four bytes per texel.
            settings.format = TextureImporterFormat.RGB9E5;
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(settings);
        }

        private static bool PlatformMatches(TextureImporter importer, string name, int maximumSize)
        {
            TextureImporterPlatformSettings settings = importer.GetPlatformTextureSettings(name);
            return settings.overridden && settings.maxTextureSize == maximumSize && settings.format == TextureImporterFormat.RGB9E5 && settings.textureCompression == TextureImporterCompression.Uncompressed;
        }

        private static void RequireHash(string path, long bytes, string expected)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != bytes) { throw new InvalidDataException("Missing or changed daylight source: " + path); }
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                if (hash != expected) { throw new InvalidDataException("Daylight source hash mismatch: " + path); }
            }
        }
    }
}
