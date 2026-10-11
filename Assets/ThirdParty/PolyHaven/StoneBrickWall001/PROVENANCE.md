# Poly Haven Stone Brick Wall 001 provenance

## Identity and rights

- Stable intake ID: `POLYHAVEN-2026-001`.
- Poly Haven asset ID: `stone_brick_wall_001`.
- Display name: Stone Brick Wall 001.
- Asset type: seamless scanned PBR wall material.
- Provider and distributor: Poly Haven.
- Contributors named by the asset page: Dimitrios Savva (photography) and Rico
  Cilliers (processing).
- Official asset page: https://polyhaven.com/a/stone_brick_wall_001
- Official Poly Haven asset-license page: https://polyhaven.com/license
- License: Creative Commons Zero v1.0 Universal (`CC0-1.0`). Poly Haven states
  that its assets may be used, modified, redistributed, and included in commercial
  work without required attribution.
- Retained legal code: `CC0-1.0.txt`, downloaded from
  https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt
- Retrieval date: 2026-09-02.

Attribution is not required by CC0, but the provider and contributors remain
credited here and in the project third-party notice for provenance.

## Acquisition

The five selected 2K PNG maps were downloaded individually from the official
`dl.polyhaven.org` asset CDN URLs linked by the asset page. This was a one-off
manual source intake; the Poly Haven public API was not used. Exact source URLs,
byte lengths, bit depths, and SHA-256 hashes are recorded in `manifest.json`.

The asset page describes the material as 2.5 m wide. At 2K it reports approximately
8.2 pixels per centimetre. The retained maps are:

- diffuse/albedo, intended for sRGB sampling;
- OpenGL (`+Y`) tangent-space normal, intended for non-color sampling;
- ambient occlusion, intended for non-color sampling;
- roughness, intended for non-color sampling; and
- displacement, retained as a source-only height reference for offline mesh or
  material derivation. It is not approved as an additional mobile runtime sample.

The DirectX normal and combined AO/roughness/metallic map were not imported. Unity
URP can consume the retained channels through an explicitly reviewed material or a
deterministic packed derivative; retaining the independent originals avoids an
untracked channel conversion.

## Project boundary

Status is `TECH_REVIEW`. This directory remains an immutable third-party source
intake; the status change records a bounded runtime-candidate binding, not
production-material acceptance:

- `AuthoringAssets` selects the untouched diffuse and OpenGL/+Y normal sources for
  the shared `RuinStone` material used by the first-party fortress geometry;
- `Tools/Art/Blender/prepare_fortress_pbr_maps.py` validates this manifest's source
  hashes and derives the 1024 px linear
  `Assets/SessionRpg/Art/Environment/Textures/FortressMasonryMask.png`;
- the derivative packs R metallic `0`, G ambient occlusion, B displacement
  reference, and A smoothness clamped from `1 - roughness` to `0.02..0.42`; current
  URP Lit sampling does not consume B;
- the exact derivative record and output SHA-256 are retained in
  `SourceArt/Blender/Environment/Textures/FortressMasonryPbr.manifest.json`;
- Unity authoring constrains diffuse to sRGB, normal/mask to linear data, all three
  to 1024 px maximum, repeat/bilinear/anisotropy-4 sampling, mipmaps, non-readable
  CPU state, and an Android compressed override; and
- no source PNG was modified, no first-party texture was overwritten, and no Core
  collision, gameplay geometry, or arena footprint changed.

The authoring selection still requires a fresh generated rebuild and validation at
the current revision. More importantly, `TECH_REVIEW` does not mean that the user
accepted the resulting look: tiling, seams, dark-basalt stylization, gameplay-camera
coherence at Arena yaw `90`, `30`, and `150`, all required aspect ratios, shader and
texture memory, sustained frame time, and thermal behavior remain pending. Physical
Android evidence is required before production promotion.
