using System;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class SteadyStateAllocationTests
    {
        private const int WarmupTicks = 256;
        private const int MeasuredTicks = 2048;

        [Test]
        public void Tick_AfterWarmupAllocatesZeroManagedBytes()
        {
            CombatSimulation simulation = CreateArenaSimulation();
            for (int tick = 0; tick < WarmupTicks; tick++)
            {
                simulation.Tick();
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int tick = 0; tick < MeasuredTicks; tick++)
            {
                simulation.Tick();
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(
                allocatedBytes,
                Is.Zero,
                "CombatSimulation.Tick allocated after warm-up; fixed-step gameplay " +
                "must retain its fixed-capacity storage contract.");
            Assert.That(
                simulation.CurrentTick,
                Is.EqualTo(WarmupTicks + MeasuredTicks));
        }

        private static CombatSimulation CreateArenaSimulation()
        {
            CombatSimulation simulation =
                new CombatSimulation(CombatRules.ArenaDefault);
            Add(
                simulation,
                1,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(-5f, 0f, -3f));
            Add(
                simulation,
                2,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(-5f, 0f, 3f));
            Add(
                simulation,
                3,
                2,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(5f, 0f, -3f));
            Add(
                simulation,
                4,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(5f, 0f, 3f));
            return simulation;
        }

        private static void Add(
            CombatSimulation simulation,
            int participant,
            int team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position)
        {
            bool added = simulation.AddCombatant(
                new ParticipantId(participant),
                new TeamId(team),
                classId,
                specialization,
                position,
                120f);
            Assert.That(added, Is.True);
        }

        private static void ForceCollectionAndPrimeCounter()
        {
            GC.GetAllocatedBytesForCurrentThread();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.GetAllocatedBytesForCurrentThread();
        }
    }
}
