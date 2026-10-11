using System;
using System.Collections.Generic;
using UnityEngine;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>
    /// Editor-only, deterministic broadleaf tree geometry. Each opaque leaf has
    /// its own silhouette and UVs; crowns do not use balls, billboards or runtime
    /// updates. The three buffers append into the basin's material-batched drafts.
    /// </summary>
    public static class SunbreakCanopyGeometry
    {
        public const int NearTriangleCount = 6372;
        public const int DistantTriangleCount = 2518;
        public const int NearLeafCount = 1216;
        public const int DistantLeafCount = 440;
        public const float MaximumLeafLength = 0.36f;
        private const float GoldenAngle = 2.39996323f;

        public sealed class TreeGeometry
        {
            public readonly GeometryBuffer Bark = new GeometryBuffer();
            public readonly GeometryBuffer Leaves = new GeometryBuffer();
            public readonly GeometryBuffer Highlights = new GeometryBuffer();
            public int LeafCount { get; internal set; }
            public int TriangleCount => Bark.TriangleCount + Leaves.TriangleCount + Highlights.TriangleCount;
        }

        public sealed class GeometryBuffer
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
            public int TriangleCount => Triangles.Count / 3;

            public void AppendTo(List<Vector3> vertices, List<Vector3> normals,
                List<Vector2> uv, List<int> triangles)
            {
                int offset = vertices.Count;
                vertices.AddRange(Vertices);
                normals.AddRange(Normals);
                uv.AddRange(Uv);
                foreach (int index in Triangles) { triangles.Add(offset + index); }
            }

            internal void Triangle(Vector3 a, Vector3 b, Vector3 c,
                Vector2 uvA, Vector2 uvB, Vector2 uvC,
                Vector3 normalA, Vector3 normalB, Vector3 normalC)
            {
                int first = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
                Normals.Add(normalA); Normals.Add(normalB); Normals.Add(normalC);
                Uv.Add(uvA); Uv.Add(uvB); Uv.Add(uvC);
                Triangles.Add(first); Triangles.Add(first + 1); Triangles.Add(first + 2);
            }

            internal void FlatTriangle(Vector3 a, Vector3 b, Vector3 c,
                Vector2 uvA, Vector2 uvB, Vector2 uvC)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                Triangle(a, b, c, uvA, uvB, uvC, normal, normal, normal);
            }
        }

        public static TreeGeometry Build(Vector3 center, float height, float spread,
            ref uint random, bool distant = false)
        {
            if (!Finite(center.x) || !Finite(center.y) || !Finite(center.z))
            {
                throw new ArgumentOutOfRangeException(nameof(center));
            }
            if (!Finite(height) || height <= 0f) { throw new ArgumentOutOfRangeException(nameof(height)); }
            if (!Finite(spread) || spread <= 0f) { throw new ArgumentOutOfRangeException(nameof(spread)); }

            var tree = new TreeGeometry();
            float yaw = Next(ref random) * Mathf.PI * 2f;
            float lean = height * (0.025f + Next(ref random) * 0.032f);
            float phase = Next(ref random) * Mathf.PI * 2f;
            var trunk = new Vector3[8];
            var trunkRadii = new float[8];
            for (int ring = 0; ring < trunk.Length; ring++)
            {
                float t = ring / 7f;
                trunk[ring] = center + TrunkOffset(t, height, lean, yaw);
                // The low buttress swells into the roots; above it, taper is
                // continuous through the branch unions, not stacked cylinders.
                trunkRadii[ring] = height * (0.004f + 0.032f * Mathf.Pow(1f - t, 1.35f)
                    + (ring == 0 ? 0.013f : 0f));
            }
            Tube(tree.Bark, trunk, trunkRadii, distant ? 8 : 10, phase);

            for (int root = 0; root < 5; root++)
            {
                float angle = yaw + root * Mathf.PI * 0.4f + (Next(ref random) - 0.5f) * 0.3f;
                float reach = height * (0.09f + Next(ref random) * 0.035f);
                Tube(tree.Bark, new[]
                {
                    center + Polar(height * 0.018f, angle, height * 0.095f),
                    center + Polar(reach * 0.56f, angle + 0.05f, height * 0.014f),
                    center + Polar(reach, angle + 0.09f, -height * 0.022f)
                }, new[] { height * 0.024f, height * 0.013f, height * 0.0025f }, distant ? 5 : 6, phase);
            }

            int branches = distant ? 5 : 8;
            int secondaries = distant ? 2 : 3;
            int leavesPerSpray = distant ? 40 : 48;
            for (int branch = 0; branch < branches; branch++)
            {
                float level = branch / (branches - 1f);
                float angle = yaw + GoldenAngle * branch + (Next(ref random) - 0.5f) * 0.32f;
                float reach = spread * (0.64f - level * 0.29f) * (0.88f + Next(ref random) * 0.16f);
                float startLevel = 0.35f + level * 0.37f;
                Vector3 start = center + TrunkOffset(startLevel, height, lean, yaw);
                Vector3 knuckle = center + Polar(reach * 0.35f, angle - 0.12f,
                    height * (0.52f + level * 0.27f));
                Vector3 shoulder = center + Polar(reach * 0.76f, angle,
                    height * (0.58f + level * 0.27f));
                Vector3 tip = center + Polar(reach, angle + 0.10f,
                    height * (0.62f + level * 0.28f));
                float radius = height * (0.018f - level * 0.008f);
                Tube(tree.Bark, new[] { start, knuckle, shoulder, tip },
                    new[] { radius, radius * 0.64f, radius * 0.31f, radius * 0.10f },
                    distant ? 6 : 7, phase + branch * 0.37f);

                for (int twig = 0; twig < secondaries; twig++)
                {
                    float fan = secondaries == 2 ? (twig == 0 ? -0.56f : 0.56f) : (twig - 1f) * 0.66f;
                    float forkAngle = angle + fan;
                    Vector3 forkStart = Vector3.Lerp(knuckle, tip, 0.30f + twig * 0.24f);
                    float forkReach = spread * (0.18f + Next(ref random) * 0.06f);
                    Vector3 forkMiddle = forkStart + Polar(forkReach * 0.61f, forkAngle,
                        height * (0.027f + Next(ref random) * 0.025f));
                    Vector3 forkEnd = forkStart + Polar(forkReach, forkAngle + fan * 0.22f,
                        height * (0.058f + Next(ref random) * 0.025f));
                    Tube(tree.Bark, new[] { forkStart, forkMiddle, forkEnd },
                        new[] { height * 0.0046f, height * 0.0023f, height * 0.00065f },
                        distant ? 4 : 5, phase + twig * 0.57f);
                    LeafSpray(tree, Vector3.Lerp(forkStart, forkEnd, 0.16f), forkEnd,
                        spread * (0.115f + Next(ref random) * 0.018f), branch * secondaries + twig,
                        leavesPerSpray, ref random);
                }
            }

            LeafSpray(tree, trunk[6], trunk[7] + Vector3.up * height * 0.018f,
                spread * 0.15f, branches * secondaries, distant ? 40 : 64, ref random);
            return tree;
        }

        private static void LeafSpray(TreeGeometry tree, Vector3 start, Vector3 end,
            float radius, int sprayIndex, int count, ref uint random)
        {
            Vector3 axis = (end - start).normalized;
            Vector3 side = Vector3.Cross(axis, Vector3.up).normalized;
            if (side.sqrMagnitude < 0.1f) { side = Vector3.right; }
            Vector3 crownUp = Vector3.Cross(side, axis).normalized;
            if (crownUp.y < 0f) { crownUp = -crownUp; }
            for (int leaf = 0; leaf < count; leaf++)
            {
                // Overlapping ellipsoidal sprays attach to twig axes. The
                // shared silhouette is dense, but its edge remains individual
                // leaves with branch-sized negative spaces between sprays.
                float t = (leaf + Next(ref random)) / count;
                float angle = leaf * GoldenAngle + Next(ref random) * 0.45f;
                float reach = radius * Mathf.Sqrt(0.08f + Next(ref random) * 0.92f);
                float taper = 0.54f + 0.46f * Mathf.Sin(t * Mathf.PI);
                Vector3 point = Vector3.Lerp(start, end, t) +
                    side * (Mathf.Cos(angle) * reach * taper) +
                    crownUp * (Mathf.Sin(angle) * reach * 0.59f);
                float direction = angle + sprayIndex * 0.46f;
                Vector3 along = Polar(1f, direction, -0.26f + Next(ref random) * 0.68f).normalized;
                Vector3 across = Vector3.Cross(Vector3.up, along).normalized;
                across = Quaternion.AngleAxis((Next(ref random) - 0.5f) * 58f, along) * across;
                Vector3 normal = Vector3.Cross(along, across).normalized;
                float length = 0.22f + Next(ref random) * (MaximumLeafLength - 0.22f);
                Vector3 bottom = point - along * length * 0.45f;
                Vector3 tip = point + along * length * 0.55f;
                Vector3 left = point - across * length * 0.34f - normal * length * 0.065f;
                Vector3 right = point + across * length * 0.34f - normal * length * 0.065f;
                GeometryBuffer buffer = leaf % 7 == 0 || (sprayIndex % 4 == 0 && leaf % 3 == 0)
                    ? tree.Highlights : tree.Leaves;
                Leaf(buffer, bottom, left, tip, right);
                tree.LeafCount++;
            }
        }

        private static void Leaf(GeometryBuffer buffer, Vector3 bottom, Vector3 left,
            Vector3 tip, Vector3 right)
        {
            var baseUv = new Vector2(0.5f, 0f);
            var leftUv = new Vector2(0f, 0.45f);
            var tipUv = new Vector2(0.5f, 1f);
            var rightUv = new Vector2(1f, 0.45f);
            buffer.FlatTriangle(bottom, left, tip, baseUv, leftUv, tipUv);
            buffer.FlatTriangle(bottom, tip, right, baseUv, tipUv, rightUv);
            buffer.FlatTriangle(tip, left, bottom, tipUv, leftUv, baseUv);
            buffer.FlatTriangle(right, tip, bottom, rightUv, tipUv, baseUv);
        }

        private static void Tube(GeometryBuffer buffer, Vector3[] centers, float[] radii,
            int sides, float phase)
        {
            var ring = new Vector3[centers.Length, sides + 1];
            var normals = new Vector3[centers.Length, sides + 1];
            var length = new float[centers.Length];
            Vector3 reference = Vector3.Cross((centers[centers.Length - 1] - centers[0]).normalized,
                Vector3.forward).normalized;
            if (reference.sqrMagnitude < 0.1f) { reference = Vector3.right; }
            for (int row = 0; row < centers.Length; row++)
            {
                Vector3 tangent = (centers[Mathf.Min(row + 1, centers.Length - 1)] -
                    centers[Mathf.Max(row - 1, 0)]).normalized;
                Vector3 right = Vector3.ProjectOnPlane(reference, tangent).normalized;
                if (right.sqrMagnitude < 0.1f)
                {
                    right = Vector3.Cross(tangent, Mathf.Abs(tangent.z) < 0.9f ? Vector3.forward : Vector3.right).normalized;
                }
                Vector3 forward = Vector3.Cross(right, tangent).normalized;
                if (row > 0) { length[row] = length[row - 1] + Vector3.Distance(centers[row], centers[row - 1]); }
                for (int side = 0; side <= sides; side++)
                {
                    float angle = (side % sides) * Mathf.PI * 2f / sides + phase;
                    Vector3 normal = right * Mathf.Cos(angle) + forward * Mathf.Sin(angle);
                    float ridges = 1f + 0.09f * Mathf.Sin(angle * 3f + phase);
                    ring[row, side] = centers[row] + normal * radii[row] * ridges;
                    normals[row, side] = normal;
                }
            }
            for (int row = 0; row < centers.Length - 1; row++)
            {
                for (int side = 0; side < sides; side++)
                {
                    Vector3 a = ring[row, side], b = ring[row + 1, side];
                    Vector3 c = ring[row + 1, side + 1], d = ring[row, side + 1];
                    Vector3 na = normals[row, side], nb = normals[row + 1, side];
                    Vector3 nc = normals[row + 1, side + 1], nd = normals[row, side + 1];
                    var uvA = new Vector2(side / (float)sides, length[row] * 0.8f);
                    var uvB = new Vector2(side / (float)sides, length[row + 1] * 0.8f);
                    var uvC = new Vector2((side + 1f) / sides, length[row + 1] * 0.8f);
                    var uvD = new Vector2((side + 1f) / sides, length[row] * 0.8f);
                    buffer.Triangle(a, b, c, uvA, uvB, uvC, na, nb, nc);
                    buffer.Triangle(a, c, d, uvA, uvC, uvD, na, nc, nd);
                }
            }
            int last = centers.Length - 1;
            for (int side = 0; side < sides; side++)
            {
                buffer.FlatTriangle(ring[0, side + 1], centers[0], ring[0, side],
                    Vector2.right, Vector2.one * 0.5f, Vector2.zero);
                buffer.FlatTriangle(ring[last, side], centers[last], ring[last, side + 1],
                    Vector2.zero, Vector2.one * 0.5f, Vector2.right);
            }
        }

        private static Vector3 TrunkOffset(float t, float height, float lean, float yaw) =>
            Polar(lean * t * t, yaw + t * 0.58f, height * t * 0.93f);

        private static Vector3 Polar(float radius, float angle, float height) =>
            new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Next(ref uint random)
        {
            random = unchecked(random * 1664525u + 1013904223u);
            return (random & 0x00ffffffu) / 16777216f;
        }
    }
}
