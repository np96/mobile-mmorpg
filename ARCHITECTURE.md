# Technical Architecture

## Goals

The architecture supports an Arena-first local product with a recommended,
skippable Training route and bounded Hero Siege and Dungeon extensions. The current
source composition contains one local 2v2 Arena, one legacy three-participant Boss
prototype, the guided five-pack level 1–6 Ashen Causeway route, a continuous
12-wave Hero Siege with one-lane and four-lane variants, and a four-hero four-floor
Dungeon across four classes and seven specializations. The recorded runnable/build
evidence predates the current Siege redesign until a fresh same-revision rebuild,
validation, and full suites are recorded. The additive-world layer scales local
presentation content; it is not a networked or persistent MMO world.

The approved 2026-09-04 expansion adds a separate no-quest PvE Leveling mode:
repeatable camps, independent levels 1–10, device-local per-specialization XP,
and refuge recovery. `Docs/PVE_LEVELING_MODE.md` defines this local slice;
`Docs/PVE_LEVELING_EVIDENCE.md` records its verification. It does not yet use the
separate .NET server foundation or provide online co-op.

Unity `6000.5.6f1`, C#, Universal Render Pipeline, Input System, and Unity Netcode
for GameObjects (NGO) are the pinned foundation. The project must remain
independently openable; it cannot depend on Ringfall's `../game/` project. A tested
host-authority/protocol foundation and an anonymous private four-player MPS/Relay
lifecycle exist around NGO, but there is no combat RPC/custom-message or snapshot
delivery path. All runnable gameplay remains one local deterministic simulation
with human and bot command sources.

## Assembly boundaries

| Assembly | Responsibility | Unity references |
|---|---|---|
| `SessionRpg.Core` | Immutable combat, run identity, objective state, and world-coordinate contracts; deterministic rules, validation, cooldowns, health, status, targeting, displacement requests, and encounter state. | None |
| `SessionRpg.Application` | Session, continuous-Siege, and sequential-Dungeon flow; Training steps; local-profile/session-record contracts; mode rules; bot command orchestration; deterministic chunk-residency planning; result/death recap construction; and presentation-facing view state. | None |
| `SessionRpg.Networking` | NGO/direct-IP lifecycle, private MPS/Relay session orchestration, host roster/command/snapshot/event contracts, and future combat transport/reconciliation adapters. | NGO and Unity Services only at the boundary |
| `SessionRpg.Unity` | Scene composition, additive visual-chunk lifetime, character controller, fixed strategy camera, mobile input, HUD, animation, audio, VFX, and world queries. | Yes |
| `SessionRpg.Editor` | Manifest validation, generated world/arena content, asset/scene validation, authoring helpers, tests, and build pipelines. | Editor only |

Dependencies point inward: Unity and Networking call Application/Core contracts; Core does not know about GameObjects, MonoBehaviours, NGO, input devices, animation, or rendering.

## Authority model

The following remains the target networking authority model. The repository now
has a 30 Hz host clock, fixed four-slot Arena roster, session epoch and ordered
command envelopes, per-participant bounded command queues, ownership/sequence/tick
validation, bounded snapshot/event envelopes, and deterministic disconnect/replay
policies. Focused EditMode tests exercise those pure contracts. They are not wired
to `CombatSimulation`, NGO RPC/custom messaging, remote snapshot application, or
owner reconciliation, so there is still no host/client combat path or remote-session
acceptance evidence.

Separately, `PrivateArenaRelayCoordinator` and its Unity Services adapter implement
anonymous sign-in plus create/join-code/leave/abort lifecycle for an MPS-owned
four-player Relay session. Tests use an injected service seam; no live UGS/Relay,
multi-process, or device session has been accepted. Session establishment is not
combat replication.

The server is the sole authority for:

- Spawn, team, class, alive state, and session phase.
- Legal targets, range, line of sight, cooldowns, cast state, and resource costs.
- Health, healing, status effects, interrupts, knockback, lava damage, and death.
- Boss and Dungeon floor/telegraph/phase/retry state, plus Hero Siege timed-wave,
  lane, fortress-objective, gold, consumable, and run-result state.
- Arena result and replay state.
- Training XP credit, level bonuses, pack activation, unlocks, and route result.

Clients send intent: movement input, facing, explicit target choice, ability request, and replay/leave choice. They never send final damage, healing, position after knockback, cooldown completion, or match results.

When combat transport integration is implemented, the listen-server must submit local
intent through the same authenticated authority queue as a remote client. Host
shortcuts that bypass validation are prohibited. A future dedicated server may
replace hosting without replacing combat rules.

## Simulation and synchronization

- Run combat on a fixed server tick independent of rendered frame rate.
- Assign every accepted command a participant ID, session epoch, monotonically
  increasing sequence number, requested tick, and stable `AbilityId`.
- Reject duplicate, stale, impossible, out-of-phase, or unauthorized commands deterministically.
- Resolve simultaneous actions in a documented stable order, with tests for simultaneous damage, healing, interrupts, displacement, and deaths.
- Use kinematic gameplay movement. Unity physics queries may provide collision, overlap, ground, and line-of-sight facts, but Rigidbody momentum does not decide combat outcomes.
- Represent knockback as an authoritative displacement operation with authored distance/duration and collision rules. Replicate its start tick and resolved path so all clients present the same event.
- Replicate compact authoritative snapshots and reliable combat events. Owners may predict locomotion and cosmetic cast startup; correction cannot alter an already accepted ability's aim invisibly.
- Treat lava volumes and safe recovery surfaces as server-authored world data. Damage uses server overlap/position, not a client trigger report.

Network identities and simulation identities remain separate. A bot, local human, remote human, or automated test supplies the same `CombatCommand` contract.

## Core contracts

Stable identifiers replace presentation indices:

- `ParticipantId`, `TeamId`, `ClassId`, `SpecializationId`, `AbilityId`,
  `AuraId`, `StatusId`, `PlayableModeId`, `RunId`, `EncounterId`, `SpawnId`,
  `ObjectiveId`, `RunKind`, `EncounterKind`, and `CommandSequence`.
- `WorldId`, `ZoneId`, `WorldChunkCoordinate`, and `WorldChunkKey` identify world
  space without Unity object names. `WorldPosition` stores a chunk key plus
  normalized 64-meter chunk-local planar coordinates, including correct negative
  coordinate normalization.
- `CombatCommand`: move/facing intent plus an optional target, point, direction, and ability request.
- `AbilityDefinition`: class/spec authorization, relation and shape targeting,
  range, cast/channel profile, spell school, cooldown, GCD, resource cost,
  requirements, and immutable effect slices.
- `CombatantState`: level, health, Mana/Energy, target-bound combo points, cooldowns,
  GCD, casts/channels, school lockouts, fixed-capacity auras, DR state, summons,
  position, facing, team, class/spec, and alive state.
- `CompanionSnapshot` and companion orders: the Hunter's persistent pet position,
  health, active order, current target, and local deterministic command state. The
  pet is intentionally not a victory participant. Direct hostile targeting of the
  pet remains pending until commands can address combatants and companions through
  one stable target identity.
- `CombatEvent`: accepted/rejected cast, hit, heal, status, interrupt, knockback, hazard tick, death, and result.
- `SessionViewState`: immutable UI projection for phase, objective, teams, participants, selected target, cooldowns, casts, telegraphs, recap, and result.
- `RunDefinition`, `EncounterDefinition`, `RunSpawnDefinition`, `ObjectiveState`, and
  `IRunController`: fixed-capacity sequential-encounter contracts used by Dungeon
  and legacy migration paths. Definitions admit at most four party members, five
  encounters, and eight spawns per encounter while respecting the 12-combatant
  simulation cap.
- `SiegeVariantDefinition`, `SiegeWaveDefinition`, `SiegeSpawnDefinition`,
  `SiegeEnemyArchetypeDefinition`, and `HeroSiegeRunController`: a separate
  fixed-capacity continuous-defense contract with 12 timed waves, one or four
  lanes, nine non-boss archetypes, stable per-spawn identities, a persistent
  fortress objective, and session-local gold plus HP/mana potions. Siege wave
  boundaries never create an encounter or replace the active simulation.
- `GuidedTrainingController`: a seven-step, per-specialization state machine driven
  by semantic observations rather than Unity input objects.
- `LocalPlayerProfileV1` and `SessionRecordV1`: migration-friendly local settings,
  Training status, mastery/cosmetic identifiers, and PII-free session-event data.

Presentation catalogs map stable IDs to prefabs, icons, materials, animation triggers, effects, audio, and timing. Presentation data cannot change an outcome.

## Game-mode composition

Shared services own combatants, commands, combat rules, navigation facts, events, bots, and view-state projection. Modes supply only mode-specific configuration and rule controllers.

Layer status is intentionally explicit:

| Product mode | Stable compatibility mapping | Current layer boundary |
|---|---|---|
| Arena | `GameModeId.Arena = 0` -> `PlayableModeId.Arena = 1`, `RunKind.Match` | Runnable local Unity session. |
| Training | UI `Training`; legacy `GameModeId.Leveling = 2` -> `PlayableModeId.None`, `RunKind.Training` | Runnable guided local Ashen Causeway session. |
| Hero Siege | `GameModeId.HeroSiege = 3` -> `PlayableModeId.HeroSiege = 2`, legacy `RunKind.Sequential` mapping | Source-integrated continuous 12-wave local session with Solo Lane and Four Lanes variants; fresh generated/test evidence is pending. |
| Dungeon | `GameModeId.Dungeon = 4` -> `PlayableModeId.Dungeon = 3`, `RunKind.Sequential` | Runnable local four-floor Unity session. |
| PvE Leveling | `GameModeId.PveLeveling = 5` -> `PlayableModeId.PveLeveling = 4`, `RunKind.PersistentRegion = 5` | Local repeatable camps and device-saved XP; no online transport or procedural terrain. |
| Legacy Boss | `GameModeId.Boss = 1` -> `PlayableModeId.Dungeon`, `RunKind.Sequential`, `EncounterKind.Boss` | Runnable local compatibility session and explicit Dungeon migration input. |

`GameModeId` ordinals are serialized compatibility values. `PlayableModeId` contains
`None`, Arena, Hero Siege, Dungeon, and PvE Leveling; Training is a `RunKind`, while legacy
Boss is an `EncounterKind` migrated into Dungeon. Neither identity set may be
renumbered or conflated. `LegacyGameModeMigrationAdapter` and the current Unity
routing make each conversion explicit, and all five product cards start the named
local mode.

### Persistent local PvE

Core owns combat outcomes and the separate persistent-level scaling rules.
`GrindingRegionRunController` owns bounded camp activation, unique spawn IDs,
kill credit, respawn/rearm clocks and accumulated XP. The Unity controller applies
Core death events before retiring actors, then flushes dirty XP outside the tick
through `LocalGrindingProgressStore`. Writes use per-specialization checksummed
files and atomic replacement; failures pause play instead of silently resetting.
Death replaces the local simulation at a frame boundary while retaining XP.
The existing chunk streamer presents the authored region; it does not decide
collision or combat. This store is not an account service or anti-cheat boundary.

### Arena session

`Lobby/Setup -> Countdown -> Playing -> Results -> Replay or Leave`

The arena controller owns two teams, spawn sets, victory conditions, disconnect policy, and normalized combat configuration. The first composition is 2v2 with the current seven-specialization roster: Discipline/Shadow Priest, Fire/Frost Mage, Shadow Dance/Mutilate Rogue, and Marksmanship Hunter. Team sizes from one through five are future configurations and must be load-tested before release claims.

### Legacy Boss session

`Party Setup -> Countdown -> Encounter -> Victory/Wipe -> Retry or Leave`

The encounter controller owns boss schedule, telegraphs, tankless target policy,
phases, wipe/victory, and reset. The current composition is three local participants.
It remains useful compatibility/content evidence but is not the Dungeon controller
and does not prove online cooperation or a raid system.

### Training route (legacy Leveling session)

`Class Setup -> Countdown -> Five gated packs -> Level 6/Failure -> Replay or Leave`

`LevelingRunController` owns the immutable Ashen Causeway definition, ten stable
mob IDs, one-time kill credit, integer XP, pack cadence, eastward activation gates,
seven stable district identities, and completion. Five cleared packs award five
levels, taking the player from level 1 to level 6. Core owns combat level,
health/damage bonuses, restoration, and the Leveling player-team ability gate.
`LevelingAbilityUnlockCatalog` supplies seven allocation-free per-specialization
kits: three actions at level 1, then one action at each of levels 2, 3, 4, and 5
after the first four packs. Arena and legacy Boss leave the progression-team
identity invalid and therefore retain their full kits.
Core also owns a conservative rectangular causeway envelope shared by movement,
placement, Blink, knockback, and companion movement; the Unity environment has no
gameplay colliders.
Application maps eight stable hostile archetypes to distinct
class/spec/primary/signature/range profiles, with an optional legal fallback action
for elite cooldown coverage. The five packs bind Raider +
Marksman, Prelate + Frostweaver, Brute + Warder, Raider + Marksman + Hexer, and
the Rift Warden finale in that order. Unity rebinds three existing enemy views
across packs, streams seven additive presentation chunks from `x=-3` through
`x=3`, and presents district, route, XP, and skill-unlock state.
Dead simulation entities are never respawned or assigned a reused `ParticipantId`;
the complete run consumes 11 of the fixed 12 combatant slots. Persistence, quests,
drops, NPC dialogue, actor streaming, and backend/account state are outside this
first layer.

`GuidedTrainingController` adds a deterministic seven-step coaching contract:
movement, target selection, a resolved combat action, hazard entry/recovery, ordered
pack-one-through-pack-four progression, final pack, and a resolved flick action.
It supports recommendation, skip, completion, and reset for every specialization.
Unity instantiates it for the legacy Leveling route, feeds semantic movement,
hostile-target, resolved-action, hazard, pack-clear, and resolved-flick observations,
presents the active step, binds the full Arena kit for flick mastery, exposes
skip/reset, persists per-spec completion/skip, and emits local structured records.
The reset path restarts both coaching and route progression. A safe fallback advances
the hazard-recovery lesson if the player reaches the practice target without ever
entering lava. These are implementation facts; uncoached comprehension remains a
manual product gate.

### Persistent PvE region

`GrindingRegionRunController` activates seven repeatable camps around the Core
`PveRegionLayout` for Sunbreak Basin (world `1`, zone `2`). Its 180 × 180 m
playable rectangle and thirteen compound cover landmarks replace the road for
this mode only. Each landmark is the union of three unequal overlapping circles
(39 solid components overall). Every component contains its landmark anchor,
making each union star-convex without an enclosed walkable pocket. Landmark
bounds and component ranges are separate authoring metadata: the bounds are not
solid circles. `ArenaCollisionMap.PveLevelingDefault` resolves movement,
placement, Blink, knockback and LOS against the actual components. PvE NPCs detour
around blocked chase/return paths. Placement projection stops once a complete
component pass needs no correction. Training's geometry and pack order are unchanged.

Unity selects the nine-chunk basin, supplies snapshot positions to streaming,
shows nearest-camp compass guidance, permits full camera yaw, and restores the
player at the southwest refuge on death/re-entry. Per-specialization XP remains
in the separate device-local save; neither chunks nor the server prototype own
combat or progression authority. No quest, procedural terrain or online claim is implied.

### Hero Siege session

`Variant Setup -> Preparation -> 12 timed, potentially overlapping waves -> Victory/fortress or defender failure -> Replay or Leave`

The immutable catalog defines two variants around the same 1000-health central
fortress. `SoloLane` assigns only the human to the east lane and creates no bots.
`FourLanes` assigns the human and three deterministic defenders one each to the
east, north, west, and south lanes. Twelve waves begin on their authored schedule
whether or not prior-wave enemies remain. Spawn release is bounded by eight active
hostiles globally; Four Lanes reserves two active hostile slots per lane.

The catalog contains nine regular, non-boss archetypes across swarm-minion,
standard, and elite presentation roles. Hero Siege deliberately has no miniboss,
boss rank, boss tier, final boss, or encounter reset. Wave definitions scale health,
damage, speed, population, and gold while retaining stable wave/lane/spawn/
participant identities. Kills and wave clears award run-local gold. The HUD keeps
compact gold and potion-use controls visible while purchase actions live in a
closed-by-default drawer opened explicitly by the player. HP or mana potion legality
still follows the human's resource type.

`HeroSiegeRunController` deterministically owns scheduling, bounded spawn queues,
objective-pressure damage, defender and objective failure, gold, consumables,
abandon, and the terminal result. Unity keeps the defenders, fortress objective,
and one `CombatSimulation` alive across all waves, retires defeated hostiles into a
twelve-view pool, and does not heal/revive the party or reconstruct combat state at
wave boundaries. It selects the generated `ashen_stronghold` presentation, exposes
the one-/four-lane setup, uses variant-specific defender bots and camera framing,
and presents wave/objective/supply/result/replay/leave state. This integration still
requires fresh same-revision generated, EditMode, PlayMode, and manual evidence.

`ModeAbilityKitCatalog` replaces the PvP trinket slot with one append-only,
Hero-Siege-only area action for every specialization: `DawnNova` (Discipline),
`UmbralCollapse` (Shadow), `FlameSurge` (Fire), `Blizzard` (Frost), `BladeTempest`
(both Rogues), or `ArrowRain` (Marksmanship). Target shape, legality, damage,
healing/slow riders, resource cost, and multi-target outcome are Core-owned; bots
prefer the mode action against useful hostile clusters. HUD, effect, animation, and
audio routes exist in source. `BladeTempest` retains its provenance-recorded
prototype icon; the expanded icon/catalog union still requires fresh acceptance.

### Dungeon session

`Run Setup -> Floors 1–4 -> Victory/Defeat -> Same-floor retry or advance -> Leave`

The immutable catalog declares four heroes and four ordered floors: three medium
enemies on the streamed traversal floor, four elites in an Arena floor, a mixed
four-hostile mechanics/miniboss floor, and a mixed four-hostile Rift Warden boss
floor. Each encounter owns its Core rules, party/enemy spawn positions, stable
identities, ranks, tiers, and health. `DungeonRunController` deterministically owns
encounter resolution, same-floor retry after defeat, next-floor advance, abandon,
and terminal result.

Unity constructs the controller, uses the same bounded eight-view pool, composes
Ashen Causeway for floor one, Arena presentation for floors two/three, Boss
presentation for floor four, resets simulation at explicit boundaries, and presents
floor/retry/result/replay/leave state. This reuses prototype environments and enemy
lineage; it is runnable local architecture, not proof of a production multi-floor
Dungeon content set or physical-device performance.

## World partition and streaming

The world-content pipeline has four explicit layers:

1. Core defines stable `WorldId`/`ZoneId` identities, integer chunk coordinates,
   and normalized 64-meter chunk-local positions without Unity dependencies.
2. Application computes deterministic, preallocated load/unload plans. Required
   chunks are ordered nearest-first, retained chunks use hysteresis, and a manifest
   cap bounds residency.
3. Editor manifests declare zone membership and content recipes. Rebuild produces
   a runtime `WorldZoneCatalog.asset`, one generated scene per chunk, and exact
   build-scene membership. A deterministic source fingerprint binds every generated
   catalog entry and scene root to the manifest version, seed, biome, and POI input.
4. `WorldZoneStreamController` serializes built-in additive scene operations around
   an explicitly supplied focus. A loaded scene is accepted only when its sole root
   carries the matching world, zone, and chunk identity.

`Ashen Causeway` currently declares world `1`, zone `1`, seven 64-meter chunks at
`(-3, 0)` through `(3, 0)`, load radius `1`, retain radius `2`, and a maximum of
`7` resident chunks. All seven POI recipes retain bespoke landmark geometry over
the shared Ashen biome/causeway recipe from West Refuge through East Watch. Chunk
scenes contain presentation only: no camera, audio
listener, event system, game controller, network manager, Collider, or Rigidbody.
The deterministic Core snapshot remains authoritative for player position, combat,
the route/basin envelopes, obstructions, camps, packs, and XP; Unity uses that position only as the
visual-streaming focus.

`Sunbreak Basin` adds world `1`, zone `2`, content version `2`, with a 3 × 3 grid at coordinates
`-1..1` on both axes, the same load/retain radii, and a nine-chunk cap. Its meadow,
plaza and rocky/ruined shelf builder reads shared Core bounds, landmark grouping,
and circle-union footprints. A version bump changes every basin chunk fingerprint
without changing world/zone/chunk identities, camp positions, or device-local XP.
The PvE region is authored independently of the guided causeway.

Manifest safety caps each zone at `1024` chunks and both streaming radii at `8`.
Neighborhood-budget validation is bounded by the local radius rather than quadratic
in total chunk count. Runtime lookup always uses the full `(WorldId, ZoneId)` pair,
and owner shutdown drains resident or in-flight additive scenes.

This is a scalable local content boundary, not a complete large-world runtime.
There is no chunk-owned actor lifecycle, spawn persistence, navigation partition,
general terrain topology, network area of interest, server shard, cross-zone travel,
save/backend persistence, or floating origin. Built-in scenes are packaged with the
player; Addressables or remote content delivery are not implemented. The current
generator supports explicit Ashen Causeway and Sunbreak Basin biome/POI pairs,
so an unknown recipe or cross-biome combination is rejected. See `Docs/WORLD_AUTHORING.md`.

## Bots

Bots read the same legal perception/view state available to a human participant and emit the same commands. They do not mutate health, cooldowns, targets, or positions directly. Their role logic prioritizes:

- Immediate hazard recovery and telegraph avoidance.
- Required interrupt, cleanse, heal, or ally rescue.
- Focus target or boss objective.
- Position, line of sight, and cooldown preparation.

Decision randomness is seeded. Reaction delay, aim variance, and limited lookahead
make behavior testable without giving bots hidden rules access. Seeded bot sessions
form deterministic regression fixtures; the current structured session-record
schema does not emit bot seeds.

## Scene and asset composition

- Serialized bootstrap/composition roots own explicit references; runtime discovery and silent fallback generation are disallowed.
- Bootstrap currently owns menu and gameplay composition plus generated Arena,
  legacy Boss, and Hero Siege presentation roots. `ArenaContentRuntimeLoader`
  selects a cataloged
  default by stable `ArenaContentId`, supports cataloged prefab/scene lifetime, and
  keeps one presentation resident. It does not expose player selection or rotation.
  Ashen Causeway visuals live in manifest-declared additive chunk scenes and are
  loaded for Training and the first Dungeon traversal floor; later Dungeon floors
  deliberately reuse current Arena/Boss presentations.
- Character, boss, spell, telegraph, hazard, HUD, and pooled-effect prefabs are authorable assets.
- ScriptableObject tuning assets are validated and converted to immutable runtime definitions when a session starts.
- Pools have fixed or bounded growth, return all objects on reset, and expose development counters.
- One semantic theme controls team, target, hazard, friendly, hostile, cast, and recovery presentation.

Release art may be first-party, commissioned with documented ownership/IP transfer,
CC0, or commercially licensed for use and distribution in the game. Every accepted
source needs a retained license, source/version provenance, permitted-use review,
and a record of any raw-source redistribution restriction. Purchase alone is not
production acceptance: style direction, originality, mobile budgets, LODs,
materials, rig/deformation, animation, VFX/audio language, and in-game readability
still require review.

A third-party source exists behind an explicit source-level selector and immutable
provenance/hash validation. Adobe's official Mixamo FAQ permits royalty-free use of
Mixamo characters and animations in commercial video games; raw/standalone asset
redistribution remains prohibited. Adobe General Terms section 3.6 also permits
modifying Content Files before embedding them in the game and distributing the
modified derivative only with that game; modified clothes, hair, face, mesh, and
materials may ship in-game, but original/modified raw FBX may not be redistributed.
The selected Mixamo sources now record `promotionAllowed:true` and may enter
Development or non-Development embedded game builds after source validation. The
user accepted them as customizable character bases on 2026-09-01. The build
validators enforce hashes, importer state, provenance, and model-only runtime
policy rather than a Unity Development flag. Erika's rejected raw animation and
prop evidence remains technical/art evidence, not a licensing failure. Deformation,
customization, originality, gameplay-camera, and mobile gates remain product work.

The retained KayKit and Quaternius distributions
are CC0 prototype sources, but current art-direction decisions can still exclude
them from a production lineage. “Stylized readable fantasy” guides adaptation; no
Warcraft model, texture, icon, interface layout, sound, effect, or recognizable
character design may be copied. Familiar internal spell labels remain a mechanical
comparison aid; public naming requires a separate originality/legal review.
Every proposed production source must have a complete, hash-bound entry in
`Docs/Art/PRODUCTION_ASSET_INTAKE_LEDGER.md`. The ledger currently records no
commissioned or commercially purchased production replacements and no accepted
production entry.

## Input, camera, and targeting

Input components retain originating pointer IDs. Movement, world targeting,
ability gestures, and presentation-only camera orbit may occur simultaneously.
Application code consumes device-neutral combat gestures, while desktop input
provides an equivalent development path. A short world press can select; a
thresholded horizontal world drag is owned by its pointer, emits yaw-only camera
orbit, and never becomes a selection tap.

There is one cross-team focus. Tapping an ally or enemy changes the same world
marker and detailed frame; unit-targeted abilities never consult a second hidden
friendly or hostile selection. A session begins with no focus. Target selection
is changed only by tapping a hero or its compact roster circle; there is no
cycling command or implicit acquisition path.

Unit actions resolve only against the explicitly focused hero. Invalid relation,
range, facing, setup, or line of sight produces a visible rejection without
substitution. Self and caster-area actions resolve without focus; mobility and
directional actions use movement intent, then facing when movement is neutral.
There is no drag-to-aim mode.

A release inside the movement dead zone is the only gesture that commits the
button's primary `AbilityId`. Arena, Hero Siege, Dungeon, and legacy Boss use ten
visible controls in one `2 + 4 + 4` contract: slots 0–1 are large gesture families,
slots 2–5 are medium common actions, and slots 6–9 are small utilities. The two
gesture families each combine a primary with four cardinal alternates, exposing at
most 18 actions. Only the gesture-family slots may bind directional
alternates or show cardinal cues. Each family is one 232-reference-pixel disc with an
84-pixel center primary and four transparent 60-pixel direction regions at fixed
78-pixel offsets; primary and directional art are 48 pixels. One persistent 232-pixel
non-raycast guide supplies the shared background and emits mesh only for bound
directions. Direction regions own only icon/glyph content, never separate surfaces,
bezels, accents, or frame art. Holding reveals captions at their authored below-icon
offsets and changes selection state without resizing or rearranging the disc.
The four common controls are 132-pixel circles with 82-pixel art; the four utilities
are 92-pixel circles with 56-pixel art. Their visible input faces use circular raycast
filters. Pause is a 64-pixel action, while Siege potion-use/shop actions are 52 pixels
tall and purchase actions are 56 pixels tall.
A quick
cardinal flick can commit one explicitly
bound alternate `AbilityId`; the originating pointer owns the gesture and the first
threshold crossing locks its direction. A slow swipe, moved hold, or flick toward
an unbound direction cancels. It cannot re-quantize or silently fall back to the
primary action on release. Every specialization fills all four cardinal directions
across both families, which exercises the complete gesture contract without
ability pages or drag-to-aim. Desktop development shortcuts map the ten primaries
to `1`–`9` and `0`; Shift exposes configured Up alternates on slots 0–1. Training
binds a separate seven-primary learned-only subset to the same controls, disables
swipe alternates, begins with three actions, hides four pack-gated actions until
learned, and leaves slots 7–9 inactive.
Core applies the same restriction to team 1, so presentation and desktop binding
cannot authorize a hidden or unlearned action.

Hero Siege replaces the Arena Trinket slot with the specialization's explicit area
action: `DawnNova`, `UmbralCollapse`, `FlameSurge`, `Blizzard`, `BladeTempest`, or
`ArrowRain`. The stable mode is part of readiness, bot use, and Core authorization;
Arena, Dungeon, Training, and legacy Boss do not inherit the binding. The source
icon/catalog union is now 108 abilities and requires fresh generated/test evidence.

The perspective strategy camera has fixed authored pitch, distance, FOV, and
objective-side look-ahead plus player-controlled wrapped yaw. Empty-world
horizontal drag, Q/E, or gamepad right-stick input rotates heading only. It follows the human, then a spectated ally after
death, without inheriting target rotation or using collision zoom. Arena and middle
Dungeon headings keep combat centered; Hero Siege selects a lane-following Solo
composition or a higher fortress-centered Four Lanes composition; legacy Boss and
the Dungeon boss retain the wider objective composition. Training and the first
Dungeon floor use eastward look-ahead to expose the streamed route.
Framing cannot influence authoritative range or line-of-sight rules; a unit action
submits only the explicitly selected target.

## Local profile and session records

`LocalPlayerProfileV1` is a version-one, account-free schema with validated defaults
for master/effects volume, haptics, reduced motion, high contrast, per-spec Training
completion/skip state, and stable mastery/cosmetic identifiers. Normalization
recovers missing, corrupt, or unknown data without granting progression.

`SessionRecordV1`, `ISessionRecordSink`, and `SessionRecordWriter` define PII-free
started, Training-step, ability-unlock, and finished records. The Unity
`LocalProductServices` adapter reads/writes `profile-v1.json`, can append
`playtest-v1.jsonl`, rotates the log at 2 MiB, and applies local audio settings.
The tracked generated Bootstrap now owns and wires this component, the kit viewer,
and pause/settings panel. Normal session flow opens/closes records, emits Training
step/unlock events, and persists completed/skipped/reset outcomes. Reduced motion
suppresses/clears camera impulse; high contrast immediately repaints semantic HUD
states; master/effects volume are applied. The haptics setting gates a basic
supported-mobile result vibration. There is no analytics upload, account, cloud
synchronization, reward grant, or demonstrated migration from a prior schema.

## Failure and observability

- Connection, local-record, and presentation failures must not crash the combat simulation.
- A disconnect is handled by a mode policy: temporary bot takeover or removal, followed by one authoritative result.
- The host-authority layer exposes deterministic rejection reasons and bounded state
  for tests; player-facing ping/reconciliation diagnostics are not integrated.
- The implemented local record schema is intentionally narrow: session start,
  Training step, Training unlock, and finish/outcome, without account, device,
  location, free-text, or network identifiers. Combat timelines, rematch,
  disconnect, performance, and upload telemetry are not implemented.

## Verification strategy

### Core and application tests

- Ability legality, cooldown and cast timing, health/heal bounds, resources, statuses, interrupts, lava ticks, and death.
- Fixed knockback distance/path/collision/recovery and preview-command equivalence.
- Stable simultaneous-event ordering, reset, replay, victory, wipe, disconnect, and seeded bot outcomes.
- Arena rules, legacy Boss tankless target rotation, all seven Training
  specializations and their `3 -> 7` sequence, Training skip/reset, Hero Siege
  variant/lane scheduling, overlapping-wave/objective/economy outcomes and explicit
  non-boss contract, Dungeon advance/retry, profile normalization, and session-record
  validation.
- World-position normalization across positive and negative chunk boundaries;
  deterministic chunk load/retain/unload ordering, capacity, and warmed-plan
  allocation behavior.

### Network tests

Current focused EditMode tests prove the pure 30 Hz clock, four-slot roster for one
through four humans plus bots, closed admission, disconnect/replay policies,
sender/actor authorization, ordered bounded command queues, session epochs,
snapshot/event envelopes, and the injected anonymous-auth/MPS Relay
create/join/leave/termination seam. They do not contact live Unity Services, open a
real Relay session, transport combat, or run combat between processes/devices.

Before any online claim, tests and device evidence must additionally prove:

- Host-local and remote intent traverse the same NGO message/RPC adapter and
  authority queue into Core combat validation.
- NGO sender identity owns the submitted participant; duplicate, reordered,
  delayed, stale-epoch, and unauthorized commands cannot change results.
- Authoritative snapshots/events reach remote clients; owner prediction and
  reconciliation converge health, position, cooldown, status, objective, and result.
- Admission, client disconnect/bot replacement, host loss, finish, replay, leave,
  MPS session deletion, scene lifetime, and shutdown work through the actual
  services and transport.
- A host plus remote device completes repeated sessions under simulated latency,
  loss, reordering, and duplication without conflicting winners.

### Unity and device tests

- Exactly one composition root, active gameplay camera, audio listener, and input event system.
- Generated catalog/build-scene parity, one identity root per world chunk, no
  gameplay physics or global systems in additive scenes, bounded route residency,
  and complete unloading when Training ends.
- Truthful mode routing: Arena starts Arena, Training starts the guided Ashen route,
  Hero Siege starts the selected one- or four-lane continuous 12-wave defense, and
  Dungeon starts its four-floor slice.
- Generated composition contains and wires any claimed profile/settings/logging
  service; a complete/replay/leave cycle emits only the intended local records.
- Safe-area HUD at 16:9, 20:9, and 2.4:1; multiple touch IDs; pause/background clears held gestures.
- Pools, objects, materials, and memory remain bounded through ten Arena replays,
  repeated full Hero Siege completion/replay/leave cycles, and Dungeon retry/leave
  cycles.
- Stable p95 frame time at or below 16.7 ms on the selected mid-range 1080p Android device after warm-up.

## Delivery phases

The implementation sequence follows `Docs/ROADMAP_ARENA_FIRST.md`:

1. Preserve the now-explicit legacy migration, four-card routing, generated
   composition, and current automated validation evidence.
2. Validate guided Training and local profile/record behavior with uncoached users,
   restart/corruption checks, and physical-device input.
3. Validate and polish the local Arena loop across all seven specializations,
   aspect ratios, ten replays, fresh players, and the Android reference class.
4. Promote the source-integrated Hero Siege and runnable four-floor Dungeon
   prototypes only after Siege's full-run, timed-overlap, variant/lane, fortress,
   supply, swarm/AoE, soak, and physical-device gates and Dungeon's floor/retry
   equivalents pass.
5. Treat online Arena as a separate proof: connect the existing MPS/Relay lifecycle
   and host-authority contracts to combat transport/reconciliation, then pass live
   services, remote-device, and adverse-network gates before exposing an online label.
6. Replace trial/proxy presentation with an accepted commissioned, first-party,
   CC0, or commercially licensed production art line and add production-ready Arena
   rotation and Dungeon variety for Beta MLP.

Accounts, backend persistence, matchmaking, MMO actor/AOI partitioning, loot
economy, and large raids remain excluded until the bounded session product is
human-validated.

The implemented 3.3.5-inspired kits preserve the requested interaction
relationships and recognizable timing where the current deterministic rules can
represent them. Output numbers, ranges, resources, and some cooldowns are tuned
for prototype-length sessions, and omitted systems such as random critical strikes
are not implied by familiar internal spell labels.
