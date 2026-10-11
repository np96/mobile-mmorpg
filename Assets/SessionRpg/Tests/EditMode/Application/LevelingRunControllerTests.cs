using System.Collections.Generic;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class LevelingRunControllerTests
    {
        private static readonly ParticipantId Player = new ParticipantId(1);

        [Test]
        public void AshenCauseway_UsesStableBoundedFivePackDefinition()
        {
            LevelingZoneDefinition zone = LevelingZoneCatalog.AshenCauseway;
            int[] expectedPackCounts = { 2, 2, 2, 3, 1 };
            float[] expectedHealth =
            {
                52f, 52f,
                68f, 58f,
                92f, 92f,
                82f, 82f, 72f,
                260f,
            };
            int[] expectedRewards =
            {
                40, 40,
                60, 60,
                90, 90,
                100, 100, 100,
                450,
            };
            float[] expectedActivationX = { -184f, -120f, -56f, 76f, 184f };
            float[] expectedEncounterX = { -162f, -98f, -34f, 98f, 206f };
            LevelingMobArchetypeId[] expectedArchetypes =
            {
                LevelingMobArchetypeId.AshMelee,
                LevelingMobArchetypeId.AshRanged,
                LevelingMobArchetypeId.AshElite,
                LevelingMobArchetypeId.RimeFrostweaver,
                LevelingMobArchetypeId.AshBrute,
                LevelingMobArchetypeId.ReliquaryWarder,
                LevelingMobArchetypeId.AshMelee,
                LevelingMobArchetypeId.AshRanged,
                LevelingMobArchetypeId.AshCaster,
                LevelingMobArchetypeId.RiftWarden,
            };
            bool[] seenParticipant = new bool[12];
            var seenArchetypes = new HashSet<LevelingMobArchetypeId>();
            int mobIndex = 0;
            int totalExperience = 0;

            Assert.That(zone.World, Is.EqualTo(new WorldId(1u)));
            Assert.That(zone.ContentZone, Is.EqualTo(new ZoneId(1u)));
            Assert.That(zone.Id, Is.EqualTo(LevelingZoneId.AshenCauseway));
            Assert.That(zone.PackCount, Is.EqualTo(5));
            for (int packIndex = 0; packIndex < zone.PackCount; packIndex++)
            {
                LevelingPackDefinition pack = zone.GetPack(packIndex);
                Assert.That(pack.Index, Is.EqualTo(packIndex));
                Assert.That(pack.Number, Is.EqualTo(packIndex + 1));
                Assert.That(
                    pack.ActivationX,
                    Is.EqualTo(expectedActivationX[packIndex]));
                Assert.That(
                    pack.EncounterX,
                    Is.EqualTo(expectedEncounterX[packIndex]));
                Assert.That(pack.MobCount, Is.EqualTo(expectedPackCounts[packIndex]));
                Assert.That(
                    pack.MobCount,
                    Is.LessThanOrEqualTo(LevelingZoneDefinition.MaximumActiveMobCount));

                for (int index = 0; index < pack.MobCount; index++)
                {
                    LevelingMobDefinition mob = pack.GetMob(index);
                    Assert.That(mob.Participant.Value, Is.EqualTo(mobIndex + 2));
                    Assert.That(seenParticipant[mob.Participant.Value], Is.False);
                    seenParticipant[mob.Participant.Value] = true;
                    Assert.That(mob.MaximumHealth, Is.EqualTo(expectedHealth[mobIndex]));
                    Assert.That(mob.ExperienceReward, Is.EqualTo(expectedRewards[mobIndex]));
                    Assert.That(mob.Level, Is.EqualTo(packIndex + 1));
                    Assert.That(mob.Archetype, Is.EqualTo(expectedArchetypes[mobIndex]));
                    seenArchetypes.Add(mob.Archetype);
                    totalExperience += mob.ExperienceReward;
                    mobIndex++;
                }
            }

            Assert.That(mobIndex, Is.EqualTo(10));
            Assert.That(
                mobIndex + 1,
                Is.LessThan(CombatSimulation.MaxCombatants),
                "The bounded slice must preserve one spare Core combatant slot.");
            Assert.That(totalExperience, Is.EqualTo(LevelProgressionCurve.MaximumExperience));
            Assert.That(seenArchetypes, Has.Count.EqualTo(8));
        }

        [Test]
        public void AshenCauseway_MapsSevenStableDistrictsAndPackDestinations()
        {
            string[] expectedNames =
            {
                "WEST REFUGE",
                "CINDER APPROACH",
                "BROKEN GATE",
                "COLLAPSED COURT",
                "OSSUARY BEND",
                "RELIQUARY YARD",
                "EAST WATCH",
            };
            for (int index = 0; index < LevelingDistrictCatalog.Count; index++)
            {
                LevelingDistrictDefinition district =
                    LevelingDistrictCatalog.GetByIndex(index);
                int chunkX = index - 3;
                Assert.That(
                    district.Id,
                    Is.EqualTo((LevelingDistrictId)(index + 1)));
                Assert.That(
                    district.Coordinate,
                    Is.EqualTo(new WorldChunkCoordinate(chunkX, 0)));
                Assert.That(district.DisplayName, Is.EqualTo(expectedNames[index]));
                Assert.That(
                    LevelingDistrictCatalog.GetAtWorldX(
                        (chunkX * 64f) + 32f).Id,
                    Is.EqualTo(district.Id));
            }

            LevelingDistrictId[] expectedPackDistricts =
            {
                LevelingDistrictId.WestRefuge,
                LevelingDistrictId.CinderApproach,
                LevelingDistrictId.BrokenGate,
                LevelingDistrictId.OssuaryBend,
                LevelingDistrictId.EastWatch,
            };
            LevelingZoneDefinition zone = LevelingZoneCatalog.AshenCauseway;
            for (int packIndex = 0; packIndex < zone.PackCount; packIndex++)
            {
                Assert.That(
                    LevelingDistrictCatalog.GetAtWorldX(
                        zone.GetPack(packIndex).EncounterX).Id,
                    Is.EqualTo(expectedPackDistricts[packIndex]));
            }

            Assert.That(
                LevelingDistrictCatalog.GetAtWorldX(-10000f).Id,
                Is.EqualTo(LevelingDistrictId.WestRefuge));
            Assert.That(
                LevelingDistrictCatalog.GetAtWorldX(10000f).Id,
                Is.EqualTo(LevelingDistrictId.EastWatch));
        }

        [Test]
        public void AuthoredMobLevels_PreserveCatalogMaximumHealthInCore()
        {
            LevelingZoneDefinition zone = LevelingZoneCatalog.AshenCauseway;

            for (int packIndex = 0; packIndex < zone.PackCount; packIndex++)
            {
                LevelingPackDefinition pack = zone.GetPack(packIndex);
                for (int mobIndex = 0; mobIndex < pack.MobCount; mobIndex++)
                {
                    LevelingMobDefinition mob = pack.GetMob(mobIndex);
                    LevelingMobArchetypeDefinition profile =
                        LevelingMobArchetypeCatalog.Get(mob.Archetype);
                    var simulation = new CombatSimulation(CombatRules.LevelingDefault);
                    Assert.That(
                        simulation.AddCombatant(
                            mob.Participant,
                            new TeamId(2),
                            profile.Class,
                            profile.Specialization,
                            default,
                            mob.BaseMaximumHealth,
                            false),
                        Is.True);

                    if (mob.Level > LevelProgressionCurve.MinimumLevel)
                    {
                        Assert.That(
                            simulation.ApplyProgressionLevel(
                                mob.Participant,
                                mob.Level),
                            Is.True);
                    }

                    Assert.That(
                        simulation.TryGetSnapshot(
                            mob.Participant,
                            out CombatantSnapshot snapshot),
                        Is.True);
                    Assert.That(snapshot.Level, Is.EqualTo(mob.Level));
                    Assert.That(
                        snapshot.MaxHealth,
                        Is.EqualTo(mob.MaximumHealth).Within(0.0001f));
                    Assert.That(
                        snapshot.Health,
                        Is.EqualTo(mob.MaximumHealth).Within(0.0001f));
                }
            }
        }

        [Test]
        public void NewRun_StartsAtLevelOneWithFirstPackReady()
        {
            var run = new LevelingRunController();

            Assert.That(run.Player, Is.EqualTo(Player));
            Assert.That(run.Zone.Id, Is.EqualTo(LevelingZoneId.AshenCauseway));
            Assert.That(run.Status, Is.EqualTo(LevelingRunStatus.AwaitingPackActivation));
            Assert.That(run.Progression.Level, Is.EqualTo(1));
            Assert.That(run.Progression.TotalExperience, Is.Zero);
            Assert.That(run.NextPackIndex, Is.Zero);
            Assert.That(run.NextPackReadyTick, Is.Zero);
            Assert.That(run.IsNextPackReady(0), Is.True);
            Assert.That(run.GetTicksUntilNextPackReady(0), Is.Zero);
            Assert.That(run.ActivePackIndex, Is.EqualTo(-1));
            Assert.That(run.ActiveMobCount, Is.Zero);
        }

        [Test]
        public void ActivateFirstPack_ExposesStableActiveIdsAndRegistersOnlyThatPack()
        {
            var run = new LevelingRunController();

            Assert.That(run.TryActivateNextPack(0, out LevelingPackDefinition pack), Is.True);
            Assert.That(pack.Index, Is.Zero);
            Assert.That(run.Status, Is.EqualTo(LevelingRunStatus.FightingPack));
            Assert.That(run.ActivePackIndex, Is.Zero);
            Assert.That(run.ActivePackActivationTick, Is.Zero);
            Assert.That(run.NextPackIndex, Is.EqualTo(1));
            Assert.That(run.ActiveMobCount, Is.EqualTo(2));
            Assert.That(run.GetActiveMobId(0), Is.EqualTo(new ParticipantId(2)));
            Assert.That(run.GetActiveMobId(1), Is.EqualTo(new ParticipantId(3)));
            Assert.That(run.IsMobRegistered(new ParticipantId(2)), Is.True);
            Assert.That(run.IsMobRegistered(new ParticipantId(3)), Is.True);
            Assert.That(run.IsMobRegistered(new ParticipantId(4)), Is.False);
            Assert.That(run.TryActivateNextPack(0, out _), Is.False);
        }

        [Test]
        public void LaterPack_WaitsExactlyFortyFiveTicksAfterPreviousClear()
        {
            var run = new LevelingRunController();
            run.TryActivateNextPack(0, out _);
            run.ObserveCombatEvent(Death(10, Player, 2));
            LevelingCombatObservation clear = run.ObserveCombatEvent(Death(12, Player, 3));

            Assert.That(clear.PackCleared, Is.True);
            Assert.That(run.Status, Is.EqualTo(LevelingRunStatus.AwaitingPackActivation));
            Assert.That(run.NextPackReadyTick, Is.EqualTo(57));
            Assert.That(run.GetTicksUntilNextPackReady(12), Is.EqualTo(45));
            Assert.That(run.GetTicksUntilNextPackReady(56), Is.EqualTo(1));
            Assert.That(run.IsNextPackReady(56), Is.False);
            Assert.That(run.TryActivateNextPack(56, out _), Is.False);
            Assert.That(run.IsNextPackReady(57), Is.True);
            Assert.That(run.TryActivateNextPack(57, out LevelingPackDefinition pack), Is.True);
            Assert.That(pack.Index, Is.EqualTo(1));
        }

        [Test]
        public void LaterPack_AlsoRequiresAuthoritativeEastwardProgress()
        {
            var run = new LevelingRunController();
            run.TryActivateNextPack(0, -182f, out _);
            run.ObserveCombatEvent(Death(1, Player, 2));
            run.ObserveCombatEvent(Death(2, Player, 3));
            int readyTick = run.NextPackReadyTick;

            Assert.That(run.IsNextPackReady(readyTick, -120.01f), Is.False);
            Assert.That(
                run.TryActivateNextPack(readyTick, -120.01f, out _),
                Is.False);
            Assert.That(run.IsNextPackReady(readyTick, -120f), Is.True);
            Assert.That(
                run.TryActivateNextPack(
                    readyTick,
                    -120f,
                    out LevelingPackDefinition pack),
                Is.True);
            Assert.That(pack.Index, Is.EqualTo(1));
        }

        [Test]
        public void PlayerCreditedDeaths_AwardOnceAndReachOneLevelPerPack()
        {
            var run = new LevelingRunController();
            int currentTick = 0;
            int[] expectedCumulativeExperience = { 80, 200, 380, 680, 1130 };

            for (int packIndex = 0; packIndex < run.Zone.PackCount; packIndex++)
            {
                Assert.That(run.TryActivateNextPack(currentTick, out LevelingPackDefinition pack),
                    Is.True);
                LevelingCombatObservation finalObservation = default;
                for (int mobIndex = 0; mobIndex < pack.MobCount; mobIndex++)
                {
                    currentTick++;
                    LevelingMobDefinition mob = pack.GetMob(mobIndex);
                    finalObservation = run.ObserveCombatEvent(
                        Death(currentTick, Player, mob.Participant.Value));
                    Assert.That(finalObservation.WasHandled, Is.True);
                    Assert.That(
                        finalObservation.ExperienceAwarded,
                        Is.EqualTo(mob.ExperienceReward));

                    LevelingCombatObservation duplicate = run.ObserveCombatEvent(
                        Death(currentTick, Player, mob.Participant.Value));
                    Assert.That(duplicate.WasHandled, Is.False);
                    Assert.That(duplicate.ExperienceAwarded, Is.Zero);
                }

                Assert.That(finalObservation.PackCleared, Is.True);
                Assert.That(run.Progression.Level, Is.EqualTo(packIndex + 2));
                Assert.That(
                    run.Progression.TotalExperience,
                    Is.EqualTo(expectedCumulativeExperience[packIndex]));

                if (packIndex < run.Zone.PackCount - 1)
                {
                    Assert.That(finalObservation.RunCompleted, Is.False);
                    currentTick = run.NextPackReadyTick;
                }
            }

            Assert.That(run.IsCompleted, Is.True);
            Assert.That(run.IsFailed, Is.False);
            Assert.That(run.Status, Is.EqualTo(LevelingRunStatus.Completed));
            Assert.That(run.Progression.Level, Is.EqualTo(6));
            Assert.That(run.Progression.TotalExperience, Is.EqualTo(1130));
            Assert.That(run.HasNextPack, Is.False);
            Assert.That(run.ActiveMobCount, Is.Zero);
        }

        [Test]
        public void OnlyPlayerCreditedRegisteredDeathsGrantExperience()
        {
            var run = new LevelingRunController();
            run.TryActivateNextPack(0, out _);

            LevelingCombatObservation future = run.ObserveCombatEvent(Death(1, Player, 11));
            LevelingCombatObservation unknown = run.ObserveCombatEvent(Death(1, Player, 99));
            LevelingCombatObservation nonDeath = run.ObserveCombatEvent(
                CombatEvent.Create(
                    1,
                    CombatEventKind.Damage,
                    Player,
                    new ParticipantId(2),
                    amount: 52f));
            Assert.That(future.WasHandled, Is.False);
            Assert.That(unknown.WasHandled, Is.False);
            Assert.That(nonDeath.WasHandled, Is.False);
            Assert.That(run.Progression.TotalExperience, Is.Zero);

            LevelingCombatObservation uncredited = run.ObserveCombatEvent(
                Death(2, new ParticipantId(2), 2));
            Assert.That(uncredited.WasHandled, Is.True);
            Assert.That(uncredited.ExperienceAwarded, Is.Zero);
            Assert.That(run.Progression.TotalExperience, Is.Zero);

            LevelingCombatObservation replayedWithPlayerCredit = run.ObserveCombatEvent(
                Death(3, Player, 2));
            Assert.That(replayedWithPlayerCredit.WasHandled, Is.False);
            Assert.That(run.Progression.TotalExperience, Is.Zero);

            LevelingCombatObservation credited = run.ObserveCombatEvent(Death(4, Player, 3));
            Assert.That(credited.ExperienceAwarded, Is.EqualTo(40));
            Assert.That(credited.PackCleared, Is.True);
            Assert.That(run.Progression.TotalExperience, Is.EqualTo(40));
        }

        [Test]
        public void ActiveMobIds_CompactAfterEachResolvedDeath()
        {
            var run = new LevelingRunController();
            int tick = 0;
            ClearPack(run, 0, ref tick);
            Assert.That(run.TryActivateNextPack(run.NextPackReadyTick, out _), Is.True);
            tick = run.NextPackReadyTick;
            ClearActivePack(run, ref tick);
            Assert.That(run.TryActivateNextPack(run.NextPackReadyTick, out _), Is.True);
            tick = run.NextPackReadyTick;
            ClearActivePack(run, ref tick);
            Assert.That(run.TryActivateNextPack(run.NextPackReadyTick, out _), Is.True);
            tick = run.NextPackReadyTick;

            Assert.That(run.ActiveMobCount, Is.EqualTo(3));
            Assert.That(run.GetActiveMobId(0), Is.EqualTo(new ParticipantId(8)));
            Assert.That(run.GetActiveMobId(1), Is.EqualTo(new ParticipantId(9)));
            Assert.That(run.GetActiveMobId(2), Is.EqualTo(new ParticipantId(10)));

            run.ObserveCombatEvent(Death(++tick, Player, 9));
            Assert.That(run.ActiveMobCount, Is.EqualTo(2));
            Assert.That(run.GetActiveMobId(0), Is.EqualTo(new ParticipantId(8)));
            Assert.That(run.GetActiveMobId(1), Is.EqualTo(new ParticipantId(10)));
        }

        [Test]
        public void PlayerDeath_FailsTheRunAndBlocksFurtherMutation()
        {
            var run = new LevelingRunController();
            run.TryActivateNextPack(0, out _);

            LevelingCombatObservation failure = run.ObserveCombatEvent(
                Death(
                    3,
                    new ParticipantId(2),
                    LevelingRunController.PlayerParticipantValue));

            Assert.That(failure.WasHandled, Is.True);
            Assert.That(failure.RunFailed, Is.True);
            Assert.That(run.IsFailed, Is.True);
            Assert.That(run.ActivePackIndex, Is.EqualTo(-1));
            Assert.That(run.ActiveMobCount, Is.Zero);
            Assert.That(run.TryActivateNextPack(100, out _), Is.False);
            Assert.That(
                run.ObserveCombatEvent(Death(4, Player, 2)).WasHandled,
                Is.False);
            Assert.That(run.Progression.TotalExperience, Is.Zero);
            Assert.That(run.Fail(), Is.False);
        }

        [Test]
        public void FinalPackClear_RequiresMaximumProgressionToComplete()
        {
            var run = new LevelingRunController();
            int tick = 0;
            LevelingCombatObservation finalObservation = default;

            for (int packIndex = 0; packIndex < run.Zone.PackCount; packIndex++)
            {
                Assert.That(
                    run.TryActivateNextPack(tick, out LevelingPackDefinition pack),
                    Is.True);
                for (int mobIndex = 0; mobIndex < pack.MobCount; mobIndex++)
                {
                    LevelingMobDefinition mob = pack.GetMob(mobIndex);
                    ParticipantId actor = packIndex == 0 && mobIndex == 0
                        ? mob.Participant
                        : Player;
                    finalObservation = run.ObserveCombatEvent(
                        Death(++tick, actor, mob.Participant.Value));
                }

                if (packIndex < run.Zone.PackCount - 1)
                {
                    tick = run.NextPackReadyTick;
                }
            }

            Assert.That(finalObservation.PackCleared, Is.True);
            Assert.That(finalObservation.RunCompleted, Is.False);
            Assert.That(finalObservation.RunFailed, Is.True);
            Assert.That(run.IsCompleted, Is.False);
            Assert.That(run.IsFailed, Is.True);
            Assert.That(run.Progression.TotalExperience, Is.EqualTo(1090));
            Assert.That(run.Progression.Level, Is.EqualTo(5));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SameTickFinalMobAndPlayerDeaths_AlwaysFailTheRun(
            bool finalMobDeathArrivesFirst)
        {
            var run = new LevelingRunController();
            int tick = 0;
            for (int packIndex = 0; packIndex < run.Zone.PackCount - 1; packIndex++)
            {
                Assert.That(run.TryActivateNextPack(tick, out _), Is.True);
                ClearActivePack(run, ref tick);
                tick = run.NextPackReadyTick;
            }

            Assert.That(
                run.TryActivateNextPack(tick, out LevelingPackDefinition finalPack),
                Is.True);
            ParticipantId finalMob = finalPack.GetMob(0).Participant;
            int terminalTick = ++tick;
            CombatEvent mobDeath = Death(terminalTick, Player, finalMob.Value);
            CombatEvent playerDeath = Death(
                terminalTick,
                finalMob,
                LevelingRunController.PlayerParticipantValue);

            if (finalMobDeathArrivesFirst)
            {
                Assert.That(run.ObserveCombatEvent(mobDeath).WasHandled, Is.True);
                Assert.That(run.ObserveCombatEvent(playerDeath).WasHandled, Is.True);
            }
            else
            {
                Assert.That(run.ObserveCombatEvent(playerDeath).WasHandled, Is.True);
                Assert.That(run.ObserveCombatEvent(mobDeath).WasHandled, Is.False);
            }

            Assert.That(run.IsFailed, Is.True);
            Assert.That(run.IsCompleted, Is.False);
            Assert.That(run.Status, Is.EqualTo(LevelingRunStatus.Failed));
            Assert.That(run.ActiveMobCount, Is.Zero);
        }

        private static void ClearPack(
            LevelingRunController run,
            int activationTick,
            ref int currentTick)
        {
            currentTick = activationTick;
            Assert.That(run.TryActivateNextPack(currentTick, out _), Is.True);
            ClearActivePack(run, ref currentTick);
        }

        private static void ClearActivePack(
            LevelingRunController run,
            ref int currentTick)
        {
            while (run.ActiveMobCount > 0)
            {
                ParticipantId mob = run.GetActiveMobId(0);
                run.ObserveCombatEvent(Death(++currentTick, Player, mob.Value));
            }
        }

        private static CombatEvent Death(
            int tick,
            ParticipantId actor,
            int target)
        {
            return CombatEvent.Create(
                tick,
                CombatEventKind.Death,
                actor,
                new ParticipantId(target),
                reason: "ability");
        }
    }
}
