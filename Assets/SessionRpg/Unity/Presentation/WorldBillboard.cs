using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class WorldBillboard : MonoBehaviour
    {
        [SerializeField] private bool _yawOnly = true;

        private void LateUpdate()
        {
            UnityEngine.Camera camera = UnityEngine.Camera.main;
            if (camera == null)
            {
                return;
            }

            Vector3 direction = transform.position - camera.transform.position;
            if (_yawOnly)
            {
                direction.y = 0f;
            }

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            }
        }
    }
}
