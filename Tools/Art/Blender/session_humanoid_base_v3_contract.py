#!/usr/bin/env python3
"""Shared contract for the Blender Foundation SessionHumanoidBaseV3 proof."""

from __future__ import annotations

from dataclasses import dataclass
from hashlib import sha256
import math
from pathlib import Path
from typing import Callable

import bpy
from mathutils import Matrix, Vector


PROJECT_ROOT = Path(__file__).resolve().parents[3]
RIG_BLEND = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/SessionHumanoidRigV2/SessionHumanoidRigV2.blend"
)
RIG_FBX = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/SessionHumanoidRigV2/Outputs/SessionHumanoidRigV2.fbx"
)
RIG_MANIFEST = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/SessionHumanoidRigV2/Outputs/SessionHumanoidRigV2.manifest.json"
)
SUBSET_BLEND = (
    PROJECT_ROOT
    / "SourceArt/ThirdParty/BlenderFoundation/HumanBaseMeshes_1.4.1/SelectedSubset"
    / "HumanBaseMeshes_StylizedBodies_v1.4.1.blend"
)
SUBSET_MANIFEST = SUBSET_BLEND.with_suffix(".manifest.json")
OUTPUT_ROOT = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidBaseV3"
OUTPUTS = OUTPUT_ROOT / "Outputs"
BLEND_PATH = OUTPUT_ROOT / "SessionHumanoidBaseV3.blend"
MANIFEST_PATH = OUTPUTS / "SessionHumanoidBaseV3.manifest.json"
DEFORMATION_ROOT = OUTPUTS / "Deformation"
DEFORMATION_REPORT = DEFORMATION_ROOT / "SessionHumanoidBaseV3DeformationProof.json"
MANUAL_REVIEW_PATH = DEFORMATION_ROOT / "SessionHumanoidBaseV3.manual-review.json"

EXPECTED_RIG_BLEND_SHA256 = "6268387e0e2c8db321b0b292954ebca94422b405c667a6f3ee5ecd8b49f6b783"
EXPECTED_RIG_FBX_SHA256 = "beb653aba9873141418d95b38cfda2151c1852e2e7b0df9aba2eeaf85c9eed66"
EXPECTED_RIG_MANIFEST_SHA256 = "7f8468fea292a1416c5d8ac697fd24652bba84e77808187647cf4eb5f3d3a6e6"
EXPECTED_SUBSET_SHA256 = "f9cb3640ddda86ec5681ed6f9260cb08797a5d9cfd59068f675c1442d9bea265"
CANONICAL_RIG_NAME = "SessionHumanoidRigV2"
CANONICAL_META_NAME = "SessionHumanoidRigV2_RigifyHumanMeta_NotExported"
CANONICAL_CONTROL_NAME = "SessionHumanoidRigV2_RigifyControls_NotExported"
CANONICAL_HEIGHT = 1.78

VARIANTS = ("Masculine", "Feminine")
SOURCE_NAMES = {
    "Masculine": (
        "GEO-body_male_stylized",
        "GEO-body_male_stylized.eye.L",
        "GEO-body_male_stylized.eye.R",
    ),
    "Feminine": (
        "GEO-body_female_stylized",
        "GEO-body_female_stylized.eye.L",
        "GEO-body_female_stylized.eye.R",
    ),
}
BODY_NAMES = {variant: f"SessionHumanoidBaseV3_{variant}_Body" for variant in VARIANTS}
RIG_NAMES = {variant: f"SessionHumanoidBaseV3_{variant}_Rig" for variant in VARIANTS}
EYE_NAMES = {
    variant: (
        f"SessionHumanoidBaseV3_{variant}_Eye_L",
        f"SessionHumanoidBaseV3_{variant}_Eye_R",
    )
    for variant in VARIANTS
}

HEAD_FACE_SETS = {6, 27, 28, 29, 37, 44, 45, 46, 47, 48, 49}
PELVIS_FACE_SETS = {258}
TORSO_FACE_SETS = {259}
SHOULDER_FACE_SETS = {260}
UPPER_ARM_FACE_SETS = {263}
FOREARM_FACE_SETS = {262}
HAND_FACE_SETS = {272, 273, 274, 275, 276, 277, 278, 279, 280, 281, 282, 283, 284, 285, 286, 359}
THIGH_FACE_SETS = {256}
CALF_FACE_SETS = {257}
FOOT_FACE_SETS = {288, 316, 317, 318, 319, 320, 321, 322}
PALM_FACE_SETS = {273}
ANKLE_FACE_SETS = {288}
MAIN_FOOT_FACE_SETS = {316}
TOE_FACE_SETS = {317, 318, 319, 320, 321, 322}

# The official mesh ships one sculpt face set per phalanx.  These chains were
# resolved from the source topology's adjacency (palm -> proximal -> middle ->
# distal), then checked on both selected variants.  Keeping this explicit avoids
# the unstable nearest-bone assignment that previously crossed splayed fingers.
FINGER_FACE_SET_CHAINS = {
    "Thumb": (359, 285, 284),
    "Index": (278, 283, 282),
    "Middle": (277, 279, 272),
    "Ring": (280, 274, 275),
    "Little": (281, 286, 276),
}
ALL_KNOWN_FACE_SETS = (
    HEAD_FACE_SETS
    | PELVIS_FACE_SETS
    | TORSO_FACE_SETS
    | SHOULDER_FACE_SETS
    | UPPER_ARM_FACE_SETS
    | FOREARM_FACE_SETS
    | HAND_FACE_SETS
    | THIGH_FACE_SETS
    | CALF_FACE_SETS
    | FOOT_FACE_SETS
)

TWIST_BONES = (
    "LeftUpperArmTwist",
    "LeftForearmTwist",
    "RightUpperArmTwist",
    "RightForearmTwist",
    "LeftThighTwist",
    "LeftCalfTwist",
    "RightThighTwist",
    "RightCalfTwist",
)
LEGACY_SUPPORT_BONES = ("LeftElbowSupport", "RightElbowSupport")

FINGER_PREFIXES = ("Thumb", "Index", "Middle", "Ring", "Little")
FINGER_SEGMENTS = ("Proximal", "Intermediate", "Distal")


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def file_sha256(path: Path) -> str:
    digest = sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def smoothstep(lower: float, upper: float, value: float) -> float:
    if upper <= lower:
        return float(value >= upper)
    t = max(0.0, min(1.0, (value - lower) / (upper - lower)))
    return t * t * (3.0 - 2.0 * t)


def vertex_face_sets(mesh: bpy.types.Mesh) -> list[set[int]]:
    attribute = mesh.attributes.get(".sculpt_face_set")
    require(attribute is not None and attribute.domain == "FACE", "Body lost sculpt face sets")
    memberships = [set() for _ in mesh.vertices]
    for polygon in mesh.polygons:
        value = int(attribute.data[polygon.index].value)
        for index in polygon.vertices:
            memberships[index].add(value)
    require(all(memberships), "At least one body vertex has no face-set membership")
    return memberships


def normalize_top_four(weights: dict[str, float]) -> dict[str, float]:
    retained = sorted(
        ((name, max(0.0, float(weight))) for name, weight in weights.items() if weight > 1e-8),
        key=lambda item: (-item[1], item[0]),
    )[:4]
    total = sum(weight for _name, weight in retained)
    require(total > 1e-8, "Weight row has no positive influence")
    return {name: weight / total for name, weight in retained}


def distance_to_segment(point: Vector, head: Vector, tail: Vector) -> float:
    axis = tail - head
    if axis.length_squared <= 1e-12:
        return (point - head).length
    t = max(0.0, min(1.0, (point - head).dot(axis) / axis.length_squared))
    return (point - (head + axis * t)).length


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


def rotate_pair(
    rig: bpy.types.Object,
    left: str,
    right: str,
    axis: Vector,
    left_degrees: float,
    right_degrees: float,
) -> None:
    rotate_pose_bone_armature_space(rig, left, axis, left_degrees)
    rotate_pose_bone_armature_space(rig, right, axis, right_degrees)


def rotate_longitudinal(rig: bpy.types.Object, bone_name: str, degrees: float) -> None:
    pose_bone = rig.pose.bones[bone_name]
    axis = (pose_bone.tail - pose_bone.head).normalized()
    rotate_pose_bone_armature_space(rig, bone_name, axis, degrees)


def set_correctives(rig: bpy.types.Object, names: tuple[str, ...]) -> None:
    active = set(names)
    for key in tuple(rig.keys()):
        if key.startswith("PSC_"):
            rig[key] = 1.0 if key in active else 0.0
    rig.update_tag(refresh={"OBJECT"})
    bpy.context.view_layer.update()


def reset_pose(rig: bpy.types.Object) -> None:
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    set_correctives(rig, ())
    bpy.context.view_layer.update()


def pose_neutral(_rig: bpy.types.Object) -> None:
    return


def pose_shoulders_down(rig: bpy.types.Object) -> None:
    # Downward adduction remains centered at the glenohumeral joint; the
    # clavicular split below is reserved for forward reach and elevation.
    rotate_pair(rig, "LeftUpperArm", "RightUpperArm", Vector((0.0, 1.0, 0.0)), 75.0, -75.0)
    set_correctives(rig, ("PSC_LeftShoulder", "PSC_RightShoulder"))


def pose_shoulders_forward(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftShoulder", "RightShoulder", Vector((0.0, 0.0, 1.0)), -18.0, 18.0)
    rotate_pair(rig, "LeftUpperArm", "RightUpperArm", Vector((0.0, 0.0, 1.0)), -72.0, 72.0)
    set_correctives(rig, ("PSC_LeftShoulder", "PSC_RightShoulder"))


def pose_shoulders_overhead(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftShoulder", "RightShoulder", Vector((0.0, 1.0, 0.0)), -18.0, 18.0)
    rotate_pair(rig, "LeftUpperArm", "RightUpperArm", Vector((0.0, 1.0, 0.0)), -42.0, 42.0)
    set_correctives(rig, ("PSC_LeftShoulder", "PSC_RightShoulder"))


def pose_elbows(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftLowerArm", "RightLowerArm", Vector((0.0, 0.0, 1.0)), -110.0, 110.0)
    set_correctives(rig, ("PSC_LeftElbow", "PSC_RightElbow"))


def pose_pronation(rig: bpy.types.Object) -> None:
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        rotate_longitudinal(rig, f"{side}LowerArm", sign * 36.0)
        rotate_longitudinal(rig, f"{side}ForearmTwist", sign * 44.0)
        rotate_longitudinal(rig, f"{side}Hand", sign * 44.0)
    set_correctives(rig, ("PSC_LeftWrist", "PSC_RightWrist"))


def pose_elbows_pronated(rig: bpy.types.Object) -> None:
    pose_elbows(rig)
    pose_pronation(rig)
    set_correctives(
        rig,
        ("PSC_LeftElbow", "PSC_RightElbow", "PSC_LeftWrist", "PSC_RightWrist"),
    )


def pose_wrists(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftHand", "RightHand", Vector((0.0, 1.0, 0.0)), 60.0, -60.0)
    set_correctives(rig, ("PSC_LeftWrist", "PSC_RightWrist"))


def pose_fists(rig: bpy.types.Object) -> None:
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        for prefix in FINGER_PREFIXES:
            # The thumb's saddle joint opposes and flexes through a materially
            # smaller arc than the four long digits.  Driving it through the
            # long-finger 175-degree cumulative curl is not an anatomical fist.
            curl = (34.0, 42.0, 30.0) if prefix == "Thumb" else (55.0, 65.0, 55.0)
            for segment, degrees in zip(FINGER_SEGMENTS, curl):
                rotate_pose_bone_armature_space(
                    rig,
                    f"{side}{prefix}{segment}",
                    Vector((0.0, 1.0, 0.0)),
                    sign * degrees,
                )


def pose_hips_knees(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftUpperLeg", "RightUpperLeg", Vector((1.0, 0.0, 0.0)), -90.0, -90.0)
    rotate_pair(rig, "LeftLowerLeg", "RightLowerLeg", Vector((1.0, 0.0, 0.0)), 110.0, 110.0)
    set_correctives(
        rig,
        ("PSC_LeftHip", "PSC_RightHip", "PSC_LeftKnee", "PSC_RightKnee"),
    )


def pose_knees(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftLowerLeg", "RightLowerLeg", Vector((1.0, 0.0, 0.0)), 120.0, 120.0)
    set_correctives(rig, ("PSC_LeftKnee", "PSC_RightKnee"))


def pose_feet(rig: bpy.types.Object) -> None:
    rotate_pair(rig, "LeftFoot", "RightFoot", Vector((1.0, 0.0, 0.0)), 25.0, 25.0)
    rotate_pair(rig, "LeftToes", "RightToes", Vector((1.0, 0.0, 0.0)), 35.0, 35.0)
    set_correctives(rig, ("PSC_LeftAnkle", "PSC_RightAnkle"))


@dataclass(frozen=True)
class PoseSpec:
    name: str
    apply: Callable[[bpy.types.Object], None]
    minimum_area_ratio: float = 0.15
    minimum_edge_ratio: float = 0.25
    maximum_edge_ratio: float = 4.0


POSES = (
    PoseSpec("NeutralTPose", pose_neutral),
    PoseSpec("ShouldersDown75", pose_shoulders_down),
    PoseSpec("ShouldersForward90", pose_shoulders_forward),
    PoseSpec("ShouldersOverhead150", pose_shoulders_overhead),
    PoseSpec("ElbowsFlex110", pose_elbows),
    PoseSpec("ForearmPronation80", pose_pronation),
    PoseSpec("ElbowsFlex110Pronation80", pose_elbows_pronated),
    PoseSpec("WristsFlexExtension60", pose_wrists),
    PoseSpec("HandsFist", pose_fists),
    PoseSpec("Hips90Knees110", pose_hips_knees),
    PoseSpec("KneesFlex120", pose_knees),
    PoseSpec("FeetDorsi25Toes35", pose_feet),
)


def evaluated_positions(obj: bpy.types.Object) -> list[Vector]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
    try:
        return [evaluated.matrix_world @ vertex.co for vertex in mesh.vertices]
    finally:
        evaluated.to_mesh_clear()


def triangle_area(first: Vector, second: Vector, third: Vector) -> float:
    return (second - first).cross(third - first).length * 0.5


def deformation_metrics(
    obj: bpy.types.Object,
    rest_positions: list[Vector],
    posed_positions: list[Vector],
) -> dict[str, object]:
    require(len(rest_positions) == len(posed_positions) == len(obj.data.vertices), "Evaluated topology changed")
    obj.data.calc_loop_triangles()
    minimum_area_ratio = float("inf")
    minimum_area_triangle: dict[str, object] = {}
    degenerate = 0
    for triangle in obj.data.loop_triangles:
        a, b, c = triangle.vertices
        rest_area = triangle_area(rest_positions[a], rest_positions[b], rest_positions[c])
        posed_area = triangle_area(posed_positions[a], posed_positions[b], posed_positions[c])
        require(rest_area > 1e-12, f"Rest triangle {triangle.index} is degenerate")
        ratio = posed_area / rest_area
        if ratio < minimum_area_ratio:
            minimum_area_ratio = ratio
            minimum_area_triangle = {
                "triangleIndex": triangle.index,
                "vertices": [int(a), int(b), int(c)],
                "restAreaSquareMeters": rest_area,
                "posedAreaSquareMeters": posed_area,
            }
        degenerate += posed_area <= 1e-12

    minimum_edge_ratio = float("inf")
    maximum_edge_ratio = 0.0
    minimum_edge: dict[str, object] = {}
    maximum_edge: dict[str, object] = {}
    for edge in obj.data.edges:
        first, second = edge.vertices
        rest_length = (rest_positions[first] - rest_positions[second]).length
        posed_length = (posed_positions[first] - posed_positions[second]).length
        require(rest_length > 1e-10, f"Rest edge {edge.index} is degenerate")
        ratio = posed_length / rest_length
        if ratio < minimum_edge_ratio:
            minimum_edge_ratio = ratio
            minimum_edge = {
                "edgeIndex": edge.index,
                "vertices": [int(first), int(second)],
                "restLengthMeters": rest_length,
                "posedLengthMeters": posed_length,
            }
        if ratio > maximum_edge_ratio:
            maximum_edge_ratio = ratio
            maximum_edge = {
                "edgeIndex": edge.index,
                "vertices": [int(first), int(second)],
                "restLengthMeters": rest_length,
                "posedLengthMeters": posed_length,
            }

    minimum = Vector(tuple(min(point[axis] for point in posed_positions) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in posed_positions) for axis in range(3)))
    return {
        "minimumTriangleAreaRatio": minimum_area_ratio,
        "minimumTriangle": minimum_area_triangle,
        "minimumEdgeLengthRatio": minimum_edge_ratio,
        "minimumEdge": minimum_edge,
        "maximumEdgeLengthRatio": maximum_edge_ratio,
        "maximumEdge": maximum_edge,
        "degenerateTriangles": degenerate,
        "heightMeters": maximum.z - minimum.z,
        "widthMeters": maximum.x - minimum.x,
        "depthMeters": maximum.y - minimum.y,
    }
