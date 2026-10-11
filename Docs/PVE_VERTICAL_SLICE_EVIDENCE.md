# Frostbound Watch measured-asset vertical slice — 2026-09-07

Scope:

Unity presentation/camera, editor authoring, third-party asset intake, shader and
documentation. This iteration follows the user's rejection of the courtyard pass;
that historical pass's successful tests did not establish visual acceptance.
One Leveling pocket is the benchmark, not a production-art claim for three modes.
Core combat, 30 Hz simulation, level/XP rules, camp positions, exact cover unions,
network authority and the enlarged 1.3× mobile controls are unchanged.

Source changed:

- `Editor/Authoring/SunbreakBasinAuthoring.cs`: replace focal landmark 0/7
  procedural ruins, trees and rubble dressing with selected measured fort and
  bounded vegetation assets. The old courtyard paving/litter is no longer
  instantiated at camp 1; its historical source and generated files are retained.
  Preserve low erosion banks, the single admitted moss rock, clear combat ground,
  and other camps' authoring. Imported meshes are positioned/scaled uniformly,
  never squeezed to fit collision. Tall source geometry must fit exact Core unions.
- New `SunbreakFortAssets`: immutable CC0 FBX and measured wall/trim maps,
  explicit imports, selected-module placement, source integrity and material gates.
  Generated render copies preserve positions, normals, UVs and material indices;
  Gram–Schmidt correction fixes imported non-orthogonal tangent frames without
  modifying the original FBX or relaxing the geometry test.
- New `SunbreakVegetationAssets` and DCC derivative tooling: immutable CC0 source,
  bounded source-derived vegetation, measured material maps and alpha-cutout
  foliage. Original high-resolution sources are not directly instantiated.
  Coverage-preserving mipmaps and alpha-to-coverage share the color/depth/normal
  coverage contract; shadows retain a hard cutoff. This improves edges, not leaf
  density. Four localized grass/stone patches replace the old paving treatment.
- New `SunbreakVistaAuthoring`: an independent, two-renderer, 5,122-triangle
  distant ground/horizon plus a real CC0 cloud panorama. Raised vista geometry
  stays outside the Core playable rectangle. No detailed chunk-streaming policy
  change, lights, cameras, physics, or runtime terrain generation.
- New `SunbreakDaylightSkyAssets`: immutable 1K HDR, linear RGB9E5 import,
  desktop 1024 / Android 512 cap, neutral panoramic material and explicit
  `_MAPPING_LATITUDE_LONGITUDE_LAYOUT` keyword. Existing analytic reflection and
  ambient lighting are not mislabeled as HDRI-derived lighting.
- `AuthoringAssets` and `SessionRpgAuthoringGenerator`: source configuration,
  generated composition references and validation.
- `WorldLightingPresenter`: Sunbreak-only sky/vista activation and atmospheric
  fog; exact prior sky/reflection/environment restoration when leaving. Reuse
  the existing key and cached reflection; no frame-time material creation.
- `StrategyFollowCamera`: PvE-only 70° yaw / 20° pitch / 18 m / 58° FOV,
  forward focus 6 m and right focus 2.5 m, far plane 400 m and skybox clearing.
  Preserve the public 35° clamp and other mode presets. Restore exact entry
  far-plane/clear settings on exit/disable/destruction. `PrototypeGameController`
  restores the non-PvE composition on return home.
- `SessionRpgReferenceCapture`: default/yaw-only captures preserve the actual
  PvE camera instead of silently passing it through the public 35° clamp.
- Focused import, generated composition, foliage, vista, camera and lighting
  tests; asset ledger/notices and this record.

Rendering/tooling source also includes `Shaders/SunbreakVegetation.shader`,
`Tools/Art/Blender/{prepare,review}_sunbreak_vegetation.py`, and
`Tools/Agent/unity.sh` (isolated-source copy). New EditMode fixtures are
`SunbreakFortAssetsTests`, `SunbreakVegetationAssetsTests`,
`SunbreakVegetationCoverageTests`, `SunbreakMeasuredVegetationCompositionTests`,
`SunbreakDaylightSkyAssetsTests` and `SunbreakVistaAuthoringTests`; existing
`SunbreakBasinAuthoringTests`, `SunbreakCourtyardCompositionTests`,
`PveLevelingCameraPlayModeTests` and `WorldLightingPlayModeTests` were updated.

`Editor/`, `Unity/` and `Shaders/` paths are relative to `Assets/SessionRpg/`;
`Tools/`, `Docs/` and source-evidence paths are repository-relative. Full source
provenance is in each intake.

Generated changed:

Fresh rebuilds completed. SHA-256 comparison against **start of this turn**, not
Git HEAD: 1,689 files before, 1,758 after; **31 changed, 69 added, zero removed**
after final builds.
Exact paths and before/after hashes are retained in
`build/review/pve-vertical-slice-review-20260907/generated-delta.json`.
No generated YAML has been edited by hand. The workspace began dirty; unrelated
changes and historical candidates remain intact. Intentional outputs include the
Bootstrap camera/lighting/vista bindings, focal chunk, fort render copies,
vegetation meshes/materials, ground-detail mesh and sky/vista assets.

The global foundation rebuild also reserialized four generated controllers,
Combatant, three arena prefabs and all sixteen world chunks. This is disclosed
regeneration churn, not other-mode acceptance. Immediately after rebuilding,
fourteen existing materials also had different serialized hashes; the build
pipeline normalized **all fourteen back to their exact entry hashes**. They are
therefore not final generated changes. No unrelated source-art settings were
deliberately redesigned here.
Six existing meadow meshes across chunks `(-1,-1)` and `(-1,0)` change because the
focal vegetation exclusion reaches the adjacent chunk; empty generated measured
patch outputs there remain harmless and tracked. Thirty-three new generated
assets plus their metadata and three folder metadata files comprise the additions.

Commands (fresh):

- `Tools/Agent/unity.sh doctor`: passed, Editor closed.
- Every Unity command below uses `UNITY_BURST_DISABLE_COMPILATION=1` and the
  wrapper, serially against the closed-Editor real project. Evidence directories
  are under `../outputs/agent/`.
- `rebuild`: first attempt `20260907-222549-rebuild-26989` failed on the selected
  FBX submesh order; an invalid 33-character new test metadata GUID was also fixed.
  Read-only imported-node diagnostics `test-edit --filter SessionRpg.Editor.Tests.SunbreakFortAssetsTests.StableNodes`
  (`20260907-222807-test-edit-28047`) failed 5/5 and established exact imported
  material order/axes before source correction.
- Corrected `rebuild` / `validate` / `build-macos` passed:
  `20260907-223234-rebuild-29788`, `20260907-223339-validate-30372`,
  `20260907-223429-build-macos-30767`.
- `test-all` stopped at EditMode: 886/889 passed
  (`20260907-223554-test-edit-31568`). Failures exposed missing cutout material
  metadata, non-orthogonal imported fort tangents and an exact sky-color assertion.
  Corrected source/material contracts, not the geometric tolerance.
- Revised `rebuild` / `validate` / `build-macos` passed:
  `20260907-225053-rebuild-37880`, `20260907-225209-validate-38509`,
  `20260907-225339-build-macos-39240`.
- Next `test-all` stopped at EditMode: 898/903 passed
  (`20260907-225511-test-edit-39937`). Two tests still named replaced procedural
  assets/original-FBX bindings; three shared one sky keyword mismatch. Separate
  `test-play` passed 249/249 (`20260907-225615-test-play-40431`).
- Exact diagnostic command `test-edit --filter SessionRpg.Editor.Tests.SunbreakDaylightSkyAssetsTests`
  (`20260907-230345-test-edit-43535`) passed 2/4. Unity automatically installs the
  correct lat-long keyword on load; the previous empty-keyword contract was
  erroneous. Setup/validation now explicitly require exactly that keyword, with
  four new regression cases. No HDR or sky color change was needed.
- Final `rebuild` / `validate` passed:
  `20260907-230626-rebuild-44628`, `20260907-230856-validate-45708`.
- Final `test-all`: EditMode **907/907 passed**
  (`20260907-230935-test-edit-46015`); PlayMode **249/249 passed**
  (`20260907-231014-test-play-46015`). Zero failed/skipped in both final suites.
- Final `build-macos` passed (`20260907-231201-build-macos-47148`), producing
  `../outputs/SessionRpg-mac-capture.app`.
- Final `build-android` passed (`20260907-231314-build-android-47713`), producing
  `../outputs/SessionRpg-dev.apk`, **231,982,237 bytes** (221.2 MiB), SHA-256
  `11129ce20d968b3f34351d60f3494c062cad632de82082dc2ce95382ca3adab3`.
  This is the new revision's APK, not the older similarly named artifact.
- Post-build `validate` passed (`20260907-231532-validate-49111`), including the
  explicit sky keyword after platform switch. Final generated inventory above
  was checked after both builds.
- `bash -n Tools/Agent/unity.sh`: passed. Source-only
  `git diff --check -- '*.cs' '*.shader' '*.md' '*.sh'`: passed. The unrestricted
  whitespace check reports Unity-generated YAML trailing spaces; these were not
  hand-normalized and are not evidence of a clean whole-worktree diff.

Automated result counts:

Final EditMode **907/907 passed**; PlayMode **249/249 passed**. Prior courtyard results are not acceptance for
this revision. Fresh content audit: focal chunk **63,851 triangles / 28 renderers**
(ceiling 65,000 / 80), compared with 61,703 / 35 at entry. Four fort instances total
6,458; two trees plus eight ferns total 24,928. Measured meadow 5,006, embedded
stones 3,996 and retained distant prototype meadow 6,500. Independent vista adds
5,122 triangles / two renderers. These are content counts, not a measured GPU cost.

Manual checks:

Actual native-player captures, all exit 0, are in
`build/review/pve-vertical-slice-review-20260907/`:

| File | Mode / class / specialization | Resolution / camera | Result |
|---|---|---|---|
| `final-camp1-discipline.png` | PvE Leveling / Priest / Discipline, camp 1 | 1920×1080, default, final build | Final sky-keyword authoring fix retains the reviewed image; the visual target remains unmet. |
| `camp1-discipline-1920.png` | PvE Leveling / Priest / Discipline, camp 1 | 1920×1080, default | Actors and enlarged controls distinguishable; visible sky and measured stonework. |
| `camp1-fire-720.png` | PvE Leveling / Mage / Fire, camp 1 | 1280×720, default | Large controls/actors distinguishable; small text and radial sub-icons less clear. |
| `camp1-frost-wide.png` | PvE Leveling / Mage / Frost, camp 1 | 2400×1080, default | No observed large-control clipping; wider frame exposes sparse midground. |
| `camp1-shadow-ultrawide.png` | PvE Leveling / Priest / Shadow, camp 1 | 2592×1080, default | No observed large-control clipping; target-art quality remains unmet. |
| `camp1-orbit135.png` | PvE Leveling / Priest / Discipline, camp 1 | 1920×1080, yaw 135° | Actors visible; right frame edge exposes a foreground rock and distant prototype assets. |
| `camp0-marksmanship.png` | PvE Leveling / Hunter / Marksmanship, camp 0 | 1920×1080, default | Shared lens/sky works; unchanged camp and pet retain conspicuously prototype art. |
| `arena-mutilate.png` | Arena / Rogue / Mutilate | 1920×1080, default | Prior steep camera/dark arena retained; no Sunbreak sky/vista visible. Dark character readability still limited. |
| `dungeon-discipline.png` | Dungeon / Priest / Discipline | 1920×1080, default | Prior steep camera/traversal floor retained; no Sunbreak sky/vista visible. Party overlap/right-edge indicators remain visible limitations. |

The eight non-`final` captures use the second native build; the final capture uses
the fully passing revision. The sole intervening generated visual input is the
explicit sky keyword that Unity was already installing automatically, not a
new composition/color change. All nine frames were inspected.

Capture invocation template (absolute output/log paths used):

```sh
'../outputs/SessionRpg-mac-capture.app/Contents/MacOS/Session RPG Prototype' \
  -sessionRpgCapture '<capture>.png' -sessionRpgCaptureMode PveLeveling \
  -sessionRpgCaptureSpecialization Discipline -sessionRpgCaptureCamp 1 \
  -screen-width 1920 -screen-height 1080 -screen-fullscreen 0 \
  -logFile '<capture>.log'
```

Orbit adds `-sessionRpgCaptureCameraYaw 135`. Other rows use their listed mode,
specialization, camp and dimensions. At most two capture players ran together.
The harness writes a separate `pve-capture-save` profile beside the captures.
These are rendered-frame inspections, not physical touch, traversal, long combat,
progression persistence, animation or all-specialization acceptance.

The first tree-reduction DCC candidate was rejected for sparse leaves and woody
artifacts. A later modeling preview also overstated density because eight
transparency bounces falsely filled overlapping cards. Controlled 64-bounce,
gameplay-scale review (`build/review/sunbreak-vegetation-native-scale/`) and native
frames reveal a thin canopy. Its production visual gate is explicitly still open.

Performance/device/network checks:

- Static geometry/import/scene contracts: **passed**, final rebuild, validation
  and full suites above.
- Automated warmed-up Core/Application allocation guards: **passed** in the
  full EditMode suite, including PvE mob decisions, repeated camp cycles and the
  steady-state simulation/bot loop. This is not an integrated player profile.
- Live allocation and sustained CPU/GPU profiling: **not run**.
- Physical Android, touch, overdraw, memory and thermals: **not run**.
- Network/co-op/server checks: **not run**; gameplay networking is unchanged.
- Android package build: **passed**; an APK alone cannot establish mobile performance.
- Clean native shutdown/memory acceptance: **failed**. Captures exit 0, but
  Unity logs retain shutdown `MemoryLeaks`/premature thread-finalization reports.
  No gameplay exception or shader error was observed in these capture logs.

Known caveats:

**The target-art visual gate is not met.** This is a local playable benchmark with
more convincing individual stone assets, not a high-quality finished environment.
The fort pieces are isolated on smooth banks, midground is flat and sparse, the
horizon is a nearly featureless blue strip and foliage is thin. Large controls
remain intentionally enlarged; their screen footprint competes with scenery, and
small text/sub-icons need device review. Other camps still use prototype assets;
Arena/Dungeon have not received this art kit. Character animation, authored
terrain, final environment variation and physical-device validation remain open.
CC0 intake status stays `TECH_REVIEW` until actual visual/technical acceptance.

Next safe action:

Author a coherent connected ruin/ground
composition and fuller canopy in this same pocket, with a real gameplay-camera
approval gate before copying it to more camps or modes. Any walkable terrain
height or new cover layout needs an explicit Core/application design change and
tests; do not hide collision changes inside art or trade away mobile budgets
without measured device evidence.
