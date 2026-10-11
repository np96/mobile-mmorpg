using System;
using System.Reflection;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Editor.Authoring;
using SessionRpg.Unity.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakCourtyardCompositionTests
    {
        private const string ChunkPath = "Assets/SessionRpg/Content/Worlds/SunbreakBasin/Chunks/Chunk_-1_0.unity";
        private const string MeadowPath = "Assets/SessionRpg/Art/Materials/Sunbreak_Meadow.mat";

        [TestCase(0)]
        [TestCase(2)]
        public void FrostboundFoundation_UsesLowErodedBanksInsteadOfTheMajorShelfHeight(int landmark)
        {
            MethodInfo shelfPoint = ShelfPointMethod();
            ArenaCircleObstacle bound = PveRegionLayout.GetLandmark(landmark);
            float minimum = float.PositiveInfinity;
            float maximum = float.NegativeInfinity;
            float minimumUpper = float.PositiveInfinity;
            int samples = 0;
            for (float z = bound.Center.Z - bound.Radius; z <= bound.Center.Z + bound.Radius; z += 0.5f)
            {
                for (float x = bound.Center.X - bound.Radius; x <= bound.Center.X + bound.Radius; x += 0.5f)
                {
                    var point = new Vector3(x, 0f, z);
                    if (!InsideLandmark(point, landmark)) { continue; }
                    Vector3 actual = SampleShelf(shelfPoint, point, landmark, 2.65f);
                    Assert.That(actual.x, Is.EqualTo(x));
                    Assert.That(actual.z, Is.EqualTo(z));
                    Assert.That(actual.y, Is.InRange(0.025f, 1.4051f),
                        "The abbey foundation must not return to the tall rounded shelf profile.");
                    Assert.That(SampleShelf(shelfPoint, point, landmark, 8f), Is.EqualTo(actual),
                        "The dedicated low bank must not inherit the other major shelves' height parameter.");
                    minimum = Mathf.Min(minimum, actual.y);
                    maximum = Mathf.Max(maximum, actual.y);
                    if (actual.y > 0.3f) { minimumUpper = Mathf.Min(minimumUpper, actual.y); }
                    samples++;
                }
            }
            Assert.That(samples, Is.GreaterThan(800));
            Assert.That(maximum - minimum, Is.GreaterThan(0.6f), "Retain visible erosion, not a flat plinth.");
            Assert.That(maximum - minimumUpper, Is.GreaterThan(0.25f));
        }

        [TestCase(57f, -40f)]
        [TestCase(51f, -40f)]
        [TestCase(60f, -35f)]
        public void OtherMajorShelf_RetainsItsExistingGeologicalProfile(float x, float z)
        {
            const int landmark = 1;
            const float height = 2.65f;
            var point = new Vector3(x, 0f, z);
            float inside = Mathf.Max(0f, InteriorDistance(point, landmark) - 0.03f);
            float broad = Noise(x + landmark * 9.31f, z, 0.12f);
            float layer = height * (0.34f + broad * 0.93f);
            float course = Mathf.Floor(layer / 0.38f) * 0.38f;
            float bevel = Mathf.SmoothStep(0f, 1f, Mathf.Repeat(layer, 0.38f) / 0.38f);
            float erosion = (Noise(x, z - 61f, 0.69f) - 0.5f) * 0.19f;
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inside / 0.85f));
            float expected = 0.025f + Mathf.Max(0.1f, course + bevel * 0.38f + erosion) * edge;
            Assert.That(SampleShelf(ShelfPointMethod(), point, landmark, height).y,
                Is.EqualTo(expected).Within(0.00001f),
                "The focal courtyard revision must not silently flatten unrelated major landmarks.");
        }

        [Test]
        public void GeneratedFrostboundFoundation_PreservesLowEarthAndSeamAlignedWorldUvs()
        {
            WithChunk(root =>
            {
                MeshFilter filter = RequireMesh(root, "BlockerIslands/Blocker_0/RockShelf");
                Assert.That(AssetDatabase.GetAssetPath(filter.GetComponent<MeshRenderer>().sharedMaterial),
                    Is.EqualTo(MeadowPath), "The lower abbey foundation must blend into earth, not a bright rock base.");
                Vector3[] vertices = filter.sharedMesh.vertices;
                Vector2[] uvs = filter.sharedMesh.uv;
                float minimum = float.PositiveInfinity;
                float maximum = float.NegativeInfinity;
                Assert.That(uvs.Length, Is.EqualTo(vertices.Length));
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 world = filter.transform.TransformPoint(vertices[i]);
                    Assert.That(world.y, Is.InRange(0.025f, 1.4051f));
                    Assert.That(InsideLandmark(world, 0), Is.True);
                    Assert.That(uvs[i].x, Is.EqualTo((world.x + 64f) / 192f).Within(0.00001f));
                    Assert.That(uvs[i].y, Is.EqualTo((world.z + 64f) / 192f).Within(0.00001f));
                    minimum = Mathf.Min(minimum, world.y);
                    maximum = Mathf.Max(maximum, world.y);
                }
                Assert.That(maximum - minimum, Is.GreaterThan(0.6f));
            });
        }

        [TestCase(0, "MeasuredWatchtower", SunbreakFortAssets.Module.RoundTower, 4772, 10.57f, 10.65f)]
        [TestCase(0, "MeasuredTowerWing", SunbreakFortAssets.Module.BattlementWall, 622, 5.52f, 5.62f)]
        [TestCase(0, "MeasuredSouthReturn", SunbreakFortAssets.Module.LowWall, 406, 4.68f, 4.76f)]
        [TestCase(0, "MeasuredNorthReturn", SunbreakFortAssets.Module.LowWall, 406, 3.58f, 3.68f)]
        [TestCase(0, "MeasuredBrokenAbutment", SunbreakFortAssets.Module.ThickEnd, 442, 3.29f, 3.35f)]
        public void GeneratedArchitecture_IsGroundedAndEveryTriangleSampleStaysInsideExactCover(
            int landmark, string name, SunbreakFortAssets.Module module, int triangleCount, float minimumTop, float maximumTop)
        {
            WithChunk(root =>
            {
                MeshFilter filter = RequireMesh(root, "BlockerIslands/Blocker_" + landmark + "/" + name);
                Assert.That(filter.sharedMesh, Is.SameAs(SunbreakFortAssets.RequireRenderMesh(module)),
                    "Use the verified source mesh and source UVs without clamping or regenerated geometry.");
                Vector3[] vertices = WorldVertices(filter);
                int[] indices = filter.sharedMesh.triangles;
                Bounds bounds = new Bounds(vertices[0], Vector3.zero);
                foreach (Vector3 vertex in vertices)
                {
                    bounds.Encapsulate(vertex);
                    AssertRaisedPointInside(vertex, landmark, name);
                    Assert.That(InsideLandmark(vertex, landmark), Is.True,
                        "The complete measured silhouette, including its base, must remain in the exact Core union.");
                }
                Assert.That(bounds.min.y, Is.InRange(0f, 0.181f), "Masonry must stand on the ground, not float on a display base.");
                Assert.That(bounds.max.y, Is.InRange(minimumTop, maximumTop));
                Assert.That(indices.Length / 3, Is.EqualTo(triangleCount));
                Assert.That(filter.sharedMesh.subMeshCount, Is.EqualTo(2));
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                    AssertRaisedPointInside((a + b + c) / 3f, landmark, name);
                    AssertRaisedPointInside((a + b) * 0.5f, landmark, name);
                    AssertRaisedPointInside((b + c) * 0.5f, landmark, name);
                    AssertRaisedPointInside((c + a) * 0.5f, landmark, name);
                }
                Material[] materials = filter.GetComponent<MeshRenderer>().sharedMaterials;
                Assert.That(materials, Has.Length.EqualTo(2));
                Assert.That(AssetDatabase.GetAssetPath(materials[SunbreakFortAssets.WallSubmesh(module)]), Is.EqualTo(SunbreakFortAssets.WallMaterialPath));
                Assert.That(AssetDatabase.GetAssetPath(materials[SunbreakFortAssets.TrimSubmesh(module)]), Is.EqualTo(SunbreakFortAssets.TrimMaterialPath));
                foreach (Material material in materials)
                {
                    Assert.That(material.shader.name, Is.EqualTo(SunbreakFortAssets.ShaderName));
                    Assert.That(material.GetFloat("_Surface"), Is.Zero);
                    Assert.That(material.GetFloat("_AlphaClip"), Is.Zero);
                    Assert.That(material.GetTexture("_BaseMap"), Is.InstanceOf<Texture2D>());
                    Assert.That(material.GetTexture("_BumpMap"), Is.InstanceOf<Texture2D>());
                }
                Assert.That(filter.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(filter.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            });
        }

        [Test]
        public void RejectedCourtyardAndProceduralFocalGroups_AreAbsentWhileContinuousMeadowRemains()
        {
            WithChunk(root =>
            {
                string[] names = { "FrostboundCourtyardPaving", "FrostboundCourtyardEarthEdges", "FrostboundCourtyardChips" };
                foreach (string name in names)
                {
                    Assert.That(root.Find("Encampments/" + name), Is.Null,
                        "The rejected paver patches must not survive underneath the measured composition.");
                }
                Assert.That(root.Find("Encampments/Camp_1_WornClearing"), Is.Null);
                Assert.That(root.Find("Encampments/Camp_1_LeafLitterApron"), Is.Null);
                string[] replaced = { "BranchingWood", "LeafCanopy", "SunlitLeafEdges", "RockFernUnderstory",
                    "EmbeddedBoulders", "WeatheredAbbeyMasonry", "FrostboundWatchtower", "FrostboundAbbeyFacade" };
                foreach (int landmark in new[] { 0, 7 })
                {
                    foreach (string group in replaced)
                    {
                        Assert.That(root.Find("BlockerIslands/Blocker_" + landmark + "/" + group), Is.Null,
                            "Do not overlay measured modules on the procedural focal silhouette: " + group);
                    }
                    RequireMesh(root, "BlockerIslands/Blocker_" + landmark + "/RockShelf");
                }
                MeshFilter ground = RequireMesh(root, "Ground/ContinuousMeadow");
                Assert.That(ground.sharedMesh.triangles, Has.Length.EqualTo(6));
                Assert.That(AssetDatabase.GetAssetPath(ground.GetComponent<MeshRenderer>().sharedMaterial), Is.EqualTo(MeadowPath));
                foreach (Vector3 point in WorldVertices(ground)) { Assert.That(point.y, Is.EqualTo(0f).Within(0.00001f)); }
            });
        }

        [TestCase(0, "MeasuredTowerWing", -6.1f, 0.06f, -1.8f, 40f)]
        public void MeasuredWalls_HaveSolidLowerCoursesRatherThanFalseWalkableGateways(
            int landmark, string name, float offsetX, float offsetY, float offsetZ, float yaw)
        {
            WithChunk(root =>
            {
                MeshFilter filter = RequireMesh(root, "BlockerIslands/Blocker_" + landmark + "/" + name);
                Assert.That(filter.sharedMesh, Is.SameAs(SunbreakFortAssets.RequireRenderMesh(SunbreakFortAssets.Module.BattlementWall)));
                Vector3[] vertices = WorldVertices(filter);
                int[] indices = filter.sharedMesh.triangles;
                Float3 anchor = PveRegionLayout.GetLandmark(landmark).Center;
                Vector3 bottom = new Vector3(anchor.X + offsetX, offsetY, anchor.Z + offsetZ);
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                for (int column = -4; column <= 4; column++)
                {
                    float z = column * 0.2f;
                    for (int row = 1; row <= 5; row++)
                    {
                        // Source wall thickness is X; its long axis is Z.
                        Vector3 start = bottom + rotation * new Vector3(-4f, row * 0.2f, z);
                        Vector3 end = bottom + rotation * new Vector3(4f, row * 0.2f, z);
                        Assert.That(IntersectsMesh(vertices, indices, start, end), Is.True,
                            name + " must visibly fill its lower solid course at column " + column + ", row " + row);
                    }
                }
                Assert.That(IntersectsMesh(vertices, indices, bottom + Vector3.up * 8f - Vector3.right * 4f,
                    bottom + Vector3.up * 8f + Vector3.right * 4f), Is.False,
                    "The mesh intersection guard must reject a segment above the complete wall.");
            });
        }

        [Test]
        public void ArchitectureComposition_RetainsFiveOriginalModulesAndOneBudgetedRuinDerivative()
        {
            WithChunk(root =>
            {
                int instances = 0, triangles = 0;
                foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (!AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(
                        SunbreakFortAssets.MeshFolder + "/", StringComparison.Ordinal)) { continue; }
                    instances++;
                    for (int submesh = 0; submesh < filter.sharedMesh.subMeshCount; submesh++)
                    {
                        triangles += (int)filter.sharedMesh.GetIndexCount(submesh) / 3;
                    }
                }
                Assert.That(instances, Is.EqualTo(5));
                Assert.That(triangles, Is.EqualTo(6648));
                MeshFilter ruin = RequireMesh(root, "BlockerIslands/Blocker_7/" + SunbreakRuinMasonryAuthoring.RootName);
                Assert.That(AssetDatabase.GetAssetPath(ruin.sharedMesh), Is.EqualTo(SunbreakRuinMasonryAuthoring.MeshPath));
                Assert.That(ruin.sharedMesh.subMeshCount, Is.EqualTo(1));
                int ruinTriangles = (int)ruin.sharedMesh.GetIndexCount(0) / 3;
                Assert.That(ruinTriangles, Is.InRange(100, SunbreakRuinMasonryAuthoring.MaximumTriangles));
                triangles += ruinTriangles;
                Assert.That(triangles, Is.LessThanOrEqualTo(7924));
                Assert.That(triangles, Is.LessThanOrEqualTo(SunbreakFortAssets.MaximumCompositionTriangles));
            });
        }

        [TestCase(0, 5)]
        public void MeasuredCompoundParts_FormOneOverlappingArchitecturalFootprint(int landmark, int expectedParts)
        {
            WithChunk(root =>
            {
                Transform owner = root.Find("BlockerIslands/Blocker_" + landmark);
                var bounds = new System.Collections.Generic.List<Bounds>();
                foreach (MeshFilter filter in owner.GetComponentsInChildren<MeshFilter>())
                {
                    if (!AssetDatabase.GetAssetPath(filter.sharedMesh).StartsWith(
                        SunbreakFortAssets.MeshFolder + "/", StringComparison.Ordinal)) { continue; }
                    bounds.Add(filter.GetComponent<MeshRenderer>().bounds);
                }
                Assert.That(bounds.Count, Is.EqualTo(expectedParts));
                bool[] reached = new bool[bounds.Count];
                reached[0] = true;
                for (int pass = 0; pass < bounds.Count; pass++)
                {
                    for (int i = 0; i < bounds.Count; i++)
                    {
                        if (!reached[i]) { continue; }
                        for (int j = 0; j < bounds.Count; j++)
                        {
                            float dx = Mathf.Max(0f, Mathf.Max(bounds[i].min.x - bounds[j].max.x,
                                bounds[j].min.x - bounds[i].max.x));
                            float dz = Mathf.Max(0f, Mathf.Max(bounds[i].min.z - bounds[j].max.z,
                                bounds[j].min.z - bounds[i].max.z));
                            if (dx * dx + dz * dz <= 0.25f) { reached[j] = true; }
                        }
                    }
                }
                Assert.That(reached, Is.All.True,
                    "No selected building bounds may become another isolated prop across the field.");
            });
        }

        [Test]
        public void BrokenAbbey_ReplacesTheIntactForegroundStackWithOneOpaqueSourceDerivative()
        {
            WithChunk(root =>
            {
                MeshFilter ruin = RequireMesh(root, "BlockerIslands/Blocker_7/" + SunbreakRuinMasonryAuthoring.RootName);
                SunbreakRuinMasonryAuthoring.Validate(ruin.gameObject);
                Assert.That(AssetDatabase.GetAssetPath(ruin.sharedMesh), Is.EqualTo(SunbreakRuinMasonryAuthoring.MeshPath));
                Material[] materials = ruin.GetComponent<MeshRenderer>().sharedMaterials;
                Assert.That(materials, Has.Length.EqualTo(1));
                Assert.That(materials[0].GetFloat("_Surface"), Is.Zero);
                Assert.That(materials[0].GetFloat("_AlphaClip"), Is.Zero);
                Vector3[] vertices = WorldVertices(ruin);
                int[] indices = ruin.sharedMesh.triangles;
                foreach (Vector3 point in vertices) { Assert.That(InsideLandmark(point, 7), Is.True); }
                for (int i = 0; i < indices.Length; i += 3)
                {
                    Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                    foreach (Vector3 point in new[] { (a + b + c) / 3f, (a + b) * 0.5f, (b + c) * 0.5f, (c + a) * 0.5f })
                    { Assert.That(InsideLandmark(point, 7), Is.True, "A generated ruin triangle crosses walkable Core space."); }
                }
                Assert.That(ruin.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(ruin.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
                foreach (string rejected in new[] { "MeasuredAbbeyWall", "MeasuredAbbeyCorner", "MeasuredWallButtress", "MeasuredGardenReturn", "MeasuredGardenRemnant" })
                {
                    Assert.That(root.Find("BlockerIslands/Blocker_7/" + rejected), Is.Null,
                        "Intact and mismatched modules must be replaced, not hidden behind the broken derivative.");
                }
            });
        }

        [TestCase(0.25f)]
        [TestCase(0.8f)]
        [TestCase(1.8f)]
        [TestCase(3.2f)]
        public void AbbeyJoint_HasMatchingRealMasonryCrossSectionsAtEveryBodyCourse(float height)
        {
            using (SunbreakRuinMasonryAuthoring.Geometry geometry = SunbreakRuinMasonryAuthoring.CreateGeometry())
            {
                // The derivative removes hidden overlap at the common cut plane.
                // Probe actual faces half a millimetre on each side so neither
                // a coplanar cut edge nor touching AABBs can satisfy this check.
                Vector2 wallSection = SectionAlongX(geometry.Wall.vertices, geometry.Wall.triangles, height,
                    SunbreakRuinMasonryAuthoring.JointZ - 0.0005f);
                Vector2 cornerSection = SectionAlongX(geometry.Corner.vertices, geometry.Corner.triangles, height,
                    SunbreakRuinMasonryAuthoring.JointZ + 0.0005f);
                string report = $"Abbey joint at y={height:F3}, z=26.020 +/- 0.0005: wall=[{wallSection.x:F6}, {wallSection.y:F6}], " +
                    $"corner=[{cornerSection.x:F6}, {cornerSection.y:F6}].";
                TestContext.WriteLine(report);
                Assert.That(wallSection.y - wallSection.x, Is.GreaterThan(0.8f), report);
                Assert.That(cornerSection.y - cornerSection.x, Is.GreaterThan(0.8f), report);
                Assert.That(cornerSection.x, Is.EqualTo(wallSection.x).Within(0.012f), report);
                Assert.That(cornerSection.y, Is.EqualTo(wallSection.y).Within(0.012f), report);
                Assert.That(Mathf.Min(wallSection.y, cornerSection.y) - Mathf.Max(wallSection.x, cornerSection.x),
                    Is.GreaterThan(0.8f), "Both complete wall courses must occupy the same joint section.");
            }
        }

        [Test]
        public void GenuineAbbeyCorner_SuppliesAPerpendicularSolidReturn()
        {
            using (SunbreakRuinMasonryAuthoring.Geometry geometry = SunbreakRuinMasonryAuthoring.CreateGeometry())
            {
                var start = new Vector3(-42f, 0.8f, 25f);
                var end = new Vector3(-42f, 0.8f, 30f);
                Assert.That(IntersectsMesh(geometry.Corner.vertices, geometry.Corner.triangles, start, end), Is.True,
                    "The genuine source corner must turn into a solid return, not another parallel wall sample.");
                Assert.That(IntersectsMesh(geometry.Wall.vertices, geometry.Wall.triangles, start, end), Is.False,
                    "The return probe is outside the straight wall and cannot pass through AABB coincidence alone.");
            }
        }

        [Test]
        public void BrokenAbbey_RetainsSolidLowerCoursesBelowTheFracture()
        {
            WithChunk(root =>
            {
                MeshFilter ruin = RequireMesh(root, "BlockerIslands/Blocker_7/" + SunbreakRuinMasonryAuthoring.RootName);
                Vector3[] vertices = WorldVertices(ruin); int[] indices = ruin.sharedMesh.triangles;
                for (int column = 0; column <= 12; column++)
                {
                    float z = 22.6f + column * 0.3f;
                    for (int row = 1; row <= 5; row++)
                    {
                        float height = 0.05f + row * 0.2f;
                        Assert.That(IntersectsMesh(vertices, indices, new Vector3(-46f, height, z),
                            new Vector3(-40f, height, z)), Is.True, "The broken wall must keep its coherent solid base.");
                    }
                }
                Assert.That(IntersectsMesh(vertices, indices, new Vector3(-46f, 4f, 24.2f),
                    new Vector3(-40f, 4f, 24.2f)), Is.False, "Intact high parapets must not survive the derivative.");
            });
        }

        private static Vector2 SectionAlongX(Vector3[] vertices, int[] indices, float y, float z)
        {
            float minimum = float.PositiveInfinity, maximum = float.NegativeInfinity;
            int hits = 0;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                float determinant = (b.y - a.y) * (c.z - a.z) - (c.y - a.y) * (b.z - a.z);
                if (Mathf.Abs(determinant) < 0.00000001f) { continue; }
                float u = ((y - a.y) * (c.z - a.z) - (z - a.z) * (c.y - a.y)) / determinant;
                float v = ((b.y - a.y) * (z - a.z) - (b.z - a.z) * (y - a.y)) / determinant;
                if (u < -0.00001f || v < -0.00001f || u + v > 1.00001f) { continue; }
                float x = a.x + u * (b.x - a.x) + v * (c.x - a.x);
                minimum = Mathf.Min(minimum, x);
                maximum = Mathf.Max(maximum, x);
                hits++;
            }
            Assert.That(hits, Is.GreaterThanOrEqualTo(2), "A section requires two real masonry faces.");
            return new Vector2(minimum, maximum);
        }

        private static MethodInfo ShelfPointMethod()
        {
            Type authoring = typeof(SessionRpgAuthoringGenerator).Assembly.GetType(
                "SessionRpg.Editor.Authoring.SunbreakBasinAuthoring");
            Assert.That(authoring, Is.Not.Null);
            MethodInfo method = authoring.GetMethod("ShelfPoint", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method;
        }

        private static Vector3 SampleShelf(MethodInfo method, Vector3 point, int landmark, float height) =>
            (Vector3)method.Invoke(null, new object[] { point, landmark, height });

        private static float Noise(float x, float z, float scale) =>
            Mathf.PerlinNoise(x * scale + 183.3f, z * scale + 291.7f);

        private static float InteriorDistance(Vector3 point, int landmark)
        {
            float result = float.NegativeInfinity;
            int start = PveRegionLayout.GetLandmarkObstacleStart(landmark);
            int count = PveRegionLayout.GetLandmarkObstacleCount(landmark);
            for (int i = start; i < start + count; i++)
            {
                ArenaCircleObstacle circle = PveRegionLayout.GetObstacle(i);
                float x = point.x - circle.Center.X, z = point.z - circle.Center.Z;
                result = Mathf.Max(result, circle.Radius - Mathf.Sqrt(x * x + z * z));
            }
            return result;
        }

        private static bool InsideLandmark(Vector3 point, int landmark) => InteriorDistance(point, landmark) >= -0.005f;

        private static void AssertRaisedPointInside(Vector3 point, int landmark, string name)
        {
            Assert.That(float.IsNaN(point.x) || float.IsInfinity(point.x) ||
                float.IsNaN(point.y) || float.IsInfinity(point.y) ||
                float.IsNaN(point.z) || float.IsInfinity(point.z), Is.False);
            if (point.y <= 0.181f) { return; }
            Assert.That(InsideLandmark(point, landmark), Is.True,
                name + " must not bridge a walkable concavity or rely on the conservative landmark bound: " + point);
        }

        private static Vector3[] WorldVertices(MeshFilter filter)
        {
            Vector3[] vertices = filter.sharedMesh.vertices;
            for (int i = 0; i < vertices.Length; i++) { vertices[i] = filter.transform.TransformPoint(vertices[i]); }
            return vertices;
        }

        private static MeshFilter RequireMesh(Transform root, string relativePath)
        {
            Transform child = root.Find(relativePath);
            Assert.That(child, Is.Not.Null, relativePath);
            MeshFilter filter = child.GetComponent<MeshFilter>();
            Assert.That(filter, Is.Not.Null, relativePath);
            Assert.That(filter.sharedMesh, Is.Not.Null, relativePath);
            return filter;
        }

        private static bool IntersectsMesh(Vector3[] vertices, int[] indices, Vector3 start, Vector3 end)
        {
            // Double-sided segment/triangle intersection inspects the presentation
            // mesh itself; adding a physics collider would invalidate the contract.
            Vector3 direction = end - start;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], edge1 = vertices[indices[i + 1]] - a;
                Vector3 edge2 = vertices[indices[i + 2]] - a;
                Vector3 p = Vector3.Cross(direction, edge2);
                float determinant = Vector3.Dot(edge1, p);
                if (Mathf.Abs(determinant) < 0.000001f) { continue; }
                float inverse = 1f / determinant;
                Vector3 t = start - a;
                float u = Vector3.Dot(t, p) * inverse;
                if (u < -0.00001f || u > 1.00001f) { continue; }
                Vector3 q = Vector3.Cross(t, edge1);
                float v = Vector3.Dot(direction, q) * inverse;
                if (v < -0.00001f || u + v > 1.00001f) { continue; }
                float distance = Vector3.Dot(edge2, q) * inverse;
                if (distance >= 0f && distance <= 1f) { return true; }
            }
            return false;
        }

        private static void WithChunk(Action<Transform> inspect)
        {
            Scene scene = SceneManager.GetSceneByPath(ChunkPath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened) { scene = EditorSceneManager.OpenScene(ChunkPath, OpenSceneMode.Additive); }
                GameObject[] roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(1));
                WorldChunkPresentation presentation = roots[0].GetComponent<WorldChunkPresentation>();
                Assert.That(presentation, Is.Not.Null);
                Transform visual = presentation.VisualRoot;
                Assert.That(visual, Is.Not.Null);
                inspect(visual);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded) { EditorSceneManager.CloseScene(scene, true); }
            }
        }
    }
}
