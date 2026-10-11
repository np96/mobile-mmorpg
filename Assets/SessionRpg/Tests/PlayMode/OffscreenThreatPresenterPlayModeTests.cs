using System.Collections;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class OffscreenThreatPresenterPlayModeTests
    {
        private const int FriendlyTeam = 1;

        [UnitySetUp]
        public IEnumerator LoadBootstrap()
        {
            SceneManager.LoadScene("Bootstrap", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Bootstrap_AuthorsExactlyTwoCompactThreatIndicators()
        {
            OffscreenThreatPresenter[] presenters =
                Object.FindObjectsByType<OffscreenThreatPresenter>(
                    FindObjectsInactive.Include);
            Assert.That(presenters, Has.Length.EqualTo(1));
            Assert.That(
                presenters[0].IndicatorCount,
                Is.EqualTo(OffscreenThreatPresenter.MaximumIndicatorCount));
            Assert.That(presenters[0].GetIndicator(0), Is.Not.Null);
            Assert.That(presenters[0].GetIndicator(1), Is.Not.Null);
            Assert.That(presenters[0].VisibleCount, Is.Zero);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Presenter_SuppressesOnScreenEnemyAndClampsCastingThreatToEdge()
        {
            PrepareGameplay(
                out PrototypeGameController controller,
                out OffscreenThreatPresenter presenter,
                out UnityEngine.Camera gameCamera);
            yield return null;
            Canvas.ForceUpdateCanvases();

            CombatantSnapshot[] snapshots =
            {
                CreateSnapshot(
                    2,
                    ClassId.Rogue,
                    PositionForViewport(gameCamera, new Vector2(0.5f, 0.5f)),
                    88f,
                    true,
                    false,
                    AbilityId.None),
                CreateSnapshot(
                    3,
                    ClassId.Mage,
                    PositionForViewport(gameCamera, new Vector2(1.35f, 0.55f)),
                    73f,
                    true,
                    false,
                    AbilityId.Fireball),
            };

            presenter.Present(gameCamera, snapshots, snapshots.Length, FriendlyTeam);

            Assert.That(presenter.VisibleCount, Is.EqualTo(1));
            OffscreenThreatIndicatorView indicator = presenter.GetIndicator(0);
            Assert.That(indicator.IsVisible, Is.True);
            Assert.That(indicator.PresentedClass, Is.EqualTo(ClassId.Mage));
            Assert.That(indicator.HealthPercent, Is.EqualTo(73));
            Assert.That(indicator.IsCasting, Is.True);
            Assert.That(indicator.Direction.x, Is.GreaterThan(0.8f));
            Rect bounds = presenter.Bounds;
            Vector2 position = indicator.AnchoredPosition;
            Assert.That(
                position.x,
                Is.InRange(
                    bounds.xMin + presenter.HorizontalPadding - 0.5f,
                    bounds.xMax - presenter.HorizontalPadding + 0.5f));
            Assert.That(
                position.y,
                Is.InRange(
                    bounds.yMin + presenter.VerticalPadding - 0.5f,
                    bounds.yMax - presenter.VerticalPadding + 0.5f));

            presenter.Present(gameCamera, snapshots, 1, FriendlyTeam);
            Assert.That(presenter.VisibleCount, Is.Zero);
            Assert.That(indicator.IsVisible, Is.False);
            Object.Destroy(controller.gameObject);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Presenter_NeverRevealsStealthedOrDeadEnemies()
        {
            PrepareGameplay(
                out PrototypeGameController controller,
                out OffscreenThreatPresenter presenter,
                out UnityEngine.Camera gameCamera);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Float3 offscreen = PositionForViewport(gameCamera, new Vector2(-0.4f, 0.5f));
            CombatantSnapshot[] visibleEnemy =
            {
                CreateSnapshot(
                    2,
                    ClassId.Rogue,
                    offscreen,
                    100f,
                    true,
                    false,
                    AbilityId.None),
            };
            presenter.Present(gameCamera, visibleEnemy, visibleEnemy.Length, FriendlyTeam);
            Assert.That(presenter.VisibleCount, Is.EqualTo(1));

            CombatantSnapshot[] hiddenEnemies =
            {
                CreateSnapshot(
                    2,
                    ClassId.Rogue,
                    offscreen,
                    100f,
                    true,
                    true,
                    AbilityId.None),
                CreateSnapshot(
                    3,
                    ClassId.Mage,
                    offscreen,
                    0f,
                    false,
                    false,
                    AbilityId.None),
            };
            presenter.Present(gameCamera, hiddenEnemies, hiddenEnemies.Length, FriendlyTeam);

            Assert.That(presenter.VisibleCount, Is.Zero);
            Assert.That(presenter.GetIndicator(0).IsVisible, Is.False);
            Assert.That(presenter.GetIndicator(1).IsVisible, Is.False);
            Object.Destroy(controller.gameObject);
            yield return null;
        }

        private static void PrepareGameplay(
            out PrototypeGameController controller,
            out OffscreenThreatPresenter presenter,
            out UnityEngine.Camera gameCamera)
        {
            controller = Object.FindAnyObjectByType<PrototypeGameController>();
            controller.StartLocalSession(GameModeId.Arena, ClassId.Mage);
            controller.enabled = false;
            presenter = Object.FindAnyObjectByType<OffscreenThreatPresenter>(
                FindObjectsInactive.Include);
            gameCamera = Object.FindAnyObjectByType<UnityEngine.Camera>();
            Assert.That(presenter, Is.Not.Null);
            Assert.That(gameCamera, Is.Not.Null);
            presenter.Clear();
        }

        private static Float3 PositionForViewport(
            UnityEngine.Camera gameCamera,
            Vector2 viewport)
        {
            Ray ray = gameCamera.ViewportPointToRay(
                new Vector3(viewport.x, viewport.y, 0f));
            Vector3 world = ray.GetPoint(18f);
            return new Float3(world.x, world.y - 1.6f, world.z);
        }

        private static CombatantSnapshot CreateSnapshot(
            int participantValue,
            ClassId classId,
            Float3 position,
            float health,
            bool alive,
            bool stealthed,
            AbilityId castingAbility)
        {
            StatusFlags statuses = stealthed
                ? StatusFlags.Stealthed
                : StatusFlags.None;
            AuraTag auraTags = stealthed
                ? AuraTag.Stealth | AuraTag.StealthAccess
                : AuraTag.None;
            return new CombatantSnapshot(
                new ParticipantId(participantValue),
                new TeamId(2),
                classId,
                SpecializationCatalog.GetDefault(classId),
                position,
                new Float3(0f, 0f, 1f),
                health,
                100f,
                0f,
                castingAbility,
                0,
                castingAbility == AbilityId.None ? 0 : 30,
                castingAbility == AbilityId.None
                    ? AbilityActivationType.Instant
                    : AbilityActivationType.Cast,
                SpellSchoolMask.None,
                ResourceType.None,
                0f,
                0f,
                0,
                default,
                0,
                SpellSchoolMask.None,
                alive,
                false,
                statuses,
                auraTags);
        }
    }
}
