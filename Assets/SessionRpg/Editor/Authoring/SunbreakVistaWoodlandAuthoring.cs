using System;
using System.Collections.Generic;
using SessionRpg.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>
    /// Resident, outside-Core woodland. Actual woody branches and intersecting
    /// measured branch-cluster cards supply parallax; no whole-tree billboard,
    /// primitive crown, camera-facing update, or gameplay component is generated.
    /// </summary>
    public static class SunbreakVistaWoodlandAuthoring
    {
        public const string BranchesName = "FoothillWoodlandBranches";
        public const string CanopiesName = "FoothillWoodlandCanopies";
        public const string BranchMaterialPath = "Assets/SessionRpg/Art/Materials/Sunbreak_VistaBark.mat";
        public const string LeafMaterialPath = "Assets/SessionRpg/Art/Materials/Sunbreak_VistaLeaves.mat";
        public const int GroveCount = 14;
        public const int TreesPerGrove = 6;
        public const int TreeCount = GroveCount * TreesPerGrove;
        public const int BranchTrianglesPerTree = 40;
        public const int LeafTrianglesPerTree = 160;
        public const int BranchVerticesPerTree = 80;
        public const int LeafVerticesPerTree = 320;
        public const int MaximumTriangles = TreeCount * (BranchTrianglesPerTree + LeafTrianglesPerTree);
        public const float CoreClearance = 2f;

        // Irregular, deliberately separated groves on two overlapping shoulders.
        // There is no row at one radius: rock saddles and glades remain visible.
        private static readonly Vector2[] Groves =
        {
            new Vector2(-164f, 34f), new Vector2(-137f, 62f), new Vector2(-111f, 31f),
            new Vector2(-82f, 70f), new Vector2(-53f, 42f), new Vector2(-27f, 30f),
            new Vector2(-4f, 60f), new Vector2(22f, 29f), new Vector2(41f, 71f),
            new Vector2(61f, 33f), new Vector2(87f, 58f), new Vector2(119f, 29f),
            new Vector2(146f, 61f), new Vector2(170f, 35f)
        };

        public static void Build(Transform parent, Mesh terrain)
        {
            if (terrain == null || parent.position != Vector3.zero || parent.rotation != Quaternion.identity ||
                parent.lossyScale != Vector3.one)
            { throw new InvalidOperationException("Woodland requires the world-space resident Vista terrain."); }
            var branches = new Draft();
            var leaves = new Draft();
            Vector3[] ground = terrain.vertices;
            int[] groundTriangles = terrain.triangles;
            for (int grove = 0; grove < Groves.Length; grove++)
            {
                for (int tree = 0; tree < TreesPerGrove; tree++)
                {
                    int identity = grove * TreesPerGrove + tree;
                    var random = new System.Random(7187 + identity * 7919);
                    float angle = Groves[grove].x * Mathf.Deg2Rad;
                    Vector2 radial = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                    Vector2 across = new Vector2(-radial.y, radial.x);
                    float spacingAngle = tree * 2.3999632f + (float)random.NextDouble() * 0.45f;
                    float spacingRadius = tree == 0 ? 0f : 1.55f * Mathf.Sqrt(tree);
                    float radius = 92f / Mathf.Max(Mathf.Abs(radial.x), Mathf.Abs(radial.y));
                    Vector2 center = new Vector2(32f, 32f) + radial * (radius + Groves[grove].y) +
                        across * (Mathf.Cos(spacingAngle) * spacingRadius * 1.05f) +
                        radial * (Mathf.Sin(spacingAngle) * spacingRadius * 0.65f);
                    float height = 6.5f + (float)random.NextDouble() * 3.5f;
                    if (tree == 0) { height += 1.2f; }
                    float yaw = (float)random.NextDouble() * Mathf.PI * 2f;
                    var localBranches = new Draft();
                    var localLeaves = new Draft();
                    BuildTree(localBranches, localLeaves, height, yaw, random, identity);
                    Bounds footprint = localBranches.Bounds;
                    footprint.Encapsulate(localLeaves.Bounds);
                    // Move the intact representation outward when a crown at an
                    // oblique boundary needs room. Never clamp its vertices.
                    int shifts = 0;
                    while (!OutsideCore(footprint, center) && shifts++ < 12) { center += radial * 2f; }
                    if (!OutsideCore(footprint, center))
                    { throw new InvalidOperationException("Authored woodland footprint intersects Core."); }
                    float surfaceY = SurfaceHeight(center, ground, groundTriangles);
                    var origin = new Vector3(center.x, surfaceY - 0.32f, center.y);
                    branches.Append(localBranches, origin);
                    leaves.Append(localLeaves, origin);
                }
            }
            Material bark = CloneMaterial(SunbreakVegetationAssets.MaterialPath("TreeTrunk"), BranchMaterialPath,
                new Color(0.72f, 0.76f, 0.68f), false);
            Material leaf = CloneMaterial(SunbreakVegetationAssets.MaterialPath("TreeLeaf"), LeafMaterialPath,
                new Color(0.79f, 0.93f, 0.78f), true);
            Publish(parent, BranchesName, branches, bark);
            Publish(parent, CanopiesName, leaves, leaf);
        }

        private static void BuildTree(Draft branches, Draft leaves, float height, float yaw,
            System.Random random, int identity)
        {
            Vector3 bend = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * height * 0.035f;
            Vector3 fork = Vector3.up * height * 0.25f + bend * 0.45f;
            Vector3 leader = Vector3.up * height * 0.78f + bend;
            branches.Tube(Vector3.zero, fork, height * 0.026f, height * 0.014f, 4);
            branches.Tube(fork, leader, height * 0.014f, height * 0.004f, 4);
            for (int bough = 0; bough < 8; bough++)
            {
                float phase = yaw + bough * 2.3999632f;
                Vector3 direction = new Vector3(Mathf.Cos(phase), 0f, Mathf.Sin(phase));
                float reach = height * (0.18f + 0.06f * Mathf.Sin(bough * 0.9f + 0.5f));
                Vector3 center = bend + Vector3.up * height * (0.31f + bough * 0.074f) + direction * reach;
                if (bough < 4)
                {
                    Vector3 attachment = fork + Vector3.up * height * (bough * 0.07f);
                    branches.Tube(attachment, center, height * 0.012f, height * 0.003f, 3);
                }
                float size = height * (0.40f + (float)random.NextDouble() * 0.045f);
                // The rejected V1 had only two sparse pairs per bough and left
                // pole-like trunks. Five separated crossed pairs now overlap
                // through each lower/wider crown, rather than one opaque shell.
                for (int cluster = 0; cluster < 5; cluster++)
                {
                    float clusterPhase = phase + cluster * 2.3999632f;
                    Vector3 offset = new Vector3(Mathf.Cos(clusterPhase), 0f, Mathf.Sin(clusterPhase)) * height * 0.06f +
                        Vector3.up * height * (-0.05f + cluster * 0.025f);
                    Quaternion rotation = Quaternion.Euler(20f + bough * 7f, phase * Mathf.Rad2Deg + cluster * 61f,
                        -28f + (float)random.NextDouble() * 56f);
                    Vector3 right = rotation * Vector3.right * size;
                    Vector3 up = rotation * Vector3.up * size;
                    Vector3 depth = rotation * Vector3.forward * size;
                    Vector3 crownNormal = (direction * 0.45f + Vector3.up).normalized;
                    int row = (identity + bough + cluster) % 4;
                    leaves.Card(center + offset, right, up, crownNormal, row, 0);
                    leaves.Card(center + offset, right, depth, crownNormal, row, 1);
                }
            }
        }

        public static void Validate(GameObject root)
        {
            foreach (string name in new[] { BranchesName, CanopiesName })
            {
                Transform child = root.transform.Find(name);
                MeshFilter filter = child == null ? null : child.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                { throw new InvalidOperationException("Woodland must retain its two generated mesh batches."); }
                Mesh mesh = filter.sharedMesh;
                int expected = TreeCount * (name == BranchesName ? BranchTrianglesPerTree : LeafTrianglesPerTree);
                if (mesh.subMeshCount != 1 || mesh.GetIndexCount(0) / 3 != expected)
                { throw new InvalidOperationException("Woodland exceeded its exact source representation budget."); }
                foreach (Vector3 vertex in mesh.vertices)
                {
                    Vector3 world = filter.transform.TransformPoint(vertex);
                    if (world.x >= PveRegionLayout.MinimumX - CoreClearance && world.x <= PveRegionLayout.MaximumX + CoreClearance &&
                        world.z >= PveRegionLayout.MinimumZ - CoreClearance && world.z <= PveRegionLayout.MaximumZ + CoreClearance)
                    { throw new InvalidOperationException("A woodland branch or leaf intrudes into the Core clearance."); }
                }
            }
        }

        private static bool OutsideCore(Bounds local, Vector2 origin)
        {
            return local.max.x + origin.x < PveRegionLayout.MinimumX - CoreClearance ||
                local.min.x + origin.x > PveRegionLayout.MaximumX + CoreClearance ||
                local.max.z + origin.y < PveRegionLayout.MinimumZ - CoreClearance ||
                local.min.z + origin.y > PveRegionLayout.MaximumZ + CoreClearance;
        }

        private static float SurfaceHeight(Vector2 point, Vector3[] vertices, int[] indices)
        {
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                float denominator = (b.z - c.z) * (a.x - c.x) + (c.x - b.x) * (a.z - c.z);
                if (Mathf.Abs(denominator) < 0.000001f) { continue; }
                float u = ((b.z - c.z) * (point.x - c.x) + (c.x - b.x) * (point.y - c.z)) / denominator;
                float v = ((c.z - a.z) * (point.x - c.x) + (a.x - c.x) * (point.y - c.z)) / denominator;
                if (u >= -0.00001f && v >= -0.00001f && u + v <= 1.00001f)
                { return u * a.y + v * b.y + (1f - u - v) * c.y; }
            }
            throw new InvalidOperationException("Woodland has no actual triangulated terrain beneath its root.");
        }

        private static Material CloneMaterial(string sourcePath, string path, Color color, bool leaf)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(sourcePath);
            if (source == null) { throw new InvalidOperationException("Vetted measured woodland source material missing."); }
            Material result = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = result == null;
            if (create) { result = new Material(source); }
            else { result.shader = source.shader; result.CopyPropertiesFromMaterial(source); }
            result.name = leaf ? "Sunbreak_VistaLeaves" : "Sunbreak_VistaBark";
            result.SetColor("_BaseColor", color);
            result.SetFloat("_BumpScale", leaf ? 0.45f : 0.7f);
            result.SetFloat("_Smoothness", leaf ? 0.3f : 0.65f);
            if (create) { AssetDatabase.CreateAsset(result, path); }
            EditorUtility.SetDirty(result);
            return result;
        }

        private static void Publish(Transform parent, string name, Draft draft, Material material)
        {
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
            mesh.SetVertices(draft.Vertices);
            mesh.SetNormals(draft.Normals);
            mesh.SetUVs(0, draft.Uv);
            mesh.SetTriangles(draft.Indices, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            string path = SunbreakVistaAuthoring.Folder + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(mesh, path); }
            else
            {
                EditorUtility.CopySerialized(mesh, existing);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = existing;
                EditorUtility.SetDirty(existing);
            }
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = 2 };
            child.transform.SetParent(parent, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic);
        }

        private sealed class Draft
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Indices = new List<int>();
            public Bounds Bounds
            {
                get
                {
                    var result = new Bounds(Vertices[0], Vector3.zero);
                    foreach (Vector3 point in Vertices) { result.Encapsulate(point); }
                    return result;
                }
            }
            public void Append(Draft other, Vector3 origin)
            {
                int start = Vertices.Count;
                foreach (Vector3 point in other.Vertices) { Vertices.Add(point + origin); }
                Normals.AddRange(other.Normals);
                Uv.AddRange(other.Uv);
                foreach (int index in other.Indices) { Indices.Add(start + index); }
            }
            public void Card(Vector3 center, Vector3 right, Vector3 up, Vector3 normal, int row, int column)
            {
                int first = Vertices.Count;
                Vertices.Add(center - right * 0.5f - up * 0.5f);
                Vertices.Add(center + right * 0.5f - up * 0.5f);
                Vertices.Add(center + right * 0.5f + up * 0.5f);
                Vertices.Add(center - right * 0.5f + up * 0.5f);
                for (int i = 0; i < 4; i++) { Normals.Add(normal); }
                // Inset one source pixel: the atlas retains its measured alpha
                // while mip filtering never borrows a neighboring bough cell.
                float u0 = column * 0.5f + 1f / 1024f, u1 = (column + 1) * 0.5f - 1f / 1024f;
                float v0 = row * 0.25f + 1f / 2048f, v1 = (row + 1) * 0.25f - 1f / 2048f;
                Uv.Add(new Vector2(u0, v0)); Uv.Add(new Vector2(u1, v0));
                Uv.Add(new Vector2(u1, v1)); Uv.Add(new Vector2(u0, v1));
                Indices.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
            }
            public void Tube(Vector3 a, Vector3 b, float radiusA, float radiusB, int sides)
            {
                Vector3 axis = (b - a).normalized;
                Vector3 across = Vector3.Cross(axis, Mathf.Abs(axis.y) > 0.9f ? Vector3.forward : Vector3.up).normalized;
                Vector3 depth = Vector3.Cross(axis, across).normalized;
                float length = Vector3.Distance(a, b);
                for (int side = 0; side < sides; side++)
                {
                    float phaseA = side * Mathf.PI * 2f / sides, phaseB = (side + 1) * Mathf.PI * 2f / sides;
                    Vector3 normalA = across * Mathf.Cos(phaseA) + depth * Mathf.Sin(phaseA);
                    Vector3 normalB = across * Mathf.Cos(phaseB) + depth * Mathf.Sin(phaseB);
                    int first = Vertices.Count;
                    Vertices.Add(a + normalA * radiusA); Vertices.Add(a + normalB * radiusA);
                    Vertices.Add(b + normalB * radiusB); Vertices.Add(b + normalA * radiusB);
                    Normals.Add(normalA); Normals.Add(normalB); Normals.Add(normalB); Normals.Add(normalA);
                    Uv.Add(new Vector2((float)side / sides, 0f)); Uv.Add(new Vector2((float)(side + 1) / sides, 0f));
                    Uv.Add(new Vector2((float)(side + 1) / sides, length * 0.25f)); Uv.Add(new Vector2((float)side / sides, length * 0.25f));
                    Indices.AddRange(new[] { first, first + 1, first + 2, first, first + 2, first + 3 });
                }
            }
        }
    }
}
