#!/usr/bin/env python3
"""Retain and inspect the selected Blender Foundation Human Base subset.

The official bundle stays unmodified.  This command appends only the selected
stylized male/female bodies and their paired eyes into a clean DCC blend outside
Unity Assets, records topology/provenance, and renders neutral intake evidence.
It performs no rigging, weight generation, retopology, or production promotion.
"""

from __future__ import annotations

import bmesh
import bpy
from hashlib import sha256
import json
from math import radians
from mathutils import Vector
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[3]
SOURCE_ROOT = (
    PROJECT_ROOT
    / "SourceArt/ThirdParty/BlenderFoundation/HumanBaseMeshes_1.4.1"
)
SOURCE_BLEND = (
    SOURCE_ROOT
    / "human-base-meshes-bundle-v1.4.1/human_base_meshes_bundle.blend"
)
SUBSET_ROOT = SOURCE_ROOT / "SelectedSubset"
SUBSET_BLEND = SUBSET_ROOT / "HumanBaseMeshes_StylizedBodies_v1.4.1.blend"
MANIFEST_PATH = SUBSET_ROOT / "HumanBaseMeshes_StylizedBodies_v1.4.1.manifest.json"
EVIDENCE_ROOT = SUBSET_ROOT / "Evidence"

ARCHIVE_SHA256 = "811f43accbb31a88266d932f8f5563b2d13586fca0ba2693aad1f5fe582b3515"
SOURCE_BLEND_SHA256 = "3c121505651140ceb4d69fd1d8923f7788ffadd81672f5be14845a5f2c75c137"
SELECTED = (
    "GEO-body_male_stylized",
    "GEO-body_male_stylized.eye.L",
    "GEO-body_male_stylized.eye.R",
    "GEO-body_female_stylized",
    "GEO-body_female_stylized.eye.L",
    "GEO-body_female_stylized.eye.R",
)
VARIANTS = {
    "Masculine": SELECTED[:3],
    "Feminine": SELECTED[3:],
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def file_sha256(path: Path) -> str:
    digest = sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = 30


def append_selected() -> dict[str, bpy.types.Object]:
    require(SOURCE_BLEND.is_file(), f"Missing official source blend: {SOURCE_BLEND}")
    require(
        file_sha256(SOURCE_BLEND) == SOURCE_BLEND_SHA256,
        "Official Human Base source blend hash changed",
    )
    with bpy.data.libraries.load(str(SOURCE_BLEND), link=False) as (source, target):
        missing = sorted(set(SELECTED) - set(source.objects))
        require(not missing, f"Official bundle is missing selected objects: {missing}")
        target.objects = list(SELECTED)

    collection = bpy.data.collections.new("HumanBaseMeshes_CC0_SelectedStylizedBodies_v1.4.1")
    bpy.context.scene.collection.children.link(collection)
    result: dict[str, bpy.types.Object] = {}
    for obj in target.objects:
        require(obj is not None and obj.type == "MESH", "Selected object append failed")
        collection.objects.link(obj)
        obj.animation_data_clear()
        obj.hide_set(False)
        obj.hide_viewport = False
        obj.hide_render = False
        for modifier in list(obj.modifiers):
            obj.modifiers.remove(modifier)
        for slot in list(obj.material_slots):
            if slot.material is not None:
                slot.material = None
        result[obj.name] = obj
    require(set(result) == set(SELECTED), "Selected object set changed during append")
    bpy.context.view_layer.update()
    return result


def topology_metrics(obj: bpy.types.Object) -> dict[str, object]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bm.normal_update()
    boundary_edges = sum(edge.is_boundary for edge in bm.edges)
    non_manifold_edges = sum(not edge.is_manifold for edge in bm.edges)
    loose_vertices = sum(not vertex.link_edges for vertex in bm.verts)
    loose_edges = sum(not edge.link_faces for edge in bm.edges)
    degenerate_faces = sum(face.calc_area() <= 1e-12 for face in bm.faces)
    bm.free()
    return {
        "vertices": len(mesh.vertices),
        "edges": len(mesh.edges),
        "polygons": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "quadFaces": sum(len(poly.vertices) == 4 for poly in mesh.polygons),
        "triangleFaces": sum(len(poly.vertices) == 3 for poly in mesh.polygons),
        "ngonFaces": sum(len(poly.vertices) > 4 for poly in mesh.polygons),
        "boundaryEdges": boundary_edges,
        "nonManifoldEdges": non_manifold_edges,
        "looseVertices": loose_vertices,
        "looseEdges": loose_edges,
        "degenerateFaces": degenerate_faces,
    }


def world_bounds(objects: tuple[bpy.types.Object, ...]) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for obj in objects for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def object_report(obj: bpy.types.Object) -> dict[str, object]:
    minimum, maximum = world_bounds((obj,))
    mesh = obj.data
    face_set_regions: dict[str, object] = {}
    face_set_attribute = mesh.attributes.get(".sculpt_face_set")
    if face_set_attribute is not None and face_set_attribute.domain == "FACE":
        grouped: dict[int, set[int]] = {}
        for polygon in mesh.polygons:
            value = int(face_set_attribute.data[polygon.index].value)
            grouped.setdefault(value, set()).update(polygon.vertices)
        for value, indices in sorted(grouped.items()):
            points = [obj.matrix_world @ mesh.vertices[index].co for index in indices]
            region_minimum = Vector(
                tuple(min(point[axis] for point in points) for axis in range(3))
            )
            region_maximum = Vector(
                tuple(max(point[axis] for point in points) for axis in range(3))
            )
            face_set_regions[str(value)] = {
                "vertexCount": len(indices),
                "boundsMeters": {
                    "minimum": [round(component, 6) for component in region_minimum],
                    "maximum": [round(component, 6) for component in region_maximum],
                },
            }
    return {
        "name": obj.name,
        "dataName": mesh.name,
        "topology": topology_metrics(obj),
        "boundsMeters": {
            "minimum": [round(value, 6) for value in minimum],
            "maximum": [round(value, 6) for value in maximum],
            "dimensions": [round(value, 6) for value in maximum - minimum],
        },
        "uvLayers": [layer.name for layer in mesh.uv_layers],
        "attributes": [
            {"name": attribute.name, "domain": attribute.domain, "dataType": attribute.data_type}
            for attribute in mesh.attributes
        ],
        "sculptFaceSetRegions": face_set_regions,
        "vertexGroups": [group.name for group in obj.vertex_groups],
        "shapeKeys": (
            [key.name for key in mesh.shape_keys.key_blocks]
            if mesh.shape_keys is not None
            else []
        ),
        "materials": [
            slot.material.name if slot.material is not None else None
            for slot in obj.material_slots
        ],
        "modifiers": [modifier.type for modifier in obj.modifiers],
        "objectTransform": {
            "location": [round(value, 6) for value in obj.location],
            "rotationEuler": [round(value, 6) for value in obj.rotation_euler],
            "scale": [round(value, 6) for value in obj.scale],
        },
    }


def add_metadata_texts() -> None:
    license_text = bpy.data.texts.new("LICENSE_CC0_1.0.txt")
    license_text.write(
        "Blender Foundation / Blender Studio Human Base Meshes v1.4.1\n"
        "License: CC0 1.0 Universal (Public Domain Dedication)\n"
        "Official listing: https://www.blender.org/download/demo-files/\n"
        "License reference: https://creativecommons.org/publicdomain/zero/1.0/\n"
    )
    provenance = bpy.data.texts.new("PROVENANCE.md")
    provenance.write((SOURCE_ROOT / "PROVENANCE.md").read_text(encoding="utf-8"))


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_workbench() -> None:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.52, 0.55, 0.59)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.display.shading.curvature_ridge_factor = 1.7
    scene.display.shading.curvature_valley_factor = 1.2
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("HumanBaseIntakeWorld")
    scene.world.color = (0.025, 0.03, 0.04)


def render_variant(label: str, names: tuple[str, ...], objects: dict[str, bpy.types.Object]) -> list[str]:
    for obj in objects.values():
        obj.hide_render = obj.name not in names
    active = tuple(objects[name] for name in names)
    minimum, maximum = world_bounds(active)
    center = (minimum + maximum) * 0.5
    height = maximum.z - minimum.z

    camera_data = bpy.data.cameras.get("IntakeCameraData")
    if camera_data is None:
        camera_data = bpy.data.cameras.new("IntakeCameraData")
    camera = bpy.data.objects.get("IntakeCamera")
    if camera is None:
        camera = bpy.data.objects.new("IntakeCamera", camera_data)
        bpy.context.scene.collection.objects.link(camera)
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = height * 1.13
    bpy.context.scene.camera = camera

    views = {
        "front": Vector((0.0, -4.5, center.z)),
        "three-quarter": Vector((3.0, -3.4, center.z + height * 0.04)),
        "side": Vector((4.5, 0.0, center.z)),
        "back": Vector((0.0, 4.5, center.z)),
    }
    outputs: list[str] = []
    for view_label, offset in views.items():
        camera.location = Vector((center.x + offset.x, center.y + offset.y, offset.z))
        look_at(camera, center)
        output = EVIDENCE_ROOT / f"human-base-v1.4.1-{label.lower()}-{view_label}.png"
        bpy.context.scene.render.filepath = str(output)
        bpy.ops.render.render(write_still=True)
        outputs.append(str(output.relative_to(PROJECT_ROOT)))
    return outputs


def main() -> None:
    require(bpy.app.version >= (4, 2, 0), "Blender 4.2 or newer is required")
    reset_scene()
    objects = append_selected()
    add_metadata_texts()
    SUBSET_ROOT.mkdir(parents=True, exist_ok=True)
    EVIDENCE_ROOT.mkdir(parents=True, exist_ok=True)

    reports = {name: object_report(obj) for name, obj in sorted(objects.items())}
    variant_reports: dict[str, object] = {}
    for label, names in VARIANTS.items():
        minimum, maximum = world_bounds(tuple(objects[name] for name in names))
        variant_reports[label] = {
            "objects": list(names),
            "boundsMeters": {
                "minimum": [round(value, 6) for value in minimum],
                "maximum": [round(value, 6) for value in maximum],
                "dimensions": [round(value, 6) for value in maximum - minimum],
            },
            "sourcePose": "A-pose",
        }

    bpy.ops.wm.save_as_mainfile(filepath=str(SUBSET_BLEND), compress=True)
    subset_hash = file_sha256(SUBSET_BLEND)
    configure_workbench()
    for label, names in VARIANTS.items():
        variant_reports[label]["evidence"] = render_variant(label, names, objects)

    manifest = {
        "schemaVersion": 1,
        "status": "PASS_RETAINED_CC0_SELECTED_DCC_SUBSET",
        "selectionStatus": "SELECTED_FOR_BIND_PROOF_NOT_UNITY_ACCEPTED",
        "source": {
            "product": "Blender Foundation / Blender Studio Human Base Meshes",
            "version": "1.4.1",
            "license": "CC0-1.0",
            "officialPage": "https://www.blender.org/download/demo-files/",
            "officialArchive": "https://download.blender.org/demo/asset-bundles/human-base-meshes/human-base-meshes-bundle-v1.4.1.zip",
            "archiveSha256": ARCHIVE_SHA256,
            "sourceBlend": str(SOURCE_BLEND.relative_to(PROJECT_ROOT)),
            "sourceBlendSha256": SOURCE_BLEND_SHA256,
        },
        "subsetBlend": str(SUBSET_BLEND.relative_to(PROJECT_ROOT)),
        "subsetBlendSha256": subset_hash,
        "selectedObjects": list(SELECTED),
        "objects": reports,
        "variants": variant_reports,
        "boundaries": {
            "quaterniusProductionLineage": False,
            "mpfbProductionLineage": False,
            "rigOrWeightsRetained": False,
            "unityIntegrationAllowed": False,
            "nextGate": "T-pose conversion and two-variant deformation proof",
        },
        "blenderVersion": list(bpy.app.version),
    }
    MANIFEST_PATH.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print("BLENDER_FOUNDATION_HUMAN_BASE_SUBSET_OK")


if __name__ == "__main__":
    main()
