using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.UI
{
    [DisallowMultipleComponent]
    public sealed class OffscreenThreatPresenter : MonoBehaviour
    {
        public const int MaximumIndicatorCount = 2;

        private const float CombatantWorldHeight = 1.6f;
        private const float MinimumDirectionMagnitude = 0.0001f;

        [SerializeField] private RectTransform _boundsRoot;
        [SerializeField] private OffscreenThreatIndicatorView[] _indicators =
            new OffscreenThreatIndicatorView[MaximumIndicatorCount];
        [SerializeField] private float _horizontalPadding = 96f;
        [SerializeField] private float _verticalPadding = 108f;

        private int _visibleCount;

        public int VisibleCount => _visibleCount;

        public int IndicatorCount => _indicators != null
            ? Mathf.Min(_indicators.Length, MaximumIndicatorCount)
            : 0;

        public Rect Bounds => _boundsRoot != null ? _boundsRoot.rect : default;

        public float HorizontalPadding => _horizontalPadding;

        public float VerticalPadding => _verticalPadding;

        public OffscreenThreatIndicatorView GetIndicator(int index)
        {
            if (_indicators == null || index < 0 || index >= _indicators.Length)
            {
                return null;
            }

            return _indicators[index];
        }

        public void Present(
            UnityEngine.Camera gameCamera,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            int viewerTeamValue)
        {
            if (gameCamera == null || snapshots == null || _boundsRoot == null ||
                _indicators == null || _indicators.Length == 0)
            {
                Clear();
                return;
            }

            _visibleCount = 0;
            int count = Mathf.Min(snapshotCount, snapshots.Length);
            PresentPass(gameCamera, snapshots, count, viewerTeamValue, true);
            PresentPass(gameCamera, snapshots, count, viewerTeamValue, false);
            for (int index = _visibleCount; index < _indicators.Length; index++)
            {
                _indicators[index]?.Hide();
            }
        }

        public void Clear()
        {
            _visibleCount = 0;
            if (_indicators == null)
            {
                return;
            }

            for (int index = 0; index < _indicators.Length; index++)
            {
                _indicators[index]?.Hide();
            }
        }

        public bool TryResolveEdgePosition(
            UnityEngine.Camera gameCamera,
            Vector3 worldPosition,
            out Vector2 anchoredPosition,
            out Vector2 direction)
        {
            anchoredPosition = default;
            direction = default;
            if (gameCamera == null || _boundsRoot == null)
            {
                return false;
            }

            Vector3 viewport = gameCamera.WorldToViewportPoint(worldPosition);
            bool inFront = viewport.z > 0f;
            if (inFront &&
                viewport.x >= 0f && viewport.x <= 1f &&
                viewport.y >= 0f && viewport.y <= 1f)
            {
                return false;
            }

            Vector2 viewportDirection = new Vector2(
                viewport.x - 0.5f,
                viewport.y - 0.5f);
            if (!inFront)
            {
                viewportDirection = -viewportDirection;
            }

            if (viewportDirection.sqrMagnitude < MinimumDirectionMagnitude)
            {
                viewportDirection = Vector2.up;
            }

            direction = viewportDirection.normalized;
            Rect bounds = _boundsRoot.rect;
            float availableHalfWidth = Mathf.Max(
                0f,
                (bounds.width * 0.5f) - _horizontalPadding);
            float availableHalfHeight = Mathf.Max(
                0f,
                (bounds.height * 0.5f) - _verticalPadding);
            if (availableHalfWidth <= 0f || availableHalfHeight <= 0f)
            {
                return false;
            }

            float horizontalDistance = Mathf.Abs(direction.x) > 0.0001f
                ? availableHalfWidth / Mathf.Abs(direction.x)
                : float.MaxValue;
            float verticalDistance = Mathf.Abs(direction.y) > 0.0001f
                ? availableHalfHeight / Mathf.Abs(direction.y)
                : float.MaxValue;
            float edgeDistance = Mathf.Min(horizontalDistance, verticalDistance);
            anchoredPosition = bounds.center + (direction * edgeDistance);
            return true;
        }

        private void PresentPass(
            UnityEngine.Camera gameCamera,
            CombatantSnapshot[] snapshots,
            int snapshotCount,
            int viewerTeamValue,
            bool castingPass)
        {
            int indicatorLimit = Mathf.Min(_indicators.Length, MaximumIndicatorCount);
            for (int index = 0;
                 index < snapshotCount && _visibleCount < indicatorLimit;
                 index++)
            {
                CombatantSnapshot snapshot = snapshots[index];
                bool isCasting = snapshot.CastingAbility != AbilityId.None;
                if (isCasting != castingPass ||
                    !snapshot.IsAlive ||
                    snapshot.Team.Value == viewerTeamValue ||
                    snapshot.HasStatus(StatusId.Stealthed) ||
                    snapshot.HasAuraTag(AuraTag.Stealth))
                {
                    continue;
                }

                Vector3 worldPosition = new Vector3(
                    snapshot.Position.X,
                    snapshot.Position.Y + CombatantWorldHeight,
                    snapshot.Position.Z);
                if (!TryResolveEdgePosition(
                        gameCamera,
                        worldPosition,
                        out Vector2 anchoredPosition,
                        out Vector2 direction))
                {
                    continue;
                }

                OffscreenThreatIndicatorView indicator = _indicators[_visibleCount];
                if (indicator == null)
                {
                    continue;
                }

                indicator.Present(
                    snapshot.Class,
                    snapshot.Health,
                    snapshot.MaxHealth,
                    isCasting,
                    anchoredPosition,
                    direction);
                _visibleCount++;
            }
        }
    }
}
