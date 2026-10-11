using System;
using System.IO;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.UI;
using UnityEngine;

namespace SessionRpg.Unity.Product
{
    /// <summary>
    /// Device-local, account-free persistence and PII-free playtest logging.
    /// Corrupt or unknown profiles fail closed to version-one defaults.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class LocalProductServices : MonoBehaviour
    {
        private const string ProfileFileName = "profile-v1.json";
        private const string SessionLogFileName = "playtest-v1.jsonl";

        private LocalPlayerProfileV1 _profile;
        private LocalJsonLineSessionSink _recordSink;
        private SessionRecordWriter _recordWriter;
        private int _localSessionSequence;
        private int _recordSequence;

        public LocalPlayerProfileV1 Profile => _profile ??
            LocalPlayerProfileSchemaV1.CreateDefault();

        public bool RecoveredCorruptProfile { get; private set; }

        public string ProfilePath { get; private set; }

        public string SessionLogPath { get; private set; }

        private void Awake()
        {
            ProfilePath = Path.Combine(
                UnityEngine.Application.persistentDataPath,
                ProfileFileName);
            SessionLogPath = Path.Combine(
                UnityEngine.Application.persistentDataPath,
                SessionLogFileName);
            _profile = LoadProfile(ProfilePath, out bool recovered);
            RecoveredCorruptProfile = recovered;
            _recordSink = new LocalJsonLineSessionSink(SessionLogPath);
            _recordWriter = new SessionRecordWriter(_recordSink);
            ApplyMasterVolume(_profile.Settings.MasterVolumePercent);
        }

        public bool ShouldRecommendTraining(SpecializationId specialization)
        {
            LocalSpecializationProgressV1 progress =
                LocalPlayerProfileSchemaV1.GetProgress(_profile, specialization);
            return !progress.TrainingCompleted && !progress.TrainingSkipped;
        }

        public void RecordTrainingOutcome(
            SpecializationId specialization,
            LocalTrainingOutcome outcome)
        {
            _profile = LocalPlayerProfileSchemaV1.WithTrainingOutcome(
                _profile,
                specialization,
                outcome);
            SaveProfile();
        }

        public void ResetTraining(SpecializationId specialization)
        {
            _profile = LocalPlayerProfileSchemaV1.WithTrainingOutcome(
                _profile,
                specialization,
                LocalTrainingOutcome.None);
            SaveProfile();
        }

        public void ApplySettings(LocalSettingsSelection selection)
        {
            var settings = new LocalProfileSettingsV1(
                LocalProfileSettingsV1.CurrentSchemaVersion,
                selection.MasterVolumePercent,
                selection.EffectsVolumePercent,
                selection.HapticsEnabled,
                selection.ReducedMotion,
                selection.HighContrast);
            _profile = LocalPlayerProfileSchemaV1.WithSettings(_profile, settings);
            ApplyMasterVolume(_profile.Settings.MasterVolumePercent);
            SaveProfile();
        }

        public LocalSettingsSelection GetSettingsSelection()
        {
            LocalProfileSettingsV1 settings = Profile.Settings;
            return new LocalSettingsSelection(
                settings.MasterVolumePercent,
                settings.EffectsVolumePercent,
                settings.HapticsEnabled,
                settings.ReducedMotion,
                settings.HighContrast);
        }

        public void BeginSession(
            SessionRecordMode mode,
            SpecializationId specialization,
            int simulationTick = 0)
        {
            _localSessionSequence = _localSessionSequence == int.MaxValue
                ? 1
                : _localSessionSequence + 1;
            _recordSequence = 0;
            Write(SessionRecordFactory.Started(
                _localSessionSequence,
                _recordSequence++,
                Mathf.Max(0, simulationTick),
                mode,
                specialization));
        }

        public void RecordTrainingStep(
            SpecializationId specialization,
            GuidedTrainingStep step,
            int simulationTick)
        {
            Write(SessionRecordFactory.TrainingStepCompleted(
                _localSessionSequence,
                _recordSequence++,
                Mathf.Max(0, simulationTick),
                specialization,
                step));
        }

        public void RecordTrainingUnlock(
            SpecializationId specialization,
            AbilityId ability,
            int unlockedAbilityCount,
            int simulationTick)
        {
            Write(SessionRecordFactory.TrainingAbilityUnlocked(
                _localSessionSequence,
                _recordSequence++,
                Mathf.Max(0, simulationTick),
                specialization,
                ability,
                unlockedAbilityCount));
        }

        public void FinishSession(
            SessionRecordMode mode,
            SpecializationId specialization,
            int simulationTick,
            int durationTicks,
            SessionRecordOutcome outcome)
        {
            Write(SessionRecordFactory.Finished(
                _localSessionSequence,
                _recordSequence++,
                Mathf.Max(0, simulationTick),
                mode,
                specialization,
                Mathf.Max(0, durationTicks),
                outcome));
        }

        private void Write(SessionRecordV1 record)
        {
            _recordWriter?.TryWrite(record);
        }

        private void SaveProfile()
        {
            ProfileDto dto = ProfileDto.From(Profile);
            string json = JsonUtility.ToJson(dto, true);
            string temporaryPath = ProfilePath + ".tmp";
            try
            {
                File.WriteAllText(temporaryPath, json);
                File.Copy(temporaryPath, ProfilePath, true);
                File.Delete(temporaryPath);
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Could not persist local profile: " + exception.Message);
            }
        }

        private static LocalPlayerProfileV1 LoadProfile(
            string path,
            out bool recovered)
        {
            recovered = false;
            if (!File.Exists(path))
            {
                return LocalPlayerProfileSchemaV1.CreateDefault();
            }

            try
            {
                ProfileDto dto = JsonUtility.FromJson<ProfileDto>(
                    File.ReadAllText(path));
                LocalPlayerProfileV1 candidate = dto != null
                    ? dto.ToProfile()
                    : null;
                LocalPlayerProfileV1 normalized =
                    LocalPlayerProfileSchemaV1.Normalize(candidate);
                recovered = !LocalPlayerProfileSchemaV1.IsValid(candidate);
                return normalized;
            }
            catch (Exception)
            {
                recovered = true;
                return LocalPlayerProfileSchemaV1.CreateDefault();
            }
        }

        private static void ApplyMasterVolume(int percent)
        {
            AudioListener.volume = Mathf.Clamp01(percent / 100f);
        }

        [Serializable]
        private sealed class ProfileDto
        {
            public int schemaVersion;
            public SettingsDto settings;
            public SpecializationDto[] specializations;

            public static ProfileDto From(LocalPlayerProfileV1 profile)
            {
                var result = new ProfileDto
                {
                    schemaVersion = profile.SchemaVersion,
                    settings = SettingsDto.From(profile.Settings),
                    specializations = new SpecializationDto[
                        profile.Specializations.Length],
                };
                for (int index = 0; index < result.specializations.Length; index++)
                {
                    result.specializations[index] =
                        SpecializationDto.From(profile.Specializations[index]);
                }

                return result;
            }

            public LocalPlayerProfileV1 ToProfile()
            {
                LocalSpecializationProgressV1[] progress = specializations != null
                    ? new LocalSpecializationProgressV1[specializations.Length]
                    : null;
                if (progress != null)
                {
                    for (int index = 0; index < progress.Length; index++)
                    {
                        progress[index] = specializations[index] != null
                            ? specializations[index].ToProgress()
                            : default;
                    }
                }

                return new LocalPlayerProfileV1(
                    schemaVersion,
                    settings != null ? settings.ToSettings() : default,
                    progress);
            }
        }

        [Serializable]
        private sealed class SettingsDto
        {
            public int schemaVersion;
            public int masterVolumePercent;
            public int effectsVolumePercent;
            public bool hapticsEnabled;
            public bool reducedMotion;
            public bool highContrast;

            public static SettingsDto From(LocalProfileSettingsV1 settings) =>
                new SettingsDto
                {
                    schemaVersion = settings.SchemaVersion,
                    masterVolumePercent = settings.MasterVolumePercent,
                    effectsVolumePercent = settings.EffectsVolumePercent,
                    hapticsEnabled = settings.HapticsEnabled,
                    reducedMotion = settings.ReducedMotion,
                    highContrast = settings.HighContrast,
                };

            public LocalProfileSettingsV1 ToSettings() =>
                new LocalProfileSettingsV1(
                    schemaVersion,
                    masterVolumePercent,
                    effectsVolumePercent,
                    hapticsEnabled,
                    reducedMotion,
                    highContrast);
        }

        [Serializable]
        private sealed class SpecializationDto
        {
            public int specialization;
            public bool trainingCompleted;
            public bool trainingSkipped;
            public string masteryId;
            public string cosmeticId;

            public static SpecializationDto From(
                LocalSpecializationProgressV1 progress) =>
                new SpecializationDto
                {
                    specialization = (int)progress.Specialization,
                    trainingCompleted = progress.TrainingCompleted,
                    trainingSkipped = progress.TrainingSkipped,
                    masteryId = progress.MasteryId,
                    cosmeticId = progress.CosmeticId,
                };

            public LocalSpecializationProgressV1 ToProgress() =>
                new LocalSpecializationProgressV1(
                    (SpecializationId)specialization,
                    trainingCompleted,
                    trainingSkipped,
                    masteryId,
                    cosmeticId);
        }

        private sealed class LocalJsonLineSessionSink : ISessionRecordSink
        {
            private const long MaximumBytes = 2L * 1024L * 1024L;
            private readonly string _path;
            private readonly string _previousPath;

            public LocalJsonLineSessionSink(string path)
            {
                _path = path;
                _previousPath = path + ".previous";
            }

            public bool TryWrite(in SessionRecordV1 record)
            {
                try
                {
                    RotateIfNeeded();
                    RecordDto dto = RecordDto.From(record);
                    File.AppendAllText(_path, JsonUtility.ToJson(dto) + "\n");
                    return true;
                }
                catch (Exception exception)
                {
                    Debug.LogWarning(
                        "Could not write local playtest record: " + exception.Message);
                    return false;
                }
            }

            private void RotateIfNeeded()
            {
                if (!File.Exists(_path) || new FileInfo(_path).Length < MaximumBytes)
                {
                    return;
                }

                if (File.Exists(_previousPath))
                {
                    File.Delete(_previousPath);
                }

                File.Move(_path, _previousPath);
            }
        }

        [Serializable]
        private sealed class RecordDto
        {
            public int schemaVersion;
            public int localSessionSequence;
            public int recordSequence;
            public int simulationTick;
            public int mode;
            public int kind;
            public int specialization;
            public bool hasTrainingStep;
            public int trainingStep;
            public int ability;
            public int unlockedAbilityCount;
            public int durationTicks;
            public int outcome;

            public static RecordDto From(SessionRecordV1 record) => new RecordDto
            {
                schemaVersion = record.SchemaVersion,
                localSessionSequence = record.LocalSessionSequence,
                recordSequence = record.RecordSequence,
                simulationTick = record.SimulationTick,
                mode = (int)record.Mode,
                kind = (int)record.Kind,
                specialization = (int)record.Specialization,
                hasTrainingStep = record.HasTrainingStep,
                trainingStep = (int)record.TrainingStep,
                ability = (int)record.Ability,
                unlockedAbilityCount = record.UnlockedAbilityCount,
                durationTicks = record.DurationTicks,
                outcome = (int)record.Outcome,
            };
        }
    }
}
