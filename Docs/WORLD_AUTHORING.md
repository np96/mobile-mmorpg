# World Authoring

This document defines the source, generation, runtime, and verification contract for
Session RPG world zones. The current implementation streams bounded presentation
content. It does not stream authoritative actors and is not a persistent or
networked MMO world.

## Data flow

```text
Tools/Agent/world-zones.json
  -> Tools/Agent/Worlds/<Zone>/zone.json
  -> SessionRpgAuthoringGenerator rebuild
  -> Assets/SessionRpg/Settings/WorldZoneCatalog.asset
  -> Assets/SessionRpg/Content/Worlds/<Zone>/Chunks/Chunk_<x>_<z>.unity
  -> ProjectSettings/EditorBuildSettings.asset
  -> WorldZoneStreamController built-in additive loading
```

The two JSON layers and supported Editor authoring recipes are source. The runtime
catalog, chunk scenes, and build-scene list are generated and tracked. Never edit a
generated chunk scene, `WorldZoneCatalog.asset`, or build-scene YAML by hand.

## Identity and coordinates

- `WorldId` and `ZoneId` are explicit non-zero stable integers. A
  `WorldChunkCoordinate` is a signed integer `(x, z)` cell within that zone.
- `WorldChunkKey` combines world, zone, and coordinate. It is the stable identity
  used by Core/Application planning; Unity scene names and GameObject names are not
  identities.
- Every chunk is exactly `64` meters on X and Z. `WorldPosition` combines a chunk
  key with chunk-local planar coordinates normalized to `[0, 64)`, plus height.
  Crossing a positive or negative boundary changes the integer chunk coordinate
  without losing local precision.
- Runtime lookup and activation require the complete `(worldId, zoneId)` pair, so
  zones in different worlds cannot be confused even when their zone numbers match.
- Each generated catalog entry and scene root carries a deterministic SHA-256
  source fingerprint covering zone identity/version/streaming settings and the
  chunk path, coordinate, generation seed, biome, and POIs. Validation and runtime
  loading reject stale or mismatched generated content.

## Current Ashen Causeway slice

The guided Training zone is world `1`, zone `1`, key
`ashen_causeway`, content version `3`. It is a single east-west row:

PvE Leveling now uses the separate Sunbreak Basin below. It does not change
Training's five-pack encounters. The causeway presentation revision is `3`: daylight, dedicated
world materials, flat broken paving, shoulder masonry/vegetation, and district
landmarks supplement the two retained off-route ruin silhouettes. The darker
revision 2 was rejected during visual review; see
[daylight revision evidence](PVE_LOCATION_DAYLIGHT_EVIDENCE.md).

| Chunk | Authored POI recipe | Generated scene |
|---|---|---|
| `(-3, 0)` | `west_refuge` | `Chunk_-3_0.unity` |
| `(-2, 0)` | `cinder_approach` | `Chunk_-2_0.unity` |
| `(-1, 0)` | `broken_gate` | `Chunk_-1_0.unity` |
| `(0, 0)` | `collapsed_court` | `Chunk_0_0.unity` |
| `(1, 0)` | `ossuary_bend` | `Chunk_1_0.unity` |
| `(2, 0)` | `reliquary_yard` | `Chunk_2_0.unity` |
| `(3, 0)` | `east_watch` | `Chunk_3_0.unity` |

The seven chunks span approximately 448 meters. Every listed POI now has bespoke
retained landmark geometry, forming a complete local route from a refuge through
approach, gate, court, ossuary, and reliquary staging to the watch finale. The
authoritative playable route is
the narrower Core rectangle from X `-191` through `255`, centered at Z `32`.
Five gated packs contain ten lifetime mobs and award five levels, taking the player
from level 1 to 6.

The encounter route and stable composition are explicit:

| Pack | District | Activation X -> encounter X | Level | Stable mob IDs and archetypes | Pack XP |
|---|---|---:|---:|---|---:|
| 1 | West Refuge | `-184 -> -162` | 1 | `2` Ashbound Raider; `3` Cinder Marksman | 80 |
| 2 | Cinder Approach | `-120 -> -98` | 2 | `4` Causeway Prelate; `5` Rimebound Frostweaver | 120 |
| 3 | Broken Gate | `-56 -> -34` | 3 | `6` Ashbound Brute; `7` Reliquary Warder | 180 |
| 4 | Ossuary Bend | `76 -> 98` | 4 | `8` Ashbound Raider; `9` Cinder Marksman; `10` Cinder Hexer | 300 |
| 5 | East Watch | `184 -> 206` | 5 | `11` Rift Warden | 450 |

These ten mobs bind all eight hostile archetypes; Raider and Marksman deliberately
return in pack four as familiar anchors. Collapsed Court is a breathing district,
and Reliquary Yard stages the Rift Warden finale.

All chunks use biome recipe `ashen_causeway_volcanic`. The seven authored POI
recipes are still prototype environment compositions, not evidence of final
Diablo/PoE production art.

## Zone manifest contract

### Sunbreak Basin — separate PvE topology

World `1`, zone `2`, key `sunbreak_basin`, content version `2`, uses all nine
coordinates `(x,z)` with both axes `-1..1`. Each 64 m chunk uses biome
`sunbreak_basin` and POI `sunbreak_basin_clearing`. The presentation footprint is
192 × 192 m. Load/retain radii remain `1`/`2`, with a nine-chunk resident cap.

`PveRegionLayout` supplies Core-owned playable bounds `[-58,122]` on both axes,
seven scattered camps, the southwest refuge, and thirteen compound landmarks
(six major rock shelves plus seven camp-side groves). Each solid footprint is the
union of three unequal overlapping circles: 39 components overall. Every circle
contains its landmark anchor, so each union is star-convex and cannot enclose a
walkable pocket. `GetLandmark` supplies a conservative generation bound;
`GetLandmarkObstacleStart`/`GetLandmarkObstacleCount` and
`GetObstacleLandmarkIndex` keep landmark identity separate from component indexing.
Those bounds are not collision disks. The builder uses the actual component union
for raised terrain, ruins and trees. All raised solids stay inside that union or
beyond the playable perimeter; flat meadow/plaza surfaces and low plants do not
add collision. This is a fully authored flat region, not procedural terrain or a
general-purpose navigation graph. See `PVE_LEVELING_MODE.md`.

The six major anchors sit along camp rims, 19–27 m from their associated camp
centers. They frame camps `1,2,3,6,5,4` respectively;
camp `0` retains an open entry from refuge. Their Core `(X,Z)` anchors in landmark
order are `(-16,5)`, `(57,-40)`, `(29,27)`, `(101,71)`, `(78,45)`, `(95,-12)`.
Every camp retains a complete nine-meter combat disk clear of all components at
the combatant collision radius. The four perimeter routes and loops remain open.

Version `2` identifies the compound-landmark presentation revision. It changes all
nine chunk fingerprints through the existing versioned manifest contract; world,
zone and chunk identities, camp/refuge coordinates, and per-specialization local
XP are unchanged. Old generated catalog/chunk fingerprints must be rebuilt
together and are not accepted as evidence for this revision.

`SunbreakBasinAuthoring` owns this separate recipe and its validation. It never
calls the causeway-floor builder, so the basin is not a widened or decorated road.
Both recipes preserve presentation-only chunk ownership and the 65,000-triangle cap.

### Shared schema

`Tools/Agent/world-zones.json` is schema version `1` and lists catalog membership:

- `worldId`, `zoneId`, and `zoneKey` must remain stable and unique.
- `definitionPath` must resolve below `Tools/Agent/Worlds/` and end in
  `/zone.json`.
- Catalog identity must exactly match the referenced zone definition.

A schema version `1` zone definition contains stable identity/display fields,
`contentVersion`, streaming values, and its complete chunk list. Validation enforces:

- `chunkSizeMeters` is exactly `64`.
- `loadRadius` is non-negative and `retainRadius` is strictly greater, providing
  hysteresis. Both are capped at `8` so one zone cannot request an unbounded local
  residency neighborhood.
- `maxLoadedChunks` is between `1` and `15`, does not exceed authored chunk count,
  and can contain the largest load-radius neighborhood in the authored footprint.
- Coordinate `(0, 0)` exists and all chunks form one four-neighbor-connected
  footprint.
- A zone contains at most `1024` chunks. Larger worlds must be partitioned into
  bounded zones or indexed regions rather than one linearly scanned catalog.
- Coordinates, case-insensitive scene paths, and positive generation seeds are
  unique. Coordinates stay within `+/-1,000,000` for bounded distance arithmetic.
- Every scene path is below `Assets/SessionRpg/Content/Worlds/` and ends with the
  coordinate-derived `Chunk_<x>_<z>.unity` filename.
- `biomeRecipeId` and every unique `poiRecipeId` are lowercase stable ASCII tokens.
  `poiRecipeIds` must be an array, including when empty.

The generator has a fail-closed biome/POI registry. `west_refuge`,
`cinder_approach`, `broken_gate`, `collapsed_court`, `ossuary_bend`,
`reliquary_yard`, and `east_watch` belong to `ashen_causeway_volcanic`;
`sunbreak_basin_clearing` belongs to `sunbreak_basin`. Unknown tokens and
cross-biome recipe combinations fail rebuild and validation.

The schema accepts data only after structural and semantic validation. A valid
manifest still needs an explicit authoring recipe. The two supported biomes each
have a dedicated builder/validator; adding JSON alone is insufficient.

## Generated chunk contract

Each generated scene has exactly one root carrying `WorldChunkPresentation` with
the manifest world, zone, coordinate, and source fingerprint. Its visual child is
positioned within the 64-meter cell. The repeated causeway floor uses matching
endpoint profiles and no terminal faces at internal cell boundaries, avoiding the
width step and doubled end-cap seam of the original standalone segment. The chunk
must not contain any of these:

- `Camera`, `AudioListener`, or `EventSystem`;
- `PrototypeGameController` or `NetworkManager`;
- `Collider` or `Rigidbody`.

The rebuild also checks each biome's required visual hierarchy, catalog parity,
and enabled build-scene order. That order is Bootstrap first, followed by every
manifest chunk in stable catalog/coordinate order. The build pipelines consume all
enabled scenes rather than a hard-coded one-scene list.

The static-triangle budget is enforced after the selected POI recipe strips all
unused landmark geometry, so it measures the generated chunk that actually ships.
Presentation limits currently applied per chunk include at most one enabled
unshadowed point light, deterministic particle seeds, and at most 32 particles per
particle system. These are authoring safeguards, not measured Android performance.

`WorldRegionDetailAuthoring` generates a bounded near-field detail group for each
district: at most 24 mesh renderers and 8,000 triangles. Raised detail bounds stay
outside visual-local Z `+/-8.15` (the Core route half-width is 7.55 m) and inside
visual-local X `+/-27`, including rotated/scaled bounds. Original chipped masonry
and opaque vegetation use the dedicated world palette; retained CC0 KayKit wooden
supplies preserve their source atlas UVs and unmodified imports. None supplies
physics or gameplay obstruction. `WorldPavingDetailAuthoring` adds only flat,
non-colliding dressed-stone inlays at Y <= 0.041 m; the complete generated chunk
still obeys the 65,000-triangle limit. Generated detail meshes are source-driven,
not Inspector-authored assets.

`WorldLightingPresenter` temporarily reuses the Bootstrap directional key, supplies
an explicit ambient probe and distant fog, and restores the exact entry state when
leaving the world. It also covers Training and the Dungeon's world-route floor;
Arena, Boss and Siege keep their own baseline. It adds no light, post-processing
profile, per-frame update or additional shadow map. Ambient fill is reasserted on
additive scene loading, not every frame.

The 2026-09-07 Sunbreak benchmark uses a distinct PvE key/shadow response and a
cached 32²-face daylight reflection cubemap. Training and Dungeon traversal retain
their previous look; leaving PvE restores reflection mode/map/intensity/bounces
as well as the prior key/ambient/fog state. The cache is generated once per
presenter lifetime and destroyed with its owner. It is an analytic environment,
not a captured HDRI or a realtime probe. See
[realism benchmark evidence](PVE_REALISM_BENCHMARK_EVIDENCE.md).

The same benchmark admits exactly one scanned rock03 mesh at Frostbound Watch
(camp 1 / landmark 7). `SunbreakScannedRockAuthoring` owns hash-locked model-only
imports and its dedicated measured material; it never instantiates the full
six-rock set. The 5,000-triangle visual stays inside the actual Core cover and
does not add physics. The unchanged 65,000-triangle/80-renderer chunk gate still
applies. Source intake `POLYHAVEN-2026-003` remains `TECH_REVIEW`.

The subsequent [Frostbound courtyard revision](PVE_COURTYARD_EVIDENCE.md) adds an
editor-only `SunbreakCourtyardGeometry` builder: seeded, adjoining convex slabs
and small displaced chips, three opaque material groups, at most 2,500 triangles,
and at most 10 cm height. Complete pieces are emitted only inside their 64 m
owner chunk; shared clipping planes prevent overlap. It adds no collider or
runtime update. The two new ruin meshes stay inside landmark 0/7's exact Core
circle unions and use elevated windows with filled lower courses, not deceptive
walk-through entrances. Only landmark 0 changes from a tall shelf to a low
eroded earth foundation. Tests preserve the other major shelf profiles.

Ground uses independent grass/earth tint and ground-only, luminance-preserving
earth saturation, with domain-warped world masks and narrower blend transitions.
The shader retains seven ground texture samples and the measured normal and
roughness inputs; it adds no invented height/AO or global grading. Non-ground
materials disable/reset the ground layer controls.

The [measured-asset vertical slice](PVE_VERTICAL_SLICE_EVIDENCE.md) supersedes
the rejected camp-1 courtyard composition. Selected `SunbreakFortAssets` and
`SunbreakVegetationAssets` parts replace the focal primitive groups while keeping
the low banks and one scanned rock. Imported meshes retain their source shape;
move or uniformly scale a placement that fails exact Core bounds, never clamp
its source vertices. The old courtyard builder remains historical source.

`SunbreakVistaAuthoring` supplies a separate always-resident visual underlay and
mountain horizon and woodland: 27,042 triangles, four renderers, no physics/cameras/lights, and
all raised terrain outside the playable rectangle. It is not an extra streamed
chunk or gameplay terrain. The Bootstrap lighting presenter activates it and its
sky only for Sunbreak, restores prior sky/environment on exit, and performs no
per-frame work. Near-chunk load/retain/cap rules are unchanged. The isolated Unity
wrapper copies the exact `SourceArt/Environment/SunbreakVegetationV1` source
buffers for hash validation; it does not copy unrelated licensed DCC archives.

The [2026-09-08 ruin composition](PVE_RUIN_COMPOSITION_EVIDENCE.md) uses nine
source fort pieces, three trees and eight ferns in the focal chunk, replacing its
distant prototype meadow budget. `SunbreakRuinGroundAuthoring` builds one broad,
fractured court below 0.012 m with source-scale world UVs and missing-cell earth
pockets. This is no authoritative elevation change. Vista's three varying crests
retain exact outside-Core placement; its cap is 12,000 triangles. Sunbreak fog
density is 0.0035 to preserve their contrast. Camera and streaming remain unchanged.

The [subsequent woodland candidate](PVE_WOODLAND_POLISH_EVIDENCE.md) replaces
the focal three tree canopies at the same 9,328 triangles/tree, adds two ferns,
and replaces middle-distance groves 2/9 with four additional source-derived trees.
The V1 source delivery stays immutable; a separate reproducible V2 representation
expands complete paired clusters inside the original envelope. Exact Core triangle
checks, generated source-frame parity, and the unchanged per-chunk caps apply.
The independent Vista adds 84 low-detail 3D branch/cluster-card trees in 14 groves,
all wholly outside Core, rooted to actual terrain triangles. Its new resident cap
is 28,000 triangles / four renderers; no additional streamed chunks are admitted.
A first 176-tree version was rejected in-game for thin, isolated crowns. The
revision spends that same geometry budget on lower, wider, overlapping foliage.
Landmark 2 also adopts the low eroded bank instead of its former raised plinth;
its exact Core cover and the flat gameplay floor are unchanged.
A dedicated rock/grass material consumes authored altitude/slope vertex weights
instead of sampling the near ground's clamped world mask outside its domain.
Its Vista-only continuous world projection replaces per-triangle axis selection
after the latter exposed triangular shading islands at yaw135. Signed whiteout
normal blending retains the underlying geometric normal; Forward/DepthNormals
share it. Two layers × three projections × three measured maps cost 18 texture
fetches per shaded fragment, rather than six. Near ground sampling is unchanged.
The foreground wall stack is replaced by a genuine admitted corner from the
same immutable FBX, matching scale/base and real stonework sections. The focal
fort composition at that revision was seven instances / 7,924 triangles; the intact source
still lacks the visual target's ruin damage.
These are authored rendering bounds, not measured phone-performance acceptance.

The later [graphics/creep-retirement pass](PVE_GRAPHICS_AND_CREEP_FIX_EVIDENCE.md)
replaces that foreground pair with one explicitly modified, clipped/capped
`BrokenAbbeyMasonry` batch and three source-derived offcuts. Original admitted
meshes remain immutable; the derivative has its own 1,276-triangle ceiling and
exact Core-footprint/joint tests. Short whole-source grass colonies replace the
very sparse focal scatter; fewer repeated perimeter props fund their geometry.
The continuous boundary bank receives shared smooth lighting normals without
changing its positions, UV phase or collision. Vista retains its existing
27,042-triangle/four-renderer topology but uses five independent world-space
ridgelines and low connecting saddles instead of concentric crest bands.
No streaming residency, Core navigation or runtime lighting ownership changes.

## Runtime behavior

`WorldZoneStreamController` lives on the persistent Bootstrap composition root. On
Training or Dungeon-floor-one entry, the game activates zone `1`; PvE Leveling
activates zone `2` at Sunbreak Refuge. It then supplies the authoritative Core
snapshot position as the visual focus. Leaving a world mode deactivates the zone
and unloads its owned additive scenes; switching between zones drains stale content.

For Ashen Causeway, the deterministic Application planner uses:

| Setting | Value | Meaning |
|---|---:|---|
| Load radius | `1` | Request authored chunks within one Chebyshev cell of focus. |
| Retain radius | `2` | Keep recently loaded chunks until focus is more than two cells away. |
| Maximum resident chunks | `7` | Hard planner/controller capacity for this zone. |

Load candidates are deterministic and nearest-first. The planner reuses fixed
buffers after construction. The Unity controller performs only one additive load or
unload operation at a time, verifies the loaded root identity, exposes readiness and
fault status, and safely discards work from an obsolete activation generation.
Disabling or destroying the controller drains both resident scenes and an in-flight
load so additive content cannot be orphaned with its Bootstrap owner gone.

Streaming readiness is presentation readiness only. It cannot activate a pack,
validate movement, decide collision, spawn an enemy, grant XP, or change combat.
Those outcomes remain deterministic Core/Application state even if a visual scene is
late or missing.

## Editing a zone safely

1. Run `Tools/Agent/unity.sh doctor` and make sure the Unity Editor is closed before
   generation.
2. Edit `Tools/Agent/world-zones.json` only for catalog membership. Edit the
   referenced per-zone `zone.json` for chunk coordinates, scene paths, stable seeds,
   and supported recipes.
3. Add or change the matching Editor authoring recipe before declaring a new biome,
   POI implementation, or second zone. Keep gameplay collision and encounter rules
   in Core/Application rather than adding physics to the chunk.
4. Update manifest, spatial, planner, generated-scene, and runtime-lifetime tests in
   the same change.
5. Run one `Tools/Agent/unity.sh rebuild`. It replaces every manifest-declared chunk,
   the runtime catalog, Bootstrap references, and build-scene membership.
6. Run `Tools/Agent/unity.sh validate`, then fresh EditMode and PlayMode suites.
7. Traverse every changed seam and POI in both directions. Confirm bounded
   residency, clean exit/re-entry, no visual holes or duplicate global systems, and
   final pixels at required aspect ratios.
8. Rebuild and profile Android after any world-content or residency-budget change.

Do not run a configurator repeatedly after failure. Read the first manifest or
authoring validator error, correct its source, then rebuild once.

## What must be built before calling this an MMO world

The next scaling work is not more decorative chunk copies. It is explicit systems
work, in this order:

1. Give every world spawn/actor a stable identity and deterministic ownership;
   partition activation, retirement, and save state without reusing combat IDs.
2. Generalize the authored rectangle/circle-union topology into versioned
   navigation facts that can be loaded independently of Unity presentation.
3. Add server-owned area-of-interest replication, reconnect/transition recovery,
   and capacity tests. A client must never decide residency of authoritative state.
4. Add persistence, world/instance lifecycle, migration/version rules, and backend
   observability.
5. Add remote content delivery and floating-origin support only when measured world
   size and precision require them.

Before expanding a single zone toward its `1024`-chunk ceiling, replace the current
catalog-wide runtime scans with a spatial chunk index. The ceiling is a safety bound,
not a recommended content shape or a claim that one built-in scene list is an MMO
delivery strategy.

Until those steps exist and pass network/device evidence, describe the feature as
"additive visual world streaming" or a "streamed local leveling zone," not as a
large MMO world.
