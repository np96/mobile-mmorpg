#!/usr/bin/env python3
"""Independent source/FBX verifier for Session Humanoid Rig V2."""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


SCRIPT_PATH = Path(__file__).resolve()
PROJECT_ROOT = SCRIPT_PATH.parents[3]
sys.path.insert(0, str(SCRIPT_PATH.parent))
import build_session_humanoid_rig_v2 as contract


REPORT_PATH = (
    PROJECT_ROOT.parent
    / "outputs/art/blender/session-humanoid-rig-v2/SessionHumanoidRigV2FbxRoundTrip.json"
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


def verify_files(manifest: dict[str, object]) -> None:
    require("Assets" not in contract.SOURCE_DIR.parts, "V2 source is under Unity Assets")
    stray = list((PROJECT_ROOT / "Assets").rglob("*SessionHumanoidRigV2*"))
    require(not stray, f"V2 was integrated before approval: {stray}")
    require(manifest["integrationStatus"] == "SOURCE_DCC_NOT_IN_UNITY", "Integration boundary changed")
    for relative, expected_hash in manifest["sourceFiles"].items():
        path = PROJECT_ROOT / relative
        require(path.is_file(), f"Missing source: {path}")
        require(sha256(path) == expected_hash, f"Stale source hash: {path}")
    for name, expected_hash in manifest["outputHashes"].items():
        path = contract.BLEND_PATH if name == contract.BLEND_PATH.name else contract.OUTPUT_DIR / name
        require(path.is_file(), f"Missing output: {path}")
        require(sha256(path) == expected_hash, f"Stale output hash: {path}")
    contract.read_frozen_v1_manifest()


def verify_manifest(manifest: dict[str, object], v1_manifest: dict[str, object]) -> dict[str, object]:
    require(manifest["schemaVersion"] == 1 and manifest["assetId"] == contract.ASSET_ID, "Wrong V2 manifest identity")
    require(manifest["rigifyUsed"] and not manifest["rigifyContentInFbx"], "Rigify boundary changed")
    require(not manifest["externalSkeletonOrModelDependency"], "External skeleton dependency declared")
    hierarchy = manifest["hierarchy"]
    require(hierarchy["exportedBoneCount"] == 68, "Expected 68 joints")
    require(hierarchy["unityHumanoidDeformBoneCount"] == 55, "Humanoid mapping count changed")
    require(hierarchy["additionalDeformBoneCount"] == 2 and hierarchy["totalDeformBoneCount"] == 57, "V2 deform counts changed")
    require(hierarchy["socketBoneCount"] == 10, "Socket count changed")
    require(hierarchy["orderedBoneNames"] == [spec.name for spec in contract.BONE_SPECS], "V2 ordered hierarchy changed")
    require(hierarchy["humanBoneMap"] == contract.HUMANOID_MAP, "Humanoid map changed")
    require(hierarchy["bones"][:66] == v1_manifest["hierarchy"]["bones"], "Frozen V1 records changed")
    prefix_bytes = contract.ordered_records_bytes(hierarchy["bones"][:66])
    prefix_hash = hashlib.sha256(prefix_bytes).hexdigest()
    require(prefix_hash == hierarchy["v1PrefixOrderedRecordsSerializedSha256"], "V1 prefix hash changed")
    require(prefix_hash == manifest["v1Compatibility"]["first66OrderedRecordsSerializedSha256"], "V1 compatibility hash mismatch")
    compatibility = manifest["v1Compatibility"]
    require(compatibility["first66BoneRecordsUnchanged"] and compatibility["first66BoneRecordsOrderUnchanged"], "V1 prefix is not frozen")
    require(not compatibility["v1FilesWrittenByBuild"], "V2 claims to write V1")
    require(compatibility["frozenManifestSha256"] == contract.EXPECTED_V1_MANIFEST_SHA256, "Wrong V1 manifest ancestry")
    support = manifest["bendSupportContract"]
    require(support["names"] == list(contract.SUPPORT_NAMES), "Support names changed")
    require(support["deform"] and not support["connected"] and not support["unityHumanoidMapped"], "Support role changed")
    require(support["displayLengthMeters"] == 0.08 and support["dccRotationFactor"] == 0.5, "Support geometry/factor changed")
    require(not support["displayLengthFbxSemantic"] and support["displayLengthScope"].startswith("DCC source only"), "FBX leaf display length is treated as semantic")
    require(support["exportedCurvesAuthority"] == "DCC and Generic proof only", "Support curve authority changed")
    require(support["runtimeAuthority"].startswith("Unity LateUpdate driver"), "Runtime driver is not authoritative")
    runtime = manifest["unityRuntimeSupportDriverContract"]
    require(runtime["implementationStatus"] == "NOT_IMPLEMENTED_IN_THIS_DCC_TASK", "Manifest falsely claims a runtime implementation")
    require(not runtime["humanoidRetargetingAloneIsAuthoritative"], "Humanoid retargeting is incorrectly authoritative")
    optimize = runtime["optimizeGameObjectsPrecondition"]
    require(len(optimize["requiredExtraExposedTransformPathsWhenOptimized"]) == 2, "Support exposure paths changed")
    require(optimize["mustBeVerifiedAfterUnityImport"], "Unity import exposure check is not required")
    dcc = manifest["dccAuthoring"]
    require(dcc["driverConstraintCount"] == 60, "Expected 60 source DCC constraints")
    require(dcc["drivenGameBoneCount"] == 58 and dcc["drivenBendSupportBoneCount"] == 2, "Driven joint count changed")
    require(dcc["bendSupportTargetMap"] == contract.SUPPORT_TARGETS, "Support target map changed")
    export = manifest["exportContract"]
    require(export["fbxContainsArmatures"] == 1 and export["fbxContainsMeshes"] == 0 and export["fbxContainsActions"] == 0, "FBX purity contract changed")
    require(export["controlsOrMechanismBones"] == 0, "FBX permits DCC bones")
    return {"first66OrderedRecordsSerializedSha256": prefix_hash, "first66RecordEquality": True}


def verify_source_blend(manifest: dict[str, object], v1_manifest: dict[str, object]) -> dict[str, object]:
    bpy.ops.wm.open_mainfile(filepath=str(contract.BLEND_PATH))
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    meshes = [obj for obj in objects if obj.type == "MESH"]
    by_object = {obj.name: obj for obj in objects}
    expected_armatures = {contract.RIG_OBJECT_NAME, contract.RIGIFY_META_NAME, contract.RIGIFY_CONTROL_NAME}
    require({obj.name for obj in armatures} == expected_armatures, "Source armature set changed")
    require(contract.BIND_PROBE_NAME in by_object, "Source bind probe missing")
    probe = by_object[contract.BIND_PROBE_NAME]
    widgets = [obj for obj in meshes if obj != probe]
    require(widgets and all(obj.name.startswith("WGT-") for obj in widgets), "Rigify widgets missing or contaminated")
    require(not bpy.data.actions and not bpy.data.libraries, "Source contains actions or linked libraries")
    require(not any(obj.type in {"CAMERA", "LIGHT"} for obj in objects), "Source contains camera/light")
    rig = by_object[contract.RIG_OBJECT_NAME]
    control = by_object[contract.RIGIFY_CONTROL_NAME]
    metarig = by_object[contract.RIGIFY_META_NAME]
    require(rig.parent is None and near_zero(rig.location) and near_zero(rig.rotation_euler) and near_one(rig.scale), "Game rig transform is not identity")
    require(not rig.constraints and rig.matrix_world.to_3x3().determinant() > 0.0, "Game armature object is constrained/mirrored")
    for helper in (control, metarig):
        require(helper.get("exported") is False and helper.hide_render, f"{helper.name} export boundary changed")

    by_name = {bone.name: bone for bone in rig.data.bones}
    require(set(by_name) == set(contract.SPEC_BY_NAME) and len(by_name) == 68, "Source V2 bone set changed")
    maximum_head_error = 0.0
    maximum_tail_error = 0.0
    for spec in contract.BONE_SPECS:
        bone = by_name[spec.name]
        require((bone.parent.name if bone.parent else None) == spec.parent, f"Source parent changed for {spec.name}")
        head_error = (bone.head_local - Vector(spec.head)).length
        tail_error = (bone.tail_local - Vector(spec.tail)).length
        maximum_head_error = max(maximum_head_error, head_error)
        maximum_tail_error = max(maximum_tail_error, tail_error)
        require(head_error <= 1e-7 and tail_error <= 1e-7, f"Source rest changed for {spec.name}")
        require(bone.use_deform == spec.deform and str(bone.get("sessionRole")) == spec.role, f"Source role changed for {spec.name}")
        require(abs(bone.matrix_local.to_3x3().determinant() - 1.0) <= 1e-5, f"Non-orthonormal rest for {spec.name}")
    records = [contract.base.bone_contract_record(rig, spec) for spec in contract.BONE_SPECS]
    require(records == manifest["hierarchy"]["bones"], "Source records differ from manifest")
    require(records[:66] == v1_manifest["hierarchy"]["bones"], "Source changed frozen V1 records")

    total_constraints = sum(len(pose_bone.constraints) for pose_bone in rig.pose.bones)
    require(total_constraints == 60, f"Source has {total_constraints} constraints")
    for game_name, target_name in contract.DCC_DRIVER_TARGETS.items():
        constraints = list(rig.pose.bones[game_name].constraints)
        require(len([item for item in constraints if item.type == "COPY_ROTATION"]) == 1, f"{game_name} rotation bridge changed")
        require(len([item for item in constraints if item.type == "COPY_LOCATION"]) == (1 if game_name in {"Root", "Hips"} else 0), f"{game_name} location bridge changed")
        for item in constraints:
            require(item.target == control and item.subtarget == target_name, f"{game_name} DCC target changed")
            require(item.owner_space == "LOCAL" and item.target_space == "LOCAL_OWNER_ORIENT", f"{game_name} DCC spaces changed")
            require(abs(item.influence - 1.0) <= 1e-8 and not item.mute, f"{game_name} DCC bridge inactive")
    support_orientation_error = 0.0
    for side in ("Left", "Right"):
        support_name = side + "ElbowSupport"
        lower_name = side + "LowerArm"
        item = rig.pose.bones[support_name].constraints[0]
        require(len(rig.pose.bones[support_name].constraints) == 1 and item.type == "COPY_ROTATION", f"{support_name} bridge changed")
        require(item.target == control and item.subtarget == contract.SUPPORT_TARGETS[support_name], f"{support_name} target changed")
        require(item.owner_space == "LOCAL" and item.target_space == "LOCAL_OWNER_ORIENT" and item.mix_mode == "REPLACE", f"{support_name} spaces changed")
        require(abs(item.influence - 0.5) <= 1e-8 and not item.mute, f"{support_name} factor changed")
        support = by_name[support_name]
        lower = by_name[lower_name]
        error = max(abs(support.matrix_local[r][c] - lower.matrix_local[r][c]) for r in range(3) for c in range(3))
        support_orientation_error = max(support_orientation_error, error)
        require(error <= 0.0000005 and (support.head_local - lower.head_local).length <= 1e-9, f"{support_name} rest alignment changed")
    evaluated = rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
    maximum_driven_rest_error = max(
        (evaluated.pose.bones[spec.name].head - Vector(spec.head)).length
        for spec in contract.BONE_SPECS
    )
    require(maximum_driven_rest_error <= HEAD_TOLERANCE_METERS, "DCC layer shifts neutral joints")

    modifiers = [item for item in probe.modifiers if item.type == "ARMATURE"]
    require(len(modifiers) == 1 and modifiers[0].object == rig and probe.get("exported") is False, "Bind probe boundary changed")
    max_weights = 0
    group_names = {group.index: group.name for group in probe.vertex_groups}
    for vertex in probe.data.vertices:
        active = [item for item in vertex.groups if item.weight > 1e-6]
        require(1 <= len(active) <= 4 and abs(sum(item.weight for item in active) - 1.0) <= 1e-6, "Bind probe weights invalid")
        require(all(group_names[item.group] in by_name for item in active), "Bind probe references an unknown bone")
        max_weights = max(max_weights, len(active))
    require(max_weights == 4, "Bind probe no longer exercises four weights")
    return {
        "objects": len(objects),
        "armatures": len(armatures),
        "boneCount": len(by_name),
        "gameDccDriverConstraints": total_constraints,
        "bendSupportConstraints": 2,
        "rigifyMetarigBones": len(metarig.data.bones),
        "rigifyControlBones": len(control.data.bones),
        "rigifyWidgetMeshes": len(widgets),
        "maximumSourceHeadDeviationMeters": maximum_head_error,
        "maximumSourceTailDeviationMeters": maximum_tail_error,
        "maximumDrivenRestJointDeviationMeters": maximum_driven_rest_error,
        "maximumSupportVsLowerArmRestOrientationComponentError": support_orientation_error,
        "v1PrefixRecordsExact": True,
        "bindProbeMaximumWeights": max_weights,
        "actions": 0,
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
    require(not bpy.data.actions, "Rig FBX contains animation")
    rig = armatures[0]
    require(rig.name == contract.RIG_OBJECT_NAME and rig.parent is None, "Imported armature identity changed")
    require(near_zero(rig.location) and near_zero(rig.rotation_euler) and near_one(rig.scale), "Imported root transform is not identity")
    require(rig.matrix_world.to_3x3().determinant() > 0.0, "Imported armature is mirrored")
    require(not rig.constraints and not any(bone.constraints for bone in rig.pose.bones), "Imported FBX contains constraints")
    by_name = {bone.name: bone for bone in rig.data.bones}
    require(len(by_name) == 68 and set(by_name) == set(contract.SPEC_BY_NAME), "Imported V2 bone set changed")
    require(not any(any(token in name.upper() for token in contract.FORBIDDEN_BONE_TOKENS) for name in by_name), "Imported FBX contains DCC/helper bones")
    maximum_head_error = 0.0
    per_bone = {}
    for spec in contract.BONE_SPECS:
        bone = by_name[spec.name]
        require((bone.parent.name if bone.parent else None) == spec.parent, f"Imported parent changed for {spec.name}")
        error = (bone.head_local - Vector(spec.head)).length
        per_bone[spec.name] = round(error, 9)
        maximum_head_error = max(maximum_head_error, error)
        require(error <= HEAD_TOLERANCE_METERS, f"Imported joint changed for {spec.name}: {error:.8f} m")
    maximum_imported_support_basis_error = 0.0
    imported_support_properties = {}
    for side in ("Left", "Right"):
        support = by_name[side + "ElbowSupport"]
        lower = by_name[side + "LowerArm"]
        require((support.head_local - lower.head_local).length <= HEAD_TOLERANCE_METERS, f"Imported {support.name} lost elbow pivot")
        require(support.use_deform, f"Imported {support.name} is not marked deforming")
        basis_error = max(
            abs(support.matrix_local[row][column] - lower.matrix_local[row][column])
            for row in range(3) for column in range(3)
        )
        maximum_imported_support_basis_error = max(maximum_imported_support_basis_error, basis_error)
        require(basis_error <= HEAD_TOLERANCE_METERS, f"Imported {support.name} lost LowerArm rest basis")
        properties = {
            "sessionRole": support.get("sessionRole"),
            "runtimeDriverAuthority": support.get("runtimeDriverAuthority"),
            "genericDccProofOnly": support.get("genericDccProofOnly"),
        }
        require(properties == {
            "sessionRole": "bend_support",
            "runtimeDriverAuthority": "Unity LateUpdate support-bone driver",
            "genericDccProofOnly": True,
        }, f"Imported {support.name} role/provenance changed: {properties}")
        imported_support_properties[support.name] = properties
    require(abs(by_name["Socket_HeadFX"].head_local.z - 1.78) <= HEAD_TOLERANCE_METERS, "Crown changed")
    require(abs(by_name["Root"].head_local.z) <= HEAD_TOLERANCE_METERS, "Root is not grounded")
    return {
        "objects": 1,
        "armatures": 1,
        "meshes": 0,
        "actions": 0,
        "boneCount": len(by_name),
        "maximumJointHeadDeviationMeters": maximum_head_error,
        "jointHeadDeviationMeters": per_bone,
        "identityRootTransform": True,
        "positiveDeterminant": True,
        "controlsOrMechanismBones": 0,
        "bendSupportCount": 2,
        "maximumSupportVsLowerArmImportedRestBasisComponentError": maximum_imported_support_basis_error,
        "leafDisplayLengthVerified": False,
        "leafDisplayLengthSemantic": False,
        "importedSupportCustomProperties": imported_support_properties,
        "importedSupportUseDeform": True,
        "groundToCrownJointLandmarkMeters": round(by_name["Socket_HeadFX"].head_local.z, 6),
    }


def main() -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    contract.configure_base_module()
    require(contract.MANIFEST_PATH.is_file(), "V2 manifest is missing")
    manifest = json.loads(contract.MANIFEST_PATH.read_text(encoding="utf-8"))
    v1_manifest = contract.read_frozen_v1_manifest()
    verify_files(manifest)
    prefix_report = verify_manifest(manifest, v1_manifest)
    source_report = verify_source_blend(manifest, v1_manifest)
    fbx_report = verify_fbx()
    report = {
        "status": "PASS_TECHNICAL_DCC_ROUND_TRIP_NOT_IN_UNITY",
        "assetId": contract.ASSET_ID,
        "sourceBlendSha256": manifest["outputHashes"][contract.BLEND_PATH.name],
        "fbxSha256": manifest["outputHashes"][contract.FBX_PATH.name],
        "manifestSha256": sha256(contract.MANIFEST_PATH),
        "frozenV1": {
            "manifestSha256": contract.EXPECTED_V1_MANIFEST_SHA256,
            "blendSha256": contract.EXPECTED_V1_BLEND_SHA256,
            "fbxSha256": contract.EXPECTED_V1_FBX_SHA256,
            **prefix_report,
        },
        "sourceBlend": source_report,
        "fbxRoundTrip": fbx_report,
        "unityIntegrationPerformed": False,
        "runtimeSupportDriverImplemented": False,
        "runtimeSupportDriverAuthorityContractDeclared": True,
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps(report, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
