#!/usr/bin/env python3
"""Shared, source-level contract for SessionHumanoidBaseV2 binding checks.

This module contains no build entry point.  The builder and the independent FBX
round-trip verifier import it so the accepted limits and the MPFB-to-project bone
map cannot drift between the two commands.
"""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import json
import math
from pathlib import Path
import struct
from typing import Callable, Iterable

import bmesh
import bpy
from mathutils import Matrix, Quaternion, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import intersect_ray_tri


PROJECT_ROOT = Path(__file__).resolve().parents[3]
REPOSITORY_ROOT = PROJECT_ROOT.parent
BASE_INPUT_SOURCE_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidBaseV1"
BASE_SOURCE_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidBaseV2"
BASE_OUTPUT_DIR = BASE_SOURCE_DIR / "Outputs"
RIG_OUTPUT_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidRigV2/Outputs"
ANIMATION_OUTPUT_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidAnimationProofV2/Outputs"
BOUND_BLEND = BASE_OUTPUT_DIR / "SessionHumanoidBaseV2_Bound.blend"
BINDING_MANIFEST = BASE_OUTPUT_DIR / "SessionHumanoidBaseV2_BindingManifest.json"
BINDING_REPORT = (
    REPOSITORY_ROOT
    / "outputs/art/blender/session-humanoid-base-v2-bound/SessionHumanoidBaseV2BindingReport.json"
)
ROUNDTRIP_REPORT = (
    REPOSITORY_ROOT
    / "outputs/art/blender/session-humanoid-base-v2-bound/SessionHumanoidBaseV2FbxRoundTrip.json"
)
EVIDENCE_DIR = REPOSITORY_ROOT / "outputs/captures/20260820-session-humanoid-base-v2-bound"
STAGING_ROOT = REPOSITORY_ROOT / "outputs/art/blender/session-humanoid-base-v2-bound/staging"
STAGED_PACKAGE_DIR = STAGING_ROOT / "package"
STAGED_BLEND = STAGED_PACKAGE_DIR / "SessionHumanoidBaseV2_Bound.blend"
STAGED_BUILD_MANIFEST = STAGING_ROOT / "SessionHumanoidBaseV2_StagedBuildManifest.json"
PROMOTION_TEMP_DIR = BASE_SOURCE_DIR / ".SessionHumanoidBaseV2-Outputs-promotion"
RIG_FBX = RIG_OUTPUT_DIR / "SessionHumanoidRigV2.fbx"
RIG_MANIFEST = RIG_OUTPUT_DIR / "SessionHumanoidRigV2.manifest.json"
ANIMATION_FBX = ANIMATION_OUTPUT_DIR / "SessionHumanoidAnimationProofV2.fbx"
ANIMATION_MANIFEST = ANIMATION_OUTPUT_DIR / "SessionHumanoidAnimationProofV2.manifest.json"
V1_CHECKPOINT_BLEND = BASE_INPUT_SOURCE_DIR / "SessionHumanoidBaseV1_MPFB_ClayCheckpoint.blend"
V1_BODY_BUILD_MANIFEST = BASE_INPUT_SOURCE_DIR / "Outputs/SessionHumanoidBaseV1_BuildManifest.json"
V1_RIG_FIT_LANDMARKS = BASE_INPUT_SOURCE_DIR / "Outputs/SessionHumanoidBaseV1_RigFitLandmarks.json"
V1_BODY_INTAKE_REPORT = (
    REPOSITORY_ROOT
    / "outputs/art/blender/session-humanoid-base-v1-mpfb/SessionHumanoidBaseV1MpfbIntake.json"
)
V1_BODY_INTAKE_BUILDER = PROJECT_ROOT / "Tools/Art/Blender/build_session_humanoid_base_v1.py"
V2_BINDING_BUILDER = PROJECT_ROOT / "Tools/Art/Blender/bind_session_humanoid_base_v2.py"
BOW_DRAW_POSE_SNAPSHOT = PROJECT_ROOT / "Tools/Art/Blender/session_humanoid_base_v2_bowdraw_pose_snapshot.json"

VARIANTS = ("Masculine", "Feminine")
BOUND_MESH_NAMES = {
    variant: f"SessionHumanoidBaseV2_{variant}_BoundSource" for variant in VARIANTS
}
BOUND_FBX_PATHS = {
    variant: BASE_OUTPUT_DIR / f"SessionHumanoidBaseV2_{variant}_Bound.fbx"
    for variant in VARIANTS
}
STAGED_FBX_PATHS = {
    variant: STAGED_PACKAGE_DIR / f"SessionHumanoidBaseV2_{variant}_Bound.fbx"
    for variant in VARIANTS
}

EXPECTED_VERTICES = 13380
EXPECTED_TRIANGLES = 26756
EXPECTED_RIG_JOINTS = 68
EXPECTED_DEFORM_GROUPS = 57
# Independently audited, cross-seed deterministic V2 freeze.  The builder and
# verifier both reject any byte drift before reading the artifacts.
EXPECTED_RIG_FBX_SHA256 = "beb653aba9873141418d95b38cfda2151c1852e2e7b0df9aba2eeaf85c9eed66"
EXPECTED_RIG_MANIFEST_SHA256 = "7f8468fea292a1416c5d8ac697fd24652bba84e77808187647cf4eb5f3d3a6e6"
EXPECTED_LANDMARK_ARTIFACT_SHA256 = "e55a921a1f3f18aa1c08bcbb98e31317a6c6ab2fc710fee36ea37c2c54636e79"
EXPECTED_V1_FIRST66_ORDERED_RECORDS_SHA256 = "beec11c8152c7d69159babd726288c45fb63460ef61b95db88f5c5d26e386d33"
EXPECTED_V1_CHECKPOINT_SHA256 = "fb0615e54a9c02a8e1ab9005a8ec2eeeac10c645cf064844324b120c5791fb63"
EXPECTED_V1_BODY_BUILD_MANIFEST_SHA256 = "97c6828ed971ee459dff1c1d66275029a3219ce175c53a8be8f6febcea01d4c2"
EXPECTED_V1_BODY_INTAKE_REPORT_SHA256 = "30852bdb824f7c93789955bb5749d663ff20c5850b7a146e28f514da5bdf5f95"
EXPECTED_V1_BODY_INTAKE_BUILDER_SHA256 = "1efa69a84f1bee5fd6306533f65ca821c512320e01f36b0868469c9d0adcaeec"
EXPECTED_MPFb_WEIGHTS_SHA256 = "9f4a773e74ce5ba08415b4f30070338e9f75bae5971b5075b10cd6b9caa58473"
EXPECTED_MPFb_TPOSE_SHA256 = "fefc2969c3e16049803e1ffea2a7cf9b7d98ec5eaeef42ca38dc6184241aacf8"
EXPECTED_MPFb_BASE_OBJ_SHA256 = "8e761e6624b8f54536409135d1636da63b32486a90d4897f84e121d144f6fb4c"
EXPECTED_ANIMATION_FBX_SHA256 = "a0c88e7ca75e94e20227829846e052b66e8a31332eee8d44c6990187f2fa1fe7"
EXPECTED_ANIMATION_MANIFEST_SHA256 = "eea8ce8aa45f198fc4b54b80cf88d5f71c78e165897c3002df698e0c3783a043"
EXPECTED_BOW_DRAW_POSE_SNAPSHOT_SHA256 = "c7c9b9ca787fb57041ec49c6ad041ebdad486a5a0e14edf35f2c98e416ea4e08"

MAX_INFLUENCES = 4
WEIGHT_SUM_MIN = 0.999
WEIGHT_SUM_MAX = 1.001
OPPOSITE_SIDE_DEADBAND_METERS = 0.003
MIN_TRIANGLE_AREA_RATIO = 0.15
MIN_EDGE_LENGTH_RATIO = 0.35
MAX_EDGE_LENGTH_RATIO = 2.25
MIN_CROSS_SECTION_RETENTION = 0.60
MAX_MIRROR_ERROR_METERS = 0.002
MAX_REST_MIRROR_ERROR_METERS = 0.005
MAX_PENETRATION_METERS = 0.002
MAX_FINGERTIP_MARKER_DRIFT_METERS = 0.005
MAX_SOLE_BELOW_GROUND_METERS = 0.002
GROUND_CONTACT_TOLERANCE_METERS = 0.003
MIN_IDLE_SOLE_CONTACT_FRACTION = 0.95
MAX_PLANTED_MARKER_SLIDE_METERS = 0.010
MIN_LOCOMOTION_GROUNDED_FRACTION = 0.70
ROUNDTRIP_VERTEX_TOLERANCE_METERS = 0.0005
ROUNDTRIP_BONE_TRANSLATION_TOLERANCE_METERS = 0.0001
ROUNDTRIP_BONE_ROTATION_TOLERANCE_DEGREES = 0.05


MPFB_TO_SESSION = {
    "pelvis": "Hips",
    "spine_01": "Spine",
    "spine_02": "Chest",
    "spine_03": "UpperChest",
    "neck_01": "Neck",
    "head": "Head",
    "clavicle_l": "LeftShoulder",
    "upperarm_l": "LeftUpperArm",
    "lowerarm_l": "LeftLowerArm",
    "hand_l": "LeftHand",
    "thumb_01_l": "LeftThumbProximal",
    "thumb_02_l": "LeftThumbIntermediate",
    "thumb_03_l": "LeftThumbDistal",
    "index_01_l": "LeftIndexProximal",
    "index_02_l": "LeftIndexIntermediate",
    "index_03_l": "LeftIndexDistal",
    "middle_01_l": "LeftMiddleProximal",
    "middle_02_l": "LeftMiddleIntermediate",
    "middle_03_l": "LeftMiddleDistal",
    "ring_01_l": "LeftRingProximal",
    "ring_02_l": "LeftRingIntermediate",
    "ring_03_l": "LeftRingDistal",
    "pinky_01_l": "LeftLittleProximal",
    "pinky_02_l": "LeftLittleIntermediate",
    "pinky_03_l": "LeftLittleDistal",
    "thigh_l": "LeftUpperLeg",
    "calf_l": "LeftLowerLeg",
    "foot_l": "LeftFoot",
    "ball_l": "LeftToes",
    "clavicle_r": "RightShoulder",
    "upperarm_r": "RightUpperArm",
    "lowerarm_r": "RightLowerArm",
    "hand_r": "RightHand",
    "thumb_01_r": "RightThumbProximal",
    "thumb_02_r": "RightThumbIntermediate",
    "thumb_03_r": "RightThumbDistal",
    "index_01_r": "RightIndexProximal",
    "index_02_r": "RightIndexIntermediate",
    "index_03_r": "RightIndexDistal",
    "middle_01_r": "RightMiddleProximal",
    "middle_02_r": "RightMiddleIntermediate",
    "middle_03_r": "RightMiddleDistal",
    "ring_01_r": "RightRingProximal",
    "ring_02_r": "RightRingIntermediate",
    "ring_03_r": "RightRingDistal",
    "pinky_01_r": "RightLittleProximal",
    "pinky_02_r": "RightLittleIntermediate",
    "pinky_03_r": "RightLittleDistal",
    "thigh_r": "RightUpperLeg",
    "calf_r": "RightLowerLeg",
    "foot_r": "RightFoot",
    "ball_r": "RightToes",
}

ZERO_INFLUENCE_FACE_GROUPS = ("Jaw", "LeftEye", "RightEye")
ELBOW_SUPPORT_GROUPS = ("LeftElbowSupport", "RightElbowSupport")
SESSION_DEFORM_GROUPS = tuple(dict.fromkeys((*MPFB_TO_SESSION.values(), *ZERO_INFLUENCE_FACE_GROUPS, *ELBOW_SUPPORT_GROUPS)))
ELBOW_SUPPORT_ROTATION_FACTOR = 0.5
SUPPORT_HEAD_MATCH_TOLERANCE_METERS = 1e-6
SUPPORT_LENGTH_METERS = 0.080
SUPPORT_LENGTH_TOLERANCE_METERS = 1e-5
SUPPORT_LOCAL_ROTATION_TOLERANCE_DEGREES = 0.05
ELBOW_SUPPORT_ENVELOPE_CONTRACT = {
    "zeroAtOrBelow": 0.20,
    "zeroAtOrAbove": 0.80,
    "smoothFadeToRigidCore": [0.20, 0.42],
    "rigidCoreInclusive": [0.42, 0.58],
    "smoothFadeFromRigidCore": [0.58, 0.80],
    "fadeFunction": "smoothstep",
}


def elbow_support_envelope(geodesic_fraction: float) -> float:
    value = max(0.0, min(1.0, geodesic_fraction))
    if value <= 0.20 or value >= 0.80:
        return 0.0
    if value < 0.42:
        parameter = (value - 0.20) / 0.22
        return parameter * parameter * (3.0 - (2.0 * parameter))
    if value <= 0.58:
        return 1.0
    parameter = (0.80 - value) / 0.22
    return parameter * parameter * (3.0 - (2.0 * parameter))

CLIPS = {
    "CombatIdle": (1, 49, True),
    "LocomotionForward": (1, 25, True),
    "Hit": (1, 18, False),
    "Death": (1, 42, False),
    "Interrupt": (1, 16, False),
    "HunterBowReady": (1, 25, True),
    "HunterBowDraw": (1, 20, False),
    "HunterBowHold": (1, 25, True),
    "HunterBowLoose": (1, 10, False),
    "HunterBowRecovery": (1, 18, False),
}


@dataclass(frozen=True)
class StaticPoseSpec:
    name: str
    symmetric: bool
    configure: Callable[[bpy.types.Object], None]


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def canonical_json_sha256(value: object) -> str:
    payload = json.dumps(value, sort_keys=True, separators=(",", ":"), ensure_ascii=False)
    return hashlib.sha256(payload.encode("utf-8")).hexdigest()


def v1_body_provenance_report(
    rig_manifest: dict[str, object],
    landmark_artifact_path: Path,
) -> dict[str, object]:
    """Enforce the complete accepted-body chain before binding or promotion."""
    expected = {
        "checkpointBlend": (V1_CHECKPOINT_BLEND, EXPECTED_V1_CHECKPOINT_SHA256),
        "bodyBuildManifest": (V1_BODY_BUILD_MANIFEST, EXPECTED_V1_BODY_BUILD_MANIFEST_SHA256),
        "bodyIntakeReport": (V1_BODY_INTAKE_REPORT, EXPECTED_V1_BODY_INTAKE_REPORT_SHA256),
        "intakeBuilder": (V1_BODY_INTAKE_BUILDER, EXPECTED_V1_BODY_INTAKE_BUILDER_SHA256),
        "rigFitLandmarks": (landmark_artifact_path, EXPECTED_LANDMARK_ARTIFACT_SHA256),
    }
    actual = {}
    for label, (path, expected_hash) in expected.items():
        require(path.is_file(), f"Accepted V1 provenance input is missing: {path}")
        actual_hash = sha256(path)
        require(actual_hash == expected_hash, f"Accepted V1 {label} changed: {actual_hash}")
        actual[label] = {
            "path": str(path.relative_to(REPOSITORY_ROOT)),
            "sha256": actual_hash,
        }

    build_manifest = json.loads(V1_BODY_BUILD_MANIFEST.read_text(encoding="utf-8"))
    intake_report = json.loads(V1_BODY_INTAKE_REPORT.read_text(encoding="utf-8"))
    landmark_artifact = json.loads(landmark_artifact_path.read_text(encoding="utf-8"))
    require(build_manifest["checkpointBlend"]["sha256"] == EXPECTED_V1_CHECKPOINT_SHA256, "V1 build manifest checkpoint pin changed")
    require(build_manifest["bodyIntakeReport"]["sha256"] == EXPECTED_V1_BODY_INTAKE_REPORT_SHA256, "V1 build manifest intake-report pin changed")
    require(build_manifest["authoringSources"]["builderSha256"] == EXPECTED_V1_BODY_INTAKE_BUILDER_SHA256, "V1 build manifest builder pin changed")
    require(intake_report["checkpointBlendSha256"] == EXPECTED_V1_CHECKPOINT_SHA256, "V1 intake report checkpoint pin changed")
    require(intake_report["generator"]["scriptSha256"] == EXPECTED_V1_BODY_INTAKE_BUILDER_SHA256, "V1 intake report builder pin changed")
    require(intake_report["stableRigFitLandmarkArtifact"]["sha256"] == EXPECTED_LANDMARK_ARTIFACT_SHA256, "V1 intake report landmark pin changed")
    require(intake_report["mpfb"]["bundledBaseObjSha256"] == EXPECTED_MPFb_BASE_OBJ_SHA256, "V1 intake report MPFB base pin changed")
    require(landmark_artifact["generator"]["sourceScriptSha256"] == EXPECTED_V1_BODY_INTAKE_BUILDER_SHA256, "V1 landmark generator pin changed")

    rest_fit = rig_manifest["restFit"]
    require(rest_fit["inputArtifactSha256"] == EXPECTED_LANDMARK_ARTIFACT_SHA256, "V2 rig landmark provenance changed")
    require(rest_fit["originatingCheckpointBlendSha256"] == EXPECTED_V1_CHECKPOINT_SHA256, "V2 rig checkpoint provenance changed")
    require(rest_fit["perBuildProvenanceManifestSha256"] == EXPECTED_V1_BODY_BUILD_MANIFEST_SHA256, "V2 rig build-manifest provenance changed")
    require(rest_fit["originatingBodyIntakeReportSha256"] == EXPECTED_V1_BODY_INTAKE_REPORT_SHA256, "V2 rig intake-report provenance changed")
    return {
        "status": "PASS",
        "acceptedInputs": actual,
        "crossReferencesVerified": [
            "V1 build manifest -> checkpoint/intake report/builder",
            "V1 intake report -> checkpoint/builder/landmark artifact",
            "V1 landmark artifact -> builder",
            "V2 rig restFit -> checkpoint/build manifest/intake report/landmark artifact",
        ],
    }


def near_zero(values: Iterable[float], tolerance: float = 1e-5) -> bool:
    return max(abs(value) for value in values) <= tolerance


def near_one(values: Iterable[float], tolerance: float = 1e-5) -> bool:
    return max(abs(value - 1.0) for value in values) <= tolerance


def topology_sha256(obj: bpy.types.Object) -> str:
    digest = hashlib.sha256()
    mesh = obj.data
    digest.update(struct.pack("<III", len(mesh.vertices), len(mesh.loops), len(mesh.polygons)))
    for loop in mesh.loops:
        digest.update(struct.pack("<I", loop.vertex_index))
    for polygon in mesh.polygons:
        digest.update(struct.pack("<II", polygon.loop_start, polygon.loop_total))
    return digest.hexdigest()


def uv_loop_sha256(obj: bpy.types.Object) -> str:
    require(len(obj.data.uv_layers) == 1, f"{obj.name} must have exactly one UV layer")
    layer = obj.data.uv_layers.active
    require(layer is not None, f"{obj.name} has no active UV layer")
    digest = hashlib.sha256()
    digest.update(layer.name.encode("utf-8"))
    for datum in layer.data:
        digest.update(struct.pack("<ff", datum.uv.x, datum.uv.y))
    return digest.hexdigest()


def position_sha256(obj: bpy.types.Object) -> str:
    digest = hashlib.sha256()
    for vertex in obj.data.vertices:
        digest.update(struct.pack("<fff", vertex.co.x, vertex.co.y, vertex.co.z))
    return digest.hexdigest()


def triangle_indices(mesh: bpy.types.Mesh) -> list[tuple[int, int, int]]:
    mesh.calc_loop_triangles()
    return [tuple(item.vertices) for item in mesh.loop_triangles]


def unique_edges(triangles: Iterable[tuple[int, int, int]]) -> list[tuple[int, int]]:
    edges: set[tuple[int, int]] = set()
    for a, b, c in triangles:
        for first, second in ((a, b), (b, c), (c, a)):
            edges.add((min(first, second), max(first, second)))
    return sorted(edges)


def evaluated_positions(obj: bpy.types.Object) -> list[Vector]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    matrix = evaluated.matrix_world
    return [matrix @ vertex.co for vertex in evaluated.data.vertices]


def weight_rows(obj: bpy.types.Object) -> list[list[tuple[str, float]]]:
    names = {group.index: group.name for group in obj.vertex_groups}
    return [
        sorted((names[item.group], float(item.weight)) for item in vertex.groups)
        for vertex in obj.data.vertices
    ]


def weight_audit(
    obj: bpy.types.Object,
    allowed_groups: list[str],
    deadband: float = OPPOSITE_SIDE_DEADBAND_METERS,
) -> dict[str, object]:
    group_names = [group.name for group in obj.vertex_groups]
    require(group_names == allowed_groups, f"{obj.name} group order/set changed")
    rows = weight_rows(obj)
    influence_histogram: dict[str, int] = {}
    maximum_influences = 0
    minimum_sum = float("inf")
    maximum_sum = 0.0
    forbidden_names: set[str] = set()
    opposite_side: list[dict[str, object]] = []
    unweighted: list[int] = []
    non_finite: list[int] = []
    for vertex, row in zip(obj.data.vertices, rows):
        positive = [(name, weight) for name, weight in row if weight > 1e-8]
        count = len(positive)
        influence_histogram[str(count)] = influence_histogram.get(str(count), 0) + 1
        maximum_influences = max(maximum_influences, count)
        total = sum(weight for _name, weight in positive)
        minimum_sum = min(minimum_sum, total)
        maximum_sum = max(maximum_sum, total)
        if count == 0:
            unweighted.append(vertex.index)
        if not math.isfinite(total) or any(not math.isfinite(weight) for _name, weight in positive):
            non_finite.append(vertex.index)
        for name, weight in positive:
            if name not in allowed_groups:
                forbidden_names.add(name)
            if vertex.co.x > deadband and name.startswith("Right"):
                opposite_side.append({"vertex": vertex.index, "x": vertex.co.x, "group": name, "weight": weight})
            if vertex.co.x < -deadband and name.startswith("Left"):
                opposite_side.append({"vertex": vertex.index, "x": vertex.co.x, "group": name, "weight": weight})
    require(not unweighted, f"{obj.name} has unweighted vertices")
    require(not non_finite, f"{obj.name} has non-finite weights")
    require(not forbidden_names, f"{obj.name} has forbidden groups: {sorted(forbidden_names)}")
    require(maximum_influences <= MAX_INFLUENCES, f"{obj.name} exceeds four weights")
    require(minimum_sum >= WEIGHT_SUM_MIN and maximum_sum <= WEIGHT_SUM_MAX, f"{obj.name} weights are not normalized")
    require(not opposite_side, f"{obj.name} has opposite-side weights outside the {deadband}m seam band")
    zero_face_counts = {
        name: sum(1 for row in rows for group, weight in row if group == name and weight > 1e-8)
        for name in ZERO_INFLUENCE_FACE_GROUPS
    }
    require(not any(zero_face_counts.values()), f"{obj.name} facial child groups unexpectedly carry weights")
    return {
        "allowedGroupCount": len(group_names),
        "allowedGroups": group_names,
        "maximumInfluences": maximum_influences,
        "influenceHistogram": influence_histogram,
        "minimumWeightSum": minimum_sum,
        "maximumWeightSum": maximum_sum,
        "unweightedVertices": 0,
        "nonFiniteWeights": 0,
        "forbiddenGroups": [],
        "oppositeSideWeightsOutsideSeamBand": 0,
        "oppositeSideSeamDeadbandMeters": deadband,
        "zeroInfluenceFacialChildGroups": zero_face_counts,
        "facialSurfaceWeightPolicy": "accepted game_engine head weights remain on Head; Jaw/eyes are hierarchy-only until first-party facial deformation is authored",
    }


def elbow_support_rest_report(
    rig: bpy.types.Object,
    ordered_bone_names: list[str],
    *,
    imported_fbx_semantics: bool,
) -> dict[str, object]:
    require(ordered_bone_names[-2:] == list(ELBOW_SUPPORT_GROUPS), f"V2 support append order changed: {ordered_bone_names[-2:]}")
    require(len(ordered_bone_names) == EXPECTED_RIG_JOINTS, f"V2 ordered bone count is {len(ordered_bone_names)}")
    per_side = {}
    for side in ("Left", "Right"):
        upper = rig.data.bones[f"{side}UpperArm"]
        lower = rig.data.bones[f"{side}LowerArm"]
        support = rig.data.bones[f"{side}ElbowSupport"]
        require(support.parent == upper, f"{support.name} parent is not {upper.name}")
        require(support.use_deform, f"{support.name} must deform")
        require(support.get("sessionRole") == "bend_support", f"{support.name} lost its bend-support role")
        head_error = (support.head_local - lower.head_local).length
        length_error = abs(support.length - SUPPORT_LENGTH_METERS)
        orientation_error = quaternion_error_degrees(
            support.matrix_local.to_quaternion(),
            lower.matrix_local.to_quaternion(),
        )
        lower_direction = (lower.tail_local - lower.head_local).normalized()
        support_direction = (support.tail_local - support.head_local).normalized()
        direction_error = math.degrees(math.acos(max(-1.0, min(1.0, lower_direction.dot(support_direction)))))
        basis_component_error = max(
            abs(support.matrix_local[row][column] - lower.matrix_local[row][column])
            for row in range(3)
            for column in range(3)
        )
        require(head_error <= SUPPORT_HEAD_MATCH_TOLERANCE_METERS, f"{support.name} head error {head_error}m")
        require(orientation_error <= SUPPORT_LOCAL_ROTATION_TOLERANCE_DEGREES, f"{support.name} rest orientation error {orientation_error} degrees")
        require(direction_error <= SUPPORT_LOCAL_ROTATION_TOLERANCE_DEGREES, f"{support.name} direction error {direction_error} degrees")
        require(basis_component_error <= 1e-5, f"{support.name} rest-basis component error {basis_component_error}")
        if not imported_fbx_semantics:
            require(not support.use_connect, f"{support.name} source must be unconnected")
            require(length_error <= SUPPORT_LENGTH_TOLERANCE_METERS, f"{support.name} source length error {length_error}m")
        per_side[side] = {
            "parent": support.parent.name,
            "connected": support.use_connect,
            "connectedIsFbxImportSynthesis": imported_fbx_semantics and support.use_connect,
            "deform": support.use_deform,
            "role": support.get("sessionRole"),
            "headMeters": list(support.head_local),
            "tailMeters": list(support.tail_local),
            "lengthMeters": support.length,
            "headMatchErrorMeters": head_error,
            "lengthErrorMeters": length_error,
            "restOrientationErrorDegrees": orientation_error,
            "directionErrorDegrees": direction_error,
            "restBasisMaximumComponentError": basis_component_error,
        }
    return {
        "status": "PASS",
        "validationMode": "IMPORTED_FBX_SEMANTICS" if imported_fbx_semantics else "SOURCE_DCC_CONTRACT",
        "appendOrder": list(ELBOW_SUPPORT_GROUPS),
        "jointCount": len(ordered_bone_names),
        "deformGroupCount": EXPECTED_DEFORM_GROUPS,
        "perSide": per_side,
    }


def elbow_support_manifest_report(rig_manifest: dict[str, object]) -> dict[str, object]:
    """Verify the source-only connection and display-length contract.

    FBX has no Blender ``use_connect`` semantic.  Blender 5.2 reconstructs a
    connected display bone whenever a child head equals its parent tail and also
    synthesizes a leaf-display length.  Those imported values are nonsemantic;
    the source manifest remains authoritative for disconnected state and 80 mm
    authoring length.
    """
    entries = rig_manifest["hierarchy"]["bones"]
    by_name = {entry["name"]: entry for entry in entries}
    require([entry["name"] for entry in entries[-2:]] == list(ELBOW_SUPPORT_GROUPS), "V2 source support append order changed")
    per_side = {}
    for side in ("Left", "Right"):
        support = by_name[f"{side}ElbowSupport"]
        lower = by_name[f"{side}LowerArm"]
        require(support["parent"] == f"{side}UpperArm", f"{support['name']} source parent changed")
        require(support["connected"] is False, f"{support['name']} source must be unconnected")
        require(bool(support["deform"]), f"{support['name']} source must deform")
        require(support["role"] == "bend_support", f"{support['name']} source role changed")
        head_error = Vector(support["headMeters"]) - Vector(lower["headMeters"])
        length_error = abs(float(support["lengthMeters"]) - SUPPORT_LENGTH_METERS)
        require(head_error.length <= SUPPORT_HEAD_MATCH_TOLERANCE_METERS, f"{support['name']} source pivot error {head_error.length}m")
        require(length_error <= SUPPORT_LENGTH_TOLERANCE_METERS, f"{support['name']} source length error {length_error}m")
        per_side[side] = {
            "parent": support["parent"],
            "connected": support["connected"],
            "deform": support["deform"],
            "role": support["role"],
            "headMatchErrorMeters": head_error.length,
            "lengthMeters": support["lengthMeters"],
            "lengthErrorMeters": length_error,
        }
    return {
        "status": "PASS",
        "validationMode": "SOURCE_MANIFEST_CONTRACT",
        "appendOrder": list(ELBOW_SUPPORT_GROUPS),
        "perSide": per_side,
    }


def dominant_vertex_regions(obj: bpy.types.Object, allowed_groups: list[str]) -> dict[str, list[int]]:
    rows = weight_rows(obj)
    result = {name: [] for name in allowed_groups}
    for index, row in enumerate(rows):
        positive = [(name, weight) for name, weight in row if weight > 1e-8]
        if not positive:
            continue
        name, _weight = max(positive, key=lambda item: (item[1], item[0]))
        result[name].append(index)
    return result


def barycentric_coordinates(point: Vector, a: Vector, b: Vector, c: Vector) -> tuple[float, float, float]:
    edge_ab = b - a
    edge_ac = c - a
    relative = point - a
    dot_ab_ab = edge_ab.dot(edge_ab)
    dot_ab_ac = edge_ab.dot(edge_ac)
    dot_ac_ac = edge_ac.dot(edge_ac)
    dot_relative_ab = relative.dot(edge_ab)
    dot_relative_ac = relative.dot(edge_ac)
    denominator = (dot_ab_ab * dot_ac_ac) - (dot_ab_ac * dot_ab_ac)
    require(abs(denominator) > 1e-18, "Mirror projection reached a degenerate triangle")
    second = ((dot_ac_ac * dot_relative_ab) - (dot_ab_ac * dot_relative_ac)) / denominator
    third = ((dot_ab_ab * dot_relative_ac) - (dot_ab_ac * dot_relative_ab)) / denominator
    first = 1.0 - second - third
    # BVH nearest points lie on the triangle.  Clamp only float noise, then
    # renormalize so interpolation is deterministic across import round trips.
    values = [max(0.0, min(1.0, value)) for value in (first, second, third)]
    total = sum(values)
    require(total > 1e-12, "Mirror projection produced invalid barycentrics")
    return tuple(value / total for value in values)


def mirror_surface_samples(
    rest_positions: list[Vector],
    triangles: list[tuple[int, int, int]],
) -> tuple[list[dict[str, object]], float]:
    """Map +X vertices to exact points on the opposite rest surface.

    MPFB morphs retain symmetric topology but intentionally leave a few
    millimetres of surface asymmetry.  Nearest-vertex displacement comparison is
    not rotation invariant: rotating that accepted rest offset creates a false
    residual.  Sampling the opposite deformation field at the reflected rest
    location tests skinning symmetry without changing the accepted anatomy.
    """
    opposite_triangles = [
        triangle for triangle in triangles
        if sum(rest_positions[index].x for index in triangle) / 3.0 < -1e-8
    ]
    require(opposite_triangles, "No -X surface triangles found for mirror projection")
    tree = BVHTree.FromPolygons(
        [tuple(point) for point in rest_positions],
        opposite_triangles,
        all_triangles=True,
        epsilon=1e-9,
    )
    samples: list[dict[str, object]] = []
    maximum_distance = 0.0
    worst: dict[str, object] | None = None
    for vertex_index, point in enumerate(rest_positions):
        if point.x <= 1e-6:
            continue
        reflected = Vector((-point.x, point.y, point.z))
        projected, _normal, filtered_triangle_index, distance = tree.find_nearest(reflected)
        require(projected is not None and filtered_triangle_index is not None and distance is not None, "Mirror surface projection failed")
        triangle = opposite_triangles[int(filtered_triangle_index)]
        barycentrics = barycentric_coordinates(
            projected,
            *(rest_positions[index] for index in triangle),
        )
        sample = {
            "vertex": vertex_index,
            "triangle": triangle,
            "barycentrics": barycentrics,
            "reflectedPoint": tuple(reflected),
            "projectedPoint": tuple(projected),
            "projectionDistanceMeters": float(distance),
        }
        samples.append(sample)
        if distance > maximum_distance:
            maximum_distance = float(distance)
            worst = sample
    require(samples, "No +X vertices found for mirror deformation sampling")
    require(
        maximum_distance <= MAX_REST_MIRROR_ERROR_METERS,
        f"Mirror surface projection distance {maximum_distance}m exceeds "
        f"{MAX_REST_MIRROR_ERROR_METERS}m; worst sample {worst}",
    )
    return samples, maximum_distance


def object_weight_rows(obj: bpy.types.Object) -> list[dict[str, float]]:
    group_names = {group.index: group.name for group in obj.vertex_groups}
    return [
        {
            group_names[item.group]: float(item.weight)
            for item in vertex.groups
            if item.weight > 1e-8
        }
        for vertex in obj.data.vertices
    ]


def pose_deform_matrices(rig: bpy.types.Object) -> dict[str, Matrix]:
    return {
        pose_bone.name: pose_bone.matrix @ pose_bone.bone.matrix_local.inverted()
        for pose_bone in rig.pose.bones
        if pose_bone.name in SESSION_DEFORM_GROUPS
    }


def lbs_deform_point(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    point_object_space: Vector,
    row: dict[str, float],
    deform_matrices: dict[str, Matrix],
) -> Vector:
    total = sum(row.values())
    require(WEIGHT_SUM_MIN <= total <= WEIGHT_SUM_MAX, f"Analytic LBS row sum {total} is invalid")
    object_to_armature = rig.matrix_world.inverted() @ obj.matrix_world
    armature_to_object = obj.matrix_world.inverted() @ rig.matrix_world
    point_armature_space = object_to_armature @ point_object_space
    deformed_armature_space = Vector()
    for name, weight in row.items():
        require(name in deform_matrices, f"Analytic LBS row references unknown bone {name}")
        deformed_armature_space += (deform_matrices[name] @ point_armature_space) * weight
    return armature_to_object @ deformed_armature_space


def interpolate_weight_row(
    rows: list[dict[str, float]],
    triangle: tuple[int, int, int],
    barycentrics: tuple[float, float, float],
) -> dict[str, float]:
    result: dict[str, float] = {}
    for index, barycentric in zip(triangle, barycentrics):
        for name, weight in rows[index].items():
            result[name] = result.get(name, 0.0) + (weight * barycentric)
    total = sum(result.values())
    require(total > 1e-8, "Interpolated LBS row is empty")
    return {name: weight / total for name, weight in result.items() if weight > 1e-8}


def reset_pose(rig: bpy.types.Object) -> None:
    if rig.animation_data is not None:
        rig.animation_data.action = None
    for pose_bone in rig.pose.bones:
        pose_bone.matrix_basis = Matrix.Identity(4)
    bpy.context.scene.frame_set(1)
    bpy.context.view_layer.update()


def expected_support_local_rotation(lower_arm: bpy.types.PoseBone) -> Quaternion:
    identity = Quaternion((1.0, 0.0, 0.0, 0.0))
    lower_rotation = lower_arm.matrix_basis.to_quaternion().normalized()
    return identity.slerp(lower_rotation, ELBOW_SUPPORT_ROTATION_FACTOR).normalized()


def apply_elbow_support_pose(rig: bpy.types.Object) -> None:
    """Drive V2 support siblings from half the matching forearm local pose."""
    for side in ("Left", "Right"):
        lower_arm = rig.pose.bones[f"{side}LowerArm"]
        support = rig.pose.bones[f"{side}ElbowSupport"]
        support.matrix_basis = Matrix.LocRotScale(
            Vector(),
            expected_support_local_rotation(lower_arm),
            Vector((1.0, 1.0, 1.0)),
        )
    bpy.context.view_layer.update()


def quaternion_error_degrees(first: Quaternion, second: Quaternion) -> float:
    dot = max(-1.0, min(1.0, abs(first.normalized().dot(second.normalized()))))
    return math.degrees(2.0 * math.acos(dot))


def elbow_support_pose_report(rig: bpy.types.Object) -> dict[str, object]:
    per_side = {}
    maximum_rotation_error = 0.0
    maximum_translation = 0.0
    maximum_scale_error = 0.0
    for side in ("Left", "Right"):
        lower_arm = rig.pose.bones[f"{side}LowerArm"]
        support = rig.pose.bones[f"{side}ElbowSupport"]
        location, rotation, scale = support.matrix_basis.decompose()
        expected = expected_support_local_rotation(lower_arm)
        rotation_error = quaternion_error_degrees(rotation, expected)
        translation = location.length
        scale_error = max(abs(value - 1.0) for value in scale)
        maximum_rotation_error = max(maximum_rotation_error, rotation_error)
        maximum_translation = max(maximum_translation, translation)
        maximum_scale_error = max(maximum_scale_error, scale_error)
        per_side[side] = {
            "rotationErrorDegrees": rotation_error,
            "translationMeters": translation,
            "maximumScaleError": scale_error,
        }
    require(maximum_rotation_error <= SUPPORT_LOCAL_ROTATION_TOLERANCE_DEGREES, f"Elbow-support local rotation error {maximum_rotation_error} degrees")
    require(maximum_translation <= 1e-6, f"Elbow-support pose has {maximum_translation}m translation")
    require(maximum_scale_error <= 1e-6, f"Elbow-support pose has scale error {maximum_scale_error}")
    return {
        "status": "PASS",
        "rotationFactor": ELBOW_SUPPORT_ROTATION_FACTOR,
        "maximumRotationErrorDegrees": maximum_rotation_error,
        "maximumTranslationMeters": maximum_translation,
        "maximumScaleError": maximum_scale_error,
        "perSide": per_side,
    }


def rotate_pose_bone_armature_space(
    rig: bpy.types.Object,
    bone_name: str,
    axis: Vector,
    degrees: float,
) -> None:
    pose_bone = rig.pose.bones[bone_name]
    pivot = pose_bone.head.copy()
    transform = (
        Matrix.Translation(pivot)
        @ Matrix.Rotation(math.radians(degrees), 4, axis.normalized())
        @ Matrix.Translation(-pivot)
    )
    pose_bone.matrix = transform @ pose_bone.matrix
    bpy.context.view_layer.update()


def rotate_pair(rig: bpy.types.Object, left: str, right: str, axis: Vector, left_degrees: float, right_degrees: float) -> None:
    rotate_pose_bone_armature_space(rig, left, axis, left_degrees)
    rotate_pose_bone_armature_space(rig, right, axis, right_degrees)


def rotate_longitudinal(rig: bpy.types.Object, bone_name: str, degrees: float) -> None:
    pose_bone = rig.pose.bones[bone_name]
    axis = (pose_bone.tail - pose_bone.head).normalized()
    rotate_pose_bone_armature_space(rig, bone_name, axis, degrees)


def pose_arms_down(rig: bpy.types.Object) -> None:
    # A physiologic neutral stance retains roughly 15 degrees of abduction.  A
    # mathematically vertical arm drives this body's closed fingers through the
    # pelvis, which tests an impossible collision rather than shoulder skinning.
    rotate_pair(rig, "LeftUpperArm", "RightUpperArm", Vector((0.0, 1.0, 0.0)), 75.0, -75.0)


def pose_arms_forward(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftUpperArm", "RightUpperArm", Vector((0.0, 0.0, 1.0)), -90.0, 90.0)


def pose_arms_overhead_150(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftUpperArm", "RightUpperArm", Vector((0.0, 1.0, 0.0)), -60.0, 60.0)


def pose_arms_back_35(rig: bpy.types.Object) -> None:
    pose_arms_down(rig)
    rotate_pair(rig, "LeftUpperArm", "RightUpperArm", Vector((1.0, 0.0, 0.0)), 35.0, 35.0)


def pose_humeral_twist_positive(rig: bpy.types.Object) -> None:
    rotate_longitudinal(rig, "LeftUpperArm", 70.0)
    rotate_longitudinal(rig, "RightUpperArm", -70.0)


def pose_humeral_twist_negative(rig: bpy.types.Object) -> None:
    rotate_longitudinal(rig, "LeftUpperArm", -70.0)
    rotate_longitudinal(rig, "RightUpperArm", 70.0)


def pose_elbow_110(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftLowerArm", "RightLowerArm", Vector((0.0, 0.0, 1.0)), -110.0, 110.0)


def bow_draw_pose_snapshot_contract() -> dict[str, object]:
    require(BOW_DRAW_POSE_SNAPSHOT.is_file(), f"Missing BowDraw pose snapshot: {BOW_DRAW_POSE_SNAPSHOT}")
    require(
        sha256(BOW_DRAW_POSE_SNAPSHOT) == EXPECTED_BOW_DRAW_POSE_SNAPSHOT_SHA256,
        "BowDraw pose snapshot changed without a source-contract update",
    )
    snapshot = json.loads(BOW_DRAW_POSE_SNAPSHOT.read_text(encoding="utf-8"))
    require(snapshot["schemaVersion"] == 1, "BowDraw pose snapshot schema changed")
    require(snapshot["asset"] == "SessionHumanoidBaseV2BowDrawPoseSnapshot", "BowDraw pose snapshot identity changed")
    source = snapshot["source"]
    require(source["animationFbxSha256"] == EXPECTED_ANIMATION_FBX_SHA256, "BowDraw snapshot pins another animation FBX")
    require(source["animationManifestSha256"] == EXPECTED_ANIMATION_MANIFEST_SHA256, "BowDraw snapshot pins another animation manifest")
    require(source["rigFbxSha256"] == EXPECTED_RIG_FBX_SHA256, "BowDraw snapshot pins another rest rig")
    require(source["clip"] == "HunterBowDraw" and source["frame"] == CLIPS["HunterBowDraw"][1], "BowDraw snapshot is not the frozen full-draw frame")
    require(source["actionSlotIdentifier"] == "OBSlot" and source["actionSlotTargetIdType"] == "OBJECT", "BowDraw snapshot was sampled without the frozen Blender action slot")
    require(len(snapshot["bones"]) == 22, "BowDraw snapshot meaningful-bone count changed")
    return snapshot


def pose_bow_draw(rig: bpy.types.Object) -> None:
    """Apply the accepted HunterBowDraw full-draw frame as frozen local bases."""
    snapshot = bow_draw_pose_snapshot_contract()
    for bone_name, record in snapshot["bones"].items():
        require(bone_name in rig.pose.bones, f"BowDraw snapshot references missing bone {bone_name}")
        rig.pose.bones[bone_name].matrix_basis = Matrix.LocRotScale(
            Vector(record["location"]),
            Quaternion(record["rotationQuaternionWxyz"]),
            Vector(record["scale"]),
        )
    bpy.context.view_layer.update()
    maximum_hand_error = 0.0
    for bone_name, expected in snapshot["expectedHandHeadsArmatureSpace"].items():
        maximum_hand_error = max(maximum_hand_error, (rig.pose.bones[bone_name].head - Vector(expected)).length)
    require(maximum_hand_error <= 1e-5, f"BowDraw snapshot hand-head reproduction error {maximum_hand_error}m")


def pose_pronation_positive(rig: bpy.types.Object) -> None:
    rotate_longitudinal(rig, "LeftLowerArm", 80.0)
    rotate_longitudinal(rig, "RightLowerArm", -80.0)


def pose_pronation_negative(rig: bpy.types.Object) -> None:
    rotate_longitudinal(rig, "LeftLowerArm", -80.0)
    rotate_longitudinal(rig, "RightLowerArm", 80.0)


def pose_elbow_pronation_positive(rig: bpy.types.Object) -> None:
    pose_elbow_110(rig)
    pose_pronation_positive(rig)


def pose_elbow_pronation_negative(rig: bpy.types.Object) -> None:
    pose_elbow_110(rig)
    pose_pronation_negative(rig)


def pose_wrist_flex_extension(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftHand", "RightHand", Vector((0.0, 1.0, 0.0)), 60.0, -60.0)


def pose_wrist_radial_ulnar(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftHand", "RightHand", Vector((0.0, 0.0, 1.0)), 25.0, -25.0)


FINGER_PREFIXES = ("Thumb", "Index", "Middle", "Ring", "Little")
FINGER_SEGMENTS = ("Proximal", "Intermediate", "Distal")


def curl_fingers(rig: bpy.types.Object, degrees: tuple[float, float, float]) -> None:
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        for finger in FINGER_PREFIXES:
            for segment, amount in zip(FINGER_SEGMENTS, degrees):
                rotate_pose_bone_armature_space(
                    rig,
                    f"{side}{finger}{segment}",
                    Vector((0.0, 1.0, 0.0)),
                    sign * amount,
                )


def pose_fist(rig: bpy.types.Object) -> None:
    curl_fingers(rig, (55.0, 65.0, 55.0))


def pose_bow_grip(rig: bpy.types.Object) -> None:
    curl_fingers(rig, (35.0, 45.0, 35.0))


def pose_hip_knee(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftUpperLeg", "RightUpperLeg", Vector((1.0, 0.0, 0.0)), -90.0, -90.0)
    rotate_pair(rig, "LeftLowerLeg", "RightLowerLeg", Vector((1.0, 0.0, 0.0)), 110.0, 110.0)


def pose_ankle_dorsi(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftFoot", "RightFoot", Vector((1.0, 0.0, 0.0)), 25.0, 25.0)


def pose_ankle_plantar(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftFoot", "RightFoot", Vector((1.0, 0.0, 0.0)), -45.0, -45.0)


def pose_ankle_inversion(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftFoot", "RightFoot", Vector((0.0, 1.0, 0.0)), -20.0, 20.0)


def pose_toe_45(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftToes", "RightToes", Vector((1.0, 0.0, 0.0)), 45.0, 45.0)


STATIC_POSES = (
    StaticPoseSpec("NeutralTPose", True, lambda _rig: None),
    StaticPoseSpec("ArmsDown", True, pose_arms_down),
    StaticPoseSpec("ArmsForward90", True, pose_arms_forward),
    StaticPoseSpec("ArmsOverhead150", True, pose_arms_overhead_150),
    StaticPoseSpec("ArmsBack35", True, pose_arms_back_35),
    StaticPoseSpec("HumeralTwistPositive70", True, pose_humeral_twist_positive),
    StaticPoseSpec("HumeralTwistNegative70", True, pose_humeral_twist_negative),
    StaticPoseSpec("Elbow110", True, pose_elbow_110),
    StaticPoseSpec("BowDraw", False, pose_bow_draw),
    StaticPoseSpec("PronationPositive80", True, pose_pronation_positive),
    StaticPoseSpec("PronationNegative80", True, pose_pronation_negative),
    StaticPoseSpec("Elbow110PronationPositive80", True, pose_elbow_pronation_positive),
    StaticPoseSpec("Elbow110PronationNegative80", True, pose_elbow_pronation_negative),
    StaticPoseSpec("WristFlexExtension60", False, pose_wrist_flex_extension),
    StaticPoseSpec("WristRadialUlnar25", False, pose_wrist_radial_ulnar),
    StaticPoseSpec("Fist", True, pose_fist),
    StaticPoseSpec("BowGrip", True, pose_bow_grip),
    StaticPoseSpec("Hip90Knee110", True, pose_hip_knee),
    StaticPoseSpec("AnkleDorsi25", True, pose_ankle_dorsi),
    StaticPoseSpec("AnklePlantar45", True, pose_ankle_plantar),
    StaticPoseSpec("AnkleInversion20", True, pose_ankle_inversion),
    StaticPoseSpec("Toe45", True, pose_toe_45),
)

HISTORIC_ELBOW_TRIANGLE_INDICES = (5818, 19069, 20431)
HISTORIC_ELBOW_EDGES = ((10015, 10032), (3879, 3880))


def triangle_area(a: Vector, b: Vector, c: Vector) -> float:
    return (b - a).cross(c - a).length * 0.5


def distance_to_axis(point: Vector, head: Vector, tail: Vector) -> float:
    axis = tail - head
    length_squared = axis.length_squared
    if length_squared <= 1e-12:
        return (point - head).length
    parameter = (point - head).dot(axis) / length_squared
    closest = head + (axis * parameter)
    return (point - closest).length


def segment_hits_triangle(start: Vector, end: Vector, a: Vector, b: Vector, c: Vector) -> bool:
    direction = end - start
    length = direction.length
    if length <= 1e-12:
        return False
    hit = intersect_ray_tri(a, b, c, direction.normalized(), start, True)
    return hit is not None and (hit - start).length <= length + 1e-7


def triangles_intersect(points: list[Vector], first: tuple[int, int, int], second: tuple[int, int, int]) -> bool:
    a = [points[index] for index in first]
    b = [points[index] for index in second]
    return any(
        segment_hits_triangle(a[i], a[(i + 1) % 3], b[0], b[1], b[2])
        for i in range(3)
    ) or any(
        segment_hits_triangle(b[i], b[(i + 1) % 3], a[0], a[1], a[2])
        for i in range(3)
    )


def crossing_edge_depth(start: Vector, end: Vector, a: Vector, b: Vector, c: Vector) -> float:
    """Return the inward endpoint depth only when this edge crosses the triangle.

    The previous proxy measured every vertex of both overlapping triangles against
    both oriented planes.  A vertex which did not participate in the crossing could
    therefore report almost an entire triangle edge as "penetration".  The hard
    2 mm gate is retained, but it is now applied only to endpoints of segments that
    actually cross the opposite surface.
    """
    if not segment_hits_triangle(start, end, a, b, c):
        return 0.0
    normal = (b - a).cross(c - a)
    if normal.length_squared <= 1e-18:
        return 0.0
    normal.normalize()
    start_distance = (start - a).dot(normal)
    end_distance = (end - a).dot(normal)
    return max(0.0, -start_distance, -end_distance)


def approximate_intersection_depth(points: list[Vector], first: tuple[int, int, int], second: tuple[int, int, int]) -> float:
    a = [points[index] for index in first]
    b = [points[index] for index in second]
    depths = [
        crossing_edge_depth(a[index], a[(index + 1) % 3], b[0], b[1], b[2])
        for index in range(3)
    ]
    depths.extend(
        crossing_edge_depth(b[index], b[(index + 1) % 3], a[0], a[1], a[2])
        for index in range(3)
    )
    return max(depths, default=0.0)


def self_penetration_report(points: list[Vector], triangles: list[tuple[int, int, int]]) -> dict[str, object]:
    tree = BVHTree.FromPolygons([tuple(point) for point in points], triangles, all_triangles=True, epsilon=1e-7)
    candidates = tree.overlap(tree)
    seen: set[tuple[int, int]] = set()
    intersections: list[dict[str, object]] = []
    maximum_depth = 0.0
    for first_index, second_index in candidates:
        if first_index == second_index:
            continue
        key = (min(first_index, second_index), max(first_index, second_index))
        if key in seen:
            continue
        seen.add(key)
        first = triangles[key[0]]
        second = triangles[key[1]]
        if set(first) & set(second):
            continue
        if not triangles_intersect(points, first, second):
            continue
        depth = approximate_intersection_depth(points, first, second)
        maximum_depth = max(maximum_depth, depth)
        if depth > MAX_PENETRATION_METERS:
            intersections.append({
                "triangles": list(key),
                "vertices": [list(first), list(second)],
                "centers": [
                    [sum(points[index][axis] for index in first) / 3.0 for axis in range(3)],
                    [sum(points[index][axis] for index in second) / 3.0 for axis in range(3)],
                ],
                "approximateDepthMeters": depth,
            })
            if len(intersections) >= 32:
                break
    require(
        not intersections,
        f"Nonadjacent surface penetration exceeds {MAX_PENETRATION_METERS}m; "
        f"first failures {intersections[:4]}",
    )
    return {
        "candidatePairs": len(seen),
        "intersectionsOverLimit": 0,
        "maximumApproximateDepthMeters": maximum_depth,
        "limitMeters": MAX_PENETRATION_METERS,
        "method": "BVH broad phase, exact segment-triangle crossing, oriented-plane depth proxy; adjacent triangles excluded",
    }


def deformation_frame_report(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    rest_positions: list[Vector],
    triangles: list[tuple[int, int, int]],
    edges: list[tuple[int, int]],
    rest_triangle_areas: list[float],
    rest_edge_lengths: list[float],
    dominant_regions: dict[str, list[int]],
    rest_region_radii: dict[str, float],
    mirror_samples: list[dict[str, object]] | None,
    run_penetration: bool,
) -> dict[str, object]:
    points = evaluated_positions(obj)
    require(len(points) == len(rest_positions), "Evaluated vertex count changed")
    require(all(all(math.isfinite(component) for component in point) for point in points), "Deformation produced non-finite vertices")
    group_names = {group.index: group.name for group in obj.vertex_groups}

    def vertex_weight_debug(index: int) -> list[tuple[str, float]]:
        return sorted(
            ((group_names[item.group], round(float(item.weight), 6)) for item in obj.data.vertices[index].groups if item.weight > 1e-8),
            key=lambda item: (-item[1], item[0]),
        )

    area_ratios = []
    for triangle, rest_area in zip(triangles, rest_triangle_areas):
        area = triangle_area(*(points[index] for index in triangle))
        require(area > 1e-12, "Deformation collapsed a triangle")
        area_ratios.append(area / rest_area)
    edge_ratios = []
    for edge, rest_length in zip(edges, rest_edge_lengths):
        length = (points[edge[0]] - points[edge[1]]).length
        require(length > 1e-9, "Deformation collapsed an edge")
        edge_ratios.append(length / rest_length)
    minimum_area_ratio = min(area_ratios)
    minimum_area_triangle = area_ratios.index(minimum_area_ratio)
    minimum_edge_ratio = min(edge_ratios)
    maximum_edge_ratio = max(edge_ratios)
    minimum_edge_index = edge_ratios.index(minimum_edge_ratio)
    maximum_edge_index = edge_ratios.index(maximum_edge_ratio)
    require(
        minimum_area_ratio >= MIN_TRIANGLE_AREA_RATIO,
        f"Triangle area ratio {minimum_area_ratio} is below {MIN_TRIANGLE_AREA_RATIO}; triangle {minimum_area_triangle} "
        f"vertices {triangles[minimum_area_triangle]} rest {[list(rest_positions[index]) for index in triangles[minimum_area_triangle]]} "
        f"posed {[list(points[index]) for index in triangles[minimum_area_triangle]]} "
        f"weights {[(index, vertex_weight_debug(index)) for index in triangles[minimum_area_triangle]]}",
    )
    require(
        minimum_edge_ratio >= MIN_EDGE_LENGTH_RATIO,
        f"Edge ratio {minimum_edge_ratio} is below {MIN_EDGE_LENGTH_RATIO}; edge {edges[minimum_edge_index]} weights {[(index, vertex_weight_debug(index)) for index in edges[minimum_edge_index]]}",
    )
    require(
        maximum_edge_ratio <= MAX_EDGE_LENGTH_RATIO,
        f"Edge ratio {maximum_edge_ratio} exceeds {MAX_EDGE_LENGTH_RATIO}; edge {edges[maximum_edge_index]}",
    )

    cross_sections: dict[str, float] = {}
    for group_name, indices in dominant_regions.items():
        if len(indices) < 4 or group_name in ZERO_INFLUENCE_FACE_GROUPS:
            continue
        pose_bone = rig.pose.bones[group_name]
        radii = [distance_to_axis(points[index], pose_bone.head, pose_bone.tail) for index in indices]
        current = math.sqrt(sum(radius * radius for radius in radii) / len(radii))
        baseline = rest_region_radii[group_name]
        ratio = current / baseline if baseline > 1e-8 else 1.0
        cross_sections[group_name] = ratio
    minimum_cross_section = min(cross_sections.values())
    require(minimum_cross_section >= MIN_CROSS_SECTION_RETENTION, f"Cross-section retention {minimum_cross_section} is below {MIN_CROSS_SECTION_RETENTION}")

    mirror_error = None
    mirror_field_report = None
    if mirror_samples is not None:
        rows = object_weight_rows(obj)
        deform_matrices = pose_deform_matrices(rig)
        analytic_vertex_errors = [
            (lbs_deform_point(obj, rig, rest_point, row, deform_matrices) - evaluated_point).length
            for rest_point, row, evaluated_point in zip(rest_positions, rows, points)
        ]
        maximum_analytic_vertex_error = max(analytic_vertex_errors)
        analytic_worst_vertex = analytic_vertex_errors.index(maximum_analytic_vertex_error)
        require(
            maximum_analytic_vertex_error <= 0.0001,
            f"Analytic LBS evaluator differs from Blender by {maximum_analytic_vertex_error}m "
            f"at vertex {analytic_worst_vertex}; limit 0.0001m",
        )
        mirror_deltas = []
        interpolated_rows = []
        reflected_results = []
        for sample in mirror_samples:
            vertex_index = int(sample["vertex"])
            triangle = tuple(int(index) for index in sample["triangle"])
            barycentrics = tuple(float(value) for value in sample["barycentrics"])
            interpolated_row = interpolate_weight_row(rows, triangle, barycentrics)
            reflected_rest_point = Vector(sample["reflectedPoint"])
            opposite_deformed_point = lbs_deform_point(
                obj,
                rig,
                reflected_rest_point,
                interpolated_row,
                deform_matrices,
            )
            reflected_opposite_deformed = Vector((
                -opposite_deformed_point.x,
                opposite_deformed_point.y,
                opposite_deformed_point.z,
            ))
            mirror_deltas.append((points[vertex_index] - reflected_opposite_deformed).length)
            interpolated_rows.append(interpolated_row)
            reflected_results.append(reflected_opposite_deformed)
        mirror_error = max(mirror_deltas)
        mirror_worst_index = mirror_deltas.index(mirror_error)
        mirror_worst_sample = mirror_samples[mirror_worst_index]
        mirror_worst_vertex = int(mirror_worst_sample["vertex"])
        mirror_worst_triangle = tuple(int(index) for index in mirror_worst_sample["triangle"])
        mirror_worst_row = interpolated_rows[mirror_worst_index]
        mirror_worst_reflected_result = reflected_results[mirror_worst_index]
        require(
            mirror_error <= MAX_MIRROR_ERROR_METERS,
            f"Exact-point opposite-side LBS residual {mirror_error} exceeds {MAX_MIRROR_ERROR_METERS}; "
            f"sample vertex {mirror_worst_vertex}, triangle {mirror_worst_triangle}, "
            f"barycentrics {mirror_worst_sample['barycentrics']}, "
            f"projection distance {mirror_worst_sample['projectionDistanceMeters']}, "
            f"interpolated opposite row {sorted(mirror_worst_row.items())}, "
            f"actual +X point {list(points[mirror_worst_vertex])}, "
            f"reflected analytic point {list(mirror_worst_reflected_result)}, "
            f"weights {[(index, vertex_weight_debug(index)) for index in (mirror_worst_vertex, *mirror_worst_triangle)]}",
        )
        mirror_field_report = {
            "maximumResidualMeters": mirror_error,
            "limitMeters": MAX_MIRROR_ERROR_METERS,
            "analyticLbsValidation": {
                "maximumVertexDeviationMeters": maximum_analytic_vertex_error,
                "worstVertex": analytic_worst_vertex,
                "limitMeters": 0.0001,
            },
            "worstSample": {
                **mirror_worst_sample,
                "interpolatedOppositeWeightRow": sorted(mirror_worst_row.items()),
                "actualPositiveDeformedPoint": list(points[mirror_worst_vertex]),
                "reflectedAnalyticDeformedPoint": list(mirror_worst_reflected_result),
            },
            "method": "nearest opposite rest triangle and barycentric final-weight interpolation; evaluate exact reflected rest point T with opposite-side pose*inverse-bind LBS matrices; reflect deformed T; compare to actual +X Blender vertex",
        }

    penetration = self_penetration_report(points, triangles) if run_penetration else None
    bounds_min = [min(point[axis] for point in points) for axis in range(3)]
    bounds_max = [max(point[axis] for point in points) for axis in range(3)]
    return {
        "minimumTriangleAreaRatio": minimum_area_ratio,
        "minimumEdgeLengthRatio": minimum_edge_ratio,
        "maximumEdgeLengthRatio": maximum_edge_ratio,
        "minimumCrossSectionRetention": minimum_cross_section,
        "maximumMirrorErrorMeters": mirror_error,
        "mirrorField": mirror_field_report,
        "selfPenetration": penetration,
        "boundsMin": bounds_min,
        "boundsMax": bounds_max,
        "heightMeters": bounds_max[2] - bounds_min[2],
    }


def rest_deformation_context(obj: bpy.types.Object, rig: bpy.types.Object, allowed_groups: list[str]) -> dict[str, object]:
    reset_pose(rig)
    rest_positions = evaluated_positions(obj)
    triangles = triangle_indices(obj.data)
    edges = unique_edges(triangles)
    rest_triangle_areas = [triangle_area(*(rest_positions[index] for index in triangle)) for triangle in triangles]
    rest_edge_lengths = [(rest_positions[first] - rest_positions[second]).length for first, second in edges]
    require(min(rest_triangle_areas) > 1e-12, "Rest mesh contains a degenerate triangle")
    require(min(rest_edge_lengths) > 1e-9, "Rest mesh contains a collapsed edge")
    dominant_regions = dominant_vertex_regions(obj, allowed_groups)
    rest_region_radii: dict[str, float] = {}
    for group_name, indices in dominant_regions.items():
        if len(indices) < 4 or group_name in ZERO_INFLUENCE_FACE_GROUPS:
            continue
        bone = rig.data.bones[group_name]
        radii = [distance_to_axis(rest_positions[index], bone.head_local, bone.tail_local) for index in indices]
        rest_region_radii[group_name] = math.sqrt(sum(radius * radius for radius in radii) / len(radii))
    mirror_samples, rest_mirror_error = mirror_surface_samples(rest_positions, triangles)
    sole_masks = {}
    for side, sign in (("Left", 1), ("Right", -1)):
        side_indices = [
            index for index, point in enumerate(rest_positions)
            if (point.x * sign) > 0.02 and point.z <= 0.02
        ]
        require(side_indices, f"No {side} sole vertices found")
        minimum = min(rest_positions[index].z for index in side_indices)
        sole_masks[side] = [index for index in side_indices if rest_positions[index].z <= minimum + 0.0025]
        require(len(sole_masks[side]) >= 8, f"{side} sole contact patch is too small")
    return {
        "restPositions": rest_positions,
        "triangles": triangles,
        "edges": edges,
        "restTriangleAreas": rest_triangle_areas,
        "restEdgeLengths": rest_edge_lengths,
        "dominantRegions": dominant_regions,
        "restRegionRadii": rest_region_radii,
        "mirrorSamples": mirror_samples,
        "restMirrorErrorMeters": rest_mirror_error,
        "soleMasks": sole_masks,
        "restHeightMeters": max(point.z for point in rest_positions) - min(point.z for point in rest_positions),
    }


def run_static_deformation_gates(obj: bpy.types.Object, rig: bpy.types.Object, allowed_groups: list[str]) -> dict[str, object]:
    context = rest_deformation_context(obj, rig, allowed_groups)
    reports: dict[str, object] = {}
    for spec in STATIC_POSES:
        reset_pose(rig)
        spec.configure(rig)
        apply_elbow_support_pose(rig)
        bpy.context.view_layer.update()
        support_pose = elbow_support_pose_report(rig)
        try:
            report = deformation_frame_report(
                obj,
                rig,
                context["restPositions"],
                context["triangles"],
                context["edges"],
                context["restTriangleAreas"],
                context["restEdgeLengths"],
                context["dominantRegions"],
                context["restRegionRadii"],
                context["mirrorSamples"] if spec.symmetric else None,
                True,
            )
        except RuntimeError as error:
            raise RuntimeError(f"Static pose {spec.name} failed: {error}") from error
        if spec.name not in {"Hip90Knee110"}:
            require(
                report["heightMeters"] >= context["restHeightMeters"] * 0.85,
                f"{spec.name} collapses below 85% rest height",
            )
        report["elbowSupportPose"] = support_pose
        reports[spec.name] = report
    reset_pose(rig)
    pose_elbow_110(rig)
    apply_elbow_support_pose(rig)
    bpy.context.view_layer.update()
    elbow_points = evaluated_positions(obj)
    historic_triangles = {}
    for triangle_index in HISTORIC_ELBOW_TRIANGLE_INDICES:
        triangle = context["triangles"][triangle_index]
        rest_area = triangle_area(*(context["restPositions"][index] for index in triangle))
        posed_area = triangle_area(*(elbow_points[index] for index in triangle))
        ratio = posed_area / rest_area
        require(ratio >= MIN_TRIANGLE_AREA_RATIO, f"Historic Elbow110 triangle {triangle_index} area ratio {ratio}")
        historic_triangles[str(triangle_index)] = {"vertices": list(triangle), "areaRatio": ratio}
    historic_edges = {}
    for first, second in HISTORIC_ELBOW_EDGES:
        rest_length = (context["restPositions"][first] - context["restPositions"][second]).length
        posed_length = (elbow_points[first] - elbow_points[second]).length
        ratio = posed_length / rest_length
        require(MIN_EDGE_LENGTH_RATIO <= ratio <= MAX_EDGE_LENGTH_RATIO, f"Historic Elbow110 edge {(first, second)} ratio {ratio}")
        historic_edges[f"{first}-{second}"] = {"vertices": [first, second], "lengthRatio": ratio}
    reset_pose(rig)
    return {
        "status": "PASS",
        "poseCount": len(STATIC_POSES),
        "poses": reports,
        "bowDrawPoseSnapshot": {
            "path": str(BOW_DRAW_POSE_SNAPSHOT.relative_to(PROJECT_ROOT)),
            "sha256": sha256(BOW_DRAW_POSE_SNAPSHOT),
            "source": bow_draw_pose_snapshot_contract()["source"],
        },
        "restMirrorSurfaceSampleCount": len(context["mirrorSamples"]),
        "restMaximumMirrorProjectionDistanceMeters": context["restMirrorErrorMeters"],
        "historicElbow110Regression": {
            "status": "PASS",
            "triangles": historic_triangles,
            "edges": historic_edges,
        },
        "limits": {
            "minimumTriangleAreaRatio": MIN_TRIANGLE_AREA_RATIO,
            "edgeLengthRatio": [MIN_EDGE_LENGTH_RATIO, MAX_EDGE_LENGTH_RATIO],
            "minimumCrossSectionRetention": MIN_CROSS_SECTION_RETENTION,
            "maximumMirrorErrorMeters": MAX_MIRROR_ERROR_METERS,
            "maximumAbsoluteRestMirrorErrorMeters": MAX_REST_MIRROR_ERROR_METERS,
            "mirrorMetric": "for each +X rest vertex, reflect and nearest-project to the -X rest surface, retain deterministic triangle+barycentrics, interpolate posed displacement, reflect that displacement, and compare fields",
            "maximumPenetrationMeters": MAX_PENETRATION_METERS,
            "elbowSupportLocalRotationToleranceDegrees": SUPPORT_LOCAL_ROTATION_TOLERANCE_DEGREES,
        },
    }


def normalized_action_name(name: str) -> str:
    for clip_name in CLIPS:
        if name == clip_name or name.endswith("|" + clip_name) or name.endswith("_" + clip_name):
            return clip_name
    return name


def iter_action_fcurves(action: bpy.types.Action):
    """Yield Blender 5.2 layered-action curves without relying on legacy APIs."""
    for layer in action.layers:
        for strip in layer.strips:
            if strip.type != "KEYFRAME":
                continue
            for channelbag in strip.channelbags:
                yield from channelbag.fcurves


def action_curve_contract_report(
    action: bpy.types.Action,
    clip_name: str,
    bone_names: Iterable[str],
) -> dict[str, object]:
    """Prove the frozen FBX action has exact per-frame 68-bone coverage."""
    bone_names = tuple(bone_names)
    frame_start, frame_end, _loop = CLIPS[clip_name]
    expected_frames = set(range(frame_start, frame_end + 1))
    expected_bone_keys = {
        (f'pose.bones["{bone_name}"].{property_name}', component)
        for bone_name in bone_names
        for property_name, component_count in (
            ("location", 3),
            ("rotation_quaternion", 4),
            ("scale", 3),
        )
        for component in range(component_count)
    }
    expected_object_keys = {
        *(("location", component) for component in range(3)),
        *(("rotation_euler", component) for component in range(3)),
        *(("scale", component) for component in range(3)),
    }
    curves = list(iter_action_fcurves(action))
    actual_keys = [(curve.data_path, int(curve.array_index)) for curve in curves]
    require(len(actual_keys) == len(set(actual_keys)), f"{clip_name} contains duplicate action channels")
    expected_keys = expected_bone_keys | expected_object_keys
    require(set(actual_keys) == expected_keys, f"{clip_name} action curve topology changed: {set(actual_keys) ^ expected_keys}")
    maximum_non_integer_error = 0.0
    maximum_object_identity_error = 0.0
    for curve in curves:
        require(not curve.modifiers, f"{clip_name} action curve {curve.data_path}[{curve.array_index}] has a modifier")
        frames = []
        for point in curve.keyframe_points:
            rounded = round(float(point.co.x))
            maximum_non_integer_error = max(maximum_non_integer_error, abs(float(point.co.x) - rounded))
            require(abs(float(point.co.x) - rounded) <= 1e-7, f"{clip_name} has a non-integer key time")
            require(point.interpolation == "LINEAR", f"{clip_name} has non-linear interpolation")
            frames.append(rounded)
        require(len(frames) == len(set(frames)), f"{clip_name} has duplicate per-curve frames")
        require(set(frames) == expected_frames, f"{clip_name} has missing/extra per-curve frames")
        if (curve.data_path, int(curve.array_index)) in expected_object_keys:
            expected_value = 1.0 if curve.data_path == "scale" else 0.0
            for frame in expected_frames:
                error = abs(float(curve.evaluate(frame)) - expected_value)
                maximum_object_identity_error = max(maximum_object_identity_error, error)
                require(error <= 1e-6, f"{clip_name} animates the imported armature object")
    slots = list(action.slots)
    require(len(slots) == 1, f"{clip_name} imported with {len(slots)} action slots instead of one")
    slot = slots[0]
    require(slot.target_id_type == "OBJECT", f"{clip_name} action slot targets {slot.target_id_type}")
    require(slot.identifier == "OBSlot", f"{clip_name} action slot identifier changed: {slot.identifier}")
    return {
        "status": "PASS",
        "actionSlotIdentifier": slot.identifier,
        "actionSlotTargetIdType": slot.target_id_type,
        "boneFcurveCount": len(expected_bone_keys),
        "armatureObjectFcurveCount": len(expected_object_keys),
        "totalFcurveCount": len(curves),
        "keyedBoneCount": len(bone_names),
        "exactIntegerFrameSetPerCurve": True,
        "allLinear": True,
        "maximumNonIntegerFrameError": maximum_non_integer_error,
        "maximumArmatureObjectIdentityError": maximum_object_identity_error,
    }


def bind_action_slot(
    rig: bpy.types.Object,
    action: bpy.types.Action,
    clip_name: str,
) -> dict[str, object]:
    """Explicitly bind Blender 5.2's imported OBJECT slot to the target rig."""
    if rig.animation_data is None:
        rig.animation_data_create()
    slots = list(action.slots)
    require(len(slots) == 1, f"{clip_name} imported with {len(slots)} action slots instead of one")
    slot = slots[0]
    require(slot.target_id_type == "OBJECT", f"{clip_name} action slot targets {slot.target_id_type}")
    require(slot.identifier == "OBSlot", f"{clip_name} action slot identifier changed: {slot.identifier}")
    rig.animation_data.action = action
    # Assignment of `action` alone leaves action_slot null for the first FBX
    # action in Blender 5.2, silently evaluating every requested frame at rest.
    rig.animation_data.action_slot = slot
    require(rig.animation_data.action == action, f"{clip_name} action assignment failed")
    require(rig.animation_data.action_slot == slot, f"{clip_name} action-slot assignment failed")
    bpy.context.view_layer.update()
    return {
        "status": "PASS_EXPLICIT_OBJECT_SLOT_BOUND",
        "identifier": slot.identifier,
        "targetIdType": slot.target_id_type,
    }


def evaluated_action_motion_report(
    rig: bpy.types.Object,
    clip_name: str,
) -> dict[str, object]:
    """Reject an unbound/rest-pose-repeat clip before any deformation scan."""
    frame_start, frame_end, _loop = CLIPS[clip_name]
    baseline: dict[str, Matrix] | None = None
    per_bone_maximum = {bone.name: 0.0 for bone in rig.pose.bones}
    for frame in range(frame_start, frame_end + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        current = {bone.name: bone.matrix.copy() for bone in rig.pose.bones}
        if baseline is None:
            baseline = current
            continue
        for bone_name, matrix in current.items():
            difference = max(
                abs(float(matrix[row][column]) - float(baseline[bone_name][row][column]))
                for row in range(4)
                for column in range(4)
            )
            per_bone_maximum[bone_name] = max(per_bone_maximum[bone_name], difference)
    require(baseline is not None, f"{clip_name} has no sampled animation frames")
    maximum = max(per_bone_maximum.values())
    moving_bones = sorted(name for name, delta in per_bone_maximum.items() if delta > 1e-5)
    require(maximum > 1e-5 and moving_bones, f"{clip_name} evaluates as repeated rest pose; action slot is not active")
    return {
        "status": "PASS_NONZERO_EVALUATED_MOTION",
        "sampledFrames": frame_end - frame_start + 1,
        "maximumBoneMatrixComponentDeltaFromFirstFrame": maximum,
        "movingBoneCount": len(moving_bones),
        "movingBones": moving_bones,
        "minimumRequiredDelta": 1e-5,
    }


def import_animation_actions(target_rig: bpy.types.Object) -> tuple[dict[str, bpy.types.Action], dict[str, object]]:
    before_objects = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(
        filepath=str(ANIMATION_FBX),
        use_anim=True,
        automatic_bone_orientation=False,
        ignore_leaf_bones=False,
    )
    imported_objects = [obj for obj in bpy.data.objects if obj not in before_objects]
    imported_armatures = [obj for obj in imported_objects if obj.type == "ARMATURE"]
    require(len(imported_armatures) == 1, f"Animation import created {len(imported_armatures)} armatures")
    animation_rig = imported_armatures[0]
    require(set(bone.name for bone in animation_rig.data.bones) == set(bone.name for bone in target_rig.data.bones), "Animation rig bone set differs from bound rig")
    imported_actions = [action for action in bpy.data.actions if action not in before_actions]
    actions: dict[str, bpy.types.Action] = {}
    for action in imported_actions:
        normalized = normalized_action_name(action.name)
        if normalized in CLIPS and normalized not in actions:
            actions[normalized] = action
    require(set(actions) == set(CLIPS), f"Imported animation action set changed: {set(actions) ^ set(CLIPS)}")
    bone_names = tuple(sorted(bone.name for bone in target_rig.data.bones))
    curve_reports = {
        clip_name: action_curve_contract_report(actions[clip_name], clip_name, bone_names)
        for clip_name in CLIPS
    }
    other_imported_objects = [obj for obj in imported_objects if obj != animation_rig]
    armature_data = animation_rig.data
    bpy.data.objects.remove(animation_rig, do_unlink=True)
    if armature_data.users == 0:
        bpy.data.armatures.remove(armature_data)
    for obj in other_imported_objects:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    evaluated_binding_reports = {}
    for clip_name in CLIPS:
        evaluated_binding_reports[clip_name] = {
            "binding": bind_action_slot(target_rig, actions[clip_name], clip_name),
            "evaluatedMotion": evaluated_action_motion_report(target_rig, clip_name),
        }
    reset_pose(target_rig)
    return actions, {
        "fbx": str(ANIMATION_FBX.relative_to(PROJECT_ROOT)),
        "fbxSha256": sha256(ANIMATION_FBX),
        "manifest": str(ANIMATION_MANIFEST.relative_to(PROJECT_ROOT)),
        "manifestSha256": sha256(ANIMATION_MANIFEST),
        "hashStatus": "FROZEN_AFTER_THREE_BYTE_IDENTICAL_EXPORTS_AND_ROUND_TRIP_PASS",
        "actions": sorted(actions),
        "curveTopology": curve_reports,
        "evaluatedActionBinding": evaluated_binding_reports,
    }


def sole_frame_metrics(points: list[Vector], sole_masks: dict[str, list[int]]) -> dict[str, object]:
    result = {}
    for side, indices in sole_masks.items():
        z_values = [points[index].z for index in indices]
        contact_fraction = sum(abs(value) <= GROUND_CONTACT_TOLERANCE_METERS for value in z_values) / len(z_values)
        centroid = sum((points[index] for index in indices), Vector()) / len(indices)
        result[side] = {
            "minimumZ": min(z_values),
            "maximumZ": max(z_values),
            "contactFraction": contact_fraction,
            "centroid": list(centroid),
            "grounded": contact_fraction >= MIN_IDLE_SOLE_CONTACT_FRACTION,
        }
    return result


def planted_slide_metrics(frames: list[dict[str, object]]) -> dict[str, object]:
    maximum_slide = 0.0
    per_side = {}
    for side in ("Left", "Right"):
        episodes: list[list[Vector]] = []
        current: list[Vector] = []
        for frame in frames:
            metrics = frame["sole"][side]
            if metrics["grounded"]:
                current.append(Vector(metrics["centroid"][:2]))
            else:
                if current:
                    episodes.append(current)
                current = []
        if current:
            episodes.append(current)
        side_maximum = 0.0
        for episode in episodes:
            origin = episode[0]
            side_maximum = max(side_maximum, max((point - origin).length for point in episode))
        maximum_slide = max(maximum_slide, side_maximum)
        per_side[side] = {
            "contactEpisodes": len(episodes),
            "maximumSlideMeters": side_maximum,
        }
    require(maximum_slide <= MAX_PLANTED_MARKER_SLIDE_METERS, f"Planted sole marker slides {maximum_slide}m")
    return {
        "maximumSlideMeters": maximum_slide,
        "limitMeters": MAX_PLANTED_MARKER_SLIDE_METERS,
        "perSide": per_side,
    }


def run_animation_deformation_gates(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    allowed_groups: list[str],
    actions: dict[str, bpy.types.Action],
) -> dict[str, object]:
    context = rest_deformation_context(obj, rig, allowed_groups)
    reports: dict[str, object] = {}
    total_frames = 0
    for clip_name, (frame_start, frame_end, loop) in CLIPS.items():
        action = actions[clip_name]
        binding = bind_action_slot(rig, action, clip_name)
        evaluated_motion = evaluated_action_motion_report(rig, clip_name)
        frames: list[dict[str, object]] = []
        for frame in range(frame_start, frame_end + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            support_pose = elbow_support_pose_report(rig)
            gate = deformation_frame_report(
                obj,
                rig,
                context["restPositions"],
                context["triangles"],
                context["edges"],
                context["restTriangleAreas"],
                context["restEdgeLengths"],
                context["dominantRegions"],
                context["restRegionRadii"],
                None,
                True,
            )
            points = evaluated_positions(obj)
            sole = sole_frame_metrics(points, context["soleMasks"])
            require(min(sole[side]["minimumZ"] for side in sole) >= -MAX_SOLE_BELOW_GROUND_METERS, f"{clip_name} frame {frame} places skin below ground")
            if clip_name != "Death":
                require(gate["heightMeters"] >= context["restHeightMeters"] * 0.85, f"{clip_name} frame {frame} collapses below 85% rest height")
            frames.append({"frame": frame, "deformation": gate, "sole": sole, "elbowSupportPose": support_pose})
            total_frames += 1
        grounded_fraction = sum(any(frame["sole"][side]["grounded"] for side in ("Left", "Right")) for frame in frames) / len(frames)
        if clip_name == "CombatIdle":
            require(all(frame["sole"]["Left"]["grounded"] and frame["sole"]["Right"]["grounded"] for frame in frames), "CombatIdle loses full sole contact")
        if clip_name == "LocomotionForward":
            require(grounded_fraction >= MIN_LOCOMOTION_GROUNDED_FRACTION, f"Locomotion grounded fraction {grounded_fraction} is below {MIN_LOCOMOTION_GROUNDED_FRACTION}")
        if clip_name != "Death":
            require(grounded_fraction > 0.0, f"{clip_name} has no grounded skin frame")
        slide = planted_slide_metrics(frames)
        reports[clip_name] = {
            "frameStart": frame_start,
            "frameEnd": frame_end,
            "sampledFrames": len(frames),
            "loop": loop,
            "actionBinding": binding,
            "evaluatedMotion": evaluated_motion,
            "groundedFrameFraction": grounded_fraction,
            "plantedSlide": slide,
            "minimumTriangleAreaRatio": min(frame["deformation"]["minimumTriangleAreaRatio"] for frame in frames),
            "minimumEdgeLengthRatio": min(frame["deformation"]["minimumEdgeLengthRatio"] for frame in frames),
            "maximumEdgeLengthRatio": max(frame["deformation"]["maximumEdgeLengthRatio"] for frame in frames),
            "minimumCrossSectionRetention": min(frame["deformation"]["minimumCrossSectionRetention"] for frame in frames),
            "maximumPenetrationMeters": max(frame["deformation"]["selfPenetration"]["maximumApproximateDepthMeters"] for frame in frames),
            "minimumSkinZ": min(frame["sole"][side]["minimumZ"] for frame in frames for side in ("Left", "Right")),
        }
    reset_pose(rig)
    return {
        "status": "PASS",
        "clipCount": len(reports),
        "sampledFrameCount": total_frames,
        "clips": reports,
        "groundingLimits": {
            "groundContactToleranceMeters": GROUND_CONTACT_TOLERANCE_METERS,
            "idleSoleContactFraction": MIN_IDLE_SOLE_CONTACT_FRACTION,
            "locomotionGroundedFrameFraction": MIN_LOCOMOTION_GROUNDED_FRACTION,
            "maximumSoleBelowGroundMeters": MAX_SOLE_BELOW_GROUND_METERS,
            "maximumPlantedMarkerSlideMeters": MAX_PLANTED_MARKER_SLIDE_METERS,
        },
    }
