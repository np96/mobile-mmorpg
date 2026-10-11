using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class ArenaTeamCoordinatorTests
    {
        private static readonly TeamId Friendly = new TeamId(1);
        private static readonly TeamId Hostile = new TeamId(2);

        [Test]
        public void Build_IsOrderIndependentAndKeepsStableFocus()
        {
            ArenaTeamCoordinator coordinator = new ArenaTeamCoordinator();
            CombatantSnapshot allyOne = Create(
                1,
                Friendly,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(-4f, 0f, 0f));
            CombatantSnapshot allyTwo = Create(
                2,
                Friendly,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(-3f, 0f, 1f));
            CombatantSnapshot lowerEnemy = Create(
                3,
                Hostile,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(4f, 0f, 0f));
            CombatantSnapshot higherEnemy = Create(
                4,
                Hostile,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(4f, 0f, 0f));

            ArenaTeamIntent first = coordinator.Build(
                Friendly,
                new[] { higherEnemy, allyTwo, lowerEnemy, allyOne },
                4);
            ArenaTeamIntent second = coordinator.Build(
                Friendly,
                new[] { allyOne, lowerEnemy, allyTwo, higherEnemy },
                4);

            Assert.That(second.FocusTarget, Is.EqualTo(first.FocusTarget));
            Assert.That(
                first.FocusTarget == lowerEnemy.Id ||
                first.FocusTarget == higherEnemy.Id,
                Is.True);
        }

        [Test]
        public void Build_ProtectsBreakableControlAndFocusesTheAvailableEnemy()
        {
            ArenaTeamCoordinator coordinator = new ArenaTeamCoordinator();
            CombatantSnapshot ally = Create(
                1,
                Friendly,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            CombatantSnapshot polymorphed = Create(
                3,
                Hostile,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(5f, 0f, 0f),
                auraTags: AuraTag.Disorient | AuraTag.BreaksOnDamage);
            CombatantSnapshot available = Create(
                4,
                Hostile,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(6f, 0f, 0f));

            ArenaTeamIntent intent = coordinator.Build(
                Friendly,
                new[] { ally, polymorphed, available },
                3);

            Assert.That(intent.ProtectedControlledEnemy, Is.EqualTo(polymorphed.Id));
            Assert.That(intent.FocusTarget, Is.EqualTo(available.Id));
            Assert.That(intent.IsProtectedControl(polymorphed.Id), Is.True);
        }

        [Test]
        public void Build_IdentifiesMostPressuredAllyAndActiveSetupTarget()
        {
            ArenaTeamCoordinator coordinator = new ArenaTeamCoordinator();
            CombatantSnapshot safeAlly = Create(
                1,
                Friendly,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(-5f, 0f, 0f));
            CombatantSnapshot pressuredAlly = Create(
                2,
                Friendly,
                ClassId.Priest,
                SpecializationId.Discipline,
                default,
                health: 38f);
            CombatantSnapshot frozenEnemy = Create(
                3,
                Hostile,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(4f, 0f, 0f),
                auraTags: AuraTag.Frozen | AuraTag.Root);
            CombatantSnapshot otherEnemy = Create(
                4,
                Hostile,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(7f, 0f, 0f));

            ArenaTeamIntent intent = coordinator.Build(
                Friendly,
                new[] { safeAlly, pressuredAlly, otherEnemy, frozenEnemy },
                4);

            Assert.That(intent.PressuredAlly, Is.EqualTo(pressuredAlly.Id));
            Assert.That(intent.ActiveControlledEnemy, Is.EqualTo(frozenEnemy.Id));
            Assert.That(intent.FocusTarget, Is.EqualTo(frozenEnemy.Id));
        }

        [Test]
        public void Build_ExcludesStealthedAndDeadEnemies()
        {
            ArenaTeamCoordinator coordinator = new ArenaTeamCoordinator();
            CombatantSnapshot ally = Create(
                1,
                Friendly,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            CombatantSnapshot stealthed = Create(
                3,
                Hostile,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                new Float3(2f, 0f, 0f),
                auraTags: AuraTag.Stealth);
            CombatantSnapshot dead = Create(
                4,
                Hostile,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(3f, 0f, 0f),
                alive: false);

            ArenaTeamIntent intent = coordinator.Build(
                Friendly,
                new[] { ally, stealthed, dead },
                3);

            Assert.That(intent.FocusTarget.IsValid, Is.False);
            Assert.That(intent.ActiveControlledEnemy.IsValid, Is.False);
        }

        [Test]
        public void Build_DropsFeignDeathTargetEvenWhenItWasStableFocus()
        {
            ArenaTeamCoordinator coordinator = new ArenaTeamCoordinator();
            CombatantSnapshot ally = Create(
                1,
                Friendly,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            CombatantSnapshot hunter = Create(
                3,
                Hostile,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                new Float3(4f, 0f, 0f));

            Assert.That(
                coordinator.Build(
                    Friendly,
                    new[] { ally, hunter },
                    2).FocusTarget,
                Is.EqualTo(hunter.Id));

            CombatantSnapshot feigningHunter = Create(
                3,
                Hostile,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                new Float3(4f, 0f, 0f),
                auraTags: AuraTag.TargetingDropped);
            ArenaTeamIntent intent = coordinator.Build(
                Friendly,
                new[] { ally, feigningHunter },
                2);

            Assert.That(intent.FocusTarget.IsValid, Is.False);
        }

        [Test]
        public void Reset_ForgetsPreviousFocusWithoutChangingDeterministicTieBreak()
        {
            ArenaTeamCoordinator coordinator = new ArenaTeamCoordinator();
            CombatantSnapshot ally = Create(
                1,
                Friendly,
                ClassId.Mage,
                SpecializationId.Frost,
                default);
            CombatantSnapshot first = Create(
                3,
                Hostile,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(5f, 0f, 0f));
            CombatantSnapshot second = Create(
                4,
                Hostile,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(5f, 0f, 0f));
            CombatantSnapshot[] snapshots = { ally, second, first };

            ParticipantId before = coordinator.Build(Friendly, snapshots, 3).FocusTarget;
            coordinator.Reset();
            ParticipantId after = coordinator.Build(Friendly, snapshots, 3).FocusTarget;

            Assert.That(after, Is.EqualTo(before));
        }

        private static CombatantSnapshot Create(
            int id,
            TeamId team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            float health = 100f,
            bool alive = true,
            AuraTag auraTags = AuraTag.None)
        {
            ResourceProfile resource =
                SpecializationCatalog.GetResourceProfile(specialization);
            return new CombatantSnapshot(
                new ParticipantId(id),
                team,
                classId,
                specialization,
                position,
                new Float3(0f, 0f, 1f),
                health,
                100f,
                0f,
                AbilityId.None,
                0,
                0,
                AbilityActivationType.Instant,
                SpellSchoolMask.None,
                resource.Type,
                resource.Initial,
                resource.Maximum,
                0,
                default,
                0,
                SpellSchoolMask.None,
                alive,
                false,
                StatusFlags.None,
                auraTags);
        }
    }
}
