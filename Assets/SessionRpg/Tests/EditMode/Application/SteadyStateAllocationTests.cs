using System;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class SteadyStateAllocationTests
    {
        private const int WarmupIterations = 256;
        private const int MeasuredIterations = 4096;

        [Test]
        public void SnapshotCopyAndBothTeamBuilds_AfterWarmupAllocateZeroManagedBytes()
        {
            CombatSimulation simulation = CreateArenaSimulation();
            CombatantSnapshot[] snapshots =
                new CombatantSnapshot[CombatSimulation.MaxCombatants];
            ArenaTeamCoordinator coordinator = new ArenaTeamCoordinator();
            TeamId friendly = new TeamId(1);
            TeamId hostile = new TeamId(2);
            int focusChecksum = 0;

            for (int iteration = 0; iteration < WarmupIterations; iteration++)
            {
                int snapshotCount = simulation.CopySnapshots(snapshots);
                focusChecksum += coordinator
                    .Build(friendly, snapshots, snapshotCount)
                    .FocusTarget.Value;
                focusChecksum += coordinator
                    .Build(hostile, snapshots, snapshotCount)
                    .FocusTarget.Value;
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < MeasuredIterations; iteration++)
            {
                int snapshotCount = simulation.CopySnapshots(snapshots);
                focusChecksum += coordinator
                    .Build(friendly, snapshots, snapshotCount)
                    .FocusTarget.Value;
                focusChecksum += coordinator
                    .Build(hostile, snapshots, snapshotCount)
                    .FocusTarget.Value;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(
                allocatedBytes,
                Is.Zero,
                "Snapshot copying plus both ArenaTeamCoordinator.Build calls allocated " +
                "after warm-up; the decision-frame path must remain buffer-backed.");
            Assert.That(focusChecksum, Is.GreaterThan(0));
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
