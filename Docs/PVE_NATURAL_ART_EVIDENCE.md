# Sunbreak Basin — naturalistic surface/geometry candidate

2026-09-04. The user rejected the previous basin's toy-like graphics. This record
separates the new in-engine candidate from both the rejected blockout and generated
texture previews. No concept image is used as gameplay evidence.

Scope:

- Unity presentation authoring, first-party source albedos, import/material
  contracts, generated chunk geometry and Editor tests. No Core, Application,
  progression, camp, camera, HUD, lighting, world membership or server change.
- Retain the 180 × 180 m open basin, seven camps, thirteen authoritative cover
  footprints, flat combat ground, daylight and no-quest local gameplay.

Source changed:

- `Assets/SessionRpg/Editor/Authoring/SunbreakBasinAuthoring.cs`: replace flat shelf
  tops with layered eroded profiles and fractured boulders; replace polygon canopy
  blobs with tapered branching trees and individual bent leaves; use smaller
  masonry courses and worn arch stones. Keep all raised solids within Core-owned
  cover or beyond the playable boundary. Add explicit leaf/bark UVs and smooth
  trunk normals. Distant trees have lower leaf density. A matched-frame iteration
  replaces radial rock faceting with three uneven smooth shoulders and reduces
  oversized paving fragments to human-scale limestone pieces.
- `Assets/SessionRpg/Editor/Authoring/SunbreakSurfaceAuthoring.cs`: new repeatable
  first-party import and isolated material bindings, 4 m ground albedo repeats and
  world-continuous linear camp tint. Local URP source review established that
  detail UVs follow the base transform; `48 × (1/48) = 1` preserves macro phase.
- `Assets/SessionRpg/Editor/Authoring/AuthoringAssets.cs`: basin-only neutral stone,
  bark and foliage palette, textured surfaces; preserve other mode materials.
- `Assets/SessionRpg/Tests/EditMode/Editor/SunbreakBasinAuthoringTests.cs`: twenty
  cases, nine more than the prior fixture. Import bounds/material isolation,
  UV composition, eroded silhouette/footprint agreement, leaf UVs and surface
  frames supplement the existing nine-scene/no-physics/complexity contracts.
- Four original, unedited generated albedo masters and their Unity metadata at
  `Assets/SessionRpg/SourceArt/Environment/SunbreakNaturalV1/`. Built-in image
  generation was used through the imagegen skill; see the exact prompts, hashes
  and saved filenames in [source provenance](../Assets/SessionRpg/SourceArt/Environment/SunbreakNaturalV1/PROVENANCE.md).
- Workflow/source ownership, visual target and evidence documentation.

Generated changed:

- Nine basin scenes, existing basin meshes/materials and the shared meadow macro
  are regenerated. The full generator also rewrites Bootstrap, Combatant, arena
  prefabs, legacy causeway chunks, catalogs and selected build settings. Generated
  YAML was not hand-edited. Existing dirty work was preserved.
- Generated basin complexity: 32,230–43,850 triangles and 9–21 mesh renderers per
  chunk (65,000/80 caps). This is higher than the rejected blockout and is not a
  GPU/frame-time acceptance result. The existing 155 mesh assets remain bounded.
- A before/after SHA-256 audit covered 61 pre-existing generated materials,
  causeway scenes, arena prefabs and shared scene/prefab/catalog/build-list files.
  **42 remained byte-identical**: all 40 non-target materials plus the world catalog
  and build list. The seven intentionally changed Sunbreak materials differ. The
  generator also rewrote seven causeway scenes, three arena prefabs, Bootstrap and
  Combatant; byte identity or pixel equivalence is not claimed for those twelve
  broad generated outputs. No generated YAML was normalized or hand-edited.

Commands (fresh):

- `Tools/Agent/unity.sh doctor`: passed; Unity 6000.5.6f1, Editor closed.
- `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh rebuild`: passed;
  `../outputs/agent/20260904-171445-rebuild-83203/unity.log` (final).
- `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh validate`: passed;
  `../outputs/agent/20260904-171519-validate-83546/unity.log` (final).
- `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-all`: passed;
  EditMode `../outputs/agent/20260904-171644-test-edit-84191/results.xml`, PlayMode
  `../outputs/agent/20260904-171700-test-play-84191/results.xml`.
- `UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh build-macos`: passed;
  `../outputs/agent/20260904-171601-build-macos-83821/unity.log`, output
  `../outputs/SessionRpg-mac-capture.app`.
- A first `test-all` attempt reached **720/721 EditMode** before stopping on one
  new material-keyword assertion. Inspection of local URP `LitDetailGUI.cs` showed
  scale 1 uses `_DETAIL_MULX2`, not `_DETAIL_SCALED`; source/test were corrected,
  regenerated, and the complete fresh pass above supersedes that result.

Automated result counts:

- EditMode: **722 passed / 0 failed / 0 skipped**.
- PlayMode: **221 passed / 0 failed / 0 skipped**.
- Total Unity: **943 passed**. The existing Core/Application allocation and mode
  regressions passed alongside twenty basin-authoring cases. Server tests were not
  run because these changes do not touch Core, Application, networking or server.

Manual checks:

- Inspected eleven successful final-player captures: all seven specializations and
  camps, the refuge, four camera headings, and 1920 × 1080 (16:9), 2400 × 1080
  (20:9), 2592 × 1080 (2.4:1). Every player exited successfully; all eleven logs
  contain their final capture line and no Error/Exception/Fatal entries. Captures:
  `build/review/pve-natural-final-20260904/<key>/frame.png`, where keys are `camp0`
  through `camp6`, `camp3-north`, `camp3-south`, `camp3-west`, and `refuge`.
- Compared with the rejected blockout, ground has fine dry-grass/soil detail;
  trees are taller and visibly branched with individual leaves; paving pieces are
  smaller; rock lighting is smooth rather than radially faceted. Combat remains
  visible at the authored default angle.
- **Mature/cinematic art acceptance remains failed.** Oval Core-cover islands still
  resemble model bases; leaves remain small flat diamonds; the dark masonry sits on
  isolated mounds; flat turf dominates the frame with sparse, even dressing; pale
  stones can read as stickers. Camp 0 and refuge reveal hard straight boundary
  banks. Rotated headings retain the known HUD/off-screen framing failures.
- Source texture previews were also inspected; they are not gameplay evidence.

Performance/device/network checks: passed | failed | not run

- Allocation: existing automated warm Core/Application and cached-HUD checks pass;
  no new runtime code or per-frame work was added. Whole-player frame allocations
  were not profiled.
- CPU/GPU profiling, Android build/device, iOS, physical touch, sustained traversal,
  multi-session soak and connected multiplayer: not run.

Known caveats:

- This is a naturalistic art candidate, not user approval or cinematic production
  acceptance. Four AI-generated images provide albedo only, not measured scanned
  PBR; no normals were fabricated from color. Retained licensed masonry PBR is
  separate and unchanged. Fine surface repetition still needs gameplay review.
- Enemies, UI, flat terrain topology and prototype encounter density are unchanged.
  Prior top/right HUD overlap and off-screen heading cases remain unresolved.
- Source textures are 1254²; runtime imports are 1024², with leaves at 512²,
  mipmapped and non-readable. Android ASTC 6×6 is an import setting, not device proof.
- The process-local Burst compiler workaround remains necessary for these local
  Unity commands; this change does not fix the native Burst crash.

Next safe action:

- Review the candidate with the user. Reaching a mature location requires a
  cohesive authored terrain/landmark kit and regional composition; removing the
  conspicuous oval bases also requires redesigning Core's circular-cover topology,
  not hiding authoritative collision with decoration. Confirm that larger change
  before implementing it. Profile foliage/shadows and texture residency on a
  physical target device before claiming mobile acceptance.
