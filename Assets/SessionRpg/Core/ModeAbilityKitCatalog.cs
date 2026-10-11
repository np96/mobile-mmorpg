using System;

namespace SessionRpg.Core
{
    /// <summary>
    /// Applies small, explicit mode loadout extensions without mutating the
    /// normalized Arena specialization kits. Hero Siege replaces the PvP trinket
    /// with a visible, class-readable crowd-clear action. Arena, Dungeon, and
    /// compatibility callers retain their existing power and bindings.
    /// </summary>
    public static class ModeAbilityKitCatalog
    {
        public const int SiegeAreaSlot = 9;
        public const AbilitySlotDirection SiegeAreaDirection =
            AbilitySlotDirection.None;

        public static AbilityKit GetKit(
            PlayableModeId mode,
            SpecializationId specialization)
        {
            AbilityKit baseKit = SpecializationCatalog.GetKit(specialization);
            if (mode != PlayableModeId.HeroSiege)
            {
                return baseKit;
            }

            AbilityId siegeAreaAbility = GetSiegeAreaAbility(specialization);
            if (siegeAreaAbility == AbilityId.None)
            {
                return baseKit;
            }

            AbilityId existing = baseKit.GetAbility(SiegeAreaSlot);
            if (existing != AbilityId.ArenaTrinket &&
                existing != siegeAreaAbility)
            {
                throw new InvalidOperationException(
                    "The reserved Hero Siege AoE binding is occupied.");
            }

            return existing == siegeAreaAbility
                ? baseKit
                : baseKit.WithPrimaryAbility(SiegeAreaSlot, siegeAreaAbility);
        }

        public static bool IsModeOnlyAbility(AbilityId ability) =>
            ability == AbilityId.BladeTempest ||
            ability == AbilityId.DawnNova ||
            ability == AbilityId.UmbralCollapse ||
            ability == AbilityId.FlameSurge ||
            ability == AbilityId.Blizzard ||
            ability == AbilityId.ArrowRain;

        public static AbilityId GetSiegeAreaAbility(
            SpecializationId specialization)
        {
            switch (specialization)
            {
                case SpecializationId.Discipline:
                    return AbilityId.DawnNova;
                case SpecializationId.Shadow:
                    return AbilityId.UmbralCollapse;
                case SpecializationId.Fire:
                    return AbilityId.FlameSurge;
                case SpecializationId.Frost:
                    return AbilityId.Blizzard;
                case SpecializationId.ShadowDance:
                case SpecializationId.Mutilate:
                    return AbilityId.BladeTempest;
                case SpecializationId.Marksmanship:
                    return AbilityId.ArrowRain;
                default:
                    return AbilityId.None;
            }
        }
    }
}
