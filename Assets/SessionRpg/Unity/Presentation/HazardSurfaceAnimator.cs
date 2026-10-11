using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class HazardSurfaceAnimator : MonoBehaviour
    {
        [SerializeField] private Renderer _surface;
        [SerializeField] private Renderer _seams;
        [SerializeField] private Color _lowColor = new Color(0.82f, 0.42f, 0.2f, 1f);
        [SerializeField] private Color _highColor = new Color(1f, 0.58f, 0.26f, 1f);
        [SerializeField] private Color _lowSurfaceEmissionColor =
            new Color(0.55f, 0.32f, 0.12f, 1f);
        [SerializeField] private Color _highSurfaceEmissionColor =
            new Color(2.8f, 1.3f, 0.38f, 1f);
        [SerializeField] private Color _lowSeamColor = new Color(0.003f, 0f, 0f, 1f);
        [SerializeField] private Color _highSeamColor = new Color(0.012f, 0.001f, 0f, 1f);
        [SerializeField, Min(0f)] private float _pulseSpeed = 0.22f;
        [SerializeField, Min(0.01f)] private float _surfaceTiling = 1.7f;
        [SerializeField] private Vector2 _surfaceScrollSpeed = new Vector2(-0.0045f, 0.003f);
        [SerializeField] private Vector2 _seamScrollSpeed = new Vector2(0.012f, -0.008f);
        [SerializeField] private float _phaseOffset;

        private MaterialPropertyBlock _surfacePropertyBlock;
        private MaterialPropertyBlock _seamPropertyBlock;

        private void Awake()
        {
            EnsurePropertyBlocks();
        }

        private void Update()
        {
            if (_surface == null && _seams == null)
            {
                return;
            }

            EnsurePropertyBlocks();
            float time = Time.time + _phaseOffset;
            if (_surface != null)
            {
                float surfaceBlend = 0.5f +
                    (Mathf.Sin(time * _pulseSpeed) * 0.5f);
                Color surfaceColor = Color.Lerp(_lowColor, _highColor, surfaceBlend);
                _surface.GetPropertyBlock(_surfacePropertyBlock);
                _surfacePropertyBlock.SetColor("_BaseColor", surfaceColor);
                _surfacePropertyBlock.SetColor("_Color", surfaceColor);
                Color surfaceEmission = Color.Lerp(
                    _lowSurfaceEmissionColor,
                    _highSurfaceEmissionColor,
                    surfaceBlend);
                _surfacePropertyBlock.SetColor(
                    "_EmissionColor",
                    surfaceEmission * Mathf.Lerp(1.05f, 1.55f, surfaceBlend));
                Vector2 surfaceOffset = _surfaceScrollSpeed * time;
                Vector4 surfaceTextureTransform = new Vector4(
                    _surfaceTiling,
                    _surfaceTiling,
                    surfaceOffset.x,
                    surfaceOffset.y);
                _surfacePropertyBlock.SetVector("_BaseMap_ST", surfaceTextureTransform);
                _surfacePropertyBlock.SetVector("_MainTex_ST", surfaceTextureTransform);
                _surfacePropertyBlock.SetVector("_EmissionMap_ST", surfaceTextureTransform);
                _surface.SetPropertyBlock(_surfacePropertyBlock);
            }

            if (_seams != null)
            {
                float seamBlend = 0.5f +
                    (Mathf.Sin((time * (_pulseSpeed * 1.63f)) + 1.7f) * 0.5f);
                seamBlend = Mathf.SmoothStep(0f, 1f, seamBlend);
                Color seamColor = Color.Lerp(_lowSeamColor, _highSeamColor, seamBlend);
                Vector2 offset = _seamScrollSpeed * time;
                Vector4 textureTransform = new Vector4(1f, 1f, offset.x, offset.y);
                _seams.GetPropertyBlock(_seamPropertyBlock);
                _seamPropertyBlock.SetColor("_BaseColor", seamColor);
                _seamPropertyBlock.SetColor("_Color", seamColor);
                _seamPropertyBlock.SetColor(
                    "_EmissionColor",
                    seamColor * Mathf.Lerp(1.05f, 1.7f, seamBlend));
                _seamPropertyBlock.SetVector("_BaseMap_ST", textureTransform);
                _seamPropertyBlock.SetVector("_MainTex_ST", textureTransform);
                _seams.SetPropertyBlock(_seamPropertyBlock);
            }
        }

        private void EnsurePropertyBlocks()
        {
            _surfacePropertyBlock ??= new MaterialPropertyBlock();
            _seamPropertyBlock ??= new MaterialPropertyBlock();
        }
    }
}
