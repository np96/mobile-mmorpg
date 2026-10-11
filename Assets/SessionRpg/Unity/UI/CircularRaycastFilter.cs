using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Unity.UI
{
    /// <summary>
    /// Restricts a UI Graphic's hit area to the largest circle contained by its
    /// RectTransform. The filter is allocation-free and supports overlay,
    /// camera-space, and world-space canvases through the event camera supplied
    /// by Unity's GraphicRaycaster.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class CircularRaycastFilter : MonoBehaviour, ICanvasRaycastFilter
    {
        public bool IsRaycastLocationValid(
            Vector2 screenPoint,
            UnityEngine.Camera eventCamera)
        {
            RectTransform rectTransform = transform as RectTransform;
            if (rectTransform == null ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    rectTransform,
                    screenPoint,
                    eventCamera,
                    out Vector2 localPoint))
            {
                return false;
            }

            Rect bounds = rectTransform.rect;
            float radius = Mathf.Min(bounds.width, bounds.height) * 0.5f;
            if (radius <= 0f)
            {
                return false;
            }

            Vector2 fromCenter = localPoint - bounds.center;
            return fromCenter.sqrMagnitude <= radius * radius;
        }
    }
}
