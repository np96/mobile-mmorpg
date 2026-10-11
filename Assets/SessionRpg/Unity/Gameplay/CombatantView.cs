using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Input;
using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SoftTargetCandidate))]
    public sealed class CombatantView : MonoBehaviour
    {
        private const int GroundHitCapacity = 4;

        private const int SiegeSwarmFollowerCapacity = 2;

        private static readonly int MoveSpeedAnimatorHash =
            Animator.StringToHash(CombatantAnimationPresenter.MoveSpeedParameter);

        private static readonly int DeadAnimatorHash =
            Animator.StringToHash(CombatantAnimationPresenter.DeadParameter);

        private static readonly int CastingAnimatorHash =
            Animator.StringToHash(CombatantAnimationPresenter.CastingParameter);

        private const float PlayableWorldStatusHeight = 2.98f;

        private const float PlayableRelationMarkerHeight = 3.36f;

        private const float PlayableHumanMarkerHeight = 3.38f;

        private const float BossWorldStatusHeight = 2.30f;

        private const float BossRelationMarkerHeight = 2.68f;

        private const float BossHumanMarkerHeight = 2.70f;

        [Header("Class models")]
        [SerializeField] private GameObject _healerModel;
        [SerializeField] private GameObject _rogueModel;
        [SerializeField] private GameObject _mageModel;
        [SerializeField] private GameObject _hunterModel;
        [SerializeField] private GameObject _bossModel;

        [Header("Readability")]
        [SerializeField] private Renderer _teamRing;
        [SerializeField] private GameObject _targetRing;
        [SerializeField] private GameObject _humanMarker;
        [SerializeField] private CombatantRelationMarker _relationMarker;
        [SerializeField] private Transform _healthFill;
        [SerializeField] private Transform _barrierFill;
        [SerializeField] private Transform _castFill;
        [SerializeField] private Collider _targetCollider;
        [SerializeField] private Transform _aimPoint;
        [SerializeField] private CombatantAnimationPresenter _animationPresenter;
        [SerializeField] private AuraVisualPresenter _auraVisualPresenter;
        [SerializeField] private HunterCompanionView _companionView;
        [SerializeField] private LayerMask _groundLayers = ~0;
        [SerializeField, Min(0f)] private float _positionSharpness = 24f;
        [SerializeField, Min(0f)] private float _rotationSharpness = 18f;

        private readonly RaycastHit[] _groundHits = new RaycastHit[GroundHitCapacity];
        private readonly GameObject[] _siegeSwarmFollowerRoots =
            new GameObject[SiegeSwarmFollowerCapacity];
        private readonly Animator[] _siegeSwarmFollowerAnimators =
            new Animator[SiegeSwarmFollowerCapacity];
        private MaterialPropertyBlock _propertyBlock;

        private SoftTargetCandidate _targetCandidate;
        private GameObject _activeCharacterModel;
        private Renderer[] _activeModelRenderers = System.Array.Empty<Renderer>();
        private bool[] _activeModelRendererDefaults = System.Array.Empty<bool>();
        private ModelRendererCache _healerRendererCache;
        private ModelRendererCache _rogueRendererCache;
        private ModelRendererCache _mageRendererCache;
        private ModelRendererCache _hunterRendererCache;
        private ModelRendererCache _bossRendererCache;
        private CombatantSnapshot _snapshot;
        private GameObject _worldStatusRoot;
        private GameObject _contactShadow;
        private Vector3 _desiredPosition;
        private Vector3 _desiredFacing = Vector3.forward;
        private Vector3 _healthFillBaseScale;
        private Vector3 _barrierFillBaseScale;
        private Vector3 _castFillBaseScale;
        private Vector3 _teamRingAuthoredScale = Vector3.one;
        private Vector3 _teamRingRelationScale = Vector3.one;
        private Vector3 _targetRingBaseScale = Vector3.one;
        private Vector3 _healerModelAuthoredScale = Vector3.one;
        private Vector3 _rogueModelAuthoredScale = Vector3.one;
        private Vector3 _mageModelAuthoredScale = Vector3.one;
        private Vector3 _hunterModelAuthoredScale = Vector3.one;
        private Vector3 _bossModelAuthoredScale = Vector3.one;
        private Vector3 _lastSimulationPosition;
        private Color _teamColor = Color.white;
        private CombatantControlVisualState _controlVisualState;
        private float _absorbStrength;
        private float _mitigationStrength;
        private float _siegePresentationScale = 1f;
        private bool _referencesInitialized;
        private bool _hasLastSimulationPosition;
        private bool _modelVisible = true;
        private bool _isStealthed;
        private bool _isHiddenFromViewer;
        private bool _initialized;
        private UnityEngine.Camera _readabilityCamera;

        public ParticipantId Id { get; private set; }

        public TeamId Team { get; private set; }

        public ClassId Class { get; private set; }

        public RunEnemyTier EnemyTier { get; private set; }

        public SiegeEnemyArchetypeId SiegeArchetype { get; private set; }

        /// <summary>
        /// Visible rendered bodies represented by this authoritative Siege view.
        /// Medium enemies present one body; a living visible swarm presents three.
        /// Followers are presentation-only children and never add target candidates.
        /// </summary>
        public int SiegePresentationBodyCount
        {
            get
            {
                if (!_initialized ||
                    SiegeArchetype == SiegeEnemyArchetypeId.None ||
                    !_modelVisible ||
                    !gameObject.activeInHierarchy ||
                    _activeCharacterModel == null ||
                    !_activeCharacterModel.activeInHierarchy)
                {
                    return 0;
                }

                int count = 1;
                for (int index = 0; index < SiegeSwarmFollowerCapacity; index++)
                {
                    GameObject follower = _siegeSwarmFollowerRoots[index];
                    if (follower != null && follower.activeInHierarchy)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public bool IsHuman { get; private set; }

        public bool IsAlive => _snapshot.IsAlive;

        public bool IsStealthed => _isStealthed;

        public bool IsHiddenFromViewer => _isHiddenFromViewer;

#if UNITY_EDITOR || DEVELOPMENT_BUILD || SESSION_RPG_CAPTURE
        internal void ForceVisibleForStaticReview()
        {
            _isStealthed = false;
            _isHiddenFromViewer = false;
            SetModelVisible(true);
            SetActive(_contactShadow, true);
            SetActive(_worldStatusRoot, true);
            if (_relationMarker != null)
            {
                SetActive(_relationMarker.gameObject, true);
            }

            if (_teamRing != null)
            {
                _teamRing.enabled = true;
            }

            SetActive(_humanMarker, IsHuman);
            RefreshSiegeSwarmFollowerVisibility();
        }
#endif

        public bool IsModelVisible => _modelVisible;

        public bool IsTargetable => _targetCandidate != null && _targetCandidate.IsTargetable;

        public Transform ActiveCharacterModelRoot => _activeCharacterModel != null
            ? _activeCharacterModel.transform
            : null;

        public Transform AimPoint => _aimPoint != null ? _aimPoint : transform;

        public Transform EffectOrigin => _animationPresenter != null &&
            _animationPresenter.EffectOrigin != null
                ? _animationPresenter.EffectOrigin
                : AimPoint;

        public AuraVisualPresenter AuraVisuals => _auraVisualPresenter;

        public HunterCompanionView CompanionVisuals => _companionView;

        private void Awake()
        {
            EnsureReferencesInitialized();
        }

        private void Update()
        {
            if (!_initialized)
            {
                return;
            }

            float positionBlend = _positionSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-_positionSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, _desiredPosition, positionBlend);

            if (_desiredFacing.sqrMagnitude > 0.001f)
            {
                Quaternion desiredRotation = Quaternion.LookRotation(_desiredFacing, Vector3.up);
                float rotationBlend = _rotationSharpness <= 0f
                    ? 1f
                    : 1f - Mathf.Exp(-_rotationSharpness * Time.deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationBlend);
            }

            if (_teamRing != null)
            {
                float defensivePulse = (_absorbStrength * 0.035f) +
                    (_mitigationStrength * 0.055f);
                float identityPulse = IsHuman ? 0.018f : 0f;
                float pulse = 1f +
                    (Mathf.Sin(Time.time * (IsHuman ? 7f : 5f)) *
                     (defensivePulse + identityPulse));
                Vector3 ringScale = _teamRingRelationScale;
                ringScale.x *= pulse;
                ringScale.z *= pulse;
                _teamRing.transform.localScale = ringScale;
            }

            MirrorSiegeSwarmAnimatorState();
        }

        private void LateUpdate()
        {
            if (!_initialized)
            {
                return;
            }

            ApplyPersistentSiegePresentationScale();

            if (_readabilityCamera == null)
            {
                _readabilityCamera = UnityEngine.Camera.main;
            }

            if (_readabilityCamera == null)
            {
                return;
            }

            // These elements are authored in screen space but live in the world.
            // Matching the strategy camera rotation (rather than looking at the
            // camera position) keeps every bar horizontal, including combatants at
            // the edges of the perspective view.
            Quaternion screenRotation = _readabilityCamera.transform.rotation;
            SetWorldRotation(_worldStatusRoot, screenRotation);
            SetWorldRotation(_humanMarker, screenRotation);
        }

        public void Initialize(
            ParticipantId id,
            TeamId team,
            ClassId classId,
            bool isHuman,
            Vector3 spawnPosition)
        {
            EnsureReferencesInitialized();
            HideSiegeSwarmFollowers();
            Id = id;
            Team = team;
            Class = classId;
            EnemyTier = RunEnemyTier.None;
            SiegeArchetype = SiegeEnemyArchetypeId.None;
            _siegePresentationScale = 1f;
            IsHuman = isHuman;
            _initialized = true;
            _snapshot = default;
            _lastSimulationPosition = spawnPosition;
            _hasLastSimulationPosition = true;
            _desiredPosition = ProjectToGround(spawnPosition);
            transform.position = _desiredPosition;
            transform.localScale = Vector3.one;
            _controlVisualState = CombatantControlVisualState.None;
            _absorbStrength = 0f;
            _mitigationStrength = 0f;

            gameObject.SetActive(true);
            ConfigureSelectionCollider(classId);
            ConfigureReadabilityAnchorHeights(classId);

            ActivateCharacterModel(classId);
            CacheActiveModelRenderers(_activeCharacterModel);
            SetActive(_humanMarker, isHuman);
            SetActive(_targetRing, false);
            SetActive(_worldStatusRoot, true);
            SetActive(_contactShadow, true);
            if (_relationMarker != null)
            {
                SetActive(_relationMarker.gameObject, true);
            }

            if (_teamRing != null)
            {
                _teamRing.enabled = true;
            }

            _isStealthed = false;
            _isHiddenFromViewer = false;
            SetModelVisible(true);
            _relationMarker?.Configure(team, isHuman, classId);
            _relationMarker?.SetControlVisualState(CombatantControlVisualState.None);
            _relationMarker?.SetDefenseState(false, false);
            _relationMarker?.SetCastingState(false, false);
            ConfigureRelationRing(team, isHuman, classId);

            if (_targetCollider != null)
            {
                _targetCollider.enabled = true;
            }

            _targetCandidate.Configure(id.Value, team.Value, AimPoint);
            _targetCandidate.SetTargetable(true);
            _teamColor = team.Value == 1
                ? new Color(0.15f, 0.72f, 1f, 1f)
                : new Color(1f, 0.24f, 0.16f, 1f);
            ApplyTeamColor(_teamColor);
            _companionView?.Hide();
            _animationPresenter?.Initialize(classId, ActiveCharacterModelRoot);
            _animationPresenter?.ConfigureRelation(
                isHuman,
                team.Value == 1,
                classId == ClassId.Boss);
            _animationPresenter?.SetDefenseVisualState(0f, 0f);
            _auraVisualPresenter?.ResetPresentation();

            // Relation and animation initialization may refresh authored marker
            // transforms. Apply the class-specific readability anchors last so a
            // pooled view cannot retain playable heights when it becomes a boss.
            ConfigureReadabilityAnchorHeights(classId);
        }

        public void ConfigureLevelingArchetype(LevelingMobArchetypeId archetype)
        {
            SiegeArchetype = SiegeEnemyArchetypeId.None;
            _siegePresentationScale = 1f;
            HideSiegeSwarmFollowers();
            string badge;
            Color accent;
            switch (archetype)
            {
                case LevelingMobArchetypeId.AshMelee:
                    badge = "RA";
                    accent = new Color(0.55f, 0.88f, 0.34f, 1f);
                    break;
                case LevelingMobArchetypeId.AshRanged:
                    badge = "MK";
                    accent = new Color(0.96f, 0.72f, 0.24f, 1f);
                    break;
                case LevelingMobArchetypeId.AshBrute:
                    badge = "BR";
                    accent = new Color(1f, 0.34f, 0.18f, 1f);
                    break;
                case LevelingMobArchetypeId.AshCaster:
                    badge = "HX";
                    accent = new Color(1f, 0.49f, 0.16f, 1f);
                    break;
                case LevelingMobArchetypeId.AshElite:
                    badge = "PR";
                    accent = new Color(0.78f, 0.38f, 1f, 1f);
                    break;
                case LevelingMobArchetypeId.RimeFrostweaver:
                    badge = "FW";
                    accent = new Color(0.32f, 0.86f, 1f, 1f);
                    break;
                case LevelingMobArchetypeId.ReliquaryWarder:
                    badge = "WD";
                    accent = new Color(1f, 0.84f, 0.44f, 1f);
                    break;
                case LevelingMobArchetypeId.RiftWarden:
                    badge = "WN";
                    accent = new Color(1f, 0.23f, 0.1f, 1f);
                    break;
                default:
                    return;
            }

            _relationMarker?.ConfigureLevelingBadge(badge, accent);
        }

        /// <summary>
        /// Applies presentation-only identity language for a regular Hero Siege
        /// enemy. Authoritative health, damage, movement, and targeting remain in
        /// Core/Application; this method only changes the authored model scale and
        /// the compact world-space badge.
        /// </summary>
        public void ConfigureSiegeArchetype(SiegeEnemyArchetypeId archetype)
        {
            if (!_initialized || _activeCharacterModel == null)
            {
                throw new System.InvalidOperationException(
                    "Initialize the pooled combatant view before applying a Siege archetype.");
            }

            SiegeEnemyArchetypeDefinition definition =
                SiegeEnemyArchetypeCatalog.Get(archetype);
            if (Class != definition.Class)
            {
                throw new System.InvalidOperationException(
                    "Siege archetype " + archetype + " requires " +
                    definition.Class + " presentation, but the pooled view is " +
                    Class + ".");
            }

            SiegeArchetype = archetype;
            EnemyTier = definition.Tier;
            _siegePresentationScale = definition.PresentationScale;
            _activeCharacterModel.transform.localScale =
                GetAuthoredModelScale(Class) * definition.PresentationScale;

            if (definition.Tier == RunEnemyTier.SwarmMinion)
            {
                if (definition.Class != ClassId.Rogue)
                {
                    throw new System.InvalidOperationException(
                        "Siege swarm followers currently require a Rogue archetype source.");
                }

                EnsureSiegeSwarmFollowers();
                ConfigureSiegeSwarmFollowerScale(definition.PresentationScale);
                RefreshSiegeSwarmFollowerVisibility();
            }
            else
            {
                HideSiegeSwarmFollowers();
            }

            ResolveSiegeBadge(archetype, out string badge, out Color accent);
            _relationMarker?.ConfigureLevelingBadge(badge, accent);
        }

        /// <summary>
        /// Returns a dynamic run combatant to its authored pool slot without
        /// retaining identity or presentation state from the defeated actor.
        /// Safe to call more than once and safe for a never-initialized slot.
        /// </summary>
        public void ReleaseToPool()
        {
            EnsureReferencesInitialized();

            SetSelected(false);
            ClearCastPresentation();
            _controlVisualState = CombatantControlVisualState.None;
            _animationPresenter?.SetControlVisualState(
                CombatantControlVisualState.None);
            _relationMarker?.SetControlVisualState(
                CombatantControlVisualState.None);
            _absorbStrength = 0f;
            _mitigationStrength = 0f;
            _animationPresenter?.SetDefenseVisualState(0f, 0f);
            _relationMarker?.SetDefenseState(false, false);
            _relationMarker?.SetCastingState(false, false);
            _auraVisualPresenter?.ResetPresentation();
            if (_initialized)
            {
                // HunterCompanionView captures its authored transform in Awake.
                // A never-activated pool slot must not reset it before that cache exists.
                _companionView?.Hide();
            }
            HideSiegeSwarmFollowers();

            SetHorizontalFill(_healthFill, _healthFillBaseScale, 0f);
            SetHorizontalFill(_barrierFill, _barrierFillBaseScale, 0f);
            SetHorizontalFill(_castFill, _castFillBaseScale, 0f);
            SetActive(_targetRing, false);
            if (_targetRing != null)
            {
                _targetRing.transform.localScale = _targetRingBaseScale;
            }

            if (_targetCollider != null)
            {
                _targetCollider.enabled = false;
            }

            _targetCandidate.Configure(0, 0, AimPoint);
            _targetCandidate.SetTargetable(false);

            _isStealthed = false;
            _isHiddenFromViewer = false;
            SetModelVisible(true);
            RestoreAuthoredModelScales();
            if (_initialized && _activeCharacterModel != null)
            {
                // Rebinding clears death, locomotion, cast triggers, gesture
                // offsets, and relation scale before this slot is deactivated.
                _animationPresenter?.Initialize(Class, ActiveCharacterModelRoot);
            }
            ResetSiegeSwarmFollowerPresentation();

            SetActive(_healerModel, false);
            SetActive(_rogueModel, false);
            SetActive(_mageModel, false);
            SetActive(_hunterModel, false);
            SetActive(_bossModel, false);
            _activeCharacterModel = null;
            _activeModelRenderers = System.Array.Empty<Renderer>();
            _activeModelRendererDefaults = System.Array.Empty<bool>();
            _modelVisible = true;

            SetActive(_humanMarker, false);
            SetActive(_worldStatusRoot, false);
            SetActive(_contactShadow, false);
            if (_relationMarker != null)
            {
                SetActive(_relationMarker.gameObject, false);
            }

            _teamRingRelationScale = _teamRingAuthoredScale;
            if (_teamRing != null)
            {
                _teamRing.transform.localScale = _teamRingAuthoredScale;
                _teamRing.enabled = false;
            }

            _teamColor = Color.white;
            ApplyTeamColor(_teamColor);
            transform.localScale = Vector3.one;
            _desiredPosition = transform.position;
            _desiredFacing = Vector3.forward;
            _lastSimulationPosition = default;
            _hasLastSimulationPosition = false;
            _snapshot = default;
            Id = default;
            Team = default;
            Class = ClassId.None;
            EnemyTier = RunEnemyTier.None;
            SiegeArchetype = SiegeEnemyArchetypeId.None;
            _siegePresentationScale = 1f;
            IsHuman = false;
            _initialized = false;

            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Applies presentation-only Siege/Dungeon tier language. Scaling affects
        /// the active character model, not the authoritative Core position,
        /// collision topology, or the touch-selection collider.
        /// </summary>
        public void ConfigureRunEnemyTier(RunEnemyTier tier)
        {
            if (tier < RunEnemyTier.SwarmMinion || tier > RunEnemyTier.Boss)
            {
                throw new System.ArgumentOutOfRangeException(nameof(tier));
            }

            EnemyTier = tier;
            SiegeArchetype = SiegeEnemyArchetypeId.None;
            _siegePresentationScale = 1f;
            HideSiegeSwarmFollowers();
            float modelScale;
            string badge;
            Color accent;
            switch (tier)
            {
                case RunEnemyTier.SwarmMinion:
                    modelScale = 0.72f;
                    badge = "SW";
                    accent = new Color(0.95f, 0.78f, 0.24f, 1f);
                    break;
                case RunEnemyTier.Medium:
                    modelScale = 1f;
                    badge = "MD";
                    accent = new Color(1f, 0.42f, 0.2f, 1f);
                    break;
                default:
                    modelScale = 1.08f;
                    badge = "BS";
                    accent = new Color(1f, 0.16f, 0.1f, 1f);
                    break;
            }

            if (_activeCharacterModel != null)
            {
                _activeCharacterModel.transform.localScale =
                    GetAuthoredModelScale(Class) * modelScale;
            }

            _relationMarker?.ConfigureLevelingBadge(badge, accent);
        }

        public void PresentCompanion(CompanionSnapshot snapshot)
        {
            if (Class != ClassId.Hunter || snapshot.Owner != Id)
            {
                _companionView?.Hide();
                return;
            }

            _companionView?.Present(snapshot, _teamColor);
        }

        public void HideCompanion()
        {
            _companionView?.Hide();
        }

        public void Present(
            CombatantSnapshot snapshot,
            int currentTick,
            int tickRate,
            float maximumMoveSpeed,
            int viewerTeamId = 1,
            PersistentAuraVisual persistentAuras = PersistentAuraVisual.None)
        {
            _snapshot = snapshot;
            Vector3 simulationPosition = new Vector3(
                snapshot.Position.X,
                0f,
                snapshot.Position.Z);
            _desiredPosition = ProjectToGround(simulationPosition);
            _desiredFacing = new Vector3(snapshot.Facing.X, 0f, snapshot.Facing.Z);

            float normalizedMoveSpeed = 0f;
            Vector2 localMoveDirection = Vector2.zero;
            if (_hasLastSimulationPosition && tickRate > 0 && maximumMoveSpeed > 0.001f)
            {
                Vector2 previous = new Vector2(
                    _lastSimulationPosition.x,
                    _lastSimulationPosition.z);
                Vector2 current = new Vector2(simulationPosition.x, simulationPosition.z);
                normalizedMoveSpeed = Vector2.Distance(previous, current) * tickRate / maximumMoveSpeed;

                Vector3 movement = simulationPosition - _lastSimulationPosition;
                movement.y = 0f;
                if (movement.sqrMagnitude > 0.000001f)
                {
                    Vector3 facing = _desiredFacing.sqrMagnitude > 0.001f
                        ? _desiredFacing.normalized
                        : Vector3.forward;
                    Vector3 right = Vector3.Cross(Vector3.up, facing);
                    Vector3 movementDirection = movement.normalized;
                    localMoveDirection = new Vector2(
                        Vector3.Dot(movementDirection, right),
                        Vector3.Dot(movementDirection, facing));
                }
            }

            _lastSimulationPosition = simulationPosition;
            _hasLastSimulationPosition = true;
            _animationPresenter?.Present(
                normalizedMoveSpeed,
                localMoveDirection,
                snapshot.IsAlive);

            float healthRatio = snapshot.MaxHealth > 0f
                ? Mathf.Clamp01(snapshot.Health / snapshot.MaxHealth)
                : 0f;
            SetHorizontalFill(_healthFill, _healthFillBaseScale, healthRatio);
            SetHorizontalFill(
                _barrierFill,
                _barrierFillBaseScale,
                Mathf.Clamp01(snapshot.Barrier / Mathf.Max(1f, snapshot.MaxHealth)));
            SetAbsorbVisual(
                Mathf.Clamp01(snapshot.Barrier / Mathf.Max(1f, snapshot.MaxHealth)));

            float castRatio = 0f;
            bool casting = snapshot.CastingAbility != AbilityId.None &&
                snapshot.CastEndTick > currentTick;
            if (casting)
            {
                int totalTicks = Mathf.Max(1, snapshot.CastEndTick - snapshot.CastStartTick);
                castRatio = Mathf.Clamp01(
                    (currentTick - snapshot.CastStartTick) / (float)totalTicks);
            }

            SetHorizontalFill(_castFill, _castFillBaseScale, castRatio);
            bool channeling = casting && IsChannelAbility(snapshot.CastingAbility);
            SetCastPresentation(
                casting ? snapshot.CastingAbility : AbilityId.None,
                castRatio,
                channeling);

            bool alive = snapshot.IsAlive;
            ApplyStealthPresentation(
                alive && snapshot.HasAuraTag(AuraTag.Stealth),
                viewerTeamId,
                alive);
            _auraVisualPresenter?.Present(snapshot, viewerTeamId, persistentAuras);
            bool targetable = alive && !_isHiddenFromViewer;
            _targetCandidate.SetTargetable(targetable);
            if (_targetCollider != null)
            {
                _targetCollider.enabled = targetable;
            }

            if (!alive)
            {
                SetSelected(false);
                ClearCastPresentation();
                SetControlVisualState(CombatantControlVisualState.None);
                SetMitigationVisual(false);
            }

            RefreshSiegeSwarmFollowerVisibility();
        }

        public void SetSelected(bool selected)
        {
            bool showSelection = selected && IsAlive && !_isHiddenFromViewer;
            SetActive(_targetRing, showSelection);
            if (_targetRing != null)
            {
                _targetRing.transform.localScale = _targetRingBaseScale *
                    (showSelection ? 1.16f : 1f);
            }

            _relationMarker?.SetSelected(showSelection);
        }

        public void Flash()
        {
            _animationPresenter?.PlayImpactPulse();
        }

        public void SetCastPresentation(
            AbilityId ability,
            float normalizedProgress,
            bool channeling)
        {
            _animationPresenter?.SetCastState(ability, normalizedProgress, channeling);
            _relationMarker?.SetCastingState(ability != AbilityId.None, channeling);
            MirrorSiegeSwarmAnimatorState();
        }

        public void ClearCastPresentation()
        {
            _animationPresenter?.ClearCastState();
            _relationMarker?.SetCastingState(false, false);
            MirrorSiegeSwarmAnimatorState();
        }

        public void SetControlVisualState(CombatantControlVisualState state)
        {
            if (_controlVisualState == state)
            {
                return;
            }

            _controlVisualState = state;
            _animationPresenter?.SetControlVisualState(state);
            _relationMarker?.SetControlVisualState(state);
        }

        public void SetAbsorbVisual(float normalizedStrength)
        {
            float strength = Mathf.Clamp01(normalizedStrength);
            bool wasActive = _absorbStrength > 0.001f;
            bool isActive = strength > 0.001f;
            _absorbStrength = strength;
            _animationPresenter?.SetDefenseVisualState(
                _absorbStrength,
                _mitigationStrength);
            if (wasActive != isActive)
            {
                RefreshDefensePresentation();
            }
        }

        public void SetMitigationVisual(bool active, float normalizedStrength = 1f)
        {
            float strength = active ? Mathf.Clamp01(normalizedStrength) : 0f;
            bool wasActive = _mitigationStrength > 0.001f;
            bool isActive = strength > 0.001f;
            _mitigationStrength = strength;
            _animationPresenter?.SetDefenseVisualState(
                _absorbStrength,
                _mitigationStrength);
            if (wasActive != isActive)
            {
                RefreshDefensePresentation();
            }
        }

        public void PlayCastAnimation()
        {
            _animationPresenter?.PlayCast();
        }

        public void PlayCastAnimation(AbilityId ability)
        {
            _animationPresenter?.PlayCast(ability);
        }

        public void PlayCastReleaseAnimation(AbilityId ability)
        {
            _animationPresenter?.PlayCastRelease(ability);
        }

        public void CancelCastAnimation(
            bool hardInterrupted = false,
            Vector3 sourcePosition = default)
        {
            if (_animationPresenter == null)
            {
                return;
            }

            if (hardInterrupted && sourcePosition != default)
            {
                _animationPresenter.PlayCastCanceled(true, sourcePosition);
                return;
            }

            _animationPresenter.PlayCastCanceled(hardInterrupted);
        }

        public void PlayChannelPulseAnimation(AbilityId ability)
        {
            _animationPresenter?.PlayChannelPulse(ability);
        }

        public void PlayHitAnimation(Vector3 sourcePosition, bool playAuthoredHit = true)
        {
            if (playAuthoredHit)
            {
                _animationPresenter?.PlayHit(sourcePosition);
                return;
            }

            _animationPresenter?.PlayImpactPulse(sourcePosition);
        }

        public void PlayKnockbackAnimation(Vector3 direction)
        {
            _animationPresenter?.PlayKnockback(direction);
        }

        private Vector3 ProjectToGround(Vector3 position)
        {
            Vector3 origin = new Vector3(position.x, 12f, position.z);
            int hitCount = Physics.RaycastNonAlloc(
                origin,
                Vector3.down,
                _groundHits,
                30f,
                _groundLayers,
                QueryTriggerInteraction.Ignore);

            float bestY = float.NegativeInfinity;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _groundHits[index];
                if (hit.collider != null &&
                    !hit.collider.transform.IsChildOf(transform) &&
                    hit.collider.transform != transform &&
                    hit.point.y > bestY)
                {
                    bestY = hit.point.y;
                }
            }

            position.y = float.IsNegativeInfinity(bestY) ? 0f : bestY;
            return position;
        }

        private void ApplyTeamColor(Color color)
        {
            if (_teamRing == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            _teamRing.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            _propertyBlock.SetColor("_Color", color);
            _propertyBlock.SetColor("_EmissionColor", color * 1.35f);
            _teamRing.SetPropertyBlock(_propertyBlock);
        }

        private void EnsureReferencesInitialized()
        {
            if (_referencesInitialized)
            {
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            _targetCandidate = GetComponent<SoftTargetCandidate>();
            if (_animationPresenter == null)
            {
                _animationPresenter = GetComponent<CombatantAnimationPresenter>();
            }

            if (_auraVisualPresenter == null)
            {
                _auraVisualPresenter = GetComponent<AuraVisualPresenter>();
            }

            _healthFillBaseScale = _healthFill != null ? _healthFill.localScale : Vector3.one;
            _barrierFillBaseScale = _barrierFill != null ? _barrierFill.localScale : Vector3.one;
            _castFillBaseScale = _castFill != null ? _castFill.localScale : Vector3.one;
            _teamRingAuthoredScale = _teamRing != null
                ? _teamRing.transform.localScale
                : Vector3.one;
            _teamRingRelationScale = _teamRingAuthoredScale;
            _targetRingBaseScale = _targetRing != null
                ? _targetRing.transform.localScale
                : Vector3.one;
            _healerModelAuthoredScale = GetLocalScale(_healerModel);
            _rogueModelAuthoredScale = GetLocalScale(_rogueModel);
            _mageModelAuthoredScale = GetLocalScale(_mageModel);
            _hunterModelAuthoredScale = GetLocalScale(_hunterModel);
            _bossModelAuthoredScale = GetLocalScale(_bossModel);
            // The generated pool has five stable model roots. Cache every root
            // while the slot is cold, including inactive classes, so camp respawn
            // and class changes only select existing renderer/default arrays.
            _healerRendererCache = new ModelRendererCache(_healerModel);
            _rogueRendererCache = new ModelRendererCache(_rogueModel);
            _mageRendererCache = new ModelRendererCache(_mageModel);
            _hunterRendererCache = new ModelRendererCache(_hunterModel);
            _bossRendererCache = new ModelRendererCache(_bossModel);
            _worldStatusRoot = _healthFill != null && _healthFill.parent != null
                ? _healthFill.parent.gameObject
                : null;
            Transform contactShadow = transform.Find("ContactShadow");
            _contactShadow = contactShadow != null ? contactShadow.gameObject : null;
            _readabilityCamera = UnityEngine.Camera.main;
            _referencesInitialized = true;
        }

        private void ActivateCharacterModel(ClassId classId)
        {
            GameObject selectedModel;
            switch (classId)
            {
                case ClassId.Healer:
                    selectedModel = _healerModel;
                    break;
                case ClassId.Rogue:
                    selectedModel = _rogueModel;
                    break;
                case ClassId.Mage:
                    selectedModel = _mageModel;
                    break;
                case ClassId.Hunter:
                    selectedModel = _hunterModel;
                    break;
                case ClassId.Boss:
                    selectedModel = _bossModel;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(classId));
            }

            // Always clear every authored root before enabling the selected one. This
            // remains correct when a pooled CombatantView changes class between matches
            // and also avoids order-dependent results if a serialized reference is ever
            // accidentally duplicated.
            SetActive(_healerModel, false);
            SetActive(_rogueModel, false);
            SetActive(_mageModel, false);
            SetActive(_hunterModel, false);
            SetActive(_bossModel, false);

            if (selectedModel == null)
            {
                throw new System.InvalidOperationException(
                    "Combatant is missing the authored model root for " + classId + ".");
            }

            selectedModel.SetActive(true);
            selectedModel.transform.localScale = GetAuthoredModelScale(classId);
            _activeCharacterModel = selectedModel;
        }

        private Vector3 GetAuthoredModelScale(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Healer:
                    return _healerModelAuthoredScale;
                case ClassId.Rogue:
                    return _rogueModelAuthoredScale;
                case ClassId.Mage:
                    return _mageModelAuthoredScale;
                case ClassId.Hunter:
                    return _hunterModelAuthoredScale;
                case ClassId.Boss:
                    return _bossModelAuthoredScale;
                default:
                    return Vector3.one;
            }
        }

        private static Vector3 GetLocalScale(GameObject target) =>
            target != null ? target.transform.localScale : Vector3.one;

        private void RestoreAuthoredModelScales()
        {
            SetLocalScale(_healerModel, _healerModelAuthoredScale);
            SetLocalScale(_rogueModel, _rogueModelAuthoredScale);
            SetLocalScale(_mageModel, _mageModelAuthoredScale);
            SetLocalScale(_hunterModel, _hunterModelAuthoredScale);
            SetLocalScale(_bossModel, _bossModelAuthoredScale);
        }

        private static void SetLocalScale(GameObject target, Vector3 scale)
        {
            if (target != null)
            {
                target.transform.localScale = scale;
            }
        }

        private void EnsureSiegeSwarmFollowers()
        {
            if (_rogueModel == null)
            {
                throw new System.InvalidOperationException(
                    "Hero Siege swarm presentation requires the authored Rogue model.");
            }

            for (int index = 0; index < SiegeSwarmFollowerCapacity; index++)
            {
                if (_siegeSwarmFollowerRoots[index] != null)
                {
                    continue;
                }

                GameObject follower = Instantiate(_rogueModel, transform, false);
                follower.name = "SiegeSwarmFollower_" + (index + 1);
                follower.transform.localPosition =
                    _rogueModel.transform.localPosition + GetSiegeFollowerOffset(index);
                follower.transform.localRotation = _rogueModel.transform.localRotation;
                follower.transform.localScale = _rogueModelAuthoredScale;

                try
                {
                    ValidateSiegeFollowerHasNoGameplaySurface(follower);
                    Animator followerAnimator =
                        follower.GetComponentInChildren<Animator>(true);
                    if (followerAnimator == null)
                    {
                        throw new System.InvalidOperationException(
                            "A Siege swarm follower clone has no Rogue Animator.");
                    }

                    CombatantAnimatorIkRelay[] relays = follower
                        .GetComponentsInChildren<CombatantAnimatorIkRelay>(true);
                    for (int relayIndex = 0; relayIndex < relays.Length; relayIndex++)
                    {
                        CombatantAnimatorIkRelay relay = relays[relayIndex];
                        if (relay == null)
                        {
                            continue;
                        }

                        relay.Bind(null);
                        relay.enabled = false;
                    }

                    followerAnimator.applyRootMotion = false;
                    followerAnimator.keepAnimatorStateOnDisable = true;
                    followerAnimator.Rebind();
                    followerAnimator.SetFloat(MoveSpeedAnimatorHash, 0f);
                    followerAnimator.SetBool(DeadAnimatorHash, false);
                    followerAnimator.SetBool(CastingAnimatorHash, false);
                    followerAnimator.Update(0f);
                    follower.SetActive(false);
                    _siegeSwarmFollowerRoots[index] = follower;
                    _siegeSwarmFollowerAnimators[index] = followerAnimator;
                }
                catch
                {
                    // The clone never becomes visible if the source gains a
                    // targetable/gameplay component in a future art revision.
                    follower.SetActive(false);
                    Destroy(follower);
                    throw;
                }
            }
        }

        private static void ValidateSiegeFollowerHasNoGameplaySurface(GameObject follower)
        {
            bool hasForbiddenComponent =
                follower.GetComponentInChildren<SoftTargetCandidate>(true) != null ||
                follower.GetComponentInChildren<Collider>(true) != null ||
                follower.GetComponentInChildren<CombatantView>(true) != null ||
                follower.GetComponentInChildren<CombatantRelationMarker>(true) != null ||
                follower.GetComponentInChildren<AuraVisualPresenter>(true) != null ||
                follower.GetComponentInChildren<HunterCompanionView>(true) != null ||
                follower.GetComponentInChildren<TextMesh>(true) != null;
            if (hasForbiddenComponent)
            {
                throw new System.InvalidOperationException(
                    "A Siege swarm follower source contains targeting, collision, " +
                    "health, or identity presentation and cannot be used safely.");
            }

            Transform[] hierarchy = follower.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < hierarchy.Length; index++)
            {
                Transform item = hierarchy[index];
                if (item == null)
                {
                    continue;
                }

                string itemName = item.name;
                if (itemName == "WorldStatusBars" ||
                    itemName == "TeamRing" ||
                    itemName == "TargetRing" ||
                    itemName == "HumanMarker_DiamondChevron")
                {
                    throw new System.InvalidOperationException(
                        "A Siege swarm follower source contains the forbidden '" +
                        itemName + "' gameplay-readability surface.");
                }
            }
        }

        private void ConfigureSiegeSwarmFollowerScale(float presentationScale)
        {
            Vector3 scale = _rogueModelAuthoredScale * presentationScale;
            for (int index = 0; index < SiegeSwarmFollowerCapacity; index++)
            {
                GameObject follower = _siegeSwarmFollowerRoots[index];
                if (follower != null)
                {
                    follower.transform.localScale = scale;
                }
            }
        }

        private void ApplyPersistentSiegePresentationScale()
        {
            if (SiegeArchetype == SiegeEnemyArchetypeId.None ||
                _activeCharacterModel == null)
            {
                return;
            }

            Vector3 baseScale = _animationPresenter != null
                ? _animationPresenter.ConfiguredVisualScale
                : GetAuthoredModelScale(Class);
            Vector3 scale = baseScale * _siegePresentationScale;
            _activeCharacterModel.transform.localScale = scale;
            if (EnemyTier != RunEnemyTier.SwarmMinion)
            {
                return;
            }

            for (int index = 0; index < SiegeSwarmFollowerCapacity; index++)
            {
                GameObject follower = _siegeSwarmFollowerRoots[index];
                if (follower != null)
                {
                    follower.transform.localScale = scale;
                }
            }
        }

        private void RefreshSiegeSwarmFollowerVisibility()
        {
            MirrorSiegeSwarmAnimatorState();
            bool visible =
                _initialized &&
                Class == ClassId.Rogue &&
                EnemyTier == RunEnemyTier.SwarmMinion &&
                SiegeArchetype != SiegeEnemyArchetypeId.None &&
                _snapshot.IsAlive &&
                !_isStealthed &&
                !_isHiddenFromViewer &&
                _modelVisible &&
                gameObject.activeInHierarchy;
            for (int index = 0; index < SiegeSwarmFollowerCapacity; index++)
            {
                SetActive(_siegeSwarmFollowerRoots[index], visible);
            }
        }

        private void HideSiegeSwarmFollowers()
        {
            for (int index = 0; index < SiegeSwarmFollowerCapacity; index++)
            {
                SetActive(_siegeSwarmFollowerRoots[index], false);
            }
        }

        private void MirrorSiegeSwarmAnimatorState()
        {
            if (EnemyTier != RunEnemyTier.SwarmMinion ||
                SiegeArchetype == SiegeEnemyArchetypeId.None ||
                _siegeSwarmFollowerAnimators[0] == null)
            {
                return;
            }

            Animator mainAnimator = _animationPresenter != null
                ? _animationPresenter.ActiveAnimator
                : null;
            if (mainAnimator == null)
            {
                return;
            }

            float moveSpeed = mainAnimator.GetFloat(MoveSpeedAnimatorHash);
            bool dead = mainAnimator.GetBool(DeadAnimatorHash);
            bool casting = mainAnimator.GetBool(CastingAnimatorHash);
            for (int index = 0; index < SiegeSwarmFollowerCapacity; index++)
            {
                Animator followerAnimator = _siegeSwarmFollowerAnimators[index];
                if (followerAnimator == null)
                {
                    continue;
                }

                followerAnimator.SetFloat(MoveSpeedAnimatorHash, moveSpeed);
                followerAnimator.SetBool(DeadAnimatorHash, dead);
                followerAnimator.SetBool(CastingAnimatorHash, casting);
            }
        }

        private void ResetSiegeSwarmFollowerPresentation()
        {
            for (int index = 0; index < SiegeSwarmFollowerCapacity; index++)
            {
                GameObject follower = _siegeSwarmFollowerRoots[index];
                Animator followerAnimator = _siegeSwarmFollowerAnimators[index];
                if (follower != null)
                {
                    follower.SetActive(false);
                    follower.transform.localPosition =
                        _rogueModel.transform.localPosition +
                        GetSiegeFollowerOffset(index);
                    follower.transform.localRotation = _rogueModel.transform.localRotation;
                    follower.transform.localScale = _rogueModelAuthoredScale;
                }

                if (followerAnimator != null)
                {
                    followerAnimator.Rebind();
                    followerAnimator.SetFloat(MoveSpeedAnimatorHash, 0f);
                    followerAnimator.SetBool(DeadAnimatorHash, false);
                    followerAnimator.SetBool(CastingAnimatorHash, false);
                    followerAnimator.Update(0f);
                }
            }
        }

        private static Vector3 GetSiegeFollowerOffset(int index)
        {
            switch (index)
            {
                case 0:
                    return new Vector3(-0.68f, 0f, -0.62f);
                case 1:
                    return new Vector3(0.68f, 0f, -0.84f);
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        private static void ResolveSiegeBadge(
            SiegeEnemyArchetypeId archetype,
            out string badge,
            out Color accent)
        {
            switch (archetype)
            {
                case SiegeEnemyArchetypeId.Ashling:
                    badge = "AL";
                    accent = new Color(0.62f, 0.94f, 0.28f, 1f);
                    return;
                case SiegeEnemyArchetypeId.CinderRunner:
                    badge = "RN";
                    accent = new Color(1f, 0.55f, 0.14f, 1f);
                    return;
                case SiegeEnemyArchetypeId.AshboundRaider:
                    badge = "RA";
                    accent = new Color(0.96f, 0.3f, 0.16f, 1f);
                    return;
                case SiegeEnemyArchetypeId.CinderMarksman:
                    badge = "MK";
                    accent = new Color(1f, 0.8f, 0.22f, 1f);
                    return;
                case SiegeEnemyArchetypeId.AshenBrute:
                    badge = "BR";
                    accent = new Color(0.92f, 0.14f, 0.1f, 1f);
                    return;
                case SiegeEnemyArchetypeId.CinderHexer:
                    badge = "HX";
                    accent = new Color(0.9f, 0.34f, 1f, 1f);
                    return;
                case SiegeEnemyArchetypeId.RimeWeaver:
                    badge = "RM";
                    accent = new Color(0.24f, 0.86f, 1f, 1f);
                    return;
                case SiegeEnemyArchetypeId.ReliquaryWarder:
                    badge = "WD";
                    accent = new Color(1f, 0.92f, 0.48f, 1f);
                    return;
                case SiegeEnemyArchetypeId.CausewayPrelate:
                    badge = "PR";
                    accent = new Color(1f, 0.32f, 0.7f, 1f);
                    return;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(archetype));
            }
        }

        private void CacheActiveModelRenderers(GameObject activeModel)
        {
            ModelRendererCache cache;
            switch (Class)
            {
                case ClassId.Healer:
                    cache = _healerRendererCache;
                    break;
                case ClassId.Rogue:
                    cache = _rogueRendererCache;
                    break;
                case ClassId.Mage:
                    cache = _mageRendererCache;
                    break;
                case ClassId.Hunter:
                    cache = _hunterRendererCache;
                    break;
                case ClassId.Boss:
                    cache = _bossRendererCache;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(Class));
            }

            if (cache.Model != activeModel)
            {
                throw new System.InvalidOperationException(
                    "Combatant model roots cannot change after the pool slot is initialized.");
            }

            _activeModelRenderers = cache.Renderers;
            _activeModelRendererDefaults = cache.EnabledDefaults;
            for (int index = 0; index < _activeModelRenderers.Length; index++)
            {
                Renderer renderer = _activeModelRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = _activeModelRendererDefaults[index];
                }
            }

            _modelVisible = true;
        }

        private readonly struct ModelRendererCache
        {
            public ModelRendererCache(GameObject model)
            {
                Model = model;
                Renderers = model != null
                    ? model.GetComponentsInChildren<Renderer>(true)
                    : System.Array.Empty<Renderer>();
                EnabledDefaults = Renderers.Length > 0
                    ? new bool[Renderers.Length]
                    : System.Array.Empty<bool>();
                for (int index = 0; index < Renderers.Length; index++)
                {
                    EnabledDefaults[index] = Renderers[index] != null &&
                        Renderers[index].enabled;
                }
            }

            public GameObject Model { get; }
            public Renderer[] Renderers { get; }
            public bool[] EnabledDefaults { get; }
        }

        private void ApplyStealthPresentation(
            bool stealthed,
            int viewerTeamId,
            bool alive)
        {
            bool hiddenFromViewer = stealthed && Team.Value != viewerTeamId;
            _isStealthed = stealthed;
            _isHiddenFromViewer = hiddenFromViewer;

            // A stealthed body is genuinely absent. Friendly players retain the authored
            // ground/identity markers, while enemies lose every positional UI hint.
            SetModelVisible(!stealthed || !alive);
            SetActive(_contactShadow, !stealthed);
            SetActive(_worldStatusRoot, !hiddenFromViewer);
            if (_relationMarker != null)
            {
                SetActive(_relationMarker.gameObject, !hiddenFromViewer);
            }

            if (_teamRing != null)
            {
                _teamRing.enabled = !hiddenFromViewer;
            }

            SetActive(_humanMarker, IsHuman && !hiddenFromViewer);
            if (hiddenFromViewer)
            {
                SetSelected(false);
            }
        }

        private void SetModelVisible(bool visible)
        {
            if (_modelVisible == visible)
            {
                return;
            }

            _modelVisible = visible;
            for (int index = 0; index < _activeModelRenderers.Length; index++)
            {
                Renderer renderer = _activeModelRenderers[index];
                if (renderer != null)
                {
                    renderer.enabled = visible && _activeModelRendererDefaults[index];
                }
            }
        }

        private void ConfigureRelationRing(TeamId team, bool isHuman, ClassId classId)
        {
            Vector3 relationShape;
            if (classId == ClassId.Boss)
            {
                relationShape = new Vector3(1.28f, 1f, 1.28f);
            }
            else if (isHuman)
            {
                relationShape = new Vector3(1.18f, 1f, 1.18f);
            }
            else if (team.Value == 1)
            {
                relationShape = new Vector3(1.16f, 1f, 0.86f);
            }
            else
            {
                relationShape = new Vector3(0.86f, 1f, 1.16f);
            }

            _teamRingRelationScale = Vector3.Scale(
                _teamRingAuthoredScale,
                relationShape);
            if (_teamRing != null)
            {
                _teamRing.transform.localScale = _teamRingRelationScale;
            }
        }

        private void ConfigureSelectionCollider(ClassId classId)
        {
            if (!(_targetCollider is CapsuleCollider capsule))
            {
                return;
            }

            bool isBoss = classId == ClassId.Boss;
            capsule.center = new Vector3(0f, isBoss ? 1.5f : 1.175f, 0f);
            capsule.radius = isBoss ? 1.05f : 0.72f;
            capsule.height = isBoss ? 3f : 2.35f;
        }

        private void ConfigureReadabilityAnchorHeights(ClassId classId)
        {
            bool isBoss = classId == ClassId.Boss;
            if (_worldStatusRoot != null)
            {
                _worldStatusRoot.transform.localPosition = new Vector3(
                    0f,
                    isBoss ? BossWorldStatusHeight : PlayableWorldStatusHeight,
                    0f);
            }

            if (_relationMarker != null)
            {
                _relationMarker.transform.localPosition = new Vector3(
                    0f,
                    isBoss ? BossRelationMarkerHeight : PlayableRelationMarkerHeight,
                    0f);
            }

            if (_humanMarker != null)
            {
                _humanMarker.transform.localPosition = new Vector3(
                    0f,
                    isBoss ? BossHumanMarkerHeight : PlayableHumanMarkerHeight,
                    0f);
            }
        }

        private void RefreshDefensePresentation()
        {
            bool absorbing = _absorbStrength > 0.001f;
            bool mitigating = _mitigationStrength > 0.001f;
            _relationMarker?.SetDefenseState(absorbing, mitigating);

            Color ringColor = _teamColor;
            if (mitigating)
            {
                ringColor = Color.Lerp(ringColor, new Color(1f, 0.72f, 0.2f, 1f), 0.58f);
            }
            else if (absorbing)
            {
                ringColor = Color.Lerp(ringColor, new Color(0.42f, 0.92f, 1f, 1f), 0.52f);
            }

            ApplyTeamColor(ringColor);
        }

        private static bool IsChannelAbility(AbilityId ability)
        {
            return AbilityCatalog.TryGet(ability, out AbilityDefinition definition) &&
                definition.CastProfile.ActivationType == AbilityActivationType.Channel;
        }

        private static void SetHorizontalFill(Transform fill, Vector3 baseScale, float ratio)
        {
            if (fill == null)
            {
                return;
            }

            Vector3 scale = baseScale;
            scale.x *= ratio;
            fill.localScale = scale;
            fill.gameObject.SetActive(ratio > 0.001f);
        }

        private static void SetActive(GameObject value, bool active)
        {
            if (value != null && value.activeSelf != active)
            {
                value.SetActive(active);
            }
        }

        private static void SetWorldRotation(GameObject value, Quaternion rotation)
        {
            if (value != null && value.activeInHierarchy)
            {
                value.transform.rotation = rotation;
            }
        }
    }
}
