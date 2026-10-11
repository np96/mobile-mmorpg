using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    /// <summary>
    /// A bounded, immutable summary of what a team can infer from the current
    /// combat snapshots. The value owns no collections and is safe to rebuild
    /// every decision frame.
    /// </summary>
    public readonly struct ArenaTeamIntent
    {
        private readonly PursuitCounts _pursuits;

        internal ArenaTeamIntent(
            TeamId team,
            ParticipantId focusTarget,
            ParticipantId pressuredAlly,
            ParticipantId activeControlledEnemy,
            ParticipantId protectedControlledEnemy,
            PursuitCounts pursuits)
        {
            Team = team;
            FocusTarget = focusTarget;
            PressuredAlly = pressuredAlly;
            ActiveControlledEnemy = activeControlledEnemy;
            ProtectedControlledEnemy = protectedControlledEnemy;
            _pursuits = pursuits;
        }

        public TeamId Team { get; }

        /// <summary>
        /// The stable offensive suggestion. Individual bots still account for
        /// their distance and how many teammates are already pursuing it.
        /// </summary>
        public ParticipantId FocusTarget { get; }

        public ParticipantId PressuredAlly { get; }

        /// <summary>
        /// A rooted, frozen, stunned, feared, or disoriented enemy. Frozen and
        /// stunned enemies are intentionally useful burst windows.
        /// </summary>
        public ParticipantId ActiveControlledEnemy { get; }

        /// <summary>
        /// A feared or disoriented enemy whose control is broken by damage.
        /// Coordinated bots must leave this target alone while another target
        /// is available.
        /// </summary>
        public ParticipantId ProtectedControlledEnemy { get; }

        public bool IsValid => Team.Value > 0;

        public bool IsFor(TeamId team) => IsValid && Team == team;

        public bool IsProtectedControl(ParticipantId participant) =>
            participant.IsValid && participant == ProtectedControlledEnemy;

        public int GetPursuitCount(ParticipantId target) =>
            _pursuits.GetCount(target);

        public float GetPursuitPenalty(ParticipantId target)
        {
            int pursuers = GetPursuitCount(target);
            return pursuers > 1
                ? Math.Min(4.5f, (pursuers - 1) * 1.5f)
                : 0f;
        }
    }

    /// <summary>
    /// Builds deterministic team intent from snapshots only. It retains just a
    /// stable focus identifier per team; no simulation state or event history is
    /// read. The fixed storage supports the planned five-versus-five ceiling
    /// without steady-state allocations.
    /// </summary>
    public sealed class ArenaTeamCoordinator
    {
        private const float RangeEpsilon = 0.001f;
        private const float PressureHealthThreshold = 0.82f;
        private const float ThreatRadius = 6f;
        private const float StableFocusBonus = 2.25f;
        private const float ProtectedControlPenalty = 1000f;

        private readonly TeamId[] _trackedTeams =
            new TeamId[CombatSimulation.MaxCombatants];
        private readonly ParticipantId[] _stableFocusTargets =
            new ParticipantId[CombatSimulation.MaxCombatants];

        public ArenaTeamIntent Build(
            TeamId team,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            ValidateArguments(team, snapshots, snapshotCount);

            PursuitCounts pursuits = default;
            BuildPursuitCounts(team, snapshots, snapshotCount, ref pursuits);

            ParticipantId pressuredAlly = FindPressuredAlly(
                team,
                snapshots,
                snapshotCount);
            ParticipantId activeControlledEnemy = FindActiveControlledEnemy(
                team,
                snapshots,
                snapshotCount);
            ParticipantId protectedControlledEnemy = FindProtectedControlledEnemy(
                team,
                snapshots,
                snapshotCount);

            int teamSlot = FindOrCreateTeamSlot(team);
            ParticipantId previousFocus = teamSlot >= 0
                ? _stableFocusTargets[teamSlot]
                : default;
            ParticipantId focusTarget = FindFocusTarget(
                team,
                previousFocus,
                pressuredAlly,
                activeControlledEnemy,
                protectedControlledEnemy,
                pursuits,
                snapshots,
                snapshotCount);

            if (teamSlot >= 0)
            {
                _stableFocusTargets[teamSlot] = focusTarget;
            }

            return new ArenaTeamIntent(
                team,
                focusTarget,
                pressuredAlly,
                activeControlledEnemy,
                protectedControlledEnemy,
                pursuits);
        }

        public void Reset()
        {
            Array.Clear(_trackedTeams, 0, _trackedTeams.Length);
            Array.Clear(_stableFocusTargets, 0, _stableFocusTargets.Length);
        }

        private static void ValidateArguments(
            TeamId team,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            if (team.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(team));
            }

            if (snapshots == null)
            {
                throw new ArgumentNullException(nameof(snapshots));
            }

            if (snapshotCount < 0 || snapshotCount > snapshots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(snapshotCount));
            }
        }

        private static void BuildPursuitCounts(
            TeamId team,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            ref PursuitCounts pursuits)
        {
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot ally = snapshots[index];
                if (!ally.IsAlive || ally.Team != team)
                {
                    continue;
                }

                ParticipantId pursuedTarget = ResolvePursuedTarget(
                    ally,
                    snapshots,
                    snapshotCount);
                pursuits.Add(pursuedTarget);
            }
        }

        private static ParticipantId ResolvePursuedTarget(
            CombatantSnapshot ally,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            if (ally.ComboPoints > 0 &&
                TryFindLivingEnemy(
                    ally.Team,
                    ally.ComboTarget,
                    snapshots,
                    snapshotCount,
                    out _))
            {
                return ally.ComboTarget;
            }

            ParticipantId nearest = default;
            float nearestDistance = float.MaxValue;
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot enemy = snapshots[index];
                if (!IsVisibleLivingEnemy(ally.Team, enemy))
                {
                    continue;
                }

                float distance = Float3.PlanarDistance(
                    ally.Position,
                    enemy.Position);
                if (distance < nearestDistance ||
                    (Math.Abs(distance - nearestDistance) < RangeEpsilon &&
                     enemy.Id.Value < nearest.Value))
                {
                    nearest = enemy.Id;
                    nearestDistance = distance;
                }
            }

            return nearest;
        }

        private static ParticipantId FindPressuredAlly(
            TeamId team,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            ParticipantId best = default;
            float bestScore = float.MinValue;
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot ally = snapshots[index];
                if (!ally.IsAlive || ally.Team != team || ally.MaxHealth <= 0f)
                {
                    continue;
                }

                float healthRatio = HealthRatio(ally);
                int nearbyThreats = CountNearbyThreats(
                    ally,
                    snapshots,
                    snapshotCount);
                bool controlled = IsMovementControl(ally) ||
                    IsBreakableControl(ally);
                bool isPressured = healthRatio < PressureHealthThreshold ||
                    nearbyThreats >= 2 ||
                    controlled ||
                    ally.IsInLava;
                if (!isPressured)
                {
                    continue;
                }

                float score = ((1f - healthRatio) * 100f) +
                    (nearbyThreats * 12f) +
                    (controlled ? 18f : 0f) +
                    (ally.IsInLava ? 30f : 0f);
                if (score > bestScore ||
                    (Math.Abs(score - bestScore) < RangeEpsilon &&
                     ally.Id.Value < best.Value))
                {
                    best = ally.Id;
                    bestScore = score;
                }
            }

            return best;
        }

        private static int CountNearbyThreats(
            CombatantSnapshot ally,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            int count = 0;
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot enemy = snapshots[index];
                if (IsVisibleLivingEnemy(ally.Team, enemy) &&
                    Float3.PlanarDistance(ally.Position, enemy.Position) <=
                        ThreatRadius)
                {
                    count++;
                }
            }

            return count;
        }

        private static ParticipantId FindActiveControlledEnemy(
            TeamId team,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            ParticipantId best = default;
            int bestPriority = 0;
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot enemy = snapshots[index];
                if (!IsVisibleLivingEnemy(team, enemy))
                {
                    continue;
                }

                int priority = GetControlPriority(enemy);
                if (priority > bestPriority ||
                    (priority == bestPriority && priority > 0 &&
                     enemy.Id.Value < best.Value))
                {
                    best = enemy.Id;
                    bestPriority = priority;
                }
            }

            return best;
        }

        private static ParticipantId FindProtectedControlledEnemy(
            TeamId team,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            ParticipantId best = default;
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot enemy = snapshots[index];
                if (!IsVisibleLivingEnemy(team, enemy) ||
                    !IsBreakableControl(enemy))
                {
                    continue;
                }

                if (!best.IsValid || enemy.Id.Value < best.Value)
                {
                    best = enemy.Id;
                }
            }

            return best;
        }

        private static ParticipantId FindFocusTarget(
            TeamId team,
            ParticipantId previousFocus,
            ParticipantId pressuredAlly,
            ParticipantId activeControlledEnemy,
            ParticipantId protectedControlledEnemy,
            PursuitCounts pursuits,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            CombatantSnapshot pressured = default;
            TryFindSnapshot(
                pressuredAlly,
                snapshots,
                snapshotCount,
                out pressured);

            ParticipantId best = default;
            float bestScore = float.MaxValue;
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot enemy = snapshots[index];
                if (!IsVisibleLivingEnemy(team, enemy))
                {
                    continue;
                }

                float score = HealthRatio(enemy) * 10f;
                score += AverageTeamDistance(
                    team,
                    enemy,
                    snapshots,
                    snapshotCount) * 0.18f;
                score += pursuits.GetCount(enemy.Id) * 0.65f;

                if (enemy.Id == previousFocus)
                {
                    score -= StableFocusBonus;
                }

                if (enemy.Specialization == SpecializationId.Discipline)
                {
                    score -= 0.6f;
                }
                else if (enemy.Specialization == SpecializationId.Shadow)
                {
                    score -= 0.2f;
                }

                if (enemy.Id == activeControlledEnemy &&
                    enemy.Id != protectedControlledEnemy)
                {
                    score -= 4f;
                }

                score -= ComboSetupBonus(
                    team,
                    enemy.Id,
                    snapshots,
                    snapshotCount);

                if (pressured.Id.IsValid)
                {
                    score += Float3.PlanarDistance(
                        pressured.Position,
                        enemy.Position) * 0.22f;
                }

                if (enemy.Id == protectedControlledEnemy)
                {
                    score += ProtectedControlPenalty;
                }

                if (score < bestScore ||
                    (Math.Abs(score - bestScore) < RangeEpsilon &&
                     enemy.Id.Value < best.Value))
                {
                    best = enemy.Id;
                    bestScore = score;
                }
            }

            return best;
        }

        private static float AverageTeamDistance(
            TeamId team,
            CombatantSnapshot enemy,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            float total = 0f;
            int livingAllies = 0;
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot ally = snapshots[index];
                if (!ally.IsAlive || ally.Team != team)
                {
                    continue;
                }

                total += Float3.PlanarDistance(ally.Position, enemy.Position);
                livingAllies++;
            }

            return livingAllies > 0 ? total / livingAllies : float.MaxValue;
        }

        private static float ComboSetupBonus(
            TeamId team,
            ParticipantId enemy,
            CombatantSnapshot[] snapshots,
            int snapshotCount)
        {
            float bestBonus = 0f;
            for (int index = 0; index < snapshotCount; index++)
            {
                CombatantSnapshot ally = snapshots[index];
                if (!ally.IsAlive ||
                    ally.Team != team ||
                    ally.ComboTarget != enemy ||
                    ally.ComboPoints <= 0)
                {
                    continue;
                }

                bestBonus = Math.Max(bestBonus, 3f + (ally.ComboPoints * 0.4f));
            }

            return bestBonus;
        }

        private int FindOrCreateTeamSlot(TeamId team)
        {
            int empty = -1;
            for (int index = 0; index < _trackedTeams.Length; index++)
            {
                if (_trackedTeams[index] == team)
                {
                    return index;
                }

                if (empty < 0 && _trackedTeams[index].Value <= 0)
                {
                    empty = index;
                }
            }

            if (empty >= 0)
            {
                _trackedTeams[empty] = team;
            }

            return empty;
        }

        private static bool TryFindLivingEnemy(
            TeamId team,
            ParticipantId participant,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            out CombatantSnapshot enemy)
        {
            if (TryFindSnapshot(participant, snapshots, snapshotCount, out enemy))
            {
                return IsVisibleLivingEnemy(team, enemy);
            }

            return false;
        }

        private static bool TryFindSnapshot(
            ParticipantId participant,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            out CombatantSnapshot snapshot)
        {
            if (participant.IsValid)
            {
                for (int index = 0; index < snapshotCount; index++)
                {
                    if (snapshots[index].Id == participant)
                    {
                        snapshot = snapshots[index];
                        return true;
                    }
                }
            }

            snapshot = default;
            return false;
        }

        private static bool IsVisibleLivingEnemy(
            TeamId team,
            CombatantSnapshot snapshot)
        {
            return snapshot.IsAlive &&
                snapshot.Team != team &&
                !snapshot.HasAuraTag(AuraTag.TargetingDropped) &&
                (!snapshot.HasAuraTag(AuraTag.Stealth) ||
                 snapshot.HasAuraTag(AuraTag.Marked));
        }

        private static bool IsBreakableControl(CombatantSnapshot snapshot)
        {
            bool controlled = snapshot.HasAuraTag(AuraTag.Fear) ||
                snapshot.HasAuraTag(AuraTag.Disorient) ||
                snapshot.HasStatus(StatusId.Feared) ||
                snapshot.HasStatus(StatusId.Disoriented);
            return controlled &&
                (snapshot.HasAuraTag(AuraTag.BreaksOnDamage) ||
                 snapshot.HasStatus(StatusId.Feared) ||
                 snapshot.HasStatus(StatusId.Disoriented));
        }

        private static bool IsMovementControl(CombatantSnapshot snapshot)
        {
            return snapshot.HasAuraTag(AuraTag.Stun) ||
                snapshot.HasAuraTag(AuraTag.Root) ||
                snapshot.HasAuraTag(AuraTag.Frozen) ||
                snapshot.HasStatus(StatusId.Stunned) ||
                snapshot.HasStatus(StatusId.Rooted);
        }

        private static int GetControlPriority(CombatantSnapshot snapshot)
        {
            if (snapshot.HasAuraTag(AuraTag.Frozen))
            {
                return 4;
            }

            if (snapshot.HasAuraTag(AuraTag.Stun) ||
                snapshot.HasStatus(StatusId.Stunned))
            {
                return 3;
            }

            if (snapshot.HasAuraTag(AuraTag.Root) ||
                snapshot.HasStatus(StatusId.Rooted))
            {
                return 2;
            }

            return IsBreakableControl(snapshot) ? 1 : 0;
        }

        private static float HealthRatio(CombatantSnapshot snapshot)
        {
            return snapshot.MaxHealth > 0f
                ? snapshot.Health / snapshot.MaxHealth
                : 1f;
        }
    }

    internal struct PursuitCounts
    {
        private ParticipantId _targetOne;
        private ParticipantId _targetTwo;
        private ParticipantId _targetThree;
        private ParticipantId _targetFour;
        private ParticipantId _targetFive;
        private byte _countOne;
        private byte _countTwo;
        private byte _countThree;
        private byte _countFour;
        private byte _countFive;

        public void Add(ParticipantId target)
        {
            if (!target.IsValid)
            {
                return;
            }

            if (TryIncrement(target))
            {
                return;
            }

            if (!_targetOne.IsValid)
            {
                _targetOne = target;
                _countOne = 1;
            }
            else if (!_targetTwo.IsValid)
            {
                _targetTwo = target;
                _countTwo = 1;
            }
            else if (!_targetThree.IsValid)
            {
                _targetThree = target;
                _countThree = 1;
            }
            else if (!_targetFour.IsValid)
            {
                _targetFour = target;
                _countFour = 1;
            }
            else if (!_targetFive.IsValid)
            {
                _targetFive = target;
                _countFive = 1;
            }
        }

        public int GetCount(ParticipantId target)
        {
            if (target == _targetOne)
            {
                return _countOne;
            }

            if (target == _targetTwo)
            {
                return _countTwo;
            }

            if (target == _targetThree)
            {
                return _countThree;
            }

            if (target == _targetFour)
            {
                return _countFour;
            }

            return target == _targetFive ? _countFive : 0;
        }

        private bool TryIncrement(ParticipantId target)
        {
            if (target == _targetOne)
            {
                _countOne++;
                return true;
            }

            if (target == _targetTwo)
            {
                _countTwo++;
                return true;
            }

            if (target == _targetThree)
            {
                _countThree++;
                return true;
            }

            if (target == _targetFour)
            {
                _countFour++;
                return true;
            }

            if (target == _targetFive)
            {
                _countFive++;
                return true;
            }

            return false;
        }
    }
}
