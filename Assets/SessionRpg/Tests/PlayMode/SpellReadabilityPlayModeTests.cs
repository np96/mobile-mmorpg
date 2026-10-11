using System.Collections;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class SpellReadabilityPlayModeTests
    {
        [Test]
        public void AreaAbilities_UseMechanicallyDistinctSilhouettesWithoutDependingOnColor()
        {
            TelegraphVisualMotif frost = TelegraphPresenter.ResolveMotif(AbilityId.FrostNova);
            TelegraphVisualMotif knockback = TelegraphPresenter.ResolveMotif(AbilityId.BlastWave);
            TelegraphVisualMotif fire = TelegraphPresenter.ResolveMotif(AbilityId.DragonBreath);
            TelegraphVisualMotif fear = TelegraphPresenter.ResolveMotif(AbilityId.PsychicScream);
            TelegraphVisualMotif polymorph = TelegraphPresenter.ResolveMotif(AbilityId.Polymorph);

            Assert.That(frost, Is.EqualTo(TelegraphVisualMotif.FrostBurst));
            Assert.That(knockback, Is.EqualTo(TelegraphVisualMotif.Shockwave));
            Assert.That(fire, Is.EqualTo(TelegraphVisualMotif.FlameBurst));
            Assert.That(fear, Is.EqualTo(TelegraphVisualMotif.PsychicBurst));
            Assert.That(polymorph, Is.EqualTo(TelegraphVisualMotif.TargetLock));
            Assert.That(frost, Is.Not.EqualTo(knockback));
            Assert.That(knockback, Is.Not.EqualTo(fire));
            Assert.That(fire, Is.Not.EqualTo(fear));
            Assert.That(fear, Is.Not.EqualTo(polymorph));
        }

        [Test]
        public void AuraMotifs_DescribeStateAndNeverRevealHostileStealth()
        {
            CombatantSnapshot visible = CreateSnapshot(
                team: 1,
                barrier: 24f,
                statuses: StatusFlags.Burning | StatusFlags.Stunned,
                tags: AuraTag.Absorb | AuraTag.Frozen | AuraTag.Poison);
            AuraVisualMotif visibleMotifs = AuraVisualPresenter.ResolveMotifs(visible, 1);
            Assert.That((visibleMotifs & AuraVisualMotif.WardBands) != 0, Is.True);
            Assert.That((visibleMotifs & AuraVisualMotif.FrozenSpikes) != 0, Is.True);
            Assert.That((visibleMotifs & AuraVisualMotif.ControlCrown) != 0, Is.True);
            Assert.That((visibleMotifs & AuraVisualMotif.FlameWisps) != 0, Is.True);
            Assert.That((visibleMotifs & AuraVisualMotif.PoisonOrbit) != 0, Is.True);

            CombatantSnapshot hostileStealth = CreateSnapshot(
                team: 2,
                barrier: 24f,
                statuses: StatusFlags.Burning | StatusFlags.Stealthed,
                tags: AuraTag.Absorb | AuraTag.Stealth);
            Assert.That(
                AuraVisualPresenter.ResolveMotifs(hostileStealth, 1),
                Is.EqualTo(AuraVisualMotif.None));

            CombatantSnapshot friendlyStealth = CreateSnapshot(
                team: 1,
                barrier: 0f,
                statuses: StatusFlags.Stealthed,
                tags: AuraTag.Stealth);
            AuraVisualMotif friendlyMotifs =
                AuraVisualPresenter.ResolveMotifs(friendlyStealth, 1);
            Assert.That(
                (friendlyMotifs & AuraVisualMotif.FriendlyStealthTrace) != 0,
                Is.True);

            CombatantSnapshot polymorphed = CreateSnapshot(
                team: 2,
                barrier: 0f,
                statuses: StatusFlags.Disoriented,
                tags: AuraTag.Disorient | AuraTag.Periodic);
            AuraVisualMotif polymorphMotifs =
                AuraVisualPresenter.ResolveMotifs(polymorphed, 1);
            Assert.That(
                (polymorphMotifs & AuraVisualMotif.PolymorphCloud) != 0,
                Is.True);
            Assert.That(
                (polymorphMotifs & AuraVisualMotif.ControlCrown) == 0,
                Is.True);
        }

        [UnityTest]
        public IEnumerator AuthoredFoundation_UsesBoundedReusableSpellMotifs()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;

            GameObject combatantRoot = GameObject.Find("Combatants");
            Assert.That(combatantRoot, Is.Not.Null);
            AuraVisualPresenter[] auras =
                combatantRoot.GetComponentsInChildren<AuraVisualPresenter>(true);
            Assert.That(
                auras,
                Has.Length.EqualTo(CombatSimulation.MaxCombatants));
            for (int index = 0; index < auras.Length; index++)
            {
                Assert.That(auras[index].SerializedRendererCount, Is.EqualTo(37));
                Assert.That(
                    auras[index].HasAuthoredPersistentMotifs,
                    Is.True,
                    "Every combatant needs the bounded class-specific aura grammar.");
                Assert.That(
                    auras[index].PersistentRendererCount,
                    Is.EqualTo(AuraVisualPresenter.AuthoredPersistentRendererCount));
                Assert.That(auras[index].ActiveMotifs, Is.EqualTo(AuraVisualMotif.None));
            }

            TelegraphPresenter[] telegraphs = Object.FindObjectsByType<TelegraphPresenter>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(
                telegraphs,
                Has.Length.EqualTo(4),
                "All remaining authored telegraphs belong to boss mechanics; ability drag previews were removed.");
            TelegraphPresenter presenter = telegraphs[0];
            Assert.That(presenter.SerializedAccentRendererCount, Is.EqualTo(21));

            presenter.ShowResolved(
                AbilityId.FrostNova,
                Vector3.zero,
                4f,
                0.3f,
                Vector3.forward,
                true);
            Assert.That(presenter.ActiveMotif, Is.EqualTo(TelegraphVisualMotif.FrostBurst));
            Assert.That(presenter.LastResolutionConnected, Is.True);

            presenter.ShowResolved(
                AbilityId.BlastWave,
                Vector3.zero,
                6f,
                0.3f,
                Vector3.forward,
                false);
            Assert.That(presenter.ActiveMotif, Is.EqualTo(TelegraphVisualMotif.Shockwave));
            Assert.That(presenter.LastResolutionConnected, Is.False);
            yield return null;
        }

        private static CombatantSnapshot CreateSnapshot(
            int team,
            float barrier,
            StatusFlags statuses,
            AuraTag tags)
        {
            return new CombatantSnapshot(
                new ParticipantId(1),
                new TeamId(team),
                ClassId.Mage,
                SpecializationId.Frost,
                new Float3(0f, 0f, 0f),
                new Float3(0f, 0f, 1f),
                100f,
                100f,
                barrier,
                AbilityId.None,
                0,
                0,
                AbilityActivationType.Instant,
                SpellSchoolMask.None,
                ResourceType.Mana,
                100f,
                100f,
                0,
                default,
                0,
                SpellSchoolMask.None,
                true,
                false,
                statuses,
                tags);
        }
    }
}
