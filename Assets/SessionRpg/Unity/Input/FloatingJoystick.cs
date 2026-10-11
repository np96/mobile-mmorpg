using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SessionRpg.Unity.Input
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class FloatingJoystick : MonoBehaviour,
        IPointerDownHandler,
        IPointerUpHandler,
        IDragHandler,
        ICancelHandler
    {
        private const int NoPointer = int.MinValue;

        [SerializeField] private RectTransform _baseRect;
        [SerializeField] private RectTransform _handleRect;
        [SerializeField] private bool _moveBaseToTouch = true;
        [SerializeField, Min(0f)] private float _radius = 84f;
        [SerializeField, Range(0f, 0.95f)] private float _deadZone = 0.08f;

        private int _pointerId = NoPointer;
        private Vector2 _defaultBasePosition;
        private Vector2 _value;

        public event Action<Vector2> ValueChanged;

        public Vector2 Value => _value;

        public bool IsActive => _pointerId != NoPointer;

        public int ActivePointerId => _pointerId;

        private void Awake()
        {
            if (_baseRect == null)
            {
                _baseRect = (RectTransform)transform;
            }

            _defaultBasePosition = _baseRect.anchoredPosition;
            _value = Vector2.zero;
            if (_handleRect != null)
            {
                _handleRect.anchoredPosition = Vector2.zero;
            }
        }

        private void OnDisable()
        {
            ResetPointer(true);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || _pointerId != NoPointer)
            {
                return;
            }

            _pointerId = eventData.pointerId;

            if (_moveBaseToTouch && _baseRect.parent is RectTransform parentRect)
            {
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        parentRect,
                        eventData.position,
                        eventData.pressEventCamera,
                        out Vector2 localPoint))
                {
                    _baseRect.anchoredPosition = localPoint;
                }
            }

            UpdateValue(eventData.position, eventData.pressEventCamera);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
            {
                return;
            }

            UpdateValue(eventData.position, eventData.pressEventCamera);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId)
            {
                ResetPointer(true);
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            ResetPointer(true);
        }

        public void ClearPointer()
        {
            ResetPointer(true);
        }

        private void UpdateValue(Vector2 screenPosition, UnityEngine.Camera eventCamera)
        {
            if (_baseRect == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _baseRect,
                    screenPosition,
                    eventCamera,
                    out Vector2 localPoint))
            {
                return;
            }

            float radius = _radius > 0f
                ? _radius
                : Mathf.Min(_baseRect.rect.width, _baseRect.rect.height) * 0.5f;
            Vector2 normalized = radius > 0.001f
                ? Vector2.ClampMagnitude(localPoint / radius, 1f)
                : Vector2.zero;

            float magnitude = normalized.magnitude;
            if (magnitude <= _deadZone)
            {
                normalized = Vector2.zero;
            }
            else if (magnitude > 0f)
            {
                float rescaledMagnitude = Mathf.InverseLerp(_deadZone, 1f, magnitude);
                normalized = normalized.normalized * rescaledMagnitude;
            }

            SetValue(normalized);
        }

        private void SetValue(Vector2 value)
        {
            if ((_value - value).sqrMagnitude <= 0.000001f)
            {
                return;
            }

            _value = value;
            if (_handleRect != null)
            {
                float radius = _radius > 0f
                    ? _radius
                    : Mathf.Min(_baseRect.rect.width, _baseRect.rect.height) * 0.5f;
                _handleRect.anchoredPosition = _value * radius;
            }

            ValueChanged?.Invoke(_value);
        }

        private void ResetPointer(bool restoreBase)
        {
            _pointerId = NoPointer;
            SetValue(Vector2.zero);

            if (restoreBase && _moveBaseToTouch && _baseRect != null)
            {
                _baseRect.anchoredPosition = _defaultBasePosition;
            }
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (_baseRect == null)
            {
                _baseRect = GetComponent<RectTransform>();
            }

            _radius = Mathf.Max(0f, _radius);
        }
#endif
    }
}
