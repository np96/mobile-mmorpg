using System;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class PersistentLevelProgressionTests
    {
        [Test]
        public void Curve_HasIndependentStrictlyIncreasingTenLevelThresholds()
        {
            int[] expected = { 0, 160, 420, 800, 1320, 2000, 2860, 3920, 5200, 6720 };
            Assert.That(PersistentLevelProgression.MinimumLevel, Is.EqualTo(1));
            Assert.That(PersistentLevelProgression.MaximumLevel, Is.EqualTo(10));
            for (int index = 0; index < expected.Length; index++)
            {
                int level = index + 1;
                int threshold = PersistentLevelProgression.GetCumulativeExperienceForLevel(level);
                Assert.That(threshold, Is.EqualTo(expected[index]));
                Assert.That(PersistentLevelProgression.GetLevelForExperience(threshold),
                    Is.EqualTo(level));
                if (index > 0)
                {
                    Assert.That(PersistentLevelProgression.GetLevelForExperience(threshold - 1),
                        Is.EqualTo(level - 1));
                }
            }

            Assert.That(LevelProgressionCurve.MaximumLevel, Is.EqualTo(6));
            Assert.That(LevelProgressionCurve.MaximumExperience, Is.EqualTo(1130));
        }

        [TestCase(-1)]
        [TestCase(6721)]
        [TestCase(int.MaxValue)]
        public void Curve_RejectsInvalidSavedExperienceInsteadOfResetting(int experience)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PersistentLevelProgression.GetLevelForExperience(experience));
        }

        [TestCase(0)]
        [TestCase(11)]
        public void Curve_RejectsInvalidLevel(int level)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                PersistentLevelProgression.GetCumulativeExperienceForLevel(level));
        }

        [Test]
        public void CombatLevel_HasExplicitModeIsolationAndTwelveHealthPerLevel()
        {
            ParticipantId player = new ParticipantId(1);
            CombatSimulation arena = new CombatSimulation(CombatRules.ArenaDefault);
            arena.AddCombatant(player, new TeamId(1), ClassId.Mage,
                SpecializationId.Fire, default);
            Assert.That(arena.ApplyPersistentProgressionLevel(player, 2), Is.False);

            CombatSimulation simulation = CreatePveDuel();
            Assert.That(simulation.ApplyProgressionLevel(player, 2), Is.False);
            Assert.That(simulation.ApplyPersistentProgressionLevel(player, 0), Is.False);
            Assert.That(simulation.ApplyPersistentProgressionLevel(player, 11), Is.False);
            Assert.That(simulation.ApplyPersistentProgressionLevel(player, 10), Is.True);
            Assert.That(simulation.TryGetSnapshot(player, out CombatantSnapshot state), Is.True);
            Assert.That(state.Level, Is.EqualTo(10));
            Assert.That(state.MaxHealth, Is.EqualTo(208f));
            Assert.That(state.Health, Is.EqualTo(state.MaxHealth));
            Assert.That(simulation.ApplyPersistentProgressionLevel(player, 9), Is.False);
            Assert.That(simulation.ApplyPersistentProgressionLevel(player, 10), Is.False);
        }

        [Test]
        public void CombatLevel_RestoresSpentResourcesAndHealthOnGain()
        {
            CombatSimulation simulation = CreatePveDuel();
            ParticipantId player = new ParticipantId(1);
            ParticipantId enemy = new ParticipantId(2);
            simulation.SubmitCommand(Cast(player, enemy));
            simulation.SubmitCommand(Cast(enemy, player));
            simulation.Tick();
            simulation.TryGetSnapshot(player, out CombatantSnapshot before);
            Assert.That(before.Health, Is.LessThan(before.MaxHealth));
            Assert.That(before.Resource, Is.LessThan(before.MaxResource));

            Assert.That(simulation.ApplyPersistentProgressionLevel(player, 2), Is.True);
            simulation.TryGetSnapshot(player, out CombatantSnapshot after);
            Assert.That(after.Health, Is.EqualTo(112f));
            Assert.That(after.Resource, Is.EqualTo(after.MaxResource));
        }

        [Test]
        public void CombatLevel_AddsEightPercentDamageOnlyInPersistentMode()
        {
            Assert.That(ResolvePveFireBlast(1), Is.EqualTo(12f).Within(0.001f));
            Assert.That(ResolvePveFireBlast(2), Is.EqualTo(12f * 1.08f).Within(0.001f));
            Assert.That(ResolvePveFireBlast(10), Is.EqualTo(12f * 1.72f).Within(0.001f));
        }

        [Test]
        public void PveRules_KeepThirtyHertzAndDoNotApplyTrainingAbilityLocks()
        {
            CombatRules rules = CombatRules.PveLevelingDefault;
            Assert.That(rules.TickRate, Is.EqualTo(30));
            Assert.That(rules.PlayableMode, Is.EqualTo(PlayableModeId.PveLeveling));
            Assert.That(rules.AbilityProgressionTeam.Value, Is.Zero);
            Assert.That(rules.RadialHazardsEnabled, Is.False);
            CombatSimulation simulation = CreatePveDuel();
            Assert.That(simulation.CanUseAbility(new ParticipantId(1), AbilityId.Blink,
                out string reason), Is.True, reason);
        }

        private static CombatSimulation CreatePveDuel()
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.PveLevelingDefault);
            simulation.AddCombatant(new ParticipantId(1), new TeamId(1),
                ClassId.Mage, SpecializationId.Fire, PveRegionLayout.Refuge);
            simulation.AddCombatant(new ParticipantId(2), new TeamId(2),
                ClassId.Mage, SpecializationId.Fire, PveRegionLayout.Refuge + new Float3(6f, 0f, 0f), 1000f, false);
            return simulation;
        }

        [Test]
        public void RegionDespawn_RejectsOtherModesAndProtectsFriendlyActors()
        {
            CombatSimulation arena = new CombatSimulation(CombatRules.ArenaDefault);
            ParticipantId enemy = new ParticipantId(2);
            arena.AddCombatant(enemy, new TeamId(2), ClassId.Mage,
                SpecializationId.Fire, default);
            Assert.That(arena.TryDespawnRegionCombatant(enemy), Is.False);

            CombatSimulation simulation = CreatePveDuel();
            Assert.That(simulation.TryDespawnRegionCombatant(new ParticipantId(1)), Is.False);
            simulation.AddCombatant(new ParticipantId(3), new TeamId(1), ClassId.Mage,
                SpecializationId.Fire, PveRegionLayout.Refuge + new Float3(-2f, 0f, 0f));
            Assert.That(simulation.TryDespawnRegionCombatant(new ParticipantId(3)), Is.False);
            Assert.That(simulation.TryDespawnRegionCombatant(enemy), Is.True);
            Assert.That(simulation.TryGetSnapshot(enemy, out _), Is.False);
            Assert.That(simulation.TryDespawnRegionCombatant(enemy), Is.False);
        }

        [Test]
        public void RegionDespawn_ClearsOwnedAurasAndQueuedCommandsWithoutDeath()
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.PveLevelingDefault);
            ParticipantId player = new ParticipantId(1);
            ParticipantId priest = new ParticipantId(2);
            simulation.AddCombatant(player, new TeamId(1), ClassId.Mage,
                SpecializationId.Fire, PveRegionLayout.Refuge);
            simulation.AddCombatant(priest, new TeamId(2), ClassId.Priest,
                SpecializationId.Shadow, PveRegionLayout.Refuge + new Float3(6f, 0f, 0f), 100f, false);
            simulation.SubmitCommand(CombatCommand.Cast(priest, 1u, default,
                new Float3(-1f, 0f, 0f), AbilityId.ShadowWordPain, player,
                default, new Float3(-1f, 0f, 0f)));
            simulation.Tick();
            Assert.That(simulation.HasAura(player, AuraId.ShadowWordPain), Is.True);
            simulation.TryGetSnapshot(player, out CombatantSnapshot before);
            simulation.SubmitCommand(Cast(player, priest));
            simulation.SubmitCommand(CombatCommand.Move(priest, 2u,
                new Float3(-1f, 0f, 0f), new Float3(-1f, 0f, 0f)));
            Assert.That(simulation.TryDespawnRegionCombatant(priest), Is.True);
            Assert.That(simulation.HasAura(player, AuraId.ShadowWordPain), Is.False);
            for (int tick = 0; tick < 100; tick++)
            {
                simulation.Tick();
                for (int index = 0; index < simulation.EventCount; index++)
                {
                    CombatEvent combatEvent = simulation.GetEvent(index);
                    Assert.That(combatEvent.Kind, Is.Not.EqualTo(CombatEventKind.Death));
                    Assert.That(combatEvent.Actor, Is.Not.EqualTo(priest));
                    Assert.That(combatEvent.Target, Is.Not.EqualTo(priest));
                }
            }

            simulation.TryGetSnapshot(player, out CombatantSnapshot after);
            Assert.That(after.Health, Is.EqualTo(before.Health));
        }

        [Test]
        public void RegionDespawn_ReusesBoundedStorageWithoutAllocating()
        {
            CombatSimulation simulation = CreatePveDuel();
            simulation.TryDespawnRegionCombatant(new ParticipantId(2));
            Float3 position = PveRegionLayout.Refuge + new Float3(6f, 0f, 0f);
            for (int index = 3; index < 103; index++)
            {
                ParticipantId id = new ParticipantId(index);
                simulation.AddCombatant(id, new TeamId(2), ClassId.Mage,
                    SpecializationId.Fire, position, 50f, false);
                simulation.TryDespawnRegionCombatant(id);
            }

            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 103; index < 1103; index++)
            {
                ParticipantId id = new ParticipantId(index);
                simulation.AddCombatant(id, new TeamId(2), ClassId.Mage,
                    SpecializationId.Fire, position, 50f, false);
                simulation.TryDespawnRegionCombatant(id);
            }

            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocated, Is.Zero);
            Assert.That(simulation.CombatantCount, Is.EqualTo(1));
        }

        private static float ResolvePveFireBlast(int level)
        {
            CombatSimulation simulation = CreatePveDuel();
            ParticipantId player = new ParticipantId(1);
            ParticipantId enemy = new ParticipantId(2);
            if (level > 1)
            {
                simulation.ApplyPersistentProgressionLevel(player, level);
            }

            simulation.SubmitCommand(Cast(player, enemy));
            simulation.Tick();
            simulation.TryGetSnapshot(enemy, out CombatantSnapshot state);
            return state.MaxHealth - state.Health;
        }

        private static CombatCommand Cast(ParticipantId actor, ParticipantId target)
        {
            Float3 facing = actor.Value == 1
                ? new Float3(1f, 0f, 0f)
                : new Float3(-1f, 0f, 0f);
            return CombatCommand.Cast(actor, 1u, default, facing,
                AbilityId.FireBlast, target, default, facing);
        }
    }
}
