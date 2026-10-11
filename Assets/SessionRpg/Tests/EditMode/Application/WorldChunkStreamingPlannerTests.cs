using System;
using NUnit.Framework;
using SessionRpg.Core;

namespace SessionRpg.Application.Tests
{
    public sealed class WorldChunkStreamingPlannerTests
    {
        private static readonly WorldId World = new WorldId(1u);
        private static readonly ZoneId Zone = new ZoneId(1u);

        [Test]
        public void Constructor_RequiresBoundedHysteresisConfiguration()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldChunkStreamingPlanner(-1, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldChunkStreamingPlanner(2, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                new WorldChunkStreamingPlanner(0, 0, 0));

            var planner = new WorldChunkStreamingPlanner(1, 2, 9);
            Assert.That(planner.LoadRadius, Is.EqualTo(1));
            Assert.That(planner.RetainRadius, Is.EqualTo(2));
            Assert.That(planner.MaximumLoadedChunkCount, Is.EqualTo(9));
        }

        [Test]
        public void Plan_SelectsNearestLoadsInStableOrderIndependentOfCatalogOrder()
        {
            var planner = new WorldChunkStreamingPlanner(1, 2, 4);
            WorldChunkKey focus = Key(0, 0);
            WorldChunkKey[] available =
            {
                Key(1, 1),
                Key(0, 1),
                Key(-1, -1),
                Key(1, 0),
                Key(0, 0),
                Key(-1, 0),
                Key(0, -1),
                Key(-1, 1),
                Key(1, -1),
            };

            WorldChunkStreamingPlanSummary summary = planner.Plan(
                focus,
                available,
                available.Length,
                Array.Empty<WorldChunkKey>(),
                0);

            Assert.That(summary.LoadCount, Is.EqualTo(4));
            Assert.That(summary.UnloadCount, Is.Zero);
            Assert.That(summary.RetainedLoadedCount, Is.Zero);
            Assert.That(summary.ProjectedLoadedCount, Is.EqualTo(4));
            Assert.That(planner.GetLoadRequest(0), Is.EqualTo(Key(0, 0)));
            Assert.That(planner.GetLoadRequest(1), Is.EqualTo(Key(-1, 0)));
            Assert.That(planner.GetLoadRequest(2), Is.EqualTo(Key(0, -1)));
            Assert.That(planner.GetLoadRequest(3), Is.EqualTo(Key(0, 1)));
        }

        [Test]
        public void Plan_RetainsHysteresisRingAndUnloadsBeyondIt()
        {
            var planner = new WorldChunkStreamingPlanner(1, 2, 4);
            WorldChunkKey retained = Key(2, 0);
            WorldChunkKey tooFar = Key(3, 0);
            WorldChunkKey focus = Key(0, 0);
            WorldChunkKey[] available = { tooFar, focus, retained };
            WorldChunkKey[] loaded = { tooFar, retained };

            WorldChunkStreamingPlanSummary summary = planner.Plan(
                focus,
                available,
                available.Length,
                loaded,
                loaded.Length);

            Assert.That(summary.LoadCount, Is.EqualTo(1));
            Assert.That(planner.GetLoadRequest(0), Is.EqualTo(focus));
            Assert.That(summary.UnloadCount, Is.EqualTo(1));
            Assert.That(planner.GetUnloadRequest(0), Is.EqualTo(tooFar));
            Assert.That(summary.RetainedLoadedCount, Is.EqualTo(1));
            Assert.That(summary.ProjectedLoadedCount, Is.EqualTo(2));
        }

        [Test]
        public void Plan_UnloadsResidentChunkRemovedFromAuthoritativeCatalog()
        {
            var planner = new WorldChunkStreamingPlanner(0, 1, 3);
            WorldChunkKey focus = Key(0, 0);
            WorldChunkKey removed = Key(1, 0);
            WorldChunkKey[] available = { focus };
            WorldChunkKey[] loaded = { focus, removed };

            WorldChunkStreamingPlanSummary summary = planner.Plan(
                focus,
                available,
                available.Length,
                loaded,
                loaded.Length);

            Assert.That(summary.LoadCount, Is.Zero);
            Assert.That(summary.UnloadCount, Is.EqualTo(1));
            Assert.That(planner.GetUnloadRequest(0), Is.EqualTo(removed));
            Assert.That(summary.ProjectedLoadedCount, Is.EqualTo(1));
        }

        [Test]
        public void Plan_ReplacesFarRetainedChunksWhenCloserLoadsNeedTheCap()
        {
            var planner = new WorldChunkStreamingPlanner(1, 2, 2);
            WorldChunkKey focus = Key(0, 0);
            WorldChunkKey northOuter = Key(0, 2);
            WorldChunkKey eastOuter = Key(2, 0);
            WorldChunkKey eastInner = Key(1, 0);
            WorldChunkKey[] available =
            {
                northOuter,
                eastInner,
                eastOuter,
                focus,
            };
            WorldChunkKey[] loaded = { eastOuter, northOuter };

            WorldChunkStreamingPlanSummary summary = planner.Plan(
                focus,
                available,
                available.Length,
                loaded,
                loaded.Length);

            Assert.That(summary.LoadCount, Is.EqualTo(2));
            Assert.That(planner.GetLoadRequest(0), Is.EqualTo(focus));
            Assert.That(planner.GetLoadRequest(1), Is.EqualTo(eastInner));
            Assert.That(summary.UnloadCount, Is.EqualTo(2));
            Assert.That(planner.GetUnloadRequest(0), Is.EqualTo(northOuter));
            Assert.That(planner.GetUnloadRequest(1), Is.EqualTo(eastOuter));
            Assert.That(summary.RetainedLoadedCount, Is.Zero);
            Assert.That(summary.ProjectedLoadedCount, Is.EqualTo(2));
        }

        [Test]
        public void Plan_PrefersResidentChunkWhenMissingCandidateHasEqualPriority()
        {
            var planner = new WorldChunkStreamingPlanner(1, 1, 1);
            WorldChunkKey focus = Key(0, 0);
            WorldChunkKey resident = Key(-1, 0);
            WorldChunkKey equalCandidate = Key(1, 0);
            WorldChunkKey[] available = { equalCandidate, resident };
            WorldChunkKey[] loaded = { resident };

            WorldChunkStreamingPlanSummary summary = planner.Plan(
                focus,
                available,
                available.Length,
                loaded,
                loaded.Length);

            Assert.That(summary.LoadCount, Is.Zero);
            Assert.That(summary.UnloadCount, Is.Zero);
            Assert.That(summary.RetainedLoadedCount, Is.EqualTo(1));
            Assert.That(summary.ProjectedLoadedCount, Is.EqualTo(1));
        }

        [Test]
        public void Plan_TransitionsWorldAndZoneWithoutMixingResidency()
        {
            var planner = new WorldChunkStreamingPlanner(1, 2, 3);
            var nextZone = new ZoneId(2u);
            var focus = new WorldChunkKey(
                World,
                nextZone,
                new WorldChunkCoordinate(0, 0));
            WorldChunkKey nextEast = new WorldChunkKey(
                World,
                nextZone,
                new WorldChunkCoordinate(1, 0));
            WorldChunkKey[] available =
            {
                Key(0, 0),
                nextEast,
                focus,
                new WorldChunkKey(
                    new WorldId(2u),
                    nextZone,
                    new WorldChunkCoordinate(0, 0)),
            };
            WorldChunkKey[] loaded = { Key(-1, 0), Key(0, 0) };

            WorldChunkStreamingPlanSummary summary = planner.Plan(
                focus,
                available,
                available.Length,
                loaded,
                loaded.Length);

            Assert.That(summary.UnloadCount, Is.EqualTo(2));
            Assert.That(summary.LoadCount, Is.EqualTo(2));
            Assert.That(planner.GetLoadRequest(0), Is.EqualTo(focus));
            Assert.That(planner.GetLoadRequest(1), Is.EqualTo(nextEast));
            Assert.That(summary.ProjectedLoadedCount, Is.EqualTo(2));
        }

        [Test]
        public void Plan_RejectsInvalidCountsKeysAndDuplicates()
        {
            var planner = new WorldChunkStreamingPlanner(1, 2, 2);
            WorldChunkKey focus = Key(0, 0);
            WorldChunkKey[] one = { focus };
            WorldChunkKey[] duplicates = { focus, focus };
            WorldChunkKey[] invalid = { default };

            Assert.Throws<ArgumentException>(() => planner.Plan(
                default,
                one,
                1,
                Array.Empty<WorldChunkKey>(),
                0));
            Assert.Throws<ArgumentNullException>(() => planner.Plan(
                focus,
                null,
                0,
                Array.Empty<WorldChunkKey>(),
                0));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(
                focus,
                one,
                2,
                Array.Empty<WorldChunkKey>(),
                0));
            Assert.Throws<ArgumentOutOfRangeException>(() => planner.Plan(
                focus,
                one,
                1,
                new WorldChunkKey[3],
                3));
            Assert.Throws<ArgumentException>(() => planner.Plan(
                focus,
                one,
                one.Length,
                duplicates,
                duplicates.Length));
            Assert.Throws<ArgumentException>(() => planner.Plan(
                focus,
                invalid,
                invalid.Length,
                Array.Empty<WorldChunkKey>(),
                0));
        }

        [Test]
        public void RequestAccess_IsBoundedByTheLatestPlan()
        {
            var planner = new WorldChunkStreamingPlanner(0, 0, 1);
            WorldChunkKey focus = Key(0, 0);
            WorldChunkKey[] available = { focus };
            planner.Plan(
                focus,
                available,
                1,
                Array.Empty<WorldChunkKey>(),
                0);

            Assert.That(planner.GetLoadRequest(0), Is.EqualTo(focus));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                planner.GetLoadRequest(1));
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                planner.GetUnloadRequest(0));
        }

        [Test]
        public void Plan_AfterWarmupAllocatesNoManagedMemory()
        {
            var planner = new WorldChunkStreamingPlanner(1, 2, 4);
            WorldChunkKey focus = Key(0, 0);
            WorldChunkKey[] available =
            {
                Key(-1, -1), Key(-1, 0), Key(-1, 1),
                Key(0, -1), Key(0, 0), Key(0, 1),
                Key(1, -1), Key(1, 0), Key(1, 1),
            };
            WorldChunkKey[] loaded = { Key(-2, 0), Key(2, 0) };
            int checksum = 0;

            for (int index = 0; index < 256; index++)
            {
                WorldChunkStreamingPlanSummary summary = planner.Plan(
                    focus,
                    available,
                    available.Length,
                    loaded,
                    loaded.Length);
                checksum += summary.LoadCount + summary.UnloadCount;
            }

            ForceCollectionAndPrimeCounter();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int index = 0; index < 4096; index++)
            {
                WorldChunkStreamingPlanSummary summary = planner.Plan(
                    focus,
                    available,
                    available.Length,
                    loaded,
                    loaded.Length);
                checksum += summary.LoadCount + summary.UnloadCount;
            }

            long allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            Assert.That(allocatedBytes, Is.Zero);
            Assert.That(checksum, Is.GreaterThan(0));
        }

        private static WorldChunkKey Key(int x, int z)
        {
            return new WorldChunkKey(
                World,
                Zone,
                new WorldChunkCoordinate(x, z));
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
