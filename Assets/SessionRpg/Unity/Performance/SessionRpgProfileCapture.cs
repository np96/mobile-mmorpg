#if UNITY_EDITOR || DEVELOPMENT_BUILD || SESSION_RPG_CAPTURE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using SessionRpg.Application;
using SessionRpg.Core;
using SessionRpg.Unity.Gameplay;
using SessionRpg.Unity.World;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace SessionRpg.Unity.Performance
{
    /// <summary>
    /// Opt-in, bounded live-player observation. It never stages attacks, freezes
    /// the controller, changes quality, or runs in a normal release player.
    /// The separate screenshot runner deliberately freezes a visual tableau.
    /// </summary>
    internal sealed class SessionRpgProfileCapture : MonoBehaviour
    {
        private static bool _started;
        private SessionRpgProfileCaptureOptions _options;
        private ProfileReport _report;
        private RecordedCounter[] _counters;
        private bool _finished;
        private double _watchdogDeadline;
        private readonly FrameTiming[] _timing = new FrameTiming[1];
#if ENABLE_IL2CPP
        private const bool IsIl2CppBackend = true;
#else
        private const bool IsIl2CppBackend = false;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            if (_started) { return; }
            if (!SessionRpgProfileCaptureOptions.TryParse(Environment.GetCommandLineArgs(),
                    out SessionRpgProfileCaptureOptions options, out string error))
            {
                if (!string.IsNullOrEmpty(error)) { Debug.LogError(error); Quit(2); }
                return;
            }
            _started = true;
            if (File.Exists(options.OutputPath))
            {
                Debug.LogError("Profile output already exists; use a fresh evidence path: " + options.OutputPath);
                Quit(2);
                return;
            }
            UnityEngine.Application.runInBackground = true;
            var runner = new GameObject(nameof(SessionRpgProfileCapture));
            DontDestroyOnLoad(runner);
            var capture = runner.AddComponent<SessionRpgProfileCapture>();
            capture._options = options;
            capture._watchdogDeadline = Now + 120d;
            UnityEngine.Application.logMessageReceived += capture.ObserveLog;
            capture.StartCoroutine(capture.Observe());
        }

        private static double Now => Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency;

        private void Update()
        {
            if (!_finished && _options != null && Now > _watchdogDeadline)
            {
                Complete("watchdog-timeout", "No completion within 120 seconds.", 3);
            }
        }

        private IEnumerator Observe()
        {
            _report = new ProfileReport
            {
                startedUtc = DateTime.UtcNow.ToString("O"),
                epoch = Guid.NewGuid().ToString("N"),
                platform = UnityEngine.Application.platform.ToString(),
                scriptingBackend = IsIl2CppBackend ? "IL2CPP" : "Mono",
                limitations = ProfileCaptureMeasurementContract.DescribeLimitations(UnityEngine.Application.platform.ToString()),
                threadAllocationCounter = ProfileCaptureMeasurementContract.DescribeThreadAllocationCounter(IsIl2CppBackend),
                unityVersion = UnityEngine.Application.unityVersion,
                developmentBuild = Debug.isDebugBuild,
                operatingSystem = SystemInfo.operatingSystem,
                processor = SystemInfo.processorType,
                graphicsDevice = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                width = Screen.width,
                height = Screen.height,
                targetFrameRate = UnityEngine.Application.targetFrameRate,
                vSyncCount = QualitySettings.vSyncCount,
                specialization = _options.Specialization,
                camp = _options.Camp,
                requestedSamples = _options.Samples,
                warmupSeconds = _options.WarmupSeconds,
                frames = new ObservedFrame[_options.Samples],
            };
            PrototypeGameController controller = null;
            double discoveryDeadline = Now + 10d;
            while (controller == null && Now < discoveryDeadline)
            {
                controller = UnityEngine.Object.FindAnyObjectByType<PrototypeGameController>();
                yield return null;
            }
            if (controller == null) { Complete("setup-failed", "Game controller not found.", 3); yield break; }
            bool setupFailed = false;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_options.OutputPath));
                _report.diagnosticSaveDirectory = Path.Combine(Path.GetDirectoryName(_options.OutputPath),
                    Path.GetFileNameWithoutExtension(_options.OutputPath) + "-profile-save-" + _report.epoch);
                controller.ConfigureGrindingCapture(_report.diagnosticSaveDirectory, _options.Camp);
                var specialization = (SpecializationId)Enum.Parse(typeof(SpecializationId), _options.Specialization);
                controller.StartLocalSession(GameModeId.PveLeveling,
                    SpecializationCatalog.GetDefinition(specialization).OwnerClass, specialization);
            }
            catch (Exception error) { Complete("setup-failed", error.ToString(), 3); setupFailed = true; }
            if (setupFailed) { yield break; }

            var streamer = UnityEngine.Object.FindAnyObjectByType<WorldZoneStreamController>();
            double settleDeadline = Now + 30d;
            while (Now < settleDeadline && (streamer == null || !streamer.IsInitialWindowReady || !controller.IsGrindingCaptureReady))
            {
                yield return null;
            }
            if (streamer == null || !streamer.IsInitialWindowReady || !controller.IsGrindingCaptureReady)
            {
                Complete("setup-failed", "Initial streamed camp did not reach live playing state.", 3);
                yield break;
            }
            var quality = UnityEngine.Object.FindAnyObjectByType<MobileQualityController>();
            _report.qualityTier = quality != null ? quality.ActiveTier.ToString() : "unavailable";
            CombatantView human = null;
            CombatantView[] views = UnityEngine.Object.FindObjectsByType<CombatantView>(FindObjectsInactive.Exclude);
            foreach (CombatantView view in views) { if (view.IsHuman) { human = view; break; } }
            _report.frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
            _counters = new[]
            {
                new RecordedCounter(ProfilerCategory.Internal, "Main Thread", "nanoseconds; includes waits/overhead", _options.Samples),
                new RecordedCounter(ProfilerCategory.Internal, "Render Thread", "nanoseconds; may include waits", _options.Samples),
                new RecordedCounter(ProfilerCategory.Memory, "GC Allocated In Frame", "bytes; Unity frame allocation counter", _options.Samples),
                new RecordedCounter(ProfilerCategory.Memory, "Total Used Memory", "bytes; Unity tracked, not process RSS", _options.Samples),
                new RecordedCounter(ProfilerCategory.Memory, "Total Reserved Memory", "bytes; Unity tracked, not process RSS", _options.Samples),
                new RecordedCounter(ProfilerCategory.Render, "Draw Calls Count", "count", _options.Samples),
                new RecordedCounter(ProfilerCategory.Render, "SetPass Calls Count", "count", _options.Samples),
                new RecordedCounter(ProfilerCategory.Render, "Triangles Count", "count", _options.Samples),
            };
            _report.availableRenderMetrics = DescribeAvailableRenderMetrics();
            double warmupDeadline = Now + _options.WarmupSeconds;
            while (Now < warmupDeadline)
            {
                if (_report.frameTimingEnabled) { FrameTimingManager.CaptureFrameTimings(); }
                yield return null;
            }
            // Prime this runtime entry point before the observed window. Our own
            // final arrays, sorting, JSON serialization and I/O occur afterwards.
            long previousAllocated = ReadAllocatedBytes();
            _report.acceptedHumanCastsAtStart = controller.AcceptedHumanCastCount;
            double previousTime = Now;
            double sampleStart = previousTime;
            double sampleDeadline = previousTime + SessionRpgProfileCaptureOptions.SampleDeadlineSeconds;
            ulong previousTimingTimestamp = 0;
            while (_report.sampleCount < _options.Samples && Now < sampleDeadline)
            {
                yield return null;
                long allocated = previousAllocated >= 0 ? ReadAllocatedBytes() : -1;
                double now = Now;
                int index = _report.sampleCount;
                var sample = new ObservedFrame
                {
                    frame = Time.frameCount,
                    elapsedSeconds = now - sampleStart,
                    interFrameMilliseconds = (now - previousTime) * 1000d,
                    mainThreadAllocatedBytes = allocated >= 0 && previousAllocated >= 0 ? allocated - previousAllocated : -1,
                    controllerEnabled = controller.enabled,
                    playing = controller.IsGrindingCaptureReady,
                    humanAlive = human != null && human.IsAlive,
                    residentChunks = streamer.ResidentChunkCount,
                    loadedChunks = streamer.LoadedChunkCount,
                    cpuFrameMilliseconds = -1d,
                    cpuMainThreadMilliseconds = -1d,
                    cpuRenderThreadMilliseconds = -1d,
                    gpuFrameMilliseconds = -1d,
                };
                if (sample.playing) { _report.playingFrames++; }
                if (sample.humanAlive) { _report.humanAliveFrames++; }
                if (sample.controllerEnabled) { _report.controllerEnabledFrames++; }
                for (int counter = 0; counter < _counters.Length; counter++) { _counters[counter].Sample(index); }
                if (_report.frameTimingEnabled)
                {
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, _timing) > 0 &&
                        _timing[0].frameStartTimestamp != 0 && _timing[0].frameStartTimestamp != previousTimingTimestamp)
                    {
                        FrameTiming timing = _timing[0];
                        previousTimingTimestamp = timing.frameStartTimestamp;
                        sample.frameTimingTimestamp = timing.frameStartTimestamp;
                        sample.cpuFrameMilliseconds = PositiveOrUnavailable(timing.cpuFrameTime);
                        sample.cpuMainThreadMilliseconds = PositiveOrUnavailable(timing.cpuMainThreadFrameTime);
                        sample.cpuRenderThreadMilliseconds = PositiveOrUnavailable(timing.cpuRenderThreadFrameTime);
                        sample.gpuFrameMilliseconds = PositiveOrUnavailable(timing.gpuFrameTime);
                    }
                }
                _report.frames[index] = sample;
                _report.sampleCount++;
                previousTime = now;
                previousAllocated = allocated;
            }
            _report.observedSeconds = Now - sampleStart;
            _report.acceptedHumanCastsAtEnd = controller.AcceptedHumanCastCount;
            Complete(_report.sampleCount == _options.Samples ? "completed" : "sample-timeout",
                string.Empty, _report.sampleCount == _options.Samples ? 0 : 3);
        }

        private static double PositiveOrUnavailable(double value) => value > 0d && !double.IsInfinity(value) ? value : -1d;

        private static long ReadAllocatedBytes()
        {
#if ENABLE_IL2CPP
            // Unity 6000.5's native implementation asserts/returns zero rather
            // than throwing NotSupportedException. Never invoke it on IL2CPP:
            // a native assertion or a false zero would invalidate the evidence.
            return ProfileCaptureMeasurementContract.UnavailableAllocationBytes;
#else
            try { return GC.GetAllocatedBytesForCurrentThread(); }
            catch (NotSupportedException) { return ProfileCaptureMeasurementContract.UnavailableAllocationBytes; }
#endif
        }

        private void Complete(string status, string error, int exitCode)
        {
            if (_finished) { return; }
            _finished = true;
            StopAllCoroutines();
            DisposeRecorders();
            UnityEngine.Application.logMessageReceived -= ObserveLog;
            if (_report != null)
            {
                if (status == "completed" && _report.runtimeErrors > 0)
                {
                    status = "completed-with-runtime-errors";
                    exitCode = 3;
                }
                _report.status = status;
                _report.error = error;
                _report.finishedUtc = DateTime.UtcNow.ToString("O");
                _report.counters = _counters != null ? new CounterEvidence[_counters.Length] : Array.Empty<CounterEvidence>();
                for (int index = 0; index < _report.counters.Length; index++)
                {
                    _report.counters[index] = _counters[index].Evidence;
                    _report.counters[index].summary = ProfileMetricSummary.From(_report.counters[index].values, _report.sampleCount);
                    _report.counters[index].emissionSummary = ProfileCounterEmissionSummary.From(
                        _report.counters[index].values, _report.counters[index].emissions, _report.sampleCount);
                    if (_report.counters[index].available && _report.counters[index].summary.validSamples == 0)
                    {
                        _report.counters[index].reason = "Registered counter emitted no samples in the observed window.";
                    }
                    Array.Resize(ref _report.counters[index].values, _report.sampleCount);
                    Array.Resize(ref _report.counters[index].emissions, _report.sampleCount);
                }
                var frameIntervals = new double[_report.sampleCount];
                var allocations = new double[_report.sampleCount];
                var cpuFrame = new double[_report.sampleCount];
                var cpuMain = new double[_report.sampleCount];
                var cpuRender = new double[_report.sampleCount];
                var gpuFrame = new double[_report.sampleCount];
                for (int index = 0; index < _report.sampleCount; index++)
                {
                    frameIntervals[index] = _report.frames[index].interFrameMilliseconds;
                    allocations[index] = _report.frames[index].mainThreadAllocatedBytes;
                    cpuFrame[index] = _report.frames[index].cpuFrameMilliseconds;
                    cpuMain[index] = _report.frames[index].cpuMainThreadMilliseconds;
                    cpuRender[index] = _report.frames[index].cpuRenderThreadMilliseconds;
                    gpuFrame[index] = _report.frames[index].gpuFrameMilliseconds;
                }
                _report.interFrameMilliseconds = ProfileMetricSummary.From(frameIntervals, frameIntervals.Length);
                _report.mainThreadAllocatedBytes = ProfileMetricSummary.From(allocations, allocations.Length);
                _report.cpuFrameMilliseconds = ProfileMetricSummary.From(cpuFrame, cpuFrame.Length);
                _report.cpuMainThreadMilliseconds = ProfileMetricSummary.From(cpuMain, cpuMain.Length);
                _report.cpuRenderThreadMilliseconds = ProfileMetricSummary.From(cpuRender, cpuRender.Length);
                _report.gpuFrameMilliseconds = ProfileMetricSummary.From(gpuFrame, gpuFrame.Length);
                Array.Resize(ref _report.frames, _report.sampleCount);
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(_options.OutputPath));
                    // Never replace earlier evidence even if another process created it
                    // after startup validation. Serialization is outside measurement.
                    using (var stream = new FileStream(_options.OutputPath, FileMode.CreateNew, FileAccess.Write))
                    using (var writer = new StreamWriter(stream)) { writer.Write(JsonUtility.ToJson(_report, true)); }
                    Debug.Log("Session RPG live profile " + status + ": " + _options.OutputPath);
                }
                catch (Exception writeError) { Debug.LogException(writeError); exitCode = 3; }
            }
            if (!string.IsNullOrEmpty(error)) { Debug.LogError(error); }
            Quit(exitCode);
        }

        private void ObserveLog(string condition, string stackTrace, LogType type)
        {
            if (_report != null && (type == LogType.Error || type == LogType.Exception || type == LogType.Assert))
            {
                _report.runtimeErrors++;
            }
        }
        private void OnApplicationQuit()
        {
            if (!_finished && _options != null) { Complete("cancelled", "Player closed before sampling completed.", 4); }
        }
        private void OnDestroy()
        {
            UnityEngine.Application.logMessageReceived -= ObserveLog;
            DisposeRecorders();
        }
        private void DisposeRecorders()
        {
            if (_counters == null) { return; }
            foreach (RecordedCounter counter in _counters) { counter.Dispose(); }
        }
        private static void Quit(int exitCode)
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            UnityEngine.Application.Quit(exitCode);
#endif
        }

        private static AvailableMetric[] DescribeAvailableRenderMetrics()
        {
            // Startup-only inventory exposes unsupported/misnamed counters without
            // silently replacing the requested metric with a different statistic.
            var handles = new List<ProfilerRecorderHandle>(256);
            var metrics = new List<AvailableMetric>(32);
            ProfilerRecorderHandle.GetAvailable(handles);
            foreach (ProfilerRecorderHandle handle in handles)
            {
                ProfilerRecorderDescription description = ProfilerRecorderHandle.GetDescription(handle);
                string name = description.Name;
                if (name.IndexOf("Draw Calls", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Batches", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("SetPass", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Triangles", StringComparison.OrdinalIgnoreCase) < 0 &&
                    name.IndexOf("Render Thread", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                metrics.Add(new AvailableMetric
                {
                    name = name,
                    category = description.Category.Name,
                    unitType = description.UnitType.ToString(),
                    dataType = description.DataType.ToString(),
                    flags = description.Flags.ToString(),
                });
            }
            return metrics.ToArray();
        }

        private sealed class RecordedCounter : IDisposable
        {
            private ProfilerRecorder _recorder;
            private bool _disposed;
            public readonly CounterEvidence Evidence;
            public RecordedCounter(ProfilerCategory category, string name, string units, int samples)
            {
                Evidence = new CounterEvidence { name = name, category = category.Name, units = units,
                    values = new double[samples], emissions = new long[samples] };
                Array.Fill(Evidence.values, -1d);
                Array.Fill(Evidence.emissions, -1L);
                try
                {
                    _recorder = ProfilerRecorder.StartNew(category, name, 1);
                    Evidence.available = _recorder.Valid;
                    Evidence.reason = _recorder.Valid ? string.Empty : "Counter is unavailable on this build/platform.";
                    if (_recorder.Valid)
                    {
                        Evidence.unitType = _recorder.UnitType.ToString();
                        Evidence.dataType = _recorder.DataType.ToString();
                    }
                }
                catch (Exception error) { Evidence.reason = error.GetType().Name + ": " + error.Message; }
            }
            public void Sample(int index)
            {
                if (!_disposed && _recorder.Valid && _recorder.Count > 0)
                {
                    // Capacity remains one, default aggregation and sampling phase
                    // are unchanged. Reading one sample retains Value and Count from
                    // the same record; zero/multiple emissions are never normalized.
                    ProfilerRecorderSample sample = _recorder.GetSample(0);
                    Evidence.values[index] = sample.Value;
                    Evidence.emissions[index] = sample.Count;
                }
            }
            public void Dispose()
            {
                if (_disposed) { return; }
                _disposed = true;
                if (_recorder.Valid) { _recorder.Dispose(); }
            }
        }

        [Serializable]
        private sealed class CounterEvidence
        {
            public string name, category, units, reason;
            public string unitType = "unavailable", dataType = "unavailable";
            public string recorderOptions = "Default (SumAllSamplesInFrame | WrapAroundWhenCapacityReached); capacity 1";
            public string qualification = "Raw recorder-frame value, not guaranteed aligned with the rendered frame. Zero/multiple emissions may reflect aggregation or delayed flushes; do not divide, filter, or interpret an unqualified rendering p95 as per-rendered-frame cost.";
            public bool available;
            public double[] values;
            public long[] emissions;
            public ProfileMetricSummary summary;
            public ProfileCounterEmissionSummary emissionSummary;
        }
        [Serializable]
        private sealed class AvailableMetric
        {
            public string name, category, unitType, dataType, flags;
        }
        [Serializable]
        private struct ObservedFrame
        {
            public int frame, residentChunks, loadedChunks;
            public bool controllerEnabled, playing, humanAlive;
            public long mainThreadAllocatedBytes;
            public ulong frameTimingTimestamp;
            public double elapsedSeconds, interFrameMilliseconds;
            public double cpuFrameMilliseconds, cpuMainThreadMilliseconds, cpuRenderThreadMilliseconds, gpuFrameMilliseconds;
        }
        [Serializable]
        private sealed class ProfileReport
        {
            public int schemaVersion = 3;
            public string epoch, status, error, startedUtc, finishedUtc;
            public string scenario = "Stationary live PvE camp observation; no automated human attacks or movement.";
            public string limitations, scriptingBackend, threadAllocationCounter;
            public string platform, unityVersion, operatingSystem, processor, graphicsDevice, graphicsApi, qualityTier;
            public string specialization, diagnosticSaveDirectory;
            public bool developmentBuild, frameTimingEnabled;
            public int width, height, targetFrameRate, vSyncCount, camp, requestedSamples, sampleCount;
            public int playingFrames, humanAliveFrames, controllerEnabledFrames;
            public int acceptedHumanCastsAtStart, acceptedHumanCastsAtEnd, runtimeErrors;
            public double warmupSeconds, observedSeconds;
            public ProfileMetricSummary interFrameMilliseconds, mainThreadAllocatedBytes;
            public ProfileMetricSummary cpuFrameMilliseconds, cpuMainThreadMilliseconds, cpuRenderThreadMilliseconds, gpuFrameMilliseconds;
            public CounterEvidence[] counters;
            public AvailableMetric[] availableRenderMetrics;
            public ObservedFrame[] frames;
        }
    }
}
#endif
