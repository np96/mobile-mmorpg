using System;
using System.Collections.Generic;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakRuinGroundAuthoringTests
    {
        [Test]
        public void Geometry_ReproducesEveryBufferWithoutConsumingUnityRandom()
        {
            UnityEngine.Random.State original = UnityEngine.Random.state;
            Mesh first = SunbreakRuinGroundAuthoring.CreateMesh();
            Mesh second = SunbreakRuinGroundAuthoring.CreateMesh();
            try
            {
                Assert.That(UnityEngine.Random.state, Is.EqualTo(original));
                CollectionAssert.AreEqual(first.vertices, second.vertices);
                CollectionAssert.AreEqual(first.normals, second.normals);
                CollectionAssert.AreEqual(first.tangents, second.tangents);
                CollectionAssert.AreEqual(first.uv, second.uv);
                CollectionAssert.AreEqual(first.triangles, second.triangles);
                Assert.That(AssetDatabase.GetAssetPath(first), Is.Empty,
                    "Pure geometry tests may not publish or rewrite generated assets.");
            }
            finally { Object.DestroyImmediate(first); Object.DestroyImmediate(second); }
        }

        [Test]
        public void Geometry_KeepsShallowCompleteFragmentsInsideTheFocalChunk()
        {
            Mesh mesh = SunbreakRuinGroundAuthoring.CreateMesh();
            try
            {
                Assert.That(mesh.triangles.Length / 3, Is.InRange(600, 3000));
                Assert.That(mesh.subMeshCount, Is.EqualTo(1));
                foreach (Vector3 local in mesh.vertices)
                {
                    Vector3 world = local + SunbreakRuinGroundAuthoring.ChunkOrigin;
                    Assert.That(world.x, Is.InRange(-54f, -24f));
                    Assert.That(world.z, Is.InRange(1f, 23f));
                    Assert.That(world.y, Is.InRange(0.005f, 0.012f));
                    Assert.That(local.x, Is.InRange(0f, 64f));
                    Assert.That(local.z, Is.InRange(0f, 64f));
                    AssertClearOfCover(world);
                }
                Vector3[] vertices = mesh.vertices;
                int[] indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = vertices[indices[i]] + SunbreakRuinGroundAuthoring.ChunkOrigin;
                    Vector3 b = vertices[indices[i + 1]] + SunbreakRuinGroundAuthoring.ChunkOrigin;
                    Vector3 c = vertices[indices[i + 2]] + SunbreakRuinGroundAuthoring.ChunkOrigin;
                    AssertClearOfCover((a + b + c) / 3f);
                    AssertClearOfCover((a + b) * 0.5f);
                    AssertClearOfCover((b + c) * 0.5f);
                    AssertClearOfCover((c + a) * 0.5f);
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Geometry_HasFiniteOrthonormalFramesAndNonDegenerateTriangles()
        {
            Mesh mesh = SunbreakRuinGroundAuthoring.CreateMesh();
            try
            {
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                Vector4[] tangents = mesh.tangents;
                Vector2[] uv = mesh.uv;
                Assert.That(normals.Length, Is.EqualTo(vertices.Length));
                Assert.That(tangents.Length, Is.EqualTo(vertices.Length));
                Assert.That(uv.Length, Is.EqualTo(vertices.Length));
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                    AssertFinite(vertices[i].x); AssertFinite(vertices[i].y); AssertFinite(vertices[i].z);
                    AssertFinite(normals[i].x); AssertFinite(normals[i].y); AssertFinite(normals[i].z);
                    AssertFinite(tangents[i].x); AssertFinite(tangents[i].y); AssertFinite(tangents[i].z);
                    AssertFinite(uv[i].x); AssertFinite(uv[i].y);
                    Assert.That(normals[i].magnitude, Is.EqualTo(1f).Within(0.0002f));
                    Assert.That(tangent.magnitude, Is.EqualTo(1f).Within(0.0002f));
                    Assert.That(Vector3.Dot(normals[i], tangent), Is.EqualTo(0f).Within(0.0002f));
                    Assert.That(Mathf.Abs(tangents[i].w), Is.EqualTo(1f));
                }
                int[] indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                    Vector3 cross = Vector3.Cross(b - a, c - a);
                    Assert.That(cross.sqrMagnitude, Is.GreaterThan(0.0000000001f));
                    Assert.That(Vector3.Dot(cross.normalized, normals[indices[i]]), Is.GreaterThan(0.9999f));
                    Assert.That(normals[indices[i]].y, Is.GreaterThanOrEqualTo(-0.002f),
                        "Shallow sidewalls face outward; beveled and top surfaces must not be inverted.");
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Geometry_UsesPhysicalWorldCoordinatesForEveryTexturePlane()
        {
            Mesh mesh = SunbreakRuinGroundAuthoring.CreateMesh();
            try
            {
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                Vector2[] uv = mesh.uv;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 world = vertices[i] + SunbreakRuinGroundAuthoring.ChunkOrigin;
                    Vector3 normal = normals[i];
                    Vector2 expected = Mathf.Abs(normal.y) > 0.5f ? new Vector2(world.x, world.z) :
                        Mathf.Abs(normal.x) > Mathf.Abs(normal.z) ? new Vector2(world.z, world.y) :
                        new Vector2(world.x, world.y);
                    expected *= SunbreakRuinGroundAuthoring.TextureScale;
                    Assert.That(Vector2.Distance(uv[i], expected), Is.LessThan(0.00001f),
                        "Each stone must not reset or stretch a whole texture into its own UV square.");
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Geometry_HasBroadTwoDimensionalSurvivalAndOpenEarthPockets()
        {
            Mesh mesh = SunbreakRuinGroundAuthoring.CreateMesh();
            try
            {
                Assert.That(mesh.bounds.size.x, Is.GreaterThan(23f));
                Assert.That(mesh.bounds.size.z, Is.GreaterThan(15f));
                Assert.That(mesh.bounds.size.x / mesh.bounds.size.z, Is.InRange(1f, 1.9f),
                    "Surviving court is broad in both dimensions rather than a paving strip.");
                Vector3[] vertices = mesh.vertices, normals = mesh.normals;
                int[] indices = mesh.triangles;
                float upwardArea = 0f;
                var quadrants = new HashSet<int>();
                var topHeights = new HashSet<int>();
                for (int i = 0; i < indices.Length; i += 3)
                {
                    if (normals[indices[i]].y < 0.999f) { continue; }
                    Vector3 a = vertices[indices[i]] + SunbreakRuinGroundAuthoring.ChunkOrigin;
                    Vector3 b = vertices[indices[i + 1]] + SunbreakRuinGroundAuthoring.ChunkOrigin;
                    Vector3 c = vertices[indices[i + 2]] + SunbreakRuinGroundAuthoring.ChunkOrigin;
                    upwardArea += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
                    Vector3 center = (a + b + c) / 3f;
                    quadrants.Add((center.x > -39f ? 1 : 0) + (center.z > 12f ? 2 : 0));
                    topHeights.Add(Mathf.RoundToInt(center.y * 10000f));
                    Assert.That(Contains(new Vector2(-41.8f, 10.7f), a, b, c), Is.False,
                        "The central missing-cell soil pocket must remain open.");
                }
                Assert.That(upwardArea, Is.InRange(50f, 110f),
                    "Small surviving fragments must leave exposed earth dominant, not recreate the rejected tiled mat.");
                Assert.That(quadrants.Count, Is.EqualTo(4));
                Assert.That(topHeights.Count, Is.GreaterThan(12));
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void Footprints_StayHumanScaleWithErodedSoilJointsAndPositiveWinding()
        {
            List<Vector2[]> footprints = SunbreakRuinGroundAuthoring.CreateFootprints();
            List<Vector2[]> repeated = SunbreakRuinGroundAuthoring.CreateFootprints();
            Assert.That(footprints.Count, Is.InRange(80, 250));
            Assert.That(repeated.Count, Is.EqualTo(footprints.Count));
            var areas = new List<float>();
            for (int i = 0; i < footprints.Count; i++)
            {
                Vector2[] polygon = footprints[i];
                CollectionAssert.AreEqual(polygon, repeated[i]);
                float signedArea = 0f;
                for (int edge = 0; edge < polygon.Length; edge++)
                {
                    signedArea += Cross(polygon[edge], polygon[(edge + 1) % polygon.Length]);
                    for (int other = edge + 1; other < polygon.Length; other++)
                    {
                        Assert.That(Vector2.Distance(polygon[edge], polygon[other]), Is.LessThan(2.05f),
                            "A narrow fragment cannot evade the human-scale contract through low area alone.");
                    }
                }
                float area = signedArea * 0.5f;
                Assert.That(area, Is.InRange(0.2998f, 1.0002f),
                    "A buried fragment must not become another multi-meter smooth plate; its winding must remain positive.");
                areas.Add(area);
            }
            areas.Sort();
            Assert.That(areas[areas.Count / 2], Is.InRange(0.3f, 0.7f));
        }

        [Test]
        public void Geometry_RemainsBelowTheAuthoredTeamAndTargetRingPlanes()
        {
            Assert.That(SunbreakRuinGroundAuthoring.MaximumHeight, Is.EqualTo(0.012f),
                "Opaque ground must remain below combat ring planes; do not raise rings or change gameplay to accommodate art.");
            GameObject combatant = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SessionRpg/Prefabs/Combatant.prefab");
            Assert.That(combatant, Is.Not.Null);
            Mesh mesh = SunbreakRuinGroundAuthoring.CreateMesh();
            try
            {
                string[] names = { "TeamRing", "TargetRing" };
                float[] authoredHeights = { 0.025f, 0.04f };
                for (int ringIndex = 0; ringIndex < names.Length; ringIndex++)
                {
                    Transform transform = combatant.transform.Find(names[ringIndex]);
                    Assert.That(transform, Is.Not.Null);
                    LineRenderer ring = transform.GetComponent<LineRenderer>();
                    Assert.That(ring.alignment, Is.EqualTo(LineAlignment.TransformZ));
                    Assert.That(ring.useWorldSpace, Is.False);
                    Assert.That(transform.localPosition.y, Is.EqualTo(authoredHeights[ringIndex]).Within(0.000001f),
                        "This contract follows CharacterAuthoring's existing flat ring plane, not a ground-driven ring adjustment.");
                    float minimumRingY = float.MaxValue;
                    for (int point = 0; point < ring.positionCount; point++)
                    {
                        Vector3 local = combatant.transform.InverseTransformPoint(transform.TransformPoint(ring.GetPosition(point)));
                        minimumRingY = Mathf.Min(minimumRingY, local.y);
                    }
                    foreach (Vector3 vertex in mesh.vertices)
                    {
                        Assert.That(vertex.y, Is.LessThanOrEqualTo(SunbreakRuinGroundAuthoring.MaximumHeight));
                        Assert.That(vertex.y + 0.0125f, Is.LessThan(minimumRingY),
                            "Every opaque fragment needs at least 12.5 mm of depth clearance below the lowest existing ring.");
                    }
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void GeneratedCourt_IsOneStaticShadowReceivingBatchWithoutPhysicsOrExtraSystems()
        {
            const string path = "Assets/SessionRpg/Content/Worlds/SunbreakBasin/Chunks/Chunk_-1_0.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
                GameObject court = null;
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (filter.name == SunbreakRuinGroundAuthoring.RootName) { court = filter.gameObject; }
                    }
                }
                Assert.That(court, Is.Not.Null);
                Assert.DoesNotThrow(() => SunbreakRuinGroundAuthoring.Validate(court));
                Assert.That(court.GetComponents<Component>().Length, Is.EqualTo(3));
                Assert.That(court.transform.childCount, Is.EqualTo(0));
                Assert.That(court.layer, Is.EqualTo(2));
                MeshRenderer renderer = court.GetComponent<MeshRenderer>();
                Assert.That(renderer.sharedMaterial, Is.Not.Null);
                Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(renderer.receiveShadows, Is.True);
                Assert.That(GameObjectUtility.AreStaticEditorFlagsSet(court, StaticEditorFlags.BatchingStatic), Is.True);
                Mesh mesh = court.GetComponent<MeshFilter>().sharedMesh;
                Assert.That(AssetDatabase.GetAssetPath(mesh), Is.EqualTo(SunbreakRuinGroundAuthoring.MeshPath));
                Mesh expected = SunbreakRuinGroundAuthoring.CreateMesh();
                try
                {
                    CollectionAssert.AreEqual(expected.vertices, mesh.vertices);
                    CollectionAssert.AreEqual(expected.triangles, mesh.triangles);
                    CollectionAssert.AreEqual(expected.uv, mesh.uv);
                    CollectionAssert.AreEqual(expected.normals, mesh.normals);
                    CollectionAssert.AreEqual(expected.tangents, mesh.tangents);
                }
                finally { Object.DestroyImmediate(expected); }
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) { EditorSceneManager.CloseScene(scene, true); }
            }
        }

        [Test]
        public void Build_RejectsMissingCompositionBeforeWritingAssets()
        {
            Assert.Throws<ArgumentNullException>(() => SunbreakRuinGroundAuthoring.Build(null, null));
            var root = new GameObject("GroundContractTest");
            try { Assert.Throws<ArgumentNullException>(() => SunbreakRuinGroundAuthoring.Build(root.transform, null)); }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Build_RejectsWrongChunkScaleOrRotationBeforeWritingAssets(int mismatch)
        {
            var root = new GameObject("GroundTransformTest");
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            try
            {
                root.transform.position = SunbreakRuinGroundAuthoring.ChunkOrigin;
                if (mismatch == 0) { root.transform.position += Vector3.right; }
                if (mismatch == 1) { root.transform.localScale = new Vector3(1.1f, 1f, 1f); }
                if (mismatch == 2) { root.transform.rotation = Quaternion.Euler(0f, 5f, 0f); }
                Assert.Throws<InvalidOperationException>(() => SunbreakRuinGroundAuthoring.Build(root.transform, material));
                Assert.That(root.transform.childCount, Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(material); }
        }

        private static void AssertClearOfCover(Vector3 point)
        {
            for (int i = 0; i < PveRegionLayout.ObstacleCount; i++)
            {
                ArenaCircleObstacle circle = PveRegionLayout.GetObstacle(i);
                float x = point.x - circle.Center.X, z = point.z - circle.Center.Z;
                Assert.That(x * x + z * z, Is.GreaterThan((circle.Radius + 0.11f) * (circle.Radius + 0.11f)),
                    "The entire shallow court fragment must avoid the vertical cover footprint.");
            }
        }

        private static bool Contains(Vector2 point, Vector3 a, Vector3 b, Vector3 c)
        {
            float ab = Cross(new Vector2(b.x - a.x, b.z - a.z), point - new Vector2(a.x, a.z));
            float bc = Cross(new Vector2(c.x - b.x, c.z - b.z), point - new Vector2(b.x, b.z));
            float ca = Cross(new Vector2(a.x - c.x, a.z - c.z), point - new Vector2(c.x, c.z));
            return (ab >= 0f && bc >= 0f && ca >= 0f) || (ab <= 0f && bc <= 0f && ca <= 0f);
        }

        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
        private static void AssertFinite(float value) => Assert.That(float.IsNaN(value) || float.IsInfinity(value), Is.False);
    }
}
