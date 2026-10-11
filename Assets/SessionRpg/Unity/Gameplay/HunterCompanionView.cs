using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    /// <summary>
    /// Presentation-only view for the Hunter's deterministic companion. The pet is
    /// intentionally smaller than a player and never receives a roster ring.
    /// </summary>
    [DefaultExecutionOrder(50)]
    [DisallowMultipleComponent]
    public sealed class HunterCompanionView : MonoBehaviour
    {
        public const int AuthoredOrderRendererCount = 11;

        [SerializeField] private GameObject _visualRoot;
        [SerializeField] private Renderer[] _bodyRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Renderer[] _accentRenderers = System.Array.Empty<Renderer>();
        [SerializeField] private Transform _healthRoot;
        [SerializeField] private Transform _healthFill;
        [SerializeField] private Transform[] _gaitLimbs = System.Array.Empty<Transform>();
        [SerializeField] private Renderer _orderMarker;
        [SerializeField] private GameObject _attackOrderRoot;
        [SerializeField] private GameObject _followOrderRoot;
        [SerializeField] private GameObject _stayOrderRoot;
        [SerializeField] private Renderer[] _orderRenderers = System.Array.Empty<Renderer>();
        [SerializeField, Min(0f)] private float _positionSharpness = 28f;
        [SerializeField, Min(0f)] private float _rotationSharpness = 22f;

        private MaterialPropertyBlock _propertyBlock;
        private Vector3 _desiredPosition;
        private Vector3 _desiredFacing = Vector3.forward;
        private Vector3 _visualRootBasePosition;
        private Vector3 _visualRootBaseScale = Vector3.one;
        private Vector3 _healthBaseScale = Vector3.one;
        private Vector3 _orderMarkerBaseScale = Vector3.one;
        private Quaternion[] _gaitBaseRotations = System.Array.Empty<Quaternion>();
        private float _gaitStrength;
        private float _attackProgress = 1f;
        private CompanionOrder _order;
        private bool _visible;
        private int _presentedAttackCount;
        private UnityEngine.Camera _readabilityCamera;

        public CompanionOrder ActiveOrder => _order;

        public Transform EffectTarget => _visualRoot != null
            ? _visualRoot.transform
            : transform;

        public bool IsVisible => _visible;

        public int PresentedAttackCount => _presentedAttackCount;

        public bool HasAuthoredOrderLanguage =>
            _orderMarker != null &&
            _attackOrderRoot != null &&
            _followOrderRoot != null &&
            _stayOrderRoot != null &&
            _orderRenderers.Length == AuthoredOrderRendererCount;

        public bool HasAuthoredSpiderSilhouette =>
            _healthRoot != null &&
            _bodyRenderers.Length >= 19 &&
            _accentRenderers.Length >= 6;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            if (_visualRoot != null)
            {
                _visualRootBasePosition = _visualRoot.transform.localPosition;
                _visualRootBaseScale = _visualRoot.transform.localScale;
            }
            _healthBaseScale = _healthFill != null
                ? _healthFill.localScale
                : Vector3.one;
            _orderMarkerBaseScale = _orderMarker != null
                ? _orderMarker.transform.localScale
                : Vector3.one;
            _gaitBaseRotations = new Quaternion[_gaitLimbs.Length];
            for (int index = 0; index < _gaitLimbs.Length; index++)
            {
                _gaitBaseRotations[index] = _gaitLimbs[index] != null
                    ? _gaitLimbs[index].localRotation
                    : Quaternion.identity;
            }
            _readabilityCamera = UnityEngine.Camera.main;
            Hide();
        }

        private void LateUpdate()
        {
            if (!_visible)
            {
                return;
            }

            float deltaTime = Mathf.Max(Time.deltaTime, 0.0001f);
            float distanceBeforeMove = Vector3.Distance(transform.position, _desiredPosition);
            float positionBlend = _positionSharpness <= 0f
                ? 1f
                : 1f - Mathf.Exp(-_positionSharpness * deltaTime);
            transform.position = Vector3.Lerp(transform.position, _desiredPosition, positionBlend);
            float desiredGait = Mathf.Clamp01(distanceBeforeMove / (deltaTime * 2.2f));
            _gaitStrength = Mathf.MoveTowards(
                _gaitStrength,
                desiredGait,
                deltaTime * 7f);
            if (_desiredFacing.sqrMagnitude > 0.001f)
            {
                Quaternion rotation = Quaternion.LookRotation(_desiredFacing, Vector3.up);
                float rotationBlend = _rotationSharpness <= 0f
                    ? 1f
                    : 1f - Mathf.Exp(-_rotationSharpness * deltaTime);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, rotationBlend);
            }

            for (int index = 0; index < _gaitLimbs.Length; index++)
            {
                Transform limb = _gaitLimbs[index];
                if (limb == null)
                {
                    continue;
                }

                float sidePhase = (index & 1) == 0 ? 0f : Mathf.PI;
                float rowPhase = (index / 4) * 0.72f;
                float swing = Mathf.Sin(
                    (Time.time * 12f) + sidePhase + rowPhase) *
                    11f * _gaitStrength;
                limb.localRotation = _gaitBaseRotations[index] *
                    Quaternion.Euler(0f, 0f, swing);
            }

            if (_visualRoot != null)
            {
                _attackProgress = Mathf.Min(1f, _attackProgress + (deltaTime * 5.5f));
                float lunge = Mathf.Sin(_attackProgress * Mathf.PI);
                _visualRoot.transform.localPosition = _visualRootBasePosition +
                    (Vector3.forward * (lunge * 0.2f));
                _visualRoot.transform.localScale = Vector3.Scale(
                    _visualRootBaseScale,
                    new Vector3(
                        1f - (lunge * 0.04f),
                        1f + (lunge * 0.04f),
                        1f + (lunge * 0.12f)));
            }

            if (_orderMarker != null)
            {
                float frequency = _order == CompanionOrder.Attack ? 10f : 6f;
                float amplitude = _order == CompanionOrder.Stay ? 0.035f : 0.08f;
                float pulse = 1f + (Mathf.Sin(Time.time * frequency) * amplitude);
                _orderMarker.transform.localScale = _orderMarkerBaseScale * pulse;
            }

            if (_attackOrderRoot != null && _attackOrderRoot.activeSelf)
            {
                float attackPulse = 1f + Mathf.Sin(Time.time * 11f) * 0.09f;
                _attackOrderRoot.transform.localScale = Vector3.one * attackPulse;
            }

            if (_followOrderRoot != null && _followOrderRoot.activeSelf)
            {
                _followOrderRoot.transform.Rotate(
                    Vector3.up,
                    48f * deltaTime,
                    Space.Self);
            }

            if (_stayOrderRoot != null && _stayOrderRoot.activeSelf)
            {
                float stayPulse = 1f + Mathf.Sin(Time.time * 3.5f) * 0.025f;
                _stayOrderRoot.transform.localScale = Vector3.one * stayPulse;
            }

            if (_readabilityCamera == null)
            {
                _readabilityCamera = UnityEngine.Camera.main;
            }

            if (_readabilityCamera != null && _healthRoot != null)
            {
                _healthRoot.rotation = _readabilityCamera.transform.rotation;
            }
        }

        public void Present(CompanionSnapshot snapshot, Color teamColor)
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            _visible = snapshot.IsAlive;
            if (_visualRoot != null && _visualRoot.activeSelf != _visible)
            {
                _visualRoot.SetActive(_visible);
            }

            if (!_visible)
            {
                return;
            }

            _desiredPosition = new Vector3(snapshot.Position.X, 0.04f, snapshot.Position.Z);
            _desiredFacing = new Vector3(snapshot.Facing.X, 0f, snapshot.Facing.Z);
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }

            if (transform.position.sqrMagnitude < 0.0001f)
            {
                transform.position = _desiredPosition;
            }

            float health = snapshot.MaxHealth > 0f
                ? Mathf.Clamp01(snapshot.Health / snapshot.MaxHealth)
                : 0f;
            if (_healthFill != null)
            {
                Vector3 scale = _healthBaseScale;
                scale.x *= health;
                _healthFill.localScale = scale;
            }

            _order = snapshot.Order;
            Color orderColor = ResolveOrderColor(_order);
            Color bodyColor = Color.Lerp(
                new Color(0.014f, 0.024f, 0.03f, 1f),
                teamColor,
                0.12f);
            for (int index = 0; index < _bodyRenderers.Length; index++)
            {
                Renderer renderer = _bodyRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", bodyColor);
                _propertyBlock.SetColor("_Color", bodyColor);
                _propertyBlock.SetColor("_EmissionColor", teamColor * 0.035f);
                renderer.SetPropertyBlock(_propertyBlock);
            }

            SetActive(_attackOrderRoot, _order == CompanionOrder.Attack);
            SetActive(_followOrderRoot, _order == CompanionOrder.Follow);
            SetActive(_stayOrderRoot, _order == CompanionOrder.Stay);
            for (int index = 0; index < _accentRenderers.Length; index++)
            {
                Renderer renderer = _accentRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                Color accentColor = Color.Lerp(teamColor, Color.white, 0.08f);
                _propertyBlock.SetColor("_BaseColor", accentColor);
                _propertyBlock.SetColor("_Color", accentColor);
                _propertyBlock.SetColor("_EmissionColor", accentColor * 1.1f);
                renderer.SetPropertyBlock(_propertyBlock);
            }

            if (_orderMarker != null)
            {
                _orderMarker.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", orderColor);
                _propertyBlock.SetColor("_EmissionColor", orderColor * 1.8f);
                _orderMarker.SetPropertyBlock(_propertyBlock);
            }

            for (int index = 0; index < _orderRenderers.Length; index++)
            {
                Renderer renderer = _orderRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", orderColor);
                _propertyBlock.SetColor("_Color", orderColor);
                _propertyBlock.SetColor("_EmissionColor", orderColor * 1.8f);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        public void PlayAttack(Vector3 targetPosition)
        {
            if (!_visible)
            {
                return;
            }

            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
            {
                _desiredFacing = direction.normalized;
            }

            _attackProgress = 0f;
            _presentedAttackCount++;
        }

        public void Hide()
        {
            bool wasVisible = _visible;
            _visible = false;
            if (_visualRoot != null && (wasVisible || _visualRoot.activeSelf))
            {
                _visualRoot.SetActive(false);
            }

            if (_visualRoot != null)
            {
                _visualRoot.transform.localPosition = _visualRootBasePosition;
                _visualRoot.transform.localScale = _visualRootBaseScale;
            }
            _attackProgress = 1f;
        }

        private static Color ResolveOrderColor(CompanionOrder order)
        {
            switch (order)
            {
                case CompanionOrder.Attack:
                    return new Color(1f, 0.32f, 0.16f, 1f);
                case CompanionOrder.Stay:
                    return new Color(1f, 0.74f, 0.2f, 1f);
                default:
                    return new Color(0.28f, 0.9f, 0.72f, 1f);
            }
        }

        private static void SetActive(GameObject value, bool active)
        {
            if (value != null && value.activeSelf != active)
            {
                value.SetActive(active);
            }
        }
    }
}
