# Fortress Architecture Benchmark

## Purpose

This is the acceptance contract for the first Blender-authored arena-architecture
benchmark. It replaces the prototype perimeter dressing while preserving the
existing flat arena layout, lava rules, bounded strategy-camera contract, and
mobile render budget. Review the result in the actual gameplay frame; a Blender
beauty render is not evidence that the benchmark passes.

The reference in `VISUAL_TARGET.md` remains a composition and finish target. The
2026-09-01 `v8` target-oblique capture was explicitly rejected as too low quality;
it is historical evidence, not an accepted implementation benchmark. The current
source pass replaces its false normal maps, weak masonry source, flat architecture,
reflection gap, soft upscaling, and no-post baseline. Current High-tier macOS-player
captures exist at the authored default and both bounded Arena yaw endpoints, but
they are review evidence rather than physical-device or final-art acceptance.
Passing this benchmark does not establish final-art parity while characters,
animation, spell effects, and HUD remain separate prototype-quality systems.

Passing this benchmark is still a prototype environment milestone. It does not
promote the current Arena, its generated look-development target, or any individual
material/lighting improvement to final-quality environment art.

## Blender source and export contract

- Use Blender meters: one Blender meter equals one Unity unit.
- Apply object rotation and scale before export. Exported roots use scale `1,1,1`
  and zero rotation.
- Export FBX with `-Z Forward`, `+Y Up`, applied unit conversion, and stable ASCII
  object and material-slot names.
- Triangulate the exported meshes. Reject n-gons, visible non-manifold faces,
  inverted normals, negative scale, duplicate coplanar faces, and z-fighting.
- Use solid wall geometry where both the arena-facing and outer faces can be seen.
  Backface holes or featureless black planes are not acceptable.
- Use custom split/weighted normals and visible bevels of approximately
  `0.06-0.12 m`. Do not spend geometry on silhouette details thinner than
  `0.08 m`; they do not survive the default 1080p render scale.
- UV0 uses one shared atlas or trim-sheet convention at a consistent source density
  of approximately `128-256 px/m`. Overlap is allowed only for intentional trim,
  repetition, or mirrored details. UV2 is not required by the current real-time
  lighting path.
- FBXs contain no Collider, Rigidbody, camera, light, armature, animation, or
  embedded Unity material dependency.
- Keep the editable `.blend` source outside `Assets/` so Unity does not require
  Blender to import the project. Keep consumed FBXs under the tracked Session RPG
  source-art path with their `.meta` files.

The intended ring composition is one continuous recessed foundation, twelve wall
modules, and six tower modules. Four camera-reviewed standalone braziers occupy
the left flank, left shoulder, right shoulder, and right flank as explicit fire
anchors. They reuse the same stone and ember materials; their stone bodies may cast
and receive only the main directional shadow, while their flame particles and
local point lights remain shadow-free. They stay wholly outside the `21.25 m`
presentation boundary and may not imply playable ground. A per-ember-submesh
property block gives each one an asynchronous warm pulse without material
instances. Each brazier also owns one bounded deterministic `FirePlume`; local
lighting remains a separate fixed-budget contract. The foundation is one joined
Stone-only draw at identity under `ArenaPerimeterDressing`; it is visual-only and
never a gameplay proxy.

## Geometry and render budgets

The executable `MobileQualityContracts.High` values are authoritative for the
current strongest-smartphone review target. Its normal four-player limit is
`240,000` rendered triangles, `160` batches, and `110` SetPass calls. These are
planning limits, not measured device results; Balanced and Performance retain their
lower fallback budgets.

| Asset or assembly | Maximum LOD0 triangles |
|---|---:|
| Wall module | `1,400` |
| Tower with joined brazier | `3,200` |
| Standalone fire-anchor brazier | `650` |
| Wardstone visual | `1,500` |
| Continuous recessed foundation | `8,000` |
| Instantiated fortress ring | `45,000` |
| Complete arena environment | `65,000` |
| Complete Ember Vault environment | `65,000` |

If production LODs are added and their runtime selection is verified, the complete
fortress may use at most `55,000 / 30,000 / 14,000` triangles for LOD0/LOD1/LOD2.
Without that proof, the `45,000`-triangle no-LOD limit applies.

The instantiated Arena fortress composition is additionally limited to:

- twenty-three MeshRenderers: one foundation, eighteen connected bays, and four fire
  anchors;
- thirty-three submesh draws;
- two unique shared perimeter materials;
- two material slots per renderer; and
- fewer than `65,535` vertices in every individual mesh.

The expected one single-slot foundation renderer, twelve single-slot wall
renderers, six two-slot tower renderers, and four two-slot brazier renderers meet
the renderer and submesh limits. Count the instantiated assembly, not only the
unique source FBXs. Materials must be shared; do not create per-instance material
copies.

Fortress kit `1.4.0` currently records `1,388` triangles for the straight wall,
`1,276` for the broken wall, `3,192` for the tower, `534` for the standalone
brazier, and `6,492` for the foundation. Twelve walls plus six towers total
`35,136`; the foundation raises that to `41,628`, and the four standalone fire
anchors produce a complete Arena perimeter of `43,764` triangles. These values
remain below the stated budgets but still require runtime triangle/draw-call and
physical-device proof after import.

## Ember Vault first-party reuse contract

Ember Vault now reuses the same first-party carved-floor and fortress source kit as
Rift Crucible instead of presenting the KayKit Dungeon environment at runtime. This
is a coherent prototype art baseline, not a claim that either battleground has
reached final-quality environment production.

The generated Ember Vault presentation must retain all of these properties:

- `RaidFloorVisual` references the tracked `ArenaFloorVisual` source mesh and its
  three shared floor materials, scaled uniformly from the Arena's `15 m` safe
  radius to the boss encounter's `13 m` safe radius. Boss/recovery markings remain
  thin readable inlays rather than filled primitive platforms.
- `RaidPerimeterDressing` uses one scaled first-party foundation, eighteen
  first-party fortress bays, twelve wall modules, and six towers on the far side of
  the authored Boss composition. It remains presentation-only and contains no
  Collider or Rigidbody.
- The runtime Ember Vault perimeter contains no KayKit-named object, renderer,
  component, dungeon floor, wall, pillar, stair, or rubble dependency. This removal
  is scoped to Ember Vault environment presentation; retained third-party packages
  and non-environment character/animation uses remain governed by their own intake
  and validation contracts.
- Two first-party shoulder braziers at radius `19.4 m` and angles `-32` and `+32`
  degrees are the two authored ember landmarks. Each uses one bounded pulse and one
  unshadowed point light; do not add more local lights to simulate polish.
- The complete static Ember Vault environment remains at or below `65,000`
  triangles and reuses the existing shared floor, masonry, ember, lava, abyss, and
  atmosphere material language.

Review Ember Vault with the authored Boss composition: field of view `50` degrees,
pitch `56` degrees, authored default yaw `0` degrees, follow distance `22 m`, pivot
height `0.9 m`, forward focus offset `2.3 m`, and no right focus offset. Runtime Boss
orbit uses a HUD-aware center of `15` degrees with a `50`-degree offset, clamping
across the wrap to `325..65` degrees. Capture the same stable party-and-boss state at
yaw `0`, yaw `325`, and yaw `65`. The asymmetric arc preserves more rotation toward
the visually clear side while stopping before the boss enters the right-side ability
cluster. Full `360`-degree orbit is deferred here for the same occlusion, silhouette,
and mobile-budget reasons as Arena.

## Unity 6 import contract

Apply these settings through tracked editor authoring or validation code, not
through an unrecorded Inspector fix:

- Scale Factor `1`, Convert Units enabled, and Bake Axis Conversion enabled.
- Rig/animation, blend shapes, cameras, lights, visibility animation, constraints,
  and animated custom properties disabled.
- Material Creation Mode `None`; remap slots explicitly to tracked shared URP
  materials.
- Read/Write disabled and Generate Colliders disabled.
- Keep Quads disabled; Weld Vertices and full mesh optimization enabled.
- Mesh Compression disabled for benchmark review. Consider `Low` only after a
  side-by-side capture proves that bevels and silhouettes do not change.
- Generate secondary UVs disabled.
- Import authored normals and calculate MikkTSpace tangents. The single shared
  fortress material uses one tangent-space normal map, so every source mesh keeps
  the same bounded tangent vertex stream.
- Use automatic/16-bit indices and fail validation if one mesh exceeds the 16-bit
  vertex limit.

Use dedicated shared fortress stone/ember presentation. `RuinStone` is now a
`TECH_REVIEW` runtime candidate based on Poly Haven Stone Brick Wall 001
(`POLYHAVEN-2026-001`), a retained CC0 third-party scanned material. It binds the
untouched 2K diffuse and OpenGL/+Y normal sources plus the deterministic 1K
`FortressMasonryMask.png` derivative. Unity imports diffuse and normal at a 2K
maximum and the mask at 1K, with mipmaps, repeat wrapping, trilinear filtering,
anisotropy `8`, and Android ASTC 4x4 compression at quality `100`; diffuse is sRGB
while normal and mask are linear data.

The shared material uses neutral-warm default tint `(0.78, 0.76, 0.76)`, base,
normal, mask, and occlusion tiling `2.65`, offset `(0.31, 0.18)`, normal strength
`0.92`, occlusion strength `1`, and smoothness multiplier `0.72`. The mask packs R
metallic `0`, G ambient occlusion, B a retained displacement reference not sampled
by URP Lit, and A smoothness clamped to `0.02..0.42`; masonry therefore remains
nonmetallic. Per-module property blocks preserve the deterministic five-value
cycle (`0.90..1.00`) and small `±0.008` warmth variation around the neutral masonry
base `(0.82, 0.81, 0.80)`; fire structures use `(0.82, 0.70, 0.58)`. `LavaSeam`
now supplies a Lit emissive flame slot so the existing fire presenter can drive
`_EmissionColor`; `RuinStone` keeps its emission map empty.

The two generated wardstone visuals bind this same RuinStone PBR stack instead of a
flat prototype material. This is a presentation upgrade only: their centers,
footprints, visual height, and Core collision/LOS contract remain unchanged.

`Tools/Art/Blender/prepare_fortress_pbr_maps.py` validates the retained source
hashes before producing the packed mask. Exact inputs and the derivative hash live
in `SourceArt/Blender/Environment/Textures/FortressMasonryPbr.manifest.json`.
Neither the CC0 license nor the richer scan establishes target-camera coherence,
stylized final-art acceptance, or physical Android texture/shader performance.

The arena-floor Lit stack maps its macro albedo, explicit OpenGL/+Y macro normal,
and phase-baked macro mask once across the 30 m source UV. Keep macro-normal
strength at `0.78`. Generation `3.1.0` multiplies phase-coherent micro AO by macro
joint/cavity AO, keeps metallic at zero, and maps macro smoothness to approximately
`0.141..0.315`; the material smoothness multiplier is `1.0`.

Fine surface relief retains the independent micro transform at `4.6875` tiling and
`(0.47125, 0.64125)` offset. The first-party detail albedo is bound with strength
`0.055` as linear data, and the OpenGL/+Y detail normal uses strength `0.38`. Keep
the cool-neutral tint `(0.90, 0.90, 0.94)`. The explicit macro normal and
phase-correct packed mask
replace the former byte-identical height-to-normal workaround and empty PBR slots;
they add no renderer or draw call. Exact source hashes, channel ranges, and phase
proof live in `ArenaFloorVisual.manifest.json`.

## Gameplay and framing invariants

The architecture is presentation-only:

- Safe-stone radius remains `15.0 m`.
- Outer kill radius remains `20.5 m`.
- Every fortress vertex, after instance transforms, has planar radius
  `sqrt(x*x + z*z) >= 21.25 m`.
- The continuous foundation stays at identity with radial bounds
  `21.25-25.5 m` and vertical bounds `-1.8..-0.45 m`. Its sloped, broken shoulder
  must not read as reachable ground, and it adds no collider, gameplay proxy, or
  hazard surface.
- The eighteen authored bays use an `18` degree phase offset. This puts masonry
  shoulders behind the side lava band while keeping the tall inner towers on the
  far `+X` arc.
- `ArenaArchitecturalDepth` adds the bounded boundary stonework, an irregular
  two-material outer basalt crust of exactly `612` triangles, and off-floor flank
  silhouettes. All remain presentation-only on Ignore Raycast with no collider,
  Rigidbody, or change to the safe/hazard topology.
- The four fire anchors are `FortressBrazier_LeftFlank` at radius `22.0 m` and
  angle `30` degrees, `FortressBrazier_LeftShoulder` at radius `21.9 m` and angle
  `54` degrees, `FortressBrazier_RightShoulder` at radius `21.9 m` and angle `126`
  degrees, and `FortressBrazier_RightFlank` at radius `22.0 m` and angle `150`
  degrees. All use base height `-0.90 m` and local scale `(0.9, 1.37, 0.9)`. At
  the default Arena yaw, their readable fire cores frame the fortress without
  competing with the central combat read. The lens deliberately permits peripheral
  fortress stone and flame extremities to crop; combatants, wardstones, target
  markers, and the readable fire core may not be hidden by that crop. Every vertex
  stays outside the `21.25 m` presentation boundary.
- Each Arena brazier has one deterministic, local-space, collision-free,
  shadow-free `FirePlume`: at most `16` particles, emission `18/s`, lifetime
  `0.55-1.05 s`, start speed `0.40-0.95 m/s`, and start size `0.40-0.70 m`.
  A tighter `FireCore` adds at most `10` particles and `EmberSparks` adds at most
  `12`; the arena-wide cinder veil is separately capped at `24`.
- Wall foundations sink deterministically by `0.72-0.84 m`, standard far towers by
  `0.72 m`, and far bays `02` and `08` by `0.78 m`. Ground-origin modules therefore
  meet or penetrate the lava plane instead of floating above it. These offsets are
  presentation-only.
- Keep X/Z scale at `1` so the eighteen bays remain connected. Y scale rises from
  the near arc to the far silhouette. In one-based bay notation, walls `10/18`,
  `11/17`, `12/16`, `13/15`, and `14` use Y scales `0.60`, `0.54`, `0.48`, `0.43`,
  and `0.40`; far walls `01/09` use `1.06` and the far keystone at `05` uses
  `1.22`. Towers `02/08`, `03/07`, and `04/06` use `0.72`, `1.00`, and `1.16`.
  This camera-authored scale profile exposes constructed vertical planes above the
  recessed foundation without changing any gameplay or planar bounds.
- Prefer an inner wall edge of `21.25-21.7 m`, ring center of `22.45 m`, and
  outer silhouette no farther than `26.0 m`. The visual lava extends to `28.5 m`:
  the Core-owned `20.5 m` outer-kill radius plus an `8.0 m` presentation margin.
  Its slow `0.22` pulse moves between base tints `(0.82, 0.42, 0.20)` and
  `(1.00, 0.58, 0.26)` and HDR surface-emission colors `(0.55, 0.32, 0.12)` and
  `(2.80, 1.30, 0.38)` at texture tiling `1.7`. Albedo and emission share one
  phase-coherent transform. The thin fissure overlay pulses only between
  `(0.003, 0, 0)` and `(0.012, 0.001, 0)` so the lava reads as a surface rather
  than radial neon spokes. This changes only the rendered moat, never the Core
  hazard boundary.
- No bridge, ledge, stair, floor, or foundation may imply safe or reachable ground
  outside the authoritative stone.
- Place the complete hierarchy under `Visuals` on layer 2/Ignore Raycast. It must
  add nothing to `GameplayProxies`, `FlatGround`, or `HazardSurfaces`.
- Do not change the wardstone centers `(0, -3.8)` and `(0, +3.8)`, gameplay radius
  `1.05 m`, visual height `2.35 m`, or capsule height `2.8 m`. A replacement
  wardstone may extend upward, but not outside the authoritative XZ footprint.

Review Arena only with the authored perspective camera: field of view `46` degrees,
pitch `40` degrees, center yaw `90` degrees, follow distance `18 m`, pivot height
`0.9 m`, forward focus offset `2.3 m`, and right focus offset `0 m`. Runtime
Arena orbit is clamped to `30..150` degrees (`90° ± 60°`). Capture the same stable
composition at yaw `90`, yaw `30`, and yaw `150`; do not tune Arena art against a
separate beauty camera. Full `360`-degree review and runtime orbit are deferred until
the complete circumference passes the same occlusion, silhouette, and mobile-budget
contract. Other modes retain their separately authored distance, field of view,
center yaw, and bounded orbit; Arena values must not leak into them.

At the authored default yaw the camera views toward world `+X`, so keep the
near/west `-X` arc low or broken at roughly `0.8-1.8 m`. The far/east `+X` wall may
reach `3-4 m`, with major towers up to `5-6 m`. The `30`- and `150`-degree endpoints
also expose the north/south shoulders, which must retain readable planes without
becoming foreground occluders. At all three review yaws, check center framing and
the real follow positions: `(-5.7, -4.5)`, `(-5.7, +4.5)`, `(-14, 0)`, `(14, 0)`,
`(0, -14)`, and `(0, 14)`. Architecture may never hide the controlled character,
relevant wardstone, or lava landing risk at those positions.

Only the directional key may emit a shadow map. Major walls, towers, and the four
Arena brazier stone bodies may cast and receive it; flame particles and incidental
small props do not. Arena brazier and spell point lights remain unshadowed. High
uses per-pixel additional lights with an authoritative cap of four additional
lights affecting one object. HDR/ACES and restrained post may improve separation,
but the benchmark may not use grading, bloom, or vignette to conceal weak material
or silhouette construction.

The fixed lighting comparison uses one directional key at pitch `35`, yaw `-35`,
color `(0.86, 0.88, 0.94)`, intensity `1.95`, soft shadows, and shadow strength
`0.86`. Trilight ambient uses sky `(0.14, 0.15, 0.19)`, warm equator
`(0.22, 0.13, 0.09)`, ground `(0.04, 0.045, 0.06)`, and intensity `0.80`.
Exponential-squared fog uses color `(0.042, 0.024, 0.029)` and density `0.0065`.
The cool overhead key, warm vertical fill, and warm distance falloff are deliberate:
they retain a quiet combat floor while revealing masonry planes and separating the
lava/fire perimeter from blue-black shadow. Empty camera corners use the linear
solid color `(0.003, 0.0045, 0.010)`. The oversized textured abyss backdrop uses an
unlit `(0.018, 0.012, 0.014)` multiplier so ambient changes cannot expose a flat
grey plane at either Arena orbit endpoint.

Four unshadowed point lights align one-to-one with the four Arena braziers. Each
uses color `(1.0, 0.36, 0.08)`, intensity `9.5`, range `8.25 m`, and per-pixel
High evaluation. Their smaller overlapping pools remain local under the
four-lights-per-object cap. Each fire anchor applies a `2.4` emission multiplier
through its reused ember-slot property block. Do not compensate with fortress
emission, more lights, renderers, or stronger post-processing.

Lit floor and fortress renderers use simple reflection-probe sampling against the
generated 64 px `ArenaEnvironmentReflection` cubemap at intensity `0.50` and one
bounce. The global HDR `ArenaEnvironmentLook` profile applies ACES, post exposure
`0.42`, contrast `+4`, filter `(1.0, 0.99, 0.975)`, saturation `-2`, temperature
`+1`, tint `0`, and high-quality half-resolution bloom (`0.18` intensity, `0.90`
threshold, `0.55` scatter, clamp `8`, five iterations). Vignette intensity is
`0.065`; the camera also enables dithering and NaN rejection. There is no SSAO
renderer feature or screen-space reflection.

Shadow distance belongs to the mobile quality contract, not the legacy built-in
quality dropdown. Use `24 m`, `32 m`, and `40 m` for Performance, Balanced, and
High respectively. Performance keeps one 1024 hard-shadow main-light cascade,
per-vertex additional lighting capped at one per object, and LDR. Balanced uses a
2048 soft-shadow map with two cascades split at `0.60`, per-pixel additional lights
capped at two, and HDR. High is the default and uses a 4096 soft-shadow map with two
cascades split at `0.55`, per-pixel additional lights capped at four, and HDR. The
`0.78` Performance and `0.85` Balanced render scales use FSR with authored sharpness
`0.72`; High renders at native scale `1.0` with 4x MSAA. No additional light casts a
shadow.
The closer Arena composition changes the exact camera-to-wardstone distance as the
follow target moves, so do not use the retired `24.8-26.1 m` range as an acceptance
gate. The `40 m` High default must include both opening cover pieces, their
roughly `3.36 m` projected shadows, and all four opening combatants without adding
another shadow-casting light or renderer feature.

## Manual capture acceptance

Capture the normal gameplay frame at `16:9`, `20:9`, and `2.4:1`, using the HUD and
camera required by `visual-review-matrix.json`. The bounded Arena review set must
include the authored `90`-degree default and the `30`- and `150`-degree endpoints in
the same stable gameplay state. Also inspect the desaturated review frame and a
near-edge follow position.

The current High-tier macOS-player evidence includes default-yaw frames at
`build/review/arena-high-v19-yaw90-2400x1080.png` and
`build/review/arena-high-final-qhd-yaw90-3200x1440.png`, plus 2.4:1 endpoint frames at
`build/review/arena-high-ultrawide-yaw30-2592x1080.png` and
`build/review/arena-high-ultrawide-yaw150-2592x1080.png`. The 16:9 case, each yaw at
each aspect, desaturated/near-edge/worst-case combat frames, explicit user approval,
and physical Android profiling remain open.

Capture Ember Vault at the authored Boss yaw `0` and bounded endpoints `325` and
`65`, with the same living party/boss state. Both ember landmarks, the carved floor,
boss/recovery inlays, relevant floor edge, boss, and party must remain readable.

Reject the benchmark if any capture shows:

- featureless black wedges or large perimeter masses without readable planes and
  bevel response;
- inverted faces, missing backs, visible z-fighting, floating modules, or
  disconnected wall construction;
- lava represented only by thin angular emissive lines that resemble debug marks or
  hostile telegraphs;
- architecture covering the stone-to-lava boundary or eliminating the readable
  lava gap;
- a bridge, ledge, stair, or foundation that looks playable despite having no Core
  collision contract;
- a banner, wall, or tower hiding a combatant, wardstone, feet, target marker, or
  relevant safe edge;
- new architecture occupying more than approximately eight percent of the lower
  frame before the HUD;
- fewer than two readable tower silhouettes or fewer than two substantially
  complete brazier bodies and flames in the 16:9 spawn frame;
- any KayKit Dungeon environment geometry visible in Ember Vault, fewer than two
  Ember Vault shoulder landmarks, or Boss-orbit occlusion of the party, boss,
  inlays, or relevant floor edge;
- emissive marks that can be mistaken for ability telegraphs; or
- renderer, submesh, material, triangle, or bounds totals above this document's
  limits.

A passing frame keeps a continuous lava band visible in at least three arena
quadrants, presents stone, trim/cloth, and fire as separate value layers, and keeps
the combat floor quiet enough to track teams, feet, cover, target state, and lava in
under one second.
