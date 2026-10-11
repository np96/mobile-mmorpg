#!/usr/bin/env python3
"""Scratch-only SessionHumanoidBaseV2 elbow-support envelope study.

This command never writes SourceArt, staged packages, or canonical body outputs.
It rebuilds the frozen accepted body intake in memory, varies only the support
weight envelope, screens Elbow110, and sends screen-pass candidates through the
unchanged 22-static-pose and 248-animation-frame gates.
"""

from __future__ import annotations

from dataclasses import dataclass
import hashlib
import heapq
import itertools
import json
import math
from pathlib import Path
import sys
import traceback
from typing import Callable

import bpy
from mathutils import Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import bind_session_humanoid_base_v2 as binder
import session_humanoid_base_v2_binding_contract as contract
import verify_session_humanoid_base_v2 as verifier


REPORT_PATH = (
    contract.REPOSITORY_ROOT
    / "outputs/art/blender/session-humanoid-base-v2-elbow-envelope-study"
    / "SessionHumanoidBaseV2ElbowEnvelopeStudy.json"
)
BASELINE_REJECT_REPORT = contract.BINDING_REPORT
LATEST_V2_REJECT_TRIANGLE_INDICES = (19148,)
KNOWN_BASELINE_MASCULINE_TRIANGLE_19148_AREA_RATIO = 0.0644281215289789


def smoothstep(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * (3.0 - (2.0 * value))


def smootherstep(value: float) -> float:
    value = max(0.0, min(1.0, value))
    return value * value * value * ((value * ((value * 6.0) - 15.0)) + 10.0)


@dataclass(frozen=True)
class EnvelopeCandidate:
    name: str
    coordinate: str
    formula: str
    analytic_max_slope: str
    evaluate: Callable[[float, float], float]


def axial_quintic_plateau(_geodesic: float, axial_meters: float) -> float:
    distance = abs(axial_meters)
    if distance <= 0.025:
        return 1.0
    if distance >= 0.160:
        return 0.0
    return smootherstep((0.160 - distance) / 0.135)


def axial_compact_quartic_120mm(_geodesic: float, axial_meters: float) -> float:
    normalized = abs(axial_meters) / 0.120
    if normalized >= 1.0:
        return 0.0
    return (1.0 - (normalized * normalized)) ** 2


def geodesic_quadratic(value: float, _axial_meters: float) -> float:
    value = max(0.0, min(1.0, value))
    return 4.0 * value * (1.0 - value)


def geodesic_quartic(value: float, _axial_meters: float) -> float:
    value = max(0.0, min(1.0, value))
    return 16.0 * value * value * (1.0 - value) * (1.0 - value)


def geodesic_half_smoothstep(value: float, _axial_meters: float) -> float:
    return smoothstep(2.0 * min(max(0.0, value), max(0.0, 1.0 - value)))


def geodesic_half_smootherstep(value: float, _axial_meters: float) -> float:
    return smootherstep(2.0 * min(max(0.0, value), max(0.0, 1.0 - value)))


def geodesic_broad_plateau(value: float, _axial_meters: float) -> float:
    value = max(0.0, min(1.0, value))
    if 0.44 <= value <= 0.56:
        return 1.0
    distance_to_edge = value if value < 0.5 else 1.0 - value
    return smootherstep(distance_to_edge / 0.44)


def geodesic_raised_cosine(value: float, _axial_meters: float) -> float:
    value = max(0.0, min(1.0, value))
    return math.sin(math.pi * value) ** 2


def frozen_piecewise(value: float, _axial_meters: float) -> float:
    return contract.elbow_support_envelope(value)


CANDIDATES = (
    EnvelopeCandidate(
        "frozen_piecewise_geodesic_baseline",
        "multi-source geodesic t",
        "current frozen .20/.42/.58/.80 smoothstep plateau",
        "6.8182 per normalized t (1.5/.22)",
        frozen_piecewise,
    ),
    EnvelopeCandidate(
        "axial_compact_quartic_120mm",
        "physical signed axial distance from elbow",
        "h=(1-(|a|/.120m)^2)^2 inside 120mm; h=0 outside",
        "approximately 12.8300 per meter; zero boundary slope",
        axial_compact_quartic_120mm,
    ),
    EnvelopeCandidate(
        "axial_quintic_plateau_25mm_160mm",
        "physical signed axial distance from elbow",
        "h=1 for |a|<=.025m; quintic smootherstep fade to h=0 at |a|=.160m",
        "13.8889 per meter (1.875/.135m)",
        axial_quintic_plateau,
    ),
    EnvelopeCandidate(
        "geodesic_quadratic_bell",
        "multi-source geodesic t",
        "h=4t(1-t)",
        "4.0 per normalized t; endpoint slope is nonzero",
        geodesic_quadratic,
    ),
    EnvelopeCandidate(
        "geodesic_quartic_c1_bell",
        "multi-source geodesic t",
        "h=16t^2(1-t)^2",
        "approximately 3.0792 per normalized t; zero endpoint slope",
        geodesic_quartic,
    ),
    EnvelopeCandidate(
        "geodesic_half_smoothstep_bell",
        "multi-source geodesic t",
        "h=smoothstep(2*min(t,1-t))",
        "3.0 per normalized t; zero endpoint slope",
        geodesic_half_smoothstep,
    ),
    EnvelopeCandidate(
        "geodesic_half_smootherstep_c2_bell",
        "multi-source geodesic t",
        "h=smootherstep(2*min(t,1-t))",
        "3.75 per normalized t; zero first/second endpoint slope",
        geodesic_half_smootherstep,
    ),
    EnvelopeCandidate(
        "geodesic_broad_quintic_plateau_44_56",
        "multi-source geodesic t",
        "quintic fade over [0,.44], h=1 on [.44,.56], mirrored fade",
        "4.2614 per normalized t (1.875/.44)",
        geodesic_broad_plateau,
    ),
    EnvelopeCandidate(
        "geodesic_raised_cosine_bell",
        "multi-source geodesic t",
        "h=sin^2(pi*t)",
        "pi per normalized t; zero endpoint slope",
        geodesic_raised_cosine,
    ),
)


def weight_field_sha256(rows: list[dict[str, float]]) -> str:
    payload = [
        [(name, float(weight).hex()) for name, weight in sorted(row.items()) if weight > 1e-8]
        for row in rows
    ]
    return hashlib.sha256(json.dumps(payload, separators=(",", ":")).encode("utf-8")).hexdigest()


def generic_max4_rows(rows: list[dict[str, float]]) -> list[list[tuple[str, float]]]:
    """Reproduce the frozen binder's ordinary top-four clamp for a control body."""
    result = []
    for index, row in enumerate(rows):
        positive = [(name, weight) for name, weight in row.items() if weight > 1e-8]
        positive.sort(key=lambda item: (-item[1], item[0]))
        positive = positive[:contract.MAX_INFLUENCES]
        total = sum(weight for _name, weight in positive)
        contract.require(total > 1e-8, f"Control center row {index} lost all weights")
        result.append(sorted((name, weight / total) for name, weight in positive))
    return result


def center_atomic_units(row: dict[str, float]) -> list[dict[str, object]]:
    paired_suffixes = sorted({
        name[len("Left"):]
        if name.startswith("Left")
        else name[len("Right"):]
        for name, weight in row.items()
        if weight > 1e-8 and (name.startswith("Left") or name.startswith("Right"))
    })
    paired_names = {
        name
        for suffix in paired_suffixes
        for name in (f"Left{suffix}", f"Right{suffix}")
    }
    units = []
    for suffix in paired_suffixes:
        left_name = f"Left{suffix}"
        right_name = f"Right{suffix}"
        pair_mass = row.get(left_name, 0.0) + row.get(right_name, 0.0)
        averaged_weight = pair_mass * 0.5
        if averaged_weight <= 1e-8:
            continue
        units.append({
            "key": f"pair:{suffix}",
            "slots": 2,
            "mass": pair_mass,
            "weights": ((left_name, averaged_weight), (right_name, averaged_weight)),
        })
    for name, weight in sorted(row.items()):
        if name in paired_names or weight <= 1e-8:
            continue
        units.append({
            "key": f"single:{name}",
            "slots": 1,
            "mass": weight,
            "weights": ((name, weight),),
        })
    return sorted(units, key=lambda item: item["key"])


def atomic_max4_solution(units: list[dict[str, object]]) -> dict[str, object]:
    feasible = []
    for count in range(1, len(units) + 1):
        for selected in itertools.combinations(units, count):
            slots = sum(int(unit["slots"]) for unit in selected)
            if slots > contract.MAX_INFLUENCES:
                continue
            mass = sum(float(unit["mass"]) for unit in selected)
            keys = tuple(str(unit["key"]) for unit in selected)
            feasible.append((mass, keys, selected, slots))
    contract.require(feasible, "Center atomic optimizer found no feasible influence unit")
    feasible.sort(key=lambda item: (-item[0], item[1]))
    best = feasible[0]
    runner_up = feasible[1] if len(feasible) > 1 else None
    selected_keys = set(best[1])
    retained = {
        name: float(weight)
        for unit in best[2]
        for name, weight in unit["weights"]
    }
    retained_mass = sum(retained.values())
    contract.require(retained_mass > 1e-8, "Center atomic optimizer retained no weight")
    normalized = {
        name: weight / retained_mass
        for name, weight in retained.items()
    }
    return {
        "row": normalized,
        "chosenUnitKeys": list(best[1]),
        "droppedUnitKeys": [str(unit["key"]) for unit in units if unit["key"] not in selected_keys],
        "retainedMassBeforeNormalization": retained_mass,
        "discardedMassBeforeNormalization": max(0.0, sum(float(unit["mass"]) for unit in units) - retained_mass),
        "slotCount": best[3],
        "runnerUp": None if runner_up is None else {
            "unitKeys": list(runner_up[1]),
            "retainedMassBeforeNormalization": runner_up[0],
            "objectiveMargin": best[0] - runner_up[0],
            "slotCount": runner_up[3],
        },
    }


def apply_center_atomic_max4(
    mapped_rows: list[dict[str, float]],
) -> tuple[dict[str, object], list[list[tuple[str, float]]]]:
    """Scratch-only exact self-row symmetry, applied before the frozen clamp."""
    _side_pairs, centers, mirror_source = verifier.canonical_mirror_indices()
    before_rows = [dict(row) for row in mapped_rows]
    control_rows = generic_max4_rows(before_rows)
    before_hash = weight_field_sha256(before_rows)
    center_set = set(centers)
    changed_rows = []
    total_discarded = 0.0
    material_clamp_count = 0
    for index in centers:
        source_row = dict(mapped_rows[index])
        solution = atomic_max4_solution(center_atomic_units(source_row))
        corrected_row = dict(solution["row"])
        mapped_rows[index] = corrected_row
        names = set(source_row) | set(corrected_row)
        l1_change = sum(abs(source_row.get(name, 0.0) - corrected_row.get(name, 0.0)) for name in names)
        discarded = max(
            0.0,
            sum(source_row.values()) - float(solution["retainedMassBeforeNormalization"]),
        )
        total_discarded += discarded
        if discarded > 1e-8:
            material_clamp_count += 1
        if l1_change > 1e-12:
            changed_rows.append({
                "vertex": index,
                "sourceRow": sorted(source_row.items()),
                "correctedRow": sorted(corrected_row.items()),
                "l1Change": l1_change,
                "chosenUnitKeys": solution["chosenUnitKeys"],
                "droppedUnitKeys": solution["droppedUnitKeys"],
                "retainedMassBeforeNormalization": solution["retainedMassBeforeNormalization"],
                "discardedMassBeforeNormalization": discarded,
                "runnerUp": solution["runnerUp"],
            })
    non_center_changed = [
        index
        for index, (before, after) in enumerate(zip(before_rows, mapped_rows))
        if index not in center_set and before != after
    ]
    contract.require(not non_center_changed, f"Scratch center optimizer changed non-center rows: {non_center_changed[:16]}")
    maximum_center_l1 = 0.0
    worst_center = None
    maximum_influences = 0
    for index in centers:
        row = mapped_rows[index]
        mirrored = {binder.mirrored_group_name(name): weight for name, weight in row.items()}
        names = set(row) | set(mirrored)
        l1 = sum(abs(row.get(name, 0.0) - mirrored.get(name, 0.0)) for name in names)
        if l1 > maximum_center_l1:
            maximum_center_l1 = l1
            worst_center = index
        maximum_influences = max(maximum_influences, len(row))
    contract.require(maximum_center_l1 <= 1e-12, f"Scratch center optimizer L1 {maximum_center_l1} at {worst_center}")
    contract.require(maximum_influences <= contract.MAX_INFLUENCES, f"Scratch center optimizer retained {maximum_influences} influences")
    after_hash = weight_field_sha256(mapped_rows)
    v815 = next((row for row in changed_rows if row["vertex"] == 815), None)
    return {
        "status": "PASS",
        "method": "canonical self-rows only; exact Left/Right pair averaging; paired units consume two slots; exhaustive subset optimizer maximizes retained pre-normalization mass; stable lexicographic unit-key tie-break; normalize once",
        "mirrorSource": mirror_source,
        "centerSelfPairCount": len(centers),
        "changedCenterRowCount": len(changed_rows),
        "materialClampRowCount": material_clamp_count,
        "totalDiscardedMassBeforeNormalization": total_discarded,
        "maximumCenterSelfPairL1": maximum_center_l1,
        "maximumCenterInfluences": maximum_influences,
        "beforeWeightFieldSha256": before_hash,
        "afterWeightFieldSha256": after_hash,
        "nonCenterRowsChanged": 0,
        "changedRows": changed_rows,
        "vertex815": v815,
        "controlGenericMax4WeightFieldSha256": hashlib.sha256(
            json.dumps(
                [[(name, float(weight).hex()) for name, weight in row] for row in control_rows],
                separators=(",", ":"),
            ).encode("utf-8")
        ).hexdigest(),
    }, control_rows


def apply_candidate_envelope(
    candidate: EnvelopeCandidate,
    obj: bpy.types.Object,
    rig: bpy.types.Object,
    mapped_rows: list[dict[str, float]],
    mirror_pairs: list[tuple[int, int]],
) -> dict[str, object]:
    """Scratch equivalent of the frozen binder selection with only h changed."""
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
    contract.require(candidates, "Scratch envelope selected no elbow-transition vertices")

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
    contract.require(upper_seeds and lower_seeds, "Scratch envelope has no geodesic boundary seeds")

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
                proposed = distance + edge_length
                if proposed < distances[neighbor]:
                    distances[neighbor] = proposed
                    heapq.heappush(queue, (proposed, neighbor))
        return distances

    distance_to_upper = geodesic_distances(upper_seeds)
    distance_to_lower = geodesic_distances(lower_seeds)
    support_samples = []
    for left_index in sorted(candidates):
        upper_distance = distance_to_upper[left_index]
        lower_distance = distance_to_lower[left_index]
        contract.require(math.isfinite(upper_distance) and math.isfinite(lower_distance), f"Scratch geodesic vertex {left_index} is unreachable")
        denominator = upper_distance + lower_distance
        lower_fraction = 0.5 if denominator <= 1e-9 else upper_distance / denominator
        support_fraction = max(0.0, min(1.0, candidate.evaluate(lower_fraction, longitudinal[left_index])))
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
            binder.mirrored_group_name(name): weight
            for name, weight in mapped_rows[left_index].items()
        }
        support_samples.append({
            "leftVertex": left_index,
            "rightVertex": right_index,
            "geodesicFraction": lower_fraction,
            "axialMeters": longitudinal[left_index],
            "supportFractionOfArmChain": support_fraction,
        })
    maximum_l1 = binder.maximum_mirrored_weight_l1(mapped_rows, mirror_pairs)
    contract.require(maximum_l1 <= 1e-12, "Scratch envelope introduced side-pair asymmetry")
    return {
        "method": "frozen V2 selection/geodesic/residual split with scratch-only parameterized support h",
        "candidate": candidate.name,
        "coordinate": candidate.coordinate,
        "formula": candidate.formula,
        "analyticMaximumSlope": candidate.analytic_max_slope,
        "elbowMeters": list(elbow),
        "armAxis": list(arm_axis),
        "candidateAxialExtentMeters": transition_extent,
        "boundarySeedOffsetMeters": seed_offset,
        "radialLimitMeters": radial_limit,
        "candidatePairCount": len(candidates),
        "upperSeedCount": len(upper_seeds),
        "lowerSeedCount": len(lower_seeds),
        "supportPoseRotationFactor": contract.ELBOW_SUPPORT_ROTATION_FACTOR,
        "correctedVertexCount": len(candidates) * 2,
        "preCorrectionWeightFieldSha256": before_hash,
        "postCorrectionWeightFieldSha256": weight_field_sha256(mapped_rows),
        "maximumPostCorrectionMirroredL1WeightDelta": maximum_l1,
        "sampleExtrema": {
            "minimumSupportFraction": min(sample["supportFractionOfArmChain"] for sample in support_samples),
            "maximumSupportFraction": max(sample["supportFractionOfArmChain"] for sample in support_samples),
        },
    }


def support_edge_gradient_report(obj: bpy.types.Object) -> dict[str, object]:
    rows = verifier.object_weight_dicts(obj)
    per_side = {}
    for side in ("Left", "Right"):
        name = f"{side}ElbowSupport"
        maximum_delta = -1.0
        maximum_slope = -1.0
        worst_delta = None
        worst_slope = None
        for edge in obj.data.edges:
            first, second = (int(value) for value in edge.vertices)
            delta = abs(rows[first].get(name, 0.0) - rows[second].get(name, 0.0))
            length = (obj.data.vertices[first].co - obj.data.vertices[second].co).length
            slope = delta / length if length > 1e-9 else float("inf")
            if delta > maximum_delta:
                maximum_delta = delta
                worst_delta = [first, second]
            if slope > maximum_slope:
                maximum_slope = slope
                worst_slope = [first, second]
        per_side[side] = {
            "maximumAdjacentWeightDelta": maximum_delta,
            "worstDeltaEdge": {
                "vertices": worst_delta,
                "coordinates": [list(obj.data.vertices[index].co) for index in worst_delta],
                "weights": [rows[index].get(name, 0.0) for index in worst_delta],
                "lengthMeters": (
                    obj.data.vertices[worst_delta[0]].co
                    - obj.data.vertices[worst_delta[1]].co
                ).length,
            },
            "maximumAdjacentWeightSlopePerMeter": maximum_slope,
            "worstSlopeEdge": {
                "vertices": worst_slope,
                "coordinates": [list(obj.data.vertices[index].co) for index in worst_slope],
                "weights": [rows[index].get(name, 0.0) for index in worst_slope],
                "lengthMeters": (
                    obj.data.vertices[worst_slope[0]].co
                    - obj.data.vertices[worst_slope[1]].co
                ).length,
            },
        }
    return {
        "status": "PASS",
        "perSide": per_side,
        "globalMaximumAdjacentWeightDelta": max(value["maximumAdjacentWeightDelta"] for value in per_side.values()),
        "globalMaximumAdjacentWeightSlopePerMeter": max(value["maximumAdjacentWeightSlopePerMeter"] for value in per_side.values()),
    }


def support_locality_report(obj: bpy.types.Object, rig: bpy.types.Object) -> dict[str, object]:
    rows = verifier.object_weight_dicts(obj)
    per_side = {}
    for side in ("Left", "Right"):
        upper = rig.data.bones[f"{side}UpperArm"]
        elbow = upper.tail_local.copy()
        axis = (upper.tail_local - upper.head_local).normalized()
        support_name = f"{side}ElbowSupport"
        indices = [index for index, row in enumerate(rows) if row.get(support_name, 0.0) > 1e-8]
        contract.require(indices, f"{support_name} has no scratch assignments")
        maximum_axial = 0.0
        maximum_radial = 0.0
        for index in indices:
            point = obj.data.vertices[index].co
            signed_axial = (point - elbow).dot(axis)
            radial = ((point - elbow) - (axis * signed_axial)).length
            maximum_axial = max(maximum_axial, abs(signed_axial))
            maximum_radial = max(maximum_radial, radial)
        per_side[side] = {
            "assignedVertexCount": len(indices),
            "totalWeightMass": sum(rows[index].get(support_name, 0.0) for index in indices),
            "maximumWeight": max(rows[index].get(support_name, 0.0) for index in indices),
            "maximumAbsoluteAxialMeters": maximum_axial,
            "maximumRadialMeters": maximum_radial,
        }
    return {"status": "PASS", "perSide": per_side}


def clone_control_body(
    corrected: bpy.types.Object,
    rig: bpy.types.Object,
    deform_groups: list[str],
    control_rows: list[list[tuple[str, float]]],
) -> bpy.types.Object:
    control = corrected.copy()
    control.data = corrected.data.copy()
    control.name = corrected.name + "_UncorrectedCenterControl"
    bpy.context.collection.objects.link(control)
    binder.assign_weight_rows(control, deform_groups, control_rows)
    control.parent = rig
    control.matrix_parent_inverse = rig.matrix_world.inverted()
    control.location = (0.0, 0.0, 0.0)
    control.rotation_euler = (0.0, 0.0, 0.0)
    control.scale = (1.0, 1.0, 1.0)
    return control


def deformation_delta_frame(
    corrected: bpy.types.Object,
    control: bpy.types.Object,
    centers: list[int],
) -> dict[str, object]:
    corrected_points = contract.evaluated_positions(corrected)
    control_points = contract.evaluated_positions(control)
    contract.require(len(corrected_points) == len(control_points), "Center control vertex count changed")
    center_set = set(centers)
    deltas = [(corrected_points[index] - control_points[index]).length for index in centers]
    worst_offset = max(range(len(centers)), key=lambda offset: deltas[offset])
    worst_index = centers[worst_offset]
    all_vertex_maximum = max(
        (corrected_point - control_point).length
        for corrected_point, control_point in zip(corrected_points, control_points)
    )
    non_center_maximum = max(
        (corrected_points[index] - control_points[index]).length
        for index in range(len(corrected_points))
        if index not in center_set
    )
    v815_delta = (corrected_points[815] - control_points[815]).length
    return {
        "maximumCenterDeltaMeters": deltas[worst_offset],
        "meanCenterDeltaMeters": sum(deltas) / len(deltas),
        "rmsCenterDeltaMeters": math.sqrt(sum(value * value for value in deltas) / len(deltas)),
        "worstCenterVertex": worst_index,
        "worstControlPosition": list(control_points[worst_index]),
        "worstCorrectedPosition": list(corrected_points[worst_index]),
        "vertex815DeltaMeters": v815_delta,
        "vertex815ControlPosition": list(control_points[815]),
        "vertex815CorrectedPosition": list(corrected_points[815]),
        "maximumAllVertexDeltaMeters": all_vertex_maximum,
        "maximumNonCenterDeltaMeters": non_center_maximum,
    }


def aggregate_delta_frames(frames: list[dict[str, object]]) -> dict[str, object]:
    worst = max(frames, key=lambda frame: frame["metrics"]["maximumCenterDeltaMeters"])
    worst_v815 = max(frames, key=lambda frame: frame["metrics"]["vertex815DeltaMeters"])
    return {
        "sampleCount": len(frames),
        "maximumCenterDeltaMeters": worst["metrics"]["maximumCenterDeltaMeters"],
        "worstSample": worst["sample"],
        "worstCenterVertex": worst["metrics"]["worstCenterVertex"],
        "maximumVertex815DeltaMeters": worst_v815["metrics"]["vertex815DeltaMeters"],
        "worstVertex815Sample": worst_v815["sample"],
        "maximumAllVertexDeltaMeters": max(frame["metrics"]["maximumAllVertexDeltaMeters"] for frame in frames),
        "maximumNonCenterDeltaMeters": max(frame["metrics"]["maximumNonCenterDeltaMeters"] for frame in frames),
    }


def baseline_center_deformation_delta_report(
    corrected: bpy.types.Object,
    control: bpy.types.Object,
    rig: bpy.types.Object,
    actions: dict[str, bpy.types.Action],
) -> dict[str, object]:
    _pairs, centers, mirror_source = verifier.canonical_mirror_indices()
    center_set = set(centers)
    contract.require(815 in center_set, "Expected audited center vertex 815 is no longer canonical center")
    static_frames = []
    if rig.animation_data is not None:
        rig.animation_data.action = None
    for spec in contract.STATIC_POSES:
        contract.reset_pose(rig)
        spec.configure(rig)
        contract.apply_elbow_support_pose(rig)
        bpy.context.view_layer.update()
        static_frames.append({
            "sample": spec.name,
            "metrics": deformation_delta_frame(corrected, control, centers),
        })
    contract.require(len(static_frames) == 22, "Center delta static pose count changed")

    animation_frames = []
    if rig.animation_data is None:
        rig.animation_data_create()
    for clip_name, (frame_start, frame_end, _loop) in contract.CLIPS.items():
        contract.bind_action_slot(rig, actions[clip_name], clip_name)
        contract.evaluated_action_motion_report(rig, clip_name)
        for frame in range(frame_start, frame_end + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            animation_frames.append({
                "sample": f"{clip_name}:{frame}",
                "clip": clip_name,
                "frame": frame,
                "metrics": deformation_delta_frame(corrected, control, centers),
            })
    contract.require(len(animation_frames) == 248, "Center delta animation frame count changed")
    rig.animation_data.action = None
    contract.reset_pose(rig)
    return {
        "status": "MEASURED_NO_TOLERANCE_INVENTED",
        "method": "same frozen RigV2 pose/action evaluated on ordinary-max4 baseline control versus pair-atomic corrected body; exact canonical self-row vertices are the center region",
        "mirrorSource": mirror_source,
        "centerVertexCount": len(centers),
        "static": {
            "aggregate": aggregate_delta_frames(static_frames),
            "samples": static_frames,
        },
        "animation": {
            "aggregate": aggregate_delta_frames(animation_frames),
            "samples": animation_frames,
        },
        "allSamples": aggregate_delta_frames(static_frames + animation_frames),
    }


def elbow_110_screen(obj: bpy.types.Object, rig: bpy.types.Object, deform_groups: list[str]) -> dict[str, object]:
    context = contract.rest_deformation_context(obj, rig, deform_groups)
    contract.reset_pose(rig)
    contract.pose_elbow_110(rig)
    contract.apply_elbow_support_pose(rig)
    bpy.context.view_layer.update()
    points = contract.evaluated_positions(obj)
    area_ratios = [
        contract.triangle_area(*(points[index] for index in triangle)) / rest_area
        for triangle, rest_area in zip(context["triangles"], context["restTriangleAreas"])
    ]
    edge_ratios = [
        (points[first] - points[second]).length / rest_length
        for (first, second), rest_length in zip(context["edges"], context["restEdgeLengths"])
    ]
    minimum_area = min(area_ratios)
    minimum_area_index = area_ratios.index(minimum_area)
    minimum_edge = min(edge_ratios)
    minimum_edge_index = edge_ratios.index(minimum_edge)
    maximum_edge = max(edge_ratios)
    maximum_edge_index = edge_ratios.index(maximum_edge)
    historic_triangles = {}
    tracked_triangle_indices = tuple(dict.fromkeys(
        contract.HISTORIC_ELBOW_TRIANGLE_INDICES
        + LATEST_V2_REJECT_TRIANGLE_INDICES
    ))
    for triangle_index in tracked_triangle_indices:
        historic_triangles[str(triangle_index)] = area_ratios[triangle_index]
    edge_to_ratio = {edge: ratio for edge, ratio in zip(context["edges"], edge_ratios)}
    historic_edges = {
        f"{first}-{second}": edge_to_ratio[(min(first, second), max(first, second))]
        for first, second in contract.HISTORIC_ELBOW_EDGES
    }
    contract.reset_pose(rig)
    return {
        "minimumTriangleAreaRatio": minimum_area,
        "minimumTriangleIndex": minimum_area_index,
        "minimumTriangleVertices": list(context["triangles"][minimum_area_index]),
        "minimumEdgeLengthRatio": minimum_edge,
        "minimumEdge": list(context["edges"][minimum_edge_index]),
        "maximumEdgeLengthRatio": maximum_edge,
        "maximumEdge": list(context["edges"][maximum_edge_index]),
        "historicTriangleAreaRatios": historic_triangles,
        "historicEdgeLengthRatios": historic_edges,
        "passesAreaAndEdgeScreen": (
            minimum_area >= contract.MIN_TRIANGLE_AREA_RATIO
            and minimum_edge >= contract.MIN_EDGE_LENGTH_RATIO
            and maximum_edge <= contract.MAX_EDGE_LENGTH_RATIO
        ),
        "screenLimits": {
            "minimumTriangleAreaRatio": contract.MIN_TRIANGLE_AREA_RATIO,
            "edgeLengthRatio": [contract.MIN_EDGE_LENGTH_RATIO, contract.MAX_EDGE_LENGTH_RATIO],
        },
    }


def aggregate_full_gate_metrics(static_report: dict[str, object], animation_report: dict[str, object]) -> dict[str, object]:
    static_poses = static_report["poses"].values()
    animation_clips = animation_report["clips"].values()
    return {
        "staticPoseCount": static_report["poseCount"],
        "animationFrameCount": animation_report["sampledFrameCount"],
        "minimumTriangleAreaRatio": min(
            [pose["minimumTriangleAreaRatio"] for pose in static_poses]
            + [clip["minimumTriangleAreaRatio"] for clip in animation_clips]
        ),
        "minimumEdgeLengthRatio": min(
            [pose["minimumEdgeLengthRatio"] for pose in static_report["poses"].values()]
            + [clip["minimumEdgeLengthRatio"] for clip in animation_report["clips"].values()]
        ),
        "maximumEdgeLengthRatio": max(
            [pose["maximumEdgeLengthRatio"] for pose in static_report["poses"].values()]
            + [clip["maximumEdgeLengthRatio"] for clip in animation_report["clips"].values()]
        ),
        "minimumCrossSectionRetention": min(
            [pose["minimumCrossSectionRetention"] for pose in static_report["poses"].values()]
            + [clip["minimumCrossSectionRetention"] for clip in animation_report["clips"].values()]
        ),
        "maximumPenetrationMeters": max(
            [pose["selfPenetration"]["maximumApproximateDepthMeters"] for pose in static_report["poses"].values()]
            + [clip["maximumPenetrationMeters"] for clip in animation_report["clips"].values()]
        ),
        "historicElbow110Regression": static_report["historicElbow110Regression"],
    }


def write_report(report: dict[str, object]) -> None:
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def selected_candidates() -> tuple[tuple[EnvelopeCandidate, ...], str]:
    script_arguments = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    if "--baseline-only" in script_arguments:
        return (CANDIDATES[0],), "BASELINE_ONLY_PREFLIGHT"
    contract.require(not script_arguments, f"Unknown scratch study arguments: {script_arguments}")
    return CANDIDATES, "FULL_ENVELOPE_SWEEP"


def assert_no_canonical_outputs(expected_binding_report_sha256: str) -> None:
    contract.require(not contract.BASE_OUTPUT_DIR.exists(), f"Scratch study refuses canonical V2 body outputs: {contract.BASE_OUTPUT_DIR}")
    contract.require(not contract.PROMOTION_TEMP_DIR.exists(), f"Scratch study refuses promotion temp: {contract.PROMOTION_TEMP_DIR}")
    contract.require(not contract.STAGED_PACKAGE_DIR.exists(), f"Scratch study refuses staged package: {contract.STAGED_PACKAGE_DIR}")
    staging_members = sorted(str(path.relative_to(contract.STAGING_ROOT)) for path in contract.STAGING_ROOT.rglob("*")) if contract.STAGING_ROOT.exists() else []
    contract.require(not staging_members, f"Scratch study refuses non-empty staging root: {staging_members}")
    contract.require(BASELINE_REJECT_REPORT.exists(), f"Baseline reject report disappeared: {BASELINE_REJECT_REPORT}")
    contract.require(
        contract.sha256(BASELINE_REJECT_REPORT) == expected_binding_report_sha256,
        "Scratch study changed the frozen baseline binding report",
    )


def run_candidate(candidate: EnvelopeCandidate, expected_binding_report_sha256: str) -> dict[str, object]:
    assert_no_canonical_outputs(expected_binding_report_sha256)
    original_apply = binder.apply_elbow_support_envelope
    patched = False
    phase = "PREPARE_FROZEN_INPUTS"
    source_support = None
    imported_support = None
    body_provenance = None
    variants = {}
    scratch_state = {
        "controlRows": {},
        "centerCorrections": {},
    }

    def scratch_apply(obj, target_rig, mapped_rows, mirror_pairs):
        envelope_report = apply_candidate_envelope(candidate, obj, target_rig, mapped_rows, mirror_pairs)
        center_report, control_rows = apply_center_atomic_max4(mapped_rows)
        scratch_state["controlRows"][obj.name] = control_rows
        scratch_state["centerCorrections"][obj.name] = center_report
        envelope_report["scratchCenterCorrection"] = center_report
        return envelope_report

    try:
        binder.reset_scene()
        rig_manifest, _animation_manifest, source_support, body_provenance = binder.verify_frozen_inputs()
        references = binder.append_checkpoint_references()
        rig, deform_groups, imported_support = binder.import_refitted_rig(rig_manifest)
        landmark_artifact = json.loads(binder.LANDMARK_ARTIFACT.read_text(encoding="utf-8"))
        binder.apply_elbow_support_envelope = scratch_apply
        patched = True
        bodies = {}
        for variant in contract.VARIANTS:
            phase = f"BUILD_VARIANT_{variant}"
            body, build_report = binder.build_variant(
                variant,
                references[variant],
                rig,
                deform_groups,
                landmark_artifact,
            )
            bodies[variant] = body
            variants[variant] = {"build": build_report}
            variants[variant]["centerCorrection"] = scratch_state["centerCorrections"][body.name]
            phase = f"WEIGHT_DIAGNOSTICS_{variant}"
            variants[variant]["supportEdgeGradient"] = support_edge_gradient_report(body)
            variants[variant]["supportLocality"] = support_locality_report(body, rig)
            variants[variant]["mirrorWeightField"] = verifier.mirror_weight_field_report(body)
            phase = f"ELBOW110_SCREEN_{variant}"
            variants[variant]["elbow110Screen"] = elbow_110_screen(body, rig, deform_groups)
        promising = all(variant["elbow110Screen"]["passesAreaAndEdgeScreen"] for variant in variants.values())
        full_status = "NOT_RUN_SCREEN_REJECT"
        animation_input = None
        if candidate.name == "frozen_piecewise_geodesic_baseline":
            phase = "BASELINE_CENTER_DELTA_IMPORT_ANIMATION"
            actions, animation_input = contract.import_animation_actions(rig)
            for variant, body in bodies.items():
                phase = f"BASELINE_CENTER_DELTA_{variant}"
                control = clone_control_body(
                    body,
                    rig,
                    deform_groups,
                    scratch_state["controlRows"][body.name],
                )
                variants[variant]["baselineCenterDeformationDelta"] = baseline_center_deformation_delta_report(
                    body,
                    control,
                    rig,
                    actions,
                )
            full_status = "BASELINE_CONTROL_MEASURED_SCREEN_REJECT"
        if promising:
            static_reports = {}
            for variant, body in bodies.items():
                phase = f"STATIC_DEFORMATION_GATES_{variant}"
                static_reports[variant] = contract.run_static_deformation_gates(body, rig, deform_groups)
                contract.require(
                    static_reports[variant]["poseCount"] == 22,
                    f"{variant} scratch static pose count changed",
                )
            phase = "IMPORT_FROZEN_ANIMATION"
            if animation_input is None:
                actions, animation_input = contract.import_animation_actions(rig)
            animation_reports = {}
            for variant, body in bodies.items():
                phase = f"ANIMATION_DEFORMATION_GATES_{variant}"
                animation_reports[variant] = contract.run_animation_deformation_gates(body, rig, deform_groups, actions)
                contract.require(
                    animation_reports[variant]["sampledFrameCount"] == 248,
                    f"{variant} scratch animation frame count changed",
                )
                variants[variant]["fullGateMetrics"] = aggregate_full_gate_metrics(
                    static_reports[variant],
                    animation_reports[variant],
                )
            full_status = "PASS_ALL_22_STATIC_AND_248_ANIMATION_FRAMES_BOTH_VARIANTS"
        return {
            "candidate": candidate.name,
            "coordinate": candidate.coordinate,
            "formula": candidate.formula,
            "analyticMaximumSlope": candidate.analytic_max_slope,
            "status": full_status,
            "screenPromising": promising,
            "sourceSupportContract": source_support,
            "importedSupportSemantics": imported_support,
            "bodyProvenance": body_provenance,
            "animationInput": animation_input,
            "variants": variants,
        }
    except BaseException as error:
        error_text = str(error)
        fatal_center_failure = "center self-pair" in error_text
        candidate_build_failure = (
            phase.startswith("BUILD_VARIANT_")
            and any(
                token in error_text.lower()
                for token in ("elbowsupport", "elbow-support", "envelope", "support assignment", "support weight")
            )
        )
        deformation_failure = phase.startswith("STATIC_DEFORMATION_GATES_") or phase.startswith("ANIMATION_DEFORMATION_GATES_")
        diagnostic_failure = phase.startswith("WEIGHT_DIAGNOSTICS_") and not fatal_center_failure
        return {
            "candidate": candidate.name,
            "coordinate": candidate.coordinate,
            "formula": candidate.formula,
            "analyticMaximumSlope": candidate.analytic_max_slope,
            "status": (
                "FATAL_CENTER_SELF_PAIR_WEIGHT_GATE"
                if fatal_center_failure
                else "REJECT_DEFORMATION_GATE"
                if deformation_failure
                else "REJECT_CANDIDATE_WEIGHT_GATE"
                if candidate_build_failure or diagnostic_failure
                else "ERROR_INFRASTRUCTURE_OR_SOURCE"
            ),
            "failurePhase": phase,
            "error": error_text,
            "traceback": traceback.format_exc(),
            "variants": variants,
        }
    finally:
        if patched:
            binder.apply_elbow_support_envelope = original_apply
        assert_no_canonical_outputs(expected_binding_report_sha256)


def main() -> None:
    contract.require(bpy.app.version >= (5, 2, 0), f"Blender 5.2+ required, got {bpy.app.version_string}")
    contract.require(BASELINE_REJECT_REPORT.exists(), f"Missing baseline reject report: {BASELINE_REJECT_REPORT}")
    expected_binding_report_sha256 = contract.sha256(BASELINE_REJECT_REPORT)
    assert_no_canonical_outputs(expected_binding_report_sha256)
    candidates, run_mode = selected_candidates()
    report = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV2ElbowEnvelopeScratchStudy",
        "status": "RUNNING",
        "unityIntegrationAllowed": False,
        "canonicalOutputsWritten": False,
        "supersedes": {
            "reportSha256": "919e3bf148f05d34af8e05bcaf975fd8dc6bd157029e4274e018ced77db6c82d",
            "reason": "The prior sweep used a malformed hand-tuned cross-body BowDraw static pose and Blender 5.2 actions without an explicitly bound OBJECT slot, so its BowDraw classification and 248-frame evidence are not authoritative.",
        },
        "bowDrawPoseSnapshot": {
            "path": str(contract.BOW_DRAW_POSE_SNAPSHOT.relative_to(contract.PROJECT_ROOT)),
            "sha256": contract.sha256(contract.BOW_DRAW_POSE_SNAPSHOT),
            "source": contract.bow_draw_pose_snapshot_contract()["source"],
        },
        "animationEvaluationContract": "Every imported clip must have exact 68-bone/689-curve coverage, bind its single Blender 5.2 OBJECT/OBSlot explicitly, and prove nonzero evaluated motion before frame gates.",
        "sourceHashes": {
            "study": contract.sha256(Path(__file__)),
            "binder": contract.sha256(Path(binder.__file__)),
            "bindingContract": contract.sha256(Path(contract.__file__)),
            "verifier": contract.sha256(Path(verifier.__file__)),
            "rigFbx": contract.sha256(contract.RIG_FBX),
            "rigManifest": contract.sha256(contract.RIG_MANIFEST),
            "animationFbx": contract.sha256(contract.ANIMATION_FBX),
            "animationManifest": contract.sha256(contract.ANIMATION_MANIFEST),
            "baselineRejectReport": contract.sha256(BASELINE_REJECT_REPORT),
        },
        "unchangedLimits": {
            "minimumTriangleAreaRatio": contract.MIN_TRIANGLE_AREA_RATIO,
            "edgeLengthRatio": [contract.MIN_EDGE_LENGTH_RATIO, contract.MAX_EDGE_LENGTH_RATIO],
            "minimumCrossSectionRetention": contract.MIN_CROSS_SECTION_RETENTION,
            "maximumPenetrationMeters": contract.MAX_PENETRATION_METERS,
            "maximumInfluences": contract.MAX_INFLUENCES,
        },
        "blenderVersion": bpy.app.version_string,
        "runMode": run_mode,
        "candidateCount": len(candidates),
        "candidates": [],
    }
    write_report(report)
    for candidate in candidates:
        result = run_candidate(candidate, expected_binding_report_sha256)
        if candidate.name == "frozen_piecewise_geodesic_baseline":
            if result["status"] == "FATAL_CENTER_SELF_PAIR_WEIGHT_GATE":
                result["baselineReproduction"] = {
                    "status": "NOT_REACHED_FATAL_CENTER_SELF_PAIR_WEIGHT_GATE",
                    "triangleIndex": 19148,
                    "expectedAreaRatio": KNOWN_BASELINE_MASCULINE_TRIANGLE_19148_AREA_RATIO,
                }
            else:
                try:
                    observed = result["variants"]["Masculine"]["elbow110Screen"]["historicTriangleAreaRatios"]["19148"]
                    delta = abs(observed - KNOWN_BASELINE_MASCULINE_TRIANGLE_19148_AREA_RATIO)
                    result["baselineReproduction"] = {
                        "status": "PASS" if delta <= 1e-12 else "FAIL",
                        "triangleIndex": 19148,
                        "expectedAreaRatio": KNOWN_BASELINE_MASCULINE_TRIANGLE_19148_AREA_RATIO,
                        "observedAreaRatio": observed,
                        "absoluteDelta": delta,
                        "tolerance": 1e-12,
                    }
                    if delta > 1e-12:
                        result["status"] = "ERROR_BASELINE_REPRODUCTION"
                except (KeyError, TypeError) as error:
                    result["baselineReproduction"] = {
                        "status": "FAIL",
                        "error": f"Baseline diagnostic missing: {error}",
                    }
                    result["status"] = "ERROR_BASELINE_REPRODUCTION"
        report["candidates"].append(result)
        write_report(report)
        if result["status"] in {
            "FATAL_CENTER_SELF_PAIR_WEIGHT_GATE",
            "ERROR_BASELINE_REPRODUCTION",
            "ERROR_INFRASTRUCTURE_OR_SOURCE",
        }:
            break
    passing = [item["candidate"] for item in report["candidates"] if item["status"].startswith("PASS_ALL")]
    fatal_center_failure = any(
        item["status"] == "FATAL_CENTER_SELF_PAIR_WEIGHT_GATE"
        for item in report["candidates"]
    )
    infrastructure_failure = any(
        item["status"] in {"ERROR_BASELINE_REPRODUCTION", "ERROR_INFRASTRUCTURE_OR_SOURCE"}
        for item in report["candidates"]
    )
    shared_bowdraw_wrist_blockers = [
        item["candidate"]
        for item in report["candidates"]
        if item.get("status") == "REJECT_DEFORMATION_GATE"
        and item.get("failurePhase") == "STATIC_DEFORMATION_GATES_Masculine"
        and "Static pose BowDraw failed" in item.get("error", "")
        and "triangle 7246" in item.get("error", "")
    ]
    baseline_ready = (
        run_mode == "BASELINE_ONLY_PREFLIGHT"
        and len(report["candidates"]) == 1
        and report["candidates"][0]["status"] == "BASELINE_CONTROL_MEASURED_SCREEN_REJECT"
        and report["candidates"][0].get("baselineReproduction", {}).get("status") == "PASS"
    )
    if infrastructure_failure:
        report["status"] = "ERROR_STUDY_INFRASTRUCTURE_OR_BASELINE_REPRODUCTION"
    elif fatal_center_failure:
        report["status"] = "REJECT_CENTER_SELF_PAIR_WEIGHT_GATE_STOPPED_STUDY"
    elif baseline_ready:
        report["status"] = "PASS_BASELINE_CENTER_CORRECTION_PREFLIGHT_READY_FOR_SWEEP"
    elif passing:
        report["status"] = "PASS_SCRATCH_CANDIDATES_REQUIRE_SOURCE_REVIEW_NO_PROMOTION"
    elif len(shared_bowdraw_wrist_blockers) >= 2:
        report["status"] = "REJECT_SHARED_BOWDRAW_WRIST_POSE_GATE_ENVELOPE_FULL_SUITE_INCONCLUSIVE"
    else:
        report["status"] = "REJECT_NO_ENVELOPE_PASSES_UNCHANGED_FULL_SUITE"
    report["passingCandidates"] = passing
    report["sharedBowDrawWristBlockingCandidates"] = shared_bowdraw_wrist_blockers
    report["canonicalOutputsWritten"] = contract.BASE_OUTPUT_DIR.exists()
    assert_no_canonical_outputs(expected_binding_report_sha256)
    write_report(report)
    print(json.dumps({
        "status": report["status"],
        "report": str(REPORT_PATH),
        "reportSha256": contract.sha256(REPORT_PATH),
        "passingCandidates": passing,
    }, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
