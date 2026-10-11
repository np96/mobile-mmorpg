using System;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class GuidedTrainingControllerTests
    {
        private static readonly ParticipantId Target = new ParticipantId(2);
        private static readonly Float3 HazardEntryPosition =
            new Float3(12f, 0f, 8f);
        private static readonly Float3 RecoveredPosition =
            new Float3(13f, 0f, 8f);

        [TestCase(SpecializationId.Discipline)]
        [TestCase(SpecializationId.Fire)]
        [TestCase(SpecializationId.Frost)]
        [TestCase(SpecializationId.ShadowDance)]
        [TestCase(SpecializationId.Mutilate)]
        [TestCase(SpecializationId.Shadow)]
        [TestCase(SpecializationId.Marksmanship)]
        public void BasicCombat_RequiresTheSpecializationsAuthoredOpener(
            SpecializationId specialization)
        {
            var controller = new GuidedTrainingController(specialization);
            Assert.That(controller.Start(), Is.True);
            Assert.That(
                controller.Observe(GuidedTrainingObservation.Moved()).WasHandled,
                Is.True);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.SelectedTarget(Target)).WasHandled,
                Is.True);

            AbilityId otherStartingAction =
                GuidedTrainingAbilityCatalog.GetAbility(specialization, 1);
            GuidedTrainingObservationResult otherAction = controller.Observe(
                GuidedTrainingObservation.ResolvedCombatAction(
                    otherStartingAction));

            Assert.That(otherAction.StepAdvanced, Is.False);
            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.BasicCombat));

            AbilityId expectedOpener =
                GuidedTrainingAbilityCatalog.GetBasicCombatAbility(specialization);
            Assert.That(
                expectedOpener,
                Is.EqualTo(
                    GuidedTrainingAbilityCatalog.GetAbility(
                        specialization,
                        GuidedTrainingAbilityCatalog.BasicCombatProgressionSlot)));
            GuidedTrainingObservationResult opener = controller.Observe(
                GuidedTrainingObservation.ResolvedCombatAction(expectedOpener));

            Assert.That(opener.WasHandled, Is.True);
            Assert.That(opener.StepAdvanced, Is.True);
            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.HazardRecovery));
        }

        [TestCase(SpecializationId.Discipline)]
        [TestCase(SpecializationId.Fire)]
        [TestCase(SpecializationId.Frost)]
        [TestCase(SpecializationId.ShadowDance)]
        [TestCase(SpecializationId.Mutilate)]
        [TestCase(SpecializationId.Shadow)]
        [TestCase(SpecializationId.Marksmanship)]
        public void HazardRecovery_RequiresPostSimulationSafeDisplacement(
            SpecializationId specialization)
        {
            GuidedTrainingController controller = ReachHazardRecovery(
                specialization);

            Assert.That(
                controller.Observe(GuidedTrainingObservation.Moved()).WasHandled,
                Is.False,
                "Movement intent alone must not prove recovery.");
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.EnteredHazard()).WasHandled,
                Is.True,
                "The legacy event remains observable but carries no state evidence.");
            Assert.That(controller.Snapshot.HazardEntered, Is.True);
            Assert.That(
                controller.Snapshot.HazardRecoveryEvidenceReady,
                Is.False);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.RecoveredFromHazard()).WasHandled,
                Is.False,
                "The legacy recovery signal cannot complete the lesson.");

            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.EnteredHazard(
                        HazardEntryPosition)).WasHandled,
                Is.True);
            Assert.That(
                controller.Snapshot.HazardRecoveryEvidenceReady,
                Is.True);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ObservedHazardRecovery(
                        RecoveredPosition,
                        true)).WasHandled,
                Is.False,
                "Displacement while still in danger is not recovery.");
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ObservedHazardRecovery(
                        HazardEntryPosition + new Float3(0.25f, 0f, 0f),
                        false)).WasHandled,
                Is.False,
                "A safe sample without meaningful displacement is not recovery.");

            GuidedTrainingObservationResult recovered = controller.Observe(
                GuidedTrainingObservation.ObservedHazardRecovery(
                    RecoveredPosition,
                    false));

            Assert.That(recovered.WasHandled, Is.True);
            Assert.That(recovered.StepAdvanced, Is.True);
            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.AbilityProgression));
        }

        [TestCase(SpecializationId.Discipline)]
        [TestCase(SpecializationId.Fire)]
        [TestCase(SpecializationId.Frost)]
        [TestCase(SpecializationId.ShadowDance)]
        [TestCase(SpecializationId.Mutilate)]
        [TestCase(SpecializationId.Shadow)]
        [TestCase(SpecializationId.Marksmanship)]
        public void PlayableSpecialization_UnlocksOneActionPerFirstFourPacks(
            SpecializationId specialization)
        {
            GuidedTrainingController controller =
                ReachAbilityProgression(specialization);

            Assert.That(
                controller.UnlockedAbilityCount,
                Is.EqualTo(GuidedTrainingAbilityCatalog.StartingAbilityCount));
            for (int slot = 0;
                 slot < GuidedTrainingAbilityCatalog.StartingAbilityCount;
                 slot++)
            {
                AbilityId ability = GuidedTrainingAbilityCatalog.GetAbility(
                    specialization,
                    slot);
                Assert.That(ability, Is.Not.EqualTo(AbilityId.None));
                Assert.That(
                    GuidedTrainingAbilityCatalog.IsUnlocked(
                        specialization,
                        ability,
                        controller.ClearedPackCount),
                    Is.True);
            }

            AbilityId firstLocked = GuidedTrainingAbilityCatalog.GetAbility(
                specialization,
                GuidedTrainingAbilityCatalog.StartingAbilityCount);
            Assert.That(
                GuidedTrainingAbilityCatalog.IsUnlocked(
                    specialization,
                    firstLocked,
                    controller.ClearedPackCount),
                Is.False);

            for (int packNumber = 1;
                 packNumber <= GuidedTrainingAbilityCatalog.UnlockingPackCount;
                 packNumber++)
            {
                GuidedTrainingObservationResult result = controller.Observe(
                    GuidedTrainingObservation.ClearedPack(packNumber));
                int expectedCount =
                    GuidedTrainingAbilityCatalog.StartingAbilityCount +
                    packNumber;

                Assert.That(result.WasHandled, Is.True);
                Assert.That(result.AbilityUnlocked, Is.True);
                Assert.That(result.UnlockedAbilityCount, Is.EqualTo(expectedCount));
                Assert.That(controller.UnlockedAbilityCount, Is.EqualTo(expectedCount));
                Assert.That(
                    result.UnlockedAbility,
                    Is.EqualTo(
                        GuidedTrainingAbilityCatalog.GetAbility(
                            specialization,
                            expectedCount - 1)));
            }

            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.RecoveryDefense));
            GuidedTrainingObservationResult recovery = controller.Observe(
                GuidedTrainingObservation.ResolvedCombatAction(
                    FindLessonAbility(specialization, true)));
            Assert.That(recovery.WasHandled, Is.True);
            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.ControlInterrupt));
            GuidedTrainingObservationResult control = controller.Observe(
                GuidedTrainingObservation.ResolvedCombatAction(
                    FindLessonAbility(specialization, false)));
            Assert.That(control.WasHandled, Is.True);
            Assert.That(controller.CurrentStep, Is.EqualTo(GuidedTrainingStep.FinalPack));
            GuidedTrainingObservationResult finalPack = controller.Observe(
                GuidedTrainingObservation.ClearedPack(
                    GuidedTrainingAbilityCatalog.FinalPackNumber));
            Assert.That(finalPack.WasHandled, Is.True);
            Assert.That(finalPack.AbilityUnlocked, Is.False);
            Assert.That(
                finalPack.UnlockedAbilityCount,
                Is.EqualTo(GuidedTrainingAbilityCatalog.TotalAbilityCount));
            Assert.That(controller.CurrentStep, Is.EqualTo(GuidedTrainingStep.FlickMastery));

            GuidedTrainingObservationResult flick = controller.Observe(
                GuidedTrainingObservation.ResolvedFlickAction(
                    GuidedTrainingAbilityCatalog.GetAbility(
                        specialization,
                        GuidedTrainingAbilityCatalog.TotalAbilityCount - 1),
                    AbilitySlotDirection.Up));
            Assert.That(flick.TrainingCompleted, Is.True);
            Assert.That(controller.Status, Is.EqualTo(GuidedTrainingStatus.Completed));
        }

        [Test]
        public void NineSteps_AdvanceOnlyOnTheirRequiredObservations()
        {
            var controller = new GuidedTrainingController(
                SpecializationId.Discipline);

            Assert.That(controller.Status, Is.EqualTo(GuidedTrainingStatus.Recommended));
            Assert.That(controller.IsRecommended, Is.True);
            Assert.That(controller.CanSkip, Is.True);
            Assert.That(controller.CurrentStep, Is.EqualTo(GuidedTrainingStep.Movement));
            Assert.That(controller.CompletedStepCount, Is.Zero);
            Assert.That(controller.Start(), Is.True);
            Assert.That(controller.Start(), Is.False);

            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.SelectedTarget(Target)).WasHandled,
                Is.False);
            AssertAdvance(
                controller,
                GuidedTrainingObservation.Moved(),
                GuidedTrainingStep.Targeting,
                1);
            AssertAdvance(
                controller,
                GuidedTrainingObservation.SelectedTarget(Target),
                GuidedTrainingStep.BasicCombat,
                2);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ResolvedCombatAction(
                        AbilityId.None)).WasHandled,
                Is.False);
            AssertAdvance(
                controller,
                GuidedTrainingObservation.ResolvedCombatAction(AbilityId.Penance),
                GuidedTrainingStep.HazardRecovery,
                3);

            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.RecoveredFromHazard()).WasHandled,
                Is.False);
            GuidedTrainingObservationResult entered = controller.Observe(
                GuidedTrainingObservation.EnteredHazard(HazardEntryPosition));
            Assert.That(entered.WasHandled, Is.True);
            Assert.That(entered.StepAdvanced, Is.False);
            Assert.That(controller.Snapshot.HazardEntered, Is.True);
            Assert.That(
                controller.Observe(GuidedTrainingObservation.Moved()).WasHandled,
                Is.False);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ObservedHazardRecovery(
                        RecoveredPosition,
                        true)).WasHandled,
                Is.False);
            AssertAdvance(
                controller,
                GuidedTrainingObservation.ObservedHazardRecovery(
                    RecoveredPosition,
                    false),
                GuidedTrainingStep.AbilityProgression,
                4);

            for (int packNumber = 1; packNumber <= 3; packNumber++)
            {
                GuidedTrainingObservationResult pack = controller.Observe(
                    GuidedTrainingObservation.ClearedPack(packNumber));
                Assert.That(pack.WasHandled, Is.True);
                Assert.That(pack.StepAdvanced, Is.False);
                Assert.That(
                    controller.CurrentStep,
                    Is.EqualTo(GuidedTrainingStep.AbilityProgression));
            }

            AssertAdvance(
                controller,
                GuidedTrainingObservation.ClearedPack(4),
                GuidedTrainingStep.RecoveryDefense,
                5);
            GuidedTrainingObservationResult earlyControl = controller.Observe(
                GuidedTrainingObservation.ResolvedCombatAction(
                    AbilityId.PsychicScream));
            Assert.That(earlyControl.WasHandled, Is.True);
            Assert.That(earlyControl.StepAdvanced, Is.False);
            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.RecoveryDefense));
            AssertAdvance(
                controller,
                GuidedTrainingObservation.ResolvedCombatAction(
                    AbilityId.PowerWordShield),
                GuidedTrainingStep.FinalPack,
                7);
            AssertAdvance(
                controller,
                GuidedTrainingObservation.ClearedPack(5),
                GuidedTrainingStep.FlickMastery,
                8);

            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ResolvedFlickAction(
                        AbilityId.PowerInfusion,
                        AbilitySlotDirection.None)).WasHandled,
                Is.False);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ResolvedFlickAction(
                        AbilityId.Penance,
                        AbilitySlotDirection.Up)).WasHandled,
                Is.False);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ResolvedFlickAction(
                        AbilityId.PowerInfusion,
                        AbilitySlotDirection.Left)).WasHandled,
                Is.False);
            GuidedTrainingObservationResult completed = controller.Observe(
                GuidedTrainingObservation.ResolvedFlickAction(
                    AbilityId.PowerInfusion,
                    AbilitySlotDirection.Up));
            Assert.That(completed.WasHandled, Is.True);
            Assert.That(completed.StepAdvanced, Is.True);
            Assert.That(completed.TrainingCompleted, Is.True);
            Assert.That(completed.CompletedStepCount, Is.EqualTo(9));
            Assert.That(controller.CompletedStepCount, Is.EqualTo(9));
            Assert.That(controller.CanSkip, Is.False);
            Assert.That(
                controller.Observe(GuidedTrainingObservation.Moved()).WasHandled,
                Is.False);
        }

        [Test]
        public void PackObservations_AreOrderedAndCanAccumulateDuringHazardCoaching()
        {
            var controller = new GuidedTrainingController(SpecializationId.Fire);
            controller.Observe(GuidedTrainingObservation.Moved());
            controller.Observe(GuidedTrainingObservation.SelectedTarget(Target));
            controller.Observe(
                GuidedTrainingObservation.ResolvedCombatAction(AbilityId.Fireball));

            Assert.That(controller.CurrentStep, Is.EqualTo(GuidedTrainingStep.HazardRecovery));
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ClearedPack(2)).WasHandled,
                Is.False);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ClearedPack(1)).AbilityUnlocked,
                Is.True);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ClearedPack(1)).WasHandled,
                Is.False);
            Assert.That(controller.ClearedPackCount, Is.EqualTo(1));
            Assert.That(controller.UnlockedAbilityCount, Is.EqualTo(4));

            controller.Observe(
                GuidedTrainingObservation.EnteredHazard(HazardEntryPosition));
            GuidedTrainingObservationResult recovered = controller.Observe(
                GuidedTrainingObservation.ObservedHazardRecovery(
                    RecoveredPosition,
                    false));
            Assert.That(recovered.WasHandled, Is.True);
            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.AbilityProgression));
        }

        [Test]
        public void PackClearsBeforePackLesson_AreRetainedWithoutSkippingPrerequisites()
        {
            var controller = new GuidedTrainingController(SpecializationId.Fire);
            Assert.That(controller.Start(), Is.True);

            GuidedTrainingObservationResult earlyPack = controller.Observe(
                GuidedTrainingObservation.ClearedPack(1));

            Assert.That(earlyPack.WasHandled, Is.True);
            Assert.That(earlyPack.AbilityUnlocked, Is.True);
            Assert.That(controller.ClearedPackCount, Is.EqualTo(1));
            Assert.That(controller.CurrentStep, Is.EqualTo(GuidedTrainingStep.Movement));

            Assert.That(
                controller.Observe(GuidedTrainingObservation.Moved()).WasHandled,
                Is.True);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.SelectedTarget(Target)).WasHandled,
                Is.True);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ResolvedCombatAction(
                        AbilityId.Fireball)).WasHandled,
                Is.True);
            Assert.That(controller.CurrentStep, Is.EqualTo(GuidedTrainingStep.HazardRecovery));
        }

        [Test]
        public void ValidAbilityLessonsResolvedEarly_AreConsumedAfterFourthPack()
        {
            GuidedTrainingController controller = ReachAbilityProgression(
                SpecializationId.Fire);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ClearedPack(1)).WasHandled,
                Is.True);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ClearedPack(2)).WasHandled,
                Is.True);

            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ResolvedCombatAction(
                        AbilityId.Blink)).WasHandled,
                Is.True);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ResolvedCombatAction(
                        AbilityId.Polymorph)).WasHandled,
                Is.True);
            Assert.That(controller.CurrentStep, Is.EqualTo(GuidedTrainingStep.AbilityProgression));

            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ClearedPack(3)).WasHandled,
                Is.True);
            GuidedTrainingObservationResult fourthPack = controller.Observe(
                GuidedTrainingObservation.ClearedPack(4));

            Assert.That(fourthPack.StepAdvanced, Is.True);
            Assert.That(controller.CurrentStep, Is.EqualTo(GuidedTrainingStep.FinalPack));
        }

        [Test]
        public void FinalPackClearedEarly_AdvancesAfterRequiredAbilityLessons()
        {
            GuidedTrainingController controller = ReachAbilityProgression(
                SpecializationId.Frost);
            for (int packNumber = 1; packNumber <= 5; packNumber++)
            {
                Assert.That(
                    controller.Observe(
                        GuidedTrainingObservation.ClearedPack(packNumber))
                        .WasHandled,
                    Is.True);
            }

            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.RecoveryDefense));
            controller.Observe(
                GuidedTrainingObservation.ResolvedCombatAction(
                    AbilityId.IceBarrier));
            GuidedTrainingObservationResult control = controller.Observe(
                GuidedTrainingObservation.ResolvedCombatAction(
                    AbilityId.DeepFreeze));

            Assert.That(control.StepAdvanced, Is.True);
            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.FlickMastery));
        }

        [Test]
        public void SkipAndReset_PreserveExplicitLocalOutcomeSemantics()
        {
            var controller = new GuidedTrainingController(
                SpecializationId.Marksmanship);
            controller.Observe(GuidedTrainingObservation.Moved());

            Assert.That(controller.Status, Is.EqualTo(GuidedTrainingStatus.InProgress));
            Assert.That(controller.Skip(), Is.True);
            Assert.That(controller.Status, Is.EqualTo(GuidedTrainingStatus.Skipped));
            Assert.That(controller.IsRecommended, Is.False);
            Assert.That(controller.CanSkip, Is.False);
            Assert.That(
                controller.UnlockedAbilityCount,
                Is.EqualTo(GuidedTrainingAbilityCatalog.TotalAbilityCount));
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.SelectedTarget(Target)).WasHandled,
                Is.False);
            Assert.That(controller.Skip(), Is.False);

            controller.Reset();
            Assert.That(controller.Status, Is.EqualTo(GuidedTrainingStatus.Recommended));
            Assert.That(controller.CurrentStep, Is.EqualTo(GuidedTrainingStep.Movement));
            Assert.That(controller.CompletedStepCount, Is.Zero);
            Assert.That(controller.ClearedPackCount, Is.Zero);
            Assert.That(
                controller.UnlockedAbilityCount,
                Is.EqualTo(GuidedTrainingAbilityCatalog.StartingAbilityCount));
            Assert.That(controller.Snapshot.HazardEntered, Is.False);
        }

        [Test]
        public void NonPlayableSpecializations_AreRejected()
        {
            Assert.That(
                () => new GuidedTrainingController(SpecializationId.None),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => new GuidedTrainingController(SpecializationId.Boss),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        [Test]
        public void PracticeTarget_UsesTheOnlyFreeStableRouteIdentity()
        {
            Assert.That(GuidedTrainingPracticeTargetCatalog.Spawn.IsValid, Is.True);
            Assert.That(
                GuidedTrainingPracticeTargetCatalog.Participant,
                Is.EqualTo(new ParticipantId(12)));
            Assert.That(
                GuidedTrainingPracticeTargetCatalog.MaximumHealth,
                Is.GreaterThan(0f));
            Assert.That(
                GuidedTrainingPracticeTargetCatalog.Position.X,
                Is.GreaterThan(
                    LevelingZoneCatalog.AshenCauseway.GetPack(4).EncounterX));
            Assert.That(
                CombatSimulation.MaxCombatants,
                Is.EqualTo(
                    1 +
                    CountRouteMobIdentities() +
                    1));
        }

        private static GuidedTrainingController ReachAbilityProgression(
            SpecializationId specialization)
        {
            GuidedTrainingController controller = ReachHazardRecovery(
                specialization);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.EnteredHazard(
                        HazardEntryPosition)).WasHandled,
                Is.True);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ObservedHazardRecovery(
                        RecoveredPosition,
                        false)).WasHandled,
                Is.True);
            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.AbilityProgression));
            return controller;
        }

        private static GuidedTrainingController ReachHazardRecovery(
            SpecializationId specialization)
        {
            var controller = new GuidedTrainingController(specialization);
            Assert.That(controller.Start(), Is.True);
            Assert.That(
                controller.Observe(GuidedTrainingObservation.Moved()).WasHandled,
                Is.True);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.SelectedTarget(Target)).WasHandled,
                Is.True);
            Assert.That(
                controller.Observe(
                    GuidedTrainingObservation.ResolvedCombatAction(
                        GuidedTrainingAbilityCatalog.GetBasicCombatAbility(
                            specialization))).WasHandled,
                Is.True);
            Assert.That(
                controller.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.HazardRecovery));
            return controller;
        }

        private static void AssertAdvance(
            GuidedTrainingController controller,
            GuidedTrainingObservation observation,
            GuidedTrainingStep expectedStep,
            int expectedCompletedStepCount)
        {
            GuidedTrainingObservationResult result = controller.Observe(observation);
            Assert.That(result.WasHandled, Is.True);
            Assert.That(result.StepAdvanced, Is.True);
            Assert.That(result.StepAfter, Is.EqualTo(expectedStep));
            Assert.That(
                result.CompletedStepCount,
                Is.EqualTo(expectedCompletedStepCount));
            Assert.That(controller.CurrentStep, Is.EqualTo(expectedStep));
        }

        private static AbilityId FindLessonAbility(
            SpecializationId specialization,
            bool recoveryOrDefense)
        {
            for (int slot = 0;
                 slot < GuidedTrainingAbilityCatalog.TotalAbilityCount;
                 slot++)
            {
                AbilityId ability = GuidedTrainingAbilityCatalog.GetAbility(
                    specialization,
                    slot);
                bool matches = recoveryOrDefense
                    ? GuidedTrainingAbilityCatalog.IsRecoveryOrDefense(ability)
                    : GuidedTrainingAbilityCatalog.IsControlOrInterrupt(ability);
                if (matches)
                {
                    return ability;
                }
            }

            Assert.Fail(
                specialization + " has no " +
                (recoveryOrDefense ? "recovery/defense" : "control/interrupt") +
                " action in its Training kit.");
            return AbilityId.None;
        }

        private static int CountRouteMobIdentities()
        {
            int count = 0;
            LevelingZoneDefinition zone = LevelingZoneCatalog.AshenCauseway;
            for (int packIndex = 0; packIndex < zone.PackCount; packIndex++)
            {
                count += zone.GetPack(packIndex).MobCount;
            }

            return count;
        }
    }
}
