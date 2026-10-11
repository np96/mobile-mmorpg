#!/usr/bin/env python3
"""Independent source and FBX round-trip verifier for Session Humanoid Rig V1."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_session_humanoid_rig_v1 as contract


PROJECT_ROOT = Path(__file__).resolve().parents[3]
REPORT_PATH = (
    PROJECT_ROOT.parent
    / "outputs/art/blender/session-humanoid-rig-v1/SessionHumanoidRigV1FbxRoundTrip.json"
)
HEAD_TOLERANCE_METERS = 0.00002
IDENTITY_TOLERANCE = 0.00001


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def near_zero(value, tolerance: float = IDENTITY_TOLERANCE) -> bool:
    return max(abs(component) for component in value) <= tolerance


def near_one(value, tolerance: float = IDENTITY_TOLERANCE) -> bool:
    return max(abs(component - 1.0) for component in value) <= tolerance


def expected_parent_map() -> dict[str, str | None]:
    return {spec.name: spec.parent for spec in contract.BONE_SPECS}


def verify_files(manifest: dict[str, object]) -> None:
    require("Assets" not in contract.SOURCE_DIR.parts, "Rig source is inside Unity Assets")
    stray = list((PROJECT_ROOT / "Assets").rglob("*SessionHumanoidRigV1*"))
    require(not stray, f"Rig was integrated before its source gate: {stray}")
    require(manifest["integrationStatus"] == "SOURCE_DCC_NOT_IN_UNITY", "Integration boundary changed")
    require(not manifest["externalSkeletonOrModelDependency"], "Manifest declares an external skeleton/model dependency")
    require(
        manifest["restProportionCalibrationDependency"]
        == "retained CC0 coordinates-only SessionHumanoidBaseV1 body-fit artifact",
        "Rest-proportion calibration boundary changed",
    )
    require(not manifest["legacySkeletonImportedOrSampled"], "Manifest permits a legacy skeleton")
    require(manifest["rigifyUsed"], "Source blend lost its required Rigify DCC authoring layer")
    require(not manifest["rigifyContentInFbx"], "Manifest permits Rigify content in the game FBX")
    for relative, expected_hash in manifest["sourceFiles"].items():
        path = PROJECT_ROOT / relative
        require(path.is_file(), f"Missing source file: {path}")
        require(sha256(path) == expected_hash, f"Stale source hash: {path}")
    for name, expected_hash in manifest["outputHashes"].items():
        path = contract.SOURCE_DIR / name if name.endswith(".blend") else contract.OUTPUT_DIR / name
        require(path.is_file(), f"Missing output: {path}")
        require(sha256(path) == expected_hash, f"Stale output hash: {path}")


def verify_manifest(manifest: dict[str, object]) -> None:
    require(manifest["schemaVersion"] == 1, "Unexpected rig manifest schema")
    require(manifest["assetId"] == contract.ASSET_ID, "Wrong rig asset id")
    require(manifest["toolchain"]["blenderVersion"] == bpy.app.version_string, "Rig was built with a different Blender version")
    require(manifest["toolchain"]["rigifySource"] == "bundled addons_core/rigify", "Rigify source contract changed")
    determinism = manifest["determinismBoundary"]
    require(determinism["stableLandmarkInputByteDeterministic"], "Stable landmark input is not deterministic")
    require(determinism["neutralProposalCanonicalHashPinned"], "Neutral proposal hash is not pinned")
    require(determinism["fbxUsesFixedTimestampAndDeterministicUuids"], "FBX deterministic guards changed")
    require(determinism["fbxExpectedByteDeterministicForIdenticalInputs"], "FBX deterministic contract changed")
    require(not determinism["blendBytesDeterministic"] and not determinism["manifestBytesDeterministic"], "Manifest falsely claims deterministic Blender serialization")
    coordinates = manifest["coordinates"]
    require(coordinates == {
        "unitSystem": "METRIC",
        "metersPerUnit": 1.0,
        "blenderUp": "+Z",
        "blenderForward": "-Y",
        "blenderLeft": "+X",
        "fbxAxisForward": "-Z",
        "fbxAxisUp": "+Y",
        "unityUp": "+Y",
        "unityForward": "+Z",
    }, f"Coordinate contract changed: {coordinates}")
    hierarchy = manifest["hierarchy"]
    require(hierarchy["exportedBoneCount"] == 66, "Expected 66 exported joints")
    require(hierarchy["unityHumanoidDeformBoneCount"] == 55, "Expected 55 Humanoid bones")
    require(hierarchy["socketBoneCount"] == 10, "Expected ten sockets")
    require(hierarchy["orderedBoneNames"] == [spec.name for spec in contract.BONE_SPECS], "Authored contract order changed")
    require(hierarchy["humanBoneMap"] == contract.HUMANOID_MAP, "Unity Humanoid map changed")
    require(set(hierarchy["socketBoneNames"]) == {spec.name for spec in contract.BONE_SPECS if spec.role == "socket"}, "Socket set changed")
    dimensions = manifest["dimensions"]
    require(dimensions["referenceGroundToCrownMeters"] == 1.78, "Reference height changed")
    expected_dimension_points = {
        "hipsHeadMeters": contract.SPEC_BY_NAME["Hips"].head,
        "leftShoulderRootMeters": contract.SPEC_BY_NAME["LeftShoulder"].head,
        "leftUpperArmHeadMeters": contract.SPEC_BY_NAME["LeftUpperArm"].head,
        "rightUpperArmHeadMeters": contract.SPEC_BY_NAME["RightUpperArm"].head,
        "leftElbowMeters": contract.SPEC_BY_NAME["LeftLowerArm"].head,
        "rightElbowMeters": contract.SPEC_BY_NAME["RightLowerArm"].head,
        "leftWristMeters": contract.SPEC_BY_NAME["LeftHand"].head,
        "rightWristMeters": contract.SPEC_BY_NAME["RightHand"].head,
        "leftMiddleFingertipJointMeters": contract.SPEC_BY_NAME["LeftMiddleDistal"].tail,
        "rightMiddleFingertipJointMeters": contract.SPEC_BY_NAME["RightMiddleDistal"].tail,
        "leftFootTipMeters": contract.SPEC_BY_NAME["Socket_FootFX_L"].head,
        "rightFootTipMeters": contract.SPEC_BY_NAME["Socket_FootFX_R"].head,
    }
    for key, value in expected_dimension_points.items():
        require(dimensions[key] == contract.vector_tuple(Vector(value)), f"{key} changed")
    left_tip = Vector(contract.SPEC_BY_NAME["LeftMiddleDistal"].tail)
    right_tip = Vector(contract.SPEC_BY_NAME["RightMiddleDistal"].tail)
    require(
        dimensions["middleFingertipJointSpanXMeters"] == round(left_tip.x - right_tip.x, 6),
        "Rig fingertip span changed",
    )
    require(
        dimensions["neutralBodySurfaceFingertipSpanMeters"]
        == round(contract.REST_FIT_PROPOSAL["surfaceFingertipHalfSpanMeters"] * 2.0, 6),
        "Body surface fingertip span changed",
    )
    rest_fit = manifest["restFit"]
    require(rest_fit["revision"] == contract.REST_FIT_REVISION, "Rest-fit revision changed")
    require(rest_fit["inputArtifact"] == str(contract.REST_FIT_ARTIFACT_PATH.relative_to(PROJECT_ROOT)), "Rest-fit artifact path changed")
    require(rest_fit["inputArtifactSha256"] == contract.EXPECTED_REST_FIT_ARTIFACT_SHA256, "Rest-fit artifact hash changed")
    require(rest_fit["neutralProposalCanonicalSha256"] == contract.EXPECTED_REST_FIT_PROPOSAL_SHA256, "Rest-fit proposal hash changed")
    require(
        rest_fit["perBuildProvenanceManifest"]
        == str(contract.REST_FIT_BUILD_MANIFEST_PATH.relative_to(PROJECT_ROOT)),
        "Rest-fit per-build provenance path changed",
    )
    require(
        rest_fit["perBuildProvenanceManifestSha256"] == contract.EXPECTED_REST_FIT_BUILD_MANIFEST_SHA256,
        "Rest-fit per-build provenance hash changed",
    )
    require(
        rest_fit["originatingBodyIntakeReportSha256"] == contract.EXPECTED_REST_FIT_REPORT_SHA256,
        "Rest-fit intake-report hash changed",
    )
    require(not rest_fit["checkpointBlendBitwiseDeterministic"], "Rig manifest falsely claims deterministic Blender checkpoint bytes")
    require(rest_fit["landmarkCount"] == 53, "Rest-fit landmark count changed")
    require(rest_fit["authoredGroundRootMeters"] == [0.0, 0.0, 0.0], "Authored Root changed")
    require(not rest_fit["legacyOrFoundationSourceUsed"], "Rest fit permits a retired source")
    require(not rest_fit["globalOrAxialStretchAppliedToEitherBody"], "Rest fit permits body stretching")
    require(all(not value for value in rest_fit["variantStockPoseModified"].values()), "Rest fit modifies a stock pose")
    require(all(value is None for value in rest_fit["variantLocalPoseCorrection"].values()), "Rest fit applies a local pose correction")
    require(set(rest_fit["variantLandmarkContainmentStatus"].values()) == {"PASS"}, "Rest-fit containment failed")
    export = manifest["exportContract"]
    require(export["fbxContainsArmatures"] == 1, "FBX armature count contract changed")
    require(export["fbxContainsMeshes"] == 0, "FBX is no longer skeleton-only")
    require(export["fbxContainsActions"] == 0, "FBX action contract changed")
    require(export["fixedCreationTimestamp"] == contract.FBX_CREATION_TIMESTAMP.isoformat(), "FBX deterministic timestamp contract changed")
    require(export["deterministicFbxUuidHash"] == "sha256(repr(exporter-key))[:8] masked to signed int64", "FBX deterministic UUID contract changed")
    require(export["controlsOrMechanismBones"] == 0, "Control/mechanism bones are permitted")
    require(export["selectedObjectNames"] == [contract.RIG_OBJECT_NAME], "FBX selection contract changed")
    require(export["rigifyMetarigObjects"] == 0, "FBX permits a Rigify metarig object")
    require(export["rigifyControlRigObjects"] == 0, "FBX permits a Rigify control object")
    require(export["rigifyWidgetObjects"] == 0, "FBX permits Rigify widget objects")
    dcc = manifest["dccAuthoring"]
    require(dcc["profile"] == "Blender 5.2 bundled Rigify Human metarig", "Unexpected DCC metarig profile")
    require(dcc["metarigObject"] == contract.RIGIFY_META_NAME, "Unexpected Rigify metarig object name")
    require(dcc["generatedControlObject"] == contract.RIGIFY_CONTROL_NAME, "Unexpected Rigify control object name")
    require(dcc["generatedRigId"] == contract.RIGIFY_GENERATED_ID, "Generated Rigify rig id is not deterministic")
    require(dcc["drivenGameBoneCount"] == 56, "Root plus 55 Humanoid bones must be DCC-driven")
    require(dcc["drivenHumanoidBoneCount"] == 55, "All Humanoid bones must be DCC-driven")
    require(dcc["driverConstraintCount"] == 58, "Expected 56 rotations plus Root/Hips translations")
    require(dcc["targetMap"] == contract.DCC_DRIVER_TARGETS, "DCC driver target map changed")
    require(dcc["driverOwnerSpace"] == "LOCAL", "Unexpected DCC owner space")
    require(dcc["driverTargetSpace"] == "LOCAL_OWNER_ORIENT", "Unexpected DCC target space")
    require(dcc["hipsDriverTarget"] == "hips", "Hips is not driven by the animator control")
    require(not dcc["metarigExported"] and not dcc["generatedControlsExported"] and not dcc["widgetsExported"], "DCC helpers are marked exportable")


def verify_source_blend(manifest: dict[str, object]) -> dict[str, object]:
    bpy.ops.wm.open_mainfile(filepath=str(contract.BLEND_PATH))
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in objects if obj.type == "MESH"]
    by_object_name = {obj.name: obj for obj in objects}
    expected_armatures = {
        contract.RIG_OBJECT_NAME,
        contract.RIGIFY_META_NAME,
        contract.RIGIFY_CONTROL_NAME,
    }
    require({obj.name for obj in armatures} == expected_armatures, f"Source armature set changed: {[obj.name for obj in armatures]}")
    require(contract.BIND_PROBE_NAME in by_object_name, "Source blend lost its bind probe")
    probe = by_object_name[contract.BIND_PROBE_NAME]
    require(probe.type == "MESH", "Bind probe is not a mesh")
    widgets = [obj for obj in meshes if obj != probe]
    require(widgets, "Source blend lost its Rigify control widgets")
    require(all(obj.name.startswith("WGT-") for obj in widgets), "Source blend contains an unexpected non-probe mesh")
    require(not any(obj.type in {"CAMERA", "LIGHT"} for obj in objects), "Source blend contains a camera or light")
    require(not bpy.data.actions, "Source blend contains actions")
    require(not bpy.data.libraries, "Source blend contains linked/appended libraries")
    rig = by_object_name[contract.RIG_OBJECT_NAME]
    metarig = by_object_name[contract.RIGIFY_META_NAME]
    control_rig = by_object_name[contract.RIGIFY_CONTROL_NAME]
    require(rig.name == contract.RIG_OBJECT_NAME, "Source armature object name changed")
    require(rig.parent is None, "Source armature has a parent")
    require(near_zero(rig.location), "Source armature location is not identity")
    require(near_zero(rig.rotation_euler), "Source armature rotation is not identity")
    require(near_one(rig.scale), "Source armature scale is not identity")
    require(rig.matrix_world.to_3x3().determinant() > 0.0, "Source armature is mirrored")
    require(not rig.constraints, "Source game armature object contains constraints")
    for dcc_object in (metarig, control_rig):
        require(dcc_object.parent is None, f"{dcc_object.name} is not a scene root")
        require(near_zero(dcc_object.location), f"{dcc_object.name} location is not identity")
        require(near_zero(dcc_object.rotation_euler), f"{dcc_object.name} rotation is not identity")
        require(near_one(dcc_object.scale), f"{dcc_object.name} scale is not identity")
        require(dcc_object.get("exported") is False, f"{dcc_object.name} is marked exportable")
        require(dcc_object.hide_render, f"{dcc_object.name} can render")
    require(metarig.get("sessionRole") == "rigify_human_metarig_not_exported", "Metarig role metadata changed")
    require(control_rig.get("sessionRole") == "rigify_generated_controls_not_exported", "Control-rig role metadata changed")
    dcc_manifest = manifest["dccAuthoring"]
    require(len(metarig.data.bones) == dcc_manifest["metarigBoneCount"], "Metarig bone count changed")
    require(len(control_rig.data.bones) == dcc_manifest["generatedControlBoneCount"], "Generated control-rig bone count changed")
    require(str(control_rig.data.get("rig_id", "")) == dcc_manifest["generatedRigId"], "Generated Rigify rig id changed")
    require(len(widgets) == dcc_manifest["widgetObjectCount"], "Rigify widget count changed")
    require(all(widget.get("sessionRole") == "rigify_widget_not_exported" for widget in widgets), "Rigify widget role metadata changed")
    require(all(widget.get("exported") is False and widget.hide_render for widget in widgets), "A Rigify widget is exportable/renderable")

    expected_constraint_count = len(contract.DCC_DRIVER_TARGETS) + 2
    actual_constraint_count = sum(len(pose_bone.constraints) for pose_bone in rig.pose.bones)
    require(actual_constraint_count == expected_constraint_count, "Source game-rig DCC constraint count changed")
    for game_name, target_name in contract.DCC_DRIVER_TARGETS.items():
        constraints = list(rig.pose.bones[game_name].constraints)
        rotations = [item for item in constraints if item.type == "COPY_ROTATION"]
        locations = [item for item in constraints if item.type == "COPY_LOCATION"]
        require(len(rotations) == 1, f"{game_name} lost its single Rigify rotation driver")
        require(len(locations) == (1 if game_name in {"Root", "Hips"} else 0), f"{game_name} has the wrong location driver count")
        for constraint in constraints:
            require(constraint.target == control_rig, f"{game_name} targets the wrong DCC rig")
            require(constraint.subtarget == target_name, f"{game_name} targets the wrong DCC bone")
            require(
                constraint.owner_space == "LOCAL" and constraint.target_space == "LOCAL_OWNER_ORIENT",
                f"{game_name} DCC driver lost owner-oriented local retargeting",
            )
            require(abs(constraint.influence - 1.0) <= 1e-8 and not constraint.mute, f"{game_name} DCC driver is inactive")
    for spec in contract.BONE_SPECS:
        if spec.role == "socket":
            require(not rig.pose.bones[spec.name].constraints, f"Socket {spec.name} must inherit from its game parent")
    by_name = {bone.name: bone for bone in rig.data.bones}
    require(set(by_name) == set(contract.SPEC_BY_NAME), "Source bone set changed")
    maximum_source_head_deviation = 0.0
    maximum_source_tail_deviation = 0.0
    for spec in contract.BONE_SPECS:
        bone = by_name[spec.name]
        require((bone.parent.name if bone.parent else None) == spec.parent, f"Source parent changed for {spec.name}")
        head_deviation = (bone.head_local - Vector(spec.head)).length
        tail_deviation = (bone.tail_local - Vector(spec.tail)).length
        maximum_source_head_deviation = max(maximum_source_head_deviation, head_deviation)
        maximum_source_tail_deviation = max(maximum_source_tail_deviation, tail_deviation)
        require(head_deviation <= 1e-7, f"Source head changed for {spec.name}")
        require(tail_deviation <= 1e-7, f"Source tail changed for {spec.name}")
        require(bone.use_deform == spec.deform, f"Source deform flag changed for {spec.name}")
        require(str(bone.get("sessionRole")) == spec.role, f"Source role metadata changed for {spec.name}")
        require(abs(bone.matrix_local.to_3x3().determinant() - 1.0) <= 1e-5, f"Source rest basis is not orthonormal for {spec.name}")

    bpy.context.view_layer.update()
    evaluated = rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
    maximum_driven_rest_deviation = 0.0
    for spec in contract.BONE_SPECS:
        deviation = (evaluated.pose.bones[spec.name].head - Vector(spec.head)).length
        maximum_driven_rest_deviation = max(maximum_driven_rest_deviation, deviation)
        require(deviation <= HEAD_TOLERANCE_METERS, f"DCC driver changes the neutral joint for {spec.name}")

    # Prove the generated animator controls are functionally connected, not just
    # present beside the game rig.  Exercise one standard Rigify FK control in
    # memory, measure the evaluated game-bone response, then restore neutral.
    fk_switch = control_rig.pose.bones["upper_arm_parent.L"]
    fk_control = control_rig.pose.bones["upper_arm_fk.L"]
    require("IK_FK" in fk_switch, "Rigify arm lost its IK/FK switch")
    previous_switch = float(fk_switch["IK_FK"])
    previous_basis = fk_control.matrix_basis.copy()
    fk_switch["IK_FK"] = 1.0
    bpy.context.view_layer.update()
    baseline = rig.evaluated_get(bpy.context.evaluated_depsgraph_get()).pose.bones["LeftUpperArm"].matrix.copy()
    fk_control.rotation_mode = "XYZ"
    fk_control.rotation_euler.z = 0.35
    bpy.context.view_layer.update()
    moved = rig.evaluated_get(bpy.context.evaluated_depsgraph_get()).pose.bones["LeftUpperArm"].matrix.copy()
    control_response_delta = sum(
        abs(baseline[row][column] - moved[row][column])
        for row in range(4)
        for column in range(4)
    )
    require(control_response_delta >= 0.1, "Rigify FK control does not drive the game upper arm")
    fk_control.matrix_basis = previous_basis
    fk_switch["IK_FK"] = previous_switch
    bpy.context.view_layer.update()
    restored = rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
    maximum_restored_deviation = max(
        (restored.pose.bones[spec.name].head - Vector(spec.head)).length
        for spec in contract.BONE_SPECS
    )
    require(maximum_restored_deviation <= HEAD_TOLERANCE_METERS, "DCC response test did not restore the neutral pose")

    require(probe.parent == rig, "Bind probe is not parented to the rig")
    require(probe.get("exported") is False, "Bind probe is marked exportable")
    require(probe.hide_render, "Bind probe can render")
    modifiers = [modifier for modifier in probe.modifiers if modifier.type == "ARMATURE"]
    require(len(modifiers) == 1 and modifiers[0].object == rig, "Bind probe armature modifier is invalid")
    group_names = {group.index: group.name for group in probe.vertex_groups}
    maximum_weights = 0
    for vertex in probe.data.vertices:
        active = [element for element in vertex.groups if element.weight > 1e-6]
        require(1 <= len(active) <= 4, f"Bind probe vertex {vertex.index} has {len(active)} weights")
        require(abs(sum(element.weight for element in active) - 1.0) <= 1e-6, f"Bind probe vertex {vertex.index} is not normalized")
        require(all(group_names[element.group] in by_name for element in active), "Bind probe references an unknown bone")
        maximum_weights = max(maximum_weights, len(active))
    probe.data.calc_loop_triangles()
    for triangle in probe.data.loop_triangles:
        first, second, third = (probe.data.vertices[index].co for index in triangle.vertices)
        require((second - first).cross(third - first).length > 1e-10, "Bind probe contains a degenerate triangle")
    require(maximum_weights == 4, "Bind probe no longer exercises four weights")
    return {
        "objects": len(objects),
        "armatures": len(armatures),
        "gameArmatures": 1,
        "rigifyMetarigs": 1,
        "rigifyGeneratedControlRigs": 1,
        "rigifyWidgetMeshes": len(widgets),
        "nonExportedBindProbeMeshes": 1,
        "boneCount": len(by_name),
        "metarigBoneCount": len(metarig.data.bones),
        "generatedControlBoneCount": len(control_rig.data.bones),
        "gameRigDccDriverConstraints": actual_constraint_count,
        "animatorControlResponse": {
            "control": "upper_arm_fk.L",
            "ikFkModeDuringTest": "FK",
            "rotationRadians": 0.35,
            "drivenGameBone": "LeftUpperArm",
            "matrixAbsoluteDelta": control_response_delta,
            "neutralRestored": True,
            "maximumRestoredJointDeviationMeters": maximum_restored_deviation,
        },
        "maximumDrivenRestJointDeviationMeters": maximum_driven_rest_deviation,
        "maximumSourceHeadDeviationMeters": maximum_source_head_deviation,
        "maximumSourceTailDeviationMeters": maximum_source_tail_deviation,
        "bindProbeMaximumWeights": maximum_weights,
        "bindProbeWeightsNormalized": True,
        "actions": len(bpy.data.actions),
    }


def verify_t_pose(by_name: dict[str, bpy.types.Bone]) -> dict[str, object]:
    maximum_mirror_error = 0.0
    maximum_arm_vertical_range = 0.0
    maximum_arm_forward_range = 0.0
    maximum_leg_lateral_drift = 0.0
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        arm_points = [
            by_name[side + stem].head_local
            for stem in ("Shoulder", "UpperArm", "LowerArm", "Hand", "MiddleDistal")
        ]
        maximum_arm_vertical_range = max(
            maximum_arm_vertical_range,
            max(point.z for point in arm_points) - min(point.z for point in arm_points),
        )
        maximum_arm_forward_range = max(
            maximum_arm_forward_range,
            max(point.y for point in arm_points) - min(point.y for point in arm_points),
        )
        shoulder = by_name[side + "Shoulder"].head_local
        wrist = by_name[side + "Hand"].head_local
        require((wrist.x - shoulder.x) * sign > 0.0, f"{side} arm does not extend laterally")
        leg_points = [by_name[side + stem].head_local for stem in ("UpperLeg", "LowerLeg", "Foot", "Toes")]
        maximum_leg_lateral_drift = max(
            maximum_leg_lateral_drift,
            max(abs(point.x - leg_points[0].x) for point in leg_points),
        )
        foot = by_name[side + "Foot"].head_local
        toes = by_name[side + "Toes"].head_local
        require(toes.y < foot.y, f"{side} foot does not point Blender-forward (-Y)")
    for stem in (
        "Shoulder", "UpperArm", "LowerArm", "Hand",
        "ThumbProximal", "ThumbIntermediate", "ThumbDistal",
        "IndexProximal", "IndexIntermediate", "IndexDistal",
        "MiddleProximal", "MiddleIntermediate", "MiddleDistal",
        "RingProximal", "RingIntermediate", "RingDistal",
        "LittleProximal", "LittleIntermediate", "LittleDistal",
        "UpperLeg", "LowerLeg", "Foot", "Toes",
    ):
        left = by_name["Left" + stem].head_local
        right = by_name["Right" + stem].head_local
        maximum_mirror_error = max(
            maximum_mirror_error,
            (left - Vector((-right.x, right.y, right.z))).length,
        )
    require(maximum_mirror_error <= HEAD_TOLERANCE_METERS, "Round-trip neutral pose lost exact symmetry")
    require(maximum_arm_vertical_range <= 0.10, "Accepted T-pose arm slope is excessive")
    require(maximum_arm_forward_range <= 0.10, "Accepted T-pose arm forward range is excessive")
    require(maximum_leg_lateral_drift <= 0.025, "Accepted neutral leg chain has excessive lateral drift")
    require(abs(by_name["Root"].head_local.z) <= HEAD_TOLERANCE_METERS, "Root is not on ground")
    require((by_name["Hips"].head_local - Vector(contract.SPEC_BY_NAME["Hips"].head)).length <= HEAD_TOLERANCE_METERS, "Hips landmark changed")
    require(abs(by_name["Socket_HeadFX"].head_local.z - 1.78) <= HEAD_TOLERANCE_METERS, "Crown landmark changed")
    for name in ("Socket_FootFX_L", "Socket_FootFX_R"):
        require(
            (by_name[name].head_local - Vector(contract.SPEC_BY_NAME[name].head)).length <= HEAD_TOLERANCE_METERS,
            f"{name} changed",
        )
    return {
        "maximumMirrorErrorMeters": maximum_mirror_error,
        "maximumArmVerticalRangeMeters": maximum_arm_vertical_range,
        "maximumArmForwardRangeMeters": maximum_arm_forward_range,
        "maximumLegLateralDriftMeters": maximum_leg_lateral_drift,
        "groundToCrownJointLandmarkMeters": round(
            by_name["Socket_HeadFX"].head_local.z - by_name["Root"].head_local.z,
            6,
        ),
        "hipsHeightMeters": round(by_name["Hips"].head_local.z, 6),
        "armsLaterallyExtended": True,
        "preservesAcceptedArmSlope": True,
        "legsAnatomicallyNeutral": True,
        "feetForwardNegativeY": True,
    }


def verify_fbx() -> dict[str, object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(
        filepath=str(contract.FBX_PATH),
        use_anim=True,
        automatic_bone_orientation=False,
        ignore_leaf_bones=False,
    )
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    require(len(objects) == 1 and len(armatures) == 1, f"FBX is not skeleton-only: {[(obj.name, obj.type) for obj in objects]}")
    require(not bpy.data.actions, "FBX contains an action")
    rig = armatures[0]
    require(rig.name == contract.RIG_OBJECT_NAME, f"Imported rig name changed: {rig.name}")
    require(rig.parent is None, "Imported rig has a parent or wrapper")
    require(near_zero(rig.location), "Imported rig location is not identity")
    require(near_zero(rig.rotation_euler), f"Imported rig rotation is not identity: {list(rig.rotation_euler)}")
    require(near_one(rig.scale), "Imported rig scale is not identity")
    require(rig.matrix_world.to_3x3().determinant() > 0.0, "Imported rig is mirrored")
    require(not rig.constraints and not any(bone.constraints for bone in rig.pose.bones), "Imported FBX contains constraints")
    by_name = {bone.name: bone for bone in rig.data.bones}
    require(len(by_name) == 66, f"Expected 66 imported bones, found {len(by_name)}")
    require(set(by_name) == set(contract.SPEC_BY_NAME), f"Imported bone set changed: {set(by_name) ^ set(contract.SPEC_BY_NAME)}")
    require(not any(any(token in name.upper() for token in contract.FORBIDDEN_BONE_TOKENS) for name in by_name), "FBX contains a control/mechanism bone")

    maximum_head_deviation = 0.0
    per_bone_head_deviation: dict[str, float] = {}
    for spec in contract.BONE_SPECS:
        bone = by_name[spec.name]
        require((bone.parent.name if bone.parent else None) == spec.parent, f"Imported parent changed for {spec.name}")
        deviation = (bone.head_local - Vector(spec.head)).length
        per_bone_head_deviation[spec.name] = round(deviation, 9)
        maximum_head_deviation = max(maximum_head_deviation, deviation)
        require(deviation <= HEAD_TOLERANCE_METERS, f"Imported joint changed for {spec.name}: {deviation:.8f} m")
    t_pose = verify_t_pose(by_name)
    return {
        "objects": len(objects),
        "armatures": len(armatures),
        "meshes": 0,
        "cameras": 0,
        "lights": 0,
        "actions": len(bpy.data.actions),
        "boneCount": len(by_name),
        "maximumJointHeadDeviationMeters": maximum_head_deviation,
        "jointHeadDeviationMeters": per_bone_head_deviation,
        "identityRootTransform": True,
        "positiveDeterminant": True,
        "controlsOrMechanismBones": 0,
        "note": "FBX stores joint transforms, not Blender edit-bone tail display lengths; every joint head/parent is checked, while source tails/rest matrices are checked in the .blend.",
        "tPose": t_pose,
    }


def main() -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    require(contract.MANIFEST_PATH.is_file(), f"Missing manifest: {contract.MANIFEST_PATH}")
    manifest = json.loads(contract.MANIFEST_PATH.read_text(encoding="utf-8"))
    verify_files(manifest)
    verify_manifest(manifest)
    source_report = verify_source_blend(manifest)
    fbx_report = verify_fbx()
    report = {
        "status": "PASS_TECHNICAL_DCC_ROUND_TRIP_NOT_IN_UNITY",
        "assetId": contract.ASSET_ID,
        "externalSkeletonOrModelDependency": False,
        "legacySkeletonImportedOrSampled": False,
        "sourceBlendSha256": manifest["outputHashes"][contract.BLEND_PATH.name],
        "fbxSha256": manifest["outputHashes"][contract.FBX_PATH.name],
        "manifestSha256": sha256(contract.MANIFEST_PATH),
        "sourceBlend": source_report,
        "fbxRoundTrip": fbx_report,
        "unityIntegrationPerformed": False,
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
