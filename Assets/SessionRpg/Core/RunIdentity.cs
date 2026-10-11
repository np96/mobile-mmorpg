using System;

namespace SessionRpg.Core
{
    /// <summary>
    /// Stable cross-layer mode identity. Values are explicit and intentionally
    /// independent from the legacy Application.GameModeId ordinal values.
    /// </summary>
    public enum PlayableModeId
    {
        None = 0,
        Arena = 1,
        HeroSiege = 2,
        Dungeon = 3,
        PveLeveling = 4,
    }

    public enum RunKind
    {
        None = 0,
        Match = 1,
        Training = 2,
        Sequential = 3,
        WaveDefense = 4,
        PersistentRegion = 5,
    }

    public enum EncounterKind
    {
        None = 0,
        Combat = 1,
        Boss = 2,
    }

    public readonly struct RunId : IEquatable<RunId>, IComparable<RunId>
    {
        public RunId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool IsValid => Value != 0u;

        public int CompareTo(RunId other) => Value.CompareTo(other.Value);

        public bool Equals(RunId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is RunId other && Equals(other);

        public override int GetHashCode() => unchecked((int)Value);

        public override string ToString() => Value.ToString();

        public static bool operator ==(RunId left, RunId right) => left.Equals(right);

        public static bool operator !=(RunId left, RunId right) => !left.Equals(right);
    }

    public readonly struct EncounterId : IEquatable<EncounterId>, IComparable<EncounterId>
    {
        public EncounterId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool IsValid => Value != 0u;

        public int CompareTo(EncounterId other) => Value.CompareTo(other.Value);

        public bool Equals(EncounterId other) => Value == other.Value;

        public override bool Equals(object obj) =>
            obj is EncounterId other && Equals(other);

        public override int GetHashCode() => unchecked((int)Value);

        public override string ToString() => Value.ToString();

        public static bool operator ==(EncounterId left, EncounterId right) =>
            left.Equals(right);

        public static bool operator !=(EncounterId left, EncounterId right) =>
            !left.Equals(right);
    }

    public readonly struct SpawnId : IEquatable<SpawnId>, IComparable<SpawnId>
    {
        public SpawnId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool IsValid => Value != 0u;

        public int CompareTo(SpawnId other) => Value.CompareTo(other.Value);

        public bool Equals(SpawnId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is SpawnId other && Equals(other);

        public override int GetHashCode() => unchecked((int)Value);

        public override string ToString() => Value.ToString();

        public static bool operator ==(SpawnId left, SpawnId right) => left.Equals(right);

        public static bool operator !=(SpawnId left, SpawnId right) => !left.Equals(right);
    }

    public readonly struct ObjectiveId :
        IEquatable<ObjectiveId>,
        IComparable<ObjectiveId>
    {
        public ObjectiveId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool IsValid => Value != 0u;

        public int CompareTo(ObjectiveId other) => Value.CompareTo(other.Value);

        public bool Equals(ObjectiveId other) => Value == other.Value;

        public override bool Equals(object obj) =>
            obj is ObjectiveId other && Equals(other);

        public override int GetHashCode() => unchecked((int)Value);

        public override string ToString() => Value.ToString();

        public static bool operator ==(ObjectiveId left, ObjectiveId right) =>
            left.Equals(right);

        public static bool operator !=(ObjectiveId left, ObjectiveId right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// Mutable deterministic objective health. Default is an invalid objective;
    /// valid instances always start at full health and clamp applied damage.
    /// </summary>
    public struct ObjectiveState
    {
        private float _health;

        public ObjectiveState(ObjectiveId id, float maximumHealth)
        {
            if (!id.IsValid)
            {
                throw new ArgumentException(
                    "An objective requires a valid stable ID.",
                    nameof(id));
            }

            if (!IsFinitePositive(maximumHealth))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumHealth),
                    maximumHealth,
                    "Objective maximum health must be finite and positive.");
            }

            Id = id;
            MaximumHealth = maximumHealth;
            _health = maximumHealth;
        }

        public ObjectiveId Id { get; }

        public float MaximumHealth { get; }

        public float Health => _health;

        public bool IsValid => Id.IsValid && IsFinitePositive(MaximumHealth);

        public bool IsAlive => IsValid && _health > 0f;

        public bool IsDefeated => IsValid && _health <= 0f;

        public float HealthFraction => IsValid ? _health / MaximumHealth : 0f;

        public float ApplyDamage(float requestedDamage)
        {
            if (!IsAlive || !IsFinitePositive(requestedDamage))
            {
                return 0f;
            }

            float appliedDamage = Math.Min(_health, requestedDamage);
            _health -= appliedDamage;
            if (_health < 0f)
            {
                _health = 0f;
            }

            return appliedDamage;
        }

        private static bool IsFinitePositive(float value) =>
            value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
