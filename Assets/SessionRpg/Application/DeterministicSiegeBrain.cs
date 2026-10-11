using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    /// <summary>
    /// Deterministic lane-enemy intent. It reads Core snapshots and submits the
    /// same commands as players; objective damage remains controller-owned.
    /// </summary>
    public sealed class DeterministicSiegeEnemyBrain
    {
        internal const int SignatureDecisionInterval = 4;
        internal const float DefenderLaneMinimumForward = 3.6f;
        internal const float DefenderLaneMaximumForward = 7.5f;
        internal const float DefenderLaneMaximumLateral = 2.5f;
        internal const float DefenderAggroRange = 6.5f;

        private readonly SiegeEnemyArchetypeDefinition _archetype;
        private readonly ParticipantId _assignedDefender;
        private readonly SiegeLaneId _lane;
        private uint _sequence;
        private int _decisionCount;
        private int _nextDecisionTick;

        public DeterministicSiegeEnemyBrain(
            SiegeEnemyArchetypeId archetype,
            ParticipantId assignedDefender,
            SiegeLaneId lane)
        {
            if (!assignedDefender.IsValid)
            {
                throw new ArgumentException(
                    "A Siege enemy requires a stable lane defender.",
                    nameof(assignedDefender));
            }

            if (lane < SiegeLaneId.East || lane > SiegeLaneId.South)
            {
                throw new ArgumentOutOfRangeException(nameof(lane));
            }

            _archetype = SiegeEnemyArchetypeCatalog.Get(archetype);
            _assignedDefender = assignedDefender;
            _lane = lane;
        }

        public bool TryBuildCommand(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            out CombatCommand command)
        {
            ValidateArguments(simulation, snapshots, snapshotCount);
            if (!self.IsAlive || simulation.CurrentTick < _nextDecisionTick)
            {
                command = default;
                return false;
            }

            _nextDecisionTick = simulation.CurrentTick +
                Math.Max(1, simulation.Rules.TickRate / 4);
            if (self.CastingAbility != AbilityId.None &&
                self.CastEndTick > simulation.CurrentTick)
            {
                command = default;
                return false;
            }

            bool hasDefender = TryFindSnapshot(
                _assignedDefender,
                snapshots,
                snapshotCount,
                out CombatantSnapshot defender) &&
                defender.IsAlive &&
                !defender.HasAuraTag(AuraTag.Stealth) &&
                !defender.HasAuraTag(AuraTag.TargetingDropped) &&
                IsInsideLaneLeash(defender.Position, _lane);
            float defenderDistance = hasDefender
                ? Float3.PlanarDistance(self.Position, defender.Position)
                : float.MaxValue;
            bool engageDefender = hasDefender &&
                defenderDistance <= DefenderAggroRange &&
                (!_archetype.RushesObjective || defenderDistance <= 2.5f);
            _decisionCount++;
            uint sequence = _sequence + 1u;

            if (_archetype.SignaturePolicy ==
                    SiegeEnemySignaturePolicy.DefensiveAlly &&
                TryFindShieldTarget(
                    simulation, self, snapshots, snapshotCount,
                    out CombatantSnapshot shieldTarget) &&
                TryBuildAbilityCommand(
                    simulation, self, shieldTarget,
                    _archetype.SignatureAbility, sequence,
                    out command))
            {
                _sequence = sequence;
                _decisionCount = 0;
                return true;
            }

            if (engageDefender &&
                ShouldUseHostileSignature(defenderDistance) &&
                TryBuildAbilityCommand(
                    simulation, self, defender,
                    _archetype.SignatureAbility, sequence,
                    out command))
            {
                _sequence = sequence;
                _decisionCount = 0;
                return true;
            }

            if (engageDefender &&
                IsWithinAbilityReach(
                    _archetype.PrimaryAbility,
                    defenderDistance) &&
                TryBuildAbilityCommand(
                    simulation, self, defender,
                    _archetype.PrimaryAbility, sequence,
                    out command))
            {
                _sequence = sequence;
                return true;
            }

            Float3 destination = engageDefender
                ? defender.Position
                : HeroSiegeRunCatalog.ObjectivePosition;
            Float3 offset = destination - self.Position;
            float stopRadius = engageDefender
                ? _archetype.PreferredRange
                : HeroSiegeRunCatalog.ObjectiveApproachStopRadius;
            Float3 movement = offset.PlanarLength > stopRadius
                ? offset.PlanarNormalized
                : default;
            Float3 facing = offset.PlanarLengthSquared > 0.0001f
                ? offset.PlanarNormalized
                : self.Facing;
            command = CombatCommand.Move(
                self.Id, sequence, movement, facing);
            _sequence = sequence;
            return true;
        }

        internal static bool IsInsideLaneLeash(
            Float3 position,
            SiegeLaneId lane)
        {
            float forward;
            float lateral;
            switch (lane)
            {
                case SiegeLaneId.East:
                    forward = position.X;
                    lateral = Math.Abs(position.Z);
                    break;
                case SiegeLaneId.North:
                    forward = position.Z;
                    lateral = Math.Abs(position.X);
                    break;
                case SiegeLaneId.West:
                    forward = -position.X;
                    lateral = Math.Abs(position.Z);
                    break;
                case SiegeLaneId.South:
                    forward = -position.Z;
                    lateral = Math.Abs(position.X);
                    break;
                default:
                    return false;
            }

            return forward >= DefenderLaneMinimumForward &&
                forward <= DefenderLaneMaximumForward &&
                lateral <= DefenderLaneMaximumLateral;
        }

        private bool ShouldUseHostileSignature(float distance)
        {
            if (_archetype.SignatureAbility == AbilityId.None ||
                _decisionCount < SignatureDecisionInterval)
            {
                return false;
            }

            if (_archetype.SignaturePolicy ==
                SiegeEnemySignaturePolicy.CadenceHostile)
            {
                return IsWithinAbilityReach(
                    _archetype.SignatureAbility, distance);
            }

            if (_archetype.SignaturePolicy !=
                    SiegeEnemySignaturePolicy.CloseCasterArea ||
                !AbilityCatalog.TryGet(
                    _archetype.SignatureAbility,
                    out AbilityDefinition ability))
            {
                return false;
            }

            return distance <= ability.Targeting.Radius + 0.001f;
        }

        private bool TryFindShieldTarget(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            out CombatantSnapshot target)
        {
            target = default;
            float lowestRatio = 0.8f;
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot candidate = snapshots[index];
                if (!candidate.IsAlive ||
                    candidate.Team != self.Team ||
                    candidate.MaxHealth <= 0f ||
                    candidate.Barrier > 0.001f ||
                    !IsInsideHostileLane(candidate.Position, _lane))
                {
                    continue;
                }

                float ratio = candidate.Health / candidate.MaxHealth;
                float distance = Float3.PlanarDistance(
                    self.Position,
                    candidate.Position);
                if (ratio < lowestRatio &&
                    IsWithinAbilityReach(_archetype.SignatureAbility, distance) &&
                    (!AbilityCatalog.Get(_archetype.SignatureAbility)
                        .Targeting.RequiresLineOfSight ||
                     simulation.Rules.CollisionMap.HasLineOfSight(
                         self.Position,
                         candidate.Position)))
                {
                    lowestRatio = ratio;
                    target = candidate;
                }
            }

            return target.Id.IsValid;
        }

        private static bool IsInsideHostileLane(
            Float3 position,
            SiegeLaneId lane)
        {
            float forward;
            float lateral;
            switch (lane)
            {
                case SiegeLaneId.East:
                    forward = position.X;
                    lateral = Math.Abs(position.Z);
                    break;
                case SiegeLaneId.North:
                    forward = position.Z;
                    lateral = Math.Abs(position.X);
                    break;
                case SiegeLaneId.West:
                    forward = -position.X;
                    lateral = Math.Abs(position.Z);
                    break;
                case SiegeLaneId.South:
                    forward = -position.Z;
                    lateral = Math.Abs(position.X);
                    break;
                default:
                    return false;
            }

            return forward >= 2.2f && forward <= 13.5f && lateral <= 2.5f;
        }

        private static bool TryBuildAbilityCommand(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot targetSnapshot,
            AbilityId abilityId,
            uint sequence,
            out CombatCommand command)
        {
            if (!AbilityCatalog.TryGet(abilityId, out AbilityDefinition ability))
            {
                command = default;
                return false;
            }

            Float3 towardTarget =
                (targetSnapshot.Position - self.Position).PlanarNormalized;
            ParticipantId target = targetSnapshot.Id;
            Float3 targetPoint = targetSnapshot.Position;
            switch (ability.Targeting.Shape)
            {
                case AbilityTargetShape.CasterArea:
                    target = self.Id;
                    targetPoint = self.Position;
                    break;
                case AbilityTargetShape.Point:
                case AbilityTargetShape.Direction:
                case AbilityTargetShape.TargetArea:
                    target = default;
                    break;
                default:
                    if (!ability.Targeting.Allows(
                            self.Team == targetSnapshot.Team
                                ? AbilityTargetRelation.Friendly
                                : AbilityTargetRelation.Hostile) &&
                        ability.Targeting.Allows(AbilityTargetRelation.Self))
                    {
                        target = self.Id;
                        targetPoint = self.Position;
                    }
                    break;
            }

            CombatCommand candidate = CombatCommand.Cast(
                self.Id, sequence, default, towardTarget,
                abilityId, target, targetPoint, towardTarget);
            if (!simulation.CanStartAbility(candidate, out _))
            {
                command = default;
                return false;
            }

            command = candidate;
            return true;
        }

        private static bool IsWithinAbilityReach(
            AbilityId abilityId,
            float distance)
        {
            if (!AbilityCatalog.TryGet(abilityId, out AbilityDefinition ability))
            {
                return false;
            }

            float reach = ability.Targeting.Range;
            if (ability.Targeting.Shape == AbilityTargetShape.CasterArea)
            {
                reach = ability.Targeting.Radius;
            }
            else if (ability.Targeting.Shape == AbilityTargetShape.Point ||
                     ability.Targeting.Shape == AbilityTargetShape.TargetArea)
            {
                reach += ability.Targeting.Radius;
            }

            return distance + 0.001f >= ability.Targeting.MinimumRange &&
                distance <= reach + 0.001f;
        }

        private static bool TryFindSnapshot(
            ParticipantId participant,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            out CombatantSnapshot snapshot)
        {
            for (int index = 0; index < snapshotCount; index++)
            {
                if (snapshots[index].Id == participant)
                {
                    snapshot = snapshots[index];
                    return true;
                }
            }

            snapshot = default;
            return false;
        }

        private static void ValidateArguments(
            CombatSimulation simulation,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (snapshotCount < 0 || snapshotCount > snapshots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshotCount));
            }
        }
    }

    /// <summary>
    /// Lane-scoped adapter for the existing deterministic hero bot. The reusable
    /// buffer prevents a defender from targeting enemies authored for another lane.
    /// </summary>
    public sealed class DeterministicSiegeDefenderBrain
    {
        internal const float AnchorArrivalRadius = 0.35f;

        private readonly SiegeLaneId _lane;
        private readonly DeterministicBotBrain _bot;
        private readonly CombatantSnapshot[] _laneSnapshots;

        public DeterministicSiegeDefenderBrain(SiegeLaneId lane, int seed)
        {
            if (lane < SiegeLaneId.East || lane > SiegeLaneId.South)
            {
                throw new ArgumentOutOfRangeException(nameof(lane));
            }

            _lane = lane;
            _bot = new DeterministicBotBrain(seed);
            _laneSnapshots =
                new CombatantSnapshot[CombatSimulation.MaxCombatants];
        }

        public bool TryBuildCommand(
            CombatSimulation simulation,
            CombatantSnapshot self,
            HeroSiegeRunController controller,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            out CombatCommand command)
        {
            if (controller == null)
            {
                throw new ArgumentNullException(nameof(controller));
            }

            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (snapshotCount < 0 || snapshotCount > snapshots.Length ||
                controller.Variant.GetLaneIndex(_lane) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshotCount));
            }

            int laneSnapshotCount = 0;
            if (self.IsAlive)
            {
                _laneSnapshots[laneSnapshotCount++] = self;
            }

            for (int activeIndex = 0;
                activeIndex < controller.State.ActiveHostileCount;
                activeIndex++)
            {
                SiegeSpawnDefinition spawn =
                    controller.GetActiveSpawn(activeIndex);
                if (spawn.Lane != _lane ||
                    !TryFindSnapshot(
                        spawn.Participant,
                        snapshots,
                        snapshotCount,
                        out CombatantSnapshot enemy))
                {
                    continue;
                }

                _laneSnapshots[laneSnapshotCount++] = enemy;
            }

            bool hasCommand = _bot.TryBuildCommand(
                simulation,
                self,
                _laneSnapshots,
                laneSnapshotCount,
                out command);
            if (!hasCommand || !self.IsAlive ||
                self.CastingAbility != AbilityId.None)
            {
                return hasCommand;
            }

            Float3 anchor = controller.Variant.GetDefenderAnchor(_lane);
            Float3 toAnchor = anchor - self.Position;
            float anchorDistance = toAnchor.PlanarLength;
            bool hasLaneEnemy = laneSnapshotCount > 1;
            bool mustReturn =
                !DeterministicSiegeEnemyBrain.IsInsideLaneLeash(
                    self.Position,
                    _lane) ||
                (!hasLaneEnemy && anchorDistance > AnchorArrivalRadius);
            if (!mustReturn)
            {
                return true;
            }

            Float3 facing = toAnchor.PlanarLengthSquared > 0.0001f
                ? toAnchor.PlanarNormalized
                : self.Facing;
            command = CombatCommand.Move(
                self.Id,
                command.Sequence,
                facing,
                facing);
            return true;
        }

        private static bool TryFindSnapshot(
            ParticipantId participant,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            out CombatantSnapshot snapshot)
        {
            for (int index = 0; index < snapshotCount; index++)
            {
                if (snapshots[index].Id == participant)
                {
                    snapshot = snapshots[index];
                    return true;
                }
            }

            snapshot = default;
            return false;
        }
    }
}
