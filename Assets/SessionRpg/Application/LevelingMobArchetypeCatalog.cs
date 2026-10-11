using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    public enum LevelingMobSignaturePolicy
    {
        None = 0,
        CadenceHostile = 1,
        CloseCasterArea = 2,
        DefensiveSelf = 3,
        PointAtRange = 4,
    }

    /// <summary>
    /// Stable content profile for one leveling enemy family. Combat-facing values
    /// live in Application so the Unity view layer cannot silently choose a
    /// different class, specialization, attack, or engagement range.
    /// </summary>
    public readonly struct LevelingMobArchetypeDefinition
    {
        internal LevelingMobArchetypeDefinition(
            LevelingMobArchetypeId id,
            string displayName,
            string shortDisplayName,
            ClassId classId,
            SpecializationId specialization,
            AbilityId primaryAttack,
            AbilityId signatureAbility,
            AbilityId fallbackAbility,
            LevelingMobSignaturePolicy signaturePolicy,
            float preferredRange,
            float presentationScale)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
            ShortDisplayName = shortDisplayName ?? string.Empty;
            Class = classId;
            Specialization = specialization;
            PrimaryAttack = primaryAttack;
            SignatureAbility = signatureAbility;
            FallbackAbility = fallbackAbility;
            SignaturePolicy = signaturePolicy;
            PreferredRange = Math.Max(0f, preferredRange);
            PresentationScale = Math.Max(0.01f, presentationScale);
        }

        public LevelingMobArchetypeId Id { get; }

        public string DisplayName { get; }

        public string ShortDisplayName { get; }

        public ClassId Class { get; }

        public SpecializationId Specialization { get; }

        public AbilityId PrimaryAttack { get; }

        public AbilityId SignatureAbility { get; }

        /// <summary>
        /// Optional pressure action attempted only when the primary is unavailable.
        /// This keeps elite cooldown windows active without changing Core cooldowns.
        /// </summary>
        public AbilityId FallbackAbility { get; }

        public LevelingMobSignaturePolicy SignaturePolicy { get; }

        public float PreferredRange { get; }

        /// <summary>
        /// Authored presentation hint. It never participates in Core collision,
        /// targeting, damage, or any other deterministic combat outcome.
        /// </summary>
        public float PresentationScale { get; }

        public bool IsRanged => PreferredRange > 3f;
    }

    /// <summary>
    /// Allocation-free registry for the complete Ashen Causeway enemy roster.
    /// Switch-backed lookup keeps stable IDs explicit and avoids runtime content
    /// dictionaries in the warmed-up decision path.
    /// </summary>
    public static class LevelingMobArchetypeCatalog
    {
        public const int Count = 8;

        private static readonly LevelingMobArchetypeDefinition AshMelee =
            new LevelingMobArchetypeDefinition(
                LevelingMobArchetypeId.AshMelee,
                "Ashbound Raider",
                "Raider",
                ClassId.Rogue,
                SpecializationId.Mutilate,
                AbilityId.Shiv,
                AbilityId.Envenom,
                AbilityId.None,
                LevelingMobSignaturePolicy.CadenceHostile,
                1.85f,
                0.98f);

        private static readonly LevelingMobArchetypeDefinition AshRanged =
            new LevelingMobArchetypeDefinition(
                LevelingMobArchetypeId.AshRanged,
                "Cinder Marksman",
                "Marksman",
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                AbilityId.SteadyShot,
                AbilityId.AimedShot,
                AbilityId.None,
                LevelingMobSignaturePolicy.CadenceHostile,
                10f,
                0.98f);

        private static readonly LevelingMobArchetypeDefinition AshBrute =
            new LevelingMobArchetypeDefinition(
                LevelingMobArchetypeId.AshBrute,
                "Ashbound Brute",
                "Brute",
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                AbilityId.Hemorrhage,
                AbilityId.Eviscerate,
                AbilityId.None,
                LevelingMobSignaturePolicy.CadenceHostile,
                2.05f,
                1.14f);

        private static readonly LevelingMobArchetypeDefinition AshCaster =
            new LevelingMobArchetypeDefinition(
                LevelingMobArchetypeId.AshCaster,
                "Cinder Hexer",
                "Hexer",
                ClassId.Mage,
                SpecializationId.Fire,
                AbilityId.Fireball,
                AbilityId.LivingBomb,
                AbilityId.None,
                LevelingMobSignaturePolicy.CadenceHostile,
                10f,
                1.02f);

        private static readonly LevelingMobArchetypeDefinition AshElite =
            new LevelingMobArchetypeDefinition(
                LevelingMobArchetypeId.AshElite,
                "Causeway Prelate",
                "Prelate",
                ClassId.Priest,
                SpecializationId.Shadow,
                AbilityId.MindFlay,
                AbilityId.MindBlast,
                AbilityId.None,
                LevelingMobSignaturePolicy.CadenceHostile,
                9f,
                1.13f);

        private static readonly LevelingMobArchetypeDefinition RimeFrostweaver =
            new LevelingMobArchetypeDefinition(
                LevelingMobArchetypeId.RimeFrostweaver,
                "Rimebound Frostweaver",
                "Weaver",
                ClassId.Mage,
                SpecializationId.Frost,
                AbilityId.Frostbolt,
                AbilityId.FrostNova,
                AbilityId.None,
                LevelingMobSignaturePolicy.CloseCasterArea,
                9.5f,
                0.96f);

        private static readonly LevelingMobArchetypeDefinition ReliquaryWarder =
            new LevelingMobArchetypeDefinition(
                LevelingMobArchetypeId.ReliquaryWarder,
                "Reliquary Warder",
                "Warder",
                ClassId.Priest,
                SpecializationId.Discipline,
                AbilityId.Penance,
                AbilityId.PowerWordShield,
                AbilityId.None,
                LevelingMobSignaturePolicy.DefensiveSelf,
                9f,
                1.06f);

        private static readonly LevelingMobArchetypeDefinition RiftWarden =
            new LevelingMobArchetypeDefinition(
                LevelingMobArchetypeId.RiftWarden,
                "Rift Warden",
                "Warden",
                ClassId.Boss,
                SpecializationId.Boss,
                AbilityId.BossCleave,
                AbilityId.BossMeteor,
                AbilityId.BossKnockback,
                LevelingMobSignaturePolicy.PointAtRange,
                3.25f,
                1.22f);

        public static LevelingMobArchetypeDefinition Get(
            LevelingMobArchetypeId id)
        {
            if (!TryGet(id, out LevelingMobArchetypeDefinition definition))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(id),
                    id,
                    "Unknown leveling-mob archetype ID.");
            }

            return definition;
        }

        public static bool TryGet(
            LevelingMobArchetypeId id,
            out LevelingMobArchetypeDefinition definition)
        {
            switch (id)
            {
                case LevelingMobArchetypeId.AshMelee:
                    definition = AshMelee;
                    return true;
                case LevelingMobArchetypeId.AshRanged:
                    definition = AshRanged;
                    return true;
                case LevelingMobArchetypeId.AshBrute:
                    definition = AshBrute;
                    return true;
                case LevelingMobArchetypeId.AshCaster:
                    definition = AshCaster;
                    return true;
                case LevelingMobArchetypeId.AshElite:
                    definition = AshElite;
                    return true;
                case LevelingMobArchetypeId.RimeFrostweaver:
                    definition = RimeFrostweaver;
                    return true;
                case LevelingMobArchetypeId.ReliquaryWarder:
                    definition = ReliquaryWarder;
                    return true;
                case LevelingMobArchetypeId.RiftWarden:
                    definition = RiftWarden;
                    return true;
                default:
                    definition = default;
                    return false;
            }
        }

        public static LevelingMobArchetypeDefinition GetByIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return AshMelee;
                case 1:
                    return AshRanged;
                case 2:
                    return AshBrute;
                case 3:
                    return AshCaster;
                case 4:
                    return AshElite;
                case 5:
                    return RimeFrostweaver;
                case 6:
                    return ReliquaryWarder;
                case 7:
                    return RiftWarden;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        internal static bool TryGetBySpecialization(
            SpecializationId specialization,
            out LevelingMobArchetypeDefinition definition)
        {
            switch (specialization)
            {
                case SpecializationId.Mutilate:
                    definition = AshMelee;
                    return true;
                case SpecializationId.Marksmanship:
                    definition = AshRanged;
                    return true;
                case SpecializationId.ShadowDance:
                    definition = AshBrute;
                    return true;
                case SpecializationId.Fire:
                    definition = AshCaster;
                    return true;
                case SpecializationId.Shadow:
                    definition = AshElite;
                    return true;
                case SpecializationId.Frost:
                    definition = RimeFrostweaver;
                    return true;
                case SpecializationId.Discipline:
                    definition = ReliquaryWarder;
                    return true;
                case SpecializationId.Boss:
                    definition = RiftWarden;
                    return true;
                default:
                    definition = default;
                    return false;
            }
        }
    }
}
