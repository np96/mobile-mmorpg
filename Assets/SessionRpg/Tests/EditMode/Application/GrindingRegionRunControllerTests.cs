using System;
using System.Collections.Generic;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class GrindingRegionRunControllerTests
    {
        [Test]
        public void Region_DefinesSevenSpatialCampsAndEightEnemyProfiles()
        {
            int[] levels = { 1, 2, 3, 4, 5, 7, 9 };
            HashSet<LevelingMobArchetypeId> archetypes = new HashSet<LevelingMobArchetypeId>();
            Assert.That(GrindingRegionRunController.CampCount, Is.EqualTo(7));
            for (int index = 0; index < GrindingRegionRunController.CampCount; index++)
            {
                GrindingCampDefinition camp = GrindingRegionRunController.GetCampDefinition(index);
                Assert.That(camp.Id, Is.EqualTo(index));
                Assert.That(camp.Index, Is.EqualTo(index));
                Assert.That(camp.Level, Is.EqualTo(levels[index]));
                Assert.That(camp.DisplayName, Is.Not.Empty);
                Assert.That(camp.RoleHint, Is.Not.Empty);
                Assert.That(camp.MobCount, Is.InRange(1, 3));
                Assert.That(ArenaCollisionMap.PveLevelingDefault.IsPositionValid(
                    camp.Center + new Float3(-14f, 0f, 0f),
                    ArenaLayout.CombatantCollisionRadius), Is.True,
                    "The capture/approach position must remain clear of landmark islands.");
                for (int mobIndex = 0; mobIndex < camp.MobCount; mobIndex++)
                {
                    GrindingMobDefinition mob = camp.GetMob(mobIndex);
                    Assert.That(mob.Participant.IsValid, Is.False,
                        "Preview camp definitions cannot assign combat identities.");
                    Assert.That(mob.Level, Is.EqualTo(camp.Level));
                    Assert.That(mob.BaseMaximumHealth, Is.GreaterThan(0f));
                    Assert.That(mob.ExperienceReward, Is.GreaterThan(0));
                    Assert.That(camp.Center, Is.EqualTo(PveRegionLayout.GetCampCenter(index)));
                    Assert.That(ArenaCollisionMap.PveLevelingDefault.IsPositionValid(
                        mob.Position, ArenaLayout.CombatantCollisionRadius), Is.True);
                    Assert.That(LevelingMobArchetypeCatalog.TryGet(mob.Archetype, out _), Is.True);
                    archetypes.Add(mob.Archetype);
                }
            }

            Assert.That(archetypes.Count, Is.EqualTo(8));
        }

        [Test]
        public void Region_CampsBranchAcrossBothAxesAndRefugeCannotActivateAnyCamp()
        {
            GrindingRegionRunController run = new GrindingRegionRunController();
            Float3 first = GrindingRegionRunController.GetCampDefinition(0).Center;
            Float3 second = GrindingRegionRunController.GetCampDefinition(1).Center;
            Float3 third = GrindingRegionRunController.GetCampDefinition(2).Center;
            float cross = (second.X - first.X) * (third.Z - first.Z) -
                (second.Z - first.Z) * (third.X - first.X);
            Assert.That(Math.Abs(cross), Is.GreaterThan(1000f));
            Assert.That(second.X, Is.LessThan(first.X));
            Assert.That(second.Z, Is.GreaterThan(first.Z + 40f));
            for (int campIndex = 0; campIndex < GrindingRegionRunController.CampCount; campIndex++)
            {
                Float3 center = GrindingRegionRunController.GetCampDefinition(campIndex).Center;
                Assert.That(Float3.PlanarDistance(center, PveRegionLayout.Refuge),
                    Is.GreaterThan(PveRegionLayout.RefugeRadius + GrindingRegionRunController.ActivationRadius));
                for (int other = campIndex + 1; other < GrindingRegionRunController.CampCount; other++)
                {
                    Assert.That(Float3.PlanarDistance(center,
                        GrindingRegionRunController.GetCampDefinition(other).Center),
                        Is.GreaterThan(GrindingRegionRunController.ActivationRadius * 2f),
                        "Activation circles must not ambiguously overlap.");
                }
            }

            for (int direction = 0; direction < 32; direction++)
            {
                float angle = direction * (float)Math.PI / 16f;
                Float3 edge = PveRegionLayout.Refuge + new Float3(
                    (float)Math.Cos(angle) * PveRegionLayout.RefugeRadius, 0f,
                    (float)Math.Sin(angle) * PveRegionLayout.RefugeRadius);
                Assert.That(run.TryActivateCamp(direction, edge, out _), Is.False);
            }

            Assert.That(run.ActiveCampIndex, Is.EqualTo(-1));
            Assert.That(run.Kills, Is.Zero);
        }

        [Test]
        public void Activation_UsesTwoDimensionalProximityAndOnlyOneActiveCamp()
        {
            GrindingRegionRunController run = new GrindingRegionRunController();
            GrindingCampDefinition preview = GrindingRegionRunController.GetCampDefinition(0);
            Assert.That(run.TryActivateCamp(0, preview.Center + new Float3(0f, 0f, 23f), out _), Is.False);
            Assert.That(run.TryActivateCamp(-1, preview.Center, out _), Is.False);
            Assert.That(run.TryActivateCamp(0, new Float3(float.NaN, 0f, 32f), out _), Is.False);
            Assert.That(run.TryActivateCamp(10, preview.Center, out GrindingCampDefinition live), Is.True);
            Assert.That(live.GetMob(0).Participant, Is.EqualTo(new ParticipantId(2)));
            Assert.That(run.ActiveMobCount, Is.EqualTo(2));
            Assert.That(run.TryActivateCamp(11,
                GrindingRegionRunController.GetCampDefinition(1).Center, out _), Is.False);
            Assert.That(run.IsWithinActiveCampLeash(preview.Center + new Float3(45f, 0f, 0f)), Is.True);
            Assert.That(run.IsWithinActiveCampLeash(preview.Center + new Float3(46f, 0f, 0f)), Is.False);
        }

        [Test]
        public void DeathAwards_PerActiveMobOnceAndNeverForForeignOrStaleDeaths()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            GrindingMobDefinition first = camp.GetMob(0);
            Assert.That(run.ObserveCombatEvent(Death(-1, run.Player, first.Participant)).WasHandled, Is.False);
            Assert.That(run.ObserveCombatEvent(Death(1, run.Player, new ParticipantId(99))).WasHandled, Is.False);
            GrindingCombatObservation observation = run.ObserveCombatEvent(Death(1, run.Player, first.Participant));
            Assert.That(observation.ExperienceAwarded, Is.EqualTo(first.ExperienceReward));
            Assert.That(run.Kills, Is.EqualTo(1));
            Assert.That(run.ObserveCombatEvent(Death(1, run.Player, first.Participant)).WasHandled, Is.False);
            Assert.That(run.TotalExperience, Is.EqualTo(first.ExperienceReward));
            GrindingCombatObservation foreign = run.ObserveCombatEvent(
                Death(2, new ParticipantId(77), camp.GetMob(1).Participant));
            Assert.That(foreign.WasHandled, Is.True);
            Assert.That(foreign.ExperienceAwarded, Is.Zero);
            Assert.That(foreign.CampCleared, Is.True);
            Assert.That(run.Kills, Is.EqualTo(1));
        }

        [Test]
        public void Activation_RejectsDeathsBeforeCurrentActivationTick()
        {
            GrindingRegionRunController run = new GrindingRegionRunController();
            run.TryActivateCamp(100, GrindingRegionRunController.GetCampDefinition(0).Center,
                out GrindingCampDefinition camp);
            Assert.That(run.ObserveCombatEvent(Death(99, run.Player,
                camp.GetMob(0).Participant)).WasHandled, Is.False);
            Assert.That(run.ActiveMobCount, Is.EqualTo(2));
        }

        [Test]
        public void Respawn_RequiresTwentySecondsAndExitBeforeReentry()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition first);
            Clear(run, 1);
            int ready = run.RespawnAtTick;
            Assert.That(ready, Is.EqualTo(GrindingRegionRunController.RespawnDelayTicks + 1));
            Assert.That(run.TryActivateCamp(ready, first.Center, out _), Is.False,
                "Waiting on the corpses must never respawn on top of the player.");
            Float3 outside = first.Center + new Float3(-27f, 0f, 0f);
            run.TryActivateCamp(ready - 1, outside, out _);
            Assert.That(run.TryActivateCamp(ready - 1, first.Center, out _), Is.False);
            Assert.That(run.TryActivateCamp(ready, first.Center, out GrindingCampDefinition second), Is.True);
            Assert.That(second.GetMob(0).Participant.Value,
                Is.GreaterThan(first.GetMob(first.MobCount - 1).Participant.Value));
            Assert.That(run.ObserveCombatEvent(Death(ready + 1, run.Player,
                first.GetMob(0).Participant)).WasHandled, Is.False);
        }

        [Test]
        public void ClearedCamp_DoesNotBlockOtherUnclearedCamps()
        {
            GrindingRegionRunController run = Activate(0, out _);
            Clear(run, 1);
            Assert.That(run.TryActivateCamp(2,
                GrindingRegionRunController.GetCampDefinition(1).Center,
                out GrindingCampDefinition second), Is.True);
            Assert.That(second.Index, Is.EqualTo(1));
        }

        [Test]
        public void AbandonCamp_RequiresLeavingLeashAwardsNothingAndAllowsFreeTravel()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition first);
            run.ObserveCombatEvent(Death(1, run.Player, first.GetMob(0).Participant));
            int earned = run.TotalExperience;
            Assert.That(run.TryAbandonCamp(2, first.Center,
                out _), Is.False);
            Assert.That(run.TryAbandonCamp(2, first.Center + new Float3(45f, 0f, 0f),
                out _), Is.False);
            Float3 next = GrindingRegionRunController.GetCampDefinition(1).Center;
            Assert.That(run.TryAbandonCamp(2, next,
                out GrindingCampDefinition abandoned), Is.True);
            Assert.That(abandoned.Index, Is.EqualTo(first.Index));
            Assert.That(abandoned.GetMob(1).Participant, Is.EqualTo(first.GetMob(1).Participant));
            Assert.That(run.ActiveMobCount, Is.Zero);
            Assert.That(run.TotalExperience, Is.EqualTo(earned));
            Assert.That(run.Kills, Is.EqualTo(1));
            Assert.That(run.RespawnAtTick, Is.EqualTo(602));
            Assert.That(run.ObserveCombatEvent(Death(3, run.Player,
                first.GetMob(1).Participant)).WasHandled, Is.False);
            Assert.That(run.TryActivateCamp(3, next,
                out GrindingCampDefinition second), Is.True);
            Assert.That(second.Index, Is.EqualTo(1));
        }

        [Test]
        public void SnapshotAbandon_RetainsLivingChaserAfterPlayerCrossesCampLeash()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            Float3 playerPosition = camp.Center + new Float3(46f, 0f, 0f);
            CombatSimulation simulation = CreateRetirementSimulation(camp, playerPosition,
                playerPosition + new Float3(-2f, 0f, 0f), camp.GetMob(1).Position);
            Assert.That(run.IsWithinActiveCampLeash(playerPosition), Is.False);
            Assert.That(run.TryAbandonCamp(simulation, out _), Is.False);
            Assert.That(run.ActiveMobCount, Is.EqualTo(camp.MobCount));
            Assert.That(run.GetActiveMobId(0), Is.EqualTo(camp.GetMob(0).Participant));
            Assert.That(run.RespawnAtTick, Is.Zero);
            Assert.That(run.TotalExperience, Is.Zero);
        }

        [TestCase(60f, false)]
        [TestCase(60.01f, true)]
        public void SnapshotAbandon_IncludesExactRetentionBoundary(float distance, bool expectedRetirement)
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            Float3 playerPosition = camp.Center + new Float3(46f, 0f, 0f);
            Float3 firstPosition = playerPosition + new Float3(-distance, 0f, 0f);
            CombatSimulation simulation = CreateRetirementSimulation(camp, playerPosition,
                firstPosition, firstPosition + new Float3(-1f, 0f, 0f));
            Assert.That(run.TryAbandonCamp(simulation, out _), Is.EqualTo(expectedRetirement));
        }

        [Test]
        public void SnapshotAbandon_AllLivingMobsFarRetiresWithoutRewardsAndKeepsUniqueIds()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            Float3 nextCamp = PveRegionLayout.GetCampCenter(6);
            CombatSimulation simulation = CreateRetirementSimulation(camp, nextCamp,
                camp.GetMob(0).Position, camp.GetMob(1).Position);
            Assert.That(run.TryAbandonCamp(simulation, out GrindingCampDefinition abandoned), Is.True);
            Assert.That(abandoned.GetMob(0).Participant, Is.EqualTo(camp.GetMob(0).Participant));
            Assert.That(run.ActiveMobCount, Is.Zero);
            Assert.That(run.TotalExperience, Is.Zero);
            Assert.That(run.Kills, Is.Zero);
            Assert.That(run.RespawnAtTick, Is.EqualTo(GrindingRegionRunController.RespawnDelayTicks));
            Assert.That(run.TryActivateCamp(1, nextCamp, out GrindingCampDefinition replacement), Is.True);
            Assert.That(replacement.GetMob(0).Participant.Value,
                Is.GreaterThan(camp.GetMob(camp.MobCount - 1).Participant.Value));
            Assert.That(run.ObserveCombatEvent(Death(1, run.Player,
                camp.GetMob(0).Participant)).WasHandled, Is.False);
        }

        [Test]
        public void SnapshotAbandon_UsesDistanceToSurvivorRatherThanOnlyItsCamp()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            Float3 nextCamp = PveRegionLayout.GetCampCenter(6);
            CombatSimulation simulation = CreateRetirementSimulation(camp, nextCamp,
                nextCamp + new Float3(-2f, 0f, 0f), camp.GetMob(1).Position);
            Assert.That(Float3.PlanarDistance(nextCamp, camp.Center),
                Is.GreaterThan(GrindingRegionRunController.LiveMobRetentionRadius));
            Assert.That(run.TryAbandonCamp(simulation, out _), Is.False);
            Assert.That(run.TryActivateCamp(1, nextCamp, out _), Is.False,
                "Retaining a chaser cannot activate another camp or exceed the existing population cap.");
            Assert.That(run.ActiveMobCount, Is.LessThanOrEqualTo(GrindingRegionRunController.MaximumActiveMobCount));
        }

        [Test]
        public void SnapshotAbandon_ReturningMobStaysAliveWhenPlayerFollowsInsideLeash()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            Float3 playerPosition = camp.Center + new Float3(46f, 0f, 0f);
            CombatSimulation simulation = CreateRetirementSimulation(camp, playerPosition,
                playerPosition + new Float3(-2f, 0f, 0f), camp.GetMob(1).Position);
            DeterministicLevelingMobBrain brain = new DeterministicLevelingMobBrain(camp.GetMob(0).Archetype);
            Float3 west = new Float3(-1f, 0f, 0f);
            for (uint tick = 1u; tick <= 40u; tick++)
            {
                Assert.That(run.TryAbandonCamp(simulation, out _), Is.False);
                simulation.TryGetSnapshot(run.Player, out CombatantSnapshot player);
                simulation.TryGetSnapshot(camp.GetMob(0).Participant, out CombatantSnapshot self);
                Assert.That(brain.TryBuildLeashedCommand(simulation, self, player, camp.Center,
                    GrindingRegionRunController.LeashRadius, out CombatCommand command), Is.True);
                Assert.That(command.Ability, Is.EqualTo(AbilityId.None));
                Assert.That(command.Movement.X, Is.LessThan(0f),
                    "Reapproaching the evading mob cannot make it attack again before reaching home.");
                Assert.That(simulation.SubmitCommand(command), Is.True);
                Assert.That(simulation.SubmitCommand(CombatCommand.Move(run.Player, tick, west, west)), Is.True);
                simulation.Tick();
            }
            simulation.TryGetSnapshot(run.Player, out CombatantSnapshot returningPlayer);
            simulation.TryGetSnapshot(camp.GetMob(0).Participant, out CombatantSnapshot survivor);
            Assert.That(run.IsWithinActiveCampLeash(returningPlayer.Position), Is.True);
            Assert.That(survivor.IsAlive, Is.True);
            Assert.That(run.ActiveMobCount, Is.EqualTo(2));
            Assert.That(run.TotalExperience, Is.Zero);
        }

        [Test]
        public void SnapshotAbandon_DeadNearbyMobDoesNotHoldDistantSurvivor()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            Float3 playerPosition = camp.Center + new Float3(46f, 0f, 0f);
            CombatSimulation simulation = CreateRetirementSimulation(camp, playerPosition,
                playerPosition + new Float3(-2f, 0f, 0f),
                playerPosition + new Float3(-61f, 0f, 0f), false, 1f);
            Float3 facing = new Float3(-1f, 0f, 0f);
            Assert.That(simulation.SubmitCommand(CombatCommand.Cast(run.Player, 1u,
                default, facing, AbilityId.FireBlast, camp.GetMob(0).Participant,
                playerPosition + facing * 2f, facing)), Is.True);
            simulation.Tick();
            Assert.That(simulation.TryGetSnapshot(camp.GetMob(0).Participant, out CombatantSnapshot defeated), Is.True);
            Assert.That(defeated.IsAlive, Is.False, "The fixture uses a real Core-resolved death.");
            Assert.That(run.TryAbandonCamp(simulation, out _), Is.True);
            Assert.That(run.Kills, Is.Zero, "Retirement itself cannot invent a death reward.");
        }

        [Test]
        public void SnapshotAbandon_MissingActiveMobSnapshotFailsClosed()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            CombatSimulation simulation = CreateRetirementSimulation(camp, PveRegionLayout.GetCampCenter(6),
                camp.GetMob(0).Position, camp.GetMob(1).Position, true);
            Assert.That(run.TryAbandonCamp(simulation, out _), Is.False);
            Assert.That(run.ActiveMobCount, Is.EqualTo(camp.MobCount));
        }

        [Test]
        public void SnapshotAbandon_RefugeKeepsImmediateSanctuaryResetEvenWithNearbyMobs()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            CombatSimulation simulation = CreateRetirementSimulation(camp, PveRegionLayout.Refuge,
                PveRegionLayout.Refuge + new Float3(2f, 0f, 0f), camp.GetMob(1).Position);
            Assert.That(run.TryAbandonCamp(simulation, out _), Is.True,
                "Refuge remains an explicit reset; this fix only changes ordinary leash retirement.");
            Assert.That(run.ActiveMobCount, Is.Zero);
            Assert.That(run.TotalExperience, Is.Zero);
        }

        [Test]
        public void SnapshotAbandon_RetainedChecksAllocateNothingAfterWarmup()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            Float3 playerPosition = camp.Center + new Float3(46f, 0f, 0f);
            CombatSimulation simulation = CreateRetirementSimulation(camp, playerPosition,
                playerPosition + new Float3(-2f, 0f, 0f), camp.GetMob(1).Position);
            for (int index = 0; index < 32; index++) { run.TryAbandonCamp(simulation, out _); }
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 2000; index++) { run.TryAbandonCamp(simulation, out _); }
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(run.ActiveMobCount, Is.EqualTo(2));
        }

        [Test]
        public void Progression_ReportsLevelGainAndSaturatesAtMaximumWithoutOverflow()
        {
            GrindingRegionRunController run = new GrindingRegionRunController(150);
            run.TryActivateCamp(0, GrindingRegionRunController.GetCampDefinition(0).Center,
                out GrindingCampDefinition camp);
            GrindingCombatObservation gain = run.ObserveCombatEvent(Death(1, run.Player,
                camp.GetMob(0).Participant));
            Assert.That(gain.DidLevelUp, Is.True);
            Assert.That(run.Level, Is.EqualTo(2));

            GrindingRegionRunController capped = new GrindingRegionRunController(
                PersistentLevelProgression.MaximumExperience - 1);
            capped.TryActivateCamp(0, camp.Center, out GrindingCampDefinition last);
            Assert.That(capped.ObserveCombatEvent(Death(1, capped.Player,
                last.GetMob(0).Participant)).ExperienceAwarded, Is.EqualTo(1));
            Assert.That(capped.ObserveCombatEvent(Death(1, capped.Player,
                last.GetMob(1).Participant)).ExperienceAwarded, Is.Zero);
            Assert.That(capped.TotalExperience, Is.EqualTo(PersistentLevelProgression.MaximumExperience));
            Assert.That(capped.Kills, Is.EqualTo(2));
        }

        [Test]
        public void EnteringRefuge_AbandonsNearbyCampWithoutRewardAndKeepsRespawnCooldown()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition first);
            Assert.That(Float3.PlanarDistance(first.Center, PveRegionLayout.Refuge),
                Is.LessThan(GrindingRegionRunController.LeashRadius));
            Assert.That(run.TryAbandonCamp(10, PveRegionLayout.Refuge,
                out GrindingCampDefinition abandoned), Is.True);
            Assert.That(abandoned.GetMob(0).Participant, Is.EqualTo(first.GetMob(0).Participant));
            Assert.That(run.ActiveCampIndex, Is.EqualTo(-1));
            Assert.That(run.ActiveMobCount, Is.Zero);
            Assert.That(run.TotalExperience, Is.Zero);
            Assert.That(run.Kills, Is.Zero);
            Assert.That(run.RespawnAtTick, Is.EqualTo(610));
            Assert.That(run.ObserveCombatEvent(Death(11, run.Player,
                first.GetMob(0).Participant)).WasHandled, Is.False);
            Assert.That(run.TryActivateCamp(11, PveRegionLayout.Refuge, out _), Is.False);
            Assert.That(run.TryActivateCamp(11, first.Center, out _), Is.False);
            Assert.That(run.TryActivateCamp(609, first.Center, out _), Is.False);
            Assert.That(run.TryActivateCamp(610, PveRegionLayout.Refuge, out _), Is.False);
            Assert.That(run.TryActivateCamp(610, first.Center, out GrindingCampDefinition replacement), Is.True);
            Assert.That(replacement.GetMob(0).Participant.Value,
                Is.GreaterThan(first.GetMob(first.MobCount - 1).Participant.Value));
        }

        [Test]
        public void DeathRecovery_PreservesProgressionAndKillsButRejectsOldEnemyIdentities()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition original);
            run.ObserveCombatEvent(Death(1, run.Player, original.GetMob(0).Participant));
            int experience = run.TotalExperience;
            Assert.That(run.ObserveCombatEvent(Death(2, original.GetMob(1).Participant,
                run.Player)).PlayerDied, Is.True);
            Assert.That(run.IsPlayerDead, Is.True);
            Assert.That(run.TryActivateCamp(1000, original.Center, out _), Is.False);
            Assert.That(run.ObserveCombatEvent(Death(3, run.Player,
                original.GetMob(1).Participant)).WasHandled, Is.False);
            run.ResetAfterDeath(0);
            Assert.That(run.TotalExperience, Is.EqualTo(experience));
            Assert.That(run.Kills, Is.EqualTo(1));
            Assert.That(run.IsPlayerDead, Is.False);
            Assert.That(run.TryActivateCamp(0, original.Center, out GrindingCampDefinition replacement), Is.True);
            Assert.That(replacement.GetMob(0).Participant.Value,
                Is.GreaterThan(original.GetMob(1).Participant.Value));
            Assert.That(run.ObserveCombatEvent(Death(10, run.Player,
                original.GetMob(1).Participant)).WasHandled, Is.False);
        }

        [Test]
        public void SavedExperience_RecreatesSameLevelWithoutDependingOnTraining()
        {
            GrindingRegionRunController run = new GrindingRegionRunController(3920);
            Assert.That(run.Level, Is.EqualTo(8));
            Assert.That(run.TotalExperience, Is.EqualTo(3920));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrindingRegionRunController(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => new GrindingRegionRunController(int.MaxValue));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void MutualKills_RewardSameTickDeathsIndependentlyOfEventOrder(bool playerDeathFirst)
        {
            GrindingRegionRunController run = new GrindingRegionRunController(150);
            run.TryActivateCamp(0, GrindingRegionRunController.GetCampDefinition(0).Center,
                out GrindingCampDefinition camp);
            CombatEvent playerDeath = Death(10, camp.GetMob(0).Participant, run.Player);
            if (playerDeathFirst)
            {
                Assert.That(run.ObserveCombatEvent(playerDeath).PlayerDied, Is.True);
            }

            int expectedExperience = 150;
            for (int index = 0; index < camp.MobCount; index++)
            {
                GrindingMobDefinition mob = camp.GetMob(index);
                GrindingCombatObservation kill = run.ObserveCombatEvent(
                    Death(10, run.Player, mob.Participant));
                Assert.That(kill.WasHandled, Is.True);
                Assert.That(kill.ExperienceAwarded, Is.EqualTo(mob.ExperienceReward));
                expectedExperience += mob.ExperienceReward;
            }

            if (!playerDeathFirst)
            {
                Assert.That(run.ObserveCombatEvent(playerDeath).PlayerDied, Is.True);
            }

            Assert.That(run.TotalExperience, Is.EqualTo(expectedExperience));
            Assert.That(run.Kills, Is.EqualTo(camp.MobCount));
            Assert.That(run.Level, Is.EqualTo(2));
            Assert.That(run.IsPlayerDead, Is.True);
            Assert.That(run.ObserveCombatEvent(playerDeath).WasHandled, Is.False);
            run.ResetAfterDeath(0);
            Assert.That(run.TotalExperience, Is.EqualTo(expectedExperience));
            Assert.That(run.IsPlayerDead, Is.False);
        }

        [Test]
        public void PlayerDeath_RejectsOwnedKillsFromLaterTicks()
        {
            GrindingRegionRunController run = Activate(0, out GrindingCampDefinition camp);
            run.ObserveCombatEvent(Death(10, camp.GetMob(0).Participant, run.Player));
            Assert.That(run.ObserveCombatEvent(Death(11, run.Player,
                camp.GetMob(0).Participant)).WasHandled, Is.False);
            Assert.That(run.TotalExperience, Is.Zero);
            Assert.That(run.Kills, Is.Zero);
        }

        [Test]
        public void CampRespawnDeadline_SaturatesRatherThanWrapping()
        {
            GrindingRegionRunController run = Activate(0, out _);
            Clear(run, int.MaxValue - 2);
            Assert.That(run.RespawnAtTick, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void RepeatedCampCycles_AreBoundedAndAllocateNothingAfterWarmup()
        {
            GrindingRegionRunController run = new GrindingRegionRunController();
            GrindingCampDefinition preview = GrindingRegionRunController.GetCampDefinition(0);
            Float3 outside = preview.Center + new Float3(-27f, 0f, 0f);
            for (int index = 0; index < 20; index++)
            {
                Cycle(run, index * 1000, preview.Center, outside);
            }

            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 20; index < 1020; index++)
            {
                Cycle(run, index * 1000, preview.Center, outside);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(run.Kills, Is.EqualTo(2040));
            Assert.That(run.ActiveMobCount, Is.Zero);
            Assert.That(run.Level, Is.EqualTo(10));
        }

        [Test]
        public void NewModeIdentity_IsAppendedAndDoesNotReinterpretTraining()
        {
            Assert.That((int)GameModeId.PveLeveling, Is.EqualTo(5));
            Assert.That((int)PlayableModeId.PveLeveling, Is.EqualTo(4));
            Assert.That((int)RunKind.PersistentRegion, Is.EqualTo(5));
            LegacyGameModeMigration mapping = LegacyGameModeMigrationAdapter.Migrate(GameModeId.PveLeveling);
            Assert.That(mapping.IsTraining, Is.False);
            Assert.That(mapping.PlayableMode, Is.EqualTo(PlayableModeId.PveLeveling));
            Assert.That(mapping.RunKind, Is.EqualTo(RunKind.PersistentRegion));
            Assert.That(LegacyGameModeMigrationAdapter.Migrate(GameModeId.Leveling).IsTraining, Is.True);
            SessionFlowController flow = new SessionFlowController();
            flow.Start(GameModeId.PveLeveling, 0);
            Assert.That(flow.Mode, Is.EqualTo(GameModeId.PveLeveling));
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Playing));
        }

        private static void Cycle(GrindingRegionRunController run, int tick, Float3 center, Float3 outside)
        {
            run.TryActivateCamp(tick, outside, out _);
            run.TryActivateCamp(tick, center, out _);
            Clear(run, tick + 1);
        }

        private static CombatSimulation CreateRetirementSimulation(
            GrindingCampDefinition camp, Float3 playerPosition, Float3 firstPosition,
            Float3 secondPosition, bool omitSecond = false, float firstHealth = 100f)
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.PveLevelingDefault);
            Assert.That(simulation.AddCombatant(new ParticipantId(1), new TeamId(1),
                ClassId.Mage, SpecializationId.Fire, playerPosition), Is.True);
            for (int index = 0; index < (omitSecond ? 1 : camp.MobCount); index++)
            {
                GrindingMobDefinition mob = camp.GetMob(index);
                Float3 position = index == 0 ? firstPosition : secondPosition;
                LevelingMobArchetypeDefinition archetype = LevelingMobArchetypeCatalog.Get(mob.Archetype);
                Assert.That(simulation.Rules.CollisionMap.IsPositionValid(position,
                    ArenaLayout.CombatantCollisionRadius), Is.True, "Fixture position must not be resolved elsewhere.");
                Assert.That(simulation.AddCombatant(mob.Participant, new TeamId(2),
                    archetype.Class, archetype.Specialization, position,
                    index == 0 ? firstHealth : mob.BaseMaximumHealth, false), Is.True);
            }
            Assert.That(simulation.TryGetSnapshot(new ParticipantId(1), out CombatantSnapshot player), Is.True);
            Assert.That(player.Position, Is.EqualTo(playerPosition));
            return simulation;
        }

        private static GrindingRegionRunController Activate(int index, out GrindingCampDefinition camp)
        {
            GrindingRegionRunController run = new GrindingRegionRunController();
            Assert.That(run.TryActivateCamp(0,
                GrindingRegionRunController.GetCampDefinition(index).Center, out camp), Is.True);
            return run;
        }

        private static void Clear(GrindingRegionRunController run, int tick)
        {
            while (run.ActiveMobCount > 0)
            {
                run.ObserveCombatEvent(Death(tick, run.Player, run.GetActiveMobId(0)));
            }
        }

        private static CombatEvent Death(int tick, ParticipantId actor, ParticipantId target)
        {
            return CombatEvent.Create(tick, CombatEventKind.Death, actor, target,
                reason: "ability");
        }
    }
}
