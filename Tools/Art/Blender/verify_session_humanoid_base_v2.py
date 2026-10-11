#!/usr/bin/env python3
"""Independent SessionHumanoidBaseV2 staged-FBX verifier and promoter.

Run this in a fresh Blender 5.2 process only after the V2 builder has completed.
It does not trust builder-side in-memory objects: it opens the staged Blend,
imports both staged FBXs, repeats structural/deformation checks, compares every
static pose and animation frame, then atomically promotes a complete DCC package.
Unity Assets are never read or written.
"""

from __future__ import annotations

import json
import math
import os
from pathlib import Path
import shutil
import sys
import traceback

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import session_humanoid_base_v2_binding_contract as contract


def verify_staged_package_membership_and_hashes(
    staged_manifest: dict[str, object],
    package_dir: Path,
) -> dict[str, object]:
    expected_hashes = {
        contract.STAGED_BLEND.name: staged_manifest["stagedOutputs"]["boundBlend"]["sha256"],
        **{
            path.name: staged_manifest["stagedOutputs"]["fbx"][variant]["sha256"]
            for variant, path in contract.STAGED_FBX_PATHS.items()
        },
    }
    contract.require(package_dir.is_dir(), f"Staged package directory is missing: {package_dir}")
    entries = list(package_dir.iterdir())
    contract.require(all(entry.is_file() for entry in entries), f"Staged package contains a non-file entry: {entries}")
    actual_names = {entry.name for entry in entries}
    contract.require(actual_names == set(expected_hashes), f"Staged package membership changed: {sorted(actual_names)}")
    actual_hashes = {}
    for name, expected_hash in expected_hashes.items():
        actual_hash = contract.sha256(package_dir / name)
        contract.require(actual_hash == expected_hash, f"Staged package file changed: {name}")
        actual_hashes[name] = actual_hash
    return {
        "status": "PASS",
        "packageDirectory": str(package_dir),
        "exactFilenames": sorted(actual_names),
        "sha256": actual_hashes,
    }


def require_frozen_inputs() -> tuple[dict[str, object], dict[str, object], dict[str, object], dict[str, object]]:
    contract.require(
        all((
            contract.EXPECTED_RIG_FBX_SHA256,
            contract.EXPECTED_RIG_MANIFEST_SHA256,
            contract.EXPECTED_ANIMATION_FBX_SHA256,
            contract.EXPECTED_ANIMATION_MANIFEST_SHA256,
        )),
        "V2 rig/animation hashes are not frozen",
    )
    contract.require(contract.sha256(contract.RIG_FBX) == contract.EXPECTED_RIG_FBX_SHA256, "Frozen V2 rig FBX changed")
    contract.require(contract.sha256(contract.RIG_MANIFEST) == contract.EXPECTED_RIG_MANIFEST_SHA256, "Frozen V2 rig manifest changed")
    contract.require(contract.sha256(contract.ANIMATION_FBX) == contract.EXPECTED_ANIMATION_FBX_SHA256, "Frozen V2 animation FBX changed")
    contract.require(contract.sha256(contract.ANIMATION_MANIFEST) == contract.EXPECTED_ANIMATION_MANIFEST_SHA256, "Frozen V2 animation manifest changed")
    contract.bow_draw_pose_snapshot_contract()
    contract.require(contract.STAGED_BUILD_MANIFEST.is_file(), "Staged V2 build manifest is missing")
    contract.require(contract.STAGED_BLEND.is_file(), "Staged V2 Blend is missing")
    contract.require(not contract.BASE_OUTPUT_DIR.exists(), f"Canonical V2 outputs already exist: {contract.BASE_OUTPUT_DIR}")
    contract.require(not contract.PROMOTION_TEMP_DIR.exists(), f"Incomplete promotion directory exists: {contract.PROMOTION_TEMP_DIR}")
    staged_manifest = json.loads(contract.STAGED_BUILD_MANIFEST.read_text(encoding="utf-8"))
    contract.require(staged_manifest["status"] == "PASS_V2_DCC_AND_DEFORMATION_GATES_STAGED_PENDING_INDEPENDENT_FBX_ROUND_TRIP", "Staged builder status is not promotable")
    contract.require(not staged_manifest["canonicalOutputsWritten"], "Staged manifest incorrectly claims canonical output")
    contract.require(contract.BINDING_REPORT.is_file(), "Staged builder report is missing")
    contract.require(
        contract.sha256(contract.BINDING_REPORT) == staged_manifest["bindingReport"]["sha256"],
        "Staged builder report hash differs from staged manifest",
    )
    staged_report = json.loads(contract.BINDING_REPORT.read_text(encoding="utf-8"))
    contract.require(staged_report["status"] == staged_manifest["status"], "Staged builder report status differs from manifest")
    staged_package = verify_staged_package_membership_and_hashes(staged_manifest, contract.STAGED_PACKAGE_DIR)
    for variant, path in contract.STAGED_FBX_PATHS.items():
        contract.require(path.is_file(), f"Staged {variant} FBX is missing")
        expected = staged_manifest["stagedOutputs"]["fbx"][variant]["sha256"]
        contract.require(contract.sha256(path) == expected, f"Staged {variant} FBX hash differs from staged manifest")
        envelope = staged_manifest["variants"][variant]["weightCorrection"]["elbowSupportEnvelope"]["supportEnvelope"]
        contract.require(envelope == contract.ELBOW_SUPPORT_ENVELOPE_CONTRACT, f"Staged {variant} support envelope differs from the V2 contract")
    rig_manifest = json.loads(contract.RIG_MANIFEST.read_text(encoding="utf-8"))
    animation_manifest = json.loads(contract.ANIMATION_MANIFEST.read_text(encoding="utf-8"))
    compatibility = rig_manifest["v1Compatibility"]
    contract.require(compatibility["first66BoneRecordsUnchanged"] and compatibility["first66BoneRecordsOrderUnchanged"], "V2 no longer preserves frozen V1 records")
    contract.require(compatibility["first66OrderedRecordsSerializedSha256"] == contract.EXPECTED_V1_FIRST66_ORDERED_RECORDS_SHA256, "V2 first-66 record hash changed")
    source_hashes = staged_manifest["sourceHashes"]
    contract.require(source_hashes["bindingContract"] == contract.sha256(Path(contract.__file__)), "V2 binding contract changed after staging")
    contract.require(source_hashes["builder"] == contract.sha256(contract.V2_BINDING_BUILDER), "V2 binding builder changed after staging")
    contract.require(source_hashes["rigFbx"] == contract.EXPECTED_RIG_FBX_SHA256, "Staged build pins another V2 rig")
    contract.require(source_hashes["rigManifest"] == contract.EXPECTED_RIG_MANIFEST_SHA256, "Staged build pins another V2 rig manifest")
    contract.require(source_hashes["animationFbx"] == contract.EXPECTED_ANIMATION_FBX_SHA256, "Staged build pins another V2 animation")
    contract.require(source_hashes["animationManifest"] == contract.EXPECTED_ANIMATION_MANIFEST_SHA256, "Staged build pins another V2 animation manifest")
    contract.require(source_hashes["bowDrawPoseSnapshot"] == contract.EXPECTED_BOW_DRAW_POSE_SNAPSHOT_SHA256, "Staged build pins another BowDraw pose snapshot")
    contract.require(source_hashes["bodyLandmarkArtifact"] == contract.EXPECTED_LANDMARK_ARTIFACT_SHA256, "Staged build pins another V1 body landmark source")
    contract.require(source_hashes["acceptedBodyCheckpoint"] == contract.EXPECTED_V1_CHECKPOINT_SHA256, "Staged build pins another V1 checkpoint")
    contract.require(source_hashes["bodyBuildManifest"] == contract.EXPECTED_V1_BODY_BUILD_MANIFEST_SHA256, "Staged build pins another V1 build manifest")
    contract.require(source_hashes["bodyIntakeReport"] == contract.EXPECTED_V1_BODY_INTAKE_REPORT_SHA256, "Staged build pins another V1 intake report")
    contract.require(source_hashes["intakeBuilder"] == contract.EXPECTED_V1_BODY_INTAKE_BUILDER_SHA256, "Staged build pins another V1 intake builder")
    body_provenance = contract.v1_body_provenance_report(rig_manifest, contract.V1_RIG_FIT_LANDMARKS)
    body_provenance["stagedPackagePreVerification"] = staged_package
    return staged_manifest, rig_manifest, animation_manifest, body_provenance


def evaluated_world_positions(obj: bpy.types.Object) -> list[Vector]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = obj.evaluated_get(depsgraph)
    return [evaluated.matrix_world @ vertex.co for vertex in evaluated.data.vertices]


def maximum_vertex_delta(first: bpy.types.Object, second: bpy.types.Object) -> tuple[float, int]:
    first_points = evaluated_world_positions(first)
    second_points = evaluated_world_positions(second)
    contract.require(len(first_points) == len(second_points) == contract.EXPECTED_VERTICES, "Round-trip vertex count changed")
    deltas = [(left - right).length for left, right in zip(first_points, second_points)]
    maximum = max(deltas)
    return maximum, deltas.index(maximum)


def object_weight_dicts(obj: bpy.types.Object) -> list[dict[str, float]]:
    names = {group.index: group.name for group in obj.vertex_groups}
    return [
        {names[item.group]: float(item.weight) for item in vertex.groups if item.weight > 1e-8}
        for vertex in obj.data.vertices
    ]


def mirrored_group_name(name: str) -> str:
    if name.startswith("Left"):
        return "Right" + name[len("Left"):]
    if name.startswith("Right"):
        return "Left" + name[len("Right"):]
    return name


def canonical_mirror_indices() -> tuple[list[tuple[int, int]], list[int], dict[str, object]]:
    intake_report = json.loads(contract.V1_BODY_INTAKE_REPORT.read_text(encoding="utf-8"))
    base_obj = Path(intake_report["mpfb"]["bundledBaseObj"])
    contract.require(base_obj.is_file(), f"Pinned MPFB base OBJ is missing: {base_obj}")
    base_hash = contract.sha256(base_obj)
    contract.require(base_hash == contract.EXPECTED_MPFb_BASE_OBJ_SHA256, "Pinned MPFB base OBJ changed")
    positions = []
    with base_obj.open("r", encoding="utf-8") as stream:
        for line in stream:
            if line.startswith("v "):
                positions.append(tuple(float(value) for value in line.split()[1:4]))
                if len(positions) == contract.EXPECTED_VERTICES:
                    break
    contract.require(len(positions) == contract.EXPECTED_VERTICES, "Pinned MPFB retained vertex range changed")
    lookup = {
        (round(x, 9), round(y, 9), round(z, 9)): index
        for index, (x, y, z) in enumerate(positions)
    }
    contract.require(len(lookup) == contract.EXPECTED_VERTICES, "Pinned MPFB retained base vertices are not unique")
    pairs = []
    centers = []
    for index, (x, y, z) in enumerate(positions):
        if abs(x) <= 1e-9:
            centers.append(index)
            continue
        if x < 0.0:
            continue
        opposite = lookup.get((round(-x, 9), round(y, 9), round(z, 9)))
        contract.require(opposite is not None, f"Pinned MPFB base vertex {index} has no exact mirror")
        pairs.append((index, int(opposite)))
    contract.require(pairs and centers, "Pinned MPFB mirror map is incomplete")
    contract.require((len(pairs) * 2) + len(centers) == contract.EXPECTED_VERTICES, "Pinned MPFB mirror map does not cover every retained vertex")
    return pairs, centers, {
        "baseObj": str(base_obj),
        "baseObjSha256": base_hash,
        "sidePairCount": len(pairs),
        "centerSelfPairCount": len(centers),
    }


def mirror_weight_field_report(obj: bpy.types.Object) -> dict[str, object]:
    rows = object_weight_dicts(obj)
    pairs, centers, source = canonical_mirror_indices()
    maximum_pair_l1 = 0.0
    worst_pair = None
    for positive, negative in pairs:
        negative_as_positive = {
            mirrored_group_name(name): weight
            for name, weight in rows[negative].items()
        }
        names = set(rows[positive]) | set(negative_as_positive)
        l1 = sum(abs(rows[positive].get(name, 0.0) - negative_as_positive.get(name, 0.0)) for name in names)
        if l1 > maximum_pair_l1:
            maximum_pair_l1 = l1
            worst_pair = {
                "positiveVertex": positive,
                "negativeVertex": negative,
                "positiveCoordinate": list(obj.data.vertices[positive].co),
                "negativeCoordinate": list(obj.data.vertices[negative].co),
                "positiveRow": sorted(rows[positive].items()),
                "negativeAsPositiveRow": sorted(negative_as_positive.items()),
            }
    maximum_center_l1 = 0.0
    worst_center = None
    for index in centers:
        mirrored = {mirrored_group_name(name): weight for name, weight in rows[index].items()}
        names = set(rows[index]) | set(mirrored)
        l1 = sum(abs(rows[index].get(name, 0.0) - mirrored.get(name, 0.0)) for name in names)
        if l1 > maximum_center_l1:
            maximum_center_l1 = l1
            worst_center = {
                "vertex": index,
                "coordinate": list(obj.data.vertices[index].co),
                "row": sorted(rows[index].items()),
                "mirroredRow": sorted(mirrored.items()),
            }
    tolerance = 1e-12
    contract.require(maximum_pair_l1 <= tolerance, f"{obj.name} mirrored pair L1 {maximum_pair_l1}; worst {worst_pair}")
    contract.require(maximum_center_l1 <= tolerance, f"{obj.name} center self-pair L1 {maximum_center_l1}; worst {worst_center}")
    support_stats = {}
    for side in ("Left", "Right"):
        name = f"{side}ElbowSupport"
        values = [row.get(name, 0.0) for row in rows]
        positive = [value for value in values if value > 1e-8]
        support_stats[side] = {
            "assignedVertexCount": len(positive),
            "totalWeightMass": sum(positive),
            "maximumWeight": max(positive, default=0.0),
        }
    contract.require(support_stats["Left"]["assignedVertexCount"] == support_stats["Right"]["assignedVertexCount"], f"{obj.name} support assignment counts differ")
    support_mass_delta = abs(support_stats["Left"]["totalWeightMass"] - support_stats["Right"]["totalWeightMass"])
    contract.require(support_mass_delta <= tolerance, f"{obj.name} support weight mass differs by {support_mass_delta}")
    return {
        "status": "PASS",
        "toleranceL1": tolerance,
        "maximumSidePairL1": maximum_pair_l1,
        "worstSidePair": worst_pair,
        "maximumCenterSelfPairL1": maximum_center_l1,
        "worstCenterSelfPair": worst_center,
        "supportStats": support_stats,
        "supportWeightMassDelta": support_mass_delta,
        "mirrorSource": source,
    }


def object_transform_report(obj: bpy.types.Object) -> dict[str, object]:
    translation = obj.location.length
    rotation = max(abs(value) for value in obj.rotation_euler)
    scale_error = max(abs(value - 1.0) for value in obj.scale)
    determinant = obj.matrix_world.to_3x3().determinant()
    contract.require(translation <= 1e-5, f"{obj.name} object translation is {translation}m")
    contract.require(rotation <= 1e-5, f"{obj.name} object rotation is {rotation} radians")
    contract.require(scale_error <= 1e-5, f"{obj.name} object scale error is {scale_error}")
    contract.require(determinant > 0.0, f"{obj.name} object determinant is not positive")
    return {
        "status": "PASS",
        "location": list(obj.location),
        "rotationEulerRadians": list(obj.rotation_euler),
        "scale": list(obj.scale),
        "matrixWorldDeterminant": determinant,
    }


def mesh_structure_round_trip_report(
    source: bpy.types.Object,
    imported: bpy.types.Object,
    staged_variant_manifest: dict[str, object],
) -> dict[str, object]:
    source_triangles = len(contract.triangle_indices(source.data))
    imported_triangles = len(contract.triangle_indices(imported.data))
    contract.require(len(source.data.vertices) == len(imported.data.vertices) == contract.EXPECTED_VERTICES, "Bound body vertex count changed")
    contract.require(source_triangles == imported_triangles == contract.EXPECTED_TRIANGLES, "Bound body triangle count changed")
    contract.require(len(source.data.uv_layers) == len(imported.data.uv_layers) == 1, "Bound body must have exactly one UV layer")
    contract.require(source.data.uv_layers.active.name == imported.data.uv_layers.active.name == "UV0", "Bound body UV layer must be UV0")
    source_topology = contract.topology_sha256(source)
    imported_topology = contract.topology_sha256(imported)
    source_uv = contract.uv_loop_sha256(source)
    imported_uv = contract.uv_loop_sha256(imported)
    contract.require(source_topology == staged_variant_manifest["topologySha256"], "Staged Blend topology differs from staged manifest")
    contract.require(source_uv == staged_variant_manifest["uv0LoopSha256"], "Staged Blend UV0 differs from staged manifest")
    contract.require(imported_topology == source_topology, "FBX topology/order differs from staged Blend")
    contract.require(imported_uv == source_uv, "FBX UV0 loop data differs from staged Blend")
    return {
        "status": "PASS",
        "vertices": contract.EXPECTED_VERTICES,
        "triangles": contract.EXPECTED_TRIANGLES,
        "topologySha256": source_topology,
        "uv0LoopSha256": source_uv,
        "sourceTransform": object_transform_report(source),
        "importedTransform": object_transform_report(imported),
    }


def compare_weight_fields(source: bpy.types.Object, imported: bpy.types.Object) -> dict[str, object]:
    source_rows = object_weight_dicts(source)
    imported_rows = object_weight_dicts(imported)
    maximum_l1 = 0.0
    worst_vertex = 0
    for index, (source_row, imported_row) in enumerate(zip(source_rows, imported_rows)):
        names = set(source_row) | set(imported_row)
        l1 = sum(abs(source_row.get(name, 0.0) - imported_row.get(name, 0.0)) for name in names)
        if l1 > maximum_l1:
            maximum_l1 = l1
            worst_vertex = index
    contract.require(maximum_l1 <= 0.001, f"FBX weight-field L1 delta {maximum_l1} at vertex {worst_vertex}")
    support = {}
    for name in contract.ELBOW_SUPPORT_GROUPS:
        weights = [row.get(name, 0.0) for row in imported_rows]
        positive = [weight for weight in weights if weight > 1e-8]
        contract.require(positive and max(positive) >= 0.95, f"FBX lost {name} support envelope")
        support[name] = {
            "assignedVertexCount": len(positive),
            "maximumWeight": max(positive),
            "totalWeightMass": sum(positive),
        }
    return {
        "status": "PASS",
        "maximumPerVertexL1Delta": maximum_l1,
        "worstVertex": worst_vertex,
        "limit": 0.001,
        "supportWeights": support,
    }


def support_weight_contract_report(
    obj: bpy.types.Object,
    rig: bpy.types.Object,
) -> dict[str, object]:
    """Independently verify the observable portion of the support envelope.

    Four-weight pruning can remove a very small residual UpperArm or LowerArm
    influence near either fade boundary.  Rows which retain all three arm-chain
    influences remain algebraically observable: LowerArm / (UpperArm + LowerArm)
    recovers the geodesic coordinate and Support / arm-total must equal the
    frozen piecewise envelope.  Rigid-core ownership and spatial locality are
    checked for every row, including rows where pruning makes that coordinate
    unobservable.
    """
    rows = object_weight_dicts(obj)
    per_side = {}
    maximum_formula_delta = 0.0
    formula_tolerance = 0.002
    spatial_slack = 0.010
    for side in ("Left", "Right"):
        upper_name = f"{side}UpperArm"
        lower_name = f"{side}LowerArm"
        support_name = f"{side}ElbowSupport"
        upper_bone = rig.data.bones[upper_name]
        elbow = upper_bone.tail_local.copy()
        arm_axis = (upper_bone.tail_local - upper_bone.head_local).normalized()
        positive_indices = [
            index for index, row in enumerate(rows)
            if row.get(support_name, 0.0) > 1e-8
        ]
        contract.require(positive_indices, f"{obj.name} has no {support_name} weights")
        observable = []
        maximum_axial = 0.0
        maximum_radial = 0.0
        maximum_support = 0.0
        for index in positive_indices:
            row = rows[index]
            upper = row.get(upper_name, 0.0)
            lower = row.get(lower_name, 0.0)
            support = row[support_name]
            arm_total = upper + lower + support
            contract.require(arm_total > 1e-8, f"{support_name} row {index} has no arm-chain mass")
            maximum_support = max(maximum_support, support)
            point = obj.data.vertices[index].co
            axial = abs((point - elbow).dot(arm_axis))
            radial_vector = (point - elbow) - (arm_axis * ((point - elbow).dot(arm_axis)))
            radial = radial_vector.length
            maximum_axial = max(maximum_axial, axial)
            maximum_radial = max(maximum_radial, radial)
            contract.require(
                axial <= 0.160 + spatial_slack and radial <= 0.150 + spatial_slack,
                f"{support_name} weight escaped the elbow envelope at vertex {index}: "
                f"axial {axial}m, radial {radial}m",
            )
            if upper <= 1e-5 or lower <= 1e-5:
                continue
            geodesic_fraction = lower / (upper + lower)
            actual_fraction = support / arm_total
            expected_fraction = contract.elbow_support_envelope(geodesic_fraction)
            delta = abs(actual_fraction - expected_fraction)
            maximum_formula_delta = max(maximum_formula_delta, delta)
            observable.append({
                "vertex": index,
                "geodesicFraction": geodesic_fraction,
                "actualSupportFractionOfArmChain": actual_fraction,
                "expectedSupportFractionOfArmChain": expected_fraction,
                "absoluteDelta": delta,
            })
        contract.require(
            observable,
            f"{obj.name} has no independently observable {support_name} fade row after four-weight pruning",
        )
        side_maximum_delta = max(sample["absoluteDelta"] for sample in observable)
        contract.require(
            side_maximum_delta <= formula_tolerance,
            f"{obj.name} {support_name} observable envelope delta {side_maximum_delta} "
            f"exceeds {formula_tolerance}",
        )
        contract.require(maximum_support >= 0.95, f"{obj.name} {support_name} has no rigid-core ownership")
        worst = max(observable, key=lambda sample: sample["absoluteDelta"])
        per_side[side] = {
            "assignedVertexCount": len(positive_indices),
            "observableFadeRowCount": len(observable),
            "maximumSupportWeight": maximum_support,
            "maximumAxialDistanceMeters": maximum_axial,
            "maximumRadialDistanceMeters": maximum_radial,
            "maximumFormulaDelta": side_maximum_delta,
            "worstObservableRow": worst,
        }
    return {
        "status": "PASS",
        "formula": contract.ELBOW_SUPPORT_ENVELOPE_CONTRACT,
        "formulaTolerance": formula_tolerance,
        "spatialSelectionMeters": {
            "axial": 0.160,
            "radial": 0.150,
            "verificationSlack": spatial_slack,
        },
        "maximumFormulaDelta": maximum_formula_delta,
        "perSide": per_side,
        "method": "recover t from retained UpperArm/LowerArm fade rows, then compare Support/arm-total to the frozen piecewise h(t); also verify all support rows remain local and each side retains a rigid core",
    }


def matrix_rotation_error_degrees(first: Matrix, second: Matrix) -> float:
    return contract.quaternion_error_degrees(first.to_quaternion(), second.to_quaternion())


def hierarchy_round_trip_report(
    source_rig: bpy.types.Object,
    imported_rig: bpy.types.Object,
    rig_manifest: dict[str, object],
) -> dict[str, object]:
    ordered = [entry["name"] for entry in rig_manifest["hierarchy"]["bones"]]
    contract.require(len(ordered) == contract.EXPECTED_RIG_JOINTS, "Manifest V2 joint count changed")
    contract.require([bone.name for bone in source_rig.data.bones] == ordered, "Staged Blend hierarchy order differs from V2 manifest")
    contract.require([bone.name for bone in imported_rig.data.bones] == ordered, "FBX hierarchy order differs from V2 manifest")
    maximum_translation = 0.0
    maximum_rotation = 0.0
    maximum_manifest_translation = 0.0
    maximum_manifest_rotation = 0.0
    worst_translation = None
    worst_rotation = None
    manifest_by_name = {entry["name"]: entry for entry in rig_manifest["hierarchy"]["bones"]}
    for name in ordered:
        source = source_rig.data.bones[name]
        imported = imported_rig.data.bones[name]
        manifest_entry = manifest_by_name[name]
        source_parent = source.parent.name if source.parent else None
        imported_parent = imported.parent.name if imported.parent else None
        contract.require(source_parent == imported_parent, f"FBX parent changed for {name}")
        contract.require(source_parent == manifest_entry["parent"], f"Staged Blend parent differs from manifest for {name}")
        contract.require(source.use_deform == imported.use_deform, f"FBX deform flag changed for {name}")
        contract.require(source.use_deform == bool(manifest_entry["deform"]), f"Staged Blend deform flag differs from manifest for {name}")
        manifest_matrix = Matrix(manifest_entry["restMatrix"])
        manifest_translation = (source.matrix_local.translation - manifest_matrix.translation).length
        manifest_rotation = matrix_rotation_error_degrees(source.matrix_local, manifest_matrix)
        maximum_manifest_translation = max(maximum_manifest_translation, manifest_translation)
        maximum_manifest_rotation = max(maximum_manifest_rotation, manifest_rotation)
        translation = (source.matrix_local.translation - imported.matrix_local.translation).length
        rotation = matrix_rotation_error_degrees(source.matrix_local, imported.matrix_local)
        if translation > maximum_translation:
            maximum_translation = translation
            worst_translation = name
        if rotation > maximum_rotation:
            maximum_rotation = rotation
            worst_rotation = name
    contract.require(maximum_translation <= contract.ROUNDTRIP_BONE_TRANSLATION_TOLERANCE_METERS, f"FBX bone translation delta {maximum_translation}m")
    contract.require(maximum_rotation <= contract.ROUNDTRIP_BONE_ROTATION_TOLERANCE_DEGREES, f"FBX bone rotation delta {maximum_rotation} degrees")
    contract.require(maximum_manifest_translation <= contract.ROUNDTRIP_BONE_TRANSLATION_TOLERANCE_METERS, f"Staged Blend/manifest bone translation delta {maximum_manifest_translation}m")
    contract.require(maximum_manifest_rotation <= contract.ROUNDTRIP_BONE_ROTATION_TOLERANCE_DEGREES, f"Staged Blend/manifest bone rotation delta {maximum_manifest_rotation} degrees")
    source_support = contract.elbow_support_rest_report(
        source_rig,
        ordered,
        imported_fbx_semantics=True,
    )
    imported_support = contract.elbow_support_rest_report(
        imported_rig,
        ordered,
        imported_fbx_semantics=True,
    )
    return {
        "status": "PASS",
        "jointCount": len(ordered),
        "orderedNames": ordered,
        "maximumTranslationDeltaMeters": maximum_translation,
        "worstTranslationBone": worst_translation,
        "maximumRotationDeltaDegrees": maximum_rotation,
        "worstRotationBone": worst_rotation,
        "maximumManifestTranslationDeltaMeters": maximum_manifest_translation,
        "maximumManifestRotationDeltaDegrees": maximum_manifest_rotation,
        "sourceSupport": source_support,
        "importedSupport": imported_support,
    }


def import_bound_fbx(path: Path) -> tuple[bpy.types.Object, bpy.types.Object, list[bpy.types.Object]]:
    before_objects = set(bpy.data.objects)
    before_actions = set(bpy.data.actions)
    bpy.ops.import_scene.fbx(
        filepath=str(path),
        use_anim=False,
        automatic_bone_orientation=False,
        ignore_leaf_bones=False,
    )
    imported = [obj for obj in bpy.data.objects if obj not in before_objects]
    armatures = [obj for obj in imported if obj.type == "ARMATURE"]
    meshes = [obj for obj in imported if obj.type == "MESH"]
    contract.require(len(armatures) == 1 and len(meshes) == 1, f"Bound FBX imported {len(armatures)} rigs/{len(meshes)} meshes")
    contract.require(not [obj for obj in imported if obj.type in {"CAMERA", "LIGHT"}], "Bound FBX contains camera/light objects")
    contract.require(set(bpy.data.actions) == before_actions, "Static bound FBX unexpectedly contains actions")
    modifiers = [modifier for modifier in meshes[0].modifiers if modifier.type == "ARMATURE"]
    contract.require(len(modifiers) == 1 and modifiers[0].object == armatures[0], "Bound FBX armature modifier contract changed")
    return armatures[0], meshes[0], imported


def compare_static_poses(
    source_body: bpy.types.Object,
    source_rig: bpy.types.Object,
    imported_body: bpy.types.Object,
    imported_rig: bpy.types.Object,
) -> dict[str, object]:
    reports = {}
    maximum = 0.0
    worst = None
    for spec in contract.STATIC_POSES:
        for rig in (source_rig, imported_rig):
            contract.reset_pose(rig)
            spec.configure(rig)
            contract.apply_elbow_support_pose(rig)
        bpy.context.view_layer.update()
        source_support = contract.elbow_support_pose_report(source_rig)
        imported_support = contract.elbow_support_pose_report(imported_rig)
        delta, vertex = maximum_vertex_delta(source_body, imported_body)
        contract.require(delta <= contract.ROUNDTRIP_VERTEX_TOLERANCE_METERS, f"{spec.name} FBX vertex delta {delta}m at {vertex}")
        if delta > maximum:
            maximum = delta
            worst = {"pose": spec.name, "vertex": vertex}
        reports[spec.name] = {
            "maximumVertexDeltaMeters": delta,
            "worstVertex": vertex,
            "sourceSupport": source_support,
            "importedSupport": imported_support,
        }
    contract.reset_pose(source_rig)
    contract.reset_pose(imported_rig)
    return {"status": "PASS", "maximumVertexDeltaMeters": maximum, "worst": worst, "poses": reports}


def compare_animation_frames(
    source_body: bpy.types.Object,
    source_rig: bpy.types.Object,
    imported_body: bpy.types.Object,
    imported_rig: bpy.types.Object,
    actions: dict[str, bpy.types.Action],
) -> dict[str, object]:
    if source_rig.animation_data is None:
        source_rig.animation_data_create()
    if imported_rig.animation_data is None:
        imported_rig.animation_data_create()
    reports = {}
    overall_maximum = 0.0
    overall_worst = None
    sampled_frames = 0
    for clip, (frame_start, frame_end, _loop) in contract.CLIPS.items():
        contract.bind_action_slot(source_rig, actions[clip], clip)
        contract.bind_action_slot(imported_rig, actions[clip], clip)
        contract.evaluated_action_motion_report(source_rig, clip)
        contract.evaluated_action_motion_report(imported_rig, clip)
        clip_maximum = 0.0
        clip_worst = None
        for frame in range(frame_start, frame_end + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            contract.elbow_support_pose_report(source_rig)
            contract.elbow_support_pose_report(imported_rig)
            delta, vertex = maximum_vertex_delta(source_body, imported_body)
            contract.require(delta <= contract.ROUNDTRIP_VERTEX_TOLERANCE_METERS, f"{clip} frame {frame} FBX delta {delta}m at {vertex}")
            if delta > clip_maximum:
                clip_maximum = delta
                clip_worst = {"frame": frame, "vertex": vertex}
            if delta > overall_maximum:
                overall_maximum = delta
                overall_worst = {"clip": clip, "frame": frame, "vertex": vertex}
            sampled_frames += 1
        reports[clip] = {"maximumVertexDeltaMeters": clip_maximum, "worst": clip_worst}
    contract.reset_pose(source_rig)
    contract.reset_pose(imported_rig)
    return {
        "status": "PASS",
        "sampledFrames": sampled_frames,
        "maximumVertexDeltaMeters": overall_maximum,
        "worst": overall_worst,
        "clips": reports,
    }


def remove_imported_objects(objects: list[bpy.types.Object]) -> None:
    for obj in objects:
        if obj.name not in bpy.data.objects:
            continue
        data = obj.data
        object_type = obj.type
        bpy.data.objects.remove(obj, do_unlink=True)
        if data is not None and data.users == 0:
            if object_type == "MESH":
                bpy.data.meshes.remove(data)
            elif object_type == "ARMATURE":
                bpy.data.armatures.remove(data)


def run_verification() -> tuple[dict[str, object], dict[str, object], dict[str, object]]:
    staged_manifest, rig_manifest, _animation_manifest, body_provenance = require_frozen_inputs()
    bpy.ops.wm.open_mainfile(filepath=str(contract.STAGED_BLEND))
    rigs = [obj for obj in bpy.context.scene.objects if obj.type == "ARMATURE"]
    contract.require(len(rigs) == 1 and rigs[0].name == "SessionHumanoidRigV2", "Staged Blend rig composition changed")
    source_rig = rigs[0]
    source_rig_transform = object_transform_report(source_rig)
    source_bodies = {
        variant: bpy.data.objects.get(contract.BOUND_MESH_NAMES[variant])
        for variant in contract.VARIANTS
    }
    contract.require(all(body is not None and body.type == "MESH" for body in source_bodies.values()), "Staged Blend body set changed")
    deform_groups = list(rig_manifest["hierarchy"]["deformBoneNames"])
    ordered = [entry["name"] for entry in rig_manifest["hierarchy"]["bones"]]
    source_support_manifest = contract.elbow_support_manifest_report(rig_manifest)
    source_support_rest = contract.elbow_support_rest_report(
        source_rig,
        ordered,
        imported_fbx_semantics=True,
    )
    for body in source_bodies.values():
        contract.weight_audit(body, deform_groups)
    actions, animation_input = contract.import_animation_actions(source_rig)
    variant_reports = {}
    for variant in contract.VARIANTS:
        source_body = source_bodies[variant]
        contract.reset_pose(source_rig)
        imported_rig, imported_body, imported_objects = import_bound_fbx(contract.STAGED_FBX_PATHS[variant])
        hierarchy = hierarchy_round_trip_report(source_rig, imported_rig, rig_manifest)
        imported_rig_transform = object_transform_report(imported_rig)
        imported_weight_audit = contract.weight_audit(imported_body, deform_groups)
        weights = compare_weight_fields(source_body, imported_body)
        source_mirror_weights = mirror_weight_field_report(source_body)
        imported_mirror_weights = mirror_weight_field_report(imported_body)
        source_support_weights = support_weight_contract_report(source_body, source_rig)
        imported_support_weights = support_weight_contract_report(imported_body, imported_rig)
        mesh_structure = mesh_structure_round_trip_report(
            source_body,
            imported_body,
            staged_manifest["variants"][variant],
        )
        rest_delta, rest_vertex = maximum_vertex_delta(source_body, imported_body)
        contract.require(rest_delta <= contract.ROUNDTRIP_VERTEX_TOLERANCE_METERS, f"{variant} rest FBX vertex delta {rest_delta}m")
        static_compare = compare_static_poses(source_body, source_rig, imported_body, imported_rig)
        animation_compare = compare_animation_frames(source_body, source_rig, imported_body, imported_rig, actions)
        imported_static_gates = contract.run_static_deformation_gates(imported_body, imported_rig, deform_groups)
        imported_animation_gates = contract.run_animation_deformation_gates(imported_body, imported_rig, deform_groups, actions)
        variant_reports[variant] = {
            "status": "PASS",
            "hierarchy": hierarchy,
            "sourceRigTransform": source_rig_transform,
            "importedRigTransform": imported_rig_transform,
            "importedWeightAudit": imported_weight_audit,
            "weightFieldRoundTrip": weights,
            "sourceMirrorWeightField": source_mirror_weights,
            "importedMirrorWeightField": imported_mirror_weights,
            "sourceSupportWeightContract": source_support_weights,
            "importedSupportWeightContract": imported_support_weights,
            "meshStructureRoundTrip": mesh_structure,
            "restMaximumVertexDeltaMeters": rest_delta,
            "restWorstVertex": rest_vertex,
            "staticPoseVertexComparison": static_compare,
            "animationFrameVertexComparison": animation_compare,
            "importedStaticDeformationGates": imported_static_gates,
            "importedAnimationDeformationGates": imported_animation_gates,
        }
        remove_imported_objects(imported_objects)
    return staged_manifest, {
        "sourceSupportManifestContract": source_support_manifest,
        "sourceSupportImportedFbxSemantics": source_support_rest,
        "acceptedBodyProvenance": body_provenance,
        "animationInput": animation_input,
        "variants": variant_reports,
    }, rig_manifest


def promote(
    staged_manifest: dict[str, object],
    verification: dict[str, object],
    rig_manifest: dict[str, object],
) -> dict[str, object]:
    contract.require(not contract.BASE_OUTPUT_DIR.exists(), "Canonical output directory appeared during verification")
    contract.require(not contract.PROMOTION_TEMP_DIR.exists(), "Promotion temp directory appeared during verification")
    contract.require(contract.sha256(contract.RIG_FBX) == contract.EXPECTED_RIG_FBX_SHA256, "V2 rig FBX changed during verification")
    contract.require(contract.sha256(contract.RIG_MANIFEST) == contract.EXPECTED_RIG_MANIFEST_SHA256, "V2 rig manifest changed during verification")
    contract.require(contract.sha256(contract.ANIMATION_FBX) == contract.EXPECTED_ANIMATION_FBX_SHA256, "V2 animation FBX changed during verification")
    contract.require(contract.sha256(contract.ANIMATION_MANIFEST) == contract.EXPECTED_ANIMATION_MANIFEST_SHA256, "V2 animation manifest changed during verification")
    contract.require(contract.sha256(contract.BOW_DRAW_POSE_SNAPSHOT) == contract.EXPECTED_BOW_DRAW_POSE_SNAPSHOT_SHA256, "BowDraw pose snapshot changed during verification")
    contract.require(staged_manifest["sourceHashes"]["builder"] == contract.sha256(contract.V2_BINDING_BUILDER), "V2 binding builder changed during verification")
    contract.require(staged_manifest["sourceHashes"]["bindingContract"] == contract.sha256(Path(contract.__file__)), "V2 binding contract changed during verification")
    contract.require(staged_manifest["sourceHashes"]["bowDrawPoseSnapshot"] == contract.sha256(contract.BOW_DRAW_POSE_SNAPSHOT), "BowDraw pose snapshot source pin changed during verification")
    pre_promotion_provenance = contract.v1_body_provenance_report(rig_manifest, contract.V1_RIG_FIT_LANDMARKS)
    staged_package_pre_copy = verify_staged_package_membership_and_hashes(staged_manifest, contract.STAGED_PACKAGE_DIR)
    contract.BASE_SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    shutil.copytree(contract.STAGED_PACKAGE_DIR, contract.PROMOTION_TEMP_DIR)
    promotion_copy = verify_staged_package_membership_and_hashes(staged_manifest, contract.PROMOTION_TEMP_DIR)
    canonical_outputs = {
        "boundBlend": {
            "path": str(contract.BOUND_BLEND.relative_to(contract.PROJECT_ROOT)),
            "sha256": contract.sha256(contract.PROMOTION_TEMP_DIR / contract.BOUND_BLEND.name),
        },
        "fbx": {
            variant: {
                "path": str(path.relative_to(contract.PROJECT_ROOT)),
                "sha256": contract.sha256(contract.PROMOTION_TEMP_DIR / path.name),
            }
            for variant, path in contract.BOUND_FBX_PATHS.items()
        },
    }
    manifest = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV2",
        "status": "PASS_INDEPENDENT_FBX_ROUND_TRIP_CANONICAL_DCC_ONLY",
        "unityIntegrationAllowed": False,
        "sourceHashes": staged_manifest["sourceHashes"],
        "prePromotionProvenance": pre_promotion_provenance,
        "stagedPackagePreCopy": staged_package_pre_copy,
        "promotionCopyVerification": promotion_copy,
        "stagedBuildManifestSha256": contract.sha256(contract.STAGED_BUILD_MANIFEST),
        "verifierSha256": contract.sha256(Path(__file__)),
        "bindingContractSha256": contract.sha256(Path(contract.__file__)),
        "rigJointCount": contract.EXPECTED_RIG_JOINTS,
        "deformGroupCount": contract.EXPECTED_DEFORM_GROUPS,
        "rigOrderedNames": [entry["name"] for entry in rig_manifest["hierarchy"]["bones"]],
        "canonicalOutputs": canonical_outputs,
        "variants": staged_manifest["variants"],
        "roundTripSummary": {
            variant: {
                "restMaximumVertexDeltaMeters": report["restMaximumVertexDeltaMeters"],
                "staticMaximumVertexDeltaMeters": report["staticPoseVertexComparison"]["maximumVertexDeltaMeters"],
                "animationMaximumVertexDeltaMeters": report["animationFrameVertexComparison"]["maximumVertexDeltaMeters"],
                "sampledAnimationFrames": report["animationFrameVertexComparison"]["sampledFrames"],
                "maximumBoneTranslationDeltaMeters": report["hierarchy"]["maximumTranslationDeltaMeters"],
                "maximumBoneRotationDeltaDegrees": report["hierarchy"]["maximumRotationDeltaDegrees"],
                "maximumWeightL1Delta": report["weightFieldRoundTrip"]["maximumPerVertexL1Delta"],
            }
            for variant, report in verification["variants"].items()
        },
        "evidence": staged_manifest["evidence"],
        "deferred": [
            "class-specific hidden-body-face deletion and LODs",
            "class art/materials",
            "Unity import and runtime LateUpdate support driver",
        ],
    }
    temp_manifest = contract.PROMOTION_TEMP_DIR / contract.BINDING_MANIFEST.name
    temp_manifest.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    os.replace(contract.PROMOTION_TEMP_DIR, contract.BASE_OUTPUT_DIR)
    contract.require(contract.BINDING_MANIFEST.is_file(), "Atomic promotion did not produce the canonical manifest")
    return manifest


def write_failure(error: BaseException) -> None:
    contract.ROUNDTRIP_REPORT.parent.mkdir(parents=True, exist_ok=True)
    report = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV2",
        "status": "REJECT_INDEPENDENT_FBX_ROUND_TRIP",
        "unityIntegrationAllowed": False,
        "canonicalOutputsWritten": contract.BASE_OUTPUT_DIR.exists(),
        "error": str(error),
        "traceback": traceback.format_exc(),
        "verifierSha256": contract.sha256(Path(__file__)),
        "bindingContractSha256": contract.sha256(Path(contract.__file__)),
        "note": "No canonical V2 outputs are created before every independent gate passes; inspect any hidden promotion temp directory manually if a filesystem copy failed.",
    }
    contract.ROUNDTRIP_REPORT.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> None:
    if contract.BASE_OUTPUT_DIR.exists():
        raise RuntimeError(
            f"Canonical V2 outputs already exist; refusing rerun without changing accepted evidence: "
            f"{contract.BASE_OUTPUT_DIR}"
        )
    try:
        staged_manifest, verification, rig_manifest = run_verification()
        manifest = promote(staged_manifest, verification, rig_manifest)
        report = {
            "schemaVersion": 1,
            "asset": "SessionHumanoidBaseV2",
            "status": manifest["status"],
            "unityIntegrationAllowed": False,
            "canonicalOutputsWritten": True,
            "verification": verification,
            "canonicalManifest": {
                "path": str(contract.BINDING_MANIFEST.relative_to(contract.PROJECT_ROOT)),
                "sha256": contract.sha256(contract.BINDING_MANIFEST),
            },
        }
        contract.ROUNDTRIP_REPORT.parent.mkdir(parents=True, exist_ok=True)
        contract.ROUNDTRIP_REPORT.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        contract.BINDING_REPORT.write_text(json.dumps({
            "schemaVersion": 1,
            "asset": "SessionHumanoidBaseV2",
            "status": manifest["status"],
            "unityIntegrationAllowed": False,
            "canonicalOutputsWritten": True,
            "canonicalManifest": report["canonicalManifest"],
            "roundTripReport": {
                "path": str(contract.ROUNDTRIP_REPORT.relative_to(contract.REPOSITORY_ROOT)),
                "sha256": contract.sha256(contract.ROUNDTRIP_REPORT),
            },
        }, indent=2, sort_keys=True) + "\n", encoding="utf-8")
        print(json.dumps({
            "status": manifest["status"],
            "bindingManifest": str(contract.BINDING_MANIFEST),
            "bindingManifestSha256": contract.sha256(contract.BINDING_MANIFEST),
            "roundTripReport": str(contract.ROUNDTRIP_REPORT),
            "roundTripReportSha256": contract.sha256(contract.ROUNDTRIP_REPORT),
        }, indent=2, sort_keys=True))
    except BaseException as error:
        if not contract.BASE_OUTPUT_DIR.exists():
            write_failure(error)
        raise


if __name__ == "__main__":
    main()
