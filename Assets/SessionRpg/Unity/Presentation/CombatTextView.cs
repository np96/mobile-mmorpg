using UnityEngine;

namespace SessionRpg.Unity.Presentation
{
    [DisallowMultipleComponent]
    public sealed class CombatTextView : MonoBehaviour
    {
        [SerializeField] private TextMesh _textMesh;
        [SerializeField, Min(0.01f)] private float _riseDistance = 0.85f;

        private Color _color;
        private Vector3 _startPosition;
        private float _duration;
        private float _remaining;

        public bool IsPlaying => _remaining > 0f;

        public void Play(string value, Color color, Vector3 position, float duration)
        {
            _color = color;
            _startPosition = position;
            _duration = Mathf.Max(0.05f, duration);
            _remaining = _duration;
            transform.position = position;
            if (_textMesh != null)
            {
                _textMesh.text = value;
                _textMesh.color = color;
            }

            gameObject.SetActive(true);
        }

        public bool Tick(float deltaTime, UnityEngine.Camera camera)
        {
            if (_remaining <= 0f)
            {
                return false;
            }

            _remaining -= deltaTime;
            float normalized = 1f - Mathf.Clamp01(_remaining / _duration);
            transform.position = _startPosition + Vector3.up * (_riseDistance * normalized);
            if (camera != null)
            {
                Vector3 direction = transform.position - camera.transform.position;
                if (direction.sqrMagnitude > 0.0001f)
                {
                    transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                }
            }

            if (_textMesh != null)
            {
                Color color = _color;
                color.a = 1f - normalized;
                _textMesh.color = color;
            }

            if (_remaining > 0f)
            {
                return true;
            }

            gameObject.SetActive(false);
            return false;
        }
    }
}
