using System;

namespace SessionRpg.Core
{
    /// <summary>
    /// Deterministic Marksmanship companion, trap, and hunter-only ability rules.
    /// Companion and ground-effect state intentionally live outside the participant
    /// array: they never count as arena survivors and cannot change match victory.
    /// </summary>
    public sealed partial class CombatSimulation
    {
        private const int MaximumGroundEffects = MaxCombatants * 2;
        private const float CompanionHealthScale = 0.65f;
        private const float CompanionMinimumHealth = 50f;
        private const float CompanionFollowDistance = 1.35f;
        private const float CompanionFollowTolerance = 0.3f;
        private const float CompanionTeleportLeash = 12f;
        private const float CompanionAttackLeash = 18f;
        private const float CompanionMoveSpeed = 6.2f;
        private const float CompanionMeleeRange = 1.5f;
        private const float CompanionMeleeDamage = 4f;
        private const float CompanionMeleeIntervalSeconds = 1.5f;
        private const float CompanionLavaDamagePerSecond = 12f;
        private const float DisengageDistance = 8f;
        private const float MendPetAmount = 30f;
        private const float SpiderWebDurationSeconds = 4f;
        private const float MastersCallDurationSeconds = 4f;
        private const float FreezingTrapDurationSeconds = 8f;
        private const float TrapLifetimeSeconds = 30f;
        private const float FreezingTrapRadius = 1.5f;
        private const float FrostTrapRadius = 3.5f;
        private const float FrostTrapRefreshSeconds = 1f;
        private const float RoarRedirectFraction = 0.2f;
        private const float ChimeraSerpentDamage = 10f;
        private const float ChimeraViperManaReturn = 10f;
        private const float SerpentStingDurationSeconds = 15f;
        private const float ViperStingDurationSeconds = 8f;
        private const float ScorpidStingDurationSeconds = 20f;
        private const float ChimeraScorpidDisarmSeconds = 10f;
        private const float ChimeraScorpidLockoutSeconds = 60f;

        private readonly CompanionState[] _companions =
            new CompanionState[MaxCombatants];
        private readonly GroundEffectState[] _groundEffects =
            new GroundEffectState[MaximumGroundEffects];

        public int CompanionCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _companions.Length; index++)
                {
                    if (_companions[index].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int GroundEffectCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _groundEffects.Length; index++)
                {
                    if (_groundEffects[index].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool TryGetCompanionSnapshot(
            ParticipantId owner,
            out CompanionSnapshot snapshot)
        {
            int index = FindCompanionIndex(owner);
            if (index < 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = CreateCompanionSnapshot(_companions[index]);
            return true;
        }

        public int CopyCompanionSnapshots(CompanionSnapshot[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            int count = 0;
            for (int index = 0;
                 index < _companions.Length && count < destination.Length;
                 index++)
            {
                if (_companions[index].Active)
                {
                    destination[count++] = CreateCompanionSnapshot(_companions[index]);
                }
            }

            return count;
        }

        public int CopyGroundEffectSnapshots(GroundEffectSnapshot[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            int count = 0;
            for (int index = 0;
                 index < _groundEffects.Length && count < destination.Length;
                 index++)
            {
                GroundEffectState effect = _groundEffects[index];
                if (!effect.Active)
                {
                    continue;
                }

                destination[count++] = new GroundEffectSnapshot(
                    effect.Id,
                    effect.Owner,
                    effect.Team,
                    effect.Position,
                    effect.Radius,
                    effect.EndTick);
            }

            return count;
        }

        private void SpawnCompanion(CombatantState owner)
        {
            int companionIndex = FindCompanionIndex(owner.Id);
            if (companionIndex < 0)
            {
                companionIndex = FindFreeCompanionIndex();
            }

            if (companionIndex < 0)
            {
                return;
            }

            Float3 desiredPosition = owner.Position -
                (owner.Facing.PlanarNormalized * CompanionFollowDistance);
            Float3 position = _rules.CollisionMap.ResolvePlacement(
                desiredPosition,
                ArenaLayout.CombatantCollisionRadius,
                default(Float3) - owner.Facing.PlanarNormalized);
            float maximumHealth = Math.Max(
                CompanionMinimumHealth,
                owner.MaxHealth * CompanionHealthScale);
            _companions[companionIndex] = new CompanionState
            {
                Active = true,
                Owner = owner.Id,
                Team = owner.Team,
                Position = position,
                Facing = owner.Facing,
                Health = maximumHealth,
                MaxHealth = maximumHealth,
                IsAlive = true,
                Order = CompanionOrder.Follow,
                StayPoint = position,
                NextAttackTick = CurrentTick,
            };
        }

        private void DespawnCompanion(ParticipantId owner)
        {
            int companionIndex = FindCompanionIndex(owner);
            if (companionIndex >= 0)
            {
                _companions[companionIndex] = default;
            }
        }

        private void ClearHunterParticipantState(ParticipantId removed)
        {
            DespawnCompanion(removed);

            for (int index = 0; index < _groundEffects.Length; index++)
            {
                if (_groundEffects[index].Active &&
                    _groundEffects[index].Owner == removed)
                {
                    _groundEffects[index] = default;
                }
            }

            for (int index = 0; index < _companions.Length; index++)
            {
                CompanionState companion = _companions[index];
                if (!companion.Active || companion.Target != removed)
                {
                    continue;
                }

                companion.Order = CompanionOrder.Follow;
                companion.Target = default;
                _companions[index] = companion;
            }
        }

        private void ProcessCompanions()
        {
            for (int companionIndex = 0;
                 companionIndex < _companions.Length;
                 companionIndex++)
            {
                CompanionState companion = _companions[companionIndex];
                if (!companion.Active)
                {
                    continue;
                }

                int ownerIndex = FindIndex(companion.Owner);
                if (ownerIndex < 0 || !_combatants[ownerIndex].IsAlive)
                {
                    _companions[companionIndex] = default;
                    continue;
                }

                if (!companion.IsAlive)
                {
                    continue;
                }

                if (companion.ControlEndTick > CurrentTick)
                {
                    _companions[companionIndex] = companion;
                    continue;
                }

                CombatantState owner = _combatants[ownerIndex];
                if (_rules.RadialHazardsEnabled &&
                    companion.Position.PlanarLength >= _rules.OuterKillRadius)
                {
                    DamageCompanion(companionIndex, companion.Health);
                    continue;
                }

                if (_rules.RadialHazardsEnabled &&
                    companion.Position.PlanarLength > _rules.SafeRadius)
                {
                    DamageCompanion(
                        companionIndex,
                        CompanionLavaDamagePerSecond * _rules.TickSeconds);
                    companion = _companions[companionIndex];
                    if (!companion.IsAlive)
                    {
                        continue;
                    }
                }

                if (companion.Order == CompanionOrder.Attack)
                {
                    int targetIndex = FindIndex(companion.Target);
                    bool validTarget = targetIndex >= 0 &&
                        _combatants[targetIndex].IsAlive &&
                        IsEnemy(owner, _combatants[targetIndex]) &&
                        !HasAuraTag(_combatants[targetIndex], AuraTag.Stealth) &&
                        !HasAuraTag(
                            _combatants[targetIndex],
                            AuraTag.TargetingDropped) &&
                        Float3.PlanarDistance(owner.Position, _combatants[targetIndex].Position) <=
                            CompanionAttackLeash;
                    if (!validTarget)
                    {
                        companion.Order = CompanionOrder.Follow;
                        companion.Target = default;
                    }
                    else
                    {
                        MoveAndAttackWithCompanion(
                            companionIndex,
                            ownerIndex,
                            targetIndex,
                            ref companion);
                    }
                }

                if (companion.Order == CompanionOrder.Follow)
                {
                    FollowCompanionOwner(owner, ref companion);
                }

                _companions[companionIndex] = companion;
            }
        }

        private void MoveAndAttackWithCompanion(
            int companionIndex,
            int ownerIndex,
            int targetIndex,
            ref CompanionState companion)
        {
            CombatantState target = _combatants[targetIndex];
            Float3 toTarget = target.Position - companion.Position;
            float distance = toTarget.PlanarLength;
            if (distance > 0.0001f)
            {
                companion.Facing = toTarget.PlanarNormalized;
            }

            if (distance > CompanionMeleeRange)
            {
                float travel = Math.Min(
                    CompanionMoveSpeed * _rules.TickSeconds,
                    Math.Max(0f, distance - CompanionMeleeRange));
                companion.Position = _rules.CollisionMap.ResolveMovement(
                    companion.Position,
                    companion.Facing * travel,
                    ArenaLayout.CombatantCollisionRadius);
                distance = Float3.PlanarDistance(companion.Position, target.Position);
            }

            if (distance <= CompanionMeleeRange + 0.001f &&
                companion.NextAttackTick <= CurrentTick)
            {
                _companions[companionIndex] = companion;
                ApplyDamage(
                    ownerIndex,
                    targetIndex,
                    CompanionMeleeDamage,
                    AbilityId.PetAttack,
                    false,
                    SpellSchoolMask.Physical);
                companion = _companions[companionIndex];
                companion.NextAttackTick = CurrentTick +
                    SecondsToTicks(CompanionMeleeIntervalSeconds);
                if (!_combatants[targetIndex].IsAlive)
                {
                    companion.Order = CompanionOrder.Follow;
                    companion.Target = default;
                }
            }

            _companions[companionIndex] = companion;
        }

        private void FollowCompanionOwner(
            CombatantState owner,
            ref CompanionState companion)
        {
            Float3 desiredPosition = owner.Position -
                (owner.Facing.PlanarNormalized * CompanionFollowDistance);
            Float3 toDesired = desiredPosition - companion.Position;
            float distance = toDesired.PlanarLength;
            if (distance > CompanionTeleportLeash)
            {
                companion.Position = _rules.CollisionMap.ResolvePlacement(
                    desiredPosition,
                    ArenaLayout.CombatantCollisionRadius,
                    default(Float3) - owner.Facing.PlanarNormalized);
                companion.Facing = owner.Facing;
                return;
            }

            if (distance <= CompanionFollowTolerance)
            {
                return;
            }

            companion.Facing = toDesired.PlanarNormalized;
            float travel = Math.Min(
                CompanionMoveSpeed * _rules.TickSeconds,
                distance - CompanionFollowTolerance);
            companion.Position = _rules.CollisionMap.ResolveMovement(
                companion.Position,
                companion.Facing * travel,
                ArenaLayout.CombatantCollisionRadius);
        }

        private void ProcessGroundEffects()
        {
            for (int effectIndex = 0;
                 effectIndex < _groundEffects.Length;
                 effectIndex++)
            {
                GroundEffectState effect = _groundEffects[effectIndex];
                if (!effect.Active)
                {
                    continue;
                }

                if (effect.EndTick <= CurrentTick)
                {
                    _groundEffects[effectIndex] = default;
                    continue;
                }

                if (effect.ArmTick > CurrentTick)
                {
                    continue;
                }

                int sourceIndex = FindIndex(effect.Owner);
                if (sourceIndex < 0)
                {
                    _groundEffects[effectIndex] = default;
                    continue;
                }

                for (int targetIndex = 0;
                     targetIndex < _combatantCount;
                     targetIndex++)
                {
                    CombatantState target = _combatants[targetIndex];
                    if (!target.IsAlive || target.Team == effect.Team ||
                        Float3.PlanarDistance(effect.Position, target.Position) >
                            effect.Radius + 0.001f)
                    {
                        continue;
                    }

                    if (effect.Id == GroundEffectId.FreezingTrap)
                    {
                        ApplyAura(
                            sourceIndex,
                            targetIndex,
                            AuraId.FreezingTrap,
                            FreezingTrapDurationSeconds,
                            0f,
                            AbilityId.FreezingArrow,
                            SpellSchoolMask.Frost);
                        _groundEffects[effectIndex] = default;
                        break;
                    }

                    ApplyAuraOnceOrRefreshDuration(
                        sourceIndex,
                        targetIndex,
                        AuraId.FrostTrapSlow,
                        FrostTrapRefreshSeconds,
                        0f,
                        AbilityId.FrostTrap,
                        SpellSchoolMask.Frost);
                }
            }
        }

        private bool ValidateHunterAbilityTarget(
            int actorIndex,
            int targetIndex,
            AbilityDefinition definition,
            out string reason)
        {
            if (!IsHunterAbility(definition.Id))
            {
                reason = null;
                return true;
            }

            CombatantState actor = _combatants[actorIndex];
            int companionIndex = FindCompanionIndex(actor.Id);
            bool companionRequired = definition.Id == AbilityId.PetAttack ||
                definition.Id == AbilityId.PetFollow ||
                definition.Id == AbilityId.PetStay ||
                definition.Id == AbilityId.SpiderWeb ||
                definition.Id == AbilityId.MendPet ||
                definition.Id == AbilityId.MastersCall;
            if (companionRequired &&
                (companionIndex < 0 || !_companions[companionIndex].IsAlive))
            {
                reason = "companion_unavailable";
                return false;
            }

            CompanionState companion = companionIndex >= 0
                ? _companions[companionIndex]
                : default;
            if ((definition.Id == AbilityId.PetAttack ||
                 definition.Id == AbilityId.SpiderWeb ||
                 definition.Id == AbilityId.MastersCall) &&
                companion.ControlEndTick > CurrentTick)
            {
                reason = "companion_controlled";
                return false;
            }

            if (definition.Id == AbilityId.SpiderWeb)
            {
                if (targetIndex < 0 ||
                    !IsEnemy(actor, _combatants[targetIndex]))
                {
                    reason = "invalid_target";
                    return false;
                }

                Float3 targetPosition = _combatants[targetIndex].Position;
                if (Float3.PlanarDistance(companion.Position, targetPosition) >
                    definition.Targeting.Range + 0.001f)
                {
                    reason = "companion_out_of_range";
                    return false;
                }

                if (definition.Targeting.RequiresLineOfSight &&
                    !_rules.CollisionMap.HasLineOfSight(
                        companion.Position,
                        targetPosition))
                {
                    reason = "companion_line_of_sight";
                    return false;
                }
            }

            if (definition.Id == AbilityId.MastersCall && targetIndex >= 0)
            {
                Float3 targetPosition = _combatants[targetIndex].Position;
                if (Float3.PlanarDistance(companion.Position, targetPosition) >
                    definition.Targeting.Range + 0.001f)
                {
                    reason = "companion_out_of_range";
                    return false;
                }

                if (definition.Targeting.RequiresLineOfSight &&
                    !_rules.CollisionMap.HasLineOfSight(
                        companion.Position,
                        targetPosition))
                {
                    reason = "companion_line_of_sight";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        private bool ResolveHunterAbilityBeforeEffects(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition,
            AbilityEffectMoment moment)
        {
            if (moment != AbilityEffectMoment.Completed ||
                !IsHunterAbility(definition.Id))
            {
                return false;
            }

            switch (definition.Id)
            {
                case AbilityId.FreezingArrow:
                    PlaceGroundEffect(
                        actorIndex,
                        command,
                        definition,
                        GroundEffectId.FreezingTrap);
                    return true;
                case AbilityId.FrostTrap:
                    PlaceGroundEffect(
                        actorIndex,
                        command,
                        definition,
                        GroundEffectId.FrostTrap);
                    return true;
                case AbilityId.FeignDeath:
                    ResolveFeignDeath(actorIndex);
                    return true;
                case AbilityId.Disengage:
                    ResolveDisengage(actorIndex);
                    return true;
                case AbilityId.MastersCall:
                    ResolveMastersCall(actorIndex, command.Target);
                    return true;
                case AbilityId.Readiness:
                    ResolveReadiness(actorIndex);
                    return true;
                case AbilityId.PetAttack:
                    SetCompanionOrder(
                        _combatants[actorIndex].Id,
                        CompanionOrder.Attack,
                        command.Target);
                    return true;
                case AbilityId.PetFollow:
                    SetCompanionOrder(
                        _combatants[actorIndex].Id,
                        CompanionOrder.Follow,
                        default);
                    return true;
                case AbilityId.PetStay:
                    SetCompanionOrder(
                        _combatants[actorIndex].Id,
                        CompanionOrder.Stay,
                        default);
                    return true;
                case AbilityId.SpiderWeb:
                    ResolveSpiderWeb(actorIndex, command.Target);
                    return true;
                case AbilityId.MendPet:
                    ResolveMendPet(actorIndex);
                    return true;
                default:
                    return false;
            }
        }

        private void ResolveHunterAbilityAfterEffects(
            int actorIndex,
            int targetIndex,
            AbilityDefinition definition,
            AbilityEffectMoment moment)
        {
            if (moment == AbilityEffectMoment.Completed &&
                definition.Id == AbilityId.ChimeraShot && targetIndex >= 0 &&
                _combatants[targetIndex].IsAlive)
            {
                ResolveChimeraSting(actorIndex, targetIndex);
            }
        }

        private void PlaceGroundEffect(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition,
            GroundEffectId id)
        {
            CombatantState actor = _combatants[actorIndex];
            for (int index = 0; index < _groundEffects.Length; index++)
            {
                if (_groundEffects[index].Active &&
                    _groundEffects[index].Owner == actor.Id)
                {
                    _groundEffects[index] = default;
                }
            }

            int freeIndex = -1;
            for (int index = 0; index < _groundEffects.Length; index++)
            {
                if (!_groundEffects[index].Active)
                {
                    freeIndex = index;
                    break;
                }
            }

            if (freeIndex < 0)
            {
                return;
            }

            Float3 point = definition.Targeting.Shape == AbilityTargetShape.CasterArea
                ? actor.Position
                : command.TargetPoint;
            if ((point - actor.Position).PlanarLengthSquared <=
                MovementCancelThresholdSquared)
            {
                point = actor.Position;
            }

            point = ClampTargetPoint(actor.Position, point, definition.Targeting.Range);
            float fallbackRadius = id == GroundEffectId.FreezingTrap
                ? FreezingTrapRadius
                : FrostTrapRadius;
            float radius = definition.Targeting.Radius > 0f
                ? definition.Targeting.Radius
                : fallbackRadius;
            _groundEffects[freeIndex] = new GroundEffectState
            {
                Active = true,
                Id = id,
                Owner = actor.Id,
                Team = actor.Team,
                Position = point,
                Radius = radius,
                ArmTick = CurrentTick + 1,
                EndTick = CurrentTick + SecondsToTicks(TrapLifetimeSeconds),
            };
        }

        private void ResolveFeignDeath(int actorIndex)
        {
            CombatantState actor = _combatants[actorIndex];
            SetCompanionOrder(actor.Id, CompanionOrder.Follow, default);
            actor.Cast = default;
            ApplyAura(
                actorIndex,
                actorIndex,
                AuraId.FeignDeath,
                1f,
                0f,
                AbilityId.FeignDeath,
                SpellSchoolMask.Physical);
            DropHostileTargeting(actorIndex);
        }

        private void DropHostileTargeting(int actorIndex)
        {
            CombatantState feigningActor = _combatants[actorIndex];
            for (int index = 0; index < _combatantCount; index++)
            {
                CombatantState enemy = _combatants[index];
                if (enemy.IsAlive &&
                    IsEnemy(feigningActor, enemy) &&
                    enemy.Cast.IsActive &&
                    enemy.Cast.Command.Target == feigningActor.Id)
                {
                    CancelCast(index, feigningActor.Id, "targeting_dropped");
                }
            }

            for (int index = 0; index < _companions.Length; index++)
            {
                CompanionState companion = _companions[index];
                if (!companion.Active ||
                    !companion.IsAlive ||
                    companion.Team == feigningActor.Team ||
                    companion.Target != feigningActor.Id)
                {
                    continue;
                }

                companion.Order = CompanionOrder.Follow;
                companion.Target = default;
                _companions[index] = companion;
            }
        }

        private void ResolveDisengage(int actorIndex)
        {
            CombatantState actor = _combatants[actorIndex];
            Float3 desiredPosition = actor.Position -
                (actor.Facing.PlanarNormalized * DisengageDistance);
            actor.Position = _rules.CollisionMap.ResolveBlink(
                actor.Position,
                desiredPosition,
                ArenaLayout.CombatantCollisionRadius);
        }

        private void ResolveMastersCall(int actorIndex, ParticipantId target)
        {
            int targetIndex = FindIndex(target);
            if (targetIndex < 0)
            {
                return;
            }

            CombatantState actor = _combatants[actorIndex];
            RemoveAurasByTag(
                targetIndex,
                AuraTag.Root | AuraTag.Slow,
                actor.Id,
                AbilityId.MastersCall);
            ApplyAura(
                actorIndex,
                targetIndex,
                AuraId.MastersCall,
                MastersCallDurationSeconds,
                0f,
                AbilityId.MastersCall,
                SpellSchoolMask.Physical);

            int companionIndex = FindCompanionIndex(actor.Id);
            if (companionIndex >= 0)
            {
                CompanionState companion = _companions[companionIndex];
                companion.ControlEndTick = 0;
                _companions[companionIndex] = companion;
            }
        }

        private void ResolveReadiness(int actorIndex)
        {
            CombatantState actor = _combatants[actorIndex];
            ResetHunterCooldown(actor, AbilityId.ChimeraShot);
            ResetHunterCooldown(actor, AbilityId.AimedShot);
            ResetHunterCooldown(actor, AbilityId.ArcaneShot);
            ResetHunterCooldown(actor, AbilityId.KillShot);
            ResetHunterCooldown(actor, AbilityId.ScatterShot);
            ResetHunterCooldown(actor, AbilityId.FreezingArrow);
            ResetHunterCooldown(actor, AbilityId.FrostTrap);
            ResetHunterCooldown(actor, AbilityId.SilencingShot);
            ResetHunterCooldown(actor, AbilityId.FeignDeath);
            ResetHunterCooldown(actor, AbilityId.Disengage);
            ResetHunterCooldown(actor, AbilityId.MastersCall);
            ResetHunterCooldown(actor, AbilityId.Deterrence);
            ResetHunterCooldown(actor, AbilityId.RapidFire);
            ResetHunterCooldown(actor, AbilityId.TranquilizingShot);
            ResetHunterCooldown(actor, AbilityId.ConcussiveShot);
            ResetHunterCooldown(actor, AbilityId.Flare);
        }

        private void ResetHunterCooldown(CombatantState actor, AbilityId ability)
        {
            int index = (int)ability;
            if (index > 0 && index < actor.CooldownEndTicks.Length)
            {
                actor.CooldownEndTicks[index] = CurrentTick;
            }
        }

        private void SetCompanionOrder(
            ParticipantId owner,
            CompanionOrder order,
            ParticipantId target)
        {
            int companionIndex = FindCompanionIndex(owner);
            if (companionIndex < 0)
            {
                return;
            }

            CompanionState companion = _companions[companionIndex];
            companion.Order = order;
            companion.Target = order == CompanionOrder.Attack ? target : default;
            companion.StayPoint = companion.Position;
            if (order == CompanionOrder.Attack)
            {
                companion.NextAttackTick = Math.Min(
                    companion.NextAttackTick,
                    CurrentTick);
            }

            _companions[companionIndex] = companion;
        }

        private void ResolveSpiderWeb(int actorIndex, ParticipantId target)
        {
            int targetIndex = FindIndex(target);
            if (targetIndex < 0)
            {
                return;
            }

            ApplyAura(
                actorIndex,
                targetIndex,
                AuraId.SpiderWeb,
                SpiderWebDurationSeconds,
                0f,
                AbilityId.SpiderWeb,
                SpellSchoolMask.Nature);
        }

        private void ResolveMendPet(int actorIndex)
        {
            CombatantState actor = _combatants[actorIndex];
            int companionIndex = FindCompanionIndex(actor.Id);
            if (companionIndex < 0)
            {
                return;
            }

            CompanionState companion = _companions[companionIndex];
            float applied = Math.Min(
                MendPetAmount,
                companion.MaxHealth - companion.Health);
            companion.Health += applied;
            _companions[companionIndex] = companion;
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.Heal,
                actor.Id,
                actor.Id,
                AbilityId.MendPet,
                amount: applied));
        }

        private void ResolveChimeraSting(int actorIndex, int targetIndex)
        {
            CombatantState actor = _combatants[actorIndex];
            CombatantState target = _combatants[targetIndex];
            if (RefreshAuraFromSource(
                    target,
                    AuraId.SerpentSting,
                    actor.Id,
                    SerpentStingDurationSeconds))
            {
                ApplyDamage(
                    actorIndex,
                    targetIndex,
                    ChimeraSerpentDamage,
                    AbilityId.ChimeraShot,
                    false,
                    SpellSchoolMask.Nature);
                return;
            }

            if (RefreshAuraFromSource(
                    target,
                    AuraId.ViperSting,
                    actor.Id,
                    ViperStingDurationSeconds))
            {
                ModifyResource(actorIndex, ResourceType.Mana, ChimeraViperManaReturn);
                return;
            }

            if (!RefreshAuraFromSource(
                    target,
                    AuraId.ScorpidSting,
                    actor.Id,
                    ScorpidStingDurationSeconds) ||
                HasAura(target, AuraId.ChimeraScorpidLockout))
            {
                return;
            }

            ApplyAura(
                actorIndex,
                targetIndex,
                AuraId.Disarm,
                ChimeraScorpidDisarmSeconds,
                0f,
                AbilityId.ChimeraShot,
                SpellSchoolMask.Nature);
            ApplyAura(
                actorIndex,
                targetIndex,
                AuraId.ChimeraScorpidLockout,
                ChimeraScorpidLockoutSeconds,
                0f,
                AbilityId.ChimeraShot,
                SpellSchoolMask.Nature);
        }

        private bool RefreshAuraFromSource(
            CombatantState target,
            AuraId auraId,
            ParticipantId source,
            float durationSeconds)
        {
            int auraIndex = (int)auraId;
            AuraState aura = target.Auras[auraIndex];
            if (!IsAuraActive(aura) || aura.Source != source)
            {
                return false;
            }

            aura.EndTick = CurrentTick + SecondsToTicks(durationSeconds);
            target.Auras[auraIndex] = aura;
            return true;
        }

        private void ApplyAuraOnceOrRefreshDuration(
            int sourceIndex,
            int targetIndex,
            AuraId auraId,
            float durationSeconds,
            float magnitude,
            AbilityId ability,
            SpellSchoolMask originatingSchool)
        {
            CombatantState target = _combatants[targetIndex];
            int auraIndex = (int)auraId;
            AuraState aura = target.Auras[auraIndex];
            if (IsAuraActive(aura))
            {
                aura.EndTick = CurrentTick + SecondsToTicks(durationSeconds);
                target.Auras[auraIndex] = aura;
                return;
            }

            ApplyAura(
                sourceIndex,
                targetIndex,
                auraId,
                durationSeconds,
                magnitude,
                ability,
                originatingSchool);
        }

        private bool IsBlockedByDeterrence(
            int sourceIndex,
            int targetIndex,
            AbilityId ability)
        {
            CombatantState target = _combatants[targetIndex];
            return !IsPeriodicDamageAbility(ability) &&
                HasAura(target, AuraId.Deterrence) && IsFacing(
                target.Position,
                target.Facing,
                _combatants[sourceIndex].Position);
        }

        private static bool IsPeriodicDamageAbility(AbilityId ability)
        {
            return ability == AbilityId.LivingBomb ||
                ability == AbilityId.VampiricTouch ||
                ability == AbilityId.ShadowWordPain ||
                ability == AbilityId.DevouringPlague ||
                ability == AbilityId.SerpentSting;
        }

        private void MirrorDamageToRoarCompanion(int targetIndex, float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            CombatantState target = _combatants[targetIndex];
            int auraIndex = (int)AuraId.RoarOfSacrifice;
            AuraState roar = target.Auras[auraIndex];
            if (!IsAuraActive(roar))
            {
                return;
            }

            int companionIndex = FindCompanionIndex(roar.Source);
            if (companionIndex < 0 || !_companions[companionIndex].IsAlive)
            {
                return;
            }

            DamageCompanion(companionIndex, amount * RoarRedirectFraction);
        }

        private void DamageCompanion(int companionIndex, float amount)
        {
            CompanionState companion = _companions[companionIndex];
            if (!companion.Active || !companion.IsAlive || amount <= 0f)
            {
                return;
            }

            companion.Health = Math.Max(0f, companion.Health - amount);
            if (companion.Health <= 0f)
            {
                companion.IsAlive = false;
                companion.Order = CompanionOrder.Follow;
                companion.Target = default;
            }

            _companions[companionIndex] = companion;
            if (!companion.IsAlive)
            {
                EndRoarOfSacrifice(companion.Owner);
            }
        }

        private void EndRoarOfSacrifice(ParticipantId owner)
        {
            for (int targetIndex = 0; targetIndex < _combatantCount; targetIndex++)
            {
                AuraState roar = _combatants[targetIndex].Auras[
                    (int)AuraId.RoarOfSacrifice];
                if (IsAuraActive(roar) && roar.Source == owner)
                {
                    RemoveAura(
                        targetIndex,
                        AuraId.RoarOfSacrifice,
                        owner,
                        AbilityId.RoarOfSacrifice,
                        false);
                }
            }
        }

        private int FindCompanionIndex(ParticipantId owner)
        {
            for (int index = 0; index < _companions.Length; index++)
            {
                if (_companions[index].Active && _companions[index].Owner == owner)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindFreeCompanionIndex()
        {
            for (int index = 0; index < _companions.Length; index++)
            {
                if (!_companions[index].Active)
                {
                    return index;
                }
            }

            return -1;
        }

        private bool HasLivingCompanion(ParticipantId owner)
        {
            int index = FindCompanionIndex(owner);
            return index >= 0 && _companions[index].IsAlive;
        }

        private bool CanHunterSeeMarkedTarget(
            CombatantState actor,
            CombatantState target)
        {
            if (actor.Class != ClassId.Hunter)
            {
                return false;
            }

            AuraState mark = target.Auras[(int)AuraId.HuntersMark];
            return IsAuraActive(mark) && mark.Source == actor.Id;
        }

        private static CompanionSnapshot CreateCompanionSnapshot(CompanionState companion)
        {
            return new CompanionSnapshot(
                companion.Owner,
                companion.Team,
                companion.Position,
                companion.Facing,
                companion.Health,
                companion.MaxHealth,
                companion.IsAlive,
                companion.Order,
                companion.Target);
        }

        private static bool IsHunterAbility(AbilityId ability)
        {
            int value = (int)ability;
            return value >= (int)AbilityId.ChimeraShot &&
                value <= (int)AbilityId.AspectOfTheViper;
        }

        private static bool IsHostileAbility(AbilityDefinition definition)
        {
            return definition.IsHostile;
        }

        private struct CompanionState
        {
            public bool Active;
            public ParticipantId Owner;
            public TeamId Team;
            public Float3 Position;
            public Float3 Facing;
            public float Health;
            public float MaxHealth;
            public bool IsAlive;
            public CompanionOrder Order;
            public ParticipantId Target;
            public Float3 StayPoint;
            public int NextAttackTick;
            public int ControlEndTick;
        }

        private struct GroundEffectState
        {
            public bool Active;
            public GroundEffectId Id;
            public ParticipantId Owner;
            public TeamId Team;
            public Float3 Position;
            public float Radius;
            public int ArmTick;
            public int EndTick;
        }
    }
}
