using SessionRpg.Core;

namespace SessionRpg.Application
{
    public static class SpectatorTargetSelector
    {
        public static ParticipantId Select(
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            ParticipantId humanId,
            TeamId humanTeam)
        {
            if (snapshots == null || snapshotCount <= 0)
            {
                return default;
            }

            int count = snapshotCount < snapshots.Length
                ? snapshotCount
                : snapshots.Length;
            ParticipantId friendly = default;
            ParticipantId fallback = default;
            for (int index = 0; index < count; index++)
            {
                CombatantSnapshot candidate = snapshots[index];
                if (!candidate.IsAlive || candidate.Id == humanId)
                {
                    continue;
                }

                if (!fallback.IsValid || candidate.Id.Value < fallback.Value)
                {
                    fallback = candidate.Id;
                }

                if (candidate.Team == humanTeam &&
                    (!friendly.IsValid || candidate.Id.Value < friendly.Value))
                {
                    friendly = candidate.Id;
                }
            }

            return friendly.IsValid ? friendly : fallback;
        }
    }
}
