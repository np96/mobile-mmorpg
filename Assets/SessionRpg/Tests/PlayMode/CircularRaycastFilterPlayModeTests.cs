using NUnit.Framework;
using SessionRpg.Unity.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class CircularRaycastFilterPlayModeTests
    {
        private const float Diameter = 204f;
        private const float Radius = Diameter * 0.5f;

        private GameObject _canvasObject;
        private RectTransform _surface;
        private Image _graphic;

        [SetUp]
        public void SetUp()
        {
            _canvasObject = new GameObject(
                "CircularRaycastFilterCanvas",
                typeof(RectTransform),
                typeof(Canvas));
            Canvas canvas = _canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            GameObject surfaceObject = new GameObject(
                "CircularSurface",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image),
                typeof(CircularRaycastFilter));
            surfaceObject.transform.SetParent(_canvasObject.transform, false);
            _surface = surfaceObject.GetComponent<RectTransform>();
            _surface.anchorMin = new Vector2(0.5f, 0.5f);
            _surface.anchorMax = new Vector2(0.5f, 0.5f);
            _surface.pivot = new Vector2(0.5f, 0.5f);
            _surface.anchoredPosition = Vector2.zero;
            _surface.sizeDelta = Vector2.one * Diameter;
            _graphic = surfaceObject.GetComponent<Image>();
            _graphic.color = new Color(1f, 1f, 1f, 0.001f);
            _graphic.raycastTarget = true;
            Canvas.ForceUpdateCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_canvasObject);
        }

        [Test]
        public void CenterAndCircularEdgeHit_ButTransparentSquareCornersMiss()
        {
            Assert.That(IsValid(Vector2.zero), Is.True, "center");
            Assert.That(
                IsValid(new Vector2(Radius - 0.25f, 0f)),
                Is.True,
                "right circular edge");
            Assert.That(
                IsValid(new Vector2(0f, -Radius + 0.25f)),
                Is.True,
                "bottom circular edge");

            Vector2 insideRectCorner = new Vector2(
                Radius - 0.25f,
                Radius - 0.25f);
            Assert.That(
                _surface.rect.Contains(insideRectCorner),
                Is.True,
                "The regression point must remain inside the rectangular Graphic.");
            Assert.That(
                IsValid(insideRectCorner),
                Is.False,
                "transparent top-right corner");
            Assert.That(
                IsValid(-insideRectCorner),
                Is.False,
                "transparent bottom-left corner");
            Assert.That(
                IsValid(new Vector2(Radius + 0.25f, 0f)),
                Is.False,
                "outside circular edge");
        }

        [Test]
        public void EnlargedGestureDisc_ExpandsTheRealHitAreaWithoutSquareCorners()
        {
            const float originalGestureDiameter = 232f;
            const float controlScale = 1.3f;
            _surface.sizeDelta = Vector2.one * originalGestureDiameter;
            _surface.localScale = new Vector3(controlScale, controlScale, 1f);
            Canvas.ForceUpdateCanvases();

            Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(
                null,
                _surface.TransformPoint(Vector2.zero));
            float originalRadius = originalGestureDiameter * 0.5f;
            float enlargedRadius = originalRadius * controlScale;
            Assert.That(
                _graphic.Raycast(screenCenter + new Vector2(originalRadius + 12f, 0f), null),
                Is.True,
                "The enlarged visible annulus must receive touches, not just the old-size disc.");
            Assert.That(
                _graphic.Raycast(screenCenter + new Vector2(enlargedRadius - 0.25f, 0f), null),
                Is.True,
                "Enlarged circular edge.");
            Assert.That(
                _graphic.Raycast(screenCenter + new Vector2(enlargedRadius + 0.25f, 0f), null),
                Is.False,
                "Outside enlarged circular edge.");
            Assert.That(
                _graphic.Raycast(screenCenter + Vector2.one * (enlargedRadius - 0.25f), null),
                Is.False,
                "The enlarged transparent square corner must not steal world input.");
        }

        private bool IsValid(Vector2 localPoint)
        {
            Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(
                null,
                _surface.TransformPoint(localPoint));
            return _graphic.Raycast(screenPoint, null);
        }
    }
}
