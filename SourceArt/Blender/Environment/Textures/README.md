# Arena floor texture sources

`ArenaFloorMacroAlbedoSource.png` is original first-party source art generated
in-house with OpenAI image generation on 2026-08-19 for the Session RPG arena. It
defines the complete dark-slate masonry, restrained bronze inlays, central
medallion, and broad annular composition. The checked-in 1024 px runtime derivative
is `Assets/SessionRpg/Art/Environment/Textures/ArenaFloorMacroAlbedo.png`.

The arena FBX maps its complete 30 m disk once into UV0: Blender/world X and Y
`[-15,+15]` map exactly to UV `[0,1]`. The runtime macro image is the Stone
material's only BaseMap at tiling `1` and offset `0`; it is not an overlay mesh and
is not blended with a second albedo.

`ArenaFloorSlateMicrodetailSource.png` is original first-party source art generated
in-house with OpenAI image generation on 2026-08-19 for the Session RPG arena. It
was prompted as neutral cool slate microdetail with restrained warm mineral flecks,
without mortar, runes, circles, logos, or copied game imagery.

`Tools/Art/Blender/arena_floor_surface_maps.py` deterministically converts this
1254 px source into a 1024 px diagnostic albedo, OpenGL/+Y tangent normal, and URP
Lit packed mask. `_DetailAlbedoMap_ST` supplies tiling `4.6875` and offset
`(0.47125, 0.64125)`, preserving the 6.4 m microdetail repeat and phase after
adopting the 30 m UV. The derived micro albedo remains diagnostic.

The same script derives `ArenaFloorMacroNormal.png` from local relief in the
checked-in macro albedo instead of asking Unity to interpret a duplicate albedo as
height. It also writes `ArenaFloorMacroMask.png`, a full-floor phase bake of the
micro packed mask using `fract(uv * 4.6875 + vec2(0.47125, 0.64125))`. URP's
base-map PBR slots can bind that baked mask at tiling `1`/offset `0`, while its AO
and smoothness still match the detail normal's authored source phase. The packed
layout is R metallic, G occlusion, B spare height, and A smoothness.

No third-party texture, scan, photograph, or commercial-game asset is included.
