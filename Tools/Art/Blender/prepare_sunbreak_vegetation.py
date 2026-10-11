#!/usr/bin/env python3
"""Deterministic CC0 vegetation derivative; run with Blender 5.2 --background.

Original delivery bytes are never rewritten. Tree foliage uses two orthographic
cards baked from each intact author LOD1 cluster; only woody surfaces are
decimated. Unity-axis buffers preserve source UVs/normals or documented baked
cluster UVs. Color, alpha, normal and roughness derive from actual source channels.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "SourceArt/Environment/SunbreakVegetationV1"


def hash_record(path, role=None):
    record = {"path": str(path.relative_to(ROOT)), "bytes": path.stat().st_size,
              "sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
    if role: record["role"] = role
    return record


def validate_originals():
    for directory in ("TreeSmall02", "GrassMedium01", "Fern02"):
        folder = ROOT / "Assets/ThirdParty/PolyHaven" / directory
        manifest = json.loads((folder / "manifest.json").read_text())
        for item in manifest["files"]:
            data = (folder / item["path"]).read_bytes()
            if len(data) != item["bytes"] or hashlib.sha256(data).hexdigest() != item["sha256"] or hashlib.md5(data).hexdigest() != item["md5"]:
                raise RuntimeError("Immutable source drift: " + item["path"])


def set_input(modifier, key, value):
    if hasattr(modifier, "properties"):
        getattr(modifier.properties.inputs, key).value = value
    else:
        modifier[key] = value


def build_tree(probe=False, bake=False):
    bpy.ops.wm.open_mainfile(filepath=str(ROOT / "Assets/ThirdParty/PolyHaven/TreeSmall02/tree_small_02_1k.blend.source"))
    source = bpy.data.objects["tree_small_02_geometry_nodes"]
    modifier = source.modifiers[0]
    if bake:
        bake_cluster_cards()
    else:
        replace_cluster_cards()
    for key, value in {"Input_32": bpy.data.collections["tree_small_02_leaves_LOD1"],
                       "Input_9": 23, "Input_34": 24, "Input_14": 14,
                       "Input_24": 9, "Input_26": 5, "Input_28": 3,
                       "Input_16": 0.08, "Input_31": 0.16}.items():
        set_input(modifier, key, value)
    source.hide_viewport = False
    source.hide_set(False)
    bpy.context.view_layer.update()
    mesh = bpy.data.meshes.new_from_object(source.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    mesh.calc_loop_triangles()
    counts = {(material.name if material else "NONE"): 0 for material in mesh.materials}
    for tri in mesh.loop_triangles:
        material = mesh.materials[tri.material_index]
        counts[material.name if material else "NONE"] += 1
    print("TREE_CANDIDATE", len(mesh.vertices), counts, flush=True)
    return mesh


def replace_cluster_cards():
    # Two depth-separated views per authored branch cluster. Original GN still
    # owns placement/rotation; this changes representation, not tree silhouette.
    for letter in "abcd":
        obj = bpy.data.objects[f"tree_small_02_leaves_{letter}_LOD1"]
        points = [v.co.copy() for v in obj.data.vertices]
        lo = Vector(tuple(min(v[i] for v in points) for i in range(3)))
        hi = Vector(tuple(max(v[i] for v in points) for i in range(3)))
        cx, cy, cz = (lo + hi) * .5
        extent = max(hi.x - lo.x, hi.y - lo.y) * 1.18
        # The transparent square retains physical cluster extent and UV border.
        verts = [(cx - extent/2, cy - extent/2, cz), (cx + extent/2, cy - extent/2, cz),
                 (cx + extent/2, cy + extent/2, cz), (cx - extent/2, cy + extent/2, cz),
                 (cx - extent/2, cy, cz - extent/2), (cx + extent/2, cy, cz - extent/2),
                 (cx + extent/2, cy, cz + extent/2), (cx - extent/2, cy, cz + extent/2)]
        mesh = bpy.data.meshes.new("ClusterCards" + letter)
        mesh.from_pydata(verts, [], [(0, 1, 2, 3), (4, 5, 6, 7)])
        uv = mesh.uv_layers.new(name="UVMap")
        coords = [(0,0),(.5,0),(.5,1),(0,1),(.5,0),(1,0),(1,1),(.5,1)]
        for index, value in enumerate(coords):
            uv.data[index].uv = value
        # One common atlas material: four cluster variants tile vertically.
        row = "abcd".index(letter)
        for item in uv.data:
            item.uv.y = (item.uv.y + row) * .25
        mesh.materials.append(bpy.data.materials["tree_small_02_leaves"])
        obj.data = mesh


def bake_cluster_cards():
    # Pure emission color plus actual source alpha; no baked scene lighting/AO.
    # Atlases are assembled from actual renders, never painted. Normal RGB is
    # measured source tangent normal transformed into each card camera basis.
    folder = ROOT / "Assets/ThirdParty/PolyHaven/TreeSmall02/Derived"
    folder.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.cycles.device = "CPU"
    scene.cycles.seed = 0
    scene.cycles.use_animated_seed = False
    scene.cycles.use_adaptive_sampling = False
    scene.render.film_transparent = True
    scene.render.resolution_x = 512; scene.render.resolution_y = 512
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = "Standard"
    scene.view_settings.look = "None"
    scene.view_settings.exposure = 0
    scene.view_settings.gamma = 1
    for obj in bpy.data.objects:
        obj.hide_render = True
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    scene.camera = camera
    mat = bpy.data.materials.new("MeasuredLeafEmission")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    nodes.clear()
    output = nodes.new("ShaderNodeOutputMaterial")
    emission = nodes.new("ShaderNodeEmission")
    transparent = nodes.new("ShaderNodeBsdfTransparent")
    mix = nodes.new("ShaderNodeMixShader")
    color = nodes.new("ShaderNodeTexImage")
    color.image = bpy.data.images.load(str(ROOT / "Assets/ThirdParty/PolyHaven/TreeSmall02/tree_small_02_leaves_diff_2k.jpg"))
    normal_texture = nodes.new("ShaderNodeTexImage")
    normal_texture.image = bpy.data.images.load(str(ROOT / "Assets/ThirdParty/PolyHaven/TreeSmall02/tree_small_02_leaves_nor_gl_2k.jpg"))
    normal_texture.image.colorspace_settings.name = "Non-Color"
    normal_map = nodes.new("ShaderNodeNormalMap")
    normal_to_camera = nodes.new("ShaderNodeVectorTransform")
    normal_to_camera.vector_type = "NORMAL"
    normal_to_camera.convert_from = "WORLD"
    normal_to_camera.convert_to = "CAMERA"
    separate_normal = nodes.new("ShaderNodeSeparateXYZ")
    back_normal = nodes.new("ShaderNodeMath"); back_normal.operation = "LESS_THAN"
    back_normal.inputs[1].default_value = 0
    hemisphere_sign = nodes.new("ShaderNodeMath"); hemisphere_sign.operation = "MULTIPLY_ADD"
    hemisphere_sign.inputs[1].default_value = -2; hemisphere_sign.inputs[2].default_value = 1
    face_normal = nodes.new("ShaderNodeVectorMath"); face_normal.operation = "SCALE"
    normal_half = nodes.new("ShaderNodeVectorMath"); normal_half.operation = "SCALE"
    normal_half.inputs[3].default_value = .5
    normal_bias = nodes.new("ShaderNodeVectorMath"); normal_bias.operation = "ADD"
    normal_bias.inputs[1].default_value = (.5,.5,.5)
    roughness = nodes.new("ShaderNodeTexImage")
    roughness.image = bpy.data.images.load(str(ROOT / "Assets/ThirdParty/PolyHaven/TreeSmall02/tree_small_02_leaves_rough_2k.jpg"))
    roughness.image.colorspace_settings.name = "Non-Color"
    alpha = nodes.new("ShaderNodeTexImage")
    alpha.image = bpy.data.images.load(str(ROOT / "Assets/ThirdParty/PolyHaven/TreeSmall02/tree_small_02_leaves_alpha_2k.png"))
    alpha.image.colorspace_settings.name = "Non-Color"
    links = mat.node_tree.links
    links.new(normal_texture.outputs["Color"], normal_map.inputs["Color"])
    links.new(normal_map.outputs["Normal"], normal_to_camera.inputs[0])
    links.new(normal_to_camera.outputs[0], separate_normal.inputs[0])
    links.new(separate_normal.outputs["Z"], back_normal.inputs[0])
    links.new(back_normal.outputs[0], hemisphere_sign.inputs[0])
    links.new(normal_to_camera.outputs[0], face_normal.inputs[0])
    links.new(hemisphere_sign.outputs[0], face_normal.inputs[3])
    links.new(face_normal.outputs[0], normal_half.inputs[0])
    links.new(normal_half.outputs[0], normal_bias.inputs[0])
    links.new(color.outputs["Color"], emission.inputs[0]); links.new(alpha.outputs["Color"], mix.inputs[0])
    links.new(transparent.outputs[0], mix.inputs[1]); links.new(emission.outputs[0], mix.inputs[2]); links.new(mix.outputs[0], output.inputs[0])
    import numpy as np
    atlases = {channel: np.zeros((2048, 1024, 4), dtype=np.float32) for channel in ("Color", "Normal", "Roughness")}
    for row, letter in enumerate("abcd"):
        source = bpy.data.objects[f"tree_small_02_leaves_{letter}_LOD1"]
        obj = bpy.data.objects.new("BakeCluster" + letter, source.data.copy())
        scene.collection.objects.link(obj)
        obj.hide_render = False
        original_materials = list(obj.data.materials)
        obj.data.materials.clear(); obj.data.materials.append(mat)
        points = [v.co.copy() for v in obj.data.vertices]
        lo = Vector(tuple(min(v[i] for v in points) for i in range(3)))
        hi = Vector(tuple(max(v[i] for v in points) for i in range(3)))
        center = (lo + hi) * .5
        camera.data.ortho_scale = max(hi.x - lo.x, hi.y - lo.y) * 1.04
        for column, offset in enumerate([Vector((0,0,2)), Vector((0,-2,0))]):
            camera.location = center + offset
            camera.rotation_euler = (-offset).to_track_quat("-Z", "Y").to_euler()
            raw_folder = OUT / "ClusterBakes"
            raw_folder.mkdir(parents=True, exist_ok=True)
            for channel, output_socket in [("Color", color.outputs["Color"]), ("Normal", normal_bias.outputs[0]), ("Roughness", roughness.outputs["Color"])]:
                scene.view_settings.view_transform = "Standard" if channel == "Color" else "Raw"
                links.new(output_socket, emission.inputs[0])
                scene.render.filepath = str(raw_folder / f"Cluster{letter}{column}{channel}.png")
                bpy.ops.render.render(write_still=True)
                baked = bpy.data.images.load(scene.render.filepath, check_existing=False)
                if channel != "Color": baked.colorspace_settings.name = "Non-Color"
                atlases[channel][row*512:(row+1)*512, column*512:(column+1)*512] = np.array(baked.pixels[:], dtype=np.float32).reshape((512,512,4))
                bpy.data.images.remove(baked)
        obj.data.materials.clear()
        for original in original_materials: obj.data.materials.append(original)
        obj.hide_render = True
    for channel, atlas in atlases.items():
        filename = "TreeClusterAtlas.png" if channel == "Color" else "TreeCluster" + channel + ".png"
        image = bpy.data.images.new(filename, width=1024, height=2048, alpha=True)
        if channel != "Color": image.colorspace_settings.name = "Non-Color"
        image.pixels.foreach_set(atlas.ravel())
        image.file_format = "PNG"
        image.filepath_raw = str(folder / filename)
        image.save()
    replace_cluster_cards()


def extract_part(mesh, material_index, name):
    mesh.calc_loop_triangles()
    vertices, normals, uvs, triangles = [], [], [], []
    for tri in mesh.loop_triangles:
        if tri.material_index != material_index:
            continue
        face = []
        for loop_index in tri.loops:
            loop = mesh.loops[loop_index]
            face.append(len(vertices))
            vertices.append(tuple(mesh.vertices[loop.vertex_index].co))
            normals.append(tuple(mesh.corner_normals[loop_index].vector))
            uvs.append(tuple(mesh.uv_layers.active.data[loop_index].uv))
        triangles.append(face)
    result = bpy.data.meshes.new(name)
    result.from_pydata(vertices, [], triangles)
    layer = result.uv_layers.new(name="UVMap")
    for i, uv in enumerate(uvs):
        layer.data[i].uv = uv
    result.normals_split_custom_set(normals)
    result.materials.append(mesh.materials[material_index])
    result.update()
    obj = bpy.data.objects.new(name, result)
    bpy.context.collection.objects.link(obj)
    return obj


def simplify_wood(obj, budget):
    # Weld the split export corners before COLLAPSE, retaining UV seams.
    import bmesh
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=0.000001)
    bm.to_mesh(obj.data)
    bm.free()
    obj.data.calc_loop_triangles()
    modifier = obj.modifiers.new("WoodyOnlyMobileReduction", "DECIMATE")
    modifier.ratio = min(1.0, budget / len(obj.data.loop_triangles))
    modifier.use_collapse_triangulate = True
    bpy.context.view_layer.objects.active = obj
    obj.select_set(True)
    bpy.ops.object.modifier_apply(modifier=modifier.name)
    obj.select_set(False)


def bind_preview_material(obj, role):
    directory, stem = (("TreeSmall02", "tree_small_02"))
    suffix = {"TreeTrunk": "", "TreeBranch": "_branch", "TreeLeaf": "_leaves"}[role]
    mat = bpy.data.materials.new(role + "_Preview")
    mat.use_nodes = True
    nodes = mat.node_tree.nodes
    shader = nodes.get("Principled BSDF")
    image = nodes.new("ShaderNodeTexImage")
    image.image = bpy.data.images.load(str(ROOT / ("Assets/ThirdParty/PolyHaven/TreeSmall02/Derived/TreeClusterAtlas.png" if role == "TreeLeaf" else f"Assets/ThirdParty/PolyHaven/{directory}/{stem}{suffix}_diff_2k.jpg")))
    mat.node_tree.links.new(image.outputs["Color"], shader.inputs["Base Color"])
    shader.inputs["Roughness"].default_value = 0.8
    if role == "TreeLeaf":
        threshold = nodes.new("ShaderNodeMath")
        threshold.operation = "GREATER_THAN"
        threshold.inputs[1].default_value = 0.4
        mat.node_tree.links.new(image.outputs["Alpha"], threshold.inputs[0])
        mat.node_tree.links.new(threshold.outputs[0], shader.inputs["Alpha"])
        normal = nodes.new("ShaderNodeTexImage")
        normal.image = bpy.data.images.load(str(ROOT / "Assets/ThirdParty/PolyHaven/TreeSmall02/Derived/TreeClusterNormal.png"))
        normal.image.colorspace_settings.name = "Non-Color"
        normal_map = nodes.new("ShaderNodeNormalMap")
        mat.node_tree.links.new(normal.outputs["Color"], normal_map.inputs["Color"])
        mat.node_tree.links.new(normal_map.outputs[0], shader.inputs["Normal"])
        roughness = nodes.new("ShaderNodeTexImage")
        roughness.image = bpy.data.images.load(str(ROOT / "Assets/ThirdParty/PolyHaven/TreeSmall02/Derived/TreeClusterRoughness.png"))
        roughness.image.colorspace_settings.name = "Non-Color"
        mat.node_tree.links.new(roughness.outputs["Color"], shader.inputs["Roughness"])
    obj.data.materials.clear()
    obj.data.materials.append(mat)


def tree_parts(bake=False):
    mesh = build_tree(bake=bake)
    parts = []
    for index, material in enumerate(mesh.materials):
        if not material or material.name == "tree_small_02_branches":
            continue
        role = {"tree_small_02_trunk": "TreeTrunk", "tree_small_02_branches": "TreeBranch", "tree_small_02_leaves": "TreeLeaf"}[material.name]
        obj = extract_part(mesh, index, role)
        if role != "TreeLeaf":
            simplify_wood(obj, 3000)
        parts.append((obj, role))
    for obj in bpy.data.objects:
        obj.hide_render = obj not in [p[0] for p in parts]
    return parts


def render_tree(parts):
    for obj, role in parts:
        bind_preview_material(obj, role)
    bpy.ops.object.light_add(type="AREA", location=(3, -4, 8))
    light = bpy.context.object
    light.data.energy = 1100
    light.data.shape = "DISK"
    light.data.size = 6
    light.rotation_euler = (Vector((0, 0, 2)) - light.location).to_track_quat("-Z", "Y").to_euler()
    bpy.ops.object.camera_add()
    camera = bpy.context.object
    camera.data.type = "ORTHO"
    camera.data.ortho_scale = 5.7
    scene = bpy.context.scene
    scene.camera = camera
    scene.render.engine = "CYCLES"
    scene.cycles.samples = 16
    scene.render.resolution_x = 768
    scene.render.resolution_y = 768
    scene.render.resolution_percentage = 100
    scene.world.use_nodes = True
    scene.world.node_tree.nodes.get("Background").inputs[0].default_value = (0.45, 0.48, 0.52, 1)
    scene.world.node_tree.nodes.get("Background").inputs[1].default_value = 0.65
    scene.view_settings.view_transform = "AgX"
    folder = ROOT / "build/review/sunbreak-vegetation-dcc"
    folder.mkdir(parents=True, exist_ok=True)
    for index, position in enumerate([(7, -9, 6), (-8, -6, 5)]):
        camera.location = position
        camera.rotation_euler = (Vector((0, 0, 2.1)) - camera.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(folder / f"tree-{index + 1}.png")
        bpy.ops.render.render(write_still=True)


def export_selection(selection, parts):
    # Blender Z-up right handed -> Unity Y-up left handed. Reflection requires
    # reversing winding; UVs are unchanged and corner normals transformed once.
    points = [obj.matrix_world.to_3x3() @ v.co for obj, _ in parts for v in obj.data.vertices]
    pivot = Vector(((min(v.x for v in points) + max(v.x for v in points)) * .5,
                    (min(v.y for v in points) + max(v.y for v in points)) * .5,
                    min(v.z for v in points)))
    result = {"schemaVersion": 1, "selection": selection, "axis": "Unity Y-up meters; XZ-centered minY pivot", "parts": []}
    for obj, role in parts:
        data = obj.data
        data.calc_loop_triangles()
        vertices, normals, uvs, triangles = [], [], [], []
        index_by_corner = {}
        normal_matrix = obj.matrix_world.to_3x3().inverted().transposed()
        for tri in data.loop_triangles:
            face = []
            for loop_index in tri.loops:
                loop = data.loops[loop_index]
                point = obj.matrix_world.to_3x3() @ data.vertices[loop.vertex_index].co - pivot
                normal = (normal_matrix @ data.corner_normals[loop_index].vector).normalized()
                uv = data.uv_layers.active.data[loop_index].uv
                key = tuple(round(x, 7) for x in (point.x, point.z, point.y, normal.x, normal.z, normal.y, uv.x, uv.y))
                if key not in index_by_corner:
                    index_by_corner[key] = len(vertices) // 3
                    vertices.extend(key[:3]); normals.extend(key[3:6]); uvs.extend(key[6:])
                face.append(index_by_corner[key])
            triangles.extend((face[0], face[2], face[1]))
        part = {"role": role, "vertices": vertices, "normals": normals, "uv": uvs, "triangles": triangles}
        result["parts"].append(part)
    OUT.mkdir(parents=True, exist_ok=True)
    destination = OUT / (selection + ".json")
    destination.write_text(json.dumps(result, separators=(",", ":")) + "\n")
    metrics = {"selection": selection, "path": str(destination.relative_to(ROOT)), "bytes": destination.stat().st_size,
               "sha256": hashlib.sha256(destination.read_bytes()).hexdigest(),
               "parts": [{"role": p["role"], "triangles": len(p["triangles"]) // 3, "vertices": len(p["vertices"]) // 3} for p in result["parts"]],
               "boundsSize": [round(max(v.x for v in points) - min(v.x for v in points), 6), round(max(v.z for v in points) - min(v.z for v in points), 6), round(max(v.y for v in points) - min(v.y for v in points), 6)]}
    print("EXPORT", json.dumps(metrics), flush=True)
    return metrics


def import_small_plants():
    metrics = []
    for directory, stem, choices, role in [
        ("GrassMedium01", "grass_medium_01", {f"grass_medium_01_tiny_{c}_LOD0": "GrassTiny" + c.upper() for c in "abcdef"}, "Grass"),
        ("Fern02", "fern_02", {"fern_02_a": "Fern02a"}, "Fern")]:
        bpy.ops.wm.read_factory_settings(use_empty=True)
        bpy.ops.import_scene.fbx(filepath=str(ROOT / f"Assets/ThirdParty/PolyHaven/{directory}/{stem}_1k.fbx.source"), use_anim=False)
        for source_name, selection in choices.items():
            obj = bpy.data.objects.get(source_name)
            if obj is None:
                raise RuntimeError(f"Missing exact source node {source_name}; {[o.name for o in bpy.data.objects]}")
            metrics.append(export_selection(selection, [(obj, role)]))
    return metrics


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--probe", action="store_true")
    parser.add_argument("--render", action="store_true")
    parser.add_argument("--bake", action="store_true")
    args = parser.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])
    validate_originals()
    if args.probe:
        build_tree(True)
        return
    parts = tree_parts(args.bake)
    metrics = [export_selection("TreeSmall02", parts)]
    if args.render:
        render_tree(parts)
    metrics.extend(import_small_plants())
    atlas_folder = ROOT / "Assets/ThirdParty/PolyHaven/TreeSmall02/Derived"
    manifest = {"schemaVersion": 1, "intakeId": "POLYHAVEN-2026-005", "status": "TECH_REVIEW", "blenderVersion": bpy.app.version_string,
        "procedure": "Tools/Art/Blender/prepare_sunbreak_vegetation.py", "procedureSha256": hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        "atlases": [hash_record(atlas_folder / name, role) for name, role in [
            ("TreeClusterAtlas.png", "TreeLeafDiffuse"), ("TreeClusterNormal.png", "TreeLeafNormal"), ("TreeClusterRoughness.png", "TreeLeafRoughness")]],
        "originalManifests": [hash_record(ROOT / "Assets/ThirdParty/PolyHaven" / folder / "manifest.json") for folder in ("TreeSmall02", "GrassMedium01", "Fern02")],
        "settings": {"treeTrunkBudget": 3000, "treeLeaves": "two perpendicular cards per source LOD1 cluster; 4 variants; original Geometry Nodes placement",
            "trunkResolution": 23, "branchResolution": 24, "twigResolution": 14, "twigProbability": .08, "leafGrowth": .16,
            "clusterVisualScale": "1.18 / 1.04 = 1.134615 relative to exact orthographic bake bounds", "clusterAtlasSize": "1024 x 2048; eight 512-square views",
            "normals": "measured GL tangent normals transformed WORLD to CAMERA, oriented to positive-Z card hemisphere; encoded linear RGB (normal * 0.5 + 0.5)",
            "roughness": "measured source channel rendered unlit/Raw; no inferred AO or height", "omittedGeometry": "fine Geometry Nodes twigs; source scanned trunk carries primary branches"},
        "selections": metrics}
    (OUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")


if __name__ == "__main__":
    main()
