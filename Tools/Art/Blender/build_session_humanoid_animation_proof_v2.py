#!/usr/bin/env python3
"""Build ten first-party actions on the isolated 68-joint V2 game rig.

The original sparse Rigify-control performances are reused as first-party source
definitions.  Every integer frame is freshly evaluated on RigV2.  The two elbow
supports are baked analytically at the shortest-arc midpoint of the corresponding
LowerArm local rotation; exported support curves are DCC/Generic proof only.
"""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector


SCRIPT_PATH = Path(__file__).resolve()
PROJECT_ROOT = SCRIPT_PATH.parents[3]
TOOLS_DIR = SCRIPT_PATH.parent
sys.path.insert(0, str(TOOLS_DIR))
import build_session_humanoid_rig_v2 as rig_contract

rig_contract.configure_base_module()
import build_session_humanoid_animation_proof_v1 as base


ASSET_ID = "session-humanoid-animation-proof-v2"
SOURCE_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidAnimationProofV2"
OUTPUT_DIR = SOURCE_DIR / "Outputs"
SOURCE_BLEND = SOURCE_DIR / "SessionHumanoidAnimationProofV2.blend"
FBX_PATH = OUTPUT_DIR / "SessionHumanoidAnimationProofV2.fbx"
MANIFEST_PATH = OUTPUT_DIR / "SessionHumanoidAnimationProofV2.manifest.json"
PROVENANCE_PATH = OUTPUT_DIR / "SessionHumanoidAnimationProofV2.PROVENANCE.md"
CLIP_CONTRACT_PATH = SOURCE_DIR / "SessionHumanoidAnimationProofV2.CLIPS.md"
VERIFIER_PATH = SCRIPT_PATH.with_name("verify_session_humanoid_animation_proof_v2.py")
RIG_BLEND = rig_contract.BLEND_PATH
RIG_MANIFEST = rig_contract.MANIFEST_PATH
GAME_RIG_NAME = rig_contract.RIG_OBJECT_NAME
CONTROL_RIG_NAME = rig_contract.RIGIFY_CONTROL_NAME
CONTROL_ACTION_PREFIX = base.CONTROL_ACTION_PREFIX
FPS = base.FPS
FBX_CREATION_TIMESTAMP = base.FBX_CREATION_TIMESTAMP
IDENTITY = Matrix.Identity(4)

ControlTransform = base.ControlTransform
KeyPose = base.KeyPose
ClipSpec = base.ClipSpec
ANIMATED_CONTROLS = base.ANIMATED_CONTROLS
IK_SWITCH_CONTROLS = base.IK_SWITCH_CONTROLS
CLIPS = base.CLIPS
CLIP_BY_NAME = base.CLIP_BY_NAME
DEFORM_NAMES = tuple(spec.name for spec in rig_contract.BONE_SPECS if spec.deform)
SUPPORT_FACTOR = rig_contract.SUPPORT_ROTATION_FACTOR
BASE_SAMPLE_CLEAN_GAME_POSE = base.sample_clean_game_pose
BASE_CREATE_BAKED_ACTION = base.create_baked_action
V1_ANIMATION_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidAnimationProofV1"
V1_ANIMATION_BLEND = V1_ANIMATION_DIR / "SessionHumanoidAnimationProofV1.blend"
V1_ANIMATION_FBX = V1_ANIMATION_DIR / "Outputs/SessionHumanoidAnimationProofV1.fbx"
V1_ANIMATION_MANIFEST = V1_ANIMATION_DIR / "Outputs/SessionHumanoidAnimationProofV1.manifest.json"
EXPECTED_V1_ANIMATION_BLEND_SHA256 = "520b730e0d3272b3d34b6847c0e624f1434f3ad7068960ce19bb6ab024558ae9"
EXPECTED_V1_ANIMATION_FBX_SHA256 = "7a958aed8fda2c54f63f5178379a1a5ce68fe4a64fad3466132565859b5ed320"
EXPECTED_V1_ANIMATION_MANIFEST_SHA256 = "335a739332c52aebb9324ca42b7c506923204f315c97bdbeec690cb39b9115e6"


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def configure_base_animation_module() -> None:
    base.rig_contract = rig_contract
    values = {
        "PROJECT_ROOT": PROJECT_ROOT,
        "SOURCE_DIR": SOURCE_DIR,
        "OUTPUT_DIR": OUTPUT_DIR,
        "SOURCE_BLEND": SOURCE_BLEND,
        "FBX_PATH": FBX_PATH,
        "MANIFEST_PATH": MANIFEST_PATH,
        "PROVENANCE_PATH": PROVENANCE_PATH,
        "CLIP_CONTRACT_PATH": CLIP_CONTRACT_PATH,
        "RIG_BLEND": RIG_BLEND,
        "RIG_MANIFEST": RIG_MANIFEST,
        "VERIFIER_PATH": VERIFIER_PATH,
        "ASSET_ID": ASSET_ID,
        "GAME_RIG_NAME": GAME_RIG_NAME,
        "CONTROL_RIG_NAME": CONTROL_RIG_NAME,
        "DEFORM_NAMES": DEFORM_NAMES,
    }
    for name, value in values.items():
        setattr(base, name, value)
    # sample_clip resolves this module global at runtime.
    base.sample_clean_game_pose = sample_clean_game_pose


def assert_frozen_v1_animation() -> dict[str, object]:
    expected = (
        (V1_ANIMATION_BLEND, EXPECTED_V1_ANIMATION_BLEND_SHA256),
        (V1_ANIMATION_FBX, EXPECTED_V1_ANIMATION_FBX_SHA256),
        (V1_ANIMATION_MANIFEST, EXPECTED_V1_ANIMATION_MANIFEST_SHA256),
    )
    for path, expected_hash in expected:
        require(path.is_file() and sha256(path) == expected_hash, f"Frozen V1 animation changed: {path}")
    manifest = json.loads(V1_ANIMATION_MANIFEST.read_text(encoding="utf-8"))
    require([clip["name"] for clip in manifest["clips"]] == [clip.name for clip in CLIPS], "Frozen V1 clip order changed")
    return manifest


def load_rig_source_v2() -> tuple[bpy.types.Object, bpy.types.Object, dict[str, object]]:
    """Open RigV2 without invoking V1's intentionally frozen 66-joint gate."""

    bpy.ops.wm.open_mainfile(filepath=str(RIG_BLEND))
    bpy.context.preferences.filepaths.save_version = 0
    bpy.context.scene.render.fps = FPS
    require(not bpy.data.actions, "RigV2 source unexpectedly contains actions")
    require(GAME_RIG_NAME in bpy.data.objects and CONTROL_RIG_NAME in bpy.data.objects, "RigV2 lost its game/control rig")
    game_rig = bpy.data.objects[GAME_RIG_NAME]
    control_rig = bpy.data.objects[CONTROL_RIG_NAME]
    require(game_rig.type == "ARMATURE" and control_rig.type == "ARMATURE", "RigV2 source armatures are invalid")
    require(set(ANIMATED_CONTROLS).issubset(control_rig.pose.bones.keys()), "RigV2 lacks animator controls")
    require(set(IK_SWITCH_CONTROLS).issubset(control_rig.pose.bones.keys()), "RigV2 lacks IK switches")
    require(set(bone.name for bone in game_rig.data.bones) == set(rig_contract.SPEC_BY_NAME), "RigV2 bone set changed")
    require(sum(len(bone.constraints) for bone in game_rig.pose.bones) == 60, "RigV2 DCC constraint count changed")
    rig_manifest = json.loads(RIG_MANIFEST.read_text(encoding="utf-8"))
    require(rig_manifest["hierarchy"]["exportedBoneCount"] == 68, "AnimationProofV2 requires 68-joint RigV2")
    return game_rig, control_rig, rig_manifest


def local_basis_from_sample(
    game_rig: bpy.types.Object,
    sampled: dict[str, Matrix],
    bone_name: str,
) -> Matrix:
    bone = game_rig.data.bones[bone_name]
    rest = bone.matrix_local
    if bone.parent is None:
        return rest.inverted() @ sampled[bone_name]
    parent_rest = bone.parent.matrix_local
    parent_pose = sampled[bone.parent.name]
    placement = parent_pose @ parent_rest.inverted() @ rest
    return placement.inverted() @ sampled[bone_name]


def half_local_rotation(lower_basis: Matrix) -> Quaternion:
    lower = lower_basis.to_quaternion().normalized()
    if lower.w < 0.0:
        lower.negate()
    return Quaternion((1.0, 0.0, 0.0, 0.0)).slerp(lower, SUPPORT_FACTOR).normalized()


def sample_clean_game_pose(
    game_rig: bpy.types.Object,
    control_rig: bpy.types.Object,
) -> dict[str, Matrix]:
    sampled = BASE_SAMPLE_CLEAN_GAME_POSE(game_rig, control_rig)
    for side in ("Left", "Right"):
        parent_name = side + "UpperArm"
        lower_name = side + "LowerArm"
        support_name = side + "ElbowSupport"
        lower_basis = local_basis_from_sample(game_rig, sampled, lower_name)
        support_rest = game_rig.data.bones[support_name].matrix_local
        parent_rest = game_rig.data.bones[parent_name].matrix_local
        support_placement = sampled[parent_name] @ parent_rest.inverted() @ support_rest
        sampled[support_name] = support_placement @ Matrix.LocRotScale(
            Vector((0.0, 0.0, 0.0)),
            half_local_rotation(lower_basis),
            Vector((1.0, 1.0, 1.0)),
        )
    return sampled


def create_baked_action(
    game_rig: bpy.types.Object,
    clip: ClipSpec,
    sampled: dict[int, dict[str, Matrix]],
) -> bpy.types.Action:
    action = BASE_CREATE_BAKED_ACTION(game_rig, clip, sampled)
    action["sessionRole"] = "clean_68_joint_game_bake_v2_with_generic_support_proof"
    action["bendSupportBake"] = "analytic shortest-arc local slerp 0.5"
    action["bendSupportRuntimeAuthority"] = "Unity LateUpdate"
    return action


def evaluated_local_basis(
    rig: bpy.types.Object,
    evaluated: bpy.types.Object,
    bone_name: str,
) -> Matrix:
    rest = rig.data.bones[bone_name]
    pose = evaluated.pose.bones[bone_name]
    if rest.parent is None:
        return rest.matrix_local.inverted() @ pose.matrix
    parent_pose = evaluated.pose.bones[rest.parent.name].matrix
    placement = parent_pose @ rest.parent.matrix_local.inverted() @ rest.matrix_local
    return placement.inverted() @ pose.matrix


def quaternion_error(first: Quaternion, second: Quaternion) -> float:
    left = first.normalized()
    right = second.normalized()
    if left.dot(right) < 0.0:
        right.negate()
    return left.rotation_difference(right).angle


def collect_support_metrics(
    game_rig: bpy.types.Object,
    baked_actions: dict[str, bpy.types.Action],
) -> dict[str, object]:
    base.set_constraints_muted(game_rig, True)
    game_rig.animation_data_create()
    maximum_midpoint_error = 0.0
    maximum_pivot_separation = 0.0
    maximum_support_angle = 0.0
    maximum_lower_angle = 0.0
    maximum_support_local_translation = 0.0
    maximum_support_scale_deviation = 0.0
    per_clip: dict[str, object] = {}
    for clip in CLIPS:
        game_rig.animation_data.action = baked_actions[clip.name]
        clip_error = 0.0
        clip_pivot = 0.0
        clip_support_angle = 0.0
        for frame in range(clip.frame_start, clip.frame_end + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            evaluated = game_rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
            for side in ("Left", "Right"):
                lower_name = side + "LowerArm"
                support_name = side + "ElbowSupport"
                lower_basis = evaluated_local_basis(game_rig, evaluated, lower_name)
                support_basis = evaluated_local_basis(game_rig, evaluated, support_name)
                support_location, support_quaternion, support_scale = support_basis.decompose()
                expected = half_local_rotation(lower_basis)
                actual = support_quaternion.normalized()
                error = quaternion_error(actual, expected)
                pivot = (
                    evaluated.pose.bones[support_name].head
                    - evaluated.pose.bones[lower_name].head
                ).length
                lower_angle = lower_basis.to_quaternion().normalized().angle
                support_angle = actual.angle
                scale_deviation = max(abs(value - 1.0) for value in support_scale)
                clip_error = max(clip_error, error)
                clip_pivot = max(clip_pivot, pivot)
                clip_support_angle = max(clip_support_angle, support_angle)
                maximum_lower_angle = max(maximum_lower_angle, lower_angle)
                maximum_support_local_translation = max(maximum_support_local_translation, support_location.length)
                maximum_support_scale_deviation = max(maximum_support_scale_deviation, scale_deviation)
        maximum_midpoint_error = max(maximum_midpoint_error, clip_error)
        maximum_pivot_separation = max(maximum_pivot_separation, clip_pivot)
        maximum_support_angle = max(maximum_support_angle, clip_support_angle)
        per_clip[clip.name] = {
            "maximumMidpointAngularErrorRadians": clip_error,
            "maximumElbowPivotSeparationMeters": clip_pivot,
            "maximumSupportLocalAngleRadians": clip_support_angle,
        }
    game_rig.animation_data.action = None
    base.set_constraints_muted(game_rig, False)
    bpy.context.view_layer.update()
    metrics = {
        "rule": "shortest-arc slerp(identity local pose, LowerArm local pose, 0.5)",
        "maximumMidpointAngularErrorRadians": maximum_midpoint_error,
        "maximumElbowPivotSeparationMeters": maximum_pivot_separation,
        "maximumSupportLocalAngleRadians": maximum_support_angle,
        "maximumLowerArmLocalAngleRadians": maximum_lower_angle,
        "maximumSupportLocalTranslationMeters": maximum_support_local_translation,
        "maximumSupportScaleDeviation": maximum_support_scale_deviation,
        "supportsShowAuthoredMotion": maximum_support_angle >= 0.10,
        "exportedCurvesAuthority": "DCC and Generic proof only",
        "runtimeAuthority": "Unity LateUpdate driver",
        "perClip": per_clip,
    }
    require(maximum_midpoint_error <= 0.00002, f"Support midpoint bake error is {maximum_midpoint_error}")
    require(maximum_pivot_separation <= 0.00002, f"Support pivot separation is {maximum_pivot_separation}")
    require(maximum_support_local_translation <= 0.00002, f"Support local translation is {maximum_support_local_translation}")
    require(maximum_support_scale_deviation <= 0.00002, f"Support scale deviation is {maximum_support_scale_deviation}")
    require(maximum_support_angle >= 0.10, "Support curves contain no useful motion")
    return metrics


def write_clip_contract(metrics: dict[str, object]) -> None:
    rows = [
        f"| `{clip.name}` | {clip.frame_start}-{clip.frame_end} | {'yes' if clip.loop else 'no'} | `{clip.category}` |"
        for clip in CLIPS
    ]
    CLIP_CONTRACT_PATH.write_text(
        """# Session Humanoid Animation Proof V2 clip contract

Status: **technical animation and bend-support proof; not visual acceptance;
not integrated into Unity**.

- 30 fps, meter scale, identity in-place Root on all sampled frames.
- Ten first-party Rigify-control performances: grounded combat idle, forward
  locomotion reusable in eight headings, hit, death, interrupt, and Hunter bow
  ready/draw/hold/loose/recovery.
- Every integer frame is baked onto all 68 clean game joints. The first 66 are
  the unchanged V1 contract; the appended elbow supports use analytic 0.5
  shortest-arc local rotation and preserve the elbow pivot.
- Support curves prove the DCC/Generic path only. Unity LateUpdate is explicitly
  authoritative for Humanoid, native, and prototype-fallback playback.
- The runtime importer must either disable Optimize Game Objects or expose the
  exact Left/RightElbowSupport transform paths. The driver runs after Animator
  and OnAnimatorIK using cached rest-local rotations; Humanoid retargeting alone
  does not make auxiliary curves authoritative.
- The same root-motion, grounded-feet, loop, death-collapse, bow travel, and
  transition gates used by V1 remain mandatory.

| Clip | Frames | Loop | Purpose |
|---|---:|---|---|
""" + "\n".join(rows) + "\n\n## Technical metrics\n\n```json\n"
        + json.dumps(metrics, indent=2, sort_keys=True) + "\n```\n",
        encoding="utf-8",
    )


def write_provenance(rig_manifest: dict[str, object]) -> None:
    PROVENANCE_PATH.write_text(
        f"""# Session Humanoid Animation Proof V2 provenance

Status: **first-party technical proof; not integrated into Unity**.

The ten numeric control performances are the project's original definitions in
`build_session_humanoid_animation_proof_v1.py`; V2 evaluates and rebakes them
from source against `SessionHumanoidRigV2`. No legacy, Quaternius, downloaded,
captured, or retargeted animation is imported or sampled.

The pinned V2 rig manifest SHA-256 is `{sha256(RIG_MANIFEST)}` and its clean
68-joint rig FBX SHA-256 is `{rig_manifest['outputHashes'][rig_contract.FBX_PATH.name]}`.
The first 66 rig records remain equal to frozen V1; V2 appends only two original
elbow support deform joints. Support keys are computed analytically rather than
sampling a Blender constraint. Only the clean game rig and ten baked actions are
exported. Rigify controls, metarig, mechanism/original/deform-helper bones,
widgets, meshes, and constraints remain source-only or absent.

Exported support curves are evidence for DCC and Generic playback. They are not
the production Humanoid authority: a future, separately reviewed Unity
LateUpdate driver must reconstruct the same midpoint after animation evaluation.
""",
        encoding="utf-8",
    )


def patch_manifest(
    rig_manifest: dict[str, object],
    support_metrics: dict[str, object],
) -> None:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    manifest["sourceRig"].update({
        "exportedJointCount": 68,
        "unityHumanoidMappedJointCount": 55,
        "additionalBendSupportJointCount": 2,
        "frozenV1PrefixJointCount": 66,
        "frozenV1PrefixRecordsSerializedSha256": rig_manifest["hierarchy"]["v1PrefixOrderedRecordsSerializedSha256"],
    })
    manifest["authoring"].update({
        "bendSupportBakeMethod": "analytic shortest-arc local quaternion slerp at 0.5, rest local translation, unit scale",
        "bendSupportCurvesAuthority": "DCC and Generic proof only",
        "bendSupportRuntimeAuthority": "Unity LateUpdate for Humanoid, native, and prototype fallback playback",
        "gameJointBakeCount": 68,
    })
    for clip in manifest["clips"]:
        require(clip["bakedAction"]["keyedBoneCount"] == 68, f"{clip['name']} was not baked on 68 joints")
        require(clip["bakedAction"]["fcurveCount"] == 680, f"{clip['name']} does not contain exactly ten channels per game joint")
    manifest["technicalMetrics"]["bendSupport"] = support_metrics
    manifest["exportContract"]["fbxJointCount"] = 68
    manifest["exportContract"]["bendSupportBoneCount"] = 2
    manifest["exportContract"]["bendSupportFcurveCountPerAction"] = 20
    manifest["exportContract"]["boneFcurveCountPerAction"] = 680
    manifest["exportContract"]["importedArmatureObjectTransformFcurveCountPerAction"] = 9
    manifest["exportContract"]["expectedImportedTotalFcurveCountPerAction"] = 689
    manifest["exportContract"]["curveTopologyGate"] = "exact unique channel/index tuples; one LINEAR key at every integer clip frame; no modifiers or extra keys; imported object transforms identity"
    manifest["exportContract"]["bendSupportCurvesAuthority"] = "DCC and Generic proof only"
    manifest["v1Compatibility"] = {
        "sameTenFirstPartyClipNamesAndRanges": True,
        "first66RigRecordsUnchanged": True,
        "first66RigRecordsSerializedSha256": rig_manifest["hierarchy"]["v1PrefixOrderedRecordsSerializedSha256"],
        "frozenAnimationBlendPath": str(V1_ANIMATION_BLEND.relative_to(PROJECT_ROOT)),
        "frozenAnimationBlendSha256": EXPECTED_V1_ANIMATION_BLEND_SHA256,
        "frozenAnimationFbxPath": str(V1_ANIMATION_FBX.relative_to(PROJECT_ROOT)),
        "frozenAnimationFbxSha256": EXPECTED_V1_ANIMATION_FBX_SHA256,
        "frozenAnimationManifestPath": str(V1_ANIMATION_MANIFEST.relative_to(PROJECT_ROOT)),
        "frozenAnimationManifestSha256": EXPECTED_V1_ANIMATION_MANIFEST_SHA256,
        "first66EveryClipEveryFrameSemanticComparisonRequired": True,
        "sourceBlendAndFbxRoundTripComparisonRequired": True,
        "v1AnimationFilesWrittenByBuild": False,
    }
    manifest["runtimeSupportDriverContract"] = rig_manifest["unityRuntimeSupportDriverContract"]
    manifest["sourceFiles"] = {
        str(SCRIPT_PATH.relative_to(PROJECT_ROOT)): sha256(SCRIPT_PATH),
        str(VERIFIER_PATH.relative_to(PROJECT_ROOT)): sha256(VERIFIER_PATH),
        str(CLIP_CONTRACT_PATH.relative_to(PROJECT_ROOT)): sha256(CLIP_CONTRACT_PATH),
        str(PROVENANCE_PATH.relative_to(PROJECT_ROOT)): sha256(PROVENANCE_PATH),
        str(Path(base.__file__).resolve().relative_to(PROJECT_ROOT)): sha256(Path(base.__file__).resolve()),
        str(Path(rig_contract.__file__).resolve().relative_to(PROJECT_ROOT)): sha256(Path(rig_contract.__file__).resolve()),
        str(RIG_MANIFEST.relative_to(PROJECT_ROOT)): sha256(RIG_MANIFEST),
        str(V1_ANIMATION_BLEND.relative_to(PROJECT_ROOT)): EXPECTED_V1_ANIMATION_BLEND_SHA256,
        str(V1_ANIMATION_FBX.relative_to(PROJECT_ROOT)): EXPECTED_V1_ANIMATION_FBX_SHA256,
        str(V1_ANIMATION_MANIFEST.relative_to(PROJECT_ROOT)): EXPECTED_V1_ANIMATION_MANIFEST_SHA256,
    }
    manifest["outputHashes"] = {
        SOURCE_BLEND.name: sha256(SOURCE_BLEND),
        FBX_PATH.name: sha256(FBX_PATH),
        PROVENANCE_PATH.name: sha256(PROVENANCE_PATH),
    }
    manifest["approvalBoundary"]["nextStep"] = "Bind accepted bodies to RigV2, rerun unchanged elbow thresholds and full-frame deformation scans, then implement/review the authoritative Unity LateUpdate support driver as a separate task."
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> None:
    configure_base_animation_module()
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    require(RIG_BLEND.is_file() and RIG_MANIFEST.is_file(), "RigV2 source contract is missing")
    require(VERIFIER_PATH.is_file(), "AnimationProofV2 verifier is missing")
    require("Assets" not in SOURCE_DIR.parts, "AnimationProofV2 must remain outside Assets")
    assert_frozen_v1_animation()
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    game_rig, control_rig, rig_manifest = load_rig_source_v2()
    require(rig_manifest["assetId"] == rig_contract.ASSET_ID, "Animation proof opened the wrong rig")
    require(rig_manifest["hierarchy"]["exportedBoneCount"] == 68, "Animation proof requires RigV2's 68 joints")
    control_actions: dict[str, bpy.types.Action] = {}
    sampled_by_clip: dict[str, dict[int, dict[str, Matrix]]] = {}
    baked_actions: dict[str, bpy.types.Action] = {}
    for clip in CLIPS:
        control_action = base.create_control_action(control_rig, clip)
        control_actions[clip.name] = control_action
        sampled_by_clip[clip.name] = base.sample_clip(game_rig, control_rig, control_action, clip)
    base.stabilize_intentionally_identical_endpoints(sampled_by_clip)
    for clip in CLIPS:
        baked_actions[clip.name] = create_baked_action(game_rig, clip, sampled_by_clip[clip.name])
    metrics, _samples = base.collect_action_metrics(game_rig, baked_actions)
    base.validate_metrics(metrics)
    support_metrics = collect_support_metrics(game_rig, baked_actions)
    metrics["bendSupport"] = support_metrics
    print("PRELIMINARY_ANIMATION_V2_METRICS", json.dumps(metrics, sort_keys=True))
    control_reports = {name: base.action_curve_report(action) for name, action in control_actions.items()}
    baked_reports = {name: base.action_curve_report(action) for name, action in baked_actions.items()}
    require(all(report["keyedBoneCount"] == 68 for report in baked_reports.values()), "A baked action is not full 68-joint")
    write_clip_contract(metrics)
    write_provenance(rig_manifest)
    base.save_source_blend(game_rig, control_rig)
    base.prepare_clean_export_scene(game_rig, control_actions, baked_actions)
    base.export_animation_fbx(game_rig)
    base.write_manifest(rig_manifest, control_reports, baked_reports, metrics)
    patch_manifest(rig_manifest, support_metrics)
    print("SESSION_HUMANOID_ANIMATION_PROOF_V2_BUILD_OK")
    print("CLIPS 10 SOURCE_ACTIONS 10 BAKED_ACTIONS 10 JOINTS 68 SUPPORTS 2")
    print("FBX", FBX_PATH)
    print("SOURCE_BLEND", SOURCE_BLEND)
    print("SUPPORT_METRICS", json.dumps(support_metrics, sort_keys=True))


if __name__ == "__main__":
    main()
