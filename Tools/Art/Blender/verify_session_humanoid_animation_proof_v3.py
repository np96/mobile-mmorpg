#!/usr/bin/env python3
"""Independent source/FBX verifier for SessionHumanoidAnimationProofV3."""

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
import build_session_humanoid_animation_proof_v3 as proof
import build_session_humanoid_rig_v2 as rig_contract
import verify_session_humanoid_animation_proof_v2 as v2_verify


REPORT_PATH = (
    PROJECT_ROOT.parent
    / "outputs/art/blender/session-humanoid-animation-proof-v3/SessionHumanoidAnimationProofV3RoundTrip.json"
)
JOINT_TOLERANCE_METERS = 0.00002
SOURCE_FBX_SEMANTIC_TOLERANCE = 0.00005
UNCHANGED_V2_SEMANTIC_TOLERANCE = 0.00002


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


def configure() -> tuple[dict[str, object], tuple[proof.ClipSpec, ...]]:
    plan = json.loads(proof.PLAN_PATH.read_text(encoding="utf-8"))
    clips = proof.build_clip_specs(plan)
    proof.CLIPS = clips
    proof.CLIP_BY_NAME = {clip.name: clip for clip in clips}
    proof.configure_modules(clips)
    return plan, clips


def action_keyed_bones(action: bpy.types.Action) -> set[str]:
    result = set()
    for curve in proof.v2.base.iter_action_fcurves(action):
        if 'pose.bones["' in curve.data_path:
            result.add(curve.data_path.split('pose.bones["', 1)[1].split('"]', 1)[0])
    return result


def actions_by_clip() -> dict[str, bpy.types.Action]:
    result = {}
    unexpected = []
    for action in bpy.data.actions:
        normalized = v2_verify.normalized_action_name(action.name)
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
    *,
    imported_fbx: bool,
) -> dict[str, dict[int, dict[str, object]]]:
    proof.v2.base.set_constraints_muted(rig, True)
    snapshot = {}
    for clip in proof.CLIPS:
        proof.bind_action_slot(rig, actions[clip.name], clip.name, imported_fbx=imported_fbx)
        frames = {}
        for frame in range(clip.frame_start, clip.frame_end + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            evaluated = rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
            bones = {}
            for name in bone_names:
                basis = proof.v2.evaluated_local_basis(rig, evaluated, name)
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
    proof.v2.base.set_constraints_muted(rig, False)
    bpy.context.view_layer.update()
    return snapshot


def compare_snapshots(
    first: dict[str, dict[int, dict[str, object]]],
    second: dict[str, dict[int, dict[str, object]]],
    tolerance: float,
    label: str,
) -> dict[str, object]:
    require(first.keys() == second.keys(), f"{label} clip set changed")
    maximum_location = 0.0
    maximum_rotation = 0.0
    maximum_scale = 0.0
    compared = 0
    worst = None
    for clip_name in first:
        require(first[clip_name].keys() == second[clip_name].keys(), f"{label} frame set changed")
        for frame in first[clip_name]:
            require(first[clip_name][frame].keys() == second[clip_name][frame].keys(), f"{label} bone set changed")
            for bone_name in first[clip_name][frame]:
                left = first[clip_name][frame][bone_name]
                right = second[clip_name][frame][bone_name]
                location_error = (Vector(left["location"]) - Vector(right["location"])).length
                scale_error = max(abs(a - b) for a, b in zip(left["scale"], right["scale"]))
                left_rotation = Quaternion(left["rotation"])
                right_rotation = Quaternion(right["rotation"])
                if left_rotation.dot(right_rotation) < 0.0:
                    right_rotation.negate()
                rotation_error = left_rotation.rotation_difference(right_rotation).angle
                current = max(location_error, scale_error, rotation_error)
                if worst is None or current > worst["maximumComponent"]:
                    worst = {
                        "clip": clip_name,
                        "frame": frame,
                        "bone": bone_name,
                        "translationMeters": location_error,
                        "rotationRadians": rotation_error,
                        "scaleComponent": scale_error,
                        "maximumComponent": current,
                    }
                maximum_location = max(maximum_location, location_error)
                maximum_rotation = max(maximum_rotation, rotation_error)
                maximum_scale = max(maximum_scale, scale_error)
                compared += 1
    require(maximum_location <= tolerance, f"{label} translation changed: {maximum_location}")
    require(maximum_rotation <= tolerance, f"{label} rotation changed: {maximum_rotation}")
    require(maximum_scale <= tolerance, f"{label} scale changed: {maximum_scale}")
    return {
        "status": "PASS",
        "sampledBoneFrames": compared,
        "maximumLocalTranslationDifferenceMeters": maximum_location,
        "maximumLocalRotationDifferenceRadians": maximum_rotation,
        "maximumLocalScaleComponentDifference": maximum_scale,
        "tolerance": tolerance,
        "worst": worst,
    }


def assert_source_file_hashes(manifest: dict[str, object]) -> None:
    proof.assert_frozen_inputs()
    require("Assets" not in proof.SOURCE_DIR.parts, "AnimationProofV3 is under Assets")
    require(not list((PROJECT_ROOT / "Assets").rglob("*SessionHumanoidAnimationProofV3*")), "V3 was integrated before approval")
    for relative, expected in manifest["sourceFiles"].items():
        path = PROJECT_ROOT / relative
        require(path.is_file() and sha256(path) == expected, f"Missing/stale V3 source: {path}")
    for name, expected in manifest["outputHashes"].items():
        if name == proof.SOURCE_BLEND.name:
            path = proof.SOURCE_BLEND
        elif name.startswith("outputs/"):
            path = PROJECT_ROOT.parent / name
        else:
            path = proof.OUTPUT_DIR / name
        require(path.is_file() and sha256(path) == expected, f"Missing/stale V3 output: {path}")


def verify_manifest(manifest: dict[str, object], plan: dict[str, object]) -> None:
    require(manifest["schemaVersion"] == 1 and manifest["assetId"] == proof.ASSET_ID, "Wrong V3 manifest identity")
    require(manifest["status"] == "PASS_TECHNICAL_KINEMATIC_PROOF_BODY_V3_ACCEPTED_MANUAL_GRIP_PENDING", "V3 status changed")
    require(manifest["integrationStatus"] == "SOURCE_DCC_NOT_IN_UNITY", "V3 integration boundary changed")
    require(manifest["bodyDeformationStatus"] == "PASS_SEPARATE_SESSION_HUMANOID_BASE_V3_24_POSE_PROOF", "V3 body gate changed")
    require(manifest["manualWeaponGripVisualGate"] == "NOT_RUN", "V3 falsely accepts bow grip art")
    require(manifest["sourceRig"]["fbxSha256"] == proof.EXPECTED_RIG_FBX_SHA256, "Wrong RigV2 FBX pin")
    require(manifest["sourceRig"]["manifestSha256"] == proof.EXPECTED_RIG_MANIFEST_SHA256, "Wrong RigV2 manifest pin")
    require(manifest["v2Baseline"]["fbxSha256"] == proof.EXPECTED_V2_FBX_SHA256, "Wrong AnimationV2 FBX pin")
    require(manifest["v2Baseline"]["manifestSha256"] == proof.EXPECTED_V2_MANIFEST_SHA256, "Wrong AnimationV2 manifest pin")
    require(not manifest["v2Baseline"]["filesWrittenByV3Build"], "V3 claims to rewrite V2")
    require(
        manifest["v2Baseline"]["unaffectedBoneSemanticComparisonRequired"] == list(proof.UNCHANGED_GAME_BONES),
        "V2 unaffected-bone comparison scope changed",
    )
    require(
        manifest["v2Baseline"]["unaffectedClipSemanticComparisonRequired"] == list(proof.UNCHANGED_V2_CLIPS),
        "V2 unaffected-clip comparison scope changed",
    )
    require(
        manifest["v2Baseline"]["hunterClipsIntentionallyReauthored"] == list(proof.REAUTHORED_HUNTER_CLIPS),
        "Hunter reauthoring scope changed",
    )
    require(
        set(proof.UNCHANGED_V2_CLIPS).isdisjoint(proof.REAUTHORED_HUNTER_CLIPS)
        and set(proof.UNCHANGED_V2_CLIPS) | set(proof.REAUTHORED_HUNTER_CLIPS) == set(proof.CLIP_BY_NAME),
        "V2 baseline and Hunter reauthoring clip scopes do not partition all clips",
    )
    require(
        manifest["v2Baseline"]["wristSafetyProjectionMayChangeHandSubtreesInAllClips"],
        "V2 baseline falsely requires hand-subtree equality after the disclosed wrist projection",
    )
    require(manifest["authoring"]["controlPlanSha256"] == sha256(proof.PLAN_PATH), "Control plan pin changed")
    require(manifest["authoring"]["explicitBlender52ActionSlotBinding"], "Explicit Action-slot binding is disabled")
    require(manifest["authoring"]["sourceActionSlotIdentifier"] == "OB<exact target object name>", "Wrong source Action slot contract")
    require(manifest["authoring"]["importedFbxActionSlotIdentifier"] == "OBSlot", "Wrong imported Action slot identifier")
    require(manifest["authoring"]["actionSlotTargetIdType"] == "OBJECT", "Wrong Action slot target")
    body = manifest["acceptedBodyV3"]
    require(body["blendSha256"] == proof.EXPECTED_BODY_V3_BLEND_SHA256, "Wrong accepted BodyV3 blend pin")
    require(body["manifestSha256"] == proof.EXPECTED_BODY_V3_MANIFEST_SHA256, "Wrong accepted BodyV3 manifest pin")
    require(body["deformationProofSha256"] == proof.EXPECTED_BODY_V3_PROOF_SHA256, "Wrong accepted BodyV3 deformation pin")
    require(body["manualReviewSha256"] == proof.EXPECTED_BODY_V3_MANUAL_REVIEW_SHA256, "Wrong accepted BodyV3 manual-review pin")
    require([clip["name"] for clip in manifest["clips"]] == [clip.name for clip in proof.CLIPS], "Clip order changed")
    expected_bones = set(rig_contract.SPEC_BY_NAME)
    for expected, recorded in zip(proof.CLIPS, manifest["clips"]):
        require((recorded["frameStart"], recorded["frameEnd"]) == (expected.frame_start, expected.frame_end), f"{expected.name} range changed")
        require(recorded["bakedAction"]["keyedBoneCount"] == 68, f"{expected.name} is not a 68-joint bake")
        require(set(recorded["bakedAction"]["keyedBones"]) == expected_bones, f"{expected.name} keyed bone set changed")
    gate = manifest["kinematicGate"]
    require(gate["status"] == "PASS_ALL_248_FRAMES_EXPLICIT_ACTION_SLOT_KINEMATICS", "All-frame gate did not pass")
    require(gate["sampledFrames"] == 248, "V3 did not scan 248 frames")
    require(max(gate["maximumHandSwingDegrees"].values()) <= plan["orientationLimitsDegrees"]["maximumHandSwing"] + 1e-4, "Manifest hand swing exceeds limit")
    require(max(gate["maximumAbsoluteHandTwistDegrees"].values()) <= plan["orientationLimitsDegrees"]["maximumAbsoluteHandTwist"] + 1e-4, "Manifest hand twist exceeds limit")
    require(gate["rightDrawHandStayedOnNegativeXSide"], "Draw hand crossed center")
    require(
        gate["maximumHunterBowHandRotationStepDegrees"]
        <= plan["bowContract"]["maximumHandRotationStepDegrees"] + 1e-4,
        "Hunter bow hand rotation has a one-frame pop",
    )
    bow = manifest["technicalMetrics"]["hunterBowSequence"]
    require(bow["drawHandTravelMeters"] >= plan["bowContract"]["minimumDrawHandTravelMeters"], "Draw travel gate weakened")
    require(bow["looseDrawHandTravelMeters"] >= plan["bowContract"]["minimumReleaseHandTravelMeters"], "Release travel gate weakened")
    projection = manifest["technicalMetrics"]["wristSafetyProjection"]
    require(not projection["positionChanges"], "Wrist projection changed hand travel")
    require(projection["fingerAndSocketLocalBasesPreserved"], "Wrist projection broke the hand subtree")
    require(projection["correctedHandFrames"] > 0, "Wrist safety projection recorded no work")
    require(
        projection["maximumSwingDegreesAfterMargin"]
        == plan["orientationLimitsDegrees"]["maximumHandSwing"] - 0.25,
        "Wrist swing safety margin changed",
    )
    require(
        projection["maximumAbsoluteTwistDegreesAfterMargin"]
        == plan["orientationLimitsDegrees"]["maximumAbsoluteHandTwist"] - 0.25,
        "Wrist twist safety margin changed",
    )
    require(set(projection["perClip"]) == set(proof.CLIP_BY_NAME), "Wrist projection audit clip set changed")
    temporal = manifest["technicalMetrics"]["hunterTemporalHandFilter"]
    require(not temporal["positionChanges"], "Hunter temporal filter changed hand travel")
    require(temporal["fingerAndSocketLocalBasesPreserved"], "Hunter temporal filter broke the hand subtree")
    require(temporal["correctedHandFrames"] > 0, "Hunter temporal filter recorded no work")
    require(
        temporal["maximumStepDegreesAfterMargin"]
        == plan["bowContract"]["maximumHandRotationStepDegrees"] - 0.25,
        "Hunter temporal safety margin changed",
    )
    require(
        temporal["maximumPreFilterStepDegrees"] > temporal["maximumStepDegreesAfterMargin"],
        "Hunter temporal audit no longer proves a corrected discontinuity",
    )
    require(
        set(temporal["perClip"])
        == {"HunterBowReady", "HunterBowDraw", "HunterBowHold", "HunterBowLoose", "HunterBowRecovery"},
        "Hunter temporal audit clip set changed",
    )
    require(not manifest["approvalBoundary"]["unityIntegrationPerformed"], "V3 claims Unity integration")
    require(not manifest["approvalBoundary"]["bodyOutputPromoted"], "V3 claims body promotion")


def verify_action_set(
    rig: bpy.types.Object,
    actions: dict[str, bpy.types.Action],
    *,
    imported_fbx: bool,
) -> dict[str, object]:
    expected_bones = set(rig_contract.SPEC_BY_NAME)
    curve_reports = {}
    slots = {}
    for clip in proof.CLIPS:
        action = actions[clip.name]
        require(action_keyed_bones(action) == expected_bones, f"{clip.name} does not key all 68 bones")
        require(tuple(round(value) for value in action.frame_range) == (clip.frame_start, clip.frame_end), f"{clip.name} range changed")
        slots[clip.name] = proof.bind_action_slot(
            rig,
            action,
            clip.name,
            imported_fbx=imported_fbx,
        )
        curve_reports[clip.name] = v2_verify.verify_baked_action_curve_topology(action, clip, imported_fbx=imported_fbx)
    rig.animation_data.action = None
    return {"curveTopology": curve_reports, "actionSlots": slots}


def collect_metrics(
    rig: bpy.types.Object,
    actions: dict[str, bpy.types.Action],
    plan: dict[str, object],
) -> dict[str, object]:
    standard, _ = proof.v2.base.collect_action_metrics(rig, actions)
    proof.v2.base.validate_metrics(standard)
    standard["bendSupport"] = proof.collect_support_metrics_explicit(rig, actions)
    standard["kinematics"] = proof.collect_kinematic_metrics(rig, actions, plan)
    return standard


def compare_output_metrics(
    manifest: dict[str, object],
    measured: dict[str, object],
    *,
    path: str,
    tolerance: float,
    imported_fbx: bool,
) -> None:
    """Compare pose-derived output state, excluding immutable build-process audits."""
    recorded = json.loads(json.dumps({
        key: value
        for key, value in manifest["technicalMetrics"].items()
        if key not in {"wristSafetyProjection", "hunterTemporalHandFilter"}
    }))
    require(set(recorded) == set(measured), f"{path} output-metric key set changed")
    expected_binding = {
        "identifier": "OBSlot" if imported_fbx else "OB" + proof.GAME_RIG_NAME,
        "targetIdType": "OBJECT",
        "sourceOrImportedContract": "imported_fbx_OBSlot" if imported_fbx else "source_object_specific_slot",
    }
    for clip_name in proof.CLIP_BY_NAME:
        actual_binding = measured["kinematics"]["evaluatedMotionByClip"][clip_name]["binding"]
        require(actual_binding == expected_binding, f"{path}.{clip_name} Action-slot binding changed: {actual_binding}")
        # Slot identifiers are intentionally rewritten by the FBX importer.  The
        # exact imported/source contracts are asserted above; normalize only this
        # disclosed transport difference before comparing all pose-derived data.
        recorded["kinematics"]["evaluatedMotionByClip"][clip_name]["binding"] = actual_binding
    actual_worst = measured["kinematics"]["worstOrientationFrames"]
    for side in ("Left", "Right"):
        for axis, maximum_key in (
            ("swing", "maximumHandSwingDegrees"),
            ("twist", "maximumAbsoluteHandTwistDegrees"),
        ):
            row = actual_worst[side][axis]
            require(row["clip"] in proof.CLIP_BY_NAME, f"{path} worst {side} {axis} names an unknown clip")
            require(
                proof.CLIP_BY_NAME[row["clip"]].frame_start <= row["frame"] <= proof.CLIP_BY_NAME[row["clip"]].frame_end,
                f"{path} worst {side} {axis} frame is outside its clip",
            )
            require(
                abs(abs(float(row["degrees"])) - float(measured["kinematics"][maximum_key][side])) <= tolerance,
                f"{path} worst {side} {axis} does not match its measured maximum",
            )
    # FBX float quantization can change which member of an equal-limit tie wins
    # the argmax.  Validate the witness above and compare the maxima themselves,
    # not the non-semantic identity of the tied witness.
    recorded["kinematics"]["worstOrientationFrames"] = actual_worst
    v2_verify.compare_metrics(recorded, measured, path=path, tolerance=tolerance)


def verify_source_blend(
    manifest: dict[str, object],
    plan: dict[str, object],
) -> tuple[dict[str, object], dict[str, object], dict[str, object]]:
    bpy.ops.wm.open_mainfile(filepath=str(proof.SOURCE_BLEND))
    require(not bpy.data.libraries, "V3 source contains linked libraries")
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in objects if obj.type == "MESH"]
    require({obj.name for obj in armatures} == {rig_contract.RIG_OBJECT_NAME, rig_contract.RIGIFY_META_NAME, rig_contract.RIGIFY_CONTROL_NAME}, "V3 source armature set changed")
    require(not any(obj.type in {"CAMERA", "LIGHT"} for obj in objects), "V3 source contains a camera or light")
    by_object = {obj.name: obj for obj in objects}
    require(rig_contract.BIND_PROBE_NAME in by_object, "V3 source lost the RigV2 bind probe")
    bind_probe = by_object[rig_contract.BIND_PROBE_NAME]
    require(bind_probe.type == "MESH", "V3 RigV2 bind probe is not a mesh")
    widgets = [obj for obj in meshes if obj != bind_probe]
    require(widgets and all(obj.name.startswith("WGT-") for obj in widgets), "V3 source mesh set is not limited to RigV2 authoring widgets and bind probe")
    widget_collection = bpy.data.collections.get(rig_contract.RIGIFY_WIDGET_COLLECTION_NAME)
    require(widget_collection is not None, "V3 source lost the RigV2 widget collection")
    require(
        {obj.name for obj in widget_collection.all_objects} == {obj.name for obj in widgets},
        "V3 RigV2 widget collection membership changed",
    )
    require(bind_probe.hide_render and bind_probe.get("exported") is False, "V3 bind probe crossed the render/export boundary")
    require(
        all(obj.hide_render and obj.get("exported") is False for obj in widgets),
        "V3 Rigify widget crossed the render/export boundary",
    )
    game_rig = bpy.data.objects[proof.GAME_RIG_NAME]
    control_rig = bpy.data.objects[proof.CONTROL_RIG_NAME]
    require({bone.name for bone in game_rig.data.bones} == set(rig_contract.SPEC_BY_NAME), "V3 source bone set changed")
    require(game_rig.animation_data is None or game_rig.animation_data.action is None, "V3 game rig saved with active action")
    require(control_rig.animation_data is None or control_rig.animation_data.action is None, "V3 control rig saved with active action")
    expected_action_names = set(proof.CLIP_BY_NAME) | {proof.CONTROL_ACTION_PREFIX + name for name in proof.CLIP_BY_NAME}
    require(set(action.name for action in bpy.data.actions) == expected_action_names, "V3 source action set changed")
    actions = {name: bpy.data.actions[name] for name in proof.CLIP_BY_NAME}
    for action in actions.values():
        require(action.get("sessionRole") == "clean_68_joint_game_bake_v3_wrist_safe_kinematic_proof", f"{action.name} role changed")
    action_report = verify_action_set(game_rig, actions, imported_fbx=False)
    metrics = collect_metrics(game_rig, actions, plan)
    compare_output_metrics(manifest, metrics, path="sourceMetrics", tolerance=0.003, imported_fbx=False)
    rest = v2_verify.inspect_support_rest_and_props(game_rig, imported=False)
    snapshot = semantic_snapshot(
        game_rig,
        actions,
        tuple(rig_contract.SPEC_BY_NAME),
        imported_fbx=False,
    )
    report = {
        "objects": len(objects),
        "armatures": len(armatures),
        "meshes": len(meshes),
        "renderableMeshes": 0,
        "nonExportedBindProbeMeshes": 1,
        "nonExportedRigifyWidgetMeshes": len(widgets),
        "actions": 20,
        "rigifyControlActions": 10,
        "cleanGameActions": 10,
        "gameJointCount": 68,
        "supportRestContract": rest,
        **action_report,
    }
    return report, metrics, snapshot


def import_fbx() -> tuple[bpy.types.Object, dict[str, bpy.types.Action]]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(
        filepath=str(proof.FBX_PATH),
        use_anim=True,
        automatic_bone_orientation=False,
        ignore_leaf_bones=False,
    )
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    require(len(objects) == 1 and len(armatures) == 1, "V3 FBX is not skeleton-only")
    rig = armatures[0]
    require(rig.name == proof.GAME_RIG_NAME, f"V3 FBX rig name changed: {rig.name}")
    require(rig.parent is None and near_zero(rig.location) and near_zero(rig.rotation_euler) and near_one(rig.scale), "V3 FBX rig object is not identity")
    require(not rig.constraints and not any(bone.constraints for bone in rig.pose.bones), "V3 FBX contains constraints")
    return rig, actions_by_clip()


def verify_fbx(
    manifest: dict[str, object],
    plan: dict[str, object],
) -> tuple[dict[str, object], dict[str, object], dict[str, object]]:
    rig, actions = import_fbx()
    by_name = {bone.name: bone for bone in rig.data.bones}
    require(len(by_name) == 68 and set(by_name) == set(rig_contract.SPEC_BY_NAME), "V3 FBX bone set changed")
    maximum_joint_error = 0.0
    for spec in rig_contract.BONE_SPECS:
        bone = by_name[spec.name]
        require((bone.parent.name if bone.parent else None) == spec.parent, f"V3 FBX parent changed for {spec.name}")
        error = (bone.head_local - Vector(spec.head)).length
        maximum_joint_error = max(maximum_joint_error, error)
        require(error <= JOINT_TOLERANCE_METERS, f"V3 FBX joint changed for {spec.name}: {error}")
    action_report = verify_action_set(rig, actions, imported_fbx=True)
    metrics = collect_metrics(rig, actions, plan)
    compare_output_metrics(manifest, metrics, path="fbxMetrics", tolerance=0.010, imported_fbx=True)
    rest = v2_verify.inspect_support_rest_and_props(rig, imported=True)
    snapshot = semantic_snapshot(
        rig,
        actions,
        tuple(rig_contract.SPEC_BY_NAME),
        imported_fbx=True,
    )
    report = {
        "objects": 1,
        "armatures": 1,
        "meshes": 0,
        "actions": 10,
        "constraints": 0,
        "jointCount": 68,
        "maximumJointHeadDeviationMeters": maximum_joint_error,
        "identityRootObjectTransform": True,
        "supportRestContract": rest,
        **action_report,
    }
    return report, metrics, snapshot


def frozen_v2_unaffected_snapshot() -> dict[str, dict[int, dict[str, object]]]:
    bpy.ops.wm.open_mainfile(filepath=str(proof.V2_SOURCE_BLEND))
    rig = bpy.data.objects[proof.GAME_RIG_NAME]
    actions = {name: bpy.data.actions[name] for name in proof.CLIP_BY_NAME}
    return semantic_snapshot(
        rig,
        actions,
        proof.UNCHANGED_GAME_BONES,
        imported_fbx=False,
    )


def main() -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    plan, _clips = configure()
    require(proof.MANIFEST_PATH.is_file(), "AnimationProofV3 manifest is missing")
    manifest = json.loads(proof.MANIFEST_PATH.read_text(encoding="utf-8"))
    assert_source_file_hashes(manifest)
    verify_manifest(manifest, plan)
    source_report, source_metrics, source_snapshot = verify_source_blend(manifest, plan)
    fbx_report, fbx_metrics, fbx_snapshot = verify_fbx(manifest, plan)
    source_fbx = compare_snapshots(
        source_snapshot,
        fbx_snapshot,
        SOURCE_FBX_SEMANTIC_TOLERANCE,
        "sourceBlendVsFbx",
    )
    source_unaffected = {
        clip: {
            frame: {bone: values for bone, values in bones.items() if bone in proof.UNCHANGED_GAME_BONES}
            for frame, bones in frames.items()
        }
        for clip, frames in source_snapshot.items()
        if clip in proof.UNCHANGED_V2_CLIPS
    }
    v2_unaffected = {
        clip: frames
        for clip, frames in frozen_v2_unaffected_snapshot().items()
        if clip in proof.UNCHANGED_V2_CLIPS
    }
    unaffected = compare_snapshots(
        v2_unaffected,
        source_unaffected,
        UNCHANGED_V2_SEMANTIC_TOLERANCE,
        "frozenV2UnaffectedBonesInNonHunterClips",
    )
    report = {
        "status": "PASS_TECHNICAL_KINEMATIC_PROOF_ROUND_TRIP_BODY_V3_ACCEPTED_NOT_IN_UNITY",
        "assetId": proof.ASSET_ID,
        "sourceBlendSha256": manifest["outputHashes"][proof.SOURCE_BLEND.name],
        "fbxSha256": manifest["outputHashes"][proof.FBX_PATH.name],
        "manifestSha256": sha256(proof.MANIFEST_PATH),
        "controlPlanSha256": sha256(proof.PLAN_PATH),
        "sourceBlend": source_report,
        "sourceMetrics": source_metrics,
        "fbxRoundTrip": fbx_report,
        "fbxMetrics": fbx_metrics,
        "sourceBlendVsFbxEvery68BoneEvery248Frames": source_fbx,
        "frozenV2UnaffectedBoneSemanticsInNonHunterClips": unaffected,
        "hunterClipsIntentionallyReauthored": list(proof.REAUTHORED_HUNTER_CLIPS),
        "bodyDeformationArchitectureAccepted": True,
        "acceptedBodyV3": manifest["acceptedBodyV3"],
        "bodyOutputPromoted": False,
        "manualWeaponGripVisualGate": "NOT_RUN",
        "unityIntegrationPerformed": False,
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
