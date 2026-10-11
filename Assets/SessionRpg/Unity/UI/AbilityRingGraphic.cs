using UnityEngine;
using UnityEngine.UI;

namespace SessionRpg.Unity.UI
{
    [DisallowMultipleComponent]
    public sealed class AbilityRingGraphic : MaskableGraphic
    {
        private const int MinimumSegments = 12;
        private const int MaximumSegments = 96;

        [SerializeField, Min(0.5f)] private float _thickness = 3f;
        [SerializeField, Range(MinimumSegments, MaximumSegments)]
        private int _segments = 48;

        public float Thickness => _thickness;

        public int Segments => _segments;

        public void Configure(float thickness, int segments = 48)
        {
            float safeThickness = Mathf.Max(0.5f, thickness);
            int safeSegments = Mathf.Clamp(
                segments,
                MinimumSegments,
                MaximumSegments);
            if (Mathf.Approximately(_thickness, safeThickness) &&
                _segments == safeSegments)
            {
                return;
            }

            _thickness = safeThickness;
            _segments = safeSegments;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();
            Rect rect = GetPixelAdjustedRect();
            float outerRadiusX = rect.width * 0.5f;
            float outerRadiusY = rect.height * 0.5f;
            float maximumThickness = Mathf.Min(outerRadiusX, outerRadiusY);
            if (maximumThickness <= 0f)
            {
                return;
            }

            float thickness = Mathf.Clamp(_thickness, 0.5f, maximumThickness);
            float innerRadiusX = Mathf.Max(0f, outerRadiusX - thickness);
            float innerRadiusY = Mathf.Max(0f, outerRadiusY - thickness);
            int segmentCount = Mathf.Clamp(
                _segments,
                MinimumSegments,
                MaximumSegments);
            UIVertex vertex = UIVertex.simpleVert;
            vertex.color = color;
            Vector2 center = rect.center;
            for (int segment = 0; segment < segmentCount; segment++)
            {
                float radians = segment / (float)segmentCount * Mathf.PI * 2f;
                float cosine = Mathf.Cos(radians);
                float sine = Mathf.Sin(radians);

                vertex.position = new Vector3(
                    center.x + cosine * outerRadiusX,
                    center.y + sine * outerRadiusY);
                vertex.uv0 = new Vector4(
                    0.5f + cosine * 0.5f,
                    0.5f + sine * 0.5f,
                    0f,
                    0f);
                vertexHelper.AddVert(vertex);

                vertex.position = new Vector3(
                    center.x + cosine * innerRadiusX,
                    center.y + sine * innerRadiusY);
                vertex.uv0 = new Vector4(
                    0.5f + cosine * innerRadiusX / rect.width,
                    0.5f + sine * innerRadiusY / rect.height,
                    0f,
                    0f);
                vertexHelper.AddVert(vertex);
            }

            for (int segment = 0; segment < segmentCount; segment++)
            {
                int nextSegment = (segment + 1) % segmentCount;
                int outer = segment * 2;
                int inner = outer + 1;
                int nextOuter = nextSegment * 2;
                int nextInner = nextOuter + 1;
                vertexHelper.AddTriangle(outer, nextOuter, nextInner);
                vertexHelper.AddTriangle(outer, nextInner, inner);
            }
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            _thickness = Mathf.Max(0.5f, _thickness);
            _segments = Mathf.Clamp(
                _segments,
                MinimumSegments,
                MaximumSegments);
            SetVerticesDirty();
        }
#endif
    }
}
