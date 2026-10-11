using System;
using System.Collections.Generic;
using SessionRpg.Application;
using SessionRpg.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SessionRpg.Unity.World
{
    public enum WorldZoneStreamStatus
    {
        Inactive = 0,
        Resolving = 1,
        Loading = 2,
        Unloading = 3,
        Ready = 4,
        WaitingForContent = 5,
        CapacityLimited = 6,
        Faulted = 7,
    }

    public enum WorldZoneStreamOperation
    {
        None = 0,
        Load = 1,
        Unload = 2,
    }

    public readonly struct LoadedWorldChunk
    {
        public LoadedWorldChunk(
            uint worldId,
            uint zoneId,
            WorldChunkCoordinate coordinate,
            string scenePath,
            WorldChunkPresentation presentation)
        {
            WorldId = worldId;
            ZoneId = zoneId;
            Coordinate = coordinate;
            ScenePath = scenePath;
            Presentation = presentation;
        }

        public uint WorldId { get; }

        public uint ZoneId { get; }

        public WorldChunkCoordinate Coordinate { get; }

        public string ScenePath { get; }

        public WorldChunkPresentation Presentation { get; }
    }

    /// <summary>
    /// Streams visual chunk scenes around an explicitly supplied focus. The controller is
    /// deliberately presentation-only: it does not spawn simulation entities, author combat
    /// collision, decide navigation, or advance deterministic gameplay state.
    /// </summary>
    [DefaultExecutionOrder(-9000)]
    [DisallowMultipleComponent]
    public sealed class WorldZoneStreamController : MonoBehaviour
    {
        private enum ChunkResidency
        {
            Empty = 0,
            Loading = 1,
            Loaded = 2,
            Unloading = 3,
        }

        private struct ChunkSlot
        {
            public ChunkResidency Residency;
            public int Generation;
            public uint WorldId;
            public uint ZoneId;
            public WorldZoneChunkEntry Entry;
            public Scene Scene;
            public WorldChunkPresentation Presentation;
        }

        private static readonly ChunkSlot[] EmptySlots = Array.Empty<ChunkSlot>();

        [SerializeField] private WorldZoneCatalogAsset _catalog;

        private readonly List<GameObject> _sceneRootBuffer =
            new List<GameObject>(8);

        private ChunkSlot[] _slots = EmptySlots;
        private WorldChunkStreamingPlanner _planner;
        private WorldChunkKey[] _availableChunkKeys = Array.Empty<WorldChunkKey>();
        private WorldChunkKey[] _loadedChunkKeys = Array.Empty<WorldChunkKey>();
        private WorldZoneEntry _activeZone;
        private Transform _focusTransform;
        private Vector3 _focusPosition;
        private WorldChunkCoordinate _focusCoordinate;
        private bool _hasFocusCoordinate;
        private bool _isActive;
        private bool _needsReconcile;
        private bool _faulted;
        private bool _isInitialWindowReady;
        private bool _lifecycleCleanupRequested;
        private int _lifecycleCleanupPendingCount;
        private string _lifecycleLoadingScenePath = string.Empty;
        private int _generation;

        private AsyncOperation _operation;
        private WorldZoneStreamOperation _pendingOperation;
        private int _operationSlotIndex = -1;
        private int _operationGeneration;
        private string _pendingScenePath = string.Empty;
        private string _lastError = string.Empty;

        public WorldZoneCatalogAsset Catalog => _catalog;

        public bool IsZoneActive => _isActive;

        public bool IsInitialWindowReady => _isInitialWindowReady;

        public WorldZoneStreamStatus Status { get; private set; } =
            WorldZoneStreamStatus.Inactive;

        public uint ActiveWorldId => _isActive && _activeZone != null
            ? _activeZone.WorldId
            : 0u;

        public uint ActiveZoneId => _isActive && _activeZone != null
            ? _activeZone.ZoneId
            : 0u;

        public bool HasFocusCoordinate => _hasFocusCoordinate;

        public WorldChunkCoordinate FocusCoordinate => _focusCoordinate;

        public int Generation => _generation;

        public WorldZoneStreamOperation PendingOperation => _pendingOperation;

        public string PendingScenePath => _pendingScenePath;

        public string LastError => _lastError;

        public int LifecycleCleanupPendingCount =>
            _lifecycleCleanupPendingCount;

        public int LoadedChunkCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _slots.Length; index++)
                {
                    if (_slots[index].Residency == ChunkResidency.Loaded)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        public int ResidentChunkCount
        {
            get
            {
                int count = 0;
                for (int index = 0; index < _slots.Length; index++)
                {
                    if (_slots[index].Residency != ChunkResidency.Empty)
                    {
                        count++;
                    }
                }

                return count;
            }
        }

        private void Update()
        {
            TickStreaming();
        }

        private void OnEnable()
        {
            if (_lifecycleCleanupPendingCount == 0)
            {
                _lifecycleCleanupRequested = false;
            }
        }

        private void OnDisable()
        {
            ReleaseOwnedScenesForLifecycleEnd();
        }

        /// <summary>
        /// Selects a zone and starts loading its visual window. Calling this again creates a new
        /// generation; an in-flight operation from the previous generation is allowed to finish
        /// and is then safely discarded.
        /// </summary>
        public bool ActivateZone(
            uint worldId,
            uint zoneId,
            Vector3 initialFocus)
        {
            if (_catalog == null)
            {
                ReportCommandError("World zone catalog is not assigned.");
                return false;
            }

            if (!_catalog.TryGetZone(worldId, zoneId, out WorldZoneEntry zone))
            {
                ReportCommandError(
                    $"World zone catalog has no world {worldId}, zone {zoneId}.");
                return false;
            }

            if (!IsFinite(initialFocus))
            {
                ReportCommandError("World stream focus must contain finite coordinates.");
                return false;
            }

            AdvanceGeneration();
            EnsureSlotCapacity(zone.MaxLoadedChunks);
            PreparePlanner(zone);

            _activeZone = zone;
            _focusTransform = null;
            _focusPosition = initialFocus;
            _isActive = true;
            _faulted = false;
            _lastError = string.Empty;
            _isInitialWindowReady = false;
            _hasFocusCoordinate = false;
            _needsReconcile = true;
            Status = WorldZoneStreamStatus.Resolving;

            AdoptMatchingResidentChunksForCurrentGeneration();
            UpdateFocusCoordinate();
            return true;
        }

        /// <summary>
        /// Stops requesting zone content. Owned additive scenes unload serially while this
        /// component remains enabled on its bootstrap object.
        /// </summary>
        public void DeactivateZone()
        {
            if (!_isActive &&
                _operation == null &&
                ResidentChunkCount == 0)
            {
                Status = WorldZoneStreamStatus.Inactive;
                return;
            }

            AdvanceGeneration();
            _isActive = false;
            _activeZone = null;
            _focusTransform = null;
            _hasFocusCoordinate = false;
            _isInitialWindowReady = false;
            _faulted = false;
            _lastError = string.Empty;
            _needsReconcile = true;
            Status = WorldZoneStreamStatus.Unloading;
        }

        /// <summary>
        /// Sets an explicit world-space focus. Reconciliation only runs when this crosses a
        /// chunk boundary (or when an asynchronous operation finishes).
        /// </summary>
        public void SetFocus(Vector3 worldPosition)
        {
            _focusTransform = null;
            SetFocusPosition(worldPosition);
        }

        /// <summary>
        /// Follows a presentation transform without searching the scene at runtime.
        /// </summary>
        public void SetFocus(Transform focus)
        {
            _focusTransform = focus;
            if (focus != null)
            {
                SetFocusPosition(focus.position);
            }
        }

        public bool IsChunkLoaded(WorldChunkCoordinate coordinate)
        {
            if (!_isActive || _activeZone == null)
            {
                return false;
            }

            return FindLoadedSlot(
                _activeZone.WorldId,
                _activeZone.ZoneId,
                coordinate,
                null) >= 0;
        }

        public bool TryGetLoadedChunk(int loadedIndex, out LoadedWorldChunk chunk)
        {
            if (loadedIndex < 0)
            {
                chunk = default;
                return false;
            }

            int ordinal = 0;
            for (int index = 0; index < _slots.Length; index++)
            {
                ref ChunkSlot slot = ref _slots[index];
                if (slot.Residency != ChunkResidency.Loaded)
                {
                    continue;
                }

                if (ordinal == loadedIndex)
                {
                    chunk = new LoadedWorldChunk(
                        slot.WorldId,
                        slot.ZoneId,
                        slot.Entry.Coordinate,
                        slot.Entry.ScenePath,
                        slot.Presentation);
                    return true;
                }

                ordinal++;
            }

            chunk = default;
            return false;
        }

#if UNITY_EDITOR
        public void ConfigureForAuthoring(WorldZoneCatalogAsset catalog)
        {
            _catalog = catalog;
        }
#endif

        private void TickStreaming()
        {
            if (_isActive && _focusTransform != null)
            {
                SetFocusPosition(_focusTransform.position);
            }

            if (_operation != null)
            {
                if (!_operation.isDone)
                {
                    Status = _pendingOperation == WorldZoneStreamOperation.Load
                        ? WorldZoneStreamStatus.Loading
                        : WorldZoneStreamStatus.Unloading;
                    return;
                }

                CompleteOperation();
            }

            if (!_needsReconcile)
            {
                return;
            }

            if (!_isActive)
            {
                ReconcileInactive();
                return;
            }

            if (_faulted)
            {
                CleanupFaultedLoad();
                Status = WorldZoneStreamStatus.Faulted;
                return;
            }

            ReconcileActive();
        }

        private void ReconcileInactive()
        {
            int unloadIndex = FindDeterministicResidentSlot();
            if (unloadIndex >= 0)
            {
                TryStartUnload(unloadIndex);
                return;
            }

            _needsReconcile = false;
            Status = WorldZoneStreamStatus.Inactive;
        }

        private void ReconcileActive()
        {
            if (_activeZone == null || !_hasFocusCoordinate)
            {
                SetFault("Active world stream has no resolved zone or focus coordinate.");
                return;
            }

            int staleSlot = FindStaleSlot();
            if (staleSlot >= 0)
            {
                _isInitialWindowReady = false;
                TryStartUnload(staleSlot);
                return;
            }

            int loadedCount = PopulateLoadedKeyBuffer();
            if (loadedCount > _activeZone.MaxLoadedChunks)
            {
                _isInitialWindowReady = false;
                int overflow = FindOverflowSlot();
                if (overflow >= 0)
                {
                    TryStartUnload(overflow);
                    return;
                }

                _needsReconcile = false;
                Status = WorldZoneStreamStatus.CapacityLimited;
                return;
            }

            WorldChunkKey focus = new WorldChunkKey(
                new WorldId(_activeZone.WorldId),
                new ZoneId(_activeZone.ZoneId),
                _focusCoordinate);

            WorldChunkStreamingPlanSummary plan;
            try
            {
                plan = _planner.Plan(
                    focus,
                    _availableChunkKeys,
                    _activeZone.ChunkCount,
                    _loadedChunkKeys,
                    loadedCount);
            }
            catch (Exception exception)
            {
                SetFault(
                    $"Could not plan visual chunk residency for zone " +
                    $"'{_activeZone.ZoneKey}': {exception.Message}");
                return;
            }

            bool hasRequiredContent = HasRequiredContent(
                focus,
                out bool requiredWindowLoaded);
            _isInitialWindowReady = hasRequiredContent && requiredWindowLoaded;

            if (plan.LoadCount > 0 &&
                loadedCount < _activeZone.MaxLoadedChunks)
            {
                WorldChunkKey loadKey = _planner.GetLoadRequest(0);
                if (!TryFindCatalogEntry(loadKey, out WorldZoneChunkEntry entry))
                {
                    SetFault($"Streaming plan referenced an unknown chunk '{loadKey}'.");
                    return;
                }

                int freeSlot = FindFreeSlot();
                if (freeSlot < 0)
                {
                    SetFault("Streaming plan has no free residency slot.");
                    return;
                }

                TryStartLoad(freeSlot, entry);
                return;
            }

            if (plan.UnloadCount > 0)
            {
                WorldChunkKey unloadKey = _planner.GetUnloadRequest(0);
                int unloadSlot = FindLoadedSlot(unloadKey);
                if (unloadSlot < 0)
                {
                    SetFault(
                        $"Streaming plan referenced a non-resident chunk " +
                        $"'{unloadKey}'.");
                    return;
                }

                TryStartUnload(unloadSlot);
                return;
            }

            _needsReconcile = false;
            if (!hasRequiredContent)
            {
                Status = WorldZoneStreamStatus.WaitingForContent;
                return;
            }

            if (!requiredWindowLoaded || plan.LoadCount > 0)
            {
                Status = WorldZoneStreamStatus.CapacityLimited;
                return;
            }

            Status = WorldZoneStreamStatus.Ready;
        }

        private void PreparePlanner(WorldZoneEntry zone)
        {
            _planner = new WorldChunkStreamingPlanner(
                zone.LoadRadius,
                zone.RetainRadius,
                zone.MaxLoadedChunks);

            int availableCount = zone.ChunkCount;
            if (_availableChunkKeys.Length != availableCount)
            {
                _availableChunkKeys = new WorldChunkKey[availableCount];
            }

            WorldId world = new WorldId(zone.WorldId);
            ZoneId zoneId = new ZoneId(zone.ZoneId);
            for (int index = 0; index < availableCount; index++)
            {
                WorldZoneChunkEntry chunk = zone.GetChunk(index);
                _availableChunkKeys[index] = chunk != null
                    ? new WorldChunkKey(world, zoneId, chunk.Coordinate)
                    : default;
            }

            if (_loadedChunkKeys.Length < _slots.Length)
            {
                Array.Resize(ref _loadedChunkKeys, _slots.Length);
            }
        }

        private int PopulateLoadedKeyBuffer()
        {
            int count = 0;
            for (int index = 0; index < _slots.Length; index++)
            {
                ref ChunkSlot slot = ref _slots[index];
                if (slot.Residency != ChunkResidency.Loaded)
                {
                    continue;
                }

                _loadedChunkKeys[count] = new WorldChunkKey(
                    new WorldId(slot.WorldId),
                    new ZoneId(slot.ZoneId),
                    slot.Entry.Coordinate);
                count++;
            }

            return count;
        }

        private int FindStaleSlot()
        {
            int bestIndex = -1;
            for (int index = 0; index < _slots.Length; index++)
            {
                ref ChunkSlot slot = ref _slots[index];
                if (slot.Residency != ChunkResidency.Loaded ||
                    (slot.Generation == _generation &&
                     slot.WorldId == _activeZone.WorldId &&
                     slot.ZoneId == _activeZone.ZoneId &&
                     slot.Presentation != null))
                {
                    continue;
                }

                if (bestIndex < 0 || IsSlotBefore(slot, _slots[bestIndex]))
                {
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        private int FindOverflowSlot()
        {
            int bestIndex = -1;
            long bestChebyshev = long.MinValue;
            long bestManhattan = long.MinValue;

            for (int index = 0; index < _slots.Length; index++)
            {
                ref ChunkSlot slot = ref _slots[index];
                if (slot.Residency != ChunkResidency.Loaded)
                {
                    continue;
                }

                WorldChunkCoordinate coordinate = slot.Entry.Coordinate;
                long chebyshev = coordinate.ChebyshevDistanceTo(_focusCoordinate);
                long manhattan = coordinate.ManhattanDistanceTo(_focusCoordinate);
                if (bestIndex < 0 ||
                    chebyshev > bestChebyshev ||
                    (chebyshev == bestChebyshev && manhattan > bestManhattan) ||
                    (chebyshev == bestChebyshev &&
                     manhattan == bestManhattan &&
                     coordinate.CompareTo(_slots[bestIndex].Entry.Coordinate) > 0))
                {
                    bestIndex = index;
                    bestChebyshev = chebyshev;
                    bestManhattan = manhattan;
                }
            }

            return bestIndex;
        }

        private bool HasRequiredContent(
            WorldChunkKey focus,
            out bool requiredWindowLoaded)
        {
            bool found = false;
            requiredWindowLoaded = true;
            int count = _activeZone.ChunkCount;
            for (int index = 0; index < count; index++)
            {
                WorldChunkKey available = _availableChunkKeys[index];
                if (!available.IsValid ||
                    !available.Coordinate.IsWithinChebyshevDistance(
                        focus.Coordinate,
                        _activeZone.LoadRadius))
                {
                    continue;
                }

                found = true;
                if (FindLoadedSlot(available) < 0)
                {
                    requiredWindowLoaded = false;
                }
            }

            return found;
        }

        private bool TryFindCatalogEntry(
            WorldChunkKey key,
            out WorldZoneChunkEntry entry)
        {
            if (_activeZone == null ||
                key.World.Value != _activeZone.WorldId ||
                key.Zone.Value != _activeZone.ZoneId)
            {
                entry = null;
                return false;
            }

            return _activeZone.TryGetChunk(key.Coordinate, out entry);
        }

        private int FindLoadedSlot(WorldChunkKey key)
        {
            return FindLoadedSlot(
                key.World.Value,
                key.Zone.Value,
                key.Coordinate,
                null);
        }

        private int FindDeterministicResidentSlot()
        {
            int bestIndex = -1;
            for (int index = 0; index < _slots.Length; index++)
            {
                ref ChunkSlot slot = ref _slots[index];
                if (slot.Residency != ChunkResidency.Loaded)
                {
                    continue;
                }

                if (bestIndex < 0 || IsSlotBefore(slot, _slots[bestIndex]))
                {
                    bestIndex = index;
                }
            }

            return bestIndex;
        }

        private static bool IsSlotBefore(ChunkSlot candidate, ChunkSlot current)
        {
            if (candidate.WorldId != current.WorldId)
            {
                return candidate.WorldId < current.WorldId;
            }

            if (candidate.ZoneId != current.ZoneId)
            {
                return candidate.ZoneId < current.ZoneId;
            }

            int coordinateOrder = candidate.Entry.Coordinate.CompareTo(
                current.Entry.Coordinate);
            return coordinateOrder != 0
                ? coordinateOrder < 0
                : string.CompareOrdinal(
                    candidate.Entry.ScenePath,
                    current.Entry.ScenePath) < 0;
        }

        private void TryStartLoad(int slotIndex, WorldZoneChunkEntry entry)
        {
            if (!entry.HasScenePath)
            {
                SetFault(
                    $"World chunk {_activeZone.ZoneKey} {entry.Coordinate} has no scene path.");
                return;
            }

            if (FindLoadedScene(entry.ScenePath).IsValid())
            {
                SetFault(
                    $"World chunk scene is already loaded outside this stream controller: " +
                    entry.ScenePath);
                return;
            }

            ref ChunkSlot slot = ref _slots[slotIndex];
            slot = new ChunkSlot
            {
                Residency = ChunkResidency.Loading,
                Generation = _generation,
                WorldId = _activeZone.WorldId,
                ZoneId = _activeZone.ZoneId,
                Entry = entry,
            };

            AsyncOperation operation;
            try
            {
                operation = SceneManager.LoadSceneAsync(
                    entry.ScenePath,
                    LoadSceneMode.Additive);
            }
            catch (Exception exception)
            {
                slot = default;
                SetFault(
                    $"Could not begin loading world chunk '{entry.ScenePath}': " +
                    exception.Message);
                return;
            }

            if (operation == null)
            {
                slot = default;
                SetFault(
                    $"Unity did not create a load operation for world chunk " +
                    $"'{entry.ScenePath}'.");
                return;
            }

            _operation = operation;
            _pendingOperation = WorldZoneStreamOperation.Load;
            _operationSlotIndex = slotIndex;
            _operationGeneration = _generation;
            _pendingScenePath = entry.ScenePath;
            _needsReconcile = false;
            Status = WorldZoneStreamStatus.Loading;
        }

        private void TryStartUnload(int slotIndex)
        {
            ref ChunkSlot slot = ref _slots[slotIndex];
            Scene scene = slot.Scene;
            if (!scene.IsValid() || !scene.isLoaded)
            {
                slot = default;
                _needsReconcile = true;
                return;
            }

            AsyncOperation operation;
            try
            {
                operation = SceneManager.UnloadSceneAsync(scene);
            }
            catch (Exception exception)
            {
                SetFault(
                    $"Could not begin unloading world chunk '{slot.Entry.ScenePath}': " +
                    exception.Message);
                return;
            }

            if (operation == null)
            {
                SetFault(
                    $"Unity did not create an unload operation for world chunk " +
                    $"'{slot.Entry.ScenePath}'.");
                return;
            }

            slot.Residency = ChunkResidency.Unloading;
            _operation = operation;
            _pendingOperation = WorldZoneStreamOperation.Unload;
            _operationSlotIndex = slotIndex;
            _operationGeneration = _generation;
            _pendingScenePath = slot.Entry.ScenePath;
            _needsReconcile = false;
            Status = WorldZoneStreamStatus.Unloading;
        }

        private void CompleteOperation()
        {
            WorldZoneStreamOperation completedKind = _pendingOperation;
            int completedSlot = _operationSlotIndex;
            int completedGeneration = _operationGeneration;
            string completedPath = _pendingScenePath;

            _operation = null;
            _pendingOperation = WorldZoneStreamOperation.None;
            _operationSlotIndex = -1;
            _operationGeneration = 0;
            _pendingScenePath = string.Empty;
            _needsReconcile = true;

            if (completedSlot < 0 || completedSlot >= _slots.Length)
            {
                SetFault("World stream completed an operation with an invalid slot.");
                return;
            }

            if (completedKind == WorldZoneStreamOperation.Unload)
            {
                _slots[completedSlot] = default;
                return;
            }

            if (completedKind != WorldZoneStreamOperation.Load)
            {
                SetFault("World stream completed an operation with no operation kind.");
                return;
            }

            CompleteLoad(completedSlot, completedGeneration, completedPath);
        }

        private void CompleteLoad(
            int slotIndex,
            int operationGeneration,
            string scenePath)
        {
            ref ChunkSlot slot = ref _slots[slotIndex];
            Scene loadedScene = FindLoadedScene(scenePath);
            if (!loadedScene.IsValid() || !loadedScene.isLoaded)
            {
                slot = default;
                SetFault($"World chunk scene did not finish loading: '{scenePath}'.");
                return;
            }

            slot.Scene = loadedScene;
            slot.Residency = ChunkResidency.Loaded;

            bool stale = operationGeneration != _generation ||
                         !_isActive ||
                         _activeZone == null ||
                         slot.WorldId != _activeZone.WorldId ||
                         slot.ZoneId != _activeZone.ZoneId;
            if (stale)
            {
                return;
            }

            if (!TryFindPresentation(loadedScene, out WorldChunkPresentation presentation))
            {
                SetFault(
                    $"World chunk scene '{scenePath}' must have exactly one " +
                    $"WorldChunkPresentation on a scene root.");
                return;
            }

            if (!presentation.Matches(
                    slot.WorldId,
                    slot.ZoneId,
                    slot.Entry.Coordinate) ||
                !string.Equals(
                    presentation.SourceFingerprint,
                    slot.Entry.SourceFingerprint,
                    StringComparison.Ordinal))
            {
                SetFault(
                    $"World chunk marker identity or source fingerprint does not " +
                    $"match catalog entry " +
                    $"'{scenePath}'.");
                return;
            }

            slot.Presentation = presentation;
        }

        private bool TryFindPresentation(
            Scene scene,
            out WorldChunkPresentation presentation)
        {
            _sceneRootBuffer.Clear();
            scene.GetRootGameObjects(_sceneRootBuffer);

            int count = _sceneRootBuffer.Count;
            if (count != 1 || !_sceneRootBuffer[0].TryGetComponent(
                    out presentation))
            {
                presentation = null;
                return false;
            }

            return true;
        }

        private void ReleaseOwnedScenesForLifecycleEnd()
        {
            if (_lifecycleCleanupRequested)
            {
                return;
            }

            _lifecycleCleanupRequested = true;
            if (_operation != null &&
                _pendingOperation == WorldZoneStreamOperation.Load)
            {
                _lifecycleLoadingScenePath = _pendingScenePath;
                _lifecycleCleanupPendingCount++;
                _operation.completed += CompleteLifecyclePendingLoad;
            }
            else if (_operation != null &&
                     _pendingOperation == WorldZoneStreamOperation.Unload)
            {
                _lifecycleCleanupPendingCount++;
                _operation.completed += CompleteLifecycleUnload;
            }

            for (int index = 0; index < _slots.Length; index++)
            {
                ref ChunkSlot slot = ref _slots[index];
                if (slot.Residency == ChunkResidency.Loaded &&
                    slot.Scene.IsValid() &&
                    slot.Scene.isLoaded)
                {
                    StartLifecycleUnload(slot.Scene);
                }

                slot = default;
            }

            _operation = null;
            _pendingOperation = WorldZoneStreamOperation.None;
            _operationSlotIndex = -1;
            _operationGeneration = 0;
            _pendingScenePath = string.Empty;
            _activeZone = null;
            _focusTransform = null;
            _isActive = false;
            _hasFocusCoordinate = false;
            _isInitialWindowReady = false;
            _faulted = false;
            _needsReconcile = false;
            Status = WorldZoneStreamStatus.Inactive;
        }

        private void CompleteLifecyclePendingLoad(AsyncOperation operation)
        {
            _lifecycleCleanupPendingCount = Mathf.Max(
                0,
                _lifecycleCleanupPendingCount - 1);
            string scenePath = _lifecycleLoadingScenePath;
            _lifecycleLoadingScenePath = string.Empty;
            Scene scene = FindLoadedScene(scenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                StartLifecycleUnload(scene);
            }
        }

        private void StartLifecycleUnload(Scene scene)
        {
            try
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null)
                {
                    _lifecycleCleanupPendingCount++;
                    unload.completed += CompleteLifecycleUnload;
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }

        private void CompleteLifecycleUnload(AsyncOperation operation)
        {
            _lifecycleCleanupPendingCount = Mathf.Max(
                0,
                _lifecycleCleanupPendingCount - 1);
        }

        private void CleanupFaultedLoad()
        {
            for (int index = 0; index < _slots.Length; index++)
            {
                ref ChunkSlot slot = ref _slots[index];
                if (slot.Residency == ChunkResidency.Loaded &&
                    slot.Presentation == null)
                {
                    TryStartUnload(index);
                    return;
                }
            }

            _needsReconcile = false;
        }

        private void SetFocusPosition(Vector3 worldPosition)
        {
            if (!IsFinite(worldPosition))
            {
                if (_isActive)
                {
                    SetFault("World stream focus must contain finite coordinates.");
                }

                return;
            }

            _focusPosition = worldPosition;
            if (_isActive)
            {
                UpdateFocusCoordinate();
            }
        }

        private void UpdateFocusCoordinate()
        {
            if (_activeZone == null)
            {
                return;
            }

            WorldChunkCoordinate next = _activeZone.WorldToChunk(_focusPosition);
            if (_hasFocusCoordinate && next.Equals(_focusCoordinate))
            {
                return;
            }

            _focusCoordinate = next;
            _hasFocusCoordinate = true;
            _isInitialWindowReady = false;
            _needsReconcile = true;
            if (_operation == null)
            {
                Status = WorldZoneStreamStatus.Resolving;
            }
        }

        private void AdoptMatchingResidentChunksForCurrentGeneration()
        {
            for (int index = 0; index < _slots.Length; index++)
            {
                ref ChunkSlot slot = ref _slots[index];
                if (slot.Residency == ChunkResidency.Loaded &&
                    slot.WorldId == _activeZone.WorldId &&
                    slot.ZoneId == _activeZone.ZoneId &&
                    slot.Presentation != null)
                {
                    slot.Generation = _generation;
                }
            }
        }

        private int FindFreeSlot()
        {
            int cap = Mathf.Min(_slots.Length, _activeZone.MaxLoadedChunks);
            for (int index = 0; index < cap; index++)
            {
                if (_slots[index].Residency == ChunkResidency.Empty)
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindResidentSlot(
            uint worldId,
            uint zoneId,
            WorldChunkCoordinate coordinate,
            string scenePath)
        {
            for (int index = 0; index < _slots.Length; index++)
            {
                ref ChunkSlot slot = ref _slots[index];
                if (slot.Residency == ChunkResidency.Empty ||
                    slot.Generation != _generation ||
                    slot.WorldId != worldId ||
                    slot.ZoneId != zoneId ||
                    !slot.Entry.Coordinate.Equals(coordinate))
                {
                    continue;
                }

                if (scenePath == null ||
                    string.Equals(
                        slot.Entry.ScenePath,
                        scenePath,
                        StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private int FindLoadedSlot(
            uint worldId,
            uint zoneId,
            WorldChunkCoordinate coordinate,
            string scenePath)
        {
            int index = FindResidentSlot(worldId, zoneId, coordinate, scenePath);
            return index >= 0 && _slots[index].Residency == ChunkResidency.Loaded
                ? index
                : -1;
        }

        private static Scene FindLoadedScene(string scenePath)
        {
            Scene scene = SceneManager.GetSceneByPath(scenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                return scene;
            }

            int count = SceneManager.sceneCount;
            for (int index = 0; index < count; index++)
            {
                Scene candidate = SceneManager.GetSceneAt(index);
                if (candidate.isLoaded &&
                    string.Equals(
                        candidate.path,
                        scenePath,
                        StringComparison.Ordinal))
                {
                    return candidate;
                }
            }

            return default;
        }

        private void EnsureSlotCapacity(int capacity)
        {
            if (_slots.Length >= capacity)
            {
                return;
            }

            Array.Resize(ref _slots, capacity);
        }

        private void AdvanceGeneration()
        {
            unchecked
            {
                _generation++;
            }

            if (_generation == 0)
            {
                _generation = 1;
            }
        }

        private void ReportCommandError(string message)
        {
            _lastError = message;
            Debug.LogError(message, this);
        }

        private void SetFault(string message)
        {
            _faulted = true;
            _isInitialWindowReady = false;
            _lastError = message;
            _needsReconcile = true;
            Status = WorldZoneStreamStatus.Faulted;
            Debug.LogError(message, this);
        }

        private static bool IsFinite(Vector3 value)
        {
            return IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
