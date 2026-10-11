using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class CombatSimulationTests
    {
        private static readonly ParticipantId Healer = new ParticipantId(1);
        private static readonly ParticipantId Rogue = new ParticipantId(2);
        private static readonly ParticipantId Mage = new ParticipantId(3);

        [Test]
        public void ArenaLayout_PreservesOpeningRangesAndLavaRecoverySpace()
        {
            CombatRules rules = CombatRules.ArenaDefault;
            float sameTeamDistance = ArenaLayout.PairSpawnOffset * 2f;
            float nearestEnemyDistance = ArenaLayout.TeamSpawnOffset * 2f;
            float diagonalEnemyDistance = (float)Math.Sqrt(
                (nearestEnemyDistance * nearestEnemyDistance) +
                (sameTeamDistance * sameTeamDistance));
            float spawnRadius = (float)Math.Sqrt(
                (ArenaLayout.TeamSpawnOffset * ArenaLayout.TeamSpawnOffset) +
                (ArenaLayout.PairSpawnOffset * ArenaLayout.PairSpawnOffset));

            Assert.That(rules.SafeRadius, Is.EqualTo(15f));
            Assert.That(rules.OuterKillRadius, Is.EqualTo(20.5f));
            Assert.That(rules.OuterKillRadius - rules.SafeRadius, Is.EqualTo(5.5f));
            Assert.That(spawnRadius, Is.LessThan(rules.SafeRadius * 0.5f));
            Assert.That(sameTeamDistance, Is.LessThan(15f));
            Assert.That(nearestEnemyDistance, Is.LessThan(15f));
            Assert.That(diagonalEnemyDistance, Is.LessThan(15f));
        }

        [Test]
        public void SpecializationCatalog_HasTenAuthorizedPrimariesForAllSevenSpecs()
        {
            Assert.That((int)ClassId.Healer, Is.EqualTo(1));
            Assert.That((int)ClassId.Rogue, Is.EqualTo(2));
            Assert.That((int)ClassId.Mage, Is.EqualTo(3));
            Assert.That((int)ClassId.Hunter, Is.EqualTo(4));

            var seen = new HashSet<AbilityId>();
            SpecializationId[] specializations =
            {
                SpecializationId.Discipline,
                SpecializationId.Fire,
                SpecializationId.Frost,
                SpecializationId.ShadowDance,
                SpecializationId.Mutilate,
                SpecializationId.Shadow,
                SpecializationId.Marksmanship,
            };
            int totalSlots = 0;
            for (int specializationIndex = 0;
                 specializationIndex < specializations.Length;
                 specializationIndex++)
            {
                SpecializationId specialization = specializations[specializationIndex];
                SpecializationDefinition specializationDefinition =
                    SpecializationCatalog.GetDefinition(specialization);
                for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
                {
                    AbilityId ability = AbilityCatalog.GetSpecializationAbility(
                        specialization,
                        slot);
                    AbilityDefinition definition = AbilityCatalog.Get(ability);
                    Assert.That(ability, Is.Not.EqualTo(AbilityId.None));
                    Assert.That(definition.Id, Is.EqualTo(ability));
                    Assert.That(
                        definition.OwnerClass == ClassId.None ||
                        definition.OwnerClass == specializationDefinition.OwnerClass,
                        Is.True);
                    Assert.That(
                        AbilityCatalog.IsAbilityAllowed(
                            specializationDefinition.OwnerClass,
                            specialization,
                            ability),
                        Is.True);
                    Assert.That(string.IsNullOrWhiteSpace(definition.ShortPurpose), Is.False);
                    Assert.That(string.IsNullOrWhiteSpace(definition.Description), Is.False);
                    seen.Add(ability);
                    totalSlots++;
                }
            }

            Assert.That(totalSlots, Is.EqualTo(70));
            Assert.That(seen, Has.Count.EqualTo(57));
            Assert.That(
                AbilityCatalog.Get(AbilityId.PainSuppression).Description,
                Does.Contain("40%"));
        }

        [Test]
        public void DamageAndMend_ApplyAuthoritativeAmountsAndHealingCapsAtMaximum()
        {
            CombatSimulation simulation = CreateStandardDuel();
            simulation.SubmitCommand(TargetCast(Rogue, 1, AbilityId.QuickStrike, Healer));
            simulation.Tick();

            CombatantSnapshot damaged = Snapshot(simulation, Healer);
            Assert.That(damaged.Health, Is.EqualTo(86f).Within(0.0001f));
            AssertEvent(simulation, CombatEventKind.Damage, AbilityId.QuickStrike, 14f);

            simulation.SubmitCommand(TargetCast(Healer, 1, AbilityId.Mend, Healer));
            simulation.Tick();
            int mendEndTick = Snapshot(simulation, Healer).CastEndTick;
            Assert.That(mendEndTick, Is.EqualTo(19));
            TickUntil(simulation, mendEndTick);

            CombatantSnapshot healed = Snapshot(simulation, Healer);
            Assert.That(healed.Health, Is.EqualTo(healed.MaxHealth).Within(0.0001f));
            AssertEvent(simulation, CombatEventKind.Heal, AbilityId.Mend, 14f);
        }

        [Test]
        public void Cooldown_RejectsEarlyRepeatAndAcceptsAtReadyTick()
        {
            CombatSimulation simulation = CreateStandardDuel();
            simulation.SubmitCommand(TargetCast(Rogue, 1, AbilityId.QuickStrike, Healer));
            simulation.Tick();

            simulation.SubmitCommand(TargetCast(Rogue, 2, AbilityId.QuickStrike, Healer));
            simulation.Tick();
            CombatEvent rejection = FindEvent(simulation, CombatEventKind.CastRejected);
            Assert.That(rejection.Ability, Is.EqualTo(AbilityId.QuickStrike));
            Assert.That(rejection.Reason, Is.EqualTo("cooldown"));
            Assert.That(simulation.GetCooldownRemaining(Rogue, AbilityId.QuickStrike), Is.GreaterThan(0f));

            TickUntil(simulation, 23);
            simulation.SubmitCommand(TargetCast(Rogue, 3, AbilityId.QuickStrike, Healer));
            simulation.Tick();
            Assert.That(simulation.CurrentTick, Is.EqualTo(24));
            Assert.That(FindEvent(simulation, CombatEventKind.CastAccepted).Ability,
                Is.EqualTo(AbilityId.QuickStrike));
        }

        [Test]
        public void CommandOrdering_IsIndependentOfSubmissionOrderIncludingSequenceTies()
        {
            CombatSimulation forward = CreateOrderingSimulation();
            CombatSimulation reverse = CreateOrderingSimulation();
            CombatCommand quick = TargetCast(Rogue, 1, AbilityId.QuickStrike, Mage);
            CombatCommand ambush = TargetCast(Rogue, 1, AbilityId.Ambush, Mage);
            CombatCommand healerBolt = TargetCast(Healer, 1, AbilityId.HealerBolt, Mage);

            forward.SubmitCommand(ambush);
            forward.SubmitCommand(healerBolt);
            forward.SubmitCommand(quick);
            reverse.SubmitCommand(quick);
            reverse.SubmitCommand(healerBolt);
            reverse.SubmitCommand(ambush);
            forward.Tick();
            reverse.Tick();

            AssertSimulationsEqual(forward, reverse);
            Assert.That(Snapshot(forward, Mage).Health, Is.EqualTo(77f).Within(0.0001f));
            Assert.That(FindEvent(forward, CombatEventKind.CastRejected).Ability,
                Is.EqualTo(AbilityId.Ambush));
            Assert.That(FindEvent(forward, CombatEventKind.CastRejected).Reason,
                Is.EqualTo("stale_sequence"));
        }

        [Test]
        public void Gust_CompletesOnFixedTickAndAppliesExactKnockback()
        {
            CombatSimulation simulation = CreateMageAndRogue(anchorFirst: false);
            simulation.SubmitCommand(TargetCast(Mage, 1, AbilityId.Gust, Rogue));
            simulation.Tick();

            CombatantSnapshot casting = Snapshot(simulation, Mage);
            Assert.That(casting.CastingAbility, Is.EqualTo(AbilityId.Gust));
            Assert.That(casting.CastEndTick, Is.EqualTo(12));
            Assert.That(Snapshot(simulation, Rogue).Position.X, Is.EqualTo(2f));

            TickUntil(simulation, 12);
            CombatantSnapshot target = Snapshot(simulation, Rogue);
            Assert.That(target.Position.X, Is.EqualTo(7.5f).Within(0.0001f));
            Assert.That(target.Position.Z, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(target.Health, Is.EqualTo(92f).Within(0.0001f));
            CombatEvent knockback = FindEvent(simulation, CombatEventKind.Knockback);
            Assert.That(knockback.Amount, Is.EqualTo(5.5f).Within(0.0001f));
            Assert.That(knockback.Vector.X, Is.EqualTo(5.5f).Within(0.0001f));
        }

        [Test]
        public void Anchor_PreventsGustKnockbackForItsFullActiveWindow()
        {
            CombatSimulation simulation = CreateMageAndRogue(anchorFirst: true);
            simulation.SubmitCommand(TargetCast(Rogue, 1, AbilityId.Anchor, default));
            simulation.SubmitCommand(TargetCast(Mage, 1, AbilityId.Gust, Rogue));
            simulation.Tick();
            TickUntil(simulation, 12);

            CombatantSnapshot target = Snapshot(simulation, Rogue);
            Assert.That(target.Position.X, Is.EqualTo(2f).Within(0.0001f));
            Assert.That(target.Health, Is.EqualTo(100f).Within(0.0001f));
            Assert.That(target.Barrier, Is.EqualTo(4f).Within(0.0001f));
            Assert.That(HasEvent(simulation, CombatEventKind.Knockback), Is.False);
        }

        [Test]
        public void Lava_AppliesFixedDamageAndProducesAuthoritativeDeath()
        {
            var rules = new CombatRules(10, 0f, 5f, 20f, 50f);
            var simulation = new CombatSimulation(rules);
            Assert.That(simulation.AddCombatant(
                Healer,
                new TeamId(1),
                ClassId.Healer,
                new Float3(6f, 0f, 0f),
                10f), Is.True);

            simulation.Tick();
            CombatantSnapshot burning = Snapshot(simulation, Healer);
            Assert.That(burning.IsInLava, Is.True);
            Assert.That(burning.Health, Is.EqualTo(5f).Within(0.0001f));
            AssertEvent(simulation, CombatEventKind.HazardDamage, AbilityId.None, 5f);

            simulation.Tick();
            CombatantSnapshot dead = Snapshot(simulation, Healer);
            Assert.That(dead.Health, Is.Zero);
            Assert.That(dead.IsAlive, Is.False);
            CombatEvent death = FindEvent(simulation, CombatEventKind.Death);
            Assert.That(death.Target, Is.EqualTo(Healer));
            Assert.That(death.Reason, Is.EqualTo("lava"));
        }

        [Test]
        public void CastTiming_UsesCeilingTickConversionAndDoesNotResolveEarly()
        {
            CombatSimulation simulation = CreateMageAndRogue(anchorFirst: false);
            simulation.SubmitCommand(TargetCast(Mage, 1, AbilityId.ArcaneBolt, Rogue));
            simulation.Tick();

            CombatantSnapshot caster = Snapshot(simulation, Mage);
            Assert.That(caster.CastEndTick, Is.EqualTo(9));
            Assert.That(Snapshot(simulation, Rogue).Health, Is.EqualTo(100f));

            TickUntil(simulation, 8);
            Assert.That(Snapshot(simulation, Rogue).Health, Is.EqualTo(100f));
            Assert.That(HasEvent(simulation, CombatEventKind.CastCompleted), Is.False);

            simulation.Tick();
            Assert.That(simulation.CurrentTick, Is.EqualTo(9));
            Assert.That(Snapshot(simulation, Rogue).Health, Is.EqualTo(85f).Within(0.0001f));
            Assert.That(FindEvent(simulation, CombatEventKind.CastCompleted).Ability,
                Is.EqualTo(AbilityId.ArcaneBolt));
        }

        [Test]
        public void InstantAbilities_AcceptAtMostOneStartPerActorPerTick()
        {
            CombatSimulation simulation = CreateStandardDuel();
            simulation.SubmitCommand(TargetCast(Rogue, 1, AbilityId.QuickStrike, Healer));
            simulation.SubmitCommand(TargetCast(Rogue, 2, AbilityId.Ambush, Healer));

            simulation.Tick();

            Assert.That(Snapshot(simulation, Healer).Health, Is.EqualTo(86f).Within(0.0001f));
            CombatEvent rejection = FindEvent(simulation, CombatEventKind.CastRejected);
            Assert.That(rejection.Ability, Is.EqualTo(AbilityId.Ambush));
            Assert.That(rejection.Reason, Is.EqualTo("ability_rate"));
        }

        [Test]
        public void Rescue_RejectsSelfAndPullsAnotherLivingAllyTowardHealer()
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            simulation.AddCombatant(Healer, new TeamId(1), ClassId.Healer, default);
            simulation.AddCombatant(Rogue, new TeamId(1), ClassId.Rogue, new Float3(6f, 0f, 0f));

            simulation.SubmitCommand(TargetCast(Healer, 1, AbilityId.Rescue, Healer));
            simulation.Tick();
            CombatEvent rejection = FindEvent(simulation, CombatEventKind.CastRejected);
            Assert.That(rejection.Ability, Is.EqualTo(AbilityId.Rescue));
            Assert.That(rejection.Reason, Is.EqualTo("invalid_target"));
            Assert.That(simulation.GetCooldownRemaining(Healer, AbilityId.Rescue), Is.Zero);

            simulation.SubmitCommand(TargetCast(Healer, 2, AbilityId.Rescue, Rogue));
            simulation.Tick();
            int castEndTick = Snapshot(simulation, Healer).CastEndTick;
            TickUntil(simulation, castEndTick);

            Assert.That(Snapshot(simulation, Rogue).Position.X, Is.EqualTo(1.25f).Within(0.0001f));
            CombatEvent knockback = FindEvent(simulation, CombatEventKind.Knockback);
            Assert.That(knockback.Actor, Is.EqualTo(Healer));
            Assert.That(knockback.Target, Is.EqualTo(Rogue));
            Assert.That(knockback.Ability, Is.EqualTo(AbilityId.Rescue));
            Assert.That(knockback.Amount, Is.EqualTo(4.75f).Within(0.0001f));
        }

        [Test]
        public void Rescue_NeverPullsNearbyAllyThroughTheHealer()
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            simulation.AddCombatant(Healer, new TeamId(1), ClassId.Healer, default);
            simulation.AddCombatant(Rogue, new TeamId(1), ClassId.Rogue, new Float3(3f, 0f, 0f));
            simulation.SubmitCommand(TargetCast(Healer, 1, AbilityId.Rescue, Rogue));
            simulation.Tick();
            TickUntil(simulation, Snapshot(simulation, Healer).CastEndTick);

            CombatantSnapshot rescued = Snapshot(simulation, Rogue);
            Assert.That(rescued.Position.X, Is.EqualTo(1.25f).Within(0.0001f));
            Assert.That(rescued.Position.Z, Is.Zero.Within(0.0001f));
            CombatEvent knockback = FindEvent(simulation, CombatEventKind.Knockback);
            Assert.That(knockback.Vector.X, Is.EqualTo(-1.75f).Within(0.0001f));
            Assert.That(knockback.Amount, Is.EqualTo(1.75f).Within(0.0001f));
        }

        [Test]
        public void Rescue_AtZeroPlanarSeparationNeverCreatesArbitraryMovement()
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            simulation.AddCombatant(Healer, new TeamId(1), ClassId.Healer, default);
            simulation.AddCombatant(Rogue, new TeamId(1), ClassId.Rogue, default);
            simulation.SubmitCommand(TargetCast(Healer, 1, AbilityId.Rescue, Rogue));
            simulation.Tick();
            TickUntil(simulation, Snapshot(simulation, Healer).CastEndTick);

            Assert.That(Snapshot(simulation, Rogue).Position, Is.EqualTo(default(Float3)));
            CombatEvent knockback = FindEvent(simulation, CombatEventKind.Knockback);
            Assert.That(knockback.Vector, Is.EqualTo(default(Float3)));
            Assert.That(knockback.Amount, Is.Zero);
        }

        [Test]
        public void Barriers_KeepMaximumAmountAndExpiryAndCreditActualSource()
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            simulation.AddCombatant(Healer, new TeamId(1), ClassId.Healer, default);
            simulation.AddCombatant(Rogue, new TeamId(1), ClassId.Rogue, new Float3(1f, 0f, 0f));
            simulation.SubmitCommand(TargetCast(Healer, 1, AbilityId.Aegis, Rogue));
            simulation.SubmitCommand(TargetCast(Rogue, 1, AbilityId.Anchor, default));

            simulation.Tick();

            CombatantSnapshot protectedRogue = Snapshot(simulation, Rogue);
            Assert.That(protectedRogue.Barrier, Is.EqualTo(24f).Within(0.0001f));
            Assert.That(protectedRogue.HasStatus(StatusId.Barrier), Is.True);
            Assert.That(protectedRogue.HasStatus(StatusId.Anchored), Is.True);
            CombatEvent applied = FindEvent(
                simulation,
                CombatEventKind.BarrierApplied,
                AbilityId.Aegis);
            Assert.That(applied.Actor, Is.EqualTo(Healer));
            Assert.That(applied.Target, Is.EqualTo(Rogue));

            TickUntil(simulation, 76);
            protectedRogue = Snapshot(simulation, Rogue);
            Assert.That(protectedRogue.Barrier, Is.EqualTo(24f).Within(0.0001f));
            Assert.That(protectedRogue.HasStatus(StatusId.Barrier), Is.True);
            Assert.That(protectedRogue.HasStatus(StatusId.Anchored), Is.False);

            TickUntil(simulation, 121);
            protectedRogue = Snapshot(simulation, Rogue);
            Assert.That(protectedRogue.Barrier, Is.Zero);
            Assert.That(protectedRogue.HasStatus(StatusId.Barrier), Is.False);
        }

        [Test]
        public void Cleanse_RemovesVisibleHarmfulStatusAndPreservesStableAbilityId()
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            simulation.AddCombatant(Healer, new TeamId(1), ClassId.Healer, default);
            simulation.AddCombatant(Rogue, new TeamId(1), ClassId.Rogue, new Float3(1f, 0f, 0f));
            simulation.AddCombatant(Mage, new TeamId(2), ClassId.Mage, new Float3(2f, 0f, 0f));
            simulation.SubmitCommand(TargetCast(Mage, 1, AbilityId.Counterspell, Rogue));
            simulation.Tick();
            Assert.That(Snapshot(simulation, Rogue).HasStatus(StatusId.Silenced), Is.True);

            simulation.SubmitCommand(TargetCast(Healer, 1, AbilityId.Purify, Rogue));
            simulation.Tick();

            Assert.That(Snapshot(simulation, Rogue).HasStatus(StatusId.Silenced), Is.False);
            CombatEvent removed = FindEvent(
                simulation,
                CombatEventKind.StatusRemoved,
                AbilityId.Purify);
            Assert.That(removed.Actor, Is.EqualTo(Healer));
            Assert.That(removed.Target, Is.EqualTo(Rogue));
        }

        private static CombatSimulation CreateStandardDuel()
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            simulation.AddCombatant(Healer, new TeamId(1), ClassId.Healer, new Float3(0f, 0f, 0f));
            simulation.AddCombatant(Rogue, new TeamId(2), ClassId.Rogue, new Float3(1f, 0f, 0f));
            return simulation;
        }

        private static CombatSimulation CreateOrderingSimulation()
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            simulation.AddCombatant(Healer, new TeamId(1), ClassId.Healer, new Float3(0f, 0f, 0f));
            simulation.AddCombatant(Rogue, new TeamId(2), ClassId.Rogue, new Float3(1f, 0f, 0f));
            simulation.AddCombatant(Mage, new TeamId(3), ClassId.Mage, new Float3(1.5f, 0f, 0f));
            return simulation;
        }

        private static CombatSimulation CreateMageAndRogue(bool anchorFirst)
        {
            var simulation = new CombatSimulation(CombatRules.ArenaDefault);
            if (anchorFirst)
            {
                simulation.AddCombatant(Rogue, new TeamId(2), ClassId.Rogue, new Float3(2f, 0f, 0f));
                simulation.AddCombatant(Mage, new TeamId(1), ClassId.Mage, new Float3(0f, 0f, 0f));
            }
            else
            {
                simulation.AddCombatant(Mage, new TeamId(1), ClassId.Mage, new Float3(0f, 0f, 0f));
                simulation.AddCombatant(Rogue, new TeamId(2), ClassId.Rogue, new Float3(2f, 0f, 0f));
            }

            return simulation;
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
                new Float3(1f, 0f, 0f),
                ability,
                target,
                default,
                new Float3(1f, 0f, 0f));
        }

        private static CombatantSnapshot Snapshot(CombatSimulation simulation, ParticipantId id)
        {
            Assert.That(simulation.TryGetSnapshot(id, out CombatantSnapshot snapshot), Is.True);
            return snapshot;
        }

        private static void TickUntil(CombatSimulation simulation, int targetTick)
        {
            while (simulation.CurrentTick < targetTick)
            {
                simulation.Tick();
            }
        }

        private static CombatEvent FindEvent(CombatSimulation simulation, CombatEventKind kind)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind)
                {
                    return combatEvent;
                }
            }

            Assert.Fail($"Expected event {kind} at tick {simulation.CurrentTick}.");
            return default;
        }

        private static CombatEvent FindEvent(
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

            Assert.Fail(
                $"Expected event {kind} for {ability} at tick {simulation.CurrentTick}.");
            return default;
        }

        private static bool HasEvent(CombatSimulation simulation, CombatEventKind kind)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                if (simulation.GetEvent(index).Kind == kind)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AssertEvent(
            CombatSimulation simulation,
            CombatEventKind kind,
            AbilityId ability,
            float amount)
        {
            CombatEvent combatEvent = FindEvent(simulation, kind);
            Assert.That(combatEvent.Ability, Is.EqualTo(ability));
            Assert.That(combatEvent.Amount, Is.EqualTo(amount).Within(0.0001f));
        }

        private static void AssertSimulationsEqual(CombatSimulation left, CombatSimulation right)
        {
            Assert.That(right.CurrentTick, Is.EqualTo(left.CurrentTick));
            Assert.That(right.EventCount, Is.EqualTo(left.EventCount));
            for (int index = 0; index < left.EventCount; index++)
            {
                CombatEvent expected = left.GetEvent(index);
                CombatEvent actual = right.GetEvent(index);
                Assert.That(actual.Kind, Is.EqualTo(expected.Kind));
                Assert.That(actual.Actor, Is.EqualTo(expected.Actor));
                Assert.That(actual.Target, Is.EqualTo(expected.Target));
                Assert.That(actual.Ability, Is.EqualTo(expected.Ability));
                Assert.That(actual.Reason, Is.EqualTo(expected.Reason));
                Assert.That(actual.Amount, Is.EqualTo(expected.Amount).Within(0.0001f));
                Assert.That(actual.Vector, Is.EqualTo(expected.Vector));
            }

            CombatantSnapshot[] leftSnapshots = new CombatantSnapshot[CombatSimulation.MaxCombatants];
            CombatantSnapshot[] rightSnapshots = new CombatantSnapshot[CombatSimulation.MaxCombatants];
            int leftCount = left.CopySnapshots(leftSnapshots);
            int rightCount = right.CopySnapshots(rightSnapshots);
            Assert.That(rightCount, Is.EqualTo(leftCount));
            for (int index = 0; index < leftCount; index++)
            {
                Assert.That(rightSnapshots[index].Id, Is.EqualTo(leftSnapshots[index].Id));
                Assert.That(rightSnapshots[index].Health, Is.EqualTo(leftSnapshots[index].Health));
                Assert.That(rightSnapshots[index].Position, Is.EqualTo(leftSnapshots[index].Position));
                Assert.That(rightSnapshots[index].Barrier, Is.EqualTo(leftSnapshots[index].Barrier));
                Assert.That(rightSnapshots[index].CastingAbility,
                    Is.EqualTo(leftSnapshots[index].CastingAbility));
            }
        }
    }
}
