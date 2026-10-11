# Sunbreak Basin realism revision — 2026-09-05

Scope:

Core cover topology, Unity camera/presentation, authoring, bounded third-party
texture intake, shader/import contracts, tests and documentation. This implements
a more naturalistic environment pass for the user's persistent, no-quest mob
grinding mode. It is not procedural-world, online-co-op, or final-art acceptance.

Source changed:

- `Core/PveRegionLayout.cs`: thirteen stable landmark IDs now describe unions of
  three unequal overlapping circles (39 authoritative components). Six major
  formations sit near camp rims; every camp retains a clear nine-meter combat
  disk. World/zone IDs, seven camps, refuge, XP and levels are unchanged.
- `Core/ArenaCollisionMap.cs`: stop placement projection when a pass makes no
  changes. Movement, Blink, knockback, placement and LOS still share Core state;
  no Unity physics or 30 Hz simulation change.
- `Editor/Authoring/SunbreakBasinAuthoring.cs`: continuous union-footprint relief,
  low soil banks at small groves, separate embedded boulders, collapsed masonry,
  fern understory, smaller individual leaves, low litter/grass and irregular
  perimeter banks. Boundary rubble is separate from the upward-facing bank mesh.
- `Editor/Authoring/SunbreakScannedSurfaceAuthoring.cs` and
  `Shaders/SunbreakTerrain.shader`: independent albedo/GL-normal/roughness maps;
  two ground layers with a continuous linear 512² world mask; physically based
  lighting, cascade-safe shadows, depth and depth-normal passes. Lower-tier
  vertex lights stay in the vertex stage. Opaque geometry remains bounded.
- `Editor/Authoring/SunbreakSurfaceAuthoring.cs` and `AuthoringAssets.cs`: active
  scanned bindings and separate basin palette; original V1 images are retained.
  Dormant Lit texture dependencies are cleared before switching shaders.
- `Unity/Camera/StrategyFollowCamera.cs`: PvE pitch 37° instead of 49°, retaining
  distance 23 m, FOV 45°, look-ahead 5 m and full yaw orbit. This is the actual
  gameplay default, not a capture-only camera. Training camera is unchanged.
- Core/Application, world-manifest, generated-art and camera tests; new native
  `SunbreakTerrainShaderPlayModeTests` checks both shader variants on real graphics.
- `Tools/Agent/Worlds/SunbreakBasin/zone.json`: content version 2 invalidates all
  nine older chunk fingerprints without changing stable identities.
- Current architecture/workflow/mode metadata, source notices, intake ledger and
  `Tools/Art/verify_sunbreak_sources.sh`.

Runtime source paths above are under `Assets/SessionRpg/`. Retained CC0 sources,
original URLs, creators, measured scales, official MD5 and SHA-256 hashes are in
`Assets/ThirdParty/PolyHaven/SunbreakSurfaces/manifest.json` and `PROVENANCE.md`.
All 18 source maps / 41,996,162 bytes pass integrity verification. The active ground
uses 1K Leafy Grass (2 m) and Dirt Floor (2.07 m); rock and bark use 2K scans.
Earlier sparse-grass/gravel trials remain intact for comparison. Android texture
caps are 1K ASTC; imports are streaming-ready, but runtime mip streaming is disabled.

Generated changed:

Sunbreak's nine chunk scenes, referenced deterministic meshes, `SurfaceBlend.asset`,
six scanned-surface/palette materials and associated metadata; the version-2 world
catalog/fingerprints. The required full generator also rewrites Bootstrap,
Combatant prefab, foundation art/catalogs and selected settings. Generated YAML was
not hand-edited. Old, now-unreferenced generated mesh files were preserved.

A pre-rebuild hash audit covered 37 non-Sunbreak materials: 28 remained byte-identical
and nine were rewritten by the full generator/import lifecycle (`ArenaFloor_Trim`,
`AshenCauseway_Cinder`, `DetailAtlas`, `DistantHaze`, `DryGrass`, `Moss`, `Paving`,
`Lava`, `LavaSeam`). Their authoring inputs were not changed in this pass; this is
not a claim of byte-isolated generation. Existing unrelated dirty work was retained.

Commands (fresh):

```sh
Tools/Agent/unity.sh doctor
bash Tools/Art/verify_sunbreak_sources.sh
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh rebuild
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh validate
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh build-macos
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-all
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-play --filter SessionRpg.Tests.PlayMode.SunbreakTerrainShaderPlayModeTests
UNITY_BURST_DISABLE_COMPILATION=1 Tools/Agent/unity.sh test-play
DOTNET_CLI_HOME=build/server-tooling/cli-home DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 build/server-tooling/dotnet/dotnet run --project build/realistic-layout-check/layout-check.csproj --disable-build-servers -p:UseSharedCompilation=false
```

The process-local Burst workaround is retained from the previously diagnosed native
compiler crash. It does not change project package/settings configuration.

Latest artifacts under sibling `../outputs/agent/`:

- `20260905-102847-rebuild-77206/unity.log`: passed.
- `20260905-103000-validate-77804/unity.log`: passed.
- `20260905-103102-build-macos-78266/unity.log`: passed.
- `20260905-103622-test-edit-80421/results.xml`: 771 passed / 0 failed / 0 skipped.
- `20260905-103655-test-play-80421/results.xml`: 224/226 passed; two new shader
  tests failed before native render-pipeline initialization. Superseded below.
- `20260905-104951-test-play-86256/results.xml`: focused native shader gate,
  2 passed / 0 failed / 0 skipped.
- `20260905-105100-test-play-86787/results.xml`: final full PlayMode,
  226 passed / 0 failed / 0 skipped.

Earlier iterations exposed and corrected an intermediate test compile error, two
65K geometry-cap overruns, a bank/rubble normal-contract mismatch, a Null-renderer
pass-lookup assumption and a one-ULP absolute-Blink-endpoint test assumption.
Identical effective displacements are still checked for exact Core sweep equality.
The shader test now submits an explicit 16 × 16 native render request, asserts a
live URP instance, then checks support and all four named passes for both variants.
Its camera/temporary target are cleaned up and `RenderTexture.active` restored;
project/quality assets are not reassigned. This corrects batch-mode test setup
without weakening or skipping the native gate. Only test source changed after the
final build; runtime source, generated content and all eleven captures match.
Those earlier failures are not the final result counts.

Automated result counts:

EditMode: 771/771. PlayMode: 226/226. Combined Unity result: **997 passed / 0 failed /
0 skipped**. Separate .NET diagnostics: 101 passed,
including connected-region sampling, all camp approaches, 104 directional cover
detours, full evade/home returns and allocation checks; these are not added to the
Unity total. The new shader has headless source contracts plus a separate mandatory
native-graphics gate, not a skipped headless GPU test.

| Chunk | Triangles | Renderers |
|---|---:|---:|
| -1,-1 | 50,151 | 23 |
| -1,0 | 55,184 | 29 |
| -1,1 | 37,000 | 11 |
| 0,-1 | 53,180 | 27 |
| 0,0 | 44,570 | 22 |
| 0,1 | 41,738 | 20 |
| 1,-1 | 50,246 | 18 |
| 1,0 | 53,557 | 27 |
| 1,1 | 62,697 | 27 |

Caps remain 65,000 triangles / 80 renderers per chunk. These are geometry safeguards,
not measured frame-time or Android memory acceptance.
Scoped source/document whitespace checks and metadata-presence checks for the new
shader, authoring source and native shader tests also passed.

Manual checks:

Final player frames are under `build/review/pve-realistic-final-20260905/`. They use
actual generated content, the actual default 37° camera, normal HUD and the retained
capture harness's real countdown + 38 ticks before its frozen review frame. No
image editing, render-only props, hidden UI or displaced actors are used.
The preceding `pve-realistic-first-*` and `pve-realistic-second-*` frames are rejected
intermediates; the 37° second-pass angle study was subsequently made the runtime
default. All eleven final frames were visually inspected. Their PNG dimensions
match the table below; each log records exactly one successful capture, pitch 37°,
distance 23 m and FOV 45°. No exceptions, shader errors, C# errors or failed-load
messages were found in these player logs. This is bounded frame inspection, not a
manual gameplay playthrough or a complete aspect-by-heading cross-product.

| Capture key | Camp | Specialization (class) | Resolution | Yaw |
|---|---:|---|---|---:|
| camp0 | 0 | Frost (Mage) | 1920 × 1080 | 90° |
| camp1 | 1 | Discipline (Priest) | 2400 × 1080 | 90° |
| camp2 | 2 | ShadowDance (Rogue) | 2400 × 1080 | 90° |
| camp3 | 3 | Fire (Mage) | 2400 × 1080 | 90° |
| camp4 | 4 | Marksmanship (Hunter) | 2592 × 1080 | 90° |
| camp5 | 5 | Mutilate (Rogue) | 2400 × 1080 | 90° |
| camp6 | 6 | Shadow (Priest) | 2400 × 1080 | 90° |
| camp3-north | 3 | Fire (Mage) | 1920 × 1080 | 0° |
| camp3-south | 3 | Fire (Mage) | 2400 × 1080 | 180° |
| camp3-west | 3 | Fire (Mage) | 2592 × 1080 | 270° |
| refuge | -1 | Frost (Mage) | 1920 × 1080 | 270° |

Only PvE Leveling was manually inspected this turn; other modes were covered by
automated regressions, not fresh manual pixel review. Aspect ratios checked are
16:9, 20:9 and 2.4:1. A second reviewer independently inspected all seven camps and
the north-facing camp 3 frame. Both reviews leave realism/HUD acceptance failed.

For each row, the player command used these arguments (the row values replace the
uppercase placeholders; ROOT is the absolute `game-rpg` path):

```sh
'../outputs/SessionRpg-mac-capture.app/Contents/MacOS/Session RPG Prototype' \
  -sessionRpgCapture 'ROOT/build/review/pve-realistic-final-20260905/KEY/frame.png' \
  -sessionRpgCaptureMode PveLeveling -sessionRpgCaptureCamp CAMP \
  -sessionRpgCaptureSpecialization SPECIALIZATION -sessionRpgCaptureCameraYaw YAW \
  -screen-width WIDTH -screen-height 1080 -screen-fullscreen 0 \
  -logFile 'ROOT/build/review/pve-realistic-final-20260905-KEY.log'
```

The pitch is deliberately absent from these arguments: it is the runtime default.

Performance/device/network checks:

- Core/Application warm allocation checks: passed in fresh EditMode and local diagnostics.
- Native macOS build and actual-player rendering: passed.
- PlayMode warmed HUD/pool allocation and native-shader checks: passed. Both shader
  cases report Metal, a live High-tier URP instance and all four named passes.
- Sustained CPU/GPU frame timing, thermal behavior and memory soak: not run.
- Android build, physical device, touch and device captures: not run.
- Live network, co-op, server integration and cross-process combat: not run.
- Full manual 1–10 grinding balance and uncoached playthrough: not run.

Known caveats:

This is an incremental realism improvement, not photorealism or user art approval.
Small outcrops are better grounded, surface channels and shadows are richer, and
the lower camera exposes additional environmental layers. The region is still
planar in gameplay, broad areas remain sparse and nearly uniform, and repeated
camp-side dressing/soil shapes are apparent. Larger terrain rims still resemble
display bases; the outer terrain has conspicuous straight cuts. Foliage and masonry
retain procedural shapes, and the boss and Hunter companion are especially
toy-like. The top HUD overlaps actors in camps 5/6; right-side controls obscure
enemies in the north-facing camp 3 view. These are recorded failures of the visual
target, not passed art acceptance. Neither source scans nor automated tests
establish mature art acceptance. No quests, loot, procedural world generation or
networked leveling was added.

Next safe action:

Review these actual gameplay pixels with the user, then replace the remaining
prototype foliage/major rock shapes with cohesive production-quality source meshes
and test full camp approach/return play on a physical target device. Keep Core-owned
footprints, nine-meter camp clearance and current rendering budgets while doing so.
