using System;

namespace SessionRpg.Core
{
    /// <summary>
    /// Allocation-free ability progression for guided Training. Three essential
    /// actions are available at level one, then one new primary action is granted
    /// after each of the first four authored packs (levels two through five).
    /// The final authored pack exposes one duplicate directional binding so the
    /// player can learn the grouped-button flick without adding an eighth action.
    /// The remaining full Arena kit stays outside this learning profile; Training
    /// never changes Arena power.
    /// </summary>
    public static class LevelingAbilityUnlockCatalog
    {
        public const int InitialAbilityCount = 3;
        public const int TotalAbilityCount = 7;
        public const int LevelTwoUnlockSlot = 3;
        public const int LevelThreeUnlockSlot = 4;
        public const int LevelFourUnlockSlot = 5;
        public const int LevelFiveUnlockSlot = 6;

        private static readonly AbilityKit DisciplineKit = new AbilityKit(
            ClassId.Priest,
            SpecializationId.Discipline,
            AbilityId.Penance,
            AbilityId.Smite,
            AbilityId.ShadowWordPain,
            AbilityId.PowerWordShield,
            AbilityId.FlashHeal,
            AbilityId.PsychicScream,
            AbilityId.PowerInfusion);

        private static readonly AbilityKit ShadowKit = new AbilityKit(
            ClassId.Priest,
            SpecializationId.Shadow,
            AbilityId.ShadowWordPain,
            AbilityId.MindFlay,
            AbilityId.FlashHeal,
            AbilityId.Shadowform,
            AbilityId.PsychicScream,
            AbilityId.MindBlast,
            AbilityId.Dispersion);

        private static readonly AbilityKit FireKit = new AbilityKit(
            ClassId.Mage,
            SpecializationId.Fire,
            AbilityId.Fireball,
            AbilityId.FireBlast,
            AbilityId.Polymorph,
            AbilityId.FrostNova,
            AbilityId.Blink,
            AbilityId.DragonBreath,
            AbilityId.Combustion);

        private static readonly AbilityKit FrostKit = new AbilityKit(
            ClassId.Mage,
            SpecializationId.Frost,
            AbilityId.Frostbolt,
            AbilityId.IceLance,
            AbilityId.FrostNova,
            AbilityId.IceBarrier,
            AbilityId.Blink,
            AbilityId.DeepFreeze,
            AbilityId.IcyVeins);

        private static readonly AbilityKit ShadowDanceKit = new AbilityKit(
            ClassId.Rogue,
            SpecializationId.ShadowDance,
            AbilityId.Hemorrhage,
            AbilityId.Eviscerate,
            AbilityId.Stealth,
            AbilityId.Sap,
            AbilityId.Shadowstep,
            AbilityId.KidneyShot,
            AbilityId.ShadowDance);

        private static readonly AbilityKit MutilateKit = new AbilityKit(
            ClassId.Rogue,
            SpecializationId.Mutilate,
            AbilityId.Mutilate,
            AbilityId.Envenom,
            AbilityId.Stealth,
            AbilityId.Sap,
            AbilityId.Vanish,
            AbilityId.KidneyShot,
            AbilityId.ColdBlood);

        private static readonly AbilityKit MarksmanshipKit = new AbilityKit(
            ClassId.Hunter,
            SpecializationId.Marksmanship,
            AbilityId.SteadyShot,
            AbilityId.ArcaneShot,
            AbilityId.FreezingArrow,
            AbilityId.PetAttack,
            AbilityId.Disengage,
            AbilityId.SerpentSting,
            AbilityId.RapidFire);

        public static AbilityKit GetKit(SpecializationId specialization)
        {
            if (!TryGetKit(specialization, out AbilityKit kit))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(specialization),
                    specialization,
                    "Unknown leveling specialization ID.");
            }

            return kit;
        }

        public static AbilityKit GetFlickLessonKit(
            SpecializationId specialization)
        {
            AbilityKit kit = GetKit(specialization);
            return kit.WithDirectionalAbility(
                0,
                AbilitySlotDirection.Up,
                kit.GetAbility(LevelFiveUnlockSlot));
        }

        public static bool TryGetKit(
            SpecializationId specialization,
            out AbilityKit kit)
        {
            switch (specialization)
            {
                case SpecializationId.Discipline:
                    kit = DisciplineKit;
                    return true;
                case SpecializationId.Shadow:
                    kit = ShadowKit;
                    return true;
                case SpecializationId.Fire:
                    kit = FireKit;
                    return true;
                case SpecializationId.Frost:
                    kit = FrostKit;
                    return true;
                case SpecializationId.ShadowDance:
                    kit = ShadowDanceKit;
                    return true;
                case SpecializationId.Mutilate:
                    kit = MutilateKit;
                    return true;
                case SpecializationId.Marksmanship:
                    kit = MarksmanshipKit;
                    return true;
                default:
                    kit = default;
                    return false;
            }
        }

        public static AbilityId GetAbility(
            SpecializationId specialization,
            int progressionSlot)
        {
            if (progressionSlot < 0 || progressionSlot >= TotalAbilityCount)
            {
                throw new ArgumentOutOfRangeException(nameof(progressionSlot));
            }

            return GetKit(specialization).GetAbility(progressionSlot);
        }

        public static int GetRequiredLevel(int progressionSlot)
        {
            if (progressionSlot < 0 || progressionSlot >= TotalAbilityCount)
            {
                throw new ArgumentOutOfRangeException(nameof(progressionSlot));
            }

            return progressionSlot < InitialAbilityCount
                ? LevelProgressionCurve.MinimumLevel
                : progressionSlot - 1;
        }

        public static bool TryGetUnlockSlotForLevel(int level, out int slot)
        {
            if (level < 2 || level > 5)
            {
                slot = -1;
                return false;
            }

            slot = level + 1;
            return true;
        }

        public static bool TryGetRequiredLevel(
            SpecializationId specialization,
            AbilityId ability,
            out int requiredLevel)
        {
            if (ability == AbilityId.None ||
                !TryGetKit(
                    specialization,
                    out AbilityKit kit))
            {
                requiredLevel = 0;
                return false;
            }

            for (int slot = 0; slot < TotalAbilityCount; slot++)
            {
                if (kit.GetAbility(slot) != ability)
                {
                    continue;
                }

                requiredLevel = GetRequiredLevel(slot);
                return true;
            }

            requiredLevel = 0;
            return false;
        }

        public static bool IsUnlocked(
            SpecializationId specialization,
            AbilityId ability,
            int level)
        {
            return TryGetRequiredLevel(
                       specialization,
                       ability,
                       out int requiredLevel) &&
                   LevelProgressionCurve.ClampLevel(level) >= requiredLevel;
        }
    }
}
