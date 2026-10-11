# Blender art pipeline

Public-checkout note: the character research described below is historical and
optional. Its `SourceArt/Blender/Characters/`, `SourceArt/ThirdParty/` and rejected
archive inputs are omitted from this cleaned distribution, preserved in the
original development workspace. Do not run character-research reproduction commands
without those separately obtained inputs. The public game uses bundled CC0
Quaternius characters; current environment DCC sources remain included. See
[public source distribution](../../../Docs/PUBLIC_SOURCE_DISTRIBUTION.md).

## Current production-character status — 2026-08-20

The selected production anatomy is the official CC0 Blender Foundation / Blender
Studio Human Base Meshes v1.4.1 stylized masculine and feminine pair. Quaternius
is excluded from every production mesh, rig, bind-pose, weight, material, UV, and
proportion lineage. MPFB is retained only as comparison/fallback research.

`SessionHumanoidBaseV3` passes the independent 24-plate two-variant deformation
proof and manual review. `SessionHumanoidAnimationProofV3` passes the source-only
kinematic proof, and the Ready/Draw/Hold/Loose/Recovery sequence passes manual
body-on-motion review on both variants with `0.308676 m` draw travel and
`0.324133 m` release travel against the unchanged `0.30 m` minimum.

`HunterProductionV3ManualMacro` was rejected by both its structural gate and the
required close manual review: the outfit exceeded the evaluated topology budget,
six families penetrated the body, and its major forms read as primitives, panels,
or fused shells. That attempt and the earlier rejected Hunter experiments now
live only under
`SourceArt/Archive/RejectedCharacterResearch_2026-08-20/`; the archive manifest
records their original paths and hashes.

The separate `SessionHumanoidClothingFitStudyV1` plain top/trousers checkpoint
passes its static structural checks and its fit direction was confirmed by the
user. Its body-derived envelopes remain calibration proxies, not production
topology. A fresh BodyV3-specific Hunter kit must still pass manual macro-form and
deformation review before UV/PBR, production LOD, final FBX round-trip, Unity A/B
import, or the coordinated authoring rebuild may begin.

The body and current source-only motion stages are reproduced with:

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/build_session_humanoid_base_v3.py

/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/verify_session_humanoid_base_v3.py

/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/build_session_humanoid_animation_proof_v3.py \
  -- --source-only-review

/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/render_session_humanoid_hunter_motion_v3.py
```

Do not run the V3 animation builder without `--source-only-review`. The ungated
animation mode writes FBX; that operation remains prohibited until the manual-art
gate passes. The rejected macro output is archived evidence and may only inform a
cage/proportion rewrite. The older pre-gate export bundle is also quarantined at
`SourceArt/Archive/RejectedCharacterResearch_2026-08-20/Blender/Characters/SessionHumanoidAnimationProofV3/RejectedPreGateExport_2026-08-20/`.

## Legacy first-party humanoid rig (historical, non-production)

`SessionHumanoidRigV1` is a retained historical technical proof, not the selected
production-character rig or anatomy. Its 66-joint game hierarchy and MPFB-derived
rest-fit measurements remain auditable research only. The current production path
uses the project-owned `SessionHumanoidRigV2` mapping plus the explicitly
contracted `SessionHumanoidBaseV3` twist extension described above.

From `game-rpg/`, build and then independently round-trip the contract:

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/build_session_humanoid_rig_v1.py

/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/verify_session_humanoid_rig_v1.py
```

The build writes the editable `.blend`, exact contract, skeleton-only FBX,
manifest, and provenance under
`SourceArt/Blender/Characters/SessionHumanoidRigV1/`, entirely outside Unity
`Assets`. The verifier reopens the `.blend`, exercises a generated Rigify FK
control, checks the exact game rest hierarchy and normalized four-weight bind
probe, then imports the FBX into an empty scene. It rejects any mesh, action,
constraint, Rigify control/mechanism/widget bone, wrapper transform, hierarchy
change, or joint drift in that FBX. Its current report is written outside Unity
under `../outputs/art/blender/session-humanoid-rig-v1/`.

## Legacy first-party humanoid animation proof (historical, non-production)

`SessionHumanoidAnimationProofV1` is the retained historical technical motion
proof for the same 66-joint V1 contract. It authors ten sparse actions on Rigify animator
controls—combat idle, forward in-place locomotion, hit, death, interrupt, and
Hunter bow ready/draw/hold/loose/recovery—then performs an explicit rest-matrix
retarget and full-frame bake onto the clean game rig. It does not import, inspect,
retarget, or sample any legacy or third-party animation.

From `game-rpg/`, build and round-trip the proof:

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/build_session_humanoid_animation_proof_v1.py

/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/verify_session_humanoid_animation_proof_v1.py
```

The source `.blend`, clips contract, skeleton-only animation FBX, manifest, and
provenance remain under
`SourceArt/Blender/Characters/SessionHumanoidAnimationProofV1/`, outside Unity
`Assets`. The verifier requires exactly ten clean actions and 66 game joints,
identity object/Root motion, no mesh or Rigify/control/mechanism/widget bones,
grounded contacts, closed loops, and quantitative Hunter draw/release travel. Its
round-trip report is written under
`../outputs/art/blender/session-humanoid-animation-proof-v1/`.

This is not visual production acceptance or Unity integration and is not a valid
input to the selected Human Base V3 lineage.

The fortress kit is an original, deterministic project asset built with Blender
5.2 LTS. The script does not download, trace, or modify an external model.

From the `game-rpg/` directory:

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/build_fortress_kit.py
```

The command writes:

- six Unity FBX source inputs under
  `Assets/SessionRpg/SourceArt/Environment/`;
- the editable `SourceArt/Blender/Environment/FortressKit.blend`, outside Unity's
  imported `Assets/` tree;
- a disposable contact-sheet render under `../outputs/art/blender/fortress-kit/`;
- a full 18-bay connectivity render and exact authored strategy-camera evidence
  in the same evidence folder;
- `FortressKit.manifest.json`, which records bounds, triangle counts, UV and
  material contracts for each export.

Round-trip the exported files after a build:

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/verify_fortress_kit.py
```

The verifier imports every FBX into an empty scene and checks its root/mesh
hierarchy, meter scale, dimensions, triangle count, UV0, material order, and the
absence of cameras, lights, and armatures. Evidence is written to
`../outputs/art/blender/fortress-kit/FortressKitFbxRoundTrip.json`.

## Fortress PBR texture preparation

Prepare the mobile packed mask for the `POLYHAVEN-2026-001` Stone Brick Wall 001
runtime candidate independently of the fortress mesh build:

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/prepare_fortress_pbr_maps.py
```

The script validates every source hash against
`Assets/ThirdParty/PolyHaven/StoneBrickWall001/manifest.json`; downsizes the
retained AO, roughness, and displacement sources to a bounded 1024 px derivative;
and writes
`Assets/SessionRpg/Art/Environment/Textures/FortressMasonryMask.png`. Its channel
contract is R metallic `0`, G ambient occlusion, B displacement reference, and A
smoothness clamped from `1 - roughness` to `0.02..0.42`. The current URP Lit
material samples R/G/A; B is retained for deterministic provenance and is not
sampled by that shader. The exact generator version, inputs, channel meanings,
output bytes, and SHA-256 are recorded in
`SourceArt/Blender/Environment/Textures/FortressMasonryPbr.manifest.json`.

The runtime candidate binds the untouched Poly Haven diffuse and OpenGL/+Y normal
sources directly, plus this deterministic mask, through `AuthoringAssets`. The
source PNGs remain immutable. Status is `TECH_REVIEW`, not production acceptance:
fresh generated-runtime validation, target-camera art review, texture/shader
profiling, and physical Android evidence remain required.

## Arena floor visual

Build the original 15 m arena floor visual independently of the fortress kit:

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/build_arena_floor.py
```

Round-trip its FBX contract:

```sh
/Applications/Blender.app/Contents/MacOS/Blender \
  --background --factory-startup --python-exit-code 1 \
  --python Tools/Art/Blender/verify_arena_floor.py
```

This produces `ArenaFloorVisual.fbx`, its machine-readable manifest, the editable
`SourceArt/Blender/Environment/ArenaFloor.blend`, and target-camera evidence under
`../outputs/art/blender/arena-floor/`. The floor is visual-only: Unity continues to
own the flat authoritative collision and all combat geometry.

The FBX uses exact full-floor planar UV0: the 30 m disk maps from world X/Y
`[-15,+15]` to UV `[0,1]`. `ArenaFloorMacroAlbedo.png` is previewed as the single
Stone BaseMap at tiling `1` and offset `0`, with no overlay mesh or secondary
albedo. The same build deterministically derives `ArenaFloorSurfaceAlbedo.png`,
`ArenaFloorSurfaceNormal.png`, and `ArenaFloorSurfaceMask.png` at 1024 px from the
project's original 1254 px seam-free slate microdetail source. The detail normal
repeats at `4.6875` with phase-preserving offset `(0.47125, 0.64125)`; the derived
micro albedo and mask remain reproducible diagnostic/source derivatives.

`ArenaFloorMacroNormal.png` is an explicit OpenGL/+Y tangent normal derived from
local luminance relief in the checked-in full-floor macro albedo; it replaces the
former byte-identical height-to-normal import workaround. `ArenaFloorMacroMask.png`
bakes the micro mask into full-floor UV0 with wrapped bilinear sampling at exactly
`fract(uv * 4.6875 + vec2(0.47125, 0.64125))`. It can therefore occupy URP Lit's
base metallic/occlusion slots while its AO and smoothness remain phase-aligned with
the independently repeated detail normal. Both packed masks use R metallic, G
occlusion, B spare height, and A smoothness. To regenerate only these texture
derivatives and their channel proof, run the same Blender command with
`Tools/Art/Blender/arena_floor_surface_maps.py` as the Python script.

The floor evidence camera matches the playable strategy composition: yaw 90°,
pitch 40°, 18 m distance, 0.9 m pivot, 2.3 m forward focus offset, zero right
offset, and 46° vertical FOV at 16:9.

Every FBX uses meters, Blender Z-up, Unity-compatible `-Z Forward/+Y Up` export,
an origin at ground center, one triangulated visual mesh, UV0, weighted normals,
and a bounded material contract. Walls contain one `Fortress_Stone` submesh;
towers, braziers, and wardstones contain `Fortress_Stone` followed by
`Fortress_Ember`. There are no
colliders, cameras, lights, armatures, animation tracks, or gameplay authority in
the exported assets.

Each wall and tower spans the 7.72 m chord of an eighteen-bay ring at 22.45 m
radius. The tower's wings remain on that chord plane while the central bastion and
its two ember basins project outward. This lets the arena use twelve one-draw wall
renderers and six two-draw tower renderers while retaining a continuous silhouette:
eighteen module renderers and twenty-four module submesh draws. The standalone
brazier is optional source art and is not intended to become a twentieth perimeter
renderer after the single foundation renderer is present.

Fortress kit `1.4.0` replaces the earlier applied rectangular panels and pole-like
supports with geometry that survives the strategy camera: broad chamfered arch
frames and keystones, sloped one-piece wall buttresses, a tapered tower shaft with
a flared crown, a continuous inner portal, and deliberately damaged tower
battlements. The broken wall retains one surviving arch, a sheared buttress,
rotated rubble, exposed ashlar, and a fractured cap. These are still disconnected
closed solids joined into exactly one exported mesh per asset; they add no collider
or gameplay authority.

`FortressFoundationRing.fbx` adds one original joined stone renderer beneath those
modules. Its closed sloped shoulder spans radii 21.30-25.45 m and heights
-1.80--0.45 m, with eighteen inset inner masonry bays and eighteen broad radial
buttress feet. Version `1.4.0` also adds two staggered, sloped shoulder courses with
deterministic gaps and small height/angle variation, breaking up the broad annulus
without creating a reachable ledge. The top remains recessed and sloped rather
than presented as traversable ground; the arena therefore keeps an uninterrupted
lava band from the 15.0 m safe-stone edge through at least 21.30 m. The generator
targets at most 8,000 triangles for this asset (10,000 is the absolute integration
ceiling), one UV0, one stone submesh, and no collider or gameplay authority. Its
strategy evidence uses yaw 90, pitch 40, distance 18 m, pivot 0.9 m, forward focus
2.3 m, right focus 0, and 46 degree vertical FOV at 16:9.

## Production character pipeline

Production characters no longer derive from the prototype character packs. The
current DCC path is:

1. `SessionHumanoidRigV2`: the accepted project-owned Unity Humanoid mapping, with
   a Rigify meta/control rig retained only in the Blender authoring file. The
   production bind may append only documented conventional twist deform chains;
   it may not change mapped Human bones or export Rigify internals.
2. `SessionHumanoidBaseV3`: masculine and feminine anatomy sourced from the
   official CC0 Blender Foundation / Blender Studio Human Base Meshes v1.4.1
   stylized bodies, converted from their source A-pose to the project T-pose,
   manually region-weighted, deformation-tested, and rebound to the accepted rig.
3. Class-specific first-party equipment, hair, weapons, UVs, and PBR maps authored
   over that shared body/rig contract.

The exported game FBX must contain only the clean deform hierarchy, skinned
renderers, and explicitly approved sockets. Rigify controls, MPFB helpers, source
shape keys, cameras, lights, and generator dependencies are authoring-time data
and must not enter Unity. No production mesh, bind pose, weight set, proportion,
or renderer bone may depend on Quaternius or another prototype pack. MPFB remains
an explicitly non-selected comparison/fallback and cannot silently contribute to
the Human Base lineage.

Rejected prototype-derived Hunter studies are retained only in explicitly
historical/rejected source directories for auditability. They are not valid inputs
to this pipeline and may not contribute geometry to a new production candidate.

See `Docs/Art/PRODUCTION_CHARACTER_PIPELINE.md` and the per-character manifests
under `SourceArt/Blender/Characters/` for exact versions, commands, hashes, LOD
budgets, and visual acceptance status.
