using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class DeterministicSiegeBrainTests
    {
        private static readonly ParticipantId Defender = new ParticipantId(1);
        private static readonly ParticipantId Enemy = new ParticipantId(10101);
        private static readonly TeamId FriendlyTeam = new TeamId(1);
        private static readonly TeamId HostileTeam = new TeamId(2);

        [Test]
        public void Raider_UsesCataloguedPrimaryAgainstAssignedLaneDefender()
        {
            CombatSimulation simulation = CreateSimulation(
                SiegeEnemyArchetypeId.AshboundRaider,
                new Float3(4.25f, 0f, 0f),
                new Float3(5.5f, 0f, 0f));
            var snapshots = new CombatantSnapshot[CombatSimulation.MaxCombatants];
            int count = simulation.CopySnapshots(snapshots);
            var brain = new DeterministicSiegeEnemyBrain(
                SiegeEnemyArchetypeId.AshboundRaider,
                Defender,
                SiegeLaneId.East);

            Assert.That(brain.TryBuildCommand(
                simulation, Snapshot(simulation, Enemy), snapshots, count,
                out CombatCommand command), Is.True);
            Assert.That(command.Actor, Is.EqualTo(Enemy));
            Assert.That(command.Target, Is.EqualTo(Defender));
            Assert.That(command.Ability, Is.EqualTo(AbilityId.Shiv));
            Assert.That(simulation.CanStartAbility(command, out string rejection),
                Is.True, rejection);
        }

        [Test]
        public void Runner_IgnoresDistantDefenderAndRushesCentralObjective()
        {
            CombatSimulation simulation = CreateSimulation(
                SiegeEnemyArchetypeId.CinderRunner,
                new Float3(8f, 0f, 0f),
                new Float3(11.5f, 0f, 0f));
            var snapshots = new CombatantSnapshot[CombatSimulation.MaxCombatants];
            int count = simulation.CopySnapshots(snapshots);
            var brain = new DeterministicSiegeEnemyBrain(
                SiegeEnemyArchetypeId.CinderRunner,
                Defender,
                SiegeLaneId.East);

            Assert.That(brain.TryBuildCommand(
                simulation, Snapshot(simulation, Enemy), snapshots, count,
                out CombatCommand command), Is.True);
            Assert.That(command.HasAbility, Is.False);
            Assert.That(command.Movement.X, Is.LessThan(0f));
            Assert.That(command.Movement.Z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Warder_ShieldsInjuredFriendlyEnemyBeforeAttacking()
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(simulation.AddCombatant(
                Defender, FriendlyTeam, ClassId.Mage, SpecializationId.Fire,
                new Float3(10f, 0f, 0f), 100f, false), Is.True);
            Assert.That(simulation.AddCombatant(
                Enemy, HostileTeam, ClassId.Priest, SpecializationId.Discipline,
                new Float3(6f, 0f, 0f), 125f, false), Is.True);
            var injuredAlly = new ParticipantId(10102);
            Assert.That(simulation.AddCombatant(
                injuredAlly, HostileTeam, ClassId.Rogue,
                SpecializationId.Mutilate, new Float3(7f, 0f, 0f),
                40f, false), Is.True);
            DealDamage(simulation, Defender, injuredAlly);

            var snapshots = new CombatantSnapshot[CombatSimulation.MaxCombatants];
            int count = simulation.CopySnapshots(snapshots);
            var brain = new DeterministicSiegeEnemyBrain(
                SiegeEnemyArchetypeId.ReliquaryWarder,
                Defender,
                SiegeLaneId.East);
            Assert.That(brain.TryBuildCommand(
                simulation, Snapshot(simulation, Enemy), snapshots, count,
                out CombatCommand command), Is.True);
            Assert.That(command.Ability, Is.EqualTo(AbilityId.PowerWordShield));
            Assert.That(command.Target, Is.EqualTo(injuredAlly));
        }

        [Test]
        public void Raider_ResumesObjectivePressureWhenDefenderLeavesItsLane()
        {
            CombatSimulation simulation = CreateSimulation(
                SiegeEnemyArchetypeId.AshboundRaider,
                new Float3(0f, 0f, 6f),
                new Float3(8f, 0f, 0f));
            var snapshots = new CombatantSnapshot[CombatSimulation.MaxCombatants];
            int count = simulation.CopySnapshots(snapshots);
            var brain = new DeterministicSiegeEnemyBrain(
                SiegeEnemyArchetypeId.AshboundRaider,
                Defender,
                SiegeLaneId.East);

            Assert.That(brain.TryBuildCommand(
                simulation, Snapshot(simulation, Enemy), snapshots, count,
                out CombatCommand command), Is.True);
            Assert.That(command.HasAbility, Is.False);
            Assert.That(command.Movement.X, Is.LessThan(0f));
            Assert.That(command.Movement.Z, Is.EqualTo(0f).Within(0.001f));
        }

        [TestCase(SiegeLaneId.East)]
        [TestCase(SiegeLaneId.North)]
        [TestCase(SiegeLaneId.West)]
        [TestCase(SiegeLaneId.South)]
        public void FourLaneDefender_ReturnsToItsAuthoredAnchorAfterDrift(
            SiegeLaneId lane)
        {
            var controller = new HeroSiegeRunController(
                new RunId(1u),
                SiegeVariantId.FourLanes);
            var simulation = new CombatSimulation(CombatRules.HeroSiegeDefault);
            Float3 anchor = controller.Variant.GetDefenderAnchor(lane);
            Float3 drifted = lane == SiegeLaneId.East ||
                lane == SiegeLaneId.West
                    ? anchor + new Float3(0f, 0f, 9f)
                    : anchor + new Float3(9f, 0f, 0f);
            ParticipantId defender = controller.Variant.GetDefender(lane);
            Assert.That(simulation.AddCombatant(
                defender,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                drifted,
                100f,
                false), Is.True);
            var brain = new DeterministicSiegeDefenderBrain(
                lane,
                731);
            var snapshots = new CombatantSnapshot[CombatSimulation.MaxCombatants];

            for (int tick = 0; tick < 180; tick++)
            {
                int count = simulation.CopySnapshots(snapshots);
                CombatantSnapshot self = Snapshot(simulation, defender);
                if (brain.TryBuildCommand(
                        simulation,
                        self,
                        controller,
                        snapshots,
                        count,
                        out CombatCommand command))
                {
                    Assert.That(simulation.SubmitCommand(command), Is.True);
                }

                simulation.Tick();
            }

            CombatantSnapshot returned = Snapshot(simulation, defender);
            Assert.That(
                Float3.PlanarDistance(returned.Position, anchor),
                Is.LessThanOrEqualTo(1.5f));
        }

        [Test]
        public void ObjectiveRunner_ReachesAValidPressurePositionAndDamagesTheCore()
        {
            var controller = new HeroSiegeRunController(new RunId(23u));
            controller.Advance(HeroSiegeRunCatalog.InitialPreparationTicks);
            SiegeSpawnDefinition runner = default;
            while (controller.TryDequeueSpawn(out SiegeSpawnDefinition spawn))
            {
                if (spawn.Archetype.Id == SiegeEnemyArchetypeId.CinderRunner)
                {
                    runner = spawn;
                }
            }

            Assert.That(runner.Id.IsValid, Is.True);
            var simulation = new CombatSimulation(CombatRules.HeroSiegeDefault);
            Assert.That(simulation.AddCombatant(
                runner.Participant,
                HeroSiegeRunCatalog.HostileTeam,
                runner.Archetype.Class,
                runner.Archetype.Specialization,
                runner.Position,
                runner.MaximumHealth,
                false), Is.True);
            Assert.That(simulation.TrySetRunCombatModifiers(
                runner.Participant,
                runner.DamageMultiplierPermille,
                runner.MovementSpeedMultiplierPermille), Is.True);
            var brain = new DeterministicSiegeEnemyBrain(
                runner.Archetype.Id,
                controller.GetDefender(0),
                runner.Lane);
            var snapshots = new CombatantSnapshot[CombatSimulation.MaxCombatants];

            for (int tick = 0; tick < 300; tick++)
            {
                int count = simulation.CopySnapshots(snapshots);
                CombatantSnapshot self = Snapshot(simulation, runner.Participant);
                if (brain.TryBuildCommand(
                        simulation,
                        self,
                        snapshots,
                        count,
                        out CombatCommand command))
                {
                    Assert.That(simulation.SubmitCommand(command), Is.True);
                }

                simulation.Tick();
            }

            CombatantSnapshot arrived = Snapshot(simulation, runner.Participant);
            Assert.That(
                ArenaCollisionMap.SiegeDefault.IsPositionValid(
                    arrived.Position,
                    ArenaLayout.CombatantCollisionRadius),
                Is.True);
            Assert.That(arrived.Position.PlanarLength,
                Is.LessThanOrEqualTo(
                    HeroSiegeRunCatalog.ObjectivePressureRadius + 0.001f));
            int snapshotCount = simulation.CopySnapshots(snapshots);
            SiegeObjectivePressureObservation pressure =
                controller.ApplyObjectivePressure(
                    HeroSiegeRunCatalog.InitialPreparationTicks +
                    simulation.CurrentTick,
                    snapshots,
                    snapshotCount,
                    simulation.Rules.TickRate);
            Assert.That(pressure.PressureHostileCount, Is.EqualTo(1));
            Assert.That(pressure.AppliedDamage, Is.GreaterThan(0f));
        }

        private static CombatSimulation CreateSimulation(
            SiegeEnemyArchetypeId archetypeId,
            Float3 defenderPosition,
            Float3 enemyPosition)
        {
            SiegeEnemyArchetypeDefinition archetype =
                SiegeEnemyArchetypeCatalog.Get(archetypeId);
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(simulation.AddCombatant(
                Defender, FriendlyTeam, ClassId.Mage, SpecializationId.Fire,
                defenderPosition, 100f, false), Is.True);
            Assert.That(simulation.AddCombatant(
                Enemy, HostileTeam, archetype.Class, archetype.Specialization,
                enemyPosition, archetype.BaseMaximumHealth, false), Is.True);
            return simulation;
        }

        private static CombatantSnapshot Snapshot(
            CombatSimulation simulation,
            ParticipantId participant)
        {
            Assert.That(simulation.TryGetSnapshot(
                participant, out CombatantSnapshot snapshot), Is.True);
            return snapshot;
        }

        private static void DealDamage(
            CombatSimulation simulation,
            ParticipantId actor,
            ParticipantId target)
        {
            CombatantSnapshot source = Snapshot(simulation, actor);
            CombatantSnapshot destination = Snapshot(simulation, target);
            Assert.That(simulation.SubmitCommand(CombatCommand.Cast(
                actor, 1u, default,
                (destination.Position - source.Position).PlanarNormalized,
                AbilityId.FireBlast, target, destination.Position,
                (destination.Position - source.Position).PlanarNormalized)), Is.True);
            simulation.Tick();
        }
    }
}
