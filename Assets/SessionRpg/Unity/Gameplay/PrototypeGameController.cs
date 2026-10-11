using System;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Camera;
using SessionRpg.Unity.Arenas;
using SessionRpg.Unity.Input;
using SessionRpg.Unity.Presentation;
using SessionRpg.Unity.Product;
using SessionRpg.Unity.UI;
using SessionRpg.Unity.World;
using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    [DisallowMultipleComponent]
    public sealed partial class PrototypeGameController : MonoBehaviour
    {
        private const int HumanParticipantValue = 1;
        private const int LocalTeamValue = 1;
        private const int MaximumSessionSeconds = 180;
        private const int LevelingMaximumSessionSeconds = 600;
        private const int PveMaximumSessionSeconds = 900;
        private const int SiegeDefenderViewCapacity =
            RunPartyDefinition.MaximumMemberCount;

        [Header("Composition")]
        [SerializeField] private UnityEngine.Camera _gameCamera;
        [SerializeField] private StrategyFollowCamera _cameraRig;
        [SerializeField] private CombatInputSource _inputSource;
        [SerializeField] private SoftTargetSelectionService _targeting;
        [SerializeField] private PrototypeHud _hud;
        [SerializeField] private OffscreenThreatPresenter _offscreenThreats;
        [SerializeField] private KitViewerPanel _kitViewer;
        [SerializeField] private PauseSettingsPanel _pauseSettings;
        [SerializeField] private LocalProductServices _productServices;
        [SerializeField] private CombatantView[] _combatantViews =
            new CombatantView[CombatSimulation.MaxCombatants];
        [SerializeField] private TelegraphPresenter[] _telegraphs = new TelegraphPresenter[4];
        [SerializeField] private CombatEffectsPresenter _combatEffects;
        [SerializeField] private GameObject _arenaEnvironment;
        [SerializeField] private GameObject _bossEnvironment;
        [SerializeField] private ArenaContentRuntimeLoader _arenaContentLoader;
        [SerializeField] private WorldZoneStreamController _worldStreamer;
        [SerializeField] private WorldLightingPresenter _worldLighting;

        private readonly CombatantSnapshot[] _snapshots = new CombatantSnapshot[CombatSimulation.MaxCombatants];
        private readonly CompanionSnapshot[] _companionSnapshots =
            new CompanionSnapshot[CombatSimulation.MaxCombatants];
        private readonly GroundEffectSnapshot[] _groundEffectSnapshots =
            new GroundEffectSnapshot[CombatSimulation.MaxCombatants * 2];
        private readonly bool[] _presentedCompanions =
            new bool[CombatSimulation.MaxCombatants];
        private readonly DeterministicBotBrain[] _botBrains = new DeterministicBotBrain[CombatSimulation.MaxCombatants];
        private readonly DeterministicLevelingMobBrain[] _levelingMobBrains =
            new DeterministicLevelingMobBrain[CombatSimulation.MaxCombatants];
        private readonly DeterministicSiegeEnemyBrain[] _siegeEnemyBrains =
            new DeterministicSiegeEnemyBrain[CombatSimulation.MaxCombatants];
        private readonly DeterministicSiegeDefenderBrain[] _siegeDefenderBrains =
            new DeterministicSiegeDefenderBrain[CombatSimulation.MaxCombatants];
        private readonly ParticipantId[] _siegeRetirements =
            new ParticipantId[HeroSiegeRunCatalog.MaximumActiveHostileCount];
        private readonly int[] _nextHazardFeedbackTicks = new int[CombatSimulation.MaxCombatants];
        private readonly DeathRecapBuilder _deathRecapBuilder = new DeathRecapBuilder();
        private readonly ArenaTeamCoordinator _arenaTeamCoordinator =
            new ArenaTeamCoordinator();

        private LevelingRunController _levelingRun;
        private GuidedTrainingController _training;
        private HeroSiegeRunController _siegeRun;
        private DungeonRunController _dungeonRun;
        private RunPartyComposition _activeRunParty;

        private SessionFlowController _flow;
        private CombatSimulation _simulation;
        private CombatRules _rules;
        private GameModeId _selectedMode = GameModeId.Arena;
        private SiegeVariantId _selectedSiegeVariant =
            HeroSiegeRunCatalog.DefaultVariant;
        private ClassId _selectedClass = ClassId.Mage;
        private SpecializationId _selectedSpecialization = SpecializationId.Frost;
        private ParticipantId _humanId = new ParticipantId(HumanParticipantValue);
        private ParticipantId _focusedTarget;
        private ParticipantId _spectatorTarget;
        private uint _humanSequence;
        private float _accumulator;
        private int _snapshotCount;
        private int _nextTelegraph;
        private bool _fighterHudBound;
        private bool _hasHumanDeathRecap;
        private int _acceptedHumanCastCount;
        private CombatEvent _lastAcceptedHumanCast;
        private string _lastRecap = string.Empty;
        private SessionPhase _lastObjectivePhase = (SessionPhase)(-1);
        private GameModeId _lastObjectiveMode = (GameModeId)(-1);
        private int _lastObjectiveCountdownSeconds = int.MinValue;
        private ParticipantId _lastObjectiveSpectatorTarget;
        private int _lastProgressionLevel = int.MinValue;
        private int _lastProgressionExperience = int.MinValue;
        private int _lastObjectivePack = int.MinValue;
        private int _lastObjectiveMobCount = int.MinValue;
        private int _lastObjectiveRecoverySeconds = int.MinValue;
        private GuidedTrainingStep _lastTrainingStep = (GuidedTrainingStep)(-1);
        private int _lastRunEncounterNumber = int.MinValue;
        private int _lastRunRemainingCount = int.MinValue;
        private int _lastRunAttempt = int.MinValue;
        private int _lastObjectiveHealthPercent = int.MinValue;
        private RunStatus _lastRunStatus = (RunStatus)(-1);
        private SiegeRunStatus _lastSiegeStatus = (SiegeRunStatus)(-1);
        private int _lastSiegeActiveWaveCount = int.MinValue;
        private int _lastSiegePendingCount = int.MinValue;
        private int _lastSiegeGold = int.MinValue;
        private int _lastSiegeHealthPotions = int.MinValue;
        private int _lastSiegeManaPotions = int.MinValue;
        private int _lastSiegeNextWaveSeconds = int.MinValue;
        private int _siegeRetirementCount;
        private LevelingDistrictId _lastObjectiveDistrict =
            LevelingDistrictId.None;
        private AbilityId _pendingTrainingFlickAbility;
        private AbilitySlotDirection _pendingTrainingFlickDirection;
        private bool _trainingPracticeTargetSpawned;
        private bool _isPaused;
        private bool _sessionRecordOpen;
        private bool _hapticsEnabled;
        private bool _hasShownInteractionPrimer;

        public int AcceptedHumanCastCount => _acceptedHumanCastCount;

        public SiegeVariantId SelectedSiegeVariant => _selectedSiegeVariant;

        private void Awake()
        {
            _flow = new SessionFlowController();
            BindHud();
            BindInput();
            BindProductUi();
            ConfigureHome();
        }

        private void Start()
        {
            if (_productServices != null)
            {
                LocalSettingsSelection settings =
                    _productServices.GetSettingsSelection();
                _pauseSettings?.Apply(settings);
                ApplyPresentationSettings(settings);
            }

            ApplyInitialTrainingRecommendation();
            BindKitViewer();
            RefreshTrainingRecommendation();
        }

        private void OnDestroy()
        {
            FlushGrindingProgress(true);
            _worldLighting?.Restore();
            _worldStreamer?.DeactivateZone();
            UnbindHud();
            UnbindInput();
            UnbindProductUi();
        }

        private void Update()
        {
            FlushGrindingProgress();
            if (_grindingSaveError) { return; }
            if (_isPaused || _flow == null || _flow.Phase == SessionPhase.Home ||
                _flow.Phase == SessionPhase.Results || _simulation == null)
            {
                return;
            }

            ProcessInputFrame();

            _accumulator += Mathf.Min(Time.deltaTime, 0.1f);
            float tickSeconds = _rules.TickSeconds;
            while (_accumulator >= tickSeconds)
            {
                _accumulator -= tickSeconds;
                TickSession();
            }

            UpdateLevelingWorldFocus();

            FlushGrindingProgress();
            if (_grindingRecoveryPending) { RecoverGrindingAtRefuge(); }

            RefreshHud();
        }

        private void BindHud()
        {
            if (_hud == null)
            {
                return;
            }

            _hud.ClassSelected += OnClassSelected;
            _hud.SpecializationSelected += OnSpecializationSelected;
            _hud.ModeSelected += OnModeSelected;
            _hud.SiegeVariantSelected += OnSiegeVariantSelected;
            _hud.SiegePotionPurchaseRequested +=
                OnSiegePotionPurchaseRequested;
            _hud.SiegePotionUseRequested += OnSiegePotionUseRequested;
            _hud.StartRequested += OnStartRequested;
            _hud.SkipTrainingRequested += OnSkipTrainingRequested;
            _hud.ReplayRequested += OnReplayRequested;
            _hud.LeaveRequested += OnLeaveRequested;
            _hud.FighterSelected += OnFighterSelected;
        }

        private void UnbindHud()
        {
            if (_hud == null)
            {
                return;
            }

            _hud.ClassSelected -= OnClassSelected;
            _hud.SpecializationSelected -= OnSpecializationSelected;
            _hud.ModeSelected -= OnModeSelected;
            _hud.SiegeVariantSelected -= OnSiegeVariantSelected;
            _hud.SiegePotionPurchaseRequested -=
                OnSiegePotionPurchaseRequested;
            _hud.SiegePotionUseRequested -= OnSiegePotionUseRequested;
            _hud.StartRequested -= OnStartRequested;
            _hud.SkipTrainingRequested -= OnSkipTrainingRequested;
            _hud.ReplayRequested -= OnReplayRequested;
            _hud.LeaveRequested -= OnLeaveRequested;
            _hud.FighterSelected -= OnFighterSelected;
        }

        private void BindInput()
        {
            if (_inputSource != null)
            {
                _inputSource.WorldTapped += OnWorldTapped;
                _inputSource.CameraOrbitRequested += OnCameraOrbitRequested;
            }
        }

        private void UnbindInput()
        {
            if (_inputSource != null)
            {
                _inputSource.WorldTapped -= OnWorldTapped;
                _inputSource.CameraOrbitRequested -= OnCameraOrbitRequested;
            }
        }

        private void BindProductUi()
        {
            if (_pauseSettings == null)
            {
                return;
            }

            _pauseSettings.PauseChanged += OnPauseChanged;
            _pauseSettings.LeaveRequested += OnPauseLeaveRequested;
            _pauseSettings.TutorialResetRequested += OnTutorialResetRequested;
            _pauseSettings.SettingsChanged += OnSettingsChanged;
            if (_productServices != null)
            {
                LocalSettingsSelection settings =
                    _productServices.GetSettingsSelection();
                _pauseSettings.Apply(settings);
                ApplyPresentationSettings(settings);
            }
        }

        private void UnbindProductUi()
        {
            if (_pauseSettings == null)
            {
                return;
            }

            _pauseSettings.PauseChanged -= OnPauseChanged;
            _pauseSettings.LeaveRequested -= OnPauseLeaveRequested;
            _pauseSettings.TutorialResetRequested -= OnTutorialResetRequested;
            _pauseSettings.SettingsChanged -= OnSettingsChanged;
        }

        private void OnPauseChanged(bool paused)
        {
            bool canPause = _flow != null &&
                (_flow.Phase == SessionPhase.Countdown ||
                 _flow.Phase == SessionPhase.Playing ||
                 _flow.Phase == SessionPhase.Spectating);
            _isPaused = paused && canPause;
            if (!_isPaused && paused)
            {
                _pauseSettings?.SetPaused(false);
            }

            _inputSource?.ClearTransientInput();
        }

        private void OnPauseLeaveRequested()
        {
            ReturnHome();
        }

        private void OnTutorialResetRequested()
        {
            _productServices?.ResetTraining(_selectedSpecialization);
            if (_selectedMode == GameModeId.Leveling && _training != null)
            {
                _isPaused = false;
                _pauseSettings?.SetPaused(false);
                StartSession(GameModeId.Leveling);
                _hud?.ShowCombatNotice(
                    "TRAINING RESTARTED",
                    new Color(0.95f, 0.69f, 0.25f, 1f));
                return;
            }

            RefreshTrainingRecommendation();
            _hud?.ShowCombatNotice(
                "TRAINING RESET",
                new Color(0.95f, 0.69f, 0.25f, 1f));
        }

        private void OnSettingsChanged(LocalSettingsSelection settings)
        {
            _productServices?.ApplySettings(settings);
            ApplyPresentationSettings(settings);
        }

        private void ApplyPresentationSettings(LocalSettingsSelection settings)
        {
            AudioListener.volume = Mathf.Clamp01(
                settings.MasterVolumePercent / 100f);
            _combatEffects?.Audio?.SetMasterVolume(
                Mathf.Clamp01(settings.EffectsVolumePercent / 100f));
            _cameraRig?.SetReducedMotion(settings.ReducedMotion);
            _hud?.SetHighContrast(settings.HighContrast);
            _hapticsEnabled = settings.HapticsEnabled;
        }

        private void PresentResultHaptic()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (_hapticsEnabled && UnityEngine.Application.isMobilePlatform)
            {
                Handheld.Vibrate();
            }
#endif
        }

        private void OnWorldTapped(Vector2 screenPosition)
        {
            if (_simulation == null ||
                _flow == null ||
                _flow.Phase == SessionPhase.Home ||
                _flow.Phase == SessionPhase.Results ||
                _targeting == null ||
                !_targeting.TryGetScreenCandidate(
                    screenPosition,
                    out SoftTargetCandidate candidate))
            {
                return;
            }

            OnFighterSelected(candidate.TargetId);
        }

        private void OnCameraOrbitRequested(float yawDelta)
        {
            if (_isPaused ||
                _cameraRig == null ||
                _flow == null ||
                _flow.Phase == SessionPhase.Home ||
                _flow.Phase == SessionPhase.Results)
            {
                return;
            }

            _cameraRig.RotateHeading(yawDelta);
        }

        private void ConfigureHome()
        {
            _isPaused = false;
            _pauseSettings?.SetPaused(false);
            SetEnvironment(null);
            DeactivateCombatants();
            _hud?.SetSelectedClass(_selectedClass);
            _hud?.SetSelectedSpecialization(_selectedSpecialization);
            _hud?.SetSelectedMode(ToPrototypeMode(_selectedMode));
            _hud?.SetSelectedSiegeVariant(_selectedSiegeVariant);
            _hud?.SetSiegeSupplyState(
                false,
                0,
                0,
                0,
                HeroSiegeRunCatalog.HealthPotionCost,
                HeroSiegeRunCatalog.ManaPotionCost,
                false,
                false,
                false,
                false);
            _hud?.SetLevelProgression(1, 0, 0, 80, false);
            _hud?.SetStartInteractable(true);
            _hud?.SetAbilityControlsInteractable(true);
            _hud?.ClearFighters();
            _hud?.ShowHome();
            BindKitViewer();
            _kitViewer?.Close();
            RefreshTrainingRecommendation();
            _offscreenThreats?.Clear();
            _inputSource?.ClearTransientInput();
            _targeting?.ClearSelection();
        }

        private void OnClassSelected(ClassId classId)
        {
            _combatEffects?.PresentUiSelect();
            SpecializationId requestedSpecialization = _hud != null &&
                SpecializationCatalog.IsValidForClass(
                    classId,
                    _hud.SelectedSpecialization)
                    ? _hud.SelectedSpecialization
                    : SpecializationCatalog.GetDefault(classId);
            _flow.SelectClass(classId);
            _flow.SelectSpecialization(requestedSpecialization);
            _selectedClass = _flow.SelectedClass;
            _selectedSpecialization = _flow.SelectedSpecialization;
            _hud?.SetSelectedSpecialization(_selectedSpecialization);
            BindKitViewer();
            RefreshTrainingRecommendation();
            BindDesktopKit(_selectedSpecialization);
        }

        private void OnSpecializationSelected(SpecializationId specialization)
        {
            _combatEffects?.PresentUiSelect();
            _flow.SelectSpecialization(specialization);
            _selectedSpecialization = _flow.SelectedSpecialization;
            _hud?.BindSpecializationKit(_selectedSpecialization);
            BindKitViewer();
            RefreshTrainingRecommendation();
            BindDesktopKit(_selectedSpecialization);
        }

        private void OnModeSelected(PrototypeMode mode)
        {
            _combatEffects?.PresentUiSelect();
            switch (mode)
            {
                case PrototypeMode.Dungeon:
                    _selectedMode = GameModeId.Dungeon;
                    break;
                case PrototypeMode.Training:
                    _selectedMode = GameModeId.Leveling;
                    break;
                case PrototypeMode.PveLeveling:
                    _selectedMode = GameModeId.PveLeveling;
                    break;
                case PrototypeMode.HeroSiege:
                    _selectedMode = GameModeId.HeroSiege;
                    break;
                default:
                    _selectedMode = GameModeId.Arena;
                    break;
            }

            BindKitViewer();
        }

        private void OnSiegeVariantSelected(SiegeVariantId variant)
        {
            _combatEffects?.PresentUiSelect();
            SetSiegeVariant(variant);
        }

        private void OnSiegePotionPurchaseRequested(SiegePotionId potion)
        {
            if (_selectedMode != GameModeId.HeroSiege || _siegeRun == null ||
                _simulation == null)
            {
                return;
            }

            SiegePurchaseObservation purchase =
                _siegeRun.TryPurchasePotion(potion);
            if (!purchase.WasHandled)
            {
                SiegeRunState state = _siegeRun.State;
                int count = potion == SiegePotionId.Health
                    ? state.HealthPotionCount
                    : state.ManaPotionCount;
                string blocker = count >= HeroSiegeRunCatalog.MaximumPotionCount
                    ? "POTION INVENTORY FULL"
                    : "NEED " + HeroSiegeRunCatalog.GetPotionCost(potion) +
                      " GOLD";
                _hud?.ShowCombatNotice(
                    blocker,
                    new Color(0.96f, 0.47f, 0.32f, 1f));
                return;
            }

            _combatEffects?.PresentUiConfirm();
            _hud?.ShowCombatNotice(
                (potion == SiegePotionId.Health ? "HP" : "MANA") +
                " POTION BOUGHT  •  " + purchase.RemainingGold + " GOLD",
                new Color(0.95f, 0.76f, 0.24f, 1f));
            RefreshSiegeSupply();
        }

        private void OnSiegePotionUseRequested(SiegePotionId potion)
        {
            if (_selectedMode != GameModeId.HeroSiege || _siegeRun == null ||
                _simulation == null ||
                !_simulation.TryGetSnapshot(
                    _humanId,
                    out CombatantSnapshot human))
            {
                return;
            }

            SiegePotionUse use = _siegeRun.TryConsumePotion(potion, human);
            if (!use.WasHandled)
            {
                SiegeRunState state = _siegeRun.State;
                int count = potion == SiegePotionId.Health
                    ? state.HealthPotionCount
                    : state.ManaPotionCount;
                string blocker = count <= 0
                    ? "NO " + (potion == SiegePotionId.Health ? "HP" : "MANA") +
                      " POTION"
                    : potion == SiegePotionId.Mana &&
                      human.ResourceType != ResourceType.Mana
                        ? "THIS HERO DOES NOT USE MANA"
                        : (potion == SiegePotionId.Health
                            ? "HEALTH ALREADY FULL"
                            : "MANA ALREADY FULL");
                _hud?.ShowCombatNotice(
                    blocker,
                    new Color(0.96f, 0.47f, 0.32f, 1f));
                return;
            }

            float restoredAmount;
            bool restored = use.ResourceType == ResourceType.None
                ? _simulation.TryRestoreHealth(
                    use.Consumer,
                    use.RequestedRestore,
                    out restoredAmount)
                : _simulation.TryRestoreResource(
                    use.Consumer,
                    use.ResourceType,
                    use.RequestedRestore,
                    out restoredAmount);
            if (!restored)
            {
                throw new InvalidOperationException(
                    "A validated Siege potion could not be applied to Core state.");
            }

            _combatEffects?.PresentUiConfirm();
            _hud?.ShowCombatNotice(
                (potion == SiegePotionId.Health ? "HEALTH" : "MANA") +
                " RESTORED  +" + Mathf.CeilToInt(restoredAmount),
                new Color(0.39f, 0.94f, 0.63f, 1f));
            RefreshSiegeSupply();
        }

        private void OnSkipTrainingRequested()
        {
            _combatEffects?.PresentUiConfirm();
            _productServices?.BeginSession(
                SessionRecordMode.Training,
                _selectedSpecialization);
            _productServices?.RecordTrainingOutcome(
                _selectedSpecialization,
                LocalTrainingOutcome.Skipped);
            _productServices?.FinishSession(
                SessionRecordMode.Training,
                _selectedSpecialization,
                0,
                0,
                SessionRecordOutcome.Skipped);
            _selectedMode = GameModeId.Arena;
            _hud?.SetSelectedMode(PrototypeMode.Arena2V2);
            StartSession(GameModeId.Arena);
        }

        private void OnStartRequested()
        {
            _combatEffects?.PresentUiConfirm();
            StartSession(_selectedMode);
        }

        private void OnReplayRequested()
        {
            _combatEffects?.PresentUiConfirm();
            if (_selectedMode == GameModeId.Leveling &&
                _flow != null &&
                _flow.ViewState.DidWin)
            {
                _selectedMode = GameModeId.Arena;
                _hud?.SetSelectedMode(PrototypeMode.Arena2V2);
            }

            StartSession(_selectedMode);
        }

        private void OnLeaveRequested()
        {
            _combatEffects?.PresentUiSelect();
            ReturnHome();
        }

        private void OnFighterSelected(int participantValue)
        {
            if (_simulation == null)
            {
                return;
            }

            ParticipantId participant = new ParticipantId(participantValue);
            if (!_simulation.TryGetSnapshot(participant, out CombatantSnapshot snapshot) ||
                !_simulation.TryGetSnapshot(_humanId, out CombatantSnapshot human) ||
                !HumanTargetSelector.IsSelectable(human, snapshot))
            {
                return;
            }

            _focusedTarget = participant;
            if (snapshot.Team.Value != LocalTeamValue)
            {
                ObserveTraining(
                    GuidedTrainingObservation.SelectedTarget(participant));
            }
            _combatEffects?.PresentTargetSelected();
            if (_targeting != null && snapshot.Team.Value != LocalTeamValue)
            {
                CombatantView view = FindView(participant);
                SoftTargetCandidate candidate = view != null
                    ? view.GetComponent<SoftTargetCandidate>()
                    : null;
                if (candidate == null || !_targeting.TrySetExplicit(candidate))
                {
                    // The controller keeps the exact tapped participant as focus so
                    // readiness can explain range. The world-pick helper must not
                    // retain a different enemy as a second, stale target.
                    _targeting.ClearSelection();
                }
            }
            else
            {
                _targeting?.ClearSelection();
            }

            RefreshTargetSelectionVisual();
            RefreshFighterHud();
            RefreshHud();
        }

        public void StartLocalSession(GameModeId mode, ClassId classId)
        {
            StartLocalSession(mode, classId, SpecializationCatalog.GetDefault(classId));
        }

        public void SetSiegeVariant(SiegeVariantId variant)
        {
            HeroSiegeRunCatalog.GetVariant(variant);
            _selectedSiegeVariant = variant;
            _hud?.SetSelectedSiegeVariant(variant);
        }

        public void StartLocalSiegeSession(
            SiegeVariantId variant,
            ClassId classId,
            SpecializationId specialization)
        {
            SetSiegeVariant(variant);
            StartLocalSession(GameModeId.HeroSiege, classId, specialization);
        }

        public void StartLocalSession(
            GameModeId mode,
            ClassId classId,
            SpecializationId specialization)
        {
            if (classId != ClassId.Healer &&
                classId != ClassId.Rogue &&
                classId != ClassId.Mage &&
                classId != ClassId.Hunter)
            {
                throw new ArgumentOutOfRangeException(nameof(classId));
            }

            if (!SpecializationCatalog.IsValidForClass(classId, specialization))
            {
                throw new ArgumentOutOfRangeException(nameof(specialization));
            }

            if (!FlushGrindingProgress()) { return; }
            _selectedClass = classId;
            _selectedSpecialization = specialization;
            _selectedMode = mode;
            _hud?.SetSelectedClass(classId);
            _hud?.SetSelectedSpecialization(specialization);
            _hud?.SetSelectedMode(ToPrototypeMode(mode));
            StartSession(mode);
        }

        public void ReturnHome()
        {
            if (!FlushGrindingProgress()) { return; }
            FinishSessionRecord(SessionRecordOutcome.Abandoned);
            AbandonActiveRun();
            _flow.ReturnHome();
            _simulation = null;
            _levelingRun = null;
            _grindingRun = null;
            _grindingRecoveryPending = false;
            _grindingSaveError = false;
            _training = null;
            _siegeRun = null;
            _dungeonRun = null;
            _activeRunParty = default;
            ConfigureHome();
        }

        private void StartSession(GameModeId mode)
        {
            if (!FlushGrindingProgress()) { return; }
            if (!PrepareGrindingSession(mode)) { return; }
            FinishSessionRecord(SessionRecordOutcome.Abandoned);
            AbandonActiveRun();
            _selectedMode = mode;
            switch (mode)
            {
                case GameModeId.Arena:
                    _rules = CombatRules.ArenaDefault;
                    break;
                case GameModeId.Boss:
                    _rules = CombatRules.BossDefault;
                    break;
                case GameModeId.Leveling:
                    _rules = CombatRules.LevelingDefault;
                    break;
                case GameModeId.PveLeveling:
                    _rules = CombatRules.PveLevelingDefault;
                    break;
                case GameModeId.HeroSiege:
                    _rules = CombatRules.HeroSiegeDefault;
                    break;
                case GameModeId.Dungeon:
                    _rules = default;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, null);
            }

            _simulation = _rules.TickRate > 0
                ? new CombatSimulation(_rules)
                : null;
            _levelingRun = mode == GameModeId.Leveling
                ? new LevelingRunController()
                : null;
            _training = mode == GameModeId.Leveling
                ? new GuidedTrainingController(_selectedSpecialization)
                : null;
            _training?.Start();
            _siegeRun = mode == GameModeId.HeroSiege
                ? new HeroSiegeRunController(
                    new RunId(1u),
                    _selectedSiegeVariant)
                : null;
            _dungeonRun = mode == GameModeId.Dungeon
                ? new DungeonRunController()
                : null;
            _activeRunParty = _siegeRun != null
                ? RunPartyCompositionResolver.Resolve(
                    _siegeRun.Variant.Party,
                    _selectedClass,
                    _selectedSpecialization)
                : _dungeonRun != null
                    ? RunPartyCompositionResolver.Resolve(
                        _dungeonRun.Definition.Party,
                        _selectedClass,
                        _selectedSpecialization)
                    : default;
            _humanSequence = 0;
            _accumulator = 0f;
            _snapshotCount = 0;
            _nextTelegraph = 0;
            _lastRecap = string.Empty;
            _hasHumanDeathRecap = false;
            _acceptedHumanCastCount = 0;
            _lastAcceptedHumanCast = default;
            _lastObjectivePhase = (SessionPhase)(-1);
            _lastObjectiveMode = (GameModeId)(-1);
            _lastObjectiveCountdownSeconds = int.MinValue;
            _lastObjectiveSpectatorTarget = default;
            _lastProgressionLevel = int.MinValue;
            _lastProgressionExperience = int.MinValue;
            _lastObjectivePack = int.MinValue;
            _lastObjectiveMobCount = int.MinValue;
            _lastObjectiveRecoverySeconds = int.MinValue;
            _lastTrainingStep = (GuidedTrainingStep)(-1);
            _lastRunEncounterNumber = int.MinValue;
            _lastRunRemainingCount = int.MinValue;
            _lastRunAttempt = int.MinValue;
            _lastObjectiveHealthPercent = int.MinValue;
            _lastRunStatus = (RunStatus)(-1);
            _lastSiegeStatus = (SiegeRunStatus)(-1);
            _lastSiegeActiveWaveCount = int.MinValue;
            _lastSiegePendingCount = int.MinValue;
            _lastSiegeGold = int.MinValue;
            _lastSiegeHealthPotions = int.MinValue;
            _lastSiegeManaPotions = int.MinValue;
            _lastSiegeNextWaveSeconds = int.MinValue;
            _siegeRetirementCount = 0;
            _lastObjectiveDistrict = LevelingDistrictId.None;
            _pendingTrainingFlickAbility = AbilityId.None;
            _pendingTrainingFlickDirection = AbilitySlotDirection.None;
            _trainingPracticeTargetSpawned = false;
            if (_rules.TickRate > 0)
            {
                _deathRecapBuilder.Reset(_rules.TickRate);
            }
            _arenaTeamCoordinator.Reset();
            _focusedTarget = default;
            _spectatorTarget = default;
            _fighterHudBound = false;
            _offscreenThreats?.Clear();
            Array.Clear(_botBrains, 0, _botBrains.Length);
            Array.Clear(_levelingMobBrains, 0, _levelingMobBrains.Length);
            Array.Clear(_siegeEnemyBrains, 0, _siegeEnemyBrains.Length);
            Array.Clear(_siegeDefenderBrains, 0, _siegeDefenderBrains.Length);
            Array.Clear(_siegeRetirements, 0, _siegeRetirements.Length);
            Array.Clear(_nextHazardFeedbackTicks, 0, _nextHazardFeedbackTicks.Length);
            DeactivateCombatants();

            SetEnvironment(mode);
            if (mode == GameModeId.Arena)
            {
                SetupArena();
            }
            else if (mode == GameModeId.Boss)
            {
                SetupBoss();
            }
            else if (mode == GameModeId.Leveling)
            {
                SetupLeveling();
            }
            else if (mode == GameModeId.PveLeveling)
            {
                SetupGrindingPlayer();
            }
            else if (mode == GameModeId.HeroSiege)
            {
                SetupHeroSiege();
            }
            else
            {
                if (!_dungeonRun.TryStartFirstFloor(
                        0,
                        out EncounterDefinition floor))
                {
                    throw new InvalidOperationException(
                        "Dungeon could not start its first authored floor.");
                }

                SetupRunEncounter(floor);
            }

            _flow.ReturnHome();
            _flow.SelectClass(_selectedClass);
            _flow.SelectSpecialization(_selectedSpecialization);
            _flow.Start(mode, _rules.TickRate * 3);
            _productServices?.BeginSession(
                ToSessionRecordMode(mode),
                _selectedSpecialization);
            _sessionRecordOpen = true;
            _hud?.SetAbilityControlsInteractable(true);
            BindActivePlayerKit();
            BindDesktopKit(_selectedSpecialization);
            _hud?.SetTeam(
                mode == GameModeId.PveLeveling
                    ? "PVE LEVELING  •  LOCAL SAVE"
                    : mode == GameModeId.Leveling
                    ? "GUIDED TRAINING"
                    : mode == GameModeId.HeroSiege
                        ? "CORE DEFENDERS"
                        : mode == GameModeId.Dungeon
                            ? "DUNGEON PARTY"
                            : "YOUR PARTY",
                mode == GameModeId.Leveling
                    ? new Color(0.9f, 0.64f, 0.25f, 1f)
                    : new Color(0.2f, 0.78f, 1f, 1f));
            _hud?.ShowGameplay();
            _inputSource?.ClearTransientInput();
            _targeting?.ClearSelection();
            if (mode == GameModeId.HeroSiege)
            {
                _cameraRig?.SetSiegeComposition(
                    _selectedSiegeVariant == SiegeVariantId.FourLanes,
                    false);
            }
            else
            {
                _cameraRig?.SetGameplayComposition(mode, false);
            }

            if (!_hasShownInteractionPrimer && mode != GameModeId.Leveling)
            {
                _hasShownInteractionPrimer = true;
                _hud?.ShowCombatNotice(
                    "TAP A HERO OR PORTRAIT TO FOCUS  •  HOLD A GROUPED SKILL FOR ITS WHEEL  •  DRAG EMPTY FIELD TO ORBIT",
                    new Color(0.55f, 0.9f, 1f, 1f),
                    4.8f);
            }

            PresentSnapshots();
            CombatantView humanView = FindView(_humanId);
            if (humanView != null)
            {
                Transform cameraTarget = mode == GameModeId.HeroSiege &&
                    _selectedSiegeVariant == SiegeVariantId.FourLanes &&
                    _arenaContentLoader != null &&
                    _arenaContentLoader.CurrentPresentation != null
                        ? _arenaContentLoader.CurrentPresentation.transform
                        : humanView.transform;
                _cameraRig?.SetFollowTarget(cameraTarget, true);
                if (_targeting != null)
                {
                    _targeting.SetContext(humanView.AimPoint, _gameCamera, 1);
                    _targeting.SetRelationFilter(TargetRelationFilter.Enemy);
                    _targeting.RefreshNow();
                }
            }

            RefreshTargetSelectionVisual();
            RefreshFighterHud();
            RefreshHud();
        }

        private void SetupArena()
        {
            ClassId allyClass = _selectedClass == ClassId.Healer
                ? ClassId.Hunter
                : ClassId.Healer;
            ClassId enemyOne = _selectedClass == ClassId.Rogue
                ? ClassId.Mage
                : ClassId.Rogue;
            ClassId enemyTwo = _selectedClass == ClassId.Hunter
                ? ClassId.Mage
                : ClassId.Hunter;

            AddCombatant(
                0,
                1,
                1,
                _selectedClass,
                _selectedSpecialization,
                new Float3(
                    -ArenaLayout.TeamSpawnOffset,
                    0f,
                    -ArenaLayout.PairSpawnOffset),
                true,
                110f);
            AddCombatant(
                1,
                2,
                1,
                allyClass,
                SpecializationCatalog.GetDefault(allyClass),
                new Float3(
                    -ArenaLayout.TeamSpawnOffset,
                    0f,
                    ArenaLayout.PairSpawnOffset),
                false,
                110f);
            AddCombatant(
                2,
                3,
                2,
                enemyOne,
                GetOpponentSpecialization(enemyOne),
                new Float3(
                    ArenaLayout.TeamSpawnOffset,
                    0f,
                    -ArenaLayout.PairSpawnOffset),
                false,
                110f);
            AddCombatant(
                3,
                4,
                2,
                enemyTwo,
                SpecializationCatalog.GetDefault(enemyTwo),
                new Float3(
                    ArenaLayout.TeamSpawnOffset,
                    0f,
                    ArenaLayout.PairSpawnOffset),
                false,
                110f);
        }

        private void SetupBoss()
        {
            ClassId allyOne;
            ClassId allyTwo;
            switch (_selectedClass)
            {
                case ClassId.Healer:
                    allyOne = ClassId.Rogue;
                    allyTwo = ClassId.Mage;
                    break;
                case ClassId.Rogue:
                    allyOne = ClassId.Healer;
                    allyTwo = ClassId.Mage;
                    break;
                case ClassId.Mage:
                    allyOne = ClassId.Healer;
                    allyTwo = ClassId.Hunter;
                    break;
                case ClassId.Hunter:
                    allyOne = ClassId.Healer;
                    allyTwo = ClassId.Rogue;
                    break;
                default:
                    allyOne = ClassId.Healer;
                    allyTwo = ClassId.Rogue;
                    break;
            }

            AddCombatant(
                0,
                1,
                1,
                _selectedClass,
                _selectedSpecialization,
                new Float3(-3.5f, 0f, -7f),
                true,
                120f);
            AddCombatant(
                1,
                2,
                1,
                allyOne,
                SpecializationCatalog.GetDefault(allyOne),
                new Float3(0f, 0f, -8f),
                false,
                120f);
            AddCombatant(
                2,
                3,
                1,
                allyTwo,
                SpecializationCatalog.GetDefault(allyTwo),
                new Float3(3.5f, 0f, -7f),
                false,
                120f);
            AddCombatant(
                3,
                4,
                2,
                ClassId.Boss,
                SpecializationId.Boss,
                new Float3(0f, 0f, 3.5f),
                false,
                480f);
        }

        private void SetupLeveling()
        {
            AddCombatant(
                0,
                LevelingRunController.PlayerParticipantValue,
                LocalTeamValue,
                _selectedClass,
                _selectedSpecialization,
                new Float3(-182f, 0f, ArenaCollisionMap.LevelingCenterZ),
                true,
                110f);
        }

        private void SetupHeroSiege()
        {
            if (_siegeRun == null || _simulation == null ||
                !_activeRunParty.IsValid ||
                _activeRunParty.Count != _siegeRun.Variant.Party.Count)
            {
                throw new InvalidOperationException(
                    "Hero Siege requires one persistent simulation and one defender per authored lane.");
            }

            for (int heroIndex = 0;
                 heroIndex < _activeRunParty.Count;
                 heroIndex++)
            {
                RunPartyMemberDefinition hero =
                    _activeRunParty.GetMember(heroIndex);
                SiegeLaneId lane = _siegeRun.GetDefenderLane(heroIndex);
                AddCombatant(
                    heroIndex,
                    hero.Participant.Value,
                    hero.Team.Value,
                    hero.Class,
                    hero.Specialization,
                    _siegeRun.Variant.GetDefenderAnchor(lane),
                    hero.IsHumanControlled,
                    hero.MaximumHealth,
                    hero.ApplyClassOpeningState);

                if (!hero.IsHumanControlled)
                {
                    _botBrains[heroIndex] = null;
                    _siegeDefenderBrains[heroIndex] =
                        new DeterministicSiegeDefenderBrain(
                            lane,
                            3203 + (hero.Participant.Value * 7919));
                }
            }

            _focusedTarget = default;
            _fighterHudBound = false;
        }

        private void SetupRunEncounter(EncounterDefinition encounter)
        {
            if (!encounter.IsValid)
            {
                throw new ArgumentException(
                    "A sequential PvE encounter requires a valid definition.",
                    nameof(encounter));
            }

            if (!_activeRunParty.IsValid)
            {
                throw new InvalidOperationException(
                    "A sequential PvE encounter requires an Application-owned party composition.");
            }

            _rules = encounter.Rules;
            _simulation = new CombatSimulation(_rules);
            _humanSequence = 0;
            _snapshotCount = 0;
            _focusedTarget = default;
            _spectatorTarget = default;
            _fighterHudBound = false;
            _deathRecapBuilder.Reset(_rules.TickRate);
            Array.Clear(_botBrains, 0, _botBrains.Length);
            Array.Clear(_levelingMobBrains, 0, _levelingMobBrains.Length);
            Array.Clear(
                _nextHazardFeedbackTicks,
                0,
                _nextHazardFeedbackTicks.Length);
            DeactivateCombatants();

            for (int heroIndex = 0;
                 heroIndex < _activeRunParty.Count;
                 heroIndex++)
            {
                RunPartyMemberDefinition hero =
                    _activeRunParty.GetMember(heroIndex);
                AddCombatant(
                    heroIndex,
                    hero.Participant.Value,
                    hero.Team.Value,
                    hero.Class,
                    hero.Specialization,
                    encounter.GetPartySpawnPosition(heroIndex),
                    hero.IsHumanControlled,
                    hero.MaximumHealth,
                    hero.ApplyClassOpeningState);
            }

            for (int spawnIndex = 0;
                 spawnIndex < encounter.SpawnCount;
                spawnIndex++)
            {
                RunSpawnDefinition spawn = encounter.GetSpawn(spawnIndex);
                AddCombatant(
                    RunPartyDefinition.MaximumMemberCount + spawnIndex,
                    spawn.Participant.Value,
                    spawn.Team.Value,
                    spawn.Class,
                    spawn.Specialization,
                    spawn.Position,
                    false,
                    spawn.MaximumHealth,
                    spawn.ApplyClassOpeningState);
                int viewIndex =
                    RunPartyDefinition.MaximumMemberCount + spawnIndex;
                if (viewIndex < _combatantViews.Length)
                {
                    _combatantViews[viewIndex]?.ConfigureRunEnemyTier(
                        spawn.Tier);
                }
            }

            _focusedTarget = default;
            _flow?.ResumePlaying();
            _hud?.SetAbilityControlsInteractable(true);
            _targeting?.ClearSelection();
            _offscreenThreats?.Clear();
            SetEnvironment(_selectedMode);
            _cameraRig?.SetGameplayComposition(_selectedMode, true);
            CombatantView humanView = FindView(_humanId);
            if (humanView != null)
            {
                _cameraRig?.SetFollowTarget(humanView.transform, true);
                if (_targeting != null)
                {
                    _targeting.SetContext(
                        humanView.AimPoint,
                        _gameCamera,
                        LocalTeamValue);
                    _targeting.SetRelationFilter(TargetRelationFilter.Enemy);
                    _targeting.RefreshNow();
                }
            }
        }

        private void TryActivateLevelingPack()
        {
            if (_selectedMode != GameModeId.Leveling ||
                _levelingRun == null ||
                !_simulation.TryGetSnapshot(
                    _humanId,
                    out CombatantSnapshot player) ||
                !_levelingRun.TryActivateNextPack(
                    _simulation.CurrentTick,
                    player.Position.X,
                    out LevelingPackDefinition pack))
            {
                return;
            }

            for (int viewIndex = 1; viewIndex < _combatantViews.Length; viewIndex++)
            {
                _botBrains[viewIndex] = null;
                _levelingMobBrains[viewIndex] = null;
                if (_combatantViews[viewIndex] != null)
                {
                    _combatantViews[viewIndex].gameObject.SetActive(false);
                }
            }

            for (int mobIndex = 0; mobIndex < pack.MobCount; mobIndex++)
            {
                int viewIndex = mobIndex + 1;
                LevelingMobDefinition mob = pack.GetMob(mobIndex);
                LevelingMobArchetypeDefinition archetype =
                    LevelingMobArchetypeCatalog.Get(mob.Archetype);
                AddCombatant(
                    viewIndex,
                    mob.Participant.Value,
                    2,
                    archetype.Class,
                    archetype.Specialization,
                    GetLevelingSpawnPosition(pack, mobIndex),
                    false,
                    mob.BaseMaximumHealth,
                    false);
                if (mob.Level > LevelProgressionCurve.MinimumLevel &&
                    !_simulation.ApplyProgressionLevel(
                        mob.Participant,
                        mob.Level))
                {
                    throw new InvalidOperationException(
                        "Could not apply authored level " + mob.Level +
                        " to leveling mob " + mob.Participant.Value + ".");
                }

                _botBrains[viewIndex] = null;
                _levelingMobBrains[viewIndex] =
                    new DeterministicLevelingMobBrain(mob.Archetype);
                if (_combatantViews[viewIndex] != null)
                {
                    _combatantViews[viewIndex].transform.localScale =
                        Vector3.one * archetype.PresentationScale;
                    _combatantViews[viewIndex].ConfigureLevelingArchetype(
                        mob.Archetype);
                }
            }

            // The opening focus lesson keeps this empty until its required tap.
            // Later packs resolve the closest mob from authoritative snapshots.
            _focusedTarget = default;
            _targeting?.ClearSelection();
            _fighterHudBound = false;
            _hud?.ShowCombatNotice(
                "PACK " + pack.Number + " / " + _levelingRun.Zone.PackCount,
                new Color(0.95f, 0.69f, 0.25f, 1f),
                1.15f);
        }

        private static Float3 GetLevelingSpawnPosition(
            LevelingPackDefinition pack,
            int mobIndex)
        {
            float z = mobIndex == 0
                ? ArenaCollisionMap.LevelingCenterZ - 2.15f
                : mobIndex == 1
                    ? ArenaCollisionMap.LevelingCenterZ + 2.15f
                    : ArenaCollisionMap.LevelingCenterZ;
            if (pack.Index == 4)
            {
                z = ArenaCollisionMap.LevelingCenterZ;
            }

            return new Float3(pack.EncounterX, 0f, z);
        }

        private void AddCombatant(
            int viewIndex,
            int participantValue,
            int teamValue,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            bool isHuman,
            float health,
            bool applyClassOpeningState = true)
        {
            ParticipantId participant = new ParticipantId(participantValue);
            TeamId team = new TeamId(teamValue);
            if (!_simulation.AddCombatant(
                    participant,
                    team,
                    classId,
                    specialization,
                    position,
                    health,
                    applyClassOpeningState))
            {
                throw new InvalidOperationException("Could not add authored combatant " + participantValue + ".");
            }

            if (viewIndex >= 0 && viewIndex < _combatantViews.Length && _combatantViews[viewIndex] != null)
            {
                _combatantViews[viewIndex].transform.localScale = Vector3.one;
                _combatantViews[viewIndex].Initialize(
                    participant,
                    team,
                    classId,
                    isHuman,
                    ToVector3(position));
            }

            if (!isHuman && _selectedMode != GameModeId.PveLeveling)
            {
                _botBrains[viewIndex] = new DeterministicBotBrain(1301 + (participantValue * 7919));
            }
        }

        private void ProcessInputFrame()
        {
            if (_inputSource == null)
            {
                return;
            }

            while (_inputSource.TryDequeueAbilityGesture(out AbilityGesture gesture))
            {
                ProcessAbilityGesture(gesture);
            }
        }

        private void ProcessAbilityGesture(AbilityGesture gesture)
        {
            if (_flow.Phase != SessionPhase.Playing || gesture.AbilityId == AbilityId.None)
            {
                return;
            }

            switch (gesture.Phase)
            {
                case AbilityGesturePhase.Tapped:
                    ClearPendingTrainingFlick();
                    SubmitHumanCast(ResolveTargetedCast(gesture.AbilityId));
                    break;
                case AbilityGesturePhase.Flicked:
                {
                    ClearPendingTrainingFlick();
                    AbilitySlotDirection flickDirection = ResolveFlickDirection(
                        gesture.Displacement);
                    if (SubmitHumanCast(ResolveTargetedCast(gesture.AbilityId)) &&
                        _training != null &&
                        _training.CurrentStep == GuidedTrainingStep.FlickMastery)
                    {
                        _pendingTrainingFlickAbility = gesture.AbilityId;
                        _pendingTrainingFlickDirection = flickDirection;
                    }
                    break;
                }
                case AbilityGesturePhase.Pressed:
                case AbilityGesturePhase.Canceled:
                    break;
            }
        }

        private ResolvedCast ResolveTargetedCast(AbilityId ability)
        {
            if (_simulation == null ||
                !_simulation.TryGetSnapshot(_humanId, out CombatantSnapshot human) ||
                !human.IsAlive || !AbilityCatalog.TryGet(ability, out AbilityDefinition definition))
            {
                return default;
            }

            ParticipantId target = default;
            Float3 targetPoint = human.Position;

            AbilityTargetRule targeting = definition.Targeting;
            switch (targeting.Shape)
            {
                case AbilityTargetShape.Unit when
                    targeting.AllowedRelations == AbilityTargetRelation.Self:
                    target = human.Id;
                    break;

                case AbilityTargetShape.Unit:
                    target = ResolveSelectedTarget(human, definition);

                    if (!target.IsValid ||
                        !_simulation.TryGetSnapshot(target, out CombatantSnapshot targetSnapshot))
                    {
                        return default;
                    }

                    targetPoint = targetSnapshot.Position;
                    break;

                case AbilityTargetShape.CasterArea:
                    targetPoint = human.Position;
                    break;

                case AbilityTargetShape.TargetArea:
                    target = ResolveSelectedTarget(human, definition);
                    if (target.IsValid &&
                        _simulation.TryGetSnapshot(
                            target,
                            out CombatantSnapshot areaTargetSnapshot))
                    {
                        targetPoint = areaTargetSnapshot.Position;
                        break;
                    }

                    goto default;

                case AbilityTargetShape.Point when
                    targeting.SmartTargetPolicy != AbilitySmartTargetPolicy.AimPoint:
                    target = ResolveSelectedTarget(human, definition);
                    if (target.IsValid &&
                        _simulation.TryGetSnapshot(
                            target,
                            out CombatantSnapshot pointTargetSnapshot))
                    {
                        targetPoint = pointTargetSnapshot.Position;
                        break;
                    }

                    goto default;

                default:
                    Float3 chosenDirection = ResolveMovementDirection(human);
                    targetPoint = human.Position +
                        (chosenDirection * Mathf.Max(2f, definition.Range));

                    targetPoint = ClampAimPoint(human.Position, targetPoint, definition.Range);
                    break;
            }

            Float3 delta = targetPoint - human.Position;
            Float3 direction = delta.PlanarLengthSquared > 0.0001f
                ? delta.PlanarNormalized
                : human.Facing.PlanarNormalized;
            return new ResolvedCast(ability, target, targetPoint, direction);
        }

        private ParticipantId ResolveSelectedTarget(
            CombatantSnapshot human,
            AbilityDefinition definition)
        {
            AbilityTargetRule targeting = definition.Targeting;
            if (targeting.AllowedRelations == AbilityTargetRelation.Self)
            {
                return human.Id;
            }

            if (_focusedTarget.IsValid &&
                _simulation.TryGetSnapshot(_focusedTarget, out CombatantSnapshot selected) &&
                HumanTargetSelector.IsSelectable(human, selected))
            {
                AbilityTargetRelation relation = GetRelation(human, selected);
                if (targeting.Allows(relation))
                {
                    return selected.Id;
                }
            }

            return default;
        }

        public bool TryResolveFocusedTargetForAbility(
            AbilityId ability,
            out ParticipantId target)
        {
            target = default;
            if (_simulation == null ||
                !_simulation.TryGetSnapshot(_humanId, out CombatantSnapshot human) ||
                !AbilityCatalog.TryGet(ability, out AbilityDefinition definition))
            {
                return false;
            }

            target = ResolveSelectedTarget(human, definition);
            return target.IsValid;
        }

        public bool TryEvaluateFocusedAbility(
            AbilityId ability,
            out ParticipantId target,
            out AbilityReadinessState state,
            out string blockedReason)
        {
            target = default;
            state = AbilityReadinessState.Unavailable;
            blockedReason = "BLOCKED";
            if (_simulation == null ||
                !_simulation.TryGetSnapshot(_humanId, out CombatantSnapshot human) ||
                !AbilityCatalog.TryGet(ability, out _))
            {
                return false;
            }

            ResolvedCast cast = ResolveTargetedCast(ability);
            target = cast.Target;
            EvaluateHumanAbilityReadiness(
                ability,
                human,
                out state,
                out blockedReason);
            return cast.IsValid;
        }

        public bool TryGetLastAcceptedHumanCast(out CombatEvent combatEvent)
        {
            combatEvent = _lastAcceptedHumanCast;
            return _acceptedHumanCastCount > 0;
        }

        private Float3 ResolveMovementDirection(CombatantSnapshot human)
        {
            if (_inputSource != null && _gameCamera != null)
            {
                Vector3 movement = _inputSource.CurrentFrame.ToCameraRelativeMovement(
                    _gameCamera.transform);
                if (movement.sqrMagnitude > 0.001f)
                {
                    return ToFloat3(movement.normalized);
                }
            }

            return human.Facing.PlanarNormalized;
        }

        private static Float3 ClampAimPoint(Float3 origin, Float3 point, float range)
        {
            if (range <= 0f)
            {
                return point;
            }

            Float3 delta = point - origin;
            float length = delta.PlanarLength;
            return length > range && length > 0.0001f
                ? origin + (delta * (range / length))
                : point;
        }

        private bool SubmitHumanCast(ResolvedCast cast)
        {
            if (!cast.IsValid)
            {
                _hud?.ShowCombatNotice(
                    "SELECT TARGET",
                    new Color(1f, 0.34f, 0.25f, 1f));
                return false;
            }

            if (!_simulation.TryGetSnapshot(_humanId, out CombatantSnapshot human) || !human.IsAlive)
            {
                return false;
            }

            Vector3 move = _inputSource.CurrentFrame.ToCameraRelativeMovement(_gameCamera.transform);
            CombatCommand command = CombatCommand.Cast(
                _humanId,
                _humanSequence + 1,
                ToFloat3(move),
                cast.Direction,
                cast.Ability,
                cast.Target,
                cast.Point,
                cast.Direction);
            if (!_simulation.CanStartAbility(command, out string reason))
            {
                _hud?.ShowCombatNotice(
                    DescribeCastRejection(reason),
                    ResolveCastRejectionColor(reason));
                return false;
            }

            _humanSequence++;
            return _simulation.SubmitCommand(command);
        }

        private void ClearPendingTrainingFlick()
        {
            _pendingTrainingFlickAbility = AbilityId.None;
            _pendingTrainingFlickDirection = AbilitySlotDirection.None;
        }

        private void TickSession()
        {
            SessionPhase previousPhase = _flow.Phase;
            _flow.Tick();
            if (previousPhase == SessionPhase.Countdown)
            {
                if (_flow.Phase == SessionPhase.Playing)
                {
                    _accumulator = 0f;
                }

                return;
            }

            if (_flow.Phase != SessionPhase.Playing &&
                _flow.Phase != SessionPhase.Spectating)
            {
                return;
            }

            RetireDefeatedSiegeHostiles();
            AdvanceGrindingCamps();
            AdvanceHeroSiege();
            TryCrossRunBoundary();

            if (_flow.Phase == SessionPhase.Playing)
            {
                TryActivateLevelingPack();
                SubmitHumanMovement();
            }

            _snapshotCount = _simulation.CopySnapshots(_snapshots);
            ResolveHumanFocusedTarget();
            CombatantSnapshot levelingPlayer = default;
            bool hasLevelingPlayer = (_selectedMode == GameModeId.Leveling ||
                _selectedMode == GameModeId.PveLeveling) &&
                _simulation.TryGetSnapshot(
                    _humanId,
                    out levelingPlayer);
            ArenaTeamIntent teamOneIntent = default;
            ArenaTeamIntent teamTwoIntent = default;
            bool coordinateArenaBots = _selectedMode == GameModeId.Arena;
            if (coordinateArenaBots)
            {
                teamOneIntent = _arenaTeamCoordinator.Build(
                    new TeamId(1),
                    _snapshots,
                    _snapshotCount);
                teamTwoIntent = _arenaTeamCoordinator.Build(
                    new TeamId(2),
                    _snapshots,
                    _snapshotCount);
            }

            for (int index = 0; index < _snapshotCount; index++)
            {
                CombatantSnapshot snapshot = _snapshots[index];
                int viewIndex = FindViewIndex(snapshot.Id);
                if (viewIndex >= 0 &&
                    _siegeEnemyBrains[viewIndex] != null)
                {
                    if (_siegeEnemyBrains[viewIndex].TryBuildCommand(
                            _simulation,
                            snapshot,
                            _snapshots,
                            _snapshotCount,
                            out CombatCommand siegeEnemyCommand))
                    {
                        _simulation.SubmitCommand(siegeEnemyCommand);
                    }

                    continue;
                }

                if (viewIndex >= 0 &&
                    _siegeDefenderBrains[viewIndex] != null)
                {
                    if (_siegeDefenderBrains[viewIndex].TryBuildCommand(
                            _simulation,
                            snapshot,
                            _siegeRun,
                            _snapshots,
                            _snapshotCount,
                            out CombatCommand siegeDefenderCommand))
                    {
                        _simulation.SubmitCommand(siegeDefenderCommand);
                    }

                    continue;
                }

                if (viewIndex < 0 || _botBrains[viewIndex] == null)
                {
                    if (_selectedMode == GameModeId.PveLeveling && hasLevelingPlayer &&
                        viewIndex >= 0 && _levelingMobBrains[viewIndex] != null)
                    {
                        if (_levelingMobBrains[viewIndex].TryBuildLeashedCommand(
                            _simulation, snapshot, levelingPlayer,
                            _grindingRun.ActiveCampCenter, 45f, out CombatCommand grindingCommand))
                        {
                            _simulation.SubmitCommand(grindingCommand);
                        }
                        continue;
                    }
                    if (!hasLevelingPlayer ||
                        viewIndex < 0 ||
                        _levelingMobBrains[viewIndex] == null ||
                        ShouldHoldFirstTrainingPack() ||
                        !_levelingMobBrains[viewIndex].TryBuildCommand(
                            _simulation,
                            snapshot,
                            levelingPlayer,
                            out CombatCommand levelingCommand))
                    {
                        continue;
                    }

                    _simulation.SubmitCommand(levelingCommand);
                    continue;
                }

                bool hasCommand = coordinateArenaBots
                    ? _botBrains[viewIndex].TryBuildCommand(
                        _simulation,
                        snapshot,
                        _snapshots,
                        _snapshotCount,
                        snapshot.Team.Value == 1
                            ? teamOneIntent
                            : teamTwoIntent,
                        out CombatCommand command)
                    : _botBrains[viewIndex].TryBuildCommand(
                        _simulation,
                        snapshot,
                        _snapshots,
                        _snapshotCount,
                        out command);
                if (hasCommand)
                {
                    _simulation.SubmitCommand(command);
                }
            }

            _simulation.Tick();
            PresentCombatEvents();
            ObserveAuthoritativeHazardRecovery();
            EnsureTrainingPracticeTarget();
            PresentSnapshots();
            ApplySiegeObjectivePressure();
            UpdateSpectatorState();
            EvaluateResult();
        }

        private bool ShouldHoldFirstTrainingPack() =>
            _selectedMode == GameModeId.Leveling &&
            _levelingRun != null &&
            _levelingRun.ActivePackIndex == 0 &&
            _training != null &&
            (_training.CurrentStep == GuidedTrainingStep.Movement ||
             _training.CurrentStep == GuidedTrainingStep.Targeting);

        private void AdvanceHeroSiege()
        {
            if (_siegeRun == null || _simulation == null ||
                _siegeRun.State.IsTerminal)
            {
                return;
            }

            SiegeAdvanceObservation observation =
                _siegeRun.Advance(_simulation.CurrentTick);
            if (observation.WaveStarted)
            {
                SiegeRunState state = _siegeRun.State;
                _hud?.ShowCombatNotice(
                    observation.StartedWaveCount > 1
                        ? observation.StartedWaveCount +
                          " REINFORCEMENT WAVES ARRIVED"
                        : "WAVE " + state.CurrentWaveNumber + " / " +
                          HeroSiegeRunCatalog.WaveCount,
                    new Color(1f, 0.62f, 0.2f, 1f),
                    1.5f);
            }

            while (_simulation.CombatantCount < CombatSimulation.MaxCombatants &&
                TryFindFreeSiegeHostileView(out int viewIndex) &&
                _siegeRun.TryDequeueSpawn(out SiegeSpawnDefinition spawn))
            {
                AddSiegeHostile(viewIndex, spawn);
            }
        }

        private bool TryFindFreeSiegeHostileView(out int viewIndex)
        {
            for (int index = SiegeDefenderViewCapacity;
                 index < _combatantViews.Length;
                 index++)
            {
                CombatantView view = _combatantViews[index];
                if (view != null && !view.gameObject.activeSelf)
                {
                    viewIndex = index;
                    return true;
                }
            }

            viewIndex = -1;
            return false;
        }

        private void AddSiegeHostile(
            int viewIndex,
            SiegeSpawnDefinition spawn)
        {
            RunSpawnDefinition combatant = spawn.ToRunSpawnDefinition();
            AddCombatant(
                viewIndex,
                combatant.Participant.Value,
                combatant.Team.Value,
                combatant.Class,
                combatant.Specialization,
                combatant.Position,
                false,
                combatant.MaximumHealth,
                combatant.ApplyClassOpeningState);
            if (!_simulation.TrySetRunCombatModifiers(
                    spawn.Participant,
                    spawn.DamageMultiplierPermille,
                    spawn.MovementSpeedMultiplierPermille))
            {
                throw new InvalidOperationException(
                    "Could not apply deterministic Siege scaling to " +
                    spawn.Participant.Value + ".");
            }

            _botBrains[viewIndex] = null;
            _levelingMobBrains[viewIndex] = null;
            _siegeDefenderBrains[viewIndex] = null;
            _siegeEnemyBrains[viewIndex] =
                new DeterministicSiegeEnemyBrain(
                    spawn.Archetype.Id,
                    _siegeRun.Variant.GetDefender(spawn.Lane),
                    spawn.Lane);
            _combatantViews[viewIndex]?.ConfigureSiegeArchetype(
                spawn.Archetype.Id);
            _fighterHudBound = false;
            _targeting?.RefreshNow();
        }

        private void QueueSiegeRetirement(ParticipantId participant)
        {
            if (!participant.IsValid)
            {
                return;
            }

            for (int index = 0; index < _siegeRetirementCount; index++)
            {
                if (_siegeRetirements[index] == participant)
                {
                    return;
                }
            }

            if (_siegeRetirementCount >= _siegeRetirements.Length)
            {
                throw new InvalidOperationException(
                    "Siege retirement queue exceeded its bounded hostile capacity.");
            }

            _siegeRetirements[_siegeRetirementCount++] = participant;
        }

        private void RetireDefeatedSiegeHostiles()
        {
            if (_siegeRetirementCount <= 0 || _simulation == null)
            {
                return;
            }

            for (int index = 0; index < _siegeRetirementCount; index++)
            {
                ParticipantId participant = _siegeRetirements[index];
                int viewIndex = FindViewIndex(participant);
                if (!_simulation.TryRemoveDefeatedCombatant(participant))
                {
                    throw new InvalidOperationException(
                        "Could not retire defeated Siege hostile " +
                        participant.Value + ".");
                }

                if (viewIndex >= 0)
                {
                    _botBrains[viewIndex] = null;
                    _levelingMobBrains[viewIndex] = null;
                    _siegeEnemyBrains[viewIndex] = null;
                    _siegeDefenderBrains[viewIndex] = null;
                    _combatantViews[viewIndex]?.ReleaseToPool();
                }

                if (_focusedTarget == participant)
                {
                    _focusedTarget = default;
                    _targeting?.ClearSelection();
                }

                _siegeRetirements[index] = default;
            }

            _siegeRetirementCount = 0;
            _fighterHudBound = false;
        }

        private void TryCrossRunBoundary()
        {
            int runTick = _flow.ViewState.ElapsedTicks;
            if (_dungeonRun == null)
            {
                return;
            }

            EncounterDefinition floor = default;
            bool crossed = _dungeonRun.State.Status == RunStatus.AwaitingAdvance
                ? _dungeonRun.TryAdvanceToNextFloor(runTick, out floor)
                : _dungeonRun.State.Status == RunStatus.AwaitingRetry &&
                  _dungeonRun.TryRetryCurrentFloor(runTick, out floor);
            if (!crossed)
            {
                return;
            }

            bool retry = _dungeonRun.State.CurrentRetryCount > 0;
            SetupRunEncounter(floor);
            _hud?.ShowCombatNotice(
                (retry ? "RESTART  •  " : string.Empty) +
                "FLOOR " + _dungeonRun.State.CurrentEncounterNumber + " / " +
                _dungeonRun.State.EncounterCount,
                retry
                    ? new Color(1f, 0.69f, 0.28f, 1f)
                    : new Color(0.32f, 0.92f, 0.69f, 1f),
                1.5f);
        }

        private void ApplySiegeObjectivePressure()
        {
            if (_siegeRun == null ||
                _siegeRun.State.IsTerminal)
            {
                return;
            }

            SiegeObjectivePressureObservation observation =
                _siegeRun.ApplyObjectivePressure(
                    _simulation.CurrentTick,
                    _snapshots,
                    _snapshotCount,
                    _rules.TickRate);
            if (observation.RunFailed)
            {
                _hud?.ShowCombatNotice(
                    "THE STRONGHOLD HAS FALLEN",
                    new Color(1f, 0.3f, 0.18f, 1f),
                    1.8f);
            }
        }

        private void UpdateSpectatorState()
        {
            if (_selectedMode == GameModeId.Leveling || _selectedMode == GameModeId.PveLeveling)
            {
                return;
            }

            if (_dungeonRun != null &&
                _dungeonRun.State.Status == RunStatus.AwaitingRetry)
            {
                return;
            }

            if ((_flow.Phase != SessionPhase.Playing &&
                 _flow.Phase != SessionPhase.Spectating) ||
                !_simulation.TryGetSnapshot(_humanId, out CombatantSnapshot human) ||
                human.IsAlive)
            {
                return;
            }

            if (_flow.Phase == SessionPhase.Playing)
            {
                _flow.EnterSpectating();
                _hud?.SetAbilityControlsInteractable(false, "SPECTATING");
                _inputSource?.ClearTransientInput();
            }

            ParticipantId nextTarget = SpectatorTargetSelector.Select(
                _snapshots,
                _snapshotCount,
                _humanId,
                new TeamId(LocalTeamValue));
            if (!nextTarget.IsValid || nextTarget == _spectatorTarget)
            {
                return;
            }

            CombatantView spectatorView = FindView(nextTarget);
            if (spectatorView == null)
            {
                return;
            }

            _spectatorTarget = nextTarget;
            _cameraRig?.SetFollowTarget(spectatorView.transform, true);
            if (_targeting != null)
            {
                _targeting.SetContext(
                    spectatorView.AimPoint,
                    _gameCamera,
                    LocalTeamValue);
                _targeting.RefreshNow();
            }
        }

        private void SubmitHumanMovement()
        {
            if (_inputSource == null ||
                !_simulation.TryGetSnapshot(_humanId, out CombatantSnapshot human) ||
                !human.IsAlive)
            {
                return;
            }

            Vector3 movement = _inputSource.CurrentFrame.ToCameraRelativeMovement(_gameCamera.transform);
            if (movement.sqrMagnitude > 0.001f)
            {
                ObserveTraining(GuidedTrainingObservation.Moved());
            }
            Float3 facing = movement.sqrMagnitude > 0.001f
                ? ToFloat3(movement.normalized)
                : human.Facing;
            _humanSequence++;
            _simulation.SubmitCommand(CombatCommand.Move(
                _humanId,
                _humanSequence,
                ToFloat3(movement),
                facing));
        }

        private void PresentCombatEvents()
        {
            for (int index = 0; index < _simulation.EventCount; index++)
            {
                CombatEvent combatEvent = _simulation.GetEvent(index);
                _deathRecapBuilder.Observe(combatEvent);
                ObserveLevelingCombatEvent(combatEvent);
                ObserveGrindingCombatEvent(combatEvent);
                ObserveRunCombatEvent(combatEvent);
                if (combatEvent.Actor == _humanId &&
                    combatEvent.Kind == CombatEventKind.CastCompleted)
                {
                    ObserveTraining(
                        GuidedTrainingObservation.ResolvedCombatAction(
                            combatEvent.Ability));
                    if (combatEvent.Ability == _pendingTrainingFlickAbility)
                    {
                        ObserveTraining(
                            GuidedTrainingObservation.ResolvedFlickAction(
                                combatEvent.Ability,
                                _pendingTrainingFlickDirection));
                        ClearPendingTrainingFlick();
                    }
                }

                if (combatEvent.Target == _humanId &&
                    (combatEvent.Kind == CombatEventKind.HazardDamage ||
                     combatEvent.Kind == CombatEventKind.Knockback))
                {
                    if (_simulation.TryGetSnapshot(
                            _humanId,
                            out CombatantSnapshot human))
                    {
                        ObserveTraining(
                            GuidedTrainingObservation.EnteredHazard(
                                human.Position));
                    }
                }
                CombatantView actorView = FindView(combatEvent.Actor);
                CombatantView targetView = FindView(combatEvent.Target);
                bool showHazardFeedback = combatEvent.Kind != CombatEventKind.HazardDamage ||
                    ShouldPresentHazardFeedback(combatEvent.Target);
                if (combatEvent.Kind == CombatEventKind.Damage ||
                    (combatEvent.Kind == CombatEventKind.HazardDamage && showHazardFeedback) ||
                    combatEvent.Kind == CombatEventKind.Knockback)
                {
                    targetView?.Flash();
                }

                if (combatEvent.Kind == CombatEventKind.CastAccepted)
                {
                    if (combatEvent.Actor == _humanId)
                    {
                        _lastAcceptedHumanCast = combatEvent;
                        _acceptedHumanCastCount++;
                    }

                    AbilityDefinition definition = AbilityCatalog.Get(combatEvent.Ability);
                    if (definition.CastProfile.ActivationType != AbilityActivationType.Instant)
                    {
                        actorView?.PlayCastAnimation(combatEvent.Ability);
                        _combatEffects?.PresentCastStarted(
                            combatEvent.Ability,
                            actorView != null ? actorView.EffectOrigin : null);
                    }
                }

                if (combatEvent.Kind == CombatEventKind.CastRejected &&
                    combatEvent.Actor == _humanId)
                {
                    if (combatEvent.Ability == _pendingTrainingFlickAbility)
                    {
                        ClearPendingTrainingFlick();
                    }
                    _combatEffects?.PresentUiRejected();
                    _hud?.ShowCombatNotice(
                        DescribeCastRejection(combatEvent.Reason),
                        ResolveCastRejectionColor(combatEvent.Reason));
                }

                if (combatEvent.Kind == CombatEventKind.Interrupted &&
                    combatEvent.Target == _humanId &&
                    combatEvent.Ability == _pendingTrainingFlickAbility)
                {
                    ClearPendingTrainingFlick();
                }

                if (combatEvent.Kind == CombatEventKind.CastCompleted)
                {
                    AbilityDefinition definition = AbilityCatalog.Get(combatEvent.Ability);
                    AbilityActivationType activationType =
                        definition.CastProfile.ActivationType;
                    if (activationType == AbilityActivationType.Instant)
                    {
                        actorView?.PlayCastAnimation(combatEvent.Ability);
                    }
                    else
                    {
                        actorView?.PlayCastReleaseAnimation(combatEvent.Ability);
                    }

                    bool usesResolvedPoint =
                        definition.Targeting.Shape == AbilityTargetShape.Point ||
                        definition.Targeting.Shape == AbilityTargetShape.TargetArea;
                    Vector3 resolvedPoint = usesResolvedPoint
                        ? ToVector3(combatEvent.Vector)
                        : actorView != null
                            ? actorView.transform.position
                            : Vector3.zero;
                    Transform presentationTarget = targetView != null
                        ? targetView.AimPoint
                        : null;
                    bool usesCasterGround =
                        definition.Targeting.Shape == AbilityTargetShape.CasterArea;
                    if (!usesResolvedPoint && usesCasterGround && actorView != null)
                    {
                        resolvedPoint = actorView.transform.position;
                    }
                    else if (!usesResolvedPoint && usesCasterGround &&
                             _simulation.TryGetSnapshot(
                                 combatEvent.Actor,
                                 out CombatantSnapshot resolvedActor))
                    {
                        resolvedPoint = ToVector3(resolvedActor.Position);
                    }
                    else if (!usesResolvedPoint && targetView != null)
                    {
                        resolvedPoint = targetView.transform.position;
                    }
                    else if (!usesResolvedPoint && _simulation.TryGetSnapshot(
                                 combatEvent.Target,
                                 out CombatantSnapshot resolvedTarget))
                    {
                        resolvedPoint = ToVector3(resolvedTarget.Position);
                    }

                    if (usesResolvedPoint)
                    {
                        presentationTarget = null;
                    }

                    if (activationType != AbilityActivationType.Channel)
                    {
                        bool connected = IsPlacedAbility(combatEvent.Ability) ||
                            IsDeferredCompanionCommand(combatEvent.Ability) ||
                            DidAbilityConnect(index, combatEvent);
                        _combatEffects?.PresentCastCompleted(
                            combatEvent.Ability,
                            actorView != null ? actorView.EffectOrigin : null,
                            presentationTarget,
                            resolvedPoint);
                        ShowAbilityResolution(
                            combatEvent,
                            actorView,
                            targetView,
                            resolvedPoint,
                            connected);
                        if (!connected)
                        {
                            _combatEffects?.PresentMiss(
                                combatEvent.Ability,
                                presentationTarget,
                                resolvedPoint);
                        }
                    }
                }

                if (combatEvent.Kind == CombatEventKind.Damage ||
                    (combatEvent.Kind == CombatEventKind.HazardDamage && showHazardFeedback))
                {
                    bool channelDamage = combatEvent.Kind == CombatEventKind.Damage &&
                        IsChannelAbility(combatEvent.Ability);
                    if (channelDamage)
                    {
                        actorView?.PlayChannelPulseAnimation(combatEvent.Ability);
                        _combatEffects?.PresentChannelTick(
                            combatEvent.Ability,
                            actorView != null ? actorView.EffectOrigin : null,
                            targetView != null ? targetView.AimPoint : null,
                            targetView != null
                                ? targetView.AimPoint.position
                                : Vector3.zero);
                    }

                    if (combatEvent.Amount > 0.001f)
                    {
                        Transform damageSource = ResolveDamagePresentationSource(
                            combatEvent,
                            actorView);
                        if (combatEvent.Ability == AbilityId.PetAttack &&
                            actorView != null &&
                            actorView.CompanionVisuals != null)
                        {
                            actorView.CompanionVisuals.PlayAttack(
                                targetView != null
                                    ? targetView.transform.position
                                    : damageSource != null
                                        ? damageSource.position + damageSource.forward
                                        : Vector3.zero);
                        }

                        targetView?.PlayHitAnimation(
                            damageSource != null
                                ? damageSource.position
                                : targetView != null
                                    ? targetView.transform.position - targetView.transform.forward
                                    : Vector3.zero,
                            !channelDamage);
                        _combatEffects?.PresentDamage(
                            combatEvent.Ability,
                            damageSource,
                            targetView != null ? targetView.AimPoint : null,
                                combatEvent.Amount);
                        if (_simulation.TryGetSnapshot(
                                combatEvent.Target,
                                out CombatantSnapshot mitigatedTarget) &&
                            mitigatedTarget.HasStatus(StatusId.DamageReduced))
                        {
                            _combatEffects?.PresentMitigated(
                                combatEvent.Ability,
                                targetView != null ? targetView.transform : null,
                                combatEvent.Amount);
                        }
                    }
                    else
                    {
                        _combatEffects?.PresentAbsorbed(
                            combatEvent.Ability,
                            targetView != null ? targetView.transform : null);
                    }

                    if (combatEvent.Target == _humanId)
                    {
                        _cameraRig?.AddCombatImpulse(
                            combatEvent.Kind == CombatEventKind.HazardDamage ? 0.16f : 0.2f);
                    }
                }

                if (combatEvent.Kind == CombatEventKind.Heal)
                {
                    _combatEffects?.PresentHeal(
                        combatEvent.Ability,
                        actorView != null ? actorView.EffectOrigin : null,
                        ResolveHealPresentationTarget(combatEvent, targetView),
                        combatEvent.Amount);
                    if (IsChannelAbility(combatEvent.Ability))
                    {
                        actorView?.PlayChannelPulseAnimation(combatEvent.Ability);
                    }
                }

                if (combatEvent.Kind == CombatEventKind.BarrierApplied)
                {
                    _combatEffects?.PresentBarrier(
                        combatEvent.Ability,
                        targetView != null ? targetView.transform : null,
                        combatEvent.Amount);
                }

                if (combatEvent.Kind == CombatEventKind.Interrupted)
                {
                    bool hardInterrupted = string.IsNullOrEmpty(combatEvent.Reason);
                    targetView?.CancelCastAnimation(
                        hardInterrupted,
                        actorView != null
                            ? actorView.transform.position
                            : default);
                    if (hardInterrupted)
                    {
                        _combatEffects?.PresentInterrupted(
                            combatEvent.Ability,
                            targetView != null ? targetView.transform : null);
                        if (combatEvent.Target == _humanId)
                        {
                            _hud?.ShowCombatNotice(
                                "INTERRUPTED",
                                new Color(1f, 0.26f, 0.62f, 1f),
                                1f);
                        }
                    }
                }

                if (combatEvent.Kind == CombatEventKind.Knockback)
                {
                    targetView?.PlayKnockbackAnimation(ToVector3(combatEvent.Vector));
                    _combatEffects?.PresentKnockback(
                        combatEvent.Ability,
                        actorView != null ? actorView.transform : null,
                        targetView != null ? targetView.transform : null,
                        ToVector3(combatEvent.Vector));
                    if (combatEvent.Target == _humanId)
                    {
                        _cameraRig?.AddCombatImpulse(0.72f);
                    }
                }

                if (combatEvent.Kind == CombatEventKind.StatusApplied)
                {
                    _combatEffects?.PresentStatus(
                        combatEvent.Status,
                        targetView != null ? targetView.transform : null);
                }

                if (combatEvent.Kind == CombatEventKind.StatusRemoved ||
                    combatEvent.Kind == CombatEventKind.AuraRemoved)
                {
                    Transform removalTarget = targetView != null
                        ? targetView.transform
                        : null;
                    bool harmfulAuraRemoved = IsHarmfulAuraRemoval(combatEvent);
                    if ((combatEvent.Ability == AbilityId.Purify ||
                         combatEvent.Ability == AbilityId.DispelMagic ||
                         combatEvent.Ability == AbilityId.CloakOfShadows) &&
                        (combatEvent.Aura == AuraId.None || harmfulAuraRemoved))
                    {
                        _combatEffects?.PresentCleanse(removalTarget);
                    }
                    else if (combatEvent.Ability == AbilityId.TranquilizingShot &&
                        combatEvent.Actor != combatEvent.Target)
                    {
                        _combatEffects?.PresentRemovalFeedback(
                            combatEvent.Ability,
                            removalTarget,
                            "PURGED");
                    }
                    else if (combatEvent.Ability == AbilityId.Spellsteal &&
                        combatEvent.Actor != combatEvent.Target)
                    {
                        _combatEffects?.PresentRemovalFeedback(
                            combatEvent.Ability,
                            removalTarget,
                            "STOLEN");
                    }
                    else if (combatEvent.Ability == AbilityId.Flare &&
                        combatEvent.Aura == AuraId.Stealth)
                    {
                        _combatEffects?.PresentRemovalFeedback(
                            combatEvent.Ability,
                            removalTarget,
                            "REVEALED");
                    }
                    else if (combatEvent.Ability == AbilityId.IceBlock &&
                        harmfulAuraRemoved)
                    {
                        _combatEffects?.PresentRemovalFeedback(
                            combatEvent.Ability,
                            removalTarget,
                            "FREED");
                    }
                }

                if (combatEvent.Kind == CombatEventKind.CastAccepted &&
                    (combatEvent.Ability == AbilityId.BossCleave ||
                     combatEvent.Ability == AbilityId.BossMeteor ||
                     combatEvent.Ability == AbilityId.BossKnockback))
                {
                    ShowBossTelegraph(combatEvent);
                }

                if (combatEvent.Kind == CombatEventKind.Death)
                {
                    _combatEffects?.PresentDeath(
                        targetView != null ? targetView.transform : null);
                    if (combatEvent.Target == _humanId)
                    {
                        _cameraRig?.AddCombatImpulse(1f);
                    }

                    if (_deathRecapBuilder.TryGetRecap(
                            combatEvent.Target,
                            out DeathRecap recap))
                    {
                        string source = recap.CreditedActor.IsValid
                            ? " (" + DescribeParticipant(recap.CreditedActor) + ")"
                            : string.Empty;
                        string recapLine = DescribeParticipant(recap.Victim) + ": " +
                            recap.Chain + source;
                        if (combatEvent.Target == _humanId)
                        {
                            _hasHumanDeathRecap = true;
                            _lastRecap = recapLine + "\n" + recap.Hint;
                            _hud?.ShowCombatNotice(
                                recap.Chain + "\n" + recap.Hint,
                                new Color(1f, 0.42f, 0.24f, 1f),
                                4f);
                        }
                        else if (!_hasHumanDeathRecap)
                        {
                            _lastRecap = recapLine;
                        }
                    }
                }
            }
        }

        private void ObserveAuthoritativeHazardRecovery()
        {
            if (_training == null ||
                _training.CurrentStep != GuidedTrainingStep.HazardRecovery ||
                !_training.Snapshot.HazardEntered ||
                !_simulation.TryGetSnapshot(
                    _humanId,
                    out CombatantSnapshot human))
            {
                return;
            }

            ObserveTraining(
                GuidedTrainingObservation.ObservedHazardRecovery(
                    human.Position,
                    human.IsInLava));
        }

        private void ObserveLevelingCombatEvent(CombatEvent combatEvent)
        {
            if (_selectedMode != GameModeId.Leveling || _levelingRun == null)
            {
                return;
            }

            LevelingCombatObservation observation =
                _levelingRun.ObserveCombatEvent(combatEvent);
            if (!observation.WasHandled)
            {
                return;
            }

            if (observation.AwardedExperience)
            {
                if (observation.ProgressionGrant.DidLevelUp)
                {
                    _simulation.ApplyProgressionLevel(
                        _humanId,
                        observation.ProgressionGrant.CurrentLevel);
                    BindActivePlayerKit();
                    BindDesktopKit(_selectedSpecialization);
                    _hud?.ShowCombatNotice(
                        BuildLevelUpNotice(
                            observation.ProgressionGrant.CurrentLevel),
                        new Color(1f, 0.75f, 0.25f, 1f),
                        1.4f);
                    _combatEffects?.PresentUiConfirm();
                }
                else
                {
                    _hud?.ShowCombatNotice(
                        "+" + observation.ExperienceAwarded + " XP",
                        new Color(0.95f, 0.69f, 0.25f, 1f));
                }
            }

            if (observation.PackCleared)
            {
                ObserveTraining(
                    GuidedTrainingObservation.ClearedPack(
                        _levelingRun.NextPackIndex));
                if (_training != null &&
                    _training.CurrentStep == GuidedTrainingStep.FlickMastery)
                {
                    BindActivePlayerKit();
                    BindDesktopKit(_selectedSpecialization);
                }

                _focusedTarget = default;
                _targeting?.ClearSelection();
                _fighterHudBound = false;
            }
        }

        private void EnsureTrainingPracticeTarget()
        {
            if (_trainingPracticeTargetSpawned ||
                _selectedMode != GameModeId.Leveling ||
                _levelingRun == null ||
                !_levelingRun.IsCompleted ||
                _training == null ||
                _training.Status == GuidedTrainingStatus.Completed ||
                _training.Status == GuidedTrainingStatus.Skipped)
            {
                return;
            }

            const int practiceTargetViewIndex = 1;
            ParticipantId participant =
                GuidedTrainingPracticeTargetCatalog.Participant;
            AddCombatant(
                practiceTargetViewIndex,
                participant.Value,
                2,
                ClassId.Boss,
                SpecializationId.Boss,
                GuidedTrainingPracticeTargetCatalog.Position,
                false,
                GuidedTrainingPracticeTargetCatalog.MaximumHealth,
                false);
            _botBrains[practiceTargetViewIndex] = null;
            _levelingMobBrains[practiceTargetViewIndex] =
                _training.CurrentStep == GuidedTrainingStep.HazardRecovery
                    ? new DeterministicLevelingMobBrain(
                        LevelingMobArchetypeId.RiftWarden)
                    : null;
            _trainingPracticeTargetSpawned = true;
            _focusedTarget = participant;
            _targeting?.ClearSelection();
            _fighterHudBound = false;
        }

        private void ObserveTraining(GuidedTrainingObservation observation)
        {
            if (_selectedMode != GameModeId.Leveling || _training == null)
            {
                return;
            }

            GuidedTrainingObservationResult result = _training.Observe(observation);
            if (!result.WasHandled)
            {
                return;
            }

            if (result.StepBefore == GuidedTrainingStep.HazardRecovery &&
                result.StepAfter != GuidedTrainingStep.HazardRecovery)
            {
                int practiceTargetViewIndex = FindViewIndex(
                    GuidedTrainingPracticeTargetCatalog.Participant);
                if (practiceTargetViewIndex >= 0)
                {
                    _botBrains[practiceTargetViewIndex] = null;
                    _levelingMobBrains[practiceTargetViewIndex] = null;
                }
            }

            int tick = _simulation != null ? _simulation.CurrentTick : 0;
            if (result.StepAdvanced)
            {
                _productServices?.RecordTrainingStep(
                    _selectedSpecialization,
                    result.StepBefore,
                    tick);
            }

            if (result.AbilityUnlocked)
            {
                _productServices?.RecordTrainingUnlock(
                    _selectedSpecialization,
                    result.UnlockedAbility,
                    result.UnlockedAbilityCount,
                    tick);
            }

            if (result.TrainingCompleted)
            {
                _productServices?.RecordTrainingOutcome(
                    _selectedSpecialization,
                    LocalTrainingOutcome.Completed);
                _hud?.ShowCombatNotice(
                    "TRAINING COMPLETE  •  ARENA POWER UNLOCKED FROM THE START",
                    new Color(0.32f, 0.92f, 0.69f, 1f),
                    2f);
                _combatEffects?.PresentUiConfirm();
            }
            else if (result.AbilityUnlocked &&
                     AbilityCatalog.TryGet(
                         result.UnlockedAbility,
                         out AbilityDefinition unlocked))
            {
                _hud?.ShowCombatNotice(
                    unlocked.DisplayName.ToUpperInvariant() + "  •  ACTION " +
                    result.UnlockedAbilityCount + " / " +
                    GuidedTrainingAbilityCatalog.TotalAbilityCount,
                    new Color(1f, 0.75f, 0.25f, 1f),
                    1.5f);
            }

            _lastTrainingStep = (GuidedTrainingStep)(-1);
        }

        private void ObserveRunCombatEvent(CombatEvent combatEvent)
        {
            if (combatEvent.Kind != CombatEventKind.Death ||
                (_siegeRun == null && _dungeonRun == null))
            {
                return;
            }

            if (_siegeRun != null)
            {
                SiegeCombatObservation siegeObservation =
                    _siegeRun.ObserveCombatEvent(combatEvent);
                if (!siegeObservation.WasHandled)
                {
                    return;
                }

                if (siegeObservation.DefeatedSpawn.IsValid)
                {
                    QueueSiegeRetirement(combatEvent.Target);
                    _fighterHudBound = false;
                }

                if (siegeObservation.WaveCleared)
                {
                    _hud?.ShowCombatNotice(
                        "WAVE " + siegeObservation.ClearedWave.Value +
                        " CLEARED  •  +" + siegeObservation.GoldAwarded +
                        " GOLD",
                        new Color(0.32f, 0.92f, 0.69f, 1f),
                        1.5f);
                }
                else if (siegeObservation.RunFailed)
                {
                    _hud?.ShowCombatNotice(
                        "THE DEFENSE LINE HAS FALLEN",
                        new Color(1f, 0.3f, 0.18f, 1f),
                        1.8f);
                }

                return;
            }

            CombatEvent runEvent = CombatEvent.Create(
                _flow.ViewState.ElapsedTicks,
                combatEvent.Kind,
                combatEvent.Actor,
                combatEvent.Target,
                combatEvent.Ability,
                combatEvent.Status,
                combatEvent.Amount,
                combatEvent.Vector,
                combatEvent.Reason,
                combatEvent.Aura);
            EncounterResult observation = _dungeonRun.ObserveCombatEvent(runEvent);
            if (!observation.WasHandled)
            {
                return;
            }

            if (observation.EncounterCleared && !observation.RunCompleted)
            {
                _hud?.ShowCombatNotice(
                    "FLOOR CLEAR  •  PARTY RESTORED",
                    new Color(0.32f, 0.92f, 0.69f, 1f),
                    1.5f);
            }
            else if (observation.RetryAvailable)
            {
                _hud?.ShowCombatNotice(
                    "PARTY WIPE  •  RESTARTING THIS FLOOR",
                    new Color(1f, 0.5f, 0.28f, 1f),
                    1.8f);
            }

            if (observation.EncounterCleared || observation.PartyDefeated)
            {
                _focusedTarget = default;
                _targeting?.ClearSelection();
                _fighterHudBound = false;
            }
        }

        private static Transform ResolveHealPresentationTarget(
            CombatEvent combatEvent,
            CombatantView targetView)
        {
            if (targetView == null)
            {
                return null;
            }

            return combatEvent.Ability == AbilityId.MendPet &&
                targetView.CompanionVisuals != null
                    ? targetView.CompanionVisuals.EffectTarget
                    : targetView.transform;
        }

        private static Transform ResolveDamagePresentationSource(
            CombatEvent combatEvent,
            CombatantView actorView)
        {
            if (actorView == null)
            {
                return null;
            }

            HunterCompanionView companion = actorView.CompanionVisuals;
            return combatEvent.Ability == AbilityId.PetAttack &&
                companion != null && companion.IsVisible
                    ? companion.EffectTarget
                    : actorView.transform;
        }

        private void ShowBossTelegraph(CombatEvent combatEvent)
        {
            if (_telegraphs == null || _telegraphs.Length == 0)
            {
                return;
            }

            TelegraphPresenter presenter = _telegraphs[_nextTelegraph % _telegraphs.Length];
            _nextTelegraph++;
            if (presenter == null)
            {
                return;
            }

            AbilityDefinition definition = AbilityCatalog.Get(combatEvent.Ability);
            Vector3 center = Vector3.zero;
            if (combatEvent.Ability == AbilityId.BossMeteor)
            {
                // CastAccepted carries Core's resolved point, including authoritative
                // range clamping. The warning must mark the same point that will
                // resolve even when the point ability has no participant target.
                center = ToVector3(combatEvent.Vector);
            }
            else if (_simulation.TryGetSnapshot(combatEvent.Actor, out CombatantSnapshot actor))
            {
                center = ToVector3(actor.Position);
            }

            presenter.Show(
                combatEvent.Ability,
                center,
                Mathf.Max(1f, definition.AreaRadius),
                Mathf.Max(0.1f, definition.CastSeconds));
        }

        private void ShowAbilityResolution(
            CombatEvent combatEvent,
            CombatantView actorView,
            CombatantView targetView,
            Vector3 resolvedPoint,
            bool connected)
        {
            if (_telegraphs == null || _telegraphs.Length == 0 ||
                !NeedsReadableGroundResolution(combatEvent.Ability) ||
                !AbilityCatalog.TryGet(
                    combatEvent.Ability,
                    out AbilityDefinition definition))
            {
                return;
            }

            TelegraphPresenter presenter = _telegraphs[_nextTelegraph % _telegraphs.Length];
            _nextTelegraph++;
            if (presenter == null)
            {
                return;
            }

            bool casterArea = definition.Targeting.Shape == AbilityTargetShape.CasterArea;
            bool usesResolvedPoint =
                definition.Targeting.Shape == AbilityTargetShape.Point ||
                definition.Targeting.Shape == AbilityTargetShape.TargetArea;
            Vector3 center = usesResolvedPoint
                ? resolvedPoint
                : casterArea && actorView != null
                    ? actorView.transform.position
                    : targetView != null
                        ? targetView.transform.position
                        : actorView != null
                            ? actorView.transform.position
                            : Vector3.zero;
            Vector3 direction = actorView != null
                ? actorView.transform.forward
                : Vector3.forward;
            if (actorView != null)
            {
                Vector3 towardTarget = center - actorView.transform.position;
                towardTarget.y = 0f;
                if (towardTarget.sqrMagnitude > 0.001f)
                {
                    direction = towardTarget.normalized;
                }
            }

            float radius = definition.Targeting.Radius > 0.01f
                ? definition.Targeting.Radius
                : 0.86f;
            presenter.ShowResolved(
                combatEvent.Ability,
                center,
                radius,
                connected ? 0.42f : 0.28f,
                direction,
                connected);
        }

        private static bool NeedsReadableGroundResolution(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.FrostNova:
                case AbilityId.DragonBreath:
                case AbilityId.BlastWave:
                case AbilityId.PsychicScream:
                case AbilityId.DeepFreeze:
                case AbilityId.Polymorph:
                case AbilityId.FreezingArrow:
                case AbilityId.FrostTrap:
                case AbilityId.Flare:
                case AbilityId.ConeOfCold:
                case AbilityId.WaterElementalFreeze:
                case AbilityId.BossCleave:
                case AbilityId.BossKnockback:
                    return true;
                default:
                    return false;
            }
        }

        private bool ShouldPresentHazardFeedback(ParticipantId target)
        {
            int viewIndex = FindViewIndex(target);
            if (viewIndex < 0 || viewIndex >= _nextHazardFeedbackTicks.Length)
            {
                return true;
            }

            int currentTick = _simulation.CurrentTick;
            if (currentTick < _nextHazardFeedbackTicks[viewIndex])
            {
                return false;
            }

            _nextHazardFeedbackTicks[viewIndex] = currentTick + Mathf.Max(1, _rules.TickRate / 4);
            return true;
        }

        private bool DidAbilityConnect(int completedEventIndex, CombatEvent completedEvent)
        {
            if (!AbilityCatalog.TryGet(
                    completedEvent.Ability,
                    out AbilityDefinition definition) ||
                !definition.Targeting.Allows(AbilityTargetRelation.Hostile))
            {
                return true;
            }

            for (int index = completedEventIndex + 1;
                 index < _simulation.EventCount;
                 index++)
            {
                CombatEvent candidate = _simulation.GetEvent(index);
                if (candidate.Tick != completedEvent.Tick)
                {
                    break;
                }

                if (candidate.Actor != completedEvent.Actor ||
                    candidate.Ability != completedEvent.Ability)
                {
                    continue;
                }

                switch (candidate.Kind)
                {
                    case CombatEventKind.Damage:
                    case CombatEventKind.Heal:
                    case CombatEventKind.BarrierApplied:
                    case CombatEventKind.StatusApplied:
                    case CombatEventKind.StatusRemoved:
                    case CombatEventKind.AuraApplied:
                    case CombatEventKind.AuraRemoved:
                    case CombatEventKind.Interrupted:
                    case CombatEventKind.Knockback:
                    case CombatEventKind.Death:
                        return true;
                }
            }

            return false;
        }

        private static bool IsHarmfulAuraRemoval(CombatEvent combatEvent)
        {
            return combatEvent.Aura != AuraId.None &&
                AuraCatalog.TryGet(combatEvent.Aura, out AuraDefinition definition) &&
                definition.Disposition == AuraDisposition.Harmful;
        }

        private static bool IsPlacedAbility(AbilityId ability)
        {
            return ability == AbilityId.FreezingArrow ||
                ability == AbilityId.FrostTrap ||
                ability == AbilityId.Flare;
        }

        private static bool IsDeferredCompanionCommand(AbilityId ability)
        {
            // Pet: Attack resolves immediately as an order, then produces melee
            // damage on later simulation ticks after the companion reaches its
            // target. Treating the empty command tick as a miss contradicts the
            // authoritative order state and makes a functioning pet look broken.
            return ability == AbilityId.PetAttack;
        }

        private void PresentSnapshots()
        {
            if (_simulation == null)
            {
                return;
            }

            _snapshotCount = _simulation.CopySnapshots(_snapshots);
            ResolveHumanFocusedTarget();
            for (int index = 0; index < _presentedCompanions.Length; index++)
            {
                _presentedCompanions[index] = false;
            }

            int companionCount = _simulation.CopyCompanionSnapshots(_companionSnapshots);
            for (int index = 0; index < companionCount; index++)
            {
                CompanionSnapshot companion = _companionSnapshots[index];
                int viewIndex = FindViewIndex(companion.Owner);
                if (viewIndex < 0 || viewIndex >= _combatantViews.Length)
                {
                    continue;
                }

                _presentedCompanions[viewIndex] = true;
                _combatantViews[viewIndex]?.PresentCompanion(companion);
            }

            for (int index = 0; index < _combatantViews.Length; index++)
            {
                if (index >= _presentedCompanions.Length ||
                    !_presentedCompanions[index])
                {
                    _combatantViews[index]?.HideCompanion();
                }
            }

            int groundEffectCount = _simulation.CopyGroundEffectSnapshots(
                _groundEffectSnapshots);
            _combatEffects?.PresentGroundEffects(
                _groundEffectSnapshots,
                groundEffectCount,
                LocalTeamValue);

            for (int index = 0; index < _snapshotCount; index++)
            {
                CombatantSnapshot snapshot = _snapshots[index];
                CombatantView view = FindView(snapshot.Id);
                if (view != null)
                {
                    view.Present(
                        snapshot,
                        _simulation.CurrentTick,
                        _rules.TickRate,
                        _rules.MoveSpeed,
                        LocalTeamValue,
                        ResolvePersistentAuraVisuals(snapshot.Id));
                    view.SetControlVisualState(GetControlVisualState(snapshot));
                    view.SetMitigationVisual(
                        snapshot.HasStatus(StatusId.DamageReduced),
                        snapshot.HasStatus(StatusId.DamageReduced) ? 0.8f : 0f);
                }
            }

            _offscreenThreats?.Present(
                _gameCamera,
                _snapshots,
                _snapshotCount,
                LocalTeamValue);

            if (!_fighterHudBound)
            {
                BindFighterHud();
            }

            RefreshFighterHud();
            RefreshTargetSelectionVisual();
        }

        private void ResolveHumanFocusedTarget()
        {
            if (!_focusedTarget.IsValid &&
                _selectedMode == GameModeId.Leveling &&
                _training != null &&
                (_training.CurrentStep == GuidedTrainingStep.Movement ||
                 _training.CurrentStep == GuidedTrainingStep.Targeting))
            {
                // These opening steps deliberately require the learner to tap a
                // target. The observation advances immediately after that tap,
                // after which normal fallback selection applies.
                return;
            }

            ParticipantId resolvedTarget = HumanTargetSelector.Select(
                _snapshots,
                _snapshotCount,
                _humanId,
                _focusedTarget);
            if (resolvedTarget == _focusedTarget)
            {
                return;
            }

            _focusedTarget = resolvedTarget;
            // Automatic focus is authoritative snapshot state. The soft-target
            // service remains only an explicit world-pick helper and must not
            // substitute camera angle, colliders, or line of sight here.
            _targeting?.ClearSelection();
        }

        private PersistentAuraVisual ResolvePersistentAuraVisuals(ParticipantId participant)
        {
            PersistentAuraVisual result = PersistentAuraVisual.None;
            AddPersistentAura(
                participant,
                AuraId.VampiricTouch,
                PersistentAuraVisual.VampiricTouch,
                ref result);
            AddPersistentAura(
                participant,
                AuraId.ShadowWordPain,
                PersistentAuraVisual.ShadowWordPain,
                ref result);
            AddPersistentAura(
                participant,
                AuraId.DevouringPlague,
                PersistentAuraVisual.DevouringPlague,
                ref result);
            AddPersistentAura(
                participant,
                AuraId.MindFlaySlow,
                PersistentAuraVisual.MindFlay,
                ref result);
            AddPersistentAura(
                participant,
                AuraId.Dispersion,
                PersistentAuraVisual.Dispersion,
                ref result);
            AddPersistentAura(
                participant,
                AuraId.Horror,
                PersistentAuraVisual.PsychicHorror,
                ref result);
            AddPersistentAura(
                participant,
                AuraId.SerpentSting,
                PersistentAuraVisual.SerpentSting,
                ref result);
            AddPersistentAura(
                participant,
                AuraId.ViperSting,
                PersistentAuraVisual.ViperSting,
                ref result);
            AddPersistentAura(
                participant,
                AuraId.ScorpidSting,
                PersistentAuraVisual.ScorpidSting,
                ref result);
            AddPersistentAura(
                participant,
                AuraId.HuntersMark,
                PersistentAuraVisual.HuntersMark,
                ref result);
            AddPersistentAura(
                participant,
                AuraId.Deterrence,
                PersistentAuraVisual.Deterrence,
                ref result);

            if (HasActiveAura(participant, AuraId.FreezingTrap) ||
                HasActiveAura(participant, AuraId.FrostTrapSlow) ||
                HasActiveAura(participant, AuraId.SpiderWeb))
            {
                result |= PersistentAuraVisual.TrapRoot;
            }

            return result;
        }

        private void AddPersistentAura(
            ParticipantId participant,
            AuraId aura,
            PersistentAuraVisual visual,
            ref PersistentAuraVisual result)
        {
            if (HasActiveAura(participant, aura))
            {
                result |= visual;
            }
        }

        private bool HasActiveAura(ParticipantId participant, AuraId aura)
        {
            return _simulation.GetAuraRemaining(participant, aura) > 0f;
        }

        private void EvaluateResult()
        {
            // Grinding has no victory screen or timer. Death is refuge recovery.
            if (_selectedMode == GameModeId.PveLeveling) { return; }
            int alliesAlive = 0;
            int enemiesAlive = 0;
            bool bossAlive = false;
            for (int index = 0; index < _snapshotCount; index++)
            {
                CombatantSnapshot snapshot = _snapshots[index];
                if (!snapshot.IsAlive)
                {
                    continue;
                }

                if (snapshot.Team.Value == 1)
                {
                    alliesAlive++;
                }
                else
                {
                    enemiesAlive++;
                }

                if (snapshot.Class == ClassId.Boss)
                {
                    bossAlive = true;
                }
            }

            bool complete = false;
            bool won = false;
            string reason = string.Empty;
            if (_selectedMode == GameModeId.Arena)
            {
                if (enemiesAlive == 0 && alliesAlive == 0)
                {
                    complete = true;
                    reason = "Both teams fell — draw";
                }
                else if (enemiesAlive == 0)
                {
                    complete = true;
                    won = true;
                    reason = "Enemy team defeated";
                }
                else if (alliesAlive == 0)
                {
                    complete = true;
                    reason = "Your team was defeated";
                }
            }
            else if (_selectedMode == GameModeId.HeroSiege)
            {
                SiegeRunResult runResult = _siegeRun != null
                    ? _siegeRun.Result
                    : default;
                if (runResult.IsTerminal)
                {
                    complete = true;
                    won = runResult.DidWin;
                    reason = DescribeSiegeResult(runResult);
                }
            }
            else if (_selectedMode == GameModeId.Dungeon)
            {
                RunResult runResult = _dungeonRun != null
                    ? _dungeonRun.Result
                    : default;
                if (runResult.IsTerminal)
                {
                    complete = true;
                    won = runResult.DidWin;
                    reason = DescribeRunResult(runResult);
                }
            }
            else if (_selectedMode == GameModeId.Boss)
            {
                if (!bossAlive)
                {
                    complete = true;
                    won = true;
                    reason = "The Rift Warden is broken";
                }
                else if (alliesAlive == 0)
                {
                    complete = true;
                    reason = "Party wipe";
                }
            }
            else if ((_levelingRun != null && _levelingRun.IsFailed) ||
                alliesAlive == 0)
            {
                complete = true;
                reason = "The expedition was overrun";
            }
            else if (_levelingRun != null && _levelingRun.IsCompleted)
            {
                bool coachingComplete = _training == null ||
                    _training.Status == GuidedTrainingStatus.Completed ||
                    _training.Status == GuidedTrainingStatus.Skipped;
                if (coachingComplete)
                {
                    complete = true;
                    won = true;
                    reason = "Complete learning kit practiced — Arena ready";
                }
            }

            int maximumSeconds = _selectedMode == GameModeId.Leveling
                ? LevelingMaximumSessionSeconds
                : _selectedMode == GameModeId.HeroSiege ||
                  _selectedMode == GameModeId.Dungeon
                    ? PveMaximumSessionSeconds
                    : MaximumSessionSeconds;
            if (!complete &&
                _flow.ViewState.ElapsedTicks >= _rules.TickRate * maximumSeconds)
            {
                complete = true;
                if (_selectedMode == GameModeId.HeroSiege ||
                    _selectedMode == GameModeId.Dungeon)
                {
                    AbandonActiveRun();
                }
                reason = _selectedMode == GameModeId.Arena
                    ? "Time limit draw"
                    : _selectedMode == GameModeId.Boss
                        ? "The ritual completed"
                        : _selectedMode == GameModeId.Leveling
                            ? "The training run timed out"
                            : "The run timed out";
            }

            if (!complete)
            {
                return;
            }

            _flow.Complete(won, reason);
            _inputSource?.ClearTransientInput();
            bool draw = _selectedMode == GameModeId.Arena && reason.Contains("draw");
            FinishSessionRecord(
                _selectedMode == GameModeId.Leveling && won
                    ? SessionRecordOutcome.Completed
                    : draw
                        ? SessionRecordOutcome.Draw
                        : won
                            ? SessionRecordOutcome.Victory
                            : SessionRecordOutcome.Defeat);
            _combatEffects?.PresentResult(won, draw);
            PresentResultHaptic();
            bool trainingComplete = _selectedMode == GameModeId.Leveling && won;
            string recap = string.IsNullOrEmpty(_lastRecap)
                ? reason
                : reason + "\n" + _lastRecap;
            _hud?.ShowResults(
                trainingComplete
                    ? "TRAINING COMPLETE"
                    : won ? "VICTORY" : draw
                    ? "DRAW"
                    : "DEFEAT",
                BuildResultRecap(recap),
                trainingComplete
                    ? "CONTINUE TO ARENA"
                    : _selectedMode == GameModeId.Leveling
                        ? "RETRY TRAINING"
                        : BuildReplayAction(_selectedMode));
        }

        private void BindFighterHud()
        {
            if (_hud == null)
            {
                return;
            }

            _hud.ClearFighters();
            if (_selectedMode == GameModeId.HeroSiege && _siegeRun != null)
            {
                int slot = 0;
                for (int defenderIndex = 0;
                     defenderIndex < _siegeRun.Variant.Party.Count &&
                     slot < PrototypeHud.MaximumFighterViewCount;
                     defenderIndex++)
                {
                    ParticipantId defender = _siegeRun.GetDefender(defenderIndex);
                    if (_simulation.TryGetSnapshot(
                            defender,
                            out CombatantSnapshot defenderSnapshot))
                    {
                        BindFighterHudSlot(slot++, defenderSnapshot);
                    }
                }

                for (int hostileIndex = 0;
                     hostileIndex < _siegeRun.State.ActiveHostileCount &&
                     slot < PrototypeHud.MaximumFighterViewCount;
                     hostileIndex++)
                {
                    ParticipantId hostile =
                        _siegeRun.GetActiveSpawn(hostileIndex).Participant;
                    if (_simulation.TryGetSnapshot(
                            hostile,
                            out CombatantSnapshot hostileSnapshot))
                    {
                        BindFighterHudSlot(slot++, hostileSnapshot);
                    }
                }

                _fighterHudBound = true;
                return;
            }

            if (_selectedMode == GameModeId.Leveling && _levelingRun != null)
            {
                int slot = 0;
                if (_simulation.TryGetSnapshot(
                        _humanId,
                        out CombatantSnapshot levelingPlayer))
                {
                    BindFighterHudSlot(slot++, levelingPlayer);
                }

                for (int index = 0;
                     index < _levelingRun.ActiveMobCount && slot < 4;
                     index++)
                {
                    ParticipantId mob = _levelingRun.GetActiveMobId(index);
                    if (_simulation.TryGetSnapshot(
                            mob,
                            out CombatantSnapshot mobSnapshot))
                    {
                        BindFighterHudSlot(slot++, mobSnapshot);
                    }
                }

                _fighterHudBound = true;
                return;
            }

            for (int index = 0; index < _snapshotCount; index++)
            {
                BindFighterHudSlot(index, _snapshots[index]);
            }

            _fighterHudBound = true;
        }

        private void BindFighterHudSlot(int slot, CombatantSnapshot snapshot)
        {
            bool isFriendly = snapshot.Team.Value == LocalTeamValue;
            bool isHuman = snapshot.Id == _humanId;
            string relation = DescribeRelation(snapshot, isHuman);
            string fighterName = DescribeFighterName(snapshot, isHuman);
            if (_grindingRun != null && !isHuman &&
                _grindingRun.TryGetMob(snapshot.Id, out GrindingMobDefinition grindingMob))
            {
                relation = "LEVEL " + grindingMob.Level + "  •  ENEMY";
                fighterName = LevelingMobArchetypeCatalog.Get(grindingMob.Archetype).ShortDisplayName;
            }
            else if (_selectedMode == GameModeId.Leveling &&
                !isHuman &&
                _levelingRun != null &&
                _levelingRun.Zone.TryGetMob(
                    snapshot.Id,
                    out LevelingMobDefinition mob))
            {
                relation = "LEVEL " + mob.Level + "  •  ENEMY";
                fighterName = LevelingMobArchetypeCatalog.Get(
                    mob.Archetype).ShortDisplayName;
            }
            else if (_selectedMode == GameModeId.HeroSiege &&
                !isFriendly &&
                TryGetSiegeSpawn(snapshot.Id, out SiegeSpawnDefinition spawn))
            {
                relation = spawn.Archetype.Tier == RunEnemyTier.SwarmMinion
                    ? "SWARM  •  ENEMY"
                    : spawn.Archetype.Rank == RunEnemyRank.Elite
                        ? "ELITE  •  ENEMY"
                        : "ENEMY";
                fighterName = spawn.Archetype.DisplayName;
            }

            _hud.BindFighter(
                slot,
                snapshot.Id,
                snapshot.Class,
                fighterName,
                relation,
                isFriendly,
                isHuman);
        }

        private bool TryGetSiegeSpawn(
            ParticipantId participant,
            out SiegeSpawnDefinition spawn)
        {
            if (_siegeRun != null)
            {
                int count = _siegeRun.State.ActiveHostileCount;
                for (int index = 0; index < count; index++)
                {
                    SiegeSpawnDefinition candidate =
                        _siegeRun.GetActiveSpawn(index);
                    if (candidate.Participant == participant)
                    {
                        spawn = candidate;
                        return true;
                    }
                }
            }

            spawn = default;
            return false;
        }

        private void RefreshFighterHud()
        {
            if (_hud == null || !_fighterHudBound || _simulation == null)
            {
                return;
            }

            for (int index = 0; index < _snapshotCount; index++)
            {
                CombatantSnapshot snapshot = _snapshots[index];
                bool hidden = snapshot.IsAlive &&
                    IsHiddenFromTeam(snapshot, LocalTeamValue);
                _hud.SetFighterHidden(snapshot.Id, hidden);
                if (hidden)
                {
                    continue;
                }

                _hud.PresentFighter(
                    snapshot,
                    _simulation.CurrentTick,
                    _rules.TickRate,
                    DescribeStatus(snapshot),
                    snapshot.Id == _focusedTarget);
            }
        }

        private void RefreshHud()
        {
            if (_hud == null || _simulation == null)
            {
                return;
            }

            RefreshObjective();
            RefreshLevelingProgressionHud();

            if (_simulation.TryGetSnapshot(_humanId, out CombatantSnapshot human))
            {
                _hud.SetHealth(human.Health, human.MaxHealth);
                _hud.SetPlayerResource(
                    human.ResourceType,
                    human.Resource,
                    human.MaxResource,
                    human.IsAlive && human.ResourceType != ResourceType.None);
                RefreshSiegeSupply(human);
                bool comboTargetMatches = human.ComboPoints <= 0 ||
                    (_focusedTarget.IsValid && human.ComboTarget == _focusedTarget);
                _hud.SetComboPoints(
                    human.ComboPoints,
                    _rules.MaximumComboPoints,
                    comboTargetMatches,
                    human.IsAlive && human.Class == ClassId.Rogue);
                bool casting = human.CastingAbility != AbilityId.None;
                if (casting)
                {
                    AbilityDefinition castDefinition = AbilityCatalog.Get(human.CastingAbility);
                    float progress = CalculateCastProgress(human, _simulation.CurrentTick);
                    _hud.SetCast(castDefinition.DisplayName, progress, true);
                }
                else
                {
                    _hud.SetCast(string.Empty, 0f, false);
                }

                AbilityKit activeKit = GetActivePlayerKit();
                for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
                {
                    RefreshHumanAbilityState(activeKit.GetAbility(slot), human);
                    RefreshHumanAbilityState(
                        activeKit.GetAbility(slot, AbilitySlotDirection.Up),
                        human);
                    RefreshHumanAbilityState(
                        activeKit.GetAbility(slot, AbilitySlotDirection.Down),
                        human);
                    RefreshHumanAbilityState(
                        activeKit.GetAbility(slot, AbilitySlotDirection.Left),
                        human);
                    RefreshHumanAbilityState(
                        activeKit.GetAbility(slot, AbilitySlotDirection.Right),
                        human);
                }

                float globalCooldown = _simulation.GetGlobalCooldownRemaining(_humanId);
                float globalCooldownTotal = human.Class == ClassId.Rogue ? 1f : 1.5f;
                _hud.SetGlobalCooldown(
                    globalCooldown,
                    globalCooldownTotal,
                    globalCooldown > 0f);
            }

            if (_focusedTarget.IsValid &&
                _simulation.TryGetSnapshot(_focusedTarget, out CombatantSnapshot selected) &&
                selected.IsAlive &&
                !IsHiddenFromTeam(selected, LocalTeamValue))
            {
                bool isFriendly = selected.Team.Value == 1;
                _hud.SetFocusedTarget(
                    selected,
                    DescribeFighterName(selected, selected.Id == _humanId),
                    selected.Class == ClassId.Boss
                        ? "BOSS"
                        : selected.Id == _humanId
                            ? "YOU"
                            : isFriendly ? "ALLY" : "ENEMY",
                    isFriendly,
                    _simulation.CurrentTick,
                    _rules.TickRate,
                    DescribeStatus(selected),
                    true);
                _hud.SetTargetResource(
                    selected.ResourceType,
                    selected.Resource,
                    selected.MaxResource,
                    selected.ResourceType != ResourceType.None);
                if (selected.CastingAbility != AbilityId.None &&
                    AbilityCatalog.TryGet(
                        selected.CastingAbility,
                        out AbilityDefinition selectedCast))
                {
                    _hud.SetTargetCast(
                        selectedCast.DisplayName,
                        CalculateCastProgress(selected, _simulation.CurrentTick),
                        selected.CastingActivationType == AbilityActivationType.Channel,
                        selected.Class != ClassId.Boss,
                        true);
                }
                else
                {
                    _hud.SetTargetCast(string.Empty, 0f, false, false, false);
                }

                PresentFocusedAuras(selected);
            }
            else
            {
                _hud.SetTarget(string.Empty, 0f, 0f, false);
                _hud.ClearFocusedFighter();
            }
        }

        private void RefreshSiegeSupply()
        {
            if (_simulation != null &&
                _simulation.TryGetSnapshot(_humanId, out CombatantSnapshot human))
            {
                RefreshSiegeSupply(human);
                return;
            }

            _hud?.SetSiegeSupplyState(
                false,
                0,
                0,
                0,
                HeroSiegeRunCatalog.HealthPotionCost,
                HeroSiegeRunCatalog.ManaPotionCost,
                false,
                false,
                false,
                false);
        }

        private void RefreshSiegeSupply(CombatantSnapshot human)
        {
            if (_selectedMode != GameModeId.HeroSiege || _siegeRun == null)
            {
                _hud?.SetSiegeSupplyState(
                    false,
                    0,
                    0,
                    0,
                    HeroSiegeRunCatalog.HealthPotionCost,
                    HeroSiegeRunCatalog.ManaPotionCost,
                    false,
                    false,
                    false,
                    false);
                return;
            }

            SiegeRunState run = _siegeRun.State;
            bool active = run.Status == SiegeRunStatus.Preparation ||
                run.Status == SiegeRunStatus.WaveActive;
            bool usesMana = human.ResourceType == ResourceType.Mana;
            _hud?.SetSiegeSupplyState(
                true,
                run.Gold,
                run.HealthPotionCount,
                run.ManaPotionCount,
                HeroSiegeRunCatalog.HealthPotionCost,
                HeroSiegeRunCatalog.ManaPotionCost,
                active &&
                    run.Gold >= HeroSiegeRunCatalog.HealthPotionCost &&
                    run.HealthPotionCount < HeroSiegeRunCatalog.MaximumPotionCount,
                active && human.IsAlive && run.HealthPotionCount > 0 &&
                    human.Health < human.MaxHealth - 0.0001f,
                active && usesMana &&
                    run.Gold >= HeroSiegeRunCatalog.ManaPotionCost &&
                    run.ManaPotionCount < HeroSiegeRunCatalog.MaximumPotionCount,
                active && human.IsAlive && usesMana &&
                    run.ManaPotionCount > 0 &&
                    human.Resource < human.MaxResource - 0.0001f);
        }

        private void RefreshObjective()
        {
            if (_grindingRun != null && _flow.Phase == SessionPhase.Playing)
            {
                RefreshGrindingObjective();
                return;
            }
            SessionPhase phase = _flow.Phase;
            if (phase == SessionPhase.Countdown)
            {
                int seconds = Mathf.CeilToInt(
                    _flow.ViewState.CountdownTicks / (float)_rules.TickRate);
                if (_lastObjectivePhase != phase ||
                    _lastObjectiveCountdownSeconds != seconds)
                {
                    _hud.SetObjective(seconds > 0 ? seconds.ToString() : "FIGHT");
                }

                _lastObjectiveCountdownSeconds = seconds;
            }
            else if (phase == SessionPhase.Playing)
            {
                if (_selectedMode == GameModeId.Leveling && _levelingRun != null)
                {
                    LevelingDistrictDefinition district =
                        _simulation.TryGetSnapshot(
                            _humanId,
                            out CombatantSnapshot levelingHuman)
                            ? LevelingDistrictCatalog.GetAtWorldX(
                                levelingHuman.Position.X)
                            : default;
                    int activePack = _levelingRun.ActivePackIndex >= 0
                        ? _levelingRun.ActivePackIndex + 1
                        : Mathf.Min(
                            _levelingRun.NextPackIndex + 1,
                            _levelingRun.Zone.PackCount);
                    int activeMobCount = _levelingRun.ActiveMobCount;
                    int recoverySeconds = Mathf.CeilToInt(
                        _levelingRun.GetTicksUntilNextPackReady(
                            _simulation.CurrentTick) /
                        (float)_rules.TickRate);
                    GuidedTrainingStep trainingStep = _training != null
                        ? _training.CurrentStep
                        : GuidedTrainingStep.Movement;
                    if (_lastObjectivePhase != phase ||
                        _lastObjectiveMode != _selectedMode ||
                        _lastObjectiveDistrict != district.Id ||
                        _lastObjectivePack != activePack ||
                        _lastObjectiveMobCount != activeMobCount ||
                        _lastObjectiveRecoverySeconds != recoverySeconds ||
                        _lastTrainingStep != trainingStep)
                    {
                        _hud.SetObjective(
                            "STEP " + ((int)trainingStep + 1) + " / " +
                            GuidedTrainingController.StepCount + "  •  " +
                            DescribeTrainingStep(trainingStep));
                    }

                    _lastObjectiveDistrict = district.Id;
                    _lastObjectivePack = activePack;
                    _lastObjectiveMobCount = activeMobCount;
                    _lastObjectiveRecoverySeconds = recoverySeconds;
                    _lastTrainingStep = trainingStep;
                }
                else if (_siegeRun != null)
                {
                    SiegeRunState run = _siegeRun.State;
                    int healthPercent = Mathf.CeilToInt(
                        run.Objective.HealthFraction * 100f);
                    int nextWaveSeconds = run.StartedWaveCount <
                        HeroSiegeRunCatalog.WaveCount
                            ? Mathf.CeilToInt(
                                run.TicksUntilNextWave /
                                (float)_rules.TickRate)
                            : 0;
                    int visibleHostiles = run.ActiveHostileCount +
                        run.PendingSpawnCount;
                    if (_lastObjectivePhase != phase ||
                        _lastObjectiveMode != _selectedMode ||
                        _lastRunEncounterNumber != run.CurrentWaveNumber ||
                        _lastRunRemainingCount != visibleHostiles ||
                        _lastObjectiveHealthPercent != healthPercent ||
                        _lastSiegeStatus != run.Status ||
                        _lastSiegeActiveWaveCount != run.ActiveWaveCount ||
                        _lastSiegePendingCount != run.PendingSpawnCount ||
                        _lastSiegeGold != run.Gold ||
                        _lastSiegeHealthPotions != run.HealthPotionCount ||
                        _lastSiegeManaPotions != run.ManaPotionCount ||
                        _lastSiegeNextWaveSeconds != nextWaveSeconds)
                    {
                        _hud.SetObjective(
                            "WAVE " + run.StartedWaveCount + " / " +
                            HeroSiegeRunCatalog.WaveCount +
                            (run.StartedWaveCount < HeroSiegeRunCatalog.WaveCount
                                ? "  •  NEXT 0:" +
                                  nextWaveSeconds.ToString("00")
                                : string.Empty) +
                            "  •  " + run.ActiveWaveCount + " ACTIVE\n" +
                            "CORE " + healthPercent + "%  •  " +
                            visibleHostiles + " HOSTILES");
                    }

                    _lastRunEncounterNumber = run.CurrentWaveNumber;
                    _lastRunRemainingCount = visibleHostiles;
                    _lastObjectiveHealthPercent = healthPercent;
                    _lastSiegeStatus = run.Status;
                    _lastSiegeActiveWaveCount = run.ActiveWaveCount;
                    _lastSiegePendingCount = run.PendingSpawnCount;
                    _lastSiegeGold = run.Gold;
                    _lastSiegeHealthPotions = run.HealthPotionCount;
                    _lastSiegeManaPotions = run.ManaPotionCount;
                    _lastSiegeNextWaveSeconds = nextWaveSeconds;
                }
                else if (_dungeonRun != null)
                {
                    RunState run = _dungeonRun.State;
                    if (_lastObjectivePhase != phase ||
                        _lastObjectiveMode != _selectedMode ||
                        _lastRunEncounterNumber != run.CurrentEncounterNumber ||
                        _lastRunRemainingCount != run.RemainingSpawnCount ||
                        _lastRunAttempt != run.CurrentAttempt ||
                        _lastRunStatus != run.Status)
                    {
                        _hud.SetObjective(
                            "DUNGEON  •  FLOOR " + run.CurrentEncounterNumber +
                            " / " + run.EncounterCount + "  •  " +
                            DescribeDungeonFloor(run.CurrentEncounterIndex) +
                            "  •  ATTEMPT " + run.CurrentAttempt + "  •  " +
                            run.RemainingSpawnCount + " HOSTILES");
                    }

                    _lastRunEncounterNumber = run.CurrentEncounterNumber;
                    _lastRunRemainingCount = run.RemainingSpawnCount;
                    _lastRunAttempt = run.CurrentAttempt;
                    _lastRunStatus = run.Status;
                }
                else if (_lastObjectivePhase != phase ||
                    _lastObjectiveMode != _selectedMode)
                {
                    _hud.SetObjective(_selectedMode == GameModeId.Arena
                        ? "2 vs 2  •  DEFEAT THE ENEMY TEAM"
                        : "RAID  •  BREAK THE RIFT WARDEN");
                }
            }
            else if (phase == SessionPhase.Spectating &&
                (_lastObjectivePhase != phase ||
                 _lastObjectiveSpectatorTarget != _spectatorTarget))
            {
                _hud.SetObjective(ResolveSpectatorObjective());
            }

            _lastObjectivePhase = phase;
            _lastObjectiveMode = _selectedMode;
            _lastObjectiveSpectatorTarget = _spectatorTarget;
        }

        private void RefreshLevelingProgressionHud()
        {
            if (_grindingRun != null)
            {
                RefreshGrindingProgressionHud();
                return;
            }
            if (_selectedMode != GameModeId.Leveling || _levelingRun == null)
            {
                if (_lastProgressionLevel != -1)
                {
                    _hud?.SetLevelProgression(1, 0, 0, 80, false);
                    _lastProgressionLevel = -1;
                    _lastProgressionExperience = -1;
                }

                return;
            }

            LevelProgressionState progression = _levelingRun.Progression;
            if (_lastProgressionLevel == progression.Level &&
                _lastProgressionExperience == progression.TotalExperience)
            {
                return;
            }

            int levelStart = LevelProgressionCurve.GetCumulativeExperienceForLevel(
                progression.Level);
            int levelEnd = progression.Level >= LevelProgressionCurve.MaximumLevel
                ? LevelProgressionCurve.MaximumExperience
                : LevelProgressionCurve.GetCumulativeExperienceForLevel(
                    progression.Level + 1);
            _hud?.SetLevelProgression(
                progression.Level,
                progression.TotalExperience,
                levelStart,
                levelEnd,
                true);
            _lastProgressionLevel = progression.Level;
            _lastProgressionExperience = progression.TotalExperience;
        }

        private string ResolveSpectatorObjective()
        {
            if (!_spectatorTarget.IsValid ||
                !_simulation.TryGetSnapshot(
                    _spectatorTarget,
                    out CombatantSnapshot spectator))
            {
                return "SPECTATING";
            }

            if (spectator.Class == ClassId.Boss)
            {
                return "SPECTATING  •  RIFT WARDEN";
            }

            bool isAlly = spectator.Team.Value == LocalTeamValue;
            switch (spectator.Class)
            {
                case ClassId.Healer:
                    return isAlly
                        ? "SPECTATING  •  ALLY HEALER"
                        : "SPECTATING  •  ENEMY HEALER";
                case ClassId.Rogue:
                    return isAlly
                        ? "SPECTATING  •  ALLY ROGUE"
                        : "SPECTATING  •  ENEMY ROGUE";
                case ClassId.Mage:
                    return isAlly
                        ? "SPECTATING  •  ALLY MAGE"
                        : "SPECTATING  •  ENEMY MAGE";
                case ClassId.Hunter:
                    return isAlly
                        ? "SPECTATING  •  ALLY HUNTER"
                        : "SPECTATING  •  ENEMY HUNTER";
                default:
                    return isAlly
                        ? "SPECTATING  •  ALLY"
                        : "SPECTATING  •  ENEMY";
            }
        }

        private void EvaluateHumanAbilityReadiness(
            AbilityId ability,
            CombatantSnapshot human,
            out AbilityReadinessState state,
            out string blockedReason)
        {
            if (_selectedMode == GameModeId.Leveling &&
                LevelingAbilityUnlockCatalog.TryGetRequiredLevel(
                    _selectedSpecialization,
                    ability,
                    out int levelingRequiredLevel) &&
                human.Level < levelingRequiredLevel)
            {
                state = AbilityReadinessState.Unavailable;
                blockedReason = "LEVEL " + levelingRequiredLevel;
                return;
            }

            ResolvedCast cast = ResolveTargetedCast(ability);
            if (!cast.IsValid)
            {
                state = AbilityReadinessState.NoTarget;
                blockedReason = "SELECT TARGET";
                return;
            }

            Vector3 movement = _inputSource != null && _gameCamera != null
                ? _inputSource.CurrentFrame.ToCameraRelativeMovement(_gameCamera.transform)
                : Vector3.zero;
            CombatCommand command = CombatCommand.Cast(
                _humanId,
                _humanSequence + 1,
                ToFloat3(movement),
                cast.Direction,
                ability,
                cast.Target,
                cast.Point,
                cast.Direction);
            if (AbilityReadinessEvaluator.CanStartForDisplay(
                    _simulation,
                    command,
                    out string reason))
            {
                state = AbilityReadinessState.Ready;
                blockedReason = string.Empty;
                return;
            }

            state = ResolveReadinessState(reason);
            if (reason == "level_locked" &&
                LevelingAbilityUnlockCatalog.TryGetRequiredLevel(
                    _selectedSpecialization,
                    ability,
                    out int requiredLevel))
            {
                blockedReason = "LEVEL " + requiredLevel;
            }
            else
            {
                blockedReason = DescribeReadinessBadge(reason, human.ResourceType);
            }
        }

        private void RefreshHumanAbilityState(
            AbilityId ability,
            CombatantSnapshot human)
        {
            if (ability == AbilityId.None)
            {
                return;
            }

            float cooldown = _simulation.GetCooldownRemaining(_humanId, ability);
            _hud.SetAbilityCooldown(
                ability,
                cooldown,
                human.IsAlive && cooldown <= 0.0001f);
            EvaluateHumanAbilityReadiness(
                ability,
                human,
                out AbilityReadinessState readiness,
                out string blockedReason);
            _hud.SetAbilityReadiness(ability, readiness, blockedReason);
        }

        private void RefreshTargetSelectionVisual()
        {
            for (int index = 0; index < _combatantViews.Length; index++)
            {
                CombatantView view = _combatantViews[index];
                view?.SetSelected(view.Id == _focusedTarget);
            }
        }

        private void PresentFocusedAuras(CombatantSnapshot snapshot)
        {
            HudAuraIndicator first = default;
            HudAuraIndicator second = default;
            HudAuraIndicator third = default;
            HudAuraIndicator fourth = default;
            int count = 0;

            AppendHudAura(
                snapshot.Id,
                AuraId.Fear,
                "FEAR",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Stun,
                "STUN",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Blind,
                "BLIND",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Polymorphed,
                "POLYMORPH",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Disoriented,
                "DISORIENT",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Sap,
                "SAP",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Gouge,
                "GOUGE",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.FrozenRoot,
                "FROZEN",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Horror,
                "HORROR",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Silence,
                "SILENCE",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Disarm,
                "DISARM",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.ScatterDisorient,
                "SCATTER",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.FreezingTrap,
                "FREEZING TRAP",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.SpiderWeb,
                "WEB",
                HudAuraKind.Control,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Dispersion,
                "DISPERSION",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Deterrence,
                "DETERRENCE",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.RoarOfSacrifice,
                "ROAR OF SACRIFICE",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.MastersCall,
                "MASTER'S CALL",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.FeignDeath,
                "FEIGN DEATH",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.IceBlock,
                "ICE BLOCK",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.ManaShield,
                "MANA SHIELD",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.PrayerOfMending,
                "PRAYER OF MENDING",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.InnerFocus,
                "INNER FOCUS",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.FearWard,
                "FEAR WARD",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.AspectOfTheDragonhawk,
                "DRAGONHAWK",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.AspectOfTheViper,
                "VIPER ASPECT",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.MoltenArmor,
                "MOLTEN ARMOR",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.MageArmor,
                "MAGE ARMOR",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.IceArmor,
                "ICE ARMOR",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Sprint,
                "SPRINT",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.VampiricTouch,
                "VAMPIRIC TOUCH",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.ShadowWordPain,
                "SHADOW WORD: PAIN",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.DevouringPlague,
                "DEVOURING PLAGUE",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.GarroteBleed,
                "GARROTE",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.MortalWound,
                "MORTAL WOUND",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.SerpentSting,
                "SERPENT STING",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.ViperSting,
                "VIPER STING",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.ScorpidSting,
                "SCORPID STING",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.HuntersMark,
                "HUNTER'S MARK",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.ChimeraScorpidLockout,
                "SCORPID LOCKOUT",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.LivingEmber,
                "LIVING BOMB",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Poison,
                "POISON",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.FrostSlow,
                "SLOWED",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.MindFlaySlow,
                "MIND FLAY",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.WingClip,
                "WING CLIP",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.FrostTrapSlow,
                "FROST TRAP",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.WeakenedSoul,
                "WEAKENED SOUL",
                HudAuraKind.Harmful,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Renew,
                "RENEW",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.PowerInfusion,
                "POWER INFUSION",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Combustion,
                "COMBUSTION",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.IcyVeins,
                "ICY VEINS",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Evasion,
                "EVASION",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.ColdBlood,
                "COLD BLOOD",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.GuardianCovenant,
                "PAIN SUPPRESSION",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.SoulWard,
                "SHIELD",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.IceWard,
                "ICE BARRIER",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Spellveil,
                "CLOAK",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.RapidFire,
                "RAPID FIRE",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);
            AppendHudAura(
                snapshot.Id,
                AuraId.Replenishment,
                "REPLENISHMENT",
                HudAuraKind.Beneficial,
                ref count,
                ref first,
                ref second,
                ref third,
                ref fourth);

            _hud.SetTargetAuras(first, second, third, fourth);
        }

        private void AppendHudAura(
            ParticipantId participant,
            AuraId aura,
            string label,
            HudAuraKind kind,
            ref int count,
            ref HudAuraIndicator first,
            ref HudAuraIndicator second,
            ref HudAuraIndicator third,
            ref HudAuraIndicator fourth)
        {
            if (count >= PrototypeHud.TargetAuraSlotCount)
            {
                return;
            }

            float remaining = _simulation.GetAuraRemaining(participant, aura);
            if (remaining <= 0f)
            {
                return;
            }

            var indicator = new HudAuraIndicator(
                label,
                remaining,
                _simulation.GetAuraStacks(participant, aura),
                kind);
            switch (count)
            {
                case 0:
                    first = indicator;
                    break;
                case 1:
                    second = indicator;
                    break;
                case 2:
                    third = indicator;
                    break;
                case 3:
                    fourth = indicator;
                    break;
            }

            count++;
        }

        private static float CalculateCastProgress(
            CombatantSnapshot snapshot,
            int currentTick)
        {
            int totalTicks = Mathf.Max(1, snapshot.CastEndTick - snapshot.CastStartTick);
            int elapsedTicks = Mathf.Clamp(
                currentTick - snapshot.CastStartTick,
                0,
                totalTicks);
            return elapsedTicks / (float)totalTicks;
        }

        private static bool IsChannelAbility(AbilityId ability)
        {
            return AbilityCatalog.TryGet(ability, out AbilityDefinition definition) &&
                definition.CastProfile.ActivationType == AbilityActivationType.Channel;
        }

        private void BindDesktopKit(SpecializationId specialization)
        {
            AbilityKit kit = GetActivePlayerKit();
            _inputSource?.SetDesktopAbilityBindings(
                kit.GetAbility(0),
                kit.GetAbility(1),
                kit.GetAbility(2),
                kit.GetAbility(3),
                kit.GetAbility(4),
                kit.GetAbility(5),
                kit.GetAbility(6),
                kit.GetAbility(7),
                kit.GetAbility(8),
                kit.GetAbility(9));
            _inputSource?.SetDesktopAlternateUpBindings(
                kit.GetAbility(0, AbilitySlotDirection.Up),
                kit.GetAbility(1, AbilitySlotDirection.Up),
                kit.GetAbility(2, AbilitySlotDirection.Up),
                kit.GetAbility(3, AbilitySlotDirection.Up),
                kit.GetAbility(4, AbilitySlotDirection.Up),
                kit.GetAbility(5, AbilitySlotDirection.Up),
                kit.GetAbility(6, AbilitySlotDirection.Up),
                kit.GetAbility(7, AbilitySlotDirection.Up),
                kit.GetAbility(8, AbilitySlotDirection.Up),
                kit.GetAbility(9, AbilitySlotDirection.Up));
            _inputSource?.SetDesktopAlternateRightBindings(
                kit.GetAbility(0, AbilitySlotDirection.Right),
                kit.GetAbility(1, AbilitySlotDirection.Right),
                kit.GetAbility(2, AbilitySlotDirection.Right),
                kit.GetAbility(3, AbilitySlotDirection.Right),
                kit.GetAbility(4, AbilitySlotDirection.Right),
                kit.GetAbility(5, AbilitySlotDirection.Right),
                kit.GetAbility(6, AbilitySlotDirection.Right),
                kit.GetAbility(7, AbilitySlotDirection.Right),
                kit.GetAbility(8, AbilitySlotDirection.Right),
                kit.GetAbility(9, AbilitySlotDirection.Right));
        }

        private AbilityKit GetActivePlayerKit()
        {
            if (_selectedMode == GameModeId.Leveling && _levelingRun != null)
            {
                return _training != null &&
                    _training.CurrentStep == GuidedTrainingStep.FlickMastery
                        ? LevelingAbilityUnlockCatalog.GetFlickLessonKit(
                            _selectedSpecialization)
                        : LevelingAbilityUnlockCatalog.GetKit(
                            _selectedSpecialization);
            }

            if (_selectedMode == GameModeId.HeroSiege)
            {
                return ModeAbilityKitCatalog.GetKit(
                    PlayableModeId.HeroSiege,
                    _selectedSpecialization);
            }

            return SpecializationCatalog.GetKit(_selectedSpecialization);
        }

        private void BindActivePlayerKit()
        {
            if (_selectedMode == GameModeId.Leveling && _levelingRun != null)
            {
                _hud?.BindLevelingKit(
                    _selectedSpecialization,
                    _levelingRun.Progression.Level,
                    _training != null &&
                    _training.CurrentStep == GuidedTrainingStep.FlickMastery);
                return;
            }

            if (_selectedMode == GameModeId.HeroSiege)
            {
                _hud?.BindModeKit(
                    PlayableModeId.HeroSiege,
                    _selectedSpecialization);
                return;
            }

            _hud?.BindSpecializationKit(_selectedSpecialization);
        }

        private string BuildLevelUpNotice(int level)
        {
            if (LevelingAbilityUnlockCatalog.TryGetUnlockSlotForLevel(
                    level,
                    out int unlockSlot) &&
                AbilityCatalog.TryGet(
                    LevelingAbilityUnlockCatalog.GetAbility(
                        _selectedSpecialization,
                        unlockSlot),
                    out AbilityDefinition unlocked))
            {
                return "LEVEL " + level + "  •  " +
                    unlocked.DisplayName.ToUpperInvariant() + " UNLOCKED";
            }

            return "LEVEL " + level + "  •  VITALITY RESTORED";
        }

        private void RefreshTrainingRecommendation()
        {
            _hud?.SetTrainingRecommendation(IsTrainingRecommended());
        }

        private void ApplyInitialTrainingRecommendation()
        {
            if (!IsTrainingRecommended())
            {
                return;
            }

            _selectedMode = GameModeId.Leveling;
            _hud?.SetSelectedMode(PrototypeMode.Training);
        }

        private bool IsTrainingRecommended() =>
            _productServices == null ||
            _productServices.ShouldRecommendTraining(_selectedSpecialization);

        private void BindKitViewer()
        {
            if (_kitViewer == null)
            {
                return;
            }

            PlayableModeId mode = _selectedMode == GameModeId.HeroSiege
                ? PlayableModeId.HeroSiege
                : _selectedMode == GameModeId.Dungeon ||
                  _selectedMode == GameModeId.Boss
                    ? PlayableModeId.Dungeon
                    : PlayableModeId.Arena;
            _kitViewer.Bind(mode, _selectedSpecialization);
        }

        private string BuildResultRecap(string recap)
        {
            int tickRate = _rules.TickRate > 0 ? _rules.TickRate : 30;
            int elapsedTicks = _flow != null ? _flow.ViewState.ElapsedTicks : 0;
            int elapsedSeconds = Mathf.Max(0, elapsedTicks / tickRate);
            string specialization = SpecializationCatalog
                .GetDefinition(_selectedSpecialization)
                .DisplayName
                .ToUpperInvariant();
            return DescribeMode(_selectedMode) + "  •  " + specialization +
                "  •  " + (elapsedSeconds / 60) + ":" +
                (elapsedSeconds % 60).ToString("00") + "\n" + recap;
        }

        private static string BuildReplayAction(GameModeId mode)
        {
            switch (mode)
            {
                case GameModeId.HeroSiege:
                    return "REPLAY HERO SIEGE";
                case GameModeId.Dungeon:
                case GameModeId.Boss:
                    return "REPLAY DUNGEON";
                default:
                    return "PLAY ARENA AGAIN";
            }
        }

        private static string DescribeMode(GameModeId mode)
        {
            switch (mode)
            {
                case GameModeId.Leveling:
                    return "TRAINING";
                case GameModeId.PveLeveling:
                    return "PVE LEVELING";
                case GameModeId.HeroSiege:
                    return "HERO SIEGE";
                case GameModeId.Dungeon:
                case GameModeId.Boss:
                    return "DUNGEON";
                default:
                    return "2 v 2 ARENA";
            }
        }

        private void FinishSessionRecord(SessionRecordOutcome outcome)
        {
            if (!_sessionRecordOpen)
            {
                return;
            }

            SessionRecordMode mode = ToSessionRecordMode(_selectedMode);
            if (mode == SessionRecordMode.Training &&
                outcome != SessionRecordOutcome.Completed &&
                outcome != SessionRecordOutcome.Skipped)
            {
                outcome = SessionRecordOutcome.Abandoned;
            }

            int simulationTick = _simulation != null
                ? _simulation.CurrentTick
                : 0;
            int durationTicks = _flow != null
                ? _flow.ViewState.ElapsedTicks
                : 0;
            _productServices?.FinishSession(
                mode,
                _selectedSpecialization,
                simulationTick,
                durationTicks,
                outcome);
            _sessionRecordOpen = false;
        }

        private void AbandonActiveRun()
        {
            int simulationTick = _simulation != null
                ? _simulation.CurrentTick
                : 0;
            int runTick = _flow != null ? _flow.ViewState.ElapsedTicks : 0;
            _siegeRun?.TryAbandon(simulationTick);
            _dungeonRun?.TryAbandon(runTick);
        }

        private static string DescribeRunResult(RunResult result)
        {
            if (result.DidWin)
            {
                return "All four dungeon floors cleared";
            }

            switch (result.Reason)
            {
                case RunEndReason.ObjectiveDefeated:
                    return "The defense core was destroyed";
                case RunEndReason.PartyDefeated:
                    return "The defending party was defeated";
                case RunEndReason.Abandoned:
                    return "The run was abandoned";
                default:
                    return "The run failed";
            }
        }

        private static string DescribeSiegeResult(SiegeRunResult result)
        {
            if (result.DidWin)
            {
                return "The stronghold held through all " +
                    HeroSiegeRunCatalog.WaveCount + " timed waves  •  " +
                    result.Kills + " KILLS  •  " + result.Gold + " GOLD";
            }

            switch (result.Reason)
            {
                case SiegeRunEndReason.ObjectiveDefeated:
                    return "The stronghold core was destroyed";
                case SiegeRunEndReason.DefendersDefeated:
                    return "The defense line was defeated";
                case SiegeRunEndReason.Abandoned:
                    return "The defense was abandoned";
                default:
                    return "The stronghold fell";
            }
        }

        private static SessionRecordMode ToSessionRecordMode(GameModeId mode)
        {
            switch (mode)
            {
                case GameModeId.Leveling:
                    return SessionRecordMode.Training;
                case GameModeId.PveLeveling:
                    return SessionRecordMode.PveLeveling;
                case GameModeId.HeroSiege:
                    return SessionRecordMode.HeroSiege;
                case GameModeId.Dungeon:
                case GameModeId.Boss:
                    return SessionRecordMode.Dungeon;
                default:
                    return SessionRecordMode.Arena;
            }
        }

        private string DescribeTrainingStep(GuidedTrainingStep step)
        {
            switch (step)
            {
                case GuidedTrainingStep.Movement:
                    return "MOVE WITH THE LEFT STICK";
                case GuidedTrainingStep.Targeting:
                    return "TAP AN ENEMY TO FOCUS";
                case GuidedTrainingStep.BasicCombat:
                    return "CAST " + TrainingAbilityName(
                        GuidedTrainingAbilityCatalog.GetBasicCombatAbility(
                            _selectedSpecialization));
                case GuidedTrainingStep.HazardRecovery:
                    return "ESCAPE THE LAVA OR KNOCKBACK ZONE";
                case GuidedTrainingStep.AbilityProgression:
                    return "CLEAR PACKS TO LEARN ALL 7 ACTIONS";
                case GuidedTrainingStep.RecoveryDefense:
                    return "USE " + TrainingAbilityName(
                        FindTrainingAbility(true)) + " TO RECOVER OR DEFEND";
                case GuidedTrainingStep.ControlInterrupt:
                    return "STOP AN ENEMY WITH " + TrainingAbilityName(
                        FindTrainingAbility(false));
                case GuidedTrainingStep.FinalPack:
                    return "CLEAR THE FINAL PACK WITH ALL 7 ACTIONS";
                case GuidedTrainingStep.FlickMastery:
                    return "HOLD " + TrainingAbilityName(
                        LevelingAbilityUnlockCatalog.GetAbility(
                            _selectedSpecialization,
                            0)) + ", DRAG UP TO " + TrainingAbilityName(
                        LevelingAbilityUnlockCatalog.GetAbility(
                            _selectedSpecialization,
                            LevelingAbilityUnlockCatalog.LevelFiveUnlockSlot)) +
                        ", RELEASE";
                default:
                    return "TRAINING";
            }
        }

        private AbilityId FindTrainingAbility(bool recoveryOrDefense)
        {
            for (int slot = 0;
                 slot < LevelingAbilityUnlockCatalog.TotalAbilityCount;
                 slot++)
            {
                AbilityId ability = LevelingAbilityUnlockCatalog.GetAbility(
                    _selectedSpecialization,
                    slot);
                bool matches = recoveryOrDefense
                    ? GuidedTrainingAbilityCatalog.IsRecoveryOrDefense(ability)
                    : GuidedTrainingAbilityCatalog.IsControlOrInterrupt(ability);
                if (matches)
                {
                    return ability;
                }
            }

            return AbilityId.None;
        }

        private static string TrainingAbilityName(AbilityId ability) =>
            AbilityCatalog.TryGet(ability, out AbilityDefinition definition)
                ? definition.DisplayName.ToUpperInvariant()
                : "THE HIGHLIGHTED ACTION";

        private static string DescribeDungeonFloor(int floorIndex)
        {
            if (floorIndex < 0 || floorIndex >= DungeonRunCatalog.FloorCount)
            {
                return "PREPARING";
            }

            switch (DungeonRunCatalog.GetFloorKind(floorIndex))
            {
                case DungeonFloorKind.TraversalPack:
                    return "ASHEN TRAVERSAL";
                case DungeonFloorKind.EliteArena:
                    return "ELITE ARENA";
                case DungeonFloorKind.MechanicsEncounter:
                    return "MECHANICS CHAMBER";
                default:
                    return "RIFT WARDEN";
            }
        }

        private static AbilitySlotDirection ResolveFlickDirection(
            Vector2 displacement)
        {
            if (displacement.sqrMagnitude <= 0.001f)
            {
                return AbilitySlotDirection.None;
            }

            // Match AbilityButton's deterministic sector rule exactly so a perfect
            // diagonal credits the same Training direction that was actually cast.
            if (Mathf.Abs(displacement.x) >= Mathf.Abs(displacement.y))
            {
                return displacement.x >= 0f
                    ? AbilitySlotDirection.Right
                    : AbilitySlotDirection.Left;
            }

            return displacement.y >= 0f
                ? AbilitySlotDirection.Up
                : AbilitySlotDirection.Down;
        }

        private void SetEnvironment(GameModeId? mode)
        {
            if (!mode.HasValue) { _cameraRig?.SetGameplayComposition(GameModeId.Arena, false); }
            bool usesWorldRoute = UsesWorldRoute(mode);
            _worldLighting?.SetWorldLighting(usesWorldRoute, mode == GameModeId.PveLeveling);
            if (_arenaContentLoader != null)
            {
                if (!mode.HasValue || usesWorldRoute)
                {
                    _arenaContentLoader.Unload();
                }
                else
                {
                    ArenaContentMode contentMode = ResolveArenaContentMode(
                        mode.Value);
                    if (!_arenaContentLoader.TryLoadDefault(
                            contentMode,
                            out string loadError))
                    {
                        throw new InvalidOperationException(loadError);
                    }
                }

                SetLevelingEnvironment(mode);
                return;
            }

            if (_arenaEnvironment != null)
            {
                _arenaEnvironment.SetActive(
                    mode.HasValue && !usesWorldRoute &&
                    ResolveArenaContentMode(mode.Value) ==
                        ArenaContentMode.Arena);
            }

            if (_bossEnvironment != null)
            {
                _bossEnvironment.SetActive(
                    mode.HasValue && !usesWorldRoute &&
                    ResolveArenaContentMode(mode.Value) ==
                        ArenaContentMode.Boss);
            }

            SetLevelingEnvironment(mode);
        }

        private void SetLevelingEnvironment(GameModeId? mode)
        {
            if (_worldStreamer == null)
            {
                return;
            }

            if (UsesWorldRoute(mode))
            {
                LevelingZoneDefinition zone = LevelingZoneCatalog.AshenCauseway;
                bool isBasin = mode == GameModeId.PveLeveling;
                Float3 start = isBasin
                    ? _grindingCaptureSpawn ?? PveRegionLayout.Refuge
                    : new Float3(-182f, 0f, ArenaCollisionMap.LevelingCenterZ);
                if (!_worldStreamer.ActivateZone(
                        isBasin ? PveRegionLayout.WorldIdValue : zone.World.Value,
                        isBasin ? PveRegionLayout.ZoneIdValue : zone.ContentZone.Value,
                        new Vector3(start.X, start.Y, start.Z)))
                {
                    throw new InvalidOperationException(
                        "Could not activate leveling world content: " +
                        _worldStreamer.LastError);
                }
            }
            else
            {
                _worldStreamer.DeactivateZone();
            }
        }

        private void UpdateLevelingWorldFocus()
        {
            if (!UsesWorldRoute(_selectedMode) ||
                _worldStreamer == null ||
                _simulation == null ||
                !_simulation.TryGetSnapshot(
                    _humanId,
                    out CombatantSnapshot human))
            {
                return;
            }

            _worldStreamer.SetFocus(new Vector3(
                human.Position.X,
                human.Position.Y,
                human.Position.Z));
        }

        private bool UsesWorldRoute(GameModeId? mode)
        {
            if (mode == GameModeId.Leveling || mode == GameModeId.PveLeveling)
            {
                return true;
            }

            if (mode != GameModeId.Dungeon || _dungeonRun == null)
            {
                return false;
            }

            return _dungeonRun.State.CurrentEncounterIndex <= 0;
        }

        private ArenaContentMode ResolveArenaContentMode(GameModeId mode)
        {
            if (mode == GameModeId.Arena)
            {
                return ArenaContentMode.Arena;
            }

            if (mode == GameModeId.HeroSiege)
            {
                return ArenaContentMode.Siege;
            }

            if (mode == GameModeId.Dungeon && _dungeonRun != null)
            {
                int floorIndex = _dungeonRun.State.CurrentEncounterIndex;
                if (floorIndex == 1 || floorIndex == 2)
                {
                    return ArenaContentMode.Arena;
                }
            }

            return ArenaContentMode.Boss;
        }

        private void DeactivateCombatants()
        {
            for (int index = 0; index < _combatantViews.Length; index++)
            {
                if (_combatantViews[index] != null)
                {
                    _combatantViews[index].ReleaseToPool();
                }
            }
        }

        private CombatantView FindView(ParticipantId id)
        {
            int index = FindViewIndex(id);
            return index >= 0 ? _combatantViews[index] : null;
        }

        private int FindViewIndex(ParticipantId id)
        {
            for (int index = 0; index < _combatantViews.Length; index++)
            {
                if (_combatantViews[index] != null && _combatantViews[index].Id == id)
                {
                    return index;
                }
            }

            return -1;
        }

        private string DescribeParticipant(ParticipantId id)
        {
            if (_simulation != null && _simulation.TryGetSnapshot(id, out CombatantSnapshot snapshot))
            {
                if (snapshot.Class == ClassId.Boss)
                {
                    return "Rift Warden";
                }

                string owner = id == _humanId ? "You" : snapshot.Team.Value == 1 ? "Ally" : "Enemy";
                return owner + " " + snapshot.Class;
            }

            return "Combatant";
        }

        private string DescribeFighterName(CombatantSnapshot snapshot, bool isHuman)
        {
            if (_grindingRun != null && !isHuman &&
                _grindingRun.TryGetMob(snapshot.Id, out GrindingMobDefinition grindingMob))
            {
                return DescribeGrindingMob(grindingMob);
            }

            if (snapshot.Class == ClassId.Boss)
            {
                return "Rift Warden";
            }

            if (isHuman)
            {
                return "Player";
            }

            if (_selectedMode == GameModeId.Leveling &&
                _levelingRun != null &&
                _levelingRun.Zone.TryGetMob(
                    snapshot.Id,
                    out LevelingMobDefinition mob))
            {
                return LevelingMobArchetypeCatalog.Get(mob.Archetype).DisplayName;
            }

            return snapshot.Team.Value == 1 ? "Party member" : "Opponent";
        }

        private static string DescribeRelation(CombatantSnapshot snapshot, bool isHuman)
        {
            if (isHuman)
            {
                return "YOU";
            }

            if (snapshot.Class == ClassId.Boss)
            {
                return "BOSS";
            }

            return snapshot.Team.Value == 1 ? "ALLY" : "ENEMY";
        }

        private static string DescribeStatus(CombatantSnapshot snapshot)
        {
            if (snapshot.IsInLava)
            {
                return "LAVA";
            }

            if (snapshot.HasStatus(StatusId.Stunned))
            {
                return "STUNNED";
            }

            if (snapshot.HasStatus(StatusId.Feared))
            {
                return "FEARED";
            }

            if (snapshot.HasStatus(StatusId.Horrified))
            {
                return "HORRIFIED";
            }

            if (snapshot.HasStatus(StatusId.Disoriented))
            {
                return "DISORIENTED";
            }

            if (snapshot.HasAuraTag(AuraTag.Frozen))
            {
                return "FROZEN";
            }

            if (snapshot.HasStatus(StatusId.Rooted))
            {
                return "ROOTED";
            }

            if (snapshot.HasStatus(StatusId.Silenced))
            {
                return "SILENCED";
            }

            if (snapshot.HasStatus(StatusId.Disarmed))
            {
                return "DISARMED";
            }

            if (snapshot.HasStatus(StatusId.Pacified))
            {
                return "PACIFIED";
            }

            if (snapshot.HasStatus(StatusId.Slowed))
            {
                return "SLOWED";
            }

            if (snapshot.HasStatus(StatusId.Anchored))
            {
                return "BRACED";
            }

            if (snapshot.HasStatus(StatusId.DamageReduced))
            {
                return "PROTECTED";
            }

            if (snapshot.HasStatus(StatusId.Stealthed))
            {
                return "STEALTH";
            }

            if (snapshot.HasStatus(StatusId.Burning))
            {
                return "BURNING";
            }

            if (snapshot.HasStatus(StatusId.Poisoned))
            {
                return "POISONED";
            }

            return snapshot.HasStatus(StatusId.Barrier) || snapshot.Barrier >= 0.5f
                ? "WARDED"
                : string.Empty;
        }

        private static string DescribeCastRejection(string reason)
        {
            switch (reason)
            {
                case "cooldown":
                    return "ABILITY RECHARGING";
                case "silenced":
                    return "SILENCED";
                case "already_casting":
                    return "ALREADY CASTING";
                case "invalid_target":
                    return "NO VALID TARGET";
                case "target_hidden":
                    return "TARGET IS HIDDEN";
                case "out_of_range":
                    return "OUT OF RANGE";
                case "too_close":
                case "minimum_range":
                    return "CREATE DISTANCE";
                case "line_of_sight":
                    return "LINE OF SIGHT BLOCKED";
                case "not_facing":
                    return "FACE THE TARGET";
                case "ability_rate":
                    return "ABILITY NOT READY";
                case "level_locked":
                    return "ABILITY NOT YET LEARNED";
                case "global_cooldown":
                    return "GLOBAL COOLDOWN";
                case "resource":
                    return "NOT ENOUGH RESOURCE";
                case "school_locked":
                    return "SPELL SCHOOL LOCKED";
                case "stunned":
                    return "STUNNED";
                case "feared":
                    return "FEARED";
                case "disoriented":
                    return "DISORIENTED";
                case "horrified":
                    return "HORRIFIED";
                case "disarmed":
                    return "WEAPON DISARMED";
                case "controlled":
                    return "CANNOT ACT";
                case "pacified":
                    return "DEFENSIVE FORM ACTIVE";
                case "moving":
                    return "STOP MOVING TO CAST";
                case "requirement":
                    return "CONDITION NOT MET";
                case "target_not_casting":
                    return "TARGET IS NOT CASTING";
                case "requires_stealth":
                    return "REQUIRES STEALTH";
                case "in_combat":
                    return "DEFEAT ACTIVE ENEMIES FIRST";
                case "shadowform":
                    return "LEAVE SHADOWFORM TO HEAL";
                case "requires_behind":
                    return "GET BEHIND TARGET";
                case "combo_points":
                    return "BUILD COMBO POINTS";
                case "target_not_poisoned":
                    return "TARGET MUST BE POISONED";
                case "target_not_frozen":
                    return "TARGET MUST BE FROZEN";
                case "weakened_soul":
                    return "WEAKENED SOUL";
                case "target_health":
                case "target_health_above_twenty_percent":
                    return "TARGET ABOVE 20%";
                case "target_has_no_mana":
                    return "TARGET HAS NO MANA";
                case "companion_unavailable":
                    return "PET UNAVAILABLE";
                case "companion_controlled":
                    return "PET IS CONTROLLED";
                case "companion_out_of_range":
                    return "PET OUT OF RANGE";
                case "companion_line_of_sight":
                    return "PET LINE OF SIGHT";
                case "dead":
                    return "DEFEATED";
                default:
                    return "CAST FAILED";
            }
        }

        private static AbilityReadinessState ResolveReadinessState(string reason)
        {
            switch (reason)
            {
                case "cooldown":
                    return AbilityReadinessState.Cooldown;
                case "global_cooldown":
                case "ability_rate":
                    return AbilityReadinessState.GlobalCooldown;
                case "invalid_target":
                    return AbilityReadinessState.NoTarget;
                case "target_hidden":
                    return AbilityReadinessState.NoTarget;
                case "out_of_range":
                case "too_close":
                case "minimum_range":
                    return AbilityReadinessState.OutOfRange;
                case "line_of_sight":
                    return AbilityReadinessState.MissingSetup;
                case "resource":
                    return AbilityReadinessState.InsufficientResource;
                case "moving":
                    return AbilityReadinessState.Moving;
                case "already_casting":
                    return AbilityReadinessState.Casting;
                case "silenced":
                case "school_locked":
                case "stunned":
                case "feared":
                case "disoriented":
                case "horrified":
                case "disarmed":
                case "controlled":
                case "pacified":
                    return AbilityReadinessState.Controlled;
                case "dead":
                    return AbilityReadinessState.Dead;
                case "level_locked":
                    return AbilityReadinessState.Unavailable;
                case "target_not_casting":
                case "requires_stealth":
                case "in_combat":
                case "shadowform":
                case "requires_behind":
                case "combo_points":
                case "target_not_poisoned":
                case "target_not_frozen":
                case "weakened_soul":
                case "not_facing":
                case "target_health":
                case "target_health_above_twenty_percent":
                case "target_has_no_mana":
                case "companion_unavailable":
                case "companion_controlled":
                case "companion_out_of_range":
                case "companion_line_of_sight":
                    return AbilityReadinessState.MissingSetup;
                default:
                    return AbilityReadinessState.Unavailable;
            }
        }

        private static string DescribeReadinessBadge(string reason, ResourceType resourceType)
        {
            switch (reason)
            {
                case "invalid_target":
                    return "NO TARGET";
                case "target_hidden":
                    return "HIDDEN";
                case "out_of_range":
                    return "RANGE";
                case "too_close":
                case "minimum_range":
                    return "TOO CLOSE";
                case "line_of_sight":
                    return "LOS";
                case "not_facing":
                    return "FACE";
                case "resource":
                    return resourceType == ResourceType.Energy
                        ? "LOW ENERGY"
                        : "LOW MANA";
                case "moving":
                    return "STOP";
                case "already_casting":
                    return "CASTING";
                case "silenced":
                    return "SILENCE";
                case "school_locked":
                    return "LOCKED";
                case "level_locked":
                    return "LOCKED";
                case "stunned":
                    return "STUN";
                case "feared":
                    return "FEAR";
                case "disoriented":
                    return "CONTROL";
                case "horrified":
                    return "HORROR";
                case "disarmed":
                    return "DISARMED";
                case "pacified":
                    return "PACIFIED";
                case "target_not_casting":
                    return "NO CAST";
                case "requires_stealth":
                    return "NEEDS STEALTH";
                case "in_combat":
                    return "IN COMBAT";
                case "shadowform":
                    return "LEAVE FORM";
                case "requires_behind":
                    return "BEHIND";
                case "combo_points":
                    return "NEEDS COMBO";
                case "target_not_poisoned":
                    return "NEEDS POISON";
                case "target_not_frozen":
                    return "NEEDS FROZEN";
                case "weakened_soul":
                    return "WEAK SOUL";
                case "target_health":
                case "target_health_above_twenty_percent":
                    return "ABOVE 20%";
                case "target_has_no_mana":
                    return "NO MANA";
                case "companion_unavailable":
                    return "NO PET";
                case "companion_controlled":
                    return "PET CC";
                case "companion_out_of_range":
                    return "PET RANGE";
                case "companion_line_of_sight":
                    return "PET LOS";
                case "dead":
                    return "DEAD";
                default:
                    return string.Empty;
            }
        }

        private static Color ResolveCastRejectionColor(string reason)
        {
            AbilityReadinessState state = ResolveReadinessState(reason);
            switch (state)
            {
                case AbilityReadinessState.Cooldown:
                case AbilityReadinessState.GlobalCooldown:
                case AbilityReadinessState.InsufficientResource:
                    return new Color(1f, 0.72f, 0.24f, 1f);
                case AbilityReadinessState.MissingSetup:
                    return new Color(0.5f, 0.86f, 1f, 1f);
                default:
                    return new Color(1f, 0.34f, 0.25f, 1f);
            }
        }

        private SpecializationId GetOpponentSpecialization(ClassId classId)
        {
            if (classId == ClassId.Rogue)
            {
                return _selectedSpecialization == SpecializationId.Mutilate
                    ? SpecializationId.ShadowDance
                    : SpecializationId.Mutilate;
            }

            if (classId == ClassId.Mage)
            {
                return _selectedSpecialization == SpecializationId.Fire
                    ? SpecializationId.Frost
                    : SpecializationId.Fire;
            }

            if (classId == ClassId.Healer)
            {
                return _selectedSpecialization == SpecializationId.Shadow
                    ? SpecializationId.Discipline
                    : SpecializationId.Shadow;
            }

            if (classId == ClassId.Hunter)
            {
                return SpecializationId.Marksmanship;
            }

            return SpecializationCatalog.GetDefault(classId);
        }

        private static AbilityTargetRelation GetRelation(
            CombatantSnapshot actor,
            CombatantSnapshot target)
        {
            if (actor.Id == target.Id)
            {
                return AbilityTargetRelation.Self;
            }

            return actor.Team == target.Team
                ? AbilityTargetRelation.Friendly
                : AbilityTargetRelation.Hostile;
        }

        private bool IsHiddenFromTeam(
            CombatantSnapshot target,
            int viewerTeamValue)
        {
            return target.Team.Value != viewerTeamValue &&
                target.HasAuraTag(AuraTag.Stealth) &&
                !(_selectedClass == ClassId.Hunter &&
                  target.HasAuraTag(AuraTag.Marked));
        }

        private static CombatantControlVisualState GetControlVisualState(
            CombatantSnapshot snapshot)
        {
            CombatantControlVisualState state = CombatantControlVisualState.None;
            if (snapshot.HasStatus(StatusId.Feared))
            {
                state |= CombatantControlVisualState.Fear;
            }

            if (snapshot.HasStatus(StatusId.Horrified))
            {
                state |= CombatantControlVisualState.Horror;
            }

            if (snapshot.HasStatus(StatusId.Stunned))
            {
                state |= CombatantControlVisualState.Stun;
            }

            if (snapshot.HasStatus(StatusId.Rooted))
            {
                state |= CombatantControlVisualState.Root;
            }

            if (snapshot.HasStatus(StatusId.Disoriented))
            {
                state |= CombatantControlVisualState.Disorient;
            }

            if (snapshot.HasStatus(StatusId.Disarmed))
            {
                state |= CombatantControlVisualState.Disarm;
            }

            if (snapshot.HasStatus(StatusId.Pacified))
            {
                state |= CombatantControlVisualState.Pacify;
            }

            if (snapshot.HasStatus(StatusId.Stealthed))
            {
                state |= CombatantControlVisualState.Stealth;
            }

            return state;
        }

        private static Float3 ToFloat3(Vector3 value) => new Float3(value.x, value.y, value.z);

        private static Vector3 ToVector3(Float3 value) => new Vector3(value.X, value.Y, value.Z);

        private static PrototypeMode ToPrototypeMode(GameModeId mode)
        {
            switch (mode)
            {
                case GameModeId.Boss:
                    return PrototypeMode.BossTrio;
                case GameModeId.Leveling:
                    return PrototypeMode.Leveling;
                case GameModeId.PveLeveling:
                    return PrototypeMode.PveLeveling;
                case GameModeId.HeroSiege:
                    return PrototypeMode.HeroSiege;
                case GameModeId.Dungeon:
                    return PrototypeMode.Dungeon;
                default:
                    return PrototypeMode.Arena2V2;
            }
        }

        private readonly struct ResolvedCast
        {
            public ResolvedCast(
                AbilityId ability,
                ParticipantId target,
                Float3 point,
                Float3 direction)
            {
                Ability = ability;
                Target = target;
                Point = point;
                Direction = direction;
                IsValid = ability != AbilityId.None;
            }

            public AbilityId Ability { get; }

            public ParticipantId Target { get; }

            public Float3 Point { get; }

            public Float3 Direction { get; }

            public bool IsValid { get; }
        }
    }
}
