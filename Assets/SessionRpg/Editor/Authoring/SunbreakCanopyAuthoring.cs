using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>
    /// Editor-only secondary representation of the admitted TreeSmall02 trial.
    /// Each source leaf-cluster pair grows uniformly about its original cluster
    /// center, limited by the original projected hull and vertical envelope.
    /// No original source, atlas, trunk, UV, or tangent frame is overwritten.
    /// </summary>
    public static class SunbreakCanopyAuthoring
    {
        public const string Folder = "Assets/SessionRpg/Art/Environment/SunbreakCanopyV2";
        public const string LeafMeshPath = Folder + "/TreeSmall02_DenseLeaves.asset";
        public const int LeafTriangles = 6328;
        public const int TreeTriangles = 9328;
        public const float MaximumClusterScale = 1.9f;

        /// <summary>Explicit rebuild-only mutation; publishes only the new derivative leaf mesh.</summary>
        public static SunbreakVegetationAssets.Part[] BuildParts()
        {
            SunbreakVegetationAssets.Part[] source = SunbreakVegetationAssets.GetParts("TreeSmall02");
            Mesh authored = CreateFoliageMesh(source[1].Mesh);
            EnsureFolder(Folder);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(LeafMeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(authored, LeafMeshPath);
                existing = authored;
            }
            else
            {
                EditorUtility.CopySerialized(authored, existing);
                Object.DestroyImmediate(authored);
                EditorUtility.SetDirty(existing);
                AssetDatabase.SaveAssetIfDirty(existing);
            }
            return new[] { source[0], new SunbreakVegetationAssets.Part(existing, source[1].Material) };
        }

        /// <summary>Read-only lookup. Rebuild is required before this candidate can be used.</summary>
        public static SunbreakVegetationAssets.Part[] GetParts()
        {
            SunbreakVegetationAssets.Part[] source = SunbreakVegetationAssets.GetParts("TreeSmall02");
            Mesh leaf = AssetDatabase.LoadAssetAtPath<Mesh>(LeafMeshPath);
            if (leaf == null || leaf.subMeshCount != 1 || leaf.GetTopology(0) != MeshTopology.Triangles ||
                leaf.vertexCount != LeafTriangles * 3 || leaf.GetIndexCount(0) != LeafTriangles * 3)
            {
                throw new InvalidDataException("Missing or invalid source-derived Sunbreak canopy; rebuild the authored foundation.");
            }
            return new[] { source[0], new SunbreakVegetationAssets.Part(leaf, source[1].Material) };
        }

        /// <summary>Read-only exact generated-data validation against the current authoring procedure.</summary>
        public static void Validate()
        {
            Mesh actual = GetParts()[1].Mesh;
            Mesh expected = CreateFoliageMesh(SunbreakVegetationAssets.GetParts("TreeSmall02")[1].Mesh);
            try
            {
                if (!Same(actual.vertices, expected.vertices) || !Same(actual.normals, expected.normals) ||
                    !Same(actual.tangents, expected.tangents) || !Same(actual.uv, expected.uv) ||
                    !Same(actual.triangles, expected.triangles) || actual.indexFormat != IndexFormat.UInt16)
                {
                    throw new InvalidDataException("Generated Sunbreak canopy no longer matches its source-derived procedure.");
                }
            }
            finally { Object.DestroyImmediate(expected); }
        }

        /// <summary>
        /// Pure editor geometry. Triangle corners are split so the rare shared
        /// source corner cannot be transformed by two different cluster scales.
        /// Each complete crossed-card pair retains its orientation, UVs and
        /// source normal/tangent frame; the operation never clips vertices.
        /// </summary>
        public static Mesh CreateFoliageMesh(Mesh source)
        {
            if (source == null) { throw new ArgumentNullException(nameof(source)); }
            if (source.subMeshCount != 1 || source.GetTopology(0) != MeshTopology.Triangles ||
                source.GetIndexCount(0) != LeafTriangles * 3)
            {
                throw new ArgumentException("The canopy derivative requires the exact admitted TreeSmall02 foliage part.", nameof(source));
            }
            Vector3[] original = source.vertices;
            Vector3[] sourceNormals = source.normals;
            Vector4[] sourceTangents = source.tangents;
            Vector2[] sourceUv = source.uv;
            int[] sourceIndices = source.triangles;
            if (sourceNormals.Length != original.Length || sourceTangents.Length != original.Length || sourceUv.Length != original.Length)
            {
                throw new ArgumentException("The source canopy must retain complete UV and tangent frames.", nameof(source));
            }

            List<Vector2> hull = ConvexHull(original);
            float minimumY = float.PositiveInfinity, maximumY = float.NegativeInfinity;
            foreach (Vector3 point in original)
            {
                minimumY = Mathf.Min(minimumY, point.y);
                maximumY = Mathf.Max(maximumY, point.y);
            }
            var vertices = new Vector3[sourceIndices.Length];
            var normals = new Vector3[sourceIndices.Length];
            var tangents = new Vector4[sourceIndices.Length];
            var uv = new Vector2[sourceIndices.Length];
            var indices = new int[sourceIndices.Length];
            for (int start = 0; start < sourceIndices.Length; start += 12)
            {
                // The locked V1 export retains four consecutive triangles per
                // paired cluster; its first two corners span the first quad.
                Vector3 center = (original[sourceIndices[start]] + original[sourceIndices[start + 1]]) * 0.5f;
                float scale = MaximumClusterScale;
                for (int corner = start; corner < start + 12; corner++)
                {
                    Vector3 offset = original[sourceIndices[corner]] - center;
                    if (offset.y > 0.00000001f) { scale = Mathf.Min(scale, (maximumY - center.y) / offset.y); }
                    else if (offset.y < -0.00000001f) { scale = Mathf.Min(scale, (minimumY - center.y) / offset.y); }
                    for (int edge = 0; edge < hull.Count; edge++)
                    {
                        Vector2 a = hull[edge], b = hull[(edge + 1) % hull.Count];
                        var outward = new Vector2(b.y - a.y, a.x - b.x);
                        float direction = outward.x * offset.x + outward.y * offset.z;
                        if (direction <= 0.00000001f) { continue; }
                        float remaining = outward.x * (a.x - center.x) + outward.y * (a.y - center.z);
                        scale = Mathf.Min(scale, remaining / direction);
                    }
                }
                // Numerical disagreement on the unchanged source boundary must
                // never shrink a cluster. Whole-pair scaling keeps flat cards
                // planar and preserves their normal-map basis.
                scale = Mathf.Max(1f, scale);
                for (int corner = start; corner < start + 12; corner++)
                {
                    int sourceIndex = sourceIndices[corner];
                    vertices[corner] = center + (original[sourceIndex] - center) * scale;
                    normals[corner] = sourceNormals[sourceIndex];
                    tangents[corner] = sourceTangents[sourceIndex];
                    uv[corner] = sourceUv[sourceIndex];
                    indices[corner] = corner;
                }
            }
            var mesh = new Mesh { name = "TreeSmall02_DenseLeaves", indexFormat = IndexFormat.UInt16 };
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.tangents = tangents;
            mesh.uv = uv;
            mesh.triangles = indices;
            mesh.RecalculateBounds();
            return mesh;
        }

        private static List<Vector2> ConvexHull(Vector3[] vertices)
        {
            var points = new List<Vector2>(vertices.Length);
            foreach (Vector3 vertex in vertices) { points.Add(new Vector2(vertex.x, vertex.z)); }
            points.Sort((a, b) => a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y));
            var unique = new List<Vector2>(points.Count);
            foreach (Vector2 point in points)
            {
                if (unique.Count == 0 || !unique[unique.Count - 1].Equals(point)) { unique.Add(point); }
            }
            var hull = new List<Vector2>();
            foreach (Vector2 point in unique)
            {
                while (hull.Count >= 2 && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f) { hull.RemoveAt(hull.Count - 1); }
                hull.Add(point);
            }
            int lowerCount = hull.Count;
            for (int i = unique.Count - 2; i >= 0; i--)
            {
                Vector2 point = unique[i];
                while (hull.Count > lowerCount && Cross(hull[hull.Count - 2], hull[hull.Count - 1], point) <= 0f) { hull.RemoveAt(hull.Count - 1); }
                hull.Add(point);
            }
            hull.RemoveAt(hull.Count - 1);
            if (hull.Count < 3) { throw new InvalidDataException("The source tree has no projected canopy envelope."); }
            return hull;
        }

        private static float Cross(Vector2 a, Vector2 b, Vector2 c) =>
            (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);

        private static bool Same<T>(T[] left, T[] right) where T : IEquatable<T>
        {
            if (left.Length != right.Length) { return false; }
            for (int i = 0; i < left.Length; i++) { if (!left[i].Equals(right[i])) { return false; } }
            return true;
        }

        private static void EnsureFolder(string path)
        {
            string[] pieces = path.Split('/');
            string current = pieces[0];
            for (int i = 1; i < pieces.Length; i++)
            {
                string next = current + "/" + pieces[i];
                if (!AssetDatabase.IsValidFolder(next)) { AssetDatabase.CreateFolder(current, pieces[i]); }
                current = next;
            }
        }
    }
}
