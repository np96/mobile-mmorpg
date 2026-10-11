using SessionRpg.Core;

namespace SessionRpg.Unity.Input
{
    public enum AbilityFlickDirection
    {
        None,
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// Allocation-free state consumed by the radial selection overlay. A pressed
    /// pointer may move between cardinal sectors until release. IsLocked means
    /// the pointer is outside the center dead zone; an unbound sector remains
    /// correctable and is canceled only if the pointer is released there.
    /// </summary>
    public readonly struct AbilityFlickSelection
    {
        public AbilityFlickSelection(
            AbilityId abilityId,
            AbilityFlickDirection direction,
            bool isPressed,
            bool isLocked,
            bool hasDirectionalBinding,
            bool isCanceled)
        {
            AbilityId = abilityId;
            Direction = direction;
            IsPressed = isPressed;
            IsLocked = isLocked;
            HasDirectionalBinding = hasDirectionalBinding;
            IsCanceled = isCanceled;
        }

        public AbilityId AbilityId { get; }

        public AbilityFlickDirection Direction { get; }

        public bool IsPressed { get; }

        public bool IsLocked { get; }

        public bool HasDirectionalBinding { get; }

        public bool IsCanceled { get; }
    }
}
