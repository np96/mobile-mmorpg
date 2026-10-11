namespace SessionRpg.Core
{
    /// <summary>
    /// Shadow Priest rules that sit across otherwise generic aura and damage
    /// processing. The numeric output is prototype tuned; the relationships are
    /// faithful to the Wrath arena kit.
    /// </summary>
    public sealed partial class CombatSimulation
    {
        private const float VampiricEmbraceSelfHealingFraction = 0.15f;
        private const float VampiricEmbracePartyHealingFraction = 0.03f;

        private void ApplySpecializationPassives(CombatantState actor)
        {
            SpecializationDefinition definition =
                SpecializationCatalog.GetDefinition(actor.Specialization);
            ApplyPassiveAura(actor, definition.PassiveAura0);
            ApplyPassiveAura(actor, definition.PassiveAura1);
        }

        private static void ApplyPassiveAura(CombatantState actor, AuraId auraId)
        {
            int auraIndex = (int)auraId;
            if (auraIndex <= 0 || auraIndex >= AuraCatalog.StorageCapacity)
            {
                return;
            }

            actor.Auras[auraIndex] = new AuraState(
                true,
                actor.Id,
                AbilityId.None,
                int.MaxValue,
                0,
                1,
                0f);
        }

        private void RemovePriorUniqueAura(
            int sourceIndex,
            int targetIndex,
            AuraId auraId,
            AbilityId ability)
        {
            ParticipantId source = _combatants[sourceIndex].Id;
            for (int index = 0; index < _combatantCount; index++)
            {
                if (index == targetIndex)
                {
                    continue;
                }

                AuraState state = _combatants[index].Auras[(int)auraId];
                if (IsAuraActive(state) && state.Source == source)
                {
                    RemoveAura(index, auraId, source, ability, false);
                }
            }
        }

        private void TriggerVampiricEmbrace(
            int sourceIndex,
            float healthDamage,
            AbilityId ability,
            SpellSchoolMask school,
            bool isHazard,
            bool isBacklash)
        {
            if (healthDamage <= 0f || isHazard || isBacklash ||
                (school & SpellSchoolMask.Shadow) == 0 ||
                !HasAura(_combatants[sourceIndex], AuraId.VampiricEmbrace) ||
                !CanTriggerVampiricEmbrace(ability))
            {
                return;
            }

            TeamId team = _combatants[sourceIndex].Team;
            for (int targetIndex = 0; targetIndex < _combatantCount; targetIndex++)
            {
                CombatantState target = _combatants[targetIndex];
                if (!target.IsAlive || target.Team != team)
                {
                    continue;
                }

                float fraction = targetIndex == sourceIndex
                    ? VampiricEmbraceSelfHealingFraction
                    : VampiricEmbracePartyHealingFraction;
                ApplyHealing(
                    sourceIndex,
                    targetIndex,
                    healthDamage * fraction,
                    ability);
            }
        }

        private static bool CanTriggerVampiricEmbrace(AbilityId ability)
        {
            switch (ability)
            {
                case AbilityId.VampiricTouch:
                case AbilityId.ShadowWordPain:
                case AbilityId.DevouringPlague:
                case AbilityId.MindFlay:
                case AbilityId.MindBlast:
                case AbilityId.ShadowWordDeath:
                case AbilityId.ManaBurn:
                    return true;
                default:
                    return false;
            }
        }
    }
}
