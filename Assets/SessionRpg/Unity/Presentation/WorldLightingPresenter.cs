using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace SessionRpg.Unity.Presentation
{
    /// <summary>
    /// Daylight for streamed worlds, with isolated Sunbreak shadow/reflection
    /// response. Reuses the Bootstrap key without changing quality or exposure,
    /// and restores its entry state for other locations.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class WorldLightingPresenter : MonoBehaviour
    {
        public const float WorldKeyIntensity = 2.25f;
        public const float WorldShadowStrength = 0.58f;
        public const float WorldFogDensity = 0.0035f;
        public const float SunbreakKeyIntensity = 2.05f;
        public const float SunbreakShadowStrength = 0.78f;
        public const float SunbreakReflectionIntensity = 0.72f;
        public const float SunbreakFogDensity = 0.0035f;
        public static readonly Color SunbreakFogColor = new Color(0.55f, 0.66f, 0.74f, 1f);

        public static readonly Color WorldSkyColor = new Color(0.52f, 0.62f, 0.74f, 1f);
        public static readonly Color WorldEquatorColor = new Color(0.61f, 0.64f, 0.68f, 1f);
        public static readonly Color WorldGroundColor = new Color(0.38f, 0.36f, 0.33f, 1f);
        public static readonly Color WorldKeyColor = new Color(1f, 0.93f, 0.82f, 1f);
        public static readonly Color WorldFogColor = new Color(0.34f, 0.39f, 0.44f, 1f);

        [SerializeField] private Light _keyLight;
        [SerializeField] private Material _sunbreakSky;
        [SerializeField] private GameObject _sunbreakVista;

        private LightingState _baseline;
        private Scene _baselineScene;
        private bool _worldLightingEnabled;
        private bool _sunbreakLightingEnabled;
        private Cubemap _sunbreakReflection;

        public Light KeyLight => _keyLight;
        public bool HasAuthoredBindings => _keyLight != null &&
            _keyLight.type == LightType.Directional;
        public bool IsWorldLightingEnabled => _worldLightingEnabled;
        public bool HasSunbreakVistaBindings => _sunbreakSky != null && _sunbreakVista != null;
        public GameObject SunbreakVista => _sunbreakVista;

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Restore();
        }

        private void OnDestroy()
        {
            Restore();
            if (_sunbreakReflection != null)
            {
                Destroy(_sunbreakReflection);
                _sunbreakReflection = null;
            }
        }

        public void SetWorldLighting(bool enabled, bool sunbreak = false)
        {
            if (!enabled)
            {
                Restore();
                return;
            }

            if (_worldLightingEnabled && _sunbreakLightingEnabled == sunbreak)
            {
                return;
            }

            if (!HasAuthoredBindings)
            {
                throw new InvalidOperationException(
                    "World lighting requires the authored Bootstrap directional key.");
            }

            if (!_worldLightingEnabled)
            {
                _baseline = new LightingState(_keyLight);
                _baselineScene = SceneManager.GetActiveScene();
            }
            _worldLightingEnabled = true;
            _sunbreakLightingEnabled = sunbreak;
            ApplyWorldLighting();
        }

        public void Restore()
        {
            if (_sunbreakVista != null) { _sunbreakVista.SetActive(false); }
            if (!_worldLightingEnabled)
            {
                return;
            }

            // Teardown must not copy the old Bootstrap's environment into a new
            // scene that has already become active.
            bool restoreEnvironment = SceneManager.GetActiveScene() == _baselineScene;
            _baseline.Restore(_keyLight, restoreEnvironment);
            _worldLightingEnabled = false;
            _sunbreakLightingEnabled = false;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (_worldLightingEnabled && mode == LoadSceneMode.Additive &&
                SceneManager.GetActiveScene() == _baselineScene)
            {
                // Reassert indirect light once per asynchronous chunk load, not
                // every frame. Scene/probe initialization must not leave models
                // with only direct key light on their forward-facing surfaces.
                ApplyWorldLighting();
            }
        }

        private void ApplyWorldLighting()
        {
            if (_sunbreakVista != null) { _sunbreakVista.SetActive(_sunbreakLightingEnabled); }
            _keyLight.transform.rotation = Quaternion.Euler(52f, -25f, 0f);
            _keyLight.color = WorldKeyColor;
            _keyLight.intensity = _sunbreakLightingEnabled ? SunbreakKeyIntensity : WorldKeyIntensity;
            _keyLight.shadowStrength = _sunbreakLightingEnabled ? SunbreakShadowStrength : WorldShadowStrength;
            RenderSettings.sun = _keyLight;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = WorldSkyColor;
            RenderSettings.ambientEquatorColor = WorldEquatorColor;
            RenderSettings.ambientGroundColor = WorldGroundColor;
            RenderSettings.ambientIntensity = 1f;

            // Runtime color changes alone do not supply baked character probes.
            // Bounded linear SH fill keeps faces, cloth, and shadowed masonry
            // readable without another light, shadow map, or exposure increase.
            // The equator is intentionally bright: these sRGB authoring colors
            // become lower linear radiance, and the camera mostly sees fronts.
            SphericalHarmonicsL2 ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(WorldEquatorColor.linear);
            ambient.AddDirectionalLight(Vector3.up, WorldSkyColor.linear, 0.5f);
            ambient.AddDirectionalLight(Vector3.down, WorldGroundColor.linear, 0.18f);
            RenderSettings.ambientProbe = ambient;

            if (_sunbreakLightingEnabled)
            {
                if (_sunbreakSky != null) { RenderSettings.skybox = _sunbreakSky; }
                if (_sunbreakReflection == null) { _sunbreakReflection = CreateSunbreakReflection(); }
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Custom;
                RenderSettings.customReflection = _sunbreakReflection;
                RenderSettings.reflectionIntensity = SunbreakReflectionIntensity;
                RenderSettings.reflectionBounces = 1;
            }
            else
            {
                // Training keeps its exact prior indirect specular response,
                // including a direct transition from the separate PvE region.
                _baseline.RestoreReflection();
                _baseline.RestoreSky();
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = _sunbreakLightingEnabled ? SunbreakFogColor : WorldFogColor;
            RenderSettings.fogDensity = _sunbreakLightingEnabled ? SunbreakFogDensity : WorldFogDensity;
        }

        private static Cubemap CreateSunbreakReflection()
        {
            // A bounded analytic daylight environment, not a captured HDRI or
            // realtime reflection probe. Allocate once per presenter lifetime;
            // mode switches and additive scene loads reuse this same resource.
            const int size = 32;
            var reflection = new Cubemap(size, TextureFormat.RGBAHalf, true)
            {
                name = "SunbreakDaylightReflection",
                hideFlags = HideFlags.DontSave,
                filterMode = FilterMode.Trilinear,
                wrapMode = TextureWrapMode.Clamp
            };
            var pixels = new Color[size * size];
            for (int face = 0; face < 6; face++)
            {
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size * 2f - 1f;
                        float v = (y + 0.5f) / size * 2f - 1f;
                        float inverseLength = 1f / Mathf.Sqrt(1f + u * u + v * v);
                        float vertical = face == (int)CubemapFace.PositiveY ? inverseLength :
                            face == (int)CubemapFace.NegativeY ? -inverseLength : -v * inverseLength;
                        Color earth = new Color(0.105f, 0.092f, 0.067f, 1f);
                        Color horizon = new Color(0.40f, 0.43f, 0.45f, 1f);
                        Color sky = new Color(0.22f, 0.34f, 0.49f, 1f);
                        pixels[y * size + x] = vertical < 0f
                            ? Color.Lerp(horizon, earth, Mathf.SmoothStep(0f, 1f, -vertical))
                            : Color.Lerp(horizon, sky, Mathf.SmoothStep(0f, 1f, vertical));
                    }
                }
                reflection.SetPixels(pixels, (CubemapFace)face);
            }
            reflection.Apply(true, true);
            return reflection;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(Light keyLight, Material sunbreakSky = null, GameObject sunbreakVista = null)
        {
            if (keyLight == null || keyLight.type != LightType.Directional)
            {
                throw new ArgumentException(
                    "The world look must reuse a directional key.", nameof(keyLight));
            }

            _keyLight = keyLight;
            _sunbreakSky = sunbreakSky;
            _sunbreakVista = sunbreakVista;
            if (_sunbreakVista != null) { _sunbreakVista.SetActive(false); }
        }
#endif

        private readonly struct LightingState
        {
            private readonly Quaternion _rotation;
            private readonly Color _keyColor;
            private readonly float _keyIntensity;
            private readonly float _shadowStrength;
            private readonly Light _sun;
            private readonly AmbientMode _ambientMode;
            private readonly Color _sky;
            private readonly Color _equator;
            private readonly Color _ground;
            private readonly float _ambientIntensity;
            private readonly SphericalHarmonicsL2 _ambientProbe;
            private readonly bool _fog;
            private readonly FogMode _fogMode;
            private readonly Color _fogColor;
            private readonly float _fogDensity;
            private readonly DefaultReflectionMode _reflectionMode;
            private readonly Cubemap _reflection;
            private readonly float _reflectionIntensity;
            private readonly int _reflectionBounces;
            private readonly Material _skybox;

            public LightingState(Light key)
            {
                _rotation = key.transform.rotation;
                _keyColor = key.color;
                _keyIntensity = key.intensity;
                _shadowStrength = key.shadowStrength;
                _sun = RenderSettings.sun;
                _ambientMode = RenderSettings.ambientMode;
                _sky = RenderSettings.ambientSkyColor;
                _equator = RenderSettings.ambientEquatorColor;
                _ground = RenderSettings.ambientGroundColor;
                _ambientIntensity = RenderSettings.ambientIntensity;
                _ambientProbe = RenderSettings.ambientProbe;
                _fog = RenderSettings.fog;
                _fogMode = RenderSettings.fogMode;
                _fogColor = RenderSettings.fogColor;
                _fogDensity = RenderSettings.fogDensity;
                _reflectionMode = RenderSettings.defaultReflectionMode;
                _reflection = RenderSettings.customReflection;
                _reflectionIntensity = RenderSettings.reflectionIntensity;
                _reflectionBounces = RenderSettings.reflectionBounces;
                _skybox = RenderSettings.skybox;
            }

            public void RestoreReflection()
            {
                RenderSettings.defaultReflectionMode = _reflectionMode;
                RenderSettings.customReflection = _reflection;
                RenderSettings.reflectionIntensity = _reflectionIntensity;
                RenderSettings.reflectionBounces = _reflectionBounces;
            }

            public void RestoreSky() { RenderSettings.skybox = _skybox; }

            public void Restore(Light key, bool restoreEnvironment)
            {
                if (key != null)
                {
                    key.transform.rotation = _rotation;
                    key.color = _keyColor;
                    key.intensity = _keyIntensity;
                    key.shadowStrength = _shadowStrength;
                }

                if (!restoreEnvironment)
                {
                    return;
                }

                RenderSettings.sun = _sun;
                RenderSettings.ambientMode = _ambientMode;
                RenderSettings.ambientSkyColor = _sky;
                RenderSettings.ambientEquatorColor = _equator;
                RenderSettings.ambientGroundColor = _ground;
                RenderSettings.ambientIntensity = _ambientIntensity;
                RenderSettings.ambientProbe = _ambientProbe;
                RenderSettings.fog = _fog;
                RenderSettings.fogMode = _fogMode;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogDensity = _fogDensity;
                RestoreReflection();
                RestoreSky();
            }
        }
    }
}
