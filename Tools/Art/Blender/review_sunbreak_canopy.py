#!/usr/bin/env python3
"""Compare a bounded local canopy derivative; never overwrite immutable V1.

Blender --background --python Tools/Art/Blender/review_sunbreak_canopy.py
The same cluster-envelope expansion is implemented by SunbreakCanopyAuthoring.
This diagnostic uses 64 transparent bounces and gameplay-size 310px trees.
"""
import hashlib
import json
import math
from pathlib import Path

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
SOURCE = ROOT / "SourceArt/Environment/SunbreakVegetationV1/TreeSmall02.json"
OUTPUT = ROOT / "build/review/sunbreak-canopy-v2"


def hull(points):
    unique = sorted(set((p[0], p[2]) for p in points))
    def cross(a, b, c):
        return (b[0]-a[0])*(c[1]-a[1])-(b[1]-a[1])*(c[0]-a[0])
    lower, upper = [], []
    for p in unique:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    for p in reversed(unique):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def expand(part, maximum):
    original = [Vector(part["vertices"][i:i+3]) for i in range(0, len(part["vertices"]), 3)]
    boundary = hull(original)
    min_y, max_y = min(p.y for p in original), max(p.y for p in original)
    indices = part["triangles"]
    points = [None] * len(indices)
    factors = []
    for start in range(0, len(indices), 12):
        center = (original[indices[start]] + original[indices[start+1]]) * .5
        factor = maximum
        vertices = set(indices[start:start+12])
        for index in vertices:
            offset = original[index] - center
            if offset.y > 1e-8: factor = min(factor, (max_y-center.y)/offset.y)
            elif offset.y < -1e-8: factor = min(factor, (min_y-center.y)/offset.y)
            for h, a in enumerate(boundary):
                b = boundary[(h+1) % len(boundary)]
                nx, nz = b[1]-a[1], a[0]-b[0]
                direction = nx*offset.x+nz*offset.z
                if direction > 1e-8:
                    factor = min(factor, (nx*(a[0]-center.x)+nz*(a[1]-center.z))/direction)
        factor = max(1, factor)
        factors.append(factor)
        for corner in range(start, start+12):
            points[corner] = center+(original[indices[corner]]-center)*factor
    return points, {"minimum": min(factors), "mean": sum(factors)/len(factors), "maximum": max(factors)}


def main():
    binding = ROOT / "Tools/Art/Blender/prepare_sunbreak_vegetation.py"
    namespace = {"__name__": "review_only", "__file__": str(binding)}
    exec(compile(binding.read_text(), str(binding), "exec"), namespace)
    data = json.loads(SOURCE.read_text())
    OUTPUT.mkdir(parents=True, exist_ok=True)
    records = []
    for factor in (1.0, 1.6, 1.9):
        bpy.ops.wm.read_factory_settings(use_empty=True)
        all_points, metrics = [], None
        for part in data["parts"]:
            if part["role"] == "TreeLeaf":
                points, metrics = expand(part, factor)
            else:
                points = [Vector(part["vertices"][i:i+3]) for i in range(0, len(part["vertices"]), 3)]
            vertices = [(p.x, p.z, p.y) for p in points]
            indices = list(range(len(part["triangles"]))) if part["role"] == "TreeLeaf" else part["triangles"]
            faces = [(indices[i], indices[i+2], indices[i+1]) for i in range(0, len(indices), 3)]
            mesh = bpy.data.meshes.new(part["role"])
            mesh.from_pydata(vertices, [], faces)
            layer = mesh.uv_layers.new(name="UVMap")
            normals = []
            for loop in mesh.loops:
                index = part["triangles"][loop.vertex_index] if part["role"] == "TreeLeaf" else loop.vertex_index
                layer.data[loop.index].uv = part["uv"][index*2:index*2+2]
                offset = index*3
                normals.append((part["normals"][offset],part["normals"][offset+2],part["normals"][offset+1]))
            mesh.normals_split_custom_set(normals)
            obj = bpy.data.objects.new(part["role"], mesh)
            bpy.context.scene.collection.objects.link(obj)
            namespace["bind_preview_material"](obj, part["role"])
            all_points.extend(Vector(p) for p in vertices)
        scene = bpy.context.scene
        scene.render.engine = "CYCLES"
        scene.cycles.device = "CPU"
        scene.cycles.samples = 32
        scene.cycles.seed = 0
        scene.cycles.use_animated_seed = False
        scene.cycles.use_adaptive_sampling = False
        scene.cycles.transparent_max_bounces = 64
        scene.render.film_transparent = True
        scene.render.image_settings.color_mode = "RGBA"
        scene.render.resolution_x = 640
        scene.render.resolution_y = 480
        scene.render.resolution_percentage = 100
        scene.view_settings.view_transform = "AgX"
        scene.world = bpy.data.worlds.new("NeutralReviewWorld")
        scene.world.use_nodes = True
        background = scene.world.node_tree.nodes.get("Background")
        background.inputs[0].default_value = (.28,.32,.34,1)
        background.inputs[1].default_value = .8
        bpy.ops.object.light_add(type="AREA", location=(3,-4,8))
        light = bpy.context.object
        light.data.energy = 1100
        light.data.shape = "DISK"
        light.data.size = 6
        light.rotation_euler = (Vector((0,0,2))-light.location).to_track_quat("-Z","Y").to_euler()
        pitch, yaw = math.radians(20), math.radians(60)
        backward = Vector((-math.sin(yaw)*math.cos(pitch),-math.cos(yaw)*math.cos(pitch),math.sin(pitch)))
        focus = Vector((0,0,2.1))
        bpy.ops.object.camera_add(location=focus+backward*15)
        camera = bpy.context.object
        camera.rotation_euler = (-backward).to_track_quat("-Z","Y").to_euler()
        camera.data.type = "ORTHO"
        up = camera.rotation_euler.to_matrix() @ Vector((0,1,0))
        heights = [p.dot(up) for p in all_points]
        camera.data.ortho_scale = (max(heights)-min(heights))*640/310
        scene.camera = camera
        destination = OUTPUT / f"tree-cluster-{factor:.1f}-b64.png"
        scene.render.filepath = str(destination)
        bpy.ops.render.render(write_still=True)
        rendered = bpy.data.images.load(str(destination), check_existing=False)
        alpha = rendered.pixels[3::4]
        metrics.update({"factor": factor, "alphaAreaPixels": sum(alpha), "solidPixels": sum(a>.5 for a in alpha)})
        records.append(metrics)
        print("CANOPY_REVIEW", json.dumps(metrics), flush=True)
    (OUTPUT / "review.json").write_text(json.dumps({"source":str(SOURCE.relative_to(ROOT)),
        "sha256":hashlib.sha256(SOURCE.read_bytes()).hexdigest(), "triangles":9328,
        "imageSize":[640,480], "transparentBounces":64, "projectedHeightPixels":310,
        "pitch":20, "relativeYaw":60, "projection":"orthographic diagnostic; not a Unity render", "variants":records}, indent=2)+"\n")


if __name__ == "__main__":
    main()
