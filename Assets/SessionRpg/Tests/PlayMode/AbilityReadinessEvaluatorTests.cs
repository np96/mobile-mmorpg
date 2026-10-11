using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class AbilityReadinessEvaluatorTests
    {
        private static readonly ParticipantId Human = new ParticipantId(1);
        private static readonly ParticipantId SetupMage = new ParticipantId(2);
        private static readonly ParticipantId Enemy = new ParticipantId(3);
        private static readonly TeamId FriendlyTeam = new TeamId(1);
        private static readonly TeamId HostileTeam = new TeamId(2);

        [Test]
        public void DeepFreeze_ShowsSetupBeforeRangeThenReturnsToRangeAfterFreeze()
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(simulation.AddCombatant(
                Human,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(-7.75f, 0f, 0f)), Is.True);
            Assert.That(simulation.AddCombatant(
                SetupMage,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(4.75f, 0f, 0f)), Is.True);
            Assert.That(simulation.AddCombatant(
                Enemy,
                HostileTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(7.75f, 0f, 0f)), Is.True);

            CombatCommand deepFreeze = TargetCast(
                Human,
                1,
                AbilityId.DeepFreeze,
                Enemy);
            Assert.That(simulation.CanStartAbility(
                deepFreeze,
                out string coreReasonBeforeSetup), Is.False);
            Assert.That(coreReasonBeforeSetup, Is.EqualTo("out_of_range"));

            Assert.That(AbilityReadinessEvaluator.CanStartForDisplay(
                simulation,
                deepFreeze,
                out string displayReasonBeforeSetup), Is.False);
            Assert.That(displayReasonBeforeSetup, Is.EqualTo("target_not_frozen"));

            Assert.That(simulation.SubmitCommand(TargetCast(
                SetupMage,
                1,
                AbilityId.FrostNova,
                Enemy)), Is.True);
            simulation.Tick();
            Assert.That(simulation.HasAura(Enemy, AuraId.FrozenRoot), Is.True);

            Assert.That(AbilityReadinessEvaluator.CanStartForDisplay(
                simulation,
                deepFreeze,
                out string displayReasonAfterSetup), Is.False);
            Assert.That(displayReasonAfterSetup, Is.EqualTo("out_of_range"));
        }

        [Test]
        public void ActorStateBlocker_RemainsStrongerThanTargetSetup()
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(simulation.AddCombatant(
                Human,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                default), Is.True);
            Assert.That(simulation.AddCombatant(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(0f, 0f, 3f)), Is.True);

            Assert.That(simulation.SubmitCommand(TargetCast(
                Human,
                1,
                AbilityId.IceBarrier,
                Human)), Is.True);
            simulation.Tick();

            Assert.That(AbilityReadinessEvaluator.CanStartForDisplay(
                simulation,
                TargetCast(Human, 2, AbilityId.DeepFreeze, Enemy),
                out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("global_cooldown"));
        }

        [Test]
        public void PrerequisiteEvaluator_CoversAllTargetSetupRequirements()
        {
            CombatantSnapshot mage = Snapshot(
                Human,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            CombatantSnapshot enemy = Snapshot(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(0f, 0f, 4f));
            AssertPrerequisite(
                AbilityId.Counterspell,
                mage,
                enemy,
                "target_not_casting");

            CombatantSnapshot rogue = Snapshot(
                Human,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                new Float3(0f, 0f, 4f));
            AssertPrerequisite(
                AbilityId.Ambush,
                rogue,
                enemy,
                "requires_stealth");

            CombatantSnapshot stealthedRogue = Snapshot(
                Human,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                new Float3(0f, 0f, 4f),
                auraTags: AuraTag.StealthAccess);
            AssertPrerequisite(
                AbilityId.Ambush,
                stealthedRogue,
                enemy,
                "requires_behind");

            AssertPrerequisite(
                AbilityId.Eviscerate,
                rogue,
                enemy,
                "combo_points");

            CombatantSnapshot comboRogue = Snapshot(
                Human,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default,
                comboPoints: 4,
                comboTarget: Enemy);
            AssertPrerequisite(
                AbilityId.Envenom,
                comboRogue,
                enemy,
                "target_not_poisoned");

            AssertPrerequisite(
                AbilityId.DeepFreeze,
                mage,
                enemy,
                "target_not_frozen");

            CombatantSnapshot weakenedAlly = Snapshot(
                Enemy,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(0f, 0f, 4f),
                auraTags: AuraTag.PreventsShield);
            AssertPrerequisite(
                AbilityId.PowerWordShield,
                Snapshot(
                    Human,
                    FriendlyTeam,
                    ClassId.Priest,
                    SpecializationId.Discipline,
                    default),
                weakenedAlly,
                "weakened_soul");
        }

        private static void AssertPrerequisite(
            AbilityId ability,
            CombatantSnapshot actor,
            CombatantSnapshot target,
            string expectedReason)
        {
            Assert.That(AbilityReadinessEvaluator.TryGetPrerequisiteFailure(
                AbilityCatalog.Get(ability),
                actor,
                target,
                out string reason), Is.True);
            Assert.That(reason, Is.EqualTo(expectedReason));
        }

        private static CombatCommand TargetCast(
            ParticipantId actor,
            uint sequence,
            AbilityId ability,
            ParticipantId target)
        {
            return CombatCommand.Cast(
                actor,
                sequence,
                default,
                new Float3(1f, 0f, 0f),
                ability,
                target,
                default,
                new Float3(1f, 0f, 0f));
        }

        private static CombatantSnapshot Snapshot(
            ParticipantId participant,
            TeamId team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            int comboPoints = 0,
            ParticipantId comboTarget = default,
            AbilityId castingAbility = AbilityId.None,
            AuraTag auraTags = AuraTag.None)
        {
            ResourceProfile resource =
                SpecializationCatalog.GetResourceProfile(specialization);
            return new CombatantSnapshot(
                participant,
                team,
                classId,
                specialization,
                position,
                new Float3(0f, 0f, 1f),
                100f,
                100f,
                0f,
                castingAbility,
                0,
                castingAbility == AbilityId.None ? 0 : 50,
                castingAbility == AbilityId.None
                    ? AbilityActivationType.Instant
                    : AbilityActivationType.Cast,
                SpellSchoolMask.None,
                resource.Type,
                resource.Initial,
                resource.Maximum,
                comboPoints,
                comboTarget,
                0,
                SpellSchoolMask.None,
                true,
                false,
                StatusFlags.None,
                auraTags);
        }
    }
}
