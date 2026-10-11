#!/usr/bin/env python3
"""Round-trip the generated fortress FBXs through Blender and verify contracts."""

from __future__ import annotations

import argparse
import json
import math
import sys
from pathlib import Path

import bmesh
import bpy
from mathutils import Vector


FOUNDATION_ASSET_NAME = "FortressFoundationRing"


def parse_args() -> argparse.Namespace:
    script_args: list[str] = []
    if "--" in sys.argv:
        script_args = sys.argv[sys.argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", type=Path, default=Path(__file__).resolve().parents[3])
    return parser.parse_args(script_args)


def world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector((min(point.x for point in points), min(point.y for point in points), min(point.z for point in points)))
    maximum = Vector((max(point.x for point in points), max(point.y for point in points), max(point.z for point in points)))
    return minimum, maximum


def close_enough(actual: float, expected: float, tolerance: float = 0.001) -> bool:
    return math.isclose(actual, expected, rel_tol=0.0, abs_tol=tolerance)


def mesh_is_closed_manifold(mesh: bpy.types.Mesh) -> bool:
    bm = bmesh.new()
    bm.from_mesh(mesh)
    result = all(len(edge.link_faces) == 2 for edge in bm.edges)
    bm.free()
    return result


def verify_record(project_root: Path, record: dict[str, object]) -> dict[str, object]:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    fbx_path = project_root / str(record["fbx"])
    bpy.ops.import_scene.fbx(filepath=str(fbx_path))

    asset_name = str(record["asset"])
    mesh_name = str(record["mesh_object"])
    roots = [obj for obj in bpy.context.scene.objects if obj.type == "EMPTY"]
    meshes = [obj for obj in bpy.context.scene.objects if obj.type == "MESH"]
    forbidden = [obj.name for obj in bpy.context.scene.objects if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}]
    failures: list[str] = []

    if [root.name for root in roots] != [asset_name]:
        failures.append(f"unexpected roots: {[root.name for root in roots]}")
    if [mesh.name for mesh in meshes] != [mesh_name]:
        failures.append(f"unexpected meshes: {[mesh.name for mesh in meshes]}")
    if forbidden:
        failures.append(f"forbidden exported objects: {forbidden}")
    if failures:
        raise RuntimeError(f"{asset_name} FBX structure failed: {'; '.join(failures)}")

    root = roots[0]
    mesh = meshes[0]
    if mesh.parent is not root:
        failures.append("mesh is not parented to the stable root")
    if root.location.length > 0.0001:
        failures.append(f"root location is {tuple(root.location)}")
    if any(not close_enough(value, 1.0, 0.0001) for value in root.scale):
        failures.append(f"root scale is {tuple(root.scale)}")

    mesh.data.calc_loop_triangles()
    triangles = len(mesh.data.loop_triangles)
    if triangles != int(record["triangles"]):
        failures.append(f"triangles {triangles} != manifest {record['triangles']}")
    materials = [slot.material.name if slot.material else "" for slot in mesh.material_slots]
    if materials != list(record["material_slots"]):
        failures.append(f"materials {materials} != manifest {record['material_slots']}")
    uv_layers = [layer.name for layer in mesh.data.uv_layers]
    if uv_layers != ["UV0"]:
        failures.append(f"UV layers are {uv_layers}")
    if any(len(polygon.vertices) != 3 for polygon in mesh.data.polygons):
        failures.append("mesh is not fully triangulated")
    if not mesh_is_closed_manifold(mesh.data):
        failures.append("mesh contains open or non-manifold edges")

    minimum, maximum = world_bounds(mesh)
    size = maximum - minimum
    for axis, actual, expected in zip("XYZ", size, record["size_blender_xyz"], strict=True):
        if not close_enough(actual, float(expected), 0.0015):
            failures.append(f"{axis} size {actual:.4f} != manifest {float(expected):.4f}")
    if asset_name != FOUNDATION_ASSET_NAME and minimum.z < -0.001:
        failures.append(f"minimum height {minimum.z:.4f} is below ground")
    if asset_name == "Wardstone":
        maximum_planar_vertex_radius = max(
            math.hypot(
                (mesh.matrix_world @ vertex.co).x,
                (mesh.matrix_world @ vertex.co).y,
            )
            for vertex in mesh.data.vertices
        )
        if maximum_planar_vertex_radius < 0.98 or maximum_planar_vertex_radius > 1.03:
            failures.append(
                "footprint radius "
                f"{maximum_planar_vertex_radius:.4f} is outside 0.98-1.03m"
            )
        if maximum.z < 2.28 or maximum.z > 2.35:
            failures.append(
                f"visual height {maximum.z:.4f} is outside 2.28-2.35m"
            )
        contract = record.get("wardstone_design_contract")
        if not isinstance(contract, dict) or contract.get("profile") != "broad_stacked_octagonal_masonry":
            failures.append("broad stacked octagonal masonry contract is missing")
    if asset_name == FOUNDATION_ASSET_NAME:
        minimum_planar_vertex_radius = min(
            math.hypot(
                (mesh.matrix_world @ vertex.co).x,
                (mesh.matrix_world @ vertex.co).y,
            )
            for vertex in mesh.data.vertices
        )
        maximum_planar_vertex_radius = max(
            math.hypot(
                (mesh.matrix_world @ vertex.co).x,
                (mesh.matrix_world @ vertex.co).y,
            )
            for vertex in mesh.data.vertices
        )
        if triangles > 8_000:
            failures.append(f"foundation triangles {triangles} exceed 8,000")
        if minimum_planar_vertex_radius < 21.25 or minimum_planar_vertex_radius > 21.40:
            failures.append(
                "foundation inner radius "
                f"{minimum_planar_vertex_radius:.4f} is outside 21.25-21.40m"
            )
        if maximum_planar_vertex_radius < 25.30 or maximum_planar_vertex_radius > 25.50:
            failures.append(
                "foundation outer radius "
                f"{maximum_planar_vertex_radius:.4f} is outside 25.30-25.50m"
            )
        if not close_enough(maximum.z, -0.45, 0.025):
            failures.append(
                f"foundation top {maximum.z:.4f} is not recessed near -0.45m"
            )
        if not close_enough(minimum.z, -1.80, 0.025):
            failures.append(
                f"foundation bottom {minimum.z:.4f} is not near -1.80m"
            )
        contract = record.get("foundation_ring_design_contract")
        if not isinstance(contract, dict):
            failures.append("foundation ring design contract is missing")
        else:
            if contract.get("profile") != "continuous_submerged_sloped_masonry_shoulder":
                failures.append("foundation profile contract is incorrect")
            if int(contract.get("bay_count", 0)) != 18:
                failures.append("foundation does not declare eighteen constructed bays")
            if int(contract.get("flat_reachable_ledges", -1)) != 0:
                failures.append("foundation declares a reachable flat ledge")
            if int(contract.get("joined_static_meshes", 0)) != 1:
                failures.append("foundation is not declared as one joined static mesh")

    if failures:
        raise RuntimeError(f"{asset_name} FBX round-trip failed: {'; '.join(failures)}")
    result = {
        "asset": asset_name,
        "fbx": str(record["fbx"]),
        "root": root.name,
        "mesh": mesh.name,
        "triangles": triangles,
        "materials": materials,
        "uv_layers": uv_layers,
        "size_xyz": [round(value, 4) for value in size],
        "round_trip": "passed",
    }
    if asset_name == "Wardstone":
        result["maximum_planar_vertex_radius"] = round(maximum_planar_vertex_radius, 4)
        result["wardstone_profile"] = "broad_stacked_octagonal_masonry"
    if asset_name == FOUNDATION_ASSET_NAME:
        result["minimum_planar_vertex_radius"] = round(
            minimum_planar_vertex_radius,
            4,
        )
        result["maximum_planar_vertex_radius"] = round(
            maximum_planar_vertex_radius,
            4,
        )
        result["height_range"] = [round(minimum.z, 4), round(maximum.z, 4)]
        result["foundation_profile"] = (
            "continuous_submerged_sloped_masonry_shoulder"
        )
    return result


def main() -> None:
    args = parse_args()
    project_root = args.project_root.resolve()
    manifest_path = project_root / "Assets" / "SessionRpg" / "SourceArt" / "Environment" / "FortressKit.manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    camera = manifest.get("gameplay_camera_contract")
    expected_camera = {
        "yaw_degrees": 90.0,
        "pitch_degrees": 56.0,
        "distance_meters": 22.0,
        "pivot_height_meters": 0.9,
        "forward_focus_offset_meters": 2.3,
        "right_focus_offset_meters": 0.0,
        "vertical_fov_degrees": 50.0,
        "render_aspect_ratio": [16, 9],
    }
    if camera != expected_camera:
        raise RuntimeError(
            "Fortress gameplay-camera evidence contract drifted: "
            f"{camera} != {expected_camera}"
        )
    foundation = manifest.get("foundation_ring")
    if not isinstance(foundation, dict):
        raise RuntimeError("Fortress manifest is missing the foundation ring contract")
    if foundation.get("continuous_lava_band_meters") != [15.0, 21.3]:
        raise RuntimeError(
            "Foundation does not preserve the exact 15.0-21.30m lava band"
        )
    if foundation.get("authoritative_gameplay_geometry") is not False:
        raise RuntimeError("Foundation unexpectedly claims gameplay authority")
    results = [verify_record(project_root, record) for record in manifest["assets"]]
    report = {
        "schema": 1,
        "blender_version": bpy.app.version_string,
        "source_manifest": str(manifest_path.relative_to(project_root)),
        "result": "passed",
        "assets": results,
    }
    evidence_dir = project_root.parent / "outputs" / "art" / "blender" / "fortress-kit"
    evidence_dir.mkdir(parents=True, exist_ok=True)
    report_path = evidence_dir / "FortressKitFbxRoundTrip.json"
    report_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print("SESSION_RPG_FORTRESS_KIT_ROUND_TRIP_OK")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
