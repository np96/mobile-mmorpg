using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    public enum LevelingZoneId
    {
        None = 0,
        AshenCauseway = 1,
    }

    public enum LevelingMobArchetypeId
    {
        None = 0,
        AshMelee = 1,
        AshRanged = 2,
        AshBrute = 3,
        AshCaster = 4,
        AshElite = 5,
        RimeFrostweaver = 6,
        ReliquaryWarder = 7,
        RiftWarden = 8,
    }

    public enum LevelingDistrictId
    {
        None = 0,
        WestRefuge = 1,
        CinderApproach = 2,
        BrokenGate = 3,
        CollapsedCourt = 4,
        OssuaryBend = 5,
        ReliquaryYard = 6,
        EastWatch = 7,
    }

    public readonly struct LevelingDistrictDefinition
    {
        internal LevelingDistrictDefinition(
            LevelingDistrictId id,
            int chunkX,
            string displayName)
        {
            Id = id;
            Coordinate = new WorldChunkCoordinate(chunkX, 0);
            DisplayName = displayName ?? string.Empty;
        }

        public LevelingDistrictId Id { get; }

        public WorldChunkCoordinate Coordinate { get; }

        public string DisplayName { get; }
    }

    /// <summary>
    /// Stable gameplay-facing district identities for the seven authored world
    /// chunks. This is navigation/UI metadata only; generated Unity scene names
    /// and landmark objects never activate encounters or decide movement.
    /// </summary>
    public static class LevelingDistrictCatalog
    {
        public const int Count = 7;

        private static readonly LevelingDistrictDefinition WestRefuge =
            District(LevelingDistrictId.WestRefuge, -3, "WEST REFUGE");
        private static readonly LevelingDistrictDefinition CinderApproach =
            District(LevelingDistrictId.CinderApproach, -2, "CINDER APPROACH");
        private static readonly LevelingDistrictDefinition BrokenGate =
            District(LevelingDistrictId.BrokenGate, -1, "BROKEN GATE");
        private static readonly LevelingDistrictDefinition CollapsedCourt =
            District(LevelingDistrictId.CollapsedCourt, 0, "COLLAPSED COURT");
        private static readonly LevelingDistrictDefinition OssuaryBend =
            District(LevelingDistrictId.OssuaryBend, 1, "OSSUARY BEND");
        private static readonly LevelingDistrictDefinition ReliquaryYard =
            District(LevelingDistrictId.ReliquaryYard, 2, "RELIQUARY YARD");
        private static readonly LevelingDistrictDefinition EastWatch =
            District(LevelingDistrictId.EastWatch, 3, "EAST WATCH");

        public static LevelingDistrictDefinition GetByIndex(int index)
        {
            switch (index)
            {
                case 0:
                    return WestRefuge;
                case 1:
                    return CinderApproach;
                case 2:
                    return BrokenGate;
                case 3:
                    return CollapsedCourt;
                case 4:
                    return OssuaryBend;
                case 5:
                    return ReliquaryYard;
                case 6:
                    return EastWatch;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public static LevelingDistrictDefinition GetAtWorldX(float worldX)
        {
            int chunkX = (int)Math.Floor(worldX / WorldPosition.ChunkSizeMeters);
            if (chunkX < -3)
            {
                chunkX = -3;
            }
            else if (chunkX > 3)
            {
                chunkX = 3;
            }

            return GetByIndex(chunkX + 3);
        }

        private static LevelingDistrictDefinition District(
            LevelingDistrictId id,
            int chunkX,
            string displayName)
        {
            return new LevelingDistrictDefinition(id, chunkX, displayName);
        }
    }

    public enum LevelingRunStatus
    {
        AwaitingPackActivation = 0,
        FightingPack = 1,
        Completed = 2,
        Failed = 3,
    }

    public readonly struct LevelingMobDefinition
    {
        internal LevelingMobDefinition(
            ParticipantId participant,
            LevelingMobArchetypeId archetype,
            int level,
            float maximumHealth,
            int experienceReward)
        {
            Participant = participant;
            Archetype = archetype;
            Level = level;
            MaximumHealth = maximumHealth;
            ExperienceReward = experienceReward;
        }

        public ParticipantId Participant { get; }

        public LevelingMobArchetypeId Archetype { get; }

        public int Level { get; }

        public float MaximumHealth { get; }

        public float BaseMaximumHealth => MaximumHealth -
            ((Level - LevelProgressionCurve.MinimumLevel) *
             LevelProgressionCurve.HealthPerGainedLevel);

        public int ExperienceReward { get; }
    }

    public readonly struct LevelingPackDefinition
    {
        private readonly LevelingMobDefinition _mob0;
        private readonly LevelingMobDefinition _mob1;
        private readonly LevelingMobDefinition _mob2;

        internal LevelingPackDefinition(
            int index,
            float activationX,
            float encounterX,
            LevelingMobDefinition mob0,
            LevelingMobDefinition mob1 = default,
            LevelingMobDefinition mob2 = default)
        {
            Index = index;
            ActivationX = activationX;
            EncounterX = encounterX;
            _mob0 = mob0;
            _mob1 = mob1;
            _mob2 = mob2;
            MobCount = mob2.Participant.IsValid
                ? 3
                : mob1.Participant.IsValid
                    ? 2
                    : mob0.Participant.IsValid
                        ? 1
                        : 0;
        }

        public int Index { get; }

        public int Number => Index + 1;

        public float ActivationX { get; }

        /// <summary>
        /// Authoritative zone-space center used to place this encounter. Keeping
        /// it beside the activation gate prevents Unity presentation code from
        /// becoming the source of gameplay spawn positions.
        /// </summary>
        public float EncounterX { get; }

        public int MobCount { get; }

        public LevelingMobDefinition GetMob(int index)
        {
            if (index < 0 || index >= MobCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            switch (index)
            {
                case 0:
                    return _mob0;
                case 1:
                    return _mob1;
                default:
                    return _mob2;
            }
        }

        public bool TryGetMob(
            ParticipantId participant,
            out LevelingMobDefinition mob)
        {
            for (int index = 0; index < MobCount; index++)
            {
                LevelingMobDefinition candidate = GetMob(index);
                if (candidate.Participant == participant)
                {
                    mob = candidate;
                    return true;
                }
            }

            mob = default;
            return false;
        }
    }

    public readonly struct LevelingZoneDefinition
    {
        public const int MaximumPackCount = 5;
        public const int MaximumActiveMobCount = 3;

        private readonly LevelingPackDefinition _pack0;
        private readonly LevelingPackDefinition _pack1;
        private readonly LevelingPackDefinition _pack2;
        private readonly LevelingPackDefinition _pack3;
        private readonly LevelingPackDefinition _pack4;

        internal LevelingZoneDefinition(
            WorldId world,
            ZoneId contentZone,
            LevelingZoneId id,
            LevelingPackDefinition pack0,
            LevelingPackDefinition pack1,
            LevelingPackDefinition pack2,
            LevelingPackDefinition pack3,
            LevelingPackDefinition pack4)
        {
            if (!world.IsValid || !contentZone.IsValid)
            {
                throw new ArgumentException(
                    "A leveling zone requires valid world-content identity.");
            }

            World = world;
            ContentZone = contentZone;
            Id = id;
            _pack0 = pack0;
            _pack1 = pack1;
            _pack2 = pack2;
            _pack3 = pack3;
            _pack4 = pack4;
        }

        public LevelingZoneId Id { get; }

        public WorldId World { get; }

        public ZoneId ContentZone { get; }

        public int PackCount => MaximumPackCount;

        public LevelingPackDefinition GetPack(int index)
        {
            switch (index)
            {
                case 0:
                    return _pack0;
                case 1:
                    return _pack1;
                case 2:
                    return _pack2;
                case 3:
                    return _pack3;
                case 4:
                    return _pack4;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }

        public bool TryGetMob(
            ParticipantId participant,
            out LevelingMobDefinition mob)
        {
            for (int packIndex = 0; packIndex < PackCount; packIndex++)
            {
                if (GetPack(packIndex).TryGetMob(participant, out mob))
                {
                    return true;
                }
            }

            mob = default;
            return false;
        }
    }

    public static class LevelingZoneCatalog
    {
        private static readonly LevelingZoneDefinition AshenCausewayDefinition =
            new LevelingZoneDefinition(
                new WorldId(1u),
                new ZoneId(1u),
                LevelingZoneId.AshenCauseway,
                new LevelingPackDefinition(
                    0,
                    -184f,
                    -162f,
                    Mob(2, LevelingMobArchetypeId.AshMelee, 1, 52f, 40),
                    Mob(3, LevelingMobArchetypeId.AshRanged, 1, 52f, 40)),
                new LevelingPackDefinition(
                    1,
                    -120f,
                    -98f,
                    Mob(4, LevelingMobArchetypeId.AshElite, 2, 68f, 60),
                    Mob(5, LevelingMobArchetypeId.RimeFrostweaver, 2, 58f, 60)),
                new LevelingPackDefinition(
                    2,
                    -56f,
                    -34f,
                    Mob(6, LevelingMobArchetypeId.AshBrute, 3, 92f, 90),
                    Mob(7, LevelingMobArchetypeId.ReliquaryWarder, 3, 92f, 90)),
                new LevelingPackDefinition(
                    3,
                    76f,
                    98f,
                    Mob(8, LevelingMobArchetypeId.AshMelee, 4, 82f, 100),
                    Mob(9, LevelingMobArchetypeId.AshRanged, 4, 82f, 100),
                    Mob(10, LevelingMobArchetypeId.AshCaster, 4, 72f, 100)),
                new LevelingPackDefinition(
                    4,
                    184f,
                    206f,
                    Mob(11, LevelingMobArchetypeId.RiftWarden, 5, 260f, 450)));

        public static LevelingZoneDefinition AshenCauseway =>
            AshenCausewayDefinition;

        public static LevelingZoneDefinition Get(LevelingZoneId id)
        {
            if (id != LevelingZoneId.AshenCauseway)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(id),
                    id,
                    "Unknown leveling zone ID.");
            }

            return AshenCausewayDefinition;
        }

        private static LevelingMobDefinition Mob(
            int participant,
            LevelingMobArchetypeId archetype,
            int level,
            float maximumHealth,
            int experienceReward)
        {
            return new LevelingMobDefinition(
                new ParticipantId(participant),
                archetype,
                level,
                maximumHealth,
                experienceReward);
        }
    }

    public readonly struct LevelingCombatObservation
    {
        internal LevelingCombatObservation(
            bool wasHandled,
            ParticipantId defeatedMob,
            int experienceAwarded,
            LevelProgressionGrantResult progressionGrant,
            bool packCleared,
            bool runCompleted,
            bool runFailed)
        {
            WasHandled = wasHandled;
            DefeatedMob = defeatedMob;
            ExperienceAwarded = experienceAwarded;
            ProgressionGrant = progressionGrant;
            PackCleared = packCleared;
            RunCompleted = runCompleted;
            RunFailed = runFailed;
        }

        public bool WasHandled { get; }

        public ParticipantId DefeatedMob { get; }

        public int ExperienceAwarded { get; }

        public bool AwardedExperience => ExperienceAwarded > 0;

        public LevelProgressionGrantResult ProgressionGrant { get; }

        public bool PackCleared { get; }

        public bool RunCompleted { get; }

        public bool RunFailed { get; }
    }

    /// <summary>
    /// Pure deterministic mode controller for the first five-pack leveling run.
    /// It consumes authoritative combat death events, owns XP and pack cadence,
    /// and exposes immutable definitions that Unity can bind to pooled views.
    /// </summary>
    public sealed class LevelingRunController
    {
        public const int PlayerParticipantValue = 1;
        public const int InterPackDelayTicks = 45;

        private readonly LevelingZoneDefinition _zone;
        private LevelProgressionState _progression;
        private ushort _registeredMobMask;
        private ushort _resolvedMobMask;
        private ParticipantId _activeMob0;
        private ParticipantId _activeMob1;
        private ParticipantId _activeMob2;
        private int _activeMobCount;
        private int _activePackIndex = -1;
        private int _activePackActivationTick = -1;
        private int _nextPackIndex;
        private int _nextPackReadyTick;
        private int _terminalTick = -1;

        public LevelingRunController()
            : this(LevelingZoneCatalog.AshenCauseway)
        {
        }

        public LevelingRunController(LevelingZoneDefinition zone)
        {
            if (zone.Id == LevelingZoneId.None || zone.PackCount <= 0)
            {
                throw new ArgumentException(
                    "A leveling run requires a valid zone definition.",
                    nameof(zone));
            }

            _zone = zone;
            _progression = new LevelProgressionState();
            Status = LevelingRunStatus.AwaitingPackActivation;
        }

        public ParticipantId Player => new ParticipantId(PlayerParticipantValue);

        public LevelingZoneDefinition Zone => _zone;

        public LevelingRunStatus Status { get; private set; }

        public LevelProgressionState Progression => _progression;

        public int ActivePackIndex => _activePackIndex;

        public int ActivePackActivationTick => _activePackActivationTick;

        public int NextPackIndex => _nextPackIndex;

        public int NextPackReadyTick => _nextPackReadyTick;

        public int ActiveMobCount => _activeMobCount;

        public bool IsCompleted => Status == LevelingRunStatus.Completed;

        public bool IsFailed => Status == LevelingRunStatus.Failed;

        public bool HasNextPack =>
            !IsCompleted && !IsFailed && _nextPackIndex < _zone.PackCount;

        public int GetTicksUntilNextPackReady(int currentTick)
        {
            if (!HasNextPack || Status == LevelingRunStatus.FightingPack)
            {
                return 0;
            }

            return Math.Max(0, _nextPackReadyTick - Math.Max(0, currentTick));
        }

        public bool IsNextPackReady(int currentTick)
        {
            return IsNextPackReady(currentTick, float.MaxValue);
        }

        public bool IsNextPackReady(int currentTick, float playerPositionX)
        {
            return HasNextPack &&
                Status == LevelingRunStatus.AwaitingPackActivation &&
                currentTick >= 0 &&
                currentTick >= _nextPackReadyTick &&
                _zone.GetPack(_nextPackIndex).ActivationX <= playerPositionX;
        }

        public bool TryGetNextPack(out LevelingPackDefinition pack)
        {
            if (!HasNextPack)
            {
                pack = default;
                return false;
            }

            pack = _zone.GetPack(_nextPackIndex);
            return true;
        }

        public bool TryActivateNextPack(
            int currentTick,
            out LevelingPackDefinition pack)
        {
            return TryActivateNextPack(currentTick, float.MaxValue, out pack);
        }

        public bool TryActivateNextPack(
            int currentTick,
            float playerPositionX,
            out LevelingPackDefinition pack)
        {
            if (!IsNextPackReady(currentTick, playerPositionX) ||
                !TryGetNextPack(out pack))
            {
                pack = default;
                return false;
            }

            _activePackIndex = _nextPackIndex;
            _activePackActivationTick = currentTick;
            _nextPackIndex++;
            _activeMobCount = pack.MobCount;
            _activeMob0 = pack.MobCount > 0
                ? pack.GetMob(0).Participant
                : default;
            _activeMob1 = pack.MobCount > 1
                ? pack.GetMob(1).Participant
                : default;
            _activeMob2 = pack.MobCount > 2
                ? pack.GetMob(2).Participant
                : default;

            for (int index = 0; index < pack.MobCount; index++)
            {
                _registeredMobMask |= GetMobBit(pack.GetMob(index).Participant);
            }

            Status = LevelingRunStatus.FightingPack;
            return true;
        }

        public ParticipantId GetActiveMobId(int index)
        {
            if (index < 0 || index >= _activeMobCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            switch (index)
            {
                case 0:
                    return _activeMob0;
                case 1:
                    return _activeMob1;
                default:
                    return _activeMob2;
            }
        }

        public bool IsMobRegistered(ParticipantId participant)
        {
            ushort bit = GetMobBit(participant);
            return bit != 0 && (_registeredMobMask & bit) != 0;
        }

        public LevelingCombatObservation ObserveCombatEvent(CombatEvent combatEvent)
        {
            if (combatEvent.Kind != CombatEventKind.Death)
            {
                return default;
            }

            if (combatEvent.Target == Player)
            {
                if (IsFailed ||
                    (IsCompleted && combatEvent.Tick != _terminalTick))
                {
                    return default;
                }

                FailAtTick(combatEvent.Tick, true);
                return new LevelingCombatObservation(
                    true,
                    default,
                    0,
                    default,
                    false,
                    false,
                    true);
            }

            if (IsCompleted || IsFailed)
            {
                return default;
            }

            ushort mobBit = GetMobBit(combatEvent.Target);
            if (mobBit == 0 ||
                combatEvent.Tick < _activePackActivationTick ||
                (_registeredMobMask & mobBit) == 0 ||
                (_resolvedMobMask & mobBit) != 0 ||
                !RemoveActiveMob(combatEvent.Target) ||
                !_zone.TryGetMob(
                    combatEvent.Target,
                    out LevelingMobDefinition defeatedMob))
            {
                return default;
            }

            _resolvedMobMask |= mobBit;
            int experienceAwarded = 0;
            LevelProgressionGrantResult grant = default;
            if (combatEvent.Actor == Player)
            {
                grant = _progression.GrantExperience(defeatedMob.ExperienceReward);
                experienceAwarded = grant.AppliedExperience;
            }

            bool packCleared = _activeMobCount == 0;
            bool completed = false;
            bool failed = false;
            if (packCleared)
            {
                _activePackIndex = -1;
                _activePackActivationTick = -1;
                if (_nextPackIndex >= _zone.PackCount)
                {
                    completed = _progression.IsMaximumLevel &&
                        _progression.TotalExperience ==
                            LevelProgressionCurve.MaximumExperience;
                    failed = !completed;
                    Status = completed
                        ? LevelingRunStatus.Completed
                        : LevelingRunStatus.Failed;
                    _terminalTick = combatEvent.Tick;
                }
                else
                {
                    _nextPackReadyTick = combatEvent.Tick >
                        int.MaxValue - InterPackDelayTicks
                            ? int.MaxValue
                            : combatEvent.Tick + InterPackDelayTicks;
                    Status = LevelingRunStatus.AwaitingPackActivation;
                }
            }

            return new LevelingCombatObservation(
                true,
                defeatedMob.Participant,
                experienceAwarded,
                grant,
                packCleared,
                completed,
                failed);
        }

        public bool Fail()
        {
            if (IsCompleted || IsFailed)
            {
                return false;
            }

            FailAtTick(-1, false);
            return true;
        }

        private void FailAtTick(int terminalTick, bool allowCompletionOverride)
        {
            if (!allowCompletionOverride && (IsCompleted || IsFailed))
            {
                return;
            }

            Status = LevelingRunStatus.Failed;
            _terminalTick = terminalTick;
            _activePackIndex = -1;
            _activePackActivationTick = -1;
            _activeMobCount = 0;
            _activeMob0 = default;
            _activeMob1 = default;
            _activeMob2 = default;
        }

        private bool RemoveActiveMob(ParticipantId participant)
        {
            if (_activeMobCount <= 0)
            {
                return false;
            }

            if (_activeMob0 == participant)
            {
                _activeMob0 = _activeMob1;
                _activeMob1 = _activeMob2;
                _activeMob2 = default;
                _activeMobCount--;
                return true;
            }

            if (_activeMob1 == participant)
            {
                _activeMob1 = _activeMob2;
                _activeMob2 = default;
                _activeMobCount--;
                return true;
            }

            if (_activeMob2 != participant)
            {
                return false;
            }

            _activeMob2 = default;
            _activeMobCount--;
            return true;
        }

        private static ushort GetMobBit(ParticipantId participant)
        {
            int offset = participant.Value - 2;
            return offset >= 0 && offset < 10
                ? (ushort)(1 << offset)
                : (ushort)0;
        }
    }
}
