#!/usr/bin/env python3
"""Build the original Session RPG arena-floor visual with Blender 5.2 LTS.

The export is visual-only. Authoritative movement, collision, hazards, and targeting
remain in Unity/Core. Run from the game-rpg directory:

    /Applications/Blender.app/Contents/MacOS/Blender \
        --background --factory-startup --python-exit-code 1 \
        --python Tools/Art/Blender/build_arena_floor.py
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import os
import sys
from pathlib import Path
from typing import Iterable, Sequence

import bmesh
import bpy
from mathutils import Vector


SCRIPT_DIRECTORY = Path(__file__).resolve().parent
if str(SCRIPT_DIRECTORY) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIRECTORY))

from arena_floor_surface_maps import (
    MACRO_MASK_FILENAME,
    MACRO_NORMAL_FILENAME,
    NORMAL_FILENAME,
    build_surface_maps,
)


SCRIPT_VERSION = "1.5.0"
BLENDER_MINIMUM = (5, 2, 0)
ASSET_NAME = "ArenaFloorVisual"
ARENA_RADIUS = 15.0
MAXIMUM_TRIANGLES = 28_000
MAXIMUM_VERTICES = 65_535
EXPECTED_TRIANGLES = 15_320
EXPECTED_GEOMETRIC_FLAGSTONES = 147
MATERIAL_STONE = "ArenaFloor_Stone"
MATERIAL_TRIM = "ArenaFloor_Trim"
MATERIAL_GROUT = "ArenaFloor_Grout"
MATERIAL_ORDER = (MATERIAL_STONE, MATERIAL_TRIM, MATERIAL_GROUT)
UV_WORLD_SPAN_METERS = ARENA_RADIUS * 2.0
MACRO_BASE_MAP_TILING = 1.0
MACRO_BASE_MAP_OFFSET = (0.0, 0.0)
MICRO_DETAIL_TILING = 4.6875
MICRO_DETAIL_OFFSET = (0.47125, 0.64125)
MICRO_DETAIL_REPEAT_METERS = UV_WORLD_SPAN_METERS / MICRO_DETAIL_TILING
MACRO_SOURCE_RELATIVE_PATH = Path(
    "SourceArt/Blender/Environment/Textures/"
    "ArenaFloorMacroAlbedoSource.png"
)
MACRO_RUNTIME_RELATIVE_PATH = Path(
    "Assets/SessionRpg/Art/Environment/Textures/"
    "ArenaFloorMacroAlbedo.png"
)
MASONRY_BOUNDARIES = (2.50, 4.60, 6.95, 9.36, 11.92, 14.50)
MASONRY_SEGMENTS = (15, 22, 29, 37, 44)
MASONRY_PHASE_FRACTIONS = (0.08, 0.43, 0.19, 0.61, 0.31)
MASONRY_BOUNDARY_AMPLITUDES = (0.04, 0.22, 0.25, 0.23, 0.20, 0.04)


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
    parser.add_argument("--skip-preview", action="store_true")
    return parser.parse_args(script_args)


def reset_scene() -> None:
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in tuple(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for mesh in tuple(bpy.data.meshes):
        bpy.data.meshes.remove(mesh)
    for material in tuple(bpy.data.materials):
        bpy.data.materials.remove(material)

    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.engine = "BLENDER_EEVEE"
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.world.color = (0.012, 0.018, 0.032)
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.012, 0.018, 0.032, 1.0)
    background.inputs["Strength"].default_value = 0.28


def create_material(
    name: str,
    color: tuple[float, float, float, float],
    metallic: float,
    roughness: float,
) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    node = material.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value = color
    node.inputs["Metallic"].default_value = metallic
    node.inputs["Roughness"].default_value = roughness
    return material


def create_materials() -> dict[str, bpy.types.Material]:
    return {
        MATERIAL_STONE: create_material(
            MATERIAL_STONE,
            (0.92, 0.96, 1.0, 1.0),
            0.03,
            0.82,
        ),
        MATERIAL_TRIM: create_material(
            MATERIAL_TRIM,
            (0.30, 0.23, 0.14, 1.0),
            0.26,
            0.70,
        ),
        MATERIAL_GROUT: create_material(
            MATERIAL_GROUT,
            (0.025, 0.031, 0.044, 1.0),
            0.0,
            0.94,
        ),
    }


def create_asset_collection() -> tuple[bpy.types.Collection, bpy.types.Object]:
    collection = bpy.data.collections.new(ASSET_NAME)
    bpy.context.scene.collection.children.link(collection)
    root = bpy.data.objects.new(ASSET_NAME, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.6
    root["asset_id"] = ASSET_NAME
    root["source_version"] = SCRIPT_VERSION
    root["visual_only"] = True
    root["meters_per_unit"] = 1.0
    root["authoritative_collision"] = False
    collection.objects.link(root)
    return collection, root


def move_to_collection(
    obj: bpy.types.Object,
    collection: bpy.types.Collection,
) -> None:
    for linked_collection in tuple(obj.users_collection):
        linked_collection.objects.unlink(obj)
    collection.objects.link(obj)


def activate(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def assign_material(
    obj: bpy.types.Object,
    material: bpy.types.Material,
) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0


def apply_bevel(
    obj: bpy.types.Object,
    width: float,
    segments: int = 1,
) -> None:
    if width <= 0.0:
        return
    activate(obj)
    modifier = obj.modifiers.new(name="StoneEdgeBevel", type="BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(20.0)
    modifier.miter_outer = "MITER_ARC"
    modifier.harden_normals = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def polar(radius: float, angle: float) -> tuple[float, float]:
    return math.cos(angle) * radius, math.sin(angle) * radius


def deterministic_unit(*values: int) -> float:
    state = 2_166_136_261
    for value in values:
        state ^= value & 0xFFFFFFFF
        state = (state * 16_777_619) & 0xFFFFFFFF
    return state / 0xFFFFFFFF


def deterministic_signed(*values: int) -> float:
    return (deterministic_unit(*values) * 2.0) - 1.0


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def inspect_texture(
    project_root: Path,
    relative_path: Path,
    expected_size: int,
) -> dict[str, object]:
    path = project_root / relative_path
    if not path.is_file():
        raise RuntimeError(f"Arena-floor texture is missing: {path}")
    image = bpy.data.images.load(str(path), check_existing=False)
    width, height = image.size
    bpy.data.images.remove(image)
    if width != expected_size or height != expected_size:
        raise RuntimeError(
            f"Arena-floor texture {relative_path} is {width}x{height}; "
            f"expected {expected_size}x{expected_size}"
        )
    return {
        "path": str(relative_path),
        "width": width,
        "height": height,
        "sha256": sha256(path),
        "bytes": path.stat().st_size,
    }


def validate_macro_albedo(project_root: Path) -> dict[str, object]:
    source = inspect_texture(
        project_root,
        MACRO_SOURCE_RELATIVE_PATH,
        1254,
    )
    source["color_space"] = "sRGB"
    source["original_first_party"] = True
    runtime = inspect_texture(
        project_root,
        MACRO_RUNTIME_RELATIVE_PATH,
        1024,
    )
    runtime["color_space"] = "sRGB"
    runtime["role"] = "checked_in_mobile_derivative"
    return {
        "source": source,
        "runtime": runtime,
        "material_slot": MATERIAL_STONE,
        "usage": "single_full_floor_base_map",
        "overlay_mesh": False,
        "secondary_albedo_blend": False,
    }


def masonry_boundary_radius(boundary_index: int, angle: float) -> float:
    base_radius = MASONRY_BOUNDARIES[boundary_index]
    amplitude = MASONRY_BOUNDARY_AMPLITUDES[boundary_index]
    primary_frequency = 3 + ((boundary_index * 2) % 4)
    secondary_frequency = 7 + ((boundary_index * 3) % 5)
    primary_phase = deterministic_unit(boundary_index, 101) * math.tau
    secondary_phase = deterministic_unit(boundary_index, 211) * math.tau
    variation = (
        math.sin((angle * primary_frequency) + primary_phase) * 0.68 +
        math.sin((angle * secondary_frequency) + secondary_phase) * 0.32
    )
    return base_radius + (variation * amplitude)


def create_polygon_prism(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    points: Sequence[tuple[float, float]],
    top_z: float,
    bottom_z: float,
    material: bpy.types.Material,
    bevel: float,
) -> bpy.types.Object:
    if len(points) < 3:
        raise ValueError("A stone prism requires at least three points")
    count = len(points)
    vertices = [(x, y, top_z) for x, y in points]
    vertices.extend((x, y, bottom_z) for x, y in points)
    faces: list[list[int]] = [
        list(range(count)),
        list(reversed(range(count, count * 2))),
    ]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append(
            [index, next_index, count + next_index, count + index]
        )
    mesh = bpy.data.meshes.new(f"{name}_Source")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = root
    assign_material(obj, material)
    apply_bevel(obj, bevel)
    return obj


def create_cylinder(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    radius: float,
    top_z: float,
    bottom_z: float,
    segments: int,
    material: bpy.types.Material,
    bevel: float,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=segments,
        radius=radius,
        depth=top_z - bottom_z,
        location=(0.0, 0.0, (top_z + bottom_z) * 0.5),
    )
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    obj.parent = root
    assign_material(obj, material)
    apply_bevel(obj, bevel)
    return obj


def create_revolved_profile(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    profile: Sequence[tuple[float, float]],
    segments: int,
    material: bpy.types.Material,
) -> bpy.types.Object:
    if len(profile) < 3:
        raise ValueError("A closed revolved profile requires at least three points")
    vertices: list[tuple[float, float, float]] = []
    profile_count = len(profile)
    for segment in range(segments):
        angle = segment / segments * math.tau
        for radius, z in profile:
            x, y = polar(radius, angle)
            vertices.append((x, y, z))
    faces: list[list[int]] = []
    for segment in range(segments):
        next_segment = (segment + 1) % segments
        for profile_index in range(profile_count):
            next_profile = (profile_index + 1) % profile_count
            current = (segment * profile_count) + profile_index
            current_next = (segment * profile_count) + next_profile
            next_current = (next_segment * profile_count) + profile_index
            next_next = (next_segment * profile_count) + next_profile
            faces.append([current, next_current, next_next, current_next])
    mesh = bpy.data.meshes.new(f"{name}_Source")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = root
    assign_material(obj, material)
    return obj


def shrink_polygon(
    points: Sequence[tuple[float, float]],
    amount: float,
) -> list[tuple[float, float]]:
    center_x = sum(point[0] for point in points) / len(points)
    center_y = sum(point[1] for point in points) / len(points)
    result: list[tuple[float, float]] = []
    for x, y in points:
        direction = Vector((center_x - x, center_y - y))
        if direction.length > amount:
            direction.normalize()
            x += direction.x * amount
            y += direction.y * amount
        result.append((x, y))
    return result


def add_outer_edge_notch(
    points: Sequence[tuple[float, float]],
    depth: float,
) -> list[tuple[float, float]]:
    if len(points) != 4:
        raise ValueError("A flagstone edge notch requires four source corners")
    outer_start = Vector(points[1])
    outer_end = Vector(points[2])
    edge = outer_end - outer_start
    midpoint = outer_start + (edge * 0.5)
    inward = -midpoint.normalized() * depth
    return [
        points[0],
        points[1],
        tuple(outer_start + (edge * 0.47)),
        tuple(midpoint + inward),
        tuple(outer_start + (edge * 0.53)),
        points[2],
        points[3],
    ]


def build_flagstone_fields(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> int:
    crack_count = 0
    for ring_index, segment_count in enumerate(MASONRY_SEGMENTS):
        inner_radius = MASONRY_BOUNDARIES[ring_index]
        outer_radius = MASONRY_BOUNDARIES[ring_index + 1]
        average_radius = (inner_radius + outer_radius) * 0.5
        angle_step = math.tau / segment_count
        phase = MASONRY_PHASE_FRACTIONS[ring_index] * angle_step
        angular_gap = 0.060 / average_radius
        angle_offsets = [
            deterministic_signed(ring_index, boundary, 11) * angle_step * 0.115
            for boundary in range(segment_count)
        ]
        for segment in range(segment_count):
            following = (segment + 1) % segment_count
            start = (
                phase +
                (segment * angle_step) +
                angle_offsets[segment] +
                (angular_gap * 0.5)
            )
            end = (
                phase +
                ((segment + 1) * angle_step) +
                angle_offsets[following] -
                (angular_gap * 0.5)
            )
            inner_start = masonry_boundary_radius(ring_index, start) + 0.030
            inner_end = masonry_boundary_radius(ring_index, end) + 0.030
            outer_start = masonry_boundary_radius(ring_index + 1, start) - 0.030
            outer_end = masonry_boundary_radius(ring_index + 1, end) - 0.030
            points = [
                polar(inner_start, start),
                polar(outer_start, start),
                polar(outer_end, end),
                polar(inner_end, end),
            ]
            variation = int(
                deterministic_unit(ring_index, segment, 53) * 5.0
            )
            top_z = -0.010 - (variation * 0.0040)
            material = materials[MATERIAL_STONE]
            broken_edge = ((ring_index * 41) + (segment * 17)) % 23 == 4
            if broken_edge:
                crack_count += 1
                create_polygon_prism(
                    collection,
                    root,
                    f"Flagstone_{ring_index:02d}_{segment:03d}_Notched",
                    add_outer_edge_notch(points, 0.08),
                    top_z,
                    -0.110,
                    material,
                    0.020,
                )
            else:
                create_polygon_prism(
                    collection,
                    root,
                    f"Flagstone_{ring_index:02d}_{segment:03d}",
                    points,
                    top_z,
                    -0.110,
                    material,
                    0.030,
                )
    return crack_count


def create_inlaid_band(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    radius: float,
    half_width: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    return create_revolved_profile(
        collection,
        root,
        name,
        (
            (radius - half_width, -0.108),
            (radius + half_width, -0.108),
            (radius + half_width, -0.003),
            (radius - half_width, -0.003),
        ),
        56,
        material,
    )


def build_center_medallion(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> None:
    create_cylinder(
        collection,
        root,
        "Medallion_Underlay",
        2.46,
        -0.044,
        -0.112,
        48,
        materials[MATERIAL_STONE],
        0.01,
    )
    create_revolved_profile(
        collection,
        root,
        "Medallion_RecessedGroove",
        (
            (2.285, -0.108),
            (2.325, -0.108),
            (2.325, -0.022),
            (2.285, -0.022),
        ),
        48,
        materials[MATERIAL_GROUT],
    )
    create_revolved_profile(
        collection,
        root,
        "Medallion_CarvedBorder",
        (
            (2.33, -0.105),
            (2.46, -0.105),
            (2.46, -0.034),
            (2.43, -0.009),
            (2.36, -0.004),
            (2.33, -0.025),
        ),
        48,
        materials[MATERIAL_TRIM],
    )
    sector_count = 10
    for segment in range(sector_count):
        angle_step = math.tau / sector_count
        gap = 0.034
        boundary_offset = deterministic_signed(segment, 331) * angle_step * 0.06
        next_offset = deterministic_signed(
            (segment + 1) % sector_count,
            331,
        ) * angle_step * 0.06
        start = (segment * angle_step) + boundary_offset + gap
        end = ((segment + 1) * angle_step) + next_offset - gap
        points = [
            polar(0.48, start),
            polar(2.26, start),
            polar(2.26, end),
            polar(0.48, end),
        ]
        create_polygon_prism(
            collection,
            root,
            f"Medallion_Sector_{segment:02d}",
            points,
            -0.010 - ((segment % 3) * 0.003),
            -0.108,
            materials[MATERIAL_STONE],
            0.026,
        )
    create_cylinder(
        collection,
        root,
        "Medallion_Center",
        0.44,
        -0.004,
        -0.108,
        12,
        materials[MATERIAL_STONE],
        0.028,
    )


def build_arena_floor(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> int:
    create_cylinder(
        collection,
        root,
        "ArenaFloor_ShallowStoneBed",
        14.55,
        -0.035,
        -0.120,
        96,
        materials[MATERIAL_STONE],
        0.008,
    )
    create_revolved_profile(
        collection,
        root,
        "ArenaFloor_OuterStoneCourse",
        (
            (14.38, -0.120),
            (15.00, -0.120),
            (15.00, -0.060),
            (14.96, -0.016),
            (14.88, -0.008),
            (14.48, -0.010),
            (14.38, -0.046),
        ),
        96,
        materials[MATERIAL_STONE],
    )
    create_revolved_profile(
        collection,
        root,
        "ArenaFloor_OuterTrimBevel",
        (
            (14.88, -0.030),
            (15.00, -0.030),
            (15.00, -0.060),
            (14.965, -0.010),
            (14.93, 0.000),
            (14.88, -0.010),
        ),
        96,
        materials[MATERIAL_TRIM],
    )
    crack_count = build_flagstone_fields(
        collection,
        root,
        materials,
    )
    create_inlaid_band(
        collection,
        root,
        "ArenaFloor_InlayBand_Inner",
        5.16,
        0.020,
        materials[MATERIAL_TRIM],
    )
    create_inlaid_band(
        collection,
        root,
        "ArenaFloor_InlayBand_Middle",
        10.75,
        0.020,
        materials[MATERIAL_TRIM],
    )
    build_center_medallion(collection, root, materials)
    return crack_count


def recalculate_normals(mesh: bpy.types.Mesh) -> None:
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def unwrap_uv0(obj: bpy.types.Object) -> None:
    mesh = obj.data
    while len(mesh.uv_layers) > 0:
        mesh.uv_layers.remove(mesh.uv_layers[0])
    uv_layer = mesh.uv_layers.new(name="UV0")
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            loop = mesh.loops[loop_index]
            coordinate = mesh.vertices[loop.vertex_index].co
            u = (coordinate.x + ARENA_RADIUS) / UV_WORLD_SPAN_METERS
            v = (coordinate.y + ARENA_RADIUS) / UV_WORLD_SPAN_METERS
            uv_layer.data[loop_index].uv = (u, v)
    mesh.uv_layers.active = uv_layer
    mesh.update()


def validate_planar_uv0(mesh_obj: bpy.types.Object) -> dict[str, object]:
    mesh = mesh_obj.data
    uv_layer = mesh.uv_layers.get("UV0")
    if uv_layer is None:
        raise RuntimeError("ArenaFloorVisual UV0 is missing")
    minimum_u = math.inf
    minimum_v = math.inf
    maximum_u = -math.inf
    maximum_v = -math.inf
    maximum_error = 0.0
    for polygon in mesh.polygons:
        for loop_index in polygon.loop_indices:
            loop = mesh.loops[loop_index]
            coordinate = mesh.vertices[loop.vertex_index].co
            expected_u = (
                coordinate.x + ARENA_RADIUS
            ) / UV_WORLD_SPAN_METERS
            expected_v = (
                coordinate.y + ARENA_RADIUS
            ) / UV_WORLD_SPAN_METERS
            uv = uv_layer.data[loop_index].uv
            maximum_error = max(
                maximum_error,
                abs(uv.x - expected_u),
                abs(uv.y - expected_v),
            )
            minimum_u = min(minimum_u, uv.x)
            minimum_v = min(minimum_v, uv.y)
            maximum_u = max(maximum_u, uv.x)
            maximum_v = max(maximum_v, uv.y)
    if maximum_error > 0.000001:
        raise RuntimeError(
            "ArenaFloorVisual UV0 is not the exact 30 m planar mapping; "
            f"maximum error is {maximum_error:.8f}"
        )
    bounds = (minimum_u, minimum_v, maximum_u, maximum_v)
    expected_bounds = (0.0, 0.0, 1.0, 1.0)
    if any(
        not math.isclose(actual, expected, abs_tol=0.000001)
        for actual, expected in zip(bounds, expected_bounds, strict=True)
    ):
        raise RuntimeError(
            "ArenaFloorVisual UV0 bounds are "
            f"{tuple(round(value, 7) for value in bounds)}, expected "
            "(0, 0, 1, 1)"
        )
    return {
        "mapping": "arena_disk_world_planar",
        "world_x_meters": [-ARENA_RADIUS, ARENA_RADIUS],
        "world_y_meters": [-ARENA_RADIUS, ARENA_RADIUS],
        "uv_bounds": [0.0, 0.0, 1.0, 1.0],
        "maximum_mapping_error": round(maximum_error, 8),
    }


def normalize_material_slots(
    obj: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> None:
    old_names = [
        slot.material.name if slot.material else ""
        for slot in obj.material_slots
    ]
    material_lookup = {
        material_name: index
        for index, material_name in enumerate(MATERIAL_ORDER)
    }
    new_indices = [
        material_lookup[old_names[polygon.material_index]]
        for polygon in obj.data.polygons
    ]
    obj.data.materials.clear()
    for material_name in MATERIAL_ORDER:
        obj.data.materials.append(materials[material_name])
    for polygon, material_index in zip(
        obj.data.polygons,
        new_indices,
        strict=True,
    ):
        polygon.material_index = material_index


def join_and_finalize(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> bpy.types.Object:
    pieces = [obj for obj in collection.objects if obj.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for piece in pieces:
        piece.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    mesh_obj = bpy.context.object
    mesh_obj.name = f"{ASSET_NAME}_Mesh"
    mesh_obj.data.name = f"{ASSET_NAME}_MeshData"
    mesh_obj.parent = root
    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR", center="MEDIAN")
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    normalize_material_slots(mesh_obj, materials)
    recalculate_normals(mesh_obj.data)
    unwrap_uv0(mesh_obj)

    triangulate = mesh_obj.modifiers.new(
        name="ExportTriangulation",
        type="TRIANGULATE",
    )
    triangulate.quad_method = "BEAUTY"
    triangulate.ngon_method = "BEAUTY"
    bpy.ops.object.modifier_apply(modifier=triangulate.name)
    for polygon in mesh_obj.data.polygons:
        polygon.use_smooth = True
    weighted = mesh_obj.modifiers.new(
        name="ExportWeightedNormals",
        type="WEIGHTED_NORMAL",
    )
    weighted.keep_sharp = True
    weighted.weight = 50
    weighted.thresh = 0.01
    bpy.ops.object.modifier_apply(modifier=weighted.name)
    recalculate_normals(mesh_obj.data)
    mesh_obj.data.validate(verbose=True, clean_customdata=False)
    mesh_obj.data.update()
    mesh_obj["visual_only"] = True
    mesh_obj["authoritative_collision"] = False
    mesh_obj["material_contract"] = ";".join(
        f"{index}:{name}" for index, name in enumerate(MATERIAL_ORDER)
    )
    return mesh_obj


def world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector(
        (
            min(point.x for point in points),
            min(point.y for point in points),
            min(point.z for point in points),
        )
    )
    maximum = Vector(
        (
            max(point.x for point in points),
            max(point.y for point in points),
            max(point.z for point in points),
        )
    )
    return minimum, maximum


def mesh_is_closed(obj: bpy.types.Object) -> bool:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    closed = all(len(edge.link_faces) == 2 for edge in bm.edges)
    bm.free()
    return closed


def validate_asset(
    root: bpy.types.Object,
    mesh_obj: bpy.types.Object,
    crack_count: int,
) -> dict[str, object]:
    mesh_obj.data.calc_loop_triangles()
    triangles = len(mesh_obj.data.loop_triangles)
    vertices = len(mesh_obj.data.vertices)
    minimum, maximum = world_bounds(mesh_obj)
    maximum_radius = max(
        math.hypot(
            (mesh_obj.matrix_world @ vertex.co).x,
            (mesh_obj.matrix_world @ vertex.co).y,
        )
        for vertex in mesh_obj.data.vertices
    )
    material_names = [
        slot.material.name if slot.material else ""
        for slot in mesh_obj.material_slots
    ]
    uv_record = validate_planar_uv0(mesh_obj)
    failures: list[str] = []
    if triangles > MAXIMUM_TRIANGLES:
        failures.append(
            f"triangles {triangles} exceed {MAXIMUM_TRIANGLES}"
        )
    if triangles != EXPECTED_TRIANGLES:
        failures.append(
            f"triangles {triangles} changed from {EXPECTED_TRIANGLES}"
        )
    if sum(MASONRY_SEGMENTS) != EXPECTED_GEOMETRIC_FLAGSTONES:
        failures.append(
            "geometric flagstone count is "
            f"{sum(MASONRY_SEGMENTS)}, expected "
            f"{EXPECTED_GEOMETRIC_FLAGSTONES}"
        )
    if vertices >= MAXIMUM_VERTICES:
        failures.append(f"vertices {vertices} exceed 16-bit limit")
    if material_names != list(MATERIAL_ORDER):
        failures.append(f"material order is {material_names}")
    if mesh_obj.data.uv_layers.get("UV0") is None:
        failures.append("UV0 is missing")
    if any(len(polygon.vertices) != 3 for polygon in mesh_obj.data.polygons):
        failures.append("mesh is not fully triangulated")
    if not mesh_is_closed(mesh_obj):
        failures.append("mesh has open or non-manifold edges")
    if not math.isclose(maximum_radius, ARENA_RADIUS, abs_tol=0.001):
        failures.append(
            f"radius {maximum_radius:.4f} is not exactly {ARENA_RADIUS:.2f}m"
        )
    if maximum.z > 0.001 or maximum.z < -0.001:
        failures.append(f"visual top z is {maximum.z:.4f}, expected 0")
    if minimum.z < -0.121:
        failures.append(
            f"visual relief reaches {minimum.z:.4f}, below -0.120m"
        )
    if root.location.length > 0.0001:
        failures.append(f"root is not at origin: {tuple(root.location)}")
    if failures:
        raise RuntimeError(
            f"{ASSET_NAME} validation failed: {'; '.join(failures)}"
        )
    return {
        "asset": ASSET_NAME,
        "root_object": root.name,
        "mesh_object": mesh_obj.name,
        "triangles": triangles,
        "vertices": vertices,
        "submeshes": len(material_names),
        "material_slots": material_names,
        "uv_layers": [layer.name for layer in mesh_obj.data.uv_layers],
        "uv0_planar_contract": uv_record,
        "radius_meters": round(maximum_radius, 4),
        "bounds_min_blender_xyz": [round(value, 4) for value in minimum],
        "bounds_max_blender_xyz": [round(value, 4) for value in maximum],
        "vertical_extent_meters": round(maximum.z - minimum.z, 4),
        "split_flagstone_cracks": 0,
        "broken_flagstone_edges": crack_count,
        "ground_center_origin": True,
        "closed_manifold": True,
        "triangulated": True,
        "weighted_normals": True,
        "visual_only": True,
    }


def export_fbx(
    export_path: Path,
    collection: bpy.types.Collection,
    root: bpy.types.Object,
) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    for obj in collection.objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(export_path),
        check_existing=False,
        use_selection=True,
        global_scale=1.0,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_UNITS",
        use_space_transform=True,
        bake_space_transform=False,
        object_types={"EMPTY", "MESH"},
        use_mesh_modifiers=True,
        mesh_smooth_type="FACE",
        colors_type="SRGB",
        use_triangles=True,
        use_custom_props=True,
        add_leaf_bones=False,
        bake_anim=False,
        path_mode="STRIP",
        embed_textures=False,
        axis_forward="-Z",
        axis_up="Y",
    )
    if not export_path.is_file() or export_path.stat().st_size <= 0:
        raise RuntimeError(f"FBX export did not produce {export_path}")


def save_blend(path: Path) -> None:
    bpy.ops.wm.save_as_mainfile(
        filepath=str(path),
        check_existing=False,
        compress=True,
    )
    backup = path.with_name(f"{path.name}1")
    if backup.is_file():
        backup.unlink()


def add_area_light(
    collection: bpy.types.Collection,
    name: str,
    location: tuple[float, float, float],
    target: tuple[float, float, float],
    energy: float,
    size: float,
    color: tuple[float, float, float],
) -> None:
    bpy.ops.object.light_add(type="AREA", location=location)
    light = bpy.context.object
    light.name = name
    light.data.energy = energy
    light.data.shape = "DISK"
    light.data.size = size
    light.data.color = color
    light.rotation_euler = (
        Vector(target) - light.location
    ).to_track_quat("-Z", "Y").to_euler()
    move_to_collection(light, collection)


def add_gameplay_camera(
    collection: bpy.types.Collection,
    name: str,
    follow_position: tuple[float, float, float],
    yaw_degrees: float,
    pitch_degrees: float,
    distance: float,
    pivot_height: float,
    forward_focus_offset: float,
    right_focus_offset: float,
    vertical_fov_degrees: float,
) -> bpy.types.Object:
    yaw = math.radians(yaw_degrees)
    pitch = math.radians(pitch_degrees)
    planar_forward = Vector((math.sin(yaw), math.cos(yaw), 0.0))
    planar_right = Vector((math.cos(yaw), -math.sin(yaw), 0.0))
    target = (
        Vector(follow_position) +
        Vector((0.0, 0.0, pivot_height)) +
        (planar_forward * forward_focus_offset) +
        (planar_right * right_focus_offset)
    )
    view_forward = Vector(
        (
            planar_forward.x * math.cos(pitch),
            planar_forward.y * math.cos(pitch),
            -math.sin(pitch),
        )
    )
    location = target - (view_forward * distance)
    bpy.ops.object.camera_add(location=location)
    camera = bpy.context.object
    camera.name = name
    camera.rotation_euler = (
        Vector(target) - camera.location
    ).to_track_quat("-Z", "Y").to_euler()
    camera.data.sensor_fit = "VERTICAL"
    camera.data.angle = math.radians(vertical_fov_degrees)
    move_to_collection(camera, collection)
    bpy.context.scene.camera = camera
    return camera


def configure_preview_stone_surface(
    material: bpy.types.Material,
    macro_albedo_path: Path,
    macro_normal_path: Path,
    macro_packed_mask_path: Path,
    micro_normal_path: Path,
    blend_directory: Path,
) -> None:
    for texture_path in (
        macro_albedo_path,
        macro_normal_path,
        macro_packed_mask_path,
        micro_normal_path,
    ):
        if not texture_path.is_file():
            raise RuntimeError(
                f"Arena-floor preview texture is missing: {texture_path}"
            )
    macro_albedo_image = bpy.data.images.load(
        str(macro_albedo_path),
        check_existing=True,
    )
    macro_albedo_image.colorspace_settings.name = "sRGB"
    macro_normal_image = bpy.data.images.load(
        str(macro_normal_path),
        check_existing=True,
    )
    macro_normal_image.colorspace_settings.name = "Non-Color"
    macro_packed_mask_image = bpy.data.images.load(
        str(macro_packed_mask_path),
        check_existing=True,
    )
    macro_packed_mask_image.colorspace_settings.name = "Non-Color"
    micro_normal_image = bpy.data.images.load(
        str(micro_normal_path),
        check_existing=True,
    )
    micro_normal_image.colorspace_settings.name = "Non-Color"
    for image, path in (
        (macro_albedo_image, macro_albedo_path),
        (macro_normal_image, macro_normal_path),
        (macro_packed_mask_image, macro_packed_mask_path),
        (micro_normal_image, micro_normal_path),
    ):
        relative_path = os.path.relpath(path, blend_directory)
        image.filepath = "//" + Path(relative_path).as_posix()
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    if principled is None:
        raise RuntimeError("Arena-floor Stone material has no Principled BSDF")
    for link in tuple(links):
        if link.to_node == principled:
            links.remove(link)

    coordinates = nodes.new("ShaderNodeTexCoord")
    coordinates.name = "Preview_TextureCoordinates"
    macro_mapping = nodes.new("ShaderNodeMapping")
    macro_mapping.name = "Preview_MacroBaseMapTransform"
    macro_mapping.vector_type = "POINT"
    macro_mapping.inputs["Scale"].default_value = (
        MACRO_BASE_MAP_TILING,
        MACRO_BASE_MAP_TILING,
        1.0,
    )
    macro_mapping.inputs["Location"].default_value = (
        MACRO_BASE_MAP_OFFSET[0],
        MACRO_BASE_MAP_OFFSET[1],
        0.0,
    )
    micro_mapping = nodes.new("ShaderNodeMapping")
    micro_mapping.name = "Preview_MicroNormalMaskTransform"
    micro_mapping.vector_type = "POINT"
    micro_mapping.inputs["Scale"].default_value = (
        MICRO_DETAIL_TILING,
        MICRO_DETAIL_TILING,
        1.0,
    )
    micro_mapping.inputs["Location"].default_value = (
        MICRO_DETAIL_OFFSET[0],
        MICRO_DETAIL_OFFSET[1],
        0.0,
    )
    macro_albedo_texture = nodes.new("ShaderNodeTexImage")
    macro_albedo_texture.name = "Preview_ArenaFloorMacroAlbedo"
    macro_albedo_texture.image = macro_albedo_image
    macro_albedo_texture.interpolation = "Linear"
    macro_albedo_texture.extension = "REPEAT"
    macro_normal_texture = nodes.new("ShaderNodeTexImage")
    macro_normal_texture.name = "Preview_ArenaFloorMacroNormal"
    macro_normal_texture.image = macro_normal_image
    macro_normal_texture.interpolation = "Linear"
    macro_normal_texture.extension = "REPEAT"
    packed_mask_texture = nodes.new("ShaderNodeTexImage")
    packed_mask_texture.name = "Preview_ArenaFloorMacroMask"
    packed_mask_texture.image = macro_packed_mask_image
    packed_mask_texture.interpolation = "Linear"
    packed_mask_texture.extension = "REPEAT"
    micro_normal_texture = nodes.new("ShaderNodeTexImage")
    micro_normal_texture.name = "Preview_ArenaFloorSurfaceNormal"
    micro_normal_texture.image = micro_normal_image
    micro_normal_texture.interpolation = "Linear"
    micro_normal_texture.extension = "REPEAT"
    packed_channels = nodes.new("ShaderNodeSeparateColor")
    packed_channels.name = "Preview_SurfaceMaskChannels"
    tint = nodes.new("ShaderNodeMixRGB")
    tint.name = "Preview_UnityBaseColorMultiply"
    tint.blend_type = "MULTIPLY"
    tint.inputs[0].default_value = 1.0
    tint.inputs[2].default_value = (0.92, 0.96, 1.0, 1.0)
    ambient_occlusion = nodes.new("ShaderNodeMixRGB")
    ambient_occlusion.name = "Preview_SurfaceAmbientOcclusion"
    ambient_occlusion.blend_type = "MULTIPLY"
    ambient_occlusion.inputs[0].default_value = 0.72
    roughness = nodes.new("ShaderNodeMath")
    roughness.name = "Preview_SmoothnessToRoughness"
    roughness.operation = "SUBTRACT"
    roughness.inputs[0].default_value = 1.0
    macro_normal_map = nodes.new("ShaderNodeNormalMap")
    macro_normal_map.name = "Preview_MacroTangentNormal"
    macro_normal_map.inputs["Strength"].default_value = 0.58
    micro_normal_map = nodes.new("ShaderNodeNormalMap")
    micro_normal_map.name = "Preview_MicroTangentNormal"
    micro_normal_map.inputs["Strength"].default_value = 0.30
    geometry = nodes.new("ShaderNodeNewGeometry")
    geometry.name = "Preview_SurfaceGeometry"
    add_normals = nodes.new("ShaderNodeVectorMath")
    add_normals.name = "Preview_AddMacroAndMicroNormals"
    add_normals.operation = "ADD"
    remove_shared_surface_normal = nodes.new("ShaderNodeVectorMath")
    remove_shared_surface_normal.name = "Preview_RemoveSharedSurfaceNormal"
    remove_shared_surface_normal.operation = "SUBTRACT"
    normalize_normal = nodes.new("ShaderNodeVectorMath")
    normalize_normal.name = "Preview_NormalizeCombinedNormal"
    normalize_normal.operation = "NORMALIZE"
    links.new(coordinates.outputs["UV"], macro_mapping.inputs["Vector"])
    links.new(coordinates.outputs["UV"], micro_mapping.inputs["Vector"])
    links.new(
        macro_mapping.outputs["Vector"],
        macro_albedo_texture.inputs["Vector"],
    )
    for texture in (
        macro_normal_texture,
        packed_mask_texture,
    ):
        links.new(macro_mapping.outputs["Vector"], texture.inputs["Vector"])
    links.new(
        micro_mapping.outputs["Vector"],
        micro_normal_texture.inputs["Vector"],
    )
    links.new(macro_albedo_texture.outputs["Color"], tint.inputs[1])
    links.new(packed_mask_texture.outputs["Color"], packed_channels.inputs["Color"])
    links.new(tint.outputs["Color"], ambient_occlusion.inputs[1])
    links.new(packed_channels.outputs["Green"], ambient_occlusion.inputs[2])
    links.new(ambient_occlusion.outputs["Color"], principled.inputs["Base Color"])
    links.new(packed_mask_texture.outputs["Alpha"], roughness.inputs[1])
    links.new(roughness.outputs["Value"], principled.inputs["Roughness"])
    links.new(packed_channels.outputs["Red"], principled.inputs["Metallic"])
    links.new(
        macro_normal_texture.outputs["Color"],
        macro_normal_map.inputs["Color"],
    )
    links.new(
        micro_normal_texture.outputs["Color"],
        micro_normal_map.inputs["Color"],
    )
    links.new(
        macro_normal_map.outputs["Normal"],
        add_normals.inputs[0],
    )
    links.new(
        micro_normal_map.outputs["Normal"],
        add_normals.inputs[1],
    )
    links.new(
        add_normals.outputs["Vector"],
        remove_shared_surface_normal.inputs[0],
    )
    links.new(
        geometry.outputs["Normal"],
        remove_shared_surface_normal.inputs[1],
    )
    links.new(
        remove_shared_surface_normal.outputs["Vector"],
        normalize_normal.inputs[0],
    )
    links.new(
        normalize_normal.outputs["Vector"],
        principled.inputs["Normal"],
    )


def desaturate_preview_material(material: bpy.types.Material) -> None:
    nodes = material.node_tree.nodes
    links = material.node_tree.links
    principled = nodes.get("Principled BSDF")
    if principled is None:
        return
    base_color = principled.inputs["Base Color"]
    incoming = next(
        (link for link in links if link.to_socket == base_color),
        None,
    )
    if incoming is not None:
        source_socket = incoming.from_socket
        links.remove(incoming)
        grayscale = nodes.new("ShaderNodeRGBToBW")
        grayscale.name = "Evidence_DesaturatedBaseColor"
        links.new(source_socket, grayscale.inputs["Color"])
        links.new(grayscale.outputs["Val"], base_color)
        return
    color = base_color.default_value
    luminance = (
        (color[0] * 0.2126) +
        (color[1] * 0.7152) +
        (color[2] * 0.0722)
    )
    base_color.default_value = (luminance, luminance, luminance, color[3])


def render_evidence(
    evidence_dir: Path,
    source_collection: bpy.types.Collection,
    materials: dict[str, bpy.types.Material],
) -> tuple[Path, Path, Path]:
    preview = bpy.data.collections.new("ArenaFloor_PreviewOnly")
    bpy.context.scene.collection.children.link(preview)
    source_collection.hide_render = False
    source_collection.hide_viewport = False

    lava = create_material(
        "ArenaFloor_PreviewLava",
        (0.18, 0.012, 0.002, 1.0),
        0.02,
        0.50,
    )
    lava_node = lava.node_tree.nodes.get("Principled BSDF")
    lava_node.inputs["Emission Color"].default_value = (0.70, 0.022, 0.001, 1.0)
    lava_node.inputs["Emission Strength"].default_value = 0.42
    preview_root = bpy.data.objects.new("ArenaFloorPreviewRoot", None)
    preview.objects.link(preview_root)
    create_cylinder(
        preview,
        preview_root,
        "PreviewLava",
        18.0,
        -0.125,
        -0.22,
        96,
        lava,
        0.01,
    )

    light_target = (2.3, 0.0, 0.0)
    add_area_light(preview, "Floor_Key", (-10.0, -12.0, 18.0), light_target, 2300.0, 12.0, (0.56, 0.68, 1.0))
    add_area_light(preview, "Floor_Fill", (12.0, 3.0, 15.0), light_target, 1750.0, 12.0, (0.36, 0.50, 0.82))
    add_area_light(preview, "Floor_Rim", (2.0, 15.0, 10.0), light_target, 1300.0, 10.0, (1.0, 0.24, 0.045))

    scene = bpy.context.scene
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"

    target_camera_path = evidence_dir / "ArenaFloorTargetCamera.png"
    perspective = add_gameplay_camera(
        preview,
        "ArenaFloor_TargetCamera",
        (0.0, 0.0, 0.0),
        90.0,
        40.0,
        18.0,
        0.9,
        2.3,
        0.0,
        46.0,
    )
    scene.render.filepath = str(target_camera_path)
    bpy.ops.render.render(write_still=True)

    top_down_path = evidence_dir / "ArenaFloorTopDown.png"
    perspective.hide_render = True
    bpy.ops.object.camera_add(location=(0.0, 0.0, 32.0))
    top_camera = bpy.context.object
    top_camera.name = "ArenaFloor_TopDownCamera"
    top_camera.rotation_euler = (0.0, 0.0, 0.0)
    top_camera.rotation_euler = (
        Vector((0.0, 0.0, 0.0)) - top_camera.location
    ).to_track_quat("-Z", "Y").to_euler()
    top_camera.data.type = "ORTHO"
    top_camera.data.ortho_scale = 31.4
    move_to_collection(top_camera, preview)
    scene.camera = top_camera
    scene.render.filepath = str(top_down_path)
    bpy.ops.render.render(write_still=True)

    desaturated_path = evidence_dir / "ArenaFloorDesaturatedTargetCamera.png"
    for material in (
        materials[MATERIAL_STONE],
        materials[MATERIAL_TRIM],
        materials[MATERIAL_GROUT],
        lava,
    ):
        desaturate_preview_material(material)
    for obj in preview.objects:
        if obj.type == "LIGHT":
            obj.data.color = (1.0, 1.0, 1.0)
    lava_node.inputs["Emission Color"].default_value = (0.12, 0.12, 0.12, 1.0)
    perspective.hide_render = False
    scene.camera = perspective
    scene.render.filepath = str(desaturated_path)
    bpy.ops.render.render(write_still=True)

    for obj in tuple(preview.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(preview)
    bpy.data.materials.remove(lava)
    return (
        target_camera_path,
        top_down_path,
        desaturated_path,
    )


def main() -> None:
    args = parse_args()
    if bpy.app.version < BLENDER_MINIMUM:
        raise RuntimeError(
            f"Blender 5.2 LTS or newer is required; found {bpy.app.version_string}"
        )
    project_root = args.project_root.resolve()
    export_dir = (
        project_root / "Assets" / "SessionRpg" / "SourceArt" / "Environment"
    )
    blend_dir = project_root / "SourceArt" / "Blender" / "Environment"
    evidence_dir = (
        project_root.parent / "outputs" / "art" / "blender" / "arena-floor"
    )
    export_dir.mkdir(parents=True, exist_ok=True)
    blend_dir.mkdir(parents=True, exist_ok=True)
    evidence_dir.mkdir(parents=True, exist_ok=True)

    surface_maps = build_surface_maps(
        project_root,
        micro_tiling=MICRO_DETAIL_TILING,
        micro_offset=MICRO_DETAIL_OFFSET,
    )
    surface_maps["scope"] = "macro_and_micro_first_party_derivatives"
    surface_maps["surface_synthesis"]["macro_pattern"] = (
        "separate_single_mapped_ArenaFloorMacroAlbedo"
    )
    surface_maps["runtime_usage"] = {
        "albedo": "not_bound_macro_base_map_replaces_it",
        "normal": "repeated_microdetail",
        "packed_mask": "diagnostic_micro_source_for_phase_bake",
        "macro_normal": "single_full_floor_base_normal",
        "macro_packed_mask": "single_full_floor_base_pbr_mask",
    }
    macro_albedo = validate_macro_albedo(project_root)
    reset_scene()
    materials = create_materials()
    collection, root = create_asset_collection()
    crack_count = build_arena_floor(collection, root, materials)
    mesh_obj = join_and_finalize(collection, root, materials)
    record = validate_asset(root, mesh_obj, crack_count)

    export_path = export_dir / f"{ASSET_NAME}.fbx"
    export_fbx(export_path, collection, root)
    record["fbx"] = str(export_path.relative_to(project_root))
    record["fbx_bytes"] = export_path.stat().st_size

    blend_path = blend_dir / "ArenaFloor.blend"
    texture_directory = (
        project_root / "Assets/SessionRpg/Art/Environment/Textures"
    )
    configure_preview_stone_surface(
        materials[MATERIAL_STONE],
        project_root / MACRO_RUNTIME_RELATIVE_PATH,
        texture_directory / MACRO_NORMAL_FILENAME,
        texture_directory / MACRO_MASK_FILENAME,
        texture_directory / NORMAL_FILENAME,
        blend_path.parent,
    )
    save_blend(blend_path)

    target_camera_path: Path | None = None
    top_down_path: Path | None = None
    desaturated_path: Path | None = None
    if not args.skip_preview:
        (
            target_camera_path,
            top_down_path,
            desaturated_path,
        ) = render_evidence(
            evidence_dir,
            collection,
            materials,
        )

    manifest = {
        "schema": 1,
        "generator": "Tools/Art/Blender/build_arena_floor.py",
        "generator_version": SCRIPT_VERSION,
        "blender_version": bpy.app.version_string,
        "original_project_asset": True,
        "external_mesh_or_texture_sources": [],
        "units": "meters",
        "source_blend": str(blend_path.relative_to(project_root)),
        "target_camera_preview": str(
            target_camera_path.relative_to(project_root.parent)
        )
        if target_camera_path is not None and target_camera_path.is_file()
        else None,
        "top_down_preview": str(top_down_path.relative_to(project_root.parent))
        if top_down_path is not None and top_down_path.is_file()
        else None,
        "desaturated_target_camera_preview": str(
            desaturated_path.relative_to(project_root.parent)
        )
        if desaturated_path is not None and desaturated_path.is_file()
        else None,
        "export_axis": {"forward": "-Z", "up": "+Y"},
        "material_contract": list(MATERIAL_ORDER),
        "uv_contract": {
            "mapping": "arena_disk_world_planar",
            "world_x_meters": [-ARENA_RADIUS, ARENA_RADIUS],
            "world_y_meters": [-ARENA_RADIUS, ARENA_RADIUS],
            "uv_bounds": [0.0, 0.0, 1.0, 1.0],
            "u_formula": "(world_x + 15) / 30",
            "v_formula": "(world_y + 15) / 30",
            "meters_per_uv_span": UV_WORLD_SPAN_METERS,
            "macro_base_map_tiling": [
                MACRO_BASE_MAP_TILING,
                MACRO_BASE_MAP_TILING,
            ],
            "macro_base_map_offset": list(MACRO_BASE_MAP_OFFSET),
            "micro_normal_mask_tiling": [
                MICRO_DETAIL_TILING,
                MICRO_DETAIL_TILING,
            ],
            "micro_normal_mask_offset": list(MICRO_DETAIL_OFFSET),
            "micro_effective_repeat_meters": round(
                MICRO_DETAIL_REPEAT_METERS,
                4,
            ),
            "microdetail_feature_range_meters": [0.03, 0.55],
        },
        "macro_albedo": macro_albedo,
        "surface_maps": surface_maps,
        "gameplay_camera_contract": {
            "yaw_degrees": 90.0,
            "pitch_degrees": 40.0,
            "distance_meters": 18.0,
            "pivot_height_meters": 0.9,
            "forward_focus_offset_meters": 2.3,
            "right_focus_offset_meters": 0.0,
            "vertical_fov_degrees": 46.0,
            "render_aspect_ratio": [16, 9],
        },
        "design_contract": {
            "radius_meters": ARENA_RADIUS,
            "visual_top_meters": 0.0,
            "maximum_vertical_relief_meters": 0.12,
            "authoritative_collision": False,
            "raised_playable_ledges": False,
            "emissive_or_debug_rings": False,
            "field_flagstone_material": MATERIAL_STONE,
            "masonry_band_count": len(MASONRY_SEGMENTS),
            "geometric_flagstone_count": EXPECTED_GEOMETRIC_FLAGSTONES,
            "trim_ring_count": 2,
            "trim_ring_width_meters": 0.04,
            "medallion_trim_width_meters": 0.13,
            "outer_trim_width_meters": 0.12,
            "trim_usage": "restrained_medallion_two_pinstripes_outer_pinstripe",
            "macro_albedo_overlay_mesh": False,
            "authoritative_collision_source": "unity_only",
        },
        "asset": record,
    }
    manifest_path = export_dir / f"{ASSET_NAME}.manifest.json"
    manifest_path.write_text(
        json.dumps(manifest, indent=2) + "\n",
        encoding="utf-8",
    )
    print("SESSION_RPG_ARENA_FLOOR_BUILD_OK")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
