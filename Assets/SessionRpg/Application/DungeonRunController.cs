using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    public enum DungeonFloorKind
    {
        None = 0,
        TraversalPack = 1,
        EliteArena = 2,
        MechanicsEncounter = 3,
        RiftWardenBoss = 4,
    }

    /// <summary>
    /// Stable four-floor local dungeon skeleton for a four-hero party.
    /// </summary>
    public static class DungeonRunCatalog
    {
        public const int HeroCount = 4;
        public const int FloorCount = 4;
        public const int MaximumActiveHostileCount = 4;

        private static readonly SpawnId RiftWardenSpawnId =
            new SpawnId(2404u);

        private static readonly RunPartySpawnPositions TraversalPartySpawns =
            new RunPartySpawnPositions(
                new Float3(-178f, 0f, ArenaCollisionMap.LevelingCenterZ - 2.625f),
                new Float3(-176.5f, 0f, ArenaCollisionMap.LevelingCenterZ - 0.875f),
                new Float3(-178f, 0f, ArenaCollisionMap.LevelingCenterZ + 0.875f),
                new Float3(-176.5f, 0f, ArenaCollisionMap.LevelingCenterZ + 2.625f));

        private static readonly RunPartySpawnPositions ArenaPartySpawns =
            new RunPartySpawnPositions(
                new Float3(-5.5f, 0f, -2.625f),
                new Float3(-4.1f, 0f, -0.875f),
                new Float3(-5.5f, 0f, 0.875f),
                new Float3(-4.1f, 0f, 2.625f));

        private static readonly RunDefinition DungeonDefinition =
            new RunDefinition(
                PlayableModeId.Dungeon,
                new RunPartyDefinition(
                    new ParticipantId(1),
                    new ParticipantId(2),
                    new ParticipantId(3),
                    new ParticipantId(4)),
                default,
                new EncounterDefinition(
                    new EncounterId(2001u),
                    RunEncounterChallenge.Standard,
                    CombatRules.DungeonTraversalDefault,
                    TraversalPartySpawns,
                    Spawn(2101u, 201, RunEnemyRank.Standard, RunEnemyTier.Medium, 0, true),
                    Spawn(2102u, 202, RunEnemyRank.Standard, RunEnemyTier.Medium, 1, true),
                    Spawn(2103u, 203, RunEnemyRank.Standard, RunEnemyTier.Medium, 2, true)),
                new EncounterDefinition(
                    new EncounterId(2002u),
                    RunEncounterChallenge.Standard,
                    CombatRules.DungeonArenaDefault,
                    ArenaPartySpawns,
                    Spawn(2201u, 204, RunEnemyRank.Elite, RunEnemyTier.Medium, 0, false),
                    Spawn(2202u, 205, RunEnemyRank.Elite, RunEnemyTier.Medium, 1, false),
                    Spawn(2203u, 206, RunEnemyRank.Elite, RunEnemyTier.Medium, 2, false),
                    Spawn(2204u, 207, RunEnemyRank.Elite, RunEnemyTier.Medium, 3, false)),
                new EncounterDefinition(
                    new EncounterId(2003u),
                    RunEncounterChallenge.Miniboss,
                    CombatRules.DungeonArenaDefault,
                    ArenaPartySpawns,
                    Spawn(2301u, 208, RunEnemyRank.Standard, RunEnemyTier.Medium, 0, false),
                    Spawn(2302u, 209, RunEnemyRank.Standard, RunEnemyTier.Medium, 1, false),
                    Spawn(2303u, 210, RunEnemyRank.Elite, RunEnemyTier.Medium, 2, false),
                    Spawn(2304u, 211, RunEnemyRank.Miniboss, RunEnemyTier.Boss, 3, false)),
                new EncounterDefinition(
                    new EncounterId(2004u),
                    RunEncounterChallenge.Boss,
                    CombatRules.DungeonBossDefault,
                    ArenaPartySpawns,
                    Spawn(2401u, 212, RunEnemyRank.Standard, RunEnemyTier.Medium, 0, false),
                    Spawn(2402u, 213, RunEnemyRank.Elite, RunEnemyTier.Medium, 1, false),
                    Spawn(2403u, 214, RunEnemyRank.Elite, RunEnemyTier.Medium, 2, false),
                    Spawn(2404u, 215, RunEnemyRank.Boss, RunEnemyTier.Boss, 3, false)));

        public static RunDefinition Definition => DungeonDefinition;

        public static SpawnId RiftWardenSpawn => RiftWardenSpawnId;

        public static DungeonFloorKind GetFloorKind(int index)
        {
            switch (index)
            {
                case 0:
                    return DungeonFloorKind.TraversalPack;
                case 1:
                    return DungeonFloorKind.EliteArena;
                case 2:
                    return DungeonFloorKind.MechanicsEncounter;
                case 3:
                    return DungeonFloorKind.RiftWardenBoss;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static RunSpawnDefinition Spawn(
            uint id,
            int participant,
            RunEnemyRank rank,
            RunEnemyTier tier,
            int spawnIndex,
            bool traversal)
        {
            ClassId classId;
            SpecializationId specialization;
            float maximumHealth;
            if (rank == RunEnemyRank.Boss ||
                rank == RunEnemyRank.Miniboss)
            {
                classId = ClassId.Boss;
                specialization = SpecializationId.Boss;
                maximumHealth = rank == RunEnemyRank.Boss ? 520f : 300f;
            }
            else if (rank == RunEnemyRank.Elite)
            {
                classId = ClassId.Mage;
                specialization = SpecializationId.Frost;
                maximumHealth = 175f;
            }
            else
            {
                classId = GetStandardEnemyClass(spawnIndex);
                specialization = SpecializationCatalog.GetDefault(classId);
                maximumHealth = 105f;
            }

            return new RunSpawnDefinition(
                new SpawnId(id),
                new ParticipantId(participant),
                rank,
                tier,
                new TeamId(2),
                classId,
                specialization,
                maximumHealth,
                GetHostilePosition(spawnIndex, traversal));
        }

        private static ClassId GetStandardEnemyClass(int index)
        {
            switch (index % 3)
            {
                case 0:
                    return ClassId.Rogue;
                case 1:
                    return ClassId.Mage;
                default:
                    return ClassId.Hunter;
            }
        }

        private static Float3 GetHostilePosition(int index, bool traversal)
        {
            float lateral = (index - 1.5f) * 2.2f;
            return traversal
                ? new Float3(
                    -160f + ((index % 2) * 1.5f),
                    0f,
                    ArenaCollisionMap.LevelingCenterZ + lateral)
                : new Float3(
                    4.5f + ((index % 2) * 1.7f),
                    0f,
                    lateral);
        }
    }

    public sealed class DungeonRunController : IRunController
    {
        private readonly SequentialRunRuntime _runtime;

        public DungeonRunController()
            : this(new RunId(1u))
        {
        }

        public DungeonRunController(RunId run)
        {
            _runtime = new SequentialRunRuntime(
                run,
                DungeonRunCatalog.Definition,
                true);
        }

        public RunId Run => _runtime.Run;

        public RunDefinition Definition => _runtime.Definition;

        public RunState State => _runtime.State;

        public RunResult Result => _runtime.Result;

        public ParticipantId Hero => GetHero(0);

        public ParticipantId GetHero(int index) =>
            Definition.Party.GetMember(index);

        public bool TryStartFirstFloor(
            int currentTick,
            out EncounterDefinition floor)
        {
            if (State.Status != RunStatus.Ready)
            {
                floor = default;
                return false;
            }

            return _runtime.TryStartNextEncounter(currentTick, out floor);
        }

        public bool TryAdvanceToNextFloor(
            int currentTick,
            out EncounterDefinition floor)
        {
            if (State.Status != RunStatus.AwaitingAdvance)
            {
                floor = default;
                return false;
            }

            return _runtime.TryStartNextEncounter(currentTick, out floor);
        }

        public bool TryRetryCurrentFloor(
            int currentTick,
            out EncounterDefinition floor)
        {
            return _runtime.TryRetryCurrentEncounter(currentTick, out floor);
        }

        public bool TryStartNextEncounter(
            int currentTick,
            out EncounterDefinition encounter)
        {
            return _runtime.TryStartNextEncounter(currentTick, out encounter);
        }

        public EncounterResult ObserveCombatEvent(CombatEvent combatEvent) =>
            _runtime.ObserveCombatEvent(combatEvent);

        public bool TryAbandon(int currentTick) =>
            _runtime.TryAbandon(currentTick);
    }
}
