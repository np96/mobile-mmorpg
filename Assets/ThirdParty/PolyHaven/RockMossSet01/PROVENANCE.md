# Poly Haven Rock Moss Set 01 — bounded Sunbreak benchmark

Status: `TECH_REVIEW`, 2026-09-07. This is a third-party scanned source trial,
not accepted production art or a performance claim.

- Source: https://polyhaven.com/a/rock_moss_set_01
- Creator: Kless Gyzen (all contributions), distributed by Poly Haven.
- Official metadata: https://api.polyhaven.com/info/rock_moss_set_01
- Official file manifest: https://api.polyhaven.com/files/rock_moss_set_01
- License: CC0 1.0 Universal, https://polyhaven.com/license and the retained
  `CC0-1.0.txt`. The legal-code original is
  https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt.
- Acquisition: four unchanged official CDN downloads, no account or purchase,
  2026-09-07. Public metadata requests identified
  `SessionRpg-ArtIntakeResearch/1.0` as User-Agent. This project does not call the
  live API at runtime.
- Exact source URLs, byte counts, official MD5 and acquired SHA-256 hashes are
  in `manifest.json`. Preserve original bytes and Unity identities.

CC0 permits commercial use, modification and source redistribution without
attribution. We retain author/provider credit voluntarily and do not claim these
scans as original Session RPG artwork. Website preview images, logos and marketing
copy are not part of this intake. No AI-generation step is disclosed in the
source metadata; that is not proof of every processing detail.

Only `rock_moss_set_01_rock03` is admitted to the initial benchmark: one opaque
renderer in PvE Leveling camp 1 / landmark 7, with exactly 5,000 triangles and one
dedicated generated material. The other five source meshes are preserved but not
instantiated. The six-model source totals 63,127 triangles. No LOD variant is
published in the inspected manifest; texture resolution does not reduce geometry.

`SunbreakScannedRockAuthoring` owns model-only imports, original normals/UVs,
MikkTSpace tangent generation, 1K texture limits and Android ASTC overrides. The
diffuse is sRGB, roughness is linear, and the EXR is an actual OpenGL/+Y normal
source rather than generated height-from-color. Material construction uses the
existing `SessionRpg/Sunbreak Terrain` shader, with layer blending disabled.

The authoring helper recenters X/Z, puts the mesh's minimum Y at the requested
ground elevation, preserves uniform proportions, and limits horizontal radius to
1.5 m. It creates no collider, physics, animation, light, runtime updater or
gameplay authority. Root placement still must pass the Core-cover validator.

Final gameplay-camera coherence, camera/aspect coverage, physical Android cost,
LOD/repetition budgets and explicit user acceptance remain open. Do not promote
this source to `ACCEPTED_PRODUCTION` from successful import alone. To roll back,
remove the benchmark construction, its Basin admission-validation requirement,
and the corresponding exact-one generated-scene test together, then rebuild.
Preserve this original delivery and its provenance.
