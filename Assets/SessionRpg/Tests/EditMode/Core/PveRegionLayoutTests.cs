using System;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class PveRegionLayoutTests
    {
        private static readonly ArenaCollisionMap Map = ArenaCollisionMap.PveLevelingDefault;
        private const float Radius = ArenaLayout.CombatantCollisionRadius;

        [Test]
        public void Layout_HasItsOwnStableIdentityAndTwoDimensionalBounds()
        {
            Assert.That(PveRegionLayout.WorldIdValue, Is.EqualTo(1u));
            Assert.That(PveRegionLayout.ZoneIdValue, Is.EqualTo(2u));
            Assert.That(PveRegionLayout.MinimumX, Is.EqualTo(-58f));
            Assert.That(PveRegionLayout.MinimumZ, Is.EqualTo(-58f));
            Assert.That(PveRegionLayout.MaximumX, Is.EqualTo(122f));
            Assert.That(PveRegionLayout.MaximumZ, Is.EqualTo(122f));
            Assert.That(PveRegionLayout.Refuge, Is.EqualTo(new Float3(-46f, 0f, -42f)));
            Assert.That(PveRegionLayout.MajorObstacleCount, Is.EqualTo(6));
            Assert.That(PveRegionLayout.LandmarkCount, Is.EqualTo(13));
            Assert.That(Map.ObstacleCount, Is.EqualTo(39));
            Assert.That(CombatRules.PveLevelingDefault.CollisionMap.ObstacleCount, Is.EqualTo(39));
            Assert.That(CombatRules.LevelingDefault.CollisionMap.ObstacleCount, Is.Zero);
            Assert.That(CombatRules.ArenaDefault.CollisionMap.ObstacleCount, Is.EqualTo(2));
            Assert.That(CombatRules.HeroSiegeDefault.CollisionMap.ObstacleCount, Is.EqualTo(1));
            Assert.That(CombatRules.BossDefault.CollisionMap.IsEnabled, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => PveRegionLayout.GetCampCenter(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PveRegionLayout.GetCampCenter(7));
            Assert.Throws<ArgumentOutOfRangeException>(() => PveRegionLayout.GetObstacle(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PveRegionLayout.GetObstacle(39));
            Assert.Throws<ArgumentOutOfRangeException>(() => PveRegionLayout.GetLandmark(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PveRegionLayout.GetLandmark(13));
            Assert.Throws<ArgumentOutOfRangeException>(() => PveRegionLayout.GetLandmarkObstacleStart(13));
            Assert.Throws<ArgumentOutOfRangeException>(() => PveRegionLayout.GetLandmarkObstacleCount(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => PveRegionLayout.GetObstacleLandmarkIndex(39));
        }

        [Test]
        public void LandmarkComponents_AreAsymmetricSolidUnionsWithStableGroupingAndNoInternalPocket()
        {
            for (int landmarkIndex = 0; landmarkIndex < PveRegionLayout.LandmarkCount; landmarkIndex++)
            {
                ArenaCircleObstacle bound = PveRegionLayout.GetLandmark(landmarkIndex);
                int start = PveRegionLayout.GetLandmarkObstacleStart(landmarkIndex);
                int count = PveRegionLayout.GetLandmarkObstacleCount(landmarkIndex);
                Assert.That(start, Is.EqualTo(landmarkIndex * 3));
                Assert.That(count, Is.EqualTo(3));
                float primaryRadius = Map.GetObstacle(start).Radius;
                for (int offset = 0; offset < count; offset++)
                {
                    ArenaCircleObstacle component = Map.GetObstacle(start + offset);
                    Assert.That(PveRegionLayout.GetObstacleLandmarkIndex(start + offset), Is.EqualTo(landmarkIndex));
                    float anchorDistance = Float3.PlanarDistance(bound.Center, component.Center);
                    Assert.That(anchorDistance + component.Radius, Is.LessThanOrEqualTo(bound.Radius));
                    Assert.That(anchorDistance, Is.LessThan(component.Radius),
                        "Every convex disk contains the landmark anchor: their union is star-convex and has no sealed pocket.");
                    Assert.That(Map.IsPositionValid(component.Center, Radius), Is.False);
                    if (offset > 0)
                    {
                        Assert.That(component.Radius, Is.Not.EqualTo(primaryRadius));
                        Assert.That(anchorDistance + component.Radius, Is.GreaterThan(primaryRadius + 1f),
                            "Secondary lobes must actually reshape the footprint, not hide entirely within the original disk.");
                    }
                }
            }
        }

        [TestCase(0, 1, -16f, 5f)]
        [TestCase(1, 2, 57f, -40f)]
        [TestCase(2, 3, 29f, 27f)]
        [TestCase(3, 6, 101f, 71f)]
        [TestCase(4, 5, 78f, 45f)]
        [TestCase(5, 4, 95f, -12f)]
        public void MajorLandmarks_FrameCampRimsWithoutReplacingOpenCombatSpace(
            int landmarkIndex, int campIndex, float x, float z)
        {
            ArenaCircleObstacle landmark = PveRegionLayout.GetLandmark(landmarkIndex);
            Float3 camp = PveRegionLayout.GetCampCenter(campIndex);
            Assert.That(landmark.Center, Is.EqualTo(new Float3(x, 0f, z)));
            Assert.That(Float3.PlanarDistance(landmark.Center, camp), Is.InRange(19f, 27f),
                "Large shelves belong beside ordinary combat views, not in remote empty cells.");
        }

        [Test]
        public void EveryCamp_RetainsANineMeterUnobstructedCombatDisk()
        {
            for (int campIndex = 0; campIndex < PveRegionLayout.CampCount; campIndex++)
            {
                Float3 center = PveRegionLayout.GetCampCenter(campIndex);
                for (int componentIndex = 0; componentIndex < Map.ObstacleCount; componentIndex++)
                {
                    ArenaCircleObstacle component = Map.GetObstacle(componentIndex);
                    Assert.That(Float3.PlanarDistance(center, component.Center) - component.Radius,
                        Is.GreaterThanOrEqualTo(9f + Radius),
                        "Camp " + campIndex + " must retain a full actor-radius-clear combat disk.");
                }
            }
        }

        [Test]
        public void Refuge_IsARadialSafeAreaAndDoesNotAcceptNonfiniteCoordinates()
        {
            Assert.That(PveRegionLayout.IsAtRefuge(PveRegionLayout.Refuge), Is.True);
            for (int index = 0; index < 32; index++)
            {
                float angle = index * (float)Math.PI / 16f;
                Float3 direction = new Float3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle));
                Float3 inside = PveRegionLayout.Refuge + direction * 9.99f;
                Assert.That(PveRegionLayout.IsAtRefuge(inside), Is.True);
                Assert.That(Map.IsPositionValid(inside, Radius), Is.True);
                Assert.That(PveRegionLayout.IsAtRefuge(
                    PveRegionLayout.Refuge + direction * 10.01f), Is.False);
            }

            Assert.That(PveRegionLayout.IsAtRefuge(new Float3(float.NaN, 0f, 0f)), Is.False);
            Assert.That(PveRegionLayout.IsAtRefuge(new Float3(-46f, float.NaN, -42f)), Is.False);
            Assert.That(PveRegionLayout.IsAtRefuge(new Float3(-46f, 0f, float.PositiveInfinity)), Is.False);
        }

        [TestCase(-50f, -50f, 0f, 165f)]
        [TestCase(-50f, 115f, 165f, 0f)]
        [TestCase(115f, 115f, 0f, -165f)]
        [TestCase(115f, -50f, -165f, 0f)]
        public void CardinalRoutes_CrossTheRegionWithoutACorridorRestriction(
            float x, float z, float dx, float dz)
        {
            Float3 start = new Float3(x, 0f, z);
            Float3 delta = new Float3(dx, 0f, dz);
            Float3 moved = Map.ResolveMovement(start, delta, Radius);
            Assert.That(Map.IsPositionValid(start, Radius), Is.True);
            Assert.That(Map.IsPositionValid(moved, Radius), Is.True);
            Assert.That(Float3.PlanarDistance(moved, start + delta), Is.LessThan(0.001f));
            Assert.That(Map.HasLineOfSight(start, moved), Is.True);
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        public void EachLandmark_IsSolidBlocksSightAndHasACompleteWalkableLoop(int index)
        {
            ArenaCircleObstacle island = PveRegionLayout.GetLandmark(index);
            Assert.That(Map.IsPositionValid(island.Center, Radius), Is.False);
            Float3 left = island.Center + new Float3(-island.Radius - 2f, 0f, 0f);
            Float3 right = island.Center + new Float3(island.Radius + 2f, 0f, 0f);
            Assert.That(Map.HasLineOfSight(left, right), Is.False);
            Assert.That(Map.HasLineOfSight(right, left), Is.False);
            Assert.That(Map.TryGetFirstBlockingObstacle(left, right, out ArenaCircleObstacle hit), Is.True);
            Assert.That(Float3.PlanarDistance(hit.Center, island.Center), Is.LessThan(island.Radius));

            // The full loop proves that the obstacle is an island, not an
            // accidental lane divider or a one-way mandatory checkpoint.
            float loopRadius = island.Radius + 0.9f;
            Float3 current = island.Center + new Float3(loopRadius, 0f, 0f);
            Float3 start = current;
            for (int segment = 1; segment <= 32; segment++)
            {
                float angle = segment * (float)Math.PI / 16f;
                Float3 target = island.Center + new Float3(
                    (float)Math.Cos(angle) * loopRadius, 0f,
                    (float)Math.Sin(angle) * loopRadius);
                Assert.That(Map.HasLineOfSight(current, target), Is.True);
                current = Map.ResolveMovement(current, target - current, Radius);
                Assert.That(Map.IsPositionValid(current, Radius), Is.True);
                Assert.That(Float3.PlanarDistance(current, target), Is.LessThan(0.001f));
            }

            Assert.That(Float3.PlanarDistance(current, start), Is.LessThan(0.001f));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        [TestCase(6)]
        [TestCase(7)]
        [TestCase(8)]
        [TestCase(9)]
        [TestCase(10)]
        [TestCase(11)]
        [TestCase(12)]
        public void LandmarkCollision_StopsBlinkAndKnockbackAndResolvesEmbeddedPlacements(int index)
        {
            ArenaCircleObstacle island = PveRegionLayout.GetLandmark(index);
            Float3 start = island.Center + new Float3(-island.Radius - 3f, 0f, 0f);
            Float3 delta = new Float3((island.Radius + 3f) * 2f, 0f, 0f);
            Float3 blink = Map.ResolveBlink(start, start + delta, Radius);
            Float3 knockback = Map.ResolveKnockback(start, delta, Radius);
            Assert.That(Map.IsPositionValid(blink, Radius), Is.True);
            Assert.That(Map.IsPositionValid(knockback, Radius), Is.True);
            Assert.That(blink.X, Is.LessThan(island.Center.X));
            Assert.That(Float3.PlanarDistance(knockback, blink), Is.LessThan(0.0001f),
                "Blink reconstructs displacement from an absolute endpoint; the float add/subtract round-trip can differ by one ULP on Mono.");
            Assert.That(Map.ResolveKnockback(start, (start + delta) - start, Radius), Is.EqualTo(blink),
                "Identical effective displacements must use the exact same deterministic sweep for Blink and knockback.");
            for (int component = PveRegionLayout.GetLandmarkObstacleStart(index);
                component < PveRegionLayout.GetLandmarkObstacleStart(index) +
                    PveRegionLayout.GetLandmarkObstacleCount(index); component++)
            {
                for (int direction = 0; direction < 16; direction++)
                {
                    float angle = direction * (float)Math.PI / 8f;
                    Float3 placed = Map.ResolvePlacement(Map.GetObstacle(component).Center, Radius,
                        new Float3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle)));
                    Assert.That(Map.IsPositionValid(placed, Radius), Is.True,
                        "Embedded union placement must clear every component, regardless of preferred direction.");
                }
            }
            Assert.That(Map.TryResolveShadowstep(start, new Float3(-1f, 0f, 0f),
                4f, Radius, out Float3 shadowstep), Is.True);
            Assert.That(Map.IsPositionValid(shadowstep, Radius), Is.True);
        }

        [Test]
        public void OpenRegionGrid_HasNoSealedWalkablePocketsAndConnectsEveryCampAndRefuge()
        {
            const int side = 179;
            const float minimum = -57f;
            bool[] walkable = new bool[side * side];
            bool[] visited = new bool[walkable.Length];
            int[] queue = new int[walkable.Length];
            int validCount = 0;
            for (int z = 0; z < side; z++)
            {
                for (int x = 0; x < side; x++)
                {
                    int cell = z * side + x;
                    walkable[cell] = Map.IsPositionValid(new Float3(minimum + x, 0f, minimum + z), Radius);
                    if (walkable[cell]) validCount++;
                }
            }

            int start = (int)(PveRegionLayout.Refuge.Z - minimum) * side +
                (int)(PveRegionLayout.Refuge.X - minimum);
            Assert.That(walkable[start], Is.True);
            queue[0] = start;
            visited[start] = true;
            int head = 0;
            int tail = 1;
            while (head < tail)
            {
                int cell = queue[head++];
                int x = cell % side;
                int z = cell / side;
                Float3 origin = new Float3(minimum + x, 0f, minimum + z);
                for (int direction = 0; direction < 4; direction++)
                {
                    int nx = x + (direction == 0 ? -1 : direction == 1 ? 1 : 0);
                    int nz = z + (direction == 2 ? -1 : direction == 3 ? 1 : 0);
                    if (nx < 0 || nx >= side || nz < 0 || nz >= side) continue;
                    int next = nz * side + nx;
                    if (!walkable[next] || visited[next]) continue;
                    Float3 target = new Float3(minimum + nx, 0f, minimum + nz);
                    Float3 reached = Map.ResolveMovement(origin, target - origin, Radius);
                    if (Float3.PlanarDistance(reached, target) > 0.001f) continue;
                    visited[next] = true;
                    queue[tail++] = next;
                }
            }

            Assert.That(tail, Is.EqualTo(validCount),
                "Every 1m walkable sample must connect to refuge through swept, full-radius movement.");
            for (int camp = 0; camp < PveRegionLayout.CampCount; camp++)
            {
                Float3 center = PveRegionLayout.GetCampCenter(camp);
                int cell = (int)(center.Z - minimum) * side + (int)(center.X - minimum);
                Assert.That(visited[cell], Is.True, "Camp " + camp + " must remain reachable.");
                for (int heading = 0; heading < 4; heading++)
                {
                    float angle = heading * (float)Math.PI / 2f;
                    Float3 approach = center + new Float3((float)Math.Cos(angle) * 8f,
                        0f, (float)Math.Sin(angle) * 8f);
                    Assert.That(Map.IsPositionValid(approach, Radius), Is.True,
                        "All camps retain open combat space on all four sides.");
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void UnionDetours_ReachTheFarSideFromFourHeadingsWithoutGettingStuckBetweenLobes(bool clockwise)
        {
            for (int landmark = 0; landmark < PveRegionLayout.LandmarkCount; landmark++)
            {
                ArenaCircleObstacle bound = PveRegionLayout.GetLandmark(landmark);
                for (int heading = 0; heading < 4; heading++)
                {
                    float angle = heading * (float)Math.PI / 2f;
                    Float3 offset = new Float3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle)) * (bound.Radius + 0.9f);
                    Float3 current = bound.Center + offset;
                    Float3 target = bound.Center - offset;
                    for (int tick = 0; tick < 1200 && Float3.PlanarDistance(current, target) > 0.5f; tick++)
                    {
                        Float3 direction = Map.BuildDetourDirection(current, target, clockwise);
                        current = Map.ResolveMovement(current, direction * 0.18f, Radius);
                        Assert.That(Map.IsPositionValid(current, Radius), Is.True);
                    }

                    Assert.That(Float3.PlanarDistance(current, target), Is.LessThanOrEqualTo(0.5f),
                        "Landmark " + landmark + ", heading " + heading + ", clockwise " + clockwise);
                }
            }
        }

        [Test]
        public void Bounds_StopDisplacementAtFirstContactWhileMovementSlidesAlongTheEdge()
        {
            Float3 start = new Float3(115f, 0f, 115f);
            Float3 delta = new Float3(20f, 0f, 8f);
            Float3 movement = Map.ResolveMovement(start, delta, Radius);
            Float3 blink = Map.ResolveBlink(start, start + delta, Radius);
            Float3 knockback = Map.ResolveKnockback(start, delta, Radius);
            Assert.That(Map.IsPositionValid(movement, Radius), Is.True);
            Assert.That(Map.IsPositionValid(blink, Radius), Is.True);
            Assert.That(knockback, Is.EqualTo(blink));
            Assert.That(movement.X, Is.EqualTo(PveRegionLayout.MaximumX - Radius).Within(0.01f));
            Assert.That(movement.Z, Is.GreaterThan(blink.Z + 1f));
            Assert.That(blink.Z, Is.LessThan(PveRegionLayout.MaximumZ - 1f));

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Float3 placed = Map.ResolvePlacement(new Float3(x * 1000f, 0f, z * 1000f),
                        Radius, default);
                    Assert.That(Map.IsPositionValid(placed, Radius), Is.True);
                    Assert.That(placed.X, Is.InRange(PveRegionLayout.MinimumX, PveRegionLayout.MaximumX));
                    Assert.That(placed.Z, Is.InRange(PveRegionLayout.MinimumZ, PveRegionLayout.MaximumZ));
                }
            }
        }

        [Test]
        public void Simulation_UsesTheSameIslandForUnitSpellLosAndBlink()
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.PveLevelingDefault);
            ParticipantId player = new ParticipantId(1);
            ParticipantId target = new ParticipantId(2);
            ArenaCircleObstacle island = PveRegionLayout.GetLandmark(PveRegionLayout.MajorObstacleCount);
            Float3 left = island.Center + new Float3(-7.5f, 0f, 0f);
            Float3 right = island.Center + new Float3(7.5f, 0f, 0f);
            Float3 facing = new Float3(1f, 0f, 0f);
            Assert.That(simulation.AddCombatant(player, new TeamId(1), ClassId.Mage,
                SpecializationId.Frost, left), Is.True);
            Assert.That(simulation.AddCombatant(target, new TeamId(2), ClassId.Mage,
                SpecializationId.Fire, right), Is.True);
            CombatCommand cast = CombatCommand.Cast(player, 1u, default, facing,
                AbilityId.Frostbolt, target, default, facing);
            Assert.That(simulation.CanStartAbility(cast, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("line_of_sight"));

            CombatCommand blink = CombatCommand.Cast(player, 2u, facing, facing,
                AbilityId.Blink, default, right, facing);
            Assert.That(simulation.SubmitCommand(blink), Is.True);
            simulation.Tick();
            Assert.That(simulation.TryGetSnapshot(player, out CombatantSnapshot after), Is.True);
            Assert.That(after.Position.X, Is.LessThan(island.Center.X - 4.2f));
            Assert.That(after.Position.X, Is.GreaterThan(left.X));
            Assert.That(Map.IsPositionValid(after.Position, Radius), Is.True);
        }

        [Test]
        public void WarmCollisionAndLayoutQueries_AllocateNoManagedMemory()
        {
            Float3 result = default;
            for (int index = 0; index < 100; index++)
            {
                result = ExerciseGeometry(index);
            }

            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 10000; index++)
            {
                result = ExerciseGeometry(index);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(Map.IsPositionValid(result, Radius), Is.True);
        }

        private static Float3 ExerciseGeometry(int index)
        {
            ArenaCircleObstacle island = Map.GetObstacle(index % PveRegionLayout.ObstacleCount);
            Float3 start = island.Center + new Float3(-island.Radius - 3f, 0f, 0f);
            Float3 target = island.Center + new Float3(island.Radius + 3f, 0f, 0f);
            Map.HasLineOfSight(start, target);
            Map.BuildDetourDirection(start, target, (index & 1) == 0);
            Map.ResolveMovement(start, target - start + new Float3(0f, 0f, 4f), Radius);
            Map.ResolveBlink(start, target, Radius);
            Map.ResolveKnockback(start, target - start, Radius);
            PveRegionLayout.IsAtRefuge(PveRegionLayout.GetCampCenter(index % PveRegionLayout.CampCount));
            int landmark = PveRegionLayout.GetObstacleLandmarkIndex(index % PveRegionLayout.ObstacleCount);
            PveRegionLayout.GetLandmarkObstacleCount(landmark);
            PveRegionLayout.GetLandmarkObstacleStart(landmark);
            PveRegionLayout.GetLandmark(landmark);
            return Map.ResolvePlacement(island.Center, Radius, new Float3(1f, 0f, 0f));
        }
    }
}
