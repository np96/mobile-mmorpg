using System;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>Editor-only immutable-source vegetation intake and deterministic mesh asset generation.</summary>
    public static class SunbreakVegetationAssets
    {
        public const string DataFolder = "SourceArt/Environment/SunbreakVegetationV1";
        public const string MeshFolder = "Assets/SessionRpg/Art/Environment/SunbreakVegetationV1";
        public const string VegetationShader = "SessionRpg/Sunbreak Vegetation";
        public const string OpaqueShader = "SessionRpg/Sunbreak Terrain";
        private const string ProviderFolder = "Assets/ThirdParty/PolyHaven/";
        private const string ManifestHash = "b83c3311475c016259f8ed40cca7bc9a54ac38222741a786037a4cc57582aaff";
        private static readonly string[] IntakeFolders = { "TreeSmall02", "GrassMedium01", "Fern02" };
        private static readonly string[] IntakeHashes =
        {
            "d458b5f7361d45edd85a03c88c7575bde5be650acc08cab5fde330889fa3a859",
            "94a36cfc2f00364626694ffc76bca0ed0bb1c1e6da95d82c1e8eabc3a6be38a2",
            "39c31d7978240a38fb06a94746b30f0d96d271a8dbab57da532f8e0ecd2d0d3d"
        };
        private static readonly string[] Selections = { "TreeSmall02", "GrassTinyA", "GrassTinyB", "GrassTinyC", "GrassTinyD", "GrassTinyE", "GrassTinyF", "Fern02a" };
        private static readonly int[] SelectionTriangles = { 9328, 79, 28, 40, 32, 62, 60, 784 };
        private static readonly string[] Roles = { "TreeTrunk", "TreeLeaf", "Grass", "Fern" };

        public readonly struct Part
        {
            public readonly Mesh Mesh;
            public readonly Material Material;
            public Part(Mesh mesh, Material material) { Mesh = mesh; Material = material; }
        }

        public static string MeshPath(string selection, string role) => MeshFolder + "/" + selection + "_" + role + ".asset";
        public static string MaterialPath(string role) => "Assets/SessionRpg/Art/Materials/Sunbreak_Measured" + role + ".mat";

        /// <summary>Returns prebuilt single-submesh parts in source order; never changes/imports source assets.</summary>
        public static Part[] GetParts(string selection)
        {
            int index = SelectionIndex(selection);
            string[] roles = index == 0 ? new[] { "TreeTrunk", "TreeLeaf" } : new[] { index == 7 ? "Fern" : "Grass" };
            var parts = new Part[roles.Length];
            int triangles = 0;
            for (int part = 0; part < roles.Length; part++)
            {
                Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath(selection, roles[part]));
                Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(roles[part]));
                if (mesh == null || mesh.subMeshCount != 1 || mesh.GetTopology(0) != MeshTopology.Triangles || material == null)
                {
                    throw new InvalidDataException("Missing generated measured vegetation part: " + selection + "/" + roles[part]);
                }
                triangles += (int)mesh.GetIndexCount(0) / 3;
                parts[part] = new Part(mesh, material);
            }
            if (triangles != SelectionTriangles[index]) { throw new InvalidDataException("Measured vegetation triangle budget changed: " + selection); }
            return parts;
        }

        /// <summary>Explicit authoring mutation. Reimports/saves only changed settings or generated mesh/material data.</summary>
        public static void Configure()
        {
            DerivativeManifest manifest = ValidateSourceIntegrity();
            foreach (string role in Roles)
            {
                string[] paths = TexturePaths(role);
                for (int channel = 0; channel < paths.Length; channel++) { ConfigureTexture(paths[channel], channel, role == "TreeLeaf" && channel == 0); }
                ConfigureMaterial(role);
            }
            EnsureFolder(MeshFolder);
            foreach (SelectionRecord selection in manifest.selections)
            {
                SelectionData data = ReadSelection(selection);
                foreach (PartData part in data.parts)
                {
                    string path = MeshPath(selection.selection, part.role);
                    Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                    if (existing != null && MeshMatches(existing, part)) { continue; }
                    var mesh = new Mesh { name = selection.selection + "_" + part.role, indexFormat = IndexFormat.UInt16 };
                    mesh.vertices = Vectors3(part.vertices);
                    mesh.normals = Vectors3(part.normals);
                    mesh.uv = Vectors2(part.uv);
                    mesh.triangles = part.triangles;
                    mesh.RecalculateBounds();
                    mesh.RecalculateTangents();
                    if (existing == null) { AssetDatabase.CreateAsset(mesh, path); }
                    else
                    {
                        EditorUtility.CopySerialized(mesh, existing);
                        UnityEngine.Object.DestroyImmediate(mesh);
                        EditorUtility.SetDirty(existing);
                        AssetDatabase.SaveAssetIfDirty(existing);
                    }
                }
            }
            ValidateGenerated(manifest);
        }

        /// <summary>Read-only: original manifests/files, derivative JSON/atlases/script, import settings and generated assets.</summary>
        public static void Validate()
        {
            DerivativeManifest manifest = ValidateSourceIntegrity();
            foreach (string role in Roles)
            {
                string[] paths = TexturePaths(role);
                for (int channel = 0; channel < paths.Length; channel++)
                {
                    var importer = AssetImporter.GetAtPath(paths[channel]) as TextureImporter;
                    if (importer == null || !TextureSettingsMatch(importer, channel, role == "TreeLeaf" && channel == 0))
                    {
                        throw new InvalidDataException("Measured vegetation texture import drift: " + paths[channel]);
                    }
                    importer.GetSourceTextureWidthAndHeight(out int width, out int height);
                    bool atlas = role == "TreeLeaf";
                    if (width != (atlas ? 1024 : 2048) || height != 2048) { throw new InvalidDataException("Measured vegetation source map dimensions changed: " + paths[channel]); }
                    RequireTexture(paths[channel]);
                }
                Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath(role));
                if (!MaterialMatches(material, role)) { throw new InvalidDataException("Measured vegetation material drift: " + role); }
            }
            ValidateGenerated(manifest);
        }

        public static void ValidateOriginalSources() { ValidateSourceIntegrity(); }

        private static DerivativeManifest ValidateSourceIntegrity()
        {
            for (int index = 0; index < IntakeFolders.Length; index++)
            {
                string folder = ProviderFolder + IntakeFolders[index];
                RequireHash(folder + "/manifest.json", -1, IntakeHashes[index]);
                SourceManifest source = JsonUtility.FromJson<SourceManifest>(File.ReadAllText(folder + "/manifest.json"));
                if (source == null || source.schemaVersion != 1 || source.license != "CC0-1.0" || source.status != "TECH_REVIEW" || source.files == null || source.files.Length == 0)
                {
                    throw new InvalidDataException("Invalid measured vegetation source manifest: " + folder);
                }
                long total = 0;
                foreach (SourceFile file in source.files)
                {
                    if (Path.GetFileName(file.path) != file.path || !file.url.StartsWith("https://dl.polyhaven.org/file/ph-assets/Models/", StringComparison.Ordinal))
                    {
                        throw new InvalidDataException("Unexpected source location in measured vegetation manifest.");
                    }
                    RequireHash(folder + "/" + file.path, file.bytes, file.sha256);
                    total += file.bytes;
                }
                if (total != source.totalSourceBytes) { throw new InvalidDataException("Measured vegetation source byte total changed: " + folder); }
                RequireHash(folder + "/CC0-1.0.txt", 7048,
                    "a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499");
            }
            string manifestPath = DataFolder + "/manifest.json";
            RequireHash(manifestPath, -1, ManifestHash);
            DerivativeManifest manifest = JsonUtility.FromJson<DerivativeManifest>(File.ReadAllText(manifestPath));
            if (manifest == null || manifest.schemaVersion != 1 || manifest.selections == null || manifest.selections.Length != Selections.Length ||
                manifest.atlases == null || manifest.atlases.Length != 3 || manifest.procedure != "Tools/Art/Blender/prepare_sunbreak_vegetation.py")
            {
                throw new InvalidDataException("Invalid measured vegetation derivative manifest.");
            }
            RequireHash(manifest.procedure, -1, manifest.procedureSha256);
            for (int i = 0; i < Selections.Length; i++)
            {
                SelectionRecord selection = manifest.selections[i];
                if (selection.selection != Selections[i] || selection.path != DataFolder + "/" + Selections[i] + ".json")
                {
                    throw new InvalidDataException("Measured vegetation stable selection order/path changed.");
                }
                RequireHash(selection.path, selection.bytes, selection.sha256);
                ReadSelection(selection);
            }
            string[] atlasPaths = TexturePaths("TreeLeaf");
            for (int i = 0; i < atlasPaths.Length; i++)
            {
                SourceFile atlas = manifest.atlases[i];
                if (atlas.path != atlasPaths[i]) { throw new InvalidDataException("Measured tree atlas role/path order changed."); }
                RequireHash(atlas.path, atlas.bytes, atlas.sha256);
            }
            return manifest;
        }

        private static SelectionData ReadSelection(SelectionRecord selection)
        {
            SelectionData data = JsonUtility.FromJson<SelectionData>(File.ReadAllText(selection.path));
            int selectionIndex = SelectionIndex(selection.selection);
            if (data == null || data.schemaVersion != 1 || data.selection != selection.selection || data.axis != "Unity Y-up meters; XZ-centered minY pivot" || data.parts == null ||
                selection.parts == null || data.parts.Length != selection.parts.Length || data.parts.Length != (selectionIndex == 0 ? 2 : 1))
            {
                throw new InvalidDataException("Invalid vegetation mesh derivative: " + selection.path);
            }
            int total = 0;
            var bounds = new Bounds();
            bool first = true;
            for (int i = 0; i < data.parts.Length; i++)
            {
                PartData part = data.parts[i];
                PartRecord record = selection.parts[i];
                string expectedRole = selectionIndex == 0 ? Roles[i] : selectionIndex == 7 ? "Fern" : "Grass";
                if (part.role != expectedRole || record.role != expectedRole || part.vertices == null || part.normals == null || part.uv == null || part.triangles == null ||
                    part.vertices.Length != record.vertices * 3 || part.normals.Length != part.vertices.Length || part.uv.Length != record.vertices * 2 ||
                    part.triangles.Length != record.triangles * 3 || record.vertices <= 0 || record.vertices > 65535 || record.triangles <= 0)
                {
                    throw new InvalidDataException("Vegetation derivative part contract changed: " + selection.selection + "/" + expectedRole);
                }
                for (int v = 0; v < part.vertices.Length; v += 3)
                {
                    var point = new Vector3(part.vertices[v], part.vertices[v + 1], part.vertices[v + 2]);
                    var normal = new Vector3(part.normals[v], part.normals[v + 1], part.normals[v + 2]);
                    if (!Finite(point) || !Finite(normal) || normal.sqrMagnitude < 0.9f || normal.sqrMagnitude > 1.1f) { throw new InvalidDataException("Vegetation derivative vertex/normal is invalid."); }
                    if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                    else { bounds.Encapsulate(point); }
                }
                foreach (float uv in part.uv) { if (!Finite(uv)) { throw new InvalidDataException("Non-finite vegetation source UV."); } }
                foreach (int index in part.triangles) { if (index < 0 || index >= record.vertices) { throw new InvalidDataException("Vegetation triangle index out of bounds."); } }
                total += record.triangles;
            }
            if (total != SelectionTriangles[selectionIndex] || selection.boundsSize == null || selection.boundsSize.Length != 3 ||
                Mathf.Abs(bounds.center.x) > 0.00001f || Mathf.Abs(bounds.center.z) > 0.00001f || Mathf.Abs(bounds.min.y) > 0.00001f ||
                (bounds.size - new Vector3(selection.boundsSize[0], selection.boundsSize[1], selection.boundsSize[2])).sqrMagnitude > 0.000001f)
            {
                throw new InvalidDataException("Vegetation derivative must retain centered XZ/minY0 meter bounds and its triangle budget: " + selection.selection);
            }
            return data;
        }

        private static void ValidateGenerated(DerivativeManifest manifest)
        {
            foreach (SelectionRecord selection in manifest.selections)
            {
                SelectionData data = ReadSelection(selection);
                Part[] parts = GetParts(selection.selection);
                for (int index = 0; index < parts.Length; index++)
                {
                    if (!MeshMatches(parts[index].Mesh, data.parts[index])) { throw new InvalidDataException("Generated vegetation mesh no longer matches its source derivative: " + selection.selection); }
                }
            }
        }

        private static bool MeshMatches(Mesh mesh, PartData data)
        {
            if (mesh.subMeshCount != 1 || mesh.vertexCount * 3 != data.vertices.Length || mesh.tangents.Length != mesh.vertexCount || mesh.GetIndexCount(0) != data.triangles.Length) { return false; }
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            Vector2[] uv = mesh.uv;
            int[] indices = mesh.triangles;
            if (normals.Length != vertices.Length || uv.Length != vertices.Length) { return false; }
            for (int i = 0; i < vertices.Length; i++)
            {
                int v = i * 3, u = i * 2;
                if (!vertices[i].Equals(new Vector3(data.vertices[v], data.vertices[v + 1], data.vertices[v + 2])) ||
                    !normals[i].Equals(new Vector3(data.normals[v], data.normals[v + 1], data.normals[v + 2])) || !uv[i].Equals(new Vector2(data.uv[u], data.uv[u + 1]))) { return false; }
            }
            for (int i = 0; i < indices.Length; i++) { if (indices[i] != data.triangles[i]) { return false; } }
            foreach (Vector4 tangent in mesh.tangents)
            {
                if (!Finite(tangent.x) || !Finite(tangent.y) || !Finite(tangent.z) || !Finite(tangent.w)) { return false; }
            }
            return true;
        }

        private static string[] TexturePaths(string role)
        {
            if (role == "TreeLeaf")
            {
                string folder = ProviderFolder + "TreeSmall02/Derived/";
                return new[] { folder + "TreeClusterAtlas.png", folder + "TreeClusterNormal.png", folder + "TreeClusterRoughness.png" };
            }
            string stem = role == "TreeTrunk" ? "TreeSmall02/tree_small_02" : role == "Grass" ? "GrassMedium01/grass_medium_01" : role == "Fern" ? "Fern02/fern_02" : null;
            if (stem == null) { throw new ArgumentOutOfRangeException(nameof(role)); }
            stem = ProviderFolder + stem;
            return role == "TreeTrunk"
                ? new[] { stem + "_diff_2k.jpg", stem + "_nor_gl_2k.jpg", stem + "_rough_2k.jpg" }
                : new[] { stem + "_diff_2k.jpg", stem + "_nor_gl_2k.jpg", stem + "_rough_2k.jpg", stem + "_alpha_2k.png" };
        }

        private static void ConfigureMaterial(string role)
        {
            string path = MaterialPath(role);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (MaterialMatches(material, role)) { return; }
            bool opaque = role == "TreeTrunk";
            Shader shader = Shader.Find(opaque ? OpaqueShader : VegetationShader);
            if (shader == null || ShaderUtil.ShaderHasError(shader)) { throw new InvalidDataException("Measured vegetation shader missing or invalid: " + role); }
            string[] paths = TexturePaths(role);
            var textures = new Texture2D[paths.Length];
            for (int i = 0; i < paths.Length; i++) { textures[i] = RequireTexture(paths[i]); }
            bool create = material == null;
            if (create) { material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) }; }
            material.shader = shader;
            foreach (string property in material.GetTexturePropertyNames()) { material.SetTexture(property, null); }
            material.shaderKeywords = Array.Empty<string>();
            material.SetTexture("_BaseMap", textures[0]);
            material.SetTexture("_BumpMap", textures[1]);
            material.SetTexture("_RoughnessMap", textures[2]);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_Cull", opaque ? 2f : 0f);
            if (opaque)
            {
                material.SetFloat("_OcclusionStrength", 0f);
                material.SetFloat("_Surface", 0f);
                material.SetFloat("_AlphaClip", 0f);
                material.SetFloat("_SecondLayer", 0f);
                material.SetFloat("_PackedMasonry", 0f);
            }
            else
            {
                if (textures.Length == 4) { material.SetTexture("_AlphaMap", textures[3]); }
                else { material.SetTexture("_AlphaMap", Texture2D.whiteTexture); }
                material.SetFloat("_AlphaFromBase", role == "TreeLeaf" ? 1f : 0f);
                material.SetFloat("_Cutoff", 0.4f);
            }
            material.renderQueue = (int)(opaque ? RenderQueue.Geometry : RenderQueue.AlphaTest);
            material.enableInstancing = true;
            if (create) { AssetDatabase.CreateAsset(material, path); }
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
        }

        private static bool MaterialMatches(Material material, string role)
        {
            bool opaque = role == "TreeTrunk";
            if (material == null || material.shader == null || material.shader.name != (opaque ? OpaqueShader : VegetationShader) || ShaderUtil.ShaderHasError(material.shader)) { return false; }
            string[] paths = TexturePaths(role);
            string[] properties = { "_BaseMap", "_BumpMap", "_RoughnessMap", "_AlphaMap" };
            for (int i = 0; i < paths.Length; i++)
            {
                if (!(material.GetTexture(properties[i]) is Texture2D) || AssetDatabase.GetAssetPath(material.GetTexture(properties[i])) != paths[i]) { return false; }
            }
            if (material.GetTextureScale("_BaseMap") != Vector2.one || material.GetTextureOffset("_BaseMap") != Vector2.zero ||
                material.GetColor("_BaseColor") != Color.white || material.GetFloat("_BumpScale") != 1f || material.GetFloat("_Smoothness") != 1f ||
                material.GetFloat("_Cull") != (opaque ? 2f : 0f) || material.renderQueue != (int)(opaque ? RenderQueue.Geometry : RenderQueue.AlphaTest) ||
                material.shaderKeywords.Length != 0 || !material.enableInstancing) { return false; }
            return opaque ? material.GetFloat("_OcclusionStrength") == 0f && material.GetFloat("_Surface") == 0f && material.GetFloat("_AlphaClip") == 0f &&
                material.GetFloat("_SecondLayer") == 0f && material.GetFloat("_PackedMasonry") == 0f :
                material.GetFloat("_AlphaFromBase") == (role == "TreeLeaf" ? 1f : 0f) && material.GetFloat("_Cutoff") == 0.4f;
        }

        private static void ConfigureTexture(string path, int channel, bool alphaFromBase)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                importer = AssetImporter.GetAtPath(path) as TextureImporter;
            }
            if (importer == null) { throw new InvalidDataException("Missing vegetation texture importer: " + path); }
            if (TextureSettingsMatch(importer, channel, alphaFromBase)) { return; }
            importer.textureType = channel == 1 ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.convertToNormalmap = false;
            importer.flipGreenChannel = false;
            importer.sRGBTexture = channel == 0;
            importer.alphaSource = alphaFromBase ? TextureImporterAlphaSource.FromInput : channel == 3 ? TextureImporterAlphaSource.FromGrayScale : TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = alphaFromBase;
            importer.mipmapEnabled = true;
            importer.mipMapsPreserveCoverage = alphaFromBase || channel == 3;
            importer.alphaTestReferenceValue = 0.4f;
            importer.streamingMipmaps = true;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 4;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            android.name = "Android";
            android.overridden = true;
            android.maxTextureSize = 1024;
            android.format = channel == 1 || channel == 3 || alphaFromBase ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 100;
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
        }

        private static bool TextureSettingsMatch(TextureImporter importer, int channel, bool alphaFromBase)
        {
            TextureImporterPlatformSettings android = importer.GetPlatformTextureSettings("Android");
            return importer.textureType == (channel == 1 ? TextureImporterType.NormalMap : TextureImporterType.Default) && importer.textureShape == TextureImporterShape.Texture2D &&
                !importer.convertToNormalmap && !importer.flipGreenChannel && importer.sRGBTexture == (channel == 0) &&
                importer.alphaSource == (alphaFromBase ? TextureImporterAlphaSource.FromInput : channel == 3 ? TextureImporterAlphaSource.FromGrayScale : TextureImporterAlphaSource.None) && importer.alphaIsTransparency == alphaFromBase &&
                importer.mipMapsPreserveCoverage == (alphaFromBase || channel == 3) && importer.alphaTestReferenceValue == 0.4f &&
                importer.mipmapEnabled && importer.streamingMipmaps && !importer.isReadable && importer.npotScale == TextureImporterNPOTScale.None &&
                importer.maxTextureSize == 2048 && importer.wrapMode == TextureWrapMode.Clamp && importer.filterMode == FilterMode.Trilinear &&
                importer.anisoLevel == 4 && importer.textureCompression == TextureImporterCompression.CompressedHQ && android.overridden &&
                android.maxTextureSize == 1024 && android.compressionQuality == 100 &&
                android.format == (channel == 1 || channel == 3 || alphaFromBase ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6);
        }

        private static Texture2D RequireTexture(string path)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (texture == null || texture.dimension != TextureDimension.Tex2D) { throw new InvalidDataException("Measured vegetation map must import as Texture2D: " + path); }
            return texture;
        }
        private static int SelectionIndex(string selection)
        {
            int index = Array.IndexOf(Selections, selection);
            if (index < 0) { throw new ArgumentOutOfRangeException(nameof(selection), "Unknown stable measured vegetation selection."); }
            return index;
        }
        private static Vector3[] Vectors3(float[] input)
        {
            var output = new Vector3[input.Length / 3];
            for (int i = 0; i < output.Length; i++) { output[i] = new Vector3(input[i * 3], input[i * 3 + 1], input[i * 3 + 2]); }
            return output;
        }
        private static Vector2[] Vectors2(float[] input)
        {
            var output = new Vector2[input.Length / 2];
            for (int i = 0; i < output.Length; i++) { output[i] = new Vector2(input[i * 2], input[i * 2 + 1]); }
            return output;
        }
        private static void RequireHash(string path, long bytes, string expected)
        {
            if (!File.Exists(path) || (bytes >= 0 && new FileInfo(path).Length != bytes) || string.IsNullOrEmpty(expected) || expected.Length != 64) { throw new InvalidDataException("Missing or changed vegetation source: " + path); }
            using (SHA256 sha = SHA256.Create())
            using (FileStream stream = File.OpenRead(path))
            {
                string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty).ToLowerInvariant();
                if (actual != expected) { throw new InvalidDataException("Vegetation source hash mismatch: " + path); }
            }
        }
        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);

        [Serializable] private sealed class SourceManifest { public int schemaVersion; public string license; public string status; public long totalSourceBytes; public SourceFile[] files; }
        [Serializable] private sealed class SourceFile { public string path; public long bytes; public string sha256; public string url; }
        [Serializable] private sealed class DerivativeManifest { public int schemaVersion; public string procedure; public string procedureSha256; public SelectionRecord[] selections; public SourceFile[] atlases; }
        [Serializable] private sealed class SelectionRecord { public string selection; public string path; public long bytes; public string sha256; public PartRecord[] parts; public float[] boundsSize; }
        [Serializable] private sealed class PartRecord { public string role; public int triangles; public int vertices; }
        [Serializable] private sealed class SelectionData { public int schemaVersion; public string selection; public string axis; public PartData[] parts; }
        [Serializable] private sealed class PartData { public string role; public float[] vertices; public float[] normals; public float[] uv; public int[] triangles; }
    }
}
