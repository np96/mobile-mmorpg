using System;
using SessionRpg.Core;

namespace SessionRpg.Editor.Assets
{
    /// <summary>
    /// Explicit, source-level switch for playable-class visual sources. The legacy
    /// trial name remains as a numeric alias so authored source identities do not
    /// change while the selected Mixamo models become licensed game bases.
    /// </summary>
    public enum PlayableClassVisualSource
    {
        QuaterniusPrototype = 0,
        MixamoLicensedGameBase = 1,
        MixamoDevelopmentTrial = MixamoLicensedGameBase,
    }

    public static class PlayableClassTrialSourceSelection
    {
        // Public source defaults must not require non-redistributable Mixamo
        // Content Files. A licensed intake can opt in before an authored rebuild.
        public const PlayableClassVisualSource Priest =
            PlayableClassVisualSource.QuaterniusPrototype;

        // Compatibility name for authoring code that still calls ClassId.Priest
        // the healer class.
        public const PlayableClassVisualSource Healer = Priest;

        public const PlayableClassVisualSource Rogue =
            PlayableClassVisualSource.QuaterniusPrototype;

        public const PlayableClassVisualSource Mage =
            PlayableClassVisualSource.QuaterniusPrototype;

        public static PlayableClassVisualSource Get(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Priest:
                    return Priest;
                case ClassId.Rogue:
                    return Rogue;
                case ClassId.Mage:
                    return Mage;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(classId),
                        classId,
                        "Only Priest, Rogue, and Mage have a playable-class trial source selector.");
            }
        }

        public static bool UsesMixamoTrial(ClassId classId)
        {
            return UsesMixamoLicensedGameBase(classId);
        }

        public static bool UsesMixamoLicensedGameBase(ClassId classId)
        {
            return Get(classId) == PlayableClassVisualSource.MixamoLicensedGameBase;
        }

        public static bool UsesQuaterniusPrototype(ClassId classId)
        {
            return Get(classId) == PlayableClassVisualSource.QuaterniusPrototype;
        }
    }
}
