using System.Text;
using SessionRpg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Unity.UI
{
    /// <summary>
    /// Pre-match, read-only explanation of the complete normalized kit for the
    /// currently selected mode. Training still previews full Arena power.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KitViewerPanel : MonoBehaviour
    {
        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private Text _titleLabel;
        [SerializeField] private Text _bodyLabel;

        private SpecializationId _specialization;
        private PlayableModeId _mode = PlayableModeId.Arena;

        public bool IsOpen => _panelRoot != null && _panelRoot.activeSelf;

        public string VisibleGuide => _bodyLabel != null
            ? _bodyLabel.text
            : string.Empty;

        public void Bind(SpecializationId specialization)
        {
            Bind(PlayableModeId.Arena, specialization);
        }

        public void Bind(PlayableModeId mode, SpecializationId specialization)
        {
            if (!SpecializationCatalog.TryGetKit(specialization, out _))
            {
                return;
            }

            SpecializationDefinition definition =
                SpecializationCatalog.GetDefinition(specialization);
            _mode = NormalizeMode(mode);
            _specialization = specialization;
            if (_titleLabel != null)
            {
                _titleLabel.text = definition.DisplayName.ToUpperInvariant() +
                    "  •  " + ModeLabel(_mode) + " KIT";
            }

            if (_bodyLabel != null)
            {
                _bodyLabel.text = BuildGuide(_mode, specialization);
            }

            Text openLabel = _openButton != null
                ? _openButton.GetComponentInChildren<Text>(true)
                : null;
            if (openLabel != null)
            {
                openLabel.text = "VIEW " + ModeLabel(_mode) + " KIT";
            }
        }

        public void Open()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(true);
            }
        }

        public void Close()
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            _openButton?.onClick.AddListener(Open);
            _closeButton?.onClick.AddListener(Close);
            if (_specialization != SpecializationId.None)
            {
                Bind(_mode, _specialization);
            }
        }

        private void OnDisable()
        {
            _openButton?.onClick.RemoveListener(Open);
            _closeButton?.onClick.RemoveListener(Close);
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            GameObject panelRoot,
            Button openButton,
            Button closeButton,
            Text titleLabel,
            Text bodyLabel)
        {
            _panelRoot = panelRoot;
            _openButton = openButton;
            _closeButton = closeButton;
            _titleLabel = titleLabel;
            _bodyLabel = bodyLabel;
        }
#endif

        private static string BuildGuide(
            PlayableModeId mode,
            SpecializationId specialization)
        {
            AbilityKit kit = ModeAbilityKitCatalog.GetKit(mode, specialization);
            var builder = new StringBuilder(2048);
            builder.AppendLine("TAP FOR THE PRIMARY ACTION. PRESS AND HOLD A GROUPED SKILL TO INSPECT ITS WHEEL; DRAG TO A DIRECTION AND RELEASE TO CAST.");
            builder.AppendLine(mode == PlayableModeId.HeroSiege
                ? "Hero Siege grants every action below, including any mode-only crowd-clearing skill."
                : mode == PlayableModeId.Dungeon
                    ? "Dungeon grants the complete specialization kit shown below."
                    : "Training teaches seven actions; Arena always grants every action below.");
            builder.AppendLine();

            for (int slot = 0; slot < AbilityKit.Capacity; slot++)
            {
                AbilitySlotBinding binding = kit.GetSlotBinding(slot);
                AppendAbility(builder, slot + 1, AbilitySlotDirection.None, binding.Primary);
                AppendAbility(builder, slot + 1, AbilitySlotDirection.Up, binding.Up);
                AppendAbility(builder, slot + 1, AbilitySlotDirection.Down, binding.Down);
                AppendAbility(builder, slot + 1, AbilitySlotDirection.Left, binding.Left);
                AppendAbility(builder, slot + 1, AbilitySlotDirection.Right, binding.Right);
            }

            return builder.ToString();
        }

        private static PlayableModeId NormalizeMode(PlayableModeId mode) =>
            mode == PlayableModeId.HeroSiege || mode == PlayableModeId.Dungeon
                ? mode
                : PlayableModeId.Arena;

        private static string ModeLabel(PlayableModeId mode)
        {
            switch (mode)
            {
                case PlayableModeId.HeroSiege:
                    return "HERO SIEGE";
                case PlayableModeId.Dungeon:
                    return "DUNGEON";
                default:
                    return "ARENA";
            }
        }

        private static void AppendAbility(
            StringBuilder builder,
            int slot,
            AbilitySlotDirection direction,
            AbilityId ability)
        {
            if (ability == AbilityId.None ||
                !AbilityCatalog.TryGet(ability, out AbilityDefinition definition))
            {
                return;
            }

            builder.Append(direction == AbilitySlotDirection.None ? string.Empty : "   ↳ ");
            builder.Append(slot);
            if (direction != AbilitySlotDirection.None)
            {
                builder.Append(' ');
                builder.Append(DirectionGlyph(direction));
            }

            builder.Append("  ");
            builder.Append(definition.DisplayName.ToUpperInvariant());
            builder.Append(" — ");
            builder.Append(string.IsNullOrWhiteSpace(definition.Description)
                ? definition.ShortPurpose
                : definition.Description);
            builder.AppendLine();
        }

        private static char DirectionGlyph(AbilitySlotDirection direction)
        {
            switch (direction)
            {
                case AbilitySlotDirection.Up:
                    return '↑';
                case AbilitySlotDirection.Down:
                    return '↓';
                case AbilitySlotDirection.Left:
                    return '←';
                case AbilitySlotDirection.Right:
                    return '→';
                default:
                    return '•';
            }
        }
    }
}
