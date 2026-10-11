using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    public enum SessionRecordMode
    {
        None,
        Arena,
        Training,
        HeroSiege,
        Dungeon,
        PveLeveling,
    }

    public enum SessionRecordKind
    {
        None,
        SessionStarted,
        TrainingStepCompleted,
        TrainingAbilityUnlocked,
        SessionFinished,
    }

    public enum SessionRecordOutcome
    {
        None,
        Completed,
        Skipped,
        Victory,
        Defeat,
        Draw,
        Abandoned,
    }

    /// <summary>
    /// PII-free structured session record V1. The contract intentionally has no
    /// text, account, installation, network, or device identifiers. Session and
    /// record sequences are local, ephemeral ordering values only.
    /// </summary>
    public readonly struct SessionRecordV1
    {
        public const int CurrentSchemaVersion = 1;

        public SessionRecordV1(
            int schemaVersion,
            int localSessionSequence,
            int recordSequence,
            int simulationTick,
            SessionRecordMode mode,
            SessionRecordKind kind,
            SpecializationId specialization,
            bool hasTrainingStep,
            GuidedTrainingStep trainingStep,
            AbilityId ability,
            int unlockedAbilityCount,
            int durationTicks,
            SessionRecordOutcome outcome)
        {
            SchemaVersion = schemaVersion;
            LocalSessionSequence = localSessionSequence;
            RecordSequence = recordSequence;
            SimulationTick = simulationTick;
            Mode = mode;
            Kind = kind;
            Specialization = specialization;
            HasTrainingStep = hasTrainingStep;
            TrainingStep = trainingStep;
            Ability = ability;
            UnlockedAbilityCount = unlockedAbilityCount;
            DurationTicks = durationTicks;
            Outcome = outcome;
        }

        public int SchemaVersion { get; }

        public int LocalSessionSequence { get; }

        public int RecordSequence { get; }

        public int SimulationTick { get; }

        public SessionRecordMode Mode { get; }

        public SessionRecordKind Kind { get; }

        public SpecializationId Specialization { get; }

        public bool HasTrainingStep { get; }

        public GuidedTrainingStep TrainingStep { get; }

        public AbilityId Ability { get; }

        public int UnlockedAbilityCount { get; }

        public int DurationTicks { get; }

        public SessionRecordOutcome Outcome { get; }
    }

    public static class SessionRecordFactory
    {
        public static SessionRecordV1 Started(
            int localSessionSequence,
            int recordSequence,
            int simulationTick,
            SessionRecordMode mode,
            SpecializationId specialization)
        {
            return new SessionRecordV1(
                SessionRecordV1.CurrentSchemaVersion,
                localSessionSequence,
                recordSequence,
                simulationTick,
                mode,
                SessionRecordKind.SessionStarted,
                specialization,
                false,
                default,
                AbilityId.None,
                0,
                0,
                SessionRecordOutcome.None);
        }

        public static SessionRecordV1 TrainingStepCompleted(
            int localSessionSequence,
            int recordSequence,
            int simulationTick,
            SpecializationId specialization,
            GuidedTrainingStep step)
        {
            return new SessionRecordV1(
                SessionRecordV1.CurrentSchemaVersion,
                localSessionSequence,
                recordSequence,
                simulationTick,
                SessionRecordMode.Training,
                SessionRecordKind.TrainingStepCompleted,
                specialization,
                true,
                step,
                AbilityId.None,
                0,
                0,
                SessionRecordOutcome.None);
        }

        public static SessionRecordV1 TrainingAbilityUnlocked(
            int localSessionSequence,
            int recordSequence,
            int simulationTick,
            SpecializationId specialization,
            AbilityId ability,
            int unlockedAbilityCount)
        {
            return new SessionRecordV1(
                SessionRecordV1.CurrentSchemaVersion,
                localSessionSequence,
                recordSequence,
                simulationTick,
                SessionRecordMode.Training,
                SessionRecordKind.TrainingAbilityUnlocked,
                specialization,
                false,
                default,
                ability,
                unlockedAbilityCount,
                0,
                SessionRecordOutcome.None);
        }

        public static SessionRecordV1 Finished(
            int localSessionSequence,
            int recordSequence,
            int simulationTick,
            SessionRecordMode mode,
            SpecializationId specialization,
            int durationTicks,
            SessionRecordOutcome outcome)
        {
            return new SessionRecordV1(
                SessionRecordV1.CurrentSchemaVersion,
                localSessionSequence,
                recordSequence,
                simulationTick,
                mode,
                SessionRecordKind.SessionFinished,
                specialization,
                false,
                default,
                AbilityId.None,
                0,
                durationTicks,
                outcome);
        }
    }

    public static class SessionRecordContract
    {
        public static bool IsValid(SessionRecordV1 record)
        {
            if (record.SchemaVersion != SessionRecordV1.CurrentSchemaVersion ||
                record.LocalSessionSequence <= 0 ||
                record.RecordSequence < 0 ||
                record.SimulationTick < 0 ||
                !IsValidMode(record.Mode) ||
                !LevelingAbilityUnlockCatalog.TryGetKit(
                    record.Specialization,
                    out AbilityKit _))
            {
                return false;
            }

            switch (record.Kind)
            {
                case SessionRecordKind.SessionStarted:
                    return HasNoTrainingPayload(record) &&
                        record.DurationTicks == 0 &&
                        record.Outcome == SessionRecordOutcome.None;
                case SessionRecordKind.TrainingStepCompleted:
                    return record.Mode == SessionRecordMode.Training &&
                        record.HasTrainingStep &&
                        IsValidTrainingStep(record.TrainingStep) &&
                        record.Ability == AbilityId.None &&
                        record.UnlockedAbilityCount == 0 &&
                        record.DurationTicks == 0 &&
                        record.Outcome == SessionRecordOutcome.None;
                case SessionRecordKind.TrainingAbilityUnlocked:
                    return record.Mode == SessionRecordMode.Training &&
                        !record.HasTrainingStep &&
                        record.UnlockedAbilityCount >
                            GuidedTrainingAbilityCatalog.StartingAbilityCount &&
                        record.UnlockedAbilityCount <=
                            GuidedTrainingAbilityCatalog.TotalAbilityCount &&
                        record.Ability ==
                            GuidedTrainingAbilityCatalog.GetAbility(
                                record.Specialization,
                                record.UnlockedAbilityCount - 1) &&
                        record.DurationTicks == 0 &&
                        record.Outcome == SessionRecordOutcome.None;
                case SessionRecordKind.SessionFinished:
                    return HasNoTrainingPayload(record) &&
                        record.DurationTicks >= 0 &&
                        IsValidOutcome(record.Mode, record.Outcome);
                default:
                    return false;
            }
        }

        private static bool HasNoTrainingPayload(SessionRecordV1 record)
        {
            return !record.HasTrainingStep &&
                record.Ability == AbilityId.None &&
                record.UnlockedAbilityCount == 0;
        }

        private static bool IsValidTrainingStep(GuidedTrainingStep step)
        {
            return step >= GuidedTrainingStep.Movement &&
                step <= GuidedTrainingStep.FlickMastery;
        }

        private static bool IsValidMode(SessionRecordMode mode)
        {
            return mode >= SessionRecordMode.Arena &&
                mode <= SessionRecordMode.PveLeveling;
        }

        private static bool IsValidOutcome(
            SessionRecordMode mode,
            SessionRecordOutcome outcome)
        {
            if (outcome == SessionRecordOutcome.None)
            {
                return false;
            }

            if (outcome == SessionRecordOutcome.Skipped)
            {
                return mode == SessionRecordMode.Training;
            }

            if (mode == SessionRecordMode.Training &&
                (outcome == SessionRecordOutcome.Victory ||
                 outcome == SessionRecordOutcome.Defeat ||
                 outcome == SessionRecordOutcome.Draw))
            {
                return false;
            }

            return true;
        }
    }

    /// <summary>
    /// Persistence/transport boundary for validated structured records. A sink
    /// may reject a record when its bounded local storage is full.
    /// </summary>
    public interface ISessionRecordSink
    {
        bool TryWrite(in SessionRecordV1 record);
    }

    public sealed class NullSessionRecordSink : ISessionRecordSink
    {
        public static readonly NullSessionRecordSink Instance =
            new NullSessionRecordSink();

        private NullSessionRecordSink()
        {
        }

        public bool TryWrite(in SessionRecordV1 record)
        {
            return true;
        }
    }

    /// <summary>
    /// Allocation-free validation gate used before records reach a concrete sink.
    /// </summary>
    public sealed class SessionRecordWriter
    {
        private readonly ISessionRecordSink _sink;

        public SessionRecordWriter(ISessionRecordSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public bool TryWrite(SessionRecordV1 record)
        {
            return SessionRecordContract.IsValid(record) &&
                _sink.TryWrite(in record);
        }
    }
}
