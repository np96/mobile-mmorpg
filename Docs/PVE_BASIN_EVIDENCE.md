# Sunbreak Basin — PvE region redesign

The user rejected the linear road after the daylight pass. This revision gives
PvE Leveling its own two-dimensional authored region. Historical causeway captures
and test counts do not validate this revision.

The user subsequently rejected this revision's graphics as too toy-like. Its layout
and tests remain historical evidence; the next visual revision is recorded in
[natural-surface evidence](PVE_NATURAL_ART_EVIDENCE.md).

Scope:

- Core geometry, Application camps/NPC navigation, Unity routing/camera/guidance,
  world authoring/manifests, tests and documentation.
- Persistent XP, enemy kits, camp IDs/rewards, local-only scope, no-quest rule and
  Training's guided road are preserved.

Source changed:

- `Core/PveRegionLayout.cs`, `ArenaCollisionMap.cs`, `CombatRules.cs`: stable
  world/zone identity, 180 m square playable envelope, seven scattered camps,
  radial refuge and thirteen shared circular obstacles.
- `Application/GrindingRegionRunController.cs`, `DeterministicLevelingMobBrain.cs`:
  shared camp positions and PvE-only cover/return-path detours.
- `Unity/Gameplay/PrototypeGameController{,.Grinding}.cs` and
  `Unity/Camera/StrategyFollowCamera.cs`: basin selection, radial recovery,
  nearest-camp compass guidance, corrected capture spawns and full yaw.
- `Editor/Authoring/SunbreakBasinAuthoring.cs`, `AuthoringAssets.cs`,
  `WorldChunkAuthoring.cs`, `SessionRpgAuthoringGenerator.cs`,
  `Editor/World/WorldZoneManifest.cs`: dedicated meadow/plaza/ruin recipe,
  Core-matched raised solids, multi-zone validation and exact biome/POI pairing.
- World catalog/new Sunbreak manifest, project context, Core/Application/Editor/
  PlayMode tests, and mode/world/architecture/readiness documentation.

Generated changed:

- Added nine Sunbreak Basin scenes, 155 generated meshes, one shared 1024² meadow
  texture, ten materials and their Unity metadata. Runtime catalog/build membership
  now contains Bootstrap + seven Training chunks + nine basin chunks.
- The rebuild also rewrote Bootstrap, Combatant, existing arena prefabs and
  causeway scenes through the existing generator. No generated YAML was hand-edited.
  The pre-existing dirty worktree was preserved; generated rewrite review is below.
- A before/after SHA-256 audit of 51 pre-existing generated files found all
  **37 sampled materials byte-identical after the final build**. Intermediate
  changes in hidden `_Color` fields were Unity/URP float normalization; four
  individually audited material hashes returned exactly to their prior values.
  The fourteen rewritten scene/prefab/catalog/build-list files remain generated
  output; no legacy scene byte-identity or whole-game pixel-equivalence is claimed.

Commands (fresh):

- `Tools/Agent/unity.sh doctor`: passed, Unity 6000.5.6f1, Editor closed.
- `Tools/Agent/server.sh doctor`: passed, pinned .NET 10.0.400.
- `Tools/Agent/server.sh test`: **87 passed, 0 failed, 0 skipped** after the
  completed Core changes; `Server/SessionRpg.Server.Tests/TestResults/_10_2026-09-04_16_32_55_net10.0.trx`.
- `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh rebuild`: passed.
  Final log: `../outputs/agent/20260904-163934-rebuild-67756/unity.log`.
  The earlier successful `20260904-163335-rebuild-64769` pass was superseded by
  clearing-falloff, arch-face and visible-boundary fixes.
- `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh validate`: passed;
  `../outputs/agent/20260904-164019-validate-68181/unity.log`.
- `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-all`: passed serially;
  EditMode `../outputs/agent/20260904-164025-test-edit-68180/results.xml`,
  PlayMode `../outputs/agent/20260904-164038-test-play-68180/results.xml`.
- `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh build-macos`: passed;
  `../outputs/agent/20260904-164219-build-macos-69170/unity.log`.

Automated result counts:

- EditMode: **713 passed / 0 failed / 0 skipped**.
- PlayMode: **221 passed / 0 failed / 0 skipped**.
- Total Unity: **934 passed**. Includes Core terrain/LOS/teleport/loop/allocation,
  PvE NPC detours/refuge abandonment, strict manifests, every generated basin
  chunk, both-axis streaming, same-count camp HUD transitions, persistence,
  all-spec entry and unchanged Training/mode regression coverage.
- Server: 87/87 passed; this establishes linked Core compatibility and local HTTP/
  persistence tests, not connected game combat.

Manual checks:

- Visually inspected eleven final-player captures in PvE Leveling: all seven
  specializations (Mage Frost/Fire, Priest Discipline/Shadow, Rogue ShadowDance/
  Mutilate, Hunter Marksmanship), all seven camps, the refuge and four camera
  headings. Checked 1920 × 1080 (16:9), 2400 × 1080 (20:9) and 2592 × 1080
  (2.4:1); this is a capture matrix, not every spec at every aspect ratio.
- Each player process exited successfully and logged its captured frame. A scan
  of all twelve capture logs found no Error/Exception/Fatal entries. Captures use
  isolated saves and the existing real-countdown/38-tick capture hook.
- Normal camp frames show bright open ground without the previous road. Camera
  rotation changes approaches and framing. **Visual acceptance failed**: distant
  or tall enemies overlap the top HUD; north-facing enemies can sit under the
  right action HUD, and west-facing enemies can be off-screen with overlapping
  threat indicators. Meadow detail, repeated outcrops and refuge terrain joins
  remain visibly primitive.
- A twelfth, diagnostic `layout-overview` capture used a 280 m camera distance
  and produced a black world image with HUD only. It is rejected as evidence of
  topology or normal gameplay; no overview acceptance is claimed.
- Human input traversal, seam crossing, sustained combat and other modes were
  not manually played this turn. Automated streaming-window visits and regression
  tests are not substitutes for human traversal or physical-device performance.

Capture reproduction:

```sh
'../outputs/SessionRpg-mac-capture.app/Contents/MacOS/Session RPG Prototype' \
  -sessionRpgCapture '/ABS/game-rpg/build/review/pve-sunbreak-20260904/KEY/frame.png' \
  -sessionRpgCaptureMode PveLeveling -sessionRpgCaptureCamp CAMP \
  -sessionRpgCaptureSpecialization SPEC -sessionRpgCaptureCameraYaw YAW \
  -screen-width WIDTH -screen-height 1080 -screen-fullscreen 0 \
  -logFile '/ABS/game-rpg/build/review/pve-sunbreak-20260904-KEY.log'
```

`/ABS/game-rpg` denotes the absolute path to your Unity project checkout.
The following rows supply the exact varying arguments; all other camera values
use the authored PvE defaults (23 m distance, 49° pitch, 45° FOV).

| KEY | CAMP | SPEC | YAW | WIDTH |
| --- | ---: | --- | ---: | ---: |
| camp0 | 0 | Frost | 90 | 1920 |
| camp1 | 1 | Discipline | 90 | 2400 |
| camp2 | 2 | ShadowDance | 90 | 2400 |
| camp3 | 3 | Fire | 90 | 2400 |
| camp4 | 4 | Marksmanship | 90 | 2592 |
| camp5 | 5 | Mutilate | 90 | 2400 |
| camp6 | 6 | Shadow | 90 | 2400 |
| camp3-north | 3 | Fire | 0 | 1920 |
| camp3-south | 3 | Fire | 180 | 2400 |
| camp3-west | 3 | Fire | 270 | 2592 |
| refuge | -1 | Frost | 270 | 1920 |
| layout-overview (rejected diagnostic) | 5 | Mutilate | 0 | 2400 |

The rejected diagnostic also supplied `-sessionRpgCaptureCameraDistance 280
-sessionRpgCaptureCameraPitch 80 -sessionRpgCaptureCameraFocus 0
-sessionRpgCaptureCameraRightFocus -10 -sessionRpgCaptureCameraFov 55`.

Performance/device/network checks:

- Allocation: **passed** automated warmed Core/Application geometry/brain/camp
  tests and cached objective-refresh regression (0 B). This does not establish
  whole-player frame allocation or graphics performance.
- CPU/GPU profiling, Android build/device, iOS, physical touch, full 1–10 balance,
  uncoached playtest and connected multiplayer: not run.

Known caveats:

- Authored flat topology only; no randomized terrain, heightfield, cross-zone
  travel, quest, inventory or connected co-op.
- One camp and at most three active hostiles remain a bounded prototype, not a
  continuously populated world.
- The small basin can retain all nine chunks after exploration. Triangle/residency
  caps do not establish mobile frame-time, draw-call, memory or thermal acceptance.
- Generated mesh inspection reports 24,445–29,116 static triangles and 9–21 mesh
  renderers per chunk (limits 65,000/80); this is asset complexity, not measured
  visible triangles, batching, draw calls or device cost.
- Trees/ruins, enemy art and HUD remain prototypes. Full-heading occlusion and
  top-center/right action-HUD overlap were observed and remain unresolved.
- Actual default-camera frames confirm broad open ground, but meadow surfaces,
  angular trees, small repeated outcrops and flat paving fragments still read as
  blockout art. This pass is **not** cinematic-location or user art acceptance.
- Unity's native Burst crash workaround is process-local
  `UNITY_BURST_DISABLE_COMPILATION=1`, not a fixed compiler or a performance claim.

Next safe action:

- Review the playable basin's openness and camp approaches with the user before
  investing in final art. Then improve terrain transitions, landmark/camp variety
  and HUD-safe camera composition; manually traverse seams and profile on a
  physical target device. Do not treat passing automated tests as art acceptance.
