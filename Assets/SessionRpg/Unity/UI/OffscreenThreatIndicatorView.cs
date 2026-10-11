using SessionRpg.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Unity.UI
{
    [DisallowMultipleComponent]
    public sealed class OffscreenThreatIndicatorView : MonoBehaviour
    {
        [SerializeField] private RectTransform _root;
        [SerializeField] private RectTransform _directionArrow;
        [SerializeField] private Graphic _badgeSurface;
        [SerializeField] private Text _classBadgeLabel;
        [SerializeField] private Text _healthLabel;
        [SerializeField] private GameObject _castRoot;

        private ClassId _presentedClass;
        private int _healthPercent = int.MinValue;
        private bool _isCasting;
        private Vector2 _direction;

        public ClassId PresentedClass => _presentedClass;

        public int HealthPercent => _healthPercent;

        public bool IsCasting => _isCasting;

        public bool IsVisible => _root != null && _root.gameObject.activeSelf;

        public Vector2 Direction => _direction;

        public Vector2 AnchoredPosition => _root != null
            ? _root.anchoredPosition
            : Vector2.zero;

        public void Present(
            ClassId classId,
            float health,
            float maximumHealth,
            bool isCasting,
            Vector2 anchoredPosition,
            Vector2 direction)
        {
            if (_root == null)
            {
                return;
            }

            if (!_root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(true);
            }

            if (_presentedClass != classId)
            {
                _presentedClass = classId;
                SetTextIfChanged(_classBadgeLabel, GetClassBadge(classId));
                if (_badgeSurface != null)
                {
                    _badgeSurface.color = GetClassColor(classId);
                }
            }

            int healthPercent = maximumHealth > 0.001f
                ? Mathf.Clamp(Mathf.RoundToInt((health / maximumHealth) * 100f), 0, 100)
                : 0;
            if (_healthPercent != healthPercent)
            {
                _healthPercent = healthPercent;
                SetTextIfChanged(_healthLabel, healthPercent.ToString() + "%");
                if (_healthLabel != null)
                {
                    _healthLabel.color = healthPercent <= 30
                        ? new Color32(244, 112, 86, 255)
                        : new Color32(239, 245, 250, 255);
                }
            }

            if (_isCasting != isCasting)
            {
                _isCasting = isCasting;
                if (_castRoot != null)
                {
                    _castRoot.SetActive(isCasting);
                }
            }

            _root.anchoredPosition = anchoredPosition;
            _direction = direction;
            if (_directionArrow != null)
            {
                float angle = Mathf.Atan2(-direction.x, direction.y) * Mathf.Rad2Deg;
                _directionArrow.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
        }

        public void Hide()
        {
            if (_root != null && _root.gameObject.activeSelf)
            {
                _root.gameObject.SetActive(false);
            }
        }

        private static string GetClassBadge(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Priest:
                    return "P";
                case ClassId.Rogue:
                    return "R";
                case ClassId.Mage:
                    return "M";
                case ClassId.Hunter:
                    return "H";
                case ClassId.Boss:
                    return "B";
                default:
                    return "?";
            }
        }

        private static Color GetClassColor(ClassId classId)
        {
            switch (classId)
            {
                case ClassId.Priest:
                    return new Color32(75, 205, 151, 255);
                case ClassId.Rogue:
                    return new Color32(238, 183, 71, 255);
                case ClassId.Mage:
                    return new Color32(103, 171, 244, 255);
                case ClassId.Hunter:
                    return new Color32(138, 184, 86, 255);
                case ClassId.Boss:
                    return new Color32(224, 76, 68, 255);
                default:
                    return new Color32(159, 180, 201, 255);
            }
        }

        private static void SetTextIfChanged(Text label, string value)
        {
            if (label != null && label.text != value)
            {
                label.text = value;
            }
        }
    }
}
