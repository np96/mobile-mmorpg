using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SessionRpg.Unity.Input
{
    public readonly struct WorldOrbitGesture
    {
        public WorldOrbitGesture(int pointerId, Vector2 screenDelta)
        {
            PointerId = pointerId;
            ScreenDelta = screenDelta;
        }

        public int PointerId { get; }

        public Vector2 ScreenDelta { get; }
    }

    /// <summary>
    /// Turns a short press on unobstructed world space into a selection tap and a
    /// drag into presentation-only camera orbit. One pointer owns the gesture from
    /// press through release so a second touch cannot steal or complete it.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldTapSurface : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IDragHandler,
        ICancelHandler
    {
        private const int NoPointer = int.MinValue;

        [SerializeField, Min(0f)] private float _tapMovementThreshold = 24f;

        private int _pointerId = NoPointer;
        private Vector2 _pressPosition;
        private Vector2 _latestPosition;
        private float _maximumDisplacementSquared;
        private bool _isOrbiting;

        public event Action<Vector2> Tapped;

        public event Action<WorldOrbitGesture> OrbitDragged;

        public bool IsActive => _pointerId != NoPointer;

        public int ActivePointerId => _pointerId;

        public bool IsOrbiting => _isOrbiting;

        private void OnDisable()
        {
            ClearPointer();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (isActiveAndEnabled &&
                _pointerId == NoPointer &&
                eventData.button == PointerEventData.InputButton.Left)
            {
                _pointerId = eventData.pointerId;
                _pressPosition = eventData.position;
                _latestPosition = eventData.position;
                _maximumDisplacementSquared = 0f;
                _isOrbiting = false;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId)
            {
                Vector2 previousPosition = _latestPosition;
                RecordDisplacement(eventData.position);
                bool crossedThreshold = _maximumDisplacementSquared >
                    _tapMovementThreshold * _tapMovementThreshold;
                if (crossedThreshold)
                {
                    Vector2 delta = _isOrbiting
                        ? eventData.position - previousPosition
                        : eventData.position - _pressPosition;
                    _isOrbiting = true;
                    if (delta.sqrMagnitude > 0.0001f)
                    {
                        OrbitDragged?.Invoke(new WorldOrbitGesture(
                            _pointerId,
                            delta));
                    }
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
            {
                return;
            }

            RecordDisplacement(eventData.position);
            Vector2 releasePosition = eventData.position;
            bool tapped = !_isOrbiting && _maximumDisplacementSquared <=
                _tapMovementThreshold * _tapMovementThreshold;
            ClearPointer();

            if (tapped)
            {
                Tapped?.Invoke(releasePosition);
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            ClearPointer();
        }

        public void ClearPointer()
        {
            _pointerId = NoPointer;
            _pressPosition = Vector2.zero;
            _latestPosition = Vector2.zero;
            _maximumDisplacementSquared = 0f;
            _isOrbiting = false;
        }

        private void RecordDisplacement(Vector2 pointerPosition)
        {
            float displacementSquared = (pointerPosition - _pressPosition).sqrMagnitude;
            _maximumDisplacementSquared = Mathf.Max(
                _maximumDisplacementSquared,
                displacementSquared);
            _latestPosition = pointerPosition;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _tapMovementThreshold = Mathf.Max(0f, _tapMovementThreshold);
        }
#endif
    }
}
