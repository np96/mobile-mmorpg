using System;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class RunIdentityTests
    {
        [Test]
        public void PlayableModeIds_AreExplicitAndStable()
        {
            Assert.That((int)PlayableModeId.None, Is.Zero);
            Assert.That((int)PlayableModeId.Arena, Is.EqualTo(1));
            Assert.That((int)PlayableModeId.HeroSiege, Is.EqualTo(2));
            Assert.That((int)PlayableModeId.Dungeon, Is.EqualTo(3));
            Assert.That((int)RunKind.Training, Is.EqualTo(2));
            Assert.That((int)RunKind.Sequential, Is.EqualTo(3));
            Assert.That((int)RunKind.WaveDefense, Is.EqualTo(4));
            Assert.That((int)EncounterKind.Boss, Is.EqualTo(2));
        }

        [Test]
        public void RunEncounterSpawnAndObjectiveIds_UseValueSemantics()
        {
            Assert.That(default(RunId).IsValid, Is.False);
            Assert.That(default(EncounterId).IsValid, Is.False);
            Assert.That(default(SpawnId).IsValid, Is.False);
            Assert.That(default(ObjectiveId).IsValid, Is.False);

            Assert.That(new RunId(7u), Is.EqualTo(new RunId(7u)));
            Assert.That(new RunId(7u), Is.Not.EqualTo(new RunId(8u)));
            Assert.That(
                new EncounterId(2u).CompareTo(new EncounterId(3u)),
                Is.LessThan(0));
            Assert.That(new SpawnId(19u).ToString(), Is.EqualTo("19"));
            Assert.That(
                new ObjectiveId(11u).GetHashCode(),
                Is.EqualTo(new ObjectiveId(11u).GetHashCode()));
        }

        [Test]
        public void ObjectiveState_ClampsDamageAndIgnoresInvalidRequests()
        {
            var objective = new ObjectiveState(new ObjectiveId(1u), 100f);

            Assert.That(objective.IsValid, Is.True);
            Assert.That(objective.IsAlive, Is.True);
            Assert.That(objective.HealthFraction, Is.EqualTo(1f));
            Assert.That(objective.ApplyDamage(-1f), Is.Zero);
            Assert.That(objective.ApplyDamage(float.NaN), Is.Zero);
            Assert.That(objective.ApplyDamage(float.PositiveInfinity), Is.Zero);
            Assert.That(objective.ApplyDamage(35f), Is.EqualTo(35f));
            Assert.That(objective.Health, Is.EqualTo(65f));
            Assert.That(objective.HealthFraction, Is.EqualTo(0.65f));
            Assert.That(objective.ApplyDamage(1000f), Is.EqualTo(65f));
            Assert.That(objective.Health, Is.Zero);
            Assert.That(objective.IsAlive, Is.False);
            Assert.That(objective.IsDefeated, Is.True);
            Assert.That(objective.ApplyDamage(1f), Is.Zero);
        }

        [Test]
        public void ObjectiveState_RejectsInvalidIdentityOrMaximumHealth()
        {
            Assert.That(default(ObjectiveState).IsValid, Is.False);
            Assert.Throws<ArgumentException>(
                () => new ObjectiveState(default, 100f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ObjectiveState(new ObjectiveId(1u), 0f));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => new ObjectiveState(new ObjectiveId(1u), float.NaN));
        }

        [Test]
        public void ObjectiveDamage_AfterWarmupAllocatesZeroManagedBytes()
        {
            var objective = new ObjectiveState(new ObjectiveId(1u), 100000f);
            for (int index = 0; index < 256; index++)
            {
                objective.ApplyDamage(1f);
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 4096; index++)
            {
                objective.ApplyDamage(1f);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero);
            Assert.That(objective.Health, Is.EqualTo(95648f));
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
