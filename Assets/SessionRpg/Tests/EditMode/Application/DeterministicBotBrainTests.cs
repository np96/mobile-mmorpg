using System;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class DeterministicBotBrainTests
    {
        private static readonly ParticipantId Bot = new ParticipantId(1);
        private static readonly ParticipantId Ally = new ParticipantId(2);
        private static readonly ParticipantId Enemy = new ParticipantId(3);
        private static readonly ParticipantId OtherEnemy = new ParticipantId(4);
        private static readonly TeamId FriendlyTeam = new TeamId(1);
        private static readonly TeamId HostileTeam = new TeamId(2);

        [TestCase(SpecializationId.Discipline)]
        [TestCase(SpecializationId.Fire)]
        [TestCase(SpecializationId.Frost)]
        [TestCase(SpecializationId.ShadowDance)]
        [TestCase(SpecializationId.Mutilate)]
        [TestCase(SpecializationId.Shadow)]
        [TestCase(SpecializationId.Marksmanship)]
        [TestCase(SpecializationId.Boss)]
        public void SameSeedAndState_ProduceIdenticalCommands(
            SpecializationId specialization)
        {
            CombatSimulation left = CreateSimulation(specialization);
            CombatSimulation right = CreateSimulation(specialization);
            DeterministicBotBrain leftBrain = new DeterministicBotBrain(1701);
            DeterministicBotBrain rightBrain = new DeterministicBotBrain(1701);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                GetClass(specialization),
                specialization,
                default);
            CombatantSnapshot ally = CreateState(
                Ally,
                FriendlyTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(-4f, 0f, 0f));
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(6f, 0f, 0f));
            CombatantSnapshot[] snapshots = { self, ally, enemy };
            int decisions = 0;

            for (int tick = 0; tick < 90; tick++)
            {
                bool leftDecided = leftBrain.TryBuildCommand(
                    left,
                    self,
                    snapshots,
                    snapshots.Length,
                    out CombatCommand leftCommand);
                bool rightDecided = rightBrain.TryBuildCommand(
                    right,
                    self,
                    snapshots,
                    snapshots.Length,
                    out CombatCommand rightCommand);

                Assert.That(rightDecided, Is.EqualTo(leftDecided));
                if (leftDecided)
                {
                    decisions++;
                    AssertCommandsEqual(leftCommand, rightCommand);
                }

                left.Tick();
                right.Tick();
            }

            Assert.That(decisions, Is.GreaterThan(8));
        }

        [TestCase(SpecializationId.Shadow, AbilityId.VampiricTouch)]
        [TestCase(SpecializationId.Marksmanship, AbilityId.PetAttack)]
        public void NewSpecializations_ChooseAndSubmitValidSignatureOpeners(
            SpecializationId specialization,
            AbilityId expectedAbility)
        {
            CombatSimulation simulation = CreateSimulation(specialization);
            Assert.That(
                simulation.AddCombatant(
                    Enemy,
                    HostileTeam,
                    ClassId.Mage,
                    SpecializationId.Fire,
                    new Float3(10f, 0f, 0f)),
                Is.True);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                GetClass(specialization),
                specialization,
                default);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(10f, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, enemy },
                1702);

            Assert.That(command.Ability, Is.EqualTo(expectedAbility));
            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();
            Assert.That(
                HasCurrentEvent(
                    simulation,
                    CombatEventKind.CastAccepted,
                    expectedAbility),
                Is.True);
        }

        [Test]
        public void Decisions_RunAtApproximatelyFiveHertzWithSeededHesitation()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Fire);
            DeterministicBotBrain brain = new DeterministicBotBrain(91);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(10f, 0f, 0f));
            CombatantSnapshot[] snapshots = { self, enemy };

            Assert.That(brain.TryBuildCommand(
                simulation,
                self,
                snapshots,
                snapshots.Length,
                out _), Is.True);
            Assert.That(brain.TryBuildCommand(
                simulation,
                self,
                snapshots,
                snapshots.Length,
                out _), Is.False);

            int nextDecisionTick = -1;
            for (int tick = 1; tick <= 8; tick++)
            {
                simulation.Tick();
                if (brain.TryBuildCommand(
                        simulation,
                        self,
                        snapshots,
                        snapshots.Length,
                        out _))
                {
                    nextDecisionTick = simulation.CurrentTick;
                    break;
                }
            }

            Assert.That(nextDecisionTick, Is.InRange(6, 8));
        }

        [Test]
        public void NearestEnemyTie_UsesLowestParticipantIdRegardlessOfOrder()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Frost);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                default);
            CombatantSnapshot lowerIdEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(3f, 0f, 0f));
            CombatantSnapshot higherIdEnemy = CreateState(
                OtherEnemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                new Float3(-3f, 0f, 0f));
            CombatantSnapshot[] snapshots =
            {
                self,
                higherIdEnemy,
                lowerIdEnemy,
            };

            CombatCommand command = Decide(simulation, self, snapshots, 9);

            Assert.That(command.Ability, Is.EqualTo(AbilityId.FrostNova));
            Assert.That(command.Target, Is.EqualTo(Enemy));
        }

        [Test]
        public void FeignDeathTarget_IsSkippedForAnAvailableEnemy()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Fire);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            CombatantSnapshot feigningEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                new Float3(4f, 0f, 0f),
                activeAuraTags: AuraTag.TargetingDropped);
            CombatantSnapshot availableEnemy = CreateState(
                OtherEnemy,
                HostileTeam,
                ClassId.Priest,
                SpecializationId.Shadow,
                new Float3(8f, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, feigningEnemy, availableEnemy },
                1802);

            Assert.That(command.Ability, Is.Not.EqualTo(AbilityId.None));
            Assert.That(command.Target, Is.EqualTo(OtherEnemy));
        }

        [Test]
        public void Discipline_UsesPainSuppressionForEmergencyEvenWhileStunned()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Discipline);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                default,
                activeAuraTags: AuraTag.Stun);
            CombatantSnapshot ally = CreateState(
                Ally,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(5f, 0f, 0f),
                health: 24f);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(10f, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, ally, enemy },
                11);

            Assert.That(command.Ability, Is.EqualTo(AbilityId.PainSuppression));
            Assert.That(command.Target, Is.EqualTo(Ally));
            Assert.That(command.Movement.PlanarLength, Is.EqualTo(0f));
        }

        [TestCase(AuraTag.Fear)]
        [TestCase(AuraTag.Stun)]
        [TestCase(AuraTag.Disorient)]
        public void MeaningfulHardControl_UsesArenaTrinket(AuraTag control)
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Fire);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default,
                activeAuraTags: control);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(10f, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, enemy },
                111);

            Assert.That(command.Ability, Is.EqualTo(AbilityId.ArenaTrinket));
            Assert.That(command.Target, Is.EqualTo(Bot));
            Assert.That(command.Movement.PlanarLength, Is.Zero);
        }

        [TestCase(SpecializationId.Fire, AbilityId.Spellsteal, DispelType.Magic)]
        [TestCase(SpecializationId.Frost, AbilityId.Spellsteal, DispelType.Magic)]
        [TestCase(
            SpecializationId.Marksmanship,
            AbilityId.TranquilizingShot,
            DispelType.Magic | DispelType.Enrage)]
        public void PurgeActions_RequireAnActuallyDispellableBeneficialAura(
            SpecializationId specialization,
            AbilityId purgeAbility,
            DispelType matchingType)
        {
            CombatSimulation simulation = CreateSimulation(specialization);
            ClassId classId = GetClass(specialization);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                classId,
                specialization,
                default);
            CombatantSnapshot undispellableEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                new Float3(10f, 0f, 0f),
                activeAuraTags: AuraTag.DamageBoost | AuraTag.ExclusiveStance);

            CombatCommand withoutMatch = Decide(
                simulation,
                self,
                new[] { self, undispellableEnemy },
                141);
            Assert.That(withoutMatch.Ability, Is.Not.EqualTo(purgeAbility));

            CombatantSnapshot dispellableEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(10f, 0f, 0f),
                activeAuraTags: AuraTag.DamageBoost | AuraTag.ExclusiveStance,
                dispellableBeneficialAuraTypes: matchingType);
            CombatCommand withMatch = Decide(
                simulation,
                self,
                new[] { self, dispellableEnemy },
                142);
            Assert.That(withMatch.Ability, Is.EqualTo(purgeAbility));
        }

        [TestCase(SpecializationId.ShadowDance)]
        [TestCase(SpecializationId.Mutilate)]
        [TestCase(SpecializationId.Marksmanship)]
        public void DisarmedWeaponUsers_DoNotSubmitWeaponAbilities(
            SpecializationId specialization)
        {
            CombatSimulation simulation = CreateSimulation(specialization);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                GetClass(specialization),
                specialization,
                default,
                activeAuraTags: AuraTag.Disarm);
            float enemyDistance = specialization == SpecializationId.Marksmanship
                ? 10f
                : 2f;
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(enemyDistance, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, enemy },
                143);
            Assert.That(
                AbilityCatalog.IsWeaponAbility(command.Ability),
                Is.False,
                command.Ability + " would be rejected by Core while disarmed.");
        }

        [Test]
        public void ArenaTrinket_CommandResolvesAndCooldownPreventsWastefulReuse()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Fire);
            Assert.That(
                simulation.AddCombatant(
                    Enemy,
                    HostileTeam,
                    ClassId.Priest,
                    SpecializationId.Discipline,
                    new Float3(2f, 0f, 0f)),
                Is.True);
            Assert.That(
                simulation.SubmitCommand(CombatCommand.Cast(
                    Enemy,
                    1,
                    default,
                    new Float3(0f, 0f, -1f),
                    AbilityId.PsychicScream,
                    Enemy,
                    new Float3(2f, 0f, 0f),
                    new Float3(0f, 0f, -1f))),
                Is.True);
            simulation.Tick();

            CombatantSnapshot[] snapshots =
                new CombatantSnapshot[CombatSimulation.MaxCombatants];
            int snapshotCount = simulation.CopySnapshots(snapshots);
            CombatantSnapshot controlledBot = snapshots[0];
            Assert.That(controlledBot.Id, Is.EqualTo(Bot));
            Assert.That(controlledBot.HasAuraTag(AuraTag.Fear), Is.True);

            DeterministicBotBrain brain = new DeterministicBotBrain(112);
            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    controlledBot,
                    snapshots,
                    snapshotCount,
                    out CombatCommand trinket),
                Is.True);
            Assert.That(trinket.Ability, Is.EqualTo(AbilityId.ArenaTrinket));
            Assert.That(simulation.SubmitCommand(trinket), Is.True);
            simulation.Tick();

            Assert.That(simulation.HasAura(Bot, AuraId.Fear), Is.False);
            Assert.That(
                simulation.GetCooldownRemaining(Bot, AbilityId.ArenaTrinket),
                Is.GreaterThan(119f));

            CombatantSnapshot syntheticControl = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default,
                activeAuraTags: AuraTag.Fear);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(10f, 0f, 0f));
            CombatCommand cooldownDecision = Decide(
                simulation,
                syntheticControl,
                new[] { syntheticControl, enemy },
                113);
            Assert.That(cooldownDecision.Ability, Is.Not.EqualTo(AbilityId.ArenaTrinket));
        }

        [Test]
        public void RootAndSlowAlone_DoNotConsumeArenaTrinket()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Fire);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default,
                activeAuraTags: AuraTag.Root | AuraTag.Slow);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(10f, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, enemy },
                114);

            Assert.That(command.Ability, Is.EqualTo(AbilityId.LivingBomb));
            Assert.That(command.Movement.PlanarLength, Is.Zero);
        }

        [Test]
        public void Specializations_ActivateBuffsInIntentionalCombatWindows()
        {
            AssertBuffDecision(
                SpecializationId.Discipline,
                CreateState(
                    Bot,
                    FriendlyTeam,
                    ClassId.Priest,
                    SpecializationId.Discipline,
                    default),
                CreateState(
                    Enemy,
                    HostileTeam,
                    ClassId.Mage,
                    SpecializationId.Fire,
                    new Float3(10f, 0f, 0f)),
                AbilityId.PowerInfusion,
                AuraId.PowerInfusion,
                115);
            AssertBuffDecision(
                SpecializationId.Fire,
                CreateState(
                    Bot,
                    FriendlyTeam,
                    ClassId.Mage,
                    SpecializationId.Fire,
                    default),
                CreateState(
                    Enemy,
                    HostileTeam,
                    ClassId.Priest,
                    SpecializationId.Discipline,
                    new Float3(10f, 0f, 0f),
                    health: 90f,
                    activeAuraTags: AuraTag.Periodic),
                AbilityId.Combustion,
                AuraId.Combustion,
                116);
            AssertBuffDecision(
                SpecializationId.Frost,
                CreateState(
                    Bot,
                    FriendlyTeam,
                    ClassId.Mage,
                    SpecializationId.Frost,
                    default),
                CreateState(
                    Enemy,
                    HostileTeam,
                    ClassId.Priest,
                    SpecializationId.Discipline,
                    new Float3(10f, 0f, 0f)),
                AbilityId.IcyVeins,
                AuraId.IcyVeins,
                117);
            AssertBuffDecision(
                SpecializationId.ShadowDance,
                CreateState(
                    Bot,
                    FriendlyTeam,
                    ClassId.Rogue,
                    SpecializationId.ShadowDance,
                    default,
                    health: 70f),
                CreateState(
                    Enemy,
                    HostileTeam,
                    ClassId.Rogue,
                    SpecializationId.Mutilate,
                    new Float3(2f, 0f, 0f)),
                AbilityId.Evasion,
                AuraId.Evasion,
                118);
            AssertBuffDecision(
                SpecializationId.Mutilate,
                CreateState(
                    Bot,
                    FriendlyTeam,
                    ClassId.Rogue,
                    SpecializationId.Mutilate,
                    default,
                    comboPoints: 1,
                    comboTarget: Enemy),
                CreateState(
                    Enemy,
                    HostileTeam,
                    ClassId.Priest,
                    SpecializationId.Discipline,
                    new Float3(2f, 0f, 0f),
                    activeAuraTags: AuraTag.Poison),
                AbilityId.ColdBlood,
                AuraId.ColdBlood,
                119);
            AssertBuffDecision(
                SpecializationId.Shadow,
                CreateState(
                    Bot,
                    FriendlyTeam,
                    ClassId.Priest,
                    SpecializationId.Shadow,
                    default,
                    health: 30f),
                CreateState(
                    Enemy,
                    HostileTeam,
                    ClassId.Mage,
                    SpecializationId.Fire,
                    new Float3(10f, 0f, 0f)),
                AbilityId.Dispersion,
                AuraId.Dispersion,
                120);
            AssertBuffDecision(
                SpecializationId.Marksmanship,
                CreateState(
                    Bot,
                    FriendlyTeam,
                    ClassId.Hunter,
                    SpecializationId.Marksmanship,
                    default,
                    health: 30f),
                CreateState(
                    Enemy,
                    HostileTeam,
                    ClassId.Mage,
                    SpecializationId.Fire,
                    new Float3(10f, 0f, 0f)),
                AbilityId.Deterrence,
                AuraId.Deterrence,
                121);
        }

        [TestCase(AuraTag.None, AbilityId.PowerWordShield)]
        [TestCase(AuraTag.PreventsShield, AbilityId.Penance)]
        public void Discipline_RespectsWeakenedSoulBeforeShielding(
            AuraTag allyAuraTags,
            AbilityId expectedAbility)
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Discipline);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            CombatantSnapshot ally = CreateState(
                Ally,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(5f, 0f, 0f),
                health: 55f,
                activeAuraTags: allyAuraTags);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(10f, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, ally, enemy },
                12);

            Assert.That(command.Ability, Is.EqualTo(expectedAbility));
            Assert.That(command.Target, Is.EqualTo(Ally));
        }

        [Test]
        public void Discipline_DispelsHarmfulMagicBeforeRoutineCasting()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Discipline);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            CombatantSnapshot ally = CreateState(
                Ally,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                new Float3(4f, 0f, 0f),
                health: 90f,
                activeAuraTags: AuraTag.Fear | AuraTag.BreaksOnDamage);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(10f, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, ally, enemy },
                13);

            Assert.That(command.Ability, Is.EqualTo(AbilityId.DispelMagic));
            Assert.That(command.Target, Is.EqualTo(Ally));
        }

        [Test]
        public void Fire_InterruptsCastsBeforeApplyingLivingBomb()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Fire);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            CombatantSnapshot castingEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(10f, 0f, 0f),
                castingAbility: AbilityId.FlashHeal,
                castEndTick: 40,
                castingActivationType: AbilityActivationType.Cast,
                castingSchool: SpellSchoolMask.Holy);

            CombatCommand interrupt = Decide(
                simulation,
                self,
                new[] { self, castingEnemy },
                20);

            Assert.That(interrupt.Ability, Is.EqualTo(AbilityId.Counterspell));
            Assert.That(interrupt.Target, Is.EqualTo(Enemy));

            CombatantSnapshot idleEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(10f, 0f, 0f));
            CombatCommand pressure = Decide(
                simulation,
                self,
                new[] { self, idleEnemy },
                21);

            Assert.That(pressure.Ability, Is.EqualTo(AbilityId.LivingBomb));
        }

        [Test]
        public void Fire_UsesBlastWaveToKnockAnEdgeTargetTowardLava()
        {
            float enemyPositionX = CombatRules.ArenaDefault.SafeRadius - 1.25f;
            Float3 botPosition = new Float3(enemyPositionX - 5.25f, 0f, 0f);
            CombatSimulation simulation = CreateSimulation(
                SpecializationId.Fire,
                botPosition);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                botPosition);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(enemyPositionX, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, enemy },
                22);

            Assert.That(command.Ability, Is.EqualTo(AbilityId.BlastWave));
            Assert.That(command.Target, Is.EqualTo(Enemy));
        }

        [Test]
        public void Frost_UsesBarrierNovaAndFrozenTargetCombo()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Frost);
            CombatantSnapshot injuredSelf = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                default,
                health: 70f);
            CombatantSnapshot closeEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(3f, 0f, 0f));

            CombatCommand barrier = Decide(
                simulation,
                injuredSelf,
                new[] { injuredSelf, closeEnemy },
                30);
            Assert.That(barrier.Ability, Is.EqualTo(AbilityId.IceBarrier));
            Assert.That(barrier.Target, Is.EqualTo(Bot));

            CombatantSnapshot healthySelf = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                default);
            CombatCommand nova = Decide(
                simulation,
                healthySelf,
                new[] { healthySelf, closeEnemy },
                31);
            Assert.That(nova.Ability, Is.EqualTo(AbilityId.FrostNova));

            CombatantSnapshot frozenEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(10f, 0f, 0f),
                activeAuraTags: AuraTag.Root | AuraTag.Frozen);
            CombatCommand deepFreeze = Decide(
                simulation,
                healthySelf,
                new[] { healthySelf, frozenEnemy },
                32);
            Assert.That(deepFreeze.Ability, Is.EqualTo(AbilityId.DeepFreeze));

            CombatantSnapshot stunnedFrozenEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(10f, 0f, 0f),
                activeAuraTags: AuraTag.Root | AuraTag.Frozen | AuraTag.Stun);
            CombatCommand shatter = Decide(
                simulation,
                healthySelf,
                new[] { healthySelf, stunnedFrozenEnemy },
                33);
            Assert.That(shatter.Ability, Is.EqualTo(AbilityId.IceLance));
        }

        [Test]
        public void ShadowDance_UsesStepDanceAmbushAndTargetBoundFinisher()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.ShadowDance);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                default);
            CombatantSnapshot farEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(6f, 0f, 0f));

            CombatCommand step = Decide(
                simulation,
                self,
                new[] { self, farEnemy },
                40);
            Assert.That(step.Ability, Is.EqualTo(AbilityId.Shadowstep));

            Float3 behindFacing = new Float3(1f, 0f, 0f);
            CombatantSnapshot behindEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(2f, 0f, 0f),
                facing: behindFacing);
            CombatCommand dance = Decide(
                simulation,
                self,
                new[] { self, behindEnemy },
                41);
            Assert.That(dance.Ability, Is.EqualTo(AbilityId.ShadowDance));

            CombatantSnapshot dancingSelf = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                default,
                activeAuraTags: AuraTag.StealthAccess);
            CombatCommand ambush = Decide(
                simulation,
                dancingSelf,
                new[] { dancingSelf, behindEnemy },
                42);
            Assert.That(ambush.Ability, Is.EqualTo(AbilityId.Ambush));

            CombatantSnapshot finisherSelf = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                default,
                comboPoints: 4,
                comboTarget: Enemy);
            CombatCommand finisher = Decide(
                simulation,
                finisherSelf,
                new[] { finisherSelf, behindEnemy },
                43);
            Assert.That(finisher.Ability, Is.EqualTo(AbilityId.Eviscerate));

            CombatantSnapshot wrongTargetSelf = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                default,
                comboPoints: 5,
                comboTarget: OtherEnemy);
            CombatCommand noWrongTargetFinisher = Decide(
                simulation,
                wrongTargetSelf,
                new[] { wrongTargetSelf, behindEnemy },
                44);
            Assert.That(
                noWrongTargetFinisher.Ability,
                Is.EqualTo(AbilityId.ShadowDance));
        }

        [Test]
        public void Mutilate_AppliesPoisonThenUsesTargetBoundFinishers()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Mutilate);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(2f, 0f, 0f));

            CombatCommand shiv = Decide(
                simulation,
                self,
                new[] { self, enemy },
                50);
            Assert.That(shiv.Ability, Is.EqualTo(AbilityId.Shiv));

            CombatantSnapshot poisonedEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(2f, 0f, 0f),
                activeAuraTags: AuraTag.Poison);
            CombatantSnapshot envenomSelf = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default,
                comboPoints: 4,
                comboTarget: Enemy);
            CombatCommand envenom = Decide(
                simulation,
                envenomSelf,
                new[] { envenomSelf, poisonedEnemy },
                51);
            Assert.That(envenom.Ability, Is.EqualTo(AbilityId.Envenom));

            CombatantSnapshot kidneySelf = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default,
                comboPoints: 3,
                comboTarget: Enemy);
            CombatCommand kidney = Decide(
                simulation,
                kidneySelf,
                new[] { kidneySelf, poisonedEnemy },
                52);
            Assert.That(kidney.Ability, Is.EqualTo(AbilityId.KidneyShot));

            CombatantSnapshot wrongTargetSelf = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default,
                comboPoints: 5,
                comboTarget: OtherEnemy);
            CombatCommand noWrongTargetFinisher = Decide(
                simulation,
                wrongTargetSelf,
                new[] { wrongTargetSelf, poisonedEnemy },
                53);
            Assert.That(noWrongTargetFinisher.Ability, Is.EqualTo(AbilityId.Mutilate));
        }

        [Test]
        public void Mutilate_InterruptsAndUsesVanishAsDefensiveEscape()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Mutilate);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default);
            CombatantSnapshot castingEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(2f, 0f, 0f),
                castingAbility: AbilityId.Frostbolt,
                castEndTick: 50,
                castingActivationType: AbilityActivationType.Cast,
                castingSchool: SpellSchoolMask.Frost);

            CombatCommand kick = Decide(
                simulation,
                self,
                new[] { self, castingEnemy },
                54);
            Assert.That(kick.Ability, Is.EqualTo(AbilityId.Kick));

            CombatantSnapshot rootedSelf = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default,
                activeAuraTags: AuraTag.Root);
            CombatantSnapshot idleEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(2f, 0f, 0f));
            CombatCommand vanish = Decide(
                simulation,
                rootedSelf,
                new[] { rootedSelf, idleEnemy },
                55);
            Assert.That(vanish.Ability, Is.EqualTo(AbilityId.Vanish));
            Assert.That(vanish.Target, Is.EqualTo(Bot));
        }

        [Test]
        public void Boss_ChoosesKnockbackAtCloseRangeAndMeteorAtLongRange()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Boss);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Boss,
                SpecializationId.Boss,
                default);
            CombatantSnapshot closeEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(3f, 0f, 0f));
            CombatCommand knockback = Decide(
                simulation,
                self,
                new[] { self, closeEnemy },
                60);
            Assert.That(knockback.Ability, Is.EqualTo(AbilityId.BossKnockback));

            CombatantSnapshot farEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(10f, 0f, 0f));
            CombatCommand meteor = Decide(
                simulation,
                self,
                new[] { self, farEnemy },
                61);
            Assert.That(meteor.Ability, Is.EqualTo(AbilityId.BossMeteor));
            Assert.That(meteor.Target.IsValid, Is.False);
            Assert.That(meteor.TargetPoint, Is.EqualTo(farEnemy.Position));
        }

        [Test]
        public void ActiveCast_StopsMovementAndDoesNotQueueAnotherAbility()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Discipline);
            CombatantSnapshot castingSelf = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Priest,
                SpecializationId.Discipline,
                default,
                health: 50f,
                castingAbility: AbilityId.Penance,
                castStartTick: 0,
                castEndTick: 60,
                castingActivationType: AbilityActivationType.Channel,
                castingSchool: SpellSchoolMask.Holy);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(8f, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                castingSelf,
                new[] { castingSelf, enemy },
                70);

            Assert.That(command.HasAbility, Is.False);
            Assert.That(command.Movement.PlanarLength, Is.EqualTo(0f));
        }

        [Test]
        public void NewCastStopsMovementButInstantCastKeepsRepositioning()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Fire);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            CombatantSnapshot bombedEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(10f, 0f, 0f),
                activeAuraTags: AuraTag.Periodic);

            CombatCommand fireball = Decide(
                simulation,
                self,
                new[] { self, bombedEnemy },
                71);
            Assert.That(fireball.Ability, Is.EqualTo(AbilityId.Fireball));
            Assert.That(fireball.Movement.PlanarLength, Is.EqualTo(0f));

            CombatantSnapshot unbombedEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(10f, 0f, 0f));
            CombatCommand livingBomb = Decide(
                simulation,
                self,
                new[] { self, unbombedEnemy },
                72);
            Assert.That(livingBomb.Ability, Is.EqualTo(AbilityId.LivingBomb));
            Assert.That(livingBomb.Movement.PlanarLength, Is.GreaterThan(0f));
        }

        [Test]
        public void LavaRecovery_PrioritizesClassMobilityAndCenterwardMovement()
        {
            Float3 lavaPosition = new Float3(12f, 0f, 0f);
            CombatSimulation frostSimulation = CreateSimulation(
                SpecializationId.Frost,
                lavaPosition);
            CombatantSnapshot frost = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Frost,
                lavaPosition,
                inLava: true);
            CombatantSnapshot innerEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(6f, 0f, 0f));
            CombatCommand blink = Decide(
                frostSimulation,
                frost,
                new[] { frost, innerEnemy },
                80);

            Assert.That(blink.Ability, Is.EqualTo(AbilityId.Blink));
            Assert.That(blink.TargetPoint.X, Is.LessThan(frost.Position.X));
            Assert.That(blink.Movement.X, Is.EqualTo(-1f).Within(0.0001f));

            CombatSimulation danceSimulation = CreateSimulation(
                SpecializationId.ShadowDance,
                lavaPosition);
            CombatantSnapshot dancer = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                lavaPosition,
                inLava: true);
            CombatCommand step = Decide(
                danceSimulation,
                dancer,
                new[] { dancer, innerEnemy },
                81);
            Assert.That(step.Ability, Is.EqualTo(AbilityId.Shadowstep));
            Assert.That(step.Target, Is.EqualTo(Enemy));
            Assert.That(step.Movement.X, Is.EqualTo(-1f).Within(0.0001f));

            CombatSimulation mutilateSimulation = CreateSimulation(
                SpecializationId.Mutilate,
                lavaPosition);
            CombatantSnapshot mutilate = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                lavaPosition,
                inLava: true);
            CombatCommand runToSafety = Decide(
                mutilateSimulation,
                mutilate,
                new[] { mutilate, innerEnemy },
                82);
            Assert.That(runToSafety.Movement.X, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(runToSafety.Movement.Z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Marksmanship_UsesDisengageForLavaEdgeRecoveryAndCloseRangePeel()
        {
            Float3 lavaPosition = new Float3(12f, 0f, 0f);
            CombatSimulation lavaSimulation = CreateSimulation(
                SpecializationId.Marksmanship,
                lavaPosition);
            Assert.That(
                lavaSimulation.AddCombatant(
                    Enemy,
                    HostileTeam,
                    ClassId.Rogue,
                    SpecializationId.Mutilate,
                    new Float3(6f, 0f, 0f)),
                Is.True);
            CombatantSnapshot hunterInLava = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                lavaPosition,
                inLava: true);
            CombatantSnapshot innerEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(6f, 0f, 0f));

            CombatCommand recovery = Decide(
                lavaSimulation,
                hunterInLava,
                new[] { hunterInLava, innerEnemy },
                83);

            Assert.That(recovery.Ability, Is.EqualTo(AbilityId.Disengage));
            Assert.That(recovery.Facing.X, Is.EqualTo(1f).Within(0.0001f));
            Assert.That(recovery.Movement.X, Is.EqualTo(-1f).Within(0.0001f));
            Assert.That(lavaSimulation.SubmitCommand(recovery), Is.True);
            lavaSimulation.Tick();
            Assert.That(
                HasCurrentEvent(
                    lavaSimulation,
                    CombatEventKind.CastAccepted,
                    AbilityId.Disengage),
                Is.True);
            Assert.That(
                lavaSimulation.TryGetSnapshot(
                    Bot,
                    out CombatantSnapshot recoveredHunter),
                Is.True);
            Assert.That(
                recoveredHunter.Position.PlanarLength,
                Is.LessThan(lavaPosition.PlanarLength));

            Float3 edgePosition = new Float3(14f, 0f, 0f);
            CombatSimulation edgeSimulation = CreateSimulation(
                SpecializationId.Marksmanship,
                edgePosition);
            CombatantSnapshot hunterNearEdge = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                edgePosition);
            CombatCommand edgeRecovery = Decide(
                edgeSimulation,
                hunterNearEdge,
                new[] { hunterNearEdge, innerEnemy },
                84);

            Assert.That(edgeRecovery.Ability, Is.EqualTo(AbilityId.Disengage));
            Assert.That(edgeRecovery.Facing.X, Is.EqualTo(1f).Within(0.0001f));

            CombatSimulation peelSimulation = CreateSimulation(
                SpecializationId.Marksmanship);
            CombatantSnapshot hunter = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                default);
            CombatantSnapshot closeEnemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(2f, 0f, 0f));

            CombatCommand peel = Decide(
                peelSimulation,
                hunter,
                new[] { hunter, closeEnemy },
                85);

            Assert.That(peel.Ability, Is.EqualTo(AbilityId.Disengage));
            Assert.That(peel.Facing.X, Is.EqualTo(1f).Within(0.0001f));
        }

        [TestCase(0f, 0, StatusFlags.None)]
        [TestCase(100f, 100, StatusFlags.None)]
        [TestCase(100f, 0, StatusFlags.Silenced)]
        public void RequirementsPreventKnownInvalidFireCasts(
            float mana,
            int globalCooldownEndTick,
            StatusFlags statuses)
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Fire);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default,
                resource: mana,
                globalCooldownEndTick: globalCooldownEndTick,
                activeStatuses: statuses);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(10f, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, enemy },
                90);

            Assert.That(command.HasAbility, Is.False);
            Assert.That(command.Movement.PlanarLength, Is.GreaterThan(0f));
        }

        [Test]
        public void OutOfRangeMeleeBot_RepositionsWithoutSubmittingInvalidCast()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Mutilate);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(12f, 0f, 0f));

            CombatCommand command = Decide(
                simulation,
                self,
                new[] { self, enemy },
                91);

            Assert.That(command.HasAbility, Is.False);
            Assert.That(command.Movement.X, Is.GreaterThan(0f));
        }

        [Test]
        public void Decision_DoesNotMutateSimulationOrSnapshotStorage()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Fire);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            CombatantSnapshot enemy = CreateState(
                Enemy,
                HostileTeam,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(10f, 0f, 0f));
            CombatantSnapshot[] snapshots = { self, enemy };
            CombatantSnapshot beforeSelf = snapshots[0];
            CombatantSnapshot beforeEnemy = snapshots[1];
            int tickBefore = simulation.CurrentTick;
            int eventCountBefore = simulation.EventCount;

            Decide(simulation, self, snapshots, 100);

            AssertSnapshotsEqual(beforeSelf, snapshots[0]);
            AssertSnapshotsEqual(beforeEnemy, snapshots[1]);
            Assert.That(simulation.CurrentTick, Is.EqualTo(tickBefore));
            Assert.That(simulation.EventCount, Is.EqualTo(eventCountBefore));
        }

        [Test]
        public void Arguments_AreValidatedBeforeReadingSnapshotStorage()
        {
            CombatSimulation simulation = CreateSimulation(SpecializationId.Fire);
            CombatantSnapshot self = CreateState(
                Bot,
                FriendlyTeam,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            DeterministicBotBrain brain = new DeterministicBotBrain(1);

            Assert.That(
                () => brain.TryBuildCommand(null, self, Array.Empty<CombatantSnapshot>(), 0, out _),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => brain.TryBuildCommand(simulation, self, null, 0, out _),
                Throws.TypeOf<ArgumentNullException>());
            Assert.That(
                () => brain.TryBuildCommand(
                    simulation,
                    self,
                    new CombatantSnapshot[1],
                    -1,
                    out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());
            Assert.That(
                () => brain.TryBuildCommand(
                    simulation,
                    self,
                    new CombatantSnapshot[1],
                    2,
                    out _),
                Throws.TypeOf<ArgumentOutOfRangeException>());
        }

        private static void AssertBuffDecision(
            SpecializationId specialization,
            CombatantSnapshot self,
            CombatantSnapshot enemy,
            AbilityId expectedAbility,
            AuraId expectedAura,
            int seed)
        {
            CombatSimulation simulation = CreateSimulation(specialization);
            CombatantSnapshot[] snapshots = { self, enemy };
            CombatCommand command = Decide(simulation, self, snapshots, seed);

            Assert.That(command.Ability, Is.EqualTo(expectedAbility));
            Assert.That(command.Target, Is.EqualTo(Bot));
            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();
            Assert.That(simulation.HasAura(Bot, expectedAura), Is.True);
            Assert.That(
                simulation.GetCooldownRemaining(Bot, expectedAbility),
                Is.GreaterThan(44f));

            CombatCommand cooldownDecision = Decide(
                simulation,
                self,
                snapshots,
                seed + 1000);
            Assert.That(cooldownDecision.Ability, Is.Not.EqualTo(expectedAbility));
        }

        private static CombatCommand Decide(
            CombatSimulation simulation,
            CombatantSnapshot self,
            CombatantSnapshot[] snapshots,
            int seed)
        {
            DeterministicBotBrain brain = new DeterministicBotBrain(seed);
            Assert.That(brain.TryBuildCommand(
                simulation,
                self,
                snapshots,
                snapshots.Length,
                out CombatCommand command), Is.True);
            return command;
        }

        private static bool HasCurrentEvent(
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

        private static CombatSimulation CreateSimulation(
            SpecializationId specialization,
            Float3 botPosition = default)
        {
            CombatSimulation simulation = new CombatSimulation(CombatRules.ArenaDefault);
            Assert.That(simulation.AddCombatant(
                Bot,
                FriendlyTeam,
                GetClass(specialization),
                specialization,
                botPosition), Is.True);
            return simulation;
        }

        private static CombatantSnapshot CreateState(
            ParticipantId id,
            TeamId team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            Float3 facing = default,
            float health = 100f,
            float maxHealth = 100f,
            float barrier = 0f,
            float resource = -1f,
            float maxResource = -1f,
            int comboPoints = 0,
            ParticipantId comboTarget = default,
            AbilityId castingAbility = AbilityId.None,
            int castStartTick = 0,
            int castEndTick = 0,
            AbilityActivationType castingActivationType = AbilityActivationType.Instant,
            SpellSchoolMask castingSchool = SpellSchoolMask.None,
            int globalCooldownEndTick = 0,
            SpellSchoolMask lockedSchools = SpellSchoolMask.None,
            bool alive = true,
            bool inLava = false,
            StatusFlags activeStatuses = StatusFlags.None,
            AuraTag activeAuraTags = AuraTag.None,
            DispelType dispellableBeneficialAuraTypes = DispelType.None)
        {
            ResourceProfile resourceProfile =
                SpecializationCatalog.GetResourceProfile(specialization);
            Float3 resolvedFacing = facing.PlanarLengthSquared > 0.0001f
                ? facing.PlanarNormalized
                : new Float3(0f, 0f, 1f);
            float resolvedResource = resource >= 0f
                ? resource
                : resourceProfile.Initial;
            float resolvedMaxResource = maxResource >= 0f
                ? maxResource
                : resourceProfile.Maximum;

            return new CombatantSnapshot(
                id,
                team,
                classId,
                specialization,
                position,
                resolvedFacing,
                health,
                maxHealth,
                barrier,
                castingAbility,
                castStartTick,
                castEndTick,
                castingActivationType,
                castingSchool,
                resourceProfile.Type,
                resolvedResource,
                resolvedMaxResource,
                comboPoints,
                comboTarget,
                globalCooldownEndTick,
                lockedSchools,
                alive,
                inLava,
                activeStatuses,
                activeAuraTags,
                dispellableBeneficialAuraTypes);
        }

        private static ClassId GetClass(SpecializationId specialization)
        {
            switch (specialization)
            {
                case SpecializationId.Discipline:
                case SpecializationId.Shadow:
                    return ClassId.Priest;
                case SpecializationId.Fire:
                case SpecializationId.Frost:
                    return ClassId.Mage;
                case SpecializationId.ShadowDance:
                case SpecializationId.Mutilate:
                    return ClassId.Rogue;
                case SpecializationId.Marksmanship:
                    return ClassId.Hunter;
                case SpecializationId.Boss:
                    return ClassId.Boss;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(specialization),
                        specialization,
                        "Unsupported specialization.");
            }
        }

        private static void AssertCommandsEqual(
            CombatCommand expected,
            CombatCommand actual)
        {
            Assert.That(actual.Actor, Is.EqualTo(expected.Actor));
            Assert.That(actual.Sequence, Is.EqualTo(expected.Sequence));
            Assert.That(actual.Movement, Is.EqualTo(expected.Movement));
            Assert.That(actual.Facing, Is.EqualTo(expected.Facing));
            Assert.That(actual.Ability, Is.EqualTo(expected.Ability));
            Assert.That(actual.Target, Is.EqualTo(expected.Target));
            Assert.That(actual.TargetPoint, Is.EqualTo(expected.TargetPoint));
            Assert.That(actual.AimDirection, Is.EqualTo(expected.AimDirection));
        }

        private static void AssertSnapshotsEqual(
            CombatantSnapshot expected,
            CombatantSnapshot actual)
        {
            Assert.That(actual.Id, Is.EqualTo(expected.Id));
            Assert.That(actual.Team, Is.EqualTo(expected.Team));
            Assert.That(actual.Class, Is.EqualTo(expected.Class));
            Assert.That(actual.Specialization, Is.EqualTo(expected.Specialization));
            Assert.That(actual.Position, Is.EqualTo(expected.Position));
            Assert.That(actual.Facing, Is.EqualTo(expected.Facing));
            Assert.That(actual.Health, Is.EqualTo(expected.Health));
            Assert.That(actual.MaxHealth, Is.EqualTo(expected.MaxHealth));
            Assert.That(actual.Barrier, Is.EqualTo(expected.Barrier));
            Assert.That(actual.CastingAbility, Is.EqualTo(expected.CastingAbility));
            Assert.That(actual.CastStartTick, Is.EqualTo(expected.CastStartTick));
            Assert.That(actual.CastEndTick, Is.EqualTo(expected.CastEndTick));
            Assert.That(
                actual.CastingActivationType,
                Is.EqualTo(expected.CastingActivationType));
            Assert.That(actual.CastingSchool, Is.EqualTo(expected.CastingSchool));
            Assert.That(actual.ResourceType, Is.EqualTo(expected.ResourceType));
            Assert.That(actual.Resource, Is.EqualTo(expected.Resource));
            Assert.That(actual.MaxResource, Is.EqualTo(expected.MaxResource));
            Assert.That(actual.ComboPoints, Is.EqualTo(expected.ComboPoints));
            Assert.That(actual.ComboTarget, Is.EqualTo(expected.ComboTarget));
            Assert.That(
                actual.GlobalCooldownEndTick,
                Is.EqualTo(expected.GlobalCooldownEndTick));
            Assert.That(actual.LockedSchools, Is.EqualTo(expected.LockedSchools));
            Assert.That(actual.IsAlive, Is.EqualTo(expected.IsAlive));
            Assert.That(actual.IsInLava, Is.EqualTo(expected.IsInLava));
            Assert.That(actual.ActiveStatuses, Is.EqualTo(expected.ActiveStatuses));
            Assert.That(actual.ActiveAuraTags, Is.EqualTo(expected.ActiveAuraTags));
            Assert.That(
                actual.DispellableBeneficialAuraTypes,
                Is.EqualTo(expected.DispellableBeneficialAuraTypes));
        }
    }
}
