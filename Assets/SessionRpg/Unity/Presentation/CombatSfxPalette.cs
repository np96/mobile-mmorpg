using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    public enum CombatSfxCue
    {
        None,
        CastStart,
        CastReleaseMagic,
        CastReleasePhysical,
        ImpactMagic,
        ImpactPhysical,
        Hazard,
        Heal,
        Barrier,
        Interrupt,
        Control,
        Knockback,
        Death,
        ResultVictory,
        ResultDefeat,
        ResultDraw,
        UiSelect,
        UiConfirm,
        UiReject,
        TargetSelect,
        Miss,
        CastReleaseRadiant,
        ImpactRadiant,
        CastReleaseFire,
        ImpactFire,
        CastReleaseFrost,
        ImpactFrost,
        CastReleaseShadow,
        ImpactShadow,
        CastReleaseArcane,
        ImpactArcane,
        CastReleaseVenom,
        ImpactVenom,
        Count,
    }

    [CreateAssetMenu(
        fileName = "CombatSfxPalette",
        menuName = "Session RPG/Combat SFX Palette")]
    public sealed class CombatSfxPalette : ScriptableObject
    {
        [SerializeField] private AudioClip _castStart;
        [SerializeField] private AudioClip _castReleaseMagic;
        [SerializeField] private AudioClip _castReleasePhysical;
        [SerializeField] private AudioClip _impactMagic;
        [SerializeField] private AudioClip _impactPhysical;
        [SerializeField] private AudioClip _hazard;
        [SerializeField] private AudioClip _heal;
        [SerializeField] private AudioClip _barrier;
        [SerializeField] private AudioClip _interrupt;
        [SerializeField] private AudioClip _control;
        [SerializeField] private AudioClip _knockback;
        [SerializeField] private AudioClip _death;
        [SerializeField] private AudioClip _resultVictory;
        [SerializeField] private AudioClip _resultDefeat;
        [SerializeField] private AudioClip _resultDraw;
        [SerializeField] private AudioClip _uiSelect;
        [SerializeField] private AudioClip _uiConfirm;
        [SerializeField] private AudioClip _uiReject;
        [SerializeField] private AudioClip _targetSelect;
        [SerializeField] private AudioClip _miss;
        [SerializeField] private AudioClip _castReleaseRadiant;
        [SerializeField] private AudioClip _impactRadiant;
        [SerializeField] private AudioClip _castReleaseFire;
        [SerializeField] private AudioClip _impactFire;
        [SerializeField] private AudioClip _castReleaseFrost;
        [SerializeField] private AudioClip _impactFrost;
        [SerializeField] private AudioClip _castReleaseShadow;
        [SerializeField] private AudioClip _impactShadow;
        [SerializeField] private AudioClip _castReleaseArcane;
        [SerializeField] private AudioClip _impactArcane;
        [SerializeField] private AudioClip _castReleaseVenom;
        [SerializeField] private AudioClip _impactVenom;

        public const int RequiredClipCount = (int)CombatSfxCue.Count - 1;

        public AudioClip GetClip(CombatSfxCue cue)
        {
            switch (cue)
            {
                case CombatSfxCue.CastStart:
                    return _castStart;
                case CombatSfxCue.CastReleaseMagic:
                    return _castReleaseMagic;
                case CombatSfxCue.CastReleasePhysical:
                    return _castReleasePhysical;
                case CombatSfxCue.ImpactMagic:
                    return _impactMagic;
                case CombatSfxCue.ImpactPhysical:
                    return _impactPhysical;
                case CombatSfxCue.Hazard:
                    return _hazard;
                case CombatSfxCue.Heal:
                    return _heal;
                case CombatSfxCue.Barrier:
                    return _barrier;
                case CombatSfxCue.Interrupt:
                    return _interrupt;
                case CombatSfxCue.Control:
                    return _control;
                case CombatSfxCue.Knockback:
                    return _knockback;
                case CombatSfxCue.Death:
                    return _death;
                case CombatSfxCue.ResultVictory:
                    return _resultVictory;
                case CombatSfxCue.ResultDefeat:
                    return _resultDefeat;
                case CombatSfxCue.ResultDraw:
                    return _resultDraw;
                case CombatSfxCue.UiSelect:
                    return _uiSelect;
                case CombatSfxCue.UiConfirm:
                    return _uiConfirm;
                case CombatSfxCue.UiReject:
                    return _uiReject;
                case CombatSfxCue.TargetSelect:
                    return _targetSelect;
                case CombatSfxCue.Miss:
                    return _miss;
                case CombatSfxCue.CastReleaseRadiant:
                    return _castReleaseRadiant;
                case CombatSfxCue.ImpactRadiant:
                    return _impactRadiant;
                case CombatSfxCue.CastReleaseFire:
                    return _castReleaseFire;
                case CombatSfxCue.ImpactFire:
                    return _impactFire;
                case CombatSfxCue.CastReleaseFrost:
                    return _castReleaseFrost;
                case CombatSfxCue.ImpactFrost:
                    return _impactFrost;
                case CombatSfxCue.CastReleaseShadow:
                    return _castReleaseShadow;
                case CombatSfxCue.ImpactShadow:
                    return _impactShadow;
                case CombatSfxCue.CastReleaseArcane:
                    return _castReleaseArcane;
                case CombatSfxCue.ImpactArcane:
                    return _impactArcane;
                case CombatSfxCue.CastReleaseVenom:
                    return _castReleaseVenom;
                case CombatSfxCue.ImpactVenom:
                    return _impactVenom;
                default:
                    return null;
            }
        }

        public bool HasEveryRequiredClip()
        {
            for (int cueValue = 1; cueValue < (int)CombatSfxCue.Count; cueValue++)
            {
                if (GetClip((CombatSfxCue)cueValue) == null)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Presentation-only sonic grammar. Ability identity remains in Core while
    /// this mapping gives every major visual family a distinct release and
    /// contact silhouette without changing combat timing or outcomes.
    /// </summary>
    public static class CombatSoundLanguage
    {
        public static CombatSfxCue ResolveReleaseCue(AbilityId ability)
        {
            if (!SpellVisualLanguage.TryResolveProfile(
                    ability,
                    out SpellVisualProfile profile))
            {
                return IsPhysicalAbility(ability)
                    ? CombatSfxCue.CastReleasePhysical
                    : CombatSfxCue.CastReleaseMagic;
            }

            switch (profile)
            {
                case SpellVisualProfile.Radiant:
                    return CombatSfxCue.CastReleaseRadiant;
                case SpellVisualProfile.Fire:
                    return CombatSfxCue.CastReleaseFire;
                case SpellVisualProfile.Frost:
                    return CombatSfxCue.CastReleaseFrost;
                case SpellVisualProfile.Shadow:
                    return CombatSfxCue.CastReleaseShadow;
                case SpellVisualProfile.Arcane:
                    return CombatSfxCue.CastReleaseArcane;
                case SpellVisualProfile.Venom:
                    return CombatSfxCue.CastReleaseVenom;
                default:
                    return CombatSfxCue.CastReleasePhysical;
            }
        }

        public static CombatSfxCue ResolveImpactCue(AbilityId ability)
        {
            if (ability == AbilityId.None)
            {
                return CombatSfxCue.Hazard;
            }

            if (!SpellVisualLanguage.TryResolveProfile(
                    ability,
                    out SpellVisualProfile profile))
            {
                return IsPhysicalAbility(ability)
                    ? CombatSfxCue.ImpactPhysical
                    : CombatSfxCue.ImpactMagic;
            }

            switch (profile)
            {
                case SpellVisualProfile.Radiant:
                    return CombatSfxCue.ImpactRadiant;
                case SpellVisualProfile.Fire:
                    return CombatSfxCue.ImpactFire;
                case SpellVisualProfile.Frost:
                    return CombatSfxCue.ImpactFrost;
                case SpellVisualProfile.Shadow:
                    return CombatSfxCue.ImpactShadow;
                case SpellVisualProfile.Arcane:
                    return CombatSfxCue.ImpactArcane;
                case SpellVisualProfile.Venom:
                    return CombatSfxCue.ImpactVenom;
                default:
                    return CombatSfxCue.ImpactPhysical;
            }
        }

        private static bool IsPhysicalAbility(AbilityId ability)
        {
            return AbilityCatalog.TryGet(
                    ability,
                    out AbilityDefinition definition) &&
                ((definition.School & SpellSchoolMask.Physical) != 0 ||
                 definition.OwnerClass == ClassId.Rogue);
        }
    }
}
