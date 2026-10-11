using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class AlternateAbilityCombatTests
    {
        private static readonly ParticipantId Priest = new ParticipantId(1);
        private static readonly ParticipantId Ally = new ParticipantId(2);
        private static readonly ParticipantId Enemy = new ParticipantId(3);

        [Test]
        public void DirectionalBindings_AuthorizeOnlyTheirOwningSpecializations()
        {
            AbilityKit discipline = SpecializationCatalog.GetKit(
                SpecializationId.Discipline);
            Assert.That(discipline.Count, Is.EqualTo(10));
            Assert.That(discipline.AbilityCount, Is.EqualTo(18));
            Assert.That(discipline.GetAbility(0), Is.EqualTo(AbilityId.Renew));
            Assert.That(
                discipline.GetAbility(0, AbilitySlotDirection.Up),
                Is.EqualTo(AbilityId.PrayerOfMending));
            Assert.That(discipline.Contains(AbilityId.Renew), Is.True);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Priest,
                    SpecializationId.Discipline,
                    AbilityId.Renew),
                Is.True);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Mage,
                    SpecializationId.Fire,
                    AbilityId.Renew),
                Is.False);
            AbilityDefinition renew = AbilityCatalog.Get(AbilityId.Renew);
            Assert.That(
                renew.Targeting.SmartTargetPolicy,
                Is.EqualTo(AbilitySmartTargetPolicy.SelectedFriendly));
            Assert.That(renew.Targeting.Allows(AbilityTargetRelation.Self), Is.True);
            Assert.That(renew.Targeting.Allows(AbilityTargetRelation.Friendly), Is.True);
            Assert.That(renew.Targeting.Allows(AbilityTargetRelation.Hostile), Is.False);

            AbilityKit shadowDance = SpecializationCatalog.GetKit(
                SpecializationId.ShadowDance);
            AbilityKit mutilate = SpecializationCatalog.GetKit(
                SpecializationId.Mutilate);
            Assert.That(
                shadowDance.GetAbility(0),
                Is.EqualTo(AbilityId.CheapShot));
            Assert.That(
                mutilate.GetAbility(0, AbilitySlotDirection.Up),
                Is.EqualTo(AbilityId.CheapShot));
            Assert.That(shadowDance.Count, Is.EqualTo(10));
            Assert.That(mutilate.Count, Is.EqualTo(10));
            Assert.That(shadowDance.AbilityCount, Is.EqualTo(18));
            Assert.That(mutilate.AbilityCount, Is.EqualTo(18));
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Rogue,
                    SpecializationId.ShadowDance,
                    AbilityId.CheapShot),
                Is.True);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Rogue,
                    SpecializationId.Mutilate,
                    AbilityId.CheapShot),
                Is.True);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Mage,
                    SpecializationId.Frost,
                    AbilityId.CheapShot),
                Is.False);
            AbilityDefinition cheapShot = AbilityCatalog.Get(AbilityId.CheapShot);
            Assert.That(cheapShot.Targeting.RequiresFacing, Is.True);
            Assert.That(
                cheapShot.Requirements & AbilityRequirementFlags.RequiresStealthAccess,
                Is.EqualTo(AbilityRequirementFlags.RequiresStealthAccess));
            Assert.That(
                cheapShot.Requirements & AbilityRequirementFlags.BreaksStealth,
                Is.EqualTo(AbilityRequirementFlags.BreaksStealth));
        }

        [Test]
        public void Smite_IsARepeatableDisciplineDamageCast()
        {
            AbilityDefinition smite = AbilityCatalog.Get(AbilityId.Smite);
            Assert.That(smite.OwnerSpecialization, Is.EqualTo(SpecializationId.Discipline));
            Assert.That(smite.CastProfile.ActivationType, Is.EqualTo(AbilityActivationType.Cast));
            Assert.That(smite.CastProfile.DurationSeconds, Is.EqualTo(1.5f));
            Assert.That(smite.ResourceCost.Type, Is.EqualTo(ResourceType.Mana));
            Assert.That(smite.ResourceCost.Amount, Is.EqualTo(8f));

            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 2f));

            float enemyHealth = Snapshot(simulation, Enemy).Health;
            Cast(simulation, Priest, 1, AbilityId.Smite, Enemy);
            Assert.That(Snapshot(simulation, Priest).Resource, Is.EqualTo(92f));
            Assert.That(Snapshot(simulation, Priest).CastingAbility, Is.EqualTo(AbilityId.Smite));
            Assert.That(Snapshot(simulation, Enemy).Health, Is.EqualTo(enemyHealth));

            while (Snapshot(simulation, Priest).CastingAbility != AbilityId.None)
            {
                simulation.Tick();
            }

            Assert.That(
                enemyHealth - Snapshot(simulation, Enemy).Health,
                Is.EqualTo(16f).Within(0.0001f));
        }

        [Test]
        public void Renew_HealsSixEveryThreeSecondsForFifteenSeconds()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(-5f, 0f, 0f));
            Add(
                simulation,
                Ally,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(-5f, 0f, 1f));

            for (int attackerIndex = 0; attackerIndex < 3; attackerIndex++)
            {
                ParticipantId attacker = new ParticipantId(3 + attackerIndex);
                Add(
                    simulation,
                    attacker,
                    2,
                    ClassId.Priest,
                    SpecializationId.Discipline,
                    new Float3(-5f, 0f, 3f + attackerIndex));
                Cast(
                    simulation,
                    attacker,
                    1,
                    AbilityId.ShadowWordDeath,
                    Ally);
            }

            float healthBeforeRenew = Snapshot(simulation, Ally).Health;
            Assert.That(healthBeforeRenew, Is.EqualTo(34f).Within(0.0001f));

            Cast(simulation, Priest, 1, AbilityId.Renew, Ally);
            Assert.That(simulation.HasAura(Ally, AuraId.Renew), Is.True);
            Assert.That(
                simulation.GetAuraRemaining(Ally, AuraId.Renew),
                Is.EqualTo(15f).Within(0.0001f));
            Assert.That(Snapshot(simulation, Priest).Resource, Is.EqualTo(88f).Within(0.0001f));
            Assert.That(
                simulation.GetGlobalCooldownRemaining(Priest),
                Is.EqualTo(1.5f).Within(0.0001f));

            int healCount = 0;
            float totalHealing = 0f;
            for (int tick = 0; tick < 450; tick++)
            {
                simulation.Tick();
                for (int eventIndex = 0; eventIndex < simulation.EventCount; eventIndex++)
                {
                    CombatEvent combatEvent = simulation.GetEvent(eventIndex);
                    if (combatEvent.Kind == CombatEventKind.Heal &&
                        combatEvent.Ability == AbilityId.Renew &&
                        combatEvent.Target == Ally)
                    {
                        healCount++;
                        totalHealing += combatEvent.Amount;
                    }
                }
            }

            Assert.That(healCount, Is.EqualTo(5));
            Assert.That(totalHealing, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(
                Snapshot(simulation, Ally).Health,
                Is.EqualTo(healthBeforeRenew + 30f).Within(0.0001f));
            Assert.That(simulation.HasAura(Ally, AuraId.Renew), Is.False);
        }

        [Test]
        public void BasicStealth_IsAReusableOutOfCombatStanceWhileVanishRemainsTheEscape()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default);

            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.True);
            Cast(simulation, Priest, 1, AbilityId.Stealth, Priest);
            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.False);

            while (simulation.GetCooldownRemaining(Priest, AbilityId.Stealth) > 0f)
            {
                simulation.Tick();
            }

            Cast(simulation, Priest, 2, AbilityId.Stealth, Priest);
            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.True);

            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 2f));
            Cast(simulation, Priest, 3, AbilityId.Mutilate, Enemy);
            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.False);

            CombatCommand stealthInCombat = TargetCast(
                Priest,
                4,
                AbilityId.Stealth,
                Priest);
            Assert.That(
                simulation.CanStartAbility(stealthInCombat, out string reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("in_combat"));

            Cast(simulation, Priest, 4, AbilityId.Vanish, Priest);
            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.True);
        }

        [Test]
        public void Sap_ControlsOnlyOneEnemyPerRogue()
        {
            ParticipantId secondEnemy = new ParticipantId(4);
            CombatSimulation simulation = new CombatSimulation(
                CombatRules.LevelingDefault);
            Add(
                simulation,
                Priest,
                1,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                new Float3(-180f, 0f, ArenaCollisionMap.LevelingCenterZ - 2f));
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(-180.4f, 0f, ArenaCollisionMap.LevelingCenterZ));
            Add(
                simulation,
                secondEnemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(-179.6f, 0f, ArenaCollisionMap.LevelingCenterZ));
            Assert.That(simulation.ApplyProgressionLevel(Priest, 2), Is.True);

            Cast(simulation, Priest, 1, AbilityId.Sap, Enemy);
            Assert.That(simulation.HasAura(Enemy, AuraId.Sap), Is.True);
            Assert.That(simulation.HasAura(secondEnemy, AuraId.Sap), Is.False);
            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.True);

            while (simulation.GetGlobalCooldownRemaining(Priest) > 0f)
            {
                simulation.Tick();
            }

            Cast(simulation, Priest, 2, AbilityId.Sap, secondEnemy);
            Assert.That(simulation.HasAura(Enemy, AuraId.Sap), Is.False);
            Assert.That(simulation.HasAura(secondEnemy, AuraId.Sap), Is.True);
            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.True);
        }

        [Test]
        public void CheapShot_ConsumesStealthAndUsesExistingStunDiminishingReturns()
        {
            ParticipantId secondRogue = new ParticipantId(4);
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                default);
            Add(
                simulation,
                secondRogue,
                1,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(0.4f, 0f, 0f));
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 2f));

            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.True);
            Cast(simulation, Priest, 1, AbilityId.CheapShot, Enemy);
            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.False);
            Assert.That(simulation.HasAura(Enemy, AuraId.Stun), Is.True);
            Assert.That(
                simulation.GetAuraRemaining(Enemy, AuraId.Stun),
                Is.EqualTo(4f).Within(0.0001f));
            Assert.That(Snapshot(simulation, Priest).ComboPoints, Is.EqualTo(2));
            Assert.That(Snapshot(simulation, Priest).ComboTarget, Is.EqualTo(Enemy));
            Assert.That(Snapshot(simulation, Priest).Resource, Is.EqualTo(40f).Within(0.0001f));
            Assert.That(
                simulation.GetGlobalCooldownRemaining(Priest),
                Is.EqualTo(1f).Within(0.0001f));
            Assert.That(simulation.GetCooldownRemaining(Priest, AbilityId.CheapShot), Is.Zero);

            Cast(simulation, secondRogue, 1, AbilityId.CheapShot, Enemy);
            Assert.That(
                simulation.GetAuraRemaining(Enemy, AuraId.Stun),
                Is.EqualTo(2f).Within(0.0001f));
            Assert.That(Snapshot(simulation, secondRogue).ComboPoints, Is.EqualTo(2));

            while (simulation.GetGlobalCooldownRemaining(Priest) > 0f ||
                   Snapshot(simulation, Priest).Resource < 60f)
            {
                simulation.Tick();
            }

            Assert.That(
                simulation.CanStartAbility(
                    TargetCast(Priest, 2, AbilityId.CheapShot, Enemy),
                    out string reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("requires_stealth"));
        }

        private static CombatSimulation CreateSimulation()
        {
            return new CombatSimulation(CombatRules.ArenaDefault);
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

        private static void Cast(
            CombatSimulation simulation,
            ParticipantId actor,
            uint sequence,
            AbilityId ability,
            ParticipantId target)
        {
            Assert.That(
                simulation.SubmitCommand(TargetCast(actor, sequence, ability, target)),
                Is.True);
            simulation.Tick();
            Assert.That(
                FindEvent(simulation, CombatEventKind.CastAccepted, ability).Actor,
                Is.EqualTo(actor));
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
            ParticipantId actor)
        {
            Assert.That(
                simulation.TryGetSnapshot(actor, out CombatantSnapshot snapshot),
                Is.True);
            return snapshot;
        }

        private static CombatEvent FindEvent(
            CombatSimulation simulation,
            CombatEventKind kind,
            AbilityId ability)
        {
            for (int eventIndex = 0; eventIndex < simulation.EventCount; eventIndex++)
            {
                CombatEvent combatEvent = simulation.GetEvent(eventIndex);
                if (combatEvent.Kind == kind && combatEvent.Ability == ability)
                {
                    return combatEvent;
                }
            }

            Assert.Fail("Expected " + kind + " for " + ability + ".");
            return default;
        }
    }
}
