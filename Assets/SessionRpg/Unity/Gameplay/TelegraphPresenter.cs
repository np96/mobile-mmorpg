using System;
using SessionRpg.Core;
using SessionRpg.Unity.Presentation;
using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    public enum TelegraphVisualMotif
    {
        Circle,
        Shockwave,
        FrostBurst,
        FlameBurst,
        PsychicBurst,
        TargetLock,
        MeteorImpact,
    }

    [DisallowMultipleComponent]
    public sealed class TelegraphPresenter : MonoBehaviour
    {
        [SerializeField] private Transform _shape;
        [SerializeField] private Renderer _renderer;
        [SerializeField] private Transform _accentRoot;
        [SerializeField] private GameObject _circleMotif;
        [SerializeField] private GameObject _shockwaveMotif;
        [SerializeField] private GameObject _frostMotif;
        [SerializeField] private GameObject _flameMotif;
        [SerializeField] private GameObject _psychicMotif;
        [SerializeField] private GameObject _targetLockMotif;
        [SerializeField] private GameObject _meteorMotif;
        [SerializeField] private Renderer[] _accentRenderers = Array.Empty<Renderer>();

        private MaterialPropertyBlock _propertyBlock;
        private float _duration;
        private float _remaining;
        private bool _persistent;
        private bool _lastResolutionConnected = true;
        private TelegraphVisualMotif _activeMotif;

        public bool IsActive => _persistent || _remaining > 0f;

        public TelegraphVisualMotif ActiveMotif => _activeMotif;

        public bool LastResolutionConnected => _lastResolutionConnected;

        public int SerializedAccentRendererCount => _accentRenderers?.Length ?? 0;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            Hide();
        }

        private void Update()
        {
            if (!_persistent && _remaining <= 0f)
            {
                return;
            }

            if (!_persistent)
            {
                _remaining -= Time.deltaTime;
            }

            float progress = _persistent
                ? Mathf.Repeat(Time.unscaledTime * 1.18f, 1f)
                : 1f - Mathf.Clamp01(_remaining / Mathf.Max(0.01f, _duration));
            float pulse = 0.95f +
                (Mathf.Sin((progress * Mathf.PI * 2f) + 0.45f) * 0.05f);
            if (_shape != null)
            {
                _shape.localScale = new Vector3(pulse, 0.03f, pulse);
            }

            if (_accentRoot != null)
            {
                float rotationDirection = _activeMotif == TelegraphVisualMotif.PsychicBurst
                    ? -1f
                    : 1f;
                _accentRoot.Rotate(
                    Vector3.up,
                    rotationDirection * Time.deltaTime *
                    (_persistent ? 22f : 46f),
                    Space.Self);

                float accentScale;
                switch (_activeMotif)
                {
                    case TelegraphVisualMotif.Shockwave:
                    case TelegraphVisualMotif.FrostBurst:
                    case TelegraphVisualMotif.FlameBurst:
                    case TelegraphVisualMotif.PsychicBurst:
                        accentScale = _persistent
                            ? 0.91f + (Mathf.PingPong(progress * 2f, 1f) * 0.09f)
                            : 0.72f + (progress * 0.3f);
                        break;
                    case TelegraphVisualMotif.MeteorImpact:
                        accentScale = _persistent
                            ? 1.08f - (progress * 0.16f)
                            : 1.12f - (progress * 0.24f);
                        break;
                    default:
                        accentScale = pulse;
                        break;
                }

                _accentRoot.localScale = Vector3.one * accentScale;
            }

            if (!_persistent && _remaining <= 0f)
            {
                Hide();
            }
        }

        public void Show(AbilityId ability, Vector3 position, float radius, float duration)
        {
            Show(ability, position, radius, duration, Vector3.forward);
        }

        public void Show(
            AbilityId ability,
            Vector3 position,
            float radius,
            float duration,
            Vector3 direction)
        {
            Present(ability, position, radius, direction, true);
            _persistent = false;
            _duration = Mathf.Max(0.05f, duration);
            _remaining = _duration;
        }

        public void ShowResolved(
            AbilityId ability,
            Vector3 position,
            float radius,
            float duration,
            Vector3 direction,
            bool connected)
        {
            Present(ability, position, radius, direction, connected);
            _persistent = false;
            _duration = Mathf.Max(0.05f, duration);
            _remaining = _duration;
        }

        public void ShowPersistent(AbilityId ability, Vector3 position, float radius)
        {
            ShowPersistent(ability, position, radius, Vector3.forward);
        }

        public void ShowPersistent(
            AbilityId ability,
            Vector3 position,
            float radius,
            Vector3 direction)
        {
            Present(ability, position, radius, direction, true);
            _persistent = true;
            _duration = 0f;
            _remaining = 0f;
        }

        public static TelegraphVisualMotif ResolveMotif(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.Gust:
                case AbilityId.BlastWave:
                case AbilityId.BossKnockback:
                    return TelegraphVisualMotif.Shockwave;
                case AbilityId.FrostNova:
                case AbilityId.DeepFreeze:
                case AbilityId.ConeOfCold:
                case AbilityId.WaterElementalFreeze:
                    return TelegraphVisualMotif.FrostBurst;
                case AbilityId.DragonBreath:
                case AbilityId.FireBlast:
                    return TelegraphVisualMotif.FlameBurst;
                case AbilityId.PsychicScream:
                    return TelegraphVisualMotif.PsychicBurst;
                case AbilityId.Polymorph:
                case AbilityId.Flare:
                    return TelegraphVisualMotif.TargetLock;
                case AbilityId.BossMeteor:
                case AbilityId.LivingBomb:
                    return TelegraphVisualMotif.MeteorImpact;
                default:
                    return TelegraphVisualMotif.Circle;
            }
        }

        public void Hide()
        {
            _persistent = false;
            _remaining = 0f;
            gameObject.SetActive(false);
        }

        private void Present(
            AbilityId ability,
            Vector3 position,
            float radius,
            Vector3 direction,
            bool connected)
        {
            transform.position = position + new Vector3(0f, 0.065f, 0f);
            Vector3 flatDirection = direction;
            flatDirection.y = 0f;
            if (flatDirection.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(flatDirection.normalized, Vector3.up);
            }

            transform.localScale = new Vector3(radius * 2f, 1f, radius * 2f);
            _lastResolutionConnected = connected;
            _activeMotif = ResolveMotif(ability);
            SetActiveMotif(_activeMotif);
            gameObject.SetActive(true);

            Color color = ResolveColor(ability, connected);
            Color fillColor = color;
            fillColor.a = connected ? 0.2f : 0.12f;
            SetColor(_renderer, fillColor, 0.75f);
            for (int index = 0; index < _accentRenderers.Length; index++)
            {
                SetColor(_accentRenderers[index], color, 1.28f);
            }
        }

        private void SetActiveMotif(TelegraphVisualMotif motif)
        {
            SetActive(_circleMotif, motif == TelegraphVisualMotif.Circle);
            SetActive(_shockwaveMotif, motif == TelegraphVisualMotif.Shockwave);
            SetActive(_frostMotif, motif == TelegraphVisualMotif.FrostBurst);
            SetActive(_flameMotif, motif == TelegraphVisualMotif.FlameBurst);
            SetActive(_psychicMotif, motif == TelegraphVisualMotif.PsychicBurst);
            SetActive(_targetLockMotif, motif == TelegraphVisualMotif.TargetLock);
            SetActive(_meteorMotif, motif == TelegraphVisualMotif.MeteorImpact);
        }

        private static Color ResolveColor(AbilityId ability, bool connected)
        {
            if (!connected)
            {
                return new Color(0.72f, 0.78f, 0.84f, 0.72f);
            }

            if (AbilityPresentationCatalog.TryGet(
                    ability,
                    out AbilityPresentation presentation))
            {
                Color result = presentation.Color;
                result.a = 0.82f;
                return result;
            }

            return new Color(1f, 0.48f, 0.08f, 0.82f);
        }

        private void SetColor(Renderer renderer, Color color, float emission)
        {
            if (renderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            _propertyBlock.SetColor("_Color", color);
            _propertyBlock.SetColor("_EmissionColor", color * emission);
            renderer.SetPropertyBlock(_propertyBlock);
            _propertyBlock.Clear();
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
