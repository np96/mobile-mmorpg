using System;
using System.Collections.Generic;
using NUnit.Framework;
using SessionRpg.Editor.Authoring;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakCourtyardGeometryTests
    {
        private static readonly Vector3 CampCenter = new Vector3(-35f, 0f, 12f);
        private static readonly Vector3 OwnerOrigin = new Vector3(-64f, 0f, 0f);

        [TestCase(0u)]
        [TestCase(178013u)]
        [TestCase(987654321u)]
        [TestCase(uint.MaxValue)]
        public void Courtyard_HasBoundedCompleteGeometryAndHumanScaleShallowStones(uint seed)
        {
            SunbreakCourtyardGeometry.CourtyardGeometry courtyard =
                SunbreakCourtyardGeometry.Build(CampCenter, OwnerOrigin, seed);
            Assert.That(courtyard.TriangleCount, Is.InRange(2000, 2500));
            Assert.That(courtyard.Pieces.Count, Is.InRange(130, 150));
            Assert.That(SunbreakCourtyardGeometry.MaximumTriangleCount, Is.EqualTo(2500));
            Assert.That(SunbreakCourtyardGeometry.MaximumHeight, Is.LessThanOrEqualTo(0.10f));
            AssertBuffer(courtyard.Paving);
            AssertBuffer(courtyard.EarthEdges);
            AssertBuffer(courtyard.Rubble);
            foreach (Vector3 point in courtyard.Paving.Vertices)
            {
                Assert.That(point.y, Is.InRange(0.015f, SunbreakCourtyardGeometry.MaximumPavingHeight),
                    "Walkable paving must remain mostly buried rather than occluding feet with raised plates.");
            }
            foreach (Vector3 normal in courtyard.Paving.Normals)
            {
                Assert.That(normal.y, Is.GreaterThan(0.99f), "Every visible slab top faces upward.");
            }
            int edgeVertex = 0;
            foreach (SunbreakCourtyardGeometry.StoneFootprint piece in courtyard.Pieces)
            {
                Assert.That(piece.Points.Length, Is.InRange(3, 8));
                Vector2 pieceCenter = Vector2.zero;
                foreach (Vector2 point in piece.Points) { pieceCenter += point; }
                pieceCenter /= piece.Points.Length;
                for (int triangle = 0; triangle < piece.Points.Length * 2; triangle++, edgeVertex += 3)
                {
                    Vector3 middle = (courtyard.EarthEdges.Vertices[edgeVertex] +
                        courtyard.EarthEdges.Vertices[edgeVertex + 1] + courtyard.EarthEdges.Vertices[edgeVertex + 2]) / 3f;
                    Vector3 outward = new Vector3(middle.x - pieceCenter.x, 0f, middle.z - pieceCenter.y);
                    Assert.That(Vector3.Dot(courtyard.EarthEdges.Normals[edgeVertex], outward), Is.GreaterThan(0f),
                        "Sloped perimeter fronts must face out of the stone, not inward into its hidden volume.");
                }
                for (int first = 0; first < piece.Points.Length; first++)
                {
                    Vector2 point = piece.Points[first];
                    Assert.That(point.x, Is.InRange(CampCenter.x - 13f, CampCenter.x + 13f));
                    Assert.That(point.y, Is.InRange(CampCenter.z - 13f, CampCenter.z + 13f));
                    for (int second = first + 1; second < piece.Points.Length; second++)
                    {
                        Assert.That(Vector2.Distance(point, piece.Points[second]), Is.LessThanOrEqualTo(2.35f),
                            "Surviving slabs may include long dressed blocks, but never a giant floor plate.");
                    }
                }
            }
        }

        [Test]
        public void Courtyard_ReproducesAllBuffersWithoutUsingGlobalUnityRandom()
        {
            UnityEngine.Random.State original = UnityEngine.Random.state;
            SunbreakCourtyardGeometry.CourtyardGeometry first =
                SunbreakCourtyardGeometry.Build(CampCenter, OwnerOrigin);
            SunbreakCourtyardGeometry.CourtyardGeometry second =
                SunbreakCourtyardGeometry.Build(CampCenter, OwnerOrigin);
            Assert.That(UnityEngine.Random.state, Is.EqualTo(original));
            AssertEqual(first.Paving, second.Paving);
            AssertEqual(first.EarthEdges, second.EarthEdges);
            AssertEqual(first.Rubble, second.Rubble);
            Assert.That(first.Pieces.Count, Is.EqualTo(second.Pieces.Count));
            for (int index = 0; index < first.Pieces.Count; index++)
            {
                CollectionAssert.AreEqual(first.Pieces[index].Points, second.Pieces[index].Points);
                Assert.That(first.Pieces[index].IsRubble, Is.EqualTo(second.Pieces[index].IsRubble));
            }
            SunbreakCourtyardGeometry.CourtyardGeometry different =
                SunbreakCourtyardGeometry.Build(CampCenter, OwnerOrigin, 178014u);
            Assert.That(first.Paving.Vertices, Is.Not.EqualTo(different.Paving.Vertices));
        }

        [Test]
        public void Courtyard_ClippedFootprintsHaveNoOverlapsOrTouchingPaverEdges()
        {
            SunbreakCourtyardGeometry.CourtyardGeometry courtyard =
                SunbreakCourtyardGeometry.Build(CampCenter, OwnerOrigin);
            var adjoining = new HashSet<int>();
            int pavingCount = 0;
            for (int first = 0; first < courtyard.Pieces.Count; first++)
            {
                if (!courtyard.Pieces[first].IsRubble) { pavingCount++; }
                for (int second = first + 1; second < courtyard.Pieces.Count; second++)
                {
                    Vector2[] a = courtyard.Pieces[first].Points, b = courtyard.Pieces[second].Points;
                    Assert.That(HasSeparatingEdge(a, b) || HasSeparatingEdge(b, a), Is.True,
                        "Fragments " + first + " and " + second + " overlap or have no exposed earth joint.");
                    if (!courtyard.Pieces[first].IsRubble && !courtyard.Pieces[second].IsRubble &&
                        MinimumGap(a, b) < 0.071f)
                    {
                        adjoining.Add(first); adjoining.Add(second);
                    }
                }
            }
            Assert.That(adjoining.Count, Is.GreaterThanOrEqualTo(pavingCount * 0.85f),
                "Most surviving slabs must tile into patches with 5–7 cm soil joints, not isolated stepping stones.");
        }

        [Test]
        public void Courtyard_CoversAnAsymmetricAreaWithBroadEarthBreaksInsteadOfARingOrRoad()
        {
            SunbreakCourtyardGeometry.CourtyardGeometry courtyard =
                SunbreakCourtyardGeometry.Build(CampCenter, OwnerOrigin);
            Bounds bounds = BoundsOf(courtyard.Paving.Vertices);
            Assert.That(bounds.size.x, Is.GreaterThan(20f));
            Assert.That(bounds.size.z, Is.GreaterThan(18f));
            Assert.That(bounds.size.x / bounds.size.z, Is.InRange(0.7f, 1.4f),
                "The composition is a courtyard field, not a narrow paving stripe.");
            float area = 0f;
            float nearest = float.MaxValue, furthest = 0f;
            var occupiedQuadrants = new HashSet<int>();
            var heights = new HashSet<int>();
            var cornerCounts = new HashSet<int>();
            var slabAreas = new List<float>();
            foreach (SunbreakCourtyardGeometry.StoneFootprint piece in courtyard.Pieces)
            {
                cornerCounts.Add(piece.Points.Length);
                Vector2 center = Vector2.zero;
                foreach (Vector2 point in piece.Points) { center += point; }
                center /= piece.Points.Length;
                Vector2 offset = center - new Vector2(CampCenter.x, CampCenter.z);
                nearest = Mathf.Min(nearest, offset.magnitude);
                furthest = Mathf.Max(furthest, offset.magnitude);
                occupiedQuadrants.Add((offset.x > 0f ? 1 : 0) + (offset.y > 0f ? 2 : 0));
                area += PolygonArea(piece.Points);
                if (!piece.IsRubble) { slabAreas.Add(PolygonArea(piece.Points)); }
            }
            foreach (Vector3 point in courtyard.Paving.Vertices) { heights.Add(Mathf.RoundToInt(point.y * 1000f)); }
            Assert.That(occupiedQuadrants.Count, Is.EqualTo(4));
            Assert.That(nearest, Is.LessThan(3f));
            Assert.That(furthest, Is.GreaterThan(8f));
            Assert.That(furthest - nearest, Is.GreaterThan(5f), "No fixed-radius stone ring.");
            Assert.That(area / (bounds.size.x * bounds.size.z), Is.InRange(0.25f, 0.35f),
                "The default courtyard keeps substantial surviving tiled patches and broad collapsed earth breaks.");
            Assert.That(cornerCounts.Count, Is.GreaterThanOrEqualTo(4),
                "Chipped rectangular and irregular cells must not repeat one six-corner coin silhouette.");
            slabAreas.Sort();
            Assert.That(slabAreas[slabAreas.Count / 2], Is.InRange(0.7f, 1.4f),
                "Median surface area must read as human-scale paving, not giant plates beside the actors.");
            Assert.That(slabAreas[slabAreas.Count - 1], Is.LessThan(2.4f));
            Assert.That(heights.Count, Is.GreaterThanOrEqualTo(8));
        }

        [Test]
        public void Courtyard_FrostboundWatchBelongsEntirelyToOneChunk()
        {
            int total = 0;
            for (int x = -2; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    SunbreakCourtyardGeometry.CourtyardGeometry courtyard = SunbreakCourtyardGeometry.Build(
                        CampCenter, new Vector3(x * 64f, 0f, z * 64f));
                    if (x == -1 && z == 0) { Assert.That(courtyard.TriangleCount, Is.GreaterThan(2000)); }
                    else { Assert.That(courtyard.TriangleCount, Is.Zero); }
                    total += courtyard.TriangleCount;
                }
            }
            Assert.That(total, Is.LessThanOrEqualTo(2500));
        }

        [Test]
        public void Courtyard_OnlyEmitsCompletePiecesWithinTheirChunkAtASeam()
        {
            int totalTriangles = 0;
            var centers = new HashSet<Vector2>();
            for (int x = -1; x <= 0; x++)
            {
                for (int z = -1; z <= 0; z++)
                {
                    Vector3 origin = new Vector3(x * 64f, 0f, z * 64f);
                    SunbreakCourtyardGeometry.CourtyardGeometry courtyard =
                        SunbreakCourtyardGeometry.Build(Vector3.zero, origin);
                    totalTriangles += courtyard.TriangleCount;
                    foreach (SunbreakCourtyardGeometry.StoneFootprint piece in courtyard.Pieces)
                    {
                        Vector2 center = Vector2.zero;
                        foreach (Vector2 point in piece.Points)
                        {
                            Assert.That(point.x, Is.GreaterThanOrEqualTo(origin.x).And.LessThan(origin.x + 64f));
                            Assert.That(point.y, Is.GreaterThanOrEqualTo(origin.z).And.LessThan(origin.z + 64f));
                            center += point;
                        }
                        Assert.That(centers.Add(center / piece.Points.Length), Is.True, "No duplicated seam piece.");
                    }
                }
            }
            Assert.That(totalTriangles, Is.InRange(1600, 2500));
        }

        [Test]
        public void Courtyard_BufferAppendPreservesSourceAndUsesDestinationVertexOffset()
        {
            SunbreakCourtyardGeometry.GeometryBuffer source =
                SunbreakCourtyardGeometry.Build(CampCenter, OwnerOrigin).Rubble;
            var vertices = new List<Vector3> { Vector3.left, Vector3.up, Vector3.right };
            var normals = new List<Vector3> { Vector3.forward, Vector3.forward, Vector3.forward };
            var uv = new List<Vector2> { Vector2.zero, Vector2.up, Vector2.right };
            var triangles = new List<int> { 0, 1, 2 };
            int sourceCount = source.Vertices.Count;
            source.AppendTo(vertices, normals, uv, triangles);
            Assert.That(vertices.Count, Is.EqualTo(sourceCount + 3));
            Assert.That(normals.Count, Is.EqualTo(vertices.Count));
            Assert.That(uv.Count, Is.EqualTo(vertices.Count));
            Assert.That(vertices[0], Is.EqualTo(Vector3.left));
            for (int index = 0; index < source.Triangles.Count; index++)
            {
                Assert.That(triangles[index + 3], Is.EqualTo(source.Triangles[index] + 3));
            }
            source.AppendTo(vertices, normals, uv, triangles);
            Assert.That(source.Vertices.Count, Is.EqualTo(sourceCount));
            Assert.That(triangles[source.Triangles.Count + 3], Is.EqualTo(sourceCount + 3));
        }

        [TestCase(float.NaN, 0f)]
        [TestCase(0f, float.PositiveInfinity)]
        [TestCase(1f, 0f)]
        public void Courtyard_RejectsInvalidOrNonCellAlignedChunkOrigin(float x, float z)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SunbreakCourtyardGeometry.Build(CampCenter, new Vector3(x, 0f, z)));
        }

        [Test]
        public void Courtyard_RejectsNonFiniteCampCenter()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SunbreakCourtyardGeometry.Build(new Vector3(0f, float.NaN, 0f), OwnerOrigin));
        }

        private static void AssertBuffer(SunbreakCourtyardGeometry.GeometryBuffer buffer)
        {
            Assert.That(buffer.Vertices.Count, Is.GreaterThan(0));
            Assert.That(buffer.Vertices.Count, Is.EqualTo(buffer.Normals.Count));
            Assert.That(buffer.Vertices.Count, Is.EqualTo(buffer.Uv.Count));
            Assert.That(buffer.Vertices.Count, Is.EqualTo(buffer.Triangles.Count));
            for (int index = 0; index < buffer.Vertices.Count; index++)
            {
                Vector3 point = buffer.Vertices[index], normal = buffer.Normals[index];
                Assert.That(Finite(point.x) && Finite(point.y) && Finite(point.z), Is.True);
                Assert.That(normal.sqrMagnitude, Is.EqualTo(1f).Within(0.0001f));
                Assert.That(point.y, Is.InRange(0.004f - 0.00001f, SunbreakCourtyardGeometry.MaximumHeight));
                Assert.That(buffer.Triangles[index], Is.EqualTo(index));
                Vector2 expected = (Mathf.Abs(normal.y) > 0.65f ? new Vector2(point.x, point.z) :
                    Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? new Vector2(point.z, point.y) :
                    new Vector2(point.x, point.y)) * 0.4f;
                Assert.That(buffer.Uv[index], Is.EqualTo(expected));
            }
            for (int index = 0; index < buffer.Vertices.Count; index += 3)
            {
                Vector3 a = buffer.Vertices[index], b = buffer.Vertices[index + 1], c = buffer.Vertices[index + 2];
                Vector3 cross = Vector3.Cross(b - a, c - a);
                Assert.That(cross.sqrMagnitude, Is.GreaterThan(1e-12f));
                Assert.That(Vector3.Dot(cross.normalized, buffer.Normals[index]), Is.GreaterThan(0.999f),
                    "Stored normals must agree with the actual front-face winding.");
                Assert.That(buffer.Normals[index], Is.EqualTo(buffer.Normals[index + 1]));
                Assert.That(buffer.Normals[index], Is.EqualTo(buffer.Normals[index + 2]));
            }
        }

        private static void AssertEqual(SunbreakCourtyardGeometry.GeometryBuffer a,
            SunbreakCourtyardGeometry.GeometryBuffer b)
        {
            CollectionAssert.AreEqual(a.Vertices, b.Vertices);
            CollectionAssert.AreEqual(a.Normals, b.Normals);
            CollectionAssert.AreEqual(a.Uv, b.Uv);
            CollectionAssert.AreEqual(a.Triangles, b.Triangles);
        }

        private static bool HasSeparatingEdge(Vector2[] first, Vector2[] second)
        {
            for (int edge = 0; edge < first.Length; edge++)
            {
                Vector2 along = first[(edge + 1) % first.Length] - first[edge];
                Vector2 axis = new Vector2(-along.y, along.x).normalized;
                float minA = float.MaxValue, maxA = float.MinValue;
                float minB = float.MaxValue, maxB = float.MinValue;
                foreach (Vector2 point in first)
                {
                    float projection = Vector2.Dot(point, axis);
                    minA = Mathf.Min(minA, projection); maxA = Mathf.Max(maxA, projection);
                }
                foreach (Vector2 point in second)
                {
                    float projection = Vector2.Dot(point, axis);
                    minB = Mathf.Min(minB, projection); maxB = Mathf.Max(maxB, projection);
                }
                if (minA > maxB + 0.001f || minB > maxA + 0.001f) { return true; }
            }
            return false;
        }

        private static float MinimumGap(Vector2[] first, Vector2[] second)
        {
            float minimum = float.MaxValue;
            for (int a = 0; a < first.Length; a++)
            {
                for (int b = 0; b < second.Length; b++)
                {
                    minimum = Mathf.Min(minimum, PointSegmentDistance(first[a], second[b],
                        second[(b + 1) % second.Length]));
                    minimum = Mathf.Min(minimum, PointSegmentDistance(second[b], first[a],
                        first[(a + 1) % first.Length]));
                }
            }
            return minimum;
        }

        private static float PointSegmentDistance(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 edge = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, edge) / edge.sqrMagnitude);
            return Vector2.Distance(point, a + edge * t);
        }

        private static Bounds BoundsOf(List<Vector3> points)
        {
            var bounds = new Bounds(points[0], Vector3.zero);
            foreach (Vector3 point in points) { bounds.Encapsulate(point); }
            return bounds;
        }

        private static float PolygonArea(Vector2[] points)
        {
            float twiceArea = 0f;
            for (int index = 0; index < points.Length; index++)
            {
                Vector2 a = points[index], b = points[(index + 1) % points.Length];
                twiceArea += a.x * b.y - a.y * b.x;
            }
            return Mathf.Abs(twiceArea) * 0.5f;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
