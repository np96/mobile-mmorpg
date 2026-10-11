using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    public enum LocalTrainingOutcome
    {
        None,
        Completed,
        Skipped,
    }

    [Serializable]
    public readonly struct LocalProfileSettingsV1
    {
        public const int CurrentSchemaVersion = 1;

        public LocalProfileSettingsV1(
            int schemaVersion,
            int masterVolumePercent,
            int effectsVolumePercent,
            bool hapticsEnabled,
            bool reducedMotion,
            bool highContrast)
        {
            SchemaVersion = schemaVersion;
            MasterVolumePercent = masterVolumePercent;
            EffectsVolumePercent = effectsVolumePercent;
            HapticsEnabled = hapticsEnabled;
            ReducedMotion = reducedMotion;
            HighContrast = highContrast;
        }

        public int SchemaVersion { get; }

        public int MasterVolumePercent { get; }

        public int EffectsVolumePercent { get; }

        public bool HapticsEnabled { get; }

        public bool ReducedMotion { get; }

        public bool HighContrast { get; }

        public static LocalProfileSettingsV1 Default =>
            new LocalProfileSettingsV1(
                CurrentSchemaVersion,
                100,
                100,
                true,
                false,
                false);
    }

    [Serializable]
    public readonly struct LocalSpecializationProgressV1
    {
        public LocalSpecializationProgressV1(
            SpecializationId specialization,
            bool trainingCompleted,
            bool trainingSkipped,
            string masteryId,
            string cosmeticId)
        {
            Specialization = specialization;
            TrainingCompleted = trainingCompleted;
            TrainingSkipped = trainingSkipped;
            MasteryId = masteryId;
            CosmeticId = cosmeticId;
        }

        public SpecializationId Specialization { get; }

        public bool TrainingCompleted { get; }

        public bool TrainingSkipped { get; }

        public string MasteryId { get; }

        public string CosmeticId { get; }
    }

    /// <summary>
    /// Persistence-neutral local profile schema. Adapters may serialize this
    /// object in any local format, then must normalize it before gameplay use.
    /// No account identity is part of the schema.
    /// </summary>
    [Serializable]
    public sealed class LocalPlayerProfileV1
    {
        public LocalPlayerProfileV1()
            : this(
                LocalPlayerProfileSchemaV1.CurrentSchemaVersion,
                LocalProfileSettingsV1.Default,
                LocalPlayerProfileSchemaV1.CreateDefaultSpecializations())
        {
        }

        public LocalPlayerProfileV1(
            int schemaVersion,
            LocalProfileSettingsV1 settings,
            LocalSpecializationProgressV1[] specializations)
        {
            SchemaVersion = schemaVersion;
            Settings = settings;
            Specializations = specializations;
        }

        public int SchemaVersion { get; }

        public LocalProfileSettingsV1 Settings { get; }

        public LocalSpecializationProgressV1[] Specializations { get; }
    }

    /// <summary>
    /// Version boundary and corruption-safe normalization for local profile V1.
    /// Unknown outer versions fall back to V1 defaults so a later migration
    /// adapter can be inserted before this boundary without changing consumers.
    /// </summary>
    public static class LocalPlayerProfileSchemaV1
    {
        public const int CurrentSchemaVersion = 1;
        public const int SpecializationCount = 7;
        public const int MaximumIdentifierLength = 64;
        public const string DefaultMasteryId = "mastery.default";
        public const string DefaultCosmeticId = "cosmetic.default";

        private const int MaximumStoredSpecializationEntries = 32;

        public static LocalPlayerProfileV1 CreateDefault()
        {
            return new LocalPlayerProfileV1(
                CurrentSchemaVersion,
                LocalProfileSettingsV1.Default,
                CreateDefaultSpecializations());
        }

        public static LocalPlayerProfileV1 Normalize(LocalPlayerProfileV1 source)
        {
            if (source == null || source.SchemaVersion != CurrentSchemaVersion)
            {
                return CreateDefault();
            }

            LocalSpecializationProgressV1[] normalized =
                CreateDefaultSpecializations();
            LocalSpecializationProgressV1[] stored = source.Specializations;
            int seenMask = 0;

            if (stored != null)
            {
                int storedCount = Math.Min(
                    stored.Length,
                    MaximumStoredSpecializationEntries);
                for (int index = 0; index < storedCount; index++)
                {
                    LocalSpecializationProgressV1 candidate = stored[index];
                    int specializationIndex = GetSpecializationIndex(
                        candidate.Specialization);
                    if (specializationIndex < 0)
                    {
                        continue;
                    }

                    int specializationBit = 1 << specializationIndex;
                    if ((seenMask & specializationBit) != 0)
                    {
                        continue;
                    }

                    seenMask |= specializationBit;
                    bool completed = candidate.TrainingCompleted;
                    bool skipped = !completed && candidate.TrainingSkipped;
                    normalized[specializationIndex] =
                        new LocalSpecializationProgressV1(
                            candidate.Specialization,
                            completed,
                            skipped,
                            NormalizeIdentifier(
                                candidate.MasteryId,
                                DefaultMasteryId),
                            NormalizeIdentifier(
                                candidate.CosmeticId,
                                DefaultCosmeticId));
                }
            }

            return new LocalPlayerProfileV1(
                CurrentSchemaVersion,
                NormalizeSettings(source.Settings),
                normalized);
        }

        public static bool IsValid(LocalPlayerProfileV1 profile)
        {
            if (profile == null ||
                profile.SchemaVersion != CurrentSchemaVersion ||
                !IsValidSettings(profile.Settings) ||
                profile.Specializations == null ||
                profile.Specializations.Length != SpecializationCount)
            {
                return false;
            }

            for (int index = 0; index < SpecializationCount; index++)
            {
                LocalSpecializationProgressV1 progress =
                    profile.Specializations[index];
                if (progress.Specialization != GetSpecializationAt(index) ||
                    (progress.TrainingCompleted && progress.TrainingSkipped) ||
                    !IsValidIdentifier(progress.MasteryId) ||
                    !IsValidIdentifier(progress.CosmeticId))
                {
                    return false;
                }
            }

            return true;
        }

        public static LocalSpecializationProgressV1 GetProgress(
            LocalPlayerProfileV1 profile,
            SpecializationId specialization)
        {
            int index = GetSpecializationIndex(specialization);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(specialization),
                    specialization,
                    "Local profiles only carry playable specializations.");
            }

            LocalPlayerProfileV1 normalized = Normalize(profile);
            return normalized.Specializations[index];
        }

        public static LocalPlayerProfileV1 WithTrainingOutcome(
            LocalPlayerProfileV1 profile,
            SpecializationId specialization,
            LocalTrainingOutcome outcome)
        {
            int index = GetSpecializationIndex(specialization);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(specialization),
                    specialization,
                    "Local profiles only carry playable specializations.");
            }

            if (outcome < LocalTrainingOutcome.None ||
                outcome > LocalTrainingOutcome.Skipped)
            {
                throw new ArgumentOutOfRangeException(nameof(outcome));
            }

            LocalPlayerProfileV1 normalized = Normalize(profile);
            LocalSpecializationProgressV1[] copied =
                CopySpecializations(normalized.Specializations);
            LocalSpecializationProgressV1 previous = copied[index];
            copied[index] = new LocalSpecializationProgressV1(
                specialization,
                outcome == LocalTrainingOutcome.Completed,
                outcome == LocalTrainingOutcome.Skipped,
                previous.MasteryId,
                previous.CosmeticId);

            return new LocalPlayerProfileV1(
                CurrentSchemaVersion,
                normalized.Settings,
                copied);
        }

        public static LocalPlayerProfileV1 WithSettings(
            LocalPlayerProfileV1 profile,
            LocalProfileSettingsV1 settings)
        {
            LocalPlayerProfileV1 normalized = Normalize(profile);
            return new LocalPlayerProfileV1(
                CurrentSchemaVersion,
                NormalizeSettings(settings),
                CopySpecializations(normalized.Specializations));
        }

        public static LocalPlayerProfileV1 WithSpecializationIdentifiers(
            LocalPlayerProfileV1 profile,
            SpecializationId specialization,
            string masteryId,
            string cosmeticId)
        {
            int index = GetSpecializationIndex(specialization);
            if (index < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(specialization),
                    specialization,
                    "Local profiles only carry playable specializations.");
            }

            if (!IsValidIdentifier(masteryId))
            {
                throw new ArgumentException(
                    "Mastery IDs must be bounded lowercase stable identifiers.",
                    nameof(masteryId));
            }

            if (!IsValidIdentifier(cosmeticId))
            {
                throw new ArgumentException(
                    "Cosmetic IDs must be bounded lowercase stable identifiers.",
                    nameof(cosmeticId));
            }

            LocalPlayerProfileV1 normalized = Normalize(profile);
            LocalSpecializationProgressV1[] copied =
                CopySpecializations(normalized.Specializations);
            LocalSpecializationProgressV1 previous = copied[index];
            copied[index] = new LocalSpecializationProgressV1(
                specialization,
                previous.TrainingCompleted,
                previous.TrainingSkipped,
                masteryId,
                cosmeticId);
            return new LocalPlayerProfileV1(
                CurrentSchemaVersion,
                normalized.Settings,
                copied);
        }

        internal static LocalSpecializationProgressV1[]
            CreateDefaultSpecializations()
        {
            var result = new LocalSpecializationProgressV1[SpecializationCount];
            for (int index = 0; index < result.Length; index++)
            {
                result[index] = new LocalSpecializationProgressV1(
                    GetSpecializationAt(index),
                    false,
                    false,
                    DefaultMasteryId,
                    DefaultCosmeticId);
            }

            return result;
        }

        private static LocalProfileSettingsV1 NormalizeSettings(
            LocalProfileSettingsV1 settings)
        {
            if (settings.SchemaVersion !=
                LocalProfileSettingsV1.CurrentSchemaVersion)
            {
                return LocalProfileSettingsV1.Default;
            }

            LocalProfileSettingsV1 defaults = LocalProfileSettingsV1.Default;
            int masterVolume = IsPercentage(settings.MasterVolumePercent)
                ? settings.MasterVolumePercent
                : defaults.MasterVolumePercent;
            int effectsVolume = IsPercentage(settings.EffectsVolumePercent)
                ? settings.EffectsVolumePercent
                : defaults.EffectsVolumePercent;
            return new LocalProfileSettingsV1(
                LocalProfileSettingsV1.CurrentSchemaVersion,
                masterVolume,
                effectsVolume,
                settings.HapticsEnabled,
                settings.ReducedMotion,
                settings.HighContrast);
        }

        private static bool IsValidSettings(LocalProfileSettingsV1 settings)
        {
            return settings.SchemaVersion ==
                    LocalProfileSettingsV1.CurrentSchemaVersion &&
                IsPercentage(settings.MasterVolumePercent) &&
                IsPercentage(settings.EffectsVolumePercent);
        }

        private static bool IsPercentage(int value)
        {
            return value >= 0 && value <= 100;
        }

        private static string NormalizeIdentifier(
            string identifier,
            string fallback)
        {
            return IsValidIdentifier(identifier) ? identifier : fallback;
        }

        public static bool IsValidIdentifier(string identifier)
        {
            if (string.IsNullOrEmpty(identifier) ||
                identifier.Length > MaximumIdentifierLength)
            {
                return false;
            }

            for (int index = 0; index < identifier.Length; index++)
            {
                char character = identifier[index];
                bool isLowercaseLetter = character >= 'a' && character <= 'z';
                bool isDigit = character >= '0' && character <= '9';
                bool isSeparator = character == '.' ||
                    character == '_' ||
                    character == '-';
                if (!isLowercaseLetter && !isDigit && !isSeparator)
                {
                    return false;
                }
            }

            return true;
        }

        private static LocalSpecializationProgressV1[] CopySpecializations(
            LocalSpecializationProgressV1[] source)
        {
            var copied = new LocalSpecializationProgressV1[SpecializationCount];
            Array.Copy(source, copied, SpecializationCount);
            return copied;
        }

        private static int GetSpecializationIndex(
            SpecializationId specialization)
        {
            switch (specialization)
            {
                case SpecializationId.Discipline:
                    return 0;
                case SpecializationId.Fire:
                    return 1;
                case SpecializationId.Frost:
                    return 2;
                case SpecializationId.ShadowDance:
                    return 3;
                case SpecializationId.Mutilate:
                    return 4;
                case SpecializationId.Shadow:
                    return 5;
                case SpecializationId.Marksmanship:
                    return 6;
                default:
                    return -1;
            }
        }

        private static SpecializationId GetSpecializationAt(int index)
        {
            switch (index)
            {
                case 0:
                    return SpecializationId.Discipline;
                case 1:
                    return SpecializationId.Fire;
                case 2:
                    return SpecializationId.Frost;
                case 3:
                    return SpecializationId.ShadowDance;
                case 4:
                    return SpecializationId.Mutilate;
                case 5:
                    return SpecializationId.Shadow;
                case 6:
                    return SpecializationId.Marksmanship;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }
}
