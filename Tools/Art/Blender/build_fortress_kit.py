#!/usr/bin/env python3
"""Build the original Session RPG fortress kit with Blender 5.2 LTS.

Run with Blender, not the system Python:

    /Applications/Blender.app/Contents/MacOS/Blender \
        --background --factory-startup --python-exit-code 1 \
        --python Tools/Art/Blender/build_fortress_kit.py

The build is deliberately procedural and deterministic. It creates one visual-only,
triangulated mesh per FBX, a two-slot material contract (Stone, then Ember), UV0,
weighted split normals, a machine-readable audit manifest, and the editable .blend
source outside Unity's Assets directory.
"""

from __future__ import annotations

import argparse
import json
import math
import random
import sys
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Iterable, Sequence

import bmesh
import bpy
from mathutils import Vector


SCRIPT_VERSION = "1.4.0"
BLENDER_MINIMUM = (5, 2, 0)
STONE_SLOT = "Fortress_Stone"
EMBER_SLOT = "Fortress_Ember"
FORTRESS_BAY_WIDTH = 7.72
TOWER_OUTWARD_PROJECTION = 0.95
FOUNDATION_ASSET_NAME = "FortressFoundationRing"
FOUNDATION_BAY_COUNT = 18
FOUNDATION_BAY_ANGLE_OFFSET = 10.0
FOUNDATION_INNER_RADIUS = 21.30
FOUNDATION_OUTER_RADIUS = 25.45
FOUNDATION_TOP_HEIGHT = -0.45
FOUNDATION_BOTTOM_HEIGHT = -1.80


@dataclass(frozen=True)
class AssetSpec:
    name: str
    max_triangles: int
    material_slots: tuple[str, ...]
    expected_size: tuple[float, float, float]
    builder: Callable[[bpy.types.Collection, bpy.types.Object, dict[str, bpy.types.Material]], None]


def parse_args() -> argparse.Namespace:
    script_args: list[str] = []
    if "--" in sys.argv:
        script_args = sys.argv[sys.argv.index("--") + 1 :]

    default_root = Path(__file__).resolve().parents[3]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--project-root", type=Path, default=default_root)
    parser.add_argument(
        "--skip-preview",
        action="store_true",
        help="Skip the disposable contact-sheet render outside Assets.",
    )
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
    scene.world.color = (0.018, 0.025, 0.042)
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs["Color"].default_value = (0.018, 0.025, 0.042, 1.0)
    background.inputs["Strength"].default_value = 0.30


def create_material(name: str, color: tuple[float, float, float, float], metallic: float, roughness: float) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = color
    material.use_nodes = True
    node = material.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value = color
    node.inputs["Metallic"].default_value = metallic
    node.inputs["Roughness"].default_value = roughness
    if name == EMBER_SLOT:
        node.inputs["Emission Color"].default_value = (1.0, 0.055, 0.004, 1.0)
        node.inputs["Emission Strength"].default_value = 2.6
    return material


def create_materials() -> dict[str, bpy.types.Material]:
    return {
        STONE_SLOT: create_material(STONE_SLOT, (0.11, 0.15, 0.23, 1.0), 0.05, 0.78),
        EMBER_SLOT: create_material(EMBER_SLOT, (0.75, 0.035, 0.006, 1.0), 0.15, 0.32),
    }


def create_asset_collection(name: str) -> tuple[bpy.types.Collection, bpy.types.Object]:
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    root = bpy.data.objects.new(name, None)
    root.empty_display_type = "PLAIN_AXES"
    root.empty_display_size = 0.45
    root["asset_id"] = name
    root["authoring_pipeline"] = "SessionRpg deterministic Blender fortress kit"
    root["source_version"] = SCRIPT_VERSION
    root["visual_only"] = True
    root["meters_per_unit"] = 1.0
    collection.objects.link(root)
    return collection, root


def move_to_collection(obj: bpy.types.Object, collection: bpy.types.Collection) -> None:
    for linked_collection in tuple(obj.users_collection):
        linked_collection.objects.unlink(obj)
    collection.objects.link(obj)


def activate(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def assign_single_material(obj: bpy.types.Object, material: bpy.types.Material) -> None:
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for polygon in obj.data.polygons:
        polygon.material_index = 0


def apply_bevel(obj: bpy.types.Object, width: float, segments: int = 1) -> None:
    if width <= 0.0:
        return
    activate(obj)
    modifier = obj.modifiers.new(name="StructuralBevel", type="BEVEL")
    modifier.width = width
    modifier.segments = segments
    modifier.limit_method = "ANGLE"
    modifier.angle_limit = math.radians(24.0)
    modifier.miter_outer = "MITER_ARC"
    modifier.harden_normals = True
    bpy.ops.object.modifier_apply(modifier=modifier.name)


def add_box(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    location: tuple[float, float, float],
    dimensions: tuple[float, float, float],
    material: bpy.types.Material,
    bevel: float = 0.08,
    rotation: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cube_add(location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    move_to_collection(obj, collection)
    obj.parent = root
    assign_single_material(obj, material)
    apply_bevel(obj, bevel)
    return obj


def add_cylinder(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    location: tuple[float, float, float],
    radius: float,
    depth: float,
    vertices: int,
    material: bpy.types.Material,
    bevel: float = 0.06,
    rotation_z: float = 0.0,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=vertices,
        radius=radius,
        depth=depth,
        location=location,
        rotation=(0.0, 0.0, rotation_z),
    )
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    obj.parent = root
    assign_single_material(obj, material)
    apply_bevel(obj, bevel)
    return obj


def add_cone(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    location: tuple[float, float, float],
    radius_bottom: float,
    radius_top: float,
    depth: float,
    vertices: int,
    material: bpy.types.Material,
    bevel: float = 0.05,
    rotation_z: float = 0.0,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_cone_add(
        vertices=vertices,
        radius1=radius_bottom,
        radius2=radius_top,
        depth=depth,
        location=location,
        rotation=(0.0, 0.0, rotation_z),
    )
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    obj.parent = root
    assign_single_material(obj, material)
    apply_bevel(obj, bevel)
    return obj


def add_torus(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    location: tuple[float, float, float],
    major_radius: float,
    minor_radius: float,
    material: bpy.types.Material,
    major_segments: int = 16,
    minor_segments: int = 4,
) -> bpy.types.Object:
    bpy.ops.mesh.primitive_torus_add(
        align="WORLD",
        major_segments=major_segments,
        minor_segments=minor_segments,
        location=location,
        major_radius=major_radius,
        minor_radius=minor_radius,
    )
    obj = bpy.context.object
    obj.name = name
    move_to_collection(obj, collection)
    obj.parent = root
    assign_single_material(obj, material)
    return obj


def add_profile_prism(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    profile: Sequence[tuple[float, float]],
    depth: float,
    material: bpy.types.Material,
    bevel: float = 0.07,
) -> bpy.types.Object:
    half_depth = depth * 0.5
    count = len(profile)
    vertices = [(x, -half_depth, z) for x, z in profile]
    vertices.extend((x, half_depth, z) for x, z in profile)
    faces: list[list[int]] = [list(range(count)), list(reversed(range(count, count * 2)))]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append([index, next_index, count + next_index, count + index])

    mesh = bpy.data.meshes.new(f"{name}_MeshSource")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = root
    assign_single_material(obj, material)
    apply_bevel(obj, bevel)
    return obj


def add_yz_profile_prism(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    profile: Sequence[tuple[float, float]],
    width: float,
    location_x: float,
    material: bpy.types.Material,
    bevel: float = 0.06,
) -> bpy.types.Object:
    """Extrude a Y/Z architectural profile across X.

    Wall buttresses need a sloped load-bearing silhouette, not two stacked boxes.
    Keeping the profile as one closed solid also spends fewer triangles on hidden
    overlapping faces.
    """

    half_width = width * 0.5
    count = len(profile)
    vertices = [
        (location_x - half_width, y, z)
        for y, z in profile
    ]
    vertices.extend(
        (location_x + half_width, y, z)
        for y, z in profile
    )
    faces: list[list[int]] = [
        list(reversed(range(count))),
        list(range(count, count * 2)),
    ]
    for index in range(count):
        next_index = (index + 1) % count
        faces.append(
            [
                index,
                count + index,
                count + next_index,
                next_index,
            ]
        )

    mesh = bpy.data.meshes.new(f"{name}_MeshSource")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = root
    assign_single_material(obj, material)
    apply_bevel(obj, bevel)
    return obj


def add_arch_frame(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    center_x: float,
    bottom_height: float,
    spring_height: float,
    outer_radius: float,
    frame_width: float,
    front_y: float,
    depth: float,
    material: bpy.types.Material,
    arc_segments: int = 7,
    front_bevel: float = 0.035,
) -> bpy.types.Object:
    """Create one closed, shallow arched masonry frame.

    The front rim, inset bevel and buried back rim are authored directly instead
    of assembling many rounded boxes. This yields a legible arch and shadow pocket
    for substantially fewer triangles than the old rectangular panel trim.
    """

    if arc_segments < 3:
        raise ValueError("An architectural arch requires at least three segments")
    inner_radius = outer_radius - frame_width
    if inner_radius <= front_bevel:
        raise ValueError("Arch frame is too narrow for its inset bevel")

    def profile_pairs(inset: float) -> list[tuple[tuple[float, float], tuple[float, float]]]:
        outer = outer_radius - inset
        inner = inner_radius + inset
        bottom = bottom_height + inset
        pairs = [
            (
                (center_x - outer, bottom),
                (center_x - inner, bottom),
            )
        ]
        for segment in range(arc_segments + 1):
            angle = math.pi - (segment / arc_segments * math.pi)
            sine = math.sin(angle)
            cosine = math.cos(angle)
            pairs.append(
                (
                    (
                        center_x + (cosine * outer),
                        spring_height + (sine * outer),
                    ),
                    (
                        center_x + (cosine * inner),
                        spring_height + (sine * inner),
                    ),
                )
            )
        pairs.append(
            (
                (center_x + outer, bottom),
                (center_x + inner, bottom),
            )
        )
        return pairs

    layer_profiles = (
        profile_pairs(0.0),
        profile_pairs(front_bevel),
        profile_pairs(front_bevel),
    )
    layer_y = (
        front_y,
        front_y + front_bevel,
        front_y + depth,
    )
    pair_count = len(layer_profiles[0])
    vertices: list[tuple[float, float, float]] = []
    for y, pairs in zip(layer_y, layer_profiles, strict=True):
        for outer_point, inner_point in pairs:
            vertices.append((outer_point[0], y, outer_point[1]))
            vertices.append((inner_point[0], y, inner_point[1]))

    def vertex_index(layer: int, pair: int, inner: bool) -> int:
        return (layer * pair_count * 2) + (pair * 2) + int(inner)

    faces: list[tuple[int, int, int, int]] = []
    last_layer = len(layer_y) - 1
    for pair in range(pair_count - 1):
        next_pair = pair + 1
        faces.append(
            (
                vertex_index(0, pair, False),
                vertex_index(0, next_pair, False),
                vertex_index(0, next_pair, True),
                vertex_index(0, pair, True),
            )
        )
        faces.append(
            (
                vertex_index(last_layer, pair, True),
                vertex_index(last_layer, next_pair, True),
                vertex_index(last_layer, next_pair, False),
                vertex_index(last_layer, pair, False),
            )
        )

    for layer in range(last_layer):
        next_layer = layer + 1
        for pair in range(pair_count - 1):
            next_pair = pair + 1
            faces.append(
                (
                    vertex_index(layer, pair, False),
                    vertex_index(next_layer, pair, False),
                    vertex_index(next_layer, next_pair, False),
                    vertex_index(layer, next_pair, False),
                )
            )
            faces.append(
                (
                    vertex_index(layer, next_pair, True),
                    vertex_index(next_layer, next_pair, True),
                    vertex_index(next_layer, pair, True),
                    vertex_index(layer, pair, True),
                )
            )
        for end_pair in (0, pair_count - 1):
            faces.append(
                (
                    vertex_index(layer, end_pair, True),
                    vertex_index(next_layer, end_pair, True),
                    vertex_index(next_layer, end_pair, False),
                    vertex_index(layer, end_pair, False),
                )
            )

    mesh = bpy.data.meshes.new(f"{name}_MeshSource")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = root
    assign_single_material(obj, material)
    return obj


def add_wall_buttress(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    center_x: float,
    material: bpy.types.Material,
    top_height: float = 2.68,
    width: float = 0.44,
) -> bpy.types.Object:
    # The profile is inset by the modifier width so the bevel remains inside the
    # exact 1.50m wall depth and ground-plane export bounds.
    profile_top = top_height - 0.05
    shoulder_height = max(0.88, profile_top - 0.34)
    profile = (
        (-0.60, profile_top),
        (0.55, profile_top),
        (0.59, shoulder_height),
        (0.67, 0.52),
        (0.69, 0.05),
        (-0.69, 0.05),
        (-0.69, 0.52),
        (-0.60, shoulder_height),
    )
    return add_yz_profile_prism(
        collection,
        root,
        name,
        profile,
        width,
        center_x,
        material,
        bevel=0.05,
    )


def add_radial_tower_buttress(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    center: tuple[float, float],
    angle: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    """Add one economical tapered buttress around the tower shaft."""

    profile = (
        (1.49, 0.18),
        (2.02, 0.18),
        (2.02, 0.56),
        (1.78, 3.18),
        (1.52, 3.18),
    )
    obj = add_profile_prism(
        collection,
        root,
        name,
        profile,
        0.42,
        material,
        bevel=0.018,
    )
    obj.location = (center[0], center[1], 0.0)
    obj.rotation_euler.z = angle
    return obj


def add_revolved_ring_profile(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    radial_height_profile: Sequence[tuple[float, float]],
    segments: int,
    material: bpy.types.Material,
) -> bpy.types.Object:
    """Create one closed annular solid by revolving a radial/height profile."""

    profile_count = len(radial_height_profile)
    if profile_count < 4:
        raise ValueError("A closed ring profile requires at least four points")
    vertices: list[tuple[float, float, float]] = []
    for segment in range(segments):
        angle = segment / segments * math.tau
        sine = math.sin(angle)
        cosine = math.cos(angle)
        for radius, height in radial_height_profile:
            vertices.append((sine * radius, cosine * radius, height))

    faces: list[tuple[int, int, int, int]] = []
    for segment in range(segments):
        next_segment = (segment + 1) % segments
        for profile_index in range(profile_count):
            next_profile = (profile_index + 1) % profile_count
            faces.append(
                (
                    (segment * profile_count) + profile_index,
                    (next_segment * profile_count) + profile_index,
                    (next_segment * profile_count) + next_profile,
                    (segment * profile_count) + next_profile,
                )
            )

    mesh = bpy.data.meshes.new(f"{name}_MeshSource")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = root
    assign_single_material(obj, material)
    return obj


def add_annular_sector_block(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    center_angle_degrees: float,
    half_angle_degrees: float,
    inner_radius: float,
    outer_radius: float,
    bottom_height: float,
    inner_top_height: float,
    outer_top_height: float,
    material: bpy.types.Material,
    bevel: float,
) -> bpy.types.Object:
    """Create a closed, subtly sloped annular masonry block."""

    start_angle = math.radians(center_angle_degrees - half_angle_degrees)
    end_angle = math.radians(center_angle_degrees + half_angle_degrees)

    def polar(radius: float, angle: float, height: float) -> tuple[float, float, float]:
        return (
            math.sin(angle) * radius,
            math.cos(angle) * radius,
            height,
        )

    vertices = [
        polar(inner_radius, start_angle, bottom_height),
        polar(outer_radius, start_angle, bottom_height),
        polar(outer_radius, end_angle, bottom_height),
        polar(inner_radius, end_angle, bottom_height),
        polar(inner_radius, start_angle, inner_top_height),
        polar(outer_radius, start_angle, outer_top_height),
        polar(outer_radius, end_angle, outer_top_height),
        polar(inner_radius, end_angle, inner_top_height),
    ]
    faces = [
        (3, 2, 1, 0),
        (4, 5, 6, 7),
        (0, 1, 5, 4),
        (1, 2, 6, 5),
        (2, 3, 7, 6),
        (3, 0, 4, 7),
    ]
    mesh = bpy.data.meshes.new(f"{name}_MeshSource")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = root
    assign_single_material(obj, material)
    apply_bevel(obj, bevel)
    return obj


def add_flame_crystal(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    location: tuple[float, float, float],
    radius: float,
    height: float,
    material: bpy.types.Material,
) -> bpy.types.Object:
    x, y, z = location
    bottom = z - height * 0.5
    middle = bottom + height * 0.52
    top = z + height * 0.5
    vertices = [
        (x - radius, y, bottom),
        (x, y - radius, bottom),
        (x + radius, y, bottom),
        (x, y + radius, bottom),
        (x - radius * 0.62, y + radius * 0.18, middle),
        (x + radius * 0.18, y - radius * 0.62, middle),
        (x + radius * 0.62, y + radius * 0.18, middle),
        (x + radius * 0.18, y + radius * 0.62, middle),
        (x - radius * 0.22, y + radius * 0.08, top),
    ]
    faces = [
        (0, 1, 2, 3),
        (0, 4, 5, 1),
        (1, 5, 6, 2),
        (2, 6, 7, 3),
        (3, 7, 4, 0),
        (4, 8, 5),
        (5, 8, 6),
        (6, 8, 7),
        (7, 8, 4),
    ]
    mesh = bpy.data.meshes.new(f"{name}_MeshSource")
    mesh.from_pydata(vertices, [], faces)
    mesh.validate(verbose=True)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    obj.parent = root
    assign_single_material(obj, material)
    return obj


def add_inlay_bar(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    name: str,
    center_x: float,
    center_z: float,
    length: float,
    angle_degrees: float,
    front_y: float,
    material: bpy.types.Material,
    thickness: float = 0.09,
) -> bpy.types.Object:
    return add_box(
        collection,
        root,
        name,
        (center_x, front_y, center_z),
        (length, 0.08, thickness),
        material,
        bevel=0.025,
        rotation=(0.0, math.radians(-angle_degrees), 0.0),
    )


def build_wall_straight(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> None:
    stone = materials[STONE_SLOT]
    add_box(collection, root, "WallStraight_SolidBody", (0.0, 0.0, 1.42), (FORTRESS_BAY_WIDTH, 1.10, 2.54), stone, 0.10)
    add_box(collection, root, "WallStraight_Foundation", (0.0, 0.0, 0.20), (FORTRESS_BAY_WIDTH, 1.48, 0.40), stone, 0.09)
    add_box(collection, root, "WallStraight_CrownBand", (0.0, 0.0, 2.59), (FORTRESS_BAY_WIDTH, 1.28, 0.28), stone, 0.07)

    for index, x in enumerate((-3.45, 0.0, 3.45)):
        add_wall_buttress(
            collection,
            root,
            f"WallStraight_TaperedButtress_{index}",
            x,
            stone,
        )

    for index, x in enumerate((-3.30, -2.20, -1.10, 0.0, 1.10, 2.20, 3.30)):
        add_box(collection, root, f"WallStraight_Merlon_{index}", (x, 0.0, 2.95), (0.56, 1.22, 0.50), stone, 0.07)

    # Two broad arched recesses replace the old repeated rectangular trim. Their
    # authored front chamfers catch light while the solid wall becomes a recessed
    # shadow plane; the opening remains decorative and collision-free.
    for arch_index, center_x in enumerate((-1.72, 1.72)):
        add_arch_frame(
            collection,
            root,
            f"WallStraight_ArchFrame_{arch_index}",
            center_x,
            0.58,
            1.43,
            0.88,
            0.19,
            -0.72,
            0.19,
            stone,
            arc_segments=7,
            front_bevel=0.038,
        )
        add_box(
            collection,
            root,
            f"WallStraight_ArchSill_{arch_index}",
            (center_x, -0.625, 0.58),
            (1.84, 0.20, 0.18),
            stone,
            0.04,
        )
        add_box(
            collection,
            root,
            f"WallStraight_ArchKeystone_{arch_index}",
            (center_x, -0.68, 2.29),
            (0.24, 0.17, 0.30),
            stone,
            0.03,
        )

    # Heavy corbels under the crown create a readable shadow rhythm at gameplay
    # distance without introducing another renderer or material.
    for corbel_index, x in enumerate((-2.58, -0.86, 0.86, 2.58)):
        add_box(
            collection,
            root,
            f"WallStraight_CrownCorbel_{corbel_index}",
            (x, -0.625, 2.40),
            (0.46, 0.24, 0.24),
            stone,
            0.04,
        )

    add_box(collection, root, "WallStraight_MasonryCourse_Low", (0.0, 0.60, 0.82), (6.75, 0.14, 0.12), stone, 0.03)
    add_box(collection, root, "WallStraight_MasonryCourse_High", (0.0, 0.60, 2.20), (6.75, 0.14, 0.12), stone, 0.03)


def build_wall_broken(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> None:
    stone = materials[STONE_SLOT]
    profile = [
        (-3.78, 0.08),
        (3.78, 0.08),
        (3.78, 0.56),
        (3.24, 0.58),
        (2.70, 0.94),
        (2.10, 0.82),
        (1.68, 1.30),
        (1.08, 1.12),
        (0.60, 1.76),
        (0.06, 1.58),
        (-0.40, 2.18),
        (-1.05, 2.02),
        (-1.60, 2.62),
        (-2.30, 2.48),
        (-2.76, 3.12),
        (-3.42, 2.76),
        (-3.78, 2.82),
    ]
    add_profile_prism(collection, root, "WallBroken_SolidBody", profile, 1.10, stone, 0.08)
    add_box(collection, root, "WallBroken_Foundation", (0.0, 0.0, 0.19), (FORTRESS_BAY_WIDTH, 1.48, 0.38), stone, 0.08)
    add_wall_buttress(
        collection,
        root,
        "WallBroken_LeftTaperedButtress",
        -3.35,
        stone,
        top_height=2.56,
        width=0.46,
    )
    add_wall_buttress(
        collection,
        root,
        "WallBroken_ShearedButtress",
        0.18,
        stone,
        top_height=1.48,
        width=0.40,
    )

    rubble = [
        (2.46, -0.34, 0.36, 0.72, 0.64, 0.50, 8.0, -11.0, 12.0),
        (3.26, 0.30, 0.32, 0.62, 0.55, 0.44, -9.0, 13.0, -17.0),
        (1.32, 0.30, 0.26, 0.48, 0.46, 0.34, 14.0, 7.0, 24.0),
    ]
    for index, (x, y, z, sx, sy, sz, angle_x, angle_y, angle_z) in enumerate(rubble):
        add_box(
            collection,
            root,
            f"WallBroken_Rubble_{index}",
            (x, y, z),
            (sx, sy, sz),
            stone,
            0.07,
            rotation=(
                math.radians(angle_x),
                math.radians(angle_y),
                math.radians(angle_z),
            ),
        )

    # Remaining courses and one framed bay keep the damaged module constructed,
    # rather than reading as a single zig-zag silhouette.
    add_box(collection, root, "WallBroken_MasonryCourse_Low", (-1.16, -0.605, 0.82), (4.52, 0.16, 0.12), stone, 0.03)
    add_box(collection, root, "WallBroken_MasonryCourse_High", (-1.94, -0.605, 1.76), (3.02, 0.16, 0.12), stone, 0.03)
    add_arch_frame(
        collection,
        root,
        "WallBroken_SurvivingArchFrame",
        -2.08,
        0.58,
        1.28,
        0.74,
        0.17,
        -0.72,
        0.19,
        stone,
        arc_segments=6,
        front_bevel=0.034,
    )
    add_box(
        collection,
        root,
        "WallBroken_SurvivingArchSill",
        (-2.08, -0.625, 0.58),
        (1.54, 0.20, 0.17),
        stone,
        0.038,
    )
    add_box(
        collection,
        root,
        "WallBroken_ArchKeystone",
        (-2.08, -0.68, 2.00),
        (0.22, 0.17, 0.27),
        stone,
        0.03,
    )

    # A few large, offset face stones expose the construction beneath the torn
    # crown. They are deliberately sparse so the damage remains readable.
    face_blocks = (
        (-0.82, 1.35, 0.58, -3.0),
        (0.52, 1.05, 0.52, 4.0),
        (1.34, 0.68, 0.46, -5.0),
    )
    for block_index, (x, z, width, angle) in enumerate(face_blocks):
        add_box(
            collection,
            root,
            f"WallBroken_ExposedAshlar_{block_index}",
            (x, -0.625, z),
            (width, 0.18, 0.24),
            stone,
            0.035,
            rotation=(0.0, math.radians(angle), 0.0),
        )

    cap_segments = (
        ((-3.76, 2.80), (-3.42, 2.75)),
        ((-3.42, 2.75), (-2.76, 3.10)),
        ((-2.72, 3.09), (-2.30, 2.48)),
        ((-2.29, 2.47), (-1.61, 2.62)),
        ((-1.60, 2.60), (-1.06, 2.02)),
        ((-1.04, 2.01), (-0.41, 2.18)),
    )
    for index, ((x0, z0), (x1, z1)) in enumerate(cap_segments):
        dx = x1 - x0
        dz = z1 - z0
        add_box(
            collection,
            root,
            f"WallBroken_FracturedCap_{index}",
            ((x0 + x1) * 0.5, 0.0, (z0 + z1) * 0.5),
            (math.hypot(dx, dz), 1.20, 0.16),
            stone,
            0.03,
            rotation=(0.0, -math.atan2(dz, dx), 0.0),
        )


def build_tower(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> None:
    stone = materials[STONE_SLOT]
    ember = materials[EMBER_SLOT]
    outward = TOWER_OUTWARD_PROJECTION

    # Wall wings remain on the 22.45m chord plane while the bastion projects
    # outward. A tower therefore replaces one complete 18-ring bay without gaps.
    for side_index, side in enumerate((-1.0, 1.0)):
        wing_x = side * 2.83
        outer_x = side * 3.55
        add_box(collection, root, f"Tower_WingBody_{side_index}", (wing_x, 0.0, 1.42), (2.06, 1.10, 2.54), stone, 0.09)
        add_box(collection, root, f"Tower_WingFoundation_{side_index}", (wing_x, 0.0, 0.20), (2.06, 1.48, 0.40), stone, 0.08)
        add_box(collection, root, f"Tower_WingCrown_{side_index}", (wing_x, 0.0, 2.59), (2.06, 1.28, 0.28), stone, 0.06)
        add_box(collection, root, f"Tower_WingButtress_{side_index}", (outer_x, -0.01, 1.45), (0.42, 1.48, 2.66), stone, 0.07)
        add_box(collection, root, f"Tower_WingFoot_{side_index}", (outer_x, -0.01, 0.30), (0.62, 1.48, 0.60), stone, 0.07)
        add_arch_frame(
            collection,
            root,
            f"Tower_WingArch_{side_index}",
            wing_x,
            1.00,
            1.55,
            0.43,
            0.13,
            -0.70,
            0.17,
            stone,
            arc_segments=5,
            front_bevel=0.025,
        )
        for merlon_index, x_offset in enumerate((-0.47, 0.47)):
            add_box(
                collection,
                root,
                f"Tower_WingMerlon_{side_index}_{merlon_index}",
                (wing_x + x_offset, 0.0, 2.95),
                (0.56, 1.22, 0.50),
                stone,
                0.06,
            )

    add_cylinder(collection, root, "Tower_Foundation", (0.0, outward, 0.22), 2.10, 0.44, 12, stone, 0.09, math.radians(15.0))
    add_cone(
        collection,
        root,
        "Tower_TaperedShaft",
        (0.0, outward, 2.38),
        1.68,
        1.50,
        4.20,
        12,
        stone,
        0.09,
        math.radians(15.0),
    )
    add_cone(
        collection,
        root,
        "Tower_SplayedBaseBand",
        (0.0, outward, 0.62),
        1.88,
        1.76,
        0.32,
        12,
        stone,
        0.07,
        math.radians(15.0),
    )
    add_cone(
        collection,
        root,
        "Tower_FlaredCrownBand",
        (0.0, outward, 4.44),
        1.70,
        1.90,
        0.40,
        12,
        stone,
        0.07,
        math.radians(15.0),
    )
    add_cylinder(collection, root, "Tower_MasonryBand_Low", (0.0, outward, 1.42), 1.70, 0.16, 12, stone, 0.045, math.radians(15.0))
    add_cylinder(collection, root, "Tower_MasonryBand_High", (0.0, outward, 3.02), 1.70, 0.16, 12, stone, 0.045, math.radians(15.0))

    buttress_angles = (0.0, 60.0, 120.0, 180.0, 230.0, 310.0)
    for index, angle_degrees in enumerate(buttress_angles):
        angle = math.radians(angle_degrees)
        add_radial_tower_buttress(
            collection,
            root,
            f"Tower_TaperedButtress_{index:02d}",
            (0.0, outward),
            angle,
            stone,
        )

    merlon_variants = (
        (0.64, 0.58, 0.00),
        (0.58, 0.62, 0.00),
        (0.36, 0.52, -0.04),
        (0.64, 0.58, 0.00),
        (0.50, 0.64, 0.03),
        (0.64, 0.58, 0.00),
        (0.60, 0.54, 0.00),
        (0.42, 0.55, -0.02),
    )
    for index, (height, width, radial_offset) in enumerate(merlon_variants):
        angle = math.radians(index * 45.0 + 22.5)
        radius = 1.56 + radial_offset
        x = math.cos(angle) * radius
        y = outward + (math.sin(angle) * radius)
        add_box(
            collection,
            root,
            f"Tower_Merlon_{index:02d}",
            (x, y, 4.56 + (height * 0.5)),
            (width, 0.62, height),
            stone,
            0.07,
            rotation=(0.0, 0.0, angle),
        )

    # Two integrated fire basins keep perimeter lighting inside the tower renderer.
    for index, y in enumerate((outward - 1.48, outward + 1.48)):
        add_cone(collection, root, f"Tower_BrazierBowl_{index}", (0.0, y, 3.46), 0.52, 0.38, 0.28, 12, stone, 0.05)
        add_cylinder(collection, root, f"Tower_EmberBed_{index}", (0.0, y, 3.63), 0.35, 0.10, 12, ember, 0.02)
        add_flame_crystal(collection, root, f"Tower_EmberFlame_{index}", (0.0, y, 4.00), 0.24, 0.76, ember)

    # The inner face receives one continuous arched portal frame. The tapered shaft
    # behind it supplies a genuine depth gradient instead of a flat applied panel.
    add_arch_frame(
        collection,
        root,
        "Tower_InnerPortalFrame",
        0.0,
        1.36,
        2.43,
        0.74,
        0.19,
        outward - 1.74,
        0.20,
        stone,
        arc_segments=7,
        front_bevel=0.038,
    )
    add_box(
        collection,
        root,
        "Tower_InnerPortalSill",
        (0.0, outward - 1.65, 1.36),
        (1.56, 0.20, 0.18),
        stone,
        0.04,
    )
    add_box(
        collection,
        root,
        "Tower_InnerPortalKeystone",
        (0.0, outward - 1.755, 3.15),
        (0.25, 0.17, 0.32),
        stone,
        0.03,
    )


def build_brazier(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> None:
    stone = materials[STONE_SLOT]
    ember = materials[EMBER_SLOT]
    add_cylinder(collection, root, "Brazier_Foundation", (0.0, 0.0, 0.14), 0.62, 0.28, 8, stone, 0.06)
    add_cylinder(collection, root, "Brazier_Plinth", (0.0, 0.0, 0.42), 0.43, 0.28, 8, stone, 0.05)
    add_cone(collection, root, "Brazier_Stem", (0.0, 0.0, 1.08), 0.30, 0.20, 1.14, 8, stone, 0.05)
    add_cone(collection, root, "Brazier_Bowl", (0.0, 0.0, 1.70), 0.70, 0.49, 0.38, 8, stone, 0.05)
    add_torus(collection, root, "Brazier_Rim", (0.0, 0.0, 1.86), 0.55, 0.09, stone, 10, 3)
    add_cylinder(collection, root, "Brazier_EmberBed", (0.0, 0.0, 1.84), 0.45, 0.10, 8, ember, 0.02)
    add_flame_crystal(collection, root, "Brazier_EmberFlame", (0.0, 0.0, 2.18), 0.28, 0.72, ember)


def build_wardstone(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> None:
    stone = materials[STONE_SLOT]
    ember = materials[EMBER_SLOT]
    octagon_rotation = math.radians(22.5)

    # Cover must read as a short, massive masonry obstruction from the locked
    # strategy camera. The separate, overlapping closed courses create broad
    # shadow lines without adding visual-only collision ledges.
    add_cylinder(
        collection,
        root,
        "Wardstone_Foundation",
        (0.0, 0.0, 0.14),
        1.03,
        0.28,
        8,
        stone,
        0.10,
        octagon_rotation,
    )
    add_cylinder(
        collection,
        root,
        "Wardstone_BaseCourse",
        (0.0, 0.0, 0.40),
        0.97,
        0.32,
        8,
        stone,
        0.10,
        octagon_rotation,
    )
    add_cylinder(
        collection,
        root,
        "Wardstone_LowerCourse",
        (0.0, 0.0, 0.78),
        0.87,
        0.48,
        8,
        stone,
        0.09,
        octagon_rotation,
    )
    add_cylinder(
        collection,
        root,
        "Wardstone_MiddleCourse",
        (0.0, 0.0, 1.24),
        0.84,
        0.48,
        8,
        stone,
        0.09,
        octagon_rotation,
    )
    add_cylinder(
        collection,
        root,
        "Wardstone_UpperCourse",
        (0.0, 0.0, 1.70),
        0.87,
        0.48,
        8,
        stone,
        0.09,
        octagon_rotation,
    )
    add_cylinder(
        collection,
        root,
        "Wardstone_Cap",
        (0.0, 0.0, 2.12),
        1.01,
        0.36,
        8,
        stone,
        0.10,
        octagon_rotation,
    )

    # One small diamond-shaped ember insert supplies orientation and material
    # contrast. It stays subordinate to the masonry silhouette and cannot be
    # mistaken for a full-height combat telegraph.
    add_box(
        collection,
        root,
        "Wardstone_EmberInset",
        (0.0, -0.852, 1.25),
        (0.24, 0.055, 0.24),
        ember,
        0.025,
        rotation=(0.0, math.radians(45.0), 0.0),
    )


def build_foundation_ring(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
) -> None:
    stone = materials[STONE_SLOT]

    # The continuous shoulder begins beyond the 20.5m authoritative kill radius
    # and remains visibly recessed below the arena. Its radial profile is sloped
    # and broadly chamfered, so no horizontal shelf reads as reachable safe ground.
    # The 144 sides align every fifth edge with the eighteen-bay fortress rhythm.
    profile = (
        (FOUNDATION_INNER_RADIUS, FOUNDATION_BOTTOM_HEIGHT),
        (25.10, FOUNDATION_BOTTOM_HEIGHT),
        (25.22, -1.55),
        (25.22, -1.02),
        (25.08, -0.82),
        (24.72, -0.68),
        (23.90, -0.57),
        (22.95, -0.48),
        (22.05, FOUNDATION_TOP_HEIGHT),
        (21.60, -0.52),
        (21.38, -0.66),
        (FOUNDATION_INNER_RADIUS, -0.86),
    )
    add_revolved_ring_profile(
        collection,
        root,
        "FoundationRing_ContinuousShoulder",
        profile,
        144,
        stone,
    )

    # Broad inner-face voussoirs turn the otherwise continuous retaining wall into
    # eighteen legible constructed bays. Narrow recessed joints remain between
    # them, but the structural ring behind the joints never opens.
    for bay_index in range(FOUNDATION_BAY_COUNT):
        bay_angle = FOUNDATION_BAY_ANGLE_OFFSET + (
            bay_index * (360.0 / FOUNDATION_BAY_COUNT)
        )
        add_annular_sector_block(
            collection,
            root,
            f"FoundationRing_InnerBay_{bay_index + 1:02d}",
            bay_angle,
            7.35,
            FOUNDATION_INNER_RADIUS,
            21.78,
            -1.62,
            -0.56,
            -0.48,
            stone,
            0.06,
        )

        # Each module root receives one submerged radial footing. Their sloped caps
        # meet the authored -0.72m wall/tower sinks while keeping the widest edge
        # outside the gameplay boundary and below the lava-facing architecture.
        add_annular_sector_block(
            collection,
            root,
            f"FoundationRing_Buttress_{bay_index + 1:02d}",
            bay_angle,
            3.45,
            23.48,
            FOUNDATION_OUTER_RADIUS,
            -1.72,
            -0.46,
            -0.74,
            stone,
            0.08,
        )

        # Staggered shoulder stones break the large smooth annulus into two
        # architectural depth bands. Deterministic omissions and small angle/height
        # changes keep the unique ring from reading as eighteen cloned wedges.
        staggered_angle = bay_angle + 10.0
        shoulder_half_angle = 5.65 + ((bay_index % 3) * 0.35)
        shoulder_top_variation = (bay_index % 4) * 0.008
        add_annular_sector_block(
            collection,
            root,
            f"FoundationRing_ShoulderCourse_{bay_index + 1:02d}",
            staggered_angle,
            shoulder_half_angle,
            21.82,
            23.38,
            -1.22,
            FOUNDATION_TOP_HEIGHT - shoulder_top_variation,
            -0.50 - shoulder_top_variation,
            stone,
            0.045,
        )

        if bay_index not in {4, 11, 16}:
            outer_angle_offset = (-0.55, 0.20, 0.65)[bay_index % 3]
            add_annular_sector_block(
                collection,
                root,
                f"FoundationRing_OuterCourse_{bay_index + 1:02d}",
                staggered_angle + outer_angle_offset,
                5.15 + ((bay_index % 2) * 0.45),
                23.42,
                24.86,
                -1.48,
                -0.51 - shoulder_top_variation,
                -0.70 - shoulder_top_variation,
                stone,
                0.045,
            )


def recalculate_normals(mesh: bpy.types.Mesh) -> None:
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(mesh)
    bm.free()
    mesh.update()


def unwrap_uv0(obj: bpy.types.Object) -> None:
    activate(obj)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(
        angle_limit=math.radians(58.0),
        margin_method="SCALED",
        island_margin=0.025,
        area_weight=0.2,
        correct_aspect=True,
        scale_to_bounds=True,
    )
    bpy.ops.object.mode_set(mode="OBJECT")
    uv = obj.data.uv_layers.active
    if uv is None:
        raise RuntimeError(f"{obj.name} did not receive UV0")
    uv.name = "UV0"


def normalize_material_slots(
    obj: bpy.types.Object,
    materials: dict[str, bpy.types.Material],
    requested_slots: tuple[str, ...],
) -> None:
    old_names = [slot.material.name if slot.material else "" for slot in obj.material_slots]
    includes_ember = EMBER_SLOT in requested_slots
    new_indices = [
        1 if includes_ember and old_names[polygon.material_index] == EMBER_SLOT else 0
        for polygon in obj.data.polygons
    ]
    obj.data.materials.clear()
    for material_name in requested_slots:
        obj.data.materials.append(materials[material_name])
    for polygon, material_index in zip(obj.data.polygons, new_indices, strict=True):
        polygon.material_index = material_index


def join_and_finalize(
    collection: bpy.types.Collection,
    root: bpy.types.Object,
    spec: AssetSpec,
    materials: dict[str, bpy.types.Material],
) -> bpy.types.Object:
    pieces = [obj for obj in collection.objects if obj.type == "MESH"]
    if not pieces:
        raise RuntimeError(f"{spec.name} has no mesh pieces")

    bpy.ops.object.select_all(action="DESELECT")
    for piece in pieces:
        piece.select_set(True)
    bpy.context.view_layer.objects.active = pieces[0]
    bpy.ops.object.join()
    mesh_obj = bpy.context.object
    mesh_obj.name = f"{spec.name}_Mesh"
    mesh_obj.data.name = f"{spec.name}_MeshData"
    mesh_obj.parent = root

    bpy.context.scene.cursor.location = (0.0, 0.0, 0.0)
    bpy.ops.object.origin_set(type="ORIGIN_CURSOR", center="MEDIAN")
    bpy.ops.object.transform_apply(location=False, rotation=True, scale=True)
    normalize_material_slots(mesh_obj, materials, spec.material_slots)
    recalculate_normals(mesh_obj.data)
    unwrap_uv0(mesh_obj)

    triangulate = mesh_obj.modifiers.new(name="ExportTriangulation", type="TRIANGULATE")
    triangulate.quad_method = "BEAUTY"
    triangulate.ngon_method = "BEAUTY"
    bpy.ops.object.modifier_apply(modifier=triangulate.name)

    for polygon in mesh_obj.data.polygons:
        polygon.use_smooth = True
    weighted = mesh_obj.modifiers.new(name="ExportWeightedNormals", type="WEIGHTED_NORMAL")
    weighted.keep_sharp = True
    weighted.weight = 50
    weighted.thresh = 0.01
    bpy.ops.object.modifier_apply(modifier=weighted.name)

    recalculate_normals(mesh_obj.data)
    mesh_obj.data.validate(verbose=True, clean_customdata=False)
    mesh_obj.data.update()
    mesh_obj["visual_only"] = True
    mesh_obj["material_contract"] = ";".join(
        f"{index}:{material_name}" for index, material_name in enumerate(spec.material_slots)
    )
    return mesh_obj


def object_world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    world_points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    minimum = Vector((min(point.x for point in world_points), min(point.y for point in world_points), min(point.z for point in world_points)))
    maximum = Vector((max(point.x for point in world_points), max(point.y for point in world_points), max(point.z for point in world_points)))
    return minimum, maximum


def mesh_triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def mesh_vertex_count(obj: bpy.types.Object) -> int:
    return len(obj.data.vertices)


def mesh_is_closed(obj: bpy.types.Object) -> bool:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    closed = all(len(edge.link_faces) == 2 for edge in bm.edges)
    bm.free()
    return closed


def validate_asset(spec: AssetSpec, root: bpy.types.Object, mesh_obj: bpy.types.Object) -> dict[str, object]:
    triangles = mesh_triangle_count(mesh_obj)
    vertices = mesh_vertex_count(mesh_obj)
    minimum, maximum = object_world_bounds(mesh_obj)
    size = maximum - minimum
    material_names = [slot.material.name if slot.material else "" for slot in mesh_obj.material_slots]
    maximum_planar_vertex_radius = max(
        math.hypot(
            (mesh_obj.matrix_world @ vertex.co).x,
            (mesh_obj.matrix_world @ vertex.co).y,
        )
        for vertex in mesh_obj.data.vertices
    )
    minimum_planar_vertex_radius = min(
        math.hypot(
            (mesh_obj.matrix_world @ vertex.co).x,
            (mesh_obj.matrix_world @ vertex.co).y,
        )
        for vertex in mesh_obj.data.vertices
    )

    failures: list[str] = []
    if triangles > spec.max_triangles:
        failures.append(f"triangles {triangles} exceed {spec.max_triangles}")
    if vertices >= 65535:
        failures.append(f"vertices {vertices} exceed 16-bit mesh limit")
    if material_names != list(spec.material_slots):
        failures.append(f"material order {material_names} is not {list(spec.material_slots)}")
    if mesh_obj.data.uv_layers.get("UV0") is None:
        failures.append("UV0 is missing")
    if any(len(polygon.vertices) != 3 for polygon in mesh_obj.data.polygons):
        failures.append("mesh is not fully triangulated")
    if spec.name != FOUNDATION_ASSET_NAME and minimum.z < -0.001:
        failures.append(f"geometry falls below ground: min z {minimum.z:.4f}")
    if root.location.length > 0.0001:
        failures.append(f"root is not ground-centered: {tuple(root.location)}")
    if not mesh_is_closed(mesh_obj):
        failures.append("mesh contains open or non-manifold edges")
    if spec.name == "Wardstone":
        if maximum_planar_vertex_radius < 0.98:
            failures.append(
                f"wardstone footprint radius {maximum_planar_vertex_radius:.3f} is narrower than 0.98m"
            )
        if maximum_planar_vertex_radius > 1.03:
            failures.append(
                f"wardstone footprint radius {maximum_planar_vertex_radius:.3f} exceeds visual target 1.03m"
            )
        if maximum.z < 2.28:
            failures.append(f"wardstone visual height {maximum.z:.3f} is below 2.28m")
        if maximum.z > 2.35:
            failures.append(f"wardstone visual height {maximum.z:.3f} exceeds 2.35m")
    if spec.name == FOUNDATION_ASSET_NAME:
        if triangles > 8_000:
            failures.append(
                f"foundation triangles {triangles} exceed the 8,000 mobile target"
            )
        if minimum_planar_vertex_radius < 21.25:
            failures.append(
                "foundation inner radius "
                f"{minimum_planar_vertex_radius:.3f} enters the protected lava band"
            )
        if minimum_planar_vertex_radius > 21.40:
            failures.append(
                "foundation inner radius "
                f"{minimum_planar_vertex_radius:.3f} leaves a visible perimeter gap"
            )
        if maximum_planar_vertex_radius > 25.50:
            failures.append(
                "foundation outer radius "
                f"{maximum_planar_vertex_radius:.3f} exceeds 25.50m"
            )
        if maximum_planar_vertex_radius < 25.30:
            failures.append(
                "foundation outer radius "
                f"{maximum_planar_vertex_radius:.3f} lacks the authored buttress depth"
            )
        if not math.isclose(maximum.z, FOUNDATION_TOP_HEIGHT, abs_tol=0.025):
            failures.append(
                f"foundation top {maximum.z:.3f} is not recessed near "
                f"{FOUNDATION_TOP_HEIGHT:.2f}m"
            )
        if not math.isclose(minimum.z, FOUNDATION_BOTTOM_HEIGHT, abs_tol=0.025):
            failures.append(
                f"foundation bottom {minimum.z:.3f} is not near "
                f"{FOUNDATION_BOTTOM_HEIGHT:.2f}m"
            )

    for axis, actual, expected in zip("XYZ", size, spec.expected_size, strict=True):
        if actual > expected + 0.03:
            failures.append(f"{axis} size {actual:.3f} exceeds expected maximum {expected:.3f}")

    if failures:
        raise RuntimeError(f"{spec.name} validation failed: {'; '.join(failures)}")

    return {
        "asset": spec.name,
        "root_object": root.name,
        "mesh_object": mesh_obj.name,
        "triangles": triangles,
        "vertices": vertices,
        "submeshes": len(material_names),
        "material_slots": material_names,
        "uv_layers": [layer.name for layer in mesh_obj.data.uv_layers],
        "bounds_min_blender_xyz": [round(value, 4) for value in minimum],
        "bounds_max_blender_xyz": [round(value, 4) for value in maximum],
        "size_blender_xyz": [round(value, 4) for value in size],
        "minimum_planar_vertex_radius": round(minimum_planar_vertex_radius, 4),
        "maximum_planar_vertex_radius": round(maximum_planar_vertex_radius, 4),
        "ground_center_origin": spec.name != FOUNDATION_ASSET_NAME,
        "centered_origin": True,
        "closed_manifold": True,
        "triangulated": True,
        "weighted_normals": True,
        "visual_only": True,
        **(
            {
                "wardstone_design_contract": {
                    "profile": "broad_stacked_octagonal_masonry",
                    "chunky_body_courses": 3,
                    "footprint_radius_range_meters": [0.98, 1.03],
                    "height_range_meters": [2.28, 2.35],
                    "bevel_range_meters": [0.08, 0.12],
                    "ember_insets": 1,
                }
            }
            if spec.name == "Wardstone"
            else {}
        ),
        **(
            {
                "foundation_ring_design_contract": {
                    "profile": "continuous_submerged_sloped_masonry_shoulder",
                    "bay_count": FOUNDATION_BAY_COUNT,
                    "inner_radius_minimum_meters": 21.25,
                    "authored_inner_radius_meters": FOUNDATION_INNER_RADIUS,
                    "outer_radius_maximum_meters": 25.50,
                    "authored_outer_radius_meters": FOUNDATION_OUTER_RADIUS,
                    "top_height_meters": FOUNDATION_TOP_HEIGHT,
                    "bottom_height_meters": FOUNDATION_BOTTOM_HEIGHT,
                    "flat_reachable_ledges": 0,
                    "joined_static_meshes": 1,
                    "mobile_triangle_target": 8_000,
                }
            }
            if spec.name == FOUNDATION_ASSET_NAME
            else {}
        ),
    }


def export_asset(export_dir: Path, spec: AssetSpec, collection: bpy.types.Collection) -> Path:
    output = export_dir / f"{spec.name}.fbx"
    bpy.ops.object.select_all(action="DESELECT")
    for obj in collection.objects:
        obj.select_set(True)
    root = collection.objects.get(spec.name)
    bpy.context.view_layer.objects.active = root
    bpy.ops.export_scene.fbx(
        filepath=str(output),
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
    if not output.is_file() or output.stat().st_size <= 0:
        raise RuntimeError(f"FBX export did not produce {output}")
    return output


def set_collection_hidden(collection: bpy.types.Collection, hidden: bool) -> None:
    collection.hide_render = hidden
    collection.hide_viewport = hidden


def remove_preview_collection(collection: bpy.types.Collection) -> None:
    for obj in tuple(collection.objects):
        bpy.data.objects.remove(obj, do_unlink=True)
    bpy.data.collections.remove(collection)


def add_preview_area_light(
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


def add_preview_camera(
    collection: bpy.types.Collection,
    name: str,
    target: tuple[float, float, float],
    distance: float,
    pitch_degrees: float,
    horizontal_fov_degrees: float,
) -> bpy.types.Object:
    pitch = math.radians(pitch_degrees)
    horizontal_distance = distance * math.cos(pitch)
    vertical_distance = distance * math.sin(pitch)
    camera_location = (
        target[0],
        target[1] - horizontal_distance,
        target[2] + vertical_distance,
    )
    bpy.ops.object.camera_add(location=camera_location)
    camera = bpy.context.object
    camera.name = name
    camera.rotation_euler = (
        Vector(target) - camera.location
    ).to_track_quat("-Z", "Y").to_euler()
    camera.data.sensor_fit = "HORIZONTAL"
    camera.data.angle = math.radians(horizontal_fov_degrees)
    move_to_collection(camera, collection)
    bpy.context.scene.camera = camera
    return camera


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
    """Mirror the authored Unity strategy-follow camera exactly."""

    yaw = math.radians(yaw_degrees)
    pitch = math.radians(pitch_degrees)
    planar_forward = Vector((math.sin(yaw), math.cos(yaw), 0.0))
    planar_right = Vector((math.cos(yaw), -math.sin(yaw), 0.0))
    target = (
        Vector(follow_position)
        + Vector((0.0, 0.0, pivot_height))
        + (planar_forward * forward_focus_offset)
        + (planar_right * right_focus_offset)
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


def render_preview(
    preview_path: Path,
    collections: Iterable[bpy.types.Collection],
) -> None:
    preview_collection = bpy.data.collections.new("FortressKit_PreviewOnly")
    bpy.context.scene.collection.children.link(preview_collection)

    placements = {
        "FortressWallStraight": (-4.25, 4.0, 0.0),
        "FortressWallBroken": (4.25, 4.0, 0.0),
        "FortressTower": (0.0, -0.45, 0.0),
        "FortressBrazier": (5.4, -3.6, 0.0),
        "Wardstone": (-5.4, -3.6, 0.0),
    }
    roots: list[bpy.types.Object] = []
    for collection in collections:
        if collection.name == FOUNDATION_ASSET_NAME:
            set_collection_hidden(collection, True)
            continue
        root = collection.objects.get(collection.name)
        roots.append(root)
        root.location = placements[collection.name]
        set_collection_hidden(collection, False)

    preview_floor_root = bpy.data.objects.new("PreviewFloorRoot", None)
    preview_collection.objects.link(preview_floor_root)
    add_cylinder(
        preview_collection,
        preview_floor_root,
        "PreviewFloor",
        (0.0, 0.0, -0.18),
        10.0,
        0.32,
        48,
        bpy.data.materials[STONE_SLOT],
        0.02,
    )
    add_preview_area_light(preview_collection, "Preview_Key", (-4.0, -7.0, 12.0), (0.0, 0.0, 1.5), 1700.0, 6.0, (0.54, 0.68, 1.0))
    add_preview_area_light(preview_collection, "Preview_Rim", (6.0, 3.0, 8.0), (0.0, 0.0, 1.8), 1300.0, 5.0, (1.0, 0.22, 0.035))
    add_preview_area_light(preview_collection, "Preview_Fill", (0.0, -1.0, 15.0), (0.0, 0.0, 0.0), 1200.0, 9.0, (0.74, 0.80, 1.0))

    bpy.ops.object.camera_add(location=(10.5, -18.0, 22.0))
    camera = bpy.context.object
    camera.name = "Preview_Camera"
    move_to_collection(camera, preview_collection)
    target = Vector((0.0, 0.5, 1.45))
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()
    camera.data.lens = 58.0
    bpy.context.scene.camera = camera

    scene = bpy.context.scene
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 900
    scene.render.resolution_percentage = 100
    scene.render.filepath = str(preview_path)
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"
    bpy.ops.render.render(write_still=True)

    for root in roots:
        root.location = (0.0, 0.0, 0.0)
    remove_preview_collection(preview_collection)


def create_ring_instance(
    preview_collection: bpy.types.Collection,
    source_collection: bpy.types.Collection,
    name: str,
    angle_degrees: float,
    radius: float,
    vertical_offset: float = 0.0,
) -> bpy.types.Object:
    source_mesh = next(
        obj for obj in source_collection.objects if obj.type == "MESH"
    )
    angle = math.radians(angle_degrees)
    instance = source_mesh.copy()
    instance.data = source_mesh.data
    instance.name = name
    instance.parent = None
    instance.location = (
        math.sin(angle) * radius,
        math.cos(angle) * radius,
        vertical_offset,
    )
    instance.rotation_euler = (0.0, 0.0, -angle)
    preview_collection.objects.link(instance)
    return instance


def render_ring_previews(
    evidence_dir: Path,
    collections: Sequence[bpy.types.Collection],
) -> tuple[Path, Path]:
    by_name = {collection.name: collection for collection in collections}
    preview_collection = bpy.data.collections.new("FortressRing_PreviewOnly")
    bpy.context.scene.collection.children.link(preview_collection)
    for collection in collections:
        set_collection_hidden(collection, True)

    create_ring_instance(
        preview_collection,
        by_name[FOUNDATION_ASSET_NAME],
        "FoundationRing_Complete",
        0.0,
        0.0,
    )

    radius = 22.45
    angle_offset = FOUNDATION_BAY_ANGLE_OFFSET
    for index in range(FOUNDATION_BAY_COUNT):
        if index % 3 == 0:
            source_name = "FortressTower"
        elif index % 3 == 1:
            source_name = "FortressWallStraight"
        else:
            source_name = "FortressWallBroken"
        create_ring_instance(
            preview_collection,
            by_name[source_name],
            f"RingBay_{index + 1:02d}_{source_name}",
            angle_offset + (index * 20.0),
            radius,
            -0.72,
        )

    lava_material = create_material(
        "Preview_Lava",
        (0.18, 0.012, 0.003, 1.0),
        0.05,
        0.45,
    )
    lava_node = lava_material.node_tree.nodes.get("Principled BSDF")
    lava_node.inputs["Emission Color"].default_value = (0.9, 0.035, 0.002, 1.0)
    lava_node.inputs["Emission Strength"].default_value = 0.55
    preview_root = bpy.data.objects.new("FortressRingPreviewRoot", None)
    preview_collection.objects.link(preview_root)
    # Match the playable arena: safe stone ends at 15m, lava surface sits at
    # -0.79m, and the foundation does not begin until 21.30m. The uninterrupted
    # 6.30m lava band is therefore visible in both evidence frames.
    add_cylinder(preview_collection, preview_root, "RingLava", (0.0, 0.0, -0.85), 26.5, 0.12, 96, lava_material, 0.02)
    add_cylinder(preview_collection, preview_root, "RingArenaFloor", (0.0, 0.0, -0.15), 15.0, 0.30, 96, bpy.data.materials[STONE_SLOT], 0.02)
    add_torus(preview_collection, preview_root, "RingSafeTrim", (0.0, 0.0, 0.015), 15.0, 0.075, bpy.data.materials[EMBER_SLOT], 64, 4)

    light_target = (0.0, 0.0, 1.5)
    add_preview_area_light(preview_collection, "RingLight_North", (0.0, 20.0, 24.0), light_target, 2600.0, 15.0, (0.58, 0.69, 1.0))
    add_preview_area_light(preview_collection, "RingLight_South", (0.0, -20.0, 22.0), light_target, 2200.0, 15.0, (0.48, 0.60, 0.88))
    add_preview_area_light(preview_collection, "RingLight_East", (22.0, 0.0, 16.0), light_target, 1800.0, 13.0, (1.0, 0.27, 0.055))
    add_preview_area_light(preview_collection, "RingLight_West", (-22.0, 0.0, 18.0), light_target, 1700.0, 13.0, (0.36, 0.52, 0.85))

    scene = bpy.context.scene
    scene.render.resolution_x = 1920
    scene.render.resolution_y = 1080
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    scene.view_settings.look = "AgX - Medium High Contrast"

    connectivity_path = evidence_dir / "FortressRingConnectivity.png"
    full_camera = add_preview_camera(
        preview_collection,
        "RingConnectivity_Camera",
        (0.0, 0.0, 0.8),
        102.0,
        58.0,
        50.0,
    )
    scene.render.filepath = str(connectivity_path)
    bpy.ops.render.render(write_still=True)

    gameplay_path = evidence_dir / "FortressFoundationStrategyCamera.png"
    full_camera.hide_render = True
    gameplay_camera = add_gameplay_camera(
        preview_collection,
        "Foundation_StrategyCamera",
        (0.0, 0.0, 0.0),
        90.0,
        56.0,
        22.0,
        0.9,
        2.3,
        0.0,
        50.0,
    )
    scene.camera = gameplay_camera
    scene.render.filepath = str(gameplay_path)
    bpy.ops.render.render(write_still=True)

    remove_preview_collection(preview_collection)
    bpy.data.materials.remove(lava_material)
    return connectivity_path, gameplay_path


def save_blend(source_path: Path, collections: Iterable[bpy.types.Collection]) -> None:
    for collection in collections:
        set_collection_hidden(collection, False)
    bpy.ops.wm.save_as_mainfile(filepath=str(source_path), check_existing=False, compress=True)
    blender_backup = source_path.with_name(f"{source_path.name}1")
    if blender_backup.is_file():
        blender_backup.unlink()


def main() -> None:
    args = parse_args()
    if bpy.app.version < BLENDER_MINIMUM:
        raise RuntimeError(f"Blender 5.2 LTS or newer is required; found {bpy.app.version_string}")

    project_root = args.project_root.resolve()
    export_dir = project_root / "Assets" / "SessionRpg" / "SourceArt" / "Environment"
    blend_dir = project_root / "SourceArt" / "Blender" / "Environment"
    evidence_dir = project_root.parent / "outputs" / "art" / "blender" / "fortress-kit"
    export_dir.mkdir(parents=True, exist_ok=True)
    blend_dir.mkdir(parents=True, exist_ok=True)
    evidence_dir.mkdir(parents=True, exist_ok=True)

    random.seed(731_335)
    reset_scene()
    materials = create_materials()
    specs = (
        AssetSpec("FortressWallStraight", 1400, (STONE_SLOT,), (7.72, 1.50, 3.20), build_wall_straight),
        AssetSpec("FortressWallBroken", 1400, (STONE_SLOT,), (7.72, 1.50, 3.20), build_wall_broken),
        AssetSpec("FortressTower", 3200, (STONE_SLOT, EMBER_SLOT), (7.72, 4.20, 5.20), build_tower),
        AssetSpec("FortressBrazier", 650, (STONE_SLOT, EMBER_SLOT), (1.40, 1.40, 2.55), build_brazier),
        AssetSpec("Wardstone", 1500, (STONE_SLOT, EMBER_SLOT), (2.10, 2.10, 2.35), build_wardstone),
        AssetSpec(
            FOUNDATION_ASSET_NAME,
            8_000,
            (STONE_SLOT,),
            (51.00, 51.00, 1.40),
            build_foundation_ring,
        ),
    )

    collections: list[bpy.types.Collection] = []
    asset_records: list[dict[str, object]] = []
    for spec in specs:
        collection, root = create_asset_collection(spec.name)
        spec.builder(collection, root, materials)
        mesh_obj = join_and_finalize(collection, root, spec, materials)
        record = validate_asset(spec, root, mesh_obj)
        fbx_path = export_asset(export_dir, spec, collection)
        record["fbx"] = str(fbx_path.relative_to(project_root))
        record["fbx_bytes"] = fbx_path.stat().st_size
        asset_records.append(record)
        collections.append(collection)
        set_collection_hidden(collection, True)

    blend_path = blend_dir / "FortressKit.blend"
    save_blend(blend_path, collections)

    preview_path: Path | None = None
    connectivity_path: Path | None = None
    gameplay_path: Path | None = None
    if not args.skip_preview:
        preview_path = evidence_dir / "FortressKitContactSheet.png"
        render_preview(preview_path, collections)
        connectivity_path, gameplay_path = render_ring_previews(
            evidence_dir,
            collections,
        )

    ring_chord = 2.0 * 22.45 * math.sin(math.pi / 18.0)
    straight_triangles = next(
        int(record["triangles"])
        for record in asset_records
        if record["asset"] == "FortressWallStraight"
    )
    broken_triangles = next(
        int(record["triangles"])
        for record in asset_records
        if record["asset"] == "FortressWallBroken"
    )
    tower_triangles = next(
        int(record["triangles"])
        for record in asset_records
        if record["asset"] == "FortressTower"
    )
    foundation_record = next(
        record
        for record in asset_records
        if record["asset"] == FOUNDATION_ASSET_NAME
    )
    foundation_triangles = int(foundation_record["triangles"])
    ring_triangles = 6 * (
        straight_triangles + broken_triangles + tower_triangles
    )
    nominal_join_gap = ring_chord - FORTRESS_BAY_WIDTH
    if nominal_join_gap < 0.0 or nominal_join_gap > 0.10:
        raise RuntimeError(
            "Fortress module width does not satisfy the 18-bay ring chord: "
            f"chord={ring_chord:.4f}m, module={FORTRESS_BAY_WIDTH:.4f}m, "
            f"gap={nominal_join_gap:.4f}m"
        )
    if ring_triangles > 45_000:
        raise RuntimeError(
            f"Fortress ring triangles {ring_triangles} exceed 45,000"
        )
    manifest = {
        "schema": 1,
        "generator": "Tools/Art/Blender/build_fortress_kit.py",
        "generator_version": SCRIPT_VERSION,
        "blender_version": bpy.app.version_string,
        "original_project_asset": True,
        "external_mesh_or_texture_sources": [],
        "units": "meters",
        "source_blend": str(blend_path.relative_to(project_root)),
        "preview": str(preview_path.relative_to(project_root.parent))
        if preview_path is not None and preview_path.is_file()
        else None,
        "ring_connectivity_preview": str(
            connectivity_path.relative_to(project_root.parent)
        )
        if connectivity_path is not None and connectivity_path.is_file()
        else None,
        "ring_gameplay_framing_preview": str(
            gameplay_path.relative_to(project_root.parent)
        )
        if gameplay_path is not None and gameplay_path.is_file()
        else None,
        "export_axis": {"forward": "-Z", "up": "+Y"},
        "material_contract": [STONE_SLOT, EMBER_SLOT],
        "architectural_detail_contract": {
            "language": "volcanic_gothic_fortress",
            "straight_wall_chamfered_arches": 2,
            "straight_wall_tapered_buttresses": 3,
            "broken_wall_surviving_arches": 1,
            "broken_wall_sheared_buttresses": 1,
            "tower_tapered_shaft": True,
            "tower_flared_crown": True,
            "tower_inner_portal": True,
            "tower_damaged_merlons": 3,
            "foundation_staggered_shoulder_blocks": 33,
            "joined_static_meshes_per_asset": 1,
            "authoritative_gameplay_geometry": False,
        },
        "gameplay_camera_contract": {
            "yaw_degrees": 90.0,
            "pitch_degrees": 56.0,
            "distance_meters": 22.0,
            "pivot_height_meters": 0.9,
            "forward_focus_offset_meters": 2.3,
            "right_focus_offset_meters": 0.0,
            "vertical_fov_degrees": 50.0,
            "render_aspect_ratio": [16, 9],
        },
        "assets": asset_records,
        "triangle_total_unique_modules": sum(int(record["triangles"]) for record in asset_records),
        "ring_layout": {
            "radius_meters": 22.45,
            "bay_count": 18,
            "mathematical_chord_meters": round(ring_chord, 4),
            "module_tangential_width_meters": FORTRESS_BAY_WIDTH,
            "nominal_join_gap_meters": round(nominal_join_gap, 4),
            "tower_bastion_outward_projection_meters": TOWER_OUTWARD_PROJECTION,
            "wall_modules": 12,
            "tower_modules": 6,
            "renderers": 18,
            "submesh_draws": 24,
            "triangles": ring_triangles,
        },
        "foundation_ring": {
            "asset": FOUNDATION_ASSET_NAME,
            "inner_radius_meters": FOUNDATION_INNER_RADIUS,
            "outer_radius_meters": FOUNDATION_OUTER_RADIUS,
            "top_height_meters": FOUNDATION_TOP_HEIGHT,
            "bottom_height_meters": FOUNDATION_BOTTOM_HEIGHT,
            "bay_count": FOUNDATION_BAY_COUNT,
            "bay_angle_offset_degrees": FOUNDATION_BAY_ANGLE_OFFSET,
            "renderers": 1,
            "submesh_draws": 1,
            "triangles": foundation_triangles,
            "continuous_lava_band_meters": [15.0, FOUNDATION_INNER_RADIUS],
            "authoritative_gameplay_geometry": False,
        },
        "complete_perimeter": {
            "renderers": 19,
            "submesh_draws": 25,
            "triangles": ring_triangles + foundation_triangles,
        },
        "runtime_perimeter_budget_note": "At radius 22.45m, use twelve 7.72m wall modules and six 7.72m integrated-wing tower modules over the single continuous recessed foundation renderer. The standalone brazier remains optional source art and should not add a twentieth perimeter renderer.",
    }
    manifest_path = export_dir / "FortressKit.manifest.json"
    manifest_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print("SESSION_RPG_FORTRESS_KIT_BUILD_OK")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
