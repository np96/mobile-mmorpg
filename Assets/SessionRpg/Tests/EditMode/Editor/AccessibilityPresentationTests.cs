using System.Reflection;
using NUnit.Framework;
using SessionRpg.Unity.Camera;
using SessionRpg.Unity.Input;
using SessionRpg.Unity.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Editor.Tests
{
    public sealed class AccessibilityPresentationTests
    {
        [Test]
        public void ReducedMotion_ClearsAndSuppressesCombatImpulse()
        {
            var cameraObject = new GameObject(
                "Accessibility Camera",
                typeof(UnityEngine.Camera),
                typeof(StrategyFollowCamera));
            try
            {
                StrategyFollowCamera camera =
                    cameraObject.GetComponent<StrategyFollowCamera>();
                camera.AddCombatImpulse(1f);
                Assert.That(ReadCombatImpulse(camera), Is.GreaterThan(0f));

                camera.SetReducedMotion(true);

                Assert.That(camera.ReducedMotion, Is.True);
                Assert.That(ReadCombatImpulse(camera), Is.Zero);
                camera.AddCombatImpulse(1f);
                Assert.That(ReadCombatImpulse(camera), Is.Zero);

                camera.SetReducedMotion(false);
                camera.AddCombatImpulse(1f);
                Assert.That(ReadCombatImpulse(camera), Is.GreaterThan(0f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void CameraOrbit_ChangesOnlyWrappedYaw()
        {
            var cameraObject = new GameObject(
                "Orbit Camera",
                typeof(UnityEngine.Camera),
                typeof(StrategyFollowCamera));
            try
            {
                StrategyFollowCamera camera =
                    cameraObject.GetComponent<StrategyFollowCamera>();
                camera.SetFixedComposition(350f, 56f, 18f, 3.2f, 1.35f, 46f);
                camera.RotateHeading(25f);

                Assert.That(camera.Yaw, Is.EqualTo(15f).Within(0.001f));
                Assert.That(camera.Pitch, Is.EqualTo(56f).Within(0.001f));
                Assert.That(camera.Distance, Is.EqualTo(18f).Within(0.001f));
                Assert.That(camera.FieldOfView, Is.EqualTo(46f).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(cameraObject);
            }
        }

        [Test]
        public void RadialGuide_TracksBoundAndSelectedSectorsWithoutRaycasts()
        {
            var guideObject = new GameObject(
                "Radial Guide",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(AbilityRadialGuideGraphic));
            try
            {
                AbilityRadialGuideGraphic guide =
                    guideObject.GetComponent<AbilityRadialGuideGraphic>();
                guide.raycastTarget = false;
                guide.SetGuideState(
                    true,
                    true,
                    false,
                    true,
                    true,
                    AbilityFlickDirection.Right);

                Assert.That(guide.IsExpanded, Is.True);
                Assert.That(guide.BoundDirectionCount, Is.EqualTo(3));
                Assert.That(guide.HasDirection(AbilityFlickDirection.Up), Is.True);
                Assert.That(guide.HasDirection(AbilityFlickDirection.Down), Is.False);
                Assert.That(
                    guide.SelectedDirection,
                    Is.EqualTo(AbilityFlickDirection.Right));
                Assert.That(guide.raycastTarget, Is.False);

                guide.SetGuideState(
                    false,
                    true,
                    false,
                    true,
                    true,
                    AbilityFlickDirection.Right);
                Assert.That(guide.IsExpanded, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(guideObject);
            }
        }

        [Test]
        public void HudHighContrast_PropagatesToEveryFighterCard()
        {
            var hudObject = new GameObject("Accessibility HUD");
            var firstFighterObject = new GameObject("First Fighter");
            var secondFighterObject = new GameObject("Second Fighter");
            try
            {
                hudObject.SetActive(false);
                firstFighterObject.SetActive(false);
                secondFighterObject.SetActive(false);
                PrototypeHud hud = hudObject.AddComponent<PrototypeHud>();
                CombatantStatusView first =
                    firstFighterObject.AddComponent<CombatantStatusView>();
                CombatantStatusView second =
                    secondFighterObject.AddComponent<CombatantStatusView>();
                SetPrivateField(
                    hud,
                    "_fighterViews",
                    new[] { first, second });

                hud.SetHighContrast(true);

                Assert.That(hud.HighContrastEnabled, Is.True);
                Assert.That(first.HighContrastEnabled, Is.True);
                Assert.That(second.HighContrastEnabled, Is.True);

                hud.SetHighContrast(false);

                Assert.That(hud.HighContrastEnabled, Is.False);
                Assert.That(first.HighContrastEnabled, Is.False);
                Assert.That(second.HighContrastEnabled, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
                Object.DestroyImmediate(firstFighterObject);
                Object.DestroyImmediate(secondFighterObject);
            }
        }

        [Test]
        public void HudValidation_PreservesEightCardPvePool()
        {
            var hudObject = new GameObject("HUD Pool Validation");
            try
            {
                PrototypeHud hud = hudObject.AddComponent<PrototypeHud>();
                SetPrivateField(
                    hud,
                    "_fighterViews",
                    new CombatantStatusView[4]);

                MethodInfo onValidate = typeof(PrototypeHud).GetMethod(
                    "OnValidate",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(onValidate, Is.Not.Null);
                onValidate.Invoke(hud, null);

                FieldInfo fighterViews = typeof(PrototypeHud).GetField(
                    "_fighterViews",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(fighterViews, Is.Not.Null);
                Assert.That(
                    (CombatantStatusView[])fighterViews.GetValue(hud),
                    Has.Length.EqualTo(PrototypeHud.MaximumFighterViewCount));
            }
            finally
            {
                Object.DestroyImmediate(hudObject);
            }
        }

        private static float ReadCombatImpulse(StrategyFollowCamera camera)
        {
            FieldInfo field = typeof(StrategyFollowCamera).GetField(
                "_combatImpulse",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (float)field.GetValue(camera);
        }

        private static void SetPrivateField(
            object target,
            string fieldName,
            object value)
        {
            FieldInfo field = target.GetType().GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            field.SetValue(target, value);
        }
    }
}
