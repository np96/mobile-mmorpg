# Third-Party Notices

Sunbreak surface notice verified: 2026-09-05. Other entries retain their recorded
verification dates.

Only the free distributions listed below are included. No `EXTRA` or `SOURCE`
tier files are present. The project keeps Unity-oriented FBX assets and the
textures they require; duplicate GLTF and OBJ variants were intentionally not
imported.

## Poly Haven - Rock Moss Set 01

- Provider: Poly Haven; creator: Kless Gyzen. Intake `POLYHAVEN-2026-003`, 2026-09-07.
- [Official asset](https://polyhaven.com/a/rock_moss_set_01),
  [official license](https://polyhaven.com/license): CC0 1.0 Universal.
- Four unchanged sources: 1K FBX, diffuse JPEG, OpenGL/+Y normal EXR and roughness
  JPEG, 4,475,602 bytes total. Retained originals, exact CDN URLs, official MD5 and
  SHA-256 hashes, local legal code and provenance are under
  `Assets/ThirdParty/PolyHaven/RockMossSet01/`.
- Only the 5,000-triangle `rock03` is selected for one camp-one Sunbreak benchmark.
  The six-model source is 63,127 triangles, not six low-poly runtime props. No
  LODs or source preview images were acquired. No live service dependency exists.
- `SunbreakScannedRockAuthoring` owns bounded model-only import and one dedicated
  generated material. No physics, animation or gameplay collision is imported.
- Status: `TECH_REVIEW`; physical-device cost and final art approval are open.

CC0 permits commercial use, modification and redistribution without mandatory
attribution. Credit is retained for provenance; these are third-party scans, not
original Session RPG geometry. Preserve the source files and their identities.

## Poly Haven - Sunbreak scanned surfaces

- Provider: Poly Haven; source intake `POLYHAVEN-2026-002`, 2026-09-05.
- Sources and contributors: [Sparse Grass](https://polyhaven.com/a/sparse_grass)
  by Amal Kumar; [Rocks Ground 02](https://polyhaven.com/a/rocks_ground_02),
  [Rock 08](https://polyhaven.com/a/rock_08), and
  [Bark Brown 02](https://polyhaven.com/a/bark_brown_02) by Rob Tuytel.
  Revision two adds [Leafy Grass](https://polyhaven.com/a/leafy_grass) by Charlotte
  Baglioni and [Dirt Floor](https://polyhaven.com/a/dirt_floor) by eye-candy.xyz.
- License: CC0 1.0 Universal; [official asset-license statement](https://polyhaven.com/license).
  Retained legal code: `Assets/ThirdParty/PolyHaven/SunbreakSurfaces/CC0-1.0.txt`.
- Unmodified source subset: twelve 2K and six 1K JPEG diffuse, OpenGL/+Y normal and
  roughness maps, totaling 41,996,162 bytes. No previews, models or displacement.
  The six earlier ground maps remain comparison sources; revision-two ground
  bindings select Leafy Grass and Dirt Floor after gameplay-camera review.
- Exact source URLs, physical dimensions, official MD5 and project SHA-256 hashes:
  `Assets/ThirdParty/PolyHaven/SunbreakSurfaces/manifest.json`.
- Acquired through bounded official public-API metadata lookups and direct CDN
  downloads. There is no live API dependency in the game.
- Scope: `TECH_REVIEW` Sunbreak Basin material evaluation. This does not establish
  final-art, external-release or physical-Android acceptance.

CC0 permits commercial use, modification and redistribution without attribution;
the credits above are preserved for provenance. The sources remain third-party
scans. Their Unity import and generated material bindings are owned by
`SunbreakScannedSurfaceAuthoring.cs`; original JPEGs must remain unchanged.

## Poly Haven - Kloofendal partly-cloudy pure sky

- Greg Zaal (original), Jarod Guest (pure-sky edit), distributed by
  [Poly Haven](https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky), CC0-1.0.
- Unmodified 1K HDR panorama with real clouds and no ground/buildings. Exact
  source, hashes, provenance and license retained under
  `Assets/ThirdParty/PolyHaven/SunbreakDaylightSky/`.
- Intake `POLYHAVEN-2026-006`, `TECH_REVIEW`; Sunbreak sky only. The existing
  analytic reflection is not claimed to be derived from this panorama.

## Poly Haven - Source-derived Sunbreak vegetation

- [Tree Small 02](https://polyhaven.com/a/tree_small_02), Rico Cilliers;
  [Grass Medium 01](https://polyhaven.com/a/grass_medium_01), Rob Tuytel and
  Rico Cilliers; [Fern 02](https://polyhaven.com/a/fern_02), Rob Tuytel and
  Rico Cilliers. Distributed by Poly Haven under CC0-1.0.
- Exact originals, provenance, legal code and hashes are retained under
  `Assets/ThirdParty/PolyHaven/{TreeSmall02,GrassMedium01,Fern02}/`.
- The tree is a disclosed project derivative: reduced source trunk and
  source-rendered leaf-cluster cards with genuine source color/coverage,
  normal and roughness atlases. Grass/fern selections preserve source geometry.
  The reproduction procedure and all derivative hashes are recorded in
  `SourceArt/Environment/SunbreakVegetationV1/`.
- Intake `POLYHAVEN-2026-005`, `TECH_REVIEW`. These are third-party asset
  derivatives, not original Session RPG art or production-quality acceptance.

## Poly Haven - Modular Fort 01

- Creator: Rico Cilliers; distributor: [Poly Haven](https://polyhaven.com/a/modular_fort_01).
- License: [CC0-1.0](https://polyhaven.com/license); retained legal code,
  exact source URLs, hashes and provenance in
  `Assets/ThirdParty/PolyHaven/ModularFort01/`.
- Original FBX plus six measured wall/trim diffuse, normal and roughness maps.
  Four individual module instances form the Leveling focal architecture; no
  provider preview image is embedded. Source UVs and topology are preserved.
- Intake `POLYHAVEN-2026-004`, `TECH_REVIEW`; commercial use is permitted, but
  production visual quality and physical-device performance are not inferred.

## Poly Haven - Stone Brick Wall 001

- Provider and distributor: Poly Haven
- Contributors credited by the asset page: Dimitrios Savva (photography) and Rico
  Cilliers (processing)
- Official asset page: https://polyhaven.com/a/stone_brick_wall_001
- Official asset-license page: https://polyhaven.com/license
- License: Creative Commons Zero v1.0 Universal (CC0 1.0)
- Retained legal code:
  `Assets/ThirdParty/PolyHaven/StoneBrickWall001/CC0-1.0.txt`
- Legal-code source:
  https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt
- Retrieved: 2026-09-02 as individual 2K PNG maps from the official
  `dl.polyhaven.org` asset CDN links; the public API was not used
- Imported source subset: diffuse, OpenGL/+Y normal, ambient occlusion, roughness,
  and displacement maps. Displacement is retained for offline derivation and is not
  accepted as a mobile runtime sample.
- Exact URLs, dimensions, byte lengths, channel intent, and SHA-256 hashes:
  `Assets/ThirdParty/PolyHaven/StoneBrickWall001/manifest.json`
- Current boundary: immutable source intake promoted to a bounded `TECH_REVIEW`
  runtime candidate. `AuthoringAssets` selects the untouched diffuse and OpenGL/+Y
  normal for the shared `RuinStone` material and binds the deterministic 1024 px
  `FortressMasonryMask.png` AO/smoothness derivative. This is not final-art or
  physical-device acceptance.

Poly Haven states that all of its assets are CC0 and may be used, modified,
redistributed, and included in commercial work without required attribution.
Credit is retained here for provenance. Explicit Unity import settings and
deterministic channel packing are now authored and recorded. Fresh generated-runtime
verification, stylization/gameplay-camera approval, and physical Android
texture/shader/frame-time/memory evidence remain required.

## Adobe Mixamo - licensed customizable character bases

- Provider: Adobe Mixamo
- Official service: https://www.mixamo.com/
- Official use FAQ: https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html
- Adobe General Terms: https://www.adobe.com/legal/terms.html (Content Files,
  section 3.6 at the verification date)
- Characters and roles:
  - `Erika Archer With Bow/Arrow` — Hunter
  - `Uriel A Plotexia` — Priest
  - `Vampire A Lusth` — Rogue
  - `Demon T Wiezzorek` — Mage
- Acquired: 2026-08-27 through the user's signed-in Adobe account
- Retained Unity subset: hash-locked model-only FBXs and their audited required
  textures under `Assets/ThirdParty/Trials/AdobeMixamo/ErikaArcher/` and
  `Assets/ThirdParty/Trials/AdobeMixamo/PlayableClasses/`
- Runtime motion: the existing generated project controllers and Humanoid/AvatarIK
  choreography; animation import is disabled on every Mixamo character model
- Excluded: the four downloaded Erika animation FBXs remain outside Unity under
  the historical Hunter trial evidence folder; no Mixamo animation is accepted for
  the three additional class bases
- Current project-policy boundary: `promotionAllowed:true`; exact hashes, model-only
  imports, project-owned runtime motion, and embedded-game distribution are
  validated for Development and non-Development builds.
- Provenance, immutable hashes, measurements, and decision:
  `SourceArt/Trials/AdobeMixamo/ErikaArcher/` and
  `SourceArt/Trials/AdobeMixamo/PlayableClasses/`

These sources are not CC0. Adobe's official FAQ permits royalty-free Mixamo
character/animation use
in commercial video games. Adobe General Terms section 3.6 permits modifying
Content Files before embedding them in an End Use and distributing them only with
that End Use. Clothes, hair, face, mesh, and material modifications may therefore
ship inside the game, but the original or modified raw FBX must not be distributed
standalone or as an asset pack. The user accepted these models as customizable game
bases on 2026-09-01. Art customization, deformation, gameplay-camera, and mobile
acceptance remain technical/product work. Commissioned replacement is an optional
brand/quality choice, not a Mixamo license requirement.

The Ashen Causeway may reuse the Priest/Rogue/Mage model roots as enemy readability
bases. Any customized derivatives remain Mixamo-derived and subject to the same
embedded-game-only distribution boundary.

## KayKit - Character Pack : Adventurers (Free 2.0)

- Creator and distributor: Kay Lousberg
- Official catalog page: https://kaylousberg.com/game-assets/characters-adventurers
- Official download page: https://kaylousberg.itch.io/kaykit-adventurers
- License: Creative Commons Zero v1.0 Universal (CC0 1.0)
- License reference: https://creativecommons.org/publicdomain/zero/1.0/
- Retained license: `Assets/ThirdParty/KayKit/Adventurers/License.txt`
- Retrieved archive SHA-256: `abe48f4763fba0896bab486ee9e6d08ca6b5b3884b9601f235c8847ae94dc479`
- Imported subset: Knight, hooded Rogue, and Mage character FBXs, their atlas
  textures, and a small set of class equipment FBXs.

## KayKit - Character Animations (Free 1.1)

- Creator and distributor: Kay Lousberg
- Official catalog page: https://kaylousberg.com/game-assets/character-animations
- Official download page: https://kaylousberg.itch.io/kaykit-character-animations
- License: Creative Commons Zero v1.0 Universal (CC0 1.0)
- License reference: https://creativecommons.org/publicdomain/zero/1.0/
- Retained license: `Assets/ThirdParty/KayKit/CharacterAnimations/License.txt`
- Retrieved archive SHA-256: `65882f31f905ad2e953819648a59287cdeab8f623908d5ef701971d3758be20f`
- Imported subset: the medium-rig General, Movement Basic, Combat Melee, and
  Combat Ranged FBX animation collections.

## KayKit - Dungeon Pack (Free 1.1; formerly Dungeon Remastered)

- Creator and distributor: Kay Lousberg
- Official catalog page: https://kaylousberg.com/game-assets/dungeon-remastered
- Official download page: https://kaylousberg.itch.io/kaykit-dungeon-pack
- License: Creative Commons Zero v1.0 Universal (CC0 1.0)
- License reference: https://creativecommons.org/publicdomain/zero/1.0/
- Retained license: `Assets/ThirdParty/KayKit/Dungeon/License.txt`
- Retrieved archive SHA-256: `6acb859d1aefae074f937d1e6f13656a7312b2bdb6d9f1232ebdade8d93d6a1c`
- Imported subset: one shared atlas texture and 27 modular floor, wall, stair,
  structural, prop, and team-banner FBXs selected for the arena and dungeon.

## KayKit - Character Pack : Skeletons (Free 1.1)

- Creator and distributor: Kay Lousberg
- Official catalog page: https://www.kaylousberg.com/game-assets/characters-skeletons
- Official download page: https://kaylousberg.itch.io/kaykit-skeletons
- License: Creative Commons Zero v1.0 Universal (CC0 1.0)
- License reference: https://creativecommons.org/publicdomain/zero/1.0/
- Retained license: `Assets/ThirdParty/KayKit/Skeletons/License.txt`
- Retrieved archive SHA-256: `21fbad59ee6cc1d7bed12d0e425acab8ebe564b8620bbc1d017aedb29dd8a3d2`
- Imported subset: Skeleton Warrior character and atlas texture, axe, and
  large shield FBXs for the prototype boss presentation.

Attribution is not required by CC0. These notices and the original license
files are retained for provenance and auditability.

## Quaternius - Universal Base Characters (Standard)

- Creator and distributor: Quaternius
- Official download page: https://quaternius.itch.io/universal-base-characters
- License: Creative Commons Zero v1.0 Universal (CC0 1.0)
- Retained license: `Assets/ThirdParty/Quaternius/UniversalBaseCharacters/License_Standard.txt`
- Retrieved 2026-08-11 archive SHA-256: `fdbf1804c90dfc1ea03e992bff7da2dfd1a79318e13270a660180f9308455f40`
- Imported subset: male and female Superhero full-body Unity FBXs and the textures
  required to author reusable head-only meshes for the playable characters.

## Quaternius - Modular Character Outfits: Fantasy (Standard)

- Creator and distributor: Quaternius
- Official download page: https://quaternius.itch.io/modular-character-outfits-fantasy
- License: Creative Commons Zero v1.0 Universal (CC0 1.0)
- Retained license: `Assets/ThirdParty/Quaternius/FantasyOutfits/License_Standard.txt`
- Retrieved 2026-08-11 archive SHA-256: `c3468b18871cc8c8f05ab14df7712baf22cb9f389cbd870babf130e595187f70`
- Imported subset: female Peasant, male Ranger, and female Ranger assembled Unity
  FBXs plus their base-color, normal, and supporting body textures.
- First-party derivative: `Assets/SessionRpg/Art/Characters/HunterOutfitAlbedo.png`
  retains the pack's CC0 Ranger UV layout so it can use the Ranger mesh and normal
  map, but replaces the color surface with original generated project art. Its
  generation record and audited hash are in `Docs/Art/GENERATED_ASSETS.md`.

## Quaternius - Universal Animation Library (Standard)

- Creator and distributor: Quaternius
- Official download page: https://quaternius.itch.io/universal-animation-library
- License: Creative Commons Zero v1.0 Universal (CC0 1.0)
- Retained license: `Assets/ThirdParty/Quaternius/UniversalAnimationLibrary/License.txt`
- Retrieved 2026-08-11 archive SHA-256: `cc73fc4e495b82958207316596317a3f40b9fa38065bde1027937452da537724`
- Imported subset: `UAL1_Standard.fbx`, the in-place Unity animation library. The
  root-motion `_RM` variant is intentionally excluded.

Quaternius attribution is not required by CC0. The original license files and
machine-readable manifest are retained for provenance and auditability.

The Quaternius assets above remain the current Unity prototype/rollback only.
They are expressly excluded from the production-character mesh, rig, bind-pose,
weight, material, UV, and proportion lineage.

## Blender Foundation / Blender Studio - Human Base Meshes v1.4.1

Historical research acquisition record: the following source/provenance paths are
retained in the original development workspace, not this cleaned public checkout.
They are not inputs to the public Quaternius-based game.

- Creator and distributor: Blender Studio and community contributors, distributed
  by the Blender Foundation.
- Official page: https://www.blender.org/download/demo-files/
- Official archive:
  https://download.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip
- License: Creative Commons Zero v1.0 Universal (CC0 1.0), as stated by the
  official demo-files listing.
- Retrieved archive SHA-256:
  `811f43accbb31a88266d932f8f5563b2d13586fca0ba2693aad1f5fe582b3515`.
- Official source blend SHA-256:
  `3c121505651140ceb4d69fd1d8923f7788ffadd81672f5be14845a5f2c75c137`.
- Retained source and provenance:
  `SourceArt/ThirdParty/BlenderFoundation/HumanBaseMeshes_1.4.1/`.
- Selected production-anatomy subset: `GEO-body_male_stylized`,
  `GEO-body_female_stylized`, and their paired eye meshes. These remain DCC
  source outside Unity until the project body, deformation, art, and import gates
  pass.

Attribution is not required by CC0. The notice, hashes, official source, and
license reference are retained for provenance and auditability.

## MPFB 2.0.17 bundled MakeHuman base assets

Historical research acquisition record: the following source/license/provenance
paths are retained in the original development workspace and omitted publicly.
The public game does not depend on these character-research inputs.

- Creator and distributor: MakeHuman Community / MPFB contributors
- Official Blender Extensions distribution: https://extensions.blender.org/add-ons/mpfb/
- Version-pinned upstream release:
  https://github.com/makehumancommunity/mpfb2/releases/tag/v2.0.17
- Upstream tag commit: `80919fa4682335c41847f761a4d79dcad4124732`
- Add-on code license: GNU General Public License v3.0 or later
  (`GPL-3.0-or-later`), documented by the installed extension manifest and the
  version-pinned combined license at
  https://github.com/makehumancommunity/mpfb2/blob/v2.0.17/LICENSE.md
- Bundled graphical-asset/output license: Creative Commons Zero v1.0 Universal
  (CC0 1.0), documented at
  https://github.com/makehumancommunity/mpfb2/blob/v2.0.17/LICENSE.ASSETS.md
- Retained asset license:
  `SourceArt/ThirdParty/MakeHumanCommunity/MPFB_2.0.17/LICENSE.ASSETS.md`
- Retained provenance and exact source/license manifest:
  `SourceArt/ThirdParty/MakeHumanCommunity/MPFB_2.0.17/PROVENANCE.md` and
  `SourceArt/ThirdParty/MakeHumanCommunity/MPFB_2.0.17/manifest.json`
- Retained research boundary: the rejected masculine/feminine body-intake
  checkpoint and deterministic rig-refit landmark record under
  `SourceArt/Blender/Characters/SessionHumanoidBaseV1/`. No MPFB source asset,
  add-on code, temporary MPFB rig, or MPFB helper geometry is copied into Unity
  `Assets/` or a runtime deliverable.
- Reproducibility and exact presets/source hashes:
  `SourceArt/Blender/Characters/SessionHumanoidBaseV1/Outputs/SessionHumanoidBaseV1_RigFitLandmarks.json`.

MPFB's GPL add-on code remains tooling-only in Blender: it is not copied into
the repository, linked with the game, copied into Unity `Assets/`, or shipped.
The retained core mesh assets and generated body output are covered by MPFB's
documented CC0 graphical-asset/output boundary. MPFB is not the selected
production anatomy; it is retained only for comparison/fallback research.

## Alegreya Sans SC

- Designer: Juan Pablo del Peral / Huerta Tipográfica
- Distributor: Google Fonts
- Official source: https://github.com/google/fonts/tree/main/ofl/alegreyasanssc
- License: SIL Open Font License 1.1
- Retained license: `Assets/ThirdParty/Fonts/AlegreyaSansSC/OFL.txt`
- Retrieved 2026-08-18 font SHA-256:
  `532c7dc4bc0fe8109d2c5f17829d9985597aacb86e8001023e045d4ee00f356a`
- Imported subset: `AlegreyaSansSC-Medium.ttf` for compact combat HUD and
  world-space readability. The font is embedded as part of the game and is not
  sold or redistributed by itself.
