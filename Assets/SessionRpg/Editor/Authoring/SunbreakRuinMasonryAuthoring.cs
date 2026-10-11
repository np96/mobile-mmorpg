using System;
using System.Collections.Generic;
using System.IO;
using SessionRpg.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>
    /// Explicitly modified, source-derived ruin. Original CC0 fort assets remain
    /// immutable. Actual imported faces are clipped, exposed cuts are capped,
    /// and bounded offcut sections become grounded rubble. Editor only.
    /// </summary>
    public static class SunbreakRuinMasonryAuthoring
    {
        public const string RootName = "BrokenAbbeyMasonry";
        public const string Folder = "Assets/SessionRpg/Art/Environment/SunbreakRuinMasonryV1";
        public const string MeshPath = Folder + "/BrokenAbbeyMasonry.asset";
        public const int MaximumTriangles = 1276;
        public const int MaximumRenderers = 1;
        public const float JointZ = 26.02f;
        private const float Epsilon = 0.00015f;
        private static readonly float[] ProfileZ =
        {
            21.8f, 22.5f, 22.85f, 23.3f, 23.7f, 24.1f, 24.5f, 24.95f, 25.25f,
            25.6f, 26.02f, 26.38f, 26.72f, 27f, 27.38f, 27.72f, 28.1f, 28.5f
        };
        private static readonly float[] ProfileY =
        {
            1.32f, 1.52f, 2.5f, 2.56f, 2.47f, 1.38f, 1.32f, 1.44f, 2.96f,
            3.36f, 3.36f, 3.36f, 3.08f, 3.08f, 2.35f, 2.42f, 2.3f, 2.34f
        };

        public sealed class Geometry : IDisposable
        {
            public readonly Mesh Wall;
            public readonly Mesh Corner;
            public readonly Mesh Rubble;
            public readonly int CapLoops;
            public readonly int CapTriangles;
            public int TriangleCount => (Wall.triangles.Length + Corner.triangles.Length + Rubble.triangles.Length) / 3;
            internal Geometry(Mesh wall, Mesh corner, Mesh rubble, int loops, int capTriangles)
            { Wall = wall; Corner = corner; Rubble = rubble; CapLoops = loops; CapTriangles = capTriangles; }
            public void Dispose() { Object.DestroyImmediate(Wall); Object.DestroyImmediate(Corner); Object.DestroyImmediate(Rubble); }
        }

        /// <summary>Rebuild-only publication. The supplied sampler grounds debris on the existing Core-cover bank.</summary>
        public static GameObject Build(Transform parent, Func<Vector3, float> groundHeight)
        {
            if (parent == null) { throw new ArgumentNullException(nameof(parent)); }
            if (groundHeight == null) { throw new ArgumentNullException(nameof(groundHeight)); }
            if (parent.Find(RootName) != null) { throw new InvalidOperationException("Build the foreground ruin once."); }
            using (Geometry geometry = CreateGeometry(groundHeight))
            {
                var combined = new Mesh { name = RootName, indexFormat = IndexFormat.UInt16 };
                var parts = new[] { geometry.Wall, geometry.Corner, geometry.Rubble };
                var instances = new CombineInstance[parts.Length];
                for (int i = 0; i < parts.Length; i++)
                { instances[i] = new CombineInstance { mesh = parts[i], transform = parent.worldToLocalMatrix }; }
                combined.CombineMeshes(instances, true, true, false);
                combined.RecalculateBounds();
                EnsureFolder(Folder);
                Mesh asset = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (asset == null) { AssetDatabase.CreateAsset(combined, MeshPath); asset = combined; }
                else
                {
                    EditorUtility.CopySerialized(combined, asset);
                    Object.DestroyImmediate(combined);
                    EditorUtility.SetDirty(asset);
                    AssetDatabase.SaveAssetIfDirty(asset);
                }
                var root = new GameObject(RootName) { layer = 2 };
                root.transform.SetParent(parent, false);
                root.AddComponent<MeshFilter>().sharedMesh = asset;
                MeshRenderer renderer = root.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(SunbreakFortAssets.WallMaterialPath);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
                Validate(root);
                return root;
            }
        }

        /// <summary>Pure transient geometry from actual Unity-imported meshes; never publishes or edits source assets.</summary>
        public static Geometry CreateGeometry(Func<Vector3, float> groundHeight = null)
        {
            List<Face> wall = SourceFaces(SunbreakFortAssets.Module.BattlementWall, new Vector3(-43.2f, 0.05f, 24.2f), 0f);
            List<Face> corner = SourceFaces(SunbreakFortAssets.Module.ThinCorner, new Vector3(-42.675f, 0.05f, 27.1f), 90f);
            var wallDraft = new Draft();
            var cornerDraft = new Draft();
            var rubbleDraft = new Draft();
            BuildFracturedBody(wall, 21.8f, JointZ, wallDraft);
            BuildFracturedBody(corner, JointZ, 28.5f, cornerDraft);
            AddRubble(wall, new Bounds(new Vector3(-43.2f, 2.65f, 22.8f), new Vector3(3f, 0.62f, 0.54f)),
                new Vector3(-43.15f, 0f, 21.15f), Quaternion.Euler(28f, 24f, 13f), groundHeight, rubbleDraft);
            AddRubble(wall, new Bounds(new Vector3(-43.2f, 3.04f, 23.67f), new Vector3(3f, 0.58f, 0.52f)),
                new Vector3(-44.8f, 0f, 22.7f), Quaternion.Euler(61f, -37f, 19f), groundHeight, rubbleDraft);
            AddRubble(wall, new Bounds(new Vector3(-43.2f, 3.4f, 24.54f), new Vector3(3f, 0.42f, 0.48f)),
                new Vector3(-45.75f, 0f, 24.1f), Quaternion.Euler(19f, 78f, -32f), groundHeight, rubbleDraft);
            Mesh wallMesh = wallDraft.ToMesh("FracturedAbbeyWall");
            Mesh cornerMesh = cornerDraft.ToMesh("FracturedAbbeyCorner");
            Mesh rubbleMesh = rubbleDraft.ToMesh("SourceMasonryOffcuts");
            var result = new Geometry(wallMesh, cornerMesh, rubbleMesh,
                wallDraft.CapLoops + cornerDraft.CapLoops + rubbleDraft.CapLoops,
                wallDraft.CapTriangles + cornerDraft.CapTriangles + rubbleDraft.CapTriangles);
            if (result.TriangleCount > MaximumTriangles)
            { result.Dispose(); throw new InvalidDataException("Ruined masonry exceeds its replacement triangle budget."); }
            ValidateWorldMesh(wallMesh);
            ValidateWorldMesh(cornerMesh);
            ValidateWorldMesh(rubbleMesh);
            return result;
        }

        public static float FractureHeight(float worldZ)
        {
            for (int i = 0; i < ProfileZ.Length - 1; i++)
            {
                if (worldZ > ProfileZ[i + 1]) { continue; }
                return Mathf.Lerp(ProfileY[i], ProfileY[i + 1], Mathf.InverseLerp(ProfileZ[i], ProfileZ[i + 1], worldZ));
            }
            return ProfileY[ProfileY.Length - 1];
        }

        public static void Validate(GameObject root)
        {
            if (root == null || root.name != RootName || root.layer != 2 || root.transform.childCount != 0)
            { throw new InvalidDataException("Unexpected ruined-masonry presentation hierarchy."); }
            MeshFilter filter = root.GetComponent<MeshFilter>();
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            if (filter == null || renderer == null || root.GetComponents<Component>().Length != 3 ||
                AssetDatabase.GetAssetPath(renderer.sharedMaterial) != SunbreakFortAssets.WallMaterialPath)
            { throw new InvalidDataException("Ruined masonry must use one measured opaque material and no runtime components."); }
            Mesh mesh = filter.sharedMesh;
            if (mesh == null || mesh.subMeshCount != 1 || mesh.GetIndexCount(0) / 3 > MaximumTriangles)
            { throw new InvalidDataException("Ruined masonry mesh or budget is invalid."); }
            foreach (Vector3 vertex in mesh.vertices) { RequireInsideCover(root.transform.TransformPoint(vertex)); }
        }

        private static List<Face> SourceFaces(SunbreakFortAssets.Module module, Vector3 ground, float yaw)
        {
            var owner = new GameObject("TransientRuinSourceInspection");
            try
            {
                Transform placed = SunbreakFortAssets.Create(owner.transform, "Source", module, ground, yaw, 0.52f);
                Mesh mesh = placed.GetComponent<MeshFilter>().sharedMesh;
                Vector3[] points = mesh.vertices, normals = mesh.normals;
                Vector4[] tangents = mesh.tangents;
                Vector2[] uv = mesh.uv;
                var vertices = new Vertex[points.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 tangent = placed.TransformDirection(new Vector3(tangents[i].x, tangents[i].y, tangents[i].z)).normalized;
                    vertices[i] = new Vertex(placed.TransformPoint(points[i]), placed.TransformDirection(normals[i]).normalized,
                        new Vector4(tangent.x, tangent.y, tangent.z, tangents[i].w), uv[i]);
                }
                // The fracture is below the entire intact parapet trim. Reusing
                // only the source wall role avoids hidden intact crenellations.
                int[] indices = mesh.GetIndices(SunbreakFortAssets.WallSubmesh(module));
                var faces = new List<Face>(indices.Length / 3);
                for (int i = 0; i < indices.Length; i += 3)
                { faces.Add(new Face(vertices[indices[i]], vertices[indices[i + 1]], vertices[indices[i + 2]])); }
                return faces;
            }
            finally { Object.DestroyImmediate(owner); }
        }

        private static void BuildFracturedBody(List<Face> source, float minimumZ, float maximumZ, Draft output)
        {
            for (int i = 0; i < ProfileZ.Length - 1; i++)
            {
                float low = Mathf.Max(minimumZ, ProfileZ[i]), high = Mathf.Min(maximumZ, ProfileZ[i + 1]);
                if (high - low < Epsilon) { continue; }
                float slope = (ProfileY[i + 1] - ProfileY[i]) / (ProfileZ[i + 1] - ProfileZ[i]);
                var roof = new Cut(new Vector3(0f, 1f, -slope), ProfileY[i] - slope * ProfileZ[i]);
                var lowCut = new Cut(Vector3.back, -low);
                var highCut = new Cut(Vector3.forward, high);
                var roofEdges = new List<Segment>();
                foreach (Face face in source)
                {
                    List<Vertex> polygon = Clip(new List<Vertex> { face.A, face.B, face.C }, lowCut);
                    polygon = Clip(polygon, highCut);
                    polygon = Clip(polygon, roof);
                    output.Polygon(polygon);
                    CollectEdges(polygon, roof, roofEdges);
                }
                CapRoof(roofEdges, roof.Normal.normalized, low, high, output);
            }
        }

        private static void CapRoof(List<Segment> edges, Vector3 normal, float lowZ, float highZ, Draft output)
        {
            var graph = new CutGraph();
            foreach (Segment edge in edges) { graph.Add(edge.A, edge.B); }
            graph.CloseSlabBoundary(lowZ);
            graph.CloseSlabBoundary(highZ);
            foreach (List<Vector3> loop in graph.Loops()) { output.Cap(loop, normal); }
        }

        private static void AddRubble(List<Face> source, Bounds box, Vector3 destination, Quaternion rotation,
            Func<Vector3, float> groundHeight, Draft output)
        {
            var cuts = new[]
            {
                new Cut(Vector3.right, box.max.x), new Cut(Vector3.left, -box.min.x),
                new Cut(Vector3.up, box.max.y), new Cut(Vector3.down, -box.min.y),
                new Cut(Vector3.forward, box.max.z), new Cut(Vector3.back, -box.min.z)
            };
            var piece = new Draft();
            var polygons = new List<List<Vertex>>();
            foreach (Face face in source)
            {
                var polygon = new List<Vertex> { face.A, face.B, face.C };
                foreach (Cut cut in cuts) { polygon = Clip(polygon, cut); }
                if (polygon.Count >= 3) { polygons.Add(polygon); piece.Polygon(polygon); }
            }
            foreach (Cut cut in cuts)
            {
                var points = new List<Vector3>();
                foreach (List<Vertex> polygon in polygons)
                { foreach (Vertex vertex in polygon) { if (Mathf.Abs(cut.Distance(vertex.Position)) <= Epsilon) { points.Add(vertex.Position); } } }
                // A source straight-wall offcut intersected by an axis-aligned
                // box is convex; unlike the L corner, a hull is valid here.
                List<Vector3> loop = PlanarHull(points, cut.Normal);
                if (loop.Count >= 3) { piece.Cap(loop, cut.Normal); }
            }
            if (piece.Vertices.Count == 0) { throw new InvalidDataException("Masonry offcut does not intersect actual source geometry."); }
            var bounds = new Bounds(piece.Vertices[0].Position, Vector3.zero);
            foreach (Vertex vertex in piece.Vertices) { bounds.Encapsulate(vertex.Position); }
            float minimumY = float.PositiveInfinity;
            foreach (Vertex vertex in piece.Vertices)
            { minimumY = Mathf.Min(minimumY, (rotation * (vertex.Position - bounds.center)).y); }
            float ground = groundHeight == null ? 0.08f : groundHeight(destination);
            Vector3 translation = destination + Vector3.up * (ground - minimumY - 0.055f);
            foreach (Vertex vertex in piece.Vertices)
            {
                Vector3 tangent = rotation * new Vector3(vertex.Tangent.x, vertex.Tangent.y, vertex.Tangent.z);
                output.Vertices.Add(new Vertex(rotation * (vertex.Position - bounds.center) + translation,
                    rotation * vertex.Normal, new Vector4(tangent.x, tangent.y, tangent.z, vertex.Tangent.w), vertex.Uv));
            }
            output.CapLoops += piece.CapLoops;
            output.CapTriangles += piece.CapTriangles;
        }

        private static List<Vertex> Clip(List<Vertex> input, Cut cut)
        {
            var result = new List<Vertex>(input.Count + 1);
            if (input.Count == 0) { return result; }
            Vertex previous = input[input.Count - 1];
            float previousDistance = cut.Distance(previous.Position);
            foreach (Vertex current in input)
            {
                float distance = cut.Distance(current.Position);
                bool inside = distance <= 0.000001f, previousInside = previousDistance <= 0.000001f;
                if (inside != previousInside)
                {
                    float fraction = previousDistance / (previousDistance - distance);
                    result.Add(Vertex.Interpolate(previous, current, fraction));
                }
                if (inside) { result.Add(current); }
                previous = current;
                previousDistance = distance;
            }
            return result;
        }

        private static void CollectEdges(List<Vertex> polygon, Cut cut, List<Segment> result)
        {
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector3 a = polygon[i].Position, b = polygon[(i + 1) % polygon.Count].Position;
                if (Mathf.Abs(cut.Distance(a)) <= Epsilon && Mathf.Abs(cut.Distance(b)) <= Epsilon && (a - b).sqrMagnitude > 0.00000001f)
                { result.Add(new Segment(a, b)); }
            }
        }

        private readonly struct Cut
        {
            public readonly Vector3 Normal;
            private readonly float distance;
            public Cut(Vector3 normal, float distance) { Normal = normal; this.distance = distance; }
            public float Distance(Vector3 point) => Vector3.Dot(Normal, point) - distance;
        }

        private readonly struct Vertex
        {
            public readonly Vector3 Position;
            public readonly Vector3 Normal;
            public readonly Vector4 Tangent;
            public readonly Vector2 Uv;
            public Vertex(Vector3 position, Vector3 normal, Vector4 tangent, Vector2 uv)
            { Position = position; Normal = normal; Tangent = tangent; Uv = uv; }
            public static Vertex Interpolate(Vertex a, Vertex b, float t)
            {
                Vector3 normal = Vector3.LerpUnclamped(a.Normal, b.Normal, t).normalized;
                Vector4 raw = Vector4.LerpUnclamped(a.Tangent, b.Tangent, t);
                Vector3 tangent = new Vector3(raw.x, raw.y, raw.z);
                tangent = (tangent - normal * Vector3.Dot(tangent, normal)).normalized;
                return new Vertex(Vector3.LerpUnclamped(a.Position, b.Position, t), normal,
                    new Vector4(tangent.x, tangent.y, tangent.z, a.Tangent.w), Vector2.LerpUnclamped(a.Uv, b.Uv, t));
            }
        }
        private readonly struct Face
        {
            public readonly Vertex A, B, C;
            public Face(Vertex a, Vertex b, Vertex c) { A = a; B = b; C = c; }
        }
        private readonly struct Segment
        {
            public readonly Vector3 A, B;
            public Segment(Vector3 a, Vector3 b) { A = a; B = b; }
        }

        private sealed class CutGraph
        {
            private readonly List<Vector3> points = new List<Vector3>();
            private readonly List<List<int>> neighbors = new List<List<int>>();
            public void Add(Vector3 a, Vector3 b)
            {
                int first = Point(a), second = Point(b);
                if (first == second || neighbors[first].Contains(second)) { return; }
                neighbors[first].Add(second);
                neighbors[second].Add(first);
            }
            private int Point(Vector3 point)
            {
                for (int i = 0; i < points.Count; i++)
                { if ((points[i] - point).sqrMagnitude <= Epsilon * Epsilon) { return i; } }
                points.Add(point); neighbors.Add(new List<int>()); return points.Count - 1;
            }
            public void CloseSlabBoundary(float z)
            {
                var ends = new List<int>();
                for (int i = 0; i < points.Count; i++)
                { if (neighbors[i].Count == 1 && Mathf.Abs(points[i].z - z) < Epsilon * 2f) { ends.Add(i); } }
                ends.Sort((a, b) => points[a].x.CompareTo(points[b].x));
                if (ends.Count % 2 != 0) { throw new InvalidDataException("Uneven masonry cut boundary; no cap may bridge an unknown opening."); }
                for (int i = 0; i < ends.Count; i += 2) { Add(points[ends[i]], points[ends[i + 1]]); }
            }
            public List<List<Vector3>> Loops()
            {
                var result = new List<List<Vector3>>();
                var visited = new bool[points.Count];
                for (int start = 0; start < points.Count; start++)
                {
                    if (visited[start]) { continue; }
                    var loop = new List<Vector3>();
                    int previous = -1, current = start;
                    do
                    {
                        if (neighbors[current].Count != 2 || visited[current])
                        { throw new InvalidDataException("Masonry fracture cap has an open or branching boundary."); }
                        visited[current] = true;
                        loop.Add(points[current]);
                        int next = neighbors[current][0] == previous ? neighbors[current][1] : neighbors[current][0];
                        previous = current; current = next;
                    } while (current != start);
                    result.Add(loop);
                }
                return result;
            }
        }

        private sealed class Draft
        {
            public readonly List<Vertex> Vertices = new List<Vertex>();
            public int CapLoops;
            public int CapTriangles;
            public void Triangle(Vertex a, Vertex b, Vertex c)
            {
                if (Vector3.Cross(b.Position - a.Position, c.Position - a.Position).sqrMagnitude <= 0.00000000001f) { return; }
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
            }
            public void Polygon(List<Vertex> polygon)
            { for (int i = 1; i < polygon.Count - 1; i++) { Triangle(polygon[0], polygon[i], polygon[i + 1]); } }
            public void Cap(List<Vector3> input, Vector3 normal)
            {
                normal.Normalize();
                List<Vector3> points = Simplify(input, normal);
                if (points.Count < 3) { return; }
                Basis(normal, out Vector3 u, out Vector3 v);
                var projected = new List<Vector2>(points.Count);
                foreach (Vector3 point in points) { projected.Add(new Vector2(Vector3.Dot(point, u), Vector3.Dot(point, v))); }
                float area = 0f;
                for (int i = 0; i < projected.Count; i++) { Vector2 a = projected[i], b = projected[(i + 1) % projected.Count]; area += a.x * b.y - a.y * b.x; }
                if (area < 0f) { points.Reverse(); projected.Reverse(); }
                var remaining = new List<int>();
                for (int i = 0; i < points.Count; i++) { remaining.Add(i); }
                int before = Vertices.Count;
                while (remaining.Count > 2)
                {
                    bool found = false;
                    for (int i = 0; i < remaining.Count; i++)
                    {
                        int a = remaining[(i + remaining.Count - 1) % remaining.Count], b = remaining[i], c = remaining[(i + 1) % remaining.Count];
                        if (Cross(projected[a], projected[b], projected[c]) <= 0.0000001f) { continue; }
                        bool occupied = false;
                        foreach (int test in remaining)
                        {
                            if (test == a || test == b || test == c) { continue; }
                            if (InsideTriangle(projected[test], projected[a], projected[b], projected[c])) { occupied = true; break; }
                        }
                        if (occupied) { continue; }
                        Triangle(CapVertex(points[a], normal, u, v), CapVertex(points[b], normal, u, v), CapVertex(points[c], normal, u, v));
                        remaining.RemoveAt(i); found = true; break;
                    }
                    if (!found) { throw new InvalidDataException("Masonry cut cannot be triangulated without crossing its boundary."); }
                }
                CapLoops++;
                CapTriangles += (Vertices.Count - before) / 3;
            }
            public Mesh ToMesh(string name)
            {
                var points = new Vector3[Vertices.Count]; var normals = new Vector3[Vertices.Count];
                var tangents = new Vector4[Vertices.Count]; var uv = new Vector2[Vertices.Count]; var indices = new int[Vertices.Count];
                for (int i = 0; i < Vertices.Count; i++)
                { Vertex vertex = Vertices[i]; points[i] = vertex.Position; normals[i] = vertex.Normal; tangents[i] = vertex.Tangent; uv[i] = vertex.Uv; indices[i] = i; }
                var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt16 };
                mesh.vertices = points; mesh.normals = normals; mesh.tangents = tangents; mesh.uv = uv; mesh.triangles = indices; mesh.RecalculateBounds();
                return mesh;
            }
        }

        private static Vertex CapVertex(Vector3 point, Vector3 normal, Vector3 u, Vector3 v) =>
            new Vertex(point, normal, new Vector4(u.x, u.y, u.z, 1f), new Vector2(Vector3.Dot(point, u), Vector3.Dot(point, v)) * 0.32f);
        private static void Basis(Vector3 normal, out Vector3 u, out Vector3 v)
        {
            u = Vector3.Cross(Mathf.Abs(normal.y) < 0.9f ? Vector3.up : Vector3.forward, normal).normalized;
            v = Vector3.Cross(normal, u).normalized;
        }
        private static List<Vector3> Simplify(List<Vector3> source, Vector3 normal)
        {
            var points = new List<Vector3>(source);
            bool changed = true;
            while (changed && points.Count > 3)
            {
                changed = false;
                for (int i = 0; i < points.Count; i++)
                {
                    Vector3 a = points[(i + points.Count - 1) % points.Count], b = points[i], c = points[(i + 1) % points.Count];
                    if ((b - a).sqrMagnitude < Epsilon * Epsilon || Mathf.Abs(Vector3.Dot(Vector3.Cross(b - a, c - b), normal)) < 0.000001f)
                    { points.RemoveAt(i); changed = true; break; }
                }
            }
            return points;
        }
        private static List<Vector3> PlanarHull(List<Vector3> source, Vector3 normal)
        {
            Basis(normal, out Vector3 u, out Vector3 v);
            source.Sort((a, b) => Vector3.Dot(a, u) != Vector3.Dot(b, u) ? Vector3.Dot(a, u).CompareTo(Vector3.Dot(b, u)) : Vector3.Dot(a, v).CompareTo(Vector3.Dot(b, v)));
            var unique = new List<Vector3>();
            foreach (Vector3 point in source) { if (unique.Count == 0 || (point - unique[unique.Count - 1]).sqrMagnitude > Epsilon * Epsilon) { unique.Add(point); } }
            if (unique.Count < 3) { return unique; }
            var hull = new List<Vector3>();
            foreach (Vector3 point in unique)
            {
                while (hull.Count >= 2 && Vector3.Dot(Vector3.Cross(hull[hull.Count - 1] - hull[hull.Count - 2], point - hull[hull.Count - 1]), normal) <= 0.0000001f) { hull.RemoveAt(hull.Count - 1); }
                hull.Add(point);
            }
            int lower = hull.Count;
            for (int i = unique.Count - 2; i >= 0; i--)
            {
                Vector3 point = unique[i];
                while (hull.Count > lower && Vector3.Dot(Vector3.Cross(hull[hull.Count - 1] - hull[hull.Count - 2], point - hull[hull.Count - 1]), normal) <= 0.0000001f) { hull.RemoveAt(hull.Count - 1); }
                hull.Add(point);
            }
            hull.RemoveAt(hull.Count - 1);
            return hull;
        }
        private static float Cross(Vector2 a, Vector2 b, Vector2 c) => (b.x - a.x) * (c.y - a.y) - (b.y - a.y) * (c.x - a.x);
        private static bool InsideTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c) =>
            Cross(a, b, point) >= -0.0000001f && Cross(b, c, point) >= -0.0000001f && Cross(c, a, point) >= -0.0000001f;

        private static void ValidateWorldMesh(Mesh mesh)
        {
            Vector3[] points = mesh.vertices; int[] indices = mesh.triangles;
            foreach (Vector3 point in points) { RequireInsideCover(point); }
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = points[indices[i]], b = points[indices[i + 1]], c = points[indices[i + 2]];
                RequireInsideCover((a + b + c) / 3f); RequireInsideCover((a + b) * 0.5f);
                RequireInsideCover((b + c) * 0.5f); RequireInsideCover((c + a) * 0.5f);
            }
        }
        private static void RequireInsideCover(Vector3 point)
        {
            if (!Finite(point.x) || !Finite(point.y) || !Finite(point.z)) { throw new InvalidDataException("Non-finite masonry geometry."); }
            for (int i = PveRegionLayout.GetLandmarkObstacleStart(7); i < PveRegionLayout.GetLandmarkObstacleStart(7) + PveRegionLayout.GetLandmarkObstacleCount(7); i++)
            {
                ArenaCircleObstacle circle = PveRegionLayout.GetObstacle(i);
                float x = point.x - circle.Center.X, z = point.z - circle.Center.Z;
                if (x * x + z * z <= circle.Radius * circle.Radius + 0.0001f) { return; }
            }
            throw new InvalidDataException("Source-derived rubble or masonry escaped the exact Core cover union: " + point);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/'); string current = parts[0];
            for (int i = 1; i < parts.Length; i++) { string next = current + "/" + parts[i]; if (!AssetDatabase.IsValidFolder(next)) { AssetDatabase.CreateFolder(current, parts[i]); } current = next; }
        }
    }
}
