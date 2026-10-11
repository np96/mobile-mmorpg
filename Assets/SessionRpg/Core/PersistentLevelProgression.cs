using System;

namespace SessionRpg.Core
{
    /// <summary>
    /// Independent prototype curve for persistent PvE grinding. Training's short
    /// demonstration curve and Arena's normalized power are deliberately unchanged.
    /// </summary>
    public static class PersistentLevelProgression
    {
        public const int MinimumLevel = 1;
        public const int MaximumLevel = 10;
        public const int MaximumExperience = 6720;
        public const float HealthPerGainedLevel = 12f;
        public const float DamagePerGainedLevel = 0.08f;

        public static int GetCumulativeExperienceForLevel(int level)
        {
            switch (level)
            {
                case 1: return 0;
                case 2: return 160;
                case 3: return 420;
                case 4: return 800;
                case 5: return 1320;
                case 6: return 2000;
                case 7: return 2860;
                case 8: return 3920;
                case 9: return 5200;
                case 10: return MaximumExperience;
                default: throw new ArgumentOutOfRangeException(nameof(level));
            }
        }

        public static int GetLevelForExperience(int totalExperience)
        {
            if (totalExperience < 0 || totalExperience > MaximumExperience)
            {
                throw new ArgumentOutOfRangeException(nameof(totalExperience));
            }

            for (int level = MaximumLevel; level > MinimumLevel; level--)
            {
                if (totalExperience >= GetCumulativeExperienceForLevel(level))
                {
                    return level;
                }
            }

            return MinimumLevel;
        }
    }
}
