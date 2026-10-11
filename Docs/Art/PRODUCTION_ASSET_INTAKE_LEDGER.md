# Production Asset Intake Ledger

This ledger is the auditable gate for any purchased, commissioned, first-party, or
third-party visual/audio source proposed for a player-facing external build. A
valid license or receipt permits evaluation; it does not by itself establish
production quality, originality, technical fitness, or inclusion in a release.

## Current production-replacement state

**No commissioned or commercially purchased production replacements are present
as of 2026-09-02. The selected Mixamo models are licensed, customizable
embedded-game bases in `TECH_REVIEW`; Poly Haven Stone Brick Wall 001 is a separate
CC0 `TECH_REVIEW` runtime candidate for fortress masonry. There are no
`ACCEPTED_PRODUCTION` entries yet.**

The retained Mixamo characters use `promotionAllowed:true` hash-locked provenance.
Their validators now admit Development and non-Development embedded game builds.
Adobe's official terms/FAQ permit royalty-free commercial-game use and modification
of Content Files before they are embedded/distributed only with the game. Raw or
standalone original/modified FBX redistribution is prohibited. The user accepted
these models as the customization direction on 2026-09-01. Deformation, animation,
mobile performance, and final visual customization remain technical/product work.
Erika's raw animation and prop findings remain rejected; commissioning is optional
for brand/quality, not an Adobe license requirement.

Retained KayKit and Quaternius packages are documented CC0
prototype/rollback sources, but the current art-direction records exclude their
character lineage from production. Blender Human Base Meshes is a selected CC0 DCC
anatomy source, not an accepted runtime character. Exact current-source notices and
hashes remain in `THIRD_PARTY_NOTICES.md` and the linked provenance records.

The Poly Haven candidate replaces neither the first-party fortress meshes nor their
gameplay contract. It contributes diffuse/normal source data and a reproducibly
packed mask to the shared `RuinStone` evaluation material. The current game-camera
look and physical Android cost have not been accepted.

## Status vocabulary

| Status | Meaning |
|---|---|
| `PROPOSED` | Candidate named; evidence or review is incomplete. It cannot enter an external build. |
| `TRIAL_ONLY` | Approved only for a guarded development build. It cannot be promoted by visual quality or a successful import. |
| `LEGAL_REVIEW` | Source, receipt/contract, and license are retained; permitted use or redistribution still needs a decision. |
| `ART_REVIEW` | Legal intake is complete; gameplay-camera originality/coherence is not accepted. |
| `TECH_REVIEW` | Art direction is selected for bounded technical/runtime evaluation; final gameplay-camera art approval, materials, deformation, animation, LOD, memory, or device budgets may remain open. |
| `ACCEPTED_PRODUCTION` | Legal, art, technical, gameplay-camera, and build-composition gates passed for a named version and hash set. |
| `REJECTED` | Candidate is excluded; the reason and retained evidence prevent accidental reintroduction. |
| `RETIRED` | A formerly accepted version is no longer admitted; replacement and migration are recorded. |

Only `ACCEPTED_PRODUCTION` may satisfy the Beta MLP production-art gate. Acceptance
is scoped to the exact asset ID, version, hashes, permitted use, platforms, and
integration recorded below; it is not inherited by a later update or related pack.

## Required evidence per intake

Create one entry from the template for every purchased pack, commissioned delivery,
first-party production source, or material version update. Do not combine unrelated
providers or license versions in one entry.

1. **Identity and scope**
   - Stable intake ID and asset/display name.
   - Product roles: class, creature, environment, UI, animation, VFX, audio, or
     other; exact modes/scenes and excluded uses.
   - Creator, rights holder, distributor/source URL, and acquisition route.
   - Pack/delivery version, release date, acquisition date, and imported revision.
2. **Rights evidence**
   - Exact license name/version, official license URL, and retained local copy.
   - Receipt/order ID or commission contract/SOW and acceptance date. Keep private
     financial or personal data outside the repository; retain a redacted evidence
     copy or stable private-vault reference plus its SHA-256 here.
   - Plain-language permitted-use decision: commercial game use, modification,
     platform/build scope, contractor/team seats, promotional renders, and whether
     generated derivatives may ship.
   - Attribution/notice obligations and where they are fulfilled.
   - Raw-source, editor-project, extractable-file, sublicensing, and asset-pack
     redistribution restrictions. “May ship in a game” never means “may redistribute
     the source pack.”
   - For commissions: written ownership/IP assignment or perpetual commercial
     license, originality/non-infringement representation, third-party dependency
     disclosure, and source-file handoff terms.
3. **Provenance and integrity**
   - SHA-256 for the original archive/delivery, retained license/contract evidence,
     every admitted source file, and the normalized Unity import/derivative set.
   - Reproducible import/configuration command or authoring path, selector/build
     guard, dependency list, and a record of excluded files.
   - AI/tool disclosure when supplied by the creator or marketplace, plus the
     project's originality/legal decision; absence of disclosure is not proof of
     absence.
4. **Gameplay-camera art acceptance**
   - Art director/owner, target visual brief, silhouette/material/color criteria,
     and comparison against the project's own visual target rather than a copied
     game asset.
   - Representative in-game captures at 16:9, 20:9, and 2.4:1: rest, locomotion,
     anticipation, release, hit/control, death, crowded combat, and desaturated
     readability where relevant.
   - Accepted/rejected date, reviewer, unresolved polish items, and explicit
     originality/coherence result.
5. **Deformation and animation acceptance**
   - Skeleton/Avatar, bind pose, scale, root/pivot, skin weights, twist chains,
     attachment sockets, facial/cloth dependencies, and retarget source.
   - Sampled extreme-pose evidence for shoulders, elbows, wrists, hands, hips,
     knees, ankles, held weapons, casts, knockback, hit, and death.
   - Clip ownership/license, root-motion policy, event timing, cancel/interrupt
     response, and any class-specific draw/release measurements.
6. **Mobile technical budget**
   - Source and rendered triangles, submeshes/materials, textures/formats/mips,
     bones/weights, blend shapes, animation memory, shader variants, VFX/audio
     memory, LOD thresholds, draw/SetPass impact, and pooled instance limits.
   - Measured build type, device/SKU, resolution, quality profile, thermal state,
     test scenario, p95 CPU/GPU/frame time, peak/resident memory, and comparison to
     the current project budget. Editor FPS or an isolated turntable is not device
     acceptance.
7. **Decision and release control**
   - Status, legal/art/technical reviewers, decision date, accepted version/hash
     set, allowed build channels, required notices, and rollback/replacement path.
   - Validator/build-rule reference that rejects unapproved versions or trial
     selectors from external builds.

## Intake entry template

Copy this section once per candidate. Replace every `TBD`; an incomplete entry must
remain `PROPOSED`, `LEGAL_REVIEW`, `ART_REVIEW`, or `TECH_REVIEW`.

### `MIXAMO-2026-001` — Four customizable character bases

| Field | Evidence / decision |
|---|---|
| Status | `TECH_REVIEW` |
| Product role and exact scope | Erika Archer for Hunter; Uriel A Plotexia for Priest; Vampire A Lusth for Rogue; Demon T Wiezzorek for Mage. Embedded runtime characters and temporary derived enemy silhouettes. |
| Explicitly excluded uses | Standalone original/modified asset distribution, public source packages, templates/asset packs, AI/ML use, and the rejected downloaded Erika animation set. |
| Creator / rights holder | Adobe Mixamo / respective Mixamo content rights holders under Adobe terms |
| Distributor and official source URL | Adobe Mixamo — https://www.mixamo.com/ |
| Acquisition route and date | User's signed-in Adobe account, 2026-08-27 |
| Pack/delivery version and release date | Named Mixamo downloads; service has no pack version. Exact acquired files are hash locked. |
| Receipt/order or commission contract/SOW reference | No purchase receipt required; authenticated free-service acquisition is recorded in source provenance. |
| Receipt/contract evidence SHA-256 | Not applicable |
| License name/version and official URL | Adobe General Terms, Content Files section 3.6; Mixamo Additional Terms effective 2021-06-23; official URLs in `Docs/Legal/ADOBE_MIXAMO_USE_RECORD.md` |
| Retained license path and SHA-256 | Project interpretation/evidence record: `Docs/Legal/ADOBE_MIXAMO_USE_RECORD.md`; official terms remain authoritative and are URL/version referenced rather than copied. |
| Permitted commercial/platform/modification/promotional use | Royalty-free commercial game use; manual modification of clothes, hair, face, proportions, mesh, textures, and materials; embedded Android/desktop game distribution. |
| Attribution/notice obligation and fulfillment | Published FAQ says attribution is not required; provider and source remain in `THIRD_PARTY_NOTICES.md`. |
| Raw/source/project redistribution restrictions | Never distribute original or modified Mixamo Content Files standalone, as an asset pack/template, or in a public source package. |
| Commission ownership/IP assignment or commercial-license terms | Not applicable; custom additions remain subject to their own ownership/license evidence. |
| Originality and third-party dependency representation | Mixamo-derived bases; never describe the underlying characters as wholly original. New custom parts require independent provenance. |
| Original archive/delivery path and SHA-256 | Per-character originals and exact hashes in the two source manifests. |
| Admitted source/import/derivative paths and SHA-256 manifest | `SourceArt/Trials/AdobeMixamo/ErikaArcher/manifest.json`; `SourceArt/Trials/AdobeMixamo/PlayableClasses/manifest.json` |
| Excluded files and reason | Erika's four downloaded animation FBXs remain outside runtime `Assets`; draw/recoil/root-motion and embedded weapon staging failed project gates. |
| Import/configuration path and build selector/guard | `configure-mixamo-trial`, `configure-mixamo-class-trials`; Unity validators enforce exact hashes, importer settings, embedded-use provenance, and model-only runtime policy for every build type. |
| Gameplay-camera evidence: 16:9 / 20:9 / 2.4:1 | Existing Erika evaluation evidence only; complete four-class/aspect-ratio pass remains required. |
| Gameplay readability and art-direction result | User accepted Mixamo as the character-base direction on 2026-09-01; further manual customization is desired. |
| Skeleton/bind pose/scale/weights/socket result | Valid Humanoid imports; project controllers/AvatarIK; Erika rigid project bow/arrow staging. Extreme deformation polish remains open. |
| Extreme-pose deformation evidence | Partial Erika evidence; all-class crowded-combat and extreme-pose review not complete. |
| Animation/retarget/root-motion/event result | Model animation import disabled; project-owned deterministic controllers used. Downloaded Erika set rejected. |
| LOD/triangle/submesh/material/texture/bone budgets | Exact imported contracts recorded; physical Android draw-call, LOD, texture, and crowded-mode profiling remains open. |
| Device/build/resolution/thermal test context | Not run for the promoted four-character composition. |
| p95 CPU/GPU/frame time and memory result | Not run. |
| Legal reviewer and date | Project review, 2026-09-01; based on official Adobe sources linked above. |
| Art reviewer and date | User, 2026-09-01: accepted Mixamo as good enough for the base direction. |
| Technical reviewer and date | Pending physical-device and deformation review. |
| Accepted version/hash set and build channels | Current manifests only; permitted in embedded Development and non-Development release-candidate builds. Final `ACCEPTED_PRODUCTION` remains pending technical gates. |
| Required notices | Preserve this ledger, source provenance, manifests, and `THIRD_PARTY_NOTICES.md`; exclude raw Content Files from public source distribution. |
| Rollback/replacement path | Source-level selectors retain Quaternius/first-party alternatives; commissioned replacement remains optional. |
| Final decision and rationale | License/art direction accepted; `TECH_REVIEW` until mobile/deformation/customization checks close. |

### `POLYHAVEN-2026-001` — Stone Brick Wall 001 PBR source

| Field | Evidence / decision |
|---|---|
| Status | `TECH_REVIEW` |
| Product role and exact scope | Bounded runtime-candidate masonry surface for the first-party fortress meshes in Arena, Ember Vault, and compatible Training decoration. `AuthoringAssets` selects its untouched diffuse and OpenGL/+Y normal for the shared `RuinStone` material and a deterministic packed mask supplies AO/smoothness. |
| Explicitly excluded uses | No webpage preview/thumbnail art; no DirectX normal; no displacement sample in the mobile runtime; no claim that this photoreal source is already coherent with the stylized target. |
| Creator / rights holder | Poly Haven; asset page credits Dimitrios Savva for photography and Rico Cilliers for processing. |
| Distributor and official source URL | Poly Haven — https://polyhaven.com/a/stone_brick_wall_001 |
| Acquisition route and date | One-off manual download of five individual 2K PNG maps from the asset page's official `dl.polyhaven.org` CDN links, 2026-09-02. The public API was not used. |
| Pack/delivery version and release date | Poly Haven asset ID `stone_brick_wall_001`, 2K PNG variant. No package version is published; the exact delivered files are hash locked. |
| Receipt/order or commission contract/SOW reference | Not applicable; CC0 download without account or purchase. |
| Receipt/contract evidence SHA-256 | Not applicable. |
| License name/version and official URL | Creative Commons Zero v1.0 Universal (`CC0-1.0`); Poly Haven license statement: https://polyhaven.com/license |
| Retained license path and SHA-256 | `Assets/ThirdParty/PolyHaven/StoneBrickWall001/CC0-1.0.txt`; `a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499`; source is the official Creative Commons legal-code URL recorded in the manifest. |
| Permitted commercial/platform/modification/promotional use | Poly Haven states its CC0 assets may be used for any purpose, including commercial work, and may be modified, redistributed, or included in a sold product. |
| Attribution/notice obligation and fulfillment | Attribution is not required. Provider and named contributors remain credited in `THIRD_PARTY_NOTICES.md` and the local provenance record. |
| Raw/source/project redistribution restrictions | CC0 permits redistribution. Do not misrepresent the project as the original author or apply a conflicting license to the unchanged source. |
| Commission ownership/IP assignment or commercial-license terms | Not applicable. |
| Originality and third-party dependency representation | Poly Haven identifies assets on its site as work by its staff or artists who directly donate/sell it to Poly Haven. This remains third-party scanned source and must not be described as original Session RPG art. |
| Original archive/delivery path and SHA-256 | Five individual files under `Assets/ThirdParty/PolyHaven/StoneBrickWall001/`; exact URLs, byte lengths, and hashes are in `manifest.json`. Combined PNG source size is 71,512,208 bytes. |
| Admitted source/import/derivative paths and SHA-256 manifest | Source contract: `Assets/ThirdParty/PolyHaven/StoneBrickWall001/manifest.json`. Runtime derivative: `Assets/SessionRpg/Art/Environment/Textures/FortressMasonryMask.png`, SHA-256 `fddbbf69b2d67b54ef381e8af01bfe3665bbccc560caec4e3487de4b0b246ca2`; reproducible record: `SourceArt/Blender/Environment/Textures/FortressMasonryPbr.manifest.json`. |
| Excluded files and reason | Other resolutions and encodings, DirectX normal, combined ARM map, Blend/glTF/MaterialX/ZIP packages, and preview images were unnecessary for this bounded Unity material intake. |
| Import/configuration path and build selector/guard | `Assets/SessionRpg/Editor/Authoring/AuthoringAssets.cs` applies explicit sRGB/linear roles, normal-map type, mipmaps, repeat wrapping, trilinear filtering, anisotropy `8`, non-readable CPU state, and Android ASTC 4x4 quality-100 overrides. Diffuse and normal retain a 2048 px maximum; the packed mask is 1024 px. `Tools/Art/Blender/prepare_fortress_pbr_maps.py` validates exact source hashes and derives the packed mask without changing source files. |
| Gameplay-camera evidence: 16:9 / 20:9 / 2.4:1 | High-tier macOS-player runtime captures: 20:9 default yaw at `build/review/arena-high-v19-yaw90-2400x1080.png` and `build/review/arena-high-final-qhd-yaw90-3200x1440.png`; 2.4:1 yaw endpoints at `build/review/arena-high-ultrawide-yaw30-2592x1080.png` and `build/review/arena-high-ultrawide-yaw150-2592x1080.png`. The 16:9 case and the complete aspect/yaw cross-product remain unrun. These are not physical Android captures. |
| Gameplay readability and art-direction result | The current runtime frames exercise the scanned diffuse/normal/AO/roughness binding on fortress modules and wardstones under the authored High camera and lighting. This replaces the albedo-derived false normal, but final repetition/seam/value/telegraph review and explicit user acceptance remain pending. |
| Skeleton/bind pose/scale/weights/socket result | Not applicable. Asset page reports a 2.5 m material width and approximately 8.2 px/cm at 2K. |
| Extreme-pose deformation evidence | Not applicable. |
| Animation/retarget/root-motion/event result | Not applicable. |
| LOD/triangle/submesh/material/texture/bone budgets | Five immutable 2048 x 2048 16-bit PNG sources total 71,512,208 bytes. Runtime `RuinStone` selects diffuse and normal at a 2048 px import maximum plus one 1024 px packed mask (R metallic `0`, G AO, B unused displacement reference, A clamped smoothness). Android uses ASTC 4x4 at quality `100`; actual resident memory, shader cost, and frame impact remain unmeasured. |
| Device/build/resolution/thermal test context | Not run. |
| p95 CPU/GPU/frame time and memory result | Not run. |
| Legal reviewer and date | Project source-intake review, 2026-09-02, against the official asset and Poly Haven license pages plus retained CC0 legal code. |
| Art reviewer and date | PBR source direction selected for technical evaluation, 2026-09-02; current gameplay-camera captures exist, but final user acceptance remains pending. |
| Technical reviewer and date | Deterministic packing, authoring-contract, and macOS-player capture review, 2026-09-02; physical-device review remains pending. |
| Accepted version/hash set and build channels | Exact source hashes in `manifest.json` plus packed-mask hash `fddbbf69b2d67b54ef381e8af01bfe3665bbccc560caec4e3487de4b0b246ca2` are admitted only as a `TECH_REVIEW` runtime candidate for review builds. No production-material acceptance exists. |
| Required notices | Preserve `PROVENANCE.md`, `manifest.json`, the retained CC0 legal code, and the project third-party notice. |
| Rollback/replacement path | Change the source-level `RuinStone` authoring binding and rebuild; never mutate the immutable Poly Haven originals to roll back. |
| Final decision and rationale | Legal/provenance intake, reproducible runtime-candidate preparation, and bounded High-tier desktop capture are complete. Remain in `TECH_REVIEW` until explicit user art approval, the missing 16:9 and complete aspect/yaw review, and physical Android memory/performance gates pass. |

### `POLYHAVEN-2026-002` — Sunbreak close-scale scanned surfaces

Revision two (2026-09-05) adds Leafy Grass (Charlotte Baglioni, 2 m tile) and
Dirt Floor (eye-candy.xyz, 2.07 m tile) as six unchanged 1K JPEGs after actual
gameplay-camera rejection of the orange-brown sparse grass and bright repeated
Rocks Ground 02 pebbles. The original twelve files remain untouched. Current
delivery is eighteen maps / 41,996,162 bytes; the additional intake is only
5,633,455 bytes. All eighteen SHA-256 and official MD5 hashes pass the updated
read-only verifier. `SunbreakScannedSurfaceAuthoring.cs` now owns scan imports
and selects the new two ground sets; rock and bark retain their prior sources.
This supersedes the initial four-set/delivery-size/import-owner rows below while
preserving their original intake record. No production-art acceptance is implied.

| Field | Evidence / decision |
|---|---|
| Status | `TECH_REVIEW`; bounded local material evaluation under the user's 2026-09-05 realism request. |
| Product role and exact scope | Four close-scale scanned PBR surfaces for Sunbreak Basin ground, exposed soil/gravel, weathered rock and bark. Other modes and character sources are excluded. |
| Creator / rights holder / official source | Poly Haven: Sparse Grass by Amal Kumar; Rocks Ground 02, Rock 08 and Bark Brown 02 by Rob Tuytel. Exact official asset URLs are in `Assets/ThirdParty/PolyHaven/SunbreakSurfaces/manifest.json`. |
| Acquisition route/date/version | 2026-09-05, bounded public-API metadata lookup and unchanged official CDN files, 2K JPEG variants. No vendor package version; this exact delivery is hash-locked. |
| License and retained evidence | CC0 1.0 Universal; https://polyhaven.com/license. `Assets/ThirdParty/PolyHaven/SunbreakSurfaces/CC0-1.0.txt`, SHA-256 `a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499`, from the official Creative Commons legal-code URL. |
| Receipt / contract / seats | Not applicable: no purchase, account or subscription required. |
| Permitted use and restrictions | Commercial game use, modification, promotional rendering and source redistribution permitted by CC0; attribution not required. Do not misrepresent these third-party scans as wholly original project art. Website previews, text and logos are not included. |
| Notice fulfillment | Project `THIRD_PARTY_NOTICES.md`, local `PROVENANCE.md`, retained legal code and hash manifest. |
| Originality and AI/tool disclosure | Official pages credit the named contributors and describe scanned surfaces. No AI generation step is disclosed; this is not proof of every processing detail. No first-party-originality claim. |
| Delivery/source integrity | Twelve untouched 2048 × 2048 8-bit JPEG maps: 36,362,707 bytes (34.68 MiB). Every file matched official MD5 and byte length; project SHA-256 hashes in the manifest. |
| Source physical scale | Grass and soil/gravel: 2 m tiles; rock: 1.5 m; bark: 1 m. Exact dimensions are recorded per set. |
| Channels and excluded inputs | sRGB diffuse/color; OpenGL/+Y normal; linear roughness. All other resolutions, AO/ARM, displacement, DirectX normals, packages and preview images excluded. |
| Import/configuration path | `SunbreakSurfaceAuthoring.cs` owns importer and material configuration; no acquisition-time hand editing of generated materials or `.meta` import settings. Preserve source hashes and stable asset identities. |
| Derivatives / build-composition guard | Source-only intake makes no production approval. Generated masks/materials, if used, must be reproducible authoring output and verified by authoring tests before review builds. |
| Gameplay-camera / visual acceptance | Pending integrated 16:9, 20:9 and 2.4:1 review. The raw scans' realism does not prove gameplay readability, material coherence or user acceptance. |
| Deformation / animation | Not applicable to texture-only sources. |
| Mobile/rendering budgets | Source dimensions and payload verified; final importer formats, active sampling, shader variants, resident memory and GPU/frame costs require integrated evidence. |
| Physical device / thermal / p95 CPU/GPU | Not run during source intake. |
| Legal review | Project source-intake review, 2026-09-05, against official asset, license and API terms. This is a source-rights evidence record, not legal advice. |
| Art / technical reviewers | Technical source integrity checked 2026-09-05; gameplay-camera and physical-device acceptance remain open. |
| Accepted version / channels | Exact manifest source set for local development/review evaluation only; no `ACCEPTED_PRODUCTION` decision. |
| Rollback | Change source-level Sunbreak material selection and rebuild. Preserve all acquired originals and provenance. |
| Final decision | Admitted as a bounded `TECH_REVIEW` candidate; source rights and integrity are complete, integration/art/performance gates remain separate. |

### `POLYHAVEN-2026-003` — one scanned moss rock benchmark

| Field | Evidence / decision |
|---|---|
| Status / scope | `TECH_REVIEW`, 2026-09-07. Exactly one `rock03` in Sunbreak camp 1 / landmark 7; no Arena/Dungeon or broad region replacement. |
| Creator / source | Kless Gyzen, distributed by [Poly Haven](https://polyhaven.com/a/rock_moss_set_01). |
| Acquisition / version | Four official CDN sources acquired 2026-09-07; no account or payment. Asset slug `rock_moss_set_01`, exact delivery hash locked; no semantic pack version. |
| Rights | CC0 1.0 Universal; [official license](https://polyhaven.com/license). Commercial use, modification, promotional use and source redistribution permitted; attribution optional and voluntarily retained. No source-originality claim. |
| License evidence | `Assets/ThirdParty/PolyHaven/RockMossSet01/CC0-1.0.txt`, SHA-256 `a2010f343487d3f7618affe54f789f5487602331c0a8d03f49e9a7c547cf0499`. |
| Receipt / commission / seats | Not applicable. |
| Source integrity | Four original files / 4,475,602 bytes; official MD5 and acquired SHA-256 in `Assets/ThirdParty/PolyHaven/RockMossSet01/manifest.json`. Source/provider details in adjacent `PROVENANCE.md`. |
| Originality / AI disclosure | Third-party scanned asset. No AI step is disclosed in inspected source metadata; this does not prove every processing detail. |
| Exclusions | Other five meshes are retained as source but not placed. No full-set scene, previews, logos, text from provider pages, animation, physics, extra resolution, glTF dependency or texture-derived fake normal. |
| Import / generated composition | `SunbreakScannedRockAuthoring`: model-only FBX, original normals/UVs, generated tangents, 1K mapped diffuse/normal/roughness and dedicated `Sunbreak_ScannedMossRock` material. Only one selected source mesh is referenced by the generated scene. Hash/import/mesh/placement validation must pass for the exact source. |
| Geometry / LOD | Source set 63,127 triangles, selected rock03 5,000; uniform proportions, recentered XZ, minimum-Y placement and at most 1.5 m horizontal radius. No published LODs; no broad instancing accepted. |
| Material / mobile | One opaque material, sRGB diffuse, genuine linear GL normal, linear roughness, mips, 1K ASTC caps. Physical memory/GPU cost not measured. |
| Rig / deformation / animation | Not applicable, static environmental mesh. |
| Art / camera acceptance | Trial must be judged against same-camp actual-player captures, not provider renders or the AI concept. Explicit final user acceptance remains open. |
| Build / device / thermal / p95 | Fresh integrated build/test results in `Docs/PVE_REALISM_BENCHMARK_EVIDENCE.md`; physical phone measurements not run. |
| Review / channels | Project license and source-integrity review 2026-09-07. Local development/review candidate only; not `ACCEPTED_PRODUCTION`. |
| Notices / rollback | Preserve this entry, project third-party notice, originals, manifest and local legal code. Remove the source-level benchmark construction and its mandatory admission/placement validation and exact-one test contract together, then rebuild to remove runtime use; do not delete or mutate source originals. |
| Decision | A bounded real-mesh trial, not proof of final environment quality or phone performance. Expand only after gameplay-camera and budget evidence. |

### `ASSET-YYYY-NNN` — TBD asset name

| Field | Evidence / decision |
|---|---|
| Status | `PROPOSED` |
| Product role and exact scope | TBD |
| Explicitly excluded uses | TBD |
| Creator / rights holder | TBD |
| Distributor and official source URL | TBD |
| Acquisition route and date | TBD |
| Pack/delivery version and release date | TBD |
| Receipt/order or commission contract/SOW reference | TBD; use a redacted repository artifact or private-vault reference, never personal/payment data |
| Receipt/contract evidence SHA-256 | TBD |
| License name/version and official URL | TBD |
| Retained license path and SHA-256 | TBD |
| Permitted commercial/platform/modification/promotional use | TBD |
| Attribution/notice obligation and fulfillment | TBD |
| Raw/source/project redistribution restrictions | TBD |
| Commission ownership/IP assignment or commercial-license terms | TBD / not applicable |
| Originality and third-party dependency representation | TBD |
| Original archive/delivery path and SHA-256 | TBD |
| Admitted source/import/derivative paths and SHA-256 manifest | TBD |
| Excluded files and reason | TBD |
| Import/configuration path and build selector/guard | TBD |
| Gameplay-camera evidence: 16:9 / 20:9 / 2.4:1 | TBD |
| Gameplay readability and art-direction result | TBD |
| Skeleton/bind pose/scale/weights/socket result | TBD |
| Extreme-pose deformation evidence | TBD |
| Animation/retarget/root-motion/event result | TBD |
| LOD/triangle/submesh/material/texture/bone budgets | TBD |
| Device/build/resolution/thermal test context | TBD |
| p95 CPU/GPU/frame time and memory result | TBD |
| Legal reviewer and date | TBD |
| Art reviewer and date | TBD |
| Technical reviewer and date | TBD |
| Accepted version/hash set and build channels | None until `ACCEPTED_PRODUCTION` |
| Required notices | TBD |
| Rollback/replacement path | TBD |
| Final decision and rationale | TBD |

### `POLYHAVEN-2026-004` — measured modular fort, focal Leveling slice

The latest graphics/creep-fix pass replaces the foreground pair with the separately
named `SunbreakRuinMasonryV1` clipped/capped derivative and three source offcuts.
Five original tower-compound modules remain. The derivative uses one existing
opaque wall material and must stay below the replaced pair's 1,276-triangle budget;
the original source files and admitted meshes are unchanged. Procedure:
`SourceArt/Environment/SunbreakRuinMasonryV1/README.md`; fresh verification:
`Docs/PVE_GRAPHICS_AND_CREEP_FIX_EVIDENCE.md`. Status remains `TECH_REVIEW`.

The later 2026-09-08 woodland-polish composition supersedes the nine-instance
layout below: one existing 654-triangle thin corner replaces three foreground
wall pieces at different scales. Seven instances now total 7,924 triangles.
The adjoining wall/corner use one physical scale and real cross-section joint
guards. `SunbreakFortAssets` explicitly admits that sixth source node; the
source manifest changes selection metadata only. All seven delivery files and
their hashes remain unchanged. This is still `TECH_REVIEW`: the intact kit does
not supply the target's fractured ruin edges. Current verification belongs in
`Docs/PVE_WOODLAND_POLISH_EVIDENCE.md`.

| Gate | Recorded decision / evidence |
|---|---|
| Status / scope | `TECH_REVIEW`, 2026-09-07 intake, composition updated 2026-09-08. Nine selected module instances in Sunbreak camp 1's two existing cover unions. No broader region, Arena or Dungeon art acceptance. |
| Creator / source | Rico Cilliers, distributed by [Poly Haven](https://polyhaven.com/a/modular_fort_01). Weathered masonry architecture, not original Session RPG mesh work. |
| Delivery / exact identity | Original `modular_fort_01_1k.fbx` and six original 2K wall/trim diffuse, OpenGL normal and roughness maps: 32,523,603 bytes. Exact acquisition URLs, provider MD5, SHA-256 and sizes in `Assets/ThirdParty/PolyHaven/ModularFort01/manifest.json`; provenance and retained CC0 legal code beside it. No semantic release version published. |
| Legal / acquisition | CC0-1.0, official [provider license](https://polyhaven.com/license). Commercial, platform, modification and redistribution use permitted; no purchase, account, seat or attribution requirement. Asset license does not cover the provider's website preview renders. Project intake checked against official sources on 2026-09-07; this is not external legal advice. |
| Source integrity / dependency | Exact original source bytes retained; no texture repaint, repacking or mesh decimation. FBX contains 22 meshes / 28,218 triangles. The generated scene references selected render copies, never the showroom hierarchy. Render copies preserve every position, normal, UV and per-material index; only Unity's imported non-orthogonal tangents are orthogonalized. Original FBX remains untouched. The six retained maps are the selected modules' wall/trim dependencies. |
| Runtime composition | One round tower (4,772 triangles), two battlement walls (622 each), two thick wall ends (442 each), four low walls (406 each): 8,524 triangles total, two shared measured materials. Uniform physical scaling only, base pivots derived from preserved source geometry. Solid walls selected; the unused gate is not presented as a walkable opening. Current verification is in `Docs/PVE_RUIN_COMPOSITION_EVIDENCE.md`. |
| Import / budgets | `SunbreakFortAssets.Configure()` in the foundation build: model-only FBX, imported normals/UVs, Mikk tangents, no animation/avatar/cameras/lights/collision. Desktop maps at most 2K, Android 1K ASTC. Dedicated wall/trim material bindings; no global material replacement. The existing 65K-triangle/80-renderer chunk ceiling remains enforced. |
| Technical / artistic acceptance | Source/import/placement tests plus exact Core union checks required. Fresh runtime evidence belongs in `Docs/PVE_VERTICAL_SLICE_EVIDENCE.md`; an intake record or successful build is not artistic acceptance. Physical Android performance and thermal acceptance remain separate gates. |
| Rollback | Change focal source composition and rebuild; preserve all immutable originals, metadata, provenance and rejected historical assets. |

### `POLYHAVEN-2026-005` — source-derived trees, grass and ferns

The subsequent 2026-09-08 woodland revision extends the evaluation, not production
acceptance. `SunbreakCanopyAuthoring` makes a separate V2 generated leaf mesh by
uniformly expanding complete paired V1 clusters inside the original source
envelope. The source files, V1 hash manifest, measured atlases and trunk are
unchanged; the new recipe, exact generated-parity tests and 64-bounce DCC procedure
are recorded in `SourceArt/Environment/SunbreakCanopyV2/README.md`. Seven placed
trees retain 9,328 triangles each; eighteen ferns retain their original topology
across the focal and two middle-distance groves. Separately authored distant
woodland uses the same measured branch atlas on 84 lower-detail branched trees
(16,800 triangles / two shared batches), outside Core. Neither representation
adds a new source acquisition, texture or license grant. The older counts and
rejected V1 silhouette below remain historical evidence. Current native evidence
and unresolved overdraw/device/art gates are in `Docs/PVE_WOODLAND_POLISH_EVIDENCE.md`.

| Gate | Recorded decision / evidence |
|---|---|
| Status / scope | `TECH_REVIEW`, 2026-09-07 intake, composition updated 2026-09-08. Three trees, eight ferns and bounded low grass in the focal PvE Leveling pocket; not wholesale biome or other-mode acceptance. |
| Creator / official sources | [Tree Small 02](https://polyhaven.com/a/tree_small_02), Rico Cilliers; [Grass Medium 01](https://polyhaven.com/a/grass_medium_01), Rob Tuytel (photography), Rico Cilliers (modeling); [Fern 02](https://polyhaven.com/a/fern_02), Rob Tuytel (scanning), Rico Cilliers (modeling). Distributed by Poly Haven. |
| License / acquisition | CC0-1.0; official provider license and retained legal code in each source folder. Commercial, modification, platform and redistribution uses permitted, no seat/payment/attribution requirement. Acquired from official asset downloads on 2026-09-07. Website preview images are not game assets. |
| Original source identity | 21 unchanged source deliveries total 94,903,968 bytes. `Assets/ThirdParty/PolyHaven/{TreeSmall02,GrassMedium01,Fern02}/manifest.json` records original names, URLs, provider MD5, SHA-256 and exact sizes. Model files use an additional `.source` suffix to prevent accidental full-density import; their bytes are unchanged. |
| Source density / derivative need | Published Tree Small 02 Blender LOD1 has 376,155 polygons and was rejected as a direct mobile instance. The first reduced candidate was also rejected for sparse leaves and broken woody triangles. No nominal provider LOD label is treated as a mobile-performance guarantee. |
| Deterministic procedure | `Tools/Art/Blender/prepare_sunbreak_vegetation.py`, Blender 5.2.0 LTS. Preserve the source trunk at 3,000 triangles; omit fine Geometry Nodes twigs. Bake four intact source LOD1 leaf clusters from two orthographic directions, retain seeded source placement/rotation with recorded density and a 1.134615 cluster visual scale. Final tree: 6,328 leaf-card triangles + 3,000 trunk = 9,328. Grass/fern selected mesh topology is not decimated. |
| Measured material evidence | Three 1024×2048 cluster atlases: unlit source color/real coverage alpha; source tangent normals transformed to the card-camera basis and positive-Z hemisphere; measured linear roughness. No painted/generated albedo, inferred normal, invented height/AO or baked scene illumination. Original grass/fern/bark channels remain intact. Shared alpha-cutout shader uses matching coverage in color/depth/normals/shadows. |
| Derivative identity / bounds | `SourceArt/Environment/SunbreakVegetationV1/manifest.json` locks procedure, original manifests, eight mesh JSONs and all final atlas hashes. Tree physical bounds 2.942 × 4.576 × 4.319 m before uniform scene scale. JSONs are Unity Y-up, centered XZ, minY ground pivots; original source normals/UVs retained where not explicitly baked. README records exact reproduction command and DCC-only review images. |
| Import / runtime cost | `SunbreakVegetationAssets`: generated single-material mesh parts, four shared materials, maximum 2K desktop/1K Android textures, channel-correct normal/linear/sRGB imports and preserved coverage mipmaps. Source JSONs stay outside runtime Assets; isolated validation copies their exact directory. Three trees + eight ferns total 34,256 triangles. The 65K chunk ceiling is unchanged; nearby assets take budget from distant prototype meadow detail. No new collider, runtime vegetation updates or simulation authority. |
| Review / remaining gates | Initial DCC silhouette approval was withdrawn: the inherited eight-transparency-bounce render falsely filled overlapping cards. A controlled 64-bounce, gameplay-scale render and the native game both reveal thin foliage. Alpha-to-coverage improves edges but does not supply a fuller canopy. This source remains an explicit trial, not a production-tree acceptance. Native screenshots/tests are in `Docs/PVE_VERTICAL_SLICE_EVIDENCE.md`; density, motion and physical-device GPU/overdraw/memory/thermal gates remain open. |
| Rollback | Change selected source composition and rebuild. Preserve all original downloads, final source derivatives, hash manifests and historical rejected evidence; never edit generated scene/mesh YAML. |

### `POLYHAVEN-2026-006` — bright cloud panorama

| Gate | Recorded decision / evidence |
|---|---|
| Status / scope | `TECH_REVIEW`, 2026-09-07. One Sunbreak-only sky panorama; no baked world backdrop or other-mode sky replacement. |
| Creator / source | Greg Zaal (original), Jarod Guest (pure-sky edit), distributed by [Poly Haven](https://polyhaven.com/a/kloofendal_48d_partly_cloudy_puresky). CC0-1.0. |
| Exact delivery | Unmodified `kloofendal_48d_partly_cloudy_puresky_1k.hdr`, 1,435,119 bytes, SHA-256 `fd94c84997b8a3c353b62c2125a9b44e19509956986a126e472684432a02d798`. Official URLs, MD5, provenance and retained CC0 legal code in `Assets/ThirdParty/PolyHaven/SunbreakDaylightSky/`. No paid acquisition, seat or attribution requirement. |
| Source review | The ordinary panorama includes buildings and was excluded from runtime inputs. The selected provider pure-sky source contains actual bright clouds without terrain/buildings; no website preview is embedded. |
| Import / use | `SunbreakDaylightSkyAssets`, native RGB9E5 HDR, 1024 desktop / 512 Android maximum width, 2:1 panorama, neutral tint, exposure 1. `Skybox/Panoramic`; lighting presenter restores the prior sky on exit. Existing analytic reflection/ambient fill is not relabeled as HDRI-derived lighting. |
| Remaining / rollback | Native composition, phone and thermal acceptance are separate. Replace the source-level sky binding and rebuild to roll back; preserve the original HDR and provenance. |

## Promotion rule

A candidate moves to `ACCEPTED_PRODUCTION` only when every required field is
resolved, retained hashes match, the exact imported version passes current Unity
validation/full suites, gameplay-camera and deformation evidence are accepted, and
physical-device budgets pass. If the source, license, version, file hashes, import
recipe, rig, materials, or target platforms change, reopen the relevant reviews;
do not inherit acceptance silently.
