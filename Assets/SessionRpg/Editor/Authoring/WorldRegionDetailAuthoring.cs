using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>
    /// Bounded, visual-only district dioramas: original weathered masonry and
    /// bent vegetation, with retained CC0 KayKit wood supplies only at service
    /// camps. Source FBXs/importers are untouched; no gameplay collision exists.
    /// </summary>
    internal static class WorldRegionDetailAuthoring
    {
        private const string SourceRoot = "Assets/ThirdParty/KayKit/Dungeon/Environment/";
        private const string MeshFolder = "Assets/SessionRpg/Art/Environment/WorldDetails";
        private const int MaximumRenderers = 24;
        private const int MaximumTriangles = 8000;
        private const float MinimumShoulderZ = 8.15f;
        // Shared masonry material multiplies UV by 2.65. This produces one
        // 2.5-meter source-texture repeat rather than miniature brick wallpaper.
        private const float MasonryUvScale = 0.15f;

        public static void Build(Transform visuals, AuthoringAssets assets, int seed, string[] poiRecipeIds)
        {
            if (visuals == null) { throw new ArgumentNullException(nameof(visuals)); }
            if (assets == null) { throw new ArgumentNullException(nameof(assets)); }
            if (poiRecipeIds == null) { throw new ArgumentNullException(nameof(poiRecipeIds)); }
            if (visuals.Find("WorldRegionDetails") != null)
            {
                throw new InvalidOperationException("World region details must be authored exactly once per chunk.");
            }
            if (assets.KayKitMaterial == null || assets.AshenCausewayMasonry == null || assets.AshenCausewayMoss == null ||
                assets.AshenCausewayDryGrass == null)
            {
                throw new InvalidOperationException("World details require the shared atlas, moss, and dry-grass materials.");
            }

            Transform root = Child(visuals, "WorldRegionDetails");
            Transform shoulders = Child(root, "ShoulderClusters");
            Transform landmark = Child(root, "DistrictLandmark");
            Transform vegetation = Child(root, "Vegetation");
            float[] centers = { -18f, -6f, 6f, 18f };
            for (int index = 0; index < centers.Length; index++)
            {
                Transform cluster = Child(shoulders, "ShoulderCluster_" + index);
                // Denser original piles anchor the camp; low-poly repeats share
                // exactly the weathered masonry used by district architecture.
                if (index == 1)
                {
                    AddOriginalRubble(cluster, "NorthCollapsedMasonry", assets.AshenCausewayMasonry,
                        new Vector3(centers[index], 0.02f, 9.65f), unchecked(seed + index * 101), "North", 8);
                }
                else
                {
                    AddOriginalRubble(cluster, "NorthMasonryScatter", assets.AshenCausewayMasonry,
                        new Vector3(centers[index], 0.02f, 9.65f), unchecked(seed + index * 101), "North");
                }
                if (index == 2)
                {
                    AddOriginalRubble(cluster, "SouthCollapsedMasonry", assets.AshenCausewayMasonry,
                        new Vector3(centers[index] + 1.2f, 0.02f, -9.7f), unchecked(seed + index * 101), "South", 8);
                }
                else
                {
                    AddOriginalRubble(cluster, "SouthMasonryScatter", assets.AshenCausewayMasonry,
                        new Vector3(centers[index] + 1.2f, 0.02f, -9.7f), unchecked(seed + index * 101), "South");
                }
            }

            BuildDistrict(landmark, assets, poiRecipeIds);
            BuildVegetation(vegetation, assets, seed, true);
            BuildVegetation(vegetation, assets, seed, false);
            Validate(root, visuals);
        }

        private static void BuildDistrict(Transform root, AuthoringAssets assets, string[] recipes)
        {
            Material stone = assets.AshenCausewayMasonry;
            Material atlas = assets.KayKitMaterial;
            if (Contains(recipes, "west_refuge"))
            {
                AddModel(root, "RefugeArcade", "wall_doorway", stone, new Vector3(6f, 0f, 14f), 3.4f, 2.3f, 0f);
                AddModel(root, "RefugeColumnWest", "column", stone, new Vector3(1.9f, 0f, 12.7f), 3.1f, 0.95f, 0f);
                AddModel(root, "RefugeColumnEast", "column", stone, new Vector3(10.1f, 0f, 12.7f), 2.5f, 0.95f, 10f);
                AddService(root, atlas, -6f);
            }
            else if (Contains(recipes, "cinder_approach"))
            {
                AddModel(root, "OutriderBrokenShelter", "wall_broken", stone, new Vector3(-5.5f, 0f, 12.8f), 2.3f, 2.1f, 18f);
                AddModel(root, "OutriderWatchColumn", "column", stone, new Vector3(6f, 0f, 12.6f), 3.4f, 0.9f, -8f);
                AddModel(root, "OutriderBarricade", "barrier", stone, new Vector3(18f, 0f, 10.2f), 1.15f, 1.6f, -10f);
                AddService(root, atlas, 1.5f);
            }
            else if (Contains(recipes, "broken_gate"))
            {
                AddModel(root, "GateSurvivingArch", "wall_doorway", stone, new Vector3(-1.75f, 0f, 14.1f), 3.8f, 2.4f, 0f);
                AddModel(root, "GateStandingColumn", "column", stone, new Vector3(-6.1f, 0f, 13.1f), 3.7f, 1f, -8f);
                AddModel(root, "GateBrokenColumn", "pillar", stone, new Vector3(3.1f, 0f, 12.8f), 1.6f, 0.8f, 12f);
                AddModel(root, "GateCollapsedWall", "wall_broken", stone, new Vector3(12f, 0f, 12.4f), 1.8f, 2.1f, -18f);
            }
            else if (Contains(recipes, "collapsed_court"))
            {
                AddModel(root, "CourtColumnWest", "column", stone, new Vector3(-7f, 0f, 13f), 3.5f, 1f, 0f);
                AddModel(root, "CourtColumnMiddle", "column", stone, new Vector3(1f, 0f, 13f), 2.6f, 1f, 5f);
                AddModel(root, "CourtColumnEast", "column", stone, new Vector3(9f, 0f, 13f), 3.2f, 1f, -5f);
                AddModel(root, "CourtArchRemnant", "wall_doorway", stone, new Vector3(5f, 0f, 15.8f), 3.5f, 2.3f, 0f);
                AddModel(root, "CourtCollapsedWing", "wall_broken", stone, new Vector3(-16f, 0f, 12.5f), 1.7f, 2.1f, 24f);
            }
            else if (Contains(recipes, "ossuary_bend"))
            {
                AddModel(root, "OssuaryMemorialWest", "pillar", stone, new Vector3(-17f, 0f, 12.6f), 2.8f, 1f, 9f);
                AddModel(root, "OssuaryCollapsedScreen", "wall_broken", stone, new Vector3(0f, 0f, 13.2f), 2.4f, 2.2f, -12f);
                AddModel(root, "OssuaryMemorialEast", "column", stone, new Vector3(17f, 0f, 12.9f), 3.1f, 1f, -8f);
            }
            else if (Contains(recipes, "reliquary_yard"))
            {
                AddModel(root, "ReliquaryColumnWest", "column", stone, new Vector3(-15f, 0f, 13f), 3.1f, 0.95f, 0f);
                AddModel(root, "ReliquaryColumnCenterWest", "column", stone, new Vector3(-6f, 0f, 13f), 3.7f, 0.95f, 0f);
                AddModel(root, "ReliquaryColumnCenterEast", "column", stone, new Vector3(3f, 0f, 13f), 3.7f, 0.95f, 0f);
                AddModel(root, "ReliquaryColumnEast", "column", stone, new Vector3(12f, 0f, 13f), 2.3f, 0.95f, 8f);
                AddModel(root, "ReliquaryProcessionalArch", "wall_doorway", stone, new Vector3(-1.5f, 0f, 16f), 3.8f, 2.4f, 0f);
            }
            else if (Contains(recipes, "east_watch"))
            {
                AddModel(root, "WardenWatchArch", "wall_doorway", stone, new Vector3(-10.5f, 0f, 14.2f), 4f, 2.4f, 0f);
                AddModel(root, "WardenColumnWest", "column", stone, new Vector3(-15.3f, 0f, 13f), 4f, 1.05f, -5f);
                AddModel(root, "WardenColumnEast", "column", stone, new Vector3(-5.7f, 0f, 13f), 4f, 1.05f, 5f);
                AddModel(root, "WatchBreachedWing", "wall_broken", stone, new Vector3(9f, 0f, 12.7f), 2.6f, 2.1f, 18f);
            }
        }

        private static void AddService(Transform root, Material atlas, float x)
        {
            AddModel(root, "ServiceCrates", "crates_stacked", atlas,
                new Vector3(x, 0.03f, 9.7f), 1.15f, 1.2f, -15f);
            AddModel(root, "ServiceBarrel", "barrel_large", atlas,
                new Vector3(x + 2f, 0.03f, 9.55f), 1f, 0.7f, 12f);
        }

        private static void AddOriginalRubble(Transform parent, string name, Material material,
            Vector3 position, int seed, string side, int stoneCount = 4)
        {
            List<Vector3> vertices = new List<Vector3>(240);
            List<Vector3> normals = new List<Vector3>(240);
            List<Vector2> uv = new List<Vector2>(240);
            List<int> triangles = new List<int>(240);
            uint random = unchecked((uint)seed) ^ (side == "North" ? 0xab192d51u : 0x192b7a31u);
            for (int stone = 0; stone < stoneCount; stone++)
            {
                float placement = stone * Mathf.PI * 2f / stoneCount + Next(ref random) * 0.35f;
                Vector3 center = new Vector3(Mathf.Cos(placement) * 0.62f,
                    stone >= 4 ? 0.18f : 0f, Mathf.Sin(placement) * 0.62f);
                float radius = 0.33f + Next(ref random) * 0.16f;
                float height = 0.35f + Next(ref random) * 0.4f;
                float yaw = Next(ref random) * Mathf.PI * 2f;
                Vector3[] bottom = new Vector3[6];
                Vector3[] top = new Vector3[6];
                for (int corner = 0; corner < 6; corner++)
                {
                    float angle = yaw + corner * Mathf.PI / 3f;
                    float irregularRadius = radius * (0.8f + Next(ref random) * 0.2f);
                    Vector3 offset = new Vector3(Mathf.Cos(angle) * irregularRadius, 0f,
                        Mathf.Sin(angle) * irregularRadius);
                    bottom[corner] = center + offset;
                    top[corner] = center + offset * 0.73f + new Vector3(0.035f,
                        height + Next(ref random) * 0.06f, -0.025f);
                }
                for (int corner = 0; corner < 6; corner++)
                {
                    int next = (corner + 1) % 6;
                    AddStoneTriangle(bottom[corner], top[corner], top[next], vertices, normals, uv, triangles);
                    AddStoneTriangle(bottom[corner], top[next], bottom[next], vertices, normals, uv, triangles);
                }
                for (int corner = 1; corner < 5; corner++)
                {
                    AddStoneTriangle(top[0], top[corner + 1], top[corner], vertices, normals, uv, triangles);
                    AddStoneTriangle(bottom[0], bottom[corner], bottom[corner + 1], vertices, normals, uv, triangles);
                }
            }

            Mesh authored = new Mesh { name = "ShoulderRubble_" + seed + "_" + side };
            authored.SetVertices(vertices);
            authored.SetNormals(normals);
            authored.SetUVs(0, uv);
            authored.SetTriangles(triangles, 0);
            authored.RecalculateBounds();
            authored.RecalculateTangents();
            Transform root = Child(parent, name);
            root.localPosition = position;
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = PersistMesh(authored, authored.name + ".asset");
            MeshRenderer renderer = root.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(root.gameObject, StaticEditorFlags.BatchingStatic);
        }

        private static void AddStoneTriangle(Vector3 a, Vector3 b, Vector3 c,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int first = vertices.Count;
            Vector3[] points = { a, b, c };
            for (int index = 0; index < 3; index++)
            {
                Vector3 point = points[index];
                vertices.Add(point);
                normals.Add(normal);
                uv.Add((Mathf.Abs(normal.y) > 0.65f
                    ? new Vector2(point.x, point.z)
                    : Mathf.Abs(normal.x) > Mathf.Abs(normal.z)
                        ? new Vector2(point.z, point.y)
                        : new Vector2(point.x, point.y)) * MasonryUvScale);
                triangles.Add(first + index);
            }
        }

        private static void AddModel(Transform parent, string name, string sourceName,
            Material atlas, Vector3 position, float height, float maximumRadius, float yaw)
        {
            if (sourceName == "column" || sourceName == "pillar" || sourceName == "wall_doorway" ||
                sourceName == "wall_broken" || sourceName == "barrier")
            {
                AddMasonryFeature(parent, name, sourceName, atlas, position, height, maximumRadius, yaw);
                return;
            }
            string path = SourceRoot + sourceName + ".fbx";
            GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (source == null) { throw new InvalidOperationException("Missing retained CC0 world detail: " + path); }
            Transform root = Child(parent, name);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            GameObject model = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (model == null) { throw new InvalidOperationException("Could not instantiate world detail: " + path); }
            model.name = sourceName + "_Source";
            model.transform.SetParent(root, false);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one;

            Component[] components = model.GetComponentsInChildren<Component>(true);
            for (int index = components.Length - 1; index >= 0; index--)
            {
                Component component = components[index];
                if (!(component is Transform) && !(component is MeshFilter) && !(component is MeshRenderer))
                {
                    Object.DestroyImmediate(component);
                }
            }
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            if (filters.Length == 0) { throw new InvalidOperationException("World detail has no static mesh: " + path); }
            Bounds local = new Bounds();
            bool initialized = false;
            for (int index = 0; index < filters.Length; index++)
            {
                MeshFilter filter = filters[index];
                Mesh mesh = filter.sharedMesh;
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (mesh == null || renderer == null) { throw new InvalidOperationException("Malformed static world detail: " + path); }
                Encapsulate(mesh.bounds, model.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix,
                    ref local, ref initialized);
                Material[] materials = new Material[mesh.subMeshCount];
                for (int slot = 0; slot < materials.Length; slot++) { materials[slot] = atlas; }
                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            if (local.size.y < 0.0001f) { throw new InvalidOperationException("World detail has no usable height: " + path); }
            float radius = new Vector2(local.extents.x, local.extents.z).magnitude;
            float scale = Mathf.Min(height / local.size.y, maximumRadius / Mathf.Max(0.001f, radius));
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = -new Vector3(local.center.x, local.min.y, local.center.z) * scale;
            foreach (Transform transform in model.GetComponentsInChildren<Transform>(true))
            {
                transform.gameObject.layer = 2;
                GameObjectUtility.SetStaticEditorFlags(transform.gameObject, StaticEditorFlags.BatchingStatic);
            }
        }

        private static void AddMasonryFeature(Transform parent, string name, string kind,
            Material stone, Vector3 position, float height, float maximumRadius, float yaw)
        {
            MasonryBuilder builder = new MasonryBuilder();
            if (kind == "wall_doorway")
            {
                const float innerRadius = 1.05f;
                const float outerRadius = 1.4f;
                float springY = height - outerRadius;
                for (int side = -1; side <= 1; side += 2)
                {
                    builder.Block(new Vector3(side * 1.24f, 0.11f, 0f), new Vector3(0.99f, 0.22f, 0.91f), 0f, side + 3);
                    float courseHeight = (springY - 0.2f) / 4f;
                    for (int course = 0; course < 4; course++)
                    {
                        builder.Block(new Vector3(side * 1.24f + (course % 2 == 0 ? 0.02f : -0.02f),
                            0.2f + (course + 0.5f) * courseHeight, 0f),
                            new Vector3(0.72f, courseHeight - 0.025f, 0.76f), (course % 3 - 1) * 1.5f, course + side + 8);
                    }
                }
                for (int segment = 0; segment < 9; segment++)
                {
                    if (name.Contains("Remnant") && segment == 1) { continue; }
                    float from = segment * Mathf.PI / 9f + 0.013f;
                    float to = (segment + 1) * Mathf.PI / 9f - 0.013f;
                    builder.ArchStone(innerRadius, outerRadius, from, to, springY, 0.75f);
                }
            }
            else if (kind == "column" || kind == "pillar")
            {
                int courses = Mathf.Clamp(Mathf.CeilToInt(height / 0.55f), 3, 6);
                builder.Block(new Vector3(0f, 0.12f, 0f), new Vector3(1.05f, 0.24f, 1.03f), 0f, 3);
                float courseHeight = (height - 0.4f) / courses;
                for (int course = 0; course < courses; course++)
                {
                    float taper = 1f - course * 0.025f;
                    builder.Block(new Vector3(Mathf.Sin(course * 2.4f) * 0.025f,
                        0.24f + (course + 0.5f) * courseHeight, 0f),
                        new Vector3(0.82f * taper, courseHeight - 0.024f, 0.8f * taper), course % 2 == 0 ? 1.5f : -1.5f, course + 13);
                }
                builder.Block(new Vector3(0f, height - 0.08f, 0f), new Vector3(0.92f, 0.16f, 0.9f), -2f, 29);
            }
            else
            {
                // A staggered, genuinely broken wall profile, never a flat door
                // panel. The highest courses taper into two disconnected teeth.
                float courseHeight = height / 3f;
                for (int course = 0; course < 3; course++)
                {
                    int count = course == 0 ? 3 : course == 1 ? 2 : 1;
                    for (int block = 0; block < count; block++)
                    {
                        float x = block * 0.97f - (count - 1) * 0.485f + (course == 1 ? -0.43f : 0f);
                        builder.Block(new Vector3(x, (course + 0.5f) * courseHeight, 0f),
                            new Vector3(0.94f, courseHeight - 0.03f, 0.78f), (block - 1) * 2f, course * 3 + block + 31);
                    }
                }
            }

            Mesh mesh = PersistMesh(builder.Finish("WeatheredMasonry_" + name), "WeatheredMasonry_" + name + ".asset");
            float radius = new Vector2(mesh.bounds.extents.x, mesh.bounds.extents.z).magnitude;
            float scale = Mathf.Min(height / mesh.bounds.size.y, maximumRadius / radius);
            Transform root = Child(parent, name);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform geometry = Child(root, "WeatheredMasonry");
            geometry.localScale = Vector3.one * scale;
            geometry.localPosition = -new Vector3(mesh.bounds.center.x, mesh.bounds.min.y, mesh.bounds.center.z) * scale;
            geometry.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = geometry.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = stone;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(geometry.gameObject, StaticEditorFlags.BatchingStatic);
        }

        private sealed class MasonryBuilder
        {
            private readonly List<Vector3> vertices = new List<Vector3>();
            private readonly List<Vector3> normals = new List<Vector3>();
            private readonly List<Vector2> uv = new List<Vector2>();
            private readonly List<int> triangles = new List<int>();

            public void Block(Vector3 center, Vector3 size, float yaw, int salt)
            {
                Vector2[] profile =
                {
                    new Vector2(-0.5f, -0.32f), new Vector2(-0.32f, -0.5f),
                    new Vector2(0.32f, -0.5f), new Vector2(0.5f, -0.32f),
                    new Vector2(0.5f, 0.32f), new Vector2(0.32f, 0.5f),
                    new Vector2(-0.32f, 0.5f), new Vector2(-0.5f, 0.32f),
                };
                Vector3[,] rings = new Vector3[4, 8];
                Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
                float bevel = Mathf.Min(0.055f, size.y * 0.2f);
                for (int ring = 0; ring < 4; ring++)
                {
                    float elevation = ring == 0 ? -size.y * 0.5f : ring == 1 ? -size.y * 0.5f + bevel :
                        ring == 2 ? size.y * 0.5f - bevel : size.y * 0.5f;
                    float inset = ring == 0 || ring == 3 ? 0.9f : 1f;
                    for (int corner = 0; corner < 8; corner++)
                    {
                        float wear = 0.97f + Mathf.Sin((salt + corner) * 2.73f) * 0.025f;
                        Vector3 point = new Vector3(profile[corner].x * size.x * inset * wear,
                            elevation, profile[corner].y * size.z * inset * wear);
                        rings[ring, corner] = center + rotation * point;
                    }
                }
                for (int ring = 0; ring < 3; ring++)
                {
                    for (int corner = 0; corner < 8; corner++)
                    {
                        int next = (corner + 1) % 8;
                        Quad(rings[ring, corner], rings[ring + 1, corner], rings[ring + 1, next], rings[ring, next]);
                    }
                }
                for (int corner = 1; corner < 7; corner++)
                {
                    Triangle(rings[0, 0], rings[0, corner], rings[0, corner + 1]);
                    Triangle(rings[3, 0], rings[3, corner + 1], rings[3, corner]);
                }
            }

            public void ArchStone(float inner, float outer, float from, float to, float elevation, float depth)
            {
                Vector3 a = new Vector3(Mathf.Cos(from) * inner, elevation + Mathf.Sin(from) * inner, -depth * 0.5f);
                Vector3 b = new Vector3(Mathf.Cos(to) * inner, elevation + Mathf.Sin(to) * inner, -depth * 0.5f);
                Vector3 c = new Vector3(Mathf.Cos(to) * outer, elevation + Mathf.Sin(to) * outer, -depth * 0.5f);
                Vector3 d = new Vector3(Mathf.Cos(from) * outer, elevation + Mathf.Sin(from) * outer, -depth * 0.5f);
                Vector3 back = new Vector3(0f, 0f, depth);
                Quad(a, b, c, d);
                Quad(a + back, d + back, c + back, b + back);
                Quad(a, a + back, b + back, b);
                Quad(b, b + back, c + back, c);
                Quad(c, c + back, d + back, d);
                Quad(d, d + back, a + back, a);
            }

            public Mesh Finish(string name)
            {
                Mesh mesh = new Mesh { name = name };
                mesh.SetVertices(vertices);
                mesh.SetNormals(normals);
                mesh.SetUVs(0, uv);
                mesh.SetTriangles(triangles, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();
                return mesh;
            }

            private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Triangle(a, b, c);
                Triangle(a, c, d);
            }

            private void Triangle(Vector3 a, Vector3 b, Vector3 c)
            {
                AddStoneTriangle(a, b, c, vertices, normals, uv, triangles);
            }
        }

        private static void BuildVegetation(Transform root, AuthoringAssets assets, int seed, bool north)
        {
            List<Vector3> vertices = new List<Vector3>(700);
            List<Vector3> normals = new List<Vector3>(700);
            List<Vector2> uv = new List<Vector2>(700);
            List<int> moss = new List<int>(360);
            List<int> grass = new List<int>(720);
            uint random = unchecked((uint)seed) ^ (north ? 0x4b1d3a91u : 0x853c9e27u);
            float[] patchCenters = { -21f, -12f, 0f, 11f, 21f };
            int[] patchCounts = { 2, 3, 2, 4, 3 };
            int clump = 0;
            for (int patch = 0; patch < patchCenters.Length; patch++)
            {
                for (int tuft = 0; tuft < patchCounts[patch]; tuft++, clump++)
                {
                    float x = patchCenters[patch] + (Next(ref random) - 0.5f) * 2.2f;
                    float z = (north ? 1f : -1f) * (8.85f + Next(ref random) * 1.1f);
                    for (int blade = 0; blade < 4; blade++)
                    {
                        float angle = Next(ref random) * Mathf.PI * 2f;
                        Vector3 side = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.026f + Next(ref random) * 0.018f);
                        Vector3 lean = new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (0.14f + Next(ref random) * 0.12f);
                        Vector3 bottom = new Vector3(x + (Next(ref random) - 0.5f) * 0.25f, 0.018f,
                            z + (Next(ref random) - 0.5f) * 0.25f);
                        float height = 0.2f + Next(ref random) * 0.3f;
                        Vector3 bend = bottom + Vector3.up * height * 0.68f + lean * 0.3f;
                        Vector3 tip = bottom + Vector3.up * height + lean;
                        List<int> materialTriangles = (clump + blade) % 4 == 0 ? moss : grass;
                        AddQuad(bottom - side, bottom + side, bend + side * 0.52f, bend - side * 0.52f,
                            vertices, normals, uv, materialTriangles);
                        AddStoneTriangle(bend - side * 0.52f, bend + side * 0.52f, tip, vertices, normals, uv, materialTriangles);
                        AddStoneTriangle(tip, bend + side * 0.52f, bend - side * 0.52f, vertices, normals, uv, materialTriangles);
                    }
                }
            }

            Mesh authored = new Mesh { name = "ShoulderVegetation_" + (north ? "North" : "South") };
            authored.SetVertices(vertices);
            authored.SetNormals(normals);
            authored.SetUVs(0, uv);
            authored.subMeshCount = 2;
            authored.SetTriangles(moss, 0);
            authored.SetTriangles(grass, 1);
            authored.RecalculateBounds();
            Mesh mesh = PersistMesh(authored,
                "ShoulderVegetation_" + seed + (north ? "_North.asset" : "_South.asset"));
            Transform visual = Child(root, mesh.name);
            visual.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = visual.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = new[] { assets.AshenCausewayMoss, assets.AshenCausewayDryGrass };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(visual.gameObject, StaticEditorFlags.BatchingStatic);
        }

        private static Mesh PersistMesh(Mesh authored, string fileName)
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
            {
                AssetDatabase.CreateFolder("Assets/SessionRpg/Art/Environment", "WorldDetails");
            }
            string path = MeshFolder + "/" + fileName;
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = authored;
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.CopySerialized(authored, mesh);
                Object.DestroyImmediate(authored);
                EditorUtility.SetDirty(mesh);
            }
            return mesh;
        }

        private static void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            List<Vector3> vertices, List<Vector3> normals, List<Vector2> uv, List<int> triangles)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            for (int side = 0; side < 2; side++)
            {
                int first = vertices.Count;
                vertices.Add(a); vertices.Add(b); vertices.Add(c); vertices.Add(d);
                for (int index = 0; index < 4; index++) { normals.Add(side == 0 ? normal : -normal); }
                uv.Add(Vector2.zero); uv.Add(Vector2.right); uv.Add(Vector2.one); uv.Add(Vector2.up);
                triangles.Add(first); triangles.Add(first + (side == 0 ? 1 : 2)); triangles.Add(first + (side == 0 ? 2 : 1));
                triangles.Add(first); triangles.Add(first + (side == 0 ? 2 : 3)); triangles.Add(first + (side == 0 ? 3 : 2));
            }
        }

        private static void Validate(Transform root, Transform visuals)
        {
            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length > MaximumRenderers) { throw new InvalidOperationException("World detail renderer budget exceeded."); }
            long triangles = 0;
            List<string> breakdown = new List<string>();
            foreach (MeshRenderer renderer in renderers)
            {
                Mesh mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                long meshTriangles = 0;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++) { meshTriangles += mesh.GetIndexCount(submesh) / 3; }
                triangles += meshTriangles;
                breakdown.Add(renderer.transform.parent.name + "/" + renderer.name + "=" + meshTriangles);
                Bounds bounds = new Bounds();
                bool initialized = false;
                Encapsulate(renderer.bounds, visuals.worldToLocalMatrix, ref bounds, ref initialized);
                if (bounds.min.x < -27f || bounds.max.x > 27f ||
                    !(bounds.min.z >= MinimumShoulderZ || bounds.max.z <= -MinimumShoulderZ))
                {
                    throw new InvalidOperationException("World detail intrudes on route or chunk seam: " + renderer.name + " " + bounds);
                }
            }
            if (triangles > MaximumTriangles)
            {
                throw new InvalidOperationException("World detail triangle budget exceeded: " + triangles + "/" +
                    MaximumTriangles + ". " + string.Join(", ", breakdown));
            }
        }

        private static void Encapsulate(Bounds source, Matrix4x4 matrix, ref Bounds result, ref bool initialized)
        {
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 point = matrix.MultiplyPoint3x4(new Vector3(
                    (corner & 1) == 0 ? source.min.x : source.max.x,
                    (corner & 2) == 0 ? source.min.y : source.max.y,
                    (corner & 4) == 0 ? source.min.z : source.max.z));
                if (!initialized) { result = new Bounds(point, Vector3.zero); initialized = true; }
                else { result.Encapsulate(point); }
            }
        }

        private static Transform Child(Transform parent, string name)
        {
            GameObject child = new GameObject(name);
            child.transform.SetParent(parent, false);
            child.layer = 2;
            return child.transform;
        }

        private static bool Contains(string[] recipes, string recipe)
        {
            for (int index = 0; index < recipes.Length; index++)
            {
                if (string.Equals(recipes[index], recipe, StringComparison.Ordinal)) { return true; }
            }
            return false;
        }

        private static float Next(ref uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return (value & 0x00ffffffu) / 16777216f;
        }
    }
}
