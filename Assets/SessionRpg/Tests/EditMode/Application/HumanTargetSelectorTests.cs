using System;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class HumanTargetSelectorTests
    {
        private static readonly ParticipantId Human = new ParticipantId(1);

        [Test]
        public void Select_WithNoSelectedTarget_SelectsNearestLivingHostile()
        {
            CombatantSnapshot[] snapshots =
            {
                CreateSnapshot(1, 1, 0f, 0f),
                CreateSnapshot(7, 2, 5f, 0f),
                CreateSnapshot(4, 1, 1f, 0f),
                CreateSnapshot(9, 2, 2f, 0f),
            };

            ParticipantId selected = HumanTargetSelector.Select(
                snapshots,
                snapshots.Length,
                Human,
                default);

            Assert.That(selected, Is.EqualTo(new ParticipantId(9)));
        }

        [Test]
        public void Select_WhenSelectedEnemyDied_SelectsNearestRemainingHostile()
        {
            CombatantSnapshot[] snapshots =
            {
                CreateSnapshot(1, 1, 0f, 0f),
                CreateSnapshot(3, 2, 1f, 0f, isAlive: false),
                CreateSnapshot(8, 2, 4f, 0f),
                CreateSnapshot(6, 2, 2f, 0f),
            };

            ParticipantId selected = HumanTargetSelector.Select(
                snapshots,
                snapshots.Length,
                Human,
                new ParticipantId(3));

            Assert.That(selected, Is.EqualTo(new ParticipantId(6)));
        }

        [Test]
        public void Select_AtEqualDistance_UsesLowestParticipantIdRegardlessOfOrder()
        {
            CombatantSnapshot[] firstOrder =
            {
                CreateSnapshot(1, 1, 0f, 0f),
                CreateSnapshot(12, 2, -3f, 4f),
                CreateSnapshot(5, 2, 3f, 4f),
            };
            CombatantSnapshot[] reverseOrder =
            {
                firstOrder[0],
                firstOrder[2],
                firstOrder[1],
            };

            ParticipantId first = HumanTargetSelector.Select(
                firstOrder,
                firstOrder.Length,
                Human,
                default);
            ParticipantId second = HumanTargetSelector.Select(
                reverseOrder,
                reverseOrder.Length,
                Human,
                default);

            Assert.That(first, Is.EqualTo(new ParticipantId(5)));
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void Select_WithNoTargetableHostile_ReturnsInvalid()
        {
            CombatantSnapshot[] snapshots =
            {
                CreateSnapshot(1, 1, 0f, 0f),
                CreateSnapshot(2, 1, 1f, 0f),
                CreateSnapshot(3, 2, 2f, 0f, isAlive: false),
                CreateSnapshot(
                    4,
                    2,
                    3f,
                    0f,
                    auraTags: AuraTag.TargetingDropped),
                CreateSnapshot(
                    5,
                    2,
                    4f,
                    0f,
                    auraTags: AuraTag.Stealth),
            };

            ParticipantId selected = HumanTargetSelector.Select(
                snapshots,
                snapshots.Length,
                Human,
                default);

            Assert.That(selected.IsValid, Is.False);
        }

        [Test]
        public void Select_PreservesValidManualTargetEvenWhenAnotherHostileIsCloser()
        {
            ParticipantId manualTarget = new ParticipantId(7);
            CombatantSnapshot[] snapshots =
            {
                CreateSnapshot(1, 1, 0f, 0f),
                CreateSnapshot(7, 2, 8f, 0f),
                CreateSnapshot(3, 2, 1f, 0f),
            };

            ParticipantId selected = HumanTargetSelector.Select(
                snapshots,
                snapshots.Length,
                Human,
                manualTarget);

            Assert.That(selected, Is.EqualTo(manualTarget));
        }

        [Test]
        public void Select_WhenCurrentHostileDropsTargeting_SelectsNearestAlternative()
        {
            CombatantSnapshot[] snapshots =
            {
                CreateSnapshot(1, 1, 0f, 0f),
                CreateSnapshot(
                    2,
                    2,
                    1f,
                    0f,
                    auraTags: AuraTag.TargetingDropped),
                CreateSnapshot(3, 2, 3f, 0f),
            };

            ParticipantId selected = HumanTargetSelector.Select(
                snapshots,
                snapshots.Length,
                Human,
                new ParticipantId(2));

            Assert.That(selected, Is.EqualTo(new ParticipantId(3)));
        }

        [Test]
        public void Select_AfterWarmup_AllocatesZeroManagedBytes()
        {
            CombatantSnapshot[] snapshots =
            {
                CreateSnapshot(1, 1, 0f, 0f),
                CreateSnapshot(9, 2, 2f, 0f),
                CreateSnapshot(4, 2, 4f, 0f),
            };
            int checksum = 0;
            for (int iteration = 0; iteration < 256; iteration++)
            {
                checksum += HumanTargetSelector.Select(
                    snapshots,
                    snapshots.Length,
                    Human,
                    default).Value;
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int iteration = 0; iteration < 4096; iteration++)
            {
                checksum += HumanTargetSelector.Select(
                    snapshots,
                    snapshots.Length,
                    Human,
                    default).Value;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero);
            Assert.That(checksum, Is.GreaterThan(0));
        }

        private static CombatantSnapshot CreateSnapshot(
            int participant,
            int team,
            float x,
            float z,
            bool isAlive = true,
            AuraTag auraTags = AuraTag.None)
        {
            return new CombatantSnapshot(
                new ParticipantId(participant),
                new TeamId(team),
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(x, 0f, z),
                new Float3(0f, 0f, 1f),
                isAlive ? 100f : 0f,
                100f,
                0f,
                AbilityId.None,
                0,
                0,
                AbilityActivationType.Instant,
                SpellSchoolMask.None,
                ResourceType.Mana,
                100f,
                100f,
                0,
                default,
                0,
                SpellSchoolMask.None,
                isAlive,
                false,
                activeAuraTags: auraTags);
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
