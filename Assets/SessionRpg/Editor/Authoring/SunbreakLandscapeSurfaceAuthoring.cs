using System;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Authoring
{
    /// <summary>Measured rock/grass on authored distant slopes, not a heightmap or collision source.</summary>
    public static class SunbreakLandscapeSurfaceAuthoring
    {
        public const string MaterialPath = "Assets/SessionRpg/Art/Materials/Sunbreak_VistaTerrain.mat";
        public const float MeshUvScale = 0.15f;

        public static Material Configure(Material rock, Material meadow)
        {
            if (rock == null || meadow == null) { throw new ArgumentNullException("Measured landscape sources are required."); }
            Material result = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool create = result == null;
            if (create) { result = new Material(rock); }
            else { result.CopyPropertiesFromMaterial(rock); }
            result.name = "Sunbreak_VistaTerrain";
            // Vista alone uses continuous world-space triplanar projection.
            // These are cycles per meter, independent of retained mesh UVs.
            result.SetTextureScale("_BaseMap", Vector2.one / 1.5f);
            result.SetTextureOffset("_BaseMap", Vector2.zero);
            result.SetColor("_BaseColor", new Color(0.55f, 0.59f, 0.58f));
            result.SetTexture("_LayerMap", meadow.GetTexture("_BaseMap"));
            result.SetTexture("_LayerNormal", meadow.GetTexture("_BumpMap"));
            result.SetTexture("_LayerRoughness", meadow.GetTexture("_RoughnessMap"));
            result.SetTextureScale("_LayerMap", Vector2.one / 2f);
            result.SetTextureOffset("_LayerMap", Vector2.zero);
            result.SetTexture("_BlendMap", null);
            result.SetColor("_LayerColor", new Color(0.34f, 0.47f, 0.29f));
            result.SetFloat("_LayerSaturation", 0.8f);
            result.SetFloat("_SecondLayer", 1f);
            result.SetFloat("_VertexLayer", 1f);
            result.EnableKeyword("_SECOND_LAYER");
            result.EnableKeyword("_VERTEX_LAYER");
            result.SetFloat("_BumpScale", 0.75f);
            result.SetFloat("_Smoothness", 0.55f);
            if (create) { AssetDatabase.CreateAsset(result, MaterialPath); }
            EditorUtility.SetDirty(result);
            return result;
        }

        public static Color VertexColor(Vector3 point, Vector3 normal)
        {
            // Broad geology is authored once, never animated. Vegetation recedes
            // from steep exposures and high crests; broken ecotones prevent a
            // single horizontal green stripe around the mountain ring.
            float broad = Mathf.PerlinNoise(point.x * 0.017f + 41f, point.z * 0.017f + 73f);
            float middle = Mathf.PerlinNoise(point.x * 0.041f + 13f, point.z * 0.041f + 97f);
            float altitude = 1f - Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(7f + broad * 13f, 23f + broad * 13f, point.y));
            float slope = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.54f, 0.86f, normal.normalized.y));
            float patches = Mathf.SmoothStep(0f, 1f,
                Mathf.InverseLerp(0.30f, 0.65f, broad * 0.7f + middle * 0.3f));
            float coverage = altitude * slope * Mathf.Lerp(0.08f, 0.98f, patches);
            // G is restrained albedo variation, not fabricated occlusion.
            return new Color(Mathf.Clamp01(coverage), Mathf.Lerp(0.62f, 0.95f, middle), 0f, 1f);
        }
    }
}
