using System.Collections.Generic;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class DeterministicLevelingMobBrainTests
    {
        private static readonly ParticipantId Player = new ParticipantId(1);
        private static readonly ParticipantId Mob = new ParticipantId(2);
        private static readonly TeamId FriendlyTeam = new TeamId(1);
        private static readonly TeamId HostileTeam = new TeamId(2);

        [TestCase(
            LevelingMobArchetypeId.AshMelee,
            ClassId.Rogue,
            SpecializationId.Mutilate,
            AbilityId.Shiv,
            1.25f)]
        [TestCase(
            LevelingMobArchetypeId.AshRanged,
            ClassId.Hunter,
            SpecializationId.Marksmanship,
            AbilityId.SteadyShot,
            8f)]
        [TestCase(
            LevelingMobArchetypeId.AshBrute,
            ClassId.Rogue,
            SpecializationId.ShadowDance,
            AbilityId.Hemorrhage,
            1.25f)]
        [TestCase(
            LevelingMobArchetypeId.AshCaster,
            ClassId.Mage,
            SpecializationId.Fire,
            AbilityId.Fireball,
            8f)]
        [TestCase(
            LevelingMobArchetypeId.AshElite,
            ClassId.Priest,
            SpecializationId.Shadow,
            AbilityId.MindFlay,
            8f)]
        [TestCase(
            LevelingMobArchetypeId.RimeFrostweaver,
            ClassId.Mage,
            SpecializationId.Frost,
            AbilityId.Frostbolt,
            8f)]
        [TestCase(
            LevelingMobArchetypeId.ReliquaryWarder,
            ClassId.Priest,
            SpecializationId.Discipline,
            AbilityId.Penance,
            8f)]
        [TestCase(
            LevelingMobArchetypeId.RiftWarden,
            ClassId.Boss,
            SpecializationId.Boss,
            AbilityId.BossCleave,
            1.25f)]
        public void PrimaryLoadouts_SubmitTheExpectedLegalAttack(
            LevelingMobArchetypeId archetype,
            ClassId mobClass,
            SpecializationId specialization,
            AbilityId expectedAbility,
            float distance)
        {
            CombatSimulation simulation = CreateEncounter(
                mobClass,
                specialization,
                distance);
            var brain = new DeterministicLevelingMobBrain(archetype);
            CombatantSnapshot self = Snapshot(simulation, Mob);
            CombatantSnapshot player = Snapshot(simulation, Player);

            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    self,
                    player,
                    out CombatCommand command),
                Is.True);
            Assert.That(command.Actor, Is.EqualTo(Mob));
            Assert.That(command.Sequence, Is.EqualTo(1u));
            Assert.That(command.Ability, Is.EqualTo(expectedAbility));
            ParticipantId expectedTarget = AbilityCatalog.Get(expectedAbility)
                .Targeting.Shape == AbilityTargetShape.CasterArea
                    ? Mob
                    : Player;
            Assert.That(command.Target, Is.EqualTo(expectedTarget));
            Assert.That(command.Movement.PlanarLength, Is.Zero);
            Assert.That(
                simulation.CanStartAbility(command, out string rejection),
                Is.True,
                rejection);

            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();

            Assert.That(
                HasEvent(
                    simulation,
                    CombatEventKind.CastAccepted,
                    Mob,
                    expectedTarget,
                    expectedAbility),
                Is.True);
        }

        [Test]
        public void ArchetypeCatalog_DefinesEightDistinctLegalCombatProfiles()
        {
            var seenClasses = new HashSet<ClassId>();
            var seenSpecializations = new HashSet<SpecializationId>();
            var seenAbilities = new HashSet<AbilityId>();

            Assert.That(LevelingMobArchetypeCatalog.Count, Is.EqualTo(8));
            for (int index = 0; index < LevelingMobArchetypeCatalog.Count; index++)
            {
                LevelingMobArchetypeDefinition profile =
                    LevelingMobArchetypeCatalog.GetByIndex(index);

                Assert.That(profile.Id, Is.Not.EqualTo(LevelingMobArchetypeId.None));
                Assert.That(
                    (int)profile.Id,
                    Is.EqualTo(index + 1),
                    "Stable archetype IDs are append-only storage keys.");
                Assert.That(profile.DisplayName, Is.Not.Empty);
                Assert.That(profile.ShortDisplayName, Is.Not.Empty);
                Assert.That(
                    profile.ShortDisplayName.Length,
                    Is.LessThanOrEqualTo(8),
                    profile.DisplayName);
                Assert.That(profile.Class, Is.Not.EqualTo(ClassId.None));
                Assert.That(profile.Specialization, Is.Not.EqualTo(SpecializationId.None));
                Assert.That(
                    SpecializationCatalog.IsCompatible(
                        profile.Class,
                        profile.Specialization),
                    Is.True,
                    profile.DisplayName);
                Assert.That(
                    AbilityCatalog.TryGet(
                        profile.PrimaryAttack,
                        out AbilityDefinition ability),
                    Is.True,
                    profile.DisplayName);
                Assert.That(ability.OwnerClass, Is.EqualTo(profile.Class));
                Assert.That(
                    ability.OwnerSpecialization == SpecializationId.None ||
                    ability.OwnerSpecialization == profile.Specialization,
                    Is.True,
                    profile.DisplayName);
                Assert.That(
                    AbilityCatalog.IsAbilityAllowed(
                        profile.Class,
                        profile.Specialization,
                        profile.PrimaryAttack),
                    Is.True,
                    profile.DisplayName);
                Assert.That(profile.PreferredRange, Is.GreaterThan(0f));
                Assert.That(profile.PresentationScale, Is.GreaterThan(0f));
                Assert.That(seenAbilities.Add(profile.PrimaryAttack), Is.True);
                seenClasses.Add(profile.Class);
                Assert.That(seenSpecializations.Add(profile.Specialization), Is.True);
                Assert.That(profile.SignatureAbility, Is.Not.EqualTo(AbilityId.None));
                Assert.That(profile.SignatureAbility, Is.Not.EqualTo(profile.PrimaryAttack));
                Assert.That(profile.SignaturePolicy, Is.Not.EqualTo(LevelingMobSignaturePolicy.None));
                Assert.That(
                    AbilityCatalog.TryGet(
                        profile.SignatureAbility,
                        out AbilityDefinition signature),
                    Is.True,
                    profile.DisplayName);
                Assert.That(signature.OwnerClass, Is.EqualTo(profile.Class));
                Assert.That(
                    AbilityCatalog.IsAbilityAllowed(
                        profile.Class,
                        profile.Specialization,
                        profile.SignatureAbility),
                    Is.True,
                    profile.DisplayName);
                if (profile.FallbackAbility != AbilityId.None)
                {
                    Assert.That(profile.FallbackAbility, Is.Not.EqualTo(profile.PrimaryAttack));
                    Assert.That(profile.FallbackAbility, Is.Not.EqualTo(profile.SignatureAbility));
                    Assert.That(
                        AbilityCatalog.IsAbilityAllowed(
                            profile.Class,
                            profile.Specialization,
                            profile.FallbackAbility),
                        Is.True,
                        profile.DisplayName);
                }
            }

            Assert.That(seenClasses, Does.Contain(ClassId.Rogue));
            Assert.That(seenClasses, Does.Contain(ClassId.Mage));
            Assert.That(seenClasses, Does.Contain(ClassId.Priest));
            Assert.That(seenClasses, Does.Contain(ClassId.Hunter));
            Assert.That(seenClasses, Does.Contain(ClassId.Boss));
            Assert.That(seenSpecializations, Has.Count.EqualTo(8));
        }

        [TestCase(LevelingMobArchetypeId.AshMelee, AbilityId.Envenom, 1.25f)]
        [TestCase(LevelingMobArchetypeId.AshRanged, AbilityId.AimedShot, 8f)]
        [TestCase(LevelingMobArchetypeId.AshBrute, AbilityId.Eviscerate, 1.25f)]
        [TestCase(LevelingMobArchetypeId.AshCaster, AbilityId.LivingBomb, 8f)]
        [TestCase(LevelingMobArchetypeId.AshElite, AbilityId.MindBlast, 8f)]
        [TestCase(LevelingMobArchetypeId.RimeFrostweaver, AbilityId.FrostNova, 4f)]
        public void CadenceProfiles_EventuallyCastTheirSignature(
            LevelingMobArchetypeId archetype,
            AbilityId expectedSignature,
            float distance)
        {
            LevelingMobArchetypeDefinition profile =
                LevelingMobArchetypeCatalog.Get(archetype);
            CombatSimulation simulation = CreateEncounter(
                profile.Class,
                profile.Specialization,
                distance,
                playerHealth: 1000f);
            var brain = new DeterministicLevelingMobBrain(archetype);

            bool accepted = DriveUntilAccepted(
                simulation,
                brain,
                expectedSignature,
                420);

            Assert.That(
                accepted,
                Is.True,
                profile.DisplayName + " never reached its signature action.");
        }

        [Test]
        public void DefensiveWarder_ShieldsItselfAfterTakingPressure()
        {
            CombatSimulation simulation = CreateEncounter(
                ClassId.Priest,
                SpecializationId.Discipline,
                8f,
                playerHealth: 1000f,
                mobHealth: 30f,
                playerClass: ClassId.Priest,
                playerSpecialization: SpecializationId.Shadow,
                useLevelingRules: false);
            CombatCommand pressure = CombatCommand.Cast(
                Player,
                1,
                default,
                new Float3(-1f, 0f, 0f),
                AbilityId.ShadowWordDeath,
                Mob,
                default,
                new Float3(-1f, 0f, 0f));
            Assert.That(simulation.SubmitCommand(pressure), Is.True);
            simulation.Tick();
            Assert.That(Snapshot(simulation, Mob).Health, Is.LessThanOrEqualTo(21f));

            var brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.ReliquaryWarder);
            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    Snapshot(simulation, Player),
                    out CombatCommand shield),
                Is.True);
            Assert.That(shield.Ability, Is.EqualTo(AbilityId.PowerWordShield));
            Assert.That(shield.Target, Is.EqualTo(Mob));
        }

        [Test]
        public void RiftWarden_OpensAtRangeWithTargetPointMeteor()
        {
            CombatSimulation simulation = CreateEncounter(
                ClassId.Boss,
                SpecializationId.Boss,
                8f,
                playerHealth: 1000f);
            var brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.RiftWarden);

            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    Snapshot(simulation, Player),
                    out CombatCommand meteor),
                Is.True);
            Assert.That(meteor.Ability, Is.EqualTo(AbilityId.BossMeteor));
            Assert.That(meteor.Target.IsValid, Is.False);
            Assert.That(meteor.TargetPoint, Is.EqualTo(Snapshot(simulation, Player).Position));
        }

        [Test]
        public void RiftWarden_ChainsThreeDistinctPressureActions()
        {
            CombatSimulation simulation = CreateEncounter(
                ClassId.Boss,
                SpecializationId.Boss,
                2.5f,
                playerHealth: 1000f);
            var brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.RiftWarden);

            Assert.That(
                DriveUntilAccepted(simulation, brain, AbilityId.BossCleave, 120),
                Is.True);
            Assert.That(
                DriveUntilAccepted(simulation, brain, AbilityId.BossKnockback, 180),
                Is.True);
            Assert.That(
                DriveUntilAccepted(simulation, brain, AbilityId.BossMeteor, 240),
                Is.True);
        }

        [Test]
        public void RiftWarden_ClosesBeforeMeteorCanReachThePlayer()
        {
            CombatSimulation simulation = CreateEncounter(
                ClassId.Boss,
                SpecializationId.Boss,
                30f,
                playerHealth: 1000f);
            var brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.RiftWarden);

            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    Snapshot(simulation, Player),
                    out CombatCommand command),
                Is.True);
            Assert.That(command.Ability, Is.EqualTo(AbilityId.None));
            Assert.That(command.Movement.X, Is.GreaterThan(0f));
            Assert.That(command.Movement.PlanarLength, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void CastedPrimary_IsNeverCanceledByTheBrainsNextDecision()
        {
            CombatSimulation simulation = CreateEncounter(
                ClassId.Mage,
                SpecializationId.Fire,
                8f,
                playerHealth: 1000f);
            var brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.AshCaster);
            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    Snapshot(simulation, Player),
                    out CombatCommand fireball),
                Is.True);
            Assert.That(fireball.Ability, Is.EqualTo(AbilityId.Fireball));
            Assert.That(simulation.SubmitCommand(fireball), Is.True);
            simulation.Tick();
            Assert.That(Snapshot(simulation, Mob).CastingAbility, Is.EqualTo(AbilityId.Fireball));

            for (int tick = 0; tick < 20; tick++)
            {
                Assert.That(
                    brain.TryBuildCommand(
                        simulation,
                        Snapshot(simulation, Mob),
                        Snapshot(simulation, Player),
                        out _),
                    Is.False);
                simulation.Tick();
                Assert.That(
                    HasEvent(
                        simulation,
                        CombatEventKind.Interrupted,
                        Mob,
                        Mob,
                        AbilityId.Fireball),
                    Is.False);
            }
        }

        [Test]
        public void RangedArchetype_RetreatsInsideItsMinimumAttackRange()
        {
            CombatSimulation simulation = CreateEncounter(
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                2f);
            var brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.AshRanged);

            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    Snapshot(simulation, Player),
                    out CombatCommand command),
                Is.True);
            Assert.That(command.Ability, Is.EqualTo(AbilityId.None));
            Assert.That(command.Movement.X, Is.LessThan(0f));
        }

        [TestCase(LevelingMobArchetypeId.AshRanged, 10f)]
        [TestCase(LevelingMobArchetypeId.AshCaster, 10f)]
        [TestCase(LevelingMobArchetypeId.AshElite, 9f)]
        [TestCase(LevelingMobArchetypeId.RimeFrostweaver, 9.5f)]
        [TestCase(LevelingMobArchetypeId.ReliquaryWarder, 9f)]
        public void RangedArchetype_ClosesToItsAuthoredEngagementRange(
            LevelingMobArchetypeId archetype,
            float preferredRange)
        {
            LevelingMobArchetypeDefinition profile =
                LevelingMobArchetypeCatalog.Get(archetype);
            CombatSimulation simulation = CreateEncounter(
                profile.Class,
                profile.Specialization,
                preferredRange + 2f);
            var brain = new DeterministicLevelingMobBrain(archetype);

            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    Snapshot(simulation, Player),
                    out CombatCommand command),
                Is.True);
            Assert.That(command.Ability, Is.EqualTo(AbilityId.None));
            Assert.That(command.Movement.X, Is.GreaterThan(0f));
            Assert.That(command.Movement.PlanarLength, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Decisions_AreThrottledForSevenTicksAtThirtyHertz()
        {
            CombatSimulation simulation = CreateEncounter(
                ClassId.Rogue,
                SpecializationId.Mutilate,
                8f);
            var brain = new DeterministicLevelingMobBrain();

            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    Snapshot(simulation, Player),
                    out CombatCommand first),
                Is.True);
            Assert.That(first.Ability, Is.EqualTo(AbilityId.None));
            Assert.That(first.Sequence, Is.EqualTo(1u));
            Assert.That(first.Movement.PlanarLength, Is.GreaterThan(0f));
            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    Snapshot(simulation, Player),
                    out _),
                Is.False);

            int decisionInterval = CombatRules.LevelingDefault.TickRate / 4;
            Assert.That(CombatRules.LevelingDefault.TickRate, Is.EqualTo(30));
            Assert.That(decisionInterval, Is.EqualTo(7));
            for (int tick = 1; tick < decisionInterval; tick++)
            {
                simulation.Tick();
                Assert.That(
                    brain.TryBuildCommand(
                        simulation,
                        Snapshot(simulation, Mob),
                        Snapshot(simulation, Player),
                        out _),
                    Is.False,
                    "The mob decided again at tick " + tick + ".");
            }

            simulation.Tick();
            Assert.That(simulation.CurrentTick, Is.EqualTo(decisionInterval));
            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    Snapshot(simulation, Player),
                    out CombatCommand next),
                Is.True);
            Assert.That(next.Sequence, Is.EqualTo(2u));
            Assert.That(next.Ability, Is.EqualTo(AbilityId.None));
        }

        [TestCase(AuraTag.Stealth)]
        [TestCase(AuraTag.TargetingDropped)]
        public void ConcealedPlayer_ImmediatelyStopsPersistentMovementAndReacquiresOnReveal(
            AuraTag concealment)
        {
            CombatSimulation simulation = CreateEncounter(
                ClassId.Rogue,
                SpecializationId.Mutilate,
                8f);
            var brain = new DeterministicLevelingMobBrain(
                LevelingMobArchetypeId.AshMelee);

            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    Snapshot(simulation, Player),
                    out CombatCommand chase),
                Is.True);
            Assert.That(chase.Sequence, Is.EqualTo(1u));
            Assert.That(chase.Movement.X, Is.GreaterThan(0f));
            Assert.That(simulation.SubmitCommand(chase), Is.True);
            simulation.Tick();

            CombatantSnapshot movingMob = Snapshot(simulation, Mob);
            CombatantSnapshot concealedPlayer = CopySnapshot(
                Snapshot(simulation, Player),
                new Float3(-8f, 0f, 0f),
                concealment);
            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    movingMob,
                    concealedPlayer,
                    out CombatCommand stop),
                Is.True,
                "Concealment must bypass the seven-tick decision throttle.");
            Assert.That(stop.Sequence, Is.EqualTo(2u));
            Assert.That(stop.Ability, Is.EqualTo(AbilityId.None));
            Assert.That(stop.Movement.PlanarLengthSquared, Is.Zero);
            Assert.That(stop.Facing, Is.EqualTo(movingMob.Facing));
            Assert.That(simulation.SubmitCommand(stop), Is.True);
            simulation.Tick();

            Float3 stoppedPosition = Snapshot(simulation, Mob).Position;
            for (int tick = 0; tick < 10; tick++)
            {
                Assert.That(
                    brain.TryBuildCommand(
                        simulation,
                        Snapshot(simulation, Mob),
                        concealedPlayer,
                        out CombatCommand hiddenCommand),
                    Is.False,
                    "A continuously concealed player must not be tracked.");
                Assert.That(hiddenCommand, Is.EqualTo(default(CombatCommand)));
                simulation.Tick();
                Assert.That(
                    Snapshot(simulation, Mob).Position,
                    Is.EqualTo(stoppedPosition));
            }

            CombatantSnapshot revealedPlayer = CopySnapshot(
                Snapshot(simulation, Player),
                concealedPlayer.Position,
                AuraTag.None);
            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    revealedPlayer,
                    out CombatCommand reacquire),
                Is.True);
            Assert.That(reacquire.Sequence, Is.EqualTo(3u));
            Assert.That(reacquire.Ability, Is.EqualTo(AbilityId.None));
            Assert.That(
                reacquire.Movement.X,
                Is.LessThan(0f),
                "The mob must reacquire the revealed position, not its stale heading.");
        }

        [Test]
        public void DeadMob_DoesNotSubmitAnotherDecision()
        {
            CombatSimulation simulation = CreateEncounter(
                ClassId.Rogue,
                SpecializationId.Mutilate,
                1f,
                mobHealth: 1f);
            KillWithFireBlast(simulation, Player, Mob);
            CombatantSnapshot self = Snapshot(simulation, Mob);

            Assert.That(self.IsAlive, Is.False);
            Assert.That(
                new DeterministicLevelingMobBrain().TryBuildCommand(
                    simulation,
                    self,
                    Snapshot(simulation, Player),
                    out CombatCommand command),
                Is.False);
            Assert.That(command, Is.EqualTo(default(CombatCommand)));
        }

        [Test]
        public void DeadPlayer_SuppressesMobDecisions()
        {
            CombatSimulation simulation = CreateEncounter(
                ClassId.Mage,
                SpecializationId.Fire,
                1f,
                playerHealth: 1f);
            KillWithFireBlast(simulation, Mob, Player);
            CombatantSnapshot player = Snapshot(simulation, Player);

            Assert.That(player.IsAlive, Is.False);
            Assert.That(
                new DeterministicLevelingMobBrain().TryBuildCommand(
                    simulation,
                    Snapshot(simulation, Mob),
                    player,
                    out CombatCommand command),
                Is.False);
            Assert.That(command, Is.EqualTo(default(CombatCommand)));
        }

        private static CombatSimulation CreateEncounter(
            ClassId mobClass,
            SpecializationId mobSpecialization,
            float playerDistance,
            float playerHealth = 100f,
            float mobHealth = 100f,
            ClassId playerClass = ClassId.Mage,
            SpecializationId playerSpecialization = SpecializationId.Fire,
            bool useLevelingRules = true)
        {
            var simulation = new CombatSimulation(
                useLevelingRules
                    ? CombatRules.LevelingDefault
                    : CombatRules.ArenaDefault);
            Assert.That(
                simulation.AddCombatant(
                    Player,
                    FriendlyTeam,
                    playerClass,
                    playerSpecialization,
                    new Float3(playerDistance, 0f, 0f),
                    playerHealth),
                Is.True);
            Assert.That(
                simulation.AddCombatant(
                    Mob,
                    HostileTeam,
                    mobClass,
                    mobSpecialization,
                    default,
                    mobHealth,
                    false),
                Is.True);
            return simulation;
        }

        private static bool DriveUntilAccepted(
            CombatSimulation simulation,
            DeterministicLevelingMobBrain brain,
            AbilityId ability,
            int maximumTicks)
        {
            for (int tick = 0; tick < maximumTicks; tick++)
            {
                if (brain.TryBuildCommand(
                        simulation,
                        Snapshot(simulation, Mob),
                        Snapshot(simulation, Player),
                        out CombatCommand command))
                {
                    simulation.SubmitCommand(command);
                }

                simulation.Tick();
                AbilityDefinition definition = AbilityCatalog.Get(ability);
                ParticipantId expectedTarget;
                if (definition.Targeting.Shape == AbilityTargetShape.Point ||
                    definition.Targeting.Shape == AbilityTargetShape.Direction ||
                    definition.Targeting.Shape == AbilityTargetShape.TargetArea)
                {
                    expectedTarget = default;
                }
                else if (definition.Targeting.Shape == AbilityTargetShape.CasterArea ||
                         (!definition.Targeting.Allows(AbilityTargetRelation.Hostile) &&
                          definition.Targeting.Allows(AbilityTargetRelation.Self)))
                {
                    expectedTarget = Mob;
                }
                else
                {
                    expectedTarget = Player;
                }
                if (HasEvent(
                        simulation,
                        CombatEventKind.CastAccepted,
                        Mob,
                        expectedTarget,
                        ability))
                {
                    return true;
                }
            }

            return false;
        }

        private static void KillWithFireBlast(
            CombatSimulation simulation,
            ParticipantId actor,
            ParticipantId target)
        {
            CombatantSnapshot actorSnapshot = Snapshot(simulation, actor);
            CombatantSnapshot targetSnapshot = Snapshot(simulation, target);
            Float3 direction =
                (targetSnapshot.Position - actorSnapshot.Position).PlanarNormalized;
            CombatCommand command = CombatCommand.Cast(
                actor,
                1,
                default,
                direction,
                AbilityId.FireBlast,
                target,
                targetSnapshot.Position,
                direction);
            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();
            Assert.That(
                HasEvent(
                    simulation,
                    CombatEventKind.Death,
                    actor,
                    target,
                    AbilityId.FireBlast),
                Is.True);
        }

        private static bool HasEvent(
            CombatSimulation simulation,
            CombatEventKind kind,
            ParticipantId actor,
            ParticipantId target,
            AbilityId ability)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind &&
                    combatEvent.Actor == actor &&
                    combatEvent.Target == target &&
                    combatEvent.Ability == ability)
                {
                    return true;
                }
            }

            return false;
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

        private static CombatantSnapshot CopySnapshot(
            CombatantSnapshot source,
            Float3 position,
            AuraTag activeAuraTags)
        {
            return new CombatantSnapshot(
                source.Id,
                source.Team,
                source.Class,
                source.Specialization,
                position,
                source.Facing,
                source.Health,
                source.MaxHealth,
                source.Barrier,
                source.CastingAbility,
                source.CastStartTick,
                source.CastEndTick,
                source.CastingActivationType,
                source.CastingSchool,
                source.ResourceType,
                source.Resource,
                source.MaxResource,
                source.ComboPoints,
                source.ComboTarget,
                source.GlobalCooldownEndTick,
                source.LockedSchools,
                source.IsAlive,
                source.IsInLava,
                source.ActiveStatuses,
                activeAuraTags,
                source.DispellableBeneficialAuraTypes,
                source.Level);
        }
    }
}
