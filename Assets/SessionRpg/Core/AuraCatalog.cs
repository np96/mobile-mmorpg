using System;

namespace SessionRpg.Core
{
    public enum AuraId
    {
        None = 0,
        SoulWard = 1,
        WeakenedSoul = 2,
        GuardianCovenant = 3,
        Fear = 4,
        Stun = 5,
        NightfallDance = 6,
        Spellveil = 7,
        Poison = 8,
        VenomRush = 9,
        Blind = 10,
        Stealth = 11,
        LivingEmber = 12,
        Disoriented = 13,
        Polymorphed = 14,
        FrostSlow = 15,
        FrozenRoot = 16,
        IceWard = 17,
        PowerInfusion = 18,
        Combustion = 19,
        IcyVeins = 20,
        Evasion = 21,
        ColdBlood = 22,
        Renew = 23,
        VampiricTouch = 24,
        ShadowWordPain = 25,
        DevouringPlague = 26,
        MindFlaySlow = 27,
        MortalWound = 28,
        Silence = 29,
        Horror = 30,
        Disarm = 31,
        Dispersion = 32,
        Replenishment = 33,
        Shadowform = 34,
        VampiricEmbrace = 35,
        SerpentSting = 36,
        ViperSting = 37,
        ScorpidSting = 38,
        ScatterDisorient = 39,
        FreezingTrap = 40,
        WingClip = 41,
        MastersCall = 42,
        Deterrence = 43,
        HuntersMark = 44,
        FrostTrapSlow = 45,
        RoarOfSacrifice = 46,
        RapidFire = 47,
        FeignDeath = 48,
        SpiderWeb = 49,
        ChimeraScorpidLockout = 50,
        ConcussiveSlow = 51,
        AspectOfTheDragonhawk = 52,
        AspectOfTheViper = 53,
        PrayerOfMending = 54,
        InnerFocus = 55,
        FearWard = 56,
        ManaShield = 57,
        IceBlock = 58,
        MoltenArmor = 59,
        MageArmor = 60,
        IceArmor = 61,
        Sap = 62,
        GarroteBleed = 63,
        Gouge = 64,
        Sprint = 65,
    }

    public enum AuraDisposition
    {
        Beneficial,
        Harmful,
    }

    [Flags]
    public enum DispelType
    {
        None = 0,
        Magic = 1 << 0,
        Poison = 1 << 1,
        Disease = 1 << 2,
        Curse = 1 << 3,
        Enrage = 1 << 4,
    }

    [Flags]
    public enum AuraTag
    {
        None = 0,
        Absorb = 1 << 0,
        DamageReduction = 1 << 1,
        Fear = 1 << 2,
        Stun = 1 << 3,
        Disorient = 1 << 4,
        Slow = 1 << 5,
        Root = 1 << 6,
        Frozen = 1 << 7,
        Poison = 1 << 8,
        Stealth = 1 << 9,
        StealthAccess = 1 << 10,
        MagicImmunity = 1 << 11,
        PreventsShield = 1 << 12,
        BreaksOnDamage = 1 << 13,
        Periodic = 1 << 14,
        CastHaste = 1 << 15,
        DamageBoost = 1 << 16,
        Silence = 1 << 17,
        Horror = 1 << 18,
        Disarm = 1 << 19,
        Pacified = 1 << 20,
        MovementImpairmentImmunity = 1 << 21,
        HealingReduction = 1 << 22,
        UniquePerCaster = 1 << 23,
        Marked = 1 << 24,
        AbilityLock = 1 << 25,
        TargetingDropped = 1 << 26,
        ExclusiveStance = 1 << 27,
        FearWard = 1 << 28,
        ReactiveHeal = 1 << 29,
    }

    public enum DiminishingReturnCategory
    {
        None,
        Fear,
        ControlledStun,
        Disorient,
        Root,
        Horror,
        Silence,
        Disarm,
    }

    public readonly struct AuraDefinition
    {
        public AuraDefinition(
            AuraId id,
            AuraDisposition disposition,
            DispelType dispelType,
            AuraTag tags,
            DiminishingReturnCategory diminishingReturn = DiminishingReturnCategory.None,
            int maximumStacks = 1,
            float damageTakenMultiplier = 1f,
            float movementMultiplier = 1f,
            float periodSeconds = 0f,
            AbilityEffectSlice periodicEffects = default,
            AbilityEffectSlice expirationEffects = default,
            float damageDoneMultiplier = 1f,
            SpellSchoolMask damageDoneSchools = SpellSchoolMask.None,
            float castTimeMultiplier = 1f,
            float physicalDamageTakenMultiplier = 1f,
            float healingTakenMultiplier = 1f,
            AbilityEffectSlice dispelEffects = default)
        {
            Id = id;
            Disposition = disposition;
            DispelType = dispelType;
            Tags = tags;
            DiminishingReturn = diminishingReturn;
            MaximumStacks = Math.Max(1, maximumStacks);
            DamageTakenMultiplier = Math.Max(0f, damageTakenMultiplier);
            MovementMultiplier = Math.Max(0f, movementMultiplier);
            PeriodSeconds = Math.Max(0f, periodSeconds);
            PeriodicEffects = periodicEffects;
            ExpirationEffects = expirationEffects;
            DamageDoneMultiplier = Math.Max(0f, damageDoneMultiplier);
            DamageDoneSchools = damageDoneSchools;
            CastTimeMultiplier = Math.Max(0.01f, castTimeMultiplier);
            PhysicalDamageTakenMultiplier = Math.Max(
                0f,
                physicalDamageTakenMultiplier);
            HealingTakenMultiplier = Math.Max(0f, healingTakenMultiplier);
            DispelEffects = dispelEffects;
        }

        public AuraId Id { get; }

        public AuraDisposition Disposition { get; }

        public DispelType DispelType { get; }

        public AuraTag Tags { get; }

        public DiminishingReturnCategory DiminishingReturn { get; }

        public int MaximumStacks { get; }

        public float DamageTakenMultiplier { get; }

        public float MovementMultiplier { get; }

        public float PeriodSeconds { get; }

        public AbilityEffectSlice PeriodicEffects { get; }

        public AbilityEffectSlice ExpirationEffects { get; }

        public float DamageDoneMultiplier { get; }

        public SpellSchoolMask DamageDoneSchools { get; }

        public float CastTimeMultiplier { get; }

        public float PhysicalDamageTakenMultiplier { get; }

        public float HealingTakenMultiplier { get; }

        public AbilityEffectSlice DispelEffects { get; }
    }

    public static class AuraCatalog
    {
        public const int StorageCapacity = 66;

        private static readonly AuraDefinition[] Definitions =
        {
            default,
            new AuraDefinition(AuraId.SoulWard, AuraDisposition.Beneficial, DispelType.Magic, AuraTag.Absorb),
            new AuraDefinition(AuraId.WeakenedSoul, AuraDisposition.Harmful, DispelType.None, AuraTag.PreventsShield),
            new AuraDefinition(AuraId.GuardianCovenant, AuraDisposition.Beneficial, DispelType.Magic, AuraTag.DamageReduction, damageTakenMultiplier: 0.6f),
            new AuraDefinition(AuraId.Fear, AuraDisposition.Harmful, DispelType.Magic, AuraTag.Fear | AuraTag.BreaksOnDamage, DiminishingReturnCategory.Fear),
            new AuraDefinition(AuraId.Stun, AuraDisposition.Harmful, DispelType.None, AuraTag.Stun, DiminishingReturnCategory.ControlledStun),
            new AuraDefinition(AuraId.NightfallDance, AuraDisposition.Beneficial, DispelType.None, AuraTag.StealthAccess),
            new AuraDefinition(AuraId.Spellveil, AuraDisposition.Beneficial, DispelType.None, AuraTag.MagicImmunity),
            new AuraDefinition(AuraId.Poison, AuraDisposition.Harmful, DispelType.Poison, AuraTag.Poison, maximumStacks: 5),
            new AuraDefinition(AuraId.VenomRush, AuraDisposition.Beneficial, DispelType.None, AuraTag.None),
            new AuraDefinition(AuraId.Blind, AuraDisposition.Harmful, DispelType.None, AuraTag.Disorient | AuraTag.BreaksOnDamage, DiminishingReturnCategory.Disorient),
            new AuraDefinition(
                AuraId.Stealth,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.Stealth | AuraTag.StealthAccess | AuraTag.BreaksOnDamage),
            new AuraDefinition(AuraId.LivingEmber, AuraDisposition.Harmful, DispelType.Magic, AuraTag.Periodic, periodSeconds: 3f, periodicEffects: AbilityEffectCatalog.LivingBombPeriodic, expirationEffects: AbilityEffectCatalog.LivingBombExpired),
            new AuraDefinition(AuraId.Disoriented, AuraDisposition.Harmful, DispelType.Magic, AuraTag.Disorient | AuraTag.BreaksOnDamage, DiminishingReturnCategory.Disorient),
            new AuraDefinition(AuraId.Polymorphed, AuraDisposition.Harmful, DispelType.Magic, AuraTag.Disorient | AuraTag.BreaksOnDamage | AuraTag.Periodic, DiminishingReturnCategory.Disorient, periodSeconds: 1f, periodicEffects: AbilityEffectCatalog.PolymorphPeriodic),
            new AuraDefinition(AuraId.FrostSlow, AuraDisposition.Harmful, DispelType.Magic, AuraTag.Slow, movementMultiplier: 0.5f),
            new AuraDefinition(AuraId.FrozenRoot, AuraDisposition.Harmful, DispelType.Magic, AuraTag.Root | AuraTag.Frozen, DiminishingReturnCategory.Root, movementMultiplier: 0f),
            new AuraDefinition(AuraId.IceWard, AuraDisposition.Beneficial, DispelType.Magic, AuraTag.Absorb),
            new AuraDefinition(
                AuraId.PowerInfusion,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.CastHaste,
                castTimeMultiplier: 0.75f),
            new AuraDefinition(
                AuraId.Combustion,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.DamageBoost,
                damageDoneMultiplier: 1.25f,
                damageDoneSchools: SpellSchoolMask.Fire),
            new AuraDefinition(
                AuraId.IcyVeins,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.CastHaste,
                castTimeMultiplier: 0.7f),
            new AuraDefinition(
                AuraId.Evasion,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.DamageReduction,
                physicalDamageTakenMultiplier: 0.4f),
            new AuraDefinition(
                AuraId.ColdBlood,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.DamageBoost,
                damageDoneMultiplier: 1.25f,
                damageDoneSchools: SpellSchoolMask.Physical | SpellSchoolMask.Nature),
            new AuraDefinition(
                AuraId.Renew,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.None,
                periodSeconds: 3f,
                periodicEffects: AbilityEffectCatalog.RenewPeriodic),
            new AuraDefinition(
                AuraId.VampiricTouch,
                AuraDisposition.Harmful,
                DispelType.Magic,
                AuraTag.Periodic,
                periodSeconds: 3f,
                periodicEffects: AbilityEffectCatalog.VampiricTouchPeriodic,
                dispelEffects: AbilityEffectCatalog.VampiricTouchDispelled),
            new AuraDefinition(
                AuraId.ShadowWordPain,
                AuraDisposition.Harmful,
                DispelType.Magic,
                AuraTag.Periodic,
                periodSeconds: 3f,
                periodicEffects: AbilityEffectCatalog.ShadowWordPainPeriodic),
            new AuraDefinition(
                AuraId.DevouringPlague,
                AuraDisposition.Harmful,
                DispelType.Disease,
                AuraTag.Periodic | AuraTag.UniquePerCaster,
                periodSeconds: 3f,
                periodicEffects: AbilityEffectCatalog.DevouringPlaguePeriodic),
            new AuraDefinition(
                AuraId.MindFlaySlow,
                AuraDisposition.Harmful,
                DispelType.Magic,
                AuraTag.Slow,
                movementMultiplier: 0.5f),
            new AuraDefinition(
                AuraId.MortalWound,
                AuraDisposition.Harmful,
                DispelType.None,
                AuraTag.HealingReduction,
                healingTakenMultiplier: 0.5f),
            new AuraDefinition(
                AuraId.Silence,
                AuraDisposition.Harmful,
                DispelType.Magic,
                AuraTag.Silence,
                DiminishingReturnCategory.Silence),
            new AuraDefinition(
                AuraId.Horror,
                AuraDisposition.Harmful,
                DispelType.Magic,
                AuraTag.Horror,
                DiminishingReturnCategory.Horror),
            new AuraDefinition(
                AuraId.Disarm,
                AuraDisposition.Harmful,
                DispelType.None,
                AuraTag.Disarm,
                DiminishingReturnCategory.Disarm),
            new AuraDefinition(
                AuraId.Dispersion,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.DamageReduction |
                AuraTag.Pacified |
                AuraTag.AbilityLock |
                AuraTag.MovementImpairmentImmunity |
                AuraTag.Periodic,
                damageTakenMultiplier: 0.1f,
                periodSeconds: 1f,
                periodicEffects: AbilityEffectCatalog.DispersionPeriodic),
            new AuraDefinition(
                AuraId.Replenishment,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.Periodic,
                periodSeconds: 5f,
                periodicEffects: AbilityEffectCatalog.ReplenishmentPeriodic),
            new AuraDefinition(
                AuraId.Shadowform,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.DamageReduction | AuraTag.DamageBoost,
                damageTakenMultiplier: 0.85f,
                damageDoneMultiplier: 1.15f,
                damageDoneSchools: SpellSchoolMask.Shadow),
            new AuraDefinition(
                AuraId.VampiricEmbrace,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.None),
            new AuraDefinition(
                AuraId.SerpentSting,
                AuraDisposition.Harmful,
                DispelType.Poison,
                AuraTag.Poison | AuraTag.Periodic,
                periodSeconds: 3f,
                periodicEffects: AbilityEffectCatalog.SerpentStingPeriodic),
            new AuraDefinition(
                AuraId.ViperSting,
                AuraDisposition.Harmful,
                DispelType.Poison,
                AuraTag.Poison | AuraTag.Periodic,
                periodSeconds: 2f,
                periodicEffects: AbilityEffectCatalog.ViperStingPeriodic),
            new AuraDefinition(
                AuraId.ScorpidSting,
                AuraDisposition.Harmful,
                DispelType.Poison,
                AuraTag.Poison),
            new AuraDefinition(
                AuraId.ScatterDisorient,
                AuraDisposition.Harmful,
                DispelType.None,
                AuraTag.Disorient | AuraTag.BreaksOnDamage,
                DiminishingReturnCategory.Disorient),
            new AuraDefinition(
                AuraId.FreezingTrap,
                AuraDisposition.Harmful,
                DispelType.Magic,
                AuraTag.Disorient | AuraTag.BreaksOnDamage,
                DiminishingReturnCategory.Disorient),
            new AuraDefinition(
                AuraId.WingClip,
                AuraDisposition.Harmful,
                DispelType.None,
                AuraTag.Slow,
                movementMultiplier: 0.5f),
            new AuraDefinition(
                AuraId.MastersCall,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.MovementImpairmentImmunity),
            new AuraDefinition(
                AuraId.Deterrence,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.Pacified),
            new AuraDefinition(
                AuraId.HuntersMark,
                AuraDisposition.Harmful,
                DispelType.Magic,
                AuraTag.Marked),
            new AuraDefinition(
                AuraId.FrostTrapSlow,
                AuraDisposition.Harmful,
                DispelType.None,
                AuraTag.Slow,
                movementMultiplier: 0.5f),
            new AuraDefinition(
                AuraId.RoarOfSacrifice,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.None),
            new AuraDefinition(
                AuraId.RapidFire,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.CastHaste,
                castTimeMultiplier: 0.6f),
            new AuraDefinition(
                AuraId.FeignDeath,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.TargetingDropped),
            new AuraDefinition(
                AuraId.SpiderWeb,
                AuraDisposition.Harmful,
                DispelType.Magic,
                AuraTag.Root,
                DiminishingReturnCategory.Root,
                movementMultiplier: 0f),
            new AuraDefinition(
                AuraId.ChimeraScorpidLockout,
                AuraDisposition.Harmful,
                DispelType.None,
                AuraTag.None),
            new AuraDefinition(
                AuraId.ConcussiveSlow,
                AuraDisposition.Harmful,
                DispelType.None,
                AuraTag.Slow,
                movementMultiplier: 0.5f),
            new AuraDefinition(
                AuraId.AspectOfTheDragonhawk,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.ExclusiveStance | AuraTag.DamageBoost,
                damageDoneMultiplier: 1.08f,
                physicalDamageTakenMultiplier: 0.96f),
            new AuraDefinition(
                AuraId.AspectOfTheViper,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.ExclusiveStance | AuraTag.Periodic,
                periodSeconds: 2f,
                periodicEffects: AbilityEffectCatalog.AspectOfTheViperPeriodic,
                damageDoneMultiplier: 0.6f),
            new AuraDefinition(
                AuraId.PrayerOfMending,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.ReactiveHeal),
            new AuraDefinition(
                AuraId.InnerFocus,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.CastHaste,
                castTimeMultiplier: 0.75f),
            new AuraDefinition(
                AuraId.FearWard,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.FearWard),
            new AuraDefinition(
                AuraId.ManaShield,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.Absorb),
            new AuraDefinition(
                AuraId.IceBlock,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.DamageReduction | AuraTag.MagicImmunity |
                AuraTag.Pacified | AuraTag.AbilityLock,
                damageTakenMultiplier: 0f),
            new AuraDefinition(
                AuraId.MoltenArmor,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.ExclusiveStance | AuraTag.DamageBoost,
                damageDoneMultiplier: 1.08f,
                damageDoneSchools: SpellSchoolMask.Fire),
            new AuraDefinition(
                AuraId.MageArmor,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.ExclusiveStance | AuraTag.DamageReduction,
                damageTakenMultiplier: 0.9f),
            new AuraDefinition(
                AuraId.IceArmor,
                AuraDisposition.Beneficial,
                DispelType.Magic,
                AuraTag.ExclusiveStance | AuraTag.DamageReduction,
                physicalDamageTakenMultiplier: 0.82f),
            new AuraDefinition(
                AuraId.Sap,
                AuraDisposition.Harmful,
                DispelType.None,
                AuraTag.Disorient | AuraTag.BreaksOnDamage |
                AuraTag.UniquePerCaster,
                DiminishingReturnCategory.Disorient),
            new AuraDefinition(
                AuraId.GarroteBleed,
                AuraDisposition.Harmful,
                DispelType.None,
                AuraTag.Periodic,
                periodSeconds: 3f,
                periodicEffects: AbilityEffectCatalog.GarrotePeriodic),
            new AuraDefinition(
                AuraId.Gouge,
                AuraDisposition.Harmful,
                DispelType.None,
                AuraTag.Disorient | AuraTag.BreaksOnDamage,
                DiminishingReturnCategory.Disorient),
            new AuraDefinition(
                AuraId.Sprint,
                AuraDisposition.Beneficial,
                DispelType.None,
                AuraTag.None,
                movementMultiplier: 1.7f),
        };

        public static AuraDefinition Get(AuraId id)
        {
            if (!TryGet(id, out AuraDefinition definition))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown aura ID.");
            }

            return definition;
        }

        public static bool TryGet(AuraId id, out AuraDefinition definition)
        {
            int index = (int)id;
            if (index > 0 && index < Definitions.Length && Definitions[index].Id == id)
            {
                definition = Definitions[index];
                return true;
            }

            definition = default;
            return false;
        }
    }
}
