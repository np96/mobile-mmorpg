# PvE Leveling — local grinding prototype

This mode implements the approved no-quest direction independently of guided
Training. Select **PvE Leveling** on the home screen, choose a specialization,
and enter Sunbreak Basin. It is solo and device-local; it does not connect to the
separate server prototype or claim online co-op.

## The loop

Explore → approach a camp → fight → earn XP per kill → grow stronger → return to
cleared camps or choose another clearing. There are no quests, dialogue, victory timer,
finite pack checklist, equipment drops, or quest rewards.

Seven camps use the existing eight enemy archetypes in different compositions:

| Camp | Enemy level | Combat identity |
|---|---:|---|
| Cinder Outriders | 1 | Melee raider with ranged marksman |
| Frostbound Watch | 2 | Frost control with melee pressure |
| Broken Gate Guard | 3 | Brute with shielding warder |
| Ashen Conclave | 4 | Hexer and shadow caster |
| Ossuary Marauders | 5 | Three-enemy melee/ranged/caster mix |
| Reliquary Sentinels | 7 | Veteran warder and brute |
| Rift Warden | 9 | Optional elite with readable area attacks |

Camps activate within 22 meters. Only one camp and at most three hostiles are
active at once; this is a bounded actor-lifetime prototype, not a populated open
world. Cleared camps wait at least 20 seconds and require the player to leave a
26-meter radius before they can activate again. Respawn is triggered on approach,
not a sudden appearance under a stationary player.

Enemies leash within 45 meters of their camp. Crossing that limit makes them return
home; nearby survivors remain in Core and keep their existing visible models and
identities. The camp retires without rewards only when the player is outside the
leash and every living survivor is more than 60 meters away. This can briefly delay
the next camp because the one-camp/three-hostile cap remains. Refuge entry is still
an explicit immediate encounter reset. Returning obeys the respawn cooldown. Enemy
identities are unique for the simulation lifetime; the same death cannot award XP twice.

## Progression and recovery

The independent prototype curve spans levels 1–10 with cumulative XP thresholds
`0 / 160 / 420 / 800 / 1320 / 2000 / 2860 / 3920 / 5200 / 6720`.
Each level adds 12 maximum health and 8 percentage points of base damage;
level-up restores health/resource. This is an initial tuning curve, not final
balance. Stronger camps pay more XP. At level 10, combat remains available but XP
does not exceed the cap.

The existing full specialization kits remain available. This pass does not add
talents, new abilities, equipment, or progressive ability unlocks. Training's
separate 1–6 curve and learned-only kit are unchanged; Arena remains normalized.

Sunbreak Refuge restores health/resource within its 10 m radius when no camp is active. Death returns the
player to that refuge with earned XP and level retained. There is no item/XP loss.
Death begins a fresh combat simulation; unfinished enemy health/status is not saved.
Re-entering also starts at the refuge, not at a saved arbitrary combat position.

XP is saved separately for each of the seven specialization selections on this
device, under `Application.persistentDataPath/pve-characters`. That is an explicit
prototype character-selection model, not a finalized multi-character account model.
No XP is copied into the local server's account/world records.

Writes occur after the authoritative event batch, outside the simulation tick,
using flushed temporary files and atomic replacement. Corruption, unsupported
versions and XP rollback fail closed instead of resetting the file. A failed save
pauses play and retains pending XP in memory while retrying at bounded intervals.
Closing the process before an unsuccessful save is repaired can lose that pending
XP. Checksums detect damage; they are not cheat protection or cloud backup.

## Location and presentation

PvE now uses **Sunbreak Basin**, world `1`, zone `2`, content version `2`: a separate 192 × 192 m
presentation footprint containing 180 × 180 m of bounded, flat playable ground.
Nine 64 m chunks form a 3 × 3 square. Training and Dungeon's first floor retain
the seven-chunk Ashen Causeway road in zone `1`.

Seven camps are distributed across both axes, with no gates or required camp
order. Six large rocky/ruined shelves and seven camp-side groves create alternate
approaches, loops, and line-of-sight cover. These thirteen landmarks each use three
unequal overlapping circles (39 collision components), forming irregular solid
unions rather than individual round bases. Every component contains its landmark
anchor, so the union cannot enclose a hidden walkable pocket. `PveRegionLayout` is
the single Core source for bounds, refuge, camp positions, component footprints,
and stable landmark grouping. Conservative landmark bounds are authoring metadata,
not solid geometry. Movement, Blink, knockback, targeting and NPC detours respect
the same component unions; Unity meshes and physics do not decide them. This is
authored planar topology, not generated terrain, a heightfield, walkable elevation,
or a general navigation mesh. Version `2` changes the nine chunk fingerprints;
camp/refuge coordinates, stable world/zone identities and saved XP are unchanged.
The large shelves now sit 19–27 m from their associated camp centers, placing
terrain alongside ordinary combat views; camp `0` keeps its more open entry.
Every camp retains a full nine-meter actor-clear combat disk and multiple approaches.

| Camp / landmark | X | Z |
|---|---:|---:|
| Sunbreak Refuge | -46 | -42 |
| Cinder Outriders | -10 | -38 |
| Frostbound Watch | -35 | 12 |
| Broken Gate Guard | 38 | -24 |
| Ashen Conclave | 12 | 45 |
| Ossuary Marauders | 77 | 6 |
| Reliquary Sentinels | 60 | 63 |
| Rift Warden | 98 | 96 |

The new recipe uses sunlit meadow surfaces, irregular worn camp plazas,
low vegetation and textured rock/ruin/tree groups. Raised solids stay inside
Core obstruction footprints or outside playable bounds. Chunks remain
presentation-only, with a 65,000-triangle cap and no new global lights or physics.
The streamer loads radius `1`, retains radius `2`, and caps basin residency at
nine chunks; it can retain the complete small basin after exploration.

PvE Leveling has its own camera: yaw 70°, pitch 20°, distance 18 m, forward look
6 m, right offset 2.5 m, FOV 58°, and full 360° yaw for all camp approaches. Its
400 m far plane and daylight sky reveal the independent low-detail vista;
leaving the mode restores the prior lens clearing/far settings. Training retains
its prior camera. Presentation chunks stay bounded and combat geometry remains Core-owned.
This is an in-engine environment revision, not production environment-art acceptance.
The original dark/sparse version and the subsequent road layout were rejected.
The old daylight revision remains historical in `PVE_LOCATION_DAYLIGHT_EVIDENCE.md`;
the basin layout revision is tracked in `PVE_BASIN_EVIDENCE.md`. The user also
rejected its toy-like graphics. The subsequent naturalistic surface/geometry
candidate is tracked in `PVE_NATURAL_ART_EVIDENCE.md`: textured organic ground,
individual-leaf trees, eroded rock shoulders and smaller weathered masonry.
The version-2 compound-landmark revision now has its own generated, automated and
actual-player evidence in `PVE_REALISM_EVIDENCE.md`, including scanned grass/dirt,
rock and bark surfaces, low soil banks, broken masonry and the lower 37° camera.
It remains an incremental environment pass: sparse ground, rounded larger relief,
prototype foliage and stylized characters/boss do not meet the user's realism bar.
Top-center HUD occlusion of distant actors remains a separate presentation caveat;
this location change does not redesign the HUD or imply user art approval.

The 2026-09-07 [Frostbound courtyard revision](PVE_COURTYARD_EVIDENCE.md) adds
connected broken paving, an elevated-window façade, a ruined watchtower and a
lower eroded foundation to the camp-one benchmark. Grass/earth transitions are
less uniformly mixed, and the bare earth is locally desaturated without lowering
scene exposure. The closer 35° camera increases actor scale; other modes retain
their camera. Walkable detail stays below 10 cm and every raised ruin remains
inside existing Core cover. This is still not target-art or phone acceptance.

That courtyard result was rejected. The current
[measured-asset vertical slice](PVE_VERTICAL_SLICE_EVIDENCE.md) replaces its focal
ruins with a licensed stone fort kit, introduces source-derived tree/fern/grass
geometry and removes the rejected courtyard paving from the runtime composition.
The lower camera, sky and two-batch distant horizon provide location depth.
This is a benchmark pocket, not a replacement of every camp or acceptance of the
target-art/physical-phone gates. Its fresh checks are recorded separately.

## Acceptance boundary

Fresh evidence is recorded in `PVE_LEVELING_EVIDENCE.md`. Required checks include
all seven specialization entry paths, correct new-mode routing, authoritative kill
XP, death/re-entry persistence, safe respawn, bounded population, no timer ending,
save corruption handling, and warm Core/Application allocation checks.

Device profiling, full 1–10 class balance, uncoached playtests, procedural terrain,
inventory, durable world-state changes, cloud saves and two-player networking are
separate gates. The next gameplay iteration should follow playtesting of this
grinding loop before adding more progression systems.
