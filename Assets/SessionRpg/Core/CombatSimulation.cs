using System;

namespace SessionRpg.Core
{
    /// <summary>
    /// Deterministic, server-authoritative planar combat simulation. The simulation
    /// owns combat outcomes and uses fixed-capacity storage so a warmed-up tick does
    /// not allocate. Optional arena cover is resolved here so movement, displacement,
    /// teleports, and line of sight cannot disagree with combat outcomes.
    /// </summary>
    public sealed partial class CombatSimulation
    {
        public const int MaxCombatants = 12;
        public const int MaxEventsPerTick = 256;

        private const int MaxQueuedCommands = 128;
        private const int CooldownCapacity = AbilityCatalog.StorageCapacity;
        private const int StatusCapacity = (int)StatusId.Pacified + 1;
        private const int SchoolCapacity = 7;
        private const int DiminishingReturnCapacity =
            (int)DiminishingReturnCategory.Disarm + 1;
        private const float MinimumPullSeparation = 1.25f;
        private const float MovementCancelThresholdSquared = 0.0001f;
        private const float FacingThreshold = -0.05f;
        private const float BehindThreshold = -0.25f;
        private const float ShadowstepSeparation = 1.25f;
        private const float FamiliarDamage = 8f;
        private const float FamiliarManaReturn = 5f;
        private const float FamiliarPulseSeconds = 1.5f;

        private static readonly StatusId[] LegacyHarmfulStatuses =
        {
            StatusId.Silenced,
            StatusId.Slowed,
        };

        private readonly CombatRules _rules;
        private readonly CombatantState[] _combatants = new CombatantState[MaxCombatants];
        private readonly CombatCommand[] _commands = new CombatCommand[MaxQueuedCommands];
        private readonly CombatEvent[] _events = new CombatEvent[MaxEventsPerTick];
        private readonly SummonState[] _summons = new SummonState[MaxCombatants];

        private int _combatantCount;
        private int _commandCount;
        private int _eventCount;

        public CombatSimulation(CombatRules rules)
        {
            _rules = rules;
            for (int index = 0; index < _combatants.Length; index++)
            {
                _combatants[index] = new CombatantState();
            }
        }

        public int CurrentTick { get; private set; }

        public int CombatantCount => _combatantCount;

        public int EventCount => _eventCount;

        public CombatRules Rules => _rules;

        /// <summary>
        /// Compatibility overload. It exposes the class default specialization but
        /// also accepts the original five-action kit and preserves shared legacy IDs.
        /// New match setup should use the specialization-aware overload.
        /// </summary>
        public bool AddCombatant(
            ParticipantId id,
            TeamId team,
            ClassId classId,
            Float3 position,
            float maxHealth = 100f)
        {
            return AddCombatantInternal(
                id,
                team,
                classId,
                SpecializationCatalog.GetDefault(classId),
                position,
                maxHealth,
                true);
        }

        public bool AddCombatant(
            ParticipantId id,
            TeamId team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            float maxHealth = 100f)
        {
            return AddCombatantInternal(
                id,
                team,
                classId,
                specialization,
                position,
                maxHealth,
                false,
                true);
        }

        /// <summary>
        /// Adds a specialization-aware combatant while allowing authored NPC
        /// encounters to suppress player-only opening state such as Rogue stealth
        /// or the Marksmanship companion. Specialization passives still apply.
        /// </summary>
        public bool AddCombatant(
            ParticipantId id,
            TeamId team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            float maxHealth,
            bool applyClassOpeningState)
        {
            return AddCombatantInternal(
                id,
                team,
                classId,
                specialization,
                position,
                maxHealth,
                false,
                applyClassOpeningState);
        }

        public bool AddCombatant(
            ParticipantId id,
            TeamId team,
            ClassId classId,
            Float3 position,
            SpecializationId specialization,
            float maxHealth = 100f)
        {
            return AddCombatant(
                id,
                team,
                classId,
                specialization,
                position,
                maxHealth);
        }

        public bool SubmitCommand(CombatCommand command)
        {
            if (_commandCount >= MaxQueuedCommands || FindIndex(command.Actor) < 0)
            {
                return false;
            }

            _commands[_commandCount++] = command;
            return true;
        }

        public void Tick()
        {
            CurrentTick++;
            _eventCount = 0;

            RegenerateResources();
            ProcessAuras();
            ProcessSummons();
            ExpireLegacyStatusesAndSchoolLocks();
            SortCommands();
            ProcessCommands();
            AdvanceCasts();
            ApplyMovement();
            ProcessGroundEffects();
            ProcessCompanions();
            ApplyHazards();
            _commandCount = 0;
        }

        public CombatEvent GetEvent(int index)
        {
            if (index < 0 || index >= _eventCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _events[index];
        }

        public bool TryGetSnapshot(ParticipantId id, out CombatantSnapshot snapshot)
        {
            int index = FindIndex(id);
            if (index < 0)
            {
                snapshot = default;
                return false;
            }

            snapshot = CreateSnapshot(_combatants[index]);
            return true;
        }

        /// <summary>
        /// Applies a deterministic progression level to a living combatant. A
        /// level can only move forward. Each gained level adds eight maximum
        /// health, then restores health and the combatant's authored resource.
        /// </summary>
        public bool ApplyProgressionLevel(ParticipantId id, int level)
        {
            if (_rules.PlayableMode == PlayableModeId.PveLeveling)
            {
                return false;
            }

            int index = FindIndex(id);
            if (index < 0)
            {
                return false;
            }

            CombatantState actor = _combatants[index];
            int nextLevel = LevelProgressionCurve.ClampLevel(level);
            if (!actor.IsAlive || nextLevel <= actor.Level)
            {
                return false;
            }

            int levelsGained = nextLevel - actor.Level;
            actor.Level = nextLevel;
            actor.MaxHealth += levelsGained *
                LevelProgressionCurve.HealthPerGainedLevel;
            actor.Health = actor.MaxHealth;
            actor.Resource = actor.ResourceProfile.Maximum;
            return true;
        }

        /// <summary>
        /// Applies the independent persistent PvE curve only in its explicit
        /// mode. Gaining a level restores health and resources; loading a saved
        /// character uses the same authoritative mutation after initial spawn.
        /// </summary>
        public bool ApplyPersistentProgressionLevel(ParticipantId id, int level)
        {
            if (_rules.PlayableMode != PlayableModeId.PveLeveling ||
                level < PersistentLevelProgression.MinimumLevel ||
                level > PersistentLevelProgression.MaximumLevel)
            {
                return false;
            }

            int index = FindIndex(id);
            if (index < 0)
            {
                return false;
            }

            CombatantState actor = _combatants[index];
            if (!actor.IsAlive || level <= actor.Level)
            {
                return false;
            }

            actor.MaxHealth += (level - actor.Level) *
                PersistentLevelProgression.HealthPerGainedLevel;
            actor.Level = level;
            actor.Health = actor.MaxHealth;
            actor.Resource = actor.ResourceProfile.Maximum;
            return true;
        }

        /// <summary>
        /// Removes one defeated combatant so a persistent authored run can reuse
        /// the bounded simulation storage. Living actors are never removable and
        /// participant identities remain the caller's responsibility: a run must
        /// never add the same identity again after this succeeds.
        /// </summary>
        public bool TryRemoveDefeatedCombatant(ParticipantId id)
        {
            int removedIndex = FindIndex(id);
            if (removedIndex < 0 || _combatants[removedIndex].IsAlive)
            {
                return false;
            }

            ReleaseCombatant(removedIndex, id);
            return true;
        }

        /// <summary>
        /// Retires an evaded persistent-region NPC without inventing a death or
        /// reward. No other mode or friendly participant admits live despawning.
        /// The caller must never reuse its participant identity in this simulation.
        /// </summary>
        public bool TryDespawnRegionCombatant(ParticipantId id)
        {
            if (_rules.PlayableMode != PlayableModeId.PveLeveling || id.Value == 1)
            {
                return false;
            }

            int removedIndex = FindIndex(id);
            if (removedIndex < 0 || _combatants[removedIndex].Team == new TeamId(1))
            {
                return false;
            }

            ReleaseCombatant(removedIndex, id);
            return true;
        }

        private void ReleaseCombatant(int removedIndex, ParticipantId id)
        {
            ClearParticipantOwnedState(id);
            ClearHunterParticipantState(id);

            CombatantState released = _combatants[removedIndex];
            for (int index = removedIndex; index < _combatantCount - 1; index++)
            {
                _combatants[index] = _combatants[index + 1];
            }

            _combatantCount--;
            released.ClearForPool();
            _combatants[_combatantCount] = released;
        }

        /// <summary>
        /// Applies an authoritative run consumable without inventing an ability.
        /// The Application layer owns price and inventory policy; Core owns the
        /// clamped combat-state mutation.
        /// </summary>
        public bool TryRestoreHealth(
            ParticipantId id,
            float amount,
            out float restored)
        {
            restored = 0f;
            int index = FindIndex(id);
            if (index < 0 || !IsFinitePositive(amount))
            {
                return false;
            }

            CombatantState actor = _combatants[index];
            if (!actor.IsAlive || actor.Health >= actor.MaxHealth - 0.0001f)
            {
                return false;
            }

            restored = Math.Min(amount, actor.MaxHealth - actor.Health);
            actor.Health += restored;
            return restored > 0f;
        }

        public bool TryRestoreResource(
            ParticipantId id,
            ResourceType type,
            float amount,
            out float restored)
        {
            restored = 0f;
            int index = FindIndex(id);
            if (index < 0 || type == ResourceType.None || !IsFinitePositive(amount))
            {
                return false;
            }

            CombatantState actor = _combatants[index];
            if (!actor.IsAlive || actor.ResourceProfile.Type != type ||
                actor.Resource >= actor.ResourceProfile.Maximum - 0.0001f)
            {
                return false;
            }

            restored = Math.Min(
                amount,
                actor.ResourceProfile.Maximum - actor.Resource);
            actor.Resource += restored;
            return restored > 0f;
        }

        public bool TrySetRunCombatModifiers(
            ParticipantId id,
            int damageMultiplierPermille,
            int movementSpeedMultiplierPermille)
        {
            int index = FindIndex(id);
            if (index < 0 || damageMultiplierPermille < 250 ||
                damageMultiplierPermille > 4000 ||
                movementSpeedMultiplierPermille < 250 ||
                movementSpeedMultiplierPermille > 4000)
            {
                return false;
            }

            CombatantState actor = _combatants[index];
            if (!actor.IsAlive)
            {
                return false;
            }

            actor.DamageMultiplierPermille = damageMultiplierPermille;
            actor.MovementSpeedMultiplierPermille =
                movementSpeedMultiplierPermille;
            return true;
        }

        public int CopySnapshots(CombatantSnapshot[] destination)
        {
            if (destination == null)
            {
                throw new ArgumentNullException(nameof(destination));
            }

            int count = Math.Min(destination.Length, _combatantCount);
            for (int index = 0; index < count; index++)
            {
                destination[index] = CreateSnapshot(_combatants[index]);
            }

            return count;
        }

        public float GetCooldownRemaining(ParticipantId actor, AbilityId ability)
        {
            int index = FindIndex(actor);
            int abilityIndex = (int)ability;
            if (index < 0 || abilityIndex <= 0 || abilityIndex >= CooldownCapacity)
            {
                return 0f;
            }

            int ticks = Math.Max(
                0,
                _combatants[index].CooldownEndTicks[abilityIndex] - CurrentTick);
            return ticks * _rules.TickSeconds;
        }

        public float GetGlobalCooldownRemaining(ParticipantId actor)
        {
            int index = FindIndex(actor);
            if (index < 0)
            {
                return 0f;
            }

            int ticks = Math.Max(
                0,
                _combatants[index].GlobalCooldownEndTick - CurrentTick);
            return ticks * _rules.TickSeconds;
        }

        public bool CanUseAbility(
            ParticipantId actor,
            AbilityId ability,
            out string reason)
        {
            int actorIndex = FindIndex(actor);
            if (actorIndex < 0)
            {
                reason = "unknown_actor";
                return false;
            }

            if (!AbilityCatalog.TryGet(ability, out AbilityDefinition definition))
            {
                reason = "unknown_ability";
                return false;
            }

            return CanUseAbility(actorIndex, definition, out reason);
        }

        public bool CanStartAbility(CombatCommand command, out string reason)
        {
            int actorIndex = FindIndex(command.Actor);
            if (actorIndex < 0)
            {
                reason = "unknown_actor";
                return false;
            }

            if (!AbilityCatalog.TryGet(command.Ability, out AbilityDefinition definition))
            {
                reason = "unknown_ability";
                return false;
            }

            return ValidateAbilityStart(
                actorIndex,
                command,
                definition,
                out _,
                out _,
                out reason);
        }

        public bool HasAura(ParticipantId actor, AuraId aura)
        {
            int index = FindIndex(actor);
            int auraIndex = (int)aura;
            return index >= 0 && auraIndex > 0 && auraIndex < AuraCatalog.StorageCapacity &&
                   IsAuraActive(_combatants[index].Auras[auraIndex]);
        }

        public float GetAuraRemaining(ParticipantId actor, AuraId aura)
        {
            int index = FindIndex(actor);
            int auraIndex = (int)aura;
            if (index < 0 || auraIndex <= 0 || auraIndex >= AuraCatalog.StorageCapacity)
            {
                return 0f;
            }

            AuraState state = _combatants[index].Auras[auraIndex];
            int ticks = state.Active ? Math.Max(0, state.EndTick - CurrentTick) : 0;
            return ticks * _rules.TickSeconds;
        }

        public int GetAuraStacks(ParticipantId actor, AuraId aura)
        {
            int index = FindIndex(actor);
            int auraIndex = (int)aura;
            if (index < 0 || auraIndex <= 0 || auraIndex >= AuraCatalog.StorageCapacity)
            {
                return 0;
            }

            AuraState state = _combatants[index].Auras[auraIndex];
            return IsAuraActive(state) ? state.Stacks : 0;
        }

        private bool AddCombatantInternal(
            ParticipantId id,
            TeamId team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            float maxHealth,
            bool legacyCompatibility,
            bool applyClassOpeningState = true)
        {
            if (!id.IsValid || classId == ClassId.None || maxHealth <= 0f ||
                _combatantCount >= MaxCombatants || FindIndex(id) >= 0 ||
                !SpecializationCatalog.IsCompatible(classId, specialization))
            {
                return false;
            }

            position = _rules.CollisionMap.ResolvePlacement(
                position,
                ArenaLayout.CombatantCollisionRadius,
                position);

            CombatantState addedCombatant = _combatants[_combatantCount];
            addedCombatant.Reset(
                id,
                team,
                classId,
                specialization,
                position,
                maxHealth,
                legacyCompatibility);
            _combatantCount++;
            if (!legacyCompatibility)
            {
                ApplySpecializationPassives(addedCombatant);
            }

            if (!legacyCompatibility && applyClassOpeningState &&
                classId == ClassId.Rogue)
            {
                CombatantState rogue = addedCombatant;
                rogue.Auras[(int)AuraId.Stealth] = new AuraState(
                    true,
                    id,
                    AbilityId.None,
                    int.MaxValue,
                    0,
                    1,
                    0f);
            }

            if (!legacyCompatibility && applyClassOpeningState &&
                specialization == SpecializationId.Marksmanship)
            {
                SpawnCompanion(addedCombatant);
            }

            return true;
        }

        private void RegenerateResources()
        {
            for (int index = 0; index < _combatantCount; index++)
            {
                CombatantState actor = _combatants[index];
                if (!actor.IsAlive || actor.ResourceProfile.Type == ResourceType.None)
                {
                    continue;
                }

                actor.Resource = Math.Min(
                    actor.ResourceProfile.Maximum,
                    actor.Resource +
                    (actor.ResourceProfile.RegenerationPerSecond * _rules.TickSeconds));
            }
        }

        private void ProcessCommands()
        {
            for (int commandIndex = 0; commandIndex < _commandCount; commandIndex++)
            {
                CombatCommand command = _commands[commandIndex];
                int actorIndex = FindIndex(command.Actor);
                if (actorIndex < 0)
                {
                    continue;
                }

                CombatantState actor = _combatants[actorIndex];
                if (!actor.IsAlive || command.Sequence <= actor.LastSequence)
                {
                    if (command.HasAbility)
                    {
                        Emit(CombatEvent.Create(
                            CurrentTick,
                            CombatEventKind.CastRejected,
                            command.Actor,
                            ability: command.Ability,
                            reason: actor.IsAlive ? "stale_sequence" : "dead"));
                    }

                    continue;
                }

                actor.LastSequence = command.Sequence;
                actor.MoveIntent = ClampPlanar(command.Movement, 1f);
                if (command.Facing.PlanarLengthSquared > MovementCancelThresholdSquared)
                {
                    actor.Facing = command.Facing.PlanarNormalized;
                }

                if (actor.Cast.IsActive && actor.MoveIntent.PlanarLengthSquared >
                    MovementCancelThresholdSquared)
                {
                    CancelCast(actorIndex, actor.Id, "movement");
                }

                if (command.HasAbility)
                {
                    TryStartAbility(actorIndex, command);
                }
            }
        }

        private void TryStartAbility(int actorIndex, CombatCommand command)
        {
            if (!AbilityCatalog.TryGet(command.Ability, out AbilityDefinition definition))
            {
                Reject(command, "unknown_ability");
                return;
            }

            if (!ValidateAbilityStart(
                    actorIndex,
                    command,
                    definition,
                    out ParticipantId resolvedTarget,
                    out Float3 resolvedPoint,
                    out string reason))
            {
                Reject(command, reason);
                return;
            }

            CombatantState actor = _combatants[actorIndex];
            if (definition.Id == AbilityId.FeignDeath && actor.Cast.IsActive)
            {
                CancelCast(actorIndex, actor.Id, "feign_death");
            }

            bool modernMechanics = UsesModernMechanics(actor, definition);
            Float3 aimDirection = command.AimDirection.PlanarLengthSquared >
                MovementCancelThresholdSquared
                    ? command.AimDirection.PlanarNormalized
                    : actor.Facing;
            CombatCommand resolvedCommand = CombatCommand.Cast(
                command.Actor,
                command.Sequence,
                command.Movement,
                command.Facing,
                command.Ability,
                resolvedTarget,
                resolvedPoint,
                aimDirection);

            actor.LastAbilityStartTick = CurrentTick;
            float cooldownSeconds = modernMechanics && definition.Id == AbilityId.Ambush
                ? 0f
                : definition.CooldownSeconds;
            actor.CooldownEndTicks[(int)definition.Id] =
                CurrentTick + SecondsToTicks(cooldownSeconds);
            if (modernMechanics)
            {
                SpendResource(actor, definition.ResourceCost);
                StartGlobalCooldown(actor, definition);
                if ((definition.Requirements & AbilityRequirementFlags.BreaksStealth) != 0)
                {
                    RemoveAura(actorIndex, AuraId.Stealth, actor.Id, definition.Id, false);
                }
            }

            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.CastAccepted,
                actor.Id,
                resolvedTarget,
                definition.Id,
                vector: resolvedPoint));

            if (modernMechanics)
            {
                BeginModernAbility(actorIndex, resolvedCommand, definition);
            }
            else
            {
                BeginLegacyAbility(actorIndex, resolvedCommand, definition);
            }
        }

        private bool ValidateAbilityStart(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition,
            out ParticipantId resolvedTarget,
            out Float3 resolvedPoint,
            out string reason)
        {
            resolvedTarget = default;
            resolvedPoint = default;
            if (!CanUseAbility(actorIndex, definition, out reason))
            {
                return false;
            }

            CombatantState actor = _combatants[actorIndex];
            bool modernMechanics = UsesModernMechanics(actor, definition);
            if (actor.LastAbilityStartTick == CurrentTick)
            {
                reason = "ability_rate";
                return false;
            }

            if (definition.CastProfile.ActivationType != AbilityActivationType.Instant &&
                command.Movement.PlanarLengthSquared > MovementCancelThresholdSquared)
            {
                reason = "moving";
                return false;
            }

            bool targetValid = modernMechanics
                ? ValidateTarget(
                    actorIndex,
                    command,
                    definition,
                    out resolvedTarget,
                    out resolvedPoint,
                    out reason)
                : ValidateLegacyTarget(
                    actorIndex,
                    command,
                    definition,
                    out resolvedTarget,
                    out resolvedPoint,
                    out reason);
            if (!targetValid)
            {
                return false;
            }

            if (modernMechanics && !ValidateTargetRequirements(
                    actorIndex,
                    FindIndex(resolvedTarget),
                    definition,
                    out reason))
            {
                return false;
            }

            return !modernMechanics || ValidateHunterAbilityTarget(
                actorIndex,
                FindIndex(resolvedTarget),
                definition,
                out reason);
        }

        private bool CanUseAbility(
            int actorIndex,
            AbilityDefinition definition,
            out string reason)
        {
            CombatantState actor = _combatants[actorIndex];
            if (!actor.IsAlive)
            {
                reason = "dead";
                return false;
            }

            if (definition.OwnerClass != ClassId.None &&
                definition.OwnerClass != actor.Class)
            {
                reason = "wrong_class";
                return false;
            }

            bool legacyAbility = (int)definition.Id <= (int)AbilityId.BossKnockback;
            bool usesProgressionKit =
                _rules.AbilityProgressionTeam.Value > 0 &&
                actor.Team == _rules.AbilityProgressionTeam;
            int requiredProgressionLevel = 0;
            bool isProgressionAbility = usesProgressionKit &&
                LevelingAbilityUnlockCatalog.TryGetRequiredLevel(
                    actor.Specialization,
                    definition.Id,
                    out requiredProgressionLevel);
            bool allowedBySpec = isProgressionAbility ||
                AbilityCatalog.IsAbilityAllowed(
                    actor.Class,
                    actor.Specialization,
                    definition.Id,
                    _rules.PlayableMode);
            if (!allowedBySpec && !(actor.LegacyCompatibility && legacyAbility))
            {
                reason = "wrong_specialization";
                return false;
            }

            if (usesProgressionKit &&
                (!isProgressionAbility ||
                 actor.Level < requiredProgressionLevel))
            {
                reason = "level_locked";
                return false;
            }

            if ((definition.Requirements &
                 AbilityRequirementFlags.RequiresNoLivingHostile) != 0 &&
                HasLivingHostile(actor))
            {
                reason = "in_combat";
                return false;
            }

            if ((definition.Requirements &
                 AbilityRequirementFlags.RequiresNoShadowform) != 0 &&
                HasAura(actor, AuraId.Shadowform))
            {
                reason = "shadowform";
                return false;
            }

            if (actor.CooldownEndTicks[(int)definition.Id] > CurrentTick)
            {
                reason = "cooldown";
                return false;
            }

            if (actor.Cast.IsActive && definition.Id != AbilityId.FeignDeath)
            {
                reason = "already_casting";
                return false;
            }

            bool usableWhileHardControlled = (definition.Requirements &
                AbilityRequirementFlags.UsableWhileHardControlled) != 0;
            bool usableWhileStunned = usableWhileHardControlled ||
                (definition.Requirements & AbilityRequirementFlags.UsableWhileStunned) != 0;
            if (!usableWhileStunned && HasControlTag(actor, AuraTag.Stun))
            {
                reason = "stunned";
                return false;
            }

            if (!usableWhileHardControlled && HasControlTag(actor, AuraTag.Fear))
            {
                reason = "feared";
                return false;
            }

            if (!usableWhileHardControlled && HasControlTag(actor, AuraTag.Disorient))
            {
                reason = "disoriented";
                return false;
            }

            if (!usableWhileHardControlled && HasControlTag(actor, AuraTag.Horror))
            {
                reason = "horrified";
                return false;
            }

            bool usableWhileSilenced = (definition.Requirements &
                AbilityRequirementFlags.UsableWhileSilenced) != 0;
            if (!usableWhileSilenced &&
                (actor.StatusEndTicks[(int)StatusId.Silenced] > CurrentTick ||
                 HasControlTag(actor, AuraTag.Silence)))
            {
                reason = "silenced";
                return false;
            }

            if (HasControlTag(actor, AuraTag.Pacified) && IsHostileAbility(definition))
            {
                reason = "pacified";
                return false;
            }

            if (HasControlTag(actor, AuraTag.AbilityLock))
            {
                reason = "pacified";
                return false;
            }

            if (HasControlTag(actor, AuraTag.Disarm) &&
                AbilityCatalog.IsWeaponAbility(definition.Id))
            {
                reason = "disarmed";
                return false;
            }

            if ((definition.Requirements &
                 AbilityRequirementFlags.RequiresLivingCompanion) != 0 &&
                !HasLivingCompanion(actor.Id))
            {
                reason = "companion_unavailable";
                return false;
            }

            bool modernMechanics = UsesModernMechanics(actor, definition);
            if (modernMechanics && IsSchoolLocked(actor, definition.School))
            {
                reason = "school_locked";
                return false;
            }

            bool ignoresGlobalCooldown = (definition.Requirements &
                AbilityRequirementFlags.IgnoresGlobalCooldown) != 0;
            if (modernMechanics && !ignoresGlobalCooldown &&
                actor.GlobalCooldownEndTick > CurrentTick)
            {
                reason = "global_cooldown";
                return false;
            }

            if (modernMechanics && definition.ResourceCost.Type != ResourceType.None &&
                (actor.ResourceProfile.Type != definition.ResourceCost.Type ||
                 actor.Resource + 0.0001f < definition.ResourceCost.Amount))
            {
                reason = "resource";
                return false;
            }

            reason = null;
            return true;
        }

        private bool ValidateLegacyTarget(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition,
            out ParticipantId resolvedTarget,
            out Float3 resolvedPoint,
            out string reason)
        {
            CombatantState actor = _combatants[actorIndex];
            resolvedTarget = command.Target;
            resolvedPoint = command.TargetPoint;
            if (definition.TargetMode == AbilityTargetMode.Self)
            {
                resolvedTarget = actor.Id;
                resolvedPoint = actor.Position;
                reason = null;
                return true;
            }

            if (definition.TargetMode == AbilityTargetMode.Point ||
                definition.TargetMode == AbilityTargetMode.Direction ||
                definition.TargetMode == AbilityTargetMode.EnemyArea)
            {
                resolvedPoint = ClampTargetPoint(
                    actor.Position,
                    command.TargetPoint,
                    definition.Range);
                if (definition.Targeting.RequiresLineOfSight &&
                    !_rules.CollisionMap.HasLineOfSight(
                        actor.Position,
                        resolvedPoint))
                {
                    reason = "line_of_sight";
                    return false;
                }

                reason = null;
                return true;
            }

            int targetIndex = FindIndex(command.Target);
            if (targetIndex < 0 && definition.TargetMode == AbilityTargetMode.Ally &&
                definition.Id != AbilityId.Rescue)
            {
                resolvedTarget = actor.Id;
                targetIndex = actorIndex;
            }

            if (targetIndex < 0 || !_combatants[targetIndex].IsAlive)
            {
                reason = "invalid_target";
                return false;
            }

            bool sameTeam = actor.Team == _combatants[targetIndex].Team;
            if ((definition.TargetMode == AbilityTargetMode.Ally && !sameTeam) ||
                (definition.TargetMode == AbilityTargetMode.Enemy && sameTeam) ||
                (definition.Id == AbilityId.Rescue && targetIndex == actorIndex))
            {
                reason = "invalid_target";
                return false;
            }

            if (!sameTeam &&
                HasAuraTag(_combatants[targetIndex], AuraTag.TargetingDropped))
            {
                reason = "targeting_dropped";
                return false;
            }

            if (Float3.PlanarDistance(actor.Position, _combatants[targetIndex].Position) >
                definition.Range + 0.001f)
            {
                reason = "invalid_target";
                return false;
            }

            if (definition.Targeting.RequiresLineOfSight &&
                !_rules.CollisionMap.HasLineOfSight(
                    actor.Position,
                    _combatants[targetIndex].Position))
            {
                reason = "line_of_sight";
                return false;
            }

            resolvedPoint = _combatants[targetIndex].Position;
            reason = null;
            return true;
        }

        private bool ValidateTarget(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition,
            out ParticipantId resolvedTarget,
            out Float3 resolvedPoint,
            out string reason)
        {
            CombatantState actor = _combatants[actorIndex];
            AbilityTargetRule targeting = definition.Targeting;
            resolvedTarget = command.Target;
            resolvedPoint = command.TargetPoint;

            if (targeting.Shape == AbilityTargetShape.CasterArea)
            {
                resolvedTarget = actor.Id;
                resolvedPoint = actor.Position;
                reason = null;
                return true;
            }

            if (targeting.Shape == AbilityTargetShape.Point ||
                targeting.Shape == AbilityTargetShape.Direction ||
                targeting.Shape == AbilityTargetShape.TargetArea)
            {
                if (targeting.Shape == AbilityTargetShape.Direction &&
                    command.TargetPoint.PlanarLengthSquared <=
                    MovementCancelThresholdSquared)
                {
                    resolvedPoint = actor.Position +
                        (command.AimDirection.PlanarNormalized * targeting.Range);
                }

                resolvedPoint = ClampTargetPoint(
                    actor.Position,
                    resolvedPoint,
                    targeting.Range);
                if (targeting.RequiresLineOfSight &&
                    !_rules.CollisionMap.HasLineOfSight(
                        actor.Position,
                        resolvedPoint))
                {
                    reason = "line_of_sight";
                    return false;
                }

                reason = null;
                return true;
            }

            if (!resolvedTarget.IsValid && targeting.Allows(AbilityTargetRelation.Self))
            {
                resolvedTarget = actor.Id;
            }

            int targetIndex = FindIndex(resolvedTarget);
            if (targetIndex < 0 || !_combatants[targetIndex].IsAlive)
            {
                reason = "invalid_target";
                return false;
            }

            CombatantState target = _combatants[targetIndex];
            AbilityTargetRelation relation = GetRelation(actor, target);
            if (!targeting.Allows(relation))
            {
                reason = "invalid_target";
                return false;
            }

            if (relation == AbilityTargetRelation.Hostile &&
                HasAuraTag(target, AuraTag.TargetingDropped))
            {
                reason = "targeting_dropped";
                return false;
            }

            if (relation == AbilityTargetRelation.Hostile &&
                HasAuraTag(target, AuraTag.Stealth) &&
                !CanHunterSeeMarkedTarget(actor, target))
            {
                reason = "target_hidden";
                return false;
            }

            if (definition.Id == AbilityId.Rescue && actorIndex == targetIndex)
            {
                reason = "invalid_target";
                return false;
            }

            bool companionOrigin = definition.Id == AbilityId.SpiderWeb ||
                definition.Id == AbilityId.MastersCall;
            if (!companionOrigin)
            {
                float distance = Float3.PlanarDistance(actor.Position, target.Position);
                if (distance > targeting.Range + 0.001f)
                {
                    reason = "out_of_range";
                    return false;
                }

                if (distance + 0.001f < targeting.MinimumRange)
                {
                    reason = "minimum_range";
                    return false;
                }

                if (targeting.RequiresLineOfSight &&
                    !_rules.CollisionMap.HasLineOfSight(actor.Position, target.Position))
                {
                    reason = "line_of_sight";
                    return false;
                }
            }

            Float3 effectiveFacing = command.Facing.PlanarLengthSquared >
                MovementCancelThresholdSquared
                    ? command.Facing.PlanarNormalized
                    : actor.Facing;
            if (!companionOrigin && targeting.RequiresFacing && !IsFacing(
                    actor.Position,
                    effectiveFacing,
                    target.Position))
            {
                reason = "not_facing";
                return false;
            }

            resolvedPoint = target.Position;
            reason = null;
            return true;
        }

        private bool ValidateTargetRequirements(
            int actorIndex,
            int targetIndex,
            AbilityDefinition definition,
            out string reason)
        {
            CombatantState actor = _combatants[actorIndex];
            AbilityRequirementFlags requirements = definition.Requirements;
            if ((requirements & AbilityRequirementFlags.RequiresActiveEnemyCast) != 0)
            {
                if (targetIndex < 0 || !IsEnemy(actor, _combatants[targetIndex]) ||
                    !_combatants[targetIndex].Cast.IsActive)
                {
                    reason = "target_not_casting";
                    return false;
                }
            }

            if ((requirements & AbilityRequirementFlags.RequiresStealthAccess) != 0 &&
                !HasAuraTag(actor, AuraTag.StealthAccess))
            {
                reason = "requires_stealth";
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresBehindTarget) != 0 &&
                (targetIndex < 0 || !IsBehind(actor.Position, _combatants[targetIndex])))
            {
                reason = "requires_behind";
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresComboPoints) != 0 &&
                (targetIndex < 0 || actor.ComboTarget != _combatants[targetIndex].Id ||
                 actor.ComboPoints <= 0))
            {
                reason = "combo_points";
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresPoisonedTarget) != 0 &&
                (targetIndex < 0 || !HasAuraTag(_combatants[targetIndex], AuraTag.Poison)))
            {
                reason = "target_not_poisoned";
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresFrozenTarget) != 0 &&
                (targetIndex < 0 || !HasAuraTag(_combatants[targetIndex], AuraTag.Frozen)))
            {
                reason = "target_not_frozen";
                return false;
            }

            if ((requirements &
                 AbilityRequirementFlags.TargetBelowTwentyPercentHealth) != 0 &&
                (targetIndex < 0 ||
                 _combatants[targetIndex].Health >
                    (_combatants[targetIndex].MaxHealth * 0.2f) + 0.0001f))
            {
                reason = "target_health_above_twenty_percent";
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresManaTarget) != 0 &&
                (targetIndex < 0 ||
                 _combatants[targetIndex].ResourceProfile.Type != ResourceType.Mana ||
                 _combatants[targetIndex].ResourceProfile.Maximum <= 0f))
            {
                reason = "target_has_no_mana";
                return false;
            }

            if ((requirements & AbilityRequirementFlags.TargetMustNotHaveWeakenedSoul) != 0 &&
                targetIndex >= 0 && HasAura(_combatants[targetIndex], AuraId.WeakenedSoul))
            {
                reason = "weakened_soul";
                return false;
            }

            reason = null;
            return true;
        }

        private void BeginModernAbility(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition)
        {
            AbilityCastProfile profile = definition.CastProfile;
            if (profile.ActivationType == AbilityActivationType.Instant ||
                profile.DurationSeconds <= 0f)
            {
                EmitCastCompleted(actorIndex, command, definition);
                ResolveModernAbility(
                    actorIndex,
                    command,
                    definition,
                    AbilityEffectMoment.Completed);
                return;
            }

            float castTimeMultiplier = GetCastTimeMultiplier(_combatants[actorIndex]);
            int endTick = CurrentTick + SecondsToTicks(
                profile.DurationSeconds * castTimeMultiplier);
            int intervalTicks = SecondsToIntervalTicks(
                profile.TickIntervalSeconds * castTimeMultiplier);
            int remainingTicks = profile.ActivationType == AbilityActivationType.Channel
                ? profile.TickCount
                : 0;
            PendingCast cast = new PendingCast(
                command,
                CurrentTick,
                endTick,
                profile.ActivationType,
                definition.School,
                profile.BreaksOnMovement,
                remainingTicks,
                profile.TickImmediately
                    ? CurrentTick
                    : CurrentTick + intervalTicks,
                intervalTicks,
                true);
            _combatants[actorIndex].Cast = cast;

            if (profile.ActivationType == AbilityActivationType.Channel &&
                profile.TickImmediately && remainingTicks > 0)
            {
                ResolveModernAbility(
                    actorIndex,
                    command,
                    definition,
                    AbilityEffectMoment.ChannelTick);
                cast = cast.AfterChannelTick(CurrentTick + intervalTicks);
                _combatants[actorIndex].Cast = cast;
            }
        }

        private void BeginLegacyAbility(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition)
        {
            int castTicks = SecondsToTicks(definition.CastSeconds);
            if (castTicks <= 0)
            {
                EmitCastCompleted(actorIndex, command, definition);
                ResolveLegacyAbility(actorIndex, command, definition);
                return;
            }

            _combatants[actorIndex].Cast = new PendingCast(
                command,
                CurrentTick,
                CurrentTick + castTicks,
                AbilityActivationType.Cast,
                definition.School,
                true,
                0,
                0,
                0,
                false);
        }

        private void AdvanceCasts()
        {
            for (int actorIndex = 0; actorIndex < _combatantCount; actorIndex++)
            {
                CombatantState actor = _combatants[actorIndex];
                if (!actor.IsAlive || !actor.Cast.IsActive)
                {
                    continue;
                }

                PendingCast cast = actor.Cast;
                AbilityDefinition definition = AbilityCatalog.Get(cast.Command.Ability);
                if (cast.Modern && cast.ActivationType == AbilityActivationType.Channel)
                {
                    if (!HasResolutionLineOfSight(
                            actorIndex,
                            cast.Command,
                            definition))
                    {
                        CancelCast(actorIndex, actor.Id, "line_of_sight");
                        continue;
                    }

                    while (cast.RemainingChannelTicks > 0 &&
                           cast.NextChannelTick <= CurrentTick &&
                           cast.NextChannelTick < cast.EndTick)
                    {
                        ResolveModernAbility(
                            actorIndex,
                            cast.Command,
                            definition,
                            AbilityEffectMoment.ChannelTick);
                        cast = cast.AfterChannelTick(
                            cast.NextChannelTick + cast.ChannelIntervalTicks);
                        if (!actor.IsAlive)
                        {
                            break;
                        }
                    }

                    actor.Cast = cast;
                }

                if (!actor.IsAlive || !actor.Cast.IsActive ||
                    actor.Cast.EndTick > CurrentTick)
                {
                    continue;
                }

                cast = actor.Cast;
                actor.Cast = default;
                EmitCastCompleted(actorIndex, cast.Command, definition);
                if (!HasResolutionLineOfSight(actorIndex, cast.Command, definition))
                {
                    continue;
                }

                if (cast.Modern)
                {
                    if (cast.ActivationType == AbilityActivationType.Cast)
                    {
                        ResolveModernAbility(
                            actorIndex,
                            cast.Command,
                            definition,
                            AbilityEffectMoment.Completed);
                    }
                }
                else
                {
                    ResolveLegacyAbility(actorIndex, cast.Command, definition);
                }

            }
        }

        private bool HasResolutionLineOfSight(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition)
        {
            if (!definition.Targeting.RequiresLineOfSight)
            {
                return true;
            }

            AbilityTargetShape shape = definition.Targeting.Shape;
            if (shape == AbilityTargetShape.Point ||
                shape == AbilityTargetShape.Direction ||
                shape == AbilityTargetShape.TargetArea)
            {
                return _rules.CollisionMap.HasLineOfSight(
                    _combatants[actorIndex].Position,
                    command.TargetPoint);
            }

            int targetIndex = FindIndex(command.Target);
            if (targetIndex < 0 || !_combatants[targetIndex].IsAlive)
            {
                // Target lifetime is handled by effect resolution. This helper only
                // owns cover, so a despawn or death must not be mislabeled as LOS.
                return true;
            }

            return _rules.CollisionMap.HasLineOfSight(
                _combatants[actorIndex].Position,
                _combatants[targetIndex].Position);
        }

        private void EmitCastCompleted(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition)
        {
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.CastCompleted,
                _combatants[actorIndex].Id,
                command.Target,
                definition.Id,
                vector: command.TargetPoint));
        }

        private void ResolveModernAbility(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition,
            AbilityEffectMoment moment)
        {
            if (ResolveHunterAbilityBeforeEffects(
                    actorIndex,
                    command,
                    definition,
                    moment))
            {
                return;
            }

            int selectedTargetIndex = FindIndex(command.Target);
            int comboPoints = _combatants[actorIndex].ComboPoints;
            bool consumedComboPoints = false;
            bool selectedTargetSurvived = selectedTargetIndex >= 0 &&
                _combatants[selectedTargetIndex].IsAlive;

            AbilityEffectSlice slice = definition.Effects;
            for (int effectIndex = 0; effectIndex < slice.Count; effectIndex++)
            {
                AbilityEffectDefinition effect = AbilityEffectCatalog.Get(
                    slice.Offset + effectIndex);
                if (effect.Moment != moment || !EvaluateEffectCondition(
                        actorIndex,
                        selectedTargetIndex,
                        effect,
                        selectedTargetSurvived))
                {
                    continue;
                }

                ApplyEffect(
                    actorIndex,
                    selectedTargetIndex,
                    command,
                    definition,
                    effect,
                    comboPoints,
                    ref consumedComboPoints);
                selectedTargetSurvived = selectedTargetIndex >= 0 &&
                    _combatants[selectedTargetIndex].IsAlive;
            }

            if (consumedComboPoints)
            {
                CombatantState actor = _combatants[actorIndex];
                actor.ComboPoints = 0;
                actor.ComboTarget = default;
            }

            ResolveHunterAbilityAfterEffects(
                actorIndex,
                selectedTargetIndex,
                definition,
                moment);
        }

        private void ApplyEffect(
            int sourceIndex,
            int selectedTargetIndex,
            CombatCommand command,
            AbilityDefinition ability,
            AbilityEffectDefinition effect,
            int comboPoints,
            ref bool consumedComboPoints)
        {
            if (effect.Kind == AbilityEffectKind.DispelArea)
            {
                DispelArea(sourceIndex, command, ability, effect);
                return;
            }

            if (effect.Target == AbilityEffectTarget.AreaFriendlies ||
                effect.Target == AbilityEffectTarget.AreaHostiles)
            {
                Float3 center = ResolveEffectCenter(
                    sourceIndex,
                    selectedTargetIndex,
                    command,
                    ability.Targeting.Shape);
                float radius = ability.Targeting.Radius;
                for (int targetIndex = 0; targetIndex < _combatantCount; targetIndex++)
                {
                    CombatantState source = _combatants[sourceIndex];
                    CombatantState targetState = _combatants[targetIndex];
                    bool wantsHostile = effect.Target == AbilityEffectTarget.AreaHostiles;
                    if (!targetState.IsAlive ||
                        IsEnemy(source, targetState) != wantsHostile ||
                        Float3.PlanarDistance(center, targetState.Position) > radius + 0.001f)
                    {
                        continue;
                    }

                    ApplyEffectToTarget(
                        sourceIndex,
                        targetIndex,
                        command,
                        ability,
                        effect,
                        comboPoints,
                        ref consumedComboPoints);
                }

                return;
            }

            if (effect.Target == AbilityEffectTarget.TeamFriendlies)
            {
                CombatantState source = _combatants[sourceIndex];
                for (int targetIndex = 0; targetIndex < _combatantCount; targetIndex++)
                {
                    CombatantState candidate = _combatants[targetIndex];
                    if (!candidate.IsAlive || candidate.Team != source.Team)
                    {
                        continue;
                    }

                    ApplyEffectToTarget(
                        sourceIndex,
                        targetIndex,
                        command,
                        ability,
                        effect,
                        comboPoints,
                        ref consumedComboPoints);
                }

                return;
            }

            int target = effect.Target == AbilityEffectTarget.Caster
                ? sourceIndex
                : selectedTargetIndex;
            if (target >= 0)
            {
                ApplyEffectToTarget(
                    sourceIndex,
                    target,
                    command,
                    ability,
                    effect,
                    comboPoints,
                    ref consumedComboPoints);
            }
        }

        private void ApplyEffectToTarget(
            int sourceIndex,
            int targetIndex,
            CombatCommand command,
            AbilityDefinition ability,
            AbilityEffectDefinition effect,
            int comboPoints,
            ref bool consumedComboPoints)
        {
            CombatantState target = _combatants[targetIndex];
            if (!target.IsAlive && effect.Kind != AbilityEffectKind.Teleport)
            {
                return;
            }

            if (sourceIndex != targetIndex &&
                IsEnemy(_combatants[sourceIndex], target) &&
                IsMagicSchool(ability.School) &&
                HasAuraTag(target, AuraTag.MagicImmunity))
            {
                return;
            }

            switch (effect.Kind)
            {
                case AbilityEffectKind.Damage:
                    ApplyDamage(
                        sourceIndex,
                        targetIndex,
                        effect.Amount,
                        ability.Id,
                        false,
                        effect.School);
                    break;
                case AbilityEffectKind.Heal:
                    ApplyHealing(sourceIndex, targetIndex, effect.Amount, ability.Id);
                    break;
                case AbilityEffectKind.ApplyAura:
                    ApplyAura(
                        sourceIndex,
                        targetIndex,
                        effect.Aura,
                        effect.SecondaryAmount,
                        effect.Amount,
                        ability.Id,
                        ability.School);
                    break;
                case AbilityEffectKind.Dispel:
                    Dispel(sourceIndex, targetIndex, effect, ability.Id);
                    break;
                case AbilityEffectKind.Interrupt:
                    Interrupt(
                        targetIndex,
                        _combatants[sourceIndex].Id,
                        ability.Id,
                        effect.Amount);
                    break;
                case AbilityEffectKind.Knockback:
                    ApplyKnockback(sourceIndex, targetIndex, effect.Amount, ability.Id);
                    break;
                case AbilityEffectKind.Teleport:
                    ApplyTeleport(sourceIndex, selectedTargetIndex: FindIndex(command.Target), command, effect.Amount);
                    break;
                case AbilityEffectKind.ModifyResource:
                    ModifyResource(
                        targetIndex,
                        (ResourceType)effect.Count,
                        effect.Amount);
                    break;
                case AbilityEffectKind.AddComboPoints:
                    AddComboPoints(sourceIndex, targetIndex, effect.Count);
                    break;
                case AbilityEffectKind.ConsumeComboPointsDamage:
                    ApplyDamage(
                        sourceIndex,
                        targetIndex,
                        effect.Amount * comboPoints,
                        ability.Id,
                        false,
                        effect.School);
                    consumedComboPoints = true;
                    break;
                case AbilityEffectKind.ConsumeComboPointsAura:
                    ApplyAura(
                        sourceIndex,
                        targetIndex,
                        effect.Aura,
                        effect.Amount * comboPoints,
                        0f,
                        ability.Id,
                        ability.School);
                    consumedComboPoints = true;
                    break;
                case AbilityEffectKind.Summon:
                    StartSummon(
                        sourceIndex,
                        targetIndex,
                        effect.Summon,
                        effect.Amount,
                        ability.Id);
                    break;
                case AbilityEffectKind.RemoveAurasByTag:
                    RemoveAurasByTag(
                        targetIndex,
                        effect.AuraTags,
                        _combatants[sourceIndex].Id,
                        ability.Id);
                    break;
                case AbilityEffectKind.LeechDamage:
                    float healthDamage = ApplyDamage(
                        sourceIndex,
                        targetIndex,
                        effect.Amount,
                        ability.Id,
                        false,
                        effect.School);
                    ApplyHealing(
                        sourceIndex,
                        sourceIndex,
                        healthDamage * effect.SecondaryAmount,
                        ability.Id);
                    break;
                case AbilityEffectKind.ModifyResourcePercentMaximum:
                    ModifyResourcePercentMaximum(
                        targetIndex,
                        (ResourceType)effect.Count,
                        effect.Amount);
                    break;
                case AbilityEffectKind.BurnResource:
                    BurnResource(
                        sourceIndex,
                        targetIndex,
                        ability.Id,
                        effect);
                    break;
                case AbilityEffectKind.RefreshAuraDuration:
                    RefreshAuraFromSource(
                        target,
                        effect.Aura,
                        _combatants[sourceIndex].Id,
                        effect.SecondaryAmount);
                    break;
                case AbilityEffectKind.RemoveAnyAurasByTag:
                    RemoveAnyAurasByTag(
                        targetIndex,
                        effect.AuraTags,
                        _combatants[sourceIndex].Id,
                        ability.Id);
                    break;
                case AbilityEffectKind.Spellsteal:
                    Spellsteal(sourceIndex, targetIndex, effect, ability.Id);
                    break;
                case AbilityEffectKind.ResetCooldowns:
                    ResetCooldownGroup(
                        _combatants[sourceIndex],
                        (CooldownResetGroup)effect.Count,
                        ability.Id);
                    break;
                case AbilityEffectKind.ToggleAura:
                    if (HasAura(target, effect.Aura))
                    {
                        RemoveAura(
                            targetIndex,
                            effect.Aura,
                            _combatants[sourceIndex].Id,
                            ability.Id,
                            false);
                    }
                    else
                    {
                        ApplyAura(
                            sourceIndex,
                            targetIndex,
                            effect.Aura,
                            effect.Amount,
                            0f,
                            ability.Id,
                            ability.School);
                    }
                    break;
            }
        }

        private void ResolveLegacyAbility(
            int actorIndex,
            CombatCommand command,
            AbilityDefinition definition)
        {
            CombatantState actor = _combatants[actorIndex];
            if (definition.Id == AbilityId.Shadowstep || definition.Id == AbilityId.Blink)
            {
                Float3 desiredPosition = ClampTargetPoint(
                    actor.Position,
                    command.TargetPoint,
                    definition.Range);
                actor.Position = _rules.CollisionMap.ResolveBlink(
                    actor.Position,
                    desiredPosition,
                    ArenaLayout.CombatantCollisionRadius);
                return;
            }

            if (definition.Id == AbilityId.Anchor)
            {
                ApplyLegacyBarrier(
                    actorIndex,
                    actorIndex,
                    definition.Barrier,
                    definition.StatusSeconds,
                    definition.Id);
                ApplyLegacyStatus(
                    actorIndex,
                    actorIndex,
                    StatusId.Anchored,
                    definition.StatusSeconds,
                    definition.Id);
                return;
            }

            if (definition.Id == AbilityId.Purify)
            {
                int allyIndex = FindIndex(command.Target);
                if (allyIndex < 0)
                {
                    allyIndex = actorIndex;
                }

                ClearLegacyHarmfulStatuses(allyIndex, actor.Id, definition.Id);
                ApplyHealing(actorIndex, allyIndex, definition.Healing, definition.Id);
                return;
            }

            if (definition.TargetMode == AbilityTargetMode.EnemyArea)
            {
                ResolveLegacyEnemyArea(actorIndex, actor.Position, definition);
                return;
            }

            if (definition.TargetMode == AbilityTargetMode.Point && definition.AreaRadius > 0f)
            {
                ResolveLegacyEnemyArea(actorIndex, command.TargetPoint, definition);
                return;
            }

            int targetIndex = FindIndex(command.Target);
            if (targetIndex < 0 || !_combatants[targetIndex].IsAlive)
            {
                return;
            }

            if (definition.Id == AbilityId.Ambush)
            {
                Float3 towardTarget =
                    (_combatants[targetIndex].Position - actor.Position).PlanarNormalized;
                Float3 desiredPosition = _combatants[targetIndex].Position -
                    (towardTarget * ShadowstepSeparation);
                actor.Position = _rules.CollisionMap.ResolvePlacement(
                    desiredPosition,
                    ArenaLayout.CombatantCollisionRadius,
                    default(Float3) - towardTarget);
            }

            if (definition.Id == AbilityId.Kick || definition.Id == AbilityId.Counterspell)
            {
                Interrupt(targetIndex, actor.Id, definition.Id, definition.StatusSeconds);
            }

            if (definition.Damage > 0f)
            {
                ApplyDamage(
                    actorIndex,
                    targetIndex,
                    definition.Damage,
                    definition.Id,
                    false,
                    definition.School);
            }

            if (definition.Healing > 0f)
            {
                ApplyHealing(actorIndex, targetIndex, definition.Healing, definition.Id);
            }

            if (definition.Barrier > 0f)
            {
                ApplyLegacyBarrier(
                    actorIndex,
                    targetIndex,
                    definition.Barrier,
                    definition.StatusSeconds,
                    definition.Id);
            }

            if (definition.Status != StatusId.None && definition.Status != StatusId.Barrier)
            {
                ApplyLegacyStatus(
                    actorIndex,
                    targetIndex,
                    definition.Status,
                    definition.StatusSeconds,
                    definition.Id);
            }

            if (Math.Abs(definition.KnockbackDistance) > 0.001f)
            {
                ApplyKnockback(
                    actorIndex,
                    targetIndex,
                    definition.KnockbackDistance,
                    definition.Id);
            }
        }

        private void ResolveLegacyEnemyArea(
            int actorIndex,
            Float3 center,
            AbilityDefinition definition)
        {
            CombatantState actor = _combatants[actorIndex];
            for (int targetIndex = 0; targetIndex < _combatantCount; targetIndex++)
            {
                CombatantState target = _combatants[targetIndex];
                if (!target.IsAlive || target.Team == actor.Team ||
                    Float3.PlanarDistance(center, target.Position) > definition.AreaRadius)
                {
                    continue;
                }

                if (definition.Damage > 0f)
                {
                    ApplyDamage(
                        actorIndex,
                        targetIndex,
                        definition.Damage,
                        definition.Id,
                        false,
                        definition.School);
                }

                if (definition.Status != StatusId.None)
                {
                    ApplyLegacyStatus(
                        actorIndex,
                        targetIndex,
                        definition.Status,
                        definition.StatusSeconds,
                        definition.Id);
                }

                if (definition.KnockbackDistance > 0f)
                {
                    ApplyKnockback(
                        actorIndex,
                        targetIndex,
                        definition.KnockbackDistance,
                        definition.Id);
                }
            }
        }

        private void ProcessAuras()
        {
            for (int targetIndex = 0; targetIndex < _combatantCount; targetIndex++)
            {
                CombatantState target = _combatants[targetIndex];
                for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
                {
                    AuraState aura = target.Auras[auraIndex];
                    if (!aura.Active)
                    {
                        continue;
                    }

                    AuraDefinition definition = AuraCatalog.Get((AuraId)auraIndex);
                    if (definition.PeriodSeconds > 0f && aura.NextPeriodicTick > 0)
                    {
                        int periodTicks = SecondsToTicks(definition.PeriodSeconds);
                        while (aura.NextPeriodicTick <= CurrentTick &&
                               aura.NextPeriodicTick <= aura.EndTick)
                        {
                            ExecuteAuraEffects(
                                targetIndex,
                                aura,
                                definition.PeriodicEffects,
                                AbilityEffectMoment.AuraPeriodic);
                            aura.NextPeriodicTick += periodTicks;
                        }

                        target.Auras[auraIndex] = aura;
                    }

                    if (aura.EndTick <= CurrentTick)
                    {
                        ExecuteAuraEffects(
                            targetIndex,
                            aura,
                            definition.ExpirationEffects,
                            AbilityEffectMoment.AuraExpired);
                        RemoveAura(
                            targetIndex,
                            (AuraId)auraIndex,
                            aura.Source,
                            aura.Ability,
                            false);
                    }
                }
            }
        }

        private void ExecuteAuraEffects(
            int auraTargetIndex,
            AuraState aura,
            AbilityEffectSlice slice,
            AbilityEffectMoment moment,
            int dispellerIndex = -1)
        {
            if (slice.IsEmpty)
            {
                return;
            }

            int sourceIndex = FindIndex(aura.Source);
            if (sourceIndex < 0)
            {
                sourceIndex = auraTargetIndex;
            }

            for (int effectIndex = 0; effectIndex < slice.Count; effectIndex++)
            {
                AbilityEffectDefinition effect = AbilityEffectCatalog.Get(
                    slice.Offset + effectIndex);
                if (effect.Moment != moment)
                {
                    continue;
                }

                if (effect.Target == AbilityEffectTarget.AreaHostiles ||
                    effect.Target == AbilityEffectTarget.AreaFriendlies)
                {
                    bool hostiles = effect.Target == AbilityEffectTarget.AreaHostiles;
                    for (int targetIndex = 0; targetIndex < _combatantCount; targetIndex++)
                    {
                        CombatantState candidate = _combatants[targetIndex];
                        if (!candidate.IsAlive ||
                            IsEnemy(_combatants[sourceIndex], candidate) != hostiles ||
                            Float3.PlanarDistance(
                                _combatants[auraTargetIndex].Position,
                                candidate.Position) > 5f)
                        {
                            continue;
                        }

                        ApplyAuraPeriodicEffect(
                            sourceIndex,
                            targetIndex,
                            aura.Ability,
                            effect,
                            moment);
                    }
                }
                else
                {
                    int targetIndex;
                    if (effect.Target == AbilityEffectTarget.Caster)
                    {
                        targetIndex = sourceIndex;
                    }
                    else if (effect.Target == AbilityEffectTarget.Dispeller)
                    {
                        targetIndex = dispellerIndex;
                    }
                    else
                    {
                        targetIndex = auraTargetIndex;
                    }

                    ApplyAuraPeriodicEffect(
                        sourceIndex,
                        targetIndex,
                        aura.Ability,
                        effect,
                        moment);
                }
            }
        }

        private void ApplyAuraPeriodicEffect(
            int sourceIndex,
            int targetIndex,
            AbilityId ability,
            AbilityEffectDefinition effect,
            AbilityEffectMoment moment)
        {
            if (targetIndex < 0 || targetIndex >= _combatantCount)
            {
                return;
            }

            if (effect.Kind == AbilityEffectKind.Damage)
            {
                ApplyDamage(
                    sourceIndex,
                    targetIndex,
                    effect.Amount,
                    ability,
                    false,
                    effect.School,
                    moment == AbilityEffectMoment.AuraDispelled);
            }
            else if (effect.Kind == AbilityEffectKind.Heal)
            {
                ApplyHealing(sourceIndex, targetIndex, effect.Amount, ability);
            }
            else if (effect.Kind == AbilityEffectKind.LeechDamage)
            {
                float healthDamage = ApplyDamage(
                    sourceIndex,
                    targetIndex,
                    effect.Amount,
                    ability,
                    false,
                    effect.School,
                    moment == AbilityEffectMoment.AuraDispelled);
                ApplyHealing(
                    sourceIndex,
                    sourceIndex,
                    healthDamage * effect.SecondaryAmount,
                    ability);
            }
            else if (effect.Kind == AbilityEffectKind.ModifyResourcePercentMaximum)
            {
                ModifyResourcePercentMaximum(
                    targetIndex,
                    (ResourceType)effect.Count,
                    effect.Amount);
            }
            else if (effect.Kind == AbilityEffectKind.BurnResource)
            {
                BurnResource(sourceIndex, targetIndex, ability, effect);
            }
            else if (effect.Kind == AbilityEffectKind.ApplyAura)
            {
                SpellSchoolMask school = AbilityCatalog.TryGet(
                    ability,
                    out AbilityDefinition definition)
                        ? definition.School
                        : effect.School;
                ApplyAura(
                    sourceIndex,
                    targetIndex,
                    effect.Aura,
                    effect.SecondaryAmount,
                    effect.Amount,
                    ability,
                    school);
            }
        }

        private void ApplyAura(
            int sourceIndex,
            int targetIndex,
            AuraId auraId,
            float seconds,
            float magnitude,
            AbilityId ability,
            SpellSchoolMask originatingSchool)
        {
            if (!AuraCatalog.TryGet(auraId, out AuraDefinition definition) || seconds <= 0f)
            {
                return;
            }

            CombatantState source = _combatants[sourceIndex];
            CombatantState target = _combatants[targetIndex];
            bool hostile = IsEnemy(source, target);
            bool magicalHarm = hostile && definition.Disposition == AuraDisposition.Harmful &&
                IsMagicSchool(originatingSchool);
            if (magicalHarm && HasAuraTag(target, AuraTag.MagicImmunity))
            {
                return;
            }

            bool movementImpairing = (definition.Tags & (AuraTag.Root | AuraTag.Slow)) != 0;
            if (hostile && movementImpairing &&
                HasAuraTag(target, AuraTag.MovementImpairmentImmunity))
            {
                return;
            }

            if (hostile && (definition.Tags & AuraTag.Fear) != 0 &&
                HasAuraTag(target, AuraTag.FearWard))
            {
                RemoveAura(
                    targetIndex,
                    AuraId.FearWard,
                    source.Id,
                    ability,
                    false);
                return;
            }

            if (definition.Disposition == AuraDisposition.Beneficial &&
                (definition.Tags & AuraTag.ExclusiveStance) != 0)
            {
                RemoveOtherExclusiveStances(targetIndex, auraId, source.Id, ability);
            }

            if ((definition.Tags & AuraTag.UniquePerCaster) != 0)
            {
                RemovePriorUniqueAura(sourceIndex, targetIndex, auraId, ability);
            }

            int durationTicks = SecondsToTicks(seconds);
            if (definition.DiminishingReturn != DiminishingReturnCategory.None && hostile)
            {
                float multiplier = GetDiminishingReturnMultiplier(
                    target,
                    definition.DiminishingReturn);
                if (multiplier <= 0f)
                {
                    return;
                }

                durationTicks = Math.Max(1, (int)Math.Ceiling(durationTicks * multiplier));
                AdvanceDiminishingReturn(target, definition.DiminishingReturn);
            }

            int auraIndex = (int)auraId;
            AuraState existing = target.Auras[auraIndex];
            int endTick = CurrentTick + durationTicks;
            int periodTicks = SecondsToTicks(definition.PeriodSeconds);
            int stacks = existing.Active
                ? Math.Min(definition.MaximumStacks, existing.Stacks + 1)
                : 1;
            float absorb = (definition.Tags & AuraTag.Absorb) != 0
                ? Math.Max(existing.Active ? existing.AbsorbRemaining : 0f, magnitude)
                : 0f;
            AuraState state = new AuraState(
                true,
                source.Id,
                ability,
                endTick,
                periodTicks > 0 ? CurrentTick + periodTicks : 0,
                stacks,
                absorb,
                magnitude);
            target.Auras[auraIndex] = state;

            StatusId status = MapAuraStatus(definition);
            if ((definition.Tags & AuraTag.Absorb) != 0)
            {
                Emit(CombatEvent.Create(
                    CurrentTick,
                    CombatEventKind.BarrierApplied,
                    source.Id,
                    target.Id,
                    ability,
                    StatusId.Barrier,
                    magnitude,
                    aura: auraId));
            }
            else if (status != StatusId.None)
            {
                Emit(CombatEvent.Create(
                    CurrentTick,
                    CombatEventKind.StatusApplied,
                    source.Id,
                    target.Id,
                    ability,
                    status,
                    stacks,
                    aura: auraId));
            }
            else
            {
                Emit(CombatEvent.Create(
                    CurrentTick,
                    CombatEventKind.AuraApplied,
                    source.Id,
                    target.Id,
                    ability,
                    aura: auraId));
            }

            if (IsHardControl(definition.Tags) ||
                (definition.Tags & AuraTag.Silence) != 0)
            {
                CancelCast(targetIndex, source.Id, "control");
            }

            if (IsHardControl(definition.Tags))
            {
                target.MoveIntent = default;
            }
        }

        private void RemoveAura(
            int targetIndex,
            AuraId auraId,
            ParticipantId remover,
            AbilityId ability,
            bool executeDispelExpiration)
        {
            int auraIndex = (int)auraId;
            if (auraIndex <= 0 || auraIndex >= AuraCatalog.StorageCapacity)
            {
                return;
            }

            CombatantState target = _combatants[targetIndex];
            AuraState state = target.Auras[auraIndex];
            if (!state.Active)
            {
                return;
            }

            AuraDefinition definition = AuraCatalog.Get(auraId);
            if (executeDispelExpiration && !definition.ExpirationEffects.IsEmpty)
            {
                ExecuteAuraEffects(
                    targetIndex,
                    state,
                    definition.ExpirationEffects,
                    AbilityEffectMoment.AuraExpired);
            }

            if (executeDispelExpiration && !definition.DispelEffects.IsEmpty)
            {
                ExecuteAuraEffects(
                    targetIndex,
                    state,
                    definition.DispelEffects,
                    AbilityEffectMoment.AuraDispelled,
                    FindIndex(remover));
            }

            target.Auras[auraIndex] = default;
            if (definition.DiminishingReturn != DiminishingReturnCategory.None)
            {
                int drIndex = (int)definition.DiminishingReturn;
                target.DiminishingReturnResetTicks[drIndex] =
                    CurrentTick + SecondsToTicks(_rules.DiminishingReturnResetSeconds);
            }

            StatusId status = MapAuraStatus(definition);
            if (status != StatusId.None)
            {
                Emit(CombatEvent.Create(
                    CurrentTick,
                    CombatEventKind.StatusRemoved,
                    remover,
                    target.Id,
                    ability,
                    status,
                    aura: auraId));
            }
            else
            {
                Emit(CombatEvent.Create(
                    CurrentTick,
                    CombatEventKind.AuraRemoved,
                    remover,
                    target.Id,
                    ability,
                    aura: auraId));
            }
        }

        private void Dispel(
            int sourceIndex,
            int targetIndex,
            AbilityEffectDefinition effect,
            AbilityId ability)
        {
            CombatantState source = _combatants[sourceIndex];
            CombatantState target = _combatants[targetIndex];
            bool friendly = !IsEnemy(source, target);
            AuraDisposition wantedDisposition;
            switch (effect.DispelSelection)
            {
                case DispelSelectionMode.HarmfulFromFriendly:
                    wantedDisposition = AuraDisposition.Harmful;
                    break;
                case DispelSelectionMode.BeneficialFromHostile:
                    wantedDisposition = AuraDisposition.Beneficial;
                    break;
                default:
                    wantedDisposition = friendly
                        ? AuraDisposition.Harmful
                        : AuraDisposition.Beneficial;
                    break;
            }

            int remaining = effect.Count;
            for (int auraIndex = 1;
                 auraIndex < AuraCatalog.StorageCapacity && remaining > 0;
                 auraIndex++)
            {
                AuraState state = target.Auras[auraIndex];
                if (!IsAuraActive(state))
                {
                    continue;
                }

                AuraDefinition aura = AuraCatalog.Get((AuraId)auraIndex);
                if (aura.Disposition != wantedDisposition ||
                    aura.DispelType == DispelType.None ||
                    (aura.DispelType & effect.DispelType) == 0)
                {
                    continue;
                }

                RemoveAura(
                    targetIndex,
                    (AuraId)auraIndex,
                    source.Id,
                    ability,
                    true);
                remaining--;
            }
        }

        private void DispelArea(
            int sourceIndex,
            CombatCommand command,
            AbilityDefinition ability,
            AbilityEffectDefinition effect)
        {
            if (effect.Count <= 0)
            {
                return;
            }

            CombatantState source = _combatants[sourceIndex];
            Float3 center = command.TargetPoint;
            float radius = ability.Targeting.Radius;
            bool wantsHostile = effect.Target == AbilityEffectTarget.AreaHostiles;
            AuraDisposition wantedDisposition = wantsHostile
                ? AuraDisposition.Beneficial
                : AuraDisposition.Harmful;
            int lastParticipantValue = 0;
            int dispelledTargets = 0;
            while (dispelledTargets < effect.Count)
            {
                int selectedIndex = -1;
                int selectedParticipantValue = int.MaxValue;
                for (int targetIndex = 0;
                     targetIndex < _combatantCount;
                     targetIndex++)
                {
                    CombatantState candidate = _combatants[targetIndex];
                    bool hostile = IsEnemy(source, candidate);
                    if (!candidate.IsAlive || hostile != wantsHostile ||
                        candidate.Id.Value <= lastParticipantValue ||
                        candidate.Id.Value >= selectedParticipantValue ||
                        Float3.PlanarDistance(center, candidate.Position) >
                            radius + 0.001f ||
                        !HasDispellableAura(
                            candidate,
                            wantedDisposition,
                            effect.DispelType))
                    {
                        continue;
                    }

                    selectedIndex = targetIndex;
                    selectedParticipantValue = candidate.Id.Value;
                }

                if (selectedIndex < 0)
                {
                    break;
                }

                RemoveFirstDispellableAura(
                    selectedIndex,
                    wantedDisposition,
                    effect.DispelType,
                    source.Id,
                    ability.Id);
                lastParticipantValue = selectedParticipantValue;
                dispelledTargets++;
            }
        }

        private bool HasDispellableAura(
            CombatantState target,
            AuraDisposition disposition,
            DispelType dispelType)
        {
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                if (!IsAuraActive(target.Auras[auraIndex]))
                {
                    continue;
                }

                AuraDefinition aura = AuraCatalog.Get((AuraId)auraIndex);
                if (aura.Disposition == disposition && aura.DispelType != DispelType.None &&
                    (aura.DispelType & dispelType) != 0)
                {
                    return true;
                }
            }

            return false;
        }

        private void RemoveFirstDispellableAura(
            int targetIndex,
            AuraDisposition disposition,
            DispelType dispelType,
            ParticipantId remover,
            AbilityId ability)
        {
            CombatantState target = _combatants[targetIndex];
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                if (!IsAuraActive(target.Auras[auraIndex]))
                {
                    continue;
                }

                AuraDefinition aura = AuraCatalog.Get((AuraId)auraIndex);
                if (aura.Disposition != disposition || aura.DispelType == DispelType.None ||
                    (aura.DispelType & dispelType) == 0)
                {
                    continue;
                }

                RemoveAura(targetIndex, (AuraId)auraIndex, remover, ability, true);
                return;
            }
        }

        private void RemoveAurasByTag(
            int targetIndex,
            AuraTag tags,
            ParticipantId remover,
            AbilityId ability)
        {
            if (tags == AuraTag.None)
            {
                return;
            }

            CombatantState target = _combatants[targetIndex];
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                if (!IsAuraActive(target.Auras[auraIndex]))
                {
                    continue;
                }

                AuraDefinition definition = AuraCatalog.Get((AuraId)auraIndex);
                if (definition.Disposition != AuraDisposition.Harmful ||
                    (definition.Tags & tags) == 0)
                {
                    continue;
                }

                RemoveAura(targetIndex, (AuraId)auraIndex, remover, ability, false);
            }
        }

        private void RemoveAnyAurasByTag(
            int targetIndex,
            AuraTag tags,
            ParticipantId remover,
            AbilityId ability)
        {
            if (tags == AuraTag.None)
            {
                return;
            }

            CombatantState target = _combatants[targetIndex];
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                if (!IsAuraActive(target.Auras[auraIndex]))
                {
                    continue;
                }

                AuraDefinition definition = AuraCatalog.Get((AuraId)auraIndex);
                if ((definition.Tags & tags) != 0)
                {
                    RemoveAura(targetIndex, (AuraId)auraIndex, remover, ability, false);
                }
            }
        }

        private void RemoveOtherExclusiveStances(
            int targetIndex,
            AuraId incomingAura,
            ParticipantId remover,
            AbilityId ability)
        {
            CombatantState target = _combatants[targetIndex];
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                AuraId activeAura = (AuraId)auraIndex;
                if (activeAura == incomingAura ||
                    !IsAuraActive(target.Auras[auraIndex]) ||
                    (AuraCatalog.Get(activeAura).Tags & AuraTag.ExclusiveStance) == 0)
                {
                    continue;
                }

                RemoveAura(targetIndex, activeAura, remover, ability, false);
            }
        }

        private void Spellsteal(
            int sourceIndex,
            int targetIndex,
            AbilityEffectDefinition effect,
            AbilityId ability)
        {
            CombatantState target = _combatants[targetIndex];
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                AuraState state = target.Auras[auraIndex];
                if (!IsAuraActive(state))
                {
                    continue;
                }

                AuraId auraId = (AuraId)auraIndex;
                AuraDefinition definition = AuraCatalog.Get(auraId);
                if (definition.Disposition != AuraDisposition.Beneficial ||
                    definition.DispelType == DispelType.None ||
                    (definition.DispelType & effect.DispelType) == 0)
                {
                    continue;
                }

                float remainingSeconds = Math.Max(
                    _rules.TickSeconds,
                    (state.EndTick - CurrentTick) * _rules.TickSeconds);
                float magnitude = (definition.Tags & AuraTag.Absorb) != 0
                    ? state.AbsorbRemaining
                    : state.Magnitude;
                RemoveAura(
                    targetIndex,
                    auraId,
                    _combatants[sourceIndex].Id,
                    ability,
                    true);
                ApplyAura(
                    sourceIndex,
                    sourceIndex,
                    auraId,
                    remainingSeconds,
                    magnitude,
                    ability,
                    SpellSchoolMask.Arcane);
                return;
            }
        }

        private void ResetCooldownGroup(
            CombatantState actor,
            CooldownResetGroup group,
            AbilityId resetAbility)
        {
            switch (group)
            {
                case CooldownResetGroup.MageFrost:
                    ResetCooldown(actor, AbilityId.FrostNova, resetAbility);
                    ResetCooldown(actor, AbilityId.ConeOfCold, resetAbility);
                    ResetCooldown(actor, AbilityId.DeepFreeze, resetAbility);
                    ResetCooldown(actor, AbilityId.IceBarrier, resetAbility);
                    ResetCooldown(actor, AbilityId.IceBlock, resetAbility);
                    ResetCooldown(actor, AbilityId.IcyVeins, resetAbility);
                    ResetCooldown(actor, AbilityId.WaterElementalFreeze, resetAbility);
                    break;
                case CooldownResetGroup.RoguePreparation:
                    ResetCooldown(actor, AbilityId.Evasion, resetAbility);
                    ResetCooldown(actor, AbilityId.Sprint, resetAbility);
                    ResetCooldown(actor, AbilityId.Vanish, resetAbility);
                    ResetCooldown(actor, AbilityId.Shadowstep, resetAbility);
                    break;
            }
        }

        private void ResetCooldown(
            CombatantState actor,
            AbilityId ability,
            AbilityId resetAbility)
        {
            if (ability != resetAbility && AbilityCatalog.TryGet(ability, out _))
            {
                actor.CooldownEndTicks[(int)ability] = CurrentTick;
            }
        }

        private void ProcessSummons()
        {
            for (int summonIndex = 0; summonIndex < _summons.Length; summonIndex++)
            {
                SummonState summon = _summons[summonIndex];
                if (!summon.Active)
                {
                    continue;
                }

                if (summon.EndTick <= CurrentTick)
                {
                    _summons[summonIndex] = default;
                    continue;
                }

                int sourceIndex = FindIndex(summon.Owner);
                int targetIndex = FindIndex(summon.Target);
                if (sourceIndex < 0 || targetIndex < 0 ||
                    !_combatants[sourceIndex].IsAlive || !_combatants[targetIndex].IsAlive)
                {
                    _summons[summonIndex] = default;
                    continue;
                }

                while (summon.NextPulseTick <= CurrentTick &&
                       summon.NextPulseTick < summon.EndTick)
                {
                    ApplyDamage(
                        sourceIndex,
                        targetIndex,
                        FamiliarDamage,
                        summon.Ability,
                        false,
                        SpellSchoolMask.Shadow);
                    ModifyResource(sourceIndex, ResourceType.Mana, FamiliarManaReturn);
                    summon = summon.WithNextPulseTick(
                        summon.NextPulseTick + SecondsToTicks(FamiliarPulseSeconds));
                    if (!_combatants[targetIndex].IsAlive)
                    {
                        summon = default;
                        break;
                    }
                }

                _summons[summonIndex] = summon;
            }
        }

        private void StartSummon(
            int sourceIndex,
            int targetIndex,
            SummonId summonId,
            float seconds,
            AbilityId ability)
        {
            if (summonId == SummonId.None || targetIndex < 0)
            {
                return;
            }

            int freeIndex = -1;
            for (int index = 0; index < _summons.Length; index++)
            {
                if (_summons[index].Active &&
                    _summons[index].Owner == _combatants[sourceIndex].Id &&
                    _summons[index].Id == summonId)
                {
                    freeIndex = index;
                    break;
                }

                if (!_summons[index].Active && freeIndex < 0)
                {
                    freeIndex = index;
                }
            }

            if (freeIndex < 0)
            {
                return;
            }

            _summons[freeIndex] = new SummonState(
                true,
                summonId,
                _combatants[sourceIndex].Id,
                _combatants[targetIndex].Id,
                ability,
                CurrentTick + SecondsToTicks(seconds),
                CurrentTick + SecondsToTicks(FamiliarPulseSeconds));
        }

        private void ApplyMovement()
        {
            float baseDistance = _rules.MoveSpeed * _rules.TickSeconds;
            for (int index = 0; index < _combatantCount; index++)
            {
                CombatantState actor = _combatants[index];
                if (!actor.IsAlive)
                {
                    continue;
                }

                if (actor.Cast.IsActive && actor.Cast.BreaksOnMovement &&
                    actor.MoveIntent.PlanarLengthSquared > MovementCancelThresholdSquared)
                {
                    CancelCast(index, actor.Id, "movement");
                }

                AuraTag tags = GetActiveAuraTags(actor);
                if ((tags &
                    (AuraTag.Stun | AuraTag.Fear | AuraTag.Horror |
                     AuraTag.Disorient | AuraTag.Root)) != 0)
                {
                    continue;
                }

                float speedScale = actor.StatusEndTicks[(int)StatusId.Slowed] > CurrentTick
                    ? 0.65f
                    : 1f;
                speedScale *= actor.MovementSpeedMultiplierPermille / 1000f;
                for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
                {
                    if (!IsAuraActive(actor.Auras[auraIndex]))
                    {
                        continue;
                    }

                    speedScale *= AuraCatalog.Get((AuraId)auraIndex).MovementMultiplier;
                }

                Float3 displacement = ClampPlanar(actor.MoveIntent, 1f) *
                    (baseDistance * speedScale);
                actor.Position = _rules.CollisionMap.ResolveMovement(
                    actor.Position,
                    displacement,
                    ArenaLayout.CombatantCollisionRadius);
            }
        }

        private void ApplyHazards()
        {
            if (!_rules.RadialHazardsEnabled)
            {
                return;
            }

            for (int index = 0; index < _combatantCount; index++)
            {
                CombatantState target = _combatants[index];
                if (!target.IsAlive || target.Class == ClassId.Boss)
                {
                    continue;
                }

                float radius = target.Position.PlanarLength;
                if (radius >= _rules.OuterKillRadius)
                {
                    ApplyImmediateHazardDeath(index);
                }
                else if (radius > _rules.SafeRadius)
                {
                    ApplyDamage(
                        index,
                        index,
                        _rules.LavaDamagePerSecond * _rules.TickSeconds,
                        AbilityId.None,
                        true,
                        SpellSchoolMask.None);
                }
            }
        }

        private void ApplyImmediateHazardDeath(int targetIndex)
        {
            CombatantState target = _combatants[targetIndex];
            if (!target.IsAlive)
            {
                return;
            }

            float healthDamage = target.Health;
            target.Health = 0f;
            target.IsAlive = false;
            target.Cast = default;
            target.MoveIntent = default;
            target.ComboPoints = 0;
            target.ComboTarget = default;
            DespawnCompanion(target.Id);
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.HazardDamage,
                target.Id,
                target.Id,
                amount: healthDamage));
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.Death,
                target.Id,
                target.Id,
                reason: "lava"));
        }

        private float ApplyDamage(
            int sourceIndex,
            int targetIndex,
            float amount,
            AbilityId ability,
            bool isHazard,
            SpellSchoolMask school,
            bool isBacklash = false)
        {
            CombatantState target = _combatants[targetIndex];
            if (!target.IsAlive || amount <= 0f)
            {
                return 0f;
            }

            bool hostileMagic = sourceIndex != targetIndex &&
                IsEnemy(_combatants[sourceIndex], target) && IsMagicSchool(school);
            if (hostileMagic && HasAuraTag(target, AuraTag.MagicImmunity))
            {
                return 0f;
            }

            if (!isHazard && sourceIndex != targetIndex &&
                IsEnemy(_combatants[sourceIndex], target) &&
                IsBlockedByDeterrence(sourceIndex, targetIndex, ability))
            {
                return 0f;
            }

            float reducedAmount = amount *
                GetDamageDoneMultiplier(_combatants[sourceIndex], school) *
                GetDamageTakenMultiplier(target, school);
            float remaining = reducedAmount;

            for (int auraIndex = 1;
                 auraIndex < AuraCatalog.StorageCapacity && remaining > 0f;
                 auraIndex++)
            {
                AuraState aura = target.Auras[auraIndex];
                if (!IsAuraActive(aura) || aura.AbsorbRemaining <= 0f)
                {
                    continue;
                }

                float absorbed = Math.Min(aura.AbsorbRemaining, remaining);
                aura.AbsorbRemaining -= absorbed;
                remaining -= absorbed;
                target.Auras[auraIndex] = aura;
                if (aura.AbsorbRemaining <= 0.0001f)
                {
                    RemoveAura(
                        targetIndex,
                        (AuraId)auraIndex,
                        _combatants[sourceIndex].Id,
                        ability,
                        false);
                }
            }

            float legacyAbsorbed = Math.Min(target.LegacyBarrier, remaining);
            target.LegacyBarrier -= legacyAbsorbed;
            remaining -= legacyAbsorbed;
            float healthDamage = Math.Min(target.Health, Math.Max(0f, remaining));
            target.Health -= healthDamage;
            MirrorDamageToRoarCompanion(targetIndex, healthDamage);

            Emit(CombatEvent.Create(
                CurrentTick,
                isHazard ? CombatEventKind.HazardDamage : CombatEventKind.Damage,
                _combatants[sourceIndex].Id,
                target.Id,
                ability,
                amount: healthDamage));

            TriggerVampiricEmbrace(
                sourceIndex,
                healthDamage,
                ability,
                school,
                isHazard,
                isBacklash || sourceIndex == targetIndex);

            if (reducedAmount > 0f)
            {
                BreakDamageSensitiveAuras(targetIndex, _combatants[sourceIndex].Id, ability);
            }

            if (target.Health <= 0f)
            {
                target.Health = 0f;
                target.IsAlive = false;
                target.Cast = default;
                target.MoveIntent = default;
                target.ComboPoints = 0;
                target.ComboTarget = default;
                DespawnCompanion(target.Id);
                Emit(CombatEvent.Create(
                    CurrentTick,
                    CombatEventKind.Death,
                    _combatants[sourceIndex].Id,
                    target.Id,
                    ability,
                    reason: isHazard ? "lava" : "ability"));
            }
            else if (healthDamage > 0f)
            {
                TriggerPrayerOfMending(targetIndex);
            }

            return healthDamage;
        }

        private void TriggerPrayerOfMending(int targetIndex)
        {
            CombatantState target = _combatants[targetIndex];
            int auraIndex = (int)AuraId.PrayerOfMending;
            AuraState prayer = target.Auras[auraIndex];
            if (!IsAuraActive(prayer))
            {
                return;
            }

            int healerIndex = FindIndex(prayer.Source);
            if (healerIndex < 0)
            {
                healerIndex = targetIndex;
            }

            float healing = prayer.Magnitude > 0f ? prayer.Magnitude : 16f;
            AbilityId sourceAbility = prayer.Ability;
            RemoveAura(
                targetIndex,
                AuraId.PrayerOfMending,
                target.Id,
                sourceAbility,
                false);
            ApplyHealing(healerIndex, targetIndex, healing, sourceAbility);
        }

        private void ApplyHealing(int sourceIndex, int targetIndex, float amount, AbilityId ability)
        {
            CombatantState target = _combatants[targetIndex];
            if (!target.IsAlive || amount <= 0f)
            {
                return;
            }

            float modifiedAmount = amount * GetHealingTakenMultiplier(target);
            float applied = Math.Min(modifiedAmount, target.MaxHealth - target.Health);
            target.Health += applied;
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.Heal,
                _combatants[sourceIndex].Id,
                target.Id,
                ability,
                amount: applied));
        }

        private void ApplyLegacyBarrier(
            int sourceIndex,
            int targetIndex,
            float amount,
            float seconds,
            AbilityId ability)
        {
            CombatantState target = _combatants[targetIndex];
            target.LegacyBarrier = Math.Max(target.LegacyBarrier, amount);
            target.StatusEndTicks[(int)StatusId.Barrier] = Math.Max(
                target.StatusEndTicks[(int)StatusId.Barrier],
                CurrentTick + SecondsToTicks(seconds));
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.BarrierApplied,
                _combatants[sourceIndex].Id,
                target.Id,
                ability,
                StatusId.Barrier,
                amount));
        }

        private void ApplyLegacyStatus(
            int sourceIndex,
            int targetIndex,
            StatusId status,
            float seconds,
            AbilityId ability)
        {
            if (status == StatusId.None || status == StatusId.Barrier || seconds <= 0f)
            {
                return;
            }

            CombatantState target = _combatants[targetIndex];
            target.StatusEndTicks[(int)status] = Math.Max(
                target.StatusEndTicks[(int)status],
                CurrentTick + SecondsToTicks(seconds));
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.StatusApplied,
                _combatants[sourceIndex].Id,
                target.Id,
                ability,
                status));
        }

        private void ClearLegacyHarmfulStatuses(
            int targetIndex,
            ParticipantId source,
            AbilityId ability)
        {
            CombatantState target = _combatants[targetIndex];
            for (int index = 0; index < LegacyHarmfulStatuses.Length; index++)
            {
                StatusId status = LegacyHarmfulStatuses[index];
                if (target.StatusEndTicks[(int)status] <= CurrentTick)
                {
                    continue;
                }

                target.StatusEndTicks[(int)status] = 0;
                Emit(CombatEvent.Create(
                    CurrentTick,
                    CombatEventKind.StatusRemoved,
                    source,
                    target.Id,
                    ability,
                    status));
            }
        }

        private void Interrupt(
            int targetIndex,
            ParticipantId source,
            AbilityId ability,
            float lockoutSeconds)
        {
            CombatantState target = _combatants[targetIndex];
            if (!target.Cast.IsActive)
            {
                return;
            }

            AbilityId interruptedAbility = target.Cast.Command.Ability;
            SpellSchoolMask interruptedSchool = target.Cast.School;
            target.Cast = default;
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.Interrupted,
                source,
                target.Id,
                ability,
                amount: (float)interruptedAbility));

            if (interruptedSchool == SpellSchoolMask.None || lockoutSeconds <= 0f)
            {
                return;
            }

            int lockEndTick = CurrentTick + SecondsToTicks(lockoutSeconds);
            for (int schoolIndex = 0; schoolIndex < SchoolCapacity; schoolIndex++)
            {
                SpellSchoolMask school = (SpellSchoolMask)(1 << schoolIndex);
                if ((interruptedSchool & school) != 0)
                {
                    target.SchoolLockEndTicks[schoolIndex] = Math.Max(
                        target.SchoolLockEndTicks[schoolIndex],
                        lockEndTick);
                }
            }

            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.StatusApplied,
                source,
                target.Id,
                ability,
                StatusId.SchoolLocked));
        }

        private void CancelCast(int targetIndex, ParticipantId source, string reason)
        {
            CombatantState target = _combatants[targetIndex];
            if (!target.Cast.IsActive)
            {
                return;
            }

            AbilityId interruptedAbility = target.Cast.Command.Ability;
            target.Cast = default;
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.Interrupted,
                source,
                target.Id,
                interruptedAbility,
                reason: reason));
        }

        private void ApplyKnockback(
            int sourceIndex,
            int targetIndex,
            float distance,
            AbilityId ability)
        {
            CombatantState target = _combatants[targetIndex];
            if (target.StatusEndTicks[(int)StatusId.Anchored] > CurrentTick)
            {
                return;
            }

            Float3 offset = target.Position - _combatants[sourceIndex].Position;
            float planarLength = offset.PlanarLength;
            float resolvedDistance = distance;
            if (distance < 0f)
            {
                float maximumPullDistance = Math.Max(
                    0f,
                    planarLength - MinimumPullSeparation);
                resolvedDistance = -Math.Min(Math.Abs(distance), maximumPullDistance);
            }

            Float3 displacement = planarLength > 0.0001f &&
                Math.Abs(resolvedDistance) > 0.0001f
                    ? offset * (resolvedDistance / planarLength)
                    : default;
            Float3 resolvedPosition = _rules.CollisionMap.ResolveKnockback(
                target.Position,
                displacement,
                ArenaLayout.CombatantCollisionRadius);
            displacement = resolvedPosition - target.Position;
            target.Position = resolvedPosition;
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.Knockback,
                _combatants[sourceIndex].Id,
                target.Id,
                ability,
                amount: displacement.PlanarLength,
                vector: displacement));
        }

        private void ApplyTeleport(
            int sourceIndex,
            int selectedTargetIndex,
            CombatCommand command,
            float distance)
        {
            CombatantState actor = _combatants[sourceIndex];
            if (command.Ability == AbilityId.Shadowstep && selectedTargetIndex >= 0)
            {
                CombatantState target = _combatants[selectedTargetIndex];
                if (_rules.CollisionMap.TryResolveShadowstep(
                        target.Position,
                        target.Facing,
                        ShadowstepSeparation,
                        ArenaLayout.CombatantCollisionRadius,
                        out Float3 resolvedPosition))
                {
                    actor.Position = resolvedPosition;
                }

                actor.Facing = (target.Position - actor.Position).PlanarNormalized;
                return;
            }

            Float3 point = command.TargetPoint;
            if ((point - actor.Position).PlanarLengthSquared <=
                MovementCancelThresholdSquared)
            {
                point = actor.Position +
                    (command.AimDirection.PlanarNormalized * distance);
            }

            Float3 desiredPosition = ClampTargetPoint(actor.Position, point, distance);
            actor.Position = _rules.CollisionMap.ResolveBlink(
                actor.Position,
                desiredPosition,
                ArenaLayout.CombatantCollisionRadius);
        }

        private void AddComboPoints(int sourceIndex, int targetIndex, int count)
        {
            if (count <= 0 || !_combatants[targetIndex].IsAlive)
            {
                return;
            }

            CombatantState source = _combatants[sourceIndex];
            ParticipantId target = _combatants[targetIndex].Id;
            if (source.ComboTarget != target)
            {
                source.ComboTarget = target;
                source.ComboPoints = 0;
            }

            source.ComboPoints = Math.Min(
                _rules.MaximumComboPoints,
                source.ComboPoints + count);
        }

        private void ModifyResource(int targetIndex, ResourceType type, float amount)
        {
            CombatantState target = _combatants[targetIndex];
            if (type == ResourceType.None || target.ResourceProfile.Type != type)
            {
                return;
            }

            target.Resource = Math.Max(
                0f,
                Math.Min(target.ResourceProfile.Maximum, target.Resource + amount));
        }

        private void ModifyResourcePercentMaximum(
            int targetIndex,
            ResourceType type,
            float fraction)
        {
            CombatantState target = _combatants[targetIndex];
            if (type == ResourceType.None || target.ResourceProfile.Type != type)
            {
                return;
            }

            ModifyResource(
                targetIndex,
                type,
                target.ResourceProfile.Maximum * fraction);
        }

        private void BurnResource(
            int sourceIndex,
            int targetIndex,
            AbilityId ability,
            AbilityEffectDefinition effect)
        {
            ResourceType type = (ResourceType)effect.Count;
            CombatantState source = _combatants[sourceIndex];
            CombatantState target = _combatants[targetIndex];
            if (type == ResourceType.None || target.ResourceProfile.Type != type ||
                target.ResourceProfile.Maximum <= 0f)
            {
                return;
            }

            float burn = Math.Min(
                target.Resource,
                target.ResourceProfile.Maximum * effect.Amount);
            if (effect.SecondaryAmount > 0f)
            {
                if (source.ResourceProfile.Type != type)
                {
                    return;
                }

                burn = Math.Min(
                    burn,
                    source.ResourceProfile.Maximum * effect.SecondaryAmount);
            }

            target.Resource = Math.Max(0f, target.Resource - burn);
            if (effect.TertiaryAmount > 0f)
            {
                ApplyDamage(
                    sourceIndex,
                    targetIndex,
                    burn * effect.TertiaryAmount,
                    ability,
                    false,
                    effect.School);
            }
        }

        private void SpendResource(CombatantState actor, ResourceCost cost)
        {
            if (cost.Type != ResourceType.None)
            {
                actor.Resource = Math.Max(0f, actor.Resource - cost.Amount);
            }
        }

        private void StartGlobalCooldown(
            CombatantState actor,
            AbilityDefinition definition)
        {
            if ((definition.Requirements & AbilityRequirementFlags.IgnoresGlobalCooldown) != 0)
            {
                return;
            }

            float seconds = definition.GlobalCooldownSeconds;
            if (seconds <= 0f && (int)definition.Id > (int)AbilityId.BossKnockback)
            {
                seconds = _rules.DefaultGlobalCooldownSeconds;
            }

            if (seconds > 0f)
            {
                actor.GlobalCooldownEndTick = Math.Max(
                    actor.GlobalCooldownEndTick,
                    CurrentTick + SecondsToTicks(seconds));
            }
        }

        private void BreakDamageSensitiveAuras(
            int targetIndex,
            ParticipantId source,
            AbilityId ability)
        {
            CombatantState target = _combatants[targetIndex];
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                if (!IsAuraActive(target.Auras[auraIndex]))
                {
                    continue;
                }

                AuraDefinition definition = AuraCatalog.Get((AuraId)auraIndex);
                if ((definition.Tags & AuraTag.BreaksOnDamage) != 0)
                {
                    RemoveAura(
                        targetIndex,
                        (AuraId)auraIndex,
                        source,
                        ability,
                        false);
                }
            }
        }

        private void ExpireLegacyStatusesAndSchoolLocks()
        {
            for (int combatantIndex = 0;
                 combatantIndex < _combatantCount;
                 combatantIndex++)
            {
                CombatantState target = _combatants[combatantIndex];
                for (int statusIndex = 1; statusIndex < StatusCapacity; statusIndex++)
                {
                    if (target.StatusEndTicks[statusIndex] != CurrentTick)
                    {
                        continue;
                    }

                    StatusId status = (StatusId)statusIndex;
                    if (status == StatusId.Barrier)
                    {
                        target.LegacyBarrier = 0f;
                    }

                    Emit(CombatEvent.Create(
                        CurrentTick,
                        CombatEventKind.StatusRemoved,
                        target.Id,
                        target.Id,
                        status: status));
                }

                bool schoolLockExpired = false;
                for (int schoolIndex = 0; schoolIndex < SchoolCapacity; schoolIndex++)
                {
                    if (target.SchoolLockEndTicks[schoolIndex] == CurrentTick)
                    {
                        schoolLockExpired = true;
                    }
                }

                if (schoolLockExpired && GetLockedSchools(target) == SpellSchoolMask.None)
                {
                    Emit(CombatEvent.Create(
                        CurrentTick,
                        CombatEventKind.StatusRemoved,
                        target.Id,
                        target.Id,
                        status: StatusId.SchoolLocked));
                }

                for (int drIndex = 1;
                     drIndex < DiminishingReturnCapacity;
                     drIndex++)
                {
                    if (target.DiminishingReturnResetTicks[drIndex] > 0 &&
                        target.DiminishingReturnResetTicks[drIndex] <= CurrentTick)
                    {
                        target.DiminishingReturnStages[drIndex] = 0;
                        target.DiminishingReturnResetTicks[drIndex] = 0;
                    }
                }
            }
        }

        private CombatantSnapshot CreateSnapshot(CombatantState actor)
        {
            PendingCast cast = actor.Cast;
            return new CombatantSnapshot(
                actor.Id,
                actor.Team,
                actor.Class,
                actor.Specialization,
                actor.Position,
                actor.Facing,
                actor.Health,
                actor.MaxHealth,
                GetTotalBarrier(actor),
                cast.IsActive ? cast.Command.Ability : AbilityId.None,
                cast.IsActive ? cast.StartTick : 0,
                cast.IsActive ? cast.EndTick : 0,
                cast.IsActive ? cast.ActivationType : AbilityActivationType.Instant,
                cast.IsActive ? cast.School : SpellSchoolMask.None,
                actor.ResourceProfile.Type,
                actor.Resource,
                actor.ResourceProfile.Maximum,
                actor.ComboPoints,
                actor.ComboTarget,
                actor.GlobalCooldownEndTick,
                GetLockedSchools(actor),
                actor.IsAlive,
                _rules.RadialHazardsEnabled &&
                    actor.Position.PlanarLength > _rules.SafeRadius,
                GetActiveStatuses(actor),
                GetActiveAuraTags(actor),
                GetDispellableBeneficialAuraTypes(actor),
                actor.Level,
                _rules.PlayableMode);
        }

        private StatusFlags GetActiveStatuses(CombatantState actor)
        {
            StatusFlags flags = StatusFlags.None;
            if (GetTotalBarrier(actor) > 0f)
            {
                flags |= StatusFlags.Barrier;
            }

            if (actor.StatusEndTicks[(int)StatusId.Silenced] > CurrentTick)
            {
                flags |= StatusFlags.Silenced;
            }

            if (actor.StatusEndTicks[(int)StatusId.Anchored] > CurrentTick)
            {
                flags |= StatusFlags.Anchored;
            }

            if (actor.StatusEndTicks[(int)StatusId.Slowed] > CurrentTick)
            {
                flags |= StatusFlags.Slowed;
            }

            AuraTag tags = GetActiveAuraTags(actor);
            if ((tags & AuraTag.Fear) != 0)
            {
                flags |= StatusFlags.Feared;
            }

            if ((tags & AuraTag.Horror) != 0)
            {
                flags |= StatusFlags.Horrified;
            }

            if ((tags & AuraTag.Stun) != 0)
            {
                flags |= StatusFlags.Stunned;
            }

            if ((tags & AuraTag.Disorient) != 0)
            {
                flags |= StatusFlags.Disoriented;
            }

            if ((tags & AuraTag.Silence) != 0)
            {
                flags |= StatusFlags.Silenced;
            }

            if ((tags & AuraTag.Disarm) != 0)
            {
                flags |= StatusFlags.Disarmed;
            }

            if ((tags & AuraTag.Pacified) != 0)
            {
                flags |= StatusFlags.Pacified;
            }

            if ((tags & AuraTag.Root) != 0)
            {
                flags |= StatusFlags.Rooted;
            }

            if ((tags & AuraTag.DamageReduction) != 0)
            {
                flags |= StatusFlags.DamageReduced;
            }

            if ((tags & AuraTag.Stealth) != 0)
            {
                flags |= StatusFlags.Stealthed;
            }

            if ((tags & AuraTag.Poison) != 0)
            {
                flags |= StatusFlags.Poisoned;
            }

            if ((tags & AuraTag.Slow) != 0)
            {
                flags |= StatusFlags.Slowed;
            }

            if (HasAura(actor, AuraId.LivingEmber))
            {
                flags |= StatusFlags.Burning;
            }

            if (GetLockedSchools(actor) != SpellSchoolMask.None)
            {
                flags |= StatusFlags.SchoolLocked;
            }

            return flags;
        }

        private AuraTag GetActiveAuraTags(CombatantState actor)
        {
            AuraTag tags = AuraTag.None;
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                if (IsAuraActive(actor.Auras[auraIndex]))
                {
                    tags |= AuraCatalog.Get((AuraId)auraIndex).Tags;
                }
            }

            return tags;
        }

        private DispelType GetDispellableBeneficialAuraTypes(CombatantState actor)
        {
            DispelType types = DispelType.None;
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                if (!IsAuraActive(actor.Auras[auraIndex]))
                {
                    continue;
                }

                AuraDefinition definition = AuraCatalog.Get((AuraId)auraIndex);
                if (definition.Disposition == AuraDisposition.Beneficial)
                {
                    types |= definition.DispelType;
                }
            }

            return types;
        }

        private float GetTotalBarrier(CombatantState actor)
        {
            float barrier = actor.LegacyBarrier;
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                AuraState aura = actor.Auras[auraIndex];
                if (IsAuraActive(aura))
                {
                    barrier += aura.AbsorbRemaining;
                }
            }

            return barrier;
        }

        private float GetDamageTakenMultiplier(
            CombatantState actor,
            SpellSchoolMask school)
        {
            float multiplier = 1f;
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                if (IsAuraActive(actor.Auras[auraIndex]))
                {
                    AuraDefinition definition = AuraCatalog.Get((AuraId)auraIndex);
                    multiplier *= definition.DamageTakenMultiplier;
                    if ((school & SpellSchoolMask.Physical) != 0)
                    {
                        multiplier *= definition.PhysicalDamageTakenMultiplier;
                    }
                }
            }

            return multiplier;
        }

        private float GetDamageDoneMultiplier(
            CombatantState actor,
            SpellSchoolMask school)
        {
            float damagePerLevel = _rules.PlayableMode == PlayableModeId.PveLeveling
                ? PersistentLevelProgression.DamagePerGainedLevel
                : LevelProgressionCurve.DamagePerGainedLevel;
            float multiplier = actor.DamageMultiplierPermille / 1000f * (1f +
                ((actor.Level - LevelProgressionCurve.MinimumLevel) *
                 damagePerLevel));
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                if (!IsAuraActive(actor.Auras[auraIndex]))
                {
                    continue;
                }

                AuraDefinition definition = AuraCatalog.Get((AuraId)auraIndex);
                if (definition.DamageDoneSchools == SpellSchoolMask.None ||
                    (definition.DamageDoneSchools & school) != 0)
                {
                    multiplier *= definition.DamageDoneMultiplier;
                }
            }

            return multiplier;
        }

        private float GetHealingTakenMultiplier(CombatantState actor)
        {
            float multiplier = 1f;
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                AuraState aura = actor.Auras[auraIndex];
                if (IsAuraActive(aura))
                {
                    AuraDefinition definition = AuraCatalog.Get((AuraId)auraIndex);
                    multiplier *= (definition.Tags & AuraTag.HealingReduction) != 0 &&
                        aura.Magnitude > 0f
                            ? aura.Magnitude
                            : definition.HealingTakenMultiplier;
                }
            }

            return multiplier;
        }

        private float GetCastTimeMultiplier(CombatantState actor)
        {
            float multiplier = 1f;
            for (int auraIndex = 1; auraIndex < AuraCatalog.StorageCapacity; auraIndex++)
            {
                if (IsAuraActive(actor.Auras[auraIndex]))
                {
                    multiplier *= AuraCatalog.Get((AuraId)auraIndex).CastTimeMultiplier;
                }
            }

            return multiplier;
        }

        private SpellSchoolMask GetLockedSchools(CombatantState actor)
        {
            SpellSchoolMask schools = SpellSchoolMask.None;
            for (int schoolIndex = 0; schoolIndex < SchoolCapacity; schoolIndex++)
            {
                if (actor.SchoolLockEndTicks[schoolIndex] > CurrentTick)
                {
                    schools |= (SpellSchoolMask)(1 << schoolIndex);
                }
            }

            return schools;
        }

        private bool IsSchoolLocked(CombatantState actor, SpellSchoolMask school)
        {
            return school != SpellSchoolMask.None &&
                   (GetLockedSchools(actor) & school) != 0;
        }

        private float GetDiminishingReturnMultiplier(
            CombatantState target,
            DiminishingReturnCategory category)
        {
            int index = (int)category;
            if (target.DiminishingReturnResetTicks[index] > 0 &&
                target.DiminishingReturnResetTicks[index] <= CurrentTick)
            {
                target.DiminishingReturnStages[index] = 0;
                target.DiminishingReturnResetTicks[index] = 0;
            }

            switch (target.DiminishingReturnStages[index])
            {
                case 0:
                    return 1f;
                case 1:
                    return 0.5f;
                case 2:
                    return 0.25f;
                default:
                    return 0f;
            }
        }

        private void AdvanceDiminishingReturn(
            CombatantState target,
            DiminishingReturnCategory category)
        {
            int index = (int)category;
            target.DiminishingReturnStages[index] = Math.Min(
                3,
                target.DiminishingReturnStages[index] + 1);
            target.DiminishingReturnResetTicks[index] = 0;
        }

        private bool EvaluateEffectCondition(
            int sourceIndex,
            int selectedTargetIndex,
            AbilityEffectDefinition effect,
            bool selectedTargetSurvived)
        {
            switch (effect.Condition)
            {
                case AbilityEffectCondition.Always:
                    return true;
                case AbilityEffectCondition.TargetIsFriendly:
                    return selectedTargetIndex >= 0 &&
                        !IsEnemy(_combatants[sourceIndex], _combatants[selectedTargetIndex]);
                case AbilityEffectCondition.TargetIsHostile:
                    return selectedTargetIndex >= 0 &&
                        IsEnemy(_combatants[sourceIndex], _combatants[selectedTargetIndex]);
                case AbilityEffectCondition.TargetSurvivedPreviousEffect:
                    return selectedTargetSurvived;
                case AbilityEffectCondition.CasterHasStealthAccess:
                    return HasAuraTag(_combatants[sourceIndex], AuraTag.StealthAccess);
                case AbilityEffectCondition.TargetIsPoisoned:
                    return selectedTargetIndex >= 0 &&
                        HasAuraTag(_combatants[selectedTargetIndex], AuraTag.Poison);
                case AbilityEffectCondition.TargetIsFrozen:
                    return selectedTargetIndex >= 0 &&
                        HasAuraTag(_combatants[selectedTargetIndex], AuraTag.Frozen);
                case AbilityEffectCondition.TargetHasAura:
                    if (selectedTargetIndex < 0)
                    {
                        return false;
                    }

                    int auraIndex = (int)effect.ConditionAura;
                    if (auraIndex <= 0 || auraIndex >= AuraCatalog.StorageCapacity)
                    {
                        return false;
                    }

                    AuraState aura = _combatants[selectedTargetIndex].Auras[auraIndex];
                    return IsAuraActive(aura) &&
                        aura.Source == _combatants[sourceIndex].Id;
                case AbilityEffectCondition.TargetHasResource:
                    if (selectedTargetIndex < 0)
                    {
                        return false;
                    }

                    ResourceType resourceType = (ResourceType)effect.Count;
                    CombatantState target = _combatants[selectedTargetIndex];
                    return resourceType != ResourceType.None &&
                        target.ResourceProfile.Type == resourceType &&
                        target.ResourceProfile.Maximum > 0f;
                default:
                    return false;
            }
        }

        private bool UsesModernMechanics(
            CombatantState actor,
            AbilityDefinition definition)
        {
            if (definition.Effects.IsEmpty && !IsHunterAbility(definition.Id))
            {
                return false;
            }

            return !actor.LegacyCompatibility ||
                   (int)definition.Id > (int)AbilityId.BossKnockback;
        }

        private bool HasControlTag(CombatantState actor, AuraTag tag)
        {
            return (GetActiveAuraTags(actor) & tag) != 0;
        }

        private bool HasAuraTag(CombatantState actor, AuraTag tag)
        {
            return (GetActiveAuraTags(actor) & tag) != 0;
        }

        private bool HasAura(CombatantState actor, AuraId aura)
        {
            int index = (int)aura;
            return index > 0 && index < AuraCatalog.StorageCapacity &&
                   IsAuraActive(actor.Auras[index]);
        }

        private bool HasLivingHostile(CombatantState actor)
        {
            for (int index = 0; index < _combatantCount; index++)
            {
                CombatantState candidate = _combatants[index];
                if (candidate.IsAlive && IsEnemy(actor, candidate))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsAuraActive(AuraState aura)
        {
            return aura.Active && aura.EndTick > CurrentTick;
        }

        private static bool IsEnemy(CombatantState left, CombatantState right)
        {
            return left.Team != right.Team;
        }

        private static AbilityTargetRelation GetRelation(
            CombatantState actor,
            CombatantState target)
        {
            if (actor.Id == target.Id)
            {
                return AbilityTargetRelation.Self;
            }

            return actor.Team == target.Team
                ? AbilityTargetRelation.Friendly
                : AbilityTargetRelation.Hostile;
        }

        private static bool IsFacing(
            Float3 actorPosition,
            Float3 actorFacing,
            Float3 point)
        {
            Float3 toPoint = (point - actorPosition).PlanarNormalized;
            return PlanarDot(actorFacing.PlanarNormalized, toPoint) > FacingThreshold;
        }

        private static bool IsBehind(Float3 actorPosition, CombatantState target)
        {
            Float3 targetToActor = (actorPosition - target.Position).PlanarNormalized;
            return PlanarDot(target.Facing.PlanarNormalized, targetToActor) < BehindThreshold;
        }

        private static float PlanarDot(Float3 left, Float3 right)
        {
            return (left.X * right.X) + (left.Z * right.Z);
        }

        private static bool IsMagicSchool(SpellSchoolMask school)
        {
            return (school & SpellSchoolMask.AllMagic) != 0;
        }

        private static bool IsHardControl(AuraTag tags)
        {
            return (tags &
                (AuraTag.Fear | AuraTag.Stun | AuraTag.Disorient | AuraTag.Horror)) != 0;
        }

        private static StatusId MapAuraStatus(AuraDefinition definition)
        {
            AuraTag tags = definition.Tags;
            if ((tags & AuraTag.Absorb) != 0)
            {
                return StatusId.Barrier;
            }

            if ((tags & AuraTag.Fear) != 0)
            {
                return StatusId.Feared;
            }

            if ((tags & AuraTag.Horror) != 0)
            {
                return StatusId.Horrified;
            }

            if ((tags & AuraTag.Stun) != 0)
            {
                return StatusId.Stunned;
            }

            if ((tags & AuraTag.Disorient) != 0)
            {
                return StatusId.Disoriented;
            }

            if ((tags & AuraTag.Silence) != 0)
            {
                return StatusId.Silenced;
            }

            if ((tags & AuraTag.Disarm) != 0)
            {
                return StatusId.Disarmed;
            }

            if ((tags & AuraTag.Pacified) != 0)
            {
                return StatusId.Pacified;
            }

            if ((tags & AuraTag.Root) != 0)
            {
                return StatusId.Rooted;
            }

            if ((tags & AuraTag.DamageReduction) != 0)
            {
                return StatusId.DamageReduced;
            }

            if ((tags & AuraTag.Stealth) != 0)
            {
                return StatusId.Stealthed;
            }

            if ((tags & AuraTag.Poison) != 0)
            {
                return StatusId.Poisoned;
            }

            if ((tags & AuraTag.Slow) != 0)
            {
                return StatusId.Slowed;
            }

            return definition.Id == AuraId.LivingEmber
                ? StatusId.Burning
                : StatusId.None;
        }

        private Float3 ResolveEffectCenter(
            int sourceIndex,
            int selectedTargetIndex,
            CombatCommand command,
            AbilityTargetShape shape)
        {
            if (shape == AbilityTargetShape.Point ||
                shape == AbilityTargetShape.TargetArea ||
                shape == AbilityTargetShape.Direction)
            {
                return command.TargetPoint;
            }

            if (shape == AbilityTargetShape.Unit && selectedTargetIndex >= 0)
            {
                return _combatants[selectedTargetIndex].Position;
            }

            return _combatants[sourceIndex].Position;
        }

        private void Reject(CombatCommand command, string reason)
        {
            Emit(CombatEvent.Create(
                CurrentTick,
                CombatEventKind.CastRejected,
                command.Actor,
                command.Target,
                command.Ability,
                reason: reason));
        }

        private void Emit(CombatEvent value)
        {
            if (_eventCount < _events.Length)
            {
                _events[_eventCount++] = value;
            }
        }

        private int FindIndex(ParticipantId id)
        {
            for (int index = 0; index < _combatantCount; index++)
            {
                if (_combatants[index].Id == id)
                {
                    return index;
                }
            }

            return -1;
        }

        private void ClearParticipantOwnedState(ParticipantId removed)
        {
            for (int targetIndex = 0; targetIndex < _combatantCount; targetIndex++)
            {
                CombatantState target = _combatants[targetIndex];
                for (int auraIndex = 1;
                     auraIndex < AuraCatalog.StorageCapacity;
                     auraIndex++)
                {
                    AuraState aura = target.Auras[auraIndex];
                    if (aura.Active && aura.Source == removed)
                    {
                        target.Auras[auraIndex] = default;
                    }
                }

                if (target.ComboTarget == removed)
                {
                    target.ComboTarget = default;
                    target.ComboPoints = 0;
                }

                if (target.Cast.IsActive && target.Cast.Command.Target == removed)
                {
                    target.Cast = default;
                }
            }

            for (int summonIndex = 0; summonIndex < _summons.Length; summonIndex++)
            {
                SummonState summon = _summons[summonIndex];
                if (summon.Active &&
                    (summon.Owner == removed || summon.Target == removed))
                {
                    _summons[summonIndex] = default;
                }
            }

            int writeIndex = 0;
            for (int readIndex = 0; readIndex < _commandCount; readIndex++)
            {
                CombatCommand command = _commands[readIndex];
                if (command.Actor == removed || command.Target == removed)
                {
                    continue;
                }

                _commands[writeIndex++] = command;
            }

            _commandCount = writeIndex;
        }

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);

        private void SortCommands()
        {
            for (int index = 1; index < _commandCount; index++)
            {
                CombatCommand value = _commands[index];
                int insertionIndex = index - 1;
                while (insertionIndex >= 0 &&
                       Compare(_commands[insertionIndex], value) > 0)
                {
                    _commands[insertionIndex + 1] = _commands[insertionIndex];
                    insertionIndex--;
                }

                _commands[insertionIndex + 1] = value;
            }
        }

        private static int Compare(CombatCommand left, CombatCommand right)
        {
            int actorComparison = left.Actor.CompareTo(right.Actor);
            if (actorComparison != 0)
            {
                return actorComparison;
            }

            int sequenceComparison = left.Sequence.CompareTo(right.Sequence);
            if (sequenceComparison != 0)
            {
                return sequenceComparison;
            }

            int abilityComparison = left.Ability.CompareTo(right.Ability);
            if (abilityComparison != 0)
            {
                return abilityComparison;
            }

            int targetComparison = left.Target.CompareTo(right.Target);
            if (targetComparison != 0)
            {
                return targetComparison;
            }

            int pointComparison = Compare(left.TargetPoint, right.TargetPoint);
            if (pointComparison != 0)
            {
                return pointComparison;
            }

            int movementComparison = Compare(left.Movement, right.Movement);
            if (movementComparison != 0)
            {
                return movementComparison;
            }

            int facingComparison = Compare(left.Facing, right.Facing);
            return facingComparison != 0
                ? facingComparison
                : Compare(left.AimDirection, right.AimDirection);
        }

        private static int Compare(Float3 left, Float3 right)
        {
            int xComparison = left.X.CompareTo(right.X);
            if (xComparison != 0)
            {
                return xComparison;
            }

            int yComparison = left.Y.CompareTo(right.Y);
            return yComparison != 0
                ? yComparison
                : left.Z.CompareTo(right.Z);
        }

        private int SecondsToTicks(float seconds)
        {
            return seconds <= 0f
                ? 0
                : Math.Max(1, (int)Math.Ceiling(seconds * _rules.TickRate));
        }

        private int SecondsToIntervalTicks(float seconds)
        {
            return seconds <= 0f
                ? 0
                : Math.Max(
                    1,
                    (int)Math.Round(
                        seconds * _rules.TickRate,
                        MidpointRounding.AwayFromZero));
        }

        private static Float3 ClampPlanar(Float3 value, float maxLength)
        {
            float length = value.PlanarLength;
            return length > maxLength && length > 0.0001f
                ? value * (maxLength / length)
                : new Float3(value.X, 0f, value.Z);
        }

        private static Float3 ClampTargetPoint(Float3 origin, Float3 point, float range)
        {
            if (range <= 0f)
            {
                return point;
            }

            Float3 delta = point - origin;
            return origin + ClampPlanar(delta, range);
        }

        private sealed class CombatantState
        {
            public CombatantState()
            {
                CooldownEndTicks = new int[CooldownCapacity];
                StatusEndTicks = new int[StatusCapacity];
                SchoolLockEndTicks = new int[SchoolCapacity];
                Auras = new AuraState[AuraCatalog.StorageCapacity];
                DiminishingReturnStages = new int[DiminishingReturnCapacity];
                DiminishingReturnResetTicks = new int[DiminishingReturnCapacity];
            }

            public CombatantState(
                ParticipantId id,
                TeamId team,
                ClassId classId,
                SpecializationId specialization,
                Float3 position,
                float maxHealth,
                bool legacyCompatibility)
                : this()
            {
                Reset(
                    id,
                    team,
                    classId,
                    specialization,
                    position,
                    maxHealth,
                    legacyCompatibility);
            }

            public void Reset(
                ParticipantId id,
                TeamId team,
                ClassId classId,
                SpecializationId specialization,
                Float3 position,
                float maxHealth,
                bool legacyCompatibility)
            {
                Id = id;
                Team = team;
                Class = classId;
                Specialization = specialization;
                Position = position;
                Facing = new Float3(0f, 0f, 1f);
                MaxHealth = maxHealth;
                Health = maxHealth;
                Level = LevelProgressionCurve.MinimumLevel;
                IsAlive = true;
                LegacyCompatibility = legacyCompatibility;
                ResourceProfile = SpecializationCatalog.GetResourceProfile(specialization);
                Resource = ResourceProfile.Initial;
                LastAbilityStartTick = -1;
                LastSequence = 0u;
                GlobalCooldownEndTick = 0;
                DamageMultiplierPermille = 1000;
                MovementSpeedMultiplierPermille = 1000;
                ComboPoints = 0;
                ComboTarget = default;
                LegacyBarrier = 0f;
                MoveIntent = default;
                Cast = default;
                Array.Clear(CooldownEndTicks, 0, CooldownEndTicks.Length);
                Array.Clear(StatusEndTicks, 0, StatusEndTicks.Length);
                Array.Clear(SchoolLockEndTicks, 0, SchoolLockEndTicks.Length);
                Array.Clear(Auras, 0, Auras.Length);
                Array.Clear(DiminishingReturnStages, 0, DiminishingReturnStages.Length);
                Array.Clear(
                    DiminishingReturnResetTicks,
                    0,
                    DiminishingReturnResetTicks.Length);
            }

            public void ClearForPool()
            {
                Id = default;
                Team = default;
                Class = ClassId.None;
                Specialization = SpecializationId.None;
                Position = default;
                Facing = default;
                MoveIntent = default;
                Health = 0f;
                MaxHealth = 0f;
                Level = LevelProgressionCurve.MinimumLevel;
                LegacyBarrier = 0f;
                IsAlive = false;
                LegacyCompatibility = false;
                LastSequence = 0u;
                LastAbilityStartTick = -1;
                GlobalCooldownEndTick = 0;
                DamageMultiplierPermille = 1000;
                MovementSpeedMultiplierPermille = 1000;
                ResourceProfile = default;
                Resource = 0f;
                ComboPoints = 0;
                ComboTarget = default;
                Cast = default;
                Array.Clear(CooldownEndTicks, 0, CooldownEndTicks.Length);
                Array.Clear(StatusEndTicks, 0, StatusEndTicks.Length);
                Array.Clear(SchoolLockEndTicks, 0, SchoolLockEndTicks.Length);
                Array.Clear(Auras, 0, Auras.Length);
                Array.Clear(DiminishingReturnStages, 0, DiminishingReturnStages.Length);
                Array.Clear(
                    DiminishingReturnResetTicks,
                    0,
                    DiminishingReturnResetTicks.Length);
            }

            public ParticipantId Id;
            public TeamId Team;
            public ClassId Class;
            public SpecializationId Specialization;
            public Float3 Position;
            public Float3 Facing;
            public Float3 MoveIntent;
            public float Health;
            public float MaxHealth;
            public int Level;
            public float LegacyBarrier;
            public bool IsAlive;
            public bool LegacyCompatibility;
            public uint LastSequence;
            public int LastAbilityStartTick;
            public int GlobalCooldownEndTick;
            public int DamageMultiplierPermille;
            public int MovementSpeedMultiplierPermille;
            public ResourceProfile ResourceProfile;
            public float Resource;
            public int ComboPoints;
            public ParticipantId ComboTarget;
            public int[] CooldownEndTicks;
            public int[] StatusEndTicks;
            public int[] SchoolLockEndTicks;
            public AuraState[] Auras;
            public int[] DiminishingReturnStages;
            public int[] DiminishingReturnResetTicks;
            public PendingCast Cast;
        }

        private struct AuraState
        {
            public AuraState(
                bool active,
                ParticipantId source,
                AbilityId ability,
                int endTick,
                int nextPeriodicTick,
                int stacks,
                float absorbRemaining,
                float magnitude = 0f)
            {
                Active = active;
                Source = source;
                Ability = ability;
                EndTick = endTick;
                NextPeriodicTick = nextPeriodicTick;
                Stacks = stacks;
                AbsorbRemaining = absorbRemaining;
                Magnitude = magnitude;
            }

            public bool Active;
            public ParticipantId Source;
            public AbilityId Ability;
            public int EndTick;
            public int NextPeriodicTick;
            public int Stacks;
            public float AbsorbRemaining;
            public float Magnitude;
        }

        private readonly struct SummonState
        {
            public SummonState(
                bool active,
                SummonId id,
                ParticipantId owner,
                ParticipantId target,
                AbilityId ability,
                int endTick,
                int nextPulseTick)
            {
                Active = active;
                Id = id;
                Owner = owner;
                Target = target;
                Ability = ability;
                EndTick = endTick;
                NextPulseTick = nextPulseTick;
            }

            public bool Active { get; }

            public SummonId Id { get; }

            public ParticipantId Owner { get; }

            public ParticipantId Target { get; }

            public AbilityId Ability { get; }

            public int EndTick { get; }

            public int NextPulseTick { get; }

            public SummonState WithNextPulseTick(int tick)
            {
                return new SummonState(
                    Active,
                    Id,
                    Owner,
                    Target,
                    Ability,
                    EndTick,
                    tick);
            }
        }

        private readonly struct PendingCast
        {
            public PendingCast(
                CombatCommand command,
                int startTick,
                int endTick,
                AbilityActivationType activationType,
                SpellSchoolMask school,
                bool breaksOnMovement,
                int remainingChannelTicks,
                int nextChannelTick,
                int channelIntervalTicks,
                bool modern)
            {
                Command = command;
                StartTick = startTick;
                EndTick = endTick;
                ActivationType = activationType;
                School = school;
                BreaksOnMovement = breaksOnMovement;
                RemainingChannelTicks = remainingChannelTicks;
                NextChannelTick = nextChannelTick;
                ChannelIntervalTicks = channelIntervalTicks;
                Modern = modern;
                IsActive = true;
            }

            public CombatCommand Command { get; }

            public int StartTick { get; }

            public int EndTick { get; }

            public AbilityActivationType ActivationType { get; }

            public SpellSchoolMask School { get; }

            public bool BreaksOnMovement { get; }

            public int RemainingChannelTicks { get; }

            public int NextChannelTick { get; }

            public int ChannelIntervalTicks { get; }

            public bool Modern { get; }

            public bool IsActive { get; }

            public PendingCast AfterChannelTick(int nextTick)
            {
                return new PendingCast(
                    Command,
                    StartTick,
                    EndTick,
                    ActivationType,
                    School,
                    BreaksOnMovement,
                    Math.Max(0, RemainingChannelTicks - 1),
                    nextTick,
                    ChannelIntervalTicks,
                    Modern);
            }
        }
    }
}
