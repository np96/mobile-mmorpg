using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    /// <summary>
    /// Keeps the authored ember submesh readable without creating a material
    /// instance or another light. The property block is applied only to the
    /// ember slot, so the shared fortress stone remains physically lit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FortressFireAnchorPresenter : MonoBehaviour
    {
        [SerializeField] private MeshRenderer _surface;
        [SerializeField, Min(0)] private int _emberMaterialIndex = 1;
        [SerializeField] private Color _lowColor =
            new Color(1f, 0.28f, 0.018f, 1f);
        [SerializeField] private Color _highColor =
            new Color(1f, 0.7f, 0.09f, 1f);
        [SerializeField, Min(0f)] private float _emissionMultiplier = 2.4f;
        [SerializeField, Min(0.01f)] private float _pulseSpeed = 0.92f;
        [SerializeField] private float _phaseOffset;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private MaterialPropertyBlock _propertyBlock;
        private int _materialCount;

        public MeshRenderer Surface => _surface;

        public int EmberMaterialIndex => _emberMaterialIndex;

        public float EmissionMultiplier => _emissionMultiplier;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
            _materialCount = _surface != null
                ? _surface.sharedMaterials.Length
                : 0;
            Apply(Time.time);
        }

        private void Update()
        {
            Apply(Time.time);
        }

        private void Apply(float time)
        {
            if (_surface == null ||
                _emberMaterialIndex < 0 ||
                _emberMaterialIndex >= _materialCount)
            {
                return;
            }

            _propertyBlock ??= new MaterialPropertyBlock();
            float pulse = 0.5f +
                (Mathf.Sin((time * _pulseSpeed) + _phaseOffset) * 0.5f);
            pulse = Mathf.SmoothStep(0f, 1f, pulse);
            Color color = Color.Lerp(_lowColor, _highColor, pulse);
            Color emissionColor = new Color(
                color.r * _emissionMultiplier,
                color.g * _emissionMultiplier,
                color.b * _emissionMultiplier,
                color.a);
            _surface.GetPropertyBlock(_propertyBlock, _emberMaterialIndex);
            _propertyBlock.SetColor(BaseColorId, color);
            _propertyBlock.SetColor(ColorId, color);
            _propertyBlock.SetColor(EmissionColorId, emissionColor);
            _surface.SetPropertyBlock(_propertyBlock, _emberMaterialIndex);
        }
    }
}
