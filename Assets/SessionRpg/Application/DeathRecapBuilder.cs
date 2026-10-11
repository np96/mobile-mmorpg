using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    public enum DeathContributionKind
    {
        Damage,
        Control,
        Knockback,
        Hazard,
    }

    public readonly struct DeathRecapStep
    {
        public DeathRecapStep(
            int tick,
            ParticipantId source,
            AbilityId ability,
            DeathContributionKind kind)
        {
            Tick = tick;
            Source = source;
            Ability = ability;
            Kind = kind;
        }

        public int Tick { get; }

        public ParticipantId Source { get; }

        public AbilityId Ability { get; }

        public DeathContributionKind Kind { get; }

        public bool IsValid => Source.IsValid && Ability != AbilityId.None;
    }

    public readonly struct DeathRecap
    {
        private readonly DeathRecapStep _first;
        private readonly DeathRecapStep _second;
        private readonly DeathRecapStep _third;

        public DeathRecap(
            ParticipantId victim,
            ParticipantId creditedActor,
            AbilityId finishingAbility,
            bool endedInHazard,
            DeathRecapStep first,
            DeathRecapStep second,
            DeathRecapStep third,
            int stepCount,
            string chain,
            string hint)
        {
            Victim = victim;
            CreditedActor = creditedActor;
            FinishingAbility = finishingAbility;
            EndedInHazard = endedInHazard;
            _first = first;
            _second = second;
            _third = third;
            StepCount = Math.Max(0, Math.Min(3, stepCount));
            Chain = chain ?? string.Empty;
            Hint = hint ?? string.Empty;
        }

        public ParticipantId Victim { get; }

        public ParticipantId CreditedActor { get; }

        public AbilityId FinishingAbility { get; }

        public bool EndedInHazard { get; }

        public int StepCount { get; }

        public string Chain { get; }

        public string Hint { get; }

        public DeathRecapStep GetStep(int index)
        {
            switch (index)
            {
                case 0 when StepCount > 0:
                    return _first;
                case 1 when StepCount > 1:
                    return _second;
                case 2 when StepCount > 2:
                    return _third;
                default:
                    throw new ArgumentOutOfRangeException(nameof(index));
            }
        }
    }

    /// <summary>
    /// Maintains a small deterministic contribution ring for each possible combatant.
    /// Ordinary combat events do not allocate; display strings are created only when a
    /// combatant dies.
    /// </summary>
    public sealed class DeathRecapBuilder
    {
        public const int DefaultHistorySeconds = 8;

        private const int MaximumVictims = CombatSimulation.MaxCombatants;
        private const int ContributionsPerVictim = 24;
        private const int MaximumRecapSteps = 3;

        private readonly ParticipantId[] _victims = new ParticipantId[MaximumVictims];
        private readonly int[] _contributionCounts = new int[MaximumVictims];
        private readonly int[] _nextContributionIndices = new int[MaximumVictims];
        private readonly int[] _latestTicks = new int[MaximumVictims];
        private readonly DeathContribution[] _contributions =
            new DeathContribution[MaximumVictims * ContributionsPerVictim];
        private readonly DeathRecap[] _latestRecaps = new DeathRecap[MaximumVictims];
        private readonly bool[] _hasLatestRecap = new bool[MaximumVictims];

        private int _tickRate;
        private int _historyTicks;

        public DeathRecapBuilder(int tickRate = 30)
        {
            Reset(tickRate);
        }

        public int HistoryTicks => _historyTicks;

        public void Reset(int tickRate)
        {
            _tickRate = Math.Max(1, tickRate);
            _historyTicks = _tickRate * DefaultHistorySeconds;
            Array.Clear(_victims, 0, _victims.Length);
            Array.Clear(_contributionCounts, 0, _contributionCounts.Length);
            Array.Clear(_nextContributionIndices, 0, _nextContributionIndices.Length);
            Array.Clear(_latestTicks, 0, _latestTicks.Length);
            Array.Clear(_contributions, 0, _contributions.Length);
            Array.Clear(_latestRecaps, 0, _latestRecaps.Length);
            Array.Clear(_hasLatestRecap, 0, _hasLatestRecap.Length);
        }

        public void Observe(CombatEvent combatEvent)
        {
            switch (combatEvent.Kind)
            {
                case CombatEventKind.Damage:
                    RecordHostileContribution(combatEvent, DeathContributionKind.Damage);
                    break;
                case CombatEventKind.HazardDamage:
                    RecordHazard(combatEvent);
                    break;
                case CombatEventKind.StatusApplied:
                    if (IsControlStatus(combatEvent.Status))
                    {
                        RecordHostileContribution(combatEvent, DeathContributionKind.Control);
                    }

                    break;
                case CombatEventKind.Interrupted:
                    RecordHostileContribution(combatEvent, DeathContributionKind.Control);
                    break;
                case CombatEventKind.Knockback:
                    if (combatEvent.Amount > 0.001f || combatEvent.Vector.PlanarLengthSquared > 0.000001f)
                    {
                        RecordHostileContribution(combatEvent, DeathContributionKind.Knockback);
                    }

                    break;
                case CombatEventKind.Death:
                    BuildAndStoreRecap(combatEvent);
                    break;
            }
        }

        public bool TryGetRecap(ParticipantId victim, out DeathRecap recap)
        {
            int slot = FindVictimSlot(victim);
            if (slot >= 0 && _hasLatestRecap[slot])
            {
                recap = _latestRecaps[slot];
                return true;
            }

            recap = default;
            return false;
        }

        private void RecordHostileContribution(
            CombatEvent combatEvent,
            DeathContributionKind kind)
        {
            if (!combatEvent.Target.IsValid ||
                !combatEvent.Actor.IsValid ||
                combatEvent.Actor == combatEvent.Target ||
                combatEvent.Ability == AbilityId.None)
            {
                return;
            }

            RecordContribution(
                combatEvent.Target,
                new DeathContribution(
                    combatEvent.Tick,
                    combatEvent.Actor,
                    combatEvent.Ability,
                    kind));
        }

        private void RecordHazard(CombatEvent combatEvent)
        {
            if (!combatEvent.Target.IsValid)
            {
                return;
            }

            RecordContribution(
                combatEvent.Target,
                new DeathContribution(
                    combatEvent.Tick,
                    combatEvent.Actor,
                    AbilityId.None,
                    DeathContributionKind.Hazard));
        }

        private void RecordContribution(ParticipantId victim, DeathContribution contribution)
        {
            int slot = GetOrCreateVictimSlot(victim, contribution.Tick);
            int writeIndex = _nextContributionIndices[slot];
            _contributions[(slot * ContributionsPerVictim) + writeIndex] = contribution;
            _nextContributionIndices[slot] = (writeIndex + 1) % ContributionsPerVictim;
            _contributionCounts[slot] = Math.Min(
                ContributionsPerVictim,
                _contributionCounts[slot] + 1);
            _latestTicks[slot] = Math.Max(_latestTicks[slot], contribution.Tick);
        }

        private void BuildAndStoreRecap(CombatEvent deathEvent)
        {
            if (!deathEvent.Target.IsValid)
            {
                return;
            }

            int slot = GetOrCreateVictimSlot(deathEvent.Target, deathEvent.Tick);
            bool endedInHazard = string.Equals(
                    deathEvent.Reason,
                    "lava",
                    StringComparison.OrdinalIgnoreCase) ||
                HasRecentHazard(slot, deathEvent.Tick);

            DeathRecapStep first = default;
            DeathRecapStep second = default;
            DeathRecapStep third = default;
            int stepCount = 0;
            int count = _contributionCounts[slot];
            int start = count == ContributionsPerVictim
                ? _nextContributionIndices[slot]
                : 0;
            int baseIndex = slot * ContributionsPerVictim;
            for (int index = 0; index < count; index++)
            {
                DeathContribution contribution = _contributions[
                    baseIndex + ((start + index) % ContributionsPerVictim)];
                if (!IsRecent(contribution, deathEvent.Tick) ||
                    contribution.Kind == DeathContributionKind.Hazard ||
                    !contribution.Source.IsValid ||
                    contribution.Source == deathEvent.Target ||
                    contribution.Ability == AbilityId.None)
                {
                    continue;
                }

                AppendStep(
                    new DeathRecapStep(
                        contribution.Tick,
                        contribution.Source,
                        contribution.Ability,
                        contribution.Kind),
                    ref first,
                    ref second,
                    ref third,
                    ref stepCount);
            }

            if (!endedInHazard && deathEvent.Ability != AbilityId.None)
            {
                DeathRecapStep last = stepCount > 0
                    ? GetStep(first, second, third, stepCount - 1)
                    : default;
                if (stepCount == 0 ||
                    last.Source != deathEvent.Actor ||
                    last.Ability != deathEvent.Ability)
                {
                    AppendStep(
                        new DeathRecapStep(
                            deathEvent.Tick,
                            deathEvent.Actor,
                            deathEvent.Ability,
                            DeathContributionKind.Damage),
                        ref first,
                        ref second,
                        ref third,
                        ref stepCount);
                }
            }

            ParticipantId creditedActor = ResolveCreditedActor(
                slot,
                deathEvent,
                endedInHazard,
                first,
                second,
                third,
                stepCount);
            AbilityId finishingAbility = deathEvent.Ability != AbilityId.None
                ? deathEvent.Ability
                : stepCount > 0
                    ? GetStep(first, second, third, stepCount - 1).Ability
                    : AbilityId.None;
            bool knockbackSetup = endedInHazard &&
                HasRecentKnockbackFrom(slot, deathEvent.Tick, creditedActor);
            string chain = BuildChain(first, second, third, stepCount, endedInHazard);
            string hint = BuildHint(
                first,
                second,
                third,
                stepCount,
                finishingAbility,
                endedInHazard,
                knockbackSetup);

            _latestRecaps[slot] = new DeathRecap(
                deathEvent.Target,
                creditedActor,
                finishingAbility,
                endedInHazard,
                first,
                second,
                third,
                stepCount,
                chain,
                hint);
            _hasLatestRecap[slot] = true;
            _latestTicks[slot] = Math.Max(_latestTicks[slot], deathEvent.Tick);
        }

        private ParticipantId ResolveCreditedActor(
            int slot,
            CombatEvent deathEvent,
            bool endedInHazard,
            DeathRecapStep first,
            DeathRecapStep second,
            DeathRecapStep third,
            int stepCount)
        {
            if (!endedInHazard && deathEvent.Actor.IsValid && deathEvent.Actor != deathEvent.Target)
            {
                return deathEvent.Actor;
            }

            if (endedInHazard && TryFindRecentKnockbackSource(slot, deathEvent.Tick, out ParticipantId source))
            {
                return source;
            }

            ParticipantId latestSource = stepCount > 0
                ? GetStep(first, second, third, stepCount - 1).Source
                : default;
            return latestSource != deathEvent.Target ? latestSource : default;
        }

        private bool HasRecentHazard(int slot, int deathTick)
        {
            int count = _contributionCounts[slot];
            int start = count == ContributionsPerVictim
                ? _nextContributionIndices[slot]
                : 0;
            int baseIndex = slot * ContributionsPerVictim;
            for (int index = 0; index < count; index++)
            {
                DeathContribution contribution = _contributions[
                    baseIndex + ((start + index) % ContributionsPerVictim)];
                if (IsRecent(contribution, deathTick) &&
                    contribution.Kind == DeathContributionKind.Hazard &&
                    contribution.Tick == deathTick)
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryFindRecentKnockbackSource(
            int slot,
            int deathTick,
            out ParticipantId source)
        {
            int attributionTicks = Math.Min(_historyTicks, _tickRate * 4);
            int latestTick = int.MinValue;
            ParticipantId latestSource = default;
            int count = _contributionCounts[slot];
            int start = count == ContributionsPerVictim
                ? _nextContributionIndices[slot]
                : 0;
            int baseIndex = slot * ContributionsPerVictim;
            for (int index = 0; index < count; index++)
            {
                DeathContribution contribution = _contributions[
                    baseIndex + ((start + index) % ContributionsPerVictim)];
                if (!IsRecent(contribution, deathTick) ||
                    contribution.Kind != DeathContributionKind.Knockback ||
                    !contribution.Source.IsValid ||
                    deathTick - contribution.Tick > attributionTicks ||
                    contribution.Tick < latestTick)
                {
                    continue;
                }

                latestTick = contribution.Tick;
                latestSource = contribution.Source;
            }

            source = latestSource;
            return source.IsValid;
        }

        private bool HasRecentKnockbackFrom(
            int slot,
            int deathTick,
            ParticipantId source)
        {
            if (!source.IsValid)
            {
                return false;
            }

            int attributionTicks = Math.Min(_historyTicks, _tickRate * 4);
            int count = _contributionCounts[slot];
            int start = count == ContributionsPerVictim
                ? _nextContributionIndices[slot]
                : 0;
            int baseIndex = slot * ContributionsPerVictim;
            for (int index = 0; index < count; index++)
            {
                DeathContribution contribution = _contributions[
                    baseIndex + ((start + index) % ContributionsPerVictim)];
                if (IsRecent(contribution, deathTick) &&
                    contribution.Kind == DeathContributionKind.Knockback &&
                    contribution.Source == source &&
                    deathTick - contribution.Tick <= attributionTicks)
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsRecent(DeathContribution contribution, int currentTick)
        {
            int age = currentTick - contribution.Tick;
            return age >= 0 && age <= _historyTicks;
        }

        private int FindVictimSlot(ParticipantId victim)
        {
            if (!victim.IsValid)
            {
                return -1;
            }

            for (int slot = 0; slot < _victims.Length; slot++)
            {
                if (_victims[slot] == victim)
                {
                    return slot;
                }
            }

            return -1;
        }

        private int GetOrCreateVictimSlot(ParticipantId victim, int tick)
        {
            int existing = FindVictimSlot(victim);
            if (existing >= 0)
            {
                return existing;
            }

            int selected = -1;
            int oldestTick = int.MaxValue;
            for (int slot = 0; slot < _victims.Length; slot++)
            {
                if (!_victims[slot].IsValid)
                {
                    selected = slot;
                    break;
                }

                if (_latestTicks[slot] < oldestTick)
                {
                    oldestTick = _latestTicks[slot];
                    selected = slot;
                }
            }

            ClearVictimSlot(selected);
            _victims[selected] = victim;
            _latestTicks[selected] = tick;
            return selected;
        }

        private void ClearVictimSlot(int slot)
        {
            _victims[slot] = default;
            _contributionCounts[slot] = 0;
            _nextContributionIndices[slot] = 0;
            _latestTicks[slot] = 0;
            _latestRecaps[slot] = default;
            _hasLatestRecap[slot] = false;
            Array.Clear(
                _contributions,
                slot * ContributionsPerVictim,
                ContributionsPerVictim);
        }

        private static void AppendStep(
            DeathRecapStep step,
            ref DeathRecapStep first,
            ref DeathRecapStep second,
            ref DeathRecapStep third,
            ref int count)
        {
            if (count > 0)
            {
                DeathRecapStep last = GetStep(first, second, third, count - 1);
                if (last.Source == step.Source && last.Ability == step.Ability)
                {
                    SetStep(
                        count - 1,
                        step,
                        ref first,
                        ref second,
                        ref third);
                    return;
                }
            }

            if (count < MaximumRecapSteps)
            {
                SetStep(count, step, ref first, ref second, ref third);
                count++;
                return;
            }

            first = second;
            second = third;
            third = step;
        }

        private static DeathRecapStep GetStep(
            DeathRecapStep first,
            DeathRecapStep second,
            DeathRecapStep third,
            int index)
        {
            switch (index)
            {
                case 0:
                    return first;
                case 1:
                    return second;
                default:
                    return third;
            }
        }

        private static void SetStep(
            int index,
            DeathRecapStep step,
            ref DeathRecapStep first,
            ref DeathRecapStep second,
            ref DeathRecapStep third)
        {
            switch (index)
            {
                case 0:
                    first = step;
                    break;
                case 1:
                    second = step;
                    break;
                default:
                    third = step;
                    break;
            }
        }

        private static string BuildChain(
            DeathRecapStep first,
            DeathRecapStep second,
            DeathRecapStep third,
            int stepCount,
            bool endedInHazard)
        {
            string firstName = stepCount > 0 ? GetAbilityName(first.Ability) : string.Empty;
            string secondName = stepCount > 1 ? GetAbilityName(second.Ability) : string.Empty;
            string thirdName = stepCount > 2 ? GetAbilityName(third.Ability) : string.Empty;
            if (endedInHazard)
            {
                switch (stepCount)
                {
                    case 0:
                        return "Lava";
                    case 1:
                        return firstName + " → Lava";
                    case 2:
                        return firstName + " → " + secondName + " → Lava";
                    default:
                        return firstName + " → " + secondName + " → " + thirdName + " → Lava";
                }
            }

            switch (stepCount)
            {
                case 0:
                    return "Defeated";
                case 1:
                    return firstName;
                case 2:
                    return firstName + " → " + secondName;
                default:
                    return firstName + " → " + secondName + " → " + thirdName;
            }
        }

        private static string BuildHint(
            DeathRecapStep first,
            DeathRecapStep second,
            DeathRecapStep third,
            int stepCount,
            AbilityId finishingAbility,
            bool endedInHazard,
            bool knockbackSetup)
        {
            if (endedInHazard)
            {
                return knockbackSetup
                    ? "Keep safe ground behind you; save mobility for the knockback."
                    : "Return to safe ground before committing to another cast.";
            }

            if (finishingAbility == AbilityId.DeepFreeze)
            {
                return "Break the root setup or use a defensive before Deep Freeze.";
            }

            if (finishingAbility == AbilityId.Eviscerate ||
                finishingAbility == AbilityId.Envenom ||
                finishingAbility == AbilityId.KidneyShot)
            {
                return "Deny combo-point setup or disengage before the finisher.";
            }

            if (finishingAbility == AbilityId.BossMeteor)
            {
                return "Leave the warning area before the impact.";
            }

            if (finishingAbility == AbilityId.BossCleave)
            {
                return "Move out of the boss's front arc.";
            }

            for (int index = 0; index < stepCount; index++)
            {
                DeathRecapStep step = GetStep(first, second, third, index);
                if (step.Ability == AbilityId.Kick ||
                    step.Ability == AbilityId.Counterspell)
                {
                    return "Bait the interrupt before committing to a long cast.";
                }

                if (step.Kind == DeathContributionKind.Control)
                {
                    return "Use a cleanse, defensive, or escape before the control chain finishes.";
                }
            }

            return "Create distance or use a defensive before the next burst.";
        }

        private static string GetAbilityName(AbilityId ability)
        {
            return AbilityCatalog.TryGet(ability, out AbilityDefinition definition)
                ? definition.DisplayName
                : "Unknown attack";
        }

        private static bool IsControlStatus(StatusId status)
        {
            switch (status)
            {
                case StatusId.Silenced:
                case StatusId.Slowed:
                case StatusId.Feared:
                case StatusId.Stunned:
                case StatusId.Disoriented:
                case StatusId.Rooted:
                case StatusId.SchoolLocked:
                case StatusId.Horrified:
                case StatusId.Disarmed:
                case StatusId.Pacified:
                    return true;
                default:
                    return false;
            }
        }

        private readonly struct DeathContribution
        {
            public DeathContribution(
                int tick,
                ParticipantId source,
                AbilityId ability,
                DeathContributionKind kind)
            {
                Tick = tick;
                Source = source;
                Ability = ability;
                Kind = kind;
            }

            public int Tick { get; }

            public ParticipantId Source { get; }

            public AbilityId Ability { get; }

            public DeathContributionKind Kind { get; }
        }
    }
}
