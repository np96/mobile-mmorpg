using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    [DefaultExecutionOrder(110)]
    [DisallowMultipleComponent]
    public sealed class CombatantRelationMarker : MonoBehaviour
    {
        [SerializeField] private TextMesh _label;
        [SerializeField] private TextMesh _classBadgeLabel;
        [SerializeField] private Transform _selectionChevron;
        [SerializeField] private GameObject _allyIdentityRoot;
        [SerializeField] private GameObject _enemyIdentityRoot;
        [SerializeField] private GameObject _humanIdentityRoot;
        [SerializeField] private GameObject _bossIdentityRoot;
        [SerializeField] private Renderer[] _identityRenderers = System.Array.Empty<Renderer>();
        [SerializeField, Min(0f)] private float _idleBobHeight = 0.035f;

        private Color _relationColor = Color.white;
        private MaterialPropertyBlock _propertyBlock;
        private RelationKind _relation;
        private CombatantControlVisualState _controlVisualState;
        private Vector3 _labelBasePosition;
        private Vector3 _chevronBasePosition;
        private float _labelBaseCharacterSize = 1f;
        private bool _absorbing;
        private bool _mitigating;
        private bool _casting;
        private bool _channeling;
        private bool _selected;
        private bool _referencesInitialized;
        private UnityEngine.Camera _readabilityCamera;

        public bool HasAuthoredIdentityLanguage =>
            _classBadgeLabel != null &&
            _allyIdentityRoot != null &&
            _enemyIdentityRoot != null &&
            _humanIdentityRoot != null &&
            _bossIdentityRoot != null &&
            _identityRenderers.Length > 0;

        public string ClassBadgeText => _classBadgeLabel != null
            ? _classBadgeLabel.text
            : string.Empty;

        private void Awake()
        {
            EnsureReferencesInitialized();
        }

        private void LateUpdate()
        {
            if (!_referencesInitialized)
            {
                return;
            }

            float relationOffset = (int)_relation * 0.8f;
            float bob = Mathf.Sin((Time.time * 4.5f) + relationOffset) * _idleBobHeight;
            if (_label != null)
            {
                _label.transform.localPosition = _labelBasePosition + (Vector3.up * bob);
            }

            if (_selectionChevron != null && _selectionChevron.gameObject.activeSelf)
            {
                float selectionBob = Mathf.Sin(Time.time * 9f) * (_idleBobHeight * 1.4f);
                _selectionChevron.localPosition =
                    _chevronBasePosition + (Vector3.up * selectionBob);
            }

            if (_readabilityCamera == null)
            {
                _readabilityCamera = UnityEngine.Camera.main;
            }

            if (_readabilityCamera != null)
            {
                // Align to the camera axes, not the camera position. A spherical
                // look-at billboard visibly tilts at the edges of a perspective
                // strategy view and makes relation/class badges harder to scan.
                transform.rotation = _readabilityCamera.transform.rotation;
            }
        }

        public void Configure(TeamId team, bool isHuman, ClassId classId)
        {
            EnsureReferencesInitialized();
            if (isHuman)
            {
                _relation = RelationKind.Human;
                _relationColor = new Color(1f, 0.76f, 0.22f, 1f);
            }
            else if (classId == ClassId.Boss)
            {
                _relation = RelationKind.Boss;
                _relationColor = new Color(1f, 0.3f, 0.12f, 1f);
            }
            else if (team.Value == 1)
            {
                _relation = RelationKind.Ally;
                _relationColor = new Color(0.2f, 0.8f, 1f, 1f);
            }
            else
            {
                _relation = RelationKind.Enemy;
                _relationColor = new Color(1f, 0.32f, 0.22f, 1f);
            }

            _controlVisualState = CombatantControlVisualState.None;
            _absorbing = false;
            _mitigating = false;
            _casting = false;
            _channeling = false;
            _selected = false;
            ConfigureIdentityLanguage(classId);
            ApplyRelationColor();
            RefreshLabel();
            SetChevronActive(false);
        }

        public void ConfigureLevelingBadge(string badge, Color accent)
        {
            if (_classBadgeLabel == null || string.IsNullOrEmpty(badge))
            {
                return;
            }

            _classBadgeLabel.text = badge;
            _classBadgeLabel.color = accent;
        }

        public void SetSelected(bool selected)
        {
            if (_selected == selected)
            {
                return;
            }

            _selected = selected;
            RefreshLabel();
            SetChevronActive(selected);
        }

        public void SetControlVisualState(CombatantControlVisualState state)
        {
            if (_controlVisualState == state)
            {
                return;
            }

            _controlVisualState = state;
            RefreshLabel();
        }

        public void SetDefenseState(bool absorbing, bool mitigating)
        {
            if (_absorbing == absorbing && _mitigating == mitigating)
            {
                return;
            }

            _absorbing = absorbing;
            _mitigating = mitigating;
            RefreshLabel();
        }

        public void SetCastingState(bool casting, bool channeling)
        {
            bool activeChannel = casting && channeling;
            if (_casting == casting && _channeling == activeChannel)
            {
                return;
            }

            _casting = casting;
            _channeling = activeChannel;
            RefreshLabel();
        }

        private void ConfigureIdentityLanguage(ClassId classId)
        {
            FontStyle fontStyle;
            switch (_relation)
            {
                case RelationKind.Human:
                    fontStyle = FontStyle.Bold;
                    break;
                case RelationKind.Enemy:
                    fontStyle = FontStyle.Bold;
                    break;
                case RelationKind.Boss:
                    fontStyle = FontStyle.Bold;
                    break;
                default:
                    fontStyle = FontStyle.Normal;
                    break;
            }

            if (_label != null)
            {
                _label.characterSize = _labelBaseCharacterSize;
                _label.fontStyle = fontStyle;
            }

            if (_classBadgeLabel != null)
            {
                _classBadgeLabel.text = ResolveClassBadge(classId);
                _classBadgeLabel.fontStyle = FontStyle.Bold;
            }

            SetActive(_allyIdentityRoot, _relation == RelationKind.Ally);
            SetActive(_enemyIdentityRoot, _relation == RelationKind.Enemy);
            SetActive(_humanIdentityRoot, _relation == RelationKind.Human);
            SetActive(_bossIdentityRoot, _relation == RelationKind.Boss);
        }

        private void RefreshLabel()
        {
            if (_label == null)
            {
                return;
            }

            string stateLabel = ResolveStateLabel();
            // Identity and health already live in the compact fighter circles and
            // world bars. Repeating them as meter-wide text obscures combat on a
            // phone. Keep this marker for exceptional, actionable state only.
            _label.text = stateLabel;
            _label.color = ResolveStateColor();
        }

        private string ResolveStateLabel()
        {
            if ((_controlVisualState & CombatantControlVisualState.Stun) != 0)
            {
                return "STUNNED";
            }

            if ((_controlVisualState & CombatantControlVisualState.Horror) != 0)
            {
                return "HORRIFIED";
            }

            if ((_controlVisualState & CombatantControlVisualState.Fear) != 0)
            {
                return "FEARED";
            }

            if ((_controlVisualState & CombatantControlVisualState.Disorient) != 0)
            {
                return "DISORIENTED";
            }

            if ((_controlVisualState & CombatantControlVisualState.Root) != 0)
            {
                return "ROOTED";
            }

            if ((_controlVisualState & CombatantControlVisualState.Disarm) != 0)
            {
                return "DISARMED";
            }

            if ((_controlVisualState & CombatantControlVisualState.Pacify) != 0)
            {
                return "PACIFIED";
            }

            if ((_controlVisualState & CombatantControlVisualState.Stealth) != 0)
            {
                return "STEALTH";
            }

            if (_channeling)
            {
                return "CHANNEL";
            }

            if (_mitigating)
            {
                return "MITIGATING";
            }

            return _absorbing ? "ABSORB" : string.Empty;
        }

        private Color ResolveStateColor()
        {
            if ((_controlVisualState & CombatantControlVisualState.Stun) != 0)
            {
                return new Color(1f, 0.82f, 0.18f, 1f);
            }

            if ((_controlVisualState & CombatantControlVisualState.Horror) != 0)
            {
                return new Color(0.94f, 0.24f, 0.72f, 1f);
            }

            if ((_controlVisualState & CombatantControlVisualState.Fear) != 0)
            {
                return new Color(0.82f, 0.38f, 1f, 1f);
            }

            if ((_controlVisualState & CombatantControlVisualState.Root) != 0)
            {
                return new Color(0.4f, 0.9f, 1f, 1f);
            }

            if ((_controlVisualState & CombatantControlVisualState.Disorient) != 0)
            {
                return new Color(1f, 0.52f, 0.86f, 1f);
            }

            if ((_controlVisualState & CombatantControlVisualState.Disarm) != 0)
            {
                return new Color(1f, 0.5f, 0.2f, 1f);
            }

            if ((_controlVisualState & CombatantControlVisualState.Pacify) != 0)
            {
                return new Color(0.66f, 0.76f, 0.92f, 1f);
            }

            if ((_controlVisualState & CombatantControlVisualState.Stealth) != 0)
            {
                return new Color(0.64f, 0.7f, 0.82f, 0.78f);
            }

            if (_mitigating)
            {
                return new Color(1f, 0.74f, 0.24f, 1f);
            }

            if (_absorbing)
            {
                return new Color(0.44f, 0.9f, 1f, 1f);
            }

            return _relationColor;
        }

        private void EnsureReferencesInitialized()
        {
            if (_referencesInitialized)
            {
                return;
            }

            if (_label != null)
            {
                _labelBasePosition = _label.transform.localPosition;
                _labelBaseCharacterSize = _label.characterSize;
            }

            if (_selectionChevron != null)
            {
                _chevronBasePosition = _selectionChevron.localPosition;
            }

            _propertyBlock = new MaterialPropertyBlock();
            _readabilityCamera = UnityEngine.Camera.main;
            _referencesInitialized = true;
        }

        private void ApplyRelationColor()
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            for (int index = 0; index < _identityRenderers.Length; index++)
            {
                Renderer renderer = _identityRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", _relationColor);
                _propertyBlock.SetColor("_Color", _relationColor);
                _propertyBlock.SetColor("_EmissionColor", _relationColor * 1.25f);
                renderer.SetPropertyBlock(_propertyBlock);
            }

            if (_classBadgeLabel != null)
            {
                _classBadgeLabel.color = _relationColor;
            }
        }

        private static string ResolveClassBadge(ClassId classId)
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

        private void SetChevronActive(bool active)
        {
            if (_selectionChevron != null && _selectionChevron.gameObject.activeSelf != active)
            {
                _selectionChevron.gameObject.SetActive(active);
            }
        }

        private static void SetActive(GameObject value, bool active)
        {
            if (value != null && value.activeSelf != active)
            {
                value.SetActive(active);
            }
        }

        private enum RelationKind
        {
            Ally,
            Enemy,
            Human,
            Boss,
        }
    }
}
