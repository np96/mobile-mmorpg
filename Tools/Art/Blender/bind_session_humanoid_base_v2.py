#!/usr/bin/env python3
"""Bind accepted SessionHumanoidBaseV1 surfaces to SessionHumanoidRigV2.

The command rebuilds the exact accepted MPFB body intake so it can carry the
audited game_engine weight field through the evaluated stock-T-pose bake.  It
then maps that field onto 55 Humanoid bones plus two project-owned elbow
supports, clamps to four
normalized weights, runs deformation gates, and writes only DCC/FBX evidence
outside Unity Assets.
"""

from __future__ import annotations

from datetime import datetime, timezone
import hashlib
import heapq
import inspect
import json
import math
from pathlib import Path
import sys
import traceback

import bmesh
import bpy
from mathutils import Vector

from bl_ext.blender_org.mpfb.services import HumanService, RigService

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_session_humanoid_base_v1 as intake
import session_humanoid_base_v2_binding_contract as contract


FBX_CREATION_TIMESTAMP = datetime(2026, 8, 20, 8, 0, 0, tzinfo=timezone.utc)
CHECKPOINT_BLEND = contract.V1_CHECKPOINT_BLEND
LANDMARK_ARTIFACT = contract.V1_RIG_FIT_LANDMARKS


def activate(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.scene.unit_settings.system = "METRIC"
    bpy.context.scene.unit_settings.scale_length = 1.0
    bpy.context.scene.render.fps = 30


def verify_frozen_inputs() -> tuple[dict[str, object], dict[str, object], dict[str, object], dict[str, object]]:
    contract.require(
        all((
            contract.EXPECTED_RIG_FBX_SHA256,
            contract.EXPECTED_RIG_MANIFEST_SHA256,
            contract.EXPECTED_ANIMATION_FBX_SHA256,
            contract.EXPECTED_ANIMATION_MANIFEST_SHA256,
        )),
        "SessionHumanoidRigV2/AnimationProofV2 hashes are not frozen; binding execution is disabled",
    )
    contract.require(contract.sha256(contract.RIG_FBX) == contract.EXPECTED_RIG_FBX_SHA256, "Refitted rig FBX changed")
    contract.require(contract.sha256(contract.RIG_MANIFEST) == contract.EXPECTED_RIG_MANIFEST_SHA256, "Refitted rig manifest changed")
    contract.require(contract.sha256(contract.ANIMATION_FBX) == contract.EXPECTED_ANIMATION_FBX_SHA256, "Frozen animation FBX changed")
    contract.require(contract.sha256(contract.ANIMATION_MANIFEST) == contract.EXPECTED_ANIMATION_MANIFEST_SHA256, "Frozen animation manifest changed")
    contract.bow_draw_pose_snapshot_contract()
    contract.require(contract.sha256(LANDMARK_ARTIFACT) == contract.EXPECTED_LANDMARK_ARTIFACT_SHA256, "Body landmark artifact changed")
    rig_manifest = json.loads(contract.RIG_MANIFEST.read_text(encoding="utf-8"))
    animation_manifest = json.loads(contract.ANIMATION_MANIFEST.read_text(encoding="utf-8"))
    compatibility = rig_manifest["v1Compatibility"]
    contract.require(compatibility["first66BoneRecordsUnchanged"] and compatibility["first66BoneRecordsOrderUnchanged"], "V2 no longer preserves frozen V1 records")
    contract.require(compatibility["first66OrderedRecordsSerializedSha256"] == contract.EXPECTED_V1_FIRST66_ORDERED_RECORDS_SHA256, "V2 first-66 record hash changed")
    contract.require([entry["name"] for entry in rig_manifest["hierarchy"]["bones"][-2:]] == list(contract.ELBOW_SUPPORT_GROUPS), "V2 support append order changed")
    contract.require(rig_manifest["restFit"]["inputArtifactSha256"] == contract.EXPECTED_LANDMARK_ARTIFACT_SHA256, "Rig no longer fits the accepted body artifact")
    contract.require(animation_manifest["sourceRig"]["fbxSha256"] == contract.EXPECTED_RIG_FBX_SHA256, "Animation proof uses a different rest rig")
    source_support_report = contract.elbow_support_manifest_report(rig_manifest)
    body_provenance_report = contract.v1_body_provenance_report(rig_manifest, LANDMARK_ARTIFACT)
    return rig_manifest, animation_manifest, source_support_report, body_provenance_report


def append_checkpoint_references() -> dict[str, bpy.types.Object]:
    names = [f"SessionHumanoidBaseV1_{variant}_MPFBSource" for variant in contract.VARIANTS]
    with bpy.data.libraries.load(str(CHECKPOINT_BLEND), link=False) as (source, target):
        missing = sorted(set(names) - set(source.objects))
        contract.require(not missing, f"Accepted checkpoint is missing {missing}")
        target.objects = names
    result = {}
    for obj in target.objects:
        contract.require(obj is not None and obj.type == "MESH", "Checkpoint reference failed to append")
        bpy.context.collection.objects.link(obj)
        result[obj.name.split("_")[1]] = obj
        obj.hide_set(True)
        obj.hide_render = True
    contract.require(set(result) == set(contract.VARIANTS), f"Checkpoint variant set changed: {set(result)}")
    return result


def import_refitted_rig(rig_manifest: dict[str, object]) -> tuple[bpy.types.Object, list[str], dict[str, object]]:
    bpy.ops.import_scene.fbx(
        filepath=str(contract.RIG_FBX),
        use_anim=False,
        automatic_bone_orientation=False,
        ignore_leaf_bones=False,
    )
    objects = list(bpy.context.scene.objects)
    armatures = [obj for obj in objects if obj.type == "ARMATURE"]
    contract.require(len(armatures) == 1, f"Rig import produced {len(armatures)} armatures")
    rig = armatures[0]
    contract.require(rig.name == "SessionHumanoidRigV2", f"Unexpected rig name {rig.name}")
    contract.require(len(rig.data.bones) == contract.EXPECTED_RIG_JOINTS, "Rig joint count changed")
    contract.require(contract.near_zero(rig.location) and contract.near_zero(rig.rotation_euler) and contract.near_one(rig.scale), "Rig root transform is not identity")
    deform_groups = list(rig_manifest["hierarchy"]["deformBoneNames"])
    contract.require(len(deform_groups) == contract.EXPECTED_DEFORM_GROUPS, "Humanoid deform group count changed")
    expected_deform = set(contract.MPFB_TO_SESSION.values()) | set(contract.ZERO_INFLUENCE_FACE_GROUPS) | set(contract.ELBOW_SUPPORT_GROUPS)
    contract.require(expected_deform == set(deform_groups), "V2 deform set is not exactly 55 Humanoid plus two elbow-support groups")
    for bone in rig.data.bones:
        bone.use_deform = bone.name in deform_groups
    contract.require(sum(bone.use_deform for bone in rig.data.bones) == contract.EXPECTED_DEFORM_GROUPS, "Rig deform flags are not exactly 57")
    ordered_bones = [entry["name"] for entry in rig_manifest["hierarchy"]["bones"]]
    support_report = contract.elbow_support_rest_report(
        rig,
        ordered_bones,
        imported_fbx_semantics=True,
    )
    return rig, deform_groups, support_report


def capture_mpfb_weight_field(
    human: bpy.types.Object,
    helper_indices: set[int],
) -> tuple[list[int], list[list[tuple[str, float]]], dict[str, object]]:
    kept_indices = [index for index in range(len(human.data.vertices)) if index not in helper_indices]
    contract.require(len(kept_indices) == contract.EXPECTED_VERTICES, f"Helper deletion would retain {len(kept_indices)} vertices")
    group_names = {group.index: group.name for group in human.vertex_groups}
    allowed_source = set(contract.MPFB_TO_SESSION) | {"Root"}
    rows: list[list[tuple[str, float]]] = []
    source_maximum = 0
    source_minimum_sum = float("inf")
    source_maximum_sum = 0.0
    source_root_assignments = 0
    digest = hashlib.sha256()
    for original_index in kept_indices:
        memberships = []
        for membership in human.data.vertices[original_index].groups:
            name = group_names[membership.group]
            if name not in allowed_source or membership.weight <= 1e-8:
                continue
            memberships.append((name, float(membership.weight)))
        memberships.sort()
        total = sum(weight for _name, weight in memberships)
        source_minimum_sum = min(source_minimum_sum, total)
        source_maximum_sum = max(source_maximum_sum, total)
        source_maximum = max(source_maximum, len(memberships))
        source_root_assignments += sum(name == "Root" for name, _weight in memberships)
        contract.require(0.999 <= total <= 1.001, f"MPFB source vertex {original_index} weight sum is {total}")
        for name, weight in memberships:
            digest.update(original_index.to_bytes(4, "little"))
            digest.update(name.encode("utf-8") + b"\0")
            digest.update(float(weight).hex().encode("ascii") + b"\n")
        rows.append(memberships)
    contract.require(source_root_assignments == 0, "MPFB Root unexpectedly carries skin weight")
    return kept_indices, rows, {
        "capturedAfterPhenotypeRigFitBeforeStockTPoseBake": True,
        "retainedVertexCount": len(rows),
        "sourceMaximumInfluences": source_maximum,
        "sourceMinimumWeightSum": source_minimum_sum,
        "sourceMaximumWeightSum": source_maximum_sum,
        "sourceRootAssignments": source_root_assignments,
        "carriedWeightFieldSha256": digest.hexdigest(),
    }


def evaluated_mesh_copy(source: bpy.types.Object, name: str) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
    result = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(result)
    return result


def mirrored_group_name(name: str) -> str:
    if name.startswith("Left"):
        return "Right" + name[len("Left"):]
    if name.startswith("Right"):
        return "Left" + name[len("Right"):]
    return name


def canonical_body_mirror_pairs(vertex_count: int) -> list[tuple[int, int]]:
    """Return the exact topological mirror map from the pinned MPFB base OBJ."""
    base_obj, _manifest = intake.mpfb_paths()
    positions = []
    with base_obj.open("r", encoding="utf-8") as stream:
        for line in stream:
            if line.startswith("v "):
                positions.append(tuple(float(value) for value in line.split()[1:4]))
                if len(positions) == vertex_count:
                    break
    contract.require(len(positions) == vertex_count, "Pinned MPFB base OBJ retained vertex range changed")
    lookup = {
        (round(x, 9), round(y, 9), round(z, 9)): index
        for index, (x, y, z) in enumerate(positions)
    }
    contract.require(len(lookup) == vertex_count, "Pinned MPFB retained base vertices are not unique")
    pairs = []
    for index, (x, y, z) in enumerate(positions):
        if x <= 1e-9:
            continue
        other = lookup.get((round(-x, 9), round(y, 9), round(z, 9)))
        contract.require(other is not None, f"Pinned MPFB base vertex {index} has no exact mirror")
        pairs.append((index, int(other)))
    contract.require(pairs, "Pinned MPFB base produced no topological mirror pairs")
    return pairs


def weight_field_sha256(rows: list[dict[str, float]]) -> str:
    payload = [
        [(name, float(weight).hex()) for name, weight in sorted(row.items()) if weight > 1e-8]
        for row in rows
    ]
    return hashlib.sha256(json.dumps(payload, separators=(",", ":")).encode("utf-8")).hexdigest()


def maximum_mirrored_weight_l1(
    rows: list[dict[str, float]],
    pairs: list[tuple[int, int]],
) -> float:
    maximum = 0.0
    for left, right in pairs:
        right_as_left = {mirrored_group_name(name): weight for name, weight in rows[right].items()}
        names = set(rows[left]) | set(right_as_left)
        maximum = max(maximum, sum(abs(rows[left].get(name, 0.0) - right_as_left.get(name, 0.0)) for name in names))
    return maximum


def symmetrize_weight_field(
    obj: bpy.types.Object,
    mapped_rows: list[dict[str, float]],
) -> dict[str, object]:
    """Average mapped weights across exact base-topology counterparts.

    MPFB's stock game_engine field contains small left/right paint differences.
    Accepted surface coordinates remain untouched; only the mapped deformation
    field is made symmetric before the deterministic four-weight clamp.
    """
    pairs = canonical_body_mirror_pairs(len(obj.data.vertices))
    pre_hash = weight_field_sha256(mapped_rows)
    maximum_pre_average_l1 = 0.0
    changed_pairs = 0
    for left, right in pairs:
        contract.require(obj.data.vertices[left].co.x > 0.0 and obj.data.vertices[right].co.x < 0.0, f"Morphed mirror pair {left}/{right} crossed the center plane")
        left_row = mapped_rows[left]
        right_as_left = {mirrored_group_name(name): weight for name, weight in mapped_rows[right].items()}
        names = sorted(set(left_row) | set(right_as_left))
        pre_average_l1 = sum(abs(left_row.get(name, 0.0) - right_as_left.get(name, 0.0)) for name in names)
        maximum_pre_average_l1 = max(maximum_pre_average_l1, pre_average_l1)
        if pre_average_l1 > 1e-8:
            changed_pairs += 1
        averaged = {
            name: (left_row.get(name, 0.0) + right_as_left.get(name, 0.0)) * 0.5
            for name in names
        }
        averaged = {name: weight for name, weight in averaged.items() if weight > 1e-8}
        mapped_rows[left] = averaged
        mapped_rows[right] = {mirrored_group_name(name): weight for name, weight in averaged.items()}
    post_hash = weight_field_sha256(mapped_rows)
    maximum_post_average_l1 = maximum_mirrored_weight_l1(mapped_rows, pairs)
    contract.require(maximum_post_average_l1 <= 1e-12, "Topology-mirrored weight averaging did not produce an exact symmetric field")
    return {
        "method": "exact mirror-index pairs from pinned MPFB base.obj; average canonical mapped rows before spatial correction and four-weight clamp; mirror bone names; no coordinate/UV/topology changes",
        "pairCount": len(pairs),
        "changedPairCount": changed_pairs,
        "maximumPreAverageL1WeightDelta": maximum_pre_average_l1,
        "maximumPostAverageL1WeightDelta": maximum_post_average_l1,
        "preAverageWeightFieldSha256": pre_hash,
        "postAverageWeightFieldSha256": post_hash,
    }


def apply_elbow_support_envelope(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    mapped_rows: list[dict[str, float]],
    mirror_pairs: list[tuple[int, int]],
) -> dict[str, object]:
    """Move elbow influence into the V2 half-rotation support bone."""
    upper_bone = rig.data.bones["LeftUpperArm"]
    elbow = upper_bone.tail_local.copy()
    arm_axis = (upper_bone.tail_local - upper_bone.head_local).normalized()
    transition_extent = 0.160
    seed_offset = 0.120
    radial_limit = 0.150
    before_hash = weight_field_sha256(mapped_rows)
    pair_by_left = {left: right for left, right in mirror_pairs}
    neutral_points: dict[int, Vector] = {}
    longitudinal: dict[int, float] = {}
    candidates: set[int] = set()
    for left_index, right_index in mirror_pairs:
        left_point = obj.data.vertices[left_index].co
        right_point = obj.data.vertices[right_index].co
        neutral_point = Vector((
            (left_point.x - right_point.x) * 0.5,
            (left_point.y + right_point.y) * 0.5,
            (left_point.z + right_point.z) * 0.5,
        ))
        axial = (neutral_point - elbow).dot(arm_axis)
        radial = ((neutral_point - elbow) - (arm_axis * axial)).length
        row = mapped_rows[left_index]
        arm_total = row.get("LeftUpperArm", 0.0) + row.get("LeftLowerArm", 0.0)
        if abs(axial) > transition_extent or radial > radial_limit or arm_total <= 0.20:
            continue
        neutral_points[left_index] = neutral_point
        longitudinal[left_index] = axial
        candidates.add(left_index)
    contract.require(candidates, "No elbow-transition vertices were selected")

    adjacency: dict[int, list[tuple[int, float]]] = {index: [] for index in candidates}
    for edge in obj.data.edges:
        first, second = (int(value) for value in edge.vertices)
        if first not in candidates or second not in candidates:
            continue
        distance = (neutral_points[first] - neutral_points[second]).length
        if distance <= 1e-9:
            continue
        adjacency[first].append((second, distance))
        adjacency[second].append((first, distance))

    upper_seeds = {index for index in candidates if longitudinal[index] <= -seed_offset}
    lower_seeds = {index for index in candidates if longitudinal[index] >= seed_offset}
    contract.require(upper_seeds and lower_seeds, "Elbow geodesic transition has no boundary seeds")

    def geodesic_distances(seeds: set[int]) -> dict[int, float]:
        distances = {index: float("inf") for index in candidates}
        queue: list[tuple[float, int]] = []
        for index in sorted(seeds):
            distances[index] = 0.0
            heapq.heappush(queue, (0.0, index))
        while queue:
            distance, index = heapq.heappop(queue)
            if distance != distances[index]:
                continue
            for neighbor, edge_length in adjacency[index]:
                candidate = distance + edge_length
                if candidate < distances[neighbor]:
                    distances[neighbor] = candidate
                    heapq.heappush(queue, (candidate, neighbor))
        return distances

    distance_to_upper = geodesic_distances(upper_seeds)
    distance_to_lower = geodesic_distances(lower_seeds)
    corrected_vertices = 0
    unreachable = []
    for left_index in sorted(candidates):
        upper_distance = distance_to_upper[left_index]
        lower_distance = distance_to_lower[left_index]
        if not math.isfinite(upper_distance) or not math.isfinite(lower_distance):
            unreachable.append(left_index)
            continue
        denominator = upper_distance + lower_distance
        lower_fraction = 0.5 if denominator <= 1e-9 else upper_distance / denominator
        support_fraction = contract.elbow_support_envelope(lower_fraction)
        remaining_fraction = 1.0 - support_fraction
        row = mapped_rows[left_index]
        arm_total = row.get("LeftUpperArm", 0.0) + row.get("LeftLowerArm", 0.0)
        corrected = dict(row)
        corrected["LeftUpperArm"] = arm_total * remaining_fraction * (1.0 - lower_fraction)
        corrected["LeftLowerArm"] = arm_total * remaining_fraction * lower_fraction
        corrected["LeftElbowSupport"] = arm_total * support_fraction
        mapped_rows[left_index] = {name: weight for name, weight in corrected.items() if weight > 1e-8}
        right_index = pair_by_left[left_index]
        mapped_rows[right_index] = {
            mirrored_group_name(name): weight
            for name, weight in mapped_rows[left_index].items()
        }
        corrected_vertices += 2
    contract.require(not unreachable, f"Elbow geodesic transition has unreachable vertices: {unreachable[:16]}")
    maximum_l1 = maximum_mirrored_weight_l1(mapped_rows, mirror_pairs)
    contract.require(maximum_l1 <= 1e-12, "Elbow-support envelope introduced weight-field asymmetry")
    return {
        "method": "pair-symmetric multi-source Dijkstra geodesic upper/lower coordinate; h=0 outside (.20,.80), smoothstep fades .20->.42 and .80->.58, rigid h=1 core [.42,.58]; residual influence splits by t; preserve total arm and all non-arm influence",
        "elbowMeters": list(elbow),
        "armAxis": list(arm_axis),
        "candidateAxialExtentMeters": transition_extent,
        "boundarySeedOffsetMeters": seed_offset,
        "radialLimitMeters": radial_limit,
        "candidatePairCount": len(candidates),
        "upperSeedCount": len(upper_seeds),
        "lowerSeedCount": len(lower_seeds),
        "supportEnvelope": contract.ELBOW_SUPPORT_ENVELOPE_CONTRACT,
        "supportPoseRotationFactor": contract.ELBOW_SUPPORT_ROTATION_FACTOR,
        "correctedVertexCount": corrected_vertices,
        "preCorrectionWeightFieldSha256": before_hash,
        "postCorrectionWeightFieldSha256": weight_field_sha256(mapped_rows),
        "maximumPostCorrectionMirroredL1WeightDelta": maximum_l1,
    }


def prune_and_normalize_weights(
    obj: bpy.types.Object,
    source_rows: list[list[tuple[str, float]]],
    rig: bpy.types.Object,
) -> tuple[list[list[tuple[str, float]]], dict[str, object]]:
    mapped_rows: list[dict[str, float]] = []
    opposite_assignments_corrected = 0
    opposite_weight_mass_corrected = 0.0
    for vertex, source_row in zip(obj.data.vertices, source_rows):
        mapped: dict[str, float] = {}
        for source_name, weight in source_row:
            if source_name == "Root":
                continue
            target_name = contract.MPFB_TO_SESSION[source_name]
            corrected_name = target_name
            if vertex.co.x > contract.OPPOSITE_SIDE_DEADBAND_METERS and target_name.startswith("Right"):
                corrected_name = "Left" + target_name[len("Right"):]
            elif vertex.co.x < -contract.OPPOSITE_SIDE_DEADBAND_METERS and target_name.startswith("Left"):
                corrected_name = "Right" + target_name[len("Left"):]
            if corrected_name != target_name:
                contract.require(corrected_name in contract.MPFB_TO_SESSION.values(), f"No mirrored target for {target_name}")
                opposite_assignments_corrected += 1
                opposite_weight_mass_corrected += weight
            mapped[corrected_name] = mapped.get(corrected_name, 0.0) + weight
        total = sum(mapped.values())
        contract.require(total > 1e-8, f"Vertex {vertex.index} lost all mapped weights")
        mapped_rows.append({name: weight / total for name, weight in mapped.items() if weight > 1e-8})

    symmetry_report = symmetrize_weight_field(obj, mapped_rows)
    mirror_pairs = canonical_body_mirror_pairs(len(obj.data.vertices))
    spatial_repaint_vertices = 0
    introduced_upper_chest_complements = 0
    for left_index, right_index in mirror_pairs:
        left_vertex = obj.data.vertices[left_index]
        right_vertex = obj.data.vertices[right_index]
        average_z = (left_vertex.co.z + right_vertex.co.z) * 0.5
        average_abs_x = (abs(left_vertex.co.x) + abs(right_vertex.co.x)) * 0.5
        if not (1.00 <= average_z <= 1.70 and 0.05 <= average_abs_x <= 0.60):
            continue
        side_names = {
            name for name in mapped_rows[left_index]
            if name.startswith("Left") and (
                "Shoulder" in name
                or "UpperArm" in name
                or "LowerArm" in name
                or "Hand" in name
                or any(finger in name for finger in ("Thumb", "Index", "Middle", "Ring", "Little"))
            )
        }
        side_total = sum(mapped_rows[left_index].get(name, 0.0) for name in side_names)
        if side_total <= 0.02:
            continue
        central_names = set(mapped_rows[left_index]) - side_names
        central_total = sum(mapped_rows[left_index].get(name, 0.0) for name in central_names)
        if central_total <= 1e-8:
            central_names = {"UpperChest"}
            central_total = 1.0
            mapped_rows[left_index]["UpperChest"] = 1.0
            introduced_upper_chest_complements += 2
        desired_side_total = max(0.0, min(1.0, (average_abs_x - 0.08) / 0.38))
        strength_t = max(0.0, min(1.0, (side_total - 0.02) / 0.18))
        repaint_strength = strength_t * strength_t * (3.0 - (2.0 * strength_t))
        final_side_total = side_total + ((desired_side_total - side_total) * repaint_strength)
        repainted = {}
        for name in side_names:
            repainted[name] = mapped_rows[left_index][name] * final_side_total / side_total
        for name in central_names:
            repainted[name] = mapped_rows[left_index][name] * (1.0 - final_side_total) / central_total
        repainted = {name: value for name, value in repainted.items() if value > 1e-8}
        mapped_rows[left_index] = repainted
        mapped_rows[right_index] = {mirrored_group_name(name): value for name, value in repainted.items()}
        spatial_repaint_vertices += 2
    post_spatial_l1 = maximum_mirrored_weight_l1(mapped_rows, mirror_pairs)
    contract.require(post_spatial_l1 <= 1e-12, "Symmetric shoulder correction introduced weight-field asymmetry")
    post_spatial_hash = weight_field_sha256(mapped_rows)
    elbow_report = apply_elbow_support_envelope(obj, rig, mapped_rows, mirror_pairs)

    result: list[list[tuple[str, float]]] = []
    truncated_vertices = 0
    truncated_weight_mass = 0.0
    maximum_before = 0
    for vertex, mapped in zip(obj.data.vertices, mapped_rows):
        positive = [(name, weight) for name, weight in mapped.items() if weight > 1e-8]
        maximum_before = max(maximum_before, len(positive))
        positive.sort(key=lambda item: (-item[1], item[0]))
        if len(positive) > contract.MAX_INFLUENCES:
            truncated_vertices += 1
            truncated_weight_mass += sum(weight for _name, weight in positive[contract.MAX_INFLUENCES:])
            positive = positive[:contract.MAX_INFLUENCES]
        total = sum(weight for _name, weight in positive)
        contract.require(total > 1e-8, f"Vertex {vertex.index} lost all weights")
        normalized = sorted((name, weight / total) for name, weight in positive)
        result.append(normalized)
    final_dict_rows = [dict(row) for row in result]
    final_mirror_l1 = maximum_mirrored_weight_l1(final_dict_rows, mirror_pairs)
    contract.require(final_mirror_l1 <= 1e-12, "Four-weight clamp introduced weight-field asymmetry")
    support_weight_stats = {}
    for side in ("Left", "Right"):
        name = f"{side}ElbowSupport"
        assignments = [row.get(name, 0.0) for row in final_dict_rows]
        positive = [weight for weight in assignments if weight > 1e-8]
        contract.require(positive, f"{name} has no final skin assignments")
        contract.require(max(positive) >= 0.95, f"{name} envelope does not own the elbow center")
        support_weight_stats[name] = {
            "assignedVertexCount": len(positive),
            "minimumPositiveWeight": min(positive),
            "maximumWeight": max(positive),
            "totalWeightMass": sum(positive),
        }
    return result, {
        "maximumMappedInfluencesBeforeClamp": maximum_before,
        "maximumFinalInfluences": max(len(row) for row in result),
        "verticesClampedToFour": truncated_vertices,
        "totalDiscardedWeightMassBeforeRenormalization": truncated_weight_mass,
        "oppositeSideAssignmentsCorrectedOutsideSeamBand": opposite_assignments_corrected,
        "oppositeSideWeightMassCorrected": opposite_weight_mass_corrected,
        "shoulderSpatialRepaint": {
            "vertexCount": spatial_repaint_vertices,
            "selection": "pair-average 1.00m <= z <= 1.70m, 0.05m <= abs(x) <= 0.60m, and carried same-side arm-chain weight >2%",
            "targetArmChainInfluence": "linear clamp((pair-average abs(x)-0.08m)/0.38m, 0, 1)",
            "repaintStrength": "smoothstep(clamp((carriedArmChainWeight-0.02)/0.18, 0, 1))",
            "preservesRelativeWeightsWithinArmAndNonArmChains": True,
            "pureLimbRowsReceiveUpperChestTransitionComplement": introduced_upper_chest_complements,
            "symmetricSelection": True,
            "maximumPostSpatialMirroredL1WeightDelta": post_spatial_l1,
            "postSpatialWeightFieldSha256": post_spatial_hash,
        },
        "weightFieldSymmetry": symmetry_report,
        "elbowSupportEnvelope": elbow_report,
        "finalWeightFieldSha256": weight_field_sha256(final_dict_rows),
        "finalMaximumMirroredL1WeightDelta": final_mirror_l1,
        "elbowSupportWeightAudit": {
            "status": "PASS",
            "groups": support_weight_stats,
            "maximumMirroredL1WeightDelta": final_mirror_l1,
        },
        "correctionMethod": "deterministic mirrored-bone reassignment outside +/-3mm center seam; continuous spatial repaint of total same-side arm-chain influence; exact base-topology pair averaging; top-four stable sort by weight then bone name; exact renormalization",
    }


def assign_weight_rows(
    obj: bpy.types.Object,
    deform_groups: list[str],
    rows: list[list[tuple[str, float]]],
) -> None:
    for group in list(obj.vertex_groups):
        obj.vertex_groups.remove(group)
    for name in deform_groups:
        obj.vertex_groups.new(name=name)
    group_indices = {group.name: group.index for group in obj.vertex_groups}
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    deform_layer = bm.verts.layers.deform.verify()
    for vertex, row in zip(bm.verts, rows):
        weights = vertex[deform_layer]
        weights.clear()
        for name, weight in row:
            weights[group_indices[name]] = weight
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.update()


def remove_temporary_mpfb(human: bpy.types.Object, temporary_rig: bpy.types.Object) -> None:
    human_mesh = human.data
    armature = temporary_rig.data
    bpy.data.objects.remove(human, do_unlink=True)
    bpy.data.objects.remove(temporary_rig, do_unlink=True)
    if human_mesh.users == 0:
        bpy.data.meshes.remove(human_mesh)
    if armature.users == 0:
        bpy.data.armatures.remove(armature)


def compare_checkpoint_geometry(obj: bpy.types.Object, reference: bpy.types.Object) -> dict[str, object]:
    contract.require(len(obj.data.vertices) == len(reference.data.vertices), "Checkpoint vertex count differs")
    deviations = [(vertex.co - reference.data.vertices[vertex.index].co).length for vertex in obj.data.vertices]
    maximum = max(deviations)
    contract.require(maximum <= 2e-6, f"Regenerated body differs from accepted checkpoint by {maximum}m")
    return {
        "acceptedCheckpointObject": reference.name,
        "maximumVertexDeviationMeters": maximum,
        "toleranceMeters": 2e-6,
        "exactTopologySha256": contract.topology_sha256(obj),
        "uv0LoopSha256": contract.uv_loop_sha256(obj),
    }


def build_variant(
    variant: str,
    reference: bpy.types.Object,
    rig: bpy.types.Object,
    deform_groups: list[str],
    landmark_artifact: dict[str, object],
) -> tuple[bpy.types.Object, dict[str, object]]:
    preset = intake.build_macro_preset(intake.VARIANTS[variant])
    human = HumanService.create_human(
        mask_helpers=False,
        detailed_helpers=True,
        extra_vertex_groups=True,
        feet_on_ground=True,
        scale=0.1,
        macro_detail_dict=preset,
    )
    human.name = f"TemporaryMPFB_{variant}_BindingHuman"
    helper_indices = intake.helper_vertex_indices(human)
    extension_root = Path(inspect.getfile(HumanService)).resolve().parent.parent
    rig_path = extension_root / "data/rigs/standard/rig.game_engine.json"
    weights_path = extension_root / "data/rigs/standard/weights.game_engine.json"
    pose_path = extension_root / "data/poses/game_engine_fk/t-pose.json"
    contract.require(contract.sha256(weights_path) == contract.EXPECTED_MPFb_WEIGHTS_SHA256, "MPFB game_engine weights changed")
    contract.require(contract.sha256(pose_path) == contract.EXPECTED_MPFb_TPOSE_SHA256, "MPFB stock T-pose changed")
    temporary_rig = HumanService.add_builtin_rig(human, "game_engine", import_weights=True)
    contract.require(temporary_rig is not None, "MPFB temporary game_engine rig creation failed")
    rig_fit = intake.validate_temporary_rig_fit(human, temporary_rig, rig_path)
    kept_indices, source_rows, field_report = capture_mpfb_weight_field(human, helper_indices)

    pose = json.loads(pose_path.read_text(encoding="utf-8"))
    activate(temporary_rig)
    bpy.ops.object.mode_set(mode="POSE")
    RigService.set_pose_from_dict(temporary_rig, pose)
    bpy.ops.object.mode_set(mode="OBJECT")
    bpy.context.view_layer.update()
    result = evaluated_mesh_copy(human, contract.BOUND_MESH_NAMES[variant])
    intake.clear_vertex_groups(result)
    intake.delete_vertices(result, helper_indices)
    normalization = intake.normalize_height(result)
    result.data.uv_layers.active.name = "UV0"
    expected_hashes = landmark_artifact["variants"][variant]["retainedBodySourceHashes"]
    contract.require(contract.topology_sha256(result) == expected_hashes["topologySha256"], "Accepted topology hash changed")
    contract.require(contract.uv_loop_sha256(result) == expected_hashes["productionUv0LoopSha256"], "Accepted UV0 hash changed")
    contract.require(len(result.data.vertices) == len(kept_indices) == contract.EXPECTED_VERTICES, "Retained body vertex count changed")
    contract.require(len(contract.triangle_indices(result.data)) == contract.EXPECTED_TRIANGLES, "Retained body triangle count changed")
    checkpoint_comparison = compare_checkpoint_geometry(result, reference)

    final_rows, correction_report = prune_and_normalize_weights(result, source_rows, rig)
    assign_weight_rows(result, deform_groups, final_rows)
    modifier = result.modifiers.new("SessionHumanoidRigV2_Skin", "ARMATURE")
    modifier.object = rig
    modifier.use_vertex_groups = True
    modifier.use_bone_envelopes = False
    result.parent = rig
    result.matrix_parent_inverse = rig.matrix_world.inverted()
    result.location = (0.0, 0.0, 0.0)
    result.rotation_euler = (0.0, 0.0, 0.0)
    result.scale = (1.0, 1.0, 1.0)
    for polygon in result.data.polygons:
        polygon.use_smooth = True
    weight_report = contract.weight_audit(result, deform_groups)
    remove_temporary_mpfb(human, temporary_rig)
    return result, {
        "variant": variant,
        "macroPreset": preset,
        "normalization": normalization,
        "sourceVertices": len(result.data.vertices),
        "sourceTriangles": len(contract.triangle_indices(result.data)),
        "topologySha256": contract.topology_sha256(result),
        "uv0LoopSha256": contract.uv_loop_sha256(result),
        "positionSha256": contract.position_sha256(result),
        "objectTransform": {"location": list(result.location), "rotationEuler": list(result.rotation_euler), "scale": list(result.scale)},
        "mpfbTemporaryRigFit": rig_fit,
        "mpfbWeightField": field_report,
        "mpfbToSessionBoneMap": dict(sorted(contract.MPFB_TO_SESSION.items())),
        "zeroInfluenceFacialGroups": list(contract.ZERO_INFLUENCE_FACE_GROUPS),
        "weightCorrection": correction_report,
        "weightAudit": weight_report,
        "checkpointComparison": checkpoint_comparison,
    }


def create_clay_material() -> bpy.types.Material:
    material = bpy.data.materials.new("SessionHumanoidBaseV2_BoundClay")
    material.use_nodes = True
    principled = material.node_tree.nodes.get("Principled BSDF")
    principled.inputs["Base Color"].default_value = (0.32, 0.17, 0.11, 1.0)
    principled.inputs["Roughness"].default_value = 0.58
    return material


def assign_material(obj: bpy.types.Object, material: bpy.types.Material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)


def snapshot_evaluated(obj: bpy.types.Object, name: str, location: Vector, material: bpy.types.Material) -> bpy.types.Object:
    snapshot = evaluated_mesh_copy(obj, name)
    snapshot.location = location
    snapshot.modifiers.clear()
    intake.clear_vertex_groups(snapshot)
    snapshot.parent = None
    assign_material(snapshot, material)
    return snapshot


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_evidence_scene() -> tuple[bpy.types.Object, bpy.types.Material]:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x = 3200
    scene.render.resolution_y = 1800
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world = scene.world or bpy.data.worlds.new("SessionHumanoidBaseV2_BoundEvidenceWorld")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.008, 0.012, 0.022, 1.0)
    background.inputs["Strength"].default_value = 0.25
    camera_data = bpy.data.cameras.new("BoundEvidenceCamera")
    camera = bpy.data.objects.new("BoundEvidenceCamera", camera_data)
    bpy.context.collection.objects.link(camera)
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 9.8
    camera.location = (0.0, -18.0, 4.0)
    look_at(camera, Vector((0.0, 0.0, 4.0)))
    scene.camera = camera
    for index, (location, energy, size) in enumerate((
        ((-5.0, -7.0, 10.0), 1800.0, 7.0),
        ((6.0, -2.0, 7.0), 1100.0, 6.0),
        ((0.0, 6.0, 9.0), 900.0, 5.0),
    )):
        data = bpy.data.lights.new(f"BoundEvidenceLight{index}", "AREA")
        light = bpy.data.objects.new(data.name, data)
        bpy.context.collection.objects.link(light)
        light.location = location
        data.energy = energy
        data.shape = "DISK"
        data.size = size
        look_at(light, Vector((0.0, 0.0, 3.5)))
    return camera, create_clay_material()


def render_snapshot_sheet(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    variant: str,
    actions: dict[str, bpy.types.Action],
    kind: str,
) -> Path:
    _camera, material = configure_evidence_scene()
    obj.hide_render = True
    obj.hide_set(True)
    snapshots = []
    columns = 5
    if kind == "Static":
        selections = [
            next(spec for spec in contract.STATIC_POSES if spec.name == name)
            for name in (
                "ArmsDown", "ArmsForward90", "ArmsOverhead150", "ArmsBack35", "HumeralTwistPositive70",
                "Elbow110", "PronationPositive80", "Elbow110PronationPositive80", "Elbow110PronationNegative80", "BowDraw",
                "Fist", "BowGrip", "Hip90Knee110", "AnkleDorsi25", "Toe45",
            )
        ]
        for index, spec in enumerate(selections):
            contract.reset_pose(rig)
            spec.configure(rig)
            contract.apply_elbow_support_pose(rig)
            bpy.context.view_layer.update()
            col = index % columns
            row = index // columns
            location = Vector(((col - 2) * 1.9, 0.0, (1 - row) * 3.1))
            snapshots.append(snapshot_evaluated(obj, f"Evidence_{variant}_{spec.name}", location, material))
    else:
        selections = [
            ("CombatIdle", 25),
            ("LocomotionForward", 7),
            ("LocomotionForward", 19),
            ("Hit", 18),
            ("Death", 42),
            ("Interrupt", 16),
            ("HunterBowReady", 13),
            ("HunterBowDraw", 20),
            ("HunterBowLoose", 10),
            ("HunterBowRecovery", 18),
        ]
        if rig.animation_data is None:
            rig.animation_data_create()
        for index, (clip, frame) in enumerate(selections):
            contract.bind_action_slot(rig, actions[clip], clip)
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            col = index % columns
            row = index // columns
            location = Vector(((col - 2) * 1.9, 0.0, (1 - row) * 3.3))
            snapshots.append(snapshot_evaluated(obj, f"Evidence_{variant}_{clip}_{frame}", location, material))
    path = contract.EVIDENCE_DIR / f"SessionHumanoidBaseV2_{variant}_Bound{kind}PoseSheet.png"
    path.parent.mkdir(parents=True, exist_ok=True)
    bpy.context.scene.render.filepath = str(path)
    bpy.ops.render.render(write_still=True)
    for snapshot in snapshots:
        mesh = snapshot.data
        bpy.data.objects.remove(snapshot, do_unlink=True)
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)
    for obj_to_remove in [item for item in list(bpy.data.objects) if item.type in {"CAMERA", "LIGHT"}]:
        object_type = obj_to_remove.type
        data = obj_to_remove.data
        bpy.data.objects.remove(obj_to_remove, do_unlink=True)
        if data.users == 0:
            if object_type == "CAMERA":
                bpy.data.cameras.remove(data)
            else:
                bpy.data.lights.remove(data)
    obj.hide_set(False)
    obj.hide_render = False
    contract.reset_pose(rig)
    return path


def deterministic_export_fbx(rig: bpy.types.Object, mesh: bpy.types.Object, path: Path) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True)
    mesh.select_set(True)
    bpy.context.view_layer.objects.active = rig
    from io_scene_fbx import export_fbx_bin, fbx_utils
    original_header_writer = export_fbx_bin.fbx_header_elements
    original_uuid_factory = fbx_utils._key_to_uuid

    def deterministic_header_writer(root, scene_data, time=None):
        return original_header_writer(root, scene_data, time=FBX_CREATION_TIMESTAMP)

    def deterministic_uuid_factory(uuids, key):
        if isinstance(key, int) and 0 <= key < 2**63:
            value = key
        else:
            value = int.from_bytes(hashlib.sha256(repr(key).encode("utf-8")).digest()[:8], "big") & ((1 << 63) - 1)
            value = value or 1
        while value in uuids:
            value = (value + 1) & ((1 << 63) - 1)
            value = value or 1
        return fbx_utils.UUID(value)

    export_fbx_bin.fbx_header_elements = deterministic_header_writer
    fbx_utils._keys_to_uuids.clear()
    fbx_utils._uuids_to_keys.clear()
    fbx_utils._key_to_uuid = deterministic_uuid_factory
    try:
        bpy.ops.export_scene.fbx(
            filepath=str(path),
            use_selection=True,
            object_types={"ARMATURE", "MESH"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS",
            bake_space_transform=False,
            axis_forward="-Z",
            axis_up="Y",
            add_leaf_bones=False,
            primary_bone_axis="Y",
            secondary_bone_axis="X",
            use_armature_deform_only=False,
            armature_nodetype="ROOT",
            bake_anim=False,
            use_custom_props=True,
            path_mode="AUTO",
            embed_textures=False,
        )
    finally:
        export_fbx_bin.fbx_header_elements = original_header_writer
        fbx_utils._key_to_uuid = original_uuid_factory
        fbx_utils._keys_to_uuids.clear()
        fbx_utils._uuids_to_keys.clear()
    contract.require(path.is_file() and path.stat().st_size > 100_000, f"Bound FBX export failed: {path}")


def write_failure(error: BaseException) -> None:
    contract.BINDING_REPORT.parent.mkdir(parents=True, exist_ok=True)
    report = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV2",
        "status": "REJECT_V2_BINDING_OR_DEFORMATION_GATE",
        "unityIntegrationAllowed": False,
        "error": str(error),
        "traceback": traceback.format_exc(),
        "rigFbxSha256": contract.sha256(contract.RIG_FBX) if contract.RIG_FBX.is_file() else None,
        "animationFbxSha256": contract.sha256(contract.ANIMATION_FBX) if contract.ANIMATION_FBX.is_file() else None,
        "builderSha256": contract.sha256(Path(__file__)),
        "bindingContractSha256": contract.sha256(Path(contract.__file__)),
        "canonicalOutputsWritten": contract.BASE_OUTPUT_DIR.exists(),
        "note": "Limits were not weakened. Canonical SessionHumanoidBaseV2 outputs are written only by the independent verifier after all gates pass.",
    }
    contract.BINDING_REPORT.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def run() -> None:
    contract.require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    rig_manifest, animation_manifest, source_support_report, body_provenance_report = verify_frozen_inputs()
    contract.require(not contract.BASE_OUTPUT_DIR.exists(), f"Canonical V2 outputs already exist: {contract.BASE_OUTPUT_DIR}")
    contract.require(not contract.PROMOTION_TEMP_DIR.exists(), f"Incomplete V2 promotion exists: {contract.PROMOTION_TEMP_DIR}")
    contract.require(not contract.STAGED_PACKAGE_DIR.exists(), f"Staged package already exists: {contract.STAGED_PACKAGE_DIR}")
    contract.STAGING_ROOT.mkdir(parents=True, exist_ok=True)
    contract.BINDING_REPORT.parent.mkdir(parents=True, exist_ok=True)
    reset_scene()
    references = append_checkpoint_references()
    rig, deform_groups, support_rest_report = import_refitted_rig(rig_manifest)
    landmark_artifact = json.loads(LANDMARK_ARTIFACT.read_text(encoding="utf-8"))
    bodies = {}
    variant_reports = {}
    for variant in contract.VARIANTS:
        body, report = build_variant(variant, references[variant], rig, deform_groups, landmark_artifact)
        bodies[variant] = body
        variant_reports[variant] = report
    for reference in references.values():
        mesh = reference.data
        bpy.data.objects.remove(reference, do_unlink=True)
        if mesh.users == 0:
            bpy.data.meshes.remove(mesh)

    actions, animation_input = contract.import_animation_actions(rig)
    for variant, body in bodies.items():
        variant_reports[variant]["staticDeformationGates"] = contract.run_static_deformation_gates(body, rig, deform_groups)
        variant_reports[variant]["animationDeformationGates"] = contract.run_animation_deformation_gates(body, rig, deform_groups, actions)

    evidence = {}
    for variant, body in bodies.items():
        static_path = render_snapshot_sheet(body, rig, variant, actions, "Static")
        animation_path = render_snapshot_sheet(body, rig, variant, actions, "Animation")
        evidence[variant] = {
            "staticPoseSheet": str(static_path.relative_to(contract.REPOSITORY_ROOT)),
            "staticPoseSheetSha256": contract.sha256(static_path),
            "animationPoseSheet": str(animation_path.relative_to(contract.REPOSITORY_ROOT)),
            "animationPoseSheetSha256": contract.sha256(animation_path),
        }

    contract.reset_pose(rig)
    if rig.animation_data is not None:
        rig.animation_data_clear()
    for action in list(bpy.data.actions):
        bpy.data.actions.remove(action)
    for body in bodies.values():
        body.hide_set(False)
        body.hide_render = False
    contract.STAGED_PACKAGE_DIR.mkdir(parents=False, exist_ok=False)
    bpy.ops.wm.save_as_mainfile(filepath=str(contract.STAGED_BLEND), check_existing=False)
    for variant, body in bodies.items():
        deterministic_export_fbx(rig, body, contract.STAGED_FBX_PATHS[variant])

    source_hashes = {
        "builder": contract.sha256(Path(__file__)),
        "bindingContract": contract.sha256(Path(contract.__file__)),
        "intakeBuilder": contract.sha256(Path(intake.__file__)),
        "bodyLandmarkArtifact": contract.sha256(LANDMARK_ARTIFACT),
        "acceptedBodyCheckpoint": contract.sha256(CHECKPOINT_BLEND),
        "bodyBuildManifest": contract.sha256(contract.V1_BODY_BUILD_MANIFEST),
        "bodyIntakeReport": contract.sha256(contract.V1_BODY_INTAKE_REPORT),
        "rigFbx": contract.sha256(contract.RIG_FBX),
        "rigManifest": contract.sha256(contract.RIG_MANIFEST),
        "animationFbx": contract.sha256(contract.ANIMATION_FBX),
        "animationManifest": contract.sha256(contract.ANIMATION_MANIFEST),
        "bowDrawPoseSnapshot": contract.sha256(contract.BOW_DRAW_POSE_SNAPSHOT),
    }
    staged_outputs = {
        "boundBlend": {"path": str(contract.STAGED_BLEND.relative_to(contract.REPOSITORY_ROOT)), "sha256": contract.sha256(contract.STAGED_BLEND)},
        "fbx": {
            variant: {"path": str(path.relative_to(contract.REPOSITORY_ROOT)), "sha256": contract.sha256(path)}
            for variant, path in contract.STAGED_FBX_PATHS.items()
        },
    }
    report = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV2",
        "status": "PASS_V2_DCC_AND_DEFORMATION_GATES_STAGED_PENDING_INDEPENDENT_FBX_ROUND_TRIP",
        "unityIntegrationAllowed": False,
        "canonicalOutputsWritten": False,
        "sourceStatus": "preserved 26,756-triangle MPFB topology; class occlusion cuts and production LODs remain deferred",
        "weightSource": "MPFB 2.0.17 game_engine field carried by unchanged vertex index through the evaluated stock-T-pose bake, then exact pair symmetrization, shoulder transition, and project-owned V2 geodesic elbow-support envelope; no heat/envelope auto-weighting",
        "rigAndAnimationFreeze": {
            "rigFbxSha256": source_hashes["rigFbx"],
            "rigManifestSha256": source_hashes["rigManifest"],
            "animationFbxSha256": source_hashes["animationFbx"],
            "animationManifestSha256": source_hashes["animationManifest"],
            "animationDeterminismEvidence": "accepted V2 rig/animation hashes are pinned from the rig owner's cross-seed deterministic build and independent round-trip report",
        },
        "animationInput": animation_input,
        "elbowSupportSourceManifestContract": source_support_report,
        "elbowSupportImportedFbxSemantics": support_rest_report,
        "acceptedBodyProvenance": body_provenance_report,
        "variants": variant_reports,
        "evidence": evidence,
        "sourceHashes": source_hashes,
        "stagedOutputs": staged_outputs,
        "deferred": [
            "independent bound-FBX round-trip command and atomic canonical promotion",
            "class-specific hidden-body-face deletion and final LOD0/1/2",
            "class textures/material maps and gear",
            "Unity Assets/import/rebuild/tests",
        ],
    }
    contract.BINDING_REPORT.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    staged_manifest = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV2",
        "status": report["status"],
        "unityIntegrationAllowed": False,
        "canonicalOutputsWritten": False,
        "sourceHashes": source_hashes,
        "stagedOutputs": staged_outputs,
        "elbowSupportSourceManifestContract": source_support_report,
        "elbowSupportImportedFbxSemantics": support_rest_report,
        "acceptedBodyProvenance": body_provenance_report,
        "variants": {
            variant: {
                "meshName": bodies[variant].name,
                "vertices": variant_reports[variant]["sourceVertices"],
                "triangles": variant_reports[variant]["sourceTriangles"],
                "topologySha256": variant_reports[variant]["topologySha256"],
                "uv0LoopSha256": variant_reports[variant]["uv0LoopSha256"],
                "positionSha256": variant_reports[variant]["positionSha256"],
                "weightAudit": variant_reports[variant]["weightAudit"],
                "weightCorrection": variant_reports[variant]["weightCorrection"],
            }
            for variant in contract.VARIANTS
        },
        "evidence": evidence,
        "bindingReport": {
            "path": str(contract.BINDING_REPORT.relative_to(contract.REPOSITORY_ROOT)),
            "sha256": contract.sha256(contract.BINDING_REPORT),
        },
    }
    contract.STAGED_BUILD_MANIFEST.write_text(json.dumps(staged_manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(json.dumps({
        "status": report["status"],
        "bindingReport": str(contract.BINDING_REPORT),
        "bindingReportSha256": contract.sha256(contract.BINDING_REPORT),
        "stagedBuildManifest": str(contract.STAGED_BUILD_MANIFEST),
        "stagedBuildManifestSha256": contract.sha256(contract.STAGED_BUILD_MANIFEST),
        "stagedFbx": {variant: contract.sha256(path) for variant, path in contract.STAGED_FBX_PATHS.items()},
    }, indent=2, sort_keys=True))


def main() -> None:
    if contract.BASE_OUTPUT_DIR.exists():
        raise RuntimeError(
            f"Canonical V2 outputs already exist; refusing rerun without changing accepted evidence: "
            f"{contract.BASE_OUTPUT_DIR}"
        )
    try:
        run()
    except BaseException as error:
        write_failure(error)
        raise


if __name__ == "__main__":
    main()
