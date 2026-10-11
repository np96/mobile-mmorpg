# Sunbreak natural surfaces V1

First-party AI-generated albedo sources created on 2026-09-04 with the built-in
image-generation tool through the imagegen skill. These are not photographed scans,
PBR maps, final art approval, or evidence of mobile performance. No third-party
source image was supplied. Source PNGs are preserved unedited at the paths below;
the Unity importer creates bounded compressed runtime copies.

The user rejected the basin's toy-like graphics. These textures support a more
naturalistic, weathered fantasy direction without darkening the daylight location.

| Source | SHA-256 |
| --- | --- |
| MeadowAlbedoV1.png | 9132f1abc42d2db316eefe6517a65ec3df463a3891487165723f72f2e2c4927e |
| LimestoneAlbedoV1.png | 11b290a7f0694b34c92e19bc743cc257d6a3a608ed2ec58f8cc2a3bfac118015 |
| BarkAlbedoV1.png | 65b206997712706fee41cf82174d66a07fb694d28099723e2e435e64cf9ba93b |
| LeafAlbedoV1.png | 74eeab7aa896c85bf21405b3b71f598a4715dac833729d58e77925a9a3d89789 |

All four masters are 1254 × 1254 pixels. `SunbreakSurfaceAuthoring` imports ground,
rock and bark at 1024; leaf at 512, with mipmaps, trilinear filtering, repeat wrap,
anisotropy 8, sRGB color, no runtime CPU-readable copy, no alpha or height-to-normal
conversion. Android ASTC 6×6 settings are authored, not an Android build/device pass.
Geometry supplies the small leaf silhouettes; there are no transparent tree cards.
Existing licensed Poly Haven masonry remains separate from these generated sources.

## Exact prompts

### MeadowAlbedoV1.png

Use case: photorealistic-natural. Asset type: seamless tileable albedo texture for a 3D fantasy game's meadow ground, not a concept scene. Primary request: an orthographic overhead square material scan of natural worn temperate meadow soil, approximately 4 metres square. Dense very fine short muted sage-green grass interwoven with warm grey-brown dry soil, scattered small limestone grit, a few tiny dry straw blades and minute dead leaves. Mature naturalistic grounded fantasy materials, realistic fine tactile surface detail. Grass covers about 55 percent in ragged irregular tufts, soil 40 percent, tiny pale stone grit 5 percent. Low-to-moderate contrast, muted neutral greens and taupe, no fluorescent green or yellow. Flat diffuse overcast illumination for albedo; no directional sunlight, large shadows or highlights. Exact overhead orthographic view, fills the complete square with uniform texel density. Seamlessly tileable on all four edges with no border, no vignette, no central focal motif, no large stones, no paths or road, no flowers, no large plants, no buildings, no scene perspective, no horizon, no props, no lettering or watermark. The image itself must be only the texture, square format, detailed enough for a 2048-pixel game material. Avoid cartoon brushwork, low-poly shapes and plastic surfaces.

### LimestoneAlbedoV1.png

Use case: photorealistic-natural. Asset type: seamless tileable albedo texture for natural eroded limestone boulders in a 3D fantasy environment. A square orthographic material scan of weathered pale warm-grey limestone, fine sedimentary grain, hairline fractures, mineral speckles, very sparse subdued olive-grey lichen. Natural irregular stone surface; absolutely no masonry, bricks, paving slabs, grout lines or repeated blocks. Mid-value warm neutral grey, restrained color variation and contrast, authentic tactile details. Flat diffuse overcast illumination for albedo only, no cast shadows, no direct sun, no specular highlights, no large black cracks. Seamlessly repeatable on all four edges, full bleed, consistent texel density, no vignette or focal object. No scene, no perspective, no text, no watermark, no cartoon or low-poly aesthetic.

### BarkAlbedoV1.png

Use case: photorealistic-natural. Asset type: seamless tileable bark albedo texture for 3D tree trunks in a naturalistic fantasy game. Orthographic square close-up surface scan, rugged weathered ash-tree bark with long vertical interlocking fissures, small flaking scales, warm grey and brown with sparse subtle pale lichen. Mature real wood, finely textured; no plastic or cartoon style. Moderate shallow contrast, flat diffuse overcast neutral lighting for an albedo map, no directional shadow, no highlights, no perspective, no round trunk silhouette. Bark surface fills the entire square. Seamlessly tileable on all four edges, avoid strong repeating knots or focal motifs, no text or watermark.

### LeafAlbedoV1.png

Use case: photorealistic-natural. Asset type: opaque albedo texture for small individual 3D tree leaves in a naturalistic fantasy game. Exact overhead macro photograph-style scan of a single ordinary narrow oval pointed oak-like leaf, central vein running vertically from bottom to top, very fine branching veins, slightly leathery organic surface, muted deep sage and dusty grey-green, subtle brown speckles. Leaf surface fills the entire square edge to edge; no empty background, no leaf silhouette cutout, no stem outside it. Lighting is flat diffuse overcast, no cast shadows or gloss highlights. Restrained contrast so the surface reads naturally from gameplay distance; no saturated lime, no glow, no illustration style, no letters or watermark. Square texture only.


