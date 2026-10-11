#!/usr/bin/env python3
"""Round-trip the generated arena-floor FBX through Blender."""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import sys
from pathlib import Path

import bpy
from mathutils import Vector


ARENA_RADIUS = 15.0
UV_WORLD_SPAN_METERS = 30.0
EXPECTED_TRIANGLES = 15_320
EXPECTED_GEOMETRIC_FLAGSTONES = 147
EXPECTED_MATERIALS = [
    "ArenaFloor_Stone",
    "ArenaFloor_Trim",
    "ArenaFloor_Grout",
]
EXPECTED_MACRO_TILING = [1.0, 1.0]
EXPECTED_MACRO_OFFSET = [0.0, 0.0]
EXPECTED_MICRO_TILING = [4.6875, 4.6875]
EXPECTED_MICRO_OFFSET = [0.47125, 0.64125]


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def verify_texture_record(
    project_root: Path,
    record: dict[str, object],
    failures: list[str],
) -> None:
    path = project_root / str(record["path"])
    if not path.is_file():
        failures.append(f"texture is missing: {path}")
        return
    actual_hash = sha256(path)
    if actual_hash != record["sha256"]:
        failures.append(
            f"texture hash changed for {record['path']}: {actual_hash}"
        )
    image = bpy.data.images.load(str(path), check_existing=False)
    width, height = image.size
    bpy.data.images.remove(image)
    if width != record["width"] or height != record["height"]:
        failures.append(
            f"texture dimensions changed for {record['path']}: "
            f"{width}x{height}"
        )


def parse_args() -> argparse.Namespace:
    script_args: list[str] = []
    if "--" in sys.argv:
        script_args = sys.argv[sys.argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--project-root",
        type=Path,
        default=Path(__file__).resolve().parents[3],
    )
    return parser.parse_args(script_args)


def main() -> None:
    args = parse_args()
    project_root = args.project_root.resolve()
    manifest_path = (
        project_root
        / "Assets"
        / "SessionRpg"
        / "SourceArt"
        / "Environment"
        / "ArenaFloorVisual.manifest.json"
    )
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    expected = manifest["asset"]
    uv_contract = manifest["uv_contract"]
    macro_albedo = manifest["macro_albedo"]
    design_contract = manifest["design_contract"]
    camera_contract = manifest["gameplay_camera_contract"]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    fbx_path = project_root / expected["fbx"]
    bpy.ops.import_scene.fbx(filepath=str(fbx_path))

    objects = list(bpy.context.scene.objects)
    roots = [obj for obj in objects if obj.type == "EMPTY"]
    meshes = [obj for obj in objects if obj.type == "MESH"]
    forbidden = [
        obj.name
        for obj in objects
        if obj.type in {"CAMERA", "LIGHT", "ARMATURE"}
    ]
    failures: list[str] = []
    if [obj.name for obj in roots] != [expected["root_object"]]:
        failures.append(f"roots are {[obj.name for obj in roots]}")
    if [obj.name for obj in meshes] != [expected["mesh_object"]]:
        failures.append(f"meshes are {[obj.name for obj in meshes]}")
    if forbidden:
        failures.append(f"forbidden objects are {forbidden}")
    if failures:
        raise RuntimeError("Arena floor structure failed: " + "; ".join(failures))

    root = roots[0]
    mesh = meshes[0]
    if mesh.parent is not root:
        failures.append("mesh is not parented to root")
    if bool(root.get("authoritative_collision", True)):
        failures.append("root unexpectedly claims authoritative collision")
    if not bool(root.get("visual_only", False)):
        failures.append("root is not marked visual-only")
    mesh.data.calc_loop_triangles()
    triangles = len(mesh.data.loop_triangles)
    if triangles != expected["triangles"]:
        failures.append(
            f"triangles {triangles} != {expected['triangles']}"
        )
    if triangles != EXPECTED_TRIANGLES:
        failures.append(
            f"triangles {triangles} changed from {EXPECTED_TRIANGLES}"
        )
    vertices = len(mesh.data.vertices)
    if vertices != expected["vertices"]:
        failures.append(f"vertices {vertices} != {expected['vertices']}")
    materials = [
        slot.material.name if slot.material else ""
        for slot in mesh.material_slots
    ]
    if materials != expected["material_slots"]:
        failures.append(f"materials are {materials}")
    if materials != EXPECTED_MATERIALS:
        failures.append(f"material contract changed to {materials}")
    uv_layers = [layer.name for layer in mesh.data.uv_layers]
    if uv_layers != ["UV0"]:
        failures.append(f"UV layers are {uv_layers}")
    uv_layer = mesh.data.uv_layers.get("UV0")
    maximum_uv_error = 0.0
    uv_bounds = [math.inf, math.inf, -math.inf, -math.inf]
    if uv_layer is not None:
        for polygon in mesh.data.polygons:
            for loop_index in polygon.loop_indices:
                loop = mesh.data.loops[loop_index]
                coordinate = mesh.matrix_world @ mesh.data.vertices[
                    loop.vertex_index
                ].co
                expected_u = (
                    coordinate.x + ARENA_RADIUS
                ) / UV_WORLD_SPAN_METERS
                expected_v = (
                    coordinate.y + ARENA_RADIUS
                ) / UV_WORLD_SPAN_METERS
                uv = uv_layer.data[loop_index].uv
                maximum_uv_error = max(
                    maximum_uv_error,
                    abs(uv.x - expected_u),
                    abs(uv.y - expected_v),
                )
                uv_bounds[0] = min(uv_bounds[0], uv.x)
                uv_bounds[1] = min(uv_bounds[1], uv.y)
                uv_bounds[2] = max(uv_bounds[2], uv.x)
                uv_bounds[3] = max(uv_bounds[3], uv.y)
        if maximum_uv_error > 0.00001:
            failures.append(
                "UV0 no longer matches the exact 30 m planar mapping; "
                f"maximum error is {maximum_uv_error:.8f}"
            )
        for actual, expected_bound in zip(
            uv_bounds,
            (0.0, 0.0, 1.0, 1.0),
            strict=True,
        ):
            if not math.isclose(actual, expected_bound, abs_tol=0.00001):
                failures.append(
                    "UV0 bounds changed to "
                    f"{tuple(round(value, 7) for value in uv_bounds)}"
                )
                break
    maximum_radius = max(
        math.hypot(
            (mesh.matrix_world @ vertex.co).x,
            (mesh.matrix_world @ vertex.co).y,
        )
        for vertex in mesh.data.vertices
    )
    points = [mesh.matrix_world @ Vector(corner) for corner in mesh.bound_box]
    maximum_z = max(point.z for point in points)
    minimum_z = min(point.z for point in points)
    if not math.isclose(maximum_radius, ARENA_RADIUS, abs_tol=0.001):
        failures.append(f"radius is {maximum_radius:.4f}")
    if not math.isclose(maximum_z, 0.0, abs_tol=0.001):
        failures.append(f"top is {maximum_z:.4f}")
    if minimum_z < -0.121:
        failures.append(f"minimum is {minimum_z:.4f}")
    if uv_contract.get("mapping") != "arena_disk_world_planar":
        failures.append(f"UV mapping is {uv_contract.get('mapping')}")
    if uv_contract.get("meters_per_uv_span") != UV_WORLD_SPAN_METERS:
        failures.append("UV world span is not exactly 30 m")
    if uv_contract.get("macro_base_map_tiling") != EXPECTED_MACRO_TILING:
        failures.append("macro BaseMap tiling is not 1/1")
    if uv_contract.get("macro_base_map_offset") != EXPECTED_MACRO_OFFSET:
        failures.append("macro BaseMap offset is not 0/0")
    if uv_contract.get("micro_normal_mask_tiling") != EXPECTED_MICRO_TILING:
        failures.append("micro normal/mask tiling is not 4.6875")
    if uv_contract.get("micro_normal_mask_offset") != EXPECTED_MICRO_OFFSET:
        failures.append("micro normal/mask phase offset changed")
    if macro_albedo.get("overlay_mesh") is not False:
        failures.append("macro albedo unexpectedly uses an overlay mesh")
    if macro_albedo.get("secondary_albedo_blend") is not False:
        failures.append("macro albedo unexpectedly blends a second albedo")
    verify_texture_record(project_root, macro_albedo["source"], failures)
    verify_texture_record(project_root, macro_albedo["runtime"], failures)
    if design_contract.get("geometric_flagstone_count") != EXPECTED_GEOMETRIC_FLAGSTONES:
        failures.append("geometric flagstone count changed from 147")
    if design_contract.get("authoritative_collision") is not False:
        failures.append("manifest unexpectedly claims authoritative collision")
    expected_camera = {
        "yaw_degrees": 90.0,
        "pitch_degrees": 40.0,
        "distance_meters": 18.0,
        "pivot_height_meters": 0.9,
        "forward_focus_offset_meters": 2.3,
        "right_focus_offset_meters": 0.0,
        "vertical_fov_degrees": 46.0,
        "render_aspect_ratio": [16, 9],
    }
    if camera_contract != expected_camera:
        failures.append(f"gameplay camera contract is {camera_contract}")
    if failures:
        raise RuntimeError("Arena floor round-trip failed: " + "; ".join(failures))

    report = {
        "schema": 1,
        "result": "passed",
        "blender_version": bpy.app.version_string,
        "source_manifest": str(manifest_path.relative_to(project_root)),
        "asset": {
            "root": root.name,
            "mesh": mesh.name,
            "triangles": triangles,
            "vertices": vertices,
            "materials": materials,
            "uv_layers": uv_layers,
            "uv0_bounds": [round(value, 7) for value in uv_bounds],
            "uv0_maximum_mapping_error": round(maximum_uv_error, 8),
            "radius_meters": round(maximum_radius, 4),
            "minimum_z": round(minimum_z, 4),
            "maximum_z": round(maximum_z, 4),
            "round_trip": "passed",
            "authoritative_collision": False,
        },
        "macro_albedo": {
            "source_sha256": macro_albedo["source"]["sha256"],
            "runtime_sha256": macro_albedo["runtime"]["sha256"],
            "base_map_tiling": EXPECTED_MACRO_TILING,
            "base_map_offset": EXPECTED_MACRO_OFFSET,
            "overlay_mesh": False,
        },
        "micro_normal_mask": {
            "tiling": EXPECTED_MICRO_TILING,
            "offset": EXPECTED_MICRO_OFFSET,
        },
        "gameplay_camera_contract": camera_contract,
    }
    evidence_dir = (
        project_root.parent / "outputs" / "art" / "blender" / "arena-floor"
    )
    evidence_dir.mkdir(parents=True, exist_ok=True)
    report_path = evidence_dir / "ArenaFloorFbxRoundTrip.json"
    report_path.write_text(
        json.dumps(report, indent=2) + "\n",
        encoding="utf-8",
    )
    print("SESSION_RPG_ARENA_FLOOR_ROUND_TRIP_OK")
    print(json.dumps(report, indent=2))


if __name__ == "__main__":
    main()
