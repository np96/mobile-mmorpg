using NUnit.Framework;
using System.Reflection;
using SessionRpg.Editor.Authoring;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakRockProjectionTests
    {
        [TestCase(0f, 1f, 0f, 1.2f, 2.8f)]
        [TestCase(1f, 0f, 0f, 2.8f, 2f)]
        [TestCase(0f, 0f, 1f, 1.2f, 2f)]
        [TestCase(-1f, 0f, 0f, 2.8f, 2f)]
        public void RockUvProjection_PreservesVerticalTexelDensity(
            float x, float y, float z, float u, float v)
        {
            MethodInfo project = typeof(SessionRpgAuthoringGenerator).Assembly
                .GetType("SessionRpg.Editor.Authoring.SunbreakBasinAuthoring")
                .GetMethod("ProjectRockSurfaceUv", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(project, Is.Not.Null);
            Vector2 uv = (Vector2)project.Invoke(null, new object[]
                { new Vector3(3f, 5f, 7f), new Vector3(x, y, z) });
            Assert.That(uv.x, Is.EqualTo(u).Within(0.0001f));
            Assert.That(uv.y, Is.EqualTo(v).Within(0.0001f));
        }

        [Test]
        public void GeneratedMajorRockShelves_KeepUvAreaOnSteepTriangles()
        {
            int steepFaces = 0;
            for (int x = -1; x <= 1; x++)
            {
                for (int z = -1; z <= 1; z++)
                {
                    string path = "Assets/SessionRpg/Content/Worlds/SunbreakBasin/Chunks/Chunk_" +
                        x + "_" + z + ".unity";
                    Scene scene = SceneManager.GetSceneByPath(path);
                    bool opened = !scene.IsValid() || !scene.isLoaded;
                    try
                    {
                        if (opened) { scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive); }
                        foreach (GameObject root in scene.GetRootGameObjects())
                        {
                            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                            {
                                if (filter.name != "RockShelf" ||
                                    filter.GetComponent<MeshRenderer>().sharedMaterial.name != "Sunbreak_Rock")
                                {
                                    continue;
                                }
                                Mesh mesh = filter.sharedMesh;
                                Vector3[] vertices = mesh.vertices;
                                Vector2[] uv = mesh.uv;
                                int[] triangles = mesh.triangles;
                                for (int i = 0; i < triangles.Length; i += 3)
                                {
                                    int a = triangles[i];
                                    int b = triangles[i + 1];
                                    int c = triangles[i + 2];
                                    Vector3 cross = Vector3.Cross(vertices[b] - vertices[a], vertices[c] - vertices[a]);
                                    if (cross.magnitude < 0.0001f || Mathf.Abs(cross.normalized.y) >= 0.4f) { continue; }
                                    Vector2 ab = uv[b] - uv[a];
                                    Vector2 ac = uv[c] - uv[a];
                                    float projectedArea = Mathf.Abs(ab.x * ac.y - ab.y * ac.x);
                                    Assert.That(projectedArea / cross.magnitude, Is.GreaterThan(0.085f),
                                        path + " cliff must not collapse into XZ-only texture stripes");
                                    steepFaces++;
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (opened && scene.IsValid() && scene.isLoaded)
                        {
                            EditorSceneManager.CloseScene(scene, true);
                        }
                    }
                }
            }
            Assert.That(steepFaces, Is.GreaterThan(100), "Exercise real generated cliff faces.");
        }
    }
}
