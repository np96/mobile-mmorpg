# Sunbreak ruined masonry V1 — explicitly modified source derivative

This editor-authored derivative replaces the intact foreground wall and corner
at Sunbreak landmark 7. It is a bounded local graphics candidate, not accepted
production art. Original admitted meshes and original third-party delivery bytes
remain immutable; this derivative is separately named and separately generated.

## Approved input and provenance

The only model input is the already admitted Poly Haven Modular Fort 01 by Rico
Cilliers, intake `POLYHAVEN-2026-004`, CC0 1.0 Universal. Its original FBX, texture
maps, exact file sizes and hashes remain governed by
`Assets/ThirdParty/PolyHaven/ModularFort01/manifest.json` and `PROVENANCE.md`.
No additional download, source import, purchase or license change is involved.

The selected original nodes are
`modular_fort_01_wall_thin_straight_04` (the admitted battlement wall) and
`modular_fort_01_wall_thin_corner_01`. The authoring helper obtains the exact
admitted render mesh through `SunbreakFortAssets`; its vertices are transformed
using Unity's actual imported hierarchy, including the established source-axis
conversion. It does not reinterpret raw FBX axes or claim modified geometry is
the unmodified original source.

## Reproducible geometry

`Assets/SessionRpg/Editor/Authoring/SunbreakRuinMasonryAuthoring.cs` is the source.
`Build(parent, groundHeight)` publishes one stable generated mesh at
`Assets/SessionRpg/Art/Environment/SunbreakRuinMasonryV1/BrokenAbbeyMasonry.asset`.
The integrating generator owns the normal rebuild; do not edit generated YAML.
`CreateGeometry()` returns disposable world-space wall, corner and rubble meshes
for tests without saving or modifying any asset.

Both source pieces retain a positive uniform scale of 0.52. The wall is grounded
at `(-43.2, 0.05, 24.2)`, yaw 0 degrees; the corner at
`(-42.675, 0.05, 27.1)`, yaw 90 degrees. The 90-degree corner placement is required
by the previously measured Unity-imported joint, not inferred from renderer
bounds. Hidden overlap is removed at the common `z = 26.02` section. The visible
body faces retain their source positions, battered profile and physical scale.

The complete intact parapet/trim role is removed. Source wall-role triangles are
clipped to the explicit uneven height profile, from 1.32 to 3.36 metres, along
17 successive Z intervals. Exposed upper boundaries are joined into closed
loops and triangulated with concavity-preserving ear clipping. A convex hull is
not used to bridge the L-shaped corner. There is no vertex clamping into Core
or nonuniform deformation of the surviving source faces.

The first native capture exposed an overly regular diagonal saw-stair profile.
The revised profile retains two broad masses separated by a deep central
collapse around `z = 24.1–24.9`, rather than successively higher small teeth.
The common joint remains at 3.36 m and the minimum cut height at 1.32 m.

Three bounded convex offcuts are extracted from the same source body, cut faces
are capped, and each piece is rigidly rotated into grounded debris. Their XZ
centres are `(-43.15, 21.15)`, `(-44.8, 22.7)` and `(-45.75, 24.1)`. Each lowest
point is embedded 5.5 cm beneath the existing bank height sampled at that centre.
This does not assert that every underside triangle conforms to arbitrary terrain;
the bank is existing presentation cover and is not changed by this helper.

Surviving and clipped source-side vertices retain source UV interpolation,
normals and orthonormalized tangents. Newly exposed cut caps necessarily have
new planar UVs and geometric normal/tangent frames. All use the already admitted
opaque measured wall material, at its existing source settings; no dark tint,
painted normal map, new texture or camouflage material is added.

## Geometry and runtime contracts

- One MeshFilter/MeshRenderer and one opaque material, replacing the original
  two pieces/four material submissions. Shadows may add render passes; one
  renderer is not a claim of one total GPU draw call.
- Hard cap of 1,276 triangles, no higher than the replaced original wall/corner.
  The integrating chunk's independent 65k cap remains required.
- Every generated vertex, triangle centre and edge midpoint must lie in the
  exact existing three-circle landmark-7 Core union, including rubble bases.
- Continuous solid lower courses and the source-derived perpendicular return
  remain. Four real triangle cross-sections test the common joint to the existing
  1.2 cm tolerance; no tolerance relaxation or bounding-box substitute is used.
- No new collider, Rigidbody, gameplay state, runtime component or per-frame
  allocation. All clipping, temporary source objects and mesh allocations occur
  only in editor authoring/tests. The flat Core collision contract is unchanged.

## Verification boundary

`SunbreakRuinMasonryAuthoringTests` covers closed cap rays at rises and notches,
the separate corner return, six-direction closed-volume rubble probes, finite
orthonormal frames, exact footprint samples, deterministic output, the existing
triangle cap, ground sampling, and unchanged original mesh/material bytes.
`SunbreakCourtyardCompositionTests` checks generated derivative presence and
binding, rejected intact foreground modules, aggregate architecture budget,
lower courses and real source-derived joint sections. Landmark-0 original-module
contracts remain unchanged.

The integrating task owns fresh Unity validation/test counts, native screenshots,
visual acceptance and device evidence. A passing source build or closed-cap test
does not establish convincing fracture detail, production ruin quality, Android
performance, allocation measurements or physical-device acceptance.
