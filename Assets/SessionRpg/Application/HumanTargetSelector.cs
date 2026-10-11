using SessionRpg.Core;

namespace SessionRpg.Application
{
    public static class HumanTargetSelector
    {
        public static ParticipantId Select(
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            ParticipantId humanId,
            ParticipantId selectedTarget)
        {
            if (snapshots == null || snapshotCount <= 0 || !humanId.IsValid)
            {
                return default;
            }

            int count = snapshotCount < snapshots.Length
                ? snapshotCount
                : snapshots.Length;
            CombatantSnapshot human = default;
            bool foundHuman = false;
            for (int index = 0; index < count; index++)
            {
                if (snapshots[index].Id == humanId)
                {
                    human = snapshots[index];
                    foundHuman = true;
                    break;
                }
            }

            if (!foundHuman)
            {
                return default;
            }

            bool selectedTargetIsValid = false;
            ParticipantId nearestHostile = default;
            float nearestDistanceSquared = float.PositiveInfinity;
            for (int index = 0; index < count; index++)
            {
                CombatantSnapshot candidate = snapshots[index];
                if (candidate.Id == selectedTarget &&
                    IsSelectable(human, candidate))
                {
                    selectedTargetIsValid = true;
                }

                if (!IsSelectableHostile(human, candidate))
                {
                    continue;
                }

                float distanceSquared =
                    (candidate.Position - human.Position).PlanarLengthSquared;
                int distanceComparison =
                    distanceSquared.CompareTo(nearestDistanceSquared);
                if (!nearestHostile.IsValid ||
                    distanceComparison < 0 ||
                    (distanceComparison == 0 &&
                     candidate.Id.Value < nearestHostile.Value))
                {
                    nearestHostile = candidate.Id;
                    nearestDistanceSquared = distanceSquared;
                }
            }

            return selectedTargetIsValid ? selectedTarget : nearestHostile;
        }

        public static bool IsSelectable(
            CombatantSnapshot human,
            CombatantSnapshot candidate)
        {
            if (!candidate.Id.IsValid || !candidate.IsAlive)
            {
                return false;
            }

            return candidate.Team == human.Team ||
                IsSelectableHostile(human, candidate);
        }

        private static bool IsSelectableHostile(
            CombatantSnapshot human,
            CombatantSnapshot candidate)
        {
            if (!candidate.Id.IsValid ||
                !candidate.IsAlive ||
                candidate.Team == human.Team ||
                candidate.HasAuraTag(AuraTag.TargetingDropped))
            {
                return false;
            }

            return !candidate.HasAuraTag(AuraTag.Stealth) ||
                (human.Class == ClassId.Hunter &&
                 candidate.HasAuraTag(AuraTag.Marked));
        }
    }
}
