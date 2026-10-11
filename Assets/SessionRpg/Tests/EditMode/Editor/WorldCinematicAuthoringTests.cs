using NUnit.Framework;
using SessionRpg.Unity.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Tests
{
    public sealed class WorldCinematicAuthoringTests
    {
        [TestCase(-3, "WestRefuge")]
        [TestCase(-2, "CinderApproach")]
        [TestCase(-1, "BrokenGate")]
        [TestCase(0, "CollapsedCourt")]
        [TestCase(1, "OssuaryBend")]
        [TestCase(2, "ReliquaryYard")]
        [TestCase(3, "EastWatch")]
        public void GeneratedDistrict_KeepsBoundedOffRouteCinematicDepth(
            int coordinate,
            string district)
        {
            string path =
                "Assets/SessionRpg/Content/Worlds/AshenCauseway/Chunks/Chunk_" +
                coordinate + "_0.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                GameObject[] roots = scene.GetRootGameObjects();
                Assert.That(roots, Has.Length.EqualTo(1));
                WorldChunkPresentation presentation =
                    roots[0].GetComponent<WorldChunkPresentation>();
                Assert.That(presentation, Is.Not.Null);
                Assert.That(presentation.PresentationVersion, Is.EqualTo(3));
                Transform visuals = presentation.VisualRoot;
                AssertDedicatedWorldBackdrop(visuals);
                AssertRegionDetails(visuals);
                AssertFlatPavingDetails(visuals);
                Transform landmarks = visuals.Find("AshenCausewayLandmarks");
                Transform depth = landmarks.Find(district + "/CinematicDepth");
                Assert.That(depth, Is.Not.Null, district);
                Assert.That(depth.childCount, Is.EqualTo(2));
                Assert.That(depth.Find("DistantButtress"), Is.Not.Null);
                Assert.That(depth.Find("BrokenApproachSilhouette"), Is.Not.Null);

                int retainedDepthRoots = 0;
                for (int index = 0; index < landmarks.childCount; index++)
                {
                    if (landmarks.GetChild(index).Find("CinematicDepth") != null)
                    {
                        retainedDepthRoots++;
                    }
                }

                Assert.That(retainedDepthRoots, Is.EqualTo(1),
                    "Unselected POIs must not duplicate skyline geometry.");
                MeshRenderer[] depthRenderers =
                    depth.GetComponentsInChildren<MeshRenderer>(true);
                Assert.That(depthRenderers, Has.Length.EqualTo(2));
                foreach (MeshRenderer renderer in depthRenderers)
                {
                    Assert.That(renderer.shadowCastingMode,
                        Is.EqualTo(ShadowCastingMode.Off));
                    Vector3 minimum = visuals.InverseTransformPoint(renderer.bounds.min);
                    Vector3 maximum = visuals.InverseTransformPoint(renderer.bounds.max);
                    Assert.That(minimum.z >= 11.5f || maximum.z <= -11.5f, Is.True,
                        renderer.name + " must stay outside the entire playable shoulder.");
                    Assert.That(minimum.x, Is.GreaterThanOrEqualTo(-28f));
                    Assert.That(maximum.x, Is.LessThanOrEqualTo(28f));
                }

                Assert.That(depth.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
                Assert.That(depth.GetComponentsInChildren<Light>(true), Is.Empty);
                Assert.That(visuals.GetComponentsInChildren<Collider>(true), Is.Empty);
                Assert.That(visuals.GetComponentsInChildren<Rigidbody>(true), Is.Empty);

                int enabledPointLights = 0;
                foreach (Light light in visuals.GetComponentsInChildren<Light>(true))
                {
                    if (light.type == LightType.Point && light.enabled)
                    {
                        enabledPointLights++;
                        Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
                    }
                }

                Assert.That(enabledPointLights, Is.LessThanOrEqualTo(1));
                long triangles = 0;
                foreach (MeshFilter filter in visuals.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = filter.sharedMesh;
                    Assert.That(mesh, Is.Not.Null);
                    for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                    {
                        triangles += mesh.GetIndexCount(submesh) / 3;
                    }
                }

                Assert.That(triangles, Is.InRange(1L, 65000L));
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        [TestCase("AshenCauseway_Stone", 0.88f, 0.81f, 0.68f)]
        [TestCase("AshenCauseway_StoneVariation", 0.69f, 0.72f, 0.66f)]
        [TestCase("AshenCauseway_Cinder", 0.36f, 0.32f, 0.26f)]
        [TestCase("AshenCauseway_Grout", 0.23f, 0.22f, 0.20f)]
        [TestCase("AshenCauseway_Masonry", 0.92f, 0.86f, 0.73f)]
        [TestCase("AshenCauseway_Paving", 0.32f, 0.33f, 0.30f)]
        public void AuthoredWorldSurfaces_KeepReadablePaletteAndTexturedRelief(
            string materialName, float red, float green, float blue)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/" + materialName + ".mat");
            Assert.That(material, Is.Not.Null, materialName);
            Assert.That(material.shader, Is.Not.Null, materialName);
            Assert.That(material.HasProperty("_BaseColor"), Is.True, materialName);
            Color tint = material.GetColor("_BaseColor");
            Assert.That(tint.r, Is.EqualTo(red).Within(0.0001f), materialName);
            Assert.That(tint.g, Is.EqualTo(green).Within(0.0001f), materialName);
            Assert.That(tint.b, Is.EqualTo(blue).Within(0.0001f), materialName);
            Assert.That(tint.a, Is.EqualTo(1f).Within(0.0001f), materialName);
            AssertTexturedSurface(material);
        }

        [TestCase("AshenCauseway_Moss", 0.24f, 0.28f, 0.13f)]
        [TestCase("AshenCauseway_DryGrass", 0.38f, 0.33f, 0.21f)]
        public void AuthoredVegetation_UsesOpaqueReadableMaterials(
            string materialName, float red, float green, float blue)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/" + materialName + ".mat");
            Assert.That(material, Is.Not.Null, materialName);
            Assert.That(material.shader, Is.Not.Null, materialName);
            Color tint = material.GetColor("_BaseColor");
            Assert.That(tint.r, Is.EqualTo(red).Within(0.0001f));
            Assert.That(tint.g, Is.EqualTo(green).Within(0.0001f));
            Assert.That(tint.b, Is.EqualTo(blue).Within(0.0001f));
            Assert.That(tint.a, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(material.GetFloat("_Surface"), Is.Zero,
                "Small vegetation is bounded opaque geometry, not another transparent-overdraw layer.");
        }

        [Test]
        public void DetailAtlas_PreservesSourceAtlasTextureAndUvTransform()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/AshenCauseway_DetailAtlas.mat");
            Texture2D atlas = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/ThirdParty/KayKit/Dungeon/Environment/dungeon_texture.png");
            Assert.That(material, Is.Not.Null);
            Assert.That(material.shader, Is.Not.Null);
            Assert.That(atlas, Is.Not.Null);
            Assert.That(material.GetTexture("_BaseMap"), Is.SameAs(atlas));
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(Vector2.one));
            Assert.That(material.GetTextureOffset("_BaseMap"), Is.EqualTo(Vector2.zero));
            Color tint = material.GetColor("_BaseColor");
            Assert.That(tint.r, Is.EqualTo(0.62f).Within(0.0001f));
            Assert.That(tint.g, Is.EqualTo(0.57f).Within(0.0001f));
            Assert.That(tint.b, Is.EqualTo(0.49f).Within(0.0001f));
            Assert.That(tint.a, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void WorldBackdropMaterial_IsDedicatedUnlitAndDoesNotReturnToBlack()
        {
            Material haze = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/AshenCauseway_DistantHaze.mat");
            Material arenaAbyss = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/Abyss.mat");
            Assert.That(haze, Is.Not.Null);
            Assert.That(arenaAbyss, Is.Not.Null);
            Assert.That(haze, Is.Not.SameAs(arenaAbyss),
                "Making the PvE landscape readable must not recolor the Arena abyss.");
            Assert.That(haze.shader, Is.Not.Null);
            Assert.That(haze.shader.name, Is.EqualTo("Universal Render Pipeline/Unlit"));
            Color tint = haze.GetColor("_BaseColor");
            Assert.That(tint.r, Is.GreaterThanOrEqualTo(0.10f));
            Assert.That(tint.g, Is.GreaterThanOrEqualTo(0.10f));
            Assert.That(tint.b, Is.GreaterThanOrEqualTo(0.10f));
            Assert.That(tint.maxColorComponent, Is.LessThanOrEqualTo(0.40f),
                "Distant haze should frame the road, not become a bright competing surface.");
            Assert.That(tint.a, Is.EqualTo(1f).Within(0.0001f));
        }

        private static void AssertDedicatedWorldBackdrop(Transform visuals)
        {
            Transform backdrop = visuals.Find("AshenCausewayAbyssBackdrop");
            Assert.That(backdrop, Is.Not.Null);
            MeshRenderer renderer = backdrop.GetComponent<MeshRenderer>();
            Assert.That(renderer, Is.Not.Null);
            Material haze = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/AshenCauseway_DistantHaze.mat");
            Assert.That(haze, Is.Not.Null);
            Assert.That(renderer.sharedMaterial, Is.SameAs(haze),
                "Every generated world chunk must bind its dedicated haze rather than the global abyss.");
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(renderer.receiveShadows, Is.False);
        }

        private static void AssertRegionDetails(Transform visuals)
        {
            Transform details = visuals.Find("WorldRegionDetails");
            Assert.That(details, Is.Not.Null,
                "Every district needs retained near-field scenery, not only distant silhouettes.");
            string[] requiredGroups = { "ShoulderClusters", "DistrictLandmark", "Vegetation" };
            foreach (string groupName in requiredGroups)
            {
                Transform group = details.Find(groupName);
                Assert.That(group, Is.Not.Null, groupName);
                Assert.That(group.GetComponentsInChildren<MeshFilter>(true), Is.Not.Empty,
                    groupName + " must contain actual geometry.");
            }
            AssertVegetationHasNoFlatGroundDiscs(details.Find("Vegetation"), visuals);

            MeshRenderer[] renderers = details.GetComponentsInChildren<MeshRenderer>(true);
            Assert.That(renderers.Length, Is.InRange(1, 24));
            Assert.That(CountTriangles(details), Is.InRange(1L, 8000L));
            foreach (MeshFilter filter in details.GetComponentsInChildren<MeshFilter>(true))
            {
                Assert.That(filter.sharedMesh, Is.Not.Null, filter.name);
                Bounds localBounds = GetBoundsInVisualSpace(filter, visuals);
                if (localBounds.max.y > 0.075f)
                {
                    Assert.That(localBounds.min.z >= 8.15f - 0.001f ||
                        localBounds.max.z <= -8.15f + 0.001f, Is.True,
                        filter.name + " must not suggest a collision obstacle in the Core walkable lane.");
                }
            }

            foreach (MeshRenderer renderer in renderers)
            {
                Assert.That(renderer.sharedMaterials, Is.Not.Empty, renderer.name);
                foreach (Material material in renderer.sharedMaterials)
                {
                    Assert.That(material, Is.Not.Null, renderer.name);
                    Assert.That(material.shader, Is.Not.Null, renderer.name);
                }
            }

            Assert.That(details.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(details.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(details.GetComponentsInChildren<Animator>(true), Is.Empty);
            Assert.That(details.GetComponentsInChildren<Light>(true), Is.Empty);
            Assert.That(details.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
        }

        private static void AssertFlatPavingDetails(Transform visuals)
        {
            Transform paving = visuals.Find("CausewayPavingDetails");
            Assert.That(paving, Is.Not.Null,
                "The readable road surface requires authored paving detail.");
            Assert.That(CountTriangles(paving), Is.InRange(1L, 6000L));
            Material pavingMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/AshenCauseway_Paving.mat");
            Material groutMaterial = AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/SessionRpg/Art/Materials/AshenCauseway_Grout.mat");
            Texture2D roughStone = AssetDatabase.LoadAssetAtPath<Texture2D>(
                "Assets/SessionRpg/Art/Environment/Textures/ArenaFloorSurfaceAlbedo.png");
            Assert.That(pavingMaterial, Is.Not.Null);
            Assert.That(groutMaterial, Is.Not.Null);
            Assert.That(roughStone, Is.Not.Null);
            Assert.That(pavingMaterial.GetTexture("_BaseMap"), Is.SameAs(roughStone),
                "Broad paving must use rough stone relief, not repeated cobblestone-shaped patches.");
            foreach (MeshFilter filter in paving.GetComponentsInChildren<MeshFilter>(true))
            {
                Assert.That(filter.sharedMesh, Is.Not.Null, filter.name);
                Assert.That(filter.sharedMesh.subMeshCount, Is.EqualTo(3), filter.name);
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                Assert.That(renderer, Is.Not.Null, filter.name);
                Material[] materials = renderer.sharedMaterials;
                Assert.That(materials, Has.Length.EqualTo(3), filter.name);
                Assert.That(materials[0], Is.SameAs(pavingMaterial), filter.name);
                Assert.That(materials[1], Is.SameAs(pavingMaterial), filter.name);
                Assert.That(materials[2], Is.SameAs(groutMaterial), filter.name);
                Bounds bounds = GetBoundsInVisualSpace(filter, visuals);
                Assert.That(bounds.max.y, Is.LessThanOrEqualTo(0.075f + 0.0001f),
                    filter.name + " must remain flat dressing, not false playable elevation.");
                Matrix4x4 toVisual = visuals.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                foreach (Vector3 vertex in filter.sharedMesh.vertices)
                {
                    Assert.That(Mathf.Abs(toVisual.MultiplyPoint3x4(vertex).z),
                        Is.GreaterThanOrEqualTo(5.5f),
                        "Paving belongs at the road edge, not as high-contrast interior lane markings.");
                }
            }

            Assert.That(paving.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(paving.GetComponentsInChildren<Rigidbody>(true), Is.Empty);
            Assert.That(paving.GetComponentsInChildren<Light>(true), Is.Empty);
            Assert.That(paving.GetComponentsInChildren<ParticleSystem>(true), Is.Empty);
        }

        private static void AssertVegetationHasNoFlatGroundDiscs(
            Transform vegetation, Transform visuals)
        {
            foreach (MeshFilter filter in vegetation.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                Assert.That(mesh, Is.Not.Null, filter.name);
                Vector3[] vertices = mesh.vertices;
                Matrix4x4 matrix = visuals.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    Assert.That(mesh.GetTopology(submesh), Is.EqualTo(MeshTopology.Triangles));
                    int[] indices = mesh.GetIndices(submesh);
                    for (int index = 0; index < indices.Length; index += 3)
                    {
                        Vector3 a = matrix.MultiplyPoint3x4(vertices[indices[index]]);
                        Vector3 b = matrix.MultiplyPoint3x4(vertices[indices[index + 1]]);
                        Vector3 c = matrix.MultiplyPoint3x4(vertices[indices[index + 2]]);
                        float minimumY = Mathf.Min(a.y, Mathf.Min(b.y, c.y));
                        float maximumY = Mathf.Max(a.y, Mathf.Max(b.y, c.y));
                        float planarArea = Mathf.Abs((b.x - a.x) * (c.z - a.z) -
                            (b.z - a.z) * (c.x - a.x)) * 0.5f;
                        bool isFlatGroundPatch = maximumY <= 0.1f &&
                            maximumY - minimumY <= 0.01f && planarArea > 0.005f;
                        Assert.That(isFlatGroundPatch, Is.False,
                            filter.name + " must use dimensional vegetation, not flat colored ground discs.");
                    }
                }
            }
        }

        private static void AssertTexturedSurface(Material material)
        {
            string[] textureProperties = { "_BaseMap", "_BumpMap" };
            foreach (string property in textureProperties)
            {
                Assert.That(material.HasProperty(property), Is.True, material.name + property);
                Assert.That(material.GetTexture(property), Is.Not.Null, material.name + property);
                Vector2 scale = material.GetTextureScale(property);
                Assert.That(scale.x, Is.GreaterThan(0f), material.name + property);
                Assert.That(scale.y, Is.GreaterThan(0f), material.name + property);
                Assert.That(float.IsInfinity(scale.x) || float.IsNaN(scale.x) ||
                    float.IsInfinity(scale.y) || float.IsNaN(scale.y), Is.False);
            }

            Assert.That(material.HasProperty("_BumpScale"), Is.True, material.name);
            Assert.That(material.GetFloat("_BumpScale"), Is.GreaterThan(0f), material.name);
        }

        private static long CountTriangles(Transform root)
        {
            long count = 0;
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                Assert.That(mesh, Is.Not.Null, filter.name);
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (mesh.GetTopology(submesh) == MeshTopology.Triangles)
                    {
                        count += mesh.GetIndexCount(submesh) / 3;
                    }
                }
            }

            return count;
        }

        private static Bounds GetBoundsInVisualSpace(MeshFilter filter, Transform visuals)
        {
            Bounds bounds = filter.sharedMesh.bounds;
            Matrix4x4 matrix = visuals.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds transformed = new Bounds(matrix.MultiplyPoint3x4(bounds.min), Vector3.zero);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int y = -1; y <= 1; y += 2)
                {
                    for (int z = -1; z <= 1; z += 2)
                    {
                        Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                            new Vector3(x, y, z));
                        transformed.Encapsulate(matrix.MultiplyPoint3x4(corner));
                    }
                }
            }

            return transformed;
        }
    }
}
