using System;
using System.Collections.Generic;
using SessionRpg.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>
    /// First-party, shallow remains of one collapsed court at Frostbound Watch.
    /// Oblique fracture planes partition an authored broad footprint; absent
    /// pieces expose the existing earth. This is neither a paving grid nor a
    /// path, and it never supplies collision or changes the flat simulation floor.
    /// </summary>
    public static class SunbreakRuinGroundAuthoring
    {
        public const string RootName = "FracturedRuinCourt";
        public const string Folder = "Assets/SessionRpg/Art/Environment/SunbreakRuinGround";
        public const string MeshPath = Folder + "/FrostboundFracturedCourt.asset";
        public const int MaximumTriangles = 3000;
        public const int MaximumRenderers = 1;
        public const float MinimumHeight = 0.005f;
        // CharacterAuthoring places the flat TeamRing at 0.025 m and the
        // TargetRing at 0.04 m. Keep every opaque fragment below both rings.
        public const float MaximumHeight = 0.012f;
        public const float TextureScale = 0.4f;
        public static readonly Vector3 ChunkOrigin = new Vector3(-64f, 0f, 0f);
        private const uint Seed = 0x9AF24031u;

        /// <summary>
        /// Rebuild-only publication into chunk (-1, 0)'s unrotated visual tree.
        /// The single mesh stores chunk-local positions and world-space UVs.
        /// </summary>
        public static GameObject Build(Transform parent, Material material)
        {
            if (parent == null) { throw new ArgumentNullException(nameof(parent)); }
            if (material == null) { throw new ArgumentNullException(nameof(material)); }
            RequireFocalTransform(parent);
            if (parent.Find(RootName) != null)
            {
                throw new InvalidOperationException("The Frostbound court must be built once per focal chunk.");
            }
            Mesh authored = CreateMesh();
            EnsureFolder(Folder);
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(authored, MeshPath);
                existing = authored;
            }
            else
            {
                EditorUtility.CopySerialized(authored, existing);
                Object.DestroyImmediate(authored);
                EditorUtility.SetDirty(existing);
            }

            var root = new GameObject(RootName) { layer = 2 };
            root.transform.SetParent(parent, false);
            root.AddComponent<MeshFilter>().sharedMesh = existing;
            MeshRenderer renderer = root.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(root, StaticEditorFlags.BatchingStatic);
            Validate(root);
            return root;
        }

        /// <summary>Pure editor geometry; does not publish or alter any asset.</summary>
        public static Mesh CreateMesh()
        {
            List<Vector2[]> footprints = CreateFootprints();
            uint random = Seed ^ 0x481B2Fu;
            var draft = new Draft();
            for (int index = 0; index < footprints.Count; index++)
            {
                var footprint = new List<Vector2>(footprints[index]);
                EmitSlab(draft, footprint, ref random);
                // A few smaller chips are embedded within the same ground
                // plane, never stacked into ring-occluding raised plates.
                if (index % 7 != 0 || footprint.Count < 4) { continue; }
                int corner = (index * 3) % footprint.Count;
                Vector2 point = footprint[corner];
                Vector2 previous = footprint[(corner + footprint.Count - 1) % footprint.Count];
                Vector2 next = footprint[(corner + 1) % footprint.Count];
                var chip = new List<Vector2>
                {
                    Vector2.Lerp(point, previous, 0.24f),
                    Vector2.Lerp(point, Center(footprint), 0.18f),
                    Vector2.Lerp(point, next, 0.31f)
                };
                EnsureCounterClockwise(chip);
                EmitSlab(draft, chip, ref random, true);
            }
            if (draft.Triangles.Count / 3 > MaximumTriangles)
            {
                throw new InvalidOperationException("Frostbound court exceeded its fixed triangle budget.");
            }
            var mesh = new Mesh { name = "FrostboundFracturedCourt" };
            mesh.SetVertices(draft.Vertices);
            mesh.SetNormals(draft.Normals);
            mesh.SetUVs(0, draft.Uv);
            mesh.SetTriangles(draft.Triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Pure world-space footprints for geometry and human-scale contracts.</summary>
        public static List<Vector2[]> CreateFootprints()
        {
            // The 28 x 20 m survival area is broad in both directions. Its
            // asymmetric broken perimeter is deliberately not a route ribbon.
            var outline = new List<Vector2>
            {
                new Vector2(-51f, 4f), new Vector2(-43f, 2f),
                new Vector2(-31f, 3.8f), new Vector2(-24.8f, 9f),
                new Vector2(-26f, 17.4f), new Vector2(-33f, 21.5f),
                new Vector2(-45f, 22f), new Vector2(-52.8f, 16f)
            };
            uint random = Seed;
            var pieces = new List<List<Vector2>>(1024);
            Fracture(outline, 0, ref random, pieces);
            var footprints = new List<Vector2[]>(256);
            for (int index = 0; index < pieces.Count; index++)
            {
                List<Vector2> polygon = pieces[index];
                Vector2 center = Center(polygon);
                float keep = Next(ref random);
                // Retain the fracture cells even when their stone is absent:
                // ground pockets have broken masonry edges, not smooth decals.
                // Most of the old floor is gone. Modulated erosion clusters
                // the remainder while broad soil openings dominate the court.
                float erosion = 0.52f + 0.14f * Mathf.Sin(center.x * 0.73f + center.y * 0.31f) *
                    Mathf.Cos(center.y * 0.61f - center.x * 0.18f);
                if (EarthPocket(center) || keep < erosion || !ClearOfCover(polygon, 0.12f)) { continue; }
                float joint = 0.035f + Next(ref random) * 0.095f;
                List<Vector2> footprint = Inset(polygon, joint);
                float area = Area(footprint);
                if (footprint.Count < 3 || area < 0.3f || area > 1f) { continue; }
                EnsureCounterClockwise(footprint);
                footprints.Add(footprint.ToArray());
            }
            return footprints;
        }

        public static void Validate(GameObject root)
        {
            if (root == null || root.name != RootName || root.GetComponentsInChildren<MeshRenderer>(true).Length != 1 ||
                root.GetComponentsInChildren<Collider>(true).Length != 0 ||
                root.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                root.GetComponentsInChildren<Light>(true).Length != 0 ||
                root.GetComponentsInChildren<Camera>(true).Length != 0)
            {
                throw new InvalidOperationException("Frostbound court must be one static presentation-only batch.");
            }
            RequireFocalTransform(root.transform);
            if (root.layer != 2)
            {
                throw new InvalidOperationException("Frostbound court must remain on the presentation-only layer.");
            }
            Mesh mesh = root.GetComponent<MeshFilter>()?.sharedMesh;
            if (mesh == null || mesh.triangles.Length / 3 > MaximumTriangles || mesh.vertexCount == 0)
            {
                throw new InvalidOperationException("Frostbound court mesh is absent or outside its budget.");
            }
            foreach (Vector3 local in mesh.vertices)
            {
                Vector3 world = local + ChunkOrigin;
                if (!Finite(world.x) || !Finite(world.y) || !Finite(world.z) ||
                    world.x < -54f || world.x > -24f || world.z < 1f || world.z > 23f ||
                    world.y < MinimumHeight || world.y > MaximumHeight)
                {
                    throw new InvalidOperationException("Frostbound court escaped its shallow focal footprint.");
                }
            }
        }

        private static void RequireFocalTransform(Transform transform)
        {
            if ((transform.position - ChunkOrigin).sqrMagnitude > 0.000001f ||
                Quaternion.Angle(transform.rotation, Quaternion.identity) > 0.0001f ||
                (transform.lossyScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                throw new InvalidOperationException("Frostbound court requires the unscaled, unrotated (-1, 0) chunk origin.");
            }
        }

        private static void Fracture(List<Vector2> polygon, int depth, ref uint random,
            List<List<Vector2>> pieces)
        {
            float targetArea = 0.65f + Next(ref random) * 0.55f;
            if (depth >= 11 || Area(polygon) <= targetArea)
            {
                pieces.Add(polygon);
                return;
            }
            Vector2 minimum = polygon[0], maximum = polygon[0];
            foreach (Vector2 point in polygon)
            {
                minimum = Vector2.Min(minimum, point);
                maximum = Vector2.Max(maximum, point);
            }
            Vector2 center = Center(polygon);
            bool splitX = maximum.x - minimum.x > maximum.y - minimum.y;
            float angle = (splitX ? 0f : Mathf.PI * 0.5f) + (Next(ref random) - 0.5f) * 0.9f;
            var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            float extent = splitX ? maximum.x - minimum.x : maximum.y - minimum.y;
            float plane = Vector2.Dot(center, normal) + (Next(ref random) - 0.5f) * extent * 0.22f;
            List<Vector2> first = Clip(polygon, normal, plane);
            List<Vector2> second = Clip(polygon, -normal, -plane);
            if (first.Count < 3 || second.Count < 3 || Area(first) < 0.14f || Area(second) < 0.14f)
            {
                pieces.Add(polygon);
                return;
            }
            Fracture(first, depth + 1, ref random, pieces);
            Fracture(second, depth + 1, ref random, pieces);
        }

        private static bool EarthPocket(Vector2 point)
        {
            // Two broad collapsed voids and a broken western edge interrupt the
            // surviving surface without splitting it into a straight corridor.
            float first = Mathf.Pow((point.x + 41.8f) / 2.35f, 2f) + Mathf.Pow((point.y - 10.7f) / 2.7f, 2f);
            float second = Mathf.Pow((point.x + 31.3f) / 2.1f, 2f) + Mathf.Pow((point.y - 17.1f) / 2.1f, 2f);
            return first < 1f || second < 1f || (point.x < -48.5f && point.y < 8.2f);
        }

        private static bool ClearOfCover(List<Vector2> polygon, float clearance)
        {
            for (int obstacle = 0; obstacle < PveRegionLayout.ObstacleCount; obstacle++)
            {
                ArenaCircleObstacle circle = PveRegionLayout.GetObstacle(obstacle);
                var center = new Vector2(circle.Center.X, circle.Center.Z);
                float radius = circle.Radius + clearance;
                bool enclosed = true;
                for (int edge = 0; edge < polygon.Count; edge++)
                {
                    Vector2 a = polygon[edge], b = polygon[(edge + 1) % polygon.Count];
                    Vector2 along = b - a;
                    float t = Mathf.Clamp01(Vector2.Dot(center - a, along) / along.sqrMagnitude);
                    if ((a + along * t - center).sqrMagnitude < radius * radius) { return false; }
                    enclosed &= Cross(along, center - a) >= 0f;
                }
                if (enclosed) { return false; }
            }
            return true;
        }

        private static void EmitSlab(Draft draft, List<Vector2> polygon, ref uint random, bool chip = false)
        {
            EnsureCounterClockwise(polygon);
            Vector2 center = Center(polygon);
            float height = chip ? 0.011f : 0.007f + Next(ref random) * 0.0025f;
            float tiltX = (Next(ref random) - 0.5f) * 0.0005f;
            float tiltZ = (Next(ref random) - 0.5f) * 0.0005f;
            List<Vector2> inset = Inset(polygon, chip ? 0.006f : 0.012f + Next(ref random) * 0.018f);
            // A very slender clipping fragment needs no bevel. Keep its own
            // complete convex shape rather than collapsing it to a zero edge.
            if (inset.Count != polygon.Count) { inset = ScaleAbout(polygon, center, 0.9f); }
            AlignFirstCorner(inset, polygon[0]);
            var inner = new Vector3[polygon.Count];
            var outer = new Vector3[polygon.Count];
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 delta = inset[i] - center;
                inner[i] = new Vector3(inset[i].x, height + delta.x * tiltX + delta.y * tiltZ, inset[i].y);
                outer[i] = new Vector3(polygon[i].x, MinimumHeight, polygon[i].y);
            }
            // XZ polygons are counterclockwise; reverse the top fan for +Y.
            for (int i = 1; i < inner.Length - 1; i++) { draft.Triangle(inner[0], inner[i + 1], inner[i]); }
            for (int i = 0; i < polygon.Count; i++)
            {
                int next = (i + 1) % polygon.Count;
                draft.Quad(inner[i], inner[next], outer[next], outer[i]);
            }
            // No vertical curb: the shallow bevel is embedded in the ground.
            // Eliminating buried side faces also reserves geometry for more
            // small fragments instead of the rejected oversized floor plates.
        }

        private static void EnsureCounterClockwise(List<Vector2> polygon)
        {
            float signedArea = 0f;
            for (int i = 0; i < polygon.Count; i++)
            {
                signedArea += Cross(polygon[i], polygon[(i + 1) % polygon.Count]);
            }
            if (signedArea < 0f) { polygon.Reverse(); }
        }

        private static void AlignFirstCorner(List<Vector2> inset, Vector2 original)
        {
            int nearest = 0;
            for (int index = 1; index < inset.Count; index++)
            {
                if ((inset[index] - original).sqrMagnitude < (inset[nearest] - original).sqrMagnitude)
                {
                    nearest = index;
                }
            }
            if (nearest == 0) { return; }
            Vector2[] copy = inset.ToArray();
            for (int index = 0; index < inset.Count; index++) { inset[index] = copy[(index + nearest) % copy.Length]; }
        }

        private static List<Vector2> Inset(List<Vector2> polygon, float distance)
        {
            var result = new List<Vector2>(polygon);
            for (int edge = 0; edge < polygon.Count && result.Count >= 3; edge++)
            {
                Vector2 a = polygon[edge], b = polygon[(edge + 1) % polygon.Count];
                var outward = new Vector2(b.y - a.y, a.x - b.x).normalized;
                result = Clip(result, outward, Vector2.Dot(a, outward) - distance);
            }
            return result;
        }

        private static List<Vector2> ScaleAbout(List<Vector2> polygon, Vector2 center, float scale)
        {
            var result = new List<Vector2>(polygon.Count);
            foreach (Vector2 point in polygon) { result.Add(center + (point - center) * scale); }
            return result;
        }

        private static List<Vector2> Clip(List<Vector2> polygon, Vector2 normal, float plane)
        {
            var result = new List<Vector2>(polygon.Count + 1);
            for (int i = 0; i < polygon.Count; i++)
            {
                Vector2 a = polygon[i], b = polygon[(i + 1) % polygon.Count];
                float da = Vector2.Dot(a, normal) - plane;
                float db = Vector2.Dot(b, normal) - plane;
                if (da <= 0f) { AddDistinct(result, a); }
                if ((da < 0f && db > 0f) || (da > 0f && db < 0f))
                {
                    AddDistinct(result, Vector2.LerpUnclamped(a, b, da / (da - db)));
                }
            }
            if (result.Count > 1 && (result[0] - result[result.Count - 1]).sqrMagnitude < 0.000001f)
            {
                result.RemoveAt(result.Count - 1);
            }
            return result;
        }

        private static void AddDistinct(List<Vector2> points, Vector2 point)
        {
            if (points.Count == 0 || (points[points.Count - 1] - point).sqrMagnitude > 0.000001f)
            {
                points.Add(point);
            }
        }

        private static Vector2 Center(List<Vector2> polygon)
        {
            Vector2 sum = Vector2.zero;
            foreach (Vector2 point in polygon) { sum += point; }
            return sum / polygon.Count;
        }

        private static float Area(List<Vector2> polygon)
        {
            float result = 0f;
            for (int i = 0; i < polygon.Count; i++) { result += Cross(polygon[i], polygon[(i + 1) % polygon.Count]); }
            return Mathf.Abs(result) * 0.5f;
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static float Next(ref uint state)
        {
            state = unchecked(state * 1664525u + 1013904223u);
            return (state >> 8) / 16777216f;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private sealed class Draft
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(a, b, c);
                Triangle(a, c, d);
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 cross = Vector3.Cross(b - a, c - a);
                if (cross.sqrMagnitude < 0.000000001f) { return; }
                Vector3 normal = cross.normalized;
                int first = Vertices.Count;
                Add(a, normal); Add(b, normal); Add(c, normal);
                Triangles.Add(first); Triangles.Add(first + 1); Triangles.Add(first + 2);
            }

            private void Add(Vector3 world, Vector3 normal)
            {
                Vertices.Add(world - ChunkOrigin);
                Normals.Add(normal);
                // Continuous physical 2.5 m mapping on each dominant plane;
                // never restart 0..1 on every stone or scale a texture to shape.
                Vector2 coordinate = Mathf.Abs(normal.y) > 0.5f ? new Vector2(world.x, world.z) :
                    Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? new Vector2(world.z, world.y) :
                    new Vector2(world.x, world.y);
                Uv.Add(coordinate * TextureScale);
            }
        }
    }
}
