using System.Reflection;
using NUnit.Framework;
using SessionRpg.Application;
using SessionRpg.Unity.Camera;
using UnityEngine;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class PveLevelingCameraPlayModeTests
    {
        private GameObject _cameraObject;
        private StrategyFollowCamera _camera;

        [SetUp]
        public void SetUp()
        {
            _cameraObject = new GameObject("PveLevelingCameraContract");
            _camera = _cameraObject.AddComponent<StrategyFollowCamera>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_cameraObject);
        }

        [Test]
        public void PveLeveling_UsesItsOwnObliqueComposition()
        {
            _camera.SetGameplayComposition(GameModeId.PveLeveling);

            Assert.That(_camera.Yaw, Is.EqualTo(70f));
            Assert.That(_camera.Pitch, Is.EqualTo(20f));
            Assert.That(_camera.Distance, Is.EqualTo(18f));
            Assert.That(_camera.ForwardFocusOffset, Is.EqualTo(6f));
            Assert.That(_camera.RightFocusOffset, Is.EqualTo(2.5f));
            Assert.That(_camera.FieldOfView, Is.EqualTo(58f));
            Assert.That(_camera.OrbitCenterYaw, Is.EqualTo(70f));
            Assert.That(_camera.MaximumOrbitOffset, Is.EqualTo(180f));
            Assert.That(Projection.farClipPlane, Is.EqualTo(400f));
            Assert.That(Projection.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
        }

        [Test]
        public void ReturningToTraining_RestoresItsExistingCamera()
        {
            _camera.SetGameplayComposition(GameModeId.PveLeveling);
            _camera.SetGameplayComposition(GameModeId.Leveling);

            Assert.That(_camera.Yaw, Is.EqualTo(90f));
            Assert.That(_camera.Pitch, Is.EqualTo(56f));
            Assert.That(_camera.Distance, Is.EqualTo(17f));
            Assert.That(_camera.ForwardFocusOffset, Is.EqualTo(4.1f));
            Assert.That(_camera.RightFocusOffset, Is.EqualTo(1.15f));
            Assert.That(_camera.FieldOfView, Is.EqualTo(45f));
        }

        [TestCase(16f / 9f)]
        [TestCase(20f / 9f)]
        [TestCase(2.4f)]
        public void PveLeveling_LowerPerspectiveRetainsActorScaleAndRevealsBackground(float aspect)
        {
            var target = new GameObject("CameraCompositionTarget");
            try
            {
                UnityEngine.Camera projection = _cameraObject.GetComponent<UnityEngine.Camera>();
                projection.aspect = aspect;
                _camera.SetGameplayComposition(GameModeId.PveLeveling);
                _camera.SetFollowTarget(target.transform);
                Vector3 feet = projection.WorldToViewportPoint(Vector3.zero);
                Vector3 head = projection.WorldToViewportPoint(Vector3.up * 1.8f);
                Assert.That(feet.x, Is.InRange(0.38f, 0.44f));
                Assert.That(feet.y, Is.InRange(0.27f, 0.32f));
                Assert.That((head.y - feet.y) * 1080f, Is.InRange(110f, 150f),
                    "The landscape lens must produce a materially larger hero without shrinking the HUD.");
                Assert.That(_camera.transform.position.y, Is.InRange(7f, 7.1f),
                    "The fixed camera remains above the flat playable floor; tall occluders need separate review.");

                // Match both the eastward authored camp entry and the camera's
                // forward direction. Check the complete health/cast billboard,
                // not only its 2.98 m anchor beside the top HUD.
                AssertTargetAndBillboard(projection, Vector3.right * 14f);
                AssertTargetAndBillboard(projection, _camera.PlanarForward * 14f);

                // Existing camp-one geometry relative to entry (-49, 12): a
                // foreground side facade and distant tower must form two layers.
                Vector3 facade = projection.WorldToViewportPoint(new Vector3(6.2f, 6.2f, 11.8f));
                Vector3 towerBase = projection.WorldToViewportPoint(new Vector3(33.5f, 0.1f, -7.5f));
                Vector3 towerTop = projection.WorldToViewportPoint(new Vector3(33.5f, 11.25f, -7.5f));
                Assert.That(facade.x, Is.InRange(0.18f, 0.3f));
                Assert.That(facade.y, Is.InRange(0.75f, 0.82f));
                Assert.That(towerTop.y, Is.InRange(0.90f, 0.96f));
                Assert.That(towerBase.y, Is.InRange(0.63f, 0.69f));
                float topHudRight = 0.5f + 350f / (1080f * aspect);
                Assert.That(towerBase.x, Is.GreaterThan(topHudRight + 0.005f),
                    "The watchtower belongs beside the centered top HUD, not hidden behind it.");
                Assert.That(towerTop.z, Is.GreaterThan(facade.z + 10f));

                Vector3 horizon = projection.WorldToViewportPoint(projection.transform.position +
                    _camera.PlanarForward * 300f);
                Assert.That(horizon.y, Is.InRange(0.80f, 0.85f),
                    "A real sky band and distant vista replace the old ground-only frame.");
                Assert.That(horizon.z, Is.LessThan(projection.farClipPlane));
            }
            finally { Object.DestroyImmediate(target); }
        }

        [TestCase(0f, 0f)]
        [TestCase(90f, 90f)]
        [TestCase(180f, 180f)]
        [TestCase(270f, 270f)]
        [TestCase(360f, 0f)]
        public void PveLeveling_AllCardinalApproachesCanBeViewedWithoutChangingZoom(
            float requested,
            float expected)
        {
            _camera.SetGameplayComposition(GameModeId.PveLeveling);
            _camera.SetHeading(requested);

            Assert.That(_camera.Yaw, Is.EqualTo(expected).Within(0.001f));
            Assert.That(_camera.Pitch, Is.EqualTo(20f));
            Assert.That(_camera.Distance, Is.EqualTo(18f));
            Assert.That(_camera.FieldOfView, Is.EqualTo(58f));
        }

        [TestCase(GameModeId.Arena)]
        [TestCase(GameModeId.Boss)]
        [TestCase(GameModeId.Dungeon)]
        [TestCase(GameModeId.Leveling)]
        [TestCase(GameModeId.HeroSiege)]
        public void LeavingPve_RestoresExactEntryFarPlaneAndClearModeWithoutChangingBackground(GameModeId mode)
        {
            Projection.farClipPlane = 173f;
            Projection.nearClipPlane = 0.31f;
            Projection.clearFlags = CameraClearFlags.Depth;
            Color background = new Color(0.12f, 0.24f, 0.36f, 1f);
            Projection.backgroundColor = background;
            _camera.SetGameplayComposition(GameModeId.PveLeveling);
            Assert.That(Projection.farClipPlane, Is.EqualTo(400f));
            Assert.That(Projection.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
            Assert.That(Projection.backgroundColor, Is.EqualTo(background));
            _camera.SetGameplayComposition(mode);
            Assert.That(Projection.farClipPlane, Is.EqualTo(173f));
            Assert.That(Projection.nearClipPlane, Is.EqualTo(0.31f));
            Assert.That(Projection.clearFlags, Is.EqualTo(CameraClearFlags.Depth));
            Assert.That(Projection.backgroundColor, Is.EqualTo(background));
            Assert.That(_camera.Pitch, Is.GreaterThanOrEqualTo(35f));
            Assert.That(Projection.fieldOfView, Is.EqualTo(_camera.FieldOfView));
        }

        [Test]
        public void RepeatedPveEntry_DoesNotCaptureItsOwnOverrideAsTheBaseline()
        {
            Projection.farClipPlane = 100f;
            Projection.clearFlags = CameraClearFlags.SolidColor;
            _camera.SetGameplayComposition(GameModeId.PveLeveling);
            _camera.SetGameplayComposition(GameModeId.PveLeveling);
            _camera.SetGameplayComposition(GameModeId.Arena);
            Assert.That(Projection.farClipPlane, Is.EqualTo(100f));
            Assert.That(Projection.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(_camera.Pitch, Is.EqualTo(40f));
            Assert.That(Projection.fieldOfView, Is.EqualTo(46f));
        }

        [Test]
        public void DisableRestoreAndReenable_ReapplyOnlyTheCurrentPvePresentation()
        {
            Projection.farClipPlane = 137f;
            Projection.clearFlags = CameraClearFlags.SolidColor;
            _camera.SetGameplayComposition(GameModeId.PveLeveling);
            _camera.enabled = false;
            Assert.That(Projection.farClipPlane, Is.EqualTo(137f));
            Assert.That(Projection.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            _camera.enabled = true;
            Assert.That(Projection.farClipPlane, Is.EqualTo(400f));
            Assert.That(Projection.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
            Assert.That(_camera.Pitch, Is.EqualTo(20f));
            _camera.SetGameplayComposition(GameModeId.Leveling);
            _camera.enabled = false;
            _camera.enabled = true;
            Assert.That(Projection.farClipPlane, Is.EqualTo(137f));
            Assert.That(Projection.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
            Assert.That(_camera.Pitch, Is.EqualTo(56f));
        }

        [Test]
        public void DestroyingPveRig_RestoresTheRemainingCamera()
        {
            UnityEngine.Camera projection = Projection;
            projection.farClipPlane = 147f;
            projection.clearFlags = CameraClearFlags.SolidColor;
            _camera.SetGameplayComposition(GameModeId.PveLeveling);
            Object.DestroyImmediate(_camera);
            Assert.That(projection.farClipPlane, Is.EqualTo(147f));
            Assert.That(projection.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
        }

        [Test]
        public void PublicFixedComposition_StillClampsAt35AndRestoresTheNonPveLens()
        {
            Projection.farClipPlane = 100f;
            Projection.clearFlags = CameraClearFlags.SolidColor;
            _camera.SetGameplayComposition(GameModeId.PveLeveling);
            _camera.SetFixedComposition(450f, 20f, 0f, -3f, 100f, 90f);
            Assert.That(_camera.Yaw, Is.EqualTo(90f));
            Assert.That(_camera.Pitch, Is.EqualTo(35f));
            Assert.That(_camera.Distance, Is.EqualTo(1f));
            Assert.That(_camera.ForwardFocusOffset, Is.Zero);
            Assert.That(_camera.RightFocusOffset, Is.EqualTo(10f));
            Assert.That(_camera.FieldOfView, Is.EqualTo(70f));
            Assert.That(Projection.farClipPlane, Is.EqualTo(100f));
            Assert.That(Projection.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
        }

        [TestCase(70f)]
        [TestCase(135f)]
        public void OrdinaryAndYawOnlyCapture_PreserveTheActualPveComposition(float yaw)
        {
            _camera.SetGameplayComposition(GameModeId.PveLeveling);
            ApplyCaptureValues(yaw, false);
            Assert.That(_camera.Yaw, Is.EqualTo(yaw));
            Assert.That(_camera.Pitch, Is.EqualTo(20f));
            Assert.That(_camera.Distance, Is.EqualTo(18f));
            Assert.That(_camera.FieldOfView, Is.EqualTo(58f));
            Assert.That(Projection.farClipPlane, Is.EqualTo(400f));
            Assert.That(Projection.clearFlags, Is.EqualTo(CameraClearFlags.Skybox));
        }

        [Test]
        public void ExplicitCaptureProjectionOverride_KeepsLegacyLookDevelopmentClamps()
        {
            Projection.farClipPlane = 100f;
            Projection.clearFlags = CameraClearFlags.SolidColor;
            _camera.SetGameplayComposition(GameModeId.PveLeveling);
            ApplyCaptureValues(90f, true);
            Assert.That(_camera.Yaw, Is.EqualTo(90f));
            Assert.That(_camera.Pitch, Is.EqualTo(35f));
            Assert.That(Projection.farClipPlane, Is.EqualTo(100f));
            Assert.That(Projection.clearFlags, Is.EqualTo(CameraClearFlags.SolidColor));
        }

        private UnityEngine.Camera Projection => _cameraObject.GetComponent<UnityEngine.Camera>();

        private void ApplyCaptureValues(float yaw, bool projectionOverride)
        {
            System.Type capture = typeof(StrategyFollowCamera).Assembly.GetType(
                "SessionRpg.Unity.Gameplay.SessionRpgReferenceCapture");
            Assert.That(capture, Is.Not.Null);
            MethodInfo apply = capture.GetMethod("ApplyCaptureCameraValues", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(apply, Is.Not.Null);
            apply.Invoke(null, new object[] { _camera, yaw, 20f, 18f, 6f, 2.5f, 58f, projectionOverride });
        }

        private void AssertTargetAndBillboard(UnityEngine.Camera projection, Vector3 target)
        {
            Vector3 feet = projection.WorldToViewportPoint(target);
            Vector3 head = projection.WorldToViewportPoint(target + Vector3.up * 1.8f);
            Assert.That(feet.x, Is.InRange(0.35f, 0.62f));
            Assert.That(feet.y, Is.InRange(0.52f, 0.60f));
            Assert.That((head.y - feet.y) * 1080f, Is.GreaterThan(60f));
            Quaternion billboardRotation = projection.transform.rotation;
            Vector3 anchor = target + Vector3.up * 2.98f;
            for (int side = -1; side <= 1; side += 2)
            {
                foreach (float height in new[] { -0.14f, 0.18f })
                {
                    Vector3 corner = anchor + billboardRotation * new Vector3(side * 0.61f, height, 0f);
                    Vector3 viewport = projection.WorldToViewportPoint(corner);
                    Assert.That(viewport.x, Is.InRange(0.32f, 0.64f));
                    Assert.That(viewport.y, Is.InRange(0.60f, 1f - 258f / 1080f - 0.02f),
                        "The entire health/cast billboard must stay below the lowest centered notice, with padding.");
                    Assert.That(viewport.z, Is.GreaterThan(projection.nearClipPlane));
                }
            }
        }
    }
}
