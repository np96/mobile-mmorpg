using UnityEngine;

namespace SessionRpg.Unity.Input
{
    /// <summary>
    /// A render-frame movement sample. Ability gestures are read separately from
    /// IHumanInputSource's bounded queue.
    /// </summary>
    public readonly struct HumanInputFrame
    {
        public HumanInputFrame(Vector2 movement)
        {
            Movement = Vector2.ClampMagnitude(movement, 1f);
        }

        public Vector2 Movement { get; }

        public Vector3 ToCameraRelativeMovement(Transform cameraTransform)
        {
            if (cameraTransform == null)
            {
                return new Vector3(Movement.x, 0f, Movement.y);
            }

            Vector3 forward = cameraTransform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;

            Vector3 right = cameraTransform.right;
            right.y = 0f;
            right = right.sqrMagnitude > 0.0001f ? right.normalized : Vector3.right;

            return Vector3.ClampMagnitude((right * Movement.x) + (forward * Movement.y), 1f);
        }
    }

    public interface IHumanInputSource
    {
        HumanInputFrame CurrentFrame { get; }

        int DroppedGestureCount { get; }

        bool TryDequeueAbilityGesture(out AbilityGesture gesture);

        void ClearTransientInput();
    }
}
