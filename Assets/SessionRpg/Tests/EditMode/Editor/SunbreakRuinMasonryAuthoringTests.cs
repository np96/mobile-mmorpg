using System;
using System.IO;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakRuinMasonryAuthoringTests
    {
        [Test]
        public void FractureProfile_HasACentralCollapseBetweenBroadSurvivingMasses()
        {
            float nearMass = SunbreakRuinMasonryAuthoring.FractureHeight(23.3f);
            float collapsed = SunbreakRuinMasonryAuthoring.FractureHeight(24.3f);
            Assert.That(nearMass, Is.GreaterThan(2.4f));
            Assert.That(collapsed, Is.InRange(1.32f, 1.5f));
            Assert.That(nearMass - collapsed, Is.GreaterThan(1f),
                "Avoid a monotonic diagonal saw-stair made from repeated small teeth.");
            Assert.That(SunbreakRuinMasonryAuthoring.FractureHeight(24.8f), Is.LessThan(1.5f));
            Assert.That(SunbreakRuinMasonryAuthoring.FractureHeight(25.6f), Is.EqualTo(3.36f));
            Assert.That(SunbreakRuinMasonryAuthoring.FractureHeight(SunbreakRuinMasonryAuthoring.JointZ), Is.EqualTo(3.36f));
        }

        [Test]
        public void ModifiedMasonry_ReplacesIntactParapetsWithinTheOriginalPairBudget()
        {
            using (SunbreakRuinMasonryAuthoring.Geometry geometry = SunbreakRuinMasonryAuthoring.CreateGeometry())
            {
                Assert.That(geometry.TriangleCount, Is.InRange(100, 1276));
                Assert.That(geometry.CapLoops, Is.GreaterThanOrEqualTo(20));
                Assert.That(geometry.CapTriangles, Is.GreaterThan(30));
                Assert.That(geometry.Rubble.vertexCount, Is.GreaterThan(36));
                foreach (Mesh mesh in new[] { geometry.Wall, geometry.Corner })
                {
                    Assert.That(mesh.bounds.max.y, Is.LessThanOrEqualTo(3.361f));
                    foreach (Vector3 point in mesh.vertices)
                    {
                        Assert.That(point.y, Is.GreaterThanOrEqualTo(0.049f));
                        Assert.That(point.y, Is.LessThanOrEqualTo(SunbreakRuinMasonryAuthoring.FractureHeight(point.z) + 0.001f));
                    }
                }
                Assert.That(geometry.Wall.bounds.size.y, Is.GreaterThan(3f));
            }
        }

        [Test]
        public void EveryTriangle_RetainsFiniteUvOrthonormalFramesAndExactCoreContainment()
        {
            using (SunbreakRuinMasonryAuthoring.Geometry geometry = SunbreakRuinMasonryAuthoring.CreateGeometry())
            {
                foreach (Mesh mesh in new[] { geometry.Wall, geometry.Corner, geometry.Rubble })
                {
                    Vector3[] points = mesh.vertices, normals = mesh.normals;
                    Vector4[] tangents = mesh.tangents; Vector2[] uv = mesh.uv; int[] indices = mesh.triangles;
                    Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                    Assert.That(normals.Length, Is.EqualTo(points.Length));
                    Assert.That(tangents.Length, Is.EqualTo(points.Length));
                    Assert.That(uv.Length, Is.EqualTo(points.Length));
                    for (int i = 0; i < points.Length; i++)
                    {
                        Assert.That(Finite(points[i].x) && Finite(points[i].y) && Finite(points[i].z) && Finite(uv[i].x) && Finite(uv[i].y), Is.True);
                        Assert.That(normals[i].sqrMagnitude, Is.EqualTo(1f).Within(0.002f));
                        var tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                        Assert.That(tangent.sqrMagnitude, Is.EqualTo(1f).Within(0.002f));
                        Assert.That(Mathf.Abs(Vector3.Dot(normals[i], tangent)), Is.LessThan(0.002f));
                        Assert.That(Mathf.Abs(tangents[i].w), Is.EqualTo(1f));
                        AssertInside(points[i]);
                    }
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 a = points[indices[i]], b = points[indices[i + 1]], c = points[indices[i + 2]];
                        Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(0.00000000001f));
                        AssertInside((a + b + c) / 3f); AssertInside((a + b) * 0.5f);
                        AssertInside((b + c) * 0.5f); AssertInside((c + a) * 0.5f);
                    }
                }
            }
        }

        [TestCase(22.55f)]
        [TestCase(22.95f)]
        [TestCase(23.5f)]
        [TestCase(24.3f)]
        [TestCase(24.8f)]
        [TestCase(25.4f)]
        [TestCase(25.9f)]
        [TestCase(26.2f)]
        [TestCase(26.6f)]
        public void FractureRoof_HasARealCappedSurfaceAtEverySampledRiseAndNotch(float z)
        {
            using (SunbreakRuinMasonryAuthoring.Geometry geometry = SunbreakRuinMasonryAuthoring.CreateGeometry())
            {
                Mesh mesh = z < SunbreakRuinMasonryAuthoring.JointZ ? geometry.Wall : geometry.Corner;
                var origin = new Vector3(-43.4f, 5f, z);
                Assert.That(Ray(mesh, origin, Vector3.down, out float distance), Is.True);
                Assert.That(origin.y - distance, Is.EqualTo(SunbreakRuinMasonryAuthoring.FractureHeight(z)).Within(0.003f),
                    "The cut must be capped, not an open shell whose far face or ground happens to be visible.");
            }
        }

        [Test]
        public void PerpendicularCornerReturn_HasItsOwnClosedFractureSurface()
        {
            using (SunbreakRuinMasonryAuthoring.Geometry geometry = SunbreakRuinMasonryAuthoring.CreateGeometry())
            {
                var origin = new Vector3(-42.1f, 5f, 27.9f);
                Assert.That(Ray(geometry.Corner, origin, Vector3.down, out float distance), Is.True);
                Assert.That(origin.y - distance, Is.EqualTo(SunbreakRuinMasonryAuthoring.FractureHeight(origin.z)).Within(0.003f));
                Assert.That(Ray(geometry.Wall, origin, Vector3.down, out _), Is.False);
            }
        }

        [TestCase(-43.15f, 21.15f)]
        [TestCase(-44.8f, 22.7f)]
        [TestCase(-45.75f, 24.1f)]
        public void GroundedOffcuts_AreClosedVolumesRatherThanFloatingSourceFaceCards(float x, float z)
        {
            using (SunbreakRuinMasonryAuthoring.Geometry geometry = SunbreakRuinMasonryAuthoring.CreateGeometry())
            {
                Mesh mesh = geometry.Rubble;
                Assert.That(Ray(mesh, new Vector3(x, 5f, z), Vector3.down, out float down), Is.True);
                Assert.That(Ray(mesh, new Vector3(x, -2f, z), Vector3.up, out float up), Is.True);
                float highest = 5f - down, lowest = -2f + up;
                Assert.That(highest - lowest, Is.GreaterThan(0.1f));
                var center = new Vector3(x, (highest + lowest) * 0.5f, z);
                foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back, Vector3.up, Vector3.down })
                {
                    Assert.That(Ray(mesh, center, direction, out float distance), Is.True, "Offcut has an open cut face: " + direction);
                    Assert.That(distance, Is.InRange(0.001f, 1.5f));
                }
            }
        }

        [Test]
        public void GroundSampler_MovesOnlyRubbleAndEmbedsItsLowestPoints()
        {
            using (SunbreakRuinMasonryAuthoring.Geometry baseline = SunbreakRuinMasonryAuthoring.CreateGeometry())
            using (SunbreakRuinMasonryAuthoring.Geometry raised = SunbreakRuinMasonryAuthoring.CreateGeometry(_ => 0.35f))
            {
                Assert.That(raised.Wall.vertices, Is.EqualTo(baseline.Wall.vertices));
                Assert.That(raised.Corner.vertices, Is.EqualTo(baseline.Corner.vertices));
                Vector3[] a = baseline.Rubble.vertices, b = raised.Rubble.vertices;
                Assert.That(a.Length, Is.EqualTo(b.Length));
                for (int i = 0; i < a.Length; i++) { Assert.That(Vector3.Distance(b[i], a[i] + Vector3.up * 0.27f), Is.LessThan(0.00001f)); }
                Assert.That(baseline.Rubble.bounds.min.y, Is.EqualTo(0.025f).Within(0.00001f));
                Assert.That(raised.Rubble.bounds.min.y, Is.EqualTo(0.295f).Within(0.00001f));
            }
        }

        [Test]
        public void Derivation_IsDeterministicAndPreservesAllOriginalGeometryAndMaterialBytes()
        {
            string[] paths = { SunbreakFortAssets.ModelPath, SunbreakFortAssets.WallMaterialPath,
                AssetDatabase.GetAssetPath(SunbreakFortAssets.RequireRenderMesh(SunbreakFortAssets.Module.BattlementWall)),
                AssetDatabase.GetAssetPath(SunbreakFortAssets.RequireRenderMesh(SunbreakFortAssets.Module.ThinCorner)) };
            var before = new byte[paths.Length][];
            for (int i = 0; i < paths.Length; i++) { before[i] = File.ReadAllBytes(paths[i]); }
            using (SunbreakRuinMasonryAuthoring.Geometry a = SunbreakRuinMasonryAuthoring.CreateGeometry())
            using (SunbreakRuinMasonryAuthoring.Geometry b = SunbreakRuinMasonryAuthoring.CreateGeometry())
            {
                Assert.That(a.Wall.vertices, Is.EqualTo(b.Wall.vertices));
                Assert.That(a.Corner.vertices, Is.EqualTo(b.Corner.vertices));
                Assert.That(a.Rubble.vertices, Is.EqualTo(b.Rubble.vertices));
                Assert.That(a.CapTriangles, Is.EqualTo(b.CapTriangles));
            }
            for (int i = 0; i < paths.Length; i++) { Assert.That(File.ReadAllBytes(paths[i]), Is.EqualTo(before[i])); }
        }

        private static bool Ray(Mesh mesh, Vector3 origin, Vector3 direction, out float nearest)
        {
            nearest = float.PositiveInfinity;
            Vector3[] vertices = mesh.vertices; int[] indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], first = vertices[indices[i + 1]] - a, second = vertices[indices[i + 2]] - a;
                Vector3 p = Vector3.Cross(direction, second); float determinant = Vector3.Dot(first, p);
                if (Mathf.Abs(determinant) < 0.0000001f) { continue; }
                float inverse = 1f / determinant; Vector3 t = origin - a;
                float u = Vector3.Dot(t, p) * inverse;
                if (u < -0.00001f || u > 1.00001f) { continue; }
                Vector3 q = Vector3.Cross(t, first); float v = Vector3.Dot(direction, q) * inverse;
                if (v < -0.00001f || u + v > 1.00001f) { continue; }
                float distance = Vector3.Dot(second, q) * inverse;
                if (distance > 0.00001f) { nearest = Mathf.Min(nearest, distance); }
            }
            return !float.IsPositiveInfinity(nearest);
        }
        private static void AssertInside(Vector3 point)
        {
            bool inside = false;
            for (int i = PveRegionLayout.GetLandmarkObstacleStart(7); i < PveRegionLayout.GetLandmarkObstacleStart(7) + 3; i++)
            {
                ArenaCircleObstacle circle = PveRegionLayout.GetObstacle(i);
                float x = point.x - circle.Center.X, z = point.z - circle.Center.Z;
                inside |= x * x + z * z <= circle.Radius * circle.Radius + 0.0001f;
            }
            Assert.That(inside, Is.True, "Masonry must not consume walkable Core space: " + point);
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
