# Sunbreak scanned-surface provenance

Intake `POLYHAVEN-2026-002`, acquired 2026-09-05 for local Sunbreak Basin
naturalistic-material evaluation. Status: `TECH_REVIEW`; the scan source is not
evidence of final gameplay-camera art or mobile performance acceptance.

| Source | Contributor | Physical tile | Intended role |
|---|---|---|---|
| [Sparse Grass](https://polyhaven.com/a/sparse_grass) | Amal Kumar | 2 m × 2 m | Soil with fine living grass and organic litter |
| [Rocks Ground 02](https://polyhaven.com/a/rocks_ground_02) | Rob Tuytel | 2 m × 2 m | Exposed compact soil, gravel and rock |
| [Rock 08](https://polyhaven.com/a/rock_08) | Rob Tuytel | 1.5 m × 1.5 m | Pale weathered geological rock |
| [Bark Brown 02](https://polyhaven.com/a/bark_brown_02) | Rob Tuytel | 1 m × 1 m | Trunk and branch bark |
| [Leafy Grass](https://polyhaven.com/a/leafy_grass) | Charlotte Baglioni | 2 m × 2 m | Revision-two greener grass/leaf-litter base |
| [Dirt Floor](https://polyhaven.com/a/dirt_floor) | eye-candy.xyz | 2.07 m × 2.07 m | Revision-two fine soil without large white gravel |

The official source pages and metadata report the physical widths above. The
original four sets retain unmodified 2048 × 2048, 8-bit JPEGs; the two revision-two
ground sets use unmodified 1024 × 1024 JPEGs served by `dl.polyhaven.org`.
All 18 delivered files match the official API's MD5 and byte length. The project's
SHA-256 hashes, roles, exact URLs and lengths are retained in `manifest.json`.
Texture payload: **41,996,162 bytes (40.05 MiB)**. The separate retained CC0 legal
code is 7,048 bytes.

Revision two followed actual gameplay-camera review: sparse grass read as an
orange-brown carpet and Rocks Ground 02 produced conspicuous bright repeated
pebbles. Their six files remain untouched comparison sources. Leafy Grass and
Dirt Floor add only **5,633,455 bytes (5.37 MiB)** and are selected for the next
local review build. Raw albedo review confirmed greener fine plant cover and
quieter soil; this does not substitute for the next gameplay-camera check.

## Rights and acquisition

The [Poly Haven asset license](https://polyhaven.com/license) identifies these
assets as **CC0 1.0 Universal**, permitting commercial use, modification and
redistribution without required attribution. Provider and artists are credited
here voluntarily. `CC0-1.0.txt` is the unchanged
[official Creative Commons legal code](https://creativecommons.org/publicdomain/zero/1.0/legalcode.txt),
SHA-256 `a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499`.
No account, purchase, subscription, receipt or seat license was needed.

Acquisition used bounded official public-API metadata queries and direct CDN
downloads, with user agent `SessionRpg-Sunbreak-AssetIntake/1.0` for the intake
requests. The [API terms](https://github.com/Poly-Haven/Public-API/blob/master/ToS.md)
were reviewed separately from the asset license. The game has no live Poly Haven
API or network dependency. No website render, preview, thumbnail, logo or webpage
copy is included.

These are third-party scanned PBR sources, not first-party generated artwork.
The source pages credit the named contributors; they do not disclose an AI
generation step. Absence of disclosure is not proof of every processing detail.

## Import and integrity boundary

- Preserve these delivered JPEGs and their stable `.meta` identities. Do not
  repaint, crop, color-correct, pack or resave the originals.
- Diffuse/`col` maps represent sRGB color. `nor_gl` is OpenGL/+Y normal data;
  roughness is linear scalar data. None is an albedo-derived false normal.
- `SunbreakScannedSurfaceAuthoring.cs` owns bounded Unity import settings and bindings.
  Derived textures, if needed, belong in generated project art, with reproducible
  source-owned conversion. Normal/roughness must not be sampled as sRGB.
- All other sizes, height/displacement, AO/ARM, DirectX normals and model/package
  variants were excluded. No terrain silhouette is baked into these close-scale
  maps, and no displacement is authorized to affect deterministic gameplay.
- New importer `.meta` files are generated/configured by the normal Unity
  authoring rebuild; the acquisition step does not hand-author import settings.

Fresh source hashing and dimensions passed during intake. Unity import, rendering,
all-aspect gameplay review, user art acceptance, Android memory, shader cost and
physical-device profiling are separate integration gates. Roll back by changing
the Sunbreak source-level binding and rebuilding, preserving these originals.
