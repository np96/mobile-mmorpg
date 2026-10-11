using System;
using System.IO;
using NUnit.Framework;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakCanopyAuthoringTests
    {
        private Mesh candidate;
        private Mesh source;

        [SetUp]
        public void SetUp()
        {
            source = SunbreakVegetationAssets.GetParts("TreeSmall02")[1].Mesh;
            candidate = SunbreakCanopyAuthoring.CreateFoliageMesh(source);
        }

        [TearDown]
        public void TearDown() { Object.DestroyImmediate(candidate); }

        [Test]
        public void Candidate_RetainsOriginalTreeBudgetAndOneFoliageMaterial()
        {
            Assert.That(candidate.subMeshCount, Is.EqualTo(1));
            Assert.That(candidate.GetTopology(0), Is.EqualTo(MeshTopology.Triangles));
            Assert.That(candidate.indexFormat, Is.EqualTo(IndexFormat.UInt16));
            Assert.That(candidate.GetIndexCount(0), Is.EqualTo(6328 * 3));
            Assert.That(candidate.vertexCount, Is.EqualTo(6328 * 3));
            Assert.That(SunbreakCanopyAuthoring.TreeTriangles, Is.EqualTo(9328));
        }

        [Test]
        public void EveryCorner_RetainsMeasuredUvNormalAndTangentFrame()
        {
            int[] indices = source.triangles;
            Vector3[] normals = candidate.normals, sourceNormals = source.normals;
            Vector4[] tangents = candidate.tangents, sourceTangents = source.tangents;
            Vector2[] uv = candidate.uv, sourceUv = source.uv;
            for (int i = 0; i < indices.Length; i++)
            {
                Assert.That(normals[i], Is.EqualTo(sourceNormals[indices[i]]));
                Assert.That(tangents[i], Is.EqualTo(sourceTangents[indices[i]]));
                Assert.That(uv[i], Is.EqualTo(sourceUv[indices[i]]));
                var tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                Assert.That(tangent.sqrMagnitude, Is.EqualTo(1f).Within(0.003f));
                Assert.That(normals[i].sqrMagnitude, Is.EqualTo(1f).Within(0.003f));
                Assert.That(Mathf.Abs(Vector3.Dot(normals[i], tangent)), Is.LessThan(0.003f));
                Assert.That(Mathf.Abs(tangents[i].w), Is.EqualTo(1f));
            }
        }

        [Test]
        public void EveryCluster_IsUniformlyExpandedAtTheOriginalClusterCenter()
        {
            int[] indices = source.triangles;
            Vector3[] original = source.vertices, vertices = candidate.vertices;
            float scaleSum = 0f;
            int expanded = 0;
            for (int start = 0; start < indices.Length; start += 12)
            {
                Vector3 center = (original[indices[start]] + original[indices[start + 1]]) * 0.5f;
                Vector3 baseline = original[indices[start]] - center;
                float scale = (vertices[start] - center).magnitude / baseline.magnitude;
                Assert.That(scale, Is.InRange(0.99999f, SunbreakCanopyAuthoring.MaximumClusterScale + 0.00001f));
                for (int i = start; i < start + 12; i++)
                {
                    Vector3 expected = center + (original[indices[i]] - center) * scale;
                    Assert.That(Vector3.Distance(vertices[i], expected), Is.LessThan(0.000005f));
                }
                if (scale > 1.5f) { expanded++; }
                scaleSum += scale;
            }
            Assert.That(scaleSum / (indices.Length / 12), Is.GreaterThan(1.8f));
            Assert.That(expanded, Is.GreaterThan(indices.Length / 12 * 0.9f));
        }

        [Test]
        public void ExpandedClusters_StayWithinSourceHeightAndProjectedEnvelope()
        {
            Vector3[] sourcePoints = source.vertices, points = candidate.vertices;
            Bounds bounds = source.bounds;
            foreach (Vector3 point in points)
            {
                Assert.That(point.y, Is.InRange(bounds.min.y - 0.000002f, bounds.max.y + 0.000002f));
                Assert.That(!float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z) &&
                    !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z), Is.True);
            }
            // A dense set of support planes checks the original convex envelope
            // without copying the implementation's convex-hull algorithm.
            for (int sample = 0; sample < 720; sample++)
            {
                float angle = sample * Mathf.PI / 360f;
                var normal = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float sourceLimit = float.NegativeInfinity;
                foreach (Vector3 point in sourcePoints)
                {
                    sourceLimit = Mathf.Max(sourceLimit, normal.x * point.x + normal.y * point.z);
                }
                float candidateLimit = float.NegativeInfinity;
                foreach (Vector3 point in points) { candidateLimit = Mathf.Max(candidateLimit, normal.x * point.x + normal.y * point.z); }
                Assert.That(candidateLimit, Is.LessThanOrEqualTo(sourceLimit + 0.000003f));
            }
        }

        [Test]
        public void Triangles_RemainPlanarNondegenerateAndRetainSourceWinding()
        {
            int[] sourceIndices = source.triangles;
            Vector3[] before = source.vertices, after = candidate.vertices;
            for (int i = 0; i < sourceIndices.Length; i += 3)
            {
                Vector3 oldNormal = Vector3.Cross(before[sourceIndices[i + 1]] - before[sourceIndices[i]],
                    before[sourceIndices[i + 2]] - before[sourceIndices[i]]);
                Vector3 newNormal = Vector3.Cross(after[i + 1] - after[i], after[i + 2] - after[i]);
                Assert.That(newNormal.sqrMagnitude, Is.GreaterThan(0.000000000001f));
                Assert.That(Vector3.Dot(oldNormal.normalized, newNormal.normalized), Is.GreaterThan(0.99999f));
            }
        }

        [Test]
        public void Generation_IsDeterministicAndDoesNotMutateAdmittedSource()
        {
            string path = AssetDatabase.GetAssetPath(source);
            byte[] before = File.ReadAllBytes(path);
            Mesh repeated = SunbreakCanopyAuthoring.CreateFoliageMesh(source);
            try
            {
                Assert.That(repeated.vertices, Is.EqualTo(candidate.vertices));
                Assert.That(repeated.triangles, Is.EqualTo(candidate.triangles));
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
            }
            finally { Object.DestroyImmediate(repeated); }
        }

        [Test]
        public void GeneratedLookup_RetainsOriginalTrunkAndSharedMeasuredMaterialsWithoutWrites()
        {
            SunbreakVegetationAssets.Part[] original = SunbreakVegetationAssets.GetParts("TreeSmall02");
            byte[] before = File.ReadAllBytes(SunbreakCanopyAuthoring.LeafMeshPath);
            SunbreakVegetationAssets.Part[] parts = SunbreakCanopyAuthoring.GetParts();
            Assert.That(parts, Has.Length.EqualTo(2));
            Assert.That(parts[0].Mesh, Is.SameAs(original[0].Mesh));
            Assert.That(parts[0].Material, Is.SameAs(original[0].Material));
            Assert.That(parts[1].Material, Is.SameAs(original[1].Material));
            Assert.That(parts[1].Mesh.vertices, Is.EqualTo(candidate.vertices));
            Assert.That(parts[1].Mesh.triangles, Is.EqualTo(candidate.triangles));
            Assert.DoesNotThrow(SunbreakCanopyAuthoring.Validate);
            Assert.That(File.ReadAllBytes(SunbreakCanopyAuthoring.LeafMeshPath), Is.EqualTo(before));
        }

        [Test]
        public void MissingOrUnrelatedGeometry_IsRejectedWithoutFallback()
        {
            Assert.Throws<ArgumentNullException>(() => SunbreakCanopyAuthoring.CreateFoliageMesh(null));
            Mesh unrelated = new Mesh();
            try { Assert.Throws<ArgumentException>(() => SunbreakCanopyAuthoring.CreateFoliageMesh(unrelated)); }
            finally { Object.DestroyImmediate(unrelated); }
        }
    }
}
