using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class SiegeAreaBotTests
    {
        [TestCase(ClassId.Priest, SpecializationId.Discipline, AbilityId.DawnNova)]
        [TestCase(ClassId.Priest, SpecializationId.Shadow, AbilityId.UmbralCollapse)]
        [TestCase(ClassId.Rogue, SpecializationId.ShadowDance, AbilityId.BladeTempest)]
        [TestCase(ClassId.Rogue, SpecializationId.Mutilate, AbilityId.BladeTempest)]
        [TestCase(ClassId.Mage, SpecializationId.Fire, AbilityId.FlameSurge)]
        [TestCase(ClassId.Mage, SpecializationId.Frost, AbilityId.Blizzard)]
        [TestCase(ClassId.Hunter, SpecializationId.Marksmanship, AbilityId.ArrowRain)]
        public void DefenderBot_UsesItsClassAreaAbilityForAHostileClusterInSiege(
            ClassId classId,
            SpecializationId specialization,
            AbilityId expectedAbility)
        {
            CombatSimulation simulation = new CombatSimulation(
                new CombatRules(
                    30,
                    0f,
                    100f,
                    120f,
                    0f,
                    1.5f,
                    15f,
                    5,
                    ArenaCollisionMap.None,
                    false,
                    playableMode: PlayableModeId.HeroSiege));
            Assert.That(
                simulation.AddCombatant(
                    new ParticipantId(1),
                    new TeamId(1),
                    classId,
                    specialization,
                    default),
                Is.True);
            Assert.That(
                simulation.AddCombatant(
                    new ParticipantId(2),
                    new TeamId(2),
                    ClassId.Mage,
                    SpecializationId.Fire,
                    new Float3(2f, 0f, 0f)),
                Is.True);
            Assert.That(
                simulation.AddCombatant(
                    new ParticipantId(3),
                    new TeamId(2),
                    ClassId.Priest,
                    SpecializationId.Shadow,
                    new Float3(-2f, 0f, 0f)),
                Is.True);

            var snapshots = new CombatantSnapshot[CombatSimulation.MaxCombatants];
            int count = simulation.CopySnapshots(snapshots);
            var brain = new DeterministicBotBrain(701);

            Assert.That(
                brain.TryBuildCommand(
                    simulation,
                    snapshots[0],
                    snapshots,
                    count,
                    out CombatCommand command),
                Is.True);
            Assert.That(command.Ability, Is.EqualTo(expectedAbility));
            Assert.That(command.Actor, Is.EqualTo(new ParticipantId(1)));
        }
    }
}
