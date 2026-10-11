# PvE Leveling — 2026-09-04

Scope:

A playable local, solo, no-quest mob-grinding slice. Classification: Core,
Application, Unity presentation/input integration, authoring, tests, documentation.
The user requested leveling, progression, varied enemies and a cinematic location.
This adds persistent XP to a separate mode, not to guided Training. See
[mode rules](PVE_LEVELING_MODE.md). Existing unrelated worktree changes are preserved.

Source changed:

- Core `PersistentLevelProgression.cs`, `CombatRules.cs`, `CombatSimulation.cs`,
  `CombatTypes.cs`, `RunIdentity.cs`: independent level 1–10 rules, appended stable
  mode IDs, mode-correct snapshots and safe region-NPC retirement.
- Application `GrindingRegionRunController.cs`, `LocalGrindingProgressStore.cs`,
  `DeterministicLevelingMobBrain.cs`, `SessionFlowController.cs`, and
  `SessionRecords.cs`: seven spatially activated camps, eight existing archetypes,
  authoritative unique death credit, leashing/rearm, per-specialization durable XP,
  explicit mode routing/records. Mutual kills award identical XP regardless of
  same-tick death-event order; later-tick post-death credit is rejected.
- Unity `PrototypeGameController.cs` and `.Grinding.cs`: pool-based camp lifecycle,
  XP/HUD, save-failure pause/retry, refuge healing, death recovery and local entry.
  Disk writes occur outside `TickSession`; shutdown bypasses retry throttling.
- Unity `CombatantView.cs`: prewarmed per-model renderer caches avoid repeated
  renderer-array allocation when changing pooled enemy archetypes and preserve
  authored disabled-renderer defaults.
- Unity `PrototypeHud.cs`, `StrategyFollowCamera.cs`, and
  `SessionRpgReferenceCapture.cs`: separate home card, persistent XP presentation,
  bounded PvE camera and isolated camp capture entry.
- Editor `HudAuthoring.cs`, `SessionRpgAuthoringGenerator.cs`,
  `EnvironmentAuthoring.cs`, `WorldChunkAuthoring.cs`: generated fifth mode card,
  serialized contract validation, presentation-version checks and off-route
  layered ruin silhouettes for every district.
- New tests: `PersistentLevelProgressionTests`, `GrindingRegionRunControllerTests`,
  `GrindingMobBrainTests`, `LocalGrindingProgressStoreTests`,
  `WorldCinematicAuthoringTests`, `PveLevelingGameplayPlayModeTests`,
  `PveLevelingCameraPlayModeTests`, `CombatantPoolAllocationPlayModeTests`.
- `DESIGN.md`, `ARCHITECTURE.md`, `Docs/AGENT_WORKFLOW.md`,
  `Docs/DEMO_READINESS.md`, `Docs/WORLD_AUTHORING.md`, mode/evidence documents:
  approved expansion, current implementation boundary and verification.

Generated changed:

Authoring intentionally rebuilt `Scenes/Bootstrap.unity`, standard generated
combatant/art/settings outputs, the world catalog and seven Ashen Causeway chunk
scenes. The new serialized home card and district depth roots come from C# authoring.
The shared rebuild also evaluates existing Arena/Siege generators; pre-existing
worktree source/output changes were not reset. No generated YAML was hand-edited.
Unity-created `.meta` files for new sources/tests are retained. Builds, test logs,
XML and screenshots are disposable evidence outside `Assets`.

Commands (fresh):

```sh
Tools/Agent/unity.sh doctor
Tools/Agent/unity.sh rebuild
Tools/Agent/unity.sh validate
Tools/Agent/unity.sh test-all
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-all
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-play --filter SessionRpg.Tests.PlayMode.PveLevelingGameplayPlayModeTests
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh validate
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-all
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh build-macos
Tools/Agent/server.sh test
```

The successful rebuild log is
`../outputs/agent/20260904-145047-rebuild-19971/unity.log`. Earlier compile attempts
caught integration/test API mismatches; these were corrected before generation.
The first default EditMode run crashed in native Burst compiler `LinkAndFinalize`
with exit 139 and produced no NUnit XML:
`../outputs/agent/20260904-145157-test-edit-20524/unity.log`.
The installed Burst package documents `UNITY_BURST_DISABLE_COMPILATION`; it is set
only for the subsequent test/build processes. No project/global Burst preference
was modified. This workaround is not evidence that the native compiler is repaired.

The first completed suites reported EditMode 622/622 and PlayMode 200/202.
The failures identified a test using insufficient Fire Blast damage and a real
missing `InvalidDataException` catch in save loading. The corrected real-Fireball
test waits for cast completion; corrupt saves now fail closed. A new regression
also verifies actual controller destruction saves pending XP before a retry deadline.
Focused intermediate gameplay results: 7/7 passed at
`../outputs/agent/20260904-150046-test-play-24662/results.xml`.
The first rendered camp frame then exposed a generic target name. PvE target
panels now show the archetype and persistent level, including the elite; two
additional integrated regressions verify focus changes and the boss identity.

Automated result counts:

Final EditMode: **625 passed, 0 failed, 0 skipped** at
`../outputs/agent/20260904-150747-test-edit-27913/results.xml`.
Final PlayMode: **205 passed, 0 failed, 0 skipped** at
`../outputs/agent/20260904-150803-test-play-27913/results.xml`.
New focused fixtures: 13 progression, 16 camp-loop, 5 leashed-brain, 32 store,
7 world-authoring, 9 integrated-gameplay, 5 camera and 3 pooled-view cases.
Final validation passed at
`../outputs/agent/20260904-150128-validate-25024/unity.log`.
Final macOS capture build passed at
`../outputs/agent/20260904-150907-build-macos-27912/unity.log`; its build entry point
also revalidates the tracked foundation before building.
The server/shared-Core regression passed **87/87**, zero failures/skips:
`Server/SessionRpg.Server.Tests/TestResults/_10_2026-09-04_14_50_58_net10.0.trx`.
Independent linked Core/Application compile and assertion smoke passed 34/34 after
the mutual-kill correction, with zero compile warnings/errors. It is supplementary
evidence, not a substitute for the Unity suites.

Manual checks:

Fresh local-player PNGs were inspected at these paths under
`build/review/pve-final-20260904/`:

| Frame | Mode/spec/camp | Actual pixels | Camera yaw |
|---|---|---:|---:|
| `home/frame.png` | Home, Training recommended; fifth PvE card visible | 1920×1080 | — |
| `camp0/frame.png` | PvE / Frost / Cinder Outriders | 1920×1080 | 90° |
| `camp3/frame.png` | PvE / Fire / Ashen Conclave | 2400×1080 | 90° |
| `camp4-yaw35/frame.png` | PvE / Marksmanship / Ossuary Marauders | 2592×1080 | 35° |
| `camp4-yaw145/frame.png` | PvE / Marksmanship / Ossuary Marauders | 2592×1080 | 145° |
| `camp6/frame.png` | PvE / Frost / Rift Warden | 2400×1080 | 90° |

Capture executable:
`../outputs/SessionRpg-mac-capture.app/Contents/MacOS/Session RPG Prototype`.
Each run used `-sessionRpgCapture` with the absolute PNG path above, plus
`-screen-width <table width> -screen-height 1080 -screen-fullscreen 0`.
Home used `-sessionRpgCaptureScreen Home`. Gameplay used
`-sessionRpgCaptureMode PveLeveling -sessionRpgCaptureCamp <table camp index>
-sessionRpgCaptureSpecialization <table spec>`; endpoint captures additionally used
`-sessionRpgCaptureCameraYaw 35` or `145`, and elite explicitly used `90`.
`-logFile` targeted `build/review/pve-final-20260904-<frame folder>.log` by absolute
path. All six processes exited 0 and wrote their PNGs; each camp used a separate
capture-save directory, not the user's gameplay XP.
`sips -g pixelWidth -g pixelHeight build/review/pve-final-20260904/*/frame.png`
confirmed the listed dimensions. Scoped source/document `git diff --check` passed;
capture logs contain no exception/error/timeout messages.

Checked 16:9, 20:9 and 2.4:1 HUD fit, mode-card separation, named/leveled targets,
one/two/three-hostile populations and both bounded camera endpoints. The elite
frame is still East Watch with a living player, not a refuge recovery frame.
Full kits for Frost, Fire and Marksmanship are visible; Priest/Rogue manual capture
and full traversal/playthrough were not run. These are staged resting frames after
real local simulation, not proof of completed fights or human usability.

Visual verdict: functional labels/controls fit these frames, but the region is
dark and sparse, and the top-center HUD/temporary camp notice can obscure distant
actors (especially the elite). Cinematic-quality/readability acceptance is **not
passed**; brighter character separation, location dressing and HUD composition
need another focused art pass. No user art approval is inferred.

Automated gameplay tests cover all seven
specialization entry/full-kit paths, separate Training routing, real kill XP,
level-up, leave/re-entry, real NPC player death, refuge recovery, no timeout,
corrupt load and interrupted-save shutdown. They do not represent human playtests.

Performance/device/network checks: passed | failed | not run

- Warm Core/Application allocation loops: passed in the final Unity suite (0 B in
  measured camp-cycle/despawn loops).
- Unity pooled renderer/model reuse: passed for same-class and all-eight-archetype
  cycles (0 B measured); this is not a whole-game frame/GC or GPU measurement.
- Full gameplay allocation/profile/long soak: not run.
- Android build, physical Android/iOS, touch comfort, FPS/thermals: not run.
- Online/co-op/networked gameplay: not run; no Unity-server combat adapter exists.
- Local server HTTP/persistence regression: passed as part of the 87 tests above.

Known caveats:

The region is the existing authored 448 m Ashen Causeway with an added composition
pass, not procedurally generated terrain or production art acceptance. Only one
camp/up to three hostiles is live. XP persists per specialization on this device;
camp clocks/health/position do not. Saves are checksummed, not tamper-proof or
cloud-backed. A process exit while storage remains unavailable can lose pending
in-memory XP. No quests, drops, equipment, talents or incremental ability unlocks.
Full kits are available from level one; Training's learned-only kits are unchanged.
Level curve, rewards and class difficulty are prototype tuning, not accepted balance.

Next safe action:

Play the separate PvE Leveling card on desktop, exercise a complete camp-clear /
retreat / return / death / re-entry loop, then tune kill time and travel pacing
before adding more progression systems. Connect the established server only in a
separate authoritative gameplay slice; do not upload local prototype XP as trusted
server progression. The requested cinematic direction still needs the focused
lighting/dressing/HUD art pass identified above.
