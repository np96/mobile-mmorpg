using System;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class DungeonRunControllerTests
    {
        [Test]
        public void PartyDefeat_OffersSameFloorRetryWithoutTerminalResult()
        {
            var controller = new DungeonRunController(new RunId(44u));

            Assert.That(
                controller.TryAdvanceToNextFloor(0, out _),
                Is.False);
            Assert.That(
                controller.TryRetryCurrentFloor(0, out _),
                Is.False);
            Assert.That(
                controller.TryStartFirstFloor(0, out EncounterDefinition floor),
                Is.True);
            Assert.That(floor.Id, Is.EqualTo(new EncounterId(2001u)));
            for (int heroIndex = 0;
                heroIndex < DungeonRunCatalog.HeroCount;
                heroIndex++)
            {
                Assert.That(
                    controller.GetHero(heroIndex),
                    Is.EqualTo(new ParticipantId(heroIndex + 1)));
            }

            EncounterResult defeat = default;
            for (int heroIndex = 0;
                heroIndex < DungeonRunCatalog.HeroCount;
                heroIndex++)
            {
                defeat = controller.ObserveCombatEvent(
                    CombatEvent.Create(
                        heroIndex + 1,
                        CombatEventKind.Death,
                        floor.GetSpawn(0).Participant,
                        controller.GetHero(heroIndex)));
                Assert.That(defeat.WasHandled, Is.True);
                Assert.That(
                    defeat.PartyDefeated,
                    Is.EqualTo(heroIndex == DungeonRunCatalog.HeroCount - 1));
            }

            Assert.That(defeat.PartyDefeated, Is.True);
            Assert.That(defeat.RetryAvailable, Is.True);
            Assert.That(defeat.RunFailed, Is.False);
            Assert.That(controller.State.Status, Is.EqualTo(RunStatus.AwaitingRetry));
            Assert.That(controller.State.CurrentEncounterIndex, Is.Zero);
            Assert.That(controller.State.CurrentAttempt, Is.EqualTo(1));
            Assert.That(controller.State.LivingPartyCount, Is.Zero);
            Assert.That(controller.State.RemainingSpawnCount, Is.Zero);
            Assert.That(controller.State.ClearedEncounterCount, Is.Zero);
            Assert.That(controller.Result.IsTerminal, Is.False);
            Assert.That(
                controller.TryAdvanceToNextFloor(5, out _),
                Is.False);
            Assert.That(
                controller.TryRetryCurrentFloor(4, out _),
                Is.False,
                "A retry cannot begin on the defeat tick.");

            Assert.That(
                controller.TryRetryCurrentFloor(
                    5,
                    out EncounterDefinition retryFloor),
                Is.True);
            Assert.That(retryFloor.Id, Is.EqualTo(floor.Id));
            Assert.That(controller.State.Status, Is.EqualTo(RunStatus.EncounterActive));
            Assert.That(controller.State.CurrentAttempt, Is.EqualTo(2));
            Assert.That(controller.State.CurrentRetryCount, Is.EqualTo(1));
            Assert.That(controller.State.LivingPartyCount, Is.EqualTo(4));
            Assert.That(
                controller.State.RemainingSpawnCount,
                Is.EqualTo(retryFloor.SpawnCount));
            Assert.That(
                controller.ObserveCombatEvent(
                    SpawnDeath(4, controller.Hero, retryFloor.GetSpawn(0))).WasHandled,
                Is.False,
                "Pre-retry events must not resolve the new attempt.");
        }

        [Test]
        public void Floors_ClearAdvanceAndRetryThroughDeterministicVictory()
        {
            var first = new DungeonRunController(new RunId(9u));
            var replay = new DungeonRunController(new RunId(9u));
            int tick = 0;

            Assert.That(first.TryStartFirstFloor(tick, out _), Is.True);
            Assert.That(replay.TryStartFirstFloor(tick, out _), Is.True);

            EncounterResult firstDefeat =
                DefeatParty(first, first.Definition.GetEncounter(0), 1);
            EncounterResult replayDefeat =
                DefeatParty(replay, replay.Definition.GetEncounter(0), 1);
            Assert.That(replayDefeat.RetryAvailable, Is.EqualTo(firstDefeat.RetryAvailable));
            Assert.That(first.TryRetryCurrentFloor(5, out _), Is.True);
            Assert.That(replay.TryRetryCurrentFloor(5, out _), Is.True);
            tick = 5;

            for (int floorIndex = 0;
                floorIndex < DungeonRunCatalog.FloorCount;
                floorIndex++)
            {
                EncounterDefinition floor =
                    first.Definition.GetEncounter(floorIndex);
                for (int spawnIndex = 0;
                    spawnIndex < floor.SpawnCount;
                    spawnIndex++)
                {
                    tick++;
                    CombatEvent death =
                        SpawnDeath(tick, first.Hero, floor.GetSpawn(spawnIndex));
                    EncounterResult firstObservation =
                        first.ObserveCombatEvent(death);
                    EncounterResult replayObservation =
                        replay.ObserveCombatEvent(death);
                    Assert.That(
                        replayObservation.WasHandled,
                        Is.EqualTo(firstObservation.WasHandled));
                    Assert.That(
                        replayObservation.DefeatedSpawn,
                        Is.EqualTo(firstObservation.DefeatedSpawn));
                    Assert.That(
                        replayObservation.EncounterCleared,
                        Is.EqualTo(firstObservation.EncounterCleared));
                    Assert.That(
                        replayObservation.RunCompleted,
                        Is.EqualTo(firstObservation.RunCompleted));
                    AssertStateEqual(first.State, replay.State);
                }

                if (floorIndex < DungeonRunCatalog.FloorCount - 1)
                {
                    Assert.That(
                        first.State.Status,
                        Is.EqualTo(RunStatus.AwaitingAdvance));
                    Assert.That(
                        first.TryRetryCurrentFloor(tick + 1, out _),
                        Is.False,
                        "A cleared floor cannot be retried.");
                    Assert.That(
                        first.TryAdvanceToNextFloor(tick, out _),
                        Is.False,
                        "Advance is a distinct later-tick boundary.");
                    tick++;
                    Assert.That(
                        first.TryAdvanceToNextFloor(
                            tick,
                            out EncounterDefinition nextFloor),
                        Is.True);
                    Assert.That(
                        replay.TryAdvanceToNextFloor(tick, out _),
                        Is.True);
                    Assert.That(
                        nextFloor.Id,
                        Is.EqualTo(first.Definition.GetEncounter(floorIndex + 1).Id));
                    Assert.That(first.State.CurrentAttempt, Is.EqualTo(1));
                }
            }

            Assert.That(first.State.Status, Is.EqualTo(RunStatus.Completed));
            Assert.That(first.State.ClearedEncounterCount, Is.EqualTo(4));
            Assert.That(first.Result.Outcome, Is.EqualTo(RunOutcome.Victory));
            Assert.That(
                first.Result.Reason,
                Is.EqualTo(RunEndReason.AllEncountersCleared));
            Assert.That(
                first.Result.TerminalEncounter,
                Is.EqualTo(new EncounterId(2004u)));
            Assert.That(first.Result.TerminalTick, Is.EqualTo(tick));
            Assert.That(first.Result.ClearedEncounterCount, Is.EqualTo(4));
        }

        [Test]
        public void AbandoningRetryState_ProducesTerminalDefeat()
        {
            var controller = new DungeonRunController();
            Assert.That(controller.TryStartFirstFloor(0, out EncounterDefinition floor), Is.True);
            DefeatParty(controller, floor, 1);

            Assert.That(controller.State.Status, Is.EqualTo(RunStatus.AwaitingRetry));
            Assert.That(controller.TryAbandon(3), Is.False);
            Assert.That(controller.TryAbandon(5), Is.True);
            Assert.That(controller.State.Status, Is.EqualTo(RunStatus.Failed));
            Assert.That(controller.Result.Outcome, Is.EqualTo(RunOutcome.Defeat));
            Assert.That(controller.Result.Reason, Is.EqualTo(RunEndReason.Abandoned));
            Assert.That(controller.Result.TerminalTick, Is.EqualTo(5));
            Assert.That(controller.TryRetryCurrentFloor(6, out _), Is.False);
        }

        [Test]
        public void InterfaceAndObservationPath_AfterWarmupAllocateZeroManagedBytes()
        {
            IRunController controller = new DungeonRunController();
            Assert.That(
                controller.TryStartNextEncounter(0, out _),
                Is.True);
            CombatEvent unknownDeath = CombatEvent.Create(
                1,
                CombatEventKind.Death,
                new ParticipantId(1),
                new ParticipantId(9999));
            int checksum = 0;

            for (int index = 0; index < 256; index++)
            {
                checksum += controller.ObserveCombatEvent(unknownDeath).WasHandled ? 1 : 0;
                checksum += controller.State.RemainingSpawnCount;
                checksum += controller.Result.IsTerminal ? 1 : 0;
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 4096; index++)
            {
                checksum += controller.ObserveCombatEvent(unknownDeath).WasHandled ? 1 : 0;
                checksum += controller.State.RemainingSpawnCount;
                checksum += controller.Result.IsTerminal ? 1 : 0;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero);
            Assert.That(checksum, Is.GreaterThan(0));
        }

        private static CombatEvent SpawnDeath(
            int tick,
            ParticipantId hero,
            RunSpawnDefinition spawn)
        {
            return CombatEvent.Create(
                tick,
                CombatEventKind.Death,
                hero,
                spawn.Participant);
        }

        private static EncounterResult DefeatParty(
            DungeonRunController controller,
            EncounterDefinition floor,
            int firstTick)
        {
            EncounterResult observation = default;
            for (int heroIndex = 0;
                heroIndex < DungeonRunCatalog.HeroCount;
                heroIndex++)
            {
                observation = controller.ObserveCombatEvent(
                    CombatEvent.Create(
                        firstTick + heroIndex,
                        CombatEventKind.Death,
                        floor.GetSpawn(0).Participant,
                        controller.GetHero(heroIndex)));
            }

            return observation;
        }

        private static void AssertStateEqual(RunState first, RunState second)
        {
            Assert.That(second.Status, Is.EqualTo(first.Status));
            Assert.That(
                second.CurrentEncounterIndex,
                Is.EqualTo(first.CurrentEncounterIndex));
            Assert.That(second.CurrentAttempt, Is.EqualTo(first.CurrentAttempt));
            Assert.That(
                second.ClearedEncounterCount,
                Is.EqualTo(first.ClearedEncounterCount));
            Assert.That(second.LivingPartyCount, Is.EqualTo(first.LivingPartyCount));
            Assert.That(
                second.RemainingSpawnCount,
                Is.EqualTo(first.RemainingSpawnCount));
            Assert.That(second.LastTransitionTick, Is.EqualTo(first.LastTransitionTick));
        }

        private static void ForceCollectionAndPrimeCounter()
        {
            GC.GetAllocatedBytesForCurrentThread();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.GetAllocatedBytesForCurrentThread();
        }
    }
}
