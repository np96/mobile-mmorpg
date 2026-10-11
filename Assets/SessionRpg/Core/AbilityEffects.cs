using System;

namespace SessionRpg.Core
{
    public enum AbilityEffectKind
    {
        None,
        Damage,
        Heal,
        ApplyAura,
        Dispel,
        Interrupt,
        Knockback,
        Teleport,
        ModifyResource,
        AddComboPoints,
        ConsumeComboPointsDamage,
        ConsumeComboPointsAura,
        Summon,
        RemoveAurasByTag,
        LeechDamage,
        ModifyResourcePercentMaximum,
        BurnResource,
        RefreshAuraDuration,
        DispelArea,
        RemoveAnyAurasByTag,
        Spellsteal,
        ResetCooldowns,
        ToggleAura,
    }

    public enum AbilityEffectMoment
    {
        Accepted,
        Completed,
        ChannelTick,
        AuraPeriodic,
        AuraExpired,
        AuraDispelled,
    }

    public enum AbilityEffectTarget
    {
        Caster,
        SelectedTarget,
        AreaFriendlies,
        AreaHostiles,
        TeamFriendlies,
        Dispeller,
    }

    public enum AbilityEffectCondition
    {
        Always,
        TargetIsFriendly,
        TargetIsHostile,
        TargetSurvivedPreviousEffect,
        CasterHasStealthAccess,
        TargetIsPoisoned,
        TargetIsFrozen,
        TargetHasAura,
        TargetHasResource,
    }

    public enum DispelSelectionMode
    {
        HarmfulFromFriendly,
        BeneficialFromHostile,
        RelationAppropriate,
        AreaHarmfulFromFriendlyIncludingProtected,
        AreaBeneficialFromHostileIncludingProtected,
    }

    public enum SummonId
    {
        None,
        UmbralFamiliar,
    }

    public enum CooldownResetGroup
    {
        None,
        MageFrost,
        RoguePreparation,
    }

    public readonly struct AbilityEffectSlice
    {
        public AbilityEffectSlice(int offset, int count)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset));
            }

            if (count < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(count));
            }

            Offset = offset;
            Count = count;
        }

        public int Offset { get; }

        public int Count { get; }

        public bool IsEmpty => Count == 0;

        public static AbilityEffectSlice Empty => default;
    }

    public readonly struct AbilityEffectDefinition
    {
        private AbilityEffectDefinition(
            AbilityEffectKind kind,
            AbilityEffectMoment moment,
            AbilityEffectTarget target,
            AbilityEffectCondition condition,
            float amount,
            float secondaryAmount,
            int count,
            AuraId aura,
            SummonId summon,
            DispelType dispelType,
            DispelSelectionMode dispelSelection,
            SpellSchoolMask school,
            AuraTag auraTags = AuraTag.None,
            float tertiaryAmount = 0f,
            AuraId conditionAura = AuraId.None)
        {
            Kind = kind;
            Moment = moment;
            Target = target;
            Condition = condition;
            Amount = amount;
            SecondaryAmount = secondaryAmount;
            Count = count;
            Aura = aura;
            Summon = summon;
            DispelType = dispelType;
            DispelSelection = dispelSelection;
            School = school;
            AuraTags = auraTags;
            TertiaryAmount = tertiaryAmount;
            ConditionAura = conditionAura;
        }

        public AbilityEffectKind Kind { get; }

        public AbilityEffectMoment Moment { get; }

        public AbilityEffectTarget Target { get; }

        public AbilityEffectCondition Condition { get; }

        public float Amount { get; }

        public float SecondaryAmount { get; }

        public int Count { get; }

        public AuraId Aura { get; }

        public SummonId Summon { get; }

        public DispelType DispelType { get; }

        public DispelSelectionMode DispelSelection { get; }

        public SpellSchoolMask School { get; }

        public AuraTag AuraTags { get; }

        public float TertiaryAmount { get; }

        public AuraId ConditionAura { get; }

        public static AbilityEffectDefinition Damage(
            float amount,
            SpellSchoolMask school,
            AbilityEffectMoment moment = AbilityEffectMoment.Completed,
            AbilityEffectTarget target = AbilityEffectTarget.SelectedTarget,
            AbilityEffectCondition condition = AbilityEffectCondition.Always)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.Damage,
                moment,
                target,
                condition,
                amount,
                0f,
                0,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                school);
        }

        public static AbilityEffectDefinition Heal(
            float amount,
            AbilityEffectMoment moment = AbilityEffectMoment.Completed,
            AbilityEffectTarget target = AbilityEffectTarget.SelectedTarget,
            AbilityEffectCondition condition = AbilityEffectCondition.Always)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.Heal,
                moment,
                target,
                condition,
                amount,
                0f,
                0,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.Holy);
        }

        public static AbilityEffectDefinition ApplyAura(
            AuraId aura,
            float durationSeconds,
            float magnitude = 0f,
            AbilityEffectMoment moment = AbilityEffectMoment.Completed,
            AbilityEffectTarget target = AbilityEffectTarget.SelectedTarget,
            AbilityEffectCondition condition = AbilityEffectCondition.Always,
            AuraId conditionAura = AuraId.None)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.ApplyAura,
                moment,
                target,
                condition,
                magnitude,
                durationSeconds,
                0,
                aura,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None,
                conditionAura: conditionAura);
        }

        public static AbilityEffectDefinition Dispel(
            DispelType type,
            int count,
            DispelSelectionMode selection,
            AbilityEffectTarget target = AbilityEffectTarget.SelectedTarget)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.Dispel,
                AbilityEffectMoment.Completed,
                target,
                AbilityEffectCondition.Always,
                0f,
                0f,
                Math.Max(0, count),
                AuraId.None,
                SummonId.None,
                type,
                selection,
                SpellSchoolMask.None);
        }

        public static AbilityEffectDefinition Interrupt(float lockoutSeconds)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.Interrupt,
                AbilityEffectMoment.Completed,
                AbilityEffectTarget.SelectedTarget,
                AbilityEffectCondition.Always,
                Math.Max(0f, lockoutSeconds),
                0f,
                0,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None);
        }

        public static AbilityEffectDefinition Knockback(
            float distance,
            AbilityEffectTarget target = AbilityEffectTarget.SelectedTarget)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.Knockback,
                AbilityEffectMoment.Completed,
                target,
                AbilityEffectCondition.Always,
                distance,
                0f,
                0,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None);
        }

        public static AbilityEffectDefinition Teleport(float distance)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.Teleport,
                AbilityEffectMoment.Completed,
                AbilityEffectTarget.Caster,
                AbilityEffectCondition.Always,
                distance,
                0f,
                0,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None);
        }

        public static AbilityEffectDefinition ModifyResource(
            float amount,
            ResourceType resourceType,
            AbilityEffectMoment moment,
            AbilityEffectTarget target)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.ModifyResource,
                moment,
                target,
                AbilityEffectCondition.Always,
                amount,
                0f,
                (int)resourceType,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None);
        }

        public static AbilityEffectDefinition AddComboPoints(int count)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.AddComboPoints,
                AbilityEffectMoment.Completed,
                AbilityEffectTarget.SelectedTarget,
                AbilityEffectCondition.Always,
                0f,
                0f,
                Math.Max(0, count),
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.Physical);
        }

        public static AbilityEffectDefinition ConsumeComboPointsDamage(
            float damagePerPoint,
            SpellSchoolMask school)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.ConsumeComboPointsDamage,
                AbilityEffectMoment.Completed,
                AbilityEffectTarget.SelectedTarget,
                AbilityEffectCondition.Always,
                damagePerPoint,
                0f,
                0,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                school);
        }

        public static AbilityEffectDefinition ConsumeComboPointsAura(
            AuraId aura,
            float secondsPerPoint)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.ConsumeComboPointsAura,
                AbilityEffectMoment.Completed,
                AbilityEffectTarget.SelectedTarget,
                AbilityEffectCondition.Always,
                secondsPerPoint,
                0f,
                0,
                aura,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.Physical);
        }

        public static AbilityEffectDefinition SummonUnit(
            SummonId summon,
            float durationSeconds)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.Summon,
                AbilityEffectMoment.Completed,
                AbilityEffectTarget.SelectedTarget,
                AbilityEffectCondition.Always,
                durationSeconds,
                0f,
                0,
                AuraId.None,
                summon,
                DispelType.None,
                default,
                SpellSchoolMask.Shadow);
        }

        public static AbilityEffectDefinition RemoveAurasByTag(
            AuraTag auraTags,
            AbilityEffectTarget target = AbilityEffectTarget.Caster)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.RemoveAurasByTag,
                AbilityEffectMoment.Completed,
                target,
                AbilityEffectCondition.Always,
                0f,
                0f,
                0,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None,
                auraTags);
        }

        public static AbilityEffectDefinition LeechDamage(
            float amount,
            float healingFraction,
            SpellSchoolMask school,
            AbilityEffectMoment moment = AbilityEffectMoment.Completed,
            AbilityEffectTarget target = AbilityEffectTarget.SelectedTarget)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.LeechDamage,
                moment,
                target,
                AbilityEffectCondition.Always,
                amount,
                Math.Max(0f, healingFraction),
                0,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                school);
        }

        public static AbilityEffectDefinition ModifyResourcePercentMaximum(
            float fraction,
            ResourceType resourceType,
            AbilityEffectMoment moment,
            AbilityEffectTarget target)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.ModifyResourcePercentMaximum,
                moment,
                target,
                AbilityEffectCondition.Always,
                fraction,
                0f,
                (int)resourceType,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None);
        }

        public static AbilityEffectDefinition BurnResource(
            float targetMaximumFraction,
            float casterMaximumCapFraction,
            float damagePerResource,
            ResourceType resourceType,
            AbilityEffectMoment moment = AbilityEffectMoment.Completed,
            AbilityEffectTarget target = AbilityEffectTarget.SelectedTarget)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.BurnResource,
                moment,
                target,
                AbilityEffectCondition.TargetHasResource,
                targetMaximumFraction,
                casterMaximumCapFraction,
                (int)resourceType,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.Shadow,
                tertiaryAmount: damagePerResource);
        }

        public static AbilityEffectDefinition RefreshAuraDuration(
            AuraId aura,
            float durationSeconds,
            AbilityEffectMoment moment = AbilityEffectMoment.Completed,
            AbilityEffectTarget target = AbilityEffectTarget.SelectedTarget)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.RefreshAuraDuration,
                moment,
                target,
                AbilityEffectCondition.TargetHasAura,
                0f,
                Math.Max(0f, durationSeconds),
                0,
                aura,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None,
                conditionAura: aura);
        }

        public static AbilityEffectDefinition DispelArea(
            DispelType type,
            int maximumTargets,
            DispelSelectionMode selection,
            AbilityEffectTarget target)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.DispelArea,
                AbilityEffectMoment.Completed,
                target,
                AbilityEffectCondition.Always,
                0f,
                0f,
                Math.Max(0, maximumTargets),
                AuraId.None,
                SummonId.None,
                type,
                selection,
                SpellSchoolMask.None);
        }

        public static AbilityEffectDefinition RemoveAnyAurasByTag(
            AuraTag auraTags,
            AbilityEffectTarget target)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.RemoveAnyAurasByTag,
                AbilityEffectMoment.Completed,
                target,
                AbilityEffectCondition.Always,
                0f,
                0f,
                0,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None,
                auraTags);
        }

        public static AbilityEffectDefinition Spellsteal(DispelType type)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.Spellsteal,
                AbilityEffectMoment.Completed,
                AbilityEffectTarget.SelectedTarget,
                AbilityEffectCondition.Always,
                0f,
                0f,
                1,
                AuraId.None,
                SummonId.None,
                type,
                DispelSelectionMode.BeneficialFromHostile,
                SpellSchoolMask.Arcane);
        }

        public static AbilityEffectDefinition ResetCooldowns(
            CooldownResetGroup group)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.ResetCooldowns,
                AbilityEffectMoment.Completed,
                AbilityEffectTarget.Caster,
                AbilityEffectCondition.Always,
                0f,
                0f,
                (int)group,
                AuraId.None,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None);
        }

        public static AbilityEffectDefinition ToggleAura(
            AuraId aura,
            float enabledDurationSeconds = 86400f)
        {
            return new AbilityEffectDefinition(
                AbilityEffectKind.ToggleAura,
                AbilityEffectMoment.Completed,
                AbilityEffectTarget.Caster,
                AbilityEffectCondition.Always,
                enabledDurationSeconds,
                0f,
                0,
                aura,
                SummonId.None,
                DispelType.None,
                default,
                SpellSchoolMask.None);
        }
    }

    public static class AbilityEffectCatalog
    {
        public static readonly AbilityEffectSlice Penance = new AbilityEffectSlice(0, 2);
        public static readonly AbilityEffectSlice FlashHeal = new AbilityEffectSlice(2, 1);
        public static readonly AbilityEffectSlice PowerWordShield = new AbilityEffectSlice(3, 2);
        public static readonly AbilityEffectSlice PainSuppression = new AbilityEffectSlice(5, 1);
        public static readonly AbilityEffectSlice PsychicScream = new AbilityEffectSlice(6, 1);
        public static readonly AbilityEffectSlice ShadowWordDeath = new AbilityEffectSlice(7, 2);
        public static readonly AbilityEffectSlice Shadowfiend = new AbilityEffectSlice(9, 1);
        public static readonly AbilityEffectSlice DispelMagic = new AbilityEffectSlice(10, 1);
        public static readonly AbilityEffectSlice Ambush = new AbilityEffectSlice(11, 2);
        public static readonly AbilityEffectSlice Hemorrhage = new AbilityEffectSlice(13, 2);
        public static readonly AbilityEffectSlice Eviscerate = new AbilityEffectSlice(15, 1);
        public static readonly AbilityEffectSlice KidneyShot = new AbilityEffectSlice(16, 1);
        public static readonly AbilityEffectSlice ShadowDance = new AbilityEffectSlice(17, 1);
        public static readonly AbilityEffectSlice Shadowstep = new AbilityEffectSlice(18, 1);
        public static readonly AbilityEffectSlice Kick = new AbilityEffectSlice(19, 1);
        public static readonly AbilityEffectSlice CloakOfShadows = new AbilityEffectSlice(20, 2);
        public static readonly AbilityEffectSlice Mutilate = new AbilityEffectSlice(22, 3);
        public static readonly AbilityEffectSlice Envenom = new AbilityEffectSlice(25, 2);
        public static readonly AbilityEffectSlice Shiv = new AbilityEffectSlice(27, 3);
        public static readonly AbilityEffectSlice Blind = new AbilityEffectSlice(30, 1);
        public static readonly AbilityEffectSlice Vanish = new AbilityEffectSlice(31, 1);
        public static readonly AbilityEffectSlice Fireball = new AbilityEffectSlice(32, 1);
        public static readonly AbilityEffectSlice FireBlast = new AbilityEffectSlice(33, 1);
        public static readonly AbilityEffectSlice LivingBomb = new AbilityEffectSlice(34, 1);
        public static readonly AbilityEffectSlice DragonBreath = new AbilityEffectSlice(35, 2);
        public static readonly AbilityEffectSlice BlastWave = new AbilityEffectSlice(37, 2);
        public static readonly AbilityEffectSlice Counterspell = new AbilityEffectSlice(39, 1);
        public static readonly AbilityEffectSlice Blink = new AbilityEffectSlice(40, 1);
        public static readonly AbilityEffectSlice Polymorph = new AbilityEffectSlice(41, 1);
        public static readonly AbilityEffectSlice Frostbolt = new AbilityEffectSlice(42, 2);
        public static readonly AbilityEffectSlice IceLance = new AbilityEffectSlice(44, 2);
        public static readonly AbilityEffectSlice FrostNova = new AbilityEffectSlice(46, 2);
        public static readonly AbilityEffectSlice DeepFreeze = new AbilityEffectSlice(48, 1);
        public static readonly AbilityEffectSlice IceBarrier = new AbilityEffectSlice(49, 1);
        public static readonly AbilityEffectSlice LivingBombPeriodic = new AbilityEffectSlice(50, 1);
        public static readonly AbilityEffectSlice LivingBombExpired = new AbilityEffectSlice(51, 1);
        public static readonly AbilityEffectSlice PolymorphPeriodic = new AbilityEffectSlice(52, 1);
        public static readonly AbilityEffectSlice ArenaTrinket = new AbilityEffectSlice(53, 1);
        public static readonly AbilityEffectSlice PowerInfusion = new AbilityEffectSlice(54, 1);
        public static readonly AbilityEffectSlice Combustion = new AbilityEffectSlice(55, 1);
        public static readonly AbilityEffectSlice IcyVeins = new AbilityEffectSlice(56, 1);
        public static readonly AbilityEffectSlice Evasion = new AbilityEffectSlice(57, 1);
        public static readonly AbilityEffectSlice ColdBlood = new AbilityEffectSlice(58, 1);
        public static readonly AbilityEffectSlice Renew = new AbilityEffectSlice(59, 1);
        public static readonly AbilityEffectSlice RenewPeriodic = new AbilityEffectSlice(60, 1);
        public static readonly AbilityEffectSlice CheapShot = new AbilityEffectSlice(61, 2);
        public static readonly AbilityEffectSlice VampiricTouch = new AbilityEffectSlice(63, 1);
        public static readonly AbilityEffectSlice VampiricTouchPeriodic = new AbilityEffectSlice(64, 1);
        public static readonly AbilityEffectSlice VampiricTouchDispelled = new AbilityEffectSlice(65, 1);
        public static readonly AbilityEffectSlice ShadowWordPain = new AbilityEffectSlice(66, 1);
        public static readonly AbilityEffectSlice ShadowWordPainPeriodic = new AbilityEffectSlice(67, 1);
        public static readonly AbilityEffectSlice DevouringPlague = new AbilityEffectSlice(68, 2);
        public static readonly AbilityEffectSlice DevouringPlaguePeriodic = new AbilityEffectSlice(70, 1);
        public static readonly AbilityEffectSlice MindFlay = new AbilityEffectSlice(71, 3);
        public static readonly AbilityEffectSlice MindBlast = new AbilityEffectSlice(74, 3);
        public static readonly AbilityEffectSlice Silence = new AbilityEffectSlice(77, 1);
        public static readonly AbilityEffectSlice Dispersion = new AbilityEffectSlice(78, 2);
        public static readonly AbilityEffectSlice DispersionPeriodic = new AbilityEffectSlice(80, 1);
        public static readonly AbilityEffectSlice PsychicHorror = new AbilityEffectSlice(81, 2);
        public static readonly AbilityEffectSlice ManaBurn = new AbilityEffectSlice(83, 1);
        public static readonly AbilityEffectSlice MassDispel = new AbilityEffectSlice(84, 2);
        public static readonly AbilityEffectSlice Fade = new AbilityEffectSlice(86, 1);
        public static readonly AbilityEffectSlice ReplenishmentPeriodic = new AbilityEffectSlice(87, 1);
        public static readonly AbilityEffectSlice ChimeraShot = new AbilityEffectSlice(88, 1);
        public static readonly AbilityEffectSlice SerpentSting = new AbilityEffectSlice(89, 1);
        public static readonly AbilityEffectSlice SerpentStingPeriodic = new AbilityEffectSlice(90, 1);
        public static readonly AbilityEffectSlice ViperSting = new AbilityEffectSlice(91, 1);
        public static readonly AbilityEffectSlice ViperStingPeriodic = new AbilityEffectSlice(92, 1);
        public static readonly AbilityEffectSlice ScorpidSting = new AbilityEffectSlice(93, 1);
        public static readonly AbilityEffectSlice AimedShot = new AbilityEffectSlice(94, 2);
        public static readonly AbilityEffectSlice ArcaneShot = new AbilityEffectSlice(96, 1);
        public static readonly AbilityEffectSlice KillShot = new AbilityEffectSlice(97, 1);
        public static readonly AbilityEffectSlice SteadyShot = new AbilityEffectSlice(98, 1);
        public static readonly AbilityEffectSlice HuntersMark = new AbilityEffectSlice(99, 1);
        public static readonly AbilityEffectSlice ScatterShot = new AbilityEffectSlice(100, 2);
        public static readonly AbilityEffectSlice SilencingShot = new AbilityEffectSlice(102, 1);
        public static readonly AbilityEffectSlice WingClip = new AbilityEffectSlice(103, 1);
        public static readonly AbilityEffectSlice Deterrence = new AbilityEffectSlice(104, 1);
        public static readonly AbilityEffectSlice RoarOfSacrifice = new AbilityEffectSlice(105, 1);
        public static readonly AbilityEffectSlice RapidFire = new AbilityEffectSlice(106, 1);
        public static readonly AbilityEffectSlice TranquilizingShot = new AbilityEffectSlice(107, 2);
        public static readonly AbilityEffectSlice ConcussiveShot = new AbilityEffectSlice(109, 2);
        public static readonly AbilityEffectSlice Flare = new AbilityEffectSlice(111, 2);
        public static readonly AbilityEffectSlice AspectOfTheDragonhawk = new AbilityEffectSlice(113, 1);
        public static readonly AbilityEffectSlice AspectOfTheViper = new AbilityEffectSlice(114, 1);
        public static readonly AbilityEffectSlice AspectOfTheViperPeriodic = new AbilityEffectSlice(115, 1);
        public static readonly AbilityEffectSlice PrayerOfMending = new AbilityEffectSlice(116, 1);
        public static readonly AbilityEffectSlice InnerFocus = new AbilityEffectSlice(117, 1);
        public static readonly AbilityEffectSlice FearWard = new AbilityEffectSlice(118, 1);
        public static readonly AbilityEffectSlice Scorch = new AbilityEffectSlice(119, 1);
        public static readonly AbilityEffectSlice Pyroblast = new AbilityEffectSlice(120, 1);
        public static readonly AbilityEffectSlice ConeOfCold = new AbilityEffectSlice(121, 2);
        public static readonly AbilityEffectSlice Spellsteal = new AbilityEffectSlice(123, 1);
        public static readonly AbilityEffectSlice IceBlock = new AbilityEffectSlice(124, 2);
        public static readonly AbilityEffectSlice ManaShield = new AbilityEffectSlice(126, 1);
        public static readonly AbilityEffectSlice MoltenArmor = new AbilityEffectSlice(127, 1);
        public static readonly AbilityEffectSlice MageArmor = new AbilityEffectSlice(128, 1);
        public static readonly AbilityEffectSlice IceArmor = new AbilityEffectSlice(129, 1);
        public static readonly AbilityEffectSlice WaterElementalFreeze = new AbilityEffectSlice(130, 1);
        public static readonly AbilityEffectSlice ColdSnap = new AbilityEffectSlice(131, 1);
        public static readonly AbilityEffectSlice Sap = new AbilityEffectSlice(132, 1);
        public static readonly AbilityEffectSlice Garrote = new AbilityEffectSlice(133, 3);
        public static readonly AbilityEffectSlice GarrotePeriodic = new AbilityEffectSlice(136, 1);
        public static readonly AbilityEffectSlice Gouge = new AbilityEffectSlice(137, 2);
        public static readonly AbilityEffectSlice Sprint = new AbilityEffectSlice(139, 1);
        public static readonly AbilityEffectSlice Dismantle = new AbilityEffectSlice(140, 1);
        public static readonly AbilityEffectSlice Preparation = new AbilityEffectSlice(141, 1);
        public static readonly AbilityEffectSlice Stealth = new AbilityEffectSlice(142, 1);
        public static readonly AbilityEffectSlice Shadowform = new AbilityEffectSlice(143, 1);
        public static readonly AbilityEffectSlice Smite = new AbilityEffectSlice(144, 1);
        public static readonly AbilityEffectSlice BladeTempest = new AbilityEffectSlice(145, 1);
        public static readonly AbilityEffectSlice DawnNova = new AbilityEffectSlice(146, 2);
        public static readonly AbilityEffectSlice UmbralCollapse = new AbilityEffectSlice(148, 1);
        public static readonly AbilityEffectSlice FlameSurge = new AbilityEffectSlice(149, 1);
        public static readonly AbilityEffectSlice Blizzard = new AbilityEffectSlice(150, 2);
        public static readonly AbilityEffectSlice ArrowRain = new AbilityEffectSlice(152, 1);

        private static readonly AbilityEffectDefinition[] Definitions =
        {
            AbilityEffectDefinition.Heal(14f, AbilityEffectMoment.ChannelTick, condition: AbilityEffectCondition.TargetIsFriendly),
            AbilityEffectDefinition.Damage(10f, SpellSchoolMask.Holy, AbilityEffectMoment.ChannelTick, condition: AbilityEffectCondition.TargetIsHostile),
            AbilityEffectDefinition.Heal(32f),
            AbilityEffectDefinition.ApplyAura(AuraId.SoulWard, 15f, 30f),
            AbilityEffectDefinition.ApplyAura(AuraId.WeakenedSoul, 15f),
            AbilityEffectDefinition.ApplyAura(AuraId.GuardianCovenant, 8f),
            AbilityEffectDefinition.ApplyAura(AuraId.Fear, 8f, target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Damage(22f, SpellSchoolMask.Shadow),
            AbilityEffectDefinition.Damage(22f, SpellSchoolMask.Shadow, target: AbilityEffectTarget.Caster, condition: AbilityEffectCondition.TargetSurvivedPreviousEffect),
            AbilityEffectDefinition.SummonUnit(SummonId.UmbralFamiliar, 15f),
            AbilityEffectDefinition.Dispel(DispelType.Magic, 2, DispelSelectionMode.RelationAppropriate),
            AbilityEffectDefinition.Damage(24f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.AddComboPoints(2),
            AbilityEffectDefinition.Damage(12f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.AddComboPoints(1),
            AbilityEffectDefinition.ConsumeComboPointsDamage(9f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.ConsumeComboPointsAura(AuraId.Stun, 1f),
            AbilityEffectDefinition.ApplyAura(AuraId.NightfallDance, 6f, target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.Teleport(8f),
            AbilityEffectDefinition.Interrupt(5f),
            AbilityEffectDefinition.Dispel(DispelType.Magic, 32, DispelSelectionMode.HarmfulFromFriendly, AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(AuraId.Spellveil, 5f, target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.Damage(14f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.Damage(6f, SpellSchoolMask.Nature, condition: AbilityEffectCondition.TargetIsPoisoned),
            AbilityEffectDefinition.AddComboPoints(2),
            AbilityEffectDefinition.ConsumeComboPointsDamage(10f, SpellSchoolMask.Nature),
            AbilityEffectDefinition.ApplyAura(AuraId.VenomRush, 6f, target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.Damage(5f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.ApplyAura(AuraId.Poison, 12f),
            AbilityEffectDefinition.AddComboPoints(1),
            AbilityEffectDefinition.ApplyAura(AuraId.Blind, 8f),
            AbilityEffectDefinition.ApplyAura(AuraId.Stealth, 3f, target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.Damage(18f, SpellSchoolMask.Fire),
            AbilityEffectDefinition.Damage(12f, SpellSchoolMask.Fire),
            AbilityEffectDefinition.ApplyAura(AuraId.LivingEmber, 12f),
            AbilityEffectDefinition.Damage(10f, SpellSchoolMask.Fire, target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.ApplyAura(AuraId.Disoriented, 3f, target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Damage(12f, SpellSchoolMask.Fire, target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Knockback(4f, AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Interrupt(8f),
            AbilityEffectDefinition.Teleport(8f),
            AbilityEffectDefinition.ApplyAura(AuraId.Polymorphed, 8f),
            AbilityEffectDefinition.Damage(15f, SpellSchoolMask.Frost),
            AbilityEffectDefinition.ApplyAura(AuraId.FrostSlow, 4f),
            AbilityEffectDefinition.Damage(8f, SpellSchoolMask.Frost),
            AbilityEffectDefinition.Damage(16f, SpellSchoolMask.Frost, condition: AbilityEffectCondition.TargetIsFrozen),
            AbilityEffectDefinition.Damage(10f, SpellSchoolMask.Frost, target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.ApplyAura(AuraId.FrozenRoot, 5f, target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.ApplyAura(AuraId.Stun, 5f),
            AbilityEffectDefinition.ApplyAura(AuraId.IceWard, 60f, 28f, target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.Damage(5f, SpellSchoolMask.Fire, AbilityEffectMoment.AuraPeriodic),
            AbilityEffectDefinition.Damage(14f, SpellSchoolMask.Fire, AbilityEffectMoment.AuraExpired, target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Heal(4f, AbilityEffectMoment.AuraPeriodic),
            AbilityEffectDefinition.RemoveAurasByTag(
                AuraTag.Fear |
                AuraTag.Horror |
                AuraTag.Stun |
                AuraTag.Disorient |
                AuraTag.Root |
                AuraTag.Slow),
            AbilityEffectDefinition.ApplyAura(
                AuraId.PowerInfusion,
                10f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(
                AuraId.Combustion,
                10f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(
                AuraId.IcyVeins,
                10f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(
                AuraId.Evasion,
                6f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(
                AuraId.ColdBlood,
                8f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(AuraId.Renew, 15f),
            AbilityEffectDefinition.Heal(6f, AbilityEffectMoment.AuraPeriodic),
            AbilityEffectDefinition.ApplyAura(AuraId.Stun, 4f),
            AbilityEffectDefinition.AddComboPoints(2),

            AbilityEffectDefinition.ApplyAura(AuraId.VampiricTouch, 15f),
            AbilityEffectDefinition.Damage(
                5f,
                SpellSchoolMask.Shadow,
                AbilityEffectMoment.AuraPeriodic),
            AbilityEffectDefinition.Damage(
                18f,
                SpellSchoolMask.Shadow,
                AbilityEffectMoment.AuraDispelled,
                AbilityEffectTarget.SelectedTarget),
            AbilityEffectDefinition.ApplyAura(AuraId.ShadowWordPain, 18f),
            AbilityEffectDefinition.Damage(
                4f,
                SpellSchoolMask.Shadow,
                AbilityEffectMoment.AuraPeriodic),
            AbilityEffectDefinition.Damage(8f, SpellSchoolMask.Shadow),
            AbilityEffectDefinition.ApplyAura(AuraId.DevouringPlague, 24f),
            AbilityEffectDefinition.LeechDamage(
                4f,
                0.15f,
                SpellSchoolMask.Shadow,
                AbilityEffectMoment.AuraPeriodic),
            AbilityEffectDefinition.Damage(
                5f,
                SpellSchoolMask.Shadow,
                AbilityEffectMoment.ChannelTick),
            AbilityEffectDefinition.ApplyAura(
                AuraId.MindFlaySlow,
                1.1f,
                moment: AbilityEffectMoment.ChannelTick),
            AbilityEffectDefinition.RefreshAuraDuration(
                AuraId.ShadowWordPain,
                18f,
                AbilityEffectMoment.ChannelTick),
            AbilityEffectDefinition.Damage(18f, SpellSchoolMask.Shadow),
            AbilityEffectDefinition.ApplyAura(AuraId.MortalWound, 10f, 0.8f),
            AbilityEffectDefinition.ApplyAura(
                AuraId.Replenishment,
                15f,
                target: AbilityEffectTarget.TeamFriendlies,
                condition: AbilityEffectCondition.TargetHasAura,
                conditionAura: AuraId.VampiricTouch),
            AbilityEffectDefinition.ApplyAura(AuraId.Silence, 5f),
            AbilityEffectDefinition.RemoveAurasByTag(AuraTag.Root | AuraTag.Slow),
            AbilityEffectDefinition.ApplyAura(
                AuraId.Dispersion,
                6f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ModifyResourcePercentMaximum(
                0.06f,
                ResourceType.Mana,
                AbilityEffectMoment.AuraPeriodic,
                AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(AuraId.Horror, 3f),
            AbilityEffectDefinition.ApplyAura(AuraId.Disarm, 10f),
            AbilityEffectDefinition.BurnResource(
                0.1f,
                0.2f,
                0.5f,
                ResourceType.Mana),
            AbilityEffectDefinition.DispelArea(
                DispelType.Magic,
                10,
                DispelSelectionMode.AreaHarmfulFromFriendlyIncludingProtected,
                AbilityEffectTarget.AreaFriendlies),
            AbilityEffectDefinition.DispelArea(
                DispelType.Magic,
                10,
                DispelSelectionMode.AreaBeneficialFromHostileIncludingProtected,
                AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.RemoveAurasByTag(AuraTag.Root | AuraTag.Slow),
            AbilityEffectDefinition.ModifyResourcePercentMaximum(
                0.01f,
                ResourceType.Mana,
                AbilityEffectMoment.AuraPeriodic,
                AbilityEffectTarget.Caster),

            AbilityEffectDefinition.Damage(18f, SpellSchoolMask.Nature),
            AbilityEffectDefinition.ApplyAura(AuraId.SerpentSting, 15f),
            AbilityEffectDefinition.Damage(
                5f,
                SpellSchoolMask.Nature,
                AbilityEffectMoment.AuraPeriodic),
            AbilityEffectDefinition.ApplyAura(AuraId.ViperSting, 8f),
            AbilityEffectDefinition.BurnResource(
                0.04f,
                0.04f,
                0f,
                ResourceType.Mana,
                AbilityEffectMoment.AuraPeriodic),
            AbilityEffectDefinition.ApplyAura(AuraId.ScorpidSting, 20f),
            AbilityEffectDefinition.Damage(16f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.ApplyAura(AuraId.MortalWound, 10f, 0.5f),
            AbilityEffectDefinition.Damage(12f, SpellSchoolMask.Arcane),
            AbilityEffectDefinition.Damage(28f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.Damage(14f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.ApplyAura(AuraId.HuntersMark, 120f),
            AbilityEffectDefinition.Damage(4f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.ApplyAura(AuraId.ScatterDisorient, 4f),
            AbilityEffectDefinition.ApplyAura(AuraId.Silence, 3f),
            AbilityEffectDefinition.ApplyAura(AuraId.WingClip, 6f),
            AbilityEffectDefinition.ApplyAura(
                AuraId.Deterrence,
                5f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(AuraId.RoarOfSacrifice, 12f),
            AbilityEffectDefinition.ApplyAura(
                AuraId.RapidFire,
                15f,
                target: AbilityEffectTarget.Caster),

            AbilityEffectDefinition.Dispel(
                DispelType.Magic,
                1,
                DispelSelectionMode.BeneficialFromHostile),
            AbilityEffectDefinition.Dispel(
                DispelType.Enrage,
                1,
                DispelSelectionMode.BeneficialFromHostile),
            AbilityEffectDefinition.Damage(5f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.ApplyAura(AuraId.ConcussiveSlow, 4f),
            AbilityEffectDefinition.RemoveAnyAurasByTag(
                AuraTag.Stealth,
                AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.ApplyAura(
                AuraId.HuntersMark,
                10f,
                target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.ApplyAura(
                AuraId.AspectOfTheDragonhawk,
                600f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(
                AuraId.AspectOfTheViper,
                600f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ModifyResourcePercentMaximum(
                0.04f,
                ResourceType.Mana,
                AbilityEffectMoment.AuraPeriodic,
                AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(AuraId.PrayerOfMending, 30f, 16f),
            AbilityEffectDefinition.ApplyAura(
                AuraId.InnerFocus,
                15f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(AuraId.FearWard, 180f),
            AbilityEffectDefinition.Damage(10f, SpellSchoolMask.Fire),
            AbilityEffectDefinition.Damage(28f, SpellSchoolMask.Fire),
            AbilityEffectDefinition.Damage(
                10f,
                SpellSchoolMask.Frost,
                target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.ApplyAura(
                AuraId.FrostSlow,
                4f,
                target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Spellsteal(DispelType.Magic),
            AbilityEffectDefinition.RemoveAurasByTag(
                AuraTag.Fear | AuraTag.Horror | AuraTag.Stun |
                AuraTag.Disorient | AuraTag.Root | AuraTag.Slow),
            AbilityEffectDefinition.ApplyAura(
                AuraId.IceBlock,
                10f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(
                AuraId.ManaShield,
                60f,
                24f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(
                AuraId.MoltenArmor,
                600f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(
                AuraId.MageArmor,
                600f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(
                AuraId.IceArmor,
                600f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(
                AuraId.FrozenRoot,
                5f,
                target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.ResetCooldowns(CooldownResetGroup.MageFrost),
            AbilityEffectDefinition.ApplyAura(AuraId.Sap, 8f),
            AbilityEffectDefinition.Damage(8f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.ApplyAura(AuraId.GarroteBleed, 18f),
            AbilityEffectDefinition.AddComboPoints(1),
            AbilityEffectDefinition.Damage(
                4f,
                SpellSchoolMask.Physical,
                AbilityEffectMoment.AuraPeriodic),
            AbilityEffectDefinition.Damage(4f, SpellSchoolMask.Physical),
            AbilityEffectDefinition.ApplyAura(AuraId.Gouge, 4f),
            AbilityEffectDefinition.ApplyAura(
                AuraId.Sprint,
                8f,
                target: AbilityEffectTarget.Caster),
            AbilityEffectDefinition.ApplyAura(AuraId.Disarm, 10f),
            AbilityEffectDefinition.ResetCooldowns(
                CooldownResetGroup.RoguePreparation),
            AbilityEffectDefinition.ToggleAura(AuraId.Stealth),
            AbilityEffectDefinition.ToggleAura(AuraId.Shadowform),
            AbilityEffectDefinition.Damage(16f, SpellSchoolMask.Holy),
            AbilityEffectDefinition.Damage(
                10f,
                SpellSchoolMask.Physical,
                target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Damage(
                16f,
                SpellSchoolMask.Holy,
                target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Heal(
                8f,
                target: AbilityEffectTarget.AreaFriendlies),
            AbilityEffectDefinition.Damage(
                18f,
                SpellSchoolMask.Shadow,
                target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Damage(
                18f,
                SpellSchoolMask.Fire,
                target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Damage(
                15f,
                SpellSchoolMask.Frost,
                target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.ApplyAura(
                AuraId.FrostSlow,
                2f,
                target: AbilityEffectTarget.AreaHostiles),
            AbilityEffectDefinition.Damage(
                18f,
                SpellSchoolMask.Physical,
                target: AbilityEffectTarget.AreaHostiles),
        };

        public static int Count => Definitions.Length;

        public static AbilityEffectDefinition Get(int index)
        {
            if (index < 0 || index >= Definitions.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return Definitions[index];
        }
    }
}
