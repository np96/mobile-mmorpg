using System;
using SessionRpg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Unity.UI
{
    [DisallowMultipleComponent]
    public sealed class CombatantStatusView : MonoBehaviour
    {
        private static readonly string[] HealthPercentageLabels =
            CreateHealthPercentageLabels();

        private static readonly Color HighContrastFriendlyColor =
            new Color(0f, 0.84f, 1f, 1f);
        private static readonly Color HighContrastHostileColor =
            new Color(1f, 0.42f, 0f, 1f);
        private static readonly Color HighContrastHumanColor =
            new Color(1f, 0.9f, 0.28f, 1f);
        private static readonly Color HighContrastSurfaceColor =
            new Color(0.015f, 0.025f, 0.04f, 0.99f);
        private static readonly Color HighContrastSelectedSurfaceColor =
            new Color(0.06f, 0.23f, 0.34f, 1f);
        private static readonly Color HighContrastHealthyColor =
            new Color(0.16f, 1f, 0.62f, 1f);
        private static readonly Color HighContrastWoundedColor =
            new Color(1f, 0.86f, 0.08f, 1f);
        private static readonly Color HighContrastCriticalColor =
            new Color(1f, 0.2f, 0.18f, 1f);
        private static readonly Color HighContrastHiddenColor =
            new Color(0.82f, 0.87f, 0.94f, 1f);
        private static readonly Color HighContrastHiddenSurfaceColor =
            new Color(0.025f, 0.03f, 0.04f, 1f);
        private static readonly Color HighContrastStatusColor = Color.white;
        private static readonly Color HighContrastCastColor =
            new Color(1f, 0.86f, 0.08f, 1f);
        private static readonly Color HighContrastBarrierColor =
            new Color(0.08f, 0.82f, 1f, 1f);

        [Header("Interaction")]
        [SerializeField] private Button _button;
        [SerializeField] private Graphic _surface;
        [SerializeField] private Graphic _relationAccent;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Identity")]
        [SerializeField] private Text _participantLabel;
        [SerializeField] private Text _relationLabel;
        [SerializeField] private Text _identityLabel;
        [SerializeField] private Graphic _classBadge;

        [Header("Vitals")]
        [SerializeField] private Image _healthFill;
        [SerializeField] private Image _barrierFill;
        [SerializeField] private Text _healthLabel;
        [SerializeField] private Text _barrierLabel;

        [Header("Cast and status")]
        [SerializeField] private GameObject _castRoot;
        [SerializeField] private Image _castFill;
        [SerializeField] private Text _castLabel;
        [SerializeField] private Text _statusLabel;

        [Header("State")]
        [SerializeField] private GameObject _selectedMarker;
        [SerializeField] private GameObject _deadOverlay;
        [SerializeField] private Color _friendlyColor = new Color(0.22f, 0.78f, 0.9f, 1f);
        [SerializeField] private Color _hostileColor = new Color(0.94f, 0.3f, 0.25f, 1f);
        [SerializeField] private Color _humanColor = new Color(1f, 0.75f, 0.24f, 1f);
        [SerializeField] private Color _surfaceColor = new Color(0.07f, 0.11f, 0.17f, 0.96f);
        [SerializeField] private Color _selectedSurfaceColor = new Color(0.16f, 0.23f, 0.31f, 0.98f);
        [SerializeField] private Color _healthyColor = new Color(0.29f, 0.8f, 0.59f, 1f);
        [SerializeField] private Color _woundedColor = new Color(0.95f, 0.62f, 0.2f, 1f);
        [SerializeField] private Color _criticalColor = new Color(0.9f, 0.25f, 0.22f, 1f);
        [SerializeField] private Color _healerBadgeColor = new Color(0.29f, 0.8f, 0.59f, 1f);
        [SerializeField] private Color _rogueBadgeColor = new Color(0.93f, 0.72f, 0.28f, 1f);
        [SerializeField] private Color _mageBadgeColor = new Color(0.4f, 0.67f, 0.96f, 1f);
        [SerializeField] private Color _hunterBadgeColor = new Color(0.54f, 0.75f, 0.32f, 1f);
        [SerializeField] private Color _bossBadgeColor = new Color(0.82f, 0.28f, 0.24f, 1f);
        [SerializeField, Range(0f, 1f)] private float _deadAlpha = 0.48f;
        [SerializeField, Range(0f, 1f)] private float _hiddenAlpha = 0.62f;
        [SerializeField] private Color _hiddenColor = new Color(0.43f, 0.48f, 0.55f, 1f);
        [SerializeField] private Color _hiddenSurfaceColor = new Color(0.055f, 0.065f, 0.08f, 0.98f);

        private int _participantId;
        private bool _isBound;
        private bool _isFriendly;
        private bool _isHuman;
        private bool _isAlive = true;
        private bool _isSelected;
        private bool _isHidden;
        private bool _highContrastEnabled;
        private int _lastHealthPercent = int.MinValue;
        private string _normalRelation = string.Empty;
        private Color _normalRelationColor;
        private Color? _defaultStatusColor;
        private Color? _defaultCastFillColor;
        private Color? _defaultCastLabelColor;
        private Color? _defaultBarrierColor;

        public event Action<int> Selected;

        public int ParticipantId => _participantId;

        public bool IsBound => _isBound;

        public bool IsFriendly => _isFriendly;

        public bool IsAlive => _isAlive;

        public bool IsSelected => _isSelected;

        public bool IsHidden => _isHidden;

        public bool HighContrastEnabled => _highContrastEnabled;

        public string DisplayName => _participantLabel != null
            ? _participantLabel.text
            : string.Empty;

        public RectTransform RectTransform => transform as RectTransform;

        private void OnEnable()
        {
            CaptureDefaultGraphicColors();
            _button?.onClick.AddListener(OnClicked);
        }

        private void OnDisable()
        {
            _button?.onClick.RemoveListener(OnClicked);
        }

        public void Bind(
            int participantId,
            ClassId classId,
            string displayName,
            string relation,
            bool isFriendly,
            bool isHuman)
        {
            CaptureDefaultGraphicColors();
            _participantId = participantId;
            _isBound = participantId > 0;
            _isFriendly = isFriendly;
            _isHuman = isHuman;
            _isAlive = true;
            _isSelected = false;
            _isHidden = false;
            _lastHealthPercent = int.MinValue;

            SetTextIfChanged(
                _participantLabel,
                participantId > 0 && !string.IsNullOrWhiteSpace(displayName)
                    ? displayName.ToUpperInvariant()
                    : string.Empty);
            _normalRelation = ResolveRelation(relation, isFriendly, isHuman);
            SetTextIfChanged(_relationLabel, _normalRelation);
            SetTextIfChanged(_identityLabel, DescribeClassBadge(classId));

            if (_classBadge != null)
            {
                _classBadge.color = ResolveClassColor(classId);
            }

            Color relationColor = ResolveRelationColor(isFriendly, isHuman);
            _normalRelationColor = relationColor;
            if (_relationAccent != null)
            {
                _relationAccent.color = relationColor;
            }

            if (_relationLabel != null)
            {
                _relationLabel.color = relationColor;
            }

            if (_button != null)
            {
                _button.interactable = _isBound;
            }

            SetSelected(false);
            SetVisible(_isBound);
            RestoreCanvasInteraction();
        }

        public void Present(
            float currentHealth,
            float maximumHealth,
            float barrier,
            string castName,
            float castProgress,
            string status,
            bool isAlive,
            bool isSelected)
        {
            CaptureDefaultGraphicColors();
            SetHidden(false);
            float safeMaximum = Mathf.Max(0f, maximumHealth);
            float safeHealth = Mathf.Clamp(currentHealth, 0f, safeMaximum);
            float safeBarrier = Mathf.Max(0f, barrier);
            float healthRatio = safeMaximum > 0.001f ? safeHealth / safeMaximum : 0f;
            float barrierRatio = safeMaximum > 0.001f
                ? Mathf.Clamp01(safeBarrier / safeMaximum)
                : 0f;

            SetFill(_barrierFill, barrierRatio);
            SetFill(_healthFill, healthRatio);
            int healthPercent = Mathf.Clamp(
                Mathf.RoundToInt(healthRatio * 100f),
                0,
                100);
            if (healthPercent != _lastHealthPercent)
            {
                SetTextIfChanged(
                    _healthLabel,
                    HealthPercentageLabels[healthPercent]);
                _lastHealthPercent = healthPercent;
            }

            SetTextIfChanged(_barrierLabel, string.Empty);

            if (_healthFill != null)
            {
                _healthFill.color = ResolveHealthColor(healthRatio);
            }

            if (_barrierFill != null)
            {
                SetActiveIfNeeded(_barrierFill.gameObject, isAlive && safeBarrier >= 0.5f);
            }

            bool isCasting = isAlive && !string.IsNullOrWhiteSpace(castName);
            SetActiveIfNeeded(_castRoot, isCasting);
            if (isCasting)
            {
                SetTextIfChanged(_castLabel, castName);
                SetFill(_castFill, Mathf.Clamp01(castProgress));
            }

            SetTextIfChanged(_statusLabel, isAlive ? status : string.Empty);
            ApplyStatusAndCastColors();
            SetActiveIfNeeded(_deadOverlay, !isAlive);
            _isAlive = isAlive;
            SetSelected(isSelected);

            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = isAlive ? 1f : _deadAlpha;
                _canvasGroup.interactable = _isBound && isAlive;
                _canvasGroup.blocksRaycasts = _isBound && isAlive;
            }

            if (_button != null)
            {
                _button.interactable = _isBound && isAlive;
            }
        }

        public void SetHidden(bool isHidden)
        {
            bool resolvedHidden = _isBound && _isAlive && isHidden;
            if (_isHidden == resolvedHidden)
            {
                return;
            }

            _isHidden = resolvedHidden;
            if (resolvedHidden)
            {
                SetVisible(true);
                _isSelected = false;
                SetActiveIfNeeded(_selectedMarker, false);
                SetActiveIfNeeded(_deadOverlay, false);
                SetActiveIfNeeded(_barrierFill != null ? _barrierFill.gameObject : null, false);
                SetActiveIfNeeded(_castRoot, false);
                SetFill(_healthFill, 1f);
                SetFill(_barrierFill, 0f);
                SetFill(_castFill, 0f);
                _lastHealthPercent = int.MinValue;
                SetTextIfChanged(_healthLabel, "?");
                SetTextIfChanged(_barrierLabel, string.Empty);
                SetTextIfChanged(_castLabel, string.Empty);
                SetTextIfChanged(_statusLabel, string.Empty);
                SetTextIfChanged(_relationLabel, "HIDDEN");
                if (_healthFill != null)
                {
                    _healthFill.color = ResolveHiddenColor();
                }

                if (_relationAccent != null)
                {
                    _relationAccent.color = ResolveHiddenColor();
                }

                if (_relationLabel != null)
                {
                    _relationLabel.color = ResolveHiddenColor();
                }

                if (_surface != null)
                {
                    _surface.color = ResolveHiddenSurfaceColor();
                }

                if (_canvasGroup != null)
                {
                    _canvasGroup.alpha = _hiddenAlpha;
                    _canvasGroup.interactable = false;
                    _canvasGroup.blocksRaycasts = false;
                }

                if (_button != null)
                {
                    _button.interactable = false;
                }

                return;
            }

            SetTextIfChanged(_relationLabel, _normalRelation);
            if (_relationAccent != null)
            {
                _relationAccent.color = _normalRelationColor;
            }

            if (_relationLabel != null)
            {
                _relationLabel.color = _normalRelationColor;
            }

            if (_surface != null)
            {
                _surface.color = ResolveSurfaceColor(_isSelected);
            }

            RestoreCanvasInteraction();
        }

        public void SetSelected(bool isSelected)
        {
            _isSelected = _isBound && _isAlive && !_isHidden && isSelected;
            SetActiveIfNeeded(_selectedMarker, _isSelected);
            if (_surface != null)
            {
                _surface.color = _isHidden
                    ? ResolveHiddenSurfaceColor()
                    : ResolveSurfaceColor(_isSelected);
            }
        }

        /// <summary>
        /// Applies a higher-luminance, blue/orange relation palette while
        /// retaining the existing text labels as the non-color signal. Existing
        /// card state is repainted immediately; no new combat snapshot is needed.
        /// </summary>
        public void SetHighContrast(bool enabled)
        {
            CaptureDefaultGraphicColors();
            _highContrastEnabled = enabled;

            if (_isBound)
            {
                _normalRelationColor = ResolveRelationColor(_isFriendly, _isHuman);
            }

            if (_isHidden)
            {
                if (_healthFill != null)
                {
                    _healthFill.color = ResolveHiddenColor();
                }

                if (_relationAccent != null)
                {
                    _relationAccent.color = ResolveHiddenColor();
                }

                if (_relationLabel != null)
                {
                    _relationLabel.color = ResolveHiddenColor();
                }

                if (_surface != null)
                {
                    _surface.color = ResolveHiddenSurfaceColor();
                }
            }
            else
            {
                if (_relationAccent != null)
                {
                    _relationAccent.color = _normalRelationColor;
                }

                if (_relationLabel != null)
                {
                    _relationLabel.color = _normalRelationColor;
                }

                if (_surface != null)
                {
                    _surface.color = ResolveSurfaceColor(_isSelected);
                }

                if (_healthFill != null)
                {
                    _healthFill.color = ResolveHealthColor(_healthFill.fillAmount);
                }
            }

            ApplyStatusAndCastColors();
        }

        public void SetLayoutParent(RectTransform parent)
        {
            if (parent == null)
            {
                return;
            }

            if (transform.parent != parent)
            {
                transform.SetParent(parent, false);
            }

            transform.SetAsLastSibling();
        }

        public void SetVisible(bool visible)
        {
            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }
        }

        public void Clear()
        {
            _participantId = 0;
            _isBound = false;
            _isFriendly = false;
            _isHuman = false;
            _isAlive = false;
            _isSelected = false;
            _isHidden = false;
            _lastHealthPercent = int.MinValue;
            SetActiveIfNeeded(_selectedMarker, false);
            SetVisible(false);
        }

        private void OnClicked()
        {
            if (_isBound && _isAlive && !_isHidden)
            {
                Selected?.Invoke(_participantId);
            }
        }

        private void RestoreCanvasInteraction()
        {
            bool interactable = _isBound && _isAlive && !_isHidden;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = _isAlive ? 1f : _deadAlpha;
                _canvasGroup.interactable = interactable;
                _canvasGroup.blocksRaycasts = interactable;
            }

            if (_button != null)
            {
                _button.interactable = interactable;
            }
        }

        private static string ResolveRelation(string relation, bool isFriendly, bool isHuman)
        {
            if (!string.IsNullOrWhiteSpace(relation))
            {
                return relation.ToUpperInvariant();
            }

            if (isHuman)
            {
                return "YOU";
            }

            return isFriendly ? "ALLY" : "ENEMY";
        }

        private static string DescribeClassBadge(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Healer:
                    return "P";
                case ClassId.Rogue:
                    return "R";
                case ClassId.Mage:
                    return "M";
                case ClassId.Hunter:
                    return "H";
                case ClassId.Boss:
                    return "B";
                default:
                    return "?";
            }
        }

        private Color ResolveClassColor(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Healer:
                    return _healerBadgeColor;
                case ClassId.Rogue:
                    return _rogueBadgeColor;
                case ClassId.Mage:
                    return _mageBadgeColor;
                case ClassId.Hunter:
                    return _hunterBadgeColor;
                case ClassId.Boss:
                    return _bossBadgeColor;
                default:
                    return _friendlyColor;
            }
        }

        private Color ResolveHealthColor(float healthRatio)
        {
            Color critical = _highContrastEnabled
                ? HighContrastCriticalColor
                : _criticalColor;
            Color wounded = _highContrastEnabled
                ? HighContrastWoundedColor
                : _woundedColor;
            Color healthy = _highContrastEnabled
                ? HighContrastHealthyColor
                : _healthyColor;
            if (healthRatio <= 0.3f)
            {
                return Color.Lerp(critical, wounded, healthRatio / 0.3f);
            }

            return Color.Lerp(wounded, healthy, (healthRatio - 0.3f) / 0.7f);
        }

        private Color ResolveRelationColor(bool isFriendly, bool isHuman)
        {
            if (_highContrastEnabled)
            {
                return isHuman
                    ? HighContrastHumanColor
                    : isFriendly
                        ? HighContrastFriendlyColor
                        : HighContrastHostileColor;
            }

            return isHuman
                ? _humanColor
                : isFriendly
                    ? _friendlyColor
                    : _hostileColor;
        }

        private Color ResolveSurfaceColor(bool isSelected)
        {
            if (_highContrastEnabled)
            {
                return isSelected
                    ? HighContrastSelectedSurfaceColor
                    : HighContrastSurfaceColor;
            }

            return isSelected ? _selectedSurfaceColor : _surfaceColor;
        }

        private Color ResolveHiddenColor()
        {
            return _highContrastEnabled ? HighContrastHiddenColor : _hiddenColor;
        }

        private Color ResolveHiddenSurfaceColor()
        {
            return _highContrastEnabled
                ? HighContrastHiddenSurfaceColor
                : _hiddenSurfaceColor;
        }

        private void CaptureDefaultGraphicColors()
        {
            if (!_defaultStatusColor.HasValue && _statusLabel != null)
            {
                _defaultStatusColor = _statusLabel.color;
            }

            if (!_defaultCastFillColor.HasValue && _castFill != null)
            {
                _defaultCastFillColor = _castFill.color;
            }

            if (!_defaultCastLabelColor.HasValue && _castLabel != null)
            {
                _defaultCastLabelColor = _castLabel.color;
            }

            if (!_defaultBarrierColor.HasValue && _barrierFill != null)
            {
                _defaultBarrierColor = _barrierFill.color;
            }
        }

        private void ApplyStatusAndCastColors()
        {
            if (_statusLabel != null)
            {
                _statusLabel.color = _highContrastEnabled
                    ? HighContrastStatusColor
                    : _defaultStatusColor ?? Color.white;
            }

            if (_castFill != null)
            {
                _castFill.color = _highContrastEnabled
                    ? HighContrastCastColor
                    : _defaultCastFillColor ?? Color.white;
            }

            if (_castLabel != null)
            {
                _castLabel.color = _highContrastEnabled
                    ? HighContrastCastColor
                    : _defaultCastLabelColor ?? Color.white;
            }

            if (_barrierFill != null)
            {
                _barrierFill.color = _highContrastEnabled
                    ? HighContrastBarrierColor
                    : _defaultBarrierColor ?? Color.white;
            }
        }

        private static string[] CreateHealthPercentageLabels()
        {
            var labels = new string[101];
            for (int percent = 0; percent < labels.Length; percent++)
            {
                labels[percent] = percent + "%";
            }

            return labels;
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
            _deadAlpha = Mathf.Clamp01(_deadAlpha);
            _hiddenAlpha = Mathf.Clamp01(_hiddenAlpha);
        }
#endif
    }
}
