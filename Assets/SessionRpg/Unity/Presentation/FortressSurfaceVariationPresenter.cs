using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    /// <summary>
    /// Applies a deterministic, per-renderer stone variation through one cached
    /// property block. Shared materials remain unchanged and URP Lit continues
    /// to use one texture transform for its aligned albedo and normal sampling.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FortressSurfaceVariationPresenter : MonoBehaviour
    {
        private static readonly int BaseMapTransformId =
            Shader.PropertyToID("_BaseMap_ST");
        private static readonly int BaseColorId =
            Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        [SerializeField] private Renderer _surface;
        [SerializeField, Min(0)] private int _stoneMaterialIndex;
        [SerializeField] private Vector2 _textureOffset;
        [SerializeField] private Color _surfaceTint = Color.white;

        private MaterialPropertyBlock _propertyBlock;

        public Renderer Surface => _surface;

        public int StoneMaterialIndex => _stoneMaterialIndex;

        public Vector2 TextureOffset => _textureOffset;

        public Color SurfaceTint => _surfaceTint;

        private void OnEnable()
        {
            ApplyVariation();
        }

        public void Configure(
            Renderer surface,
            int stoneMaterialIndex,
            Vector2 textureOffset,
            Color surfaceTint)
        {
            _surface = surface;
            _stoneMaterialIndex = stoneMaterialIndex;
            _textureOffset = textureOffset;
            _surfaceTint = surfaceTint;
            ApplyVariation();
        }

        private void ApplyVariation()
        {
            if (_surface == null || _stoneMaterialIndex < 0)
            {
                return;
            }

            Material[] sharedMaterials = _surface.sharedMaterials;
            if (_stoneMaterialIndex >= sharedMaterials.Length)
            {
                return;
            }

            Material stoneMaterial = sharedMaterials[_stoneMaterialIndex];
            if (stoneMaterial == null || !stoneMaterial.HasProperty("_BaseMap"))
            {
                return;
            }

            Vector2 textureScale = stoneMaterial.GetTextureScale("_BaseMap");
            Vector2 textureOffset =
                stoneMaterial.GetTextureOffset("_BaseMap") + _textureOffset;

            _propertyBlock ??= new MaterialPropertyBlock();
            _surface.GetPropertyBlock(_propertyBlock, _stoneMaterialIndex);
            _propertyBlock.SetVector(
                BaseMapTransformId,
                new Vector4(
                    textureScale.x,
                    textureScale.y,
                    textureOffset.x,
                    textureOffset.y));
            _propertyBlock.SetColor(BaseColorId, _surfaceTint);
            _propertyBlock.SetColor(ColorId, _surfaceTint);
            _surface.SetPropertyBlock(_propertyBlock, _stoneMaterialIndex);
        }
    }
}
