# Visual Quality Contract

## North star

Session RPG should read as a high-quality, grounded 3D mobile action RPG with MMO
arena decision density. The user's 2026-09-07 realism direction supersedes the
earlier chunky/toy-like environment target: believable proportions, detailed
physical materials, coherent architecture and vegetation, and cinematic location
depth. Immediate team/target/state clarity, responsive cast choreography, and
identifiable spell shapes remain mandatory in a busy 2v2 fight. It must not
imitate Warcraft or Mobile Legends assets, UI, characters, icons, animation,
sounds, or effects. Realism is not a reason to darken the scene until detail is lost.

The order of importance is:

1. Combat state and target readability.
2. Character grounding, facing, anticipation, and response.
3. Ability identity and resolution feedback.
4. Environment hierarchy and composition.
5. Decorative polish.

Do not hide a rules/readiness problem with more particles or content.

The camera-matched finish reference and its honest asset boundary live in
`Docs/Art/VISUAL_TARGET.md`. Review the in-engine frame against that target; do not
use the concept image itself as evidence that a visual feature exists in the game.
The Blender-authored perimeter must also satisfy the measurable source, import,
geometry, framing, and capture contract in
`Docs/Art/FORTRESS_BENCHMARK.md`.

The current three-mode direction is in `Docs/Art/MODE_TARGETS_20260907.md`.
`Docs/PVE_VERTICAL_SLICE_EVIDENCE.md` records the measured-asset Leveling trial.
Successful import/build/tests are technical gates, not visual acceptance.

## Character and team language

- A player, ally, enemy, focused target, controlled unit, dead unit, and stealthed
  opponent must differ by shape/state as well as color.
- Characters face their actual intent; their back must never present as forward.
- Locomotion owns planar movement. Root motion and animation never own gameplay
  altitude. Feet stay on the authored ground and knockback remains readable.
- Every cast has anticipation, active/held or channel motion where relevant,
  release, and recovery/cancel/interrupt response. Instant abilities still need a
  readable authored accent; projectile motion alone is insufficient.
- Priest, Mage, Rogue, Hunter, Hunter companion, and boss must be distinguishable
  by silhouette at gameplay distance before nameplates are read. Discipline and
  Shadow share a class silhouette but require unmistakable active spell/aura
  language.
- The Hunter companion must communicate Attack, Follow, Stay, Web, incapacitated,
  and dead states without pretending it is currently a directly targetable enemy
  combatant.

## Ability-button language

The ten controls must read as two large gesture actions, four medium common actions,
and four small utilities without making the combat view secondary. Only the two
gesture controls may show cardinal actions or the expanded radial guide. Their
resting state is one 301.6-pixel circular cluster: a 109.2-pixel center primary and four
transparent 78-pixel directional icon regions at 101.4-pixel offsets, all using 62.4-pixel
art. The complete family uses a stable 1.3× root scale over its original local
geometry, including captions and circular touch surface. One persistent 301.6-pixel
guide/background draws only sectors that have bound
actions; unlearned directions stay visually empty. Direction regions must not add
their own surfaces, bezels, accent rings, or frame art. Hold and selection may reveal
labels at their authored below-icon offsets and highlights, but never resize or
rearrange the cluster. One opaque dark circular backing, layered outer/highlight/accent
rings, and four small cardinal ticks provide the shared premium frame. Every added
cluster layer is presentation-only and must remain non-raycast.
The four common controls are 132-pixel circles with 82-pixel art, and the four
bottom-center utilities are 92-pixel circles with 56-pixel art. Every ability face
uses a circular hit filter. Pause is 64 pixels; active Siege supply actions are at
least 52 pixels tall and drawer purchases are 56 pixels tall. Physical size and
comfort still require acceptance-device measurement.
At the 1920×1080 reference canvas, the right-bottom anchored gesture centers are
`(-240, 292)` and `(-564, 292)`. The right-bottom anchored common centers are
`(-182, 526)`, `(-326, 535)`, `(-474, 537)`, and `(-676, 495)`; this arc leaves room
for the enlarged discs and outward flicks without changing the smaller button sizes.
Bottom-center utilities remain at
`(-156, 74)`, `(-52, 74)`, `(52, 74)`, and `(156, 74)`. The complete circular touch
faces, transient ready rings, and offset shadows must remain unclipped and mutually
disjoint at 16:9, 20:9, and 2.4:1.
Every class and specialization uses the same circular medallion construction rather
than a class-specific button shell: blackened-iron framing, restrained aged-brass
ornaments, a dark jewel well with an ability-color tint, and an integrated caption
plaque. Ability identity comes from the icon or glyph, caption, and restrained accent
color—not from changing the shared forged frame.

The shared frame is the authored first-party sprite at
`Assets/SessionRpg/Art/UI/Hud/AbilityMedallionFrame.png`, not a stack of decorative
procedural studs. It must remain one reused sprite across all slots and classes;
cooldown, readiness, lock, and interaction rings stay separate so state remains
dynamic and testable.

Authored icons and procedural glyphs retain their complete true-alpha silhouettes
inside the jewel well. Do not add a circular `Mask`/`RectMask2D`, crop the art to the
well, or bake a square background into the icon. The caption plaque belongs to the
medallion and must keep its short action label readable over both cool stone and hot
lava without obscuring the central silhouette.

Every ability in the bounded seven-specialization leveling progression must resolve
through `AbilityIconCatalog` to painted sprite art. Procedural glyphs remain an
explicit prototype fallback for arena-only actions; they are not acceptable as a
substitute inside the seven-slot leveling kits. The catalog coverage test binds this
rule to stable `AbilityId` values rather than HUD slot indices.

Training uses a learned-only seven-action subset. It starts with exactly three visible
actions, reveals one additional action after each of the first four packs, and shows
seven visible actions at level 5. Do not reserve future positions with empty or
locked circles, show future ability icons/captions, or present milestone preview
controls. Each learned action appears in its stable authored position only when its
unlock is earned. The final flick lesson may temporarily bridge to the full Arena
kit, but skipped Training never withholds Arena power.

Use one unambiguous visual layer for each state:

| State | Required treatment |
|---|---|
| Ready | full icon contrast; restrained ready accent only |
| Pressed/flick locked | immediate scale/rim response; selected alternate is explicit |
| Global cooldown | thin cyan radial and `GCD`; icon identity remains visible |
| Ability cooldown | dark radial plus final-second number |
| Missing setup/resource/range/LOS/facing | concise blocker on a still-tappable button |
| Casting/channeling | caster cast bar and button acknowledgement, not a fake cooldown |
| Hard-disabled/dead | desaturated and inert, distinct from temporary GCD |

Cooldown, GCD, cast time, and tactical blocker may coexist, but the next actionable
reason takes priority. A player must answer “why can I not cast this?” in under one
second. Icons must not rely on hue alone; silhouette/motif and label provide the
fallback. Verify physical targets on hardware (approximately 9 mm minimum), not
only Unity canvas pixels.

Current targeted PlayMode coverage verifies the shared medallion hierarchy and
authored layers, rebinds the same ten medallions across all seven playable
specializations, requires every playable ability to have an authored icon or
dedicated glyph, and exercises primary/alternate readiness and interaction states.
Fresh desktop-player captures of the current styling cover Hunter at 16:9, 20:9,
2.2:1, and 2.4:1 plus Discipline, Fire, and Shadow Dance at 2.2:1. This establishes
the intended final pixels and safe-area composition for the four class identities;
it does not replace physical-device reachability, target-size, and touch-comfort
evidence.

## Spell visual grammar

| Family | Shape/motion | Readability rule |
|---|---|---|
| Radiant/heal | ascending arcs, tether, soft concentric pulse | destination and amount timing are clear |
| Fire | comet/wedge, turbulent tail, hot core, expanding blast | projectile versus cone versus radial knockback differ in silhouette |
| Frost | crystal/shard, sharp ring, restrained trail | root, Frozen setup, absorb, and Deep Freeze resolution are distinct |
| Shadow | broken orbit, inward pull, disappearing veil | stealth/control never obscures selection rules |
| Physical | short directional slash/impact, minimal lingering glow | contact point and facing matter; no generic magic ball |
| Venom | narrow green cut, droplets/runes, brief residue | poison setup differs from direct physical impact |
| Hunter | taut trajectory, trap geometry, restrained wind streak | shot, Sting, trap, disengage, and pet command differ by silhouette, not projectile color |
| Companion | low ground pulse, web strands, order marker | pet-origin action and current Attack/Follow/Stay order remain legible without a second ability HUD |
| Arcane/utility | geometric warp/ring | teleport, interrupt, and dispel cannot look like damage hits |

Every accepted cast provides feedback, including a miss. Every rejection provides UI
feedback without spawning a successful world effect. Projectiles preserve ownership
and target direction. Avoid full-screen flashes, opaque ground coverage, and effects
that hide health, cast bars, wardstones, lava boundaries, or feet.

## Environment visual language

The current first-party environment baseline is the carved Arena floor plus the
Blender fortress foundation, wall, tower, brazier, masonry, ember, lava, and abyss
kit. Rift Crucible uses one foundation, twelve walls, six towers, and four
standalone braziers at Arena scale. Each Arena brazier owns one deterministic
collision-free and shadow-free plume capped at `16` particles, a tighter fire core
capped at `10`, and world-space ember sparks capped at `12`. A separate bounded
cinder veil supplies sparse depth motion. Ember Vault reuses the same tracked floor
source at boss scale and a separately composed first-party fortress ring with two
authored shoulder-brazier landmarks. Ember Vault must not instantiate or retain
KayKit Dungeon environment objects, renderers, components, or materials at runtime.
Ashen Stronghold uses the same accepted source kit in a distinct central-fortress
and four-cardinal-lane composition; it is not an Arena or Boss layout relabeled as
Siege.

This reuse removes the most visible style discontinuity between the Arena and Boss
spaces, but the 2026-09-01 `v8` Arena capture was explicitly rejected as too low
quality. It is historical evidence, not an accepted match for the v2 target. The
current High-tier authoring pass has macOS-player default and bounded-endpoint
captures listed in `Art/VISUAL_TARGET.md`. They are a new evaluation candidate;
user art approval, the missing 16:9 frame, the complete all-angle/all-aspect review,
and physical-device profiling remain mandatory.

The floor tint is `(0.90, 0.90, 0.94)`. Its surface uses a real OpenGL/+Y macro
normal at strength `0.78`, a generation-3.1 packed macro mask with phase-coherent
micro response plus macro joint/cavity AO, a linear detail-albedo layer at strength
`0.055`, and an independently tiled micro normal at strength `0.38`. Material
smoothness multiplier `1.0` is bounded by the packed mask rather than acting as a
constant glossy surface. The shared `RuinStone` candidate binds the CC0 Poly Haven
Stone Brick Wall 001 diffuse and OpenGL/+Y normal plus a deterministic AO/smoothness
mask, at tiling `2.65`, normal strength `0.92`, and smoothness multiplier `0.72`.
Instantiated masonry uses a neutral base `(0.82, 0.81, 0.80)` and fire structures
use `(0.82, 0.70, 0.58)`, with restrained deterministic value/warmth variation.
Both wardstones use this same RuinStone PBR response while retaining their exact
Core-owned footprints. This source is third-party scanned PBR data in `TECH_REVIEW`,
not original Session RPG texture art or production acceptance.

The visual lava extends `8 m` beyond the Core-owned `20.5 m` outer-kill radius and
uses one phase-coherent Lit albedo/emission transform at tiling `1.7`. It slowly
pulses between `(0.82, 0.42, 0.20)` and `(1.00, 0.58, 0.26)` with HDR surface
emission; the thin overlay fissure mesh is intentionally near-invisible so it cannot
read as a telegraph or neon spoke. The shared abyss backdrop is textured, unlit, and
tinted `(0.018, 0.012, 0.014)` so camera rotation falls into warm black rather than
a light-reactive grey plane. Fortress kit `1.4.0`, the 612-triangle irregular outer
basalt crust, and the flank silhouettes add constructed depth while preserving
visual-only gameplay boundaries. These changes still require physical-runtime
inspection for tiling, seams, scale, clutter, and telegraph readability.

## Arena composition

- Safe stone is cool/dark; lava is the hottest value and color; team accents and
  spell accents sit between them.
- Wardstones are gameplay cover, not decoration. Their collision footprint must be
  visually obvious and match Core geometry.
- Decorative props never suggest false collision, elevation, or safe ground.
- Central runes establish orientation without competing with ground telegraphs.
- Four standalone braziers frame the left/right flanks and shoulders. Their four
  layered deterministic fire effects are bounded as described above, and their four
  aligned unshadowed point lights use color `(1.0, 0.36, 0.08)`, intensity `9.5`,
  and range `8.25 m`. High evaluates additional lights per pixel with an executable
  four-lights-per-object cap.
- One soft-shadowed directional key at pitch `35`, yaw `-35`, color
  `(0.86, 0.88, 0.94)`, intensity `1.95`, and shadow strength `0.86` establishes
  floor relief. Trilight ambient uses sky `(0.14, 0.15, 0.19)`, equator
  `(0.22, 0.13, 0.09)`, ground `(0.04, 0.045, 0.06)`, and intensity `0.80`;
  exponential-squared fog uses `(0.042, 0.024, 0.029)` at density `0.0065`.
- Lit floor and fortress renderers sample the generated 64 px custom environment
  reflection at intensity `0.50`. High renders through HDR and ACES with post
  exposure `0.42`, contrast `+4`, and high-quality half-resolution bloom at
  intensity `0.18`. Restrained white balance, color filtering, vignette, and camera
  dithering complete the profile. It must reveal existing surface/shape work, not
  substitute for it; SSAO and screen-space reflections remain disabled.
- The elevated strategy camera must keep the human, current target, immediate
  threats, and relevant arena edge legible at its authored default angle and
  throughout the reviewed per-mode user-controlled orbit. Arena's target-oblique
  composition uses yaw `90`, pitch `40`, follow distance `18 m`, forward focus
  offset `2.3 m`, right focus offset `0 m`, and field of view `46` degrees. It
  clamps to `30..150` degrees (`90° ± 60°`); each other mode owns its own authored
  center and bound. Orbit never pans, collision-zooms, target-faces, or changes
  combat state. Arena and other bounded-orbit modes defer full `360`-degree orbit.
  Ashen Stronghold intentionally supports wrapped yaw for its four cardinal lanes,
  while full-heading occlusion, framing, and mobile-performance acceptance remains
  open. Off-screen warnings stay bounded and prioritize active hostile casts.

See `ARENA_AUTHORING.md` before changing geometry or environment composition.

## Ashen Stronghold composition

- The central fortress is the persistent objective and must remain visually
  identifiable while pressure overlaps across waves. Nothing may read as a boss
  dais, miniboss gate, or between-wave replacement arena.
- Four cardinal lanes must remain separable by silhouette and heading. Solo Lane
  follows the human along the east approach; Four Lanes frames the fortress and all
  four defender assignments. The inactive lanes in Solo remain presentation context,
  not hidden combat routes.
- Regular swarm and medium/elite archetypes need readable scale, badge, and casting
  differences without inventing a boss silhouette. Compact gold and HP/mana use
  controls must remain legible without covering fortress health or lane pressure;
  purchase controls stay in a closed-by-default drawer until Shop is pressed.
- The Solo camera defaults to yaw `90`, pitch `58`, distance `19 m`, forward focus
  `2.4 m`, and FOV `46`. Four Lanes defaults to yaw `45`, pitch `65`, distance
  `24 m`, forward focus `0 m`, right focus `3 m`, and FOV `50`. The
  right-focus bias holds the defense left of the action HUD. Both allow full yaw orbit because
  the defense is cardinal; every representative heading still requires explicit
  obstruction, target, fortress, and mobile-performance review.

## Ember Vault composition

- The scaled carved floor, boss/recovery inlays, and continuous lava boundary must
  read as one encounter surface without introducing false elevation or cover.
- The first-party fortress shoulders frame the boss above the party. Towers and
  walls never replace Core-owned encounter bounds or imply walkable perimeter
  ground.
- Exactly two shoulder braziers provide the readable ember landmarks and exactly two
  unshadowed local light pools. More lights, brighter global emission, or stronger
  bloom/post-processing are not acceptable substitutes for material/shape quality.
- The Boss camera keeps authored default yaw `0` degrees, while its HUD-aware orbit
  arc is centered at `15` degrees with a `50`-degree offset and clamps across the
  wrap to yaw `325..65`. The default and both endpoints must keep the living party,
  boss, inlays, two landmarks, and relevant floor edge readable. Full `360`-degree
  orbit remains deferred.

## Mobile budgets

The authored visual-review default is High, intended to expose the image-quality
ceiling. The user's latest 2026-09-08 instruction prioritizes the best image and
physical **iQOO Neo11 at High / best quality** as the first acceptance target.
Do not silently reduce quality to make that test pass. The earlier one-tier-below
minimum, provisionally Snapdragon 8 Gen 3 / Adreno 750 class, is a later
compatibility goal. See
[the device reference contract](DEMO_READINESS.md#performance-reference-class)
for official source references, regional-SKU caveats and the untested boundary.
The target remains sustained 60 FPS at a declared 1080p-class render resolution;
explicit Balanced/Performance fallbacks exist, but no automatic tier selection
or full-panel-resolution High pass is implied. No current physical Android
profiling proves frame time, thermal behavior, battery, memory or touch comfort
on any tier. The iPhone reference remains untested.

High uses native render scale, 4x MSAA, HDR, a 4096 soft-shadowed main-light map at
`40 m` with two cascades split at `0.55`, per-pixel additional lights with at most
four affecting one object, and full authored VFX density. Balanced remains the
0.85-scale FSR, 2x MSAA, HDR, 2048/`32 m`/two-cascade (`0.60`) fallback with two
per-pixel additional lights. Performance remains the 0.78-scale FSR, 1x MSAA, LDR,
1024/`24 m`/single-hard-cascade fallback with one per-vertex additional light.
No additional light casts a shadow. These are authored candidates, not measured
approvals; profile post, reflection, texture sampling, shadow resolution/cascades,
and FSR on physical Android hardware before assigning supported hardware. The
three tiers are explicit contracts, not ad-hoc Inspector tuning. Spell and brazier
lights remain unshadowed; use shared materials/`MaterialPropertyBlock` and bounded
pools. Current hard pool contracts are 24 spell views, 16 combat-text views, 10
world SFX sources, and 3 UI SFX sources. The frame, geometry, draw-call, allocation,
and memory budgets are in `DEMO_READINESS.md`; exact tier values live in
`Unity/Performance/MobileQualityContract.cs`.

For each new visual feature:

- Capture worst-case simultaneous casts, not only an isolated beauty shot.
- Record main-thread, render-thread, and GPU p95 after warm-up.
- Inspect overdraw on the ability cluster, nameplates, particles, and lava.
- Confirm pool/material counts return to baseline after replay.
- Prefer one stronger shape and timing curve over several translucent layers.

## Review sheet

Capture the same scene/state at 16:9, 20:9, and 2.4:1 landscape, with UI safe areas.
`Tools/Agent/visual-review-matrix.json` is the machine-readable bounded scenario set;
do not replace it with unbounded or arbitrary screenshots.
Include:

- all seven player specializations in representative combat;
- ready, GCD, cooldown, casting, blocked, selected target, stealth, and death states;
- Fireball/Blast Wave, Frost Nova/Deep Freeze, Penance/Shield, Rogue opener/finisher,
  Shadow damage-over-time/dispel/Dispersion, Hunter Sting/Chimera/trap/pet-order,
  boss telegraph/knockback, miss, interrupt, absorb, and lava contact;
- Hunter companion Attack, Follow, Stay, Web, damaged, and dead presentation,
  while preserving the current no-direct-enemy-targeting boundary;
- the same stable Arena state at yaw `90` (authored default), yaw `30` (minimum),
  and yaw `150` (maximum), keeping the human, focused target, immediate threat,
  cover, and arena-edge relationship obvious;
- the same stable Ember Vault state at Boss yaw `0` (authored default), yaw `325`
  (negative endpoint), and yaw `65` (positive endpoint), keeping the party, boss,
  inlays, two ember landmarks, and relevant floor edge obvious;
- the same stable Ashen Stronghold state in Solo Lane and Four Lanes at their
  authored defaults plus the four cardinal headings, keeping the human, assigned
  lane or full fortress, overlapping threats, objective health, and supply controls
  obvious;
- one desaturated capture and one small-screen capture;
- one worst-case multi-effect frame with profiler evidence.

A visual change passes only if a reviewer can identify teams and focus in under one
second, name why a pressed ability failed, distinguish the demonstrated abilities
without color, identify cast resolution, and track feet/cover/lava throughout.
