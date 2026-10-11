using System;

namespace SessionRpg.Core
{
    public enum ClassId
    {
        None = 0,
        Priest = 1,
        // Compatibility alias for scenes, tests, and callers authored before
        // the playable healer family was named explicitly.
        Healer = Priest,
        Rogue = 2,
        Mage = 3,
        Hunter = 4,
        Boss = 100,
    }

    public enum SpecializationId
    {
        None = 0,
        Discipline = 1,
        Fire = 2,
        Frost = 3,
        ShadowDance = 4,
        Mutilate = 5,
        Shadow = 6,
        Marksmanship = 7,
        Boss = 100,
    }

    public enum AbilityId
    {
        None = 0,

        HealerBolt = 1,
        Mend = 2,
        Aegis = 3,
        Purify = 4,
        Rescue = 5,

        QuickStrike = 6,
        Ambush = 7,
        Kick = 8,
        Shadowstep = 9,
        Anchor = 10,

        ArcaneBolt = 11,
        Gust = 12,
        Counterspell = 13,
        Blink = 14,
        FrostNova = 15,

        BossCleave = 16,
        BossMeteor = 17,
        BossKnockback = 18,

        Penance = 19,
        FlashHeal = 20,
        PowerWordShield = 21,
        PainSuppression = 22,
        PsychicScream = 23,
        ShadowWordDeath = 24,
        Shadowfiend = 25,
        DispelMagic = 26,

        Hemorrhage = 27,
        Eviscerate = 28,
        KidneyShot = 29,
        ShadowDance = 30,
        CloakOfShadows = 31,
        Mutilate = 32,
        Envenom = 33,
        Shiv = 34,
        Blind = 35,
        Vanish = 36,

        Fireball = 37,
        FireBlast = 38,
        LivingBomb = 39,
        DragonBreath = 40,
        BlastWave = 41,
        Frostbolt = 42,
        IceLance = 43,
        DeepFreeze = 44,
        IceBarrier = 45,
        Polymorph = 46,

        ArenaTrinket = 47,
        PowerInfusion = 48,
        Combustion = 49,
        IcyVeins = 50,
        Evasion = 51,
        ColdBlood = 52,

        Renew = 53,
        CheapShot = 54,

        VampiricTouch = 55,
        ShadowWordPain = 56,
        DevouringPlague = 57,
        MindFlay = 58,
        MindBlast = 59,
        Silence = 60,
        Dispersion = 61,
        PsychicHorror = 62,
        ManaBurn = 63,
        MassDispel = 64,
        Fade = 65,

        ChimeraShot = 66,
        SerpentSting = 67,
        ViperSting = 68,
        ScorpidSting = 69,
        AimedShot = 70,
        ArcaneShot = 71,
        KillShot = 72,
        SteadyShot = 73,
        HuntersMark = 74,
        ScatterShot = 75,
        FreezingArrow = 76,
        FrostTrap = 77,
        SilencingShot = 78,
        FeignDeath = 79,
        Disengage = 80,
        WingClip = 81,
        MastersCall = 82,
        Deterrence = 83,
        RoarOfSacrifice = 84,
        Readiness = 85,
        RapidFire = 86,
        PetAttack = 87,
        PetFollow = 88,
        PetStay = 89,
        SpiderWeb = 90,
        MendPet = 91,

        TranquilizingShot = 92,
        ConcussiveShot = 93,
        Flare = 94,
        AspectOfTheDragonhawk = 95,
        AspectOfTheViper = 96,

        PrayerOfMending = 97,
        InnerFocus = 98,
        FearWard = 99,

        Scorch = 100,
        Pyroblast = 101,
        ConeOfCold = 102,
        Spellsteal = 103,
        IceBlock = 104,
        ManaShield = 105,
        MoltenArmor = 106,
        MageArmor = 107,
        IceArmor = 108,
        WaterElementalFreeze = 109,
        ColdSnap = 110,

        Sap = 111,
        Garrote = 112,
        Gouge = 113,
        Sprint = 114,
        Dismantle = 115,
        Preparation = 116,

        Stealth = 117,
        Shadowform = 118,
        Smite = 119,

        // Siege-only player action. Ability IDs are dense storage keys, so new
        // entries remain append-only and existing serialized values stay stable.
        BladeTempest = 120,
        DawnNova = 121,
        UmbralCollapse = 122,
        FlameSurge = 123,
        Blizzard = 124,
        ArrowRain = 125,
    }

    [Flags]
    public enum AbilityTargetRelation
    {
        None = 0,
        Self = 1 << 0,
        Friendly = 1 << 1,
        Hostile = 1 << 2,
    }

    public enum AbilityTargetShape
    {
        Unit,
        Point,
        Direction,
        CasterArea,
        TargetArea,
    }

    public enum AbilitySmartTargetPolicy
    {
        None,
        Self,
        SelectedFriendly,
        LowestHealthFriendly,
        SelectedHostile,
        ContextualFriendlyOrHostile,
        AimPoint,
    }

    public readonly struct AbilityTargetRule
    {
        public AbilityTargetRule(
            AbilityTargetShape shape,
            AbilityTargetRelation allowedRelations,
            AbilitySmartTargetPolicy smartTargetPolicy,
            float range,
            float radius = 0f,
            bool requiresLineOfSight = true,
            bool requiresFacing = false,
            float minimumRange = 0f)
        {
            Shape = shape;
            AllowedRelations = allowedRelations;
            SmartTargetPolicy = smartTargetPolicy;
            Range = Math.Max(0f, range);
            MinimumRange = Math.Max(0f, Math.Min(minimumRange, Range));
            Radius = Math.Max(0f, radius);
            RequiresLineOfSight = requiresLineOfSight;
            RequiresFacing = requiresFacing;
        }

        public AbilityTargetShape Shape { get; }

        public AbilityTargetRelation AllowedRelations { get; }

        public AbilitySmartTargetPolicy SmartTargetPolicy { get; }

        public float Range { get; }

        public float MinimumRange { get; }

        public float Radius { get; }

        public bool RequiresLineOfSight { get; }

        public bool RequiresFacing { get; }

        public bool Allows(AbilityTargetRelation relation) =>
            (AllowedRelations & relation) != 0;
    }

    public enum AbilityActivationType
    {
        Instant,
        Cast,
        Channel,
    }

    public readonly struct AbilityCastProfile
    {
        public AbilityCastProfile(
            AbilityActivationType activationType,
            float durationSeconds,
            float tickIntervalSeconds = 0f,
            int tickCount = 0,
            bool tickImmediately = false,
            bool breaksOnMovement = true)
        {
            ActivationType = activationType;
            DurationSeconds = Math.Max(0f, durationSeconds);
            TickIntervalSeconds = Math.Max(0f, tickIntervalSeconds);
            TickCount = Math.Max(0, tickCount);
            TickImmediately = tickImmediately;
            BreaksOnMovement = breaksOnMovement;
        }

        public AbilityActivationType ActivationType { get; }

        public float DurationSeconds { get; }

        public float TickIntervalSeconds { get; }

        public int TickCount { get; }

        public bool TickImmediately { get; }

        public bool BreaksOnMovement { get; }

        public static AbilityCastProfile Instant => new AbilityCastProfile(
            AbilityActivationType.Instant,
            0f,
            breaksOnMovement: false);

        public static AbilityCastProfile Cast(float seconds) => new AbilityCastProfile(
            AbilityActivationType.Cast,
            seconds);

        public static AbilityCastProfile Channel(
            float seconds,
            float intervalSeconds,
            int tickCount,
            bool tickImmediately = false) => new AbilityCastProfile(
                AbilityActivationType.Channel,
                seconds,
                intervalSeconds,
                tickCount,
                tickImmediately);
    }

    [Flags]
    public enum SpellSchoolMask
    {
        None = 0,
        Physical = 1 << 0,
        Holy = 1 << 1,
        Shadow = 1 << 2,
        Arcane = 1 << 3,
        Fire = 1 << 4,
        Frost = 1 << 5,
        Nature = 1 << 6,
        AllMagic = Holy | Shadow | Arcane | Fire | Frost | Nature,
    }

    public enum ResourceType
    {
        None,
        Mana,
        Energy,
    }

    public readonly struct ResourceCost
    {
        public ResourceCost(ResourceType type, float amount)
        {
            Type = type;
            Amount = Math.Max(0f, amount);
        }

        public ResourceType Type { get; }

        public float Amount { get; }

        public static ResourceCost None => default;
    }

    public readonly struct ResourceProfile
    {
        public ResourceProfile(
            ResourceType type,
            float maximum,
            float initial,
            float regenerationPerSecond)
        {
            Type = type;
            Maximum = Math.Max(0f, maximum);
            Initial = Math.Max(0f, Math.Min(initial, Maximum));
            RegenerationPerSecond = Math.Max(0f, regenerationPerSecond);
        }

        public ResourceType Type { get; }

        public float Maximum { get; }

        public float Initial { get; }

        public float RegenerationPerSecond { get; }

        public static ResourceProfile None => default;
    }

    [Flags]
    public enum AbilityRequirementFlags
    {
        None = 0,
        RequiresActiveEnemyCast = 1 << 0,
        RequiresStealthAccess = 1 << 1,
        RequiresBehindTarget = 1 << 2,
        RequiresComboPoints = 1 << 3,
        RequiresPoisonedTarget = 1 << 4,
        RequiresFrozenTarget = 1 << 5,
        TargetMustNotHaveWeakenedSoul = 1 << 6,
        BreaksStealth = 1 << 7,
        IgnoresGlobalCooldown = 1 << 8,
        UsableWhileStunned = 1 << 9,
        UsableWhileHardControlled = 1 << 10,
        UsableWhileSilenced = 1 << 11,
        TargetBelowTwentyPercentHealth = 1 << 12,
        RequiresLivingCompanion = 1 << 13,
        RequiresManaTarget = 1 << 14,
        RequiresNoLivingHostile = 1 << 15,
        RequiresNoShadowform = 1 << 16,
    }

    public enum AbilityTargetMode
    {
        Self,
        Ally,
        Enemy,
        Point,
        Direction,
        EnemyArea,
    }

    public enum StatusId
    {
        None,
        Barrier,
        Silenced,
        Anchored,
        Slowed,
        Feared,
        Stunned,
        Disoriented,
        Rooted,
        DamageReduced,
        Stealthed,
        Poisoned,
        Burning,
        SchoolLocked,
        Horrified,
        Disarmed,
        Pacified,
    }

    [Flags]
    public enum StatusFlags
    {
        None = 0,
        Barrier = 1 << 0,
        Silenced = 1 << 1,
        Anchored = 1 << 2,
        Slowed = 1 << 3,
        Feared = 1 << 4,
        Stunned = 1 << 5,
        Disoriented = 1 << 6,
        Rooted = 1 << 7,
        DamageReduced = 1 << 8,
        Stealthed = 1 << 9,
        Poisoned = 1 << 10,
        Burning = 1 << 11,
        SchoolLocked = 1 << 12,
        Horrified = 1 << 13,
        Disarmed = 1 << 14,
        Pacified = 1 << 15,
    }

    public enum CompanionOrder
    {
        Follow,
        Stay,
        Attack,
    }

    public readonly struct CompanionSnapshot
    {
        public CompanionSnapshot(
            ParticipantId owner,
            TeamId team,
            Float3 position,
            Float3 facing,
            float health,
            float maxHealth,
            bool isAlive,
            CompanionOrder order,
            ParticipantId target)
        {
            Owner = owner;
            Team = team;
            Position = position;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
            IsAlive = isAlive;
            Order = order;
            Target = target;
        }

        public ParticipantId Owner { get; }

        public TeamId Team { get; }

        public Float3 Position { get; }

        public Float3 Facing { get; }

        public float Health { get; }

        public float MaxHealth { get; }

        public bool IsAlive { get; }

        public CompanionOrder Order { get; }

        public ParticipantId Target { get; }
    }

    public enum GroundEffectId
    {
        None,
        FreezingTrap,
        FrostTrap,
    }

    public readonly struct GroundEffectSnapshot
    {
        public GroundEffectSnapshot(
            GroundEffectId id,
            ParticipantId owner,
            TeamId team,
            Float3 position,
            float radius,
            int endTick)
        {
            Id = id;
            Owner = owner;
            Team = team;
            Position = position;
            Radius = radius;
            EndTick = endTick;
        }

        public GroundEffectId Id { get; }

        public ParticipantId Owner { get; }

        public TeamId Team { get; }

        public Float3 Position { get; }

        public float Radius { get; }

        public int EndTick { get; }
    }

    public enum CombatEventKind
    {
        CastAccepted,
        CastRejected,
        CastCompleted,
        Damage,
        Heal,
        BarrierApplied,
        StatusApplied,
        StatusRemoved,
        Interrupted,
        Knockback,
        HazardDamage,
        Death,
        AuraApplied,
        AuraRemoved,
    }

    public readonly struct ParticipantId : IEquatable<ParticipantId>, IComparable<ParticipantId>
    {
        public ParticipantId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool IsValid => Value > 0;

        public int CompareTo(ParticipantId other) => Value.CompareTo(other.Value);

        public bool Equals(ParticipantId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ParticipantId other && Equals(other);

        public override int GetHashCode() => Value;

        public override string ToString() => Value.ToString();

        public static bool operator ==(ParticipantId left, ParticipantId right) => left.Equals(right);

        public static bool operator !=(ParticipantId left, ParticipantId right) => !left.Equals(right);
    }

    public readonly struct TeamId : IEquatable<TeamId>
    {
        public TeamId(int value)
        {
            Value = value;
        }

        public int Value { get; }

        public bool Equals(TeamId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is TeamId other && Equals(other);

        public override int GetHashCode() => Value;

        public static bool operator ==(TeamId left, TeamId right) => left.Equals(right);

        public static bool operator !=(TeamId left, TeamId right) => !left.Equals(right);
    }

    public readonly struct Float3 : IEquatable<Float3>
    {
        public Float3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }

        public float Y { get; }

        public float Z { get; }

        public float PlanarLengthSquared => (X * X) + (Z * Z);

        public float PlanarLength => (float)Math.Sqrt(PlanarLengthSquared);

        public Float3 PlanarNormalized
        {
            get
            {
                float length = PlanarLength;
                return length > 0.0001f
                    ? new Float3(X / length, 0f, Z / length)
                    : new Float3(0f, 0f, 1f);
            }
        }

        public bool Equals(Float3 other) =>
            X.Equals(other.X) && Y.Equals(other.Y) && Z.Equals(other.Z);

        public override bool Equals(object obj) => obj is Float3 other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(X, Y, Z);

        public static Float3 operator +(Float3 left, Float3 right) =>
            new Float3(left.X + right.X, left.Y + right.Y, left.Z + right.Z);

        public static Float3 operator -(Float3 left, Float3 right) =>
            new Float3(left.X - right.X, left.Y - right.Y, left.Z - right.Z);

        public static Float3 operator *(Float3 value, float scale) =>
            new Float3(value.X * scale, value.Y * scale, value.Z * scale);

        public static float PlanarDistance(Float3 left, Float3 right) => (left - right).PlanarLength;
    }

    public readonly struct CombatCommand
    {
        public CombatCommand(
            ParticipantId actor,
            uint sequence,
            Float3 movement,
            Float3 facing,
            AbilityId ability,
            ParticipantId target,
            Float3 targetPoint,
            Float3 aimDirection)
        {
            Actor = actor;
            Sequence = sequence;
            Movement = movement;
            Facing = facing;
            Ability = ability;
            Target = target;
            TargetPoint = targetPoint;
            AimDirection = aimDirection;
        }

        public ParticipantId Actor { get; }

        public uint Sequence { get; }

        public Float3 Movement { get; }

        public Float3 Facing { get; }

        public AbilityId Ability { get; }

        public ParticipantId Target { get; }

        public Float3 TargetPoint { get; }

        public Float3 AimDirection { get; }

        public bool HasAbility => Ability != AbilityId.None;

        public static CombatCommand Move(
            ParticipantId actor,
            uint sequence,
            Float3 movement,
            Float3 facing)
        {
            return new CombatCommand(
                actor,
                sequence,
                movement,
                facing,
                AbilityId.None,
                default,
                default,
                facing);
        }

        public static CombatCommand Cast(
            ParticipantId actor,
            uint sequence,
            Float3 movement,
            Float3 facing,
            AbilityId ability,
            ParticipantId target,
            Float3 targetPoint,
            Float3 aimDirection)
        {
            return new CombatCommand(
                actor,
                sequence,
                movement,
                facing,
                ability,
                target,
                targetPoint,
                aimDirection);
        }
    }

    public readonly struct CombatEvent
    {
        private CombatEvent(
            int tick,
            CombatEventKind kind,
            ParticipantId actor,
            ParticipantId target,
            AbilityId ability,
            StatusId status,
            float amount,
            Float3 vector,
            string reason,
            AuraId aura)
        {
            Tick = tick;
            Kind = kind;
            Actor = actor;
            Target = target;
            Ability = ability;
            Status = status;
            Amount = amount;
            Vector = vector;
            Reason = reason;
            Aura = aura;
        }

        public int Tick { get; }

        public CombatEventKind Kind { get; }

        public ParticipantId Actor { get; }

        public ParticipantId Target { get; }

        public AbilityId Ability { get; }

        public StatusId Status { get; }

        public float Amount { get; }

        public Float3 Vector { get; }

        public string Reason { get; }

        public AuraId Aura { get; }

        public static CombatEvent Create(
            int tick,
            CombatEventKind kind,
            ParticipantId actor,
            ParticipantId target = default,
            AbilityId ability = AbilityId.None,
            StatusId status = StatusId.None,
            float amount = 0f,
            Float3 vector = default,
            string reason = null,
            AuraId aura = AuraId.None)
        {
            return new CombatEvent(
                tick,
                kind,
                actor,
                target,
                ability,
                status,
                amount,
                vector,
                reason,
                aura);
        }
    }

    public readonly struct CombatantSnapshot
    {
        public CombatantSnapshot(
            ParticipantId id,
            TeamId team,
            ClassId classId,
            Float3 position,
            Float3 facing,
            float health,
            float maxHealth,
            float barrier,
            AbilityId castingAbility,
            int castEndTick,
            bool alive,
            bool inLava,
            StatusFlags activeStatuses = StatusFlags.None)
        {
            Id = id;
            Team = team;
            Class = classId;
            Position = position;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
            Barrier = barrier;
            CastingAbility = castingAbility;
            CastEndTick = castEndTick;
            IsAlive = alive;
            IsInLava = inLava;
            ActiveStatuses = activeStatuses;
            _level = LevelProgressionCurve.MinimumLevel;
            Specialization = SpecializationCatalog.GetDefault(classId);
            ResourceProfile resourceProfile =
                SpecializationCatalog.GetResourceProfile(Specialization);
            ResourceType = resourceProfile.Type;
            Resource = resourceProfile.Initial;
            MaxResource = resourceProfile.Maximum;
            ComboPoints = 0;
            ComboTarget = default;
            CastStartTick = 0;
            CastingActivationType = castingAbility == AbilityId.None
                ? AbilityActivationType.Instant
                : AbilityActivationType.Cast;
            CastingSchool = AbilityCatalog.TryGet(
                castingAbility,
                out AbilityDefinition castingDefinition)
                    ? castingDefinition.School
                    : SpellSchoolMask.None;
            GlobalCooldownEndTick = 0;
            LockedSchools = SpellSchoolMask.None;
            ActiveAuraTags = AuraTag.None;
            DispellableBeneficialAuraTypes = DispelType.None;
        }

        public CombatantSnapshot(
            ParticipantId id,
            TeamId team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            Float3 facing,
            float health,
            float maxHealth,
            float barrier,
            AbilityId castingAbility,
            int castStartTick,
            int castEndTick,
            AbilityActivationType castingActivationType,
            SpellSchoolMask castingSchool,
            ResourceType resourceType,
            float resource,
            float maxResource,
            int comboPoints,
            ParticipantId comboTarget,
            int globalCooldownEndTick,
            SpellSchoolMask lockedSchools,
            bool alive,
            bool inLava,
            StatusFlags activeStatuses = StatusFlags.None,
            AuraTag activeAuraTags = AuraTag.None,
            DispelType dispellableBeneficialAuraTypes = DispelType.None,
            int level = LevelProgressionCurve.MinimumLevel,
            PlayableModeId playableMode = PlayableModeId.None)
        {
            Id = id;
            Team = team;
            Class = classId;
            Specialization = specialization;
            Position = position;
            Facing = facing;
            Health = health;
            MaxHealth = maxHealth;
            Barrier = barrier;
            CastingAbility = castingAbility;
            CastStartTick = castStartTick;
            CastEndTick = castEndTick;
            CastingActivationType = castingActivationType;
            CastingSchool = castingSchool;
            ResourceType = resourceType;
            Resource = resource;
            MaxResource = maxResource;
            ComboPoints = comboPoints;
            ComboTarget = comboTarget;
            GlobalCooldownEndTick = globalCooldownEndTick;
            LockedSchools = lockedSchools;
            IsAlive = alive;
            IsInLava = inLava;
            ActiveStatuses = activeStatuses;
            ActiveAuraTags = activeAuraTags;
            DispellableBeneficialAuraTypes = dispellableBeneficialAuraTypes;
            _level = playableMode == PlayableModeId.PveLeveling
                ? Math.Max(PersistentLevelProgression.MinimumLevel,
                    Math.Min(PersistentLevelProgression.MaximumLevel, level))
                : LevelProgressionCurve.ClampLevel(level);
        }

        public ParticipantId Id { get; }

        public TeamId Team { get; }

        public ClassId Class { get; }

        public SpecializationId Specialization { get; }

        public int Level => _level < LevelProgressionCurve.MinimumLevel
            ? LevelProgressionCurve.MinimumLevel
            : _level;

        private readonly int _level;

        public Float3 Position { get; }

        public Float3 Facing { get; }

        public float Health { get; }

        public float MaxHealth { get; }

        public float Barrier { get; }

        public AbilityId CastingAbility { get; }

        public int CastEndTick { get; }

        public int CastStartTick { get; }

        public AbilityActivationType CastingActivationType { get; }

        public SpellSchoolMask CastingSchool { get; }

        public ResourceType ResourceType { get; }

        public float Resource { get; }

        public float MaxResource { get; }

        public int ComboPoints { get; }

        public ParticipantId ComboTarget { get; }

        public int GlobalCooldownEndTick { get; }

        public SpellSchoolMask LockedSchools { get; }

        public bool IsAlive { get; }

        public bool IsInLava { get; }

        public StatusFlags ActiveStatuses { get; }

        public AuraTag ActiveAuraTags { get; }

        public DispelType DispellableBeneficialAuraTypes { get; }

        public bool HasStatus(StatusId status)
        {
            StatusFlags flag;
            switch (status)
            {
                case StatusId.Barrier:
                    flag = StatusFlags.Barrier;
                    break;
                case StatusId.Silenced:
                    flag = StatusFlags.Silenced;
                    break;
                case StatusId.Anchored:
                    flag = StatusFlags.Anchored;
                    break;
                case StatusId.Slowed:
                    flag = StatusFlags.Slowed;
                    break;
                case StatusId.Feared:
                    flag = StatusFlags.Feared;
                    break;
                case StatusId.Stunned:
                    flag = StatusFlags.Stunned;
                    break;
                case StatusId.Disoriented:
                    flag = StatusFlags.Disoriented;
                    break;
                case StatusId.Rooted:
                    flag = StatusFlags.Rooted;
                    break;
                case StatusId.DamageReduced:
                    flag = StatusFlags.DamageReduced;
                    break;
                case StatusId.Stealthed:
                    flag = StatusFlags.Stealthed;
                    break;
                case StatusId.Poisoned:
                    flag = StatusFlags.Poisoned;
                    break;
                case StatusId.Burning:
                    flag = StatusFlags.Burning;
                    break;
                case StatusId.SchoolLocked:
                    flag = StatusFlags.SchoolLocked;
                    break;
                case StatusId.Horrified:
                    flag = StatusFlags.Horrified;
                    break;
                case StatusId.Disarmed:
                    flag = StatusFlags.Disarmed;
                    break;
                case StatusId.Pacified:
                    flag = StatusFlags.Pacified;
                    break;
                default:
                    return false;
            }

            return (ActiveStatuses & flag) != 0;
        }

        public bool HasAuraTag(AuraTag tag) => (ActiveAuraTags & tag) != 0;

        public bool HasDispellableBeneficialAura(DispelType dispelType) =>
            dispelType != DispelType.None &&
            (DispellableBeneficialAuraTypes & dispelType) != 0;
    }
}
