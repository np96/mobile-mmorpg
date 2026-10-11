using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CombatEffectView : MonoBehaviour
    {
        private const int TrailPointCount = 8;
        public const float CoreAlphaCap = 0.85f;
        public const float ShellAlphaCap = 0.56f;
        public const float DiscAlphaCap = 0.12f;
        public const float MotifAlphaCap = 0.7f;
        public const float HaloAlphaCap = 0.34f;
        public const float TrailAlphaCap = 0.68f;
        public const float ProjectileParticleMinimumSize = 0.22f;
        public const float ProjectileParticleMaximumSize = 0.6f;
        public const float SpatialParticleMinimumSize = 0.3f;
        public const float SpatialParticleMaximumSize = 0.9f;
        public const float ProjectileCoreAlphaCap = 0.68f;
        public const float ProjectileShellAlphaCap = 0.24f;

        [SerializeField] private Transform _core;
        [SerializeField] private Transform _shell;
        [SerializeField] private Transform _disc;
        [SerializeField] private Transform[] _motifs = new Transform[0];
        [SerializeField] private LineRenderer[] _halos = new LineRenderer[0];
        [SerializeField] private LineRenderer[] _trails = new LineRenderer[0];
        [SerializeField] private ParticleSystem _particles;
        [SerializeField] private ParticleSystemRenderer _particleRenderer;
        [SerializeField] private ParticleSystem _groundParticles;
        [SerializeField] private ParticleSystemRenderer _groundParticleRenderer;
        [SerializeField] private Light _pulseLight;
        [SerializeField] private Renderer _coreRenderer;
        [SerializeField] private Renderer _shellRenderer;
        [SerializeField] private Renderer _discRenderer;
        [SerializeField] private Renderer[] _motifRenderers = new Renderer[0];
        [SerializeField] private MeshFilter _coreMeshFilter;
        [SerializeField] private MeshFilter _shellMeshFilter;
        [SerializeField] private MeshFilter[] _motifMeshFilters = new MeshFilter[0];
        [SerializeField] private Mesh[] _motifBaseMeshes = new Mesh[0];
        [SerializeField] private Mesh _roundedMesh;
        [SerializeField] private Mesh _cometMesh;
        [SerializeField] private Mesh _crystalMesh;

        private MaterialPropertyBlock _propertyBlock;
        private EffectMotion _motion;
        private EffectVisualPhase _phase;
        private SpellVisualProfile _profile;
        private SpellBurstSilhouette _burstSilhouette;
        private HunterShotVisualProfile _hunterShotProfile;
        private AbilityId _ability;
        private Vector3 _origin;
        private Vector3 _target;
        private float _duration;
        private float _remaining;
        private float _size;
        private Color _primaryColor;
        private Color _accentColor;

        public bool IsPlaying => _remaining > 0f;

        public AbilityId ActiveAbility => IsPlaying ? _ability : AbilityId.None;

        public SpellVisualProfile VisualProfile => _profile;

        public SpellBurstSilhouette BurstSilhouette => _burstSilhouette;

        public HunterShotVisualProfile HunterShotProfile => IsPlaying
            ? _hunterShotProfile
            : HunterShotVisualProfile.None;

        public EffectVisualPhase VisualPhase => _phase;

        public EffectMotion VisualMotion => _motion;

        public float VisualSize => IsPlaying ? _size : 0f;

        public float VisualDuration => IsPlaying ? _duration : 0f;

        public Vector3 VisualTarget => _target;

        public float TravelProgress => _duration > 0f
            ? 1f - Mathf.Clamp01(_remaining / _duration)
            : 0f;

        public int MotifCount => _motifs != null ? _motifs.Length : 0;

        public int HaloCount => _halos != null ? _halos.Length : 0;

        public int ActiveMotifCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _motifs.Length; index++)
                {
                    if (_motifs[index] != null && _motifs[index].gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ActiveHaloCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _halos.Length; index++)
                {
                    if (_halos[index] != null && _halos[index].gameObject.activeSelf)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public float ActiveMotifVerticalSpan
        {
            get
            {
                float minimum = float.PositiveInfinity;
                float maximum = float.NegativeInfinity;
                for (int index = 0; index < _motifs.Length; index++)
                {
                    Transform motif = _motifs[index];
                    if (motif == null || !motif.gameObject.activeSelf)
                    {
                        continue;
                    }

                    minimum = Mathf.Min(minimum, motif.position.y);
                    maximum = Mathf.Max(maximum, motif.position.y);
                }

                return minimum <= maximum ? maximum - minimum : 0f;
            }
        }

        public float MaximumActiveMotifRadialAlignment(Vector3 center)
        {
            float maximum = 0f;
            for (int index = 0; index < _motifs.Length; index++)
            {
                Transform motif = _motifs[index];
                if (motif == null || !motif.gameObject.activeSelf)
                {
                    continue;
                }

                Vector3 radial = motif.position - center;
                radial.y = 0f;
                Vector3 forward = motif.forward;
                forward.y = 0f;
                if (radial.sqrMagnitude > 0.0001f && forward.sqrMagnitude > 0.0001f)
                {
                    maximum = Mathf.Max(maximum,
                        Mathf.Abs(Vector3.Dot(radial.normalized, forward.normalized)));
                }
            }

            return maximum;
        }

        public int TrailCount => _trails != null ? _trails.Length : 0;

        public bool HasParticleLayer => _particles != null && _particleRenderer != null;

        public bool HasGroundParticleLayer =>
            _groundParticles != null && _groundParticleRenderer != null;

        public bool HasPulseLight => _pulseLight != null;

        public bool GroundParticlesPlaying =>
            _groundParticles != null && _groundParticles.isPlaying;

        public bool PulseLightEnabled =>
            _pulseLight != null && _pulseLight.enabled && _pulseLight.intensity > 0f;

        public int LiveParticleCount => _particles != null ? _particles.particleCount : 0;

        public float ParticleEmitterRadius => _particles != null
            ? _particles.shape.radius
            : 0f;

        public bool UsesRoundedProjectileBody =>
            _motion == EffectMotion.Projectile &&
            _coreMeshFilter != null &&
            _coreMeshFilter.sharedMesh == _roundedMesh;

        public bool HasProfileMeshes =>
            _coreMeshFilter != null &&
            _shellMeshFilter != null &&
            _motifMeshFilters != null &&
            _motifBaseMeshes != null &&
            _motifMeshFilters.Length == MotifCount &&
            _motifBaseMeshes.Length == MotifCount &&
            _roundedMesh != null &&
            _cometMesh != null &&
            _crystalMesh != null;

        public bool HasAuthoredSpellRig =>
            _core != null &&
            _shell != null &&
            _disc != null &&
            MotifCount >= 10 &&
            HaloCount >= 3 &&
            TrailCount >= 2 &&
            HasParticleLayer &&
            HasGroundParticleLayer &&
            HasPulseLight &&
            HasProfileMeshes;

        private bool IsVolumetricAreaAbility =>
            _ability == AbilityId.BlastWave || _ability == AbilityId.FrostNova;

        private bool UsesVolumetricAreaRecipe =>
            IsVolumetricAreaAbility &&
            _phase == EffectVisualPhase.Primary &&
            _motion == EffectMotion.Nova;

        private bool UsesVolumetricAreaResolution =>
            IsVolumetricAreaAbility &&
            (_phase == EffectVisualPhase.Impact || _phase == EffectVisualPhase.Miss);

        private bool UsesVolumetricAreaPresentation =>
            UsesVolumetricAreaRecipe || UsesVolumetricAreaResolution;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        public void Play(
            AbilityPresentation presentation,
            Vector3 origin,
            Vector3 target,
            EffectVisualPhase phase = EffectVisualPhase.Primary)
        {
            _ability = presentation.Ability;
            _motion = presentation.Motion;
            _phase = phase;
            _profile = SpellVisualLanguage.ResolveProfile(presentation.Ability);
            _burstSilhouette = SpellVisualLanguage.ResolveBurstSilhouette(
                presentation.Ability);
            SpellVisualLanguage.TryResolveHunterShotProfile(
                presentation.Ability,
                out _hunterShotProfile);
            _origin = origin;
            _target = target;
            _duration = Mathf.Max(0.05f, presentation.Duration);
            _remaining = _duration;
            _size = Mathf.Max(0.08f, presentation.Size);
            _primaryColor = presentation.Color;
            _accentColor = ResolveAccentColor(_profile, presentation.Color);

            gameObject.SetActive(true);
            ResetVisuals();
            ApplyProfileMeshes();
            ApplyRigColors();
            ConfigureParticles();
            ConfigureGroundParticles();
            ConfigurePulseLight();
            TickVisual(0f, 0f);
        }

        public bool Tick(float deltaTime)
        {
            if (_remaining <= 0f)
            {
                return false;
            }

            _remaining -= deltaTime;
            float normalized = TravelProgress;
            TickVisual(deltaTime, normalized);
            if (_remaining > 0f)
            {
                return true;
            }

            ResetVisuals();
            gameObject.SetActive(false);
            return false;
        }

        private void TickVisual(float deltaTime, float normalized)
        {
            TickPulseLight(normalized);
            if (_phase == EffectVisualPhase.CastStart)
            {
                TickCastStart(deltaTime, normalized);
                return;
            }

            if (_phase == EffectVisualPhase.Impact || _phase == EffectVisualPhase.Miss)
            {
                TickImpact(normalized);
                return;
            }

            switch (_motion)
            {
                case EffectMotion.Projectile:
                    TickProjectile(deltaTime, normalized);
                    break;
                case EffectMotion.Tether:
                    TickTether(normalized);
                    break;
                case EffectMotion.Dash:
                    TickDash(normalized);
                    break;
                case EffectMotion.Shield:
                    TickShield(deltaTime, normalized);
                    break;
                case EffectMotion.ShadowBrand:
                    TickShadowBrand(normalized);
                    break;
                case EffectMotion.MindChannel:
                    TickMindChannel(normalized);
                    break;
                case EffectMotion.HunterArrow:
                    TickHunterArrow(normalized);
                    break;
                case EffectMotion.Trap:
                    TickTrap(normalized);
                    break;
                case EffectMotion.FacetedWard:
                    TickFacetedWard(deltaTime, normalized);
                    break;
                case EffectMotion.Horror:
                    TickHorror(normalized);
                    break;
                case EffectMotion.TargetMark:
                    TickTargetMark(normalized);
                    break;
                case EffectMotion.Command:
                    TickCommand(normalized);
                    break;
                case EffectMotion.Volley:
                    TickVolley(normalized);
                    break;
                case EffectMotion.BladeStorm:
                    TickBladeStorm(normalized);
                    break;
                default:
                    if (_motion == EffectMotion.Burst &&
                        _burstSilhouette == SpellBurstSilhouette.MartialContact)
                    {
                        TickMeleeContact(normalized);
                    }
                    else
                    {
                        TickArea(normalized);
                    }
                    break;
            }
        }

        private void TickProjectile(float deltaTime, float normalized)
        {
            Vector3 direction = _target - _origin;
            float distance = direction.magnitude;
            Vector3 forward = distance > 0.001f ? direction / distance : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            float arcHeight = Mathf.Clamp(distance * 0.055f, 0.08f, 0.72f);
            Vector3 position = Vector3.Lerp(_origin, _target, normalized);
            position.y += Mathf.Sin(normalized * Mathf.PI) * arcHeight;
            SetParticleEmitter(
                position - forward * _size * 0.18f,
                Quaternion.LookRotation(forward, Vector3.up));
            float arrival = Mathf.InverseLerp(0.76f, 1f, normalized);
            float bodyFade = 1f - (arrival * 0.84f);

            switch (_profile)
            {
                case SpellVisualProfile.Fire:
                    TickFireComet(position, forward, side, normalized, arrival);
                    break;

                case SpellVisualProfile.Frost:
                    TickFrostLance(position, forward, side, normalized, arrival);
                    break;

                case SpellVisualProfile.Radiant:
                    TickRadiantBolt(position, forward, side, normalized, arrival);
                    break;

                case SpellVisualProfile.Shadow:
                    SetTransform(_core, true, position, Quaternion.Euler(90f, normalized * 540f, 0f),
                        new Vector3(0.34f, 0.12f, 0.34f) * _size * bodyFade);
                    SetTransform(_shell, true, position, Quaternion.Euler(0f, -normalized * 620f, 0f),
                        new Vector3(0.48f, 0.16f, 0.48f) * _size * bodyFade);
                    TickOrbitingMotifs(position, forward, side, normalized, arrival, 0.48f, -3.5f);
                    TickRibbonTrails(position, forward, side, normalized, 0.16f, true);
                    break;

                case SpellVisualProfile.Arcane:
                    TickCrystalCluster(position, forward, side, normalized, arrival);
                    TickProjectileHalos(position, forward, normalized, arrival);
                    TickRibbonTrails(position, forward, side, normalized, 0.22f, true);
                    break;

                default:
                    SetTransform(_core, false, position, Quaternion.identity, Vector3.zero);
                    SetTransform(_shell, false, position, Quaternion.identity, Vector3.zero);
                    TickCrystalCluster(position, forward, side, normalized, arrival);
                    TickRibbonTrails(position, forward, side, normalized, 0.1f, false);
                    break;
            }

            if (arrival > 0f)
            {
                TickArrivalFlourish(forward, side, normalized, arrival);
            }

            if (_shell != null && _shell.gameObject.activeSelf)
            {
                _shell.Rotate(forward, 420f * deltaTime, Space.World);
            }
        }

        private void TickFireComet(
            Vector3 position,
            Vector3 forward,
            Vector3 side,
            float normalized,
            float arrival)
        {
            float bodyFade = 1f - (arrival * 0.88f);
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);

            // The moving wisp volume carries the entire projectile silhouette.
            // Any solid head reads as either a toy ball or a flat wedge from at
            // least one strategy-camera angle.
            SetTransform(
                _core,
                false,
                position,
                facing,
                Vector3.zero);
            SetTransform(_shell, false, position, facing, Vector3.zero);

            SetParticleEmitter(
                position - forward * _size * 0.12f,
                Quaternion.LookRotation(-forward, Vector3.up));

            for (int index = 0; index < _motifs.Length; index++)
            {
                bool active = false;
                float alternatingSide = index % 2 == 0 ? -1f : 1f;
                float tailDistance = _size * (0.28f + index * 0.15f);
                float flicker = Mathf.Sin(normalized * 34f + index * 1.7f);
                float lateralNoise = MotifSignedNoise(index, 3);
                float verticalNoise = MotifSignedNoise(index, 7);
                Vector3 offset = -forward * tailDistance;
                offset += side * _size *
                    (alternatingSide * (0.18f + (index % 3) * 0.06f) +
                     lateralNoise * 0.045f);
                offset += Vector3.up * _size *
                    (flicker * (0.14f + (index % 2) * 0.045f) +
                     verticalNoise * 0.09f);
                float taper = 1f - index / (float)(_motifs.Length + 2);
                SetTransform(
                    _motifs[index],
                    active,
                    position + offset,
                    facing * Quaternion.Euler(flicker * 8f, alternatingSide * 7f, flicker * 12f),
                    new Vector3(0.065f, 0.055f, 0.24f) * _size * taper * bodyFade);
            }

            HideTrails();
        }

        private void TickFrostLance(
            Vector3 position,
            Vector3 forward,
            Vector3 side,
            float normalized,
            float arrival)
        {
            float bodyFade = 1f - (arrival * 0.9f);
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);

            // Two nested, elongated bodies give Frostbolt a stable spear
            // silhouette, while the fins counter-rotate like cut ice.
            SetTransform(
                _shell,
                true,
                position,
                facing * Quaternion.Euler(0f, 0f, normalized * 220f),
                new Vector3(0.22f, 0.22f, 1.24f) * _size * bodyFade);
            SetTransform(
                _core,
                true,
                position + forward * _size * 0.18f,
                facing,
                new Vector3(0.1f, 0.1f, 0.92f) * _size * bodyFade);

            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * 60f + normalized * (index % 2 == 0 ? 260f : -210f);
                float radians = angle * Mathf.Deg2Rad;
                Vector3 radial = side * Mathf.Cos(radians) + Vector3.up * Mathf.Sin(radians);
                Vector3 point = position - forward * _size * (0.16f + index * 0.045f);
                point += radial * _size * (0.24f + index % 2 * 0.08f);
                SetTransform(
                    _motifs[index],
                    true,
                    point,
                    facing * Quaternion.Euler(0f, 0f, angle),
                    new Vector3(0.05f, 0.075f, 0.38f) * _size * bodyFade);
            }

            TickRibbonTrails(position - forward * _size * 0.28f, forward, side, normalized, 0.045f, false);
            SetTrailWidth(0, Mathf.Max(0.016f, _size * 0.055f));
        }

        private void TickRadiantBolt(
            Vector3 position,
            Vector3 forward,
            Vector3 side,
            float normalized,
            float arrival)
        {
            float bodyFade = 1f - (arrival * 0.9f);
            float pulse = 0.88f + Mathf.Sin(normalized * Mathf.PI * 8f) * 0.12f;
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
            bool penance = _ability == AbilityId.Penance;
            if (penance)
            {
                SetTransform(_core, false, position, facing, Vector3.zero);
                SetTransform(_shell, false, position, facing, Vector3.zero);
                HideCastHalosFrom(0);
                float beadDiameter = Mathf.Max(0.22f, _size * 0.58f);
                Vector3 middleBead = position;
                for (int index = 0; index < _motifs.Length; index++)
                {
                    bool majorBead = index < 3;
                    bool wakeFleck = index >= 3 && index < 6;
                    int laneIndex = majorBead ? index : index - 3;
                    float delay = laneIndex * 0.1f;
                    float laneTravel = Mathf.Clamp01(
                        (normalized - delay) / Mathf.Max(0.01f, 1f - delay));
                    bool active = (majorBead || wakeFleck) &&
                        normalized >= delay &&
                        laneTravel < 0.999f;
                    Vector3 bead = Vector3.Lerp(_origin, _target, laneTravel);
                    bead.y += Mathf.Sin(laneTravel * Mathf.PI) *
                        Mathf.Clamp(Vector3.Distance(_origin, _target) * 0.045f, 0.06f, 0.42f);
                    bead += side * (laneIndex - 1) * 0.18f;
                    bead += Vector3.up * (laneIndex == 1 ? 0.08f : -0.025f);
                    if (laneIndex == 1)
                    {
                        middleBead = bead;
                    }
                    if (wakeFleck)
                    {
                        bead -= forward * (0.18f + laneIndex * 0.035f);
                        bead += side * Mathf.Sin(
                            normalized * Mathf.PI * 7f + laneIndex) * 0.045f;
                    }
                    SetTransform(_motifs[index], active, bead, facing,
                        (majorBead
                            ? new Vector3(
                                beadDiameter * 0.32f,
                                beadDiameter * 0.32f,
                                beadDiameter * 1.7f)
                            : new Vector3(
                                beadDiameter * 0.14f,
                                beadDiameter * 0.14f,
                                beadDiameter * 0.56f)) * bodyFade);
                }

                SetParticleEmitter(middleBead, Quaternion.identity);
            }
            else
            {
                SetTransform(
                    _core,
                    true,
                    position + forward * _size * 0.1f,
                    facing,
                    new Vector3(0.36f, 0.36f, 0.62f) * _size * bodyFade * pulse);
                SetTransform(
                    _shell,
                    true,
                    position - forward * _size * 0.08f,
                    facing,
                    new Vector3(0.56f, 0.56f, 0.82f) * _size * bodyFade * pulse);
                TickOrbitingMotifs(position, forward, side, normalized, arrival, 0.34f, 2.25f);
                TickProjectileHalos(position, forward, normalized, arrival);
            }
            if (penance)
            {
                HideTrails();
            }
            else
            {
                TickRibbonTrails(
                    position - forward * _size * 0.24f,
                    forward,
                    side,
                    normalized,
                    0.055f,
                    false);
                SetTrailWidth(0, Mathf.Max(0.045f, _size * 0.1f));
            }
        }

        private void TickCrystalCluster(
            Vector3 position,
            Vector3 forward,
            Vector3 side,
            float normalized,
            float arrival)
        {
            float bodyFade = 1f - (arrival * 0.86f);
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float signed = index - ((_motifs.Length - 1) * 0.5f);
                float angle = (normalized * 420f) + (index * 137.5f);
                float radius = _size * (index == 0 ? 0.08f : 0.22f + (index % 2) * 0.1f);
                Vector3 offset = side * Mathf.Cos(angle * Mathf.Deg2Rad) * radius;
                offset += Vector3.up * Mathf.Sin(angle * Mathf.Deg2Rad) * radius;
                offset -= forward * Mathf.Abs(signed) * _size * 0.065f;
                Quaternion rotation = facing * Quaternion.Euler(0f, 0f, angle + signed * 12f);
                Vector3 scale = new Vector3(0.08f, 0.08f, 0.52f) * _size * bodyFade;
                if (index == 0)
                {
                    scale *= 1.25f;
                }

                SetTransform(_motifs[index], true, position + offset, rotation, scale);
            }
        }

        private void TickOrbitingMotifs(
            Vector3 position,
            Vector3 forward,
            Vector3 side,
            float normalized,
            float arrival,
            float radiusMultiplier,
            float turns)
        {
            float bodyFade = 1f - (arrival * 0.78f);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float phase = (index / (float)_motifs.Length) * Mathf.PI * 2f;
                float angle = phase + normalized * Mathf.PI * 2f * turns;
                float radius = _size * radiusMultiplier * (0.72f + (index % 2) * 0.28f);
                Vector3 offset = side * Mathf.Cos(angle) * radius;
                offset += Vector3.up * Mathf.Sin(angle) * radius;
                offset -= forward * (index + 1) * _size * 0.11f;
                Vector3 scale = new Vector3(0.13f, 0.13f, 0.32f) * _size * bodyFade;
                Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up) *
                    Quaternion.Euler(0f, 0f, angle * Mathf.Rad2Deg);
                SetTransform(_motifs[index], true, position + offset, rotation, scale);
            }
        }

        private void TickProjectileHalos(
            Vector3 position,
            Vector3 forward,
            float normalized,
            float arrival)
        {
            for (int index = 0; index < _halos.Length; index++)
            {
                _halos[index].gameObject.SetActive(false);
            }
        }

        private void TickRibbonTrails(
            Vector3 position,
            Vector3 forward,
            Vector3 side,
            float normalized,
            float amplitude,
            bool twinSpiral)
        {
            float trailLength = Mathf.Clamp(Vector3.Distance(_origin, _target) * 0.18f, 0.55f, 2.1f);
            for (int trailIndex = 0; trailIndex < _trails.Length; trailIndex++)
            {
                LineRenderer trail = _trails[trailIndex];
                trail.gameObject.SetActive(trailIndex == 0 || twinSpiral);
                if (!trail.gameObject.activeSelf)
                {
                    continue;
                }

                float phase = trailIndex * Mathf.PI;
                for (int pointIndex = 0; pointIndex < TrailPointCount; pointIndex++)
                {
                    float pointT = pointIndex / (float)(TrailPointCount - 1);
                    float wave = (normalized * 20f) - (pointT * Mathf.PI * 3f) + phase;
                    Vector3 point = position - forward * trailLength * pointT;
                    point += side * Mathf.Sin(wave) * _size * amplitude * pointT;
                    point += Vector3.up * Mathf.Cos(wave) * _size * amplitude * pointT;
                    trail.SetPosition(pointIndex, point);
                }
            }
        }

        private void TickArrivalFlourish(
            Vector3 forward,
            Vector3 side,
            float normalized,
            float arrival)
        {
            float spread = Mathf.Sin(arrival * Mathf.PI) * _size;
            for (int index = 0; index < _motifs.Length; index++)
            {
                Transform motif = _motifs[index];
                if (motif == null)
                {
                    continue;
                }

                float angle = index * Mathf.PI * 2f / _motifs.Length;
                Vector3 radial = side * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
                motif.position = _target + radial * spread - forward * (1f - arrival) * 0.3f;
                motif.localScale *= 0.75f + arrival * 0.65f;
            }

            if (_halos.Length > 0 && _halos[0] != null)
            {
                LineRenderer arrivalRing = _halos[0];
                arrivalRing.gameObject.SetActive(true);
                arrivalRing.transform.position = _target;
                arrivalRing.transform.rotation =
                    Quaternion.FromToRotation(Vector3.up, forward) *
                    Quaternion.Euler(0f, normalized * 720f, 0f);
                arrivalRing.transform.localScale = Vector3.one *
                    Mathf.Max(0.04f, spread * 0.8f);
            }
        }

        private void TickCastStart(float deltaTime, float normalized)
        {
            float gather = Mathf.Sin(normalized * Mathf.PI);
            Vector3 center = _origin;
            SetParticleEmitter(center + Vector3.up * 0.32f, Quaternion.identity);

            switch (_profile)
            {
                case SpellVisualProfile.Fire:
                    TickFireCastStart(center, normalized, gather);
                    break;
                case SpellVisualProfile.Frost:
                    TickFrostCastStart(center, normalized, gather);
                    break;
                case SpellVisualProfile.Radiant:
                    TickRadiantCastStart(center, normalized, gather, false);
                    break;
                case SpellVisualProfile.Arcane:
                    TickRadiantCastStart(center, normalized, gather, true);
                    break;
                case SpellVisualProfile.Shadow:
                    TickShadowCastStart(center, normalized, gather);
                    break;
                default:
                    TickMartialCastStart(center, normalized, gather);
                    break;
            }

            if (_core != null)
            {
                _core.Rotate(Vector3.up, deltaTime * 420f, Space.World);
            }
        }

        private void TickFireCastStart(Vector3 center, float normalized, float gather)
        {
            Vector3 focus = center + Vector3.up * (0.24f + normalized * 0.14f);
            SetTransform(
                _core,
                true,
                focus,
                Quaternion.Euler(90f, normalized * 720f, 0f),
                new Vector3(0.14f, 0.14f, 0.38f) * _size * gather);
            SetTransform(
                _shell,
                true,
                focus,
                Quaternion.Euler(20f, -normalized * 540f, 35f),
                new Vector3(0.38f, 0.16f, 0.38f) * _size * gather);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length +
                    normalized * Mathf.PI * 3f;
                float radius = _size * Mathf.Lerp(0.9f, 0.2f, normalized);
                Vector3 point = focus + Vector3.up * ((index % 3) * 0.09f);
                point += new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Vector3 inward = focus - point;
                inward.y += 0.32f;
                SetTransform(
                    _motifs[index],
                    true,
                    point,
                    Quaternion.LookRotation(inward.normalized, Vector3.up),
                    new Vector3(0.055f, 0.055f, 0.24f) * _size * gather);
            }

            SetDisc(false, center, 0f, 0f);
            SetCastHalo(
                0,
                true,
                focus,
                Quaternion.Euler(7f, normalized * 90f, -5f),
                _size * (0.18f + gather * 0.32f));
            HideCastHalosFrom(1);
        }

        private void TickFrostCastStart(Vector3 center, float normalized, float gather)
        {
            Vector3 focus = center + Vector3.up * 0.26f;
            SetTransform(
                _core,
                true,
                focus,
                Quaternion.Euler(0f, normalized * 360f, 45f),
                new Vector3(0.12f, 0.34f, 0.12f) * _size * gather);
            SetTransform(_shell, false, focus, Quaternion.identity, Vector3.zero);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float radius = _size * Mathf.Lerp(0.62f, 0.25f, normalized);
                Vector3 point = focus + radial * radius;
                point.y += (index % 2 == 0 ? -0.08f : 0.12f) * _size;
                Quaternion rotation = Quaternion.LookRotation(radial, Vector3.up) *
                    Quaternion.Euler(90f, 0f, 0f);
                SetTransform(
                    _motifs[index],
                    true,
                    point,
                    rotation,
                    new Vector3(0.065f, 0.24f, 0.065f) * _size * gather);
            }

            SetDisc(false, center, 0f, 0f);
            SetCastHalo(
                0,
                true,
                focus,
                Quaternion.Euler(5f, normalized * -80f, 3f),
                _size * (0.24f + gather * 0.28f));
            HideCastHalosFrom(1);
        }

        private void TickRadiantCastStart(
            Vector3 center,
            float normalized,
            float gather,
            bool arcane)
        {
            Vector3 focus = center + Vector3.up * 0.3f;
            SetTransform(
                _core,
                true,
                focus,
                Quaternion.Euler(0f, normalized * (arcane ? -540f : 180f), 45f),
                new Vector3(0.13f, 0.40f, 0.13f) * _size * gather);
            SetTransform(
                _shell,
                arcane,
                focus,
                Quaternion.Euler(35f, normalized * 460f, 35f),
                Vector3.one * _size * 0.26f * gather);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length;
                float radius = _size * (arcane ? 0.42f : 0.31f);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 point = focus + radial * radius * gather;
                point.y += ((index % 3) - 1) * _size * 0.16f;
                Vector3 scale = arcane
                    ? new Vector3(0.08f, 0.18f, 0.08f)
                    : new Vector3(0.07f, 0.30f, 0.07f);
                SetTransform(
                    _motifs[index],
                    true,
                    point,
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, arcane ? 45f : 0f),
                    scale * _size * gather);
            }

            SetDisc(true, center, _size * (0.16f + gather * 0.28f), 0.018f);
            RotateDisc(normalized * (arcane ? -420f : 180f));
            for (int index = 0; index < _halos.Length; index++)
            {
                SetCastHalo(
                    index,
                    index == 0,
                    focus,
                    Quaternion.Euler(arcane ? -8f : 4f, normalized * 70f, arcane ? 6f : -4f),
                    _size * (0.16f + gather * 0.22f));
            }
        }

        private void TickShadowCastStart(Vector3 center, float normalized, float gather)
        {
            Vector3 focus = center + Vector3.up * 0.22f;
            SetTransform(
                _core,
                true,
                focus,
                Quaternion.Euler(0f, normalized * -720f, 45f),
                Vector3.one * _size * 0.13f * gather);
            SetTransform(
                _shell,
                true,
                focus,
                Quaternion.Euler(0f, normalized * -520f, 0f),
                new Vector3(0.42f, 0.14f, 0.42f) * _size * gather);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length -
                    normalized * Mathf.PI * 4f;
                float radius = _size * Mathf.Lerp(0.76f, 0.12f, normalized);
                Vector3 point = focus + new Vector3(
                    Mathf.Cos(angle) * radius,
                    Mathf.Sin(angle * 2f) * _size * 0.08f,
                    Mathf.Sin(angle) * radius);
                SetTransform(
                    _motifs[index],
                    true,
                    point,
                    Quaternion.Euler(0f, angle * Mathf.Rad2Deg, 45f),
                    new Vector3(0.075f, 0.18f, 0.075f) * _size * gather);
            }

            SetDisc(false, center, 0f, 0f);
            SetCastHalo(
                0,
                true,
                focus,
                Quaternion.Euler(-9f, normalized * -110f, 7f),
                _size * (0.12f + gather * 0.36f));
            HideCastHalosFrom(1);
        }

        private void TickMartialCastStart(Vector3 center, float normalized, float gather)
        {
            bool venom = _profile == SpellVisualProfile.Venom;
            Vector3 focus = center + Vector3.up * 0.12f;
            SetTransform(
                _core,
                venom,
                focus,
                Quaternion.Euler(0f, normalized * 540f, 0f),
                new Vector3(0.18f, 0.10f, 0.18f) * _size * gather);
            SetTransform(_shell, false, focus, Quaternion.identity, Vector3.zero);
            for (int index = 0; index < _motifs.Length; index++)
            {
                bool active = index < (venom ? 3 : 2);
                float handedness = index % 2 == 0 ? -1f : 1f;
                Vector3 point = focus + new Vector3(
                    handedness * _size * (0.28f - normalized * 0.12f),
                    index * 0.035f,
                    normalized * _size * 0.16f);
                SetTransform(
                    _motifs[index],
                    active,
                    point,
                    Quaternion.Euler(12f, handedness * 18f, handedness * 58f),
                    new Vector3(0.055f, 0.055f, 0.30f) * _size * gather);
            }

            SetDisc(false, center, 0f, 0f);
            HideCastHalosFrom(0);
        }

        private void SetCastHalo(
            int index,
            bool active,
            Vector3 position,
            Quaternion rotation,
            float scale)
        {
            if (index < 0 || index >= _halos.Length || _halos[index] == null)
            {
                return;
            }

            SetTransform(
                _halos[index].transform,
                active,
                position,
                rotation,
                Vector3.one * Mathf.Max(0.001f, scale));
        }

        private void HideCastHalosFrom(int firstHiddenIndex)
        {
            for (int index = Mathf.Max(0, firstHiddenIndex); index < _halos.Length; index++)
            {
                if (_halos[index] != null)
                {
                    _halos[index].gameObject.SetActive(false);
                }
            }
        }

        private void TickImpact(float normalized)
        {
            if (_hunterShotProfile != HunterShotVisualProfile.None)
            {
                TickHunterShotImpact(normalized);
                return;
            }

            if (_motion == EffectMotion.BladeStorm)
            {
                TickBladeStormImpact(normalized);
                return;
            }

            if (_burstSilhouette == SpellBurstSilhouette.MartialContact)
            {
                TickMeleeContact(normalized);
                return;
            }

            if (UsesVolumetricAreaResolution)
            {
                TickVolumetricAreaResolution(normalized);
                return;
            }

            TickProfileBurst(
                normalized,
                _phase == EffectVisualPhase.Miss ? 0.56f : 1f,
                false);
        }

        private void TickHunterShotImpact(float normalized)
        {
            float burst = Mathf.Sin(normalized * Mathf.PI);
            bool missed = _phase == EffectVisualPhase.Miss;
            bool twin = _hunterShotProfile == HunterShotVisualProfile.TwinSerpent;
            bool heavy = _hunterShotProfile == HunterShotVisualProfile.Heavy;
            Vector3 direction = _target - _origin;
            Vector3 forward = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            if (side.sqrMagnitude <= 0.001f)
            {
                side = Vector3.right;
            }

            float visualSize = Mathf.Max(0.58f, _size);
            float separation = twin ? visualSize * 0.14f : 0f;
            Vector3 center = _target + (missed ? side * visualSize * 0.28f : Vector3.zero);
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
            SetParticleEmitter(center, facing);

            float shaftLength = visualSize * (heavy ? 1.08f : 0.86f) * burst;
            float shaftThickness = visualSize * (heavy ? 0.12f : 0.085f) * burst;
            SetTransform(
                _core,
                true,
                center - side * separation - forward * visualSize * 0.18f,
                facing,
                new Vector3(shaftThickness, shaftThickness, shaftLength));
            SetTransform(
                _shell,
                true,
                twin
                    ? center + side * separation - forward * visualSize * 0.18f
                    : center + forward * visualSize * 0.18f,
                facing,
                twin
                    ? new Vector3(shaftThickness, shaftThickness, shaftLength)
                    : new Vector3(
                        visualSize * (heavy ? 0.28f : 0.22f),
                        visualSize * (heavy ? 0.14f : 0.1f),
                        visualSize * 0.48f) * burst);

            for (int trailIndex = 0; trailIndex < _trails.Length; trailIndex++)
            {
                LineRenderer trail = _trails[trailIndex];
                if (trail == null)
                {
                    continue;
                }

                bool active = twin || trailIndex == 0 || heavy;
                trail.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                float lane = twin ? (trailIndex == 0 ? -1f : 1f) : 0f;
                float fan = heavy && trailIndex == 1 ? 0.22f : 0f;
                for (int pointIndex = 0; pointIndex < TrailPointCount; pointIndex++)
                {
                    float pointT = pointIndex / (float)(TrailPointCount - 1);
                    Vector3 point = center + side * lane * separation;
                    point += forward * Mathf.Lerp(0.42f, -0.82f, pointT) *
                        visualSize * burst;
                    point += side * fan * visualSize * (pointT - 0.5f) * burst;
                    trail.SetPosition(pointIndex, point);
                }

                trail.widthMultiplier = Mathf.Max(
                    0.018f,
                    visualSize * (twin ? 0.052f : trailIndex == 0 ? 0.07f : 0.03f) * burst);
            }

            for (int index = 0; index < _motifs.Length; index++)
            {
                bool active = index < (twin || heavy ? 6 : 4);
                float angle = index * Mathf.PI * 2f / Mathf.Max(1, _motifs.Length);
                Vector3 radial = side * Mathf.Cos(angle) + Vector3.up * Mathf.Sin(angle);
                Vector3 point = center + radial * visualSize *
                    (0.12f + normalized * (missed ? 0.28f : 0.48f));
                point -= forward * visualSize * normalized * 0.12f;
                SetTransform(
                    _motifs[index],
                    active,
                    point,
                    Quaternion.LookRotation(radial + forward * 0.2f, Vector3.up),
                    new Vector3(0.045f, 0.035f, 0.34f) * visualSize * burst);
            }

            for (int index = 0; index < _halos.Length; index++)
            {
                LineRenderer halo = _halos[index];
                if (halo == null)
                {
                    continue;
                }

                bool active = index == 0 && (heavy || twin);
                halo.gameObject.SetActive(active);
                if (active)
                {
                    halo.transform.position = center;
                    halo.transform.rotation = Quaternion.FromToRotation(Vector3.up, forward);
                    halo.transform.localScale = Vector3.one * visualSize *
                        (0.08f + normalized * (missed ? 0.25f : 0.42f));
                }
            }
        }

        private void TickMeleeContact(float normalized)
        {
            float slash = Mathf.Sin(normalized * Mathf.PI);
            bool crosscut = _ability == AbilityId.Mutilate ||
                _ability == AbilityId.Eviscerate ||
                _ability == AbilityId.QuickStrike;
            float radius = _size * (0.34f + normalized * 0.16f);
            Vector3 attackForward = _target - _origin;
            attackForward.y = 0f;
            attackForward = attackForward.sqrMagnitude > 0.001f
                ? attackForward.normalized
                : Vector3.forward;
            Vector3 attackSide = Vector3.Cross(Vector3.up, attackForward).normalized;
            // Target points already sit at torso height; adding another body-height
            // offset put the old crosscut above the opponent's head.
            Vector3 contactCenter = _target + Vector3.up * 0.02f;
            SetParticleEmitter(contactCenter, Quaternion.identity);

            // Compact diagonal blade streaks cross at the target. This keeps
            // weapon contact readable without covering the combatant in a
            // giant white arch.
            for (int trailIndex = 0; trailIndex < _trails.Length; trailIndex++)
            {
                LineRenderer trail = _trails[trailIndex];
                bool active = trailIndex == 0 || crosscut;
                trail.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                float handedness = trailIndex == 0 ? 1f : -1f;
                trail.widthMultiplier = Mathf.Max(
                    0.018f,
                    _size * (trailIndex == 0 ? 0.065f : 0.05f));
                for (int pointIndex = 0; pointIndex < TrailPointCount; pointIndex++)
                {
                    float pointT = pointIndex / (float)(TrailPointCount - 1);
                    float signed = pointT * 2f - 1f;
                    Vector3 point = contactCenter + attackSide * signed * radius;
                    point.y += -signed * radius * 0.82f * handedness;
                    point.y += Mathf.Sin(pointT * Mathf.PI) * radius * 0.16f;
                    point -= attackForward * Mathf.Abs(signed) * _size * 0.06f;
                    trail.SetPosition(pointIndex, point);
                }
            }

            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length;
                float splashRadius = _profile == SpellVisualProfile.Venom
                    ? _size * normalized * 0.24f
                    : _size * normalized * 0.1f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0.3f, Mathf.Sin(angle));
                Vector3 point = _target + Vector3.up * (index % 2 * 0.08f);
                point += radial * splashRadius;
                SetTransform(
                    _motifs[index],
                    _profile == SpellVisualProfile.Venom || index < 2,
                    point,
                    Quaternion.LookRotation(radial.normalized, Vector3.up),
                    new Vector3(0.045f, 0.045f, 0.22f) * _size * slash);
            }

            // Envenom is deliberately a tight contact splash instead of a nova.
            if (_profile == SpellVisualProfile.Venom)
            {
                SetTransform(
                    _core,
                    true,
                    _target,
                    Quaternion.identity,
                    new Vector3(0.24f, 0.18f, 0.24f) * _size * slash);
            }
        }

        public bool UsesCloseContactVisual =>
            IsPlaying &&
            _motion == EffectMotion.Burst &&
            _burstSilhouette == SpellBurstSilhouette.MartialContact;

        private void TickTether(float normalized)
        {
            Vector3 direction = _target - _origin;
            Vector3 forward = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            SetParticleEmitter(
                Vector3.Lerp(_origin, _target, normalized),
                Quaternion.LookRotation(forward, Vector3.up));
            float pulse = 0.78f + Mathf.Sin(normalized * Mathf.PI * 4f) * 0.18f;
            for (int trailIndex = 0; trailIndex < _trails.Length; trailIndex++)
            {
                LineRenderer trail = _trails[trailIndex];
                trail.gameObject.SetActive(true);
                float phase = trailIndex * Mathf.PI;
                for (int pointIndex = 0; pointIndex < TrailPointCount; pointIndex++)
                {
                    float pointT = pointIndex / (float)(TrailPointCount - 1);
                    float wave = pointT * Mathf.PI * 4f + normalized * Mathf.PI * 5f + phase;
                    Vector3 point = Vector3.Lerp(_origin, _target, pointT);
                    point += side * Mathf.Sin(wave) * _size * 0.34f * pulse;
                    point += Vector3.up * Mathf.Cos(wave) * _size * 0.15f * pulse;
                    trail.SetPosition(pointIndex, point);
                }
            }

            SetTransform(_core, true, _target, Quaternion.identity,
                Vector3.one * _size * 0.32f * pulse);
        }

        private void TickShadowBrand(float normalized)
        {
            float visualSize = Mathf.Max(0.62f, _size);
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized * 2.4f));
            float pulse = 0.88f + Mathf.Sin(normalized * Mathf.PI * 6f) * 0.12f;
            Vector3 direction = _target - _origin;
            Vector3 forward = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            SetParticleEmitter(_target, Quaternion.identity);

            for (int trailIndex = 0; trailIndex < _trails.Length; trailIndex++)
            {
                LineRenderer trail = _trails[trailIndex];
                bool active = trailIndex == 0 || _ability == AbilityId.VampiricTouch;
                trail.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                float phase = trailIndex * Mathf.PI;
                for (int pointIndex = 0; pointIndex < TrailPointCount; pointIndex++)
                {
                    float pointT = pointIndex / (float)(TrailPointCount - 1);
                    float wave = (pointT * Mathf.PI * 3f) +
                        (normalized * Mathf.PI * 5f) + phase;
                    Vector3 point = Vector3.Lerp(_origin, _target, pointT);
                    point += side * Mathf.Sin(wave) * visualSize * 0.08f * pointT;
                    point.y += Mathf.Sin(pointT * Mathf.PI) * visualSize * 0.12f;
                    trail.SetPosition(pointIndex, point);
                }
            }

            for (int index = 0; index < _halos.Length; index++)
            {
                LineRenderer halo = _halos[index];
                bool active = index < 2;
                halo.gameObject.SetActive(active);
                if (!active)
                {
                    continue;
                }

                halo.transform.position = _target;
                halo.transform.rotation = Quaternion.Euler(
                    90f,
                    normalized * (index == 0 ? -420f : 300f),
                    index * 45f);
                halo.transform.localScale = Vector3.one * visualSize *
                    (0.46f + index * 0.18f) * reveal * pulse;
            }

            for (int index = 0; index < _motifs.Length; index++)
            {
                bool active = _ability == AbilityId.DevouringPlague
                    ? index < 3
                    : _ability == AbilityId.ShadowWordPain
                        ? index < 3
                        : index < 2;
                float angle = index * Mathf.PI * 2f / Mathf.Max(1, active ? 3 : 1) +
                    normalized * (_ability == AbilityId.DevouringPlague ? -5f : 2f);
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 point = _target + radial * visualSize * 0.48f;
                point.y += _ability == AbilityId.ShadowWordPain
                    ? 0.38f + index * 0.12f
                    : 0.08f + index * 0.16f;
                Vector3 scale = _ability == AbilityId.DevouringPlague
                    ? Vector3.one * visualSize * 0.14f * reveal
                    : new Vector3(0.08f, 0.36f, 0.08f) * visualSize * reveal;
                SetTransform(
                    _motifs[index],
                    active,
                    point,
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, index % 2 == 0 ? 28f : -28f),
                    scale);
            }

            SetTransform(
                _core,
                true,
                _target,
                Quaternion.Euler(0f, normalized * -540f, 45f),
                Vector3.one * visualSize * 0.18f * reveal * pulse);
        }

        private void TickMindChannel(float normalized)
        {
            Vector3 direction = _target - _origin;
            Vector3 forward = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            float pulse = 0.82f + Mathf.Sin(normalized * Mathf.PI * 8f) * 0.18f;
            SetParticleEmitter(Vector3.Lerp(_origin, _target, 0.72f), Quaternion.identity);
            for (int trailIndex = 0; trailIndex < _trails.Length; trailIndex++)
            {
                LineRenderer trail = _trails[trailIndex];
                trail.gameObject.SetActive(true);
                trail.widthMultiplier = Mathf.Max(0.02f, _size * (trailIndex == 0 ? 0.12f : 0.075f));
                for (int pointIndex = 0; pointIndex < TrailPointCount; pointIndex++)
                {
                    float pointT = pointIndex / (float)(TrailPointCount - 1);
                    float step = pointIndex % 2 == 0 ? -1f : 1f;
                    float handedness = trailIndex == 0 ? 1f : -1f;
                    Vector3 point = Vector3.Lerp(_origin, _target, pointT);
                    point += side * step * handedness * _size * 0.2f *
                        Mathf.Sin(pointT * Mathf.PI);
                    point.y += Mathf.Sin((pointT + normalized) * Mathf.PI * 4f) *
                        _size * 0.08f;
                    trail.SetPosition(pointIndex, point);
                }
            }

            for (int index = 0; index < _motifs.Length; index++)
            {
                float pointT = (index + 1f) / (_motifs.Length + 1f);
                Vector3 point = Vector3.Lerp(_origin, _target, pointT);
                point += side * (index % 2 == 0 ? -1f : 1f) * _size * 0.18f;
                SetTransform(
                    _motifs[index],
                    true,
                    point,
                    Quaternion.LookRotation(forward, Vector3.up) *
                        Quaternion.Euler(0f, 0f, normalized * 480f + index * 60f),
                    new Vector3(0.07f, 0.07f, 0.3f) * _size * pulse);
            }

            SetTransform(
                _core,
                true,
                _target,
                Quaternion.Euler(0f, normalized * 620f, 45f),
                new Vector3(0.36f, 0.52f, 0.36f) * _size * pulse);
            for (int index = 0; index < _halos.Length; index++)
            {
                LineRenderer halo = _halos[index];
                halo.gameObject.SetActive(index < 2);
                if (index < 2)
                {
                    halo.transform.position = _target;
                    halo.transform.rotation = Quaternion.Euler(90f, normalized * -520f, index * 55f);
                    halo.transform.localScale = Vector3.one * _size * (0.42f + index * 0.18f) * pulse;
                }
            }
        }

        private void TickHunterArrow(float normalized)
        {
            Vector3 direction = _target - _origin;
            float distance = direction.magnitude;
            Vector3 forward = distance > 0.001f ? direction / distance : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            if (side.sqrMagnitude <= 0.001f)
            {
                side = Vector3.right;
            }

            float visualSize = Mathf.Max(0.3f, _size);
            Vector3 position = EvaluateHunterShotPath(normalized, distance);
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
            float arrival = Mathf.InverseLerp(0.88f, 1f, normalized);
            float fade = 1f - arrival * 0.9f;
            bool heavy = _hunterShotProfile == HunterShotVisualProfile.Heavy;
            bool twin = _hunterShotProfile == HunterShotVisualProfile.TwinSerpent;
            bool precision = _hunterShotProfile == HunterShotVisualProfile.Precision;
            bool icy = _hunterShotProfile == HunterShotVisualProfile.Frost;
            bool arcane = _hunterShotProfile == HunterShotVisualProfile.Arcane;
            float separation = twin ? visualSize * 0.16f : 0f;
            float shaftLength = heavy
                ? 2.55f
                : twin
                    ? 2.08f
                    : precision
                        ? 2.35f
                        : 1.92f;
            float shaftThickness = heavy
                ? 0.15f
                : twin
                    ? 0.105f
                    : precision
                        ? 0.145f
                        : 0.12f;

            SetParticleEmitter(position - forward * visualSize * 0.46f, facing);
            SetTransform(
                _core,
                true,
                position - side * separation,
                facing,
                new Vector3(shaftThickness, shaftThickness, shaftLength) *
                    visualSize * fade);
            SetTransform(
                _shell,
                true,
                twin
                    ? position + side * separation
                    : position + forward * visualSize *
                        (heavy || precision ? 0.82f : 0.64f),
                facing * Quaternion.Euler(
                    0f,
                    0f,
                    twin ? -4f : icy ? normalized * 260f : 0f),
                twin
                    ? new Vector3(shaftThickness, shaftThickness, shaftLength) *
                        visualSize * fade
                    : new Vector3(
                        heavy ? 0.36f : precision ? 0.37f : 0.29f,
                        heavy ? 0.17f : precision ? 0.18f : 0.135f,
                        heavy ? 0.74f : precision ? 0.82f : 0.58f) *
                        visualSize * fade);

            for (int index = 0; index < _motifs.Length; index++)
            {
                bool active = index < (heavy || twin || icy || arcane ? 6 : 4);
                float handedness = index % 2 == 0 ? -1f : 1f;
                bool front = twin ? index % 3 == 0 : index < 2;
                float lane = twin ? (index < 3 ? -1f : 1f) : 0f;
                Vector3 point = position + side * lane * separation;
                point += forward * visualSize * (front ? 0.64f : -0.72f);
                point += side * handedness * visualSize *
                    (front ? 0.1f : twin ? 0.085f : 0.13f);
                point.y += handedness * visualSize *
                    (icy ? 0.09f : front ? 0.035f : 0.065f);
                float roll = handedness * (front ? 34f : 58f);
                float yaw = handedness * (front ? 17f : -12f);
                SetTransform(
                    _motifs[index],
                    active,
                    point,
                    facing * Quaternion.Euler(0f, yaw, roll),
                    new Vector3(
                        front ? 0.07f : 0.095f,
                        icy ? 0.105f : 0.045f,
                        front ? 0.46f : 0.31f) * visualSize * fade);
            }

            TickHunterShotTrails(
                distance,
                forward,
                side,
                normalized,
                separation,
                heavy,
                twin);
        }

        private Vector3 EvaluateHunterShotPath(float normalized, float distance)
        {
            Vector3 position = Vector3.Lerp(_origin, _target, normalized);
            position.y += Mathf.Sin(normalized * Mathf.PI) *
                Mathf.Clamp(distance * 0.014f, 0.018f, 0.14f);
            return position;
        }

        private void TickHunterShotTrails(
            float distance,
            Vector3 forward,
            Vector3 side,
            float normalized,
            float separation,
            bool heavy,
            bool twin)
        {
            float travelledDistance = distance * Mathf.Clamp01(normalized);
            bool precision = _hunterShotProfile == HunterShotVisualProfile.Precision;
            float maximumTrailLength = heavy
                ? 4.6f
                : twin
                    ? 4.1f
                    : precision
                        ? 4f
                        : 3.5f;
            float visibleTrailLength = Mathf.Min(travelledDistance, maximumTrailLength);
            float history = distance > 0.001f
                ? visibleTrailLength / distance
                : 0f;
            float visualSize = Mathf.Max(0.3f, _size);
            for (int trailIndex = 0; trailIndex < _trails.Length; trailIndex++)
            {
                LineRenderer trail = _trails[trailIndex];
                if (trail == null)
                {
                    continue;
                }

                trail.gameObject.SetActive(true);
                float lane = twin ? (trailIndex == 0 ? -1f : 1f) : 0f;
                float verticalInset = !twin && trailIndex == 1
                    ? visualSize * 0.025f
                    : 0f;
                for (int pointIndex = 0; pointIndex < TrailPointCount; pointIndex++)
                {
                    float pointT = pointIndex / (float)(TrailPointCount - 1);
                    float sample = Mathf.Max(0f, normalized - history * pointT);
                    Vector3 point = EvaluateHunterShotPath(sample, distance);
                    point += side * lane * separation;
                    point += Vector3.up * verticalInset;
                    trail.SetPosition(pointIndex, point);
                }

                float width = twin
                    ? visualSize * 0.075f
                    : trailIndex == 0
                        ? visualSize *
                            (heavy ? 0.135f : precision ? 0.14f : 0.105f)
                        : visualSize *
                            (heavy ? 0.055f : precision ? 0.065f : 0.045f);
                trail.widthMultiplier = Mathf.Max(
                    trailIndex == 0
                        ? precision ? 0.034f : 0.026f
                        : precision ? 0.02f : 0.015f,
                    width);
            }
        }

        private void TickTrap(float normalized)
        {
            float reveal = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);
            float radius = _size * (0.32f + normalized * 0.68f);
            SetParticleEmitter(_target + Vector3.up * 0.06f, Quaternion.identity);
            SetDisc(false, _target, 0f, 0f);
            for (int index = 0; index < _halos.Length; index++)
            {
                LineRenderer halo = _halos[index];
                halo.gameObject.SetActive(false);
            }

            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float height = 0.18f + (index % 4) * 0.12f;
                Vector3 point = _target + radial * radius *
                    (0.38f + (index % 3) * 0.16f) + Vector3.up * height;
                SetTransform(
                    _motifs[index],
                    true,
                    point,
                    Quaternion.LookRotation((radial * 0.7f + Vector3.up).normalized, Vector3.up),
                    new Vector3(0.07f, 0.07f, 0.42f + (index % 3) * 0.1f) * _size * reveal);
            }
        }

        private void TickFacetedWard(float deltaTime, float normalized)
        {
            float pulse = 0.92f + Mathf.Sin(normalized * Mathf.PI * 5f) * 0.08f;
            bool blades = _ability == AbilityId.Deterrence;
            Vector3 center = _target;
            SetParticleEmitter(center, Quaternion.identity);
            SetTransform(
                _shell,
                true,
                center,
                Quaternion.Euler(0f, normalized * (blades ? 220f : -140f), 45f),
                new Vector3(0.76f, 1.05f, 0.76f) * _size * pulse);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length +
                    normalized * (blades ? 3.2f : -1.8f);
                Vector3 point = center +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _size * 0.68f;
                point.y += (index % 3 - 1) * _size * 0.28f;
                Vector3 scale = blades
                    ? new Vector3(0.075f, 0.48f, 0.075f)
                    : new Vector3(0.13f, 0.4f, 0.06f);
                SetTransform(
                    _motifs[index],
                    true,
                    point,
                    Quaternion.Euler(blades ? 0f : 45f, -angle * Mathf.Rad2Deg, blades ? 0f : 45f),
                    scale * _size * pulse);
            }

            for (int index = 0; index < _halos.Length; index++)
            {
                LineRenderer halo = _halos[index];
                halo.gameObject.SetActive(true);
                halo.transform.position = center + Vector3.up * ((index - 1) * _size * 0.34f);
                halo.transform.rotation = Quaternion.Euler(0f, normalized * (index % 2 == 0 ? 240f : -180f), 0f);
                halo.transform.localScale = Vector3.one * _size * (0.5f + index * 0.12f) * pulse;
            }

            if (_shell != null)
            {
                _shell.Rotate(Vector3.up, deltaTime * (blades ? 160f : -90f), Space.World);
            }
        }

        private void TickHorror(float normalized)
        {
            float spike = Mathf.Sin(normalized * Mathf.PI);
            Vector3 center = _target + Vector3.up * _size * 0.32f;
            SetParticleEmitter(center, Quaternion.identity);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 point = center + radial * _size * 0.5f;
                point.y += index % 2 == 0 ? _size * 0.32f : 0f;
                SetTransform(
                    _motifs[index],
                    true,
                    point,
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, index % 2 == 0 ? 20f : -20f),
                    new Vector3(0.09f, 0.48f, 0.09f) * _size * spike);
            }

            for (int index = 0; index < _halos.Length; index++)
            {
                LineRenderer halo = _halos[index];
                halo.gameObject.SetActive(index < 2);
                if (index < 2)
                {
                    halo.transform.position = center;
                    halo.transform.rotation = Quaternion.Euler(0f, normalized * (index == 0 ? 460f : -380f), 0f);
                    halo.transform.localScale = Vector3.one * _size * (0.48f + index * 0.2f) * spike;
                }
            }

            SetTransform(
                _core,
                true,
                center,
                Quaternion.Euler(45f, normalized * -620f, 45f),
                Vector3.one * _size * 0.2f * spike);
        }

        private void TickTargetMark(float normalized)
        {
            float reveal = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized * 2f));
            float pulse = 0.92f + Mathf.Sin(normalized * Mathf.PI * 5f) * 0.08f;
            Vector3 center = _target + Vector3.up * 0.16f;
            for (int index = 0; index < _halos.Length; index++)
            {
                LineRenderer halo = _halos[index];
                halo.gameObject.SetActive(true);
                halo.transform.position = center;
                halo.transform.rotation = Quaternion.Euler(
                    index == 0 ? 90f : 0f,
                    normalized * (index == 0 ? 120f : -160f),
                    index * 60f);
                halo.transform.localScale = Vector3.one * _size *
                    (0.42f + index * 0.16f) * reveal * pulse;
            }

            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length;
                Vector3 point = center +
                    new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _size * 0.66f;
                SetTransform(
                    _motifs[index],
                    index < 4,
                    point,
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 45f),
                    new Vector3(0.07f, 0.24f, 0.07f) * _size * reveal);
            }
        }

        private void TickCommand(float normalized)
        {
            Vector3 direction = _target - _origin;
            direction.y = 0f;
            Vector3 forward = direction.sqrMagnitude > 0.001f
                ? direction.normalized
                : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            bool stay = _ability == AbilityId.PetStay;
            bool follow = _ability == AbilityId.PetFollow;
            SetParticleEmitter(_origin + Vector3.up * 0.12f, Quaternion.identity);
            SetDisc(stay, _origin, _size * (0.42f + normalized * 0.34f), 0.02f);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float pointT = (index + 1f) / (_motifs.Length + 1f);
                Vector3 point;
                Quaternion rotation;
                if (stay)
                {
                    float angle = index * Mathf.PI * 2f / _motifs.Length;
                    point = _origin + new Vector3(Mathf.Cos(angle), 0.08f, Mathf.Sin(angle)) * _size * 0.62f;
                    rotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 45f);
                }
                else
                {
                    point = Vector3.Lerp(_origin, _target, pointT);
                    point += side * (index % 2 == 0 ? -0.12f : 0.12f) * _size;
                    rotation = Quaternion.LookRotation(follow ? -forward : forward, Vector3.up);
                }

                SetTransform(
                    _motifs[index],
                    true,
                    point,
                    rotation,
                    new Vector3(0.08f, 0.08f, 0.34f) * _size * pulse);
            }
        }

        private void TickVolley(float normalized)
        {
            Vector3 direction = _target - _origin;
            float distance = direction.magnitude;
            float visualSize = Mathf.Max(0.48f, _size);
            if (_ability == AbilityId.RapidFire || distance <= 0.001f)
            {
                float burst = Mathf.Sin(normalized * Mathf.PI);
                for (int index = 0; index < _motifs.Length; index++)
                {
                    float angle = index * Mathf.PI * 2f / _motifs.Length;
                    Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                    Vector3 point = _origin + radial * visualSize *
                        (0.3f + normalized * 0.55f);
                    point.y += 0.08f + index % 2 * visualSize * 0.22f;
                    SetTransform(
                        _motifs[index],
                        true,
                        point,
                        Quaternion.LookRotation(radial, Vector3.up),
                        new Vector3(0.065f, 0.065f, 0.72f) * visualSize * burst);
                }

                SetDisc(true, _origin, visualSize * (0.42f + normalized * 0.32f), 0.02f);
                RotateDisc(normalized * 420f);
                return;
            }

            Vector3 forward = distance > 0.001f ? direction / distance : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            Quaternion facing = Quaternion.LookRotation(forward, Vector3.up);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float lane = (index % 3) - 1f;
                float delay = (index / 3) * 0.18f;
                float travel = Mathf.Clamp01((normalized - delay) / Mathf.Max(0.01f, 1f - delay));
                Vector3 point = Vector3.Lerp(_origin, _target, travel);
                point += side * lane * visualSize * 0.28f;
                point.y += (index / 3) * visualSize * 0.16f;
                SetTransform(
                    _motifs[index],
                    travel > 0f && travel < 1f,
                    point,
                    facing,
                    new Vector3(0.065f, 0.065f, 0.72f) * visualSize);
            }

            TickRibbonTrails(_target, forward, side, normalized, 0.045f, true);
            SetTrailWidth(0, Mathf.Max(0.016f, visualSize * 0.045f));
            SetTrailWidth(1, Mathf.Max(0.012f, visualSize * 0.03f));
        }

        private void TickBladeStorm(float normalized)
        {
            Vector3 center = _target + Vector3.up * 0.14f;
            float attack = Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.02f, 0.38f, normalized));
            float recovery = 1f - Mathf.SmoothStep(
                0f,
                1f,
                Mathf.InverseLerp(0.72f, 1f, normalized));
            float envelope = attack * recovery;
            float turn = normalized * Mathf.PI * 4.4f;
            SetParticleEmitter(center, Quaternion.identity);
            SetTransform(_core, false, center, Quaternion.identity, Vector3.zero);
            SetTransform(_shell, false, center, Quaternion.identity, Vector3.zero);
            SetDisc(false, center, 0f, 0f);
            HideCastHalosFrom(0);

            for (int index = 0; index < _motifs.Length; index++)
            {
                float lane = index % 2 == 0 ? 0f : 1f;
                float angle = index * Mathf.PI * 2f / _motifs.Length +
                    turn * (lane > 0f ? -0.82f : 1f);
                Vector3 radial = new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                float radius = _size * Mathf.Lerp(
                    0.16f + lane * 0.06f,
                    0.64f + lane * 0.13f,
                    attack);
                Vector3 point = center + radial * radius;
                point.y += _size *
                    (0.03f + lane * 0.11f + Mathf.Sin(angle * 2f) * 0.035f);
                Quaternion rotation = Quaternion.LookRotation(
                    tangent + (Vector3.up * (lane > 0f ? -0.08f : 0.08f)),
                    Vector3.up) * Quaternion.Euler(0f, 0f, lane > 0f ? 24f : -18f);
                SetTransform(
                    _motifs[index],
                    envelope > 0.002f,
                    point,
                    rotation,
                    new Vector3(0.04f, 0.022f, 0.36f + lane * 0.08f) *
                        _size * envelope);
            }

            for (int trailIndex = 0; trailIndex < _trails.Length; trailIndex++)
            {
                LineRenderer trail = _trails[trailIndex];
                if (trail == null)
                {
                    continue;
                }

                trail.gameObject.SetActive(envelope > 0.002f);
                float trailRadius = _size * (0.48f + trailIndex * 0.17f) * attack;
                float handedness = trailIndex == 0 ? 1f : -1f;
                for (int pointIndex = 0; pointIndex < TrailPointCount; pointIndex++)
                {
                    float history = pointIndex / (float)(TrailPointCount - 1);
                    float angle = turn * handedness - history * 1.7f * handedness;
                    Vector3 point = center + new Vector3(
                        Mathf.Cos(angle),
                        0f,
                        Mathf.Sin(angle)) * trailRadius;
                    point.y += _size * (0.04f + trailIndex * 0.1f);
                    trail.SetPosition(pointIndex, point);
                }

                trail.widthMultiplier = Mathf.Max(
                    0.015f,
                    _size * (trailIndex == 0 ? 0.038f : 0.026f) * envelope);
            }
        }

        private void TickBladeStormImpact(float normalized)
        {
            Vector3 center = _target + Vector3.up * 0.34f;
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            float radius = _size * (0.08f + normalized * 0.34f);
            SetParticleEmitter(center, Quaternion.identity);
            SetTransform(_core, false, center, Quaternion.identity, Vector3.zero);
            SetTransform(_shell, false, center, Quaternion.identity, Vector3.zero);
            SetDisc(false, center, 0f, 0f);
            HideCastHalosFrom(0);
            HideTrails();

            for (int index = 0; index < _motifs.Length; index++)
            {
                bool active = index < 4;
                float angle = index * Mathf.PI * 0.5f + normalized * 1.2f;
                Vector3 radial = new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                Vector3 point = center + radial * radius;
                point.y += (index % 2 == 0 ? -0.08f : 0.08f) * _size;
                SetTransform(
                    _motifs[index],
                    active,
                    point,
                    Quaternion.LookRotation(tangent, Vector3.up) *
                        Quaternion.Euler(0f, 0f, index % 2 == 0 ? 32f : -32f),
                    new Vector3(0.035f, 0.02f, 0.28f) * _size * pulse);
            }
        }

        private void TickDash(float normalized)
        {
            Vector3 position = Vector3.Lerp(_origin, _target, normalized);
            Vector3 forward = (_target - _origin).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;
            SetParticleEmitter(position, Quaternion.LookRotation(forward, Vector3.up));
            TickRibbonTrails(position, forward, side, normalized, 0.3f, true);
            TickOrbitingMotifs(position, forward, side, normalized, normalized, 0.8f, 2.5f);
        }

        private void TickShield(float deltaTime, float normalized)
        {
            float pulse = 0.9f + Mathf.Sin(normalized * Mathf.PI * 3f) * 0.1f;
            SetParticleEmitter(_target + Vector3.up * 0.78f, Quaternion.identity);
            SetTransform(_shell, true, _target + Vector3.up * 0.78f,
                Quaternion.Euler(0f, normalized * 160f, 0f),
                new Vector3(1f, 1.32f, 1f) * _size * pulse);
            for (int index = 0; index < _halos.Length; index++)
            {
                LineRenderer halo = _halos[index];
                halo.gameObject.SetActive(true);
                halo.transform.position = _target + Vector3.up * (0.14f + index * 0.48f);
                halo.transform.rotation = Quaternion.Euler(0f, normalized * (180f + index * 70f), 0f);
                halo.transform.localScale = Vector3.one * _size * (0.64f + index * 0.1f) * pulse;
            }

            TickShieldMotifs(normalized, pulse);
            _shell.Rotate(Vector3.up, deltaTime * 80f, Space.World);
        }

        private void TickShieldMotifs(float normalized, float pulse)
        {
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length + normalized * Mathf.PI;
                Vector3 point = _target + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * _size * 0.76f;
                point.y += 0.22f + (index % 3) * 0.42f;
                SetTransform(_motifs[index], true, point,
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 45f),
                    new Vector3(0.08f, 0.32f, 0.12f) * _size * pulse);
            }
        }

        private void TickArea(float normalized)
        {
            if (_ability == AbilityId.BlastWave)
            {
                TickBlastWaveVolume(normalized);
                return;
            }

            if (_ability == AbilityId.FrostNova)
            {
                TickFrostNovaVolume(normalized);
                return;
            }

            TickProfileBurst(normalized, 1f, true);
            SetDisc(false, _target, 0f, 0f);
            HideCastHalosFrom(0);
        }

        private void TickBlastWaveVolume(float normalized)
        {
            Vector3 center = _target + Vector3.up * 0.035f;
            SetParticleEmitter(center + Vector3.up * 0.08f, Quaternion.identity);
            SetTransform(_core, false, center, Quaternion.identity, Vector3.zero);
            SetTransform(_shell, false, center, Quaternion.identity, Vector3.zero);
            SetDisc(false, center, 0f, 0f);

            for (int index = 0; index < _motifs.Length; index++)
            {
                float phaseStart = index * 0.055f;
                float localPhase = Mathf.Clamp01(
                    (normalized - phaseStart) / (1f - phaseStart));
                float reveal = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(localPhase / 0.15f));
                float fade = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.58f, 1f, localPhase));
                float envelope = reveal * fade;
                bool active = index < 3 &&
                    normalized > phaseStart && envelope > 0.002f;

                float angle = index * 2.399963f +
                    MotifSignedNoise(index, 101) * 0.54f;
                Vector3 radial = new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                float travel = Mathf.SmoothStep(0f, 1f, localPhase);
                float radialTier = 0.62f + (index % 5) * 0.105f +
                    MotifSignedNoise(index, 103) * 0.065f;
                float reach = _size * Mathf.Lerp(0.08f, 0.96f, travel) *
                    radialTier;
                float wander = MotifSignedNoise(index, 107) *
                    _size * 0.095f * travel;
                float lift = _size *
                    (0.025f +
                     travel * (0.24f + ((index * 3) % 5) * 0.1f) +
                     Mathf.Sin(localPhase * Mathf.PI) *
                     (0.11f + ((index * 5) % 3) * 0.05f));
                Vector3 point = center + radial * reach + tangent * wander +
                    Vector3.up * lift;
                float tangentBias = (index & 1) == 0 ? -1f : 1f;
                tangentBias *= 0.34f + MotifSignedNoise(index, 113) * 0.04f;
                Vector3 axis = (
                    radial * (0.5f + MotifSignedNoise(index, 109) * 0.12f) +
                    tangent * tangentBias +
                    Vector3.up * (0.78f + (index % 3) * 0.08f)).normalized;
                Quaternion rotation = Quaternion.LookRotation(axis, tangent) *
                    Quaternion.Euler(0f, 0f, MotifSignedNoise(index, 127) * 24f);
                float width = 0.032f + (index % 3) * 0.008f;
                float length = 0.11f + (index % 3) * 0.035f;
                SetTransform(
                    _motifs[index],
                    active,
                    point,
                    rotation,
                    new Vector3(width, width * 0.82f, length) *
                        _size * envelope);
            }

            HideCastHalosFrom(0);
            HideTrails();
        }

        private void TickFrostNovaVolume(float normalized)
        {
            Vector3 center = _target + Vector3.up * 0.028f;
            SetParticleEmitter(center + Vector3.up * 0.055f, Quaternion.identity);
            SetTransform(_core, false, center, Quaternion.identity, Vector3.zero);
            SetTransform(_shell, false, center, Quaternion.identity, Vector3.zero);
            SetDisc(false, center, 0f, 0f);

            for (int index = 0; index < _motifs.Length; index++)
            {
                float phaseStart = index * 0.055f;
                float localPhase = Mathf.Clamp01(
                    (normalized - phaseStart) / (1f - phaseStart));
                float reveal = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(localPhase / 0.18f));
                float fade = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.67f, 1f, localPhase));
                float envelope = reveal * fade;
                bool active = index < 6 &&
                    normalized > phaseStart && envelope > 0.002f;

                float angle = index * 2.399963f +
                    MotifSignedNoise(index, 131) * 0.66f;
                Vector3 radial = new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                float travel = Mathf.SmoothStep(0f, 1f, localPhase);
                float radialTier = 0.56f + (index % 5) * 0.105f +
                    MotifSignedNoise(index, 137) * 0.07f;
                float reach = _size * Mathf.Lerp(0.06f, 0.92f, travel) *
                    radialTier;
                float wander = MotifSignedNoise(index, 139) *
                    _size * 0.07f * travel;
                float lift = _size *
                    (0.02f +
                     travel * (0.14f + ((index * 3) % 5) * 0.11f) +
                     Mathf.Sin(localPhase * Mathf.PI) *
                     (0.08f + ((index * 5) % 4) * 0.045f));
                Vector3 point = center + radial * reach + tangent * wander +
                    Vector3.up * lift;
                float tangentBias = (index & 1) == 0 ? 1f : -1f;
                tangentBias *= 0.32f + MotifSignedNoise(index, 151) * 0.03f;
                Vector3 axis = (
                    radial * (0.43f + MotifSignedNoise(index, 149) * 0.1f) +
                    tangent * tangentBias +
                    Vector3.up * (0.84f + (index % 2) * 0.09f)).normalized;
                Quaternion rotation = Quaternion.LookRotation(axis, tangent) *
                    Quaternion.Euler(0f, 0f, MotifSignedNoise(index, 157) * 18f);
                float width = 0.035f + (index % 3) * 0.009f;
                float length = 0.14f + (index % 4) * 0.038f;
                SetTransform(
                    _motifs[index],
                    active,
                    point,
                    rotation,
                    new Vector3(width, width * 0.88f, length) *
                        _size * envelope);
            }

            HideCastHalosFrom(0);
            HideTrails();
        }

        private void TickVolumetricAreaResolution(float normalized)
        {
            Vector3 center = _target + Vector3.up * 0.03f;
            SetParticleEmitter(center + Vector3.up * 0.06f, Quaternion.identity);
            SetTransform(_core, false, center, Quaternion.identity, Vector3.zero);
            SetTransform(_shell, false, center, Quaternion.identity, Vector3.zero);
            SetDisc(false, center, 0f, 0f);
            float intensity = _phase == EffectVisualPhase.Miss ? 0.56f : 1f;

            for (int index = 0; index < _motifs.Length; index++)
            {
                bool included = index <
                    (_ability == AbilityId.BlastWave ? 2 : 4);
                float phaseStart = (index % 3) * 0.045f;
                float localPhase = Mathf.Clamp01(
                    (normalized - phaseStart) / (1f - phaseStart));
                float reveal = Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.Clamp01(localPhase / 0.2f));
                float fade = 1f - Mathf.SmoothStep(
                    0f,
                    1f,
                    Mathf.InverseLerp(0.54f, 1f, localPhase));
                float envelope = reveal * fade * intensity;
                bool active = included && normalized > phaseStart &&
                    envelope > 0.002f;
                float angle = index * 2.399963f +
                    MotifSignedNoise(index, 163) * 0.58f;
                Vector3 radial = new Vector3(
                    Mathf.Cos(angle),
                    0f,
                    Mathf.Sin(angle));
                Vector3 tangent = new Vector3(-radial.z, 0f, radial.x);
                float travel = Mathf.SmoothStep(0f, 1f, localPhase);
                float radius = _size * Mathf.Lerp(0.035f, 0.24f, travel) *
                    (0.72f + (index % 4) * 0.1f);
                float height = _size *
                    (0.025f +
                     travel * (0.11f + ((index * 3) % 4) * 0.055f) +
                     Mathf.Sin(localPhase * Mathf.PI) *
                     (0.07f + (index % 2) * 0.035f));
                Vector3 point = center + radial * radius +
                    tangent * MotifSignedNoise(index, 167) * _size * 0.035f +
                    Vector3.up * height;
                float tangentBias = (index & 1) == 0 ? -0.3f : 0.3f;
                Vector3 axis = (
                    radial * 0.4f +
                    tangent * tangentBias +
                    Vector3.up * (0.86f + (index % 2) * 0.08f)).normalized;
                Quaternion rotation = Quaternion.LookRotation(axis, tangent) *
                    Quaternion.Euler(0f, 0f, MotifSignedNoise(index, 173) * 20f);
                float width = 0.024f + (index % 3) * 0.006f;
                float length = 0.09f + (index % 3) * 0.03f;
                SetTransform(
                    _motifs[index],
                    active,
                    point,
                    rotation,
                    new Vector3(width, width * 0.86f, length) *
                        _size * envelope);
            }

            HideCastHalosFrom(0);
            HideTrails();
        }

        private void HideTrails()
        {
            for (int index = 0; index < _trails.Length; index++)
            {
                if (_trails[index] != null)
                {
                    _trails[index].gameObject.SetActive(false);
                }
            }
        }

        private void TickProfileBurst(
            float normalized,
            float intensity,
            bool area)
        {
            switch (_burstSilhouette)
            {
                case SpellBurstSilhouette.VenomSplash:
                    TickVenomBurst(normalized, intensity, area);
                    break;
                case SpellBurstSilhouette.FireEruption:
                    TickFireBurst(normalized, intensity, area);
                    break;
                case SpellBurstSilhouette.FrostShatter:
                    TickFrostBurst(normalized, intensity, area);
                    break;
                case SpellBurstSilhouette.RadiantJudgment:
                    TickRadiantBurst(normalized, intensity, area);
                    break;
                case SpellBurstSilhouette.ShadowCollapse:
                    TickShadowBurst(normalized, intensity, area);
                    break;
                case SpellBurstSilhouette.ArcaneFracture:
                    TickArcaneBurst(normalized, intensity, area);
                    break;
                default:
                    TickPhysicalBurst(normalized, intensity, area);
                    break;
            }
        }

        private void TickPhysicalBurst(float normalized, float intensity, bool area)
        {
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            float reach = _size * (0.14f + normalized * (area ? 0.92f : 0.5f)) * intensity;
            Vector3 center = _target + Vector3.up * 0.06f;
            SetParticleEmitter(center, Quaternion.identity);
            SetTransform(_core, false, center, Quaternion.identity, Vector3.zero);
            SetTransform(_shell, false, center, Quaternion.identity, Vector3.zero);
            SetDisc(false, center, 0f, 0f);
            for (int index = 0; index < _motifs.Length; index++)
            {
                bool active = index < 4;
                float angle = index * Mathf.PI * 0.5f + normalized * 0.7f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 point = center + radial * reach;
                point.y += (index % 2) * _size * 0.12f;
                SetTransform(
                    _motifs[index],
                    active,
                    point,
                    Quaternion.LookRotation(radial + Vector3.up * 0.16f, Vector3.up),
                    new Vector3(0.06f, 0.045f, 0.42f) * _size * pulse * intensity);
            }

            SetCastHalo(0, true, center, Quaternion.identity,
                _size * (0.1f + normalized * (area ? 0.94f : 0.5f)) * intensity);
            HideCastHalosFrom(1);
        }

        private void TickVenomBurst(float normalized, float intensity, bool area)
        {
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            float radius = _size * (0.12f + normalized * (area ? 0.82f : 0.5f)) * intensity;
            Vector3 center = _target + Vector3.up * 0.05f;
            SetParticleEmitter(center, Quaternion.LookRotation(Vector3.up, Vector3.forward));
            SetTransform(_core, true, center + Vector3.up * _size * 0.12f,
                Quaternion.Euler(0f, normalized * -420f, 0f),
                new Vector3(0.2f, 0.08f, 0.2f) * _size * pulse * intensity);
            SetTransform(_shell, false, center, Quaternion.identity, Vector3.zero);
            SetDisc(true, center, radius * 0.78f, 0.018f);
            RotateDisc(normalized * -190f);
            for (int index = 0; index < _motifs.Length; index++)
            {
                bool active = index < 5;
                float angle = index * Mathf.PI * 2f / 5f - normalized * 1.4f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 point = center + radial * radius * 0.72f;
                point.y += _size * (0.05f + normalized * (0.06f + (index % 2) * 0.08f));
                SetTransform(_motifs[index], active, point,
                    Quaternion.LookRotation(Vector3.down + radial * 0.35f, Vector3.forward),
                    new Vector3(0.045f, 0.045f, 0.2f) * _size * pulse * intensity);
            }

            SetCastHalo(0, true, center, Quaternion.identity, radius * 0.9f);
            HideCastHalosFrom(1);
        }

        private void TickFireBurst(float normalized, float intensity, bool area)
        {
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            float radius = _size * (0.1f + normalized * (area ? 1.08f : 0.82f)) * intensity;
            Vector3 center = _target + Vector3.up * 0.04f;
            Quaternion upward = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            SetParticleEmitter(center, upward);
            SetTransform(_core, true,
                center + Vector3.up * _size * (0.08f + normalized * 0.14f),
                upward * Quaternion.Euler(0f, 0f, normalized * 240f),
                new Vector3(0.09f, 0.09f, area ? 0.34f : 0.4f) *
                    _size * pulse * intensity);
            SetTransform(_shell, false, center, Quaternion.identity, Vector3.zero);
            SetDisc(false, center, 0f, 0f);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * 2.399963f + MotifSignedNoise(index, 11) * 0.72f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float heightBias = 0.34f + (index % 4) * 0.17f +
                    MotifSignedNoise(index, 13) * 0.09f;
                Vector3 point = center + radial * radius *
                    (0.32f + (index % 5) * 0.14f);
                point.y += normalized * _size * heightBias;
                SetTransform(_motifs[index], true, point,
                    Quaternion.Euler(
                        -32f - (index % 4) * 8f,
                        180f - angle * Mathf.Rad2Deg +
                            MotifSignedNoise(index, 29) * 22f,
                        MotifSignedNoise(index, 31) * 28f),
                    new Vector3(0.1f, 0.085f, 0.3f + (index % 3) * 0.09f) *
                        _size * pulse * intensity);
            }

            SetCastHalo(0, true, center, Quaternion.identity, radius);
            HideCastHalosFrom(1);
        }

        private void TickFrostBurst(float normalized, float intensity, bool area)
        {
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            float radius = _size * (0.12f + normalized * (area ? 1.02f : 0.74f)) * intensity;
            Vector3 center = _target + Vector3.up * 0.045f;
            Quaternion upward = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            SetParticleEmitter(center, upward);
            SetTransform(_core, true, center + Vector3.up * _size * 0.12f,
                upward * Quaternion.Euler(0f, 0f, normalized * -180f),
                new Vector3(0.07f, 0.07f, 0.3f) * _size * pulse * intensity);
            SetTransform(_shell, false, center, Quaternion.identity, Vector3.zero);
            SetDisc(false, center, 0f, 0f);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * 2.399963f + MotifSignedNoise(index, 17) * 0.8f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float height = 0.22f + (index % 4) * 0.14f +
                    MotifSignedNoise(index, 23) * 0.07f;
                Vector3 point = center + radial * radius *
                    (0.34f + (index % 5) * 0.13f + MotifSignedNoise(index, 19) * 0.08f);
                point.y += _size * height * pulse;
                SetTransform(_motifs[index], true, point,
                    Quaternion.Euler(
                        -38f - (index % 3) * 9f,
                        180f - angle * Mathf.Rad2Deg +
                            MotifSignedNoise(index, 37) * 25f,
                        MotifSignedNoise(index, 41) * 24f),
                    new Vector3(0.12f, 0.11f, 0.36f + (index % 3) * 0.11f) *
                        _size * pulse * intensity);
            }

            SetCastHalo(0, true, center, Quaternion.identity, radius);
            HideCastHalosFrom(1);
        }

        private void TickRadiantBurst(float normalized, float intensity, bool area)
        {
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            float radius = _size * (0.1f + normalized * (area ? 0.94f : 0.62f)) * intensity;
            Vector3 center = _target + Vector3.up * 0.04f;
            Quaternion upward = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            SetParticleEmitter(center, upward);
            SetTransform(_core, true, center + Vector3.up * _size * 0.24f,
                upward, new Vector3(0.07f, 0.07f, area ? 0.34f : 0.48f) * _size * pulse * intensity);
            SetTransform(_shell, true, center + Vector3.up * _size * 0.11f,
                Quaternion.Euler(45f, normalized * 180f, 45f),
                Vector3.one * _size * 0.14f * pulse * intensity);
            SetDisc(true, center, radius, 0.018f);
            RotateDisc(normalized * 120f);
            for (int index = 0; index < _motifs.Length; index++)
            {
                bool active = index < 4;
                float angle = index * Mathf.PI * 0.5f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 point = center + radial * radius * 0.7f +
                    Vector3.up * _size * (0.08f + (index % 2) * 0.07f);
                SetTransform(_motifs[index], active, point, upward,
                    new Vector3(0.04f, 0.04f, 0.22f) * _size * pulse * intensity);
            }

            SetCastHalo(0, true, center, Quaternion.identity, radius);
            HideCastHalosFrom(1);
        }

        private void TickShadowBurst(float normalized, float intensity, bool area)
        {
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            float outerRadius = _size * (area ? 1.04f : 0.76f) * intensity;
            float radius = Mathf.Lerp(outerRadius, _size * 0.12f, normalized);
            Vector3 center = _target + Vector3.up * 0.08f;
            SetParticleEmitter(center, Quaternion.identity);
            SetTransform(_core, true, center,
                Quaternion.Euler(45f, normalized * -720f, 45f),
                Vector3.one * _size * 0.18f * pulse * intensity);
            SetTransform(_shell, true, center,
                Quaternion.Euler(0f, normalized * -520f, 0f),
                new Vector3(0.3f, 0.1f, 0.3f) * _size * pulse * intensity);
            SetDisc(true, center, Mathf.Max(_size * 0.12f, radius), 0.015f);
            RotateDisc(normalized * -560f);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length - normalized * 3.8f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 point = center + radial * radius;
                point.y += _size * (0.06f + (index % 3) * 0.06f) * (1f - normalized);
                SetTransform(_motifs[index], true, point,
                    Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, index % 2 == 0 ? 42f : -42f),
                    new Vector3(0.045f, 0.16f, 0.045f) * _size * pulse * intensity);
            }

            SetCastHalo(0, true, center, Quaternion.Euler(-7f, normalized * -90f, 5f), radius);
            HideCastHalosFrom(1);
        }

        private void TickArcaneBurst(float normalized, float intensity, bool area)
        {
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            float radius = _size * (0.14f + normalized * (area ? 0.9f : 0.68f)) * intensity;
            Vector3 center = _target + Vector3.up * 0.12f;
            SetParticleEmitter(center, Quaternion.identity);
            SetTransform(_core, true, center,
                Quaternion.Euler(45f, normalized * 620f, 45f),
                new Vector3(0.14f, 0.26f, 0.14f) * _size * pulse * intensity);
            SetTransform(_shell, true, center,
                Quaternion.Euler(-35f, normalized * -480f, 35f),
                Vector3.one * _size * 0.2f * pulse * intensity);
            SetDisc(false, center, 0f, 0f);
            for (int index = 0; index < _motifs.Length; index++)
            {
                float angle = index * Mathf.PI * 2f / _motifs.Length + normalized * 2.6f;
                Vector3 radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 point = center + radial * radius;
                point.y += (index % 3 - 1) * _size * 0.11f;
                SetTransform(_motifs[index], true, point,
                    Quaternion.Euler(index * 22f, -angle * Mathf.Rad2Deg, 45f),
                    new Vector3(0.05f, 0.13f, 0.05f) * _size * pulse * intensity);
            }

            SetCastHalo(0, true, center,
                Quaternion.Euler(6f, normalized * 95f, -5f), radius * 0.82f);
            HideCastHalosFrom(1);
        }

        private void SetDisc(bool active, Vector3 position, float radius, float height)
        {
            SetTransform(
                _disc,
                false,
                position + Vector3.up * 0.045f,
                Quaternion.identity,
                new Vector3(radius, height, radius));
        }

        private void RotateDisc(float degrees)
        {
            if (_disc != null)
            {
                _disc.rotation = Quaternion.Euler(0f, degrees, 0f);
            }
        }

        private void ResetVisuals()
        {
            if (_particles != null)
            {
                _particles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (_groundParticles != null)
            {
                _groundParticles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
            }

            if (_pulseLight != null)
            {
                _pulseLight.enabled = false;
                _pulseLight.intensity = 0f;
            }

            SetActive(_core, false);
            SetActive(_shell, false);
            SetActive(_disc, false);
            for (int index = 0; index < _motifs.Length; index++)
            {
                SetActive(_motifs[index], false);
            }

            for (int index = 0; index < _halos.Length; index++)
            {
                if (_halos[index] != null)
                {
                    _halos[index].gameObject.SetActive(false);
                }
            }

            for (int index = 0; index < _trails.Length; index++)
            {
                if (_trails[index] != null)
                {
                    _trails[index].gameObject.SetActive(false);
                }
            }
        }

        private void ConfigureParticles()
        {
            if (_particles == null || _particleRenderer == null)
            {
                return;
            }

            bool oneShot = _phase == EffectVisualPhase.Impact ||
                _phase == EffectVisualPhase.Miss ||
                (_motion == EffectMotion.Burst &&
                 (_profile == SpellVisualProfile.Physical ||
                  _profile == SpellVisualProfile.Venom));
            bool spatialArea = _phase == EffectVisualPhase.Primary &&
                (_motion == EffectMotion.Burst ||
                 _motion == EffectMotion.Nova ||
                 _motion == EffectMotion.Trap ||
                 _motion == EffectMotion.BladeStorm);
            bool volumetricArea = UsesVolumetricAreaRecipe;
            bool volumetricPresentation = UsesVolumetricAreaPresentation;
            bool readableSpellProjectile = _phase == EffectVisualPhase.Primary &&
                _motion == EffectMotion.Projectile &&
                (_profile == SpellVisualProfile.Fire ||
                 _profile == SpellVisualProfile.Radiant);
            Vector3 initialPosition = _phase == EffectVisualPhase.CastStart
                ? _origin + Vector3.up * 0.32f
                : oneShot || spatialArea
                    ? _target
                    : _origin;
            SetParticleEmitter(initialPosition, Quaternion.identity);

            ParticleSystem.MainModule main = _particles.main;
            main.loop = !oneShot;
            Color particleCore = volumetricPresentation
                ? _ability == AbilityId.BlastWave
                    ? new Color(0.18f, 0.035f, 0.012f, 0.46f)
                    : new Color(0.075f, 0.2f, 0.3f, 0.4f)
                : readableSpellProjectile
                    ? _profile == SpellVisualProfile.Fire
                        ? new Color(1f, 0.16f, 0.015f, 0.82f)
                        : new Color(1f, 0.66f, 0.14f, 0.78f)
                    : ResolveCoreColor(_profile, _primaryColor);
            particleCore.a = Mathf.Min(
                particleCore.a,
                readableSpellProjectile ? 0.85f : 0.62f);
            Color particleAccent = volumetricPresentation
                ? _ability == AbilityId.BlastWave
                    ? new Color(1f, 0.52f, 0.055f, 0.8f)
                    : new Color(0.62f, 0.96f, 1f, 0.74f)
                : readableSpellProjectile
                    ? _profile == SpellVisualProfile.Fire
                        ? new Color(1f, 0.82f, 0.22f, 0.72f)
                        : new Color(1f, 0.94f, 0.58f, 0.7f)
                    : _accentColor;
            particleAccent.a = Mathf.Min(
                particleAccent.a,
                readableSpellProjectile ? 0.72f : 0.62f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                particleCore,
                particleAccent);
            main.startLifetime = ResolveParticleLifetime();
            main.startSize = ResolveParticleSize();
            main.startSpeed = ResolveParticleSpeed();
            main.startRotation = volumetricPresentation ||
                spatialArea ||
                readableSpellProjectile
                ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f)
                : new ParticleSystem.MinMaxCurve(0f);
            main.gravityModifier = volumetricPresentation
                ? _ability == AbilityId.BlastWave ? -0.08f : 0.12f
                : _profile == SpellVisualProfile.Fire
                    ? -0.12f
                    : _profile == SpellVisualProfile.Frost
                        ? 0.08f
                        : 0f;
            main.maxParticles = 72;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = !oneShot;
            emission.rateOverTime = ResolveParticleRate();

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.radius = volumetricPresentation
                ? Mathf.Clamp(
                    _size * (volumetricArea
                        ? _ability == AbilityId.BlastWave ? 0.28f : 0.24f
                        : 0.12f),
                    0.26f,
                    0.88f)
                : spatialArea
                    ? Mathf.Clamp(_size * 0.42f, 0.22f, 1.15f)
                : readableSpellProjectile
                    ? _profile == SpellVisualProfile.Fire
                        ? 0.055f
                        : Mathf.Clamp(_size * 0.28f, 0.1f, 0.16f)
                    : Mathf.Clamp(_size * 0.09f, 0.025f, 0.14f);
            shape.shapeType = volumetricPresentation
                ? ParticleSystemShapeType.Hemisphere
                : readableSpellProjectile && _profile == SpellVisualProfile.Radiant
                    ? ParticleSystemShapeType.Sphere
                : _profile == SpellVisualProfile.Fire
                    ? ParticleSystemShapeType.Cone
                    : _profile == SpellVisualProfile.Frost ||
                      _profile == SpellVisualProfile.Radiant
                        ? ParticleSystemShapeType.Circle
                        : ParticleSystemShapeType.Sphere;
            shape.radiusThickness = volumetricPresentation
                ? _ability == AbilityId.BlastWave ? 0.65f : 0.8f
                : 1f;
            shape.randomDirectionAmount = volumetricPresentation
                ? _ability == AbilityId.BlastWave ? 0.22f : 0.12f
                : 0f;
            shape.angle = _profile == SpellVisualProfile.Fire ? 18f : 25f;

            ParticleSystem.NoiseModule noise = _particles.noise;
            noise.enabled = volumetricPresentation ||
                _profile == SpellVisualProfile.Fire ||
                _profile == SpellVisualProfile.Shadow ||
                _profile == SpellVisualProfile.Arcane;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = volumetricPresentation
                ? _ability == AbilityId.BlastWave ? 0.28f : 0.05f
                : _profile == SpellVisualProfile.Shadow ? 0.18f : 0.1f;
            noise.frequency = _profile == SpellVisualProfile.Arcane ? 1.1f : 0.72f;
            noise.scrollSpeed = 0.24f;

            _particleRenderer.renderMode = volumetricPresentation ||
                readableSpellProjectile
                ? ParticleSystemRenderMode.Billboard
                : (_profile == SpellVisualProfile.Fire && !spatialArea) ||
                  _profile == SpellVisualProfile.Shadow ||
                  _hunterShotProfile != HunterShotVisualProfile.None
                    ? ParticleSystemRenderMode.Stretch
                    : ParticleSystemRenderMode.Billboard;
            _particleRenderer.lengthScale =
                _hunterShotProfile != HunterShotVisualProfile.None
                    ? 3.4f
                    : _profile == SpellVisualProfile.Fire
                        ? 2.8f
                        : 1.7f;
            _particleRenderer.velocityScale = 0.18f;
            _particleRenderer.maxParticleSize =
                _hunterShotProfile != HunterShotVisualProfile.None
                    ? 0.09f
                    : volumetricPresentation
                        ? volumetricArea ? 0.32f : 0.22f
                        : readableSpellProjectile
                            ? 0.32f
                            : spatialArea ? 0.28f : 0.2f;

            _particles.Play(true);
            if (volumetricArea)
            {
                _particles.Emit(_ability == AbilityId.BlastWave ? 56 : 48);
            }
            else if (UsesVolumetricAreaResolution)
            {
                _particles.Emit(_phase == EffectVisualPhase.Miss
                    ? 8
                    : _ability == AbilityId.BlastWave ? 14 : 12);
            }
            else if (spatialArea &&
                (_profile == SpellVisualProfile.Fire ||
                 _profile == SpellVisualProfile.Frost))
            {
                _particles.Emit(_profile == SpellVisualProfile.Fire ? 38 : 34);
            }
            else if (readableSpellProjectile)
            {
                _particles.Emit(_profile == SpellVisualProfile.Fire ? 32 : 28);
            }
            else if (oneShot)
            {
                int count = _phase == EffectVisualPhase.Miss
                    ? 6
                    : _profile == SpellVisualProfile.Physical ||
                      _profile == SpellVisualProfile.Venom
                        ? 12
                        : 20;
                _particles.Emit(count);
            }
        }

        private void ConfigureGroundParticles()
        {
            if (_groundParticles == null || _groundParticleRenderer == null)
            {
                return;
            }

            bool groundEvent = _phase == EffectVisualPhase.Impact ||
                _phase == EffectVisualPhase.Miss ||
                (_phase == EffectVisualPhase.Primary &&
                 (_motion == EffectMotion.Burst ||
                  _motion == EffectMotion.Nova ||
                  _motion == EffectMotion.Trap ||
                  _motion == EffectMotion.BladeStorm));
            bool volumetricArea = UsesVolumetricAreaRecipe;
            bool volumetricPresentation = UsesVolumetricAreaPresentation;
            if (!groundEvent)
            {
                _groundParticles.Stop(
                    true,
                    ParticleSystemStopBehavior.StopEmittingAndClear);
                return;
            }

            Transform groundTransform = _groundParticles.transform;
            groundTransform.SetPositionAndRotation(
                _target + Vector3.up * 0.035f,
                Quaternion.identity);
            Color dust = volumetricPresentation
                ? _ability == AbilityId.BlastWave
                    ? new Color(0.12f, 0.055f, 0.035f, 0.34f)
                    : new Color(0.12f, 0.25f, 0.3f, 0.3f)
                : Color.Lerp(
                    _primaryColor,
                    new Color(0.16f, 0.14f, 0.18f, 1f),
                    0.58f);
            if (!volumetricPresentation)
            {
                dust.a = _phase == EffectVisualPhase.Miss ? 0.16f : 0.28f;
            }

            Color spark = volumetricPresentation
                ? _ability == AbilityId.BlastWave
                    ? new Color(1f, 0.34f, 0.025f, 0.54f)
                    : new Color(0.52f, 0.9f, 1f, 0.4f)
                : _accentColor;
            if (!volumetricPresentation)
            {
                spark.a = _phase == EffectVisualPhase.Miss ? 0.22f : 0.38f;
            }

            ParticleSystem.MainModule main = _groundParticles.main;
            main.loop = false;
            main.startColor = new ParticleSystem.MinMaxGradient(dust, spark);
            main.startLifetime = volumetricPresentation
                ? _ability == AbilityId.BlastWave
                    ? new ParticleSystem.MinMaxCurve(0.38f, 0.76f)
                    : new ParticleSystem.MinMaxCurve(0.46f, 0.84f)
                : new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
            main.startSize = volumetricPresentation
                ? _ability == AbilityId.BlastWave
                    ? new ParticleSystem.MinMaxCurve(0.08f, 0.26f)
                    : new ParticleSystem.MinMaxCurve(0.12f, 0.32f)
                : new ParticleSystem.MinMaxCurve(
                    Mathf.Clamp(_size * 0.025f, 0.018f, 0.07f),
                    Mathf.Clamp(_size * 0.08f, 0.05f, 0.16f));
            main.startSpeed = volumetricPresentation
                ? _ability == AbilityId.BlastWave
                    ? new ParticleSystem.MinMaxCurve(0.22f, 0.72f)
                    : new ParticleSystem.MinMaxCurve(0.06f, 0.28f)
                : new ParticleSystem.MinMaxCurve(0.08f, 0.38f);
            main.startRotation = volumetricPresentation
                ? new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f)
                : new ParticleSystem.MinMaxCurve(0f);
            main.gravityModifier = volumetricPresentation
                ? _ability == AbilityId.BlastWave ? -0.04f : -0.015f
                : 0.16f;
            main.maxParticles = 28;

            ParticleSystem.ShapeModule shape = _groundParticles.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Clamp(
                _size * (volumetricPresentation ? 0.46f : 0.42f),
                0.2f,
                1.25f);
            shape.radiusThickness = volumetricPresentation ? 1f : 0.72f;
            shape.randomDirectionAmount = volumetricPresentation ? 0.68f : 0f;

            ParticleSystem.NoiseModule noise = _groundParticles.noise;
            noise.enabled = true;
            noise.strength = volumetricPresentation
                ? _ability == AbilityId.BlastWave ? 0.18f : 0.06f
                : 0.045f;
            noise.frequency = volumetricPresentation ? 0.68f : 0.55f;

            _groundParticleRenderer.renderMode = volumetricPresentation
                ? ParticleSystemRenderMode.Billboard
                : ParticleSystemRenderMode.Stretch;
            _groundParticleRenderer.lengthScale = volumetricPresentation ? 1f : 1.35f;
            _groundParticleRenderer.velocityScale = volumetricPresentation ? 0f : 0.1f;
            _groundParticleRenderer.maxParticleSize = volumetricPresentation ? 0.14f : 0.08f;
            _groundParticles.Play(true);
            _groundParticles.Emit(volumetricPresentation
                ? volumetricArea
                    ? _ability == AbilityId.BlastWave ? 24 : 22
                    : _phase == EffectVisualPhase.Miss ? 6 : 10
                : _phase == EffectVisualPhase.Miss ? 7 : 18);
        }

        private void ConfigurePulseLight()
        {
            if (_pulseLight == null)
            {
                return;
            }

            _pulseLight.color = Color.Lerp(_primaryColor, _accentColor, 0.48f);
            _pulseLight.range = Mathf.Clamp(1.15f + _size * 0.9f, 1.25f, 3.6f);
            _pulseLight.intensity = 0f;
            _pulseLight.enabled = false;
        }

        private void TickPulseLight(float normalized)
        {
            if (_pulseLight == null)
            {
                return;
            }

            bool shortPulse = _phase == EffectVisualPhase.CastStart ||
                _phase == EffectVisualPhase.Impact ||
                _phase == EffectVisualPhase.Miss ||
                _motion == EffectMotion.Burst ||
                _motion == EffectMotion.Nova ||
                _motion == EffectMotion.BladeStorm;
            float envelope = shortPulse
                ? Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI)
                : Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI) * 0.42f;
            envelope *= _phase == EffectVisualPhase.Miss ? 0.35f : 1f;
            Vector3 lightCenter = _phase == EffectVisualPhase.CastStart
                ? _origin
                : _phase == EffectVisualPhase.Primary &&
                  (_motion == EffectMotion.Projectile ||
                   _motion == EffectMotion.HunterArrow ||
                   _motion == EffectMotion.Dash)
                    ? Vector3.Lerp(_origin, _target, normalized)
                    : _target;
            float lightHeight = UsesVolumetricAreaPresentation
                ? Mathf.Clamp(_size * 0.055f, 0.1f, 0.18f)
                : Mathf.Clamp(_size * 0.18f, 0.12f, 0.42f);
            _pulseLight.transform.position = lightCenter + Vector3.up * lightHeight;
            _pulseLight.intensity = Mathf.Clamp(
                envelope * (shortPulse ? 1.55f : 0.72f),
                0f,
                1.8f);
            _pulseLight.enabled = _pulseLight.intensity > 0.015f;
        }

        private ParticleSystem.MinMaxCurve ResolveParticleLifetime()
        {
            if (UsesVolumetricAreaPresentation)
            {
                return _ability == AbilityId.BlastWave
                    ? new ParticleSystem.MinMaxCurve(0.42f, 0.72f)
                    : new ParticleSystem.MinMaxCurve(0.45f, 0.8f);
            }

            switch (_profile)
            {
                case SpellVisualProfile.Fire:
                    return _phase == EffectVisualPhase.Primary &&
                        _motion == EffectMotion.Projectile
                        ? new ParticleSystem.MinMaxCurve(0.3f, 0.55f)
                        : new ParticleSystem.MinMaxCurve(0.24f, 0.46f);
                case SpellVisualProfile.Frost:
                    return new ParticleSystem.MinMaxCurve(0.2f, 0.36f);
                case SpellVisualProfile.Radiant:
                    return _phase == EffectVisualPhase.Primary &&
                        _motion == EffectMotion.Projectile
                        ? new ParticleSystem.MinMaxCurve(0.26f, 0.48f)
                        : new ParticleSystem.MinMaxCurve(0.18f, 0.34f);
                case SpellVisualProfile.Shadow:
                    return new ParticleSystem.MinMaxCurve(0.32f, 0.58f);
                default:
                    return new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            }
        }

        private ParticleSystem.MinMaxCurve ResolveParticleSize()
        {
            float scale = Mathf.Clamp(_size, 0.35f, 1.4f);
            if (_hunterShotProfile != HunterShotVisualProfile.None)
            {
                return new ParticleSystem.MinMaxCurve(
                    0.018f * scale,
                    0.055f * scale);
            }

            if (UsesVolumetricAreaResolution)
            {
                return new ParticleSystem.MinMaxCurve(0.28f, 0.78f);
            }

            if (UsesVolumetricAreaRecipe)
            {
                return _ability == AbilityId.BlastWave
                    ? new ParticleSystem.MinMaxCurve(0.55f, 1.35f)
                    : new ParticleSystem.MinMaxCurve(0.45f, 1.1f);
            }

            bool readableSpellProjectile = _phase == EffectVisualPhase.Primary &&
                _motion == EffectMotion.Projectile &&
                (_profile == SpellVisualProfile.Fire ||
                 _profile == SpellVisualProfile.Radiant);
            if (readableSpellProjectile)
            {
                return new ParticleSystem.MinMaxCurve(
                    ProjectileParticleMinimumSize,
                    ProjectileParticleMaximumSize);
            }

            bool spatialArea = _phase == EffectVisualPhase.Primary &&
                (_motion == EffectMotion.Burst ||
                 _motion == EffectMotion.Nova ||
                 _motion == EffectMotion.Trap ||
                 _motion == EffectMotion.BladeStorm);
            if (spatialArea &&
                (_profile == SpellVisualProfile.Fire ||
                 _profile == SpellVisualProfile.Frost))
            {
                return new ParticleSystem.MinMaxCurve(
                    SpatialParticleMinimumSize,
                    SpatialParticleMaximumSize);
            }

            float minimum = spatialArea
                ? 0.1f
                : _profile == SpellVisualProfile.Fire ? 0.075f : 0.05f;
            float maximum = spatialArea
                ? 0.36f
                : _profile == SpellVisualProfile.Radiant ? 0.2f : 0.23f;
            return new ParticleSystem.MinMaxCurve(
                minimum * scale,
                maximum * scale);
        }

        private ParticleSystem.MinMaxCurve ResolveParticleSpeed()
        {
            if (UsesVolumetricAreaPresentation)
            {
                return _ability == AbilityId.BlastWave
                    ? new ParticleSystem.MinMaxCurve(1.5f, 3.2f)
                    : new ParticleSystem.MinMaxCurve(0.7f, 1.7f);
            }

            if (_phase == EffectVisualPhase.Primary &&
                _motion == EffectMotion.Projectile)
            {
                if (_profile == SpellVisualProfile.Fire)
                {
                    return new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
                }

                if (_profile == SpellVisualProfile.Radiant)
                {
                    return new ParticleSystem.MinMaxCurve(0.08f, 0.32f);
                }
            }

            float multiplier = _phase == EffectVisualPhase.Impact ||
                _phase == EffectVisualPhase.Miss
                ? 2.4f
                : 1f;
            return new ParticleSystem.MinMaxCurve(
                0.06f * multiplier,
                0.26f * multiplier);
        }

        private float ResolveParticleRate()
        {
            if (UsesVolumetricAreaPresentation)
            {
                return _ability == AbilityId.BlastWave ? 18f : 10f;
            }

            if (_hunterShotProfile != HunterShotVisualProfile.None)
            {
                return 18f;
            }

            if (_phase == EffectVisualPhase.Primary &&
                _motion == EffectMotion.Projectile)
            {
                if (_profile == SpellVisualProfile.Fire)
                {
                    return 65f;
                }

                if (_profile == SpellVisualProfile.Radiant)
                {
                    return 55f;
                }
            }

            switch (_profile)
            {
                case SpellVisualProfile.Fire:
                    return 54f;
                case SpellVisualProfile.Frost:
                    return 30f;
                case SpellVisualProfile.Radiant:
                    return 38f;
                case SpellVisualProfile.Shadow:
                    return 42f;
                case SpellVisualProfile.Venom:
                    return 24f;
                default:
                    return 28f;
            }
        }

        private void SetParticleEmitter(Vector3 position, Quaternion rotation)
        {
            if (_particles == null)
            {
                return;
            }

            Transform particleTransform = _particles.transform;
            particleTransform.SetPositionAndRotation(position, rotation);
        }

        private void ApplyRigColors()
        {
            bool readableSpellProjectile = _phase == EffectVisualPhase.Primary &&
                _motion == EffectMotion.Projectile &&
                (_ability == AbilityId.Fireball ||
                 _ability == AbilityId.Pyroblast ||
                 _ability == AbilityId.Penance);
            SetColor(
                _coreRenderer,
                ResolveCoreColor(_profile, _primaryColor),
                readableSpellProjectile ? ProjectileCoreAlphaCap : CoreAlphaCap);
            SetColor(
                _shellRenderer,
                _primaryColor,
                readableSpellProjectile ? ProjectileShellAlphaCap : ShellAlphaCap);
            SetColor(_discRenderer, _primaryColor, DiscAlphaCap);
            for (int index = 0; index < _motifRenderers.Length; index++)
            {
                bool subduedSpatialMotif = UsesVolumetricAreaPresentation ||
                    (_phase == EffectVisualPhase.Primary &&
                     (_motion == EffectMotion.Burst ||
                      _motion == EffectMotion.Nova ||
                      _motion == EffectMotion.Trap) &&
                     (_profile == SpellVisualProfile.Fire ||
                      _profile == SpellVisualProfile.Frost));
                float spatialAlpha = UsesVolumetricAreaPresentation
                    ? _profile == SpellVisualProfile.Frost ? 0.34f : 0.3f
                    : _profile == SpellVisualProfile.Frost ? 0.38f : 0.44f;
                float motifAlpha = subduedSpatialMotif
                    ? spatialAlpha
                    : _phase == EffectVisualPhase.Primary &&
                      (_ability == AbilityId.Fireball ||
                       _ability == AbilityId.Pyroblast)
                        ? 0.38f
                        : _phase == EffectVisualPhase.Primary &&
                          _ability == AbilityId.Penance
                            ? 0.64f
                            : MotifAlphaCap;
                SetColor(
                    _motifRenderers[index],
                    index % 2 == 0 ? _primaryColor : _accentColor,
                    motifAlpha);
            }

            for (int index = 0; index < _halos.Length; index++)
            {
                LineRenderer halo = _halos[index];
                Color color = index % 2 == 0 ? _accentColor : _primaryColor;
                color.a = Mathf.Min(
                    color.a,
                    HaloAlphaCap - index * 0.055f);
                halo.startColor = color;
                halo.endColor = color;
                halo.widthMultiplier = Mathf.Clamp(
                    _size * (0.028f - index * 0.004f),
                    0.012f,
                    0.052f);
            }

            for (int index = 0; index < _trails.Length; index++)
            {
                LineRenderer trail = _trails[index];
                Color color = index == 0 ? _primaryColor : _accentColor;
                bool seriousProjectile = _phase == EffectVisualPhase.Primary &&
                    (_ability == AbilityId.Fireball ||
                     _ability == AbilityId.Pyroblast ||
                     _ability == AbilityId.Penance);
                color.a = seriousProjectile
                    ? index == 0 ? 0.28f : 0.18f
                    : index == 0 ? TrailAlphaCap : 0.44f;
                trail.startColor = color;
                Color tailColor = color;
                tailColor.a = 0f;
                trail.endColor = tailColor;
                trail.widthMultiplier = Mathf.Max(
                    0.012f,
                    seriousProjectile
                        ? Mathf.Min(
                            index == 0 ? 0.045f : 0.025f,
                            _size * (index == 0 ? 0.08f : 0.045f))
                        : _size * (index == 0 ? 0.052f : 0.028f));
            }
        }

        private void ApplyProfileMeshes()
        {
            if (!HasProfileMeshes)
            {
                return;
            }

            bool penanceProjectile = _motion == EffectMotion.Projectile &&
                _ability == AbilityId.Penance;
            bool crystalMotion =
                _hunterShotProfile != HunterShotVisualProfile.None ||
                _motion == EffectMotion.HunterArrow ||
                _motion == EffectMotion.Volley ||
                _motion == EffectMotion.Trap ||
                _motion == EffectMotion.BladeStorm ||
                _motion == EffectMotion.FacetedWard ||
                _motion == EffectMotion.Horror ||
                _motion == EffectMotion.TargetMark;

            if (penanceProjectile)
            {
                _coreMeshFilter.sharedMesh = _cometMesh;
                _shellMeshFilter.sharedMesh = _cometMesh;
            }
            else if (crystalMotion)
            {
                _coreMeshFilter.sharedMesh = _crystalMesh;
                _shellMeshFilter.sharedMesh = _crystalMesh;
            }
            else
            {
                switch (_profile)
                {
                    case SpellVisualProfile.Fire:
                        _coreMeshFilter.sharedMesh = _cometMesh;
                        _shellMeshFilter.sharedMesh = _cometMesh;
                        break;
                    case SpellVisualProfile.Frost:
                    case SpellVisualProfile.Radiant:
                    case SpellVisualProfile.Arcane:
                        _coreMeshFilter.sharedMesh = _crystalMesh;
                        _shellMeshFilter.sharedMesh = _crystalMesh;
                        break;
                    default:
                        _coreMeshFilter.sharedMesh = _roundedMesh;
                        _shellMeshFilter.sharedMesh = _roundedMesh;
                        break;
                }
            }

            for (int index = 0; index < _motifMeshFilters.Length; index++)
            {
                MeshFilter motif = _motifMeshFilters[index];
                if (motif == null)
                {
                    continue;
                }

                if (penanceProjectile)
                {
                    motif.sharedMesh = _cometMesh;
                }
                else if (crystalMotion)
                {
                    motif.sharedMesh = _crystalMesh;
                }
                else
                {
                    switch (_profile)
                    {
                        case SpellVisualProfile.Fire:
                            motif.sharedMesh = _cometMesh;
                            break;
                        case SpellVisualProfile.Frost:
                        case SpellVisualProfile.Radiant:
                        case SpellVisualProfile.Arcane:
                            motif.sharedMesh = _crystalMesh;
                            break;
                        default:
                            motif.sharedMesh = _motifBaseMeshes[index];
                            break;
                    }
                }
            }
        }

        private void SetTrailWidth(int index, float width)
        {
            if (index >= 0 && index < _trails.Length && _trails[index] != null)
            {
                _trails[index].widthMultiplier = width;
            }
        }

        private float MotifSignedNoise(int index, int channel)
        {
            unchecked
            {
                uint value = (uint)((int)_ability * 73856093);
                value ^= (uint)((index + 1) * 19349663);
                value ^= (uint)(channel * 83492791);
                value ^= value >> 13;
                value *= 1274126177u;
                value ^= value >> 16;
                return (value & 1023u) / 511.5f - 1f;
            }
        }

        private static Color ResolveCoreColor(SpellVisualProfile profile, Color primary)
        {
            switch (profile)
            {
                case SpellVisualProfile.Fire:
                    return new Color(1f, 0.8f, 0.12f, 1f);
                case SpellVisualProfile.Frost:
                    return new Color(0.28f, 0.9f, 1f, 1f);
                case SpellVisualProfile.Radiant:
                    return new Color(1f, 0.9f, 0.28f, 1f);
                case SpellVisualProfile.Shadow:
                    return new Color(0.72f, 0.24f, 1f, 1f);
                case SpellVisualProfile.Arcane:
                    return new Color(0.72f, 0.46f, 1f, 1f);
                case SpellVisualProfile.Venom:
                    return new Color(0.62f, 1f, 0.18f, 1f);
                default:
                    return Color.Lerp(primary, Color.white, 0.32f);
            }
        }

        private static Color ResolveAccentColor(SpellVisualProfile profile, Color primary)
        {
            switch (profile)
            {
                case SpellVisualProfile.Fire:
                    return new Color(1f, 0.88f, 0.24f, 1f);
                case SpellVisualProfile.Frost:
                    return new Color(0.32f, 0.86f, 1f, 1f);
                case SpellVisualProfile.Radiant:
                    return new Color(1f, 0.78f, 0.22f, 1f);
                case SpellVisualProfile.Shadow:
                    return new Color(0.94f, 0.2f, 1f, 1f);
                case SpellVisualProfile.Arcane:
                    return new Color(0.88f, 0.5f, 1f, 1f);
                case SpellVisualProfile.Venom:
                    return new Color(0.76f, 1f, 0.14f, 1f);
                default:
                    return Color.Lerp(primary, Color.white, 0.48f);
            }
        }

        private static void SetTransform(
            Transform target,
            bool active,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            if (target == null)
            {
                return;
            }

            target.gameObject.SetActive(active);
            if (!active)
            {
                return;
            }

            target.position = position;
            target.rotation = rotation;
            target.localScale = scale;
        }

        private static void SetActive(Transform target, bool active)
        {
            if (target != null)
            {
                target.gameObject.SetActive(active);
            }
        }

        private void SetColor(Renderer renderer, Color color, float alphaCap)
        {
            if (renderer == null)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            color.a = Mathf.Min(color.a, alphaCap);
            Color emission = color * 1.35f;
            emission.a = color.a;
            renderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor("_BaseColor", color);
            _propertyBlock.SetColor("_Color", color);
            _propertyBlock.SetColor("_EmissionColor", emission);
            renderer.SetPropertyBlock(_propertyBlock);
        }
    }
}
