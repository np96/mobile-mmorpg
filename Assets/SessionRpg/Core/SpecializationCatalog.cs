using System;

namespace SessionRpg.Core
{
    public enum AbilitySlotDirection
    {
        None = 0,
        Up = 1,
        Down = 2,
        Left = 3,
        Right = 4,
    }

    public readonly struct AbilitySlotBinding
    {
        public AbilitySlotBinding(
            AbilityId primary,
            AbilityId up = AbilityId.None,
            AbilityId down = AbilityId.None,
            AbilityId left = AbilityId.None,
            AbilityId right = AbilityId.None)
        {
            Primary = primary;
            Up = up;
            Down = down;
            Left = left;
            Right = right;
        }

        public AbilityId Primary { get; }

        public AbilityId Up { get; }

        public AbilityId Down { get; }

        public AbilityId Left { get; }

        public AbilityId Right { get; }

        public int Count =>
            (Primary == AbilityId.None ? 0 : 1) +
            (Up == AbilityId.None ? 0 : 1) +
            (Down == AbilityId.None ? 0 : 1) +
            (Left == AbilityId.None ? 0 : 1) +
            (Right == AbilityId.None ? 0 : 1);

        public AbilityId GetAbility(AbilitySlotDirection direction)
        {
            switch (direction)
            {
                case AbilitySlotDirection.None:
                    return Primary;
                case AbilitySlotDirection.Up:
                    return Up;
                case AbilitySlotDirection.Down:
                    return Down;
                case AbilitySlotDirection.Left:
                    return Left;
                case AbilitySlotDirection.Right:
                    return Right;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }

        public bool Contains(AbilityId ability)
        {
            return ability != AbilityId.None &&
                (Primary == ability || Up == ability || Down == ability ||
                 Left == ability || Right == ability);
        }

        public AbilitySlotBinding WithDirectionalAbility(
            AbilitySlotDirection direction,
            AbilityId ability)
        {
            if (direction == AbilitySlotDirection.None)
            {
                throw new ArgumentException(
                    "A directional ability cannot replace the primary ability.",
                    nameof(direction));
            }

            if (ability == AbilityId.None)
            {
                throw new ArgumentException(
                    "A directional binding must reference an ability.",
                    nameof(ability));
            }

            switch (direction)
            {
                case AbilitySlotDirection.Up:
                    return new AbilitySlotBinding(Primary, ability, Down, Left, Right);
                case AbilitySlotDirection.Down:
                    return new AbilitySlotBinding(Primary, Up, ability, Left, Right);
                case AbilitySlotDirection.Left:
                    return new AbilitySlotBinding(Primary, Up, Down, ability, Right);
                case AbilitySlotDirection.Right:
                    return new AbilitySlotBinding(Primary, Up, Down, Left, ability);
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction));
            }
        }
    }

    public readonly struct SpecializationDefinition
    {
        public SpecializationDefinition(
            SpecializationId id,
            ClassId ownerClass,
            string displayName,
            AuraId passiveAura0 = AuraId.None,
            AuraId passiveAura1 = AuraId.None)
        {
            Id = id;
            OwnerClass = ownerClass;
            DisplayName = displayName ?? string.Empty;
            PassiveAura0 = passiveAura0;
            PassiveAura1 = passiveAura1;
        }

        public SpecializationId Id { get; }

        public ClassId OwnerClass { get; }

        public string DisplayName { get; }

        public AuraId PassiveAura0 { get; }

        public AuraId PassiveAura1 { get; }
    }

    public readonly struct AbilityKit
    {
        public const int GestureSlotCount = 2;
        public const int CommonSlotCount = 4;
        public const int UtilitySlotCount = 4;
        public const int Capacity = GestureSlotCount + CommonSlotCount + UtilitySlotCount;
        public const int MaximumActionCount = (GestureSlotCount * 5) +
            CommonSlotCount + UtilitySlotCount;

        private readonly AbilitySlotBinding _slot0;
        private readonly AbilitySlotBinding _slot1;
        private readonly AbilitySlotBinding _slot2;
        private readonly AbilitySlotBinding _slot3;
        private readonly AbilitySlotBinding _slot4;
        private readonly AbilitySlotBinding _slot5;
        private readonly AbilitySlotBinding _slot6;
        private readonly AbilitySlotBinding _slot7;
        private readonly AbilitySlotBinding _slot8;
        private readonly AbilitySlotBinding _slot9;

        public AbilityKit(
            ClassId ownerClass,
            SpecializationId specialization,
            AbilityId slot0,
            AbilityId slot1,
            AbilityId slot2,
            AbilityId slot3,
            AbilityId slot4,
            AbilityId slot5 = AbilityId.None,
            AbilityId slot6 = AbilityId.None,
            AbilityId slot7 = AbilityId.None,
            AbilityId slot8 = AbilityId.None,
            AbilityId slot9 = AbilityId.None)
        {
            OwnerClass = ownerClass;
            Specialization = specialization;
            _slot0 = new AbilitySlotBinding(slot0);
            _slot1 = new AbilitySlotBinding(slot1);
            _slot2 = new AbilitySlotBinding(slot2);
            _slot3 = new AbilitySlotBinding(slot3);
            _slot4 = new AbilitySlotBinding(slot4);
            _slot5 = new AbilitySlotBinding(slot5);
            _slot6 = new AbilitySlotBinding(slot6);
            _slot7 = new AbilitySlotBinding(slot7);
            _slot8 = new AbilitySlotBinding(slot8);
            _slot9 = new AbilitySlotBinding(slot9);
            Count = 0;
            AbilityCount = 0;
            Count = CountPrimaryAbilities();
            AbilityCount = CountAllAbilities();
        }

        private AbilityKit(
            ClassId ownerClass,
            SpecializationId specialization,
            AbilitySlotBinding slot0,
            AbilitySlotBinding slot1,
            AbilitySlotBinding slot2,
            AbilitySlotBinding slot3,
            AbilitySlotBinding slot4,
            AbilitySlotBinding slot5,
            AbilitySlotBinding slot6,
            AbilitySlotBinding slot7,
            AbilitySlotBinding slot8,
            AbilitySlotBinding slot9)
        {
            OwnerClass = ownerClass;
            Specialization = specialization;
            _slot0 = slot0;
            _slot1 = slot1;
            _slot2 = slot2;
            _slot3 = slot3;
            _slot4 = slot4;
            _slot5 = slot5;
            _slot6 = slot6;
            _slot7 = slot7;
            _slot8 = slot8;
            _slot9 = slot9;
            Count = 0;
            AbilityCount = 0;
            Count = CountPrimaryAbilities();
            AbilityCount = CountAllAbilities();
        }

        public ClassId OwnerClass { get; }

        public SpecializationId Specialization { get; }

        public int Count { get; }

        public int AbilityCount { get; }

        public AbilityId GetAbility(int slot)
        {
            return GetSlotBinding(slot).Primary;
        }

        public AbilityId GetAbility(int slot, AbilitySlotDirection direction)
        {
            return GetSlotBinding(slot).GetAbility(direction);
        }

        public AbilityId GetDirectionalAbility(
            int slot,
            AbilitySlotDirection direction)
        {
            return GetAbility(slot, direction);
        }

        public AbilitySlotBinding GetSlotBinding(int slot)
        {
            switch (slot)
            {
                case 0:
                    return _slot0;
                case 1:
                    return _slot1;
                case 2:
                    return _slot2;
                case 3:
                    return _slot3;
                case 4:
                    return _slot4;
                case 5:
                    return _slot5;
                case 6:
                    return _slot6;
                case 7:
                    return _slot7;
                case 8:
                    return _slot8;
                case 9:
                    return _slot9;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        public bool Contains(AbilityId ability)
        {
            if (ability == AbilityId.None)
            {
                return false;
            }

            for (int slot = 0; slot < Capacity; slot++)
            {
                if (GetSlotBinding(slot).Contains(ability))
                {
                    return true;
                }
            }

            return false;
        }

        public AbilityKit WithDirectionalAbility(
            int slot,
            AbilitySlotDirection direction,
            AbilityId ability)
        {
            AbilitySlotBinding updated = GetSlotBinding(slot)
                .WithDirectionalAbility(direction, ability);
            return new AbilityKit(
                OwnerClass,
                Specialization,
                slot == 0 ? updated : _slot0,
                slot == 1 ? updated : _slot1,
                slot == 2 ? updated : _slot2,
                slot == 3 ? updated : _slot3,
                slot == 4 ? updated : _slot4,
                slot == 5 ? updated : _slot5,
                slot == 6 ? updated : _slot6,
                slot == 7 ? updated : _slot7,
                slot == 8 ? updated : _slot8,
                slot == 9 ? updated : _slot9);
        }

        public AbilityKit WithPrimaryAbility(int slot, AbilityId ability)
        {
            if (slot < 0 || slot >= Capacity)
            {
                throw new ArgumentOutOfRangeException(nameof(slot));
            }

            AbilitySlotBinding current = GetSlotBinding(slot);
            AbilitySlotBinding updated = new AbilitySlotBinding(
                ability,
                current.Up,
                current.Down,
                current.Left,
                current.Right);
            return new AbilityKit(
                OwnerClass,
                Specialization,
                slot == 0 ? updated : _slot0,
                slot == 1 ? updated : _slot1,
                slot == 2 ? updated : _slot2,
                slot == 3 ? updated : _slot3,
                slot == 4 ? updated : _slot4,
                slot == 5 ? updated : _slot5,
                slot == 6 ? updated : _slot6,
                slot == 7 ? updated : _slot7,
                slot == 8 ? updated : _slot8,
                slot == 9 ? updated : _slot9);
        }

        private int CountPrimaryAbilities()
        {
            int count = 0;
            for (int slot = 0; slot < Capacity; slot++)
            {
                count += GetSlotBinding(slot).Primary == AbilityId.None ? 0 : 1;
            }

            return count;
        }

        private int CountAllAbilities()
        {
            int count = 0;
            for (int slot = 0; slot < Capacity; slot++)
            {
                count += GetSlotBinding(slot).Count;
            }

            return count;
        }
    }

    public static class SpecializationCatalog
    {
        private static readonly SpecializationDefinition DisciplineDefinition =
            new SpecializationDefinition(
                SpecializationId.Discipline,
                ClassId.Priest,
                "Discipline");

        private static readonly SpecializationDefinition FireDefinition =
            new SpecializationDefinition(SpecializationId.Fire, ClassId.Mage, "Fire");

        private static readonly SpecializationDefinition FrostDefinition =
            new SpecializationDefinition(SpecializationId.Frost, ClassId.Mage, "Frost");

        private static readonly SpecializationDefinition ShadowDanceDefinition =
            new SpecializationDefinition(
                SpecializationId.ShadowDance,
                ClassId.Rogue,
                "Shadow Dance");

        private static readonly SpecializationDefinition MutilateDefinition =
            new SpecializationDefinition(
                SpecializationId.Mutilate,
                ClassId.Rogue,
                "Mutilate");

        private static readonly SpecializationDefinition ShadowDefinition =
            new SpecializationDefinition(
                SpecializationId.Shadow,
                ClassId.Priest,
                "Shadow",
                AuraId.Shadowform,
                AuraId.VampiricEmbrace);

        private static readonly SpecializationDefinition MarksmanshipDefinition =
            new SpecializationDefinition(
                SpecializationId.Marksmanship,
                ClassId.Hunter,
                "Marksmanship");

        private static readonly SpecializationDefinition BossDefinition =
            new SpecializationDefinition(
                SpecializationId.Boss,
                ClassId.Boss,
                "Rift Warden");

        private static readonly AbilityKit DisciplineKit = new AbilityKit(
            ClassId.Priest,
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
            AbilityId.ArenaTrinket)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Up,
                AbilityId.PrayerOfMending)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Down,
                AbilityId.FlashHeal)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Left,
                AbilityId.PowerWordShield)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Right,
                AbilityId.PainSuppression)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Up,
                AbilityId.ManaBurn)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Down,
                AbilityId.ShadowWordPain)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Left,
                AbilityId.MassDispel)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Right,
                AbilityId.FearWard);

        private static readonly AbilityKit FireKit = new AbilityKit(
            ClassId.Mage,
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
            AbilityId.ArenaTrinket)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Up,
                AbilityId.Polymorph)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Down,
                AbilityId.FrostNova)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Left,
                AbilityId.ConeOfCold)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Right,
                AbilityId.BlastWave)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Up,
                AbilityId.Spellsteal)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Down,
                AbilityId.MoltenArmor)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Left,
                AbilityId.IceBlock)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Right,
                AbilityId.Blink);

        private static readonly AbilityKit FrostKit = new AbilityKit(
            ClassId.Mage,
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
            AbilityId.ArenaTrinket)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Up,
                AbilityId.ConeOfCold)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Down,
                AbilityId.WaterElementalFreeze)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Left,
                AbilityId.FrostNova)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Right,
                AbilityId.DeepFreeze)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Up,
                AbilityId.ManaShield)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Down,
                AbilityId.IceBlock)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Left,
                AbilityId.Blink)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Right,
                AbilityId.ColdSnap);

        private static readonly AbilityKit ShadowDanceKit = new AbilityKit(
            ClassId.Rogue,
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
            AbilityId.ArenaTrinket)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Up,
                AbilityId.Garrote)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Down,
                AbilityId.Gouge)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Left,
                AbilityId.Blind)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Right,
                AbilityId.KidneyShot)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Up,
                AbilityId.Sprint)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Down,
                AbilityId.Vanish)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Left,
                AbilityId.Evasion)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Right,
                AbilityId.Stealth);

        private static readonly AbilityKit MutilateKit = new AbilityKit(
            ClassId.Rogue,
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
            AbilityId.ArenaTrinket)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Up,
                AbilityId.CheapShot)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Down,
                AbilityId.Gouge)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Left,
                AbilityId.Blind)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Right,
                AbilityId.Garrote)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Up,
                AbilityId.CloakOfShadows)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Down,
                AbilityId.Evasion)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Left,
                AbilityId.Sprint)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Right,
                AbilityId.Stealth);

        private static readonly AbilityKit ShadowKit = new AbilityKit(
            ClassId.Priest,
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
            AbilityId.ArenaTrinket)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Up,
                AbilityId.ShadowWordDeath)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Down,
                AbilityId.ManaBurn)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Left,
                AbilityId.ShadowWordPain)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Right,
                AbilityId.MindBlast)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Up,
                AbilityId.Silence)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Down,
                AbilityId.PsychicHorror)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Left,
                AbilityId.Fade)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Right,
                AbilityId.Dispersion);

        private static readonly AbilityKit MarksmanshipKit = new AbilityKit(
            ClassId.Hunter,
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
            AbilityId.ArenaTrinket)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Up,
                AbilityId.KillShot)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Down,
                AbilityId.ScorpidSting)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Left,
                AbilityId.TranquilizingShot)
            .WithDirectionalAbility(
                0,
                AbilitySlotDirection.Right,
                AbilityId.ConcussiveShot)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Up,
                AbilityId.FreezingArrow)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Down,
                AbilityId.FrostTrap)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Left,
                AbilityId.SilencingShot)
            .WithDirectionalAbility(
                1,
                AbilitySlotDirection.Right,
                AbilityId.Disengage);

        private static readonly AbilityKit BossKit = new AbilityKit(
            ClassId.Boss,
            SpecializationId.Boss,
            AbilityId.BossCleave,
            AbilityId.BossMeteor,
            AbilityId.BossKnockback,
            AbilityId.None,
            AbilityId.None);

        public static SpecializationId GetDefault(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Priest:
                    return SpecializationId.Discipline;
                case ClassId.Rogue:
                    return SpecializationId.ShadowDance;
                case ClassId.Mage:
                    return SpecializationId.Frost;
                case ClassId.Hunter:
                    return SpecializationId.Marksmanship;
                case ClassId.Boss:
                    return SpecializationId.Boss;
                default:
                    return SpecializationId.None;
            }
        }

        public static bool IsCompatible(ClassId classId, SpecializationId specialization)
        {
            return TryGetKit(specialization, out AbilityKit kit) && kit.OwnerClass == classId;
        }

        public static bool IsValidForClass(
            ClassId classId,
            SpecializationId specialization) => IsCompatible(classId, specialization);

        public static SpecializationDefinition GetDefinition(
            SpecializationId specialization)
        {
            switch (specialization)
            {
                case SpecializationId.Discipline:
                    return DisciplineDefinition;
                case SpecializationId.Fire:
                    return FireDefinition;
                case SpecializationId.Frost:
                    return FrostDefinition;
                case SpecializationId.ShadowDance:
                    return ShadowDanceDefinition;
                case SpecializationId.Mutilate:
                    return MutilateDefinition;
                case SpecializationId.Shadow:
                    return ShadowDefinition;
                case SpecializationId.Marksmanship:
                    return MarksmanshipDefinition;
                case SpecializationId.Boss:
                    return BossDefinition;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(specialization),
                        specialization,
                        "Unknown specialization ID.");
            }
        }

        public static AbilityKit GetKit(SpecializationId specialization)
        {
            if (!TryGetKit(specialization, out AbilityKit kit))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(specialization),
                    specialization,
                    "Unknown specialization ID.");
            }

            return kit;
        }

        public static AbilityKit GetKit(ClassId classId, SpecializationId specialization)
        {
            AbilityKit kit = GetKit(specialization);
            if (kit.OwnerClass != classId)
            {
                throw new ArgumentException(
                    "Specialization does not belong to the supplied class.",
                    nameof(specialization));
            }

            return kit;
        }

        public static bool TryGetKit(SpecializationId specialization, out AbilityKit kit)
        {
            switch (specialization)
            {
                case SpecializationId.Discipline:
                    kit = DisciplineKit;
                    return true;
                case SpecializationId.Fire:
                    kit = FireKit;
                    return true;
                case SpecializationId.Frost:
                    kit = FrostKit;
                    return true;
                case SpecializationId.ShadowDance:
                    kit = ShadowDanceKit;
                    return true;
                case SpecializationId.Mutilate:
                    kit = MutilateKit;
                    return true;
                case SpecializationId.Shadow:
                    kit = ShadowKit;
                    return true;
                case SpecializationId.Marksmanship:
                    kit = MarksmanshipKit;
                    return true;
                case SpecializationId.Boss:
                    kit = BossKit;
                    return true;
                default:
                    kit = default;
                    return false;
            }
        }

        public static ResourceProfile GetResourceProfile(SpecializationId specialization)
        {
            switch (specialization)
            {
                case SpecializationId.Discipline:
                case SpecializationId.Fire:
                case SpecializationId.Frost:
                case SpecializationId.Shadow:
                case SpecializationId.Marksmanship:
                    return new ResourceProfile(ResourceType.Mana, 100f, 100f, 2f);
                case SpecializationId.ShadowDance:
                case SpecializationId.Mutilate:
                    return new ResourceProfile(ResourceType.Energy, 100f, 100f, 10f);
                default:
                    return ResourceProfile.None;
            }
        }
    }
}
