using System;
using System.Collections.Generic;
using SessionRpg.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>
    /// A low-cost, always-resident visual horizon for the oblique PvE camera.
    /// Near chunks remain the only detailed world; this never supplies gameplay
    /// geometry, streaming authority, physics, or a second lighting system.
    /// </summary>
    public static class SunbreakVistaAuthoring
    {
        public const string Folder = "Assets/SessionRpg/Art/Environment/SunbreakVista";
        public const string SkyPath = "Assets/SessionRpg/Art/Materials/Sunbreak_DaylightSky.mat";
        public const int MaximumTriangles = 28000;
        public const int MaximumRenderers = 4;
        public const float UnderlayHeight = -0.045f;

        // World-space ridgelines, not copies of one perimeter profile. Each
        // station is (X, summit elevation, Z); unequal peaks and low end spurs
        // compose five overlapping massifs with open saddles between them.
        private static readonly Vector3[][] Massifs =
        {
            new[] { new Vector3(142f, 12f, 238f), new Vector3(191f, 33f, 239f), new Vector3(219f, 54f, 193f),
                new Vector3(251f, 35f, 201f), new Vector3(279f, 12f, 172f) },
            new[] { new Vector3(288f, 8f, 135f), new Vector3(274f, 21f, 92f), new Vector3(270f, 44f, 35f),
                new Vector3(292f, 23f, -6f), new Vector3(258f, 39f, -52f), new Vector3(218f, 8f, -90f) },
            new[] { new Vector3(198f, 10f, -153f), new Vector3(145f, 33f, -185f), new Vector3(104f, 50f, -200f),
                new Vector3(60f, 28f, -205f), new Vector3(12f, 42f, -190f), new Vector3(-45f, 8f, -160f) },
            new[] { new Vector3(-170f, 10f, -125f), new Vector3(-206f, 32f, -75f), new Vector3(-195f, 46f, -28f),
                new Vector3(-230f, 28f, 20f), new Vector3(-204f, 44f, 75f), new Vector3(-166f, 8f, 126f) },
            new[] { new Vector3(-143f, 8f, 213f), new Vector3(-86f, 29f, 247f), new Vector3(-15f, 43f, 230f),
                new Vector3(45f, 26f, 259f), new Vector3(111f, 10f, 237f) }
        };
        private static readonly float[] InwardWidths = { 36f, 42f, 38f, 38f, 42f };
        private static readonly float[] LeeWidths = { 78f, 90f, 86f, 90f, 82f };

        public static GameObject Build(Transform parent, Material meadow, Material rock)
        {
            EnsureFolder(Folder);
            var root = new GameObject("SunbreakDistantVista");
            root.transform.SetParent(parent, false);
            root.layer = 2;
            var floor = new Draft();
            floor.Quad(new Vector3(-64f, UnderlayHeight, -64f), new Vector3(-64f, UnderlayHeight, 128f),
                new Vector3(128f, UnderlayHeight, 128f), new Vector3(128f, UnderlayHeight, -64f), true);
            Publish(root.transform, "DistantGroundUnderlay", floor, meadow);

            // The innermost ring is wholly beyond Core's playable rectangle.
            // Square radial coordinates only tessellate the outside-Core domain.
            // Elevations come from independent world-space massif ridgelines,
            // not radial bands or the set of currently resident near chunks.
            const int segments = 160;
            float[] distance = { 0f, 4f, 8f, 12f, 16f, 20f, 25f, 30f, 35f, 40f,
                46f, 52f, 58f, 64f, 70f, 76f, 84f, 92f, 100f, 110f, 120f,
                130f, 140f, 150f, 160f, 172f, 184f, 196f, 208f, 220f, 232f, 240f, 245f };
            var terrain = new Draft();
            for (int segment = 0; segment < segments; segment++)
            {
                float a = segment * Mathf.PI * 2f / segments;
                float b = ((segment + 1) % segments) * Mathf.PI * 2f / segments;
                for (int ring = 0; ring < distance.Length - 1; ring++)
                {
                    // At most 12 m between radial samples resolves the crest
                    // shoulders without restoring the earlier large wedges.
                    terrain.Quad(Point(a, distance[ring]), Point(b, distance[ring]),
                        Point(b, distance[ring + 1]), Point(a, distance[ring + 1]), false);
                }
            }
            Publish(root.transform, "ErodedMountainHorizon", terrain, rock, true);
            SunbreakVistaWoodlandAuthoring.Build(root.transform,
                root.transform.Find("ErodedMountainHorizon").GetComponent<MeshFilter>().sharedMesh);
            Validate(root);
            root.SetActive(false);
            return root;
        }

        public static Material BuildSky() => SunbreakDaylightSkyAssets.ConfigureSky();

        public static void Validate(GameObject root)
        {
            if (root == null || root.transform.Find("DistantGroundUnderlay") == null ||
                root.transform.Find("ErodedMountainHorizon") == null ||
                root.transform.Find(SunbreakVistaWoodlandAuthoring.BranchesName) == null ||
                root.transform.Find(SunbreakVistaWoodlandAuthoring.CanopiesName) == null)
            { throw new InvalidOperationException("Sunbreak must retain its bounded independent horizon."); }
            int triangles = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) { throw new InvalidOperationException("Vista mesh missing."); }
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                { triangles += (int)mesh.GetIndexCount(submesh) / 3; }
                foreach (Vector3 local in mesh.vertices)
                {
                    Vector3 point = filter.transform.TransformPoint(local);
                    if (point.y > 0.18f && point.x >= PveRegionLayout.MinimumX &&
                        point.x <= PveRegionLayout.MaximumX && point.z >= PveRegionLayout.MinimumZ &&
                        point.z <= PveRegionLayout.MaximumZ)
                    { throw new InvalidOperationException("Distant scenery intrudes into the playable rectangle."); }
                }
            }
            if (triangles > MaximumTriangles || root.GetComponentsInChildren<MeshRenderer>(true).Length != MaximumRenderers ||
                root.GetComponentsInChildren<Collider>(true).Length != 0 ||
                root.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                root.GetComponentsInChildren<Light>(true).Length != 0 ||
                root.GetComponentsInChildren<Camera>(true).Length != 0)
            { throw new InvalidOperationException("Vista must remain four bounded visual-only terrain/woodland batches."); }
            SunbreakVistaWoodlandAuthoring.Validate(root);
        }

        private static Vector3 Point(float angle, float distance)
        {
            var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float radius = 92f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
            Vector2 position = new Vector2(32f, 32f) + direction * (radius + distance);
            float elevation = 2.3f + 0.65f * Mathf.Sin(position.x * 0.031f + position.y * 0.013f) +
                0.45f * Mathf.Sin(position.y * 0.043f - position.x * 0.017f);
            for (int massif = 0; massif < Massifs.Length; massif++)
            {
                float rock = MassifHeight(position, massif);
                float seam = Mathf.Max(2f - Mathf.Abs(elevation - rock), 0f);
                elevation = Mathf.Max(elevation, rock) + seam * seam / 8f;
            }
            // Only this low foot follows the playable boundary. It connects
            // existing bank surfaces without recreating the rejected tall ring.
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(distance / 24f));
            return new Vector3(position.x, -0.08f + edge * elevation, position.y);
        }

        private static float MassifHeight(Vector2 point, int massif)
        {
            Vector3[] stations = Massifs[massif];
            float result = 0f, along = 0f;
            for (int station = 0; station < stations.Length - 1; station++)
            {
                Vector3 first = stations[station], next = stations[station + 1];
                Vector2 start = new Vector2(first.x, first.z);
                Vector2 direction = new Vector2(next.x - first.x, next.z - first.z);
                float length = direction.magnitude;
                float progress = Mathf.Clamp01(Vector2.Dot(point - start, direction) / direction.sqrMagnitude);
                Vector2 offset = point - start - direction * progress;
                float side = direction.x * offset.y - direction.y * offset.x;
                float width = side < 0f ? InwardWidths[massif] : LeeWidths[massif];
                float profile = Mathf.Clamp01(1f - offset.magnitude / width);
                float crest = Mathf.Lerp(first.y, next.y, progress);
                // Gullies follow the local ridge instead of concentric rings.
                // Their broad, coherent phase is resolved by the fixed grid.
                float gully = Mathf.Pow(0.5f + 0.5f * Mathf.Sin((along + progress * length) * 0.075f +
                    Mathf.Sign(side) * offset.magnitude * 0.02f + massif * 1.17f), 3f);
                float cut = 3.5f * gully * Mathf.Sin(Mathf.PI * profile);
                result = Mathf.Max(result, crest * profile * profile * profile - cut);
                along += length;
            }
            return Mathf.Max(0f, result);
        }

        private static void Publish(Transform parent, string name, Draft draft, Material material,
            bool projectRockFaces = false)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(draft.Positions);
            mesh.SetUVs(0, draft.Uv);
            mesh.SetTriangles(draft.Indices, 0);
            mesh.RecalculateNormals();
            if (projectRockFaces) { ApplyRockSurfaceProjection(mesh); }
            if (projectRockFaces)
            {
                Vector3[] positions = mesh.vertices;
                Vector3[] normals = mesh.normals;
                var colors = new Color[positions.Length];
                for (int i = 0; i < positions.Length; i++)
                { colors[i] = SunbreakLandscapeSurfaceAuthoring.VertexColor(positions[i], normals[i]); }
                mesh.colors = colors;
            }
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            string path = Folder + "/" + name + ".asset";
            Mesh old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (old == null) { AssetDatabase.CreateAsset(mesh, path); }
            else
            {
                EditorUtility.CopySerialized(mesh, old);
                UnityEngine.Object.DestroyImmediate(mesh);
                mesh = old;
                EditorUtility.SetDirty(old);
            }
            var child = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer)) { layer = 2 };
            child.transform.SetParent(parent, false);
            child.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = child.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(child, StaticEditorFlags.BatchingStatic);
        }

        private static void ApplyRockSurfaceProjection(Mesh mesh)
        {
            Vector3[] source = mesh.vertices;
            Vector3[] normals = mesh.normals;
            int[] indices = mesh.triangles;
            var positions = new List<Vector3>(source.Length);
            var smoothNormals = new List<Vector3>(source.Length);
            var uv = new List<Vector2>(source.Length);
            var remapped = new int[indices.Length];
            var vertices = new Dictionary<int, int>();
            for (int triangle = 0; triangle < indices.Length; triangle += 3)
            {
                Vector3 a = source[indices[triangle]], b = source[indices[triangle + 1]], c = source[indices[triangle + 2]];
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int axis = DominantAxis(normal);
                for (int corner = 0; corner < 3; corner++)
                {
                    int original = indices[triangle + corner];
                    int key = original * 3 + axis;
                    if (!vertices.TryGetValue(key, out int vertex))
                    {
                        vertex = positions.Count;
                        vertices.Add(key, vertex);
                        Vector3 point = source[original];
                        positions.Add(point);
                        // A UV seam is not a hard lighting seam: retain the
                        // original welded surface normal before splitting it.
                        smoothNormals.Add(normals[original]);
                        uv.Add((axis == 0 ? new Vector2(point.x, point.z) :
                            axis == 1 ? new Vector2(point.z, point.y) : new Vector2(point.x, point.y)) * 0.15f);
                    }
                    remapped[triangle + corner] = vertex;
                }
            }
            mesh.Clear();
            mesh.SetVertices(positions);
            mesh.SetNormals(smoothNormals);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(remapped, 0);
        }

        private static int DominantAxis(Vector3 normal)
        {
            float x = Mathf.Abs(normal.x), y = Mathf.Abs(normal.y), z = Mathf.Abs(normal.z);
            return y >= x && y >= z ? 0 : x >= z ? 1 : 2;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }

        private sealed class Draft
        {
            public readonly List<Vector3> Positions = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Indices = new List<int>();
            private readonly Dictionary<Vector3Int, int> _vertices = new Dictionary<Vector3Int, int>();
            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool worldUv)
            {
                if (Vector3.Cross(b - a, c - a).y < 0f) { (a, d) = (d, a); (b, c) = (c, b); }
                var corners = new int[4];
                int corner = 0;
                foreach (Vector3 point in new[] { a, b, c, d })
                {
                    var key = new Vector3Int(Mathf.RoundToInt(point.x * 1000f),
                        Mathf.RoundToInt(point.y * 1000f), Mathf.RoundToInt(point.z * 1000f));
                    if (!_vertices.TryGetValue(key, out int vertex))
                    {
                        vertex = Positions.Count;
                        _vertices.Add(key, vertex);
                        Positions.Add(point);
                        Uv.Add(worldUv ? new Vector2((point.x + 64f) / 192f, (point.z + 64f) / 192f) :
                            new Vector2(point.x, point.z) * 0.15f);
                    }
                    corners[corner++] = vertex;
                }
                Indices.Add(corners[0]); Indices.Add(corners[1]); Indices.Add(corners[2]);
                Indices.Add(corners[0]); Indices.Add(corners[2]); Indices.Add(corners[3]);
            }
        }
    }
}
