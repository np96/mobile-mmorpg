using SessionRpg.Core;
using SessionRpg.Unity.Input;
using SessionRpg.Unity.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SessionRpg.Unity.UI
{
    public enum AbilityReadinessState
    {
        Ready,
        Cooldown,
        GlobalCooldown,
        NoTarget,
        OutOfRange,
        MissingSetup,
        InsufficientResource,
        Moving,
        Casting,
        Controlled,
        Dead,
        Spectating,
        Unavailable,
    }

    [DisallowMultipleComponent]
    public sealed class AbilitySlotView : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        ISelectHandler,
        IDeselectHandler
    {
        private const int MaximumCachedCooldownSecond = 600;
        private const float RestingDirectionPipScale = 1f;
        private const float ExpandedDirectionScale = 1f;
        private const float SelectedDirectionScale = 1f;
        private const float RestingDirectionOffset = 78f;
        private const float ExpandedDirectionOffset = 78f;
        private const float DirectionSpellDiameter = 60f;
        private const float DirectionArtDiameter = 48f;

        private static readonly Color Obsidian = new Color32(7, 6, 10, 255);
        private static readonly Color BlackenedIron = new Color32(38, 35, 37, 255);
        private static readonly Color RaisedIron = new Color32(67, 60, 58, 255);
        private static readonly Color AgedBrass = new Color32(126, 91, 48, 255);
        private static readonly Color BrightBrass = new Color32(232, 181, 94, 255);
        private static readonly Color CooldownAmber = new Color32(255, 184, 69, 255);
        private static readonly Color GlobalCooldownCyan = new Color32(122, 229, 255, 255);
        private static readonly Color WarmText = new Color32(228, 198, 127, 255);

        private static readonly string[] CooldownSecondLabels =
            CreateCooldownLabels(string.Empty);
        private static readonly string[] SelectedCooldownLabels =
            CreateCooldownLabels("✓ ");
        private static readonly string[] DownCooldownLabels =
            CreateCooldownLabels("↓ ");
        private static readonly string[] LeftCooldownLabels =
            CreateCooldownLabels("← ");
        private static readonly string[] RightCooldownLabels =
            CreateCooldownLabels("→ ");

        [SerializeField] private AbilityId _abilityId;
        [SerializeField] private AbilityButton _inputButton;
        [SerializeField] private Image _icon;
        [SerializeField] private AbilityGlyphGraphic _glyph;
        [SerializeField] private Graphic _iconGlow;
        [SerializeField] private Graphic _rim;
        [SerializeField] private Graphic _outerFrame;
        [SerializeField] private Graphic _frameHighlight;
        [SerializeField] private Graphic _innerFrame;
        [SerializeField] private Graphic _interactionHalo;
        [SerializeField] private Text _actionLabel;
        [SerializeField] private Text _label;
        [SerializeField] private Text _purposeLabel;
        [SerializeField] private Graphic _stateBadgeSurface;
        [SerializeField] private Text _targetBadge;
        [SerializeField] private Image _cooldownFill;
        [SerializeField] private Text _cooldownText;
        [SerializeField] private Image _globalCooldownFill;
        [SerializeField] private GameObject _globalCooldownLockRoot;
        [SerializeField] private Text _blockedReasonLabel;
        [SerializeField] private Graphic _stateSurface;
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _readyPulse;
        [SerializeField] private Graphic _readyPulseGraphic;
        [SerializeField] private RectTransform _visualRoot;
        [SerializeField] private GameObject _blockedMark;

        [Header("Directional alternates")]
        [SerializeField] private AbilityRadialGuideGraphic _directionalGuide;
        [SerializeField] private GameObject _alternateUpRoot;
        [SerializeField] private Image _alternateUpIcon;
        [SerializeField] private AbilityGlyphGraphic _alternateUpGlyph;
        [SerializeField] private Graphic _alternateUpSurface;
        [SerializeField] private Graphic _alternateUpHighlight;
        [SerializeField] private Text _alternateUpLabel;
        [SerializeField] private Image _alternateUpCooldownFill;
        [SerializeField] private Text _alternateUpCooldownText;
        [SerializeField] private Text _alternateDownHint;
        [SerializeField] private GameObject _alternateDownHintRoot;
        [SerializeField] private Graphic _alternateDownHintSurface;
        [SerializeField] private Image _alternateDownIcon;
        [SerializeField] private AbilityGlyphGraphic _alternateDownGlyph;
        [SerializeField] private Text _alternateLeftHint;
        [SerializeField] private GameObject _alternateLeftHintRoot;
        [SerializeField] private Graphic _alternateLeftHintSurface;
        [SerializeField] private Image _alternateLeftIcon;
        [SerializeField] private AbilityGlyphGraphic _alternateLeftGlyph;
        [SerializeField] private Text _alternateRightHint;
        [SerializeField] private GameObject _alternateRightHintRoot;
        [SerializeField] private Graphic _alternateRightHintSurface;
        [SerializeField] private Image _alternateRightIcon;
        [SerializeField] private AbilityGlyphGraphic _alternateRightGlyph;

        [Header("State")]
        [SerializeField, Range(0f, 1f)] private float _unavailableAlpha = 0.68f;
        [SerializeField, Min(0.01f)] private float _readyPulseDuration = 0.42f;
        [SerializeField, Min(1f)] private float _readyPulseScale = 1.2f;
        [SerializeField, Min(0f)] private float _interactionSharpness = 22f;

        private int _lastCooldownSecond = int.MinValue;
        private bool _isAvailable = true;
        private bool _isPressed;
        private bool _isHovered;
        private bool _isNavigationSelected;
        private bool _isGlobalCooldownLocked;
        private float _cooldownRemaining;
        private float _globalCooldownFillAmount;
        private float _readyPulseRemaining;
        private Sprite _authoredIconSprite;
        private Vector3 _visualBaseScale = Vector3.one;
        private Color _abilityColor = Color.white;
        private string _primaryActionCaption = string.Empty;
        private string _description = string.Empty;
        private string _targetBadgeText = string.Empty;
        private string _blockedReason = string.Empty;
        private string _blockedReasonCompact = string.Empty;
        private string _interactionBlockedReason = string.Empty;
        private string _interactionBlockedReasonCompact = string.Empty;
        private AbilityReadinessState _readinessState = AbilityReadinessState.Ready;
        private bool _isInteractionEnabled = true;
        private AbilityId _alternateUpAbilityId;
        private AbilityId _alternateDownAbilityId;
        private AbilityId _alternateLeftAbilityId;
        private AbilityId _alternateRightAbilityId;
        private Sprite _authoredAlternateUpIconSprite;
        private Color _alternateUpColor = Color.white;
        private string _alternateUpName = string.Empty;
        private string _alternateUpDescription = string.Empty;
        private string _alternateUpBlockedReason = string.Empty;
        private string _alternateUpBlockedCaption = string.Empty;
        private string _alternateUpCaption = string.Empty;
        private string _alternateUpSelectedCaption = string.Empty;
        private string _alternateUpActionCaption = string.Empty;
        private float _alternateUpCooldownRemaining;
        private float _alternateUpCooldownFillAmount;
        private float _alternateUpGlobalCooldownFillAmount;
        private bool _alternateUpGlobalCooldownLocked;
        private AbilityReadinessState _alternateUpReadinessState =
            AbilityReadinessState.Unavailable;
        private DirectionHintState _alternateDownState;
        private DirectionHintState _alternateLeftState;
        private DirectionHintState _alternateRightState;
        private bool _directionalGlobalCooldownLocked;
        private bool _alternatePaletteExpanded;
        private int _restingSiblingIndex;
        private bool _isRaisedForDirectionalPalette;
        private bool _restoreSiblingWhenActive;

        public AbilityId AbilityId => _abilityId;

        public AbilityId AlternateUpAbilityId => _alternateUpAbilityId;

        public AbilityId SelectedAbilityId => _inputButton != null
            ? _inputButton.SelectedAbilityId
            : _abilityId;

        public bool HasAlternateUp => _alternateUpAbilityId != AbilityId.None;

        public AbilityId GetDirectionalAbility(AbilitySlotDirection direction)
        {
            switch (direction)
            {
                case AbilitySlotDirection.Up:
                    return _alternateUpAbilityId;
                case AbilitySlotDirection.Down:
                    return _alternateDownAbilityId;
                case AbilitySlotDirection.Left:
                    return _alternateLeftAbilityId;
                case AbilitySlotDirection.Right:
                    return _alternateRightAbilityId;
                default:
                    return _abilityId;
            }
        }

        public bool IsAlternateUpSelected => _inputButton != null &&
            _inputButton.Selection.IsPressed &&
            _inputButton.Selection.IsLocked &&
            !_inputButton.Selection.IsCanceled &&
            _inputButton.Selection.Direction == AbilityFlickDirection.Up &&
            _inputButton.Selection.AbilityId == _alternateUpAbilityId;

        public bool IsAlternateUpVisible => HasAlternateUp &&
            IsDirectionalPaletteExpanded();

        public AbilityRadialGuideGraphic DirectionalGuide => _directionalGuide;

        public string AlternateUpVisibleCaption => _alternateUpLabel != null
            ? _alternateUpLabel.text
            : _alternateUpCaption;

        public string AlternateUpDescription => _alternateUpDescription;

        public AbilityReadinessState AlternateUpReadinessState =>
            ResolveAlternateUpReadiness();

        public bool IsAvailable => _isAvailable;

        public bool IsGlobalCooldownLocked => _isGlobalCooldownLocked;

        public bool IsInteractionEnabled => _isInteractionEnabled;

        public bool IsHovered => _isHovered;

        public bool IsPresentationSelected => HasLockedDirectionalSelection() ||
            _isNavigationSelected;

        public AbilityReadinessState ReadinessState => ResolveEffectiveReadiness();

        public string BlockedReason => _isInteractionEnabled
            ? _blockedReason
            : _interactionBlockedReason;

        public string Description => _description;

        public AbilityGlyphGraphic Glyph => _glyph;

        public string VisibleCaption => _actionLabel != null ? _actionLabel.text : string.Empty;

        public bool UsesAuthoredIcon => _icon != null &&
            _icon.enabled &&
            _icon.sprite != null &&
            _icon.color.a > 0.01f;

        private void OnEnable()
        {
            if (!_restoreSiblingWhenActive)
            {
                _restingSiblingIndex = transform.GetSiblingIndex();
            }

            _isRaisedForDirectionalPalette = false;

            if (_authoredIconSprite == null && _icon != null)
            {
                _authoredIconSprite = _icon.sprite;
            }

            if (_authoredAlternateUpIconSprite == null && _alternateUpIcon != null)
            {
                _authoredAlternateUpIconSprite = _alternateUpIcon.sprite;
            }

            ConfigureCooldownFill();

            if (_visualRoot != null)
            {
                _visualBaseScale = _visualRoot.localScale;
            }

            if (_inputButton != null)
            {
                _inputButton.BindAbility(_abilityId);
                _inputButton.ClearDirectionalAbilities();
                _inputButton.BindDirectionalAbility(
                    AbilityFlickDirection.Up,
                    _alternateUpAbilityId);
                _inputButton.BindDirectionalAbility(
                    AbilityFlickDirection.Down,
                    _alternateDownAbilityId);
                _inputButton.BindDirectionalAbility(
                    AbilityFlickDirection.Left,
                    _alternateLeftAbilityId);
                _inputButton.BindDirectionalAbility(
                    AbilityFlickDirection.Right,
                    _alternateRightAbilityId);
                _inputButton.GestureChanged += OnGestureChanged;
                _inputButton.SelectionChanged += OnFlickSelectionChanged;
            }

            RefreshVisualState();
            RefreshAlternateVisualState();
            ApplyReadyPulse(0f);
        }

        private void OnDisable()
        {
            RestoreDirectionalPaletteLayer();

            if (_visualRoot != null)
            {
                _visualRoot.localScale = _visualBaseScale;
            }

            if (_inputButton != null)
            {
                _inputButton.GestureChanged -= OnGestureChanged;
                _inputButton.SelectionChanged -= OnFlickSelectionChanged;
            }

            _isPressed = false;
            _isHovered = false;
            _isNavigationSelected = false;
            _isGlobalCooldownLocked = false;
            _cooldownRemaining = 0f;
            _globalCooldownFillAmount = 0f;
            _blockedReason = string.Empty;
            _blockedReasonCompact = string.Empty;
            _readinessState = AbilityReadinessState.Ready;
            _readyPulseRemaining = 0f;
            _alternateUpCooldownRemaining = 0f;
            _alternateUpCooldownFillAmount = 0f;
            _alternateUpGlobalCooldownFillAmount = 0f;
            _alternateUpGlobalCooldownLocked = false;
            _directionalGlobalCooldownLocked = false;
            if (_globalCooldownFill != null)
            {
                _globalCooldownFill.fillAmount = 0f;
                _globalCooldownFill.enabled = false;
            }

            SetActiveIfNeeded(_globalCooldownLockRoot, false);
            if (_alternateUpCooldownFill != null)
            {
                _alternateUpCooldownFill.fillAmount = 0f;
                _alternateUpCooldownFill.enabled = false;
            }
            if (_blockedReasonLabel != null)
            {
                _blockedReasonLabel.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            RestoreDeferredDirectionalPaletteLayer();

            float deltaTime = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            if (_visualRoot != null)
            {
                float targetScale = HasDirectionalPaletteBindings()
                    ? 1f
                    : HasLockedDirectionalSelection()
                        ? 0.975f
                    : _isPressed
                        ? 0.93f
                        : _isHovered || _isNavigationSelected
                            ? 1.035f
                            : 1f;
                float blend = _interactionSharpness <= 0f
                    ? 1f
                    : 1f - Mathf.Exp(-_interactionSharpness * deltaTime);
                _visualRoot.localScale = Vector3.Lerp(
                    _visualRoot.localScale,
                    _visualBaseScale * targetScale,
                    blend);
            }

            if (_readyPulseRemaining > 0f)
            {
                _readyPulseRemaining = Mathf.Max(0f, _readyPulseRemaining - deltaTime);
                float normalizedTime = 1f - (_readyPulseRemaining / _readyPulseDuration);
                float strength = Mathf.Sin(normalizedTime * Mathf.PI);
                ApplyReadyPulse(strength);
            }
        }

        public void Bind(AbilityId abilityId, Sprite icon, string displayName)
        {
            Bind(
                abilityId,
                icon,
                displayName,
                ResolveActionKeyword(abilityId),
                string.Empty,
                string.Empty);
        }

        public void Bind(
            AbilityId abilityId,
            Sprite icon,
            string displayName,
            string actionKeyword,
            string shortPurpose,
            string description)
        {
            ClearDirectionalAbilities();
            _abilityId = abilityId;
            _description = description ?? string.Empty;
            _blockedReason = string.Empty;
            _blockedReasonCompact = string.Empty;
            _readinessState = abilityId == AbilityId.None
                ? AbilityReadinessState.Unavailable
                : AbilityReadinessState.Ready;
            if (_inputButton != null)
            {
                _inputButton.BindAbility(_abilityId);
            }

            if (_icon != null)
            {
                if (_authoredIconSprite == null && _icon.sprite != null)
                {
                    _authoredIconSprite = _icon.sprite;
                }

                bool hasAuthoredIcon = icon != null;
                _icon.sprite = hasAuthoredIcon ? icon : _authoredIconSprite;
                _icon.enabled = hasAuthoredIcon;
                _icon.color = hasAuthoredIcon ? Color.white : Color.clear;
            }

            _abilityColor = AbilityPresentationCatalog.TryGet(
                abilityId,
                out AbilityPresentation presentation)
                    ? presentation.Color
                    : Color.white;
            if (_glyph != null)
            {
                _glyph.Bind(abilityId, _abilityColor);
                _glyph.enabled = icon == null && abilityId != AbilityId.None;
            }

            if (_readyPulseGraphic != null)
            {
                _readyPulseGraphic.color = WithAlpha(_abilityColor, 0f);
            }

            if (_actionLabel != null)
            {
                string caption = !string.IsNullOrWhiteSpace(actionKeyword)
                    ? actionKeyword
                    : ResolveActionKeyword(abilityId);
                if (string.IsNullOrWhiteSpace(caption))
                {
                    caption = displayName;
                }

                _primaryActionCaption = (caption ?? string.Empty).ToUpperInvariant();
                _actionLabel.text = _primaryActionCaption;
            }

            if (_label != null)
            {
                _label.text = displayName ?? string.Empty;
                _label.enabled = false;
            }

            if (_purposeLabel != null)
            {
                _purposeLabel.text = shortPurpose ?? string.Empty;
                _purposeLabel.enabled = !string.IsNullOrWhiteSpace(shortPurpose);
            }

            if (_targetBadge != null)
            {
                _targetBadgeText = AbilityCatalog.TryGet(
                    abilityId,
                    out AbilityDefinition definition)
                        ? ResolveTargetBadge(definition.Targeting)
                        : string.Empty;
                _targetBadge.text = _targetBadgeText;
            }

            RefreshVisualState();
        }

        public void BindAlternateUp(
            AbilityId abilityId,
            Sprite icon,
            string displayName,
            string description = null)
        {
            _alternateUpAbilityId = abilityId;
            _alternateUpName = displayName ?? string.Empty;
            _alternateUpDescription = description ?? string.Empty;
            _alternateUpBlockedReason = string.Empty;
            _alternateUpBlockedCaption = string.Empty;
            CacheAlternateCaptions();
            _alternateUpCooldownRemaining = 0f;
            _alternateUpCooldownFillAmount = 0f;
            _alternateUpGlobalCooldownFillAmount = 0f;
            _alternateUpGlobalCooldownLocked = false;
            _alternateUpReadinessState = abilityId == AbilityId.None
                ? AbilityReadinessState.Unavailable
                : AbilityReadinessState.Ready;
            _inputButton?.BindDirectionalAbility(
                AbilityFlickDirection.Up,
                abilityId);

            if (_alternateUpIcon != null)
            {
                if (_authoredAlternateUpIconSprite == null &&
                    _alternateUpIcon.sprite != null)
                {
                    _authoredAlternateUpIconSprite = _alternateUpIcon.sprite;
                }

                bool hasAuthoredIcon = icon != null;
                _alternateUpIcon.sprite = hasAuthoredIcon
                    ? icon
                    : _authoredAlternateUpIconSprite;
                _alternateUpIcon.enabled = hasAuthoredIcon;
                _alternateUpIcon.color = hasAuthoredIcon ? Color.white : Color.clear;
            }

            _alternateUpColor = AbilityPresentationCatalog.TryGet(
                abilityId,
                out AbilityPresentation presentation)
                    ? presentation.Color
                    : Color.white;
            if (_alternateUpGlyph != null)
            {
                _alternateUpGlyph.Bind(abilityId, _alternateUpColor);
                _alternateUpGlyph.enabled = icon == null && abilityId != AbilityId.None;
            }

            RefreshAlternateVisualState();
        }

        public void BindDirectionalAbility(
            AbilitySlotDirection direction,
            AbilityId abilityId)
        {
            BindDirectionalAbility(direction, abilityId, null);
        }

        public void BindDirectionalAbility(
            AbilitySlotDirection direction,
            AbilityId abilityId,
            Sprite icon)
        {
            switch (direction)
            {
                case AbilitySlotDirection.Up:
                    if (AbilityCatalog.TryGet(
                        abilityId,
                        out AbilityDefinition definition))
                    {
                        BindAlternateUp(
                            abilityId,
                            icon,
                            definition.DisplayName,
                            definition.Description);
                    }
                    else
                    {
                        BindAlternateUp(abilityId, icon, string.Empty, string.Empty);
                    }
                    break;
                case AbilitySlotDirection.Down:
                    _alternateDownAbilityId = abilityId;
                    _alternateDownState = DirectionHintState.Create(abilityId, "↓");
                    BindDirectionalArt(
                        _alternateDownIcon,
                        _alternateDownGlyph,
                        abilityId,
                        icon);
                    _inputButton?.BindDirectionalAbility(
                        AbilityFlickDirection.Down,
                        abilityId);
                    break;
                case AbilitySlotDirection.Left:
                    _alternateLeftAbilityId = abilityId;
                    _alternateLeftState = DirectionHintState.Create(abilityId, "←");
                    BindDirectionalArt(
                        _alternateLeftIcon,
                        _alternateLeftGlyph,
                        abilityId,
                        icon);
                    _inputButton?.BindDirectionalAbility(
                        AbilityFlickDirection.Left,
                        abilityId);
                    break;
                case AbilitySlotDirection.Right:
                    _alternateRightAbilityId = abilityId;
                    _alternateRightState = DirectionHintState.Create(abilityId, "→");
                    BindDirectionalArt(
                        _alternateRightIcon,
                        _alternateRightGlyph,
                        abilityId,
                        icon);
                    _inputButton?.BindDirectionalAbility(
                        AbilityFlickDirection.Right,
                        abilityId);
                    break;
            }

            RefreshDirectionalHints();
        }

        public void ClearDirectionalAbilities()
        {
            _inputButton?.ClearDirectionalAbilities();
            _alternateUpAbilityId = AbilityId.None;
            _alternateDownAbilityId = AbilityId.None;
            _alternateLeftAbilityId = AbilityId.None;
            _alternateRightAbilityId = AbilityId.None;
            _alternateDownState = DirectionHintState.Create(AbilityId.None, "↓");
            _alternateLeftState = DirectionHintState.Create(AbilityId.None, "←");
            _alternateRightState = DirectionHintState.Create(AbilityId.None, "→");
            BindDirectionalArt(
                _alternateDownIcon,
                _alternateDownGlyph,
                AbilityId.None,
                null);
            BindDirectionalArt(
                _alternateLeftIcon,
                _alternateLeftGlyph,
                AbilityId.None,
                null);
            BindDirectionalArt(
                _alternateRightIcon,
                _alternateRightGlyph,
                AbilityId.None,
                null);
            _alternateUpName = string.Empty;
            _alternateUpDescription = string.Empty;
            _alternateUpBlockedReason = string.Empty;
            _alternateUpBlockedCaption = string.Empty;
            _alternateUpCaption = string.Empty;
            _alternateUpSelectedCaption = string.Empty;
            _alternateUpActionCaption = string.Empty;
            _alternateUpCooldownRemaining = 0f;
            _alternateUpCooldownFillAmount = 0f;
            _alternateUpGlobalCooldownFillAmount = 0f;
            _alternateUpGlobalCooldownLocked = false;
            _alternateUpReadinessState = AbilityReadinessState.Unavailable;
            if (_alternateUpIcon != null)
            {
                _alternateUpIcon.enabled = false;
                _alternateUpIcon.color = Color.clear;
            }
            if (_alternateUpGlyph != null)
            {
                _alternateUpGlyph.Bind(AbilityId.None, Color.white);
                _alternateUpGlyph.enabled = false;
                _alternateUpGlyph.color = Color.clear;
            }
            RefreshAlternateVisualState();
            RefreshDirectionalHints();
        }

        public bool ContainsAbility(AbilityId abilityId)
        {
            return abilityId != AbilityId.None &&
                (abilityId == _abilityId ||
                 abilityId == _alternateUpAbilityId ||
                 abilityId == _alternateDownAbilityId ||
                 abilityId == _alternateLeftAbilityId ||
                 abilityId == _alternateRightAbilityId);
        }

        public bool SetAbilityCooldown(
            AbilityId abilityId,
            float remainingSeconds,
            float totalSeconds,
            bool isAvailable)
        {
            if (abilityId == _abilityId)
            {
                SetCooldown(remainingSeconds, totalSeconds, isAvailable);
                return true;
            }

            if (abilityId != _alternateUpAbilityId)
            {
                if (abilityId == _alternateDownAbilityId)
                {
                    UpdateDirectionHintCooldown(
                        ref _alternateDownState,
                        remainingSeconds,
                        isAvailable);
                    return true;
                }

                if (abilityId == _alternateLeftAbilityId)
                {
                    UpdateDirectionHintCooldown(
                        ref _alternateLeftState,
                        remainingSeconds,
                        isAvailable);
                    return true;
                }

                if (abilityId == _alternateRightAbilityId)
                {
                    UpdateDirectionHintCooldown(
                        ref _alternateRightState,
                        remainingSeconds,
                        isAvailable);
                    return true;
                }

                return false;
            }

            SetAlternateUpCooldown(remainingSeconds, totalSeconds, isAvailable);
            return true;
        }

        public bool SetAbilityReadiness(
            AbilityId abilityId,
            AbilityReadinessState state,
            string blockedReason = null)
        {
            if (abilityId == _abilityId)
            {
                SetReadiness(state, blockedReason);
                return true;
            }

            if (abilityId != _alternateUpAbilityId)
            {
                if (abilityId == _alternateDownAbilityId)
                {
                    UpdateDirectionHintReadiness(
                        ref _alternateDownState,
                        state,
                        blockedReason);
                    return true;
                }

                if (abilityId == _alternateLeftAbilityId)
                {
                    UpdateDirectionHintReadiness(
                        ref _alternateLeftState,
                        state,
                        blockedReason);
                    return true;
                }

                if (abilityId == _alternateRightAbilityId)
                {
                    UpdateDirectionHintReadiness(
                        ref _alternateRightState,
                        state,
                        blockedReason);
                    return true;
                }

                return false;
            }

            string reason = blockedReason ?? string.Empty;
            if (_alternateUpReadinessState == state &&
                string.Equals(_alternateUpBlockedReason, reason))
            {
                return true;
            }

            _alternateUpReadinessState = state;
            _alternateUpBlockedReason = reason;
            _alternateUpBlockedCaption = ResolveCompactBlockedReason(reason);
            RefreshAlternateVisualState();
            return true;
        }

        private void SetAlternateUpCooldown(
            float remainingSeconds,
            float totalSeconds,
            bool isAvailable)
        {
            AbilityReadinessState previousState = ResolveAlternateUpReadiness();
            float clampedRemaining = Mathf.Max(0f, remainingSeconds);
            _alternateUpCooldownRemaining = clampedRemaining;
            float fillAmount = totalSeconds > 0.001f
                ? Mathf.Clamp01(clampedRemaining / totalSeconds)
                : 0f;
            _alternateUpCooldownFillAmount = fillAmount;
            if (_alternateUpCooldownFill != null)
            {
                _alternateUpCooldownFill.fillAmount = fillAmount;
                _alternateUpCooldownFill.enabled =
                    IsDirectionalPaletteExpanded() && clampedRemaining > 0.0001f;
            }

            if (clampedRemaining <= 0.0001f &&
                (_alternateUpReadinessState == AbilityReadinessState.Cooldown ||
                 _alternateUpReadinessState == AbilityReadinessState.Unavailable))
            {
                _alternateUpReadinessState = isAvailable
                    ? AbilityReadinessState.Ready
                    : AbilityReadinessState.Unavailable;
            }
            else if (clampedRemaining <= 0.0001f &&
                     !isAvailable &&
                     _alternateUpReadinessState == AbilityReadinessState.Ready)
            {
                _alternateUpReadinessState = AbilityReadinessState.Unavailable;
            }
            else if (clampedRemaining > 0.0001f)
            {
                _alternateUpReadinessState = AbilityReadinessState.Cooldown;
            }

            if (previousState != ResolveAlternateUpReadiness())
            {
                RefreshAlternateVisualState();
                return;
            }

            RefreshAlternateCooldownText();
        }

        public void SetCooldown(float remainingSeconds, float totalSeconds, bool isAvailable)
        {
            AbilityReadinessState previousState = ResolveEffectiveReadiness();
            float clampedRemaining = Mathf.Max(0f, remainingSeconds);
            _cooldownRemaining = clampedRemaining;
            float fillAmount = totalSeconds > 0.001f
                ? Mathf.Clamp01(clampedRemaining / totalSeconds)
                : 0f;
            if (_cooldownFill != null)
            {
                _cooldownFill.fillAmount = fillAmount;
                _cooldownFill.enabled = false;
            }

            int cooldownSecond = clampedRemaining > 0f ? Mathf.CeilToInt(clampedRemaining) : 0;
            if (_cooldownText != null && cooldownSecond != _lastCooldownSecond)
            {
                _cooldownText.text = ResolveCooldownSecondLabel(cooldownSecond);
                _cooldownText.enabled = cooldownSecond > 0;
            }

            _lastCooldownSecond = cooldownSecond;
            if (clampedRemaining <= 0.0001f &&
                (_readinessState == AbilityReadinessState.Cooldown ||
                 _readinessState == AbilityReadinessState.Unavailable))
            {
                _readinessState = isAvailable
                    ? AbilityReadinessState.Ready
                    : AbilityReadinessState.Unavailable;
            }
            else if (clampedRemaining <= 0.0001f &&
                     !isAvailable &&
                     _readinessState == AbilityReadinessState.Ready)
            {
                _readinessState = AbilityReadinessState.Unavailable;
            }
            else if (clampedRemaining > 0.0001f)
            {
                _readinessState = AbilityReadinessState.Cooldown;
            }

            bool wasAvailable = _isAvailable;
            if (previousState != ResolveEffectiveReadiness())
            {
                RefreshVisualState();
            }

            if (!wasAvailable && _isAvailable)
            {
                PlayReadyPulse();
            }
        }

        public void SetReadiness(AbilityReadinessState state, string blockedReason = null)
        {
            string reason = blockedReason ?? string.Empty;
            if (_readinessState == state && string.Equals(_blockedReason, reason))
            {
                return;
            }

            bool wasAvailable = _isAvailable;
            _readinessState = state;
            _blockedReason = reason;
            _blockedReasonCompact = ResolveCompactBlockedReason(reason);
            RefreshVisualState();
            if (!wasAvailable && _isAvailable)
            {
                PlayReadyPulse();
            }
        }

        public void SetGlobalCooldown(
            float remainingSeconds,
            float totalSeconds,
            bool isLocked)
        {
            AbilityReadinessState previousState = ResolveEffectiveReadiness();
            AbilityReadinessState previousAlternateState = ResolveAlternateUpReadiness();
            bool wasAvailable = _isAvailable;
            float safeRemaining = Mathf.Max(0f, remainingSeconds);
            float rawFillAmount = isLocked
                ? totalSeconds > 0.001f
                    ? Mathf.Clamp01(safeRemaining / totalSeconds)
                    : 1f
                : 0f;
            bool previousDirectionalLock = _directionalGlobalCooldownLocked;
            _isGlobalCooldownLocked = isLocked && UsesGlobalCooldown(_abilityId);
            _directionalGlobalCooldownLocked = isLocked;
            _globalCooldownFillAmount = _isGlobalCooldownLocked ? rawFillAmount : 0f;
            _alternateUpGlobalCooldownLocked = isLocked &&
                UsesGlobalCooldown(_alternateUpAbilityId);
            _alternateUpGlobalCooldownFillAmount = _alternateUpGlobalCooldownLocked
                ? rawFillAmount
                : 0f;
            if (_globalCooldownFill != null)
            {
                _globalCooldownFill.fillAmount = _globalCooldownFillAmount;
                _globalCooldownFill.enabled = false;
            }

            if (_alternateUpCooldownFill != null &&
                _alternateUpCooldownRemaining <= 0.0001f)
            {
                _alternateUpCooldownFill.fillAmount =
                    _alternateUpGlobalCooldownFillAmount;
                _alternateUpCooldownFill.enabled =
                    IsDirectionalPaletteExpanded() &&
                    _alternateUpGlobalCooldownFillAmount > 0.0001f;
            }

            Text globalCooldownLabel = _globalCooldownLockRoot != null
                ? _globalCooldownLockRoot.GetComponent<Text>()
                : null;
            if (globalCooldownLabel != null)
            {
                globalCooldownLabel.text = "GCD";
            }

            if (previousState != ResolveEffectiveReadiness() ||
                previousAlternateState != ResolveAlternateUpReadiness())
            {
                RefreshVisualState();
            }
            if (previousDirectionalLock != _directionalGlobalCooldownLocked)
            {
                RefreshDirectionalHints();
            }
            if (!wasAvailable && _isAvailable)
            {
                PlayReadyPulse();
            }
        }

        public void SetPressed(bool isPressed)
        {
            _isPressed = isPressed;
            RefreshVisualState();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (!_isInteractionEnabled || _isHovered)
            {
                return;
            }

            _isHovered = true;
            RefreshVisualState();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isHovered)
            {
                return;
            }

            _isHovered = false;
            RefreshVisualState();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (!_isInteractionEnabled || _isNavigationSelected)
            {
                return;
            }

            _isNavigationSelected = true;
            RefreshVisualState();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (!_isNavigationSelected)
            {
                return;
            }

            _isNavigationSelected = false;
            RefreshVisualState();
        }

        public void SetInteractionEnabled(bool isEnabled, string blockedReason = null)
        {
            string reason = isEnabled ? string.Empty : blockedReason ?? string.Empty;
            if (_isInteractionEnabled == isEnabled &&
                string.Equals(_interactionBlockedReason, reason))
            {
                return;
            }

            if (!isEnabled)
            {
                _inputButton?.CancelActivePointer();
                _isHovered = false;
                _isNavigationSelected = false;
            }

            _isInteractionEnabled = isEnabled;
            _interactionBlockedReason = reason;
            _interactionBlockedReasonCompact = ResolveCompactBlockedReason(reason);
            if (_inputButton != null)
            {
                _inputButton.enabled = isEnabled;
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.interactable = isEnabled;
                _canvasGroup.blocksRaycasts = isEnabled;
            }

            RefreshVisualState();
            RefreshDirectionalHints();
        }

        public void PlayReadyPulse()
        {
            _readyPulseRemaining = _readyPulseDuration;
            ApplyReadyPulse(0f);
        }

        private void OnGestureChanged(AbilityGesture gesture)
        {
            switch (gesture.Phase)
            {
                case AbilityGesturePhase.Pressed:
                    if (gesture.AbilityId == _abilityId)
                    {
                        SetPressed(true);
                    }

                    break;
                case AbilityGesturePhase.Tapped:
                case AbilityGesturePhase.Flicked:
                case AbilityGesturePhase.Canceled:
                    SetPressed(false);
                    break;
            }
        }

        private void OnFlickSelectionChanged(AbilityFlickSelection selection)
        {
            _isPressed = selection.IsPressed && !selection.IsCanceled;
            SetDirectionalPaletteLayer(_isPressed);
            RefreshVisualState();
            RefreshDirectionalHints();
        }

        private void SetDirectionalPaletteLayer(bool isRaised)
        {
            Transform parent = transform.parent;
            if (parent == null || isRaised == _isRaisedForDirectionalPalette)
            {
                return;
            }

            if (isRaised)
            {
                RestoreDeferredDirectionalPaletteLayer();
                _restingSiblingIndex = transform.GetSiblingIndex();
                transform.SetAsLastSibling();
                _isRaisedForDirectionalPalette = true;
                return;
            }

            RestoreDirectionalPaletteLayer();
        }

        private void RestoreDirectionalPaletteLayer()
        {
            Transform parent = transform.parent;
            if (!_isRaisedForDirectionalPalette || parent == null)
            {
                if (parent == null)
                {
                    _restoreSiblingWhenActive = false;
                }

                _isRaisedForDirectionalPalette = false;
                return;
            }

            if (!gameObject.activeInHierarchy ||
                !parent.gameObject.activeInHierarchy)
            {
                _isRaisedForDirectionalPalette = false;
                _restoreSiblingWhenActive = true;
                return;
            }

            transform.SetSiblingIndex(Mathf.Clamp(
                _restingSiblingIndex,
                0,
                parent.childCount - 1));
            _isRaisedForDirectionalPalette = false;
            _restoreSiblingWhenActive = false;
        }

        private void RestoreDeferredDirectionalPaletteLayer()
        {
            if (!_restoreSiblingWhenActive)
            {
                return;
            }

            Transform parent = transform.parent;
            if (parent == null)
            {
                _restoreSiblingWhenActive = false;
                return;
            }

            if (!gameObject.activeInHierarchy ||
                !parent.gameObject.activeInHierarchy)
            {
                return;
            }

            transform.SetSiblingIndex(Mathf.Clamp(
                _restingSiblingIndex,
                0,
                parent.childCount - 1));
            _restoreSiblingWhenActive = false;
        }

        private bool HasLockedDirectionalSelection()
        {
            if (_inputButton == null)
            {
                return false;
            }

            AbilityFlickSelection selection = _inputButton.Selection;
            return selection.IsPressed &&
                selection.IsLocked &&
                selection.HasDirectionalBinding &&
                !selection.IsCanceled;
        }

        private bool IsDirectionalPaletteExpanded()
        {
            if (_inputButton == null)
            {
                return false;
            }

            AbilityFlickSelection selection = _inputButton.Selection;
            return selection.IsPressed && !selection.IsCanceled;
        }

        private bool HasDirectionalPaletteBindings() =>
            _alternateUpAbilityId != AbilityId.None ||
            _alternateDownAbilityId != AbilityId.None ||
            _alternateLeftAbilityId != AbilityId.None ||
            _alternateRightAbilityId != AbilityId.None;

        private void RefreshVisualState()
        {
            AbilityReadinessState effectiveState = ResolveEffectiveReadiness();
            _isAvailable = effectiveState == AbilityReadinessState.Ready;
            bool isGlobalCooldown = effectiveState == AbilityReadinessState.GlobalCooldown;
            bool isOwnCooldown = effectiveState == AbilityReadinessState.Cooldown;
            bool isReasonState = IsReasonState(effectiveState);
            bool isDirectionalSelection = HasLockedDirectionalSelection();
            bool isHighlighted = _isHovered || _isNavigationSelected;
            if (_actionLabel != null)
            {
                // The compact mobile HUD is icon-first. Names appear only while
                // inspecting or pressing a control, keeping ten buttons readable.
                SetActiveIfNeeded(
                    _actionLabel.gameObject,
                    _isPressed || isHighlighted);
            }
            string activeBlockedReason = _isInteractionEnabled
                ? _blockedReason
                : _interactionBlockedReason;
            bool isProgressionLock = effectiveState == AbilityReadinessState.Unavailable &&
                !string.IsNullOrEmpty(activeBlockedReason) &&
                activeBlockedReason.StartsWith(
                    "LEVEL ",
                    System.StringComparison.OrdinalIgnoreCase);
            bool isTargetSelectionBlocker = isReasonState &&
                IsTargetSelectionReason(activeBlockedReason);
            bool keepIconLegible = _isAvailable ||
                isGlobalCooldown ||
                isTargetSelectionBlocker;
            bool showReasonBadge = isReasonState && !isTargetSelectionBlocker;
            Color readinessColor = isProgressionLock
                ? new Color(0.86f, 0.68f, 0.34f, 1f)
                : ResolveReadinessColor(effectiveState);
            Color selectedAbilityColor = ResolveSelectedDirectionalColor();
            if (_stateSurface != null)
            {
                Color faceAccent = isDirectionalSelection
                    ? selectedAbilityColor
                    : isOwnCooldown
                        ? CooldownAmber
                        : isGlobalCooldown
                            ? GlobalCooldownCyan
                            : isReasonState && !isTargetSelectionBlocker
                                ? readinessColor
                                : _abilityColor;
                float faceBlend = isDirectionalSelection
                    ? 0.3f
                    : _isPressed
                        ? 0.25f
                        : isHighlighted
                            ? 0.22f
                            : 0.18f;
                _stateSurface.color = WithAlpha(
                    Color.Lerp(Obsidian, faceAccent, faceBlend),
                    0.94f);
            }

            if (_outerFrame != null)
            {
                _outerFrame.color = WithAlpha(
                    isDirectionalSelection
                        ? Color.Lerp(BlackenedIron, AgedBrass, 0.38f)
                        : _isPressed
                            ? Color.Lerp(BlackenedIron, AgedBrass, 0.2f)
                            : BlackenedIron,
                    0.96f);
            }

            if (_frameHighlight != null)
            {
                _frameHighlight.color = WithAlpha(
                    isDirectionalSelection
                        ? Color.Lerp(RaisedIron, BrightBrass, 0.62f)
                        : _isPressed
                            ? Color.Lerp(RaisedIron, BrightBrass, 0.36f)
                            : isHighlighted
                                ? Color.Lerp(RaisedIron, _abilityColor, 0.28f)
                                : RaisedIron,
                    isDirectionalSelection
                        ? 0.96f
                        : _isPressed
                            ? 0.82f
                            : isHighlighted
                                ? 0.72f
                                : 0.58f);
            }

            if (_innerFrame != null)
            {
                Color innerAccent = isOwnCooldown
                    ? CooldownAmber
                    : isGlobalCooldown
                        ? GlobalCooldownCyan
                        : isReasonState
                            ? readinessColor
                            : isDirectionalSelection
                                ? selectedAbilityColor
                                : _abilityColor;
                _innerFrame.color = WithAlpha(
                    Color.Lerp(Obsidian, innerAccent, 0.12f),
                    0.92f);
            }

            if (_interactionHalo != null)
            {
                Color haloColor = isDirectionalSelection
                    ? Color.Lerp(selectedAbilityColor, BrightBrass, 0.46f)
                    : isOwnCooldown
                        ? CooldownAmber
                        : isGlobalCooldown
                            ? GlobalCooldownCyan
                            : isReasonState
                                ? readinessColor
                                : _abilityColor;
                _interactionHalo.color = WithAlpha(
                    haloColor,
                    isDirectionalSelection
                        ? 0.68f
                        : _isPressed
                            ? 0.46f
                            : isHighlighted
                                ? 0.26f
                                : 0f);
            }

            if (_rim != null)
            {
                Color rimColor = isDirectionalSelection
                    ? Color.Lerp(selectedAbilityColor, BrightBrass, 0.36f)
                    : isOwnCooldown
                        ? CooldownAmber
                        : isGlobalCooldown
                            ? GlobalCooldownCyan
                            : isReasonState && !isTargetSelectionBlocker
                                ? readinessColor
                                : _abilityColor;
                _rim.color = WithAlpha(
                    rimColor,
                    isDirectionalSelection
                        ? 0.96f
                        : isOwnCooldown || isGlobalCooldown
                            ? 0.92f
                            : isReasonState
                                ? 0.88f
                                : isHighlighted || _isPressed
                                    ? 0.9f
                                    : 0.78f);
            }

            if (_iconGlow != null)
            {
                Color glowAccent = isDirectionalSelection
                    ? selectedAbilityColor
                    : isTargetSelectionBlocker
                        ? _abilityColor
                        : isOwnCooldown
                            ? CooldownAmber
                            : isGlobalCooldown
                                ? GlobalCooldownCyan
                                : isReasonState
                                    ? readinessColor
                                    : _abilityColor;
                float glowAlpha = isDirectionalSelection
                    ? 0.24f
                    : _isPressed
                        ? 0.2f
                        : isHighlighted
                            ? 0.16f
                            : 0.11f;
                _iconGlow.color = WithAlpha(
                    Color.Lerp(glowAccent, Color.white, 0.08f),
                    glowAlpha);
            }

            if (_actionLabel != null)
            {
                _actionLabel.color = isDirectionalSelection
                    ? new Color32(255, 239, 174, 255)
                    : _isPressed
                        ? Color.white
                        : isReasonState
                            ? Color.Lerp(WarmText, readinessColor, 0.12f)
                            : isHighlighted
                                ? new Color32(246, 222, 162, 255)
                                : WarmText;
            }

            if (_glyph != null)
            {
                _glyph.color = keepIconLegible
                    ? isGlobalCooldown
                        ? new Color(0.82f, 0.96f, 1f, 0.94f)
                        : isTargetSelectionBlocker
                            ? new Color(0.92f, 0.96f, 1f, 0.94f)
                            : Color.white
                    : isOwnCooldown
                        ? new Color(0.4f, 0.43f, 0.48f, _unavailableAlpha)
                        : new Color(0.58f, 0.62f, 0.68f, _unavailableAlpha);
            }

            if (_icon != null && _icon.enabled)
            {
                _icon.color = keepIconLegible
                    ? isGlobalCooldown
                        ? new Color(0.82f, 0.96f, 1f, 0.94f)
                        : isTargetSelectionBlocker
                            ? new Color(0.92f, 0.96f, 1f, 0.94f)
                            : Color.white
                    : isOwnCooldown
                        ? new Color(0.38f, 0.4f, 0.44f, _unavailableAlpha)
                        : new Color(0.52f, 0.56f, 0.62f, _unavailableAlpha);
            }

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _isInteractionEnabled ? 1f : 0.58f;
                _canvasGroup.interactable = _isInteractionEnabled;
                _canvasGroup.blocksRaycasts = _isInteractionEnabled;
            }

            bool showGlobalCooldown = isGlobalCooldown &&
                _globalCooldownFillAmount > 0.0001f;
            if (_cooldownFill != null)
            {
                _cooldownFill.enabled = false;
                _cooldownFill.color = Color.clear;
            }
            if (_globalCooldownFill != null)
            {
                _globalCooldownFill.enabled = false;
                _globalCooldownFill.color = Color.clear;
            }

            SetActiveIfNeeded(_globalCooldownLockRoot, showGlobalCooldown);

            if (_cooldownText != null)
            {
                _cooldownText.color = isOwnCooldown
                    ? CooldownAmber
                    : Color.white;
            }

            if (_globalCooldownLockRoot != null)
            {
                Text globalCooldownLabel = _globalCooldownLockRoot.GetComponent<Text>();
                if (globalCooldownLabel != null)
                {
                    globalCooldownLabel.color = GlobalCooldownCyan;
                }
            }

            if (_targetBadge != null)
            {
                string compactBlockedReason = _isInteractionEnabled
                    ? _blockedReasonCompact
                    : _interactionBlockedReasonCompact;
                _targetBadge.text = isOwnCooldown
                    ? "CD"
                    : isGlobalCooldown
                        ? "GCD"
                        : isProgressionLock
                            ? BuildProgressionBadge(activeBlockedReason)
                        : showReasonBadge
                            ? string.IsNullOrEmpty(compactBlockedReason)
                                ? "LOCKED"
                                : compactBlockedReason
                            : _targetBadgeText;
                _targetBadge.color = isOwnCooldown
                    ? new Color(1f, 0.72f, 0.24f, 0.96f)
                    : isGlobalCooldown
                        ? new Color(0.48f, 0.9f, 1f, 0.96f)
                        : showReasonBadge
                            ? readinessColor
                            : new Color(0.82f, 0.9f, 0.96f, 0.9f);
            }

            // Relation badges are useful while choosing an action, and setup
            // blockers must remain visible. Keeping FOE/SELF/CD/GCD stamped over
            // all resting icons makes the bar read like a debug overlay.
            // Cooldown sweeps and numbers already communicate the two timers.
            bool showStateBadge = _isPressed || showReasonBadge;
            if (_stateBadgeSurface != null)
            {
                Color badgeAccent = isOwnCooldown
                    ? new Color(1f, 0.72f, 0.24f, 1f)
                    : isGlobalCooldown
                        ? new Color(0.48f, 0.9f, 1f, 1f)
                        : showReasonBadge
                            ? readinessColor
                            : _abilityColor;
                _stateBadgeSurface.color = Color.Lerp(
                    new Color32(19, 15, 20, 247),
                    WithAlpha(badgeAccent, 0.97f),
                    showReasonBadge ? 0.34f : 0.14f);
                SetActiveIfNeeded(
                    _stateBadgeSurface.gameObject,
                    showStateBadge);
            }
            if (_targetBadge != null &&
                (_stateBadgeSurface == null ||
                 _targetBadge.transform.parent != _stateBadgeSurface.transform))
            {
                SetActiveIfNeeded(_targetBadge.gameObject, showStateBadge);
            }

            // Full readiness sentences obscure the spell silhouette when every
            // action shares the same blocker. The exact reason remains available
            // through BlockedReason and the central combat notice; the button uses
            // the compact state badge and rim instead.
            if (_blockedReasonLabel != null)
            {
                _blockedReasonLabel.text = string.Empty;
                SetActiveIfNeeded(_blockedReasonLabel.gameObject, false);
            }

            bool showBlockedMark = effectiveState == AbilityReadinessState.Controlled ||
                effectiveState == AbilityReadinessState.Dead ||
                effectiveState == AbilityReadinessState.Spectating ||
                (effectiveState == AbilityReadinessState.Unavailable &&
                 !isProgressionLock);
            SetActiveIfNeeded(_blockedMark, showBlockedMark);
            RefreshAlternateVisualState();
        }

        private void RefreshAlternateVisualState()
        {
            bool hasAlternate = HasAlternateUp;
            AbilityFlickSelection selection = _inputButton != null
                ? _inputButton.Selection
                : default;
            bool isPressed = selection.IsPressed && !selection.IsCanceled;
            SetActiveIfNeeded(_alternateUpRoot, hasAlternate);
            RefreshSelectedActionCaption(selection);
            RefreshRadialGuide(selection);
            if (!hasAlternate)
            {
                SetAlternatePaletteScale(false, false);
                if (_alternateUpCooldownFill != null)
                {
                    _alternateUpCooldownFill.enabled = false;
                    _alternateUpCooldownFill.fillAmount = 0f;
                }

                if (_alternateUpCooldownText != null)
                {
                    _alternateUpCooldownText.enabled = false;
                    _alternateUpCooldownText.text = string.Empty;
                }

                return;
            }

            EnsureAlternatePaletteExpanded();
            if (!isPressed)
            {
                if (_alternateUpLabel != null &&
                    !string.Equals(_alternateUpLabel.text, _alternateUpCaption))
                {
                    _alternateUpLabel.text = _alternateUpCaption;
                }

                SetAlternatePaletteScale(false, false);
                if (_alternateUpSurface != null)
                {
                    _alternateUpSurface.color = WithAlpha(
                        Color.Lerp(Obsidian, _alternateUpColor, 0.34f),
                        0.92f);
                }
                if (_alternateUpHighlight != null)
                {
                    _alternateUpHighlight.color = WithAlpha(_alternateUpColor, 0.52f);
                }
                AbilityReadinessState restingState = ResolveAlternateUpReadiness();
                bool restingTargetSelectionBlocker =
                    IsTargetSelectionReason(_alternateUpBlockedReason);
                Color restingArtColor =
                    restingState == AbilityReadinessState.Ready ||
                    restingTargetSelectionBlocker
                        ? Color.white
                        : Color.Lerp(
                            Color.white,
                            ResolveReadinessColor(restingState),
                            0.58f);
                restingArtColor.a = 0.9f;
                if (_alternateUpIcon != null && _alternateUpIcon.enabled)
                {
                    _alternateUpIcon.color = restingArtColor;
                }
                if (_alternateUpGlyph != null && _alternateUpGlyph.enabled)
                {
                    _alternateUpGlyph.color = restingArtColor;
                }
                if (_alternateUpLabel != null)
                {
                    SetActiveIfNeeded(_alternateUpLabel.gameObject, false);
                }
                if (_alternateUpCooldownFill != null)
                {
                    _alternateUpCooldownFill.enabled = false;
                }
                if (_alternateUpCooldownText != null)
                {
                    _alternateUpCooldownText.enabled = false;
                }

                return;
            }

            bool isSelected = isPressed &&
                selection.IsLocked &&
                selection.Direction == AbilityFlickDirection.Up &&
                selection.AbilityId == _alternateUpAbilityId;
            AbilityReadinessState effectiveState = ResolveAlternateUpReadiness();
            bool isReady = effectiveState == AbilityReadinessState.Ready;
            bool isGlobalCooldown = effectiveState == AbilityReadinessState.GlobalCooldown;
            bool isOwnCooldown = effectiveState == AbilityReadinessState.Cooldown;
            bool isTargetSelectionBlocker =
                IsTargetSelectionReason(_alternateUpBlockedReason);
            SetAlternatePaletteScale(true, isSelected);

            if (_alternateUpSurface != null)
            {
                _alternateUpSurface.color = isSelected
                    ? Color.Lerp(_alternateUpColor, BrightBrass, 0.48f)
                    : isPressed
                        ? Color.Lerp(
                            Obsidian,
                            _alternateUpColor,
                            0.48f)
                        : Color.Lerp(
                            Obsidian,
                            WithAlpha(_alternateUpColor, 0.98f),
                            0.18f);
            }

            if (_alternateUpHighlight != null)
            {
                _alternateUpHighlight.color = WithAlpha(
                    isSelected ? Color.white : _alternateUpColor,
                    isSelected ? 0.98f : isPressed ? 0.56f : 0.32f);
            }

            Color contentColor = isReady ||
                isGlobalCooldown ||
                isTargetSelectionBlocker
                ? Color.white
                : isOwnCooldown
                    ? new Color(0.48f, 0.5f, 0.54f, _unavailableAlpha)
                    : new Color(0.62f, 0.65f, 0.7f, _unavailableAlpha);
            if (_alternateUpIcon != null && _alternateUpIcon.enabled)
            {
                _alternateUpIcon.color = contentColor;
            }

            if (_alternateUpGlyph != null)
            {
                _alternateUpGlyph.color = contentColor;
            }

            if (_alternateUpLabel != null)
            {
                _alternateUpLabel.text = isSelected
                    ? _alternateUpSelectedCaption
                    : _alternateUpCaption;
                _alternateUpLabel.color = isSelected
                    ? new Color(1f, 0.96f, 0.68f, 1f)
                    : contentColor;
                SetActiveIfNeeded(_alternateUpLabel.gameObject, true);
            }

            float cooldownFillAmount = isOwnCooldown
                ? _alternateUpCooldownFillAmount
                : isGlobalCooldown
                    ? _alternateUpGlobalCooldownFillAmount
                    : 0f;
            if (_alternateUpCooldownFill != null)
            {
                _alternateUpCooldownFill.fillAmount = cooldownFillAmount;
                _alternateUpCooldownFill.enabled = cooldownFillAmount > 0.0001f;
            }

            int cooldownSecond = _alternateUpCooldownRemaining > 0f
                ? Mathf.CeilToInt(_alternateUpCooldownRemaining)
                : 0;
            if (_alternateUpCooldownText != null)
            {
                string stateText = cooldownSecond > 0
                    ? ResolveCooldownSecondLabel(cooldownSecond)
                    : isGlobalCooldown
                        ? "GCD"
                        : IsReasonState(effectiveState) &&
                          !IsTargetSelectionReason(_alternateUpBlockedReason)
                            ? _alternateUpBlockedCaption
                            : string.Empty;
                _alternateUpCooldownText.text = stateText;
                _alternateUpCooldownText.enabled = !string.IsNullOrEmpty(stateText);
            }

        }

        private void CacheAlternateCaptions()
        {
            string name = ResolveCompactAlternateName();
            _alternateUpCaption = ("↑ " + name).ToUpperInvariant();
            _alternateUpSelectedCaption = ("✓ " + name).ToUpperInvariant();
            string caption = string.IsNullOrWhiteSpace(_alternateUpName)
                ? ResolveActionKeyword(_alternateUpAbilityId)
                : _alternateUpName;
            _alternateUpActionCaption = (caption ?? string.Empty).ToUpperInvariant();
        }

        private string ResolveCompactAlternateName()
        {
            string name = string.IsNullOrWhiteSpace(_alternateUpName)
                ? ResolveActionKeyword(_alternateUpAbilityId)
                : _alternateUpName;
            if (string.IsNullOrWhiteSpace(name))
            {
                return "ALT";
            }

            const int MaximumLength = 7;
            string trimmed = name.Trim();
            int firstSpace = trimmed.IndexOf(' ');
            if (firstSpace > 0)
            {
                trimmed = trimmed.Substring(0, firstSpace);
            }

            return trimmed.Length <= MaximumLength
                ? trimmed
                : trimmed.Substring(0, MaximumLength);
        }

        private void RefreshAlternateCooldownText()
        {
            if (_alternateUpCooldownText == null)
            {
                return;
            }

            if (!IsDirectionalPaletteExpanded())
            {
                _alternateUpCooldownText.enabled = false;
                return;
            }

            int cooldownSecond = _alternateUpCooldownRemaining > 0f
                ? Mathf.CeilToInt(_alternateUpCooldownRemaining)
                : 0;
            AbilityReadinessState effectiveState = ResolveAlternateUpReadiness();
            string stateText = cooldownSecond > 0
                ? ResolveCooldownSecondLabel(cooldownSecond)
                : effectiveState == AbilityReadinessState.GlobalCooldown
                    ? "GCD"
                    : IsReasonState(effectiveState) &&
                      !IsTargetSelectionReason(_alternateUpBlockedReason)
                        ? _alternateUpBlockedCaption
                        : string.Empty;
            if (!string.Equals(_alternateUpCooldownText.text, stateText))
            {
                _alternateUpCooldownText.text = stateText;
            }

            _alternateUpCooldownText.enabled = !string.IsNullOrEmpty(stateText);
        }

        private void SetAlternatePaletteScale(bool isExpanded, bool isSelected)
        {
            if (_alternateUpRoot == null)
            {
                return;
            }

            SetDirectionMarkerPosition(
                _alternateUpRoot,
                AbilityFlickDirection.Up,
                isExpanded);
            float scale = !isExpanded
                ? RestingDirectionPipScale
                : isSelected
                    ? SelectedDirectionScale
                    : ExpandedDirectionScale;
            _alternateUpRoot.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private void RefreshSelectedActionCaption(AbilityFlickSelection selection)
        {
            if (_actionLabel == null)
            {
                return;
            }

            string caption = _primaryActionCaption;
            if (selection.IsPressed &&
                selection.IsLocked &&
                selection.HasDirectionalBinding &&
                !selection.IsCanceled)
            {
                switch (selection.Direction)
                {
                    case AbilityFlickDirection.Up:
                        caption = _alternateUpActionCaption;
                        break;
                    case AbilityFlickDirection.Down:
                        caption = _alternateDownState.ActionCaption;
                        break;
                    case AbilityFlickDirection.Left:
                        caption = _alternateLeftState.ActionCaption;
                        break;
                    case AbilityFlickDirection.Right:
                        caption = _alternateRightState.ActionCaption;
                        break;
                }
            }

            if (string.IsNullOrEmpty(caption))
            {
                caption = _primaryActionCaption;
            }

            if (!string.Equals(_actionLabel.text, caption))
            {
                _actionLabel.text = caption;
            }
        }

        private Color ResolveSelectedDirectionalColor()
        {
            if (!HasLockedDirectionalSelection() || _inputButton == null)
            {
                return _abilityColor;
            }

            AbilityId selectedAbility = _inputButton.Selection.AbilityId;
            return AbilityPresentationCatalog.TryGet(
                selectedAbility,
                out AbilityPresentation presentation)
                    ? presentation.Color
                    : _abilityColor;
        }

        private void EnsureAlternatePaletteExpanded()
        {
            if (_alternatePaletteExpanded || _alternateUpRoot == null)
            {
                return;
            }

            _alternatePaletteExpanded = true;
            RectTransform root = _alternateUpRoot.transform as RectTransform;
            if (root != null)
            {
                root.sizeDelta = new Vector2(
                    DirectionSpellDiameter,
                    DirectionSpellDiameter);
            }

            if (_alternateUpIcon != null)
            {
                ApplyAlternateContentLayout(_alternateUpIcon.rectTransform);
            }
            if (_alternateUpGlyph != null)
            {
                ApplyAlternateContentLayout(_alternateUpGlyph.rectTransform);
            }
        }

        private static void ApplyAlternateContentLayout(RectTransform content)
        {
            Vector2 anchor = new Vector2(0.5f, 0.5f);
            content.anchorMin = anchor;
            content.anchorMax = anchor;
            content.pivot = new Vector2(0.5f, 0.5f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(
                DirectionArtDiameter,
                DirectionArtDiameter);
        }

        private AbilityReadinessState ResolveAlternateUpReadiness()
        {
            if (!_isInteractionEnabled)
            {
                return AbilityReadinessState.Spectating;
            }

            if (_alternateUpCooldownRemaining > 0.0001f)
            {
                return AbilityReadinessState.Cooldown;
            }

            if (_alternateUpGlobalCooldownLocked &&
                _alternateUpGlobalCooldownFillAmount > 0.0001f)
            {
                return AbilityReadinessState.GlobalCooldown;
            }

            return _alternateUpReadinessState;
        }

        private static bool UsesGlobalCooldown(AbilityId abilityId)
        {
            return AbilityCatalog.TryGet(abilityId, out AbilityDefinition definition) &&
                definition.GlobalCooldownSeconds > 0.001f &&
                (definition.Requirements & AbilityRequirementFlags.IgnoresGlobalCooldown) == 0;
        }

        private static string BuildProgressionBadge(string blockedReason)
        {
            const string Prefix = "LEVEL ";
            return blockedReason != null &&
                   blockedReason.StartsWith(
                       Prefix,
                       System.StringComparison.OrdinalIgnoreCase)
                ? "UNLOCK " + blockedReason.Substring(Prefix.Length)
                : "UNLOCK";
        }

        private static string ResolveCompactBlockedReason(string blockedReason)
        {
            if (string.IsNullOrWhiteSpace(blockedReason))
            {
                return string.Empty;
            }

            string upper = blockedReason.ToUpperInvariant();
            switch (upper)
            {
                case "SELECT TARGET":
                case "NO TARGET":
                case "WRONG TARGET":
                    return "TGT";
                case "OUT OF RANGE":
                case "RANGE":
                    return "RANGE";
                case "LINE OF SIGHT":
                case "NO LINE OF SIGHT":
                case "LOS":
                    return "LOS";
                case "LOW ENERGY":
                    return "ENERGY";
                case "LOW MANA":
                    return "MANA";
                case "NEEDS STEALTH":
                    return "STEALTH";
                case "NEEDS COMBO":
                    return "COMBO";
                case "NEEDS FROZEN":
                    return "FROZEN";
                case "NO PET":
                case "NEEDS PET":
                    return "PET";
                case "SPECTATING":
                case "UNAVAILABLE":
                    return "LOCKED";
                case "CONTROLLED":
                    return "CC";
                case "CASTING":
                    return "CAST";
                case "MOVING":
                    return "MOVE";
                default:
                    const int MaximumLength = 7;
                    return upper.Length <= MaximumLength
                        ? upper
                        : upper.Substring(0, MaximumLength);
            }
        }

        private static bool IsTargetSelectionReason(string blockedReason)
        {
            return string.Equals(
                       blockedReason,
                       "SELECT TARGET",
                       System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    blockedReason,
                    "NO TARGET",
                    System.StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    blockedReason,
                    "WRONG TARGET",
                    System.StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateDirectionHintCooldown(
            ref DirectionHintState hintState,
            float remainingSeconds,
            bool isAvailable)
        {
            int cooldownSecond = remainingSeconds > 0.0001f
                ? Mathf.CeilToInt(remainingSeconds)
                : 0;
            AbilityReadinessState readiness = hintState.Readiness;
            if (cooldownSecond > 0)
            {
                readiness = AbilityReadinessState.Cooldown;
            }
            else if (readiness == AbilityReadinessState.Cooldown)
            {
                readiness = isAvailable
                    ? AbilityReadinessState.Ready
                    : AbilityReadinessState.Unavailable;
            }

            if (hintState.CooldownSecond == cooldownSecond &&
                hintState.IsAvailable == isAvailable &&
                hintState.Readiness == readiness)
            {
                return;
            }

            hintState.CooldownSecond = cooldownSecond;
            hintState.IsAvailable = isAvailable;
            hintState.Readiness = readiness;
            RefreshDirectionalHints();
        }

        private void UpdateDirectionHintReadiness(
            ref DirectionHintState hintState,
            AbilityReadinessState readiness,
            string blockedReason)
        {
            string reason = blockedReason ?? string.Empty;
            if (hintState.Readiness == readiness &&
                string.Equals(hintState.BlockedReason, reason))
            {
                return;
            }

            hintState.Readiness = readiness;
            hintState.BlockedReason = reason;
            hintState.BlockedCaption = BuildDirectionCaption(
                hintState.Arrow,
                ResolveCompactBlockedReason(reason));
            hintState.SelectedBlockedCaption = BuildDirectionCaption(
                "✓",
                ResolveCompactBlockedReason(reason));
            RefreshDirectionalHints();
        }

        private void RefreshDirectionalHints()
        {
            AbilityFlickSelection selection = _inputButton != null
                ? _inputButton.Selection
                : default;
            RefreshRadialGuide(selection);
            RefreshDirectionHint(
                _alternateDownHint,
                _alternateDownHintRoot,
                _alternateDownHintSurface,
                _alternateDownIcon,
                _alternateDownGlyph,
                AbilityFlickDirection.Down,
                _alternateDownAbilityId,
                _alternateDownState);
            RefreshDirectionHint(
                _alternateLeftHint,
                _alternateLeftHintRoot,
                _alternateLeftHintSurface,
                _alternateLeftIcon,
                _alternateLeftGlyph,
                AbilityFlickDirection.Left,
                _alternateLeftAbilityId,
                _alternateLeftState);
            RefreshDirectionHint(
                _alternateRightHint,
                _alternateRightHintRoot,
                _alternateRightHintSurface,
                _alternateRightIcon,
                _alternateRightGlyph,
                AbilityFlickDirection.Right,
                _alternateRightAbilityId,
                _alternateRightState);
        }

        private void RefreshRadialGuide(AbilityFlickSelection selection)
        {
            if (_directionalGuide == null)
            {
                return;
            }

            AbilityFlickDirection selectedDirection = selection.IsPressed &&
                selection.IsLocked &&
                selection.HasDirectionalBinding &&
                !selection.IsCanceled
                    ? selection.Direction
                    : AbilityFlickDirection.None;
            _directionalGuide.SetGuideState(
                selection.IsPressed && !selection.IsCanceled,
                _alternateUpAbilityId != AbilityId.None,
                _alternateDownAbilityId != AbilityId.None,
                _alternateLeftAbilityId != AbilityId.None,
                _alternateRightAbilityId != AbilityId.None,
                selectedDirection);
        }

        private void RefreshDirectionHint(
            Text label,
            GameObject root,
            Graphic surface,
            Image icon,
            AbilityGlyphGraphic glyph,
            AbilityFlickDirection direction,
            AbilityId abilityId,
            DirectionHintState hintState)
        {
            if (label == null)
            {
                return;
            }

            bool hasBinding = AbilityCatalog.TryGet(abilityId, out _);
            AbilityFlickSelection selection = _inputButton != null
                ? _inputButton.Selection
                : default;
            bool showPalette = hasBinding &&
                selection.IsPressed &&
                !selection.IsCanceled;
            SetActiveIfNeeded(root, hasBinding);
            if (!hasBinding)
            {
                SetActiveIfNeeded(label.gameObject, false);
                if (!string.IsNullOrEmpty(label.text))
                {
                    label.text = string.Empty;
                }

                return;
            }

            AbilityReadinessState readiness = ResolveDirectionHintReadiness(
                abilityId,
                hintState);
            bool isTargetSelectionBlocker =
                IsTargetSelectionReason(hintState.BlockedReason);
            bool selected = selection.IsPressed &&
                selection.IsLocked &&
                !selection.IsCanceled &&
                selection.Direction == direction &&
                selection.AbilityId == abilityId;
            SetDirectionMarkerScale(root, direction, showPalette, selected);
            // Direction captions are authored below their wedge icons. Their
            // RectTransforms are independent of the icon roots, so preserve
            // those offsets across rest, hold, and locked-selection refreshes.
            SetActiveIfNeeded(label.gameObject, showPalette);

            Color abilityColor = AbilityPresentationCatalog.TryGet(
                abilityId,
                out AbilityPresentation presentation)
                    ? presentation.Color
                    : Color.white;
            Color artColor = selected
                ? Color.white
                : readiness == AbilityReadinessState.Ready ||
                  isTargetSelectionBlocker
                    ? Color.white
                    : Color.Lerp(
                        Color.white,
                        ResolveReadinessColor(readiness),
                        0.58f);
            artColor.a = showPalette ? 1f : 0.9f;
            if (icon != null && icon.enabled)
            {
                icon.color = artColor;
            }
            if (glyph != null && glyph.enabled)
            {
                glyph.color = artColor;
            }
            if (!showPalette)
            {
                if (surface != null)
                {
                    Color pipColor = readiness == AbilityReadinessState.Ready
                        ? abilityColor
                        : isTargetSelectionBlocker
                            ? abilityColor
                        : readiness == AbilityReadinessState.GlobalCooldown
                            ? GlobalCooldownCyan
                            : ResolveReadinessColor(readiness);
                    surface.color = WithAlpha(
                        Color.Lerp(Obsidian, pipColor, 0.34f),
                        0.92f);
                }

                return;
            }

            string caption;
            if (hintState.CooldownSecond > 0)
            {
                caption = ResolveDirectionCooldownLabel(
                    direction,
                    selected,
                    hintState.CooldownSecond);
            }
            else if (readiness == AbilityReadinessState.GlobalCooldown)
            {
                caption = ResolveDirectionGcdLabel(direction, selected);
            }
            else if (IsReasonState(readiness) &&
                     !IsTargetSelectionReason(hintState.BlockedReason))
            {
                caption = selected
                    ? hintState.SelectedBlockedCaption
                    : hintState.BlockedCaption;
            }
            else
            {
                caption = selected
                    ? hintState.SelectedReadyCaption
                    : hintState.ReadyCaption;
            }

            if (!string.Equals(label.text, caption))
            {
                label.text = caption;
            }
            Color expandedColor = selected
                ? new Color(1f, 0.96f, 0.68f, 1f)
                : readiness == AbilityReadinessState.Ready || isTargetSelectionBlocker
                    ? Color.Lerp(abilityColor, Color.white, 0.5f)
                    : readiness == AbilityReadinessState.GlobalCooldown
                        ? new Color(0.48f, 0.9f, 1f, 0.96f)
                        : ResolveReadinessColor(readiness);
            label.color = expandedColor;
            if (surface != null)
            {
                surface.color = selected
                    ? Color.Lerp(abilityColor, BrightBrass, 0.38f)
                    : readiness == AbilityReadinessState.Ready ||
                      isTargetSelectionBlocker
                        ? Color.Lerp(
                            Obsidian,
                            WithAlpha(abilityColor, 0.98f),
                            0.18f)
                        : readiness == AbilityReadinessState.GlobalCooldown
                            ? new Color(0.035f, 0.12f, 0.17f, 0.98f)
                            : Color.Lerp(
                                Obsidian,
                                WithAlpha(ResolveReadinessColor(readiness), 0.98f),
                                0.2f);
            }
        }

        private void SetDirectionMarkerScale(
            GameObject root,
            AbilityFlickDirection direction,
            bool isExpanded,
            bool isSelected)
        {
            if (root == null)
            {
                return;
            }

            SetDirectionMarkerPosition(root, direction, isExpanded);
            float scale = !isExpanded
                ? RestingDirectionPipScale
                : isSelected
                    ? SelectedDirectionScale
                    : ExpandedDirectionScale;
            root.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private static void SetDirectionMarkerPosition(
            GameObject root,
            AbilityFlickDirection direction,
            bool isExpanded)
        {
            if (root == null || !(root.transform is RectTransform rect))
            {
                return;
            }

            float distance = isExpanded
                ? ExpandedDirectionOffset
                : RestingDirectionOffset;
            switch (direction)
            {
                case AbilityFlickDirection.Up:
                    rect.anchoredPosition = new Vector2(0f, distance);
                    break;
                case AbilityFlickDirection.Down:
                    rect.anchoredPosition = new Vector2(0f, -distance);
                    break;
                case AbilityFlickDirection.Left:
                    rect.anchoredPosition = new Vector2(-distance, 0f);
                    break;
                case AbilityFlickDirection.Right:
                    rect.anchoredPosition = new Vector2(distance, 0f);
                    break;
            }
        }

        private static void BindDirectionalArt(
            Image iconView,
            AbilityGlyphGraphic glyphView,
            AbilityId abilityId,
            Sprite icon)
        {
            bool hasAbility = abilityId != AbilityId.None;
            bool hasPaintedIcon = hasAbility && icon != null;
            Color abilityColor = AbilityPresentationCatalog.TryGet(
                abilityId,
                out AbilityPresentation presentation)
                    ? presentation.Color
                    : Color.white;

            if (iconView != null)
            {
                iconView.sprite = icon;
                iconView.enabled = hasPaintedIcon;
                iconView.color = hasPaintedIcon ? Color.white : Color.clear;
            }

            if (glyphView != null)
            {
                glyphView.Bind(abilityId, abilityColor);
                glyphView.enabled = hasAbility && !hasPaintedIcon;
                glyphView.color = Color.white;
            }
        }

        private AbilityReadinessState ResolveDirectionHintReadiness(
            AbilityId abilityId,
            DirectionHintState hintState)
        {
            if (!_isInteractionEnabled)
            {
                return AbilityReadinessState.Spectating;
            }

            if (hintState.CooldownSecond > 0)
            {
                return AbilityReadinessState.Cooldown;
            }

            if (_directionalGlobalCooldownLocked && UsesGlobalCooldown(abilityId))
            {
                return AbilityReadinessState.GlobalCooldown;
            }

            return hintState.Readiness;
        }

        private static string ResolveCompactAbilityName(AbilityDefinition definition)
        {
            string name = definition.DisplayName.ToUpperInvariant();
            int separator = name.IndexOf(' ');
            if (separator > 0)
            {
                name = name.Substring(0, separator);
            }

            const int MaximumLength = 7;
            return name.Length <= MaximumLength
                ? name
                : name.Substring(0, MaximumLength);
        }

        private static string[] CreateCooldownLabels(string prefix)
        {
            string[] values = new string[MaximumCachedCooldownSecond + 1];
            values[0] = string.Empty;
            for (int second = 1; second < values.Length; second++)
            {
                values[second] = prefix + second.ToString();
            }

            return values;
        }

        private static string ResolveCooldownSecondLabel(int second)
        {
            int clamped = Mathf.Clamp(second, 0, MaximumCachedCooldownSecond);
            return CooldownSecondLabels[clamped];
        }

        private static string ResolveDirectionCooldownLabel(
            AbilityFlickDirection direction,
            bool selected,
            int second)
        {
            int clamped = Mathf.Clamp(second, 0, MaximumCachedCooldownSecond);
            if (selected)
            {
                return SelectedCooldownLabels[clamped];
            }

            switch (direction)
            {
                case AbilityFlickDirection.Down:
                    return DownCooldownLabels[clamped];
                case AbilityFlickDirection.Left:
                    return LeftCooldownLabels[clamped];
                case AbilityFlickDirection.Right:
                    return RightCooldownLabels[clamped];
                default:
                    return CooldownSecondLabels[clamped];
            }
        }

        private static string ResolveDirectionGcdLabel(
            AbilityFlickDirection direction,
            bool selected)
        {
            if (selected)
            {
                return "✓ GCD";
            }

            switch (direction)
            {
                case AbilityFlickDirection.Down:
                    return "↓ GCD";
                case AbilityFlickDirection.Left:
                    return "← GCD";
                case AbilityFlickDirection.Right:
                    return "→ GCD";
                default:
                    return "GCD";
            }
        }

        private static string BuildDirectionCaption(string prefix, string value)
        {
            string safeValue = string.IsNullOrEmpty(value) ? "BLOCKED" : value;
            return prefix + " " + safeValue;
        }

        private AbilityReadinessState ResolveEffectiveReadiness()
        {
            if (!_isInteractionEnabled)
            {
                return AbilityReadinessState.Spectating;
            }

            if (_cooldownRemaining > 0.0001f)
            {
                return AbilityReadinessState.Cooldown;
            }

            if (_isGlobalCooldownLocked && _globalCooldownFillAmount > 0.0001f)
            {
                return AbilityReadinessState.GlobalCooldown;
            }

            return _readinessState;
        }

        private static bool IsReasonState(AbilityReadinessState state)
        {
            return state != AbilityReadinessState.Ready &&
                state != AbilityReadinessState.Cooldown &&
                state != AbilityReadinessState.GlobalCooldown;
        }

        private static Color ResolveReadinessColor(AbilityReadinessState state)
        {
            switch (state)
            {
                case AbilityReadinessState.MissingSetup:
                    return new Color(0.56f, 0.88f, 1f, 1f);
                case AbilityReadinessState.InsufficientResource:
                    return new Color(1f, 0.72f, 0.24f, 1f);
                case AbilityReadinessState.Moving:
                case AbilityReadinessState.Casting:
                    return new Color(1f, 0.9f, 0.48f, 1f);
                case AbilityReadinessState.Spectating:
                    return new Color(0.56f, 0.72f, 0.86f, 1f);
                default:
                    return new Color(1f, 0.4f, 0.32f, 1f);
            }
        }

        private void ApplyReadyPulse(float strength)
        {
            if (_readyPulse != null)
            {
                float scale = Mathf.Lerp(1f, _readyPulseScale, strength);
                _readyPulse.localScale = new Vector3(scale, scale, 1f);
            }

            if (_readyPulseGraphic != null)
            {
                Color color = _readyPulseGraphic.color;
                color.a = strength * 0.9f;
                _readyPulseGraphic.color = color;
            }
        }

        private static Color WithAlpha(Color source, float alpha)
        {
            source.a = alpha;
            return source;
        }

        private static string ResolveTargetBadge(AbilityTargetRule targeting)
        {
            if (targeting.Shape == AbilityTargetShape.Point ||
                targeting.Shape == AbilityTargetShape.Direction ||
                targeting.Shape == AbilityTargetShape.CasterArea ||
                targeting.Shape == AbilityTargetShape.TargetArea)
            {
                return "AREA";
            }

            bool self = targeting.Allows(AbilityTargetRelation.Self);
            bool friendly = targeting.Allows(AbilityTargetRelation.Friendly);
            bool hostile = targeting.Allows(AbilityTargetRelation.Hostile);
            if (hostile && (friendly || self))
            {
                return "FLEX";
            }

            if (hostile)
            {
                return "FOE";
            }

            if (friendly)
            {
                return "ALLY";
            }

            return self ? "SELF" : string.Empty;
        }

        private void ConfigureCooldownFill()
        {
            ConfigureRadialFill(_cooldownFill);
            ConfigureRadialFill(_globalCooldownFill);
            ConfigureRadialFill(_alternateUpCooldownFill);
        }

        private static void ConfigureRadialFill(Image fill)
        {
            if (fill == null)
            {
                return;
            }

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Radial360;
            fill.fillOrigin = (int)Image.Origin360.Top;
            fill.fillClockwise = false;
            fill.raycastTarget = false;
        }

        private static void SetActiveIfNeeded(GameObject target, bool isActive)
        {
            if (target != null && target.activeSelf != isActive)
            {
                target.SetActive(isActive);
            }
        }

        private static string ResolveActionKeyword(AbilityId abilityId)
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
                    return "DIVE";
                case AbilityId.Kick:
                case AbilityId.Counterspell:
                    return "STOP";
                case AbilityId.Shadowstep:
                case AbilityId.Blink:
                    return "BLINK";
                case AbilityId.Anchor:
                    return "BRACE";
                case AbilityId.ArcaneBolt:
                    return "BOLT";
                case AbilityId.Gust:
                    return "PUSH";
                case AbilityId.FrostNova:
                    return "FREEZE";
                case AbilityId.Penance:
                    return "CHANNEL";
                case AbilityId.FlashHeal:
                    return "HEAL";
                case AbilityId.Renew:
                    return "HOT";
                case AbilityId.PowerWordShield:
                case AbilityId.IceBarrier:
                    return "SHIELD";
                case AbilityId.PainSuppression:
                    return "SAVE";
                case AbilityId.PsychicScream:
                    return "FEAR";
                case AbilityId.ShadowWordDeath:
                    return "EXECUTE";
                case AbilityId.Shadowfiend:
                    return "SUMMON";
                case AbilityId.DispelMagic:
                    return "DISPEL";
                case AbilityId.Hemorrhage:
                case AbilityId.Mutilate:
                    return "BUILD";
                case AbilityId.Eviscerate:
                case AbilityId.Envenom:
                    return "FINISH";
                case AbilityId.KidneyShot:
                case AbilityId.DeepFreeze:
                case AbilityId.CheapShot:
                    return "STUN";
                case AbilityId.ShadowDance:
                    return "BURST";
                case AbilityId.CloakOfShadows:
                    return "IMMUNE";
                case AbilityId.Shiv:
                    return "POISON";
                case AbilityId.Blind:
                case AbilityId.DragonBreath:
                case AbilityId.Polymorph:
                    return "CONTROL";
                case AbilityId.Vanish:
                    return "VANISH";
                case AbilityId.Fireball:
                case AbilityId.Frostbolt:
                    return "CAST";
                case AbilityId.FireBlast:
                case AbilityId.IceLance:
                    return "BURST";
                case AbilityId.LivingBomb:
                    return "DOT";
                case AbilityId.BlastWave:
                    return "PUSH";
                case AbilityId.BossCleave:
                    return "CLEAVE";
                case AbilityId.BossMeteor:
                    return "METEOR";
                case AbilityId.BossKnockback:
                    return "ROAR";
                case AbilityId.ArenaTrinket:
                    return "BREAK";
                case AbilityId.PowerInfusion:
                case AbilityId.IcyVeins:
                    return "HASTE";
                case AbilityId.Combustion:
                case AbilityId.ColdBlood:
                    return "POWER";
                case AbilityId.Evasion:
                    return "EVADE";
                case AbilityId.VampiricTouch:
                case AbilityId.ShadowWordPain:
                    return "AFFLICT";
                case AbilityId.DevouringPlague:
                    return "DRAIN";
                case AbilityId.MindFlay:
                    return "CHANNEL";
                case AbilityId.MindBlast:
                    return "BURST";
                case AbilityId.Silence:
                case AbilityId.SilencingShot:
                    return "SILENCE";
                case AbilityId.Dispersion:
                    return "SURVIVE";
                case AbilityId.PsychicHorror:
                    return "HORROR";
                case AbilityId.ManaBurn:
                    return "BURN MANA";
                case AbilityId.MassDispel:
                    return "MASS DISPEL";
                case AbilityId.Fade:
                    return "ESCAPE";
                case AbilityId.ChimeraShot:
                    return "CHIMERA";
                case AbilityId.SerpentSting:
                    return "VENOM";
                case AbilityId.ViperSting:
                    return "DRAIN MANA";
                case AbilityId.ScorpidSting:
                    return "DISARM SETUP";
                case AbilityId.AimedShot:
                    return "MORTAL";
                case AbilityId.ArcaneShot:
                    return "BURST";
                case AbilityId.KillShot:
                    return "EXECUTE";
                case AbilityId.SteadyShot:
                    return "STEADY";
                case AbilityId.HuntersMark:
                    return "MARK";
                case AbilityId.ScatterShot:
                    return "SCATTER";
                case AbilityId.FreezingArrow:
                    return "FREEZING";
                case AbilityId.FrostTrap:
                    return "FROST TRAP";
                case AbilityId.FeignDeath:
                    return "DROP TARGET";
                case AbilityId.Disengage:
                    return "LEAP";
                case AbilityId.WingClip:
                    return "SLOW";
                case AbilityId.MastersCall:
                    return "FREEDOM";
                case AbilityId.Deterrence:
                    return "DEFLECT";
                case AbilityId.RoarOfSacrifice:
                    return "PROTECT";
                case AbilityId.Readiness:
                    return "RESET";
                case AbilityId.RapidFire:
                    return "HASTE";
                case AbilityId.PetAttack:
                    return "PET ATTACK";
                case AbilityId.PetFollow:
                    return "PET FOLLOW";
                case AbilityId.PetStay:
                    return "PET STAY";
                case AbilityId.SpiderWeb:
                    return "WEB";
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
                case AbilityId.Gouge:
                    return "CONTROL";
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
                case AbilityId.DawnNova:
                case AbilityId.UmbralCollapse:
                case AbilityId.FlameSurge:
                case AbilityId.Blizzard:
                case AbilityId.ArrowRain:
                    return "AREA";
                default:
                    return string.Empty;
            }
        }

        private struct DirectionHintState
        {
            public string Arrow;
            public int CooldownSecond;
            public bool IsAvailable;
            public AbilityReadinessState Readiness;
            public string BlockedReason;
            public string ActionCaption;
            public string ReadyCaption;
            public string SelectedReadyCaption;
            public string BlockedCaption;
            public string SelectedBlockedCaption;

            public static DirectionHintState Create(AbilityId abilityId, string arrow)
            {
                string name = AbilityCatalog.TryGet(
                    abilityId,
                    out AbilityDefinition definition)
                        ? ResolveCompactAbilityName(definition)
                        : string.Empty;
                return new DirectionHintState
                {
                    Arrow = arrow,
                    CooldownSecond = 0,
                    IsAvailable = abilityId != AbilityId.None,
                    Readiness = abilityId == AbilityId.None
                        ? AbilityReadinessState.Unavailable
                        : AbilityReadinessState.Ready,
                    BlockedReason = string.Empty,
                    ActionCaption = abilityId == AbilityId.None
                        ? string.Empty
                        : BuildDirectionCaption(arrow, name),
                    ReadyCaption = abilityId == AbilityId.None
                        ? string.Empty
                        : BuildDirectionCaption(arrow, name),
                    SelectedReadyCaption = abilityId == AbilityId.None
                        ? string.Empty
                        : BuildDirectionCaption("✓", name),
                    BlockedCaption = BuildDirectionCaption(arrow, "BLOCKED"),
                    SelectedBlockedCaption = BuildDirectionCaption("✓", "BLOCKED"),
                };
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _readyPulseDuration = Mathf.Max(0.01f, _readyPulseDuration);
            _readyPulseScale = Mathf.Max(1f, _readyPulseScale);
            _interactionSharpness = Mathf.Max(0f, _interactionSharpness);

            if (_inputButton != null)
            {
                _inputButton.BindAbility(_abilityId);
                _inputButton.ClearDirectionalAbilities();
                _inputButton.BindDirectionalAbility(
                    AbilityFlickDirection.Up,
                    _alternateUpAbilityId);
                _inputButton.BindDirectionalAbility(
                    AbilityFlickDirection.Down,
                    _alternateDownAbilityId);
                _inputButton.BindDirectionalAbility(
                    AbilityFlickDirection.Left,
                    _alternateLeftAbilityId);
                _inputButton.BindDirectionalAbility(
                    AbilityFlickDirection.Right,
                    _alternateRightAbilityId);
            }

            ConfigureCooldownFill();
            RefreshVisualState();
            RefreshDirectionalHints();
        }
#endif
    }
}
