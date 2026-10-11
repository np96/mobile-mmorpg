# Larger mobile gesture controls — 2026-09-07

Scope:

Unity HUD authoring and presentation/input verification. The user requested at
least 1.3× larger large buttons because their contents were difficult to see on a
phone. The two gesture families now have 30% greater width/height, including every
icon, caption and their actual circular touch surface. The four common buttons,
four utilities and joystick keep their sizes. No Core, gameplay, camera, world,
network or progression rules changed.

Source changed:

- `Assets/SessionRpg/Editor/Authoring/HudAuthoring.cs`: stable 1.3× scale on the
  complete two gesture roots. The 232-unit local disc displays at 301.6 reference
  pixels, its 84-unit center at 109.2, 60-unit direction regions at 78, and 48-unit
  painted icons at 62.4. This survives runtime refresh and held/released states.
- Final right-bottom anchors: gesture controls `(-240,292)`, `(-564,292)`;
  common controls `(-182,526)`, `(-326,535)`, `(-474,537)`, `(-676,495)`.
  Bottom-center utility anchors remain unchanged. Outward swipes retain room at
  720p; input remains screen-pixel based and requires no extra finger travel.
- PlayMode `BootstrapPlayModeTests`, `FourClassHudPlayModeTests`,
  `AbilityButtonFlickPlayModeTests`, `CircularRaycastFilterPlayModeTests`: three
  additional tests plus updated generated/runtime scale contracts. Geometry
  includes icons, touch discs, transient chrome and offset shadows. The bounded
  layout matrix includes 4:3 robustness, 16:9, 20:9 and 2.4:1, 720p/1080p, and
  explicitly simulated notch/bottom insets. This is not physical-device evidence.
- The Four Lanes camera contract now checks actual scaled circular controls and
  their visible shadows against the unchanged padded actor spans, rather than
  treating transparent square corners as occlusion or ignoring transform scale.
- `DESIGN.md`, `Docs/VISUAL_QUALITY.md`, `Docs/DEMO_READINESS.md` and this record.

Generated changed:

`Assets/SessionRpg/Scenes/Bootstrap.unity` intentionally carries the new HUD scale
and anchors. The mandatory full rebuild also rewrites generated controllers,
materials, Combatant prefab, arena prefabs and world chunks. A pre-rebuild SHA-256
audit covered 1,677 generated/settings files: after final builds, 25 differed,
1,652 were byte-identical and none were removed. The earlier 14 material rewrites
returned to their pre-turn bytes after the builds. Runtime art/camera authoring inputs were
not edited. Generated YAML was not hand-edited; all pre-existing dirty work remains.

Commands (fresh):

```sh
Tools/Agent/unity.sh doctor
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh rebuild
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh validate
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-play --filter Enlarged
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-all
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh build-macos
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh build-android
```

The existing process-local Burst workaround remains unchanged. Fresh artifact
directories below are relative to sibling `../outputs/agent/`:

- `20260907-134716-test-play-74898/results.xml`: focused size/hit/flick tests, 3/3 passed.
- `20260907-135652-rebuild-78942/unity.log`: final layout rebuild passed.
- `20260907-135813-validate-79598/unity.log`: final layout validation passed.
- `20260907-135919-test-edit-80066/results.xml`: 771/771 passed.
- `20260907-135950-test-play-80066/results.xml`: 229/229 passed.
- `20260907-140426-build-macos-82104/unity.log`: final player build passed.
- `20260907-140631-build-android-83154/unity.log`: Android Development APK passed;
  sibling `../outputs/SessionRpg-dev.apk`, 221.2 MiB reported by the builder.

An earlier full run passed 771 EditMode and 227/229 PlayMode. It exposed an actual
4:3 narrow-display collision between the left disc and the centered utilities,
plus the unscaled square-control assumption in the Four Lanes camera test.
Raising the left disc/common neighbors fixed spacing; the camera check now uses
scaled circular geometry and still includes the wider offset shadow. These earlier
failures are not the final result counts.

Automated result counts:

771 EditMode + 229 PlayMode = 1,000/1,000 passed, zero failed/skipped. These are the
final HUD-revision results, before the subsequently requested environment work.

Manual checks:

Seven final actual-player captures were reviewed in
`build/review/hud-large-130-final-20260907/`. Each was launched separately with the
final macOS player, `-sessionRpgCapture <absolute PNG>`, `-screen-fullscreen 0`,
`-logFile <absolute log>`, and these case-specific arguments:

| PNG | Mode / specialization | Resolution | Additional flag |
|---|---|---|---|
| `arena-fire-720.png` | Arena / Fire | 1280 × 720 | `-sessionRpgCaptureSwipeAbility DragonBreath` |
| `pve-discipline.png` | PveLeveling / Discipline | 1920 × 1080 | `-sessionRpgCaptureCamp 1` |
| `pve-shadow.png` | PveLeveling / Shadow | 2592 × 1080 | `-sessionRpgCaptureCamp 1` |
| `dungeon-frost.png` | Dungeon / Frost | 1920 × 1080 | none |
| `training-shadowdance.png` | Leveling / ShadowDance | 2400 × 1080 | none |
| `siege-solo-mutilate.png` | HeroSiege / Mutilate | 2400 × 1080 | `-sessionRpgCaptureSiegeVariant SoloLane` |
| `siege-four-hunter.png` | HeroSiege / Marksmanship | 2592 × 1080 | `-sessionRpgCaptureSiegeVariant FourLanes` |

Mode/spec use `-sessionRpgCaptureMode` / `-sessionRpgCaptureSpecialization`;
resolution uses `-screen-width` / `-screen-height`. No camera override was used.
This bounded matrix covers all seven specs/four classes and 16:9, 20:9, 2.4:1.
The enlarged discs/icons are visible and disjoint from each other/utilities; Four
Lanes keeps its active combat left of them. All seven players exited zero and
reported capture completion, without Error/Exception/failed entries. Shutdown
thread-finalization and MemoryLeaks diagnostics remain; these are not soak proof.
The harness stages reproducible review states, not a manual combat playthrough.

Earlier captures under `build/review/hud-large-130-20260907/` show the first layout
and are not final proof. No generated concept art is gameplay evidence.

Performance/device/network checks: passed | failed | not run

- Core/Application warm allocation checks: passed in fresh EditMode.
- Native macOS build/render and PlayMode HUD/pool allocation: passed.
- Android build: passed; physical install/render/performance not run.
- Physical-device install, touch comfort, readability and multi-touch: not run.
- Sustained CPU/GPU, overdraw, memory and thermal profiling: not run.
- Networking/server/online play: not run; unchanged.
- Full manual combat playthrough and broad visual acceptance: not run.

Known caveats:

Canvas scale is not a measurement of physical millimeters. The user must still
check the installed build on their phone. Larger controls use more screen space.
Existing Dungeon off-screen enemy cards can overlap common controls; crowded
actor/roster labels and existing world-art limitations are not fixed by this resize.

Next safe action:

Test the larger controls on the user's phone, including outward flicks and
simultaneous movement/casting. Review the separate Leveling/Arena/Dungeon target
art before committing to any additional environment production work.
