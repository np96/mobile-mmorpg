using SessionRpg.Core;

namespace SessionRpg.Unity.Presentation
{
    public enum SpellVisualProfile
    {
        Physical,
        Radiant,
        Fire,
        Frost,
        Shadow,
        Arcane,
        Venom,
    }

    public enum EffectVisualPhase
    {
        Primary,
        CastStart,
        Impact,
        Miss,
    }

    /// <summary>
    /// Stable silhouette families for arrows that must remain readable from the
    /// strategy camera. This is presentation metadata only; it does not describe
    /// damage, range, timing, or any other combat outcome.
    /// </summary>
    public enum HunterShotVisualProfile
    {
        None,
        Precision,
        Heavy,
        TwinSerpent,
        Venom,
        Frost,
        Arcane,
    }

    /// <summary>
    /// Geometry-first burst signatures. These are deliberately independent of
    /// combat rules: they only decide which pooled pieces are posed when an
    /// already-resolved action is presented.
    /// </summary>
    public enum SpellBurstSilhouette
    {
        PhysicalPulse,
        MartialContact,
        VenomSplash,
        FireEruption,
        FrostShatter,
        RadiantJudgment,
        ShadowCollapse,
        ArcaneFracture,
    }

    /// <summary>
    /// Maps simulation ability IDs onto a stable, non-gameplay visual language.
    /// Keeping this mapping explicit prevents a newly added spell from silently
    /// inheriting an unrelated projectile motif.
    /// </summary>
    public static class SpellVisualLanguage
    {
        public static SpellBurstSilhouette ResolveBurstSilhouette(
            AbilityId ability)
        {
            if (UsesMartialContact(ability))
            {
                return SpellBurstSilhouette.MartialContact;
            }

            switch (ResolveProfile(ability))
            {
                case SpellVisualProfile.Venom:
                    return SpellBurstSilhouette.VenomSplash;
                case SpellVisualProfile.Fire:
                    return SpellBurstSilhouette.FireEruption;
                case SpellVisualProfile.Frost:
                    return SpellBurstSilhouette.FrostShatter;
                case SpellVisualProfile.Radiant:
                    return SpellBurstSilhouette.RadiantJudgment;
                case SpellVisualProfile.Shadow:
                    return SpellBurstSilhouette.ShadowCollapse;
                case SpellVisualProfile.Arcane:
                    return SpellBurstSilhouette.ArcaneFracture;
                default:
                    return SpellBurstSilhouette.PhysicalPulse;
            }
        }

        public static bool UsesMartialContact(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.QuickStrike:
                case AbilityId.Kick:
                case AbilityId.Hemorrhage:
                case AbilityId.Eviscerate:
                case AbilityId.KidneyShot:
                case AbilityId.CheapShot:
                case AbilityId.Mutilate:
                case AbilityId.Envenom:
                case AbilityId.Shiv:
                case AbilityId.WingClip:
                case AbilityId.Garrote:
                case AbilityId.Gouge:
                case AbilityId.Dismantle:
                    return true;
                default:
                    return false;
            }
        }

        public static bool TryResolveHunterShotProfile(
            AbilityId ability,
            out HunterShotVisualProfile profile)
        {
            switch (ability)
            {
                case AbilityId.SteadyShot:
                    profile = HunterShotVisualProfile.Precision;
                    return true;
                case AbilityId.AimedShot:
                case AbilityId.KillShot:
                    profile = HunterShotVisualProfile.Heavy;
                    return true;
                case AbilityId.ChimeraShot:
                    profile = HunterShotVisualProfile.TwinSerpent;
                    return true;
                case AbilityId.SerpentSting:
                case AbilityId.ViperSting:
                case AbilityId.ScorpidSting:
                    profile = HunterShotVisualProfile.Venom;
                    return true;
                case AbilityId.FreezingArrow:
                    profile = HunterShotVisualProfile.Frost;
                    return true;
                case AbilityId.ArcaneShot:
                case AbilityId.SilencingShot:
                case AbilityId.TranquilizingShot:
                    profile = HunterShotVisualProfile.Arcane;
                    return true;
                case AbilityId.ConcussiveShot:
                    profile = HunterShotVisualProfile.Precision;
                    return true;
                default:
                    profile = HunterShotVisualProfile.None;
                    return false;
            }
        }

        public static SpellVisualProfile ResolveProfile(AbilityId ability)
        {
            TryResolveProfile(ability, out SpellVisualProfile profile);
            return profile;
        }

        public static bool TryResolveProfile(
            AbilityId ability,
            out SpellVisualProfile profile)
        {
            switch (ability)
            {
                case AbilityId.HealerBolt:
                case AbilityId.Mend:
                case AbilityId.Aegis:
                case AbilityId.Purify:
                case AbilityId.Rescue:
                case AbilityId.Penance:
                case AbilityId.FlashHeal:
                case AbilityId.PowerWordShield:
                case AbilityId.PainSuppression:
                case AbilityId.DispelMagic:
                case AbilityId.PowerInfusion:
                case AbilityId.Renew:
                case AbilityId.MastersCall:
                case AbilityId.RoarOfSacrifice:
                case AbilityId.MendPet:
                case AbilityId.PrayerOfMending:
                case AbilityId.InnerFocus:
                case AbilityId.FearWard:
                case AbilityId.Smite:
                case AbilityId.DawnNova:
                    profile = SpellVisualProfile.Radiant;
                    return true;

                case AbilityId.QuickStrike:
                case AbilityId.Kick:
                case AbilityId.Anchor:
                case AbilityId.Hemorrhage:
                case AbilityId.Eviscerate:
                case AbilityId.KidneyShot:
                case AbilityId.Blind:
                case AbilityId.CheapShot:
                case AbilityId.ConcussiveShot:
                case AbilityId.Sap:
                case AbilityId.Garrote:
                case AbilityId.Gouge:
                case AbilityId.Dismantle:
                case AbilityId.BladeTempest:
                case AbilityId.ArrowRain:
                    profile = SpellVisualProfile.Physical;
                    return true;

                case AbilityId.Fireball:
                case AbilityId.FireBlast:
                case AbilityId.LivingBomb:
                case AbilityId.DragonBreath:
                case AbilityId.BlastWave:
                case AbilityId.BossCleave:
                case AbilityId.BossMeteor:
                case AbilityId.BossKnockback:
                case AbilityId.Combustion:
                case AbilityId.Flare:
                case AbilityId.AspectOfTheDragonhawk:
                case AbilityId.Scorch:
                case AbilityId.Pyroblast:
                case AbilityId.MoltenArmor:
                case AbilityId.FlameSurge:
                    profile = SpellVisualProfile.Fire;
                    return true;

                case AbilityId.FrostNova:
                case AbilityId.Frostbolt:
                case AbilityId.IceLance:
                case AbilityId.DeepFreeze:
                case AbilityId.IceBarrier:
                case AbilityId.IcyVeins:
                case AbilityId.ConeOfCold:
                case AbilityId.IceBlock:
                case AbilityId.IceArmor:
                case AbilityId.WaterElementalFreeze:
                case AbilityId.ColdSnap:
                case AbilityId.Blizzard:
                    profile = SpellVisualProfile.Frost;
                    return true;

                case AbilityId.PsychicScream:
                case AbilityId.ShadowWordDeath:
                case AbilityId.Shadowfiend:
                case AbilityId.Ambush:
                case AbilityId.Shadowstep:
                case AbilityId.ShadowDance:
                case AbilityId.CloakOfShadows:
                case AbilityId.Vanish:
                case AbilityId.Evasion:
                case AbilityId.VampiricTouch:
                case AbilityId.ShadowWordPain:
                case AbilityId.DevouringPlague:
                case AbilityId.MindFlay:
                case AbilityId.MindBlast:
                case AbilityId.Silence:
                case AbilityId.Dispersion:
                case AbilityId.PsychicHorror:
                case AbilityId.ManaBurn:
                case AbilityId.Fade:
                case AbilityId.Sprint:
                case AbilityId.Preparation:
                case AbilityId.Stealth:
                case AbilityId.Shadowform:
                case AbilityId.UmbralCollapse:
                    profile = SpellVisualProfile.Shadow;
                    return true;

                case AbilityId.ArcaneBolt:
                case AbilityId.Gust:
                case AbilityId.Counterspell:
                case AbilityId.Blink:
                case AbilityId.Polymorph:
                case AbilityId.ArenaTrinket:
                case AbilityId.MassDispel:
                case AbilityId.ArcaneShot:
                case AbilityId.HuntersMark:
                case AbilityId.SilencingShot:
                case AbilityId.Readiness:
                case AbilityId.PetFollow:
                case AbilityId.TranquilizingShot:
                case AbilityId.Spellsteal:
                case AbilityId.ManaShield:
                case AbilityId.MageArmor:
                    profile = SpellVisualProfile.Arcane;
                    return true;

                case AbilityId.Mutilate:
                case AbilityId.Envenom:
                case AbilityId.Shiv:
                case AbilityId.ColdBlood:
                case AbilityId.ChimeraShot:
                case AbilityId.SerpentSting:
                case AbilityId.ViperSting:
                case AbilityId.ScorpidSting:
                case AbilityId.AspectOfTheViper:
                    profile = SpellVisualProfile.Venom;
                    return true;

                case AbilityId.FreezingArrow:
                case AbilityId.FrostTrap:
                    profile = SpellVisualProfile.Frost;
                    return true;

                case AbilityId.AimedShot:
                case AbilityId.KillShot:
                case AbilityId.SteadyShot:
                case AbilityId.ScatterShot:
                case AbilityId.FeignDeath:
                case AbilityId.Disengage:
                case AbilityId.WingClip:
                case AbilityId.Deterrence:
                case AbilityId.RapidFire:
                case AbilityId.PetAttack:
                case AbilityId.PetStay:
                case AbilityId.SpiderWeb:
                    profile = SpellVisualProfile.Physical;
                    return true;

                default:
                    profile = SpellVisualProfile.Physical;
                    return false;
            }
        }
    }
}
