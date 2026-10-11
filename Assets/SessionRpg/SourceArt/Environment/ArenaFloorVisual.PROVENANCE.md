# Arena floor provenance

- Classification: original first-party project art.
- Generator: `Tools/Art/Blender/build_arena_floor.py`.
- DCC: Blender 5.2 LTS.
- External meshes, textures, scans, or traced symbols: none.
- Authoritative collision or gameplay geometry: none.

The floor is deterministically assembled from five broad original masonry bands,
non-aligned flagstone sectors, restrained inlays, and original annular profiles. It
is not extracted from or modeled over a commercial game's arena.

`ArenaFloorMacroAlbedoSource.png` is original first-party source art generated
in-house with OpenAI image generation on 2026-08-19 for this project. Its checked-in
1024 px runtime derivative, `ArenaFloorMacroAlbedo.png`, is the Stone material's
single BaseMap. The mesh maps world X/Y `[-15,+15]` exactly to UV0 `[0,1]`, so the
macro composition appears once at tiling `1` and offset `0`. There is no texture
overlay mesh and no second albedo blend.

`arena_floor_surface_maps.py` derives the micro normal and packed mask from the
project's original 1254 px `ArenaFloorSlateMicrodetailSource.png`. That source was
also generated in-house with OpenAI image generation on 2026-08-19 and contains no
mortar, rune, logo, traced symbol, or commercial-game asset. The detail normal
repeats at `4.6875` with offset `(0.47125, 0.64125)`, preserving the earlier 6.4 m
detail scale and phase. The derived micro albedo remains diagnostic and is not
bound to ArenaFloorVisual.

The generator derives `ArenaFloorMacroNormal.png` directly as an explicit
OpenGL/+Y tangent normal from local relief in the first-party macro albedo. It also
phase-bakes the micro packed mask into `ArenaFloorMacroMask.png` across the complete
floor UV at `fract(uv * 4.6875 + vec2(0.47125, 0.64125))`. This lets Unity bind
base-map AO/smoothness at tiling `1` without drifting from the independently tiled
detail normal. Both masks follow the Unity URP Lit channel contract: R metallic, G
occlusion, B spare height, and A smoothness.

Evidence renders use the same macro and micro transforms as the authored Unity
material. Textures are not embedded in the FBX; Unity continues to assign all
materials.
