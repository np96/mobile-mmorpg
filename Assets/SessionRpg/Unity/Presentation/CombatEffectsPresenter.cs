using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CombatEffectsPresenter : MonoBehaviour
    {
        private const int ProjectileArrivalCapacity = 12;

        [SerializeField] private UnityEngine.Camera _gameCamera;
        [SerializeField] private CombatAudioPresenter _audio;
        [SerializeField] private CombatEffectView[] _effectPool = new CombatEffectView[0];
        [SerializeField] private CombatTextView[] _textPool = new CombatTextView[0];
        [SerializeField] private GameObject[] _groundEffectRoots = new GameObject[0];
        [SerializeField] private GameObject[] _freezingTrapRoots = new GameObject[0];
        [SerializeField] private GameObject[] _frostTrapRoots = new GameObject[0];
        [SerializeField] private Renderer[] _groundEffectRenderers = new Renderer[0];
        [SerializeField] private int _groundEffectRenderersPerView;

        private int _nextEffect;
        private int _nextText;
        private readonly ProjectileLaunch[] _projectileLaunches =
            new ProjectileLaunch[ProjectileArrivalCapacity];
        private readonly PendingArrival[] _pendingArrivals =
            new PendingArrival[ProjectileArrivalCapacity];
        private int _nextProjectileLaunch;
        private int _nextPendingArrival;
        private MaterialPropertyBlock _groundEffectPropertyBlock;
        private GroundEffectId[] _groundEffectTypes = new GroundEffectId[0];
        private int[] _groundEffectColorKeys = new int[0];
        private int _activeGroundEffectCount;

        public CombatAudioPresenter Audio => _audio;

        public int ActiveEffectCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _effectPool.Length; index++)
                {
                    if (_effectPool[index] != null && _effectPool[index].IsPlaying)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int PendingProjectileArrivalCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _pendingArrivals.Length; index++)
                {
                    if (_pendingArrivals[index].Active)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int EffectPoolCount => _effectPool != null ? _effectPool.Length : 0;

        public int GroundEffectPoolCount =>
            _groundEffectRoots != null ? _groundEffectRoots.Length : 0;

        public int ActiveGroundEffectCount => _activeGroundEffectCount;

        public bool HasAuthoredGroundEffectRig =>
            GroundEffectPoolCount > 0 &&
            _freezingTrapRoots != null &&
            _freezingTrapRoots.Length == GroundEffectPoolCount &&
            _frostTrapRoots != null &&
            _frostTrapRoots.Length == GroundEffectPoolCount &&
            _groundEffectRenderersPerView > 0 &&
            _groundEffectRenderers != null &&
            _groundEffectRenderers.Length ==
                GroundEffectPoolCount * _groundEffectRenderersPerView;

        public CombatEffectView GetEffect(int index) =>
            index >= 0 && index < _effectPool.Length ? _effectPool[index] : null;

        private void Awake()
        {
            _groundEffectPropertyBlock = new MaterialPropertyBlock();
            int groundEffectCount = GroundEffectPoolCount;
            _groundEffectTypes = new GroundEffectId[groundEffectCount];
            _groundEffectColorKeys = new int[groundEffectCount];
            for (int index = 0; index < _groundEffectColorKeys.Length; index++)
            {
                _groundEffectColorKeys[index] = -1;
            }

            if (_gameCamera == null)
            {
                _gameCamera = UnityEngine.Camera.main;
            }
        }

        public void SetGameCamera(UnityEngine.Camera gameCamera)
        {
            _gameCamera = gameCamera;
        }

        public void SetAudio(CombatAudioPresenter audio)
        {
            _audio = audio;
        }

        public void PresentCastStarted(AbilityId ability, Transform actor)
        {
            if (actor == null ||
                !AbilityPresentationCatalog.TryGet(
                    ability,
                    out AbilityPresentation presentation))
            {
                return;
            }

            AbilityPresentation castStart = new AbilityPresentation(
                ability,
                presentation.Color,
                presentation.Style,
                EffectMotion.Burst,
                Mathf.Clamp(presentation.Size * 0.72f, 0.62f, 1.4f),
                0.3f);
            PlayEffect(
                castStart,
                actor.position,
                actor.position,
                EffectVisualPhase.CastStart);
            _audio?.PlayWorld(CombatSfxCue.CastStart, actor.position);
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            for (int index = 0; index < _effectPool.Length; index++)
            {
                CombatEffectView effect = _effectPool[index];
                if (effect != null && effect.IsPlaying)
                {
                    effect.Tick(deltaTime);
                }
            }

            for (int index = 0; index < _textPool.Length; index++)
            {
                CombatTextView text = _textPool[index];
                if (text != null && text.IsPlaying)
                {
                    text.Tick(deltaTime, _gameCamera);
                }
            }

            TickPendingArrivals();
            TickGroundEffects(deltaTime);
        }

        public void PresentGroundEffects(
            GroundEffectSnapshot[] snapshots,
            int count,
            int viewerTeamId)
        {
            int visibleCount = snapshots == null
                ? 0
                : Mathf.Min(Mathf.Max(0, count), GroundEffectPoolCount);
            _activeGroundEffectCount = visibleCount;
            for (int index = 0; index < GroundEffectPoolCount; index++)
            {
                GameObject root = _groundEffectRoots[index];
                bool active = index < visibleCount && root != null;
                SetActive(root, active);
                if (!active)
                {
                    if (index < _groundEffectTypes.Length)
                    {
                        _groundEffectTypes[index] = GroundEffectId.None;
                    }

                    continue;
                }

                GroundEffectSnapshot snapshot = snapshots[index];
                root.transform.position = new Vector3(
                    snapshot.Position.X,
                    0.045f,
                    snapshot.Position.Z);
                root.transform.rotation = Quaternion.identity;
                root.transform.localScale = Vector3.one * Mathf.Max(0.45f, snapshot.Radius);
                bool freezing = snapshot.Id == GroundEffectId.FreezingTrap;
                SetActive(GetAt(_freezingTrapRoots, index), freezing);
                SetActive(GetAt(_frostTrapRoots, index), !freezing);
                if (index < _groundEffectTypes.Length)
                {
                    _groundEffectTypes[index] = snapshot.Id;
                }

                bool friendly = snapshot.Team.Value == viewerTeamId;
                int colorKey = ((int)snapshot.Id * 2) + (friendly ? 1 : 0);
                if (index < _groundEffectColorKeys.Length &&
                    _groundEffectColorKeys[index] != colorKey)
                {
                    _groundEffectColorKeys[index] = colorKey;
                    ApplyGroundEffectColor(index, snapshot.Id, friendly);
                }
            }
        }

        private void TickGroundEffects(float deltaTime)
        {
            for (int index = 0; index < _activeGroundEffectCount; index++)
            {
                GroundEffectId type = index < _groundEffectTypes.Length
                    ? _groundEffectTypes[index]
                    : GroundEffectId.None;
                GameObject freezing = GetAt(_freezingTrapRoots, index);
                GameObject frost = GetAt(_frostTrapRoots, index);
                if (type == GroundEffectId.FreezingTrap && freezing != null)
                {
                    float pulse = 1f + Mathf.Sin((Time.time * 4.8f) + index) * 0.055f;
                    freezing.transform.localScale = Vector3.one * pulse;
                }
                else if (type == GroundEffectId.FrostTrap && frost != null)
                {
                    frost.transform.Rotate(Vector3.up, 34f * deltaTime, Space.Self);
                    float pulse = 1f + Mathf.Sin((Time.time * 2.4f) + index) * 0.035f;
                    frost.transform.localScale = Vector3.one * pulse;
                }
            }
        }

        private void ApplyGroundEffectColor(
            int viewIndex,
            GroundEffectId type,
            bool friendly)
        {
            if (_groundEffectRenderersPerView <= 0)
            {
                return;
            }

            _groundEffectPropertyBlock ??= new MaterialPropertyBlock();

            Color baseColor = type == GroundEffectId.FreezingTrap
                ? new Color(0.42f, 0.9f, 1f, 0.82f)
                : new Color(0.24f, 0.74f, 1f, 0.7f);
            Color relationColor = friendly
                ? new Color(0.18f, 0.82f, 1f, 1f)
                : new Color(1f, 0.26f, 0.18f, 1f);
            Color color = Color.Lerp(baseColor, relationColor, 0.34f);
            int start = viewIndex * _groundEffectRenderersPerView;
            int end = Mathf.Min(
                _groundEffectRenderers.Length,
                start + _groundEffectRenderersPerView);
            for (int index = start; index < end; index++)
            {
                Renderer renderer = _groundEffectRenderers[index];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_groundEffectPropertyBlock);
                _groundEffectPropertyBlock.SetColor("_BaseColor", color);
                _groundEffectPropertyBlock.SetColor("_Color", color);
                _groundEffectPropertyBlock.SetColor("_EmissionColor", color * 1.45f);
                renderer.SetPropertyBlock(_groundEffectPropertyBlock);
                _groundEffectPropertyBlock.Clear();
            }
        }

        public void PresentCastCompleted(
            AbilityId ability,
            Transform actor,
            Transform target,
            Vector3 targetPoint)
        {
            if (!AbilityPresentationCatalog.TryGet(ability, out AbilityPresentation presentation))
            {
                return;
            }

            Vector3 origin = actor != null ? actor.position : targetPoint;
            Vector3 visualOrigin = ability == AbilityId.ArrowRain
                ? targetPoint + new Vector3(-1.8f, 6.5f, 1.2f)
                : origin;
            Vector3 destination = ability == AbilityId.ArrowRain ||
                UsesGroundedPrimaryDestination(presentation.Motion)
                ? targetPoint
                : target != null
                    ? target.position
                    : targetPoint;
            PlayEffect(presentation, visualOrigin, destination);
            if (IsTravelMotion(presentation.Motion))
            {
                RecordProjectileLaunch(
                    ability,
                    target,
                    visualOrigin,
                    destination,
                    Time.time + Mathf.Max(0.05f, presentation.Duration));
            }

            _audio?.PlayWorld(
                CombatSoundLanguage.ResolveReleaseCue(ability),
                origin);
        }

        public static bool UsesGroundedPrimaryDestination(EffectMotion motion)
        {
            return motion == EffectMotion.Nova ||
                motion == EffectMotion.Burst ||
                motion == EffectMotion.Trap ||
                motion == EffectMotion.BladeStorm;
        }

        public void PresentChannelTick(
            AbilityId ability,
            Transform actor,
            Transform target,
            Vector3 targetPoint)
        {
            if (!AbilityPresentationCatalog.TryGet(
                    ability,
                    out AbilityPresentation presentation))
            {
                return;
            }

            Vector3 origin = actor != null ? actor.position : targetPoint;
            Vector3 destination = target != null ? target.position : targetPoint;
            AbilityPresentation tickPresentation = new AbilityPresentation(
                ability,
                presentation.Color,
                presentation.Style,
                IsTravelMotion(presentation.Motion)
                    ? presentation.Motion
                    : presentation.Motion == EffectMotion.MindChannel
                        ? EffectMotion.MindChannel
                        : EffectMotion.Tether,
                Mathf.Max(0.36f, presentation.Size * 1.05f),
                Mathf.Clamp(presentation.Duration * 0.78f, 0.2f, 0.36f));
            PlayEffect(tickPresentation, origin, destination);
            if (IsTravelMotion(tickPresentation.Motion))
            {
                RecordProjectileLaunch(
                    ability,
                    target,
                    origin,
                    destination,
                    Time.time + Mathf.Max(0.05f, tickPresentation.Duration));
            }
        }

        public void PresentImpact(
            AbilityId ability,
            Transform target,
            Vector3 targetPoint)
        {
            AbilityPresentation presentation = GetPresentationOrFallback(
                ability,
                EffectMotion.Burst,
                new Color(1f, 0.38f, 0.16f));
            Vector3 point = target != null ? target.position : targetPoint;
            PlayEffect(
                CreateImpactPresentation(presentation, false),
                point,
                point,
                EffectVisualPhase.Impact);
        }

        public void PresentMiss(
            AbilityId ability,
            Transform target,
            Vector3 targetPoint)
        {
            AbilityPresentation presentation = GetPresentationOrFallback(
                ability,
                EffectMotion.Burst,
                new Color(0.78f, 0.82f, 0.86f));
            Vector3 point = target != null ? target.position : targetPoint;
            AbilityPresentation missPresentation = CreateImpactPresentation(
                presentation,
                true);
            if (!TryScheduleProjectileArrival(
                    ability,
                    target,
                    point,
                    missPresentation,
                    true,
                    CombatSfxCue.Miss,
                    "MISS",
                    missPresentation.Color,
                    point + (Vector3.up * 1.65f)))
            {
                PlayEffect(
                    missPresentation,
                    point,
                    point,
                    EffectVisualPhase.Miss);
                _audio?.PlayWorld(CombatSfxCue.Miss, point);
                PlayText(
                    "MISS",
                    missPresentation.Color,
                    point + (Vector3.up * 1.65f));
            }
        }

        public void PresentResolution(
            AbilityId ability,
            Transform target,
            Vector3 targetPoint,
            bool connected)
        {
            if (connected)
            {
                PresentImpact(ability, target, targetPoint);
            }
            else
            {
                PresentMiss(ability, target, targetPoint);
            }
        }

        public void PresentDamage(AbilityId ability, Transform actor, Transform target, float amount)
        {
            Vector3 point = target != null ? target.position : actor != null ? actor.position : transform.position;
            AbilityPresentation presentation = GetPresentationOrFallback(ability, EffectMotion.Burst, new Color(1f, 0.3f, 0.12f));
            AbilityPresentation impact = CreateImpactPresentation(presentation, false);
            SpellVisualProfile visualProfile = SpellVisualLanguage.ResolveProfile(ability);
            if (visualProfile == SpellVisualProfile.Physical ||
                visualProfile == SpellVisualProfile.Venom)
            {
                impact = new AbilityPresentation(
                    impact.Ability,
                    impact.Color,
                    impact.Style,
                    EffectMotion.Burst,
                    Mathf.Clamp(presentation.Size * 0.46f, 0.3f, 0.58f),
                    0.12f);
            }

            string damageText = "-" + Mathf.CeilToInt(amount);
            Vector3 textPoint = point + (Vector3.up * 0.52f);
            if (!TryScheduleProjectileArrival(
                    ability,
                    target,
                    point,
                    impact,
                    false,
                    CombatSoundLanguage.ResolveImpactCue(ability),
                    damageText,
                    presentation.Color,
                    textPoint))
            {
                PlayEffect(impact, point, point, EffectVisualPhase.Impact);
                _audio?.PlayWorld(
                    CombatSoundLanguage.ResolveImpactCue(ability),
                    point);
                PlayText(damageText, presentation.Color, textPoint);
            }
        }

        public void PresentAbsorbed(AbilityId ability, Transform target)
        {
            Vector3 point = target != null ? target.position : transform.position;
            AbilityPresentation presentation = GetPresentationOrFallback(
                ability,
                EffectMotion.Shield,
                new Color(0.25f, 0.82f, 1f));
            PlayEffect(
                new AbilityPresentation(
                    presentation.Ability,
                    new Color(0.25f, 0.82f, 1f),
                    "absorbed hit",
                    EffectMotion.Shield,
                    1.05f,
                    0.28f),
                point,
                point);
            PlayText("ABSORBED", new Color(0.4f, 0.88f, 1f), point + Vector3.up * 1.65f);
            _audio?.PlayWorld(CombatSfxCue.Barrier, point);
        }

        public void PresentMitigated(AbilityId ability, Transform target, float amount)
        {
            Vector3 point = target != null ? target.position : transform.position;
            AbilityPresentation presentation = GetPresentationOrFallback(
                ability,
                EffectMotion.Shield,
                new Color(1f, 0.72f, 0.2f));
            Color mitigationColor = Color.Lerp(
                presentation.Color,
                new Color(1f, 0.76f, 0.24f),
                0.62f);
            AbilityPresentation mitigation = new AbilityPresentation(
                presentation.Ability,
                mitigationColor,
                "mitigated hit",
                EffectMotion.Shield,
                1.14f,
                0.3f);
            PlayEffect(mitigation, point, point);
            PlayText(
                "REDUCED " + Mathf.CeilToInt(amount),
                mitigationColor,
                point + (Vector3.up * 1.65f));
        }

        public void PresentHeal(AbilityId ability, Transform actor, Transform target, float amount)
        {
            Vector3 origin = actor != null ? actor.position : transform.position;
            Vector3 point = target != null ? target.position : origin;
            AbilityPresentation presentation = GetPresentationOrFallback(ability, EffectMotion.Tether, new Color(0.25f, 1f, 0.56f));
            PlayEffect(presentation, origin, point);
            PlayText("+" + Mathf.CeilToInt(amount), presentation.Color, point + Vector3.up * 1.6f);
            _audio?.PlayWorld(CombatSfxCue.Heal, point);
        }

        public void PresentBarrier(AbilityId ability, Transform target, float amount)
        {
            Vector3 point = target != null ? target.position : transform.position;
            AbilityPresentation presentation = GetPresentationOrFallback(ability, EffectMotion.Shield, new Color(0.2f, 0.8f, 1f));
            PlayEffect(presentation, point, point);
            PlayText("WARD " + Mathf.CeilToInt(amount), presentation.Color, point + Vector3.up * 1.6f);
            _audio?.PlayWorld(CombatSfxCue.Barrier, point);
        }

        public void PresentInterrupted(AbilityId ability, Transform target)
        {
            Vector3 point = target != null ? target.position : transform.position;
            AbilityPresentation presentation = GetPresentationOrFallback(ability, EffectMotion.Burst, new Color(1f, 0.22f, 0.64f));
            PlayEffect(presentation, point, point);
            PlayText("INTERRUPT", presentation.Color, point + Vector3.up * 1.7f);
            _audio?.PlayWorld(CombatSfxCue.Interrupt, point);
        }

        public void PresentKnockback(AbilityId ability, Transform actor, Transform target, Vector3 displacement)
        {
            Vector3 point = target != null ? target.position : actor != null ? actor.position : transform.position;
            AbilityPresentation presentation = GetPresentationOrFallback(ability, EffectMotion.Nova, new Color(1f, 0.32f, 0.08f));
            PlayEffect(presentation, actor != null ? actor.position : point - displacement, point);
            PlayText("PUSH", presentation.Color, point + Vector3.up * 1.65f);
            _audio?.PlayWorld(CombatSfxCue.Knockback, point);
        }

        public void PresentStatus(StatusId status, Transform target)
        {
            Vector3 point = target != null ? target.position : transform.position;
            Color color;
            string label;
            switch (status)
            {
                case StatusId.Silenced:
                    color = new Color(1f, 0.25f, 0.6f);
                    label = "SILENCED";
                    break;
                case StatusId.Slowed:
                    color = new Color(0.45f, 0.9f, 1f);
                    label = "SLOWED";
                    break;
                case StatusId.Anchored:
                    color = new Color(1f, 0.72f, 0.16f);
                    label = "BRACED";
                    break;
                case StatusId.Feared:
                    color = new Color(0.68f, 0.34f, 0.92f);
                    label = "FEARED";
                    break;
                case StatusId.Horrified:
                    color = new Color(0.94f, 0.24f, 0.72f);
                    label = "HORRIFIED";
                    break;
                case StatusId.Stunned:
                    color = new Color(1f, 0.78f, 0.2f);
                    label = "STUNNED";
                    break;
                case StatusId.Disoriented:
                    color = new Color(0.86f, 0.52f, 1f);
                    label = "DISORIENTED";
                    break;
                case StatusId.Rooted:
                    color = new Color(0.42f, 0.86f, 1f);
                    label = "ROOTED";
                    break;
                case StatusId.DamageReduced:
                    color = new Color(1f, 0.74f, 0.28f);
                    label = "PROTECTED";
                    break;
                case StatusId.Stealthed:
                    color = new Color(0.6f, 0.56f, 0.78f);
                    label = "STEALTH";
                    break;
                case StatusId.Poisoned:
                    color = new Color(0.48f, 0.92f, 0.26f);
                    label = "POISONED";
                    break;
                case StatusId.Burning:
                    color = new Color(1f, 0.38f, 0.12f);
                    label = "BURNING";
                    break;
                case StatusId.SchoolLocked:
                    color = new Color(1f, 0.2f, 0.55f);
                    label = "LOCKED";
                    break;
                case StatusId.Disarmed:
                    color = new Color(1f, 0.5f, 0.2f);
                    label = "DISARMED";
                    break;
                case StatusId.Pacified:
                    color = new Color(0.66f, 0.76f, 0.92f);
                    label = "PACIFIED";
                    break;
                default:
                    color = new Color(0.72f, 0.86f, 1f);
                    label = "WARD";
                    break;
            }

            PlayText(label, color, point + Vector3.up * 1.7f);
            if (IsControlStatus(status))
            {
                _audio?.PlayWorld(CombatSfxCue.Control, point);
            }
        }

        public void PresentCleanse(Transform target)
        {
            Vector3 point = target != null ? target.position : transform.position;
            AbilityPresentation presentation = GetPresentationOrFallback(
                AbilityId.Purify,
                EffectMotion.Burst,
                new Color(0.55f, 1f, 0.7f));
            PlayEffect(presentation, point, point);
            PlayText("CLEANSED", presentation.Color, point + Vector3.up * 1.7f);
            _audio?.PlayWorld(CombatSfxCue.Heal, point);
        }

        public void PresentRemovalFeedback(
            AbilityId ability,
            Transform target,
            string label)
        {
            Vector3 point = target != null ? target.position : transform.position;
            AbilityPresentation source = GetPresentationOrFallback(
                ability,
                EffectMotion.Burst,
                new Color(0.64f, 0.86f, 1f));
            AbilityPresentation presentation = new AbilityPresentation(
                source.Ability,
                source.Color,
                source.Style,
                EffectMotion.Burst,
                Mathf.Max(0.9f, source.Size),
                0.32f);
            PlayEffect(presentation, point, point);
            PlayText(label, presentation.Color, point + Vector3.up * 1.7f);
            _audio?.PlayWorld(
                CombatSoundLanguage.ResolveImpactCue(ability),
                point);
        }

        public void PresentDeath(Transform target)
        {
            Vector3 point = target != null ? target.position : transform.position;
            _audio?.PlayWorld(CombatSfxCue.Death, point);
        }

        public void PresentResult(bool won, bool draw)
        {
            CombatSfxCue cue = draw
                ? CombatSfxCue.ResultDraw
                : won
                    ? CombatSfxCue.ResultVictory
                    : CombatSfxCue.ResultDefeat;
            _audio?.PlayUi(cue);
        }

        public void PresentUiSelect()
        {
            _audio?.PlayUi(CombatSfxCue.UiSelect);
        }

        public void PresentUiConfirm()
        {
            _audio?.PlayUi(CombatSfxCue.UiConfirm);
        }

        public void PresentUiRejected()
        {
            _audio?.PlayUi(CombatSfxCue.UiReject);
        }

        public void PresentTargetSelected()
        {
            _audio?.PlayUi(CombatSfxCue.TargetSelect);
        }

        private static bool IsControlStatus(StatusId status)
        {
            switch (status)
            {
                case StatusId.Silenced:
                case StatusId.Feared:
                case StatusId.Horrified:
                case StatusId.Stunned:
                case StatusId.Disoriented:
                case StatusId.Rooted:
                case StatusId.SchoolLocked:
                case StatusId.Disarmed:
                case StatusId.Pacified:
                    return true;
                default:
                    return false;
            }
        }

        private AbilityPresentation GetPresentationOrFallback(
            AbilityId ability,
            EffectMotion fallbackMotion,
            Color fallbackColor)
        {
            return AbilityPresentationCatalog.TryGet(ability, out AbilityPresentation presentation)
                ? presentation
                : new AbilityPresentation(AbilityId.None, fallbackColor, "combat", fallbackMotion, 1f, 0.22f);
        }

        private static AbilityPresentation CreateImpactPresentation(
            AbilityPresentation source,
            bool missed)
        {
            EffectMotion motion = ResolveImpactMotion(source.Ability);
            float size = motion == EffectMotion.Nova
                ? Mathf.Max(1.05f, source.Size * 1.08f)
                : Mathf.Max(0.72f, source.Size * (missed ? 0.90f : 1.5f));
            Color color = missed
                ? Color.Lerp(source.Color, new Color(0.82f, 0.86f, 0.9f), 0.68f)
                : source.Color;
            color.a = missed ? 0.72f : color.a;
            return new AbilityPresentation(
                source.Ability,
                color,
                source.Style,
                motion,
                size,
                missed ? 0.3f : 0.24f);
        }

        private static EffectMotion ResolveImpactMotion(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.Gust:
                case AbilityId.FrostNova:
                case AbilityId.BossKnockback:
                case AbilityId.PsychicScream:
                case AbilityId.BlastWave:
                case AbilityId.Flare:
                case AbilityId.ConeOfCold:
                case AbilityId.WaterElementalFreeze:
                case AbilityId.ColdSnap:
                case AbilityId.Preparation:
                case AbilityId.DawnNova:
                case AbilityId.UmbralCollapse:
                case AbilityId.FlameSurge:
                case AbilityId.Blizzard:
                    return EffectMotion.Nova;
                case AbilityId.BladeTempest:
                    return EffectMotion.BladeStorm;
                case AbilityId.ArrowRain:
                    return EffectMotion.Volley;
                case AbilityId.Aegis:
                case AbilityId.Anchor:
                case AbilityId.PowerWordShield:
                case AbilityId.PainSuppression:
                case AbilityId.CloakOfShadows:
                case AbilityId.IceBarrier:
                case AbilityId.PowerInfusion:
                case AbilityId.Evasion:
                case AbilityId.Dispersion:
                case AbilityId.Shadowform:
                case AbilityId.Deterrence:
                case AbilityId.AspectOfTheDragonhawk:
                case AbilityId.AspectOfTheViper:
                case AbilityId.PrayerOfMending:
                case AbilityId.InnerFocus:
                case AbilityId.FearWard:
                case AbilityId.IceBlock:
                case AbilityId.ManaShield:
                case AbilityId.MoltenArmor:
                case AbilityId.MageArmor:
                case AbilityId.IceArmor:
                    return EffectMotion.Shield;
                case AbilityId.ArenaTrinket:
                case AbilityId.Combustion:
                case AbilityId.IcyVeins:
                    return EffectMotion.Nova;
                default:
                    return EffectMotion.Burst;
            }
        }

        private void PlayEffect(
            AbilityPresentation presentation,
            Vector3 origin,
            Vector3 destination,
            EffectVisualPhase phase = EffectVisualPhase.Primary)
        {
            CombatEffectView effect = GetNextEffect();
            effect?.Play(presentation, origin, destination, phase);
        }

        private void RecordProjectileLaunch(
            AbilityId ability,
            Transform target,
            Vector3 origin,
            Vector3 targetPoint,
            float arrivalTime)
        {
            _projectileLaunches[_nextProjectileLaunch] = new ProjectileLaunch(
                ability,
                target,
                origin,
                targetPoint,
                arrivalTime);
            _nextProjectileLaunch =
                (_nextProjectileLaunch + 1) % _projectileLaunches.Length;
        }

        private bool TryScheduleProjectileArrival(
            AbilityId ability,
            Transform target,
            Vector3 targetPoint,
            AbilityPresentation presentation,
            bool missed,
            CombatSfxCue cue,
            string text,
            Color textColor,
            Vector3 textPoint)
        {
            float now = Time.time;
            int matchIndex = -1;
            float bestAge = float.MaxValue;
            for (int index = 0; index < _projectileLaunches.Length; index++)
            {
                ProjectileLaunch launch = _projectileLaunches[index];
                if (!launch.Active || launch.Ability != ability)
                {
                    continue;
                }

                bool sameTarget = target != null &&
                    launch.Target != null &&
                    (launch.Target == target ||
                     launch.Target.IsChildOf(target) ||
                     target.IsChildOf(launch.Target));
                bool samePoint = target == null &&
                    (launch.TargetPoint - targetPoint).sqrMagnitude <= 1f;
                if (!sameTarget && !samePoint)
                {
                    continue;
                }

                float age = Mathf.Abs(launch.ArrivalTime - now);
                if (age < bestAge)
                {
                    bestAge = age;
                    matchIndex = index;
                }
            }

            if (matchIndex < 0)
            {
                return false;
            }

            ProjectileLaunch matched = _projectileLaunches[matchIndex];
            _projectileLaunches[matchIndex] = default;
            _pendingArrivals[_nextPendingArrival] = new PendingArrival(
                presentation,
                target,
                matched.Origin,
                targetPoint,
                Mathf.Max(now, matched.ArrivalTime),
                missed,
                cue,
                text,
                textColor,
                textPoint);
            _nextPendingArrival =
                (_nextPendingArrival + 1) % _pendingArrivals.Length;
            return true;
        }

        private void TickPendingArrivals()
        {
            float now = Time.time;
            for (int index = 0; index < _pendingArrivals.Length; index++)
            {
                PendingArrival arrival = _pendingArrivals[index];
                if (!arrival.Active || now < arrival.ArrivalTime)
                {
                    continue;
                }

                Vector3 point = arrival.Target != null
                    ? arrival.Target.position
                    : arrival.TargetPoint;
                PlayEffect(
                    arrival.Presentation,
                    arrival.Origin,
                    point,
                    arrival.Missed ? EffectVisualPhase.Miss : EffectVisualPhase.Impact);
                _audio?.PlayWorld(arrival.Cue, point);
                if (!string.IsNullOrEmpty(arrival.Text))
                {
                    Vector3 textPoint = arrival.Target != null
                        ? point + Vector3.up * 1.6f
                        : arrival.TextPoint;
                    PlayText(arrival.Text, arrival.TextColor, textPoint);
                }

                _pendingArrivals[index] = default;
            }
        }

        private void PlayText(string value, Color color, Vector3 position)
        {
            CombatTextView text = GetNextText();
            text?.Play(value, color, position, 0.65f);
        }

        private static GameObject GetAt(GameObject[] values, int index)
        {
            return values != null && index >= 0 && index < values.Length
                ? values[index]
                : null;
        }

        private static bool IsTravelMotion(EffectMotion motion)
        {
            return motion == EffectMotion.Projectile ||
                motion == EffectMotion.HunterArrow;
        }

        private static void SetActive(GameObject value, bool active)
        {
            if (value != null && value.activeSelf != active)
            {
                value.SetActive(active);
            }
        }

        private CombatEffectView GetNextEffect()
        {
            if (_effectPool.Length == 0)
            {
                return null;
            }

            for (int offset = 0; offset < _effectPool.Length; offset++)
            {
                int index = (_nextEffect + offset) % _effectPool.Length;
                CombatEffectView effect = _effectPool[index];
                if (effect != null && !effect.IsPlaying)
                {
                    _nextEffect = (index + 1) % _effectPool.Length;
                    return effect;
                }
            }

            CombatEffectView replacement = _effectPool[_nextEffect];
            _nextEffect = (_nextEffect + 1) % _effectPool.Length;
            return replacement;
        }

        private CombatTextView GetNextText()
        {
            if (_textPool.Length == 0)
            {
                return null;
            }

            for (int offset = 0; offset < _textPool.Length; offset++)
            {
                int index = (_nextText + offset) % _textPool.Length;
                CombatTextView text = _textPool[index];
                if (text != null && !text.IsPlaying)
                {
                    _nextText = (index + 1) % _textPool.Length;
                    return text;
                }
            }

            CombatTextView replacement = _textPool[_nextText];
            _nextText = (_nextText + 1) % _textPool.Length;
            return replacement;
        }

        private readonly struct ProjectileLaunch
        {
            public ProjectileLaunch(
                AbilityId ability,
                Transform target,
                Vector3 origin,
                Vector3 targetPoint,
                float arrivalTime)
            {
                Ability = ability;
                Target = target;
                Origin = origin;
                TargetPoint = targetPoint;
                ArrivalTime = arrivalTime;
                Active = true;
            }

            public AbilityId Ability { get; }

            public Transform Target { get; }

            public Vector3 Origin { get; }

            public Vector3 TargetPoint { get; }

            public float ArrivalTime { get; }

            public bool Active { get; }
        }

        private readonly struct PendingArrival
        {
            public PendingArrival(
                AbilityPresentation presentation,
                Transform target,
                Vector3 origin,
                Vector3 targetPoint,
                float arrivalTime,
                bool missed,
                CombatSfxCue cue,
                string text,
                Color textColor,
                Vector3 textPoint)
            {
                Presentation = presentation;
                Target = target;
                Origin = origin;
                TargetPoint = targetPoint;
                ArrivalTime = arrivalTime;
                Missed = missed;
                Cue = cue;
                Text = text;
                TextColor = textColor;
                TextPoint = textPoint;
                Active = true;
            }

            public AbilityPresentation Presentation { get; }

            public Transform Target { get; }

            public Vector3 Origin { get; }

            public Vector3 TargetPoint { get; }

            public float ArrivalTime { get; }

            public bool Missed { get; }

            public CombatSfxCue Cue { get; }

            public string Text { get; }

            public Color TextColor { get; }

            public Vector3 TextPoint { get; }

            public bool Active { get; }
        }
    }
}
