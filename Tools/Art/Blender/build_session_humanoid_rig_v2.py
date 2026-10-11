#!/usr/bin/env python3
"""Build the isolated Session Humanoid Rig V2 bend-support extension.

V2 deliberately reuses the frozen, first-party V1 numeric game-rig contract as
an implementation library.  It does not open or rewrite a V1 output.  The first
66 ordered game-joint records remain exactly equal to the frozen V1 manifest;
two deforming elbow support siblings are appended for production skinning.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Vector


SCRIPT_PATH = Path(__file__).resolve()
PROJECT_ROOT = SCRIPT_PATH.parents[3]
TOOLS_DIR = SCRIPT_PATH.parent
sys.path.insert(0, str(TOOLS_DIR))
import build_session_humanoid_rig_v1 as base


ASSET_ID = "session-humanoid-rig-v2"
SOURCE_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidRigV2"
OUTPUT_DIR = SOURCE_DIR / "Outputs"
BLEND_PATH = SOURCE_DIR / "SessionHumanoidRigV2.blend"
FBX_PATH = OUTPUT_DIR / "SessionHumanoidRigV2.fbx"
MANIFEST_PATH = OUTPUT_DIR / "SessionHumanoidRigV2.manifest.json"
PROVENANCE_PATH = OUTPUT_DIR / "SessionHumanoidRigV2.PROVENANCE.md"
CONTRACT_PATH = SOURCE_DIR / "SessionHumanoidRigV2.CONTRACT.md"
VERIFIER_PATH = SCRIPT_PATH.with_name("verify_session_humanoid_rig_v2.py")

RIG_OBJECT_NAME = "SessionHumanoidRigV2"
ARMATURE_DATA_NAME = "SessionHumanoidRigV2_Armature"
BIND_PROBE_NAME = "SessionHumanoidRigV2_BindProbe_NotExported"
RIGIFY_META_NAME = "SessionHumanoidRigV2_RigifyHumanMeta_NotExported"
RIGIFY_CONTROL_NAME = "SessionHumanoidRigV2_RigifyControls_NotExported"
RIGIFY_WIDGET_COLLECTION_NAME = "SessionHumanoidRigV2_RigifyWidgets_NotExported"
RIGIFY_GENERATED_ID = "srhvrigifyv2dcc0"

V1_MANIFEST_PATH = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/SessionHumanoidRigV1/Outputs/SessionHumanoidRigV1.manifest.json"
)
V1_BLEND_PATH = V1_MANIFEST_PATH.parents[1] / "SessionHumanoidRigV1.blend"
V1_FBX_PATH = V1_MANIFEST_PATH.with_name("SessionHumanoidRigV1.fbx")
EXPECTED_V1_MANIFEST_SHA256 = "3ec07eb3dc4f62c67383f1ee61bfcd74c9a2f62ce57a3e869841cdf732f33278"
EXPECTED_V1_BLEND_SHA256 = "b9adc7881ab12cadb8a894952e7fb69c1cddff6a6886ea6ccc1922d89981e180"
EXPECTED_V1_FBX_SHA256 = "261f0e389a7358a12bcdda1b3ae9a8fd19429c446a434fae7b2a7325f50e7d6d"

V1_BONE_COUNT = 66
SUPPORT_LENGTH_METERS = 0.080
SUPPORT_ROTATION_FACTOR = 0.5
SUPPORT_NAMES = ("LeftElbowSupport", "RightElbowSupport")
SUPPORT_TARGETS = {
    "LeftElbowSupport": "ORG-forearm.L",
    "RightElbowSupport": "ORG-forearm.R",
}


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def bytes_sha256(payload: bytes) -> str:
    return hashlib.sha256(payload).hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def support_spec(side: str) -> base.BoneSpec:
    lower = base.SPEC_BY_NAME[side + "LowerArm"]
    head = Vector(lower.head)
    direction = (Vector(lower.tail) - head).normalized()
    tail = head + direction * SUPPORT_LENGTH_METERS
    return base.BoneSpec(
        side + "ElbowSupport",
        side + "UpperArm",
        tuple(float(value) for value in head),
        tuple(float(value) for value in tail),
        True,
        False,
        "bend_support",
        lower.roll_reference,
        None,
    )


V1_BONE_SPECS = tuple(base.BONE_SPECS)
BONE_SPECS = V1_BONE_SPECS + (support_spec("Left"), support_spec("Right"))
SPEC_BY_NAME = {spec.name: spec for spec in BONE_SPECS}
HUMANOID_MAP = dict(base.HUMANOID_MAP)
DCC_DRIVER_TARGETS = dict(base.DCC_DRIVER_TARGETS)
FORBIDDEN_BONE_TOKENS = base.FORBIDDEN_BONE_TOKENS
REFERENCE_HEIGHT_METERS = base.REFERENCE_HEIGHT_METERS
REST_FIT_REVISION = base.REST_FIT_REVISION
EXPECTED_REST_FIT_ARTIFACT_SHA256 = base.EXPECTED_REST_FIT_ARTIFACT_SHA256
EXPECTED_REST_FIT_PROPOSAL_SHA256 = base.EXPECTED_REST_FIT_PROPOSAL_SHA256
REST_FIT_ARTIFACT_PATH = base.REST_FIT_ARTIFACT_PATH
REST_FIT_BUILD_MANIFEST_PATH = base.REST_FIT_BUILD_MANIFEST_PATH
REST_FIT_PROPOSAL = base.REST_FIT_PROPOSAL
FBX_CREATION_TIMESTAMP = base.FBX_CREATION_TIMESTAMP


def configure_base_module() -> None:
    """Point reusable V1 construction functions only at isolated V2 paths."""

    values = {
        "ASSET_ID": ASSET_ID,
        "SOURCE_DIR": SOURCE_DIR,
        "OUTPUT_DIR": OUTPUT_DIR,
        "BLEND_PATH": BLEND_PATH,
        "FBX_PATH": FBX_PATH,
        "MANIFEST_PATH": MANIFEST_PATH,
        "PROVENANCE_PATH": PROVENANCE_PATH,
        "CONTRACT_PATH": CONTRACT_PATH,
        "VERIFIER_PATH": VERIFIER_PATH,
        "RIG_OBJECT_NAME": RIG_OBJECT_NAME,
        "ARMATURE_DATA_NAME": ARMATURE_DATA_NAME,
        "BIND_PROBE_NAME": BIND_PROBE_NAME,
        "RIGIFY_META_NAME": RIGIFY_META_NAME,
        "RIGIFY_CONTROL_NAME": RIGIFY_CONTROL_NAME,
        "RIGIFY_WIDGET_COLLECTION_NAME": RIGIFY_WIDGET_COLLECTION_NAME,
        "RIGIFY_GENERATED_ID": RIGIFY_GENERATED_ID,
        "BONE_SPECS": BONE_SPECS,
        "SPEC_BY_NAME": SPEC_BY_NAME,
    }
    for name, value in values.items():
        setattr(base, name, value)


def read_frozen_v1_manifest() -> dict[str, object]:
    require(V1_MANIFEST_PATH.is_file(), "Frozen V1 manifest is missing")
    require(sha256(V1_MANIFEST_PATH) == EXPECTED_V1_MANIFEST_SHA256, "Frozen V1 manifest changed")
    require(sha256(V1_BLEND_PATH) == EXPECTED_V1_BLEND_SHA256, "Frozen V1 blend changed")
    require(sha256(V1_FBX_PATH) == EXPECTED_V1_FBX_SHA256, "Frozen V1 FBX changed")
    manifest = json.loads(V1_MANIFEST_PATH.read_text(encoding="utf-8"))
    require(len(manifest["hierarchy"]["bones"]) == V1_BONE_COUNT, "Frozen V1 record count changed")
    return manifest


def validate_static_contract(v1_manifest: dict[str, object]) -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    require("Assets" not in SOURCE_DIR.parts, "V2 rig source must remain outside Unity Assets")
    require(VERIFIER_PATH.is_file(), "V2 round-trip verifier is missing")
    require(len(BONE_SPECS) == 68 and BONE_SPECS[:66] == V1_BONE_SPECS, "V1 prefix was not preserved")
    require(tuple(spec.name for spec in BONE_SPECS[-2:]) == SUPPORT_NAMES, "Support append order changed")
    require(len(HUMANOID_MAP) == 55, "Unity Humanoid map changed")
    require(sum(spec.deform for spec in BONE_SPECS) == 57, "Expected 57 deform joints")
    require(sum(spec.role == "socket" for spec in BONE_SPECS) == 10, "Expected ten sockets")
    require([spec.name for spec in V1_BONE_SPECS] == v1_manifest["hierarchy"]["orderedBoneNames"], "V1 spec order changed")
    for side in ("Left", "Right"):
        support = SPEC_BY_NAME[side + "ElbowSupport"]
        upper = SPEC_BY_NAME[side + "UpperArm"]
        lower = SPEC_BY_NAME[side + "LowerArm"]
        require(support.parent == upper.name and not support.connected, f"{support.name} hierarchy changed")
        require(Vector(support.head) == Vector(lower.head), f"{support.name} pivot changed")
        require(abs((Vector(support.tail) - Vector(support.head)).length - SUPPORT_LENGTH_METERS) <= 1e-7, f"{support.name} length changed")
        require(
            (Vector(support.tail) - Vector(support.head)).normalized().dot(
                (Vector(lower.tail) - Vector(lower.head)).normalized()
            ) >= 1.0 - 1e-6,
            f"{support.name} rest direction changed",
        )


def add_support_constraints(game_rig: bpy.types.Object, control_rig: bpy.types.Object) -> None:
    require(set(SUPPORT_TARGETS.values()).issubset(control_rig.pose.bones.keys()), "Rigify support targets are missing")
    for support_name, target_name in SUPPORT_TARGETS.items():
        pose_bone = game_rig.pose.bones[support_name]
        require(not pose_bone.constraints, f"{support_name} already has a driver")
        rotation = pose_bone.constraints.new("COPY_ROTATION")
        rotation.name = "DCC_Rigify_ElbowMidpointRotation"
        rotation.target = control_rig
        rotation.subtarget = target_name
        rotation.owner_space = "LOCAL"
        rotation.target_space = "LOCAL_OWNER_ORIENT"
        rotation.mix_mode = "REPLACE"
        rotation.influence = SUPPORT_ROTATION_FACTOR
        pose_bone.bone["runtimeDriverAuthority"] = "Unity LateUpdate support-bone driver"
        pose_bone.bone["genericDccProofOnly"] = True
    bpy.context.view_layer.update()


def align_support_rest_bones(game_rig: bpy.types.Object) -> None:
    """Copy each LowerArm edit-bone basis exactly, then shorten its sibling."""

    bpy.context.view_layer.objects.active = game_rig
    game_rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    for side in ("Left", "Right"):
        support = game_rig.data.edit_bones[side + "ElbowSupport"]
        lower = game_rig.data.edit_bones[side + "LowerArm"]
        support.matrix = lower.matrix.copy()
        support.length = SUPPORT_LENGTH_METERS
        support.parent = game_rig.data.edit_bones[side + "UpperArm"]
        support.use_connect = False
    bpy.ops.object.mode_set(mode="OBJECT")


def validate_support_layer(
    game_rig: bpy.types.Object,
    control_rig: bpy.types.Object,
    report: dict[str, object],
) -> None:
    maximum_rest_deviation = 0.0
    for side in ("Left", "Right"):
        support_name = side + "ElbowSupport"
        lower_name = side + "LowerArm"
        support = game_rig.data.bones[support_name]
        lower = game_rig.data.bones[lower_name]
        require((support.parent.name if support.parent else None) == side + "UpperArm", f"{support_name} parent changed")
        require(not support.use_connect and support.use_deform, f"{support_name} flags changed")
        require((support.head_local - lower.head_local).length <= 1e-9, f"{support_name} pivot differs from elbow")
        orientation_error = max(
            abs(support.matrix_local[row][column] - lower.matrix_local[row][column])
            for row in range(3)
            for column in range(3)
        )
        require(orientation_error <= 0.0000005, f"{support_name} rest orientation differs from LowerArm: {orientation_error:.9g}")
        constraints = list(game_rig.pose.bones[support_name].constraints)
        require(len(constraints) == 1 and constraints[0].type == "COPY_ROTATION", f"{support_name} driver set changed")
        constraint = constraints[0]
        require(constraint.target == control_rig and constraint.subtarget == SUPPORT_TARGETS[support_name], f"{support_name} target changed")
        require(constraint.owner_space == "LOCAL" and constraint.target_space == "LOCAL_OWNER_ORIENT", f"{support_name} spaces changed")
        require(constraint.mix_mode == "REPLACE" and abs(constraint.influence - 0.5) <= 1e-9, f"{support_name} factor changed")
        evaluated = game_rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
        deviation = (evaluated.pose.bones[support_name].head - Vector(SPEC_BY_NAME[support_name].head)).length
        maximum_rest_deviation = max(maximum_rest_deviation, deviation)
        require(deviation <= 0.00002, f"{support_name} shifts in neutral")
    actual_constraints = sum(len(pose_bone.constraints) for pose_bone in game_rig.pose.bones)
    require(actual_constraints == 60, f"Expected 60 DCC constraints, found {actual_constraints}")
    report.update({
        "deterministicRandomSeed": "session-humanoid-rig-v2-rigify-blender-5.2",
        "driverConstraintCount": 60,
        "drivenGameBoneCount": 58,
        "drivenBendSupportBoneCount": 2,
        "bendSupportTargetMap": SUPPORT_TARGETS,
        "bendSupportRotationFactor": SUPPORT_ROTATION_FACTOR,
        "bendSupportDriver": "COPY_ROTATION LOCAL <- LOCAL_OWNER_ORIENT REPLACE",
        "bendSupportRuntimeAuthority": "Unity LateUpdate driver; DCC constraints and exported support curves are Generic proof only",
        "maximumBendSupportDrivenRestJointDeviationMeters": maximum_rest_deviation,
        "maximumBendSupportVsLowerArmRestOrientationComponentError": max(
            max(
                abs(
                    game_rig.data.bones[side + "ElbowSupport"].matrix_local[row][column]
                    - game_rig.data.bones[side + "LowerArm"].matrix_local[row][column]
                )
                for row in range(3) for column in range(3)
            )
            for side in ("Left", "Right")
        ),
    })


def ordered_records_bytes(records: list[dict[str, object]]) -> bytes:
    return (json.dumps(records, indent=2, sort_keys=True) + "\n").encode("utf-8")


def assert_v1_prefix_records(rig: bpy.types.Object, v1_manifest: dict[str, object]) -> tuple[list[dict[str, object]], str]:
    records = [base.bone_contract_record(rig, spec) for spec in BONE_SPECS]
    expected = v1_manifest["hierarchy"]["bones"]
    require(records[:V1_BONE_COUNT] == expected, "V2 changed one or more frozen V1 joint records")
    expected_bytes = ordered_records_bytes(expected)
    actual_bytes = ordered_records_bytes(records[:V1_BONE_COUNT])
    require(actual_bytes == expected_bytes, "V2 V1-prefix record bytes changed")
    return records, bytes_sha256(actual_bytes)


def write_contract(rig: bpy.types.Object, v1_record_hash: str) -> None:
    rows = []
    for spec in BONE_SPECS[-2:]:
        bone = rig.data.bones[spec.name]
        rows.append(
            f"| `{spec.name}` | `{spec.parent}` | `{tuple(round(v, 8) for v in bone.head_local)}` | "
            f"`{tuple(round(v, 8) for v in bone.tail_local)}` | `{spec.role}` |"
        )
    CONTRACT_PATH.write_text(
        f"""# Session Humanoid Rig V2 contract

Status: first-party bend-support DCC source; not integrated into Unity.

V2 preserves the frozen V1 armature, names, parent records, rest matrices, and
manifest order for joints 0-65. Their canonical ordered-record SHA-256 is
`{v1_record_hash}`. It then appends exactly two deforming elbow support siblings.

- Total exported joints: 68 = frozen V1 66 + two bend supports.
- Unity Humanoid mapping remains the same 55 conventional joints. The supports
  are unmapped auxiliary deform joints; ten sockets remain non-deforming.
- Each support shares its LowerArm elbow pivot and rest orientation, is parented
  directly to UpperArm, is disconnected, and displays 0.080 m toward the wrist.
- Rigify authoring uses one 0.5-influence local owner-oriented rotation driver
  per support. Animation baking evaluates the midpoint analytically.
- Exported support animation curves are DCC/Generic proof only. For Humanoid,
  native, or fallback playback, a deterministic Unity LateUpdate driver is the
  authoritative support motion. Unity integration is deliberately out of scope.
- Unity import must either disable Optimize Game Objects or add both exact
  support hierarchy paths to `ModelImporter.extraExposedTransformPaths`. The
  future driver caches rest-local rotations and overwrites support rotations
  after Animator/OnAnimatorIK; Humanoid retargeting alone is not authoritative.
- Blender meters, +Z up, -Y forward; FBX -Z forward/+Y up; identity root scale.

| Bone | Parent | Head m | Tail m | Role |
|---|---|---:|---:|---|
""" + "\n".join(rows) + "\n",
        encoding="utf-8",
    )


def write_provenance() -> None:
    PROVENANCE_PATH.write_text(
        f"""# Session Humanoid Rig V2 provenance

Status: **first-party technical rig extension; not integrated into Unity**.

The frozen first-party `SessionHumanoidRigV1` contract is used only as a local
numeric construction library. Its manifest, blend, and FBX SHA-256 values are
`{EXPECTED_V1_MANIFEST_SHA256}`, `{EXPECTED_V1_BLEND_SHA256}`, and
`{EXPECTED_V1_FBX_SHA256}`. V2 never opens for write or mutates those artifacts.

The only new shipping hierarchy records are `LeftElbowSupport` and
`RightElbowSupport`. Their dimensions, hierarchy, DCC drivers, deterministic
animation-bake rule, and Unity runtime-authority boundary are authored by this
project. No Quaternius, Blender Foundation Human Base, legacy skeleton, or
legacy clip is imported or sampled. Blender 5.2 bundled Rigify remains a
non-exported authoring layer; no control, mechanism, ORG, DEF, metarig, or widget
bone is present in the V2 FBX.
""",
        encoding="utf-8",
    )


def patch_manifest(
    rig: bpy.types.Object,
    v1_manifest: dict[str, object],
    all_records: list[dict[str, object]],
    v1_record_hash: str,
) -> None:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    hierarchy = manifest["hierarchy"]
    require(hierarchy["bones"] == all_records, "Base manifest writer changed V2 bone records")
    hierarchy.update({
        "exportedBoneCount": 68,
        "unityHumanoidDeformBoneCount": 55,
        "additionalDeformBoneCount": 2,
        "totalDeformBoneCount": 57,
        "bendSupportBoneCount": 2,
        "bendSupportBoneNames": list(SUPPORT_NAMES),
        "v1PrefixBoneCount": 66,
        "v1PrefixExactOrderedRecordEquality": True,
        "v1PrefixOrderedRecordsSerializedSha256": v1_record_hash,
    })
    manifest["ownership"] = "first-party V1 game hierarchy preserved exactly plus first-party elbow bend supports; Rigify is non-exported DCC tooling"
    manifest["v1Compatibility"] = {
        "frozenAssetId": v1_manifest["assetId"],
        "frozenManifestPath": str(V1_MANIFEST_PATH.relative_to(PROJECT_ROOT)),
        "frozenManifestSha256": EXPECTED_V1_MANIFEST_SHA256,
        "frozenBlendSha256": EXPECTED_V1_BLEND_SHA256,
        "frozenFbxSha256": EXPECTED_V1_FBX_SHA256,
        "first66BoneRecordsUnchanged": True,
        "first66BoneRecordsOrderUnchanged": True,
        "first66OrderedRecordsSerializedSha256": v1_record_hash,
        "v1FilesWrittenByBuild": False,
    }
    manifest["bendSupportContract"] = {
        "names": list(SUPPORT_NAMES),
        "parents": {name: name.replace("ElbowSupport", "UpperArm") for name in SUPPORT_NAMES},
        "lowerArmSiblings": True,
        "headEqualsLowerArmHead": True,
        "restOrientationEqualsLowerArm": True,
        "displayLengthMeters": SUPPORT_LENGTH_METERS,
        "displayLengthScope": "DCC source only; FBX leaf-bone tail/display length is non-semantic",
        "displayLengthFbxSemantic": False,
        "tailDirection": "toward corresponding wrist",
        "deform": True,
        "connected": False,
        "unityHumanoidMapped": False,
        "dccTargetMap": SUPPORT_TARGETS,
        "dccRotationFactor": SUPPORT_ROTATION_FACTOR,
        "animationBakeRule": "shortest-arc slerp(identity local pose, corresponding LowerArm local pose, 0.5); rest local translation; unit scale",
        "exportedCurvesAuthority": "DCC and Generic proof only",
        "runtimeAuthority": "Unity LateUpdate driver for Humanoid, native, and prototype fallback playback",
        "fbxVerification": "parent, elbow pivot, imported rest basis, and sampled pose equivalence; never synthesized leaf display length",
    }
    manifest["unityRuntimeSupportDriverContract"] = {
        "implementationStatus": "NOT_IMPLEMENTED_IN_THIS_DCC_TASK",
        "authority": "LateUpdate driver overwrites auxiliary support local rotations after Animator and OnAnimatorIK for Humanoid, native, and prototype fallback playback",
        "humanoidRetargetingAloneIsAuthoritative": False,
        "cachedInputs": ["support rest-local rotation", "corresponding LowerArm rest-local rotation"],
        "perFrameRule": "supportLocal = supportRestLocal * shortestArcSlerp(identity, inverse(lowerRestLocal) * lowerCurrentLocal, 0.5)",
        "executionOrder": "reviewed component/script order after every Animator and IK writer; LateUpdate is the minimum lifecycle boundary",
        "optimizeGameObjectsPrecondition": {
            "allowedModes": [
                "Optimize Game Objects disabled",
                "both exact support transform paths retained in ModelImporter.extraExposedTransformPaths",
            ],
            "requiredExtraExposedTransformPathsWhenOptimized": [
                "Root/Hips/Spine/Chest/UpperChest/LeftShoulder/LeftUpperArm/LeftElbowSupport",
                "Root/Hips/Spine/Chest/UpperChest/RightShoulder/RightUpperArm/RightElbowSupport",
            ],
            "mustBeVerifiedAfterUnityImport": True,
        },
    }
    manifest["exportContract"]["note"] = "Skeleton-only 68-joint FBX: frozen 66-joint V1 prefix plus two unmapped bend supports; Unity Humanoid map remains 55 joints and sockets remain explicitly non-deform."
    manifest["sourceFiles"] = {
        str(SCRIPT_PATH.relative_to(PROJECT_ROOT)): sha256(SCRIPT_PATH),
        str(VERIFIER_PATH.relative_to(PROJECT_ROOT)): sha256(VERIFIER_PATH),
        str(CONTRACT_PATH.relative_to(PROJECT_ROOT)): sha256(CONTRACT_PATH),
        str(PROVENANCE_PATH.relative_to(PROJECT_ROOT)): sha256(PROVENANCE_PATH),
        str(Path(base.__file__).resolve().relative_to(PROJECT_ROOT)): sha256(Path(base.__file__).resolve()),
        str(base.REST_FIT_ARTIFACT_PATH.relative_to(PROJECT_ROOT)): sha256(base.REST_FIT_ARTIFACT_PATH),
        str(base.REST_FIT_BUILD_MANIFEST_PATH.relative_to(PROJECT_ROOT)): sha256(base.REST_FIT_BUILD_MANIFEST_PATH),
        str(V1_MANIFEST_PATH.relative_to(PROJECT_ROOT)): EXPECTED_V1_MANIFEST_SHA256,
    }
    manifest["outputHashes"] = {
        BLEND_PATH.name: sha256(BLEND_PATH),
        FBX_PATH.name: sha256(FBX_PATH),
        PROVENANCE_PATH.name: sha256(PROVENANCE_PATH),
    }
    manifest["approvalBoundary"]["nextStep"] = "Bind the accepted body variants to V2 and rerun the unchanged elbow deformation thresholds; Unity runtime-driver implementation is a separate reviewed task."
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> None:
    v1_manifest = read_frozen_v1_manifest()
    validate_static_contract(v1_manifest)
    configure_base_module()
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    base.reset_scene()
    rig = base.create_armature()
    align_support_rest_bones(rig)
    probe = base.create_bind_probe(rig)
    for modifier in probe.modifiers:
        if modifier.type == "ARMATURE":
            modifier.name = "SessionHumanoidRigV2_Bind"
    bind_probe_report = base.validate_source_rig(rig, probe)
    metarig, control_rig, widgets, dcc_report = base.create_rigify_authoring_layer(rig)
    # Validate the unchanged V1 Rigify bridge first, then append and validate the
    # V2-only support bridge. This prevents the extension from weakening V1 gates.
    base.validate_dcc_authoring_layer(rig, metarig, control_rig, widgets, dcc_report)
    add_support_constraints(rig, control_rig)
    validate_support_layer(rig, control_rig, dcc_report)
    all_records, v1_record_hash = assert_v1_prefix_records(rig, v1_manifest)
    write_contract(rig, v1_record_hash)
    write_provenance()
    base.export_fbx(rig)
    probe.hide_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), check_existing=False)
    base.write_manifest(rig, bind_probe_report, dcc_report)
    patch_manifest(rig, v1_manifest, all_records, v1_record_hash)
    # Recheck the immutable ancestry after every output write.
    read_frozen_v1_manifest()
    print("SESSION_HUMANOID_RIG_V2_BUILD_OK")
    print("BLEND", BLEND_PATH)
    print("FBX", FBX_PATH)
    print("BONES 68 DEFORM 57 HUMANOID 55 SUPPORTS 2 SOCKETS 10")
    print("V1_PREFIX_RECORDS_SHA256", v1_record_hash)


if __name__ == "__main__":
    main()
