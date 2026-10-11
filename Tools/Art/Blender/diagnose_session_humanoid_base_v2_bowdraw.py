#!/usr/bin/env python3
"""Scratch-only anatomical diagnosis for the V2 BowDraw penetration gate.

No blend, FBX, SourceArt, staging, canonical, or Unity output is written.  The
only output is a JSON diagnostic under repository-level outputs/art/blender.
"""

from __future__ import annotations

from collections import deque
import hashlib
import inspect
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree
from mathutils.geometry import closest_point_on_tri, intersect_ray_tri

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bind_session_humanoid_base_v2 as binder
import build_session_humanoid_base_v1 as intake
import session_humanoid_base_v2_binding_contract as contract
import study_session_humanoid_base_v2_elbow_envelopes as study


REPORT_PATH = (
    contract.REPOSITORY_ROOT
    / "outputs/art/blender/session-humanoid-base-v2-bowdraw-diagnosis"
    / "SessionHumanoidBaseV2BowDrawDiagnosis.json"
)
TARGET_TRIANGLES = (6219, 6222, 6223, 6954, 15445)
TARGET_PAIRS = ((6219, 15445), (6222, 15445), (6223, 15445), (6954, 15445))
CANDIDATE = next(candidate for candidate in study.CANDIDATES if candidate.name == "axial_compact_quartic_120mm")


def row_sha256(rows: list[list[tuple[str, float]]]) -> str:
    payload = [
        [(name, float(weight).hex()) for name, weight in row]
        for row in rows
    ]
    return hashlib.sha256(json.dumps(payload, separators=(",", ":")).encode("utf-8")).hexdigest()


def object_rows(obj: bpy.types.Object) -> list[dict[str, float]]:
    return contract.object_weight_rows(obj)


def anatomical_region(row: dict[str, float]) -> str:
    family_mass = {
        "torso": sum(row.get(name, 0.0) for name in ("Hips", "Spine", "Chest", "UpperChest", "Neck", "Head")),
        "shoulder_upper_arm": sum(weight for name, weight in row.items() if "Shoulder" in name or "UpperArm" in name or "ElbowSupport" in name),
        "forearm_hand": sum(weight for name, weight in row.items() if "LowerArm" in name or "Hand" in name or any(finger in name for finger in ("Thumb", "Index", "Middle", "Ring", "Little"))),
        "leg_foot": sum(weight for name, weight in row.items() if any(token in name for token in ("UpperLeg", "LowerLeg", "Foot", "Toes"))),
    }
    return max(sorted(family_mass), key=lambda name: family_mass[name])


def averaged_row(rows: list[dict[str, float]], vertices: tuple[int, int, int]) -> dict[str, float]:
    result = {}
    for index in vertices:
        for name, weight in rows[index].items():
            result[name] = result.get(name, 0.0) + (weight / 3.0)
    return {name: weight for name, weight in sorted(result.items()) if weight > 1e-8}


def triangle_normal(points: list[Vector], triangle: tuple[int, int, int]) -> Vector:
    a, b, c = (points[index] for index in triangle)
    normal = (b - a).cross(c - a)
    return normal.normalized() if normal.length_squared > 1e-18 else Vector()


def triangle_center(points: list[Vector], triangle: tuple[int, int, int]) -> Vector:
    return sum((points[index] for index in triangle), Vector()) / 3.0


def segment_segment_distance(a0: Vector, a1: Vector, b0: Vector, b1: Vector) -> float:
    u = a1 - a0
    v = b1 - b0
    w = a0 - b0
    aa = u.dot(u)
    bb = u.dot(v)
    cc = v.dot(v)
    dd = u.dot(w)
    ee = v.dot(w)
    denominator = (aa * cc) - (bb * bb)
    s_numerator = 0.0
    s_denominator = denominator
    t_numerator = 0.0
    t_denominator = denominator
    if denominator < 1e-14:
        s_numerator = 0.0
        s_denominator = 1.0
        t_numerator = ee
        t_denominator = cc
    else:
        s_numerator = (bb * ee) - (cc * dd)
        t_numerator = (aa * ee) - (bb * dd)
        if s_numerator < 0.0:
            s_numerator = 0.0
            t_numerator = ee
            t_denominator = cc
        elif s_numerator > s_denominator:
            s_numerator = s_denominator
            t_numerator = ee + bb
            t_denominator = cc
    if t_numerator < 0.0:
        t_numerator = 0.0
        if -dd < 0.0:
            s_numerator = 0.0
        elif -dd > aa:
            s_numerator = s_denominator
        else:
            s_numerator = -dd
            s_denominator = aa
    elif t_numerator > t_denominator:
        t_numerator = t_denominator
        if (-dd + bb) < 0.0:
            s_numerator = 0.0
        elif (-dd + bb) > aa:
            s_numerator = s_denominator
        else:
            s_numerator = -dd + bb
            s_denominator = aa
    sc = 0.0 if abs(s_numerator) < 1e-14 else s_numerator / s_denominator
    tc = 0.0 if abs(t_numerator) < 1e-14 else t_numerator / t_denominator
    return (w + (u * sc) - (v * tc)).length


def triangle_distance(points: list[Vector], first: tuple[int, int, int], second: tuple[int, int, int]) -> float:
    a = [points[index] for index in first]
    b = [points[index] for index in second]
    if contract.triangles_intersect(points, first, second):
        return 0.0
    distances = []
    for point in a:
        distances.append((point - closest_point_on_tri(point, b[0], b[1], b[2])).length)
    for point in b:
        distances.append((point - closest_point_on_tri(point, a[0], a[1], a[2])).length)
    for first_index in range(3):
        for second_index in range(3):
            distances.append(segment_segment_distance(
                a[first_index],
                a[(first_index + 1) % 3],
                b[second_index],
                b[(second_index + 1) % 3],
            ))
    return min(distances)


def crossing_records(points: list[Vector], first: tuple[int, int, int], second: tuple[int, int, int]) -> list[dict[str, object]]:
    result = []
    for source_label, source, target_label, target in (("first", first, "second", second), ("second", second, "first", first)):
        a, b, c = (points[index] for index in target)
        normal = (b - a).cross(c - a)
        if normal.length_squared <= 1e-18:
            continue
        normal.normalize()
        for edge_index in range(3):
            start_index = source[edge_index]
            end_index = source[(edge_index + 1) % 3]
            start = points[start_index]
            end = points[end_index]
            direction = end - start
            if direction.length <= 1e-12:
                continue
            hit = intersect_ray_tri(a, b, c, direction.normalized(), start, True)
            if hit is None or (hit - start).length > direction.length + 1e-7:
                continue
            result.append({
                "sourceTriangle": source_label,
                "targetTriangle": target_label,
                "edgeVertices": [start_index, end_index],
                "hitPoint": list(hit),
                "startPlaneDistanceMeters": (start - a).dot(normal),
                "endPlaneDistanceMeters": (end - a).dot(normal),
                "crossingDepthMeters": contract.crossing_edge_depth(start, end, a, b, c),
            })
    return result


def shortest_topology_path(obj: bpy.types.Object, first: tuple[int, int, int], second: tuple[int, int, int]) -> dict[str, object]:
    targets = set(second)
    parents = {index: None for index in first}
    queue = deque(first)
    adjacency = [[] for _ in obj.data.vertices]
    for edge in obj.data.edges:
        a, b = (int(index) for index in edge.vertices)
        adjacency[a].append(b)
        adjacency[b].append(a)
    found = None
    while queue and found is None:
        current = queue.popleft()
        if current in targets:
            found = current
            break
        for neighbor in sorted(adjacency[current]):
            if neighbor in parents:
                continue
            parents[neighbor] = current
            queue.append(neighbor)
    contract.require(found is not None, "Target triangles are in disconnected topology")
    path = []
    current = found
    while current is not None:
        path.append(current)
        current = parents[current]
    path.reverse()
    return {
        "edgeCount": len(path) - 1,
        "vertexPath": path,
        "restSurfacePathMeters": sum(
            (obj.data.vertices[first_index].co - obj.data.vertices[second_index].co).length
            for first_index, second_index in zip(path, path[1:])
        ),
    }


def face_report(
    obj: bpy.types.Object,
    rows: list[dict[str, float]],
    rest: list[Vector],
    posed: list[Vector],
    triangle_index: int,
    triangles: list[tuple[int, int, int]],
) -> dict[str, object]:
    triangle = triangles[triangle_index]
    mean_row = averaged_row(rows, triangle)
    return {
        "triangle": triangle_index,
        "vertices": list(triangle),
        "anatomicalRegion": anatomical_region(mean_row),
        "averageWeights": sorted(mean_row.items(), key=lambda item: (-item[1], item[0])),
        "vertexWeights": {str(index): sorted(rows[index].items(), key=lambda item: (-item[1], item[0])) for index in triangle},
        "restPositions": {str(index): list(rest[index]) for index in triangle},
        "posedPositions": {str(index): list(posed[index]) for index in triangle},
        "restCenter": list(triangle_center(rest, triangle)),
        "posedCenter": list(triangle_center(posed, triangle)),
        "restNormal": list(triangle_normal(rest, triangle)),
        "posedNormal": list(triangle_normal(posed, triangle)),
        "restAreaSquareMeters": contract.triangle_area(*(rest[index] for index in triangle)),
        "posedAreaSquareMeters": contract.triangle_area(*(posed[index] for index in triangle)),
    }


def pair_report(
    obj: bpy.types.Object,
    rest: list[Vector],
    posed: list[Vector],
    triangles: list[tuple[int, int, int]],
    first_index: int,
    second_index: int,
) -> dict[str, object]:
    first = triangles[first_index]
    second = triangles[second_index]
    return {
        "triangles": [first_index, second_index],
        "shareVertex": bool(set(first) & set(second)),
        "shortestTopologyPath": shortest_topology_path(obj, first, second),
        "restMinimumSurfaceDistanceMeters": triangle_distance(rest, first, second),
        "posedMinimumSurfaceDistanceMeters": triangle_distance(posed, first, second),
        "restIntersects": contract.triangles_intersect(rest, first, second),
        "posedIntersects": contract.triangles_intersect(posed, first, second),
        "approximateDepthMeters": contract.approximate_intersection_depth(posed, first, second),
        "posedCrossings": crossing_records(posed, first, second),
        "restNormalDot": triangle_normal(rest, first).dot(triangle_normal(rest, second)),
        "posedNormalDot": triangle_normal(posed, first).dot(triangle_normal(posed, second)),
    }


def global_bowdraw_metrics(rest: list[Vector], posed: list[Vector], triangles: list[tuple[int, int, int]], edges: list[tuple[int, int]]) -> dict[str, object]:
    area_ratios = [
        contract.triangle_area(*(posed[index] for index in triangle))
        / contract.triangle_area(*(rest[index] for index in triangle))
        for triangle in triangles
    ]
    edge_ratios = [
        (posed[first] - posed[second]).length / (rest[first] - rest[second]).length
        for first, second in edges
    ]
    return {
        "minimumTriangleAreaRatio": min(area_ratios),
        "minimumTriangle": area_ratios.index(min(area_ratios)),
        "minimumEdgeLengthRatio": min(edge_ratios),
        "minimumEdge": list(edges[edge_ratios.index(min(edge_ratios))]),
        "maximumEdgeLengthRatio": max(edge_ratios),
        "maximumEdge": list(edges[edge_ratios.index(max(edge_ratios))]),
    }


def all_intersections_over_limit(posed: list[Vector], triangles: list[tuple[int, int, int]]) -> dict[str, object]:
    tree = BVHTree.FromPolygons([tuple(point) for point in posed], triangles, all_triangles=True, epsilon=1e-7)
    seen = set()
    failures = []
    maximum = 0.0
    exact_intersections = 0
    for first_index, second_index in tree.overlap(tree):
        if first_index == second_index:
            continue
        key = (min(first_index, second_index), max(first_index, second_index))
        if key in seen:
            continue
        seen.add(key)
        first = triangles[key[0]]
        second = triangles[key[1]]
        if set(first) & set(second) or not contract.triangles_intersect(posed, first, second):
            continue
        exact_intersections += 1
        depth = contract.approximate_intersection_depth(posed, first, second)
        maximum = max(maximum, depth)
        if depth > contract.MAX_PENETRATION_METERS:
            failures.append({
                "triangles": list(key),
                "vertices": [list(first), list(second)],
                "depthMeters": depth,
            })
    failures.sort(key=lambda item: (-item["depthMeters"], item["triangles"]))
    return {
        "candidatePairCount": len(seen),
        "exactIntersectionCount": exact_intersections,
        "overLimitCount": len(failures),
        "maximumApproximateDepthMeters": maximum,
        "overLimit": failures[:128],
        "limitMeters": contract.MAX_PENETRATION_METERS,
    }


def map_symmetrized_mpfb_rows_without_repaint(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    source_rows: list[list[tuple[str, float]]],
) -> tuple[list[dict[str, float]], list[tuple[int, int]], dict[str, object]]:
    mapped_rows = []
    for vertex, source_row in zip(obj.data.vertices, source_rows):
        mapped = {}
        for source_name, weight in source_row:
            if source_name == "Root":
                continue
            target_name = contract.MPFB_TO_SESSION[source_name]
            corrected_name = target_name
            if vertex.co.x > contract.OPPOSITE_SIDE_DEADBAND_METERS and target_name.startswith("Right"):
                corrected_name = "Left" + target_name[len("Right"):]
            elif vertex.co.x < -contract.OPPOSITE_SIDE_DEADBAND_METERS and target_name.startswith("Left"):
                corrected_name = "Right" + target_name[len("Left"):]
            mapped[corrected_name] = mapped.get(corrected_name, 0.0) + weight
        total = sum(mapped.values())
        contract.require(total > 1e-8, f"No-repaint vertex {vertex.index} lost all weights")
        mapped_rows.append({name: weight / total for name, weight in mapped.items() if weight > 1e-8})
    symmetry = binder.symmetrize_weight_field(obj, mapped_rows)
    pairs = binder.canonical_body_mirror_pairs(len(obj.data.vertices))
    envelope = study.apply_candidate_envelope(CANDIDATE, obj, rig, mapped_rows, pairs)
    center, _control = study.apply_center_atomic_max4(mapped_rows)
    return mapped_rows, pairs, {
        "mapping": "same MPFB->Session/opposite-side correction and exact pair averaging as binder; shoulderSpatialRepaint omitted; same axial 120mm support and scratch center correction",
        "symmetry": symmetry,
        "envelope": envelope,
        "center": center,
    }


def build_project_ab() -> tuple[bpy.types.Object, bpy.types.Object, bpy.types.Object, dict[str, object]]:
    rig_manifest, _animation_manifest, _source_support, _body_provenance = binder.verify_frozen_inputs()
    references = binder.append_checkpoint_references()
    rig, deform_groups, _imported_support = binder.import_refitted_rig(rig_manifest)
    artifact = json.loads(binder.LANDMARK_ARTIFACT.read_text(encoding="utf-8"))
    state = {}
    original_envelope = binder.apply_elbow_support_envelope
    original_prune = binder.prune_and_normalize_weights

    def scratch_envelope(obj, target_rig, mapped_rows, mirror_pairs):
        envelope = study.apply_candidate_envelope(CANDIDATE, obj, target_rig, mapped_rows, mirror_pairs)
        center, _control = study.apply_center_atomic_max4(mapped_rows)
        envelope["scratchCenterCorrection"] = center
        return envelope

    def capture_prune(obj, source_rows, target_rig):
        state["sourceRows"] = [list(row) for row in source_rows]
        return original_prune(obj, source_rows, target_rig)

    binder.apply_elbow_support_envelope = scratch_envelope
    binder.prune_and_normalize_weights = capture_prune
    try:
        current, build = binder.build_variant("Masculine", references["Masculine"], rig, deform_groups, artifact)
    finally:
        binder.apply_elbow_support_envelope = original_envelope
        binder.prune_and_normalize_weights = original_prune
    no_repaint_rows, _pairs, no_repaint_build = map_symmetrized_mpfb_rows_without_repaint(
        current,
        rig,
        state["sourceRows"],
    )
    no_repaint_final = study.generic_max4_rows(no_repaint_rows)
    no_repaint = study.clone_control_body(current, rig, deform_groups, no_repaint_final)
    no_repaint.name = "SessionHumanoidBaseV2_Masculine_NoShoulderRepaint"
    return rig, current, no_repaint, {
        "currentBuild": build,
        "noRepaintBuild": no_repaint_build,
        "currentWeightSha256": binder.weight_field_sha256(object_rows(current)),
        "noRepaintWeightSha256": row_sha256(no_repaint_final),
    }


def pose_session_bowdraw(rig: bpy.types.Object, apply_support: bool) -> None:
    contract.reset_pose(rig)
    contract.pose_bow_draw(rig)
    if apply_support:
        contract.apply_elbow_support_pose(rig)
    bpy.context.view_layer.update()


def build_original_mpfb_equivalent() -> tuple[bpy.types.Object, bpy.types.Object, dict[str, object]]:
    preset = intake.build_macro_preset(intake.VARIANTS["Masculine"])
    human = binder.HumanService.create_human(
        mask_helpers=False,
        detailed_helpers=True,
        extra_vertex_groups=True,
        feet_on_ground=True,
        scale=0.1,
        macro_detail_dict=preset,
    )
    helper_indices = intake.helper_vertex_indices(human)
    temporary_rig = binder.HumanService.add_builtin_rig(human, "game_engine", import_weights=True)
    contract.require(temporary_rig is not None, "MPFB comparison rig creation failed")
    extension_root = Path(inspect.getfile(binder.HumanService)).resolve().parent.parent
    pose_path = extension_root / "data/poses/game_engine_fk/t-pose.json"
    pose = json.loads(pose_path.read_text(encoding="utf-8"))
    binder.activate(temporary_rig)
    bpy.ops.object.mode_set(mode="POSE")
    binder.RigService.set_pose_from_dict(temporary_rig, pose)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    rest = binder.evaluated_mesh_copy(human, "MPFB_GameEngine_TPose_Rest")
    intake.delete_vertices(rest, helper_indices)
    rest_normalization = intake.normalize_height(rest)
    contract.rotate_pair(temporary_rig, "upperarm_l", "upperarm_r", Vector((0.0, 0.0, 1.0)), -90.0, 90.0)
    contract.rotate_pose_bone_armature_space(temporary_rig, "lowerarm_r", Vector((0.0, 0.0, 1.0)), 110.0)
    contract.rotate_pose_bone_armature_space(temporary_rig, "hand_r", Vector((1.0, 0.0, 0.0)), -18.0)
    bpy.context.view_layer.update()
    posed = binder.evaluated_mesh_copy(human, "MPFB_GameEngine_BowDraw")
    intake.delete_vertices(posed, helper_indices)
    posed_normalization = intake.normalize_height(posed)
    binder.remove_temporary_mpfb(human, temporary_rig)
    return rest, posed, {
        "method": "original phenotype-fitted MPFB game_engine rig and stock weights; immutable stock T-pose followed by equivalent armature-space upperarm forward90/right elbow110/right hand -18 rotations; helper-free evaluated mesh; no Session mapping/repaint/support",
        "restNormalization": rest_normalization,
        "posedNormalization": posed_normalization,
    }


def surface_configuration_report(
    name: str,
    obj: bpy.types.Object,
    rest: list[Vector],
    posed: list[Vector],
    rows: list[dict[str, float]] | None,
) -> dict[str, object]:
    triangles = contract.triangle_indices(obj.data)
    edges = contract.unique_edges(triangles)
    result = {
        "name": name,
        "vertexCount": len(rest),
        "triangleCount": len(triangles),
        "globalBowDraw": global_bowdraw_metrics(rest, posed, triangles, edges),
        "selfIntersection": all_intersections_over_limit(posed, triangles),
        "pairs": {
            f"{first}-{second}": pair_report(obj, rest, posed, triangles, first, second)
            for first, second in TARGET_PAIRS
        },
    }
    if rows is not None:
        result["faces"] = {
            str(index): face_report(obj, rows, rest, posed, index, triangles)
            for index in TARGET_TRIANGLES
        }
    return result


def main() -> None:
    contract.require(bpy.app.version >= (5, 2, 0), "Blender 5.2+ required")
    contract.require(not contract.BASE_OUTPUT_DIR.exists(), "Canonical V2 body outputs must remain absent")
    contract.require(not contract.STAGED_PACKAGE_DIR.exists(), "Staged V2 body package must remain absent")
    binder.reset_scene()
    rig, current, no_repaint, build = build_project_ab()
    rest = [vertex.co.copy() for vertex in current.data.vertices]
    pose_session_bowdraw(rig, True)
    current_posed = contract.evaluated_positions(current)
    no_repaint_posed = contract.evaluated_positions(no_repaint)
    current_report = surface_configuration_report("SessionRigV2_CurrentShoulderRepaint", current, rest, current_posed, object_rows(current))
    no_repaint_report = surface_configuration_report("SessionRigV2_NoShoulderRepaint", no_repaint, rest, no_repaint_posed, object_rows(no_repaint))

    pose_session_bowdraw(rig, False)
    support_identity_posed = contract.evaluated_positions(current)
    support_identity_report = surface_configuration_report("SessionRigV2_CurrentWeights_SupportIdentity", current, rest, support_identity_posed, object_rows(current))

    mpfb_rest_obj, mpfb_posed_obj, mpfb_build = build_original_mpfb_equivalent()
    mpfb_rest = [vertex.co.copy() for vertex in mpfb_rest_obj.data.vertices]
    mpfb_posed = [vertex.co.copy() for vertex in mpfb_posed_obj.data.vertices]
    mpfb_report = surface_configuration_report("OriginalMPFBGameEngineWeightsAndRig", mpfb_rest_obj, mpfb_rest, mpfb_posed, None)

    report = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV2BowDrawDiagnosis",
        "status": "READ_ONLY_DIAGNOSIS",
        "sourceFilesMutated": False,
        "canonicalOutputsWritten": False,
        "unityIntegrationAllowed": False,
        "sourceHashes": {
            "diagnostic": contract.sha256(Path(__file__)),
            "binder": contract.sha256(Path(binder.__file__)),
            "contract": contract.sha256(Path(contract.__file__)),
            "envelopeStudy": contract.sha256(Path(study.__file__)),
            "rigFbx": contract.sha256(contract.RIG_FBX),
        },
        "candidate": {
            "name": CANDIDATE.name,
            "formula": CANDIDATE.formula,
        },
        "build": build,
        "mpfbBuild": mpfb_build,
        "configurations": {
            "current": current_report,
            "noShoulderRepaint": no_repaint_report,
            "supportIdentity": support_identity_report,
            "originalMpfb": mpfb_report,
        },
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    contract.require(not contract.BASE_OUTPUT_DIR.exists(), "Diagnostic created canonical output")
    contract.require(not contract.STAGED_PACKAGE_DIR.exists(), "Diagnostic created staged output")
    print(json.dumps({
        "status": report["status"],
        "report": str(REPORT_PATH),
        "reportSha256": contract.sha256(REPORT_PATH),
    }, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
