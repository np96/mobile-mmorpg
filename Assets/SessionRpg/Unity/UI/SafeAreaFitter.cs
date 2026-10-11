using UnityEngine;

namespace SessionRpg.Unity.UI
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        [SerializeField] private bool _fitHorizontally = true;
        [SerializeField] private bool _fitVertically = true;

        private RectTransform _rectTransform;
        private Rect _lastSafeArea;
        private int _lastScreenWidth = -1;
        private int _lastScreenHeight = -1;

        private void OnEnable()
        {
            _rectTransform = GetComponent<RectTransform>();
            ApplySafeArea(true);
        }

        private void Update()
        {
            ApplySafeArea(false);
        }

        public void ApplySafeArea(bool force = true)
        {
            int screenWidth = Screen.width;
            int screenHeight = Screen.height;
            Rect safeArea = Screen.safeArea;

            if (!force &&
                screenWidth == _lastScreenWidth &&
                screenHeight == _lastScreenHeight &&
                ApproximatelyEqual(safeArea, _lastSafeArea))
            {
                return;
            }

            _lastScreenWidth = screenWidth;
            _lastScreenHeight = screenHeight;
            _lastSafeArea = safeArea;

            if (_rectTransform == null || screenWidth <= 0 || screenHeight <= 0)
            {
                return;
            }

            Vector2 minimum = safeArea.position;
            Vector2 maximum = safeArea.position + safeArea.size;
            minimum.x /= screenWidth;
            maximum.x /= screenWidth;
            minimum.y /= screenHeight;
            maximum.y /= screenHeight;

            Vector2 anchorMinimum = _rectTransform.anchorMin;
            Vector2 anchorMaximum = _rectTransform.anchorMax;
            if (_fitHorizontally)
            {
                anchorMinimum.x = minimum.x;
                anchorMaximum.x = maximum.x;
            }

            if (_fitVertically)
            {
                anchorMinimum.y = minimum.y;
                anchorMaximum.y = maximum.y;
            }

            _rectTransform.anchorMin = anchorMinimum;
            _rectTransform.anchorMax = anchorMaximum;
            _rectTransform.offsetMin = Vector2.zero;
            _rectTransform.offsetMax = Vector2.zero;
        }

        private static bool ApproximatelyEqual(Rect left, Rect right)
        {
            return Mathf.Approximately(left.x, right.x) &&
                   Mathf.Approximately(left.y, right.y) &&
                   Mathf.Approximately(left.width, right.width) &&
                   Mathf.Approximately(left.height, right.height);
        }
    }
}
