using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    /// <summary>Stable product variants within the Hero Siege mode.</summary>
    public enum SiegeVariantId
    {
        None = 0,
        SoloLane = 1,
        FourLanes = 2,
    }

    /// <summary>
    /// Stable lanes around the shared central objective. Solo Lane uses East so
    /// its route and spawn identities are also valid in Four Lanes.
    /// </summary>
    public enum SiegeLaneId
    {
        None = 0,
        East = 1,
        North = 2,
        West = 3,
        South = 4,
    }

    public enum SiegeEnemyArchetypeId
    {
        None = 0,
        Ashling = 1,
        CinderRunner = 2,
        AshboundRaider = 3,
        CinderMarksman = 4,
        AshenBrute = 5,
        CinderHexer = 6,
        RimeWeaver = 7,
        ReliquaryWarder = 8,
        CausewayPrelate = 9,
    }

    public enum SiegeEnemySignaturePolicy
    {
        None = 0,
        CadenceHostile = 1,
        CloseCasterArea = 2,
        DefensiveAlly = 3,
    }

    public enum SiegeRunStatus
    {
        Preparation = 0,
        WaveActive = 1,
        Completed = 2,
        Failed = 3,
        Abandoned = 4,
    }

    public enum SiegeRunEndReason
    {
        None = 0,
        AllWavesCleared = 1,
        DefendersDefeated = 2,
        ObjectiveDefeated = 3,
        Abandoned = 4,
    }

    public enum SiegePotionId
    {
        None = 0,
        Health = 1,
        Mana = 2,
    }

    public readonly struct SiegeWaveId :
        IEquatable<SiegeWaveId>,
        IComparable<SiegeWaveId>
    {
        public SiegeWaveId(uint value) => Value = value;

        public uint Value { get; }

        public bool IsValid => Value != 0u;

        public int CompareTo(SiegeWaveId other) => Value.CompareTo(other.Value);

        public bool Equals(SiegeWaveId other) => Value == other.Value;

        public override bool Equals(object obj) =>
            obj is SiegeWaveId other && Equals(other);

        public override int GetHashCode() => unchecked((int)Value);

        public override string ToString() => Value.ToString();

        public static bool operator ==(SiegeWaveId left, SiegeWaveId right) =>
            left.Equals(right);

        public static bool operator !=(SiegeWaveId left, SiegeWaveId right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Unity-free combat and presentation metadata for one regular Siege enemy.
    /// Siege intentionally has no boss class, boss tier, or final-boss exception.
    /// </summary>
    public readonly struct SiegeEnemyArchetypeDefinition
    {
        public SiegeEnemyArchetypeDefinition(
            SiegeEnemyArchetypeId id,
            string displayName,
            RunEnemyRank rank,
            RunEnemyTier tier,
            ClassId classId,
            SpecializationId specialization,
            AbilityId primaryAbility,
            AbilityId signatureAbility,
            SiegeEnemySignaturePolicy signaturePolicy,
            float baseMaximumHealth,
            int baseGoldReward,
            float baseObjectiveDamagePerSecond,
            int baseMovementSpeedMultiplierPermille,
            float preferredRange,
            float presentationScale,
            bool rushesObjective)
        {
            if (id < SiegeEnemyArchetypeId.Ashling ||
                id > SiegeEnemyArchetypeId.CausewayPrelate)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if (string.IsNullOrWhiteSpace(displayName))
            {
                throw new ArgumentException(
                    "A Siege enemy requires a readable display name.",
                    nameof(displayName));
            }

            if (rank != RunEnemyRank.Standard && rank != RunEnemyRank.Elite)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(rank), rank, "Siege enemies cannot be bosses.");
            }

            if (tier != RunEnemyTier.SwarmMinion &&
                tier != RunEnemyTier.Medium)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(tier), tier, "Siege enemies cannot use the boss tier.");
            }

            if (!SpecializationCatalog.IsCompatible(classId, specialization))
            {
                throw new ArgumentException(
                    "A Siege enemy class and specialization must be compatible.",
                    nameof(specialization));
            }

            if (primaryAbility == AbilityId.None ||
                !AbilityCatalog.TryGet(primaryAbility, out _))
            {
                throw new ArgumentException(
                    "A Siege enemy requires a catalogued primary ability.",
                    nameof(primaryAbility));
            }

            if (signatureAbility != AbilityId.None &&
                !AbilityCatalog.TryGet(signatureAbility, out _))
            {
                throw new ArgumentException(
                    "A Siege signature ability must be catalogued.",
                    nameof(signatureAbility));
            }

            if ((signatureAbility == AbilityId.None) !=
                (signaturePolicy == SiegeEnemySignaturePolicy.None))
            {
                throw new ArgumentException(
                    "Signature ability and policy must both be set or both be absent.",
                    nameof(signaturePolicy));
            }

            if (!IsFinitePositive(baseMaximumHealth) ||
                baseGoldReward <= 0 ||
                !IsFinitePositive(baseObjectiveDamagePerSecond) ||
                baseMovementSpeedMultiplierPermille < 250 ||
                baseMovementSpeedMultiplierPermille > 1000 ||
                !IsFinitePositive(preferredRange) ||
                !IsFinitePositive(presentationScale))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(baseMaximumHealth),
                    "Siege enemy combat values must be finite and positive.");
            }

            Id = id;
            DisplayName = displayName;
            Rank = rank;
            Tier = tier;
            Class = classId;
            Specialization = specialization;
            PrimaryAbility = primaryAbility;
            SignatureAbility = signatureAbility;
            SignaturePolicy = signaturePolicy;
            BaseMaximumHealth = baseMaximumHealth;
            BaseGoldReward = baseGoldReward;
            BaseObjectiveDamagePerSecond = baseObjectiveDamagePerSecond;
            BaseMovementSpeedMultiplierPermille =
                baseMovementSpeedMultiplierPermille;
            PreferredRange = preferredRange;
            PresentationScale = presentationScale;
            RushesObjective = rushesObjective;
        }

        public SiegeEnemyArchetypeId Id { get; }
        public string DisplayName { get; }
        public RunEnemyRank Rank { get; }
        public RunEnemyTier Tier { get; }
        public ClassId Class { get; }
        public SpecializationId Specialization { get; }
        public AbilityId PrimaryAbility { get; }
        public AbilityId SignatureAbility { get; }
        public SiegeEnemySignaturePolicy SignaturePolicy { get; }
        public float BaseMaximumHealth { get; }
        public int BaseGoldReward { get; }
        public float BaseObjectiveDamagePerSecond { get; }
        public int BaseMovementSpeedMultiplierPermille { get; }
        public float PreferredRange { get; }
        public float PresentationScale { get; }
        public bool RushesObjective { get; }

        public bool IsValid =>
            Id >= SiegeEnemyArchetypeId.Ashling &&
            Id <= SiegeEnemyArchetypeId.CausewayPrelate &&
            !string.IsNullOrWhiteSpace(DisplayName) &&
            (Rank == RunEnemyRank.Standard || Rank == RunEnemyRank.Elite) &&
            (Tier == RunEnemyTier.SwarmMinion || Tier == RunEnemyTier.Medium) &&
            Class != ClassId.Boss &&
            Specialization != SpecializationId.Boss &&
            SpecializationCatalog.IsCompatible(Class, Specialization) &&
            PrimaryAbility != AbilityId.None &&
            IsFinitePositive(BaseMaximumHealth) &&
            BaseGoldReward > 0 &&
            IsFinitePositive(BaseObjectiveDamagePerSecond) &&
            BaseMovementSpeedMultiplierPermille >= 250 &&
            BaseMovementSpeedMultiplierPermille <= 1000 &&
            IsFinitePositive(PreferredRange) &&
            IsFinitePositive(PresentationScale);

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }

    public static class SiegeEnemyArchetypeCatalog
    {
        public const int Count = 9;

        private static readonly SiegeEnemyArchetypeDefinition Ashling = Create(
            SiegeEnemyArchetypeId.Ashling, "Ashling",
            RunEnemyRank.Standard, RunEnemyTier.SwarmMinion,
            ClassId.Rogue, SpecializationId.Mutilate,
            AbilityId.Shiv, AbilityId.None, SiegeEnemySignaturePolicy.None,
            42f, 3, 6f, 560, 1.6f, 0.72f, false);

        private static readonly SiegeEnemyArchetypeDefinition CinderRunner = Create(
            SiegeEnemyArchetypeId.CinderRunner, "Cinder Runner",
            RunEnemyRank.Standard, RunEnemyTier.SwarmMinion,
            ClassId.Rogue, SpecializationId.ShadowDance,
            AbilityId.Hemorrhage, AbilityId.None, SiegeEnemySignaturePolicy.None,
            38f, 4, 9f, 640, 1.5f, 0.78f, true);

        private static readonly SiegeEnemyArchetypeDefinition AshboundRaider = Create(
            SiegeEnemyArchetypeId.AshboundRaider, "Ashbound Raider",
            RunEnemyRank.Standard, RunEnemyTier.Medium,
            ClassId.Rogue, SpecializationId.Mutilate,
            AbilityId.Shiv, AbilityId.Envenom,
            SiegeEnemySignaturePolicy.CadenceHostile,
            100f, 7, 5f, 530, 1.7f, 0.96f, false);

        private static readonly SiegeEnemyArchetypeDefinition CinderMarksman = Create(
            SiegeEnemyArchetypeId.CinderMarksman, "Cinder Marksman",
            RunEnemyRank.Standard, RunEnemyTier.Medium,
            ClassId.Hunter, SpecializationId.Marksmanship,
            AbilityId.SteadyShot, AbilityId.AimedShot,
            SiegeEnemySignaturePolicy.CadenceHostile,
            82f, 7, 4f, 480, 8.5f, 0.94f, false);

        private static readonly SiegeEnemyArchetypeDefinition AshenBrute = Create(
            SiegeEnemyArchetypeId.AshenBrute, "Ashen Brute",
            RunEnemyRank.Elite, RunEnemyTier.Medium,
            ClassId.Rogue, SpecializationId.ShadowDance,
            AbilityId.Hemorrhage, AbilityId.Eviscerate,
            SiegeEnemySignaturePolicy.CadenceHostile,
            165f, 10, 8f, 420, 1.8f, 1.18f, false);

        private static readonly SiegeEnemyArchetypeDefinition CinderHexer = Create(
            SiegeEnemyArchetypeId.CinderHexer, "Cinder Hexer",
            RunEnemyRank.Standard, RunEnemyTier.Medium,
            ClassId.Mage, SpecializationId.Fire,
            AbilityId.Fireball, AbilityId.LivingBomb,
            SiegeEnemySignaturePolicy.CadenceHostile,
            90f, 9, 4f, 470, 8f, 0.98f, false);

        private static readonly SiegeEnemyArchetypeDefinition RimeWeaver = Create(
            SiegeEnemyArchetypeId.RimeWeaver, "Rime Weaver",
            RunEnemyRank.Standard, RunEnemyTier.Medium,
            ClassId.Mage, SpecializationId.Frost,
            AbilityId.Frostbolt, AbilityId.FrostNova,
            SiegeEnemySignaturePolicy.CloseCasterArea,
            105f, 9, 4f, 460, 7.5f, 1f, false);

        private static readonly SiegeEnemyArchetypeDefinition ReliquaryWarder = Create(
            SiegeEnemyArchetypeId.ReliquaryWarder, "Reliquary Warder",
            RunEnemyRank.Elite, RunEnemyTier.Medium,
            ClassId.Priest, SpecializationId.Discipline,
            AbilityId.Smite, AbilityId.PowerWordShield,
            SiegeEnemySignaturePolicy.DefensiveAlly,
            125f, 11, 3f, 440, 8f, 1.06f, false);

        private static readonly SiegeEnemyArchetypeDefinition CausewayPrelate = Create(
            SiegeEnemyArchetypeId.CausewayPrelate, "Causeway Prelate",
            RunEnemyRank.Elite, RunEnemyTier.Medium,
            ClassId.Priest, SpecializationId.Shadow,
            AbilityId.MindFlay, AbilityId.MindBlast,
            SiegeEnemySignaturePolicy.CadenceHostile,
            118f, 12, 5f, 450, 8f, 1.04f, false);

        public static SiegeEnemyArchetypeDefinition Get(SiegeEnemyArchetypeId id)
        {
            if (!TryGet(id, out SiegeEnemyArchetypeDefinition definition))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(id), id, "Unknown Siege enemy archetype ID.");
            }

            return definition;
        }

        public static SiegeEnemyArchetypeDefinition GetByIndex(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return Get((SiegeEnemyArchetypeId)(index + 1));
        }

        public static bool TryGet(
            SiegeEnemyArchetypeId id,
            out SiegeEnemyArchetypeDefinition definition)
        {
            switch (id)
            {
                case SiegeEnemyArchetypeId.Ashling:
                    definition = Ashling;
                    return true;
                case SiegeEnemyArchetypeId.CinderRunner:
                    definition = CinderRunner;
                    return true;
                case SiegeEnemyArchetypeId.AshboundRaider:
                    definition = AshboundRaider;
                    return true;
                case SiegeEnemyArchetypeId.CinderMarksman:
                    definition = CinderMarksman;
                    return true;
                case SiegeEnemyArchetypeId.AshenBrute:
                    definition = AshenBrute;
                    return true;
                case SiegeEnemyArchetypeId.CinderHexer:
                    definition = CinderHexer;
                    return true;
                case SiegeEnemyArchetypeId.RimeWeaver:
                    definition = RimeWeaver;
                    return true;
                case SiegeEnemyArchetypeId.ReliquaryWarder:
                    definition = ReliquaryWarder;
                    return true;
                case SiegeEnemyArchetypeId.CausewayPrelate:
                    definition = CausewayPrelate;
                    return true;
                default:
                    definition = default;
                    return false;
            }
        }

        private static SiegeEnemyArchetypeDefinition Create(
            SiegeEnemyArchetypeId id,
            string name,
            RunEnemyRank rank,
            RunEnemyTier tier,
            ClassId classId,
            SpecializationId specialization,
            AbilityId primary,
            AbilityId signature,
            SiegeEnemySignaturePolicy policy,
            float health,
            int gold,
            float objectiveDamage,
            int movementSpeedMultiplierPermille,
            float preferredRange,
            float scale,
            bool rushesObjective)
        {
            return new SiegeEnemyArchetypeDefinition(
                id, name, rank, tier, classId, specialization,
                primary, signature, policy, health, gold, objectiveDamage,
                movementSpeedMultiplierPermille, preferredRange, scale,
                rushesObjective);
        }
    }

    public readonly struct SiegeVariantDefinition
    {
        public SiegeVariantDefinition(
            SiegeVariantId id,
            RunPartyDefinition party,
            int laneCount,
            int maximumActiveHostilesPerLane)
        {
            if (id != SiegeVariantId.SoloLane && id != SiegeVariantId.FourLanes)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if (!party.IsValid || party.Count != laneCount)
            {
                throw new ArgumentException(
                    "A Siege variant requires exactly one defender per lane.",
                    nameof(party));
            }

            if ((laneCount != 1 && laneCount != 4) ||
                maximumActiveHostilesPerLane <= 0 ||
                laneCount * maximumActiveHostilesPerLane >
                    HeroSiegeRunCatalog.MaximumActiveHostileCount)
            {
                throw new ArgumentOutOfRangeException(nameof(laneCount));
            }

            Id = id;
            Party = party;
            LaneCount = laneCount;
            MaximumActiveHostilesPerLane = maximumActiveHostilesPerLane;
        }

        public SiegeVariantId Id { get; }
        public RunPartyDefinition Party { get; }
        public int LaneCount { get; }
        public int MaximumActiveHostilesPerLane { get; }

        public int MaximumCombatantCount =>
            Party.Count + (LaneCount * MaximumActiveHostilesPerLane);

        public SiegeLaneId GetLane(int index)
        {
            if (index < 0 || index >= LaneCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            switch (index)
            {
                case 0: return SiegeLaneId.East;
                case 1: return SiegeLaneId.North;
                case 2: return SiegeLaneId.West;
                default: return SiegeLaneId.South;
            }
        }

        public int GetLaneIndex(SiegeLaneId lane)
        {
            for (int index = 0; index < LaneCount; index++)
            {
                if (GetLane(index) == lane)
                {
                    return index;
                }
            }

            return -1;
        }

        public ParticipantId GetDefender(SiegeLaneId lane)
        {
            int index = GetLaneIndex(lane);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lane));
            }

            return Party.GetMember(index);
        }

        public Float3 GetLaneSpawnPosition(SiegeLaneId lane)
        {
            ValidateLane(lane);
            switch (lane)
            {
                case SiegeLaneId.East: return new Float3(11.5f, 0f, 0f);
                case SiegeLaneId.North: return new Float3(0f, 0f, 11.5f);
                case SiegeLaneId.West: return new Float3(-11.5f, 0f, 0f);
                default: return new Float3(0f, 0f, -11.5f);
            }
        }

        public Float3 GetDefenderAnchor(SiegeLaneId lane)
        {
            ValidateLane(lane);
            switch (lane)
            {
                case SiegeLaneId.East: return new Float3(4.25f, 0f, 0f);
                case SiegeLaneId.North: return new Float3(0f, 0f, 4.25f);
                case SiegeLaneId.West: return new Float3(-4.25f, 0f, 0f);
                default: return new Float3(0f, 0f, -4.25f);
            }
        }

        private void ValidateLane(SiegeLaneId lane)
        {
            if (GetLaneIndex(lane) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lane));
            }
        }
    }

    public readonly struct SiegeWaveDefinition
    {
        public SiegeWaveDefinition(
            SiegeWaveId id,
            int number,
            int spawnCountPerLane,
            int healthMultiplierPermille,
            int damageMultiplierPermille,
            int movementSpeedMultiplierPermille)
        {
            if (!id.IsValid || number <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(id));
            }

            if (spawnCountPerLane <= 0 ||
                spawnCountPerLane > HeroSiegeRunCatalog.MaximumSpawnsPerLane)
            {
                throw new ArgumentOutOfRangeException(nameof(spawnCountPerLane));
            }

            if (healthMultiplierPermille < 1000 ||
                damageMultiplierPermille < 1000 ||
                movementSpeedMultiplierPermille < 1000)
            {
                throw new ArgumentOutOfRangeException(nameof(healthMultiplierPermille));
            }

            Id = id;
            Number = number;
            SpawnCountPerLane = spawnCountPerLane;
            HealthMultiplierPermille = healthMultiplierPermille;
            DamageMultiplierPermille = damageMultiplierPermille;
            MovementSpeedMultiplierPermille = movementSpeedMultiplierPermille;
        }

        public SiegeWaveId Id { get; }
        public int Number { get; }
        public int SpawnCountPerLane { get; }
        public int HealthMultiplierPermille { get; }
        public int DamageMultiplierPermille { get; }
        public int MovementSpeedMultiplierPermille { get; }

        public SiegeEnemyArchetypeId GetArchetype(int laneOrdinal)
        {
            if (laneOrdinal < 0 || laneOrdinal >= SpawnCountPerLane)
            {
                throw new ArgumentOutOfRangeException(nameof(laneOrdinal));
            }

            int unlockedCount = Math.Min(
                SiegeEnemyArchetypeCatalog.Count,
                Number + 1);
            int zeroBasedId = (laneOrdinal + Number - 1) % unlockedCount;
            return (SiegeEnemyArchetypeId)(zeroBasedId + 1);
        }
    }

    public readonly struct SiegeSpawnDefinition
    {
        public SiegeSpawnDefinition(
            SiegeWaveId wave,
            SpawnId id,
            ParticipantId participant,
            SiegeLaneId lane,
            int laneOrdinal,
            SiegeEnemyArchetypeDefinition archetype,
            float maximumHealth,
            int goldReward,
            float objectiveDamagePerSecond,
            int damageMultiplierPermille,
            int movementSpeedMultiplierPermille,
            Float3 position)
        {
            if (!wave.IsValid || !id.IsValid || !participant.IsValid ||
                lane < SiegeLaneId.East || lane > SiegeLaneId.South ||
                laneOrdinal < 0 || !archetype.IsValid ||
                !IsFinitePositive(maximumHealth) || goldReward <= 0 ||
                !IsFinitePositive(objectiveDamagePerSecond) ||
                damageMultiplierPermille < 1000 ||
                movementSpeedMultiplierPermille < 250 ||
                movementSpeedMultiplierPermille > 4000 || !IsFinite(position))
            {
                throw new ArgumentException("Invalid deterministic Siege spawn.");
            }

            Wave = wave;
            Id = id;
            Participant = participant;
            Lane = lane;
            LaneOrdinal = laneOrdinal;
            Archetype = archetype;
            MaximumHealth = maximumHealth;
            GoldReward = goldReward;
            ObjectiveDamagePerSecond = objectiveDamagePerSecond;
            DamageMultiplierPermille = damageMultiplierPermille;
            MovementSpeedMultiplierPermille = movementSpeedMultiplierPermille;
            Position = position;
        }

        public SiegeWaveId Wave { get; }
        public SpawnId Id { get; }
        public ParticipantId Participant { get; }
        public SiegeLaneId Lane { get; }
        public int LaneOrdinal { get; }
        public SiegeEnemyArchetypeDefinition Archetype { get; }
        public float MaximumHealth { get; }
        public int GoldReward { get; }
        public float ObjectiveDamagePerSecond { get; }
        public int DamageMultiplierPermille { get; }
        public int MovementSpeedMultiplierPermille { get; }
        public Float3 Position { get; }

        public bool IsValid =>
            Wave.IsValid && Id.IsValid && Participant.IsValid &&
            Lane >= SiegeLaneId.East && Lane <= SiegeLaneId.South &&
            LaneOrdinal >= 0 && Archetype.IsValid &&
            IsFinitePositive(MaximumHealth) && GoldReward > 0 &&
            IsFinitePositive(ObjectiveDamagePerSecond) &&
            DamageMultiplierPermille >= 1000 &&
            MovementSpeedMultiplierPermille >= 250 &&
            MovementSpeedMultiplierPermille <= 4000;

        public RunSpawnDefinition ToRunSpawnDefinition()
        {
            return new RunSpawnDefinition(
                Id, Participant, Archetype.Rank, Archetype.Tier,
                HeroSiegeRunCatalog.HostileTeam, Archetype.Class,
                Archetype.Specialization, MaximumHealth, Position,
                false);
        }

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Float3 value) =>
            !float.IsNaN(value.X) && !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) && !float.IsInfinity(value.Y) &&
            !float.IsNaN(value.Z) && !float.IsInfinity(value.Z);
    }

    /// <summary>
    /// Authored data for the first continuous Hero Siege slice. Wave boundaries
    /// never create a new encounter or combat simulation.
    /// </summary>
    public static class HeroSiegeRunCatalog
    {
        public const int WaveCount = 12;
        public const int MaximumLaneCount = 4;
        public const int MaximumActiveHostileCount = 8;
        public const int MaximumSpawnsPerLane = 14;
        public const int InitialPreparationTicks = 300;
        public const int WaveIntervalTicks = 900;
        public const int SpawnIntervalTicks = 15;
        public const float ObjectiveMaximumHealth = 1000f;
        public const float ObjectivePressureRadius = 2.7f;
        public const float ObjectiveApproachStopRadius = 2.4f;
        public const int StartingGold = 50;
        public const int MaximumPotionCount = 3;
        public const int HealthPotionCost = 45;
        public const int ManaPotionCost = 35;
        public const float HealthPotionRestoreFraction = 0.4f;
        public const float ManaPotionRestoreFraction = 0.5f;

        public static readonly TeamId DefenderTeam = new TeamId(1);
        public static readonly TeamId HostileTeam = new TeamId(2);
        public static readonly ObjectiveId Objective = new ObjectiveId(1u);
        public static readonly Float3 ObjectivePosition = default;

        private static readonly SiegeVariantDefinition SoloLane =
            new SiegeVariantDefinition(
                SiegeVariantId.SoloLane,
                new RunPartyDefinition(new ParticipantId(1)),
                1,
                MaximumActiveHostileCount);

        private static readonly SiegeVariantDefinition FourLanes =
            new SiegeVariantDefinition(
                SiegeVariantId.FourLanes,
                new RunPartyDefinition(
                    new ParticipantId(1), new ParticipantId(2),
                    new ParticipantId(3), new ParticipantId(4)),
                MaximumLaneCount,
                MaximumActiveHostileCount / MaximumLaneCount);

        public static SiegeVariantId DefaultVariant => SiegeVariantId.SoloLane;

        public static SiegeVariantDefinition GetVariant(SiegeVariantId id)
        {
            switch (id)
            {
                case SiegeVariantId.SoloLane: return SoloLane;
                case SiegeVariantId.FourLanes: return FourLanes;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(id), id, "Unknown Siege variant ID.");
            }
        }

        public static SiegeWaveDefinition GetWave(int waveNumber)
        {
            if (waveNumber <= 0 || waveNumber > WaveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(waveNumber));
            }

            int zeroBased = waveNumber - 1;
            return new SiegeWaveDefinition(
                new SiegeWaveId((uint)waveNumber), waveNumber,
                Math.Min(MaximumSpawnsPerLane, 6 + zeroBased),
                Math.Min(3500, 1000 + (120 * zeroBased)),
                Math.Min(2500, 1000 + (70 * zeroBased)),
                Math.Min(1200, 1000 + (10 * zeroBased)));
        }

        public static int GetWaveStartTick(int waveNumber)
        {
            if (waveNumber <= 0 || waveNumber > WaveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(waveNumber));
            }

            return checked(
                InitialPreparationTicks +
                ((waveNumber - 1) * WaveIntervalTicks));
        }

        public static SiegeSpawnDefinition CreateSpawn(
            SiegeWaveDefinition wave,
            SiegeLaneId lane,
            int laneOrdinal)
        {
            if (!wave.Id.IsValid || wave.Number <= 0 || wave.Number > WaveCount)
            {
                throw new ArgumentException(
                    "A spawn requires a catalogued Siege wave.", nameof(wave));
            }

            if (lane < SiegeLaneId.East || lane > SiegeLaneId.South)
            {
                throw new ArgumentOutOfRangeException(nameof(lane));
            }

            SiegeEnemyArchetypeDefinition archetype =
                SiegeEnemyArchetypeCatalog.Get(wave.GetArchetype(laneOrdinal));
            uint serial = checked(
                ((uint)wave.Number * 10000u) +
                ((uint)lane * 100u) + (uint)laneOrdinal + 1u);
            int participantValue = checked(100000 + (int)serial);
            float health = archetype.BaseMaximumHealth *
                wave.HealthMultiplierPermille / 1000f;
            int gold = archetype.BaseGoldReward + ((wave.Number - 1) / 3);
            float objectiveDamage = archetype.BaseObjectiveDamagePerSecond *
                wave.DamageMultiplierPermille / 1000f;
            int movementSpeed = checked(
                ((archetype.BaseMovementSpeedMultiplierPermille *
                    wave.MovementSpeedMultiplierPermille) + 500) / 1000);

            return new SiegeSpawnDefinition(
                wave.Id, new SpawnId(serial),
                new ParticipantId(participantValue), lane, laneOrdinal,
                archetype, health, gold, objectiveDamage,
                wave.DamageMultiplierPermille,
                movementSpeed,
                GetLaneSpawnPosition(lane, laneOrdinal));
        }

        public static int GetWaveClearGoldBonus(int waveNumber)
        {
            if (waveNumber <= 0 || waveNumber > WaveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(waveNumber));
            }

            return 10 + (3 * waveNumber);
        }

        public static int GetPotionCost(SiegePotionId potion)
        {
            switch (potion)
            {
                case SiegePotionId.Health: return HealthPotionCost;
                case SiegePotionId.Mana: return ManaPotionCost;
                default: throw new ArgumentOutOfRangeException(nameof(potion));
            }
        }

        private static Float3 GetLaneSpawnPosition(
            SiegeLaneId lane,
            int laneOrdinal)
        {
            int column = laneOrdinal % 3;
            int row = laneOrdinal / 3;
            float lateral = column == 0 ? 0f : column == 1 ? -0.85f : 0.85f;
            float outward = row * 0.65f;
            switch (lane)
            {
                case SiegeLaneId.East:
                    return new Float3(11.5f + outward, 0f, lateral);
                case SiegeLaneId.North:
                    return new Float3(-lateral, 0f, 11.5f + outward);
                case SiegeLaneId.West:
                    return new Float3(-11.5f - outward, 0f, -lateral);
                default:
                    return new Float3(lateral, 0f, -11.5f - outward);
            }
        }
    }

    public readonly struct SiegeRunState
    {
        internal SiegeRunState(
            SiegeRunStatus status,
            SiegeVariantId variant,
            SiegeWaveId currentWave,
            int currentWaveNumber,
            int startedWaveCount,
            int clearedWaveCount,
            int currentTick,
            int nextWaveStartTick,
            int currentWaveSpawnCount,
            int scheduledSpawnCount,
            int defeatedSpawnCount,
            int totalScheduledSpawnCount,
            int totalDefeatedSpawnCount,
            int pendingSpawnCount,
            int activeHostileCount,
            int livingDefenderCount,
            ObjectiveState objective,
            int kills,
            int gold,
            int healthPotionCount,
            int manaPotionCount,
            int lastTransitionTick)
        {
            Status = status;
            Variant = variant;
            CurrentWave = currentWave;
            CurrentWaveNumber = currentWaveNumber;
            StartedWaveCount = startedWaveCount;
            ClearedWaveCount = clearedWaveCount;
            CurrentTick = currentTick;
            NextWaveStartTick = nextWaveStartTick;
            CurrentWaveSpawnCount = currentWaveSpawnCount;
            ScheduledSpawnCount = scheduledSpawnCount;
            DefeatedSpawnCount = defeatedSpawnCount;
            TotalScheduledSpawnCount = totalScheduledSpawnCount;
            TotalDefeatedSpawnCount = totalDefeatedSpawnCount;
            PendingSpawnCount = pendingSpawnCount;
            ActiveHostileCount = activeHostileCount;
            LivingDefenderCount = livingDefenderCount;
            Objective = objective;
            Kills = kills;
            Gold = gold;
            HealthPotionCount = healthPotionCount;
            ManaPotionCount = manaPotionCount;
            LastTransitionTick = lastTransitionTick;
        }

        public SiegeRunStatus Status { get; }
        public SiegeVariantId Variant { get; }
        public SiegeWaveId CurrentWave { get; }
        public int CurrentWaveNumber { get; }
        public int StartedWaveCount { get; }
        public int ClearedWaveCount { get; }
        public int ActiveWaveCount => StartedWaveCount - ClearedWaveCount;
        public int CurrentTick { get; }
        public int NextWaveStartTick { get; }
        public int CurrentWaveSpawnCount { get; }
        public int ScheduledSpawnCount { get; }
        public int DefeatedSpawnCount { get; }
        public int TotalScheduledSpawnCount { get; }
        public int TotalDefeatedSpawnCount { get; }
        public int PendingSpawnCount { get; }
        public int ActiveHostileCount { get; }
        public int LivingDefenderCount { get; }
        public ObjectiveState Objective { get; }
        public int Kills { get; }
        public int Gold { get; }
        public int HealthPotionCount { get; }
        public int ManaPotionCount { get; }
        public int LastTransitionTick { get; }

        public int TicksUntilNextWave =>
            StartedWaveCount < HeroSiegeRunCatalog.WaveCount
                ? Math.Max(0, NextWaveStartTick - CurrentTick)
                : 0;

        public int RemainingWaveSpawnCount =>
            Math.Max(0, CurrentWaveSpawnCount - DefeatedSpawnCount);

        public bool IsTerminal =>
            Status == SiegeRunStatus.Completed ||
            Status == SiegeRunStatus.Failed ||
            Status == SiegeRunStatus.Abandoned;
    }

    public readonly struct SiegeRunResult
    {
        internal SiegeRunResult(
            RunId run,
            RunOutcome outcome,
            SiegeRunEndReason reason,
            int terminalTick,
            int clearedWaveCount,
            int kills,
            int gold)
        {
            Run = run;
            Outcome = outcome;
            Reason = reason;
            TerminalTick = terminalTick;
            ClearedWaveCount = clearedWaveCount;
            Kills = kills;
            Gold = gold;
        }

        public RunId Run { get; }
        public RunOutcome Outcome { get; }
        public SiegeRunEndReason Reason { get; }
        public int TerminalTick { get; }
        public int ClearedWaveCount { get; }
        public int Kills { get; }
        public int Gold { get; }
        public bool IsTerminal => Outcome != RunOutcome.None;
        public bool DidWin => Outcome == RunOutcome.Victory;
    }

    public readonly struct SiegeAdvanceObservation
    {
        internal SiegeAdvanceObservation(
            bool wasHandled,
            int startedWaveCount,
            SiegeWaveId wave,
            int queuedSpawnCount)
        {
            WasHandled = wasHandled;
            StartedWaveCount = startedWaveCount;
            Wave = wave;
            QueuedSpawnCount = queuedSpawnCount;
        }

        public bool WasHandled { get; }
        public int StartedWaveCount { get; }
        public bool WaveStarted => StartedWaveCount > 0;
        public SiegeWaveId Wave { get; }
        public int QueuedSpawnCount { get; }
    }

    public readonly struct SiegeCombatObservation
    {
        internal SiegeCombatObservation(
            bool wasHandled,
            SpawnId defeatedSpawn,
            ParticipantId defeatedDefender,
            int goldAwarded,
            bool waveCleared,
            SiegeWaveId clearedWave,
            bool runCompleted,
            bool runFailed)
        {
            WasHandled = wasHandled;
            DefeatedSpawn = defeatedSpawn;
            DefeatedDefender = defeatedDefender;
            GoldAwarded = goldAwarded;
            WaveCleared = waveCleared;
            ClearedWave = clearedWave;
            RunCompleted = runCompleted;
            RunFailed = runFailed;
        }

        public bool WasHandled { get; }
        public SpawnId DefeatedSpawn { get; }
        public ParticipantId DefeatedDefender { get; }
        public int GoldAwarded { get; }
        public bool WaveCleared { get; }
        public SiegeWaveId ClearedWave { get; }
        public bool RunCompleted { get; }
        public bool RunFailed { get; }
    }

    public readonly struct SiegeObjectivePressureObservation
    {
        internal SiegeObjectivePressureObservation(
            bool wasHandled,
            int pressureHostileCount,
            float requestedDamage,
            float appliedDamage,
            ObjectiveState objective,
            bool runFailed)
        {
            WasHandled = wasHandled;
            PressureHostileCount = pressureHostileCount;
            RequestedDamage = requestedDamage;
            AppliedDamage = appliedDamage;
            Objective = objective;
            RunFailed = runFailed;
        }

        public bool WasHandled { get; }
        public int PressureHostileCount { get; }
        public float RequestedDamage { get; }
        public float AppliedDamage { get; }
        public ObjectiveState Objective { get; }
        public bool RunFailed { get; }
    }

    public readonly struct SiegePurchaseObservation
    {
        internal SiegePurchaseObservation(
            bool wasHandled,
            SiegePotionId potion,
            int cost,
            int remainingGold,
            int inventoryCount)
        {
            WasHandled = wasHandled;
            Potion = potion;
            Cost = cost;
            RemainingGold = remainingGold;
            InventoryCount = inventoryCount;
        }

        public bool WasHandled { get; }
        public SiegePotionId Potion { get; }
        public int Cost { get; }
        public int RemainingGold { get; }
        public int InventoryCount { get; }
    }

    public readonly struct SiegePotionUse
    {
        internal SiegePotionUse(
            bool wasHandled,
            SiegePotionId potion,
            ParticipantId consumer,
            ResourceType resourceType,
            float requestedRestore,
            int remainingInventoryCount)
        {
            WasHandled = wasHandled;
            Potion = potion;
            Consumer = consumer;
            ResourceType = resourceType;
            RequestedRestore = requestedRestore;
            RemainingInventoryCount = remainingInventoryCount;
        }

        public bool WasHandled { get; }
        public SiegePotionId Potion { get; }
        public ParticipantId Consumer { get; }
        public ResourceType ResourceType { get; }
        public float RequestedRestore { get; }
        public int RemainingInventoryCount { get; }
    }

    /// <summary>
    /// Continuous, allocation-free-after-construction Siege orchestration. It
    /// schedules bounded reinforcements into one long-lived CombatSimulation and
    /// observes authoritative snapshots/events. Wave boundaries only update run
    /// economy and pacing; they do not create encounters or recycle identities.
    /// </summary>
    public sealed class HeroSiegeRunController
    {
        private readonly RunId _run;
        private readonly SiegeVariantDefinition _variant;
        private readonly int[] _scheduledPerWaveLane;
        private readonly int[] _scheduledPerWave;
        private readonly int[] _defeatedPerWave;
        private readonly int[] _occupiedPerLane;
        private readonly SiegeSpawnDefinition[] _pendingSpawns;
        private readonly SiegeSpawnDefinition[] _activeSpawns;

        private ObjectiveState _objective;
        private SiegeRunStatus _status;
        private SiegeRunEndReason _endReason;
        private RunOutcome _outcome;
        private SiegeWaveDefinition _latestStartedWave;
        private int _startedWaveCount;
        private int _clearedWaveCount;
        private int _clearedWaveMask;
        private int _currentTick;
        private int _lastAdvanceTick;
        private int _lastCombatEventTick;
        private int _lastObjectivePressureTick;
        private int _lastTransitionTick;
        private int _terminalTick;
        private int _nextWaveStartTick;
        private int _nextSpawnTick;
        private int _totalScheduledSpawnCount;
        private int _totalDefeatedSpawnCount;
        private int _spawnWaveCursor;
        private int _pendingHead;
        private int _pendingSpawnCount;
        private int _activeHostileCount;
        private int _livingDefenderMask;
        private int _kills;
        private int _gold;
        private int _healthPotionCount;
        private int _manaPotionCount;

        public HeroSiegeRunController()
            : this(new RunId(1u), HeroSiegeRunCatalog.DefaultVariant)
        {
        }

        public HeroSiegeRunController(RunId run)
            : this(run, HeroSiegeRunCatalog.DefaultVariant)
        {
        }

        public HeroSiegeRunController(RunId run, SiegeVariantId variant)
        {
            if (!run.IsValid)
            {
                throw new ArgumentException(
                    "A Hero Siege run requires a valid stable ID.", nameof(run));
            }

            _run = run;
            _variant = HeroSiegeRunCatalog.GetVariant(variant);
            _scheduledPerWaveLane = new int[
                HeroSiegeRunCatalog.WaveCount *
                HeroSiegeRunCatalog.MaximumLaneCount];
            _scheduledPerWave = new int[HeroSiegeRunCatalog.WaveCount];
            _defeatedPerWave = new int[HeroSiegeRunCatalog.WaveCount];
            _occupiedPerLane = new int[HeroSiegeRunCatalog.MaximumLaneCount];
            _pendingSpawns = new SiegeSpawnDefinition[
                HeroSiegeRunCatalog.MaximumActiveHostileCount];
            _activeSpawns = new SiegeSpawnDefinition[
                HeroSiegeRunCatalog.MaximumActiveHostileCount];
            _objective = new ObjectiveState(
                HeroSiegeRunCatalog.Objective,
                HeroSiegeRunCatalog.ObjectiveMaximumHealth);
            _status = SiegeRunStatus.Preparation;
            _lastAdvanceTick = -1;
            _lastCombatEventTick = -1;
            _lastObjectivePressureTick = -1;
            _terminalTick = -1;
            _nextWaveStartTick = HeroSiegeRunCatalog.GetWaveStartTick(1);
            _livingDefenderMask = (1 << _variant.Party.Count) - 1;
            _gold = HeroSiegeRunCatalog.StartingGold;
        }

        public RunId Run => _run;
        public PlayableModeId Mode => PlayableModeId.HeroSiege;
        public SiegeVariantDefinition Variant => _variant;

        public SiegeRunState State => new SiegeRunState(
            _status, _variant.Id, _latestStartedWave.Id, _startedWaveCount,
            _startedWaveCount, _clearedWaveCount, _currentTick, _nextWaveStartTick,
            CurrentWaveSpawnCount, CurrentWaveScheduledSpawnCount,
            CurrentWaveDefeatedSpawnCount,
            _totalScheduledSpawnCount, _totalDefeatedSpawnCount,
            _pendingSpawnCount, _activeHostileCount,
            CountSetBits(_livingDefenderMask), _objective, _kills, _gold,
            _healthPotionCount, _manaPotionCount, _lastTransitionTick);

        public SiegeRunResult Result => new SiegeRunResult(
            _run, _outcome, _endReason, _terminalTick,
            _clearedWaveCount, _kills, _gold);

        public ParticipantId GetDefender(int partyIndex) =>
            _variant.Party.GetMember(partyIndex);

        public SiegeLaneId GetDefenderLane(int partyIndex) =>
            _variant.GetLane(partyIndex);

        public int GetLaneOccupiedCount(SiegeLaneId lane)
        {
            int index = _variant.GetLaneIndex(lane);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(lane));
            }

            return _occupiedPerLane[index];
        }

        public int GetScheduledSpawnCount(SiegeWaveId wave)
        {
            int index = GetStartedWaveIndex(wave);
            return _scheduledPerWave[index];
        }

        public int GetDefeatedSpawnCount(SiegeWaveId wave)
        {
            int index = GetStartedWaveIndex(wave);
            return _defeatedPerWave[index];
        }

        public bool IsWaveCleared(SiegeWaveId wave)
        {
            int index = GetStartedWaveIndex(wave);
            return (_clearedWaveMask & (1 << index)) != 0;
        }

        public SiegeSpawnDefinition GetActiveSpawn(int index)
        {
            if (index < 0 || index >= _activeHostileCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _activeSpawns[index];
        }

        public SiegeAdvanceObservation Advance(int currentTick)
        {
            if (currentTick < 0 || currentTick <= _lastAdvanceTick ||
                currentTick < _currentTick || IsTerminal)
            {
                return default;
            }

            _lastAdvanceTick = currentTick;
            _currentTick = currentTick;
            int startedWaveCount = StartDueWaves(currentTick);

            int queued = 0;
            if (_status == SiegeRunStatus.WaveActive &&
                currentTick >= _nextSpawnTick)
            {
                queued = QueueSpawnBatch();
                if (queued > 0)
                {
                    _nextSpawnTick = currentTick +
                        HeroSiegeRunCatalog.SpawnIntervalTicks;
                }
            }

            return new SiegeAdvanceObservation(
                true, startedWaveCount, _latestStartedWave.Id, queued);
        }

        public bool TryDequeueSpawn(out SiegeSpawnDefinition spawn)
        {
            if (_pendingSpawnCount <= 0 ||
                _activeHostileCount >= _activeSpawns.Length)
            {
                spawn = default;
                return false;
            }

            spawn = _pendingSpawns[_pendingHead];
            _pendingSpawns[_pendingHead] = default;
            _pendingHead = (_pendingHead + 1) % _pendingSpawns.Length;
            _pendingSpawnCount--;
            _activeSpawns[_activeHostileCount++] = spawn;
            return true;
        }

        public SiegeCombatObservation ObserveCombatEvent(CombatEvent combatEvent)
        {
            if (_status != SiegeRunStatus.WaveActive ||
                combatEvent.Kind != CombatEventKind.Death ||
                combatEvent.Tick < _lastCombatEventTick ||
                combatEvent.Tick < _lastTransitionTick)
            {
                return default;
            }

            if (_variant.Party.TryGetMemberIndex(
                combatEvent.Target, out int defenderIndex))
            {
                int bit = 1 << defenderIndex;
                if ((_livingDefenderMask & bit) == 0)
                {
                    return default;
                }

                _livingDefenderMask &= ~bit;
                _lastCombatEventTick = combatEvent.Tick;
                UpdateCurrentTick(combatEvent.Tick);
                bool failed = _livingDefenderMask == 0;
                if (failed)
                {
                    Finish(
                        RunOutcome.Defeat, SiegeRunStatus.Failed,
                        SiegeRunEndReason.DefendersDefeated, combatEvent.Tick);
                }

                return new SiegeCombatObservation(
                    true, default, combatEvent.Target, 0,
                    false, default, false, failed);
            }

            int activeIndex = FindActiveSpawn(combatEvent.Target);
            if (activeIndex < 0)
            {
                return default;
            }

            SiegeSpawnDefinition defeated = _activeSpawns[activeIndex];
            RemoveActiveSpawn(activeIndex);
            int laneIndex = _variant.GetLaneIndex(defeated.Lane);
            _occupiedPerLane[laneIndex]--;
            int waveIndex = checked((int)defeated.Wave.Value - 1);
            _defeatedPerWave[waveIndex]++;
            _totalDefeatedSpawnCount++;
            _kills++;
            _gold += defeated.GoldReward;
            _lastCombatEventTick = combatEvent.Tick;
            UpdateCurrentTick(combatEvent.Tick);

            int goldAwarded = defeated.GoldReward;
            bool waveCleared = IsWaveNewlyCleared(waveIndex);
            bool completed = false;
            if (waveCleared)
            {
                _clearedWaveMask |= 1 << waveIndex;
                _clearedWaveCount++;
                int bonus = HeroSiegeRunCatalog.GetWaveClearGoldBonus(
                    waveIndex + 1);
                _gold += bonus;
                goldAwarded += bonus;
                _lastTransitionTick = combatEvent.Tick;

                if (_clearedWaveCount >= HeroSiegeRunCatalog.WaveCount)
                {
                    Finish(
                        RunOutcome.Victory, SiegeRunStatus.Completed,
                        SiegeRunEndReason.AllWavesCleared, combatEvent.Tick);
                    completed = true;
                }
                else if (_clearedWaveCount == _startedWaveCount &&
                    _activeHostileCount == 0 && _pendingSpawnCount == 0)
                {
                    _status = SiegeRunStatus.Preparation;
                }
                else
                {
                    _status = SiegeRunStatus.WaveActive;
                }
            }

            return new SiegeCombatObservation(
                true, defeated.Id, default, goldAwarded, waveCleared,
                waveCleared ? defeated.Wave : default,
                completed, false);
        }

        /// <summary>
        /// Applies objective damage only for active hostile snapshots currently
        /// inside the central base radius. The snapshot buffer is never retained.
        /// </summary>
        public SiegeObjectivePressureObservation ApplyObjectivePressure(
            int currentTick,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            int simulationTickRate)
        {
            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (snapshotCount < 0 || snapshotCount > snapshots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshotCount));
            }

            if (_status != SiegeRunStatus.WaveActive || currentTick < 0 ||
                currentTick <= _lastObjectivePressureTick ||
                currentTick < _lastTransitionTick || currentTick < _currentTick ||
                simulationTickRate <= 0)
            {
                return default;
            }

            float radiusSquared = HeroSiegeRunCatalog.ObjectivePressureRadius *
                HeroSiegeRunCatalog.ObjectivePressureRadius;
            int pressureCount = 0;
            float requestedDamage = 0f;
            for (int index = 0; index < _activeHostileCount; index++)
            {
                SiegeSpawnDefinition spawn = _activeSpawns[index];
                if (!TryFindLivingSnapshot(
                    spawn.Participant, snapshots, snapshotCount,
                    out CombatantSnapshot snapshot) ||
                    IsUnableToPressureObjective(snapshot))
                {
                    continue;
                }

                Float3 offset = snapshot.Position -
                    HeroSiegeRunCatalog.ObjectivePosition;
                if (offset.PlanarLengthSquared > radiusSquared)
                {
                    continue;
                }

                pressureCount++;
                requestedDamage += spawn.ObjectiveDamagePerSecond /
                    simulationTickRate;
            }

            float appliedDamage = _objective.ApplyDamage(requestedDamage);
            _lastObjectivePressureTick = currentTick;
            UpdateCurrentTick(currentTick);
            bool failed = _objective.IsDefeated;
            if (failed)
            {
                Finish(
                    RunOutcome.Defeat, SiegeRunStatus.Failed,
                    SiegeRunEndReason.ObjectiveDefeated, currentTick);
            }

            return new SiegeObjectivePressureObservation(
                true, pressureCount, requestedDamage, appliedDamage,
                _objective, failed);
        }

        private static bool IsUnableToPressureObjective(
            CombatantSnapshot snapshot)
        {
            const AuraTag blockingTags = AuraTag.Fear |
                AuraTag.Stun |
                AuraTag.Disorient |
                AuraTag.Horror |
                AuraTag.Pacified |
                AuraTag.TargetingDropped;
            return snapshot.CastingAbility != AbilityId.None ||
                (snapshot.ActiveAuraTags & blockingTags) != 0;
        }

        public SiegePurchaseObservation TryPurchasePotion(SiegePotionId potion)
        {
            if (IsTerminal ||
                (potion != SiegePotionId.Health && potion != SiegePotionId.Mana))
            {
                return default;
            }

            int cost = HeroSiegeRunCatalog.GetPotionCost(potion);
            int count = GetPotionCount(potion);
            if (_gold < cost || count >= HeroSiegeRunCatalog.MaximumPotionCount)
            {
                return default;
            }

            _gold -= cost;
            count++;
            SetPotionCount(potion, count);
            return new SiegePurchaseObservation(
                true, potion, cost, _gold, count);
        }

        /// <summary>
        /// Consumes inventory only for a living authoritative defender snapshot
        /// that can receive a non-zero restore. Core applies the returned value.
        /// </summary>
        public SiegePotionUse TryConsumePotion(
            SiegePotionId potion,
            CombatantSnapshot consumer)
        {
            if (IsTerminal || !consumer.IsAlive ||
                !_variant.Party.TryGetMemberIndex(consumer.Id, out _) ||
                (potion != SiegePotionId.Health && potion != SiegePotionId.Mana))
            {
                return default;
            }

            int count = GetPotionCount(potion);
            if (count <= 0)
            {
                return default;
            }

            ResourceType resourceType;
            float restore;
            if (potion == SiegePotionId.Health)
            {
                resourceType = ResourceType.None;
                restore = Math.Min(
                    consumer.MaxHealth - consumer.Health,
                    consumer.MaxHealth *
                        HeroSiegeRunCatalog.HealthPotionRestoreFraction);
            }
            else
            {
                if (consumer.ResourceType != ResourceType.Mana)
                {
                    return default;
                }

                resourceType = ResourceType.Mana;
                restore = Math.Min(
                    consumer.MaxResource - consumer.Resource,
                    consumer.MaxResource *
                        HeroSiegeRunCatalog.ManaPotionRestoreFraction);
            }

            if (restore <= 0f || float.IsNaN(restore) || float.IsInfinity(restore))
            {
                return default;
            }

            count--;
            SetPotionCount(potion, count);
            return new SiegePotionUse(
                true, potion, consumer.Id, resourceType, restore, count);
        }

        public bool TryAbandon(int currentTick)
        {
            if (currentTick < 0 || currentTick < _currentTick || IsTerminal)
            {
                return false;
            }

            UpdateCurrentTick(currentTick);
            Finish(
                RunOutcome.Defeat, SiegeRunStatus.Abandoned,
                SiegeRunEndReason.Abandoned, currentTick);
            return true;
        }

        private bool IsTerminal =>
            _status == SiegeRunStatus.Completed ||
            _status == SiegeRunStatus.Failed ||
            _status == SiegeRunStatus.Abandoned;

        private int CurrentWaveSpawnCount =>
            _latestStartedWave.Id.IsValid
                ? _latestStartedWave.SpawnCountPerLane * _variant.LaneCount
                : 0;

        private int CurrentWaveScheduledSpawnCount =>
            _startedWaveCount > 0
                ? _scheduledPerWave[_startedWaveCount - 1]
                : 0;

        private int CurrentWaveDefeatedSpawnCount =>
            _startedWaveCount > 0
                ? _defeatedPerWave[_startedWaveCount - 1]
                : 0;

        private int StartDueWaves(int currentTick)
        {
            int started = 0;
            while (_startedWaveCount < HeroSiegeRunCatalog.WaveCount &&
                currentTick >= HeroSiegeRunCatalog.GetWaveStartTick(
                    _startedWaveCount + 1))
            {
                _startedWaveCount++;
                _latestStartedWave = HeroSiegeRunCatalog.GetWave(
                    _startedWaveCount);
                started++;
            }

            if (started <= 0)
            {
                return 0;
            }

            _status = SiegeRunStatus.WaveActive;
            _nextWaveStartTick = _startedWaveCount < HeroSiegeRunCatalog.WaveCount
                ? HeroSiegeRunCatalog.GetWaveStartTick(_startedWaveCount + 1)
                : -1;
            // Each absolute wave boundary gets an immediate fair scheduling turn.
            _nextSpawnTick = currentTick;
            _lastTransitionTick = currentTick;
            return started;
        }

        private int QueueSpawnBatch()
        {
            int queued = 0;
            bool madeProgress;
            do
            {
                madeProgress = false;
                int initialWaveCursor = _spawnWaveCursor;
                for (int waveOffset = 0;
                    waveOffset < _startedWaveCount &&
                    _activeHostileCount + _pendingSpawnCount <
                        HeroSiegeRunCatalog.MaximumActiveHostileCount;
                    waveOffset++)
                {
                    int waveIndex = (initialWaveCursor + waveOffset) %
                        _startedWaveCount;
                    SiegeWaveDefinition wave = HeroSiegeRunCatalog.GetWave(
                        waveIndex + 1);
                    bool queuedFromWave = false;
                    for (int laneIndex = 0;
                        laneIndex < _variant.LaneCount &&
                        _activeHostileCount + _pendingSpawnCount <
                            HeroSiegeRunCatalog.MaximumActiveHostileCount;
                        laneIndex++)
                    {
                        int scheduleIndex = GetWaveLaneIndex(waveIndex, laneIndex);
                        if (_scheduledPerWaveLane[scheduleIndex] >=
                                wave.SpawnCountPerLane ||
                            _occupiedPerLane[laneIndex] >=
                                _variant.MaximumActiveHostilesPerLane)
                        {
                            continue;
                        }

                        SiegeLaneId lane = _variant.GetLane(laneIndex);
                        SiegeSpawnDefinition spawn = HeroSiegeRunCatalog.CreateSpawn(
                            wave,
                            lane,
                            _scheduledPerWaveLane[scheduleIndex]);
                        int tail = (_pendingHead + _pendingSpawnCount) %
                            _pendingSpawns.Length;
                        _pendingSpawns[tail] = spawn;
                        _pendingSpawnCount++;
                        _scheduledPerWaveLane[scheduleIndex]++;
                        _scheduledPerWave[waveIndex]++;
                        _occupiedPerLane[laneIndex]++;
                        _totalScheduledSpawnCount++;
                        queued++;
                        queuedFromWave = true;
                        madeProgress = true;
                    }

                    if (queuedFromWave)
                    {
                        _spawnWaveCursor = (waveIndex + 1) % _startedWaveCount;
                    }
                }
            }
            while (madeProgress &&
                _activeHostileCount + _pendingSpawnCount <
                    HeroSiegeRunCatalog.MaximumActiveHostileCount);

            return queued;
        }

        private bool IsWaveNewlyCleared(int waveIndex)
        {
            if (waveIndex < 0 || waveIndex >= _startedWaveCount ||
                (_clearedWaveMask & (1 << waveIndex)) != 0)
            {
                return false;
            }

            SiegeWaveDefinition wave = HeroSiegeRunCatalog.GetWave(waveIndex + 1);
            int quota = wave.SpawnCountPerLane * _variant.LaneCount;
            return _scheduledPerWave[waveIndex] == quota &&
                _defeatedPerWave[waveIndex] == quota;
        }

        private static int GetWaveLaneIndex(int waveIndex, int laneIndex) =>
            (waveIndex * HeroSiegeRunCatalog.MaximumLaneCount) + laneIndex;

        private int GetStartedWaveIndex(SiegeWaveId wave)
        {
            int index = checked((int)wave.Value - 1);
            if (!wave.IsValid || index < 0 || index >= _startedWaveCount)
            {
                throw new ArgumentOutOfRangeException(nameof(wave));
            }

            return index;
        }

        private int FindActiveSpawn(ParticipantId participant)
        {
            for (int index = 0; index < _activeHostileCount; index++)
            {
                if (_activeSpawns[index].Participant == participant)
                {
                    return index;
                }
            }

            return -1;
        }

        private void RemoveActiveSpawn(int index)
        {
            for (int move = index; move < _activeHostileCount - 1; move++)
            {
                _activeSpawns[move] = _activeSpawns[move + 1];
            }

            _activeHostileCount--;
            _activeSpawns[_activeHostileCount] = default;
        }

        private static bool TryFindLivingSnapshot(
            ParticipantId participant,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            out CombatantSnapshot snapshot)
        {
            for (int index = 0; index < snapshotCount; index++)
            {
                if (snapshots[index].Id == participant && snapshots[index].IsAlive)
                {
                    snapshot = snapshots[index];
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        private int GetPotionCount(SiegePotionId potion) =>
            potion == SiegePotionId.Health
                ? _healthPotionCount
                : _manaPotionCount;

        private void SetPotionCount(SiegePotionId potion, int count)
        {
            if (potion == SiegePotionId.Health)
            {
                _healthPotionCount = count;
            }
            else
            {
                _manaPotionCount = count;
            }
        }

        private void UpdateCurrentTick(int tick)
        {
            if (tick > _currentTick)
            {
                _currentTick = tick;
            }
        }

        private void Finish(
            RunOutcome outcome,
            SiegeRunStatus status,
            SiegeRunEndReason reason,
            int terminalTick)
        {
            _outcome = outcome;
            _status = status;
            _endReason = reason;
            _terminalTick = terminalTick;
            _lastTransitionTick = terminalTick;
        }

        private static int CountSetBits(int mask)
        {
            int count = 0;
            while (mask != 0)
            {
                count += mask & 1;
                mask >>= 1;
            }

            return count;
        }
    }
}
