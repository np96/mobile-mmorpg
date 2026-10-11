using System;

namespace SessionRpg.Core
{
    /// <summary>
    /// Deterministic cumulative-experience curve for the first leveling slice.
    /// The six levels are intentionally represented without collections so the
    /// curve is safe to query from allocation-sensitive simulation code.
    /// </summary>
    public static class LevelProgressionCurve
    {
        public const int MinimumLevel = 1;
        public const int MaximumLevel = 6;
        public const int MaximumExperience = 1130;
        public const float HealthPerGainedLevel = 8f;
        public const float DamagePerGainedLevel = 0.06f;

        public static int GetCumulativeExperienceForLevel(int level)
        {
            switch (level)
            {
                case 1:
                    return 0;
                case 2:
                    return 80;
                case 3:
                    return 200;
                case 4:
                    return 380;
                case 5:
                    return 680;
                case 6:
                    return MaximumExperience;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(level),
                        level,
                        "Level must be between 1 and 6.");
            }
        }

        public static int GetLevelForExperience(int totalExperience)
        {
            int clamped = ClampExperience(totalExperience);
            if (clamped >= MaximumExperience)
            {
                return 6;
            }

            if (clamped >= 680)
            {
                return 5;
            }

            if (clamped >= 380)
            {
                return 4;
            }

            if (clamped >= 200)
            {
                return 3;
            }

            return clamped >= 80 ? 2 : 1;
        }

        public static int ClampLevel(int level)
        {
            if (level < MinimumLevel)
            {
                return MinimumLevel;
            }

            return level > MaximumLevel ? MaximumLevel : level;
        }

        internal static int ClampExperience(int totalExperience)
        {
            if (totalExperience <= 0)
            {
                return 0;
            }

            return totalExperience >= MaximumExperience
                ? MaximumExperience
                : totalExperience;
        }
    }

    public readonly struct LevelProgressionGrantResult
    {
        internal LevelProgressionGrantResult(
            int previousExperience,
            int currentExperience,
            int previousLevel,
            int currentLevel)
        {
            PreviousExperience = previousExperience;
            CurrentExperience = currentExperience;
            AppliedExperience = currentExperience - previousExperience;
            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
            LevelsGained = currentLevel - previousLevel;
        }

        public int PreviousExperience { get; }

        public int CurrentExperience { get; }

        public int AppliedExperience { get; }

        public int PreviousLevel { get; }

        public int CurrentLevel { get; }

        public int LevelsGained { get; }

        public bool DidLevelUp => LevelsGained > 0;
    }

    /// <summary>
    /// Mutable, allocation-free progression state. Its default value is a valid
    /// level-one state with zero experience.
    /// </summary>
    public struct LevelProgressionState
    {
        private int _totalExperience;

        public LevelProgressionState(int totalExperience)
        {
            _totalExperience = LevelProgressionCurve.ClampExperience(totalExperience);
        }

        public int TotalExperience => _totalExperience;

        public int Level => LevelProgressionCurve.GetLevelForExperience(_totalExperience);

        public bool IsMaximumLevel => Level == LevelProgressionCurve.MaximumLevel;

        public int ExperienceIntoLevel => _totalExperience -
            LevelProgressionCurve.GetCumulativeExperienceForLevel(Level);

        public int ExperienceToNextLevel
        {
            get
            {
                int level = Level;
                return level >= LevelProgressionCurve.MaximumLevel
                    ? 0
                    : LevelProgressionCurve.GetCumulativeExperienceForLevel(level + 1) -
                      _totalExperience;
            }
        }

        public LevelProgressionGrantResult GrantExperience(int amount)
        {
            int previousExperience = _totalExperience;
            int previousLevel = LevelProgressionCurve.GetLevelForExperience(
                previousExperience);
            if (amount > 0 && previousExperience < LevelProgressionCurve.MaximumExperience)
            {
                long requested = (long)previousExperience + amount;
                _totalExperience = requested >= LevelProgressionCurve.MaximumExperience
                    ? LevelProgressionCurve.MaximumExperience
                    : (int)requested;
            }

            int currentLevel = LevelProgressionCurve.GetLevelForExperience(
                _totalExperience);
            return new LevelProgressionGrantResult(
                previousExperience,
                _totalExperience,
                previousLevel,
                currentLevel);
        }
    }
}
