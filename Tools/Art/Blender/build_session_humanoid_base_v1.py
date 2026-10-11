#!/usr/bin/env python3
"""Build the MPFB-based SessionHumanoidBaseV1 clay intake checkpoint.

This is the single authoritative SessionHumanoidBaseV1 entry point. The prior
procedural loft experiment is retained only as a rejected .blend and verdict;
no loft/capsule anatomy is generated here.

Run with Blender 5.2 and official MPFB 2.0.17 enabled:

    Blender --background --python build_session_humanoid_base_v1.py -- --stage early

The early stage bakes helper-free masculine/feminine meshes, performs the
project-owned 1.780 m T-pose rest fit, creates non-production 20k clay review
proxies, and renders evidence.  The intact 26,756-triangle MPFB surface remains
the deformation source: class authoring will delete body faces hidden by fitted
gear before protected/manual LOD reduction.  This stage deliberately stops
before binding, textures, production LODs, FBX, or Unity integration.
"""

from __future__ import annotations

import argparse
import hashlib
import inspect
import json
import math
from pathlib import Path
import struct
import sys

import bmesh
import bpy
from bpy_extras.object_utils import world_to_camera_view
from mathutils import Vector
from mathutils.bvhtree import BVHTree

from bl_ext.blender_org.mpfb.services import HumanService, RigService, TargetService


PROJECT_ROOT = Path(__file__).resolve().parents[3]
SOURCE_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidBaseV1"
OUTPUT_DIR = SOURCE_DIR / "Outputs"
CHECKPOINT_BLEND = SOURCE_DIR / "SessionHumanoidBaseV1_MPFB_ClayCheckpoint.blend"
EVIDENCE_DIR = PROJECT_ROOT.parent / "outputs/captures/20260820-session-humanoid-base-v1-mpfb"
REPORT_DIR = PROJECT_ROOT.parent / "outputs/art/blender/session-humanoid-base-v1-mpfb"
INTAKE_REPORT = REPORT_DIR / "SessionHumanoidBaseV1MpfbIntake.json"
LANDMARK_REPORT = OUTPUT_DIR / "SessionHumanoidBaseV1_RigFitLandmarks.json"
BUILD_MANIFEST = OUTPUT_DIR / "SessionHumanoidBaseV1_BuildManifest.json"

MPFB_VERSION = "2.0.17"
MPFB_LICENSE_URL = "https://github.com/makehumancommunity/mpfb2/blob/master/LICENSE.md"
MPFB_ASSET_LICENSE_URL = "https://github.com/makehumancommunity/mpfb2/blob/master/LICENSE.ASSETS.md"
EXPECTED_BASE_OBJ_SHA256 = "8e761e6624b8f54536409135d1636da63b32486a90d4897f84e121d144f6fb4c"
EXPECTED_GAME_ENGINE_RIG_SHA256 = "b324ddb4b707721c19630bfadb87c3e3b38eed3b6e4590bb3cead43ea918f799"
EXPECTED_GAME_ENGINE_WEIGHTS_SHA256 = "9f4a773e74ce5ba08415b4f30070338e9f75bae5971b5075b10cd6b9caa58473"
EXPECTED_GAME_ENGINE_TPOSE_SHA256 = "fefc2969c3e16049803e1ffea2a7cf9b7d98ec5eaeef42ca38dc6184241aacf8"

TARGET_HEIGHT = 1.780
CLAY_REVIEW_TARGET_TRIANGLES = 20000
CLAY_REVIEW_TRIANGLE_RANGE = (16000, 22000)

CAMERA_YAW = 90.0
CAMERA_PITCH = 56.0
CAMERA_DISTANCE = 22.0
CAMERA_PIVOT_HEIGHT = 0.9
CAMERA_FORWARD_FOCUS = 2.3
CAMERA_RIGHT_FOCUS = 0.0
CAMERA_VERTICAL_FOV = 50.0
IDENTITY_DCC_SCALE = 1.0
HUNTER_RUNTIME_VISUAL_ROOT_SCALE = 1.46
HUNTER_RUNTIME_RELATION_SCALE = 1.08
HUNTER_RUNTIME_PRESENTATION_SCALE = HUNTER_RUNTIME_VISUAL_ROOT_SCALE * HUNTER_RUNTIME_RELATION_SCALE

RACE_MIX = {
    "african": 1.0 / 3.0,
    "asian": 1.0 / 3.0,
    "caucasian": 1.0 / 3.0,
}

VARIANTS = {
    "Masculine": {
        "gender": 0.84,
        "age": 0.50,
        "muscle": 0.69,
        "weight": 0.52,
        "height": 0.54,
        "proportions": 0.60,
        "cupsize": 0.50,
        "firmness": 0.50,
    },
    "Feminine": {
        "gender": 0.18,
        "age": 0.50,
        "muscle": 0.56,
        "weight": 0.50,
        "height": 0.52,
        "proportions": 0.44,
        "cupsize": 0.54,
        "firmness": 0.60,
    },
}

LANDMARK_BONES = (
    "Root",
    "pelvis",
    "spine_01",
    "spine_02",
    "spine_03",
    "neck_01",
    "head",
    "clavicle_l",
    "clavicle_r",
    "upperarm_l",
    "upperarm_r",
    "lowerarm_l",
    "lowerarm_r",
    "hand_l",
    "hand_r",
    "thumb_01_l",
    "thumb_02_l",
    "thumb_03_l",
    "thumb_01_r",
    "thumb_02_r",
    "thumb_03_r",
    "index_01_l",
    "index_02_l",
    "index_03_l",
    "index_01_r",
    "index_02_r",
    "index_03_r",
    "middle_01_l",
    "middle_02_l",
    "middle_03_l",
    "middle_01_r",
    "middle_02_r",
    "middle_03_r",
    "ring_01_l",
    "ring_02_l",
    "ring_03_l",
    "ring_01_r",
    "ring_02_r",
    "ring_03_r",
    "pinky_01_l",
    "pinky_02_l",
    "pinky_03_l",
    "pinky_01_r",
    "pinky_02_r",
    "pinky_03_r",
    "thigh_l",
    "thigh_r",
    "calf_l",
    "calf_r",
    "foot_l",
    "foot_r",
    "ball_l",
    "ball_r",
)


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


def reset_scene() -> None:
    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    for collection in list(bpy.data.collections):
        if collection.name != "Collection":
            bpy.data.collections.remove(collection)


def mpfb_paths() -> tuple[Path, Path]:
    service_path = Path(inspect.getfile(HumanService)).resolve()
    extension_root = service_path.parent.parent
    base_obj = extension_root / "data/3dobjs/base.obj"
    manifest = extension_root / "blender_manifest.toml"
    require(base_obj.is_file(), f"MPFB bundled base mesh is missing: {base_obj}")
    require(manifest.is_file(), f"MPFB extension manifest is missing: {manifest}")
    require(
        sha256(base_obj) == EXPECTED_BASE_OBJ_SHA256,
        "Installed MPFB base.obj differs from the audited MPFB 2.0.17 source",
    )
    return base_obj, manifest


def build_macro_preset(values: dict[str, float]) -> dict[str, object]:
    preset = TargetService.get_default_macro_info_dict()
    preset["race"] = dict(RACE_MIX)
    for key, value in RACE_MIX.items():
        preset[key] = value
    preset.update(values)
    return preset


def activate(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def helper_vertex_indices(obj: bpy.types.Object) -> set[int]:
    group_indices = {
        obj.vertex_groups[name].index
        for name in ("HelperGeometry", "JointCubes")
    }
    return {
        vertex.index
        for vertex in obj.data.vertices
        if any(group.group in group_indices for group in vertex.groups)
    }


def evaluated_mesh_copy(source: bpy.types.Object, name: str) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(
        evaluated,
        preserve_all_data_layers=True,
        depsgraph=depsgraph,
    )
    result = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(result)
    return result


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


def require_clean_baked_object(
    obj: bpy.types.Object,
    expected_vertex_count: int,
    expected_topology_sha256: str,
    expected_uv_sha256: str,
) -> dict[str, object]:
    require(len(obj.data.vertices) == expected_vertex_count, f"{obj.name} vertex order/count changed while baking")
    require(topology_sha256(obj) == expected_topology_sha256, f"{obj.name} topology/order changed while baking")
    require(uv_loop_sha256(obj) == expected_uv_sha256, f"{obj.name} UV loop data changed while baking")
    removed_vertex_groups = len(obj.vertex_groups)
    clear_vertex_groups(obj)
    removed_object_properties = len(obj.keys())
    removed_mesh_properties = len(obj.data.keys())
    for key in list(obj.keys()):
        del obj[key]
    for key in list(obj.data.keys()):
        del obj.data[key]
    require(obj.data.shape_keys is None, f"{obj.name} retained MPFB shape keys")
    require(len(obj.modifiers) == 0, f"{obj.name} retained MPFB modifiers")
    require(obj.parent is None, f"{obj.name} retained the temporary MPFB armature parent")
    require(len(obj.vertex_groups) == 0, f"{obj.name} unexpectedly copied MPFB vertex groups")
    require(len(obj.keys()) == 0, f"{obj.name} retained MPFB object custom properties")
    require(len(obj.data.keys()) == 0, f"{obj.name} retained MPFB mesh custom properties")
    return {
        "vertexCountAndOrderPreserved": True,
        "topologySha256": expected_topology_sha256,
        "uvLoopSha256": expected_uv_sha256,
        "shapeKeys": 0,
        "modifiers": 0,
        "parents": 0,
        "vertexGroups": 0,
        "customProperties": 0,
        "removedVertexGroups": removed_vertex_groups,
        "removedObjectCustomProperties": removed_object_properties,
        "removedMeshCustomProperties": removed_mesh_properties,
    }


def delete_vertices(obj: bpy.types.Object, indices: set[int]) -> None:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bmesh.ops.delete(
        bm,
        geom=[bm.verts[index] for index in sorted(indices)],
        context="VERTS",
    )
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def clear_vertex_groups(obj: bpy.types.Object) -> None:
    for group in list(obj.vertex_groups):
        obj.vertex_groups.remove(group)


def bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [vertex.co for vertex in obj.data.vertices]
    minimum = Vector(tuple(min(point[axis] for point in points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in points) for axis in range(3)))
    return minimum, maximum


def normalize_height(obj: bpy.types.Object) -> dict[str, object]:
    before_minimum, before_maximum = bounds(obj)
    before_height = before_maximum.z - before_minimum.z
    require(before_height > 1.0, f"Implausible MPFB height: {before_height}")
    scale = TARGET_HEIGHT / before_height
    center_x = (before_minimum.x + before_maximum.x) * 0.5
    for vertex in obj.data.vertices:
        vertex.co.x = (vertex.co.x - center_x) * scale
        vertex.co.y *= scale
        vertex.co.z = (vertex.co.z - before_minimum.z) * scale
    obj.data.update()
    obj.location = (0.0, 0.0, 0.0)
    obj.rotation_euler = (0.0, 0.0, 0.0)
    obj.scale = (1.0, 1.0, 1.0)
    after_minimum, after_maximum = bounds(obj)
    require(abs(after_minimum.z) < 1e-5, "Normalized feet are not grounded")
    require(abs(after_maximum.z - TARGET_HEIGHT) < 1e-5, "Normalized crown is not 1.780 m")
    return {
        "rawBoundsMin": list(before_minimum),
        "rawBoundsMax": list(before_maximum),
        "rawHeightMeters": before_height,
        "meshSpaceHeightScale": scale,
        "normalizedBoundsMin": list(after_minimum),
        "normalizedBoundsMax": list(after_maximum),
    }


def vertex_group_center(obj: bpy.types.Object, group_name: str) -> Vector:
    group = obj.vertex_groups.get(group_name)
    require(group is not None, f"Required MPFB joint cube group is missing: {group_name}")
    # MPFB fits bones against the mixed macro shape, not the unmodified Basis
    # coordinates in obj.data.  Read evaluated coordinates while retaining the
    # original vertex-group membership/index contract.
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    coordinates = [
        evaluated.data.vertices[vertex.index].co
        for vertex in obj.data.vertices
        if any(membership.group == group.index for membership in vertex.groups)
    ]
    require(coordinates, f"MPFB joint cube group has no vertices: {group_name}")
    return sum(coordinates, Vector()) / len(coordinates)


def validate_temporary_rig_fit(
    human: bpy.types.Object,
    temporary_rig: bpy.types.Object,
    rig_path: Path,
) -> dict[str, object]:
    definition = json.loads(rig_path.read_text(encoding="utf-8"))
    errors: list[dict[str, object]] = []
    human_to_rig = temporary_rig.matrix_world.inverted() @ human.matrix_world
    for bone_name, bone_definition in definition.items():
        bone = temporary_rig.data.bones.get(bone_name)
        require(bone is not None, f"Temporary MPFB rig is missing {bone_name}")
        for endpoint_name, actual in (("head", bone.head_local), ("tail", bone.tail_local)):
            endpoint = bone_definition[endpoint_name]
            if endpoint.get("strategy") != "CUBE":
                continue
            group_name = endpoint["cube_name"]
            expected = human_to_rig @ vertex_group_center(human, group_name)
            error = (Vector(actual) - expected).length
            errors.append({
                "bone": bone_name,
                "endpoint": endpoint_name,
                "jointCube": group_name,
                "errorMeters": error,
            })
    require(errors, "No MPFB joint-cube endpoints were available for rig-fit validation")
    maximum = max(item["errorMeters"] for item in errors)
    require(maximum <= 0.001, f"MPFB phenotype rig-fit error is {maximum:.6f} m")
    return {
        "method": "MPFB detailed joint-cube landmark agreement; no fallback/edit-bone correction",
        "validatedCubeEndpoints": len(errors),
        "maximumCubeEndpointErrorMeters": maximum,
        "maximumAllowedErrorMeters": 0.001,
        "fallbackWholeRigShiftApplied": False,
    }


def capture_pose_bone_landmarks(
    human: bpy.types.Object,
    temporary_rig: bpy.types.Object,
) -> dict[str, dict[str, list[float]]]:
    rig_to_human = human.matrix_world.inverted() @ temporary_rig.matrix_world
    landmarks: dict[str, dict[str, list[float]]] = {}
    for bone_name in LANDMARK_BONES:
        bone = temporary_rig.pose.bones.get(bone_name)
        require(bone is not None, f"Temporary MPFB rig is missing landmark bone {bone_name}")
        landmarks[bone_name] = {
            "head": list(rig_to_human @ bone.head),
            "tail": list(rig_to_human @ bone.tail),
        }
    return landmarks


def normalize_landmarks(
    landmarks: dict[str, dict[str, list[float]]],
    normalization: dict[str, object],
) -> dict[str, dict[str, list[float]]]:
    minimum = Vector(normalization["rawBoundsMin"])
    maximum = Vector(normalization["rawBoundsMax"])
    scale = float(normalization["meshSpaceHeightScale"])
    center_x = (minimum.x + maximum.x) * 0.5
    result: dict[str, dict[str, list[float]]] = {}
    for bone_name, endpoints in landmarks.items():
        result[bone_name] = {}
        for endpoint_name, values in endpoints.items():
            point = Vector(values)
            normalized = Vector((
                (point.x - center_x) * scale,
                point.y * scale,
                (point.z - minimum.z) * scale,
            ))
            result[bone_name][endpoint_name] = list(normalized)
    return result


def landmark_mirror_error(landmarks: dict[str, dict[str, list[float]]]) -> dict[str, object]:
    errors: list[dict[str, object]] = []
    for left_name in sorted(name for name in landmarks if name.endswith("_l")):
        right_name = left_name[:-2] + "_r"
        require(right_name in landmarks, f"Missing mirrored landmark bone {right_name}")
        for endpoint_name in ("head", "tail"):
            left = Vector(landmarks[left_name][endpoint_name])
            right = Vector(landmarks[right_name][endpoint_name])
            mirrored_right = Vector((-right.x, right.y, right.z))
            errors.append({
                "leftBone": left_name,
                "rightBone": right_name,
                "endpoint": endpoint_name,
                "errorMeters": (left - mirrored_right).length,
            })
    maximum = max(item["errorMeters"] for item in errors)
    return {
        "pairedEndpoints": len(errors),
        "maximumErrorMeters": maximum,
        "meanErrorMeters": sum(item["errorMeters"] for item in errors) / len(errors),
    }


def surface_landmarks(obj: bpy.types.Object) -> dict[str, object]:
    vertices = list(obj.data.vertices)
    left_fingertip = max(vertices, key=lambda vertex: vertex.co.x).co
    right_fingertip = min(vertices, key=lambda vertex: vertex.co.x).co
    sole = min(vertex.co.z for vertex in vertices)
    crown = max(vertex.co.z for vertex in vertices)
    return {
        "measurement": "helper-free baked skin extrema in normalized mesh-local meters; bone joints remain authoritative",
        "groundZ": sole,
        "crownZ": crown,
        "leftFingertipSurface": list(left_fingertip),
        "rightFingertipSurface": list(right_fingertip),
        "fingertipHalfSpanMeters": max(abs(left_fingertip.x), abs(right_fingertip.x)),
    }


def landmark_skin_containment(
    obj: bpy.types.Object,
    landmarks: dict[str, dict[str, list[float]]],
) -> dict[str, object]:
    vertices = [tuple(vertex.co) for vertex in obj.data.vertices]
    polygons = [tuple(polygon.vertices) for polygon in obj.data.polygons]
    tree = BVHTree.FromPolygons(vertices, polygons, all_triangles=False)
    directions = [
        Vector((1.0, 0.173, 0.097)).normalized(),
        Vector((0.131, 1.0, 0.211)).normalized(),
        Vector((0.193, 0.127, 1.0)).normalized(),
    ]

    def ray_intersections(point: Vector, direction: Vector) -> int:
        origin = point.copy()
        remaining = 5.0
        count = 0
        for _ in range(64):
            location, _normal, _face, distance = tree.ray_cast(origin, direction, remaining)
            if location is None or distance is None:
                break
            count += 1
            advance = max(float(distance), 0.0) + 1e-5
            origin = location + (direction * 1e-5)
            remaining -= advance
            if remaining <= 0.0:
                break
        return count

    checks: list[dict[str, object]] = []
    failures: list[str] = []
    surface_tolerance = 0.003
    terminal_surface_tolerance = 0.010
    non_skin_control_endpoints = {"Root.head", "Root.tail"}
    for bone_name, endpoints in landmarks.items():
        for endpoint_name, values in endpoints.items():
            point = Vector(values)
            _nearest, _normal, _face, distance = tree.find_nearest(point)
            require(distance is not None, f"No skin surface found for {bone_name}.{endpoint_name}")
            parity_votes = [ray_intersections(point, direction) % 2 == 1 for direction in directions]
            inside = sum(parity_votes) >= 2
            key = f"{bone_name}.{endpoint_name}"
            is_non_skin_control = key in non_skin_control_endpoints
            is_terminal_surface_endpoint = key == "head.tail"
            accepted = (
                is_non_skin_control
                or inside
                or float(distance) <= surface_tolerance
                or (is_terminal_surface_endpoint and float(distance) <= terminal_surface_tolerance)
            )
            if not accepted:
                failures.append(key)
            checks.append({
                "landmark": key,
                "insideByMajorityRayParity": inside,
                "nearestSurfaceDistanceMeters": float(distance),
                "acceptedInsideOrNearSurface": accepted,
                "nonSkinControlEndpoint": is_non_skin_control,
                "terminalSurfaceEndpoint": is_terminal_surface_endpoint,
            })
    return {
        "method": "three non-axis ray-parity votes against closed helper-free skin; surface-adjacent endpoints accept <=3mm",
        "surfaceToleranceMeters": surface_tolerance,
        "terminalSurfaceToleranceMeters": terminal_surface_tolerance,
        "nonSkinControlEndpoints": sorted(non_skin_control_endpoints),
        "checkedEndpoints": len(checks),
        "acceptedEndpoints": sum(1 for check in checks if check["acceptedInsideOrNearSurface"]),
        "outsideBeyondTolerance": failures,
        "maximumNearestSurfaceDistanceMeters": max(check["nearestSurfaceDistanceMeters"] for check in checks),
        "status": "PASS" if not failures else "REVIEW",
    }


def neutral_midpoint_landmarks(
    reports: dict[str, dict[str, object]],
) -> dict[str, object]:
    masculine = reports["Masculine"]["tPoseFit"]["normalizedPoseBoneLandmarks"]
    feminine = reports["Feminine"]["tPoseFit"]["normalizedPoseBoneLandmarks"]
    neutral: dict[str, dict[str, list[float]]] = {}
    handled: set[str] = set()
    for bone_name in LANDMARK_BONES:
        if bone_name in handled:
            continue
        if bone_name.endswith("_r"):
            continue
        if bone_name.endswith("_l"):
            right_name = bone_name[:-2] + "_r"
            neutral[bone_name] = {}
            neutral[right_name] = {}
            for endpoint_name in ("head", "tail"):
                samples = [
                    Vector(masculine[bone_name][endpoint_name]),
                    Vector(masculine[right_name][endpoint_name]),
                    Vector(feminine[bone_name][endpoint_name]),
                    Vector(feminine[right_name][endpoint_name]),
                ]
                x = sum(abs(sample.x) for sample in samples) / len(samples)
                y = sum(sample.y for sample in samples) / len(samples)
                z = sum(sample.z for sample in samples) / len(samples)
                neutral[bone_name][endpoint_name] = [x, y, z]
                neutral[right_name][endpoint_name] = [-x, y, z]
            handled.update((bone_name, right_name))
            continue
        neutral[bone_name] = {}
        for endpoint_name in ("head", "tail"):
            first = Vector(masculine[bone_name][endpoint_name])
            second = Vector(feminine[bone_name][endpoint_name])
            neutral[bone_name][endpoint_name] = list((first + second) * 0.5)
        handled.add(bone_name)
    surface_half_span = (
        reports["Masculine"]["tPoseFit"]["surfaceLandmarks"]["fingertipHalfSpanMeters"]
        + reports["Feminine"]["tPoseFit"]["surfaceLandmarks"]["fingertipHalfSpanMeters"]
    ) * 0.5
    return {
        "status": "PROPOSAL_FOR_SESSION_HUMANOID_RIG_REFIT_NOT_A_FROZEN_CONTRACT",
        "rule": "Center bones are the arithmetic masculine/feminine midpoint. Paired bones average both variants and mirrored sides, then enforce exact X symmetry.",
        "orientation": "Blender mesh local: meters, +Z up, -Y forward, Left is +X",
        "landmarks": neutral,
        "surfaceFingertipHalfSpanMeters": surface_half_span,
        "reason": "Accepted body anatomy is authoritative; do not axially stretch it to the provisional 0.99 m rig half-span.",
    }


def bake_mpfb_rest_and_t_pose(
    human: bpy.types.Object,
    variant: str,
    extension_root: Path,
) -> tuple[bpy.types.Object, bpy.types.Object, dict[str, object]]:
    """Use MPFB rig/weights only as a disposable anatomically-safe pose tool."""
    helper_indices = helper_vertex_indices(human)
    input_vertex_count = len(human.data.vertices)
    input_topology_sha256 = topology_sha256(human)
    input_uv_sha256 = uv_loop_sha256(human)
    rig_path = extension_root / "data/rigs/standard/rig.game_engine.json"
    weights_path = extension_root / "data/rigs/standard/weights.game_engine.json"
    require(sha256(rig_path) == EXPECTED_GAME_ENGINE_RIG_SHA256, "MPFB game-engine rig source changed")
    require(sha256(weights_path) == EXPECTED_GAME_ENGINE_WEIGHTS_SHA256, "MPFB game-engine weights changed")
    temporary_rig = HumanService.add_builtin_rig(
        human,
        "game_engine",
        import_weights=True,
    )
    require(temporary_rig is not None, "MPFB game_engine rig creation failed")
    rig_fit = validate_temporary_rig_fit(human, temporary_rig, rig_path)

    raw_rest = evaluated_mesh_copy(
        human,
        f"EvidenceOnly_SessionHumanoidBaseV1_{variant}_RawARest",
    )
    raw_bake = require_clean_baked_object(
        raw_rest,
        input_vertex_count,
        input_topology_sha256,
        input_uv_sha256,
    )
    delete_vertices(raw_rest, helper_indices)

    pose_path = extension_root / "data/poses/game_engine_fk/t-pose.json"
    require(pose_path.is_file(), f"MPFB game-engine T-pose is missing: {pose_path}")
    require(sha256(pose_path) == EXPECTED_GAME_ENGINE_TPOSE_SHA256, "MPFB stock T-pose source changed")
    pose = json.loads(pose_path.read_text(encoding="utf-8"))
    activate(temporary_rig)
    bpy.ops.object.mode_set(mode="POSE")
    RigService.set_pose_from_dict(temporary_rig, pose)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    raw_pose_landmarks = capture_pose_bone_landmarks(human, temporary_rig)

    t_pose = evaluated_mesh_copy(
        human,
        f"SessionHumanoidBaseV1_{variant}_MPFBSource",
    )
    t_bake = require_clean_baked_object(
        t_pose,
        input_vertex_count,
        input_topology_sha256,
        input_uv_sha256,
    )
    delete_vertices(t_pose, helper_indices)

    human_mesh = human.data
    temporary_armature = temporary_rig.data
    bpy.data.objects.remove(human, do_unlink=True)
    bpy.data.objects.remove(temporary_rig, do_unlink=True)
    require(human_mesh.users == 0, "Temporary MPFB basemesh datablock still has users")
    require(temporary_armature.users == 0, "Temporary MPFB armature datablock still has users")
    bpy.data.meshes.remove(human_mesh)
    bpy.data.armatures.remove(temporary_armature)
    return raw_rest, t_pose, {
        "method": "detailed-helper-fitted temporary MPFB game_engine rig/weights; evaluated posed mesh baked; temporary rig deleted",
        "temporaryRigRetained": False,
        "temporaryRigName": "game_engine",
        "temporaryRigSource": str(rig_path),
        "temporaryRigSourceSha256": sha256(rig_path),
        "temporaryWeightsSource": str(weights_path),
        "temporaryWeightsSourceSha256": sha256(weights_path),
        "rigFit": rig_fit,
        "pose": str(pose_path),
        "poseSha256": sha256(pose_path),
        "stockPoseModified": False,
        "localPoseCorrection": None,
        "rawPoseBoneLandmarks": raw_pose_landmarks,
        "helperVerticesRemovedAfterBake": len(helper_indices),
        "temporaryMeshDatablockRemoved": True,
        "temporaryArmatureDatablockRemoved": True,
        "rawBakeHygiene": raw_bake,
        "tPoseBakeHygiene": t_bake,
    }


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def mesh_integrity(obj: bpy.types.Object) -> dict[str, int]:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    bm.edges.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    boundary_edges = sum(1 for edge in bm.edges if edge.is_boundary)
    non_manifold_edges = sum(1 for edge in bm.edges if not edge.is_manifold)
    loose_vertices = sum(1 for vertex in bm.verts if not vertex.link_edges)
    degenerate_faces = sum(1 for face in bm.faces if face.calc_area() <= 1e-12)
    components = 0
    unseen = set(bm.verts)
    while unseen:
        components += 1
        stack = [unseen.pop()]
        while stack:
            vertex = stack.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in unseen:
                    unseen.remove(other)
                    stack.append(other)
    bm.free()
    return {
        "boundaryEdges": boundary_edges,
        "nonManifoldEdges": non_manifold_edges,
        "looseVertices": loose_vertices,
        "degenerateFaces": degenerate_faces,
        "connectedComponents": components,
    }


def apply_modifier(obj: bpy.types.Object, modifier: bpy.types.Modifier) -> None:
    activate(obj)
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def create_clay_review_mesh(source: bpy.types.Object, variant: str) -> bpy.types.Object:
    review = source.copy()
    review.data = source.data.copy()
    review.name = f"EvidenceOnly_SessionHumanoidBaseV1_{variant}_Review20k"
    review.data.name = review.name + "_Mesh"
    bpy.context.collection.objects.link(review)
    before = triangle_count(review)
    modifier = review.modifiers.new("EvidenceOnly_GlobalSymmetricReviewReduction", "DECIMATE")
    modifier.decimate_type = "COLLAPSE"
    modifier.ratio = CLAY_REVIEW_TARGET_TRIANGLES / before
    modifier.use_collapse_triangulate = True
    modifier.use_symmetry = True
    modifier.symmetry_axis = "X"
    apply_modifier(review, modifier)
    after = triangle_count(review)
    require(
        CLAY_REVIEW_TRIANGLE_RANGE[0] <= after <= CLAY_REVIEW_TRIANGLE_RANGE[1],
        f"{review.name} has {after} triangles",
    )
    for polygon in review.data.polygons:
        polygon.use_smooth = True
    return review


def create_variant(
    variant: str,
    values: dict[str, float],
) -> tuple[bpy.types.Object, bpy.types.Object, bpy.types.Object, dict[str, object]]:
    preset = build_macro_preset(values)
    human = HumanService.create_human(
        mask_helpers=False,
        # MPFB's phenotype-aware rig fit requires the detailed joint-* cube
        # groups.  They are authoring helpers and are removed after the pose
        # bake, but excluding them makes MPFB fall back to generic pivots.
        detailed_helpers=True,
        extra_vertex_groups=True,
        feet_on_ground=True,
        scale=0.1,
        macro_detail_dict=preset,
    )
    human.name = f"TemporaryMPFB_{variant}_Human"
    extension_root = Path(inspect.getfile(HumanService)).resolve().parent.parent
    raw_rest, source, temporary_pose = bake_mpfb_rest_and_t_pose(
        human,
        variant,
        extension_root,
    )
    raw_normalization = normalize_height(raw_rest)
    normalization = normalize_height(source)
    normalized_landmarks = normalize_landmarks(
        temporary_pose["rawPoseBoneLandmarks"],
        normalization,
    )
    temporary_pose["normalizedPoseBoneLandmarks"] = normalized_landmarks
    temporary_pose["normalizedLandmarkMirrorError"] = landmark_mirror_error(normalized_landmarks)
    temporary_pose["surfaceLandmarks"] = surface_landmarks(source)
    temporary_pose["landmarkSkinContainment"] = landmark_skin_containment(source, normalized_landmarks)
    require(len(source.data.vertices) == 13380, f"{variant} retained body vertex count changed")
    require(triangle_count(source) == 26756, f"{variant} retained body triangle count changed")
    retained_topology_sha256 = topology_sha256(source)
    source_uv_name = source.data.uv_layers.active.name
    source_uv_sha256 = uv_loop_sha256(source)
    source.data.uv_layers.active.name = "UV0"
    require(len(source.data.uv_layers) == 1, f"{variant} retained source must have one UV layer")
    retained_uv0_sha256 = uv_loop_sha256(source)
    for obj in (raw_rest, source):
        for polygon in obj.data.polygons:
            polygon.use_smooth = True
    source_integrity = mesh_integrity(source)
    require(source_integrity["boundaryEdges"] == 0, f"{variant} source has open edges")
    require(source_integrity["nonManifoldEdges"] == 0, f"{variant} source is non-manifold")
    require(source_integrity["connectedComponents"] == 1, f"{variant} source is disconnected")
    source.hide_render = True
    source.hide_set(True)
    raw_rest.hide_render = True
    raw_rest.hide_set(True)
    review = create_clay_review_mesh(source, variant)
    review_integrity = mesh_integrity(review)
    require(review_integrity["boundaryEdges"] == 0, f"{variant} clay review mesh has open edges")
    require(review_integrity["nonManifoldEdges"] == 0, f"{variant} clay review mesh is non-manifold")
    report = {
        "variant": variant,
        "preset": preset,
        "rawARestEvidence": {
            "vertices": len(raw_rest.data.vertices),
            "triangles": triangle_count(raw_rest),
            "normalization": raw_normalization,
            "integrity": mesh_integrity(raw_rest),
        },
        "sourceVertices": len(source.data.vertices),
        "sourceTriangles": triangle_count(source),
        "sourceUvLayers": [layer.name for layer in source.data.uv_layers],
        "retainedBodySourceHashes": {
            "topologySha256": retained_topology_sha256,
            "sourceUvLayerName": source_uv_name,
            "sourceUvLoopSha256": source_uv_sha256,
            "productionUvLayerName": "UV0",
            "productionUv0LoopSha256": retained_uv0_sha256,
        },
        "sourceIntegrity": source_integrity,
        "normalization": normalization,
        "tPoseFit": {
            **temporary_pose,
            "globalAnisotropicMeshScaling": False,
            "nonMacroTargetWeights": {},
            "forwardAxis": "-Y",
            "upAxis": "+Z",
        },
        "productionDeformationSource": {
            "mesh": source.name,
            "triangles": triangle_count(source),
            "policy": "Preserve this topology for binding; class LOD0 deletes fully occluded body faces before protected/manual simplification.",
        },
        "clayReviewProxy": {
            "mesh": review.name,
            "productionAsset": False,
            "vertices": len(review.data.vertices),
            "triangles": triangle_count(review),
            "uvLayers": [layer.name for layer in review.data.uv_layers],
            "integrity": review_integrity,
            "purpose": "Evidence rendering only; global decimation is not an authoritative deform/LOD topology.",
        },
        "objectTransform": {
            "location": list(source.location),
            "rotationEuler": list(source.rotation_euler),
            "scale": list(source.scale),
        },
    }
    return raw_rest, source, review, report


def create_clay_material() -> bpy.types.Material:
    material = bpy.data.materials.new("SessionHumanoidBaseV1_ClayReview")
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (0.42, 0.27, 0.19, 1.0)
    principled.inputs["Roughness"].default_value = 0.64
    principled.inputs["Metallic"].default_value = 0.0
    return material


def assign_material(obj: bpy.types.Object, material: bpy.types.Material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_render(width: int, height: int) -> None:
    scene = bpy.context.scene
    try:
        scene.render.engine = "BLENDER_EEVEE_NEXT"
    except TypeError:
        scene.render.engine = "BLENDER_EEVEE"
    scene.render.resolution_x = width
    scene.render.resolution_y = height
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("SessionHumanoidBaseV1_EvidenceWorld")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.006, 0.010, 0.020, 1.0)
    background.inputs["Strength"].default_value = 0.18
    try:
        scene.view_settings.look = "AgX - Medium High Contrast"
    except TypeError:
        pass


def add_area_light(name: str, location: tuple[float, float, float], energy: float, color: tuple[float, float, float], size: float, target: Vector) -> bpy.types.Object:
    data = bpy.data.lights.new(name, "AREA")
    data.energy = energy
    data.color = color
    data.shape = "DISK"
    data.size = size
    light = bpy.data.objects.new(name, data)
    light.location = location
    bpy.context.collection.objects.link(light)
    look_at(light, target)
    return light


def add_studio_lights(target: Vector, prefix: str) -> list[bpy.types.Object]:
    return [
        add_area_light(prefix + "_Key", (-4.0, -5.0, target.z + 3.0), 1050.0, (0.72, 0.84, 1.0), 3.5, target),
        add_area_light(prefix + "_Fill", (4.0, -2.0, target.z + 2.0), 650.0, (0.46, 0.60, 0.82), 3.2, target),
        add_area_light(prefix + "_Rim", (2.0, 4.0, target.z + 2.8), 900.0, (1.0, 0.40, 0.16), 2.8, target),
    ]


def duplicate_display(
    source: bpy.types.Object,
    name: str,
    location: tuple[float, float, float],
    heading_degrees: float,
    material: bpy.types.Material,
    visual_scale: float = 1.0,
) -> bpy.types.Object:
    obj = source.copy()
    obj.data = source.data
    obj.name = name
    obj.location = location
    obj.rotation_euler = (0.0, 0.0, math.radians(heading_degrees))
    obj.scale = (visual_scale, visual_scale, visual_scale)
    obj.hide_render = False
    obj.hide_set(False)
    bpy.context.collection.objects.link(obj)
    assign_material(obj, material)
    return obj


def render_turnaround(
    variant: str,
    review: bpy.types.Object,
    raw_rest: bpy.types.Object,
    material: bpy.types.Material,
) -> Path:
    configure_render(3240, 2160)
    layouts = [
        (review, "TFront", (-2.10, 0.0, 2.05), 0.0),
        (review, "TThreeQuarter", (0.0, 0.0, 2.05), 45.0),
        (raw_rest, "RawAFront", (2.10, 0.0, 2.05), 0.0),
        (review, "TSide", (-2.10, 0.0, 0.0), 90.0),
        (review, "TBack", (0.0, 0.0, 0.0), 180.0),
        (raw_rest, "RawAThreeQuarter", (2.10, 0.0, 0.0), 45.0),
    ]
    displays = [
        duplicate_display(source, f"Evidence_{variant}_{label}", location, heading, material)
        for source, label, location, heading in layouts
    ]
    lights = add_studio_lights(Vector((0.0, 0.0, 1.95)), "Turnaround" + variant)
    camera_data = bpy.data.cameras.new("Turnaround" + variant + "Camera")
    camera_data.type = "ORTHO"
    # Orthographic scale is the horizontal span for this 3:2 render.  The
    # six-cell board is 6.2 m wide, so the former square-board value of 4.25
    # cropped the outer columns and the heads/feet.  Keep a small review-safe
    # margin around every complete silhouette.
    camera_data.ortho_scale = 6.50
    camera = bpy.data.objects.new("Turnaround" + variant + "Camera", camera_data)
    camera.location = (0.0, -8.0, 1.93)
    bpy.context.collection.objects.link(camera)
    look_at(camera, Vector((0.0, 0.0, 1.93)))
    bpy.context.scene.camera = camera
    for display in displays:
        frame = projected_pixel_bounds(display, camera, 1, 1)
        margin = 0.0125
        require(frame["allDepthPositive"], f"{display.name} is behind the turnaround camera")
        require(
            frame["minX"] >= margin
            and frame["maxX"] <= 1.0 - margin
            and frame["minY"] >= margin
            and frame["maxY"] <= 1.0 - margin,
            f"{display.name} is cropped in turnaround evidence: {frame}",
        )
    path = EVIDENCE_DIR / f"SessionHumanoidBaseV1_{variant}_ClayTurnaround.png"
    bpy.context.scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    for obj in displays + lights + [camera]:
        bpy.data.objects.remove(obj, do_unlink=True)
    return path


def add_gameplay_camera() -> bpy.types.Object:
    yaw = math.radians(CAMERA_YAW)
    pitch = math.radians(CAMERA_PITCH)
    planar_forward = Vector((math.sin(yaw), math.cos(yaw), 0.0))
    planar_right = Vector((math.cos(yaw), -math.sin(yaw), 0.0))
    target = (
        Vector((0.0, 0.0, CAMERA_PIVOT_HEIGHT))
        + (planar_forward * CAMERA_FORWARD_FOCUS)
        + (planar_right * CAMERA_RIGHT_FOCUS)
    )
    view_forward = Vector((planar_forward.x * math.cos(pitch), planar_forward.y * math.cos(pitch), -math.sin(pitch)))
    data = bpy.data.cameras.new("SessionHumanoidBaseV1_LockedGameplayCamera")
    data.sensor_fit = "VERTICAL"
    data.angle = math.radians(CAMERA_VERTICAL_FOV)
    camera = bpy.data.objects.new("SessionHumanoidBaseV1_LockedGameplayCamera", data)
    camera.location = target - (view_forward * CAMERA_DISTANCE)
    bpy.context.collection.objects.link(camera)
    look_at(camera, target)
    bpy.context.scene.camera = camera
    return camera


def projected_pixel_bounds(
    obj: bpy.types.Object,
    camera: bpy.types.Object,
    width: int,
    height: int,
    vertex_indices: list[int] | None = None,
) -> dict[str, float | bool]:
    bpy.context.view_layer.update()
    vertices = (
        [obj.data.vertices[index] for index in vertex_indices]
        if vertex_indices is not None
        else obj.data.vertices
    )
    coordinates = [
        world_to_camera_view(bpy.context.scene, camera, obj.matrix_world @ vertex.co)
        for vertex in vertices
    ]
    xs = [coordinate.x * width for coordinate in coordinates]
    ys = [coordinate.y * height for coordinate in coordinates]
    return {
        "minX": min(xs),
        "maxX": max(xs),
        "minY": min(ys),
        "maxY": max(ys),
        "widthPixels": max(xs) - min(xs),
        "heightPixels": max(ys) - min(ys),
        "minimumCameraDepth": min(coordinate.z for coordinate in coordinates),
        "allDepthPositive": all(coordinate.z > 0.0 for coordinate in coordinates),
    }


def render_locked_camera(
    variant: str,
    neutral_body: bpy.types.Object,
    material: bpy.types.Material,
    visual_scale: float,
    evidence_label: str,
) -> tuple[Path, dict[str, object], dict[str, object]]:
    configure_render(1920, 1080)
    display = duplicate_display(
        neutral_body,
        f"Evidence_{variant}_{evidence_label}_NeutralARest_LockedCamera",
        (0.0, 0.0, 0.0),
        -45.0,
        material,
        visual_scale,
    )
    bpy.ops.mesh.primitive_plane_add(size=34.0, location=(0.0, 0.0, -0.003))
    floor = bpy.context.object
    floor.name = "EvidenceOnly_StrategyFloor"
    floor_material = bpy.data.materials.new("EvidenceOnly_StrategyFloorMaterial_" + variant)
    floor_material.diffuse_color = (0.035, 0.045, 0.065, 1.0)
    floor.data.materials.append(floor_material)
    lights = [
        add_area_light("StrategyKey_" + variant, (-5.0, -7.0, 12.0), 1750.0, (0.62, 0.76, 1.0), 8.0, Vector((0.0, 0.0, 0.8))),
        add_area_light("StrategyRim_" + variant, (5.0, 6.0, 8.0), 1300.0, (1.0, 0.32, 0.10), 7.0, Vector((0.0, 0.0, 0.8))),
    ]
    camera = add_gameplay_camera()
    pixels = projected_pixel_bounds(display, camera, 1920, 1080)
    torso_indices = [
        vertex.index
        for vertex in display.data.vertices
        if 0.90 <= vertex.co.z <= 1.58 and abs(vertex.co.x) <= 0.42
    ]
    require(len(torso_indices) >= 500, f"{variant} torso projection mask is unexpectedly small")
    torso_digest = hashlib.sha256()
    for index in torso_indices:
        torso_digest.update(struct.pack("<I", index))
    torso_pixels = projected_pixel_bounds(display, camera, 1920, 1080, torso_indices)
    require(pixels["allDepthPositive"], f"{variant} body has vertices behind the locked camera")
    require(torso_pixels["allDepthPositive"], f"{variant} torso has vertices behind the locked camera")
    path = EVIDENCE_DIR / f"SessionHumanoidBaseV1_{variant}_{evidence_label}_LockedGameplayCamera.png"
    bpy.context.scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    for obj in [display, floor, camera] + lights:
        bpy.data.objects.remove(obj, do_unlink=True)
    return path, pixels, {
        "selection": "stable neutral A/rest vertices with 0.90m <= z <= 1.58m and abs(x) <= 0.42m before display rotation",
        "vertexCount": len(torso_indices),
        "vertexIndexSha256": torso_digest.hexdigest(),
        "projectedBounds": torso_pixels,
        "scope": "pre-bind neutral A/rest proxy; production idle/combat-pose projection remains required after rig refit",
        "presentationScale": visual_scale,
    }


def run_early_stage() -> None:
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    EVIDENCE_DIR.mkdir(parents=True, exist_ok=True)
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    base_obj, extension_manifest = mpfb_paths()
    reset_scene()
    clay = create_clay_material()

    built: dict[
        str,
        tuple[bpy.types.Object, bpy.types.Object, bpy.types.Object],
    ] = {}
    reports: dict[str, dict[str, object]] = {}
    for variant, values in VARIANTS.items():
        raw_rest, source, review, report = create_variant(variant, values)
        assign_material(review, clay)
        assign_material(raw_rest, clay)
        built[variant] = (raw_rest, source, review)
        reports[variant] = report

    evidence: dict[str, dict[str, object]] = {}
    for variant, (raw_rest, _source, review) in built.items():
        turnaround = render_turnaround(variant, review, raw_rest, clay)
        identity_locked, identity_pixels, identity_torso = render_locked_camera(
            variant,
            raw_rest,
            clay,
            IDENTITY_DCC_SCALE,
            "IdentityDcc",
        )
        runtime_locked, runtime_pixels, runtime_torso = render_locked_camera(
            variant,
            raw_rest,
            clay,
            HUNTER_RUNTIME_PRESENTATION_SCALE,
            "HunterRuntimePresentation",
        )
        evidence[variant] = {
            "turnaround": str(turnaround.relative_to(PROJECT_ROOT.parent)),
            "turnaroundSha256": sha256(turnaround),
            "turnaroundLayout": "top: T front, T 3/4, raw MPFB A front; bottom: T side, T back, raw MPFB A 3/4",
            "identityDccLockedGameplayCamera": str(identity_locked.relative_to(PROJECT_ROOT.parent)),
            "identityDccLockedGameplayCameraSha256": sha256(identity_locked),
            "identityDccProjectedBounds": identity_pixels,
            "identityDccNeutralARestTorsoProjection": identity_torso,
            "identityDccScale": IDENTITY_DCC_SCALE,
            "hunterRuntimeLockedGameplayCamera": str(runtime_locked.relative_to(PROJECT_ROOT.parent)),
            "hunterRuntimeLockedGameplayCameraSha256": sha256(runtime_locked),
            "hunterRuntimeProjectedBounds": runtime_pixels,
            "hunterRuntimeNeutralARestTorsoProjection": runtime_torso,
            "hunterRuntimeVisualRootScale": HUNTER_RUNTIME_VISUAL_ROOT_SCALE,
            "hunterRuntimeRelationScale": HUNTER_RUNTIME_RELATION_SCALE,
            "hunterRuntimeCombinedPresentationScale": HUNTER_RUNTIME_PRESENTATION_SCALE,
            "presentationScalePolicy": "DCC mesh/FBX remain true 1.780m with identity transforms. Hunter's authored visual-root and existing relation scales are presentation-only and are not baked into source geometry.",
            "turnaroundRenderedGeometry": "non-production 20k T-pose clay review proxy plus preserved raw A/rest intake; preserved source topology is authoritative for deformation",
            "lockedRenderedGeometry": "preserved neutral A/rest intake rendered both at identity DCC scale and current Hunter runtime presentation scale; post-bind idle/combat evidence remains required",
        }

    for raw_rest, source, review in built.values():
        raw_rest.hide_set(True)
        raw_rest.hide_render = True
        source.hide_set(True)
        source.hide_render = True
        review.hide_set(False)
        review.hide_render = False
    bpy.ops.wm.save_as_mainfile(filepath=str(CHECKPOINT_BLEND), check_existing=False)

    neutral_refit = neutral_midpoint_landmarks(reports)
    neutral_refit_sha256 = canonical_json_sha256(neutral_refit)
    landmark_artifact = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV1",
        "purpose": "deterministic body-authoritative input for SessionHumanoidRigV1 neutral rest refit",
        "status": "BODY_INTAKE_ACCEPTED_RIG_REFIT_INPUT_ONLY_NOT_A_PRODUCTION_EXPORT",
        "deterministicScope": "This file excludes Blender checkpoint/report/evidence hashes because Blender .blend bytes are not stable across identical saves; those per-build hashes live in SessionHumanoidBaseV1_BuildManifest.json.",
        "orientation": "Blender mesh local: meters, +Z up, -Y forward, Left is +X",
        "normalizationMethod": "helper-free baked body: x centered on bounds, uniform xyz scale=1.780/rawHeight, z grounded by raw minZ",
        "generator": {
            "sourceScript": str(Path(__file__).relative_to(PROJECT_ROOT)),
            "sourceScriptSha256": sha256(Path(__file__)),
            "blenderVersion": bpy.app.version_string,
            "mpfbVersion": MPFB_VERSION,
        },
        "sourceBoundary": {
            "mpfbExtensionManifestSha256": sha256(extension_manifest),
            "mpfbBundledBaseObjSha256": sha256(base_obj),
            "mpfbGameEngineRigSha256": EXPECTED_GAME_ENGINE_RIG_SHA256,
            "mpfbGameEngineWeightsSha256": EXPECTED_GAME_ENGINE_WEIGHTS_SHA256,
            "mpfbStockTPoseSha256": EXPECTED_GAME_ENGINE_TPOSE_SHA256,
            "assetLicense": "CC0-1.0",
            "assetLicenseUrl": MPFB_ASSET_LICENSE_URL,
            "addonCodeBoundary": "GPL MPFB add-on code is tooling-only and absent from Unity/runtime deliverables.",
            "excludedByDesign": "No provisional SessionHumanoidRigV1 FBX/manifest path or hash is present.",
        },
        "variants": {},
        "neutralMidpointProposalCanonicalSha256": neutral_refit_sha256,
        "neutralMidpointProposal": neutral_refit,
    }
    for variant, variant_report in reports.items():
        pose_fit = variant_report["tPoseFit"]
        landmark_artifact["variants"][variant] = {
            "macroPreset": variant_report["preset"],
            "nonMacroTargetWeights": pose_fit["nonMacroTargetWeights"],
            "normalization": variant_report["normalization"],
            "retainedBodySourceHashes": variant_report["retainedBodySourceHashes"],
            "rigFitAgainstMpfbJointCubes": pose_fit["rigFit"],
            "stockPoseModified": pose_fit["stockPoseModified"],
            "localPoseCorrection": pose_fit["localPoseCorrection"],
            "rawPoseBoneLandmarks": pose_fit["rawPoseBoneLandmarks"],
            "normalizedPoseBoneLandmarks": pose_fit["normalizedPoseBoneLandmarks"],
            "normalizedLandmarkMirrorError": pose_fit["normalizedLandmarkMirrorError"],
            "surfaceLandmarks": pose_fit["surfaceLandmarks"],
            "landmarkSkinContainment": pose_fit["landmarkSkinContainment"],
        }
    LANDMARK_REPORT.write_text(json.dumps(landmark_artifact, indent=2) + "\n", encoding="utf-8")
    landmark_artifact_sha256 = sha256(LANDMARK_REPORT)

    report = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV1",
        "stage": "MPFB_HELPER_FREE_BODY_INTAKE_ACCEPTED",
        "status": "BODY_INTAKE_ACCEPTED_RIG_REFIT_AND_BINDING_DEFERRED",
        "unityIntegrationAllowed": False,
        "manualBaseAnatomyGate": {
            "status": "PASS",
            "scope": "untextured shared base anatomy only, not a production class character",
            "findings": "Face, hands, feet, shoulders, knees, and the A-to-T conversion are visually viable; no underarm spikes remain.",
        },
        "temporaryRigPipelineAudit": {
            "status": "PASS",
            "scope": "MPFB phenotype-aware temporary rig fit, immutable stock T-pose, evaluated bake, helper/tooling cleanup, topology/UV preservation, and source/evidence separation",
        },
        "blenderVersion": bpy.app.version_string,
        "mpfb": {
            "version": MPFB_VERSION,
            "extensionManifest": str(extension_manifest),
            "extensionManifestSha256": sha256(extension_manifest),
            "bundledBaseObj": str(base_obj),
            "bundledBaseObjSha256": sha256(base_obj),
            "assetLicense": "CC0-1.0",
            "assetLicenseUrl": MPFB_ASSET_LICENSE_URL,
            "combinedLicenseUrl": MPFB_LICENSE_URL,
            "addonCodeBoundary": "GPL MPFB add-on code is tooling-only and absent from generated meshes, Unity Assets, and runtime deliverables.",
        },
        "generator": {
            "script": str(Path(__file__).relative_to(PROJECT_ROOT)),
            "scriptSha256": sha256(Path(__file__)),
            "command": "'/Applications/Blender.app/Contents/MacOS/Blender' --background --python game-rpg/Tools/Art/Blender/build_session_humanoid_base_v1.py -- --stage early",
        },
        "rigRefitDependency": {
            "bindingStatus": "deferred pending SessionHumanoidRigV1 neutral rest refit to the accepted body landmark midpoint",
            "heightMeters": TARGET_HEIGHT,
            "sourceAxes": "+Z up, -Y forward",
            "objectTransforms": "identity",
            "provisionalProjectRigIsNotAnInput": True,
            "dccAndFbxScale": IDENTITY_DCC_SCALE,
            "hunterRuntimePresentationScale": {
                "visualRoot": HUNTER_RUNTIME_VISUAL_ROOT_SCALE,
                "relation": HUNTER_RUNTIME_RELATION_SCALE,
                "combined": HUNTER_RUNTIME_PRESENTATION_SCALE,
                "bakedIntoGeometry": False,
            },
        },
        "variants": reports,
        "proposedNeutralRigRefit": neutral_refit,
        "stableRigFitLandmarkArtifact": {
            "path": str(LANDMARK_REPORT.relative_to(PROJECT_ROOT)),
            "sha256": landmark_artifact_sha256,
            "neutralMidpointProposalCanonicalSha256": neutral_refit_sha256,
            "excludesProvisionalProjectRigHashes": True,
        },
        "perBuildManifest": {
            "path": str(BUILD_MANIFEST.relative_to(PROJECT_ROOT)),
            "generatedAfterThisIntakeReport": True,
            "containsVolatileBlenderCheckpointHash": True,
        },
        "evidence": evidence,
        "checkpointBlend": str(CHECKPOINT_BLEND.relative_to(PROJECT_ROOT)),
        "checkpointBlendSha256": sha256(CHECKPOINT_BLEND),
        "deferredUntilRefitRigAndClassAuthoring": [
            "SessionHumanoidRigV1 binding and deformation actions",
            "class-specific occlusion-cut production LOD0, LOD1, and LOD2",
            "BaseColor, tangent Normal, AO/direct URP Mask atlases",
            "FBX and Blender round-trip report",
            "Unity integration",
        ],
    }
    INTAKE_REPORT.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")

    evidence_files: list[dict[str, str]] = []
    for variant_evidence in evidence.values():
        for path_key, hash_key in (
            ("turnaround", "turnaroundSha256"),
            ("identityDccLockedGameplayCamera", "identityDccLockedGameplayCameraSha256"),
            ("hunterRuntimeLockedGameplayCamera", "hunterRuntimeLockedGameplayCameraSha256"),
        ):
            evidence_files.append({
                "path": variant_evidence[path_key],
                "sha256": variant_evidence[hash_key],
            })
    third_party_notices = PROJECT_ROOT / "THIRD_PARTY_NOTICES.md"
    build_manifest = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV1",
        "status": report["status"],
        "determinismBoundary": "The stable landmark artifact is bitwise deterministic for identical inputs. Blender .blend checkpoint bytes and this per-build manifest are provenance snapshots and may change across identical saves.",
        "stableRigFitLandmarks": {
            "path": str(LANDMARK_REPORT.relative_to(PROJECT_ROOT)),
            "sha256": landmark_artifact_sha256,
            "neutralMidpointProposalCanonicalSha256": neutral_refit_sha256,
        },
        "bodyIntakeReport": {
            "path": str(INTAKE_REPORT.relative_to(PROJECT_ROOT.parent)),
            "sha256": sha256(INTAKE_REPORT),
        },
        "checkpointBlend": {
            "path": str(CHECKPOINT_BLEND.relative_to(PROJECT_ROOT)),
            "sha256": sha256(CHECKPOINT_BLEND),
            "bitwiseDeterministic": False,
        },
        "authoringSources": {
            "builder": str(Path(__file__).relative_to(PROJECT_ROOT)),
            "builderSha256": sha256(Path(__file__)),
            "thirdPartyNotices": str(third_party_notices.relative_to(PROJECT_ROOT)),
            "thirdPartyNoticesSha256": sha256(third_party_notices),
            "mpfbExtensionManifestSha256": sha256(extension_manifest),
            "mpfbBundledBaseObjSha256": sha256(base_obj),
            "mpfbGameEngineRigSha256": EXPECTED_GAME_ENGINE_RIG_SHA256,
            "mpfbGameEngineWeightsSha256": EXPECTED_GAME_ENGINE_WEIGHTS_SHA256,
            "mpfbStockTPoseSha256": EXPECTED_GAME_ENGINE_TPOSE_SHA256,
        },
        "evidence": evidence_files,
    }
    BUILD_MANIFEST.write_text(json.dumps(build_manifest, indent=2) + "\n", encoding="utf-8")

    print(json.dumps({
        "status": report["status"],
        "intakeReport": str(INTAKE_REPORT),
        "intakeReportSha256": sha256(INTAKE_REPORT),
        "rigFitLandmarks": str(LANDMARK_REPORT),
        "rigFitLandmarksSha256": sha256(LANDMARK_REPORT),
        "neutralMidpointProposalCanonicalSha256": neutral_refit_sha256,
        "perBuildManifest": str(BUILD_MANIFEST),
        "perBuildManifestSha256": sha256(BUILD_MANIFEST),
        "checkpointBlend": str(CHECKPOINT_BLEND),
        "checkpointBlendSha256": sha256(CHECKPOINT_BLEND),
    }, indent=2))


def parse_args() -> argparse.Namespace:
    args = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--stage", choices=("early",), default="early")
    return parser.parse_args(args)


def main() -> None:
    args = parse_args()
    if args.stage == "early":
        run_early_stage()


if __name__ == "__main__":
    main()
