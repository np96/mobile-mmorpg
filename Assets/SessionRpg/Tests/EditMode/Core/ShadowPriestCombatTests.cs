using System;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class ShadowPriestCombatTests
    {
        private static readonly ParticipantId Shadow = new ParticipantId(1);
        private static readonly ParticipantId Ally = new ParticipantId(2);
        private static readonly ParticipantId Enemy = new ParticipantId(3);
        private static readonly ParticipantId EnemyTwo = new ParticipantId(4);

        [Test]
        public void Catalog_IsDenseAndExposesTheShadowArenaKit()
        {
            Assert.That(AbilityCatalog.StorageCapacity, Is.EqualTo(126));
            for (int index = 1; index < AbilityCatalog.StorageCapacity; index++)
            {
                AbilityId ability = (AbilityId)index;
                Assert.That(AbilityCatalog.Get(ability).Id, Is.EqualTo(ability));
            }

            Assert.That(AuraCatalog.StorageCapacity, Is.EqualTo(66));
            for (int index = 1; index < AuraCatalog.StorageCapacity; index++)
            {
                AuraId aura = (AuraId)index;
                Assert.That(AuraCatalog.Get(aura).Id, Is.EqualTo(aura));
            }

            SpecializationDefinition specialization =
                SpecializationCatalog.GetDefinition(SpecializationId.Shadow);
            Assert.That(specialization.OwnerClass, Is.EqualTo(ClassId.Priest));
            Assert.That(specialization.PassiveAura0, Is.EqualTo(AuraId.Shadowform));
            Assert.That(specialization.PassiveAura1, Is.EqualTo(AuraId.VampiricEmbrace));
            Assert.That(
                SpecializationCatalog.GetResourceProfile(SpecializationId.Shadow).Type,
                Is.EqualTo(ResourceType.Mana));

            AbilityKit kit = SpecializationCatalog.GetKit(SpecializationId.Shadow);
            AbilityId[] expectedPrimary =
            {
                AbilityId.DevouringPlague,
                AbilityId.PsychicScream,
                AbilityId.VampiricTouch,
                AbilityId.MindFlay,
                AbilityId.PowerWordShield,
                AbilityId.DispelMagic,
                AbilityId.Shadowfiend,
                AbilityId.MassDispel,
                AbilityId.Shadowform,
                AbilityId.ArenaTrinket,
            };
            Assert.That(kit.Count, Is.EqualTo(10));
            Assert.That(kit.AbilityCount, Is.EqualTo(18));
            for (int slot = 0; slot < expectedPrimary.Length; slot++)
            {
                Assert.That(kit.GetAbility(slot), Is.EqualTo(expectedPrimary[slot]));
                Assert.That(
                    AbilityCatalog.IsAbilityAllowed(
                        ClassId.Priest,
                        SpecializationId.Shadow,
                        expectedPrimary[slot]),
                    Is.True);
            }

            AssertDirectional(kit, 0, AbilitySlotDirection.Up, AbilityId.ShadowWordDeath);
            AssertDirectional(kit, 0, AbilitySlotDirection.Down, AbilityId.ManaBurn);
            AssertDirectional(kit, 0, AbilitySlotDirection.Left, AbilityId.ShadowWordPain);
            AssertDirectional(kit, 0, AbilitySlotDirection.Right, AbilityId.MindBlast);
            AssertDirectional(kit, 1, AbilitySlotDirection.Up, AbilityId.Silence);
            AssertDirectional(kit, 1, AbilitySlotDirection.Down, AbilityId.PsychicHorror);
            AssertDirectional(kit, 1, AbilitySlotDirection.Left, AbilityId.Fade);
            AssertDirectional(kit, 1, AbilitySlotDirection.Right, AbilityId.Dispersion);
            Assert.That(kit.Contains(AbilityId.InnerFocus), Is.False);
            Assert.That(kit.Contains(AbilityId.FearWard), Is.False);
            Assert.That(kit.Contains(AbilityId.FlashHeal), Is.False);
            Assert.That(kit.Contains(AbilityId.Renew), Is.False);
            Assert.That(kit.Contains(AbilityId.PrayerOfMending), Is.False);

            Assert.That(
                AuraCatalog.Get(AuraId.DevouringPlague).Tags & AuraTag.UniquePerCaster,
                Is.EqualTo(AuraTag.UniquePerCaster));
            Assert.That(
                AbilityCatalog.GetEffect(AbilityId.DevouringPlague, 0).Kind,
                Is.EqualTo(AbilityEffectKind.Damage));
            Assert.That(
                AbilityEffectCatalog.Get(
                    AuraCatalog.Get(AuraId.DevouringPlague).PeriodicEffects.Offset).Kind,
                Is.EqualTo(AbilityEffectKind.LeechDamage));
            Assert.That(
                AbilityCatalog.GetEffect(AbilityId.ManaBurn, 0).Kind,
                Is.EqualTo(AbilityEffectKind.BurnResource));
            Assert.That(
                AbilityCatalog.GetEffect(AbilityId.MassDispel, 0).Kind,
                Is.EqualTo(AbilityEffectKind.DispelArea));

            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Shadow,
                1,
                ClassId.Priest,
                SpecializationId.Shadow,
                new Float3(-2f, 0f, 0f));
            Assert.That(simulation.HasAura(Shadow, AuraId.Shadowform), Is.True);
            Assert.That(simulation.HasAura(Shadow, AuraId.VampiricEmbrace), Is.True);
        }

        [Test]
        public void Shadowform_TogglesOffForFlashHealAndCanBeRestored()
        {
            CombatSimulation simulation = new CombatSimulation(
                CombatRules.LevelingDefault);
            Add(
                simulation,
                Shadow,
                1,
                ClassId.Priest,
                SpecializationId.Shadow,
                new Float3(-180f, 0f, ArenaCollisionMap.LevelingCenterZ));
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(-176f, 0f, ArenaCollisionMap.LevelingCenterZ));
            Assert.That(simulation.ApplyProgressionLevel(Shadow, 2), Is.True);

            CombatCommand blockedHeal = TargetCast(
                Shadow,
                1,
                AbilityId.FlashHeal,
                Shadow);
            Assert.That(
                simulation.CanStartAbility(blockedHeal, out string blockedReason),
                Is.False);
            Assert.That(blockedReason, Is.EqualTo("shadowform"));

            CastAndResolve(
                simulation,
                Shadow,
                1,
                AbilityId.Shadowform,
                Shadow);
            Assert.That(simulation.HasAura(Shadow, AuraId.Shadowform), Is.False);

            CastAndResolve(
                simulation,
                Enemy,
                1,
                AbilityId.FireBlast,
                Shadow);
            float damagedHealth = Snapshot(simulation, Shadow).Health;
            Assert.That(damagedHealth, Is.LessThan(100f));

            CastAndResolve(
                simulation,
                Shadow,
                2,
                AbilityId.FlashHeal,
                Shadow);
            Assert.That(Snapshot(simulation, Shadow).Health, Is.GreaterThan(damagedHealth));

            CastAndResolve(
                simulation,
                Shadow,
                3,
                AbilityId.Shadowform,
                Shadow);
            Assert.That(simulation.HasAura(Shadow, AuraId.Shadowform), Is.True);
        }

        [Test]
        public void VampiricTouch_DispelBacklashDamagesAfflictedTargetWithoutSilence()
        {
            CombatSimulation simulation = CreateThreeCombatantSimulation(
                ClassId.Mage,
                SpecializationId.Fire,
                ClassId.Priest,
                SpecializationId.Discipline);

            CastAndResolve(simulation, Shadow, 1, AbilityId.VampiricTouch, Enemy);
            Assert.That(simulation.HasAura(Enemy, AuraId.VampiricTouch), Is.True);
            float afflictedHealth = Snapshot(simulation, Enemy).Health;
            float dispellerHealth = Snapshot(simulation, EnemyTwo).Health;

            CastAndResolve(simulation, EnemyTwo, 1, AbilityId.DispelMagic, Enemy);

            Assert.That(simulation.HasAura(Enemy, AuraId.VampiricTouch), Is.False);
            Assert.That(Snapshot(simulation, Enemy).Health, Is.LessThan(afflictedHealth));
            Assert.That(
                Snapshot(simulation, EnemyTwo).Health,
                Is.EqualTo(dispellerHealth).Within(0.0001f));
            Assert.That(simulation.HasAura(Enemy, AuraId.Silence), Is.False);
            Assert.That(simulation.HasAura(EnemyTwo, AuraId.Silence), Is.False);
        }

        [Test]
        public void MindFlay_FirstTickRefreshesCasterOwnedShadowWordPain()
        {
            CombatSimulation simulation = CreateDuel(
                ClassId.Mage,
                SpecializationId.Fire);
            CastAndResolve(simulation, Shadow, 1, AbilityId.ShadowWordPain, Enemy);
            TickFor(simulation, 2f);
            Assert.That(
                simulation.GetAuraRemaining(Enemy, AuraId.ShadowWordPain),
                Is.LessThan(17f));

            BeginCast(simulation, Shadow, 2, AbilityId.MindFlay, Enemy);
            TickFor(simulation, 1f);

            Assert.That(
                simulation.GetAuraRemaining(Enemy, AuraId.ShadowWordPain),
                Is.EqualTo(18f).Within(simulation.Rules.TickSeconds));
        }

        [Test]
        public void DevouringPlague_ReplacesPriorTargetAndLeechesActualDamage()
        {
            CombatSimulation simulation = CreateThreeCombatantSimulation(
                ClassId.Mage,
                SpecializationId.Fire,
                ClassId.Mage,
                SpecializationId.Frost);
            CastAndResolve(simulation, Enemy, 1, AbilityId.FireBlast, Shadow);
            CastAndResolve(simulation, Shadow, 1, AbilityId.DevouringPlague, Enemy);
            Assert.That(simulation.HasAura(Enemy, AuraId.DevouringPlague), Is.True);

            CastAndResolve(simulation, Shadow, 2, AbilityId.DevouringPlague, EnemyTwo);

            Assert.That(simulation.HasAura(Enemy, AuraId.DevouringPlague), Is.False);
            Assert.That(simulation.HasAura(EnemyTwo, AuraId.DevouringPlague), Is.True);
            TickFor(simulation, 3f);

            float periodicDamage = SumCurrentEvents(
                simulation,
                CombatEventKind.Damage,
                AbilityId.DevouringPlague,
                EnemyTwo);
            Assert.That(periodicDamage, Is.GreaterThan(0f));
            Assert.That(
                HasCurrentEventAmount(
                    simulation,
                    CombatEventKind.Heal,
                    AbilityId.DevouringPlague,
                    Shadow,
                    periodicDamage * 0.15f),
                Is.True,
                "Devouring Plague must heal for 15% of actual post-mitigation damage.");
        }

        [Test]
        public void MindBlast_AppliesTwentyPercentHealingReductionAndReplenishment()
        {
            CombatSimulation simulation = CreateSimulation();
            AddShadowAndAlly(simulation);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(3f, 0f, 0f));
            Add(
                simulation,
                EnemyTwo,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(3f, 0f, 2f));

            CastAndResolve(simulation, Ally, 1, AbilityId.FireBlast, Enemy);
            CastAndResolve(simulation, Shadow, 1, AbilityId.VampiricTouch, Enemy);
            CastAndResolve(simulation, Shadow, 2, AbilityId.MindBlast, Enemy);

            Assert.That(simulation.HasAura(Enemy, AuraId.MortalWound), Is.True);
            Assert.That(simulation.HasAura(Shadow, AuraId.Replenishment), Is.True);
            Assert.That(simulation.HasAura(Ally, AuraId.Replenishment), Is.True);

            CastAndResolve(simulation, EnemyTwo, 1, AbilityId.FlashHeal, Enemy);
            Assert.That(
                SumCurrentEvents(
                    simulation,
                    CombatEventKind.Heal,
                    AbilityId.FlashHeal,
                    Enemy),
                Is.EqualTo(25.6f).Within(0.001f));
        }

        [Test]
        public void Silence_CancelsActiveCastAndBlocksMagicForFiveSeconds()
        {
            CombatSimulation simulation = CreateDuel(
                ClassId.Mage,
                SpecializationId.Fire);
            BeginCast(simulation, Enemy, 1, AbilityId.Fireball, Shadow);
            Assert.That(
                Snapshot(simulation, Enemy).CastingAbility,
                Is.EqualTo(AbilityId.Fireball));

            CastAndResolve(simulation, Shadow, 1, AbilityId.Silence, Enemy);

            Assert.That(Snapshot(simulation, Enemy).CastingAbility, Is.EqualTo(AbilityId.None));
            Assert.That(simulation.HasAura(Enemy, AuraId.Silence), Is.True);
            Assert.That(
                simulation.GetAuraRemaining(Enemy, AuraId.Silence),
                Is.EqualTo(5f).Within(simulation.Rules.TickSeconds));
            Assert.That(
                simulation.CanStartAbility(
                    TargetCast(Enemy, 2, AbilityId.FireBlast, Shadow),
                    out string reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("silenced"));
        }

        [Test]
        public void PsychicHorror_TrinketRemovesHorrorButLeavesIndependentDisarm()
        {
            CombatSimulation simulation = CreateDuel(
                ClassId.Hunter,
                SpecializationId.Marksmanship);
            CastAndResolve(simulation, Shadow, 1, AbilityId.PsychicHorror, Enemy);
            Assert.That(simulation.HasAura(Enemy, AuraId.Horror), Is.True);
            Assert.That(simulation.HasAura(Enemy, AuraId.Disarm), Is.True);
            Assert.That(
                Snapshot(simulation, Enemy).HasStatus(StatusId.Horrified),
                Is.True);
            Assert.That(
                Snapshot(simulation, Enemy).HasStatus(StatusId.Disarmed),
                Is.True);

            CastAndResolve(simulation, Enemy, 1, AbilityId.ArenaTrinket, Enemy);

            Assert.That(simulation.HasAura(Enemy, AuraId.Horror), Is.False);
            Assert.That(simulation.HasAura(Enemy, AuraId.Disarm), Is.True);
            Assert.That(
                Snapshot(simulation, Enemy).ActiveStatuses & StatusFlags.Horrified,
                Is.EqualTo(StatusFlags.None));
            Assert.That(
                Snapshot(simulation, Enemy).ActiveStatuses & StatusFlags.Disarmed,
                Is.EqualTo(StatusFlags.Disarmed));
            Assert.That(
                Snapshot(simulation, Enemy).HasStatus(StatusId.Horrified),
                Is.False);
            Assert.That(
                Snapshot(simulation, Enemy).HasStatus(StatusId.Disarmed),
                Is.True);
            Assert.That(
                simulation.CanStartAbility(
                    TargetCast(Enemy, 2, AbilityId.AimedShot, Shadow),
                    out string reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("disarmed"));
        }

        [Test]
        public void Dispersion_ClearsMovementControlPacifiesReducesDamageAndRestoresMana()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Shadow,
                1,
                ClassId.Priest,
                SpecializationId.Shadow,
                new Float3(-2f, 0f, 0f));
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(2f, 0f, 0f));
            Add(
                simulation,
                EnemyTwo,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(2f, 0f, 2f));

            CastAndResolve(simulation, Shadow, 1, AbilityId.ShadowWordPain, Enemy);
            CastAndResolve(simulation, Enemy, 1, AbilityId.Frostbolt, Shadow);
            CastAndResolve(simulation, Enemy, 2, AbilityId.FrostNova, Shadow);
            Assert.That(simulation.HasAura(Shadow, AuraId.FrostSlow), Is.True);
            Assert.That(simulation.HasAura(Shadow, AuraId.FrozenRoot), Is.True);

            CastAndResolve(simulation, Shadow, 2, AbilityId.Dispersion, Shadow);

            Assert.That(simulation.HasAura(Shadow, AuraId.FrostSlow), Is.False);
            Assert.That(simulation.HasAura(Shadow, AuraId.FrozenRoot), Is.False);
            Assert.That(simulation.HasAura(Shadow, AuraId.Dispersion), Is.True);
            Assert.That(
                Snapshot(simulation, Shadow).ActiveStatuses & StatusFlags.Pacified,
                Is.EqualTo(StatusFlags.Pacified));
            Assert.That(
                Snapshot(simulation, Shadow).HasStatus(StatusId.Pacified),
                Is.True);
            Assert.That(
                simulation.CanStartAbility(
                    TargetCast(Shadow, 3, AbilityId.ShadowWordPain, Enemy),
                    out string reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("pacified"));

            CastAndResolve(simulation, EnemyTwo, 1, AbilityId.FireBlast, Shadow);
            Assert.That(
                SumCurrentEvents(
                    simulation,
                    CombatEventKind.Damage,
                    AbilityId.FireBlast,
                    Shadow),
                Is.InRange(0.01f, 1.2f));

            float manaBefore = Snapshot(simulation, Shadow).Resource;
            TickFor(simulation, 1f);
            Assert.That(
                Snapshot(simulation, Shadow).Resource,
                Is.EqualTo(Math.Min(100f, manaBefore + 8f)).Within(0.001f));
        }

        [Test]
        public void Fade_RemovesBothRootAndSlow()
        {
            CombatSimulation simulation = CreateDuel(
                ClassId.Mage,
                SpecializationId.Frost);
            CastAndResolve(simulation, Enemy, 1, AbilityId.Frostbolt, Shadow);
            CastAndResolve(simulation, Enemy, 2, AbilityId.FrostNova, Shadow);
            Assert.That(simulation.HasAura(Shadow, AuraId.FrostSlow), Is.True);
            Assert.That(simulation.HasAura(Shadow, AuraId.FrozenRoot), Is.True);

            CastAndResolve(simulation, Shadow, 1, AbilityId.Fade, Shadow);

            Assert.That(simulation.HasAura(Shadow, AuraId.FrostSlow), Is.False);
            Assert.That(simulation.HasAura(Shadow, AuraId.FrozenRoot), Is.False);
        }

        [Test]
        public void ManaBurn_BurnsTenPercentManaAndDealsHalfAsShadowDamage()
        {
            CombatSimulation simulation = CreateDuel(
                ClassId.Mage,
                SpecializationId.Fire);

            CastAndResolve(simulation, Shadow, 1, AbilityId.ManaBurn, Enemy);

            Assert.That(Snapshot(simulation, Enemy).Resource, Is.EqualTo(90f).Within(0.001f));
            Assert.That(
                SumCurrentEvents(
                    simulation,
                    CombatEventKind.Damage,
                    AbilityId.ManaBurn,
                    Enemy),
                Is.EqualTo(5.75f).Within(0.001f),
                "Five raw damage is increased by Shadowform's 15% Shadow bonus.");
        }

        [Test]
        public void MassDispel_CleansesFriendAndEnemyAtTheTargetPoint()
        {
            CombatSimulation simulation = CreateSimulation();
            AddShadowAndAlly(simulation);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(2f, 0f, 0f));
            Add(
                simulation,
                EnemyTwo,
                2,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(2f, 0f, 2f));

            CastAndResolve(simulation, Enemy, 1, AbilityId.LivingBomb, Ally);
            CastAndResolve(simulation, EnemyTwo, 1, AbilityId.IceBarrier, EnemyTwo);
            Assert.That(simulation.HasAura(Ally, AuraId.LivingEmber), Is.True);
            Assert.That(simulation.HasAura(EnemyTwo, AuraId.IceWard), Is.True);

            CastAndResolve(
                simulation,
                Shadow,
                1,
                AbilityId.MassDispel,
                default,
                new Float3(0f, 0f, 1f));

            Assert.That(simulation.HasAura(Ally, AuraId.LivingEmber), Is.False);
            Assert.That(simulation.HasAura(EnemyTwo, AuraId.IceWard), Is.False);
        }

        private static void AssertDirectional(
            AbilityKit kit,
            int slot,
            AbilitySlotDirection direction,
            AbilityId expected)
        {
            Assert.That(
                kit.GetAbility(slot, direction),
                Is.EqualTo(expected));
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Priest,
                    SpecializationId.Shadow,
                    expected),
                Is.True);
        }

        private static CombatSimulation CreateDuel(
            ClassId enemyClass,
            SpecializationId enemySpecialization)
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Shadow,
                1,
                ClassId.Priest,
                SpecializationId.Shadow,
                new Float3(-2f, 0f, 0f));
            Add(
                simulation,
                Enemy,
                2,
                enemyClass,
                enemySpecialization,
                new Float3(2f, 0f, 0f));
            return simulation;
        }

        private static CombatSimulation CreateThreeCombatantSimulation(
            ClassId firstEnemyClass,
            SpecializationId firstEnemySpecialization,
            ClassId secondEnemyClass,
            SpecializationId secondEnemySpecialization)
        {
            CombatSimulation simulation = CreateDuel(
                firstEnemyClass,
                firstEnemySpecialization);
            Add(
                simulation,
                EnemyTwo,
                2,
                secondEnemyClass,
                secondEnemySpecialization,
                new Float3(2f, 0f, 2f));
            return simulation;
        }

        private static CombatSimulation CreateSimulation()
        {
            return new CombatSimulation(new CombatRules(
                30,
                5.4f,
                100f,
                120f,
                0f,
                1.5f,
                15f,
                5,
                ArenaCollisionMap.None));
        }

        private static void AddShadowAndAlly(CombatSimulation simulation)
        {
            Add(
                simulation,
                Shadow,
                1,
                ClassId.Priest,
                SpecializationId.Shadow,
                new Float3(-2f, 0f, 0f));
            Add(
                simulation,
                Ally,
                1,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(-2f, 0f, 2f));
        }

        private static void Add(
            CombatSimulation simulation,
            ParticipantId actor,
            int team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position)
        {
            Assert.That(
                simulation.AddCombatant(
                    actor,
                    new TeamId(team),
                    classId,
                    specialization,
                    position),
                Is.True);
        }

        private static void BeginCast(
            CombatSimulation simulation,
            ParticipantId actor,
            uint sequence,
            AbilityId ability,
            ParticipantId target,
            Float3 targetPoint = default)
        {
            CombatCommand command = TargetCast(
                actor,
                sequence,
                ability,
                target,
                targetPoint);
            WaitUntilStartable(simulation, command);
            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();
            Assert.That(
                FindCurrentEvent(simulation, CombatEventKind.CastAccepted, ability).Actor,
                Is.EqualTo(actor));
        }

        private static void CastAndResolve(
            CombatSimulation simulation,
            ParticipantId actor,
            uint sequence,
            AbilityId ability,
            ParticipantId target,
            Float3 targetPoint = default)
        {
            BeginCast(simulation, actor, sequence, ability, target, targetPoint);
            while (Snapshot(simulation, actor).CastingAbility != AbilityId.None)
            {
                simulation.Tick();
            }
        }

        private static CombatCommand TargetCast(
            ParticipantId actor,
            uint sequence,
            AbilityId ability,
            ParticipantId target,
            Float3 targetPoint = default)
        {
            return CombatCommand.Cast(
                actor,
                sequence,
                default,
                new Float3(1f, 0f, 0f),
                ability,
                target,
                targetPoint,
                new Float3(1f, 0f, 0f));
        }

        private static void WaitUntilStartable(
            CombatSimulation simulation,
            CombatCommand command)
        {
            for (int tick = 0; tick < 1000; tick++)
            {
                if (simulation.CanStartAbility(command, out string reason))
                {
                    return;
                }

                if (reason != "global_cooldown" &&
                    reason != "cooldown" &&
                    reason != "already_casting" &&
                    reason != "resource")
                {
                    Assert.Fail("Ability could not start: " + reason);
                }

                simulation.Tick();
            }

            Assert.Fail("Timed out waiting to start " + command.Ability + ".");
        }

        private static void TickFor(CombatSimulation simulation, float seconds)
        {
            int ticks = (int)Math.Ceiling(seconds / simulation.Rules.TickSeconds);
            for (int tick = 0; tick < ticks; tick++)
            {
                simulation.Tick();
            }
        }

        private static CombatantSnapshot Snapshot(
            CombatSimulation simulation,
            ParticipantId actor)
        {
            Assert.That(
                simulation.TryGetSnapshot(actor, out CombatantSnapshot snapshot),
                Is.True);
            return snapshot;
        }

        private static CombatEvent FindCurrentEvent(
            CombatSimulation simulation,
            CombatEventKind kind,
            AbilityId ability)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind && combatEvent.Ability == ability)
                {
                    return combatEvent;
                }
            }

            Assert.Fail("Expected current-tick " + kind + " for " + ability + ".");
            return default;
        }

        private static float SumCurrentEvents(
            CombatSimulation simulation,
            CombatEventKind kind,
            AbilityId ability,
            ParticipantId target)
        {
            float amount = 0f;
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind && combatEvent.Ability == ability &&
                    combatEvent.Target == target)
                {
                    amount += combatEvent.Amount;
                }
            }

            return amount;
        }

        private static bool HasCurrentEventAmount(
            CombatSimulation simulation,
            CombatEventKind kind,
            AbilityId ability,
            ParticipantId target,
            float expected)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind && combatEvent.Ability == ability &&
                    combatEvent.Target == target &&
                    Math.Abs(combatEvent.Amount - expected) <= 0.001f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
