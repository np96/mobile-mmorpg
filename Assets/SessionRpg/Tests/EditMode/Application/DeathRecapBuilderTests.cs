using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class DeathRecapBuilderTests
    {
        private static readonly ParticipantId Victim = new ParticipantId(1);
        private static readonly ParticipantId Mage = new ParticipantId(2);
        private static readonly ParticipantId Rogue = new ParticipantId(3);
        private static readonly ParticipantId Boss = new ParticipantId(4);

        [Test]
        public void Recap_PreservesChronologicalSetupAndSourceActors()
        {
            var builder = new DeathRecapBuilder(10);
            builder.Observe(CombatEvent.Create(
                10,
                CombatEventKind.StatusApplied,
                Mage,
                Victim,
                AbilityId.FrostNova,
                StatusId.Rooted));
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Damage,
                Mage,
                Victim,
                AbilityId.IceLance,
                amount: 18f));
            builder.Observe(CombatEvent.Create(
                30,
                CombatEventKind.Damage,
                Rogue,
                Victim,
                AbilityId.Envenom,
                amount: 40f));
            builder.Observe(CombatEvent.Create(
                30,
                CombatEventKind.Death,
                Rogue,
                Victim,
                AbilityId.Envenom,
                reason: "ability"));

            Assert.That(builder.TryGetRecap(Victim, out DeathRecap recap), Is.True);
            Assert.That(recap.Victim, Is.EqualTo(Victim));
            Assert.That(recap.CreditedActor, Is.EqualTo(Rogue));
            Assert.That(recap.FinishingAbility, Is.EqualTo(AbilityId.Envenom));
            Assert.That(recap.EndedInHazard, Is.False);
            Assert.That(recap.StepCount, Is.EqualTo(3));
            Assert.That(recap.GetStep(0).Ability, Is.EqualTo(AbilityId.FrostNova));
            Assert.That(recap.GetStep(0).Source, Is.EqualTo(Mage));
            Assert.That(recap.GetStep(0).Kind, Is.EqualTo(DeathContributionKind.Control));
            Assert.That(recap.GetStep(1).Ability, Is.EqualTo(AbilityId.IceLance));
            Assert.That(recap.GetStep(2).Ability, Is.EqualTo(AbilityId.Envenom));
            Assert.That(recap.GetStep(2).Source, Is.EqualTo(Rogue));
            Assert.That(recap.Chain, Is.EqualTo("Frost Nova → Ice Lance → Envenom"));
            Assert.That(recap.Hint, Does.Contain("combo-point"));
        }

        [Test]
        public void Contributions_ExpireOutsideEightSecondWindow()
        {
            var builder = new DeathRecapBuilder(10);
            builder.Observe(CombatEvent.Create(
                1,
                CombatEventKind.Damage,
                Mage,
                Victim,
                AbilityId.Fireball,
                amount: 20f));
            builder.Observe(CombatEvent.Create(
                82,
                CombatEventKind.Death,
                Victim,
                Victim,
                reason: "unknown"));

            Assert.That(builder.TryGetRecap(Victim, out DeathRecap recap), Is.True);
            Assert.That(recap.StepCount, Is.Zero);
            Assert.That(recap.CreditedActor.IsValid, Is.False);
            Assert.That(recap.Chain, Is.EqualTo("Defeated"));
        }

        [Test]
        public void HazardFinish_CreditsRecentKnockbackAndExplainsRecovery()
        {
            var builder = new DeathRecapBuilder(10);
            builder.Observe(CombatEvent.Create(
                50,
                CombatEventKind.Damage,
                Mage,
                Victim,
                AbilityId.FireBlast,
                amount: 12f));
            builder.Observe(CombatEvent.Create(
                60,
                CombatEventKind.Knockback,
                Boss,
                Victim,
                AbilityId.BossKnockback,
                amount: 6f,
                vector: new Float3(6f, 0f, 0f)));
            builder.Observe(CombatEvent.Create(
                65,
                CombatEventKind.HazardDamage,
                Victim,
                Victim,
                amount: 20f));
            builder.Observe(CombatEvent.Create(
                65,
                CombatEventKind.Death,
                Victim,
                Victim,
                reason: "lava"));

            Assert.That(builder.TryGetRecap(Victim, out DeathRecap recap), Is.True);
            Assert.That(recap.EndedInHazard, Is.True);
            Assert.That(recap.CreditedActor, Is.EqualTo(Boss));
            Assert.That(recap.Chain, Is.EqualTo("Fire Blast → Rift Roar → Lava"));
            Assert.That(recap.Hint, Does.Contain("save mobility"));
        }

        [Test]
        public void SimultaneousVictims_KeepIndependentRecaps()
        {
            var secondVictim = new ParticipantId(5);
            var builder = new DeathRecapBuilder(10);
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Damage,
                Mage,
                Victim,
                AbilityId.Fireball,
                amount: 50f));
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Damage,
                Rogue,
                secondVictim,
                AbilityId.Eviscerate,
                amount: 50f));
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Death,
                Mage,
                Victim,
                AbilityId.Fireball,
                reason: "ability"));
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Death,
                Rogue,
                secondVictim,
                AbilityId.Eviscerate,
                reason: "ability"));

            Assert.That(builder.TryGetRecap(Victim, out DeathRecap first), Is.True);
            Assert.That(builder.TryGetRecap(secondVictim, out DeathRecap second), Is.True);
            Assert.That(first.Chain, Is.EqualTo("Fireball"));
            Assert.That(first.CreditedActor, Is.EqualTo(Mage));
            Assert.That(second.Chain, Is.EqualTo("Eviscerate"));
            Assert.That(second.CreditedActor, Is.EqualTo(Rogue));
        }

        [Test]
        public void Reset_ClearsHistoryAndStoredRecaps()
        {
            var builder = new DeathRecapBuilder(10);
            builder.Observe(CombatEvent.Create(
                10,
                CombatEventKind.Damage,
                Mage,
                Victim,
                AbilityId.Fireball,
                amount: 50f));
            builder.Observe(CombatEvent.Create(
                10,
                CombatEventKind.Death,
                Mage,
                Victim,
                AbilityId.Fireball,
                reason: "ability"));
            Assert.That(builder.TryGetRecap(Victim, out _), Is.True);

            builder.Reset(20);

            Assert.That(builder.HistoryTicks, Is.EqualTo(160));
            Assert.That(builder.TryGetRecap(Victim, out _), Is.False);
            builder.Observe(CombatEvent.Create(
                1,
                CombatEventKind.Death,
                Victim,
                Victim,
                reason: "unknown"));
            Assert.That(builder.TryGetRecap(Victim, out DeathRecap recap), Is.True);
            Assert.That(recap.StepCount, Is.Zero);
        }

        [Test]
        public void ConsecutiveEventsFromSameAbility_AppearOnceWithLatestMeaning()
        {
            var builder = new DeathRecapBuilder(10);
            builder.Observe(CombatEvent.Create(
                10,
                CombatEventKind.Damage,
                Mage,
                Victim,
                AbilityId.FrostNova,
                amount: 8f));
            builder.Observe(CombatEvent.Create(
                10,
                CombatEventKind.StatusApplied,
                Mage,
                Victim,
                AbilityId.FrostNova,
                StatusId.Rooted));
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Damage,
                Mage,
                Victim,
                AbilityId.DeepFreeze,
                amount: 80f));
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Death,
                Mage,
                Victim,
                AbilityId.DeepFreeze,
                reason: "ability"));

            Assert.That(builder.TryGetRecap(Victim, out DeathRecap recap), Is.True);
            Assert.That(recap.StepCount, Is.EqualTo(2));
            Assert.That(recap.GetStep(0).Kind, Is.EqualTo(DeathContributionKind.Control));
            Assert.That(recap.Chain, Is.EqualTo("Frost Nova → Deep Freeze"));
            Assert.That(recap.Hint, Does.Contain("root setup"));
        }

        [Test]
        public void SelfInflictedFinisher_IsNamedWithoutCreditingTheVictim()
        {
            var builder = new DeathRecapBuilder(10);
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Damage,
                Victim,
                Victim,
                AbilityId.ShadowWordDeath,
                amount: 20f));
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Death,
                Victim,
                Victim,
                AbilityId.ShadowWordDeath,
                reason: "ability"));

            Assert.That(builder.TryGetRecap(Victim, out DeathRecap recap), Is.True);
            Assert.That(recap.Chain, Is.EqualTo("Shadow Word: Death"));
            Assert.That(recap.CreditedActor.IsValid, Is.False);
        }

        [TestCase(StatusId.Horrified)]
        [TestCase(StatusId.Disarmed)]
        [TestCase(StatusId.Pacified)]
        public void RestrictiveStatus_IsRecordedAsControl(StatusId status)
        {
            var builder = new DeathRecapBuilder(10);
            builder.Observe(CombatEvent.Create(
                10,
                CombatEventKind.StatusApplied,
                Mage,
                Victim,
                AbilityId.PsychicHorror,
                status));
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Death,
                Victim,
                Victim,
                reason: "unknown"));

            Assert.That(builder.TryGetRecap(Victim, out DeathRecap recap), Is.True);
            Assert.That(recap.StepCount, Is.EqualTo(1));
            Assert.That(
                recap.GetStep(0).Kind,
                Is.EqualTo(DeathContributionKind.Control));
        }

        [TestCase(StatusId.Barrier)]
        [TestCase(StatusId.Anchored)]
        [TestCase(StatusId.DamageReduced)]
        [TestCase(StatusId.Stealthed)]
        [TestCase(StatusId.Poisoned)]
        [TestCase(StatusId.Burning)]
        public void NonControlStatus_IsNotRecordedAsControl(StatusId status)
        {
            var builder = new DeathRecapBuilder(10);
            builder.Observe(CombatEvent.Create(
                10,
                CombatEventKind.StatusApplied,
                Mage,
                Victim,
                AbilityId.PowerWordShield,
                status));
            builder.Observe(CombatEvent.Create(
                20,
                CombatEventKind.Death,
                Victim,
                Victim,
                reason: "unknown"));

            Assert.That(builder.TryGetRecap(Victim, out DeathRecap recap), Is.True);
            Assert.That(recap.StepCount, Is.Zero);
        }
    }
}
