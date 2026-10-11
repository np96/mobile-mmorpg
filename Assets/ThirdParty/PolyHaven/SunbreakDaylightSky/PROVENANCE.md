# Sunbreak daylight sky — immutable source intake

- Intake POLYHAVEN-2026-006; TECH_REVIEW, not final visual/device acceptance.
- Asset: [Kloofendal 48d Partly Cloudy (Pure Sky)](https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky).
- Greg Zaal: original photography/capture. Jarod Guest: provider sky edits. Voluntary credits.
- Acquired 2026-09-07 from the [official file API](https://api.polyhaven.com/files/kloofendal_48d_partly_cloudy_puresky); no account, purchase or paid-service change.
- [Poly Haven license](https://polyhaven.com/license): CC0 1.0 Universal; commercial use, modification and redistribution permitted without required attribution.
- [CC0 legal text](https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt) is retained unchanged as CC0-1.0.txt with SHA-256 `a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499`.
- No provider thumbnail, preview render, screenshot, logo, avatar, or web copy is included as game art.

## Source verification and selection

The only sky binary in this intake is the untouched official 1K HDR delivery:
1,435,119 bytes; published MD5 `c69498687e876bf68d2a8b8a62234eb9`;
independent SHA-256
`fd94c84997b8a3c353b62c2125a9b44e19509956986a126e472684432a02d798`.
The exact official download URL is retained in manifest.json.

The original HDR was independently opened in Blender 5.2.0 LTS, confirming
1024×512 equirectangular dimensions and positive RGB values from 0.04296875
through 61,184. A temporary AgX viewing conversion was inspected outside Assets;
it is diagnostic only, not runtime art. The source binary has not been cropped,
painted, tonemapped, relit, or converted into an LDR replacement.

The ordinary non-pure-sky variant was inspected first and rejected because
suburban buildings are visible at its horizon. That candidate was moved out of
Assets to build/review/sky-intake-candidates for recoverable review history.
The final provider-authored pure-sky version contains cloud atmosphere with no
buildings, photographed terrain or embedded scene props. Its lower hemisphere
is the provider's sky extension, not game geometry. No local editing of the sky
was performed.

## Unity authoring contract

SunbreakDaylightSkyAssets.ConfigureSky() configures only this import and returns a
saved material at Assets/SessionRpg/Art/Materials/Sunbreak_DaylightSky.mat.
It never binds RenderSettings, alters ambient/reflection/fog/light settings,
spawns geometry, or modifies a camera. The integrating world profile owns scope.

The built-in Skybox/Panoramic shader uses the original Texture2D, full
360-degree latitude-longitude mapping, no stereo, zero rotation, exposure 1 and
neutral 0.5 RGB tint (the shader's neutral convention, not white's additional
brightness multiplication). The exact `_MAPPING_LATITUDE_LONGITUDE_LAYOUT`
keyword is authored and validated; no six-frame/cubemap or unrelated keyword is
enabled. Unity's KeywordEnum material drawer installs this latitude-longitude
keyword on a fresh load, so an empty keyword list is not a stable contract.
[Unity panoramic documentation](https://docs.unity3d.com/6000.0/Documentation/Manual/shader-skybox-panoramic.html)
documents this URP-supported sky mapping.

The texture is linear HDR, never sRGB or an inferred cubemap. Explicit RGB9E5
shared-exponent HDR imports preserve positive brightness at four bytes per texel:
desktop 1024×512, Android cap 512×256, mipmapped/trilinear, horizontal repeat and
vertical clamp. Approximate resident texture payload including mips is 2.67 MiB
desktop and 0.67 MiB Android, excluding driver overhead. These are format estimates,
not a measured device-memory or performance result. Source pixels remain intact.
[Unity RGB9E5 reference](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/TextureImporterFormat.RGB9E5.html).

ConfigureSky repairs only missing/drifted import/material settings. Validate is
read-only and fails for missing or changed binary/license hashes, dimensions,
HDR format, mapping or material bindings. Focused tests cover the source hash,
bounded HDR import, full panorama material and absence of global lighting writes.

## Handoff boundary

No Unity command was run for this intake subtask. Root owns import/build/tests,
sky rotation alignment with the directional light if later approved, actual
in-game horizon/cloud review, Android build, allocations/performance and physical
device verification. Source acquisition and good-looking clouds alone do not
constitute production acceptance.
