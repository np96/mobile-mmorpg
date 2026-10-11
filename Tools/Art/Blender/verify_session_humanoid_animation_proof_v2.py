#!/usr/bin/env python3
"""Independent all-frame verifier for SessionHumanoidAnimationProofV2."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Quaternion, Vector


SCRIPT_PATH = Path(__file__).resolve()
PROJECT_ROOT = SCRIPT_PATH.parents[3]
sys.path.insert(0, str(SCRIPT_PATH.parent))
import build_session_humanoid_animation_proof_v2 as proof
import build_session_humanoid_rig_v2 as rig_contract


REPORT_PATH = (
    PROJECT_ROOT.parent
    / "outputs/art/blender/session-humanoid-animation-proof-v2/SessionHumanoidAnimationProofV2RoundTrip.json"
)
JOINT_TOLERANCE_METERS = 0.00002
METRIC_TOLERANCE = 0.010
SOURCE_V1_SEMANTIC_TOLERANCE = 0.00002
FBX_V1_SEMANTIC_TOLERANCE = 0.00005
OBJECT_CURVE_IDENTITY_TOLERANCE = 0.000001


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def near_zero(values, tolerance=0.00001) -> bool:
    return max(abs(value) for value in values) <= tolerance


def near_one(values, tolerance=0.00001) -> bool:
    return max(abs(value - 1.0) for value in values) <= tolerance


def action_keyed_bones(action: bpy.types.Action) -> set[str]:
    result = set()
    for curve in proof.base.iter_action_fcurves(action):
        if 'pose.bones["' in curve.data_path:
            result.add(curve.data_path.split('pose.bones["', 1)[1].split('"]', 1)[0])
    return result


def expected_bone_curve_keys() -> set[tuple[str, int]]:
    result = set()
    for bone_name in rig_contract.SPEC_BY_NAME:
        prefix = f'pose.bones["{bone_name}"]'
        result.update((prefix + ".location", index) for index in range(3))
        result.update((prefix + ".rotation_quaternion", index) for index in range(4))
        result.update((prefix + ".scale", index) for index in range(3))
    return result


EXPECTED_BONE_CURVE_KEYS = expected_bone_curve_keys()
EXPECTED_OBJECT_CURVE_KEYS = {
    *(("location", index) for index in range(3)),
    *(("rotation_euler", index) for index in range(3)),
    *(("scale", index) for index in range(3)),
}
EXPECTED_SUPPORT_CURVE_KEYS = {
    key
    for key in EXPECTED_BONE_CURVE_KEYS
    if any(f'pose.bones["{name}"]' in key[0] for name in rig_contract.SUPPORT_NAMES)
}


def verify_baked_action_curve_topology(
    action: bpy.types.Action,
    clip: proof.ClipSpec,
    *,
    imported_fbx: bool,
) -> dict[str, object]:
    curves = list(proof.base.iter_action_fcurves(action))
    actual_keys = [(curve.data_path, curve.array_index) for curve in curves]
    expected_keys = EXPECTED_BONE_CURVE_KEYS | (EXPECTED_OBJECT_CURVE_KEYS if imported_fbx else set())
    require(len(actual_keys) == len(set(actual_keys)), f"{action.name} contains duplicate curve channel/index tuples")
    require(set(actual_keys) == expected_keys, f"{action.name} curve topology changed: {set(actual_keys) ^ expected_keys}")
    expected_frames = set(range(clip.frame_start, clip.frame_end + 1))
    maximum_non_integer_error = 0.0
    maximum_object_identity_error = 0.0
    for curve in curves:
        require(not curve.modifiers, f"{action.name} curve {curve.data_path}[{curve.array_index}] has a modifier")
        frames = []
        for point in curve.keyframe_points:
            nearest = round(point.co.x)
            maximum_non_integer_error = max(maximum_non_integer_error, abs(point.co.x - nearest))
            require(abs(point.co.x - nearest) <= 1e-7, f"{action.name} has a non-integer key time {point.co.x}")
            require(point.interpolation == "LINEAR", f"{action.name} has non-LINEAR interpolation")
            frames.append(nearest)
        require(len(frames) == len(expected_frames) and len(frames) == len(set(frames)), f"{action.name} has duplicate/missing per-frame keys")
        require(set(frames) == expected_frames, f"{action.name} frame topology changed for {curve.data_path}[{curve.array_index}]")
        if imported_fbx and (curve.data_path, curve.array_index) in EXPECTED_OBJECT_CURVE_KEYS:
            expected_value = 1.0 if curve.data_path == "scale" else 0.0
            for frame in expected_frames:
                error = abs(curve.evaluate(frame) - expected_value)
                maximum_object_identity_error = max(maximum_object_identity_error, error)
                require(error <= OBJECT_CURVE_IDENTITY_TOLERANCE, f"{action.name} animates armature object {curve.data_path}[{curve.array_index}]: {error}")
    support_count = sum(key in EXPECTED_SUPPORT_CURVE_KEYS for key in actual_keys)
    require(support_count == 20, f"{action.name} has {support_count} support FCurves instead of 20")
    return {
        "boneFcurveCount": sum(key in EXPECTED_BONE_CURVE_KEYS for key in actual_keys),
        "armatureObjectFcurveCount": sum(key in EXPECTED_OBJECT_CURVE_KEYS for key in actual_keys),
        "totalFcurveCount": len(curves),
        "bendSupportFcurveCount": support_count,
        "uniqueChannelIndexTuples": True,
        "exactIntegerFrameSetPerCurve": True,
        "allLinear": True,
        "modifiers": 0,
        "maximumNonIntegerFrameError": maximum_non_integer_error,
        "maximumArmatureObjectIdentityError": maximum_object_identity_error,
        "armatureObjectIdentityTolerance": OBJECT_CURVE_IDENTITY_TOLERANCE,
    }


def normalized_action_name(name: str) -> str:
    for expected in proof.CLIP_BY_NAME:
        if name == expected or name.endswith("|" + expected) or name.endswith("_" + expected):
            return expected
    return name


def compare_metrics(expected, actual, path="technicalMetrics", tolerance=METRIC_TOLERANCE) -> None:
    if isinstance(expected, dict):
        require(isinstance(actual, dict) and set(expected) == set(actual), f"{path} keys/type changed")
        for key in expected:
            compare_metrics(expected[key], actual[key], f"{path}.{key}", tolerance)
    elif isinstance(expected, list):
        require(isinstance(actual, list) and len(expected) == len(actual), f"{path} list changed")
        for index, (left, right) in enumerate(zip(expected, actual)):
            compare_metrics(left, right, f"{path}[{index}]", tolerance)
    elif isinstance(expected, (int, float)) and not isinstance(expected, bool):
        require(isinstance(actual, (int, float)), f"{path} is not numeric")
        require(abs(float(expected) - float(actual)) <= tolerance, f"{path} changed: {expected} vs {actual}")
    else:
        require(expected == actual, f"{path} changed: {expected!r} vs {actual!r}")


def actions_by_clip_from_current_file() -> dict[str, bpy.types.Action]:
    result: dict[str, bpy.types.Action] = {}
    unexpected = []
    for action in bpy.data.actions:
        normalized = normalized_action_name(action.name)
        if normalized in proof.CLIP_BY_NAME and normalized not in result:
            result[normalized] = action
        elif not action.name.startswith(proof.CONTROL_ACTION_PREFIX):
            unexpected.append(action.name)
    require(not unexpected, f"Unexpected or duplicate baked actions: {unexpected}")
    require(set(result) == set(proof.CLIP_BY_NAME), f"Baked action set changed: {set(result) ^ set(proof.CLIP_BY_NAME)}")
    return result


def semantic_snapshot(
    rig: bpy.types.Object,
    actions: dict[str, bpy.types.Action],
    bone_names: tuple[str, ...],
) -> dict[str, dict[int, dict[str, tuple]]]:
    proof.base.set_constraints_muted(rig, True)
    rig.animation_data_create()
    snapshot = {}
    for clip in proof.CLIPS:
        rig.animation_data.action = actions[clip.name]
        frames = {}
        for frame in range(clip.frame_start, clip.frame_end + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            evaluated = rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
            bones = {}
            for name in bone_names:
                basis = proof.evaluated_local_basis(rig, evaluated, name)
                location, rotation, scale = basis.decompose()
                rotation.normalize()
                bones[name] = {
                    "location": tuple(float(value) for value in location),
                    "rotation": tuple(float(value) for value in rotation),
                    "scale": tuple(float(value) for value in scale),
                }
            frames[frame] = bones
        snapshot[clip.name] = frames
    rig.animation_data.action = None
    proof.base.set_constraints_muted(rig, False)
    bpy.context.view_layer.update()
    return snapshot


def compare_semantic_snapshots(
    v1: dict[str, dict[int, dict[str, tuple]]],
    v2: dict[str, dict[int, dict[str, tuple]]],
    tolerance: float,
    label: str,
) -> dict[str, object]:
    require(v1.keys() == v2.keys(), f"{label} clip set changed")
    maximum_location = 0.0
    maximum_rotation = 0.0
    maximum_scale = 0.0
    compared = 0
    for clip_name in v1:
        require(v1[clip_name].keys() == v2[clip_name].keys(), f"{label} frame set changed for {clip_name}")
        for frame in v1[clip_name]:
            require(v1[clip_name][frame].keys() == v2[clip_name][frame].keys(), f"{label} bone set changed")
            for bone_name in v1[clip_name][frame]:
                left = v1[clip_name][frame][bone_name]
                right = v2[clip_name][frame][bone_name]
                location_error = (Vector(left["location"]) - Vector(right["location"])).length
                scale_error = max(abs(a - b) for a, b in zip(left["scale"], right["scale"]))
                left_rotation = Quaternion(left["rotation"])
                right_rotation = Quaternion(right["rotation"])
                if left_rotation.dot(right_rotation) < 0.0:
                    right_rotation.negate()
                rotation_error = left_rotation.rotation_difference(right_rotation).angle
                maximum_location = max(maximum_location, location_error)
                maximum_rotation = max(maximum_rotation, rotation_error)
                maximum_scale = max(maximum_scale, scale_error)
                compared += 1
    require(maximum_location <= tolerance, f"{label} V1 location changed: {maximum_location}")
    require(maximum_rotation <= tolerance, f"{label} V1 rotation changed: {maximum_rotation}")
    require(maximum_scale <= tolerance, f"{label} V1 scale changed: {maximum_scale}")
    return {
        "status": "PASS_FIRST_66_EVERY_CLIP_EVERY_FRAME_SEMANTIC_EQUALITY",
        "clipCount": len(v1),
        "sampledBoneFrames": compared,
        "maximumLocalTranslationDifferenceMeters": maximum_location,
        "maximumLocalRotationDifferenceRadians": maximum_rotation,
        "maximumLocalScaleComponentDifference": maximum_scale,
        "tolerance": tolerance,
    }


def verify_files(manifest: dict[str, object]) -> None:
    require("Assets" not in proof.SOURCE_DIR.parts, "AnimationProofV2 is under Unity Assets")
    require(not list((PROJECT_ROOT / "Assets").rglob("*SessionHumanoidAnimationProofV2*")), "V2 animation was integrated before approval")
    require(manifest["integrationStatus"] == "SOURCE_DCC_NOT_IN_UNITY", "Integration boundary changed")
    require(manifest["status"] == "PASS_TECHNICAL_ANIMATION_PROOF_NOT_VISUAL_ACCEPTANCE", "Technical-only status changed")
    for relative, expected_hash in manifest["sourceFiles"].items():
        path = PROJECT_ROOT / relative
        require(path.is_file() and sha256(path) == expected_hash, f"Missing/stale source: {path}")
    for name, expected_hash in manifest["outputHashes"].items():
        path = proof.SOURCE_BLEND if name == proof.SOURCE_BLEND.name else proof.OUTPUT_DIR / name
        require(path.is_file() and sha256(path) == expected_hash, f"Missing/stale output: {path}")
    proof.assert_frozen_v1_animation()


def verify_manifest(manifest: dict[str, object], rig_manifest: dict[str, object]) -> None:
    require(manifest["schemaVersion"] == 1 and manifest["assetId"] == proof.ASSET_ID, "Wrong animation manifest identity")
    require(manifest["fps"] == 30 and manifest["sourceRig"]["exportedJointCount"] == 68, "Rig/FPS contract changed")
    require(manifest["sourceRig"]["manifestSha256"] == sha256(proof.RIG_MANIFEST), "Source rig manifest pin changed")
    require(manifest["sourceRig"]["fbxSha256"] == rig_manifest["outputHashes"][rig_contract.FBX_PATH.name], "Source rig FBX pin changed")
    require(manifest["sourceRig"]["frozenV1PrefixRecordsSerializedSha256"] == rig_manifest["hierarchy"]["v1PrefixOrderedRecordsSerializedSha256"], "V1 rig prefix pin changed")
    compatibility = manifest["v1Compatibility"]
    require(compatibility["first66EveryClipEveryFrameSemanticComparisonRequired"], "All-frame V1 comparison is not required")
    require(compatibility["sourceBlendAndFbxRoundTripComparisonRequired"], "Source/FBX dual comparison is not required")
    require(not compatibility["v1AnimationFilesWrittenByBuild"], "V2 claims to write V1 animation")
    require(compatibility["frozenAnimationBlendSha256"] == proof.EXPECTED_V1_ANIMATION_BLEND_SHA256, "Wrong V1 blend pin")
    require(compatibility["frozenAnimationFbxSha256"] == proof.EXPECTED_V1_ANIMATION_FBX_SHA256, "Wrong V1 FBX pin")
    require(compatibility["frozenAnimationManifestSha256"] == proof.EXPECTED_V1_ANIMATION_MANIFEST_SHA256, "Wrong V1 manifest pin")
    require([clip["name"] for clip in manifest["clips"]] == [clip.name for clip in proof.CLIPS], "Clip order changed")
    expected_bones = set(rig_contract.SPEC_BY_NAME)
    for expected, recorded in zip(proof.CLIPS, manifest["clips"]):
        require(recorded["frameStart"] == expected.frame_start and recorded["frameEnd"] == expected.frame_end, f"{expected.name} range changed")
        require(recorded["loop"] == expected.loop and recorded["category"] == expected.category, f"{expected.name} metadata changed")
        require(recorded["bakedAction"]["keyedBoneCount"] == 68, f"{expected.name} is not a 68-joint bake")
        require(set(recorded["bakedAction"]["keyedBones"]) == expected_bones, f"{expected.name} keyed set changed")
        require(recorded["sourceAction"]["allLinear"] and recorded["bakedAction"]["allLinear"], f"{expected.name} interpolation changed")
    export = manifest["exportContract"]
    require(export["fbxJointCount"] == 68 and export["fbxActions"] == 10 and export["fbxMeshObjects"] == 0, "FBX export contract changed")
    require(export["fbxConstraints"] == 0 and export["fbxControlMechanismWidgetBones"] == 0, "FBX permits helpers")
    require(export["bendSupportBoneCount"] == 2 and export["bendSupportFcurveCountPerAction"] == 20, "Support bone/FCurve counts changed")
    require(export["boneFcurveCountPerAction"] == 680, "Bone FCurve count changed")
    require(export["importedArmatureObjectTransformFcurveCountPerAction"] == 9, "Imported object FCurve count changed")
    require(export["expectedImportedTotalFcurveCountPerAction"] == 689, "Imported total FCurve count changed")
    require(export["bendSupportCurvesAuthority"] == "DCC and Generic proof only", "Support curve authority changed")
    runtime = manifest["runtimeSupportDriverContract"]
    require(runtime["implementationStatus"] == "NOT_IMPLEMENTED_IN_THIS_DCC_TASK", "Runtime integration falsely claimed")
    require(not runtime["humanoidRetargetingAloneIsAuthoritative"], "Humanoid retargeting is incorrectly authoritative")
    require(len(runtime["optimizeGameObjectsPrecondition"]["requiredExtraExposedTransformPathsWhenOptimized"]) == 2, "Runtime support paths changed")


def inspect_support_rest_and_props(rig: bpy.types.Object, imported: bool) -> dict[str, object]:
    by_name = {bone.name: bone for bone in rig.data.bones}
    maximum_pivot = 0.0
    maximum_basis = 0.0
    properties = {}
    for side in ("Left", "Right"):
        support = by_name[side + "ElbowSupport"]
        lower = by_name[side + "LowerArm"]
        require((support.parent.name if support.parent else None) == side + "UpperArm", f"{support.name} parent changed")
        pivot = (support.head_local - lower.head_local).length
        basis = max(abs(support.matrix_local[r][c] - lower.matrix_local[r][c]) for r in range(3) for c in range(3))
        maximum_pivot = max(maximum_pivot, pivot)
        maximum_basis = max(maximum_basis, basis)
        require(pivot <= JOINT_TOLERANCE_METERS and basis <= JOINT_TOLERANCE_METERS, f"{support.name} rest contract changed")
        require(support.use_deform, f"{support.name} is not marked deforming")
        current = {
            "sessionRole": support.get("sessionRole"),
            "runtimeDriverAuthority": support.get("runtimeDriverAuthority"),
            "genericDccProofOnly": support.get("genericDccProofOnly"),
        }
        require(current == {
            "sessionRole": "bend_support",
            "runtimeDriverAuthority": "Unity LateUpdate support-bone driver",
            "genericDccProofOnly": True,
        }, f"{support.name} role/provenance changed")
        properties[support.name] = current
    return {
        "maximumSupportElbowPivotSeparationMeters": maximum_pivot,
        "maximumSupportVsLowerArmRestBasisComponentError": maximum_basis,
        "supportProperties": properties,
        "leafDisplayLengthSemantic": False,
        "supportUseDeform": True,
        "importedFbx": imported,
    }


def verify_source_blend(manifest: dict[str, object]) -> tuple[dict[str, object], dict[str, object], dict[str, object]]:
    bpy.ops.wm.open_mainfile(filepath=str(proof.SOURCE_BLEND))
    require(not bpy.data.libraries, "V2 source contains linked libraries")
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    require({obj.name for obj in armatures} == {rig_contract.RIG_OBJECT_NAME, rig_contract.RIGIFY_META_NAME, rig_contract.RIGIFY_CONTROL_NAME}, "V2 source armature set changed")
    require(not any(obj.type in {"CAMERA", "LIGHT"} for obj in objects), "V2 source contains camera/light")
    game_rig = bpy.data.objects[proof.GAME_RIG_NAME]
    control_rig = bpy.data.objects[proof.CONTROL_RIG_NAME]
    require(set(bone.name for bone in game_rig.data.bones) == set(rig_contract.SPEC_BY_NAME), "V2 source bone set changed")
    require(sum(len(bone.constraints) for bone in game_rig.pose.bones) == 60, "V2 source DCC constraints changed")
    require(game_rig.animation_data is None or game_rig.animation_data.action is None, "Game rig saved with active action")
    require(control_rig.animation_data is None or control_rig.animation_data.action is None, "Control rig saved with active action")
    actions = {action.name: action for action in bpy.data.actions}
    expected_names = set(proof.CLIP_BY_NAME) | {proof.CONTROL_ACTION_PREFIX + name for name in proof.CLIP_BY_NAME}
    require(set(actions) == expected_names, f"V2 source action set changed: {set(actions) ^ expected_names}")
    baked = {name: actions[name] for name in proof.CLIP_BY_NAME}
    expected_bones = set(rig_contract.SPEC_BY_NAME)
    curve_topology = {}
    for clip in proof.CLIPS:
        require(action_keyed_bones(baked[clip.name]) == expected_bones, f"Source {clip.name} does not key all 68 joints")
        require(baked[clip.name].get("sessionRole") == "clean_68_joint_game_bake_v2_with_generic_support_proof", f"Source {clip.name} role changed")
        curve_topology[clip.name] = verify_baked_action_curve_topology(baked[clip.name], clip, imported_fbx=False)
    standard_metrics, _ = proof.base.collect_action_metrics(game_rig, baked)
    proof.base.validate_metrics(standard_metrics)
    support_metrics = proof.collect_support_metrics(game_rig, baked)
    standard_metrics["bendSupport"] = support_metrics
    compare_metrics(manifest["technicalMetrics"], standard_metrics, path="sourceMetrics", tolerance=0.003)
    rest_report = inspect_support_rest_and_props(game_rig, imported=False)
    v2_snapshot = semantic_snapshot(game_rig, baked, tuple(spec.name for spec in rig_contract.V1_BONE_SPECS))

    bpy.ops.wm.open_mainfile(filepath=str(proof.V1_ANIMATION_BLEND))
    v1_rig = bpy.data.objects["SessionHumanoidRigV1"]
    v1_actions = {name: bpy.data.actions[name] for name in proof.CLIP_BY_NAME}
    v1_snapshot = semantic_snapshot(v1_rig, v1_actions, tuple(spec.name for spec in rig_contract.V1_BONE_SPECS))
    comparison = compare_semantic_snapshots(v1_snapshot, v2_snapshot, SOURCE_V1_SEMANTIC_TOLERANCE, "sourceBlend")
    return {
        "objects": len(objects),
        "armatures": len(armatures),
        "actions": 20,
        "rigifyControlActions": 10,
        "cleanGameBakedActions": 10,
        "gameJointCount": 68,
        "gameDccDriverConstraints": 60,
        "allBakedActionsKeyExactly68Bones": True,
        "bendSupportEveryFrameVerified": True,
        "curveTopologyReports": curve_topology,
        "supportRestContract": rest_report,
    }, standard_metrics, comparison


def import_animation_fbx(path: Path, expected_rig_name: str) -> tuple[bpy.types.Object, dict[str, bpy.types.Action]]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(path), use_anim=True, automatic_bone_orientation=False, ignore_leaf_bones=False)
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    require(len(objects) == 1 and len(armatures) == 1, f"FBX {path.name} is not skeleton-only")
    rig = armatures[0]
    require(rig.name == expected_rig_name, f"FBX rig name changed: {rig.name}")
    require(rig.parent is None and near_zero(rig.location) and near_zero(rig.rotation_euler) and near_one(rig.scale), "FBX rig object is not identity")
    require(not rig.constraints and not any(bone.constraints for bone in rig.pose.bones), "FBX contains constraints")
    actions = actions_by_clip_from_current_file()
    return rig, actions


def verify_fbx(manifest: dict[str, object]) -> tuple[dict[str, object], dict[str, object], dict[str, object]]:
    rig, actions = import_animation_fbx(proof.FBX_PATH, proof.GAME_RIG_NAME)
    by_name = {bone.name: bone for bone in rig.data.bones}
    require(len(by_name) == 68 and set(by_name) == set(rig_contract.SPEC_BY_NAME), "V2 animation FBX bone set changed")
    maximum_joint_error = 0.0
    for spec in rig_contract.BONE_SPECS:
        bone = by_name[spec.name]
        require((bone.parent.name if bone.parent else None) == spec.parent, f"FBX parent changed for {spec.name}")
        error = (bone.head_local - Vector(spec.head)).length
        maximum_joint_error = max(maximum_joint_error, error)
        require(error <= JOINT_TOLERANCE_METERS, f"FBX joint changed for {spec.name}: {error}")
    forbidden = rig_contract.FORBIDDEN_BONE_TOKENS + ("RIGIFY", "METARIG", "WGT-")
    require(not any(any(token in name.upper() for token in forbidden) for name in by_name), "FBX contains DCC/helper bones")
    expected_bones = set(rig_contract.SPEC_BY_NAME)
    curve_topology = {}
    for clip in proof.CLIPS:
        require(action_keyed_bones(actions[clip.name]) == expected_bones, f"FBX {clip.name} does not key all 68 bones")
        require(tuple(round(value) for value in actions[clip.name].frame_range) == (clip.frame_start, clip.frame_end), f"FBX {clip.name} range changed")
        curve_topology[clip.name] = verify_baked_action_curve_topology(actions[clip.name], clip, imported_fbx=True)
    standard_metrics, _ = proof.base.collect_action_metrics(rig, actions)
    proof.base.validate_metrics(standard_metrics)
    support_metrics = proof.collect_support_metrics(rig, actions)
    standard_metrics["bendSupport"] = support_metrics
    compare_metrics(manifest["technicalMetrics"], standard_metrics, path="fbxMetrics", tolerance=METRIC_TOLERANCE)
    rest_report = inspect_support_rest_and_props(rig, imported=True)
    v2_snapshot = semantic_snapshot(rig, actions, tuple(spec.name for spec in rig_contract.V1_BONE_SPECS))

    v1_rig, v1_actions = import_animation_fbx(proof.V1_ANIMATION_FBX, "SessionHumanoidRigV1")
    v1_snapshot = semantic_snapshot(v1_rig, v1_actions, tuple(spec.name for spec in rig_contract.V1_BONE_SPECS))
    comparison = compare_semantic_snapshots(v1_snapshot, v2_snapshot, FBX_V1_SEMANTIC_TOLERANCE, "fbxRoundTrip")
    return {
        "objects": 1,
        "armatures": 1,
        "meshes": 0,
        "actions": 10,
        "constraints": 0,
        "jointCount": 68,
        "maximumJointHeadDeviationMeters": maximum_joint_error,
        "identityRootObjectTransform": True,
        "controlsMechanismsWidgets": 0,
        "allActionsKeyExactly68Bones": True,
        "bendSupportEveryFrameVerified": True,
        "curveTopologyReports": curve_topology,
        "supportRestContract": rest_report,
        "actionNames": sorted(actions),
    }, standard_metrics, comparison


def main() -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    proof.configure_base_animation_module()
    require(proof.MANIFEST_PATH.is_file(), "AnimationProofV2 manifest missing")
    manifest = json.loads(proof.MANIFEST_PATH.read_text(encoding="utf-8"))
    rig_manifest = json.loads(proof.RIG_MANIFEST.read_text(encoding="utf-8"))
    verify_files(manifest)
    verify_manifest(manifest, rig_manifest)
    source_report, source_metrics, source_v1_comparison = verify_source_blend(manifest)
    fbx_report, fbx_metrics, fbx_v1_comparison = verify_fbx(manifest)
    report = {
        "status": "PASS_TECHNICAL_ANIMATION_PROOF_ROUND_TRIP_NOT_IN_UNITY",
        "assetId": proof.ASSET_ID,
        "sourceBlendSha256": manifest["outputHashes"][proof.SOURCE_BLEND.name],
        "fbxSha256": manifest["outputHashes"][proof.FBX_PATH.name],
        "manifestSha256": sha256(proof.MANIFEST_PATH),
        "sourceRig": manifest["sourceRig"],
        "frozenV1Animation": {
            "blendSha256": proof.EXPECTED_V1_ANIMATION_BLEND_SHA256,
            "fbxSha256": proof.EXPECTED_V1_ANIMATION_FBX_SHA256,
            "manifestSha256": proof.EXPECTED_V1_ANIMATION_MANIFEST_SHA256,
            "sourceBlendSemanticComparison": source_v1_comparison,
            "fbxRoundTripSemanticComparison": fbx_v1_comparison,
        },
        "sourceBlend": source_report,
        "sourceMetrics": source_metrics,
        "fbxRoundTrip": fbx_report,
        "fbxMetrics": fbx_metrics,
        "supportCurvesAuthority": "DCC and Generic proof only",
        "runtimeSupportDriverAuthority": "Unity LateUpdate",
        "runtimeSupportDriverImplemented": False,
        "unityIntegrationPerformed": False,
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
