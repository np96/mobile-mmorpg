# Character production checkpoint — 2026-08-20

Status: **HISTORICAL CHECKPOINT, SUPERSEDED LATER ON 2026-08-20**.

The user subsequently authorized the six-step resume order recorded below. The
current result is documented in `PRODUCTION_BODY_SOURCE_DECISION_2026-08-20.md`
and `PRODUCTION_CHARACTER_PIPELINE.md`: Blender Foundation Human Base is selected,
the two-variant body and Hunter source-motion gates pass, and the first complete
Hunter V3 macro review is rejected. UV/PBR, LOD, final FBX, Unity A/B import, and
the authoring rebuild therefore remain blocked. Everything below this notice is a
preserved pre-resume snapshot, not current instruction or current asset status.

## Corrected product direction

- Blender Foundation / Blender Foundation Human Base assets are allowed. The
  earlier request to remove “Foundation” was a naming confusion.
- Quaternius is the low-poly source the user does not want for production
  characters. It may remain only as the current rollback/prototype until a
  replacement is accepted; do not use its mesh, rig, weights, bind pose, or
  materials as the production source.
- The next session should focus on properly rigged characters before additional
  environment or cosmetic passes.
- Keep the existing HUD/button treatment unchanged.
- The current `PRODUCTION_CHARACTER_PIPELINE.md` still describes the Blender
  Foundation Human Base as retired. Correct that documentation before resuming
  production work.

## Accepted technical DCC baseline

`SessionHumanoidRigV2` and `SessionHumanoidAnimationProofV2` are accepted as
technical DCC/FBX proofs only. They have not been imported into Unity.

- Rig: 68 exported transforms: the stable 55-bone Unity Humanoid mapping,
  10 sockets, and two elbow-support deform bones plus Root.
- Rig FBX:
  `SourceArt/Blender/Characters/SessionHumanoidRigV2/Outputs/SessionHumanoidRigV2.fbx`
  — SHA-256
  `beb653aba9873141418d95b38cfda2151c1852e2e7b0df9aba2eeaf85c9eed66`.
- Rig manifest:
  `SourceArt/Blender/Characters/SessionHumanoidRigV2/Outputs/SessionHumanoidRigV2.manifest.json`
  — SHA-256
  `7f8468fea292a1416c5d8ac697fd24652bba84e77808187647cf4eb5f3d3a6e6`.
- Animation FBX:
  `SourceArt/Blender/Characters/SessionHumanoidAnimationProofV2/Outputs/SessionHumanoidAnimationProofV2.fbx`
  — SHA-256
  `a0c88e7ca75e94e20227829846e052b66e8a31332eee8d44c6990187f2fa1fe7`.
- Animation manifest:
  `SourceArt/Blender/Characters/SessionHumanoidAnimationProofV2/Outputs/SessionHumanoidAnimationProofV2.manifest.json`
  — SHA-256
  `eea8ce8aa45f198fc4b54b80cf88d5f71c78e165897c3002df698e0c3783a043`.
- Independent reports:
  `outputs/art/blender/session-humanoid-rig-v2/SessionHumanoidRigV2FbxRoundTrip.json`
  and
  `outputs/art/blender/session-humanoid-animation-proof-v2/SessionHumanoidAnimationProofV2RoundTrip.json`.

The bound production body is not accepted. The MPFB intake remains useful
research, but the user has now explicitly allowed reconsidering the Blender
Foundation Human Base as the visible/source anatomy. If that route is resumed,
reacquire and retain the official licensed source cleanly; the earlier retained
Foundation subset is no longer present in the workspace.

## Work intentionally not promoted

### AnimationProofV3

Source-only work exists at:

- `Tools/Art/Blender/build_session_humanoid_animation_proof_v3.py`
- `Tools/Art/Blender/verify_session_humanoid_animation_proof_v3.py`
- `Tools/Art/Blender/session_humanoid_animation_proof_v3_control_plan.json`

No V3 Blend, FBX, manifest, or Unity asset was accepted or generated. The safer
hand-orientation solve removed the prior wrist singularity. A revised Ready pose
then passed the draw-distance intent, but the next unchanged gate rejected the
release: FullDraw to LooseSettle right-hand travel was `0.221256 m`, below the
`0.30 m` contract. The interrupted follow-up had only a scratch proposal; do not
assume it passed. Evidence:
`outputs/art/blender/session-humanoid-animation-proof-v3/SessionHumanoidAnimationProofV3FailedAttempt.json`.

### RigV3 deformation scratch probe

No canonical RigV3 or BaseV3 exists. The non-promoting probe is documented at:

- `outputs/art/blender/session-humanoid-rig-v3-deformation-architecture/SessionHumanoidRigV3DeformationArchitecture.SCRATCH.md`
- `outputs/art/blender/session-humanoid-rig-v3-deformation-architecture/SessionHumanoidRigV3SupportDriverProbe.json`
  — SHA-256
  `99d2907aad79bdd8bcf3fdda005a65fe0ede67c6fd10186728182ecac735d1b9`.

Raw result: reject. The broad auxiliary-bone experiment did not pass as a
production body. It recorded 27 true raw geometry failures; eight additional
records were validator coverage gaps for new auxiliary matrices and must not be
reported as deformation failures. The useful conclusion is to avoid adding more
ad-hoc bones. Resume with a conventional twist rig, manual compact regional
weight painting, and a small set of sculpted pose-space correctives for measured
residuals.

### Hunter art candidates

All candidates remain outside Unity `Assets` and are rejected research:

- `HunterProductionV1StaticFitScratch`: body direction viable, kit too primitive.
- `HunterProductionV2SculptStudy`: procedural/panel-built macro forms; rejected
  before materials, LODs, or export.
- TripoSR r128/r256 trial: watertight output, but body, gear, hands, face, bow,
  hood, and cape remained fused/melted. Final report:
  `outputs/art/triposr-hunter-high-source-trial/TripoSRHunterHighSourceTrial.report.json`
  — SHA-256
  `7b55ad057c536df2e9f4eddb60a87f4e5b01e2957a4be295ed78cdbae94cae07`.
- The V3 macro-form rewrite was subsequently executed and rejected by both its
  structural and manual-art gates. Its builder, outputs, and original README are
  preserved under
  `SourceArt/Archive/RejectedCharacterResearch_2026-08-20/`, with exact hashes in
  the archive manifest; they are excluded from production lineage.
- The later `SessionHumanoidClothingFitStudyV1` plain top/trousers checkpoint
  established and user-confirmed the BodyV3 static fit direction. Its dense
  body-derived envelopes are calibration evidence only, not production clothing.

## Unity boundary

- No production character from this work entered `Assets`.
- No Unity authoring rebuild, validator, tests, build, device run, or capture was
  run for these character candidates.
- The current Quaternius-backed prototype remains the runnable rollback.
- Do not run a Unity configure/rebuild until a character has passed DCC technical,
  deformation, and manual-art gates outside `Assets`.

## Recommended resume order

1. Correct the pipeline documentation: Blender Foundation is allowed;
   Quaternius is excluded from the production lineage.
2. Select the production body source explicitly. Given the corrected direction,
   evaluate the official Blender Foundation Human Base as the primary anatomy
   and keep MPFB only as a comparison/fallback if useful.
3. Bind the chosen body to the accepted project Humanoid/Rigify control setup.
   Use standard twist chains, manual weight painting, and sparse pose-space
   correctives; prove shoulders, elbows/pronation, wrists/hands, hips/knees, and
   feet on both body variants before class clothing.
4. Finish the clean Hunter Ready/Draw/Loose/Recovery motion without weakening the
   `0.30 m` draw/release gates.
5. Build one manually authored Hunter macro-form close review. Reject before
   surfacing if the hood, cowl, mantle, brigandine, pauldrons, cape, or bow read as
   primitives, panels, or fused shells.
6. Only after body, animation, and manual-art acceptance: author UV/PBR maps,
   LODs, FBX round-trip evidence, then stage a reversible Unity A/B import and one
   coordinated rebuild.

## Environment caveat

The current Codex/macOS session accumulated many exited Blender background jobs
in `UEs` state at 0% CPU. Individual TERM signals did not reap them. Before the
next long DCC session, restart the Codex app session or the machine so Blender and
Metal start from a clean process state.
