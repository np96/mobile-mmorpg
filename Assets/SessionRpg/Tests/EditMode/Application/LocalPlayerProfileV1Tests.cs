using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class LocalPlayerProfileV1Tests
    {
        private static readonly SpecializationId[] PlayableSpecializations =
        {
            SpecializationId.Discipline,
            SpecializationId.Fire,
            SpecializationId.Frost,
            SpecializationId.ShadowDance,
            SpecializationId.Mutilate,
            SpecializationId.Shadow,
            SpecializationId.Marksmanship,
        };

        [Test]
        public void DefaultProfile_IsCompleteValidAndMigrationVersioned()
        {
            LocalPlayerProfileV1 profile =
                LocalPlayerProfileSchemaV1.CreateDefault();

            Assert.That(
                profile.SchemaVersion,
                Is.EqualTo(LocalPlayerProfileSchemaV1.CurrentSchemaVersion));
            Assert.That(
                profile.Settings.SchemaVersion,
                Is.EqualTo(LocalProfileSettingsV1.CurrentSchemaVersion));
            Assert.That(profile.Settings.MasterVolumePercent, Is.EqualTo(100));
            Assert.That(profile.Settings.EffectsVolumePercent, Is.EqualTo(100));
            Assert.That(profile.Settings.HapticsEnabled, Is.True);
            Assert.That(profile.Specializations, Has.Length.EqualTo(7));
            Assert.That(LocalPlayerProfileSchemaV1.IsValid(profile), Is.True);

            for (int index = 0; index < PlayableSpecializations.Length; index++)
            {
                LocalSpecializationProgressV1 progress =
                    profile.Specializations[index];
                Assert.That(
                    progress.Specialization,
                    Is.EqualTo(PlayableSpecializations[index]));
                Assert.That(progress.TrainingCompleted, Is.False);
                Assert.That(progress.TrainingSkipped, Is.False);
                Assert.That(
                    progress.MasteryId,
                    Is.EqualTo(LocalPlayerProfileSchemaV1.DefaultMasteryId));
                Assert.That(
                    progress.CosmeticId,
                    Is.EqualTo(LocalPlayerProfileSchemaV1.DefaultCosmeticId));
            }
        }

        [Test]
        public void Normalize_NullUnknownAndDefaultPayloadsFallBackSafely()
        {
            LocalPlayerProfileV1 fromNull =
                LocalPlayerProfileSchemaV1.Normalize(null);
            LocalPlayerProfileV1 fromUnknownVersion =
                LocalPlayerProfileSchemaV1.Normalize(
                    new LocalPlayerProfileV1(
                        99,
                        LocalProfileSettingsV1.Default,
                        new LocalSpecializationProgressV1[0]));
            LocalPlayerProfileV1 fromDefaultPayload =
                LocalPlayerProfileSchemaV1.Normalize(
                    new LocalPlayerProfileV1(
                        LocalPlayerProfileSchemaV1.CurrentSchemaVersion,
                        default,
                        null));

            Assert.That(LocalPlayerProfileSchemaV1.IsValid(fromNull), Is.True);
            Assert.That(
                LocalPlayerProfileSchemaV1.IsValid(fromUnknownVersion),
                Is.True);
            Assert.That(
                LocalPlayerProfileSchemaV1.IsValid(fromDefaultPayload),
                Is.True);
            Assert.That(
                fromDefaultPayload.Settings.MasterVolumePercent,
                Is.EqualTo(100));
            Assert.That(fromDefaultPayload.Specializations, Has.Length.EqualTo(7));
        }

        [Test]
        public void Normalize_RepairsCorruptionDuplicatesAndMissingSpecs()
        {
            var corruptSettings = new LocalProfileSettingsV1(
                LocalProfileSettingsV1.CurrentSchemaVersion,
                -10,
                101,
                false,
                true,
                true);
            var stored = new[]
            {
                new LocalSpecializationProgressV1(
                    SpecializationId.Shadow,
                    true,
                    true,
                    "Player Name",
                    string.Empty),
                new LocalSpecializationProgressV1(
                    SpecializationId.Shadow,
                    false,
                    true,
                    "mastery.duplicate",
                    "cosmetic.duplicate"),
                new LocalSpecializationProgressV1(
                    SpecializationId.Discipline,
                    false,
                    true,
                    "mastery.discipline",
                    "cosmetic.discipline"),
                new LocalSpecializationProgressV1(
                    SpecializationId.Boss,
                    true,
                    false,
                    "mastery.boss",
                    "cosmetic.boss"),
            };
            var corrupt = new LocalPlayerProfileV1(
                LocalPlayerProfileSchemaV1.CurrentSchemaVersion,
                corruptSettings,
                stored);

            Assert.That(LocalPlayerProfileSchemaV1.IsValid(corrupt), Is.False);
            LocalPlayerProfileV1 normalized =
                LocalPlayerProfileSchemaV1.Normalize(corrupt);

            Assert.That(LocalPlayerProfileSchemaV1.IsValid(normalized), Is.True);
            Assert.That(normalized.Settings.MasterVolumePercent, Is.EqualTo(100));
            Assert.That(normalized.Settings.EffectsVolumePercent, Is.EqualTo(100));
            Assert.That(normalized.Settings.HapticsEnabled, Is.False);
            Assert.That(normalized.Settings.ReducedMotion, Is.True);
            Assert.That(normalized.Settings.HighContrast, Is.True);

            LocalSpecializationProgressV1 shadow =
                LocalPlayerProfileSchemaV1.GetProgress(
                    normalized,
                    SpecializationId.Shadow);
            Assert.That(shadow.TrainingCompleted, Is.True);
            Assert.That(shadow.TrainingSkipped, Is.False);
            Assert.That(
                shadow.MasteryId,
                Is.EqualTo(LocalPlayerProfileSchemaV1.DefaultMasteryId));
            Assert.That(
                shadow.CosmeticId,
                Is.EqualTo(LocalPlayerProfileSchemaV1.DefaultCosmeticId));

            LocalSpecializationProgressV1 discipline =
                LocalPlayerProfileSchemaV1.GetProgress(
                    normalized,
                    SpecializationId.Discipline);
            Assert.That(discipline.TrainingCompleted, Is.False);
            Assert.That(discipline.TrainingSkipped, Is.True);
            Assert.That(discipline.MasteryId, Is.EqualTo("mastery.discipline"));
            Assert.That(discipline.CosmeticId, Is.EqualTo("cosmetic.discipline"));

            LocalSpecializationProgressV1 frost =
                LocalPlayerProfileSchemaV1.GetProgress(
                    normalized,
                    SpecializationId.Frost);
            Assert.That(frost.TrainingCompleted, Is.False);
            Assert.That(frost.TrainingSkipped, Is.False);
        }

        [Test]
        public void TrainingOutcome_UpdateIsCopyOnWriteAndMutuallyExclusive()
        {
            LocalPlayerProfileV1 original =
                LocalPlayerProfileSchemaV1.CreateDefault();
            LocalPlayerProfileV1 skipped =
                LocalPlayerProfileSchemaV1.WithTrainingOutcome(
                    original,
                    SpecializationId.Fire,
                    LocalTrainingOutcome.Skipped);
            LocalPlayerProfileV1 completed =
                LocalPlayerProfileSchemaV1.WithTrainingOutcome(
                    skipped,
                    SpecializationId.Fire,
                    LocalTrainingOutcome.Completed);

            LocalSpecializationProgressV1 originalFire =
                LocalPlayerProfileSchemaV1.GetProgress(
                    original,
                    SpecializationId.Fire);
            LocalSpecializationProgressV1 skippedFire =
                LocalPlayerProfileSchemaV1.GetProgress(
                    skipped,
                    SpecializationId.Fire);
            LocalSpecializationProgressV1 completedFire =
                LocalPlayerProfileSchemaV1.GetProgress(
                    completed,
                    SpecializationId.Fire);

            Assert.That(originalFire.TrainingCompleted, Is.False);
            Assert.That(originalFire.TrainingSkipped, Is.False);
            Assert.That(skippedFire.TrainingCompleted, Is.False);
            Assert.That(skippedFire.TrainingSkipped, Is.True);
            Assert.That(completedFire.TrainingCompleted, Is.True);
            Assert.That(completedFire.TrainingSkipped, Is.False);
            Assert.That(LocalPlayerProfileSchemaV1.IsValid(completed), Is.True);
        }

        [Test]
        public void SettingsAndStableIdentifiers_HaveValidatedCopyOnWriteUpdates()
        {
            LocalPlayerProfileV1 original =
                LocalPlayerProfileSchemaV1.CreateDefault();
            LocalPlayerProfileV1 configured =
                LocalPlayerProfileSchemaV1.WithSettings(
                    original,
                    new LocalProfileSettingsV1(
                        LocalProfileSettingsV1.CurrentSchemaVersion,
                        70,
                        55,
                        false,
                        true,
                        true));
            configured = LocalPlayerProfileSchemaV1.WithSpecializationIdentifiers(
                configured,
                SpecializationId.Mutilate,
                "mastery.poison_adept",
                "cosmetic.crimson-blades");

            Assert.That(original.Settings.MasterVolumePercent, Is.EqualTo(100));
            Assert.That(configured.Settings.MasterVolumePercent, Is.EqualTo(70));
            Assert.That(configured.Settings.EffectsVolumePercent, Is.EqualTo(55));
            Assert.That(configured.Settings.HapticsEnabled, Is.False);
            Assert.That(configured.Settings.ReducedMotion, Is.True);
            LocalSpecializationProgressV1 mutilate =
                LocalPlayerProfileSchemaV1.GetProgress(
                    configured,
                    SpecializationId.Mutilate);
            Assert.That(mutilate.MasteryId, Is.EqualTo("mastery.poison_adept"));
            Assert.That(mutilate.CosmeticId, Is.EqualTo("cosmetic.crimson-blades"));
            Assert.That(LocalPlayerProfileSchemaV1.IsValid(configured), Is.True);
            Assert.That(
                () => LocalPlayerProfileSchemaV1.WithSpecializationIdentifiers(
                    configured,
                    SpecializationId.Mutilate,
                    "Player Name",
                    "cosmetic.valid"),
                Throws.ArgumentException);
        }
    }
}
