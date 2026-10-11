using UnityEngine;

namespace SessionRpg.Unity.Gameplay
{
    /// <summary>
    /// Keeps the evaluation Hunter's rigid bow vertical and aligns its nocked
    /// arrow between the animated hands. This is presentation-only: it follows
    /// Humanoid output after IK and never feeds simulation state back to Core.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    public sealed class HunterTrialWeaponPose : MonoBehaviour
    {
        private const float ArrowCentreOffset = 0.34f;

        [SerializeField] private Transform _orientationFrame;
        [SerializeField] private Transform _leftHand;
        [SerializeField] private Transform _rightHand;
        [SerializeField] private Transform _bow;
        [SerializeField] private Transform _nockedArrow;

        public Transform OrientationFrame => _orientationFrame;

        public Transform LeftHand => _leftHand;

        public Transform RightHand => _rightHand;

        public Transform Bow => _bow;

        public Transform NockedArrow => _nockedArrow;

        public void Bind(
            Transform orientationFrame,
            Transform leftHand,
            Transform rightHand,
            Transform bow,
            Transform nockedArrow)
        {
            _orientationFrame = orientationFrame;
            _leftHand = leftHand;
            _rightHand = rightHand;
            _bow = bow;
            _nockedArrow = nockedArrow;
            ApplyPose();
        }

        private void OnEnable()
        {
            ApplyPose();
        }

        private void LateUpdate()
        {
            ApplyPose();
        }

        private void ApplyPose()
        {
            if (_orientationFrame == null ||
                _leftHand == null ||
                _rightHand == null ||
                _bow == null ||
                _nockedArrow == null)
            {
                return;
            }

            Vector3 up = _orientationFrame.up;
            Vector3 drawDirection = _leftHand.position - _rightHand.position;
            if (drawDirection.sqrMagnitude <= 0.0001f)
            {
                drawDirection = _orientationFrame.forward;
            }

            drawDirection.Normalize();
            Vector3 bowFacing = _nockedArrow.gameObject.activeSelf
                ? Vector3.ProjectOnPlane(drawDirection, up)
                : Vector3.ProjectOnPlane(_orientationFrame.forward, up);
            if (bowFacing.sqrMagnitude <= 0.0001f)
            {
                bowFacing = _orientationFrame.forward;
            }

            bowFacing.Normalize();
            _bow.SetPositionAndRotation(
                _leftHand.position,
                Quaternion.LookRotation(up, bowFacing));
            _nockedArrow.SetPositionAndRotation(
                _rightHand.position + (drawDirection * ArrowCentreOffset),
                Quaternion.LookRotation(drawDirection, up));
        }
    }
}
