using SessionRpg.Core;
using UnityEngine;

namespace SessionRpg.Unity.Input
{
    public enum AbilityGesturePhase
    {
        Pressed,
        // Primary action released without leaving the movement dead zone.
        Tapped,
        // Explicitly bound alternate released as a valid quick cardinal flick.
        Flicked,
        // Interrupted or intentionally rejected gesture; never commits an action.
        Canceled,
    }

    /// <summary>
    /// Device-neutral UI intent keyed by the authoritative Core AbilityId.
    /// </summary>
    public readonly struct AbilityGesture
    {
        public AbilityGesture(
            AbilityId abilityId,
            AbilityGesturePhase phase,
            int pointerId,
            Vector2 screenPosition,
            Vector2 displacement,
            float duration)
        {
            AbilityId = abilityId;
            Phase = phase;
            PointerId = pointerId;
            ScreenPosition = screenPosition;
            Displacement = displacement;
            Duration = duration;
        }

        public AbilityId AbilityId { get; }

        public AbilityGesturePhase Phase { get; }

        public int PointerId { get; }

        public Vector2 ScreenPosition { get; }

        public Vector2 Displacement { get; }

        public float Duration { get; }
    }
}
