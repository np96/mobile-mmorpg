# Arena-First Product Roadmap

This plan deliberately narrows the solo-development product to Arena, Training,
Hero Siege, and Dungeon. Arena is the primary repeatable mode. Training teaches one
large specialization kit without making leveling mandatory. Hero Siege and Dungeon
add bounded local PvE variety, with Siege supporting solo or bot-backed defense.
MMO world simulation, accounts, backend
persistence, matchmaking, persistent/meta economy, open-world quests, and large
raids are outside Beta MVP, MVP, and Beta MLP. Hero Siege's run-local gold and
consumables are part of that bounded session, not an account economy.

## Status language

- **Runnable:** the tracked Unity composition starts, plays, ends, replays, and
  leaves the named mode.
- **Contract:** deterministic Core/Application definitions and tests exist, but
  they do not establish Unity gameplay.
- **External gate:** evidence required before a feature or asset can be described
  in a build shown outside the development team.

## Stable mode taxonomy and current truth

| Product name | Compatibility mapping | Current truth |
|---|---|---|
| Arena | `GameModeId.Arena = 0` -> `PlayableModeId.Arena = 1`, `RunKind.Match` | Runnable local 2v2. Stable-ID loader selects the catalog default; no player-facing map selection. |
| Training | UI `Training`; legacy `GameModeId.Leveling = 2` -> `PlayableModeId.None`, `RunKind.Training` | Runnable guided five-pack `3 -> 7` route with recommendation, skip/reset, flick bridge, local profile, and local records. |
| Hero Siege | `GameModeId.HeroSiege = 3` -> `PlayableModeId.HeroSiege = 2`, legacy `RunKind.Sequential` mapping | Source-integrated 12-wave continuous fortress-defense prototype: selectable Solo Lane is one human with no bots; Four Lanes is one human plus three bots. Fresh generated/test acceptance is pending. |
| Dungeon | `GameModeId.Dungeon = 4` -> `PlayableModeId.Dungeon = 3`, `RunKind.Sequential` | Runnable local four-hero/four-floor prototype with same-floor retry and bots. |
| Legacy Boss | `GameModeId.Boss = 1` -> `PlayableModeId.Dungeon`, `RunKind.Sequential`, `EncounterKind.Boss` | Runnable compatibility prototype and explicit Dungeon migration input, not another product mode. |

Never renumber the legacy or cross-layer enums. Training and legacy Boss must remain
explicit run/encounter adapters rather than invented `PlayableModeId` values. All
four cards now have explicit routes. The recorded 2026-09-02 rebuild, validation,
EditMode, PlayMode, and Android Development build evidence predates the current
Hero Siege redesign and cannot prove it. See `Docs/DEMO_READINESS.md` for exact
historical artifacts and the fresh generated/test/manual gates still required.

The 2026-09-01 usability/presentation pass also replaced rectangular alternate
descriptions with one four-sector radial wheel, added pointer-owned horizontal-drag
camera yaw plus Q/E/gamepad equivalents, fixed Pet Attack command/damage-source
presentation, separated six spell-school sound families, and gave Blade Tempest a
dedicated knife-storm/whirlwind presentation. The current Siege redesign adds a
direct mode-only area action for every specialization—Dawn Nova, Umbral Collapse,
Flame Surge, Blizzard, Blade Tempest, or Arrow Rain—plus the Ashen Stronghold
composition. These changes remain subject to the fresh evidence and physical-device
gates below.

## Phase 1 — Beta MVP

### Product promise

A player chooses any of seven specializations, can take a recommended but skippable
Training route, and then plays repeated local 2v2 Arena rounds with bots. The build
is honest about being offline; Hero Siege and Dungeon may be shown as prototype
previews only after the exact build shown has fresh acceptance evidence. They do not
become Beta MVP exit blockers.

### In scope

- Arena as the only primary replay loop: class/spec selection, combat, result,
  replay, leave, readable target/control/hazard state, and the current default map.
- Training on Ashen Causeway: three starting actions, one unlock after each of the
  first four packs, final-pack mastery, then one resolved flick lesson that bridges
  to the ten-control, 18-action Arena kit.
- Seven guided steps wired from semantic movement, target, resolved-combat,
  hazard-entry/recovery, pack-clear, and flick observations. Training is recommended
  once per specialization, can be skipped, and can be reset.
- Device-local schema-v1 settings and per-spec Training state, with corruption-safe
  defaults. Master/effects volume, reduced motion, high contrast, and a basic
  settings-gated supported-mobile result vibration are applied.
- PII-free, device-local started/Training-step/unlock/finished records wired once per
  session boundary. No network upload or free-text/device/account identifier.
- Correct stable mode mapping. Hero Siege and Dungeon route to their real local
  controllers; legacy Boss remains a compatibility/development combat test. Routing
  source alone does not establish a runnable generated build.
- The selected Mixamo models are licensed customizable game bases. Embedded builds
  retain exact provenance/hash/import validation and never redistribute original or
  modified Content Files standalone.

### Exit gates

- Fresh `validate`, full EditMode, and full PlayMode evidence after rebuilding the
  generated foundation; no stale-scene assumptions.
- All seven specializations complete the exact `3 -> 7` sequence, skip/reset, return
  to the full Arena kit, and replay without state leakage.
- An uncoached new player identifies the objective, selects a target, uses a recovery
  action, understands the four unlocks, and performs one intended radial-wheel flick.
- Arena testers voluntarily request or start a fifth round.
- Ten consecutive Arena/Training completion-replay-leave cycles show no lost input,
  duplicate presentation/audio, exceptions, recurring GC spikes, or material memory
  growth.
- Current safe-area/final-pixel checks at 16:9, 20:9, and 2.4:1 plus reachability,
  readability, and sustained profiling on the chosen mid-range Android device.
- The actual generated scene contains every claimed settings/profile/logging
  component; restart/corruption tests prove defaults, and inspected local records
  contain only the declared PII-free fields.
- Hero Siege/Dungeon are labeled prototype slices, not polished MVP content; no
  online, production-art, MMO, account, matchmaking, or persistent-power claim
  appears in the build or demo script.

## Phase 2 — MVP

### Product promise

Arena remains the main repeat loop. Training is a short optional ramp. Hero Siege
and Dungeon each provide one complete local alternative built on the same combat
rules, stable IDs, and mode-appropriate continuity/result discipline.

### In scope

- A polished local Arena loop across all seven specializations and reliable bots.
- One complete `X Hero Siege`-referenced structure using original Session RPG
  content: a persistent central fortress; selectable Solo Lane (one human, zero
  bots) and Four Lanes (one human plus three bots) variants; 12 timed waves that can
  overlap; nine regular swarm/medium archetypes; a global eight-hostile cap; victory
  and both failure paths; results, replay, leave, HUD, VFX/audio, and bounded pools.
  Siege has no minibosses, bosses, final-boss wave, or between-wave encounter reset.
  Kills and wave clears grant session-local gold used to buy and consume HP and mana
  potions. Every specialization replaces its PvP trinket with a direct mode-only
  area action: `Dawn Nova`, `Umbral Collapse`, `Flame Surge`, `Blizzard`, `Blade
  Tempest`, or `Arrow Rain`; the Rogue action fills the requested close-range role.
- One complete Dungeon slice: four sequential floors (traversal, elite Arena,
  mechanics/miniboss, Rift Warden boss), stable spawn population, same-floor retry,
  advance, results, replay, leave, clean environment lifetime, HUD, VFX/audio, and
  bounded pools.
- Training recommendation/skip state and settings persist locally. Mastery and
  cosmetic identifiers may be stored, but cannot imply an earn/equip UI that does
  not exist or grant competitive power.
- Any externally distributed MVP must clear the asset gate below. Adobe licensing
  permits Mixamo use and modification embedded in commercial games. The user
  accepted the selected models as customizable game bases on 2026-09-01; source
  validators now admit non-Development embedded builds while technical/art gates
  remain tracked separately.

### Exit gates

- Each card starts the named controller and content; no fallback to Arena or legacy
  Boss. Invalid transitions fail visibly and never create partial sessions.
- Hero Siege and Dungeon combat outcomes come from Core simulation. Unity physics,
  presentation, wave/floor loaders, and animations cannot decide damage, objective
  loss, scheduled-wave completion, Dungeon encounter/retry state, purchases,
  consumable restoration, or result.
- Deterministic replay and invalid-event tests pass; warmed controllers and steady
  gameplay remain allocation-free within their documented boundaries.
- Manual full-run coverage for every admitted specialization/party role, both
  failure paths, replay, leave, pause/background, and target aspect ratios.
- Ten-session soak and physical-device CPU/GPU/memory evidence pass for Arena,
  Hero Siege, and Dungeon scenarios.
- Fresh-player tests confirm that the three modes have distinct, immediately clear
  objectives and that Arena remains the mode players most want to repeat.

Online Arena is not an MVP implication. It may be scheduled separately only after
the proof gate below; otherwise the MVP is labeled local/offline.

## Phase 3 — Beta MLP

### Product promise

The proven session game becomes coherent and desirable, not merely functional:
production-ready characters, creatures, environments, animation, spell language,
sound, multiple Arena battleground experiences, and authored multi-floor Dungeon
variety support the same readable combat.

### In scope

- Customize and optimize the selected Mixamo-derived bases while preserving their
  exact provenance and embedded-game-only distribution boundary. Original or
  modified raw FBX must never be redistributed. First-party, commissioned, CC0,
  or other licensed additions remain optional brand/quality routes and require
  their own provenance.
- One coherent art direction across all four classes, enemies, Hero Siege fortress
  and lanes, Arena silhouettes, Dungeon floors and bosses, UI, animation, VFX, and
  audio.
- A player-facing Arena rotation with more than one production-ready battleground;
  stable selection/rotation and lifetime behavior must be tested, not inferred from
  catalog entries.
- Authored Dungeon floor/room variety with readable landmarks, encounter pacing,
  clean transitions, and a final boss that feels part of the same visual world.
- A cosmetic-only mastery loop may expose the existing stable identifiers after its
  earn, equip, reset/migration, and no-competitive-power rules are designed and
  tested.
- Human validation focuses on repeat intent, Training completion/skip behavior,
  mode preference, readability, delight, and return intent rather than content count.

### Exit gates

- Art-direction and originality review; provenance/license audit; no development
  trial selector or forbidden source in an external build.
- Production deformation, animation, material/PBR, LOD, VFX/audio, safe-area, and
  Android performance evidence in normal combat, not only turntables or screenshots.
- Fresh uncoached sessions pass the design gates in `DESIGN.md`; usability or
  readability failures send work back to the existing modes rather than expanding
  MMO/meta scope.
- Every external claim is supported by the exact current build, full automated
  suites, physical-device evidence, and reproducible capture/profiler records.

## Separate online proof gate

The existing network layer proves protocol/authority data structures plus an
injected anonymous-auth/private four-player MPS Relay create/join-code/leave/abort
lifecycle. It has not contacted live Unity Services or transported combat. Do not
label Arena online until all of the following are integrated and evidenced:

1. Host-local and remote intent use the same authenticated NGO message/RPC adapter,
   bounded authority queue, and Core simulation path.
2. Sender ownership, session epoch, sequence, tick, and ability/target legality are
   enforced against real NGO clients.
3. Authoritative snapshots/events are delivered and applied; owner prediction and
   reconciliation converge without rewriting accepted outcomes.
4. Anonymous sign-in, private Relay creation/join code, admission, bot
   fill/replacement, client disconnect, host loss/session deletion, result, replay,
   leave, and shutdown work through live services and the actual transport.
5. A host and remote physical client complete repeated sessions under latency, loss,
   reordering, and duplication without divergent state or winners.

Public lobby/discovery, matchmaking, accounts beyond anonymous service identity,
anti-cheat operations, and dedicated hosting remain separate product/backend
decisions even after this private-Relay proof.

## External asset gate

Adobe's current official terms/FAQ permit royalty-free Mixamo characters and
animations in commercial games. Content Files may be modified before embedding in
an End Use and distributed only with that End Use; raw/standalone original or
modified FBX redistribution is prohibited. Clothes, hair, face, mesh, and material
changes may therefore ship inside the game, but remain derivative assets.

The repository now records the selected Mixamo models as
`licensed-game-base` with `promotionAllowed:true`. Validators admit Development
and non-Development embedded game builds while enforcing exact hashes, importer
state, provenance, and model-only runtime motion. Erika's raw animation and
weapon/prop findings remain technical/art findings. Commissioned replacement is
optional for brand/quality, not required by the Mixamo license.

`Docs/Art/PRODUCTION_ASSET_INTAKE_LEDGER.md` is the mandatory source/license/receipt,
version, permitted-use, hash, redistribution, gameplay-camera, deformation, mobile
budget, and acceptance record. It currently contains no commissioned or
commercially purchased replacement and no `ACCEPTED_PRODUCTION` entry.

Buying an asset pack or hiring an artist does not automatically satisfy Beta MLP.
The result still needs coherent direction, originality review, mobile optimization,
rig/deformation and animation acceptance, VFX/audio integration, LOD/material
budgets, and readable gameplay-camera evidence.

## Product decisions still required

1. Is “MVP” strictly closed/internal, or externally distributed? The Mixamo bases
   are approved for embedded builds; public distribution still needs a source-
   packaging check plus physical-device and final customization acceptance.
2. Is the current four-hero Dungeon party (one player plus bots) the intended MVP
   identity, or should a distinct solo variant be researched later? Do not overload
   the current run implicitly.
3. Is online Arena required before Beta MLP, or is local/offline the deliberate
   product until the three-mode loop validates? The recommendation is local first.
4. How far should the selected Mixamo bases be customized for Beta MLP? Clothes,
   hair, face, materials, silhouette, deformation, and LOD work determine brand
   distinction and schedule; commissioned replacement is no longer the default.
5. What does mastery unlock: display-only badges, cosmetic variants, or both? It
   must remain separate from competitive power.
