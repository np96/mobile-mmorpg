# Product and Combat Design

## Approved PvE expansion — 2026-09-04

Alongside the Arena-first baseline below, the user approved a separate **PvE
Leveling** prototype: persistent device-local XP, repeatable mob camps, varied
enemy roles, refuge recovery, and a cinematic location pass, with **no quests**.
See [PvE Leveling mode](Docs/PVE_LEVELING_MODE.md). This is an explicit expansion
of the earlier session-only scope; Training and its serialized Leveling identity
remain unchanged. The separate [local backend](Server/README.md) is not connected
to this gameplay slice. The older exclusions below describe the baseline roadmap,
not a prohibition on this approved local prototype.

## Product promise

Deliver the coordination, class expression, positioning, and progression associated
with a fantasy MMO in bounded, replayable sessions. **Arena is the primary product
loop.** Training is a recommended, skippable onboarding route for the large ability
kit; Hero Siege and Dungeon are the two source-integrated local PvE prototype modes,
with Siege supporting either solo or bot-backed defense. An MMO,
shared open world, and persistent leveling journey are deliberately outside the
current solo-development scope.

The first milestone answers one question: is touch-first strategy-view class combat
with strong positional knockback clear and satisfying enough that players request a
fifth Arena round after learning one specialization in Training?

## Design pillars

1. **Combat immediately.** A player chooses a class and mode, then reaches meaningful combat with minimal delay.
2. **Readable decisions.** Damage, healing, crowd control, telegraphs, hazards, and knockback causes are visible early enough to answer them.
3. **Coordination over composition tax.** Classes have distinct jobs and combinations, but the first boss does not require a conventional tank.
4. **Position is a resource.** High ground, cover, spacing, knockback, recovery tools, and lava create tactical choices without making deaths feel random.
5. **Fair competition.** PvP is decided by execution and cooperation. Future equipment is normalized or provides tradeoffs rather than permanent power advantages.
6. **World scale by composition.** Stable world, zone, and chunk identities let
   authored locations grow without turning presentation scenes into combat
   authority or one unbounded Unity scene.

## Product mode taxonomy

The player-facing names below are the stable roadmap vocabulary. Existing serialized
`Boss` and `Leveling` identifiers are compatibility contracts and must not be
renumbered or silently reinterpreted.

| Player-facing mode | Product role | Current boundary |
|---|---|---|
| Arena | Primary competitive/replay loop | One local 2v2 implementation is runnable. |
| Training | Recommended and skippable onboarding | The guided seven-step Ashen Causeway route is runnable locally through the legacy `Leveling` adapter. |
| PvE Leveling | Persistent local mob-grinding prototype | Separate appended identities; seven repeatable camps, device-local per-specialization XP and levels 1–10; no quests, inventory, cloud save or networked combat. See fresh mode evidence. |
| Hero Siege | Solo/cooperative fortress defense | Source implements a continuous 12-wave fortress defense with selectable one-lane solo and four-lane human-plus-bot variants; fresh same-revision generated/test evidence is still required. |
| Dungeon | Multi-floor PvE progression | A local four-hero/four-floor sequential slice is runnable with bots. |
| Boss | Compatibility content, not a long-term top-level lane | The local three-participant prototype remains runnable; its encounter identity migrates into the Dungeon taxonomy. |

The home screen routes Arena, Training, Hero Siege, and Dungeon to distinct local
sessions. Legacy `Boss` and `Leveling` values remain serialized migration inputs,
not additional `PlayableModeId` values. Current automated evidence establishes
the previously accepted composition and deterministic flow, but it predates the
current Siege redesign. Fresh generated/test evidence, uncoached full runs, soak,
final-art, and physical-device quality gates remain open.

## First playable scope

### Arena

- One 2v2 map with a clearly readable flat combat floor, two mirrored
  line-of-sight wardstones, knockback space, recovery routes, and visible lava.
  The wardstones use the same authoritative collision contract for movement,
  displacement, teleports, targeting, and bots. Elevated contest areas remain
  deferred until they can meet that same standard.
- A team wins by defeating both opponents; health reaches zero through normal combat and hazards.
- Fixed, deterministic knockbacks create openings and displacement plays. A Mage can remove a Healer from favorable ground, but every displacement has a readable cast, direction, and landing risk.
- Lava deals heavy, continuous damage and pressures the player to use a recovery route or mobility skill. Touching lava is not an unexplained instant death.
- Bots use the same movement, targeting, cooldown, damage, healing, status, and knockback rules as humans.

### Legacy boss encounter

- One current compatibility encounter for three participants: one human plus two bots.
- There is no dedicated tank requirement. The boss uses explicit targeting rules, short commitments, and spatial telegraphs rather than unavoidable sustained tank damage.
- The signature mechanic is a clearly telegraphed knockback toward lava, followed by a safe recovery window and route.
- Success depends on avoiding telegraphs, coordinating interrupts or recovery, and maintaining damage and healing—not on hidden gear checks.
- This is useful combat and encounter evidence, but it is not a fourth roadmap mode,
  a raid system, or evidence of networked cooperative play. Its strongest content
  can be migrated into Dungeon after that mode has a real runtime slice.

### Training route (legacy Leveling runtime)

- `Ashen Causeway` is a recommended, skippable, session-local level 1–6 onboarding
  route and the first streamed-zone proof, not a persistent MMO zone. The current
  runnable implementation still uses the legacy `Leveling` mode identifier.
- Seven additive 64-meter presentation chunks cover coordinates `(-3, 0)` through
  `(3, 0)`, approximately 448 meters east to west. Around the player's authoritative
  position, the visual streamer loads radius `1`, retains radius `2` to avoid seam
  thrashing, and never exceeds the manifest cap of `7` resident chunks.
- The route is one complete local leveling map with seven named districts: West
  Refuge, Cinder Approach, Broken Gate, Collapsed Court, Ossuary Bend, Reliquary
  Yard, and the East Watch finale. Every district has a retained bespoke landmark
  recipe; Collapsed Court and Reliquary Yard deliberately provide breathing and
  staging space between combat packs.
- The player advances east through five deterministic packs containing ten
  lifetime enemies. A cleared pack and 1.5-second recovery window are necessary,
  but later packs also require authoritative route progress; waiting at the spawn
  cannot farm or pull the whole location.
- A conservative Core-owned causeway envelope keeps movement, Blink, knockback,
  and the Hunter companion inside the visible route. Unity walls, cinder, and lava
  are presentation-only and do not decide collision.
- Each pack grants exactly one level. Cumulative thresholds are `0`, `80`, `200`,
  `380`, `680`, and `1130` XP. A level adds health and damage and restores health
  and the class resource so the short run demonstrates progression without loot.
- A Training player starts with three curated primary actions. Clearing each of
  the first four packs reaches levels 2 through 5 and unlocks exactly one new
  action, producing a `3 -> 4 -> 5 -> 6 -> 7` learning curve. Core rejects every
  unlearned action with `level_locked`; the compact HUD shows only learned actions
  and reveals each new control in its stable authored position. The four Arena-only
  slots remain hidden. Directional alternates are not part of the route kit.
- Eight hostile NPC archetypes have stable names and distinct deterministic
  primary/signature combat profiles: Ashbound Raider, Cinder Marksman, Ashbound
  Brute, Cinder Hexer, Causeway Prelate, Rimebound Frostweaver, Reliquary Warder,
  and Rift Warden. The finale Warden additionally chains Rift Roar between Cleave
  and Meteor cooldowns, uses Core's resolved Meteor point for its warning, and
  keeps repositioning rather than becoming inert at close range.
- The exact pack sequence is Raider + Marksman in West Refuge; Prelate +
  Frostweaver in Cinder Approach; Brute + Warder at Broken Gate; Raider +
  Marksman + Hexer in Ossuary Bend; and Rift Warden at East Watch.
- The seven class-derived enemies reuse the selected Mixamo licensed game bases;
  Rift Warden reuses the existing prototype boss proxy. Enemy-specific clothes,
  faces, hair, materials, and silhouette customization remain presentation work.
- There are no quests, dialogue, drops, persistent power, respawns, account state,
  or MMO backend in this slice.
- The selected Mixamo class models may present the seven class-derived enemy
  archetypes in embedded Development and non-Development game builds. They remain
  Mixamo-derived and must not be distributed as original or modified standalone
  Content Files.

The Unity-free guided Training controller defines seven ordered steps: movement,
targeting, basic combat, hazard recovery, ability progression, final-pack mastery,
and one resolved flick action that bridges into the Arena control language. It has
per-specialization recommended, completed, skipped, and reset semantics. Unity now
instantiates it, feeds movement/target/combat/hazard/pack/flick observations,
presents the active step, exposes skip/reset, and records per-spec outcomes in the
device-local profile. Automated Core/Application/Unity coverage is current; whether
new players understand the route without coaching remains a human-validation gate.

#### Training skill progression

| Specialization | Level 1 — starting three | Pack 1 / level 2 | Pack 2 / level 3 | Pack 3 / level 4 | Pack 4 / level 5 |
|---|---|---|---|---|---|
| Discipline | Penance, Smite, Shadow Word: Pain | Power Word: Shield | Flash Heal | Psychic Scream | Power Infusion |
| Shadow | Shadow Word: Pain, Mind Flay, Flash Heal | Shadowform | Psychic Scream | Mind Blast | Dispersion |
| Fire | Fireball, Fire Blast, Polymorph | Frost Nova | Blink | Dragon's Breath | Combustion |
| Frost | Frostbolt, Ice Lance, Frost Nova | Ice Barrier | Blink | Deep Freeze | Icy Veins |
| Shadow Dance | Hemorrhage, Eviscerate, Stealth | Sap | Shadowstep | Kidney Shot | Shadow Dance |
| Mutilate | Mutilate, Envenom, Stealth | Sap | Vanish | Kidney Shot | Cold Blood |
| Marksmanship | Steady Shot, Arcane Shot, Freezing Arrow | Pet Attack | Disengage | Serpent Sting | Rapid Fire |

### Hero Siege slice

- `X Hero Siege` is the structural reference for this mode's pressure loop: one
  persistent central fortress, readable approach lanes, timed overlapping waves,
  and a session-local supply economy. Maps, enemy identities, abilities, art, and
  other authored content remain original to Session RPG.
- The player selects either `Solo Lane`—one human defending the east lane with no
  bots—or `Four Lanes`—one human plus three bots, with one defender assigned to
  each cardinal lane. Both variants defend the same 1000-health fortress through
  12 scheduled waves in one long-lived combat simulation.
- A due wave starts even if enemies from earlier waves remain alive, so falling
  behind creates overlapping pressure rather than an encounter reset. The bounded
  combat population permits at most eight active hostiles globally; the four-lane
  variant reserves two hostile slots per lane.
- Nine regular enemy archetypes span small swarm and medium/elite roles: Ashling,
  Cinder Runner, Ashbound Raider, Cinder Marksman, Ashen Brute, Cinder Hexer, Rime
  Weaver, Reliquary Warder, and Causeway Prelate. Hero Siege has no miniboss, boss,
  boss tier, final-boss exception, or between-wave party revival.
- Kills and cleared waves award session-local gold. Compact `G`, `HP`, and `MP`
  counters keep consumption available without a permanent shop panel; a deliberate
  Shop action opens the otherwise hidden purchase drawer. The player can buy and
  consume HP potions, and mana-using heroes can buy and consume mana potions. Gold and
  potion inventory end with the run; they are not loot, equipment, account power,
  or a persistent economy.
- Every specialization receives one direct, Siege-only crowd-clear action in place
  of the PvP trinket: Discipline `Dawn Nova`, Shadow `Umbral Collapse`, Fire `Flame
  Surge`, Frost `Blizzard`, both Rogue specs `Blade Tempest`, and Marksmanship
  `Arrow Rain`. Their deterministic area outcomes remain Core-owned. `Blade
  Tempest` preserves the requested close-range Rogue role and is an original
  five-meter physical AoE, not a copied spell/name.
- Stable run, variant, lane, wave, spawn, participant, archetype, and objective
  identities plus deterministic scheduling, objective pressure, failure, and
  economy rules exist in Core/Application. A wave is not a resettable encounter and
  does not recycle its combat identities.
- Unity source composes the authored Ashen Stronghold central fortress and four
  lanes, a twelve-view pool for up to four defenders plus eight hostiles, variant
  selection, defender/enemy bots, wave/objective/supply HUD, results, replay, and
  leave. The mode-only area icons and Siege environment remain prototype art.
  Until a fresh same-revision rebuild, validation, and full test suites are recorded,
  older runnable/build evidence does not establish this redesigned Siege slice.

### Dungeon slice

- Four heroes—one local player plus bots—cross four sequential floors: a streamed
  traversal pack, an elite arena, a mechanics/miniboss encounter, and a Rift Warden
  boss floor. A party defeat retries the current floor rather than resetting the
  complete run.
- Stable floor/encounter/spawn identities and deterministic advance/retry/abandon
  rules exist in Core/Application.
- Unity composes combat population, route/Arena/Boss presentation selection, floor
  transitions, retry, HUD/results, replay, and leave. The slice deliberately reuses
  current prototype environments and enemies; it is a functional run, not yet a
  production-ready multi-floor tileset or content set.

### Classes and specializations

The first demo has four classes and seven pre-match specializations. Its combat
decisions intentionally follow Wrath-era arena logic: Mana or Energy, a shared
global cooldown, visible casts and channels, spell-school interruption, dispels,
breakable control, diminishing returns, absorbs, target-bound combo points,
stealth access, poisons, roots, and defensive timing. Cooldowns and numeric output
are compressed for a short prototype round; the relationships and counterplay are
the fidelity target.

| Class | Specialization | Combat identity |
|---|---|---|
| Priest | Discipline | Preventive healing, dual-purpose Penance, dispels, defensive cooldown timing, and fear peel. |
| Priest | Shadow | Sustained Shadow pressure, dispel punishment, mana denial, healing reduction, control, and a high-commitment defensive form. |
| Mage | Fire | Casted burst, instant pressure, a dispellable periodic bomb, disorient, and area knockback. |
| Mage | Frost | Slows, roots, frozen-target burst, controlled stun, absorb defense, and kiting. |
| Rogue | Shadow Dance | Stealth-window burst, positional openers, interrupts, target-bound combo builders, and physical finishers. |
| Rogue | Mutilate | Poison setup, efficient builders, Nature-damage finishers, control, stealth reset, and magic defense. |
| Hunter | Marksmanship | Ranged dead-zone management, Sting and Chimera choices, aimed pressure, traps, kiting, and explicit companion commands. |

Healing must create recoverable fights without making stalemates. If 1v1 is later
enabled, Discipline uses a dedicated duel ruleset with dampening or remains out of
that queue until the matchup is demonstrably fair.

### Playable kits

Arena, Hero Siege, Dungeon, and the legacy Boss prototype keep ten controls visible
in a deliberate `2 + 4 + 4` hierarchy. The first two are large gesture families:
each is one compact circular control divided into a center-tap primary and four
directional regions for explicitly bound flick alternatives. Four medium common
buttons contain the main attacks or heals and
never expose swipe directions. Four small utilities contain cooldowns, defensive
or support decisions, and Arena Trinket; they also never expose swipe directions.
Both unified gesture discs remain fully visible at rest and retain the same size
through press, selection, cooldown, and blocked states. Only bound or learned sectors
are drawn; below-icon labels, rim, and highlight communicate held state without
rearranging the learned spatial map. A shared dark backing, layered perimeter rings,
and four restrained cardinal ticks make each family read as one control rather than
five adjacent circular buttons.
At the 1920×1080 reference canvas, each grouped disc is 301.6 pixels (the complete
232-pixel local family at 1.3× scale), each common action is a 132-pixel circle,
and each utility is a 92-pixel circle. Their displayed art is 62.4, 82, and 56 pixels
respectively; every ability face keeps circular hit geometry. The large controls'
captions, directional icons and touch areas grow together; flick recognition still
uses the existing screen-pixel threshold rather than requiring longer swipes.
The four common actions sit in a lowered, near-tangent arc around the two gesture
discs rather than a detached upper row; their touch faces, transient rings, and
shadows remain disjoint at 16:9, 20:9, and 2.4:1. The enlarged gesture pair and common
arc have spaced anchors, while bottom-center utility sizes and anchors remain unchanged.
This exposes at most 18 actions without pages or a dense grid. The player
selects exactly one focused unit by tapping its compact HP circle. Every friendly,
hostile, or contextual unit spell obeys that same focus.

Training uses a learned-only seven-action subset of the same authored positions.
Three are usable at level 1; the first four packs reveal one additional action
each. It has no directional alternates while teaching those actions. Completing
the final pack and one flick lesson bridges into Arena, where the full ten-control,
18-action specialization kit is available.

Hero Siege retains each specialization's overall ten-control hierarchy but
replaces the PvP-only Arena Trinket slot with its direct Siege area action: `Dawn
Nova`, `Umbral Collapse`, `Flame Surge`, `Blizzard`, `Blade Tempest`, or `Arrow
Rain` as mapped above. Those bindings exist only in Hero Siege; Arena, Dungeon,
Training, and legacy Boss identities, balance, and input remain unchanged.

| Spec | Ten-control / 18-action kit | Intended decisions |
|---|---|---|
| Discipline | Gesture: Renew (up Prayer of Mending, down Flash Heal, left Power Word: Shield, right Pain Suppression); Psychic Scream (up Mana Burn, down Shadow Word: Pain, left Mass Dispel, right Fear Ward). Common: Penance, Smite, Shadow Word: Death, Dispel Magic. Utilities: Shadowfiend, Inner Focus, Power Infusion, Arena Trinket. | Choose immediate, periodic, or next-hit healing; contribute Holy or Shadow damage; pre-ward Fear; trade damage for mana pressure; use single or area dispel; and align Power Infusion or Inner Focus with a planned cast window. |
| Shadow | Gesture: Devouring Plague (up Shadow Word: Death, down Mana Burn, left Shadow Word: Pain, right Mind Blast); Psychic Scream (up Silence, down Psychic Horror, left Fade, right Dispersion). Common: Vampiric Touch, Mind Flay, Power Word: Shield, Dispel Magic. Utilities: Shadowfiend, Mass Dispel, Shadowform, Arena Trinket. | Maintain Shadow pressure while retaining control, mana denial, single and area dispels, self-protection, and a high-commitment defensive. Vampiric Embrace remains a passive prototype simplification. |
| Fire | Gesture: Dragon's Breath (up Polymorph, down Frost Nova, left Cone of Cold, right Blast Wave); Mana Shield (up Spellsteal, down Molten Armor, left Ice Block, right Blink). Common: Fireball, Fire Blast, Living Bomb, Scorch. Utilities: Counterspell, Pyroblast, Combustion, Arena Trinket. | Select quick or committed casts; group close-range peel; steal a setup buff; choose emergency absorb or immunity; and align burst with control. |
| Frost | Gesture: Polymorph (up Cone of Cold, down Water Elemental: Freeze, left Frost Nova, right Deep Freeze); Ice Barrier (up Mana Shield, down Ice Block, left Blink, right Cold Snap). Common: Frostbolt, Ice Lance, Counterspell, Spellsteal. Utilities: Icy Veins, Mage Armor, Ice Armor, Arena Trinket. | Establish Frozen from self or targeted roots, exploit it with Lance or Deep Freeze, layer defense, steal buffs, switch exclusive armors, and spend Cold Snap only after valuable Frost cooldowns. |
| Shadow Dance | Gesture: Cheap Shot (up Garrote, down Gouge, left Blind, right Kidney Shot); Shadowstep (up Sprint, down Vanish, left Evasion, right Stealth). Common: Hemorrhage, Ambush, Eviscerate, Kick. Utilities: Cloak of Shadows, Shadow Dance, Preparation, Arena Trinket. | Choose damage, bleed, or control from stealth; keep basic out-of-combat Stealth distinct from combat Vanish; pair mobility with escape; deny casts; and use Preparation after spending mobility or defense cooldowns. |
| Mutilate | Gesture: Kidney Shot (up Cheap Shot, down Gouge, left Blind, right Garrote); Vanish (up Cloak of Shadows, down Evasion, left Sprint, right Stealth). Common: Mutilate, Envenom, Shiv, Eviscerate. Utilities: Kick, Cold Blood, Preparation, Arena Trinket. | Retain poison/combo pressure with basic Stealth, combat Vanish, control, magic defense, mobility, interrupt, and cooldown-reset decisions. |
| Marksmanship | Gesture: Serpent Sting (up Kill Shot, down Scorpid Sting, left Tranquilizing Shot, right Concussive Shot); Scatter Shot (up Freezing Arrow, down Frost Trap, left Silencing Shot, right Disengage). Common: Chimera Shot, Aimed Shot, Arcane Shot, Steady Shot. Utilities: Pet Attack, Rapid Fire, Deterrence, Arena Trinket. | Hold the ranged band, purge buffs, combine Sting and Chimera choices, retain a readable cast-time shot, set up traps and control, command the companion, kite, and time burst or defense. Extended Hunter commands remain implemented outside this normalized kit. |

Stable `AbilityId` definitions for excluded prototype actions remain intact, but
they are not members of the active specialization kit. Core applies that same
authorization to humans, bots, touch, and desktop input; an omitted action is not a
hidden shortcut or additional balance obligation in full-kit modes. Training may
still authorize one explicitly through its separate seven-action learning kit.

The first intended discussions are Shield versus dispel timing, Pain Suppression
versus switching targets, Fear or Polymorph diminishing returns, Living Bomb
dispels, Frost Nova into Deep Freeze or Ice Lance, Shadow Dance into Ambush, and
Shiv or Mutilate into Envenom, dispelling Vampiric Touch, Shadow DoT versus mana
pressure, Sting choice into Chimera, trap setup, ranged dead-zone control, and pet
peel. Knockback remains deliberately shared with the Arena and PvE layers: Blast
Wave can create lava pressure, while the legacy Boss tests Blink, Shadowstep,
Disengage, positioning, and ally peel.

## Camera and controls

- Landscape, elevated perspective camera that follows the human or spectated
  ally. Arena and the middle Dungeon floors use a closer combat composition;
  Hero Siege uses a lane-following Solo composition or a higher fortress-centered
  Four Lanes composition; the legacy Boss and Dungeon boss use a wider
  objective-safe composition; Training and the first Dungeon floor use eastward
  look-ahead for the streamed route. Arena opponents and PvE objectives stay toward
  the top of the screen.
- Horizontal empty-world drag orbits camera yaw. Q/E and the gamepad right stick
  provide desktop equivalents. Pitch, distance, FOV, follow target, and mode-safe
  look-ahead remain authored, so orbit does not become free pan or gameplay input.
- Left thumb moves. Six enlarged combat controls form one dense right-thumb arc;
  four smaller utilities sit in a symmetric bottom-center strip.
- Tap a hero or compact HP circle to set the one authoritative focus. No nearby
  unit is substituted when that focus is the wrong relation, out of range, or
  behind cover; the button instead states the failed requirement. Sessions begin
  without a focus, and there is no cycle-target control.
- Tap any button for its primary action. Only the two large gesture buttons accept
  a quick cardinal flick for an explicitly configured alternate; the first threshold
  crossing locks the gesture until release. Unbound directions, slow swipes, and
  moved holds cancel; they never fall back to the primary. Each configured Up,
  Down, Left, or Right binding has a persistent cardinal marker. Holding a grouped
  button opens one circular four-sector wheel with the bound icons and selected
  direction; it does not paint four rectangular description chips. Direct and
  utility buttons never show swipe cues. Every
  specialization fills every cardinal direction on both gesture controls.
- Off-screen enemies retain bounded edge awareness, with active casts prioritized.
  Stealth hides position, live health, casts, and targetability but preserves an
  obscured roster token so the known team composition remains truthful.
- Blink resolves from the movement-stick direction, falling back to retained
  character facing when the stick is neutral. Shadowstep uses the explicitly
  focused hostile unit and resolves behind it. Self and caster-area actions need
  no focus.
- The player can move, select a target, cast, and orbit with separately owned touch
  IDs. A short world press may select; crossing the horizontal drag threshold owns
  camera orbit and can never become a target tap on release.
- Enemy and boss actions use world telegraphs, cast feedback, and audio cues. Important information never depends on color alone.
- Conditional abilities show the missing tactical setup before secondary spatial
  blockers: for example, Deep Freeze reads `NEEDS FROZEN`, then changes to range
  or line-of-sight guidance after Frozen is actually present.

## Combat rules

- Combat uses health, cooldowns, cast times, resources where useful, and named status effects.
- Knockback is a fixed effect authored per ability or encounter action. It is not inherited Rigidbody momentum and is never randomized.
- The authoritative simulation validates range, line of sight, target legality, cooldown, resource cost, damage, healing, status, displacement, and death.
- Players retain a meaningful response to displacement through steering, terrain, class mobility, or ally rescue.
- A death recap identifies the decisive damage, control, knockback, and hazard contribution.
- Random critical hits, long stun chains, hidden hazards, and pay-to-win statistics are excluded from the first milestone.

## Session and progression boundaries

The current source boundary has local class/spec selection, Arena, guided Training,
continuous 12-wave Hero Siege, four-floor Dungeon, the legacy Boss compatibility
prototype, result flow, replay, pause/settings, and leave. Each active session uses
one local fixed-capacity deterministic simulation with one human and the bots
selected by its mode or Siege variant; the Solo Lane Siege variant has no bots.
Current automation does not substitute for a fresh generated build, an uncoached
full run, or physical-device evidence.

A version-one device-local profile persists settings, per-spec Training
completion/skip state, and stable mastery/cosmetic identifiers. Reduced motion and
high contrast are applied to the current camera/HUD; the haptics toggle gates a
basic supported-mobile result vibration. PII-free JSONL records cover session start,
Training steps/unlocks, and finish/outcome with bounded rotation. These local
services do not provide accounts, cloud saves, persistent rewards, an account
economy, or analytics upload. They do not persist Hero Siege's run-local gold or
potion inventory.

The project still has no shared online world, backend, matchmaking, cross-zone
travel, quest log, account-level economy, equipment store, persistent inventory,
respawning farm loop, or item drops. Hero Siege's gold and potions are deliberately
session-local supplies. World actors and combat state remain one local
fixed-capacity simulation rather than being streamed, replicated, or persisted by
chunk.

An anonymous private four-player MPS/Relay create/join/leave lifecycle and pure
host-authority protocol contracts exist, but they do not transport combat or make
Arena online. Until live-service, multi-process, replication, reconciliation,
adverse-network, and remote-device gates pass, every playable mode is labeled local.

Items may be explored only after combat validates. The intended future loop is fast session completion and collection, with builds formed by choices and sidegrades. Persistent power must not undermine normalized PvP.

## Validation gates

The milestone is successful only when playtests support all of the following:

- A new player identifies allies, enemies, current target, health, danger zones, and the immediate objective without verbal coaching.
- By the end of Training or the second Arena match, the player can intentionally
  select a target, change focus, use a recovery ability, and resolve one intended
  directional flick.
- At least 80% of test deaths are correctly attributed to an enemy action, positioning mistake, or hazard before the recap is explained.
- Knockback victims can state what warned them and name at least one possible response.
- Arena testers voluntarily request or start a fifth round; Training testers reach
  the next eastward landmark, understand each unlock, and either complete or
  deliberately skip the tutorial without verbal coaching.
- Hero Siege testers understand their selected lane layout, track overlapping wave
  pressure and fortress health, distinguish the nine regular enemy archetypes, spend
  gold on an appropriate HP/mana consumable, and deliberately use an area tool;
  Dungeon testers identify the current floor, retry versus advance state, and final
  objective.
- Players spontaneously discuss a class combination, counterplay, rescue, interrupt, or positioning plan.
- A loss feels deserved, funny, or educational rather than arbitrary.

Failed gates require changes to controls, camera, targeting, combat timing, telegraphs, recovery, maps, bots, or recaps. They do not justify adding classes, arenas, bosses, loot, or progression.

## Performance and quality gates

- First validate the best-quality High presentation on a physical iQOO Neo11,
  recording actual resolution and sustained performance; do not silently lower
  quality to pass. No physical validation has yet passed.
- For later compatibility, target a stable 60 FPS at a declared 1080p-class render resolution on the
  user-selected minimum tier: one performance tier below iQOO Neo11. The working
  Snapdragon 8 Gen 3 / Adreno 750 interpretation and physical-device acceptance
  boundary are recorded in `Docs/DEMO_READINESS.md`; this is not a measured pass.
- Complete ten consecutive sessions without lost touch input, unhandled exceptions, unbounded pooled objects, or material memory growth.
- Before any online claim, a host plus the required remote clients must finish,
  replay, and leave the admitted mode without divergent health, position,
  cooldown, status, or result state.
- Before any online claim, simulated latency, loss, reordering, and duplication
  must not permit duplicate casts, invalid hits, client-authored displacement, or
  conflicting winners. Current protocol/authority contracts do not pass this gate.
- Accessibility checks include non-color target/team signals, scalable safe-area HUD layouts, and distinct desaturated spell silhouettes.

## Roadmap

The authoritative phase plan and external gates are in
`Docs/ROADMAP_ARENA_FIRST.md`.

1. **Beta MVP:** make Arena plus recommended/skippable Training truthful,
   repeatable, locally observable, and usable across all seven specializations.
   The runnable Hero Siege and Dungeon slices may remain labeled prototype previews;
   they are not Beta MVP exit blockers. Selected Mixamo bases are permitted in
   embedded builds; exact hashes/import settings and raw-file distribution rules
   remain validated.
2. **MVP:** polish Arena replay and promote the bounded local Hero Siege and
   four-floor Dungeon slices only after Siege passes continuous-wave, lane, supply,
   HUD, result, soak, physical-device, and uncoached play gates and Dungeon passes
   its floor/retry equivalents. An externally distributed MVP must also clear every
   asset license, provenance, technical/art acceptance, and build guard.
3. **Beta MLP:** add a production-ready arena rotation and authored Dungeon
   variety, customize and optimize the licensed Mixamo character bases within a
   coherent art line, and validate retention, mastery, cosmetics, and
   physical-device quality. Commissioned replacements remain optional. All added assets still require art direction,
   provenance, optimization, and legal acceptance.

MMO world simulation, accounts, matchmaking, backend persistence, loot economy,
and large raids are exclusions from these three phases. Online Arena is a separate
proof gate, not implied by any phase name.
