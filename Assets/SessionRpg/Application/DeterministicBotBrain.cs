using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    /// <summary>
    /// Deterministic, legible arena AI. It only reads public snapshots and submits
    /// the same CombatCommand values as a human player.
    /// </summary>
    public sealed class DeterministicBotBrain
    {
        private const float RangeEpsilon = 0.001f;

        private uint _randomState;
        private uint _sequence;
        private int _nextDecisionTick;
        private readonly bool _clockwiseDetour;

        public DeterministicBotBrain(int seed)
        {
            _randomState = seed == 0 ? 0x9E3779B9u : (uint)seed;
            _clockwiseDetour = (_randomState & 1u) == 0u;
        }

        public bool TryBuildCommand(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            out CombatCommand command)
        {
            return TryBuildCommandInternal(
                simulation,
                self,
                snapshots,
                snapshotCount,
                default,
                false,
                out command);
        }

        public bool TryBuildCommand(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            in ArenaTeamIntent teamIntent,
            out CombatCommand command)
        {
            return TryBuildCommandInternal(
                simulation,
                self,
                snapshots,
                snapshotCount,
                teamIntent,
                teamIntent.IsFor(self.Team),
                out command);
        }

        private bool TryBuildCommandInternal(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            ArenaTeamIntent teamIntent,
            bool useTeamIntent,
            out CombatCommand command)
        {
            ValidateArguments(simulation, snapshots, snapshotCount);
            if (!self.IsAlive || simulation.CurrentTick < _nextDecisionTick)
            {
                command = default;
                return false;
            }

            int decisionIntervalTicks = Math.Max(1, simulation.Rules.TickRate / 5);
            int deterministicHesitation = (int)(NextRandom() % 3u);
            _nextDecisionTick = simulation.CurrentTick +
                decisionIntervalTicks +
                deterministicHesitation;

            CombatantSnapshot enemy = useTeamIntent
                ? FindCoordinatedEnemy(
                    self,
                    teamIntent,
                    snapshots,
                    snapshotCount)
                : FindNearestEnemy(self, snapshots, snapshotCount);
            CombatantSnapshot ally = useTeamIntent &&
                TryFindFriendlySnapshot(
                    self.Team,
                    teamIntent.PressuredAlly,
                    snapshots,
                    snapshotCount,
                    out CombatantSnapshot pressuredAlly)
                    ? pressuredAlly
                    : FindMostInjuredAlly(self, snapshots, snapshotCount);
            CombatantSnapshot dispelAlly = FindDispelAlly(self, snapshots, snapshotCount);

            bool alreadyCasting = self.CastingAbility != AbilityId.None;
            BotAction action;
            if (alreadyCasting)
            {
                action = BotAction.None;
            }
            else if (!TryChooseSiegeAreaAction(
                    simulation,
                    self,
                    snapshots,
                    snapshotCount,
                    out action))
            {
                action = ChooseAction(
                    simulation,
                    self,
                    enemy,
                    ally,
                    dispelAlly,
                    teamIntent,
                    useTeamIntent);
            }
            Float3 movement = alreadyCasting || IsMovementPrevented(self)
                ? default
                : BuildMovement(simulation, self, enemy, ally);
            Float3 facing = ResolveFacing(self, enemy, action);

            if (action.HasAbility)
            {
                AbilityDefinition definition = AbilityCatalog.Get(action.Ability);
                if (definition.CastProfile.ActivationType != AbilityActivationType.Instant &&
                    definition.CastProfile.BreaksOnMovement)
                {
                    movement = default;
                }
            }

            _sequence++;
            if (!action.HasAbility)
            {
                command = CombatCommand.Move(self.Id, _sequence, movement, facing);
                return true;
            }

            Float3 aimDirection = (action.TargetPoint - self.Position).PlanarLengthSquared >
                RangeEpsilon * RangeEpsilon
                    ? (action.TargetPoint - self.Position).PlanarNormalized
                    : facing;
            command = CombatCommand.Cast(
                self.Id,
                _sequence,
                movement,
                facing,
                action.Ability,
                action.Target,
                action.TargetPoint,
                aimDirection);
            return true;
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

        private BotAction ChooseAction(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            CombatantSnapshot ally,
            CombatantSnapshot dispelAlly,
            ArenaTeamIntent teamIntent,
            bool useTeamIntent)
        {
            SpecializationId specialization = ResolveSpecialization(self);
            if (specialization == SpecializationId.Discipline &&
                HasMeaningfulHardControl(self) &&
                ally.Id.IsValid &&
                HealthRatio(ally) < 0.3f &&
                !HasDamageReduction(ally) &&
                TryAbility(
                    simulation,
                    self,
                    ally,
                    AbilityId.PainSuppression,
                    out BotAction emergencySave))
            {
                return emergencySave;
            }

            if (HasMeaningfulHardControl(self) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.ArenaTrinket,
                    out BotAction controlBreak))
            {
                return controlBreak;
            }

            if (self.IsInLava &&
                TryChooseLavaRecovery(simulation, self, enemy, out BotAction recovery))
            {
                return recovery;
            }

            switch (specialization)
            {
                case SpecializationId.Discipline:
                    return ChooseDiscipline(
                        simulation,
                        self,
                        enemy,
                        ally,
                        dispelAlly);
                case SpecializationId.Shadow:
                    return ChooseShadow(simulation, self, enemy, ally, dispelAlly);
                case SpecializationId.Fire:
                    return ChooseFire(
                        simulation,
                        self,
                        enemy,
                        ally,
                        teamIntent,
                        useTeamIntent);
                case SpecializationId.Frost:
                    return ChooseFrost(
                        simulation,
                        self,
                        enemy,
                        ally,
                        teamIntent,
                        useTeamIntent);
                case SpecializationId.ShadowDance:
                    return ChooseShadowDance(simulation, self, enemy);
                case SpecializationId.Mutilate:
                    return ChooseMutilate(
                        simulation,
                        self,
                        enemy,
                        ally,
                        useTeamIntent && teamIntent.PressuredAlly.IsValid);
                case SpecializationId.Marksmanship:
                    return ChooseMarksmanship(simulation, self, enemy, ally);
                case SpecializationId.Boss:
                    return ChooseBoss(simulation, self, enemy);
                default:
                    return BotAction.None;
            }
        }

        private static BotAction ChooseDiscipline(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            CombatantSnapshot ally,
            CombatantSnapshot dispelAlly)
        {
            float allyHealth = HealthRatio(ally);
            if (enemy.Id.IsValid && enemy.Class == ClassId.Priest &&
                !simulation.HasAura(self.Id, AuraId.FearWard) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.FearWard,
                    out BotAction action))
            {
                return action;
            }

            if (ally.Id.IsValid && allyHealth < 0.3f &&
                !HasDamageReduction(ally) &&
                TryAbility(
                    simulation,
                    self,
                    ally,
                    AbilityId.PainSuppression,
                    out action))
            {
                return action;
            }

            if (ally.Id.IsValid && allyHealth < 0.78f &&
                !HasAbsorb(ally) &&
                !ally.HasAuraTag(AuraTag.PreventsShield) &&
                TryAbility(
                    simulation,
                    self,
                    ally,
                    AbilityId.PowerWordShield,
                    out action))
            {
                return action;
            }

            if (dispelAlly.Id.IsValid &&
                TryAbility(
                    simulation,
                    self,
                    dispelAlly,
                    AbilityId.DispelMagic,
                    out action))
            {
                return action;
            }

            if (ally.Id.IsValid && allyHealth < 0.63f &&
                TryAbility(
                    simulation,
                    self,
                    ally,
                    AbilityId.Penance,
                    out action))
            {
                return action;
            }

            if (ally.Id.IsValid && allyHealth < 0.63f &&
                !simulation.HasAura(ally.Id, AuraId.PrayerOfMending) &&
                TryAbility(
                    simulation,
                    self,
                    ally,
                    AbilityId.PrayerOfMending,
                    out action))
            {
                return action;
            }

            if (ally.Id.IsValid && allyHealth < 0.92f &&
                !simulation.HasAura(ally.Id, AuraId.Renew) &&
                TryAbility(
                    simulation,
                    self,
                    ally,
                    AbilityId.Renew,
                    out action))
            {
                return action;
            }

            if (ally.Id.IsValid && allyHealth < 0.86f &&
                TryAbility(
                    simulation,
                    self,
                    ally,
                    AbilityId.FlashHeal,
                    out action))
            {
                return action;
            }

            float enemyDistance = Distance(self, enemy);
            if (enemy.Id.IsValid &&
                (enemyDistance <= 3.8f || allyHealth < 0.55f) &&
                !HasBreakableControl(enemy) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.PsychicScream,
                    out action))
            {
                return action;
            }

            if (enemy.Id.IsValid && ResourceRatio(self) < 0.55f &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Shadowfiend,
                    out action))
            {
                return action;
            }

            if (enemy.Id.IsValid && HealthRatio(enemy) < 0.42f &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.ShadowWordDeath,
                    out action))
            {
                return action;
            }

            if (enemy.Id.IsValid &&
                !HasBreakableControl(enemy) &&
                !self.HasAuraTag(AuraTag.CastHaste) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.PowerInfusion,
                    out action))
            {
                return action;
            }

            if (enemy.Id.IsValid && !HasBreakableControl(enemy) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Penance,
                    out action))
            {
                return action;
            }

            return enemy.Id.IsValid && !HasBreakableControl(enemy) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Smite,
                    out action)
                    ? action
                    : BotAction.None;
        }

        private static BotAction ChooseShadow(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            CombatantSnapshot ally,
            CombatantSnapshot dispelAlly)
        {
            if (HealthRatio(self) < 0.34f &&
                TryAbility(simulation, self, self, AbilityId.Dispersion, out BotAction action))
            {
                return action;
            }

            if ((IsRooted(self) || self.HasStatus(StatusId.Slowed)) &&
                TryAbility(simulation, self, self, AbilityId.Fade, out action))
            {
                return action;
            }

            if (HealthRatio(self) < 0.74f && !HasAbsorb(self) &&
                !self.HasAuraTag(AuraTag.PreventsShield) &&
                TryAbility(simulation, self, self, AbilityId.PowerWordShield, out action))
            {
                return action;
            }

            if (HealthRatio(self) < 0.62f &&
                !simulation.HasAura(self.Id, AuraId.PrayerOfMending) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.PrayerOfMending,
                    out action))
            {
                return action;
            }

            if (dispelAlly.Id.IsValid &&
                TryAbility(simulation, self, dispelAlly, AbilityId.DispelMagic, out action))
            {
                return action;
            }

            if (!enemy.Id.IsValid)
            {
                return BotAction.None;
            }

            if (enemy.CastingAbility != AbilityId.None &&
                TryAbility(simulation, self, enemy, AbilityId.Silence, out action))
            {
                return action;
            }

            if (Distance(self, enemy) <= 5.2f && !HasBreakableControl(enemy) &&
                TryAbility(simulation, self, enemy, AbilityId.PsychicScream, out action))
            {
                return action;
            }

            if (!simulation.HasAura(enemy.Id, AuraId.VampiricTouch) &&
                TryAbility(simulation, self, enemy, AbilityId.VampiricTouch, out action))
            {
                return action;
            }

            if (!simulation.HasAura(enemy.Id, AuraId.ShadowWordPain) &&
                TryAbility(simulation, self, enemy, AbilityId.ShadowWordPain, out action))
            {
                return action;
            }

            if (!simulation.HasAura(enemy.Id, AuraId.DevouringPlague) &&
                TryAbility(simulation, self, enemy, AbilityId.DevouringPlague, out action))
            {
                return action;
            }

            if (HealthRatio(enemy) < 0.28f &&
                TryAbility(simulation, self, enemy, AbilityId.ShadowWordDeath, out action))
            {
                return action;
            }

            if (ResourceRatio(self) < 0.38f &&
                TryAbility(simulation, self, enemy, AbilityId.Shadowfiend, out action))
            {
                return action;
            }

            if (TryAbility(simulation, self, enemy, AbilityId.MindBlast, out action))
            {
                return action;
            }

            return TryAbility(simulation, self, enemy, AbilityId.MindFlay, out action)
                ? action
                : BotAction.None;
        }

        private static BotAction ChooseMarksmanship(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            CombatantSnapshot ally)
        {
            if (HealthRatio(self) < 0.34f &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.Deterrence,
                    out BotAction action))
            {
                return action;
            }

            if ((IsRooted(self) || self.HasStatus(StatusId.Slowed)) &&
                TryAbility(simulation, self, self, AbilityId.MastersCall, out action))
            {
                return action;
            }

            if (IsNearSafeEdge(simulation, self) &&
                TryAbilityWithFacing(
                    simulation,
                    self,
                    self,
                    AbilityId.Disengage,
                    self.Position.PlanarNormalized,
                    out action))
            {
                return action;
            }

            if (!enemy.Id.IsValid)
            {
                return BotAction.None;
            }

            if (enemy.CastingAbility != AbilityId.None &&
                TryAbility(simulation, self, enemy, AbilityId.SilencingShot, out action))
            {
                return action;
            }

            if (enemy.HasDispellableBeneficialAura(
                    DispelType.Magic | DispelType.Enrage) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.TranquilizingShot,
                    out action))
            {
                return action;
            }

            float distance = Distance(self, enemy);
            if (distance < 4.2f)
            {
                if (TryAbility(
                        simulation,
                        self,
                        self,
                        AbilityId.Disengage,
                        out action))
                {
                    return action;
                }

                if (TryAbility(simulation, self, enemy, AbilityId.WingClip, out action))
                {
                    return action;
                }

                if (!HasBreakableControl(enemy) &&
                    TryAbility(simulation, self, enemy, AbilityId.ScatterShot, out action))
                {
                    return action;
                }
            }

            if (simulation.TryGetCompanionSnapshot(self.Id, out CompanionSnapshot companion) &&
                companion.IsAlive &&
                (companion.Order != CompanionOrder.Attack || companion.Target != enemy.Id) &&
                TryAbility(simulation, self, enemy, AbilityId.PetAttack, out action))
            {
                return action;
            }

            if (!simulation.HasAura(enemy.Id, AuraId.SerpentSting) &&
                TryAbility(simulation, self, enemy, AbilityId.SerpentSting, out action))
            {
                return action;
            }

            if (HealthRatio(enemy) <= 0.2f &&
                TryAbility(simulation, self, enemy, AbilityId.KillShot, out action))
            {
                return action;
            }

            if (TryAbility(simulation, self, enemy, AbilityId.AimedShot, out action))
            {
                return action;
            }

            if (TryAbility(simulation, self, enemy, AbilityId.ChimeraShot, out action))
            {
                return action;
            }

            if (ally.Id.IsValid && HealthRatio(ally) < 0.42f &&
                TryAbility(simulation, self, ally, AbilityId.RoarOfSacrifice, out action))
            {
                return action;
            }

            return TryAbility(simulation, self, enemy, AbilityId.SteadyShot, out action)
                ? action
                : BotAction.None;
        }

        private static BotAction ChooseFire(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            CombatantSnapshot ally,
            ArenaTeamIntent teamIntent,
            bool useTeamIntent)
        {
            if (!enemy.Id.IsValid)
            {
                return BotAction.None;
            }

            if (HealthRatio(self) < 0.28f &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.IceBlock,
                    out BotAction action))
            {
                return action;
            }

            if (HealthRatio(self) < 0.62f && !HasAbsorb(self) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.ManaShield,
                    out action))
            {
                return action;
            }

            if (enemy.CastingAbility != AbilityId.None &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Counterspell,
                    out action))
            {
                return action;
            }

            if (enemy.HasDispellableBeneficialAura(DispelType.Magic) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Spellsteal,
                    out action))
            {
                return action;
            }

            if (HasBreakableControl(enemy))
            {
                return BotAction.None;
            }

            float distance = Distance(self, enemy);
            bool coordinatedPeel = useTeamIntent &&
                teamIntent.PressuredAlly.IsValid;
            if (coordinatedPeel &&
                enemy.Id != teamIntent.FocusTarget &&
                !enemy.HasAuraTag(AuraTag.Periodic) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Polymorph,
                    out action))
            {
                return action;
            }

            if (coordinatedPeel &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.DragonBreath,
                    out action))
            {
                return action;
            }

            if (ShouldUsePolymorph(self, ally, enemy, distance) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Polymorph,
                    out action))
            {
                return action;
            }

            if (distance <= AbilityCatalog.Get(AbilityId.BlastWave).Targeting.Radius +
                    RangeEpsilon &&
                (enemy.IsInLava || IsNearSafeEdge(simulation, enemy)) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.BlastWave,
                    out action))
            {
                return action;
            }

            if (enemy.HasAuraTag(AuraTag.Periodic) &&
                HealthRatio(enemy) < 0.98f &&
                distance > AbilityCatalog.Get(AbilityId.DragonBreath).Targeting.Radius +
                    RangeEpsilon &&
                !self.HasAuraTag(AuraTag.DamageBoost) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.Combustion,
                    out action))
            {
                return action;
            }

            if (TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.DragonBreath,
                    out action))
            {
                return action;
            }

            if (!enemy.HasAuraTag(AuraTag.Periodic) &&
                !enemy.HasStatus(StatusId.Burning) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.LivingBomb,
                    out action))
            {
                return action;
            }

            if (HealthRatio(enemy) < 0.32f &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.FireBlast,
                    out action))
            {
                return action;
            }

            if (TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Fireball,
                    out action))
            {
                return action;
            }

            return TryAbility(
                simulation,
                self,
                enemy,
                AbilityId.FireBlast,
                out action)
                    ? action
                    : BotAction.None;
        }

        private static BotAction ChooseFrost(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            CombatantSnapshot ally,
            ArenaTeamIntent teamIntent,
            bool useTeamIntent)
        {
            if (HealthRatio(self) < 0.28f &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.IceBlock,
                    out BotAction action))
            {
                return action;
            }

            if (enemy.Id.IsValid && enemy.CastingAbility != AbilityId.None &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Counterspell,
                    out action))
            {
                return action;
            }

            if (HealthRatio(self) < 0.8f && !HasAbsorb(self) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.IceBarrier,
                    out action))
            {
                return action;
            }

            if (!enemy.Id.IsValid || HasBreakableControl(enemy))
            {
                return BotAction.None;
            }

            if (enemy.HasDispellableBeneficialAura(DispelType.Magic) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Spellsteal,
                    out action))
            {
                return action;
            }

            float distance = Distance(self, enemy);
            if (useTeamIntent &&
                teamIntent.PressuredAlly.IsValid &&
                enemy.Id != teamIntent.FocusTarget &&
                !enemy.HasAuraTag(AuraTag.Periodic) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Polymorph,
                    out action))
            {
                return action;
            }

            if (ShouldUsePolymorph(self, ally, enemy, distance) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Polymorph,
                    out action))
            {
                return action;
            }

            if (IsFrozen(enemy))
            {
                if (!IsStunned(enemy) &&
                    TryAbility(
                        simulation,
                        self,
                        enemy,
                        AbilityId.DeepFreeze,
                        out action))
                {
                    return action;
                }

                if (TryAbility(
                        simulation,
                        self,
                        enemy,
                        AbilityId.IceLance,
                        out action))
                {
                    return action;
                }
            }

            if (!IsFrozen(enemy) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.FrostNova,
                    out action))
            {
                return action;
            }

            if (distance > AbilityCatalog.Get(AbilityId.FrostNova).Targeting.Radius +
                    RangeEpsilon &&
                !self.HasAuraTag(AuraTag.CastHaste) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.IcyVeins,
                    out action))
            {
                return action;
            }

            if (!IsFrozen(enemy) &&
                distance > AbilityCatalog.Get(AbilityId.FrostNova).Targeting.Radius +
                    RangeEpsilon &&
                TryPointAbility(
                    simulation,
                    self,
                    enemy.Position,
                    AbilityId.WaterElementalFreeze,
                    out action))
            {
                return action;
            }

            if (TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Frostbolt,
                    out action))
            {
                return action;
            }

            return TryAbility(
                simulation,
                self,
                enemy,
                AbilityId.IceLance,
                out action)
                    ? action
                    : BotAction.None;
        }

        private static BotAction ChooseShadowDance(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy)
        {
            if (!enemy.Id.IsValid)
            {
                return BotAction.None;
            }

            if (HasHarmfulMagic(self) && HealthRatio(self) < 0.8f &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.CloakOfShadows,
                    out BotAction action))
            {
                return action;
            }

            if (enemy.CastingAbility != AbilityId.None &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Kick,
                    out action))
            {
                return action;
            }

            float distance = Distance(self, enemy);
            bool closePhysicalThreat =
                (enemy.Class == ClassId.Rogue || enemy.Class == ClassId.Boss) &&
                distance <= 3.5f + RangeEpsilon;
            if (!HasBreakableControl(enemy) &&
                !HasDamageReduction(self) &&
                closePhysicalThreat &&
                HealthRatio(self) < 0.85f &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.Evasion,
                    out action))
            {
                return action;
            }

            if (HasBreakableControl(enemy))
            {
                return BotAction.None;
            }

            bool comboBound = HasTargetBoundComboPoints(self, enemy);
            if (comboBound && self.ComboPoints >= 4 &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Eviscerate,
                    out action))
            {
                return action;
            }

            if (comboBound && self.ComboPoints >= 3 && !IsStunned(enemy) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.KidneyShot,
                    out action))
            {
                return action;
            }

            if (HasStealthAccess(self) && !IsBehind(self, enemy) &&
                !IsStunned(enemy) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.CheapShot,
                    out action))
            {
                return action;
            }

            if ((distance > 3f || !IsBehind(self, enemy)) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Shadowstep,
                    out action))
            {
                return action;
            }

            if (HasStealthAccess(self) && IsBehind(self, enemy) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Ambush,
                    out action))
            {
                return action;
            }

            if (!HasStealthAccess(self) && distance <= 3f && IsBehind(self, enemy) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.ShadowDance,
                    out action))
            {
                return action;
            }

            return TryAbility(
                simulation,
                self,
                enemy,
                AbilityId.Hemorrhage,
                out action)
                    ? action
                    : BotAction.None;
        }

        private static BotAction ChooseMutilate(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            CombatantSnapshot ally,
            bool coordinatedPeel)
        {
            if (!enemy.Id.IsValid)
            {
                return BotAction.None;
            }

            if (HasHarmfulMagic(self) && HealthRatio(self) < 0.8f &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.CloakOfShadows,
                    out BotAction action))
            {
                return action;
            }

            if (enemy.CastingAbility != AbilityId.None &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Kick,
                    out action))
            {
                return action;
            }

            if ((HealthRatio(self) < 0.28f || IsRooted(self)) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.Vanish,
                    out action))
            {
                return action;
            }

            if (HasBreakableControl(enemy))
            {
                return BotAction.None;
            }

            if (HasStealthAccess(self) && !IsStunned(enemy) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.CheapShot,
                    out action))
            {
                return action;
            }

            if ((Math.Min(HealthRatio(self), HealthRatio(ally)) < 0.4f ||
                 coordinatedPeel) &&
                !enemy.HasAuraTag(AuraTag.Periodic) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Blind,
                    out action))
            {
                return action;
            }

            bool poisoned = IsPoisoned(enemy);
            bool comboBound = HasTargetBoundComboPoints(self, enemy);
            if (poisoned && comboBound && self.ComboPoints >= 4 &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Envenom,
                    out action))
            {
                return action;
            }

            if (comboBound && self.ComboPoints >= 3 && !IsStunned(enemy) &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.KidneyShot,
                    out action))
            {
                return action;
            }

            if (poisoned && comboBound && self.ComboPoints <= 2 &&
                Distance(self, enemy) <= 3f + RangeEpsilon &&
                !self.HasAuraTag(AuraTag.DamageBoost) &&
                TryAbility(
                    simulation,
                    self,
                    self,
                    AbilityId.ColdBlood,
                    out action))
            {
                return action;
            }

            if (!poisoned &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Shiv,
                    out action))
            {
                return action;
            }

            if (TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Mutilate,
                    out action))
            {
                return action;
            }

            return TryAbility(
                simulation,
                self,
                enemy,
                AbilityId.Shiv,
                out action)
                    ? action
                    : BotAction.None;
        }

        private static BotAction ChooseBoss(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy)
        {
            if (!enemy.Id.IsValid)
            {
                return BotAction.None;
            }

            if (TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.BossKnockback,
                    out BotAction action))
            {
                return action;
            }

            if (TryPointAbility(
                    simulation,
                    self,
                    enemy.Position,
                    AbilityId.BossMeteor,
                    out action))
            {
                return action;
            }

            return TryAbility(
                simulation,
                self,
                enemy,
                AbilityId.BossCleave,
                out action)
                    ? action
                    : BotAction.None;
        }

        private static bool TryChooseLavaRecovery(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            out BotAction action)
        {
            SpecializationId specialization = ResolveSpecialization(self);
            if (specialization == SpecializationId.Fire ||
                specialization == SpecializationId.Frost)
            {
                Float3 recoveryPoint = BuildRecoveryPoint(
                    self,
                    AbilityCatalog.Get(AbilityId.Blink).Targeting.Range);
                if (TryPointAbility(
                        simulation,
                        self,
                        recoveryPoint,
                        AbilityId.Blink,
                        out action))
                {
                    return true;
                }
            }

            if (specialization == SpecializationId.ShadowDance &&
                enemy.Id.IsValid &&
                enemy.Position.PlanarLength + 0.5f < self.Position.PlanarLength &&
                TryAbility(
                    simulation,
                    self,
                    enemy,
                    AbilityId.Shadowstep,
                    out action))
            {
                return true;
            }

            if (specialization == SpecializationId.Marksmanship &&
                TryAbilityWithFacing(
                    simulation,
                    self,
                    self,
                    AbilityId.Disengage,
                    self.Position.PlanarNormalized,
                    out action))
            {
                return true;
            }

            action = default;
            return false;
        }

        private static bool TryAbility(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot target,
            AbilityId ability,
            out BotAction action)
        {
            Float3 targetPoint = target.Id.IsValid ? target.Position : self.Position;
            if (!CanAttempt(simulation, self, target, targetPoint, ability))
            {
                action = default;
                return false;
            }

            AbilityDefinition definition = AbilityCatalog.Get(ability);
            ParticipantId targetId = definition.Targeting.Allows(AbilityTargetRelation.Self) &&
                !definition.Targeting.Allows(AbilityTargetRelation.Friendly) &&
                !definition.Targeting.Allows(AbilityTargetRelation.Hostile)
                    ? self.Id
                    : target.Id;
            action = new BotAction(ability, targetId, targetPoint);
            return true;
        }

        private static bool TryChooseSiegeAreaAction(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            out BotAction action)
        {
            SpecializationId specialization = ResolveSpecialization(self);
            AbilityId areaAbility =
                ModeAbilityKitCatalog.GetSiegeAreaAbility(specialization);
            if (simulation.Rules.PlayableMode != PlayableModeId.HeroSiege ||
                areaAbility == AbilityId.None ||
                !AbilityCatalog.TryGet(
                    areaAbility,
                    out AbilityDefinition definition))
            {
                action = default;
                return false;
            }

            float radius = definition.Targeting.Radius;
            if (definition.Targeting.Shape == AbilityTargetShape.CasterArea)
            {
                int nearbyHostiles = 0;
                for (int index = 0; index < snapshotCount; index++)
                {
                    CombatantSnapshot candidate = snapshots[index];
                    if (!candidate.IsAlive || candidate.Team == self.Team ||
                        Float3.PlanarDistance(
                            self.Position,
                            candidate.Position) > radius + RangeEpsilon)
                    {
                        continue;
                    }

                    nearbyHostiles++;
                    if (nearbyHostiles >= 2)
                    {
                        return TryAbility(
                            simulation,
                            self,
                            candidate,
                            areaAbility,
                            out action);
                    }
                }

                action = default;
                return false;
            }

            if (definition.Targeting.Shape == AbilityTargetShape.TargetArea)
            {
                for (int centerIndex = 0;
                     centerIndex < snapshotCount;
                     centerIndex++)
                {
                    CombatantSnapshot center = snapshots[centerIndex];
                    if (!center.IsAlive || center.Team == self.Team ||
                        Float3.PlanarDistance(
                            self.Position,
                            center.Position) >
                            definition.Targeting.Range + RangeEpsilon)
                    {
                        continue;
                    }

                    int clusteredHostiles = 0;
                    for (int targetIndex = 0;
                         targetIndex < snapshotCount;
                         targetIndex++)
                    {
                        CombatantSnapshot candidate = snapshots[targetIndex];
                        if (candidate.IsAlive && candidate.Team != self.Team &&
                            Float3.PlanarDistance(
                                center.Position,
                                candidate.Position) <= radius + RangeEpsilon)
                        {
                            clusteredHostiles++;
                        }
                    }

                    if (clusteredHostiles >= 2 &&
                        TryPointAbility(
                            simulation,
                            self,
                            center.Position,
                            areaAbility,
                            out action))
                    {
                        return true;
                    }
                }
            }

            action = default;
            return false;
        }

        private static bool TryAbilityWithFacing(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot target,
            AbilityId ability,
            Float3 facing,
            out BotAction action)
        {
            if (!TryAbility(simulation, self, target, ability, out BotAction resolved))
            {
                action = default;
                return false;
            }

            Float3 normalizedFacing = facing.PlanarNormalized;
            if (normalizedFacing.PlanarLengthSquared <= RangeEpsilon * RangeEpsilon)
            {
                normalizedFacing = self.Facing.PlanarNormalized;
            }

            action = new BotAction(
                resolved.Ability,
                resolved.Target,
                resolved.TargetPoint,
                normalizedFacing);
            return true;
        }

        private static bool TryPointAbility(
            CombatSimulation simulation,
            CombatantSnapshot self,
            Float3 targetPoint,
            AbilityId ability,
            out BotAction action)
        {
            if (!CanAttempt(simulation, self, default, targetPoint, ability))
            {
                action = default;
                return false;
            }

            action = new BotAction(ability, default, targetPoint);
            return true;
        }

        private static bool CanAttempt(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot target,
            Float3 targetPoint,
            AbilityId ability)
        {
            if (!AbilityCatalog.TryGet(ability, out AbilityDefinition definition) ||
                !AbilityCatalog.IsAbilityAllowed(
                    self.Class,
                    ResolveSpecialization(self),
                    ability,
                    simulation.Rules.PlayableMode) ||
                self.CastingAbility != AbilityId.None ||
                simulation.GetCooldownRemaining(self.Id, ability) > RangeEpsilon)
            {
                return false;
            }

            AbilityRequirementFlags requirements = definition.Requirements;
            bool usableWhileHardControlled =
                (requirements & AbilityRequirementFlags.UsableWhileHardControlled) != 0;
            bool usableWhileStunned = usableWhileHardControlled ||
                (requirements & AbilityRequirementFlags.UsableWhileStunned) != 0;
            bool usableWhileSilenced =
                (requirements & AbilityRequirementFlags.UsableWhileSilenced) != 0;
            if ((IsStunned(self) && !usableWhileStunned) ||
                (!usableWhileHardControlled &&
                 (IsFeared(self) || IsDisoriented(self) || IsHorrified(self))))
            {
                return false;
            }

            if ((requirements & AbilityRequirementFlags.IgnoresGlobalCooldown) == 0 &&
                self.GlobalCooldownEndTick > simulation.CurrentTick)
            {
                return false;
            }

            if (definition.ResourceCost.Type != ResourceType.None &&
                (self.ResourceType != definition.ResourceCost.Type ||
                 self.Resource + RangeEpsilon < definition.ResourceCost.Amount))
            {
                return false;
            }

            if (self.HasAuraTag(AuraTag.AbilityLock) ||
                (self.HasAuraTag(AuraTag.Pacified) && definition.IsHostile))
            {
                return false;
            }

            if (self.HasAuraTag(AuraTag.Disarm) &&
                AbilityCatalog.IsWeaponAbility(ability))
            {
                return false;
            }

            if ((definition.School & self.LockedSchools) != 0 ||
                (definition.School != SpellSchoolMask.Physical &&
                 self.HasStatus(StatusId.Silenced) &&
                 !usableWhileSilenced))
            {
                return false;
            }

            if (!ValidateTargetRule(self, target, targetPoint, definition.Targeting))
            {
                return false;
            }

            AbilityTargetShape shape = definition.Targeting.Shape;
            bool pointTarget = shape == AbilityTargetShape.Point ||
                shape == AbilityTargetShape.Direction ||
                shape == AbilityTargetShape.TargetArea;
            Float3 lineOfSightTarget = pointTarget
                ? targetPoint
                : target.Position;
            if (definition.Targeting.RequiresLineOfSight &&
                (pointTarget || target.Id.IsValid) &&
                !simulation.Rules.CollisionMap.HasLineOfSight(
                    self.Position,
                    lineOfSightTarget))
            {
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresActiveEnemyCast) != 0 &&
                target.CastingAbility == AbilityId.None)
            {
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresStealthAccess) != 0 &&
                !HasStealthAccess(self))
            {
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresBehindTarget) != 0 &&
                !IsBehind(self, target))
            {
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresComboPoints) != 0 &&
                !HasTargetBoundComboPoints(self, target))
            {
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresPoisonedTarget) != 0 &&
                !IsPoisoned(target))
            {
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresFrozenTarget) != 0 &&
                !IsFrozen(target))
            {
                return false;
            }

            if ((requirements & AbilityRequirementFlags.TargetBelowTwentyPercentHealth) != 0 &&
                HealthRatio(target) > 0.2f + RangeEpsilon)
            {
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresManaTarget) != 0 &&
                (target.ResourceType != ResourceType.Mana ||
                 target.MaxResource <= RangeEpsilon))
            {
                return false;
            }

            if ((requirements & AbilityRequirementFlags.RequiresLivingCompanion) != 0 &&
                (!simulation.TryGetCompanionSnapshot(self.Id, out CompanionSnapshot companion) ||
                 !companion.IsAlive))
            {
                return false;
            }

            return (requirements & AbilityRequirementFlags.TargetMustNotHaveWeakenedSoul) == 0 ||
                !target.HasAuraTag(AuraTag.PreventsShield);
        }

        private static bool ValidateTargetRule(
            CombatantSnapshot self,
            CombatantSnapshot target,
            Float3 targetPoint,
            AbilityTargetRule rule)
        {
            switch (rule.Shape)
            {
                case AbilityTargetShape.Unit:
                    if (!target.Id.IsValid || !target.IsAlive)
                    {
                        return false;
                    }

                    AbilityTargetRelation relation = ResolveRelation(self, target);
                    float distance = Distance(self, target);
                    return rule.Allows(relation) &&
                        (relation != AbilityTargetRelation.Hostile ||
                         !target.HasAuraTag(AuraTag.TargetingDropped)) &&
                        (target.Id == self.Id ||
                         (distance <= rule.Range + RangeEpsilon &&
                          distance + RangeEpsilon >= rule.MinimumRange));
                case AbilityTargetShape.CasterArea:
                    if (!rule.Allows(AbilityTargetRelation.Hostile))
                    {
                        return true;
                    }

                    return target.Id.IsValid &&
                        ResolveRelation(self, target) == AbilityTargetRelation.Hostile &&
                        Distance(self, target) <= rule.Radius + RangeEpsilon;
                case AbilityTargetShape.Point:
                case AbilityTargetShape.TargetArea:
                case AbilityTargetShape.Direction:
                    return Float3.PlanarDistance(self.Position, targetPoint) <=
                        rule.Range + RangeEpsilon;
                default:
                    return false;
            }
        }

        private Float3 BuildMovement(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            CombatantSnapshot ally)
        {
            if (self.IsInLava)
            {
                return (default(Float3) - self.Position).PlanarNormalized;
            }

            if (ResolveSpecialization(self) == SpecializationId.Discipline &&
                ally.Id.IsValid &&
                HealthRatio(ally) < 0.8f &&
                Distance(self, ally) > 12.5f)
            {
                return (ally.Position - self.Position).PlanarNormalized;
            }

            if (!enemy.Id.IsValid)
            {
                return self.Position.PlanarLength > simulation.Rules.SafeRadius * 0.7f
                    ? (default(Float3) - self.Position).PlanarNormalized
                    : default;
            }

            Float3 toEnemy = enemy.Position - self.Position;
            float distance = toEnemy.PlanarLength;
            ArenaCollisionMap collisionMap = simulation.Rules.CollisionMap;
            if (collisionMap.IsEnabled &&
                !collisionMap.HasLineOfSight(self.Position, enemy.Position))
            {
                return collisionMap.BuildDetourDirection(
                    self.Position,
                    enemy.Position,
                    _clockwiseDetour);
            }

            float desiredDistance = GetDesiredRange(ResolveSpecialization(self));
            if (distance > desiredDistance + 1f)
            {
                return toEnemy.PlanarNormalized;
            }

            bool melee = ResolveSpecialization(self) == SpecializationId.ShadowDance ||
                ResolveSpecialization(self) == SpecializationId.Mutilate ||
                ResolveSpecialization(self) == SpecializationId.Boss;
            if (!melee && distance < desiredDistance - 1.5f)
            {
                return toEnemy.PlanarNormalized * -0.75f;
            }

            float strafeSign = (_randomState & 1u) == 0u ? 1f : -1f;
            Float3 tangent = new Float3(-toEnemy.Z, 0f, toEnemy.X).PlanarNormalized;
            return tangent * (0.45f * strafeSign);
        }

        private static Float3 ResolveFacing(
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            BotAction action)
        {
            if (action.HasFacingOverride)
            {
                return action.FacingOverride;
            }

            if (action.HasAbility &&
                AbilityCatalog.Get(action.Ability).Targeting.RequiresFacing &&
                (action.TargetPoint - self.Position).PlanarLengthSquared >
                    RangeEpsilon * RangeEpsilon)
            {
                return (action.TargetPoint - self.Position).PlanarNormalized;
            }

            if (enemy.Id.IsValid)
            {
                return (enemy.Position - self.Position).PlanarNormalized;
            }

            Float3 towardCenter = default(Float3) - self.Position;
            return towardCenter.PlanarLengthSquared > RangeEpsilon * RangeEpsilon
                ? towardCenter.PlanarNormalized
                : self.Facing.PlanarNormalized;
        }

        private static CombatantSnapshot FindNearestEnemy(
            CombatantSnapshot self,
            CombatantSnapshot[] snapshots,
            int count)
        {
            CombatantSnapshot best = default;
            float bestDistance = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                CombatantSnapshot candidate = snapshots[index];
                if (!candidate.IsAlive ||
                    candidate.Id == self.Id ||
                    candidate.Team == self.Team ||
                    candidate.HasAuraTag(AuraTag.TargetingDropped) ||
                    (candidate.HasAuraTag(AuraTag.Stealth) &&
                     !candidate.HasAuraTag(AuraTag.Marked)))
                {
                    continue;
                }

                float distance = Distance(self, candidate);
                if (distance < bestDistance ||
                    (Math.Abs(distance - bestDistance) < RangeEpsilon &&
                     candidate.Id.Value < best.Id.Value))
                {
                    best = candidate;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private static CombatantSnapshot FindCoordinatedEnemy(
            CombatantSnapshot self,
            ArenaTeamIntent intent,
            CombatantSnapshot[] snapshots,
            int count)
        {
            CombatantSnapshot personalNearest = FindNearestEnemy(
                self,
                snapshots,
                count);
            TryFindFriendlySnapshot(
                self.Team,
                intent.PressuredAlly,
                snapshots,
                count,
                out CombatantSnapshot pressuredAlly);

            CombatantSnapshot best = default;
            float bestScore = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                CombatantSnapshot candidate = snapshots[index];
                if (!candidate.IsAlive ||
                    candidate.Team == self.Team ||
                    candidate.HasAuraTag(AuraTag.TargetingDropped) ||
                    (candidate.HasAuraTag(AuraTag.Stealth) &&
                     !candidate.HasAuraTag(AuraTag.Marked)) ||
                    intent.IsProtectedControl(candidate.Id))
                {
                    continue;
                }

                float score = Distance(self, candidate);
                int teammatePursuers = intent.GetPursuitCount(candidate.Id);
                if (personalNearest.Id == candidate.Id && teammatePursuers > 0)
                {
                    teammatePursuers--;
                }

                score += teammatePursuers * 3.25f;
                if (candidate.Id == intent.FocusTarget)
                {
                    score -= 1.5f;
                }

                if (candidate.Id == intent.ActiveControlledEnemy)
                {
                    score -= 5.5f;
                }

                if (pressuredAlly.Id.IsValid)
                {
                    score += Float3.PlanarDistance(
                        pressuredAlly.Position,
                        candidate.Position) * 0.2f;
                }

                if (self.ComboPoints > 0 && self.ComboTarget == candidate.Id)
                {
                    score -= 10f + self.ComboPoints;
                }

                if (score < bestScore ||
                    (Math.Abs(score - bestScore) < RangeEpsilon &&
                     candidate.Id.Value < best.Id.Value))
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private static bool TryFindFriendlySnapshot(
            TeamId team,
            ParticipantId participant,
            CombatantSnapshot[] snapshots,
            int count,
            out CombatantSnapshot snapshot)
        {
            if (participant.IsValid)
            {
                for (int index = 0; index < count; index++)
                {
                    CombatantSnapshot candidate = snapshots[index];
                    if (candidate.Id == participant &&
                        candidate.Team == team &&
                        candidate.IsAlive)
                    {
                        snapshot = candidate;
                        return true;
                    }
                }
            }

            snapshot = default;
            return false;
        }

        private static CombatantSnapshot FindMostInjuredAlly(
            CombatantSnapshot self,
            CombatantSnapshot[] snapshots,
            int count)
        {
            CombatantSnapshot best = self;
            float bestRatio = HealthRatio(self);
            for (int index = 0; index < count; index++)
            {
                CombatantSnapshot candidate = snapshots[index];
                if (!candidate.IsAlive ||
                    candidate.Team != self.Team ||
                    candidate.MaxHealth <= 0f)
                {
                    continue;
                }

                float ratio = HealthRatio(candidate);
                if (ratio < bestRatio ||
                    (Math.Abs(ratio - bestRatio) < RangeEpsilon &&
                     candidate.Id.Value < best.Id.Value))
                {
                    best = candidate;
                    bestRatio = ratio;
                }
            }

            return best;
        }

        private static CombatantSnapshot FindDispelAlly(
            CombatantSnapshot self,
            CombatantSnapshot[] snapshots,
            int count)
        {
            CombatantSnapshot best = default;
            float bestRatio = float.MaxValue;
            for (int index = 0; index < count; index++)
            {
                CombatantSnapshot candidate = snapshots[index];
                if (!candidate.IsAlive ||
                    candidate.Team != self.Team ||
                    !HasHarmfulMagic(candidate) ||
                    Distance(self, candidate) >
                        AbilityCatalog.Get(AbilityId.DispelMagic).Targeting.Range +
                        RangeEpsilon)
                {
                    continue;
                }

                float ratio = HealthRatio(candidate);
                if (ratio < bestRatio ||
                    (Math.Abs(ratio - bestRatio) < RangeEpsilon &&
                     candidate.Id.Value < best.Id.Value))
                {
                    best = candidate;
                    bestRatio = ratio;
                }
            }

            return best;
        }

        private static bool ShouldUsePolymorph(
            CombatantSnapshot self,
            CombatantSnapshot ally,
            CombatantSnapshot enemy,
            float enemyDistance)
        {
            return enemyDistance > 5f &&
                Math.Min(HealthRatio(self), HealthRatio(ally)) < 0.42f &&
                !enemy.HasAuraTag(AuraTag.Periodic) &&
                !HasBreakableControl(enemy);
        }

        private static bool HasHarmfulMagic(CombatantSnapshot value)
        {
            const AuraTag harmfulMagic = AuraTag.Fear |
                AuraTag.Horror |
                AuraTag.Silence |
                AuraTag.Disorient |
                AuraTag.Root |
                AuraTag.Slow |
                AuraTag.Periodic;
            const StatusFlags harmfulStatuses = StatusFlags.Feared |
                StatusFlags.Horrified |
                StatusFlags.Silenced |
                StatusFlags.Disoriented |
                StatusFlags.Rooted |
                StatusFlags.Slowed |
                StatusFlags.Burning;
            return (value.ActiveAuraTags & harmfulMagic) != 0 ||
                (value.ActiveStatuses & harmfulStatuses) != 0;
        }

        private static bool HasBreakableControl(CombatantSnapshot value)
        {
            return value.HasAuraTag(AuraTag.Fear) ||
                value.HasAuraTag(AuraTag.Disorient) ||
                value.HasStatus(StatusId.Feared) ||
                value.HasStatus(StatusId.Disoriented);
        }

        private static bool HasMeaningfulHardControl(CombatantSnapshot value)
        {
            const AuraTag meaningfulHardControl = AuraTag.Fear |
                AuraTag.Horror |
                AuraTag.Stun |
                AuraTag.Disorient;
            return (value.ActiveAuraTags & meaningfulHardControl) != 0;
        }

        private static bool HasDamageReduction(CombatantSnapshot value)
        {
            return value.HasAuraTag(AuraTag.DamageReduction) ||
                value.HasStatus(StatusId.DamageReduced);
        }

        private static bool HasAbsorb(CombatantSnapshot value)
        {
            return value.Barrier > RangeEpsilon ||
                value.HasAuraTag(AuraTag.Absorb) ||
                value.HasStatus(StatusId.Barrier);
        }

        private static bool HasStealthAccess(CombatantSnapshot value)
        {
            return value.HasAuraTag(AuraTag.StealthAccess) ||
                value.HasStatus(StatusId.Stealthed);
        }

        private static bool IsPoisoned(CombatantSnapshot value)
        {
            return value.HasAuraTag(AuraTag.Poison) ||
                value.HasStatus(StatusId.Poisoned);
        }

        private static bool IsFrozen(CombatantSnapshot value)
        {
            return value.HasAuraTag(AuraTag.Frozen) ||
                (value.ActiveAuraTags & (AuraTag.Root | AuraTag.Slow)) ==
                    (AuraTag.Root | AuraTag.Slow);
        }

        private static bool IsStunned(CombatantSnapshot value)
        {
            return value.HasAuraTag(AuraTag.Stun) ||
                value.HasStatus(StatusId.Stunned);
        }

        private static bool IsFeared(CombatantSnapshot value)
        {
            return value.HasAuraTag(AuraTag.Fear) ||
                value.HasStatus(StatusId.Feared);
        }

        private static bool IsDisoriented(CombatantSnapshot value)
        {
            return value.HasAuraTag(AuraTag.Disorient) ||
                value.HasStatus(StatusId.Disoriented);
        }

        private static bool IsHorrified(CombatantSnapshot value)
        {
            return value.HasAuraTag(AuraTag.Horror) ||
                value.HasStatus(StatusId.Horrified);
        }

        private static bool IsRooted(CombatantSnapshot value)
        {
            return value.HasAuraTag(AuraTag.Root) ||
                value.HasStatus(StatusId.Rooted);
        }

        private static bool IsMovementPrevented(CombatantSnapshot value)
        {
            return IsStunned(value) ||
                IsFeared(value) ||
                IsHorrified(value) ||
                IsDisoriented(value) ||
                IsRooted(value);
        }

        private static bool HasTargetBoundComboPoints(
            CombatantSnapshot self,
            CombatantSnapshot target)
        {
            return self.ComboPoints > 0 &&
                target.Id.IsValid &&
                self.ComboTarget == target.Id;
        }

        private static bool IsBehind(
            CombatantSnapshot self,
            CombatantSnapshot target)
        {
            if (!target.Id.IsValid)
            {
                return false;
            }

            Float3 targetToSelf = (self.Position - target.Position).PlanarNormalized;
            Float3 targetFacing = target.Facing.PlanarNormalized;
            float dot = (targetToSelf.X * targetFacing.X) +
                (targetToSelf.Z * targetFacing.Z);
            return dot < -0.25f;
        }

        private static AbilityTargetRelation ResolveRelation(
            CombatantSnapshot self,
            CombatantSnapshot target)
        {
            if (target.Id == self.Id)
            {
                return AbilityTargetRelation.Self;
            }

            return target.Team == self.Team
                ? AbilityTargetRelation.Friendly
                : AbilityTargetRelation.Hostile;
        }

        private static SpecializationId ResolveSpecialization(CombatantSnapshot self)
        {
            return self.Specialization != SpecializationId.None
                ? self.Specialization
                : SpecializationCatalog.GetDefault(self.Class);
        }

        private static Float3 BuildRecoveryPoint(
            CombatantSnapshot self,
            float maximumDistance)
        {
            Float3 towardCenter = default(Float3) - self.Position;
            float distance = Math.Min(maximumDistance, towardCenter.PlanarLength);
            return self.Position + (towardCenter.PlanarNormalized * distance);
        }

        private static float GetDesiredRange(SpecializationId specialization)
        {
            switch (specialization)
            {
                case SpecializationId.Discipline:
                    return 10.5f;
                case SpecializationId.Shadow:
                    return 11.5f;
                case SpecializationId.Fire:
                    return 11.5f;
                case SpecializationId.Frost:
                    return 13f;
                case SpecializationId.ShadowDance:
                case SpecializationId.Mutilate:
                    return 2.1f;
                case SpecializationId.Marksmanship:
                    return 12.5f;
                case SpecializationId.Boss:
                    return 3.5f;
                default:
                    return 8f;
            }
        }

        private static bool IsNearSafeEdge(
            CombatSimulation simulation,
            CombatantSnapshot value)
        {
            return !value.IsInLava &&
                value.Position.PlanarLength >=
                    Math.Max(0f, simulation.Rules.SafeRadius - 2f);
        }

        private static float HealthRatio(CombatantSnapshot value)
        {
            return value.Id.IsValid && value.MaxHealth > 0f
                ? value.Health / value.MaxHealth
                : 1f;
        }

        private static float ResourceRatio(CombatantSnapshot value)
        {
            return value.MaxResource > 0f
                ? value.Resource / value.MaxResource
                : 1f;
        }

        private static float Distance(
            CombatantSnapshot left,
            CombatantSnapshot right)
        {
            return right.Id.IsValid
                ? Float3.PlanarDistance(left.Position, right.Position)
                : float.MaxValue;
        }

        private uint NextRandom()
        {
            uint value = _randomState;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _randomState = value;
            return value;
        }

        private readonly struct BotAction
        {
            public BotAction(
                AbilityId ability,
                ParticipantId target,
                Float3 targetPoint)
                : this(ability, target, targetPoint, default)
            {
            }

            public BotAction(
                AbilityId ability,
                ParticipantId target,
                Float3 targetPoint,
                Float3 facingOverride)
            {
                Ability = ability;
                Target = target;
                TargetPoint = targetPoint;
                FacingOverride = facingOverride;
            }

            public AbilityId Ability { get; }

            public ParticipantId Target { get; }

            public Float3 TargetPoint { get; }

            public Float3 FacingOverride { get; }

            public bool HasFacingOverride =>
                FacingOverride.PlanarLengthSquared > RangeEpsilon * RangeEpsilon;

            public bool HasAbility => Ability != AbilityId.None;

            public static BotAction None => default;
        }
    }
}
