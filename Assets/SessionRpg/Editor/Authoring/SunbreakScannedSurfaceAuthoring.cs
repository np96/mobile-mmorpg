using System.IO;
using SessionRpg.Core;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>CC0 measured landscape surfaces; no gameplay or per-frame work.</summary>
    internal static class SunbreakScannedSurfaceAuthoring
    {
        internal const string ScanFolder = "Assets/ThirdParty/PolyHaven/SunbreakSurfaces";
        internal const string BlendMaskPath = SunbreakBasinAuthoring.MeshFolder + "/SurfaceBlend.asset";
        internal const float GroundRepeats = 96f; // 192 m / 2 m per measured scan tile.
        internal const string ShaderName = "SessionRpg/Sunbreak Terrain";
        internal const string MasonrySourceFolder = "Assets/ThirdParty/PolyHaven/StoneBrickWall001";
        internal const string MasonryMaskPath =
            "Assets/SessionRpg/Art/Environment/Textures/FortressMasonryMask.png";

        public static void Configure(AuthoringAssets assets)
        {
            var grass = new Scan("leafy_grass", "diff", "1k");
            var soil = new Scan("dirt_floor", "diff", "1k");
            var rock = new Scan("rock_08", "diff");
            var bark = new Scan("bark_brown_02", "diff");
            BindScan(assets.SunbreakMeadow, grass, GroundRepeats, new Color(0.62f, 0.70f, 0.59f), 1f);
            // Rock mesh UVs are projected at 0.4 repeats/m; retain the scan's
            // measured 1.5 m footprint. Bark V was authored at 0.8 repeats/m.
            BindScan(assets.SunbreakRock, rock, 1f / (1.5f * 0.4f), new Color(0.69f, 0.72f, 0.66f), 0.9f);
            BindMasonry(assets.SunbreakLimestone, new Color(0.86f, 0.85f, 0.78f));
            BindMasonry(assets.SunbreakPaving, new Color(0.74f, 0.74f, 0.67f));
            BindScan(assets.SunbreakBark, bark, 1.25f, Color.white, 0.8f);
            Material ground = assets.SunbreakMeadow;
            ground.SetTexture("_LayerMap", soil.Color);
            ground.SetTexture("_LayerNormal", soil.Normal);
            ground.SetTexture("_LayerRoughness", soil.Roughness);
            // Earth has its own neutral mineral response. Multiplying
            // both scans by the meadow's green tint made the entire basin read
            // as one yellow lawn. The retained soil is naturally orange; reduce
            // its chroma, not its luminance, to sit beside weathered limestone.
            ground.SetColor("_LayerColor", new Color(0.68f, 0.68f, 0.66f));
            ground.SetFloat("_LayerSaturation", 0.28f);
            ground.SetTextureScale("_LayerMap", Vector2.one * (192f / 2.07f));
            ground.SetTextureOffset("_LayerMap", Vector2.zero);
            ground.SetTexture("_BlendMap", BuildBlendMask());
            ground.SetFloat("_SecondLayer", 1f);
            ground.EnableKeyword("_SECOND_LAYER");
            EditorUtility.SetDirty(ground);
        }

        private static void BindScan(Material material, Scan scan, float tiling, Color tint, float normalStrength)
        {
            BindMaps(material, scan.Color, scan.Normal, scan.Roughness, tiling, tint, normalStrength);
        }

        private static void BindMasonry(Material material, Color tint)
        {
            // Reuse the already hash-verified fortress intake and its linear
            // derivative; do not reinterpret the source-only roughness/AO PNG
            // importers or add another generated mask. At 0.4 mesh UV repeats/m,
            // scale one preserves this scan's documented 2.5 m physical tile.
            BindMaps(material,
                RequireTexture(MasonrySourceFolder + "/stone_brick_wall_001_diff_2k.png"),
                RequireTexture(MasonrySourceFolder + "/stone_brick_wall_001_nor_gl_2k.png"),
                null, 1f, tint, 0.92f);
            material.SetTexture("_PackedMasonryMap", RequireTexture(MasonryMaskPath));
            material.SetFloat("_PackedMasonry", 1f);
            material.SetFloat("_OcclusionStrength", 0.85f);
            material.EnableKeyword("_PACKED_MASONRY");
            EditorUtility.SetDirty(material);
        }

        private static Texture2D RequireTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null) { throw new InvalidDataException("Missing verified masonry texture: " + path); }
            return texture;
        }

        private static void BindMaps(Material material, Texture2D color, Texture2D normal,
            Texture2D roughness, float tiling, Color tint, float normalStrength)
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                throw new InvalidDataException("Sunbreak scanned-surface shader is missing, unsupported, or has compiler errors.");
            }
            // Switching shader must not keep dormant Lit detail/legacy maps
            // as serialized dependencies in the player build.
            foreach (string property in material.GetTexturePropertyNames())
            {
                material.SetTexture(property, null);
            }
            material.shader = shader;
            material.shaderKeywords = System.Array.Empty<string>();
            material.SetTexture("_BaseMap", color);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_RoughnessMap", roughness);
            material.SetTexture("_PackedMasonryMap", null);
            material.SetTextureScale("_BaseMap", Vector2.one * tiling);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", tint);
            material.SetColor("_LayerColor", Color.white);
            material.SetFloat("_LayerSaturation", 1f);
            material.SetFloat("_BumpScale", normalStrength);
            material.SetFloat("_Smoothness", 0.8f);
            material.SetFloat("_SecondLayer", 0f);
            material.SetFloat("_VertexLayer", 0f);
            material.SetFloat("_PackedMasonry", 0f);
            material.SetFloat("_OcclusionStrength", 0f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_Cull", 2f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
        }

        private readonly struct Scan
        {
            public Scan(string name, string colorChannel, string resolution = "2k")
            {
                Color = ImportScan(name, colorChannel, true, false, resolution);
                Normal = ImportScan(name, "nor_gl", false, true, resolution);
                Roughness = ImportScan(name, "rough", false, false, resolution);
            }
            public Texture2D Color { get; }
            public Texture2D Normal { get; }
            public Texture2D Roughness { get; }
        }

        private static Texture2D ImportScan(string name, string channel, bool color, bool normal, string resolution)
        {
            string path = ScanFolder + "/" + name + "/" + name + "_" + channel + "_" + resolution + ".jpg";
            if (!File.Exists(path)) { throw new InvalidDataException("Missing retained Sunbreak scan: " + path); }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { throw new InvalidDataException("Scan texture importer missing: " + path); }
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.convertToNormalmap = false;
            importer.flipGreenChannel = false;
            importer.sRGBTexture = color;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = resolution == "1k" ? 1024 : 2048;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            var android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        internal static Vector2 SampleGroundBlend(float x, float z)
        {
            // A continuous world-space field survives both-axis chunk seams.
            // Low-frequency domain distortion and a separate middle scale form
            // irregular patches, rather than evenly blurred clearing circles.
            float warpX = (Mathf.PerlinNoise(x * 0.031f + 7f, z * 0.031f + 61f) - 0.5f) * 6f;
            float warpZ = (Mathf.PerlinNoise(x * 0.031f + 73f, z * 0.031f + 11f) - 0.5f) * 6f;
            float large = Mathf.PerlinNoise((x + warpX) * 0.046f + 27f, (z + warpZ) * 0.046f + 19f);
            float middle = Mathf.PerlinNoise(x * 0.115f + 43f, z * 0.115f + 97f);
            float small = Mathf.PerlinNoise(x * 0.29f + 83f, z * 0.29f + 31f);
            float soil = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((0.56f - large + (middle - 0.5f) * 0.22f) * 3.4f));
            for (int camp = -1; camp < PveRegionLayout.CampCount; camp++)
            {
                Float3 center = camp < 0 ? PveRegionLayout.Refuge : PveRegionLayout.GetCampCenter(camp);
                float dx = x - center.X;
                float dz = z - center.Z;
                float distance = Mathf.Sqrt(dx * dx + dz * dz) +
                    (middle - 0.5f) * 4f + (small - 0.5f) * 3f;
                float wear = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(3f, 11f, distance));
                soil = Mathf.Max(soil, wear * 0.94f);
            }
            float edge = float.MaxValue;
            for (int index = 0; index < PveRegionLayout.ObstacleCount; index++)
            {
                ArenaCircleObstacle obstacle = PveRegionLayout.GetObstacle(index);
                float dx = x - obstacle.Center.X;
                float dz = z - obstacle.Center.Z;
                edge = Mathf.Min(edge, Mathf.Sqrt(dx * dx + dz * dz) - obstacle.Radius);
            }
            float scree = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0f, 5.5f, edge + small * 1.5f));
            soil = Mathf.Max(soil, scree * 0.9f);
            float vegetation = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.2f, 0.8f, large * 0.7f + middle * 0.3f));
            return new Vector2(Mathf.Clamp01(soil + (small - 0.5f) * 0.14f), vegetation);
        }

        private static Texture2D BuildBlendMask()
        {
            const int size = 512;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true, true)
            {
                name = "Sunbreak_SurfaceBlend", wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear, anisoLevel = 4
            };
            var pixels = new Color32[size * size];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 sample = SampleGroundBlend(-64f + 192f * x / (size - 1f), -64f + 192f * z / (size - 1f));
                    pixels[z * size + x] = new Color(sample.x, sample.y, 0f, 1f);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(BlendMaskPath);
            if (existing == null) { AssetDatabase.CreateAsset(texture, BlendMaskPath); return texture; }
            EditorUtility.CopySerialized(texture, existing);
            Object.DestroyImmediate(texture);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
