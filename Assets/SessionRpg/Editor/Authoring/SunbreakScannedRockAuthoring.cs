using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>One hash-locked CC0 rock benchmark; no gameplay or runtime updates.</summary>
    public static class SunbreakScannedRockAuthoring
    {
        public const string SourceFolder = "Assets/ThirdParty/PolyHaven/RockMossSet01";
        public const string ModelPath = SourceFolder + "/rock_moss_set_01_1k.fbx";
        public const string DiffusePath = SourceFolder + "/rock_moss_set_01_diff_1k.jpg";
        public const string NormalPath = SourceFolder + "/rock_moss_set_01_nor_gl_1k.exr";
        public const string RoughnessPath = SourceFolder + "/rock_moss_set_01_rough_1k.jpg";
        public const string MaterialPath = "Assets/SessionRpg/Art/Materials/Sunbreak_ScannedMossRock.mat";
        public const string SourceNodeName = "rock_moss_set_01_rock03";
        public const string InstanceName = "ScannedMossRockBenchmark";
        public const string ShaderName = "SessionRpg/Sunbreak Terrain";
        public const int TriangleCount = 5000;
        public const float MaximumHorizontalRadius = 1.5f;

        private static readonly string[] SourcePaths = { ModelPath, DiffusePath, NormalPath, RoughnessPath };
        private static readonly long[] SourceSizes = { 2652988L, 168099L, 1571891L, 82624L };
        private static readonly string[] SourceHashes =
        {
            "8fa2a2666ecc4591f59e1d45db05d86857115b55492c8522917f3de5e650e6f9",
            "40cea65d8aa4ee73a93b04af19963834d061eee9779c3fc2c1cba76eef812ccc",
            "d86555deabb910ed82b2b770d852ba5aa931d373689ff7ae028574c7b310be99",
            "4d6ec46623abd8e2cdb855f59c7ce31873ad61b36b60e4de579e9e776a497c6b"
        };

        public static Transform CreateBenchmarkRock(Transform parent, Vector3 worldCenter, float groundY)
        {
            if (parent == null) { throw new ArgumentNullException(nameof(parent)); }
            if (!Finite(worldCenter.x) || !Finite(worldCenter.y) || !Finite(worldCenter.z) || !Finite(groundY))
            {
                throw new ArgumentOutOfRangeException(nameof(worldCenter));
            }
            if (parent.Find(InstanceName) != null)
            {
                throw new InvalidOperationException("A Sunbreak landmark may contain only one scanned-rock benchmark.");
            }
            ConfigureSources();
            Material material = ConfigureMaterial();
            MeshFilter source = RequireSourceRock();
            Vector3 sourceScale = source.transform.lossyScale;
            if (sourceScale.x <= 0f || Mathf.Abs(sourceScale.x - sourceScale.y) > 0.0001f ||
                Mathf.Abs(sourceScale.x - sourceScale.z) > 0.0001f)
            {
                throw new InvalidDataException("Rock03 source transform must retain positive uniform scale.");
            }
            Quaternion rotation = source.transform.rotation;
            Vector3[] points = source.sharedMesh.vertices;
            var bounds = new Bounds(rotation * points[0], Vector3.zero);
            foreach (Vector3 point in points) { bounds.Encapsulate(rotation * point); }
            Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            float radius = 0f;
            foreach (Vector3 point in points)
            {
                Vector3 offset = rotation * point - pivot;
                radius = Mathf.Max(radius, new Vector2(offset.x, offset.z).magnitude);
            }
            if (!Finite(radius) || radius <= 0f) { throw new InvalidDataException("Rock03 has invalid bounds."); }
            float scale = Mathf.Min(sourceScale.x, MaximumHorizontalRadius / radius);
            var root = new GameObject(InstanceName) { layer = 2 };
            root.transform.SetParent(parent, false);
            root.transform.rotation = rotation;
            root.transform.localScale = Vector3.one * scale;
            // The owning chunk is already translated in world space. Recenter
            // the selected mesh, not the source set's six-object staging layout.
            root.transform.position = new Vector3(worldCenter.x, groundY, worldCenter.z) - pivot * scale;
            root.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            return root.transform;
        }

        public static void ConfigureSources()
        {
            ValidateSourceIntegrity();
            // Correct texture dimensionality before any explicit FBX reimport
            // can inspect its original referenced texture paths.
            ConfigureTexture(DiffusePath, true, false);
            ConfigureTexture(NormalPath, false, true);
            ConfigureTexture(RoughnessPath, false, false);
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            }
            if (importer == null) { throw new InvalidDataException("Rock model importer missing: " + ModelPath); }
            if (!ModelSettingsMatch(importer))
            {
                importer.materialImportMode = ModelImporterMaterialImportMode.None;
                importer.importAnimation = false;
                importer.animationType = ModelImporterAnimationType.None;
                importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
                importer.addCollider = false;
                importer.importCameras = false;
                importer.importLights = false;
                importer.importBlendShapes = false;
                importer.globalScale = 1f;
                importer.useFileScale = true;
                importer.isReadable = true;
                importer.meshCompression = ModelImporterMeshCompression.Off;
                importer.importNormals = ModelImporterNormals.Import;
                importer.importTangents = ModelImporterTangents.CalculateMikk;
                importer.keepQuads = false;
                importer.SaveAndReimport();
            }
            RequireSourceRock();
        }

        /// <summary>Read-only integrity/import/material gate for standalone validate and builds.</summary>
        public static void Validate()
        {
            ValidateSourceIntegrity();
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null || !ModelSettingsMatch(importer))
            {
                throw new InvalidDataException("Scanned rock model-only import contract drifted: " + ModelPath);
            }
            ValidateTexture(DiffusePath, true, false);
            ValidateTexture(NormalPath, false, true);
            ValidateTexture(RoughnessPath, false, false);
            RequireSourceRock();
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null || material.shader == null || material.shader.name != ShaderName ||
                ShaderUtil.ShaderHasError(material.shader) ||
                AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) != DiffusePath ||
                AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")) != NormalPath ||
                AssetDatabase.GetAssetPath(material.GetTexture("_RoughnessMap")) != RoughnessPath ||
                material.GetTextureScale("_BaseMap") != Vector2.one ||
                material.GetTextureOffset("_BaseMap") != Vector2.zero ||
                material.GetColor("_BaseColor") != Color.white ||
                material.GetFloat("_BumpScale") != 1f || material.GetFloat("_Smoothness") != 1f ||
                material.GetFloat("_OcclusionStrength") != 0f || material.GetFloat("_Cull") != 2f ||
                material.GetFloat("_Surface") != 0f || material.GetFloat("_AlphaClip") != 0f ||
                material.GetFloat("_SecondLayer") != 0f || material.GetFloat("_PackedMasonry") != 0f ||
                material.shaderKeywords.Length != 0 || material.renderQueue != (int)RenderQueue.Geometry ||
                !material.enableInstancing)
            {
                throw new InvalidDataException("Scanned rock measured material contract drifted: " + MaterialPath);
            }
        }

        public static void ValidateSourceIntegrity()
        {
            for (int index = 0; index < SourcePaths.Length; index++)
            {
                RequireHash(SourcePaths[index], SourceSizes[index], SourceHashes[index]);
            }
            RequireHash(SourceFolder + "/CC0-1.0.txt", -1L,
                "a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499");
        }

        public static MeshFilter RequireSourceRock()
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) { throw new InvalidDataException("Missing imported rock source: " + ModelPath); }
            MeshFilter chosen = null;
            MeshFilter fallback = null;
            int fallbackCount = 0;
            foreach (MeshFilter candidate in model.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = candidate.sharedMesh;
                if (mesh == null) { continue; }
                if (candidate.name == SourceNodeName || candidate.name.EndsWith(":" + SourceNodeName, StringComparison.Ordinal))
                {
                    if (chosen != null) { throw new InvalidDataException("Duplicate stable rock03 source node."); }
                    chosen = candidate;
                }
                if (mesh.subMeshCount == 1 && mesh.GetIndexCount(0) == TriangleCount * 3u)
                {
                    fallback = candidate;
                    fallbackCount++;
                }
            }
            // Unity may normalize FBX object names; this hash-locked source has
            // exactly one 5,000-triangle mesh. Never select by array/UI index.
            if (chosen == null && fallbackCount == 1) { chosen = fallback; }
            if (chosen == null || chosen.sharedMesh.subMeshCount != 1 ||
                chosen.sharedMesh.GetIndexCount(0) != TriangleCount * 3u)
            {
                throw new InvalidDataException("Rock03 must resolve uniquely to the verified 5,000-triangle source mesh.");
            }
            Mesh selected = chosen.sharedMesh;
            if (selected.normals.Length != selected.vertexCount || selected.uv.Length != selected.vertexCount ||
                selected.tangents.Length != selected.vertexCount)
            {
                throw new InvalidDataException("Rock03 must preserve normals/UVs and have generated MikkTSpace tangents.");
            }
            return chosen;
        }

        private static Material ConfigureMaterial()
        {
            Shader shader = Shader.Find(ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader))
            {
                throw new InvalidDataException("Measured Sunbreak terrain shader is missing or invalid.");
            }
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool isNew = material == null;
            // Resolve these before creating an asset. A cubemap or incomplete
            // import must fail here, never become a silent null 2D binding.
            Texture2D diffuse = RequireTexture(DiffusePath);
            Texture2D normal = RequireTexture(NormalPath);
            Texture2D roughness = RequireTexture(RoughnessPath);
            if (isNew)
            {
                material = new Material(shader) { name = "Sunbreak_ScannedMossRock" };
            }
            foreach (string property in material.GetTexturePropertyNames()) { material.SetTexture(property, null); }
            material.shader = shader;
            material.shaderKeywords = Array.Empty<string>();
            material.SetTexture("_BaseMap", diffuse);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_RoughnessMap", roughness);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SecondLayer", 0f);
            material.SetFloat("_PackedMasonry", 0f);
            material.SetFloat("_OcclusionStrength", 0f);
            material.SetFloat("_Cull", 2f);
            material.renderQueue = (int)RenderQueue.Geometry;
            material.enableInstancing = true;
            // Create the initial asset only after all bindings are present.
            // Later chunk import refreshes must not reload an unconfigured
            // on-disk material over the in-memory generated instance.
            if (isNew) { AssetDatabase.CreateAsset(material, MaterialPath); }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static Texture2D RequireTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.dimension != TextureDimension.Tex2D)
            {
                throw new InvalidDataException("Scanned rock source must import as Texture2D: " + path);
            }
            return texture;
        }

        private static bool ModelSettingsMatch(ModelImporter importer) =>
            importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation &&
            importer.animationType == ModelImporterAnimationType.None && importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar &&
            !importer.addCollider && !importer.importCameras && !importer.importLights && !importer.importBlendShapes &&
            importer.globalScale == 1f && importer.useFileScale && importer.isReadable &&
            importer.meshCompression == ModelImporterMeshCompression.Off && importer.importNormals == ModelImporterNormals.Import &&
            importer.importTangents == ModelImporterTangents.CalculateMikk && !importer.keepQuads;

        private static void ConfigureTexture(string path, bool color, bool normal)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { throw new InvalidDataException("Scanned rock texture importer missing: " + path); }
            if (TextureSettingsMatch(importer, color, normal)) { return; }
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.convertToNormalmap = false;
            importer.flipGreenChannel = false;
            importer.sRGBTexture = color;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 1024;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 8;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 100;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        private static bool TextureSettingsMatch(TextureImporter importer, bool color, bool normal)
        {
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            return importer.textureType == (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) &&
                importer.textureShape == TextureImporterShape.Texture2D &&
                !importer.convertToNormalmap && !importer.flipGreenChannel && importer.sRGBTexture == color &&
                importer.alphaSource == TextureImporterAlphaSource.None && !importer.alphaIsTransparency &&
                importer.mipmapEnabled && importer.streamingMipmaps && !importer.isReadable &&
                importer.npotScale == TextureImporterNPOTScale.None && importer.maxTextureSize == 1024 &&
                importer.wrapMode == TextureWrapMode.Repeat && importer.filterMode == FilterMode.Trilinear &&
                importer.anisoLevel == 8 && importer.textureCompression == TextureImporterCompression.CompressedHQ &&
                android.overridden && android.maxTextureSize == 1024 && android.compressionQuality == 100 &&
                android.format == (normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6);
        }

        private static void ValidateTexture(string path, bool color, bool normal)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || !TextureSettingsMatch(importer, color, normal))
            {
                throw new InvalidDataException("Scanned rock texture import contract drifted: " + path);
            }
            RequireTexture(path);
        }

        private static void RequireHash(string path, long size, string expected)
        {
            if (!File.Exists(path) || (size >= 0 && new FileInfo(path).Length != size))
            {
                throw new InvalidDataException("Missing or changed scanned rock source: " + path);
            }
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                if (hash != expected) { throw new InvalidDataException("Scanned rock source hash mismatch: " + path); }
            }
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
