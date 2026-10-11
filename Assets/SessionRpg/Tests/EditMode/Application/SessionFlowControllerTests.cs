using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class SessionFlowControllerTests
    {
        [Test]
        public void DefaultsAndClassSelection_AcceptOnlyPlayableClassesAtHome()
        {
            var flow = new SessionFlowController();
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Home));
            Assert.That(flow.Mode, Is.EqualTo(GameModeId.Arena));
            Assert.That(flow.SelectedClass, Is.EqualTo(ClassId.Mage));
            Assert.That(flow.SelectedSpecialization, Is.EqualTo(SpecializationId.Frost));

            flow.SelectClass(ClassId.Healer);
            Assert.That(flow.SelectedClass, Is.EqualTo(ClassId.Healer));
            Assert.That(flow.SelectedSpecialization, Is.EqualTo(SpecializationId.Discipline));
            flow.SelectClass(ClassId.Rogue);
            Assert.That(flow.SelectedClass, Is.EqualTo(ClassId.Rogue));
            Assert.That(flow.SelectedSpecialization, Is.EqualTo(SpecializationId.ShadowDance));
            flow.SelectClass(ClassId.Hunter);
            Assert.That(flow.SelectedClass, Is.EqualTo(ClassId.Hunter));
            Assert.That(
                flow.SelectedSpecialization,
                Is.EqualTo(SpecializationId.Marksmanship));
            flow.SelectClass(ClassId.Healer);
            flow.SelectClass(ClassId.Boss);
            Assert.That(flow.SelectedClass, Is.EqualTo(ClassId.Healer));

            flow.Start(GameModeId.Arena, 0);
            flow.SelectClass(ClassId.Rogue);
            Assert.That(flow.SelectedClass, Is.EqualTo(ClassId.Healer));
        }

        [Test]
        public void SpecializationSelection_IsClassBoundAndPersistsThroughAReplay()
        {
            var flow = new SessionFlowController();
            flow.SelectSpecialization(SpecializationId.Fire);
            Assert.That(flow.SelectedSpecialization, Is.EqualTo(SpecializationId.Fire));

            flow.SelectSpecialization(SpecializationId.Mutilate);
            Assert.That(flow.SelectedSpecialization, Is.EqualTo(SpecializationId.Fire));

            flow.SelectClass(ClassId.Healer);
            flow.SelectSpecialization(SpecializationId.Shadow);
            Assert.That(flow.SelectedSpecialization, Is.EqualTo(SpecializationId.Shadow));
            flow.SelectSpecialization(SpecializationId.Marksmanship);
            Assert.That(flow.SelectedSpecialization, Is.EqualTo(SpecializationId.Shadow));

            flow.SelectClass(ClassId.Hunter);
            Assert.That(
                flow.SelectedSpecialization,
                Is.EqualTo(SpecializationId.Marksmanship));
            flow.SelectSpecialization(SpecializationId.Shadow);
            Assert.That(
                flow.SelectedSpecialization,
                Is.EqualTo(SpecializationId.Marksmanship));

            flow.SelectClass(ClassId.Mage);
            flow.SelectSpecialization(SpecializationId.Fire);

            flow.Start(GameModeId.Arena, 0);
            flow.SelectSpecialization(SpecializationId.Frost);
            Assert.That(flow.SelectedSpecialization, Is.EqualTo(SpecializationId.Fire));
            flow.Complete(true, "done");
            flow.Start(GameModeId.Arena, 0);

            Assert.That(flow.ViewState.SelectedSpecialization, Is.EqualTo(SpecializationId.Fire));
        }

        [Test]
        public void Countdown_TransitionsExactlyAndStartsElapsedTimeOnFollowingTick()
        {
            var flow = new SessionFlowController();
            flow.Start(GameModeId.Boss, 3);
            Assert.That(flow.ViewState.Phase, Is.EqualTo(SessionPhase.Countdown));
            Assert.That(flow.ViewState.CountdownTicks, Is.EqualTo(3));

            flow.Tick();
            Assert.That(flow.ViewState.CountdownTicks, Is.EqualTo(2));
            flow.Tick();
            Assert.That(flow.ViewState.CountdownTicks, Is.EqualTo(1));
            flow.Tick();
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Playing));
            Assert.That(flow.ViewState.CountdownTicks, Is.Zero);
            Assert.That(flow.ViewState.ElapsedTicks, Is.Zero);

            flow.Tick();
            Assert.That(flow.ViewState.ElapsedTicks, Is.EqualTo(1));
        }

        [Test]
        public void LevelingMode_IsAnIndependentOptionalSessionRoute()
        {
            var flow = new SessionFlowController();

            flow.Start(GameModeId.Leveling, 2);

            Assert.That(flow.Mode, Is.EqualTo(GameModeId.Leveling));
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Countdown));
            flow.Tick();
            flow.Tick();
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Playing));
        }

        [Test]
        public void CompleteAndReplay_PreserveSelectionButResetResultAndTiming()
        {
            var flow = new SessionFlowController();
            flow.SelectClass(ClassId.Rogue);
            flow.Start(GameModeId.Arena, 0);
            flow.Tick();
            flow.Tick();
            flow.Complete(true, "Enemy team defeated");

            SessionViewState result = flow.ViewState;
            Assert.That(result.Phase, Is.EqualTo(SessionPhase.Results));
            Assert.That(result.DidWin, Is.True);
            Assert.That(result.ResultReason, Is.EqualTo("Enemy team defeated"));
            Assert.That(result.ElapsedTicks, Is.EqualTo(2));

            flow.Start(GameModeId.Boss, 1);
            SessionViewState replay = flow.ViewState;
            Assert.That(replay.Phase, Is.EqualTo(SessionPhase.Countdown));
            Assert.That(replay.Mode, Is.EqualTo(GameModeId.Boss));
            Assert.That(replay.SelectedClass, Is.EqualTo(ClassId.Rogue));
            Assert.That(replay.ElapsedTicks, Is.Zero);
            Assert.That(replay.DidWin, Is.False);
            Assert.That(replay.ResultReason, Is.Empty);

            flow.ReturnHome();
            Assert.That(flow.ViewState.Phase, Is.EqualTo(SessionPhase.Home));
            Assert.That(flow.ViewState.SelectedClass, Is.EqualTo(ClassId.Rogue));
        }

        [Test]
        public void Spectating_ContinuesElapsedTimeAndCanCompleteTheSession()
        {
            var flow = new SessionFlowController();
            flow.Start(GameModeId.Arena, 0);
            flow.Tick();

            flow.EnterSpectating();
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Spectating));
            Assert.That(flow.ViewState.ElapsedTicks, Is.EqualTo(1));

            flow.Tick();
            flow.Tick();
            Assert.That(flow.ViewState.ElapsedTicks, Is.EqualTo(3));

            flow.Complete(false, "Ally fell last");
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Results));
            Assert.That(flow.ViewState.ResultReason, Is.EqualTo("Ally fell last"));
        }

        [Test]
        public void ResumePlaying_RevivesControlOnlyFromSpectating()
        {
            var flow = new SessionFlowController();
            flow.ResumePlaying();
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Home));

            flow.Start(GameModeId.Dungeon, 0);
            flow.EnterSpectating();
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Spectating));
            flow.ResumePlaying();
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Playing));
        }

        [Test]
        public void EnterSpectating_OnlyTransitionsFromActivePlay()
        {
            var flow = new SessionFlowController();
            flow.EnterSpectating();
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Home));

            flow.Start(GameModeId.Boss, 1);
            flow.EnterSpectating();
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Countdown));

            flow.Tick();
            flow.EnterSpectating();
            flow.Complete(false, "done");
            flow.EnterSpectating();
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Results));
        }

        [Test]
        public void InvalidModeAndOutOfPhaseCompletion_DoNotMutateSession()
        {
            var flow = new SessionFlowController();
            flow.Start((GameModeId)99, 0);
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Home));
            Assert.That(flow.Mode, Is.EqualTo(GameModeId.Arena));

            flow.Start(GameModeId.Arena, 2);
            flow.Complete(true, "too early");
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Countdown));
            Assert.That(flow.ViewState.DidWin, Is.False);
            Assert.That(flow.ViewState.ResultReason, Is.Empty);

            flow.Start(GameModeId.Boss, 0);
            Assert.That(flow.Mode, Is.EqualTo(GameModeId.Arena));
            Assert.That(flow.Phase, Is.EqualTo(SessionPhase.Countdown));
        }
    }
}
