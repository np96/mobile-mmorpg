using System;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class DeterministicSessionSoakTests
    {
        private const int MaximumSessionSeconds = 180;
        private const float MeaningfulAmountEpsilon = 0.001f;

        [Test]
        public void Arena_AllBotsSustainMeaningfulCombatUntilAResultOrTimeCap()
        {
            SessionMetrics first = RunArena();
            SessionMetrics repeated = RunArena();

            AssertDeterministic(first, repeated);
            AssertMeaningfulSession(first, participantCount: 6);
            Assert.That(first.Outcome, Is.Not.EqualTo(SessionOutcome.None));
            Assert.That(first.DamageAbilityCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(first.TotalDamage, Is.GreaterThanOrEqualTo(200f));
            Assert.That(first.HealCount, Is.GreaterThan(0));
            Assert.That(first.TotalHealing, Is.GreaterThan(0f));
            Report("Arena", first);
        }

        [Test]
        public void Boss_AllBotsSustainMeaningfulCombatUntilAResultOrTimeCap()
        {
            SessionMetrics first = RunBoss();
            SessionMetrics repeated = RunBoss();

            AssertDeterministic(first, repeated);
            AssertMeaningfulSession(first, participantCount: 4);
            Assert.That(first.Outcome, Is.Not.EqualTo(SessionOutcome.None));
            Assert.That(first.DamageAbilityCount, Is.GreaterThanOrEqualTo(3));
            Assert.That(first.TotalDamage, Is.GreaterThanOrEqualTo(300f));
            Assert.That(first.HealCount, Is.GreaterThan(0));
            Assert.That(first.TotalHealing, Is.GreaterThan(0f));
            Assert.That(first.AcceptedCastsByParticipant[4], Is.GreaterThan(0),
                "The boss never completed an AI decision into an accepted ability.");
            Report("Boss", first);
        }

        private static SessionMetrics RunArena()
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.ArenaDefault);
            AddCombatant(
                simulation,
                1,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(
                    -ArenaLayout.TeamSpawnOffset,
                    0f,
                    -ArenaLayout.PairSpawnOffset),
                110f);
            AddCombatant(
                simulation,
                2,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(
                    -ArenaLayout.TeamSpawnOffset,
                    0f,
                    0f),
                110f);
            AddCombatant(
                simulation,
                3,
                1,
                ClassId.Priest,
                SpecializationId.Shadow,
                new Float3(
                    -ArenaLayout.TeamSpawnOffset,
                    0f,
                    ArenaLayout.PairSpawnOffset),
                110f);
            AddCombatant(
                simulation,
                4,
                2,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(
                    ArenaLayout.TeamSpawnOffset,
                    0f,
                    -ArenaLayout.PairSpawnOffset),
                110f);
            AddCombatant(
                simulation,
                5,
                2,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                new Float3(
                    ArenaLayout.TeamSpawnOffset,
                    0f,
                    0f),
                110f);
            AddCombatant(
                simulation,
                6,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(
                    ArenaLayout.TeamSpawnOffset,
                    0f,
                    ArenaLayout.PairSpawnOffset),
                110f);

            return RunSession(simulation, SessionKind.Arena);
        }

        private static SessionMetrics RunBoss()
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.BossDefault);
            AddCombatant(
                simulation,
                1,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(-3.5f, 0f, -7f),
                120f);
            AddCombatant(
                simulation,
                2,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, -8f),
                120f);
            AddCombatant(
                simulation,
                3,
                1,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                new Float3(3.5f, 0f, -7f),
                120f);
            AddCombatant(
                simulation,
                4,
                2,
                ClassId.Boss,
                SpecializationId.Boss,
                new Float3(0f, 0f, 3.5f),
                480f);

            return RunSession(simulation, SessionKind.Boss);
        }

        private static void AddCombatant(
            CombatSimulation simulation,
            int participant,
            int team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            float health)
        {
            bool added = simulation.AddCombatant(
                new ParticipantId(participant),
                new TeamId(team),
                classId,
                specialization,
                position,
                health);
            Assert.That(added, Is.True, "The soak fixture could not create participant " + participant + ".");
        }

        private static SessionMetrics RunSession(
            CombatSimulation simulation,
            SessionKind kind)
        {
            int maximumTicks = simulation.Rules.TickRate * MaximumSessionSeconds;
            DeterministicBotBrain[] brains = CreateBrains(simulation.CombatantCount);
            ArenaTeamCoordinator teamCoordinator = new ArenaTeamCoordinator();
            CombatantSnapshot[] snapshots =
                new CombatantSnapshot[CombatSimulation.MaxCombatants];
            SessionMetrics metrics = new SessionMetrics();

            for (int tick = 0; tick < maximumTicks; tick++)
            {
                int snapshotCount = simulation.CopySnapshots(snapshots);
                ArenaTeamIntent teamOneIntent = kind == SessionKind.Arena
                    ? teamCoordinator.Build(new TeamId(1), snapshots, snapshotCount)
                    : default;
                ArenaTeamIntent teamTwoIntent = kind == SessionKind.Arena
                    ? teamCoordinator.Build(new TeamId(2), snapshots, snapshotCount)
                    : default;
                for (int index = 0; index < snapshotCount; index++)
                {
                    CombatantSnapshot self = snapshots[index];
                    bool builtCommand;
                    CombatCommand command;
                    if (kind == SessionKind.Arena)
                    {
                        ArenaTeamIntent teamIntent = self.Team.Value == 1
                            ? teamOneIntent
                            : teamTwoIntent;
                        builtCommand = brains[index].TryBuildCommand(
                            simulation,
                            self,
                            snapshots,
                            snapshotCount,
                            teamIntent,
                            out command);
                    }
                    else
                    {
                        builtCommand = brains[index].TryBuildCommand(
                            simulation,
                            self,
                            snapshots,
                            snapshotCount,
                            out command);
                    }

                    if (builtCommand)
                    {
                        Assert.That(simulation.SubmitCommand(command), Is.True,
                            "A deterministic bot command was unexpectedly dropped.");
                    }
                }

                simulation.Tick();
                metrics.ObserveTick(simulation);

                snapshotCount = simulation.CopySnapshots(snapshots);
                SessionOutcome outcome = ResolveOutcome(kind, snapshots, snapshotCount);
                if (outcome != SessionOutcome.None)
                {
                    metrics.Finish(simulation.CurrentTick, outcome, reachedTimeCap: false);
                    return metrics;
                }
            }

            metrics.Finish(
                simulation.CurrentTick,
                SessionOutcome.TimeCap,
                reachedTimeCap: true);
            return metrics;
        }

        private static DeterministicBotBrain[] CreateBrains(int participantCount)
        {
            DeterministicBotBrain[] brains =
                new DeterministicBotBrain[participantCount];
            for (int index = 0; index < participantCount; index++)
            {
                int participantValue = index + 1;
                brains[index] = new DeterministicBotBrain(
                    1301 + (participantValue * 7919));
            }

            return brains;
        }

        private static SessionOutcome ResolveOutcome(
            SessionKind kind,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            int friendlyAlive = 0;
            int hostileAlive = 0;
            bool bossAlive = false;
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot snapshot = snapshots[index];
                if (!snapshot.IsAlive)
                {
                    continue;
                }

                if (snapshot.Team.Value == 1)
                {
                    friendlyAlive++;
                }
                else
                {
                    hostileAlive++;
                }

                if (snapshot.Class == ClassId.Boss)
                {
                    bossAlive = true;
                }
            }

            if (kind == SessionKind.Arena)
            {
                if (friendlyAlive == 0 && hostileAlive == 0)
                {
                    return SessionOutcome.Draw;
                }

                if (friendlyAlive == 0)
                {
                    return SessionOutcome.HostileWin;
                }

                return hostileAlive == 0
                    ? SessionOutcome.FriendlyWin
                    : SessionOutcome.None;
            }

            if (!bossAlive)
            {
                return SessionOutcome.FriendlyWin;
            }

            return friendlyAlive == 0
                ? SessionOutcome.HostileWin
                : SessionOutcome.None;
        }

        private static void AssertMeaningfulSession(
            SessionMetrics metrics,
            int participantCount)
        {
            int maximumTicks = CombatRules.ArenaDefault.TickRate * MaximumSessionSeconds;
            Assert.That(metrics.DurationTicks, Is.InRange(1, maximumTicks));
            Assert.That(metrics.FirstDamageTick, Is.InRange(1, 30 * 25),
                "Bots failed to make damaging contact within 25 seconds.");
            Assert.That(metrics.LastActivityTick, Is.GreaterThan(0));
            Assert.That(metrics.MaximumActivityGapTicks, Is.LessThanOrEqualTo(30 * 20),
                "Combat went quiet for more than 20 seconds.");
            Assert.That(metrics.AcceptedCastCount, Is.GreaterThanOrEqualTo(12));
            Assert.That(metrics.CompletedCastCount, Is.GreaterThanOrEqualTo(8));
            Assert.That(metrics.DamageCount, Is.GreaterThan(0));
            Assert.That(metrics.DeathCount, Is.LessThanOrEqualTo(participantCount));
            Assert.That(metrics.RejectedCastCount,
                Is.LessThanOrEqualTo(metrics.AcceptedCastCount / 2 + 1),
                "The bot loop is generating too many avoidable rejected casts.");

            for (int participant = 1; participant <= participantCount; participant++)
            {
                Assert.That(metrics.AcceptedCastsByParticipant[participant], Is.GreaterThan(0),
                    "Participant " + participant + " never made an accepted cast.");
            }
        }

        private static void AssertDeterministic(
            SessionMetrics expected,
            SessionMetrics actual)
        {
            Assert.That(actual.DurationTicks, Is.EqualTo(expected.DurationTicks));
            Assert.That(actual.Outcome, Is.EqualTo(expected.Outcome));
            Assert.That(actual.ReachedTimeCap, Is.EqualTo(expected.ReachedTimeCap));
            Assert.That(actual.AcceptedCastCount, Is.EqualTo(expected.AcceptedCastCount));
            Assert.That(actual.RejectedCastCount, Is.EqualTo(expected.RejectedCastCount));
            Assert.That(actual.CompletedCastCount, Is.EqualTo(expected.CompletedCastCount));
            Assert.That(actual.DamageCount, Is.EqualTo(expected.DamageCount));
            Assert.That(actual.HealCount, Is.EqualTo(expected.HealCount));
            Assert.That(actual.DeathCount, Is.EqualTo(expected.DeathCount));
            Assert.That(actual.TotalDamage, Is.EqualTo(expected.TotalDamage));
            Assert.That(actual.TotalHealing, Is.EqualTo(expected.TotalHealing));
            Assert.That(actual.DamageAbilityCount, Is.EqualTo(expected.DamageAbilityCount));
            Assert.That(actual.FirstDamageTick, Is.EqualTo(expected.FirstDamageTick));
            Assert.That(actual.LastActivityTick, Is.EqualTo(expected.LastActivityTick));
            Assert.That(actual.MaximumActivityGapTicks,
                Is.EqualTo(expected.MaximumActivityGapTicks));

            for (int participant = 1; participant < expected.AcceptedCastsByParticipant.Length;
                 participant++)
            {
                Assert.That(
                    actual.AcceptedCastsByParticipant[participant],
                    Is.EqualTo(expected.AcceptedCastsByParticipant[participant]));
            }
        }

        private static void Report(string label, SessionMetrics metrics)
        {
            TestContext.Out.WriteLine(
                label + " soak: outcome=" + metrics.Outcome +
                ", duration=" + (metrics.DurationTicks / 30f).ToString("0.0") + "s" +
                ", casts=" + metrics.AcceptedCastCount +
                ", rejected=" + metrics.RejectedCastCount +
                ", damage=" + metrics.TotalDamage.ToString("0.0") +
                " in " + metrics.DamageCount + " events" +
                ", healing=" + metrics.TotalHealing.ToString("0.0") +
                " in " + metrics.HealCount + " events" +
                ", deaths=" + metrics.DeathCount +
                ", damaging abilities=" + metrics.DamageAbilityCount + ".");
        }

        private enum SessionKind
        {
            Arena,
            Boss,
        }

        private enum SessionOutcome
        {
            None,
            FriendlyWin,
            HostileWin,
            Draw,
            TimeCap,
        }

        private sealed class SessionMetrics
        {
            private readonly bool[] _damageAbilities =
                new bool[AbilityCatalog.StorageCapacity];
            private int _previousActivityTick;

            public SessionMetrics()
            {
                AcceptedCastsByParticipant =
                    new int[CombatSimulation.MaxCombatants + 1];
                FirstDamageTick = -1;
            }

            public int DurationTicks { get; private set; }

            public SessionOutcome Outcome { get; private set; }

            public bool ReachedTimeCap { get; private set; }

            public int AcceptedCastCount { get; private set; }

            public int RejectedCastCount { get; private set; }

            public int CompletedCastCount { get; private set; }

            public int DamageCount { get; private set; }

            public int HealCount { get; private set; }

            public int DeathCount { get; private set; }

            public float TotalDamage { get; private set; }

            public float TotalHealing { get; private set; }

            public int DamageAbilityCount { get; private set; }

            public int FirstDamageTick { get; private set; }

            public int LastActivityTick { get; private set; }

            public int MaximumActivityGapTicks { get; private set; }

            public int[] AcceptedCastsByParticipant { get; }

            public void ObserveTick(CombatSimulation simulation)
            {
                for (int eventIndex = 0; eventIndex < simulation.EventCount; eventIndex++)
                {
                    CombatEvent combatEvent = simulation.GetEvent(eventIndex);
                    switch (combatEvent.Kind)
                    {
                        case CombatEventKind.CastAccepted:
                            AcceptedCastCount++;
                            if (combatEvent.Actor.Value > 0 &&
                                combatEvent.Actor.Value < AcceptedCastsByParticipant.Length)
                            {
                                AcceptedCastsByParticipant[combatEvent.Actor.Value]++;
                            }

                            ObserveActivity(combatEvent.Tick);
                            break;
                        case CombatEventKind.CastRejected:
                            RejectedCastCount++;
                            break;
                        case CombatEventKind.CastCompleted:
                            CompletedCastCount++;
                            ObserveActivity(combatEvent.Tick);
                            break;
                        case CombatEventKind.Damage:
                        case CombatEventKind.HazardDamage:
                            if (combatEvent.Amount <= MeaningfulAmountEpsilon)
                            {
                                break;
                            }

                            DamageCount++;
                            TotalDamage += combatEvent.Amount;
                            if (FirstDamageTick < 0)
                            {
                                FirstDamageTick = combatEvent.Tick;
                            }

                            int abilityIndex = (int)combatEvent.Ability;
                            if (abilityIndex > 0 &&
                                abilityIndex < _damageAbilities.Length &&
                                !_damageAbilities[abilityIndex])
                            {
                                _damageAbilities[abilityIndex] = true;
                                DamageAbilityCount++;
                            }

                            ObserveActivity(combatEvent.Tick);
                            break;
                        case CombatEventKind.Heal:
                            if (combatEvent.Amount <= MeaningfulAmountEpsilon)
                            {
                                break;
                            }

                            HealCount++;
                            TotalHealing += combatEvent.Amount;
                            ObserveActivity(combatEvent.Tick);
                            break;
                        case CombatEventKind.Death:
                            DeathCount++;
                            ObserveActivity(combatEvent.Tick);
                            break;
                    }
                }
            }

            public void Finish(
                int durationTicks,
                SessionOutcome outcome,
                bool reachedTimeCap)
            {
                DurationTicks = durationTicks;
                Outcome = outcome;
                ReachedTimeCap = reachedTimeCap;
                int finalGap = durationTicks - LastActivityTick;
                MaximumActivityGapTicks = Math.Max(MaximumActivityGapTicks, finalGap);
            }

            private void ObserveActivity(int tick)
            {
                int gap = tick - _previousActivityTick;
                MaximumActivityGapTicks = Math.Max(MaximumActivityGapTicks, gap);
                _previousActivityTick = tick;
                LastActivityTick = tick;
            }
        }
    }
}
