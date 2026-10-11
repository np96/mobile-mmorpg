using System;
using SessionRpg.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.Serialization;

namespace SessionRpg.Unity.Input
{
    [DisallowMultipleComponent]
    public sealed class CombatInputSource : MonoBehaviour, IHumanInputSource
    {
        private const int GestureQueueCapacity = 32;
        private const int EditorPointerId = -1000;
        private const float GamepadOrbitDeadZone = 0.12f;
        private static readonly AbilityButton[] EmptyButtons = new AbilityButton[0];

        [Header("Touch")]
        [SerializeField] private FloatingJoystick _movementJoystick;
        [FormerlySerializedAs("_lookSurface")]
        [SerializeField] private WorldTapSurface _worldSurface;
        [SerializeField] private AbilityButton[] _abilityButtons = EmptyButtons;

        [Header("Editor and desktop fallback")]
        [SerializeField] private bool _enableDesktopFallback = true;
        [SerializeField] private AbilityId[] _desktopAbilityBindings =
            new AbilityId[AbilityCatalog.AbilitySlotCount];
        [SerializeField] private AbilityId[] _desktopAlternateUpBindings =
            new AbilityId[AbilityCatalog.AbilitySlotCount];
        [SerializeField] private AbilityId[] _desktopAlternateRightBindings =
            new AbilityId[AbilityCatalog.AbilitySlotCount];
        [SerializeField, Range(45f, 360f)] private float _touchOrbitDegreesPerScreen =
            180f;
        [SerializeField, Range(30f, 240f)] private float _desktopOrbitDegreesPerSecond =
            90f;

        private readonly AbilityGesture[] _gestureQueue = new AbilityGesture[GestureQueueCapacity];
        private HumanInputFrame _currentFrame;
        private int _gestureHead;
        private int _gestureCount;
        private int _droppedGestureCount;

        public HumanInputFrame CurrentFrame => _currentFrame;

        public int DroppedGestureCount => _droppedGestureCount;

        public event Action<Vector2> WorldTapped;

        /// <summary>
        /// Presentation-only yaw delta. It never enters the deterministic command
        /// queue or changes a combat outcome.
        /// </summary>
        public event Action<float> CameraOrbitRequested;

        private void OnEnable()
        {
            if (_worldSurface != null)
            {
                _worldSurface.Tapped += OnWorldTapped;
                _worldSurface.OrbitDragged += OnWorldOrbitDragged;
            }

            for (int i = 0; i < _abilityButtons.Length; i++)
            {
                AbilityButton button = _abilityButtons[i];
                if (button != null)
                {
                    button.GestureChanged += EnqueueGesture;
                }
            }
        }

        private void OnDisable()
        {
            if (_worldSurface != null)
            {
                _worldSurface.Tapped -= OnWorldTapped;
                _worldSurface.OrbitDragged -= OnWorldOrbitDragged;
            }

            for (int i = 0; i < _abilityButtons.Length; i++)
            {
                AbilityButton button = _abilityButtons[i];
                if (button != null)
                {
                    button.GestureChanged -= EnqueueGesture;
                }
            }

            ClearTransientInput();
        }

        private void Update()
        {
            Vector2 movement = _movementJoystick != null ? _movementJoystick.Value : Vector2.zero;

            if (_enableDesktopFallback)
            {
                ReadDesktopInput(ref movement);
            }

            _currentFrame = new HumanInputFrame(movement);
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ClearTransientInput();
            }
        }

        private void OnApplicationPause(bool isPaused)
        {
            if (isPaused)
            {
                ClearTransientInput();
            }
        }

        public bool TryDequeueAbilityGesture(out AbilityGesture gesture)
        {
            if (_gestureCount == 0)
            {
                gesture = default;
                return false;
            }

            gesture = _gestureQueue[_gestureHead];
            _gestureHead = (_gestureHead + 1) % GestureQueueCapacity;
            _gestureCount--;
            return true;
        }

        public void ClearTransientInput()
        {
            _movementJoystick?.ClearPointer();
            _worldSurface?.ClearPointer();

            for (int i = 0; i < _abilityButtons.Length; i++)
            {
                _abilityButtons[i]?.CancelActivePointer();
            }

            _currentFrame = default;
            _gestureHead = 0;
            _gestureCount = 0;
        }

        public void SetTouchControls(
            FloatingJoystick movementJoystick,
            WorldTapSurface worldSurface,
            AbilityButton[] abilityButtons)
        {
            bool wasEnabled = isActiveAndEnabled;
            if (wasEnabled)
            {
                OnDisable();
            }

            _movementJoystick = movementJoystick;
            _worldSurface = worldSurface;
            _abilityButtons = abilityButtons ?? EmptyButtons;

            if (wasEnabled)
            {
                OnEnable();
            }
        }

        public void SetDesktopAbilityBindings(
            AbilityId basic,
            AbilityId first,
            AbilityId second,
            AbilityId third,
            AbilityId fourth)
        {
            SetDesktopAbilityBindings(new[] { basic, first, second, third, fourth });
        }

        public void SetDesktopAbilityBindings(params AbilityId[] bindings)
        {
            EnsureDesktopBindingCapacity();
            Array.Clear(_desktopAbilityBindings, 0, _desktopAbilityBindings.Length);
            if (bindings == null)
            {
                return;
            }

            int count = Mathf.Min(bindings.Length, _desktopAbilityBindings.Length);
            Array.Copy(bindings, _desktopAbilityBindings, count);
        }

        private void OnWorldTapped(Vector2 screenPosition)
        {
            WorldTapped?.Invoke(screenPosition);
        }

        private void OnWorldOrbitDragged(WorldOrbitGesture gesture)
        {
            float screenWidth = Mathf.Max(1f, Screen.width);
            float yawDelta = -gesture.ScreenDelta.x / screenWidth *
                _touchOrbitDegreesPerScreen;
            EmitCameraOrbit(yawDelta);
        }

        private void EnqueueGesture(AbilityGesture gesture)
        {
            if (_gestureCount == GestureQueueCapacity)
            {
                _gestureHead = (_gestureHead + 1) % GestureQueueCapacity;
                _gestureCount--;
                _droppedGestureCount++;
            }

            int tail = (_gestureHead + _gestureCount) % GestureQueueCapacity;
            _gestureQueue[tail] = gesture;
            _gestureCount++;
        }

        private void ReadDesktopInput(ref Vector2 movement)
        {
            float orbitAxis = 0f;
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                Vector2 keyboardMovement = Vector2.zero;
                keyboardMovement.x = ReadAxis(keyboard.aKey.isPressed, keyboard.dKey.isPressed);
                keyboardMovement.y = ReadAxis(keyboard.sKey.isPressed, keyboard.wKey.isPressed);
                movement = Vector2.ClampMagnitude(movement + keyboardMovement, 1f);
                orbitAxis += ReadAxis(
                    keyboard.qKey.isPressed,
                    keyboard.eKey.isPressed);

                bool alternateUp = keyboard.leftShiftKey.isPressed ||
                    keyboard.rightShiftKey.isPressed;
                bool alternateRight = keyboard.leftCtrlKey.isPressed ||
                    keyboard.rightCtrlKey.isPressed;
                AbilitySlotDirection direction = alternateRight
                    ? AbilitySlotDirection.Right
                    : alternateUp
                        ? AbilitySlotDirection.Up
                        : AbilitySlotDirection.None;
                for (int slot = 0; slot < AbilityCatalog.AbilitySlotCount; slot++)
                {
                    TryEnqueueKeyboardAbility(
                        keyboard[GetDesktopAbilityKey(slot)],
                        GetDesktopAbility(slot, direction));
                }
            }

            Gamepad gamepad = Gamepad.current;
            if (gamepad != null)
            {
                movement = Vector2.ClampMagnitude(movement + gamepad.leftStick.ReadValue(), 1f);
                float gamepadOrbit = gamepad.rightStick.ReadValue().x;
                if (Mathf.Abs(gamepadOrbit) > GamepadOrbitDeadZone)
                {
                    orbitAxis += gamepadOrbit;
                }
                TryEnqueueGamepadAbility(gamepad.buttonSouth, GetDesktopAbility(0));
                TryEnqueueGamepadAbility(gamepad.buttonWest, GetDesktopAbility(1));
                TryEnqueueGamepadAbility(gamepad.buttonNorth, GetDesktopAbility(2));
                TryEnqueueGamepadAbility(gamepad.buttonEast, GetDesktopAbility(3));
                TryEnqueueGamepadAbility(gamepad.leftTrigger, GetDesktopAbility(4));
            }

            if (Mathf.Abs(orbitAxis) > 0.0001f)
            {
                EmitCameraOrbit(
                    Mathf.Clamp(orbitAxis, -1f, 1f) *
                    _desktopOrbitDegreesPerSecond *
                    Time.unscaledDeltaTime);
            }
        }

        private void EmitCameraOrbit(float yawDelta)
        {
            if (Mathf.Abs(yawDelta) > 0.0001f)
            {
                CameraOrbitRequested?.Invoke(yawDelta);
            }
        }

        private void TryEnqueueKeyboardAbility(KeyControl key, AbilityId abilityId)
        {
            if (abilityId != AbilityId.None && key != null && key.wasPressedThisFrame)
            {
                Vector2 screenPosition = Mouse.current != null
                    ? Mouse.current.position.ReadValue()
                    : new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                EnqueueGesture(new AbilityGesture(
                    abilityId,
                    AbilityGesturePhase.Tapped,
                    EditorPointerId,
                    screenPosition,
                    Vector2.zero,
                    0f));
            }
        }

        private void TryEnqueueGamepadAbility(ButtonControl button, AbilityId abilityId)
        {
            if (abilityId != AbilityId.None && button != null && button.wasPressedThisFrame)
            {
                EnqueueGesture(new AbilityGesture(
                    abilityId,
                    AbilityGesturePhase.Tapped,
                    EditorPointerId,
                    new Vector2(Screen.width * 0.5f, Screen.height * 0.5f),
                    Vector2.zero,
                    0f));
            }
        }

        public void SetDesktopAlternateUpBindings(params AbilityId[] bindings)
        {
            EnsureDesktopBindingCapacity();
            Array.Clear(
                _desktopAlternateUpBindings,
                0,
                _desktopAlternateUpBindings.Length);
            if (bindings == null)
            {
                return;
            }

            int count = Mathf.Min(bindings.Length, _desktopAlternateUpBindings.Length);
            Array.Copy(bindings, _desktopAlternateUpBindings, count);
        }

        public void SetDesktopAlternateRightBindings(params AbilityId[] bindings)
        {
            EnsureDesktopBindingCapacity();
            Array.Clear(
                _desktopAlternateRightBindings,
                0,
                _desktopAlternateRightBindings.Length);
            if (bindings == null)
            {
                return;
            }

            int count = Mathf.Min(
                bindings.Length,
                _desktopAlternateRightBindings.Length);
            Array.Copy(bindings, _desktopAlternateRightBindings, count);
        }

        private AbilityId GetDesktopAbility(
            int bindingIndex,
            AbilitySlotDirection direction = AbilitySlotDirection.None)
        {
            AbilityId[] bindings;
            switch (direction)
            {
                case AbilitySlotDirection.Up:
                    bindings = _desktopAlternateUpBindings;
                    break;
                case AbilitySlotDirection.Right:
                    bindings = _desktopAlternateRightBindings;
                    break;
                default:
                    bindings = _desktopAbilityBindings;
                    break;
            }

            return bindings != null &&
                   bindingIndex >= 0 &&
                   bindingIndex < bindings.Length
                ? bindings[bindingIndex]
                : AbilityId.None;
        }

        public static Key GetDesktopAbilityKey(int slot)
        {
            switch (slot)
            {
                case 0:
                    return Key.Digit1;
                case 1:
                    return Key.Digit2;
                case 2:
                    return Key.Digit3;
                case 3:
                    return Key.Digit4;
                case 4:
                    return Key.Digit5;
                case 5:
                    return Key.Digit6;
                case 6:
                    return Key.Digit7;
                case 7:
                    return Key.Digit8;
                case 8:
                    return Key.Digit9;
                case 9:
                    return Key.Digit0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(slot));
            }
        }

        private void EnsureDesktopBindingCapacity()
        {
            if (_desktopAbilityBindings == null ||
                _desktopAbilityBindings.Length != AbilityCatalog.AbilitySlotCount)
            {
                Array.Resize(
                    ref _desktopAbilityBindings,
                    AbilityCatalog.AbilitySlotCount);
            }

            if (_desktopAlternateUpBindings == null ||
                _desktopAlternateUpBindings.Length != AbilityCatalog.AbilitySlotCount)
            {
                Array.Resize(
                    ref _desktopAlternateUpBindings,
                    AbilityCatalog.AbilitySlotCount);
            }

            if (_desktopAlternateRightBindings == null ||
                _desktopAlternateRightBindings.Length != AbilityCatalog.AbilitySlotCount)
            {
                Array.Resize(
                    ref _desktopAlternateRightBindings,
                    AbilityCatalog.AbilitySlotCount);
            }
        }

        private static float ReadAxis(bool negative, bool positive)
        {
            if (negative == positive)
            {
                return 0f;
            }

            return positive ? 1f : -1f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            EnsureDesktopBindingCapacity();
            _touchOrbitDegreesPerScreen = Mathf.Clamp(
                _touchOrbitDegreesPerScreen,
                45f,
                360f);
            _desktopOrbitDegreesPerSecond = Mathf.Clamp(
                _desktopOrbitDegreesPerSecond,
                30f,
                240f);
        }
#endif
    }
}
