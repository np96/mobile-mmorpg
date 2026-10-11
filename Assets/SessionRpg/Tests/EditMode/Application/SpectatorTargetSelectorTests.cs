using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class SpectatorTargetSelectorTests
    {
        [Test]
        public void Select_PrefersLowestLivingAllyAndExcludesHuman()
        {
            CombatantSnapshot[] snapshots =
            {
                CreateSnapshot(1, 1, true),
                CreateSnapshot(4, 2, true),
                CreateSnapshot(3, 1, true),
                CreateSnapshot(2, 1, true),
            };

            ParticipantId selected = SpectatorTargetSelector.Select(
                snapshots,
                snapshots.Length,
                new ParticipantId(1),
                new TeamId(1));

            Assert.That(selected, Is.EqualTo(new ParticipantId(2)));
        }

        [Test]
        public void Select_FallsBackToLivingOpponentWhenPartyIsEliminated()
        {
            CombatantSnapshot[] snapshots =
            {
                CreateSnapshot(1, 1, false),
                CreateSnapshot(2, 1, false),
                CreateSnapshot(4, 2, true),
                CreateSnapshot(3, 2, true),
            };

            ParticipantId selected = SpectatorTargetSelector.Select(
                snapshots,
                snapshots.Length,
                new ParticipantId(1),
                new TeamId(1));

            Assert.That(selected, Is.EqualTo(new ParticipantId(3)));
        }

        [Test]
        public void Select_ReturnsInvalidWhenNobodyElseIsAlive()
        {
            CombatantSnapshot[] snapshots =
            {
                CreateSnapshot(1, 1, false),
                CreateSnapshot(2, 1, false),
                CreateSnapshot(3, 2, false),
            };

            ParticipantId selected = SpectatorTargetSelector.Select(
                snapshots,
                snapshots.Length,
                new ParticipantId(1),
                new TeamId(1));

            Assert.That(selected.IsValid, Is.False);
        }

        private static CombatantSnapshot CreateSnapshot(
            int participant,
            int team,
            bool isAlive)
        {
            return new CombatantSnapshot(
                new ParticipantId(participant),
                new TeamId(team),
                ClassId.Mage,
                default,
                new Float3(0f, 0f, 1f),
                isAlive ? 100f : 0f,
                100f,
                0f,
                AbilityId.None,
                0,
                isAlive,
                false);
        }
    }
}
