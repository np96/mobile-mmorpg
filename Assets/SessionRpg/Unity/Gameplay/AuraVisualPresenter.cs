using System;
using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    [Flags]
    public enum AuraVisualMotif
    {
        None = 0,
        WardBands = 1 << 0,
        GuardianRunes = 1 << 1,
        FrozenSpikes = 1 << 2,
        ControlCrown = 1 << 3,
        FlameWisps = 1 << 4,
        PoisonOrbit = 1 << 5,
        FriendlyStealthTrace = 1 << 6,
        PolymorphCloud = 1 << 7,
    }

    [Flags]
    public enum PersistentAuraVisual
    {
        None = 0,
        VampiricTouch = 1 << 0,
        ShadowWordPain = 1 << 1,
        DevouringPlague = 1 << 2,
        MindFlay = 1 << 3,
        Dispersion = 1 << 4,
        PsychicHorror = 1 << 5,
        SerpentSting = 1 << 6,
        ViperSting = 1 << 7,
        ScorpidSting = 1 << 8,
        HuntersMark = 1 << 9,
        TrapRoot = 1 << 10,
        Deterrence = 1 << 11,
    }

    /// <summary>
    /// Presents persistent combat state using authored, bounded world-space motifs.
    /// It has no combat authority and never reveals hostile stealth.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AuraVisualPresenter : MonoBehaviour
    {
        public const int AuthoredMotifCount = 8;
        public const int AuthoredPersistentMotifCount = 12;
        public const int AuthoredPersistentRendererCount = 40;

        [Header("Ward")]
        [SerializeField] private GameObject _wardRoot;
        [SerializeField] private Transform[] _wardBands = Array.Empty<Transform>();
        [SerializeField] private Renderer[] _wardRenderers = Array.Empty<Renderer>();

        [Header("Mitigation")]
        [SerializeField] private GameObject _guardianRoot;
        [SerializeField] private Transform[] _guardianRunes = Array.Empty<Transform>();
        [SerializeField] private Renderer[] _guardianRenderers = Array.Empty<Renderer>();

        [Header("Frozen")]
        [SerializeField] private GameObject _frozenRoot;
        [SerializeField] private Transform[] _frozenSpikes = Array.Empty<Transform>();
        [SerializeField] private Renderer[] _frozenRenderers = Array.Empty<Renderer>();

        [Header("Control")]
        [SerializeField] private GameObject _controlRoot;
        [SerializeField] private Transform[] _controlRunes = Array.Empty<Transform>();
        [SerializeField] private Renderer[] _controlRenderers = Array.Empty<Renderer>();

        [Header("Burning")]
        [SerializeField] private GameObject _burningRoot;
        [SerializeField] private Transform[] _flameWisps = Array.Empty<Transform>();
        [SerializeField] private Renderer[] _burningRenderers = Array.Empty<Renderer>();

        [Header("Poison")]
        [SerializeField] private GameObject _poisonRoot;
        [SerializeField] private Transform[] _poisonNodes = Array.Empty<Transform>();
        [SerializeField] private Renderer[] _poisonRenderers = Array.Empty<Renderer>();

        [Header("Friendly stealth")]
        [SerializeField] private GameObject _stealthRoot;
        [SerializeField] private Transform[] _stealthWisps = Array.Empty<Transform>();
        [SerializeField] private Renderer[] _stealthRenderers = Array.Empty<Renderer>();

        [Header("Polymorph")]
        [SerializeField] private GameObject _polymorphRoot;
        [SerializeField] private Renderer[] _polymorphRenderers = Array.Empty<Renderer>();

        [Header("Specialized persistent states")]
        [SerializeField] private GameObject[] _persistentRoots = Array.Empty<GameObject>();
        [SerializeField] private Transform[] _persistentMotionRoots = Array.Empty<Transform>();
        [SerializeField] private Renderer[] _persistentRenderers = Array.Empty<Renderer>();
        [SerializeField] private int[] _persistentRendererStarts = Array.Empty<int>();
        [SerializeField] private int[] _persistentRendererCounts = Array.Empty<int>();

        private MaterialPropertyBlock _propertyBlock;
        private Vector3[] _frozenBaseScales = Array.Empty<Vector3>();
        private Vector3[] _controlBasePositions = Array.Empty<Vector3>();
        private Vector3[] _flameBasePositions = Array.Empty<Vector3>();
        private Vector3[] _flameBaseScales = Array.Empty<Vector3>();
        private Vector3[] _stealthBaseScales = Array.Empty<Vector3>();
        private Vector3[] _persistentBaseScales = Array.Empty<Vector3>();
        private AuraVisualMotif _activeMotifs;
        private PersistentAuraVisual _activePersistentMotifs;
        private bool _initialized;

        public AuraVisualMotif ActiveMotifs => _activeMotifs;

        public PersistentAuraVisual ActivePersistentMotifs => _activePersistentMotifs;

        public int PersistentRendererCount => Length(_persistentRenderers);

        public bool HasAuthoredPersistentMotifs =>
            Length(_persistentRoots) == AuthoredPersistentMotifCount &&
            Length(_persistentMotionRoots) == AuthoredPersistentMotifCount &&
            Length(_persistentRendererStarts) == AuthoredPersistentMotifCount &&
            Length(_persistentRendererCounts) == AuthoredPersistentMotifCount &&
            PersistentRendererCount == AuthoredPersistentRendererCount;

        public int SerializedRendererCount =>
            Length(_wardRenderers) +
            Length(_guardianRenderers) +
            Length(_frozenRenderers) +
            Length(_controlRenderers) +
            Length(_burningRenderers) +
            Length(_poisonRenderers) +
            Length(_stealthRenderers) +
            Length(_polymorphRenderers);

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            Initialize();
            ResetPresentation();
        }

        private void Update()
        {
            if (_activeMotifs == AuraVisualMotif.None &&
                _activePersistentMotifs == PersistentAuraVisual.None)
            {
                return;
            }

            float time = Time.time;
            float deltaTime = Time.deltaTime;
            RotateIfActive(_wardRoot, 30f * deltaTime);
            RotateIfActive(_guardianRoot, -42f * deltaTime);
            RotateIfActive(_controlRoot, 78f * deltaTime);
            RotateIfActive(_burningRoot, -34f * deltaTime);
            RotateIfActive(_poisonRoot, 46f * deltaTime);
            RotateIfActive(_stealthRoot, 58f * deltaTime);
            RotateIfActive(_polymorphRoot, -24f * deltaTime);

            for (int index = 0; index < _persistentRoots.Length; index++)
            {
                PersistentAuraVisual motif = (PersistentAuraVisual)(1 << index);
                if ((_activePersistentMotifs & motif) == 0)
                {
                    continue;
                }

                Transform motionRoot = index < _persistentMotionRoots.Length
                    ? _persistentMotionRoots[index]
                    : null;
                if (motionRoot == null)
                {
                    continue;
                }

                Vector3 baseScale = index < _persistentBaseScales.Length
                    ? _persistentBaseScales[index]
                    : Vector3.one;
                TickPersistentMotion(
                    motif,
                    index,
                    motionRoot,
                    baseScale,
                    time);
            }

            if ((_activeMotifs & AuraVisualMotif.WardBands) != 0)
            {
                for (int index = 0; index < _wardBands.Length; index++)
                {
                    Transform band = _wardBands[index];
                    if (band != null)
                    {
                        float phase = time * (1.4f + (index * 0.17f)) + index;
                        float pulse = 0.96f + (Mathf.Sin(phase) * 0.045f);
                        band.localScale = Vector3.one * pulse;
                    }
                }
            }

            if ((_activeMotifs & AuraVisualMotif.FrozenSpikes) != 0)
            {
                for (int index = 0; index < _frozenSpikes.Length; index++)
                {
                    Transform spike = _frozenSpikes[index];
                    if (spike != null)
                    {
                        Vector3 scale = _frozenBaseScales[index];
                        scale.y *= 0.96f +
                            (Mathf.Sin((time * 5.2f) + index) * 0.045f);
                        spike.localScale = scale;
                    }
                }
            }

            if ((_activeMotifs & AuraVisualMotif.ControlCrown) != 0)
            {
                for (int index = 0; index < _controlRunes.Length; index++)
                {
                    Transform rune = _controlRunes[index];
                    if (rune != null)
                    {
                        Vector3 position = _controlBasePositions[index];
                        position.y += Mathf.Sin((time * 4f) + (index * 2.1f)) * 0.07f;
                        rune.localPosition = position;
                        rune.Rotate(Vector3.up, (105f + (index * 14f)) * deltaTime, Space.Self);
                    }
                }
            }

            if ((_activeMotifs & AuraVisualMotif.FlameWisps) != 0)
            {
                for (int index = 0; index < _flameWisps.Length; index++)
                {
                    Transform wisp = _flameWisps[index];
                    if (wisp != null)
                    {
                        float cycle = Mathf.Repeat((time * 0.92f) + (index * 0.31f), 1f);
                        Vector3 position = _flameBasePositions[index];
                        position.y += cycle * 0.48f;
                        wisp.localPosition = position;
                        Vector3 scale = _flameBaseScales[index];
                        scale.x *= 1f - (cycle * 0.42f);
                        scale.z *= 1f - (cycle * 0.42f);
                        scale.y *= 0.82f + (Mathf.Sin(cycle * Mathf.PI) * 0.38f);
                        wisp.localScale = scale;
                    }
                }
            }

            if ((_activeMotifs & AuraVisualMotif.FriendlyStealthTrace) != 0)
            {
                for (int index = 0; index < _stealthWisps.Length; index++)
                {
                    Transform wisp = _stealthWisps[index];
                    if (wisp != null)
                    {
                        float pulse = 0.72f +
                            (Mathf.PingPong((time * 1.5f) + (index * 0.28f), 1f) * 0.36f);
                        wisp.localScale = _stealthBaseScales[index] * pulse;
                    }
                }
            }
        }

        public void Present(
            in CombatantSnapshot snapshot,
            int viewerTeamId,
            PersistentAuraVisual persistentMotifs = PersistentAuraVisual.None)
        {
            Initialize();
            AuraVisualMotif motifs = ResolveMotifs(snapshot, viewerTeamId);
            bool hiddenHostile = snapshot.IsAlive &&
                (snapshot.HasAuraTag(AuraTag.Stealth) ||
                 snapshot.HasStatus(StatusId.Stealthed)) &&
                snapshot.Team.Value != viewerTeamId;
            PersistentAuraVisual visiblePersistent = snapshot.IsAlive && !hiddenHostile
                ? persistentMotifs
                : PersistentAuraVisual.None;
            if ((visiblePersistent & PersistentAuraVisual.Dispersion) != 0)
            {
                motifs &= ~AuraVisualMotif.GuardianRunes;
            }

            if ((visiblePersistent & PersistentAuraVisual.PsychicHorror) != 0)
            {
                motifs &= ~AuraVisualMotif.ControlCrown;
            }

            if ((visiblePersistent & PersistentAuraVisual.TrapRoot) != 0)
            {
                motifs &= ~AuraVisualMotif.FrozenSpikes;
            }

            PersistentAuraVisual stingMotifs =
                PersistentAuraVisual.SerpentSting |
                PersistentAuraVisual.ViperSting |
                PersistentAuraVisual.ScorpidSting;
            if ((visiblePersistent & stingMotifs) != 0)
            {
                motifs &= ~AuraVisualMotif.PoisonOrbit;
            }

            if (_activeMotifs == motifs &&
                _activePersistentMotifs == visiblePersistent)
            {
                return;
            }

            _activeMotifs = motifs;
            _activePersistentMotifs = visiblePersistent;
            SetRoot(_wardRoot, AuraVisualMotif.WardBands);
            SetRoot(_guardianRoot, AuraVisualMotif.GuardianRunes);
            SetRoot(_frozenRoot, AuraVisualMotif.FrozenSpikes);
            SetRoot(_controlRoot, AuraVisualMotif.ControlCrown);
            SetRoot(_burningRoot, AuraVisualMotif.FlameWisps);
            SetRoot(_poisonRoot, AuraVisualMotif.PoisonOrbit);
            SetRoot(_stealthRoot, AuraVisualMotif.FriendlyStealthTrace);
            SetRoot(_polymorphRoot, AuraVisualMotif.PolymorphCloud);
            for (int index = 0; index < _persistentRoots.Length; index++)
            {
                PersistentAuraVisual motif = (PersistentAuraVisual)(1 << index);
                SetActive(
                    _persistentRoots[index],
                    (_activePersistentMotifs & motif) != 0);
            }
        }

        public void ResetPresentation()
        {
            _activeMotifs = AuraVisualMotif.None;
            _activePersistentMotifs = PersistentAuraVisual.None;
            SetActive(_wardRoot, false);
            SetActive(_guardianRoot, false);
            SetActive(_frozenRoot, false);
            SetActive(_controlRoot, false);
            SetActive(_burningRoot, false);
            SetActive(_poisonRoot, false);
            SetActive(_stealthRoot, false);
            SetActive(_polymorphRoot, false);
            for (int index = 0; index < _persistentRoots.Length; index++)
            {
                SetActive(_persistentRoots[index], false);
            }
        }

        public static AuraVisualMotif ResolveMotifs(
            in CombatantSnapshot snapshot,
            int viewerTeamId)
        {
            if (!snapshot.IsAlive)
            {
                return AuraVisualMotif.None;
            }

            bool stealthed = snapshot.HasAuraTag(AuraTag.Stealth) ||
                snapshot.HasStatus(StatusId.Stealthed);
            if (stealthed && snapshot.Team.Value != viewerTeamId)
            {
                return AuraVisualMotif.None;
            }

            AuraVisualMotif motifs = AuraVisualMotif.None;
            if (snapshot.Barrier > 0.001f || snapshot.HasAuraTag(AuraTag.Absorb))
            {
                motifs |= AuraVisualMotif.WardBands;
            }

            if (snapshot.HasAuraTag(AuraTag.DamageReduction) ||
                snapshot.HasStatus(StatusId.DamageReduced) ||
                snapshot.HasAuraTag(AuraTag.MagicImmunity))
            {
                motifs |= AuraVisualMotif.GuardianRunes;
            }

            if (snapshot.HasAuraTag(AuraTag.Frozen) ||
                snapshot.HasAuraTag(AuraTag.Root) ||
                snapshot.HasStatus(StatusId.Rooted))
            {
                motifs |= AuraVisualMotif.FrozenSpikes;
            }

            bool polymorphed = snapshot.HasAuraTag(AuraTag.Disorient) &&
                snapshot.HasAuraTag(AuraTag.Periodic);
            bool controlled = !polymorphed && (snapshot.HasAuraTag(AuraTag.Fear) ||
                snapshot.HasAuraTag(AuraTag.Stun) ||
                snapshot.HasAuraTag(AuraTag.Horror) ||
                snapshot.HasAuraTag(AuraTag.Disorient) ||
                snapshot.HasStatus(StatusId.Feared) ||
                snapshot.HasStatus(StatusId.Horrified) ||
                snapshot.HasStatus(StatusId.Stunned) ||
                snapshot.HasStatus(StatusId.Disoriented));
            if (controlled)
            {
                motifs |= AuraVisualMotif.ControlCrown;
            }

            if (polymorphed)
            {
                motifs |= AuraVisualMotif.PolymorphCloud;
            }

            // Periodic is deliberately not enough: Polymorph and future damage-over-time
            // effects also use that tag. Only the authoritative Burning status may show fire.
            if (snapshot.HasStatus(StatusId.Burning))
            {
                motifs |= AuraVisualMotif.FlameWisps;
            }

            if (snapshot.HasAuraTag(AuraTag.Poison) ||
                snapshot.HasStatus(StatusId.Poisoned))
            {
                motifs |= AuraVisualMotif.PoisonOrbit;
            }

            if (stealthed)
            {
                motifs |= AuraVisualMotif.FriendlyStealthTrace;
            }

            return motifs;
        }

        private void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _frozenBaseScales = CaptureScales(_frozenSpikes);
            _controlBasePositions = CapturePositions(_controlRunes);
            _flameBasePositions = CapturePositions(_flameWisps);
            _flameBaseScales = CaptureScales(_flameWisps);
            _stealthBaseScales = CaptureScales(_stealthWisps);
            _persistentBaseScales = CaptureScales(_persistentMotionRoots);

            ApplyColor(_wardRenderers, new Color(0.28f, 0.86f, 1f, 0.58f));
            ApplyColor(_guardianRenderers, new Color(1f, 0.7f, 0.2f, 0.62f));
            ApplyColor(_frozenRenderers, new Color(0.48f, 0.92f, 1f, 0.78f));
            ApplyColor(_controlRenderers, new Color(0.86f, 0.42f, 1f, 0.76f));
            ApplyColor(_burningRenderers, new Color(1f, 0.26f, 0.04f, 0.7f));
            ApplyColor(_poisonRenderers, new Color(0.42f, 0.96f, 0.18f, 0.68f));
            ApplyColor(_stealthRenderers, new Color(0.5f, 0.34f, 0.68f, 0.42f));
            ApplyColor(_polymorphRenderers, new Color(1f, 0.58f, 0.92f, 0.74f));
            ApplyPersistentColors();
            _initialized = true;
        }

        private void ApplyPersistentColors()
        {
            int motifCount = Mathf.Min(
                AuthoredPersistentMotifCount,
                Mathf.Min(
                    _persistentRendererStarts.Length,
                    _persistentRendererCounts.Length));
            for (int motifIndex = 0; motifIndex < motifCount; motifIndex++)
            {
                int start = Mathf.Max(0, _persistentRendererStarts[motifIndex]);
                int end = Mathf.Min(
                    _persistentRenderers.Length,
                    start + Mathf.Max(0, _persistentRendererCounts[motifIndex]));
                Color color = ResolvePersistentColor(motifIndex);
                _propertyBlock ??= new MaterialPropertyBlock();
                for (int rendererIndex = start; rendererIndex < end; rendererIndex++)
                {
                    Renderer renderer = _persistentRenderers[rendererIndex];
                    if (renderer == null)
                    {
                        continue;
                    }

                    renderer.GetPropertyBlock(_propertyBlock);
                    _propertyBlock.SetColor("_BaseColor", color);
                    _propertyBlock.SetColor("_Color", color);
                    _propertyBlock.SetColor("_EmissionColor", color * 1.35f);
                    renderer.SetPropertyBlock(_propertyBlock);
                    _propertyBlock.Clear();
                }
            }
        }

        private static void TickPersistentMotion(
            PersistentAuraVisual motif,
            int index,
            Transform motionRoot,
            Vector3 baseScale,
            float time)
        {
            float phase = time + index * 0.41f;
            float yaw;
            float pitch = 0f;
            float roll = 0f;
            float pulse;
            float bob = 0f;
            switch (motif)
            {
                case PersistentAuraVisual.TrapRoot:
                    yaw = 0f;
                    pulse = 1f + Mathf.Sin(phase * 2.2f) * 0.012f;
                    break;
                case PersistentAuraVisual.HuntersMark:
                    yaw = time * 16f;
                    pulse = 1f + Mathf.Sin(phase * 2.4f) * 0.025f;
                    break;
                case PersistentAuraVisual.Deterrence:
                    yaw = time * 168f;
                    roll = Mathf.Sin(phase * 5.6f) * 4f;
                    pulse = 1f + Mathf.Sin(phase * 5.6f) * 0.035f;
                    break;
                case PersistentAuraVisual.SerpentSting:
                case PersistentAuraVisual.ViperSting:
                case PersistentAuraVisual.ScorpidSting:
                    yaw = time * (motif == PersistentAuraVisual.ViperSting ? -48f : 54f);
                    roll = Mathf.Sin(phase * 3.8f) * 7f;
                    bob = Mathf.Sin(phase * 3.1f) * 0.025f;
                    pulse = 1f + Mathf.Sin(phase * 3.8f) * 0.055f;
                    break;
                case PersistentAuraVisual.Dispersion:
                    yaw = time * -22f;
                    pitch = 8f;
                    pulse = 1f + Mathf.Sin(phase * 2.1f) * 0.025f;
                    break;
                case PersistentAuraVisual.MindFlay:
                    yaw = time * 62f;
                    pitch = Mathf.Sin(phase * 4.6f) * 8f;
                    roll = Mathf.Cos(phase * 3.7f) * 7f;
                    pulse = 1f + Mathf.Sin(phase * 4.6f) * 0.07f;
                    break;
                case PersistentAuraVisual.ShadowWordPain:
                case PersistentAuraVisual.PsychicHorror:
                    yaw = time * (motif == PersistentAuraVisual.PsychicHorror ? -76f : 48f);
                    roll = Mathf.Sin(phase * 7.2f) * 9f;
                    pulse = 1f + Mathf.Sin(phase * 7.2f) * 0.085f;
                    break;
                default:
                    yaw = time * (index % 2 == 0 ? 34f : -34f);
                    pitch = Mathf.Sin(phase * 2.8f) * 4f;
                    pulse = 1f + Mathf.Sin(phase * 2.8f) * 0.06f;
                    break;
            }

            motionRoot.localPosition = Vector3.up * bob;
            motionRoot.localRotation = Quaternion.Euler(pitch, yaw, roll);
            motionRoot.localScale = baseScale * pulse;
        }

        private static Color ResolvePersistentColor(int motifIndex)
        {
            switch ((PersistentAuraVisual)(1 << motifIndex))
            {
                case PersistentAuraVisual.VampiricTouch:
                    return new Color(0.78f, 0.16f, 0.94f, 0.68f);
                case PersistentAuraVisual.ShadowWordPain:
                    return new Color(0.62f, 0.2f, 1f, 0.72f);
                case PersistentAuraVisual.DevouringPlague:
                    return new Color(0.46f, 0.08f, 0.68f, 0.74f);
                case PersistentAuraVisual.MindFlay:
                    return new Color(0.66f, 0.4f, 1f, 0.7f);
                case PersistentAuraVisual.Dispersion:
                    return new Color(0.48f, 0.2f, 0.86f, 0.78f);
                case PersistentAuraVisual.PsychicHorror:
                    return new Color(0.92f, 0.14f, 0.7f, 0.78f);
                case PersistentAuraVisual.SerpentSting:
                    return new Color(0.32f, 0.94f, 0.28f, 0.7f);
                case PersistentAuraVisual.ViperSting:
                    return new Color(0.12f, 0.86f, 0.7f, 0.72f);
                case PersistentAuraVisual.ScorpidSting:
                    return new Color(1f, 0.64f, 0.14f, 0.74f);
                case PersistentAuraVisual.HuntersMark:
                    return new Color(1f, 0.82f, 0.22f, 0.8f);
                case PersistentAuraVisual.TrapRoot:
                    return new Color(0.42f, 0.9f, 1f, 0.78f);
                case PersistentAuraVisual.Deterrence:
                    return new Color(0.76f, 0.84f, 0.94f, 0.82f);
                default:
                    return Color.white;
            }
        }

        private void ApplyColor(Renderer[] renderers, Color color)
        {
            _propertyBlock ??= new MaterialPropertyBlock();
            for (int index = 0; index < renderers.Length; index++)
            {
                Renderer renderer = renderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor("_BaseColor", color);
                _propertyBlock.SetColor("_Color", color);
                _propertyBlock.SetColor("_EmissionColor", color * 1.2f);
                renderer.SetPropertyBlock(_propertyBlock);
                _propertyBlock.Clear();
            }
        }

        private void SetRoot(GameObject root, AuraVisualMotif motif)
        {
            SetActive(root, (_activeMotifs & motif) != 0);
        }

        private static void RotateIfActive(GameObject root, float degrees)
        {
            if (root != null && root.activeSelf)
            {
                root.transform.Rotate(Vector3.up, degrees, Space.Self);
            }
        }

        private static Vector3[] CapturePositions(Transform[] transforms)
        {
            Vector3[] values = new Vector3[transforms.Length];
            for (int index = 0; index < transforms.Length; index++)
            {
                values[index] = transforms[index] != null
                    ? transforms[index].localPosition
                    : Vector3.zero;
            }

            return values;
        }

        private static Vector3[] CaptureScales(Transform[] transforms)
        {
            Vector3[] values = new Vector3[transforms.Length];
            for (int index = 0; index < transforms.Length; index++)
            {
                values[index] = transforms[index] != null
                    ? transforms[index].localScale
                    : Vector3.one;
            }

            return values;
        }

        private static int Length(Array values) => values?.Length ?? 0;

        private static void SetActive(GameObject value, bool active)
        {
            if (value != null && value.activeSelf != active)
            {
                value.SetActive(active);
            }
        }
    }
}
