using System;

namespace SessionRpg.Core
{
    public readonly struct AbilityDefinition
    {
        public AbilityDefinition(
            AbilityId id,
            ClassId ownerClass,
            string displayName,
            string shortPurpose,
            string description,
            AbilityTargetMode targetMode,
            float range,
            float cooldownSeconds,
            float castSeconds,
            float damage,
            float healing,
            float barrier,
            float knockbackDistance,
            StatusId status,
            float statusSeconds,
            float areaRadius = 0f)
            : this(
                id,
                ownerClass,
                SpecializationId.None,
                displayName,
                shortPurpose,
                description,
                targetMode,
                range,
                cooldownSeconds,
                castSeconds,
                damage,
                healing,
                barrier,
                knockbackDistance,
                status,
                statusSeconds,
                areaRadius,
                CreateLegacyTargeting(targetMode, range, areaRadius),
                castSeconds > 0f
                    ? AbilityCastProfile.Cast(castSeconds)
                    : AbilityCastProfile.Instant,
                SpellSchoolMask.None,
                ResourceCost.None,
                0f,
                AbilityRequirementFlags.None,
                AbilityEffectSlice.Empty)
        {
        }

        public AbilityDefinition(
            AbilityId id,
            ClassId ownerClass,
            SpecializationId ownerSpecialization,
            string displayName,
            string shortPurpose,
            string description,
            AbilityTargetRule targeting,
            float cooldownSeconds,
            AbilityCastProfile castProfile,
            SpellSchoolMask school,
            ResourceCost resourceCost,
            float globalCooldownSeconds,
            AbilityRequirementFlags requirements,
            AbilityEffectSlice effects)
            : this(
                id,
                ownerClass,
                ownerSpecialization,
                displayName,
                shortPurpose,
                description,
                ToLegacyTargetMode(targeting),
                targeting.Range,
                cooldownSeconds,
                castProfile.DurationSeconds,
                0f,
                0f,
                0f,
                0f,
                StatusId.None,
                0f,
                targeting.Radius,
                targeting,
                castProfile,
                school,
                resourceCost,
                globalCooldownSeconds,
                requirements,
                effects)
        {
        }

        private AbilityDefinition(
            AbilityId id,
            ClassId ownerClass,
            SpecializationId ownerSpecialization,
            string displayName,
            string shortPurpose,
            string description,
            AbilityTargetMode targetMode,
            float range,
            float cooldownSeconds,
            float castSeconds,
            float damage,
            float healing,
            float barrier,
            float knockbackDistance,
            StatusId status,
            float statusSeconds,
            float areaRadius,
            AbilityTargetRule targeting,
            AbilityCastProfile castProfile,
            SpellSchoolMask school,
            ResourceCost resourceCost,
            float globalCooldownSeconds,
            AbilityRequirementFlags requirements,
            AbilityEffectSlice effects)
        {
            Id = id;
            OwnerClass = ownerClass;
            OwnerSpecialization = ownerSpecialization;
            DisplayName = displayName ?? string.Empty;
            ShortPurpose = shortPurpose ?? string.Empty;
            Description = description ?? string.Empty;
            TargetMode = targetMode;
            Range = Math.Max(0f, range);
            CooldownSeconds = Math.Max(0f, cooldownSeconds);
            CastSeconds = Math.Max(0f, castSeconds);
            Damage = Math.Max(0f, damage);
            Healing = Math.Max(0f, healing);
            Barrier = Math.Max(0f, barrier);
            KnockbackDistance = knockbackDistance;
            Status = status;
            StatusSeconds = Math.Max(0f, statusSeconds);
            AreaRadius = Math.Max(0f, areaRadius);
            Targeting = targeting;
            CastProfile = castProfile;
            School = school;
            ResourceCost = resourceCost;
            GlobalCooldownSeconds = Math.Max(0f, globalCooldownSeconds);
            Requirements = requirements;
            Effects = effects;
        }

        public AbilityId Id { get; }

        public ClassId OwnerClass { get; }

        public SpecializationId OwnerSpecialization { get; }

        public string DisplayName { get; }

        public string ShortPurpose { get; }

        public string Description { get; }

        public AbilityTargetMode TargetMode { get; }

        public float Range { get; }

        public float CooldownSeconds { get; }

        public float CastSeconds { get; }

        public float Damage { get; }

        public float Healing { get; }

        public float Barrier { get; }

        public float KnockbackDistance { get; }

        public StatusId Status { get; }

        public float StatusSeconds { get; }

        public float AreaRadius { get; }

        public AbilityTargetRule Targeting { get; }

        public AbilityCastProfile CastProfile { get; }

        public SpellSchoolMask School { get; }

        public ResourceCost ResourceCost { get; }

        public float GlobalCooldownSeconds { get; }

        public AbilityRequirementFlags Requirements { get; }

        public AbilityEffectSlice Effects { get; }

        public bool IsHostile => Targeting.Allows(AbilityTargetRelation.Hostile);

        public AbilityDefinition WithMechanics(
            SpecializationId ownerSpecialization,
            AbilityTargetRule targeting,
            AbilityCastProfile castProfile,
            SpellSchoolMask school,
            ResourceCost resourceCost,
            float globalCooldownSeconds,
            AbilityRequirementFlags requirements,
            AbilityEffectSlice effects)
        {
            return new AbilityDefinition(
                Id,
                OwnerClass,
                ownerSpecialization,
                DisplayName,
                ShortPurpose,
                Description,
                TargetMode,
                Range,
                CooldownSeconds,
                CastSeconds,
                Damage,
                Healing,
                Barrier,
                KnockbackDistance,
                Status,
                StatusSeconds,
                AreaRadius,
                targeting,
                castProfile,
                school,
                resourceCost,
                globalCooldownSeconds,
                requirements,
                effects);
        }

        private static AbilityTargetRule CreateLegacyTargeting(
            AbilityTargetMode targetMode,
            float range,
            float areaRadius)
        {
            switch (targetMode)
            {
                case AbilityTargetMode.Self:
                    return new AbilityTargetRule(
                        AbilityTargetShape.Unit,
                        AbilityTargetRelation.Self,
                        AbilitySmartTargetPolicy.Self,
                        range,
                        areaRadius,
                        false);
                case AbilityTargetMode.Ally:
                    return new AbilityTargetRule(
                        AbilityTargetShape.Unit,
                        AbilityTargetRelation.Self | AbilityTargetRelation.Friendly,
                        AbilitySmartTargetPolicy.LowestHealthFriendly,
                        range,
                        areaRadius);
                case AbilityTargetMode.Enemy:
                    return new AbilityTargetRule(
                        AbilityTargetShape.Unit,
                        AbilityTargetRelation.Hostile,
                        AbilitySmartTargetPolicy.SelectedHostile,
                        range,
                        areaRadius);
                case AbilityTargetMode.Point:
                    return new AbilityTargetRule(
                        AbilityTargetShape.Point,
                        AbilityTargetRelation.None,
                        AbilitySmartTargetPolicy.AimPoint,
                        range,
                        areaRadius,
                        false);
                case AbilityTargetMode.Direction:
                    return new AbilityTargetRule(
                        AbilityTargetShape.Direction,
                        AbilityTargetRelation.None,
                        AbilitySmartTargetPolicy.AimPoint,
                        range,
                        areaRadius,
                        false);
                case AbilityTargetMode.EnemyArea:
                    return new AbilityTargetRule(
                        AbilityTargetShape.CasterArea,
                        AbilityTargetRelation.Hostile,
                        AbilitySmartTargetPolicy.None,
                        range,
                        areaRadius,
                        false);
                default:
                    return default;
            }
        }

        private static AbilityTargetMode ToLegacyTargetMode(AbilityTargetRule targeting)
        {
            switch (targeting.Shape)
            {
                case AbilityTargetShape.Point:
                case AbilityTargetShape.TargetArea:
                    return AbilityTargetMode.Point;
                case AbilityTargetShape.Direction:
                    return AbilityTargetMode.Direction;
                case AbilityTargetShape.CasterArea:
                    return AbilityTargetMode.EnemyArea;
                default:
                    if (targeting.AllowedRelations == AbilityTargetRelation.Self)
                    {
                        return AbilityTargetMode.Self;
                    }

                    return targeting.Allows(AbilityTargetRelation.Friendly) ||
                           targeting.Allows(AbilityTargetRelation.Self)
                        ? AbilityTargetMode.Ally
                        : AbilityTargetMode.Enemy;
            }
        }
    }

    public static class AbilityCatalog
    {
        public const int AbilitySlotCount = AbilityKit.Capacity;
        public const int StorageCapacity = 126;

        private const float StandardGlobalCooldown = 1.5f;
        private const float RogueGlobalCooldown = 1f;

        private static readonly AbilityDefinition[] Definitions =
        {
            default,
            new AbilityDefinition(
                AbilityId.HealerBolt, ClassId.Priest, "Radiant Bolt", "Basic ranged damage",
                "A quick ranged attack for contributing pressure between support casts.",
                AbilityTargetMode.Enemy, 15f, 0.9f, 0f, 9f, 0f, 0f, 0f, StatusId.None, 0f),
            new AbilityDefinition(
                AbilityId.Mend, ClassId.Priest, "Mend", "Sustained healing",
                "Restore a large amount of health to yourself or an ally after a short cast.",
                AbilityTargetMode.Ally, 14f, 3.5f, 0.55f, 0f, 28f, 0f, 0f, StatusId.None, 0f),
            new AbilityDefinition(
                AbilityId.Aegis, ClassId.Priest, "Aegis", "Prevent burst",
                "Protect yourself or an ally with a temporary damage barrier.",
                AbilityTargetMode.Ally, 13f, 8f, 0f, 0f, 0f, 24f, 0f, StatusId.Barrier, 4f),
            new AbilityDefinition(
                AbilityId.Purify, ClassId.Priest, "Cleanse", "Remove harmful control",
                "Remove Silence and Slow from yourself or an ally and restore a little health.",
                AbilityTargetMode.Ally, 14f, 9f, 0f, 0f, 8f, 0f, 0f, StatusId.None, 0f),
            new AbilityDefinition(
                AbilityId.Rescue, ClassId.Priest, "Rescue", "Pull an ally to safety",
                "Pull another living ally toward you and restore a little health.",
                AbilityTargetMode.Ally, 16f, 12f, 0.25f, 0f, 10f, 0f, -5f, StatusId.None, 0f),

            new AbilityDefinition(
                AbilityId.QuickStrike, ClassId.Rogue, "Quick Strike", "Basic melee damage",
                "Deliver a fast close-range strike to maintain single-target pressure.",
                AbilityTargetMode.Enemy, 2.5f, 0.75f, 0f, 14f, 0f, 0f, 0f, StatusId.None, 0f),
            new AbilityDefinition(
                AbilityId.Ambush, ClassId.Rogue, "Ambush", "Gap-close burst",
                "Leap into melee range and deal a heavy opening strike.",
                AbilityTargetMode.Enemy, 8f, 6f, 0f, 24f, 0f, 0f, 0f, StatusId.None, 0f)
                .WithMechanics(
                    SpecializationId.ShadowDance,
                    HostileUnit(8f, true),
                    AbilityCastProfile.Instant,
                    SpellSchoolMask.Physical,
                    new ResourceCost(ResourceType.Energy, 60f),
                    RogueGlobalCooldown,
                    AbilityRequirementFlags.RequiresStealthAccess |
                    AbilityRequirementFlags.RequiresBehindTarget |
                    AbilityRequirementFlags.BreaksStealth,
                    AbilityEffectCatalog.Ambush),
            new AbilityDefinition(
                AbilityId.Kick, ClassId.Rogue, "Kick", "Interrupt and silence",
                "Interrupt an enemy cast and briefly prevent further ability use.",
                AbilityTargetMode.Enemy, 2.7f, 8f, 0f, 6f, 0f, 0f, 0f, StatusId.Silenced, 1.4f)
                .WithMechanics(
                    SpecializationId.None,
                    HostileUnit(2.7f, true),
                    AbilityCastProfile.Instant,
                    SpellSchoolMask.Physical,
                    new ResourceCost(ResourceType.Energy, 25f),
                    0f,
                    AbilityRequirementFlags.RequiresActiveEnemyCast |
                    AbilityRequirementFlags.BreaksStealth |
                    AbilityRequirementFlags.IgnoresGlobalCooldown,
                    AbilityEffectCatalog.Kick),
            new AbilityDefinition(
                AbilityId.Shadowstep, ClassId.Rogue, "Shadowstep", "Ground mobility",
                "Teleport to a chosen point to engage, escape, or recover from danger.",
                AbilityTargetMode.Point, 7f, 8f, 0f, 0f, 0f, 0f, 0f, StatusId.None, 0f)
                .WithMechanics(
                    SpecializationId.ShadowDance,
                    HostileUnit(8f),
                    AbilityCastProfile.Instant,
                    SpellSchoolMask.Physical,
                    ResourceCost.None,
                    0f,
                    AbilityRequirementFlags.IgnoresGlobalCooldown,
                    AbilityEffectCatalog.Shadowstep),
            new AbilityDefinition(
                AbilityId.Anchor, ClassId.Rogue, "Brace", "Resist knockback",
                "Gain a small barrier and temporary immunity to knockback.",
                AbilityTargetMode.Self, 0f, 10f, 0f, 0f, 0f, 12f, 0f, StatusId.Anchored, 2.5f),

            new AbilityDefinition(
                AbilityId.ArcaneBolt, ClassId.Mage, "Arcane Bolt", "Basic ranged cast",
                "Cast a reliable long-range bolt of arcane damage.",
                AbilityTargetMode.Enemy, 17f, 1f, 0.25f, 15f, 0f, 0f, 0f, StatusId.None, 0f),
            new AbilityDefinition(
                AbilityId.Gust, ClassId.Mage, "Gust", "Displace one enemy",
                "Damage and launch one enemy away from you toward positional danger.",
                AbilityTargetMode.Enemy, 13f, 6f, 0.35f, 8f, 0f, 0f, 5.5f, StatusId.None, 0f),
            new AbilityDefinition(
                AbilityId.Counterspell, ClassId.Mage, "Counterspell", "Interrupt at range",
                "Interrupt an enemy cast and briefly prevent further ability use.",
                AbilityTargetMode.Enemy, 15f, 10f, 0f, 3f, 0f, 0f, 0f, StatusId.Silenced, 1.2f)
                .WithMechanics(
                    SpecializationId.None,
                    HostileUnit(15f, true),
                    AbilityCastProfile.Instant,
                    SpellSchoolMask.Arcane,
                    ResourceCost.None,
                    0f,
                    AbilityRequirementFlags.RequiresActiveEnemyCast |
                    AbilityRequirementFlags.IgnoresGlobalCooldown,
                    AbilityEffectCatalog.Counterspell),
            new AbilityDefinition(
                AbilityId.Blink, ClassId.Mage, "Blink", "Ground mobility",
                "Teleport to a chosen point to create space or recover from danger.",
                AbilityTargetMode.Point, 7f, 8f, 0f, 0f, 0f, 0f, 0f, StatusId.None, 0f)
                .WithMechanics(
                    SpecializationId.None,
                    Point(8f),
                    AbilityCastProfile.Instant,
                    SpellSchoolMask.Arcane,
                    ResourceCost.None,
                    StandardGlobalCooldown,
                    AbilityRequirementFlags.None,
                    AbilityEffectCatalog.Blink),
            new AbilityDefinition(
                AbilityId.FrostNova, ClassId.Mage, "Frost Nova", "Close-range peel",
                "Damage and freeze nearby enemies, setting them up for Deep Freeze and Ice Lance.",
                AbilityTargetMode.EnemyArea, 0f, 9f, 0f, 10f, 0f, 0f, 0f, StatusId.Slowed, 2f, 4f)
                .WithMechanics(
                    SpecializationId.None,
                    CasterArea(4f),
                    AbilityCastProfile.Instant,
                    SpellSchoolMask.Frost,
                    new ResourceCost(ResourceType.Mana, 8f),
                    StandardGlobalCooldown,
                    AbilityRequirementFlags.None,
                    AbilityEffectCatalog.FrostNova),

            new AbilityDefinition(
                AbilityId.BossCleave, ClassId.Boss, "Molten Cleave", "Close-range pressure",
                "After a warning, damage every player close to the Rift Warden.",
                AbilityTargetMode.EnemyArea, 0f, 3.5f, 0.7f, 20f, 0f, 0f, 0f, StatusId.None, 0f, 4.5f),
            new AbilityDefinition(
                AbilityId.BossMeteor, ClassId.Boss, "Falling Star", "Targeted danger area",
                "Mark a location, then damage players who remain inside the warning area.",
                AbilityTargetMode.Point, 18f, 7f, 1.1f, 26f, 0f, 0f, 0f, StatusId.None, 0f, 3f),
            new AbilityDefinition(
                AbilityId.BossKnockback, ClassId.Boss, "Rift Roar", "Raid-wide knockback",
                "After a long warning, damage and launch every nearby player toward danger.",
                AbilityTargetMode.EnemyArea, 0f, 9f, 1.25f, 10f, 0f, 0f, 8f, StatusId.None, 0f, 8f),

            Modern(
                AbilityId.Penance, ClassId.Priest, SpecializationId.Discipline,
                "Penance", "Channel heal or damage",
                "Channel three pulses: heal an ally or damage an enemy.",
                DualUnit(15f), 8f, AbilityCastProfile.Channel(2f, 2f / 3f, 3, true),
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 12f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.Penance),
            Modern(
                AbilityId.FlashHeal, ClassId.Priest, SpecializationId.None,
                "Flash Heal", "Fast direct heal",
                "Quickly restore a large amount of health to yourself or an ally.",
                FriendlyUnit(15f), 0f, AbilityCastProfile.Cast(1.25f),
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 10f),
                StandardGlobalCooldown, AbilityRequirementFlags.RequiresNoShadowform,
                AbilityEffectCatalog.FlashHeal),
            Modern(
                AbilityId.PowerWordShield, ClassId.Priest, SpecializationId.None,
                "Power Word: Shield", "Absorb incoming damage",
                "Place a damage-absorbing ward; Weakened Soul prevents immediate reapplication.",
                FriendlyUnit(15f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.TargetMustNotHaveWeakenedSoul,
                AbilityEffectCatalog.PowerWordShield),
            Modern(
                AbilityId.PainSuppression, ClassId.Priest, SpecializationId.Discipline,
                "Pain Suppression", "Reduce damage by 40%",
                "Reduce all damage taken by the chosen ally by 40% for eight seconds.",
                FriendlyUnit(15f), 60f, AbilityCastProfile.Instant,
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 5f),
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.UsableWhileStunned, AbilityEffectCatalog.PainSuppression),
            Modern(
                AbilityId.PsychicScream, ClassId.Priest, SpecializationId.None,
                "Psychic Scream", "Fear nearby enemies",
                "Fear nearby enemies; repeated fears are shortened by diminishing returns.",
                CasterArea(6f), 30f, AbilityCastProfile.Instant,
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 15f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.PsychicScream),
            Modern(
                AbilityId.ShadowWordDeath, ClassId.Priest, SpecializationId.None,
                "Shadow Word: Death", "Burst with backlash",
                "Damage an enemy instantly; if it survives, the caster suffers backlash.",
                HostileUnit(15f), 12f, AbilityCastProfile.Instant,
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 12f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.ShadowWordDeath),
            Modern(
                AbilityId.Shadowfiend, ClassId.Priest, SpecializationId.None,
                "Shadowfiend", "Summon mana attacker",
                "Summon a familiar that attacks the target and returns mana with each pulse.",
                HostileUnit(15f), 300f, AbilityCastProfile.Instant,
                SpellSchoolMask.Shadow, ResourceCost.None,
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.Shadowfiend),
            Modern(
                AbilityId.DispelMagic, ClassId.Priest, SpecializationId.None,
                "Dispel Magic", "Remove magic effects",
                "Remove harmful magic from an ally or beneficial magic from an enemy.",
                DualUnit(15f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 14f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.DispelMagic),

            Modern(
                AbilityId.Hemorrhage, ClassId.Rogue, SpecializationId.ShadowDance,
                "Hemorrhage", "Builder strike",
                "Deal melee damage and add one combo point to the target.",
                HostileUnit(2.7f, true), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 35f),
                RogueGlobalCooldown, AbilityRequirementFlags.BreaksStealth, AbilityEffectCatalog.Hemorrhage),
            Modern(
                AbilityId.Eviscerate, ClassId.Rogue, SpecializationId.None,
                "Eviscerate", "Combo finisher",
                "Consume combo points to deal increasing physical damage.",
                HostileUnit(2.7f, true), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 35f),
                RogueGlobalCooldown, AbilityRequirementFlags.RequiresComboPoints |
                AbilityRequirementFlags.BreaksStealth, AbilityEffectCatalog.Eviscerate),
            Modern(
                AbilityId.KidneyShot, ClassId.Rogue, SpecializationId.None,
                "Kidney Shot", "Combo stun finisher",
                "Consume combo points to stun the target for longer.",
                HostileUnit(2.7f, true), 20f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 25f),
                RogueGlobalCooldown, AbilityRequirementFlags.RequiresComboPoints |
                AbilityRequirementFlags.BreaksStealth, AbilityEffectCatalog.KidneyShot),
            Modern(
                AbilityId.ShadowDance, ClassId.Rogue, SpecializationId.ShadowDance,
                "Shadow Dance", "Open a stealth window",
                "Briefly allow stealth-only attacks while remaining in combat.",
                Self(), 45f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown, AbilityEffectCatalog.ShadowDance),
            Modern(
                AbilityId.CloakOfShadows, ClassId.Rogue, SpecializationId.None,
                "Cloak of Shadows", "Cleanse and resist magic",
                "Remove harmful magic and briefly become immune to new magical effects.",
                Self(), 45f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown, AbilityEffectCatalog.CloakOfShadows),
            Modern(
                AbilityId.Mutilate, ClassId.Rogue, SpecializationId.Mutilate,
                "Mutilate", "Poisoned-target builder",
                "Strike twice, deal bonus damage to a poisoned target, and add two combo points.",
                HostileUnit(2.7f, true), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 60f),
                RogueGlobalCooldown, AbilityRequirementFlags.BreaksStealth, AbilityEffectCatalog.Mutilate),
            Modern(
                AbilityId.Envenom, ClassId.Rogue, SpecializationId.Mutilate,
                "Envenom", "Poison combo finisher",
                "Consume combo points for Nature damage and briefly empower poison attacks.",
                HostileUnit(2.7f, true), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Nature, new ResourceCost(ResourceType.Energy, 35f),
                RogueGlobalCooldown, AbilityRequirementFlags.RequiresComboPoints |
                AbilityRequirementFlags.RequiresPoisonedTarget |
                AbilityRequirementFlags.BreaksStealth, AbilityEffectCatalog.Envenom),
            Modern(
                AbilityId.Shiv, ClassId.Rogue, SpecializationId.Mutilate,
                "Shiv", "Apply poison",
                "Deliver a cheap strike, apply stacking poison, and add one combo point.",
                HostileUnit(2.7f, true), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 20f),
                RogueGlobalCooldown, AbilityRequirementFlags.BreaksStealth, AbilityEffectCatalog.Shiv),
            Modern(
                AbilityId.Blind, ClassId.Rogue, SpecializationId.None,
                "Blind", "Long disorient",
                "Disorient one enemy until it takes damage; subject to diminishing returns.",
                HostileUnit(10f), 30f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 15f),
                RogueGlobalCooldown, AbilityRequirementFlags.BreaksStealth, AbilityEffectCatalog.Blind),
            Modern(
                AbilityId.Vanish, ClassId.Rogue, SpecializationId.None,
                "Vanish", "Enter stealth",
                "Leave sight and gain a brief stealth-action window.",
                Self(), 60f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown, AbilityEffectCatalog.Vanish),

            Modern(
                AbilityId.Fireball, ClassId.Mage, SpecializationId.Fire,
                "Fireball", "Cast fire damage",
                "Launch a heavy fire projectile after a visible cast.",
                HostileUnit(18f), 0f, AbilityCastProfile.Cast(2f),
                SpellSchoolMask.Fire, new ResourceCost(ResourceType.Mana, 10f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.Fireball),
            Modern(
                AbilityId.FireBlast, ClassId.Mage, SpecializationId.Fire,
                "Fire Blast", "Instant fire damage",
                "Deal immediate fire damage while moving.",
                HostileUnit(15f), 6f, AbilityCastProfile.Instant,
                SpellSchoolMask.Fire, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.FireBlast),
            Modern(
                AbilityId.LivingBomb, ClassId.Mage, SpecializationId.Fire,
                "Living Bomb", "Periodic fire bomb",
                "Burn the target every three seconds, then explode around it when the effect ends.",
                HostileUnit(18f), 12f, AbilityCastProfile.Instant,
                SpellSchoolMask.Fire, new ResourceCost(ResourceType.Mana, 12f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.LivingBomb),
            Modern(
                AbilityId.DragonBreath, ClassId.Mage, SpecializationId.Fire,
                "Dragon's Breath", "Disorient nearby enemies",
                "Burn and briefly disorient enemies directly around the caster.",
                CasterArea(5f), 20f, AbilityCastProfile.Instant,
                SpellSchoolMask.Fire, new ResourceCost(ResourceType.Mana, 10f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.DragonBreath),
            Modern(
                AbilityId.BlastWave, ClassId.Mage, SpecializationId.Fire,
                "Blast Wave", "Area knockback",
                "Damage and knock nearby enemies away from the caster.",
                CasterArea(6f), 25f, AbilityCastProfile.Instant,
                SpellSchoolMask.Fire, new ResourceCost(ResourceType.Mana, 12f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.BlastWave),

            Modern(
                AbilityId.Frostbolt, ClassId.Mage, SpecializationId.Frost,
                "Frostbolt", "Cast damage and slow",
                "Launch a frost projectile that damages and slows the target.",
                HostileUnit(18f), 0f, AbilityCastProfile.Cast(2f),
                SpellSchoolMask.Frost, new ResourceCost(ResourceType.Mana, 9f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.Frostbolt),
            Modern(
                AbilityId.IceLance, ClassId.Mage, SpecializationId.Frost,
                "Ice Lance", "Exploit frozen targets",
                "Deal instant damage, with a large bonus against a frozen target.",
                HostileUnit(18f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, new ResourceCost(ResourceType.Mana, 5f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.IceLance),
            Modern(
                AbilityId.DeepFreeze, ClassId.Mage, SpecializationId.Frost,
                "Deep Freeze", "Stun a frozen target",
                "Stun an enemy that is already frozen; subject to stun diminishing returns.",
                HostileUnit(15f), 30f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.RequiresFrozenTarget,
                AbilityEffectCatalog.DeepFreeze),
            Modern(
                AbilityId.IceBarrier, ClassId.Mage, SpecializationId.Frost,
                "Ice Barrier", "Personal absorb",
                "Surround yourself with an absorb shield that can be removed as beneficial magic.",
                Self(), 30f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, new ResourceCost(ResourceType.Mana, 10f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.IceBarrier),
            Modern(
                AbilityId.Polymorph, ClassId.Mage, SpecializationId.None,
                "Polymorph", "Breakable control",
                "Transform one enemy, disorienting it until damage breaks the effect while it rapidly recovers.",
                HostileUnit(18f), 0f, AbilityCastProfile.Cast(1.5f),
                SpellSchoolMask.Arcane, new ResourceCost(ResourceType.Mana, 10f),
                StandardGlobalCooldown, AbilityRequirementFlags.None, AbilityEffectCatalog.Polymorph),

            Modern(
                AbilityId.ArenaTrinket, ClassId.None, SpecializationId.None,
                "Arena Trinket", "Escape hard control",
                "Remove fear, stun, disorient, root, and slow effects from yourself.",
                Self(), 120f, AbilityCastProfile.Instant,
                SpellSchoolMask.None, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.UsableWhileStunned |
                AbilityRequirementFlags.UsableWhileHardControlled,
                AbilityEffectCatalog.ArenaTrinket),
            Modern(
                AbilityId.PowerInfusion, ClassId.Priest, SpecializationId.Discipline,
                "Power Infusion", "Accelerate spell casts",
                "Reduce your cast and channel times by 25% for ten seconds.",
                Self(), 45f, AbilityCastProfile.Instant,
                SpellSchoolMask.Holy, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.PowerInfusion),
            Modern(
                AbilityId.Combustion, ClassId.Mage, SpecializationId.Fire,
                "Combustion", "Empower fire damage",
                "Increase your Fire damage by 25% for ten seconds.",
                Self(), 45f, AbilityCastProfile.Instant,
                SpellSchoolMask.Fire, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.Combustion),
            Modern(
                AbilityId.IcyVeins, ClassId.Mage, SpecializationId.Frost,
                "Icy Veins", "Accelerate frost casts",
                "Reduce your cast and channel times by 30% for ten seconds.",
                Self(), 45f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.IcyVeins),
            Modern(
                AbilityId.Evasion, ClassId.Rogue, SpecializationId.None,
                "Evasion", "Deflect physical pressure",
                "Reduce incoming Physical damage by 60% for six seconds.",
                Self(), 45f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.Evasion),
            Modern(
                AbilityId.ColdBlood, ClassId.Rogue, SpecializationId.Mutilate,
                "Cold Blood", "Empower weapon damage",
                "Increase your Physical and Nature damage by 25% for eight seconds.",
                Self(), 45f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.ColdBlood),
            Modern(
                AbilityId.Renew, ClassId.Priest, SpecializationId.None,
                "Renew", "Healing over time",
                "Restore health to the selected ally every three seconds for fifteen seconds.",
                SelectedFriendlyUnit(15f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 12f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.Renew),
            Modern(
                AbilityId.CheapShot, ClassId.Rogue, SpecializationId.None,
                "Cheap Shot", "Stealth opening stun",
                "Stun an enemy for four seconds and add two combo points; subject to stun diminishing returns.",
                HostileUnit(2.7f, true), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 60f),
                RogueGlobalCooldown,
                AbilityRequirementFlags.RequiresStealthAccess |
                AbilityRequirementFlags.BreaksStealth,
                AbilityEffectCatalog.CheapShot),

            Modern(
                AbilityId.VampiricTouch, ClassId.Priest, SpecializationId.Shadow,
                "Vampiric Touch", "Cast a protected damage-over-time effect",
                "Afflict an enemy for fifteen seconds; dispelling it deals backlash damage to the afflicted target.",
                HostileUnit(18f), 0f, AbilityCastProfile.Cast(1.5f),
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 16f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.VampiricTouch),
            Modern(
                AbilityId.ShadowWordPain, ClassId.Priest, SpecializationId.None,
                "Shadow Word: Pain", "Instant sustained pressure",
                "Afflict an enemy for eighteen seconds; Mind Flay refreshes the effect.",
                HostileUnit(18f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 14f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.ShadowWordPain),
            Modern(
                AbilityId.DevouringPlague, ClassId.Priest, SpecializationId.Shadow,
                "Devouring Plague", "Disease damage and self-healing",
                "Deal immediate damage, then drain health over twenty-four seconds; only one target may be afflicted per caster.",
                HostileUnit(18f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 18f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.DevouringPlague),
            Modern(
                AbilityId.MindFlay, ClassId.Priest, SpecializationId.Shadow,
                "Mind Flay", "Channel damage, slow, and refresh Pain",
                "Channel three Shadow pulses that slow the target and refresh your Shadow Word: Pain.",
                HostileUnit(18f), 0f, AbilityCastProfile.Channel(3f, 1f, 3),
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 9f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.MindFlay),
            Modern(
                AbilityId.MindBlast, ClassId.Priest, SpecializationId.Shadow,
                "Mind Blast", "Burst and reduce healing",
                "Deal burst damage, reduce healing received by twenty percent, and trigger Replenishment against Vampiric Touch.",
                HostileUnit(18f), 8f, AbilityCastProfile.Cast(1.5f),
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 17f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.MindBlast),
            Modern(
                AbilityId.Silence, ClassId.Priest, SpecializationId.Shadow,
                "Silence", "Stop spellcasting at range",
                "Interrupt the current cast and silence the enemy for five seconds.",
                HostileUnit(18f), 45f, AbilityCastProfile.Instant,
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 5f),
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.Silence),
            Modern(
                AbilityId.Dispersion, ClassId.Priest, SpecializationId.Shadow,
                "Dispersion", "Survive pressure and restore mana",
                "Clear movement impairments and take ninety percent less damage for six seconds while restoring mana; attacks are disabled.",
                Self(), 90f, AbilityCastProfile.Instant,
                SpellSchoolMask.Shadow, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.UsableWhileStunned |
                AbilityRequirementFlags.UsableWhileHardControlled |
                AbilityRequirementFlags.UsableWhileSilenced,
                AbilityEffectCatalog.Dispersion),
            Modern(
                AbilityId.PsychicHorror, ClassId.Priest, SpecializationId.Shadow,
                "Psychic Horror", "Horror and disarm",
                "Horrify an enemy for three seconds and independently disarm it for ten seconds.",
                HostileUnit(18f), 120f, AbilityCastProfile.Instant,
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.PsychicHorror),
            Modern(
                AbilityId.ManaBurn, ClassId.Priest, SpecializationId.None,
                "Mana Burn", "Punish mana users",
                "Burn ten percent of the target's maximum mana, capped by the caster's mana pool, and deal half the amount as damage.",
                HostileUnit(18f), 0f, AbilityCastProfile.Cast(3f),
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 14f),
                StandardGlobalCooldown, AbilityRequirementFlags.RequiresManaTarget,
                AbilityEffectCatalog.ManaBurn),
            Modern(
                AbilityId.MassDispel, ClassId.Priest, SpecializationId.None,
                "Mass Dispel", "Cleanse a targeted area",
                "Remove one harmful Magic effect from up to ten allies and one beneficial Magic effect from up to ten enemies, including protected effects.",
                TargetArea(15f, 4f), 0f, AbilityCastProfile.Cast(1.5f),
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 33f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.MassDispel),
            Modern(
                AbilityId.Fade, ClassId.Priest, SpecializationId.None,
                "Fade", "Escape roots and slows",
                "Remove movement-impairing effects from yourself.",
                Self(), 30f, AbilityCastProfile.Instant,
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 5f),
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.Fade),

            Modern(
                AbilityId.ChimeraShot, ClassId.Hunter, SpecializationId.Marksmanship,
                "Chimera Shot", "Strike and trigger the active Sting",
                "Deal weapon damage, refresh your active Sting, and trigger its unique secondary effect.",
                HostileUnit(18f, true, 3.5f), 10f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Mana, 12f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.ChimeraShot),
            Modern(
                AbilityId.SerpentSting, ClassId.Hunter, SpecializationId.None,
                "Serpent Sting", "Sustained poison damage",
                "Poison an enemy for fifteen seconds; Chimera Shot deals part of its remaining damage immediately.",
                HostileUnit(18f, true, 3.5f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Nature, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.SerpentSting),
            Modern(
                AbilityId.ViperSting, ClassId.Hunter, SpecializationId.None,
                "Viper Sting", "Drain enemy mana",
                "Poison a mana user for eight seconds; Chimera Shot returns mana to the hunter.",
                HostileUnit(18f, true, 3.5f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Nature, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.ViperSting),
            Modern(
                AbilityId.ScorpidSting, ClassId.Hunter, SpecializationId.None,
                "Scorpid Sting", "Set up a Chimera disarm",
                "Poison an enemy for twenty seconds; Chimera Shot disarms once per internal cooldown.",
                HostileUnit(18f, true, 3.5f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Nature, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.ScorpidSting),
            Modern(
                AbilityId.AimedShot, ClassId.Hunter, SpecializationId.Marksmanship,
                "Aimed Shot", "Burst and halve healing",
                "Deal weapon damage and reduce the target's healing received by fifty percent for ten seconds.",
                HostileUnit(18f, true, 3.5f), 10f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Mana, 10f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.AimedShot),
            Modern(
                AbilityId.ArcaneShot, ClassId.Hunter, SpecializationId.None,
                "Arcane Shot", "Instant ranged pressure",
                "Deal immediate Arcane damage to the selected enemy.",
                HostileUnit(18f, true, 3.5f), 6f, AbilityCastProfile.Instant,
                SpellSchoolMask.Arcane, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.ArcaneShot),
            Modern(
                AbilityId.KillShot, ClassId.Hunter, SpecializationId.None,
                "Kill Shot", "Execute a weakened enemy",
                "Deal heavy weapon damage to an enemy below twenty percent health.",
                HostileUnit(18f, true, 3.5f), 10f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Mana, 7f),
                StandardGlobalCooldown,
                AbilityRequirementFlags.TargetBelowTwentyPercentHealth,
                AbilityEffectCatalog.KillShot),
            Modern(
                AbilityId.SteadyShot, ClassId.Hunter, SpecializationId.None,
                "Steady Shot", "Reliable casted shot",
                "Fire a reliable weapon shot after a short cast.",
                HostileUnit(18f, true, 3.5f), 0f, AbilityCastProfile.Cast(1.5f),
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Mana, 5f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.SteadyShot),
            Modern(
                AbilityId.HuntersMark, ClassId.Hunter, SpecializationId.None,
                "Hunter's Mark", "Mark a priority target",
                "Mark an enemy for two minutes so the hunter and companion can track it clearly.",
                HostileUnit(18f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Arcane, new ResourceCost(ResourceType.Mana, 3f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.HuntersMark),
            Modern(
                AbilityId.ScatterShot, ClassId.Hunter, SpecializationId.Marksmanship,
                "Scatter Shot", "Breakable ranged disorient",
                "Deal light damage and disorient the enemy for four seconds or until it takes damage.",
                HostileUnit(12f, true, 3.5f), 30f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Mana, 6f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.ScatterShot),
            Modern(
                AbilityId.FreezingArrow, ClassId.Hunter, SpecializationId.None,
                "Freezing Arrow", "Place a ranged freezing trap",
                "Launch a trap to the targeted area; the first enemy entering is frozen until damaged.",
                HostileTargetArea(18f, 3f), 30f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectSlice.Empty),
            Modern(
                AbilityId.FrostTrap, ClassId.Hunter, SpecializationId.None,
                "Frost Trap", "Create a slowing field",
                "Place a trap at your position that creates a persistent slowing field when triggered.",
                CasterArea(4f), 30f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, new ResourceCost(ResourceType.Mana, 6f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectSlice.Empty),
            Modern(
                AbilityId.SilencingShot, ClassId.Hunter, SpecializationId.Marksmanship,
                "Silencing Shot", "Instant ranged silence",
                "Silence the selected enemy for three seconds without consuming the global cooldown.",
                HostileUnit(18f, true, 3.5f), 20f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Mana, 6f),
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.SilencingShot),
            Modern(
                AbilityId.FeignDeath, ClassId.Hunter, SpecializationId.None,
                "Feign Death", "Drop enemy targeting",
                "Feign death, interrupting attacks and forcing enemy target selection to update.",
                Self(), 25f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectSlice.Empty),
            Modern(
                AbilityId.Disengage, ClassId.Hunter, SpecializationId.None,
                "Disengage", "Leap away from danger",
                "Leap backward from the current facing direction.",
                Self(), 25f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectSlice.Empty),
            Modern(
                AbilityId.WingClip, ClassId.Hunter, SpecializationId.None,
                "Wing Clip", "Melee movement slow",
                "Slow a nearby enemy by fifty percent for six seconds.",
                HostileUnit(2.7f, true), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Mana, 5f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.WingClip),
            Modern(
                AbilityId.MastersCall, ClassId.Hunter, SpecializationId.None,
                "Master's Call", "Escape movement control",
                "Command the companion to clear movement impairments and grant brief immunity to an ally.",
                FriendlyUnit(18f), 60f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Mana, 4f),
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.RequiresLivingCompanion,
                AbilityEffectSlice.Empty),
            Modern(
                AbilityId.Deterrence, ClassId.Hunter, SpecializationId.None,
                "Deterrence", "Deflect frontal attacks",
                "Become unable to attack while deflecting incoming frontal attacks for five seconds.",
                Self(), 90f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.Deterrence),
            Modern(
                AbilityId.RoarOfSacrifice, ClassId.Hunter, SpecializationId.None,
                "Roar of Sacrifice", "Protect an ally from critical pressure",
                "Command the companion to protect an ally from critical strikes for twelve seconds.",
                FriendlyUnit(18f), 60f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.RequiresLivingCompanion,
                AbilityEffectCatalog.RoarOfSacrifice),
            Modern(
                AbilityId.Readiness, ClassId.Hunter, SpecializationId.Marksmanship,
                "Readiness", "Reset tactical cooldowns",
                "Reset the hunter's other tactical cooldowns, excluding Readiness itself.",
                Self(), 180f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectSlice.Empty),
            Modern(
                AbilityId.RapidFire, ClassId.Hunter, SpecializationId.None,
                "Rapid Fire", "Accelerate ranged casts",
                "Reduce ranged cast and channel times by forty percent for fifteen seconds.",
                Self(), 180f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.RapidFire),
            Modern(
                AbilityId.PetAttack, ClassId.Hunter, SpecializationId.None,
                "Pet: Attack", "Order the companion to attack",
                "Order the companion to pursue and attack the selected enemy.",
                HostileUnit(18f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.RequiresLivingCompanion,
                AbilityEffectSlice.Empty),
            Modern(
                AbilityId.PetFollow, ClassId.Hunter, SpecializationId.None,
                "Pet: Follow", "Recall the companion",
                "Cancel the companion's attack and order it to follow the hunter.",
                Self(), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.RequiresLivingCompanion,
                AbilityEffectSlice.Empty),
            Modern(
                AbilityId.PetStay, ClassId.Hunter, SpecializationId.None,
                "Pet: Stay", "Hold the companion's position",
                "Order the companion to hold its current position.",
                Self(), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.RequiresLivingCompanion,
                AbilityEffectSlice.Empty),
            Modern(
                AbilityId.SpiderWeb, ClassId.Hunter, SpecializationId.None,
                "Spider: Web", "Root with the companion",
                "Command the spider to root the selected enemy for four seconds.",
                HostileUnit(18f), 40f, AbilityCastProfile.Instant,
                SpellSchoolMask.Nature, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.RequiresLivingCompanion,
                AbilityEffectSlice.Empty),
            Modern(
                AbilityId.MendPet, ClassId.Hunter, SpecializationId.None,
                "Mend Pet", "Restore companion health",
                "Restore health to the hunter's living companion.",
                Self(), 10f, AbilityCastProfile.Instant,
                SpellSchoolMask.Nature, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown,
                AbilityRequirementFlags.RequiresLivingCompanion,
                AbilityEffectSlice.Empty),

            Modern(
                AbilityId.TranquilizingShot, ClassId.Hunter, SpecializationId.None,
                "Tranquilizing Shot", "Purge enemy magic and enrages",
                "Remove up to one Magic effect and one Enrage effect from the selected enemy.",
                HostileUnit(18f, true, 3.5f), 6f, AbilityCastProfile.Instant,
                SpellSchoolMask.Nature, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.TranquilizingShot),
            Modern(
                AbilityId.ConcussiveShot, ClassId.Hunter, SpecializationId.None,
                "Concussive Shot", "Slow a fleeing enemy",
                "Deal light weapon damage and slow the selected enemy by fifty percent for four seconds.",
                HostileUnit(18f, true, 3.5f), 5f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Mana, 5f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.ConcussiveShot),
            Modern(
                AbilityId.Flare, ClassId.Hunter, SpecializationId.None,
                "Flare", "Reveal hidden enemies in an area",
                "Reveal and briefly mark enemies inside the targeted area. This prototype resolves the reveal when Flare lands.",
                TargetArea(18f, 4f), 20f, AbilityCastProfile.Instant,
                SpellSchoolMask.Arcane, new ResourceCost(ResourceType.Mana, 4f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.Flare),
            Modern(
                AbilityId.AspectOfTheDragonhawk, ClassId.Hunter, SpecializationId.None,
                "Aspect of the Dragonhawk", "Adopt an offensive aspect",
                "Adopt an exclusive offensive aspect, increasing damage and slightly reducing incoming Physical damage.",
                Self(), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Nature, ResourceCost.None,
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.AspectOfTheDragonhawk),
            Modern(
                AbilityId.AspectOfTheViper, ClassId.Hunter, SpecializationId.None,
                "Aspect of the Viper", "Trade damage for mana recovery",
                "Adopt an exclusive recovery aspect, restoring mana periodically while reducing damage dealt.",
                Self(), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Nature, ResourceCost.None,
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.AspectOfTheViper),

            Modern(
                AbilityId.PrayerOfMending, ClassId.Priest, SpecializationId.None,
                "Prayer of Mending", "Prime a reactive heal",
                "Place a blessing that heals the ally after the next damaging hit. Bouncing is deferred in this prototype.",
                FriendlyUnit(18f), 10f, AbilityCastProfile.Instant,
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 14f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.PrayerOfMending),
            Modern(
                AbilityId.InnerFocus, ClassId.Priest, SpecializationId.None,
                "Inner Focus", "Accelerate the next casting window",
                "Enter a focused casting window that reduces cast and channel time for fifteen seconds.",
                Self(), 45f, AbilityCastProfile.Instant,
                SpellSchoolMask.Holy, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.InnerFocus),
            Modern(
                AbilityId.FearWard, ClassId.Priest, SpecializationId.None,
                "Fear Ward", "Prevent the next fear",
                "Ward an ally against the next Fear effect. The ward is consumed when it prevents Fear.",
                FriendlyUnit(18f), 180f, AbilityCastProfile.Instant,
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 5f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.FearWard),

            Modern(
                AbilityId.Scorch, ClassId.Mage, SpecializationId.Fire,
                "Scorch", "Fast casted fire pressure",
                "Cast a quick Fire spell for mobile arena pressure.",
                HostileUnit(18f), 0f, AbilityCastProfile.Cast(1.5f),
                SpellSchoolMask.Fire, new ResourceCost(ResourceType.Mana, 6f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.Scorch),
            Modern(
                AbilityId.Pyroblast, ClassId.Mage, SpecializationId.Fire,
                "Pyroblast", "Hard-cast heavy fire burst",
                "Launch a long hard-cast projectile for heavy Fire damage; instant Hot Streak behavior is not modeled yet.",
                HostileUnit(18f), 0f, AbilityCastProfile.Cast(4.5f),
                SpellSchoolMask.Fire, new ResourceCost(ResourceType.Mana, 18f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.Pyroblast),
            Modern(
                AbilityId.ConeOfCold, ClassId.Mage, SpecializationId.None,
                "Cone of Cold", "Damage and slow nearby enemies",
                "Blast nearby enemies with Frost damage and slow them for four seconds.",
                CasterArea(5f), 8f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, new ResourceCost(ResourceType.Mana, 10f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.ConeOfCold),
            Modern(
                AbilityId.Spellsteal, ClassId.Mage, SpecializationId.None,
                "Spellsteal", "Steal an enemy Magic buff",
                "Remove one beneficial Magic effect from an enemy and transfer its remaining duration to the mage.",
                HostileUnit(18f), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Arcane, new ResourceCost(ResourceType.Mana, 15f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.Spellsteal),
            Modern(
                AbilityId.IceBlock, ClassId.Mage, SpecializationId.None,
                "Ice Block", "Become immune but unable to act",
                "Clear control and become immune to damage for ten seconds while unable to use abilities.",
                Self(), 240f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.UsableWhileStunned |
                AbilityRequirementFlags.UsableWhileHardControlled |
                AbilityRequirementFlags.UsableWhileSilenced,
                AbilityEffectCatalog.IceBlock),
            Modern(
                AbilityId.ManaShield, ClassId.Mage, SpecializationId.None,
                "Mana Shield", "Absorb emergency damage",
                "Spend mana to place a short-lived damage absorb on yourself.",
                Self(), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Arcane, new ResourceCost(ResourceType.Mana, 18f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.ManaShield),
            Modern(
                AbilityId.MoltenArmor, ClassId.Mage, SpecializationId.None,
                "Molten Armor", "Adopt the Fire damage armor",
                "Adopt an exclusive armor that increases Fire damage dealt.",
                Self(), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Fire, new ResourceCost(ResourceType.Mana, 4f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.MoltenArmor),
            Modern(
                AbilityId.MageArmor, ClassId.Mage, SpecializationId.None,
                "Mage Armor", "Adopt the anti-magic armor",
                "Adopt an exclusive armor that reduces incoming damage.",
                Self(), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Arcane, new ResourceCost(ResourceType.Mana, 4f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.MageArmor),
            Modern(
                AbilityId.IceArmor, ClassId.Mage, SpecializationId.None,
                "Ice Armor", "Adopt the anti-melee armor",
                "Adopt an exclusive armor that strongly reduces incoming Physical damage.",
                Self(), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, new ResourceCost(ResourceType.Mana, 4f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.IceArmor),
            Modern(
                AbilityId.WaterElementalFreeze, ClassId.Mage, SpecializationId.Frost,
                "Water Elemental: Freeze", "Root enemies at a target area",
                "Command the Water Elemental to freeze enemies in a targeted area; the persistent pet is abstracted in this prototype.",
                HostileTargetArea(18f, 4f), 25f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.WaterElementalFreeze),
            Modern(
                AbilityId.ColdSnap, ClassId.Mage, SpecializationId.Frost,
                "Cold Snap", "Reset Frost cooldowns",
                "Reset the mage's Frost tactical cooldowns, excluding Cold Snap itself.",
                Self(), 300f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.ColdSnap),

            Modern(
                AbilityId.Sap, ClassId.Rogue, SpecializationId.None,
                "Sap", "Stealth disorient an enemy",
                "Disorient an enemy for eight seconds or until damaged. Combat-state restrictions are deferred in this prototype.",
                HostileUnit(2.7f, true), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 35f),
                RogueGlobalCooldown, AbilityRequirementFlags.RequiresStealthAccess,
                AbilityEffectCatalog.Sap),
            Modern(
                AbilityId.Garrote, ClassId.Rogue, SpecializationId.None,
                "Garrote", "Open with a bleeding attack",
                "Strike from stealth, add a combo point, and bleed the enemy for eighteen seconds.",
                HostileUnit(2.7f, true), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 50f),
                RogueGlobalCooldown, AbilityRequirementFlags.RequiresStealthAccess |
                AbilityRequirementFlags.RequiresBehindTarget |
                AbilityRequirementFlags.BreaksStealth,
                AbilityEffectCatalog.Garrote),
            Modern(
                AbilityId.Gouge, ClassId.Rogue, SpecializationId.None,
                "Gouge", "Short breakable disorient",
                "Deal light damage and disorient the enemy for four seconds or until damaged.",
                HostileUnit(2.7f, true), 10f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 45f),
                RogueGlobalCooldown, AbilityRequirementFlags.BreaksStealth,
                AbilityEffectCatalog.Gouge),
            Modern(
                AbilityId.Sprint, ClassId.Rogue, SpecializationId.None,
                "Sprint", "Rapidly close or escape",
                "Increase movement speed by seventy percent for eight seconds.",
                Self(), 180f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.Sprint),
            Modern(
                AbilityId.Dismantle, ClassId.Rogue, SpecializationId.None,
                "Dismantle", "Disarm an enemy",
                "Disarm the selected enemy for ten seconds, subject to disarm diminishing returns.",
                HostileUnit(2.7f, true), 60f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 25f),
                RogueGlobalCooldown, AbilityRequirementFlags.BreaksStealth,
                AbilityEffectCatalog.Dismantle),
            Modern(
                AbilityId.Preparation, ClassId.Rogue, SpecializationId.None,
                "Preparation", "Reset mobility and escape cooldowns",
                "Reset Evasion, Sprint, Vanish, and Shadowstep, excluding Preparation itself.",
                Self(), 300f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.Preparation),

            Modern(
                AbilityId.Stealth, ClassId.Rogue, SpecializationId.None,
                "Stealth", "Enter or leave concealment",
                "Enter persistent stealth while no living enemy is engaged; attacks break concealment and Vanish remains the combat escape.",
                Self(), 1f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown |
                AbilityRequirementFlags.RequiresNoLivingHostile,
                AbilityEffectCatalog.Stealth),
            Modern(
                AbilityId.Shadowform, ClassId.Priest, SpecializationId.Shadow,
                "Shadowform", "Toggle shadow stance",
                "Enter or leave Shadowform. Leave it before casting Flash Heal, then return to shadow pressure when safe.",
                Self(), 0f, AbilityCastProfile.Instant,
                SpellSchoolMask.Shadow, ResourceCost.None,
                0f, AbilityRequirementFlags.IgnoresGlobalCooldown,
                AbilityEffectCatalog.Shadowform),
            Modern(
                AbilityId.Smite, ClassId.Priest, SpecializationId.Discipline,
                "Smite", "Reliable holy damage",
                "Cast a focused bolt of Holy damage as Discipline's repeatable ranged attack.",
                HostileUnit(18f), 0f, AbilityCastProfile.Cast(1.5f),
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 8f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.Smite),
            Modern(
                AbilityId.BladeTempest, ClassId.Rogue, SpecializationId.None,
                "Blade Tempest", "Damage nearby enemies",
                "Throw a ring of blades, damaging every nearby enemy. Available only in Hero Siege.",
                CasterArea(5f), 4f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Energy, 35f),
                RogueGlobalCooldown, AbilityRequirementFlags.BreaksStealth,
                AbilityEffectCatalog.BladeTempest),
            Modern(
                AbilityId.DawnNova, ClassId.Priest, SpecializationId.Discipline,
                "Dawn Nova", "Damage foes and mend allies",
                "Release a radiant nova that damages nearby enemies and restores nearby allies. Available only in Hero Siege.",
                CasterArea(5.5f), 5f, AbilityCastProfile.Instant,
                SpellSchoolMask.Holy, new ResourceCost(ResourceType.Mana, 16f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.DawnNova),
            Modern(
                AbilityId.UmbralCollapse, ClassId.Priest, SpecializationId.Shadow,
                "Umbral Collapse", "Collapse a hostile group",
                "Crush a selected enemy group in a field of shadow. Available only in Hero Siege.",
                HostileTargetArea(18f, 4.5f), 5f, AbilityCastProfile.Instant,
                SpellSchoolMask.Shadow, new ResourceCost(ResourceType.Mana, 18f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.UmbralCollapse),
            Modern(
                AbilityId.FlameSurge, ClassId.Mage, SpecializationId.Fire,
                "Flame Surge", "Burn a hostile group",
                "Detonate a wide surge of flame around the selected enemy. Available only in Hero Siege.",
                HostileTargetArea(18f, 4.5f), 4f, AbilityCastProfile.Instant,
                SpellSchoolMask.Fire, new ResourceCost(ResourceType.Mana, 18f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.FlameSurge),
            Modern(
                AbilityId.Blizzard, ClassId.Mage, SpecializationId.Frost,
                "Blizzard", "Damage and slow a hostile group",
                "Call down freezing shards that damage and briefly slow an enemy group. Available only in Hero Siege.",
                HostileTargetArea(18f, 5f), 5f, AbilityCastProfile.Instant,
                SpellSchoolMask.Frost, new ResourceCost(ResourceType.Mana, 18f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.Blizzard),
            Modern(
                AbilityId.ArrowRain, ClassId.Hunter, SpecializationId.Marksmanship,
                "Arrow Rain", "Volley into a hostile group",
                "Loose a dense volley that damages every enemy near the selected target. Available only in Hero Siege.",
                HostileTargetArea(20f, 5f), 4f, AbilityCastProfile.Instant,
                SpellSchoolMask.Physical, new ResourceCost(ResourceType.Mana, 14f),
                StandardGlobalCooldown, AbilityRequirementFlags.None,
                AbilityEffectCatalog.ArrowRain),
        };

        public static AbilityDefinition Get(AbilityId id)
        {
            if (!TryGet(id, out AbilityDefinition definition))
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, "Unknown ability ID.");
            }

            return definition;
        }

        public static bool TryGet(AbilityId id, out AbilityDefinition definition)
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

        public static AbilityKit GetKit(
            ClassId classId,
            SpecializationId specialization) =>
            SpecializationCatalog.GetKit(classId, specialization);

        public static AbilityId GetSpecializationAbility(
            SpecializationId specialization,
            int slot)
        {
            ValidateSlot(slot);
            return SpecializationCatalog.GetKit(specialization).GetAbility(slot);
        }

        public static AbilityId GetSpecializationAbility(
            SpecializationId specialization,
            int slot,
            AbilitySlotDirection direction)
        {
            ValidateSlot(slot);
            return SpecializationCatalog.GetKit(specialization).GetAbility(slot, direction);
        }

        public static AbilityId GetClassAbility(ClassId classId, int slot)
        {
            ValidateSlot(slot);
            SpecializationId specialization = SpecializationCatalog.GetDefault(classId);
            return specialization == SpecializationId.None
                ? AbilityId.None
                : GetSpecializationAbility(specialization, slot);
        }

        public static bool IsAbilityAllowed(
            ClassId classId,
            SpecializationId specialization,
            AbilityId ability)
        {
            return IsAbilityAllowed(
                classId,
                specialization,
                ability,
                PlayableModeId.None);
        }

        public static bool IsAbilityAllowed(
            ClassId classId,
            SpecializationId specialization,
            AbilityId ability,
            PlayableModeId mode)
        {
            if (!SpecializationCatalog.IsCompatible(classId, specialization) ||
                !TryGet(ability, out AbilityDefinition definition) ||
                (definition.OwnerClass != ClassId.None &&
                 definition.OwnerClass != classId))
            {
                return false;
            }

            return ModeAbilityKitCatalog.GetKit(mode, specialization).Contains(ability);
        }

        public static bool IsWeaponAbility(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.QuickStrike:
                case AbilityId.Ambush:
                case AbilityId.Hemorrhage:
                case AbilityId.Eviscerate:
                case AbilityId.KidneyShot:
                case AbilityId.Mutilate:
                case AbilityId.Envenom:
                case AbilityId.Shiv:
                case AbilityId.CheapShot:
                case AbilityId.Garrote:
                case AbilityId.Gouge:
                case AbilityId.Dismantle:
                case AbilityId.BladeTempest:
                case AbilityId.ArrowRain:
                case AbilityId.ChimeraShot:
                case AbilityId.SerpentSting:
                case AbilityId.ViperSting:
                case AbilityId.ScorpidSting:
                case AbilityId.AimedShot:
                case AbilityId.ArcaneShot:
                case AbilityId.KillShot:
                case AbilityId.SteadyShot:
                case AbilityId.ScatterShot:
                case AbilityId.SilencingShot:
                case AbilityId.WingClip:
                case AbilityId.TranquilizingShot:
                case AbilityId.ConcussiveShot:
                    return true;
                default:
                    return false;
            }
        }

        public static AbilityEffectDefinition GetEffect(AbilityId ability, int effectIndex)
        {
            AbilityEffectSlice effects = Get(ability).Effects;
            if (effectIndex < 0 || effectIndex >= effects.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(effectIndex));
            }

            return AbilityEffectCatalog.Get(effects.Offset + effectIndex);
        }

        private static AbilityDefinition Modern(
            AbilityId id,
            ClassId ownerClass,
            SpecializationId ownerSpecialization,
            string displayName,
            string shortPurpose,
            string description,
            AbilityTargetRule targeting,
            float cooldownSeconds,
            AbilityCastProfile castProfile,
            SpellSchoolMask school,
            ResourceCost resourceCost,
            float globalCooldownSeconds,
            AbilityRequirementFlags requirements,
            AbilityEffectSlice effects)
        {
            return new AbilityDefinition(
                id,
                ownerClass,
                ownerSpecialization,
                displayName,
                shortPurpose,
                description,
                targeting,
                cooldownSeconds,
                castProfile,
                school,
                resourceCost,
                globalCooldownSeconds,
                requirements,
                effects);
        }

        private static AbilityTargetRule Self()
        {
            return new AbilityTargetRule(
                AbilityTargetShape.Unit,
                AbilityTargetRelation.Self,
                AbilitySmartTargetPolicy.Self,
                0f,
                requiresLineOfSight: false);
        }

        private static AbilityTargetRule FriendlyUnit(float range)
        {
            return new AbilityTargetRule(
                AbilityTargetShape.Unit,
                AbilityTargetRelation.Self | AbilityTargetRelation.Friendly,
                AbilitySmartTargetPolicy.LowestHealthFriendly,
                range);
        }

        private static AbilityTargetRule SelectedFriendlyUnit(float range)
        {
            return new AbilityTargetRule(
                AbilityTargetShape.Unit,
                AbilityTargetRelation.Self | AbilityTargetRelation.Friendly,
                AbilitySmartTargetPolicy.SelectedFriendly,
                range);
        }

        private static AbilityTargetRule HostileUnit(
            float range,
            bool requiresFacing = false,
            float minimumRange = 0f)
        {
            return new AbilityTargetRule(
                AbilityTargetShape.Unit,
                AbilityTargetRelation.Hostile,
                AbilitySmartTargetPolicy.SelectedHostile,
                range,
                requiresFacing: requiresFacing,
                minimumRange: minimumRange);
        }

        private static AbilityTargetRule DualUnit(float range)
        {
            return new AbilityTargetRule(
                AbilityTargetShape.Unit,
                AbilityTargetRelation.Self |
                AbilityTargetRelation.Friendly |
                AbilityTargetRelation.Hostile,
                AbilitySmartTargetPolicy.ContextualFriendlyOrHostile,
                range);
        }

        private static AbilityTargetRule Point(float range)
        {
            return new AbilityTargetRule(
                AbilityTargetShape.Point,
                AbilityTargetRelation.None,
                AbilitySmartTargetPolicy.AimPoint,
                range,
                requiresLineOfSight: false);
        }

        private static AbilityTargetRule TargetArea(float range, float radius)
        {
            return new AbilityTargetRule(
                AbilityTargetShape.TargetArea,
                AbilityTargetRelation.Self |
                AbilityTargetRelation.Friendly |
                AbilityTargetRelation.Hostile,
                AbilitySmartTargetPolicy.AimPoint,
                range,
                radius,
                requiresLineOfSight: true);
        }

        private static AbilityTargetRule HostileTargetArea(float range, float radius)
        {
            return new AbilityTargetRule(
                AbilityTargetShape.TargetArea,
                AbilityTargetRelation.Hostile,
                AbilitySmartTargetPolicy.SelectedHostile,
                range,
                radius,
                requiresLineOfSight: true);
        }

        private static AbilityTargetRule CasterArea(float radius)
        {
            return new AbilityTargetRule(
                AbilityTargetShape.CasterArea,
                AbilityTargetRelation.Hostile,
                AbilitySmartTargetPolicy.None,
                0f,
                radius,
                false);
        }

        private static void ValidateSlot(int slot)
        {
            if (slot < 0 || slot >= AbilitySlotCount)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }
    }
}
