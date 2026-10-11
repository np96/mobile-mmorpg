#!/usr/bin/env python3
"""Build the first fresh BodyV3-specific Hunter macro review.

This is an isolated source-only authoring checkpoint.  It reads the accepted
BodyV3 masculine body and the accepted static clothing-fit study, but the fit
study remains hidden and contributes no visible faces.  Every visible Hunter
family is rebuilt as a small independent cage.  The script deliberately stops
at family-colour clay evidence and a manual-review request: it writes no UVs,
PBR maps, LODs, FBX, Unity assets, production weights, or acceptance verdict.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass, field
import hashlib
import json
import math
from pathlib import Path
import sys
from typing import Iterable, Sequence

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


SCRIPT_PATH = Path(__file__).resolve()
SCRIPT_DIR = SCRIPT_PATH.parent
PROJECT_ROOT = SCRIPT_DIR.parents[2]
TOOLS_DIR = PROJECT_ROOT / "Tools/Art/Blender"
sys.path.insert(0, str(TOOLS_DIR))
import session_humanoid_base_v3_contract as body_contract


ASSET_ID = "hunter-production-body-v3-v1-r1"
MODEL_NAME = "HunterProductionBodyV3V1_R1"
OUTPUT_ROOT = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1"
)

BODY_BLEND = body_contract.BLEND_PATH
BODY_MANIFEST = body_contract.MANIFEST_PATH
BODY_PROOF = body_contract.DEFORMATION_REPORT
BODY_MANUAL = body_contract.MANUAL_REVIEW_PATH
FIT_ROOT = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/SessionHumanoidClothingFitStudyV1/Outputs"
)
FIT_BLEND = FIT_ROOT / "SessionHumanoidClothingFitStudyV1.blend"
FIT_REPORT = FIT_ROOT / "SessionHumanoidClothingFitStudyV1.report.json"
FIT_REVIEW = FIT_ROOT / "SessionHumanoidClothingFitStudyV1.internal-review.json"

EXPECTED_INPUTS = {
    BODY_BLEND: "ff30baebeb3cb03186cf1663691a65c743af16616c82003f4358b5a41fb08458",
    BODY_MANIFEST: "62aa4ce0ab1dc2caf554f1483bb38c2e01319f40ffa3940f6c6af60d995dd4ea",
    BODY_PROOF: "ef085d15a7dca7e4a0e357c74dd13a60c77969dc1c5db9da9c1843109046e719",
    BODY_MANUAL: "c6a08aca8b84c325031c8d9c85a4083e1de056ca2de2dfe8f9b5abef02c86f43",
    FIT_BLEND: "5d67e10c7b18969262c1ed0f02f85478b6951e445799acaa47fdb253b7e8d36f",
    FIT_REPORT: "1e5c8097b961666f3a1ed8285cf0ae80b88d03b73f9e7f88039cc6e93d98f1bc",
    FIT_REVIEW: "12aeb3f3653255a2d8e3eea8bf8a2392f9e1a9d8f56b067fbfbb5bfb56705ea1",
}

BODY_NAME = body_contract.BODY_NAMES["Masculine"]
RIG_NAME = body_contract.RIG_NAMES["Masculine"]
EYE_NAMES = body_contract.EYE_NAMES["Masculine"]
FIT_TOP_NAME = "BodyV3Fit_LongSleeveTop"
FIT_TROUSERS_NAME = "BodyV3Fit_Trousers"

EXPECTED_KIT_OBJECTS = {
    "HunterBodyV3_BaseGarments",
    "HunterBodyV3_HoodHat",
    "HunterBodyV3_Cowl",
    "HunterBodyV3_TailoredBrigandine",
    "HunterBodyV3_AsymmetricMantle",
    "HunterBodyV3_LayeredPauldrons",
    "HunterBodyV3_SplitCape",
    "HunterBodyV3_LimbGear",
    "HunterBodyV3_Boots",
    "HunterBodyV3_HarnessBelt",
    "HunterBodyV3_RecurveBow",
    "HunterBodyV3_QuiverArrows",
    "HunterBodyV3_StaticString",
    "HunterBodyV3_DrawString",
    "HunterBodyV3_NockedArrow",
}

FAMILY_TARGETS = {
    "base_garments": (1_200, 4_500),
    "hood_hat": (500, 1_500),
    "cowl": (350, 1_200),
    "brigandine": (500, 1_800),
    "mantle_pauldrons": (900, 2_800),
    "split_cape": (500, 1_600),
    "limb_gear": (1_000, 3_200),
    "harness_belt": (300, 1_300),
    "weapon_quiver": (700, 2_500),
}


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", default=str(OUTPUT_ROOT))
    parser.add_argument("--allow-overwrite", action="store_true")
    return parser.parse_args(raw)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def relative(path: Path) -> str:
    try:
        return str(path.resolve().relative_to(PROJECT_ROOT.resolve()))
    except ValueError:
        return str(path.resolve())


def validate_output_root(path: Path) -> Path:
    result = path.expanduser().resolve()
    normalized = "/" + result.as_posix().strip("/") + "/"
    require("/Assets/" not in normalized, "Macro output may not enter Unity Assets")
    require(result != PROJECT_ROOT.resolve(), "Output may not be the project root")
    require(
        "/SourceArt/Blender/Characters/HunterProductionBodyV3V1/" in normalized,
        "R1 output must remain in the new HunterProductionBodyV3V1 character path",
    )
    return result


def verify_inputs() -> dict[str, str]:
    verified: dict[str, str] = {}
    for path, expected in EXPECTED_INPUTS.items():
        require(path.is_file(), f"Pinned accepted input is missing: {path}")
        actual = sha256(path)
        require(actual == expected, f"Pinned accepted input changed: {path}")
        verified[relative(path)] = actual
    body_review = json.loads(BODY_MANUAL.read_text(encoding="utf-8"))
    fit_report = json.loads(FIT_REPORT.read_text(encoding="utf-8"))
    fit_review = json.loads(FIT_REVIEW.read_text(encoding="utf-8"))
    require(body_review["status"] == "PASS_MANUAL_DEFORMATION_REVIEW", "BodyV3 is not accepted")
    require(
        fit_report["status"] == "PASS_STRUCTURAL_AND_CLEARANCE_PENDING_MANUAL_FIT_REVIEW",
        "Clothing-fit structural input is not the pinned accepted checkpoint",
    )
    require(
        fit_review["status"] == "PASS_STATIC_FIT_DIRECTION_USER_CONFIRMED",
        "Clothing-fit visual direction is not the pinned user-confirmed checkpoint",
    )
    return verified


@dataclass(frozen=True)
class Anatomy:
    shoulder: Vector
    elbow: Vector
    wrist: Vector
    hip: Vector
    knee: Vector
    ankle: Vector
    toes: Vector
    head_top: Vector
    neck: Vector

    def as_json(self) -> dict[str, list[float]]:
        return {
            name: [round(float(value), 6) for value in getattr(self, name)]
            for name in (
                "shoulder", "elbow", "wrist", "hip", "knee", "ankle",
                "toes", "head_top", "neck",
            )
        }


@dataclass
class Buffers:
    vertices: list[Vector] = field(default_factory=list)
    faces: list[tuple[int, ...]] = field(default_factory=list)

    def add(
        self,
        vertices: Sequence[Vector],
        faces: Sequence[Sequence[int]],
    ) -> None:
        offset = len(self.vertices)
        self.vertices.extend(Vector(value) for value in vertices)
        self.faces.extend(tuple(offset + int(index) for index in face) for face in faces)


class Surface:
    def __init__(self, obj: bpy.types.Object):
        require(obj.type == "MESH", f"Surface source must be a mesh: {obj.name}")
        obj.data.calc_loop_triangles()
        vertices = [vertex.co.copy() for vertex in obj.data.vertices]
        triangles = [tuple(triangle.vertices) for triangle in obj.data.loop_triangles]
        self.tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)

    def nearest(self, point: Vector) -> tuple[Vector, Vector, float]:
        nearest, normal, _index, distance = self.tree.find_nearest(point)
        require(nearest is not None and normal is not None and distance is not None, "Nearest query failed")
        return nearest, normal.normalized(), float(distance)

    def ray(self, origin: Vector, direction: Vector, maximum: float = 1.0) -> Vector:
        normalized = direction.normalized()
        location, _normal, _index, _distance = self.tree.ray_cast(origin, normalized, maximum)
        if location is not None:
            return location
        # The accepted fit envelopes have deliberate open cuff/hem boundaries.
        # A sparse authored station exactly on one of those boundaries may miss
        # even though its intended section is unambiguous.  Query a point along
        # the same authored ray and use only its nearest proxy point; this does
        # not copy proxy topology or add sampling density.
        probe = origin + normalized * min(0.12, maximum * 0.5)
        nearest, _normal, _distance = self.nearest(probe)
        projected = float((nearest - origin).dot(normalized))
        require(
            projected > -0.005,
            f"Fit-proxy ray and boundary fallback missed at {tuple(origin)} / {tuple(normalized)}",
        )
        return nearest

    def overlap_count(self, obj: bpy.types.Object) -> int:
        obj.data.calc_loop_triangles()
        other = BVHTree.FromPolygons(
            [vertex.co.copy() for vertex in obj.data.vertices],
            [tuple(triangle.vertices) for triangle in obj.data.loop_triangles],
            all_triangles=True,
        )
        return len(self.tree.overlap(other))


def loft(
    rings: Sequence[Sequence[Vector]],
    *,
    close_ring: bool,
    cap_start: bool = False,
    cap_end: bool = False,
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    require(len(rings) >= 2, "Loft needs at least two rings")
    width = len(rings[0])
    require(width >= 3 and all(len(ring) == width for ring in rings), "Loft ring mismatch")
    vertices = [Vector(point) for ring in rings for point in ring]
    faces: list[tuple[int, ...]] = []
    spans = width if close_ring else width - 1
    for row in range(len(rings) - 1):
        for column in range(spans):
            following = (column + 1) % width
            a = row * width + column
            b = row * width + following
            c = (row + 1) * width + following
            d = (row + 1) * width + column
            faces.append((a, b, c, d))
    if cap_start:
        faces.append(tuple(reversed(range(width))))
    if cap_end:
        offset = (len(rings) - 1) * width
        faces.append(tuple(offset + index for index in range(width)))
    return vertices, faces


def grid(rows: Sequence[Sequence[Vector]]) -> tuple[list[Vector], list[tuple[int, ...]]]:
    require(len(rows) >= 2, "Grid needs at least two rows")
    width = len(rows[0])
    require(width >= 2 and all(len(row) == width for row in rows), "Grid row mismatch")
    vertices = [Vector(point) for row in rows for point in row]
    faces = []
    for row in range(len(rows) - 1):
        for column in range(width - 1):
            a = row * width + column
            b = a + 1
            c = (row + 1) * width + column + 1
            d = (row + 1) * width + column
            faces.append((a, b, c, d))
    return vertices, faces


def prism(
    front: Sequence[Vector],
    back: Sequence[Vector],
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    require(len(front) == len(back) and len(front) >= 3, "Prism loop mismatch")
    count = len(front)
    vertices = [Vector(value) for value in (*front, *back)]
    faces: list[tuple[int, ...]] = [tuple(reversed(range(count))), tuple(count + i for i in range(count))]
    for index in range(count):
        following = (index + 1) % count
        faces.append((index, following, count + following, count + index))
    return vertices, faces


def mesh_object(
    name: str,
    family: str,
    buffers: Buffers,
    material: bpy.types.Material,
    construction: str,
    *,
    thickness: float | None = None,
    bevel: float | None = None,
    fit_audit: bool = False,
) -> bpy.types.Object:
    require(buffers.vertices and buffers.faces, f"Empty mesh: {name}")
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata([tuple(value) for value in buffers.vertices], [], buffers.faces)
    mesh.validate(verbose=False)
    mesh.update(calc_edges=True)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    obj.data.materials.append(material)
    obj["assetId"] = ASSET_ID
    obj["macroFamily"] = family
    obj["construction"] = construction
    obj["sourceOnlyMacro"] = True
    obj["promotionAllowed"] = False
    obj["bodyFitAudit"] = fit_audit
    if thickness is not None:
        modifier = obj.modifiers.new("AuthoredThickness", "SOLIDIFY")
        modifier.thickness = thickness
        modifier.offset = 1.0
        modifier.use_even_offset = True
        apply_modifier(obj, modifier.name)
    if bevel is not None and bevel > 0.0:
        modifier = obj.modifiers.new("SingleSegmentEdgeSoftening", "BEVEL")
        modifier.width = bevel
        modifier.segments = 1
        modifier.limit_method = "ANGLE"
        apply_modifier(obj, modifier.name)
    obj.data.validate(verbose=False)
    obj.data.update(calc_edges=True)
    return obj


def apply_modifier(obj: bpy.types.Object, name: str) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.modifier_apply(modifier=name)
    obj.select_set(False)


def material(name: str, color: tuple[float, float, float], roughness: float) -> bpy.types.Material:
    result = bpy.data.materials.new(name)
    result.diffuse_color = (*color, 1.0)
    result.use_nodes = True
    node = result.node_tree.nodes.get("Principled BSDF")
    require(node is not None, "Principled node missing")
    node.inputs["Base Color"].default_value = (*color, 1.0)
    node.inputs["Roughness"].default_value = roughness
    return result


def evaluated_static_copy(source: bpy.types.Object, name: str) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
    clone = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(clone)
    clone.matrix_world = source.matrix_world.copy()
    return clone


def load_accepted_sources() -> tuple[
    bpy.types.Object,
    tuple[bpy.types.Object, ...],
    bpy.types.Object,
    bpy.types.Object,
    Anatomy,
]:
    bpy.ops.wm.open_mainfile(filepath=str(BODY_BLEND), load_ui=False)
    body_contract.reset_pose(bpy.data.objects[RIG_NAME])
    bpy.context.view_layer.update()
    rig = bpy.data.objects[RIG_NAME]
    bones = rig.data.bones

    def head(name: str) -> Vector:
        return bones[name].head_local.copy()

    anatomy = Anatomy(
        shoulder=head("LeftUpperArm"),
        elbow=head("LeftLowerArm"),
        wrist=head("LeftHand"),
        hip=head("LeftUpperLeg"),
        knee=head("LeftLowerLeg"),
        ankle=head("LeftFoot"),
        toes=bones["LeftToes"].tail_local.copy(),
        head_top=bones["Head"].tail_local.copy(),
        neck=head("Neck"),
    )
    body = evaluated_static_copy(
        bpy.data.objects[BODY_NAME],
        "EvidenceOnly_HunterBodyV3_AcceptedMasculineBody",
    )
    eyes = tuple(
        evaluated_static_copy(bpy.data.objects[name], f"EvidenceOnly_HunterBodyV3_{name.rsplit('_', 1)[-1]}")
        for name in EYE_NAMES
    )
    keep = {body, *eyes}
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)
    for obj in (body, *eyes):
        obj["evidenceOnly"] = True
        obj["promotionAllowed"] = False
    body["acceptedBodyInput"] = True
    body["productionLineage"] = "Blender Foundation Human Base Meshes v1.4.1 stylized masculine"

    with bpy.data.libraries.load(str(FIT_BLEND), link=False) as (available, requested):
        wanted = {FIT_TOP_NAME, FIT_TROUSERS_NAME}
        require(wanted.issubset(set(available.objects)), "Pinned fit-study objects are missing")
        requested.objects = [name for name in available.objects if name in wanted]
    proxies = {obj.name: obj for obj in requested.objects if obj is not None}
    require(set(proxies) == {FIT_TOP_NAME, FIT_TROUSERS_NAME}, "Fit proxy append changed")
    for proxy in proxies.values():
        bpy.context.scene.collection.objects.link(proxy)
        proxy.hide_render = True
        proxy.hide_set(True)
        proxy["hiddenAcceptedFitProxy"] = True
        proxy["visibleFacesReused"] = False
        proxy["promotionAllowed"] = False
    return body, eyes, proxies[FIT_TOP_NAME], proxies[FIT_TROUSERS_NAME], anatomy


def radial_ring(
    surface: Surface,
    z: float,
    count: int,
    extra: float,
    *,
    front_ease: float = 0.0,
    side_ease: float = 0.0,
) -> list[Vector]:
    points = []
    for index in range(count):
        angle = 2.0 * math.pi * index / count
        direction = Vector((math.cos(angle), math.sin(angle), 0.0))
        point = surface.ray(Vector((0.0, 0.0, z)), direction, 0.8)
        front = max(0.0, -math.sin(angle))
        side = abs(math.cos(angle))
        points.append(point + direction * (extra + front_ease * front + side_ease * side))
    return points


def arm_ring(
    surface: Surface,
    x: float,
    side: int,
    count: int,
    extra: float,
    center_y: float,
    center_z: float,
    *,
    open_underside: bool = False,
) -> list[Vector]:
    center = Vector((side * x, center_y, center_z))
    if open_underside:
        gap = math.radians(48.0)
        start = -math.pi / 2.0 + gap / 2.0
        sweep = 2.0 * math.pi - gap
        angles = [start + sweep * index / (count - 1) for index in range(count)]
    else:
        angles = [2.0 * math.pi * index / count for index in range(count)]
    points = []
    for angle in angles:
        direction = Vector((0.0, math.cos(angle), math.sin(angle)))
        point = surface.ray(center, direction, 0.35)
        points.append(point + direction * extra)
    return points


def leg_ring(
    surface: Surface,
    z: float,
    side: int,
    count: int,
    extra: float,
    center_x: float,
    center_y: float,
    *,
    open_back: bool = False,
    front_crown: float = 0.0,
) -> list[Vector]:
    center = Vector((side * center_x, center_y, z))
    if open_back:
        gap = math.radians(58.0)
        start = math.pi / 2.0 + gap / 2.0
        sweep = 2.0 * math.pi - gap
        angles = [start + sweep * index / (count - 1) for index in range(count)]
    else:
        angles = [2.0 * math.pi * index / count for index in range(count)]
    points = []
    for angle in angles:
        direction = Vector((math.cos(angle), math.sin(angle), 0.0))
        point = surface.ray(center, direction, 0.35)
        front = max(0.0, -math.sin(angle))
        points.append(point + direction * (extra + front_crown * front))
    return points


def build_base_garments(
    top_surface: Surface,
    trouser_surface: Surface,
    anatomy: Anatomy,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    buffers = Buffers()
    torso_levels = (0.955, 1.025, 1.105, 1.185, 1.265, 1.335, 1.395)
    torso = [
        radial_ring(
            trouser_surface if z < 1.055 else top_surface,
            z,
            20,
            0.003,
            front_ease=0.004,
            side_ease=0.002,
        )
        for z in torso_levels
    ]
    buffers.add(*loft(torso, close_ring=True))

    arm_xs = (0.205, 0.270, 0.345, 0.420, 0.495, 0.570, 0.645, 0.710)
    for side in (-1, 1):
        rings = [
            arm_ring(
                top_surface, x, side, 12, 0.0035,
                anatomy.shoulder.y, anatomy.shoulder.z,
            )
            for x in arm_xs
        ]
        buffers.add(*loft(rings, close_ring=True))

    hip_levels = (0.955, 0.905, 0.855, 0.815)
    hip_rings = [radial_ring(trouser_surface, z, 20, 0.003) for z in hip_levels]
    buffers.add(*loft(hip_rings, close_ring=True))
    leg_levels = (0.805, 0.745, 0.675, 0.605, 0.535, 0.465, 0.395, 0.315, 0.235, 0.155, 0.115)
    for side in (-1, 1):
        rings = [
            leg_ring(
                trouser_surface, z, side, 12, 0.003,
                abs(anatomy.hip.x), anatomy.hip.y,
            )
            for z in leg_levels
        ]
        buffers.add(*loft(rings, close_ring=True))
    return mesh_object(
        "HunterBodyV3_BaseGarments", "base_garments", buffers, mat,
        "independent low torso, sleeve, pelvis, and leg cages anchored to the hidden accepted fit proxies",
        thickness=0.0035, bevel=0.0012, fit_audit=True,
    )


def build_hood_hat(mat: bpy.types.Material) -> bpy.types.Object:
    specs = (
        (1.475, 0.150, 0.135, 0.88, 0.000),
        (1.525, 0.158, 0.150, 0.84, 0.004),
        (1.585, 0.162, 0.162, 0.76, 0.010),
        (1.650, 0.158, 0.168, 0.63, 0.018),
        (1.710, 0.145, 0.160, 0.48, 0.030),
        (1.765, 0.120, 0.140, 0.30, 0.047),
        (1.810, 0.078, 0.105, 0.13, 0.062),
        (1.835, 0.024, 0.040, 0.03, 0.070),
    )
    rings = []
    count = 20
    for z, radius_x, radius_y, half_open, back_shift in specs:
        start = -math.pi / 2.0 + half_open
        end = 3.0 * math.pi / 2.0 - half_open
        ring = []
        for index in range(count):
            angle = start + (end - start) * index / (count - 1)
            back = max(0.0, math.sin(angle))
            x = radius_x * math.cos(angle)
            y = -0.018 + radius_y * math.sin(angle) + back_shift * (0.25 + 0.75 * back)
            ring.append(Vector((x, y, z)))
        rings.append(ring)
    buffers = Buffers()
    buffers.add(*loft(rings, close_ring=False, cap_end=True))
    obj = mesh_object(
        "HunterBodyV3_HoodHat", "hood_hat", buffers, mat,
        "one open soft hood-cap with a shrinking face aperture, closed crown, and rear crown droop",
        thickness=0.006, bevel=0.002, fit_audit=True,
    )
    obj["faceOpeningWidthTargetMeters"] = "0.14-0.16"
    obj["crownClosed"] = True
    obj["literalHatStackedOnHood"] = False
    return obj


def build_cowl(mat: bpy.types.Material) -> bpy.types.Object:
    count = 20
    authored_hem = (
        -0.006, -0.002, 0.004, 0.010, 0.014,
        0.010, 0.002, -0.008, -0.014, -0.008,
        0.004, 0.013, 0.018, 0.011, 0.001,
        -0.009, -0.014, -0.010, -0.004, -0.006,
    )
    specs = (
        (0.092, 0.082, 1.490, 0.00),
        (0.145, 0.115, 1.455, 0.25),
        (0.212, 0.150, 1.405, 0.55),
        (0.282, 0.185, 1.350, 1.00),
    )
    rings = []
    for radius_x, radius_y, base_z, hem_strength in specs:
        ring = []
        for index in range(count):
            angle = 2.0 * math.pi * index / count
            front = max(0.0, -math.sin(angle))
            back = max(0.0, math.sin(angle))
            left = max(0.0, math.cos(angle))
            z = base_z - 0.036 * front + 0.008 * back - 0.010 * left
            z += authored_hem[index] * hem_strength
            y = radius_y * math.sin(angle) - 0.010 * front
            ring.append(Vector((radius_x * math.cos(angle), y, z)))
        rings.append(ring)
    buffers = Buffers()
    buffers.add(*loft(rings, close_ring=True))
    obj = mesh_object(
        "HunterBodyV3_Cowl", "cowl", buffers, mat,
        "four nonuniform authored drape rings with a broad irregular hem; separate from hood and mantle",
        thickness=0.005, bevel=0.0015, fit_audit=False,
    )
    obj["separateFromHoodAndMantle"] = True
    return obj


def build_brigandine(body_surface: Surface, mat: bpy.types.Material) -> bpy.types.Object:
    # Three shallow lap transitions divide one continuous tailored shell into four rows.
    levels = (
        (1.390, 0.020),
        (1.305, 0.021),
        (1.297, 0.027),
        (1.212, 0.022),
        (1.204, 0.028),
        (1.117, 0.023),
        (1.109, 0.029),
        (1.018, 0.024),
    )
    rings = []
    count = 24
    for z, clearance in levels:
        ring = []
        for index in range(count):
            angle = 2.0 * math.pi * index / count
            direction = Vector((math.cos(angle), math.sin(angle), 0.0))
            front = max(0.0, -math.sin(angle))
            side = abs(math.cos(angle))
            curved_z = z + 0.012 * front * (1.0 - 0.45 * side)
            point = body_surface.ray(Vector((0.0, 0.0, curved_z)), direction, 0.7)
            ring.append(point + direction * (clearance + 0.002 * side))
        rings.append(ring)
    buffers = Buffers()
    buffers.add(*loft(rings, close_ring=True))
    obj = mesh_object(
        "HunterBodyV3_TailoredBrigandine", "brigandine", buffers, mat,
        "one tailored wrap shell with three shallow lap transitions defining exactly four broad rows",
        thickness=0.006, bevel=0.0018, fit_audit=True,
    )
    obj["brigandineRowCount"] = 4
    obj["inflatedIndependentBands"] = False
    return obj


def build_mantle(mat: bpy.types.Material) -> bpy.types.Object:
    rows = []
    across_profile = (-1.0, -0.74, -0.48, -0.22, 0.0, 0.23, 0.51, 0.77, 1.0)
    x_profile = (0.045, 0.100, 0.165, 0.235, 0.305, 0.365, 0.420)
    edge_drop = (0.000, 0.006, 0.014, 0.004, 0.018, 0.008, 0.022)
    for row_index, x in enumerate(x_profile):
        u = row_index / (len(x_profile) - 1)
        half_depth = 0.165 - 0.040 * u
        row = []
        for column, across in enumerate(across_profile):
            crown = max(0.0, 1.0 - across * across)
            y = 0.010 + half_depth * across
            y += (0.008, -0.004, 0.006, -0.006, 0.004, -0.003, 0.007, -0.005, 0.003)[column]
            z = 1.405 + 0.098 * crown * (1.0 - 0.28 * u) - 0.098 * (u ** 1.18)
            z -= edge_drop[row_index] * (0.35 + 0.65 * abs(across))
            row.append(Vector((x, y, z)))
        rows.append(row)
    buffers = Buffers()
    buffers.add(*grid(rows))
    obj = mesh_object(
        "HunterBodyV3_AsymmetricMantle", "mantle_pauldrons", buffers, mat,
        "left-biased crowned mantle with seven broad longitudinal masses and a non-spiked perimeter",
        thickness=0.014, bevel=0.003, fit_audit=False,
    )
    obj["furCardsUsed"] = False
    obj["asymmetricSide"] = "Left"
    return obj


def pauldron_patch(side: int, layer: int, total: int) -> tuple[list[Vector], list[tuple[int, ...]]]:
    if total == 3:
        ranges = ((0.165, 0.305, 1.395, 0.100), (0.225, 0.365, 1.365, 0.086), (0.290, 0.420, 1.335, 0.070))
    else:
        ranges = ((0.165, 0.315, 1.392, 0.094), (0.245, 0.370, 1.355, 0.074))
    inner, outer, base_z, crown_height = ranges[layer]
    across_values = (-1.0, -0.78, -0.53, -0.26, 0.0, 0.27, 0.55, 0.80, 1.0)
    rows = []
    for u_index in range(6):
        u = u_index / 5.0
        absolute_x = inner + (outer - inner) * u
        row = []
        for across in across_values:
            crown = max(0.0, 1.0 - across * across)
            depth = (0.122 - 0.013 * layer) * across
            y = depth - 0.013 * u - 0.004 * layer + 0.006 * crown * (0.5 - u)
            z = base_z + crown_height * crown - (0.050 + 0.008 * layer) * (u ** 1.28)
            row.append(Vector((side * absolute_x, y, z)))
        rows.append(row)
    return grid(rows)


def build_pauldrons(mat: bpy.types.Material) -> bpy.types.Object:
    buffers = Buffers()
    for side, count in ((1, 3), (-1, 2)):
        for layer in range(count):
            buffers.add(*pauldron_patch(side, layer, count))
    obj = mesh_object(
        "HunterBodyV3_LayeredPauldrons", "mantle_pauldrons", buffers, mat,
        "five closed crowned wedges in one family mesh: three left and two right with authored overlap",
        thickness=0.009, bevel=0.0025, fit_audit=False,
    )
    obj["leftShellCount"] = 3
    obj["rightShellCount"] = 2
    obj["shellsAreClosedCrownedWedges"] = True
    return obj


def build_split_cape(mat: bpy.types.Material) -> bpy.types.Object:
    buffers = Buffers()
    fold_left = (0.018, -0.012, 0.030, -0.021, 0.015, -0.008, 0.020)
    fold_right = (-0.012, 0.021, -0.017, 0.027, -0.010, 0.015, -0.018)
    row_sag = (0.000, 0.004, 0.010, 0.018, 0.028, 0.040, 0.053, 0.066)
    hem_shape = (0.018, 0.005, -0.010, 0.012, -0.006, 0.008, 0.020)
    for side, folds in ((1, fold_left), (-1, fold_right)):
        rows = []
        for row_index in range(8):
            v = row_index / 7.0
            z_base = 1.385 - 0.685 * v
            inner = 0.045 + 0.006 * v
            outer = 0.275 + 0.055 * v
            row = []
            for column in range(7):
                u = column / 6.0
                absolute_x = inner + (outer - inner) * u
                y = 0.152 + row_sag[row_index] + folds[column] * (0.28 + 0.72 * v)
                z = z_base - hem_shape[column] * (v ** 4)
                row.append(Vector((side * absolute_x, y, z)))
            rows.append(row)
        buffers.add(*grid(rows))
    obj = mesh_object(
        "HunterBodyV3_SplitCape", "split_cape", buffers, mat,
        "two short asymmetric drape cages with a persistent center gap, three broad folds, and shaped hems",
        thickness=0.005, bevel=0.0015, fit_audit=False,
    )
    obj["capeSectionCount"] = 2
    obj["minimumCenterGapMeters"] = 0.090
    return obj


def build_limb_gear(
    top_surface: Surface,
    trouser_surface: Surface,
    anatomy: Anatomy,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    buffers = Buffers()
    for side in (-1, 1):
        bracer_rings = [
            arm_ring(
                top_surface, x, side, 11, 0.008 + 0.002 * abs((x - 0.60) / 0.11),
                anatomy.shoulder.y, anatomy.shoulder.z,
                open_underside=True,
            )
            for x in (0.495, 0.545, 0.600, 0.655, 0.700)
        ]
        buffers.add(*loft(bracer_rings, close_ring=False))
        greave_rings = [
            leg_ring(
                trouser_surface, z, side, 11,
                0.009 + 0.002 * abs((z - 0.31) / 0.18),
                abs(anatomy.hip.x), anatomy.hip.y,
                open_back=True,
                front_crown=0.010 + 0.005 * (z / 0.49),
            )
            for z in (0.150, 0.225, 0.310, 0.400, 0.490)
        ]
        buffers.add(*loft(greave_rings, close_ring=False))
    obj = mesh_object(
        "HunterBodyV3_LimbGear", "limb_gear", buffers, mat,
        "paired open-underarm bracers and open-back greaves fitted from noncircular BodyV3 proxy sections",
        thickness=0.006, bevel=0.002, fit_audit=True,
    )
    obj["bracersClosedTubes"] = False
    obj["greavesClosedTubes"] = False
    return obj


def boot_upper_rings(side: int) -> list[list[Vector]]:
    stations = (
        (0.075, 0.125, 0.075, 0.100),
        (0.025, 0.092, 0.082, 0.078),
        (-0.045, 0.072, 0.090, 0.062),
        (-0.115, 0.058, 0.097, 0.050),
        (-0.185, 0.050, 0.102, 0.043),
        (-0.235, 0.047, 0.086, 0.036),
    )
    rings = []
    count = 12
    for y, center_z, radius_x, radius_z in stations:
        ring = []
        for index in range(count):
            angle = 2.0 * math.pi * index / count
            # A flatter lower half and raised front/top create a shoe last, not a pipe.
            vertical = math.sin(angle)
            z = center_z + radius_z * vertical
            if vertical < 0.0:
                z = center_z + radius_z * vertical * 0.62
            instep = max(0.0, -y / 0.235) * max(0.0, vertical) * 0.010
            x = side * (0.100 + radius_x * math.cos(angle))
            ring.append(Vector((x, y, max(0.026, z + instep))))
        rings.append(ring)
    return rings


def sole_loops(side: int, count: int = 16) -> tuple[list[Vector], list[Vector]]:
    lower = []
    upper = []
    for index in range(count):
        angle = 2.0 * math.pi * index / count
        cosine = math.cos(angle)
        sine = math.sin(angle)
        # Rounded-rectangular sole with a narrower heel and broad toe.
        y = -0.090 + 0.190 * sine
        front_factor = max(0.0, -sine)
        width = 0.092 + 0.016 * front_factor - 0.010 * max(0.0, sine)
        x = side * (0.100 + width * math.copysign(abs(cosine) ** 0.62, cosine))
        lower.append(Vector((x, y, 0.010)))
        upper.append(Vector((x, y, 0.027 + 0.006 * front_factor)))
    return lower, upper


def build_boots(mat: bpy.types.Material) -> bpy.types.Object:
    buffers = Buffers()
    for side in (-1, 1):
        buffers.add(*loft(boot_upper_rings(side), close_ring=True, cap_start=True, cap_end=True))
        lower, upper = sole_loops(side)
        buffers.add(*loft((lower, upper), close_ring=True, cap_start=True, cap_end=True))
    obj = mesh_object(
        "HunterBodyV3_Boots", "limb_gear", buffers, mat,
        "paired authored shoe lasts with flattened lower profile, raised instep, toe extension, heel, and separate continuous soles",
        bevel=0.0025, fit_audit=True,
    )
    obj["profileContract"] = "heel, raised instep, rounded toe, continuous sole"
    return obj


def front_point(surface: Surface, x: float, z: float, clearance: float) -> Vector:
    origin = Vector((x, -0.8, z))
    point = surface.ray(origin, Vector((0.0, 1.0, 0.0)), 1.6)
    return point + Vector((0.0, -clearance, 0.0))


def strap_ribbon(
    surface: Surface,
    path: Sequence[tuple[float, float]],
    width: float,
    clearance: float,
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    rows = []
    for index, (x, z) in enumerate(path):
        previous = Vector(path[max(0, index - 1)])
        following = Vector(path[min(len(path) - 1, index + 1)])
        tangent = (following - previous).normalized()
        lateral = Vector((-tangent.y, tangent.x))
        endpoints = []
        for sign in (-1.0, 1.0):
            sample = Vector((x, z)) + lateral * width * 0.5 * sign
            endpoints.append(front_point(surface, sample.x, sample.y, clearance))
        rows.append(endpoints)
    return grid(rows)


def build_harness_belt(body_surface: Surface, mat: bpy.types.Material) -> bpy.types.Object:
    buffers = Buffers()
    diagonal = (
        (0.190, 1.382), (0.145, 1.330), (0.095, 1.278), (0.045, 1.225),
        (-0.005, 1.170), (-0.055, 1.118), (-0.105, 1.065), (-0.150, 1.012),
    )
    buffers.add(*strap_ribbon(body_surface, diagonal, 0.060, 0.040))
    belt_rings = [
        radial_ring(body_surface, z, 24, 0.036, front_ease=0.002)
        for z in (0.905, 0.962)
    ]
    buffers.add(*loft(belt_rings, close_ring=True))
    obj = mesh_object(
        "HunterBodyV3_HarnessBelt", "harness_belt", buffers, mat,
        "one 60 mm front-conforming diagonal ribbon plus one 57 mm wrap belt; both use flat strap sections",
        thickness=0.006, bevel=0.0015, fit_audit=True,
    )
    obj["roundTubeStrapsUsed"] = False
    return obj


def swept_section(
    points: Sequence[Vector],
    widths: Sequence[float],
    depths: Sequence[float],
    *,
    frame_axis: Vector,
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    require(len(points) == len(widths) == len(depths), "Sweep profile mismatch")
    profile = (
        (1.0, 0.42), (0.66, 1.0), (-0.66, 1.0), (-1.0, 0.42),
        (-1.0, -0.42), (-0.66, -1.0), (0.66, -1.0), (1.0, -0.42),
    )
    rings = []
    for index, point in enumerate(points):
        previous = points[max(0, index - 1)]
        following = points[min(len(points) - 1, index + 1)]
        tangent = (following - previous).normalized()
        first = tangent.cross(frame_axis).normalized()
        second = tangent.cross(first).normalized()
        ring = [
            point + first * (widths[index] * a) + second * (depths[index] * b)
            for a, b in profile
        ]
        rings.append(ring)
    return loft(rings, close_ring=True, cap_start=True, cap_end=True)


def thin_path(
    points: Sequence[Vector],
    radius: float,
    count: int = 6,
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    rings = []
    for index, point in enumerate(points):
        previous = points[max(0, index - 1)]
        following = points[min(len(points) - 1, index + 1)]
        tangent = (following - previous).normalized()
        reference = Vector((1.0, 0.0, 0.0))
        if abs(tangent.dot(reference)) > 0.9:
            reference = Vector((0.0, 1.0, 0.0))
        first = tangent.cross(reference).normalized()
        second = tangent.cross(first).normalized()
        rings.append([
            point + radius * (
                math.cos(2.0 * math.pi * cursor / count) * first
                + math.sin(2.0 * math.pi * cursor / count) * second
            )
            for cursor in range(count)
        ])
    return loft(rings, close_ring=True, cap_start=True, cap_end=True)


def build_bow(mat: bpy.types.Material) -> tuple[bpy.types.Object, tuple[Vector, Vector, Vector]]:
    profile = (
        (-1.00, -0.100, 0.007, 0.004),
        (-0.86, -0.135, 0.009, 0.005),
        (-0.68, -0.195, 0.012, 0.006),
        (-0.48, -0.215, 0.014, 0.007),
        (-0.27, -0.155, 0.017, 0.008),
        (-0.10, -0.090, 0.024, 0.013),
        (0.00, -0.064, 0.035, 0.018),
        (0.10, -0.090, 0.024, 0.013),
        (0.27, -0.155, 0.017, 0.008),
        (0.48, -0.215, 0.014, 0.007),
        (0.68, -0.195, 0.012, 0.006),
        (0.86, -0.135, 0.009, 0.005),
        (1.00, -0.100, 0.007, 0.004),
    )
    grip_z = 1.375
    half_length = 1.305 / 2.0
    points = [Vector((0.765, y, grip_z + t * half_length)) for t, y, _width, _depth in profile]
    widths = [width for _t, _y, width, _depth in profile]
    depths = [depth for _t, _y, _width, depth in profile]
    buffers = Buffers()
    buffers.add(*swept_section(points, widths, depths, frame_axis=Vector((1.0, 0.0, 0.0))))
    obj = mesh_object(
        "HunterBodyV3_RecurveBow", "weapon_quiver", buffers, mat,
        "1.305 m recurve with thirteen authored stations and rounded-rectangular tapered limb sections",
        bevel=0.001, fit_audit=False,
    )
    obj["lengthMeters"] = 1.305
    obj["futureBindingSocket"] = "Socket_Weapon_L"
    obj["roundTubeConstruction"] = False
    return obj, (points[0], points[len(points) // 2], points[-1])


def build_string(
    name: str,
    bow_points: tuple[Vector, Vector, Vector],
    nock: Vector,
    mat: bpy.types.Material,
    visible: bool,
) -> bpy.types.Object:
    lower, _grip, upper = bow_points
    buffers = Buffers()
    buffers.add(*thin_path((lower, nock, upper), 0.0022, 4))
    obj = mesh_object(
        name, "weapon_quiver", buffers, mat,
        "separate authored weapon-state string mesh",
        fit_audit=False,
    )
    obj.hide_render = not visible
    obj.hide_set(not visible)
    obj["weaponState"] = "Static" if "Static" in name else "Draw"
    obj["futureBinding"] = "Socket_Weapon_L" if "Static" in name else "Socket_Weapon_L+Socket_Weapon_R"
    return obj


def fletching_prism(center: Vector, side: float) -> tuple[list[Vector], list[tuple[int, ...]]]:
    thickness = 0.0025
    front = (
        center + Vector((-thickness, 0.000, 0.000)),
        center + Vector((-thickness, 0.065, 0.012 * side)),
        center + Vector((-thickness, 0.080, 0.000)),
        center + Vector((-thickness, 0.020, -0.010 * side)),
    )
    back = tuple(point + Vector((2.0 * thickness, 0.0, 0.0)) for point in front)
    return prism(front, back)


def build_nocked_arrow(mat: bpy.types.Material) -> bpy.types.Object:
    nock = Vector((0.765, 0.220, 1.375))
    tip = Vector((0.765, -0.780, 1.375))
    buffers = Buffers()
    buffers.add(*thin_path((nock, tip), 0.004, 6))
    head_front = (
        tip + Vector((-0.018, 0.030, 0.000)),
        tip + Vector((0.000, -0.030, 0.000)),
        tip + Vector((0.018, 0.030, 0.000)),
    )
    head_back = tuple(point + Vector((0.0, 0.004, 0.010)) for point in head_front)
    buffers.add(*prism(head_front, head_back))
    buffers.add(*fletching_prism(Vector((0.765, 0.135, 1.375)), 1.0))
    obj = mesh_object(
        "HunterBodyV3_NockedArrow", "weapon_quiver", buffers, mat,
        "separate draw-only nocked arrow with shaft, compact head, and thickened fletching",
        fit_audit=False,
    )
    obj.hide_render = True
    obj.hide_set(True)
    obj["weaponState"] = "DrawOnly"
    obj["futureBindingSocket"] = "Socket_Weapon_R"
    return obj


def build_quiver_arrows(mat: bpy.types.Material) -> bpy.types.Object:
    buffers = Buffers()
    rings = []
    count = 12
    for index in range(7):
        t = index / 6.0
        center = Vector((0.105 + 0.145 * t, 0.168 + 0.025 * t, 0.855 + 0.570 * t))
        radius_x = 0.043 + 0.022 * t
        radius_y = 0.033 + 0.014 * t
        rings.append([
            center + Vector((
                radius_x * math.copysign(abs(math.cos(2.0 * math.pi * cursor / count)) ** 0.75, math.cos(2.0 * math.pi * cursor / count)),
                radius_y * math.sin(2.0 * math.pi * cursor / count),
                0.0,
            ))
            for cursor in range(count)
        ])
    buffers.add(*loft(rings, close_ring=True, cap_start=True))
    for arrow_index in range(5):
        lower = Vector((0.145 + 0.024 * arrow_index, 0.185 + 0.006 * (arrow_index % 2), 1.265 + 0.010 * arrow_index))
        upper = Vector((0.190 + 0.020 * arrow_index, 0.198 + 0.004 * (arrow_index % 2), 1.625 + 0.024 * arrow_index))
        buffers.add(*thin_path((lower, upper), 0.0032, 6))
        vane_center = upper - (upper - lower).normalized() * 0.060
        for sign in (-1.0, 1.0):
            front = (
                vane_center + Vector((-0.003, 0.0, -0.025)),
                vane_center + Vector((-0.003, sign * 0.023, 0.0)),
                vane_center + Vector((-0.003, 0.0, 0.050)),
            )
            back = tuple(point + Vector((0.006, 0.0, 0.0)) for point in front)
            buffers.add(*prism(front, back))
    obj = mesh_object(
        "HunterBodyV3_QuiverArrows", "weapon_quiver", buffers, mat,
        "one tapered open oval back quiver plus five staggered shafts and thickened readable fletching",
        thickness=0.0045, bevel=0.0012, fit_audit=False,
    )
    obj["arrowCount"] = 5
    obj["futureBindingSocket"] = "Socket_Back"
    obj["roundCylinderQuiver"] = False
    return obj


def mesh_integrity(obj: bpy.types.Object) -> dict[str, int]:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    try:
        boundary = sum(1 for edge in bm.edges if edge.is_boundary)
        non_manifold = sum(1 for edge in bm.edges if not edge.is_manifold and not edge.is_boundary)
        degenerate = sum(1 for face in bm.faces if face.calc_area() <= 1e-10)
        remaining = set(bm.verts)
        islands = 0
        while remaining:
            islands += 1
            seed = remaining.pop()
            stack = [seed]
            while stack:
                vertex = stack.pop()
                for edge in vertex.link_edges:
                    other = edge.other_vert(vertex)
                    if other in remaining:
                        remaining.remove(other)
                        stack.append(other)
        return {
            "vertices": len(bm.verts),
            "faces": len(bm.faces),
            "boundaryEdges": boundary,
            "nonManifoldInteriorEdges": non_manifold,
            "degenerateFaces": degenerate,
            "connectedIslands": islands,
        }
    finally:
        bm.free()


def triangle_count(obj: bpy.types.Object) -> int:
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


def signed_clearance(body_surface: Surface, obj: bpy.types.Object) -> dict[str, object]:
    values = []
    for vertex in obj.data.vertices:
        nearest, normal, _distance = body_surface.nearest(vertex.co)
        values.append(float((vertex.co - nearest).dot(normal)))
    values.sort()
    return {
        "sampleCount": len(values),
        "minimumSignedClearanceMeters": values[0],
        "p01SignedClearanceMeters": values[max(0, round((len(values) - 1) * 0.01))],
        "medianSignedClearanceMeters": values[round((len(values) - 1) * 0.50)],
        "bodySurfaceTriangleOverlapPairs": body_surface.overlap_count(obj),
        "methodBoundary": "all final macro mesh vertices after authored outward thickness; signed nearest-normal is diagnostic, triangle overlap is structural",
    }


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_scene(
    body: bpy.types.Object,
    eyes: Sequence[bpy.types.Object],
    kit: Sequence[bpy.types.Object],
) -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.display.shading.curvature_ridge_factor = 1.7
    scene.display.shading.curvature_valley_factor = 1.3
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("HunterBodyV3R1World")
    scene.world.color = (0.010, 0.014, 0.020)
    camera_data = bpy.data.cameras.new("HunterBodyV3R1ReviewCameraData")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("HunterBodyV3R1ReviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for obj in (body, *eyes, *kit):
        if obj.name not in {"HunterBodyV3_DrawString", "HunterBodyV3_NockedArrow"}:
            obj.hide_render = False
    return camera


def render_evidence(camera: bpy.types.Object, evidence_root: Path) -> list[Path]:
    views = {
        "FrontThreeQuarter": (Vector((3.4, -5.8, 2.35)), Vector((0.0, 0.0, 0.98)), 2.30),
        "Front": (Vector((0.0, -6.0, 1.15)), Vector((0.0, 0.0, 0.98)), 2.30),
        "Side": (Vector((6.0, 0.0, 1.15)), Vector((0.0, 0.0, 0.98)), 2.30),
        "Back": (Vector((0.0, 6.0, 1.15)), Vector((0.0, 0.0, 0.98)), 2.30),
        "HeadClose": (Vector((2.4, -4.5, 1.92)), Vector((0.0, 0.0, 1.635)), 0.67),
        "ShoulderClose": (Vector((3.0, -5.1, 1.78)), Vector((0.075, 0.0, 1.405)), 0.88),
    }
    paths = []
    for label, (location, target, scale) in views.items():
        camera.location = location
        camera.data.ortho_scale = scale
        look_at(camera, target)
        path = evidence_root / f"{MODEL_NAME}_{label}.png"
        bpy.context.scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    return paths


def main() -> None:
    require((5, 2, 0) <= bpy.app.version < (5, 3, 0), f"Blender 5.2 is required, found {bpy.app.version_string}")
    args = parse_args()
    output_root = validate_output_root(Path(args.output_root))
    evidence_root = output_root / "Evidence"
    blend_path = output_root / f"{MODEL_NAME}.blend"
    report_path = output_root / f"{MODEL_NAME}.report.json"
    review_path = output_root / f"{MODEL_NAME}.manual-review-request.json"
    if not args.allow_overwrite:
        require(not any(path.exists() for path in (blend_path, report_path, review_path)), "R1 output exists; use --allow-overwrite only for this new iteration path")
    output_root.mkdir(parents=True, exist_ok=True)
    evidence_root.mkdir(parents=True, exist_ok=True)
    verified_inputs = verify_inputs()

    body, eyes, fit_top, fit_trousers, anatomy = load_accepted_sources()
    body_surface = Surface(body)
    top_surface = Surface(fit_top)
    trouser_surface = Surface(fit_trousers)

    materials = {
        "body": material("HunterBodyV3R1_BodyClay", (0.44, 0.34, 0.28), 0.86),
        "eyes": material("HunterBodyV3R1_EyeClay", (0.055, 0.075, 0.085), 0.55),
        "base": material("HunterBodyV3R1_BaseClothClay", (0.105, 0.245, 0.165), 0.92),
        "hood": material("HunterBodyV3R1_HoodClay", (0.075, 0.175, 0.115), 0.94),
        "cowl": material("HunterBodyV3R1_CowlClay", (0.145, 0.305, 0.205), 0.94),
        "armor": material("HunterBodyV3R1_ArmorClay", (0.255, 0.185, 0.105), 0.72),
        "fur": material("HunterBodyV3R1_FurClay", (0.55, 0.49, 0.39), 0.96),
        "metal": material("HunterBodyV3R1_PauldronClay", (0.40, 0.30, 0.16), 0.58),
        "cape": material("HunterBodyV3R1_CapeClay", (0.075, 0.205, 0.135), 0.96),
        "leather": material("HunterBodyV3R1_LeatherClay", (0.215, 0.115, 0.070), 0.82),
        "bow": material("HunterBodyV3R1_BowClay", (0.38, 0.18, 0.075), 0.68),
        "string": material("HunterBodyV3R1_StringClay", (0.025, 0.022, 0.018), 0.88),
    }
    body.data.materials.clear()
    body.data.materials.append(materials["body"])
    for eye in eyes:
        eye.data.materials.clear()
        eye.data.materials.append(materials["eyes"])

    kit: list[bpy.types.Object] = []
    kit.append(build_base_garments(top_surface, trouser_surface, anatomy, materials["base"]))
    kit.append(build_hood_hat(materials["hood"]))
    kit.append(build_cowl(materials["cowl"]))
    kit.append(build_brigandine(body_surface, materials["armor"]))
    kit.append(build_mantle(materials["fur"]))
    kit.append(build_pauldrons(materials["metal"]))
    kit.append(build_split_cape(materials["cape"]))
    kit.append(build_limb_gear(top_surface, trouser_surface, anatomy, materials["leather"]))
    kit.append(build_boots(materials["leather"]))
    kit.append(build_harness_belt(body_surface, materials["leather"]))
    bow, bow_points = build_bow(materials["bow"])
    kit.append(bow)
    kit.append(build_quiver_arrows(materials["leather"]))
    static_nock = Vector((0.765, -0.080, anatomy.shoulder.z))
    draw_nock = Vector((0.765, 0.220, anatomy.shoulder.z))
    kit.append(build_string("HunterBodyV3_StaticString", bow_points, static_nock, materials["string"], True))
    kit.append(build_string("HunterBodyV3_DrawString", bow_points, draw_nock, materials["string"], False))
    kit.append(build_nocked_arrow(materials["bow"]))
    require({obj.name for obj in kit} == EXPECTED_KIT_OBJECTS, "Fresh R1 kit object contract changed")

    family_reports: dict[str, dict[str, object]] = {}
    object_reports: dict[str, dict[str, object]] = {}
    failures: list[str] = []
    for obj in kit:
        integrity = mesh_integrity(obj)
        triangles = triangle_count(obj)
        family = str(obj["macroFamily"])
        object_report: dict[str, object] = {
            "family": family,
            "construction": str(obj["construction"]),
            "triangles": triangles,
            "integrity": integrity,
            "uvLayerCount": len(obj.data.uv_layers),
            "armatureModifierCount": sum(modifier.type == "ARMATURE" for modifier in obj.modifiers),
            "promotionAllowed": bool(obj["promotionAllowed"]),
            "visibleInDefaultReview": not obj.hide_render,
        }
        if bool(obj["bodyFitAudit"]):
            object_report["bodyFit"] = signed_clearance(body_surface, obj)
        object_reports[obj.name] = object_report
        aggregate = family_reports.setdefault(family, {"objectCount": 0, "triangles": 0, "objects": []})
        aggregate["objectCount"] = int(aggregate["objectCount"]) + 1
        aggregate["triangles"] = int(aggregate["triangles"]) + triangles
        aggregate["objects"].append(obj.name)
        if integrity["degenerateFaces"] or integrity["nonManifoldInteriorEdges"]:
            failures.append(f"{obj.name} mesh integrity")
        if len(obj.data.uv_layers) != 0:
            failures.append(f"{obj.name} unexpectedly has UVs")
        if any(modifier.type == "ARMATURE" for modifier in obj.modifiers):
            failures.append(f"{obj.name} unexpectedly has production binding")

    for family, (minimum, maximum) in FAMILY_TARGETS.items():
        total = int(family_reports.get(family, {}).get("triangles", 0))
        if not minimum <= total <= maximum:
            failures.append(f"{family} triangles {total} outside [{minimum},{maximum}]")
    total_triangles = sum(triangle_count(obj) for obj in kit)
    if not 7_000 <= total_triangles <= 16_000:
        failures.append(f"kit triangles {total_triangles} outside [7000,16000]")
    forbidden = [obj.name for obj in kit if any(token in obj.name for token in ("Cylinder", "Cube", "Sphere", "Panel", "Scale", "Rivet"))]
    if forbidden:
        failures.append("forbidden primitive/panel naming: " + ", ".join(forbidden))
    if not fit_top.hide_render or not fit_trousers.hide_render:
        failures.append("accepted fit proxies became visible")

    camera = configure_scene(body, eyes, kit)
    evidence_paths = render_evidence(camera, evidence_root)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True, check_existing=False)

    report = {
        "schemaVersion": 1,
        "assetId": ASSET_ID,
        "status": "READY_FOR_MANUAL_MACRO_REVIEW" if not failures else "BLOCKED_BY_STRUCTURAL_R1_FAILURES",
        "scope": "fresh source-only family-colour Hunter macro on accepted masculine BodyV3; no production promotion",
        "verifiedInputs": verified_inputs,
        "bodyLineage": "Blender Foundation Human Base Meshes v1.4.1 stylized masculine via accepted SessionHumanoidBaseV3",
        "anatomicalRigLandmarksMeters": anatomy.as_json(),
        "hiddenFitProxy": {
            "blend": relative(FIT_BLEND),
            "objects": [FIT_TOP_NAME, FIT_TROUSERS_NAME],
            "visibleFacesReused": False,
            "rendered": False,
        },
        "script": relative(SCRIPT_PATH),
        "scriptSha256": sha256(SCRIPT_PATH),
        "blend": relative(blend_path),
        "blendSha256": sha256(blend_path),
        "kitObjectCount": len(kit),
        "kitTriangles": total_triangles,
        "objects": object_reports,
        "families": family_reports,
        "contracts": {
            "hoodCapClosedCrown": True,
            "hoodCowlMantleSeparate": True,
            "brigandineRows": 4,
            "leftPauldronShells": 3,
            "rightPauldronShells": 2,
            "capeSections": 2,
            "bowLengthMeters": 1.305,
            "bowNonCircularSections": True,
            "quiverArrowCount": 5,
            "staticDrawAndNockedWeaponStatesSeparate": True,
        },
        "evidence": {relative(path): sha256(path) for path in evidence_paths},
        "failures": failures,
        "boundaries": {
            "manualArtAcceptance": False,
            "manualReviewRequired": True,
            "masculineBindPoseOnly": True,
            "garmentWeightsAuthored": False,
            "garmentDeformationTested": False,
            "uvsAuthored": False,
            "pbrMapsAuthored": False,
            "lodsAuthored": False,
            "fbxWritten": False,
            "unityIntegrationPerformed": False,
            "promotionAllowed": False,
        },
    }
    report_path.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    review_request = {
        "schemaVersion": 1,
        "assetId": ASSET_ID,
        "status": "PENDING_MANUAL_MACRO_REVIEW" if not failures else "BLOCKED_BY_STRUCTURAL_R1_FAILURES",
        "report": relative(report_path),
        "reportSha256": sha256(report_path),
        "evidence": list(report["evidence"]),
        "reviewQuestions": [
            "Does the hood-cap have one closed soft crown, a clean face aperture, and no helmet or top-hole read?",
            "Are hood, cowl, mantle, brigandine, and the 3L/2R shoulder stack separately readable?",
            "Do four shallow brigandine rows read as tailored armor construction instead of inflated torso bands?",
            "Do bracers, greaves, and boots follow BodyV3 rather than reading as tubes or boxes?",
            "Do the split cape panels show broad drape, a persistent center gap, and shaped hems rather than rigid sheets?",
            "Does the 1.305 m recurve bow read as a tapered noncircular weapon with a separate static string?",
            "Does the quiver read as a tapered back-carried case with five clear arrows and restrained fletching?",
            "At first glance, does the complete family-colour clay read as one authored Hunter outfit rather than assembled primitives or fused shells?",
        ],
        "automaticAcceptanceForbidden": True,
        "nextStepIfAccepted": "freeze the R1 blend/hash, then author weights and pose/deformation evidence before UV/PBR/LOD/FBX/Unity",
        "nextStepIfRejected": "change only macro cages and proportions in a new iteration; do not surface or promote R1",
        "promotionAllowed": False,
    }
    review_path.write_text(json.dumps(review_request, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(report["status"])
    print("KIT_OBJECTS", len(kit), "KIT_TRIANGLES", total_triangles, "EVIDENCE", len(evidence_paths))
    if failures:
        raise RuntimeError("; ".join(failures))


if __name__ == "__main__":
    main()
