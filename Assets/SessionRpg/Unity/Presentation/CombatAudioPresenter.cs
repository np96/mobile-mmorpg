using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CombatAudioPresenter : MonoBehaviour
    {
        [SerializeField] private CombatSfxPalette _palette;
        [SerializeField] private AudioSource[] _worldSources = new AudioSource[0];
        [SerializeField] private AudioSource[] _uiSources = new AudioSource[0];
        [SerializeField, Range(0f, 1f)] private float _masterVolume = 0.72f;

        private int _nextWorldSource;
        private int _nextUiSource;

        public CombatSfxPalette Palette => _palette;

        public int WorldSourceCount => _worldSources != null ? _worldSources.Length : 0;

        public int UiSourceCount => _uiSources != null ? _uiSources.Length : 0;

        public int TotalSourceCount => WorldSourceCount + UiSourceCount;

        public int PlaybackCount { get; private set; }

        public float MasterVolume => _masterVolume;

        public CombatSfxCue LastCue { get; private set; }

        public void SetMasterVolume(float normalizedVolume)
        {
            _masterVolume = Mathf.Clamp01(normalizedVolume);
        }

        public bool PlayWorld(CombatSfxCue cue, Vector3 position)
        {
            return TryPlay(cue, position, _worldSources, ref _nextWorldSource);
        }

        public bool PlayUi(CombatSfxCue cue)
        {
            return TryPlay(cue, transform.position, _uiSources, ref _nextUiSource);
        }

        private bool TryPlay(
            CombatSfxCue cue,
            Vector3 position,
            AudioSource[] sources,
            ref int nextIndex)
        {
            if (_palette == null || sources == null || sources.Length == 0)
            {
                return false;
            }

            AudioClip clip = _palette.GetClip(cue);
            if (clip == null)
            {
                return false;
            }

            AudioSource source = GetNextAvailableSource(sources, ref nextIndex);
            if (source == null)
            {
                return false;
            }

            source.Stop();
            source.transform.position = position;
            source.clip = clip;
            source.volume = _masterVolume * ResolveCueVolume(cue);
            source.pitch = 1f;
            source.Play();
            PlaybackCount++;
            LastCue = cue;
            return true;
        }

        private static AudioSource GetNextAvailableSource(
            AudioSource[] sources,
            ref int nextIndex)
        {
            for (int offset = 0; offset < sources.Length; offset++)
            {
                int index = (nextIndex + offset) % sources.Length;
                AudioSource candidate = sources[index];
                if (candidate != null && !candidate.isPlaying)
                {
                    nextIndex = (index + 1) % sources.Length;
                    return candidate;
                }
            }

            AudioSource replacement = sources[nextIndex];
            nextIndex = (nextIndex + 1) % sources.Length;
            return replacement;
        }

        private static float ResolveCueVolume(CombatSfxCue cue)
        {
            switch (cue)
            {
                case CombatSfxCue.Interrupt:
                case CombatSfxCue.Knockback:
                case CombatSfxCue.Death:
                    return 0.3f;
                case CombatSfxCue.ResultVictory:
                case CombatSfxCue.ResultDefeat:
                    return 0.34f;
                case CombatSfxCue.ResultDraw:
                    return 0.28f;
                case CombatSfxCue.ImpactMagic:
                case CombatSfxCue.ImpactPhysical:
                case CombatSfxCue.ImpactRadiant:
                case CombatSfxCue.ImpactFire:
                case CombatSfxCue.ImpactFrost:
                case CombatSfxCue.ImpactShadow:
                case CombatSfxCue.ImpactArcane:
                case CombatSfxCue.ImpactVenom:
                case CombatSfxCue.Hazard:
                    return 0.24f;
                case CombatSfxCue.Heal:
                case CombatSfxCue.Barrier:
                case CombatSfxCue.Control:
                case CombatSfxCue.CastReleaseMagic:
                case CombatSfxCue.CastReleasePhysical:
                case CombatSfxCue.CastReleaseRadiant:
                case CombatSfxCue.CastReleaseFire:
                case CombatSfxCue.CastReleaseFrost:
                case CombatSfxCue.CastReleaseShadow:
                case CombatSfxCue.CastReleaseArcane:
                case CombatSfxCue.CastReleaseVenom:
                    return 0.22f;
                case CombatSfxCue.CastStart:
                    return 0.17f;
                case CombatSfxCue.UiConfirm:
                case CombatSfxCue.UiReject:
                    return 0.2f;
                case CombatSfxCue.UiSelect:
                case CombatSfxCue.TargetSelect:
                case CombatSfxCue.Miss:
                    return 0.15f;
                default:
                    return 0.18f;
            }
        }

        private void OnDisable()
        {
            StopSources(_worldSources);
            StopSources(_uiSources);
        }

        private static void StopSources(AudioSource[] sources)
        {
            if (sources == null)
            {
                return;
            }

            for (int index = 0; index < sources.Length; index++)
            {
                sources[index]?.Stop();
            }
        }
    }
}
