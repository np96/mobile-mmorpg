using System;
using SessionRpg.Core;

namespace SessionRpg.Unity.Gameplay
{
    /// <summary>
    /// Chooses the most actionable display reason without changing combat
    /// validation. Actor-state blockers retain Core priority; unmet ability
    /// prerequisites replace only range, line-of-sight, and facing failures.
    /// </summary>
    public static class AbilityReadinessEvaluator
    {
        public static bool CanStartForDisplay(
            CombatSimulation simulation,
            CombatCommand command,
            out string reason)
        {
            if (simulation == null)
            {
                throw new ArgumentNullException(nameof(simulation));
            }

            if (!simulation.CanUseAbility(
                    command.Actor,
                    command.Ability,
                    out reason))
            {
                return false;
            }

            if (simulation.CanStartAbility(command, out reason))
            {
                return true;
            }

            if (!IsSpatialFailure(reason) ||
                !AbilityCatalog.TryGet(
                    command.Ability,
                    out AbilityDefinition definition) ||
                !simulation.TryGetSnapshot(
                    command.Actor,
                    out CombatantSnapshot actor) ||
                !simulation.TryGetSnapshot(
                    command.Target,
                    out CombatantSnapshot target) ||
                !target.IsAlive)
            {
                return false;
            }

            if (TryGetPrerequisiteFailure(
                    definition,
                    actor,
                    target,
                    out string prerequisiteReason))
            {
                reason = prerequisiteReason;
            }

            return false;
        }

        public static bool TryGetPrerequisiteFailure(
            AbilityDefinition definition,
            CombatantSnapshot actor,
            CombatantSnapshot target,
            out string reason)
        {
            AbilityRequirementFlags requirements = definition.Requirements;
            if ((requirements &
                 AbilityRequirementFlags.RequiresActiveEnemyCast) != 0 &&
                (target.Team == actor.Team ||
                 target.CastingAbility == AbilityId.None))
            {
                reason = "target_not_casting";
                return true;
            }

            if ((requirements & AbilityRequirementFlags.RequiresStealthAccess) != 0 &&
                !actor.HasAuraTag(AuraTag.StealthAccess))
            {
                reason = "requires_stealth";
                return true;
            }

            if ((requirements & AbilityRequirementFlags.RequiresBehindTarget) != 0 &&
                !IsBehind(actor.Position, target))
            {
                reason = "requires_behind";
                return true;
            }

            if ((requirements & AbilityRequirementFlags.RequiresComboPoints) != 0 &&
                (actor.ComboTarget != target.Id || actor.ComboPoints <= 0))
            {
                reason = "combo_points";
                return true;
            }

            if ((requirements &
                 AbilityRequirementFlags.RequiresPoisonedTarget) != 0 &&
                !target.HasAuraTag(AuraTag.Poison))
            {
                reason = "target_not_poisoned";
                return true;
            }

            if ((requirements &
                 AbilityRequirementFlags.RequiresFrozenTarget) != 0 &&
                !target.HasAuraTag(AuraTag.Frozen))
            {
                reason = "target_not_frozen";
                return true;
            }

            if ((requirements &
                 AbilityRequirementFlags.TargetMustNotHaveWeakenedSoul) != 0 &&
                target.HasAuraTag(AuraTag.PreventsShield))
            {
                reason = "weakened_soul";
                return true;
            }

            if ((requirements &
                 AbilityRequirementFlags.TargetBelowTwentyPercentHealth) != 0 &&
                target.MaxHealth > 0f &&
                target.Health > (target.MaxHealth * 0.2f) + 0.0001f)
            {
                reason = "target_health_above_twenty_percent";
                return true;
            }

            if ((requirements & AbilityRequirementFlags.RequiresManaTarget) != 0 &&
                (target.ResourceType != ResourceType.Mana ||
                 target.MaxResource <= 0f))
            {
                reason = "target_has_no_mana";
                return true;
            }

            reason = null;
            return false;
        }

        private static bool IsSpatialFailure(string reason)
        {
            return reason == "out_of_range" ||
                reason == "minimum_range" ||
                reason == "line_of_sight" ||
                reason == "not_facing";
        }

        private static bool IsBehind(
            Float3 actorPosition,
            CombatantSnapshot target)
        {
            Float3 targetToActor =
                (actorPosition - target.Position).PlanarNormalized;
            Float3 targetFacing = target.Facing.PlanarNormalized;
            float dot = (targetToActor.X * targetFacing.X) +
                (targetToActor.Z * targetFacing.Z);
            return dot < -0.25f;
        }
    }
}
