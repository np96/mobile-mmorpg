using System.Collections;
using NUnit.Framework;
using SessionRpg.Core;
using SessionRpg.Unity.Input;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace SessionRpg.Tests.PlayMode
{
    public sealed class AbilityButtonFlickPlayModeTests
    {
        private readonly AbilityGesture[] _gestures = new AbilityGesture[16];
        private readonly AbilityFlickSelection[] _selections = new AbilityFlickSelection[16];
        private GameObject _buttonObject;
        private GameObject _eventSystemObject;
        private GameObject _secondaryButtonObject;
        private EventSystem _eventSystem;
        private AbilityButton _button;
        private int _gestureCount;
        private int _selectionCount;

        [SetUp]
        public void SetUp()
        {
            _eventSystemObject = new GameObject("AbilityButtonTestEventSystem");
            _eventSystem = _eventSystemObject.AddComponent<EventSystem>();
            _buttonObject = new GameObject(
                "AbilityButtonUnderTest",
                typeof(RectTransform),
                typeof(AbilityButton));
            _button = _buttonObject.GetComponent<AbilityButton>();
            _button.BindAbility(AbilityId.Fireball);
            _button.ConfigureFlickThreshold(40f, 40f, false);
            _button.ConfigureMaximumFlickDuration(1f);
            _button.GestureChanged += RecordGesture;
            _button.SelectionChanged += RecordSelection;
            _gestureCount = 0;
            _selectionCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_secondaryButtonObject);
            Object.DestroyImmediate(_buttonObject);
            Object.DestroyImmediate(_eventSystemObject);
        }

        [Test]
        public void Tap_EmitsPrimaryAbilityWithOriginatingPointerId()
        {
            PointerEventData pointer = Pointer(17, new Vector2(120f, 200f));

            _button.OnPointerDown(pointer);
            _button.OnPointerUp(pointer);

            Assert.That(_gestureCount, Is.EqualTo(2));
            AssertGesture(0, AbilityGesturePhase.Pressed, AbilityId.Fireball, 17);
            AssertGesture(1, AbilityGesturePhase.Tapped, AbilityId.Fireball, 17);
            Assert.That(_button.IsPressed, Is.False);
            Assert.That(_button.Selection.IsPressed, Is.False);
        }

        [Test]
        public void DesktopShortcuts_ExposeAllTenSlotsUsingOneThroughNineAndZero()
        {
            Key[] expected =
            {
                Key.Digit1,
                Key.Digit2,
                Key.Digit3,
                Key.Digit4,
                Key.Digit5,
                Key.Digit6,
                Key.Digit7,
                Key.Digit8,
                Key.Digit9,
                Key.Digit0,
            };

            Assert.That(expected, Has.Length.EqualTo(AbilityCatalog.AbilitySlotCount));
            for (int slot = 0; slot < expected.Length; slot++)
            {
                Assert.That(
                    CombatInputSource.GetDesktopAbilityKey(slot),
                    Is.EqualTo(expected[slot]));
            }

            Assert.That(
                () => CombatInputSource.GetDesktopAbilityKey(expected.Length),
                Throws.TypeOf<System.ArgumentOutOfRangeException>());
        }

        [Test]
        public void NonPrimaryPointer_DoesNotBeginOrCast()
        {
            PointerEventData pointer = Pointer(18, new Vector2(120f, 200f));
            pointer.button = PointerEventData.InputButton.Right;

            _button.OnPointerDown(pointer);
            _button.OnPointerUp(pointer);

            Assert.That(_gestureCount, Is.Zero);
            Assert.That(_button.IsPressed, Is.False);
        }

        [Test]
        public void Flick_RequiresConfiguredDistanceThreshold()
        {
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Right,
                AbilityId.FireBlast);
            PointerEventData pointer = Pointer(23, Vector2.zero);
            _button.OnPointerDown(pointer);

            Drag(pointer, new Vector2(39f, 0f));
            Assert.That(_button.IsSelectionLocked, Is.False);
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.Fireball));

            Drag(pointer, new Vector2(40f, 0f));
            Assert.That(_button.IsSelectionLocked, Is.True);
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.FireBlast));

            _button.OnPointerUp(pointer);
            AssertGesture(1, AbilityGesturePhase.Flicked, AbilityId.FireBlast, 23);
        }

        [Test]
        public void EnlargedGestureControl_KeepsScreenPixelFlickThreshold()
        {
            _buttonObject.transform.localScale = new Vector3(1.3f, 1.3f, 1f);
            ((RectTransform)_buttonObject.transform).sizeDelta = Vector2.one * 232f;
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Right,
                AbilityId.FireBlast);
            PointerEventData pointer = Pointer(25, new Vector2(240f, 180f));
            Vector2 origin = pointer.position;
            _button.OnPointerDown(pointer);

            Drag(pointer, origin + new Vector2(39f, 0f));
            Assert.That(_button.IsSelectionLocked, Is.False);
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.Fireball));

            Drag(pointer, origin + new Vector2(40f, 0f));
            Assert.That(_button.IsSelectionLocked, Is.True);
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.FireBlast));
            _button.OnPointerUp(pointer);

            AssertGesture(1, AbilityGesturePhase.Flicked, AbilityId.FireBlast, 25);
            Assert.That(_gestures[1].Displacement, Is.EqualTo(new Vector2(40f, 0f)),
                "Larger artwork must not increase thumb travel or rescale input displacement.");
        }

        [Test]
        public void HighDpiScaling_IsCappedForReachableThumbFlicks()
        {
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Up,
                AbilityId.Renew);
            _button.ConfigureFlickThreshold(1000f, 32f, true);
            PointerEventData pointer = Pointer(24, Vector2.zero);

            _button.OnPointerDown(pointer);
            Drag(pointer, new Vector2(0f, 51f));
            Assert.That(_button.IsSelectionLocked, Is.False);

            Drag(pointer, new Vector2(0f, 52f));
            Assert.That(_button.IsSelectionLocked, Is.True);
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.Renew));

            _button.OnPointerUp(pointer);
            AssertGesture(1, AbilityGesturePhase.Flicked, AbilityId.Renew, 24);
        }

        [UnityTest]
        public IEnumerator SlowHoldThenDrag_CommitsBoundSector()
        {
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Right,
                AbilityId.FireBlast);
            _button.ConfigureMaximumFlickDuration(0.05f);
            PointerEventData pointer = Pointer(27, Vector2.zero);
            _button.OnPointerDown(pointer);

            yield return new WaitForSecondsRealtime(0.08f);
            Drag(pointer, new Vector2(80f, 0f));
            _button.OnPointerUp(pointer);

            AssertGesture(1, AbilityGesturePhase.Flicked, AbilityId.FireBlast, 27);
            AssertNoCommittedGesture(AbilityGesturePhase.Tapped);
            AssertNoCommittedGesture(AbilityGesturePhase.Canceled);
        }

        [TestCase(0f, 41f, AbilityFlickDirection.Up, AbilityId.LivingBomb)]
        [TestCase(0f, -41f, AbilityFlickDirection.Down, AbilityId.DragonBreath)]
        [TestCase(-41f, 0f, AbilityFlickDirection.Left, AbilityId.BlastWave)]
        [TestCase(41f, 0f, AbilityFlickDirection.Right, AbilityId.FireBlast)]
        public void Flick_SelectsBoundCardinalAbility(
            float deltaX,
            float deltaY,
            AbilityFlickDirection direction,
            AbilityId alternate)
        {
            _button.BindDirectionalAbility(direction, alternate);
            PointerEventData pointer = Pointer(29, new Vector2(300f, 300f));

            _button.OnPointerDown(pointer);
            Drag(pointer, pointer.position + new Vector2(deltaX, deltaY));

            Assert.That(_button.Selection.Direction, Is.EqualTo(direction));
            Assert.That(_button.Selection.AbilityId, Is.EqualTo(alternate));
            Assert.That(_button.Selection.IsLocked, Is.True);
            Assert.That(_button.Selection.HasDirectionalBinding, Is.True);
            Assert.That(_selectionCount, Is.GreaterThanOrEqualTo(2));

            _button.OnPointerUp(pointer);
            AssertGesture(1, AbilityGesturePhase.Flicked, alternate, 29);
        }

        [Test]
        public void DiagonalCorrection_TracksCurrentCardinalSectorBeforeRelease()
        {
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Right,
                AbilityId.FireBlast);
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Up,
                AbilityId.LivingBomb);
            PointerEventData pointer = Pointer(31, Vector2.zero);

            _button.OnPointerDown(pointer);
            Drag(pointer, new Vector2(45f, 39f));
            Assert.That(_button.SelectedDirection, Is.EqualTo(AbilityFlickDirection.Right));
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.FireBlast));

            Drag(pointer, new Vector2(39f, 45f));

            Assert.That(_button.SelectedDirection, Is.EqualTo(AbilityFlickDirection.Up));
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.LivingBomb));

            _button.OnPointerUp(pointer);
            AssertGesture(1, AbilityGesturePhase.Flicked, AbilityId.LivingBomb, 31);
        }

        [Test]
        public void UnboundSector_CanCorrectIntoBoundSector()
        {
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Up,
                AbilityId.LivingBomb);
            PointerEventData pointer = Pointer(37, Vector2.zero);

            _button.OnPointerDown(pointer);
            Drag(pointer, new Vector2(45f, 0f));

            Assert.That(_button.SelectedDirection, Is.EqualTo(AbilityFlickDirection.Right));
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.None));
            Assert.That(_button.Selection.HasDirectionalBinding, Is.False);
            Assert.That(_button.Selection.IsCanceled, Is.False);

            Drag(pointer, new Vector2(0f, 90f));

            Assert.That(_button.SelectedDirection, Is.EqualTo(AbilityFlickDirection.Up));
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.LivingBomb));
            Assert.That(_button.Selection.HasDirectionalBinding, Is.True);
            Assert.That(_button.Selection.IsCanceled, Is.False);

            _button.OnPointerUp(pointer);
            AssertGesture(1, AbilityGesturePhase.Flicked, AbilityId.LivingBomb, 37);
            AssertNoCommittedGesture(AbilityGesturePhase.Tapped);
            AssertNoCommittedGesture(AbilityGesturePhase.Canceled);
        }

        [Test]
        public void ForeignPointer_CannotChangeOrCommitActiveGesture()
        {
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Right,
                AbilityId.FireBlast);
            PointerEventData owner = Pointer(41, Vector2.zero);
            PointerEventData foreign = Pointer(42, new Vector2(80f, 0f));

            _button.OnPointerDown(owner);
            _button.OnDrag(foreign);
            _button.OnPointerUp(foreign);

            Assert.That(_button.ActivePointerId, Is.EqualTo(41));
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.Fireball));
            Assert.That(_gestureCount, Is.EqualTo(1));

            Drag(owner, new Vector2(80f, 0f));
            _button.OnPointerUp(owner);
            AssertGesture(1, AbilityGesturePhase.Flicked, AbilityId.FireBlast, 41);
        }

        [Test]
        public void Cancel_UsesLockedAlternateAndClearsSelection()
        {
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Up,
                AbilityId.LivingBomb);
            PointerEventData pointer = Pointer(47, Vector2.zero);

            _button.OnPointerDown(pointer);
            Drag(pointer, new Vector2(0f, 50f));
            _button.CancelActivePointer();

            AssertGesture(1, AbilityGesturePhase.Canceled, AbilityId.LivingBomb, 47);
            Assert.That(_button.IsPressed, Is.False);
            Assert.That(_button.Selection.IsPressed, Is.False);
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.Fireball));
        }

        [Test]
        public void SwipeWithoutDirectionalBindings_CancelsAndNeverCastsPrimary()
        {
            PointerEventData pointer = Pointer(53, Vector2.zero);

            _button.OnPointerDown(pointer);
            Drag(pointer, new Vector2(120f, 35f));
            _button.OnPointerUp(pointer);

            Assert.That(_gestureCount, Is.EqualTo(2));
            AssertGesture(0, AbilityGesturePhase.Pressed, AbilityId.Fireball, 53);
            AssertGesture(1, AbilityGesturePhase.Canceled, AbilityId.None, 53);
            AssertNoCommittedGesture(AbilityGesturePhase.Tapped);
            AssertNoCommittedGesture(AbilityGesturePhase.Flicked);
        }

        [UnityTest]
        public IEnumerator BoundSector_HeldIndefinitely_CommitsOnRelease()
        {
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Right,
                AbilityId.FireBlast);
            _button.ConfigureMaximumFlickDuration(0.05f);
            PointerEventData pointer = Pointer(59, Vector2.zero);

            _button.OnPointerDown(pointer);
            Drag(pointer, new Vector2(80f, 0f));
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.FireBlast));

            yield return new WaitForSecondsRealtime(0.08f);
            _button.OnPointerUp(pointer);

            AssertGesture(1, AbilityGesturePhase.Flicked, AbilityId.FireBlast, 59);
            AssertNoCommittedGesture(AbilityGesturePhase.Tapped);
            AssertNoCommittedGesture(AbilityGesturePhase.Canceled);
        }

        [Test]
        public void Recenter_ClearsDirectionalSelectionAndReleaseCastsPrimary()
        {
            _button.BindDirectionalAbility(
                AbilityFlickDirection.Right,
                AbilityId.FireBlast);
            PointerEventData pointer = Pointer(60, Vector2.zero);

            _button.OnPointerDown(pointer);
            Drag(pointer, new Vector2(80f, 0f));
            Assert.That(_button.SelectedDirection, Is.EqualTo(AbilityFlickDirection.Right));
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.FireBlast));
            Assert.That(_button.IsSelectionLocked, Is.True);

            Drag(pointer, new Vector2(10f, 0f));

            Assert.That(_button.SelectedDirection, Is.EqualTo(AbilityFlickDirection.None));
            Assert.That(_button.SelectedAbilityId, Is.EqualTo(AbilityId.Fireball));
            Assert.That(_button.IsSelectionLocked, Is.False);
            Assert.That(_button.Selection.HasDirectionalBinding, Is.False);
            Assert.That(_button.Selection.IsCanceled, Is.False);

            _button.OnPointerUp(pointer);
            AssertGesture(1, AbilityGesturePhase.Tapped, AbilityId.Fireball, 60);
            AssertNoCommittedGesture(AbilityGesturePhase.Flicked);
            AssertNoCommittedGesture(AbilityGesturePhase.Canceled);
        }

        [Test]
        public void LargeReleaseWithoutDragEvent_IsClassifiedAndCanceled()
        {
            PointerEventData pointer = Pointer(61, Vector2.zero);
            _button.OnPointerDown(pointer);
            pointer.position = new Vector2(120f, 0f);

            _button.OnPointerUp(pointer);

            AssertGesture(1, AbilityGesturePhase.Canceled, AbilityId.None, 61);
            Assert.That(_gestures[1].Displacement.x, Is.EqualTo(120f));
            AssertNoCommittedGesture(AbilityGesturePhase.Tapped);
        }

        [UnityTest]
        public IEnumerator LongPressWithSubthresholdJitter_RemainsAPrimaryTap()
        {
            _button.ConfigureMaximumFlickDuration(0.01f);
            PointerEventData pointer = Pointer(67, new Vector2(100f, 100f));
            _button.OnPointerDown(pointer);

            yield return new WaitForSecondsRealtime(0.03f);
            pointer.position += new Vector2(10f, 0f);
            _button.OnPointerUp(pointer);

            AssertGesture(1, AbilityGesturePhase.Tapped, AbilityId.Fireball, 67);
        }

        [Test]
        public void TwoPointers_CanCommitDifferentButtonsIndependently()
        {
            _secondaryButtonObject = new GameObject(
                "SecondAbilityButton",
                typeof(RectTransform),
                typeof(AbilityButton));
            AbilityButton secondButton = _secondaryButtonObject.GetComponent<AbilityButton>();
            AbilityGesture secondGesture = default;
            int secondGestureCount = 0;
            secondButton.BindAbility(AbilityId.Frostbolt);
            secondButton.GestureChanged += gesture =>
            {
                secondGesture = gesture;
                secondGestureCount++;
            };

            PointerEventData firstPointer = Pointer(71, Vector2.zero);
            PointerEventData secondPointer = Pointer(72, new Vector2(200f, 0f));
            _button.OnPointerDown(firstPointer);
            secondButton.OnPointerDown(secondPointer);

            Assert.That(_button.ActivePointerId, Is.EqualTo(71));
            Assert.That(secondButton.ActivePointerId, Is.EqualTo(72));

            secondButton.OnPointerUp(secondPointer);
            _button.OnPointerUp(firstPointer);

            AssertGesture(1, AbilityGesturePhase.Tapped, AbilityId.Fireball, 71);
            Assert.That(secondGestureCount, Is.EqualTo(2));
            Assert.That(secondGesture.Phase, Is.EqualTo(AbilityGesturePhase.Tapped));
            Assert.That(secondGesture.AbilityId, Is.EqualTo(AbilityId.Frostbolt));
            Assert.That(secondGesture.PointerId, Is.EqualTo(72));
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

        private void Drag(PointerEventData pointer, Vector2 position)
        {
            pointer.delta = position - pointer.position;
            pointer.position = position;
            _button.OnDrag(pointer);
        }

        private void RecordGesture(AbilityGesture gesture)
        {
            _gestures[_gestureCount] = gesture;
            _gestureCount++;
        }

        private void RecordSelection(AbilityFlickSelection selection)
        {
            _selections[_selectionCount] = selection;
            _selectionCount++;
        }

        private void AssertGesture(
            int index,
            AbilityGesturePhase phase,
            AbilityId ability,
            int pointerId)
        {
            Assert.That(_gestures[index].Phase, Is.EqualTo(phase));
            Assert.That(_gestures[index].AbilityId, Is.EqualTo(ability));
            Assert.That(_gestures[index].PointerId, Is.EqualTo(pointerId));
        }

        private void AssertNoCommittedGesture(AbilityGesturePhase phase)
        {
            for (int i = 0; i < _gestureCount; i++)
            {
                Assert.That(_gestures[i].Phase, Is.Not.EqualTo(phase));
            }
        }
    }
}
