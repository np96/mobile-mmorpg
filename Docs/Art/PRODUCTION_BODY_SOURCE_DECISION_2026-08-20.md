# Production body source decision — 2026-08-20

Status: **SELECTED; BODY AND HUNTER MOTION GATES PASSED; MANUAL HUNTER MACRO REJECTED; NOT UNITY-ACCEPTED**

## Decision

Use the official Blender Foundation / Blender Studio **Human Base Meshes v1.4.1**
stylized male and female bodies as the primary production anatomy:

- `GEO-body_male_stylized`
- `GEO-body_male_stylized.eye.L`
- `GEO-body_male_stylized.eye.R`
- `GEO-body_female_stylized`
- `GEO-body_female_stylized.eye.L`
- `GEO-body_female_stylized.eye.R`

The exact retained source is
`SourceArt/ThirdParty/BlenderFoundation/HumanBaseMeshes_1.4.1/`.
Quaternius is excluded from the production lineage. MPFB 2.0.17 is comparison and
fallback research only; it is not allowed to contribute topology, weights, bind
pose, or proportions to this selected body without a new explicit decision.

## Verified intake facts

- Official archive: Human Base Meshes v1.4.1, CC0, distributed from
  `download.blender.org` and listed on Blender's official demo-files page.
- Archive SHA-256:
  `811f43accbb31a88266d932f8f5563b2d13586fca0ba2693aad1f5fe582b3515`.
- Source blend SHA-256:
  `3c121505651140ceb4d69fd1d8923f7788ffadd81672f5be14845a5f2c75c137`.
- Each selected body is a closed, manifold 12,502-vertex / 12,500-quad surface
  (25,000 triangles), with UV0 and sculpt face-set metadata.
- The source has no body armature, skin weights, class materials, class equipment,
  or game LODs. New project binding, weights, correctives, surfacing, and LODs are
  therefore mandatory and auditable.
- The source bodies are approximately 1.798 m (masculine) and 1.617 m (feminine)
  high in an A-pose. The production intake must convert both to measured project
  T-poses before binding without silently equalizing their proportions.

## Why this is primary

The official stylized bodies already provide coherent stylized anatomy, closed
quad deformation topology, hands, feet, facial planes, UVs, and matching male and
female variants. They are closer to the requested premium, chunky fantasy anatomy
than the neutral MPFB comparison while avoiding any Quaternius prototype lineage.
Their lack of inherited weights is an advantage for this project: the accepted
Humanoid mapping, twist chains, regional weights, and correctives remain explicitly
project-owned.

## Comparison/fallback boundary

The MPFB research body remains useful for measuring alternate proportions and for
diagnosing topology/weighting approaches. Its earlier bind failed the unchanged
elbow deformation gate, and it is not selected. A fallback requires a written
decision, clean rebuild, all joint proofs on both variants, and a new manual art
acceptance; no previous MPFB checkpoint is promotable.

## Acceptance sequence

1. Retain and verify a minimal CC0 DCC subset.
2. Convert both bodies from A-pose to the project T-pose without topology loss.
3. Bind to the accepted Humanoid/Rigify setup with conventional arm/forearm and
   thigh/calf twist chains, four normalized influences, explicit regional weight
   painting, and sparse measured pose-space correctives.
4. Pass shoulder, elbow/pronation, wrist/hand, hip, knee, ankle/foot proofs for
   both body variants before clothing.
5. Pass the Hunter motion and manual macro-form gates before UV/PBR, LOD, FBX, or
   Unity A/B work.

Selection is not production acceptance. Failure at any downstream gate keeps the
candidate outside Unity `Assets`.

## Gate result recorded 2026-08-20

- `SessionHumanoidBaseV3` binds both selected variants to the accepted
  `SessionHumanoidRigV2` Humanoid/Rigify baseline with 76 bones / 63 deform bones,
  conventional upper-arm, forearm, thigh, and calf twist chains on both sides,
  no automatic weighting, no weighted legacy elbow-support bones, at most four
  normalized influences, and twelve sparse pose-space correctives per variant.
- The independent 24-plate proof passes shoulders, elbow flexion/pronation,
  wrists/hands, hips/knees, and ankles/feet on both bodies. The accepted source
  blend SHA-256 is
  `ff30baebeb3cb03186cf1663691a65c743af16616c82003f4358b5a41fb08458`;
  the proof SHA-256 is
  `ef085d15a7dca7e4a0e357c74dd13a60c77969dc1c5db9da9c1843109046e719`.
- The source-only Hunter Ready/Draw/Hold/Loose/Recovery proof passes on both
  bodies. Draw-hand travel is `0.308676 m` and release-hand travel is
  `0.324133 m`, both above the unchanged `0.30 m` gate. No FBX or Unity import was
  required for this pre-art review.
- The first complete Hunter macro-form attempt was rejected structurally and
  manually. Its 9,906-triangle cage evaluates to 46,608 kit triangles; six
  garment families fail the 1 mm body-penetration limit; and the hood/cowl/mantle,
  brigandine, pauldrons, cape, and limb gear read as primitives, panels, or fused
  shells in the required close frame.

That rejection freezes UV/PBR, production retopology/LODs, final FBX round-trip,
Unity A/B import, and the coordinated authoring rebuild. The next safe action is
macro-cage and proportion reauthoring only.
