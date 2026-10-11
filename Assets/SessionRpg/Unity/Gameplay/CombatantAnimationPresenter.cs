using System;
using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    [Flags]
    public enum CombatantControlVisualState
    {
        None = 0,
        Fear = 1 << 0,
        Stun = 1 << 1,
        Root = 1 << 2,
        Disorient = 1 << 3,
        Stealth = 1 << 4,
        Horror = 1 << 5,
        Disarm = 1 << 6,
        Pacify = 1 << 7,
    }

    public enum CombatantAnimationPhase
    {
        Idle,
        Locomotion,
        CastAnticipation,
        CastHold,
        ChannelHold,
        Release,
        MeleeWindup,
        MeleeContact,
        MeleeRecovery,
        HitReaction,
        InterruptReaction,
        KnockbackReaction,
        Death,
    }

    public enum CombatantGesture
    {
        Neutral,
        PriestPrayer,
        PriestChannel,
        PriestWard,
        PriestShadow,
        PriestDispel,
        RogueSlash,
        RogueDoubleSlash,
        RogueFinisher,
        RogueShadow,
        MageGather,
        MageLaunch,
        MageNova,
        MageWard,
        MageUtility,
        HunterDraw,
        HunterLoose,
        HunterTrap,
        HunterCommand,
        HunterReady,
        HunterBrace,
        HunterMelee,
        HunterVault,
        HunterFeign,
        RogueWhirlwind,
    }

    [DisallowMultipleComponent]
    public sealed class CombatantAnimationPresenter : MonoBehaviour
    {
        private const float MaximumGroundedHunterRangedTiltDegrees = 15f;

        public const string MoveSpeedParameter = "MoveSpeed";
        public const string BasicParameter = "Basic";
        public const string PowerParameter = "Power";
        public const string UtilityParameter = "Utility";
        public const string GuardParameter = "Guard";
        public const string CastStartParameter = "CastStart";
        public const string CastReleaseParameter = "CastRelease";
        public const string CastCancelParameter = "CastCancel";
        public const string ChannelPulseParameter = "ChannelPulse";
        public const string CastingParameter = "Casting";
        public const string HitParameter = "Hit";
        public const string InterruptParameter = "Interrupt";
        public const string KnockbackParameter = "Knockback";
        public const string DeadParameter = "Dead";

        private static readonly int MoveSpeedHash = Animator.StringToHash(MoveSpeedParameter);
        private static readonly int BasicHash = Animator.StringToHash(BasicParameter);
        private static readonly int PowerHash = Animator.StringToHash(PowerParameter);
        private static readonly int UtilityHash = Animator.StringToHash(UtilityParameter);
        private static readonly int GuardHash = Animator.StringToHash(GuardParameter);
        private static readonly int CastStartHash = Animator.StringToHash(CastStartParameter);
        private static readonly int CastReleaseHash = Animator.StringToHash(CastReleaseParameter);
        private static readonly int CastCancelHash = Animator.StringToHash(CastCancelParameter);
        private static readonly int ChannelPulseHash = Animator.StringToHash(ChannelPulseParameter);
        private static readonly int CastingHash = Animator.StringToHash(CastingParameter);
        private static readonly int HitHash = Animator.StringToHash(HitParameter);
        private static readonly int InterruptHash = Animator.StringToHash(InterruptParameter);
        private static readonly int KnockbackHash = Animator.StringToHash(KnockbackParameter);
        private static readonly int DeadHash = Animator.StringToHash(DeadParameter);

        [SerializeField] private Animator _healerAnimator;
        [SerializeField] private Animator _rogueAnimator;
        [SerializeField] private Animator _mageAnimator;
        [SerializeField] private Animator _hunterAnimator;
        [SerializeField] private Animator _bossAnimator;
        [SerializeField] private GameObject _hunterStaticString;
        [SerializeField] private GameObject _hunterDrawString;
        [SerializeField] private GameObject _hunterNockedArrow;
        [SerializeField, Min(0f)] private float _locomotionDamping = 0.08f;

        [Header("Dynamic pose")]
        [SerializeField, Min(0f)] private float _poseSharpness = 18f;
        [SerializeField, Range(0f, 0.04f)] private float _maximumGroundedLift = 0.018f;
        [SerializeField, Range(0f, 20f)] private float _forwardLeanDegrees = 7f;
        [SerializeField, Range(0f, 20f)] private float _strafeLeanDegrees = 9f;
        [SerializeField, Min(0.05f)] private float _hitReactionSeconds = 0.2f;
        [SerializeField, Min(0.05f)] private float _interruptReactionSeconds = 0.3f;
        [SerializeField, Min(0.05f)] private float _knockbackReactionSeconds = 0.34f;

        [Header("Ability accents")]
        [SerializeField, Min(0.05f)] private float _instantAccentSeconds = 0.34f;
        [SerializeField, Min(0.05f)] private float _finisherAccentSeconds = 0.48f;

        [Header("Hand choreography")]
        [SerializeField, Range(0f, 1f)] private float _spellHandWeight = 0.88f;
        [SerializeField, Range(0f, 1f)] private float _meleeHandWeight = 0.74f;
        [SerializeField, Range(0f, 1f)] private float _elbowHintWeight = 0.62f;

        private Animator _activeAnimator;
        private Transform _activeVisual;
        private Transform _effectOrigin;
        private Vector3 _baseLocalPosition;
        private Quaternion _baseLocalRotation;
        private Vector3 _baseLocalScale = Vector3.one;
        private Vector3 _relationScale = Vector3.one;
        private Vector2 _localMoveDirection;
        private ClassId _classId;
        private AbilityId _castingAbility;
        private AbilityId _accentAbility;
        private CombatantControlVisualState _controlVisualState;
        private float _normalizedMoveSpeed;
        private float _castProgress;
        private float _accentRemaining;
        private float _accentDuration;
        private float _impactRemaining;
        private float _interruptRemaining;
        private float _knockbackRemaining;
        private float _absorbStrength;
        private float _mitigationStrength;
        private Vector2 _impactDirection = Vector2.up;
        private Vector2 _interruptDirection = Vector2.up;
        private Vector2 _knockbackDirection = Vector2.up;
        private bool _channeling;
        private bool _accentIsRelease;
        private bool _wasAlive;
        private bool _initialized;
        private int _actionSequence;

        public Animator ActiveAnimator => _activeAnimator;

        public Transform ActiveVisualRoot => _activeVisual;

        public Transform EffectOrigin => _effectOrigin != null ? _effectOrigin : _activeVisual;

        public Vector3 AuthoredVisualScale => _baseLocalScale;

        // Presentation/debug contract for the visual-only class and relation
        // scale. Dynamic hit/cast squash is deliberately excluded so tests and
        // captures can verify that gameplay roots and colliders remain unscaled.
        public Vector3 ConfiguredVisualScale => Vector3.Scale(
            _baseLocalScale,
            _relationScale);

        public float MaximumGroundedLift => _maximumGroundedLift;

        public AbilityId ActiveAbility => _castingAbility != AbilityId.None
            ? _castingAbility
            : _accentAbility;

        public CombatantGesture CurrentGesture
        {
            get
            {
                if (_castingAbility != AbilityId.None)
                {
                    return ResolveGesture(_castingAbility, false);
                }

                return _accentAbility != AbilityId.None && _accentRemaining > 0f
                    ? ResolveGesture(_accentAbility, _accentIsRelease)
                    : CombatantGesture.Neutral;
            }
        }

        public float CurrentGestureWeight
        {
            get
            {
                if (_castingAbility != AbilityId.None)
                {
                    float castWeight = Mathf.SmoothStep(
                        0.25f,
                        1f,
                        Mathf.Clamp01(_castProgress * 2.6f));
                    if (CurrentGesture == CombatantGesture.HunterDraw)
                    {
                        // Hunter hand targets already encode Ready -> Draw in
                        // cast progress. Keep the IK chain authoritative from
                        // the first cast frame so the Ready sample cannot inherit
                        // an unrelated controller pose and collapse measured hand
                        // travel below the unchanged 0.30 m acceptance gate.
                        return Mathf.Lerp(0.9f, 1f, castWeight);
                    }

                    return castWeight;
                }

                return _accentAbility != AbilityId.None && _accentRemaining > 0f
                    ? BellPulse(GetAccentProgress(), 0.47f, 0.53f)
                    : 0f;
            }
        }

        public bool HasHumanoidHandRig => _initialized &&
            _activeAnimator != null &&
            _activeAnimator.isHuman;

        public float IntendedHandReach
        {
            get
            {
                // Deterministic debug/test description of the authored target pose.
                // This property is intentionally not queried by normal gameplay.
                CombatantGesture gesture = CurrentGesture;
                if (gesture == CombatantGesture.Neutral)
                {
                    return 0f;
                }

                CalculateHandTargets(
                    gesture,
                    CurrentGestureWeight,
                    Time.time,
                    out Vector3 leftHand,
                    out Vector3 rightHand,
                    out _,
                    out _,
                    out _,
                    out _);
                Vector3 origin = transform.position;
                Vector3 forward = transform.forward;
                return (Vector3.Dot(leftHand - origin, forward) +
                    Vector3.Dot(rightHand - origin, forward)) * 0.5f;
            }
        }

        public float IntendedHandSpan
        {
            get
            {
                CombatantGesture gesture = CurrentGesture;
                if (gesture == CombatantGesture.Neutral)
                {
                    return 0f;
                }

                CalculateHandTargets(
                    gesture,
                    CurrentGestureWeight,
                    Time.time,
                    out Vector3 leftHand,
                    out Vector3 rightHand,
                    out _,
                    out _,
                    out _,
                    out _);
                return Vector3.Distance(leftHand, rightHand);
            }
        }

        public CombatantAnimationPhase CurrentPhase
        {
            get
            {
                if (!_wasAlive)
                {
                    return CombatantAnimationPhase.Death;
                }

                if (_interruptRemaining > 0f)
                {
                    return CombatantAnimationPhase.InterruptReaction;
                }

                if (_knockbackRemaining > 0f)
                {
                    return CombatantAnimationPhase.KnockbackReaction;
                }

                if (_impactRemaining > 0f)
                {
                    return CombatantAnimationPhase.HitReaction;
                }

                if (_castingAbility != AbilityId.None)
                {
                    if (_channeling)
                    {
                        return CombatantAnimationPhase.ChannelHold;
                    }

                    return _castProgress < 0.28f
                        ? CombatantAnimationPhase.CastAnticipation
                        : CombatantAnimationPhase.CastHold;
                }

                if (_accentAbility != AbilityId.None && _accentRemaining > 0f)
                {
                    if (!IsMeleeMotion(_accentAbility))
                    {
                        return CombatantAnimationPhase.Release;
                    }

                    float progress = GetAccentProgress();
                    if (progress < 0.2f)
                    {
                        return CombatantAnimationPhase.MeleeWindup;
                    }

                    return progress < 0.58f
                        ? CombatantAnimationPhase.MeleeContact
                        : CombatantAnimationPhase.MeleeRecovery;
                }

                return _normalizedMoveSpeed > 0.05f
                    ? CombatantAnimationPhase.Locomotion
                    : CombatantAnimationPhase.Idle;
            }
        }

        public void Initialize(ClassId classId)
        {
            Initialize(classId, null);
        }

        public void Initialize(ClassId classId, Transform expectedVisualRoot)
        {
            ResetVisualTransform();
            _classId = classId;
            _activeAnimator = ResolveAnimator(classId);
            _activeVisual = _activeAnimator != null ? _activeAnimator.transform : null;
            if (expectedVisualRoot != null && _activeVisual != expectedVisualRoot)
            {
                throw new InvalidOperationException(
                    "Animation presenter model mismatch for " + classId +
                    ": CombatantView selected '" + expectedVisualRoot.name +
                    "' but the presenter selected '" +
                    (_activeVisual != null ? _activeVisual.name : "<none>") + "'.");
            }

            _wasAlive = true;
            _initialized = _activeAnimator != null;
            if (!_initialized)
            {
                return;
            }

            _activeAnimator.applyRootMotion = false;
            _activeAnimator.Rebind();
            _effectOrigin = _activeAnimator.isHuman
                ? _activeAnimator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;
            _baseLocalPosition = _activeVisual.localPosition;
            _baseLocalRotation = _activeVisual.localRotation;
            _baseLocalScale = _activeVisual.localScale;
            _relationScale = Vector3.one;
            _localMoveDirection = Vector2.zero;
            _normalizedMoveSpeed = 0f;
            _castingAbility = AbilityId.None;
            _accentAbility = AbilityId.None;
            _castProgress = 0f;
            _accentRemaining = 0f;
            _accentDuration = 0f;
            _absorbStrength = 0f;
            _mitigationStrength = 0f;
            _channeling = false;
            _accentIsRelease = false;
            _controlVisualState = CombatantControlVisualState.None;
            _impactRemaining = 0f;
            _interruptRemaining = 0f;
            _knockbackRemaining = 0f;
            _impactDirection = Vector2.up;
            _interruptDirection = Vector2.up;
            _knockbackDirection = Vector2.up;
            _actionSequence = 0;
            _activeAnimator.ResetTrigger(BasicHash);
            _activeAnimator.ResetTrigger(PowerHash);
            _activeAnimator.ResetTrigger(UtilityHash);
            _activeAnimator.ResetTrigger(GuardHash);
            _activeAnimator.ResetTrigger(CastStartHash);
            _activeAnimator.ResetTrigger(CastReleaseHash);
            _activeAnimator.ResetTrigger(CastCancelHash);
            _activeAnimator.ResetTrigger(ChannelPulseHash);
            _activeAnimator.ResetTrigger(HitHash);
            _activeAnimator.ResetTrigger(InterruptHash);
            _activeAnimator.ResetTrigger(KnockbackHash);
            _activeAnimator.SetBool(DeadHash, false);
            _activeAnimator.SetBool(CastingHash, false);
            _activeAnimator.SetFloat(MoveSpeedHash, 0f);
            _activeAnimator.Update(0f);
            PresentHunterWeapon(CombatantGesture.Neutral);
        }

        internal void ApplyAnimatorIk(Animator sourceAnimator, int layerIndex)
        {
            if (layerIndex != 0 ||
                !_initialized ||
                !_wasAlive ||
                _activeAnimator == null ||
                sourceAnimator != _activeAnimator ||
                !_activeAnimator.isHuman)
            {
                return;
            }

            CombatantGesture gesture = CurrentGesture;
            float gestureWeight = CurrentGestureWeight;
            if (gesture == CombatantGesture.Neutral || gestureWeight <= 0.001f)
            {
                ClearHandIk();
                return;
            }

            CalculateHandTargets(
                gesture,
                gestureWeight,
                Time.time,
                out Vector3 leftHand,
                out Vector3 rightHand,
                out Vector3 leftElbow,
                out Vector3 rightElbow,
                out float leftWeight,
                out float rightWeight);
            ApplyHandIk(
                AvatarIKGoal.LeftHand,
                AvatarIKHint.LeftElbow,
                leftHand,
                leftElbow,
                leftWeight);
            ApplyHandIk(
                AvatarIKGoal.RightHand,
                AvatarIKHint.RightElbow,
                rightHand,
                rightElbow,
                rightWeight);
        }

        public void Present(float normalizedMoveSpeed, bool alive)
        {
            Vector2 movement = normalizedMoveSpeed > 0.001f ? Vector2.up : Vector2.zero;
            Present(normalizedMoveSpeed, movement, alive);
        }

        public void Present(float normalizedMoveSpeed, Vector2 localMoveDirection, bool alive)
        {
            if (!_initialized)
            {
                return;
            }

            _normalizedMoveSpeed = Mathf.Clamp01(normalizedMoveSpeed);
            _localMoveDirection = localMoveDirection.sqrMagnitude > 1f
                ? localMoveDirection.normalized
                : localMoveDirection;

            float dampingDelta = Mathf.Max(Time.deltaTime, 0.0001f);
            bool movementLocked = (_controlVisualState &
                (CombatantControlVisualState.Stun |
                 CombatantControlVisualState.Root |
                 CombatantControlVisualState.Disorient)) != 0;
            _activeAnimator.SetFloat(
                MoveSpeedHash,
                movementLocked ? 0f : _normalizedMoveSpeed,
                _locomotionDamping,
                dampingDelta);

            if (_wasAlive != alive)
            {
                _activeAnimator.SetBool(DeadHash, !alive);
                _wasAlive = alive;
                if (!alive)
                {
                    _castingAbility = AbilityId.None;
                    _accentAbility = AbilityId.None;
                    _accentRemaining = 0f;
                    _absorbStrength = 0f;
                    _mitigationStrength = 0f;
                    _controlVisualState = CombatantControlVisualState.None;
                    _impactRemaining = 0f;
                    _interruptRemaining = 0f;
                    _knockbackRemaining = 0f;
                    _activeAnimator.SetBool(CastingHash, false);
                    PresentHunterWeapon(CombatantGesture.Neutral);
                    ResetVisualTransform();
                }
            }
        }

        public void ConfigureRelation(bool isHuman, bool isAlly, bool isBoss)
        {
            if (isBoss)
            {
                _relationScale = Vector3.one;
            }
            else if (isHuman)
            {
                _relationScale = new Vector3(1.08f, 1.08f, 1.08f);
            }
            else if (isAlly)
            {
                _relationScale = Vector3.one;
            }
            else
            {
                _relationScale = new Vector3(1.04f, 1.07f, 1.04f);
            }
        }

        public void SetCastState(AbilityId ability, float normalizedProgress, bool channeling)
        {
            if (ability != AbilityId.None &&
                _castingAbility == AbilityId.None &&
                IsTimedActivation(ability))
            {
                _activeAnimator.SetBool(CastingHash, true);
                if (!UsesGroundedHunterRangedPose(ability))
                {
                    _activeAnimator.SetTrigger(CastStartHash);
                }
            }

            _castingAbility = ability;
            _castProgress = Mathf.Clamp01(normalizedProgress);
            _channeling = channeling && ability != AbilityId.None;
            if (ability == AbilityId.None)
            {
                _activeAnimator.SetBool(CastingHash, false);
            }

            PresentHunterWeapon(CurrentGesture);
        }

        public void ClearCastState()
        {
            _castingAbility = AbilityId.None;
            _castProgress = 0f;
            _channeling = false;
            if (_initialized)
            {
                _activeAnimator.SetBool(CastingHash, false);
            }

            PresentHunterWeapon(CurrentGesture);
        }

        public void SetControlVisualState(CombatantControlVisualState state)
        {
            _controlVisualState = state;
        }

        public void SetDefenseVisualState(float absorbStrength, float mitigationStrength)
        {
            _absorbStrength = Mathf.Clamp01(absorbStrength);
            _mitigationStrength = Mathf.Clamp01(mitigationStrength);
        }

        public void PlayCast()
        {
            PlayCast(AbilityId.None);
        }

        public void PlayCast(AbilityId ability)
        {
            if (!_initialized || !_wasAlive)
            {
                return;
            }

            if (IsTimedActivation(ability))
            {
                _castingAbility = ability;
                _castProgress = 0f;
                _channeling = IsChannelActivation(ability);
                _activeAnimator.SetBool(CastingHash, true);
                _activeAnimator.ResetTrigger(CastReleaseHash);
                _activeAnimator.ResetTrigger(CastCancelHash);
                if (!UsesGroundedHunterRangedPose(ability))
                {
                    _activeAnimator.SetTrigger(CastStartHash);
                }

                BeginAbilityAccent(ability, false);
                PresentHunterWeapon(CurrentGesture);
                return;
            }

            if (!UsesGroundedHunterRangedPose(ability))
            {
                switch (ResolveCastAction(ability))
                {
                    case CastAction.MovementOnly:
                        break;
                    case CastAction.Power:
                        _activeAnimator.SetTrigger(PowerHash);
                        break;
                    case CastAction.Utility:
                        _activeAnimator.SetTrigger(UtilityHash);
                        break;
                    case CastAction.Guard:
                        _activeAnimator.SetTrigger(GuardHash);
                        break;
                    default:
                        _activeAnimator.SetTrigger(BasicHash);
                        break;
                }
            }

            BeginAbilityAccent(ability, true);
            PresentHunterWeapon(CurrentGesture);
        }

        public void PlayCastRelease(AbilityId ability)
        {
            if (!_initialized || !_wasAlive)
            {
                return;
            }

            _activeAnimator.SetBool(CastingHash, false);
            bool channel = IsChannelActivation(ability);
            if (channel)
            {
                _activeAnimator.SetTrigger(CastCancelHash);
            }
            else if (IsTimedActivation(ability) &&
                !UsesGroundedHunterRangedPose(ability))
            {
                _activeAnimator.SetTrigger(CastReleaseHash);
            }

            _castingAbility = AbilityId.None;
            _castProgress = 0f;
            _channeling = false;
            if (!channel)
            {
                BeginAbilityAccent(ability, true);
            }

            PresentHunterWeapon(CurrentGesture);
        }

        public void PlayCastCanceled(bool hardInterrupted = false)
        {
            PlayCastCanceled(hardInterrupted, transform.position - transform.forward);
        }

        public void PlayCastCanceled(bool hardInterrupted, Vector3 sourcePosition)
        {
            if (!_initialized || !_wasAlive)
            {
                return;
            }

            AbilityId canceledAbility = _castingAbility;
            bool hadCast = canceledAbility != AbilityId.None;
            _activeAnimator.SetBool(CastingHash, false);
            if (hadCast &&
                (hardInterrupted || !UsesGroundedHunterRangedPose(canceledAbility)))
            {
                _activeAnimator.SetTrigger(
                    hardInterrupted ? InterruptHash : CastCancelHash);
                if (hardInterrupted)
                {
                    PlayInterruptReaction(sourcePosition);
                }
            }

            _castingAbility = AbilityId.None;
            _castProgress = 0f;
            _channeling = false;
            _accentAbility = AbilityId.None;
            _accentRemaining = 0f;
            PresentHunterWeapon(CombatantGesture.Neutral);
        }

        public void PlayChannelPulse(AbilityId ability)
        {
            if (!_initialized || !_wasAlive || !IsChannelActivation(ability))
            {
                return;
            }

            _activeAnimator.SetTrigger(ChannelPulseHash);
            BeginAbilityAccent(ability, true);
        }

        public void PlayHit(Vector3 sourcePosition)
        {
            if (_initialized && _wasAlive)
            {
                _activeAnimator.SetTrigger(HitHash);
                _impactDirection = ResolveLocalImpactDirection(sourcePosition);
                _impactRemaining = Mathf.Max(_impactRemaining, _hitReactionSeconds);
            }
        }

        public void PlayHit()
        {
            PlayHit(transform.position - transform.forward);
        }

        public void PlayImpactPulse()
        {
            if (_initialized && _wasAlive)
            {
                _impactDirection = Vector2.up;
                _impactRemaining = Mathf.Max(_impactRemaining, _hitReactionSeconds);
            }
        }

        public void PlayImpactPulse(Vector3 sourcePosition)
        {
            if (_initialized && _wasAlive)
            {
                _impactDirection = ResolveLocalImpactDirection(sourcePosition);
                _impactRemaining = Mathf.Max(_impactRemaining, _hitReactionSeconds);
            }
        }

        public void PlayInterruptReaction(Vector3 sourcePosition)
        {
            if (!_initialized || !_wasAlive)
            {
                return;
            }

            _interruptDirection = ResolveLocalImpactDirection(sourcePosition);
            _interruptRemaining = Mathf.Max(
                _interruptRemaining,
                _interruptReactionSeconds);
        }

        public void PlayKnockback(Vector3 direction)
        {
            if (_initialized && _wasAlive)
            {
                _activeAnimator.SetTrigger(KnockbackHash);
                _knockbackDirection = ResolveLocalDirection(direction);
                _knockbackRemaining = Mathf.Max(
                    _knockbackRemaining,
                    _knockbackReactionSeconds);
            }
        }

        public void PlayKnockback()
        {
            PlayKnockback(transform.forward);
        }

        private void LateUpdate()
        {
            if (!_initialized || !_wasAlive || _activeVisual == null)
            {
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float animationTime = Time.time;
            _accentRemaining = Mathf.Max(0f, _accentRemaining - deltaTime);
            _impactRemaining = Mathf.Max(0f, _impactRemaining - deltaTime);
            _interruptRemaining = Mathf.Max(0f, _interruptRemaining - deltaTime);
            _knockbackRemaining = Mathf.Max(0f, _knockbackRemaining - deltaTime);
            if (_accentRemaining <= 0f)
            {
                _accentAbility = AbilityId.None;
            }

            PresentHunterWeapon(CurrentGesture);

            float pitch = _localMoveDirection.y * _normalizedMoveSpeed * _forwardLeanDegrees;
            float roll = -_localMoveDirection.x * _normalizedMoveSpeed * _strafeLeanDegrees;
            float yaw = 0f;
            float heightOffset = 0f;
            Vector3 localOffset = Vector3.zero;
            Vector3 stateScale = Vector3.one;

            if (_castingAbility != AbilityId.None)
            {
                ApplyCastingPose(
                    _castingAbility,
                    _castProgress,
                    _channeling,
                    animationTime,
                    ref pitch,
                    ref yaw,
                    ref roll,
                    ref heightOffset,
                    ref localOffset,
                    ref stateScale);
            }

            if (_accentAbility != AbilityId.None && _accentRemaining > 0f)
            {
                float accentProgress = 1f -
                    (_accentRemaining / Mathf.Max(0.05f, _accentDuration));
                ApplyAbilityAccent(
                    _accentAbility,
                    accentProgress,
                    _accentIsRelease,
                    ref pitch,
                    ref yaw,
                    ref roll,
                    ref heightOffset,
                    ref localOffset,
                    ref stateScale);
            }

            if (_absorbStrength > 0.001f)
            {
                float wardPulse = 0.82f + (Mathf.Sin(animationTime * 7f) * 0.18f);
                pitch -= 2f * _absorbStrength;
                heightOffset += 0.02f * _absorbStrength * wardPulse;
                stateScale += new Vector3(0.018f, 0.035f, 0.018f) *
                    _absorbStrength * wardPulse;
            }

            if (_mitigationStrength > 0.001f)
            {
                pitch += 4f * _mitigationStrength;
                heightOffset -= 0.025f * _mitigationStrength;
                stateScale = Vector3.Scale(
                    stateScale,
                    new Vector3(
                        1f + (0.035f * _mitigationStrength),
                        1f - (0.025f * _mitigationStrength),
                        1f + (0.035f * _mitigationStrength)));
            }

            if ((_controlVisualState & CombatantControlVisualState.Fear) != 0)
            {
                float fearWave = Mathf.Sin(animationTime * 18f);
                yaw += fearWave * 15f;
                roll += fearWave * 7f;
                pitch -= 8f;
            }

            if ((_controlVisualState & CombatantControlVisualState.Horror) != 0)
            {
                float horrorJolt = Mathf.Sin(animationTime * 26f);
                yaw += horrorJolt * 7f;
                roll -= horrorJolt * 9f;
                pitch -= 13f;
                heightOffset -= 0.035f;
            }

            if ((_controlVisualState & CombatantControlVisualState.Stun) != 0)
            {
                float stunJitter = Mathf.Sin(animationTime * 55f);
                yaw += stunJitter * 3f;
                roll += stunJitter * 2f;
                stateScale = Vector3.Scale(stateScale, new Vector3(1.06f, 0.92f, 1.06f));
            }

            if ((_controlVisualState & CombatantControlVisualState.Root) != 0)
            {
                pitch *= 0.25f;
                roll *= 0.25f;
                heightOffset -= 0.045f;
                stateScale = Vector3.Scale(stateScale, new Vector3(1.04f, 0.97f, 1.04f));
            }

            if ((_controlVisualState & CombatantControlVisualState.Disorient) != 0)
            {
                float disorientWave = Mathf.Sin(animationTime * 8f);
                yaw += disorientWave * 12f;
                roll += Mathf.Cos(animationTime * 6f) * 5f;
            }

            if ((_controlVisualState & CombatantControlVisualState.Disarm) != 0)
            {
                float disarmPulse = 0.5f + (Mathf.Sin(animationTime * 9f) * 0.5f);
                pitch += 6f + (disarmPulse * 3f);
                roll += 3f;
                stateScale = Vector3.Scale(
                    stateScale,
                    new Vector3(0.96f, 1.02f, 0.96f));
            }

            if ((_controlVisualState & CombatantControlVisualState.Pacify) != 0)
            {
                pitch += 4f;
                heightOffset -= 0.025f;
                stateScale = Vector3.Scale(
                    stateScale,
                    new Vector3(0.97f, 0.98f, 0.97f));
            }

            if ((_controlVisualState & CombatantControlVisualState.Stealth) != 0)
            {
                pitch += 9f;
                heightOffset -= 0.12f;
                stateScale = Vector3.Scale(stateScale, new Vector3(1f, 0.84f, 1f));
            }

            if (_impactRemaining > 0f)
            {
                float impactProgress = 1f -
                    (_impactRemaining / Mathf.Max(0.05f, _hitReactionSeconds));
                float impactArc = Mathf.Sin(
                    (0.5f + (impactProgress * 1.25f)) * Mathf.PI) *
                    (1f - impactProgress);
                if (impactArc < 0f)
                {
                    // A small opposite overshoot lets the body settle after the
                    // initial recoil without creating a second apparent hit.
                    impactArc *= 0.28f;
                }

                pitch -= _impactDirection.y * 12f * impactArc;
                roll += _impactDirection.x * 14f * impactArc;
                localOffset.x += _impactDirection.x * 0.045f * impactArc;
                localOffset.z -= _impactDirection.y * 0.055f * impactArc;
                stateScale += new Vector3(0.045f, -0.035f, 0.045f) * impactArc;
            }

            if (_interruptRemaining > 0f)
            {
                float interrupt = _interruptRemaining /
                    Mathf.Max(0.05f, _interruptReactionSeconds);
                float breakArc = Mathf.Sin(interrupt * Mathf.PI);
                pitch += 18f * breakArc;
                yaw += _interruptDirection.x * 13f * breakArc;
                roll += _interruptDirection.x * 9f * breakArc;
                heightOffset -= 0.055f * breakArc;
                localOffset.z -= 0.08f * breakArc;
                stateScale = Vector3.Scale(
                    stateScale,
                    new Vector3(
                        1f + (0.06f * breakArc),
                        1f - (0.09f * breakArc),
                        1f + (0.06f * breakArc)));
            }

            if (_knockbackRemaining > 0f)
            {
                float knockback = _knockbackRemaining /
                    Mathf.Max(0.05f, _knockbackReactionSeconds);
                float knockbackArc = Mathf.Sin(knockback * Mathf.PI);
                pitch -= _knockbackDirection.y * 23f * knockbackArc;
                roll += _knockbackDirection.x * 18f * knockbackArc;
                localOffset.x -= _knockbackDirection.x * 0.08f * knockbackArc;
                localOffset.z -= _knockbackDirection.y * 0.11f * knockbackArc;
            }

            bool groundedHunterRangedPose =
                _impactRemaining <= 0f &&
                _interruptRemaining <= 0f &&
                _knockbackRemaining <= 0f &&
                UsesGroundedHunterRangedPose(ActiveAbility);
            if (groundedHunterRangedPose)
            {
                // Hunter shots are presented by the arm IK, bow, and release
                // offset. Rotating the full Humanoid root makes the fighter read
                // as falling from the elevated strategy camera, especially when
                // a generic full-body spell clip is also active. Keep the lower
                // body planted and reserve larger tilts for explicit hit and
                // displacement reactions.
                ClampPlanarTilt(
                    ref pitch,
                    ref roll,
                    MaximumGroundedHunterRangedTiltDegrees);
                heightOffset = Mathf.Min(heightOffset, 0f);
            }

            // Character animation never owns altitude. Anticipations may compress
            // the stance, but releases cannot lift the visual root enough to make
            // the feet read as airborne from the fixed strategy camera.
            heightOffset = Mathf.Clamp(
                heightOffset,
                -0.14f,
                Mathf.Clamp(_maximumGroundedLift, 0f, 0.04f));

            float poseBlend = _poseSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-_poseSharpness * deltaTime);
            Quaternion pose = _baseLocalRotation * Quaternion.Euler(pitch, yaw, roll);
            Vector3 position = _baseLocalPosition +
                (Vector3.up * heightOffset) +
                (_baseLocalRotation * localOffset);
            Vector3 scale = Vector3.Scale(
                Vector3.Scale(_baseLocalScale, _relationScale),
                stateScale);
            _activeVisual.localRotation = Quaternion.Slerp(
                _activeVisual.localRotation,
                pose,
                poseBlend);
            _activeVisual.localPosition = Vector3.Lerp(
                _activeVisual.localPosition,
                position,
                poseBlend);
            _activeVisual.localScale = Vector3.Lerp(
                _activeVisual.localScale,
                scale,
                poseBlend);
        }

        private static void ClampPlanarTilt(
            ref float pitch,
            ref float roll,
            float maximumDegrees)
        {
            float tiltMagnitude = Mathf.Sqrt((pitch * pitch) + (roll * roll));
            if (tiltMagnitude <= maximumDegrees || tiltMagnitude <= 0.001f)
            {
                return;
            }

            float scale = maximumDegrees / tiltMagnitude;
            pitch *= scale;
            roll *= scale;
        }

        private void BeginAbilityAccent(AbilityId ability, bool isRelease)
        {
            if (ability == AbilityId.None)
            {
                return;
            }

            _accentAbility = ability;
            _accentIsRelease = isRelease;
            _accentDuration = IsFinisher(ability)
                ? _finisherAccentSeconds
                : _instantAccentSeconds;
            _accentRemaining = _accentDuration;
            _actionSequence++;
        }

        private float GetAccentProgress()
        {
            return 1f - (_accentRemaining / Mathf.Max(0.05f, _accentDuration));
        }

        private void ApplyCastingPose(
            AbilityId ability,
            float progress,
            bool channeling,
            float animationTime,
            ref float pitch,
            ref float yaw,
            ref float roll,
            ref float heightOffset,
            ref Vector3 localOffset,
            ref Vector3 stateScale)
        {
            float castArc = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
            float heldPose = channeling
                ? 0.78f + (Mathf.Sin(animationTime * 11f) * 0.12f)
                : Mathf.SmoothStep(0.2f, 1f, castArc);

            switch (ResolveMotionStyle(ability))
            {
                case AbilityMotionStyle.PriestChannel:
                {
                    float pulse = 0.5f + (Mathf.Sin(animationTime * 19f) * 0.5f);
                    pitch -= 10f * heldPose;
                    yaw += Mathf.Sin(animationTime * 6f) * 3f * heldPose;
                    heightOffset += (0.055f + (0.018f * pulse)) * heldPose;
                    stateScale += new Vector3(0.035f, 0.065f, 0.035f) * heldPose;
                    break;
                }
                case AbilityMotionStyle.PriestHeal:
                    pitch -= 9f * heldPose;
                    roll += Mathf.Sin(progress * Mathf.PI * 2f) * 2.5f;
                    heightOffset += 0.045f * heldPose;
                    stateScale += new Vector3(0.025f, 0.055f, 0.025f) * heldPose;
                    break;
                case AbilityMotionStyle.MageHardCast:
                {
                    float gather = Mathf.SmoothStep(0f, 1f, progress);
                    float release = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.78f, 1f, progress));
                    pitch += (9f * heldPose) - (20f * release);
                    roll += Mathf.Sin(progress * Mathf.PI) * 5f;
                    heightOffset -= 0.045f * heldPose * (1f - release);
                    localOffset.z += (0.11f * release) - (0.035f * gather);
                    stateScale = Vector3.Scale(
                        stateScale,
                        new Vector3(
                            1f + (0.035f * gather),
                            1f - (0.045f * heldPose) + (0.07f * release),
                            1f + (0.035f * gather)));
                    break;
                }
                case AbilityMotionStyle.HunterDraw:
                {
                    float draw = Mathf.SmoothStep(0f, 1f, progress);
                    float tension = 0.86f + (Mathf.Sin(animationTime * 8f) * 0.04f);
                    pitch += (7f - (13f * draw)) * heldPose;
                    yaw -= 16f * draw * heldPose;
                    roll += 5f * draw * heldPose;
                    heightOffset -= 0.055f * draw * heldPose;
                    localOffset.z -= 0.045f * draw * heldPose;
                    stateScale = Vector3.Scale(
                        stateScale,
                        new Vector3(
                            1f + (0.045f * draw * tension),
                            1f - (0.045f * draw),
                            1f + (0.025f * draw)));
                    break;
                }
                default:
                    pitch -= 7f * heldPose;
                    heightOffset += 0.035f * heldPose;
                    stateScale += new Vector3(0.025f, 0.045f, 0.025f) * heldPose;
                    break;
            }
        }

        private void ApplyAbilityAccent(
            AbilityId ability,
            float normalizedProgress,
            bool isRelease,
            ref float pitch,
            ref float yaw,
            ref float roll,
            ref float heightOffset,
            ref Vector3 localOffset,
            ref Vector3 stateScale)
        {
            float progress = Mathf.Clamp01(normalizedProgress);
            float actionArc = Mathf.Sin(progress * Mathf.PI);
            float snap = Mathf.Sin(Mathf.Clamp01(progress * 1.45f) * Mathf.PI);
            float side = (_actionSequence & 1) == 0 ? -1f : 1f;

            switch (ResolveMotionStyle(ability))
            {
                case AbilityMotionStyle.PriestChannel:
                    pitch -= 7f * actionArc;
                    heightOffset += 0.055f * actionArc;
                    stateScale += new Vector3(0.035f, 0.06f, 0.035f) * actionArc;
                    break;
                case AbilityMotionStyle.PriestHeal:
                    pitch -= 11f * actionArc;
                    heightOffset += 0.07f * actionArc;
                    stateScale += new Vector3(0.025f, 0.07f, 0.025f) * actionArc;
                    break;
                case AbilityMotionStyle.PriestWard:
                    pitch -= 6f * actionArc;
                    heightOffset += 0.085f * actionArc;
                    stateScale += new Vector3(0.075f, 0.035f, 0.075f) * actionArc;
                    break;
                case AbilityMotionStyle.PriestShadow:
                    pitch += (isRelease ? -12f : 9f) * actionArc;
                    yaw += side * 8f * actionArc;
                    localOffset.z += 0.075f * snap;
                    stateScale += new Vector3(0.045f, -0.035f, 0.045f) * actionArc;
                    break;
                case AbilityMotionStyle.PriestDispel:
                    yaw += side * 17f * actionArc;
                    roll -= side * 8f * actionArc;
                    heightOffset += 0.035f * actionArc;
                    break;
                case AbilityMotionStyle.RogueBuilder:
                {
                    float anticipation = 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0f, 0.2f, progress));
                    float contact = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.18f, 0.42f, progress));
                    float recovery = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.58f, 1f, progress));
                    float lunge = contact * (1f - recovery);
                    pitch += (12f * anticipation) - (18f * lunge);
                    yaw += side * ((-13f * anticipation) + (29f * lunge));
                    roll -= side * 17f * lunge;
                    localOffset.z += (0.035f * anticipation) + (0.22f * lunge);
                    stateScale = Vector3.Scale(
                        stateScale,
                        new Vector3(1f + (0.04f * lunge), 1f - (0.05f * lunge), 1f));
                    break;
                }
                case AbilityMotionStyle.RogueLowStrike:
                {
                    float anticipation = 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0f, 0.18f, progress));
                    float contact = BellPulse(progress, 0.36f, 0.2f);
                    float recovery = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.55f, 1f, progress));
                    float drive = contact * (1f - recovery);
                    pitch += (18f * anticipation) - (31f * drive);
                    yaw += side * ((-16f * anticipation) + (24f * drive));
                    roll -= side * 13f * drive;
                    heightOffset -= (0.07f * anticipation) + (0.12f * drive);
                    localOffset.z += (0.04f * anticipation) + (0.27f * drive);
                    stateScale = Vector3.Scale(
                        stateScale,
                        new Vector3(
                            1f + (0.055f * drive),
                            1f - (0.1f * anticipation) - (0.08f * drive),
                            1f + (0.035f * drive)));
                    break;
                }
                case AbilityMotionStyle.RogueDoubleStrike:
                {
                    float anticipation = 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0f, 0.18f, progress));
                    float firstContact = BellPulse(progress, 0.32f, 0.17f);
                    float secondContact = BellPulse(progress, 0.55f, 0.17f);
                    float contact = Mathf.Max(firstContact, secondContact);
                    pitch += (13f * anticipation) - (18f * contact);
                    yaw += side * ((-15f * anticipation) +
                        (24f * firstContact) -
                        (27f * secondContact));
                    roll -= side * ((12f * firstContact) - (14f * secondContact));
                    localOffset.z += (0.04f * anticipation) + (0.24f * contact);
                    stateScale += new Vector3(0.065f, -0.05f, 0.065f) * contact;
                    break;
                }
                case AbilityMotionStyle.RogueWhirlwind:
                {
                    float windup = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0f, 0.18f, progress));
                    float spin = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.12f, 0.82f, progress));
                    float sweep = BellPulse(progress, 0.5f, 0.43f);
                    pitch += (13f * (1f - windup)) - (12f * sweep);
                    yaw += side * 360f * spin;
                    roll -= side * 9f * sweep;
                    heightOffset -= 0.09f * sweep;
                    stateScale = Vector3.Scale(
                        stateScale,
                        new Vector3(
                            1f + (0.11f * sweep),
                            1f - (0.1f * sweep),
                            1f + (0.11f * sweep)));
                    break;
                }
                case AbilityMotionStyle.RogueFinisher:
                {
                    float anticipation = 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0f, 0.24f, progress));
                    float contact = BellPulse(progress, 0.43f, 0.24f);
                    float recovery = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.62f, 1f, progress));
                    pitch += (18f * anticipation) - (27f * contact);
                    yaw += side * ((-22f * anticipation) + (34f * contact));
                    roll -= side * 20f * contact;
                    localOffset.z += (0.055f * anticipation) + (0.3f * contact);
                    heightOffset += 0.055f * contact * (1f - recovery);
                    stateScale += new Vector3(0.075f, -0.065f, 0.075f) * contact;
                    break;
                }
                case AbilityMotionStyle.RogueShadow:
                    pitch += 12f * actionArc;
                    yaw += side * 34f * actionArc;
                    heightOffset -= 0.08f * actionArc;
                    stateScale = Vector3.Scale(
                        stateScale,
                        new Vector3(1f, 1f - (0.12f * actionArc), 1f));
                    break;
                case AbilityMotionStyle.MageHardCast:
                    pitch -= 14f * snap;
                    localOffset.z += 0.12f * snap;
                    heightOffset += 0.035f * actionArc;
                    stateScale += new Vector3(0.04f, 0.055f, 0.04f) * actionArc;
                    break;
                case AbilityMotionStyle.MageBolt:
                    pitch -= 15f * snap;
                    roll += side * 7f * actionArc;
                    localOffset.z += 0.1f * snap;
                    stateScale += new Vector3(0.035f, 0.045f, 0.035f) * actionArc;
                    break;
                case AbilityMotionStyle.MageRadial:
                    pitch += 8f * actionArc;
                    heightOffset -= 0.04f * actionArc;
                    stateScale += new Vector3(0.09f, -0.045f, 0.09f) * actionArc;
                    break;
                case AbilityMotionStyle.MageWard:
                    pitch -= 5f * actionArc;
                    heightOffset += 0.075f * actionArc;
                    stateScale += new Vector3(0.07f, 0.055f, 0.07f) * actionArc;
                    break;
                case AbilityMotionStyle.MageUtility:
                    yaw += side * 23f * actionArc;
                    roll -= side * 8f * actionArc;
                    heightOffset += 0.035f * actionArc;
                    break;
                case AbilityMotionStyle.HunterDraw:
                case AbilityMotionStyle.HunterShot:
                {
                    float loose = BellPulse(progress, 0.3f, 0.25f);
                    float settle = Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0.52f, 1f, progress));
                    pitch += (7f * actionArc) - (14f * loose);
                    yaw -= (18f * actionArc) - (7f * settle);
                    roll += 5f * actionArc;
                    localOffset.z += 0.17f * loose;
                    heightOffset -= 0.035f * actionArc;
                    stateScale += new Vector3(0.055f, -0.035f, 0.025f) * loose;
                    break;
                }
                case AbilityMotionStyle.HunterHeavyShot:
                {
                    float brace = 1f - Mathf.SmoothStep(
                        0f,
                        1f,
                        Mathf.InverseLerp(0f, 0.2f, progress));
                    float loose = BellPulse(progress, 0.38f, 0.28f);
                    pitch += (12f * brace) - (23f * loose);
                    yaw -= (24f * brace) - (11f * loose);
                    roll += 8f * actionArc;
                    localOffset.z += (0.035f * brace) + (0.25f * loose);
                    heightOffset -= 0.055f * brace;
                    stateScale += new Vector3(0.075f, -0.055f, 0.045f) * loose;
                    break;
                }
                case AbilityMotionStyle.HunterTrap:
                {
                    float plant = BellPulse(progress, 0.43f, 0.3f);
                    pitch += 34f * actionArc;
                    yaw += side * 14f * actionArc;
                    roll -= side * 7f * actionArc;
                    heightOffset -= 0.13f * actionArc;
                    localOffset.z += 0.09f * plant;
                    stateScale = Vector3.Scale(
                        stateScale,
                        new Vector3(
                            1f + (0.045f * plant),
                            1f - (0.15f * actionArc),
                            1f + (0.045f * plant)));
                    break;
                }
                case AbilityMotionStyle.HunterCommand:
                    pitch -= 5f * actionArc;
                    yaw += side * 9f * actionArc;
                    roll -= side * 5f * actionArc;
                    heightOffset += 0.045f * actionArc;
                    stateScale += new Vector3(0.025f, 0.055f, 0.025f) * actionArc;
                    break;
                case AbilityMotionStyle.HunterPrepare:
                    pitch -= 8f * actionArc;
                    yaw -= 12f * actionArc;
                    heightOffset += 0.035f * actionArc;
                    stateScale += new Vector3(0.055f, 0.025f, 0.035f) * actionArc;
                    break;
                case AbilityMotionStyle.HunterGuard:
                    pitch += 7f * actionArc;
                    heightOffset -= 0.055f * actionArc;
                    localOffset.z -= 0.06f * actionArc;
                    stateScale = Vector3.Scale(
                        stateScale,
                        new Vector3(
                            1f + (0.09f * actionArc),
                            1f - (0.075f * actionArc),
                            1f + (0.08f * actionArc)));
                    break;
                case AbilityMotionStyle.HunterMelee:
                {
                    float clip = BellPulse(progress, 0.38f, 0.25f);
                    pitch += (10f * actionArc) - (19f * clip);
                    yaw += side * ((-19f * actionArc) + (38f * clip));
                    roll -= side * 19f * clip;
                    localOffset.z += 0.24f * clip;
                    heightOffset -= 0.045f * actionArc;
                    break;
                }
                case AbilityMotionStyle.HunterVault:
                {
                    float recoil = BellPulse(progress, 0.38f, 0.34f);
                    pitch += 26f * recoil;
                    roll += side * 6f * recoil;
                    heightOffset += 0.065f * recoil;
                    localOffset.z -= 0.2f * recoil;
                    stateScale = Vector3.Scale(
                        stateScale,
                        new Vector3(
                            1f + (0.04f * recoil),
                            1f - (0.06f * recoil),
                            1f + (0.04f * recoil)));
                    break;
                }
                case AbilityMotionStyle.HunterFeign:
                    pitch += 46f * actionArc;
                    yaw += side * 8f * actionArc;
                    roll += side * 15f * actionArc;
                    heightOffset -= 0.14f * actionArc;
                    localOffset.z -= 0.08f * actionArc;
                    stateScale = Vector3.Scale(
                        stateScale,
                        new Vector3(
                            1f + (0.08f * actionArc),
                            1f - (0.24f * actionArc),
                            1f + (0.08f * actionArc)));
                    break;
                default:
                    pitch -= 8f * actionArc;
                    heightOffset += 0.03f * actionArc;
                    break;
            }
        }

        private void CalculateHandTargets(
            CombatantGesture gesture,
            float gestureWeight,
            float animationTime,
            out Vector3 leftHand,
            out Vector3 rightHand,
            out Vector3 leftElbow,
            out Vector3 rightElbow,
            out float leftWeight,
            out float rightWeight)
        {
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            Vector3 up = transform.up;
            Vector3 chest = origin + (up * 1.28f);
            float progress = _castingAbility != AbilityId.None
                ? Mathf.Clamp01(_castProgress)
                : GetAccentProgress();
            float side = (_actionSequence & 1) == 0 ? -1f : 1f;
            float baseWeight = UsesMeleeHandWeight(gesture)
                ? _meleeHandWeight
                : _spellHandWeight;

            leftWeight = baseWeight * gestureWeight;
            rightWeight = baseWeight * gestureWeight;
            leftHand = chest - (right * 0.28f) + (forward * 0.24f);
            rightHand = chest + (right * 0.28f) + (forward * 0.24f);

            switch (gesture)
            {
                case CombatantGesture.PriestPrayer:
                {
                    float gather = Mathf.SmoothStep(0f, 1f, progress);
                    float separation = Mathf.Lerp(0.31f, 0.13f, gather);
                    leftHand = chest - (right * separation) +
                        (forward * Mathf.Lerp(0.18f, 0.42f, gather)) +
                        (up * Mathf.Lerp(0.02f, 0.28f, gather));
                    rightHand = chest + (right * separation) +
                        (forward * Mathf.Lerp(0.18f, 0.42f, gather)) +
                        (up * Mathf.Lerp(0.02f, 0.28f, gather));
                    break;
                }
                case CombatantGesture.PriestChannel:
                {
                    float pulse = 0.5f + (Mathf.Sin(animationTime * 13f) * 0.5f);
                    leftHand = chest - (right * 0.24f) +
                        (forward * Mathf.Lerp(0.34f, 0.62f, pulse)) +
                        (up * Mathf.Lerp(0.2f, 0.08f, pulse));
                    rightHand = chest + (right * 0.24f) +
                        (forward * Mathf.Lerp(0.62f, 0.34f, pulse)) +
                        (up * Mathf.Lerp(0.08f, 0.2f, pulse));
                    break;
                }
                case CombatantGesture.PriestWard:
                    leftHand = chest - (right * 0.58f) + (forward * 0.22f) + (up * 0.24f);
                    rightHand = chest + (right * 0.58f) + (forward * 0.22f) + (up * 0.24f);
                    break;
                case CombatantGesture.PriestShadow:
                    leftHand = chest - (right * 0.2f) + (forward * 0.58f) - (up * 0.1f);
                    rightHand = chest + (right * 0.2f) + (forward * 0.68f) + (up * 0.08f);
                    break;
                case CombatantGesture.PriestDispel:
                {
                    float sweep = Mathf.Sin(progress * Mathf.PI);
                    leftHand = chest - (right * 0.2f) + (forward * 0.34f) + (up * 0.12f);
                    rightHand = chest + (right * Mathf.Lerp(0.48f, -0.32f, progress)) +
                        (forward * (0.36f + (0.2f * sweep))) +
                        (up * Mathf.Lerp(0.18f, -0.04f, progress));
                    leftWeight *= 0.66f;
                    break;
                }
                case CombatantGesture.RogueSlash:
                {
                    float contact = BellPulse(progress, 0.4f, 0.3f);
                    float signedSide = side;
                    rightHand = chest +
                        (right * signedSide * Mathf.Lerp(0.56f, -0.34f, progress)) +
                        (forward * (0.18f + (0.56f * contact))) -
                        (up * Mathf.Lerp(-0.22f, 0.24f, progress));
                    leftHand = chest - (right * signedSide * 0.24f) +
                        (forward * (0.32f + (0.18f * contact))) +
                        (up * 0.04f);
                    break;
                }
                case CombatantGesture.RogueDoubleSlash:
                {
                    float first = BellPulse(progress, 0.3f, 0.2f);
                    float second = BellPulse(progress, 0.57f, 0.2f);
                    leftHand = chest - (right * Mathf.Lerp(0.5f, 0.16f, first)) +
                        (forward * (0.16f + (0.58f * first))) +
                        (up * Mathf.Lerp(0.18f, -0.14f, first));
                    rightHand = chest + (right * Mathf.Lerp(0.5f, 0.16f, second)) +
                        (forward * (0.16f + (0.62f * second))) +
                        (up * Mathf.Lerp(0.2f, -0.16f, second));
                    break;
                }
                case CombatantGesture.RogueFinisher:
                {
                    float contact = BellPulse(progress, 0.44f, 0.27f);
                    leftHand = chest - (right * Mathf.Lerp(0.48f, 0.13f, contact)) +
                        (forward * (0.12f + (0.7f * contact))) +
                        (up * Mathf.Lerp(0.32f, -0.25f, contact));
                    rightHand = chest + (right * Mathf.Lerp(0.48f, 0.13f, contact)) +
                        (forward * (0.12f + (0.7f * contact))) +
                        (up * Mathf.Lerp(0.32f, -0.25f, contact));
                    break;
                }
                case CombatantGesture.RogueShadow:
                    leftHand = chest - (right * 0.3f) + (forward * 0.34f) - (up * 0.18f);
                    rightHand = chest + (right * 0.3f) + (forward * 0.38f) - (up * 0.18f);
                    break;
                case CombatantGesture.RogueWhirlwind:
                {
                    float angle = progress * Mathf.PI * 2f * side;
                    Vector3 sweep = (right * Mathf.Cos(angle)) +
                        (forward * Mathf.Sin(angle));
                    Vector3 tangent = (-right * Mathf.Sin(angle)) +
                        (forward * Mathf.Cos(angle));
                    leftHand = chest - (sweep * 0.6f) +
                        (tangent * 0.22f) - (up * 0.1f);
                    rightHand = chest + (sweep * 0.6f) -
                        (tangent * 0.22f) - (up * 0.08f);
                    break;
                }
                case CombatantGesture.MageGather:
                {
                    float gather = Mathf.SmoothStep(0f, 1f, progress);
                    float orbit = Mathf.Sin(progress * Mathf.PI * 2f) * 0.05f;
                    leftHand = chest - (right * Mathf.Lerp(0.5f, 0.22f, gather)) +
                        (forward * Mathf.Lerp(0.12f, 0.34f, gather)) +
                        (up * (Mathf.Lerp(-0.06f, 0.26f, gather) + orbit));
                    rightHand = chest + (right * Mathf.Lerp(0.5f, 0.22f, gather)) +
                        (forward * Mathf.Lerp(0.12f, 0.34f, gather)) +
                        (up * (Mathf.Lerp(0.2f, 0.26f, gather) - orbit));
                    break;
                }
                case CombatantGesture.MageLaunch:
                {
                    float launch = BellPulse(progress, 0.36f, 0.32f);
                    float separation = Mathf.Lerp(0.34f, 0.14f, launch);
                    leftHand = chest - (right * separation) +
                        (forward * (0.22f + (0.66f * launch))) +
                        (up * Mathf.Lerp(0.18f, -0.08f, launch));
                    rightHand = chest + (right * separation) +
                        (forward * (0.22f + (0.66f * launch))) +
                        (up * Mathf.Lerp(0.12f, -0.08f, launch));
                    break;
                }
                case CombatantGesture.MageNova:
                {
                    float burst = BellPulse(progress, 0.42f, 0.38f);
                    leftHand = chest - (right * Mathf.Lerp(0.2f, 0.68f, burst)) +
                        (forward * 0.26f) - (up * Mathf.Lerp(-0.16f, 0.22f, burst));
                    rightHand = chest + (right * Mathf.Lerp(0.2f, 0.68f, burst)) +
                        (forward * 0.26f) - (up * Mathf.Lerp(-0.16f, 0.22f, burst));
                    break;
                }
                case CombatantGesture.MageWard:
                    leftHand = chest - (right * 0.44f) + (forward * 0.34f) + (up * 0.32f);
                    rightHand = chest + (right * 0.44f) + (forward * 0.34f) + (up * 0.32f);
                    break;
                case CombatantGesture.MageUtility:
                    leftHand = chest - (right * 0.2f) + (forward * 0.3f) + (up * 0.08f);
                    rightHand = chest +
                        (right * Mathf.Lerp(0.46f, -0.2f, progress)) +
                        (forward * 0.58f) +
                        (up * Mathf.Lerp(0.28f, -0.08f, progress));
                    leftWeight *= 0.55f;
                    break;
                case CombatantGesture.HunterDraw:
                {
                    float draw = Mathf.SmoothStep(0f, 1f, progress);
                    // Keep the bow arm long and move the drawing hand visibly
                    // outside the torso. At strategy-camera distance a compact
                    // generic spell pose collapses the weapon, hands, and hood
                    // into one vertical stick even when the feet stay grounded.
                    leftHand = chest - (right * 0.12f) +
                        (forward * Mathf.Lerp(0.44f, 0.80f, draw)) +
                        (up * Mathf.Lerp(0.07f, 0.18f, draw));
                    rightHand = chest +
                        (right * Mathf.Lerp(0.18f, 0.58f, draw)) +
                        (forward * Mathf.Lerp(0.30f, -0.22f, draw)) +
                        (up * Mathf.Lerp(0.05f, 0.24f, draw));
                    break;
                }
                case CombatantGesture.HunterLoose:
                {
                    float loose = BellPulse(progress, 0.32f, 0.3f);
                    leftHand = chest - (right * 0.12f) +
                        (forward * (0.68f + (0.14f * loose))) +
                        (up * 0.16f);
                    rightHand = chest +
                        (right * Mathf.Lerp(0.58f, 0.2f, loose)) +
                        (forward * Mathf.Lerp(-0.18f, 0.42f, loose)) +
                        (up * Mathf.Lerp(0.24f, 0.06f, loose));
                    break;
                }
                case CombatantGesture.HunterTrap:
                {
                    float plant = BellPulse(progress, 0.44f, 0.32f);
                    Vector3 lowChest = chest - (up * 0.48f);
                    leftHand = lowChest - (right * 0.2f) +
                        (forward * (0.26f + (0.28f * plant))) -
                        (up * (0.08f * plant));
                    rightHand = lowChest + (right * 0.2f) +
                        (forward * (0.26f + (0.28f * plant))) -
                        (up * (0.08f * plant));
                    break;
                }
                case CombatantGesture.HunterCommand:
                    leftHand = chest - (right * 0.18f) + (forward * 0.24f);
                    rightHand = chest +
                        (right * Mathf.Lerp(0.18f, 0.44f, progress)) +
                        (forward * Mathf.Lerp(0.34f, 0.62f, progress)) +
                        (up * Mathf.Lerp(0.12f, 0.48f, progress));
                    leftWeight *= 0.45f;
                    break;
                case CombatantGesture.HunterReady:
                    leftHand = chest - (right * 0.44f) +
                        (forward * 0.36f) + (up * 0.17f);
                    rightHand = chest + (right * 0.44f) +
                        (forward * 0.18f) + (up * 0.2f);
                    break;
                case CombatantGesture.HunterBrace:
                {
                    float brace = BellPulse(progress, 0.45f, 0.42f);
                    leftHand = chest +
                        (right * Mathf.Lerp(-0.42f, 0.2f, brace)) +
                        (forward * (0.34f + (0.2f * brace))) +
                        (up * 0.12f);
                    rightHand = chest +
                        (right * Mathf.Lerp(0.42f, -0.2f, brace)) +
                        (forward * (0.34f + (0.2f * brace))) +
                        (up * 0.18f);
                    break;
                }
                case CombatantGesture.HunterMelee:
                {
                    float clip = BellPulse(progress, 0.38f, 0.27f);
                    rightHand = chest +
                        (right * side * Mathf.Lerp(0.52f, -0.42f, progress)) +
                        (forward * (0.18f + (0.62f * clip))) -
                        (up * (0.22f * clip));
                    leftHand = chest - (right * side * 0.2f) +
                        (forward * (0.28f + (0.16f * clip))) +
                        (up * 0.1f);
                    leftWeight *= 0.62f;
                    break;
                }
                case CombatantGesture.HunterVault:
                    leftHand = chest - (right * 0.62f) -
                        (forward * 0.08f) + (up * 0.12f);
                    rightHand = chest + (right * 0.62f) -
                        (forward * 0.08f) + (up * 0.12f);
                    break;
                case CombatantGesture.HunterFeign:
                {
                    Vector3 lowChest = chest - (up * 0.56f);
                    leftHand = lowChest - (right * 0.34f) + (forward * 0.38f);
                    rightHand = lowChest + (right * 0.24f) + (forward * 0.46f);
                    break;
                }
            }

            leftElbow = origin - (right * 0.52f) + (up * 1.16f) + (forward * 0.05f);
            rightElbow = origin + (right * 0.52f) + (up * 1.16f) + (forward * 0.05f);
        }

        private void ApplyHandIk(
            AvatarIKGoal hand,
            AvatarIKHint elbow,
            Vector3 handPosition,
            Vector3 elbowPosition,
            float weight)
        {
            float clampedWeight = Mathf.Clamp01(weight);
            _activeAnimator.SetIKPositionWeight(hand, clampedWeight);
            _activeAnimator.SetIKRotationWeight(hand, 0f);
            _activeAnimator.SetIKPosition(hand, handPosition);
            _activeAnimator.SetIKHintPositionWeight(
                elbow,
                clampedWeight * _elbowHintWeight);
            _activeAnimator.SetIKHintPosition(elbow, elbowPosition);
        }

        private void ClearHandIk()
        {
            _activeAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 0f);
            _activeAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, 0f);
            _activeAnimator.SetIKRotationWeight(AvatarIKGoal.LeftHand, 0f);
            _activeAnimator.SetIKRotationWeight(AvatarIKGoal.RightHand, 0f);
            _activeAnimator.SetIKHintPositionWeight(AvatarIKHint.LeftElbow, 0f);
            _activeAnimator.SetIKHintPositionWeight(AvatarIKHint.RightElbow, 0f);
        }

        private void OnDisable()
        {
            PresentHunterWeapon(CombatantGesture.Neutral);
            ResetVisualTransform();
        }

        private void PresentHunterWeapon(CombatantGesture gesture)
        {
            if (_hunterStaticString == null &&
                _hunterDrawString == null &&
                _hunterNockedArrow == null)
            {
                return;
            }

            bool drawn = _classId == ClassId.Hunter &&
                _wasAlive &&
                _castingAbility != AbilityId.None &&
                gesture == CombatantGesture.HunterDraw;
            if (_hunterStaticString != null)
            {
                SetActiveIfChanged(_hunterStaticString, !drawn);
            }

            if (_hunterDrawString != null)
            {
                SetActiveIfChanged(_hunterDrawString, drawn);
            }

            if (_hunterNockedArrow != null)
            {
                SetActiveIfChanged(_hunterNockedArrow, drawn);
            }
        }

        private static void SetActiveIfChanged(GameObject value, bool active)
        {
            if (value.activeSelf != active)
            {
                value.SetActive(active);
            }
        }

        private Vector2 ResolveLocalImpactDirection(Vector3 sourcePosition)
        {
            Vector3 awayFromSource = transform.position - sourcePosition;
            awayFromSource.y = 0f;
            return ResolveLocalDirection(awayFromSource);
        }

        private Vector2 ResolveLocalDirection(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude <= 0.0001f)
            {
                return Vector2.up;
            }

            Vector3 localDirection = transform.InverseTransformDirection(
                worldDirection.normalized);
            Vector2 planarDirection = new Vector2(localDirection.x, localDirection.z);
            return planarDirection.sqrMagnitude > 1f
                ? planarDirection.normalized
                : planarDirection;
        }

        private static float BellPulse(float value, float center, float halfWidth)
        {
            float distance = Mathf.Abs(value - center) / Mathf.Max(0.001f, halfWidth);
            return Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(distance));
        }

        private void ResetVisualTransform()
        {
            if (_activeVisual == null)
            {
                return;
            }

            _activeVisual.localPosition = _baseLocalPosition;
            _activeVisual.localRotation = _baseLocalRotation;
            _activeVisual.localScale = _baseLocalScale;
        }

        private AbilityMotionStyle ResolveMotionStyle(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.Penance:
                    return AbilityMotionStyle.PriestChannel;
                case AbilityId.Mend:
                case AbilityId.Rescue:
                case AbilityId.FlashHeal:
                case AbilityId.Renew:
                case AbilityId.PrayerOfMending:
                    return AbilityMotionStyle.PriestHeal;
                case AbilityId.Aegis:
                case AbilityId.PowerWordShield:
                case AbilityId.PainSuppression:
                case AbilityId.InnerFocus:
                case AbilityId.FearWard:
                case AbilityId.DawnNova:
                    return AbilityMotionStyle.PriestWard;
                case AbilityId.HealerBolt:
                case AbilityId.Smite:
                case AbilityId.PsychicScream:
                case AbilityId.ShadowWordDeath:
                case AbilityId.Shadowfiend:
                case AbilityId.VampiricTouch:
                case AbilityId.ShadowWordPain:
                case AbilityId.DevouringPlague:
                case AbilityId.MindFlay:
                case AbilityId.MindBlast:
                case AbilityId.Silence:
                case AbilityId.PsychicHorror:
                case AbilityId.ManaBurn:
                case AbilityId.UmbralCollapse:
                    return AbilityMotionStyle.PriestShadow;
                case AbilityId.Purify:
                case AbilityId.DispelMagic:
                case AbilityId.MassDispel:
                case AbilityId.Fade:
                    return AbilityMotionStyle.PriestDispel;
                case AbilityId.PowerInfusion:
                case AbilityId.Dispersion:
                case AbilityId.Shadowform:
                    return AbilityMotionStyle.PriestWard;

                case AbilityId.QuickStrike:
                case AbilityId.Hemorrhage:
                case AbilityId.Shiv:
                case AbilityId.Garrote:
                case AbilityId.Gouge:
                case AbilityId.Dismantle:
                    return AbilityMotionStyle.RogueBuilder;
                case AbilityId.CheapShot:
                    return AbilityMotionStyle.RogueLowStrike;
                case AbilityId.Mutilate:
                    return AbilityMotionStyle.RogueDoubleStrike;
                case AbilityId.BladeTempest:
                    return AbilityMotionStyle.RogueWhirlwind;
                case AbilityId.Ambush:
                case AbilityId.Eviscerate:
                case AbilityId.Envenom:
                case AbilityId.KidneyShot:
                    return AbilityMotionStyle.RogueFinisher;
                case AbilityId.Kick:
                case AbilityId.Shadowstep:
                case AbilityId.Anchor:
                case AbilityId.ShadowDance:
                case AbilityId.CloakOfShadows:
                case AbilityId.Blind:
                case AbilityId.Vanish:
                case AbilityId.Stealth:
                case AbilityId.Evasion:
                case AbilityId.Sap:
                case AbilityId.Sprint:
                case AbilityId.Preparation:
                    return AbilityMotionStyle.RogueShadow;
                case AbilityId.ColdBlood:
                    return AbilityMotionStyle.RogueFinisher;

                case AbilityId.ArcaneBolt:
                case AbilityId.Fireball:
                case AbilityId.Frostbolt:
                case AbilityId.Polymorph:
                case AbilityId.Scorch:
                case AbilityId.Pyroblast:
                    return AbilityMotionStyle.MageHardCast;
                case AbilityId.FireBlast:
                case AbilityId.LivingBomb:
                case AbilityId.IceLance:
                    return AbilityMotionStyle.MageBolt;
                case AbilityId.Gust:
                case AbilityId.FrostNova:
                case AbilityId.DragonBreath:
                case AbilityId.BlastWave:
                case AbilityId.DeepFreeze:
                case AbilityId.Combustion:
                case AbilityId.ConeOfCold:
                case AbilityId.WaterElementalFreeze:
                case AbilityId.ColdSnap:
                case AbilityId.FlameSurge:
                case AbilityId.Blizzard:
                    return AbilityMotionStyle.MageRadial;
                case AbilityId.IceBarrier:
                case AbilityId.IcyVeins:
                case AbilityId.IceBlock:
                case AbilityId.ManaShield:
                case AbilityId.MoltenArmor:
                case AbilityId.MageArmor:
                case AbilityId.IceArmor:
                    return AbilityMotionStyle.MageWard;
                case AbilityId.Counterspell:
                case AbilityId.Blink:
                case AbilityId.Spellsteal:
                    return AbilityMotionStyle.MageUtility;
                case AbilityId.SteadyShot:
                    return AbilityMotionStyle.HunterDraw;
                case AbilityId.ChimeraShot:
                case AbilityId.AimedShot:
                case AbilityId.KillShot:
                case AbilityId.ArrowRain:
                    return AbilityMotionStyle.HunterHeavyShot;
                case AbilityId.ArcaneShot:
                case AbilityId.SerpentSting:
                case AbilityId.ViperSting:
                case AbilityId.ScorpidSting:
                case AbilityId.SilencingShot:
                case AbilityId.ScatterShot:
                case AbilityId.FreezingArrow:
                case AbilityId.TranquilizingShot:
                case AbilityId.ConcussiveShot:
                    return AbilityMotionStyle.HunterShot;
                case AbilityId.FrostTrap:
                case AbilityId.Flare:
                    return AbilityMotionStyle.HunterTrap;
                case AbilityId.WingClip:
                    return AbilityMotionStyle.HunterMelee;
                case AbilityId.PetAttack:
                case AbilityId.PetFollow:
                case AbilityId.PetStay:
                case AbilityId.SpiderWeb:
                case AbilityId.MastersCall:
                case AbilityId.RoarOfSacrifice:
                case AbilityId.MendPet:
                    return AbilityMotionStyle.HunterCommand;
                case AbilityId.HuntersMark:
                case AbilityId.Readiness:
                case AbilityId.RapidFire:
                case AbilityId.AspectOfTheDragonhawk:
                case AbilityId.AspectOfTheViper:
                    return AbilityMotionStyle.HunterPrepare;
                case AbilityId.Deterrence:
                    return AbilityMotionStyle.HunterGuard;
                case AbilityId.FeignDeath:
                    return AbilityMotionStyle.HunterFeign;
                case AbilityId.Disengage:
                    return AbilityMotionStyle.HunterVault;
                case AbilityId.ArenaTrinket:
                    return ResolveArenaTrinketMotion();
                default:
                    return ResolveClassFallbackMotion();
            }
        }

        private CombatantGesture ResolveGesture(AbilityId ability, bool release)
        {
            switch (ResolveMotionStyle(ability))
            {
                case AbilityMotionStyle.PriestChannel:
                    return CombatantGesture.PriestChannel;
                case AbilityMotionStyle.PriestHeal:
                    return CombatantGesture.PriestPrayer;
                case AbilityMotionStyle.PriestWard:
                    return CombatantGesture.PriestWard;
                case AbilityMotionStyle.PriestShadow:
                    return CombatantGesture.PriestShadow;
                case AbilityMotionStyle.PriestDispel:
                    return CombatantGesture.PriestDispel;
                case AbilityMotionStyle.RogueBuilder:
                    return CombatantGesture.RogueSlash;
                case AbilityMotionStyle.RogueLowStrike:
                    return CombatantGesture.RogueSlash;
                case AbilityMotionStyle.RogueDoubleStrike:
                    return CombatantGesture.RogueDoubleSlash;
                case AbilityMotionStyle.RogueFinisher:
                    return CombatantGesture.RogueFinisher;
                case AbilityMotionStyle.RogueShadow:
                    return CombatantGesture.RogueShadow;
                case AbilityMotionStyle.RogueWhirlwind:
                    return CombatantGesture.RogueWhirlwind;
                case AbilityMotionStyle.MageHardCast:
                    return release
                        ? CombatantGesture.MageLaunch
                        : CombatantGesture.MageGather;
                case AbilityMotionStyle.MageBolt:
                    return CombatantGesture.MageLaunch;
                case AbilityMotionStyle.MageRadial:
                    return CombatantGesture.MageNova;
                case AbilityMotionStyle.MageWard:
                    return CombatantGesture.MageWard;
                case AbilityMotionStyle.MageUtility:
                    return CombatantGesture.MageUtility;
                case AbilityMotionStyle.HunterDraw:
                    return release
                        ? CombatantGesture.HunterLoose
                        : CombatantGesture.HunterDraw;
                case AbilityMotionStyle.HunterShot:
                case AbilityMotionStyle.HunterHeavyShot:
                    return CombatantGesture.HunterLoose;
                case AbilityMotionStyle.HunterTrap:
                    return CombatantGesture.HunterTrap;
                case AbilityMotionStyle.HunterCommand:
                    return CombatantGesture.HunterCommand;
                case AbilityMotionStyle.HunterPrepare:
                    return CombatantGesture.HunterReady;
                case AbilityMotionStyle.HunterGuard:
                    return CombatantGesture.HunterBrace;
                case AbilityMotionStyle.HunterMelee:
                    return CombatantGesture.HunterMelee;
                case AbilityMotionStyle.HunterVault:
                    return CombatantGesture.HunterVault;
                case AbilityMotionStyle.HunterFeign:
                    return CombatantGesture.HunterFeign;
                default:
                    return CombatantGesture.Neutral;
            }
        }

        private static bool UsesMeleeHandWeight(CombatantGesture gesture)
        {
            switch (gesture)
            {
                case CombatantGesture.RogueSlash:
                case CombatantGesture.RogueDoubleSlash:
                case CombatantGesture.RogueFinisher:
                case CombatantGesture.RogueShadow:
                case CombatantGesture.RogueWhirlwind:
                case CombatantGesture.HunterMelee:
                    return true;
                default:
                    return false;
            }
        }

        private AbilityMotionStyle ResolveClassFallbackMotion()
        {
            switch (_classId)
            {
                case ClassId.Priest:
                    return AbilityMotionStyle.PriestHeal;
                case ClassId.Rogue:
                    return AbilityMotionStyle.RogueBuilder;
                case ClassId.Mage:
                    return AbilityMotionStyle.MageBolt;
                case ClassId.Hunter:
                    return AbilityMotionStyle.HunterShot;
                default:
                    return AbilityMotionStyle.Generic;
            }
        }

        private AbilityMotionStyle ResolveArenaTrinketMotion()
        {
            switch (_classId)
            {
                case ClassId.Priest:
                    return AbilityMotionStyle.PriestDispel;
                case ClassId.Rogue:
                    return AbilityMotionStyle.RogueShadow;
                case ClassId.Mage:
                    return AbilityMotionStyle.MageUtility;
                case ClassId.Hunter:
                    return AbilityMotionStyle.HunterPrepare;
                default:
                    return AbilityMotionStyle.Generic;
            }
        }

        private static bool IsFinisher(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.Ambush:
                case AbilityId.Eviscerate:
                case AbilityId.KidneyShot:
                case AbilityId.Envenom:
                case AbilityId.BladeTempest:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsMeleeMotion(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.QuickStrike:
                case AbilityId.Hemorrhage:
                case AbilityId.Shiv:
                case AbilityId.Mutilate:
                case AbilityId.Ambush:
                case AbilityId.Eviscerate:
                case AbilityId.Envenom:
                case AbilityId.KidneyShot:
                case AbilityId.Kick:
                case AbilityId.CheapShot:
                case AbilityId.WingClip:
                case AbilityId.Garrote:
                case AbilityId.Gouge:
                case AbilityId.Dismantle:
                case AbilityId.BladeTempest:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsTimedActivation(AbilityId ability)
        {
            return AbilityCatalog.TryGet(ability, out AbilityDefinition definition) &&
                definition.CastProfile.ActivationType != AbilityActivationType.Instant;
        }

        private static bool IsChannelActivation(AbilityId ability)
        {
            return AbilityCatalog.TryGet(ability, out AbilityDefinition definition) &&
                definition.CastProfile.ActivationType == AbilityActivationType.Channel;
        }

        private bool UsesGroundedHunterRangedPose(AbilityId ability)
        {
            if (_classId != ClassId.Hunter || ability == AbilityId.None)
            {
                return false;
            }

            switch (ResolveMotionStyle(ability))
            {
                case AbilityMotionStyle.HunterDraw:
                case AbilityMotionStyle.HunterShot:
                case AbilityMotionStyle.HunterHeavyShot:
                    return true;
                default:
                    return false;
            }
        }

        private static CastAction ResolveCastAction(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.FlashHeal:
                case AbilityId.Renew:
                case AbilityId.ShadowWordDeath:
                case AbilityId.Hemorrhage:
                case AbilityId.Mutilate:
                case AbilityId.Shiv:
                case AbilityId.BladeTempest:
                case AbilityId.CheapShot:
                case AbilityId.FireBlast:
                case AbilityId.LivingBomb:
                case AbilityId.DragonBreath:
                case AbilityId.IceLance:
                case AbilityId.DeepFreeze:
                case AbilityId.Counterspell:
                case AbilityId.ShadowWordPain:
                case AbilityId.DevouringPlague:
                case AbilityId.MindBlast:
                case AbilityId.Silence:
                case AbilityId.ChimeraShot:
                case AbilityId.AimedShot:
                case AbilityId.ArcaneShot:
                case AbilityId.KillShot:
                case AbilityId.SerpentSting:
                case AbilityId.ViperSting:
                case AbilityId.ScorpidSting:
                case AbilityId.ScatterShot:
                case AbilityId.SilencingShot:
                case AbilityId.TranquilizingShot:
                case AbilityId.ConcussiveShot:
                case AbilityId.Garrote:
                case AbilityId.Gouge:
                case AbilityId.Dismantle:
                    return CastAction.Basic;
                case AbilityId.Mend:
                case AbilityId.Rescue:
                case AbilityId.Ambush:
                case AbilityId.Gust:
                case AbilityId.FrostNova:
                case AbilityId.BossMeteor:
                case AbilityId.Penance:
                case AbilityId.Smite:
                case AbilityId.PsychicScream:
                case AbilityId.Eviscerate:
                case AbilityId.Envenom:
                case AbilityId.Fireball:
                case AbilityId.BlastWave:
                case AbilityId.Frostbolt:
                case AbilityId.VampiricTouch:
                case AbilityId.MindFlay:
                case AbilityId.ManaBurn:
                case AbilityId.SteadyShot:
                case AbilityId.FreezingArrow:
                case AbilityId.Scorch:
                case AbilityId.Pyroblast:
                case AbilityId.DawnNova:
                case AbilityId.UmbralCollapse:
                case AbilityId.FlameSurge:
                case AbilityId.Blizzard:
                case AbilityId.ArrowRain:
                    return CastAction.Power;
                case AbilityId.Aegis:
                case AbilityId.Anchor:
                case AbilityId.PowerWordShield:
                case AbilityId.PainSuppression:
                case AbilityId.ShadowDance:
                case AbilityId.CloakOfShadows:
                case AbilityId.Vanish:
                case AbilityId.Stealth:
                case AbilityId.IceBarrier:
                case AbilityId.PowerInfusion:
                case AbilityId.Dispersion:
                case AbilityId.Shadowform:
                case AbilityId.Deterrence:
                case AbilityId.RoarOfSacrifice:
                case AbilityId.InnerFocus:
                case AbilityId.FearWard:
                case AbilityId.IceBlock:
                case AbilityId.ManaShield:
                case AbilityId.MoltenArmor:
                case AbilityId.MageArmor:
                case AbilityId.IceArmor:
                    return CastAction.Guard;
                case AbilityId.Combustion:
                case AbilityId.ColdBlood:
                    return CastAction.Power;
                case AbilityId.Purify:
                case AbilityId.Kick:
                case AbilityId.BossKnockback:
                case AbilityId.Shadowfiend:
                case AbilityId.DispelMagic:
                case AbilityId.KidneyShot:
                case AbilityId.Blind:
                case AbilityId.Polymorph:
                case AbilityId.ArenaTrinket:
                case AbilityId.IcyVeins:
                case AbilityId.Evasion:
                case AbilityId.PsychicHorror:
                case AbilityId.MassDispel:
                case AbilityId.Fade:
                case AbilityId.HuntersMark:
                case AbilityId.FrostTrap:
                case AbilityId.FeignDeath:
                case AbilityId.WingClip:
                case AbilityId.MastersCall:
                case AbilityId.Readiness:
                case AbilityId.RapidFire:
                case AbilityId.PetAttack:
                case AbilityId.PetFollow:
                case AbilityId.PetStay:
                case AbilityId.SpiderWeb:
                case AbilityId.MendPet:
                case AbilityId.Flare:
                case AbilityId.AspectOfTheDragonhawk:
                case AbilityId.AspectOfTheViper:
                case AbilityId.PrayerOfMending:
                case AbilityId.ConeOfCold:
                case AbilityId.Spellsteal:
                case AbilityId.WaterElementalFreeze:
                case AbilityId.ColdSnap:
                case AbilityId.Sap:
                case AbilityId.Sprint:
                case AbilityId.Preparation:
                    return CastAction.Utility;
                case AbilityId.Shadowstep:
                case AbilityId.Blink:
                case AbilityId.Disengage:
                    return CastAction.MovementOnly;
                default:
                    return CastAction.Basic;
            }
        }

        private Animator ResolveAnimator(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Healer:
                    return _healerAnimator;
                case ClassId.Rogue:
                    return _rogueAnimator;
                case ClassId.Mage:
                    return _mageAnimator;
                case ClassId.Hunter:
                    return _hunterAnimator;
                case ClassId.Boss:
                    return _bossAnimator;
                default:
                    return null;
            }
        }

        private enum CastAction
        {
            MovementOnly,
            Basic,
            Power,
            Utility,
            Guard,
        }

        private enum AbilityMotionStyle
        {
            Generic,
            PriestChannel,
            PriestHeal,
            PriestWard,
            PriestShadow,
            PriestDispel,
            RogueBuilder,
            RogueLowStrike,
            RogueDoubleStrike,
            RogueFinisher,
            RogueShadow,
            MageHardCast,
            MageBolt,
            MageRadial,
            MageWard,
            MageUtility,
            HunterDraw,
            HunterShot,
            HunterHeavyShot,
            HunterTrap,
            HunterCommand,
            HunterPrepare,
            HunterGuard,
            HunterMelee,
            HunterVault,
            HunterFeign,
            RogueWhirlwind,
        }
    }
}
