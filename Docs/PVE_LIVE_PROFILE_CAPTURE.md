# Bounded live-player profiling

This development-only diagnostic observes a **stationary, live PvE player** near
one camp. It is not the visual reference capture: that separate runner freezes
the controller and stages a presentation tableau, which cannot establish live
gameplay performance. Do not run both flags together.

The new hook is compiled only for Editor, Development or `SESSION_RPG_CAPTURE`
builds and is inert without `-sessionRpgProfile`. It does not change quality,
target frame rate, camera, combat rules, input, bots, or network behavior. It does
not attack, move, or make the human invulnerable. The player can die naturally;
the report retains per-frame alive/playing/controller-enabled state so a quiet
post-death scene cannot masquerade as sustained combat. It is not a worst-case
spell test or ten-session soak.

## Reproduce

Run the normal doctor, validation and macOS build from the project first, with no
other Unity Editor process active. Use a fresh evidence directory/path. The
following launch uses the current capture build, but does not take a screenshot:

```sh
'../outputs/SessionRpg-mac-capture.app/Contents/MacOS/Session RPG Prototype' \
  -sessionRpgProfile '/absolute/project/build/review/live-profile-YYYYMMDD/camp1-1920.json' \
  -sessionRpgProfileCamp 1 \
  -sessionRpgProfileSpecialization Discipline \
  -sessionRpgProfileWarmupSeconds 10 \
  -sessionRpgProfileSamples 600 \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -logFile '/absolute/project/build/review/live-profile-YYYYMMDD/camp1-1920.log'
```

Do not run another player, Unity rebuild/test/build, DCC render, or heavy compute
job during an acceptance measurement. A concurrent smoke run can prove the
diagnostic works, but its timings are contaminated and must be labeled as such.
Record the host load and thermal-state limitations with the result.

Default parameters are camp `1`, Discipline, `10 s` warmup and `600` samples.
Accepted camp IDs are `0..6`, specialization names are the seven playable stable
identities, warmup is `5..30 s`, and sample count is `120..1800`. Setup waits at
most `10 s` for the controller and `30 s` for the streamed camp/playing state.
Sampling has a `60 s` wall-clock deadline and the runner has a `120 s` watchdog.
They are evaluated when Unity processes frames, not by an external watchdog.
Exit codes are `0` for completed observation, `2` for invalid/conflicting options
or an existing output, `3` for setup/timeout/write/runtime-error failure, and `4`
for an observed early player close. Force-killing the process cannot guarantee a
report. A completed observation is not a performance-budget pass.

The report path must be absolute `.json` outside any `Assets` directory. Existing
files are never overwritten. Per-run PvE XP uses a unique diagnostic-save
directory beside the report. Existing local session records retain the normal
controller behavior; this diagnostic does not rewrite profile settings or claim
complete process sandboxing. Evidence remains outside Unity `Assets`.

### Android Development / IL2CPP — schema v3 safety correction

The Android builder produces a debuggable ARM64/IL2CPP APK with exported
`com.unity3d.player.UnityPlayerGameActivity`. Its generated activity forwards the
`unity` string intent extra into Unity's startup arguments. The diagnostic is
included by `DEVELOPMENT_BUILD` and does not reject Android. However, **do not use
the earlier schema-v1/v2 profiling binary on IL2CPP**: the pinned Unity 6000.5.6f1
native implementation of `GC.GetAllocatedBytesForCurrentThread` calls
`IL2CPP_NOT_IMPLEMENTED_ICALL`, which expands to a native assertion, followed by
a zero return. It does not reliably throw the managed `NotSupportedException`
that the original observer caught. It could abort or falsely report zero.

Source evidence is the installed Unity
`Resources/Scripting/il2cpp/libil2cpp/icalls/mscorlib/System/GC.cpp` (method at
line 34) and `il2cpp-config.h` (macro at line 238). Schema v3 uses an
`ENABLE_IL2CPP` compile-time branch that **never invokes** this unsupported call
and writes `-1`/zero valid samples with an explicit unavailability reason.
The independent Unity `GC Allocated In Frame` recorder remains unchanged and
must still be interpreted according to its own availability and scope.

Schema v3 also records the scripting backend and actual runtime platform in its
limitations, replacing the erroneous hardcoded desktop-only label. This makes
Android evidence identifiable; it does not make a short device run a sustained
thermal, touch, multi-device or production acceptance result. The previous
desktop reports remain unchanged historical evidence. Fresh tests/build/install
and an actual device run are required before treating the corrected path as
runtime-verified.

After verifying the app's actual private directory with
`adb shell run-as com.sessionrpg.prototype pwd`, the launch shape is:

```sh
adb shell "am start -S -W -n com.sessionrpg.prototype/com.unity3d.player.UnityPlayerGameActivity --es unity '-sessionRpgProfile /data/user/0/com.sessionrpg.prototype/files/codex-review/phone-high-camp1-01.json -sessionRpgProfileCamp 1 -sessionRpgProfileSpecialization Discipline -sessionRpgProfileWarmupSeconds 10 -sessionRpgProfileSamples 600'"
adb exec-out run-as com.sessionrpg.prototype cat files/codex-review/phone-high-camp1-01.json
```

The example assumes the verified directory is `/data/user/0/com.sessionrpg.prototype`;
use the actual reported directory if different. `-S` restarts only this app and
does not clear data. A fresh process is required because the activity is
`singleTask` and the diagnostic initializes once. Use a fresh output filename,
retain the returned JSON locally outside `Assets`, and capture the app's logcat
separately. Do not use `pm clear`, root, shared-storage permission changes or
desktop resolution flags. The report should retain the actual phone viewport,
quality and graphics API. `am start -W` success proves activity launch, not
diagnostic completion; verify JSON status/counts and logs afterwards.
[Unity Android startup arguments](https://docs.unity3d.com/6000.0/Documentation/Manual/android-custom-activity-command-line.html),
[Android activity-manager options](https://developer.android.com/tools/adb),
[app-private storage](https://developer.android.com/training/data-storage/app-specific).

## Evidence interpretation

The JSON contains an epoch, UTC timestamps, status/error, platform/Development
flag, CPU/GPU/API, resolution, quality tier, frame-rate/vsync settings, configured
camp/spec, raw samples, counters and nearest-rank p50/p95 summaries. Device
serials, account IDs and network identifiers are not collected.

- `interFrameMilliseconds` is Stopwatch time between the same coroutine phase
  on adjacent frames. It includes waits, frame limiting, OS scheduling, other
  host work, and observer overhead. It is not work-exclusive main-thread cost.
- `mainThreadAllocatedBytes` is the difference from
  `GC.GetAllocatedBytesForCurrentThread` between those phases when supported.
  On IL2CPP it is unavailable (`-1`), never a successful zero-allocation sample.
  On supported runtimes it includes the
  diagnostic observer and excludes other threads. Samples are preallocated;
  sorting, serialization, log writing and report I/O happen after measurement.
  A nonzero result needs a proper allocation call-stack investigation; it is not
  automatically attributable to Core or the environment.
- Unity `ProfilerRecorder` captures the exact named counters `Main Thread`,
  `Render Thread`, `GC Allocated In Frame`, `Total Used Memory`,
  `Total Reserved Memory`, `Draw Calls Count`, `SetPass Calls Count`, and
  `Triangles Count`. Their values are the latest completed recorder frame, which
  may lag the observer's state row. Thread-marker timing may include waits.
  Memory is Unity-tracked memory, not process RSS or proof of leak freedom.
- Schema v2 additionally retains each recorder sample's emission count, declared
  unit/type, an available render-metric inventory and FrameTimingManager's raw
  timestamp. It does not change the recorder options, capacity, sampling phase,
  or normalize measured values. With default frame summation, `sample.Count`
  identifies how many emitted samples were accumulated, not how many objects
  rendered. [Unity recorder options](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderOptions.html),
  [sample-count contract](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Unity.Profiling.ProfilerRecorderSample.Count.html).
- `FrameTimingManager` is sampled only when its existing feature is enabled.
  Unique returned frame timestamps gate CPU/GPU samples. The diagnostic never
  turns on PlayerSettings support; missing or nonpositive timing values remain
  unavailable. Availability does not make desktop GPU timing Android evidence.
- A value of `-1`, zero valid samples or an unavailable reason is **missing
  evidence**, not zero cost. Registered-but-silent counters are explicitly named.
  Raw arrays contain only the observed prefix; unobserved capacity is discarded.

Interpret p95 only alongside status, sample count, playing/alive/controller
counts and runtime error count. The current planning budgets are in
`DEMO_READINESS.md`. Desktop data can identify a problem and reproduce a local
baseline; it cannot pass the Android/iOS thermal, touch, overdraw, GPU, memory
growth or physical-device acceptance gates. The added parser/statistics tests
verify bounds and arithmetic, not performance.

## V1 measured stationary desktop baseline — 2026-09-08

Source evidence is retained unchanged at
`build/review/pve-woodland-polish-20260908/v1-live-camp1-1920.json`
and its same-basename `.log`. It is schema v1; the schema-v2 emission metadata
described above was added after reviewing this result and does not retroactively
explain its anomalies.

The run completed with exit `0`, no measured runtime errors, `600/600` frames
playing, human alive and controller enabled, after `10 s` warmup. Observation
lasted `10.054 s`; no human casts occurred. Hardware was an Apple M3 Pro, macOS
26.3, Metal, Unity 6000.5.6f1 Development player, High tier, 1920×1080, target
60 FPS/vsync off. It is a stationary camp-1 Discipline sample, not a stress test.

| Timing source | Valid samples | Median | p95 |
|---|---:|---:|---:|
| Stopwatch adjacent-frame interval | 600 | 16.732 ms | 17.299 ms |
| FrameTimingManager CPU frame, including waits | 600 | 16.672 ms | 17.217 ms |
| FrameTimingManager CPU main thread | 600 | 3.366 ms | 4.059 ms |
| FrameTimingManager CPU render thread | 600 | 1.421 ms | 2.036 ms |
| FrameTimingManager desktop GPU | 600 | 4.930 ms | 6.814 ms |

Main-thread managed allocation deltas were `0 B` for all 600 observed intervals.
The separate Unity `GC Allocated In Frame` counter was `320 B` median/p95 and
`368 B` maximum. These measurements have different scopes; the source of the
nonzero Unity-wide value is not established. **Complete gameplay allocation-free
acceptance is not passed.** Unity defines that counter as per-frame allocation
on its garbage-collected heap, not exclusively the coroutine's observed thread.
[Unity memory-counter reference](https://docs.unity3d.com/6000.0/Documentation/Manual/ProfilerMemory.html).

The raw rendering counters need qualification:

- `SetPass Calls Count`: exactly 42 zeros, 516 values of 40 and 42 values of 80.
- `Triangles Count`: 39 zeros, 522 values around 335,540–335,642, and 39 values
  around 671,080–671,284. Thirty-seven zero indices coincide with SetPass zeros.
- `Draw Calls Count` and the `Render Thread` **recorder** were unavailable.
  Render-thread timing above came independently from FrameTimingManager.

Equal numbers of zero/double values suggest delayed or multiple recorder flushes,
but that is an inference, not a proven Unity bug or a corrected result. The v1
runner did not retain `ProfilerRecorderSample.Count`, so the cause cannot be
verified from those rows. Do not erase zero values, divide spikes, substitute
means, or call raw rendering p95 a clean per-rendered-frame measurement. The
existing default `StartNew`/`LastValue` pattern agrees with Unity's documented
example; no unsupported phase or options change has been made.
[Unity rendering-counter example](https://docs.unity3d.com/6000.0/Documentation/Manual/ProfilerRendering.html).

Unity-tracked used memory started at `655.235 MiB`, peaked at `656.876 MiB`, and
ended `223,138 B` below its starting value. This ten-second observation does not
establish leak freedom or ten-session growth. The native shutdown log still
contains the previously observed thread-finalization and `MemoryLeaks` warnings,
after the diagnostic's measured window. A clean shutdown/memory claim remains
open. Desktop frame pacing p95 also exceeds the nominal `16.7 ms` planning
budget; no all-platform or production-readiness pass is inferred from the lower
CPU/GPU work figures.

## V2 metadata verification — 2026-09-08

Retained evidence:
`build/review/pve-woodland-polish-20260908/v2-live-camp1-1920.json`
and its same-basename `.log`. The schema-v2 run completed with exit `0`,
`600/600` playing/alive/controller-enabled frames, no measured runtime errors,
`10 s` warmup and `10.017 s` observation. It used the same recorded desktop
hardware/API, High tier, resolution and stationary camp/spec as V1. **These were
separate host runs without thermal control, not a controlled before/after
performance comparison.**

All 600 returned FrameTimingManager timestamps are unique and nonzero. p95
values are `17.063 ms` adjacent-frame interval, `16.749 ms` CPU frame including
waits, `3.727 ms` CPU main thread, `1.954 ms` CPU render thread and `7.313 ms`
desktop GPU. Main-thread allocation deltas remain `0 B` for all observations;
the broader Unity GC counter again reports `320 B` median/p95 and `368 B`
maximum. These figures do not close the allocation, frame-pacing or mobile
acceptance gates.

The new metadata confirms **recorder aggregation**, not clean per-rendered-frame
peaks: for both SetPass and triangles, 129 observed records contain zero
emissions, 342 contain one, and 129 contain two—600 emissions in total. Every
zero value has zero emissions. The doubled values carry two accumulated
emissions. Raw SetPass p95 remains `80`; raw triangle p95 is `670,888`, with a
median of `335,444`. No zeros were discarded and no accumulated values divided.
The emission pattern explains why these raw recorder-window peaks cannot be
presented as the workload of one rendered frame; it does not identify the
underlying engine scheduling/flush cause.

`Draw Calls Count` and `Render Thread` remain unavailable under those exact
requested names. The startup inventory lists separate Standard/SRP Batcher/BRG
draw-call metrics and `CPU Render Thread Frame Time`, but the diagnostic has
not sampled or combined them as a substitute. Total draw-call acceptance remains
**not measured**; the independent FrameTimingManager render-thread result above
remains valid desktop evidence.

### Precise shutdown finding

The V1 live log records normal diagnostic completion at line 41, then five
`Thread 0x… may have been prematurely finalized` warnings at lines 54 and
60–63, surrounding Physics/Input shutdown. After `CodeReloadManager destroyed`,
line 65 contains a native `MemoryLeaks` report with phase `Immediate` and
`3,306,544 B` (`3.153 MiB`) at `10:01:54.923 UTC`, about `166 ms` after the JSON
completion timestamp. The thread warnings lack individual timestamps and
allocation/call stacks; the earlier completion stack is a normal `Debug.Log`
stack, not an exception. V2 repeats five finalization warnings and reports
`3,306,464 B` at `10:14:14.072 UTC`, about `171 ms` after JSON completion.

The same-revision ordinary Discipline and Fire screenshot logs independently
contain five finalization warnings and approximately `3.149..3.161 MiB` of
shutdown allocations. Active profiler recorders therefore are not necessary to
trigger the pattern. Reviewed source already disposes recorders, unbinds the
controller, deactivates streamed scenes and destroys the runtime reflection.
These logs do not establish which project or engine allocation survives, nor a
specific missing-disposal fix. Classify it as an **unresolved shutdown/memory
quality gate**, not a proven crash, data corruption, `3 MiB` growth per session,
or confirmed harmless engine-only issue.

The subsequent same-build Home-screen control completed with exit `0`; evidence
is `build/review/pve-woodland-polish-20260908/v2-home-shutdown-control.log` and its
same-basename screenshot. It did not enter PvE or activate the profiling runner,
yet repeats five premature-finalization warnings and a native shutdown report of
`3,307,926 B` at `10:15:09.613 UTC`. This demonstrates that entering PvE and starting its profiling
recorders are not necessary for the warnings. It does not prove that every
project component is uninvolved: the Home control still loads Bootstrap and
uses the same Unity/runtime and screenshot-based exit path.

The next safe step is to attribute allocations using supported native allocation
stacks or memory snapshots and, if needed, compare a minimal same-version player
against Bootstrap. Do not guess a cleanup fix, claim an engine-only cause, or
change the Unity version solely from these aggregate labels. No cleanup code or
physical-device state was changed during this audit.

## Final projection-corrected revision — 2026-09-08

Evidence: `build/review/pve-woodland-polish-20260908/reviewed-live-camp1-1920.json`
and matching `.log`. This schema-v2 observation followed the Vista-only triplanar
correction (18 texture fetches per shaded fragment instead of six). It ran alone,
without concurrent Unity/DCC/capture work, using the same recorded Apple M3 Pro,
Metal, High, 1920×1080 Development configuration as V1/V2. Thermal conditions were
not controlled: **do not present the differences between runs as a measured
optimization or regression**.

Exit `0`, status `completed`, no measured runtime errors, ten-second warmup,
10.066-second observation, 600/600 alive/playing/controller-enabled samples,
zero human casts and 600 unique nonzero timing timestamps.

| Timing source | Valid samples | Median | p95 |
|---|---:|---:|---:|
| Stopwatch adjacent-frame interval | 600 | 16.756 ms | 17.400 ms |
| FrameTimingManager CPU frame, including waits | 600 | 16.672 ms | 17.406 ms |
| FrameTimingManager CPU main thread | 600 | 3.289 ms | 3.861 ms |
| FrameTimingManager CPU render thread | 600 | 1.498 ms | 2.045 ms |
| FrameTimingManager desktop GPU | 600 | 4.006 ms | 6.624 ms |

All 600 main-thread allocation deltas remain `0 B`; the broader Unity counter
remains `320 B` median/p95 and `368 B` maximum. Its origin is not attributed by
this diagnostic. Main-thread observed allocation check passes; the complete
gameplay zero-allocation gate does not. The p95 interval still exceeds the
nominal `16.7 ms` planning budget. Neither desktop work figures nor successful
Android packaging demonstrate physical-phone performance.

SetPass has 43 zero-emission, 514 single-emission and 43 double-emission records;
triangles have 40, 520 and 40 respectively. Both total 600 emissions. Raw p95
values are `80` SetPass / `670,888` triangles (medians `40` / `335,444`), not
clean per-rendered-frame peaks. No values were discarded, divided or replaced.
The exact `Draw Calls Count` and `Render Thread` recorder names remain
unavailable; independent FrameTimingManager render-thread values are above.

Unity-tracked used memory peaked at `689,005,785 B`, ending `420,511 B` below
its starting value; reserved memory rose by `2 MiB`. This is neither process RSS
nor a growth/leak/soak conclusion. Final live shutdown still contains five
premature-thread-finalization warnings and native `MemoryLeaks` of `3,307,196 B`.
All eight final screenshot logs also contain shutdown warnings (five thread
warnings each, seven for the Frost Nova showcase; shutdown allocations
`3,291,854..3,313,232 B`). Completion/error scans are otherwise clean. The
unresolved shutdown gate and required attribution work described above remain.

## Current device-access boundary

Earlier read-only discovery on 2026-09-08 found the Unity-bundled Android `adb` binary but
no listening adb server; no server was started and no physical Android query,
install or launch was performed. `xcrun devicectl list devices` could not run
because `devicectl` is unavailable in the configured developer tools. Physical
device checks remain **not run**.

Subsequently the user connected an authorized Neo11 and requested High-quality
device validation. Hardware checks passed. After user approvals, the corrected
schema-v3 APK installed successfully and its capture activity launched. The
phone then disconnected before PNG/log retrieval. No corrected live profile,
touch or thermal test has been performed; activity launch is not visual or
performance acceptance. See [Neo11 device evidence](PVE_NEO11_DEVICE_EVIDENCE.md)
for exact revisions, the resolved diagnostic defects and the current reconnect
requirement. Do not cite prior desktop allocation zeros as IL2CPP measurements.
