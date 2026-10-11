using System.Collections.Generic;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class SwipeArenaAbilityTests
    {
        private static readonly ParticipantId Player = new ParticipantId(1);
        private static readonly ParticipantId Ally = new ParticipantId(2);
        private static readonly ParticipantId Enemy = new ParticipantId(3);

        [Test]
        public void PlayableKits_ExposeTwoGestureFourCommonAndFourUtilityControls()
        {
            Assert.That(AbilityKit.GestureSlotCount, Is.EqualTo(2));
            Assert.That(AbilityKit.CommonSlotCount, Is.EqualTo(4));
            Assert.That(AbilityKit.UtilitySlotCount, Is.EqualTo(4));
            Assert.That(AbilityKit.Capacity, Is.EqualTo(10));
            Assert.That(AbilityKit.MaximumActionCount, Is.EqualTo(18));
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

            for (int index = 1; index < AbilityCatalog.StorageCapacity; index++)
            {
                AbilityId ability = (AbilityId)index;
                Assert.That(AbilityCatalog.Get(ability).Id, Is.EqualTo(ability));
            }

            for (int specializationIndex = 0;
                 specializationIndex < specializations.Length;
                 specializationIndex++)
            {
                SpecializationId specialization = specializations[specializationIndex];
                SpecializationDefinition specializationDefinition =
                    SpecializationCatalog.GetDefinition(specialization);
                AbilityKit kit = SpecializationCatalog.GetKit(specialization);
                var seen = new HashSet<AbilityId>();
                Assert.That(kit.Count, Is.EqualTo(AbilityKit.Capacity));
                Assert.That(kit.AbilityCount, Is.EqualTo(AbilityKit.MaximumActionCount));
                Assert.That(
                    kit.GetAbility(AbilityKit.Capacity - 1),
                    Is.EqualTo(AbilityId.ArenaTrinket));

                for (int slot = 0; slot < kit.Count; slot++)
                {
                    AbilitySlotBinding binding = kit.GetSlotBinding(slot);
                    if (slot < AbilityKit.GestureSlotCount)
                    {
                        Assert.That(
                            binding.Count,
                            Is.EqualTo(5),
                            specialization + " group slot " + slot +
                            " must expose tap plus four cardinal flick actions");
                    }
                    else
                    {
                        Assert.That(binding.Up, Is.EqualTo(AbilityId.None));
                        Assert.That(binding.Down, Is.EqualTo(AbilityId.None));
                        Assert.That(binding.Left, Is.EqualTo(AbilityId.None));
                        Assert.That(binding.Right, Is.EqualTo(AbilityId.None));
                    }

                    AssertOwnedUnique(binding.Primary);
                    AssertOwnedUnique(binding.Up);
                    AssertOwnedUnique(binding.Down);
                    AssertOwnedUnique(binding.Left);
                    AssertOwnedUnique(binding.Right);
                }

                void AssertOwnedUnique(AbilityId ability)
                {
                    if (ability == AbilityId.None)
                    {
                        return;
                    }

                    AbilityDefinition definition = AbilityCatalog.Get(ability);
                    Assert.That(
                        definition.OwnerClass == ClassId.None ||
                        definition.OwnerClass == specializationDefinition.OwnerClass,
                        Is.True,
                        specialization + " owns incompatible " + ability);
                    Assert.That(
                        definition.OwnerSpecialization == SpecializationId.None ||
                        definition.OwnerSpecialization == specialization,
                        Is.True,
                        specialization + " exposes stale spec-owned " + ability);
                    Assert.That(seen.Add(ability), Is.True, ability + " is duplicated");
                }
            }
        }

        [Test]
        public void PlayableKits_KeepTheAuthoredStableAbilityMapping()
        {
            AssertActionOrder(
                SpecializationId.Discipline,
                AbilityId.Renew,
                AbilityId.PrayerOfMending,
                AbilityId.FlashHeal,
                AbilityId.PowerWordShield,
                AbilityId.PainSuppression,
                AbilityId.PsychicScream,
                AbilityId.ManaBurn,
                AbilityId.ShadowWordPain,
                AbilityId.MassDispel,
                AbilityId.FearWard,
                AbilityId.Penance,
                AbilityId.Smite,
                AbilityId.ShadowWordDeath,
                AbilityId.DispelMagic,
                AbilityId.Shadowfiend,
                AbilityId.InnerFocus,
                AbilityId.PowerInfusion,
                AbilityId.ArenaTrinket);
            AssertActionOrder(
                SpecializationId.Fire,
                AbilityId.DragonBreath,
                AbilityId.Polymorph,
                AbilityId.FrostNova,
                AbilityId.ConeOfCold,
                AbilityId.BlastWave,
                AbilityId.ManaShield,
                AbilityId.Spellsteal,
                AbilityId.MoltenArmor,
                AbilityId.IceBlock,
                AbilityId.Blink,
                AbilityId.Fireball,
                AbilityId.FireBlast,
                AbilityId.LivingBomb,
                AbilityId.Scorch,
                AbilityId.Counterspell,
                AbilityId.Pyroblast,
                AbilityId.Combustion,
                AbilityId.ArenaTrinket);
            AssertActionOrder(
                SpecializationId.Frost,
                AbilityId.Polymorph,
                AbilityId.ConeOfCold,
                AbilityId.WaterElementalFreeze,
                AbilityId.FrostNova,
                AbilityId.DeepFreeze,
                AbilityId.IceBarrier,
                AbilityId.ManaShield,
                AbilityId.IceBlock,
                AbilityId.Blink,
                AbilityId.ColdSnap,
                AbilityId.Frostbolt,
                AbilityId.IceLance,
                AbilityId.Counterspell,
                AbilityId.Spellsteal,
                AbilityId.IcyVeins,
                AbilityId.MageArmor,
                AbilityId.IceArmor,
                AbilityId.ArenaTrinket);
            AssertActionOrder(
                SpecializationId.ShadowDance,
                AbilityId.CheapShot,
                AbilityId.Garrote,
                AbilityId.Gouge,
                AbilityId.Blind,
                AbilityId.KidneyShot,
                AbilityId.Shadowstep,
                AbilityId.Sprint,
                AbilityId.Vanish,
                AbilityId.Evasion,
                AbilityId.Stealth,
                AbilityId.Hemorrhage,
                AbilityId.Ambush,
                AbilityId.Eviscerate,
                AbilityId.Kick,
                AbilityId.CloakOfShadows,
                AbilityId.ShadowDance,
                AbilityId.Preparation,
                AbilityId.ArenaTrinket);
            AssertActionOrder(
                SpecializationId.Mutilate,
                AbilityId.KidneyShot,
                AbilityId.CheapShot,
                AbilityId.Gouge,
                AbilityId.Blind,
                AbilityId.Garrote,
                AbilityId.Vanish,
                AbilityId.CloakOfShadows,
                AbilityId.Evasion,
                AbilityId.Sprint,
                AbilityId.Stealth,
                AbilityId.Mutilate,
                AbilityId.Envenom,
                AbilityId.Shiv,
                AbilityId.Eviscerate,
                AbilityId.Kick,
                AbilityId.ColdBlood,
                AbilityId.Preparation,
                AbilityId.ArenaTrinket);
            AssertActionOrder(
                SpecializationId.Shadow,
                AbilityId.DevouringPlague,
                AbilityId.ShadowWordDeath,
                AbilityId.ManaBurn,
                AbilityId.ShadowWordPain,
                AbilityId.MindBlast,
                AbilityId.PsychicScream,
                AbilityId.Silence,
                AbilityId.PsychicHorror,
                AbilityId.Fade,
                AbilityId.Dispersion,
                AbilityId.VampiricTouch,
                AbilityId.MindFlay,
                AbilityId.PowerWordShield,
                AbilityId.DispelMagic,
                AbilityId.Shadowfiend,
                AbilityId.MassDispel,
                AbilityId.Shadowform,
                AbilityId.ArenaTrinket);
            AssertActionOrder(
                SpecializationId.Marksmanship,
                AbilityId.SerpentSting,
                AbilityId.KillShot,
                AbilityId.ScorpidSting,
                AbilityId.TranquilizingShot,
                AbilityId.ConcussiveShot,
                AbilityId.ScatterShot,
                AbilityId.FreezingArrow,
                AbilityId.FrostTrap,
                AbilityId.SilencingShot,
                AbilityId.Disengage,
                AbilityId.ChimeraShot,
                AbilityId.AimedShot,
                AbilityId.ArcaneShot,
                AbilityId.SteadyShot,
                AbilityId.PetAttack,
                AbilityId.RapidFire,
                AbilityId.Deterrence,
                AbilityId.ArenaTrinket);
        }

        [Test]
        public void Hunter_CompactPaletteRetainsTwoCompleteGestureFamiliesAndDocumentsCuts()
        {
            AbilityKit kit = SpecializationCatalog.GetKit(
                SpecializationId.Marksmanship);

            Assert.That(kit.GetAbility(0), Is.EqualTo(AbilityId.SerpentSting));
            Assert.That(
                kit.GetAbility(0, AbilitySlotDirection.Up),
                Is.EqualTo(AbilityId.KillShot));
            Assert.That(
                kit.GetAbility(0, AbilitySlotDirection.Down),
                Is.EqualTo(AbilityId.ScorpidSting));
            Assert.That(
                kit.GetAbility(0, AbilitySlotDirection.Left),
                Is.EqualTo(AbilityId.TranquilizingShot));
            Assert.That(
                kit.GetAbility(0, AbilitySlotDirection.Right),
                Is.EqualTo(AbilityId.ConcussiveShot));
            Assert.That(kit.Contains(AbilityId.FreezingArrow), Is.True);
            Assert.That(kit.Contains(AbilityId.FrostTrap), Is.True);
            Assert.That(kit.Contains(AbilityId.SilencingShot), Is.True);
            Assert.That(kit.Contains(AbilityId.Disengage), Is.True);
            Assert.That(kit.Contains(AbilityId.Deterrence), Is.True);
            Assert.That(kit.Contains(AbilityId.RapidFire), Is.True);

            Assert.That(kit.Contains(AbilityId.SteadyShot), Is.True);
            Assert.That(kit.Contains(AbilityId.Flare), Is.False);
            Assert.That(kit.Contains(AbilityId.FeignDeath), Is.False);
            Assert.That(kit.Contains(AbilityId.MastersCall), Is.False);
            Assert.That(kit.Contains(AbilityId.WingClip), Is.False);
            Assert.That(kit.Contains(AbilityId.AspectOfTheDragonhawk), Is.False);
            Assert.That(kit.Contains(AbilityId.AspectOfTheViper), Is.False);
            Assert.That(kit.Contains(AbilityId.RoarOfSacrifice), Is.False);
            Assert.That(kit.Contains(AbilityId.SpiderWeb), Is.False);
            Assert.That(kit.Contains(AbilityId.Readiness), Is.False);
            Assert.That(kit.Contains(AbilityId.ViperSting), Is.False);
            Assert.That(kit.Contains(AbilityId.HuntersMark), Is.False);
            Assert.That(kit.Contains(AbilityId.PetFollow), Is.False);
            Assert.That(kit.Contains(AbilityId.PetStay), Is.False);
            Assert.That(kit.Contains(AbilityId.MendPet), Is.False);
            Assert.That(kit.AbilityCount, Is.EqualTo(18));
        }

        [Test]
        public void Hunter_TranquilizingShotRemovesMagicAndEnrageEffects()
        {
            AbilityEffectSlice tranquilizing = AbilityEffectCatalog.TranquilizingShot;
            Assert.That(tranquilizing.Count, Is.EqualTo(2));
            Assert.That(
                AbilityEffectCatalog.Get(tranquilizing.Offset).DispelType,
                Is.EqualTo(DispelType.Magic));
            Assert.That(
                AbilityEffectCatalog.Get(tranquilizing.Offset + 1).DispelType,
                Is.EqualTo(DispelType.Enrage));

            CombatSimulation purge = CreateSimulation();
            Add(
                purge,
                Player,
                1,
                ClassId.Hunter,
                SpecializationId.Marksmanship,
                default);
            Add(
                purge,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 8f));
            Cast(purge, SelfCast(Enemy, 1, AbilityId.MoltenArmor));
            Assert.That(
                Snapshot(purge, Enemy).HasDispellableBeneficialAura(DispelType.Magic),
                Is.True);
            Cast(purge, UnitCast(Player, 1, AbilityId.TranquilizingShot, Enemy));
            Assert.That(purge.HasAura(Enemy, AuraId.MoltenArmor), Is.False);
            Assert.That(
                FindAuraEvent(
                    purge,
                    CombatEventKind.AuraRemoved,
                    AbilityId.TranquilizingShot,
                    AuraId.MoltenArmor),
                Is.True);
        }

        [Test]
        public void Priest_FearWardConsumesOnFear_AndPrayerHealsTheNextHit()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Player,
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
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 2f));

            Cast(simulation, UnitCast(Player, 1, AbilityId.FearWard, Ally));
            Cast(simulation, SelfCast(Enemy, 1, AbilityId.PsychicScream));
            Assert.That(simulation.HasAura(Ally, AuraId.Fear), Is.False);
            Assert.That(simulation.HasAura(Ally, AuraId.FearWard), Is.False);

            while (simulation.HasAura(Player, AuraId.Fear))
            {
                simulation.Tick();
            }

            TickUntilGlobalReady(simulation, Player);
            Cast(simulation, UnitCast(Player, 2, AbilityId.PrayerOfMending, Ally));
            TickUntilGlobalReady(simulation, Enemy);
            Cast(simulation, UnitCast(Enemy, 2, AbilityId.ShadowWordDeath, Ally));

            Assert.That(simulation.HasAura(Ally, AuraId.PrayerOfMending), Is.False);
            Assert.That(Snapshot(simulation, Ally).Health, Is.EqualTo(94f).Within(0.001f));

            CombatSimulation lethal = CreateSimulation();
            Add(
                lethal,
                Player,
                1,
                ClassId.Priest,
                SpecializationId.Discipline,
                default);
            Add(
                lethal,
                Ally,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(0f, 0f, 1f),
                15f);
            Add(
                lethal,
                Enemy,
                2,
                ClassId.Priest,
                SpecializationId.Discipline,
                new Float3(0f, 0f, 2f));

            Cast(lethal, UnitCast(Player, 1, AbilityId.PrayerOfMending, Ally));
            Cast(lethal, UnitCast(Enemy, 1, AbilityId.ShadowWordDeath, Ally));
            Assert.That(Snapshot(lethal, Ally).IsAlive, Is.False);
            Assert.That(Snapshot(lethal, Ally).Health, Is.Zero);
            Assert.That(
                FindEvent(lethal, CombatEventKind.Heal, AbilityId.PrayerOfMending),
                Is.False,
                "Reactive healing must not rescue a target after lethal damage.");
        }

        [Test]
        public void Mage_SpellstealTransfersBuff_ArmorsAreExclusive_AndColdSnapResetsFrost()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Player,
                1,
                ClassId.Mage,
                SpecializationId.Frost,
                default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 3f));

            Cast(simulation, SelfCast(Player, 1, AbilityId.IceArmor));
            Cast(simulation, SelfCast(Enemy, 1, AbilityId.MoltenArmor));
            Assert.That(
                Snapshot(simulation, Enemy).HasDispellableBeneficialAura(DispelType.Magic),
                Is.True);
            TickUntilGlobalReady(simulation, Player);
            Cast(simulation, UnitCast(Player, 2, AbilityId.Spellsteal, Enemy));

            Assert.That(simulation.HasAura(Enemy, AuraId.MoltenArmor), Is.False);
            Assert.That(simulation.HasAura(Player, AuraId.MoltenArmor), Is.True);
            Assert.That(simulation.HasAura(Player, AuraId.IceArmor), Is.False);
            Assert.That(
                FindAuraEvent(
                    simulation,
                    CombatEventKind.AuraRemoved,
                    AbilityId.Spellsteal,
                    AuraId.MoltenArmor),
                Is.True);
            Assert.That(
                FindAuraEvent(
                    simulation,
                    CombatEventKind.AuraApplied,
                    AbilityId.Spellsteal,
                    AuraId.MoltenArmor),
                Is.True);

            TickUntilGlobalReady(simulation, Player);
            Cast(simulation, SelfCast(Player, 3, AbilityId.FrostNova));
            Assert.That(
                simulation.GetCooldownRemaining(Player, AbilityId.FrostNova),
                Is.GreaterThan(0f));
            Cast(simulation, SelfCast(Player, 4, AbilityId.ColdSnap));
            Assert.That(
                simulation.GetCooldownRemaining(Player, AbilityId.FrostNova),
                Is.Zero);
            Assert.That(
                simulation.GetCooldownRemaining(Player, AbilityId.ColdSnap),
                Is.GreaterThan(0f));
        }

        [Test]
        public void Rogue_GarroteBleeds_AndPreparationResetsSprint()
        {
            CombatSimulation simulation = CreateSimulation();
            Add(
                simulation,
                Player,
                1,
                ClassId.Rogue,
                SpecializationId.ShadowDance,
                default);
            Add(
                simulation,
                Enemy,
                2,
                ClassId.Mage,
                SpecializationId.Fire,
                new Float3(0f, 0f, 1f));

            Cast(simulation, UnitCast(Player, 1, AbilityId.Garrote, Enemy));
            Assert.That(simulation.HasAura(Enemy, AuraId.GarroteBleed), Is.True);
            Assert.That(Snapshot(simulation, Player).ComboPoints, Is.EqualTo(1));

            Cast(simulation, SelfCast(Player, 2, AbilityId.Sprint));
            Assert.That(
                simulation.GetCooldownRemaining(Player, AbilityId.Sprint),
                Is.GreaterThan(0f));
            Cast(simulation, SelfCast(Player, 3, AbilityId.Preparation));
            Assert.That(simulation.GetCooldownRemaining(Player, AbilityId.Sprint), Is.Zero);
            Assert.That(
                simulation.GetCooldownRemaining(Player, AbilityId.Preparation),
                Is.GreaterThan(0f));
        }

        private static CombatSimulation CreateSimulation() =>
            new CombatSimulation(new CombatRules(30, 0f, 15f, 20f, 24f));

        private static void AssertActionOrder(
            SpecializationId specialization,
            params AbilityId[] expected)
        {
            Assert.That(expected, Has.Length.EqualTo(AbilityKit.MaximumActionCount));
            AbilityKit kit = SpecializationCatalog.GetKit(specialization);
            int action = 0;
            for (int slot = 0; slot < AbilityKit.GestureSlotCount; slot++)
            {
                AbilitySlotBinding binding = kit.GetSlotBinding(slot);
                Assert.That(binding.Primary, Is.EqualTo(expected[action++]));
                Assert.That(binding.Up, Is.EqualTo(expected[action++]));
                Assert.That(binding.Down, Is.EqualTo(expected[action++]));
                Assert.That(binding.Left, Is.EqualTo(expected[action++]));
                Assert.That(binding.Right, Is.EqualTo(expected[action++]));
            }

            for (int slot = AbilityKit.GestureSlotCount;
                 slot < AbilityKit.Capacity;
                 slot++)
            {
                Assert.That(kit.GetAbility(slot), Is.EqualTo(expected[action++]));
            }

            Assert.That(action, Is.EqualTo(expected.Length));
        }

        private static void Add(
            CombatSimulation simulation,
            ParticipantId id,
            int team,
            ClassId classId,
            SpecializationId specialization,
            Float3 position,
            float maxHealth = 100f)
        {
            Assert.That(
                simulation.AddCombatant(
                    id,
                    new TeamId(team),
                    classId,
                    specialization,
                    position,
                    maxHealth),
                Is.True);
        }

        private static void Cast(CombatSimulation simulation, CombatCommand command)
        {
            Assert.That(simulation.SubmitCommand(command), Is.True);
            simulation.Tick();
            Assert.That(
                FindEvent(simulation, CombatEventKind.CastAccepted, command.Ability),
                Is.True,
                command.Ability + " was not accepted");
        }

        private static CombatCommand SelfCast(
            ParticipantId actor,
            uint sequence,
            AbilityId ability) =>
            UnitCast(actor, sequence, ability, actor);

        private static CombatCommand UnitCast(
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
                point.PlanarNormalized);
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

        private static void TickUntilGlobalReady(
            CombatSimulation simulation,
            ParticipantId actor)
        {
            while (simulation.GetGlobalCooldownRemaining(actor) > 0f)
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

        private static bool FindAuraEvent(
            CombatSimulation simulation,
            CombatEventKind kind,
            AbilityId ability,
            AuraId aura)
        {
            for (int index = 0; index < simulation.EventCount; index++)
            {
                CombatEvent combatEvent = simulation.GetEvent(index);
                if (combatEvent.Kind == kind &&
                    combatEvent.Ability == ability &&
                    combatEvent.Aura == aura)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
