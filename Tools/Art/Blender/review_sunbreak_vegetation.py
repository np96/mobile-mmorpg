#!/usr/bin/env python3
"""Review the frozen mesh at gameplay scale without changing admitted sources.

Blender --background --python Tools/Art/Blender/review_sunbreak_vegetation.py
Outputs disposable 8/64-transparent-bounce comparisons, not source assets.
"""
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
OUTPUT = ROOT / "build/review/sunbreak-vegetation-native-scale"
SOURCE = ROOT / "SourceArt/Environment/SunbreakVegetationV1/TreeSmall02.json"


def main():
    # Reuse only the measured preview material binding; never invoke generation
    # or change the hash-locked preparation script and its original deliveries.
    source = ROOT / "Tools/Art/Blender/prepare_sunbreak_vegetation.py"
    namespace = {"__name__": "review_only", "__file__": str(source)}
    exec(compile(source.read_text(), str(source), "exec"), namespace)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    data = json.loads(SOURCE.read_text())
    points = []
    for part in data["parts"]:
        flat = part["vertices"]
        vertices = [(flat[i], flat[i + 2], flat[i + 1]) for i in range(0, len(flat), 3)]
        indices = part["triangles"]
        faces = [(indices[i], indices[i + 2], indices[i + 1]) for i in range(0, len(indices), 3)]
        mesh = bpy.data.meshes.new(part["role"])
        mesh.from_pydata(vertices, [], faces)
        layer = mesh.uv_layers.new(name="UVMap")
        normals = []
        for loop in mesh.loops:
            vertex = loop.vertex_index
            layer.data[loop.index].uv = part["uv"][vertex * 2:vertex * 2 + 2]
            offset = vertex * 3
            normals.append((part["normals"][offset], part["normals"][offset + 2], part["normals"][offset + 1]))
        mesh.normals_split_custom_set(normals)
        obj = bpy.data.objects.new(part["role"], mesh)
        bpy.context.scene.collection.objects.link(obj)
        namespace["bind_preview_material"](obj, part["role"])
        points.extend(Vector(point) for point in vertices)

    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = 32
    scene.cycles.seed = 0
    scene.cycles.use_animated_seed = False
    scene.cycles.use_adaptive_sampling = False
    scene.render.film_transparent = False
    scene.render.resolution_x = 640
    scene.render.resolution_y = 480
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "AgX"
    scene.world = bpy.data.worlds.new("NeutralReviewWorld")
    scene.world.use_nodes = True
    background = scene.world.node_tree.nodes.get("Background")
    background.inputs[0].default_value = (.28, .32, .34, 1)
    background.inputs[1].default_value = .8
    bpy.ops.object.light_add(type="AREA", location=(3, -4, 8))
    light = bpy.context.object
    light.data.energy = 1100
    light.data.shape = "DISK"
    light.data.size = 6
    light.rotation_euler = (Vector((0, 0, 2)) - light.location).to_track_quat("-Z", "Y").to_euler()

    # Native camera yaw70 minus placed tree yaw10, pitch20. Orthographic framing
    # is used only to match the observed 310px tree height exactly, not to claim
    # identical perspective, Unity lighting, compression or MSAA behavior.
    pitch, yaw = math.radians(20), math.radians(60)
    backward = Vector((-math.sin(yaw) * math.cos(pitch), -math.cos(yaw) * math.cos(pitch), math.sin(pitch)))
    focus = Vector((0, 0, 2.1))
    bpy.ops.object.camera_add(location=focus + backward * 15)
    camera = bpy.context.object
    camera.rotation_euler = (-backward).to_track_quat("-Z", "Y").to_euler()
    camera.data.type = "ORTHO"
    up = camera.rotation_euler.to_matrix() @ Vector((0, 1, 0))
    heights = [p.dot(up) for p in points]
    # Blender's orthographic scale follows the larger sensor/image dimension;
    # this landscape image therefore uses its width, not its height.
    camera.data.ortho_scale = (max(heights) - min(heights)) * 640 / 310
    scene.camera = camera
    OUTPUT.mkdir(parents=True, exist_ok=True)
    for bounces in (8, 64):
        scene.cycles.transparent_max_bounces = bounces
        scene.render.filepath = str(OUTPUT / f"tree-p20-310px-b{bounces}.png")
        bpy.ops.render.render(write_still=True)
    record = {"source": str(SOURCE.relative_to(ROOT)), "sha256": hashlib.sha256(SOURCE.read_bytes()).hexdigest(),
              "triangles": sum(len(p["triangles"]) // 3 for p in data["parts"]), "pitch": 20,
              "treeRelativeYaw": 60, "projectedHeightPixels": 310, "imageSize": [640, 480],
              "transparentBounces": [8, 64], "projection": "orthographic size-matched diagnostic, not a Unity render",
              "blenderVersion": bpy.app.version_string}
    (OUTPUT / "review.json").write_text(json.dumps(record, indent=2) + "\n")
    print(json.dumps(record), flush=True)


if __name__ == "__main__":
    main()
