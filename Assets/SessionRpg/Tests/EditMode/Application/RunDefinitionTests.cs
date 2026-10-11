using System;
using System.Collections.Generic;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class RunDefinitionTests
    {
        private static readonly RunPartySpawnPositions TestPartySpawns =
            new RunPartySpawnPositions(
                new Float3(-4f, 0f, -3f),
                new Float3(-4f, 0f, -1f),
                new Float3(-4f, 0f, 1f),
                new Float3(-4f, 0f, 3f));

        [Test]
        public void PlayableModeIdentity_DoesNotRenumberLegacyGameModeIdentity()
        {
            Assert.That((int)GameModeId.Arena, Is.Zero);
            Assert.That((int)GameModeId.Boss, Is.EqualTo(1));
            Assert.That((int)GameModeId.Leveling, Is.EqualTo(2));
            Assert.That((int)GameModeId.HeroSiege, Is.EqualTo(3));
            Assert.That((int)GameModeId.Dungeon, Is.EqualTo(4));

            Assert.That((int)PlayableModeId.Arena, Is.EqualTo(1));
            Assert.That((int)PlayableModeId.HeroSiege, Is.EqualTo(2));
            Assert.That((int)PlayableModeId.Dungeon, Is.EqualTo(3));
            Assert.That((int)RunEnemyTier.SwarmMinion, Is.EqualTo(1));
            Assert.That((int)RunEnemyTier.Medium, Is.EqualTo(2));
            Assert.That((int)RunEnemyTier.Boss, Is.EqualTo(3));

            LegacyGameModeMigration boss =
                LegacyGameModeMigrationAdapter.Migrate(GameModeId.Boss);
            Assert.That(boss.PlayableMode, Is.EqualTo(PlayableModeId.Dungeon));
            Assert.That(boss.EncounterKind, Is.EqualTo(EncounterKind.Boss));
            LegacyGameModeMigration leveling =
                LegacyGameModeMigrationAdapter.Migrate(GameModeId.Leveling);
            Assert.That(leveling.IsTraining, Is.True);
            Assert.That(leveling.PlayableMode, Is.EqualTo(PlayableModeId.None));
        }

        [Test]
        public void HeroSiegeVariants_PreserveCombatantCapacityWithoutEncounters()
        {
            SiegeVariantDefinition solo = HeroSiegeRunCatalog.GetVariant(
                SiegeVariantId.SoloLane);
            SiegeVariantDefinition four = HeroSiegeRunCatalog.GetVariant(
                SiegeVariantId.FourLanes);

            Assert.That(HeroSiegeRunCatalog.DefaultVariant,
                Is.EqualTo(SiegeVariantId.SoloLane));
            Assert.That(solo.Party.Count, Is.EqualTo(1));
            Assert.That(solo.LaneCount, Is.EqualTo(1));
            Assert.That(solo.MaximumActiveHostilesPerLane, Is.EqualTo(8));
            Assert.That(solo.MaximumCombatantCount,
                Is.EqualTo(CombatSimulation.MaxCombatants - 3));
            Assert.That(four.Party.Count, Is.EqualTo(4));
            Assert.That(four.LaneCount, Is.EqualTo(4));
            Assert.That(four.MaximumActiveHostilesPerLane, Is.EqualTo(2));
            Assert.That(four.MaximumCombatantCount,
                Is.EqualTo(CombatSimulation.MaxCombatants));

            for (int laneIndex = 0; laneIndex < four.LaneCount; laneIndex++)
            {
                SiegeLaneId lane = four.GetLane(laneIndex);
                Assert.That(four.GetDefender(lane),
                    Is.EqualTo(four.Party.GetMember(laneIndex)));
                Assert.That(four.GetLaneSpawnPosition(lane).PlanarLength,
                    Is.EqualTo(11.5f).Within(0.001f));
                Assert.That(four.GetDefenderAnchor(lane).PlanarLength,
                    Is.EqualTo(4.25f).Within(0.001f));
            }
        }

        [Test]
        public void DungeonDefinition_IsFourHeroesFourBoundedContentFloors()
        {
            RunDefinition definition = DungeonRunCatalog.Definition;
            int[] expectedSpawnCounts = { 3, 4, 4, 4 };
            DungeonFloorKind[] expectedFloorKinds =
            {
                DungeonFloorKind.TraversalPack,
                DungeonFloorKind.EliteArena,
                DungeonFloorKind.MechanicsEncounter,
                DungeonFloorKind.RiftWardenBoss,
            };

            Assert.That(definition.Mode, Is.EqualTo(PlayableModeId.Dungeon));
            Assert.That(definition.Party.Count, Is.EqualTo(4));
            Assert.That(definition.EncounterCount, Is.EqualTo(4));
            Assert.That(definition.HasObjective, Is.False);
            for (int floorIndex = 0;
                floorIndex < definition.EncounterCount;
                floorIndex++)
            {
                EncounterDefinition floor = definition.GetEncounter(floorIndex);
                Assert.That(
                    floor.Rules.PlayableMode,
                    Is.EqualTo(PlayableModeId.Dungeon));
                Assert.That(
                    floor.SpawnCount,
                    Is.EqualTo(expectedSpawnCounts[floorIndex]));
                Assert.That(
                    floor.SpawnCount,
                    Is.LessThanOrEqualTo(
                        DungeonRunCatalog.MaximumActiveHostileCount));
                Assert.That(
                    definition.Party.Count + floor.SpawnCount,
                    Is.LessThanOrEqualTo(CombatSimulation.MaxCombatants));
                Assert.That(
                    DungeonRunCatalog.GetFloorKind(floorIndex),
                    Is.EqualTo(expectedFloorKinds[floorIndex]));
                Assert.That(floor.Rules.TickRate, Is.EqualTo(30));
                Assert.That(
                    floor.Rules.CollisionMap.IsEnabled,
                    Is.EqualTo(floorIndex < 3));
                Assert.That(
                    floor.Rules.RadialHazardsEnabled,
                    Is.EqualTo(floorIndex > 0));
                Assert.That(
                    floor.GetPartySpawnPosition(0),
                    Is.EqualTo(
                        floorIndex == 0
                            ? new Float3(
                                -178f,
                                0f,
                                ArenaCollisionMap.LevelingCenterZ - 2.625f)
                            : new Float3(-5.5f, 0f, -2.625f)));

                for (int spawnIndex = 0;
                    spawnIndex < floor.SpawnCount;
                    spawnIndex++)
                {
                    RunSpawnDefinition spawn = floor.GetSpawn(spawnIndex);
                    Assert.That(spawn.Team, Is.EqualTo(new TeamId(2)));
                    Assert.That(spawn.Tier, Is.Not.EqualTo(RunEnemyTier.None));
                    Assert.That(
                        SpecializationCatalog.IsCompatible(
                            spawn.Class,
                            spawn.Specialization),
                        Is.True);
                    Assert.That(
                        spawn.Class,
                        Is.EqualTo(ExpectedEnemyClass(spawn.Rank, spawnIndex)));
                    Assert.That(
                        spawn.Specialization,
                        Is.EqualTo(
                            ExpectedEnemySpecialization(
                                spawn.Rank,
                                spawnIndex)));
                    Assert.That(
                        spawn.MaximumHealth,
                        Is.EqualTo(ExpectedEnemyHealth(spawn.Rank)));
                    Assert.That(
                        spawn.Position,
                        Is.EqualTo(
                            floorIndex == 0
                                ? ExpectedTraversalHostilePosition(spawnIndex)
                                : ExpectedArenaHostilePosition(spawnIndex)));
                }
            }

            Assert.That(
                definition.GetEncounter(0).Challenge,
                Is.EqualTo(RunEncounterChallenge.Standard));
            Assert.That(
                definition.GetEncounter(1).Challenge,
                Is.EqualTo(RunEncounterChallenge.Standard));
            Assert.That(
                definition.GetEncounter(2).Challenge,
                Is.EqualTo(RunEncounterChallenge.Miniboss));
            Assert.That(
                definition.GetEncounter(3).Challenge,
                Is.EqualTo(RunEncounterChallenge.Boss));
            EncounterDefinition bossFloor = definition.GetEncounter(3);
            RunSpawnDefinition riftWarden =
                bossFloor.GetSpawn(bossFloor.SpawnCount - 1);
            Assert.That(
                riftWarden.Id,
                Is.EqualTo(DungeonRunCatalog.RiftWardenSpawn));
            Assert.That(riftWarden.Rank, Is.EqualTo(RunEnemyRank.Boss));
            Assert.That(riftWarden.Tier, Is.EqualTo(RunEnemyTier.Boss));
            Assert.That(riftWarden.Class, Is.EqualTo(ClassId.Boss));
            Assert.That(
                riftWarden.Specialization,
                Is.EqualTo(SpecializationId.Boss));
            Assert.That(riftWarden.MaximumHealth, Is.EqualTo(520f));
        }

        [TestCase(ClassId.Healer, SpecializationId.Discipline)]
        [TestCase(ClassId.Healer, SpecializationId.Shadow)]
        [TestCase(ClassId.Rogue, SpecializationId.ShadowDance)]
        [TestCase(ClassId.Rogue, SpecializationId.Mutilate)]
        [TestCase(ClassId.Mage, SpecializationId.Fire)]
        [TestCase(ClassId.Mage, SpecializationId.Frost)]
        [TestCase(ClassId.Hunter, SpecializationId.Marksmanship)]
        public void PartyCompositionResolver_PreservesHumanSelectionAndFillsOtherClasses(
            ClassId selectedClass,
            SpecializationId selectedSpecialization)
        {
            RunPartyDefinition party = HeroSiegeRunCatalog.GetVariant(
                SiegeVariantId.FourLanes).Party;
            RunPartyComposition composition =
                RunPartyCompositionResolver.Resolve(
                    party,
                    selectedClass,
                    selectedSpecialization);
            var classes = new HashSet<ClassId>();

            Assert.That(composition.IsValid, Is.True);
            Assert.That(composition.Count, Is.EqualTo(4));
            for (int index = 0; index < composition.Count; index++)
            {
                RunPartyMemberDefinition member = composition.GetMember(index);
                Assert.That(member.Participant, Is.EqualTo(party.GetMember(index)));
                Assert.That(member.Team, Is.EqualTo(new TeamId(1)));
                Assert.That(
                    member.MaximumHealth,
                    Is.EqualTo(
                        RunPartyCompositionResolver.PartyMemberMaximumHealth));
                Assert.That(member.ApplyClassOpeningState, Is.True);
                Assert.That(member.IsHumanControlled, Is.EqualTo(index == 0));
                Assert.That(classes.Add(member.Class), Is.True);
                Assert.That(
                    SpecializationCatalog.IsCompatible(
                        member.Class,
                        member.Specialization),
                    Is.True);
            }

            Assert.That(
                composition.GetMember(0).Class,
                Is.EqualTo(selectedClass));
            Assert.That(
                composition.GetMember(0).Specialization,
                Is.EqualTo(selectedSpecialization));
            Assert.That(classes.Contains(ClassId.Healer), Is.True);
            Assert.That(classes.Contains(ClassId.Rogue), Is.True);
            Assert.That(classes.Contains(ClassId.Mage), Is.True);
            Assert.That(classes.Contains(ClassId.Hunter), Is.True);
        }

        [Test]
        public void PartyCompositionResolver_SoloSiegeKeepsOnlyTheHumanDefender()
        {
            RunPartyDefinition party = HeroSiegeRunCatalog.GetVariant(
                SiegeVariantId.SoloLane).Party;
            RunPartyComposition composition =
                RunPartyCompositionResolver.Resolve(
                    party,
                    ClassId.Hunter,
                    SpecializationId.Marksmanship);

            Assert.That(composition.IsValid, Is.True);
            Assert.That(composition.Count, Is.EqualTo(1));
            Assert.That(composition.GetMember(0).Participant,
                Is.EqualTo(new ParticipantId(1)));
            Assert.That(composition.GetMember(0).Class,
                Is.EqualTo(ClassId.Hunter));
            Assert.That(composition.GetMember(0).Specialization,
                Is.EqualTo(SpecializationId.Marksmanship));
            Assert.That(composition.GetMember(0).IsHumanControlled, Is.True);
            Assert.Throws<ArgumentOutOfRangeException>(
                () => composition.GetMember(1));
        }

        [Test]
        public void PartyCompositionResolver_AfterWarmupAllocatesZeroManagedBytes()
        {
            RunPartyDefinition party = HeroSiegeRunCatalog.GetVariant(
                SiegeVariantId.FourLanes).Party;
            int checksum = 0;
            for (int index = 0; index < 256; index++)
            {
                checksum += RunPartyCompositionResolver.Resolve(
                    party,
                    ClassId.Mage,
                    SpecializationId.Fire).GetMember(3).Participant.Value;
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 4096; index++)
            {
                checksum += RunPartyCompositionResolver.Resolve(
                    party,
                    ClassId.Mage,
                    SpecializationId.Fire).GetMember(3).Participant.Value;
            }

            long allocatedBytes =
                GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero);
            Assert.That(checksum, Is.GreaterThan(0));
        }

        [Test]
        public void FixedDefinitions_RejectGapsDuplicateIdsAndRankMismatch()
        {
            RunSpawnDefinition spawn0 = Spawn(1u, 10, RunEnemyRank.Standard);
            RunSpawnDefinition spawn1 = Spawn(2u, 11, RunEnemyRank.Standard);

            Assert.Throws<ArgumentException>(
                () => new RunPartyDefinition(
                    new ParticipantId(1),
                    default,
                    new ParticipantId(3)));
            Assert.Throws<ArgumentException>(
                () => new RunPartyDefinition(
                    new ParticipantId(1),
                    new ParticipantId(1)));
            Assert.Throws<ArgumentException>(
                () => new EncounterDefinition(
                    new EncounterId(1u),
                    RunEncounterChallenge.Standard,
                    CombatRules.BossDefault,
                    TestPartySpawns,
                    spawn0,
                    default,
                    spawn1));
            Assert.Throws<ArgumentException>(
                () => new EncounterDefinition(
                    new EncounterId(1u),
                    RunEncounterChallenge.Standard,
                    CombatRules.BossDefault,
                    TestPartySpawns,
                    spawn0,
                    Spawn(1u, 11, RunEnemyRank.Standard)));
            Assert.Throws<ArgumentException>(
                () => new EncounterDefinition(
                    new EncounterId(1u),
                    RunEncounterChallenge.Miniboss,
                    CombatRules.BossDefault,
                    TestPartySpawns,
                    spawn0));
        }

        [Test]
        public void EncounterCapacity_LeavesCoreCombatantCapAuthoritative()
        {
            Assert.That(CombatSimulation.MaxCombatants, Is.EqualTo(12));
            Assert.That(RunPartyDefinition.MaximumMemberCount, Is.EqualTo(4));
            Assert.That(EncounterDefinition.MaximumSpawnCount, Is.EqualTo(4));
            Assert.That(
                RunPartyDefinition.MaximumMemberCount +
                    EncounterDefinition.MaximumSpawnCount,
                Is.EqualTo(8));
            Assert.That(
                RunPartyDefinition.MaximumMemberCount +
                    EncounterDefinition.MaximumSpawnCount,
                Is.LessThanOrEqualTo(CombatSimulation.MaxCombatants));
        }

        [Test]
        public void EncounterDefinition_RejectsAFifthSimultaneousHostile()
        {
            ArgumentException exception = Assert.Throws<ArgumentException>(
                () => new EncounterDefinition(
                    new EncounterId(1u),
                    RunEncounterChallenge.Standard,
                    CombatRules.BossDefault,
                    TestPartySpawns,
                    Spawn(1u, 101, RunEnemyRank.Standard),
                    Spawn(2u, 102, RunEnemyRank.Standard),
                    Spawn(3u, 103, RunEnemyRank.Standard),
                    Spawn(4u, 104, RunEnemyRank.Standard),
                    Spawn(5u, 105, RunEnemyRank.Standard)));

            StringAssert.Contains(
                "more than four simultaneous hostiles",
                exception.Message);
        }

        [Test]
        public void EncounterDefinition_RejectsInvalidRulesTopologyAndTierMetadata()
        {
            Assert.Throws<ArgumentException>(
                () => new EncounterDefinition(
                    new EncounterId(1u),
                    RunEncounterChallenge.Standard,
                    new CombatRules(60, 5.4f, 13f, 19f, 28f),
                    TestPartySpawns,
                    Spawn(1u, 101, RunEnemyRank.Standard)));
            Assert.Throws<ArgumentException>(
                () => new EncounterDefinition(
                    new EncounterId(1u),
                    RunEncounterChallenge.Standard,
                    CombatRules.BossDefault,
                    default,
                    Spawn(1u, 101, RunEnemyRank.Standard)));
            Assert.Throws<ArgumentException>(
                () => new EncounterDefinition(
                    new EncounterId(1u),
                    RunEncounterChallenge.Standard,
                    CombatRules.DungeonTraversalDefault,
                    new RunPartySpawnPositions(
                        new Float3(999f, 0f, 0f),
                        new Float3(-4f, 0f, -1f),
                        new Float3(-4f, 0f, 1f),
                        new Float3(-4f, 0f, 3f)),
                    Spawn(1u, 101, RunEnemyRank.Standard)));
            Assert.Throws<ArgumentException>(
                () => new RunSpawnDefinition(
                    new SpawnId(1u),
                    new ParticipantId(101),
                    RunEnemyRank.Standard,
                    RunEnemyTier.Boss,
                    new TeamId(2),
                    ClassId.Rogue,
                    SpecializationId.ShadowDance,
                    105f,
                    new Float3(4f, 0f, 0f)));
        }

        private static Float3 ExpectedArenaHostilePosition(int index)
        {
            float lateral = (index - 1.5f) * 2.2f;
            return new Float3(
                4.5f + ((index % 2) * 1.7f),
                0f,
                lateral);
        }

        private static Float3 ExpectedTraversalHostilePosition(int index)
        {
            float lateral = (index - 1.5f) * 2.2f;
            return new Float3(
                -160f + ((index % 2) * 1.5f),
                0f,
                ArenaCollisionMap.LevelingCenterZ + lateral);
        }

        private static ClassId ExpectedEnemyClass(
            RunEnemyRank rank,
            int spawnIndex)
        {
            if (rank == RunEnemyRank.Miniboss || rank == RunEnemyRank.Boss)
            {
                return ClassId.Boss;
            }

            if (rank == RunEnemyRank.Elite)
            {
                return ClassId.Mage;
            }

            switch (spawnIndex % 3)
            {
                case 0:
                    return ClassId.Rogue;
                case 1:
                    return ClassId.Mage;
                default:
                    return ClassId.Hunter;
            }
        }

        private static SpecializationId ExpectedEnemySpecialization(
            RunEnemyRank rank,
            int spawnIndex)
        {
            if (rank == RunEnemyRank.Miniboss || rank == RunEnemyRank.Boss)
            {
                return SpecializationId.Boss;
            }

            if (rank == RunEnemyRank.Elite)
            {
                return SpecializationId.Frost;
            }

            return SpecializationCatalog.GetDefault(
                ExpectedEnemyClass(rank, spawnIndex));
        }

        private static float ExpectedEnemyHealth(
            RunEnemyRank rank,
            RunEnemyTier tier = RunEnemyTier.Medium)
        {
            switch (rank)
            {
                case RunEnemyRank.Elite:
                    return 175f;
                case RunEnemyRank.Miniboss:
                    return 300f;
                case RunEnemyRank.Boss:
                    return 520f;
                default:
                    return tier == RunEnemyTier.SwarmMinion ? 55f : 105f;
            }
        }

        private static RunSpawnDefinition Spawn(
            uint id,
            int participant,
            RunEnemyRank rank)
        {
            return new RunSpawnDefinition(
                new SpawnId(id),
                new ParticipantId(participant),
                rank,
                rank == RunEnemyRank.Miniboss || rank == RunEnemyRank.Boss
                    ? RunEnemyTier.Boss
                    : RunEnemyTier.Medium,
                new TeamId(2),
                rank == RunEnemyRank.Miniboss || rank == RunEnemyRank.Boss
                    ? ClassId.Boss
                    : ClassId.Rogue,
                rank == RunEnemyRank.Miniboss || rank == RunEnemyRank.Boss
                    ? SpecializationId.Boss
                    : SpecializationId.ShadowDance,
                rank == RunEnemyRank.Boss ? 520f : 105f,
                new Float3(4f, 0f, 0f));
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
