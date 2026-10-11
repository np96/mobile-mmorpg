using System;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class GrindingMobBrainTests
    {
        private static readonly ParticipantId Player = new ParticipantId(1);
        private static readonly ParticipantId Mob = new ParticipantId(2);
        private static readonly Float3 Home = PveRegionLayout.GetCampCenter(0);

        [Test]
        public void Reset_PreparesExistingBrainForANewSpawnWithoutAllocation()
        {
            CombatSimulation simulation = CreateEncounter(Home, Home + new Float3(8f, 0f, 0f));
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.AshRanged);
            simulation.TryGetSnapshot(Mob, out CombatantSnapshot self);
            simulation.TryGetSnapshot(Player, out CombatantSnapshot player);
            Assert.That(brain.TryBuildCommand(simulation, self, player, out CombatCommand first), Is.True);
            Assert.That(first.Sequence, Is.EqualTo(1u));
            Assert.That(brain.TryBuildCommand(simulation, self, player, out _), Is.False);
            brain.Reset();
            Assert.That(brain.TryBuildCommand(simulation, self, player, out CombatCommand reset), Is.True);
            Assert.That(reset.Sequence, Is.EqualTo(1u));
            Assert.That(reset.Ability, Is.EqualTo(first.Ability));
        }

        [Test]
        public void Leash_ReturnsToHomeAndDoesNotReacquireHalfwayThroughEvade()
        {
            CombatSimulation simulation = CreateEncounter(
                Home + new Float3(12f, 0f, 0f), Home + new Float3(46f, 0f, 0f));
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.AshRanged);
            simulation.TryGetSnapshot(Mob, out CombatantSnapshot self);
            simulation.TryGetSnapshot(Player, out CombatantSnapshot player);
            Assert.That(brain.TryBuildLeashedCommand(simulation, self, player,
                Home, 45f, out CombatCommand leaving), Is.True);
            Assert.That(leaving.Ability, Is.EqualTo(AbilityId.None));
            Assert.That(leaving.Movement.X, Is.LessThan(0f));
            simulation.SubmitCommand(leaving);
            simulation.Tick();
            simulation.TryGetSnapshot(Mob, out self);
            Assert.That(self.Position.X, Is.LessThan(Home.X + 12f));

            CombatantSnapshot returningPlayer = At(player, Home + new Float3(15f, 0f, 0f));
            Assert.That(brain.TryBuildLeashedCommand(simulation, self, returningPlayer,
                Home, 45f, out CombatCommand stillReturning), Is.True);
            Assert.That(stillReturning.Ability, Is.EqualTo(AbilityId.None));
            Assert.That(stillReturning.Movement.X, Is.LessThan(0f));
        }

        [Test]
        public void Leash_AtHomeRestoresHealthThroughCoreAndStopsMovement()
        {
            CombatSimulation simulation = CreateEncounter(Home, Home + new Float3(6f, 0f, 0f));
            simulation.SubmitCommand(CombatCommand.Cast(Player, 1u, default,
                new Float3(-1f, 0f, 0f), AbilityId.FireBlast, Mob,
                Home, new Float3(-1f, 0f, 0f)));
            simulation.Tick();
            simulation.TryGetSnapshot(Mob, out CombatantSnapshot self);
            simulation.TryGetSnapshot(Player, out CombatantSnapshot player);
            Assert.That(self.Health, Is.LessThan(self.MaxHealth));
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.AshRanged);
            CombatantSnapshot distantPlayer = At(player, Home + new Float3(46f, 0f, 0f));
            Assert.That(brain.TryBuildLeashedCommand(simulation, self, distantPlayer,
                Home, 45f, out CombatCommand stop), Is.True);
            Assert.That(stop.Movement.PlanarLengthSquared, Is.Zero);
            simulation.TryGetSnapshot(Mob, out CombatantSnapshot restored);
            Assert.That(restored.Health, Is.EqualTo(restored.MaxHealth));
        }

        [Test]
        public void Leash_DoesNotUseConcealedPlayerPositionToChangeDirection()
        {
            CombatSimulation simulation = CreateEncounter(
                Home + new Float3(10f, 0f, 0f), Home + new Float3(46f, 0f, 0f));
            simulation.TryGetSnapshot(Mob, out CombatantSnapshot self);
            simulation.TryGetSnapshot(Player, out CombatantSnapshot player);
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.AshRanged);
            CombatantSnapshot hidden = At(player, player.Position, AuraTag.Stealth);
            Assert.That(brain.TryBuildLeashedCommand(simulation, self, hidden,
                Home, 45f, out CombatCommand command), Is.True);
            Assert.That(command.Movement.PlanarLengthSquared, Is.Zero);
            Assert.That(command.Facing, Is.EqualTo(self.Facing));
            Assert.That(command.Ability, Is.EqualTo(AbilityId.None));
        }

        [Test]
        public void Leash_RejectsNonfiniteHomeOrRadius()
        {
            CombatSimulation simulation = CreateEncounter(Home, Home + new Float3(8f, 0f, 0f));
            simulation.TryGetSnapshot(Mob, out CombatantSnapshot self);
            simulation.TryGetSnapshot(Player, out CombatantSnapshot player);
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.AshRanged);
            Assert.That(brain.TryBuildLeashedCommand(simulation, self, player,
                Home, float.NaN, out _), Is.False);
            Assert.That(brain.TryBuildLeashedCommand(simulation, self, player,
                new Float3(float.NaN, 0f, 32f), 45f, out _), Is.False);
        }

        [Test]
        public void PveCover_InsidePreferredRangeMovesAroundTheIslandUntilSightIsRecovered()
        {
            ArenaCircleObstacle island = PveRegionLayout.GetLandmark(PveRegionLayout.MajorObstacleCount);
            Float3 origin = island.Center + new Float3(-4f, 0f, -3f);
            Float3 target = island.Center + new Float3(4f, 0f, -3f);
            CombatSimulation simulation = CreateEncounter(origin, target);
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(LevelingMobArchetypeId.AshRanged);
            Assert.That(Float3.PlanarDistance(origin, target), Is.LessThanOrEqualTo(
                LevelingMobArchetypeCatalog.Get(LevelingMobArchetypeId.AshRanged).PreferredRange));
            simulation.TryGetSnapshot(Mob, out CombatantSnapshot self);
            simulation.TryGetSnapshot(Player, out CombatantSnapshot player);
            Assert.That(brain.TryBuildCommand(simulation, self, player, out CombatCommand first), Is.True);
            Assert.That(first.Ability, Is.EqualTo(AbilityId.None));
            Assert.That(Math.Abs(first.Movement.Z), Is.GreaterThan(0.5f));
            simulation.SubmitCommand(first);

            bool regainedSight = false;
            for (int tick = 0; tick < 300; tick++)
            {
                simulation.Tick();
                simulation.TryGetSnapshot(Mob, out self);
                simulation.TryGetSnapshot(Player, out player);
                Assert.That(simulation.Rules.CollisionMap.IsPositionValid(self.Position,
                    ArenaLayout.CombatantCollisionRadius), Is.True);
                if (simulation.Rules.CollisionMap.HasLineOfSight(self.Position, player.Position))
                {
                    regainedSight = true;
                    break;
                }

                if (brain.TryBuildCommand(simulation, self, player, out CombatCommand move))
                {
                    simulation.SubmitCommand(move);
                }
            }

            Assert.That(regainedSight, Is.True, "The ranged mob must not remain idle behind cover.");
        }

        [Test]
        public void PveEvade_NavigatesAroundAnIslandAndReachesItsAuthoredHome()
        {
            Float3 anchor = PveRegionLayout.GetLandmark(0).Center;
            Float3 origin = anchor + new Float3(-14f, 0f, 0f);
            Float3 home = anchor + new Float3(14f, 0f, 0f);
            CombatSimulation simulation = CreateEncounter(origin, anchor + new Float3(64f, 0f, 0f));
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(LevelingMobArchetypeId.AshRanged);
            bool reachedHome = false;
            for (int tick = 0; tick < 600; tick++)
            {
                simulation.TryGetSnapshot(Mob, out CombatantSnapshot self);
                simulation.TryGetSnapshot(Player, out CombatantSnapshot player);
                Assert.That(brain.TryBuildLeashedCommand(simulation, self, player,
                    home, 45f, out CombatCommand command), Is.True);
                if (tick == 0)
                {
                    Assert.That(Math.Abs(command.Movement.Z), Is.GreaterThan(0.5f));
                }

                if (Float3.PlanarDistance(self.Position, home) <= 1f)
                {
                    reachedHome = true;
                    Assert.That(command.Movement.PlanarLengthSquared, Is.Zero);
                    break;
                }

                simulation.SubmitCommand(command);
                simulation.Tick();
                simulation.TryGetSnapshot(Mob, out CombatantSnapshot after);
                Assert.That(simulation.Rules.CollisionMap.IsPositionValid(after.Position,
                    ArenaLayout.CombatantCollisionRadius), Is.True);
            }

            Assert.That(reachedHome, Is.True, "Evading NPCs must not push indefinitely into cover.");
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
        public void PveEvade_CrossesEachCompoundLandmarkAndStopsAtHome(int landmarkIndex)
        {
            ArenaCircleObstacle bound = PveRegionLayout.GetLandmark(landmarkIndex);
            Float3 home = bound.Center + new Float3(bound.Radius + 0.9f, 0f, 0f);
            Float3 origin = bound.Center - new Float3(bound.Radius + 0.9f, 0f, 0f);
            CombatSimulation simulation = CreateEncounter(origin, PveRegionLayout.Refuge);
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(LevelingMobArchetypeId.AshRanged);
            simulation.TryGetSnapshot(Player, out CombatantSnapshot player);
            CombatantSnapshot farPlayer = At(player, home + new Float3(1000f, 0f, 0f));
            bool reachedHome = false;
            for (int tick = 0; tick < 1200; tick++)
            {
                simulation.TryGetSnapshot(Mob, out CombatantSnapshot self);
                Assert.That(simulation.Rules.CollisionMap.IsPositionValid(self.Position,
                    ArenaLayout.CombatantCollisionRadius), Is.True);
                Assert.That(brain.TryBuildLeashedCommand(simulation, self, farPlayer,
                    home, 45f, out CombatCommand command), Is.True);
                if (Float3.PlanarDistance(self.Position, home) <= 1f)
                {
                    Assert.That(command.Ability, Is.EqualTo(AbilityId.None));
                    Assert.That(command.Movement.PlanarLengthSquared, Is.Zero);
                    reachedHome = true;
                    break;
                }

                Assert.That(simulation.SubmitCommand(command), Is.True);
                simulation.Tick();
            }

            Assert.That(reachedHome, Is.True, "Compound shelf/grove " + landmarkIndex + " must not trap an evading NPC.");
        }

        [Test]
        public void CoverDetour_DoesNotAlterTheLegacyNonPveBrainPolicy()
        {
            ArenaCircleObstacle island = PveRegionLayout.GetLandmark(PveRegionLayout.MajorObstacleCount);
            CombatRules rules = new CombatRules(30, 5.4f, 32f, 38f, 0f, 1.5f, 15f, 5,
                ArenaCollisionMap.PveLevelingDefault, false);
            CombatSimulation simulation = new CombatSimulation(rules);
            simulation.AddCombatant(Mob, new TeamId(2), ClassId.Hunter,
                SpecializationId.Marksmanship, island.Center + new Float3(-4f, 0f, -3f), 100f, false);
            simulation.AddCombatant(Player, new TeamId(1), ClassId.Mage,
                SpecializationId.Fire, island.Center + new Float3(4f, 0f, -3f));
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(LevelingMobArchetypeId.AshRanged);
            simulation.TryGetSnapshot(Mob, out CombatantSnapshot self);
            simulation.TryGetSnapshot(Player, out CombatantSnapshot player);
            Assert.That(brain.TryBuildCommand(simulation, self, player, out CombatCommand command), Is.True);
            Assert.That(command.Movement.PlanarLengthSquared, Is.Zero);
        }

        [Test]
        public void WarmPveDetourAndEvadeDecisions_DoNotAllocate()
        {
            ArenaCircleObstacle island = PveRegionLayout.GetLandmark(PveRegionLayout.MajorObstacleCount);
            CombatSimulation simulation = CreateEncounter(
                island.Center + new Float3(-4f, 0f, -3f),
                island.Center + new Float3(4f, 0f, -3f));
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(LevelingMobArchetypeId.AshRanged);
            simulation.TryGetSnapshot(Mob, out CombatantSnapshot self);
            simulation.TryGetSnapshot(Player, out CombatantSnapshot player);
            Float3 home = island.Center + new Float3(4f, 0f, -3f);
            CombatantSnapshot farPlayer = At(player, home + new Float3(46f, 0f, 0f));
            for (int index = 0; index < 100; index++)
            {
                brain.Reset();
                brain.TryBuildCommand(simulation, self, player, out _);
                brain.TryBuildLeashedCommand(simulation, self, farPlayer, home, 45f, out _);
            }

            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 1000; index++)
            {
                brain.Reset();
                brain.TryBuildCommand(simulation, self, player, out _);
                brain.TryBuildLeashedCommand(simulation, self, farPlayer, home, 45f, out _);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
        }

        private static CombatSimulation CreateEncounter(Float3 mobPosition, Float3 playerPosition)
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.PveLevelingDefault);
            Assert.That(simulation.Rules.CollisionMap.IsPositionValid(mobPosition,
                ArenaLayout.CombatantCollisionRadius), Is.True, "Fixture mob must start outside every union component.");
            Assert.That(simulation.Rules.CollisionMap.IsPositionValid(playerPosition,
                ArenaLayout.CombatantCollisionRadius), Is.True, "Fixture player must start outside every union component.");
            Assert.That(simulation.AddCombatant(Player, new TeamId(1), ClassId.Mage,
                SpecializationId.Fire, playerPosition, 100f), Is.True);
            Assert.That(simulation.AddCombatant(Mob, new TeamId(2), ClassId.Hunter,
                SpecializationId.Marksmanship, mobPosition, 100f, false), Is.True);
            return simulation;
        }

        private static CombatantSnapshot At(
            CombatantSnapshot source, Float3 position, AuraTag tags = AuraTag.None)
        {
            return new CombatantSnapshot(source.Id, source.Team, source.Class,
                source.Specialization, position, source.Facing, source.Health,
                source.MaxHealth, source.Barrier, source.CastingAbility,
                source.CastStartTick, source.CastEndTick, source.CastingActivationType,
                source.CastingSchool, source.ResourceType, source.Resource,
                source.MaxResource, source.ComboPoints, source.ComboTarget,
                source.GlobalCooldownEndTick, source.LockedSchools, source.IsAlive,
                source.IsInLava, source.ActiveStatuses, tags);
        }
    }
}
