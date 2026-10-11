# Sunbreak woodland polish — 2026-09-08

Scope:

Continue toward the user's production-quality visual target in the actual local
PvE Leveling scene. Unity/editor presentation, material/shader authoring, source
derivatives, bounded existing-FBX node admission, generated contracts, optional
development profiling, and documentation.
No Core combat/cover/elevation, progression, camera, HUD, server/network, package,
cloud, purchase, source download or license changes. Other game modes remain local.

Source changed:

- `SunbreakCanopyAuthoring.cs` and tests: new explicit source-derived leaf
  representation, retaining the scanned trunk, measured atlases, complete paired
  cluster topology, UVs and frames. Whole cluster pairs expand uniformly within
  the original tree's envelope; no individual vertex clamp. 9,328 triangles/tree.
  Immutable V1 source/manifest and original deliveries are preserved.
- `Tools/Art/Blender/review_sunbreak_canopy.py` and
  `SourceArt/Environment/SunbreakCanopyV2/README.md`: reproducible 64-transparent-
  bounce, gameplay-scale DCC comparison. This is a source inspection, not game
  or mobile performance acceptance.
- `AuthoringAssets.cs`/`SunbreakBasinAuthoring.cs`: build/validate the derivative,
  swap the focal three trees, add two source ferns, and replace two middle-distance
  prototype groves (landmarks 2/9) with three/one source-derived trees respectively,
  and four ferns in each grove. Landmark 2 adopts the low eroded bank instead of
  its former artificial-looking high plinth. Existing 65,000-triangle/80-renderer chunk caps
  and exact Core unions stay.
- `SunbreakLandscapeSurfaceAuthoring.cs`/tests and `SunbreakTerrain.shader`:
  dedicated measured rock/grass material, actual scan scale, authored altitude/
  slope-dependent vegetation weights. The optional vertex-layer variant avoids
  stretching the near ground's clamped blend map over distant hills. Both forward
  and depth-normal passes share the same layer decision; no invented height/AO.
  After the V3 orbit rejection, this Vista-only variant uses continuous signed
  world-space triplanar projection and whiteout-blended world normals. Neutral
  maps preserve the geometric normal; both rendering passes use the same result.
  Material scales are directly 1/1.5 and 1/2 cycles per meter. Near material
  authoring explicitly disables the variant and retains its existing sampling.
  Vista's per-shaded-fragment cost rises from six to **18 measured texture
  fetches**; it is not a free seam fix. Twelve new EditMode cases and a native
  PlayMode variant verify arithmetic, routing and explicit sampling contracts.
- `SunbreakFortAssets`/tests, landmark-7 composition and source intake metadata:
  admit the existing 654-triangle thin corner from the unchanged FBX. Replace
  three mismatched-scale foreground pieces with one matching corner. The wall
  and corner share 0.52× scale and 0.05 m base; actual triangle sections
  across four heights guard the joint, with a separate solid-return test.
  Architecture drops from nine / 8,524 to seven / 7,924 triangles, two fewer
  mesh renderers. Original source files, hashes and all other tower placements
  stay unchanged. This is coherent construction, not a new damaged-ruin kit.
- `SunbreakVistaAuthoring.cs`, new `SunbreakVistaWoodlandAuthoring.cs`, their
  tests, and generator wiring: 84 outside-Core trees in 14 separate groves, actual
  3D woody branches plus intersecting measured cluster cards. Whole-crown clearance,
  embedded terrain roots, finite geometry and bounded material batches are tested.
  Resident Vista is 27,042 triangles / four renderers, including retained terrain.
  The first 176-tree / 27,138-triangle version was rejected in actual native
  frames for long exposed trunks, thin crowns and overly uniform green slopes.
  V2 concentrates the budget into lower, broader overlapping stands and reveals
  more rock through less uniform, lower-altitude grass coverage.
  The native V2 review still found rounded, weak far slopes; V3 changes only
  the far crest beyond the wooded shoulders to asymmetric rock faces/gullies.
- New development-only `SessionRpgProfileCapture.cs` and options/tests: an opt-in
  bounded **live**, stationary camp observation distinct from frozen screenshots.
  Raw samples, counter availability, player/session state and limitations are
  recorded. No new component/update in normal unrequested play. See
  [profile capture procedure](PVE_LIVE_PROFILE_CAPTURE.md).
- Basin/measured vegetation/generated shader contract tests updated with the same
  changes. Source/intake/evidence documentation records the new candidate without
  changing earlier acceptance claims.
- `DESIGN.md`, `Docs/DEMO_READINESS.md` and `Docs/VISUAL_QUALITY.md` align the
  later user-specified minimum phone tier; no renderer/runtime setting changes.

Generated changed:

Final SHA256 comparison against the **1,761-file turn-entry inventory** found
**37 changed, 27 new (including metas), zero removed**; 1,788 files now inventoried.
This is not a comparison against Git HEAD: the shared worktree was already dirty.
Every exact path and before/after hash is retained at
`build/review/pve-woodland-polish-20260908/generated-asset-delta.json`.

Intended generated content changes: the four focal vegetation batches, landmark-2
bank, mountain horizon, six new landmark-2/9 vegetation batches, V2 leaf mesh,
admitted source-corner mesh, two Vista woodland meshes, three Vista materials,
six existing Sunbreak materials with explicit vertex-layer defaults, nine basin
chunk scenes and Bootstrap composition.

The full supported `rebuild` also rewrote four animation controllers,
Combatant.prefab, the three generated arena prefabs and seven Ashen Causeway
chunk scenes. No animation/other-mode authoring source was changed by this pass;
those regenerated files are included in the inventory and regression checks,
not silently excluded or claimed byte-identical. No generated YAML was hand-
edited. `ProjectSettings` and the existing generated settings/catalog files are
byte-identical to the turn-entry inventory after final validation. Original art
deliveries remain; only three obsolete foreground scene placements were replaced.

Commands (fresh):

- `Tools/Agent/unity.sh doctor`: passed, Unity 6000.5.6f1, Editor closed.
- All actual Unity authoring/testing/build invocations use
  `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh ...`, serially.
- `rebuild` passed (`../outputs/agent/20260908-125320-rebuild-63637/unity.log`).
- `validate` passed (`../outputs/agent/20260908-125424-validate-64209/unity.log`).
- V1 `build-macos` passed (`../outputs/agent/20260908-125458-build-macos-64463/unity.log`).
- V1 `test-all` passed: 978/978 EditMode and 250/250 PlayMode, zero failed/skipped
  (`20260908-125654-test-edit-65332/results.xml`,
  `20260908-125739-test-play-65332/results.xml` in `../outputs/agent/`).
- V1 native camp-1 Discipline 1920×1080 and Fire 1280×720 captures exited 0.
  Near canopy improved; distant woodland/material composition rejected, above.
- V1 live profile completed: 600 frames after a ten-second warmup, no concurrent
  Unity/DCC/capture load. Files are under
  `build/review/pve-woodland-polish-20260908/v1-*`.
- V2 `rebuild`, `validate`, `build-macos` passed:
  `20260908-130841-rebuild-70146`, `20260908-130949-validate-70726`,
  `20260908-131046-build-macos-71175` (each `unity.log` in `../outputs/agent/`).
- V2 native camp-1 Discipline 1920×1080 and Fire 1280×720 captures exited 0.
  Denser groves/low bank retained; rounded far slopes and foreground wall stack
  required the V3 source correction. No final-target acceptance.
- V2 live profile completed, schema-v2 emission metadata verified. A separate
  Home screenshot control also exited 0 and reproduced the shutdown warnings.
- V3 `rebuild` passed (`20260908-132041-rebuild-75289/unity.log`).
- V3 `validate` passed (`20260908-132130-validate-75744/unity.log`).
- First V3 `test-all` stopped correctly after EditMode: 988 passed / four failed /
  zero skipped out of 992 (`20260908-132206-test-edit-76012/results.xml`). All
  four failures were the new actual stone-joint sections: Unity's imported corner
  differed by about 0.167 m from the adjoining wall, contrary to the source-only
  projection. This result rejects that placement; do not widen the 0.012 m test
  tolerance or claim the source prediction established a good connection.
  Decoding the generated Unity mesh/scene reproduced the failing values and
  identified an incorrect source-side Z reflection. The correction is a rigid
  90° corner yaw, retaining position/scale/base and all source vertices.
- Corrected `rebuild` passed (`20260908-132736-rebuild-78255/unity.log`).
- Corrected full EditMode passed: 992/992, zero failed/skipped
  (`20260908-132851-test-edit-78970/results.xml`). All four real joint sections
  now agree within about 0.00293 m, below the unchanged 0.012 m tolerance.
- Fresh PlayMode passed: 250/250, zero failed/skipped
  (`20260908-132940-test-play-78970/results.xml`).
- V3 `build-macos` passed (`20260908-133133-build-macos-80177/unity.log`).
- Eight native captures named `final-*` exited 0 and were reviewed (the prefix
  reflects their intended role, **not acceptance**). Camp 1 at 16:9/720p and
  20:9 retains clear actor rings and a coherent foreground corner. Camp 3 shows
  the new measured groves; staged Frost Nova is visible on the ground. Arena
  Fire and Dungeon floor-1 Discipline render without missing/magenta materials;
  these are smoke checks, not new art acceptance.
- V3's 2.4:1 / yaw-135 review exposed unacceptable triangular far-terrain
  shading islands. Smooth normals and layer colors were continuous; the affected
  area mixed per-triangle XZ and XY projections. This rejects the V3 Vista
  material at that orbit despite all tests passing. A Vista-only continuous
  triplanar material correction was subsequently verified below.
- Projection-corrected `rebuild` and `build-macos` passed:
  `20260908-134759-rebuild-86788` and `20260908-134910-build-macos-87412`.
- Eight `reviewed-*` native captures exited 0 and were inspected. The same
  2.4:1 / yaw135 image no longer shows the triangular projection islands.
  Default/720p/20:9 actor rings and the foreground join remain clear; camp3
  and staged Frost Nova render correctly. Arena/Dungeon remain smoke checks
  with their existing provisional assets and incomplete HUD/readability work.
- Projection-corrected `test-all` passed: **1,004/1,004 EditMode and 251/251
  PlayMode**, zero failed/inconclusive/skipped. Fresh XML in `../outputs/agent/`:
  `20260908-135543-test-edit-89984/results.xml` and
  `20260908-135628-test-play-89984/results.xml`.
- Final live observation completed with exit 0: `reviewed-live-camp1-1920.json`
  and matching `.log`, 600 observed frames after a ten-second warmup, with no
  concurrent Unity/DCC/capture work. Measurement details are below.
- Final `build-android`: passed, exit 0, native build result Success
  (`20260908-135944-build-android-91628/unity.log`). Development ARM64 APK:
  `../outputs/SessionRpg-dev.apk`, **232,103,140 bytes** (221.4 MiB), SHA256
  `c6bb22fce90b68791d0f75db2f422270f3b6e3e8b22d0319fa1452c78ea3d700`.
  Packaging only; no install, device launch, distribution-size or release approval.
- Final `validate`: passed, exit 0, authored-foundation success marker
  (`20260908-140611-validate-95413/unity.log`).
- `bash Tools/Art/verify_sunbreak_sources.sh`: passed, 18 source maps /
  41,996,162 bytes, original MD5/project SHA256 and retained license verified.
  The first direct invocation exited 126 because the tracked script is not
  executable; running its existing contents through Bash passed without changing
  file permissions or source assets.

Automated result counts:

Final EditMode: **1,004 passed / zero failed / zero skipped**; final PlayMode:
**251 passed / zero failed / zero skipped**. V1 passed 978
EditMode / 250 PlayMode. The first V3 run had four joint failures above;
PlayMode was not attempted by that fail-fast invocation. Both suites subsequently
passed in the corrected full run.

Manual checks:

Source DCC candidate: 310px tree view, 64 transparent bounces, alpha-covered pixel
area 19,725.514 → 27,005.789 (+36.9%), with unchanged triangle count. In-game
material response, coverage and Core footprints remain separate gates.
V1/V2 native 16:9 frames reviewed, including 1280×720 readability. V3 additionally
reviewed 20:9, 2.4:1 at yaw135, camp3, staged Frost Nova and Arena/Dungeon smoke
frames, above. They do not establish human input, traversal, actual crowded
combat, all specs, full orbit endpoints or physical-touch comfort. Final
material-corrected revision was reviewed at those same eight configurations.
The local projection defect is fixed; sparse region dressing, intact fort edges,
companion/creature proxies and overall target-art acceptance remain open.

Final native capture matrix (all filenames are relative to
`build/review/pve-woodland-polish-20260908/`; all exited 0 and were visually read):

| PNG basename | Mode / specialization | Resolution | Extra arguments |
|---|---|---|---|
| `reviewed-camp1-discipline-1920` | PveLeveling / Discipline | 1920×1080 | `-sessionRpgCaptureCamp 1` |
| `reviewed-camp1-fire-720` | PveLeveling / Fire | 1280×720 | `-sessionRpgCaptureCamp 1` |
| `reviewed-camp1-hunter-20x9` | PveLeveling / Marksmanship | 2400×1080 | `-sessionRpgCaptureCamp 1` |
| `reviewed-camp1-rogue-24x10-yaw135` | PveLeveling / ShadowDance | 2592×1080 | `-sessionRpgCaptureCamp 1 -sessionRpgCaptureCameraYaw 135` |
| `reviewed-camp3-frost-1920` | PveLeveling / Frost | 1920×1080 | `-sessionRpgCaptureCamp 3` |
| `reviewed-camp1-frostnova-20x9` | PveLeveling / Frost | 2400×1080 | `-sessionRpgCaptureCamp 1 -sessionRpgCaptureAbility FrostNova -sessionRpgCaptureEffectDelay 0.15` |
| `reviewed-arena-fire-1920` | Arena / Fire | 1920×1080 | None |
| `reviewed-dungeon-discipline-1920` | Dungeon / Discipline | 1920×1080 | None |

Every row used this native invocation, substituting its mode, stable spec,
width/height, absolute output basename and extra arguments:

```sh
'../outputs/SessionRpg-mac-capture.app/Contents/MacOS/Session RPG Prototype' \
  -sessionRpgCapture '/absolute/project/build/review/pve-woodland-polish-20260908/BASENAME.png' \
  -sessionRpgCaptureMode MODE -sessionRpgCaptureSpecialization SPEC \
  EXTRA_ARGUMENTS -screen-width WIDTH -screen-height HEIGHT -screen-fullscreen 0 \
  -logFile '/absolute/project/build/review/pve-woodland-polish-20260908/BASENAME.log'
```

These are desktop visual captures, not a physical phone or human gameplay test.
The spell showcase stages the actor/camera separately and is not identical
composition evidence. No claim covers all seven specs or every orbit endpoint.

Performance/device/network checks:

- V1 desktop stationary baseline: 600/600 frames alive, playing, controller
  enabled, zero accepted human casts. FrameTimingManager p95: CPU frame including
  waits 17.217417 ms, CPU main-thread 4.059416 ms, CPU render-thread 2.036209 ms,
  Metal GPU 6.813667 ms. Inter-frame p95 17.299 ms. Apple M3 Pro, High, 1920×1080,
  target 60, vSync 0, Development player. This is not crowded combat/device acceptance.
- Observed main-thread allocation was zero in all 600 samples, but the broader
  `GC Allocated In Frame` recorder reported median 320 bytes. Different scopes;
  no whole-game allocation-free claim. SetPass/triangle recorders had equal
  zero/double samples consistent with delayed/aggregated recording (inference).
  Preserve raw values; do not treat their peaks as proven per-rendered-frame cost.
- **Final revision:** 600/600 frames playing/alive/controller enabled, zero
  measured runtime errors. Same desktop configuration as V1; no concurrent heavy
  task, but thermal state was not controlled. Inter-frame p95 **17.400 ms**;
  FrameTimingManager p95 CPU frame including waits **17.406 ms**, CPU main
  **3.861 ms**, CPU render **2.045 ms**, GPU **6.624 ms**. Main-thread allocation
  was **0 B in every sample**; broader Unity GC remained **320 B median/p95,
  368 B max**. Unity-tracked used memory peaked at **689,005,785 B**; not RSS or
  leak freedom. SetPass has 43 zero/43 double-emission rows; triangles have
  40 zero/40 double-emission rows. Preserve raw data; neither raw p95 is a
  single-rendered-frame workload. Draw-call total remains unavailable.
  These separate unthermalized runs are **not a controlled improvement claim**.
- Desktop diagnostic execution: **passed**. Main-thread observed allocation
  check: **passed**. Whole-game zero-allocation acceptance: **not passed** due to
  the broader nonzero counter without attribution. Full performance acceptance:
  **not passed**; frame-pacing p95 exceeds the 16.7 ms planning budget and this
  is only a short stationary desktop observation.
- Physical Android installation/touch/performance/thermals: **not run**.
- Android Development ARM64 packaging: **passed**; physical-device proof is
  separate. iOS packaging: **not run**.
- User minimum target clarified during handoff: one performance tier below
  iQOO Neo11, provisionally mapped to Snapdragon 8 Gen 3 / Adreno 750 in
  [the reference-class contract](DEMO_READINESS.md#performance-reference-class).
  This updates planning documentation only; no quality fallback was silently
  applied and no device performance was inferred from the desktop measurement.
- Network/co-op: **not run**; no transport changes or online acceptance claim.
- Full gameplay/10-session soak and physical GPU memory/overdraw: **not run**.
- Target-art/production acceptance: **not passed** by source generation or tests.

Later in this same task the user attached an authorized Neo11 and asked to
prioritize best quality on it. Device identity checks passed, but the initial
data-preserving update failed with phone-side permission rejection. Device
execution is still not validated; continuation is recorded separately in
[Neo11 evidence](PVE_NEO11_DEVICE_EVIDENCE.md).

Known caveats:

This remains a candidate. It does not acquire a mature-tree library, authored
ruin-damage kit, animation library, quests, loot or multiplayer. Distant bough
cards are a low-cost representation of real source clusters, not a full scanned
forest. Extra alpha coverage can increase fill/shadow cost despite unchanged
near-tree triangles; measure and inspect it. Static rendered frames do not prove
wind, LOD transitions, touch comfort or human readability. Prior development
player shutdown memory warnings are unresolved.

Next safe action:

Build out one coherent camp with genuinely broken masonry, richer ground/edge
dressing and production enemy/companion assets, preserving authoritative
footprints and mobile visibility. In parallel, attribute the shutdown allocations
using supported native stacks/snapshots and obtain physical-phone thermal/touch
evidence. Those are separate content/stability/device workstreams, not remaining
shader tweaks. Do not call the game production-ready without the remaining art,
device, stability and player-validation gates.
