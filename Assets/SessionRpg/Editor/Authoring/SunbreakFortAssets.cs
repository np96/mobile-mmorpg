using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>Hash-locked CC0 modular architecture. Authoring only; no collision or simulation authority.</summary>
    public static class SunbreakFortAssets
    {
        public enum Module { RoundTower, ThinGate, LowWall, BattlementWall, ThickEnd, ThinCorner }

        public const string SourceFolder = "Assets/ThirdParty/PolyHaven/ModularFort01";
        public const string ModelPath = SourceFolder + "/modular_fort_01_1k.fbx";
        public const string WallMaterialPath = "Assets/SessionRpg/Art/Materials/Sunbreak_FortWall.mat";
        public const string TrimMaterialPath = "Assets/SessionRpg/Art/Materials/Sunbreak_FortTrim.mat";
        public const string ShaderName = "SessionRpg/Sunbreak Terrain";
        public const int MaximumCompositionTriangles = 10000;
        public const string MeshFolder = "Assets/SessionRpg/Art/Environment/SunbreakFort";

        private static readonly string[] NodeNames =
        {
            "modular_fort_01_tower_round", "modular_fort_01_wall_thin_gate_01",
            "modular_fort_01_wall_thin_straight_03", "modular_fort_01_wall_thin_straight_04",
            "modular_fort_01_wall_thick_end_01", "modular_fort_01_wall_thin_corner_01"
        };
        private static readonly int[] WallTriangles = { 792, 384, 48, 48, 128, 64 };
        private static readonly int[] TrimTriangles = { 3980, 984, 358, 574, 314, 590 };
        // Unity orders imported FBX material slots by first use, not Blender's slot array.
        private static readonly int[] WallSubmeshes = { 1, 0, 1, 1, 1, 1 };
        // Canonical short-X/long-Z dimensions verified from original FBX/glTF.
        // Unity's raw import exchanges X/Z; an explicit presentation yaw restores
        // this intended wall layout without changing mesh positions, indices or UVs.
        private static readonly Vector3[] SourceDimensions =
        {
            new Vector3(15.838445f, 13.498152f, 15.838445f),
            new Vector3(2.671970f, 8.616226f, 7.410237f),
            new Vector3(2.549778f, 7.793655f, 7.410239f),
            new Vector3(2.549778f, 8.605536f, 7.410239f),
            new Vector3(4.172073f, 7.793655f, 4.849300f),
            new Vector3(4.563530f, 8.605457f, 4.557765f)
        };
        private static readonly string[] SourceFiles =
        {
            "modular_fort_01_1k.fbx", "modular_fort_01_wall_diff_2k.jpg",
            "modular_fort_01_wall_nor_gl_2k.exr", "modular_fort_01_wall_rough_2k.jpg",
            "modular_fort_01_trim_diff_2k.jpg", "modular_fort_01_trim_nor_gl_2k.exr",
            "modular_fort_01_trim_rough_2k.jpg"
        };
        private static readonly long[] SourceSizes =
            { 1383932L, 3768092L, 9386120L, 2659257L, 3625475L, 9052659L, 2648068L };
        private static readonly string[] SourceHashes =
        {
            "267369dc754afd545a91f302ef1819893bf8a907100010c097b58a2c576eb281",
            "de442faf434b186b2008d55dbeaee9c5cb2c46c472e40e58c5e4907854462041",
            "52cb200a1b07daecd54cd62f5cbffde94343b7f0e3e6ff9d2648076823774f54",
            "2d4839b695d93cf2d832ae8112831a0774a855d5d800f1487e11c72185c571f2",
            "e582ffdad510dd23d5b8b7a5639286b81e149ad7f10502224eba0cf1391ca8ec",
            "6d8264568c489ab21b61233dfeac4f21192cba9c052720cb2031184afd296681",
            "5ab14081fab6c93c8d75c31cda6ea7ade2cfc2a56e422269c3eeb920167518e2"
        };

        public static string SourceNodeName(Module module) => NodeNames[CheckedIndex(module)];
        public static int WallSubmesh(Module module) => WallSubmeshes[CheckedIndex(module)];
        public static int TrimSubmesh(Module module) => 1 - WallSubmesh(module);
        public static int TriangleCount(Module module)
        {
            int index = CheckedIndex(module);
            return WallTriangles[index] + TrimTriangles[index];
        }

        public static void Configure() => ConfigureSources();

        public static Transform Create(Transform parent, string instanceName, Module module,
            Vector3 worldGroundPosition, float yawDegrees, float uniformScale) =>
            CreateModule(parent, module, instanceName, worldGroundPosition, worldGroundPosition.y, uniformScale, yawDegrees);

        /// <summary>Source meters, canonical Y up, centered in XZ with base at zero. No showcase translation.</summary>
        public static Bounds GroundedSourceBounds(Module module)
        {
            Bounds bounds = CanonicalBounds(RequireSource(module));
            return new Bounds(new Vector3(0f, bounds.extents.y, 0f), bounds.size);
        }

        /// <summary>
        /// Creates one selected source mesh, never the whole showcase prefab. Call ConfigureSources once first.
        /// worldCenter supplies XZ; groundY is the mesh's lowest point. Scale is relative to source meters.
        /// The owning authoring composition must validate every vertex against its Core obstacle union.
        /// </summary>
        public static Transform CreateModule(Transform parent, Module module, string instanceName,
            Vector3 worldCenter, float groundY, float uniformScale, float yawDegrees)
        {
            if (parent == null) { throw new ArgumentNullException(nameof(parent)); }
            CheckedIndex(module);
            if (string.IsNullOrWhiteSpace(instanceName) || parent.Find(instanceName) != null)
            {
                throw new ArgumentException("Fort module instance name must be nonempty and unique within its parent.", nameof(instanceName));
            }
            if (!Finite(worldCenter) || !Finite(groundY) || !Finite(yawDegrees) || !Finite(uniformScale) || uniformScale <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(uniformScale), "Fort placement must be finite with positive uniform scale.");
            }
            if ((parent.lossyScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                throw new ArgumentException("Fort owner must have unit world scale.", nameof(parent));
            }
            MeshFilter source = RequireSource(module);
            Material wall = RequireMaterial(WallMaterialPath, 1);
            Material trim = RequireMaterial(TrimMaterialPath, 4);
            Bounds bounds = CanonicalBounds(source);
            Vector3 pivot = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            Quaternion yaw = Quaternion.Euler(0f, yawDegrees, 0f);
            var instance = new GameObject(instanceName) { layer = 2 };
            instance.transform.SetParent(parent, false);
            instance.transform.rotation = yaw * PresentationRotation(source);
            instance.transform.localScale = source.transform.lossyScale * uniformScale;
            instance.transform.position = new Vector3(worldCenter.x, groundY, worldCenter.z) - yaw * pivot * uniformScale;
            instance.AddComponent<MeshFilter>().sharedMesh = RequireRenderMesh(module);
            MeshRenderer renderer = instance.AddComponent<MeshRenderer>();
            var materials = new Material[2];
            materials[WallSubmesh(module)] = wall;
            materials[TrimSubmesh(module)] = trim;
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic);
            return instance.transform;
        }

        /// <summary>Explicit mutating import/material setup. Reimports only changed or missing inputs.</summary>
        public static void ConfigureSources()
        {
            ValidateSourceIntegrity();
            for (int index = 1; index < SourceFiles.Length; index++) { ConfigureTexture(index); }
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            }
            if (importer == null) { throw new InvalidDataException("Missing fort model importer: " + ModelPath); }
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
                importer.swapUVChannels = false;
                importer.generateSecondaryUV = false;
                importer.SaveAndReimport();
            }
            for (int index = 0; index < NodeNames.Length; index++) { RequireSource((Module)index); }
            ConfigureMaterial(WallMaterialPath, 1);
            ConfigureMaterial(TrimMaterialPath, 4);
            for (int index = 0; index < NodeNames.Length; index++) { ConfigureRenderMesh((Module)index); }
        }

        /// <summary>Read-only source/import/material gate. Does not repair, import, save or manufacture assets.</summary>
        public static void Validate()
        {
            ValidateSourceIntegrity();
            var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
            if (importer == null || !ModelSettingsMatch(importer))
            {
                throw new InvalidDataException("Fort model-only import contract drifted: " + ModelPath);
            }
            for (int index = 1; index < SourceFiles.Length; index++)
            {
                string path = PathAt(index);
                var textureImporter = AssetImporter.GetAtPath(path) as TextureImporter;
                if (textureImporter == null || !TextureSettingsMatch(textureImporter, index))
                {
                    throw new InvalidDataException("Fort texture import contract drifted: " + path);
                }
                textureImporter.GetSourceTextureWidthAndHeight(out int width, out int height);
                if (width != 2048 || height != 2048) { throw new InvalidDataException("Fort texture source must be 2048 square: " + path); }
                RequireTexture(path);
            }
            for (int index = 0; index < NodeNames.Length; index++) { RequireSource((Module)index); }
            RequireMaterial(WallMaterialPath, 1);
            RequireMaterial(TrimMaterialPath, 4);
            for (int index = 0; index < NodeNames.Length; index++) { RequireRenderMesh((Module)index); }
        }

        public static Mesh RequireRenderMesh(Module module)
        {
            Mesh source = RequireSource(module).sharedMesh;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshFolder + "/" + SourceNodeName(module) + ".asset");
            if (mesh == null || mesh.vertexCount != source.vertexCount || mesh.subMeshCount != source.subMeshCount)
            { throw new InvalidDataException("Missing bounded fort render mesh: " + module); }
            Vector3[] vertices = mesh.vertices, originalVertices = source.vertices;
            Vector3[] normals = mesh.normals, originalNormals = source.normals;
            Vector2[] uv = mesh.uv, originalUv = source.uv;
            Vector4[] tangents = mesh.tangents, originalTangents = source.tangents;
            if (normals.Length != vertices.Length || uv.Length != vertices.Length || tangents.Length != vertices.Length)
            { throw new InvalidDataException("Fort render mesh channel count changed: " + module); }
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 tangent = tangents[i];
                if (vertices[i] != originalVertices[i] || normals[i] != originalNormals[i] || uv[i] != originalUv[i] ||
                    !Finite(tangent) || Mathf.Abs(tangent.sqrMagnitude - 1f) > 0.001f ||
                    Mathf.Abs(Vector3.Dot(normals[i], tangent)) > 0.001f ||
                    tangents[i].w != (originalTangents[i].w < 0f ? -1f : 1f))
                { throw new InvalidDataException("Fort render copy must preserve source geometry and its corrected tangent frame: " + module); }
            }
            for (int s = 0; s < mesh.subMeshCount; s++)
            {
                int[] a = mesh.GetIndices(s), b = source.GetIndices(s);
                if (a.Length != b.Length) { throw new InvalidDataException("Fort render index count changed."); }
                for (int i = 0; i < a.Length; i++)
                { if (a[i] != b[i]) { throw new InvalidDataException("Fort render topology changed."); } }
            }
            return mesh;
        }

        private static void ConfigureRenderMesh(Module module)
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
            { AssetDatabase.CreateFolder("Assets/SessionRpg/Art/Environment", "SunbreakFort"); }
            // Unity's Mikk import preserves some non-orthogonal tangent frames.
            // Preserve every source position, normal, UV and material section;
            // orthogonalize only the derived tangent in a generated render copy.
            Mesh source = RequireSource(module).sharedMesh;
            Mesh authored = UnityEngine.Object.Instantiate(source);
            authored.name = SourceNodeName(module);
            Vector3[] normals = source.normals;
            Vector4[] tangents = source.tangents;
            for (int i = 0; i < tangents.Length; i++)
            {
                Vector3 normal = normals[i].normalized;
                Vector3 tangent = tangents[i];
                tangent -= normal * Vector3.Dot(normal, tangent);
                if (tangent.sqrMagnitude < 0.000001f)
                { tangent = Vector3.Cross(normal, Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.right); }
                tangent.Normalize();
                tangents[i] = new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w < 0f ? -1f : 1f);
            }
            authored.tangents = tangents;
            string path = MeshFolder + "/" + SourceNodeName(module) + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(authored, path); }
            else
            {
                EditorUtility.CopySerialized(authored, existing);
                UnityEngine.Object.DestroyImmediate(authored);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
            }
        }

        public static void ValidateSourceIntegrity()
        {
            for (int index = 0; index < SourceFiles.Length; index++) { RequireHash(PathAt(index), SourceSizes[index], SourceHashes[index]); }
            RequireHash(SourceFolder + "/CC0-1.0.txt", 7048L,
                "a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499");
        }

        public static MeshFilter RequireSource(Module module)
        {
            int index = CheckedIndex(module);
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            if (model == null) { throw new InvalidDataException("Missing imported fort source: " + ModelPath); }
            MeshFilter selected = null;
            foreach (MeshFilter candidate in model.GetComponentsInChildren<MeshFilter>(true))
            {
                if (candidate.name != NodeNames[index] && !candidate.name.EndsWith(":" + NodeNames[index], StringComparison.Ordinal)) { continue; }
                if (selected != null) { throw new InvalidDataException("Duplicate stable fort source node: " + NodeNames[index]); }
                selected = candidate;
            }
            Mesh mesh = selected == null ? null : selected.sharedMesh;
            if (mesh == null || mesh.subMeshCount != 2 || mesh.GetTopology(0) != MeshTopology.Triangles ||
                mesh.GetTopology(1) != MeshTopology.Triangles || mesh.GetIndexCount(WallSubmeshes[index]) != WallTriangles[index] * 3u ||
                mesh.GetIndexCount(1 - WallSubmeshes[index]) != TrimTriangles[index] * 3u)
            {
                var detail = new System.Text.StringBuilder();
                foreach (MeshFilter candidate in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    detail.Append(candidate.name).Append(" [");
                    Mesh actual = candidate.sharedMesh;
                    if (actual != null)
                    {
                        for (int s = 0; s < actual.subMeshCount; s++)
                        { detail.Append(actual.GetIndexCount(s) / 3).Append(','); }
                    }
                    detail.Append("] ");
                }
                throw new InvalidDataException("Fort node or verified wall/trim submesh ordering changed: " +
                    NodeNames[index] + ". Imported nodes/triangles: " + detail);
            }
            if (mesh.vertexCount == 0 || mesh.normals.Length != mesh.vertexCount ||
                mesh.tangents.Length != mesh.vertexCount || mesh.uv.Length != mesh.vertexCount)
            {
                throw new InvalidDataException("Fort must preserve source UVs/normals and MikkTSpace tangents: " + NodeNames[index]);
            }
            Vector3 scale = selected.transform.lossyScale;
            if (!Finite(scale) || scale.x <= 0f || Mathf.Abs(scale.y / scale.x - 1f) > 0.00001f || Mathf.Abs(scale.z / scale.x - 1f) > 0.00001f)
            {
                throw new InvalidDataException("Fort source must retain positive uniform scale: " + NodeNames[index]);
            }
            Bounds bounds = CanonicalBounds(selected);
            if (!Finite(bounds.center) || !Finite(bounds.size) || (bounds.size - SourceDimensions[index]).sqrMagnitude > 0.0025f)
            {
                throw new InvalidDataException("Fort physical scale/up-axis changed: " + NodeNames[index] + " size " + bounds.size);
            }
            return selected;
        }

        private static Bounds CanonicalBounds(MeshFilter source)
        {
            Quaternion rotation = PresentationRotation(source);
            float scale = source.transform.lossyScale.x;
            Vector3[] vertices = source.sharedMesh.vertices;
            var bounds = new Bounds(rotation * vertices[0] * scale, Vector3.zero);
            foreach (Vector3 point in vertices)
            {
                if (!Finite(point)) { throw new InvalidDataException("Non-finite fort source vertex."); }
                bounds.Encapsulate(rotation * point * scale);
            }
            return bounds;
        }

        private static Quaternion PresentationRotation(MeshFilter source) =>
            Quaternion.Euler(0f, 90f, 0f) * source.transform.rotation;

        private static void ConfigureMaterial(string path, int firstTexture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (MaterialSettingsMatch(material, firstTexture)) { return; }
            Shader shader = Shader.Find(ShaderName);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) { throw new InvalidDataException("Fort surface shader missing or invalid."); }
            Texture2D diffuse = RequireTexture(PathAt(firstTexture));
            Texture2D normal = RequireTexture(PathAt(firstTexture + 1));
            Texture2D roughness = RequireTexture(PathAt(firstTexture + 2));
            bool create = material == null;
            if (create) { material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) }; }
            material.shader = shader;
            foreach (string property in material.GetTexturePropertyNames()) { material.SetTexture(property, null); }
            material.shaderKeywords = Array.Empty<string>();
            material.SetTexture("_BaseMap", diffuse);
            material.SetTexture("_BumpMap", normal);
            material.SetTexture("_RoughnessMap", roughness);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_OcclusionStrength", 0f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetFloat("_SecondLayer", 0f);
            material.SetFloat("_PackedMasonry", 0f);
            material.SetFloat("_Cull", 2f);
            material.renderQueue = (int)RenderQueue.Geometry;
            material.enableInstancing = true;
            if (create) { AssetDatabase.CreateAsset(material, path); }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        private static Material RequireMaterial(string path, int firstTexture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!MaterialSettingsMatch(material, firstTexture)) { throw new InvalidDataException("Fort measured wall/trim material contract drifted: " + path); }
            return material;
        }

        private static bool MaterialSettingsMatch(Material material, int firstTexture) =>
            material != null && material.shader != null && material.shader.name == ShaderName && !ShaderUtil.ShaderHasError(material.shader) &&
            AssetDatabase.GetAssetPath(material.GetTexture("_BaseMap")) == PathAt(firstTexture) &&
            AssetDatabase.GetAssetPath(material.GetTexture("_BumpMap")) == PathAt(firstTexture + 1) &&
            AssetDatabase.GetAssetPath(material.GetTexture("_RoughnessMap")) == PathAt(firstTexture + 2) &&
            material.GetTexture("_BaseMap") is Texture2D && material.GetTexture("_BumpMap") is Texture2D && material.GetTexture("_RoughnessMap") is Texture2D &&
            material.GetTextureScale("_BaseMap") == Vector2.one && material.GetTextureOffset("_BaseMap") == Vector2.zero &&
            material.GetColor("_BaseColor") == Color.white && material.GetFloat("_BumpScale") == 1f && material.GetFloat("_Smoothness") == 1f &&
            material.GetFloat("_OcclusionStrength") == 0f && material.GetFloat("_Surface") == 0f && material.GetFloat("_AlphaClip") == 0f &&
            material.GetFloat("_SecondLayer") == 0f && material.GetFloat("_PackedMasonry") == 0f && material.GetFloat("_Cull") == 2f &&
            material.shaderKeywords.Length == 0 && material.renderQueue == (int)RenderQueue.Geometry && material.enableInstancing;

        private static bool ModelSettingsMatch(ModelImporter importer) =>
            importer.materialImportMode == ModelImporterMaterialImportMode.None && !importer.importAnimation &&
            importer.animationType == ModelImporterAnimationType.None && importer.avatarSetup == ModelImporterAvatarSetup.NoAvatar &&
            !importer.addCollider && !importer.importCameras && !importer.importLights && !importer.importBlendShapes &&
            importer.globalScale == 1f && importer.useFileScale && importer.isReadable &&
            importer.meshCompression == ModelImporterMeshCompression.Off && importer.importNormals == ModelImporterNormals.Import &&
            importer.importTangents == ModelImporterTangents.CalculateMikk && !importer.keepQuads && !importer.swapUVChannels && !importer.generateSecondaryUV;

        private static void ConfigureTexture(int index)
        {
            string path = PathAt(index);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) { throw new InvalidDataException("Missing fort texture importer: " + path); }
            if (TextureSettingsMatch(importer, index)) { return; }
            bool normal = index % 3 == 2;
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.convertToNormalmap = false;
            importer.flipGreenChannel = false;
            importer.sRGBTexture = index % 3 == 1;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = true;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
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

        private static bool TextureSettingsMatch(TextureImporter importer, int index)
        {
            bool normal = index % 3 == 2;
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            return importer.textureType == (normal ? TextureImporterType.NormalMap : TextureImporterType.Default) &&
                importer.textureShape == TextureImporterShape.Texture2D && !importer.convertToNormalmap && !importer.flipGreenChannel &&
                importer.sRGBTexture == (index % 3 == 1) && importer.alphaSource == TextureImporterAlphaSource.None && !importer.alphaIsTransparency &&
                importer.mipmapEnabled && importer.streamingMipmaps && !importer.isReadable && importer.npotScale == TextureImporterNPOTScale.None &&
                importer.maxTextureSize == 2048 && importer.wrapMode == TextureWrapMode.Repeat && importer.filterMode == FilterMode.Trilinear &&
                importer.anisoLevel == 8 && importer.textureCompression == TextureImporterCompression.CompressedHQ &&
                android.overridden && android.maxTextureSize == 1024 && android.compressionQuality == 100 &&
                android.format == (normal ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6);
        }

        private static Texture2D RequireTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.dimension != TextureDimension.Tex2D) { throw new InvalidDataException("Fort measured map must be Texture2D: " + path); }
            return texture;
        }

        private static void RequireHash(string path, long bytes, string expected)
        {
            if (!File.Exists(path) || new FileInfo(path).Length != bytes) { throw new InvalidDataException("Missing or changed fort source: " + path); }
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                string hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                if (hash != expected) { throw new InvalidDataException("Fort source hash mismatch: " + path); }
            }
        }

        private static string PathAt(int index) => SourceFolder + "/" + SourceFiles[index];
        private static int CheckedIndex(Module module)
        {
            int index = (int)module;
            if (index < 0 || index >= NodeNames.Length) { throw new ArgumentOutOfRangeException(nameof(module)); }
            return index;
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
    }
}
