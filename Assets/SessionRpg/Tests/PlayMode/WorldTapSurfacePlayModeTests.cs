using NUnit.Framework;
using SessionRpg.Unity.Input;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class WorldTapSurfacePlayModeTests
    {
        private GameObject _eventSystemObject;
        private GameObject _surfaceObject;
        private EventSystem _eventSystem;
        private WorldTapSurface _surface;

        [SetUp]
        public void SetUp()
        {
            _eventSystemObject = new GameObject(
                "World Surface Test Event System",
                typeof(EventSystem));
            _eventSystem = _eventSystemObject.GetComponent<EventSystem>();
            _surfaceObject = new GameObject(
                "World Surface Under Test",
                typeof(RectTransform),
                typeof(WorldTapSurface));
            _surface = _surfaceObject.GetComponent<WorldTapSurface>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_surfaceObject);
            Object.DestroyImmediate(_eventSystemObject);
        }

        [Test]
        public void ShortOwnedPress_EmitsTapWithoutOrbit()
        {
            int tapCount = 0;
            int orbitCount = 0;
            Vector2 tappedPosition = default;
            _surface.Tapped += position =>
            {
                tapCount++;
                tappedPosition = position;
            };
            _surface.OrbitDragged += _ => orbitCount++;

            PointerEventData pointer = Pointer(21, new Vector2(120f, 80f));
            _surface.OnPointerDown(pointer);
            pointer.position += new Vector2(8f, 4f);
            _surface.OnDrag(pointer);
            _surface.OnPointerUp(pointer);

            Assert.That(tapCount, Is.EqualTo(1));
            Assert.That(orbitCount, Is.Zero);
            Assert.That(tappedPosition, Is.EqualTo(pointer.position));
            Assert.That(_surface.IsActive, Is.False);
            Assert.That(_surface.IsOrbiting, Is.False);
        }

        [Test]
        public void HorizontalDrag_OrbitsWithOwningPointerAndNeverBecomesTap()
        {
            int tapCount = 0;
            int orbitCount = 0;
            WorldOrbitGesture lastOrbit = default;
            _surface.Tapped += _ => tapCount++;
            _surface.OrbitDragged += gesture =>
            {
                orbitCount++;
                lastOrbit = gesture;
            };

            PointerEventData owner = Pointer(31, new Vector2(100f, 100f));
            PointerEventData stranger = Pointer(32, new Vector2(200f, 100f));
            _surface.OnPointerDown(owner);
            _surface.OnDrag(stranger);
            _surface.OnPointerUp(stranger);

            Assert.That(_surface.ActivePointerId, Is.EqualTo(31));
            Assert.That(orbitCount, Is.Zero);

            owner.position = new Vector2(150f, 100f);
            _surface.OnDrag(owner);
            Assert.That(_surface.IsOrbiting, Is.True);
            Assert.That(orbitCount, Is.EqualTo(1));
            Assert.That(lastOrbit.PointerId, Is.EqualTo(31));
            Assert.That(lastOrbit.ScreenDelta, Is.EqualTo(new Vector2(50f, 0f)));

            owner.position = new Vector2(170f, 100f);
            _surface.OnDrag(owner);
            Assert.That(orbitCount, Is.EqualTo(2));
            Assert.That(lastOrbit.ScreenDelta, Is.EqualTo(new Vector2(20f, 0f)));

            _surface.OnPointerUp(owner);
            Assert.That(tapCount, Is.Zero);
            Assert.That(_surface.IsActive, Is.False);
        }

        private PointerEventData Pointer(int pointerId, Vector2 position)
        {
            return new PointerEventData(_eventSystem)
            {
                pointerId = pointerId,
                button = PointerEventData.InputButton.Left,
                position = position,
            };
        }
    }
}
