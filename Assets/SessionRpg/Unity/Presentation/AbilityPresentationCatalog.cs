using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    public enum EffectMotion
    {
        Projectile,
        Burst,
        Tether,
        Dash,
        Nova,
        Shield,
        ShadowBrand,
        MindChannel,
        HunterArrow,
        Trap,
        FacetedWard,
        Horror,
        TargetMark,
        Command,
        Volley,
        BladeStorm,
    }

    public readonly struct AbilityPresentation
    {
        public AbilityPresentation(
            AbilityId ability,
            Color color,
            string style,
            EffectMotion motion,
            float size,
            float duration)
        {
            Ability = ability;
            Color = color;
            Style = style;
            Motion = motion;
            Size = size;
            Duration = duration;
        }

        public AbilityId Ability { get; }

        public Color Color { get; }

        public string Style { get; }

        public EffectMotion Motion { get; }

        public float Size { get; }

        public float Duration { get; }
    }

    /// <summary>
    /// Stable visual language for all simulation ability IDs. This catalog has no combat authority.
    /// </summary>
    public static class AbilityPresentationCatalog
    {
        private static readonly AbilityPresentation[] Presentations =
        {
            default,
            new AbilityPresentation(AbilityId.HealerBolt, new Color(1f, 0.78f, 0.24f), "radiant bolt", EffectMotion.Projectile, 0.34f, 0.28f),
            new AbilityPresentation(AbilityId.Mend, new Color(0.22f, 1f, 0.58f), "restoring tether", EffectMotion.Tether, 0.18f, 0.36f),
            new AbilityPresentation(AbilityId.Aegis, new Color(0.18f, 0.8f, 1f), "ward shield", EffectMotion.Shield, 1.28f, 0.7f),
            new AbilityPresentation(AbilityId.Purify, new Color(0.66f, 1f, 0.76f), "cleanse burst", EffectMotion.Burst, 1.1f, 0.34f),
            new AbilityPresentation(AbilityId.Rescue, new Color(0.3f, 1f, 0.82f), "rescue tether", EffectMotion.Tether, 0.24f, 0.42f),

            new AbilityPresentation(AbilityId.QuickStrike, new Color(1f, 0.66f, 0.18f), "quick slash", EffectMotion.Burst, 0.86f, 0.18f),
            new AbilityPresentation(AbilityId.Ambush, new Color(0.72f, 0.28f, 1f), "shadow dive", EffectMotion.Dash, 0.18f, 0.26f),
            new AbilityPresentation(AbilityId.Kick, new Color(1f, 0.38f, 0.12f), "disrupting kick", EffectMotion.Burst, 0.92f, 0.18f),
            new AbilityPresentation(AbilityId.Shadowstep, new Color(0.56f, 0.2f, 1f), "shadow step", EffectMotion.Dash, 0.22f, 0.2f),
            new AbilityPresentation(AbilityId.Anchor, new Color(1f, 0.72f, 0.15f), "anchored guard", EffectMotion.Shield, 1.08f, 0.55f),

            new AbilityPresentation(AbilityId.ArcaneBolt, new Color(0.32f, 0.68f, 1f), "arcane bolt", EffectMotion.Projectile, 0.38f, 0.25f),
            new AbilityPresentation(AbilityId.Gust, new Color(0.42f, 0.92f, 1f), "force gust", EffectMotion.Burst, 1.24f, 0.26f),
            new AbilityPresentation(AbilityId.Counterspell, new Color(1f, 0.24f, 0.68f), "counter burst", EffectMotion.Burst, 0.98f, 0.22f),
            new AbilityPresentation(AbilityId.Blink, new Color(0.7f, 0.46f, 1f), "arcane blink", EffectMotion.Dash, 0.22f, 0.2f),
            new AbilityPresentation(AbilityId.FrostNova, new Color(0.46f, 0.9f, 1f), "frost nova", EffectMotion.Nova, 2.1f, 0.42f),

            new AbilityPresentation(AbilityId.BossCleave, new Color(1f, 0.3f, 0.08f), "molten cleave", EffectMotion.Burst, 2.25f, 0.36f),
            new AbilityPresentation(AbilityId.BossMeteor, new Color(1f, 0.54f, 0.08f), "falling star", EffectMotion.Projectile, 0.78f, 0.52f),
            new AbilityPresentation(AbilityId.BossKnockback, new Color(1f, 0.18f, 0.06f), "rift roar", EffectMotion.Nova, 3.5f, 0.48f),

            new AbilityPresentation(AbilityId.Penance, new Color(1f, 0.84f, 0.32f), "penitent volley", EffectMotion.Projectile, 0.42f, 0.46f),
            new AbilityPresentation(AbilityId.FlashHeal, new Color(0.95f, 1f, 0.68f), "flash of grace", EffectMotion.Tether, 0.16f, 0.28f),
            new AbilityPresentation(AbilityId.PowerWordShield, new Color(0.42f, 0.78f, 1f), "soul ward", EffectMotion.Shield, 1.24f, 0.75f),
            new AbilityPresentation(AbilityId.PainSuppression, new Color(1f, 0.58f, 0.2f), "guardian vow", EffectMotion.Shield, 1.38f, 0.86f),
            new AbilityPresentation(AbilityId.PsychicScream, new Color(0.76f, 0.24f, 1f), "psychic rupture", EffectMotion.Nova, 2.4f, 0.44f),
            new AbilityPresentation(AbilityId.ShadowWordDeath, new Color(0.5f, 0.12f, 0.72f), "death word", EffectMotion.Projectile, 0.58f, 0.3f),
            new AbilityPresentation(AbilityId.Shadowfiend, new Color(0.34f, 0.08f, 0.52f), "shadow summons", EffectMotion.Burst, 0.95f, 0.5f),
            new AbilityPresentation(AbilityId.DispelMagic, new Color(0.72f, 1f, 0.96f), "unravel magic", EffectMotion.Burst, 1.05f, 0.32f),

            new AbilityPresentation(AbilityId.Hemorrhage, new Color(0.82f, 0.12f, 0.2f), "bleeding cut", EffectMotion.Burst, 0.82f, 0.19f),
            new AbilityPresentation(AbilityId.Eviscerate, new Color(1f, 0.32f, 0.24f), "finishing strike", EffectMotion.Burst, 1.12f, 0.23f),
            new AbilityPresentation(AbilityId.KidneyShot, new Color(1f, 0.7f, 0.12f), "stunning blow", EffectMotion.Burst, 0.96f, 0.27f),
            new AbilityPresentation(AbilityId.ShadowDance, new Color(0.62f, 0.16f, 0.94f), "shadow dance", EffectMotion.Nova, 1.45f, 0.62f),
            new AbilityPresentation(AbilityId.CloakOfShadows, new Color(0.34f, 0.5f, 0.68f), "shadow cloak", EffectMotion.Shield, 1.2f, 0.72f),
            new AbilityPresentation(AbilityId.Mutilate, new Color(0.58f, 0.9f, 0.14f), "venomous crosscut", EffectMotion.Burst, 1.06f, 0.24f),
            new AbilityPresentation(AbilityId.Envenom, new Color(0.22f, 1f, 0.34f), "venom release", EffectMotion.Burst, 1.16f, 0.3f),
            new AbilityPresentation(AbilityId.Shiv, new Color(0.22f, 0.78f, 0.48f), "poisoned shiv", EffectMotion.Burst, 0.72f, 0.16f),
            new AbilityPresentation(AbilityId.Blind, new Color(0.96f, 0.92f, 0.72f), "blinding dust", EffectMotion.Burst, 1.2f, 0.24f),
            new AbilityPresentation(AbilityId.Vanish, new Color(0.28f, 0.18f, 0.38f), "vanishing veil", EffectMotion.Nova, 1.05f, 0.38f),

            new AbilityPresentation(AbilityId.Fireball, new Color(1f, 0.24f, 0.04f), "roaring fireball", EffectMotion.Projectile, 0.72f, 0.42f),
            new AbilityPresentation(AbilityId.FireBlast, new Color(1f, 0.66f, 0.08f), "fire blast", EffectMotion.Burst, 1f, 0.18f),
            new AbilityPresentation(AbilityId.LivingBomb, new Color(1f, 0.36f, 0.08f), "living ember", EffectMotion.Shield, 0.86f, 0.6f),
            new AbilityPresentation(AbilityId.DragonBreath, new Color(1f, 0.18f, 0.08f), "dragon breath", EffectMotion.Burst, 1.55f, 0.34f),
            new AbilityPresentation(AbilityId.BlastWave, new Color(1f, 0.48f, 0.06f), "blast wave", EffectMotion.Nova, 2.65f, 0.46f),
            new AbilityPresentation(AbilityId.Frostbolt, new Color(0.22f, 0.58f, 1f), "frost bolt", EffectMotion.Projectile, 0.62f, 0.4f),
            new AbilityPresentation(AbilityId.IceLance, new Color(0.68f, 0.96f, 1f), "ice lance", EffectMotion.Projectile, 0.22f, 0.22f),
            new AbilityPresentation(AbilityId.DeepFreeze, new Color(0.16f, 0.72f, 1f), "deep freeze", EffectMotion.Shield, 1.18f, 0.62f),
            new AbilityPresentation(AbilityId.IceBarrier, new Color(0.38f, 0.88f, 1f), "ice barrier", EffectMotion.Shield, 1.3f, 0.78f),
            new AbilityPresentation(AbilityId.Polymorph, new Color(1f, 0.52f, 0.9f), "polymorph", EffectMotion.Burst, 0.94f, 0.44f),
            new AbilityPresentation(AbilityId.ArenaTrinket, new Color(0.72f, 0.56f, 1f), "shattered control", EffectMotion.Nova, 1.42f, 0.38f),
            new AbilityPresentation(AbilityId.PowerInfusion, new Color(1f, 0.84f, 0.3f), "radiant acceleration", EffectMotion.Shield, 1.34f, 0.68f),
            new AbilityPresentation(AbilityId.Combustion, new Color(1f, 0.24f, 0.03f), "combustion crown", EffectMotion.Nova, 1.58f, 0.72f),
            new AbilityPresentation(AbilityId.IcyVeins, new Color(0.28f, 0.82f, 1f), "icy acceleration", EffectMotion.Nova, 1.48f, 0.7f),
            new AbilityPresentation(AbilityId.Evasion, new Color(0.55f, 0.3f, 1f), "evasive shadows", EffectMotion.Shield, 1.28f, 0.58f),
            new AbilityPresentation(AbilityId.ColdBlood, new Color(0.62f, 1f, 0.16f), "cold blood focus", EffectMotion.Burst, 1.3f, 0.58f),
            new AbilityPresentation(AbilityId.Renew, new Color(0.38f, 1f, 0.68f), "gold-mint renewal tether", EffectMotion.Tether, 0.2f, 0.44f),
            new AbilityPresentation(AbilityId.CheapShot, new Color(0.76f, 0.32f, 0.94f), "violet-amber low strike", EffectMotion.Burst, 0.78f, 0.16f),

            new AbilityPresentation(AbilityId.VampiricTouch, new Color(0.76f, 0.16f, 0.88f), "vampiric sigil tether", EffectMotion.ShadowBrand, 0.46f, 0.52f),
            new AbilityPresentation(AbilityId.ShadowWordPain, new Color(0.62f, 0.18f, 0.9f), "pain brand", EffectMotion.ShadowBrand, 0.86f, 0.52f),
            new AbilityPresentation(AbilityId.DevouringPlague, new Color(0.48f, 0.1f, 0.66f), "devouring orbit", EffectMotion.ShadowBrand, 1.06f, 0.68f),
            new AbilityPresentation(AbilityId.MindFlay, new Color(0.56f, 0.26f, 1f), "fractured mind tether", EffectMotion.MindChannel, 0.44f, 0.54f),
            new AbilityPresentation(AbilityId.MindBlast, new Color(0.84f, 0.46f, 1f), "mind spike burst", EffectMotion.Burst, 1.12f, 0.28f),
            new AbilityPresentation(AbilityId.Silence, new Color(0.48f, 0.38f, 0.76f), "sealed spell ring", EffectMotion.Burst, 1.16f, 0.26f),
            new AbilityPresentation(AbilityId.Dispersion, new Color(0.38f, 0.14f, 0.68f), "faceted dispersion shell", EffectMotion.FacetedWard, 1.42f, 0.82f),
            new AbilityPresentation(AbilityId.PsychicHorror, new Color(0.72f, 0.12f, 0.62f), "horror crown", EffectMotion.Horror, 1.2f, 0.42f),
            new AbilityPresentation(AbilityId.ManaBurn, new Color(0.42f, 0.24f, 0.92f), "mana drain flare", EffectMotion.Tether, 0.36f, 0.48f),
            new AbilityPresentation(AbilityId.MassDispel, new Color(0.68f, 0.84f, 1f), "mass unravel field", EffectMotion.Nova, 2.55f, 0.5f),
            new AbilityPresentation(AbilityId.Fade, new Color(0.46f, 0.38f, 0.58f), "fading afterimage", EffectMotion.Nova, 1.05f, 0.42f),

            new AbilityPresentation(AbilityId.ChimeraShot, new Color(0.18f, 0.88f, 0.7f), "split-serpent arrow", EffectMotion.HunterArrow, 0.72f, 0.35f),
            new AbilityPresentation(AbilityId.SerpentSting, new Color(0.3f, 0.9f, 0.3f), "serpent sting", EffectMotion.HunterArrow, 0.36f, 0.25f),
            new AbilityPresentation(AbilityId.ViperSting, new Color(0.12f, 0.82f, 0.68f), "viper mana sting", EffectMotion.HunterArrow, 0.36f, 0.25f),
            new AbilityPresentation(AbilityId.ScorpidSting, new Color(0.92f, 0.66f, 0.16f), "scorpid crippling sting", EffectMotion.HunterArrow, 0.38f, 0.25f),
            new AbilityPresentation(AbilityId.AimedShot, new Color(1f, 0.66f, 0.16f), "aimed heavy arrow", EffectMotion.HunterArrow, 0.66f, 0.38f),
            new AbilityPresentation(AbilityId.ArcaneShot, new Color(0.38f, 0.72f, 1f), "arcane arrow", EffectMotion.HunterArrow, 0.44f, 0.26f),
            new AbilityPresentation(AbilityId.KillShot, new Color(1f, 0.22f, 0.12f), "execution arrow", EffectMotion.HunterArrow, 0.74f, 0.32f),
            new AbilityPresentation(AbilityId.SteadyShot, new Color(0.92f, 0.78f, 0.42f), "steady arrow", EffectMotion.HunterArrow, 0.56f, 0.32f),
            new AbilityPresentation(AbilityId.HuntersMark, new Color(1f, 0.82f, 0.24f), "hunter sight mark", EffectMotion.TargetMark, 0.9f, 0.58f),
            new AbilityPresentation(AbilityId.ScatterShot, new Color(1f, 0.48f, 0.16f), "scatter fan", EffectMotion.Volley, 1f, 0.24f),
            new AbilityPresentation(AbilityId.FreezingArrow, new Color(0.42f, 0.88f, 1f), "freezing arrow", EffectMotion.HunterArrow, 0.56f, 0.38f),
            new AbilityPresentation(AbilityId.FrostTrap, new Color(0.32f, 0.94f, 1f), "frost trap seal", EffectMotion.Trap, 1.55f, 0.55f),
            new AbilityPresentation(AbilityId.SilencingShot, new Color(0.72f, 0.34f, 0.9f), "silencing arrow", EffectMotion.HunterArrow, 0.5f, 0.28f),
            new AbilityPresentation(AbilityId.FeignDeath, new Color(0.56f, 0.52f, 0.46f), "feigned collapse", EffectMotion.Burst, 0.9f, 0.34f),
            new AbilityPresentation(AbilityId.Disengage, new Color(0.32f, 0.72f, 0.84f), "disengage vault", EffectMotion.Dash, 0.28f, 0.28f),
            new AbilityPresentation(AbilityId.WingClip, new Color(0.9f, 0.52f, 0.22f), "wing clip slash", EffectMotion.Burst, 0.85f, 0.18f),
            new AbilityPresentation(AbilityId.MastersCall, new Color(0.44f, 0.96f, 0.76f), "master pet recall", EffectMotion.Tether, 0.25f, 0.4f),
            new AbilityPresentation(AbilityId.Deterrence, new Color(0.72f, 0.78f, 0.86f), "deterrence blades", EffectMotion.FacetedWard, 1.25f, 0.72f),
            new AbilityPresentation(AbilityId.RoarOfSacrifice, new Color(1f, 0.58f, 0.18f), "guardian pet roar", EffectMotion.Shield, 1.35f, 0.72f),
            new AbilityPresentation(AbilityId.Readiness, new Color(0.52f, 1f, 0.46f), "readiness reset", EffectMotion.Nova, 1.2f, 0.46f),
            new AbilityPresentation(AbilityId.RapidFire, new Color(1f, 0.38f, 0.14f), "rapid volley", EffectMotion.Volley, 1.15f, 0.62f),
            new AbilityPresentation(AbilityId.PetAttack, new Color(1f, 0.38f, 0.18f), "pet attack command", EffectMotion.Command, 0.22f, 0.3f),
            new AbilityPresentation(AbilityId.PetFollow, new Color(0.3f, 0.88f, 1f), "pet follow command", EffectMotion.Command, 0.18f, 0.28f),
            new AbilityPresentation(AbilityId.PetStay, new Color(1f, 0.78f, 0.24f), "pet stay command", EffectMotion.Command, 0.72f, 0.35f),
            new AbilityPresentation(AbilityId.SpiderWeb, new Color(0.78f, 0.9f, 1f), "spider web snare", EffectMotion.Burst, 1.15f, 0.42f),
            new AbilityPresentation(AbilityId.MendPet, new Color(0.4f, 1f, 0.5f), "mend pet tether", EffectMotion.Tether, 0.2f, 0.48f),

            new AbilityPresentation(AbilityId.TranquilizingShot, new Color(0.38f, 0.96f, 0.78f), "tranquilizing purge arrow", EffectMotion.HunterArrow, 0.46f, 0.28f),
            new AbilityPresentation(AbilityId.ConcussiveShot, new Color(0.72f, 0.78f, 0.88f), "concussive blunt arrow", EffectMotion.HunterArrow, 0.52f, 0.28f),
            new AbilityPresentation(AbilityId.Flare, new Color(1f, 0.58f, 0.16f), "revealing flare field", EffectMotion.TargetMark, 1.8f, 0.58f),
            new AbilityPresentation(AbilityId.AspectOfTheDragonhawk, new Color(1f, 0.62f, 0.16f), "dragonhawk aspect crest", EffectMotion.FacetedWard, 1.28f, 0.68f),
            new AbilityPresentation(AbilityId.AspectOfTheViper, new Color(0.18f, 0.86f, 0.62f), "viper aspect coil", EffectMotion.FacetedWard, 1.22f, 0.68f),

            new AbilityPresentation(AbilityId.PrayerOfMending, new Color(1f, 0.88f, 0.42f), "mending prayer relay", EffectMotion.Tether, 0.22f, 0.48f),
            new AbilityPresentation(AbilityId.InnerFocus, new Color(1f, 0.96f, 0.68f), "inner focus halo", EffectMotion.Shield, 1.18f, 0.64f),
            new AbilityPresentation(AbilityId.FearWard, new Color(0.92f, 0.76f, 1f), "fear ward seal", EffectMotion.Shield, 1.24f, 0.72f),

            new AbilityPresentation(AbilityId.Scorch, new Color(1f, 0.48f, 0.08f), "scorch lance", EffectMotion.Projectile, 0.48f, 0.3f),
            new AbilityPresentation(AbilityId.Pyroblast, new Color(1f, 0.16f, 0.02f), "pyroblast comet", EffectMotion.Projectile, 0.88f, 0.48f),
            new AbilityPresentation(AbilityId.ConeOfCold, new Color(0.32f, 0.86f, 1f), "cone of cold fan", EffectMotion.Nova, 2.1f, 0.42f),
            new AbilityPresentation(AbilityId.Spellsteal, new Color(0.72f, 0.38f, 1f), "spellsteal siphon", EffectMotion.Tether, 0.32f, 0.46f),
            new AbilityPresentation(AbilityId.IceBlock, new Color(0.58f, 0.94f, 1f), "ice block prism", EffectMotion.FacetedWard, 1.52f, 0.88f),
            new AbilityPresentation(AbilityId.ManaShield, new Color(0.34f, 0.58f, 1f), "mana shield sphere", EffectMotion.Shield, 1.26f, 0.72f),
            new AbilityPresentation(AbilityId.MoltenArmor, new Color(1f, 0.34f, 0.05f), "molten armor crown", EffectMotion.Shield, 1.3f, 0.74f),
            new AbilityPresentation(AbilityId.MageArmor, new Color(0.62f, 0.42f, 1f), "mage armor runes", EffectMotion.Shield, 1.4f, 0.74f),
            new AbilityPresentation(AbilityId.IceArmor, new Color(0.32f, 0.82f, 1f), "ice armor plates", EffectMotion.FacetedWard, 1.32f, 0.78f),
            new AbilityPresentation(AbilityId.WaterElementalFreeze, new Color(0.24f, 0.78f, 1f), "elemental freeze field", EffectMotion.Trap, 1.8f, 0.58f),
            new AbilityPresentation(AbilityId.ColdSnap, new Color(0.72f, 0.98f, 1f), "cold snap reset", EffectMotion.Nova, 1.46f, 0.48f),

            new AbilityPresentation(AbilityId.Sap, new Color(0.92f, 0.82f, 0.58f), "sap daze", EffectMotion.Burst, 0.92f, 0.24f),
            new AbilityPresentation(AbilityId.Garrote, new Color(0.74f, 0.08f, 0.16f), "garrote bleed cut", EffectMotion.Burst, 0.88f, 0.22f),
            new AbilityPresentation(AbilityId.Gouge, new Color(1f, 0.7f, 0.18f), "gouge disruption", EffectMotion.Burst, 0.82f, 0.2f),
            new AbilityPresentation(AbilityId.Sprint, new Color(0.46f, 0.72f, 1f), "sprint afterimage", EffectMotion.Nova, 1.08f, 0.34f),
            new AbilityPresentation(AbilityId.Dismantle, new Color(0.88f, 0.42f, 0.18f), "dismantling strike", EffectMotion.Burst, 0.94f, 0.24f),
            new AbilityPresentation(AbilityId.Preparation, new Color(0.64f, 0.42f, 1f), "preparation reset", EffectMotion.Nova, 1.18f, 0.46f),

            new AbilityPresentation(AbilityId.Stealth, new Color(0.24f, 0.14f, 0.34f), "stealth veil", EffectMotion.Nova, 1.04f, 0.4f),
            new AbilityPresentation(AbilityId.Shadowform, new Color(0.34f, 0.1f, 0.62f), "faceted shadowform shell", EffectMotion.FacetedWard, 1.36f, 0.8f),
            new AbilityPresentation(AbilityId.Smite, new Color(1f, 0.86f, 0.34f), "radiant smite bolt", EffectMotion.Projectile, 0.48f, 0.34f),
            new AbilityPresentation(AbilityId.BladeTempest, new Color(0.84f, 0.48f, 1f), "radial knife storm", EffectMotion.BladeStorm, 2.5f, 0.46f),
            new AbilityPresentation(AbilityId.DawnNova, new Color(1f, 0.82f, 0.26f), "dawn judgment nova", EffectMotion.Nova, 2.8f, 0.54f),
            new AbilityPresentation(AbilityId.UmbralCollapse, new Color(0.52f, 0.12f, 0.78f), "imploding umbral field", EffectMotion.Nova, 2.68f, 0.58f),
            new AbilityPresentation(AbilityId.FlameSurge, new Color(1f, 0.26f, 0.025f), "surging flame field", EffectMotion.Nova, 2.76f, 0.5f),
            new AbilityPresentation(AbilityId.Blizzard, new Color(0.28f, 0.82f, 1f), "falling blizzard field", EffectMotion.Nova, 3f, 0.68f),
            new AbilityPresentation(AbilityId.ArrowRain, new Color(0.94f, 0.72f, 0.22f), "descending arrow volley", EffectMotion.Volley, 2.85f, 0.62f),
        };

        public static bool TryGet(AbilityId ability, out AbilityPresentation presentation)
        {
            int index = (int)ability;
            if (index > 0 && index < Presentations.Length)
            {
                AbilityPresentation candidate = Presentations[index];
                if (candidate.Ability == ability)
                {
                    presentation = candidate;
                    return true;
                }
            }

            presentation = default;
            return false;
        }

        public static AbilityPresentation Get(AbilityId ability)
        {
            if (!TryGet(ability, out AbilityPresentation presentation))
            {
                throw new System.ArgumentOutOfRangeException(nameof(ability));
            }

            return presentation;
        }
    }
}
