using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class SessionRecordTests
    {
        [TestCase(SessionRecordMode.Arena)]
        [TestCase(SessionRecordMode.Training)]
        [TestCase(SessionRecordMode.HeroSiege)]
        [TestCase(SessionRecordMode.Dungeon)]
        public void StartedFactory_ProducesValidPiiFreeModeRecord(
            SessionRecordMode mode)
        {
            SessionRecordV1 record = SessionRecordFactory.Started(
                4,
                0,
                120,
                mode,
                SpecializationId.Frost);

            Assert.That(SessionRecordContract.IsValid(record), Is.True);
            Assert.That(record.SchemaVersion, Is.EqualTo(1));
            Assert.That(record.LocalSessionSequence, Is.EqualTo(4));
            Assert.That(record.RecordSequence, Is.Zero);
            Assert.That(record.SimulationTick, Is.EqualTo(120));
            Assert.That(record.Mode, Is.EqualTo(mode));
            Assert.That(record.Kind, Is.EqualTo(SessionRecordKind.SessionStarted));
            Assert.That(record.Specialization, Is.EqualTo(SpecializationId.Frost));

            foreach (System.Reflection.PropertyInfo property in
                     typeof(SessionRecordV1).GetProperties())
            {
                Assert.That(
                    property.PropertyType,
                    Is.Not.EqualTo(typeof(string)),
                    property.Name);
            }
        }

        [Test]
        public void TrainingFactories_RequireStructuredMatchingPayloads()
        {
            SessionRecordV1 step = SessionRecordFactory.TrainingStepCompleted(
                1,
                1,
                30,
                SpecializationId.ShadowDance,
                GuidedTrainingStep.Targeting);
            AbilityId unlockedAbility =
                GuidedTrainingAbilityCatalog.GetAbility(
                    SpecializationId.ShadowDance,
                    3);
            SessionRecordV1 unlock = SessionRecordFactory.TrainingAbilityUnlocked(
                1,
                2,
                90,
                SpecializationId.ShadowDance,
                unlockedAbility,
                4);
            SessionRecordV1 finished = SessionRecordFactory.Finished(
                1,
                3,
                240,
                SessionRecordMode.Training,
                SpecializationId.ShadowDance,
                240,
                SessionRecordOutcome.Completed);

            Assert.That(SessionRecordContract.IsValid(step), Is.True);
            Assert.That(step.HasTrainingStep, Is.True);
            Assert.That(step.TrainingStep, Is.EqualTo(GuidedTrainingStep.Targeting));
            Assert.That(SessionRecordContract.IsValid(unlock), Is.True);
            Assert.That(unlock.Ability, Is.EqualTo(unlockedAbility));
            Assert.That(unlock.UnlockedAbilityCount, Is.EqualTo(4));
            Assert.That(SessionRecordContract.IsValid(finished), Is.True);
            Assert.That(finished.DurationTicks, Is.EqualTo(240));
            Assert.That(finished.Outcome, Is.EqualTo(SessionRecordOutcome.Completed));
        }

        [Test]
        public void Contract_RejectsDefaultCorruptAndSemanticallyMismatchedRecords()
        {
            Assert.That(SessionRecordContract.IsValid(default), Is.False);

            SessionRecordV1 corruptSchema = new SessionRecordV1(
                99,
                1,
                0,
                0,
                SessionRecordMode.Arena,
                SessionRecordKind.SessionStarted,
                SpecializationId.Fire,
                false,
                default,
                AbilityId.None,
                0,
                0,
                SessionRecordOutcome.None);
            SessionRecordV1 wrongUnlock =
                SessionRecordFactory.TrainingAbilityUnlocked(
                    1,
                    1,
                    10,
                    SpecializationId.Fire,
                    AbilityId.Fireball,
                    4);
            SessionRecordV1 arenaSkip = SessionRecordFactory.Finished(
                1,
                2,
                20,
                SessionRecordMode.Arena,
                SpecializationId.Fire,
                20,
                SessionRecordOutcome.Skipped);
            SessionRecordV1 trainingVictory = SessionRecordFactory.Finished(
                1,
                3,
                30,
                SessionRecordMode.Training,
                SpecializationId.Fire,
                30,
                SessionRecordOutcome.Victory);

            Assert.That(SessionRecordContract.IsValid(corruptSchema), Is.False);
            Assert.That(SessionRecordContract.IsValid(wrongUnlock), Is.False);
            Assert.That(SessionRecordContract.IsValid(arenaSkip), Is.False);
            Assert.That(SessionRecordContract.IsValid(trainingVictory), Is.False);
        }

        [Test]
        public void Writer_ValidatesBeforeSinkAndPropagatesBoundedSinkRejection()
        {
            var sink = new CapturingSink();
            var writer = new SessionRecordWriter(sink);
            SessionRecordV1 valid = SessionRecordFactory.Started(
                8,
                0,
                0,
                SessionRecordMode.Dungeon,
                SpecializationId.Marksmanship);

            Assert.That(writer.TryWrite(default), Is.False);
            Assert.That(sink.WriteCount, Is.Zero);
            Assert.That(writer.TryWrite(valid), Is.True);
            Assert.That(sink.WriteCount, Is.EqualTo(1));
            Assert.That(sink.LastRecord.Kind, Is.EqualTo(SessionRecordKind.SessionStarted));

            sink.AcceptWrites = false;
            Assert.That(writer.TryWrite(valid), Is.False);
            Assert.That(sink.WriteCount, Is.EqualTo(2));
            Assert.That(
                new SessionRecordWriter(NullSessionRecordSink.Instance)
                    .TryWrite(valid),
                Is.True);
        }

        private sealed class CapturingSink : ISessionRecordSink
        {
            public bool AcceptWrites { get; set; } = true;

            public int WriteCount { get; private set; }

            public SessionRecordV1 LastRecord { get; private set; }

            public bool TryWrite(in SessionRecordV1 record)
            {
                WriteCount++;
                LastRecord = record;
                return AcceptWrites;
            }
        }
    }
}
