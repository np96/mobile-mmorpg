using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.Input;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class RunModePoolContractPlayModeTests
    {
        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HeroSiege_VariantsUseTheTwelveViewPoolWithoutEagerHostiles()
        {
            CombatantView[] authoredPool = Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Include);
            Assert.That(authoredPool, Has.Length.EqualTo(12));

            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(controller, Is.Not.Null);
            controller.StartLocalSiegeSession(
                SiegeVariantId.SoloLane,
                ClassId.Mage,
                SpecializationId.Frost);
            yield return null;

            CombatantView[] active = Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Exclude);
            Assert.That(active, Has.Length.EqualTo(1));
            Assert.That(
                active.Count(value => value.Team.Value == 1),
                Is.EqualTo(1));
            Assert.That(
                active.Count(value => value.Team.Value == 2),
                Is.Zero,
                "Timed Siege waves must not spawn during preparation.");

            controller.ReturnHome();
            yield return null;

            controller.StartLocalSiegeSession(
                SiegeVariantId.FourLanes,
                ClassId.Mage,
                SpecializationId.Frost);
            yield return null;

            active = Object.FindObjectsByType<CombatantView>(
                FindObjectsInactive.Exclude);
            Assert.That(active, Has.Length.EqualTo(4));
            Assert.That(
                active.All(value => value.Team.Value == 1 &&
                    value.EnemyTier == RunEnemyTier.None),
                Is.True);
            Assert.That(
                active.Select(value => value.Id.Value).Distinct().Count(),
                Is.EqualTo(4));
            Assert.That(
                active.All(value => authoredPool.Contains(value)),
                Is.True,
                "The run must reuse the authored pool rather than instantiate views.");

            controller.ReturnHome();
            yield return null;

            Assert.That(
                Object.FindObjectsByType<CombatantView>(
                    FindObjectsInactive.Exclude),
                Is.Empty);
            Assert.That(
                Object.FindObjectsByType<CombatantView>(
                    FindObjectsInactive.Include),
                Is.EquivalentTo(authoredPool));
        }

        [UnityTest]
        public IEnumerator SiegeCombatantView_DistinguishesNineArchetypesAndReleasesAllState()
        {
            CombatantView view = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Include)
                .First(value => !value.gameObject.activeSelf);
            string[] seenBadges = new string[SiegeEnemyArchetypeCatalog.Count];
            Color[] seenAccents = new Color[SiegeEnemyArchetypeCatalog.Count];

            for (int index = 0; index < SiegeEnemyArchetypeCatalog.Count; index++)
            {
                SiegeEnemyArchetypeDefinition definition =
                    SiegeEnemyArchetypeCatalog.GetByIndex(index);
                ParticipantId id = new ParticipantId(8000 + index);
                TeamId team = new TeamId(2);
                view.Initialize(id, team, definition.Class, false, Vector3.zero);
                Transform modelRoot = view.ActiveCharacterModelRoot;
                Assert.That(modelRoot, Is.Not.Null);
                Vector3 authoredScale = modelRoot.localScale;

                view.ConfigureSiegeArchetype(definition.Id);

                Assert.That(view.SiegeArchetype, Is.EqualTo(definition.Id));
                Assert.That(view.EnemyTier, Is.EqualTo(definition.Tier));
                Assert.That(
                    Vector3.Distance(
                        modelRoot.localScale,
                        authoredScale * definition.PresentationScale),
                    Is.LessThan(0.0001f),
                    definition.DisplayName + " must use its catalogued presentation scale.");
                CombatantRelationMarker marker = view
                    .GetComponentInChildren<CombatantRelationMarker>(true);
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.ClassBadgeText, Has.Length.EqualTo(2));
                seenBadges[index] = marker.ClassBadgeText;
                seenAccents[index] = marker
                    .GetComponentsInChildren<TextMesh>(true)
                    .Single(value => value.text == marker.ClassBadgeText)
                    .color;

                view.Present(
                    new CombatantSnapshot(
                        id,
                        team,
                        definition.Class,
                        default,
                        new Float3(0f, 0f, 1f),
                        75f,
                        100f,
                        25f,
                        definition.PrimaryAbility,
                        30,
                        true,
                        false),
                    1,
                    30,
                    6f,
                    1);
                view.SetSelected(true);
                view.SetControlVisualState(CombatantControlVisualState.Stun);
                view.transform.localScale = Vector3.one * 1.35f;
                Assert.That(view.IsTargetable, Is.True);
                Assert.That(view.transform.Find("TargetRing").gameObject.activeSelf, Is.True);
                Assert.That(view.AuraVisuals.ActiveMotifs, Is.Not.EqualTo(AuraVisualMotif.None));

                if (definition.Class == ClassId.Hunter)
                {
                    view.PresentCompanion(
                        new CompanionSnapshot(
                            id,
                            team,
                            default,
                            new Float3(0f, 0f, 1f),
                            50f,
                            50f,
                            true,
                            CompanionOrder.Attack,
                            default));
                    Assert.That(view.CompanionVisuals.IsVisible, Is.True);
                }

                CombatantAnimationPresenter animation =
                    view.GetComponent<CombatantAnimationPresenter>();
                Assert.That(animation.ActiveAbility, Is.EqualTo(definition.PrimaryAbility));

                view.ReleaseToPool();

                SoftTargetCandidate candidate = view.GetComponent<SoftTargetCandidate>();
                Assert.That(view.gameObject.activeSelf, Is.False);
                Assert.That(view.Id.IsValid, Is.False);
                Assert.That(view.Team.Value, Is.Zero);
                Assert.That(view.Class, Is.EqualTo(ClassId.None));
                Assert.That(view.EnemyTier, Is.EqualTo(RunEnemyTier.None));
                Assert.That(
                    view.SiegeArchetype,
                    Is.EqualTo(SiegeEnemyArchetypeId.None));
                Assert.That(view.IsHuman, Is.False);
                Assert.That(view.IsTargetable, Is.False);
                Assert.That(candidate.TargetId, Is.Zero);
                Assert.That(candidate.TeamId, Is.Zero);
                Assert.That(view.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(modelRoot.localScale, Is.EqualTo(authoredScale));
                Assert.That(view.ActiveCharacterModelRoot, Is.Null);
                Assert.That(view.AuraVisuals.ActiveMotifs, Is.EqualTo(AuraVisualMotif.None));
                Assert.That(
                    view.AuraVisuals.ActivePersistentMotifs,
                    Is.EqualTo(PersistentAuraVisual.None));
                Assert.That(view.CompanionVisuals.IsVisible, Is.False);
                Assert.That(animation.ActiveAbility, Is.EqualTo(AbilityId.None));
                Assert.That(view.transform.Find("TargetRing").gameObject.activeSelf, Is.False);

                // Pool release is deliberately idempotent for unused authored slots.
                Assert.DoesNotThrow(view.ReleaseToPool);
            }

            Assert.That(seenBadges.Distinct().Count(), Is.EqualTo(seenBadges.Length));
            Assert.That(seenAccents.Distinct().Count(), Is.EqualTo(seenAccents.Length));
            yield return null;
        }

        [UnityTest]
        public IEnumerator SiegeSwarmPresentation_ReusesTwoUntargetableFollowersAndTracksVisibility()
        {
            CombatantView view = Object
                .FindObjectsByType<CombatantView>(FindObjectsInactive.Include)
                .First(value => !value.gameObject.activeSelf);
            SiegeEnemyArchetypeDefinition ashling = SiegeEnemyArchetypeCatalog.Get(
                SiegeEnemyArchetypeId.Ashling);
            ParticipantId id = new ParticipantId(9101);
            TeamId team = new TeamId(2);

            view.Initialize(id, team, ashling.Class, false, Vector3.zero);
            view.ConfigureSiegeArchetype(ashling.Id);
            view.Present(CreateSiegeViewSnapshot(ashling, id, team, true, false), 1, 30, 6f);

            Transform[] followers = FindSiegeFollowers(view);
            Assert.That(followers, Has.Length.EqualTo(2));
            Assert.That(view.SiegePresentationBodyCount, Is.EqualTo(3));
            Assert.That(followers.All(value => value.gameObject.activeInHierarchy), Is.True);
            Assert.That(followers[0].localPosition.x, Is.LessThan(0f));
            Assert.That(followers[1].localPosition.x, Is.GreaterThan(0f));
            Assert.That(followers.All(value => value.localPosition.z < 0f), Is.True);
            Assert.That(
                followers.All(value => value.localScale == view.ActiveCharacterModelRoot.localScale),
                Is.True,
                "Every swarm body must use its authoritative archetype's presentation scale.");
            for (int index = 0; index < followers.Length; index++)
            {
                GameObject follower = followers[index].gameObject;
                Assert.That(
                    follower.GetComponentInChildren<SoftTargetCandidate>(true),
                    Is.Null);
                Assert.That(
                    follower.GetComponentsInChildren<Collider>(true),
                    Is.Empty);
                Assert.That(
                    follower.GetComponentInChildren<CombatantView>(true),
                    Is.Null);
                Assert.That(
                    follower.GetComponentInChildren<CombatantRelationMarker>(true),
                    Is.Null);
                Assert.That(
                    follower.GetComponentInChildren<AuraVisualPresenter>(true),
                    Is.Null);
                Assert.That(
                    follower.GetComponentInChildren<TextMesh>(true),
                    Is.Null);
                Assert.That(follower.transform.Find("WorldStatusBars"), Is.Null);
                Assert.That(follower.GetComponentInChildren<Animator>(true), Is.Not.Null);
            }

            Animator mainAnimator = view.GetComponent<CombatantAnimationPresenter>().ActiveAnimator;
            int moveSpeedHash = Animator.StringToHash(
                CombatantAnimationPresenter.MoveSpeedParameter);
            int deadHash = Animator.StringToHash(CombatantAnimationPresenter.DeadParameter);
            int castingHash = Animator.StringToHash(
                CombatantAnimationPresenter.CastingParameter);
            view.SetCastPresentation(AbilityId.Fireball, 0.35f, false);
            for (int index = 0; index < followers.Length; index++)
            {
                Animator followerAnimator = followers[index]
                    .GetComponentInChildren<Animator>(true);
                Assert.That(
                    followerAnimator.GetFloat(moveSpeedHash),
                    Is.EqualTo(mainAnimator.GetFloat(moveSpeedHash)).Within(0.0001f));
                Assert.That(
                    followerAnimator.GetBool(castingHash),
                    Is.EqualTo(mainAnimator.GetBool(castingHash)));
            }

            view.Present(CreateSiegeViewSnapshot(ashling, id, team, true, true), 2, 30, 6f);
            Assert.That(view.SiegePresentationBodyCount, Is.Zero);
            Assert.That(followers.All(value => !value.gameObject.activeSelf), Is.True);

            view.Present(CreateSiegeViewSnapshot(ashling, id, team, true, false), 3, 30, 6f);
            Assert.That(view.SiegePresentationBodyCount, Is.EqualTo(3));
            view.Present(CreateSiegeViewSnapshot(ashling, id, team, false, false), 4, 30, 6f);
            Assert.That(view.SiegePresentationBodyCount, Is.EqualTo(1));
            Assert.That(followers.All(value => !value.gameObject.activeSelf), Is.True);
            for (int index = 0; index < followers.Length; index++)
            {
                Assert.That(
                    followers[index].GetComponentInChildren<Animator>(true).GetBool(deadHash),
                    Is.EqualTo(mainAnimator.GetBool(deadHash)));
            }

            Transform[] originalFollowers = followers.ToArray();
            view.ReleaseToPool();
            Assert.That(view.SiegePresentationBodyCount, Is.Zero);
            Assert.That(followers.All(value => !value.gameObject.activeSelf), Is.True);

            SiegeEnemyArchetypeDefinition runner = SiegeEnemyArchetypeCatalog.Get(
                SiegeEnemyArchetypeId.CinderRunner);
            id = new ParticipantId(9102);
            view.Initialize(id, team, runner.Class, false, Vector3.zero);
            view.ConfigureSiegeArchetype(runner.Id);
            view.Present(CreateSiegeViewSnapshot(runner, id, team, true, false), 1, 30, 6f);
            Transform[] reusedFollowers = FindSiegeFollowers(view);
            Assert.That(reusedFollowers, Has.Length.EqualTo(originalFollowers.Length));
            for (int index = 0; index < reusedFollowers.Length; index++)
            {
                Assert.That(reusedFollowers[index], Is.SameAs(originalFollowers[index]));
            }
            Assert.That(view.SiegePresentationBodyCount, Is.EqualTo(3));
            Assert.That(
                reusedFollowers.All(
                    value => value.localScale == view.ActiveCharacterModelRoot.localScale),
                Is.True);

            view.ReleaseToPool();
            SiegeEnemyArchetypeDefinition raider = SiegeEnemyArchetypeCatalog.Get(
                SiegeEnemyArchetypeId.AshboundRaider);
            id = new ParticipantId(9103);
            view.Initialize(id, team, raider.Class, false, Vector3.zero);
            view.ConfigureSiegeArchetype(raider.Id);
            view.Present(CreateSiegeViewSnapshot(raider, id, team, true, false), 1, 30, 6f);
            Assert.That(view.SiegePresentationBodyCount, Is.EqualTo(1));
            Assert.That(FindSiegeFollowers(view), Has.Length.EqualTo(2));
            Assert.That(
                FindSiegeFollowers(view).All(value => !value.gameObject.activeSelf),
                Is.True,
                "Medium and elite enemies must not inherit swarm follower visibility.");

            view.ReleaseToPool();
            yield return null;
        }

        [UnityTest]
        public IEnumerator TutorialReset_ReplacesTrainingAndLevelingStateTogether()
        {
            PrototypeGameController controller =
                Object.FindAnyObjectByType<PrototypeGameController>();
            Assert.That(controller, Is.Not.Null);
            controller.StartLocalSession(
                GameModeId.Leveling,
                ClassId.Mage,
                SpecializationId.Frost);
            yield return null;

            GuidedTrainingController originalTraining =
                ReadPrivate<GuidedTrainingController>(controller, "_training");
            LevelingRunController originalRun =
                ReadPrivate<LevelingRunController>(controller, "_levelingRun");
            MethodInfo reset = typeof(PrototypeGameController).GetMethod(
                "OnTutorialResetRequested",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(reset, Is.Not.Null);

            reset.Invoke(controller, null);
            yield return null;

            GuidedTrainingController restartedTraining =
                ReadPrivate<GuidedTrainingController>(controller, "_training");
            LevelingRunController restartedRun =
                ReadPrivate<LevelingRunController>(controller, "_levelingRun");
            Assert.That(restartedTraining, Is.Not.SameAs(originalTraining));
            Assert.That(restartedRun, Is.Not.SameAs(originalRun));
            Assert.That(
                restartedTraining.Status,
                Is.EqualTo(GuidedTrainingStatus.InProgress));
            Assert.That(
                restartedTraining.CurrentStep,
                Is.EqualTo(GuidedTrainingStep.Movement));
            Assert.That(restartedTraining.Snapshot.ClearedPackCount, Is.Zero);
            Assert.That(restartedRun.Progression.Level, Is.EqualTo(1));
        }

        private static T ReadPrivate<T>(object target, string fieldName)
            where T : class
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return field.GetValue(target) as T;
        }

        private static Transform[] FindSiegeFollowers(CombatantView view) =>
            view.GetComponentsInChildren<Transform>(true)
                .Where(value => value.parent == view.transform &&
                    value.name.StartsWith("SiegeSwarmFollower_"))
                .OrderBy(value => value.name)
                .ToArray();

        private static CombatantSnapshot CreateSiegeViewSnapshot(
            SiegeEnemyArchetypeDefinition definition,
            ParticipantId id,
            TeamId team,
            bool alive,
            bool stealthed)
        {
            ResourceProfile resource =
                SpecializationCatalog.GetResourceProfile(definition.Specialization);
            return new CombatantSnapshot(
                id,
                team,
                definition.Class,
                definition.Specialization,
                default,
                new Float3(0f, 0f, 1f),
                alive ? 75f : 0f,
                100f,
                0f,
                AbilityId.None,
                0,
                0,
                AbilityActivationType.Instant,
                SpellSchoolMask.None,
                resource.Type,
                resource.Initial,
                resource.Maximum,
                0,
                default,
                0,
                SpellSchoolMask.None,
                alive,
                false,
                stealthed ? StatusFlags.Stealthed : StatusFlags.None,
                stealthed
                    ? AuraTag.Stealth | AuraTag.StealthAccess
                    : AuraTag.None);
        }
    }
}
