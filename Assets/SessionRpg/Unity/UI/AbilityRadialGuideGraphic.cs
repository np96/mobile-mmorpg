using SessionRpg.Unity.Input;
using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Unity.UI
{
    /// <summary>
    /// Draws the persistent, layered directional ring for one grouped ability
    /// control. Only bound sectors produce geometry. The individual ability
    /// icons remain ordinary non-interactive children; this graphic communicates
    /// depth, hold state, and selection without owning input or resizing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AbilityRadialGuideGraphic : MaskableGraphic
    {
        private const int UpMask = 1 << 0;
        private const int DownMask = 1 << 1;
        private const int LeftMask = 1 << 2;
        private const int RightMask = 1 << 3;
        private const float DepthOffset = 2.5f;
        private const float DepthOuterInset = 3.5f;
        private const float FaceInnerInset = 1.5f;
        private const float FaceOuterInset = 4f;
        private const float InnerBevelWidth = 4.5f;
        private const float OuterBevelWidth = 6f;

        private static readonly Color DeepShadow =
            new Color32(2, 4, 8, 255);
        private static readonly Color ObsidianBlue =
            new Color32(7, 14, 21, 255);
        private static readonly Color RaisedSlate =
            new Color32(24, 39, 48, 255);

        [SerializeField, Range(0.2f, 0.8f)] private float _innerRadiusRatio = 0.4f;
        [SerializeField, Range(2f, 14f)] private float _sectorGapDegrees = 3f;
        [SerializeField, Range(4, 24)] private int _segmentsPerSector = 10;
        [SerializeField] private Color _boundSectorColor =
            new Color32(55, 132, 153, 255);
        [SerializeField] private Color _heldBoundSectorColor =
            new Color32(77, 193, 216, 255);
        [SerializeField] private Color _selectedSectorColor =
            new Color32(238, 180, 82, 255);

        private int _boundMask;
        private AbilityFlickDirection _selectedDirection;
        private bool _expanded;

        public bool IsExpanded => _expanded;

        public AbilityFlickDirection SelectedDirection => _selectedDirection;

        public int BoundDirectionCount
        {
            get
            {
                int mask = _boundMask;
                int count = 0;
                while (mask != 0)
                {
                    count += mask & 1;
                    mask >>= 1;
                }

                return count;
            }
        }

        public void SetGuideState(
            bool expanded,
            bool hasUp,
            bool hasDown,
            bool hasLeft,
            bool hasRight,
            AbilityFlickDirection selectedDirection)
        {
            int boundMask = (hasUp ? UpMask : 0) |
                (hasDown ? DownMask : 0) |
                (hasLeft ? LeftMask : 0) |
                (hasRight ? RightMask : 0);
            AbilityFlickDirection safeSelection =
                IsBound(boundMask, selectedDirection)
                    ? selectedDirection
                    : AbilityFlickDirection.None;
            bool shouldExpand = expanded && boundMask != 0;
            if (_expanded == shouldExpand &&
                _boundMask == boundMask &&
                _selectedDirection == safeSelection)
            {
                return;
            }

            _expanded = shouldExpand;
            _boundMask = boundMask;
            _selectedDirection = safeSelection;
            SetVerticesDirty();
        }

        public bool HasDirection(AbilityFlickDirection direction)
        {
            return IsBound(_boundMask, direction);
        }

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (_boundMask == 0)
            {
                return;
            }

            Rect bounds = rectTransform.rect;
            float outerRadius = Mathf.Min(bounds.width, bounds.height) * 0.5f;
            float innerRadius = outerRadius * Mathf.Clamp(_innerRadiusRatio, 0.2f, 0.8f);
            if (outerRadius <= 0.001f || innerRadius >= outerRadius)
            {
                return;
            }

            Vector2 center = bounds.center;
            if (HasDirection(AbilityFlickDirection.Up))
            {
                AddSector(helper, center, innerRadius, outerRadius, 90f,
                    AbilityFlickDirection.Up);
            }
            if (HasDirection(AbilityFlickDirection.Right))
            {
                AddSector(helper, center, innerRadius, outerRadius, 0f,
                    AbilityFlickDirection.Right);
            }
            if (HasDirection(AbilityFlickDirection.Down))
            {
                AddSector(helper, center, innerRadius, outerRadius, -90f,
                    AbilityFlickDirection.Down);
            }
            if (HasDirection(AbilityFlickDirection.Left))
            {
                AddSector(helper, center, innerRadius, outerRadius, 180f,
                    AbilityFlickDirection.Left);
            }
        }

        private void AddSector(
            VertexHelper helper,
            Vector2 center,
            float innerRadius,
            float outerRadius,
            float centerDegrees,
            AbilityFlickDirection direction)
        {
            int segmentCount = Mathf.Clamp(_segmentsPerSector, 4, 24);
            float halfArc = (90f - Mathf.Clamp(_sectorGapDegrees, 2f, 14f)) * 0.5f;
            float startDegrees = centerDegrees - halfArc;
            float stepDegrees = (halfArc * 2f) / segmentCount;
            bool selected = _expanded && direction == _selectedDirection;
            Color accent = ResolveSectorAccent(direction);

            Color depthInner = WithAlpha(DeepShadow, 0.94f);
            Color depthOuter = WithAlpha(
                Color.Lerp(DeepShadow, accent, selected ? 0.22f : 0.12f),
                0.92f);
            AddSectorBand(
                helper,
                center + Vector2.down * DepthOffset,
                innerRadius,
                outerRadius - DepthOuterInset,
                startDegrees,
                stepDegrees,
                segmentCount,
                depthInner,
                depthOuter);

            float stateStrength = selected
                ? 0.82f
                : _expanded
                    ? 0.56f
                    : 0.42f;
            Color faceInner = WithAlpha(
                Color.Lerp(ObsidianBlue, accent, stateStrength * 0.42f),
                selected ? 1f : 0.97f);
            Color faceOuter = WithAlpha(
                Color.Lerp(RaisedSlate, accent, stateStrength),
                selected ? 1f : _expanded ? 0.98f : 0.95f);
            AddSectorBand(
                helper,
                center,
                innerRadius + FaceInnerInset,
                outerRadius - FaceOuterInset,
                startDegrees,
                stepDegrees,
                segmentCount,
                faceInner,
                faceOuter);

            Color outerBevelInner = WithAlpha(
                Color.Lerp(ObsidianBlue, accent, stateStrength * 0.55f),
                selected ? 0.98f : 0.88f);
            Color outerBevelEdge = WithAlpha(
                Color.Lerp(accent, Color.white, selected ? 0.34f : 0.16f),
                selected ? 1f : _expanded ? 0.94f : 0.8f);
            AddSectorBand(
                helper,
                center,
                outerRadius - OuterBevelWidth - 2f,
                outerRadius - 2f,
                startDegrees,
                stepDegrees,
                segmentCount,
                outerBevelInner,
                outerBevelEdge);

            Color innerBevelEdge = WithAlpha(
                Color.Lerp(accent, Color.white, selected ? 0.24f : 0.1f),
                selected ? 0.96f : _expanded ? 0.86f : 0.68f);
            Color innerBevelBody = WithAlpha(
                Color.Lerp(ObsidianBlue, accent, stateStrength * 0.48f),
                0.94f);
            AddSectorBand(
                helper,
                center,
                innerRadius + 0.5f,
                innerRadius + InnerBevelWidth,
                startDegrees,
                stepDegrees,
                segmentCount,
                innerBevelEdge,
                innerBevelBody);
        }

        private void AddSectorBand(
            VertexHelper helper,
            Vector2 center,
            float innerRadius,
            float outerRadius,
            float startDegrees,
            float stepDegrees,
            int segmentCount,
            Color innerColor,
            Color outerColor)
        {
            Color32 tintedInner = ApplyGraphicTint(innerColor);
            Color32 tintedOuter = ApplyGraphicTint(outerColor);
            for (int segment = 0; segment < segmentCount; segment++)
            {
                float firstRadians = (startDegrees + stepDegrees * segment) *
                    Mathf.Deg2Rad;
                float secondRadians = (startDegrees + stepDegrees * (segment + 1)) *
                    Mathf.Deg2Rad;
                Vector2 first = new Vector2(
                    Mathf.Cos(firstRadians),
                    Mathf.Sin(firstRadians));
                Vector2 second = new Vector2(
                    Mathf.Cos(secondRadians),
                    Mathf.Sin(secondRadians));
                int firstVertex = helper.currentVertCount;
                helper.AddVert(
                    center + first * innerRadius,
                    tintedInner,
                    Vector2.zero);
                helper.AddVert(
                    center + first * outerRadius,
                    tintedOuter,
                    Vector2.zero);
                helper.AddVert(
                    center + second * outerRadius,
                    tintedOuter,
                    Vector2.zero);
                helper.AddVert(
                    center + second * innerRadius,
                    tintedInner,
                    Vector2.zero);
                helper.AddTriangle(firstVertex, firstVertex + 1, firstVertex + 2);
                helper.AddTriangle(firstVertex, firstVertex + 2, firstVertex + 3);
            }
        }

        private Color ResolveSectorAccent(AbilityFlickDirection direction)
        {
            Color accent = _expanded && direction == _selectedDirection
                ? _selectedSectorColor
                : _expanded
                    ? _heldBoundSectorColor
                    : _boundSectorColor;
            accent.a = 1f;
            return accent;
        }

        private Color32 ApplyGraphicTint(Color source)
        {
            Color graphicTint = color;
            source.r *= graphicTint.r;
            source.g *= graphicTint.g;
            source.b *= graphicTint.b;
            source.a *= graphicTint.a;
            return source;
        }

        private static Color WithAlpha(Color source, float alpha)
        {
            source.a = alpha;
            return source;
        }

        private static bool IsBound(int mask, AbilityFlickDirection direction)
        {
            int bit;
            switch (direction)
            {
                case AbilityFlickDirection.Up:
                    bit = UpMask;
                    break;
                case AbilityFlickDirection.Down:
                    bit = DownMask;
                    break;
                case AbilityFlickDirection.Left:
                    bit = LeftMask;
                    break;
                case AbilityFlickDirection.Right:
                    bit = RightMask;
                    break;
                default:
                    return false;
            }

            return (mask & bit) != 0;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _innerRadiusRatio = Mathf.Clamp(_innerRadiusRatio, 0.2f, 0.8f);
            _sectorGapDegrees = Mathf.Clamp(_sectorGapDegrees, 2f, 14f);
            _segmentsPerSector = Mathf.Clamp(_segmentsPerSector, 4, 24);
            raycastTarget = false;
            SetVerticesDirty();
        }
#endif
    }
}
