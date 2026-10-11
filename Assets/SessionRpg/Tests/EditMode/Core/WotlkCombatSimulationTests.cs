using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class WotlkCombatSimulationTests
    {
        private static readonly ParticipantId Priest = new ParticipantId(1);
        private static readonly ParticipantId Ally = new ParticipantId(2);
        private static readonly ParticipantId Enemy = new ParticipantId(3);
        private static readonly ParticipantId EnemyTwo = new ParticipantId(4);

        [Test]
        public void SpecializationCatalog_ExposesTheSevenExactTenPrimaryKits()
        {
            AssertKit(
                SpecializationId.Discipline,
                AbilityId.Renew,
                AbilityId.PsychicScream,
                AbilityId.Penance,
                AbilityId.Smite,
                AbilityId.ShadowWordDeath,
                AbilityId.DispelMagic,
                AbilityId.Shadowfiend,
                AbilityId.InnerFocus,
                AbilityId.PowerInfusion,
                AbilityId.ArenaTrinket);
            AssertKit(
                SpecializationId.Fire,
                AbilityId.DragonBreath,
                AbilityId.ManaShield,
                AbilityId.Fireball,
                AbilityId.FireBlast,
                AbilityId.LivingBomb,
                AbilityId.Scorch,
                AbilityId.Counterspell,
                AbilityId.Pyroblast,
                AbilityId.Combustion,
                AbilityId.ArenaTrinket);
            AssertKit(
                SpecializationId.Frost,
                AbilityId.Polymorph,
                AbilityId.IceBarrier,
                AbilityId.Frostbolt,
                AbilityId.IceLance,
                AbilityId.Counterspell,
                AbilityId.Spellsteal,
                AbilityId.IcyVeins,
                AbilityId.MageArmor,
                AbilityId.IceArmor,
                AbilityId.ArenaTrinket);
            AssertKit(
                SpecializationId.ShadowDance,
                AbilityId.CheapShot,
                AbilityId.Shadowstep,
                AbilityId.Hemorrhage,
                AbilityId.Ambush,
                AbilityId.Eviscerate,
                AbilityId.Kick,
                AbilityId.CloakOfShadows,
                AbilityId.ShadowDance,
                AbilityId.Preparation,
                AbilityId.ArenaTrinket);
            AssertKit(
                SpecializationId.Mutilate,
                AbilityId.KidneyShot,
                AbilityId.Vanish,
                AbilityId.Mutilate,
                AbilityId.Envenom,
                AbilityId.Shiv,
                AbilityId.Eviscerate,
                AbilityId.Kick,
                AbilityId.ColdBlood,
                AbilityId.Preparation,
                AbilityId.ArenaTrinket);
            AssertKit(
                SpecializationId.Shadow,
                AbilityId.DevouringPlague,
                AbilityId.PsychicScream,
                AbilityId.VampiricTouch,
                AbilityId.MindFlay,
                AbilityId.PowerWordShield,
                AbilityId.DispelMagic,
                AbilityId.Shadowfiend,
                AbilityId.MassDispel,
                AbilityId.Shadowform,
                AbilityId.ArenaTrinket);
            AssertKit(
                SpecializationId.Marksmanship,
                AbilityId.SerpentSting,
                AbilityId.ScatterShot,
                AbilityId.ChimeraShot,
                AbilityId.AimedShot,
                AbilityId.ArcaneShot,
                AbilityId.SteadyShot,
                AbilityId.PetAttack,
                AbilityId.RapidFire,
                AbilityId.Deterrence,
                AbilityId.ArenaTrinket);

            Assert.That(AbilityCatalog.Get(AbilityId.ArenaTrinket).OwnerClass, Is.EqualTo(ClassId.None));
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Priest,
                    SpecializationId.Discipline,
                    AbilityId.ArenaTrinket),
                Is.True);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Mage,
                    SpecializationId.Fire,
                    AbilityId.ArenaTrinket),
                Is.True);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Rogue,
                    SpecializationId.Mutilate,
                    AbilityId.ArenaTrinket),
                Is.True);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Hunter,
                    SpecializationId.Marksmanship,
                    AbilityId.ArenaTrinket),
                Is.True);

            AssertSharedAction(
                ClassId.Priest,
                AbilityId.PowerWordShield,
                SpecializationId.Discipline,
                SpecializationId.Shadow);
            AssertSharedAction(
                ClassId.Priest,
                AbilityId.PsychicScream,
                SpecializationId.Discipline,
                SpecializationId.Shadow);
            AssertSharedAction(
                ClassId.Priest,
                AbilityId.ShadowWordDeath,
                SpecializationId.Discipline,
                SpecializationId.Shadow);
            AssertSharedAction(
                ClassId.Priest,
                AbilityId.Shadowfiend,
                SpecializationId.Discipline,
                SpecializationId.Shadow);
            AssertSharedAction(
                ClassId.Priest,
                AbilityId.DispelMagic,
                SpecializationId.Discipline,
                SpecializationId.Shadow);

            AssertSharedAction(
                ClassId.Mage,
                AbilityId.Counterspell,
                SpecializationId.Fire,
                SpecializationId.Frost);
            AssertSharedAction(
                ClassId.Mage,
                AbilityId.Blink,
                SpecializationId.Fire,
                SpecializationId.Frost);
            AssertSharedAction(
                ClassId.Mage,
                AbilityId.Polymorph,
                SpecializationId.Fire,
                SpecializationId.Frost);
            AssertSharedAction(
                ClassId.Rogue,
                AbilityId.KidneyShot,
                SpecializationId.ShadowDance,
                SpecializationId.Mutilate);
            AssertSharedAction(
                ClassId.Rogue,
                AbilityId.Kick,
                SpecializationId.ShadowDance,
                SpecializationId.Mutilate);
            AssertSharedAction(
                ClassId.Rogue,
                AbilityId.CloakOfShadows,
                SpecializationId.ShadowDance,
                SpecializationId.Mutilate);

            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Mage,
                    SpecializationId.Fire,
                    AbilityId.Frostbolt),
                Is.False);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Rogue,
                    SpecializationId.Mutilate,
                    AbilityId.Hemorrhage),
                Is.False);
            Assert.That(
                AbilityCatalog.IsAbilityAllowed(
                    ClassId.Hunter,
                    SpecializationId.Marksmanship,
                    AbilityId.Fireball),
                Is.False);
        }

        [Test]
        public void ResourcesAndGlobalCooldown_AreSpecSpecificAndAuthoritative()
        {
            CombatSimulation mageSimulation = CreateSimulation();
            Add(
                mageSimulation,
                Priest,
                1,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            Add(
                mageSimulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 1f));

            CombatantSnapshot mage = Snapshot(mageSimulation, Priest);
            Assert.That(mage.ResourceType, Is.EqualTo(ResourceType.Mana));
            Assert.That(mage.Resource, Is.EqualTo(100f));
            Cast(mageSimulation, Priest, 1, AbilityId.FireBlast, Enemy);

            mage = Snapshot(mageSimulation, Priest);
            Assert.That(mage.Resource, Is.EqualTo(92f).Within(0.0001f));
            Assert.That(
                mageSimulation.GetGlobalCooldownRemaining(Priest),
                Is.EqualTo(1.5f).Within(0.0001f));
            Assert.That(
                mageSimulation.CanUseAbility(Priest, AbilityId.Fireball, out string reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("global_cooldown"));

            TickUntil(mageSimulation, 46);
            Assert.That(mageSimulation.GetGlobalCooldownRemaining(Priest), Is.Zero);
            Assert.That(
                mageSimulation.CanUseAbility(Priest, AbilityId.Fireball, out reason),
                Is.True);
            Assert.That(Snapshot(mageSimulation, Priest).Resource, Is.EqualTo(95f).Within(0.001f));

            CombatSimulation rogueSimulation = CreateSimulation();
            Add(
                rogueSimulation,
                Priest,
                1,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default);
            Add(
                rogueSimulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));
            Cast(rogueSimulation, Priest, 1, AbilityId.Mutilate, Enemy);

            CombatantSnapshot rogue = Snapshot(rogueSimulation, Priest);
            Assert.That(rogue.ResourceType, Is.EqualTo(ResourceType.Energy));
            Assert.That(rogue.Resource, Is.EqualTo(40f).Within(0.0001f));
            Assert.That(
                rogueSimulation.GetGlobalCooldownRemaining(Priest),
                Is.EqualTo(1f).Within(0.0001f));
            TickUntil(rogueSimulation, 31);
            Assert.That(
                rogueSimulation.CanUseAbility(Priest, AbilityId.Mutilate, out reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("resource"));
            Assert.That(rogueSimulation.GetGlobalCooldownRemaining(Priest), Is.Zero);
        }

        [Test]
        public void ArenaTrinket_IsOffGlobalCooldownAndRemovesOnlyControlAuras()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            ParticipantId livingBombMage = new ParticipantId(5);
            ParticipantId frostNovaMage = new ParticipantId(6);
            ParticipantId deepFreezeMage = new ParticipantId(7);
            ParticipantId dragonBreathMage = new ParticipantId(8);
            ParticipantId fearPriest = new ParticipantId(9);
            ParticipantId frostboltMage = new ParticipantId(10);
            Add(
                simulation,
                livingBombMage,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));
            Add(
                simulation,
                frostNovaMage,
                2,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(0.2f, 0f, 1f));
            Add(
                simulation,
                deepFreezeMage,
                2,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(0.4f, 0f, 1f));
            Add(
                simulation,
                fearPriest,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0.6f, 0f, 1f));
            Add(
                simulation,
                dragonBreathMage,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0.8f, 0f, 1f));
            Add(
                simulation,
                frostboltMage,
                2,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(1f, 0f, 1f));

            Assert.That(
                simulation.SubmitCommand(TargetCast(
                    livingBombMage,
                    1,
                    AbilityId.LivingBomb,
                    Priest)),
                Is.True);
            Assert.That(
                simulation.SubmitCommand(TargetCast(
                    frostNovaMage,
                    1,
                    AbilityId.FrostNova,
                    frostNovaMage)),
                Is.True);
            Assert.That(
                simulation.SubmitCommand(TargetCast(
                    deepFreezeMage,
                    1,
                    AbilityId.DeepFreeze,
                    Priest)),
                Is.True);
            Assert.That(
                simulation.SubmitCommand(TargetCast(
                    frostboltMage,
                    1,
                    AbilityId.Frostbolt,
                    Priest)),
                Is.True);
            simulation.Tick();
            TickUntil(simulation, 61);
            Assert.That(
                simulation.SubmitCommand(TargetCast(
                    dragonBreathMage,
                    1,
                    AbilityId.DragonBreath,
                    dragonBreathMage)),
                Is.True);
            Assert.That(
                simulation.SubmitCommand(TargetCast(
                    fearPriest,
                    1,
                    AbilityId.PsychicScream,
                    fearPriest)),
                Is.True);
            simulation.Tick();

            Assert.That(simulation.HasAura(Priest, AuraId.LivingEmber), Is.True);
            Assert.That(simulation.HasAura(Priest, AuraId.Fear), Is.True);
            Assert.That(simulation.HasAura(Priest, AuraId.Stun), Is.True);
            Assert.That(simulation.HasAura(Priest, AuraId.Disoriented), Is.True);
            Assert.That(simulation.HasAura(Priest, AuraId.FrozenRoot), Is.True);
            Assert.That(simulation.HasAura(Priest, AuraId.FrostSlow), Is.True);

            Cast(simulation, Priest, 1, AbilityId.ArenaTrinket, Priest);

            Assert.That(simulation.HasAura(Priest, AuraId.Fear), Is.False);
            Assert.That(simulation.HasAura(Priest, AuraId.Stun), Is.False);
            Assert.That(simulation.HasAura(Priest, AuraId.Disoriented), Is.False);
            Assert.That(simulation.HasAura(Priest, AuraId.FrozenRoot), Is.False);
            Assert.That(simulation.HasAura(Priest, AuraId.FrostSlow), Is.False);
            Assert.That(simulation.HasAura(Priest, AuraId.LivingEmber), Is.True);
            Assert.That(simulation.GetGlobalCooldownRemaining(Priest), Is.Zero);
            Assert.That(
                simulation.GetCooldownRemaining(Priest, AbilityId.ArenaTrinket),
                Is.EqualTo(120f).Within(simulation.Rules.TickSeconds));
        }

        [Test]
        public void SpecializationCooldowns_ModifyAuthoritativeCastDamageAndDefense()
        {
            CombatSimulation priestSimulation = CreateSimulation();
            Add(
                priestSimulation,
                Priest,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            Add(
                priestSimulation,
                Ally,
                1,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));
            Cast(priestSimulation, Priest, 1, AbilityId.PowerInfusion, Priest);
            Cast(priestSimulation, Priest, 2, AbilityId.FlashHeal, Ally);
            CombatantSnapshot infusedPriest = Snapshot(priestSimulation, Priest);
            Assert.That(priestSimulation.HasAura(Priest, AuraId.PowerInfusion), Is.True);
            Assert.That(
                infusedPriest.CastEndTick - infusedPriest.CastStartTick,
                Is.EqualTo(29));

            CombatSimulation fireSimulation = CreateSimulation();
            Add(
                fireSimulation,
                Priest,
                1,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            Add(
                fireSimulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 1f));
            Cast(fireSimulation, Priest, 1, AbilityId.Combustion, Priest);
            Cast(fireSimulation, Priest, 2, AbilityId.FireBlast, Enemy);
            Assert.That(Snapshot(fireSimulation, Enemy).Health, Is.EqualTo(85f));

            CombatSimulation frostSimulation = CreateSimulation();
            Add(
                frostSimulation,
                Priest,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                default);
            Add(
                frostSimulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 1f));
            Cast(frostSimulation, Priest, 1, AbilityId.IcyVeins, Priest);
            Cast(frostSimulation, Priest, 2, AbilityId.Frostbolt, Enemy);
            CombatantSnapshot icyMage = Snapshot(frostSimulation, Priest);
            Assert.That(frostSimulation.HasAura(Priest, AuraId.IcyVeins), Is.True);
            Assert.That(
                icyMage.CastEndTick - icyMage.CastStartTick,
                Is.EqualTo(42));

            CombatSimulation evasionSimulation = CreateSimulation();
            Assert.That(
                evasionSimulation.AddCombatant(
                    Priest,
                    new TeamId(1),
                    ClassId.Rogue,
                    default),
                Is.True);
            Add(
                evasionSimulation,
                Enemy,
                2,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                new Float3(0f, 0f, -1f));
            Assert.That(
                evasionSimulation.SubmitCommand(TargetCast(
                    Priest,
                    1,
                    AbilityId.Evasion,
                    Priest)),
                Is.True);
            Assert.That(
                evasionSimulation.SubmitCommand(TargetCast(
                    Enemy,
                    1,
                    AbilityId.Mutilate,
                    Priest)),
                Is.True);
            evasionSimulation.Tick();
            Assert.That(evasionSimulation.HasAura(Priest, AuraId.Evasion), Is.True);
            Assert.That(Snapshot(evasionSimulation, Priest).Health, Is.EqualTo(94.4f).Within(0.001f));

            CombatSimulation coldBloodSimulation = CreateSimulation();
            Add(
                coldBloodSimulation,
                Priest,
                1,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default);
            Add(
                coldBloodSimulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 1f));
            Cast(coldBloodSimulation, Priest, 1, AbilityId.ColdBlood, Priest);
            Cast(coldBloodSimulation, Priest, 2, AbilityId.Mutilate, Enemy);
            Assert.That(
                Snapshot(coldBloodSimulation, Enemy).Health,
                Is.EqualTo(82.5f).Within(0.001f));
        }

        [Test]
        public void CastsAndChannels_CancelWhenMovementBeginsWithoutResolving()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 2f));
            Cast(simulation, Priest, 1, AbilityId.Fireball, Enemy);

            Assert.That(
                Snapshot(simulation, Priest).CastingAbility,
                Is.EqualTo(AbilityId.Fireball));
            simulation.SubmitCommand(CombatCommand.Move(
                Priest,
                2,
                new Float3(1f, 0f, 0f),
                new Float3(0f, 0f, 1f)));
            simulation.Tick();

            CombatEvent interrupted = FindEvent(
                simulation,
                CombatEventKind.Interrupted,
                AbilityId.Fireball);
            Assert.That(interrupted.Target, Is.EqualTo(Priest));
            Assert.That(interrupted.Reason, Is.EqualTo("movement"));
            Assert.That(Snapshot(simulation, Priest).CastingAbility, Is.EqualTo(AbilityId.None));
            TickUntil(simulation, 61);
            Assert.That(Snapshot(simulation, Enemy).Health, Is.EqualTo(100f));
        }

        [Test]
        public void Penance_ProducesExactlyThreeRelationAwarePulses()
        {
            CombatSimulation allySimulation = CreateSimulation();
            Add(
                allySimulation,
                Priest,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            Add(
                allySimulation,
                Ally,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(0f, 0f, 1f));
            Add(
                allySimulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 2f));
            Add(
                allySimulation,
                EnemyTwo,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(1f, 0f, 2f));
            allySimulation.SubmitCommand(TargetCast(
                Enemy,
                1,
                AbilityId.FireBlast,
                Ally));
            allySimulation.SubmitCommand(TargetCast(
                EnemyTwo,
                1,
                AbilityId.ShadowWordDeath,
                Ally));
            allySimulation.Tick();
            Assert.That(Snapshot(allySimulation, Ally).Health, Is.EqualTo(66f));

            Cast(allySimulation, Priest, 1, AbilityId.Penance, Ally);
            int healPulses = CountEvents(
                allySimulation,
                CombatEventKind.Heal,
                AbilityId.Penance,
                Ally);
            while (allySimulation.CurrentTick < 42)
            {
                allySimulation.Tick();
                healPulses += CountEvents(
                    allySimulation,
                    CombatEventKind.Heal,
                    AbilityId.Penance,
                    Ally);
            }

            Assert.That(healPulses, Is.EqualTo(3));
            Assert.That(Snapshot(allySimulation, Ally).Health, Is.EqualTo(100f));

            CombatSimulation enemySimulation = CreateSimulation();
            Add(
                enemySimulation,
                Priest,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            Add(
                enemySimulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));
            Cast(enemySimulation, Priest, 1, AbilityId.Penance, Enemy);
            int damagePulses = CountEvents(
                enemySimulation,
                CombatEventKind.Damage,
                AbilityId.Penance,
                Enemy);
            while (enemySimulation.CurrentTick < 41)
            {
                enemySimulation.Tick();
                damagePulses += CountEvents(
                    enemySimulation,
                    CombatEventKind.Damage,
                    AbilityId.Penance,
                    Enemy);
            }

            Assert.That(damagePulses, Is.EqualTo(3));
            Assert.That(Snapshot(enemySimulation, Enemy).Health, Is.EqualTo(70f));
        }

        [Test]
        public void PowerWordShield_AppliesAbsorbAndBlocksRecastWithWeakenedSoul()
        {
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
                Ally,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(0f, 0f, 1f));
            Cast(simulation, Priest, 1, AbilityId.PowerWordShield, Ally);

            CombatantSnapshot shielded = Snapshot(simulation, Ally);
            Assert.That(shielded.Barrier, Is.EqualTo(30f).Within(0.0001f));
            Assert.That(simulation.HasAura(Ally, AuraId.SoulWard), Is.True);
            Assert.That(simulation.HasAura(Ally, AuraId.WeakenedSoul), Is.True);
            Assert.That(shielded.HasAuraTag(AuraTag.PreventsShield), Is.True);

            TickUntil(simulation, 121);
            Cast(simulation, Priest, 2, AbilityId.PowerWordShield, Ally);
            CombatEvent rejection = FindEvent(
                simulation,
                CombatEventKind.CastRejected,
                AbilityId.PowerWordShield);
            Assert.That(rejection.Reason, Is.EqualTo("weakened_soul"));

            TickUntil(simulation, 451);
            Assert.That(simulation.HasAura(Ally, AuraId.SoulWard), Is.False);
            Assert.That(simulation.HasAura(Ally, AuraId.WeakenedSoul), Is.False);
            Cast(simulation, Priest, 3, AbilityId.PowerWordShield, Ally);
            Assert.That(
                FindEvent(
                    simulation,
                    CombatEventKind.CastAccepted,
                    AbilityId.PowerWordShield).Target,
                Is.EqualTo(Ally));
        }

        [Test]
        public void PainSuppression_ReducesOtherwiseIdenticalDamageByFortyPercent()
        {
            ParticipantId control = new ParticipantId(5);
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
                Ally,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(0f, 0f, 1f));
            Add(
                simulation,
                control,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(1f, 0f, 1f));
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 2f));
            Add(
                simulation,
                EnemyTwo,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(1f, 0f, 2f));
            simulation.SubmitCommand(TargetCast(
                Priest,
                1,
                AbilityId.PainSuppression,
                Ally));
            simulation.SubmitCommand(TargetCast(
                Enemy,
                1,
                AbilityId.FireBlast,
                Ally));
            simulation.SubmitCommand(TargetCast(
                EnemyTwo,
                1,
                AbilityId.FireBlast,
                control));
            simulation.Tick();

            float protectedDamage = 100f - Snapshot(simulation, Ally).Health;
            float controlDamage = 100f - Snapshot(simulation, control).Health;
            Assert.That(controlDamage, Is.EqualTo(12f).Within(0.0001f));
            Assert.That(protectedDamage, Is.EqualTo(7.2f).Within(0.0001f));
            Assert.That(protectedDamage / controlDamage, Is.EqualTo(0.6f).Within(0.0001f));
            Assert.That(simulation.HasAura(Ally, AuraId.GuardianCovenant), Is.True);
        }

        [Test]
        public void DispelMagic_RemovesHarmfulMagicFromAlliesAndBenefitsFromEnemies()
        {
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
                Ally,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(0f, 0f, 1f));
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 2f));
            Add(
                simulation,
                EnemyTwo,
                2,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(1f, 0f, 2f));
            simulation.SubmitCommand(TargetCast(
                Enemy,
                1,
                AbilityId.LivingBomb,
                Ally));
            simulation.SubmitCommand(TargetCast(
                EnemyTwo,
                1,
                AbilityId.IceBarrier,
                EnemyTwo));
            simulation.Tick();
            Assert.That(simulation.HasAura(Ally, AuraId.LivingEmber), Is.True);
            Assert.That(simulation.HasAura(EnemyTwo, AuraId.IceWard), Is.True);

            Cast(simulation, Priest, 1, AbilityId.DispelMagic, Ally);
            Assert.That(simulation.HasAura(Ally, AuraId.LivingEmber), Is.False);
            Assert.That(simulation.HasAura(EnemyTwo, AuraId.IceWard), Is.True);

            TickUntil(simulation, 47);
            Cast(simulation, Priest, 2, AbilityId.DispelMagic, EnemyTwo);
            Assert.That(simulation.HasAura(EnemyTwo, AuraId.IceWard), Is.False);
        }

        [Test]
        public void Kick_InterruptsAndLocksOnlyTheActiveSpellSchool()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));
            Add(
                simulation,
                Ally,
                2,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                default);
            Cast(simulation, Ally, 1, AbilityId.Hemorrhage, Priest);
            simulation.SubmitCommand(TargetCast(
                Priest,
                1,
                AbilityId.Fireball,
                Ally));
            simulation.SubmitCommand(TargetCast(
                Ally,
                2,
                AbilityId.Kick,
                Priest));
            simulation.Tick();

            CombatantSnapshot interruptedMage = Snapshot(simulation, Priest);
            Assert.That(interruptedMage.CastingAbility, Is.EqualTo(AbilityId.None));
            Assert.That(
                interruptedMage.LockedSchools & SpellSchoolMask.Fire,
                Is.EqualTo(SpellSchoolMask.Fire));
            Assert.That(
                interruptedMage.LockedSchools & SpellSchoolMask.Arcane,
                Is.EqualTo(SpellSchoolMask.None));
            Assert.That(
                FindEvent(simulation, CombatEventKind.Interrupted, AbilityId.Kick).Target,
                Is.EqualTo(Priest));

            TickUntil(simulation, 47);
            Assert.That(
                simulation.CanUseAbility(Priest, AbilityId.Fireball, out string reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("school_locked"));
            Assert.That(
                simulation.CanUseAbility(Priest, AbilityId.Polymorph, out reason),
                Is.True);

            TickUntil(simulation, 152);
            Assert.That(Snapshot(simulation, Priest).LockedSchools, Is.EqualTo(SpellSchoolMask.None));
            Assert.That(
                simulation.CanUseAbility(Priest, AbilityId.Fireball, out reason),
                Is.True);
        }

        [Test]
        public void FearDiminishingReturns_ShortenThenImmunizeRepeatedControl()
        {
            ParticipantId fourthPriest = new ParticipantId(4);
            ParticipantId target = new ParticipantId(5);
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
                Ally,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0.2f, 0f, 0f));
            Add(
                simulation,
                Enemy,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(-0.2f, 0f, 0f));
            Add(
                simulation,
                fourthPriest,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0.4f, 0f, 0f));
            Add(
                simulation,
                target,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));

            Cast(simulation, Priest, 1, AbilityId.PsychicScream, target);
            Assert.That(simulation.GetAuraRemaining(target, AuraId.Fear),
                Is.EqualTo(8f).Within(0.0001f));
            Cast(simulation, Ally, 1, AbilityId.PsychicScream, target);
            Assert.That(simulation.GetAuraRemaining(target, AuraId.Fear),
                Is.EqualTo(4f).Within(0.0001f));
            Cast(simulation, Enemy, 1, AbilityId.PsychicScream, target);
            Assert.That(simulation.GetAuraRemaining(target, AuraId.Fear),
                Is.EqualTo(2f).Within(0.0001f));

            float beforeImmuneCast = simulation.GetAuraRemaining(target, AuraId.Fear);
            Cast(simulation, fourthPriest, 1, AbilityId.PsychicScream, target);
            Assert.That(
                CountEvents(
                    simulation,
                    CombatEventKind.StatusApplied,
                    AbilityId.PsychicScream,
                    target),
                Is.Zero);
            Assert.That(
                simulation.GetAuraRemaining(target, AuraId.Fear),
                Is.LessThan(beforeImmuneCast).And.GreaterThan(0f));
        }

        [Test]
        public void ShadowWordDeath_BacklashesOnlyWhenTheTargetSurvives()
        {
            CombatSimulation survivorSimulation = CreateSimulation();
            Add(
                survivorSimulation,
                Priest,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            Add(
                survivorSimulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));
            Cast(survivorSimulation, Priest, 1, AbilityId.ShadowWordDeath, Enemy);

            Assert.That(Snapshot(survivorSimulation, Enemy).Health, Is.EqualTo(78f));
            Assert.That(Snapshot(survivorSimulation, Priest).Health, Is.EqualTo(78f));
            Assert.That(
                CountEvents(
                    survivorSimulation,
                    CombatEventKind.Damage,
                    AbilityId.ShadowWordDeath),
                Is.EqualTo(2));

            CombatSimulation lethalSimulation = CreateSimulation();
            Add(
                lethalSimulation,
                Priest,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            Add(
                lethalSimulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f),
                20f);
            Cast(lethalSimulation, Priest, 1, AbilityId.ShadowWordDeath, Enemy);

            Assert.That(Snapshot(lethalSimulation, Enemy).IsAlive, Is.False);
            Assert.That(Snapshot(lethalSimulation, Priest).Health, Is.EqualTo(100f));
            Assert.That(
                CountEvents(
                    lethalSimulation,
                    CombatEventKind.Damage,
                    AbilityId.ShadowWordDeath),
                Is.EqualTo(1));
        }

        [Test]
        public void Shadowfiend_PulsesDamageAndReturnsManaToItsOwner()
        {
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
                new Float3(0f, 0f, 1f));

            uint sequence = 1;
            for (int castIndex = 0; castIndex < 4; castIndex++)
            {
                Cast(
                    simulation,
                    Priest,
                    sequence++,
                    AbilityId.DispelMagic,
                    Priest);
                TickUntil(
                    simulation,
                    Snapshot(simulation, Priest).GlobalCooldownEndTick);
            }

            Cast(simulation, Priest, sequence, AbilityId.Shadowfiend, Enemy);
            int firstPulseTick = simulation.CurrentTick + 45;
            TickUntil(simulation, firstPulseTick - 1);
            float manaBeforePulse = Snapshot(simulation, Priest).Resource;
            float healthBeforePulse = Snapshot(simulation, Enemy).Health;
            simulation.Tick();

            float expectedManaGain = 5f +
                SpecializationCatalog.GetResourceProfile(
                    SpecializationId.Discipline).RegenerationPerSecond *
                simulation.Rules.TickSeconds;
            Assert.That(
                Snapshot(simulation, Priest).Resource - manaBeforePulse,
                Is.EqualTo(expectedManaGain).Within(0.001f));
            Assert.That(
                healthBeforePulse - Snapshot(simulation, Enemy).Health,
                Is.EqualTo(8f).Within(0.0001f));
            Assert.That(
                FindEvent(simulation, CombatEventKind.Damage, AbilityId.Shadowfiend).Target,
                Is.EqualTo(Enemy));
        }

        [Test]
        public void RogueMechanics_BindComboToTargetAndGatePoisonAndStealthActions()
        {
            CombatSimulation poisonSimulation = CreateSimulation();
            Add(
                poisonSimulation,
                Priest,
                1,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default);
            Add(
                poisonSimulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));
            Add(
                poisonSimulation,
                EnemyTwo,
                2,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(0.5f, 0f, 1.5f));

            Cast(poisonSimulation, Priest, 1, AbilityId.Shiv, Enemy);
            Assert.That(poisonSimulation.HasAura(Enemy, AuraId.Poison), Is.True);
            Assert.That(poisonSimulation.GetAuraStacks(Enemy, AuraId.Poison), Is.EqualTo(1));
            Assert.That(Snapshot(poisonSimulation, Priest).ComboPoints, Is.EqualTo(1));
            Assert.That(Snapshot(poisonSimulation, Priest).ComboTarget, Is.EqualTo(Enemy));

            TickUntil(poisonSimulation, 31);
            Cast(poisonSimulation, Priest, 2, AbilityId.Mutilate, Enemy);
            Assert.That(Snapshot(poisonSimulation, Priest).ComboPoints, Is.EqualTo(3));
            Assert.That(Snapshot(poisonSimulation, Enemy).Health, Is.EqualTo(75f));

            TickUntil(poisonSimulation, 62);
            Cast(poisonSimulation, Priest, 3, AbilityId.Envenom, EnemyTwo);
            Assert.That(
                FindEvent(
                    poisonSimulation,
                    CombatEventKind.CastRejected,
                    AbilityId.Envenom).Reason,
                Is.EqualTo("combo_points"));
            Cast(poisonSimulation, Priest, 4, AbilityId.Envenom, Enemy);
            Assert.That(Snapshot(poisonSimulation, Priest).ComboPoints, Is.Zero);
            Assert.That(Snapshot(poisonSimulation, Enemy).Health, Is.EqualTo(45f));
            Assert.That(poisonSimulation.HasAura(Priest, AuraId.VenomRush), Is.True);

            CombatSimulation stealthSimulation = CreateSimulation();
            Add(
                stealthSimulation,
                Priest,
                1,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                default);
            Add(
                stealthSimulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));
            Assert.That(stealthSimulation.HasAura(Priest, AuraId.Stealth), Is.True);
            Cast(stealthSimulation, Priest, 1, AbilityId.Ambush, Enemy);
            Assert.That(stealthSimulation.HasAura(Priest, AuraId.Stealth), Is.False);
            Assert.That(Snapshot(stealthSimulation, Enemy).Health, Is.EqualTo(76f));
            Assert.That(Snapshot(stealthSimulation, Priest).ComboPoints, Is.EqualTo(2));

            TickUntil(stealthSimulation, 179);
            Cast(stealthSimulation, Priest, 2, AbilityId.ShadowDance, Priest);
            Assert.That(stealthSimulation.HasAura(Priest, AuraId.NightfallDance), Is.True);
            Cast(stealthSimulation, Priest, 3, AbilityId.Ambush, Enemy);
            Assert.That(Snapshot(stealthSimulation, Enemy).Health, Is.EqualTo(52f));
            Assert.That(Snapshot(stealthSimulation, Priest).ComboPoints, Is.EqualTo(4));
        }

        [Test]
        public void Stealth_HidesRogueFromHostileUnitCastsAndBreaksOnDamage()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Rogue,
                SpecializationId.Mutilate,
                default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 2f));

            Cast(simulation, Priest, 1, AbilityId.Vanish, Priest);
            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.True);

            CombatCommand hiddenTargetCast = TargetCast(
                Enemy,
                1,
                AbilityId.FireBlast,
                Priest);
            Assert.That(
                simulation.CanStartAbility(hiddenTargetCast, out string reason),
                Is.False);
            Assert.That(reason, Is.EqualTo("target_hidden"));

            simulation.SubmitCommand(hiddenTargetCast);
            simulation.Tick();
            Assert.That(
                FindEvent(
                    simulation,
                    CombatEventKind.CastRejected,
                    AbilityId.FireBlast).Reason,
                Is.EqualTo("target_hidden"));

            Cast(simulation, Enemy, 2, AbilityId.DragonBreath, Enemy);
            Assert.That(simulation.HasAura(Priest, AuraId.Stealth), Is.False);
        }

        [Test]
        public void Kick_RevealsAInitiallyStealthedRogue()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));
            Add(
                simulation,
                Ally,
                1,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                default);
            Add(
                simulation,
                EnemyTwo,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 2f));
            Assert.That(simulation.HasAura(Ally, AuraId.Stealth), Is.True);

            simulation.SubmitCommand(TargetCast(
                Priest,
                1,
                AbilityId.Fireball,
                EnemyTwo));
            simulation.SubmitCommand(TargetCast(
                Ally,
                1,
                AbilityId.Kick,
                Priest));
            simulation.Tick();

            Assert.That(simulation.HasAura(Ally, AuraId.Stealth), Is.False);
            Assert.That(
                FindEvent(simulation, CombatEventKind.Interrupted, AbilityId.Kick).Target,
                Is.EqualTo(Priest));
        }

        [Test]
        public void FrostSetup_EnablesDeepFreezeAndTheFrozenIceLanceBonus()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 1f));

            Cast(simulation, Priest, 1, AbilityId.DeepFreeze, Enemy);
            Assert.That(
                FindEvent(
                    simulation,
                    CombatEventKind.CastRejected,
                    AbilityId.DeepFreeze).Reason,
                Is.EqualTo("target_not_frozen"));
            Cast(simulation, Priest, 2, AbilityId.FrostNova, Enemy);
            Assert.That(simulation.HasAura(Enemy, AuraId.FrozenRoot), Is.True);
            Assert.That(Snapshot(simulation, Enemy).Health, Is.EqualTo(90f));

            TickUntil(simulation, 47);
            Cast(simulation, Priest, 3, AbilityId.DeepFreeze, Enemy);
            Assert.That(simulation.HasAura(Enemy, AuraId.Stun), Is.True);

            TickUntil(simulation, 93);
            Cast(simulation, Priest, 4, AbilityId.IceLance, Enemy);
            Assert.That(Snapshot(simulation, Enemy).Health, Is.EqualTo(66f));
            Assert.That(
                CountEvents(
                    simulation,
                    CombatEventKind.Damage,
                    AbilityId.IceLance,
                    Enemy),
                Is.EqualTo(2));
        }

        [Test]
        public void CastPreflight_IsTargetAwareAndDoesNotMutateCombatState()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 2f));

            CombatCommand command = TargetCast(
                Priest,
                1,
                AbilityId.DeepFreeze,
                Enemy);
            CombatantSnapshot before = Snapshot(simulation, Priest);
            int tickBefore = simulation.CurrentTick;
            int eventsBefore = simulation.EventCount;

            Assert.That(simulation.CanStartAbility(command, out string reason), Is.False);
            Assert.That(reason, Is.EqualTo("target_not_frozen"));
            Assert.That(simulation.CurrentTick, Is.EqualTo(tickBefore));
            Assert.That(simulation.EventCount, Is.EqualTo(eventsBefore));
            Assert.That(Snapshot(simulation, Priest).Resource, Is.EqualTo(before.Resource));
            Assert.That(
                simulation.GetCooldownRemaining(Priest, AbilityId.DeepFreeze),
                Is.Zero);

            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();
            Assert.That(
                FindEvent(
                    simulation,
                    CombatEventKind.CastRejected,
                    AbilityId.DeepFreeze).Reason,
                Is.EqualTo(reason));
        }

        [Test]
        public void LivingBomb_TicksPeriodicallyAndExplodesAtExpiration()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Priest,
                1,
                ClassId.Mage,
                SpecializationId.Fire,
                default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 1f));
            Add(
                simulation,
                EnemyTwo,
                2,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(1f, 0f, 1f));
            Cast(simulation, Priest, 1, AbilityId.LivingBomb, Enemy);
            Assert.That(simulation.HasAura(Enemy, AuraId.LivingEmber), Is.True);
            Assert.That(Snapshot(simulation, Enemy).Health, Is.EqualTo(100f));

            TickUntil(simulation, 90);
            Assert.That(Snapshot(simulation, Enemy).Health, Is.EqualTo(100f));
            simulation.Tick();
            Assert.That(Snapshot(simulation, Enemy).Health, Is.EqualTo(95f));

            TickUntil(simulation, 360);
            float targetHealthBeforeExpiration = Snapshot(simulation, Enemy).Health;
            float nearbyHealthBeforeExpiration = Snapshot(simulation, EnemyTwo).Health;
            Assert.That(simulation.HasAura(Enemy, AuraId.LivingEmber), Is.True);
            simulation.Tick();

            Assert.That(simulation.HasAura(Enemy, AuraId.LivingEmber), Is.False);
            Assert.That(
                Snapshot(simulation, Enemy).Health,
                Is.LessThan(targetHealthBeforeExpiration));
            Assert.That(
                nearbyHealthBeforeExpiration - Snapshot(simulation, EnemyTwo).Health,
                Is.EqualTo(14f).Within(0.0001f));
        }

        private static void AssertKit(
            SpecializationId specialization,
            params AbilityId[] expected)
        {
            Assert.That(expected, Has.Length.EqualTo(AbilityCatalog.AbilitySlotCount));
            AbilityKit kit = SpecializationCatalog.GetKit(specialization);
            Assert.That(kit.Count, Is.EqualTo(AbilityCatalog.AbilitySlotCount));
            for (int slot = 0; slot < expected.Length; slot++)
            {
                AbilityId ability = AbilityCatalog.GetSpecializationAbility(
                    specialization,
                    slot);
                Assert.That(ability, Is.EqualTo(expected[slot]), specialization + " slot " + slot);
                Assert.That(kit.GetAbility(slot), Is.EqualTo(expected[slot]));
                Assert.That(
                    AbilityCatalog.IsAbilityAllowed(
                        kit.OwnerClass,
                        specialization,
                        ability),
                    Is.True);
            }
        }

        private static void AssertSharedAction(
            ClassId classId,
            AbilityId ability,
            SpecializationId first,
            SpecializationId second)
        {
            Assert.That(AbilityCatalog.Get(ability).OwnerSpecialization, Is.EqualTo(SpecializationId.None));
            Assert.That(AbilityCatalog.IsAbilityAllowed(classId, first, ability), Is.True);
            Assert.That(AbilityCatalog.IsAbilityAllowed(classId, second, ability), Is.True);
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
            Float3 position,
            float maximumHealth = 100f)
        {
            Assert.That(
                simulation.AddCombatant(
                    id,
                    new TeamId(team),
                    classId,
                    specialization,
                    position,
                    maximumHealth),
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
            ParticipantId id)
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
                "Expected " + kind + " for " + ability +
                " at tick " + simulation.CurrentTick + ".");
            return default;
        }

        private static int CountEvents(
            CombatSimulation simulation,
            CombatEventKind kind,
            AbilityId ability,
            ParticipantId target = default)
        {
            int count = 0;
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind && combatEvent.Ability == ability &&
                    (!target.IsValid || combatEvent.Target == target))
                {
                    count++;
                }
            }

            return count;
        }
    }
}
