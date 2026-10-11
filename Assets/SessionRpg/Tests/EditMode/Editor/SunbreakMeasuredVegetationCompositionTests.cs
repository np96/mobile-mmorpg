using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Unity.World;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakMeasuredVegetationCompositionTests
    {
        [Test]
        public void FocalField_ReplacesScatteredHexagonsAndDistantGrassWithTheRuinCourt()
        {
            const string path = "Assets/SessionRpg/Content/Worlds/SunbreakBasin/Chunks/Chunk_-1_0.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
                Transform visual = scene.GetRootGameObjects()[0].GetComponent<WorldChunkPresentation>().VisualRoot;
                Assert.That(visual.Find("MeadowDetail/EmbeddedStonePatches"), Is.Null);
                Assert.That(visual.Find("MeadowDetail/BentMeadowGrass"), Is.Null);
                Assert.That(visual.Find("MeadowDetail/GroundRosettes"), Is.Null);
                Assert.That(visual.Find("MeadowDetail/ScatteredSeedFlowers"), Is.Null);
                // Dedicated ruin-ground tests own the new fractured surface's
                // strict topology, height, source UV and draw-count contracts.
                Assert.That(visual.Find("MeadowDetail").GetComponentsInChildren<MeshFilter>().Length,
                    Is.InRange(2, 3));
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) { EditorSceneManager.CloseScene(scene, true); }
            }
        }

        [TestCase(-1)]
        [TestCase(0)]
        [TestCase(7)]
        [TestCase(2)]
        [TestCase(9)]
        public void FocalPlants_RetainSourceNormalsAndDoNotBridgeWalkableSpace(int landmark)
        {
            string path = "Assets/SessionRpg/Content/Worlds/SunbreakBasin/Chunks/Chunk_" +
                (landmark == 2 || landmark == 9 ? "0_0" : "-1_0") + ".unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
                Transform visual = scene.GetRootGameObjects()[0].GetComponent<WorldChunkPresentation>().VisualRoot;
                Transform owner = visual.Find(landmark < 0 ? "MeadowDetail" : "BlockerIslands/Blocker_" + landmark);
                int count = 0;
                int triangles = 0;
                foreach (MeshFilter filter in owner.GetComponentsInChildren<MeshFilter>())
                {
                    if (!filter.name.StartsWith("Measured")) { continue; }
                    if (landmark >= 0 && !filter.name.StartsWith("MeasuredVegetation")) { continue; }
                    count++;
                    Mesh mesh = filter.sharedMesh;
                    Vector3[] vertices = mesh.vertices;
                    Assert.That(mesh.normals.Length, Is.EqualTo(vertices.Length));
                    Assert.That(mesh.uv.Length, Is.EqualTo(vertices.Length));
                    Assert.That(mesh.tangents.Length, Is.EqualTo(vertices.Length));
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        vertices[i] = filter.transform.TransformPoint(vertices[i]);
                        CheckPoint(vertices[i], landmark);
                    }
                    int[] indices = mesh.triangles;
                    triangles += indices.Length / 3;
                    for (int i = 0; i < indices.Length; i += 3)
                    {
                        Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                        CheckPoint((a + b + c) / 3f, landmark);
                        CheckPoint((a + b) * 0.5f, landmark);
                        CheckPoint((b + c) * 0.5f, landmark);
                        CheckPoint((c + a) * 0.5f, landmark);
                    }
                    Assert.That(filter.GetComponent<MeshRenderer>().sharedMaterial, Is.Not.Null);
                    Assert.That(filter.GetComponentsInChildren<Collider>(), Is.Empty);
                    Assert.That(filter.GetComponentsInChildren<Rigidbody>(), Is.Empty);
                }
                Assert.That(count, Is.InRange(landmark < 0 ? 1 : 3, 5));
                if (landmark < 0) { Assert.That(triangles, Is.InRange(5800, 5862)); }
                else
                {
                    int expected = landmark == 7 ? 22576 : landmark == 2 ? 31120 : landmark == 9 ? 12464 : 13248;
                    Assert.That(triangles, Is.EqualTo(expected));
                }
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) { EditorSceneManager.CloseScene(scene, true); }
            }
        }

        [Test]
        public void MeasuredMeadow_FormsSeveralLowColoniesAroundAnOpenCombatCentre()
        {
            const string path = "Assets/SessionRpg/Content/Worlds/SunbreakBasin/Chunks/Chunk_-1_0.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
                Transform visual = scene.GetRootGameObjects()[0].GetComponent<WorldChunkPresentation>().VisualRoot;
                MeshFilter plants = visual.Find("MeadowDetail/MeasuredMeadow_0").GetComponent<MeshFilter>();
                Float3 camp = PveRegionLayout.GetCampCenter(1);
                bool west = false, east = false, north = false, south = false;
                foreach (Vector3 local in plants.sharedMesh.vertices)
                {
                    Vector3 world = plants.transform.TransformPoint(local);
                    float x = world.x - camp.X, z = world.z - camp.Z;
                    Assert.That(x * x + z * z, Is.GreaterThan(5f * 5f),
                        "Keep the immediate fighting area open, not a uniformly scattered lawn.");
                    Assert.That(world.y, Is.InRange(0f, 0.181f));
                    west |= x < -10f;
                    east |= x > 4f;
                    north |= z > 7f;
                    south |= z < -7f;
                }
                Assert.That(west && east && north && south, Is.True,
                    "Measured cover must frame several court margins, not accumulate in one tiny patch.");
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) { EditorSceneManager.CloseScene(scene, true); }
            }
        }

        private static void CheckPoint(Vector3 point, int landmark)
        {
            Assert.That(float.IsNaN(point.x) || float.IsNaN(point.y) || float.IsNaN(point.z) ||
                float.IsInfinity(point.x) || float.IsInfinity(point.y) || float.IsInfinity(point.z), Is.False);
            if (landmark < 0)
            {
                Assert.That(point.y, Is.InRange(0f, 0.181f));
                return;
            }
            if (point.y <= 0.181f) { return; }
            int start = PveRegionLayout.GetLandmarkObstacleStart(landmark);
            int count = PveRegionLayout.GetLandmarkObstacleCount(landmark);
            bool inside = false;
            for (int i = start; i < start + count; i++)
            {
                ArenaCircleObstacle circle = PveRegionLayout.GetObstacle(i);
                float x = point.x - circle.Center.X, z = point.z - circle.Center.Z;
                inside |= x * x + z * z <= (circle.Radius + 0.005f) * (circle.Radius + 0.005f);
            }
            Assert.That(inside, Is.True, "Source foliage must fit exact cover without squeezing its mesh: " + point);
        }
    }
}
