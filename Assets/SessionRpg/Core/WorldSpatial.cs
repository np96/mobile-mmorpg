using System;

namespace SessionRpg.Core
{
    public readonly struct WorldId : IEquatable<WorldId>, IComparable<WorldId>
    {
        public WorldId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool IsValid => Value != 0u;

        public int CompareTo(WorldId other) => Value.CompareTo(other.Value);

        public bool Equals(WorldId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is WorldId other && Equals(other);

        public override int GetHashCode() => unchecked((int)Value);

        public override string ToString() => Value.ToString();

        public static bool operator ==(WorldId left, WorldId right) => left.Equals(right);

        public static bool operator !=(WorldId left, WorldId right) => !left.Equals(right);
    }

    public readonly struct ZoneId : IEquatable<ZoneId>, IComparable<ZoneId>
    {
        public ZoneId(uint value)
        {
            Value = value;
        }

        public uint Value { get; }

        public bool IsValid => Value != 0u;

        public int CompareTo(ZoneId other) => Value.CompareTo(other.Value);

        public bool Equals(ZoneId other) => Value == other.Value;

        public override bool Equals(object obj) => obj is ZoneId other && Equals(other);

        public override int GetHashCode() => unchecked((int)Value);

        public override string ToString() => Value.ToString();

        public static bool operator ==(ZoneId left, ZoneId right) => left.Equals(right);

        public static bool operator !=(ZoneId left, ZoneId right) => !left.Equals(right);
    }

    public readonly struct WorldChunkCoordinate :
        IEquatable<WorldChunkCoordinate>,
        IComparable<WorldChunkCoordinate>
    {
        public WorldChunkCoordinate(int x, int z)
        {
            X = x;
            Z = z;
        }

        public int X { get; }

        public int Z { get; }

        public long ChebyshevDistanceTo(WorldChunkCoordinate other)
        {
            long xDistance = Math.Abs((long)X - other.X);
            long zDistance = Math.Abs((long)Z - other.Z);
            return Math.Max(xDistance, zDistance);
        }

        public long ManhattanDistanceTo(WorldChunkCoordinate other) =>
            Math.Abs((long)X - other.X) + Math.Abs((long)Z - other.Z);

        public bool IsWithinChebyshevDistance(
            WorldChunkCoordinate other,
            int radius)
        {
            if (radius < 0)
            {
                return false;
            }

            return ChebyshevDistanceTo(other) <= radius;
        }

        public WorldChunkCoordinate Offset(int x, int z) =>
            new WorldChunkCoordinate(checked(X + x), checked(Z + z));

        public int CompareTo(WorldChunkCoordinate other)
        {
            int xComparison = X.CompareTo(other.X);
            return xComparison != 0 ? xComparison : Z.CompareTo(other.Z);
        }

        public bool Equals(WorldChunkCoordinate other) =>
            X == other.X && Z == other.Z;

        public override bool Equals(object obj) =>
            obj is WorldChunkCoordinate other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                return (X * 397) ^ Z;
            }
        }

        public override string ToString() => $"({X},{Z})";

        public static bool operator ==(
            WorldChunkCoordinate left,
            WorldChunkCoordinate right) => left.Equals(right);

        public static bool operator !=(
            WorldChunkCoordinate left,
            WorldChunkCoordinate right) => !left.Equals(right);
    }

    public readonly struct WorldChunkKey :
        IEquatable<WorldChunkKey>,
        IComparable<WorldChunkKey>
    {
        public WorldChunkKey(
            WorldId world,
            ZoneId zone,
            WorldChunkCoordinate coordinate)
        {
            World = world;
            Zone = zone;
            Coordinate = coordinate;
        }

        public WorldId World { get; }

        public ZoneId Zone { get; }

        public WorldChunkCoordinate Coordinate { get; }

        public bool IsValid => World.IsValid && Zone.IsValid;

        public int CompareTo(WorldChunkKey other)
        {
            int worldComparison = World.CompareTo(other.World);
            if (worldComparison != 0)
            {
                return worldComparison;
            }

            int zoneComparison = Zone.CompareTo(other.Zone);
            return zoneComparison != 0
                ? zoneComparison
                : Coordinate.CompareTo(other.Coordinate);
        }

        public bool Equals(WorldChunkKey other) =>
            World == other.World &&
            Zone == other.Zone &&
            Coordinate == other.Coordinate;

        public override bool Equals(object obj) =>
            obj is WorldChunkKey other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = World.GetHashCode();
                hash = (hash * 397) ^ Zone.GetHashCode();
                return (hash * 397) ^ Coordinate.GetHashCode();
            }
        }

        public override string ToString() =>
            $"{World.Value}:{Zone.Value}:{Coordinate.X}:{Coordinate.Z}";

        public static bool operator ==(WorldChunkKey left, WorldChunkKey right) =>
            left.Equals(right);

        public static bool operator !=(WorldChunkKey left, WorldChunkKey right) =>
            !left.Equals(right);
    }

    /// <summary>
    /// A zone-space position represented as a stable chunk key and normalized
    /// chunk-local meters. Planar local coordinates are always in [0, 64).
    /// </summary>
    public readonly struct WorldPosition : IEquatable<WorldPosition>
    {
        public const double ChunkSizeMeters = 64d;

        public WorldPosition(
            WorldChunkKey chunkKey,
            double localX,
            double height,
            double localZ)
        {
            if (!chunkKey.IsValid)
            {
                throw new ArgumentException(
                    "A world position requires valid world and zone IDs.",
                    nameof(chunkKey));
            }

            EnsureFinite(height, nameof(height));
            NormalizeAxis(
                chunkKey.Coordinate.X,
                localX,
                nameof(localX),
                out int chunkX,
                out double normalizedX);
            NormalizeAxis(
                chunkKey.Coordinate.Z,
                localZ,
                nameof(localZ),
                out int chunkZ,
                out double normalizedZ);

            ChunkKey = new WorldChunkKey(
                chunkKey.World,
                chunkKey.Zone,
                new WorldChunkCoordinate(chunkX, chunkZ));
            LocalX = normalizedX;
            Height = height;
            LocalZ = normalizedZ;
        }

        public WorldChunkKey ChunkKey { get; }

        public WorldId World => ChunkKey.World;

        public ZoneId Zone => ChunkKey.Zone;

        public WorldChunkCoordinate Chunk => ChunkKey.Coordinate;

        public double LocalX { get; }

        public double Height { get; }

        public double LocalZ { get; }

        public bool IsValid => ChunkKey.IsValid;

        public double WorldX => (Chunk.X * ChunkSizeMeters) + LocalX;

        public double WorldZ => (Chunk.Z * ChunkSizeMeters) + LocalZ;

        public static WorldPosition FromMeters(
            WorldId world,
            ZoneId zone,
            double worldX,
            double height,
            double worldZ)
        {
            return new WorldPosition(
                new WorldChunkKey(world, zone, default),
                worldX,
                height,
                worldZ);
        }

        public WorldPosition Translate(
            double deltaX,
            double deltaHeight,
            double deltaZ)
        {
            if (!IsValid)
            {
                throw new InvalidOperationException(
                    "The default world position cannot be translated.");
            }

            EnsureFinite(deltaX, nameof(deltaX));
            EnsureFinite(deltaHeight, nameof(deltaHeight));
            EnsureFinite(deltaZ, nameof(deltaZ));
            return new WorldPosition(
                ChunkKey,
                LocalX + deltaX,
                Height + deltaHeight,
                LocalZ + deltaZ);
        }

        public bool Equals(WorldPosition other) =>
            ChunkKey == other.ChunkKey &&
            LocalX.Equals(other.LocalX) &&
            Height.Equals(other.Height) &&
            LocalZ.Equals(other.LocalZ);

        public override bool Equals(object obj) =>
            obj is WorldPosition other && Equals(other);

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = ChunkKey.GetHashCode();
                hash = (hash * 397) ^ LocalX.GetHashCode();
                hash = (hash * 397) ^ Height.GetHashCode();
                return (hash * 397) ^ LocalZ.GetHashCode();
            }
        }

        public override string ToString() =>
            $"{ChunkKey}@({LocalX},{Height},{LocalZ})";

        public static bool operator ==(WorldPosition left, WorldPosition right) =>
            left.Equals(right);

        public static bool operator !=(WorldPosition left, WorldPosition right) =>
            !left.Equals(right);

        private static void NormalizeAxis(
            int sourceChunk,
            double sourceLocal,
            string parameterName,
            out int normalizedChunk,
            out double normalizedLocal)
        {
            EnsureFinite(sourceLocal, parameterName);
            double chunkOffsetValue = Math.Floor(sourceLocal / ChunkSizeMeters);
            if (chunkOffsetValue < int.MinValue ||
                chunkOffsetValue > int.MaxValue)
            {
                throw new OverflowException(
                    "The world position exceeds the supported chunk range.");
            }

            long combinedChunk = sourceChunk + (long)chunkOffsetValue;
            if (combinedChunk < int.MinValue || combinedChunk > int.MaxValue)
            {
                throw new OverflowException(
                    "The world position exceeds the supported chunk range.");
            }

            normalizedChunk = (int)combinedChunk;
            normalizedLocal = sourceLocal -
                (chunkOffsetValue * ChunkSizeMeters);

            // Defend the canonical representation against floating-point edge
            // rounding without changing the mathematical floor convention.
            if (normalizedLocal >= ChunkSizeMeters)
            {
                if (normalizedChunk == int.MaxValue)
                {
                    throw new OverflowException(
                        "The world position exceeds the supported chunk range.");
                }

                normalizedChunk++;
                normalizedLocal = 0d;
            }
            else if (normalizedLocal < 0d)
            {
                if (normalizedChunk == int.MinValue)
                {
                    throw new OverflowException(
                        "The world position exceeds the supported chunk range.");
                }

                normalizedChunk--;
                normalizedLocal += ChunkSizeMeters;
            }
            else if (normalizedLocal == 0d)
            {
                normalizedLocal = 0d;
            }
        }

        private static void EnsureFinite(double value, string parameterName)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                throw new ArgumentOutOfRangeException(
                    parameterName,
                    "World coordinates must be finite.");
            }
        }
    }
}
