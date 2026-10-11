using System;
using System.Collections.Generic;
using SessionRpg.Core;
using SessionRpg.Editor.Assets;
using SessionRpg.Unity.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace SessionRpg.Editor.Authoring
{
    internal static class EnvironmentAuthoring
    {
        private const float ArenaVisualLavaMargin = 8f;
        private const float ArenaFortressRadius = 21.65f;
        private const float ArenaFortressWallOutwardOffset = 0.4f;
        private const float ArenaFortressTowerOutwardOffset = 0.7f;
        private const float ArenaCliffBackdropRadius = 27.4f;
        private const float ArenaFortressAngleOffset = 18f;
        private const float ArenaBoundaryInnerRadius = ArenaLayout.SafeRadius - 0.08f;
        private const float ArenaBoundaryOuterRadius = ArenaLayout.SafeRadius + 0.24f;
        private const float ArenaReliquaryRadius = ArenaLayout.OuterKillRadius + 0.95f;
        private const float ArenaFireAnchorHeight = -0.9f;
        private const float ArenaFireAnchorPlanarScale = 0.9f;
        private const float ArenaFireAnchorVerticalScale = 1.37f;
        private const float FortressWallBaseSink = -0.72f;
        private const float FortressWallSinkStep = -0.06f;
        private const float FortressTowerSink = -0.72f;
        private const float ShowcaseTowerSink = -0.78f;
        private const float BossSafeRadius = 13f;
        private const float BossVisualLavaRadius = 26f;
        private const float BossFortressRadius = 19.65f;
        private const float BossFortressPlanarScale = 0.86f;
        private const float BossBrazierRadius = 19.4f;
        private const float BossBrazierAngle = 32f;
        private const float BackdropRadius = 80f;
        private const float LevelingHalfLength = 28f;
        private const float LevelingOuterHalfWidth = 11f;
        internal const int LevelingStaticTriangleBudget = 65000;
        private const int FloorSegmentCount = 32;
        private const int DecorationLayer = 8;
        private const int GroundLayer = 10;
        private const int IgnoreRaycastLayer = 2;

        private static readonly string[] ArenaFireAnchorNames =
        {
            "FortressBrazier_LeftFlank",
            "FortressBrazier_LeftShoulder",
            "FortressBrazier_RightShoulder",
            "FortressBrazier_RightFlank",
        };

        private static readonly float[] ArenaFireAnchorRadii =
        {
            22.1f,
            22f,
            22f,
            22.1f,
        };

        private static readonly float[] ArenaFireAnchorAngles =
        {
            30f,
            54f,
            126f,
            150f,
        };

        private static readonly float[] LevelingSectionX =
        {
            -LevelingHalfLength,
            -24f,
            -19.5f,
            -15.2f,
            -10.7f,
            -6.4f,
            -1.5f,
            3.8f,
            8.4f,
            12.7f,
            17.1f,
            21.5f,
            25.2f,
            LevelingHalfLength,
        };

        private static readonly float[] LevelingSectionHalfWidth =
        {
            8.5f,
            8.75f,
            9.15f,
            8.55f,
            9.35f,
            10.25f,
            9.8f,
            10.55f,
            9.3f,
            10.05f,
            9.6f,
            10.65f,
            9.25f,
            8.5f,
        };

        private static readonly float[] LevelingSectionCenterZ =
        {
            0f,
            0.05f,
            0.25f,
            -0.2f,
            0.4f,
            0.1f,
            -0.3f,
            0.2f,
            0.45f,
            0f,
            -0.25f,
            0.25f,
            -0.1f,
            0f,
        };

        private const string EnvironmentMeshFolder =
            "Assets/SessionRpg/Art/Environment";

        internal static Vector3 GetArenaFireLightPosition(int index)
        {
            Vector3 position = GetArenaFireAnchorPosition(index);
            position.y = 1.55f;
            return position;
        }

        public static GameObject BuildArena(
            FortressSourceArtSet fortressSourceArt,
            GameObject arenaFloorSourceArt,
            Material stone,
            Material arenaFloorStone,
            Material arenaFloorTrim,
            Material arenaFloorGrout,
            Material ruinStone,
            Material lava,
            Material lavaSeam,
            Material abyss,
            Material blueAccent,
            Material redAccent,
            Material floorAccent,
            Material particleMaterial)
        {
            GameObject root = new GameObject("ArenaEnvironment");
            BuildBackdrop(root.transform, abyss, false);
            BuildArenaCliffBackdrop(root.transform, ruinStone);
            BuildLava(
                root.transform,
                ArenaLayout.OuterKillRadius + ArenaVisualLavaMargin,
                lava,
                lavaSeam,
                false,
                11f);
            CreatePrimitive(
                "SafeStone",
                PrimitiveType.Cylinder,
                root.transform,
                new Vector3(0f, -0.3f, 0f),
                new Vector3(
                    ArenaLayout.SafeRadius * 2f,
                    0.3f,
                    ArenaLayout.SafeRadius * 2f),
                stone,
                true,
                GroundLayer,
                false);

            AddArenaFloorModel(
                arenaFloorSourceArt,
                root.transform,
                "ArenaFloorVisual",
                arenaFloorStone,
                arenaFloorTrim,
                arenaFloorGrout);
            BuildArenaArchitecturalDepth(
                root.transform,
                ruinStone,
                arenaFloorGrout,
                lavaSeam);
            BuildTeamThreshold(
                root.transform,
                -ArenaLayout.TeamSpawnOffset,
                blueAccent,
                "PartyThreshold");
            BuildTeamThreshold(
                root.transform,
                ArenaLayout.TeamSpawnOffset,
                redAccent,
                "EnemyThreshold");
            BuildArenaPillar(
                root.transform,
                "ArenaPillar_South",
                -ArenaLayout.PillarOffsetZ,
                fortressSourceArt,
                ruinStone,
                lavaSeam,
                floorAccent);
            BuildArenaPillar(
                root.transform,
                "ArenaPillar_North",
                ArenaLayout.PillarOffsetZ,
                fortressSourceArt,
                ruinStone,
                lavaSeam,
                floorAccent);
            BuildPerimeterComposition(
                root.transform,
                ArenaFortressRadius,
                false,
                fortressSourceArt,
                ruinStone,
                lavaSeam,
                particleMaterial);
            BuildArenaAtmosphere(
                root.transform,
                particleMaterial,
                false);

            return root;
        }

        public static GameObject BuildLeveling(
            FortressSourceArtSet fortressSourceArt,
            Material causewayStone,
            Material causewayStoneVariation,
            Material causewayGrout,
            Material causewayCinder,
            Material causewaySoot,
            Material ruinStone,
            Material lavaCrust,
            Material lavaCore,
            Material abyss,
            Material floorAccent,
            Material particleMaterial)
        {
            if (fortressSourceArt == null)
            {
                throw new ArgumentNullException(nameof(fortressSourceArt));
            }

            RequireLevelingMaterial(causewayStone, nameof(causewayStone));
            RequireLevelingMaterial(
                causewayStoneVariation,
                nameof(causewayStoneVariation));
            RequireLevelingMaterial(causewayGrout, nameof(causewayGrout));
            RequireLevelingMaterial(causewayCinder, nameof(causewayCinder));
            RequireLevelingMaterial(causewaySoot, nameof(causewaySoot));
            RequireLevelingMaterial(ruinStone, nameof(ruinStone));
            RequireLevelingMaterial(lavaCrust, nameof(lavaCrust));
            RequireLevelingMaterial(lavaCore, nameof(lavaCore));
            RequireLevelingMaterial(abyss, nameof(abyss));
            RequireLevelingMaterial(floorAccent, nameof(floorAccent));
            RequireLevelingMaterial(particleMaterial, nameof(particleMaterial));
            ValidateLevelingLayoutDefinition();

            // This hierarchy is presentation-only. The joined, non-circular floor
            // deliberately carries no collider; gameplay ground and bounds remain
            // authoritative inputs supplied by the generated scene composition.
            GameObject root = new GameObject("LevelingEnvironment");
            BuildLevelingBackdrop(root.transform, abyss);
            BuildLevelingCinderShoulders(root.transform, causewayCinder);
            BuildLevelingLavaGullies(root.transform, lavaCrust, lavaCore);
            BuildLevelingCauseway(
                root.transform,
                causewayStone,
                causewayStoneVariation,
                causewayGrout);
            BuildLevelingGroundStory(
                root.transform,
                causewaySoot,
                floorAccent);
            BuildLevelingRouteDressing(
                root.transform,
                causewayCinder,
                ruinStone);
            BuildLevelingLandmarks(
                root.transform,
                fortressSourceArt,
                ruinStone,
                lavaCore);
            BuildLevelingAtmosphere(root.transform, particleMaterial);
            return root;
        }

        private static void RequireLevelingMaterial(
            Material material,
            string parameterName)
        {
            if (material == null)
            {
                throw new ArgumentNullException(parameterName);
            }
        }

        private static void BuildLevelingBackdrop(
            Transform parent,
            Material abyss)
        {
            GameObject backdrop = CreatePrimitive(
                "AshenCausewayAbyssBackdrop",
                PrimitiveType.Cube,
                parent,
                new Vector3(0f, -2.15f, 0f),
                new Vector3(76f, 0.08f, 54f),
                abyss,
                false,
                IgnoreRaycastLayer);
            Renderer renderer = backdrop.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void BuildLevelingCauseway(
            Transform parent,
            Material stone,
            Material stoneVariation,
            Material darkStone)
        {
            Mesh causeway = EnsureMesh(
                EnvironmentMeshFolder + "/AshenCausewayFloor.asset",
                CreateLevelingCausewayMesh());
            CreateMeshVisual(
                "AshenCausewayFloor",
                parent,
                Vector3.zero,
                causeway,
                new[] { stone, stoneVariation, darkStone },
                true,
                true);
        }

        private static Mesh CreateLevelingCausewayMesh()
        {
            var vertices = new List<Vector3>(480);
            var normals = new List<Vector3>(480);
            var uvs = new List<Vector2>(480);
            var triangles = new[]
            {
                new List<int>(420),
                new List<int>(120),
                new List<int>(240),
            };
            float[] laneStops = { 0f, 0.2f, 0.43f, 0.69f, 1f };

            for (int section = 0;
                 section < LevelingSectionX.Length - 1;
                 section++)
            {
                float x0 = LevelingSectionX[section];
                float x1 = LevelingSectionX[section + 1];
                float halfWidth0 = LevelingSectionHalfWidth[section];
                float halfWidth1 = LevelingSectionHalfWidth[section + 1];
                float center0 = LevelingSectionCenterZ[section];
                float center1 = LevelingSectionCenterZ[section + 1];

                for (int lane = 0; lane < laneStops.Length - 1; lane++)
                {
                    Vector2 lowerStart = GetLevelingFloorNode(
                        section,
                        lane,
                        laneStops,
                        x0,
                        center0,
                        halfWidth0);
                    Vector2 lowerEnd = GetLevelingFloorNode(
                        section + 1,
                        lane,
                        laneStops,
                        x1,
                        center1,
                        halfWidth1);
                    Vector2 upperEnd = GetLevelingFloorNode(
                        section + 1,
                        lane + 1,
                        laneStops,
                        x1,
                        center1,
                        halfWidth1);
                    Vector2 upperStart = GetLevelingFloorNode(
                        section,
                        lane + 1,
                        laneStops,
                        x0,
                        center0,
                        halfWidth0);
                    int firstMaterial = ResolveLevelingFloorMaterial(
                        section,
                        lane,
                        false);
                    int secondMaterial = ResolveLevelingFloorMaterial(
                        section,
                        lane,
                        true);

                    if (((section + lane) & 1) == 0)
                    {
                        AddTopTriangle(
                            vertices,
                            normals,
                            uvs,
                            triangles[firstMaterial],
                            lowerStart,
                            upperEnd,
                            lowerEnd,
                            0.008f,
                            20f);
                        AddTopTriangle(
                            vertices,
                            normals,
                            uvs,
                            triangles[secondMaterial],
                            lowerStart,
                            upperStart,
                            upperEnd,
                            0.008f,
                            20f);
                    }
                    else
                    {
                        AddTopTriangle(
                            vertices,
                            normals,
                            uvs,
                            triangles[firstMaterial],
                            lowerStart,
                            upperStart,
                            lowerEnd,
                            0.008f,
                            20f);
                        AddTopTriangle(
                            vertices,
                            normals,
                            uvs,
                            triangles[secondMaterial],
                            lowerEnd,
                            upperStart,
                            upperEnd,
                            0.008f,
                            20f);
                    }
                }

                AddLevelingCausewayEdge(
                    vertices,
                    normals,
                    uvs,
                    triangles[2],
                    x0,
                    x1,
                    center0,
                    center1,
                    halfWidth0,
                    halfWidth1,
                    true);
                AddLevelingCausewayEdge(
                    vertices,
                    normals,
                    uvs,
                    triangles[2],
                    x0,
                    x1,
                    center0,
                    center1,
                    halfWidth0,
                    halfWidth1,
                    false);
            }

            // This mesh is repeated across additive 64 m cells. Matching endpoint
            // profiles and omitting terminal faces keeps internal chunk seams
            // continuous instead of stacking two end caps on the same plane.

            Mesh mesh = CreateMesh(
                "AshenCausewayFloor",
                vertices,
                normals,
                uvs,
                triangles);
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AddWorldUvTriangle(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector3 first,
            Vector3 second,
            Vector3 third,
            Vector3 normal,
            float uvScale)
        {
            int start = vertices.Count;
            vertices.Add(first);
            vertices.Add(second);
            vertices.Add(third);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uvs.Add(new Vector2(first.x, first.z) * uvScale);
            uvs.Add(new Vector2(second.x, second.z) * uvScale);
            uvs.Add(new Vector2(third.x, third.z) * uvScale);
            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
        }

        private static Vector2 GetLevelingFloorNode(
            int section,
            int lane,
            IReadOnlyList<float> laneStops,
            float sectionX,
            float center,
            float halfWidth)
        {
            float xOffset = 0f;
            if (section > 0 && section < LevelingSectionX.Length - 1 &&
                lane > 0 && lane < laneStops.Count - 1)
            {
                int pattern = ((section * 11) + (lane * 5)) % 7;
                xOffset = (pattern - 3) * 0.075f;
            }

            float z = Mathf.Lerp(
                center - halfWidth,
                center + halfWidth,
                laneStops[lane]);
            return new Vector2(
                sectionX + xOffset,
                z + GetLevelingLaneJitter(section, lane));
        }

        private static int ResolveLevelingFloorMaterial(
            int section,
            int lane,
            bool secondTriangle)
        {
            int pattern = (section * 7) + (lane * 11) +
                (secondTriangle ? 5 : 0);
            return pattern % 13 == 0 || pattern % 17 == 4 ? 1 : 0;
        }

        private static float GetLevelingLaneJitter(
            int section,
            int lane)
        {
            if (lane == 0 || lane == 4)
            {
                return 0f;
            }

            int pattern = ((section * 5) + (lane * 3)) % 7;
            return (pattern - 3) * 0.035f;
        }

        private static void AddLevelingCausewayEdge(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            float x0,
            float x1,
            float center0,
            float center1,
            float halfWidth0,
            float halfWidth1,
            bool north)
        {
            float direction = north ? 1f : -1f;
            float z0 = center0 + (halfWidth0 * direction);
            float z1 = center1 + (halfWidth1 * direction);
            float outerZ0 = z0 + (0.42f * direction);
            float outerZ1 = z1 + (0.42f * direction);
            if (north)
            {
                AddVerticalFace(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    new Vector3(x0, 0.004f, z0),
                    new Vector3(x0, -0.43f, outerZ0),
                    new Vector3(x1, -0.43f, outerZ1),
                    new Vector3(x1, 0.004f, z1));
                return;
            }

            AddVerticalFace(
                vertices,
                normals,
                uvs,
                indices,
                new Vector3(x1, 0.004f, z1),
                new Vector3(x1, -0.43f, outerZ1),
                new Vector3(x0, -0.43f, outerZ0),
                new Vector3(x0, 0.004f, z0));
        }

        private static void AddLevelingCausewayEndCap(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            int section,
            bool west)
        {
            float x = LevelingSectionX[section];
            float halfWidth = LevelingSectionHalfWidth[section];
            float center = LevelingSectionCenterZ[section];
            float outerX = x + (west ? -0.42f : 0.42f);
            if (west)
            {
                AddVerticalFace(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    new Vector3(x, 0.004f, center + halfWidth),
                    new Vector3(outerX, -0.43f, center + halfWidth),
                    new Vector3(outerX, -0.43f, center - halfWidth),
                    new Vector3(x, 0.004f, center - halfWidth));
                return;
            }

            AddVerticalFace(
                vertices,
                normals,
                uvs,
                indices,
                new Vector3(x, 0.004f, center - halfWidth),
                new Vector3(outerX, -0.43f, center - halfWidth),
                new Vector3(outerX, -0.43f, center + halfWidth),
                new Vector3(x, 0.004f, center + halfWidth));
        }

        private static void BuildLevelingGroundStory(
            Transform parent,
            Material soot,
            Material accent)
        {
            Transform root = new GameObject(
                "AshenCausewayGroundStory").transform;
            root.SetParent(parent, false);
            root.gameObject.layer = IgnoreRaycastLayer;

            Mesh groundStory = EnsureMesh(
                EnvironmentMeshFolder + "/AshenCausewayGroundStory.asset",
                CreateLevelingGroundStoryMesh());
            CreateMeshVisual(
                "SootAndBrokenWards",
                root,
                Vector3.zero,
                groundStory,
                new[] { soot, accent },
                false,
                false);
        }

        private static Mesh CreateLevelingGroundStoryMesh()
        {
            var vertices = new List<Vector3>(720);
            var normals = new List<Vector3>(720);
            var uvs = new List<Vector2>(720);
            var triangles = new[]
            {
                new List<int>(540),
                new List<int>(240),
            };
            Vector2[] sootCenters =
            {
                new Vector2(-25.1f, -5.8f),
                new Vector2(-21.7f, 5.1f),
                new Vector2(-16.4f, -2.2f),
                new Vector2(-12.8f, 5.7f),
                new Vector2(-8.2f, -5.3f),
                new Vector2(-3.8f, 2.9f),
                new Vector2(1.2f, -4.5f),
                new Vector2(6.3f, 5.2f),
                new Vector2(11.1f, -2.8f),
                new Vector2(15.6f, 4.1f),
                new Vector2(20.3f, -5.1f),
                new Vector2(24.6f, 3.8f),
            };
            for (int index = 0; index < sootCenters.Length; index++)
            {
                AddLevelingGroundPatch(
                    vertices,
                    normals,
                    uvs,
                    triangles[0],
                    sootCenters[index],
                    0.72f + ((index * 5) % 4 * 0.19f),
                    0.46f + ((index * 7) % 5 * 0.12f),
                    index);
            }

            Vector2[] wardCenters =
            {
                new Vector2(-23.2f, 1.8f),
                new Vector2(-10.8f, -3.4f),
                new Vector2(3.9f, 3.2f),
                new Vector2(18.7f, -2.4f),
            };
            for (int index = 0; index < wardCenters.Length; index++)
            {
                Vector2 center = wardCenters[index];
                float direction = index % 2 == 0 ? 1f : -1f;
                AddFlatRibbon(
                    vertices,
                    normals,
                    uvs,
                    triangles[1],
                    center + new Vector2(-0.9f, -0.34f * direction),
                    center + new Vector2(-0.12f, 0.21f * direction),
                    0.035f,
                    0.027f);
                AddFlatRibbon(
                    vertices,
                    normals,
                    uvs,
                    triangles[1],
                    center + new Vector2(0.1f, 0.28f * direction),
                    center + new Vector2(0.82f, -0.13f * direction),
                    0.026f,
                    0.027f);
                AddFlatRibbon(
                    vertices,
                    normals,
                    uvs,
                    triangles[1],
                    center + new Vector2(-0.38f, 0.62f * direction),
                    center + new Vector2(0.16f, 0.44f * direction),
                    0.022f,
                    0.028f);
            }

            Mesh mesh = CreateMesh(
                "AshenCausewayGroundStory",
                vertices,
                normals,
                uvs,
                triangles);
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AddLevelingGroundPatch(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 center,
            float radiusX,
            float radiusZ,
            int seed)
        {
            const int sideCount = 10;
            const float y = 0.023f;
            Vector3 centerPoint = new Vector3(center.x, y, center.y);
            for (int side = 0; side < sideCount; side++)
            {
                float firstAngle = ((side / (float)sideCount) * Mathf.PI * 2f) +
                    (seed * 0.37f);
                float secondAngle = (((side + 1f) / sideCount) * Mathf.PI * 2f) +
                    (seed * 0.37f);
                float firstScale = 0.86f +
                    (((side * 3) + seed) % 5 * 0.055f);
                float secondScale = 0.86f +
                    ((((side + 1) * 3) + seed) % 5 * 0.055f);
                AddWorldUvTriangle(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    centerPoint,
                    new Vector3(
                        center.x + (Mathf.Cos(secondAngle) * radiusX * secondScale),
                        y,
                        center.y + (Mathf.Sin(secondAngle) * radiusZ * secondScale)),
                    new Vector3(
                        center.x + (Mathf.Cos(firstAngle) * radiusX * firstScale),
                        y,
                        center.y + (Mathf.Sin(firstAngle) * radiusZ * firstScale)),
                    Vector3.up,
                    0.22f);
            }
        }

        private static void BuildLevelingCinderShoulders(
            Transform parent,
            Material darkStone)
        {
            Mesh cinder = EnsureMesh(
                EnvironmentMeshFolder + "/AshenCausewayCinder.asset",
                CreateLevelingCinderMesh());
            CreateMeshVisual(
                "AshenCausewayCinderShoulders",
                parent,
                Vector3.zero,
                cinder,
                new[] { darkStone },
                false,
                true);
        }

        private static Mesh CreateLevelingCinderMesh()
        {
            var vertices = new List<Vector3>(160);
            var normals = new List<Vector3>(160);
            var uvs = new List<Vector2>(160);
            var indices = new List<int>(240);
            for (int section = 0;
                 section < LevelingSectionX.Length - 1;
                 section++)
            {
                float x0 = LevelingSectionX[section];
                float x1 = LevelingSectionX[section + 1];
                float northInner0 = LevelingSectionCenterZ[section] +
                    LevelingSectionHalfWidth[section] + 0.38f;
                float northInner1 = LevelingSectionCenterZ[section + 1] +
                    LevelingSectionHalfWidth[section + 1] + 0.38f;
                float southInner0 = LevelingSectionCenterZ[section] -
                    LevelingSectionHalfWidth[section] - 0.38f;
                float southInner1 = LevelingSectionCenterZ[section + 1] -
                    LevelingSectionHalfWidth[section + 1] - 0.38f;
                float outer0 = 23.6f + ((section % 4) * 0.42f);
                float outer1 = 23.6f + (((section + 1) % 4) * 0.42f);

                AddTopQuad(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    new Vector2(x0, northInner0),
                    new Vector2(x1, northInner1),
                    new Vector2(x1, outer1),
                    new Vector2(x0, outer0),
                    -0.405f,
                    7f);
                AddTopQuad(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    new Vector2(x0, -outer0),
                    new Vector2(x1, -outer1),
                    new Vector2(x1, southInner1),
                    new Vector2(x0, southInner0),
                    -0.405f,
                    7f);
            }

            AddTopQuad(
                vertices,
                normals,
                uvs,
                indices,
                new Vector2(-36f, -24.8f),
                new Vector2(-LevelingHalfLength, -24.8f),
                new Vector2(-LevelingHalfLength, 24.8f),
                new Vector2(-36f, 24.8f),
                -0.405f,
                7f);
            AddTopQuad(
                vertices,
                normals,
                uvs,
                indices,
                new Vector2(LevelingHalfLength, -24.8f),
                new Vector2(36f, -24.8f),
                new Vector2(36f, 24.8f),
                new Vector2(LevelingHalfLength, 24.8f),
                -0.405f,
                7f);

            Mesh mesh = CreateMesh(
                "AshenCausewayCinder",
                vertices,
                normals,
                uvs,
                new[] { indices });
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void BuildLevelingRouteDressing(
            Transform parent,
            Material darkStone,
            Material ruinStone)
        {
            Transform root = new GameObject(
                "AshenCausewayEdgeDressing").transform;
            root.SetParent(parent, false);
            root.gameObject.layer = IgnoreRaycastLayer;

            Mesh banks = EnsureMesh(
                EnvironmentMeshFolder + "/AshenCausewayBanks.asset",
                CreateLevelingCinderBankMesh());
            CreateMeshVisual(
                "FracturedCinderBanks",
                root,
                Vector3.zero,
                banks,
                new[] { darkStone, ruinStone },
                true,
                true);

            Mesh rubble = EnsureMesh(
                EnvironmentMeshFolder + "/AshenCausewayRubble.asset",
                CreateLevelingRuinDressingMesh());
            CreateMeshVisual(
                "CausewayRuinDressing",
                root,
                Vector3.zero,
                rubble,
                new[] { ruinStone, darkStone },
                true,
                true);
        }

        private static Mesh CreateLevelingCinderBankMesh()
        {
            var vertices = new List<Vector3>(1280);
            var normals = new List<Vector3>(1280);
            var uvs = new List<Vector2>(1280);
            var triangles = new[]
            {
                new List<int>(1080),
                new List<int>(660),
            };

            for (int side = 0; side < 2; side++)
            {
                bool north = side == 0;
                float direction = north ? 1f : -1f;
                for (int section = 0;
                     section < LevelingSectionX.Length - 1;
                     section++)
                {
                    float x0 = LevelingSectionX[section];
                    float x1 = LevelingSectionX[section + 1];
                    float edge0 = LevelingSectionCenterZ[section] +
                        (LevelingSectionHalfWidth[section] * direction);
                    float edge1 = LevelingSectionCenterZ[section + 1] +
                        (LevelingSectionHalfWidth[section + 1] * direction);
                    float lip0 = edge0 + (direction *
                        (0.68f + (GetLevelingBankPattern(section, side) * 0.055f)));
                    float lip1 = edge1 + (direction *
                        (0.68f + (GetLevelingBankPattern(section + 1, side) * 0.055f)));
                    int lipMaterial = ((section * 3) + side) % 5 == 1
                        ? 0
                        : 1;
                    AddLevelingBankQuad(
                        vertices,
                        normals,
                        uvs,
                        triangles[lipMaterial],
                        x0,
                        x1,
                        edge0,
                        edge1,
                        lip0,
                        lip1,
                        -0.025f,
                        -0.31f,
                        north);

                    float lavaOuter0 = edge0 + (direction *
                        (2.48f + (GetLevelingBankPattern(section + 2, side) * 0.09f)));
                    float lavaOuter1 = edge1 + (direction *
                        (2.48f + (GetLevelingBankPattern(section + 3, side) * 0.09f)));
                    float shoulderOuter0 = edge0 + (direction *
                        (3.55f + (GetLevelingBankPattern(section + 4, side) * 0.16f)));
                    float shoulderOuter1 = edge1 + (direction *
                        (3.55f + (GetLevelingBankPattern(section + 5, side) * 0.16f)));
                    int shoulderMaterial = ((section * 7) + side) % 6 == 2
                        ? 1
                        : 0;
                    AddLevelingBankQuad(
                        vertices,
                        normals,
                        uvs,
                        triangles[shoulderMaterial],
                        x0,
                        x1,
                        lavaOuter0,
                        lavaOuter1,
                        shoulderOuter0,
                        shoulderOuter1,
                        -0.36f,
                        -0.16f +
                            (GetLevelingBankPattern(section + 6, side) * 0.022f),
                        north);

                    // A fractured outer ridge gives the chasm a real vertical
                    // silhouette. It stays well outside the Core-owned lane and
                    // never becomes a second playable shelf.
                    float ridgeCrest0 = edge0 + (direction *
                        (6.15f + (GetLevelingBankPattern(section + 7, side) * 0.24f)));
                    float ridgeCrest1 = edge1 + (direction *
                        (6.15f + (GetLevelingBankPattern(section + 8, side) * 0.24f)));
                    float ridgeOuter0 = edge0 + (direction *
                        (8.9f + (GetLevelingBankPattern(section + 9, side) * 0.31f)));
                    float ridgeOuter1 = edge1 + (direction *
                        (8.9f + (GetLevelingBankPattern(section + 10, side) * 0.31f)));
                    float crestY0 = 0.34f +
                        (GetLevelingBankPattern(section + 11, side) * 0.14f);
                    float crestY1 = 0.34f +
                        (GetLevelingBankPattern(section + 12, side) * 0.14f);
                    AddLevelingBankQuad(
                        vertices,
                        normals,
                        uvs,
                        triangles[shoulderMaterial],
                        x0,
                        x1,
                        shoulderOuter0,
                        shoulderOuter1,
                        ridgeCrest0,
                        ridgeCrest1,
                        -0.16f,
                        (crestY0 + crestY1) * 0.5f,
                        north);
                    AddLevelingBankQuad(
                        vertices,
                        normals,
                        uvs,
                        triangles[0],
                        x0,
                        x1,
                        ridgeCrest0,
                        ridgeCrest1,
                        ridgeOuter0,
                        ridgeOuter1,
                        (crestY0 + crestY1) * 0.5f,
                        -0.34f,
                        north);
                }
            }

            Mesh mesh = CreateMesh(
                "AshenCausewayBanks",
                vertices,
                normals,
                uvs,
                triangles);
            mesh.RecalculateTangents();
            return mesh;
        }

        private static int GetLevelingBankPattern(int section, int side)
        {
            return ((section * 7) + (side * 3)) % 5;
        }

        private static void AddLevelingBankQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            float x0,
            float x1,
            float innerZ0,
            float innerZ1,
            float outerZ0,
            float outerZ1,
            float innerY,
            float outerY,
            bool north)
        {
            Vector3 innerStart = new Vector3(x0, innerY, innerZ0);
            Vector3 innerEnd = new Vector3(x1, innerY, innerZ1);
            Vector3 outerStart = new Vector3(x0, outerY, outerZ0);
            Vector3 outerEnd = new Vector3(x1, outerY, outerZ1);
            if (north)
            {
                AddVerticalFace(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    innerStart,
                    innerEnd,
                    outerEnd,
                    outerStart);
                return;
            }

            AddVerticalFace(
                vertices,
                normals,
                uvs,
                indices,
                outerStart,
                outerEnd,
                innerEnd,
                innerStart);
        }

        private static Mesh CreateLevelingRuinDressingMesh()
        {
            var vertices = new List<Vector3>(720);
            var normals = new List<Vector3>(720);
            var uvs = new List<Vector2>(720);
            var triangles = new[]
            {
                new List<int>(540),
                new List<int>(180),
            };
            Vector2[] centers =
            {
                new Vector2(-26.2f, 9.55f),
                new Vector2(-24.7f, 10.35f),
                new Vector2(-23.1f, -10.6f),
                new Vector2(-20.8f, -11.35f),
                new Vector2(-17.4f, 10.65f),
                new Vector2(-15.6f, 11.45f),
                new Vector2(-13.2f, -10.25f),
                new Vector2(-10.2f, -11.55f),
                new Vector2(-7.6f, 11.15f),
                new Vector2(-5.3f, 12.05f),
                new Vector2(-2.2f, -11.15f),
                new Vector2(0.5f, -12.1f),
                new Vector2(3.4f, 11.7f),
                new Vector2(6.7f, 12.45f),
                new Vector2(8.8f, -10.65f),
                new Vector2(11.6f, -11.65f),
                new Vector2(14.3f, 10.7f),
                new Vector2(17.2f, 11.75f),
                new Vector2(19.6f, -10.95f),
                new Vector2(22.1f, -12.15f),
                new Vector2(25.1f, 10.45f),
                new Vector2(27.2f, -10.1f),
            };

            for (int index = 0; index < centers.Length; index++)
            {
                float radiusX = 0.34f + ((index * 7) % 5 * 0.105f);
                float radiusZ = 0.28f + ((index * 11) % 4 * 0.09f);
                float height = 0.16f + ((index * 5) % 6 * 0.055f);
                int materialIndex = index % 6 == 4 ? 1 : 0;
                AddLevelingRubbleStone(
                    vertices,
                    normals,
                    uvs,
                    triangles[materialIndex],
                    centers[index],
                    radiusX,
                    radiusZ,
                    height,
                    index);
            }

            Vector2[] outcropCenters =
            {
                new Vector2(-26.4f, 14.2f),
                new Vector2(-22.6f, -15.1f),
                new Vector2(-17.2f, 14.8f),
                new Vector2(-11.4f, -14.4f),
                new Vector2(-6.2f, 15.3f),
                new Vector2(-0.8f, -15.4f),
                new Vector2(4.9f, 14.6f),
                new Vector2(10.1f, -15.2f),
                new Vector2(15.8f, 14.4f),
                new Vector2(20.8f, -15.5f),
                new Vector2(25.7f, 14.6f),
                new Vector2(27.1f, -14.2f),
            };
            for (int index = 0; index < outcropCenters.Length; index++)
            {
                AddLevelingRubbleStone(
                    vertices,
                    normals,
                    uvs,
                    triangles[index % 4 == 1 ? 0 : 1],
                    outcropCenters[index],
                    0.86f + ((index * 3) % 4 * 0.17f),
                    0.72f + ((index * 5) % 3 * 0.16f),
                    0.92f + ((index * 7) % 5 * 0.21f),
                    index + 31);
            }

            Mesh mesh = CreateMesh(
                "AshenCausewayRubble",
                vertices,
                normals,
                uvs,
                triangles);
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AddLevelingRubbleStone(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 center,
            float radiusX,
            float radiusZ,
            float height,
            int seed)
        {
            const float baseY = -0.39f;
            int sideCount = 5 + (seed % 3);
            float rotation = (seed * 1.37f) % (Mathf.PI * 2f);
            Vector3 topCenter = new Vector3(
                center.x + ((((seed * 3) % 5) - 2) * 0.035f),
                baseY + height,
                center.y + ((((seed * 7) % 5) - 2) * 0.03f));
            for (int side = 0; side < sideCount; side++)
            {
                float startAngle = rotation +
                    (side / (float)sideCount * Mathf.PI * 2f);
                float endAngle = rotation +
                    ((side + 1) / (float)sideCount * Mathf.PI * 2f);
                float startRoughness = 0.86f +
                    (((side * 3) + seed) % 5 * 0.07f);
                float endRoughness = 0.86f +
                    ((((side + 1) * 3) + seed) % 5 * 0.07f);
                Vector3 bottomStart = new Vector3(
                    center.x + (Mathf.Cos(startAngle) * radiusX * startRoughness),
                    baseY,
                    center.y + (Mathf.Sin(startAngle) * radiusZ * startRoughness));
                Vector3 bottomEnd = new Vector3(
                    center.x + (Mathf.Cos(endAngle) * radiusX * endRoughness),
                    baseY,
                    center.y + (Mathf.Sin(endAngle) * radiusZ * endRoughness));
                Vector3 topStart = Vector3.Lerp(bottomStart, topCenter, 0.42f);
                Vector3 topEnd = Vector3.Lerp(bottomEnd, topCenter, 0.42f);
                topStart.y = baseY + (height *
                    (0.78f + (((side + seed) % 3) * 0.055f)));
                topEnd.y = baseY + (height *
                    (0.78f + ((((side + 1) + seed) % 3) * 0.055f)));

                AddVerticalFace(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    topStart,
                    bottomStart,
                    bottomEnd,
                    topEnd);
                AddTriangleFace(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    topCenter,
                    topEnd,
                    topStart,
                    Vector3.up);
            }
        }

        private static void BuildLevelingLavaGullies(
            Transform parent,
            Material lava,
            Material lavaSeam)
        {
            Transform root = new GameObject("AshenCausewayLavaGullies").transform;
            root.SetParent(parent, false);
            root.gameObject.layer = IgnoreRaycastLayer;

            Mesh surfaceMesh = EnsureMesh(
                EnvironmentMeshFolder + "/AshenCausewayLava.asset",
                CreateLevelingLavaGullyMesh());
            GameObject surface = CreateMeshVisual(
                "BrokenLavaChannels",
                root,
                Vector3.zero,
                surfaceMesh,
                new[] { lava },
                false,
                false);

            Mesh seamMesh = EnsureMesh(
                EnvironmentMeshFolder + "/AshenCausewayLavaVeins.asset",
                CreateLevelingLavaVeinMesh());
            GameObject seams = CreateMeshVisual(
                "LavaVeins",
                root,
                Vector3.zero,
                seamMesh,
                new[] { lavaSeam },
                false,
                false);

            Renderer surfaceRenderer = surface.GetComponent<Renderer>();
            Renderer seamRenderer = seams.GetComponent<Renderer>();
            HazardSurfaceAnimator animator =
                root.gameObject.AddComponent<HazardSurfaceAnimator>();
            SerializedObject serialized = new SerializedObject(animator);
            serialized.FindProperty("_surface").objectReferenceValue =
                surfaceRenderer;
            serialized.FindProperty("_seams").objectReferenceValue =
                seamRenderer;
            serialized.FindProperty("_phaseOffset").floatValue = 7.4f;
            serialized.FindProperty("_pulseSpeed").floatValue = 0.44f;
            serialized.FindProperty("_surfaceScrollSpeed").vector2Value =
                new Vector2(-0.0022f, 0.0014f);
            serialized.FindProperty("_seamScrollSpeed").vector2Value =
                new Vector2(0.004f, -0.003f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Mesh CreateLevelingLavaGullyMesh()
        {
            var vertices = new List<Vector3>(240);
            var normals = new List<Vector3>(240);
            var uvs = new List<Vector2>(240);
            var indices = new List<int>(360);
            for (int side = 0; side < 2; side++)
            {
                bool north = side == 0;
                float direction = north ? 1f : -1f;
                for (int section = 0;
                     section < LevelingSectionX.Length - 1;
                     section++)
                {
                    if (IsLevelingLavaSectionSkipped(section, side))
                    {
                        continue;
                    }

                    bool startsPool = section == 0 ||
                        IsLevelingLavaSectionSkipped(section - 1, side);
                    bool endsPool = section == LevelingSectionX.Length - 2 ||
                        IsLevelingLavaSectionSkipped(section + 1, side);
                    float x0 = LevelingSectionX[section] +
                        (startsPool ? 0.2f : 0f);
                    float x1 = LevelingSectionX[section + 1] -
                        (endsPool ? 0.2f : 0f);
                    float xMid = Mathf.Lerp(x0, x1, 0.5f) +
                        ((((section * 3) + side) % 5) - 2) * 0.065f;
                    float distance0 = GetLevelingLavaInnerOffset(section, side);
                    float distance1 = GetLevelingLavaInnerOffset(
                        section + 1,
                        side);
                    float inner0 = LevelingSectionCenterZ[section] +
                        (distance0 * direction);
                    float inner1 = LevelingSectionCenterZ[section + 1] +
                        (distance1 * direction);
                    float innerMid = Mathf.Lerp(inner0, inner1, 0.5f) +
                        (direction *
                            ((((section * 7) + side) % 5) - 2) * 0.055f);
                    float width0 = startsPool
                        ? 0.18f
                        : GetLevelingLavaWidth(section, side);
                    float width1 = endsPool
                        ? 0.18f
                        : GetLevelingLavaWidth(section + 1, side);
                    float widthMid = GetLevelingLavaWidth(section + 3, side) +
                        0.12f;
                    float outer0 = inner0 + (width0 * direction);
                    float outerMid = innerMid + (widthMid * direction);
                    float outer1 = inner1 + (width1 * direction);
                    float y = -0.386f +
                        (((section + side) % 3) * 0.002f);
                    AddLevelingLavaQuad(
                        vertices,
                        normals,
                        uvs,
                        indices,
                        new Vector2(x0, inner0),
                        new Vector2(xMid, innerMid),
                        new Vector2(xMid, outerMid),
                        new Vector2(x0, outer0),
                        y,
                        north);
                    AddLevelingLavaQuad(
                        vertices,
                        normals,
                        uvs,
                        indices,
                        new Vector2(xMid, innerMid),
                        new Vector2(x1, inner1),
                        new Vector2(x1, outer1),
                        new Vector2(xMid, outerMid),
                        y,
                        north);
                }
            }

            Mesh mesh = CreateMesh(
                "AshenCausewayLava",
                vertices,
                normals,
                uvs,
                new[] { indices });
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AddLevelingLavaQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 innerStart,
            Vector2 innerEnd,
            Vector2 outerEnd,
            Vector2 outerStart,
            float y,
            bool north)
        {
            if (north)
            {
                AddTopQuad(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    innerStart,
                    innerEnd,
                    outerEnd,
                    outerStart,
                    y,
                    6.5f);
                return;
            }

            AddTopQuad(
                vertices,
                normals,
                uvs,
                indices,
                outerStart,
                outerEnd,
                innerEnd,
                innerStart,
                y,
                6.5f);
        }

        private static Mesh CreateLevelingLavaVeinMesh()
        {
            var vertices = new List<Vector3>(260);
            var normals = new List<Vector3>(260);
            var uvs = new List<Vector2>(260);
            var indices = new List<int>(390);
            for (int side = 0; side < 2; side++)
            {
                float direction = side == 0 ? 1f : -1f;
                for (int section = 0;
                     section < LevelingSectionX.Length - 1;
                     section++)
                {
                    if (IsLevelingLavaSectionSkipped(section, side))
                    {
                        continue;
                    }

                    bool startsPool = section == 0 ||
                        IsLevelingLavaSectionSkipped(section - 1, side);
                    bool endsPool = section == LevelingSectionX.Length - 2 ||
                        IsLevelingLavaSectionSkipped(section + 1, side);
                    float x0 = LevelingSectionX[section] +
                        (startsPool ? 0.24f : 0f);
                    float x1 = LevelingSectionX[section + 1] -
                        (endsPool ? 0.24f : 0f);
                    float xMid = Mathf.Lerp(x0, x1, 0.52f) +
                        ((((section * 3) + side) % 5) - 2) * 0.045f;
                    float startWidth = startsPool
                        ? 0.18f
                        : GetLevelingLavaWidth(section, side);
                    float endWidth = endsPool
                        ? 0.18f
                        : GetLevelingLavaWidth(section + 1, side);
                    float startOffset = GetLevelingLavaInnerOffset(section, side) +
                        (startWidth * (0.42f + (((section + side) % 3) * 0.07f)));
                    float endOffset = GetLevelingLavaInnerOffset(
                        section + 1,
                        side) +
                        (endWidth * (0.48f - (((section + side) % 2) * 0.06f)));
                    Vector2 start = new Vector2(
                        x0,
                        LevelingSectionCenterZ[section] +
                            (startOffset * direction));
                    Vector2 end = new Vector2(
                        x1,
                        LevelingSectionCenterZ[section + 1] +
                            (endOffset * direction));
                    Vector2 middle = Vector2.Lerp(start, end, 0.52f);
                    middle.x = xMid;
                    middle.y += direction *
                        ((((section * 7) + side) % 5) - 2) * 0.07f;
                    float width = 0.065f +
                        (((section + (side * 2)) % 3) * 0.02f);
                    AddFlatRibbon(
                        vertices,
                        normals,
                        uvs,
                        indices,
                        start,
                        middle,
                        width,
                        -0.368f);
                    AddFlatRibbon(
                        vertices,
                        normals,
                        uvs,
                        indices,
                        middle,
                        end,
                        width * 0.82f,
                        -0.368f);
                    if (((section * 3) + side) % 4 == 1)
                    {
                        Vector2 branch = middle + new Vector2(
                            (section & 1) == 0 ? 0.48f : -0.44f,
                            direction * (0.28f + ((section % 3) * 0.08f)));
                        AddFlatRibbon(
                            vertices,
                            normals,
                            uvs,
                            indices,
                            middle,
                            branch,
                            width * 0.58f,
                            -0.367f);
                    }
                }
            }

            Mesh mesh = CreateMesh(
                "AshenCausewayLavaVeins",
                vertices,
                normals,
                uvs,
                new[] { indices });
            mesh.RecalculateTangents();
            return mesh;
        }

        private static bool IsLevelingLavaSectionSkipped(
            int section,
            int side)
        {
            if (section < 0 || section >= LevelingSectionX.Length - 1)
            {
                return true;
            }

            return (section + (side * 2)) % 6 == 4;
        }

        private static float GetLevelingLavaWidth(int section, int side)
        {
            int pattern = ((section * 7) + (side * 3)) % 5;
            return 1.45f + (pattern * 0.2f);
        }

        private static float GetLevelingLavaInnerOffset(
            int section,
            int side)
        {
            float pattern = ((section * 3) + (side * 2)) % 5;
            return LevelingSectionHalfWidth[section] + 0.92f +
                (pattern * 0.075f);
        }

        private static void BuildLevelingLandmarks(
            Transform parent,
            FortressSourceArtSet fortressSourceArt,
            Material ruinStone,
            Material emberMaterial)
        {
            Transform root = CreateLevelingLandmarkRoot(
                parent,
                "AshenCausewayLandmarks");

            Transform refuge = CreateLevelingLandmarkRoot(
                root,
                "WestRefuge");
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallStraight,
                "RefugeNorthWall",
                refuge,
                new Vector3(-15f, -0.42f, 10.8f),
                8f,
                new Vector3(0.94f, 0.56f, 0.94f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                "RefugeSouthWall",
                refuge,
                new Vector3(-7f, -0.5f, -10.8f),
                -11f,
                new Vector3(0.9f, 0.47f, 0.9f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Wardstone,
                "RefugeWaystone",
                refuge,
                new Vector3(-17f, -0.08f, 9.4f),
                12f,
                new Vector3(0.92f, 1f, 0.92f),
                ruinStone,
                emberMaterial);
            AddLevelingBrazier(
                fortressSourceArt,
                "RefugeBrazier",
                refuge,
                new Vector3(-10f, -0.1f, -9.4f),
                -14f,
                0.85f,
                0.35f,
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Tower,
                "RefugeWatchTower",
                refuge,
                new Vector3(-5f, -0.42f, 11.8f),
                -6f,
                new Vector3(0.78f, 0.88f, 0.78f),
                ruinStone,
                emberMaterial);

            Transform cinderApproach = CreateLevelingLandmarkRoot(
                root,
                "CinderApproach");
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                "CinderApproachNorthRuin",
                cinderApproach,
                new Vector3(-7.4f, -0.48f, 11.65f),
                12f,
                new Vector3(0.9f, 0.54f, 0.9f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallStraight,
                "CinderApproachSouthRuin",
                cinderApproach,
                new Vector3(-2.8f, -0.5f, -11.75f),
                -17f,
                new Vector3(0.82f, 0.46f, 0.82f),
                ruinStone,
                emberMaterial);
            AddLevelingBrazier(
                fortressSourceArt,
                "CinderApproachBeacon",
                cinderApproach,
                new Vector3(-4.9f, -0.1f, 10.25f),
                8f,
                0.82f,
                1.05f,
                ruinStone,
                emberMaterial);

            Transform gate = CreateLevelingLandmarkRoot(
                root,
                "BrokenGate");
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                "BrokenGateNorthLeaf",
                gate,
                new Vector3(-12.2f, -0.43f, 11.35f),
                90f,
                new Vector3(0.92f, 0.72f, 0.92f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallStraight,
                "BrokenGateSouthLeaf",
                gate,
                new Vector3(-12.8f, -0.49f, -11.4f),
                90f,
                new Vector3(0.9f, 0.6f, 0.9f),
                ruinStone,
                emberMaterial);
            GameObject fallenGateLeaf = AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                "BrokenGateFallenLeaf",
                gate,
                new Vector3(-15.4f, -0.48f, -14.35f),
                24f,
                new Vector3(0.74f, 0.46f, 0.74f),
                ruinStone,
                emberMaterial);
            fallenGateLeaf.transform.localRotation = Quaternion.Euler(
                8f,
                24f,
                71f);
            AddLevelingBrazier(
                fortressSourceArt,
                "BrokenGateBrazier",
                gate,
                new Vector3(-9.8f, -0.1f, 10.35f),
                9f,
                0.88f,
                1.7f,
                ruinStone,
                emberMaterial);

            Transform court = CreateLevelingLandmarkRoot(
                root,
                "CollapsedCourt");
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Tower,
                "CollapsedCourtTower",
                court,
                new Vector3(1.5f, -0.38f, 13.5f),
                -8f,
                new Vector3(0.86f, 0.8f, 0.86f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallStraight,
                "CollapsedCourtSouthWall",
                court,
                new Vector3(-1.8f, -0.46f, -12.3f),
                4f,
                new Vector3(0.92f, 0.64f, 0.92f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                "CollapsedCourtNorthWall",
                court,
                new Vector3(6.2f, -0.5f, 12.1f),
                -9f,
                new Vector3(0.95f, 0.57f, 0.95f),
                ruinStone,
                emberMaterial);
            GameObject fallenCourtWall = AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallStraight,
                "CollapsedCourtFallenWall",
                court,
                new Vector3(7.2f, -0.47f, 15.2f),
                -31f,
                new Vector3(0.7f, 0.42f, 0.7f),
                ruinStone,
                emberMaterial);
            fallenCourtWall.transform.localRotation = Quaternion.Euler(
                -6f,
                -31f,
                -68f);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Wardstone,
                "CollapsedCourtWaystone",
                court,
                new Vector3(4.6f, -0.08f, -11.55f),
                -18f,
                new Vector3(0.9f, 0.98f, 0.9f),
                ruinStone,
                emberMaterial);
            AddLevelingBrazier(
                fortressSourceArt,
                "CollapsedCourtBrazier",
                court,
                new Vector3(7.7f, -0.1f, -11.15f),
                18f,
                0.9f,
                3.05f,
                ruinStone,
                emberMaterial);

            Transform ossuary = CreateLevelingLandmarkRoot(
                root,
                "OssuaryBend");
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                "OssuaryBendRibWall",
                ossuary,
                new Vector3(3.8f, -0.5f, -11.85f),
                -13f,
                new Vector3(0.88f, 0.5f, 0.88f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Wardstone,
                "OssuaryBendNorthMarker",
                ossuary,
                new Vector3(-2.9f, -0.08f, 10.7f),
                20f,
                new Vector3(0.82f, 1.08f, 0.82f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Wardstone,
                "OssuaryBendSouthMarker",
                ossuary,
                new Vector3(7.2f, -0.08f, -10.45f),
                -24f,
                new Vector3(0.72f, 0.9f, 0.72f),
                ruinStone,
                emberMaterial);

            Transform reliquary = CreateLevelingLandmarkRoot(
                root,
                "ReliquaryYard");
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Tower,
                "ReliquaryNorthTower",
                reliquary,
                new Vector3(22.3f, -0.42f, 13.4f),
                -5f,
                new Vector3(0.9f, 0.9f, 0.9f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Tower,
                "ReliquarySouthTower",
                reliquary,
                new Vector3(24f, -0.48f, -13.4f),
                7f,
                new Vector3(0.85f, 0.82f, 0.85f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallStraight,
                "ReliquaryNorthApproach",
                reliquary,
                new Vector3(15.2f, -0.44f, 12.1f),
                5f,
                new Vector3(0.93f, 0.68f, 0.93f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                "ReliquarySouthApproach",
                reliquary,
                new Vector3(15.8f, -0.5f, -12.3f),
                -7f,
                new Vector3(0.9f, 0.58f, 0.9f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                "ReliquaryNorthGateLeaf",
                reliquary,
                new Vector3(27.2f, -0.48f, 11.45f),
                90f,
                new Vector3(0.88f, 0.64f, 0.88f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallStraight,
                "ReliquarySouthGateLeaf",
                reliquary,
                new Vector3(27f, -0.5f, -11.45f),
                90f,
                new Vector3(0.86f, 0.56f, 0.86f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Wardstone,
                "ReliquaryWaystone",
                reliquary,
                new Vector3(18.5f, -0.08f, 11.55f),
                16f,
                new Vector3(0.94f, 1.04f, 0.94f),
                ruinStone,
                emberMaterial);
            AddLevelingBrazier(
                fortressSourceArt,
                "ReliquaryBrazier",
                reliquary,
                new Vector3(20.1f, -0.1f, 11.35f),
                -11f,
                0.92f,
                4.4f,
                ruinStone,
                emberMaterial);

            Transform eastWatch = CreateLevelingLandmarkRoot(
                root,
                "EastWatch");
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Tower,
                "EastWatchSignalTower",
                eastWatch,
                new Vector3(-18.8f, -0.42f, 13.55f),
                -6f,
                new Vector3(0.96f, 1.08f, 0.96f),
                ruinStone,
                emberMaterial);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                "EastWatchFallenParapet",
                eastWatch,
                new Vector3(-14.1f, -0.49f, -11.9f),
                9f,
                new Vector3(0.94f, 0.58f, 0.94f),
                ruinStone,
                emberMaterial);
            AddLevelingBrazier(
                fortressSourceArt,
                "EastWatchBeacon",
                eastWatch,
                new Vector3(-17.1f, -0.1f, 10.8f),
                -10f,
                0.96f,
                5.25f,
                ruinStone,
                emberMaterial);

            // Staggered ruins beyond the lava shoulders make the districts read
            // as a larger broken fortress, while the center remains quiet for
            // combat. These pieces belong to the POI so chunk recipe stripping
            // removes them together with the unused district, not as seven
            // repeated skylines in every scene.
            AddLevelingDistrictDepth(
                refuge, fortressSourceArt, ruinStone, emberMaterial,
                -5f, 16.8f, 12f, -17.2f, 1.28f, 8f);
            AddLevelingDistrictDepth(
                cinderApproach, fortressSourceArt, ruinStone, emberMaterial,
                8f, -17.4f, -18f, 16.5f, 1.06f, -12f);
            AddLevelingDistrictDepth(
                gate, fortressSourceArt, ruinStone, emberMaterial,
                -9f, 17.1f, 10f, -16.7f, 1.5f, 10f);
            AddLevelingDistrictDepth(
                court, fortressSourceArt, ruinStone, emberMaterial,
                11f, 17.8f, -10f, -17.3f, 1.34f, -8f);
            AddLevelingDistrictDepth(
                ossuary, fortressSourceArt, ruinStone, emberMaterial,
                4f, -17.6f, -17f, 16.8f, 1.42f, 14f);
            AddLevelingDistrictDepth(
                reliquary, fortressSourceArt, ruinStone, emberMaterial,
                16f, 18.2f, -5f, -17.5f, 1.6f, -10f);
            AddLevelingDistrictDepth(
                eastWatch, fortressSourceArt, ruinStone, emberMaterial,
                -12f, 17.8f, 10f, -17.2f, 1.7f, 6f);
        }

        private static void AddLevelingDistrictDepth(
            Transform district,
            FortressSourceArtSet fortressSourceArt,
            Material stoneMaterial,
            Material emberMaterial,
            float highX,
            float highZ,
            float lowX,
            float lowZ,
            float highScale,
            float yaw)
        {
            Transform depth = CreateLevelingLandmarkRoot(
                district,
                "CinematicDepth");
            GameObject high = AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                "DistantButtress",
                depth,
                new Vector3(highX, -0.78f, highZ),
                yaw,
                new Vector3(1.18f, highScale, 1.12f),
                stoneMaterial,
                emberMaterial);
            GameObject low = AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallStraight,
                "BrokenApproachSilhouette",
                depth,
                new Vector3(lowX, -0.72f, lowZ),
                -yaw,
                new Vector3(1.02f, 0.58f, 1.08f),
                stoneMaterial,
                emberMaterial);

            // Far framing does not add to the realtime shadow map, point-light
            // count, transparent layers, or gameplay physics. The two retained
            // meshes add 2,664 triangles per district with the current source kit.
            high.GetComponentInChildren<MeshRenderer>(true).shadowCastingMode =
                ShadowCastingMode.Off;
            low.GetComponentInChildren<MeshRenderer>(true).shadowCastingMode =
                ShadowCastingMode.Off;
        }

        private static Transform CreateLevelingLandmarkRoot(
            Transform parent,
            string name)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.gameObject.layer = IgnoreRaycastLayer;
            return root;
        }

        private static GameObject AddLevelingFortressModule(
            FortressSourceArtSet fortressSourceArt,
            FortressSourceModelId modelId,
            string name,
            Transform parent,
            Vector3 position,
            float yaw,
            Vector3 scale,
            Material stoneMaterial,
            Material emberMaterial)
        {
            GameObject value = AddFortressModel(
                fortressSourceArt.Get(modelId),
                name,
                parent,
                position,
                yaw,
                stoneMaterial,
                emberMaterial);
            value.transform.localScale = scale;
            return value;
        }

        private static void BuildLevelingAtmosphere(
            Transform parent,
            Material particleMaterial)
        {
            Transform root = new GameObject(
                "AshenCausewayAtmosphere").transform;
            root.SetParent(parent, false);
            root.gameObject.layer = IgnoreRaycastLayer;

            GameObject ashObject = new GameObject(
                "AshenCausewayAshDrift",
                typeof(ParticleSystem));
            ashObject.transform.SetParent(root, false);
            ashObject.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            ashObject.layer = IgnoreRaycastLayer;

            ParticleSystem particles = ashObject.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.duration = 6f;
            main.maxParticles = 56;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(4.5f, 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.025f, 0.11f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startRotation = new ParticleSystem.MinMaxCurve(
                0f,
                Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.34f, 0.32f, 0.31f, 0.16f),
                new Color(0.58f, 0.51f, 0.44f, 0.32f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 8f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(54f, 0.7f, 18f);

            ParticleSystem.VelocityOverLifetimeModule velocity =
                particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.015f, 0.055f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.035f, 0.025f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.12f;
            noise.frequency = 0.18f;
            noise.scrollSpeed = 0.025f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(0.92f, 0.78f, 0.64f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.7f, 0.18f),
                    new GradientAlphaKey(0.45f, 0.72f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = gradient;

            ParticleSystem.CollisionModule collision = particles.collision;
            collision.enabled = false;

            ParticleSystemRenderer renderer =
                ashObject.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingFudge = -0.15f;
        }

        private static void AddLevelingBrazier(
            FortressSourceArtSet fortressSourceArt,
            string name,
            Transform parent,
            Vector3 position,
            float yaw,
            float scale,
            float phaseOffset,
            Material stoneMaterial,
            Material emberMaterial)
        {
            GameObject brazier = AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Brazier,
                name,
                parent,
                position,
                yaw,
                new Vector3(scale, scale * 1.12f, scale),
                stoneMaterial,
                emberMaterial);
            MeshRenderer renderer =
                brazier.GetComponentInChildren<MeshRenderer>(true);
            renderer.shadowCastingMode = ShadowCastingMode.Off;

            FortressFireAnchorPresenter presenter =
                brazier.AddComponent<FortressFireAnchorPresenter>();
            SerializedObject serialized = new SerializedObject(presenter);
            serialized.FindProperty("_surface").objectReferenceValue = renderer;
            serialized.FindProperty("_emberMaterialIndex").intValue = 1;
            serialized.FindProperty("_emissionMultiplier").floatValue = 2.15f;
            serialized.FindProperty("_phaseOffset").floatValue = phaseOffset;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            GameObject glowRoot = new GameObject(
                "WarmBounce",
                typeof(Light));
            glowRoot.transform.SetParent(brazier.transform, false);
            glowRoot.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            Light glow = glowRoot.GetComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1f, 0.33f, 0.075f, 1f);
            glow.intensity = 2.75f;
            glow.range = 7.2f;
            glow.shadows = LightShadows.None;
            glow.renderMode = LightRenderMode.Auto;
        }

        private static void ValidateLevelingLayoutDefinition()
        {
            if (LevelingSectionX.Length != LevelingSectionHalfWidth.Length ||
                LevelingSectionX.Length != LevelingSectionCenterZ.Length ||
                LevelingSectionX.Length < 2)
            {
                throw new InvalidOperationException(
                    "Ashen Causeway section positions and widths must match.");
            }

            if (Mathf.Approximately(
                    LevelingSectionX[0],
                    -LevelingHalfLength) == false ||
                Mathf.Approximately(
                    LevelingSectionX[LevelingSectionX.Length - 1],
                    LevelingHalfLength) == false)
            {
                throw new InvalidOperationException(
                    "Ashen Causeway must retain its authored 56 meter route length.");
            }

            int lastSection = LevelingSectionX.Length - 1;
            if (!Mathf.Approximately(
                    LevelingSectionHalfWidth[0],
                    LevelingSectionHalfWidth[lastSection]) ||
                !Mathf.Approximately(
                    LevelingSectionCenterZ[0],
                    LevelingSectionCenterZ[lastSection]))
            {
                throw new InvalidOperationException(
                    "Ashen Causeway endpoints must share one periodic profile for " +
                    "additive chunk seams.");
            }

            for (int index = 0; index < LevelingSectionX.Length; index++)
            {
                float southEdge = LevelingSectionCenterZ[index] -
                    LevelingSectionHalfWidth[index];
                float northEdge = LevelingSectionCenterZ[index] +
                    LevelingSectionHalfWidth[index];
                if (LevelingSectionHalfWidth[index] <= 0f ||
                    southEdge < -LevelingOuterHalfWidth ||
                    northEdge > LevelingOuterHalfWidth)
                {
                    throw new InvalidOperationException(
                        "Ashen Causeway section " + index +
                        " exceeds the authored 22 meter route width.");
                }

                if (index > 0 &&
                    LevelingSectionX[index] <= LevelingSectionX[index - 1])
                {
                    throw new InvalidOperationException(
                        "Ashen Causeway sections must progress west to east.");
                }
            }
        }

        internal static void EnforceLevelingStaticTriangleBudget(GameObject root)
        {
            long triangleCount = 0L;
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            for (int filterIndex = 0;
                 filterIndex < filters.Length;
                 filterIndex++)
            {
                Mesh mesh = filters[filterIndex].sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    if (mesh.GetTopology(submesh) != MeshTopology.Triangles)
                    {
                        continue;
                    }

                    triangleCount += (long)mesh.GetIndexCount(submesh) / 3L;
                }
            }

            if (triangleCount <= LevelingStaticTriangleBudget)
            {
                return;
            }

            Object.DestroyImmediate(root);
            throw new InvalidOperationException(
                "Ashen Causeway authored environment contains " + triangleCount +
                " static triangles; maximum is " +
                LevelingStaticTriangleBudget + ".");
        }

        private static void BuildArenaArchitecturalDepth(
            Transform parent,
            Material masonry,
            Material recess,
            Material ember)
        {
            if (masonry == null)
            {
                throw new ArgumentNullException(nameof(masonry));
            }

            if (recess == null)
            {
                throw new ArgumentNullException(nameof(recess));
            }

            if (ember == null)
            {
                throw new ArgumentNullException(nameof(ember));
            }

            Transform depthRoot = new GameObject("ArenaArchitecturalDepth").transform;
            depthRoot.SetParent(parent, false);
            depthRoot.gameObject.layer = IgnoreRaycastLayer;

            Mesh stonework = EnsureMesh(
                EnvironmentMeshFolder + "/ArenaBoundaryStonework.asset",
                CreateArenaBoundaryStoneworkMesh());
            GameObject boundary = CreateMeshVisual(
                "BoundaryStonework",
                depthRoot,
                Vector3.zero,
                stonework,
                new[] { masonry, recess, ember },
                true,
                true);
            boundary.GetComponent<MeshRenderer>().reflectionProbeUsage =
                ReflectionProbeUsage.Simple;

            Mesh outerCrust = EnsureMesh(
                EnvironmentMeshFolder + "/ArenaOuterBasaltCrust.asset",
                CreateArenaOuterBasaltCrustMesh());
            GameObject crust = CreateMeshVisual(
                "OuterBasaltCrust",
                depthRoot,
                Vector3.zero,
                outerCrust,
                new[] { masonry, masonry },
                false,
                true);
            crust.GetComponent<MeshRenderer>().reflectionProbeUsage =
                ReflectionProbeUsage.Simple;

            Mesh flankSilhouettes = EnsureMesh(
                EnvironmentMeshFolder + "/ArenaFlankSilhouettes.asset",
                CreateArenaFlankSilhouetteMesh());
            GameObject flanks = CreateMeshVisual(
                "FlankSilhouettes",
                depthRoot,
                Vector3.zero,
                flankSilhouettes,
                new[] { masonry, recess, ember },
                false,
                true);
            flanks.GetComponent<MeshRenderer>().reflectionProbeUsage =
                ReflectionProbeUsage.Simple;
        }

        private static void BuildArenaCliffBackdrop(
            Transform parent,
            Material masonry)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            if (masonry == null)
            {
                throw new ArgumentNullException(nameof(masonry));
            }

            Mesh cliff = EnsureMesh(
                EnvironmentMeshFolder + "/ArenaCliffBackdrop.asset",
                CreateArenaCliffBackdropMesh());
            GameObject backdrop = CreateMeshVisual(
                "ArenaCliffBackdrop",
                parent,
                Vector3.zero,
                cliff,
                new[] { masonry },
                false,
                true);
            MeshRenderer renderer = backdrop.GetComponent<MeshRenderer>();
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
        }

        private static Mesh CreateArenaCliffBackdropMesh()
        {
            const int segmentCount = 48;
            const float bottomY = -1.62f;
            const float crownDepth = 0.72f;
            const float uvMetersPerTile = 10.5f;
            var vertices = new List<Vector3>(segmentCount * 8);
            var normals = new List<Vector3>(segmentCount * 8);
            var uvs = new List<Vector2>(segmentCount * 8);
            var triangles = new[] { new List<int>(segmentCount * 12) };

            for (int segment = 0; segment < segmentCount; segment++)
            {
                float startAngle = segment / (float)segmentCount * Mathf.PI * 2f;
                float endAngle = (segment + 1) /
                    (float)segmentCount * Mathf.PI * 2f;
                float startRadius = GetArenaCliffRadius(segment);
                float endRadius = GetArenaCliffRadius(segment + 1);
                float startHeight = GetArenaCliffHeight(segment, segmentCount);
                float endHeight = GetArenaCliffHeight(segment + 1, segmentCount);
                Vector2 start = Polar(startRadius, startAngle);
                Vector2 end = Polar(endRadius, endAngle);
                Vector2 outerStart = Polar(startRadius + crownDepth, startAngle);
                Vector2 outerEnd = Polar(endRadius + crownDepth, endAngle);
                Vector3 inwardNormal = -new Vector3(
                    (start.x + end.x) * 0.5f,
                    0f,
                    (start.y + end.y) * 0.5f).normalized;
                float uStart = startAngle * ArenaCliffBackdropRadius /
                    uvMetersPerTile;
                float uEnd = endAngle * ArenaCliffBackdropRadius /
                    uvMetersPerTile;
                int faceStart = vertices.Count;
                vertices.Add(new Vector3(start.x, startHeight, start.y));
                vertices.Add(new Vector3(end.x, endHeight, end.y));
                vertices.Add(new Vector3(end.x, bottomY, end.y));
                vertices.Add(new Vector3(start.x, bottomY, start.y));
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    normals.Add(inwardNormal);
                }

                uvs.Add(new Vector2(uStart, (startHeight - bottomY) /
                    uvMetersPerTile));
                uvs.Add(new Vector2(uEnd, (endHeight - bottomY) /
                    uvMetersPerTile));
                uvs.Add(new Vector2(uEnd, 0f));
                uvs.Add(new Vector2(uStart, 0f));
                triangles[0].Add(faceStart);
                triangles[0].Add(faceStart + 2);
                triangles[0].Add(faceStart + 1);
                triangles[0].Add(faceStart);
                triangles[0].Add(faceStart + 3);
                triangles[0].Add(faceStart + 2);

                int crownStart = vertices.Count;
                Vector3 crownNormal = new Vector3(
                    -inwardNormal.x * 0.18f,
                    1f,
                    -inwardNormal.z * 0.18f).normalized;
                vertices.Add(new Vector3(start.x, startHeight, start.y));
                vertices.Add(new Vector3(outerStart.x, startHeight - 0.12f, outerStart.y));
                vertices.Add(new Vector3(outerEnd.x, endHeight - 0.12f, outerEnd.y));
                vertices.Add(new Vector3(end.x, endHeight, end.y));
                for (int vertex = 0; vertex < 4; vertex++)
                {
                    normals.Add(crownNormal);
                }

                uvs.Add(new Vector2(uStart, 0f));
                uvs.Add(new Vector2(uStart, crownDepth / uvMetersPerTile));
                uvs.Add(new Vector2(uEnd, crownDepth / uvMetersPerTile));
                uvs.Add(new Vector2(uEnd, 0f));
                triangles[0].Add(crownStart);
                triangles[0].Add(crownStart + 2);
                triangles[0].Add(crownStart + 1);
                triangles[0].Add(crownStart);
                triangles[0].Add(crownStart + 3);
                triangles[0].Add(crownStart + 2);
            }

            Mesh mesh = CreateMesh(
                "ArenaCliffBackdrop",
                vertices,
                normals,
                uvs,
                triangles);
            mesh.RecalculateTangents();
            return mesh;
        }

        private static float GetArenaCliffRadius(int node)
        {
            int pattern = (node * 7) % 5;
            return ArenaCliffBackdropRadius + ((pattern - 2) * 0.12f);
        }

        private static float GetArenaCliffHeight(int node, int segmentCount)
        {
            float angle = node / (float)segmentCount * Mathf.PI * 2f;
            float farSide = Mathf.Clamp01((Mathf.Cos(angle) + 0.3f) / 1.3f);
            float brokenCrown = (((node * 11) % 7) - 3f) * 0.18f;
            return 10.2f + (farSide * 1.6f) + brokenCrown;
        }

        public static GameObject BuildBoss(
            FortressSourceArtSet fortressSourceArt,
            GameObject arenaFloorSourceArt,
            Material stone,
            Material arenaFloorStone,
            Material arenaFloorTrim,
            Material arenaFloorGrout,
            Material ruinStone,
            Material lava,
            Material lavaSeam,
            Material abyss,
            Material blueAccent,
            Material redAccent,
            Material particleMaterial)
        {
            if (fortressSourceArt == null)
            {
                throw new ArgumentNullException(nameof(fortressSourceArt));
            }

            if (arenaFloorSourceArt == null)
            {
                throw new ArgumentNullException(nameof(arenaFloorSourceArt));
            }

            GameObject root = new GameObject("BossEnvironment");
            BuildBackdrop(root.transform, abyss, true);
            BuildLava(
                root.transform,
                BossVisualLavaRadius,
                lava,
                lavaSeam,
                true,
                29f);
            CreatePrimitive(
                "RaidFloor",
                PrimitiveType.Cylinder,
                root.transform,
                new Vector3(0f, -0.3f, 0f),
                new Vector3(26f, 0.3f, 26f),
                stone,
                true,
                GroundLayer,
                false);

            GameObject bossFloor = AddArenaFloorModel(
                arenaFloorSourceArt,
                root.transform,
                "RaidFloorVisual",
                arenaFloorStone,
                arenaFloorTrim,
                arenaFloorGrout);
            bossFloor.transform.localScale = Vector3.one *
                (BossSafeRadius / ArenaLayout.SafeRadius);

            BuildBossFloorInlays(
                root.transform,
                redAccent,
                blueAccent);
            BuildPerimeterComposition(
                root.transform,
                BossFortressRadius,
                true,
                fortressSourceArt,
                ruinStone,
                lavaSeam,
                particleMaterial);
            BuildArenaAtmosphere(
                root.transform,
                particleMaterial,
                true);
            return root;
        }

        /// <summary>
        /// Builds the presentation-only Hero Siege stronghold. Siege combat is
        /// still planar and authoritative in Core; this cross-shaped dressing
        /// exists solely to make the objective and its four approach vectors
        /// legible at the strategy camera. Keep it separate from the Boss ring
        /// and the Dungeon/Arena content so a content switch cannot leak props.
        /// </summary>
        public static GameObject BuildHeroSiege(
            FortressSourceArtSet fortressSourceArt,
            GameObject arenaFloorSourceArt,
            Material stone,
            Material arenaFloorStone,
            Material arenaFloorTrim,
            Material arenaFloorGrout,
            Material ruinStone,
            Material lava,
            Material lavaSeam,
            Material abyss,
            Material blueAccent,
            Material redAccent,
            Material floorAccent,
            Material particleMaterial)
        {
            if (fortressSourceArt == null)
            {
                throw new ArgumentNullException(nameof(fortressSourceArt));
            }

            if (arenaFloorSourceArt == null)
            {
                throw new ArgumentNullException(nameof(arenaFloorSourceArt));
            }

            Material[] requiredMaterials =
            {
                stone,
                arenaFloorStone,
                arenaFloorTrim,
                arenaFloorGrout,
                ruinStone,
                lava,
                lavaSeam,
                abyss,
                blueAccent,
                redAccent,
                floorAccent,
                particleMaterial,
            };
            for (int index = 0; index < requiredMaterials.Length; index++)
            {
                if (requiredMaterials[index] == null)
                {
                    throw new ArgumentNullException(
                        nameof(requiredMaterials),
                        "Hero Siege authoring requires every shared environment material.");
                }
            }

            GameObject root = new GameObject("HeroSiegeEnvironment");
            BuildBackdrop(root.transform, abyss, false);
            BuildLava(
                root.transform,
                ArenaLayout.OuterKillRadius + ArenaVisualLavaMargin,
                lava,
                lavaSeam,
                false,
                10f);
            CreatePrimitive(
                "SafeStone",
                PrimitiveType.Cylinder,
                root.transform,
                new Vector3(0f, -0.3f, 0f),
                new Vector3(
                    ArenaLayout.SafeRadius * 2f,
                    0.3f,
                    ArenaLayout.SafeRadius * 2f),
                stone,
                true,
                GroundLayer,
                false);

            // The floor source is the same authored, flat stone language used by
            // Arena, but the Siege objective gets its own central presentation
            // root and never mutates the Arena/Boss prefabs.
            Transform floor = new GameObject("SiegeFloor").transform;
            floor.SetParent(root.transform, false);
            AddArenaFloorModel(
                arenaFloorSourceArt,
                floor,
                "SiegeCentralFloor",
                arenaFloorStone,
                arenaFloorTrim,
                arenaFloorGrout);
            floor.localScale = Vector3.one * 0.5f;

            BuildHeroSiegeCore(
                root.transform,
                fortressSourceArt,
                ruinStone,
                lavaSeam,
                floorAccent);
            BuildHeroSiegeLane(
                root.transform,
                fortressSourceArt,
                "NorthLaneApproach",
                Vector3.forward,
                blueAccent,
                ruinStone,
                floorAccent,
                0);
            BuildHeroSiegeLane(
                root.transform,
                fortressSourceArt,
                "EastLaneApproach",
                Vector3.right,
                redAccent,
                ruinStone,
                floorAccent,
                1);
            BuildHeroSiegeLane(
                root.transform,
                fortressSourceArt,
                "SouthLaneApproach",
                Vector3.back,
                blueAccent,
                ruinStone,
                floorAccent,
                2);
            BuildHeroSiegeLane(
                root.transform,
                fortressSourceArt,
                "WestLaneApproach",
                Vector3.left,
                redAccent,
                ruinStone,
                floorAccent,
                3);

            BuildArenaAtmosphere(root.transform, particleMaterial, false);
            Transform atmosphere = root.transform.Find("ArenaAtmosphere");
            if (atmosphere != null)
            {
                atmosphere.name = "HeroSiegeAtmosphere";
            }
            return root;
        }

        private static void BuildHeroSiegeCore(
            Transform parent,
            FortressSourceArtSet fortressSourceArt,
            Material stoneMaterial,
            Material emberMaterial,
            Material accentMaterial)
        {
            Transform core = new GameObject("SiegeCentralFortressCore").transform;
            core.SetParent(parent, false);
            core.gameObject.layer = IgnoreRaycastLayer;

            GameObject foundation = AddFortressModel(
                fortressSourceArt.Get(FortressSourceModelId.FoundationRing),
                "SiegeCoreFoundation",
                core,
                new Vector3(0f, -0.2f, 0f),
                0f,
                stoneMaterial,
                emberMaterial);
            foundation.transform.localScale = Vector3.one * 0.34f;

            GameObject keep = AddFortressModel(
                fortressSourceArt.Get(FortressSourceModelId.Tower),
                "SiegeCoreKeep",
                core,
                new Vector3(0f, -0.66f, 0f),
                0f,
                stoneMaterial,
                emberMaterial);
            keep.transform.localScale = new Vector3(0.5f, 0.58f, 0.5f);

            GameObject wardstone = AddFortressModel(
                fortressSourceArt.Get(FortressSourceModelId.Wardstone),
                "SiegeCoreWardstone",
                core,
                new Vector3(0f, -0.06f, 0f),
                0f,
                stoneMaterial,
                emberMaterial);
            wardstone.transform.localScale = new Vector3(1.45f, 1.75f, 1.45f);
            BuildRuneAt(
                core,
                new Vector3(0f, 0.055f, 0f),
                4.45f,
                accentMaterial,
                0.075f,
                "SiegeCoreRune");
        }

        private static void BuildHeroSiegeLane(
            Transform parent,
            FortressSourceArtSet fortressSourceArt,
            string laneName,
            Vector3 direction,
            Material laneAccent,
            Material stoneMaterial,
            Material floorAccent,
            int seed)
        {
            Transform lane = new GameObject(laneName).transform;
            lane.SetParent(parent, false);
            lane.gameObject.layer = IgnoreRaycastLayer;
            Quaternion heading = Quaternion.LookRotation(direction, Vector3.up);
            lane.localRotation = heading;

            // Four repeated slabs make the cardinal read survive a zoomed-out
            // strategy view while retaining the same flat, no-collider rule as
            // the existing presentation-only environment meshes.
            for (int segment = 0; segment < 4; segment++)
            {
                float distance = 5.1f + (segment * 4.05f);
                CreatePrimitive(
                    laneName + "_Stone_" + (segment + 1),
                    PrimitiveType.Cube,
                    lane,
                    new Vector3(0f, -0.065f, distance),
                    new Vector3(4.35f, 0.13f, 3.75f),
                    stoneMaterial,
                    false,
                    IgnoreRaycastLayer);
                CreatePrimitive(
                    laneName + "_CenterMark_" + (segment + 1),
                    PrimitiveType.Cube,
                    lane,
                    new Vector3(0f, 0.012f, distance),
                    new Vector3(0.26f, 0.018f, 2.55f),
                    floorAccent,
                    false,
                    IgnoreRaycastLayer);
            }

            // An entry tower and a broken shoulder frame each approach without
            // introducing a gameplay obstacle or altering Core's arena map.
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.Tower,
                laneName + "_EntryTower",
                lane,
                new Vector3(-3.1f, -0.55f, 17.3f),
                seed * 17f,
                new Vector3(0.55f, 0.72f, 0.55f),
                stoneMaterial,
                laneAccent);
            AddLevelingFortressModule(
                fortressSourceArt,
                FortressSourceModelId.WallBroken,
                laneName + "_EntryShoulder",
                lane,
                new Vector3(3.05f, -0.52f, 16.1f),
                90f + (seed * 11f),
                new Vector3(0.62f, 0.5f, 0.62f),
                stoneMaterial,
                laneAccent);
            AddLevelingBrazier(
                fortressSourceArt,
                laneName + "_Beacon",
                lane,
                new Vector3(0f, -0.1f, 18.85f),
                180f,
                0.62f,
                0.8f + (seed * 1.6f),
                stoneMaterial,
                laneAccent);

            // The accent ring is deliberately at the lane mouth, not under the
            // core, so players can read which of the four approaches is active.
            BuildRuneAt(
                lane,
                new Vector3(0f, 0.04f, 1.55f),
                1.28f,
                laneAccent,
                0.055f,
                laneName + "_MouthRune");
            BuildHeroSiegeLaneSideRail(
                lane,
                laneName + "_LeftRail",
                -1,
                stoneMaterial,
                laneAccent);
            BuildHeroSiegeLaneSideRail(
                lane,
                laneName + "_RightRail",
                1,
                stoneMaterial,
                laneAccent);
        }

        private static void BuildHeroSiegeLaneSideRail(
            Transform lane,
            string name,
            int side,
            Material stoneMaterial,
            Material accentMaterial)
        {
            CreatePrimitive(
                name,
                PrimitiveType.Cube,
                lane,
                new Vector3(side * 2.1f, 0.28f, 10.15f),
                new Vector3(0.18f, 0.56f, 13.4f),
                stoneMaterial,
                false,
                IgnoreRaycastLayer);
            CreatePrimitive(
                name + "Accent",
                PrimitiveType.Cube,
                lane,
                new Vector3(side * 2.08f, 0.59f, 10.15f),
                new Vector3(0.055f, 0.025f, 12.3f),
                accentMaterial,
                false,
                IgnoreRaycastLayer);
        }

        private static void BuildBossFloorInlays(
            Transform parent,
            Material bossAccent,
            Material recoveryAccent)
        {
            Transform dais = new GameObject("BossDais").transform;
            dais.SetParent(parent, false);
            dais.localPosition = new Vector3(0f, 0f, 3.5f);
            dais.gameObject.layer = IgnoreRaycastLayer;
            BuildRuneAt(
                dais,
                new Vector3(0f, 0.055f, 0f),
                3.2f,
                bossAccent,
                0.07f,
                "BossDaisInlay");

            Transform recovery = new GameObject("RecoveryLane").transform;
            recovery.SetParent(parent, false);
            recovery.localPosition = new Vector3(0f, 0f, -9f);
            recovery.gameObject.layer = IgnoreRaycastLayer;
            BuildRuneAt(
                recovery,
                new Vector3(0f, 0.055f, 0f),
                1.8f,
                recoveryAccent,
                0.055f,
                "RecoveryInlay");
        }

        private static void BuildArenaAtmosphere(
            Transform parent,
            Material particleMaterial,
            bool isBoss)
        {
            if (particleMaterial == null)
            {
                throw new ArgumentNullException(nameof(particleMaterial));
            }

            Transform atmosphere = new GameObject(
                isBoss ? "BossAtmosphere" : "ArenaAtmosphere").transform;
            atmosphere.SetParent(parent, false);
            atmosphere.gameObject.layer = IgnoreRaycastLayer;

            GameObject drift = new GameObject(
                isBoss ? "RiftCinderDrift" : "PerimeterEmberDrift",
                typeof(ParticleSystem));
            drift.transform.SetParent(atmosphere, false);
            drift.transform.localPosition = new Vector3(0f, 0.14f, 0f);
            ParticleSystem particles = drift.GetComponent<ParticleSystem>();
            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 7f;
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(
                isBoss ? 3.6f : 4.2f,
                isBoss ? 6.2f : 7f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.02f, 0.08f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.025f, 0.075f);
            main.startRotation = new ParticleSystem.MinMaxCurve(
                0f,
                Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.5f, 0.32f, 0.22f, 0.18f),
                new Color(1f, 0.34f, 0.055f, isBoss ? 0.62f : 0.48f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = isBoss ? 8f : 6f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = isBoss ? 18.5f : 20f;
            shape.radiusThickness = 0.18f;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.09f, 0.09f);
            velocity.y = new ParticleSystem.MinMaxCurve(
                isBoss ? 0.14f : 0.1f,
                isBoss ? 0.38f : 0.28f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.07f, 0.07f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = isBoss ? 0.13f : 0.09f;
            noise.frequency = 0.42f;
            noise.scrollSpeed = 0.18f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.58f, 0.24f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.58f, 0.16f),
                    new GradientAlphaKey(0.36f, 0.72f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = fade;

            ParticleSystemRenderer renderer =
                drift.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 0.075f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingFudge = -0.2f;

            BuildCinderVeil(atmosphere, particleMaterial, isBoss);
        }

        private static void BuildCinderVeil(
            Transform parent,
            Material particleMaterial,
            bool isBoss)
        {
            GameObject veil = new GameObject(
                isBoss ? "RiftCinderVeil" : "ArenaCinderVeil",
                typeof(ParticleSystem));
            veil.transform.SetParent(parent, false);
            veil.transform.localPosition = new Vector3(0f, 0.72f, 0f);
            veil.layer = IgnoreRaycastLayer;

            ParticleSystem particles = veil.GetComponent<ParticleSystem>();
            particles.useAutoRandomSeed = false;
            particles.randomSeed = isBoss ? 0xC11D3u : 0xA83E1u;

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 5.5f;
            main.maxParticles = 24;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(2.8f, 5.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.025f, 0.09f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.028f, 0.068f);
            main.startRotation = new ParticleSystem.MinMaxCurve(
                0f,
                Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.2f, 0.018f, 0.42f),
                new Color(1f, 0.78f, 0.26f, isBoss ? 0.9f : 0.76f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = isBoss ? 5.2f : 4.4f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = isBoss ? 18.2f : 20.25f;
            shape.radiusThickness = 0.62f;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.26f, 0.62f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.13f, 0.13f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = isBoss ? 0.2f : 0.16f;
            noise.frequency = 0.7f;
            noise.scrollSpeed = 0.24f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.72f), 0f),
                    new GradientColorKey(new Color(1f, 0.34f, 0.045f), 0.68f),
                    new GradientColorKey(new Color(0.7f, 0.09f, 0.008f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(0.9f, 0.14f),
                    new GradientAlphaKey(0.54f, 0.76f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = fade;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.45f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(1f, 0.28f)));

            ParticleSystemRenderer renderer =
                veil.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 0.055f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingFudge = 0.08f;
        }

        private static void BuildLava(
            Transform parent,
            float visualRadius,
            Material material,
            Material seamMaterial,
            bool isBoss,
            float phaseOffset)
        {
            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            if (seamMaterial == null)
            {
                throw new ArgumentNullException(nameof(seamMaterial));
            }

            GameObject lava = CreatePrimitive(
                "Lava",
                PrimitiveType.Cylinder,
                parent,
                new Vector3(0f, -0.85f, 0f),
                new Vector3(visualRadius * 2f, 0.12f, visualRadius * 2f),
                material,
                true,
                GroundLayer);
            Renderer lavaRenderer = lava.GetComponent<Renderer>();
            lavaRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lavaRenderer.receiveShadows = false;

            float innerFissureRadius = isBoss
                ? BossSafeRadius + 0.58f
                : ArenaLayout.SafeRadius + 0.62f;
            float outerFissureRadius = Mathf.Min(
                visualRadius - 0.8f,
                isBoss ? BossFortressRadius + 0.72f : ArenaFortressRadius + 0.72f);
            string fissureName = isBoss
                ? "BossLavaFissures"
                : "ArenaLavaFissures";
            Mesh fissureMesh = EnsureMesh(
                EnvironmentMeshFolder + "/" + fissureName + ".asset",
                CreateLavaFissureMesh(
                    fissureName,
                    innerFissureRadius,
                    outerFissureRadius,
                    isBoss ? 16 : 18,
                    isBoss ? 71 : 29));
            GameObject fissures = CreateMeshVisual(
                fissureName,
                parent,
                new Vector3(0f, -0.716f, 0f),
                fissureMesh,
                new[] { seamMaterial },
                false,
                false);
            Renderer fissureRenderer = fissures.GetComponent<Renderer>();
            fissureRenderer.shadowCastingMode = ShadowCastingMode.Off;
            fissureRenderer.receiveShadows = false;
            fissureRenderer.lightProbeUsage = LightProbeUsage.Off;
            fissureRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            HazardSurfaceAnimator animator = lava.AddComponent<HazardSurfaceAnimator>();
            SerializedObject serialized = new SerializedObject(animator);
            serialized.FindProperty("_surface").objectReferenceValue = lavaRenderer;
            serialized.FindProperty("_seams").objectReferenceValue = fissureRenderer;
            serialized.FindProperty("_lowColor").colorValue =
                new Color(0.82f, 0.42f, 0.2f, 1f);
            serialized.FindProperty("_highColor").colorValue =
                new Color(1f, 0.58f, 0.26f, 1f);
            serialized.FindProperty("_lowSurfaceEmissionColor").colorValue =
                new Color(0.55f, 0.32f, 0.12f, 1f);
            serialized.FindProperty("_highSurfaceEmissionColor").colorValue =
                new Color(2.8f, 1.3f, 0.38f, 1f);
            serialized.FindProperty("_lowSeamColor").colorValue =
                new Color(0.003f, 0f, 0f, 1f);
            serialized.FindProperty("_highSeamColor").colorValue =
                new Color(0.012f, 0.001f, 0f, 1f);
            serialized.FindProperty("_pulseSpeed").floatValue = 0.22f;
            serialized.FindProperty("_surfaceTiling").floatValue =
                AuthoringAssets.ArenaLavaTiling;
            serialized.FindProperty("_phaseOffset").floatValue = phaseOffset;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Mesh CreateLavaFissureMesh(
            string name,
            float innerRadius,
            float outerRadius,
            int fissureCount,
            int patternSeed)
        {
            if (innerRadius <= 0f || outerRadius <= innerRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(outerRadius),
                    "Lava fissures require a positive annulus.");
            }

            var vertices = new List<Vector3>(fissureCount * 20);
            var normals = new List<Vector3>(fissureCount * 20);
            var uvs = new List<Vector2>(fissureCount * 20);
            var indices = new List<int>(fissureCount * 30);
            float span = outerRadius - innerRadius;
            for (int fissure = 0; fissure < fissureCount; fissure++)
            {
                float angle = ((fissure * 137.507f) + (patternSeed * 11f)) *
                    Mathf.Deg2Rad;
                float startRadius = innerRadius + ((fissure % 4) * 0.17f);
                float endRadius = outerRadius - ((fissure % 5) * 0.31f);
                float firstBend = ((((fissure * 7) + patternSeed) % 9) - 4f) *
                    0.042f;
                float secondBend = ((((fissure * 11) + patternSeed) % 11) - 5f) *
                    0.036f;
                Vector2 start = Polar(startRadius, angle);
                Vector2 first = Polar(
                    startRadius + (span * 0.34f),
                    angle + firstBend);
                Vector2 second = Polar(
                    startRadius + (span * 0.7f),
                    angle + secondBend);
                Vector2 end = Polar(
                    endRadius,
                    angle + ((firstBend + secondBend) * 0.55f));
                float width = 0.012f + ((fissure % 4) * 0.002f);

                AddFlatRibbon(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    start,
                    first,
                    width,
                    0f);
                AddFlatRibbon(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    first,
                    second,
                    width * 0.84f,
                    0f);
                AddFlatRibbon(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    second,
                    end,
                    width * 0.58f,
                    0f);

                float branchDirection = fissure % 2 == 0 ? 1f : -1f;
                float branchRadius = startRadius + (span * 0.83f);
                Vector2 branchEnd = Polar(
                    branchRadius,
                    angle + secondBend + (branchDirection *
                        (0.11f + ((fissure % 3) * 0.035f))));
                AddFlatRibbon(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    second,
                    branchEnd,
                    width * 0.42f,
                    0f);
            }

            Mesh mesh = CreateMesh(
                name,
                vertices,
                normals,
                uvs,
                new[] { indices });
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void BuildBackdrop(
            Transform parent,
            Material abyss,
            bool isBoss)
        {
            GameObject backdrop = CreatePrimitive(
                isBoss ? "RaidAbyssBackdrop" : "ArenaAbyssBackdrop",
                PrimitiveType.Cylinder,
                parent,
                new Vector3(0f, -1.55f, 0f),
                new Vector3(BackdropRadius * 2f, 0.08f, BackdropRadius * 2f),
                abyss,
                false,
                IgnoreRaycastLayer);
            Renderer backdropRenderer = backdrop.GetComponent<Renderer>();
            backdropRenderer.shadowCastingMode = ShadowCastingMode.Off;
            backdropRenderer.receiveShadows = false;
            backdropRenderer.lightProbeUsage = LightProbeUsage.Off;
            backdropRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void BuildRadialFloor(
            Transform parent,
            string objectName,
            float radius,
            Material stone,
            Material stoneVariation,
            Material darkStone,
            Material basalt,
            string floorMeshName,
            string etchingMeshName,
            int patternSeed)
        {
            CreatePrimitive(
                objectName + "_Underlay",
                PrimitiveType.Cylinder,
                parent,
                new Vector3(0f, -0.026f, 0f),
                new Vector3(radius * 2.025f, 0.026f, radius * 2.025f),
                basalt,
                false,
                IgnoreRaycastLayer);

            Mesh floor = EnsureMesh(
                EnvironmentMeshFolder + "/" + floorMeshName,
                CreateRadialFloorMesh(
                    System.IO.Path.GetFileNameWithoutExtension(floorMeshName),
                    radius,
                    patternSeed));
            CreateMeshVisual(
                objectName,
                parent,
                Vector3.zero,
                floor,
                new[] { stone, stoneVariation, darkStone, basalt },
                true,
                true);

            Mesh etching = EnsureMesh(
                EnvironmentMeshFolder + "/" + etchingMeshName,
                CreateFloorEtchingMesh(
                    System.IO.Path.GetFileNameWithoutExtension(etchingMeshName),
                    radius,
                    patternSeed));
            CreateMeshVisual(
                objectName + "_Etching",
                parent,
                Vector3.zero,
                etching,
                new[] { darkStone },
                false,
                false);
        }

        private static void BuildTeamThreshold(
            Transform parent,
            float x,
            Material material,
            string name)
        {
            Transform root = new GameObject(name).transform;
            root.SetParent(parent, false);
            for (int index = -1; index <= 1; index++)
            {
                CreatePrimitive(
                    name + "_Mark_" + (index + 2),
                    PrimitiveType.Cube,
                    root,
                    new Vector3(x, 0.044f, index * 1.15f),
                    new Vector3(0.48f, 0.012f, 0.055f),
                    material,
                    false,
                    IgnoreRaycastLayer);
            }
        }

        private static void BuildPerimeterComposition(
            Transform parent,
            float radius,
            bool isBoss,
            FortressSourceArtSet fortressSourceArt,
            Material structureMaterial,
            Material emberMaterial,
            Material particleMaterial)
        {
            Transform composition = new GameObject(
                isBoss ? "RaidPerimeterDressing" : "ArenaPerimeterDressing").transform;
            composition.SetParent(parent, false);

            if (fortressSourceArt == null)
            {
                throw new ArgumentNullException(nameof(fortressSourceArt));
            }

            if (isBoss == false)
            {
                BuildArenaFortressRing(
                    composition,
                    radius,
                    fortressSourceArt,
                    structureMaterial,
                    emberMaterial,
                    particleMaterial);
                return;
            }

            BuildBossFortressRing(
                composition,
                radius,
                fortressSourceArt,
                structureMaterial,
                emberMaterial,
                particleMaterial);
        }

        private static void BuildBossFortressRing(
            Transform parent,
            float radius,
            FortressSourceArtSet fortressSourceArt,
            Material structureMaterial,
            Material emberMaterial,
            Material particleMaterial)
        {
            GameObject foundation = AddFortressModel(
                fortressSourceArt.Get(FortressSourceModelId.FoundationRing),
                "FortressFoundationRing",
                parent,
                Vector3.zero,
                0f,
                structureMaterial,
                emberMaterial);
            foundation.transform.localScale = Vector3.one *
                (radius / ArenaFortressRadius);

            const int bayCount = 18;
            int wallIndex = 0;
            for (int index = 0; index < bayCount; index++)
            {
                float angle = index / (float)bayCount * 360f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                    new Vector3(0f, 0f, radius);
                Transform bay = new GameObject(
                    "FortressBay_" + (index + 1).ToString("00")).transform;
                bay.SetParent(parent, false);
                bay.localPosition = position;
                bay.localRotation = Quaternion.Euler(0f, angle, 0f);
                bay.gameObject.layer = IgnoreRaycastLayer;

                bool isTower = IsBossFarSideTowerBay(index);
                FortressSourceModelId sourceId;
                if (isTower)
                {
                    sourceId = FortressSourceModelId.Tower;
                }
                else
                {
                    sourceId = wallIndex % 2 == 0
                        ? FortressSourceModelId.WallStraight
                        : FortressSourceModelId.WallBroken;
                    wallIndex++;
                }

                GameObject module = AddFortressModel(
                    fortressSourceArt.Get(sourceId),
                    isTower
                        ? "FortressTower"
                        : sourceId == FortressSourceModelId.WallStraight
                            ? "FortressWallStraight"
                            : "FortressWallBroken",
                    bay,
                    new Vector3(
                        0f,
                        GetBossFortressModuleSink(index, isTower),
                        0f),
                    0f,
                    structureMaterial,
                    emberMaterial);
                module.transform.localScale = new Vector3(
                    BossFortressPlanarScale,
                    GetBossFortressModuleVerticalScale(index, isTower),
                    BossFortressPlanarScale);
                ConfigureFortressSurfaceVariation(
                    module,
                    index + 37,
                    isTower);
                if (isTower)
                {
                    ConfigureTowerEmberPulse(module, index + 37);
                }
            }

            BuildBossFireAnchors(
                parent,
                fortressSourceArt,
                structureMaterial,
                emberMaterial,
                particleMaterial);
        }

        private static bool IsBossFarSideTowerBay(int index)
        {
            switch (index)
            {
                case 1:
                case 2:
                case 3:
                case 15:
                case 16:
                case 17:
                    return true;

                default:
                    return false;
            }
        }

        private static float GetBossFortressModuleSink(
            int bayIndex,
            bool isTower)
        {
            if (isTower)
            {
                return bayIndex == 1 || bayIndex == 17
                    ? ShowcaseTowerSink
                    : FortressTowerSink;
            }

            return FortressWallBaseSink + (((bayIndex * 5) % 3) *
                FortressWallSinkStep);
        }

        private static float GetBossFortressModuleVerticalScale(
            int bayIndex,
            bool isTower)
        {
            if (isTower)
            {
                int distanceFromFarCenter = Mathf.Min(bayIndex, 18 - bayIndex);
                switch (distanceFromFarCenter)
                {
                    case 1:
                        return 0.78f;

                    case 2:
                        return 0.68f;

                    case 3:
                        return 0.58f;

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(bayIndex),
                            bayIndex,
                            "Only the six far-side bays may contain boss towers.");
                }
            }

            int distance = Mathf.Abs(bayIndex - 9);
            return Mathf.Lerp(0.38f, 0.92f, distance / 9f);
        }

        private static void BuildBossFireAnchors(
            Transform parent,
            FortressSourceArtSet fortressSourceArt,
            Material structureMaterial,
            Material emberMaterial,
            Material particleMaterial)
        {
            Transform anchors = new GameObject("BossBraziers").transform;
            anchors.SetParent(parent, false);
            anchors.gameObject.layer = IgnoreRaycastLayer;
            for (int index = 0; index < 2; index++)
            {
                bool isRight = index == 1;
                float angle = isRight ? BossBrazierAngle : -BossBrazierAngle;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                    new Vector3(0f, 0f, BossBrazierRadius);
                position.y = ArenaFireAnchorHeight;
                GameObject brazier = AddFortressModel(
                    fortressSourceArt.Get(FortressSourceModelId.Brazier),
                    isRight
                        ? "BossBrazier_RightShoulder"
                        : "BossBrazier_LeftShoulder",
                    anchors,
                    position,
                    angle + 180f,
                    structureMaterial,
                    emberMaterial);
                brazier.transform.localScale = new Vector3(0.86f, 1.3f, 0.86f);
                MeshRenderer renderer =
                    brazier.GetComponentInChildren<MeshRenderer>(true);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                ConfigureFortressSurfaceVariation(
                    brazier,
                    index + 71,
                    true);
                BuildBrazierFlame(
                    brazier.transform,
                    particleMaterial,
                    index + 71);

                FortressFireAnchorPresenter presenter =
                    brazier.AddComponent<FortressFireAnchorPresenter>();
                SerializedObject serialized = new SerializedObject(presenter);
                serialized.FindProperty("_surface").objectReferenceValue = renderer;
                serialized.FindProperty("_emberMaterialIndex").intValue = 1;
                serialized.FindProperty("_emissionMultiplier").floatValue = 2.2f;
                serialized.FindProperty("_pulseSpeed").floatValue = 0.84f;
                serialized.FindProperty("_phaseOffset").floatValue =
                    isRight ? 2.7f : 0.8f;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                GameObject glowRoot = new GameObject(
                    isRight ? "BossGlowRight" : "BossGlowLeft",
                    typeof(Light));
                glowRoot.transform.SetParent(brazier.transform, false);
                glowRoot.transform.localPosition = new Vector3(0f, 2.45f, 0f);
                Light glow = glowRoot.GetComponent<Light>();
                glow.type = LightType.Point;
                glow.color = new Color(1f, 0.39f, 0.1f, 1f);
                glow.intensity = 6.5f;
                glow.range = 9.5f;
                glow.shadows = LightShadows.None;
            }
        }

        private static void BuildArenaFortressRing(
            Transform parent,
            float radius,
            FortressSourceArtSet fortressSourceArt,
            Material structureMaterial,
            Material emberMaterial,
            Material particleMaterial)
        {
            AddFortressModel(
                fortressSourceArt.Get(FortressSourceModelId.FoundationRing),
                "FortressFoundationRing",
                parent,
                Vector3.zero,
                0f,
                structureMaterial,
                emberMaterial);

            const int bayCount = 18;
            int wallIndex = 0;
            for (int index = 0; index < bayCount; index++)
            {
                float angle = ArenaFortressAngleOffset +
                    (index / (float)bayCount * 360f);
                Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                    new Vector3(0f, 0f, radius);
                Transform bay = new GameObject(
                    "FortressBay_" + (index + 1).ToString("00")).transform;
                bay.SetParent(parent, false);
                bay.localPosition = position;
                bay.localRotation = Quaternion.Euler(0f, angle, 0f);
                bay.gameObject.layer = IgnoreRaycastLayer;

                if (IsFarSideTowerBay(index))
                {
                    Vector3 towerOffset = new Vector3(
                        0f,
                        GetFortressModuleSink(index, true),
                        ArenaFortressTowerOutwardOffset);
                    GameObject tower = AddFortressModel(
                        fortressSourceArt.Get(FortressSourceModelId.Tower),
                        "FortressTower",
                        bay,
                        towerOffset,
                        0f,
                        structureMaterial,
                        emberMaterial);
                    tower.transform.localScale = GetFortressModuleScale(
                        index,
                        true);
                    ConfigureFortressSurfaceVariation(tower, index, true);
                    ConfigureTowerEmberPulse(tower, index);
                    continue;
                }

                FortressSourceModelId wallId = wallIndex % 2 == 0
                    ? FortressSourceModelId.WallStraight
                    : FortressSourceModelId.WallBroken;
                wallIndex++;
                GameObject wall = AddFortressModel(
                    fortressSourceArt.Get(wallId),
                    wallId == FortressSourceModelId.WallStraight
                        ? "FortressWallStraight"
                        : "FortressWallBroken",
                    bay,
                    new Vector3(
                        0f,
                        GetFortressModuleSink(index, false),
                        ArenaFortressWallOutwardOffset),
                    0f,
                    structureMaterial,
                    emberMaterial);
                wall.transform.localScale = GetFortressModuleScale(
                    index,
                    false);
                ConfigureFortressSurfaceVariation(wall, index, false);
            }

            BuildArenaFireAnchors(
                parent,
                fortressSourceArt,
                structureMaterial,
                emberMaterial,
                particleMaterial);
        }

        private static void ConfigureFortressSurfaceVariation(
            GameObject module,
            int bayIndex,
            bool isFireStructure)
        {
            MeshRenderer renderer = module.GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null)
            {
                throw new InvalidOperationException(
                    module.name + " must retain one renderer for surface variation.");
            }

            // A restrained five-value tint cycle and an independent texture phase
            // keep the repeated kit readable as hand-set masonry. Both changes are
            // presentation-only and share the original material/normal sampling.
            float value = 0.9f + (((bayIndex * 7) % 5) * 0.025f);
            float warmth = ((((bayIndex * 11) % 3) - 1) * 0.008f);
            Color baseTint = isFireStructure
                ? new Color(0.82f, 0.7f, 0.58f, 1f)
                : new Color(0.82f, 0.81f, 0.8f, 1f);
            Color tint = new Color(
                Mathf.Clamp01((baseTint.r * value) + warmth),
                Mathf.Clamp01(baseTint.g * value),
                Mathf.Clamp01((baseTint.b * value) - (warmth * 0.5f)),
                1f);
            Vector2 offset = new Vector2(
                ((bayIndex * 7) % 13) / 13f,
                ((bayIndex * 5) % 11) / 11f);

            FortressSurfaceVariationPresenter presenter =
                module.AddComponent<FortressSurfaceVariationPresenter>();
            presenter.Configure(renderer, 0, offset, tint);
            EditorUtility.SetDirty(presenter);
        }

        private static void ConfigureTowerEmberPulse(
            GameObject tower,
            int bayIndex)
        {
            MeshRenderer renderer = tower.GetComponentInChildren<MeshRenderer>(true);
            if (renderer == null || renderer.sharedMaterials.Length != 2)
            {
                throw new InvalidOperationException(
                    tower.name + " must retain stone and ember material slots.");
            }

            FortressFireAnchorPresenter presenter =
                tower.AddComponent<FortressFireAnchorPresenter>();
            SerializedObject serialized = new SerializedObject(presenter);
            serialized.FindProperty("_surface").objectReferenceValue = renderer;
            serialized.FindProperty("_emberMaterialIndex").intValue = 1;
            serialized.FindProperty("_emissionMultiplier").floatValue =
                bayIndex == 2 || bayIndex == 5
                    ? 2.35f
                    : 1.5f;
            serialized.FindProperty("_pulseSpeed").floatValue = 0.72f;
            serialized.FindProperty("_phaseOffset").floatValue =
                0.4f + (bayIndex * 0.73f);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool IsFarSideTowerBay(int index)
        {
            switch (index)
            {
                case 1:
                case 2:
                case 3:
                case 5:
                case 6:
                case 7:
                    return true;

                default:
                    return false;
            }
        }

        private static bool IsShowcaseTowerBay(int index)
        {
            return index == 1 || index == 7;
        }

        private static float GetFortressModuleSink(int bayIndex, bool isTower)
        {
            if (isTower)
            {
                return IsShowcaseTowerBay(bayIndex)
                    ? ShowcaseTowerSink
                    : FortressTowerSink;
            }

            // Three shallow foundation depths break the repeated silhouette while
            // keeping every ground-origin module at or below the lava plane. The
            // variation is deterministic and affects presentation only.
            int variation = (bayIndex * 5) % 3;
            return FortressWallBaseSink + (variation * FortressWallSinkStep);
        }

        private static Vector3 GetFortressModuleScale(
            int bayIndex,
            bool isTower)
        {
            float verticalScale = GetFortressModuleVerticalScale(
                bayIndex,
                isTower);
            return new Vector3(1f, verticalScale, 1f);
        }

        private static float GetFortressModuleVerticalScale(
            int bayIndex,
            bool isTower)
        {
            if (isTower)
            {
                switch (bayIndex)
                {
                    case 1:
                    case 7:
                        return 0.92f;

                    case 2:
                    case 6:
                        return 1.12f;

                    case 3:
                    case 5:
                        return 1.25f;

                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(bayIndex),
                            bayIndex,
                            "Only the six far-side bays may contain fortress towers.");
                }
            }

            // The camera sits on the -X side. The low near arc reads as a broken
            // foundation instead of a row of cropped panels. The raised far wall
            // shoulders and keystone expose continuous masonry planes above the
            // foundation in the locked strategy frame.
            switch (bayIndex)
            {
                case 0:
                case 8:
                    return 1.25f;

                case 4:
                    return 1.42f;

                case 9:
                case 17:
                    return 0.6f;

                case 10:
                case 16:
                    return 0.54f;

                case 11:
                case 15:
                    return 0.48f;

                case 12:
                case 14:
                    return 0.43f;

                case 13:
                    return 0.4f;

                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(bayIndex),
                        bayIndex,
                        "Only non-tower bays may use the wall composition scale.");
            }
        }

        private static void BuildArenaFireAnchors(
            Transform parent,
            FortressSourceArtSet fortressSourceArt,
            Material structureMaterial,
            Material emberMaterial,
            Material particleMaterial)
        {
            Transform anchors = new GameObject("ArenaBraziers").transform;
            anchors.SetParent(parent, false);
            anchors.gameObject.layer = IgnoreRaycastLayer;
            for (int index = 0; index < ArenaFireAnchorNames.Length; index++)
            {
                GameObject brazier = AddFortressModel(
                    fortressSourceArt.Get(FortressSourceModelId.Brazier),
                    ArenaFireAnchorNames[index],
                    anchors,
                    GetArenaFireAnchorPosition(index),
                    ArenaFireAnchorAngles[index] + 180f,
                    structureMaterial,
                    emberMaterial);
                brazier.transform.localScale = new Vector3(
                    ArenaFireAnchorPlanarScale,
                    ArenaFireAnchorVerticalScale,
                    ArenaFireAnchorPlanarScale);
                MeshRenderer renderer = brazier.GetComponentInChildren<MeshRenderer>(true);
                renderer.shadowCastingMode = ShadowCastingMode.On;
                ConfigureFortressSurfaceVariation(
                    brazier,
                    index + 83,
                    true);
                FortressFireAnchorPresenter presenter =
                    brazier.AddComponent<FortressFireAnchorPresenter>();
                SerializedObject serialized = new SerializedObject(presenter);
                serialized.FindProperty("_surface").objectReferenceValue = renderer;
                serialized.FindProperty("_emberMaterialIndex").intValue = 1;
                serialized.FindProperty("_emissionMultiplier").floatValue = 2.4f;
                serialized.FindProperty("_phaseOffset").floatValue =
                    0.35f + (index * 0.6f);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                BuildBrazierFlame(
                    brazier.transform,
                    particleMaterial,
                    index + 83);
            }
        }

        private static Vector3 GetArenaFireAnchorPosition(int index)
        {
            if (index < 0 || index >= ArenaFireAnchorAngles.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            float angle = ArenaFireAnchorAngles[index];
            float radius = ArenaFireAnchorRadii[index];
            Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                new Vector3(0f, 0f, radius);
            position.y = ArenaFireAnchorHeight;
            return position;
        }

        private static void BuildBrazierFlame(
            Transform brazier,
            Material particleMaterial,
            int seed)
        {
            if (brazier == null)
            {
                throw new ArgumentNullException(nameof(brazier));
            }

            if (particleMaterial == null)
            {
                throw new ArgumentNullException(nameof(particleMaterial));
            }

            GameObject flame = new GameObject(
                "FirePlume",
                typeof(ParticleSystem));
            flame.transform.SetParent(brazier, false);
            flame.transform.localPosition = new Vector3(0f, 2.35f, 0f);
            flame.layer = IgnoreRaycastLayer;

            ParticleSystem particles = flame.GetComponent<ParticleSystem>();
            particles.useAutoRandomSeed = false;
            particles.randomSeed = (uint)(0x51A7 + (seed * 97));

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1.4f;
            main.maxParticles = 16;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.55f, 1.05f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 0.95f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            main.startRotation = new ParticleSystem.MinMaxCurve(
                -0.32f,
                0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.24f, 0.025f, 0.82f),
                new Color(1f, 0.86f, 0.3f, 1f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 18f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 9f;
            shape.radius = 0.13f;
            shape.length = 0.26f;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.45f, 1f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.08f, 0.08f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.11f;
            noise.frequency = 1.4f;
            noise.scrollSpeed = 0.35f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.95f, 0.7f), 0f),
                    new GradientColorKey(new Color(1f, 0.34f, 0.045f), 0.62f),
                    new GradientColorKey(new Color(0.55f, 0.08f, 0.01f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.72f, 0.72f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = fade;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.55f),
                    new Keyframe(0.18f, 1f),
                    new Keyframe(1f, 0.18f)));

            ParticleSystemRenderer particleRenderer =
                flame.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = particleMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.maxParticleSize = 0.12f;
            particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.lightProbeUsage = LightProbeUsage.Off;
            particleRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particleRenderer.sortingFudge = 0.15f;

            BuildBrazierFireCore(brazier, particleMaterial, seed);
            BuildBrazierEmberSparks(brazier, particleMaterial, seed);
        }

        private static void BuildBrazierFireCore(
            Transform brazier,
            Material particleMaterial,
            int seed)
        {
            GameObject core = new GameObject(
                "FireCore",
                typeof(ParticleSystem));
            core.transform.SetParent(brazier, false);
            core.transform.localPosition = new Vector3(0f, 2.31f, 0f);
            core.layer = IgnoreRaycastLayer;

            ParticleSystem particles = core.GetComponent<ParticleSystem>();
            particles.useAutoRandomSeed = false;
            particles.randomSeed = (uint)(0xF17E + (seed * 131));

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1.1f;
            main.maxParticles = 10;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.6f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 1.05f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
            main.startRotation = new ParticleSystem.MinMaxCurve(-0.16f, 0.16f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.58f, 0.06f, 0.88f),
                new Color(1f, 0.98f, 0.72f, 1f));

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 16f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.06f;
            shape.length = 0.16f;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.62f, 1.18f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.035f, 0.035f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.065f;
            noise.frequency = 1.75f;
            noise.scrollSpeed = 0.46f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 1f, 0.84f), 0f),
                    new GradientColorKey(new Color(1f, 0.56f, 0.07f), 0.72f),
                    new GradientColorKey(new Color(0.85f, 0.12f, 0.01f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.08f),
                    new GradientAlphaKey(0.72f, 0.72f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = fade;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.4f),
                    new Keyframe(0.16f, 1f),
                    new Keyframe(1f, 0.08f)));

            ParticleSystemRenderer renderer =
                core.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.maxParticleSize = 0.075f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingFudge = 0.24f;
        }

        private static void BuildBrazierEmberSparks(
            Transform brazier,
            Material particleMaterial,
            int seed)
        {
            GameObject sparks = new GameObject(
                "EmberSparks",
                typeof(ParticleSystem));
            sparks.transform.SetParent(brazier, false);
            sparks.transform.localPosition = new Vector3(0f, 2.3f, 0f);
            sparks.layer = IgnoreRaycastLayer;

            ParticleSystem particles = sparks.GetComponent<ParticleSystem>();
            particles.useAutoRandomSeed = false;
            particles.randomSeed = (uint)(0xE8B3 + (seed * 173));

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = true;
            main.loop = true;
            main.duration = 1.8f;
            main.maxParticles = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Shape;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.7f, 1.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.34f, 0.82f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.052f);
            main.startRotation = new ParticleSystem.MinMaxCurve(
                0f,
                Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.28f, 0.015f, 0.74f),
                new Color(1f, 0.9f, 0.36f, 1f));
            main.gravityModifier = 0.06f;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 4.2f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.15f;
            shape.length = 0.18f;

            ParticleSystem.VelocityOverLifetimeModule velocity =
                particles.velocityOverLifetime;
            velocity.enabled = true;
            velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = new ParticleSystem.MinMaxCurve(-0.28f, 0.28f);
            velocity.y = new ParticleSystem.MinMaxCurve(0.58f, 1.28f);
            velocity.z = new ParticleSystem.MinMaxCurve(-0.23f, 0.23f);

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.17f;
            noise.frequency = 0.86f;
            noise.scrollSpeed = 0.3f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.96f, 0.68f), 0f),
                    new GradientColorKey(new Color(1f, 0.34f, 0.035f), 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(0.68f, 0.64f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = fade;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.6f),
                    new Keyframe(0.14f, 1f),
                    new Keyframe(1f, 0.18f)));

            ParticleSystemRenderer renderer =
                sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = particleMaterial;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.lengthScale = 1.45f;
            renderer.velocityScale = 0.11f;
            renderer.cameraVelocityScale = 0f;
            renderer.maxParticleSize = 0.045f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.sortingFudge = 0.3f;
        }

        private static Mesh CreateRadialFloorMesh(
            string name,
            float radius,
            int patternSeed)
        {
            var vertices = new List<Vector3>(FloorSegmentCount * 28);
            var normals = new List<Vector3>(FloorSegmentCount * 28);
            var uvs = new List<Vector2>(FloorSegmentCount * 28);
            var triangles = new[]
            {
                new List<int>(FloorSegmentCount * 12),
                new List<int>(FloorSegmentCount * 6),
                new List<int>(FloorSegmentCount * 6),
                new List<int>(FloorSegmentCount * 12),
            };
            float[] ringRadii =
            {
                0f,
                radius * 0.27f,
                radius * 0.6f,
                radius * 0.84f,
                radius,
            };

            for (int band = 0; band < ringRadii.Length - 1; band++)
            {
                float innerRadius = ringRadii[band];
                float outerRadius = ringRadii[band + 1];
                float slabInnerRadius = band == 0 ? 0f : innerRadius + 0.018f;
                float slabOuterRadius = band == ringRadii.Length - 2
                    ? outerRadius
                    : outerRadius - 0.018f;
                float bandAngleOffset = (band % 2) *
                    (Mathf.PI / FloorSegmentCount);
                for (int sector = 0; sector < FloorSegmentCount; sector++)
                {
                    float angularInset = 0.0014f +
                        ((((sector * 5) + band + patternSeed) % 3) * 0.00025f);
                    float startAngle =
                        (sector / (float)FloorSegmentCount * Mathf.PI * 2f) +
                        bandAngleOffset + angularInset;
                    float endAngle =
                        ((sector + 1) / (float)FloorSegmentCount * Mathf.PI * 2f) +
                        bandAngleOffset - angularInset;
                    float height = 0.012f +
                        ((((sector * 11) + (band * 7) + patternSeed) % 5) * 0.003f);
                    Vector3 slabNormal = new Vector3(
                        Mathf.Sin((sector * 1.91f) + (band * 0.73f) + patternSeed) * 0.045f,
                        1f,
                        Mathf.Cos((sector * 1.37f) - (band * 0.61f) + patternSeed) * 0.045f)
                        .normalized;
                    int materialIndex = ResolveFloorMaterialIndex(
                        band,
                        sector,
                        patternSeed,
                        ringRadii.Length - 1);
                    if (slabInnerRadius <= 0.001f)
                    {
                        AddTopTriangle(
                            vertices,
                            normals,
                            uvs,
                            triangles[materialIndex],
                            Vector2.zero,
                            Polar(slabOuterRadius, endAngle),
                            Polar(slabOuterRadius, startAngle),
                            height,
                            radius,
                            slabNormal);
                    }
                    else
                    {
                        AddTopQuad(
                            vertices,
                            normals,
                            uvs,
                            triangles[materialIndex],
                            Polar(slabInnerRadius, startAngle),
                            Polar(slabOuterRadius, startAngle),
                            Polar(slabOuterRadius, endAngle),
                            Polar(slabInnerRadius, endAngle),
                            height,
                            radius,
                            slabNormal);
                    }
                }
            }

            float bevelOuterRadius = radius + 0.42f;
            for (int sector = 0; sector < FloorSegmentCount; sector++)
            {
                float startAngle = sector / (float)FloorSegmentCount * Mathf.PI * 2f;
                float endAngle = (sector + 1) / (float)FloorSegmentCount * Mathf.PI * 2f;
                AddBevelQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles[3],
                    Polar(radius, startAngle),
                    Polar(bevelOuterRadius, startAngle),
                    Polar(bevelOuterRadius, endAngle),
                    Polar(radius, endAngle),
                    0.018f,
                    -0.34f,
                    sector / (float)FloorSegmentCount,
                    (sector + 1) / (float)FloorSegmentCount);
                AddBevelQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles[2],
                    Polar(bevelOuterRadius, startAngle),
                    Polar(bevelOuterRadius + 0.06f, startAngle),
                    Polar(bevelOuterRadius + 0.06f, endAngle),
                    Polar(bevelOuterRadius, endAngle),
                    -0.34f,
                    -0.68f,
                    sector / (float)FloorSegmentCount,
                    (sector + 1) / (float)FloorSegmentCount);
            }

            return CreateMesh(name, vertices, normals, uvs, triangles);
        }

        private static Mesh CreateArenaOuterBasaltCrustMesh()
        {
            const int clusterCount = 24;
            var vertices = new List<Vector3>(900);
            var normals = new List<Vector3>(900);
            var uvs = new List<Vector2>(900);
            var triangles = new[]
            {
                new List<int>(1200),
                new List<int>(650),
            };

            for (int cluster = 0; cluster < clusterCount; cluster++)
            {
                // Three broad gaps preserve visible lava lanes and prevent the
                // outer dressing from reading as another perfect ring.
                if (cluster % 8 == 3)
                {
                    continue;
                }

                float angle = ((cluster / (float)clusterCount) * Mathf.PI * 2f) +
                    ((((cluster * 5) % 7) - 3f) * 0.018f);
                float radialJitter = (((cluster * 11) % 5) - 2f) * 0.42f;
                float radius = 21.5f + radialJitter;
                Vector2 center = Polar(radius, angle);
                float bottomRadius = 1.12f + ((cluster % 5) * 0.16f);
                float topRadius = bottomRadius *
                    (0.62f + ((cluster % 3) * 0.055f));
                float topY = -0.43f + ((cluster % 4) * 0.075f);
                int materialIndex = cluster % 5 == 2 ? 1 : 0;
                AddStoneTier(
                    vertices,
                    normals,
                    uvs,
                    triangles[materialIndex],
                    center,
                    bottomRadius,
                    topRadius,
                    -0.83f,
                    topY,
                    angle + 0.21f,
                    410 + cluster,
                    8);

                if (cluster % 4 == 0)
                {
                    Vector2 capCenter = center + Polar(
                        bottomRadius * 0.28f,
                        angle + 1.15f);
                    AddStoneTier(
                        vertices,
                        normals,
                        uvs,
                        triangles[1],
                        capCenter,
                        bottomRadius * 0.46f,
                        bottomRadius * 0.28f,
                        topY - 0.03f,
                        topY + 0.34f,
                        angle - 0.14f,
                        470 + cluster,
                        6);
                }
            }

            Mesh mesh = CreateMesh(
                "ArenaOuterBasaltCrust",
                vertices,
                normals,
                uvs,
                triangles);
            mesh.RecalculateTangents();
            return mesh;
        }

        private static Mesh CreateArenaFlankSilhouetteMesh()
        {
            var vertices = new List<Vector3>(1400);
            var normals = new List<Vector3>(1400);
            var uvs = new List<Vector2>(1400);
            var triangles = new[]
            {
                new List<int>(2200),
                new List<int>(160),
                new List<int>(120),
            };

            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                float side = sideIndex == 0 ? -1f : 1f;
                Vector2 outerTower = new Vector2(9.8f, side * 22.7f);
                Vector2 innerTower = new Vector2(16.25f, side * 20.05f);
                int seed = 190 + (sideIndex * 37);

                AddArenaFlankSilhouetteTower(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    outerTower,
                    sideIndex == 0 ? 8.85f : 9.35f,
                    1.85f,
                    seed);
                AddArenaFlankSilhouetteTower(
                    vertices,
                    normals,
                    uvs,
                    triangles,
                    innerTower,
                    sideIndex == 0 ? 10.7f : 11.15f,
                    1.58f,
                    seed + 13);

                Vector2 wallStart = Vector2.Lerp(outerTower, innerTower, 0.18f);
                Vector2 wallEnd = Vector2.Lerp(outerTower, innerTower, 0.82f);
                AddArenaSilhouetteWallBlock(
                    vertices,
                    normals,
                    uvs,
                    triangles[0],
                    wallStart,
                    wallEnd,
                    0.68f,
                    -1.18f,
                    3.75f);
                AddArenaSilhouetteWallBlock(
                    vertices,
                    normals,
                    uvs,
                    triangles[0],
                    Vector2.Lerp(wallStart, wallEnd, 0.24f),
                    Vector2.Lerp(wallStart, wallEnd, 0.72f),
                    0.48f,
                    3.76f,
                    5.25f + (sideIndex * 0.22f));

                Vector2 wallCenter = Vector2.Lerp(wallStart, wallEnd, 0.5f);
                AddArenaSilhouettePanel(
                    vertices,
                    normals,
                    uvs,
                    triangles[1],
                    wallCenter,
                    0.7f,
                    1.15f,
                    3.38f,
                    0.34f);
            }

            Mesh mesh = CreateMesh(
                "ArenaFlankSilhouettes",
                vertices,
                normals,
                uvs,
                triangles);
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AddArenaFlankSilhouetteTower(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            IReadOnlyList<List<int>> triangles,
            Vector2 center,
            float height,
            float radius,
            int seed)
        {
            float rotation = Mathf.Atan2(center.y, center.x) +
                (Mathf.PI * 0.125f);
            AddStoneTier(
                vertices,
                normals,
                uvs,
                triangles[0],
                center,
                radius,
                radius * 0.9f,
                -1.22f,
                0.22f,
                rotation,
                seed,
                8);
            AddStoneTier(
                vertices,
                normals,
                uvs,
                triangles[0],
                center,
                radius * 0.91f,
                radius * 0.7f,
                0.24f,
                1.25f,
                rotation + 0.035f,
                seed + 1,
                8);
            AddStoneTier(
                vertices,
                normals,
                uvs,
                triangles[0],
                center,
                radius * 0.59f,
                radius * 0.52f,
                1.27f,
                height - 1.12f,
                rotation - 0.025f,
                seed + 2,
                8);
            AddStoneTier(
                vertices,
                normals,
                uvs,
                triangles[0],
                center,
                radius * 0.77f,
                radius * 0.68f,
                height - 1.1f,
                height - 0.68f,
                rotation + 0.04f,
                seed + 3,
                8);

            for (int tooth = 0; tooth < 3; tooth++)
            {
                float toothAngle = rotation + ((tooth - 1) * 0.7f);
                Vector2 toothCenter = center +
                    Polar(radius * 0.47f, toothAngle);
                float toothHeight = height +
                    ((((seed + tooth) % 3) - 1) * 0.17f);
                AddStoneTier(
                    vertices,
                    normals,
                    uvs,
                    triangles[0],
                    toothCenter,
                    radius * 0.2f,
                    radius * 0.15f,
                    height - 0.67f,
                    toothHeight,
                    toothAngle,
                    seed + 4 + tooth,
                    6);
            }

            AddArenaSilhouettePanel(
                vertices,
                normals,
                uvs,
                triangles[1],
                center,
                (radius * 0.535f) + 0.025f,
                2.15f,
                Mathf.Min(height - 2.25f, 5.75f),
                radius * 0.19f);
            AddArenaSilhouettePanel(
                vertices,
                normals,
                uvs,
                triangles[2],
                center,
                (radius * 0.54f) + 0.035f,
                height - 2.18f,
                height - 1.42f,
                radius * 0.07f);
        }

        private static void AddArenaSilhouettePanel(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 center,
            float surfaceRadius,
            float bottomY,
            float topY,
            float halfWidth)
        {
            Vector2 inward = -center.normalized;
            Vector2 tangent = new Vector2(-inward.y, inward.x);
            Vector2 surfaceCenter = center + (inward * surfaceRadius);
            Vector2 left = surfaceCenter - (tangent * halfWidth);
            Vector2 right = surfaceCenter + (tangent * halfWidth);
            AddVerticalFace(
                vertices,
                normals,
                uvs,
                indices,
                new Vector3(left.x, topY, left.y),
                new Vector3(left.x, bottomY, left.y),
                new Vector3(right.x, bottomY, right.y),
                new Vector3(right.x, topY, right.y));
        }

        private static void AddArenaSilhouetteWallBlock(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 startPoint,
            Vector2 endPoint,
            float halfWidth,
            float bottomY,
            float topY)
        {
            Vector2 direction = endPoint - startPoint;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            direction.Normalize();
            Vector2 side = new Vector2(-direction.y, direction.x) * halfWidth;
            Vector2 startLeft = startPoint + side;
            Vector2 startRight = startPoint - side;
            Vector2 endLeft = endPoint + side;
            Vector2 endRight = endPoint - side;

            AddVerticalFace(
                vertices,
                normals,
                uvs,
                indices,
                new Vector3(startLeft.x, topY, startLeft.y),
                new Vector3(startLeft.x, bottomY, startLeft.y),
                new Vector3(endLeft.x, bottomY, endLeft.y),
                new Vector3(endLeft.x, topY, endLeft.y));
            AddVerticalFace(
                vertices,
                normals,
                uvs,
                indices,
                new Vector3(endRight.x, topY, endRight.y),
                new Vector3(endRight.x, bottomY, endRight.y),
                new Vector3(startRight.x, bottomY, startRight.y),
                new Vector3(startRight.x, topY, startRight.y));
            AddVerticalFace(
                vertices,
                normals,
                uvs,
                indices,
                new Vector3(startRight.x, topY, startRight.y),
                new Vector3(startRight.x, bottomY, startRight.y),
                new Vector3(startLeft.x, bottomY, startLeft.y),
                new Vector3(startLeft.x, topY, startLeft.y));
            AddVerticalFace(
                vertices,
                normals,
                uvs,
                indices,
                new Vector3(endLeft.x, topY, endLeft.y),
                new Vector3(endLeft.x, bottomY, endLeft.y),
                new Vector3(endRight.x, bottomY, endRight.y),
                new Vector3(endRight.x, topY, endRight.y));
            AddTopQuad(
                vertices,
                normals,
                uvs,
                indices,
                startRight,
                endRight,
                endLeft,
                startLeft,
                topY,
                2f);
        }

        private static Mesh CreateArenaBoundaryStoneworkMesh()
        {
            var vertices = new List<Vector3>(1800);
            var normals = new List<Vector3>(1800);
            var uvs = new List<Vector2>(1800);
            var triangles = new[]
            {
                new List<int>(2400),
                new List<int>(1400),
                new List<int>(240),
            };

            AddArenaBoundaryCurb(vertices, normals, uvs, triangles);
            AddArenaOuterRubble(vertices, normals, uvs, triangles);
            AddArenaReliquaryColumns(vertices, normals, uvs, triangles);

            Mesh mesh = CreateMesh(
                "ArenaBoundaryStonework",
                vertices,
                normals,
                uvs,
                triangles);
            mesh.RecalculateTangents();
            return mesh;
        }

        private static void AddArenaBoundaryCurb(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            IReadOnlyList<List<int>> triangles)
        {
            const int segmentCount = 64;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                float angularInset = 0.0007f + ((segment % 3) * 0.00018f);
                float startAngle =
                    (segment / (float)segmentCount * Mathf.PI * 2f) + angularInset;
                float endAngle =
                    ((segment + 1) / (float)segmentCount * Mathf.PI * 2f) -
                    angularInset;
                float outerRadius = ArenaBoundaryOuterRadius -
                    ((segment % 7 == 0 ? 0.022f : 0f) +
                     (segment % 11 == 0 ? 0.014f : 0f));
                float topY = -0.105f - ((segment % 4) * 0.005f);
                int topMaterial = (segment + 1) % 7 == 0 ? 0 : 1;

                AddTopQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles[topMaterial],
                    Polar(ArenaBoundaryInnerRadius, startAngle),
                    Polar(outerRadius, startAngle),
                    Polar(outerRadius, endAngle),
                    Polar(ArenaBoundaryInnerRadius, endAngle),
                    topY,
                    ArenaBoundaryOuterRadius);
                AddBevelQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles[1],
                    Polar(outerRadius, startAngle),
                    Polar(outerRadius + 0.18f, startAngle),
                    Polar(outerRadius + 0.18f, endAngle),
                    Polar(outerRadius, endAngle),
                    topY,
                    -0.58f,
                    segment / (float)segmentCount,
                    (segment + 1) / (float)segmentCount);

                if (segment % 10 != 3)
                {
                    continue;
                }

                float keyCenter = (startAngle + endAngle) * 0.5f;
                float keyHalfWidth = Mathf.Deg2Rad * 0.28f;
                AddTopQuad(
                    vertices,
                    normals,
                    uvs,
                    triangles[2],
                    Polar(ArenaBoundaryInnerRadius + 0.12f, keyCenter - keyHalfWidth),
                    Polar(outerRadius + 0.012f, keyCenter - keyHalfWidth),
                    Polar(outerRadius + 0.012f, keyCenter + keyHalfWidth),
                    Polar(ArenaBoundaryInnerRadius + 0.12f, keyCenter + keyHalfWidth),
                    topY + 0.008f,
                    ArenaBoundaryOuterRadius);
            }
        }

        private static void AddArenaOuterRubble(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            IReadOnlyList<List<int>> triangles)
        {
            float[] yawAngles =
            {
                9f, 22f, 39f, 53f, 72f, 82f,
                101f, 114f, 132f, 147f, 163f, 176f,
            };
            for (int rubble = 0; rubble < yawAngles.Length; rubble++)
            {
                float radians = (90f - yawAngles[rubble]) * Mathf.Deg2Rad;
                float centerRadius = ArenaLayout.OuterKillRadius + 1.05f +
                    ((rubble % 3) * 0.16f);
                Vector2 center = Polar(centerRadius, radians);
                float bottomRadius = 0.27f + ((rubble % 4) * 0.075f);
                float topRadius = bottomRadius * (0.6f +
                    ((rubble % 2) * 0.09f));
                float bottomY = -0.51f;
                float topY = -0.12f + ((rubble % 5) * 0.095f);
                AddStoneTier(
                    vertices,
                    normals,
                    uvs,
                    triangles[rubble % 5 == 2 ? 1 : 0],
                    center,
                    bottomRadius,
                    topRadius,
                    bottomY,
                    topY,
                    radians + ((rubble % 3) * 0.17f),
                    40 + rubble,
                    6);
            }
        }

        private static void AddArenaReliquaryColumns(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            IReadOnlyList<List<int>> triangles)
        {
            // Match the fortress yaw convention: the fixed strategy camera sits
            // on -X, so these five low columns stay on the far/+X half.
            float[] yawAngles = { 32f, 59f, 90f, 121f, 148f };
            for (int column = 0; column < yawAngles.Length; column++)
            {
                float radians = (90f - yawAngles[column]) * Mathf.Deg2Rad;
                Vector2 center = Polar(
                    ArenaReliquaryRadius + ((column % 2) * 0.18f),
                    radians);
                float baseY = -0.49f;
                float heightVariation = (column % 3) * 0.14f;
                float rotation = radians + (Mathf.PI * 0.125f) +
                    ((column % 2) * 0.08f);

                AddStoneTier(
                    vertices,
                    normals,
                    uvs,
                    triangles[0],
                    center,
                    0.69f,
                    0.61f,
                    baseY,
                    baseY + 0.28f,
                    rotation,
                    column * 3,
                    8);
                AddStoneTier(
                    vertices,
                    normals,
                    uvs,
                    triangles[0],
                    center,
                    0.48f,
                    0.41f,
                    baseY + 0.3f,
                    baseY + 1.22f + heightVariation,
                    rotation + 0.07f,
                    (column * 3) + 1,
                    8);
                AddStoneTier(
                    vertices,
                    normals,
                    uvs,
                    triangles[column % 2 == 0 ? 0 : 1],
                    center,
                    0.62f,
                    0.51f,
                    baseY + 1.24f + heightVariation,
                    baseY + 1.52f + heightVariation,
                    rotation - 0.045f,
                    (column * 3) + 2,
                    8);
            }
        }

        private static void AddStoneTier(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 center,
            float bottomRadius,
            float topRadius,
            float bottomY,
            float topY,
            float rotation,
            int patternSeed,
            int sideCount)
        {
            Vector3 topCenter = new Vector3(center.x, topY, center.y);
            for (int side = 0; side < sideCount; side++)
            {
                float startAngle = rotation +
                    (side / (float)sideCount * Mathf.PI * 2f);
                float endAngle = rotation +
                    ((side + 1) / (float)sideCount * Mathf.PI * 2f);
                float startRoughness = 1f +
                    ((((side * 3) + patternSeed) % 5) - 2f) * 0.018f;
                float endRoughness = 1f +
                    (((((side + 1) * 3) + patternSeed) % 5) - 2f) * 0.018f;
                Vector2 bottomStart = center +
                    Polar(bottomRadius * startRoughness, startAngle);
                Vector2 bottomEnd = center +
                    Polar(bottomRadius * endRoughness, endAngle);
                Vector2 topStart = center +
                    Polar(topRadius * startRoughness, startAngle);
                Vector2 topEnd = center +
                    Polar(topRadius * endRoughness, endAngle);

                AddVerticalFace(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    new Vector3(topStart.x, topY, topStart.y),
                    new Vector3(bottomStart.x, bottomY, bottomStart.y),
                    new Vector3(bottomEnd.x, bottomY, bottomEnd.y),
                    new Vector3(topEnd.x, topY, topEnd.y));
                AddTriangleFace(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    topCenter,
                    new Vector3(topEnd.x, topY, topEnd.y),
                    new Vector3(topStart.x, topY, topStart.y),
                    Vector3.up);
            }
        }

        private static void AddVerticalFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector3 first,
            Vector3 second,
            Vector3 third,
            Vector3 fourth)
        {
            Vector3 normal = Vector3.Cross(third - first, second - first).normalized;
            int start = vertices.Count;
            vertices.Add(first);
            vertices.Add(second);
            vertices.Add(third);
            vertices.Add(fourth);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(Vector2.zero);
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(Vector2.one);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 1);
            indices.Add(start);
            indices.Add(start + 3);
            indices.Add(start + 2);
        }

        private static void AddTriangleFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector3 first,
            Vector3 second,
            Vector3 third,
            Vector3 normal)
        {
            int start = vertices.Count;
            vertices.Add(first);
            vertices.Add(second);
            vertices.Add(third);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            uvs.Add(new Vector2(0.5f, 0.5f));
            uvs.Add(Vector2.one);
            uvs.Add(new Vector2(1f, 0f));
            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
        }

        private static int ResolveFloorMaterialIndex(
            int band,
            int sector,
            int patternSeed,
            int bandCount)
        {
            if (band == bandCount - 1)
            {
                return ((sector / 2) + patternSeed) % 5 == 0 ? 2 : 0;
            }

            int pattern = ((sector / 3) + (band * 2) + patternSeed) % 7;
            return pattern == 0 || pattern == 4 ? 1 : 0;
        }

        private static Mesh CreateFloorEtchingMesh(
            string name,
            float radius,
            int patternSeed)
        {
            var vertices = new List<Vector3>(256);
            var normals = new List<Vector3>(256);
            var uvs = new List<Vector2>(256);
            var indices = new List<int>(384);
            const int crackCount = 13;
            for (int index = 0; index < crackCount; index++)
            {
                float baseAngle = ((index * 137.5f) + (patternSeed * 19f)) * Mathf.Deg2Rad;
                float startRadius = radius * (0.31f + ((index % 6) * 0.1f));
                float crackLength = radius * (0.09f + ((index % 4) * 0.025f));
                float middleRadius = Mathf.Min(radius * 0.92f, startRadius + (crackLength * 0.52f));
                float endRadius = Mathf.Min(radius * 0.95f, startRadius + crackLength);
                Vector2 start = Polar(startRadius, baseAngle);
                Vector2 middle = Polar(
                    middleRadius,
                    baseAngle + (Mathf.Sin(index * 1.73f) * 0.09f));
                Vector2 end = Polar(
                    endRadius,
                    baseAngle + (Mathf.Cos(index * 1.17f) * 0.13f));
                float width = 0.012f + ((index % 4) * 0.004f);
                AddFlatRibbon(vertices, normals, uvs, indices, start, middle, width, 0.038f);
                AddFlatRibbon(
                    vertices,
                    normals,
                    uvs,
                    indices,
                    middle,
                    end,
                    width * 0.62f,
                    0.038f);
            }

            return CreateMesh(
                name,
                vertices,
                normals,
                uvs,
                new[] { indices });
        }

        private static void AddTopTriangle(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            float y,
            float uvScale)
        {
            AddTopTriangle(
                vertices,
                normals,
                uvs,
                indices,
                first,
                second,
                third,
                y,
                uvScale,
                Vector3.up);
        }

        private static void AddTopTriangle(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            float y,
            float uvScale,
            Vector3 surfaceNormal)
        {
            int start = vertices.Count;
            AddTopVertex(vertices, normals, uvs, first, y, uvScale, surfaceNormal);
            AddTopVertex(vertices, normals, uvs, second, y, uvScale, surfaceNormal);
            AddTopVertex(vertices, normals, uvs, third, y, uvScale, surfaceNormal);
            indices.Add(start);
            indices.Add(start + 1);
            indices.Add(start + 2);
        }

        private static void AddTopQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            float y,
            float uvScale)
        {
            AddTopQuad(
                vertices,
                normals,
                uvs,
                indices,
                first,
                second,
                third,
                fourth,
                y,
                uvScale,
                Vector3.up);
        }

        private static void AddTopQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 first,
            Vector2 second,
            Vector2 third,
            Vector2 fourth,
            float y,
            float uvScale,
            Vector3 surfaceNormal)
        {
            int start = vertices.Count;
            AddTopVertex(vertices, normals, uvs, first, y, uvScale, surfaceNormal);
            AddTopVertex(vertices, normals, uvs, second, y, uvScale, surfaceNormal);
            AddTopVertex(vertices, normals, uvs, third, y, uvScale, surfaceNormal);
            AddTopVertex(vertices, normals, uvs, fourth, y, uvScale, surfaceNormal);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 1);
            indices.Add(start);
            indices.Add(start + 3);
            indices.Add(start + 2);
        }

        private static void AddBevelQuad(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 innerStart,
            Vector2 outerStart,
            Vector2 outerEnd,
            Vector2 innerEnd,
            float innerY,
            float outerY,
            float startU,
            float endU)
        {
            int start = vertices.Count;
            AddBevelVertex(vertices, normals, uvs, innerStart, innerY, startU, 1f);
            AddBevelVertex(vertices, normals, uvs, outerStart, outerY, startU, 0f);
            AddBevelVertex(vertices, normals, uvs, outerEnd, outerY, endU, 0f);
            AddBevelVertex(vertices, normals, uvs, innerEnd, innerY, endU, 1f);
            indices.Add(start);
            indices.Add(start + 3);
            indices.Add(start + 2);
            indices.Add(start);
            indices.Add(start + 2);
            indices.Add(start + 1);
        }

        private static void AddFlatRibbon(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            List<int> indices,
            Vector2 startPoint,
            Vector2 endPoint,
            float halfWidth,
            float y)
        {
            Vector2 direction = endPoint - startPoint;
            if (direction.sqrMagnitude <= 0.000001f)
            {
                return;
            }

            direction.Normalize();
            Vector2 perpendicular = new Vector2(-direction.y, direction.x) * halfWidth;
            AddTopQuad(
                vertices,
                normals,
                uvs,
                indices,
                startPoint + perpendicular,
                startPoint - perpendicular,
                endPoint - perpendicular,
                endPoint + perpendicular,
                y,
                1f);
        }

        private static void AddTopVertex(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            Vector2 point,
            float y,
            float uvScale,
            Vector3 surfaceNormal)
        {
            vertices.Add(new Vector3(point.x, y, point.y));
            normals.Add(surfaceNormal.sqrMagnitude > 0.5f
                ? surfaceNormal.normalized
                : Vector3.up);
            float divisor = Mathf.Max(0.001f, uvScale * 0.5f);
            uvs.Add(new Vector2(
                0.5f + (point.x / divisor),
                0.5f + (point.y / divisor)));
        }

        private static void AddBevelVertex(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            Vector2 point,
            float y,
            float u,
            float v)
        {
            vertices.Add(new Vector3(point.x, y, point.y));
            Vector3 normal = new Vector3(point.x, point.magnitude * 0.42f, point.y).normalized;
            normals.Add(normal);
            uvs.Add(new Vector2(u, v));
        }

        private static Vector2 Polar(float radius, float radians)
        {
            return new Vector2(
                Mathf.Cos(radians) * radius,
                Mathf.Sin(radians) * radius);
        }

        private static Mesh CreateMesh(
            string name,
            List<Vector3> vertices,
            List<Vector3> normals,
            List<Vector2> uvs,
            IReadOnlyList<List<int>> submeshIndices)
        {
            Mesh mesh = new Mesh
            {
                name = name,
            };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.subMeshCount = submeshIndices.Count;
            for (int submesh = 0; submesh < submeshIndices.Count; submesh++)
            {
                mesh.SetTriangles(submeshIndices[submesh], submesh, false);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh EnsureMesh(string path, Mesh authored)
        {
            EnsureEnvironmentMeshFolder();
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (mesh == null)
            {
                mesh = authored;
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(authored, mesh);
            Object.DestroyImmediate(authored);
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static void EnsureEnvironmentMeshFolder()
        {
            if (AssetDatabase.IsValidFolder(EnvironmentMeshFolder))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(AuthoringAssets.ArtFolder))
            {
                throw new InvalidOperationException(
                    "Session RPG art folder must exist before environment authoring.");
            }

            AssetDatabase.CreateFolder(AuthoringAssets.ArtFolder, "Environment");
        }

        private static GameObject CreateMeshVisual(
            string name,
            Transform parent,
            Vector3 position,
            Mesh mesh,
            Material[] materials,
            bool castShadows,
            bool receiveShadows)
        {
            GameObject value = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            value.layer = IgnoreRaycastLayer;
            value.GetComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = value.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = castShadows
                ? ShadowCastingMode.On
                : ShadowCastingMode.Off;
            renderer.receiveShadows = receiveShadows;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return value;
        }

        private static void BuildArenaPillar(
            Transform parent,
            string name,
            float z,
            FortressSourceArtSet fortressSourceArt,
            Material bodyStone,
            Material emberMaterial,
            Material accent)
        {
            float radius = ArenaLayout.PillarRadius;
            GameObject pillar = new GameObject(name, typeof(CapsuleCollider));
            pillar.transform.SetParent(parent, false);
            pillar.transform.localPosition = new Vector3(0f, 0f, z);
            pillar.layer = DecorationLayer;

            CapsuleCollider collisionProxy = pillar.GetComponent<CapsuleCollider>();
            collisionProxy.direction = 1;
            collisionProxy.radius = radius;
            collisionProxy.height = ArenaLayout.PillarColliderHeight;
            collisionProxy.center = new Vector3(
                0f,
                ArenaLayout.PillarColliderHeight * 0.5f,
                0f);

            GameObject wardstone = AddFortressModel(
                fortressSourceArt.Get(FortressSourceModelId.Wardstone),
                name + "_Wardstone",
                pillar.transform,
                Vector3.zero,
                0f,
                bodyStone,
                emberMaterial);
            wardstone.transform.localScale = new Vector3(1.05f, 1.02f, 1.05f);
            BuildRuneAt(
                parent,
                new Vector3(0f, 0.045f, z),
                radius,
                accent,
                0.065f);
        }

        private static void BuildRune(
            Transform parent,
            float radius,
            Material material,
            float y)
        {
            BuildRuneAt(parent, new Vector3(0f, y, 0f), radius, material, 0.04f);
        }

        private static void BuildRune(
            Transform parent,
            float radius,
            Material material,
            float y,
            float thickness)
        {
            BuildRuneAt(parent, new Vector3(0f, y, 0f), radius, material, thickness);
        }

        private static void BuildRuneAt(
            Transform parent,
            Vector3 position,
            float radius,
            Material material,
            float thickness,
            string objectName = "RuneRing")
        {
            const int pointCount = 72;
            GameObject ringObject = new GameObject(objectName, typeof(LineRenderer));
            ringObject.transform.SetParent(parent, false);
            ringObject.transform.localPosition = position;
            ringObject.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            ringObject.layer = IgnoreRaycastLayer;

            LineRenderer ring = ringObject.GetComponent<LineRenderer>();
            ring.alignment = LineAlignment.TransformZ;
            ring.loop = true;
            ring.useWorldSpace = false;
            ring.positionCount = pointCount;
            ring.widthMultiplier = thickness;
            ring.numCapVertices = 2;
            ring.numCornerVertices = 2;
            ring.sharedMaterial = material;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.lightProbeUsage = LightProbeUsage.Off;
            ring.reflectionProbeUsage = ReflectionProbeUsage.Off;

            for (int index = 0; index < pointCount; index++)
            {
                float radians = (index / (float)pointCount) * Mathf.PI * 2f;
                ring.SetPosition(index, new Vector3(
                    Mathf.Cos(radians) * radius,
                    Mathf.Sin(radians) * radius,
                    0f));
            }
        }

        private static GameObject AddArenaFloorModel(
            GameObject source,
            Transform parent,
            string name,
            Material stoneMaterial,
            Material trimMaterial,
            Material groutMaterial)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (stoneMaterial == null)
            {
                throw new ArgumentNullException(nameof(stoneMaterial));
            }

            if (trimMaterial == null)
            {
                throw new ArgumentNullException(nameof(trimMaterial));
            }

            if (groutMaterial == null)
            {
                throw new ArgumentNullException(nameof(groutMaterial));
            }

            GameObject instance = PrefabUtility.InstantiatePrefab(source)
                as GameObject;
            if (instance == null)
            {
                throw new InvalidOperationException(
                    "Could not instantiate original arena-floor source '" +
                    AssetDatabase.GetAssetPath(source) + "'.");
            }

            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            SetLayerRecursively(instance, IgnoreRaycastLayer);

            Collider[] colliders = instance.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Object.DestroyImmediate(colliders[index]);
            }

            MeshRenderer[] renderers =
                instance.GetComponentsInChildren<MeshRenderer>(true);
            MeshFilter[] filters = instance.GetComponentsInChildren<MeshFilter>(true);
            if (renderers.Length != 1 || filters.Length != 1 ||
                filters[0].sharedMesh == null)
            {
                Object.DestroyImmediate(instance);
                throw new InvalidOperationException(
                    "Original arena-floor source '" +
                    AssetDatabase.GetAssetPath(source) +
                    "' must instantiate exactly one static MeshRenderer/MeshFilter.");
            }

            if (filters[0].sharedMesh.subMeshCount !=
                ArenaFloorSourceArtValidator.ExpectedSubmeshCount)
            {
                Object.DestroyImmediate(instance);
                throw new InvalidOperationException(
                    "Original arena-floor source '" +
                    AssetDatabase.GetAssetPath(source) +
                    "' must retain exactly three ordered material submeshes.");
            }

            MeshRenderer renderer = renderers[0];
            renderer.sharedMaterials = new[]
            {
                stoneMaterial,
                trimMaterial,
                groutMaterial,
            };
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
            return instance;
        }

        private static GameObject AddFortressModel(
            GameObject source,
            string name,
            Transform parent,
            Vector3 position,
            float yaw,
            Material stoneMaterial,
            Material emberMaterial)
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            if (stoneMaterial == null)
            {
                throw new ArgumentNullException(nameof(stoneMaterial));
            }

            if (emberMaterial == null)
            {
                throw new ArgumentNullException(nameof(emberMaterial));
            }

            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            root.transform.localScale = Vector3.one;
            root.layer = IgnoreRaycastLayer;

            GameObject instance = PrefabUtility.InstantiatePrefab(source) as GameObject;
            if (instance == null)
            {
                Object.DestroyImmediate(root);
                throw new InvalidOperationException(
                    "Could not instantiate original fortress source '" +
                    AssetDatabase.GetAssetPath(source) + "'.");
            }

            instance.name = source.name + "_Source";
            instance.transform.SetParent(root.transform, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            SetLayerRecursively(root, IgnoreRaycastLayer);

            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                Object.DestroyImmediate(colliders[index]);
            }

            MeshRenderer[] renderers = root.GetComponentsInChildren<MeshRenderer>(true);
            if (renderers.Length != 1)
            {
                Object.DestroyImmediate(root);
                throw new InvalidOperationException(
                    "Original fortress source '" +
                    AssetDatabase.GetAssetPath(source) + "' must instantiate exactly " +
                    "one MeshRenderer; found " + renderers.Length + ".");
            }

            MeshFilter filter = renderers[0].GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
            {
                Object.DestroyImmediate(root);
                throw new InvalidOperationException(
                    "Original fortress source '" +
                    AssetDatabase.GetAssetPath(source) + "' has no static mesh.");
            }

            int materialCount = filter.sharedMesh.subMeshCount;
            if (materialCount < 1 || materialCount > 2)
            {
                Object.DestroyImmediate(root);
                throw new InvalidOperationException(
                    "Original fortress source '" +
                    AssetDatabase.GetAssetPath(source) + "' must have one Stone " +
                    "submesh and at most one Ember submesh.");
            }

            Material[] materials = new Material[materialCount];
            materials[0] = stoneMaterial;
            if (materialCount == 2)
            {
                materials[1] = emberMaterial;
            }

            MeshRenderer renderer = renderers[0];
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Simple;
            return root;
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool keepCollider,
            int layer,
            bool showRenderer = true)
        {
            GameObject value = GameObject.CreatePrimitive(type);
            value.name = name;
            value.transform.SetParent(parent, false);
            value.transform.localPosition = position;
            value.transform.localScale = scale;
            value.layer = layer;

            Renderer renderer = value.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.enabled = showRenderer;
            }

            Collider generatedCollider = value.GetComponent<Collider>();
            if (!keepCollider)
            {
                Object.DestroyImmediate(generatedCollider);
            }
            else if (type == PrimitiveType.Cylinder)
            {
                Object.DestroyImmediate(generatedCollider);
                MeshFilter meshFilter = value.GetComponent<MeshFilter>();
                MeshCollider meshCollider = value.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshFilter.sharedMesh;
                meshCollider.convex = false;
            }

            return value;
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Material material,
            bool keepCollider,
            int layer)
        {
            return CreatePrimitive(
                name,
                type,
                parent,
                position,
                scale,
                material,
                keepCollider,
                layer,
                true);
        }

        private static void SetLayerRecursively(GameObject value, int layer)
        {
            value.layer = layer;
            for (int index = 0; index < value.transform.childCount; index++)
            {
                SetLayerRecursively(value.transform.GetChild(index).gameObject, layer);
            }
        }
    }
}
