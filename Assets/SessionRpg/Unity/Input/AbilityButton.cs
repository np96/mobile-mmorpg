using System;
using SessionRpg.Core;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SessionRpg.Unity.Input
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class AbilityButton : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IDragHandler,
        ICancelHandler
    {
        private const int NoPointer = int.MinValue;
        private const float ReferenceDpi = 160f;
        private const float MaximumScaledFlickThresholdPixels = 52f;

        [SerializeField] private AbilityId _abilityId;
        [SerializeField] private AbilityId _flickUpAbilityId;
        [SerializeField] private AbilityId _flickDownAbilityId;
        [SerializeField] private AbilityId _flickLeftAbilityId;
        [SerializeField] private AbilityId _flickRightAbilityId;
        [SerializeField] private bool _scaleFlickThresholdWithDpi = true;
        [SerializeField, Min(0f)] private float _flickThresholdDp = 20f;
        [SerializeField, Min(0f)] private float _flickThresholdPixels = 32f;
        [SerializeField, Min(0f)] private float _maximumFlickSeconds = 0.32f;

        private int _pointerId = NoPointer;
        private Vector2 _pressPosition;
        private Vector2 _latestPosition;
        private float _pressTime;
        private bool _isDirectionLocked;
        private AbilityFlickSelection _selection;

        public event Action<AbilityGesture> GestureChanged;

        public event Action<AbilityFlickSelection> SelectionChanged;

        public AbilityId AbilityId => _abilityId;

        public bool IsPressed => _pointerId != NoPointer;

        public int ActivePointerId => _pointerId;

        public AbilityFlickSelection Selection => _selection;

        public AbilityId SelectedAbilityId => _pointerId == NoPointer
            ? _abilityId
            : _selection.AbilityId;

        public AbilityFlickDirection SelectedDirection => _selection.Direction;

        public bool IsSelectionLocked => _isDirectionLocked;

        private void OnDisable()
        {
            if (_pointerId != NoPointer)
            {
                Emit(SelectedAbilityId, AbilityGesturePhase.Canceled, _latestPosition);
            }

            ResetState();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isActiveAndEnabled ||
                _pointerId != NoPointer ||
                eventData.button != PointerEventData.InputButton.Left)
            {
                return;
            }

            _pointerId = eventData.pointerId;
            _pressPosition = eventData.position;
            _latestPosition = eventData.position;
            _pressTime = Time.unscaledTime;
            _isDirectionLocked = false;
            SetSelection(new AbilityFlickSelection(
                _abilityId,
                AbilityFlickDirection.None,
                true,
                false,
                false,
                false));
            Emit(_abilityId, AbilityGesturePhase.Pressed, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
            {
                return;
            }

            _latestPosition = eventData.position;
            ClassifyMovement(_latestPosition);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
            {
                return;
            }

            _latestPosition = eventData.position;
            ClassifyMovement(_latestPosition);

            if (_isDirectionLocked && _selection.HasDirectionalBinding)
            {
                Emit(SelectedAbilityId, AbilityGesturePhase.Flicked, _latestPosition);
            }
            else if (!_isDirectionLocked)
            {
                Emit(_abilityId, AbilityGesturePhase.Tapped, _latestPosition);
            }
            else
            {
                Emit(AbilityId.None, AbilityGesturePhase.Canceled, _latestPosition);
            }

            ResetState();
        }

        public void OnCancel(BaseEventData eventData)
        {
            CancelActivePointer();
        }

        public void BindAbility(AbilityId abilityId)
        {
            _abilityId = abilityId;
            if (_pointerId == NoPointer)
            {
                SetSelection(CreateInactiveSelection());
            }
        }

        public void BindDirectionalAbility(
            AbilityFlickDirection direction,
            AbilityId abilityId)
        {
            switch (direction)
            {
                case AbilityFlickDirection.Up:
                    _flickUpAbilityId = abilityId;
                    break;
                case AbilityFlickDirection.Down:
                    _flickDownAbilityId = abilityId;
                    break;
                case AbilityFlickDirection.Left:
                    _flickLeftAbilityId = abilityId;
                    break;
                case AbilityFlickDirection.Right:
                    _flickRightAbilityId = abilityId;
                    break;
                case AbilityFlickDirection.None:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
            }
        }

        public AbilityId GetDirectionalAbility(AbilityFlickDirection direction)
        {
            switch (direction)
            {
                case AbilityFlickDirection.Up:
                    return _flickUpAbilityId;
                case AbilityFlickDirection.Down:
                    return _flickDownAbilityId;
                case AbilityFlickDirection.Left:
                    return _flickLeftAbilityId;
                case AbilityFlickDirection.Right:
                    return _flickRightAbilityId;
                default:
                    return AbilityId.None;
            }
        }

        public void ClearDirectionalAbilities()
        {
            _flickUpAbilityId = AbilityId.None;
            _flickDownAbilityId = AbilityId.None;
            _flickLeftAbilityId = AbilityId.None;
            _flickRightAbilityId = AbilityId.None;
        }

        public void ConfigureFlickThreshold(
            float thresholdDp,
            float thresholdPixels,
            bool scaleWithDpi)
        {
            _flickThresholdDp = Mathf.Max(0f, thresholdDp);
            _flickThresholdPixels = Mathf.Max(0f, thresholdPixels);
            _scaleFlickThresholdWithDpi = scaleWithDpi;
        }

        public void ConfigureMaximumFlickDuration(float seconds)
        {
            // Retained for compatibility with generated HUD authoring. Radial
            // selection intentionally has no time limit so a hold can inspect it.
            _maximumFlickSeconds = Mathf.Max(0f, seconds);
        }

        public void CancelActivePointer()
        {
            if (_pointerId != NoPointer)
            {
                Emit(SelectedAbilityId, AbilityGesturePhase.Canceled, _latestPosition);
            }

            ResetState();
        }

        private void ClassifyMovement(Vector2 screenPosition)
        {
            Vector2 displacement = screenPosition - _pressPosition;
            float thresholdPixels = GetFlickThresholdPixels();
            if (displacement.sqrMagnitude < thresholdPixels * thresholdPixels)
            {
                _isDirectionLocked = false;
                SetSelection(new AbilityFlickSelection(
                    _abilityId,
                    AbilityFlickDirection.None,
                    true,
                    false,
                    false,
                    false));
                return;
            }

            AbilityFlickDirection direction = QuantizeDirection(displacement);
            AbilityId directionalAbility = GetDirectionalAbility(direction);
            bool hasBinding = directionalAbility != AbilityId.None;
            _isDirectionLocked = true;
            SetSelection(new AbilityFlickSelection(
                hasBinding ? directionalAbility : AbilityId.None,
                direction,
                true,
                true,
                hasBinding,
                false));
        }

        private float GetFlickThresholdPixels()
        {
            if (!_scaleFlickThresholdWithDpi || Screen.dpi <= 0f)
            {
                return _flickThresholdPixels;
            }

            return Mathf.Min(
                _flickThresholdDp * Screen.dpi / ReferenceDpi,
                MaximumScaledFlickThresholdPixels);
        }

        private static AbilityFlickDirection QuantizeDirection(Vector2 displacement)
        {
            if (Mathf.Abs(displacement.x) >= Mathf.Abs(displacement.y))
            {
                return displacement.x >= 0f
                    ? AbilityFlickDirection.Right
                    : AbilityFlickDirection.Left;
            }

            return displacement.y >= 0f
                ? AbilityFlickDirection.Up
                : AbilityFlickDirection.Down;
        }

        private void Emit(
            AbilityId abilityId,
            AbilityGesturePhase phase,
            Vector2 screenPosition)
        {
            float holdDuration = _pointerId == NoPointer
                ? 0f
                : Mathf.Max(0f, Time.unscaledTime - _pressTime);
            GestureChanged?.Invoke(new AbilityGesture(
                abilityId,
                phase,
                _pointerId,
                screenPosition,
                screenPosition - _pressPosition,
                holdDuration));
        }

        private void SetSelection(AbilityFlickSelection selection)
        {
            if (_selection.AbilityId == selection.AbilityId &&
                _selection.Direction == selection.Direction &&
                _selection.IsPressed == selection.IsPressed &&
                _selection.IsLocked == selection.IsLocked &&
                _selection.HasDirectionalBinding == selection.HasDirectionalBinding &&
                _selection.IsCanceled == selection.IsCanceled)
            {
                return;
            }

            _selection = selection;
            SelectionChanged?.Invoke(_selection);
        }

        private AbilityFlickSelection CreateInactiveSelection()
        {
            return new AbilityFlickSelection(
                _abilityId,
                AbilityFlickDirection.None,
                false,
                false,
                false,
                false);
        }

        private void ResetState()
        {
            _pointerId = NoPointer;
            _isDirectionLocked = false;
            SetSelection(CreateInactiveSelection());
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _flickThresholdDp = Mathf.Max(0f, _flickThresholdDp);
            _flickThresholdPixels = Mathf.Max(0f, _flickThresholdPixels);
            _maximumFlickSeconds = Mathf.Max(0f, _maximumFlickSeconds);
        }
#endif
    }
}
