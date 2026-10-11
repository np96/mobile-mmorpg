using System;

namespace SessionRpg.Unity.Arenas
{
    /// <summary>
    /// Stable content identity used by catalogs, capture manifests and future
    /// additive-scene loading. IDs are lowercase ASCII and never derive from a
    /// GameObject, prefab or scene name.
    /// </summary>
    [Serializable]
    public struct ArenaContentId : IEquatable<ArenaContentId>
    {
        public const int MaximumLength = 64;

        [UnityEngine.SerializeField] private string _value;

        public ArenaContentId(string value)
        {
            _value = value ?? string.Empty;
        }

        public string Value => _value ?? string.Empty;

        public bool IsValid => IsWellFormed(Value);

        public static ArenaContentId None => default;

        public static bool TryCreate(string value, out ArenaContentId contentId)
        {
            contentId = new ArenaContentId(value);
            return contentId.IsValid;
        }

        public static bool IsWellFormed(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.Length > MaximumLength ||
                !IsAsciiLetter(value[0]))
            {
                return false;
            }

            for (int index = 0; index < value.Length; index++)
            {
                char character = value[index];
                bool isAllowed = IsAsciiLetter(character) ||
                    (character >= '0' && character <= '9') ||
                    character == '_';
                if (!isAllowed)
                {
                    return false;
                }
            }

            return true;
        }

        public bool Equals(ArenaContentId other)
        {
            return string.Equals(Value, other.Value, StringComparison.Ordinal);
        }

        public override bool Equals(object value)
        {
            return value is ArenaContentId other && Equals(other);
        }

        public override int GetHashCode()
        {
            return StringComparer.Ordinal.GetHashCode(Value);
        }

        public override string ToString()
        {
            return Value;
        }

        public static bool operator ==(ArenaContentId left, ArenaContentId right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ArenaContentId left, ArenaContentId right)
        {
            return !left.Equals(right);
        }

        private static bool IsAsciiLetter(char value)
        {
            return value >= 'a' && value <= 'z';
        }
    }
}
