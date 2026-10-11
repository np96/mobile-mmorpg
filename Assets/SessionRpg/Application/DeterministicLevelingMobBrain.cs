using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    /// <summary>
    /// Compact leveling-enemy behavior with one repeatable action and one
    /// deterministic signature per archetype. It submits ordinary combat
    /// commands; presentation and Unity physics never decide the outcome.
    /// </summary>
    public sealed class DeterministicLevelingMobBrain
    {
        private readonly LevelingMobArchetypeId _archetype;
        private uint _sequence;
        private int _decisionCount;
        private int _nextDecisionTick;
        private bool _targetSuppressed;
        private bool _returningHome;

        internal const int SignatureDecisionInterval = 4;

        /// <summary>
        /// Compatibility path for callers authored before archetype-specific
        /// profiles existed. New leveling spawns should pass the stable archetype
        /// ID explicitly so behavior never depends on a Unity-selected loadout.
        /// </summary>
        public DeterministicLevelingMobBrain()
        {
            _archetype = LevelingMobArchetypeId.None;
        }

        public DeterministicLevelingMobBrain(LevelingMobArchetypeId archetype)
        {
            if (!LevelingMobArchetypeCatalog.TryGet(archetype, out _))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(archetype),
                    archetype,
                    "Unknown leveling-mob archetype ID.");
            }

            _archetype = archetype;
        }

        public bool TryBuildCommand(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot player,
            out CombatCommand command)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (!self.IsAlive || !player.IsAlive)
            {
                command = default;
                return false;
            }

            bool targetSuppressed = self.Team != player.Team &&
                (player.HasAuraTag(AuraTag.Stealth) ||
                 player.HasAuraTag(AuraTag.TargetingDropped));
            if (targetSuppressed)
            {
                if (_targetSuppressed)
                {
                    command = default;
                    return false;
                }

                // Move intent persists until another command replaces it. The
                // first concealed snapshot therefore bypasses the normal think
                // throttle and explicitly stops the mob without turning toward,
                // tracking, or otherwise leaking the player's hidden position.
                _targetSuppressed = true;
                _decisionCount = 0;
                _nextDecisionTick = simulation.CurrentTick;
                uint stopSequence = _sequence + 1u;
                command = CombatCommand.Move(
                    self.Id,
                    stopSequence,
                    default,
                    self.Facing);
                _sequence = stopSequence;
                return true;
            }

            if (_targetSuppressed)
            {
                // Reacquire immediately after reveal with a fresh signature
                // cadence rather than waiting on the pre-concealment schedule.
                _targetSuppressed = false;
                _decisionCount = 0;
                _nextDecisionTick = simulation.CurrentTick;
            }

            if (simulation.CurrentTick < _nextDecisionTick)
            {
                command = default;
                return false;
            }

            _nextDecisionTick = simulation.CurrentTick +
                Math.Max(1, simulation.Rules.TickRate / 4);

            // A movement command cancels an active cast. Let channels and casted
            // attacks finish instead of allowing the AI's next think to interrupt
            // its own readable telegraph.
            if (self.CastingAbility != AbilityId.None &&
                self.CastEndTick > simulation.CurrentTick)
            {
                command = default;
                return false;
            }

            Float3 towardPlayer = (player.Position - self.Position).PlanarNormalized;
            float distance = Float3.PlanarDistance(self.Position, player.Position);
            LevelingMobArchetypeDefinition profile = ResolveProfile(self);
            _decisionCount++;

            uint sequence = _sequence + 1u;
            if (ShouldTrySignature(profile, self, distance) &&
                TryBuildAbilityCommand(
                    simulation,
                    self,
                    player,
                    profile.SignatureAbility,
                    sequence,
                    towardPlayer,
                    out command))
            {
                _sequence = sequence;
                _decisionCount = 0;
                return true;
            }

            AbilityId attack = profile.PrimaryAttack;
            float minimumRange = 0f;
            if (AbilityCatalog.TryGet(attack, out AbilityDefinition ability))
            {
                minimumRange = ability.Targeting.MinimumRange;
            }

            Float3 movement = distance > profile.PreferredRange
                ? towardPlayer
                : distance + 0.001f < minimumRange
                    ? towardPlayer * -1f
                    : default;

            if (attack != AbilityId.None &&
                distance <= profile.PreferredRange + 0.001f &&
                TryBuildAbilityCommand(
                    simulation,
                    self,
                    player,
                    attack,
                    sequence,
                    towardPlayer,
                    out command))
            {
                _sequence = sequence;
                return true;
            }

            if (CanPressurePlayerWithFallback(profile.FallbackAbility, distance) &&
                TryBuildAbilityCommand(
                    simulation,
                    self,
                    player,
                    profile.FallbackAbility,
                    sequence,
                    towardPlayer,
                    out command))
            {
                _sequence = sequence;
                return true;
            }

            if (simulation.Rules.PlayableMode == PlayableModeId.PveLeveling &&
                !simulation.Rules.CollisionMap.HasLineOfSight(self.Position, player.Position))
            {
                // Range alone cannot make a covered target attackable. Circle
                // the first authoritative island even inside preferred range;
                // stable participant parity keeps the chosen side deterministic.
                movement = simulation.Rules.CollisionMap.BuildDetourDirection(
                    self.Position, player.Position, (self.Id.Value & 1) == 0);
            }

            // The finale elite keeps repositioning while its large actions recover
            // instead of appearing frozen for the Cleave cooldown. This is ordinary
            // deterministic movement intent; it does not change Core outcomes.
            if (profile.Id == LevelingMobArchetypeId.RiftWarden &&
                movement.PlanarLengthSquared <= 0.0001f &&
                towardPlayer.PlanarLengthSquared > 0.0001f)
            {
                movement = new Float3(-towardPlayer.Z, 0f, towardPlayer.X);
            }

            command = CombatCommand.Move(
                self.Id,
                sequence,
                movement,
                towardPlayer);
            _sequence = sequence;
            return true;
        }

        /// <summary>Reuses this brain only after its previous actor is retired.</summary>
        public void Reset()
        {
            _sequence = 0u;
            _decisionCount = 0;
            _nextDecisionTick = 0;
            _targetSuppressed = false;
            _returningHome = false;
        }

        /// <summary>
        /// Persistent-region-only admission to the existing NPC behavior. A mob
        /// evades toward its authored home when the visible chase leaves its
        /// camp. Core movement cancels its cast and Core restoration resets health
        /// at home. Concealed targets are never tracked by the leash decision.
        /// </summary>
        public bool TryBuildLeashedCommand(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot player,
            Float3 home,
            float leashRadius,
            out CombatCommand command)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (!self.IsAlive || !player.IsAlive || leashRadius <= 0f ||
                float.IsNaN(leashRadius) || float.IsInfinity(leashRadius) ||
                float.IsNaN(home.X) || float.IsInfinity(home.X) ||
                float.IsNaN(home.Z) || float.IsInfinity(home.Z))
            {
                command = default;
                return false;
            }

            bool concealed = self.Team != player.Team &&
                (player.HasAuraTag(AuraTag.Stealth) ||
                 player.HasAuraTag(AuraTag.TargetingDropped));
            bool shouldReturn = _returningHome ||
                Float3.PlanarDistance(self.Position, home) >
                leashRadius || (!concealed &&
                Float3.PlanarDistance(player.Position, home) > leashRadius);
            if (shouldReturn)
            {
                Float3 towardHome = home - self.Position;
                bool atHome = towardHome.PlanarLengthSquared <= 1f;
                _returningHome = !atHome;
                if (atHome)
                {
                    simulation.TryRestoreHealth(self.Id, self.MaxHealth, out _);
                }

                _decisionCount = 0;
                _nextDecisionTick = simulation.CurrentTick;
                uint sequence = _sequence + 1u;
                Float3 movement = atHome ? default : towardHome.PlanarNormalized;
                if (!atHome && simulation.Rules.PlayableMode == PlayableModeId.PveLeveling)
                {
                    movement = simulation.Rules.CollisionMap.BuildDetourDirection(
                        self.Position, home, (self.Id.Value & 1) == 0);
                }
                command = CombatCommand.Move(
                    self.Id,
                    sequence,
                    movement,
                    atHome ? self.Facing : movement);
                _sequence = sequence;
                return true;
            }

            return TryBuildCommand(simulation, self, player, out command);
        }

        private bool ShouldTrySignature(
            LevelingMobArchetypeDefinition profile,
            CombatantSnapshot self,
            float playerDistance)
        {
            if (profile.SignatureAbility == AbilityId.None)
            {
                return false;
            }

            switch (profile.SignaturePolicy)
            {
                case LevelingMobSignaturePolicy.CadenceHostile:
                    return _decisionCount >= SignatureDecisionInterval;
                case LevelingMobSignaturePolicy.CloseCasterArea:
                    return _decisionCount >= SignatureDecisionInterval &&
                        AbilityCatalog.TryGet(
                            profile.SignatureAbility,
                            out AbilityDefinition closeAbility) &&
                        playerDistance <= closeAbility.Targeting.Radius + 0.001f;
                case LevelingMobSignaturePolicy.DefensiveSelf:
                    return self.MaxHealth > 0f &&
                        self.Health / self.MaxHealth <= 0.7f &&
                        self.Barrier <= 0.001f;
                case LevelingMobSignaturePolicy.PointAtRange:
                    if (!AbilityCatalog.TryGet(
                            profile.SignatureAbility,
                            out AbilityDefinition pointAbility))
                    {
                        return false;
                    }

                    float effectiveReach = pointAbility.Targeting.Range +
                        pointAbility.Targeting.Radius;
                    return playerDistance <= effectiveReach + 0.001f &&
                        (playerDistance > profile.PreferredRange + 0.001f ||
                         _decisionCount >= SignatureDecisionInterval);
                default:
                    return false;
            }
        }

        private static bool CanPressurePlayerWithFallback(
            AbilityId abilityId,
            float playerDistance)
        {
            if (abilityId == AbilityId.None ||
                !AbilityCatalog.TryGet(abilityId, out AbilityDefinition ability))
            {
                return false;
            }

            switch (ability.Targeting.Shape)
            {
                case AbilityTargetShape.CasterArea:
                    return playerDistance <= ability.Targeting.Radius + 0.001f;
                case AbilityTargetShape.Point:
                case AbilityTargetShape.TargetArea:
                    return playerDistance <= ability.Targeting.Range +
                        ability.Targeting.Radius + 0.001f;
                default:
                    return playerDistance <= ability.Targeting.Range + 0.001f;
            }
        }

        private static bool TryBuildAbilityCommand(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot player,
            AbilityId abilityId,
            uint sequence,
            Float3 towardPlayer,
            out CombatCommand command)
        {
            if (!AbilityCatalog.TryGet(abilityId, out AbilityDefinition ability))
            {
                command = default;
                return false;
            }

            ParticipantId target = player.Id;
            Float3 targetPoint = player.Position;
            AbilityTargetRule targeting = ability.Targeting;
            if (targeting.Shape == AbilityTargetShape.CasterArea)
            {
                target = self.Id;
                targetPoint = self.Position;
            }
            else if (targeting.Shape == AbilityTargetShape.Point ||
                     targeting.Shape == AbilityTargetShape.Direction ||
                     targeting.Shape == AbilityTargetShape.TargetArea)
            {
                target = default;
            }
            else if (!targeting.Allows(AbilityTargetRelation.Hostile) &&
                     targeting.Allows(AbilityTargetRelation.Self))
            {
                target = self.Id;
                targetPoint = self.Position;
            }

            CombatCommand candidate = CombatCommand.Cast(
                self.Id,
                sequence,
                default,
                towardPlayer,
                abilityId,
                target,
                targetPoint,
                towardPlayer);
            if (!simulation.CanStartAbility(candidate, out _))
            {
                command = default;
                return false;
            }

            command = candidate;
            return true;
        }

        private LevelingMobArchetypeDefinition ResolveProfile(
            CombatantSnapshot self)
        {
            if (_archetype != LevelingMobArchetypeId.None)
            {
                return LevelingMobArchetypeCatalog.Get(_archetype);
            }

            return LevelingMobArchetypeCatalog.TryGetBySpecialization(
                self.Specialization,
                out LevelingMobArchetypeDefinition legacyProfile)
                    ? legacyProfile
                    : default;
        }
    }
}
