using System;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class LevelProgressionTests
    {
        [TestCase(1, 0)]
        [TestCase(2, 80)]
        [TestCase(3, 200)]
        [TestCase(4, 380)]
        [TestCase(5, 680)]
        [TestCase(6, 1130)]
        public void Curve_UsesTheExactCumulativeThresholds(int level, int experience)
        {
            Assert.That(
                LevelProgressionCurve.GetCumulativeExperienceForLevel(level),
                Is.EqualTo(experience));
        }

        [TestCase(-1, 1)]
        [TestCase(0, 1)]
        [TestCase(79, 1)]
        [TestCase(80, 2)]
        [TestCase(199, 2)]
        [TestCase(200, 3)]
        [TestCase(379, 3)]
        [TestCase(380, 4)]
        [TestCase(679, 4)]
        [TestCase(680, 5)]
        [TestCase(1129, 5)]
        [TestCase(1130, 6)]
        [TestCase(2000, 6)]
        public void Curve_MapsExperienceAtEveryBoundary(int experience, int level)
        {
            Assert.That(
                LevelProgressionCurve.GetLevelForExperience(experience),
                Is.EqualTo(level));
        }

        [Test]
        public void Curve_RejectsLevelsOutsideTheAuthoredRange()
        {
            Assert.Throws<ArgumentOutOfRangeException>(
                () => LevelProgressionCurve.GetCumulativeExperienceForLevel(0));
            Assert.Throws<ArgumentOutOfRangeException>(
                () => LevelProgressionCurve.GetCumulativeExperienceForLevel(7));
        }

        [Test]
        public void State_DefaultsToLevelOneAndClampsConstruction()
        {
            LevelProgressionState defaultState = default;
            LevelProgressionState belowMinimum = new LevelProgressionState(-100);
            LevelProgressionState aboveMaximum = new LevelProgressionState(int.MaxValue);

            Assert.That(defaultState.TotalExperience, Is.Zero);
            Assert.That(defaultState.Level, Is.EqualTo(1));
            Assert.That(defaultState.ExperienceIntoLevel, Is.Zero);
            Assert.That(defaultState.ExperienceToNextLevel, Is.EqualTo(80));
            Assert.That(defaultState.IsMaximumLevel, Is.False);
            Assert.That(belowMinimum.TotalExperience, Is.Zero);
            Assert.That(belowMinimum.Level, Is.EqualTo(1));
            Assert.That(aboveMaximum.TotalExperience, Is.EqualTo(1130));
            Assert.That(aboveMaximum.Level, Is.EqualTo(6));
            Assert.That(aboveMaximum.ExperienceToNextLevel, Is.Zero);
            Assert.That(aboveMaximum.IsMaximumLevel, Is.True);
        }

        [Test]
        public void GrantExperience_ReportsExactAppliedExperienceAndMultipleLevelUps()
        {
            LevelProgressionState state = new LevelProgressionState(70);

            LevelProgressionGrantResult result = state.GrantExperience(620);

            Assert.That(result.PreviousExperience, Is.EqualTo(70));
            Assert.That(result.CurrentExperience, Is.EqualTo(690));
            Assert.That(result.AppliedExperience, Is.EqualTo(620));
            Assert.That(result.PreviousLevel, Is.EqualTo(1));
            Assert.That(result.CurrentLevel, Is.EqualTo(5));
            Assert.That(result.LevelsGained, Is.EqualTo(4));
            Assert.That(result.DidLevelUp, Is.True);
            Assert.That(state.TotalExperience, Is.EqualTo(690));
            Assert.That(state.Level, Is.EqualTo(5));
            Assert.That(state.ExperienceIntoLevel, Is.EqualTo(10));
            Assert.That(state.ExperienceToNextLevel, Is.EqualTo(440));
        }

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(int.MinValue)]
        public void GrantExperience_IgnoresNonpositiveAmounts(int amount)
        {
            LevelProgressionState state = new LevelProgressionState(199);

            LevelProgressionGrantResult result = state.GrantExperience(amount);

            Assert.That(state.TotalExperience, Is.EqualTo(199));
            Assert.That(state.Level, Is.EqualTo(2));
            Assert.That(result.AppliedExperience, Is.Zero);
            Assert.That(result.LevelsGained, Is.Zero);
            Assert.That(result.DidLevelUp, Is.False);
        }

        [Test]
        public void GrantExperience_CapsAtMaximumWithoutIntegerOverflow()
        {
            LevelProgressionState state = new LevelProgressionState(1129);

            LevelProgressionGrantResult result = state.GrantExperience(int.MaxValue);
            LevelProgressionGrantResult cappedResult = state.GrantExperience(10);

            Assert.That(state.TotalExperience, Is.EqualTo(1130));
            Assert.That(state.Level, Is.EqualTo(6));
            Assert.That(state.ExperienceIntoLevel, Is.Zero);
            Assert.That(state.ExperienceToNextLevel, Is.Zero);
            Assert.That(state.IsMaximumLevel, Is.True);
            Assert.That(result.AppliedExperience, Is.EqualTo(1));
            Assert.That(result.LevelsGained, Is.EqualTo(1));
            Assert.That(cappedResult.AppliedExperience, Is.Zero);
            Assert.That(cappedResult.LevelsGained, Is.Zero);
        }

        [Test]
        public void GrantExperience_WarmedLoopAllocatesNoManagedMemory()
        {
            LevelProgressionState state = default;
            LevelProgressionGrantResult result = default;
            for (int index = 0; index < 256; index++)
            {
                state = default;
                result = state.GrantExperience(index);
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 2048; index++)
            {
                state = default;
                result = state.GrantExperience(index);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero);
            Assert.That(result.CurrentExperience, Is.EqualTo(1130));
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
