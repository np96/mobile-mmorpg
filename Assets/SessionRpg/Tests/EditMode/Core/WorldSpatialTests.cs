using System;
using NUnit.Framework;

namespace SessionRpg.Core.Tests
{
    public sealed class WorldSpatialTests
    {
        private static readonly WorldId World = new WorldId(1u);
        private static readonly ZoneId Zone = new ZoneId(7u);

        [Test]
        public void StableIds_ReserveZeroForTheInvalidDefault()
        {
            WorldId invalidWorld = default;
            ZoneId invalidZone = default;
            WorldId validWorld = new WorldId(uint.MaxValue);
            ZoneId validZone = new ZoneId(uint.MaxValue);

            Assert.That(invalidWorld.Value, Is.Zero);
            Assert.That(invalidWorld.IsValid, Is.False);
            Assert.That(invalidZone.Value, Is.Zero);
            Assert.That(invalidZone.IsValid, Is.False);
            Assert.That(validWorld.IsValid, Is.True);
            Assert.That(validZone.IsValid, Is.True);
            Assert.That(new WorldId(5u), Is.EqualTo(new WorldId(5u)));
            Assert.That(new ZoneId(5u), Is.Not.EqualTo(new ZoneId(6u)));
            Assert.That(new WorldId(4u).CompareTo(new WorldId(5u)), Is.LessThan(0));
            Assert.That(new ZoneId(6u).CompareTo(new ZoneId(5u)), Is.GreaterThan(0));
        }

        [Test]
        public void ChunkKey_CombinesWorldZoneAndSignedCoordinateIdentity()
        {
            var coordinate = new WorldChunkCoordinate(-4, 9);
            var key = new WorldChunkKey(World, Zone, coordinate);

            Assert.That(default(WorldChunkKey).IsValid, Is.False);
            Assert.That(key.IsValid, Is.True);
            Assert.That(key.World, Is.EqualTo(World));
            Assert.That(key.Zone, Is.EqualTo(Zone));
            Assert.That(key.Coordinate, Is.EqualTo(coordinate));
            Assert.That(
                key,
                Is.Not.EqualTo(new WorldChunkKey(
                    new WorldId(2u),
                    Zone,
                    coordinate)));
            Assert.That(
                key,
                Is.Not.EqualTo(new WorldChunkKey(
                    World,
                    new ZoneId(8u),
                    coordinate)));
            Assert.That(
                key,
                Is.Not.EqualTo(new WorldChunkKey(
                    World,
                    Zone,
                    coordinate.Offset(1, 0))));
        }

        [TestCase(0d, 0, 0d)]
        [TestCase(63.875d, 0, 63.875d)]
        [TestCase(64d, 1, 0d)]
        [TestCase(64.125d, 1, 0.125d)]
        [TestCase(-0.125d, -1, 63.875d)]
        [TestCase(-64d, -1, 0d)]
        [TestCase(-64.125d, -2, 63.875d)]
        public void FromMeters_NormalizesPositiveAndNegativeBoundaries(
            double meters,
            int expectedChunk,
            double expectedLocal)
        {
            WorldPosition position = WorldPosition.FromMeters(
                World,
                Zone,
                meters,
                3.5d,
                meters);

            Assert.That(position.IsValid, Is.True);
            Assert.That(position.Chunk.X, Is.EqualTo(expectedChunk));
            Assert.That(position.Chunk.Z, Is.EqualTo(expectedChunk));
            Assert.That(position.LocalX, Is.EqualTo(expectedLocal).Within(0.000001d));
            Assert.That(position.LocalZ, Is.EqualTo(expectedLocal).Within(0.000001d));
            Assert.That(position.Height, Is.EqualTo(3.5d));
            Assert.That(position.WorldX, Is.EqualTo(meters).Within(0.000001d));
            Assert.That(position.WorldZ, Is.EqualTo(meters).Within(0.000001d));
        }

        [Test]
        public void ChunkLocalConstruction_CarriesAcrossMultipleSignedChunks()
        {
            var source = new WorldChunkKey(
                World,
                Zone,
                new WorldChunkCoordinate(-2, 3));

            var position = new WorldPosition(source, 130.5d, 2d, -0.25d);

            Assert.That(
                position.Chunk,
                Is.EqualTo(new WorldChunkCoordinate(0, 2)));
            Assert.That(position.LocalX, Is.EqualTo(2.5d));
            Assert.That(position.LocalZ, Is.EqualTo(63.75d));
            Assert.That(position.WorldX, Is.EqualTo(2.5d));
            Assert.That(position.WorldZ, Is.EqualTo(191.75d));
        }

        [Test]
        public void Translate_PreservesIdsAndCanonicalRepresentation()
        {
            WorldPosition source = WorldPosition.FromMeters(
                World,
                Zone,
                -0.5d,
                10d,
                63.75d);

            WorldPosition translated = source.Translate(1d, -2d, 0.5d);

            Assert.That(translated.World, Is.EqualTo(World));
            Assert.That(translated.Zone, Is.EqualTo(Zone));
            Assert.That(
                translated.Chunk,
                Is.EqualTo(new WorldChunkCoordinate(0, 1)));
            Assert.That(translated.LocalX, Is.EqualTo(0.5d));
            Assert.That(translated.LocalZ, Is.EqualTo(0.25d));
            Assert.That(translated.Height, Is.EqualTo(8d));
            Assert.That(translated.WorldX, Is.EqualTo(0.5d));
            Assert.That(translated.WorldZ, Is.EqualTo(64.25d));
        }

        [Test]
        public void Coordinates_UseOverflowSafeDistanceAndCheckedOffset()
        {
            var minimum = new WorldChunkCoordinate(int.MinValue, int.MinValue);
            var maximum = new WorldChunkCoordinate(int.MaxValue, int.MaxValue);

            Assert.That(
                minimum.ChebyshevDistanceTo(maximum),
                Is.EqualTo(4294967295L));
            Assert.That(
                minimum.ManhattanDistanceTo(maximum),
                Is.EqualTo(8589934590L));
            Assert.That(
                minimum.IsWithinChebyshevDistance(maximum, int.MaxValue),
                Is.False);
            Assert.Throws<OverflowException>(() => maximum.Offset(1, 0));
        }

        [Test]
        public void InvalidAndNonfinitePositions_AreRejectedAtTheBoundary()
        {
            Assert.That(default(WorldPosition).IsValid, Is.False);
            Assert.Throws<ArgumentException>(() =>
                WorldPosition.FromMeters(default, Zone, 0d, 0d, 0d));
            Assert.Throws<ArgumentException>(() =>
                WorldPosition.FromMeters(World, default, 0d, 0d, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WorldPosition.FromMeters(World, Zone, double.NaN, 0d, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                WorldPosition.FromMeters(
                    World,
                    Zone,
                    0d,
                    double.PositiveInfinity,
                    0d));
            Assert.Throws<OverflowException>(() =>
                WorldPosition.FromMeters(
                    World,
                    Zone,
                    (int.MaxValue + 1d) * WorldPosition.ChunkSizeMeters,
                    0d,
                    0d));
            Assert.Throws<InvalidOperationException>(() =>
                default(WorldPosition).Translate(1d, 0d, 0d));
        }

        [Test]
        public void NormalizationAndTranslation_AfterWarmupAllocateNoManagedMemory()
        {
            WorldPosition position = default;
            double checksum = 0d;
            for (int index = 0; index < 256; index++)
            {
                position = WorldPosition.FromMeters(
                    World,
                    Zone,
                    -128.25d + index,
                    index * 0.1d,
                    96.5d - index);
                position = position.Translate(0.75d, 0.25d, -0.5d);
                checksum += position.LocalX + position.LocalZ;
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 4096; index++)
            {
                position = WorldPosition.FromMeters(
                    World,
                    Zone,
                    -128.25d + index,
                    index * 0.1d,
                    96.5d - index);
                position = position.Translate(0.75d, 0.25d, -0.5d);
                checksum += position.LocalX + position.LocalZ;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero);
            Assert.That(checksum, Is.GreaterThan(0d));
        }

        private static void ForceCollectionAndPrimeCounter()
        {
            GC.GetAllocatedBytesForCurrentThread();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            GC.GetAllocatedBytesForCurrentThread();
        }
    }
}
