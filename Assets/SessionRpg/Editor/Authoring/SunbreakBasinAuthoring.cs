using System;
using System.Collections.Generic;
using SessionRpg.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>
    /// Original, presentation-only meadow/abbey composition. Core supplies all
    /// camp locations and solid footprints; the nine scenes own no gameplay.
    /// Meshes and the shared world-UV albedo are deterministic generated assets.
    /// </summary>
    internal static class SunbreakBasinAuthoring
    {
        internal const string MeshFolder = "Assets/SessionRpg/Art/Environment/SunbreakBasin";
        internal const string GroundAlbedoPath = MeshFolder + "/MeadowMacro.asset";
        internal const int MaximumTriangles = 65000;
        internal const int MaximumRenderers = 80;
        internal const float MaximumWalkableDetailHeight = 0.18f;
        private const float CellSize = 64f;
        private const float TextureMinimum = -64f;
        private const float TextureSpan = 192f;

        public static void Build(Transform visualRoot, AuthoringAssets assets,
            WorldChunkAuthoringEntry chunk)
        {
            if (visualRoot == null) { throw new ArgumentNullException(nameof(visualRoot)); }
            if (assets == null) { throw new ArgumentNullException(nameof(assets)); }
            if (visualRoot.childCount != 0)
            {
                throw new InvalidOperationException("Sunbreak Basin must be authored once into an empty visual root.");
            }

            Vector3 origin = new Vector3(chunk.Coordinate.X * CellSize, 0f,
                chunk.Coordinate.Z * CellSize);
            string key = chunk.Coordinate.X + "_" + chunk.Coordinate.Z;
            Transform ground = Child(visualRoot, "Ground");
            Transform meadow = Child(visualRoot, "MeadowDetail");
            Transform camps = Child(visualRoot, "Encampments");
            Transform islands = Child(visualRoot, "BlockerIslands");
            Transform ridges = Child(visualRoot, "OuterRidges");

            var floor = new MeshDraft();
            floor.Quad(origin, origin + new Vector3(0f, 0f, CellSize),
                origin + new Vector3(CellSize, 0f, CellSize), origin + new Vector3(CellSize, 0f, 0f), true);
            Publish(ground, "ContinuousMeadow", floor, assets.SunbreakMeadow,
                origin, key, false);
            BuildMeadow(meadow, assets, origin, key, chunk.Seed);
            BuildClearings(camps, assets, origin, key);
            for (int index = 0; index < PveRegionLayout.LandmarkCount; index++)
            {
                ArenaCircleObstacle obstacle = PveRegionLayout.GetLandmark(index);
                if (!Owns(origin, V(obstacle.Center))) { continue; }
                Transform island = Child(islands, "Blocker_" + index);
                BuildIsland(island, assets, obstacle, index, origin, key);
            }

            BuildOuterRidges(ridges, assets, origin, key, chunk.Seed);
            if (Owns(origin, V(PveRegionLayout.Refuge)))
            {
                BuildRefuge(camps, assets, origin, key);
            }

            Validate(visualRoot);
        }

        /// <summary>Linear multiplicative tint, shared across all nine ground meshes.</summary>
        public static Texture2D BuildGroundAlbedo()
        {
            EnsureFolder(MeshFolder);
            const int size = 1024;
            var texture = new Texture2D(size, size, TextureFormat.RGB24, true, true)
            {
                name = "SunbreakBasin_MeadowMacro",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            var pixels = new Color32[size * size];
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    float worldX = TextureMinimum + x * TextureSpan / (size - 1f);
                    float worldZ = TextureMinimum + z * TextureSpan / (size - 1f);
                    float broad = Noise(worldX, worldZ, 0.032f);
                    float middle = Noise(worldX + 28f, worldZ - 17f, 0.16f);
                    float grain = Noise(worldX - 12f, worldZ + 31f, 2.8f);
                    // URP's detail layer multiplies by 2 * this linear value.
                    // Near-neutral colors modulate the real fine surface without
                    // turning it into the former blurry lime-green color field.
                    Color grass = Color.Lerp(new Color(0.45f, 0.51f, 0.43f),
                        new Color(0.51f, 0.54f, 0.47f), broad);
                    grass *= 0.96f + middle * 0.06f + grain * 0.015f;
                    float wear = ClearingWeight(worldX, worldZ, middle);
                    Color earth = Color.Lerp(new Color(0.58f, 0.52f, 0.44f),
                        new Color(0.65f, 0.57f, 0.48f), middle);
                    Color color = Color.Lerp(grass, earth, wear);
                    // Isolated pale mineral shelves, never a continuous road or
                    // repeated paving grid. Their field is continuous at seams.
                    float mineral = Mathf.SmoothStep(0f, 1f,
                        Mathf.Clamp01((Noise(worldX + 92f, worldZ, 0.11f) - 0.7f) * 5f));
                    color = Color.Lerp(color, new Color(0.59f, 0.58f, 0.54f), mineral * 0.32f);
                    color.a = 1f;
                    pixels[z * size + x] = color;
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(true, true);
            Texture2D existing = AssetDatabase.LoadAssetAtPath<Texture2D>(GroundAlbedoPath);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(texture, GroundAlbedoPath);
                return texture;
            }

            EditorUtility.CopySerialized(texture, existing);
            Object.DestroyImmediate(texture);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static float ClearingWeight(float x, float z, float noise)
        {
            float weight = 0f;
            for (int camp = -1; camp < PveRegionLayout.CampCount; camp++)
            {
                Float3 center = camp < 0 ? PveRegionLayout.Refuge : PveRegionLayout.GetCampCenter(camp);
                float dx = x - center.X;
                float dz = (z - center.Z) * (camp % 2 == 0 ? 0.82f : 1.08f);
                float distance = Mathf.Sqrt(dx * dx + dz * dz);
                float radius = camp == 6 ? 12.5f : 10.5f;
                float edge = radius + (noise - 0.5f) * 5f;
                weight = Mathf.Max(weight, 1f - Mathf.SmoothStep(0f, 1f,
                    Mathf.InverseLerp(edge - 3.5f, edge + 3f, distance)));
            }

            return weight * 0.86f;
        }

        private static void BuildMeadow(Transform parent, AuthoringAssets assets,
            Vector3 origin, string key, int seed)
        {
            var grass = new MeshDraft();
            var flowers = new MeshDraft();
            uint random = unchecked((uint)seed);
            var lowPlants = new MeshDraft();
            bool focalOwner = Owns(origin, V(PveRegionLayout.GetCampCenter(1)));
            // Dense short growth follows broad moist patches, leaving irregular
            // thin areas between them. All walkable vegetation stays below ankles.
            for (int index = 0; index < 4200; index++)
            {
                // Spend the focal chunk's finite budget on nearby source trees,
                // connected walls and fractured ground. Other chunks retain
                // their seeded field; this is not runtime density/LOD work.
                if (focalOwner) { break; }
                Vector3 point = origin + new Vector3(0.35f + Next(ref random) * 63.3f, 0.006f,
                    0.35f + Next(ref random) * 63.3f);
                if (InsideObstacle(point, 0.2f)) { continue; }
                // The benchmark clearing uses authored plant silhouettes. Keep
                // the older field outside this pocket until its own art review.
                if (MeasuredMeadowWeight(point) > 0f) { continue; }
                float wear = ClearingWeight(point.x, point.z, 0.5f);
                float patch = Noise(point.x - 28f, point.z + 4f, 0.14f);
                // Closer-spaced small tufts replace the very sparse four-blade
                // confetti. Camp centers remain worn and ankles remain visible;
                // denser growth belongs to the irregular clearing margins.
                if ((wear > 0.5f && index % 4 != 0) || patch < 0.33f) { continue; }
                for (int blade = 0; blade < 2; blade++)
                {
                    float angle = Next(ref random) * Mathf.PI * 2f;
                    Vector3 along = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 across = new Vector3(-along.z, 0f, along.x) *
                        (0.013f + Next(ref random) * 0.017f);
                    Vector3 bottom = point + along * Next(ref random) * 0.2f;
                    float height = 0.09f + Next(ref random) * 0.08f;
                    Vector3 tip = bottom + along * (0.08f + patch * 0.11f) + Vector3.up * height;
                    grass.DoubleTriangle(bottom - across, tip, bottom + across);
                    if (index % 23 == 0 && blade < 2)
                    {
                        Vector3 petalX = Vector3.right * 0.025f;
                        Vector3 petalZ = Vector3.forward * 0.025f;
                        flowers.DoubleQuad(tip - petalX, tip + petalZ,
                            tip + petalX, tip - petalZ);
                    }
                }
                if (index % 16 == 0)
                {
                    for (int leaf = 0; leaf < 6; leaf++)
                    {
                        float angle = leaf * 2.399963f + Next(ref random);
                        Vector3 stem = point + Vector3.up * 0.015f;
                        Vector3 tip = stem + Polar(0.17f + Next(ref random) * 0.14f,
                            angle, 0.045f + Next(ref random) * 0.065f);
                        Vector3 middle = Vector3.Lerp(stem, tip, 0.58f) + Vector3.up * 0.025f;
                        Vector3 across = Polar(0.035f, angle + Mathf.PI * 0.5f, 0f);
                        lowPlants.Leaf(stem, middle - across, tip, middle + across);
                    }
                }
            }

            Publish(parent, "BentMeadowGrass", grass, assets.SunbreakGrass, origin, key, false);
            Publish(parent, "ScatteredSeedFlowers", flowers, assets.SunbreakFlowers, origin, key, false);
            Publish(parent, "GroundRosettes", lowPlants, assets.SunbreakFoliage, origin, key, false);
            BuildMeasuredMeadow(parent, assets, origin, key, seed);
        }

        private static void BuildClearings(Transform parent, AuthoringAssets assets,
            Vector3 origin, string key)
        {
            for (int camp = 0; camp < PveRegionLayout.CampCount; camp++)
            {
                // The rejected paving/litter prototype is retained as source,
                // but is no longer part of the focal playable composition.
                if (camp == 1) { continue; }
                Vector3 center = V(PveRegionLayout.GetCampCenter(camp));
                if (center.x < origin.x - 15f || center.x > origin.x + 79f ||
                    center.z < origin.z - 15f || center.z > origin.z + 79f) { continue; }
                var paving = new MeshDraft();
                var litter = new MeshDraft();
                uint random = unchecked((uint)(7841 + camp * 179));
                int fragments = camp == 6 ? 95 : 65;
                for (int index = 0; index < fragments; index++)
                {
                    // Fallen grit is concentrated below the nearby outcrop and
                    // thins into the clearing; it never outlines a gameplay ring.
                    ArenaCircleObstacle landmark = PveRegionLayout.GetLandmark(
                        PveRegionLayout.MajorObstacleCount + camp);
                    Vector3 position = V(landmark.Center) + new Vector3(
                        (Next(ref random) - 0.5f) * 27f, -0.015f,
                        (Next(ref random) - 0.5f) * 25f);
                    if (!OwnsInset(origin, position, 0.6f) || InsideObstacle(position, 0.45f)) { continue; }
                    float edge = LandmarkInteriorDistance(position,
                        PveRegionLayout.MajorObstacleCount + camp);
                    if (edge < -6f || (edge < -3f && index % 4 != 0)) { continue; }
                    float scale = 0.14f + Next(ref random) * 0.25f;
                    FracturedRock(paving, position, new Vector3(scale,
                        0.05f + Next(ref random) * 0.085f, scale * 0.73f),
                        Next(ref random) * Mathf.PI * 2f, ref random);
                }
                for (int fragment = 0; fragment < 260; fragment++)
                {
                    Vector3 anchor = V(PveRegionLayout.GetLandmark(
                        PveRegionLayout.MajorObstacleCount + camp).Center);
                    Vector3 point = anchor + new Vector3((Next(ref random) - 0.5f) * 25f,
                        0.008f, (Next(ref random) - 0.5f) * 23f);
                    if (!OwnsInset(origin, point, 0.4f) || InsideObstacle(point, 0f)) { continue; }
                    float edge = LandmarkInteriorDistance(point, PveRegionLayout.MajorObstacleCount + camp);
                    if (edge < -6f || (edge < -2f && fragment % 3 != 0)) { continue; }
                    for (int leaf = 0; leaf < 3; leaf++)
                    {
                        float angle = Next(ref random) * Mathf.PI * 2f;
                        float length = 0.08f + Next(ref random) * 0.1f;
                        Vector3 middle = point + Polar(Next(ref random) * 0.32f, angle, 0.004f);
                        Vector3 along = Polar(length, angle, 0f);
                        Vector3 across = Polar(length * 0.31f, angle + Mathf.PI * 0.5f, 0.012f);
                        litter.Leaf(middle - along * 0.5f, middle - across,
                            middle + along * 0.5f, middle + across);
                    }
                }

                Publish(parent, "Camp_" + camp + "_WornClearing", paving,
                    assets.SunbreakPaving, origin, key, false);
                Publish(parent, "Camp_" + camp + "_LeafLitterApron", litter,
                    assets.SunbreakCanvas, origin, key, false);
            }
        }

        private static float MeasuredMeadowWeight(Vector3 point)
        {
            float distance = Vector2.Distance(new Vector2(point.x, point.z), new Vector2(-39f, 12f));
            return Mathf.Clamp01((28f - distance) / 5f);
        }

        private static void BuildMeasuredMeadow(Transform parent, AuthoringAssets assets, Vector3 origin, string key, int seed)
        {
            var batches = new List<PlantBatch>();
            uint random = unchecked((uint)(seed + 43119));
            // Whole measured plants, not stretched billboard quads. Smaller,
            // denser colonies give the stone/earth margins a readable edge.
            // The camp centre remains open and every blade stays below 18 cm.
            string[] selections = { "GrassTinyD", "GrassTinyD", "GrassTinyB", "GrassTinyE", "GrassTinyF" };
            float[] scales = { 3.6f, 3.6f, 2.85f, 3.5f, 3.7f };
            Vector3[] patches = { new Vector3(-50.5f, 0f, 10f), new Vector3(-48f, 0f, 18.5f),
                new Vector3(-39f, 0f, 21f), new Vector3(-44f, 0f, 4.5f),
                new Vector3(-48f, 0f, 5f), new Vector3(-34f, 0f, 3.5f),
                new Vector3(-29f, 0f, 19f), new Vector3(-42f, 0f, 18.5f) };
            int triangles = 0;
            for (int attempt = 0; attempt < 5000 && triangles < 5800; attempt++)
            {
                Vector3 point = patches[attempt % patches.Length] +
                    Polar(Mathf.Sqrt(Next(ref random)) * 1.6f, Next(ref random) * Mathf.PI * 2f, 0.003f);
                if (!OwnsInset(origin, point, 0.6f) || InsideObstacle(point, 0.45f)) { continue; }
                float moisture = Noise(point.x - 28f, point.z + 4f, 0.48f);
                if (moisture < 0.38f) { continue; }
                int selection = attempt % selections.Length;
                float scale = scales[selection] * (0.85f + Next(ref random) * 0.15f);
                triangles += AppendPlant(batches, selections[selection], point,
                    Next(ref random) * 360f, scale, -1);
            }
            PublishPlants(parent, "MeasuredMeadow", batches, origin, key, false);

            if (Owns(origin, V(PveRegionLayout.GetCampCenter(1))))
            {
                SunbreakRuinGroundAuthoring.Build(parent, assets.SunbreakRock);
            }
        }

        private static void EmbeddedGroundStone(MeshDraft mesh, Vector3 center, float radius, float height, ref uint random)
        {
            const int sides = 6;
            var top = new Vector3[sides];
            float phase = Next(ref random) * Mathf.PI * 2f;
            for (int i = 0; i < sides; i++)
            {
                Vector3 offset = Polar(radius * (0.7f + Next(ref random) * 0.3f),
                    phase + i * Mathf.PI * 2f / sides, height * (0.25f + Next(ref random) * 0.25f));
                offset.z *= 0.65f;
                top[i] = center + offset;
            }
            Vector3 peak = center + new Vector3(radius * 0.1f, height, -radius * 0.1f);
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                Vector3 bottomA = new Vector3(top[i].x, center.y, top[i].z);
                Vector3 bottomB = new Vector3(top[next].x, center.y, top[next].z);
                mesh.Quad(bottomA, top[i], top[next], bottomB);
                mesh.Triangle(top[i], peak, top[next]);
            }
        }

        private static int AppendPlant(List<PlantBatch> batches, string selection, Vector3 position,
            float yaw, float scale, int landmark)
        {
            int triangles = 0;
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            foreach (var part in selection == "TreeCanopyV2" ? SunbreakCanopyAuthoring.GetParts() :
                SunbreakVegetationAssets.GetParts(selection))
            {
                PlantBatch batch = batches.Find(value => value.Material == part.Material);
                if (batch == null)
                {
                    batch = new PlantBatch(part.Material);
                    batches.Add(batch);
                }
                Mesh mesh = part.Mesh;
                Vector3[] points = mesh.vertices;
                Vector3[] normals = mesh.normals;
                Vector2[] uv = mesh.uv;
                int first = batch.Draft.Vertices.Count;
                for (int i = 0; i < points.Length; i++)
                {
                    Vector3 world = position + rotation * (points[i] * scale);
                    // Imported topology is never squeezed into a collider union.
                    // A bad placement fails authoring and must be moved/rescaled.
                    if (world.y > MaximumWalkableDetailHeight + 0.001f &&
                        (landmark < 0 || LandmarkInteriorDistance(world, landmark) < 0.01f))
                    { throw new InvalidOperationException(selection + " placement crosses Core cover at " + world); }
                    batch.Draft.Vertices.Add(world);
                    batch.Draft.Normals.Add(rotation * normals[i]);
                    batch.Draft.Uv.Add(uv[i]);
                }
                int[] indices = mesh.triangles;
                foreach (int i in indices) { batch.Draft.Triangles.Add(first + i); }
                triangles += indices.Length / 3;
            }
            return triangles;
        }

        private static void PublishPlants(Transform parent, string prefix, List<PlantBatch> batches,
            Vector3 origin, string key, bool shadows)
        {
            for (int i = 0; i < batches.Count; i++)
            {
                Publish(parent, prefix + "_" + i, batches[i].Draft, batches[i].Material,
                    origin, key, shadows);
            }
        }

        private sealed class PlantBatch
        {
            public readonly Material Material;
            public readonly MeshDraft Draft = new MeshDraft();
            public PlantBatch(Material material) { Material = material; }
        }

        private static void BuildMeasuredIsland(Transform parent, int landmark, Vector3 center,
            Vector3 origin, string key, float shelfHeight)
        {
            var plants = new List<PlantBatch>();
            if (landmark == 0)
            {
                SunbreakFortAssets.CreateModule(parent, SunbreakFortAssets.Module.RoundTower,
                    "MeasuredWatchtower", center + new Vector3(0.5f, 0f, -0.5f), 0.08f, 0.78f, 18f);
                SunbreakFortAssets.CreateModule(parent, SunbreakFortAssets.Module.BattlementWall,
                    "MeasuredTowerWing", center + new Vector3(-6.1f, 0f, -1.8f), 0.06f, 0.64f, 40f);
                SunbreakFortAssets.CreateModule(parent, SunbreakFortAssets.Module.LowWall,
                    "MeasuredSouthReturn", center + new Vector3(-5.7f, 0f, -5.4f), 0.04f, 0.6f, 90f);
                SunbreakFortAssets.CreateModule(parent, SunbreakFortAssets.Module.LowWall,
                    "MeasuredNorthReturn", center + new Vector3(-4.8f, 0f, 4.1f), 0.04f, 0.46f, 90f);
                SunbreakFortAssets.CreateModule(parent, SunbreakFortAssets.Module.ThickEnd,
                    "MeasuredBrokenAbutment", center + new Vector3(-6.8f, 0f, 1.6f), 0.035f, 0.42f, 12f);
                AppendPlant(plants, "TreeCanopyV2", ShelfPoint(new Vector3(-10f, 0f, 9f),
                    landmark, shelfHeight) - Vector3.up * 0.06f, 35f, 1.5f, landmark);
            }
            else
            {
                // Explicit source derivative retains the joined lower courses,
                // replaces intact parapets with capped broken edges, and grounds
                // real masonry offcuts on this existing presentation-only bank.
                SunbreakRuinMasonryAuthoring.Build(parent,
                    point => ShelfPoint(point, landmark, shelfHeight).y);
                AppendPlant(plants, "TreeCanopyV2", ShelfPoint(new Vector3(-45.5f, 0f, 25.2f),
                    landmark, shelfHeight) - Vector3.up * 0.06f, 10f, 1.6f, landmark);
                AppendPlant(plants, "TreeCanopyV2", ShelfPoint(new Vector3(-42f, 0f, 25.6f),
                    landmark, shelfHeight) - Vector3.up * 0.06f, 120f, 1.5f, landmark);
            }

            // Authored fern rosettes ground the masonry without covering the
            // nine-meter fighting area. Full source geometry is retained.
            Vector3[] fernOffsets = landmark == 0
                ? new[] { new Vector3(-5.6f, 0f, -2.6f), new Vector3(-3.7f, 0f, -5f),
                    new Vector3(4.8f, 0f, -1.8f), new Vector3(2.8f, 0f, 5.5f), new Vector3(-6.5f, 0f, 0.2f) }
                : new[] { new Vector3(-2.5f, 0f, -1.8f), new Vector3(-1.5f, 0f, -2.8f),
                    new Vector3(2.2f, 0f, 1.2f), new Vector3(-2.5f, 0f, 2.3f), new Vector3(-0.8f, 0f, -2.8f) };
            for (int i = 0; i < fernOffsets.Length; i++)
            {
                AppendPlant(plants, "Fern02a", ShelfPoint(center + fernOffsets[i], landmark,
                    shelfHeight) - Vector3.up * 0.025f, i * 83f + 20f, 1.65f, landmark);
            }
            PublishPlants(parent, "MeasuredVegetation", plants, origin,
                key + "_Island" + landmark, true);
        }

        private static void BuildMeasuredGrove(Transform parent, int landmark, Vector3 center,
            Vector3 origin, string key, float shelfHeight)
        {
            var plants = new List<PlantBatch>();
            Vector3[] positions = landmark == 2
                ? new[] { center + new Vector3(-3f, 0f, -1f), center + new Vector3(3f, 0f, 3f),
                    center + new Vector3(-5f, 0f, 5f) }
                : new[] { center + new Vector3(-0.5f, 0f, 0.8f) };
            float[] scales = landmark == 2 ? new[] { 2f, 1.8f, 1.6f } : new[] { 1.6f };
            for (int tree = 0; tree < positions.Length; tree++)
            {
                AppendPlant(plants, "TreeCanopyV2", ShelfPoint(positions[tree], landmark, shelfHeight) -
                    Vector3.up * 0.06f, tree * 113f + 25f, scales[tree], landmark);
            }
            Vector3[] fernOffsets = { new Vector3(-2.1f, 0f, -1.8f), new Vector3(1.7f, 0f, -2f),
                new Vector3(2.4f, 0f, 1.4f), new Vector3(-2.3f, 0f, 2.1f) };
            for (int fern = 0; fern < fernOffsets.Length; fern++)
            {
                AppendPlant(plants, "Fern02a", ShelfPoint(center + fernOffsets[fern], landmark, shelfHeight) -
                    Vector3.up * 0.025f, fern * 83f + 20f, 1.65f, landmark);
            }
            PublishPlants(parent, "MeasuredVegetation", plants, origin, key + "_Island" + landmark, true);
        }

        private static void BuildIsland(Transform parent, AuthoringAssets assets,
            ArenaCircleObstacle obstacle, int index, Vector3 origin, string key)
        {
            Vector3 center = V(obstacle.Center);
            bool small = index >= PveRegionLayout.MajorObstacleCount;
            bool scannedRockBenchmark = index == PveRegionLayout.MajorObstacleCount + 1;
            Vector3 benchmarkCenter = center + new Vector3(1.2f, 0f, -1.4f);
            uint random = unchecked((uint)(9671 + index * 917));
            var rock = new MeshDraft();
            var terrain = new MeshDraft();
            var stone = new MeshDraft();
            var architecture = new MeshDraft();
            var bark = new MeshDraft();
            var foliage = new MeshDraft();
            var lightFoliage = new MeshDraft();
            var understory = new MeshDraft();
            float shelfHeight = small ? 0.6f : 2.65f;
            // One continuous surface spans the actual asymmetric Core union.
            // Exposed strata and vegetation share its height field; there are no
            // circular pedestals underneath otherwise disconnected set dressing.
            UnionShelf(terrain, index, obstacle, shelfHeight);
            bool earthFoundation = small || index == 0;
            if (earthFoundation) { terrain.UseWorldMacroUvs(); }
            if (index == 2 || index == 9)
            {
                // These two visible middle-distance groves replace their
                // prototype tree/rock scatter, not an extra layer of props.
                // Core shelf geometry and the routes around it are unchanged.
                if (!earthFoundation) { terrain.UseWorldMacroUvs(); }
                ClampToFootprint(terrain, index);
                Publish(parent, "RockShelf", terrain, assets.SunbreakMeadow,
                    origin, key + "_Island" + index, true);
                BuildMeasuredGrove(parent, index, center, origin, key, shelfHeight);
                return;
            }
            if (index == 0 || index == 7)
            {
                ClampToFootprint(terrain, index);
                Publish(parent, "RockShelf", terrain, assets.SunbreakMeadow,
                    origin, key + "_Island" + index, true);
                BuildMeasuredIsland(parent, index, center, origin, key, shelfHeight);
                if (scannedRockBenchmark)
                {
                    float groundY = ShelfPoint(benchmarkCenter, index, shelfHeight).y - 0.12f;
                    SunbreakScannedRockAuthoring.CreateBenchmarkRock(parent, benchmarkCenter, groundY);
                }
                return;
            }
            if (small)
            {
                int camp = index - PveRegionLayout.MajorObstacleCount;
                if (camp == 0 || camp == 4)
                {
                    Tree(bark, foliage, lightFoliage, ShelfPoint(center + new Vector3(-0.9f, 0f, 0.65f),
                            index, shelfHeight), 7.2f, 3.3f, ref random);
                    Tree(bark, foliage, lightFoliage, ShelfPoint(center + new Vector3(2.7f, 0f, 0.6f),
                            index, shelfHeight), 4.8f, 2.5f, ref random, true);
                }
                else if (camp == 1 || camp == 5)
                {
                    if (camp == 1)
                    {
                        AbbeyRemnant(architecture, center + new Vector3(0.2f, 0.08f, -0.2f),
                            90f, ref random);
                    }
                    else
                    {
                        BrokenWall(stone, center + new Vector3(0.25f, 0.13f, 1.5f),
                            6.4f, 3.8f, 0.85f, -32f, ref random);
                    }
                    Tree(bark, foliage, lightFoliage,
                        ShelfPoint(center + new Vector3(-1.6f, 0f, -1.1f), index, shelfHeight),
                        camp == 5 ? 7.7f : 6.9f, 3.3f, ref random);
                }
                else if (camp == 2)
                {
                    // A wind-torn coppice, without the same ruin/tree pairing.
                    Tree(bark, foliage, lightFoliage, ShelfPoint(center + new Vector3(0.3f, 0f, 0.8f),
                        index, shelfHeight), 5.6f, 3.5f, ref random);
                    Cylinder(bark, ShelfPoint(center + new Vector3(-2.5f, 0f, -1.1f), index, shelfHeight),
                        ShelfPoint(center + new Vector3(1.7f, 0f, -2.2f), index, shelfHeight) + Vector3.up * 0.3f,
                        0.24f, 0.17f, 9);
                }
                else
                {
                    BrokenWall(stone, center + Vector3.up * 0.12f,
                        7.7f, camp == 6 ? 4.6f : 3.6f, 1.15f, camp == 6 ? 63f : -18f, ref random);
                    Pier(stone, ShelfPoint(center + new Vector3(-2.1f, 0f, 0.9f), index, shelfHeight),
                        3.25f, 0.85f, ref random);
                    Tree(bark, foliage, lightFoliage, ShelfPoint(center + new Vector3(1.6f, 0f, -1.1f),
                        index, shelfHeight), 4.4f, 2.45f, ref random, true);
                }
            }
            else
            {
                Vector3 raised = ShelfPoint(center, index, shelfHeight) - Vector3.up * 0.2f;
                switch (index)
                {
                    case 0:
                        WatchtowerRemnant(architecture, center + new Vector3(0.5f, 0.1f, -0.5f),
                            18f, ref random);
                        BrokenWall(stone, center + new Vector3(-4.5f, 0.08f, 1.3f),
                            5.8f, 2.1f, 0.95f, 78f, ref random);
                        Tree(bark, foliage, lightFoliage,
                            ShelfPoint(center + new Vector3(3.6f, 0f, -3.4f), index, shelfHeight),
                            7.3f, 3f, ref random);
                        break;
                    case 1:
                        Arch(stone, raised, 4.1f, 4.7f, -28f, ref random);
                        Pier(stone, raised + new Vector3(-3.5f, 0f, 0.8f), 5.5f, 1.1f, ref random);
                        BrokenWall(stone, raised + new Vector3(1.8f, 0f, 2.8f), 6f, 2.2f, 1.25f, -28f, ref random);
                        Tree(bark, foliage, lightFoliage, raised + new Vector3(-2.2f, -0.3f, -2.8f),
                            7.5f, 3.1f, ref random);
                        break;
                    case 2:
                        Tree(bark, foliage, lightFoliage, raised + new Vector3(-2f, -0.25f, -1.3f),
                            9.2f, 3.8f, ref random);
                        Tree(bark, foliage, lightFoliage, raised + new Vector3(2.6f, -0.25f, 2f),
                            7.2f, 3.2f, ref random);
                        BrokenWall(stone, raised + new Vector3(0f, 0f, -3.5f), 6.8f, 1.8f, 1f, 8f, ref random);
                        break;
                    case 3:
                        Arch(stone, raised + new Vector3(1.5f, 0f, 1.5f), 5.7f, 3.8f, 55f, ref random);
                        BrokenWall(stone, raised + new Vector3(-2.7f, 0f, -0.2f), 7.2f, 3.6f, 1.25f, 145f, ref random);
                        Pier(stone, raised + new Vector3(3.8f, 0f, -3.2f), 3.4f, 1.25f, ref random);
                        Tree(bark, foliage, lightFoliage, raised + new Vector3(-3f, -0.3f, 3.5f),
                            8.2f, 3.6f, ref random);
                        break;
                    case 4:
                        BrokenWall(stone, raised + new Vector3(-1.2f, 0f, 0f), 7.6f, 3.5f, 1.2f, 12f, ref random);
                        Pier(stone, raised + new Vector3(3.8f, 0f, 1.2f), 4.3f, 1.25f, ref random);
                        Tree(bark, foliage, lightFoliage, raised + new Vector3(0f, -0.35f, 3.4f),
                            8.1f, 3.5f, ref random);
                        break;
                    default:
                        Arch(stone, raised, 3.7f, 2.4f, -45f, ref random);
                        BrokenWall(stone, raised + new Vector3(2.4f, 0f, 1.1f), 4.5f, 2.1f, 1f, 45f, ref random);
                        Tree(bark, foliage, lightFoliage, raised + new Vector3(-2.5f, -0.3f, -2.1f),
                            7.8f, 3.4f, ref random);
                        break;
                }
            }

            // Embedded fractured boulders interrupt the radial construction;
            // scattered human-scale fallen stones break up the ruin foundations.
            int rubbleCount = small ? 17 : 27;
            for (int item = 0; item < rubbleCount; item++)
            {
                int component = PveRegionLayout.GetLandmarkObstacleStart(index) + item %
                    PveRegionLayout.GetLandmarkObstacleCount(index);
                ArenaCircleObstacle lobe = PveRegionLayout.GetObstacle(component);
                float angle = Next(ref random) * Mathf.PI * 2f;
                float reach = lobe.Radius * ((index == 0 ? 0.46f : 0.15f) + Next(ref random) * 0.38f);
                Vector3 position = ShelfPoint(V(lobe.Center) + Polar(reach, angle, 0f), index, shelfHeight);
                if (scannedRockBenchmark &&
                    new Vector2(position.x - benchmarkCenter.x, position.z - benchmarkCenter.z).sqrMagnitude < 5.29f)
                {
                    // The measured focal stone replaces this local rubble pocket;
                    // do not bury its scanned silhouette under procedural rocks.
                    continue;
                }
                if (item % 3 != 2)
                {
                    FracturedRock(rock, position, new Vector3(
                        (small ? 0.85f : 1.65f) * (0.75f + Next(ref random) * 0.5f),
                        (small ? 0.95f : 1.8f) * (0.65f + Next(ref random) * 0.45f),
                        (small ? 0.7f : 1.3f) * (0.8f + Next(ref random) * 0.4f)),
                        angle, ref random);
                }
                else
                {
                    Block(stone, position, new Vector3(0.4f + Next(ref random) * 0.4f,
                        0.2f + Next(ref random) * 0.22f, 0.3f + Next(ref random) * 0.25f),
                        angle * Mathf.Rad2Deg, ref random);
                }
            }

            // Shrubby regrowth and ferns soften joints between bare rock, broken
            // masonry and the meadow. Taller growth remains inside Core cover.
            for (int plant = 0; plant < (small ? 30 : 25); plant++)
            {
                ArenaCircleObstacle lobe = PveRegionLayout.GetObstacle(
                    PveRegionLayout.GetLandmarkObstacleStart(index) +
                    plant % PveRegionLayout.GetLandmarkObstacleCount(index));
                Vector3 point = V(lobe.Center) + Polar(lobe.Radius *
                    (0.25f + Next(ref random) * 0.65f), Next(ref random) * Mathf.PI * 2f, 0f);
                point = ShelfPoint(point, index, shelfHeight);
                Fern(understory, point, 0.75f + Next(ref random) * 0.65f, ref random);
            }

            // Exact vertex guard is deliberately before serialization so future
            // art edits cannot silently create misleading cover outside Core.
            ClampToFootprint(rock, index);
            ClampToFootprint(terrain, index);
            ClampToFootprint(stone, index);
            ClampToFootprint(architecture, index);
            ClampToFootprint(bark, index);
            ClampToFootprint(foliage, index);
            ClampToFootprint(lightFoliage, index);
            ClampToFootprint(understory, index);
            string islandKey = key + "_Island" + index;
            Publish(parent, "RockShelf", terrain, earthFoundation ? assets.SunbreakMeadow : assets.SunbreakRock,
                origin, islandKey, true);
            Publish(parent, "EmbeddedBoulders", rock, assets.SunbreakRock, origin, islandKey, true);
            Publish(parent, "WeatheredAbbeyMasonry", stone, assets.SunbreakLimestone, origin, islandKey, true);
            Publish(parent, index == 0 ? "FrostboundWatchtower" : "FrostboundAbbeyFacade", architecture,
                assets.SunbreakLimestone, origin, islandKey, true);
            Publish(parent, "BranchingWood", bark, assets.SunbreakBark, origin, islandKey, true);
            Publish(parent, "LeafCanopy", foliage, assets.SunbreakFoliage, origin, islandKey, true);
            Publish(parent, "SunlitLeafEdges", lightFoliage, assets.SunbreakFoliageLight, origin, islandKey, true);
            Publish(parent, "RockFernUnderstory", understory, assets.SunbreakFoliage, origin, islandKey, true);
            if (scannedRockBenchmark)
            {
                float groundY = ShelfPoint(benchmarkCenter, index, shelfHeight).y - 0.12f;
                SunbreakScannedRockAuthoring.CreateBenchmarkRock(parent, benchmarkCenter, groundY);
            }
        }

        private static void BuildOuterRidges(Transform parent, AuthoringAssets assets,
            Vector3 origin, string key, int seed)
        {
            var backdrop = new MeshDraft();
            var boundary = new MeshDraft();
            var boundaryRubble = new MeshDraft();
            var rock = new MeshDraft();
            var bark = new MeshDraft();
            var foliage = new MeshDraft();
            var lightFoliage = new MeshDraft();
            bool west = origin.x < 0f;
            bool east = origin.x >= CellSize;
            bool south = origin.z < 0f;
            bool north = origin.z >= CellSize;
            if (!west && !east && !south && !north) { return; }
            BuildBoundaryBank(boundary, origin, west, east, south, north);
            // This is continuous terrain, not a pile of flat-shaded triangles.
            // Preserve its existing world UV phase and silhouette; smooth only
            // the lighting frame across coincident vertices within the bank.
            boundary.SmoothTerrainSurface(0, boundary.Vertices.Count, false);
            float minX = origin.x - (west ? 36f : 0f);
            float maxX = origin.x + CellSize + (east ? 36f : 0f);
            float minZ = origin.z - (south ? 36f : 0f);
            float maxZ = origin.z + CellSize + (north ? 36f : 0f);
            for (float z = minZ; z < maxZ; z += 4f)
            {
                for (float x = minX; x < maxX; x += 4f)
                {
                    if (x >= origin.x && x < origin.x + CellSize &&
                        z >= origin.z && z < origin.z + CellSize) { continue; }
                    backdrop.Quad(new Vector3(x, -0.04f, z), new Vector3(x, -0.04f, z + 4f),
                        new Vector3(x + 4f, -0.04f, z + 4f), new Vector3(x + 4f, -0.04f, z), true);
                }
            }

            uint random = unchecked((uint)(seed + 8441));
            for (int edge = 0; edge < 4; edge++)
            {
                if ((edge == 0 && !west) || (edge == 1 && !east) ||
                    (edge == 2 && !south) || (edge == 3 && !north)) { continue; }
                for (int item = 0; item < 10; item++)
                {
                    float along = item * 6.4f + 3.2f;
                    float outward = 5.5f + Next(ref random) * 4f;
                    Vector3 center = edge == 0 ? new Vector3(origin.x - outward, 0f, origin.z + along)
                        : edge == 1 ? new Vector3(origin.x + CellSize + outward, 0f, origin.z + along)
                        : edge == 2 ? new Vector3(origin.x + along, 0f, origin.z - outward)
                        : new Vector3(origin.x + along, 0f, origin.z + CellSize + outward);
                    float radius = 6.1f + Next(ref random) * 2f;
                    // Sparse outcrops sit above the continuous Core boundary
                    // bank; the former row of ten equal mounds read as a wall
                    // of repeated props. Reinvest this geometry in the meadow.
                    if (item % 2 == 0)
                    {
                        Mound(rock, center, radius, 3.2f + Next(ref random) * 3.2f, ref random);
                    }
                    if (item == 2)
                    {
                        Tree(bark, foliage, lightFoliage, center + Vector3.up * 1.8f,
                            7f + Next(ref random) * 2f, 3.4f, ref random, true);
                    }
                    Vector3 boundaryCenter = edge == 0 ? new Vector3(PveRegionLayout.MinimumX - 3.6f, 0.3f,
                            Mathf.Clamp(origin.z + along, PveRegionLayout.MinimumZ - 1f, PveRegionLayout.MaximumZ + 1f))
                        : edge == 1 ? new Vector3(PveRegionLayout.MaximumX + 3.6f, 0.3f,
                            Mathf.Clamp(origin.z + along, PveRegionLayout.MinimumZ - 1f, PveRegionLayout.MaximumZ + 1f))
                        : edge == 2 ? new Vector3(Mathf.Clamp(origin.x + along, PveRegionLayout.MinimumX - 1f,
                            PveRegionLayout.MaximumX + 1f), 0.3f, PveRegionLayout.MinimumZ - 3.6f)
                        : new Vector3(Mathf.Clamp(origin.x + along, PveRegionLayout.MinimumX - 1f,
                            PveRegionLayout.MaximumX + 1f), 0.3f, PveRegionLayout.MaximumZ + 3.6f);
                    FracturedRock(boundaryRubble, boundaryCenter,
                        new Vector3(1.8f + Next(ref random) * 0.6f, 0.9f + Next(ref random) * 1.1f, 1.8f),
                        Next(ref random) * Mathf.PI * 2f, ref random);
                }
            }

            Publish(parent, "UnbrokenDistantMeadow", backdrop, assets.SunbreakMeadow, origin, key, false);
            Publish(parent, "CoreBoundaryRockBank", boundary, assets.SunbreakRock, origin, key, true);
            Publish(parent, "ShatteredBankRocks", boundaryRubble, assets.SunbreakRock, origin, key, true);
            Publish(parent, "NaturalLimestoneRidge", rock, assets.SunbreakRock, origin, key, false);
            Publish(parent, "RidgeBranches", bark, assets.SunbreakBark, origin, key, false);
            Publish(parent, "RidgeFoliage", foliage, assets.SunbreakFoliage, origin, key, false);
            Publish(parent, "RidgeFoliageHighlights", lightFoliage, assets.SunbreakFoliageLight, origin, key, false);
        }

        private static void BuildBoundaryBank(MeshDraft mesh, Vector3 origin,
            bool west, bool east, bool south, bool north)
        {
            // The first vertex row is exactly at Core's stop, not at the visual
            // cell edge six meters farther away. A steep first rise makes the
            // boundary legible; world-space noise keeps chunk joins coherent.
            float[] reach = { 0f, 0.75f, 2.2f, 4.3f, 8f, 14f };
            float[] height = { 0.04f, 0.65f, 1.15f, 1.9f, 3.6f, 4.8f };
            for (int edge = 0; edge < 4; edge++)
            {
                if ((edge == 0 && !west) || (edge == 1 && !east) ||
                    (edge == 2 && !south) || (edge == 3 && !north)) { continue; }
                bool alongZ = edge < 2;
                float start = Mathf.Max(alongZ ? origin.z : origin.x,
                    alongZ ? PveRegionLayout.MinimumZ : PveRegionLayout.MinimumX);
                float end = Mathf.Min((alongZ ? origin.z : origin.x) + CellSize,
                    alongZ ? PveRegionLayout.MaximumZ : PveRegionLayout.MaximumX);
                Vector3 outward = edge == 0 ? Vector3.left : edge == 1 ? Vector3.right
                    : edge == 2 ? Vector3.back : Vector3.forward;
                for (float along = start; along < end; along += 2f)
                {
                    float next = Mathf.Min(along + 2f, end);
                    Vector3 first = edge == 0 ? new Vector3(PveRegionLayout.MinimumX, 0f, along)
                        : edge == 1 ? new Vector3(PveRegionLayout.MaximumX, 0f, along)
                        : edge == 2 ? new Vector3(along, 0f, PveRegionLayout.MinimumZ)
                        : new Vector3(along, 0f, PveRegionLayout.MaximumZ);
                    Vector3 second = first + (alongZ ? Vector3.forward : Vector3.right) * (next - along);
                    for (int ring = 0; ring < reach.Length - 1; ring++)
                    {
                        FacingUpQuad(mesh,
                            BoundaryPoint(first, outward, reach[ring], height[ring], ring),
                            BoundaryPoint(second, outward, reach[ring], height[ring], ring),
                            BoundaryPoint(second, outward, reach[ring + 1], height[ring + 1], ring + 1),
                            BoundaryPoint(first, outward, reach[ring + 1], height[ring + 1], ring + 1));
                    }
                }
            }

            // Join the two strips around each exterior corner; without this fan
            // the rectangular Core corner could still read as an open grass gap.
            if ((west || east) && (south || north))
            {
                Vector3 corner = new Vector3(west ? PveRegionLayout.MinimumX : PveRegionLayout.MaximumX,
                    0f, south ? PveRegionLayout.MinimumZ : PveRegionLayout.MaximumZ);
                Vector3 outwardX = west ? Vector3.left : Vector3.right;
                Vector3 outwardZ = south ? Vector3.back : Vector3.forward;
                const int segments = 6;
                for (int segment = 0; segment < segments; segment++)
                {
                    float angleA = segment * Mathf.PI * 0.5f / segments;
                    float angleB = (segment + 1) * Mathf.PI * 0.5f / segments;
                    Vector3 a = outwardX * Mathf.Cos(angleA) + outwardZ * Mathf.Sin(angleA);
                    Vector3 b = outwardX * Mathf.Cos(angleB) + outwardZ * Mathf.Sin(angleB);
                    for (int ring = 0; ring < reach.Length - 1; ring++)
                    {
                        FacingUpQuad(mesh,
                            BoundaryPoint(corner, a, reach[ring], height[ring], ring),
                            BoundaryPoint(corner, b, reach[ring], height[ring], ring),
                            BoundaryPoint(corner, b, reach[ring + 1], height[ring + 1], ring + 1),
                            BoundaryPoint(corner, a, reach[ring + 1], height[ring + 1], ring + 1));
                    }
                }
            }
        }

        private static Vector3 BankPoint(Vector3 point, float height, int ring)
        {
            point.y = height * (ring == 0 ? 1f : 0.58f + Noise(point.x, point.z, 0.085f) * 0.87f) +
                (ring == 0 ? 0f : (Noise(point.x, point.z, 0.46f) - 0.5f) * 0.3f);
            return point;
        }

        private static Vector3 BoundaryPoint(Vector3 basePoint, Vector3 outward,
            float reach, float height, int ring)
        {
            float offset = ring == 0 ? 0f : reach *
                (0.7f + Noise(basePoint.x - 31f, basePoint.z + 41f, 0.17f) * 0.65f);
            return BankPoint(basePoint + outward * offset, height, ring);
        }

        private static void FacingUpQuad(MeshDraft mesh, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            // The corner's first ring collapses to one point, so inspect its
            // nondegenerate second triangle when choosing winding.
            float vertical = Vector3.Cross(b - a, c - a).y;
            if (Mathf.Abs(vertical) < 0.00001f) { vertical = Vector3.Cross(c - a, d - a).y; }
            if (vertical >= 0f) { mesh.Quad(a, b, c, d); }
            else { mesh.Quad(d, c, b, a); }
        }

        private static void BuildRefuge(Transform parent, AuthoringAssets assets,
            Vector3 origin, string key)
        {
            Transform camp = Child(parent, "RefugeSupplies");
            var wood = new MeshDraft();
            var cloth = new MeshDraft();
            var stone = new MeshDraft();
            uint random = 731947u;
            Vector3 shelter = new Vector3(PveRegionLayout.MinimumX - 3.2f, 0f,
                PveRegionLayout.Refuge.Z);
            // The shelter and crates are outside the playable rectangle. The
            // refuge itself remains a flat, unobstructed recovery clearing.
            for (int z = -1; z <= 1; z += 2)
            {
                Cylinder(wood, shelter + new Vector3(-1.1f, 0f, z * 2.1f),
                    shelter + new Vector3(-1.1f, 2.65f, z * 2.1f), 0.12f, 0.09f, 7);
                Cylinder(wood, shelter + new Vector3(1.1f, 0f, z * 2.1f),
                    shelter + new Vector3(1.1f, 1.8f, z * 2.1f), 0.11f, 0.075f, 7);
            }

            cloth.DoubleQuad(shelter + new Vector3(-1.3f, 2.72f, -2.3f),
                shelter + new Vector3(-1.3f, 2.72f, 2.3f),
                shelter + new Vector3(1.25f, 1.78f, 2.3f),
                shelter + new Vector3(1.25f, 1.78f, -2.3f));
            for (int crate = 0; crate < 4; crate++)
            {
                Vector3 position = shelter + new Vector3(crate % 2 == 0 ? -0.4f : 0.65f,
                    0.4f, -3.8f + crate / 2 * 1.05f);
                Block(wood, position, new Vector3(0.9f, 0.8f, 0.85f), crate * 11f, ref random);
                for (int band = -1; band <= 1; band += 2)
                {
                    Block(stone, position + new Vector3(band * 0.3f, 0.02f, 0f),
                        new Vector3(0.06f, 0.83f, 0.89f), crate * 11f, ref random);
                }
            }

            Cylinder(wood, shelter + new Vector3(0f, 0f, 3.4f),
                shelter + new Vector3(0f, 1.1f, 3.4f), 0.45f, 0.4f, 12);
            Publish(camp, "TimberAndProvisions", wood, assets.SunbreakBark, origin, key, true);
            Publish(camp, "OchreCanvasShelter", cloth, assets.SunbreakCanvas, origin, key, true);
            Publish(camp, "CrateBands", stone, assets.SunbreakPaving, origin, key, true);
        }

        private static void Mound(MeshDraft mesh, Vector3 center, float radius,
            float height, ref uint random)
        {
            const int segments = 36;
            const int ringCount = 6;
            var rings = new Vector3[ringCount, segments];
            float phase = Next(ref random) * Mathf.PI * 2f;
            float[] reach = { 1f, 0.91f, 0.7f, 0.47f, 0.24f, 0.06f };
            int firstVertex = mesh.Vertices.Count;
            for (int segment = 0; segment < segments; segment++)
            {
                float angle = segment * Mathf.PI * 2f / segments;
                float erosion = Mathf.Sin(angle * 3f + phase) * 0.13f +
                    Mathf.Sin(angle * 7f - phase * 0.7f) * 0.08f;
                for (int ring = 0; ring < ringCount; ring++)
                {
                    float edge = radius * reach[ring] *
                        (1f + erosion * 0.85f + (Next(ref random) - 0.5f) * 0.018f);
                    float weathering = ring == 0 ? 0f : (Next(ref random) - 0.5f) * height * 0.012f;
                    Vector3 point = Polar(edge, angle, 0f);
                    point.y = ring == 0 ? 0.025f : MoundHeight(point.x / radius,
                        point.z / radius, height, phase) + weathering;
                    rings[ring, segment] = center + point;
                }
            }

            for (int segment = 0; segment < segments; segment++)
            {
                int next = (segment + 1) % segments;
                for (int ring = 0; ring < ringCount - 1; ring++)
                {
                    mesh.Quad(rings[ring, segment], rings[ring + 1, segment],
                        rings[ring + 1, next], rings[ring, next]);
                }
                mesh.Triangle(rings[ringCount - 1, segment],
                    center + Vector3.up * MoundHeight(0f, 0f, height, phase),
                    rings[ringCount - 1, next]);
            }

            // Smooth lighting across the relief while preserving physical texel
            // density on steep faces. Later fractured boulders keep sharp edges.
            mesh.SmoothTerrainSurface(firstVertex, mesh.Vertices.Count - firstVertex);
        }

        private static void UnionShelf(MeshDraft mesh, int landmarkIndex,
            ArenaCircleObstacle bounds, float height)
        {
            // World-aligned cells give the shelves an irregular terrain topology
            // instead of visible rings converging on the center of a model base.
            float step = landmarkIndex < PveRegionLayout.MajorObstacleCount ? 0.85f : 0.65f;
            float minimumX = Mathf.Floor((bounds.Center.X - bounds.Radius) / step) * step;
            float minimumZ = Mathf.Floor((bounds.Center.Z - bounds.Radius) / step) * step;
            float maximumX = bounds.Center.X + bounds.Radius;
            float maximumZ = bounds.Center.Z + bounds.Radius;
            int firstVertex = mesh.Vertices.Count;
            for (float z = minimumZ; z < maximumZ; z += step)
            {
                for (float x = minimumX; x < maximumX; x += step)
                {
                    Vector3 a = new Vector3(x, 0f, z);
                    Vector3 b = new Vector3(x, 0f, z + step);
                    Vector3 c = new Vector3(x + step, 0f, z + step);
                    Vector3 d = new Vector3(x + step, 0f, z);
                    ShelfTriangle(mesh, a, b, c, landmarkIndex, height);
                    ShelfTriangle(mesh, a, c, d, landmarkIndex, height);
                }
            }
            mesh.SmoothTerrainSurface(firstVertex, mesh.Vertices.Count - firstVertex);
        }

        private static void ShelfTriangle(MeshDraft mesh, Vector3 a, Vector3 b, Vector3 c,
            int landmarkIndex, float height)
        {
            Vector3[] source = { a, b, c };
            var clipped = new List<Vector3>(4);
            Vector3 previous = c;
            bool previousInside = LandmarkInteriorDistance(previous, landmarkIndex) >= 0.03f;
            for (int index = 0; index < source.Length; index++)
            {
                Vector3 current = source[index];
                bool currentInside = LandmarkInteriorDistance(current, landmarkIndex) >= 0.03f;
                if (currentInside != previousInside)
                {
                    Vector3 inside = currentInside ? current : previous;
                    Vector3 outside = currentInside ? previous : current;
                    for (int iteration = 0; iteration < 16; iteration++)
                    {
                        Vector3 middle = (inside + outside) * 0.5f;
                        if (LandmarkInteriorDistance(middle, landmarkIndex) >= 0.03f) { inside = middle; }
                        else { outside = middle; }
                    }
                    clipped.Add(inside);
                }
                if (currentInside) { clipped.Add(current); }
                previous = current;
                previousInside = currentInside;
            }
            for (int index = 1; index + 1 < clipped.Count; index++)
            {
                mesh.Triangle(ShelfPoint(clipped[0], landmarkIndex, height),
                    ShelfPoint(clipped[index], landmarkIndex, height),
                    ShelfPoint(clipped[index + 1], landmarkIndex, height));
            }
        }

        private static Vector3 ShelfPoint(Vector3 point, int landmarkIndex, float height)
        {
            float inside = Mathf.Max(0f, LandmarkInteriorDistance(point, landmarkIndex) - 0.03f);
            float broad = Noise(point.x + landmarkIndex * 9.31f, point.z, 0.12f);
            if (landmarkIndex == 0 || landmarkIndex == 2)
            {
                // The abbey and wooded middle ground use low eroded banks, not a rounded
                // two-meter display plinth. Keep the exact Core-union toe, but
                // break its interior into shallow offset banks and rubble beds.
                float folds = Noise(point.x - 37f, point.z + 19f, 0.28f);
                float erodedEdge = 1.15f + Noise(point.x, point.z, 0.23f) * 1.6f;
                float bank = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inside / erodedEdge));
                point.y = 0.025f + (0.28f + broad * 0.55f + folds * 0.55f) * bank;
                return point;
            }
            if (landmarkIndex >= PveRegionLayout.MajorObstacleCount)
            {
                float pocket = Noise(point.x - 17f, point.z + 24f, 0.24f);
                float edgeWidth = 0.45f + Noise(point.x, point.z, 0.27f) * 1.7f;
                float soilEdge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inside / edgeWidth));
                point.y = 0.025f + height * (0.08f + pocket * 1.5f) * soilEdge;
                return point;
            }
            float layer = height * (0.34f + broad * 0.93f);
            float course = Mathf.Floor(layer / 0.38f) * 0.38f;
            float bevel = Mathf.SmoothStep(0f, 1f, Mathf.Repeat(layer, 0.38f) / 0.38f);
            float erosion = (Noise(point.x, point.z - 61f, 0.69f) - 0.5f) * 0.19f;
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(inside / 0.85f));
            point.y = 0.025f + Mathf.Max(0.1f, course + bevel * 0.38f + erosion) * edge;
            return point;
        }

        private static float LandmarkInteriorDistance(Vector3 point, int landmarkIndex)
        {
            float distance = float.NegativeInfinity;
            int start = PveRegionLayout.GetLandmarkObstacleStart(landmarkIndex);
            int count = PveRegionLayout.GetLandmarkObstacleCount(landmarkIndex);
            for (int index = start; index < start + count; index++)
            {
                ArenaCircleObstacle circle = PveRegionLayout.GetObstacle(index);
                float dx = point.x - circle.Center.X;
                float dz = point.z - circle.Center.Z;
                distance = Mathf.Max(distance, circle.Radius - Mathf.Sqrt(dx * dx + dz * dz));
            }
            return distance;
        }

        private static void Fern(MeshDraft mesh, Vector3 center, float scale, ref uint random)
        {
            float phase = Next(ref random) * Mathf.PI * 2f;
            for (int frond = 0; frond < 3; frond++)
            {
                float angle = phase + frond * 2.399963f;
                for (int leaf = 1; leaf <= 4; leaf++)
                {
                    float t = leaf / 5f;
                    Vector3 stem = center + Polar(scale * t * 0.72f, angle,
                        scale * Mathf.Sin(t * Mathf.PI * 0.85f) * 0.72f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 tip = stem + Polar(scale * (1f - t) * 0.25f,
                            angle + side * 1.12f, -scale * 0.065f);
                        Vector3 middle = Vector3.Lerp(stem, tip, 0.45f);
                        Vector3 width = Polar(scale * 0.025f, angle, 0.02f);
                        mesh.Leaf(stem, middle - width, tip, middle + width);
                    }
                }
            }
        }

        private static float MoundHeight(float x, float z, float height, float phase)
        {
            float along = x * Mathf.Cos(phase) + z * Mathf.Sin(phase);
            float across = -x * Mathf.Sin(phase) + z * Mathf.Cos(phase);
            // Three offset, unequal limestone shoulders create an eroded saddle
            // instead of every ring climbing to one central conical summit.
            float first = Mathf.Exp(-((along + 0.32f) * (along + 0.32f) / 0.31f +
                (across + 0.12f) * (across + 0.12f) / 0.42f));
            float second = 0.86f * Mathf.Exp(-((along - 0.4f) * (along - 0.4f) / 0.23f +
                (across - 0.25f) * (across - 0.25f) / 0.29f));
            float third = 0.72f * Mathf.Exp(-((along - 0.13f) * (along - 0.13f) / 0.3f +
                (across + 0.48f) * (across + 0.48f) / 0.19f));
            float shoulder = Mathf.Max(first, Mathf.Max(second, third));
            float edge = Mathf.SmoothStep(0f, 1f,
                Mathf.Clamp01((1f - Mathf.Sqrt(x * x + z * z)) / 0.18f));
            return 0.025f + height * (0.12f + shoulder * 1.12f) * edge;
        }

        private static void FracturedRock(MeshDraft mesh, Vector3 center,
            Vector3 size, float yaw, ref uint random)
        {
            const int sides = 9;
            var rings = new Vector3[3, sides];
            Quaternion rotation = Quaternion.Euler(-9f + Next(ref random) * 18f,
                yaw * Mathf.Rad2Deg, -13f + Next(ref random) * 26f);
            for (int side = 0; side < sides; side++)
            {
                float angle = side * Mathf.PI * 2f / sides;
                float jag = 0.78f + Next(ref random) * 0.22f;
                for (int ring = 0; ring < 3; ring++)
                {
                    float extent = ring == 0 ? 0.8f : ring == 1 ? 1f : 0.54f;
                    Vector3 point = new Vector3(Mathf.Cos(angle) * size.x * jag * extent,
                        size.y * (ring == 0 ? -0.24f : ring == 1 ? 0.3f : 0.77f) +
                        (Next(ref random) - 0.5f) * size.y * 0.23f,
                        Mathf.Sin(angle) * size.z * jag * extent);
                    rings[ring, side] = center + rotation * point;
                }
            }
            for (int side = 0; side < sides; side++)
            {
                int next = (side + 1) % sides;
                mesh.Quad(rings[0, side], rings[1, side], rings[1, next], rings[0, next]);
                mesh.Quad(rings[1, side], rings[2, side], rings[2, next], rings[1, next]);
                mesh.Triangle(rings[2, side], center + rotation * Vector3.up * size.y * 0.85f,
                    rings[2, next]);
            }
        }

        private static void Arch(MeshDraft mesh, Vector3 center, float width,
            float pierHeight, float yaw, ref uint random)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            float inner = width * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 point = center + rotation * new Vector3(side * (inner + 0.42f), 0f, 0f);
                Pier(mesh, point, pierHeight, 0.88f, ref random);
                Block(mesh, point + Vector3.up * (pierHeight - 0.1f),
                    new Vector3(1.18f, 0.22f, 1.2f), yaw, ref random);
            }

            const int voussoirs = 17;
            for (int segment = 0; segment < voussoirs; segment++)
            {
                float a = segment * Mathf.PI / voussoirs + 0.005f;
                float b = (segment + 1) * Mathf.PI / voussoirs - 0.005f;
                float outer = inner + 0.43f + Next(ref random) * 0.1f;
                Vector3[] front = new Vector3[4];
                Vector3[] back = new Vector3[4];
                float[] radii = { inner, outer, outer, inner };
                float[] angles = { a, a, b, b };
                for (int corner = 0; corner < 4; corner++)
                {
                    Vector3 point = new Vector3(Mathf.Cos(angles[corner]) * radii[corner],
                        pierHeight + Mathf.Sin(angles[corner]) * radii[corner], 0f);
                    float chipped = corner == 1 || corner == 2 ? Next(ref random) * 0.035f : 0f;
                    front[corner] = center + rotation * (point + Vector3.back * (0.43f - chipped));
                    back[corner] = center + rotation * (point + Vector3.forward * (0.43f - chipped));
                }

                mesh.Quad(front[3], front[2], front[1], front[0]);
                mesh.Quad(back[0], back[1], back[2], back[3]);
                for (int edge = 0; edge < 4; edge++)
                {
                    int next = (edge + 1) % 4;
                    mesh.Quad(front[edge], front[next], back[next], back[edge]);
                }
            }
        }

        private static void AbbeyRemnant(MeshDraft mesh, Vector3 bottom, float yaw, ref uint random)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            // A filled lower wall makes the blocked footprint honest. The tall
            // opening is an elevated ruined window, never a false traversable door.
            Block(mesh, bottom + Vector3.up * 0.72f, new Vector3(6.1f, 1.44f, 0.96f), yaw, ref random, true);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 pier = bottom + rotation * new Vector3(side * 1.5f, 0f, 0f);
                float height = side < 0 ? 5.8f : 4.9f;
                Block(mesh, pier + Vector3.up * (height * 0.5f),
                    new Vector3(1.05f, height, 1.05f), yaw, ref random, true);
                for (int course = 0; course < 7; course++)
                {
                    Block(mesh, pier + Vector3.up * (0.35f + course * 0.74f),
                        new Vector3(course % 2 == 0 ? 1.18f : 1.10f, 0.19f, 1.16f), yaw, ref random, true);
                }
                Vector3 buttress = bottom + rotation * new Vector3(side * 2.65f, 0f, 0.08f);
                Block(mesh, buttress + Vector3.up * (side < 0 ? 1.55f : 1.05f),
                    new Vector3(0.95f, side < 0 ? 3.1f : 2.1f, 1.1f), yaw, ref random, true);
            }
            // Semicircular voussoirs without duplicate full-height arch piers.
            WindowArch(mesh, bottom + Vector3.up * 3.7f, 1.02f, 0.34f, 0.56f, yaw, ref random);
            Block(mesh, bottom + Vector3.up * 1.65f, new Vector3(3.6f, 0.2f, 1.18f), yaw, ref random, true);
            Block(mesh, bottom + rotation * new Vector3(-1.5f, 5.95f, 0f),
                new Vector3(1.3f, 0.32f, 1.24f), yaw + 7f, ref random, true);
        }

        private static void WatchtowerRemnant(MeshDraft mesh, Vector3 bottom, float yaw, ref uint random)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            // Broad masonry masses have measured texture detail and a few real
            // bevels/quoins; thousands of stacked miniature blocks are unnecessary.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 pier = bottom + rotation * new Vector3(side * 2.25f, 0f, 0f);
                float height = side < 0 ? 10.9f : 9.45f;
                Block(mesh, pier + Vector3.up * (height * 0.5f),
                    new Vector3(1.35f, height, 1.65f), yaw, ref random, true);
                for (int course = 0; course < 8; course++)
                {
                    Block(mesh, pier + Vector3.up * (0.4f + course * 1.3f),
                        new Vector3(1.48f, 0.24f, 1.79f), yaw, ref random, true);
                }
                Vector3 rear = bottom + rotation * new Vector3(side * 2.25f, 0f, 3.4f);
                Block(mesh, rear + Vector3.up * (side < 0 ? 3.7f : 2.8f),
                    new Vector3(1.15f, side < 0 ? 7.4f : 5.6f, 1.35f), yaw, ref random, true);
                Block(mesh, bottom + rotation * new Vector3(side * 2.25f, 1.9f, 1.65f),
                    new Vector3(1.15f, 3.8f, 3.1f), yaw, ref random, true);
            }
            // Solid lower courses and elevated window sill communicate that the
            // entire ruin foundation is impassable Core cover, including the void.
            Block(mesh, bottom + Vector3.up * 1.4f, new Vector3(5.8f, 2.8f, 1.5f), yaw, ref random, true);
            Block(mesh, bottom + Vector3.up * 3.05f, new Vector3(6.1f, 0.28f, 1.8f), yaw, ref random, true);
            WindowArch(mesh, bottom + Vector3.up * 6.7f, 1.57f, 0.44f, 0.79f, yaw, ref random);
            Block(mesh, bottom + Vector3.up * 8.78f, new Vector3(5.85f, 0.28f, 1.85f), yaw, ref random, true);
            Block(mesh, bottom + rotation * new Vector3(-1.2f, 9.35f, 0f),
                new Vector3(2.8f, 0.9f, 1.45f), yaw, ref random, true);
            Block(mesh, bottom + rotation * new Vector3(-2.25f, 11.03f, 0f),
                new Vector3(1.64f, 0.25f, 1.96f), yaw, ref random, true);
        }

        private static void WindowArch(MeshDraft mesh, Vector3 spring, float inner,
            float thickness, float halfDepth, float yaw, ref uint random)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            const int segments = 13;
            for (int segment = 0; segment < segments; segment++)
            {
                float a = segment * Mathf.PI / segments + 0.003f;
                float b = (segment + 1) * Mathf.PI / segments - 0.003f;
                float outer = inner + thickness + Next(ref random) * 0.055f;
                Vector3 p0 = new Vector3(Mathf.Cos(a) * inner, Mathf.Sin(a) * inner, 0f);
                Vector3 p1 = new Vector3(Mathf.Cos(a) * outer, Mathf.Sin(a) * outer, 0f);
                Vector3 p2 = new Vector3(Mathf.Cos(b) * outer, Mathf.Sin(b) * outer, 0f);
                Vector3 p3 = new Vector3(Mathf.Cos(b) * inner, Mathf.Sin(b) * inner, 0f);
                Vector3 front = Vector3.back * halfDepth;
                Vector3 back = Vector3.forward * halfDepth;
                Vector3 f0 = spring + rotation * (p0 + front), f1 = spring + rotation * (p1 + front);
                Vector3 f2 = spring + rotation * (p2 + front), f3 = spring + rotation * (p3 + front);
                Vector3 b0 = spring + rotation * (p0 + back), b1 = spring + rotation * (p1 + back);
                Vector3 b2 = spring + rotation * (p2 + back), b3 = spring + rotation * (p3 + back);
                mesh.Quad(f3, f2, f1, f0); mesh.Quad(b0, b1, b2, b3);
                mesh.Quad(f0, f1, b1, b0); mesh.Quad(f1, f2, b2, b1);
                mesh.Quad(f2, f3, b3, b2); mesh.Quad(f3, f0, b0, b3);
            }
        }

        private static void Pier(MeshDraft mesh, Vector3 bottom, float height,
            float width, ref uint random)
        {
            int courses = Mathf.CeilToInt(height / 0.31f);
            float courseHeight = height / courses;
            for (int course = 0; course < courses; course++)
            {
                Block(mesh, bottom + Vector3.up * (courseHeight * (course + 0.5f)),
                    new Vector3(width * (course == 0 ? 1.23f : 1f), courseHeight - 0.018f,
                        width * (course == 0 ? 1.23f : 1.04f)),
                    (Next(ref random) - 0.5f) * 3f, ref random);
            }
        }

        private static void BrokenWall(MeshDraft mesh, Vector3 center, float width,
            float height, float depth, float yaw, ref uint random)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            int columns = Mathf.Max(2, Mathf.CeilToInt(width / 0.8f));
            float stoneWidth = width / columns;
            for (int column = 0; column < columns; column++)
            {
                float run = column / (columns - 1f);
                float brokenHeight = height * (0.22f + Mathf.Sin(run * Mathf.PI) * 0.64f) +
                    (Next(ref random) - 0.5f) * 0.8f;
                int courses = Mathf.Max(1, Mathf.CeilToInt(brokenHeight / 0.32f));
                for (int course = 0; course < courses; course++)
                {
                    float x = (column + 0.5f) * stoneWidth - width * 0.5f;
                    x += course % 2 == 0 ? stoneWidth * 0.12f : -stoneWidth * 0.12f;
                    bool exposed = course == courses - 1;
                    float stoneHeight = exposed ? Mathf.Clamp(brokenHeight - course * 0.32f, 0.09f, 0.3f) : 0.3f;
                    float stoneLength = stoneWidth * (exposed ? 0.65f + Next(ref random) * 0.31f : 0.99f);
                    Block(mesh, center + rotation * new Vector3(x + (exposed ? (Next(ref random) - 0.5f) * 0.2f : 0f),
                            course * 0.32f + stoneHeight * 0.5f, (Next(ref random) - 0.5f) * 0.12f),
                        new Vector3(stoneLength, stoneHeight, depth * (0.88f + Next(ref random) * 0.14f)),
                        yaw + (Next(ref random) - 0.5f) * (exposed ? 12f : 3f), ref random, true);
                }
            }
        }

        private static void Block(MeshDraft mesh, Vector3 center, Vector3 size,
            float yaw, ref uint random, bool eroded = false)
        {
            float halfX = size.x * 0.5f;
            float halfZ = size.z * 0.5f;
            float bevel = Mathf.Min(eroded ? 0.08f : 0.035f,
                Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * (eroded ? 0.24f : 0.09f));
            Vector2[] outline =
            {
                new Vector2(-halfX + bevel, -halfZ), new Vector2(halfX - bevel, -halfZ),
                new Vector2(halfX, -halfZ + bevel), new Vector2(halfX, halfZ - bevel),
                new Vector2(halfX - bevel, halfZ), new Vector2(-halfX + bevel, halfZ),
                new Vector2(-halfX, halfZ - bevel), new Vector2(-halfX, -halfZ + bevel)
            };
            var rings = new Vector3[3, 8];
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            for (int ring = 0; ring < 3; ring++)
            {
                float y = ring == 0 ? -size.y * 0.5f : ring == 1
                    ? size.y * 0.5f - bevel : size.y * 0.5f;
                float inset = ring == 2 ? 0.96f : 1f;
                for (int corner = 0; corner < 8; corner++)
                {
                    float chip = eroded ? 0.94f + Next(ref random) * 0.08f : 1f;
                    rings[ring, corner] = center + rotation * new Vector3(outline[corner].x * inset * chip,
                        y + (ring == 2 ? (Next(ref random) - 0.5f) * bevel * (eroded ? 2.3f : 0.9f) : 0f),
                        outline[corner].y * inset * chip);
                }
            }

            for (int ring = 0; ring < 2; ring++)
            {
                for (int corner = 0; corner < 8; corner++)
                {
                    int next = (corner + 1) % 8;
                    mesh.Quad(rings[ring, corner], rings[ring + 1, corner],
                        rings[ring + 1, next], rings[ring, next]);
                }
            }

            for (int corner = 1; corner < 7; corner++)
            {
                mesh.Triangle(rings[2, 0], rings[2, corner + 1], rings[2, corner]);
                mesh.Triangle(rings[0, 0], rings[0, corner], rings[0, corner + 1]);
            }
        }

        private static void Tree(MeshDraft bark, MeshDraft leaves, MeshDraft highlights,
            Vector3 center, float height, float spread, ref uint random, bool distant = false)
        {
            SunbreakCanopyGeometry.TreeGeometry tree = SunbreakCanopyGeometry.Build(
                center, height, spread, ref random, distant);
            tree.Bark.AppendTo(bark.Vertices, bark.Normals, bark.Uv, bark.Triangles);
            tree.Leaves.AppendTo(leaves.Vertices, leaves.Normals, leaves.Uv, leaves.Triangles);
            tree.Highlights.AppendTo(highlights.Vertices, highlights.Normals, highlights.Uv, highlights.Triangles);
        }

        private static void Cylinder(MeshDraft mesh, Vector3 bottom, Vector3 top,
            float lowerRadius, float upperRadius, int sides)
        {
            Vector3 axis = (top - bottom).normalized;
            Vector3 right = Vector3.Cross(axis, Mathf.Abs(axis.z) < 0.9f ? Vector3.forward : Vector3.right).normalized;
            Vector3 forward = Vector3.Cross(right, axis).normalized;
            for (int side = 0; side < sides; side++)
            {
                float a = side * Mathf.PI * 2f / sides;
                float b = (side + 1) * Mathf.PI * 2f / sides;
                Vector3 offsetA = right * Mathf.Cos(a) + forward * Mathf.Sin(a);
                Vector3 offsetB = right * Mathf.Cos(b) + forward * Mathf.Sin(b);
                Vector3 lowA = bottom + offsetA * lowerRadius;
                Vector3 lowB = bottom + offsetB * lowerRadius;
                Vector3 topA = top + offsetA * upperRadius;
                Vector3 topB = top + offsetB * upperRadius;
                float vLength = Vector3.Distance(bottom, top) * 0.8f;
                mesh.SmoothQuad(lowA, topA, topB, lowB,
                    offsetA, offsetA, offsetB, offsetB,
                    new Vector2(side / (float)sides, 0f),
                    new Vector2(side / (float)sides, vLength),
                    new Vector2((side + 1f) / sides, vLength),
                    new Vector2((side + 1f) / sides, 0f));
                mesh.Triangle(topA, top, topB);
                mesh.Triangle(lowB, bottom, lowA);
            }
        }

        private static Vector3[] ChippedOutline(Vector3 center, float width,
            float depth, float angle, ref uint random)
        {
            var result = new Vector3[7];
            for (int index = 0; index < result.Length; index++)
            {
                float a = index * Mathf.PI * 2f / result.Length;
                Vector3 point = new Vector3(Mathf.Cos(a) * width * (0.74f + Next(ref random) * 0.26f),
                    0f, Mathf.Sin(a) * depth * (0.74f + Next(ref random) * 0.26f));
                result[index] = center + Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 0f) * point;
            }

            return result;
        }

        private static void AddClippedFloor(MeshDraft mesh, Vector3[] polygon, Vector3 origin)
        {
            var clipped = new List<Vector3>(polygon);
            clipped = Clip(clipped, 0, origin.x, true);
            clipped = Clip(clipped, 0, origin.x + CellSize, false);
            clipped = Clip(clipped, 2, origin.z, true);
            clipped = Clip(clipped, 2, origin.z + CellSize, false);
            for (int index = 1; index < clipped.Count - 1; index++)
            {
                mesh.Triangle(clipped[0], clipped[index + 1], clipped[index]);
            }
        }

        private static List<Vector3> Clip(List<Vector3> polygon, int axis, float edge, bool above)
        {
            var result = new List<Vector3>(polygon.Count + 1);
            if (polygon.Count == 0) { return result; }
            Vector3 previous = polygon[polygon.Count - 1];
            bool wasInside = above ? previous[axis] >= edge : previous[axis] <= edge;
            foreach (Vector3 current in polygon)
            {
                bool inside = above ? current[axis] >= edge : current[axis] <= edge;
                if (inside != wasInside)
                {
                    float t = (edge - previous[axis]) / (current[axis] - previous[axis]);
                    result.Add(Vector3.LerpUnclamped(previous, current, t));
                }
                if (inside) { result.Add(current); }
                previous = current;
                wasInside = inside;
            }

            return result;
        }

        private static void ClampToFootprint(MeshDraft mesh, int landmarkIndex)
        {
            var clipped = new bool[mesh.Vertices.Count];
            for (int index = 0; index < mesh.Vertices.Count; index++)
            {
                Vector3 point = mesh.Vertices[index];
                if (LandmarkInteriorDistance(point, landmarkIndex) < 0.025f)
                {
                    Vector3 nearest = point;
                    float minimumDistance = float.PositiveInfinity;
                    int start = PveRegionLayout.GetLandmarkObstacleStart(landmarkIndex);
                    int count = PveRegionLayout.GetLandmarkObstacleCount(landmarkIndex);
                    for (int circleIndex = start; circleIndex < start + count; circleIndex++)
                    {
                        ArenaCircleObstacle circle = PveRegionLayout.GetObstacle(circleIndex);
                        Vector3 center = V(circle.Center);
                        Vector3 offset = new Vector3(point.x - center.x, 0f, point.z - center.z);
                        Vector3 projected = center + offset.normalized * (circle.Radius - 0.025f);
                        projected.y = point.y;
                        float distance = (point - projected).sqrMagnitude;
                        if (distance >= minimumDistance) { continue; }
                        nearest = projected;
                        minimumDistance = distance;
                    }
                    mesh.Vertices[index] = nearest;
                    clipped[index] = true;
                }
            }
            for (int triangle = 0; triangle < mesh.Triangles.Count; triangle += 3)
            {
                int a = mesh.Triangles[triangle];
                int b = mesh.Triangles[triangle + 1];
                int c = mesh.Triangles[triangle + 2];
                if (!clipped[a] && !clipped[b] && !clipped[c]) { continue; }
                Vector3 normal = Vector3.Cross(mesh.Vertices[b] - mesh.Vertices[a],
                    mesh.Vertices[c] - mesh.Vertices[a]).normalized;
                if (normal.sqrMagnitude < 0.5f) { normal = Vector3.up; }
                mesh.Normals[a] = normal;
                mesh.Normals[b] = normal;
                mesh.Normals[c] = normal;
            }
        }

        public static void Validate(Transform visualRoot)
        {
            if (visualRoot == null)
            {
                throw new InvalidOperationException("Sunbreak Basin requires its authored visual root.");
            }
            string[] groups = { "Ground", "MeadowDetail", "Encampments", "BlockerIslands", "OuterRidges" };
            foreach (string group in groups)
            {
                if (visualRoot.Find(group) == null)
                {
                    throw new InvalidOperationException("Sunbreak Basin is missing " + group + ".");
                }
            }
            if (visualRoot.childCount != groups.Length || visualRoot.localScale != Vector3.one)
            {
                throw new InvalidOperationException("Sunbreak Basin requires exactly five unscaled presentation groups.");
            }
            Transform benchmarkOwner = visualRoot.Find("BlockerIslands/Blocker_7");
            if (benchmarkOwner != null)
            {
                SunbreakScannedRockAuthoring.Validate();
                Transform benchmark = benchmarkOwner.Find(SunbreakScannedRockAuthoring.InstanceName);
                MeshFilter benchmarkMesh = benchmark != null ? benchmark.GetComponent<MeshFilter>() : null;
                if (benchmarkMesh == null || benchmarkMesh.sharedMesh !=
                    SunbreakScannedRockAuthoring.RequireSourceRock().sharedMesh)
                {
                    throw new InvalidOperationException("Frostbound Watch must retain its one admitted scanned rock03 mesh.");
                }
                SunbreakFortAssets.Validate();
                SunbreakVegetationAssets.Validate();
                SunbreakCanopyAuthoring.Validate();
                string[] measuredMeshes =
                {
                    "BlockerIslands/Blocker_0/MeasuredWatchtower",
                    "BlockerIslands/Blocker_0/MeasuredTowerWing",
                    "BlockerIslands/Blocker_0/MeasuredSouthReturn",
                    "BlockerIslands/Blocker_0/MeasuredNorthReturn",
                    "BlockerIslands/Blocker_0/MeasuredBrokenAbutment",
                    "BlockerIslands/Blocker_7/" + SunbreakRuinMasonryAuthoring.RootName,
                    "BlockerIslands/Blocker_7/MeasuredVegetation_0",
                    "MeadowDetail/MeasuredMeadow_0",
                    "MeadowDetail/FracturedRuinCourt"
                };
                foreach (string path in measuredMeshes)
                {
                    Transform part = visualRoot.Find(path);
                    MeshFilter filter = part != null ? part.GetComponent<MeshFilter>() : null;
                    if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                    {
                        throw new InvalidOperationException("Frostbound measured composition is missing " + path + ".");
                    }
                }
                SunbreakRuinMasonryAuthoring.Validate(
                    benchmarkOwner.Find(SunbreakRuinMasonryAuthoring.RootName).gameObject);
            }
            if (visualRoot.GetComponentsInChildren<Collider>(true).Length != 0 ||
                visualRoot.GetComponentsInChildren<Rigidbody>(true).Length != 0 ||
                visualRoot.GetComponentsInChildren<Light>(true).Length != 0 ||
                visualRoot.GetComponentsInChildren<Camera>(true).Length != 0 ||
                visualRoot.GetComponentsInChildren<ParticleSystem>(true).Length != 0)
            {
                throw new InvalidOperationException("Sunbreak Basin must contain only static presentation, never physics or global lighting.");
            }

            Transform floorRoot = visualRoot.Find("Ground/ContinuousMeadow");
            MeshFilter floor = floorRoot != null ? floorRoot.GetComponent<MeshFilter>() : null;
            if (floor == null || floor.sharedMesh == null)
            {
                throw new InvalidOperationException("Sunbreak Basin requires its generated continuous meadow mesh.");
            }
            Bounds floorBounds = floor.sharedMesh.bounds;
            if (floorBounds.min != Vector3.zero || floorBounds.max != new Vector3(CellSize, 0f, CellSize))
            {
                throw new InvalidOperationException("Sunbreak ground must fill its exact flat 64 m chunk.");
            }
            int renderers = visualRoot.GetComponentsInChildren<MeshRenderer>(true).Length;
            long triangles = 0;
            foreach (MeshFilter filter in visualRoot.GetComponentsInChildren<MeshFilter>(true))
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null) { throw new InvalidOperationException("Sunbreak mesh is missing: " + filter.name); }
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    triangles += mesh.GetIndexCount(submesh) / 3;
                }
                foreach (Vector3 local in mesh.vertices)
                {
                    Vector3 point = filter.transform.TransformPoint(local);
                    if (point.y <= MaximumWalkableDetailHeight + 0.001f || OutsidePlayableBounds(point)) { continue; }
                    if (!InsideObstacle(point, 0.005f))
                    {
                        throw new InvalidOperationException("Sunbreak solid '" + filter.name +
                            "' extends into Core-walkable ground at " + point + ".");
                    }
                }
            }

            if (triangles > MaximumTriangles || renderers > MaximumRenderers)
            {
                throw new InvalidOperationException("Sunbreak chunk exceeds its bounded presentation: " +
                    triangles + " triangles, " + renderers + " renderers.");
            }
        }

        private static bool InsideObstacle(Vector3 point, float padding)
        {
            for (int index = 0; index < PveRegionLayout.ObstacleCount; index++)
            {
                ArenaCircleObstacle obstacle = PveRegionLayout.GetObstacle(index);
                float dx = point.x - obstacle.Center.X;
                float dz = point.z - obstacle.Center.Z;
                float radius = obstacle.Radius + padding;
                if (dx * dx + dz * dz <= radius * radius) { return true; }
            }
            return false;
        }

        private static bool OutsidePlayableBounds(Vector3 point) =>
            point.x <= PveRegionLayout.MinimumX || point.x >= PveRegionLayout.MaximumX ||
            point.z <= PveRegionLayout.MinimumZ || point.z >= PveRegionLayout.MaximumZ;

        private static bool Owns(Vector3 origin, Vector3 point) =>
            point.x >= origin.x && point.x < origin.x + CellSize &&
            point.z >= origin.z && point.z < origin.z + CellSize;

        private static bool OwnsInset(Vector3 origin, Vector3 point, float inset) =>
            point.x >= origin.x + inset && point.x < origin.x + CellSize - inset &&
            point.z >= origin.z + inset && point.z < origin.z + CellSize - inset;

        private static Vector3 V(Float3 value) => new Vector3(value.X, value.Y, value.Z);
        internal static Vector2 ProjectRockSurfaceUv(Vector3 point, Vector3 faceNormal) =>
            (Mathf.Abs(faceNormal.y) > 0.65f ? new Vector2(point.x, point.z)
                : Mathf.Abs(faceNormal.x) > Mathf.Abs(faceNormal.z)
                    ? new Vector2(point.z, point.y) : new Vector2(point.x, point.y)) * 0.4f;

        private static Vector3 Polar(float radius, float angle, float height) =>
            new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius);
        private static float Noise(float x, float z, float scale) =>
            Mathf.PerlinNoise(x * scale + 183.3f, z * scale + 291.7f);
        private static float Next(ref uint value)
        {
            value = value * 1664525u + 1013904223u;
            return (value & 0x00ffffffu) / 16777216f;
        }

        private static Transform Child(Transform parent, string name)
        {
            var child = new GameObject(name);
            child.layer = 2;
            child.transform.SetParent(parent, false);
            return child.transform;
        }

        private static void Publish(Transform parent, string name, MeshDraft draft,
            Material material, Vector3 origin, string key, bool shadows)
        {
            if (draft.Vertices.Count == 0) { return; }
            if (material == null) { throw new InvalidOperationException("Sunbreak material missing for " + name); }
            var authored = new Mesh { name = "Sunbreak_" + key + "_" + name };
            if (draft.Vertices.Count > 65535) { authored.indexFormat = IndexFormat.UInt32; }
            for (int index = 0; index < draft.Vertices.Count; index++)
            {
                draft.Vertices[index] -= origin;
            }
            authored.SetVertices(draft.Vertices);
            authored.SetUVs(0, draft.Uv);
            authored.SetTriangles(draft.Triangles, 0);
            authored.SetNormals(draft.Normals);
            authored.RecalculateTangents();
            authored.RecalculateBounds();
            EnsureFolder(MeshFolder);
            string path = MeshFolder + "/" + authored.name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null) { AssetDatabase.CreateAsset(authored, path); existing = authored; }
            else
            {
                EditorUtility.CopySerialized(authored, existing);
                Object.DestroyImmediate(authored);
                EditorUtility.SetDirty(existing);
            }

            Transform root = Child(parent, name);
            root.gameObject.AddComponent<MeshFilter>().sharedMesh = existing;
            MeshRenderer renderer = root.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            GameObjectUtility.SetStaticEditorFlags(root.gameObject, StaticEditorFlags.BatchingStatic);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) { return; }
            string parent = System.IO.Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
        }

        private sealed class MeshDraft
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uv = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, bool macro = false)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                int first = Vertices.Count;
                Add(a, normal, macro);
                Add(b, normal, macro);
                Add(c, normal, macro);
                Triangles.Add(first);
                Triangles.Add(first + 1);
                Triangles.Add(first + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, bool macro = false)
            {
                Triangle(a, b, c, macro);
                Triangle(a, c, d, macro);
            }

            public void DoubleTriangle(Vector3 a, Vector3 b, Vector3 c)
            {
                Triangle(a, b, c);
                Triangle(c, b, a);
            }

            public void DoubleQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
            {
                Quad(a, b, c, d);
                Quad(d, c, b, a);
            }

            public void Leaf(Vector3 bottom, Vector3 left, Vector3 tip, Vector3 right)
            {
                Vector2 baseUv = new Vector2(0.5f, 0f);
                Vector2 leftUv = new Vector2(0f, 0.45f);
                Vector2 tipUv = new Vector2(0.5f, 1f);
                Vector2 rightUv = new Vector2(1f, 0.45f);
                TexturedTriangle(bottom, left, tip, baseUv, leftUv, tipUv);
                TexturedTriangle(bottom, tip, right, baseUv, tipUv, rightUv);
                TexturedTriangle(tip, left, bottom, tipUv, leftUv, baseUv);
                TexturedTriangle(right, tip, bottom, rightUv, tipUv, baseUv);
            }

            public void SmoothQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
                Vector3 normalA, Vector3 normalB, Vector3 normalC, Vector3 normalD,
                Vector2 uvA, Vector2 uvB, Vector2 uvC, Vector2 uvD)
            {
                TexturedTriangle(a, b, c, uvA, uvB, uvC, normalA, normalB, normalC);
                TexturedTriangle(a, c, d, uvA, uvC, uvD, normalA, normalC, normalD);
            }

            public void SmoothTerrainSurface(int firstVertex, int vertexCount, bool projectUvs = true)
            {
                int end = firstVertex + vertexCount;
                var sums = new Dictionary<Vector3, Vector3>();
                for (int index = firstVertex; index < end; index += 3)
                {
                    Vector3 weightedNormal = Vector3.Cross(Vertices[index + 1] - Vertices[index],
                        Vertices[index + 2] - Vertices[index]);
                    for (int corner = 0; corner < 3; corner++)
                    {
                        Vector3 point = Vertices[index + corner];
                        sums.TryGetValue(point, out Vector3 sum);
                        sums[point] = sum + weightedNormal;
                    }
                }
                for (int index = firstVertex; index < end; index++)
                {
                    Vector3 point = Vertices[index];
                    Vector3 normal = sums[point].normalized;
                    Normals[index] = normal.sqrMagnitude < 0.5f ? Vector3.up : normal;
                }
                if (!projectUvs) { return; }
                // One projection per triangle avoids interpolating different
                // projection axes through a face. The former XZ-only override
                // squeezed an entire cliff height into its narrow ground width.
                // Low soil shelves explicitly restore continuous world macro UVs
                // after this step; the flat streamed floor is unaffected.
                for (int index = firstVertex; index < end; index += 3)
                {
                    Vector3 face = Vector3.Cross(Vertices[index + 1] - Vertices[index],
                        Vertices[index + 2] - Vertices[index]).normalized;
                    for (int corner = 0; corner < 3; corner++)
                    {
                        Vector3 point = Vertices[index + corner];
                        Uv[index + corner] = ProjectRockSurfaceUv(point, face);
                    }
                }
            }

            public void UseWorldMacroUvs()
            {
                for (int index = 0; index < Vertices.Count; index++)
                {
                    Vector3 point = Vertices[index];
                    Uv[index] = new Vector2((point.x - TextureMinimum) / TextureSpan,
                        (point.z - TextureMinimum) / TextureSpan);
                }
            }

            private void TexturedTriangle(Vector3 a, Vector3 b, Vector3 c,
                Vector2 uvA, Vector2 uvB, Vector2 uvC)
            {
                Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                TexturedTriangle(a, b, c, uvA, uvB, uvC, normal, normal, normal);
            }

            private void TexturedTriangle(Vector3 a, Vector3 b, Vector3 c,
                Vector2 uvA, Vector2 uvB, Vector2 uvC,
                Vector3 normalA, Vector3 normalB, Vector3 normalC)
            {
                int first = Vertices.Count;
                Vertices.Add(a); Vertices.Add(b); Vertices.Add(c);
                Uv.Add(uvA); Uv.Add(uvB); Uv.Add(uvC);
                Normals.Add(normalA); Normals.Add(normalB); Normals.Add(normalC);
                Triangles.Add(first); Triangles.Add(first + 1); Triangles.Add(first + 2);
            }

            private void Add(Vector3 point, Vector3 normal, bool macro)
            {
                Vertices.Add(point);
                Normals.Add(normal.sqrMagnitude < 0.5f ? Vector3.up : normal);
                Uv.Add(macro
                    ? new Vector2((point.x - TextureMinimum) / TextureSpan,
                        (point.z - TextureMinimum) / TextureSpan)
                    : ProjectRockSurfaceUv(point, normal));
            }
        }
    }
}
