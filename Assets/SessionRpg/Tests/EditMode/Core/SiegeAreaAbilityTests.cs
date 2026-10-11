using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class SiegeAreaAbilityTests
    {
        private static readonly ParticipantId Rogue = new ParticipantId(1);
        private static readonly ParticipantId Ally = new ParticipantId(2);
        private static readonly ParticipantId NearEnemyA = new ParticipantId(3);
        private static readonly ParticipantId NearEnemyB = new ParticipantId(4);
        private static readonly ParticipantId EdgeEnemy = new ParticipantId(5);
        private static readonly ParticipantId FarEnemy = new ParticipantId(6);

        [Test]
        public void EverySpecializationGetsOneVisibleSiegeAreaPrimaryWithoutChangingArena()
        {
            Assert.That((int)AbilityId.BladeTempest, Is.EqualTo(120));
            Assert.That((int)AbilityId.ArrowRain, Is.EqualTo(125));
            Assert.That(AbilityCatalog.StorageCapacity, Is.EqualTo(126));

            AbilityDefinition definition = AbilityCatalog.Get(
                AbilityId.BladeTempest);
            Assert.That(definition.DisplayName, Is.EqualTo("Blade Tempest"));
            Assert.That(definition.Targeting.Shape, Is.EqualTo(AbilityTargetShape.CasterArea));
            Assert.That(definition.Targeting.Radius, Is.EqualTo(5f));
            Assert.That(definition.Effects, Is.EqualTo(AbilityEffectCatalog.BladeTempest));
            Assert.That(
                AbilityEffectCatalog.Get(definition.Effects.Offset).Target,
                Is.EqualTo(AbilityEffectTarget.AreaHostiles));

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
            for (int index = 0; index < specializations.Length; index++)
            {
                AssertModeBinding(specializations[index]);
            }
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Rogue,
                    SpecializationId.ShadowDance,
                    AbilityId.BladeTempest),
                Is.False,
                "Compatibility and Arena authorization must not gain Siege power.");
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Rogue,
                    SpecializationId.ShadowDance,
                    AbilityId.BladeTempest,
                    PlayableModeId.HeroSiege),
                Is.True);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Mage,
                    SpecializationId.Fire,
                    AbilityId.BladeTempest,
                    PlayableModeId.HeroSiege),
                Is.False);
        }

        [Test]
        public void BladeTempest_DamagesEveryLivingHostileInsideInclusiveRadius()
        {
            CombatSimulation simulation = CreateSimulation(PlayableModeId.HeroSiege);
            Add(simulation, Rogue, 1, ClassId.Rogue, SpecializationId.Mutilate, default);
            Add(simulation, Ally, 1, ClassId.Priest, SpecializationId.Discipline, new Float3(1f, 0f, 0f));
            Add(simulation, NearEnemyA, 2, ClassId.Mage, SpecializationId.Fire, new Float3(2f, 0f, 0f));
            Add(simulation, NearEnemyB, 2, ClassId.Priest, SpecializationId.Shadow, new Float3(-3.5f, 0f, 0f));
            Add(simulation, EdgeEnemy, 2, ClassId.Hunter, SpecializationId.Marksmanship, new Float3(0f, 0f, 5f));
            Add(simulation, FarEnemy, 2, ClassId.Mage, SpecializationId.Frost, new Float3(0f, 0f, 5.1f));

            Assert.That(simulation.HasAura(Rogue, AuraId.Stealth), Is.True);
            CombatCommand command = Cast(Rogue, 1u, AbilityId.BladeTempest);
            Assert.That(simulation.CanStartAbility(command, out string reason), Is.True, reason);
            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();

            Assert.That(Snapshot(simulation, Ally).Health, Is.EqualTo(100f));
            Assert.That(Snapshot(simulation, NearEnemyA).Health, Is.EqualTo(90f));
            Assert.That(
                Snapshot(simulation, NearEnemyB).Health,
                Is.EqualTo(91.5f).Within(0.001f),
                "Shadowform mitigation must apply to each radial damage target.");
            Assert.That(Snapshot(simulation, EdgeEnemy).Health, Is.EqualTo(90f));
            Assert.That(Snapshot(simulation, FarEnemy).Health, Is.EqualTo(100f));
            Assert.That(Snapshot(simulation, Rogue).Resource, Is.EqualTo(65f));
            Assert.That(simulation.HasAura(Rogue, AuraId.Stealth), Is.False);
            Assert.That(
                simulation.GetCooldownRemaining(Rogue, AbilityId.BladeTempest),
                Is.GreaterThan(3.9f));
        }

        [TestCase(
            ClassId.Priest,
            SpecializationId.Shadow,
            AbilityId.UmbralCollapse)]
        [TestCase(
            ClassId.Mage,
            SpecializationId.Fire,
            AbilityId.FlameSurge)]
        [TestCase(
            ClassId.Mage,
            SpecializationId.Frost,
            AbilityId.Blizzard)]
        [TestCase(
            ClassId.Hunter,
            SpecializationId.Marksmanship,
            AbilityId.ArrowRain)]
        public void TargetedSiegeAreaAbilities_ClearAClusterAtTheChosenPoint(
            ClassId classId,
            SpecializationId specialization,
            AbilityId ability)
        {
            CombatSimulation simulation = CreateSimulation(
                PlayableModeId.HeroSiege);
            Add(simulation, Rogue, 1, classId, specialization, default);
            Add(simulation, NearEnemyA, 2, ClassId.Rogue,
                SpecializationId.Mutilate, new Float3(8f, 0f, 0f));
            Add(simulation, NearEnemyB, 2, ClassId.Rogue,
                SpecializationId.Mutilate, new Float3(11f, 0f, 0f));
            Add(simulation, FarEnemy, 2, ClassId.Rogue,
                SpecializationId.Mutilate, new Float3(15.1f, 0f, 0f));

            CombatCommand command = CombatCommand.Cast(
                Rogue,
                1u,
                default,
                new Float3(1f, 0f, 0f),
                ability,
                default,
                new Float3(10f, 0f, 0f),
                new Float3(1f, 0f, 0f));
            Assert.That(
                simulation.CanStartAbility(command, out string reason),
                Is.True,
                reason);
            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();

            Assert.That(Snapshot(simulation, NearEnemyA).Health,
                Is.LessThan(100f));
            Assert.That(Snapshot(simulation, NearEnemyB).Health,
                Is.LessThan(100f));
            Assert.That(Snapshot(simulation, FarEnemy).Health,
                Is.EqualTo(100f));
            if (ability == AbilityId.Blizzard)
            {
                Assert.That(
                    Snapshot(simulation, NearEnemyA).HasStatus(StatusId.Slowed),
                    Is.True);
            }
        }

        [Test]
        public void DawnNova_DamagesNearbyHostilesWithoutHarmingTheParty()
        {
            CombatSimulation simulation = CreateSimulation(
                PlayableModeId.HeroSiege);
            Add(simulation, Rogue, 1, ClassId.Priest,
                SpecializationId.Discipline, default);
            Add(simulation, Ally, 1, ClassId.Mage,
                SpecializationId.Fire, new Float3(2f, 0f, 0f));
            Add(simulation, NearEnemyA, 2, ClassId.Rogue,
                SpecializationId.Mutilate, new Float3(3f, 0f, 0f));
            Add(simulation, FarEnemy, 2, ClassId.Rogue,
                SpecializationId.Mutilate, new Float3(5.6f, 0f, 0f));

            CombatCommand command = Cast(Rogue, 1u, AbilityId.DawnNova);
            Assert.That(
                simulation.CanStartAbility(command, out string reason),
                Is.True,
                reason);
            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();

            Assert.That(Snapshot(simulation, Ally).Health, Is.EqualTo(100f));
            Assert.That(Snapshot(simulation, NearEnemyA).Health,
                Is.LessThan(100f));
            Assert.That(Snapshot(simulation, FarEnemy).Health,
                Is.EqualTo(100f));
        }

        [Test]
        public void BladeTempest_IsDeterministicallyRejectedOutsideSiege()
        {
            CombatSimulation simulation = CreateSimulation(PlayableModeId.Arena);
            Add(simulation, Rogue, 1, ClassId.Rogue, SpecializationId.ShadowDance, default);
            Add(simulation, NearEnemyA, 2, ClassId.Mage, SpecializationId.Fire, new Float3(2f, 0f, 0f));

            CombatCommand command = Cast(Rogue, 1u, AbilityId.BladeTempest);
            Assert.That(simulation.CanStartAbility(command, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("wrong_specialization"));
            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();

            Assert.That(Snapshot(simulation, NearEnemyA).Health, Is.EqualTo(100f));
            Assert.That(simulation.EventCount, Is.EqualTo(1));
            Assert.That(simulation.GetEvent(0).Kind, Is.EqualTo(CombatEventKind.CastRejected));
            Assert.That(simulation.GetEvent(0).Reason, Is.EqualTo("wrong_specialization"));
        }

        private static void AssertModeBinding(SpecializationId specialization)
        {
            AbilityKit arena = ModeAbilityKitCatalog.GetKit(
                PlayableModeId.Arena,
                specialization);
            AbilityKit siege = ModeAbilityKitCatalog.GetKit(
                PlayableModeId.HeroSiege,
                specialization);
            AbilityId areaAbility = ModeAbilityKitCatalog.GetSiegeAreaAbility(
                specialization);
            Assert.That(areaAbility, Is.Not.EqualTo(AbilityId.None));
            Assert.That(arena.Contains(areaAbility), Is.False);
            Assert.That(siege.Contains(areaAbility), Is.True);
            Assert.That(arena.GetAbility(ModeAbilityKitCatalog.SiegeAreaSlot),
                Is.EqualTo(AbilityId.ArenaTrinket));
            Assert.That(
                siege.GetAbility(
                    ModeAbilityKitCatalog.SiegeAreaSlot,
                    ModeAbilityKitCatalog.SiegeAreaDirection),
                Is.EqualTo(areaAbility));
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    siege.OwnerClass,
                    specialization,
                    areaAbility,
                    PlayableModeId.HeroSiege),
                Is.True);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    arena.OwnerClass,
                    specialization,
                    areaAbility,
                    PlayableModeId.Arena),
                Is.False);
        }

        private static CombatSimulation CreateSimulation(PlayableModeId mode) =>
            new CombatSimulation(new CombatRules(
                30,
                0f,
                100f,
                120f,
                0f,
                1.5f,
                15f,
                5,
                ArenaCollisionMap.None,
                false,
                playableMode: mode));

        private static void Add(
            CombatSimulation simulation,
            ParticipantId participant,
            int team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position)
        {
            Assert.That(
                simulation.AddCombatant(
                    participant,
                    new TeamId(team),
                    classId,
                    specialization,
                    position),
                Is.True);
        }

        private static CombatCommand Cast(
            ParticipantId actor,
            uint sequence,
            AbilityId ability) =>
            CombatCommand.Cast(
                actor,
                sequence,
                default,
                new Float3(1f, 0f, 0f),
                ability,
                actor,
                default,
                new Float3(1f, 0f, 0f));

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
