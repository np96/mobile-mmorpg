# Generated Art Provenance

## Arena quality target v1

- File: `Docs/Art/VisualTargets/arena-quality-target-v1.png`
- Generated: 2026-08-18 with the built-in OpenAI image-generation workflow.
- Use: composition and finish reference only; it is not imported by Unity.
- Input: `outputs/captures/20260818-final/marksmanship-baseline-v3-16x9.png`.
- Prompt summary: preserve the real strategy camera, arena geometry, combatants,
  joystick, then-current dense control grammar, rosters, and objective banner;
  transform only the
  presentation into an original premium stylized fantasy MMORPG frame with
  hand-painted stone, perimeter architecture, class silhouettes, controlled VFX,
  grounded lighting, and less intrusive UI. No franchise assets, symbols, logos,
  or copied interface design.

## Arena environment quality target v2

- File: `Docs/Art/VisualTargets/arena-environment-quality-target-v2.png`
- Generated: 2026-09-01 with the built-in ImageGen workflow.
- Source dimensions: 1870 x 841 RGB.
- Source SHA-256:
  `af57f14f59276bb5c528e502b4677af671b8807c4491351aead12856f805ad6b`
- Reference input: the current in-game screenshot at
  `build/review/ux-target-radial-final-2400x1000.png`.
- Use: environment look-development and finish reference only. This is a
  concept-only, non-runtime asset; it is not imported by Unity and is not evidence
  that the depicted environment, materials, lighting, or effects exist in-game.
- Exact final prompt:

```text
Use case: stylized-concept
Asset type: game environment look-development target for a Unity mobile action RPG arena
Input image: Image 1 is the current in-game screenshot and the layout/camera reference.
Primary request: transform only the environment presentation into a premium original volcanic fortress arena while preserving the actual gameplay composition.
Scene/backdrop: ancient circular basalt crucible suspended above a lava chasm, constructed fortress perimeter, broken masonry shoulders, restrained braziers, chains and banners outside playable space.
Style/medium: polished stylized-realistic 3D game screenshot; hand-authored mobile MMORPG environment; strong material definition without photoreal noise.
Composition/framing: preserve the exact elevated strategy camera, circular safe-floor footprint, visible continuous lava boundary, central combat space, two wardstone positions and footprints, combatant scale, HUD placement, and ability wheel placement from Image 1.
Lighting/mood: cool blue-gray key light over playable stone, localized warm fire/lava bounce at the perimeter, readable long cover shadows, deep blue-black abyss; no bloom dependency.
Materials/textures: layered basalt slabs with large-scale value variation, chipped bevels, mortar, sparse ash, localized wear and restrained carved rings; wardstones with distinct masonry courses and cap damage; lava with cooled crust plates and sparse animated-looking fissures; fortress walls with readable bevels and non-flat vertical planes.
Constraints: environment-only look-development; keep the combat floor quiet enough for telegraphs and feet; no new bridges, stairs, ledges, obstacles, elevation, cover, or playable surfaces; do not move or resize wardstones; preserve team/focus/lava readability; mobile-feasible material and geometry language; original symbols only; no franchise resemblance; no logos, trademarks, watermarks, or added text; do not redesign the UI.
```

## Arena flagstone albedo

- File: `Assets/SessionRpg/Art/Environment/Textures/ArenaFlagstoneAlbedo.png`
- Generated: 2026-08-18 with the built-in OpenAI image-generation workflow.
- Use: project-bound source albedo, imported as a repeatable 1K Android texture.
- Prompt:

  > Seamless square texture of dark blue-gray basalt flagstones, viewed perfectly
  > top-down. Medium-large irregular fitted slabs with broad hand-painted value
  > variation, shallow worn cracks, chipped beveled edges, sparse subtle engraved
  > arc fragments, and restrained cool mineral flecks. Neutral flat albedo with no
  > directional shadows or central focal point; premium original stylized fantasy
  > MMORPG environment texture; charcoal/cool slate palette; no text, recognizable
  > franchise runes, logos, trademarks, or watermark.

The source PNG is intentionally retained unmodified. Unity importer settings own
the mobile max size, mipmaps, repeat mode, and compression so the source remains
auditable and future platforms can choose a different import budget.

## Arena lava albedo

- File: `Assets/SessionRpg/Art/Environment/Textures/ArenaLavaAlbedo.png`
- Generated: 2026-08-18 with the built-in OpenAI image-generation workflow.
- Use: project-bound source albedo for the non-stone hazard/backdrop surface.
- Prompt:

  > Seamless square texture of mostly dark cooled volcanic basalt with sparse
  > branching molten-orange fissures and small ember pockets, perfectly top-down.
  > Large quiet charcoal areas, hot seams below roughly fifteen percent coverage,
  > original hand-painted stylized fantasy MMORPG treatment, no directional
  > shadows, text, symbols, logos, trademarks, or watermark.

As with the stone source, Unity owns repeat, mip, compression, and platform-size
policy. The lava animator may modulate this texture through shared materials and
`MaterialPropertyBlock`; it must never instantiate a runtime material.

## Arena floor PBR derivatives

The deterministic Blender floor pipeline now produces two additional first-party
1024 px runtime derivatives from the retained floor sources:

- `Assets/SessionRpg/Art/Environment/Textures/ArenaFloorMacroNormal.png` — explicit
  OpenGL/+Y tangent normal derived from local luminance relief in the full-floor
  macro albedo; SHA-256
  `21b6b11785d40ac4a39fab0e2fbbfab72f3081a86ac56d2aa5df5698f9a69365`.
- `Assets/SessionRpg/Art/Environment/Textures/ArenaFloorMacroMask.png` — the
  micro packed mask phase-baked across full-floor UV0 at
  `fract(uv * 4.6875 + vec2(0.47125, 0.64125))`, then combined with macro joint and
  cavity AO; SHA-256
  `f0d2a9b3511536d60fbfdde681f466c4436940575b976dac18ebcf1d87eb4cd0`.

`Tools/Art/Blender/arena_floor_surface_maps.py` version `3.1.0` owns both
derivatives and records their source hashes, channel ranges, UV proof, and runtime
roles in `Assets/SessionRpg/SourceArt/Environment/ArenaFloorVisual.manifest.json`.
The mask uses R metallic, G ambient occlusion, B spare height, and A smoothness.
Its macro G channel includes the bounded cavity term and its macro A channel remaps
the phase-coherent micro roughness. Unity binds the macro normal at strength `0.78`
and the mask once across the floor while retaining the independently tiled micro
normal at strength `0.38` and a linear detail-albedo layer at strength `0.055`.
This replaces the former byte-identical albedo-to-normal workaround; it does not
constitute physical-device performance or final-art acceptance.

## Fortress masonry PBR derivative

`POLYHAVEN-2026-001` supplies immutable Stone Brick Wall 001 diffuse, OpenGL/+Y
normal, AO, roughness, and displacement sources under
`Assets/ThirdParty/PolyHaven/StoneBrickWall001/`. The first two are selected
directly by the shared `RuinStone` authoring path. The deterministic Blender tool
`Tools/Art/Blender/prepare_fortress_pbr_maps.py` validates the retained source
hashes and produces one mobile-bounded derivative:

- `Assets/SessionRpg/Art/Environment/Textures/FortressMasonryMask.png` — 1024 px
  linear packed mask, R metallic `0`, G ambient occlusion, B displacement
  reference not sampled by the current URP Lit material, and A smoothness clamped
  to `0.02..0.42`; SHA-256
  `fddbbf69b2d67b54ef381e8af01bfe3665bbccc560caec4e3487de4b0b246ca2`.

The complete derivation record lives at
`SourceArt/Blender/Environment/Textures/FortressMasonryPbr.manifest.json`. This is
a CC0 third-party-derived runtime candidate in `TECH_REVIEW`, not original Session
RPG texture art and not an accepted production surface. Gameplay-camera coherence,
the user's final visual acceptance, and physical Android texture/shader budgets
remain open.

## Generated environment look assets

`AuthoringAssets` deterministically creates two serialized presentation assets
during a full authored rebuild:

- `Assets/SessionRpg/Settings/ArenaEnvironmentReflection.asset` — a 64 px
  `RGBAHalf` mipmapped cubemap synthesized from a cool sky, blue-black ground, and
  restrained warm horizon response. Lit floor and fortress renderers use simple
  reflection-probe sampling against it; it is not a captured or third-party HDRI.
- `Assets/SessionRpg/Settings/ArenaEnvironmentLook.asset` — a global volume profile
  containing restrained color adjustments and white balance, ACES tone
  mapping, high-quality half-resolution bloom, and vignette. The generated gameplay
  camera enables this profile, HDR, dithering, and NaN rejection under the default
  High pipeline. Performance remains the explicit LDR fallback.

Both are generated outputs owned by
`Assets/SessionRpg/Editor/Authoring/AuthoringAssets.cs`; do not tune them in the
Inspector. They are evaluation tools for material/value separation, not substitutes
for shape or surface quality. Current macOS-player High-tier captures are listed in
`VISUAL_TARGET.md`; physical Android GPU/memory/thermal review remains open.

## Generated arena depth and atmosphere assets

`EnvironmentAuthoring` also generates bounded presentation-only depth and atmosphere
under the arena visual hierarchy:

- `Assets/SessionRpg/Art/Environment/ArenaOuterBasaltCrust.asset` — an irregular
  two-material 612-triangle rock cluster field with broad lava gaps;
- `Assets/SessionRpg/Art/Environment/ArenaFlankSilhouettes.asset` — off-floor ruin
  silhouettes that extend the fortress beyond the playable ring;
- `Assets/SessionRpg/Art/Environment/ArenaLavaFissures.asset` and
  `BossLavaFissures.asset` — thin overlay meshes whose emissive pulse is deliberately
  near-invisible over the phase-coherent Lit lava surface; and
- deterministic cinder-veil, `FirePlume`, `FireCore`, and `EmberSparks` particle
  systems authored into the generated Arena/Boss presentation.

These objects live on Ignore Raycast, add no gameplay collider or Rigidbody, and do
not alter Core-owned safe, hazard, LOS, or movement geometry.

## Hunter outfit albedo

- File: `Assets/SessionRpg/Art/Characters/HunterOutfitAlbedo.png`
- Generated: 2026-08-19 with the built-in OpenAI image-generation workflow.
- Source dimensions: 1254 x 1254 RGB.
- Source SHA-256:
  `633e51d4f07f2192be91d9a92ec7d27ac257ef5cd00816728bbb071573a01aca`
- Use: project-bound first-party albedo for the current Quaternius-backed Hunter
  prototype/rollback only; it is excluded from the production-character lineage.
- Layout reference:
  `Assets/ThirdParty/Quaternius/FantasyOutfits/Textures/T_Ranger_BaseColor.png`
  from the retained CC0 Modular Character Outfits: Fantasy distribution.
- Prompt summary: preserve the normalized Ranger UV islands while repainting the
  surface as an original dark forest-green and brown leather battlemage-ranger set
  with hand-painted material depth, restrained bronze trim, worn silver hardware,
  no franchise symbols, logos, text, or watermark.

Only the functional CC0 UV layout was retained. The generated color surface is an
original project asset and does not modify any file below `Assets/ThirdParty`.
Visual inspection confirmed that the generated islands occupy the same normalized
locations as the Ranger source. The existing `T_Ranger_Normal.png` therefore stays
bound to the generated Hunter material. The source ORM is intentionally not bound:
its occlusion/roughness/metallic channel order is incompatible with the stock URP
Lit metallic/smoothness texture contract without a separately audited repack.

The tracked importer keeps sRGB, mipmaps, repeat wrapping, bilinear filtering,
anisotropy 4, non-readable CPU state, a deterministic 1024 px NPOT conversion, and
an Android 1024 px automatic compressed override. The source hash and dimensions
are validated before every authoring rebuild. A source-level A/B constant in
`CharacterAuthoring` can restore the untouched CC0 albedo for diagnosis; it is not
a runtime setting.

The generated `Hunter_Outfit` material uses a neutral white tint so the atlas owns
the cloth/leather value breakup. Strategy-distance class separation comes from a
non-emissive forest-green armor accent `(0.28, 0.52, 0.10)` and warm bronze weapon
surface `(0.96, 0.68, 0.18)` with restrained metallic response and no emission.
The authored bow limb is `0.095 m` wide; this changes projected weapon authority,
not the Hunter's `1.46` visual scale or any gameplay/selection geometry.

## Ability medallion frame

- Generated source: `SourceArt/UI/AbilityMedallionFrame_Source.png`
- Source SHA-256:
  `13666cff685c69b44b8655ed7e98d4d3784b8fcc628fc486f677f5dd6a8b3d29`
- Runtime derivative:
  `Assets/SessionRpg/Art/UI/Hud/AbilityMedallionFrame.png`
- Runtime SHA-256:
  `00a9e171b879a4c44e44d5493e284553f028bdfea6d889954d2a9067978e08bc`
- V2 baseline generated: 2026-08-31 with the built-in OpenAI image-generation
  workflow; current Siege runtime additions are pending fresh acceptance.
- Use: one shared first-party forged-iron and aged-brass frame for every ability
  slot. Dynamic readiness, cooldown, selection, and unlock rings remain authored
  Unity presentation layered independently from this art.

The generator returned a 1254 x 1254 opaque RGB source over a deliberate uniform
black matte. `Tools/Art/extract_hud_medallion_alpha.py` deterministically recovers
the clear center, transparent perimeter, and antialiased silhouette while retaining
the authored dark metal values. The tracked Unity importer then constrains the
runtime sprite to 512 px, Full Rect, clamp, bilinear, Kaiser mipmaps, and
uncompressed RGBA. `SourceArt/UI/AbilityMedallionFrame.PROVENANCE.md` preserves the
complete final prompt and derivation contract.

## Playable ability icons v2 and Siege extensions

- Leveling-extension masters:
  `SourceArt/UI/AbilityIconsLevelingV2/Masters/*.png`
- Leveling prompt record and hash manifest:
  `SourceArt/UI/AbilityIconsLevelingV2/PROVENANCE.md` and
  `SourceArt/UI/AbilityIconsLevelingV2/manifest.json`
- Arena-extension masters:
  `SourceArt/UI/AbilityIconsArenaV2/Masters/{Hunter,Mage,Priest,Rogue}/*.png`
- Arena exact-prompt records and hash manifest:
  `SourceArt/UI/AbilityIconsArenaV2/{Hunter,Mage,Priest,Rogue}.PROMPTS.md` and
  `SourceArt/UI/AbilityIconsArenaV2/manifest.json`
- Siege V1 Blade Tempest master and provenance:
  `SourceArt/UI/AbilityIconsSiegeV1/`
- Runtime RGB sources and RGBA derivatives:
  `Assets/SessionRpg/Art/UI/AbilityIcons/`
- Generated: 2026-08-31 with the built-in OpenAI image-generation workflow.

The Leveling V2 evidence adds the twelve paintings that closed the original PvE
gap: `ShadowWordPain`, `MindFlay`, `MindBlast`, `DevouringPlague`, `Dispersion`,
`SteadyShot`, `ArcaneShot`, `SerpentSting`, `PetAttack`, `Disengage`,
`FreezingArrow`, and `RapidFire`. The Arena V2 evidence adds the remaining 45
class actions that were previously reachable only through explicit vector
fallbacks. Together with the retained first-party runtime art and the level-one
foundation paintings for `Stealth`, `Shadowform`, and `Smite`, the accepted v2
baseline has 102 unique `AbilityId` entries. The provenance-recorded Siege V1
`BladeTempest` raised the prior runtime union to 103. The current continuous-Siege
source adds `DawnNova`, `UmbralCollapse`, `FlameSurge`, `Blizzard`, and `ArrowRain`,
so its expected player-bound catalog is 108. Those five additions are not covered
by the historical 2026-08-31 evidence and still require fresh generated/catalog and
visual acceptance.

Each accepted master is a 1254 px opaque RGB painting over a deliberate near-black
matte with no embedded button chrome. The class prompt records preserve the exact
generation direction, and the two manifests hash the accepted master files. A
Lanczos 256 px RGB source is retained under `Assets`;
`Tools/Art/extract_ability_icon_alpha.py` produces the deterministic RGBA runtime
derivative. Unity binds only that `_Transparent` derivative inside the shared
forged medallion. Catalog generation enforces alpha-from-input, sRGB, non-readable
CPU state, Full Rect sprite geometry, clamp, bilinear filtering, Kaiser mipmaps,
and uncompressed 256 px import during visual acceptance.

In Training, every action in the seven-ability learning kit uses this painted sprite
path, but the HUD renders only actions already learned; it does not draw locked
circles or future-skill previews. In Arena, Dungeon, and legacy Boss, primary
actions and all four expanded swipe affordances (`up`, `down`, `left`, and `right`)
use the same painted-art language. Hero Siege preserves that hierarchy but replaces
Arena Trinket with the specialization's direct mode-only area action. The vector
glyph path remains a fail-soft diagnostic for invalid or unbound content; the
expected `108`-ability player-binding union must pass fresh no-fallback coverage
before it is accepted.
