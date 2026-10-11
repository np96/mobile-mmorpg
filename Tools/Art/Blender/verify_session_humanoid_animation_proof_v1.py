#!/usr/bin/env python3
"""Verify SessionHumanoidAnimationProofV1 source and FBX round trip."""

from __future__ import annotations

import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_session_humanoid_animation_proof_v1 as proof
import build_session_humanoid_rig_v1 as rig_contract


PROJECT_ROOT = Path(__file__).resolve().parents[3]
REPORT_PATH = (
    PROJECT_ROOT.parent
    / "outputs/art/blender/session-humanoid-animation-proof-v1/SessionHumanoidAnimationProofV1RoundTrip.json"
)
JOINT_TOLERANCE_METERS = 0.00002
METRIC_TOLERANCE = 0.003


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
    for curve in proof.iter_action_fcurves(action):
        path = curve.data_path
        if 'pose.bones["' in path:
            result.add(path.split('pose.bones["', 1)[1].split('"]', 1)[0])
    return result


def all_action_points_linear(action: bpy.types.Action) -> bool:
    return all(
        point.interpolation == "LINEAR"
        for curve in proof.iter_action_fcurves(action)
        for point in curve.keyframe_points
    )


def normalized_action_name(name: str) -> str:
    for expected in proof.CLIP_BY_NAME:
        if name == expected or name.endswith("|" + expected) or name.endswith("_" + expected):
            return expected
    return name


def compare_metrics(expected, actual, path="technicalMetrics", tolerance=METRIC_TOLERANCE) -> None:
    if isinstance(expected, dict):
        require(isinstance(actual, dict), f"{path} type changed")
        require(set(expected) == set(actual), f"{path} keys changed")
        for key in expected:
            compare_metrics(expected[key], actual[key], f"{path}.{key}", tolerance)
    elif isinstance(expected, list):
        require(isinstance(actual, list) and len(expected) == len(actual), f"{path} list changed")
        for index, (left, right) in enumerate(zip(expected, actual)):
            compare_metrics(left, right, f"{path}[{index}]", tolerance)
    elif isinstance(expected, (int, float)) and not isinstance(expected, bool):
        if expected is None:
            require(actual is None, f"{path} changed")
        else:
            require(isinstance(actual, (int, float)), f"{path} is no longer numeric")
            require(abs(float(expected) - float(actual)) <= tolerance, f"{path} changed: {expected} vs {actual}")
    else:
        require(expected == actual, f"{path} changed: {expected!r} vs {actual!r}")


def verify_files(manifest: dict[str, object]) -> None:
    require("Assets" not in proof.SOURCE_DIR.parts, "Animation proof source is inside Unity Assets")
    stray = list((PROJECT_ROOT / "Assets").rglob("*SessionHumanoidAnimationProofV1*"))
    require(not stray, f"Animation proof was integrated before visual acceptance: {stray}")
    require(manifest["integrationStatus"] == "SOURCE_DCC_NOT_IN_UNITY", "Integration boundary changed")
    require(manifest["status"] == "PASS_TECHNICAL_ANIMATION_PROOF_NOT_VISUAL_ACCEPTANCE", "Technical-only status changed")
    require(
        manifest["currentRestPoseStatus"] == "REFIT_TO_ACCEPTED_MPFB_NEUTRAL_MIDPOINT",
        "Accepted-rest boundary changed",
    )
    require(not manifest["externalOrLegacyAnimationDependency"], "Manifest declares an external animation dependency")
    require(not manifest["legacyClipsImportedRetargetedOrSampled"], "Manifest permits legacy clip sampling")
    determinism = manifest["determinismBoundary"]
    require(determinism["sparseControlDefinitionsDeterministic"] and determinism["fullFrameBakeDeterministic"], "Animation source/bake determinism changed")
    require(determinism["fbxUsesFixedTimestampAndDeterministicUuids"], "Animation FBX deterministic guards changed")
    require(determinism["fbxExpectedByteDeterministicForIdenticalInputs"], "Animation FBX deterministic contract changed")
    require(not determinism["blendBytesDeterministic"] and not determinism["manifestBytesDeterministic"], "Manifest falsely claims deterministic Blender serialization")
    for relative, expected_hash in manifest["sourceFiles"].items():
        path = PROJECT_ROOT / relative
        require(path.is_file(), f"Missing source file: {path}")
        require(sha256(path) == expected_hash, f"Stale source hash: {path}")
    for name, expected_hash in manifest["outputHashes"].items():
        path = proof.SOURCE_DIR / name if name.endswith(".blend") else proof.OUTPUT_DIR / name
        require(path.is_file(), f"Missing output: {path}")
        require(sha256(path) == expected_hash, f"Stale output hash: {path}")


def verify_manifest(manifest: dict[str, object]) -> None:
    require(manifest["schemaVersion"] == 1 and manifest["assetId"] == proof.ASSET_ID, "Unexpected manifest identity")
    require(manifest["fps"] == 30, "Animation proof FPS changed")
    require(manifest["sourceRig"]["exportedJointCount"] == 66, "Source joint count changed")
    require(manifest["sourceRig"]["restFitRevision"] == rig_contract.REST_FIT_REVISION, "Source rest-fit revision changed")
    require(
        manifest["sourceRig"]["restFitInputArtifactSha256"] == rig_contract.EXPECTED_REST_FIT_ARTIFACT_SHA256,
        "Source rest-fit artifact changed",
    )
    require(
        manifest["sourceRig"]["restFitNeutralProposalCanonicalSha256"] == rig_contract.EXPECTED_REST_FIT_PROPOSAL_SHA256,
        "Source rest-fit proposal changed",
    )
    require(manifest["authoring"]["rigifyControlActions"] == 10, "Expected ten Rigify source actions")
    require(manifest["authoring"]["cleanGameBakeActions"] == 10, "Expected ten game-baked actions")
    require(manifest["authoring"]["sparseControlActionsRetainedInBlend"], "Sparse control source was not retained")
    require(manifest["authoring"]["allIntegerFramesBakedToGameRig"], "Full-frame bake contract was lost")
    require("world-space delta" in manifest["authoring"]["retargetMethod"], "Rest-matrix retarget contract changed")
    require(manifest["authoring"]["hipsTransformSource"].startswith("Rigify animator control hips"), "Hips source changed")
    require(manifest["authoring"]["identicalEndpointStabilization"].startswith("copies only"), "Endpoint stabilization contract changed")
    require(manifest["authoring"]["rebakedAfterRestRigRefit"], "Rest-refit rebake proof was lost")
    require(manifest["authoring"]["rebakeAfterAnyFutureBoundedRestRigRefit"], "Future bounded refit contract was lost")
    require([clip["name"] for clip in manifest["clips"]] == [clip.name for clip in proof.CLIPS], "Clip order/set changed")
    for expected, recorded in zip(proof.CLIPS, manifest["clips"]):
        require(recorded["frameStart"] == expected.frame_start and recorded["frameEnd"] == expected.frame_end, f"{expected.name} range changed")
        require(recorded["loop"] == expected.loop and recorded["category"] == expected.category, f"{expected.name} metadata changed")
        require(recorded["sourceAction"]["keyedBoneCount"] <= len(proof.ANIMATED_CONTROLS) + len(proof.IK_SWITCH_CONTROLS), f"{expected.name} source action keys non-control bones")
        require(recorded["bakedAction"]["keyedBoneCount"] == 66, f"{expected.name} is not a full 66-joint bake")
        require(recorded["sourceAction"]["allLinear"] and recorded["bakedAction"]["allLinear"], f"{expected.name} interpolation changed")
    export = manifest["exportContract"]
    require(export == {
        "canonicalFbxAnimationElementOrdering": True,
        "canonicalFbxConnectionOrdering": True,
        "deterministicFbxUuidHash": "sha256(repr(exporter-key))[:8] masked to signed int64",
        "fbxActions": 10,
        "fbxArmatureObjects": 1,
        "fbxConstraints": 0,
        "fbxControlMechanismWidgetBones": 0,
        "fbxJointCount": 66,
        "fbxMeshObjects": 0,
        "fixedCreationTimestamp": proof.FBX_CREATION_TIMESTAMP.isoformat(),
        "selectedObjectNames": [proof.GAME_RIG_NAME],
    }, f"Export contract changed: {export}")
    boundary = manifest["approvalBoundary"]
    require(not boundary["underUnityAssets"] and not boundary["unityIntegrationPerformed"], "Unity boundary changed")
    require(not boundary["mpfbBodyBound"] and not boundary["visualSilhouetteScanPerformed"], "Proof falsely claims body/visual review")


def verify_source_blend(manifest: dict[str, object]) -> tuple[dict[str, object], dict[str, object]]:
    bpy.ops.wm.open_mainfile(filepath=str(proof.SOURCE_BLEND))
    require(not bpy.data.libraries, "Animation source contains linked/appended libraries")
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    require({obj.name for obj in armatures} == {
        rig_contract.RIG_OBJECT_NAME,
        rig_contract.RIGIFY_META_NAME,
        rig_contract.RIGIFY_CONTROL_NAME,
    }, "Animation source lost its game/metarig/control armatures")
    require(not any(obj.type in {"CAMERA", "LIGHT"} for obj in objects), "Animation source contains camera/light")
    game_rig = bpy.data.objects[proof.GAME_RIG_NAME]
    control_rig = bpy.data.objects[proof.CONTROL_RIG_NAME]
    require(set(bone.name for bone in game_rig.data.bones) == set(rig_contract.SPEC_BY_NAME), "Source game bone set changed")
    require(sum(len(bone.constraints) for bone in game_rig.pose.bones) == 58, "Source game DCC driver constraints changed")
    require(game_rig.animation_data is None or game_rig.animation_data.action is None, "Source game rig saved with an active action")
    require(control_rig.animation_data is None or control_rig.animation_data.action is None, "Source control rig saved with an active action")

    expected_action_names = set(proof.CLIP_BY_NAME) | {proof.CONTROL_ACTION_PREFIX + name for name in proof.CLIP_BY_NAME}
    actions = {action.name: action for action in bpy.data.actions}
    require(set(actions) == expected_action_names, f"Source action set changed: {set(actions) ^ expected_action_names}")
    control_actions = {name: actions[proof.CONTROL_ACTION_PREFIX + name] for name in proof.CLIP_BY_NAME}
    baked_actions = {name: actions[name] for name in proof.CLIP_BY_NAME}
    forbidden_control_tokens = ("ORG-", "MCH-", "DEF-", "WGT-")
    source_reports = {}
    baked_reports = {}
    for clip in proof.CLIPS:
        source = control_actions[clip.name]
        baked = baked_actions[clip.name]
        require(source.get("sessionRole") == "rigify_control_source_not_exported", f"{clip.name} source role changed")
        require(baked.get("sessionRole") == "clean_66_joint_game_bake", f"{clip.name} bake role changed")
        source_bones = action_keyed_bones(source)
        baked_bones = action_keyed_bones(baked)
        require(source_bones <= set(proof.ANIMATED_CONTROLS) | set(proof.IK_SWITCH_CONTROLS), f"{clip.name} source keys a non-animator bone")
        require(not any(name.startswith(forbidden_control_tokens) for name in source_bones), f"{clip.name} source keys a Rigify helper")
        require(baked_bones == set(rig_contract.SPEC_BY_NAME), f"{clip.name} bake is not all 66 game joints")
        require(all_action_points_linear(source) and all_action_points_linear(baked), f"{clip.name} interpolation is not linear")
        require(tuple(round(value) for value in source.frame_range) == (clip.frame_start, clip.frame_end), f"{clip.name} source range changed")
        require(tuple(round(value) for value in baked.frame_range) == (clip.frame_start, clip.frame_end), f"{clip.name} bake range changed")
        source_reports[clip.name] = proof.action_curve_report(source)
        baked_reports[clip.name] = proof.action_curve_report(baked)

    source_metrics, _source_samples = proof.collect_action_metrics(game_rig, baked_actions)
    proof.validate_metrics(source_metrics)
    compare_metrics(manifest["technicalMetrics"], source_metrics)

    # Independently prove a sparse Rigify action evaluates into game motion.
    draw_clip = proof.CLIP_BY_NAME["HunterBowDraw"]
    control_rig.animation_data_create()
    control_rig.animation_data.action = control_actions["HunterBowDraw"]
    bpy.context.scene.frame_set(draw_clip.frame_start)
    bpy.context.view_layer.update()
    draw_start = proof.sample_clean_game_pose(game_rig, control_rig)["RightHand"].translation.copy()
    bpy.context.scene.frame_set(draw_clip.frame_end)
    bpy.context.view_layer.update()
    draw_end = proof.sample_clean_game_pose(game_rig, control_rig)["RightHand"].translation.copy()
    control_source_travel = (draw_end - draw_start).length
    require(control_source_travel >= 0.30, "Sparse Rigify bow draw does not drive game-hand travel")
    control_rig.animation_data.action = None

    return {
        "objects": len(objects),
        "armatures": len(armatures),
        "actions": len(actions),
        "rigifyControlActions": len(control_actions),
        "cleanGameBakedActions": len(baked_actions),
        "gameJointCount": len(game_rig.data.bones),
        "gameDccDriverConstraints": 58,
        "sparseControlDrawHandTravelMeters": control_source_travel,
        "sourceActionReports": source_reports,
        "bakedActionReports": baked_reports,
    }, source_metrics


def verify_fbx(manifest: dict[str, object]) -> tuple[dict[str, object], dict[str, object]]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(
        filepath=str(proof.FBX_PATH),
        use_anim=True,
        automatic_bone_orientation=False,
        ignore_leaf_bones=False,
    )
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    require(len(objects) == 1 and len(armatures) == 1, f"Animation FBX is not skeleton-only: {[(obj.name, obj.type) for obj in objects]}")
    rig = armatures[0]
    require(rig.name == proof.GAME_RIG_NAME, f"Imported game rig name changed: {rig.name}")
    require(rig.parent is None and near_zero(rig.location) and near_zero(rig.rotation_euler) and near_one(rig.scale), "Imported rig root transform is not identity")
    require(not rig.constraints and not any(bone.constraints for bone in rig.pose.bones), "Animation FBX contains constraints")
    by_name = {bone.name: bone for bone in rig.data.bones}
    require(set(by_name) == set(rig_contract.SPEC_BY_NAME), f"Imported animation bone set changed: {set(by_name) ^ set(rig_contract.SPEC_BY_NAME)}")
    maximum_joint_deviation = 0.0
    for spec in rig_contract.BONE_SPECS:
        bone = by_name[spec.name]
        require((bone.parent.name if bone.parent else None) == spec.parent, f"Imported animation parent changed for {spec.name}")
        deviation = (bone.head_local - Vector(spec.head)).length
        maximum_joint_deviation = max(maximum_joint_deviation, deviation)
        require(deviation <= JOINT_TOLERANCE_METERS, f"Imported animation joint changed for {spec.name}: {deviation}")
    forbidden = rig_contract.FORBIDDEN_BONE_TOKENS + ("RIGIFY", "METARIG", "WGT-")
    require(not any(any(token in name.upper() for token in forbidden) for name in by_name), "FBX contains a control/mechanism/widget bone")

    actions_by_clip: dict[str, bpy.types.Action] = {}
    unexpected = []
    for action in bpy.data.actions:
        normalized = normalized_action_name(action.name)
        if normalized in proof.CLIP_BY_NAME and normalized not in actions_by_clip:
            actions_by_clip[normalized] = action
        else:
            unexpected.append(action.name)
    require(not unexpected, f"FBX contains unexpected/duplicate actions: {unexpected}")
    require(set(actions_by_clip) == set(proof.CLIP_BY_NAME), f"FBX action set changed: {set(actions_by_clip) ^ set(proof.CLIP_BY_NAME)}")
    for clip in proof.CLIPS:
        action = actions_by_clip[clip.name]
        keyed = action_keyed_bones(action)
        require(keyed <= set(rig_contract.SPEC_BY_NAME), f"Imported {clip.name} keys a non-game bone")
        require(not any(name.startswith(proof.CONTROL_ACTION_PREFIX) for name in keyed), f"Imported {clip.name} contains control channels")
        require(tuple(round(value) for value in action.frame_range) == (clip.frame_start, clip.frame_end), f"Imported {clip.name} range changed: {tuple(action.frame_range)}")

    roundtrip_metrics, _roundtrip_samples = proof.collect_action_metrics(rig, actions_by_clip)
    proof.validate_metrics(roundtrip_metrics)
    compare_metrics(manifest["technicalMetrics"], roundtrip_metrics, path="fbxTechnicalMetrics", tolerance=0.010)
    return {
        "objects": len(objects),
        "armatures": len(armatures),
        "meshes": 0,
        "actions": len(bpy.data.actions),
        "constraints": 0,
        "jointCount": len(by_name),
        "maximumJointHeadDeviationMeters": maximum_joint_deviation,
        "identityRootObjectTransform": True,
        "controlsMechanismsWidgets": 0,
        "actionNames": sorted(actions_by_clip),
        "actionReports": {name: proof.action_curve_report(action) for name, action in actions_by_clip.items()},
    }, roundtrip_metrics


def main() -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    require(proof.MANIFEST_PATH.is_file(), f"Missing manifest: {proof.MANIFEST_PATH}")
    manifest = json.loads(proof.MANIFEST_PATH.read_text(encoding="utf-8"))
    verify_files(manifest)
    verify_manifest(manifest)
    source_report, source_metrics = verify_source_blend(manifest)
    fbx_report, fbx_metrics = verify_fbx(manifest)
    report = {
        "status": "PASS_TECHNICAL_ANIMATION_PROOF_ROUND_TRIP_NOT_IN_UNITY",
        "assetId": proof.ASSET_ID,
        "currentRestPoseStatus": manifest["currentRestPoseStatus"],
        "externalOrLegacyAnimationDependency": False,
        "sourceBlendSha256": manifest["outputHashes"][proof.SOURCE_BLEND.name],
        "fbxSha256": manifest["outputHashes"][proof.FBX_PATH.name],
        "manifestSha256": sha256(proof.MANIFEST_PATH),
        "sourceBlend": source_report,
        "sourceMetrics": source_metrics,
        "fbxRoundTrip": fbx_report,
        "fbxMetrics": fbx_metrics,
        "mpfbBodyBound": False,
        "visualSilhouetteScanPerformed": False,
        "unityIntegrationPerformed": False,
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
