using System;
using System.Collections.Generic;
using UnityEngine;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>
    /// Editor-only remnants of a courtyard, not a route or combat boundary.
    /// Independent seeded sites partition broad surviving patches into adjoining
    /// chipped slabs without changing the authoritative flat floor.
    /// </summary>
    public static class SunbreakCourtyardGeometry
    {
        public const int MaximumTriangleCount = 2500;
        public const float MaximumHeight = 0.08f;
        public const float MaximumPavingHeight = 0.04f;
        public const float MaximumHalfExtent = 13f;
        public const uint DefaultSeed = 178013u;
        private const float ChunkSize = 64f;
        private const float JointHalfWidth = 0.025f;

        public sealed class CourtyardGeometry
        {
            public readonly GeometryBuffer Paving = new GeometryBuffer();
            public readonly GeometryBuffer EarthEdges = new GeometryBuffer();
            public readonly GeometryBuffer Rubble = new GeometryBuffer();
            public readonly List<StoneFootprint> Pieces = new List<StoneFootprint>();
            public int TriangleCount => Paving.TriangleCount + EarthEdges.TriangleCount + Rubble.TriangleCount;
        }

        public sealed class StoneFootprint
        {
            public readonly Vector2[] Points;
            public readonly bool IsRubble;

            internal StoneFootprint(Vector2[] points, bool isRubble)
            {
                Points = points;
                IsRubble = isRubble;
            }
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

            internal void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int first = Vertices.Count;
                Add(a, normal); Add(b, normal); Add(c, normal);
                Triangles.Add(first); Triangles.Add(first + 1); Triangles.Add(first + 2);
            }

            private void Add(Vector3 point, Vector3 normal)
            {
                Vertices.Add(point);
                Normals.Add(normal);
                // World-space 2.5 m texture scale remains continuous through
                // adjoining pieces and independent of chunk-local mesh origins.
                Uv.Add((Mathf.Abs(normal.y) > 0.65f ? new Vector2(point.x, point.z) :
                    Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? new Vector2(point.z, point.y) :
                    new Vector2(point.x, point.y)) * 0.4f);
            }
        }

        private readonly struct Site
        {
            public readonly Vector2 Center;
            public readonly float Radius;
            public readonly uint ShapeSeed;
            public readonly bool IsRubble;

            public Site(Vector2 center, float radius, uint shapeSeed, bool isRubble)
            {
                Center = center; Radius = radius; ShapeSeed = shapeSeed; IsRubble = isRubble;
            }
        }

        /// <summary>
        /// Returns world-coordinate buffers for complete pieces inside one 64 m
        /// owner cell. Every caller generates the same sites before filtering;
        /// adjacent cells cannot emit duplicates or consume different randoms.
        /// </summary>
        public static CourtyardGeometry Build(Vector3 center, Vector3 chunkOrigin, uint seed = DefaultSeed)
        {
            if (!Finite(center.x) || !Finite(center.y) || !Finite(center.z))
            {
                throw new ArgumentOutOfRangeException(nameof(center));
            }
            if (!Finite(chunkOrigin.x) || !Finite(chunkOrigin.z) ||
                Mathf.Abs(chunkOrigin.x / ChunkSize - Mathf.Round(chunkOrigin.x / ChunkSize)) > 0.0001f ||
                Mathf.Abs(chunkOrigin.z / ChunkSize - Mathf.Round(chunkOrigin.z / ChunkSize)) > 0.0001f)
            {
                throw new ArgumentOutOfRangeException(nameof(chunkOrigin));
            }

            var result = new CourtyardGeometry();
            List<Site> sites = GenerateSites(new Vector2(center.x, center.z), seed);
            int globalTriangleCount = 0;
            for (int index = 0; index < sites.Count; index++)
            {
                Site site = sites[index];
                uint random = site.ShapeSeed;
                List<Vector2> polygon = InitialFootprint(site, ref random);
                for (int other = 0; other < sites.Count && polygon.Count >= 3; other++)
                {
                    if (other == index) { continue; }
                    Vector2 difference = sites[other].Center - site.Center;
                    float distance = difference.magnitude;
                    // Shared Voronoi planes guarantee disjoint polygons even
                    // where two independently oriented ruin patches meet.
                    polygon = Clip(polygon, difference / distance,
                        distance * 0.5f - JointHalfWidth, site.Center);
                }
                Vector2 courtyardCenter = new Vector2(center.x, center.z);
                polygon = Clip(polygon, Vector2.right, 13f, courtyardCenter);
                polygon = Clip(polygon, Vector2.left, 13f, courtyardCenter);
                polygon = Clip(polygon, Vector2.up, 11.8f, courtyardCenter);
                polygon = Clip(polygon, Vector2.down, 11.8f, courtyardCenter);
                RemoveShortEdges(polygon);
                if (polygon.Count < 3 || Area(polygon) < 0.045f) { continue; }
                // Retain the absent slabs as clipping sites: their missing cells
                // produce jagged collapse holes, not rounded decorative rings.
                if (!site.IsRubble && (index == 7 || index == 21 || index == 46 || index == 65)) { continue; }
                int triangles = polygon.Count * 3 - 2;
                int categoryBudget = site.IsRubble ? MaximumTriangleCount : MaximumTriangleCount - 128;
                if (globalTriangleCount + triangles > categoryBudget) { continue; }
                globalTriangleCount += triangles;
                if (!OwnedBy(polygon, chunkOrigin)) { continue; }
                EmitStone(result, polygon, site.IsRubble, center.y, ref random);
            }
            return result;
        }

        private static List<Site> GenerateSites(Vector2 center, uint seed)
        {
            // Asymmetric islands of surviving paving, with wide earth breaks
            // between them. No ring, grid, concentric motif, or route stripe.
            var patches = new[]
            {
                new Vector4(-6.3f, -5.7f, 3.4f, 2.7f),
                new Vector4(4.3f, -6.2f, 3.8f, 2.6f),
                new Vector4(-6.4f, 2.8f, 3.2f, 3.5f),
                new Vector4(1.8f, 1.0f, 3.7f, 3.2f),
                new Vector4(6.7f, 6.5f, 2.8f, 2.3f),
                new Vector4(-1.7f, 7.4f, 3.2f, 2.5f)
            };
            var sites = new List<Site>(152);
            for (int patch = 0; patch < patches.Length; patch++)
            {
                Vector4 bounds = patches[patch];
                int accepted = 0;
                for (int attempt = 0; attempt < 300 && accepted < 24; attempt++)
                {
                    float x = Next(ref seed) * 2f - 1f;
                    float z = Next(ref seed) * 2f - 1f;
                    if (x * x + z * z > 1f) { continue; }
                    Vector2 point = center + new Vector2((bounds.x + x * bounds.z) * 1.24f,
                        (bounds.y + z * bounds.w) * 1.10f);
                    if (!Separated(sites, point, 0.45f)) { continue; }
                    sites.Add(new Site(point, 0.72f + Next(ref seed) * 0.09f, seed ^ 0x83ac54d1u, false));
                    accepted++;
                }
            }
            // Small displaced chips occupy gaps, not a repeated decorative rim.
            for (int attempt = 0, accepted = 0; attempt < 120 && accepted < 8; attempt++)
            {
                Vector2 point = center + new Vector2((Next(ref seed) * 2f - 1f) * 11.8f,
                    (Next(ref seed) * 2f - 1f) * 10.5f);
                if (!Separated(sites, point, 0.68f)) { continue; }
                sites.Add(new Site(point, 0.20f + Next(ref seed) * 0.09f, seed ^ 0x451ec877u, true));
                accepted++;
            }
            return sites;
        }

        private static List<Vector2> InitialFootprint(Site site, ref uint random)
        {
            // Broad dressed slabs begin as rectangles with selectively broken
            // corners, then meet neighboring slabs at shared clipping planes.
            // They are not isolated radial coins with six repeated corners.
            var polygon = new List<Vector2>(8);
            float yaw = (18f + (Next(ref random) - 0.5f) * 14f) * Mathf.Deg2Rad;
            float halfX = site.Radius;
            float halfZ = site.Radius * (0.75f + Next(ref random) * 0.22f);
            if (!site.IsRubble && (site.ShapeSeed & 7u) == 2u)
            {
                halfX = 0.95f;
                halfZ = 0.42f;
            }
            var corners = new[]
            {
                new Vector2(-halfX, -halfZ), new Vector2(halfX, -halfZ),
                new Vector2(halfX, halfZ), new Vector2(-halfX, halfZ)
            };
            for (int corner = 0; corner < 4; corner++)
            {
                Vector2 current = corners[corner];
                if (Next(ref random) < 0.48f)
                {
                    float chip = 0.04f + Next(ref random) * 0.13f;
                    polygon.Add(Rotate(Vector2.Lerp(current, corners[(corner + 3) % 4], chip), yaw) + site.Center);
                    polygon.Add(Rotate(Vector2.Lerp(current, corners[(corner + 1) % 4], chip), yaw) + site.Center);
                }
                else { polygon.Add(Rotate(current, yaw) + site.Center); }
            }
            return polygon;
        }

        private static Vector2 Rotate(Vector2 point, float yaw) => new Vector2(
            point.x * Mathf.Cos(yaw) - point.y * Mathf.Sin(yaw),
            point.x * Mathf.Sin(yaw) + point.y * Mathf.Cos(yaw));

        private static List<Vector2> Clip(List<Vector2> input, Vector2 normal, float distance, Vector2 origin)
        {
            var output = new List<Vector2>(input.Count + 1);
            if (input.Count == 0) { return output; }
            Vector2 previous = input[input.Count - 1];
            float previousDistance = Vector2.Dot(previous - origin, normal) - distance;
            foreach (Vector2 current in input)
            {
                float currentDistance = Vector2.Dot(current - origin, normal) - distance;
                bool previousInside = previousDistance <= 0f, currentInside = currentDistance <= 0f;
                if (previousInside != currentInside)
                {
                    float ratio = previousDistance / (previousDistance - currentDistance);
                    output.Add(Vector2.LerpUnclamped(previous, current, ratio));
                }
                if (currentInside) { output.Add(current); }
                previous = current; previousDistance = currentDistance;
            }
            return output;
        }

        private static void RemoveShortEdges(List<Vector2> polygon)
        {
            for (int index = polygon.Count - 1; index >= 0 && polygon.Count >= 3; index--)
            {
                Vector2 a = polygon[(index + polygon.Count - 1) % polygon.Count];
                Vector2 b = polygon[index];
                Vector2 c = polygon[(index + 1) % polygon.Count];
                Vector2 ab = b - a, bc = c - b;
                if (ab.sqrMagnitude < 0.0004f || bc.sqrMagnitude < 0.0004f ||
                    Mathf.Abs(ab.x * bc.y - ab.y * bc.x) < 0.00005f)
                {
                    polygon.RemoveAt(index);
                }
            }
            // Clipping may introduce tiny additional corners. Remove the least
            // significant convex corner instead of paying for barely visible
            // facets; taking a convex subset preserves the no-overlap contract.
            while (polygon.Count > 8)
            {
                int selected = 0;
                float smallest = float.MaxValue;
                for (int index = 0; index < polygon.Count; index++)
                {
                    Vector2 a = polygon[(index + polygon.Count - 1) % polygon.Count];
                    Vector2 b = polygon[index];
                    Vector2 c = polygon[(index + 1) % polygon.Count];
                    Vector2 ab = b - a, bc = c - b;
                    float area = Mathf.Abs(ab.x * bc.y - ab.y * bc.x);
                    if (area < smallest) { smallest = area; selected = index; }
                }
                polygon.RemoveAt(selected);
            }
        }

        private static void EmitStone(CourtyardGeometry result, List<Vector2> polygon,
            bool rubble, float groundY, ref uint random)
        {
            Vector2 center = Vector2.zero;
            foreach (Vector2 point in polygon) { center += point; }
            center /= polygon.Count;
            float topY = groundY + (rubble ? 0.028f : 0.018f) + Next(ref random) * (rubble ? 0.034f : 0.010f);
            Vector2 tilt = new Vector2(Next(ref random) - 0.5f, Next(ref random) - 0.5f) * 0.004f;
            var bottom = new Vector3[polygon.Count];
            var top = new Vector3[polygon.Count];
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 outer = polygon[index];
                // A narrow inward worn edge slopes directly into the earth.
                // There is no tall vertical plate rim or extra bevel strip.
                Vector2 inner = Vector2.Lerp(center, outer, 0.988f);
                float height = topY + Vector2.Dot(inner - center, tilt);
                bottom[index] = new Vector3(outer.x, groundY + 0.004f, outer.y);
                top[index] = new Vector3(inner.x, height, inner.y);
            }
            GeometryBuffer stone = rubble ? result.Rubble : result.Paving;
            for (int index = 1; index < polygon.Count - 1; index++)
            {
                stone.Triangle(top[0], top[index + 1], top[index]);
            }
            for (int index = 0; index < polygon.Count; index++)
            {
                int next = (index + 1) % polygon.Count;
                result.EarthEdges.Triangle(top[index], bottom[next], bottom[index]);
                result.EarthEdges.Triangle(top[index], top[next], bottom[next]);
            }
            result.Pieces.Add(new StoneFootprint(polygon.ToArray(), rubble));
        }

        private static bool Separated(List<Site> sites, Vector2 point, float distance)
        {
            foreach (Site site in sites)
            {
                if ((site.Center - point).sqrMagnitude < distance * distance) { return false; }
            }
            return true;
        }

        private static bool OwnedBy(List<Vector2> polygon, Vector3 origin)
        {
            foreach (Vector2 point in polygon)
            {
                if (point.x < origin.x || point.x >= origin.x + ChunkSize ||
                    point.y < origin.z || point.y >= origin.z + ChunkSize) { return false; }
            }
            return true;
        }

        private static float Area(List<Vector2> polygon)
        {
            float twiceArea = 0f;
            for (int index = 0; index < polygon.Count; index++)
            {
                Vector2 a = polygon[index], b = polygon[(index + 1) % polygon.Count];
                twiceArea += a.x * b.y - a.y * b.x;
            }
            return Mathf.Abs(twiceArea) * 0.5f;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float Next(ref uint random)
        {
            random = unchecked(random * 1664525u + 1013904223u);
            return (random & 0x00ffffffu) / 16777216f;
        }
    }
}
