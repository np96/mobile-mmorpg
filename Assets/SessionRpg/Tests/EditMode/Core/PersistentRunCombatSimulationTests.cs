using System;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class PersistentRunCombatSimulationTests
    {
        [Test]
        public void DefeatedCombatantCanBeRemovedAndBoundedSlotReused()
        {
            CombatSimulation simulation = CreateSimulation();
            ParticipantId attacker = new ParticipantId(1);
            ParticipantId defeated = new ParticipantId(2);
            Assert.That(simulation.AddCombatant(
                attacker,
                new TeamId(1),
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default,
                100f,
                false), Is.True);
            Assert.That(simulation.AddCombatant(
                defeated,
                new TeamId(2),
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(1f, 0f, 0f),
                1f,
                false), Is.True);

            Assert.That(simulation.SubmitCommand(CombatCommand.Cast(
                attacker,
                1u,
                default,
                new Float3(1f, 0f, 0f),
                AbilityId.Mutilate,
                defeated,
                new Float3(1f, 0f, 0f),
                new Float3(1f, 0f, 0f))), Is.True);
            simulation.Tick();

            Assert.That(simulation.TryRemoveDefeatedCombatant(defeated), Is.True);
            Assert.That(simulation.CombatantCount, Is.EqualTo(1));
            Assert.That(simulation.TryGetSnapshot(defeated, out _), Is.False);
            Assert.That(simulation.TryRemoveDefeatedCombatant(attacker), Is.False);

            for (int value = 3; value <= CombatSimulation.MaxCombatants + 1; value++)
            {
                Assert.That(simulation.AddCombatant(
                    new ParticipantId(value),
                    new TeamId(2),
                    ClassId.Mage,
                    SpecializationId.Fire,
                    new Float3(value, 0f, 0f),
                    10f,
                    false), Is.True);
            }

            Assert.That(simulation.CombatantCount, Is.EqualTo(CombatSimulation.MaxCombatants));
        }

        [Test]
        public void RunRecoveryClampsAndRejectsWrongResource()
        {
            CombatSimulation simulation = CreateSimulation();
            ParticipantId priest = new ParticipantId(1);
            ParticipantId enemy = new ParticipantId(2);
            Assert.That(simulation.AddCombatant(
                priest,
                new TeamId(1),
                ClassId.Priest,
                SpecializationId.Discipline,
                default), Is.True);
            Assert.That(simulation.AddCombatant(
                enemy,
                new TeamId(2),
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(1f, 0f, 0f)), Is.True);

            Assert.That(simulation.SubmitCommand(CombatCommand.Cast(
                enemy,
                1u,
                default,
                new Float3(-1f, 0f, 0f),
                AbilityId.Mutilate,
                priest,
                default,
                new Float3(-1f, 0f, 0f))), Is.True);
            simulation.Tick();

            Assert.That(simulation.TryRestoreHealth(priest, 1000f, out float health), Is.True);
            Assert.That(health, Is.GreaterThan(0f));
            Assert.That(simulation.TryGetSnapshot(priest, out CombatantSnapshot restored), Is.True);
            Assert.That(restored.Health, Is.EqualTo(restored.MaxHealth));
            Assert.That(simulation.TryRestoreHealth(priest, 1f, out _), Is.False);
            Assert.That(simulation.TryRestoreResource(
                priest,
                ResourceType.Energy,
                10f,
                out _), Is.False);
        }

        [Test]
        public void RunModifiersScaleDamageAndMovementAuthoritatively()
        {
            CombatSimulation baseline = CreateDamageSimulation(1000);
            CombatSimulation empowered = CreateDamageSimulation(2000);

            CastShiv(baseline);
            CastShiv(empowered);
            Assert.That(baseline.TryGetSnapshot(
                new ParticipantId(2), out CombatantSnapshot baselineTarget),
                Is.True);
            Assert.That(empowered.TryGetSnapshot(
                new ParticipantId(2), out CombatantSnapshot empoweredTarget),
                Is.True);
            float baselineDamage = baselineTarget.MaxHealth - baselineTarget.Health;
            float empoweredDamage = empoweredTarget.MaxHealth - empoweredTarget.Health;
            Assert.That(empoweredDamage,
                Is.EqualTo(baselineDamage * 2f).Within(0.001f));

            CombatSimulation movement = new CombatSimulation(new CombatRules(
                30, 6f, 100f, 120f, 0f, 1.5f, 15f, 5,
                ArenaCollisionMap.None, false,
                playableMode: PlayableModeId.HeroSiege));
            ParticipantId runner = new ParticipantId(7);
            Assert.That(movement.AddCombatant(
                runner, new TeamId(2), ClassId.Rogue,
                SpecializationId.Mutilate, default, 100f, false), Is.True);
            Assert.That(movement.TrySetRunCombatModifiers(
                runner, 1000, 2000), Is.True);
            Assert.That(movement.SubmitCommand(CombatCommand.Move(
                runner, 1u, new Float3(1f, 0f, 0f),
                new Float3(1f, 0f, 0f))), Is.True);
            movement.Tick();
            Assert.That(movement.TryGetSnapshot(
                runner, out CombatantSnapshot moved), Is.True);
            Assert.That(moved.Position.X, Is.EqualTo(0.4f).Within(0.001f));
        }

        [Test]
        public void PersistentRemovalAndReuse_AfterWarmupAllocatesZeroManagedBytes()
        {
            CombatSimulation simulation = new CombatSimulation(new CombatRules(
                30, 0f, 1f, 2f, 1f, 1.5f, 15f, 5,
                ArenaCollisionMap.None, true,
                playableMode: PlayableModeId.HeroSiege));
            int participantValue = 1;
            for (int index = 0; index < 128; index++)
            {
                CycleHazardDefeat(simulation, participantValue++);
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 4096; index++)
            {
                CycleHazardDefeat(simulation, participantValue++);
            }

            long allocatedBytes =
                GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero);
            Assert.That(simulation.CombatantCount, Is.Zero);
        }

        private static CombatSimulation CreateDamageSimulation(
            int damageMultiplierPermille)
        {
            CombatSimulation simulation = CreateSimulation();
            ParticipantId attacker = new ParticipantId(1);
            Assert.That(simulation.AddCombatant(
                attacker, new TeamId(1), ClassId.Rogue,
                SpecializationId.Mutilate, default, 100f, false), Is.True);
            Assert.That(simulation.AddCombatant(
                new ParticipantId(2), new TeamId(2), ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(1f, 0f, 0f), 100f, false), Is.True);
            Assert.That(simulation.TrySetRunCombatModifiers(
                attacker, damageMultiplierPermille, 1000), Is.True);
            return simulation;
        }

        private static void CastShiv(CombatSimulation simulation)
        {
            Assert.That(simulation.SubmitCommand(CombatCommand.Cast(
                new ParticipantId(1), 1u, default,
                new Float3(1f, 0f, 0f), AbilityId.Shiv,
                new ParticipantId(2), new Float3(1f, 0f, 0f),
                new Float3(1f, 0f, 0f))), Is.True);
            simulation.Tick();
        }

        private static void CycleHazardDefeat(
            CombatSimulation simulation,
            int participantValue)
        {
            ParticipantId participant = new ParticipantId(participantValue);
            if (!simulation.AddCombatant(
                    participant, new TeamId(2), ClassId.Rogue,
                    SpecializationId.Mutilate,
                    new Float3(3f, 0f, 0f), 10f, false))
            {
                throw new InvalidOperationException("Warm reuse add failed.");
            }

            simulation.Tick();
            if (!simulation.TryRemoveDefeatedCombatant(participant))
            {
                throw new InvalidOperationException("Warm reuse removal failed.");
            }
        }

        private static void ForceCollectionAndPrimeCounter()
        {
            GC.GetAllocatedBytesForCurrentThread();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.GetAllocatedBytesForCurrentThread();
        }

        private static CombatSimulation CreateSimulation() =>
            new CombatSimulation(new CombatRules(
                30,
                0f,
                100f,
                120f,
                0f,
                1.5f,
                15f,
                5,
                ArenaCollisionMap.None,
                false,
                playableMode: PlayableModeId.HeroSiege));
    }
}
