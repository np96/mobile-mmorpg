using System;

namespace SessionRpg.Core
{
    /// <summary>
    /// Authoritative planar layout of Sunbreak Basin (world 1, zone 2).
    /// Nine 64 m presentation chunks surround this inset playable rectangle.
    /// Visual terrain never supplies collision, camp positions, or refuge safety.
    /// </summary>
    public static class PveRegionLayout
    {
        public const uint WorldIdValue = 1u;
        public const uint ZoneIdValue = 2u;
        public const float MinimumX = -58f;
        public const float MaximumX = 122f;
        public const float MinimumZ = -58f;
        public const float MaximumZ = 122f;
        public const float RefugeRadius = 10f;
        public const int CampCount = 7;
        // Landmark identifiers remain stable when a silhouette gains components.
        // A landmark bound is authoring/navigation metadata, never solid geometry.
        public const int MajorObstacleCount = 6;
        public const int LandmarkCount = MajorObstacleCount + CampCount;
        public const int ComponentsPerLandmark = 3;
        public const int ObstacleCount = LandmarkCount * ComponentsPerLandmark;

        public static readonly Float3 Refuge = new Float3(-46f, 0f, -42f);

        public static Float3 GetCampCenter(int index)
        {
            switch (index)
            {
                case 0: return new Float3(-10f, 0f, -38f);
                case 1: return new Float3(-35f, 0f, 12f);
                case 2: return new Float3(38f, 0f, -24f);
                case 3: return new Float3(12f, 0f, 45f);
                case 4: return new Float3(77f, 0f, 6f);
                case 5: return new Float3(60f, 0f, 63f);
                case 6: return new Float3(98f, 0f, 96f);
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        /// <summary>
        /// Conservative bound around one rock shelf or grove. Use its individual
        /// GetObstacle components for collision and visible raised footprints.
        /// Every component contains the anchor, so their union is star-convex and
        /// cannot enclose a hidden walkable pocket.
        /// </summary>
        public static ArenaCircleObstacle GetLandmark(int index)
        {
            if (index >= MajorObstacleCount && index < LandmarkCount)
            {
                return new ArenaCircleObstacle(
                    GetCampCenter(index - MajorObstacleCount) + new Float3(-8f, 0f, 12f),
                    7.2f);
            }

            switch (index)
            {
                // Camp-side shelves frame ordinary gameplay views. Their anchors
                // remain 19–27 m from camps 1, 2, 3, 6, 5 and 4 respectively;
                // camp 0 deliberately retains the more open refuge approach.
                case 0: return new ArenaCircleObstacle(new Float3(-16f, 0f, 5f), 12.8f);
                case 1: return new ArenaCircleObstacle(new Float3(57f, 0f, -40f), 13.1f);
                case 2: return new ArenaCircleObstacle(new Float3(29f, 0f, 27f), 14.1f);
                case 3: return new ArenaCircleObstacle(new Float3(101f, 0f, 71f), 15f);
                case 4: return new ArenaCircleObstacle(new Float3(78f, 0f, 45f), 13.4f);
                case 5: return new ArenaCircleObstacle(new Float3(95f, 0f, -12f), 13.3f);
                default: throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static int GetLandmarkObstacleStart(int landmarkIndex)
        {
            ValidateLandmarkIndex(landmarkIndex);
            return landmarkIndex * ComponentsPerLandmark;
        }

        public static int GetLandmarkObstacleCount(int landmarkIndex)
        {
            ValidateLandmarkIndex(landmarkIndex);
            return ComponentsPerLandmark;
        }

        public static int GetObstacleLandmarkIndex(int obstacleIndex)
        {
            if (obstacleIndex < 0 || obstacleIndex >= ObstacleCount)
            {
                throw new ArgumentOutOfRangeException(nameof(obstacleIndex));
            }

            return obstacleIndex / ComponentsPerLandmark;
        }

        public static ArenaCircleObstacle GetObstacle(int index)
        {
            int landmarkIndex = GetObstacleLandmarkIndex(index);
            Float3 anchor = GetLandmark(landmarkIndex).Center;
            int component = index % ComponentsPerLandmark;
            if (landmarkIndex >= MajorObstacleCount)
            {
                // The wider lobe varies across groves; the southern side remains
                // clear for combat spawns and the existing west approach.
                float side = (landmarkIndex & 1) == 0 ? 1f : -1f;
                switch (component)
                {
                    case 0: return new ArenaCircleObstacle(anchor, 4.2f);
                    case 1: return new ArenaCircleObstacle(anchor + new Float3(side * 2.4f, 0f, 1.2f), 3.7f);
                    default: return new ArenaCircleObstacle(anchor + new Float3(-side * 1.6f, 0f, 2.4f), 3.1f);
                }
            }

            // Deeply overlapping unequal lobes create one coherent solid shelf,
            // not a necklace of circular islands or a visual-only obstacle.
            switch (index)
            {
                case 0: return new ArenaCircleObstacle(anchor, 8.8f);
                case 1: return new ArenaCircleObstacle(anchor + new Float3(5f, 0f, 2f), 7.2f);
                case 2: return new ArenaCircleObstacle(anchor + new Float3(-3f, 0f, -4f), 6.4f);
                case 3: return new ArenaCircleObstacle(anchor, 8f);
                case 4: return new ArenaCircleObstacle(anchor + new Float3(3f, 0f, 5f), 7.1f);
                case 5: return new ArenaCircleObstacle(anchor + new Float3(-4f, 0f, -3f), 6.3f);
                case 6: return new ArenaCircleObstacle(anchor, 9.1f);
                case 7: return new ArenaCircleObstacle(anchor + new Float3(-5f, 0f, 2f), 7.2f);
                case 8: return new ArenaCircleObstacle(anchor + new Float3(4f, 0f, 5f), 7.6f);
                case 9: return new ArenaCircleObstacle(anchor, 9.5f);
                case 10: return new ArenaCircleObstacle(anchor + new Float3(-4f, 0f, 6f), 7.7f);
                case 11: return new ArenaCircleObstacle(anchor + new Float3(5f, 0f, -3f), 7.9f);
                case 12: return new ArenaCircleObstacle(anchor, 8.8f);
                case 13: return new ArenaCircleObstacle(anchor + new Float3(5f, 0f, -2f), 7.2f);
                case 14: return new ArenaCircleObstacle(anchor + new Float3(-3f, 0f, 5f), 7.4f);
                case 15: return new ArenaCircleObstacle(anchor, 8.3f);
                case 16: return new ArenaCircleObstacle(anchor + new Float3(-5f, 0f, -1f), 7.1f);
                default: return new ArenaCircleObstacle(anchor + new Float3(3f, 0f, 5f), 7.3f);
            }
        }

        private static void ValidateLandmarkIndex(int index)
        {
            if (index < 0 || index >= LandmarkCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static bool IsAtRefuge(Float3 position)
        {
            return !float.IsNaN(position.Y) && !float.IsInfinity(position.Y) &&
                (position - Refuge).PlanarLengthSquared <= RefugeRadius * RefugeRadius;
        }
    }
}
