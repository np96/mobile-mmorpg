#!/usr/bin/env python3
"""Shared, source-level contract for SessionHumanoidBaseV1 binding checks.

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
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import intersect_ray_tri


PROJECT_ROOT = Path(__file__).resolve().parents[3]
REPOSITORY_ROOT = PROJECT_ROOT.parent
BASE_SOURCE_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidBaseV1"
BASE_OUTPUT_DIR = BASE_SOURCE_DIR / "Outputs"
RIG_OUTPUT_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidRigV1/Outputs"
ANIMATION_OUTPUT_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidAnimationProofV1/Outputs"
BOUND_BLEND = BASE_SOURCE_DIR / "SessionHumanoidBaseV1_Bound.blend"
BINDING_MANIFEST = BASE_OUTPUT_DIR / "SessionHumanoidBaseV1_BindingManifest.json"
BINDING_REPORT = (
    REPOSITORY_ROOT
    / "outputs/art/blender/session-humanoid-base-v1-bound/SessionHumanoidBaseV1BindingReport.json"
)
ROUNDTRIP_REPORT = (
    REPOSITORY_ROOT
    / "outputs/art/blender/session-humanoid-base-v1-bound/SessionHumanoidBaseV1FbxRoundTrip.json"
)
EVIDENCE_DIR = REPOSITORY_ROOT / "outputs/captures/20260820-session-humanoid-base-v1-bound"
RIG_FBX = RIG_OUTPUT_DIR / "SessionHumanoidRigV1.fbx"
RIG_MANIFEST = RIG_OUTPUT_DIR / "SessionHumanoidRigV1.manifest.json"
ANIMATION_FBX = ANIMATION_OUTPUT_DIR / "SessionHumanoidAnimationProofV1.fbx"
ANIMATION_MANIFEST = ANIMATION_OUTPUT_DIR / "SessionHumanoidAnimationProofV1.manifest.json"

VARIANTS = ("Masculine", "Feminine")
BOUND_MESH_NAMES = {
    variant: f"SessionHumanoidBaseV1_{variant}_BoundSource" for variant in VARIANTS
}
BOUND_FBX_PATHS = {
    variant: BASE_OUTPUT_DIR / f"SessionHumanoidBaseV1_{variant}_Bound.fbx"
    for variant in VARIANTS
}

EXPECTED_VERTICES = 13380
EXPECTED_TRIANGLES = 26756
EXPECTED_RIG_JOINTS = 66
EXPECTED_DEFORM_GROUPS = 55
EXPECTED_RIG_FBX_SHA256 = "261f0e389a7358a12bcdda1b3ae9a8fd19429c446a434fae7b2a7325f50e7d6d"
EXPECTED_RIG_MANIFEST_SHA256 = "3ec07eb3dc4f62c67383f1ee61bfcd74c9a2f62ce57a3e869841cdf732f33278"
EXPECTED_LANDMARK_ARTIFACT_SHA256 = "e55a921a1f3f18aa1c08bcbb98e31317a6c6ab2fc710fee36ea37c2c54636e79"
EXPECTED_MPFb_WEIGHTS_SHA256 = "9f4a773e74ce5ba08415b4f30070338e9f75bae5971b5075b10cd6b9caa58473"
EXPECTED_MPFb_TPOSE_SHA256 = "fefc2969c3e16049803e1ffea2a7cf9b7d98ec5eaeef42ca38dc6184241aacf8"
EXPECTED_ANIMATION_FBX_SHA256 = "7a958aed8fda2c54f63f5178379a1a5ce68fe4a64fad3466132565859b5ed320"
EXPECTED_ANIMATION_MANIFEST_SHA256 = "335a739332c52aebb9324ca42b7c506923204f315c97bdbeec690cb39b9115e6"

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
SESSION_DEFORM_GROUPS = tuple(dict.fromkeys((*MPFB_TO_SESSION.values(), *ZERO_INFLUENCE_FACE_GROUPS)))

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


def pose_bow_draw(rig: bpy.types.Object) -> None:
    pose_arms_forward(rig)
    rotate_pose_bone_armature_space(rig, "RightLowerArm", Vector((0.0, 0.0, 1.0)), 110.0)
    rotate_pose_bone_armature_space(rig, "RightHand", Vector((1.0, 0.0, 0.0)), -18.0)


def pose_pronation_positive(rig: bpy.types.Object) -> None:
    rotate_longitudinal(rig, "LeftLowerArm", 80.0)
    rotate_longitudinal(rig, "RightLowerArm", -80.0)


def pose_pronation_negative(rig: bpy.types.Object) -> None:
    rotate_longitudinal(rig, "LeftLowerArm", -80.0)
    rotate_longitudinal(rig, "RightLowerArm", 80.0)


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
        bpy.context.view_layer.update()
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
        reports[spec.name] = report
    reset_pose(rig)
    return {
        "status": "PASS",
        "poseCount": len(STATIC_POSES),
        "poses": reports,
        "restMirrorSurfaceSampleCount": len(context["mirrorSamples"]),
        "restMaximumMirrorProjectionDistanceMeters": context["restMirrorErrorMeters"],
        "limits": {
            "minimumTriangleAreaRatio": MIN_TRIANGLE_AREA_RATIO,
            "edgeLengthRatio": [MIN_EDGE_LENGTH_RATIO, MAX_EDGE_LENGTH_RATIO],
            "minimumCrossSectionRetention": MIN_CROSS_SECTION_RETENTION,
            "maximumMirrorErrorMeters": MAX_MIRROR_ERROR_METERS,
            "maximumAbsoluteRestMirrorErrorMeters": MAX_REST_MIRROR_ERROR_METERS,
            "mirrorMetric": "for each +X rest vertex, reflect and nearest-project to the -X rest surface, retain deterministic triangle+barycentrics, interpolate posed displacement, reflect that displacement, and compare fields",
            "maximumPenetrationMeters": MAX_PENETRATION_METERS,
        },
    }


def normalized_action_name(name: str) -> str:
    for clip_name in CLIPS:
        if name == clip_name or name.endswith("|" + clip_name) or name.endswith("_" + clip_name):
            return clip_name
    return name


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
    other_imported_objects = [obj for obj in imported_objects if obj != animation_rig]
    armature_data = animation_rig.data
    bpy.data.objects.remove(animation_rig, do_unlink=True)
    if armature_data.users == 0:
        bpy.data.armatures.remove(armature_data)
    for obj in other_imported_objects:
        if obj.name in bpy.data.objects:
            bpy.data.objects.remove(obj, do_unlink=True)
    return actions, {
        "fbx": str(ANIMATION_FBX.relative_to(PROJECT_ROOT)),
        "fbxSha256": sha256(ANIMATION_FBX),
        "manifest": str(ANIMATION_MANIFEST.relative_to(PROJECT_ROOT)),
        "manifestSha256": sha256(ANIMATION_MANIFEST),
        "hashStatus": "FROZEN_AFTER_THREE_BYTE_IDENTICAL_EXPORTS_AND_ROUND_TRIP_PASS",
        "actions": sorted(actions),
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
        if rig.animation_data is None:
            rig.animation_data_create()
        rig.animation_data.action = action
        frames: list[dict[str, object]] = []
        for frame in range(frame_start, frame_end + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
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
            frames.append({"frame": frame, "deformation": gate, "sole": sole})
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
