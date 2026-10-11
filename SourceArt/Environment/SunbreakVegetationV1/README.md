# Sunbreak vegetation V1 — source-derived mobile trial

Intake POLYHAVEN-2026-005, CC0, TECH_REVIEW. This is not user acceptance,
Android performance evidence, or final production content.

The unchanged official deliveries and original byte/hash/license manifests are
under Assets/ThirdParty/PolyHaven/{TreeSmall02,GrassMedium01,Fern02}.
Model files have a `.source` suffix to prevent accidental full-density Unity
import; no source bytes were trimmed or replaced.

Reproduce from repository root with Blender 5.2.0 LTS:

```sh
/Applications/Blender.app/Contents/MacOS/Blender --background --python Tools/Art/Blender/prepare_sunbreak_vegetation.py -- --bake --render
```

The script verifies every original size, MD5 and SHA-256 before generation.
It never saves over the original Blend/FBX files.

TreeSmall02's published Blender LOD1 has 376,155 polygons, so it was not admitted
as a mobile tree. The derivative keeps its scanned primary trunk/branches at
3,000 triangles. Fine Geometry Nodes twig geometry is omitted, not collapsed
into broken long triangles. The four intact authored LOD1 leaf clusters are
rendered from two perpendicular orthographic directions and replaced with two
cards at source Geometry Nodes positions/rotations. Original curve resolution
is retained; fixed density controls and a documented 1.134615 cluster visual
scale give a source-derived trial canopy at 6,328 foliage triangles, 9,328 total.
Later controlled review found the foliage too thin for production acceptance;
the initial eight-transparency-bounce preview overstated its density.

The three 1024×2048 atlases contain eight actual 512-square source cluster views:
unlit source color with true coverage alpha; measured GL normal transformed to
the card-camera tangent basis; and measured linear roughness. No lighting, AO,
height, or roughness is fabricated from albedo. CPU Cycles with a fixed seed and
16 fixed samples creates the bakes. Intermediate renders remain in ClusterBakes.
Shader color/depth/depth-normal/shadow passes all use the same alpha cutoff.

Grass preserves six exact small source tufts (79/28/40/32/62/60 triangles).
Fern02a preserves the exact 784-triangle source mesh. Neither is decimated.
The JSON buffers are in meters, Unity Y-up, XZ-centered at a minY ground pivot;
source corner normals/UVs survive conversion, with reflected winding corrected.
Unity authoring computes mesh tangents and creates bounded generated mesh assets.

The generated manifest locks procedure, original intake manifests, final atlas
bytes/hashes, mesh bytes/hashes, counts and physical bounds. Runtime/import
caps are 2K desktop and 1K Android, with linear data/normal versus sRGB color.

DCC-only review images live in build/review/sunbreak-vegetation-dcc/tree-1.png
and tree-2.png. Those initial previews do not establish canopy density. Controlled
64-bounce, gameplay-scale review is in build/review/sunbreak-vegetation-native-scale/.
These are mesh/material checks, not playable camera or phone performance evidence.
Gameplay integration and all Unity tests are separate; placing more trees does
not retroactively accept the density of each source tree.
