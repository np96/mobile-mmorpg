# Frostbound courtyard realism pass — 2026-09-07

Scope:

Unity presentation/camera, editor authoring, shader and documentation. This is the
next actual-game iteration toward the [Leveling target](Art/MODE_TARGETS_20260907.md),
following the [first scanned-rock benchmark](PVE_REALISM_BENCHMARK_EVIDENCE.md).
It concentrates built geometry at Frostbound Watch, with region-wide ground
material and PvE-only camera improvements. It does not complete the three-mode
production-art request. Core combat, 30 Hz simulation, progression, camp positions,
collision, networking and the completed 1.3× large controls are unchanged.

Source changed:

Paths below are relative to `Assets/SessionRpg/`:

- `Editor/Authoring/SunbreakBasinAuthoring.cs`: two new windowed ruin silhouettes,
  integrated broken courtyard, and a lower eroded earth/rubble foundation for
  landmark 0. The foreground façade now faces the default gameplay approach.
  All tall geometry stays inside the existing exact Core circle unions. Filled
  lower courses communicate solid cover; the openings are elevated windows,
  not false walkable doors. Other major formations retain their old height field.
- New `Editor/Authoring/SunbreakCourtyardGeometry.cs` and metadata: seeded convex
  paving/chip footprints clipped against shared planes, shallow tops and sides,
  world-aligned UVs and complete-piece chunk ownership. Three opaque material
  groups reuse existing licensed scan bindings. Editor-time construction only;
  no collider, new gameplay cover, runtime update or per-frame allocation.
  Final authored paving uses approximately one-square-meter stones with mostly
  buried 1.6–3.0 cm tops and narrow sloped edges; displaced chips stay below 8 cm.
  Connected patches retain deliberate collapse gaps rather than forming a road.
- `Editor/Authoring/SunbreakScannedSurfaceAuthoring.cs` and
  `Shaders/SunbreakTerrain.shader`: independent grass/earth tints, a domain-warped
  multi-scale ground mask and more distinct soil/vegetation transitions. The
  earth scan's orange cast is reduced with ground-only saturation 0.28 in linear
  luminance-preserving space, not global exposure or darker lighting. The shader
  retains seven ground texture reads and measured normal/roughness detail.
  Other materials disable and reset these ground-only controls.
- `Unity/Camera/StrategyFollowCamera.cs`: only PvE changes from pitch 37° /
  distance 23 m to 35° / 21.5 m. Yaw 90°, forward focus 5 m, right focus 1.15 m,
  FOV 45° and full orbit remain unchanged. A modeled 1.8 m actor grows from
  about 87 to 98 pixels tall at 1080p. Training/other camera presets are unchanged.
- New EditMode `SunbreakCourtyardGeometryTests` (14),
  `SunbreakCourtyardCompositionTests` (10), `SunbreakGroundEcotoneTests` (8),
  their metadata, and strengthened existing `PveLevelingCameraPlayModeTests`.
- This record, `DEMO_READINESS.md`, `PVE_LEVELING_MODE.md`, `WORLD_AUTHORING.md`
  and `Art/REALISM_IMPLEMENTATION.md`.

No new third-party source intake. Original concept art, source textures, earlier
captures, generated meshes and unrelated worktree changes are retained.

Generated changed:

Five new generated meshes and metadata: courtyard paving/edges/chips, landmark-0
watchtower and landmark-7 façade. Existing landmark-0/7 mesh groups and the
ground blend/materials are rebuilt. The mandatory foundation generator also
rewrites Bootstrap, combatant/arena prefabs, world scenes and selected controllers
and material serialization. This is not byte-isolated generation; no generated
YAML was hand-edited and no unrelated dirty changes were reverted.

SHA-256 inventory against the start of this turn (1,679 existing generated files)
finds 45 changed, 10 added (five mesh/metadata pairs), and zero removed across
`Art`, `Content`, `Settings`, `Scenes`, `Prefabs` and `ProjectSettings`. This
inventory is relative to the turn start, not Git HEAD's much older dirty baseline.
The post-rebuild inventory had 59 changed files; after the final test/build cycle,
14 incidental material serializations again matched their start-of-turn hashes.

Commands (fresh):

```sh
Tools/Agent/unity.sh doctor
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh rebuild
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh validate
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh build-macos
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-all
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh build-android
```

Doctor passed with the Editor closed. The existing process-local Burst workaround
is unchanged. Logs/XML live under sibling `../outputs/agent/`.

Historical candidates within this pass (not final geometry acceptance):

- `20260907-152730-rebuild-21239` and `152828-validate-21767`: passed.
- `20260907-152937-test-edit-22249`: 835/836. The sole failure was exact floating
  color equality after material serialization. Per-channel 0.000001 tolerance
  fixes that test; expected tints/scan-scale/normal assertions remain explicit.
- `20260907-153214-test-edit-23509`: 836/836; `153249-test-play-23509`: 232/232.
- `20260907-153107-build-macos-22928`: passed. Initial captures in
  `build/review/pve-courtyard-20260907/` exposed orange soil and isolated hexagonal
  stepping-stone shapes. Those art issues were not treated as passed.
- `20260907-154032-rebuild-27084`, `154124-validate-27652` and
  `154217-build-macos-28037`: passed. Soil, connected slabs, façade rotation and
  closer camera are present. `154311-test-edit-28549`: 836/836;
  `154344-test-play-28549`: 232/232. Captures in the historically named
  `build/review/pve-courtyard-final-20260907/` exposed oversized, plate-like slab
  edges, prompting another source iteration. The folder name is not acceptance.

Final revision (same command forms above):

- `20260907-155045-rebuild-31620/unity.log`: passed.
- `20260907-155201-validate-32343/unity.log`: passed.
- `20260907-155254-build-macos-32769/unity.log`: passed.
- `20260907-155403-test-edit-33295/results.xml`: 836/836 passed.
- `20260907-155439-test-play-33295/results.xml`: 232/232 passed.
- `20260907-155626-build-android-34624/unity.log`: passed.

Final Android artifact: sibling `../outputs/SessionRpg-dev.apk`, 232,000,616 bytes,
built 2026-09-07 15:57:35 MSK, SHA-256
`31b41c64d6827a25799aed1edbd602bfba4f72bd8e90f6b190efcd70a7d56bf6`.
The size/time were checked with `stat -f 'bytes=%z modified=%Sm' -t
'%Y-%m-%d %H:%M:%S %Z' ../outputs/SessionRpg-dev.apk`; hash with
`shasum -a 256 ../outputs/SessionRpg-dev.apk`. This APK replaces the prior
benchmark artifact at the same path. The native review build is sibling
`../outputs/SessionRpg-mac-capture.app`.

Automated result counts:

**1,068/1,068 passed: 836 EditMode + 232 PlayMode, zero failed/skipped.**
The suite includes 32 added authoring/material cases, strengthened PvE camera
contracts, existing collision/LOS, stable mode/spec, 30 Hz progression and
warmed-up allocation/pool regressions. Passing tests certify those contracts,
not visual realism or physical-phone acceptance.

Manual checks:

Native-player captures use the actual generated scene and gameplay camera, not
concept compositing. The harness stages a repeatable camp/spec but is not a full
manual playthrough. The same camp/spec/resolution can be compared with the prior
benchmark; camera framing intentionally changes and animation/cast timing may
differ. Final captures are in `build/review/pve-courtyard-review-20260907/`:

| PNG basename | Mode / specialization | Resolution | Camp / yaw |
|---|---|---|---|
| `camp1-discipline-1920` | PvE / Discipline | 1920 × 1080 | 1 / default 90° |
| `camp1-fire-1280x720` | PvE / Fire | 1280 × 720 | 1 / default |
| `camp1-frost-2400x1080` | PvE / Frost | 2400 × 1080 | 1 / default |
| `camp1-shadow-2592x1080` | PvE / Shadow | 2592 × 1080 | 1 / default |
| `camp1-discipline-yaw135-1920x1080` | PvE / Discipline | 1920 × 1080 | 1 / 135° orbit |
| `camp0-fire-1920` | PvE / Fire | 1920 × 1080 | 0 / default |
| `camp6-marksmanship-2592` | PvE / Marksmanship | 2592 × 1080 | 6 / default |
| `regression-arena-frost-2400` | Arena / Frost | 2400 × 1080 | no override |
| `regression-dungeon-frost-1920` | Dungeon / Frost | 1920 × 1080 | first floor |
| `regression-training-mutilate-2592` | Training / Mutilate | 2592 × 1080 | guided step 1 |

Each row is a separate invocation of
`../outputs/SessionRpg-mac-capture.app/Contents/MacOS/Session RPG Prototype`, with
`-sessionRpgCapture <absolute PNG>`, `-sessionRpgCaptureMode <PveLeveling|Arena|Dungeon|Leveling>`,
`-sessionRpgCaptureSpecialization <stable spec name>`, the listed `-screen-width`
and `-screen-height`, `-screen-fullscreen 0`, and `-logFile <same absolute basename>.log`.
Only camp rows add `-sessionRpgCaptureCamp <index>`; only the orbit row adds
`-sessionRpgCaptureCameraYaw 135`.

All ten final players exited zero, produced the exact listed dimensions and
logged capture completion. No Error/Exception/failed tokens appeared. The same
Unity shutdown thread-finalization/MemoryLeaks diagnostics remain. This is not
clean-memory evidence. Six specializations are sampled across these modes;
Shadow Dance, PvE camps 2–5/refuge, full manual runs and continuous seam traversal
were not visually rechecked in this pass.

The final Frostbound views show smaller stones at a more believable actor-relative
scale, substantially less raised-edge contrast, visible hero/enemy foot rings,
neutral earth and a clearer foreground elevated window. Both enlarged gesture
discs remain visible and disjoint at 16:9, 20:9 and 2.4:1. The 720p sample still
has fine text, and the widest layout still has a narrow gap between the large
control rims. Neither is physical-thumb proof.

Camp 0 exposes the same broad flat ground and hard outer boundary. Camp 6 still
has the toy-like boss/companion and a notice panel across the boss's upper body.
Arena and Dungeon samples render their existing materials/geometry, with no new
missing-material issue observed; their production look was not upgraded. The
Dungeon frame retains crowded actors/overlays near the right-side controls.
Training retains its 56° / 17 m camera, old Causeway surfaces and learned-only
controls, with no observed Sunbreak material leakage. Its awareness-card overlap
and cramped common-action label remain.
These observations are intentionally not described as full visual acceptance.

Performance/device/network checks:

- Allocation: **passed** for the final warmed-up Core/Application/HUD/pool tests;
  whole-frame native/mobile allocation profiling **not run**.
- Static content budget: unchanged 65,000 triangles / 80 renderers per chunk.
  Courtyard helper capped at 2,500 triangles / 10 cm height. These are authored
  content limits, not GPU-frame measurements.
  The final generated courtyard is 2,413 triangles, with 148 stones/chips and
  approximately 32.5% footprint coverage. The complete owner chunk (-1,0) is
  61,703 triangles / 35 renderers, including the existing 5,000-triangle scanned
  rock. All nine chunks range 38,944–63,823 triangles / 11–35 renderers. Counts
  come from generated mesh indices and scene references, not source estimates.
- Android development build: **passed**. Physical phone installation, CPU/GPU
  performance, thermal soak, sustained framerate and touch acceptance **not run**.
- Full target-art/HUD clearance: **failed**; remaining issues are stated below.
- Network/co-op and iOS: **not run**; no network implementation claim.

Known caveats:

The artwork's vegetation density, erosion detail, distant vistas and character
quality are not reached. The rest of the region is still broad and flat, other
large rock formations remain rounded, and materials/foliage are visibly repeated
or procedural. The tall watchtower remains largely outside/behind the default
top HUD at the camp-one entry position; it needs orbit/movement to inspect.
This is still a technical/art candidate, not user art acceptance.

The camera test proves actor size and one 14 m health-bar anchor, not whole-billboard
HUD clearance. At that distance, the upper track is near the XP strip and the
cast strip can overlap the encounter notice. Small text and tight wide-aspect
control spacing remain separate physical-device gates; the large controls were
not shrunk to create a prettier screenshot. Native Unity shutdown MemoryLeaks/
thread-finalization diagnostics are not clean-memory evidence.

Next safe action:

Review the final native camp view and Android build on the target phone before
accepting this art direction or extending it to the rest of the region. Continue
with authored foliage/ruin asset quality and character work; Arena and a dedicated
Dungeon environment remain separate production passes.
