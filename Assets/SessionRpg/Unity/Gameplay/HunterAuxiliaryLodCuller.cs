using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    [DisallowMultipleComponent]
    public sealed class HunterAuxiliaryLodCuller : MonoBehaviour
    {
        private const float MinimumDivisor = 0.0001f;

        [SerializeField] private LODGroup _lodGroup;
        [SerializeField] private Renderer[] _auxiliaryRenderers = System.Array.Empty<Renderer>();
        [SerializeField, Range(0f, 1f)] private float _cullScreenRelativeHeight = 0.02f;

        private UnityEngine.Camera _gameplayCamera;

        public LODGroup TargetLodGroup => _lodGroup;

        public int AuxiliaryRendererCount => _auxiliaryRenderers.Length;

        public float CullScreenRelativeHeight => _cullScreenRelativeHeight;

        public bool AreAuxiliaryRenderersCulled
        {
            get
            {
                if (_auxiliaryRenderers.Length == 0)
                {
                    return false;
                }

                for (int index = 0; index < _auxiliaryRenderers.Length; index++)
                {
                    Renderer renderer = _auxiliaryRenderers[index];
                    if (renderer == null || !renderer.forceRenderingOff)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        private void LateUpdate()
        {
            if (_gameplayCamera == null)
            {
                _gameplayCamera = UnityEngine.Camera.main;
            }

            RefreshForCamera(_gameplayCamera);
        }

        private void OnDisable()
        {
            SetAuxiliaryCulled(false);
        }

        public bool ContainsAuxiliaryRenderer(Renderer renderer)
        {
            if (renderer == null)
            {
                return false;
            }

            for (int index = 0; index < _auxiliaryRenderers.Length; index++)
            {
                if (_auxiliaryRenderers[index] == renderer)
                {
                    return true;
                }
            }

            return false;
        }

        public void RefreshForCamera(UnityEngine.Camera camera)
        {
            if (_lodGroup == null || camera == null)
            {
                SetAuxiliaryCulled(false);
                return;
            }

            float screenRelativeHeight = CalculateScreenRelativeHeight(camera);
            SetAuxiliaryCulled(
                screenRelativeHeight < _cullScreenRelativeHeight);
        }

        public float CalculateScreenRelativeHeight(UnityEngine.Camera camera)
        {
            if (_lodGroup == null || camera == null)
            {
                return float.PositiveInfinity;
            }

            Vector3 scale = _lodGroup.transform.lossyScale;
            float largestAxis = Mathf.Max(
                Mathf.Abs(scale.x),
                Mathf.Max(Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            float worldSpaceSize = _lodGroup.size * largestAxis;
            float lodBias = Mathf.Max(QualitySettings.lodBias, MinimumDivisor);
            if (camera.orthographic)
            {
                return worldSpaceSize * lodBias /
                    (2f * Mathf.Max(camera.orthographicSize, MinimumDivisor));
            }

            Vector3 referencePoint = _lodGroup.transform.TransformPoint(
                _lodGroup.localReferencePoint);
            float distance = Mathf.Max(
                Vector3.Distance(camera.transform.position, referencePoint),
                MinimumDivisor);
            float halfAngle = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return worldSpaceSize * lodBias /
                (2f * distance * Mathf.Max(halfAngle, MinimumDivisor));
        }

        private void SetAuxiliaryCulled(bool culled)
        {
            for (int index = 0; index < _auxiliaryRenderers.Length; index++)
            {
                Renderer renderer = _auxiliaryRenderers[index];
                if (renderer != null && renderer.forceRenderingOff != culled)
                {
                    renderer.forceRenderingOff = culled;
                }
            }
        }
    }
}
