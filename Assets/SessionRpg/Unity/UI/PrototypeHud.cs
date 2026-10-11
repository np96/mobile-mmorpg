using System;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Presentation;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Unity.UI
{
    public enum HudAuraKind
    {
        Beneficial,
        Harmful,
        Control,
    }

    public readonly struct HudAuraIndicator
    {
        public HudAuraIndicator(
            string label,
            float remainingSeconds,
            int stacks = 0,
            HudAuraKind kind = HudAuraKind.Beneficial)
        {
            Label = label ?? string.Empty;
            RemainingSeconds = Mathf.Max(0f, remainingSeconds);
            Stacks = Mathf.Max(0, stacks);
            Kind = kind;
        }

        public string Label { get; }

        public float RemainingSeconds { get; }

        public int Stacks { get; }

        public HudAuraKind Kind { get; }

        public bool IsVisible => !string.IsNullOrWhiteSpace(Label);
    }

    public enum PrototypeHudScreen
    {
        Home,
        Gameplay,
        Results,
    }

    public enum PrototypeMode
    {
        None = 0,
        Arena2V2 = 1,
        Dungeon = 2,
        Training = 3,
        HeroSiege = 4,
        PveLeveling = 5,

        // Serialized/UI compatibility names from the combat-prototype slice.
        BossTrio = Dungeon,
        Leveling = Training,
    }

    [DisallowMultipleComponent]
    public sealed class PrototypeHud : MonoBehaviour
    {
        public const int TargetAuraSlotCount = 4;
        public const int MaximumFighterViewCount = 8;

        private const string InterruptibleCastLabel = "CAST  •  INTERRUPTIBLE";
        private const string ProtectedCastLabel = "CAST  •  PROTECTED";
        private const string InterruptibleChannelLabel = "CHANNEL  •  INTERRUPTIBLE";
        private const string ProtectedChannelLabel = "CHANNEL  •  PROTECTED";

        private static readonly Color HighContrastFriendlyColor =
            new Color(0f, 0.84f, 1f, 1f);
        private static readonly Color HighContrastHostileColor =
            new Color(1f, 0.42f, 0f, 1f);
        private static readonly Color HighContrastFriendlySurfaceColor =
            new Color(0.01f, 0.075f, 0.11f, 1f);
        private static readonly Color HighContrastHostileSurfaceColor =
            new Color(0.12f, 0.035f, 0.005f, 1f);
        private static readonly Color HighContrastBeneficialAuraColor =
            new Color(0f, 0.78f, 1f, 1f);
        private static readonly Color HighContrastHarmfulAuraColor =
            new Color(1f, 0.29f, 0.02f, 1f);
        private static readonly Color HighContrastControlAuraColor =
            new Color(0.84f, 0.28f, 1f, 1f);
        private static readonly Color HighContrastInterruptibleCastColor =
            new Color(1f, 0.86f, 0.08f, 1f);
        private static readonly Color HighContrastProtectedCastColor =
            new Color(0.82f, 0.91f, 1f, 1f);
        private static readonly Color HighContrastStatusColor = Color.white;

        private struct ResourcePresentationState
        {
            public bool IsInitialized;
            public ResourceType ResourceType;
            public int CurrentValue;
            public int MaximumValue;
        }

        private struct TargetDetailPresentationState
        {
            public bool IsInitialized;
            public int ParticipantId;
            public ClassId ClassId;
            public string DisplayName;
            public string Relation;
            public bool IsFriendly;
            public int HealthCurrent;
            public int HealthMaximum;
            public int HealthPercent;
            public int Barrier;
            public bool IsAlive;
            public string Status;
        }

        private struct TargetAuraPresentationState
        {
            public bool IsVisible;
            public string Label;
            public string UpperLabel;
            public int RemainingSeconds;
            public int Stacks;
            public HudAuraKind Kind;
        }

        [Header("Screens")]
        [SerializeField] private GameObject _homeRoot;
        [SerializeField] private GameObject _gameplayRoot;
        [SerializeField] private GameObject _resultsRoot;

        [Header("Home controls")]
        [SerializeField] private Button _healerButton;
        [SerializeField] private Button _rogueButton;
        [SerializeField] private Button _mageButton;
        [SerializeField] private Button _hunterButton;
        [SerializeField] private GameObject _healerSelectedMarker;
        [SerializeField] private GameObject _rogueSelectedMarker;
        [SerializeField] private GameObject _mageSelectedMarker;
        [SerializeField] private GameObject _hunterSelectedMarker;
        [SerializeField] private GameObject _disciplineSpecializationRoot;
        [SerializeField] private GameObject _rogueSpecializationRoot;
        [SerializeField] private GameObject _mageSpecializationRoot;
        [SerializeField] private GameObject _hunterSpecializationRoot;
        [SerializeField] private Button _disciplineButton;
        [SerializeField] private Button _shadowButton;
        [SerializeField] private Button _shadowDanceButton;
        [SerializeField] private Button _mutilateButton;
        [SerializeField] private Button _fireButton;
        [SerializeField] private Button _frostButton;
        [SerializeField] private Button _marksmanshipButton;
        [SerializeField] private GameObject _disciplineSelectedMarker;
        [SerializeField] private GameObject _shadowSelectedMarker;
        [SerializeField] private GameObject _shadowDanceSelectedMarker;
        [SerializeField] private GameObject _mutilateSelectedMarker;
        [SerializeField] private GameObject _fireSelectedMarker;
        [SerializeField] private GameObject _frostSelectedMarker;
        [SerializeField] private GameObject _marksmanshipSelectedMarker;
        [SerializeField] private Button _arenaModeButton;
        [SerializeField] private Button _siegeModeButton;
        [SerializeField] private Button _bossModeButton;
        [SerializeField] private Button _levelingModeButton;
        [SerializeField] private Button _pveLevelingModeButton;
        [SerializeField] private GameObject _pveLevelingSelectedMarker;
        [SerializeField] private GameObject _arenaSelectedMarker;
        [SerializeField] private GameObject _siegeSelectedMarker;
        [SerializeField] private GameObject _bossSelectedMarker;
        [SerializeField] private GameObject _levelingSelectedMarker;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _skipTrainingButton;
        [SerializeField] private Text _startButtonLabel;
        [SerializeField] private Text _trainingModeLabel;
        [SerializeField] private Text _classDetailLabel;
        [SerializeField] private GameObject _siegeVariantRoot;
        [SerializeField] private Button _siegeSoloLaneButton;
        [SerializeField] private Button _siegeFourLanesButton;
        [SerializeField] private GameObject _siegeSoloLaneSelectedMarker;
        [SerializeField] private GameObject _siegeFourLanesSelectedMarker;

        [Header("Gameplay controls")]
        [SerializeField] private AbilitySlotView[] _abilitySlots = new AbilitySlotView[AbilityCatalog.AbilitySlotCount];
        [SerializeField] private AbilityIconCatalog _abilityIconCatalog;

        [Header("Gameplay status")]
        [SerializeField] private RectTransform _friendlyFighterRoot;
        [SerializeField] private RectTransform _hostileFighterRoot;
        [SerializeField] private CombatantStatusView[] _fighterViews =
            new CombatantStatusView[MaximumFighterViewCount];
        [SerializeField] private Text _healthLabel;
        [SerializeField] private Image _healthFill;
        [SerializeField] private Text _teamLabel;
        [SerializeField] private Text _objectiveLabel;
        [SerializeField] private GameObject _progressionRoot;
        [SerializeField] private Text _levelLabel;
        [SerializeField] private Image _experienceFill;
        [SerializeField] private Text _experienceLabel;
        [SerializeField] private bool _showDetailedTargetPanel;
        [SerializeField] private GameObject _targetRoot;
        [SerializeField] private Graphic _targetSurface;
        [SerializeField] private Graphic _targetRelationAccent;
        [SerializeField] private Text _targetParticipantLabel;
        [SerializeField] private Text _targetRelationLabel;
        [SerializeField] private Text _targetLabel;
        [SerializeField] private Image _targetHealthFill;
        [SerializeField] private Image _targetBarrierFill;
        [SerializeField] private Text _targetHealthLabel;
        [SerializeField] private Text _targetBarrierLabel;
        [SerializeField] private GameObject _targetResourceRoot;
        [SerializeField] private Image _targetResourceFill;
        [SerializeField] private Text _targetResourceTypeLabel;
        [SerializeField] private Text _targetResourceValueLabel;
        [SerializeField] private Text _targetStatusLabel;
        [SerializeField] private GameObject _targetCastRoot;
        [SerializeField] private Graphic _targetCastSurface;
        [SerializeField] private Text _targetCastLabel;
        [SerializeField] private Text _targetCastStateLabel;
        [SerializeField] private Image _targetCastFill;
        [SerializeField] private GameObject[] _targetAuraRoots =
            new GameObject[TargetAuraSlotCount];
        [SerializeField] private Graphic[] _targetAuraSurfaces =
            new Graphic[TargetAuraSlotCount];
        [SerializeField] private Text[] _targetAuraLabels =
            new Text[TargetAuraSlotCount];
        [SerializeField] private CanvasGroup _cameraOrbitHintCanvasGroup;
        [SerializeField] private CanvasGroup _combatNoticeCanvasGroup;
        [SerializeField] private Text _combatNoticeLabel;
        [SerializeField] private GameObject _castRoot;
        [SerializeField] private Text _castLabel;
        [SerializeField] private Image _castFill;
        [SerializeField] private GameObject _playerResourceRoot;
        [SerializeField] private Image _playerResourceFill;
        [SerializeField] private Text _playerResourceTypeLabel;
        [SerializeField] private Text _playerResourceValueLabel;
        [SerializeField] private GameObject _comboPointRoot;
        [SerializeField] private Graphic[] _comboPointPips = new Graphic[5];
        [SerializeField] private Text _comboPointLabel;

        [Header("Hero Siege supplies")]
        [SerializeField] private GameObject _siegeSupplyRoot;
        [SerializeField] private GameObject _siegePurchaseDrawer;
        [SerializeField] private Text _siegeGoldLabel;
        [SerializeField] private Text _siegeHealthPotionLabel;
        [SerializeField] private Text _siegeManaPotionLabel;
        [SerializeField] private Button _siegeShopButton;
        [SerializeField] private Text _siegeShopLabel;
        [SerializeField] private Button _siegeBuyHealthButton;
        [SerializeField] private Button _siegeUseHealthButton;
        [SerializeField] private Button _siegeBuyManaButton;
        [SerializeField] private Button _siegeUseManaButton;
        [SerializeField] private Text _siegeBuyHealthLabel;
        [SerializeField] private Text _siegeBuyManaLabel;

        [Header("Relation colors")]
        [SerializeField] private Color _friendlyColor = new Color(0.22f, 0.78f, 0.9f, 1f);
        [SerializeField] private Color _hostileColor = new Color(0.94f, 0.3f, 0.25f, 1f);
        [SerializeField] private Color _friendlySurfaceColor = new Color(0.06f, 0.15f, 0.2f, 0.96f);
        [SerializeField] private Color _hostileSurfaceColor = new Color(0.18f, 0.075f, 0.075f, 0.96f);
        [SerializeField] private Color _manaColor = new Color(0.23f, 0.52f, 0.96f, 1f);
        [SerializeField] private Color _energyColor = new Color(0.96f, 0.77f, 0.18f, 1f);
        [SerializeField] private Color _beneficialAuraColor = new Color(0.2f, 0.68f, 0.82f, 0.96f);
        [SerializeField] private Color _harmfulAuraColor = new Color(0.84f, 0.28f, 0.24f, 0.96f);
        [SerializeField] private Color _controlAuraColor = new Color(0.63f, 0.36f, 0.9f, 0.96f);
        [SerializeField] private Color _interruptibleCastColor = new Color(0.96f, 0.7f, 0.2f, 1f);
        [SerializeField] private Color _protectedCastColor = new Color(0.5f, 0.58f, 0.68f, 1f);

        [Header("Results")]
        [SerializeField] private Text _resultTitleLabel;
        [SerializeField] private Text _resultRecapLabel;
        [SerializeField] private Button _replayButton;
        [SerializeField] private Text _replayButtonLabel;
        [SerializeField] private Button _leaveButton;

        private PrototypeHudScreen _screen;
        private ClassId _selectedClass;
        private SpecializationId _selectedSpecialization;
        private SpecializationId _lastHealerSpecialization;
        private SpecializationId _lastRogueSpecialization;
        private SpecializationId _lastMageSpecialization;
        private SpecializationId _lastHunterSpecialization;
        private PrototypeMode _selectedMode;
        private SiegeVariantId _selectedSiegeVariant = SiegeVariantId.SoloLane;
        private int _focusedFighterId;
        private int _lastHealthCurrent = int.MinValue;
        private ResourcePresentationState _playerResourceState;
        private ResourcePresentationState _targetResourceState;
        private TargetDetailPresentationState _targetDetailState;
        private readonly TargetAuraPresentationState[] _targetAuraStates =
            new TargetAuraPresentationState[TargetAuraSlotCount];
        private float _combatNoticeRemaining;
        private float _combatNoticeFadeSeconds;
        private float _cameraOrbitHintRemaining;
        private bool _siegeShopOpen;
        private int _visibleComboPoints;
        private bool _comboTargetMatches;
        private bool _highContrastEnabled;
        private bool _targetCastVisible;
        private bool _targetCastInterruptible;
        private bool _trainingRecommended;
        private bool _siegeSupplyRequestedVisible;
        private Color? _defaultTargetStatusColor;
        private Color? _defaultTargetCastLabelColor;
        private Color? _defaultCastLabelColor;
        private Color? _defaultCastFillColor;

        public event Action<ClassId> ClassSelected;

        public event Action<SpecializationId> SpecializationSelected;

        public event Action<PrototypeMode> ModeSelected;

        public event Action<SiegeVariantId> SiegeVariantSelected;

        public event Action<SiegePotionId> SiegePotionPurchaseRequested;

        public event Action<SiegePotionId> SiegePotionUseRequested;

        public event Action StartRequested;

        public event Action SkipTrainingRequested;

        public event Action ReplayRequested;

        public event Action LeaveRequested;

        public event Action<int> FighterSelected;

        public PrototypeHudScreen Screen => _screen;

        public ClassId SelectedClass => _selectedClass;

        public SpecializationId SelectedSpecialization => _selectedSpecialization;

        public PrototypeMode SelectedMode => _selectedMode;

        public SiegeVariantId SelectedSiegeVariant => _selectedSiegeVariant;

        public bool IsSiegeVariantSelectionVisible =>
            _siegeVariantRoot != null && _siegeVariantRoot.activeInHierarchy;

        public bool IsSiegeSupplyVisible =>
            _siegeSupplyRoot != null && _siegeSupplyRoot.activeInHierarchy;

        public bool IsSiegeShopOpen =>
            _siegePurchaseDrawer != null &&
            _siegePurchaseDrawer.activeInHierarchy;

        public string SiegeGoldLabel => _siegeGoldLabel != null
            ? _siegeGoldLabel.text
            : string.Empty;

        public string SiegeHealthPotionLabel => _siegeHealthPotionLabel != null
            ? _siegeHealthPotionLabel.text
            : string.Empty;

        public string SiegeManaPotionLabel => _siegeManaPotionLabel != null
            ? _siegeManaPotionLabel.text
            : string.Empty;

        public bool HasAuthoredHeroSiegeContract =>
            _siegeModeButton != null &&
            _siegeVariantRoot != null &&
            _siegeSoloLaneButton != null &&
            _siegeFourLanesButton != null &&
            _siegeSoloLaneSelectedMarker != null &&
            _siegeFourLanesSelectedMarker != null &&
            _siegeSupplyRoot != null &&
            _siegePurchaseDrawer != null &&
            _siegeGoldLabel != null &&
            _siegeHealthPotionLabel != null &&
            _siegeManaPotionLabel != null &&
            _siegeShopButton != null &&
            _siegeShopLabel != null &&
            _siegeBuyHealthButton != null &&
            _siegeUseHealthButton != null &&
            _siegeBuyManaButton != null &&
            _siegeUseManaButton != null;

        public bool HasAuthoredLevelingContract =>
            _levelingModeButton != null &&
            _levelingSelectedMarker != null &&
            _progressionRoot != null &&
            _levelLabel != null &&
            _experienceFill != null &&
            _experienceLabel != null;

        public bool HasAuthoredPveLevelingContract =>
            _pveLevelingModeButton != null && _pveLevelingSelectedMarker != null;

        public bool IsLevelProgressionVisible => _progressionRoot != null &&
            _progressionRoot.activeInHierarchy;

        public string LevelProgressionLabel => _levelLabel != null
            ? _levelLabel.text
            : string.Empty;

        public string ExperienceProgressionLabel => _experienceLabel != null
            ? _experienceLabel.text
            : string.Empty;

        public int FocusedFighterId => _focusedFighterId;

        public bool ComboPointsVisible => _comboPointRoot != null &&
            _comboPointRoot.activeInHierarchy;

        public int VisibleComboPoints => _visibleComboPoints;

        public bool ComboTargetMatches => _comboTargetMatches;

        public bool HighContrastEnabled => _highContrastEnabled;

        public string StartActionLabel => _startButtonLabel != null
            ? _startButtonLabel.text
            : string.Empty;

        public string TrainingModeLabel => _trainingModeLabel != null
            ? _trainingModeLabel.text
            : string.Empty;

        public string ReplayActionLabel => _replayButtonLabel != null
            ? _replayButtonLabel.text
            : string.Empty;

        private void OnEnable()
        {
            CaptureDefaultAccessibilityColors();
            EnsureSelectedSpecialization();
            AddButtonListeners();
            RefreshSelectionMarkers();
            RefreshClassDetail();
            RefreshHomeActionLabels();
            RefreshSiegeSurfaceVisibility();
            BindSelectedKitIfValid();
        }

        private void OnDisable()
        {
            RemoveButtonListeners();
            _combatNoticeRemaining = 0f;
            _cameraOrbitHintRemaining = 0f;
            if (_combatNoticeCanvasGroup != null)
            {
                _combatNoticeCanvasGroup.alpha = 0f;
            }

            if (_cameraOrbitHintCanvasGroup != null)
            {
                _cameraOrbitHintCanvasGroup.alpha = 0f;
            }
        }

        private void Update()
        {
            if (_combatNoticeCanvasGroup != null && _combatNoticeRemaining > 0f)
            {
                _combatNoticeRemaining = Mathf.Max(
                    0f,
                    _combatNoticeRemaining - Time.unscaledDeltaTime);
                _combatNoticeCanvasGroup.alpha = _combatNoticeRemaining <= _combatNoticeFadeSeconds
                    ? _combatNoticeRemaining / Mathf.Max(0.01f, _combatNoticeFadeSeconds)
                    : 1f;
            }

            if (_cameraOrbitHintCanvasGroup != null &&
                _cameraOrbitHintRemaining > 0f)
            {
                _cameraOrbitHintRemaining = Mathf.Max(
                    0f,
                    _cameraOrbitHintRemaining - Time.unscaledDeltaTime);
                _cameraOrbitHintCanvasGroup.alpha = _cameraOrbitHintRemaining < 1f
                    ? _cameraOrbitHintRemaining
                    : 1f;
            }
        }

        public void ShowHome()
        {
            SetScreen(PrototypeHudScreen.Home);
        }

        public void ShowGameplay()
        {
            SetScreen(PrototypeHudScreen.Gameplay);
        }

        public void ShowResults(string title, string recap)
        {
            ShowResults(title, recap, "PLAY AGAIN");
        }

        public void ShowResults(string title, string recap, string replayAction)
        {
            SetResult(title, recap, replayAction);
            SetScreen(PrototypeHudScreen.Results);
        }

        public void SetScreen(PrototypeHudScreen screen)
        {
            _screen = screen;
            SetActiveIfNeeded(_homeRoot, screen == PrototypeHudScreen.Home);
            SetActiveIfNeeded(_gameplayRoot, screen == PrototypeHudScreen.Gameplay);
            SetActiveIfNeeded(_resultsRoot, screen == PrototypeHudScreen.Results);
            if (_cameraOrbitHintCanvasGroup != null)
            {
                bool showOrbitHint = screen == PrototypeHudScreen.Gameplay;
                _cameraOrbitHintRemaining = showOrbitHint ? 4f : 0f;
                _cameraOrbitHintCanvasGroup.alpha = showOrbitHint ? 1f : 0f;
            }
            RefreshSiegeSurfaceVisibility();
        }

        public void SetSelectedClass(ClassId classId, bool notify = false)
        {
            SpecializationId previousSpecialization = _selectedSpecialization;
            _selectedClass = classId;
            _selectedSpecialization = GetRememberedSpecialization(classId);
            RefreshSelectionMarkers();
            RefreshClassDetail();
            BindSelectedKitIfValid();
            if (notify)
            {
                ClassSelected?.Invoke(_selectedClass);
                if (_selectedSpecialization != previousSpecialization)
                {
                    SpecializationSelected?.Invoke(_selectedSpecialization);
                }
            }
        }

        public void SetSelectedSpecialization(
            SpecializationId specializationId,
            bool notify = false)
        {
            if (!SpecializationCatalog.IsValidForClass(_selectedClass, specializationId))
            {
                return;
            }

            bool changed = _selectedSpecialization != specializationId;
            _selectedSpecialization = specializationId;
            RememberSpecialization(_selectedClass, specializationId);
            RefreshSelectionMarkers();
            RefreshClassDetail();
            BindSelectedKitIfValid();
            if (notify && changed)
            {
                SpecializationSelected?.Invoke(_selectedSpecialization);
            }
        }

        public void SetSelectedMode(PrototypeMode mode, bool notify = false)
        {
            _selectedMode = mode;
            RefreshSelectionMarkers();
            RefreshHomeActionLabels();
            RefreshSiegeSurfaceVisibility();
            if (notify)
            {
                ModeSelected?.Invoke(_selectedMode);
            }
        }

        public void SetSelectedSiegeVariant(
            SiegeVariantId variant,
            bool notify = false)
        {
            if (variant != SiegeVariantId.SoloLane &&
                variant != SiegeVariantId.FourLanes)
            {
                return;
            }

            bool changed = _selectedSiegeVariant != variant;
            _selectedSiegeVariant = variant;
            RefreshSelectionMarkers();
            RefreshHomeActionLabels();
            if (notify && changed)
            {
                SiegeVariantSelected?.Invoke(_selectedSiegeVariant);
            }
        }

        public void SetSiegeSupplyState(
            bool show,
            int gold,
            int healthPotionCount,
            int manaPotionCount,
            int healthPotionCost,
            int manaPotionCost,
            bool canBuyHealth,
            bool canUseHealth,
            bool canBuyMana,
            bool canUseMana)
        {
            _siegeSupplyRequestedVisible = show;
            if (!show)
            {
                _siegeShopOpen = false;
            }

            SetTextIfChanged(_siegeGoldLabel, "G " + Mathf.Max(0, gold));
            SetTextIfChanged(
                _siegeHealthPotionLabel,
                "HP ×" + Mathf.Max(0, healthPotionCount));
            SetTextIfChanged(
                _siegeManaPotionLabel,
                "MP ×" + Mathf.Max(0, manaPotionCount));
            SetTextIfChanged(
                _siegeBuyHealthLabel,
                "BUY HP  •  " + Mathf.Max(0, healthPotionCost) + "G");
            SetTextIfChanged(
                _siegeBuyManaLabel,
                "BUY MANA  •  " + Mathf.Max(0, manaPotionCost) + "G");

            if (_siegeBuyHealthButton != null)
            {
                _siegeBuyHealthButton.interactable = canBuyHealth;
            }

            if (_siegeUseHealthButton != null)
            {
                _siegeUseHealthButton.interactable = canUseHealth;
            }

            if (_siegeBuyManaButton != null)
            {
                _siegeBuyManaButton.interactable = canBuyMana;
            }

            if (_siegeUseManaButton != null)
            {
                _siegeUseManaButton.interactable = canUseMana;
            }

            RefreshSiegeSurfaceVisibility();
        }

        public void SetStartInteractable(bool isInteractable)
        {
            if (_startButton != null)
            {
                _startButton.interactable = isInteractable;
            }
        }

        public void SetTrainingRecommendation(bool recommended)
        {
            _trainingRecommended = recommended;
            SetActiveIfNeeded(
                _skipTrainingButton != null ? _skipTrainingButton.gameObject : null,
                recommended);
            RefreshHomeActionLabels();
        }

        /// <summary>
        /// Applies a brighter blue/orange relation palette plus stronger cast,
        /// aura, and status contrast. Text labels remain the primary semantic
        /// signal, so the setting never makes color the only differentiator.
        /// Existing cards and target details are repainted immediately.
        /// </summary>
        public void SetHighContrast(bool enabled)
        {
            CaptureDefaultAccessibilityColors();
            _highContrastEnabled = enabled;

            int fighterCount = _fighterViews != null ? _fighterViews.Length : 0;
            for (int index = 0; index < fighterCount; index++)
            {
                _fighterViews[index]?.SetHighContrast(enabled);
            }

            if (_targetDetailState.IsInitialized)
            {
                Color relationColor = ResolveRelationColor(
                    _targetDetailState.IsFriendly);
                if (_targetRelationAccent != null)
                {
                    _targetRelationAccent.color = relationColor;
                }

                if (_targetRelationLabel != null)
                {
                    _targetRelationLabel.color = relationColor;
                }

                if (_targetSurface != null)
                {
                    _targetSurface.color = ResolveRelationSurfaceColor(
                        _targetDetailState.IsFriendly);
                }
            }

            if (_targetStatusLabel != null)
            {
                _targetStatusLabel.color = enabled
                    ? HighContrastStatusColor
                    : _defaultTargetStatusColor ?? Color.white;
            }

            if (_targetCastLabel != null)
            {
                _targetCastLabel.color = enabled
                    ? Color.white
                    : _defaultTargetCastLabelColor ?? Color.white;
            }

            if (_targetCastVisible)
            {
                ApplyTargetCastColors(_targetCastInterruptible);
            }

            for (int slot = 0; slot < TargetAuraSlotCount; slot++)
            {
                if (!_targetAuraStates[slot].IsVisible ||
                    _targetAuraSurfaces == null ||
                    slot >= _targetAuraSurfaces.Length ||
                    _targetAuraSurfaces[slot] == null)
                {
                    continue;
                }

                _targetAuraSurfaces[slot].color = ResolveAuraColor(
                    _targetAuraStates[slot].Kind);
            }

            if (_castLabel != null)
            {
                _castLabel.color = enabled
                    ? HighContrastInterruptibleCastColor
                    : _defaultCastLabelColor ?? Color.white;
            }

            if (_castFill != null)
            {
                _castFill.color = enabled
                    ? HighContrastInterruptibleCastColor
                    : _defaultCastFillColor ?? Color.white;
            }
        }

        public void SetAbilityControlsInteractable(
            bool isInteractable,
            string blockedReason = null)
        {
            int slotCount = _abilitySlots != null ? _abilitySlots.Length : 0;
            for (int slot = 0; slot < slotCount; slot++)
            {
                _abilitySlots[slot]?.SetInteractionEnabled(
                    isInteractable,
                    blockedReason);
            }
        }

        public void BindClassKit(ClassId classId, Sprite[] icons = null)
        {
            BindSpecializationKit(
                classId,
                SpecializationCatalog.GetDefault(classId),
                icons);
        }

        public void BindSpecializationKit(
            SpecializationId specializationId,
            Sprite[] icons = null)
        {
            BindSpecializationKit(
                ResolveClass(specializationId),
                specializationId,
                icons);
        }

        public void BindSpecializationKit(
            ClassId classId,
            SpecializationId specializationId,
            Sprite[] icons = null)
        {
            SpecializationId resolvedSpecialization =
                SpecializationCatalog.IsValidForClass(classId, specializationId)
                    ? specializationId
                    : SpecializationCatalog.GetDefault(classId);
            BindAbilityKit(
                SpecializationCatalog.GetKit(resolvedSpecialization),
                icons);
        }

        public void BindModeKit(
            PlayableModeId mode,
            SpecializationId specializationId,
            Sprite[] icons = null)
        {
            BindAbilityKit(
                ModeAbilityKitCatalog.GetKit(mode, specializationId),
                icons);
        }

        public void BindLevelingKit(
            SpecializationId specializationId,
            int level,
            Sprite[] icons = null)
        {
            BindLevelingKit(specializationId, level, false, icons);
        }

        public void BindLevelingKit(
            SpecializationId specializationId,
            int level,
            bool enableFlickLesson,
            Sprite[] icons = null)
        {
            AbilityKit kit = enableFlickLesson
                ? LevelingAbilityUnlockCatalog.GetFlickLessonKit(specializationId)
                : LevelingAbilityUnlockCatalog.GetKit(specializationId);
            BindAbilityKit(kit, icons);

            int safeLevel = LevelProgressionCurve.ClampLevel(level);
            int slotCount = Mathf.Min(
                AbilityCatalog.AbilitySlotCount,
                _abilitySlots != null ? _abilitySlots.Length : 0);
            for (int slot = 0; slot < slotCount; slot++)
            {
                AbilitySlotView slotView = _abilitySlots[slot];
                if (slotView == null || slot >= LevelingAbilityUnlockCatalog.TotalAbilityCount)
                {
                    continue;
                }

                int requiredLevel = LevelingAbilityUnlockCatalog.GetRequiredLevel(slot);
                bool unlocked = safeLevel >= requiredLevel;
                string lockedReason = unlocked
                    ? null
                    : "LEVEL " + requiredLevel;
                // Training introduces controls only when they become usable. The
                // authored slot remains in place while inactive, so an unlock
                // appears at its final position instead of compacting the row.
                slotView.SetInteractionEnabled(unlocked, lockedReason);
                slotView.SetReadiness(
                    unlocked
                        ? AbilityReadinessState.Ready
                        : AbilityReadinessState.Unavailable,
                    lockedReason);
                SetActiveIfNeeded(slotView.gameObject, unlocked);
            }
        }

        private void BindAbilityKit(AbilityKit kit, Sprite[] icons)
        {
            int slotCount = Mathf.Min(
                AbilityCatalog.AbilitySlotCount,
                _abilitySlots != null ? _abilitySlots.Length : 0);
            for (int slot = 0; slot < slotCount; slot++)
            {
                AbilitySlotView slotView = _abilitySlots[slot];
                if (slotView == null)
                {
                    continue;
                }

                AbilitySlotBinding binding = kit.GetSlotBinding(slot);
                AbilityId abilityId = binding.Primary;
                SetActiveIfNeeded(slotView.gameObject, abilityId != AbilityId.None);
                Sprite icon = icons != null && slot < icons.Length ? icons[slot] : null;
                if (icon == null && _abilityIconCatalog != null)
                {
                    _abilityIconCatalog.TryGetIcon(abilityId, out icon);
                }
                if (AbilityCatalog.TryGet(abilityId, out AbilityDefinition definition))
                {
                    slotView.Bind(
                        abilityId,
                        icon,
                        definition.DisplayName,
                        ResolveActionCaption(abilityId),
                        string.Empty,
                        definition.Description);
                }
                else
                {
                    slotView.Bind(abilityId, icon, string.Empty);
                }

                slotView.SetCooldown(0f, 0f, abilityId != AbilityId.None);
                slotView.SetGlobalCooldown(0f, 0f, false);
                slotView.SetInteractionEnabled(
                    abilityId != AbilityId.None,
                    abilityId == AbilityId.None ? "UNAVAILABLE" : null);

                AbilityId alternateUp = binding.Up;
                Sprite alternateIcon = null;
                if (_abilityIconCatalog != null)
                {
                    _abilityIconCatalog.TryGetIcon(alternateUp, out alternateIcon);
                }

                if (AbilityCatalog.TryGet(
                    alternateUp,
                    out AbilityDefinition alternateDefinition))
                {
                    slotView.BindAlternateUp(
                        alternateUp,
                        alternateIcon,
                        alternateDefinition.DisplayName,
                        alternateDefinition.Description);
                    slotView.SetAbilityCooldown(
                        alternateUp,
                        0f,
                        alternateDefinition.CooldownSeconds,
                        true);
                }

                slotView.BindDirectionalAbility(
                    AbilitySlotDirection.Down,
                    binding.Down,
                    ResolveAbilityIcon(binding.Down));
                slotView.BindDirectionalAbility(
                    AbilitySlotDirection.Left,
                    binding.Left,
                    ResolveAbilityIcon(binding.Left));
                slotView.BindDirectionalAbility(
                    AbilitySlotDirection.Right,
                    binding.Right,
                    ResolveAbilityIcon(binding.Right));
            }
        }

        private Sprite ResolveAbilityIcon(AbilityId abilityId)
        {
            if (_abilityIconCatalog != null &&
                _abilityIconCatalog.TryGetIcon(abilityId, out Sprite icon))
            {
                return icon;
            }

            return null;
        }

        public AbilitySlotView GetAbilitySlot(int slot)
        {
            return _abilitySlots != null && slot >= 0 && slot < _abilitySlots.Length
                ? _abilitySlots[slot]
                : null;
        }

        public void SetAbilityCooldown(
            AbilityId abilityId,
            float remainingSeconds,
            float totalSeconds,
            bool isAvailable)
        {
            AbilitySlotView slotView = FindAbilitySlot(abilityId);
            slotView?.SetAbilityCooldown(
                abilityId,
                remainingSeconds,
                totalSeconds,
                isAvailable);
        }

        public void SetAbilityCooldown(AbilityId abilityId, float remainingSeconds, bool isAvailable)
        {
            float totalSeconds = AbilityCatalog.TryGet(abilityId, out AbilityDefinition definition)
                ? definition.CooldownSeconds
                : 0f;
            SetAbilityCooldown(abilityId, remainingSeconds, totalSeconds, isAvailable);
        }

        public void SetAbilityReadiness(
            AbilityId abilityId,
            AbilityReadinessState state,
            string blockedReason = null)
        {
            AbilitySlotView slotView = FindAbilitySlot(abilityId);
            slotView?.SetAbilityReadiness(abilityId, state, blockedReason);
        }

        public void SetGlobalCooldown(
            float remainingSeconds,
            float totalSeconds,
            bool isLocked)
        {
            int slotCount = _abilitySlots != null ? _abilitySlots.Length : 0;
            for (int slot = 0; slot < slotCount; slot++)
            {
                AbilitySlotView slotView = _abilitySlots[slot];
                if (slotView == null)
                {
                    continue;
                }

                slotView.SetGlobalCooldown(
                    remainingSeconds,
                    totalSeconds,
                    isLocked);
            }
        }

        public void SetComboPoints(
            int points,
            int maximumPoints,
            bool targetMatches,
            bool visible)
        {
            int capacity = _comboPointPips != null ? _comboPointPips.Length : 0;
            int safeMaximum = Mathf.Clamp(maximumPoints, 0, capacity);
            int safePoints = Mathf.Clamp(points, 0, safeMaximum);
            _visibleComboPoints = safePoints;
            _comboTargetMatches = safePoints == 0 || targetMatches;
            SetActiveIfNeeded(_comboPointRoot, visible);
            if (!visible)
            {
                return;
            }

            Color filledColor = _comboTargetMatches
                ? new Color(1f, 0.72f, 0.16f, 1f)
                : new Color(0.72f, 0.42f, 1f, 1f);
            Color emptyColor = new Color(0.16f, 0.2f, 0.27f, 0.9f);
            for (int index = 0; index < capacity; index++)
            {
                Graphic pip = _comboPointPips[index];
                if (pip != null)
                {
                    pip.color = index < safePoints ? filledColor : emptyColor;
                }
            }

            string label = safePoints == 0
                ? "COMBO"
                : _comboTargetMatches
                    ? "COMBO • TARGET"
                    : "COMBO • SWITCH";
            SetTextIfChanged(_comboPointLabel, label);
            if (_comboPointLabel != null)
            {
                _comboPointLabel.color = safePoints > 0 ? filledColor : new Color(0.7f, 0.76f, 0.84f, 1f);
            }
        }

        public void SetHealth(float current, float maximum)
        {
            float safeMaximum = Mathf.Max(0f, maximum);
            float safeCurrent = Mathf.Clamp(current, 0f, safeMaximum);
            if (_healthFill != null)
            {
                _healthFill.fillAmount = safeMaximum > 0.001f ? safeCurrent / safeMaximum : 0f;
            }

            int healthPercent = safeMaximum > 0.001f
                ? Mathf.RoundToInt((safeCurrent / safeMaximum) * 100f)
                : 0;
            if (_healthLabel != null && healthPercent != _lastHealthCurrent)
            {
                _healthLabel.text = healthPercent + "%";
            }

            _lastHealthCurrent = healthPercent;
        }

        public void SetPlayerResource(
            ResourceType resourceType,
            float current,
            float maximum,
            bool visible = true)
        {
            PresentResource(
                _playerResourceRoot,
                _playerResourceFill,
                _playerResourceTypeLabel,
                _playerResourceValueLabel,
                resourceType,
                current,
                maximum,
                visible,
                ref _playerResourceState);
        }

        public void SetTeam(string teamName, Color teamColor)
        {
            SetTextIfChanged(_teamLabel, teamName);
            if (_teamLabel != null)
            {
                _teamLabel.color = teamColor;
            }
        }

        public void SetObjective(string objective)
        {
            SetTextIfChanged(_objectiveLabel, objective);
        }

        public void SetLevelProgression(
            int level,
            int totalExperience,
            int currentLevelExperience,
            int nextLevelExperience,
            bool visible)
        {
            SetActiveIfNeeded(_progressionRoot, visible);
            if (!visible)
            {
                return;
            }

            int safeLevel = Mathf.Max(1, level);
            int levelStart = Mathf.Max(0, currentLevelExperience);
            int levelEnd = Mathf.Max(levelStart, nextLevelExperience);
            int safeTotal = Mathf.Clamp(totalExperience, levelStart, levelEnd);
            float progress = levelEnd > levelStart
                ? (safeTotal - levelStart) / (float)(levelEnd - levelStart)
                : 1f;
            if (_experienceFill != null)
            {
                _experienceFill.fillAmount = Mathf.Clamp01(progress);
            }

            SetTextIfChanged(_levelLabel, "LEVEL " + safeLevel);
            SetTextIfChanged(
                _experienceLabel,
                levelEnd > levelStart
                    ? safeTotal + " / " + levelEnd + " XP"
                    : "MAX LEVEL  •  " + safeTotal + " XP");
        }

        public void ShowCombatNotice(string text, Color color, float seconds = 0.8f)
        {
            if (_combatNoticeCanvasGroup == null || _combatNoticeLabel == null ||
                string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            float duration = Mathf.Max(0.1f, seconds);
            _combatNoticeRemaining = duration;
            _combatNoticeFadeSeconds = Mathf.Min(0.28f, duration * 0.45f);
            SetTextIfChanged(_combatNoticeLabel, text.ToUpperInvariant());
            _combatNoticeLabel.color = color;
            _combatNoticeCanvasGroup.alpha = 1f;
        }

        public void SetTarget(string displayName, float currentHealth, float maximumHealth, bool visible)
        {
            SetTarget(
                0,
                ClassId.None,
                displayName,
                "ENEMY",
                false,
                currentHealth,
                maximumHealth,
                0f,
                string.Empty,
                0f,
                string.Empty,
                true,
                visible);
        }

        public void SetTarget(
            int participantId,
            ClassId classId,
            string displayName,
            string relation,
            bool isFriendly,
            float currentHealth,
            float maximumHealth,
            float barrier,
            string castName,
            float castProgress,
            string status,
            bool isAlive,
            bool visible)
        {
            SetActiveIfNeeded(_targetRoot, visible && _showDetailedTargetPanel);
            if (!visible)
            {
                SetTargetResource(ResourceType.None, 0f, 0f, false);
                SetTargetCast(string.Empty, 0f, false, false, false);
                ClearTargetAuras();
                return;
            }

            float safeMaximum = Mathf.Max(0f, maximumHealth);
            float safeHealth = Mathf.Clamp(currentHealth, 0f, safeMaximum);
            float safeBarrier = Mathf.Max(0f, barrier);
            float healthRatio = safeMaximum > 0.001f ? safeHealth / safeMaximum : 0f;
            float protectedRatio = safeMaximum > 0.001f
                ? Mathf.Clamp01((safeHealth + safeBarrier) / safeMaximum)
                : 0f;
            Color relationColor = ResolveRelationColor(isFriendly);
            int healthCurrent = Mathf.CeilToInt(safeHealth);
            int healthMaximum = Mathf.CeilToInt(safeMaximum);
            int healthPercent = Mathf.RoundToInt(healthRatio * 100f);
            int barrierValue = safeBarrier >= 0.5f
                ? Mathf.CeilToInt(safeBarrier)
                : 0;
            bool wasInitialized = _targetDetailState.IsInitialized;

            if (!wasInitialized || _targetDetailState.ParticipantId != participantId)
            {
                SetTextIfChanged(
                    _targetParticipantLabel,
                    participantId > 0 ? "#" + participantId : string.Empty);
            }

            if (!wasInitialized ||
                _targetDetailState.ClassId != classId ||
                !string.Equals(
                    _targetDetailState.DisplayName,
                    displayName,
                    StringComparison.Ordinal))
            {
                SetTextIfChanged(_targetLabel, BuildIdentity(displayName, classId));
            }

            bool relationChanged = !wasInitialized ||
                _targetDetailState.IsFriendly != isFriendly ||
                !string.Equals(
                    _targetDetailState.Relation,
                    relation,
                    StringComparison.Ordinal);
            if (relationChanged)
            {
                SetTextIfChanged(
                    _targetRelationLabel,
                    string.IsNullOrWhiteSpace(relation)
                        ? (isFriendly ? "ALLY" : "ENEMY")
                        : relation.ToUpperInvariant());
            }

            if (!wasInitialized ||
                _targetDetailState.HealthCurrent != healthCurrent ||
                _targetDetailState.HealthMaximum != healthMaximum ||
                _targetDetailState.HealthPercent != healthPercent)
            {
                SetTextIfChanged(
                    _targetHealthLabel,
                    healthCurrent + " / " + healthMaximum +
                    "  •  " + healthPercent + "%");
            }

            if (!wasInitialized || _targetDetailState.Barrier != barrierValue)
            {
                SetTextIfChanged(
                    _targetBarrierLabel,
                    barrierValue > 0 ? "+" + barrierValue + " WARD" : string.Empty);
            }

            if (!wasInitialized ||
                _targetDetailState.IsAlive != isAlive ||
                !string.Equals(
                    _targetDetailState.Status,
                    status,
                    StringComparison.Ordinal))
            {
                SetTextIfChanged(
                    _targetStatusLabel,
                    isAlive
                        ? (string.IsNullOrWhiteSpace(status) ? "READY" : status)
                        : "DEFEATED");
            }

            _targetDetailState.IsInitialized = true;
            _targetDetailState.ParticipantId = participantId;
            _targetDetailState.ClassId = classId;
            _targetDetailState.DisplayName = displayName;
            _targetDetailState.Relation = relation;
            _targetDetailState.IsFriendly = isFriendly;
            _targetDetailState.HealthCurrent = healthCurrent;
            _targetDetailState.HealthMaximum = healthMaximum;
            _targetDetailState.HealthPercent = healthPercent;
            _targetDetailState.Barrier = barrierValue;
            _targetDetailState.IsAlive = isAlive;
            _targetDetailState.Status = status;

            SetFill(_targetBarrierFill, protectedRatio);
            SetFill(_targetHealthFill, healthRatio);
            if (relationChanged && _targetRelationAccent != null)
            {
                _targetRelationAccent.color = relationColor;
            }

            if (relationChanged && _targetRelationLabel != null)
            {
                _targetRelationLabel.color = relationColor;
            }

            if (relationChanged && _targetSurface != null)
            {
                _targetSurface.color = ResolveRelationSurfaceColor(isFriendly);
            }

            bool isCasting = isAlive && !string.IsNullOrWhiteSpace(castName);
            SetTargetCast(
                castName,
                castProgress,
                false,
                true,
                isCasting);
        }

        public void SetTarget(
            CombatantSnapshot snapshot,
            string displayName,
            string relation,
            bool isFriendly,
            int currentTick,
            int tickRate,
            string status,
            bool visible)
        {
            ResolveCast(
                snapshot,
                currentTick,
                tickRate,
                out string castName,
                out float castProgress);
            SetTarget(
                snapshot.Id.Value,
                snapshot.Class,
                displayName,
                relation,
                isFriendly,
                snapshot.Health,
                snapshot.MaxHealth,
                snapshot.Barrier,
                castName,
                castProgress,
                status,
                snapshot.IsAlive,
                visible);
        }

        public void SetFocusedTarget(
            CombatantSnapshot snapshot,
            string displayName,
            string relation,
            bool isFriendly,
            int currentTick,
            int tickRate,
            string status,
            bool visible)
        {
            if (visible)
            {
                SetFocusedFighter(snapshot.Id);
            }
            else
            {
                ClearFocusedFighter();
            }

            SetTarget(
                snapshot,
                displayName,
                relation,
                isFriendly,
                currentTick,
                tickRate,
                status,
                visible);
        }

        public void SetTargetResource(
            ResourceType resourceType,
            float current,
            float maximum,
            bool visible = true)
        {
            PresentResource(
                _targetResourceRoot,
                _targetResourceFill,
                _targetResourceTypeLabel,
                _targetResourceValueLabel,
                resourceType,
                current,
                maximum,
                visible,
                ref _targetResourceState);
        }

        public void SetTargetCast(
            string spellName,
            float normalizedProgress,
            bool isChannel,
            bool isInterruptible,
            bool visible)
        {
            bool show = visible && !string.IsNullOrWhiteSpace(spellName);
            _targetCastVisible = show;
            _targetCastInterruptible = isInterruptible;
            SetActiveIfNeeded(_targetCastRoot, show);
            if (!show)
            {
                return;
            }

            SetTextIfChanged(_targetCastLabel, spellName);
            SetTextIfChanged(
                _targetCastStateLabel,
                isChannel
                    ? isInterruptible
                        ? InterruptibleChannelLabel
                        : ProtectedChannelLabel
                    : isInterruptible
                        ? InterruptibleCastLabel
                        : ProtectedCastLabel);
            SetFill(_targetCastFill, normalizedProgress);
            ApplyTargetCastColors(isInterruptible);
        }

        public bool SetTargetAura(int slot, HudAuraIndicator indicator)
        {
            if (slot < 0 || slot >= TargetAuraSlotCount ||
                _targetAuraRoots == null || slot >= _targetAuraRoots.Length)
            {
                return false;
            }

            bool visible = indicator.IsVisible;
            SetActiveIfNeeded(_targetAuraRoots[slot], visible);
            ref TargetAuraPresentationState state = ref _targetAuraStates[slot];
            if (!visible)
            {
                state.IsVisible = false;
                return true;
            }

            Text label = _targetAuraLabels != null && slot < _targetAuraLabels.Length
                ? _targetAuraLabels[slot]
                : null;
            Graphic surface = _targetAuraSurfaces != null && slot < _targetAuraSurfaces.Length
                ? _targetAuraSurfaces[slot]
                : null;
            int remainingSeconds = indicator.RemainingSeconds > 0f
                ? Mathf.CeilToInt(indicator.RemainingSeconds)
                : 0;
            bool labelChanged = !state.IsVisible ||
                !string.Equals(state.Label, indicator.Label, StringComparison.Ordinal);
            bool textChanged = labelChanged ||
                state.RemainingSeconds != remainingSeconds ||
                state.Stacks != indicator.Stacks;
            if (labelChanged)
            {
                state.UpperLabel = indicator.Label.ToUpperInvariant();
            }

            if (textChanged)
            {
                SetTextIfChanged(
                    label,
                    FormatAura(
                        state.UpperLabel,
                        indicator.Stacks,
                        remainingSeconds));
            }

            if ((!state.IsVisible || state.Kind != indicator.Kind) && surface != null)
            {
                surface.color = ResolveAuraColor(indicator.Kind);
            }

            state.IsVisible = true;
            state.Label = indicator.Label;
            state.RemainingSeconds = remainingSeconds;
            state.Stacks = indicator.Stacks;
            state.Kind = indicator.Kind;

            return true;
        }

        public void SetTargetAuras(
            HudAuraIndicator first = default,
            HudAuraIndicator second = default,
            HudAuraIndicator third = default,
            HudAuraIndicator fourth = default)
        {
            SetTargetAura(0, first);
            SetTargetAura(1, second);
            SetTargetAura(2, third);
            SetTargetAura(3, fourth);
        }

        public void ClearTargetAuras()
        {
            SetTargetAuras();
        }

        public void ClearFighters()
        {
            _focusedFighterId = 0;
            int fighterCount = _fighterViews != null ? _fighterViews.Length : 0;
            for (int index = 0; index < fighterCount; index++)
            {
                _fighterViews[index]?.Clear();
            }
        }

        public bool SetFocusedFighter(int participantId, bool notify = false)
        {
            if (participantId <= 0)
            {
                ClearFocusedFighter();
                return true;
            }

            CombatantStatusView fighterView = FindFighter(participantId);
            if (fighterView == null || !fighterView.IsBound || fighterView.IsHidden)
            {
                return false;
            }

            bool changed = _focusedFighterId != participantId;
            _focusedFighterId = participantId;
            RefreshFocusedFighterSelection();
            if (notify && changed)
            {
                FighterSelected?.Invoke(_focusedFighterId);
            }

            return true;
        }

        public bool SetFocusedFighter(
            ParticipantId participantId,
            bool notify = false)
        {
            return SetFocusedFighter(participantId.Value, notify);
        }

        public bool SelectFighter(int participantId)
        {
            return SetFocusedFighter(participantId, true);
        }

        public bool SelectFighter(ParticipantId participantId)
        {
            return SelectFighter(participantId.Value);
        }

        public void ClearFocusedFighter()
        {
            _focusedFighterId = 0;
            RefreshFocusedFighterSelection();
        }

        public bool BindFighter(
            int slot,
            int participantId,
            ClassId classId,
            string displayName,
            string relation,
            bool isFriendly,
            bool isHuman)
        {
            if (_fighterViews == null || slot < 0 || slot >= _fighterViews.Length ||
                _fighterViews[slot] == null)
            {
                return false;
            }

            CombatantStatusView fighterView = _fighterViews[slot];
            fighterView.SetLayoutParent(isFriendly ? _friendlyFighterRoot : _hostileFighterRoot);
            fighterView.Bind(
                participantId,
                classId,
                displayName,
                relation,
                isFriendly,
                isHuman);

            return true;
        }

        public bool BindFighter(
            int slot,
            ParticipantId participantId,
            ClassId classId,
            string displayName,
            string relation,
            bool isFriendly,
            bool isHuman)
        {
            return BindFighter(
                slot,
                participantId.Value,
                classId,
                displayName,
                relation,
                isFriendly,
                isHuman);
        }

        public bool PresentFighter(
            int participantId,
            float currentHealth,
            float maximumHealth,
            float barrier,
            string castName,
            float castProgress,
            string status,
            bool isAlive,
            bool isSelected)
        {
            CombatantStatusView fighterView = FindFighter(participantId);
            if (fighterView == null)
            {
                return false;
            }

            if (isSelected && _focusedFighterId != participantId)
            {
                _focusedFighterId = participantId;
                RefreshFocusedFighterSelection();
            }

            fighterView.Present(
                currentHealth,
                maximumHealth,
                barrier,
                castName,
                castProgress,
                status,
                isAlive,
                participantId == _focusedFighterId);
            return true;
        }

        public bool PresentFighter(
            CombatantSnapshot snapshot,
            int currentTick,
            int tickRate,
            string status,
            bool isSelected)
        {
            ResolveCast(
                snapshot,
                currentTick,
                tickRate,
                out string castName,
                out float castProgress);
            return PresentFighter(
                snapshot.Id.Value,
                snapshot.Health,
                snapshot.MaxHealth,
                snapshot.Barrier,
                castName,
                castProgress,
                status,
                snapshot.IsAlive,
                isSelected);
        }

        public bool SetFighterVisible(ParticipantId participantId, bool visible)
        {
            CombatantStatusView fighterView = FindFighter(participantId.Value);
            if (fighterView == null)
            {
                return false;
            }

            fighterView.SetVisible(visible);
            if (!visible && _focusedFighterId == participantId.Value)
            {
                ClearFocusedFighter();
            }

            return true;
        }

        public bool SetFighterHidden(ParticipantId participantId, bool hidden)
        {
            CombatantStatusView fighterView = FindFighter(participantId.Value);
            if (fighterView == null)
            {
                return false;
            }

            fighterView.SetHidden(hidden);
            if (hidden && _focusedFighterId == participantId.Value)
            {
                ClearFocusedFighter();
            }

            return true;
        }

        public void SetCast(string displayName, float normalizedProgress, bool visible)
        {
            SetActiveIfNeeded(_castRoot, visible);
            if (!visible)
            {
                return;
            }

            SetTextIfChanged(_castLabel, displayName);
            if (_castFill != null)
            {
                _castFill.fillAmount = Mathf.Clamp01(normalizedProgress);
            }
        }

        public void SetResult(string title, string recap)
        {
            SetResult(title, recap, "PLAY AGAIN");
        }

        public void SetResult(string title, string recap, string replayAction)
        {
            SetTextIfChanged(_resultTitleLabel, title);
            SetTextIfChanged(_resultRecapLabel, recap);
            SetTextIfChanged(
                _replayButtonLabel,
                string.IsNullOrWhiteSpace(replayAction)
                    ? "PLAY AGAIN"
                    : replayAction.ToUpperInvariant());
        }

        private void RefreshHomeActionLabels()
        {
            string action;
            switch (_selectedMode)
            {
                case PrototypeMode.Training:
                    action = _trainingRecommended
                        ? "START RECOMMENDED TRAINING"
                        : "START TRAINING";
                    break;
                case PrototypeMode.PveLeveling:
                    action = "CONTINUE PVE LEVELING";
                    break;
                case PrototypeMode.HeroSiege:
                    action = _selectedSiegeVariant == SiegeVariantId.FourLanes
                        ? "START FOUR-LANE SIEGE"
                        : "START SOLO-LANE SIEGE";
                    break;
                case PrototypeMode.Dungeon:
                    action = "ENTER DUNGEON";
                    break;
                default:
                    action = "PLAY 2 v 2 ARENA";
                    break;
            }

            SetTextIfChanged(_startButtonLabel, action);
            SetTextIfChanged(
                _trainingModeLabel,
                _trainingRecommended
                    ? "TRAINING  •  RECOMMENDED\n3 → 7 ACTIONS  •  SKIPPABLE"
                    : "TRAINING  •  SKIPPABLE\n3 → 7 ACTIONS  •  ASHEN CAUSEWAY");
        }

        private void AddButtonListeners()
        {
            _healerButton?.onClick.AddListener(OnHealerClicked);
            _rogueButton?.onClick.AddListener(OnRogueClicked);
            _mageButton?.onClick.AddListener(OnMageClicked);
            _hunterButton?.onClick.AddListener(OnHunterClicked);
            _disciplineButton?.onClick.AddListener(OnDisciplineClicked);
            _shadowButton?.onClick.AddListener(OnShadowClicked);
            _shadowDanceButton?.onClick.AddListener(OnShadowDanceClicked);
            _mutilateButton?.onClick.AddListener(OnMutilateClicked);
            _fireButton?.onClick.AddListener(OnFireClicked);
            _frostButton?.onClick.AddListener(OnFrostClicked);
            _marksmanshipButton?.onClick.AddListener(OnMarksmanshipClicked);
            _arenaModeButton?.onClick.AddListener(OnArenaModeClicked);
            _siegeModeButton?.onClick.AddListener(OnSiegeModeClicked);
            _bossModeButton?.onClick.AddListener(OnBossModeClicked);
            _levelingModeButton?.onClick.AddListener(OnLevelingModeClicked);
            _pveLevelingModeButton?.onClick.AddListener(OnPveLevelingModeClicked);
            _siegeSoloLaneButton?.onClick.AddListener(OnSiegeSoloLaneClicked);
            _siegeFourLanesButton?.onClick.AddListener(OnSiegeFourLanesClicked);
            _startButton?.onClick.AddListener(OnStartClicked);
            _skipTrainingButton?.onClick.AddListener(OnSkipTrainingClicked);
            _siegeShopButton?.onClick.AddListener(OnSiegeShopClicked);
            _siegeBuyHealthButton?.onClick.AddListener(OnSiegeBuyHealthClicked);
            _siegeUseHealthButton?.onClick.AddListener(OnSiegeUseHealthClicked);
            _siegeBuyManaButton?.onClick.AddListener(OnSiegeBuyManaClicked);
            _siegeUseManaButton?.onClick.AddListener(OnSiegeUseManaClicked);
            _replayButton?.onClick.AddListener(OnReplayClicked);
            _leaveButton?.onClick.AddListener(OnLeaveClicked);
            int fighterCount = _fighterViews != null ? _fighterViews.Length : 0;
            for (int index = 0; index < fighterCount; index++)
            {
                CombatantStatusView fighterView = _fighterViews[index];
                if (fighterView != null)
                {
                    fighterView.Selected += OnFighterSelected;
                }
            }
        }

        private void RemoveButtonListeners()
        {
            _healerButton?.onClick.RemoveListener(OnHealerClicked);
            _rogueButton?.onClick.RemoveListener(OnRogueClicked);
            _mageButton?.onClick.RemoveListener(OnMageClicked);
            _hunterButton?.onClick.RemoveListener(OnHunterClicked);
            _disciplineButton?.onClick.RemoveListener(OnDisciplineClicked);
            _shadowButton?.onClick.RemoveListener(OnShadowClicked);
            _shadowDanceButton?.onClick.RemoveListener(OnShadowDanceClicked);
            _mutilateButton?.onClick.RemoveListener(OnMutilateClicked);
            _fireButton?.onClick.RemoveListener(OnFireClicked);
            _frostButton?.onClick.RemoveListener(OnFrostClicked);
            _marksmanshipButton?.onClick.RemoveListener(OnMarksmanshipClicked);
            _arenaModeButton?.onClick.RemoveListener(OnArenaModeClicked);
            _siegeModeButton?.onClick.RemoveListener(OnSiegeModeClicked);
            _bossModeButton?.onClick.RemoveListener(OnBossModeClicked);
            _levelingModeButton?.onClick.RemoveListener(OnLevelingModeClicked);
            _pveLevelingModeButton?.onClick.RemoveListener(OnPveLevelingModeClicked);
            _siegeSoloLaneButton?.onClick.RemoveListener(OnSiegeSoloLaneClicked);
            _siegeFourLanesButton?.onClick.RemoveListener(OnSiegeFourLanesClicked);
            _startButton?.onClick.RemoveListener(OnStartClicked);
            _skipTrainingButton?.onClick.RemoveListener(OnSkipTrainingClicked);
            _siegeShopButton?.onClick.RemoveListener(OnSiegeShopClicked);
            _siegeBuyHealthButton?.onClick.RemoveListener(OnSiegeBuyHealthClicked);
            _siegeUseHealthButton?.onClick.RemoveListener(OnSiegeUseHealthClicked);
            _siegeBuyManaButton?.onClick.RemoveListener(OnSiegeBuyManaClicked);
            _siegeUseManaButton?.onClick.RemoveListener(OnSiegeUseManaClicked);
            _replayButton?.onClick.RemoveListener(OnReplayClicked);
            _leaveButton?.onClick.RemoveListener(OnLeaveClicked);
            int fighterCount = _fighterViews != null ? _fighterViews.Length : 0;
            for (int index = 0; index < fighterCount; index++)
            {
                CombatantStatusView fighterView = _fighterViews[index];
                if (fighterView != null)
                {
                    fighterView.Selected -= OnFighterSelected;
                }
            }
        }

        private void OnHealerClicked()
        {
            SetSelectedClass(ClassId.Healer, true);
        }

        private void OnRogueClicked()
        {
            SetSelectedClass(ClassId.Rogue, true);
        }

        private void OnMageClicked()
        {
            SetSelectedClass(ClassId.Mage, true);
        }

        private void OnHunterClicked()
        {
            SetSelectedClass(ClassId.Hunter, true);
        }

        private void OnDisciplineClicked()
        {
            SetSelectedSpecialization(SpecializationId.Discipline, true);
        }

        private void OnShadowClicked()
        {
            SetSelectedSpecialization(SpecializationId.Shadow, true);
        }

        private void OnShadowDanceClicked()
        {
            SetSelectedSpecialization(SpecializationId.ShadowDance, true);
        }

        private void OnMutilateClicked()
        {
            SetSelectedSpecialization(SpecializationId.Mutilate, true);
        }

        private void OnFireClicked()
        {
            SetSelectedSpecialization(SpecializationId.Fire, true);
        }

        private void OnFrostClicked()
        {
            SetSelectedSpecialization(SpecializationId.Frost, true);
        }

        private void OnMarksmanshipClicked()
        {
            SetSelectedSpecialization(SpecializationId.Marksmanship, true);
        }

        private void OnArenaModeClicked()
        {
            SetSelectedMode(PrototypeMode.Arena2V2, true);
        }

        private void OnBossModeClicked()
        {
            SetSelectedMode(PrototypeMode.Dungeon, true);
        }

        private void OnLevelingModeClicked()
        {
            SetSelectedMode(PrototypeMode.Training, true);
        }

        private void OnPveLevelingModeClicked()
        {
            SetSelectedMode(PrototypeMode.PveLeveling, true);
        }

        private void OnSiegeModeClicked()
        {
            SetSelectedMode(PrototypeMode.HeroSiege, true);
        }

        private void OnSiegeSoloLaneClicked()
        {
            SetSelectedSiegeVariant(SiegeVariantId.SoloLane, true);
        }

        private void OnSiegeFourLanesClicked()
        {
            SetSelectedSiegeVariant(SiegeVariantId.FourLanes, true);
        }

        private void OnSiegeShopClicked()
        {
            _siegeShopOpen = !_siegeShopOpen;
            RefreshSiegeSurfaceVisibility();
        }

        private void OnSiegeBuyHealthClicked()
        {
            SiegePotionPurchaseRequested?.Invoke(SiegePotionId.Health);
        }

        private void OnSiegeUseHealthClicked()
        {
            SiegePotionUseRequested?.Invoke(SiegePotionId.Health);
        }

        private void OnSiegeBuyManaClicked()
        {
            SiegePotionPurchaseRequested?.Invoke(SiegePotionId.Mana);
        }

        private void OnSiegeUseManaClicked()
        {
            SiegePotionUseRequested?.Invoke(SiegePotionId.Mana);
        }

        private void OnStartClicked()
        {
            StartRequested?.Invoke();
        }

        private void OnSkipTrainingClicked()
        {
            SkipTrainingRequested?.Invoke();
        }

        private void OnReplayClicked()
        {
            ReplayRequested?.Invoke();
        }

        private void OnLeaveClicked()
        {
            LeaveRequested?.Invoke();
        }

        private void OnFighterSelected(int participantId)
        {
            if (SetFocusedFighter(participantId))
            {
                FighterSelected?.Invoke(participantId);
            }
        }

        private AbilitySlotView FindAbilitySlot(AbilityId abilityId)
        {
            int slotCount = _abilitySlots != null ? _abilitySlots.Length : 0;
            for (int i = 0; i < slotCount; i++)
            {
                AbilitySlotView slotView = _abilitySlots[i];
                if (slotView != null && slotView.ContainsAbility(abilityId))
                {
                    return slotView;
                }
            }

            return null;
        }

        public static string ResolveActionCaption(AbilityId abilityId)
        {
            switch (abilityId)
            {
                case AbilityId.HealerBolt:
                    return "SMITE";
                case AbilityId.Mend:
                    return "HEAL";
                case AbilityId.Aegis:
                    return "WARD";
                case AbilityId.Purify:
                    return "CLEANSE";
                case AbilityId.Rescue:
                    return "PULL";
                case AbilityId.QuickStrike:
                    return "STRIKE";
                case AbilityId.Ambush:
                    return "AMBUSH";
                case AbilityId.Anchor:
                    return "BRACE";
                case AbilityId.ArcaneBolt:
                    return "BOLT";
                case AbilityId.Gust:
                    return "PUSH";
                case AbilityId.BossCleave:
                    return "CLEAVE";
                case AbilityId.BossMeteor:
                    return "METEOR";
                case AbilityId.BossKnockback:
                    return "ROAR";
                case AbilityId.Penance:
                    return "PENANCE";
                case AbilityId.FlashHeal:
                    return "FLASH";
                case AbilityId.Renew:
                    return "RENEW";
                case AbilityId.PowerWordShield:
                    return "SHIELD";
                case AbilityId.IceBarrier:
                    return "BARRIER";
                case AbilityId.PainSuppression:
                    return "SUPPRESS";
                case AbilityId.PsychicScream:
                    return "SCREAM";
                case AbilityId.ShadowWordDeath:
                    return "DEATH";
                case AbilityId.Shadowfiend:
                    return "FIEND";
                case AbilityId.DispelMagic:
                    return "DISPEL";
                case AbilityId.Hemorrhage:
                    return "HEMO";
                case AbilityId.Mutilate:
                    return "MUTILATE";
                case AbilityId.Eviscerate:
                    return "EVISC";
                case AbilityId.Envenom:
                    return "ENVENOM";
                case AbilityId.KidneyShot:
                    return "KIDNEY";
                case AbilityId.DeepFreeze:
                    return "DEEP FREEZE";
                case AbilityId.CheapShot:
                    return "CHEAP";
                case AbilityId.ShadowDance:
                    return "DANCE";
                case AbilityId.FireBlast:
                    return "FIRE BLAST";
                case AbilityId.IceLance:
                    return "ICE LANCE";
                case AbilityId.Shadowstep:
                    return "STEP";
                case AbilityId.Blink:
                    return "BLINK";
                case AbilityId.Kick:
                    return "KICK";
                case AbilityId.Counterspell:
                    return "COUNTER";
                case AbilityId.CloakOfShadows:
                    return "CLOAK";
                case AbilityId.Shiv:
                    return "SHIV";
                case AbilityId.Blind:
                    return "BLIND";
                case AbilityId.DragonBreath:
                    return "BREATH";
                case AbilityId.Polymorph:
                    return "POLY";
                case AbilityId.Vanish:
                    return "VANISH";
                case AbilityId.Fireball:
                    return "FIREBALL";
                case AbilityId.Frostbolt:
                    return "FROSTBOLT";
                case AbilityId.LivingBomb:
                    return "BOMB";
                case AbilityId.BlastWave:
                    return "BLAST WAVE";
                case AbilityId.FrostNova:
                    return "NOVA";
                case AbilityId.ArenaTrinket:
                    return "TRINKET";
                case AbilityId.PowerInfusion:
                    return "INFUSION";
                case AbilityId.IcyVeins:
                    return "ICY VEINS";
                case AbilityId.Combustion:
                    return "COMBUST";
                case AbilityId.ColdBlood:
                    return "COLD BLOOD";
                case AbilityId.Evasion:
                    return "EVASION";
                case AbilityId.VampiricTouch:
                    return "VAMPIRIC";
                case AbilityId.ShadowWordPain:
                    return "PAIN";
                case AbilityId.DevouringPlague:
                    return "PLAGUE";
                case AbilityId.SerpentSting:
                    return "SERPENT";
                case AbilityId.ViperSting:
                    return "VIPER";
                case AbilityId.ScorpidSting:
                    return "SCORPID";
                case AbilityId.MindFlay:
                    return "FLAY";
                case AbilityId.MindBlast:
                    return "MIND BLAST";
                case AbilityId.ChimeraShot:
                    return "CHIMERA";
                case AbilityId.AimedShot:
                    return "AIMED";
                case AbilityId.ArcaneShot:
                    return "ARCANE";
                case AbilityId.KillShot:
                    return "EXECUTE";
                case AbilityId.Silence:
                case AbilityId.SilencingShot:
                    return "SILENCE";
                case AbilityId.Dispersion:
                    return "DISPERSION";
                case AbilityId.Deterrence:
                    return "DETERRENCE";
                case AbilityId.PsychicHorror:
                    return "HORROR";
                case AbilityId.ManaBurn:
                    return "BURN";
                case AbilityId.MassDispel:
                    return "MASS DISPEL";
                case AbilityId.Fade:
                    return "FADE";
                case AbilityId.MastersCall:
                    return "MASTER'S";
                case AbilityId.SteadyShot:
                    return "STEADY";
                case AbilityId.HuntersMark:
                    return "MARK";
                case AbilityId.ScatterShot:
                    return "SCATTER";
                case AbilityId.FreezingArrow:
                    return "FREEZING";
                case AbilityId.FrostTrap:
                    return "TRAP";
                case AbilityId.FeignDeath:
                    return "DROP";
                case AbilityId.Disengage:
                    return "LEAP";
                case AbilityId.WingClip:
                case AbilityId.SpiderWeb:
                    return "ROOT";
                case AbilityId.RoarOfSacrifice:
                    return "PROTECT";
                case AbilityId.Readiness:
                    return "RESET";
                case AbilityId.RapidFire:
                    return "RAPID";
                case AbilityId.PetAttack:
                    return "PET ATTACK";
                case AbilityId.PetFollow:
                    return "FOLLOW";
                case AbilityId.PetStay:
                    return "STAY";
                case AbilityId.MendPet:
                    return "MEND PET";
                case AbilityId.TranquilizingShot:
                    return "PURGE";
                case AbilityId.ConcussiveShot:
                    return "SLOW";
                case AbilityId.Flare:
                    return "REVEAL";
                case AbilityId.AspectOfTheDragonhawk:
                    return "DRAGONHAWK";
                case AbilityId.AspectOfTheViper:
                    return "VIPER ASPECT";
                case AbilityId.PrayerOfMending:
                    return "REACTIVE";
                case AbilityId.InnerFocus:
                    return "FOCUS";
                case AbilityId.FearWard:
                    return "WARD";
                case AbilityId.Scorch:
                    return "SCORCH";
                case AbilityId.Pyroblast:
                    return "PYRO";
                case AbilityId.ConeOfCold:
                    return "SLOW";
                case AbilityId.Spellsteal:
                    return "STEAL";
                case AbilityId.IceBlock:
                    return "IMMUNE";
                case AbilityId.ManaShield:
                    return "SHIELD";
                case AbilityId.MoltenArmor:
                    return "MOLTEN";
                case AbilityId.MageArmor:
                    return "MAGE ARMOR";
                case AbilityId.IceArmor:
                    return "ICE ARMOR";
                case AbilityId.WaterElementalFreeze:
                    return "FREEZE";
                case AbilityId.ColdSnap:
                    return "COLD SNAP";
                case AbilityId.Preparation:
                    return "PREP";
                case AbilityId.Sap:
                    return "SAP";
                case AbilityId.Gouge:
                    return "GOUGE";
                case AbilityId.Garrote:
                    return "BLEED";
                case AbilityId.Sprint:
                    return "SPRINT";
                case AbilityId.Dismantle:
                    return "DISARM";
                case AbilityId.Stealth:
                    return "STEALTH";
                case AbilityId.Shadowform:
                    return "SHADOWFORM";
                case AbilityId.Smite:
                    return "SMITE";
                case AbilityId.BladeTempest:
                    return "BLADE FAN";
                case AbilityId.DawnNova:
                    return "DAWN NOVA";
                case AbilityId.UmbralCollapse:
                    return "COLLAPSE";
                case AbilityId.FlameSurge:
                    return "FLAME SURGE";
                case AbilityId.Blizzard:
                    return "BLIZZARD";
                case AbilityId.ArrowRain:
                    return "ARROW RAIN";
                default:
                    return string.Empty;
            }
        }

        private CombatantStatusView FindFighter(int participantId)
        {
            int fighterCount = _fighterViews != null ? _fighterViews.Length : 0;
            for (int index = 0; index < fighterCount; index++)
            {
                CombatantStatusView fighterView = _fighterViews[index];
                if (fighterView != null && fighterView.IsBound && fighterView.ParticipantId == participantId)
                {
                    return fighterView;
                }
            }

            return null;
        }

        private void RefreshFocusedFighterSelection()
        {
            int fighterCount = _fighterViews != null ? _fighterViews.Length : 0;
            for (int index = 0; index < fighterCount; index++)
            {
                CombatantStatusView fighterView = _fighterViews[index];
                if (fighterView != null)
                {
                    fighterView.SetSelected(
                        fighterView.IsBound &&
                        fighterView.ParticipantId == _focusedFighterId);
                }
            }
        }

        private void RefreshSelectionMarkers()
        {
            SetActiveIfNeeded(_healerSelectedMarker, _selectedClass == ClassId.Healer);
            SetActiveIfNeeded(_rogueSelectedMarker, _selectedClass == ClassId.Rogue);
            SetActiveIfNeeded(_mageSelectedMarker, _selectedClass == ClassId.Mage);
            SetActiveIfNeeded(_hunterSelectedMarker, _selectedClass == ClassId.Hunter);
            SetActiveIfNeeded(
                _disciplineSpecializationRoot,
                _selectedClass == ClassId.Healer);
            SetActiveIfNeeded(
                _rogueSpecializationRoot,
                _selectedClass == ClassId.Rogue);
            SetActiveIfNeeded(
                _mageSpecializationRoot,
                _selectedClass == ClassId.Mage);
            SetActiveIfNeeded(
                _hunterSpecializationRoot,
                _selectedClass == ClassId.Hunter);
            SetActiveIfNeeded(
                _disciplineSelectedMarker,
                _selectedSpecialization == SpecializationId.Discipline);
            SetActiveIfNeeded(
                _shadowSelectedMarker,
                _selectedSpecialization == SpecializationId.Shadow);
            SetActiveIfNeeded(
                _shadowDanceSelectedMarker,
                _selectedSpecialization == SpecializationId.ShadowDance);
            SetActiveIfNeeded(
                _mutilateSelectedMarker,
                _selectedSpecialization == SpecializationId.Mutilate);
            SetActiveIfNeeded(
                _fireSelectedMarker,
                _selectedSpecialization == SpecializationId.Fire);
            SetActiveIfNeeded(
                _frostSelectedMarker,
                _selectedSpecialization == SpecializationId.Frost);
            SetActiveIfNeeded(
                _marksmanshipSelectedMarker,
                _selectedSpecialization == SpecializationId.Marksmanship);
            SetActiveIfNeeded(_arenaSelectedMarker, _selectedMode == PrototypeMode.Arena2V2);
            SetActiveIfNeeded(_siegeSelectedMarker, _selectedMode == PrototypeMode.HeroSiege);
            SetActiveIfNeeded(_bossSelectedMarker, _selectedMode == PrototypeMode.Dungeon);
            SetActiveIfNeeded(_levelingSelectedMarker, _selectedMode == PrototypeMode.Training);
            SetActiveIfNeeded(_pveLevelingSelectedMarker, _selectedMode == PrototypeMode.PveLeveling);
            SetActiveIfNeeded(
                _siegeSoloLaneSelectedMarker,
                _selectedSiegeVariant == SiegeVariantId.SoloLane);
            SetActiveIfNeeded(
                _siegeFourLanesSelectedMarker,
                _selectedSiegeVariant == SiegeVariantId.FourLanes);
            RefreshSiegeSurfaceVisibility();
        }

        private void RefreshSiegeSurfaceVisibility()
        {
            bool isSiege = _selectedMode == PrototypeMode.HeroSiege;
            SetActiveIfNeeded(
                _siegeVariantRoot,
                isSiege && _screen == PrototypeHudScreen.Home);
            SetActiveIfNeeded(
                _classDetailLabel != null ? _classDetailLabel.gameObject : null,
                !isSiege);
            bool showSupply = isSiege &&
                _screen == PrototypeHudScreen.Gameplay &&
                _siegeSupplyRequestedVisible;
            if (!showSupply)
            {
                _siegeShopOpen = false;
            }

            SetActiveIfNeeded(_siegeSupplyRoot, showSupply);
            SetActiveIfNeeded(
                _siegePurchaseDrawer,
                showSupply && _siegeShopOpen);
            SetTextIfChanged(
                _siegeShopLabel,
                _siegeShopOpen ? "CLOSE" : "SHOP");
        }

        private void RefreshClassDetail()
        {
            string detail;
            switch (_selectedSpecialization)
            {
                case SpecializationId.Discipline:
                    detail = "DISCIPLINE  //  PROTECT ALLIES • HEAL • REMOVE HARMFUL MAGIC";
                    break;
                case SpecializationId.Shadow:
                    detail = "SHADOW  //  DAMAGE OVER TIME • DRAIN HEALTH • SILENCE CASTERS";
                    break;
                case SpecializationId.ShadowDance:
                    detail = "SHADOW DANCE  //  ENTER STEALTH • STRIKE • SPEND COMBO POINTS";
                    break;
                case SpecializationId.Mutilate:
                    detail = "MUTILATE  //  APPLY POISON • BUILD AND SPEND COMBO POINTS";
                    break;
                case SpecializationId.Fire:
                    detail = "FIRE  //  EXPLOSIVE DAMAGE • CONTROL ENEMIES • PUSH BACK";
                    break;
                case SpecializationId.Frost:
                    detail = "FROST  //  SLOW AND FREEZE • SHATTER FROZEN ENEMIES • ESCAPE";
                    break;
                case SpecializationId.Marksmanship:
                    detail = "MARKSMANSHIP  //  LONG RANGE • TRAPS • COMMAND PET • DISENGAGE";
                    break;
                default:
                    detail = string.Empty;
                    break;
            }

            SetTextIfChanged(_classDetailLabel, detail);
        }

        private void EnsureSelectedSpecialization()
        {
            if (!SpecializationCatalog.IsValidForClass(
                    _selectedClass,
                    _selectedSpecialization))
            {
                _selectedSpecialization = GetRememberedSpecialization(_selectedClass);
            }
        }

        private void BindSelectedKitIfValid()
        {
            if (SpecializationCatalog.IsValidForClass(
                    _selectedClass,
                    _selectedSpecialization))
            {
                BindSpecializationKit(_selectedClass, _selectedSpecialization);
            }
        }

        private SpecializationId GetRememberedSpecialization(ClassId classId)
        {
            SpecializationId remembered;
            switch (classId)
            {
                case ClassId.Healer:
                    remembered = _lastHealerSpecialization;
                    break;
                case ClassId.Rogue:
                    remembered = _lastRogueSpecialization;
                    break;
                case ClassId.Mage:
                    remembered = _lastMageSpecialization;
                    break;
                case ClassId.Hunter:
                    remembered = _lastHunterSpecialization;
                    break;
                default:
                    return SpecializationId.None;
            }

            return SpecializationCatalog.IsValidForClass(classId, remembered)
                ? remembered
                : SpecializationCatalog.GetDefault(classId);
        }

        private void RememberSpecialization(
            ClassId classId,
            SpecializationId specializationId)
        {
            switch (classId)
            {
                case ClassId.Healer:
                    _lastHealerSpecialization = specializationId;
                    break;
                case ClassId.Rogue:
                    _lastRogueSpecialization = specializationId;
                    break;
                case ClassId.Mage:
                    _lastMageSpecialization = specializationId;
                    break;
                case ClassId.Hunter:
                    _lastHunterSpecialization = specializationId;
                    break;
            }
        }

        private static ClassId ResolveClass(SpecializationId specializationId)
        {
            switch (specializationId)
            {
                case SpecializationId.Discipline:
                case SpecializationId.Shadow:
                    return ClassId.Healer;
                case SpecializationId.ShadowDance:
                case SpecializationId.Mutilate:
                    return ClassId.Rogue;
                case SpecializationId.Fire:
                case SpecializationId.Frost:
                    return ClassId.Mage;
                case SpecializationId.Marksmanship:
                    return ClassId.Hunter;
                default:
                    return ClassId.None;
            }
        }

        private void PresentResource(
            GameObject root,
            Image fill,
            Text typeLabel,
            Text valueLabel,
            ResourceType resourceType,
            float current,
            float maximum,
            bool visible,
            ref ResourcePresentationState state)
        {
            bool show = visible && resourceType != ResourceType.None && maximum > 0.001f;
            SetActiveIfNeeded(root, show);
            if (!show)
            {
                return;
            }

            float safeMaximum = Mathf.Max(0f, maximum);
            float safeCurrent = Mathf.Clamp(current, 0f, safeMaximum);
            Color resourceColor = resourceType == ResourceType.Energy
                ? _energyColor
                : _manaColor;
            SetFill(fill, safeCurrent / safeMaximum);
            bool resourceTypeChanged = !state.IsInitialized ||
                state.ResourceType != resourceType;
            if (resourceTypeChanged && fill != null)
            {
                fill.color = resourceColor;
            }

            if (resourceTypeChanged)
            {
                SetTextIfChanged(typeLabel, DescribeResource(resourceType));
            }

            int currentValue = Mathf.CeilToInt(safeCurrent);
            int maximumValue = Mathf.CeilToInt(safeMaximum);
            if (!state.IsInitialized ||
                state.CurrentValue != currentValue ||
                state.MaximumValue != maximumValue)
            {
                SetTextIfChanged(
                    valueLabel,
                    currentValue + " / " + maximumValue);
            }

            if (resourceTypeChanged && typeLabel != null)
            {
                typeLabel.color = resourceColor;
            }

            state.IsInitialized = true;
            state.ResourceType = resourceType;
            state.CurrentValue = currentValue;
            state.MaximumValue = maximumValue;
        }

        private Color ResolveAuraColor(HudAuraKind kind)
        {
            switch (kind)
            {
                case HudAuraKind.Harmful:
                    return _highContrastEnabled
                        ? HighContrastHarmfulAuraColor
                        : _harmfulAuraColor;
                case HudAuraKind.Control:
                    return _highContrastEnabled
                        ? HighContrastControlAuraColor
                        : _controlAuraColor;
                default:
                    return _highContrastEnabled
                        ? HighContrastBeneficialAuraColor
                        : _beneficialAuraColor;
            }
        }

        private Color ResolveRelationColor(bool isFriendly)
        {
            if (_highContrastEnabled)
            {
                return isFriendly
                    ? HighContrastFriendlyColor
                    : HighContrastHostileColor;
            }

            return isFriendly ? _friendlyColor : _hostileColor;
        }

        private Color ResolveRelationSurfaceColor(bool isFriendly)
        {
            if (_highContrastEnabled)
            {
                return isFriendly
                    ? HighContrastFriendlySurfaceColor
                    : HighContrastHostileSurfaceColor;
            }

            return isFriendly ? _friendlySurfaceColor : _hostileSurfaceColor;
        }

        private Color ResolveTargetCastColor(bool isInterruptible)
        {
            if (_highContrastEnabled)
            {
                return isInterruptible
                    ? HighContrastInterruptibleCastColor
                    : HighContrastProtectedCastColor;
            }

            return isInterruptible
                ? _interruptibleCastColor
                : _protectedCastColor;
        }

        private void ApplyTargetCastColors(bool isInterruptible)
        {
            Color castColor = ResolveTargetCastColor(isInterruptible);
            if (_targetCastFill != null)
            {
                _targetCastFill.color = castColor;
            }

            if (_targetCastStateLabel != null)
            {
                _targetCastStateLabel.color = castColor;
            }

            if (_targetCastSurface != null)
            {
                float surfaceMultiplier = _highContrastEnabled ? 0.08f : 0.14f;
                _targetCastSurface.color = new Color(
                    castColor.r * surfaceMultiplier,
                    castColor.g * surfaceMultiplier,
                    castColor.b * surfaceMultiplier,
                    _highContrastEnabled ? 1f : 0.94f);
            }
        }

        private void CaptureDefaultAccessibilityColors()
        {
            if (!_defaultTargetStatusColor.HasValue && _targetStatusLabel != null)
            {
                _defaultTargetStatusColor = _targetStatusLabel.color;
            }

            if (!_defaultTargetCastLabelColor.HasValue && _targetCastLabel != null)
            {
                _defaultTargetCastLabelColor = _targetCastLabel.color;
            }

            if (!_defaultCastLabelColor.HasValue && _castLabel != null)
            {
                _defaultCastLabelColor = _castLabel.color;
            }

            if (!_defaultCastFillColor.HasValue && _castFill != null)
            {
                _defaultCastFillColor = _castFill.color;
            }
        }

        private static string FormatAura(
            string upperLabel,
            int stacks,
            int remainingSeconds)
        {
            string value = upperLabel;
            if (stacks > 1)
            {
                value += " x" + stacks;
            }

            if (remainingSeconds > 0)
            {
                value += "  " + remainingSeconds + "s";
            }

            return value;
        }

        private static string DescribeResource(ResourceType resourceType)
        {
            switch (resourceType)
            {
                case ResourceType.Mana:
                    return "MANA";
                case ResourceType.Energy:
                    return "ENERGY";
                default:
                    return string.Empty;
            }
        }

        private static void ResolveCast(
            CombatantSnapshot snapshot,
            int currentTick,
            int tickRate,
            out string castName,
            out float castProgress)
        {
            castName = string.Empty;
            castProgress = 0f;
            if (snapshot.CastingAbility == AbilityId.None || snapshot.CastEndTick <= currentTick ||
                !AbilityCatalog.TryGet(snapshot.CastingAbility, out AbilityDefinition definition))
            {
                return;
            }

            castName = definition.DisplayName;
            float totalTicks = Mathf.Max(1f, definition.CastSeconds * Mathf.Max(1, tickRate));
            castProgress = Mathf.Clamp01(1f - ((snapshot.CastEndTick - currentTick) / totalTicks));
        }

        private static string BuildIdentity(string displayName, ClassId classId)
        {
            string className = DescribeClass(classId);
            if (string.IsNullOrWhiteSpace(displayName))
            {
                return className;
            }

            return string.IsNullOrEmpty(className)
                ? displayName
                : displayName + "  •  " + className;
        }

        private static string DescribeClass(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Healer:
                    return "PRIEST";
                case ClassId.Rogue:
                    return "ROGUE";
                case ClassId.Mage:
                    return "MAGE";
                case ClassId.Hunter:
                    return "HUNTER";
                case ClassId.Boss:
                    return "BOSS";
                default:
                    return string.Empty;
            }
        }

        private static void SetFill(Image fill, float amount)
        {
            if (fill != null)
            {
                fill.fillAmount = Mathf.Clamp01(amount);
            }
        }

        private static void SetTextIfChanged(Text label, string value)
        {
            if (label == null)
            {
                return;
            }

            string safeValue = value ?? string.Empty;
            if (label.text != safeValue)
            {
                label.text = safeValue;
            }
        }

        private static void SetActiveIfNeeded(GameObject target, bool isActive)
        {
            if (target != null && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_abilitySlots == null || _abilitySlots.Length != AbilityCatalog.AbilitySlotCount)
            {
                Array.Resize(ref _abilitySlots, AbilityCatalog.AbilitySlotCount);
            }

            if (_fighterViews == null ||
                _fighterViews.Length != MaximumFighterViewCount)
            {
                Array.Resize(ref _fighterViews, MaximumFighterViewCount);
            }

            if (_targetAuraRoots == null ||
                _targetAuraRoots.Length != TargetAuraSlotCount)
            {
                Array.Resize(ref _targetAuraRoots, TargetAuraSlotCount);
            }

            if (_targetAuraSurfaces == null ||
                _targetAuraSurfaces.Length != TargetAuraSlotCount)
            {
                Array.Resize(ref _targetAuraSurfaces, TargetAuraSlotCount);
            }

            if (_targetAuraLabels == null ||
                _targetAuraLabels.Length != TargetAuraSlotCount)
            {
                Array.Resize(ref _targetAuraLabels, TargetAuraSlotCount);
            }
        }
#endif
    }
}
