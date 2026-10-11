# Frostbound Watch ruin composition — 2026-09-08

Scope:

Unity presentation, editor authoring, generated-scene tests and documentation.
Continue the measured-asset trial at one PvE Leveling camp. Core collision,
camp/spawn locations, XP/persistence, 30 Hz simulation, camera settings and the
1.3× mobile gesture controls are unchanged. No new download, license, cloud action,
package, networking or server implementation is part of this revision.

Source changed:

- `Assets/SessionRpg/Editor/Authoring/SunbreakBasinAuthoring.cs`: build nine
  measured fort pieces into two local compounds instead of four isolated pieces;
  reduce the nearer wall height, add return walls and broken abutments, enlarge
  one existing tree and add a third source tree. Keep eight source ferns and the
  existing scanned rock. Imported topology/UVs/normals and positive uniform scale
  remain intact. No vertex squeezing or art-owned collision.
- The same builder replaces the focal chunk's 6,500-triangle distant prototype
  meadow with closer composition, and reduces measured short grass from about
  5,000 to about 1,500 triangles. Old hexagonal embedded-stone scatter is no longer
  instantiated. Historical generated assets remain preserved.
- New `Assets/SessionRpg/Editor/Authoring/SunbreakRuinGroundAuthoring.cs`: one
  first-party fractured court, broad in both axes with missing-cell earth pockets,
  shallow bevels and embedded chips. Each footprint is 0.3–1 m², with exposed
  earth dominant. At most 3,000 triangles / one renderer, Y 0.005–0.012 m,
  all complete pieces within chunk (-1,0) and clear of Core cover. Existing
  team/target rings remain at 0.025 / 0.04 m; neither is raised to hide an art bug.
  World-space dominant-plane UVs preserve measured material scale on tops and
  sides. No repeated paving strip, new texture, physics or runtime generation.
- `Assets/SessionRpg/Editor/Authoring/SunbreakVistaAuthoring.cs`: three irregular
  radial crests, differing by azimuth, with peaks/saddles instead of the same low
  section repeated around every direction. 10,242 triangles / two static batches,
  capped at 12,000; all raised triangles remain outside the playable rectangle.
  Revised after the first capture: a 24 m eased foot replaces the steep 6 m
  edge, tapered shoulders and resolved oblique folds replace smooth humps, and
  dominant-axis rock UVs prevent vertical stretching while preserving welded
  lighting normals across texture seams. No new shader or extra material batch.
- `Assets/SessionRpg/Unity/Presentation/WorldLightingPresenter.cs`: Sunbreak-only
  Exp² density 0.0085 → 0.0035. Prior geometry varied the actual skyline by only
  about 21 pixels and fog erased most contrast at its 193–362 m depth. Other-mode
  lighting, existing sky source, reflection, camera, shadows and quality tiers
  are unchanged; exact environment restoration remains tested.
- New `SunbreakRuinGroundAuthoringTests`, updated `SunbreakCourtyardCompositionTests`,
  `SunbreakMeasuredVegetationCompositionTests`, `SunbreakVistaAuthoringTests` and
  `WorldLightingPlayModeTests`: deterministic buffers, exact footprints, source
  frames/UVs, bounded connected architecture, actual-camera skyline projection,
  fog contrast and restoration. EditMode fixtures live under
  `Assets/SessionRpg/Tests/EditMode/Editor/`; lighting under `Tests/PlayMode/`.
  The Editor test assembly adds an explicit Application reference to call the
  real camera's mode API; runtime assembly boundaries are unchanged.
- Documentation/intake notes and `SourceArt/Environment/SunbreakVegetationV1/README.md`
  correct the old canopy-density claim. No source mesh, atlas, original delivery
  or hash-locked derivative has been replaced.

Generated changed:

Final SHA256 inventory compares 1,758 entry files with 1,761 final files, **29
modified, 3 added, 0 removed**, independently of the already-dirty Git worktree.
Exact before/after hashes are preserved in
`build/review/pve-ruin-compound-20260908/generated-delta.json`.

- New court mesh, mesh `.meta` and folder `.meta` under
  `Art/Environment/SunbreakRuinGround`.
- Changed focal `Island7_MeasuredVegetation_0/1` and `MeasuredMeadow_0` meshes,
  and `SunbreakVista/ErodedMountainHorizon.asset`.
- Full rebuild also rewrote four existing animation controllers, three generated
  arena prefabs, all sixteen world chunk scenes, `Combatant.prefab`, and
  `Bootstrap.unity`. These are recorded, not hidden as unrelated clean files.
  Their authoring sources outside the listed change remain untouched; controller,
  prefab, scene and mode-restoration contracts passed. A byte-equivalence claim
  for these regenerated files is not made.
- Generated materials, settings and ProjectSettings match the entry hashes after
  both target builds. Old unused environment assets were not deleted.

No generated YAML or mesh was edited manually.

Commands (fresh):

All Unity commands use `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh`,
serially against the real closed-Editor project. Logs/XML are under
`../outputs/agent/`.

- `doctor`: passed, Unity 6000.5.6f1, Editor closed.
- First `rebuild` failed (`20260908-114600-rebuild-35241`) before generation:
  the new actual-camera test needed the Editor test assembly's explicit
  `SessionRpg.Application` reference. Added that test-only reference.
- Corrected `rebuild` passed (`20260908-114829-rebuild-36283`).
- `validate` passed (`20260908-115002-validate-37010`).
- First `build-macos` passed (`20260908-115104-build-macos-37446`).
- First native captures, `camp1-discipline-1920.png` and `camp1-fire-720.png`,
  exited 0 but failed visual review: the first court was oversized and obscured
  the player's ring, and the mountain shoulders were too smooth. These rejected
  frames are preserved under `build/review/pve-ruin-compound-20260908/`.
- First `test-all` stopped after 925/926 EditMode passes, 1 failure, 0 skipped
  (`20260908-115225-test-edit-38111/results.xml`). The strict surface-normal
  test found a downward-facing embedded chip. Corrected polygon winding,
  including chips, without relaxing the normal test. PlayMode did not run in
  this invocation. Added human-scale footprint and generated ring-clearance
  regression tests while revising the ground.
- Corrected `rebuild` passed (`20260908-120345-rebuild-42664`).
- Corrected `validate` passed (`20260908-120444-validate-43197`).
- Corrected `build-macos` passed (`20260908-120538-build-macos-43568`).
- Corrected `test-all` passed: EditMode 930/930, PlayMode 250/250, no failures or
  skips (`20260908-120622-test-edit-43926/results.xml` and
  `20260908-120703-test-play-43926/results.xml`).
- `build-android` passed (`20260908-120853-build-android-45066`).
- Final post-build `validate` passed (`20260908-121033-validate-45989`).
- `git diff --check -- '*.cs' '*.shader' '*.md' '*.sh'`: passed. Existing generated
  YAML formatting is not silently normalized through a broad whitespace rewrite.

Automated result counts:

**Passed:** 930 EditMode and 250 PlayMode. This includes deterministic rules,
existing allocation guards, exact world/cover geometry, streaming, source hashes,
12 new ground cases, 14 Vista cases, ring clearance, fog contrast and restoration.
Focal chunk (-1,0): 63,307 triangles / 30 renderers, below unchanged caps
65,000 / 80. The court is 2,050 triangles in one batch. Vista is 10,242 triangles
in two batches. Geometry caps are not CPU/GPU performance proof.

Manual checks:

Fresh files are in `build/review/pve-ruin-compound-20260908/`. All captures use the
corrected local player and its development overlay; no concept art is substituted.

| File | Mode/spec/view | Observed result |
|---|---|---|
| `v2-camp1-discipline-1920.png` | Leveling / Discipline, camp 1, 1920×1080, 16:9 | Complete player/target rings; buried fragments; no striped Vista cliff. |
| `v2-camp1-fire-720.png` | Leveling / Fire, camp 1, 1280×720, 16:9 | Rings and actors readable; original enlarged gesture controls retained. Fine ground detail is intentionally subdued at this resolution. |
| `v2-camp1-frost-wide.png` | Leveling / Frost, camp 1, 2400×1080, 20:9 | Controls inside viewport; unobscured actors and rings. |
| `v2-camp1-shadow-ultrawide.png` | Leveling / Shadow, camp 1, 2592×1080, 2.4:1 | Same visual-readability checks; no layout overflow. |
| `v2-camp1-discipline-orbit135.png` | Leveling / Discipline, camp 1, 1920×1080, yaw 135° | Rings clear from the other angle; existing faceted boundary banks and sparse backdrop remain visibly prototype-quality. |
| `v2-camp0-marksmanship.png` | Leveling / Marksmanship, camp 0, 1920×1080 | Neighboring camp loads; enemies/pet/HUD render; this camp retains prior prototype scenery. |
| `v2-arena-mutilate.png` | Arena / Mutilate, 1920×1080 | Arena environment, actors, rings and HUD render without the Sunbreak sky/vista. Existing dark presentation is not newly accepted as final art. |
| `v2-dungeon-discipline.png` | Dungeon floor 1 / Discipline, 1920×1080 | Dungeon environment, party and combat effects render without the Sunbreak sky/vista. Existing edge/HUD combat overlap remains outside this art pass. |

All eight captures exited 0. Default Leveling yaw remains 70°, pitch 20°, FoV 58°;
capture flags alone requested the orbit variant. These are desktop-rendered visual
proxies, not physical-phone touch acceptance. Manual seam traversal, movement/LOS
around each new wall, repeated streaming entry/exit, and every class/spec: **not run**;
the exact footprint and streaming automated guards passed. Exact commands, file
paths and result counts are recorded in
`build/review/pve-ruin-compound-20260908/verification.json`.

Example native command (absolute PNG/log paths used in actual invocations):

```sh
'../outputs/SessionRpg-mac-capture.app/Contents/MacOS/Session RPG Prototype' \
  -sessionRpgCapture '/absolute/game-rpg/build/review/pve-ruin-compound-20260908/v2-camp1-discipline-1920.png' \
  -sessionRpgCaptureMode PveLeveling -sessionRpgCaptureSpecialization Discipline \
  -sessionRpgCaptureCamp 1 -screen-width 1920 -screen-height 1080 \
  -screen-fullscreen 0 -logFile '/absolute/game-rpg/build/review/pve-ruin-compound-20260908/v2-camp1-discipline-1920.log'
```

Performance/device/network checks:

- Integrated live allocation and CPU/GPU frame-time profiling: **not run**.
- Physical Android touch/overdraw/memory/thermals: **not run**.
- Network/co-op/server checks: **not run**; no gameplay transport changes.
- Existing automated allocation guards: **passed** in the full suites; no claim
  about integrated live frame allocation or GPU cost follows from those tests.
- macOS build: **passed**; Android build: **passed**. The local development APK is
  `../outputs/SessionRpg-dev.apk`, 231,976,416 bytes,
  SHA256 `15ff018545a5e27ba3ba21bc09967eb9ca717177e530572185a4bf0531d3ed1e`.
- Native shutdown memory/thread-finalization warnings recur and remain unresolved;
  capture exit 0 is not a clean-shutdown or memory-profile pass.

Known caveats:

This is still one visual trial, not target-art or production acceptance. Adding a
third tree improves composition but does not make the thin individual source
canopy production-quality. Distant mountains are genuine visual geometry, not a
wooded landscape; no forest has been implemented. Other camps and characters
retain prototype art. Flat authoritative playfield and existing cover unions
remain hard constraints; actual walkable terrain elevation needs Core design and
tests, not invisible presentation changes.

Independent review of the two default frames agrees the ring/cliff defects are
fixed but the target-quality gate is **not met**: bare mountains still dominate,
fine paving becomes indistinct at 720p, the grouped wall modules do not yet form
a convincing broken building, and the foreground canopy remains too open.

Next safe action:

Keep this bounded composition for review before expanding it to more camps.
The next art priority is a convincing mature vegetation/middle-distance grouping,
followed by more organic transitions at ruin foundations and the existing
boundary banks. Character/animation polish and physical-device profiling remain
separate acceptance work. This revision fixes readability and geometric defects;
it does not meet the target's mature wooded landscape or production realism bar.
