# Production character pipeline

Public-checkout note: candidate character research paths in this document describe
optional historical work, not shipped inputs. Dormant R&D sources are retained in
the original development workspace and omitted here; the public game selects the
bundled CC0 Quaternius fallback. See
[public source distribution](../PUBLIC_SOURCE_DISTRIBUTION.md).

This contract gates first-party hero and companion art before it enters Unity.
A technically valid FBX is not automatically production art. Candidates stay
under `SourceArt/Blender/Characters/`, outside `Assets/`, until the visual,
deformation, mobile-budget, and gameplay-camera gates all pass.

## Source and ownership

- The selected production anatomy source is the official Blender Foundation /
  Blender Studio **Human Base Meshes v1.4.1** bundle, licensed CC0. The production
  variants begin from `GEO-body_male_stylized` and
  `GEO-body_female_stylized`, with their paired eye meshes, retained under
  `SourceArt/ThirdParty/BlenderFoundation/HumanBaseMeshes_1.4.1/`. The official
  bundle is source anatomy, not finished class art and not permission to ship its
  untouched showcase presentation.
- Quaternius is excluded from the production lineage. No production character may
  inherit a Quaternius mesh, skeleton, bind pose, skin weight, material, UV, or
  proportion. The current Quaternius-backed Unity character may remain selectable
  only as a reversible prototype rollback until its replacement passes every gate
  in this document.
- MPFB 2.0.17 is retained only as a documented comparison/fallback research
  source. It is not the selected production anatomy, may not silently contribute
  topology or weights to the selected body, and requires a new explicit source
  decision plus the complete body/deformation gate before it can replace Human
  Base Meshes.
- `SessionHumanoidRigV2` is the accepted project-owned Humanoid hierarchy and
  Rigify control-authoring baseline. Production deformation may append explicitly
  contracted conventional twist deform chains without changing the stable Unity
  Humanoid mapping or exporting Rigify controls, mechanisms, or widgets.
- Class clothing, armor, hair, weapons, silhouettes, UV layouts, material maps,
  and animations are original project work. Retained base-body skin is never used
  as class art. Turnaround PNGs are art-direction references only; do not trace
  their geometry or copy recognizable third-party character designs.
- Project-generated construction references live in
  `SourceArt/Blender/Characters/ProductionConcepts/` and the Hunter kit's
  `References/` folder. Their manifests explicitly mark them reference-only.
  They may clarify volumes, seams, palette, and material grouping, but never
  substitute for fitted geometry, baked maps, deformation evidence, or the
  locked-camera manual art gate.
- A rejected candidate remains explicitly marked rejected and must never be copied
  under `Assets/SessionRpg/SourceArt/Characters/`.

## Chosen authoring workflow

1. Retain and hash the official Human Base Meshes v1.4.1 bundle and extract only
   the selected male/female stylized bodies and paired eyes into a clean DCC subset
   outside Unity `Assets`. Preserve the source UVs and face-set metadata through
   intake; strip showcase cameras, drivers, layouts, and unrelated assets.
2. Convert each selected A-pose surface to the same measured neutral T-pose,
   preserving its closed quad deformation topology. Bind both variants to the
   project-owned `SessionHumanoidRigV2` Humanoid/Rigify setup. Use conventional
   upper-arm, forearm, thigh, and calf twist chains; explicit compact regional
   weight painting limited to four normalized influences; and only sparse,
   measured pose-space corrective shapes for residual deformation.
3. Prove shoulders, elbow flexion and forearm pronation, wrists and hands, hips,
   knees, ankles, and feet on both variants before any class garment is fitted.
   Automatic weights or a green Avatar alone are not proof. Record every tested
   pose before export and after FBX round-trip.
4. Finish and review the class's source motion on both accepted body variants.
   Preserve every existing class-specific numeric contract; for Hunter the
   Ready/Draw/Hold/Loose/Recovery sequence must keep both draw and release hand
   travel at or above `0.30 m` before clothing work advances.
5. Author one complete untextured class macro-form around that accepted body and
   review it at close range before sculpt detail or surfacing. Reject immediately
   if hoods, cowls, mantles, armor rows, pauldrons, capes, weapons, or limb gear
   read as primitives, flat panels, or fused shells. A rejected macro pass may
   change cages and proportions only; it may not advance to UVs, materials, LODs,
   FBX, or Unity.
6. After manual macro acceptance, fit/sculpt the high-detail original class kit.
   Clothing and armor use deliberate sculpted/fitted volumes; the body is not a
   substitute for class identity.
7. Preserve and, where a deformation test proves it necessary, locally repair the
   accepted Human Base edge flow around shoulders, elbows, hips, knees, hands,
   feet, and face openings. Clothing hems and weapon grips receive deliberate authored low
   topology. Automatic remesh output is useful for blockout only. Meet a class's
   runtime budget primarily by removing body faces fully hidden beneath fitted
   equipment, then applying protected/manual simplification. A globally decimated
   naked body is review geometry, not the authoritative production deform mesh.
8. Paint garment weights against the accepted body and first-party deform rig,
   clamp to four normalized influences, then correct and test every joint through
   the required animation set. Class garments may transfer weights from the
   accepted first-party base only after the body deformation proof passes.
9. Unwrap the accepted low mesh and bake tangent-space normal and AO/detail from
   the high mesh. Pack the direct URP mask as R metallic, G AO, B spare,
   A smoothness.
10. Generate LOD1/LOD2 from the accepted low mesh with protected silhouette,
   joint, face-opening, and weapon geometry; review each LOD at the gameplay lens.

## Shared runtime contract

- Bind every accepted hero to the accepted `SessionHumanoidRigV2` Humanoid mapping
  plus the approved production twist-chain extension. Its Unity-facing hierarchy
  uses conventional Human bone names and a valid T-pose, contains only deform and
  explicitly contracted socket bones, and is mapped to a Unity Humanoid Avatar.
  Rigify controls and mechanism bones never enter the FBX.
- Existing prototype animation clips may be retargeted temporarily through Unity
  Humanoid while first-party clips are authored. They do not define the production
  skeleton, bind pose, proportions, or renderer bones.
- Export Unity-facing meshes in meters, local `+Z` forward, feet on local ground,
  identity root rotation/scale, and with no collider, camera, light, or gameplay
  authority.
- Author the selected Human Base variants from their measured source heights
  (`1.798 m` masculine and `1.617 m` feminine before any accepted proportion
  adjustment) and export the FBX
  with identity transforms. Keep presentation scale outside the asset. The current
  prototype Hunter uses `1.46`; the replacement records its own uniform visual
  scale in the accepted manifest and source selector after locked-camera capture
  calibration. Historical MPFB review scales have no authority over the selected
  Human Base lineage. Compare each source at its committed runtime scale and show
  a separate scale-1 asset-contract frame.
  Never bake presentation scale into the mesh, rig, Avatar, collider, AimPoint, or
  simulation root, and never use non-uniform scale on the animated hierarchy.
- Preserve the existing combat root, CapsuleCollider, AimPoint, simulation scale,
  Animator controller contract, and authored class selection. A production model
  replaces one visual root; it never adds a second Animator.
- Limit skinning to four normalized weights per vertex. Require zero degenerate or
  non-manifold visible triangles and no negative-scale hierarchy.
- Force every accepted hero `SkinnedMeshRenderer.quality` to `Bone4`; do not rely
  on a project quality tier whose `QualitySettings.skinWeights` may be `TwoBones`.
  Capture and device tests must assert the effective renderer quality for every
  shipped Android/iOS quality tier.

## Mobile geometry and rendering

| Asset | LOD0 | LOD1 | LOD2 | Materials / active draws |
|---|---:|---:|---:|---:|
| Hero body and equipment | 15k-22k | 7k-11k | 3k-5k | <=2 |
| Hunter companion | 3k-6k | 1.5k-3k | 0.5k-1.2k | 1 |

- Use one 1024 px Base Color, tangent-space Normal, and direct URP mask per shared
  material. For stock URP/Lit the mask packs metallic in R, occlusion in G, an
  intentionally unused B channel, and smoothness in A. Do not call B "detail"
  unless a tracked custom shader actually samples it.
- LOD transitions must be verified in the authored strategy camera. Thin weapons,
  strings, staff heads, and daggers may not disappear before the body LOD does.
- Verify the active LOD at the locked camera under every shipped quality asset;
  Android `lodBias` changes the effective screen-relative thresholds. Evidence
  records the quality tier, `lodBias`, selected LOD, and renderer/draw count rather
  than assuming the authoring thresholds select LOD0.
- Do not use runtime material instances, per-frame collection creation, or hidden
  always-animating renderers.

## Animation and deformation gates

- Sample every frame of idle, locomotion, primary attack/cast, release, hit,
  interrupt where relevant, and death before export and after FBX round-trip.
- Feet remain grounded in idle and at least 70% of locomotion frames. A normal
  combat pose may not tilt the body more than 15 degrees or collapse below 85% of
  its idle height.
- Cloth, weapons, and armor may not intersect a limb by more than two rendered
  pixels for more than two consecutive sampled frames at the review camera. The
  same contacts must also pass a world-space surface-penetration gate; two pixels
  alone is several centimeters at the locked lens and is not sufficient proof.
- Hunter draw uses a taut static string outside the draw, a draw-only deforming
  string and nocked arrow before release, and the pooled projectile after release.
- Cast/attack anticipation and release must differ by at least 30% in a silhouette
  comparison; animation names or moving particles are not substitutes.

## Actual-camera visual gate

Use the authored 1920x1080 Arena perspective camera: yaw 90 degrees, pitch 40 degrees,
distance 18 m, pivot 0.9 m, forward focus 2.3 m, right focus 0 m, vertical FOV 46.
The follow pivot must recenter on the reviewed actor; moving only the actor is not
valid gameplay evidence.

- At the locked lens and the class's real runtime presentation scale, the hero
  body is approximately 95-130 px boot-to-head and 60-90 px across the
  torso/shoulders. The complete class silhouette, including equipment, is
  approximately 125-185 px. Also retain a scale-1 identity render as an
  asset-contract check.
  Do not enlarge an FBX, change the camera, or give only the candidate a different
  visual scale merely to satisfy a pixel counter.
- The candidate body must exceed its current prototype's projected height and
  width by at least 10%. A class weapon adds at least 45 px of useful silhouette
  without inflating the body metric.
- The hero remains identifiable in a desaturated frame, at four headings, and
  against both quiet stone and warm lava/perimeter backgrounds.
- Evidence includes separate rest, locomotion contacts, anticipation, release,
  hit, death, LOD0/1/2, and same-camera current-model A/B plates. Do not overlap or
  rescale the compared actors during compositing.

## Manual production-art gate

Passing geometry, projection, and deformation checks is necessary but not enough.
An independent review must also accept the model as finished game art at both the
locked gameplay camera and a closer neutral inspection camera.

- Anatomy and garment volumes must read as deliberately sculpted/fitted forms.
  Capsule limbs, box armor, flat sheet robes, disconnected shells, and placeholder
  primitives are automatic rejects even when their triangle counts are valid.
- The face remains readable and unobstructed at the useful class heading. Hoods,
  collars, hair, and shoulders must create separate silhouettes rather than one
  merged teardrop.
- Base color, normal, AO, metallic, and roughness response must materially separate
  skin, cloth, leather, painted metal, bare metal, fur, and weapon surfaces. A flat
  swatch atlas or whole-body tint cannot be promoted as production art.
- Seams, hems, bevels, straps, fasteners, and large material breaks must survive the
  1080p strategy view. Detail that only appears in the close review is not counted
  toward gameplay readability.
- The candidate must be clearly more authored than the current prototype, not just
  larger or technically cleaner. If reviewers disagree, keep it outside `Assets/`
  and retain the working prototype.

## Class silhouette targets

- Hunter: broad hooded brigandine, asymmetric fur mantle, split short cape,
  substantial recurve bow, quiver, and visible fletching.
- Priest: structured hood/cowl, layered split vestments, armored shoulders, and a
  large circular-reliquary staff; Shadow and Discipline identity comes from
  materials/VFX rather than separate meshes.
- Mage: asymmetric high collar and mantle, split combat coat, reinforced shoulder,
  and a crystal-tipped staff distinct from the Priest reliquary.
- Rogue: compact asymmetric cowl/half-mask, layered leather, poison vials, and two
  large daggers with a clear attack envelope.
- Hunter companion: low broad armored spider, chunky ground-contacting legs, one
  restrained owner marker, and no whole-body spectral glow.

## Promotion into Unity

1. Only after body, animation, and manual-art acceptance, build and round-trip the
   candidate outside `Assets/`.
2. Record an explicit accepted visual decision in its manifest and inspect all
   required evidence manually.
3. Copy only the accepted FBX and texture maps to the validator's locked Unity
   source paths, preserving provenance and hashes.
4. Enable the source-level A/B selection and run the one relevant import
   configurator.
5. Run one authored rebuild, then `validate`, full EditMode, full PlayMode, build,
   normal/desaturated captures, and device profiling.
6. If any gate fails, restore the prototype source selection and rebuild once.

## Current production-character gate state — 2026-08-20

- Primary anatomy: official Blender Foundation / Blender Studio Human Base Meshes
  v1.4.1 stylized masculine and feminine bodies. Quaternius contributes nothing
  to the production lineage; MPFB remains comparison/fallback research only.
- Body: `SessionHumanoidBaseV3` passes the independent numeric proof and manual
  review across 24 plates covering shoulders, elbows/pronation, wrists/hands,
  hips/knees, and feet on both variants.
- Motion: the source-only Hunter sequence passes numeric and manual two-variant
  review with `0.308676 m` draw travel and `0.324133 m` release travel against the
  unchanged `0.30 m` minimum.
- Art: the rejected `HunterProductionV3ManualMacro` and earlier failed candidates
  are recoverably archived under
  `SourceArt/Archive/RejectedCharacterResearch_2026-08-20/`. The separate plain
  BodyV3 top/trousers checkpoint established a user-confirmed static fit direction,
  but it remains calibration geometry. The fresh Hunter kit still requires a
  manual macro-form pass and full two-variant deformation proof.
- Downstream: UV/PBR maps, production retopology/LODs, final FBX round-trip,
  reversible Unity A/B import, and authoring rebuild are blocked. No rejected
  candidate may enter `Assets`.

## Mesh and animation FBX split

- The class mesh FBX contains the accepted skinned LOD meshes, the clean
  project-owned Humanoid hierarchy plus only contracted twist deform bones, and no
  embedded animation takes. Unity imports
  it as Humanoid using `Create From This Model`; its Avatar is the class source of
  truth.
- First-party animation FBXs contain the exact same baked game-bone hierarchy and
  only their contracted clips—never Rigify controls, mechanisms, widgets, meshes,
  cameras, lights, constraints, or helper bones. Unity imports them as Humanoid
  using `Copy From Other Avatar` from the accepted class mesh Avatar.
- Each animation clip is sampled before export and after Unity import for in-place
  Root/Hips drift, grounded contacts, loop closure, hand/socket travel, and the
  class-specific anticipation/release gates. Clip-name or Avatar validity alone is
  not acceptance evidence.
- Compute and serialize conservative `SkinnedMeshRenderer.localBounds` from every
  shipped clip, including death and wide weapon poses. Validate those bounds with
  `updateWhenOffscreen=false`; a mesh-FBX bind-pose bound may otherwise cull a
  separately imported animation.
- Treat `Optimize Game Objects` as a measured choice. Prefer it when device data
  supports the saving and explicitly expose only contracted sockets; leave it off
  only when the required bone/socket/IK access or measured correctness demands it.
