using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class ArenaCollisionMapTests
    {
        private static readonly ParticipantId Caster = new ParticipantId(1);
        private static readonly ParticipantId Target = new ParticipantId(2);

        [Test]
        public void CombatRules_EnableAuthoritativeGeometryForArenaAndLeveling()
        {
            Assert.That(CombatRules.ArenaDefault.CollisionMap.IsEnabled, Is.True);
            Assert.That(CombatRules.LevelingDefault.CollisionMap.IsEnabled, Is.True);
            Assert.That(CombatRules.HeroSiegeDefault.CollisionMap.IsEnabled, Is.True);
            Assert.That(
                CombatRules.HeroSiegeDefault.CollisionMap.ObstacleCount,
                Is.EqualTo(ArenaCollisionMap.SiegeObstacleCount));
            Assert.That(
                CombatRules.LevelingDefault.CollisionMap.ObstacleCount,
                Is.Zero);
            Assert.That(CombatRules.BossDefault.CollisionMap.IsEnabled, Is.False);
            Assert.That(
                new CombatRules(30, 5.4f, 15f, 20.5f, 24f)
                    .CollisionMap.IsEnabled,
                Is.False);
        }

        [Test]
        public void SiegeDefault_LeavesCardinalLanesOpenAndBlocksTheCore()
        {
            ArenaCollisionMap map = ArenaCollisionMap.SiegeDefault;
            ArenaCircleObstacle core = map.GetObstacle(0);

            Assert.That(core.Center, Is.EqualTo(default(Float3)));
            Assert.That(core.Radius, Is.EqualTo(ArenaCollisionMap.SiegeCoreRadius));
            Assert.That(map.IsPositionValid(
                new Float3(4.25f, 0f, 0f),
                ArenaLayout.CombatantCollisionRadius), Is.True);
            Assert.That(map.IsPositionValid(
                new Float3(0f, 0f, 4.25f),
                ArenaLayout.CombatantCollisionRadius), Is.True);
            Assert.That(map.IsPositionValid(
                default,
                ArenaLayout.CombatantCollisionRadius), Is.False);

            Float3 stopped = map.ResolveMovement(
                new Float3(4.25f, 0f, 0f),
                new Float3(-8f, 0f, 0f),
                ArenaLayout.CombatantCollisionRadius);
            Assert.That(map.IsPositionValid(
                stopped,
                ArenaLayout.CombatantCollisionRadius), Is.True);
            Assert.That(stopped.X, Is.GreaterThan(0f));
        }

        [Test]
        public void SiegeDefault_ClampsMovementBlinkAndKnockbackToStrongholdBounds()
        {
            ArenaCollisionMap map = ArenaCollisionMap.SiegeDefault;
            float radius = ArenaLayout.CombatantCollisionRadius;
            Float3 origin = new Float3(11.5f, 0f, 0f);

            Float3 moved = map.ResolveMovement(
                origin,
                new Float3(100f, 0f, 30f),
                radius);
            Float3 blinked = map.ResolveBlink(
                origin,
                new Float3(100f, 0f, -100f),
                radius);
            Float3 knocked = map.ResolveKnockback(
                origin,
                new Float3(100f, 0f, 0f),
                radius);

            Assert.That(map.IsPositionValid(moved, radius), Is.True);
            Assert.That(map.IsPositionValid(blinked, radius), Is.True);
            Assert.That(map.IsPositionValid(knocked, radius), Is.True);
            float expectedMaximum = ArenaCollisionMap.SiegeMaximumRadius -
                radius - 0.0025f;
            Assert.That(moved.PlanarLength,
                Is.EqualTo(expectedMaximum).Within(0.0001f));
            Assert.That(blinked.PlanarLength,
                Is.EqualTo(expectedMaximum).Within(0.0001f));
            Assert.That(knocked.PlanarLength,
                Is.EqualTo(expectedMaximum).Within(0.0001f));
        }

        [Test]
        public void LevelingDefault_ClampsMovementBlinkAndKnockbackToCausewayLane()
        {
            ArenaCollisionMap map = ArenaCollisionMap.LevelingDefault;
            float radius = ArenaLayout.CombatantCollisionRadius;
            Float3 origin = new Float3(
                -182f,
                0f,
                ArenaCollisionMap.LevelingCenterZ);

            Float3 moved = map.ResolveMovement(
                origin,
                new Float3(800f, 0f, 400f),
                radius);
            Float3 blinked = map.ResolveBlink(
                origin,
                new Float3(-800f, 0f, -400f),
                radius);
            Float3 knocked = map.ResolveKnockback(
                origin,
                new Float3(0f, 0f, 400f),
                radius);

            Assert.That(map.IsPositionValid(moved, radius), Is.True);
            Assert.That(map.IsPositionValid(blinked, radius), Is.True);
            Assert.That(map.IsPositionValid(knocked, radius), Is.True);
            Assert.That(
                moved.X,
                Is.EqualTo(ArenaCollisionMap.LevelingMaximumX - radius - 0.0025f)
                    .Within(0.0001f));
            Assert.That(
                moved.Z,
                Is.EqualTo(
                    ArenaCollisionMap.LevelingCenterZ +
                    ArenaCollisionMap.LevelingHalfWidth - radius - 0.0025f)
                    .Within(0.0001f));
            Assert.That(
                blinked.X,
                Is.EqualTo(ArenaCollisionMap.LevelingMinimumX + radius + 0.0025f)
                    .Within(0.0001f));
            Assert.That(
                knocked.Z,
                Is.EqualTo(
                    ArenaCollisionMap.LevelingCenterZ +
                    ArenaCollisionMap.LevelingHalfWidth - radius - 0.0025f)
                    .Within(0.0001f));
            Assert.That(map.HasLineOfSight(origin, moved), Is.True);
        }

        [Test]
        public void ArenaDefault_ExposesTwoMirroredPillars()
        {
            ArenaCollisionMap map = ArenaCollisionMap.ArenaDefault;

            Assert.That(map.ObstacleCount, Is.EqualTo(2));
            ArenaCircleObstacle lower = map.GetObstacle(0);
            ArenaCircleObstacle upper = map.GetObstacle(1);
            Assert.That(lower.Center.X, Is.Zero);
            Assert.That(lower.Center.Z, Is.EqualTo(-ArenaLayout.PillarOffsetZ));
            Assert.That(upper.Center.X, Is.Zero);
            Assert.That(upper.Center.Z, Is.EqualTo(ArenaLayout.PillarOffsetZ));
            Assert.That(lower.Radius, Is.EqualTo(ArenaLayout.PillarRadius));
            Assert.That(upper.Radius, Is.EqualTo(ArenaLayout.PillarRadius));
        }

        [Test]
        public void LineOfSight_IsSymmetricAndTreatsTangencyAsBlocked()
        {
            ArenaCollisionMap map = ArenaCollisionMap.ArenaDefault;
            Float3 center = map.GetObstacle(0).Center;
            Float3 left = center + new Float3(-5f, 0f, 0f);
            Float3 right = center + new Float3(5f, 0f, 0f);
            Float3 clearLeft = left + new Float3(0f, 0f, ArenaLayout.PillarRadius + 0.05f);
            Float3 clearRight = right + new Float3(0f, 0f, ArenaLayout.PillarRadius + 0.05f);
            Float3 tangentLeft = left + new Float3(0f, 0f, ArenaLayout.PillarRadius);
            Float3 tangentRight = right + new Float3(0f, 0f, ArenaLayout.PillarRadius);

            Assert.That(map.HasLineOfSight(left, right), Is.False);
            Assert.That(map.HasLineOfSight(right, left), Is.False);
            Assert.That(map.HasLineOfSight(clearLeft, clearRight), Is.True);
            Assert.That(map.HasLineOfSight(tangentLeft, tangentRight), Is.False);
            Assert.That(ArenaCollisionMap.None.HasLineOfSight(left, right), Is.True);
        }

        [Test]
        public void MovementSlidesButKnockbackAndBlinkStopAtPillar()
        {
            ArenaCollisionMap map = ArenaCollisionMap.ArenaDefault;
            Float3 center = map.GetObstacle(0).Center;
            Float3 origin = center + new Float3(-4f, 0f, 0f);
            float clearance = ArenaLayout.PillarRadius +
                ArenaLayout.CombatantCollisionRadius;

            Float3 movement = map.ResolveMovement(
                origin,
                new Float3(8f, 0f, 2f),
                ArenaLayout.CombatantCollisionRadius);
            Float3 knockback = map.ResolveKnockback(
                origin,
                new Float3(8f, 0f, 0f),
                ArenaLayout.CombatantCollisionRadius);
            Float3 blink = map.ResolveBlink(
                origin,
                center + new Float3(4f, 0f, 0f),
                ArenaLayout.CombatantCollisionRadius);

            Assert.That(map.IsPositionValid(
                movement,
                ArenaLayout.CombatantCollisionRadius), Is.True);
            Assert.That(movement.Z, Is.GreaterThan(origin.Z));
            Assert.That((knockback - center).PlanarLength, Is.GreaterThanOrEqualTo(clearance));
            Assert.That((blink - center).PlanarLength, Is.GreaterThanOrEqualTo(clearance));
            Assert.That(knockback.X, Is.LessThan(center.X));
            Assert.That(blink.X, Is.LessThan(center.X));
        }

        [Test]
        public void ShadowstepFallsBackToAValidSideWhenBehindTargetIsBlocked()
        {
            ArenaCollisionMap map = ArenaCollisionMap.ArenaDefault;
            Float3 center = map.GetObstacle(0).Center;
            Float3 targetPosition = center + new Float3(-2.3f, 0f, 0f);

            bool resolved = map.TryResolveShadowstep(
                targetPosition,
                new Float3(-1f, 0f, 0f),
                1.25f,
                ArenaLayout.CombatantCollisionRadius,
                out Float3 position);

            Assert.That(resolved, Is.True);
            Assert.That(map.IsPositionValid(
                position,
                ArenaLayout.CombatantCollisionRadius), Is.True);
            Assert.That(position, Is.Not.EqualTo(center + new Float3(-1.05f, 0f, 0f)));
        }

        [Test]
        public void UnitAbility_IsRejectedWhenPillarBlocksLineOfSight()
        {
            CombatSimulation simulation = CreateCoveredDuel(
                SpecializationId.Frost,
                new Float3(-ArenaLayout.TeamSpawnOffset, 0f, -ArenaLayout.PillarOffsetZ),
                new Float3(ArenaLayout.TeamSpawnOffset, 0f, -ArenaLayout.PillarOffsetZ));
            CombatCommand command = TargetCast(AbilityId.Frostbolt);

            Assert.That(simulation.CanStartAbility(command, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("line_of_sight"));

            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();
            CombatEvent rejection = FindEvent(simulation, CombatEventKind.CastRejected);
            Assert.That(rejection.Reason, Is.EqualTo("line_of_sight"));
            Assert.That(Snapshot(simulation, Target).Health, Is.EqualTo(100f));
        }

        [TestCase(
            AbilityId.FreezingArrow,
            ClassId.Hunter,
            SpecializationId.Marksmanship)]
        [TestCase(
            AbilityId.MassDispel,
            ClassId.Priest,
            SpecializationId.Shadow)]
        public void GroundTargetAbility_IsRejectedWhenWardstoneBlocksTargetPoint(
            AbilityId ability,
            ClassId classId,
            SpecializationId specialization)
        {
            Float3 wardstone = ArenaCollisionMap.ArenaDefault.GetObstacle(0).Center;
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(simulation.AddCombatant(
                Caster,
                new TeamId(1),
                classId,
                specialization,
                wardstone + new Float3(-5f, 0f, 0f)), Is.True);
            CombatCommand command = PointCast(
                ability,
                wardstone + new Float3(5f, 0f, 0f));

            Assert.That(simulation.CanStartAbility(command, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("line_of_sight"));

            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();
            CombatEvent rejection = FindEvent(simulation, CombatEventKind.CastRejected);
            Assert.That(rejection.Ability, Is.EqualTo(ability));
            Assert.That(rejection.Reason, Is.EqualTo("line_of_sight"));
        }

        [Test]
        public void CastCompleted_ReportsClampedResolvedTargetPoint()
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(simulation.AddCombatant(
                Caster,
                new TeamId(1),
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                default), Is.True);
            Float3 requestedPoint = new Float3(30f, 0f, 0f);

            Assert.That(simulation.SubmitCommand(
                PointCast(AbilityId.FreezingArrow, requestedPoint)), Is.True);
            simulation.Tick();

            Float3 expectedPoint = new Float3(
                AbilityCatalog.Get(AbilityId.FreezingArrow).Targeting.Range,
                0f,
                0f);
            CombatEvent accepted = FindEvent(
                simulation,
                CombatEventKind.CastAccepted);
            Assert.That(accepted.Ability, Is.EqualTo(AbilityId.FreezingArrow));
            Assert.That(
                accepted.Vector,
                Is.EqualTo(expectedPoint),
                "Telegraphs must receive Core's authoritative clamped point.");

            CombatEvent completed = FindEvent(
                simulation,
                CombatEventKind.CastCompleted);
            Assert.That(completed.Ability, Is.EqualTo(AbilityId.FreezingArrow));
            Assert.That(completed.Vector, Is.EqualTo(expectedPoint));
        }

        [Test]
        public void CastThatLosesLineOfSight_CompletesWithoutApplyingEffects()
        {
            CombatSimulation simulation = CreateCoveredDuel(
                SpecializationId.Fire,
                new Float3(-ArenaLayout.TeamSpawnOffset, 0f, -ArenaLayout.PillarOffsetZ),
                new Float3(ArenaLayout.TeamSpawnOffset, 0f, 0f));
            Assert.That(simulation.CanStartAbility(
                TargetCast(AbilityId.Fireball),
                out _), Is.True);

            Assert.That(simulation.SubmitCommand(TargetCast(AbilityId.Fireball)), Is.True);
            Assert.That(simulation.SubmitCommand(CombatCommand.Move(
                Target,
                1,
                new Float3(0f, 0f, -1f),
                new Float3(0f, 0f, -1f))), Is.True);
            simulation.Tick();
            TickUntil(simulation, 25);
            Assert.That(simulation.SubmitCommand(CombatCommand.Move(
                Target,
                2,
                default,
                new Float3(0f, 0f, -1f))), Is.True);
            simulation.Tick();
            TickUntil(simulation, 61);

            Assert.That(Snapshot(simulation, Target).Health, Is.EqualTo(100f));
            Assert.That(FindEvent(simulation, CombatEventKind.CastCompleted).Ability,
                Is.EqualTo(AbilityId.Fireball));
            Assert.That(HasEvent(simulation, CombatEventKind.Damage), Is.False);
        }

        [Test]
        public void ChannelThatLosesLineOfSight_StopsBeforeItsNextPulse()
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(simulation.AddCombatant(
                Caster,
                new TeamId(1),
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(-ArenaLayout.TeamSpawnOffset, 0f, -ArenaLayout.PillarOffsetZ)),
                Is.True);
            Assert.That(simulation.AddCombatant(
                Target,
                new TeamId(2),
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(ArenaLayout.TeamSpawnOffset, 0f, 0f)),
                Is.True);

            Assert.That(simulation.SubmitCommand(TargetCast(AbilityId.Penance)), Is.True);
            Assert.That(simulation.SubmitCommand(CombatCommand.Move(
                Target,
                1,
                new Float3(0f, 0f, -1f),
                new Float3(0f, 0f, -1f))), Is.True);
            simulation.Tick();
            Assert.That(Snapshot(simulation, Target).Health, Is.EqualTo(90f));

            bool wasInterrupted = false;
            CombatEvent interrupted = default;
            while (simulation.CurrentTick < 21)
            {
                simulation.Tick();
                if (TryFindEvent(
                        simulation,
                        CombatEventKind.Interrupted,
                        out CombatEvent currentInterrupted))
                {
                    wasInterrupted = true;
                    interrupted = currentInterrupted;
                }
            }

            Assert.That(Snapshot(simulation, Target).Health, Is.EqualTo(90f));
            Assert.That(wasInterrupted, Is.True);
            Assert.That(interrupted.Ability, Is.EqualTo(AbilityId.Penance));
            Assert.That(interrupted.Reason, Is.EqualTo("line_of_sight"));
        }

        [Test]
        public void BlastWaveAndBlink_CannotCrossPillar()
        {
            Float3 laneCenter = ArenaCollisionMap.ArenaDefault.GetObstacle(0).Center;
            var blast = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(blast.AddCombatant(
                Caster,
                new TeamId(1),
                ClassId.Mage,
                SpecializationId.Fire,
                laneCenter + new Float3(-6f, 0f, 0f)), Is.True);
            Assert.That(blast.AddCombatant(
                Target,
                new TeamId(2),
                ClassId.Priest,
                SpecializationId.Discipline,
                laneCenter + new Float3(-4f, 0f, 0f)), Is.True);

            Assert.That(blast.SubmitCommand(CombatCommand.Cast(
                Caster,
                1,
                default,
                new Float3(1f, 0f, 0f),
                AbilityId.BlastWave,
                default,
                default,
                new Float3(1f, 0f, 0f))), Is.True);
            blast.Tick();
            CombatantSnapshot pushed = Snapshot(blast, Target);
            Assert.That(pushed.Position.X, Is.LessThan(laneCenter.X));
            Assert.That(CombatRules.ArenaDefault.CollisionMap.IsPositionValid(
                pushed.Position,
                ArenaLayout.CombatantCollisionRadius), Is.True);

            var blink = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(blink.AddCombatant(
                Caster,
                new TeamId(1),
                ClassId.Mage,
                SpecializationId.Fire,
                laneCenter + new Float3(-6f, 0f, 0f)), Is.True);
            Assert.That(blink.SubmitCommand(CombatCommand.Cast(
                Caster,
                1,
                default,
                new Float3(1f, 0f, 0f),
                AbilityId.Blink,
                default,
                laneCenter + new Float3(6f, 0f, 0f),
                new Float3(1f, 0f, 0f))), Is.True);
            blink.Tick();
            CombatantSnapshot teleported = Snapshot(blink, Caster);
            Assert.That(teleported.Position.X, Is.LessThan(laneCenter.X));
            Assert.That(CombatRules.ArenaDefault.CollisionMap.IsPositionValid(
                teleported.Position,
                ArenaLayout.CombatantCollisionRadius), Is.True);
        }

        private static CombatSimulation CreateCoveredDuel(
            SpecializationId casterSpecialization,
            Float3 casterPosition,
            Float3 targetPosition)
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(simulation.AddCombatant(
                Caster,
                new TeamId(1),
                ClassId.Mage,
                casterSpecialization,
                casterPosition), Is.True);
            Assert.That(simulation.AddCombatant(
                Target,
                new TeamId(2),
                ClassId.Priest,
                SpecializationId.Discipline,
                targetPosition), Is.True);
            return simulation;
        }

        private static CombatCommand TargetCast(AbilityId ability)
        {
            return CombatCommand.Cast(
                Caster,
                1,
                default,
                new Float3(1f, 0f, 0f),
                ability,
                Target,
                default,
                new Float3(1f, 0f, 0f));
        }

        private static CombatCommand PointCast(AbilityId ability, Float3 point)
        {
            return CombatCommand.Cast(
                Caster,
                1,
                default,
                (point - default(Float3)).PlanarNormalized,
                ability,
                default,
                point,
                (point - default(Float3)).PlanarNormalized);
        }

        private static CombatantSnapshot Snapshot(
            CombatSimulation simulation,
            ParticipantId participant)
        {
            Assert.That(simulation.TryGetSnapshot(
                participant,
                out CombatantSnapshot snapshot), Is.True);
            return snapshot;
        }

        private static void TickUntil(CombatSimulation simulation, int tick)
        {
            while (simulation.CurrentTick < tick)
            {
                simulation.Tick();
            }
        }

        private static CombatEvent FindEvent(
            CombatSimulation simulation,
            CombatEventKind kind)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind)
                {
                    return combatEvent;
                }
            }

            Assert.Fail($"Expected {kind} event at tick {simulation.CurrentTick}.");
            return default;
        }

        private static bool HasEvent(
            CombatSimulation simulation,
            CombatEventKind kind)
        {
            return TryFindEvent(simulation, kind, out _);
        }

        private static bool TryFindEvent(
            CombatSimulation simulation,
            CombatEventKind kind,
            out CombatEvent result)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind)
                {
                    result = combatEvent;
                    return true;
                }
            }

            result = default;
            return false;
        }
    }
}
