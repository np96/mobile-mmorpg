using System;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Tests
{
    public sealed class SunbreakVistaWoodlandAuthoringTests
    {
        [Test]
        public void Woodland_UsesExactSharedTwoBatchAndRepresentationBudgets()
        {
            Mesh bark = Load(SunbreakVistaWoodlandAuthoring.BranchesName);
            Mesh leaf = Load(SunbreakVistaWoodlandAuthoring.CanopiesName);
            Assert.That(SunbreakVistaWoodlandAuthoring.GroveCount, Is.EqualTo(14));
            Assert.That(SunbreakVistaWoodlandAuthoring.TreeCount, Is.EqualTo(84));
            Assert.That(bark.subMeshCount, Is.EqualTo(1));
            Assert.That(leaf.subMeshCount, Is.EqualTo(1));
            Assert.That(bark.GetIndexCount(0) / 3, Is.EqualTo(84 * 40));
            Assert.That(leaf.GetIndexCount(0) / 3, Is.EqualTo(84 * 160));
            Assert.That((bark.GetIndexCount(0) + leaf.GetIndexCount(0)) / 3,
                Is.EqualTo(SunbreakVistaWoodlandAuthoring.MaximumTriangles));
            Assert.That(bark.vertexCount, Is.EqualTo(84 * 80));
            Assert.That(leaf.vertexCount, Is.EqualTo(84 * 320));
            Assert.That(bark.indexFormat, Is.EqualTo(IndexFormat.UInt16));
            Assert.That(leaf.indexFormat, Is.EqualTo(IndexFormat.UInt16));
        }

        [Test]
        public void EveryCompleteTreeFootprint_PreservesTwoMetersOfCoreClearance()
        {
            Vector3[] branches = Load(SunbreakVistaWoodlandAuthoring.BranchesName).vertices;
            Vector3[] leaves = Load(SunbreakVistaWoodlandAuthoring.CanopiesName).vertices;
            float shortest = float.PositiveInfinity, tallest = float.NegativeInfinity;
            for (int tree = 0; tree < SunbreakVistaWoodlandAuthoring.TreeCount; tree++)
            {
                var bounds = new Bounds(branches[tree * 80], Vector3.zero);
                for (int i = 0; i < 80; i++) { bounds.Encapsulate(branches[tree * 80 + i]); }
                for (int i = 0; i < 320; i++) { bounds.Encapsulate(leaves[tree * 320 + i]); }
                float margin = SunbreakVistaWoodlandAuthoring.CoreClearance;
                bool outside = bounds.max.x < PveRegionLayout.MinimumX - margin ||
                    bounds.min.x > PveRegionLayout.MaximumX + margin ||
                    bounds.max.z < PveRegionLayout.MinimumZ - margin ||
                    bounds.min.z > PveRegionLayout.MaximumZ + margin;
                Assert.That(outside, Is.True, "Complete bark/crown AABB intrudes: tree " + tree);
                Assert.That(bounds.size.y, Is.InRange(6.5f, 13.5f));
                Assert.That(bounds.size.x, Is.InRange(4f, 12f));
                Assert.That(bounds.size.z, Is.InRange(4f, 12f));
                shortest = Mathf.Min(shortest, bounds.size.y);
                tallest = Mathf.Max(tallest, bounds.size.y);
            }
            Assert.That(tallest - shortest, Is.GreaterThan(3.5f), "Canopy heights must not form a level hedge.");
        }

        [Test]
        public void DenserGroves_HaveOverlappingCrownsAndFoliageBelowOneQuarterOfTheirHeight()
        {
            Vector3[] branches = Load(SunbreakVistaWoodlandAuthoring.BranchesName).vertices;
            Vector3[] leaves = Load(SunbreakVistaWoodlandAuthoring.CanopiesName).vertices;
            var crowns = new Bounds[SunbreakVistaWoodlandAuthoring.TreeCount];
            for (int tree = 0; tree < crowns.Length; tree++)
            {
                Bounds crown = new Bounds(leaves[tree * 320], Vector3.zero);
                for (int i = 0; i < 320; i++) { crown.Encapsulate(leaves[tree * 320 + i]); }
                crowns[tree] = crown;
                float rootY = branches[tree * 80].y;
                Assert.That((crown.min.y - rootY) / (crown.max.y - rootY), Is.LessThan(0.23f),
                    "The rejected pole-like trees left most lower trunks exposed.");
                Assert.That(Mathf.Min(crown.size.x, crown.size.z) / (crown.max.y - rootY),
                    Is.GreaterThan(0.62f), "Thin vertical foliage strips are not broad bough crowns.");
            }
            for (int grove = 0; grove < SunbreakVistaWoodlandAuthoring.GroveCount; grove++)
            {
                for (int tree = 0; tree < SunbreakVistaWoodlandAuthoring.TreesPerGrove; tree++)
                {
                    int index = grove * SunbreakVistaWoodlandAuthoring.TreesPerGrove + tree;
                    int overlaps = 0;
                    for (int neighbor = 0; neighbor < SunbreakVistaWoodlandAuthoring.TreesPerGrove; neighbor++)
                    {
                        if (neighbor == tree) { continue; }
                        Bounds other = crowns[grove * SunbreakVistaWoodlandAuthoring.TreesPerGrove + neighbor];
                        float x = Mathf.Min(crowns[index].max.x, other.max.x) - Mathf.Max(crowns[index].min.x, other.min.x);
                        float z = Mathf.Min(crowns[index].max.z, other.max.z) - Mathf.Max(crowns[index].min.z, other.min.z);
                        if (x > 0.7f && z > 0.7f) { overlaps++; }
                    }
                    Assert.That(overlaps, Is.GreaterThanOrEqualTo(2),
                        "Every grove tree needs neighboring canopy volume, not a row of isolated sparse poles: " + index);
                }
            }
        }

        [Test]
        public void WoodyRoots_AreEmbeddedInTheActualTriangulatedShoulder()
        {
            Vector3[] branches = Load(SunbreakVistaWoodlandAuthoring.BranchesName).vertices;
            Mesh terrain = Load("ErodedMountainHorizon");
            Vector3[] vertices = terrain.vertices;
            int[] indices = terrain.triangles;
            float minimumRadius = float.PositiveInfinity, maximumRadius = float.NegativeInfinity;
            for (int tree = 0; tree < SunbreakVistaWoodlandAuthoring.TreeCount; tree++)
            {
                Vector3 root = Vector3.zero;
                for (int side = 0; side < 4; side++)
                {
                    root += branches[tree * 80 + side * 4];
                    root += branches[tree * 80 + side * 4 + 1];
                }
                root /= 8f;
                float ground = HeightAt(root, vertices, indices);
                Assert.That(ground - root.y, Is.EqualTo(0.32f).Within(0.003f),
                    "Trees must not float above a mathematical field that differs from the rendered triangle surface.");
                Vector2 radial = new Vector2(root.x - 32f, root.z - 32f);
                Vector2 direction = radial.normalized;
                float radius = radial.magnitude - 92f / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y));
                minimumRadius = Mathf.Min(minimumRadius, radius);
                maximumRadius = Mathf.Max(maximumRadius, radius);
            }
            Assert.That(maximumRadius - minimumRadius, Is.GreaterThan(40f),
                "Woodland needs near/middle shoulder depth instead of one perimeter row.");
        }

        [TestCase(SunbreakVistaWoodlandAuthoring.BranchesName)]
        [TestCase(SunbreakVistaWoodlandAuthoring.CanopiesName)]
        public void Geometry_HasFiniteOrthonormalFramesAndNondegenerateSurfaces(string name)
        {
            Mesh mesh = Load(name);
            Vector3[] vertices = mesh.vertices, normals = mesh.normals;
            Vector4[] tangents = mesh.tangents;
            Vector2[] uv = mesh.uv;
            Assert.That(normals, Has.Length.EqualTo(vertices.Length));
            Assert.That(tangents, Has.Length.EqualTo(vertices.Length));
            Assert.That(uv, Has.Length.EqualTo(vertices.Length));
            for (int i = 0; i < vertices.Length; i++)
            {
                Assert.That(Finite(vertices[i]) && Finite(normals[i]) && Finite(tangents[i]), Is.True);
                Assert.That(float.IsNaN(uv[i].x) || float.IsNaN(uv[i].y), Is.False);
                Assert.That(normals[i].sqrMagnitude, Is.EqualTo(1f).Within(0.002f));
                Vector3 tangent = new Vector3(tangents[i].x, tangents[i].y, tangents[i].z);
                Assert.That(tangent.sqrMagnitude, Is.EqualTo(1f).Within(0.002f));
                Assert.That(Mathf.Abs(Vector3.Dot(normals[i], tangent)), Is.LessThan(0.002f));
                Assert.That(Mathf.Abs(tangents[i].w), Is.EqualTo(1f));
            }
            int[] indices = mesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                Assert.That(Vector3.Cross(b - a, c - a).sqrMagnitude, Is.GreaterThan(0.00000001f));
            }
        }

        [Test]
        public void CrownCards_UseAllEightMeasuredViewsAndThreeDimensionalBoughPlanes()
        {
            Mesh mesh = Load(SunbreakVistaWoodlandAuthoring.CanopiesName);
            Vector2[] uv = mesh.uv;
            Vector3[] vertices = mesh.vertices;
            var used = new bool[8];
            int inclined = 0;
            for (int card = 0; card < vertices.Length; card += 4)
            {
                Vector2 center = (uv[card] + uv[card + 2]) * 0.5f;
                int row = Mathf.FloorToInt(center.y * 4f), column = Mathf.FloorToInt(center.x * 2f);
                used[row * 2 + column] = true;
                Assert.That(uv[card].x, Is.EqualTo(column * 0.5f + 1f / 1024f).Within(0.000001f));
                Assert.That(uv[card].y, Is.EqualTo(row * 0.25f + 1f / 2048f).Within(0.000001f));
                Assert.That(uv[card + 2].x, Is.EqualTo((column + 1) * 0.5f - 1f / 1024f).Within(0.000001f));
                Assert.That(uv[card + 2].y, Is.EqualTo((row + 1) * 0.25f - 1f / 2048f).Within(0.000001f));
                Vector3 normal = Vector3.Cross(vertices[card + 1] - vertices[card], vertices[card + 3] - vertices[card]).normalized;
                if (Mathf.Abs(normal.y) > 0.2f && Mathf.Abs(normal.y) < 0.8f) { inclined++; }
            }
            foreach (bool present in used) { Assert.That(present, Is.True); }
            Assert.That(inclined, Is.GreaterThan(1000), "Actual oblique boughs, not a billboard fence, must supply depth.");
        }

        [TestCase("TreeTrunk", SunbreakVistaWoodlandAuthoring.BranchMaterialPath)]
        [TestCase("TreeLeaf", SunbreakVistaWoodlandAuthoring.LeafMaterialPath)]
        public void MaterialVariants_KeepMeasuredMapProvenanceAndCutoutSemantics(string role, string path)
        {
            Material source = AssetDatabase.LoadAssetAtPath<Material>(SunbreakVegetationAssets.MaterialPath(role));
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.That(source, Is.Not.Null);
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.SameAs(source.shader));
            Assert.That(ShaderUtil.ShaderHasError(material.shader), Is.False);
            foreach (string property in new[] { "_BaseMap", "_BumpMap", "_RoughnessMap" })
            { Assert.That(material.GetTexture(property), Is.SameAs(source.GetTexture(property))); }
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(Vector2.one));
            Assert.That(material.GetTextureOffset("_BaseMap"), Is.EqualTo(Vector2.zero));
            Assert.That(material.renderQueue, Is.EqualTo(source.renderQueue));
            Assert.That(material.GetFloat("_Cull"), Is.EqualTo(source.GetFloat("_Cull")));
            Assert.That(material.enableInstancing, Is.True);
            if (role == "TreeLeaf")
            {
                Assert.That(material.GetFloat("_AlphaFromBase"), Is.EqualTo(1f));
                Assert.That(material.GetFloat("_Cutoff"), Is.EqualTo(0.4f));
                Assert.That(material.GetFloat("_BumpScale"), Is.EqualTo(0.45f));
                Assert.That(material.GetFloat("_Smoothness"), Is.EqualTo(0.3f));
            }
        }

        private static Mesh Load(string name)
        {
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SunbreakVistaAuthoring.Folder + "/" + name + ".asset");
            Assert.That(mesh, Is.Not.Null, "Rebuild the authored foundation after changing woodland source.");
            return mesh;
        }

        private static bool Finite(Vector3 value) => Finite(value.x) && Finite(value.y) && Finite(value.z);
        private static bool Finite(Vector4 value) => Finite(value.x) && Finite(value.y) && Finite(value.z) && Finite(value.w);
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        private static float HeightAt(Vector3 root, Vector3[] vertices, int[] indices)
        {
            Vector3 rayOrigin = new Vector3(root.x, 100f, root.z);
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = vertices[indices[i]], b = vertices[indices[i + 1]], c = vertices[indices[i + 2]];
                Vector3 edge1 = b - a, edge2 = c - a;
                Vector3 p = Vector3.Cross(Vector3.down, edge2);
                float determinant = Vector3.Dot(edge1, p);
                if (Mathf.Abs(determinant) < 0.000001f) { continue; }
                Vector3 offset = rayOrigin - a;
                float u = Vector3.Dot(offset, p) / determinant;
                Vector3 q = Vector3.Cross(offset, edge1);
                float v = Vector3.Dot(Vector3.down, q) / determinant;
                if (u < -0.00001f || v < -0.00001f || u + v > 1.00001f) { continue; }
                return 100f - Vector3.Dot(edge2, q) / determinant;
            }
            throw new InvalidOperationException("No generated mountain triangle below woodland root.");
        }
    }
}
