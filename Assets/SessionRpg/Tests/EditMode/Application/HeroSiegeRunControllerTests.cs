using System;
using System.Collections.Generic;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class HeroSiegeRunControllerTests
    {
        [Test]
        public void EnemyCatalog_HasNineDistinctRegularArchetypesWithoutBosses()
        {
            var ids = new HashSet<SiegeEnemyArchetypeId>();
            int swarmCount = 0;
            for (int index = 0; index < SiegeEnemyArchetypeCatalog.Count; index++)
            {
                SiegeEnemyArchetypeDefinition enemy =
                    SiegeEnemyArchetypeCatalog.GetByIndex(index);
                Assert.That(enemy.IsValid, Is.True);
                Assert.That(ids.Add(enemy.Id), Is.True);
                Assert.That(enemy.Class, Is.Not.EqualTo(ClassId.Boss));
                Assert.That(enemy.Specialization, Is.Not.EqualTo(SpecializationId.Boss));
                Assert.That(enemy.Rank,
                    Is.EqualTo(RunEnemyRank.Standard).Or.EqualTo(RunEnemyRank.Elite));
                Assert.That(enemy.Tier, Is.Not.EqualTo(RunEnemyTier.Boss));
                Assert.That(AbilityCatalog.TryGet(enemy.PrimaryAbility, out _), Is.True);
                if (enemy.Tier == RunEnemyTier.SwarmMinion)
                {
                    swarmCount++;
                }
            }

            Assert.That(SiegeEnemyArchetypeCatalog.Count, Is.EqualTo(9));
            Assert.That(swarmCount, Is.EqualTo(2));
            Assert.That(SiegeEnemyArchetypeCatalog.Get(
                SiegeEnemyArchetypeId.CinderRunner).RushesObjective, Is.True);
            Assert.That(SiegeEnemyArchetypeCatalog.Get(
                SiegeEnemyArchetypeId.ReliquaryWarder).SignaturePolicy,
                Is.EqualTo(SiegeEnemySignaturePolicy.DefensiveAlly));
        }

        [Test]
        public void EnemyMovementCatalog_UsesReadableSwarmMediumAndElitePaces()
        {
            Assert.That(
                SiegeEnemyArchetypeCatalog.Get(SiegeEnemyArchetypeId.Ashling)
                    .BaseMovementSpeedMultiplierPermille,
                Is.EqualTo(560));
            Assert.That(
                SiegeEnemyArchetypeCatalog.Get(SiegeEnemyArchetypeId.CinderRunner)
                    .BaseMovementSpeedMultiplierPermille,
                Is.EqualTo(640),
                "The objective runner remains the fastest enemy without matching hero speed.");
            Assert.That(
                SiegeEnemyArchetypeCatalog.Get(SiegeEnemyArchetypeId.AshboundRaider)
                    .BaseMovementSpeedMultiplierPermille,
                Is.EqualTo(530));
            Assert.That(
                SiegeEnemyArchetypeCatalog.Get(SiegeEnemyArchetypeId.AshenBrute)
                    .BaseMovementSpeedMultiplierPermille,
                Is.EqualTo(420),
                "The heavy elite should be the most deliberate target.");

            for (int index = 0; index < SiegeEnemyArchetypeCatalog.Count; index++)
            {
                SiegeEnemyArchetypeDefinition enemy =
                    SiegeEnemyArchetypeCatalog.GetByIndex(index);
                Assert.That(enemy.BaseMovementSpeedMultiplierPermille,
                    Is.InRange(420, 640));
            }

            Assert.That(
                CombatRules.HeroSiegeDefault.MoveSpeed * 0.56f,
                Is.EqualTo(3.024f).Within(0.0001f));
            Assert.That(
                CombatRules.HeroSiegeDefault.MoveSpeed * 0.64f,
                Is.EqualTo(3.456f).Within(0.0001f));
            Assert.That(
                CombatRules.HeroSiegeDefault.MoveSpeed * 0.42f,
                Is.EqualTo(2.268f).Within(0.0001f));
        }

        [Test]
        public void WaveMovementScaling_StartsAtAuthoredPaceAndStaysBounded()
        {
            SiegeWaveDefinition openingWave = HeroSiegeRunCatalog.GetWave(1);
            SiegeSpawnDefinition openingAshling = HeroSiegeRunCatalog.CreateSpawn(
                openingWave, SiegeLaneId.East, 0);
            SiegeSpawnDefinition openingRunner = HeroSiegeRunCatalog.CreateSpawn(
                openingWave, SiegeLaneId.East, 1);

            Assert.That(openingWave.MovementSpeedMultiplierPermille,
                Is.EqualTo(1000));
            Assert.That(openingAshling.Archetype.Id,
                Is.EqualTo(SiegeEnemyArchetypeId.Ashling));
            Assert.That(openingAshling.MovementSpeedMultiplierPermille,
                Is.EqualTo(560));
            Assert.That(openingRunner.Archetype.Id,
                Is.EqualTo(SiegeEnemyArchetypeId.CinderRunner));
            Assert.That(openingRunner.MovementSpeedMultiplierPermille,
                Is.EqualTo(640));

            SiegeWaveDefinition finalWave = HeroSiegeRunCatalog.GetWave(
                HeroSiegeRunCatalog.WaveCount);
            SiegeSpawnDefinition finalRunner = HeroSiegeRunCatalog.CreateSpawn(
                finalWave, SiegeLaneId.East, 8);
            Assert.That(finalWave.MovementSpeedMultiplierPermille,
                Is.EqualTo(1110));
            Assert.That(finalRunner.Archetype.Id,
                Is.EqualTo(SiegeEnemyArchetypeId.CinderRunner));
            Assert.That(finalRunner.MovementSpeedMultiplierPermille,
                Is.EqualTo(710));
            Assert.That(finalRunner.MovementSpeedMultiplierPermille,
                Is.GreaterThan(openingRunner.MovementSpeedMultiplierPermille));
            Assert.That(
                CombatRules.HeroSiegeDefault.MoveSpeed *
                    finalRunner.MovementSpeedMultiplierPermille / 1000f,
                Is.EqualTo(3.834f).Within(0.0001f));
        }

        [Test]
        public void WaveCatalog_EscalatesAndIntroducesAllArchetypesByWaveEight()
        {
            var introduced = new HashSet<SiegeEnemyArchetypeId>();
            SiegeWaveDefinition previous = default;
            for (int number = 1; number <= HeroSiegeRunCatalog.WaveCount; number++)
            {
                SiegeWaveDefinition wave = HeroSiegeRunCatalog.GetWave(number);
                Assert.That(wave.Id, Is.EqualTo(new SiegeWaveId((uint)number)));
                Assert.That(wave.SpawnCountPerLane,
                    Is.LessThanOrEqualTo(HeroSiegeRunCatalog.MaximumSpawnsPerLane));
                if (previous.Id.IsValid)
                {
                    Assert.That(wave.SpawnCountPerLane,
                        Is.GreaterThanOrEqualTo(previous.SpawnCountPerLane));
                    Assert.That(wave.HealthMultiplierPermille,
                        Is.GreaterThan(previous.HealthMultiplierPermille));
                    Assert.That(wave.DamageMultiplierPermille,
                        Is.GreaterThan(previous.DamageMultiplierPermille));
                    Assert.That(wave.MovementSpeedMultiplierPermille,
                        Is.GreaterThan(previous.MovementSpeedMultiplierPermille));
                }

                for (int ordinal = 0; ordinal < wave.SpawnCountPerLane; ordinal++)
                {
                    SiegeEnemyArchetypeId id = wave.GetArchetype(ordinal);
                    if (number <= 8)
                    {
                        introduced.Add(id);
                    }

                    SiegeSpawnDefinition spawn = HeroSiegeRunCatalog.CreateSpawn(
                        wave, SiegeLaneId.East, ordinal);
                    Assert.That(spawn.Archetype.Id, Is.EqualTo(id));
                    Assert.That(spawn.Archetype.Tier, Is.Not.EqualTo(RunEnemyTier.Boss));
                    Assert.That(spawn.ToRunSpawnDefinition().IsValid, Is.True);
                }

                previous = wave;
            }

            Assert.That(introduced.Count, Is.EqualTo(SiegeEnemyArchetypeCatalog.Count));
        }

        [Test]
        public void SpawnIdentities_AreStableAndUniqueAcrossWaveLaneAndOrdinal()
        {
            var spawnIds = new HashSet<uint>();
            var participants = new HashSet<int>();
            for (int waveNumber = 1;
                waveNumber <= HeroSiegeRunCatalog.WaveCount;
                waveNumber++)
            {
                SiegeWaveDefinition wave = HeroSiegeRunCatalog.GetWave(waveNumber);
                for (int lane = 1; lane <= HeroSiegeRunCatalog.MaximumLaneCount; lane++)
                {
                    for (int ordinal = 0; ordinal < wave.SpawnCountPerLane; ordinal++)
                    {
                        SiegeSpawnDefinition first = HeroSiegeRunCatalog.CreateSpawn(
                            wave, (SiegeLaneId)lane, ordinal);
                        SiegeSpawnDefinition replay = HeroSiegeRunCatalog.CreateSpawn(
                            wave, (SiegeLaneId)lane, ordinal);
                        Assert.That(first.Id, Is.EqualTo(replay.Id));
                        Assert.That(first.Participant, Is.EqualTo(replay.Participant));
                        Assert.That(spawnIds.Add(first.Id.Value), Is.True);
                        Assert.That(participants.Add(first.Participant.Value), Is.True);
                    }
                }
            }
        }

        [Test]
        public void FourLaneSoak_PreservesCapsAndUniqueIdentitiesAcrossAllWaves()
        {
            var controller = new HeroSiegeRunController(
                new RunId(91u),
                SiegeVariantId.FourLanes);
            var spawnIds = new HashSet<uint>();
            var participants = new HashSet<int>();
            int expectedTotal = 0;
            int tick = 0;

            for (int waveNumber = 1;
                waveNumber <= HeroSiegeRunCatalog.WaveCount;
                waveNumber++)
            {
                SiegeWaveDefinition wave = HeroSiegeRunCatalog.GetWave(waveNumber);
                expectedTotal += wave.SpawnCountPerLane *
                    HeroSiegeRunCatalog.MaximumLaneCount;
                tick = HeroSiegeRunCatalog.GetWaveStartTick(waveNumber);
                SiegeAdvanceObservation start = controller.Advance(tick);
                Assert.That(start.WaveStarted, Is.True);

                while (!controller.IsWaveCleared(wave.Id))
                {
                    Assert.That(
                        controller.State.ActiveHostileCount +
                        controller.State.PendingSpawnCount,
                        Is.LessThanOrEqualTo(
                            HeroSiegeRunCatalog.MaximumActiveHostileCount));
                    for (int laneIndex = 0;
                        laneIndex < controller.Variant.LaneCount;
                        laneIndex++)
                    {
                        Assert.That(controller.GetLaneOccupiedCount(
                            controller.Variant.GetLane(laneIndex)),
                            Is.LessThanOrEqualTo(
                                controller.Variant.MaximumActiveHostilesPerLane));
                    }

                    while (controller.TryDequeueSpawn(
                        out SiegeSpawnDefinition spawn))
                    {
                        Assert.That(spawnIds.Add(spawn.Id.Value), Is.True);
                        Assert.That(participants.Add(spawn.Participant.Value), Is.True);
                        Assert.That(
                            ArenaCollisionMap.SiegeDefault.IsPositionValid(
                                spawn.Position,
                                ArenaLayout.CombatantCollisionRadius),
                            Is.True);
                        SiegeCombatObservation death =
                            controller.ObserveCombatEvent(Death(++tick, spawn));
                        Assert.That(death.WasHandled, Is.True);
                    }

                    if (!controller.IsWaveCleared(wave.Id))
                    {
                        tick += HeroSiegeRunCatalog.SpawnIntervalTicks;
                        Assert.That(controller.Advance(tick).WasHandled, Is.True);
                    }
                }

                Assert.That(controller.State.ActiveHostileCount, Is.Zero);
                Assert.That(controller.State.PendingSpawnCount, Is.Zero);
            }

            Assert.That(spawnIds.Count, Is.EqualTo(expectedTotal));
            Assert.That(participants.Count, Is.EqualTo(expectedTotal));
            Assert.That(controller.Result.DidWin, Is.True);
        }

        [Test]
        public void NewRun_DefaultsToSoloPreparationAndOpeningPotionChoice()
        {
            var controller = new HeroSiegeRunController(new RunId(77u));
            Assert.That(controller.Run, Is.EqualTo(new RunId(77u)));
            Assert.That(controller.Mode, Is.EqualTo(PlayableModeId.HeroSiege));
            Assert.That(controller.Variant.Id, Is.EqualTo(SiegeVariantId.SoloLane));
            Assert.That(controller.State.Status, Is.EqualTo(SiegeRunStatus.Preparation));
            Assert.That(controller.State.CurrentWaveNumber, Is.Zero);
            Assert.That(controller.State.TicksUntilNextWave,
                Is.EqualTo(HeroSiegeRunCatalog.InitialPreparationTicks));
            Assert.That(controller.State.LivingDefenderCount, Is.EqualTo(1));
            Assert.That(controller.State.Gold, Is.EqualTo(HeroSiegeRunCatalog.StartingGold));
            Assert.That(controller.State.Objective.Health,
                Is.EqualTo(HeroSiegeRunCatalog.ObjectiveMaximumHealth));

            SiegePurchaseObservation purchase = controller.TryPurchasePotion(
                SiegePotionId.Mana);
            Assert.That(purchase.WasHandled, Is.True);
            Assert.That(purchase.Cost, Is.EqualTo(HeroSiegeRunCatalog.ManaPotionCost));
            Assert.That(controller.State.ManaPotionCount, Is.EqualTo(1));
            Assert.That(controller.TryPurchasePotion(SiegePotionId.Health).WasHandled,
                Is.False, "Opening gold buys one potion, not both.");
        }

        [Test]
        public void Advance_StartsTimedWaveAndEnforcesLaneAndGlobalCaps()
        {
            var controller = new HeroSiegeRunController(
                new RunId(2u), SiegeVariantId.FourLanes);
            Assert.That(controller.Advance(299).WaveStarted, Is.False);
            SiegeAdvanceObservation start = controller.Advance(300);
            Assert.That(start.WaveStarted, Is.True);
            Assert.That(start.Wave, Is.EqualTo(new SiegeWaveId(1u)));
            Assert.That(start.QueuedSpawnCount, Is.EqualTo(8),
                "Four Lanes must open with two attackers per lane, not a spawn drip.");
            Assert.That(controller.TryPurchasePotion(SiegePotionId.Health).WasHandled,
                Is.True, "Earned gold must remain spendable during continuous waves.");
            Assert.That(controller.State.HealthPotionCount, Is.EqualTo(1));

            var firstBatch = new SiegeSpawnDefinition[8];
            for (int index = 0; index < firstBatch.Length; index++)
            {
                Assert.That(controller.TryDequeueSpawn(out firstBatch[index]), Is.True);
                Assert.That(firstBatch[index].Lane,
                    Is.EqualTo(controller.Variant.GetLane(index % 4)));
            }

            Assert.That(controller.State.ActiveHostileCount,
                Is.EqualTo(HeroSiegeRunCatalog.MaximumActiveHostileCount));
            for (int lane = 0; lane < controller.Variant.LaneCount; lane++)
            {
                Assert.That(controller.GetLaneOccupiedCount(
                    controller.Variant.GetLane(lane)), Is.EqualTo(2));
            }

            Assert.That(controller.Advance(315).QueuedSpawnCount, Is.Zero);
            Assert.That(controller.ObserveCombatEvent(Death(315, firstBatch[0])).WasHandled,
                Is.True);
            Assert.That(controller.Advance(316).QueuedSpawnCount, Is.EqualTo(1));
            Assert.That(controller.State.ActiveHostileCount +
                controller.State.PendingSpawnCount,
                Is.EqualTo(HeroSiegeRunCatalog.MaximumActiveHostileCount));
        }

        [Test]
        public void AbsoluteWaveClock_StartsNextWaveBeforePriorWaveIsCleared()
        {
            var controller = new HeroSiegeRunController(new RunId(13u));
            int tick = HeroSiegeRunCatalog.GetWaveStartTick(1);
            controller.Advance(tick);
            while (controller.TryDequeueSpawn(out _))
            {
            }

            SiegeWaveDefinition firstWave = HeroSiegeRunCatalog.GetWave(1);
            while (controller.GetScheduledSpawnCount(firstWave.Id) <
                firstWave.SpawnCountPerLane)
            {
                tick += HeroSiegeRunCatalog.SpawnIntervalTicks;
                controller.Advance(tick);
                while (controller.TryDequeueSpawn(out _))
                {
                }
            }

            Assert.That(controller.State.ActiveHostileCount,
                Is.EqualTo(firstWave.SpawnCountPerLane));
            Assert.That(controller.IsWaveCleared(firstWave.Id), Is.False);

            tick = HeroSiegeRunCatalog.GetWaveStartTick(2);
            SiegeAdvanceObservation overlap = controller.Advance(tick);
            Assert.That(overlap.WaveStarted, Is.True);
            Assert.That(overlap.Wave, Is.EqualTo(new SiegeWaveId(2u)));
            Assert.That(overlap.QueuedSpawnCount, Is.EqualTo(2));
            Assert.That(controller.State.StartedWaveCount, Is.EqualTo(2));
            Assert.That(controller.State.ActiveWaveCount, Is.EqualTo(2));
            Assert.That(controller.State.ActiveHostileCount, Is.EqualTo(6));
            Assert.That(controller.State.PendingSpawnCount, Is.EqualTo(2));
            Assert.That(controller.TryDequeueSpawn(out SiegeSpawnDefinition second),
                Is.True);
            Assert.That(second.Wave, Is.EqualTo(new SiegeWaveId(2u)));
            Assert.That(controller.State.ActiveHostileCount, Is.EqualTo(7));
        }

        [Test]
        public void Deaths_AwardGoldAreIdempotentAndClearWithoutEncounterReset()
        {
            var controller = new HeroSiegeRunController(new RunId(3u));
            int tick = HeroSiegeRunCatalog.InitialPreparationTicks;
            controller.Advance(tick);
            Assert.That(controller.TryDequeueSpawn(out SiegeSpawnDefinition first), Is.True);
            int startingGold = controller.State.Gold;
            SiegeCombatObservation death = controller.ObserveCombatEvent(Death(tick, first));
            Assert.That(death.GoldAwarded, Is.EqualTo(first.GoldReward));
            Assert.That(controller.State.Gold, Is.EqualTo(startingGold + first.GoldReward));
            Assert.That(controller.ObserveCombatEvent(Death(tick, first)).WasHandled,
                Is.False);

            SiegeCombatObservation clear = ClearActiveWave(controller, ref tick);
            Assert.That(clear.WaveCleared, Is.True);
            Assert.That(clear.RunCompleted, Is.False);
            Assert.That(controller.State.Status, Is.EqualTo(SiegeRunStatus.Preparation));
            Assert.That(controller.State.ClearedWaveCount, Is.EqualTo(1));
            Assert.That(controller.State.Kills,
                Is.EqualTo(HeroSiegeRunCatalog.GetWave(1).SpawnCountPerLane));
            Assert.That(controller.State.TicksUntilNextWave,
                Is.EqualTo(HeroSiegeRunCatalog.GetWaveStartTick(2) - tick));

            SiegeAdvanceObservation next = controller.Advance(
                HeroSiegeRunCatalog.GetWaveStartTick(2));
            Assert.That(next.WaveStarted, Is.True);
            Assert.That(next.Wave, Is.EqualTo(new SiegeWaveId(2u)));
            Assert.That(controller.Run, Is.EqualTo(new RunId(3u)));
        }

        [Test]
        public void ObjectivePressure_UsesOnlyActiveLivingSnapshotsInsideBase()
        {
            var controller = new HeroSiegeRunController(new RunId(4u));
            int tick = HeroSiegeRunCatalog.InitialPreparationTicks;
            controller.Advance(tick);
            controller.TryDequeueSpawn(out SiegeSpawnDefinition spawn);
            var snapshots = new CombatantSnapshot[2];
            snapshots[0] = Snapshot(spawn.Participant, HeroSiegeRunCatalog.HostileTeam,
                spawn.Archetype.Class, spawn.Archetype.Specialization,
                new Float3(2.4f, 0f, 0f), spawn.MaximumHealth, spawn.MaximumHealth,
                ResourceType.Energy, 100f, 100f, true);
            snapshots[1] = Snapshot(new ParticipantId(9999),
                HeroSiegeRunCatalog.HostileTeam, ClassId.Rogue,
                SpecializationId.Mutilate, default, 100f, 100f,
                ResourceType.Energy, 100f, 100f, true);

            SiegeObjectivePressureObservation pressure =
                controller.ApplyObjectivePressure(++tick, snapshots, 2, 30);
            Assert.That(pressure.PressureHostileCount, Is.EqualTo(1));
            Assert.That(pressure.RequestedDamage,
                Is.EqualTo(spawn.ObjectiveDamagePerSecond / 30f).Within(0.0001f));
            Assert.That(controller.ApplyObjectivePressure(tick, snapshots, 2, 30)
                .WasHandled, Is.False, "A tick cannot damage the objective twice.");

            snapshots[0] = Snapshot(spawn.Participant,
                HeroSiegeRunCatalog.HostileTeam, spawn.Archetype.Class,
                spawn.Archetype.Specialization, new Float3(8f, 0f, 0f),
                spawn.MaximumHealth, spawn.MaximumHealth,
                ResourceType.Energy, 100f, 100f, true);
            SiegeObjectivePressureObservation outside =
                controller.ApplyObjectivePressure(++tick, snapshots, 1, 30);
            Assert.That(outside.WasHandled, Is.True);
            Assert.That(outside.PressureHostileCount, Is.Zero);
            Assert.That(outside.AppliedDamage, Is.Zero);

            snapshots[0] = Snapshot(spawn.Participant,
                HeroSiegeRunCatalog.HostileTeam, spawn.Archetype.Class,
                spawn.Archetype.Specialization, new Float3(2.4f, 0f, 0f),
                spawn.MaximumHealth, spawn.MaximumHealth,
                ResourceType.Energy, 100f, 100f, true,
                activeAuraTags: AuraTag.Stun);
            SiegeObjectivePressureObservation controlled =
                controller.ApplyObjectivePressure(++tick, snapshots, 1, 30);
            Assert.That(controlled.PressureHostileCount, Is.Zero,
                "Hard crowd control must pause pressure on the stronghold.");

            snapshots[0] = Snapshot(spawn.Participant,
                HeroSiegeRunCatalog.HostileTeam, spawn.Archetype.Class,
                spawn.Archetype.Specialization, new Float3(2.4f, 0f, 0f),
                spawn.MaximumHealth, spawn.MaximumHealth,
                ResourceType.Mana, 100f, 100f, true,
                castingAbility: AbilityId.Fireball);
            SiegeObjectivePressureObservation casting =
                controller.ApplyObjectivePressure(++tick, snapshots, 1, 30);
            Assert.That(casting.PressureHostileCount, Is.Zero,
                "An attacker cannot cast at a hero and damage the core simultaneously.");
        }

        [Test]
        public void ObjectiveDefeat_EndsRunDeterministically()
        {
            var controller = new HeroSiegeRunController(new RunId(5u));
            int tick = HeroSiegeRunCatalog.InitialPreparationTicks;
            controller.Advance(tick);
            controller.TryDequeueSpawn(out SiegeSpawnDefinition spawn);
            var snapshots = new[]
            {
                Snapshot(spawn.Participant, HeroSiegeRunCatalog.HostileTeam,
                    spawn.Archetype.Class, spawn.Archetype.Specialization,
                    new Float3(2.4f, 0f, 0f), spawn.MaximumHealth, spawn.MaximumHealth,
                    ResourceType.Energy, 100f, 100f, true),
            };

            SiegeObjectivePressureObservation result = default;
            while (!result.RunFailed)
            {
                result = controller.ApplyObjectivePressure(++tick, snapshots, 1, 1);
            }

            Assert.That(controller.State.Status, Is.EqualTo(SiegeRunStatus.Failed));
            Assert.That(controller.State.Objective.Health, Is.Zero);
            Assert.That(controller.Result.Reason,
                Is.EqualTo(SiegeRunEndReason.ObjectiveDefeated));
            Assert.That(controller.Result.TerminalTick, Is.EqualTo(tick));
        }

        [Test]
        public void PotionUse_RequiresOwnedCompatiblePotionAndMissingValue()
        {
            var manaController = new HeroSiegeRunController(new RunId(6u));
            manaController.TryPurchasePotion(SiegePotionId.Mana);
            CombatantSnapshot mage = Snapshot(manaController.GetDefender(0),
                HeroSiegeRunCatalog.DefenderTeam, ClassId.Mage,
                SpecializationId.Fire, default, 75f, 100f,
                ResourceType.Mana, 10f, 100f, true);
            SiegePotionUse mana = manaController.TryConsumePotion(
                SiegePotionId.Mana, mage);
            Assert.That(mana.WasHandled, Is.True);
            Assert.That(mana.ResourceType, Is.EqualTo(ResourceType.Mana));
            Assert.That(mana.RequestedRestore, Is.EqualTo(50f));

            var incompatible = new HeroSiegeRunController(new RunId(7u));
            incompatible.TryPurchasePotion(SiegePotionId.Mana);
            CombatantSnapshot rogue = Snapshot(incompatible.GetDefender(0),
                HeroSiegeRunCatalog.DefenderTeam, ClassId.Rogue,
                SpecializationId.Mutilate, default, 50f, 100f,
                ResourceType.Energy, 20f, 100f, true);
            Assert.That(incompatible.TryConsumePotion(SiegePotionId.Mana, rogue)
                .WasHandled, Is.False);
            Assert.That(incompatible.State.ManaPotionCount, Is.EqualTo(1));

            var healthController = new HeroSiegeRunController(new RunId(8u));
            healthController.TryPurchasePotion(SiegePotionId.Health);
            CombatantSnapshot full = Snapshot(healthController.GetDefender(0),
                HeroSiegeRunCatalog.DefenderTeam, ClassId.Rogue,
                SpecializationId.Mutilate, default, 100f, 100f,
                ResourceType.Energy, 100f, 100f, true);
            Assert.That(healthController.TryConsumePotion(SiegePotionId.Health, full)
                .WasHandled, Is.False);
            CombatantSnapshot injured = Snapshot(healthController.GetDefender(0),
                HeroSiegeRunCatalog.DefenderTeam, ClassId.Rogue,
                SpecializationId.Mutilate, default, 20f, 100f,
                ResourceType.Energy, 100f, 100f, true);
            Assert.That(healthController.TryConsumePotion(
                SiegePotionId.Health, injured).RequestedRestore, Is.EqualTo(40f));
        }

        [Test]
        public void FourLanes_LeavesDeadDefendersDownAndFailsOnlyOnFullWipe()
        {
            var controller = new HeroSiegeRunController(
                new RunId(9u), SiegeVariantId.FourLanes);
            int tick = HeroSiegeRunCatalog.InitialPreparationTicks;
            controller.Advance(tick);
            SiegeCombatObservation defenderDeath = controller.ObserveCombatEvent(
                CombatEvent.Create(++tick, CombatEventKind.Death,
                    new ParticipantId(100001), controller.GetDefender(1)));
            Assert.That(defenderDeath.WasHandled, Is.True);
            Assert.That(controller.State.LivingDefenderCount, Is.EqualTo(3));

            Assert.That(ClearActiveWave(controller, ref tick).WaveCleared, Is.True);
            Assert.That(controller.State.LivingDefenderCount, Is.EqualTo(3));

            var wipe = new HeroSiegeRunController(
                new RunId(10u), SiegeVariantId.FourLanes);
            tick = HeroSiegeRunCatalog.InitialPreparationTicks;
            wipe.Advance(tick);
            SiegeCombatObservation humanDeath = wipe.ObserveCombatEvent(
                CombatEvent.Create(++tick, CombatEventKind.Death,
                    new ParticipantId(100001), wipe.GetDefender(0)));

            Assert.That(humanDeath.RunFailed, Is.False);
            Assert.That(wipe.State.LivingDefenderCount, Is.EqualTo(3));
            for (int defender = 1; defender < 3; defender++)
            {
                SiegeCombatObservation death = wipe.ObserveCombatEvent(
                    CombatEvent.Create(++tick, CombatEventKind.Death,
                        new ParticipantId(100001),
                        wipe.GetDefender(defender)));
                Assert.That(death.RunFailed, Is.False);
            }

            SiegeCombatObservation last = wipe.ObserveCombatEvent(
                CombatEvent.Create(++tick, CombatEventKind.Death,
                    new ParticipantId(100001), wipe.GetDefender(3)));

            Assert.That(last.RunFailed, Is.True);
            Assert.That(wipe.Result.Reason,
                Is.EqualTo(SiegeRunEndReason.DefendersDefeated));
        }

        [Test]
        public void ClearingTwelveRegularWaves_CompletesWithVictory()
        {
            var controller = new HeroSiegeRunController(new RunId(11u));
            int tick = HeroSiegeRunCatalog.InitialPreparationTicks;
            for (int wave = 1; wave <= HeroSiegeRunCatalog.WaveCount; wave++)
            {
                tick = HeroSiegeRunCatalog.GetWaveStartTick(wave);
                SiegeAdvanceObservation start = controller.Advance(tick);
                Assert.That(start.Wave, Is.EqualTo(new SiegeWaveId((uint)wave)));
                Assert.That(ClearActiveWave(controller, ref tick).WaveCleared, Is.True);
            }

            Assert.That(controller.State.Status, Is.EqualTo(SiegeRunStatus.Completed));
            Assert.That(controller.Result.DidWin, Is.True);
            Assert.That(controller.Result.Reason,
                Is.EqualTo(SiegeRunEndReason.AllWavesCleared));
            Assert.That(controller.Result.ClearedWaveCount,
                Is.EqualTo(HeroSiegeRunCatalog.WaveCount));
        }

        [Test]
        public void WarmedReadsAndRejectedObservations_AllocateZeroManagedBytes()
        {
            var controller = new HeroSiegeRunController(new RunId(12u));
            controller.Advance(HeroSiegeRunCatalog.InitialPreparationTicks);
            controller.TryDequeueSpawn(out _);
            var snapshots = Array.Empty<CombatantSnapshot>();
            CombatEvent unknown = CombatEvent.Create(
                HeroSiegeRunCatalog.InitialPreparationTicks,
                CombatEventKind.Death, controller.GetDefender(0),
                new ParticipantId(999999));
            int checksum = 0;

            for (int index = 0; index < 256; index++)
            {
                checksum += ExerciseWarmPaths(controller, snapshots, unknown);
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 4096; index++)
            {
                checksum += ExerciseWarmPaths(controller, snapshots, unknown);
            }

            Assert.That(GC.GetAllocatedBytesForCurrentThread() - before, Is.Zero);
            Assert.That(checksum, Is.GreaterThan(0));
        }

        private static int ExerciseWarmPaths(
            HeroSiegeRunController controller,
            CombatantSnapshot[] snapshots,
            CombatEvent unknown)
        {
            int checksum = controller.State.ActiveHostileCount;
            checksum += controller.ObserveCombatEvent(unknown).WasHandled ? 1 : 0;
            checksum += controller.Advance(
                HeroSiegeRunCatalog.InitialPreparationTicks).WasHandled ? 1 : 0;
            checksum += controller.ApplyObjectivePressure(
                HeroSiegeRunCatalog.InitialPreparationTicks,
                snapshots, 0, 30).WasHandled ? 1 : 0;
            return checksum;
        }

        private static SiegeCombatObservation ClearActiveWave(
            HeroSiegeRunController controller,
            ref int tick)
        {
            SiegeCombatObservation latest = default;
            while (controller.State.Status == SiegeRunStatus.WaveActive)
            {
                while (controller.TryDequeueSpawn(out SiegeSpawnDefinition spawn))
                {
                    latest = controller.ObserveCombatEvent(Death(tick, spawn));
                    Assert.That(latest.WasHandled, Is.True);
                }

                if (controller.State.Status == SiegeRunStatus.WaveActive)
                {
                    tick += HeroSiegeRunCatalog.SpawnIntervalTicks;
                    Assert.That(controller.Advance(tick).WasHandled, Is.True);
                }
            }

            return latest;
        }

        private static CombatEvent Death(int tick, SiegeSpawnDefinition spawn) =>
            CombatEvent.Create(tick, CombatEventKind.Death,
                new ParticipantId(1), spawn.Participant);

        private static CombatantSnapshot Snapshot(
            ParticipantId id,
            TeamId team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            float health,
            float maxHealth,
            ResourceType resourceType,
            float resource,
            float maxResource,
            bool alive,
            AbilityId castingAbility = AbilityId.None,
            AuraTag activeAuraTags = AuraTag.None)
        {
            return new CombatantSnapshot(
                id, team, classId, specialization,
                position, new Float3(1f, 0f, 0f),
                health, maxHealth, 0f, castingAbility,
                0, 0, AbilityActivationType.Instant, SpellSchoolMask.None,
                resourceType, resource, maxResource,
                0, default, 0, SpellSchoolMask.None, alive, false,
                activeAuraTags: activeAuraTags);
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
