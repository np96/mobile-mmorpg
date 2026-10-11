using System;
using SessionRpg.Core;

namespace SessionRpg.Application
{
    public readonly struct WorldChunkStreamingPlanSummary
    {
        internal WorldChunkStreamingPlanSummary(
            int loadCount,
            int unloadCount,
            int retainedLoadedCount,
            int projectedLoadedCount)
        {
            LoadCount = loadCount;
            UnloadCount = unloadCount;
            RetainedLoadedCount = retainedLoadedCount;
            ProjectedLoadedCount = projectedLoadedCount;
        }

        public int LoadCount { get; }

        public int UnloadCount { get; }

        public int RetainedLoadedCount { get; }

        public int ProjectedLoadedCount { get; }
    }

    /// <summary>
    /// Builds deterministic chunk load/unload requests into buffers allocated
    /// once by the constructor. The returned requests remain valid until the
    /// next Plan call and must not be retained across plans.
    /// </summary>
    public sealed class WorldChunkStreamingPlanner
    {
        private readonly WorldChunkKey[] _loadRequests;
        private readonly WorldChunkKey[] _unloadRequests;

        public WorldChunkStreamingPlanner(
            int loadRadius,
            int retainRadius,
            int maximumLoadedChunkCount)
        {
            if (loadRadius < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(loadRadius));
            }

            if (retainRadius < loadRadius)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(retainRadius),
                    "The retain radius must be at least the load radius.");
            }

            if (maximumLoadedChunkCount <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(maximumLoadedChunkCount));
            }

            LoadRadius = loadRadius;
            RetainRadius = retainRadius;
            MaximumLoadedChunkCount = maximumLoadedChunkCount;
            _loadRequests = new WorldChunkKey[maximumLoadedChunkCount];
            _unloadRequests = new WorldChunkKey[maximumLoadedChunkCount];
        }

        public int LoadRadius { get; }

        public int RetainRadius { get; }

        public int MaximumLoadedChunkCount { get; }

        public int LoadCount { get; private set; }

        public int UnloadCount { get; private set; }

        public int RetainedLoadedCount { get; private set; }

        public int ProjectedLoadedCount { get; private set; }

        public WorldChunkStreamingPlanSummary Plan(
            WorldChunkKey focus,
            WorldChunkKey[] availableChunks,
            int availableChunkCount,
            WorldChunkKey[] loadedChunks,
            int loadedChunkCount)
        {
            ValidatePlanInput(
                focus,
                availableChunks,
                availableChunkCount,
                loadedChunks,
                loadedChunkCount);

            LoadCount = 0;
            UnloadCount = 0;

            for (int index = 0; index < loadedChunkCount; index++)
            {
                WorldChunkKey loaded = loadedChunks[index];
                if (!CanRetain(
                    loaded,
                    focus,
                    availableChunks,
                    availableChunkCount))
                {
                    AddUnloadSorted(loaded);
                }
            }

            int retainedCount = loadedChunkCount - UnloadCount;
            while (TryFindBestMissingCandidate(
                focus,
                availableChunks,
                availableChunkCount,
                loadedChunks,
                loadedChunkCount,
                out WorldChunkKey candidate))
            {
                if (retainedCount + LoadCount < MaximumLoadedChunkCount)
                {
                    AddLoadSorted(candidate, focus);
                    continue;
                }

                if (!TryFindFarthestRetained(
                    focus,
                    loadedChunks,
                    loadedChunkCount,
                    out WorldChunkKey farthestRetained) ||
                    !IsStrictlyNearer(candidate, farthestRetained, focus))
                {
                    break;
                }

                AddUnloadSorted(farthestRetained);
                retainedCount--;
                AddLoadSorted(candidate, focus);
            }

            RetainedLoadedCount = loadedChunkCount - UnloadCount;
            ProjectedLoadedCount = RetainedLoadedCount + LoadCount;
            return new WorldChunkStreamingPlanSummary(
                LoadCount,
                UnloadCount,
                RetainedLoadedCount,
                ProjectedLoadedCount);
        }

        public WorldChunkKey GetLoadRequest(int index)
        {
            if (index < 0 || index >= LoadCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _loadRequests[index];
        }

        public WorldChunkKey GetUnloadRequest(int index)
        {
            if (index < 0 || index >= UnloadCount)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _unloadRequests[index];
        }

        private bool CanRetain(
            WorldChunkKey loaded,
            WorldChunkKey focus,
            WorldChunkKey[] availableChunks,
            int availableChunkCount)
        {
            return loaded.World == focus.World &&
                loaded.Zone == focus.Zone &&
                loaded.Coordinate.IsWithinChebyshevDistance(
                    focus.Coordinate,
                    RetainRadius) &&
                Contains(availableChunks, availableChunkCount, loaded);
        }

        private bool TryFindBestMissingCandidate(
            WorldChunkKey focus,
            WorldChunkKey[] availableChunks,
            int availableChunkCount,
            WorldChunkKey[] loadedChunks,
            int loadedChunkCount,
            out WorldChunkKey candidate)
        {
            candidate = default;
            bool found = false;
            for (int index = 0; index < availableChunkCount; index++)
            {
                WorldChunkKey current = availableChunks[index];
                if (current.World != focus.World ||
                    current.Zone != focus.Zone ||
                    !current.Coordinate.IsWithinChebyshevDistance(
                        focus.Coordinate,
                        LoadRadius) ||
                    Contains(loadedChunks, loadedChunkCount, current) ||
                    Contains(_loadRequests, LoadCount, current))
                {
                    continue;
                }

                if (!found ||
                    CompareResidencyPriority(current, candidate, focus) < 0)
                {
                    candidate = current;
                    found = true;
                }
            }

            return found;
        }

        private bool TryFindFarthestRetained(
            WorldChunkKey focus,
            WorldChunkKey[] loadedChunks,
            int loadedChunkCount,
            out WorldChunkKey farthest)
        {
            farthest = default;
            bool found = false;
            for (int index = 0; index < loadedChunkCount; index++)
            {
                WorldChunkKey current = loadedChunks[index];
                if (Contains(_unloadRequests, UnloadCount, current))
                {
                    continue;
                }

                if (!found ||
                    CompareResidencyPriority(current, farthest, focus) > 0)
                {
                    farthest = current;
                    found = true;
                }
            }

            return found;
        }

        private void AddLoadSorted(WorldChunkKey key, WorldChunkKey focus)
        {
            int insertionIndex = LoadCount;
            while (insertionIndex > 0 &&
                CompareResidencyPriority(
                    key,
                    _loadRequests[insertionIndex - 1],
                    focus) < 0)
            {
                _loadRequests[insertionIndex] =
                    _loadRequests[insertionIndex - 1];
                insertionIndex--;
            }

            _loadRequests[insertionIndex] = key;
            LoadCount++;
        }

        private void AddUnloadSorted(WorldChunkKey key)
        {
            int insertionIndex = UnloadCount;
            while (insertionIndex > 0 &&
                key.CompareTo(_unloadRequests[insertionIndex - 1]) < 0)
            {
                _unloadRequests[insertionIndex] =
                    _unloadRequests[insertionIndex - 1];
                insertionIndex--;
            }

            _unloadRequests[insertionIndex] = key;
            UnloadCount++;
        }

        private static bool IsStrictlyNearer(
            WorldChunkKey candidate,
            WorldChunkKey resident,
            WorldChunkKey focus)
        {
            long candidateChebyshev = candidate.Coordinate
                .ChebyshevDistanceTo(focus.Coordinate);
            long residentChebyshev = resident.Coordinate
                .ChebyshevDistanceTo(focus.Coordinate);
            if (candidateChebyshev != residentChebyshev)
            {
                return candidateChebyshev < residentChebyshev;
            }

            return candidate.Coordinate.ManhattanDistanceTo(focus.Coordinate) <
                resident.Coordinate.ManhattanDistanceTo(focus.Coordinate);
        }

        private static int CompareResidencyPriority(
            WorldChunkKey left,
            WorldChunkKey right,
            WorldChunkKey focus)
        {
            long leftChebyshev = left.Coordinate
                .ChebyshevDistanceTo(focus.Coordinate);
            long rightChebyshev = right.Coordinate
                .ChebyshevDistanceTo(focus.Coordinate);
            int comparison = leftChebyshev.CompareTo(rightChebyshev);
            if (comparison != 0)
            {
                return comparison;
            }

            long leftManhattan = left.Coordinate
                .ManhattanDistanceTo(focus.Coordinate);
            long rightManhattan = right.Coordinate
                .ManhattanDistanceTo(focus.Coordinate);
            comparison = leftManhattan.CompareTo(rightManhattan);
            return comparison != 0 ? comparison : left.CompareTo(right);
        }

        private static bool Contains(
            WorldChunkKey[] chunks,
            int count,
            WorldChunkKey candidate)
        {
            for (int index = 0; index < count; index++)
            {
                if (chunks[index] == candidate)
                {
                    return true;
                }
            }

            return false;
        }

        private void ValidatePlanInput(
            WorldChunkKey focus,
            WorldChunkKey[] availableChunks,
            int availableChunkCount,
            WorldChunkKey[] loadedChunks,
            int loadedChunkCount)
        {
            if (!focus.IsValid)
            {
                throw new ArgumentException(
                    "The streaming focus requires valid world and zone IDs.",
                    nameof(focus));
            }

            ValidateBuffer(
                availableChunks,
                availableChunkCount,
                int.MaxValue,
                false,
                nameof(availableChunks),
                nameof(availableChunkCount));
            ValidateBuffer(
                loadedChunks,
                loadedChunkCount,
                MaximumLoadedChunkCount,
                true,
                nameof(loadedChunks),
                nameof(loadedChunkCount));
        }

        private static void ValidateBuffer(
            WorldChunkKey[] chunks,
            int count,
            int maximumCount,
            bool rejectDuplicates,
            string bufferParameterName,
            string countParameterName)
        {
            if (chunks == null)
            {
                throw new ArgumentNullException(bufferParameterName);
            }

            if (count < 0 || count > chunks.Length || count > maximumCount)
            {
                throw new ArgumentOutOfRangeException(countParameterName);
            }

            for (int index = 0; index < count; index++)
            {
                if (!chunks[index].IsValid)
                {
                    throw new ArgumentException(
                        "Chunk buffers may contain only valid keys.",
                        bufferParameterName);
                }

                if (!rejectDuplicates)
                {
                    continue;
                }

                for (int duplicateIndex = index + 1;
                     duplicateIndex < count;
                     duplicateIndex++)
                {
                    if (chunks[index] == chunks[duplicateIndex])
                    {
                        throw new ArgumentException(
                            "Chunk buffers may not contain duplicate keys.",
                            bufferParameterName);
                    }
                }
            }
        }
    }
}
