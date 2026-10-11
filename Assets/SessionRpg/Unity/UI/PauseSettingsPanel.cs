using System;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Unity.UI
{
    public readonly struct LocalSettingsSelection
    {
        public LocalSettingsSelection(
            int masterVolumePercent,
            int effectsVolumePercent,
            bool hapticsEnabled,
            bool reducedMotion,
            bool highContrast)
        {
            MasterVolumePercent = Mathf.Clamp(masterVolumePercent, 0, 100);
            EffectsVolumePercent = Mathf.Clamp(effectsVolumePercent, 0, 100);
            HapticsEnabled = hapticsEnabled;
            ReducedMotion = reducedMotion;
            HighContrast = highContrast;
        }

        public int MasterVolumePercent { get; }
        public int EffectsVolumePercent { get; }
        public bool HapticsEnabled { get; }
        public bool ReducedMotion { get; }
        public bool HighContrast { get; }
    }

    /// <summary>
    /// Offline pause and local accessibility/audio controls. It reports intent;
    /// the game controller owns simulation pausing and persistence.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseSettingsPanel : MonoBehaviour
    {
        private const int VolumeStep = 10;

        [SerializeField] private GameObject _panelRoot;
        [SerializeField] private Button _openButton;
        [SerializeField] private Button _resumeButton;
        [SerializeField] private Button _leaveButton;
        [SerializeField] private Button _masterDownButton;
        [SerializeField] private Button _masterUpButton;
        [SerializeField] private Button _effectsDownButton;
        [SerializeField] private Button _effectsUpButton;
        [SerializeField] private Button _hapticsButton;
        [SerializeField] private Button _reducedMotionButton;
        [SerializeField] private Button _highContrastButton;
        [SerializeField] private Button _tutorialResetButton;
        [SerializeField] private Text _masterLabel;
        [SerializeField] private Text _effectsLabel;
        [SerializeField] private Text _hapticsLabel;
        [SerializeField] private Text _reducedMotionLabel;
        [SerializeField] private Text _highContrastLabel;

        private LocalSettingsSelection _settings = new LocalSettingsSelection(
            100,
            100,
            true,
            false,
            false);

        public event Action<bool> PauseChanged;
        public event Action LeaveRequested;
        public event Action TutorialResetRequested;
        public event Action<LocalSettingsSelection> SettingsChanged;

        public bool IsPaused => _panelRoot != null && _panelRoot.activeSelf;

        public LocalSettingsSelection Settings => _settings;

        public void Apply(LocalSettingsSelection settings, bool notify = false)
        {
            _settings = settings;
            RefreshLabels();
            if (notify)
            {
                SettingsChanged?.Invoke(_settings);
            }
        }

        public void SetPaused(bool paused, bool notify = false)
        {
            if (_panelRoot != null)
            {
                _panelRoot.SetActive(paused);
            }

            if (notify)
            {
                PauseChanged?.Invoke(paused);
            }
        }

        private void OnEnable()
        {
            _openButton?.onClick.AddListener(Open);
            _resumeButton?.onClick.AddListener(Resume);
            _leaveButton?.onClick.AddListener(Leave);
            _masterDownButton?.onClick.AddListener(MasterDown);
            _masterUpButton?.onClick.AddListener(MasterUp);
            _effectsDownButton?.onClick.AddListener(EffectsDown);
            _effectsUpButton?.onClick.AddListener(EffectsUp);
            _hapticsButton?.onClick.AddListener(ToggleHaptics);
            _reducedMotionButton?.onClick.AddListener(ToggleReducedMotion);
            _highContrastButton?.onClick.AddListener(ToggleHighContrast);
            _tutorialResetButton?.onClick.AddListener(ResetTutorial);
            RefreshLabels();
        }

        private void OnDisable()
        {
            _openButton?.onClick.RemoveListener(Open);
            _resumeButton?.onClick.RemoveListener(Resume);
            _leaveButton?.onClick.RemoveListener(Leave);
            _masterDownButton?.onClick.RemoveListener(MasterDown);
            _masterUpButton?.onClick.RemoveListener(MasterUp);
            _effectsDownButton?.onClick.RemoveListener(EffectsDown);
            _effectsUpButton?.onClick.RemoveListener(EffectsUp);
            _hapticsButton?.onClick.RemoveListener(ToggleHaptics);
            _reducedMotionButton?.onClick.RemoveListener(ToggleReducedMotion);
            _highContrastButton?.onClick.RemoveListener(ToggleHighContrast);
            _tutorialResetButton?.onClick.RemoveListener(ResetTutorial);
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(
            GameObject panelRoot,
            Button openButton,
            Button resumeButton,
            Button leaveButton,
            Button masterDownButton,
            Button masterUpButton,
            Button effectsDownButton,
            Button effectsUpButton,
            Button hapticsButton,
            Button reducedMotionButton,
            Button highContrastButton,
            Button tutorialResetButton,
            Text masterLabel,
            Text effectsLabel,
            Text hapticsLabel,
            Text reducedMotionLabel,
            Text highContrastLabel)
        {
            _panelRoot = panelRoot;
            _openButton = openButton;
            _resumeButton = resumeButton;
            _leaveButton = leaveButton;
            _masterDownButton = masterDownButton;
            _masterUpButton = masterUpButton;
            _effectsDownButton = effectsDownButton;
            _effectsUpButton = effectsUpButton;
            _hapticsButton = hapticsButton;
            _reducedMotionButton = reducedMotionButton;
            _highContrastButton = highContrastButton;
            _tutorialResetButton = tutorialResetButton;
            _masterLabel = masterLabel;
            _effectsLabel = effectsLabel;
            _hapticsLabel = hapticsLabel;
            _reducedMotionLabel = reducedMotionLabel;
            _highContrastLabel = highContrastLabel;
        }
#endif

        private void Open() => SetPaused(true, true);

        private void Resume() => SetPaused(false, true);

        private void Leave()
        {
            SetPaused(false, true);
            LeaveRequested?.Invoke();
        }

        private void MasterDown() => ChangeVolumes(-VolumeStep, 0);

        private void MasterUp() => ChangeVolumes(VolumeStep, 0);

        private void EffectsDown() => ChangeVolumes(0, -VolumeStep);

        private void EffectsUp() => ChangeVolumes(0, VolumeStep);

        private void ChangeVolumes(int masterDelta, int effectsDelta)
        {
            Apply(new LocalSettingsSelection(
                _settings.MasterVolumePercent + masterDelta,
                _settings.EffectsVolumePercent + effectsDelta,
                _settings.HapticsEnabled,
                _settings.ReducedMotion,
                _settings.HighContrast), true);
        }

        private void ToggleHaptics()
        {
            Apply(new LocalSettingsSelection(
                _settings.MasterVolumePercent,
                _settings.EffectsVolumePercent,
                !_settings.HapticsEnabled,
                _settings.ReducedMotion,
                _settings.HighContrast), true);
        }

        private void ToggleReducedMotion()
        {
            Apply(new LocalSettingsSelection(
                _settings.MasterVolumePercent,
                _settings.EffectsVolumePercent,
                _settings.HapticsEnabled,
                !_settings.ReducedMotion,
                _settings.HighContrast), true);
        }

        private void ToggleHighContrast()
        {
            Apply(new LocalSettingsSelection(
                _settings.MasterVolumePercent,
                _settings.EffectsVolumePercent,
                _settings.HapticsEnabled,
                _settings.ReducedMotion,
                !_settings.HighContrast), true);
        }

        private void ResetTutorial()
        {
            TutorialResetRequested?.Invoke();
        }

        private void RefreshLabels()
        {
            SetLabel(_masterLabel, "MASTER  " + _settings.MasterVolumePercent + "%");
            SetLabel(_effectsLabel, "EFFECTS  " + _settings.EffectsVolumePercent + "%");
            SetLabel(_hapticsLabel, "HAPTICS  " + OnOff(_settings.HapticsEnabled));
            SetLabel(
                _reducedMotionLabel,
                "REDUCED MOTION  " + OnOff(_settings.ReducedMotion));
            SetLabel(
                _highContrastLabel,
                "HIGH CONTRAST  " + OnOff(_settings.HighContrast));
        }

        private static void SetLabel(Text label, string value)
        {
            if (label != null && label.text != value)
            {
                label.text = value;
            }
        }

        private static string OnOff(bool value) => value ? "ON" : "OFF";
    }
}
