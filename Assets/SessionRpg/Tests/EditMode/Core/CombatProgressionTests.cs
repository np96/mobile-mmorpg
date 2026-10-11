using System;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class CombatProgressionTests
    {
        private static readonly ParticipantId Player = new ParticipantId(1);
        private static readonly ParticipantId Enemy = new ParticipantId(2);

        [Test]
        public void CombatantSnapshots_DefaultToLevelOne()
        {
            CombatantSnapshot defaultSnapshot = default;
            CombatantSnapshot compatibilitySnapshot = new CombatantSnapshot(
                Player,
                new TeamId(1),
                ClassId.Mage,
                default,
                new Float3(0f, 0f, 1f),
                100f,
                100f,
                0f,
                AbilityId.None,
                0,
                true,
                false);
            CombatantSnapshot fullSnapshot = new CombatantSnapshot(
                id: Player,
                team: new TeamId(1),
                classId: ClassId.Mage,
                specialization: SpecializationId.Fire,
                position: default,
                facing: new Float3(0f, 0f, 1f),
                health: 100f,
                maxHealth: 100f,
                barrier: 0f,
                castingAbility: AbilityId.None,
                castStartTick: 0,
                castEndTick: 0,
                castingActivationType: AbilityActivationType.Instant,
                castingSchool: SpellSchoolMask.None,
                resourceType: ResourceType.Mana,
                resource: 100f,
                maxResource: 100f,
                comboPoints: 0,
                comboTarget: default,
                globalCooldownEndTick: 0,
                lockedSchools: SpellSchoolMask.None,
                alive: true,
                inLava: false);

            Assert.That(defaultSnapshot.Level, Is.EqualTo(1));
            Assert.That(compatibilitySnapshot.Level, Is.EqualTo(1));
            Assert.That(fullSnapshot.Level, Is.EqualTo(1));
        }

        [Test]
        public void AddedCombatant_StartsAtLevelOne()
        {
            CombatSimulation simulation = CreateDuel();

            CombatantSnapshot player = Snapshot(simulation, Player);

            Assert.That(player.Level, Is.EqualTo(1));
            Assert.That(player.MaxHealth, Is.EqualTo(100f));
        }

        [Test]
        public void OpenWorldNpcSpawn_CanSuppressPlayerClassOpeningState()
        {
            CombatSimulation simulation = new CombatSimulation(
                CombatRules.LevelingDefault);
            ParticipantId rogueNpc = new ParticipantId(11);

            Assert.That(
                simulation.AddCombatant(
                    rogueNpc,
                    new TeamId(2),
                    ClassId.Rogue,
                    SpecializationId.Mutilate,
                    default,
                    52f,
                    false),
                Is.True);

            Assert.That(simulation.HasAura(rogueNpc, AuraId.Stealth), Is.False);
            Assert.That(Snapshot(simulation, rogueNpc).Level, Is.EqualTo(1));
        }

        [Test]
        public void ApplyProgressionLevel_OnlyMovesForwardAndAddsHealthPerGainedLevel()
        {
            CombatSimulation simulation = CreateDuel();

            Assert.That(
                simulation.ApplyProgressionLevel(new ParticipantId(99), 2),
                Is.False);
            Assert.That(simulation.ApplyProgressionLevel(Player, 0), Is.False);
            Assert.That(simulation.ApplyProgressionLevel(Player, 1), Is.False);
            Assert.That(simulation.ApplyProgressionLevel(Player, 3), Is.True);
            AssertLevelAndHealth(simulation, 3, 116f);

            Assert.That(simulation.ApplyProgressionLevel(Player, 2), Is.False);
            AssertLevelAndHealth(simulation, 3, 116f);

            Assert.That(simulation.ApplyProgressionLevel(Player, int.MaxValue), Is.True);
            AssertLevelAndHealth(simulation, 6, 140f);
            Assert.That(simulation.ApplyProgressionLevel(Player, 7), Is.False);
            AssertLevelAndHealth(simulation, 6, 140f);
        }

        [Test]
        public void ApplyProgressionLevel_FullyRestoresHealthAndAuthoredResource()
        {
            CombatSimulation simulation = CreateDuel();
            Assert.That(
                simulation.SubmitCommand(TargetCast(Player, 1, AbilityId.FireBlast, Enemy)),
                Is.True);
            Assert.That(
                simulation.SubmitCommand(TargetCast(Enemy, 1, AbilityId.FireBlast, Player)),
                Is.True);
            simulation.Tick();
            CombatantSnapshot spentAndDamaged = Snapshot(simulation, Player);
            Assert.That(spentAndDamaged.Health, Is.LessThan(spentAndDamaged.MaxHealth));
            Assert.That(spentAndDamaged.Resource, Is.LessThan(spentAndDamaged.MaxResource));

            Assert.That(simulation.ApplyProgressionLevel(Player, 2), Is.True);

            CombatantSnapshot restored = Snapshot(simulation, Player);
            Assert.That(restored.Level, Is.EqualTo(2));
            Assert.That(restored.MaxHealth, Is.EqualTo(108f));
            Assert.That(restored.Health, Is.EqualTo(restored.MaxHealth));
            Assert.That(restored.Resource, Is.EqualTo(restored.MaxResource));
        }

        [Test]
        public void ProgressionLevel_AddsSixPercentDamagePerGainedLevel()
        {
            float levelOneDamage = ResolveFireBlastDamage(1);
            float levelTwoDamage = ResolveFireBlastDamage(2);
            float levelSixDamage = ResolveFireBlastDamage(6);

            Assert.That(levelOneDamage, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(levelTwoDamage, Is.EqualTo(levelOneDamage * 1.06f).Within(0.0001f));
            Assert.That(levelSixDamage, Is.EqualTo(levelOneDamage * 1.30f).Within(0.0001f));
        }

        [Test]
        public void LevelingAbilityCatalog_ExposesThreeInitialAndFourPackUnlocks()
        {
            SpecializationId[] specializations =
            {
                SpecializationId.Discipline,
                SpecializationId.Shadow,
                SpecializationId.Fire,
                SpecializationId.Frost,
                SpecializationId.ShadowDance,
                SpecializationId.Mutilate,
                SpecializationId.Marksmanship,
            };

            for (int specializationIndex = 0;
                 specializationIndex < specializations.Length;
                 specializationIndex++)
            {
                SpecializationId specialization =
                    specializations[specializationIndex];
                AbilityKit fullKit = SpecializationCatalog.GetKit(specialization);
                AbilityKit levelingKit = LevelingAbilityUnlockCatalog.GetKit(
                    specialization);
                var seen = new AbilityId[
                    LevelingAbilityUnlockCatalog.TotalAbilityCount];
                Assert.That(levelingKit.Count, Is.EqualTo(7));
                Assert.That(levelingKit.AbilityCount, Is.EqualTo(7));
                for (int slot = 0;
                     slot < LevelingAbilityUnlockCatalog.TotalAbilityCount;
                     slot++)
                {
                    AbilityId ability = LevelingAbilityUnlockCatalog.GetAbility(
                        specialization,
                        slot);
                    int expectedLevel = slot <
                        LevelingAbilityUnlockCatalog.InitialAbilityCount
                            ? 1
                            : slot - 1;
                    Assert.That(ability, Is.EqualTo(levelingKit.GetAbility(slot)));
                    AbilityDefinition definition = AbilityCatalog.Get(ability);
                    Assert.That(
                        definition.OwnerClass == ClassId.None ||
                        definition.OwnerClass ==
                            SpecializationCatalog.GetDefinition(specialization).OwnerClass,
                        Is.True,
                        specialization + " Training owns incompatible " + ability + ".");
                    for (int priorSlot = 0; priorSlot < slot; priorSlot++)
                    {
                        Assert.That(
                            ability,
                            Is.Not.EqualTo(seen[priorSlot]),
                            specialization + " duplicates " + ability + " in slots " +
                            priorSlot + " and " + slot + ".");
                    }

                    seen[slot] = ability;
                    Assert.That(
                        levelingKit.GetAbility(slot, AbilitySlotDirection.Up),
                        Is.EqualTo(AbilityId.None));
                    Assert.That(
                        levelingKit.GetAbility(slot, AbilitySlotDirection.Down),
                        Is.EqualTo(AbilityId.None));
                    Assert.That(
                        levelingKit.GetAbility(slot, AbilitySlotDirection.Left),
                        Is.EqualTo(AbilityId.None));
                    Assert.That(
                        levelingKit.GetAbility(slot, AbilitySlotDirection.Right),
                        Is.EqualTo(AbilityId.None));
                    Assert.That(
                        LevelingAbilityUnlockCatalog.TryGetRequiredLevel(
                            specialization,
                            ability,
                            out int requiredLevel),
                        Is.True,
                        specialization + " slot " + slot);
                    Assert.That(requiredLevel, Is.EqualTo(expectedLevel));
                    Assert.That(
                        LevelingAbilityUnlockCatalog.IsUnlocked(
                            specialization,
                            ability,
                            expectedLevel),
                        Is.True);
                    if (expectedLevel > 1)
                    {
                        Assert.That(
                            LevelingAbilityUnlockCatalog.IsUnlocked(
                                specialization,
                                ability,
                                expectedLevel - 1),
                            Is.False);
                    }
                }

                Assert.That(
                    LevelingAbilityUnlockCatalog.TryGetRequiredLevel(
                        specialization,
                        AbilityId.ArenaTrinket,
                        out _),
                    Is.False,
                    specialization + " must not expose the full arena kit.");
                AbilityId arenaOnlyAlternate = AbilityId.None;
                AbilitySlotDirection[] directions =
                {
                    AbilitySlotDirection.Up,
                    AbilitySlotDirection.Down,
                    AbilitySlotDirection.Left,
                    AbilitySlotDirection.Right,
                };
                for (int slot = 0;
                     slot < AbilityKit.Capacity && arenaOnlyAlternate == AbilityId.None;
                     slot++)
                {
                    for (int directionIndex = 0;
                         directionIndex < directions.Length;
                         directionIndex++)
                    {
                        AbilityId candidate = fullKit.GetAbility(
                            slot,
                            directions[directionIndex]);
                        if (candidate != AbilityId.None && !levelingKit.Contains(candidate))
                        {
                            arenaOnlyAlternate = candidate;
                            break;
                        }
                    }
                }

                Assert.That(arenaOnlyAlternate, Is.Not.EqualTo(AbilityId.None));
                Assert.That(
                    LevelingAbilityUnlockCatalog.TryGetRequiredLevel(
                        specialization,
                        arenaOnlyAlternate,
                        out _),
                    Is.False,
                    specialization + " must not expose Arena-only swipe alternates.");
            }
        }

        [Test]
        public void LevelingAbilityCatalog_UsesTheAuthoredLevelOneSurvivalKits()
        {
            AssertLevelingKit(
                SpecializationId.Discipline,
                AbilityId.Penance,
                AbilityId.Smite,
                AbilityId.ShadowWordPain,
                AbilityId.PowerWordShield,
                AbilityId.FlashHeal,
                AbilityId.PsychicScream,
                AbilityId.PowerInfusion);
            AssertLevelingKit(
                SpecializationId.Shadow,
                AbilityId.ShadowWordPain,
                AbilityId.MindFlay,
                AbilityId.FlashHeal,
                AbilityId.Shadowform,
                AbilityId.PsychicScream,
                AbilityId.MindBlast,
                AbilityId.Dispersion);
            AssertLevelingKit(
                SpecializationId.Fire,
                AbilityId.Fireball,
                AbilityId.FireBlast,
                AbilityId.Polymorph,
                AbilityId.FrostNova,
                AbilityId.Blink,
                AbilityId.DragonBreath,
                AbilityId.Combustion);
            AssertLevelingKit(
                SpecializationId.ShadowDance,
                AbilityId.Hemorrhage,
                AbilityId.Eviscerate,
                AbilityId.Stealth,
                AbilityId.Sap,
                AbilityId.Shadowstep,
                AbilityId.KidneyShot,
                AbilityId.ShadowDance);
            AssertLevelingKit(
                SpecializationId.Mutilate,
                AbilityId.Mutilate,
                AbilityId.Envenom,
                AbilityId.Stealth,
                AbilityId.Sap,
                AbilityId.Vanish,
                AbilityId.KidneyShot,
                AbilityId.ColdBlood);
            AssertLevelingKit(
                SpecializationId.Marksmanship,
                AbilityId.SteadyShot,
                AbilityId.ArcaneShot,
                AbilityId.FreezingArrow,
                AbilityId.PetAttack,
                AbilityId.Disengage,
                AbilityId.SerpentSting,
                AbilityId.RapidFire);
        }

        [Test]
        public void LevelingRules_EnforcePlayerUnlocksWithoutRestrictingNpcOrArenaKits()
        {
            var teamOne = new TeamId(1);
            var teamTwo = new TeamId(2);
            var leveling = new CombatSimulation(CombatRules.LevelingDefault);
            Assert.That(
                leveling.AddCombatant(
                    Player,
                    teamOne,
                    ClassId.Mage,
                    SpecializationId.Fire,
                    new Float3(-180f, 0f, ArenaCollisionMap.LevelingCenterZ),
                    100f),
                Is.True);
            Assert.That(
                leveling.AddCombatant(
                    Enemy,
                    teamTwo,
                    ClassId.Mage,
                    SpecializationId.Fire,
                    new Float3(-170f, 0f, ArenaCollisionMap.LevelingCenterZ),
                    100f),
                Is.True);

            Assert.That(
                leveling.CanUseAbility(Player, AbilityId.FireBlast, out _),
                Is.True);
            Assert.That(
                leveling.CanUseAbility(
                    Player,
                    AbilityId.DragonBreath,
                    out string levelThreeReason),
                Is.False);
            Assert.That(levelThreeReason, Is.EqualTo("level_locked"));
            Assert.That(
                leveling.CanUseAbility(
                    Player,
                    AbilityId.Combustion,
                    out string levelFiveReason),
                Is.False);
            Assert.That(levelFiveReason, Is.EqualTo("level_locked"));
            Assert.That(
                leveling.CanUseAbility(
                    Player,
                    AbilityId.Scorch,
                    out string fullKitReason),
                Is.False);
            Assert.That(fullKitReason, Is.EqualTo("level_locked"));

            Assert.That(leveling.ApplyProgressionLevel(Player, 3), Is.True);
            Assert.That(
                leveling.CanUseAbility(Player, AbilityId.Blink, out _),
                Is.True);
            Assert.That(
                leveling.CanUseAbility(Player, AbilityId.DragonBreath, out levelThreeReason),
                Is.False);
            Assert.That(levelThreeReason, Is.EqualTo("level_locked"));
            Assert.That(
                leveling.CanUseAbility(Player, AbilityId.Combustion, out levelFiveReason),
                Is.False);
            Assert.That(levelFiveReason, Is.EqualTo("level_locked"));

            Assert.That(leveling.ApplyProgressionLevel(Player, 4), Is.True);
            Assert.That(
                leveling.CanUseAbility(Player, AbilityId.DragonBreath, out _),
                Is.True);

            Assert.That(leveling.ApplyProgressionLevel(Player, 5), Is.True);
            Assert.That(
                leveling.CanUseAbility(Player, AbilityId.Combustion, out _),
                Is.True);
            Assert.That(
                leveling.CanUseAbility(Player, AbilityId.Scorch, out fullKitReason),
                Is.False);
            Assert.That(fullKitReason, Is.EqualTo("level_locked"));

            Assert.That(
                leveling.CanUseAbility(Enemy, AbilityId.Scorch, out _),
                Is.True,
                "Enemy archetype kits are authored independently of player progression.");

            CombatSimulation arena = CreateDuel();
            Assert.That(
                arena.CanUseAbility(Player, AbilityId.Scorch, out _),
                Is.True,
                "Arena must retain the complete specialization kit at level one.");
        }

        [TestCase(SpecializationId.ShadowDance)]
        [TestCase(SpecializationId.Mutilate)]
        public void TrainingAuthorizesSapWithoutAddingItBackToArena(
            SpecializationId specialization)
        {
            var training = new CombatSimulation(CombatRules.LevelingDefault);
            Assert.That(
                training.AddCombatant(
                    Player,
                    new TeamId(1),
                    ClassId.Rogue,
                    specialization,
                    new Float3(-180f, 0f, ArenaCollisionMap.LevelingCenterZ),
                    100f),
                Is.True);

            Assert.That(training.ApplyProgressionLevel(Player, 2), Is.True);
            Assert.That(
                training.CanUseAbility(Player, AbilityId.Sap, out string trainingReason),
                Is.True,
                trainingReason);

            var arena = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(
                arena.AddCombatant(
                    Player,
                    new TeamId(1),
                    ClassId.Rogue,
                    specialization,
                    default,
                    100f),
                Is.True);
            Assert.That(
                arena.CanUseAbility(Player, AbilityId.Sap, out string arenaReason),
                Is.False);
            Assert.That(arenaReason, Is.EqualTo("wrong_specialization"));
        }

        [Test]
        public void TrainingAuthorizesShadowFlashHealWithoutAddingItBackToArena()
        {
            var training = new CombatSimulation(CombatRules.LevelingDefault);
            Assert.That(
                training.AddCombatant(
                    Player,
                    new TeamId(1),
                    ClassId.Priest,
                    SpecializationId.Shadow,
                    new Float3(-180f, 0f, ArenaCollisionMap.LevelingCenterZ),
                    100f),
                Is.True);
            Assert.That(training.ApplyProgressionLevel(Player, 2), Is.True);
            Assert.That(
                training.SubmitCommand(
                    TargetCast(Player, 1, AbilityId.Shadowform, Player)),
                Is.True);
            training.Tick();
            while (training.GetGlobalCooldownRemaining(Player) > 0f)
            {
                training.Tick();
            }

            Assert.That(training.HasAura(Player, AuraId.Shadowform), Is.False);
            Assert.That(
                training.CanUseAbility(
                    Player,
                    AbilityId.FlashHeal,
                    out string trainingReason),
                Is.True,
                trainingReason);

            var arena = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(
                arena.AddCombatant(
                    Player,
                    new TeamId(1),
                    ClassId.Priest,
                    SpecializationId.Shadow,
                    default,
                    100f),
                Is.True);
            Assert.That(
                arena.CanUseAbility(
                    Player,
                    AbilityId.FlashHeal,
                    out string arenaReason),
                Is.False);
            Assert.That(arenaReason, Is.EqualTo("wrong_specialization"));
        }

        [Test]
        public void LevelingAbilityChecks_AfterWarmupAllocateNoManagedMemory()
        {
            bool unlocked = false;
            for (int index = 0; index < 256; index++)
            {
                unlocked ^= LevelingAbilityUnlockCatalog.IsUnlocked(
                    SpecializationId.Marksmanship,
                    AbilityId.FreezingArrow,
                    index % 6 + 1);
            }

            GC.GetAllocatedBytesForCurrentThread();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 2048; index++)
            {
                unlocked ^= LevelingAbilityUnlockCatalog.IsUnlocked(
                    SpecializationId.Marksmanship,
                    AbilityId.FreezingArrow,
                    index % 6 + 1);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero);
            Assert.That(unlocked, Is.False);
        }

        [Test]
        public void TrainingAuthorization_AfterWarmupAllocatesNoManagedMemory()
        {
            var simulation = new CombatSimulation(CombatRules.LevelingDefault);
            Assert.That(
                simulation.AddCombatant(
                    Player,
                    new TeamId(1),
                    ClassId.Rogue,
                    SpecializationId.ShadowDance,
                    new Float3(-180f, 0f, ArenaCollisionMap.LevelingCenterZ),
                    100f),
                Is.True);
            Assert.That(simulation.ApplyProgressionLevel(Player, 2), Is.True);

            bool allowed = false;
            for (int index = 0; index < 256; index++)
            {
                allowed ^= simulation.CanUseAbility(
                    Player,
                    AbilityId.Sap,
                    out _);
            }

            GC.GetAllocatedBytesForCurrentThread();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.GetAllocatedBytesForCurrentThread();
            GC.GetAllocatedBytesForCurrentThread();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 2048; index++)
            {
                allowed ^= simulation.CanUseAbility(
                    Player,
                    AbilityId.Sap,
                    out _);
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero);
            Assert.That(allowed, Is.False);
        }

        private static float ResolveFireBlastDamage(int level)
        {
            CombatSimulation simulation = CreateDuel();
            if (level > LevelProgressionCurve.MinimumLevel)
            {
                Assert.That(simulation.ApplyProgressionLevel(Player, level), Is.True);
            }

            Assert.That(
                simulation.SubmitCommand(TargetCast(Player, 1, AbilityId.FireBlast, Enemy)),
                Is.True);
            simulation.Tick();
            CombatantSnapshot target = Snapshot(simulation, Enemy);
            return target.MaxHealth - target.Health;
        }

        private static void AssertLevelingKit(
            SpecializationId specialization,
            params AbilityId[] expected)
        {
            Assert.That(
                expected,
                Has.Length.EqualTo(LevelingAbilityUnlockCatalog.TotalAbilityCount));

            AbilityKit kit = LevelingAbilityUnlockCatalog.GetKit(specialization);
            for (int slot = 0; slot < expected.Length; slot++)
            {
                AbilityId ability = expected[slot];
                int requiredLevel =
                    LevelingAbilityUnlockCatalog.GetRequiredLevel(slot);

                Assert.That(
                    kit.GetAbility(slot),
                    Is.EqualTo(ability),
                    specialization + " slot " + slot + ".");
                Assert.That(
                    LevelingAbilityUnlockCatalog.TryGetRequiredLevel(
                        specialization,
                        ability,
                        out int catalogLevel),
                    Is.True,
                    specialization + " must expose " + ability + ".");
                Assert.That(
                    catalogLevel,
                    Is.EqualTo(requiredLevel),
                    specialization + " " + ability + " unlock level.");
                Assert.That(
                    LevelingAbilityUnlockCatalog.IsUnlocked(
                        specialization,
                        ability,
                        requiredLevel),
                    Is.True,
                    specialization + " " + ability + " must unlock at level " +
                    requiredLevel + ".");
                if (requiredLevel > LevelProgressionCurve.MinimumLevel)
                {
                    Assert.That(
                        LevelingAbilityUnlockCatalog.IsUnlocked(
                            specialization,
                            ability,
                            requiredLevel - 1),
                        Is.False,
                        specialization + " " + ability +
                        " must remain locked before level " + requiredLevel + ".");
                }
            }
        }

        private static CombatSimulation CreateDuel()
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(
                simulation.AddCombatant(
                    Player,
                    new TeamId(1),
                    ClassId.Mage,
                    SpecializationId.Fire,
                    default,
                    100f),
                Is.True);
            Assert.That(
                simulation.AddCombatant(
                    Enemy,
                    new TeamId(2),
                    ClassId.Mage,
                    SpecializationId.Fire,
                    new Float3(0f, 0f, 1f),
                    100f),
                Is.True);
            return simulation;
        }

        private static void AssertLevelAndHealth(
            CombatSimulation simulation,
            int expectedLevel,
            float expectedMaximumHealth)
        {
            CombatantSnapshot snapshot = Snapshot(simulation, Player);
            Assert.That(snapshot.Level, Is.EqualTo(expectedLevel));
            Assert.That(snapshot.MaxHealth, Is.EqualTo(expectedMaximumHealth));
            Assert.That(snapshot.Health, Is.EqualTo(expectedMaximumHealth));
        }

        private static CombatCommand TargetCast(
            ParticipantId actor,
            uint sequence,
            AbilityId ability,
            ParticipantId target)
        {
            return CombatCommand.Cast(
                actor,
                sequence,
                default,
                new Float3(0f, 0f, 1f),
                ability,
                target,
                new Float3(0f, 0f, 1f),
                new Float3(0f, 0f, 1f));
        }

        private static CombatantSnapshot Snapshot(
            CombatSimulation simulation,
            ParticipantId participant)
        {
            Assert.That(
                simulation.TryGetSnapshot(participant, out CombatantSnapshot snapshot),
                Is.True);
            return snapshot;
        }
    }
}
