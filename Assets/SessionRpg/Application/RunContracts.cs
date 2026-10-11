using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    public enum RunStatus
    {
        Ready = 0,
        EncounterActive = 1,
        AwaitingAdvance = 2,
        AwaitingRetry = 3,
        Completed = 4,
        Failed = 5,
    }

    public enum RunOutcome
    {
        None = 0,
        Victory = 1,
        Defeat = 2,
    }

    public enum RunEndReason
    {
        None = 0,
        AllEncountersCleared = 1,
        PartyDefeated = 2,
        ObjectiveDefeated = 3,
        Abandoned = 4,
    }

    public enum RunEnemyRank
    {
        None = 0,
        Standard = 1,
        Elite = 2,
        Miniboss = 3,
        Boss = 4,
    }

    /// <summary>
    /// Stable Siege-facing combat tier. Rank describes encounter structure
    /// (standard/elite/miniboss/boss); tier describes the authored enemy family
    /// presented to combat systems without Unity inference.
    /// </summary>
    public enum RunEnemyTier
    {
        None = 0,
        SwarmMinion = 1,
        Medium = 2,
        Boss = 3,
    }

    public enum RunEncounterChallenge
    {
        None = 0,
        Standard = 1,
        Miniboss = 2,
        Boss = 3,
    }

    public readonly struct RunSpawnDefinition
    {
        public RunSpawnDefinition(
            SpawnId id,
            ParticipantId participant,
            RunEnemyRank rank,
            RunEnemyTier tier,
            TeamId team,
            ClassId classId,
            SpecializationId specialization,
            float maximumHealth,
            Float3 position,
            bool applyClassOpeningState = true)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "A run spawn requires a valid stable ID.",
                    nameof(id));
            }

            if (!participant.IsValid)
            {
                throw new ArgumentException(
                    "A run spawn requires a valid combat participant ID.",
                    nameof(participant));
            }

            if (rank < RunEnemyRank.Standard || rank > RunEnemyRank.Boss)
            {
                throw new ArgumentOutOfRangeException(nameof(rank));
            }

            if (tier < RunEnemyTier.SwarmMinion ||
                tier > RunEnemyTier.Boss)
            {
                throw new ArgumentOutOfRangeException(nameof(tier));
            }

            bool bossRank = rank == RunEnemyRank.Miniboss ||
                rank == RunEnemyRank.Boss;
            if (bossRank != (tier == RunEnemyTier.Boss))
            {
                throw new ArgumentException(
                    "Miniboss and boss ranks must use the explicit boss tier, and non-boss ranks must not.",
                    nameof(tier));
            }

            if (team.Value <= 0)
            {
                throw new ArgumentException(
                    "A run spawn requires a valid team ID.",
                    nameof(team));
            }

            if (!SpecializationCatalog.IsCompatible(classId, specialization))
            {
                throw new ArgumentException(
                    "A run spawn class and specialization must be compatible.",
                    nameof(specialization));
            }

            if (!IsFinitePositive(maximumHealth))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            }

            if (!IsFinite(position))
            {
                throw new ArgumentOutOfRangeException(nameof(position));
            }

            Id = id;
            Participant = participant;
            Rank = rank;
            Tier = tier;
            Team = team;
            Class = classId;
            Specialization = specialization;
            MaximumHealth = maximumHealth;
            Position = position;
            ApplyClassOpeningState = applyClassOpeningState;
        }

        public SpawnId Id { get; }

        public ParticipantId Participant { get; }

        public RunEnemyRank Rank { get; }

        public RunEnemyTier Tier { get; }

        public TeamId Team { get; }

        public ClassId Class { get; }

        public SpecializationId Specialization { get; }

        public float MaximumHealth { get; }

        public Float3 Position { get; }

        public bool ApplyClassOpeningState { get; }

        public bool IsValid =>
            Id.IsValid &&
            Participant.IsValid &&
            Rank >= RunEnemyRank.Standard &&
            Rank <= RunEnemyRank.Boss &&
            Tier >= RunEnemyTier.SwarmMinion &&
            Tier <= RunEnemyTier.Boss &&
            ((Rank == RunEnemyRank.Miniboss || Rank == RunEnemyRank.Boss) ==
             (Tier == RunEnemyTier.Boss)) &&
            Team.Value > 0 &&
            SpecializationCatalog.IsCompatible(Class, Specialization) &&
            IsFinitePositive(MaximumHealth) &&
            IsFinite(Position);

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private static bool IsFinite(Float3 value) =>
            !float.IsNaN(value.X) &&
            !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) &&
            !float.IsInfinity(value.Y) &&
            !float.IsNaN(value.Z) &&
            !float.IsInfinity(value.Z);
    }

    public readonly struct RunPartyDefinition
    {
        public const int MaximumMemberCount = 4;

        private readonly ParticipantId _member0;
        private readonly ParticipantId _member1;
        private readonly ParticipantId _member2;
        private readonly ParticipantId _member3;

        public RunPartyDefinition(
            ParticipantId member0,
            ParticipantId member1 = default,
            ParticipantId member2 = default,
            ParticipantId member3 = default)
        {
            _member0 = member0;
            _member1 = member1;
            _member2 = member2;
            _member3 = member3;

            if (!member0.IsValid)
            {
                throw new ArgumentException(
                    "A run party requires at least one valid participant.",
                    nameof(member0));
            }

            if ((!member1.IsValid && (member2.IsValid || member3.IsValid)) ||
                (!member2.IsValid && member3.IsValid))
            {
                throw new ArgumentException(
                    "Run party participants must occupy contiguous slots.");
            }

            Count = member3.IsValid
                ? 4
                : member2.IsValid
                    ? 3
                    : member1.IsValid
                        ? 2
                        : 1;

            for (int index = 0; index < Count; index++)
            {
                ParticipantId member = GetMemberUnchecked(index);
                for (int otherIndex = 0; otherIndex < index; otherIndex++)
                {
                    if (member == GetMemberUnchecked(otherIndex))
                    {
                        throw new ArgumentException(
                            "Run party participant IDs must be unique.");
                    }
                }
            }
        }

        public int Count { get; }

        public bool IsValid => Count > 0 && _member0.IsValid;

        public ParticipantId GetMember(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetMemberUnchecked(index);
        }

        public bool TryGetMemberIndex(ParticipantId participant, out int index)
        {
            for (int candidateIndex = 0; candidateIndex < Count; candidateIndex++)
            {
                if (GetMemberUnchecked(candidateIndex) == participant)
                {
                    index = candidateIndex;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private ParticipantId GetMemberUnchecked(int index)
        {
            switch (index)
            {
                case 0:
                    return _member0;
                case 1:
                    return _member1;
                case 2:
                    return _member2;
                default:
                    return _member3;
            }
        }
    }

    public readonly struct RunPartyMemberDefinition
    {
        public RunPartyMemberDefinition(
            ParticipantId participant,
            TeamId team,
            ClassId classId,
            SpecializationId specialization,
            float maximumHealth,
            bool isHumanControlled,
            bool applyClassOpeningState = true)
        {
            if (!participant.IsValid)
            {
                throw new ArgumentException(
                    "A run party member requires a valid participant ID.",
                    nameof(participant));
            }

            if (team.Value <= 0)
            {
                throw new ArgumentException(
                    "A run party member requires a valid team ID.",
                    nameof(team));
            }

            if (!SpecializationCatalog.IsCompatible(classId, specialization))
            {
                throw new ArgumentException(
                    "A run party member class and specialization must be compatible.",
                    nameof(specialization));
            }

            if (maximumHealth <= 0f ||
                float.IsNaN(maximumHealth) ||
                float.IsInfinity(maximumHealth))
            {
                throw new ArgumentOutOfRangeException(nameof(maximumHealth));
            }

            Participant = participant;
            Team = team;
            Class = classId;
            Specialization = specialization;
            MaximumHealth = maximumHealth;
            IsHumanControlled = isHumanControlled;
            ApplyClassOpeningState = applyClassOpeningState;
        }

        public ParticipantId Participant { get; }

        public TeamId Team { get; }

        public ClassId Class { get; }

        public SpecializationId Specialization { get; }

        public float MaximumHealth { get; }

        public bool IsHumanControlled { get; }

        public bool ApplyClassOpeningState { get; }

        public bool IsValid =>
            Participant.IsValid &&
            Team.Value > 0 &&
            SpecializationCatalog.IsCompatible(Class, Specialization) &&
            MaximumHealth > 0f &&
            !float.IsNaN(MaximumHealth) &&
            !float.IsInfinity(MaximumHealth);
    }

    /// <summary>
    /// Fixed-capacity, allocation-free party composition selected by Application.
    /// Unity binds these definitions but does not choose combat classes, health,
    /// teams, or player-only opening state.
    /// </summary>
    public readonly struct RunPartyComposition
    {
        private readonly RunPartyMemberDefinition _member0;
        private readonly RunPartyMemberDefinition _member1;
        private readonly RunPartyMemberDefinition _member2;
        private readonly RunPartyMemberDefinition _member3;

        internal RunPartyComposition(
            RunPartyMemberDefinition member0,
            RunPartyMemberDefinition member1,
            RunPartyMemberDefinition member2,
            RunPartyMemberDefinition member3)
        {
            _member0 = member0;
            _member1 = member1;
            _member2 = member2;
            _member3 = member3;
            Count = member3.IsValid
                ? 4
                : member2.IsValid
                    ? 3
                    : member1.IsValid
                        ? 2
                        : member0.IsValid
                            ? 1
                            : 0;
        }

        public int Count { get; }

        public bool IsValid
        {
            get
            {
                if (Count <= 0 || Count > RunPartyDefinition.MaximumMemberCount ||
                    !_member0.IsValid || !_member0.IsHumanControlled)
                {
                    return false;
                }

                for (int index = 1; index < Count; index++)
                {
                    RunPartyMemberDefinition member = GetMember(index);
                    if (!member.IsValid || member.IsHumanControlled)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public RunPartyMemberDefinition GetMember(int index)
        {
            if (index < 0 || index >= Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            switch (index)
            {
                case 0:
                    return _member0;
                case 1:
                    return _member1;
                case 2:
                    return _member2;
                case 3:
                    return _member3;
                default:
                    return _member3;
            }
        }
    }

    public static class RunPartyCompositionResolver
    {
        public const float PartyMemberMaximumHealth = 140f;

        private static readonly TeamId PartyTeamId = new TeamId(1);

        public static TeamId PartyTeam => PartyTeamId;

        public static RunPartyComposition Resolve(
            RunPartyDefinition party,
            ClassId selectedClass,
            SpecializationId selectedSpecialization)
        {
            if (!party.IsValid)
            {
                throw new ArgumentException(
                    "A cooperative run composition requires one to four stable party participants.",
                    nameof(party));
            }

            if (!IsPlayableClass(selectedClass) ||
                !SpecializationCatalog.IsCompatible(
                    selectedClass,
                    selectedSpecialization))
            {
                throw new ArgumentException(
                    "The selected human class and specialization must be playable and compatible.",
                    nameof(selectedSpecialization));
            }

            return new RunPartyComposition(
                Member(
                    party.GetMember(0),
                    selectedClass,
                    selectedSpecialization,
                    true),
                party.Count > 1
                    ? BotMember(party.GetMember(1), selectedClass, 0)
                    : default,
                party.Count > 2
                    ? BotMember(party.GetMember(2), selectedClass, 1)
                    : default,
                party.Count > 3
                    ? BotMember(party.GetMember(3), selectedClass, 2)
                    : default);
        }

        private static RunPartyMemberDefinition BotMember(
            ParticipantId participant,
            ClassId selectedClass,
            int teammateIndex)
        {
            ClassId classId = ResolveTeammateClass(
                selectedClass,
                teammateIndex);
            return Member(
                participant,
                classId,
                SpecializationCatalog.GetDefault(classId),
                false);
        }

        private static RunPartyMemberDefinition Member(
            ParticipantId participant,
            ClassId classId,
            SpecializationId specialization,
            bool isHumanControlled) =>
            new RunPartyMemberDefinition(
                participant,
                PartyTeamId,
                classId,
                specialization,
                PartyMemberMaximumHealth,
                isHumanControlled);

        private static ClassId ResolveTeammateClass(
            ClassId selectedClass,
            int teammateIndex)
        {
            int remainingIndex = teammateIndex;
            for (int rosterIndex = 0;
                rosterIndex < RunPartyDefinition.MaximumMemberCount;
                rosterIndex++)
            {
                ClassId candidate = GetRosterClass(rosterIndex);
                if (candidate == selectedClass)
                {
                    continue;
                }

                if (remainingIndex == 0)
                {
                    return candidate;
                }

                remainingIndex--;
            }

            throw new ArgumentOutOfRangeException(nameof(teammateIndex));
        }

        private static ClassId GetRosterClass(int index)
        {
            switch (index)
            {
                case 0:
                    return ClassId.Healer;
                case 1:
                    return ClassId.Rogue;
                case 2:
                    return ClassId.Mage;
                case 3:
                    return ClassId.Hunter;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static bool IsPlayableClass(ClassId classId) =>
            classId == ClassId.Healer ||
            classId == ClassId.Rogue ||
            classId == ClassId.Mage ||
            classId == ClassId.Hunter;
    }

    /// <summary>
    /// Four authored party placements carried by every cooperative encounter.
    /// The explicit marker keeps the all-zero default invalid while still
    /// permitting the world origin as an authored position.
    /// </summary>
    public readonly struct RunPartySpawnPositions
    {
        private readonly Float3 _position0;
        private readonly Float3 _position1;
        private readonly Float3 _position2;
        private readonly Float3 _position3;
        private readonly byte _isDefined;

        public RunPartySpawnPositions(
            Float3 position0,
            Float3 position1,
            Float3 position2,
            Float3 position3)
        {
            if (!IsFinite(position0) ||
                !IsFinite(position1) ||
                !IsFinite(position2) ||
                !IsFinite(position3))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(position0),
                    "Party spawn positions must be finite.");
            }

            _position0 = position0;
            _position1 = position1;
            _position2 = position2;
            _position3 = position3;
            _isDefined = 1;
        }

        public bool IsValid => _isDefined != 0;

        public Float3 GetPosition(int index)
        {
            switch (index)
            {
                case 0:
                    return _position0;
                case 1:
                    return _position1;
                case 2:
                    return _position2;
                case 3:
                    return _position3;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static bool IsFinite(Float3 value) =>
            !float.IsNaN(value.X) &&
            !float.IsInfinity(value.X) &&
            !float.IsNaN(value.Y) &&
            !float.IsInfinity(value.Y) &&
            !float.IsNaN(value.Z) &&
            !float.IsInfinity(value.Z);
    }

    /// <summary>
    /// Fixed-capacity encounter definition. The product contract permits four
    /// heroes plus at most four simultaneous hostiles; unused Core capacity stays
    /// reserved for companions and future non-participant state.
    /// </summary>
    public readonly struct EncounterDefinition
    {
        public const int MaximumSpawnCount = 4;

        private readonly RunSpawnDefinition _spawn0;
        private readonly RunSpawnDefinition _spawn1;
        private readonly RunSpawnDefinition _spawn2;
        private readonly RunSpawnDefinition _spawn3;
        private readonly RunSpawnDefinition _spawn4;
        private readonly RunSpawnDefinition _spawn5;
        private readonly RunSpawnDefinition _spawn6;
        private readonly RunSpawnDefinition _spawn7;

        public EncounterDefinition(
            EncounterId id,
            RunEncounterChallenge challenge,
            CombatRules rules,
            RunPartySpawnPositions partySpawnPositions,
            RunSpawnDefinition spawn0,
            RunSpawnDefinition spawn1 = default,
            RunSpawnDefinition spawn2 = default,
            RunSpawnDefinition spawn3 = default,
            RunSpawnDefinition spawn4 = default,
            RunSpawnDefinition spawn5 = default,
            RunSpawnDefinition spawn6 = default,
            RunSpawnDefinition spawn7 = default)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "An encounter requires a valid stable ID.",
                    nameof(id));
            }

            if (challenge < RunEncounterChallenge.Standard ||
                challenge > RunEncounterChallenge.Boss)
            {
                throw new ArgumentOutOfRangeException(nameof(challenge));
            }

            if (rules.TickRate != 30)
            {
                throw new ArgumentException(
                    "Run encounters must use the active deterministic 30 Hz contract.",
                    nameof(rules));
            }

            if (!partySpawnPositions.IsValid)
            {
                throw new ArgumentException(
                    "An encounter requires four authored party spawn positions.",
                    nameof(partySpawnPositions));
            }

            _spawn0 = spawn0;
            _spawn1 = spawn1;
            _spawn2 = spawn2;
            _spawn3 = spawn3;
            _spawn4 = spawn4;
            _spawn5 = spawn5;
            _spawn6 = spawn6;
            _spawn7 = spawn7;
            Id = id;
            Challenge = challenge;
            Rules = rules;
            PartySpawnPositions = partySpawnPositions;
            SpawnCount = 0;

            if (spawn4.IsValid || spawn5.IsValid || spawn6.IsValid ||
                spawn7.IsValid)
            {
                throw new ArgumentException(
                    "An encounter cannot contain more than four simultaneous hostiles.");
            }

            if (!spawn0.IsValid)
            {
                throw new ArgumentException(
                    "An encounter requires at least one valid spawn.",
                    nameof(spawn0));
            }

            bool foundGap = false;
            int spawnCount = 0;
            for (int index = 0; index < MaximumSpawnCount; index++)
            {
                RunSpawnDefinition spawn = GetSpawnUnchecked(index);
                if (!spawn.IsValid)
                {
                    foundGap = true;
                    continue;
                }

                if (foundGap)
                {
                    throw new ArgumentException(
                        "Encounter spawns must occupy contiguous slots.");
                }

                spawnCount++;
            }

            SpawnCount = spawnCount;

            for (int index = 0;
                index < RunPartyDefinition.MaximumMemberCount;
                index++)
            {
                Float3 partyPosition = partySpawnPositions.GetPosition(index);
                if (!rules.CollisionMap.IsPositionValid(partyPosition, 0f))
                {
                    throw new ArgumentException(
                        "A party spawn position is outside authoritative Core topology.",
                        nameof(partySpawnPositions));
                }
            }

            int minibossCount = 0;
            int bossCount = 0;
            for (int index = 0; index < SpawnCount; index++)
            {
                RunSpawnDefinition spawn = GetSpawnUnchecked(index);
                if (!rules.CollisionMap.IsPositionValid(spawn.Position, 0f))
                {
                    throw new ArgumentException(
                        "A hostile spawn position is outside authoritative Core topology.",
                        nameof(spawn0));
                }

                if (spawn.Rank == RunEnemyRank.Miniboss)
                {
                    minibossCount++;
                }
                else if (spawn.Rank == RunEnemyRank.Boss)
                {
                    bossCount++;
                }

                for (int otherIndex = 0; otherIndex < index; otherIndex++)
                {
                    RunSpawnDefinition other = GetSpawnUnchecked(otherIndex);
                    if (spawn.Id == other.Id)
                    {
                        throw new ArgumentException(
                            "Encounter spawn IDs must be unique.");
                    }

                    if (spawn.Participant == other.Participant)
                    {
                        throw new ArgumentException(
                            "Encounter combat participant IDs must be unique.");
                    }
                }
            }

            bool challengeMatchesRanks =
                (challenge == RunEncounterChallenge.Standard &&
                 minibossCount == 0 && bossCount == 0) ||
                (challenge == RunEncounterChallenge.Miniboss &&
                 minibossCount == 1 && bossCount == 0) ||
                (challenge == RunEncounterChallenge.Boss &&
                 minibossCount == 0 && bossCount == 1);
            if (!challengeMatchesRanks)
            {
                throw new ArgumentException(
                    "Encounter challenge must match exactly one miniboss or boss rank.",
                    nameof(challenge));
            }
        }

        public EncounterId Id { get; }

        public RunEncounterChallenge Challenge { get; }

        public CombatRules Rules { get; }

        public RunPartySpawnPositions PartySpawnPositions { get; }

        public EncounterKind Kind => Challenge == RunEncounterChallenge.Boss
            ? EncounterKind.Boss
            : EncounterKind.Combat;

        public int SpawnCount { get; }

        public bool IsValid =>
            Id.IsValid &&
            Rules.TickRate == 30 &&
            PartySpawnPositions.IsValid &&
            SpawnCount > 0;

        public Float3 GetPartySpawnPosition(int index) =>
            PartySpawnPositions.GetPosition(index);

        public RunSpawnDefinition GetSpawn(int index)
        {
            if (index < 0 || index >= SpawnCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetSpawnUnchecked(index);
        }

        public bool TryGetSpawnIndex(ParticipantId participant, out int index)
        {
            for (int candidateIndex = 0;
                candidateIndex < SpawnCount;
                candidateIndex++)
            {
                if (GetSpawnUnchecked(candidateIndex).Participant == participant)
                {
                    index = candidateIndex;
                    return true;
                }
            }

            index = -1;
            return false;
        }

        private RunSpawnDefinition GetSpawnUnchecked(int index)
        {
            switch (index)
            {
                case 0:
                    return _spawn0;
                case 1:
                    return _spawn1;
                case 2:
                    return _spawn2;
                case 3:
                    return _spawn3;
                case 4:
                    return _spawn4;
                case 5:
                    return _spawn5;
                case 6:
                    return _spawn6;
                default:
                    return _spawn7;
            }
        }
    }

    public readonly struct RunDefinition
    {
        public const int MaximumEncounterCount = 5;

        private readonly EncounterDefinition _encounter0;
        private readonly EncounterDefinition _encounter1;
        private readonly EncounterDefinition _encounter2;
        private readonly EncounterDefinition _encounter3;
        private readonly EncounterDefinition _encounter4;

        public RunDefinition(
            PlayableModeId mode,
            RunPartyDefinition party,
            ObjectiveId objective,
            EncounterDefinition encounter0,
            EncounterDefinition encounter1 = default,
            EncounterDefinition encounter2 = default,
            EncounterDefinition encounter3 = default,
            EncounterDefinition encounter4 = default)
        {
            if (mode < PlayableModeId.Arena || mode > PlayableModeId.Dungeon)
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            if (!party.IsValid)
            {
                throw new ArgumentException(
                    "A run definition requires a valid party.",
                    nameof(party));
            }

            _encounter0 = encounter0;
            _encounter1 = encounter1;
            _encounter2 = encounter2;
            _encounter3 = encounter3;
            _encounter4 = encounter4;
            Mode = mode;
            Party = party;
            Objective = objective;
            EncounterCount = 0;

            if (!encounter0.IsValid)
            {
                throw new ArgumentException(
                    "A run definition requires at least one encounter.",
                    nameof(encounter0));
            }

            bool foundGap = false;
            int encounterCount = 0;
            for (int index = 0; index < MaximumEncounterCount; index++)
            {
                EncounterDefinition encounter = GetEncounterUnchecked(index);
                if (!encounter.IsValid)
                {
                    foundGap = true;
                    continue;
                }

                if (foundGap)
                {
                    throw new ArgumentException(
                        "Run encounters must occupy contiguous slots.");
                }

                encounterCount++;
            }

            EncounterCount = encounterCount;

            ValidateParticipantBudgetAndStableIdentities();
        }

        public PlayableModeId Mode { get; }

        public RunPartyDefinition Party { get; }

        public ObjectiveId Objective { get; }

        public bool HasObjective => Objective.IsValid;

        public int EncounterCount { get; }

        public bool IsValid =>
            Mode != PlayableModeId.None &&
            Party.IsValid &&
            EncounterCount > 0;

        public EncounterDefinition GetEncounter(int index)
        {
            if (index < 0 || index >= EncounterCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return GetEncounterUnchecked(index);
        }

        private EncounterDefinition GetEncounterUnchecked(int index)
        {
            switch (index)
            {
                case 0:
                    return _encounter0;
                case 1:
                    return _encounter1;
                case 2:
                    return _encounter2;
                case 3:
                    return _encounter3;
                default:
                    return _encounter4;
            }
        }

        private void ValidateParticipantBudgetAndStableIdentities()
        {
            for (int encounterIndex = 0;
                encounterIndex < EncounterCount;
                encounterIndex++)
            {
                EncounterDefinition encounter =
                    GetEncounterUnchecked(encounterIndex);
                if (Party.Count + encounter.SpawnCount >
                    CombatSimulation.MaxCombatants)
                {
                    throw new ArgumentException(
                        "Party and encounter spawns exceed the Core combatant cap.");
                }

                for (int priorEncounterIndex = 0;
                    priorEncounterIndex < encounterIndex;
                    priorEncounterIndex++)
                {
                    if (encounter.Id ==
                        GetEncounterUnchecked(priorEncounterIndex).Id)
                    {
                        throw new ArgumentException(
                            "Encounter IDs must be unique within a run definition.");
                    }
                }

                for (int spawnIndex = 0;
                    spawnIndex < encounter.SpawnCount;
                    spawnIndex++)
                {
                    RunSpawnDefinition spawn = encounter.GetSpawn(spawnIndex);
                    if (spawn.Team == RunPartyCompositionResolver.PartyTeam)
                    {
                        throw new ArgumentException(
                            "Hostile spawns must not use the cooperative party team.");
                    }

                    if (Party.TryGetMemberIndex(spawn.Participant, out _))
                    {
                        throw new ArgumentException(
                            "Party and enemy combat participant IDs must not overlap.");
                    }

                    for (int priorEncounterIndex = 0;
                        priorEncounterIndex <= encounterIndex;
                        priorEncounterIndex++)
                    {
                        EncounterDefinition priorEncounter =
                            GetEncounterUnchecked(priorEncounterIndex);
                        int priorSpawnLimit = priorEncounterIndex == encounterIndex
                            ? spawnIndex
                            : priorEncounter.SpawnCount;
                        for (int priorSpawnIndex = 0;
                            priorSpawnIndex < priorSpawnLimit;
                            priorSpawnIndex++)
                        {
                            RunSpawnDefinition priorSpawn =
                                priorEncounter.GetSpawn(priorSpawnIndex);
                            if (spawn.Id == priorSpawn.Id)
                            {
                                throw new ArgumentException(
                                    "Spawn IDs must be unique within a run definition.");
                            }

                            if (spawn.Participant == priorSpawn.Participant)
                            {
                                throw new ArgumentException(
                                    "Enemy participant IDs must be unique across encounters.");
                            }
                        }
                    }
                }
            }
        }
    }

    public readonly struct RunState
    {
        internal RunState(
            RunId run,
            PlayableModeId mode,
            RunStatus status,
            int currentEncounterIndex,
            int encounterCount,
            int currentAttempt,
            int clearedEncounterCount,
            int livingPartyCount,
            int remainingSpawnCount,
            ObjectiveState objective,
            int encounterStartTick,
            int lastTransitionTick)
        {
            Run = run;
            Mode = mode;
            Status = status;
            CurrentEncounterIndex = currentEncounterIndex;
            EncounterCount = encounterCount;
            CurrentAttempt = currentAttempt;
            ClearedEncounterCount = clearedEncounterCount;
            LivingPartyCount = livingPartyCount;
            RemainingSpawnCount = remainingSpawnCount;
            Objective = objective;
            EncounterStartTick = encounterStartTick;
            LastTransitionTick = lastTransitionTick;
        }

        public RunId Run { get; }

        public PlayableModeId Mode { get; }

        public RunStatus Status { get; }

        public int CurrentEncounterIndex { get; }

        public int CurrentEncounterNumber =>
            CurrentEncounterIndex >= 0 ? CurrentEncounterIndex + 1 : 0;

        public int EncounterCount { get; }

        public int CurrentAttempt { get; }

        public int CurrentRetryCount =>
            CurrentAttempt > 0 ? CurrentAttempt - 1 : 0;

        public int ClearedEncounterCount { get; }

        public int LivingPartyCount { get; }

        public int RemainingSpawnCount { get; }

        public ObjectiveState Objective { get; }

        public int EncounterStartTick { get; }

        public int LastTransitionTick { get; }

        public bool HasCurrentEncounter => CurrentEncounterIndex >= 0;

        public bool IsTerminal =>
            Status == RunStatus.Completed || Status == RunStatus.Failed;
    }

    public readonly struct RunResult
    {
        internal RunResult(
            RunId run,
            PlayableModeId mode,
            RunOutcome outcome,
            RunEndReason reason,
            int terminalTick,
            EncounterId terminalEncounter,
            int clearedEncounterCount)
        {
            Run = run;
            Mode = mode;
            Outcome = outcome;
            Reason = reason;
            TerminalTick = terminalTick;
            TerminalEncounter = terminalEncounter;
            ClearedEncounterCount = clearedEncounterCount;
        }

        public RunId Run { get; }

        public PlayableModeId Mode { get; }

        public RunOutcome Outcome { get; }

        public RunEndReason Reason { get; }

        public int TerminalTick { get; }

        public EncounterId TerminalEncounter { get; }

        public int ClearedEncounterCount { get; }

        public bool IsTerminal => Outcome != RunOutcome.None;

        public bool DidWin => Outcome == RunOutcome.Victory;
    }

    public readonly struct EncounterResult
    {
        internal EncounterResult(
            bool wasHandled,
            SpawnId defeatedSpawn,
            ParticipantId defeatedPartyMember,
            bool encounterCleared,
            bool partyDefeated,
            bool retryAvailable,
            bool runCompleted,
            bool runFailed)
        {
            WasHandled = wasHandled;
            DefeatedSpawn = defeatedSpawn;
            DefeatedPartyMember = defeatedPartyMember;
            EncounterCleared = encounterCleared;
            PartyDefeated = partyDefeated;
            RetryAvailable = retryAvailable;
            RunCompleted = runCompleted;
            RunFailed = runFailed;
        }

        public bool WasHandled { get; }

        public SpawnId DefeatedSpawn { get; }

        public bool SpawnDefeated => DefeatedSpawn.IsValid;

        public ParticipantId DefeatedPartyMember { get; }

        public bool PartyMemberDefeated => DefeatedPartyMember.IsValid;

        public bool EncounterCleared { get; }

        public bool PartyDefeated { get; }

        public bool RetryAvailable { get; }

        public bool RunCompleted { get; }

        public bool RunFailed { get; }
    }

    public readonly struct ObjectiveDamageObservation
    {
        internal ObjectiveDamageObservation(
            bool wasHandled,
            float appliedDamage,
            ObjectiveState objective,
            bool runFailed)
        {
            WasHandled = wasHandled;
            AppliedDamage = appliedDamage;
            Objective = objective;
            RunFailed = runFailed;
        }

        public bool WasHandled { get; }

        public float AppliedDamage { get; }

        public ObjectiveState Objective { get; }

        public bool ObjectiveDefeated => Objective.IsDefeated;

        public bool RunFailed { get; }
    }

    public interface IRunController
    {
        RunId Run { get; }

        RunDefinition Definition { get; }

        RunState State { get; }

        RunResult Result { get; }

        bool TryStartNextEncounter(
            int currentTick,
            out EncounterDefinition encounter);

        EncounterResult ObserveCombatEvent(CombatEvent combatEvent);

        bool TryAbandon(int currentTick);
    }

    /// <summary>
    /// Shared fixed-mask state machine used by local sequential PvE modes. It
    /// performs no managed allocation after construction.
    /// </summary>
    internal sealed class SequentialRunRuntime
    {
        private readonly RunId _run;
        private readonly RunDefinition _definition;
        private readonly bool _partyDefeatIsRetryable;
        private RunStatus _status;
        private int _currentEncounterIndex;
        private int _currentAttempt;
        private int _clearedEncounterCount;
        private int _encounterStartTick;
        private int _lastTransitionTick;
        private byte _livingPartyMask;
        private byte _remainingSpawnMask;
        private ObjectiveState _objective;
        private RunResult _result;

        public SequentialRunRuntime(
            RunId run,
            RunDefinition definition,
            bool partyDefeatIsRetryable,
            ObjectiveState objective = default)
        {
            if (!run.IsValid)
            {
                throw new ArgumentException(
                    "A run controller requires a valid run ID.",
                    nameof(run));
            }

            if (!definition.IsValid)
            {
                throw new ArgumentException(
                    "A run controller requires a valid definition.",
                    nameof(definition));
            }

            if (definition.HasObjective != objective.IsValid ||
                (definition.HasObjective && definition.Objective != objective.Id))
            {
                throw new ArgumentException(
                    "Objective state must match the run definition.",
                    nameof(objective));
            }

            _run = run;
            _definition = definition;
            _partyDefeatIsRetryable = partyDefeatIsRetryable;
            _status = RunStatus.Ready;
            _currentEncounterIndex = -1;
            _currentAttempt = 0;
            _clearedEncounterCount = 0;
            _encounterStartTick = -1;
            _lastTransitionTick = -1;
            _livingPartyMask = MaskForCount(definition.Party.Count);
            _remainingSpawnMask = 0;
            _objective = objective;
            _result = new RunResult(
                run,
                definition.Mode,
                RunOutcome.None,
                RunEndReason.None,
                -1,
                default,
                0);
        }

        public RunId Run => _run;

        public RunDefinition Definition => _definition;

        public RunResult Result => _result;

        public RunState State => new RunState(
            _run,
            _definition.Mode,
            _status,
            _currentEncounterIndex,
            _definition.EncounterCount,
            _currentAttempt,
            _clearedEncounterCount,
            CountBits(_livingPartyMask),
            CountBits(_remainingSpawnMask),
            _objective,
            _encounterStartTick,
            _lastTransitionTick);

        public bool TryStartNextEncounter(
            int currentTick,
            out EncounterDefinition encounter)
        {
            bool mayStart = _status == RunStatus.Ready ||
                _status == RunStatus.AwaitingAdvance;
            if (!mayStart || !CanCrossBoundaryAt(currentTick))
            {
                encounter = default;
                return false;
            }

            int nextEncounterIndex = _status == RunStatus.Ready
                ? 0
                : _currentEncounterIndex + 1;
            if (nextEncounterIndex < 0 ||
                nextEncounterIndex >= _definition.EncounterCount)
            {
                encounter = default;
                return false;
            }

            _currentEncounterIndex = nextEncounterIndex;
            _currentAttempt = 1;
            StartCurrentEncounter(currentTick);
            encounter = _definition.GetEncounter(_currentEncounterIndex);
            return true;
        }

        public bool TryRetryCurrentEncounter(
            int currentTick,
            out EncounterDefinition encounter)
        {
            if (_status != RunStatus.AwaitingRetry ||
                !CanCrossBoundaryAt(currentTick) ||
                _currentEncounterIndex < 0)
            {
                encounter = default;
                return false;
            }

            if (_currentAttempt < int.MaxValue)
            {
                _currentAttempt++;
            }

            StartCurrentEncounter(currentTick);
            encounter = _definition.GetEncounter(_currentEncounterIndex);
            return true;
        }

        public EncounterResult ObserveCombatEvent(CombatEvent combatEvent)
        {
            if (_status != RunStatus.EncounterActive ||
                combatEvent.Kind != CombatEventKind.Death ||
                combatEvent.Tick < _encounterStartTick)
            {
                return default;
            }

            if (_definition.Party.TryGetMemberIndex(
                combatEvent.Target,
                out int partyIndex))
            {
                byte partyBit = (byte)(1 << partyIndex);
                if ((_livingPartyMask & partyBit) == 0)
                {
                    return default;
                }

                _livingPartyMask &= (byte)~partyBit;
                bool partyDefeated = _livingPartyMask == 0;
                bool retryAvailable = false;
                bool runFailed = false;
                if (partyDefeated)
                {
                    _remainingSpawnMask = 0;
                    _encounterStartTick = -1;
                    _lastTransitionTick = combatEvent.Tick;
                    if (_partyDefeatIsRetryable)
                    {
                        _status = RunStatus.AwaitingRetry;
                        retryAvailable = true;
                    }
                    else
                    {
                        EndRun(
                            RunOutcome.Defeat,
                            RunEndReason.PartyDefeated,
                            combatEvent.Tick);
                        runFailed = true;
                    }
                }

                return new EncounterResult(
                    true,
                    default,
                    combatEvent.Target,
                    false,
                    partyDefeated,
                    retryAvailable,
                    false,
                    runFailed);
            }

            EncounterDefinition encounter =
                _definition.GetEncounter(_currentEncounterIndex);
            if (!encounter.TryGetSpawnIndex(
                combatEvent.Target,
                out int spawnIndex))
            {
                return default;
            }

            byte spawnBit = (byte)(1 << spawnIndex);
            if ((_remainingSpawnMask & spawnBit) == 0)
            {
                return default;
            }

            _remainingSpawnMask &= (byte)~spawnBit;
            bool encounterCleared = _remainingSpawnMask == 0;
            bool runCompleted = false;
            if (encounterCleared)
            {
                _clearedEncounterCount++;
                _encounterStartTick = -1;
                _lastTransitionTick = combatEvent.Tick;
                if (_clearedEncounterCount >= _definition.EncounterCount)
                {
                    EndRun(
                        RunOutcome.Victory,
                        RunEndReason.AllEncountersCleared,
                        combatEvent.Tick);
                    runCompleted = true;
                }
                else
                {
                    _status = RunStatus.AwaitingAdvance;
                }
            }

            return new EncounterResult(
                true,
                encounter.GetSpawn(spawnIndex).Id,
                default,
                encounterCleared,
                false,
                false,
                runCompleted,
                false);
        }

        public ObjectiveDamageObservation ApplyObjectiveDamage(
            int currentTick,
            float requestedDamage)
        {
            if (!_objective.IsValid ||
                _status != RunStatus.EncounterActive ||
                currentTick < _encounterStartTick)
            {
                return default;
            }

            float appliedDamage = _objective.ApplyDamage(requestedDamage);
            if (appliedDamage <= 0f)
            {
                return default;
            }

            bool runFailed = _objective.IsDefeated;
            if (runFailed)
            {
                EndRun(
                    RunOutcome.Defeat,
                    RunEndReason.ObjectiveDefeated,
                    currentTick);
            }

            return new ObjectiveDamageObservation(
                true,
                appliedDamage,
                _objective,
                runFailed);
        }

        public bool TryAbandon(int currentTick)
        {
            if (_status == RunStatus.Completed ||
                _status == RunStatus.Failed ||
                currentTick < 0 ||
                currentTick < _lastTransitionTick)
            {
                return false;
            }

            EndRun(
                RunOutcome.Defeat,
                RunEndReason.Abandoned,
                currentTick);
            return true;
        }

        private void StartCurrentEncounter(int currentTick)
        {
            EncounterDefinition encounter =
                _definition.GetEncounter(_currentEncounterIndex);
            _livingPartyMask = MaskForCount(_definition.Party.Count);
            _remainingSpawnMask = MaskForCount(encounter.SpawnCount);
            _encounterStartTick = currentTick;
            _lastTransitionTick = currentTick;
            _status = RunStatus.EncounterActive;
        }

        private bool CanCrossBoundaryAt(int currentTick) =>
            currentTick >= 0 &&
            (_lastTransitionTick < 0 || currentTick > _lastTransitionTick);

        private void EndRun(
            RunOutcome outcome,
            RunEndReason reason,
            int terminalTick)
        {
            _status = outcome == RunOutcome.Victory
                ? RunStatus.Completed
                : RunStatus.Failed;
            _remainingSpawnMask = 0;
            _encounterStartTick = -1;
            _lastTransitionTick = terminalTick;
            EncounterId encounter = _currentEncounterIndex >= 0
                ? _definition.GetEncounter(_currentEncounterIndex).Id
                : default;
            _result = new RunResult(
                _run,
                _definition.Mode,
                outcome,
                reason,
                terminalTick,
                encounter,
                _clearedEncounterCount);
        }

        private static byte MaskForCount(int count) =>
            count >= 8 ? byte.MaxValue : (byte)((1 << count) - 1);

        private static int CountBits(byte mask)
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
