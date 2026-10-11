# Demo Readiness

Status values are `IMPLEMENTED`, `PARTIAL`, `UNTESTED`, and `NOT_IMPLEMENTED`.
Every status applies only to the layer named in its boundary. `IMPLEMENTED` for a
Core/Application contract does not mean that a Unity mode is runnable, and a menu
card does not mean that routing, reset, presentation, or device behavior exists.
This file describes the current demo boundary; it is not a release checklist.

Latest PvE addition: 2026-09-05. The historical evidence below remains tied to its
original revisions. New no-quest PvE Leveling evidence is tracked separately in
[PvE Leveling Evidence](PVE_LEVELING_EVIDENCE.md); its device-local saves are not
connected to the [local server foundation](../Server/README.md).
The road was subsequently rejected; the separate two-dimensional region is tracked
in [Sunbreak Basin evidence](PVE_BASIN_EVIDENCE.md). The earlier
[daylight evidence](PVE_LOCATION_DAYLIGHT_EVIDENCE.md) remains historical and does
not prove the new basin or outstanding production-art/device gates.
The current version-2 environment pass is recorded in
[realism revision evidence](PVE_REALISM_EVIDENCE.md); it is not user art acceptance.
The 2026-09-07 [target-art implementation benchmark](PVE_REALISM_BENCHMARK_EVIDENCE.md)
adds fuller foliage, corrected cliff UVs, masonry/reflection work and one scanned
rock trial. Its 1,036 automated tests pass, but the full high-quality environment,
character and physical-phone performance gates remain open.
The subsequent [Frostbound courtyard revision](PVE_COURTYARD_EVIDENCE.md) adds
connected paving, stronger ruin architecture, a lower local foundation and a
closer 35° / 21.5 m PvE camera. Its exact build/test/capture revision is recorded
separately; the earlier 37° camera descriptions below are historical evidence.
No new quests, progression rules, authoritative obstacles or online behavior are
introduced by this visual pass.

The user rejected the courtyard visuals. The current
[measured-asset vertical slice](PVE_VERTICAL_SLICE_EVIDENCE.md) replaces that
focal composition with selected licensed fort and vegetation assets, a lower
20° landscape camera, daylight sky and independent low-cost distant scenery.
Read its fresh evidence for current verification; previous pass counts do not
validate this revision. Other camps/modes and physical-device art/performance
acceptance remain incomplete.

The subsequent [2026-09-08 ruin composition](PVE_RUIN_COMPOSITION_EVIDENCE.md)
continues that same camp with connected wall sections, a third source tree,
fractured court geometry and a less uniform, less fog-obscured skyline. Its own
fresh evidence governs verification; it is not a production-art acceptance.

The [woodland polish candidate](PVE_WOODLAND_POLISH_EVIDENCE.md) continues toward
production-quality visuals with a denser source-derived canopy, two measured
middle-distance groves, layered foothill materials and bounded outside-Core
woodland. It also adds an opt-in live desktop diagnostic. Its own evidence and
explicit physical-device/art/stability omissions govern acceptance.
The final projection-corrected revision passes 1,004 EditMode and 251 PlayMode
tests and has eight inspected native frames. A stationary 600-frame desktop
sample records zero main-thread allocation but nonzero broader GC allocation,
17.400 ms p95 frame intervals and unresolved shutdown warnings. This is not
production-art, complete allocation-free, phone-performance or stability approval.

## Current capability matrix

| Capability | Status | Honest boundary / required proof |
|---|---|---|
| Local class/spec selection | IMPLEMENTED | Discipline/Shadow Priest, Fire/Frost Mage, Shadow Dance/Mutilate Rogue, and Marksmanship Hunter; verify all seven specs manually after HUD/catalog changes. |
| Product mode taxonomy and routing | IMPLEMENTED | Arena, Training, Hero Siege, Dungeon, and PvE Leveling have distinct home cards and local routing. PvE appends `GameModeId.PveLeveling = 5`, `PlayableModeId.PveLeveling = 4`, and `RunKind.PersistentRegion = 5`. Legacy Leveling still migrates to Training; Boss migrates to a Dungeon boss encounter. Existing ordinals are unchanged. |
| Local 2v2 arena | IMPLEMENTED | One human plus three bots, explicit targeting, health/resource/GCD/cast/control/knockback/lava loop. Usability gates remain untested with fresh players. |
| Legacy local Boss encounter | IMPLEMENTED | Three local participants with bots and a tankless boss loop. It is compatibility/content evidence, not the planned Dungeon runtime, a raid system, or networked cooperative play. |
| Local Training route (legacy Leveling runtime) | IMPLEMENTED | Ashen Causeway provides one complete local 448 m route through seven named districts. Its deterministic five-pack/ten-enemy run awards five levels from 1 to 6, gates packs by eastward progress, owns collision/XP in Core, streams seven presentation chunks, and drives eight stable hostile archetypes. It uses three starting actions and one unlock after each of the first four packs. It still has no quests, loot, respawns, persistent power, accounts, or backend; fresh uncoached/device proof remains open. |
| Local PvE Leveling grinding prototype | PARTIAL | Separate mode: seven repeatable spatial camps, eight existing archetypes, levels 1–10, device-local per-specialization XP, refuge recovery and named/leveled targets. Sunbreak Basin replaces PvE's road with a 180 × 180 m playable rectangle, scattered camps, thirteen Core-owned compound landmarks (39 overlapping circle components), PvE enemy detours and a southwest sanctuary. Training remains on its old route. The 2026-09-05 version-2 realism revision passes fresh suites (771 EditMode / 226 PlayMode), rebuild, validation and a macOS build; see [realism evidence](PVE_REALISM_EVIDENCE.md). Eleven actual-player frames cover all seven specs, three aspect ratios, all camps and refuge. Scanned surfaces, grounded small ruins and a lower 37° camera improve the prototype, but realism/HUD acceptance still fails: broad flat ground, repeated sparse dressing, display-base-like major relief, hard boundaries, toy-like boss/companion and actor/HUD overlap remain. Device and balance acceptance are open. No quests, equipment, ability unlock progression, procedural terrain or online gameplay. |
| Guided Training runtime | IMPLEMENTED | Nine ordered steps for all seven specializations—movement, targeting, basic combat, hazard recovery, ability progression, recovery/defense, control/interrupt, final pack, and flick mastery—are wired to semantic Unity observations, active-step HUD, recommended/skip/reset flow, full-kit flick bridge, per-spec local profile, and structured records. The first pack remains passive until basic combat begins. Reset restarts coaching and route progression; a no-lava fallback avoids hazard-step softlock. Human comprehension is still untested. |
| Hero Siege rules/content definition | IMPLEMENTED | Core/Application source defines a persistent 1000-HP fortress, 12 scheduled waves that may overlap, selectable Solo Lane and Four Lanes variants, a global eight-active-hostile cap, nine regular non-boss archetypes, stable variant/lane/wave/spawn/participant IDs, objective pressure, session-local gold, and purchasable/consumable HP and mana potions. Solo Lane has one human and no bots; Four Lanes has one human plus three bots. Siege has no miniboss, boss, boss tier, final-boss exception, or encounter reset. Focused tests exist in source, but the recorded suites below predate this redesign. |
| Local Hero Siege gameplay | PARTIAL | Unity source integrates one persistent `CombatSimulation`, the generated Ashen Stronghold central-fortress/four-lane presentation, variant selection, up to four defenders plus eight hostiles in a twelve-view pool, lane-aware defender/enemy bots, wave/objective/gold/potion HUD, both failure paths, victory, result, replay, and leave. Waves neither reconstruct the simulation nor revive the party. Every specialization receives a direct Siege-only area action in place of Arena Trinket: Dawn Nova, Umbral Collapse, Flame Surge, Blizzard, Blade Tempest, or Arrow Rain. A fresh same-revision rebuild, validation, full EditMode/PlayMode run, uncoached full runs, soak, visual review, and device evidence have not yet been recorded here, so the redesigned mode is not yet accepted as runnable. |
| Dungeon rules/content definition | IMPLEMENTED | Core/Application define and test four heroes across four floors: traversal (`3` medium spawns), elite Arena (`4`), mechanics/miniboss (`4`), and Rift Warden boss (`4`), with encounter-owned rules/spawn positions, same-floor retry, advance, abandon, and results. |
| Local Dungeon gameplay | IMPLEMENTED | Unity runs one player plus three bots, composes Ashen Causeway, two Rift Crucible-derived floors, and an Ember Vault boss floor, resets Core simulation at explicit floor/retry boundaries, and handles HUD, result, replay, and leave. It is a functional composite prototype, not production multi-floor environment/enemy content; full manual run, soak, and device evidence remain open. |
| Stable world manifest and catalog | IMPLEMENTED | Stable paired world/zone IDs, normalized 64 m coordinates, strict manifests, fail-closed biome/POI pairing, source fingerprints and build-scene parity exist. Explicit recipes support Ashen Causeway (1/1) and Sunbreak Basin (1/2); unknown or mismatched recipes fail closed. Zones are safety-capped at 1024 chunks and need spatial indexing before approaching that ceiling. |
| Additive visual world streaming | IMPLEMENTED | Training/Dungeon floor one use seven Ashen Causeway chunks; PvE Leveling uses nine Sunbreak Basin chunks in a 3 × 3 footprint. Both load radius1/retain radius2 around Core player positions through deterministic planning and serialized async operations. Scene physics/global systems are forbidden. Automation covers both-axis basin windows, bounded residency, zone switches and draining; manual seam traversal and physical-device stall checks remain required. |
| General authoritative world topology | PARTIAL | Core owns the Training rectangle and a separate flat two-dimensional PvE rectangle with thirteen irregular collision/LOS landmarks, each a union of three unequal overlapping circles (39 components). Movement, placement, displacement and NPC detours use those shared facts. No general terrain graph, heightfield authority, streamed navigation, procedural walkable topology or cross-zone travel exists. |
| World actor streaming and AOI | PARTIAL | PvE Leveling spatially activates one camp/up to three hostiles, retires defeated or abandoned enemies, and reuses pooled views with fresh participant IDs. Training retains its finite lifetime packs. Chunks do not own server interest or replication; this is local actor lifetime management, not network AOI. |
| Persistent MMO world | NOT_IMPLEMENTED | A separate local .NET development control plane saves account/character/region metadata and co-op admission, but has no Unity combat transport. There is no live world server, shard simulation, network AOI, remote content delivery, or floating origin. Local XP saves and additive scenes are not an MMO implementation. |
| Full-kit `2 + 4 + 4` HUD | PARTIAL | Arena, PvE Leveling, Hero Siege, Dungeon, and legacy Boss use two 301.6-pixel gesture discs (the original complete family at 1.3× scale), four unchanged 132-pixel common actions and four unchanged 92-pixel bottom-center utilities at the 1080p reference height. Their displayed art is 62.4, 82, and 56 pixels respectively. Each large circular control contains a 109.2-pixel center primary and four 78-pixel directional regions at 101.4-pixel offsets, supports tap plus four flicks, and exposes 18 actions through ten visible controls. Captions and real circular hit areas scale with the art; screen-pixel flick thresholds do not increase. The shared control scales identically in Training while its unlearned directions remain absent. Hold reveals labels/highlights without resizing or rearranging the disc. Hero Siege retains its direct area-action binding. The 2026-09-07 resize evidence is tracked in [large-control evidence](HUD_LARGE_CONTROLS_EVIDENCE.md); physical-device reachability/touch comfort and unrelated actor/awareness-overlay occlusion remain open. |
| Training skill unlock profile | IMPLEMENTED | Every specialization enters Training with three curated actions, then gains one after packs one, two, three, and four at levels 2, 3, 4, and 5. The learned-only seven-action subset draws exactly those available actions and reveals each unlock in its stable authored slot; it does not reserve future positions with locked circles, empty placeholders, or skill previews. Core rejects early/omitted actions with `level_locked`. Training has no alternates until the final flick lesson temporarily binds the full Arena kit. |
| Wrath 3.3.5 arena utility slice | PARTIAL | The normalized kits retain essential Priest healing/anti-fear/dispels, Mage defense/armors/Spellsteal, Rogue openers/control/mobility, and Hunter Sting/purge/trap/kiting decisions. Values are compressed. Prayer of Mending does not bounce; Water Elemental Freeze has no persistent Mage pet; Ice Block has no early cancel; Inner Focus and armor statistics are prototype abstractions; and Vampiric Embrace remains a passive simplification. Stable definitions for excluded trial actions remain in source, but Core does not authorize them for humans or bots through a full-kit mode unless a separate Training kit explicitly includes them. |
| Hunter companion and trap foundation | PARTIAL | Deterministic local companion orders, pet-origin abilities, pet health, and trap state are implemented. Pet Attack is a deferred companion order rather than an immediate miss, remains available while the Hunter is disarmed, and presents resolved damage/lunge from the pet. Focused automated regressions pass; exact physical-device readability and arena behavior still require manual proof. |
| Click/tap hero targeting and compact roster | IMPLEMENTED | One explicit cross-team focus; stealth intentionally hides enemy position/live state. |
| Camera orbit | IMPLEMENTED | Empty-world horizontal drag emits pointer-owned presentation-only yaw and cannot retarget on release; Q/E and gamepad right stick are desktop equivalents. Arena's target-oblique composition uses yaw `90`, pitch `40`, distance `18 m`, forward focus `2.3 m`, and FOV `46`, and clamps to `30..150` degrees (`90° ± 60°`). Boss/Ember Vault keeps authored default yaw `0` while its HUD-aware arc is centered at `15` degrees and clamps across the wrap to endpoints `325` and `65` (`15° ± 50°`). Siege source adds a closer lane-following Solo composition (`90`/`58`/`19 m`/FOV `46`, forward focus `2.4 m`) and a closer fortress-centered Four Lanes composition (`45`/`65`/`24 m`/FOV `50`, right focus `3 m`), each with full yaw orbit. Focused source tests cover default objective, defender, and first-pressure framing at `16:9`, `20:9`, and `2.4:1`, including the Four Lanes right-HUD corridor, but a fresh PlayMode run, physical multi-touch comfort, and complete rotated-heading framing review remain untested. |
| Deterministic rules, bots, allocation tests | IMPLEMENTED | Automated coverage exists; every change still requires a fresh run. |
| Authored animation/VFX/audio foundation | PARTIAL | Distinct states and pooled effects include six school-specific release/impact sound families plus physical/generic fallbacks. Siege source adds class-readable nova, target-area, knife-storm, blizzard, and arrow-volley routes for the six mode-only area actions; Blade Tempest retains its Rogue whirlwind body/weapon choreography without rotating the gameplay root. Fresh tests and broad manual spell review remain open, and professional recorded/layered audio and production clips remain below the `VISUAL_QUALITY.md` bar. |
| Hunter character visual base | PARTIAL | The selected Erika Mixamo model is a hash-locked licensed game base admitted to embedded Development and non-Development builds. Adobe permits royalty-free commercial-game use and modified Content Files embedded/distributed only with the game; raw original/modified FBX redistribution is prohibited. Raw animation and embedded weapon skinning/props were rejected on technical/art grounds, so project motion and rigid weapon props remain authoritative. Customization, deformation, LOD, and mobile review remain. |
| Priest/Rogue/Mage character visual bases | PARTIAL | Uriel A Plotexia, Vampire A Lusth, and Demon T Wiezzorek are reversible, licensed, model-only bases for playable classes and enemy readability. `promotionAllowed:true`; exact source/import/provenance validation applies to every build type. Model animation import is disabled; project controllers remain authoritative. Clothing/face/hair/material differentiation, deformation, LOD/PBR, and physical mobile performance remain incomplete. |
| Host-authority/private-session foundation | PARTIAL | NGO/direct-IP plus pure 30 Hz host authority cover a four-slot Arena roster, one-to-four-human-plus-bot admission, epochs, sender/actor validation, bounded queues/snapshots/events, disconnect/abort, and replay. An injected anonymous-auth/MPS Relay coordinator covers four-player private create/join-code/leave/termination lifecycle. No live UGS/Relay, combat RPC/custom-message adapter into `CombatSimulation`, remote snapshot application, prediction/reconciliation, multi-process, or device evidence exists. Do not demo or claim online PvP/PvE. |
| Accounts, matchmaking, persistent progression, loot | PARTIAL | PvE Leveling saves level 1–10 XP per specialization on this device; Training and Siege progression remain session-local. The separate local server has development accounts/world metadata, not connected gameplay progression. Matchmaking, inventory/economy, equipment loot and cloud saves remain absent. |
| Device-local profile/settings | IMPLEMENTED | The generated Bootstrap wires schema-v1 JSON defaults/normalization, per-spec Training completion/skip/reset, mastery/cosmetic identifiers, pause/settings, and restart persistence. Master/effects volume, reduced-motion camera behavior, high-contrast HUD, and a settings-gated supported-mobile result vibration are applied. This is local metadata, not account progression or cloud save. |
| PII-free structured session records | IMPLEMENTED | Normal sessions emit local JSONL start, Training step, unlock, and finish/outcome records without account/device/location/network identifiers; the sink rotates at 2 MiB. There is no combat timeline, bot seed, performance telemetry, analytics upload, or external privacy audit. |
| Android development build path | IMPLEMENTED | The 2026-09-02 High-environment revision produced `../outputs/SessionRpg-dev.apk` (231,615,506 bytes; SHA-256 `344877622897f5f23096315855f381ebc215b675ad4c379b2fb43147ebaac431`) with every then-enabled manifest-generated scene. That APK predates Ashen Stronghold and the continuous 12-wave Siege redesign; it is not current Siege evidence or an accepted distribution-size/device-performance result. |
| Mobile render profiles | IMPLEMENTED | Performance/Balanced/High remain distinct authored URP contracts, with High now applied once at startup as the strongest-smartphone visual target. High uses native render scale, 4x MSAA, HDR, a 4096 soft-shadowed main-light map at `40 m` with two cascades split at `0.55`, and per-pixel additional lights with a four-per-object cap. Balanced remains the 0.85-scale, 2x MSAA, HDR, 2048/`32 m`/two-cascade (`0.60`) fallback with two per-pixel additional lights; Performance remains the 0.78-scale, 1x MSAA, LDR, 1024/`24 m`/single-hard-cascade fallback with one per-vertex additional light. Additional lights never cast shadows. ACES, restrained bloom/grading, and the custom reflection response are authored; SSAO and screen-space reflection remain absent. Physical-device performance and thermal measurements remain open. |
| Arena/Boss/Siege presentation catalog | PARTIAL | Manifest-driven `rift_crucible` and `ember_vault` prototype prefabs retain stable IDs and prior capture evidence. Source now adds `ashen_stronghold`, a prototype persistent central fortress with four cardinal lanes and its own capture anchors. The recorded Arena/Ember Vault pixels predate that prefab and cannot prove its layout, obstruction safety, or density. None of these presentations changes Core geometry or gameplay authority. Ashen Stronghold still needs fresh rebuild/validation, full aspect/yaw captures, explicit user art approval, physical Android review, and device profiling. |
| Runtime arena content loading | PARTIAL | A stable-ID loader selects cataloged defaults for Arena, legacy Boss, and Hero Siege, can own cataloged prefab/scene lifetime, and keeps one presentation resident. The controller loads only the default per content mode; there is no player-facing Arena map selection, voting, or rotation. The Siege catalog/loading path is present in source but lacks fresh same-revision acceptance evidence. |
| External-release art provenance | PARTIAL | CC0 sources and selected Mixamo bases retain notices and exact hashes. Adobe permits commercial-game embedding and modification of Content Files but prohibits standalone/raw original or modified FBX redistribution. The user promoted Mixamo as the customization direction on 2026-09-01; non-Development embedded builds are admitted. Poly Haven Stone Brick Wall 001 is separately recorded as CC0 and remains a `TECH_REVIEW` runtime candidate with a reproducible packed-mask derivative and current macOS-player capture evidence. Neither candidate is final production acceptance: complete gameplay-camera/user art acceptance, customization/deformation where applicable, physical mobile, performance, and complete source-package evidence remain open in `Docs/Art/PRODUCTION_ASSET_INTAKE_LEDGER.md`. |
| Current Android performance/soak | UNTESTED | Must be profiled on physical hardware after the current visual pass. |
| iOS build and device behavior | UNTESTED | No iOS build/support claim in this milestone. |
| Fresh-player validation gates | UNTESTED | No objective/readability/five-round gate is considered passed without observed sessions. |

## Evidence state

The 2026-09-07 [first three-mode realism benchmark](PVE_REALISM_BENCHMARK_EVIDENCE.md)
starts with Sunbreak's near foliage, material separation and daylight response;
it does not implement the Arena/Dungeon artwork. The [larger-control revision](HUD_LARGE_CONTROLS_EVIDENCE.md)
is separately recorded. Their fresh results and explicit visual failures supersede
the historical counts below only for the exact revisions recorded in those files.
The current images still do not meet the requested high-quality 3D art bar.

Historical result artifacts were produced at different revisions and contain
different suite sizes. They demonstrate that suites existed at those revisions only;
they are not current evidence after any source or generated-asset change. Run:

```sh
Tools/Agent/unity.sh validate
Tools/Agent/unity.sh test-all
```

Record the new XML paths and counts in the handoff. If Unity is open, saved work may
be tested with `--isolated`; generated authoring must be rebuilt only after closing
the real project.

The 2026-09-02 High-tier environment candidate has current macOS-player pixels at:

- `build/review/arena-high-v19-yaw90-2400x1080.png`;
- `build/review/arena-high-final-qhd-yaw90-3200x1440.png`;
- `build/review/arena-high-ultrawide-yaw30-2592x1080.png`; and
- `build/review/arena-high-ultrawide-yaw150-2592x1080.png`.

These frames cover the authored Arena default at two 20:9 resolutions and both
bounded yaw endpoints at 2.4:1. They are desktop-rendered strongest-phone visual
proxies, not Android screenshots or evidence of device FPS, GPU cost, thermals,
touch comfort, the missing 16:9 case, a complete cross-product of yaw and aspect
ratio, or user acceptance.

The integrated 2026-09-02 High-environment revision had fresh same-revision
automated/build evidence for that earlier revision:

- `doctor`: passed with the Editor closed.
- Generated rebuild: passed; log
  `build/evidence/20260902-high/rebuild.log`.
- Validation: passed; log
  `build/evidence/20260902-high/validate.log`.
- EditMode: `501/501` passed; XML
  `build/evidence/20260902-high/editmode-results.xml`.
- PlayMode: `163/163` passed; XML
  `build/evidence/20260902-high/playmode-results.xml`.
- macOS capture build and 3200x1440 capture: passed; logs
  `build/evidence/20260902-high/build-macos.log` and
  `build/evidence/20260902-high/capture-qhd.log`.
- Android Development APK: passed; `../outputs/SessionRpg-dev.apk`, 231,615,506
  bytes, SHA-256
  `344877622897f5f23096315855f381ebc215b675ad4c379b2fb43147ebaac431`;
  log `build/evidence/20260902-high/build-android.log`.

This evidence predates the current Ashen Stronghold, 12-wave continuous Siege,
variant, archetype, gold/potion, and twelve-view-pool changes. It must not be cited
as proof of the redesigned Hero Siege source or generated composition. Fresh
same-revision rebuild, validation, full EditMode, and full PlayMode results have not
yet been recorded in this document. The older evidence also does not prove
physical-device install/input/performance, ten-session soak, uncoached usability,
live UGS/Relay, multi-process combat networking, or production art acceptance.

Earlier on 2026-09-01, a targeted Application EditMode run passed `175/175`. It included
the new Training, profile, and session-record fixtures (`23/23` across those three
fixtures) as well as the existing Application coverage. This proves the Unity-free
contracts at that revision only. It is not a fresh full EditMode/PlayMode pass, a
generated Bootstrap validation, or runtime evidence for Training, Hero Siege,
Dungeon, persistence, logging, or networking; the later full results above supersede
it as the current automated baseline.

At the 2026-08-28 documentation review, targeted PlayMode evidence for the prior,
superseded medallion HUD reported `19/19` passing tests:

- `FourClassHudPlayModeTests`: `7/7`, including all seven specializations rebinding
  the then-current medallion instances, complete playable icon/glyph coverage, and
  Hunter cardinal-family behavior.
- `AbilitySlotAlternatePlayModeTests`: `11/11`, covering layered medallion states,
  caption/readiness identity, primary and alternate interaction, and warmed-up
  allocation stability.
- the then-current Bootstrap HUD hierarchy test: `1/1`, covering its larger action
  cluster, jewel wells, shared frame
  ornaments, uncropped true-alpha art, and caption plaques.

Those focused tests prove only the prior hierarchy and runtime rebinding contracts
at their recorded revision; they do not accept the current ten-control layout. They
were supplemented there by EditMode `281/281` and
PlayMode `138/138`. After the additive-world change, complete suites passed on
2026-08-29 at EditMode `333/333` and PlayMode `140/140`. The current content-version-3
revision passed fresh complete suites on 2026-08-30: EditMode `344/344` and PlayMode
`141/141`. The current button/enemy revision passed fresh complete suites on
2026-08-31: EditMode `362/362` and PlayMode `141/141` (`503/503` combined).
The 2026-08-31 medallion
desktop-player captures are stored in
`build/review/hud-medallion-*.png`; Hunter passes 16:9, 20:9, 2.2:1, and 2.4:1
expanded-palette review, while Discipline, Fire, and Shadow Dance pass 2.2:1 resting
review. These captures prove final pixels and safe-area composition only. They do
not prove physical target size, reachability, device performance, or touch comfort.

The pre-streaming Ashen Causeway environment pass has desktop-player review frames
at 16:9 in `build/review/leveling-ashen-causeway-environment-final-1920x1080.png`
and at 20:9 in
`build/review/leveling-ashen-causeway-environment-final-mid-2400x1080.png`. They
record the accepted organic flagstone language, darker lava/chasm separation,
fortress silhouettes, XP strip, enemy silhouettes, and the larger prior controls at
that
revision. At the additive-stream foundation revision, a 16:9 desktop-player frame
of the west refuge was stored at
`build/review/leveling-streamed-world-west-refuge-1920x1080.png`. It predates
the current content version `3`, the all-seven-landmark proof, and the seven-action
learned-only HUD.
Historical frames cannot prove every seam/POI, later-pack final pixels,
physical-device stalls, sustained mobile GPU cost, or production
environment/creature-art acceptance.

The 2026-08-31 content-version-3 West Refuge frame is
`build/review/leveling-ashen-causeway-v3-west-refuge-1920x1080.png`. It proves the
forward refuge walls/watchtower/beacon composition, short hostile roster labels,
then-current five-action/two-preview layout at 16:9. That layout predates the
current `3 -> 7` unlock contract and cannot validate its reveal-on-unlock layout. It
also does not prove later-district final pixels, the full route on a physical
device, mobile performance, or production-art quality.

The 2026-08-31 button/enemy revision frame is
`build/review/leveling-buttons-enemies-final-1920x1080.png`. It proves the shared
authored frame sprite, transparent icon openings, caption and lock-state hierarchy,
West Refuge roster cards, and Raider/Marksman first-pack readability at 16:9. The
all-eight identity mapping and deterministic primary/signature/fallback behavior are
covered by automation; this single initial-pack frame does not prove later-district
final pixels, the current `3 -> 7` Training layout, touch comfort, physical-device
performance, or production creature art.

The prior icon-art revision has 16:9 desktop-player proofs for Hunter at
`build/review/leveling-icons-v2-hunter-1920x1080.png` and Shadow at
`build/review/leveling-icons-v2-shadow-1920x1080.png`, plus a 2.4:1 Hunter proof at
`build/review/leveling-icons-v2-hunter-2_4x1.png`. They prove that the two
previously glyph-only leveling families use true-alpha painted motifs inside the
shared authored medallions, including the former level-three/level-five previews. An
EditMode catalog contract covers every slot in all seven leveling specializations.
That capture set predates all current Siege-only area icons. The source now expects
a `108/108` painted player-bound union: the prior 102 plus Blade Tempest, Dawn Nova,
Umbral Collapse, Flame Surge, Blizzard, and Arrow Rain. The recorded EditMode suite
predates the last five additions, so transparent-art/catalog acceptance and fresh
gameplay pixels remain open. Representative prior 16:9
desktop-player proofs live at
`build/review/ability-icons-v2/arena-marksmanship-swipe-1920x1080.png` and
`build/review/ability-icons-v2/pve-marksmanship-1920x1080.png`; together they show
the Hunter primary/cardinal palette and the former five-start/two-lock PvE row in
the same painted language. Fresh `3 -> 7` all-spec, expanded-palette, tall-phone,
and physical-device readability review remains required.

## Demo gate

A build is ready for an internal uncoached combat demo only when all rows below have
fresh evidence. A failed row blocks the demo; it does not justify adding content.

| Gate | Automated proof | Manual proof |
|---|---|---|
| Foundation is coherent | `validate`, full EditMode, full PlayMode pass | Bootstrap opens without warnings/exceptions. |
| Mode surface is truthful | routing/migration/authoring tests assert exact stable mappings | Start every visible card and confirm Arena, Training, Hero Siege, and Dungeon reach distinct named local objectives; legacy Boss/Leveling never appear as extra product modes. |
| Seven specs are operable | catalog/readiness/input tests | Start Arena with every spec; every visible primary and alternate has a comprehensible ready/GCD/cooldown/blocker state. |
| Training teaches the Arena entry skill | all seven guided sequences, `3 -> 7` unlocks, skip/reset, profile normalization tests | With no coaching, a new player completes or deliberately skips Training, sees only learned actions, understands each of four reveals without locked circles or previews, recovers from the hazard, clears pack five, and resolves one intended flick. |
| Siege continuous-defense loop reads | 12-wave schedule/overlap, variant/lane, nine-archetype/no-boss, objective-pressure, gold/potion, Blade-Tempest, bot, persistent-simulation, and twelve-view-pool tests | Complete Solo Lane and Four Lanes victories plus defender and fortress defeats; verify one human with zero or three bots, recognize overlapping pressure without a reset, distinguish regular archetypes, spend gold on an appropriate HP/mana potion, deliberately use area damage/control/utility, and never observe more than eight active hostiles globally or two per lane in Four Lanes. |
| Dungeon floor loop reads | four-floor/rules/spawn/retry/advance/pool tests | Complete traversal, elite Arena, mechanics/miniboss, boss, same-floor retry, replay, and leave without stale actors, camera, target, world chunk, or result state. |
| Targeting and camera framing are trustworthy | PlayMode targeting/stealth/drag/bound tests | Select ally/enemy by roster and world; an empty-world drag orbits the presentation camera without retargeting or changing Core state; stealth leaks no position/live HP/cast. Capture the stable Arena and Ember Vault default/bounded states, then capture Ashen Stronghold in both Solo and Four Lanes at their authored defaults and representative cardinal headings; keep the human, assigned lane or full fortress, active threats, feet, objective, and approach routes readable. |
| Outcomes are readable | deterministic combat tests | Observe cast start, resolution, miss, interrupt, absorb, dispel, control, knockback, lava, death, and recap. |
| Arena geometry agrees | collision/LOS/teleport tests | Walk and knock around both wardstones; check LOS, Blink, Shadowstep, bot routes, edge and lava. |
| Streamed route is coherent | manifest/planner/streaming tests | Traverse Ashen Causeway west to east and back; inspect every seam and POI, bounded residency, clean unload/re-entry, and absence of visual stalls on the target device. |
| Local profile/records are actually composed | generated-scene reference checks plus profile corruption/record validation | Change/restart settings, complete/skip/reset Training per spec, then inspect bounded local files and verify no PII or duplicate session boundaries. |
| Replay is clean | flow and twelve-view pool tests | Complete/replay ten Arena/Training sessions and repeated full Hero Siege/Dungeon runs with no lost input, duplicated objects/audio, recurring GC spikes, or exceptions. |
| Mobile frame is usable | none substitutes for device profiling | Fresh final-pixel and safe-area review of the current medallions at 16:9, 20:9, and 2.4:1, including Arena and Ember Vault default/bounded-endpoint framing plus both Ashen Stronghold variants at representative headings, followed by a physical-device check that buttons and Siege supplies are reachable and readable without covering combat. |

## Performance reference class

The current visual-review build defaults to High so its captured image represents
the intended ceiling; this pass does not change startup quality or add automatic
tier selection. The user's latest 2026-09-08 direction is to pursue the best image
and **validate the ordinary iQOO Neo11 at High / best quality first**. That is the
primary visual/device acceptance target, not a passing device result. Do not
silently downgrade the test to Balanced/Performance. Record the actual panel and
render resolution, High settings, sustained frame timing, temperature and touch
readability; any required quality compromise must be reported explicitly.

The earlier minimum **one performance tier below iQOO Neo 11** remains a later
compatibility goal, not the next primary quality test. Working interpretation:
**Snapdragon 8 Gen 3 / Adreno 750**
class, with the China-market iQOO Neo10 as a concrete candidate test reference.
This is an engineering interpretation, not an official numeric device ranking or
a claim that all phones with that chip have equivalent sustained performance.
The exact physical SKU/RAM/OS and thermal behavior must be recorded when tested.

Official specifications checked 2026-09-08 identify the ordinary Neo11 as
Snapdragon 8 Elite / Adreno 830 and the China Neo10 as Snapdragon 8 Gen 3 /
Adreno 750. [Neo11 official specifications](https://www.vivo.com.cn/vivo/param/iqooneo11),
[China Neo10 official specifications](https://www.vivo.com.cn/vivo/param/iqooneo10).
Do not substitute a different regional Neo10 by name alone. The existing 60 FPS /
1080p-class planning target and explicit Balanced/Performance fallbacks remain;
native panel resolution, render scale and chosen tier must be reported rather
than silently assuming full-resolution High can pass. No phone has been measured
by this revision. The earlier generic mid-range and iQOO 12 examples are historical;
the iPhone 17-class reference remains untested and is not an iOS support claim.

Planning budgets, pending measurement:

| Metric | Demo budget |
|---|---:|
| Warm gameplay frame p95 | `<= 16.7 ms` |
| Main-thread CPU p95 | `<= 8.0 ms` |
| GPU p95 | `<= 12.5 ms` |
| Managed allocation in simulation tick | `0 B` after warm-up |
| Managed allocation in steady gameplay frame | `0 B` after warm-up |
| Memory growth, session 1 to session 10 | `< 5 MiB` |
| Rendered triangles in normal 2v2 view | `<= 200k` |
| Draw calls in normal 2v2 view | `<= 140` |
| SetPass calls in normal 2v2 view | `<= 100` |
| Simultaneous pooled spell views | `<= 24` (current authored pool) |

These numbers are budgets, not observed results. Report profiler source, build type,
resolution, thermal state, and a reproducible combat scenario with every result.

## Not a demo pass

- A green compile with no test XML.
- An old APK, screenshot, log, or XML filename.
- Editor FPS used as Android performance evidence.
- One class, the default camera alone, or one quiet camera angle used as
  visual/performance proof.
- A menu card or a deterministic run-controller test used as proof that Hero Siege
  or Dungeon is playable.
- A profile/log adapter class used as proof that the tracked scene persists or
  emits anything.
- Host/client buttons or pure protocol tests used as proof that combat networking exists.
- Coaching a tester, then marking the new-player objective gate passed.
