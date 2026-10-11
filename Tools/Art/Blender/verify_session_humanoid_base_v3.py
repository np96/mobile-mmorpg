#!/usr/bin/env python3
"""Independently verify and render SessionHumanoidBaseV3 deformation."""

from __future__ import annotations

import json
import math
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector

import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
import session_humanoid_base_v3_contract as contract


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def validate_mesh_contract(body: bpy.types.Object, rig: bpy.types.Object) -> dict[str, object]:
    require(body.type == "MESH", f"{body.name} is not a mesh")
    require(body.parent == rig, f"{body.name} is not parented to its rig")
    armatures = [modifier for modifier in body.modifiers if modifier.type == "ARMATURE"]
    require(len(armatures) == 1 and armatures[0].object == rig, f"{body.name} armature modifier changed")
    body.data.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(body.data)
    topology = {
        "vertices": len(body.data.vertices),
        "polygons": len(body.data.polygons),
        "triangles": len(body.data.loop_triangles),
        "boundaryEdges": sum(edge.is_boundary for edge in bm.edges),
        "nonManifoldEdges": sum(not edge.is_manifold for edge in bm.edges),
        "degenerateFaces": sum(face.calc_area() <= 1e-12 for face in bm.faces),
        "uvLayers": [layer.name for layer in body.data.uv_layers],
    }
    bm.free()
    require(topology["vertices"] == 12502, f"{body.name} vertex count changed")
    require(topology["triangles"] == 25000, f"{body.name} triangle count changed")
    require(topology["boundaryEdges"] == 0, f"{body.name} is no longer closed")
    require(topology["nonManifoldEdges"] == 0, f"{body.name} is non-manifold")
    require(topology["degenerateFaces"] == 0, f"{body.name} has rest degenerates")
    require(topology["uvLayers"] == ["UVMap"], f"{body.name} UV0 changed")

    group_names = {group.index: group.name for group in body.vertex_groups}
    maximum_influences = 0
    minimum_sum = float("inf")
    maximum_sum = 0.0
    twist_assignments = {name: 0 for name in contract.TWIST_BONES}
    legacy_assignments = {name: 0 for name in contract.LEGACY_SUPPORT_BONES}
    for vertex in body.data.vertices:
        row = [
            (group_names[membership.group], float(membership.weight))
            for membership in vertex.groups
            if membership.weight > 1e-8
        ]
        total = sum(weight for _name, weight in row)
        maximum_influences = max(maximum_influences, len(row))
        minimum_sum = min(minimum_sum, total)
        maximum_sum = max(maximum_sum, total)
        for name, _weight in row:
            if name in twist_assignments:
                twist_assignments[name] += 1
            if name in legacy_assignments:
                legacy_assignments[name] += 1
    require(maximum_influences <= 4, f"{body.name} exceeds four influences")
    require(0.9999 <= minimum_sum <= maximum_sum <= 1.0001, f"{body.name} weights are not normalized")
    require(all(count > 0 for count in twist_assignments.values()), f"{body.name} does not weight every twist bone")
    require(not any(legacy_assignments.values()), f"{body.name} weights a rejected legacy support")

    shape_keys = [key.name for key in body.data.shape_keys.key_blocks]
    expected_correctives = [
        "Basis",
        "LeftShoulder", "LeftElbow", "LeftWrist", "LeftHip", "LeftKnee", "LeftAnkle",
        "RightShoulder", "RightElbow", "RightWrist", "RightHip", "RightKnee", "RightAnkle",
    ]
    require(shape_keys == expected_correctives, f"{body.name} corrective set changed")
    drivers = body.data.shape_keys.animation_data.drivers if body.data.shape_keys.animation_data else []
    require(len(drivers) == 12, f"{body.name} does not have exactly twelve corrective drivers")
    return {
        "topology": topology,
        "maximumInfluences": maximum_influences,
        "minimumWeightSum": minimum_sum,
        "maximumWeightSum": maximum_sum,
        "twistAssignments": twist_assignments,
        "legacySupportAssignments": legacy_assignments,
        "shapeKeys": shape_keys,
        "correctiveDriverCount": len(drivers),
    }


def validate_rig_contract(rig: bpy.types.Object) -> dict[str, object]:
    require(rig.type == "ARMATURE", f"{rig.name} is not an armature")
    require(len(rig.data.bones) == 76, f"{rig.name} bone count changed")
    require(all(name in rig.data.bones for name in contract.TWIST_BONES), f"{rig.name} lost twist bones")
    require(all(not rig.data.bones[name].use_deform for name in contract.LEGACY_SUPPORT_BONES), f"{rig.name} re-enabled legacy supports")
    require(all(rig.data.bones[name].use_deform for name in contract.TWIST_BONES), f"{rig.name} disabled a twist bone")
    require(rig.get("sourceHumanoidRig") == "SessionHumanoidRigV2", f"{rig.name} source mapping changed")
    require(rig.get("sourceRigifyControls") == contract.CANONICAL_CONTROL_NAME, f"{rig.name} Rigify source changed")
    return {
        "bones": len(rig.data.bones),
        "deformBones": sum(bone.use_deform for bone in rig.data.bones),
        "twistBones": list(contract.TWIST_BONES),
        "legacySupportDeform": {name: rig.data.bones[name].use_deform for name in contract.LEGACY_SUPPORT_BONES},
        "humanoidSource": rig.get("sourceHumanoidRig"),
        "rigifyControlSource": rig.get("sourceRigifyControls"),
    }


def configure_render() -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.48, 0.52, 0.57)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.display.shading.curvature_ridge_factor = 1.8
    scene.display.shading.curvature_valley_factor = 1.3
    scene.render.resolution_x = 1000
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("SessionHumanoidBaseV3ProofWorld")
    scene.world.color = (0.018, 0.022, 0.03)
    camera_data = bpy.data.cameras.get("SessionHumanoidBaseV3ProofCameraData")
    if camera_data is None:
        camera_data = bpy.data.cameras.new("SessionHumanoidBaseV3ProofCameraData")
    camera = bpy.data.objects.get("SessionHumanoidBaseV3ProofCamera")
    if camera is None:
        camera = bpy.data.objects.new("SessionHumanoidBaseV3ProofCamera", camera_data)
        scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    scene.camera = camera
    return camera


def evaluated_bounds(objects: tuple[bpy.types.Object, ...]) -> tuple[Vector, Vector]:
    points: list[Vector] = []
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
        try:
            points.extend(evaluated.matrix_world @ vertex.co for vertex in mesh.vertices)
        finally:
            evaluated.to_mesh_clear()
    require(points, "No evaluated proof geometry")
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def render_pose(
    variant: str,
    pose_name: str,
    body: bpy.types.Object,
    eyes: tuple[bpy.types.Object, bpy.types.Object],
    camera: bpy.types.Object,
) -> str:
    active = (body, *eyes)
    active_set = set(active)
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH" and not obj.name.startswith("WGT-"):
            obj.hide_render = obj not in active_set
    minimum, maximum = evaluated_bounds(active)
    center = (minimum + maximum) * 0.5
    dimensions = maximum - minimum
    camera.data.ortho_scale = max(dimensions.z * 1.18, dimensions.x * 1.10, dimensions.y * 1.10)
    camera.location = center + Vector((3.2, -4.2, 1.25))
    look_at(camera, center + Vector((0.0, 0.0, dimensions.z * 0.02)))
    output = contract.DEFORMATION_ROOT / f"SessionHumanoidBaseV3_{variant}_{pose_name}.png"
    bpy.context.scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    return str(output.relative_to(contract.PROJECT_ROOT))


def active_corrective_values(body: bpy.types.Object) -> dict[str, float]:
    return {
        key.name: float(key.value)
        for key in body.data.shape_keys.key_blocks
        if key.name != "Basis" and key.value > 1e-5
    }


def main() -> None:
    require(contract.BLEND_PATH.is_file(), f"Missing body source: {contract.BLEND_PATH}")
    require(contract.MANIFEST_PATH.is_file(), f"Missing body manifest: {contract.MANIFEST_PATH}")
    manifest = json.loads(contract.MANIFEST_PATH.read_text(encoding="utf-8"))
    require(
        contract.file_sha256(contract.BLEND_PATH) == manifest["blendSha256"],
        "SessionHumanoidBaseV3 blend hash changed",
    )
    bpy.ops.wm.open_mainfile(filepath=str(contract.BLEND_PATH), load_ui=False)
    require(contract.CANONICAL_META_NAME in bpy.data.objects, "Accepted Rigify metarig is absent")
    require(contract.CANONICAL_CONTROL_NAME in bpy.data.objects, "Accepted Rigify controls are absent")
    contract.DEFORMATION_ROOT.mkdir(parents=True, exist_ok=True)
    camera = configure_render()
    reports: dict[str, object] = {}
    failures: list[str] = []

    for variant in contract.VARIANTS:
        body = bpy.data.objects.get(contract.BODY_NAMES[variant])
        rig = bpy.data.objects.get(contract.RIG_NAMES[variant])
        eyes = tuple(bpy.data.objects.get(name) for name in contract.EYE_NAMES[variant])
        require(body is not None and rig is not None and all(eye is not None for eye in eyes), f"{variant} proof objects are missing")
        rig_report = validate_rig_contract(rig)
        mesh_report = validate_mesh_contract(body, rig)
        contract.reset_pose(rig)
        rest_positions = contract.evaluated_positions(body)
        rest_minimum = Vector(tuple(min(point[axis] for point in rest_positions) for axis in range(3)))
        rest_maximum = Vector(tuple(max(point[axis] for point in rest_positions) for axis in range(3)))
        rest_height = rest_maximum.z - rest_minimum.z
        pose_reports = []
        for pose in contract.POSES:
            contract.reset_pose(rig)
            pose.apply(rig)
            bpy.context.view_layer.update()
            posed_positions = contract.evaluated_positions(body)
            metrics = contract.deformation_metrics(body, rest_positions, posed_positions)
            metrics["heightRatioVersusRest"] = metrics["heightMeters"] / rest_height
            gate_failures = []
            if metrics["minimumTriangleAreaRatio"] < pose.minimum_area_ratio:
                gate_failures.append(
                    f"area {metrics['minimumTriangleAreaRatio']:.6f} < {pose.minimum_area_ratio:.6f}"
                )
            if metrics["minimumEdgeLengthRatio"] < pose.minimum_edge_ratio:
                gate_failures.append(
                    f"edge min {metrics['minimumEdgeLengthRatio']:.6f} < {pose.minimum_edge_ratio:.6f}"
                )
            if metrics["maximumEdgeLengthRatio"] > pose.maximum_edge_ratio:
                gate_failures.append(
                    f"edge max {metrics['maximumEdgeLengthRatio']:.6f} > {pose.maximum_edge_ratio:.6f}"
                )
            if metrics["degenerateTriangles"]:
                gate_failures.append(f"{metrics['degenerateTriangles']} degenerate triangles")
            evidence = render_pose(variant, pose.name, body, eyes, camera)
            status = "PASS" if not gate_failures else "REJECT"
            if gate_failures:
                failures.append(f"{variant}/{pose.name}: {', '.join(gate_failures)}")
            pose_reports.append(
                {
                    "pose": pose.name,
                    "status": status,
                    "metrics": metrics,
                    "activeCorrectives": active_corrective_values(body),
                    "evidence": evidence,
                    "failures": gate_failures,
                }
            )
        contract.reset_pose(rig)
        reports[variant] = {
            "rig": rig_report,
            "meshAndWeights": mesh_report,
            "restHeightMeters": rest_height,
            "poses": pose_reports,
        }

    report = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV3",
        "status": "PASS_TWO_VARIANT_DEFORMATION_PROOF" if not failures else "REJECT_TWO_VARIANT_DEFORMATION_PROOF",
        "sourceBlend": str(contract.BLEND_PATH.relative_to(contract.PROJECT_ROOT)),
        "sourceBlendSha256": contract.file_sha256(contract.BLEND_PATH),
        "unchangedGates": {
            "minimumTriangleAreaRatio": 0.15,
            "minimumEdgeLengthRatio": 0.25,
            "maximumEdgeLengthRatio": 4.0,
            "maximumInfluences": 4,
            "weightSumTolerance": 0.0001,
        },
        "variants": reports,
        "failures": failures,
        "unityIntegrationAllowed": False,
        "nextGate": "manual deformation plate review, then Hunter motion",
        "blenderVersion": list(bpy.app.version),
    }
    contract.DEFORMATION_REPORT.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    pending_review = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV3",
        "status": "PENDING_MANUAL_REVIEW" if not failures else "BLOCKED_BY_NUMERIC_DEFORMATION_FAILURE",
        "reviewScope": [
            "shoulders",
            "elbows and forearm pronation",
            "wrists and hands",
            "hips and knees",
            "ankles, feet, and toes",
            "both masculine and feminine variants",
        ],
        "promotionAllowed": False,
    }
    contract.MANUAL_REVIEW_PATH.write_text(
        json.dumps(pending_review, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(report["status"])
    if failures:
        raise RuntimeError("; ".join(failures[:8]))


if __name__ == "__main__":
    main()
