# PvE location daylight revision — 2026-09-04

Scope:

Responds to the user's rejection of the dark, sparse Ashen Causeway location.
Classification: environment authoring, Unity presentation, capture support, tests
and documentation. No Core/Application combat, XP, camp, save or server rules are
changed. Existing unrelated worktree changes remain intact. This supersedes the
location verdict in [the initial PvE evidence](PVE_LEVELING_EVIDENCE.md), not its
historical gameplay evidence.

Source changed:

- `Editor/Authoring/AuthoringAssets.cs`: dedicated warm world stone, cinder,
  masonry, rough paving, distant haze, vegetation and muted wooden-supply atlas
  materials. Retained source textures/imports are unchanged; Arena materials are
  not recolored by this revision.
- `Editor/Authoring/WorldChunkAuthoring.cs`: presentation revision 3, world-only
  material bindings and source-generated near-field detail roots in seven chunks.
- `Editor/Authoring/WorldPavingDetailAuthoring.cs`: deterministic flat, chipped
  edge paving with rough stone relief rather than miniature cobblestone patches.
  The center stays quiet; no collision or false walkable elevation.
- `Editor/Authoring/WorldRegionDetailAuthoring.cs`: district ruin features,
  shoulder rubble and sparse dimensional vegetation. Raised details stay beyond
  the Core route; detail and complete-chunk budgets remain enforced.
- `Unity/Presentation/WorldLightingPresenter.cs`: reuses the existing directional
  key, supplies explicit daylight ambient fill/fog, reasserts fill after additive
  loads and restores the exact entry lighting when leaving the world. No added
  light, global exposure change, quality-profile clone or steady-frame update.
- `Editor/Authoring/SessionRpgAuthoringGenerator.cs` and
  `Unity/Gameplay/PrototypeGameController.cs`: serialized lighting ownership,
  binding validation, world entry/exit and teardown.
- `Unity/Gameplay/PrototypeGameController.Grinding.cs` and
  `SessionRpgReferenceCapture.cs`: bounded optional capture-only lateral offset;
  ordinary spawn behavior remains unchanged. PvE capture now waits for 38 actual
  gameplay ticks after countdown, with a 30-second timeout, instead of mistaking
  elapsed rendering/wall time for simulation progress. Correct singular camp-count
  text.
- `WorldCinematicAuthoringTests`, `WorldLightingPlayModeTests`, and
  `PveLevelingGameplayPlayModeTests`: palette, material, geometry, no-flat-disc
  vegetation, ambient-probe, lifecycle-restoration and capture-bound regressions.
- `Tools/Agent/visual-review-matrix.json`, world/mode/readiness documentation:
  bounded 11-frame daylight review and explicit remaining acceptance gates.

Generated changed:

The authoring rebuild intentionally rewrites Bootstrap (lighting binding), the
seven Ashen Causeway chunk scenes, world material assets and generated paving /
district / vegetation meshes with their Unity-created metadata. The shared
rebuild also reevaluates existing generated combatant, catalog and presentation
outputs from pre-existing source changes; no unrelated changes were reverted.
No scene, prefab or generated material YAML was hand-edited. Captures, logs,
test XML and the macOS player remain disposable evidence outside `Assets`.

Commands (fresh):

```sh
Tools/Agent/unity.sh doctor
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh rebuild
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh validate
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-all
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh build-macos
```

The process-only Burst environment workaround is retained from the prior run's
native compiler fault. No package/global/project Burst preference was changed;
these runs do not prove that native compiler fault repaired.

Initial generation at `../outputs/agent/20260904-153509-rebuild-39407/unity.log`
correctly rejected 12,074 detail triangles against the 8,000 cap. Repeating rubble
was simplified before the successful `20260904-154000-rebuild-41475` rebuild and
`20260904-154019-build-macos-41474` capture build. Visual review then drove coherent
masonry/vegetation and rough-paving changes. The next rebuild/build/validation
passed at `20260904-155308-rebuild-47007`, `20260904-155328-build-macos-47274`, and
`20260904-155344-validate-47453` under the same agent-output directory.

That intermediate suite passed 635/635 EditMode and 212/215 PlayMode. Two new
lighting assertions incorrectly required 1.65x uplift of the complete directional
probe, although the unchanged sky contribution yields a measured 1.589x uplift;
the regression now requires at least 1.50x and still checks vertical contrast.
The shoulder-capture test incorrectly expected exact requested coordinates,
ignoring Core's 0.0025 m contact skin. It now allows 0.005 m numerical/contact
correction while retaining the strict playable-width check. Gameplay and the
lighting look were not changed to satisfy those expectations.

Automated result counts:

Final generated-art rebuild passed:
`../outputs/agent/20260904-155811-rebuild-49399/unity.log`.
Final validation passed:
`../outputs/agent/20260904-160816-validate-53854/unity.log`.
Final EditMode: **635 passed, 0 failed, 0 skipped**:
`../outputs/agent/20260904-160826-test-edit-53985/results.xml`.
Final PlayMode: **216 passed, 0 failed, 0 skipped**:
`../outputs/agent/20260904-160838-test-play-53985/results.xml`.
Final local macOS capture build passed:
`../outputs/agent/20260904-160947-build-macos-53853/unity.log`.
An intermediate post-art suite passed 635 EditMode / 215 PlayMode before adding
the capture-timing regression. No generated art changed afterward. The final
updated focused fixtures contain 17 world-art authoring,
9 world-lighting and 11 integrated PvE gameplay cases. Capture-matrix JSON validation passed with
`jq -e '.captureContract.maximumTotalCaptures == 50 and .scenarios[0].maximumCaptures == 11' Tools/Agent/visual-review-matrix.json`.

Manual checks:

The first daylight evaluation frames in `build/review/pve-daylight-20260904/`
exposed bright toy-like atlas architecture, repeated flat green vegetation discs,
and miniature floor-texture patches. Those evaluation frames were rejected and
retained as iteration evidence. They are not final acceptance screenshots.
The two intermediate frames in `build/review/pve-daylight-final-20260904/`
improved the masonry and vegetation but exposed pale paving resembling road
markings. That intermediate directory is also superseded: paving was muted,
narrowed, moved toward the curbs, and removed from the central combat space.
The next `build/review/pve-daylight-reviewed-20260904/` set exposed an existing
wall-clock capture limitation: some frames froze during countdown under rendering
delays. Those frames remain intermediate evidence. Capture readiness now requires
actual simulation advancement and has a new regression test; final review uses
only `build/review/pve-daylight-settled-20260904/`.

All 11 final player processes exited 0, wrote a successful capture log and produced
the PNGs below. Every frame shows an active named camp, not countdown or refuge
recovery. Each used a separate capture-save directory, not normal gameplay XP.
The images were visually inspected (root: eight; independent reviewer: camps
1/2/5). `sips -g pixelWidth -g pixelHeight build/review/pve-daylight-settled-20260904/*/frame.png`
confirmed the dimensions. Capture logs contain no exception, error or timeout
matches. Scoped source/document whitespace checks passed.

| Folder (`frame.png` in each) | Camp | Specialization | Pixels | Yaw | Lateral offset |
|---|---:|---|---|---:|---:|
| `camp0` | 0 | Frost | 1920x1080 | 90 | 0 |
| `camp1` | 1 | Discipline | 2400x1080 | 90 | 0 |
| `camp2` | 2 | ShadowDance | 2400x1080 | 90 | 0 |
| `camp3` | 3 | Fire | 2400x1080 | 90 | 0 |
| `camp4` | 4 | Marksmanship | 2592x1080 | 90 | 0 |
| `camp5` | 5 | Mutilate | 2400x1080 | 90 | 0 |
| `camp6` | 6 | Shadow | 2400x1080 | 90 | 0 |
| `camp4-yaw35` | 4 | Marksmanship | 2592x1080 | 35 | 0 |
| `camp4-yaw145` | 4 | Marksmanship | 2592x1080 | 145 | 0 |
| `camp0-north` | 0 | Frost | 1920x1080 | 35 | +7 |
| `camp0-south` | 0 | Frost | 1920x1080 | 145 | -7 |

Every final capture invoked
`../outputs/SessionRpg-mac-capture.app/Contents/MacOS/Session RPG Prototype`
with `-sessionRpgCapture <absolute folder/frame.png> -sessionRpgCaptureMode PveLeveling
-sessionRpgCaptureCamp <camp> -sessionRpgCaptureSpecialization <spec>
-sessionRpgCaptureCameraYaw <yaw> -screen-width <width> -screen-height 1080
-screen-fullscreen 0 -logFile <absolute build/review/pve-daylight-settled-20260904-folder.log>`.
The two shoulder cases additionally passed `-sessionRpgCaptureCampLateralOffset 7`
or `-sessionRpgCaptureCampLateralOffset -7`. No other composition overrides were used.

Visual verdict: the playable surface and shadowed characters are substantially more
readable than the rejected initial dark frames. Textured chipped arches/piers and
rubble fit the retained fortress palette; wooden supplies are limited to service
camps, vegetation is subtle and dimensional, and the paving no longer resembles
bright road paint. No new scenery visibly blocks the player or central combat
lane in these samples. The scene is still muted and corridor-like, with bare
outer ground and dark off-world corners. The upper HUD obscures distant NPCs
in several districts, and the right controls overlap enemies at the north/yaw35
shoulder. Cinematic-quality/user-art acceptance remains **not passed**.

Normal-color stills only: desaturated review, interactive mouse/touch testing,
continuous seam traversal in both directions, and a complete grinding playthrough
were not run. These are staged resting images after real simulation ticks, not
uncoached usability or completed-combat evidence. Training/Dungeon lighting routing
and restoration to other modes are covered by automated tests, not fresh manual
screenshots of those modes.

Performance/device/network checks: passed | failed | not run

- Existing warm Core/Application and pooled combatant-view allocation tests:
  passed in the fresh full suites; these bounded tests are not whole-frame or
  environment GPU profiling.
- Full gameplay CPU/GPU/frame allocation profiling and long soak: not run.
- Android build, physical Android/iOS, touch comfort, FPS and thermals: not run.
- Online/co-op/networked gameplay: not run; no combat transport was added.
- Separate local server regression: not run in this presentation-only revision.

Known caveats:

This remains a prototype authored 448 m causeway, not procedural terrain, a
finished cinematic environment or user-approved production art. Geometry limits
are safeguards, not measured mobile performance. The existing top-center HUD and
temporary camp notice can obscure distant actors; this revision does not redesign
the HUD. Broad brown shoulders, straight corridor repetition and dark off-world
corners at some camera endpoints remain visible prototype limitations. No camera
pitch/distance changes or hidden-HUD beauty angles are used for
the comparison. Training and Dungeon floor one intentionally share this world
look; other modes must restore their previous lighting.

Next safe action:

Review the actual gameplay-camera revision, then manually traverse the whole
route and its seams in both directions before pursuing further environment-art
or physical-device acceptance. Gameplay progression and no-quest scope remain
unchanged.
