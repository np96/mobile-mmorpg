using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class HunterCombatSimulationTests
    {
        private static readonly ParticipantId Hunter = new ParticipantId(1);
        private static readonly ParticipantId Ally = new ParticipantId(2);
        private static readonly ParticipantId Enemy = new ParticipantId(3);
        private static readonly ParticipantId EnemyTwo = new ParticipantId(4);

        [Test]
        public void Marksmanship_SpawnsOneOwnerBoundCompanion_ThatDoesNotCountAsCombatant()
        {
            CombatSimulation simulation = CreateSimulation();
            AddHunter(simulation, Hunter, 1, default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 8f));

            Assert.That(simulation.CombatantCount, Is.EqualTo(2));
            Assert.That(simulation.CompanionCount, Is.EqualTo(1));
            Assert.That(
                simulation.TryGetCompanionSnapshot(Hunter, out CompanionSnapshot companion),
                Is.True);
            Assert.That(companion.Owner, Is.EqualTo(Hunter));
            Assert.That(companion.Team, Is.EqualTo(new TeamId(1)));
            Assert.That(companion.IsAlive, Is.True);
            Assert.That(companion.Health, Is.EqualTo(companion.MaxHealth));
            Assert.That(companion.Order, Is.EqualTo(CompanionOrder.Follow));

            var buffer = new CompanionSnapshot[CombatSimulation.MaxCombatants];
            Assert.That(simulation.CopyCompanionSnapshots(buffer), Is.EqualTo(1));
            Assert.That(buffer[0].Owner, Is.EqualTo(Hunter));
        }

        [Test]
        public void LevelingRules_DisableArenaRadialHazardsAcrossRouteAndForCompanion()
        {
            CombatRules rules = CombatRules.LevelingDefault;
            var simulation = new CombatSimulation(rules);
            Float3 west = new Float3(
                ArenaCollisionMap.LevelingMinimumX + 2f,
                0f,
                ArenaCollisionMap.LevelingCenterZ);
            Float3 east = new Float3(
                ArenaCollisionMap.LevelingMaximumX - 2f,
                0f,
                ArenaCollisionMap.LevelingCenterZ);
            AddHunter(simulation, Hunter, 1, west);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                east);

            Assert.That(rules.RadialHazardsEnabled, Is.False);
            simulation.Tick();

            CombatantSnapshot hunter = Snapshot(simulation, Hunter);
            CombatantSnapshot enemy = Snapshot(simulation, Enemy);
            Assert.That(hunter.IsAlive, Is.True);
            Assert.That(hunter.IsInLava, Is.False);
            Assert.That(enemy.IsAlive, Is.True);
            Assert.That(enemy.IsInLava, Is.False);
            Assert.That(
                simulation.TryGetCompanionSnapshot(
                    Hunter,
                    out CompanionSnapshot companion),
                Is.True);
            Assert.That(companion.IsAlive, Is.True);
        }

        [Test]
        public void OwnerDeath_DespawnsCompanionWithoutAddingAnotherVictoryParticipant()
        {
            var rules = new CombatRules(30, 0f, 5f, 10f, 24f);
            var simulation = new CombatSimulation(rules);
            AddHunter(simulation, Hunter, 1, new Float3(11f, 0f, 0f));

            Assert.That(simulation.CompanionCount, Is.EqualTo(1));
            simulation.Tick();

            Assert.That(Snapshot(simulation, Hunter).IsAlive, Is.False);
            Assert.That(simulation.CompanionCount, Is.Zero);
            Assert.That(
                simulation.TryGetCompanionSnapshot(Hunter, out _),
                Is.False);
        }

        [Test]
        public void PetAttack_PursuesTargetWhileOmittedDirectOrdersRejectCleanly()
        {
            CombatSimulation simulation = CreateSimulation();
            AddHunter(simulation, Hunter, 1, default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 3f));

            Cast(simulation, UnitCast(Hunter, 1, AbilityId.PetAttack, Enemy));
            TickUntil(simulation, 30);

            Assert.That(Snapshot(simulation, Enemy).Health, Is.LessThan(100f));
            Assert.That(
                simulation.TryGetCompanionSnapshot(Hunter, out CompanionSnapshot attacking),
                Is.True);
            Assert.That(attacking.Order, Is.EqualTo(CompanionOrder.Attack));
            Assert.That(attacking.Target, Is.EqualTo(Enemy));

            Assert.That(
                simulation.CanStartAbility(
                    SelfCast(Hunter, 2, AbilityId.PetStay),
                    out string stayReason),
                Is.False);
            Assert.That(stayReason, Is.EqualTo("wrong_specialization"));
            Assert.That(
                simulation.CanStartAbility(
                    SelfCast(Hunter, 3, AbilityId.PetFollow),
                    out string followReason),
                Is.False);
            Assert.That(followReason, Is.EqualTo("wrong_specialization"));
        }

        [Test]
        public void PetAttack_IsACompanionCommandAndNotAWeaponAbility()
        {
            CombatSimulation simulation = CreateSimulation();
            AddHunter(simulation, Hunter, 1, default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 2f));

            Assert.That(AbilityCatalog.IsWeaponAbility(AbilityId.PetAttack), Is.False);
            Assert.That(
                simulation.CanStartAbility(
                    UnitCast(Hunter, 1, AbilityId.PetAttack, Enemy),
                    out string reason),
                Is.True,
                reason);

            Cast(simulation, UnitCast(Hunter, 1, AbilityId.PetAttack, Enemy));
            TickUntil(simulation, simulation.CurrentTick + 12);

            Assert.That(Snapshot(simulation, Enemy).Health, Is.LessThan(100f));
        }

        [Test]
        public void Traps_UseFixedGroundStorageAndTriggerAfterArming()
        {
            CombatSimulation freezing = CreateSimulation();
            AddHunter(freezing, Hunter, 1, default);
            Float3 trapPoint = new Float3(0f, 0f, 7f);
            Add(
                freezing,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                trapPoint);

            Cast(
                freezing,
                PointCast(Hunter, 1, AbilityId.FreezingArrow, trapPoint));
            Assert.That(freezing.GroundEffectCount, Is.EqualTo(1));
            Assert.That(freezing.HasAura(Enemy, AuraId.FreezingTrap), Is.False);
            freezing.Tick();
            Assert.That(freezing.HasAura(Enemy, AuraId.FreezingTrap), Is.True);
            Assert.That(freezing.GroundEffectCount, Is.Zero);

            CombatSimulation frost = CreateSimulation();
            AddHunter(frost, Hunter, 1, default);
            Add(
                frost,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(1f, 0f, 0f));
            Cast(frost, SelfCast(Hunter, 1, AbilityId.FrostTrap));
            frost.Tick();

            Assert.That(frost.HasAura(Enemy, AuraId.FrostTrapSlow), Is.True);
            Assert.That(frost.GroundEffectCount, Is.EqualTo(1));
            var effects = new GroundEffectSnapshot[2];
            Assert.That(frost.CopyGroundEffectSnapshots(effects), Is.EqualTo(1));
            Assert.That(effects[0].Id, Is.EqualTo(GroundEffectId.FrostTrap));
            Assert.That(effects[0].Radius, Is.EqualTo(4f));
        }

        [Test]
        public void FrostTrap_OverlappingFieldsRefreshSlowWithoutRepeatingStatusApplied()
        {
            CombatSimulation simulation = CreateSimulation();
            AddHunter(simulation, Hunter, 1, new Float3(-2f, 0f, 0f));
            AddHunter(simulation, Ally, 1, new Float3(2f, 0f, 0f));
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            Cast(simulation, SelfCast(Hunter, 1, AbilityId.FrostTrap));
            Cast(simulation, SelfCast(Ally, 1, AbilityId.FrostTrap));
            int statusApplications = FindEvent(
                simulation,
                CombatEventKind.StatusApplied,
                AbilityId.FrostTrap)
                    ? 1
                    : 0;

            for (int tick = 0; tick < simulation.Rules.TickRate * 3; tick++)
            {
                simulation.Tick();
                if (FindEvent(
                        simulation,
                        CombatEventKind.StatusApplied,
                        AbilityId.FrostTrap))
                {
                    statusApplications++;
                }

                Assert.That(
                    simulation.HasAura(Enemy, AuraId.FrostTrapSlow),
                    Is.True,
                    "The slowing field must maintain the aura while the target remains inside.");
            }

            Assert.That(statusApplications, Is.EqualTo(1));
            Assert.That(
                simulation.GetAuraRemaining(Enemy, AuraId.FrostTrapSlow),
                Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void HunterRequirements_RejectMinimumRangeAndKillShotAboveThreshold()
        {
            CombatSimulation simulation = CreateSimulation();
            AddHunter(simulation, Hunter, 1, default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 2f));
            Add(
                simulation,
                EnemyTwo,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 8f));

            Assert.That(
                simulation.CanStartAbility(
                    UnitCast(Hunter, 1, AbilityId.ChimeraShot, Enemy),
                    out string closeReason),
                Is.False);
            Assert.That(closeReason, Is.EqualTo("minimum_range"));

            Assert.That(
                simulation.CanStartAbility(
                    UnitCast(Hunter, 1, AbilityId.KillShot, EnemyTwo),
                    out string healthReason),
                Is.False);
            Assert.That(
                healthReason,
                Is.EqualTo("target_health_above_twenty_percent"));
        }

        [Test]
        public void Disengage_MovesBackwardAndStartsItsCooldown()
        {
            CombatSimulation simulation = CreateSimulation();
            AddHunter(simulation, Hunter, 1, default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 8f));

            Cast(simulation, UnitCast(Hunter, 1, AbilityId.ArcaneShot, Enemy));
            Assert.That(
                simulation.GetCooldownRemaining(Hunter, AbilityId.ArcaneShot),
                Is.GreaterThan(0f));

            Cast(simulation, SelfCast(Hunter, 2, AbilityId.Disengage));
            Assert.That(Snapshot(simulation, Hunter).Position.Z, Is.EqualTo(-8f));
            Assert.That(
                simulation.GetCooldownRemaining(Hunter, AbilityId.Disengage),
                Is.GreaterThan(0f));
        }

        [TestCase(AbilityId.Flare)]
        [TestCase(AbilityId.FeignDeath)]
        [TestCase(AbilityId.MastersCall)]
        [TestCase(AbilityId.WingClip)]
        [TestCase(AbilityId.AspectOfTheDragonhawk)]
        [TestCase(AbilityId.AspectOfTheViper)]
        [TestCase(AbilityId.RoarOfSacrifice)]
        [TestCase(AbilityId.SpiderWeb)]
        [TestCase(AbilityId.Readiness)]
        public void OmittedHunterAbility_RejectsAsWrongSpecialization(
            AbilityId ability)
        {
            CombatSimulation simulation = CreateSimulation();
            AddHunter(simulation, Hunter, 1, default);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Hunter,
                    SpecializationId.Marksmanship,
                    ability),
                Is.False);
            Assert.That(
                simulation.CanUseAbility(Hunter, ability, out string reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("wrong_specialization"));
        }

        [Test]
        public void Deterrence_BlocksOnlyFrontalPressureAndPacifiesHunter()
        {
            CombatSimulation deterrence = CreateSimulation();
            AddHunter(deterrence, Hunter, 1, default);
            Add(
                deterrence,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 8f));
            Cast(deterrence, SelfCast(Hunter, 1, AbilityId.Deterrence));
            Cast(
                deterrence,
                UnitCast(
                    Enemy,
                    1,
                    AbilityId.FireBlast,
                    Hunter,
                    new Float3(0f, 0f, -1f)));

            Assert.That(Snapshot(deterrence, Hunter).Health, Is.EqualTo(100f));
            Assert.That(
                deterrence.CanStartAbility(
                    UnitCast(Hunter, 2, AbilityId.ArcaneShot, Enemy),
                    out string reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("pacified"));
        }

        [Test]
        public void AimedShot_UsesAuraMagnitudeForHealingReduction()
        {
            CombatSimulation simulation = CreateSimulation();
            AddHunter(simulation, Hunter, 1, default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 8f));

            Cast(simulation, UnitCast(Hunter, 1, AbilityId.ChimeraShot, Enemy));
            TickUntil(simulation, 46);
            Cast(simulation, UnitCast(Hunter, 2, AbilityId.AimedShot, Enemy));
            Assert.That(simulation.HasAura(Enemy, AuraId.MortalWound), Is.True);

            Cast(simulation, UnitCast(Enemy, 1, AbilityId.FlashHeal, Enemy));
            int healEndTick = Snapshot(simulation, Enemy).CastEndTick;
            TickUntil(simulation, healEndTick);

            Assert.That(
                FindEventAmount(simulation, CombatEventKind.Heal, AbilityId.FlashHeal),
                Is.EqualTo(16f).Within(0.0001f));
        }

        private static CombatSimulation CreateSimulation()
        {
            return new CombatSimulation(
                new CombatRules(30, 5.4f, 50f, 60f, 24f));
        }

        private static void AddHunter(
            CombatSimulation simulation,
            ParticipantId id,
            int team,
            Float3 position)
        {
            Add(
                simulation,
                id,
                team,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                position);
        }

        private static void Add(
            CombatSimulation simulation,
            ParticipantId id,
            int team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position)
        {
            Assert.That(
                simulation.AddCombatant(
                    id,
                    new TeamId(team),
                    classId,
                    specialization,
                    position),
                Is.True);
        }

        private static void Cast(CombatSimulation simulation, CombatCommand command)
        {
            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();
            Assert.That(
                FindEvent(simulation, CombatEventKind.CastAccepted, command.Ability),
                Is.True,
                command.Ability + " was not accepted.");
        }

        private static CombatCommand SelfCast(
            ParticipantId actor,
            uint sequence,
            AbilityId ability)
        {
            return UnitCast(actor, sequence, ability, actor);
        }

        private static CombatCommand UnitCast(
            ParticipantId actor,
            uint sequence,
            AbilityId ability,
            ParticipantId target,
            Float3 facing = default)
        {
            Float3 resolvedFacing = facing.PlanarLengthSquared > 0.0001f
                ? facing
                : new Float3(0f, 0f, 1f);
            return CombatCommand.Cast(
                actor,
                sequence,
                default,
                resolvedFacing,
                ability,
                target,
                default,
                resolvedFacing);
        }

        private static CombatCommand PointCast(
            ParticipantId actor,
            uint sequence,
            AbilityId ability,
            Float3 point)
        {
            return CombatCommand.Cast(
                actor,
                sequence,
                default,
                new Float3(0f, 0f, 1f),
                ability,
                default,
                point,
                (point - default(Float3)).PlanarNormalized);
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

        private static void TickUntil(CombatSimulation simulation, int targetTick)
        {
            while (simulation.CurrentTick < targetTick)
            {
                simulation.Tick();
            }
        }

        private static bool FindEvent(
            CombatSimulation simulation,
            CombatEventKind kind,
            AbilityId ability)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind && combatEvent.Ability == ability)
                {
                    return true;
                }
            }

            return false;
        }

        private static float FindEventAmount(
            CombatSimulation simulation,
            CombatEventKind kind,
            AbilityId ability)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind && combatEvent.Ability == ability)
                {
                    return combatEvent.Amount;
                }
            }

            Assert.Fail("Expected " + kind + " for " + ability + ".");
            return 0f;
        }

        private static string FindEventReason(
            CombatSimulation simulation,
            CombatEventKind kind,
            AbilityId ability)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind && combatEvent.Ability == ability)
                {
                    return combatEvent.Reason;
                }
            }

            Assert.Fail("Expected " + kind + " for " + ability + ".");
            return null;
        }
    }
}
