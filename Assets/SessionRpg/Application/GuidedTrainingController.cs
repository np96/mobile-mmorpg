using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    public enum GuidedTrainingStatus
    {
        Recommended,
        InProgress,
        Completed,
        Skipped,
    }

    /// <summary>
    /// Ordered player-facing coaching steps. Completion is represented by
    /// <see cref="GuidedTrainingStatus.Completed"/>, not by an additional step.
    /// </summary>
    public enum GuidedTrainingStep
    {
        Movement,
        Targeting,
        BasicCombat,
        HazardRecovery,
        AbilityProgression,
        RecoveryDefense,
        ControlInterrupt,
        FinalPack,
        FlickMastery,
    }

    public enum GuidedTrainingObservationKind
    {
        None,
        Movement,
        TargetSelected,
        CombatActionResolved,
        HazardEntered,
        HazardRecovered,
        PackCleared,
        FlickActionResolved,
    }

    /// <summary>
    /// A semantic, presentation-independent input to the training state machine.
    /// Unity input and combat adapters decide when an observation has been earned;
    /// this value carries stable identifiers and explicit authoritative Core state.
    /// </summary>
    public readonly struct GuidedTrainingObservation
    {
        private GuidedTrainingObservation(
            GuidedTrainingObservationKind kind,
            ParticipantId target,
            AbilityId ability,
            AbilitySlotDirection direction,
            int packNumber,
            Float3 position,
            bool isInHazard,
            bool hasHazardStateEvidence)
        {
            Kind = kind;
            Target = target;
            Ability = ability;
            Direction = direction;
            PackNumber = packNumber;
            Position = position;
            IsInHazard = isInHazard;
            HasHazardStateEvidence = hasHazardStateEvidence;
        }

        public GuidedTrainingObservationKind Kind { get; }

        public ParticipantId Target { get; }

        public AbilityId Ability { get; }

        public AbilitySlotDirection Direction { get; }

        public int PackNumber { get; }

        /// <summary>
        /// Authoritative Core position captured after a simulation tick. It is
        /// meaningful only when <see cref="HasHazardStateEvidence"/> is true.
        /// </summary>
        public Float3 Position { get; }

        /// <summary>
        /// Authoritative hazard membership at <see cref="Position"/>. A recovery
        /// observation is eligible only when this is false.
        /// </summary>
        public bool IsInHazard { get; }

        /// <summary>
        /// Distinguishes post-simulation state evidence from legacy intent-only
        /// observations. The latter remain source-compatible but cannot complete
        /// the recovery lesson.
        /// </summary>
        public bool HasHazardStateEvidence { get; }

        public static GuidedTrainingObservation Moved()
        {
            return new GuidedTrainingObservation(
                GuidedTrainingObservationKind.Movement,
                default,
                AbilityId.None,
                AbilitySlotDirection.None,
                0,
                default,
                false,
                false);
        }

        public static GuidedTrainingObservation SelectedTarget(ParticipantId target)
        {
            return new GuidedTrainingObservation(
                GuidedTrainingObservationKind.TargetSelected,
                target,
                AbilityId.None,
                AbilitySlotDirection.None,
                0,
                default,
                false,
                false);
        }

        public static GuidedTrainingObservation ResolvedCombatAction(AbilityId ability)
        {
            return new GuidedTrainingObservation(
                GuidedTrainingObservationKind.CombatActionResolved,
                default,
                ability,
                AbilitySlotDirection.None,
                0,
                default,
                false,
                false);
        }

        /// <summary>
        /// Compatibility signal without post-simulation state evidence. It can
        /// expose that a hazard/knockback event occurred, but cannot establish a
        /// recovery origin or complete the lesson.
        /// </summary>
        public static GuidedTrainingObservation EnteredHazard()
        {
            return new GuidedTrainingObservation(
                GuidedTrainingObservationKind.HazardEntered,
                default,
                AbilityId.None,
                AbilitySlotDirection.None,
                0,
                default,
                false,
                false);
        }

        /// <summary>
        /// Records the authoritative position produced by a hazard-damage or
        /// knockback event. Unlike <see cref="EnteredHazard()"/>, this evidence can
        /// establish the start of a measurable recovery transition.
        /// </summary>
        public static GuidedTrainingObservation EnteredHazard(Float3 position)
        {
            return new GuidedTrainingObservation(
                GuidedTrainingObservationKind.HazardEntered,
                default,
                AbilityId.None,
                AbilitySlotDirection.None,
                0,
                position,
                false,
                true);
        }

        /// <summary>
        /// Compatibility signal without post-simulation state evidence. It never
        /// completes the recovery lesson; adapters should use
        /// <see cref="ObservedHazardRecovery"/>.
        /// </summary>
        public static GuidedTrainingObservation RecoveredFromHazard()
        {
            return new GuidedTrainingObservation(
                GuidedTrainingObservationKind.HazardRecovered,
                default,
                AbilityId.None,
                AbilitySlotDirection.None,
                0,
                default,
                false,
                false);
        }

        /// <summary>
        /// Observes authoritative state after simulation has resolved movement.
        /// The training controller requires both a safe state and meaningful
        /// displacement from the recorded hazard/knockback position.
        /// </summary>
        public static GuidedTrainingObservation ObservedHazardRecovery(
            Float3 position,
            bool isInHazard)
        {
            return new GuidedTrainingObservation(
                GuidedTrainingObservationKind.HazardRecovered,
                default,
                AbilityId.None,
                AbilitySlotDirection.None,
                0,
                position,
                isInHazard,
                true);
        }

        public static GuidedTrainingObservation ClearedPack(int packNumber)
        {
            return new GuidedTrainingObservation(
                GuidedTrainingObservationKind.PackCleared,
                default,
                AbilityId.None,
                AbilitySlotDirection.None,
                packNumber,
                default,
                false,
                false);
        }

        public static GuidedTrainingObservation ResolvedFlickAction(
            AbilityId ability,
            AbilitySlotDirection direction)
        {
            return new GuidedTrainingObservation(
                GuidedTrainingObservationKind.FlickActionResolved,
                default,
                ability,
                direction,
                0,
                default,
                false,
                false);
        }
    }

    public readonly struct GuidedTrainingObservationResult
    {
        internal GuidedTrainingObservationResult(
            bool wasHandled,
            GuidedTrainingStep stepBefore,
            GuidedTrainingStep stepAfter,
            int completedStepCount,
            AbilityId unlockedAbility,
            int unlockedAbilityCount,
            bool trainingCompleted)
        {
            WasHandled = wasHandled;
            StepBefore = stepBefore;
            StepAfter = stepAfter;
            CompletedStepCount = completedStepCount;
            UnlockedAbility = unlockedAbility;
            UnlockedAbilityCount = unlockedAbilityCount;
            TrainingCompleted = trainingCompleted;
        }

        public bool WasHandled { get; }

        public GuidedTrainingStep StepBefore { get; }

        public GuidedTrainingStep StepAfter { get; }

        public bool StepAdvanced =>
            WasHandled && (StepBefore != StepAfter || TrainingCompleted);

        public int CompletedStepCount { get; }

        public AbilityId UnlockedAbility { get; }

        public bool AbilityUnlocked => UnlockedAbility != AbilityId.None;

        public int UnlockedAbilityCount { get; }

        public bool TrainingCompleted { get; }
    }

    public readonly struct GuidedTrainingSnapshot
    {
        internal GuidedTrainingSnapshot(
            SpecializationId specialization,
            GuidedTrainingStatus status,
            GuidedTrainingStep currentStep,
            int completedStepCount,
            int clearedPackCount,
            int unlockedAbilityCount,
            bool hazardEntered,
            bool hazardRecoveryEvidenceReady)
        {
            Specialization = specialization;
            Status = status;
            CurrentStep = currentStep;
            CompletedStepCount = completedStepCount;
            ClearedPackCount = clearedPackCount;
            UnlockedAbilityCount = unlockedAbilityCount;
            HazardEntered = hazardEntered;
            HazardRecoveryEvidenceReady = hazardRecoveryEvidenceReady;
        }

        public SpecializationId Specialization { get; }

        public GuidedTrainingStatus Status { get; }

        public GuidedTrainingStep CurrentStep { get; }

        public int CompletedStepCount { get; }

        public int ClearedPackCount { get; }

        public int UnlockedAbilityCount { get; }

        public bool HazardEntered { get; }

        public bool HazardRecoveryEvidenceReady { get; }

        public bool IsRecommended => Status == GuidedTrainingStatus.Recommended;

        public bool CanSkip =>
            Status == GuidedTrainingStatus.Recommended ||
            Status == GuidedTrainingStatus.InProgress;
    }

    /// <summary>
    /// The training action ramp is deliberately distinct from character level.
    /// Three actions are available immediately; packs one through four each add
    /// exactly one action from the stable seven-action leveling kit.
    /// </summary>
    public static class GuidedTrainingAbilityCatalog
    {
        public const int BasicCombatProgressionSlot = 0;
        public const int StartingAbilityCount = 3;
        public const int UnlockingPackCount = 4;
        public const int FinalPackNumber = 5;
        public const int TotalAbilityCount = 7;

        public static int GetUnlockedAbilityCount(int clearedPackCount)
        {
            return StartingAbilityCount + Math.Max(
                0,
                Math.Min(UnlockingPackCount, clearedPackCount));
        }

        public static AbilityId GetAbility(
            SpecializationId specialization,
            int progressionSlot)
        {
            if (progressionSlot < 0 || progressionSlot >= TotalAbilityCount)
            {
                throw new ArgumentOutOfRangeException(nameof(progressionSlot));
            }

            return LevelingAbilityUnlockCatalog.GetAbility(
                specialization,
                progressionSlot);
        }

        /// <summary>
        /// The authored opener taught by Basic Combat. Other initially unlocked
        /// actions remain usable, but cannot silently satisfy this lesson.
        /// </summary>
        public static AbilityId GetBasicCombatAbility(
            SpecializationId specialization)
        {
            return GetAbility(specialization, BasicCombatProgressionSlot);
        }

        public static bool IsUnlocked(
            SpecializationId specialization,
            AbilityId ability,
            int clearedPackCount)
        {
            if (ability == AbilityId.None ||
                !LevelingAbilityUnlockCatalog.TryGetKit(
                    specialization,
                    out AbilityKit kit))
            {
                return false;
            }

            int unlockedCount = GetUnlockedAbilityCount(clearedPackCount);
            for (int slot = 0; slot < unlockedCount; slot++)
            {
                if (kit.GetAbility(slot) == ability)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Bounded semantic grouping used by the tutorial only. Combat behavior
        /// remains owned by Core ability definitions; these lists identify one
        /// learnable defensive or recovery action in every seven-action kit.
        /// </summary>
        public static bool IsRecoveryOrDefense(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.FlashHeal:
                case AbilityId.PowerWordShield:
                case AbilityId.Dispersion:
                case AbilityId.Blink:
                case AbilityId.IceBarrier:
                case AbilityId.Stealth:
                case AbilityId.Vanish:
                case AbilityId.Disengage:
                case AbilityId.Deterrence:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Bounded semantic grouping for the control/interrupt lesson. Every
        /// playable specialization has at least one listed action in its seven-
        /// action learning kit.
        /// </summary>
        public static bool IsControlOrInterrupt(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.PsychicScream:
                case AbilityId.Polymorph:
                case AbilityId.FrostNova:
                case AbilityId.DragonBreath:
                case AbilityId.DeepFreeze:
                case AbilityId.Sap:
                case AbilityId.KidneyShot:
                case AbilityId.FreezingArrow:
                    return true;
                default:
                    return false;
            }
        }
    }

    /// <summary>
    /// Stable post-route practice target used when an earned lesson remains. Unity
    /// may attach the Application-owned Rift Warden brain only while Hazard
    /// Recovery is outstanding, then leaves the target inert for kit/flick practice.
    /// The route occupies participants 1 through 11, leaving this final Core
    /// combatant slot intentionally reserved.
    /// </summary>
    public static class GuidedTrainingPracticeTargetCatalog
    {
        public const float MaximumHealth = 100000f;

        private static readonly SpawnId StableSpawnId = new SpawnId(3001u);
        private static readonly ParticipantId StableParticipantId =
            new ParticipantId(12);
        private static readonly Float3 StablePosition = new Float3(
            216f,
            0f,
            ArenaCollisionMap.LevelingCenterZ);

        public static SpawnId Spawn => StableSpawnId;

        public static ParticipantId Participant => StableParticipantId;

        public static Float3 Position => StablePosition;
    }

    /// <summary>
    /// Deterministic, allocation-free-after-construction guided training state.
    /// It is recommended by default and can be skipped without withholding the
    /// seven training actions from an experienced player.
    /// </summary>
    public sealed class GuidedTrainingController
    {
        public const int StepCount = 9;
        public const float MinimumHazardRecoveryDistance = 0.75f;

        private const float MinimumHazardRecoveryDistanceSquared =
            MinimumHazardRecoveryDistance * MinimumHazardRecoveryDistance;

        private readonly SpecializationId _specialization;
        private GuidedTrainingStep _currentStep;
        private int _clearedPackCount;
        private bool _hazardEntered;
        private bool _hazardEntryHasStateEvidence;
        private Float3 _hazardEntryPosition;
        private bool _recoveryOrDefenseResolved;
        private bool _controlOrInterruptResolved;

        public GuidedTrainingController(SpecializationId specialization)
        {
            if (!LevelingAbilityUnlockCatalog.TryGetKit(
                    specialization,
                    out AbilityKit _))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(specialization),
                    specialization,
                    "Guided training requires a playable specialization.");
            }

            _specialization = specialization;
            Reset();
        }

        public SpecializationId Specialization => _specialization;

        public GuidedTrainingStatus Status { get; private set; }

        public GuidedTrainingStep CurrentStep => _currentStep;

        public int CompletedStepCount =>
            Status == GuidedTrainingStatus.Completed
                ? StepCount
                : (int)_currentStep;

        public int ClearedPackCount => _clearedPackCount;

        public int UnlockedAbilityCount =>
            Status == GuidedTrainingStatus.Skipped
                ? GuidedTrainingAbilityCatalog.TotalAbilityCount
                : GuidedTrainingAbilityCatalog.GetUnlockedAbilityCount(
                    _clearedPackCount);

        public bool IsRecommended => Status == GuidedTrainingStatus.Recommended;

        public bool CanSkip =>
            Status == GuidedTrainingStatus.Recommended ||
            Status == GuidedTrainingStatus.InProgress;

        public GuidedTrainingSnapshot Snapshot => new GuidedTrainingSnapshot(
            _specialization,
            Status,
            _currentStep,
            CompletedStepCount,
            _clearedPackCount,
            UnlockedAbilityCount,
            _hazardEntered,
            _hazardEntryHasStateEvidence);

        public bool Start()
        {
            if (Status != GuidedTrainingStatus.Recommended)
            {
                return false;
            }

            Status = GuidedTrainingStatus.InProgress;
            return true;
        }

        public bool Skip()
        {
            if (!CanSkip)
            {
                return false;
            }

            Status = GuidedTrainingStatus.Skipped;
            return true;
        }

        public void Reset()
        {
            Status = GuidedTrainingStatus.Recommended;
            _currentStep = GuidedTrainingStep.Movement;
            _clearedPackCount = 0;
            _hazardEntered = false;
            _hazardEntryHasStateEvidence = false;
            _hazardEntryPosition = default;
            _recoveryOrDefenseResolved = false;
            _controlOrInterruptResolved = false;
        }

        public GuidedTrainingObservationResult Observe(
            GuidedTrainingObservation observation)
        {
            if (Status == GuidedTrainingStatus.Completed ||
                Status == GuidedTrainingStatus.Skipped)
            {
                return default;
            }

            GuidedTrainingStep stepBefore = _currentStep;
            bool wasRecommended = Status == GuidedTrainingStatus.Recommended;
            AbilityId unlockedAbility = AbilityId.None;
            bool wasHandled = false;

            switch (observation.Kind)
            {
                case GuidedTrainingObservationKind.Movement:
                    if (_currentStep == GuidedTrainingStep.Movement)
                    {
                        _currentStep = GuidedTrainingStep.Targeting;
                        wasHandled = true;
                    }

                    break;
                case GuidedTrainingObservationKind.TargetSelected:
                    if (_currentStep == GuidedTrainingStep.Targeting &&
                        observation.Target.IsValid)
                    {
                        _currentStep = GuidedTrainingStep.BasicCombat;
                        wasHandled = true;
                    }

                    break;
                case GuidedTrainingObservationKind.CombatActionResolved:
                    bool actionIsUnlocked =
                        GuidedTrainingAbilityCatalog.IsUnlocked(
                            _specialization,
                            observation.Ability,
                            _clearedPackCount);
                    if (!actionIsUnlocked)
                    {
                        break;
                    }

                    if (GuidedTrainingAbilityCatalog.IsRecoveryOrDefense(
                            observation.Ability) &&
                        !_recoveryOrDefenseResolved)
                    {
                        _recoveryOrDefenseResolved = true;
                        wasHandled = true;
                    }

                    if (GuidedTrainingAbilityCatalog.IsControlOrInterrupt(
                            observation.Ability) &&
                        !_controlOrInterruptResolved)
                    {
                        _controlOrInterruptResolved = true;
                        wasHandled = true;
                    }

                    if (_currentStep == GuidedTrainingStep.BasicCombat &&
                        observation.Ability ==
                            GuidedTrainingAbilityCatalog.GetBasicCombatAbility(
                                _specialization))
                    {
                        _currentStep = GuidedTrainingStep.HazardRecovery;
                        wasHandled = true;
                    }

                    AdvancePastSatisfiedPackSteps();

                    break;
                case GuidedTrainingObservationKind.HazardEntered:
                    if (_currentStep == GuidedTrainingStep.HazardRecovery)
                    {
                        bool learnedNewState = !_hazardEntered ||
                            (observation.HasHazardStateEvidence &&
                             !_hazardEntryHasStateEvidence);
                        _hazardEntered = true;
                        if (observation.HasHazardStateEvidence)
                        {
                            _hazardEntryPosition = observation.Position;
                            _hazardEntryHasStateEvidence = true;
                        }

                        wasHandled = learnedNewState;
                    }

                    break;
                case GuidedTrainingObservationKind.HazardRecovered:
                    if (_currentStep == GuidedTrainingStep.HazardRecovery &&
                        _hazardEntered &&
                        _hazardEntryHasStateEvidence &&
                        observation.HasHazardStateEvidence &&
                        !observation.IsInHazard &&
                        (observation.Position - _hazardEntryPosition)
                            .PlanarLengthSquared >=
                            MinimumHazardRecoveryDistanceSquared)
                    {
                        _currentStep = GuidedTrainingStep.AbilityProgression;
                        wasHandled = true;
                        AdvancePastSatisfiedPackSteps();
                    }

                    break;
                case GuidedTrainingObservationKind.PackCleared:
                    if (observation.PackNumber == _clearedPackCount + 1 &&
                        observation.PackNumber <=
                            GuidedTrainingAbilityCatalog.FinalPackNumber)
                    {
                        _clearedPackCount++;
                        wasHandled = true;
                        if (_clearedPackCount <=
                            GuidedTrainingAbilityCatalog.UnlockingPackCount)
                        {
                            unlockedAbility =
                                GuidedTrainingAbilityCatalog.GetAbility(
                                    _specialization,
                                    UnlockedAbilityCount - 1);
                        }

                        AdvancePastSatisfiedPackSteps();
                    }

                    break;
                case GuidedTrainingObservationKind.FlickActionResolved:
                    if (_currentStep == GuidedTrainingStep.FlickMastery &&
                        observation.Ability ==
                            GuidedTrainingAbilityCatalog.GetAbility(
                                _specialization,
                                GuidedTrainingAbilityCatalog.TotalAbilityCount - 1) &&
                        observation.Direction == AbilitySlotDirection.Up)
                    {
                        Status = GuidedTrainingStatus.Completed;
                        wasHandled = true;
                    }

                    break;
            }

            if (!wasHandled)
            {
                return default;
            }

            if (wasRecommended && Status == GuidedTrainingStatus.Recommended)
            {
                Status = GuidedTrainingStatus.InProgress;
            }

            return new GuidedTrainingObservationResult(
                true,
                stepBefore,
                _currentStep,
                CompletedStepCount,
                unlockedAbility,
                UnlockedAbilityCount,
                Status == GuidedTrainingStatus.Completed);
        }

        private void AdvancePastSatisfiedPackSteps()
        {
            if (_currentStep == GuidedTrainingStep.AbilityProgression &&
                _clearedPackCount >=
                    GuidedTrainingAbilityCatalog.UnlockingPackCount)
            {
                _currentStep = GuidedTrainingStep.RecoveryDefense;
            }

            if (_currentStep == GuidedTrainingStep.RecoveryDefense &&
                _recoveryOrDefenseResolved)
            {
                _currentStep = GuidedTrainingStep.ControlInterrupt;
            }

            if (_currentStep == GuidedTrainingStep.ControlInterrupt &&
                _controlOrInterruptResolved)
            {
                _currentStep = GuidedTrainingStep.FinalPack;
            }

            if (_currentStep == GuidedTrainingStep.FinalPack &&
                _clearedPackCount >=
                    GuidedTrainingAbilityCatalog.FinalPackNumber)
            {
                _currentStep = GuidedTrainingStep.FlickMastery;
            }
        }

    }
}
