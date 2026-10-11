using System;
using System.Collections.Generic;
using NUnit.Framework;
using SessionRpg.Editor.Authoring;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakCanopyGeometryTests
    {
        [TestCase(false, 6372, 1508, 1216)]
        [TestCase(true, 2518, 758, 440)]
        public void Tree_UsesExactSeedIndependentTriangleAndIndividualLeafBudgets(
            bool distant, int triangles, int woodTriangles, int leafCount)
        {
            foreach (uint seed in new uint[] { 0u, 1u, 9671u, 123456789u, uint.MaxValue })
            {
                uint random = seed;
                SunbreakCanopyGeometry.TreeGeometry tree = SunbreakCanopyGeometry.Build(
                    Vector3.zero, 7.2f, 3.3f, ref random, distant);
                Assert.That(tree.TriangleCount, Is.EqualTo(triangles));
                Assert.That(tree.Bark.TriangleCount, Is.EqualTo(woodTriangles));
                Assert.That(tree.LeafCount, Is.EqualTo(leafCount));
                Assert.That(tree.Leaves.TriangleCount + tree.Highlights.TriangleCount,
                    Is.EqualTo(leafCount * 4), "Every small folded leaf has exactly two front and two back triangles.");
                Assert.That(tree.TriangleCount, Is.LessThanOrEqualTo(6500));
                Assert.That(tree.Highlights.TriangleCount, Is.InRange(leafCount / 2, leafCount * 2));
                AssertBuffer(tree.Bark);
                AssertBuffer(tree.Leaves);
                AssertBuffer(tree.Highlights);
                AssertLeaves(tree.Leaves);
                AssertLeaves(tree.Highlights);
            }
            Assert.That(SunbreakCanopyGeometry.NearTriangleCount, Is.EqualTo(6372));
            Assert.That(SunbreakCanopyGeometry.DistantTriangleCount, Is.EqualTo(2518));
            Assert.That(SunbreakCanopyGeometry.NearLeafCount, Is.EqualTo(1216));
            Assert.That(SunbreakCanopyGeometry.DistantLeafCount, Is.EqualTo(440));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Tree_SameSeedReproducesEveryBufferAndDoesNotUseUnityRandom(bool distant)
        {
            UnityEngine.Random.State original = UnityEngine.Random.state;
            uint firstSeed = 917u;
            uint secondSeed = 917u;
            SunbreakCanopyGeometry.TreeGeometry first = SunbreakCanopyGeometry.Build(
                new Vector3(8f, 0.43f, -11f), 8.2f, 3.6f, ref firstSeed, distant);
            SunbreakCanopyGeometry.TreeGeometry second = SunbreakCanopyGeometry.Build(
                new Vector3(8f, 0.43f, -11f), 8.2f, 3.6f, ref secondSeed, distant);
            Assert.That(firstSeed, Is.EqualTo(secondSeed));
            Assert.That(firstSeed, Is.Not.EqualTo(917u), "The caller owns the advancing deterministic authoring seed.");
            Assert.That(UnityEngine.Random.state, Is.EqualTo(original));
            AssertEqual(first.Bark, second.Bark);
            AssertEqual(first.Leaves, second.Leaves);
            AssertEqual(first.Highlights, second.Highlights);

            uint differentSeed = 918u;
            SunbreakCanopyGeometry.TreeGeometry different = SunbreakCanopyGeometry.Build(
                new Vector3(8f, 0.43f, -11f), 8.2f, 3.6f, ref differentSeed, distant);
            Assert.That(first.Bark.Vertices, Is.Not.EqualTo(different.Bark.Vertices));
            Assert.That(first.Leaves.Vertices, Is.Not.EqualTo(different.Leaves.Vertices));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Tree_HasGroundedRootsAndAnElevatedBroadCrownWithoutGiantLeaves(bool distant)
        {
            var center = new Vector3(6f, 0.5f, -13f);
            const float height = 7.2f;
            const float spread = 3.3f;
            uint seed = 9671u;
            SunbreakCanopyGeometry.TreeGeometry tree = SunbreakCanopyGeometry.Build(
                center, height, spread, ref seed, distant);
            Bounds wood = BoundsOf(tree.Bark.Vertices);
            Bounds crown = BoundsOf(tree.Leaves.Vertices);
            crown.Encapsulate(BoundsOf(tree.Highlights.Vertices));
            Assert.That(wood.min.y, Is.LessThan(center.y), "Fine roots enter the ground rather than floating above it.");
            Assert.That(wood.min.y, Is.GreaterThan(center.y - height * 0.04f));
            Assert.That(wood.max.y, Is.GreaterThan(center.y + height * 0.90f));
            Assert.That(crown.min.y, Is.GreaterThan(center.y + height * 0.42f));
            Assert.That(crown.max.y, Is.LessThan(center.y + height * 1.08f));
            Assert.That(crown.size.x, Is.GreaterThan(spread));
            Assert.That(crown.size.z, Is.GreaterThan(spread));
            Assert.That(crown.size.y, Is.GreaterThan(height * 0.25f),
                "A broadleaf crown must occupy a volume, not one paper-flat canopy plane.");
            Assert.That(crown.size.x, Is.LessThan(spread * 2.4f));
            Assert.That(crown.size.z, Is.LessThan(spread * 2.4f));
            Assert.That(crown.min.x, Is.LessThan(center.x - spread * 0.45f));
            Assert.That(crown.max.x, Is.GreaterThan(center.x + spread * 0.45f));
            Assert.That(crown.min.z, Is.LessThan(center.z - spread * 0.45f));
            Assert.That(crown.max.z, Is.GreaterThan(center.z + spread * 0.45f));
        }

        [Test]
        public void GeometryBuffer_AppendsUsingExistingVertexOffsetWithoutMutatingSource()
        {
            uint seed = 1u;
            SunbreakCanopyGeometry.GeometryBuffer source = SunbreakCanopyGeometry.Build(
                Vector3.zero, 7f, 3f, ref seed, true).Highlights;
            var vertices = new List<Vector3> { Vector3.left, Vector3.up, Vector3.right };
            var normals = new List<Vector3> { Vector3.forward, Vector3.forward, Vector3.forward };
            var uv = new List<Vector2> { Vector2.zero, Vector2.up, Vector2.right };
            var triangles = new List<int> { 0, 1, 2 };
            int sourceVertexCount = source.Vertices.Count;
            source.AppendTo(vertices, normals, uv, triangles);
            Assert.That(vertices.Count, Is.EqualTo(sourceVertexCount + 3));
            Assert.That(normals.Count, Is.EqualTo(vertices.Count));
            Assert.That(uv.Count, Is.EqualTo(vertices.Count));
            Assert.That(triangles.Count, Is.EqualTo(source.Triangles.Count + 3));
            Assert.That(vertices[0], Is.EqualTo(Vector3.left));
            Assert.That(triangles[2], Is.EqualTo(2));
            for (int index = 0; index < source.Triangles.Count; index++)
            {
                Assert.That(triangles[index + 3], Is.EqualTo(source.Triangles[index] + 3));
            }
            source.AppendTo(vertices, normals, uv, triangles);
            Assert.That(vertices.Count, Is.EqualTo(sourceVertexCount * 2 + 3));
            Assert.That(source.Vertices.Count, Is.EqualTo(sourceVertexCount));
            Assert.That(triangles[source.Triangles.Count + 3], Is.EqualTo(sourceVertexCount + 3));
        }

        [TestCase(0f, 3f)]
        [TestCase(-1f, 3f)]
        [TestCase(float.NaN, 3f)]
        [TestCase(float.PositiveInfinity, 3f)]
        [TestCase(7f, 0f)]
        [TestCase(7f, -1f)]
        [TestCase(7f, float.NaN)]
        [TestCase(7f, float.PositiveInfinity)]
        public void Tree_RejectsInvalidDimensionsWithoutAdvancingSeed(float height, float spread)
        {
            uint seed = 123u;
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                SunbreakCanopyGeometry.Build(Vector3.zero, height, spread, ref seed));
            Assert.That(seed, Is.EqualTo(123u));
        }

        [Test]
        public void Tree_RejectsNonFiniteOriginWithoutAdvancingSeed()
        {
            uint seed = 123u;
            Assert.Throws<ArgumentOutOfRangeException>(() => SunbreakCanopyGeometry.Build(
                new Vector3(float.NaN, 0f, 0f), 7f, 3f, ref seed));
            Assert.That(seed, Is.EqualTo(123u));
        }

        private static void AssertEqual(SunbreakCanopyGeometry.GeometryBuffer a,
            SunbreakCanopyGeometry.GeometryBuffer b)
        {
            CollectionAssert.AreEqual(a.Vertices, b.Vertices);
            CollectionAssert.AreEqual(a.Normals, b.Normals);
            CollectionAssert.AreEqual(a.Uv, b.Uv);
            CollectionAssert.AreEqual(a.Triangles, b.Triangles);
        }

        private static void AssertBuffer(SunbreakCanopyGeometry.GeometryBuffer buffer)
        {
            Assert.That(buffer.Vertices.Count, Is.GreaterThan(0));
            Assert.That(buffer.Normals.Count, Is.EqualTo(buffer.Vertices.Count));
            Assert.That(buffer.Uv.Count, Is.EqualTo(buffer.Vertices.Count));
            Assert.That(buffer.Triangles.Count, Is.EqualTo(buffer.Vertices.Count));
            for (int vertex = 0; vertex < buffer.Vertices.Count; vertex++)
            {
                Vector3 point = buffer.Vertices[vertex];
                Vector3 normal = buffer.Normals[vertex];
                Assert.That(Finite(point.x) && Finite(point.y) && Finite(point.z), Is.True);
                Assert.That(Finite(buffer.Uv[vertex].x) && Finite(buffer.Uv[vertex].y), Is.True);
                Assert.That(normal.sqrMagnitude, Is.EqualTo(1f).Within(0.001f));
                Assert.That(buffer.Triangles[vertex], Is.EqualTo(vertex));
            }
            for (int index = 0; index < buffer.Vertices.Count; index += 3)
            {
                Vector3 edgeA = buffer.Vertices[index + 1] - buffer.Vertices[index];
                Vector3 edgeB = buffer.Vertices[index + 2] - buffer.Vertices[index];
                Assert.That(Vector3.Cross(edgeA, edgeB).sqrMagnitude, Is.GreaterThan(1e-16f),
                    "No collapsed triangle should depend on a zero normal or invalid tangent fallback.");
            }
        }

        private static void AssertLeaves(SunbreakCanopyGeometry.GeometryBuffer buffer)
        {
            Assert.That(buffer.Vertices.Count % 12, Is.Zero);
            for (int leaf = 0; leaf < buffer.Vertices.Count; leaf += 12)
            {
                Assert.That(Vector3.Distance(buffer.Vertices[leaf], buffer.Vertices[leaf + 2]),
                    Is.InRange(0.2199f, SunbreakCanopyGeometry.MaximumLeafLength + 0.0001f));
                Assert.That(buffer.Uv[leaf], Is.EqualTo(new Vector2(0.5f, 0f)));
                Assert.That(buffer.Uv[leaf + 1], Is.EqualTo(new Vector2(0f, 0.45f)));
                Assert.That(buffer.Uv[leaf + 2], Is.EqualTo(new Vector2(0.5f, 1f)));
                Assert.That(buffer.Uv[leaf + 5], Is.EqualTo(new Vector2(1f, 0.45f)));
                Assert.That(buffer.Vertices[leaf], Is.EqualTo(buffer.Vertices[leaf + 8]));
                Assert.That(buffer.Vertices[leaf + 1], Is.EqualTo(buffer.Vertices[leaf + 7]));
                Assert.That(buffer.Vertices[leaf + 2], Is.EqualTo(buffer.Vertices[leaf + 6]));
                Assert.That(Vector3.Dot(buffer.Normals[leaf], buffer.Normals[leaf + 6]),
                    Is.EqualTo(-1f).Within(0.0001f));
            }
        }

        private static Bounds BoundsOf(List<Vector3> vertices)
        {
            var result = new Bounds(vertices[0], Vector3.zero);
            foreach (Vector3 point in vertices) { result.Encapsulate(point); }
            return result;
        }

        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
