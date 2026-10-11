using System.IO;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>
    /// First-party generated albedo sources, with repeatable import and material
    /// bindings. These are color textures, not scanned PBR or invented normals.
    /// No shared Arena/Training material or third-party importer is modified.
    /// </summary>
    internal static class SunbreakSurfaceAuthoring
    {
        internal const string SourceFolder =
            "Assets/SessionRpg/SourceArt/Environment/SunbreakNaturalV1";
        internal const float GroundRepeats = 48f; // 192 m / 4 m per material tile.

        public static void Configure(AuthoringAssets assets)
        {
            Texture2D meadow = ImportAlbedo("MeadowAlbedoV1", 1024);
            Texture2D limestone = ImportAlbedo("LimestoneAlbedoV1", 1024);
            Texture2D bark = ImportAlbedo("BarkAlbedoV1", 1024);
            Texture2D leaf = ImportAlbedo("LeafAlbedoV1", 512);
            BindAlbedo(assets.SunbreakMeadow, meadow, GroundRepeats);
            BindAlbedo(assets.SunbreakRock, limestone, 1.4f);
            BindAlbedo(assets.SunbreakPaving, limestone, 1.4f);
            BindAlbedo(assets.SunbreakBark, bark, 1f);
            BindAlbedo(assets.SunbreakFoliage, leaf, 1f);
            BindAlbedo(assets.SunbreakFoliageLight, leaf, 1f);

            // URP Lit applies BaseMap ST before DetailAlbedoMap ST. Cancel the
            // 48x fine transform here so camp wear retains its single world
            // phase rather than clamping the macro across almost every tile.
            Material ground = assets.SunbreakMeadow;
            ground.SetTexture("_DetailAlbedoMap", SunbreakBasinAuthoring.BuildGroundAlbedo());
            ground.SetTextureScale("_DetailAlbedoMap", Vector2.one / GroundRepeats);
            ground.SetTextureOffset("_DetailAlbedoMap", Vector2.zero);
            ground.SetFloat("_DetailAlbedoMapScale", 1f);
            ground.SetFloat("_DetailNormalMapScale", 0f);
            // LitDetailGUI selects the cheaper exact 2x path at strength one.
            // Author that same keyword so an Editor refresh cannot rewrite it.
            ground.EnableKeyword("_DETAIL_MULX2");
            EditorUtility.SetDirty(ground);
            // Keep V1 source imports reproducible for comparisons; the active
            // terrain now uses independent scanned albedo/normal/roughness.
            SunbreakScannedSurfaceAuthoring.Configure(assets);
        }

        private static Texture2D ImportAlbedo(string name, int maximumSize)
        {
            string path = SourceFolder + "/" + name + ".png";
            if (!File.Exists(path))
            {
                throw new InvalidDataException("Missing Sunbreak source albedo: " + path);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidDataException("Sunbreak albedo has no texture importer: " + path);
            }

            importer.textureType = TextureImporterType.Default;
            importer.convertToNormalmap = false;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.ToNearest;
            importer.maxTextureSize = maximumSize;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = maximumSize;
            android.format = TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void BindAlbedo(Material material, Texture2D texture, float tiling)
        {
            foreach (string property in new[] { "_BaseMap", "_MainTex" })
            {
                if (!material.HasProperty(property)) { continue; }
                material.SetTexture(property, texture);
                material.SetTextureScale(property, Vector2.one * tiling);
                material.SetTextureOffset(property, Vector2.zero);
            }

            foreach (string property in new[] { "_BumpMap", "_DetailNormalMap",
                "_DetailAlbedoMap", "_DetailMask", "_MetallicGlossMap", "_OcclusionMap", "_EmissionMap" })
            {
                material.SetTexture(property, null);
            }
            foreach (string keyword in new[] { "_NORMALMAP", "_DETAIL_MULX2",
                "_DETAIL_SCALED", "_METALLICSPECGLOSSMAP", "_OCCLUSIONMAP",
                "_ALPHATEST_ON", "_SURFACE_TYPE_TRANSPARENT", "_EMISSION" })
            {
                material.DisableKeyword(keyword);
            }
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetColor("_EmissionColor", Color.black);
            EditorUtility.SetDirty(material);
        }
    }
}
