# Implementing the three-mode realism direction

On 2026-09-07 the user asked for the actual 3D game to approach the
[Leveling, Arena and Dungeon artworks](MODE_TARGETS_20260907.md). Those images are
the requested direction, not evidence of current engine quality. The aim is
believable materials, proportions and architecture, with clear daylight where
appropriate—not toy-like assets hidden behind darker grading.

## First benchmark: Sunbreak Basin

The first implementation pass is deliberately the existing open PvE grinding
region. It preserves all camps, progression, Core collision, flat walkable ground,
camera framing, and the completed 1.3× gesture controls.

- Replace sparse twig silhouettes with tapered trunks, branching crowns and
  denser individual-leaf sprays. Keep the geometry editor-generated, batched by
  the existing three tree materials, and free of runtime animation/allocation.
- Increase small ground tufts around irregular clearing margins. Keep them below
  the existing 18 cm walkable-detail limit so feet and combat warnings still read.
- Preserve the measured rock texture scale on steep rock shelves. Previously,
  normal smoothing also replaced cliff UVs with flat XZ projection, stretching
  texture detail down the face.
- Give ruins and paving a distinct measured masonry surface and real packed
  material AO/smoothness, using the already retained and licensed scan. Do not
  describe this as screen-space ambient occlusion or new photogrammetry geometry.
- Give PvE a cached daylight reflection environment and stronger sun-shadow
  definition. Restore the exact prior lighting when leaving; Training, Arena and
  Dungeon retain their existing presentation. Exposure is unchanged.
- Keep the 65,000-triangle/80-renderer per-chunk ceilings. Spend less geometry on
  distant ridge tessellation and tree count to fund the nearer detail.
- Begin real geometry replacement with one source-verified scanned rock at
  Frostbound Watch: rock03 from the CC0 Poly Haven Rock Moss Set 01, retaining
  source proportions and dedicated albedo/normal/roughness maps. It replaces a
  small local rubble pocket, not the full formation or biome. Final acceptance
  remains `TECH_REVIEW`; one prop does not establish a production-quality region.

Fresh build, test and actual-camera evidence belongs in
[the benchmark record](../PVE_REALISM_BENCHMARK_EVIDENCE.md). This pass is a
technical/art candidate, not a declaration that the target has been reached.

## Next production passes

The subsequent [Frostbound courtyard pass](../PVE_COURTYARD_EVIDENCE.md) builds
connected surviving paving, a foreground windowed ruin and a taller ruined
watchtower over a lower eroded foundation. It also separates grass from neutral
earth and changes only the PvE camera to 35° / 21.5 m. These are actual generated
3D changes; the original target images remain unchanged. This is still a visual
candidate, with procedural foliage, repeated material detail and a large
character/environment quality gap—not acceptance of the production-art gate.

The user rejected that courtyard result. The next
[measured-asset vertical slice](../PVE_VERTICAL_SLICE_EVIDENCE.md) replaces its
focal primitive ruins and foliage with a selected CC0 modular fort and
source-derived vegetation. Its lower 20° PvE camera reveals an independent
distant landscape and daylight sky. The old paving is retained as historical
source but is no longer instantiated in the benchmark clearing. This remains
one pocket under visual review; the rest of the region and other modes have not
received the same kit. The source documents must not call the prior rejected
frame accepted merely because its tests passed.

The [2026-09-08 composition revision](../PVE_RUIN_COMPOSITION_EVIDENCE.md) continues
that pocket with linked return walls, a third source tree, a broad fractured court
and an irregular, less fog-obscured mountain skyline. It reuses the same source
assets and trades distant meadow detail for near composition; no camera, HUD or
Core topology change is hidden in the art work. Its own captures/tests are the
verification record, not a promotion to production quality.

The [woodland polish revision](../PVE_WOODLAND_POLISH_EVIDENCE.md) addresses
the visibly porous source canopy, replaces two middle-distance prototype groves,
lowers one artificial-looking bank and adds outside-Core wooded foothills with
measured rock/grass transitions. Its first sparse distant-tree arrangement was
rejected from native frames; a second candidate concentrates the same budget into
denser stands. A matching-scale source corner replaces the foreground stack;
sharper far terrain and continuous Vista-only material projection remove a
subsequently discovered rotated-view shading defect. An opt-in live desktop
profiler now records a stationary baseline,
separately from frozen art captures. None of this closes phone, animation, combat
stress, stability or final visual acceptance.

The subsequent [graphics and creep-retirement pass](../PVE_GRAPHICS_AND_CREEP_FIX_EVIDENCE.md)
adds explicitly source-derived fractured foreground masonry and grounded offcuts,
independent distant ridgelines instead of radial mountain bands, and denser short
grass colonies. Native review rejected the first stair-like fracture profile and
prompted a collapsed central notch; continuous bank normals also replace visible
flat-shaded triangles. Grass remains subtle at the gameplay camera and broad
areas still need richer authored content. This is not production-art acceptance.

1. **Accept one playable benchmark.** Compare the same camp and camera before and
   after, then inspect other camps and aspect ratios. Review character motion,
   foot contact, foliage/telegraph overlap, texture scale and shadow readability
   on the target phone. Improve or replace the assets that fail this comparison.
2. **Arena architecture and surface pass.** Preserve the flat fighting footprint,
   two authoritative wardstones and lava boundary while upgrading constructed
   masonry, damaged edges, peripheral depth, props and localized lighting. Verify
   the default camera and both bounded orbit endpoints.
3. **Dedicated Dungeon environment.** The current four-floor mode reuses Training,
   Arena and Boss spaces. Build a coherent sanctum kit with encounter rooms,
   galleries and transitions; the artwork's complete vaulted room is not present
   in this pass. Any new gameplay obstacle needs a matching Core rule and tests.
4. **Character/enemy production quality.** Refine the selected licensed humanoid
   bases or replace them with approved original assets; improve silhouettes,
   outfits, PBR response, animations, deformation and LODs. Lighting alone cannot
   make the current boss proxy or repeated enemy models match the art.

Asset selection, provenance and technical acceptance follow the existing intake
ledger and production-character pipeline. No paid pack purchase, commission,
engine migration or cloud deployment is implied. A stronger look must retain
explicit lower render tiers; physical-device performance remains a separate gate.
