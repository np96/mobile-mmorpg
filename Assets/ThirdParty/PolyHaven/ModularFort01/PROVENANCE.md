# Modular Fort 01 — immutable source intake

- Intake: POLYHAVEN-2026-004. Status: TECH_REVIEW, not accepted final art.
- Creator: Rico Cilliers, all contributions, voluntarily credited.
- Asset: https://polyhaven.com/a/modular_fort_01
- Official file metadata: https://api.polyhaven.com/files/modular_fort_01
- Acquisition: 2026-09-07, direct official delivery; no purchase or account required.
- License: CC0 1.0 Universal. Poly Haven license https://polyhaven.com/license and legal text https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt reviewed on acquisition.
- Copyright/trademark caveat: source is an unbranded fictional stone fort. Provider logos, page screenshots, previews and site text are not imported.
- Scope: one local Sunbreak Basin camp 1 composition and neighboring major landmark 0. No blanket replacement and no Arena, Dungeon or Training bindings.

## Immutable originals and reproducibility

The FBX and six measured channel maps in this directory are untouched official
downloads. All seven official MD5 checksums and byte counts were verified at
download. The manifest records each exact URL, size, MD5 and independently
computed SHA-256; the editor integrity gate checks SHA-256 and exact bytes before
configuration. Binary source total is 32,523,603 bytes.
CC0-1.0.txt is the unmodified legal text, SHA-256
`a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499`.

The filename's 1k token describes the provider's model delivery texture references,
not a low-poly geometry level. The original has 22 mesh objects and 28,218
triangles, independently counted by triangulating FBX polygons in Blender 5.2.0
LTS without modifying or saving the source. No published LODs are claimed.
The immutable admission pipeline does not decimate, repaint or rework the source
meshes/UVs, or manufacture raster normals. The full source showcase layout is never
instantiated. A separately named, explicitly modified editor derivative now clips
and caps two admitted pieces for the foreground ruin; its procedure and unchanged
source boundary are recorded in
`SourceArt/Environment/SunbreakRuinMasonryV1/README.md`. It never replaces these originals.

## Authoring and physical scale

`SunbreakFortAssets` selects only exact stable node names recorded in the
manifest. Six admitted nodes retain their exact two-submesh wall/trim triangle
counts. The original FBX axis rotation and positive uniform unit conversion are
preserved, with an explicit 90-degree presentation yaw because Unity's raw FBX
import exchanges X/Z relative to these canonical dimensions. Showcase
translations are discarded. The selected mesh is centered
in XZ and grounded at its minimum Y, then uniformly scaled and yawed by the
composition. Physical Y-up bounds are checked against original FBX dimensions
and the official glTF descriptor. There is no axis flattening or nonuniform fit.

Unity 6000.5.6f1's imported material order differs from Blender's slot array:
the tower, low wall, battlement wall, end and thin corner have trim in submesh 0 and wall in
submesh 1; the gate has wall 0 / trim 1. A focused read-only Unity diagnostic
measured this after the initial strict gate rejected the assumed order. The
helper binds an explicit per-module map with exact per-role triangle counts;
it does not reorder or regenerate source mesh indices or UVs.

The 2026-09-08 foreground revision explicitly admits the existing
`modular_fort_01_wall_thin_corner_01` node from the same unchanged FBX. Its 654
triangles comprise 64 wall and 590 trim triangles; canonical dimensions are
4.563530 × 8.605457 × 4.557765 m. The source manifest's module metadata records
this bounded selection; original file sizes, URLs, hashes and bytes are unchanged.
One genuine corner replaces three differently scaled foreground wall pieces.
It shares 0.52× scale and the 0.05 m base with the adjoining battlement wall.
The actual imported corner uses a 90-degree placement yaw: the first source-only
axis prediction put its broad return across the joint, and four strict Unity
cross-section tests rejected that placement. Real triangle cross-sections,
not only renderer bounds or raw-FBX projections, verify the corrected joint.
This is construction-layout improvement, not a fractured production-ruin mesh
or a new artistic acceptance.

Only Transform, MeshFilter and MeshRenderer are created. There are no imported
lights, cameras, animation, skinning, colliders or gameplay components. Placement
must fit all mesh vertices inside the existing Core obstacle unions; the
authoring composition owns this guard. A visible gate opening is not used in the
benchmark because it would imply passage through Core collision. Whole-composition
architecture budget is at most 10,000 triangles, with two shared opaque materials;
two material types are not a claim of two draw calls.

## Texture and material contract

Wall and trim each use the original 2048-square diffuse JPG, OpenGL +Y normal EXR,
and roughness JPG. All six dimensions were independently inspected (JPG with
sips, EXR with read-only Blender); the import validator also checks original
dimensions for all six maps. The provider's source
UVs and two-material ordering remain intact. Diffuse imports sRGB; normal and
roughness import linear. GL normals import as NormalMap, without green-channel
flip or conversion from color. Source normals and generated MikkTSpace tangents
remain present on every admitted mesh.

The existing opaque Sunbreak Terrain shader uses measured source UV maps at unit
tiling, white tint, normal strength 1, roughness multiplier 1 and no fabricated AO,
packed masonry or second layer. No plaster map is required by these modules.
Imports cap desktop maps at 2048 and Android at 1024; Android normals ASTC 4x4,
color/roughness ASTC 6x6. Mips/streaming flags are enabled, not a claim that runtime
streaming or physical-device performance has been accepted.

## Review boundary

`Configure()` explicitly repairs only changed import or material settings;
`Validate()` is read-only and fails on missing sources, hash drift, bad channels,
module ambiguity, changed material-slot triangles or incorrect scale/up axis.
New focused EditMode tests cover the immutable manifest, imports, source bounds,
UV reuse, positive uniform grounded placement, material binding and invalid input.

Unity import/build/tests, material appearance, final placement, Android packaging,
allocation/performance and physical-device visual acceptance remain the integrating
task's verification responsibility. TECH_REVIEW must not be promoted by source
download or triangle-budget compliance alone.
