using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    public readonly struct GrindingMobDefinition
    {
        internal GrindingMobDefinition(
            ParticipantId participant,
            LevelingMobArchetypeId archetype,
            int level,
            float baseMaximumHealth,
            int experienceReward,
            Float3 position)
        {
            Participant = participant;
            Archetype = archetype;
            Level = level;
            BaseMaximumHealth = baseMaximumHealth;
            ExperienceReward = experienceReward;
            Position = position;
        }

        public ParticipantId Participant { get; }
        public LevelingMobArchetypeId Archetype { get; }
        public int Level { get; }
        public float BaseMaximumHealth { get; }
        public float MaximumHealth => BaseMaximumHealth +
            ((Level - 1) * PersistentLevelProgression.HealthPerGainedLevel);
        public int ExperienceReward { get; }
        public Float3 Position { get; }
    }

    /// <summary>
    /// A stable camp identity plus an activation's unique participant range.
    /// Preview definitions deliberately expose invalid participant identities.
    /// </summary>
    public readonly struct GrindingCampDefinition
    {
        private readonly int _firstParticipantValue;

        internal GrindingCampDefinition(int index, int firstParticipantValue)
        {
            Index = index;
            _firstParticipantValue = firstParticipantValue;
        }

        public int Id => Index;
        public int Index { get; }

        public string DisplayName
        {
            get
            {
                switch (Index)
                {
                    case 0: return "CINDER OUTRIDERS";
                    case 1: return "FROSTBOUND WATCH";
                    case 2: return "BROKEN GATE GUARD";
                    case 3: return "ASHEN CONCLAVE";
                    case 4: return "OSSUARY MARAUDERS";
                    case 5: return "RELIQUARY SENTINELS";
                    default: return "RIFT WARDEN";
                }
            }
        }

        public string RoleHint
        {
            get
            {
                switch (Index)
                {
                    case 0: return "MELEE + MARKSMAN";
                    case 1: return "FROST CONTROL + RAIDER";
                    case 2: return "BRUTE + SHIELDED WARDER";
                    case 3: return "HEXER + SHADOW CASTER";
                    case 4: return "THREE-MOB MIXED PACK";
                    case 5: return "VETERAN WARDER + BRUTE";
                    default: return "ELITE / DODGE THE METEOR";
                }
            }
        }

        public Float3 Center => PveRegionLayout.GetCampCenter(Index);

        public int Level => Index < 5 ? Index + 1 : Index == 5 ? 7 : 9;
        public int MobCount => Index == 6 ? 1 : Index == 4 ? 3 : 2;

        public GrindingMobDefinition GetMob(int index)
        {
            if (index < 0 || index >= MobCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            LevelingMobArchetypeId archetype;
            switch (Index)
            {
                case 0:
                    archetype = index == 0
                        ? LevelingMobArchetypeId.AshMelee
                        : LevelingMobArchetypeId.AshRanged;
                    break;
                case 1:
                    archetype = index == 0
                        ? LevelingMobArchetypeId.RimeFrostweaver
                        : LevelingMobArchetypeId.AshMelee;
                    break;
                case 2:
                    archetype = index == 0
                        ? LevelingMobArchetypeId.AshBrute
                        : LevelingMobArchetypeId.ReliquaryWarder;
                    break;
                case 3:
                    archetype = index == 0
                        ? LevelingMobArchetypeId.AshCaster
                        : LevelingMobArchetypeId.AshElite;
                    break;
                case 4:
                    archetype = index == 0
                        ? LevelingMobArchetypeId.AshMelee
                        : index == 1
                            ? LevelingMobArchetypeId.AshRanged
                            : LevelingMobArchetypeId.AshCaster;
                    break;
                case 5:
                    archetype = index == 0
                        ? LevelingMobArchetypeId.ReliquaryWarder
                        : LevelingMobArchetypeId.AshBrute;
                    break;
                default:
                    archetype = LevelingMobArchetypeId.RiftWarden;
                    break;
            }

            float baseHealth = archetype == LevelingMobArchetypeId.RiftWarden
                ? 230f
                : archetype == LevelingMobArchetypeId.AshBrute
                    ? 74f
                    : archetype == LevelingMobArchetypeId.ReliquaryWarder
                        ? 66f
                        : 54f;
            int reward = Index == 6 ? 420 : 28 + (Level * Level * 3);
            Float3 offset = MobCount == 1
                ? default
                : new Float3((index - 0.5f) * 3f, 0f,
                    index == 0 ? -2f : index == 1 ? 2f : 0f);
            return new GrindingMobDefinition(
                _firstParticipantValue > 0
                    ? new ParticipantId(_firstParticipantValue + index)
                    : default,
                archetype,
                Level,
                baseHealth,
                reward,
                Center + offset);
        }
    }

    public readonly struct GrindingCombatObservation
    {
        internal GrindingCombatObservation(
            bool wasHandled,
            int experienceAwarded,
            bool didLevelUp,
            bool playerDied,
            bool campCleared)
        {
            WasHandled = wasHandled;
            ExperienceAwarded = experienceAwarded;
            DidLevelUp = didLevelUp;
            PlayerDied = playerDied;
            CampCleared = campCleared;
        }

        public bool WasHandled { get; }
        public int ExperienceAwarded { get; }
        public bool DidLevelUp { get; }
        public bool PlayerDied { get; }
        public bool CampCleared { get; }
    }

    /// <summary>
    /// Local persistent PvE's repeatable no-quest camp loop. Only authoritative
    /// death events resolve active spawn identities. Geography and accumulated
    /// XP survive recovery; no completion condition resets character progression.
    /// All steady-state operations use fixed storage allocated at construction.
    /// </summary>
    public sealed class GrindingRegionRunController
    {
        public const int CampCount = 7;
        public const int MaximumActiveMobCount = 3;
        public const int RespawnDelayTicks = 600;
        public const float ActivationRadius = 22f;
        public const float RearmRadius = 26f;
        public const float LeashRadius = 45f;
        public const float LiveMobRetentionRadius = 60f;

        private readonly int[] _campRespawnTicks = new int[CampCount];
        private readonly bool[] _campRequiresExit = new bool[CampCount];
        private readonly ParticipantId[] _activeMobs =
            new ParticipantId[MaximumActiveMobCount];
        private GrindingCampDefinition _activeCamp;
        private int _nextParticipantValue = 2;
        private int _activationTick = -1;
        private bool _playerDead;
        private int _playerDeathTick = -1;

        public GrindingRegionRunController(int totalExperience = 0)
        {
            PersistentLevelProgression.GetLevelForExperience(totalExperience);
            TotalExperience = totalExperience;
            ActiveCampIndex = -1;
        }

        public ParticipantId Player => new ParticipantId(1);
        public int TotalExperience { get; private set; }
        public int Level => PersistentLevelProgression.GetLevelForExperience(
            TotalExperience);
        public int Kills { get; private set; }
        public int ActiveCampIndex { get; private set; }
        public int ActiveMobCount { get; private set; }
        public int RespawnAtTick { get; private set; }
        public bool IsPlayerDead => _playerDead;
        public Float3 ActiveCampCenter => ActiveCampIndex >= 0
            ? _activeCamp.Center
            : default;

        public static GrindingCampDefinition GetCampDefinition(int index)
        {
            if (index < 0 || index >= CampCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return new GrindingCampDefinition(index, 0);
        }

        public int GetCampRespawnAtTick(int index)
        {
            if (index < 0 || index >= CampCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _campRespawnTicks[index];
        }

        public bool IsWithinActiveCampLeash(Float3 position)
        {
            return ActiveCampIndex >= 0 && IsFinite(position) &&
                Float3.PlanarDistance(position, _activeCamp.Center) <= LeashRadius;
        }

        public bool TryActivateCamp(
            int tick,
            Float3 playerPosition,
            out GrindingCampDefinition camp)
        {
            camp = default;
            if (tick < 0 || _playerDead || !IsFinite(playerPosition))
            {
                return false;
            }

            for (int index = 0; index < CampCount; index++)
            {
                if (_campRequiresExit[index] &&
                    Float3.PlanarDistance(
                        playerPosition,
                        GetCampDefinition(index).Center) >= RearmRadius)
                {
                    _campRequiresExit[index] = false;
                }
            }

            if (ActiveCampIndex >= 0)
            {
                return false;
            }

            for (int index = 0; index < CampCount; index++)
            {
                GrindingCampDefinition candidate = GetCampDefinition(index);
                if (_campRequiresExit[index] || tick < _campRespawnTicks[index] ||
                    Float3.PlanarDistance(playerPosition, candidate.Center) >
                        ActivationRadius)
                {
                    continue;
                }

                // Exhaustion must fail closed rather than wrap into an old spawn.
                if (_nextParticipantValue > int.MaxValue - candidate.MobCount)
                {
                    return false;
                }

                _activeCamp = new GrindingCampDefinition(index, _nextParticipantValue);
                _nextParticipantValue += candidate.MobCount;
                ActiveCampIndex = index;
                ActiveMobCount = candidate.MobCount;
                _activationTick = tick;
                for (int mobIndex = 0; mobIndex < ActiveMobCount; mobIndex++)
                {
                    _activeMobs[mobIndex] = _activeCamp.GetMob(mobIndex).Participant;
                }

                camp = _activeCamp;
                return true;
            }

            return false;
        }

        public ParticipantId GetActiveMobId(int index)
        {
            if (index < 0 || index >= ActiveMobCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _activeMobs[index];
        }

        /// <summary>
        /// Runtime retirement admission uses current Core snapshots, not camera
        /// visibility. Crossing the chase leash must not pool a living NPC beside
        /// the player: retain the camp while any survivor is within relevance so
        /// its existing brain can visibly return home. Missing snapshots fail
        /// closed. Refuge entry deliberately keeps its immediate sanctuary reset.
        /// </summary>
        public bool TryAbandonCamp(
            CombatSimulation simulation,
            out GrindingCampDefinition abandoned)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            abandoned = default;
            if (ActiveCampIndex < 0 || _playerDead ||
                !simulation.TryGetSnapshot(Player, out CombatantSnapshot player) ||
                !player.IsAlive || !IsFinite(player.Position))
            {
                return false;
            }

            if (!PveRegionLayout.IsAtRefuge(player.Position))
            {
                if (IsWithinActiveCampLeash(player.Position))
                {
                    return false;
                }

                for (int index = 0; index < ActiveMobCount; index++)
                {
                    if (!simulation.TryGetSnapshot(_activeMobs[index], out CombatantSnapshot mob) ||
                        !IsFinite(mob.Position) ||
                        (mob.IsAlive && Float3.PlanarDistance(player.Position, mob.Position) <=
                            LiveMobRetentionRadius))
                    {
                        return false;
                    }
                }
            }

            return TryAbandonCamp(simulation.CurrentTick, player.Position, out abandoned);
        }

        /// <summary>
        /// Applies an admitted camp abandonment without awarding XP. Runtime
        /// composition must first use the snapshot overload's retention gate;
        /// this state transition alone knows only the player's camp distance.
        /// The returned definition
        /// includes all original spawns; the composition layer retires only those
        /// still present in Core before activating another camp.
        /// </summary>
        public bool TryAbandonCamp(
            int tick,
            Float3 playerPosition,
            out GrindingCampDefinition abandoned)
        {
            abandoned = default;
            if (tick < 0 || _playerDead || ActiveCampIndex < 0 ||
                !IsFinite(playerPosition) ||
                (!PveRegionLayout.IsAtRefuge(playerPosition) &&
                 Float3.PlanarDistance(playerPosition, _activeCamp.Center) <= LeashRadius))
            {
                return false;
            }

            abandoned = _activeCamp;
            int deadline = tick > int.MaxValue - RespawnDelayTicks
                ? int.MaxValue
                : tick + RespawnDelayTicks;
            _campRespawnTicks[ActiveCampIndex] = deadline;
            _campRequiresExit[ActiveCampIndex] = true;
            RespawnAtTick = deadline;
            ActiveCampIndex = -1;
            ActiveMobCount = 0;
            _activationTick = -1;
            for (int index = 0; index < MaximumActiveMobCount; index++)
            {
                _activeMobs[index] = default;
            }

            return true;
        }

        public bool TryGetMob(
            ParticipantId participant,
            out GrindingMobDefinition mob)
        {
            if (ActiveCampIndex >= 0)
            {
                for (int index = 0; index < _activeCamp.MobCount; index++)
                {
                    GrindingMobDefinition candidate = _activeCamp.GetMob(index);
                    if (candidate.Participant == participant)
                    {
                        mob = candidate;
                        return true;
                    }
                }
            }

            mob = default;
            return false;
        }

        public GrindingCombatObservation ObserveCombatEvent(CombatEvent combatEvent)
        {
            if (combatEvent.Kind != CombatEventKind.Death ||
                combatEvent.Tick < 0 ||
                (_playerDead && combatEvent.Tick != _playerDeathTick))
            {
                return default;
            }

            if (combatEvent.Target == Player)
            {
                if (_playerDead)
                {
                    return default;
                }

                _playerDead = true;
                _playerDeathTick = combatEvent.Tick;
                return new GrindingCombatObservation(true, 0, false, true, false);
            }

            if (ActiveCampIndex < 0 || combatEvent.Tick < _activationTick ||
                !TryGetMob(combatEvent.Target, out GrindingMobDefinition mob))
            {
                return default;
            }

            int activeIndex = -1;
            for (int index = 0; index < ActiveMobCount; index++)
            {
                if (_activeMobs[index] == combatEvent.Target)
                {
                    activeIndex = index;
                    break;
                }
            }

            if (activeIndex < 0)
            {
                return default;
            }

            for (int index = activeIndex; index < ActiveMobCount - 1; index++)
            {
                _activeMobs[index] = _activeMobs[index + 1];
            }

            ActiveMobCount--;
            _activeMobs[ActiveMobCount] = default;
            int oldLevel = Level;
            int awarded = 0;
            if (combatEvent.Actor == Player)
            {
                awarded = Math.Min(mob.ExperienceReward,
                    PersistentLevelProgression.MaximumExperience - TotalExperience);
                TotalExperience += awarded;
                if (Kills < int.MaxValue)
                {
                    Kills++;
                }
            }

            bool cleared = ActiveMobCount == 0;
            if (cleared)
            {
                int deadline = combatEvent.Tick > int.MaxValue - RespawnDelayTicks
                    ? int.MaxValue
                    : combatEvent.Tick + RespawnDelayTicks;
                _campRespawnTicks[ActiveCampIndex] = deadline;
                _campRequiresExit[ActiveCampIndex] = true;
                RespawnAtTick = deadline;
                ActiveCampIndex = -1;
                _activationTick = -1;
            }

            return new GrindingCombatObservation(
                true, awarded, Level > oldLevel, false, cleared);
        }

        /// <summary>
        /// Call only while rebuilding combat at the refuge in a new simulation
        /// epoch. Character XP and session kills survive. Camp timers restart;
        /// unique participant IDs continue so stale mob deaths cannot be replayed.
        /// </summary>
        public void ResetAfterDeath(int tick)
        {
            if (tick < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(tick));
            }

            _playerDead = false;
            _playerDeathTick = -1;
            ActiveCampIndex = -1;
            ActiveMobCount = 0;
            _activationTick = -1;
            RespawnAtTick = 0;
            for (int index = 0; index < CampCount; index++)
            {
                _campRespawnTicks[index] = tick;
                _campRequiresExit[index] = false;
            }

            for (int index = 0; index < MaximumActiveMobCount; index++)
            {
                _activeMobs[index] = default;
            }
        }

        private static bool IsFinite(Float3 position)
        {
            return !float.IsNaN(position.X) && !float.IsInfinity(position.X) &&
                !float.IsNaN(position.Y) && !float.IsInfinity(position.Y) &&
                !float.IsNaN(position.Z) && !float.IsInfinity(position.Z);
        }
    }
}
