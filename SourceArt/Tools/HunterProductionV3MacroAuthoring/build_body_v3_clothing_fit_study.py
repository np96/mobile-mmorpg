#!/usr/bin/env python3
"""Build a source-only fitted-clothing study on accepted masculine BodyV3.

This is deliberately not Hunter class art.  It proves that a compact garment
system can follow the selected Blender Foundation anatomy before another hood,
armor, cape, bow, UV, LOD, FBX, or Unity attempt is allowed.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import json
from pathlib import Path
import sys
from typing import Sequence

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


ASSET_ID = "session-humanoid-body-v3-clothing-fit-study-v1"
BODY_BLEND = body_contract.BLEND_PATH
BODY_MANIFEST = body_contract.MANIFEST_PATH
BODY_PROOF = body_contract.DEFORMATION_REPORT
BODY_MANUAL = body_contract.MANUAL_REVIEW_PATH
EXPECTED_INPUTS = {
    BODY_BLEND: "ff30baebeb3cb03186cf1663691a65c743af16616c82003f4358b5a41fb08458",
    BODY_MANIFEST: "62aa4ce0ab1dc2caf554f1483bb38c2e01319f40ffa3940f6c6af60d995dd4ea",
    BODY_PROOF: "ef085d15a7dca7e4a0e357c74dd13a60c77969dc1c5db9da9c1843109046e719",
    BODY_MANUAL: "c6a08aca8b84c325031c8d9c85a4083e1de056ca2de2dfe8f9b5abef02c86f43",
}
DEFAULT_ROOT = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/SessionHumanoidClothingFitStudyV1/Outputs"
)
BODY_NAME = body_contract.BODY_NAMES["Masculine"]
RIG_NAME = body_contract.RIG_NAMES["Masculine"]
EYE_NAMES = body_contract.EYE_NAMES["Masculine"]
REVIEW_BODY_NAME = "EvidenceOnly_BodyV3_Masculine"


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", default=str(DEFAULT_ROOT))
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
    require("/Assets/" not in normalized, "Clothing-fit study may not enter Unity Assets")
    require(result != PROJECT_ROOT.resolve(), "Output may not be the project root")
    return result


def verify_inputs() -> dict[str, str]:
    verified = {}
    for path, expected in EXPECTED_INPUTS.items():
        require(path.is_file(), f"Pinned BodyV3 input is missing: {path}")
        actual = sha256(path)
        require(actual == expected, f"Pinned BodyV3 input changed: {path}")
        verified[relative(path)] = actual
    body_review = json.loads(BODY_MANUAL.read_text(encoding="utf-8"))
    require(body_review["status"] == "PASS_MANUAL_DEFORMATION_REVIEW", "BodyV3 is not accepted")
    return verified


@dataclass(frozen=True)
class Anatomy:
    shoulder: Vector
    elbow: Vector
    wrist: Vector
    hip: Vector
    knee: Vector
    ankle: Vector
    foot: Vector
    toes: Vector
    neck: Vector
    head_top: Vector

    def as_json(self) -> dict[str, list[float]]:
        return {
            name: [round(float(value), 6) for value in getattr(self, name)]
            for name in (
                "shoulder", "elbow", "wrist", "hip", "knee", "ankle",
                "foot", "toes", "neck", "head_top",
            )
        }


class Buffers:
    def __init__(self) -> None:
        self.vertices: list[Vector] = []
        self.faces: list[tuple[int, ...]] = []


def create_material(name: str, color: tuple[float, float, float], roughness: float) -> bpy.types.Material:
    material = bpy.data.materials.new(name)
    material.diffuse_color = (*color, 1.0)
    material.use_nodes = True
    node = material.node_tree.nodes.get("Principled BSDF")
    require(node is not None, "Principled material is missing")
    node.inputs["Base Color"].default_value = (*color, 1.0)
    node.inputs["Roughness"].default_value = roughness
    return material


def create_garment(
    name: str,
    family: str,
    buffers: Buffers,
    material: bpy.types.Material,
    thickness: float,
    maximum_p95_clearance: float,
    maximum_clearance: float,
) -> bpy.types.Object:
    require(buffers.vertices and buffers.faces, f"Empty garment: {name}")
    mesh = bpy.data.meshes.new(name + "_Mesh")
    mesh.from_pydata([tuple(vertex) for vertex in buffers.vertices], [], buffers.faces)
    mesh.validate(verbose=False)
    mesh.update(calc_edges=True)
    for polygon in mesh.polygons:
        polygon.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.data.materials.append(material)
    obj["assetId"] = ASSET_ID
    obj["fitStudyOnly"] = True
    obj["garmentFamily"] = family
    obj["maximumP95ClearanceMeters"] = maximum_p95_clearance
    obj["maximumClearanceMeters"] = maximum_clearance
    obj["promotionAllowed"] = False
    modifier = obj.modifiers.new("OutwardOnlyThickness", "SOLIDIFY")
    modifier.thickness = thickness
    modifier.offset = 1.0
    modifier.use_even_offset = True
    obj["wallThicknessMeters"] = thickness
    return obj


class BodySurface:
    def __init__(self, body: bpy.types.Object):
        body.data.calc_loop_triangles()
        vertices = [vertex.co.copy() for vertex in body.data.vertices]
        triangles = [tuple(triangle.vertices) for triangle in body.data.loop_triangles]
        self.tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)

    def nearest(self, point: Vector) -> tuple[Vector, Vector, float]:
        nearest, normal, _index, distance = self.tree.find_nearest(point)
        require(nearest is not None and normal is not None and distance is not None, "Nearest query failed")
        return nearest, normal.normalized(), float(distance)


def evaluated_static_copy(source: bpy.types.Object, name: str) -> bpy.types.Object:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated = source.evaluated_get(depsgraph)
    mesh = bpy.data.meshes.new_from_object(evaluated, preserve_all_data_layers=True, depsgraph=depsgraph)
    clone = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(clone)
    clone.matrix_world = source.matrix_world.copy()
    return clone


def load_body() -> tuple[bpy.types.Object, tuple[bpy.types.Object, ...], Anatomy]:
    bpy.ops.wm.open_mainfile(filepath=str(BODY_BLEND), load_ui=False)
    body_contract.reset_pose(bpy.data.objects[RIG_NAME])
    bpy.context.view_layer.update()
    source_body = bpy.data.objects[BODY_NAME]
    source_rig = bpy.data.objects[RIG_NAME]
    bones = source_rig.data.bones

    def head(name: str) -> Vector:
        return bones[name].head_local.copy()

    anatomy = Anatomy(
        shoulder=head("LeftUpperArm"),
        elbow=head("LeftLowerArm"),
        wrist=head("LeftHand"),
        hip=head("LeftUpperLeg"),
        knee=head("LeftLowerLeg"),
        ankle=head("LeftFoot"),
        foot=head("LeftToes"),
        toes=bones["LeftToes"].tail_local.copy(),
        neck=head("Neck"),
        head_top=bones["Head"].tail_local.copy(),
    )
    body = evaluated_static_copy(source_body, REVIEW_BODY_NAME)
    eyes = tuple(
        evaluated_static_copy(bpy.data.objects[name], f"EvidenceOnly_{name}")
        for name in EYE_NAMES
    )
    keep = {body, *eyes}
    for obj in list(bpy.data.objects):
        if obj not in keep:
            bpy.data.objects.remove(obj, do_unlink=True)
    body["evidenceOnly"] = True
    body["acceptedBodyInput"] = True
    body["productionLineage"] = "Blender Foundation Human Base Meshes v1.4.1 stylized masculine"
    body["promotionAllowed"] = False
    for eye in eyes:
        eye["evidenceOnly"] = True
        eye["promotionAllowed"] = False
    return body, eyes, anatomy


def build_surface_envelope(
    body: bpy.types.Object,
    name: str,
    family: str,
    face_sets: set[int],
    clearance: float,
    material: bpy.types.Material,
    maximum_p95_clearance: float,
    maximum_clearance: float,
) -> bpy.types.Object:
    """Create a dense, body-derived fit envelope, not production garment topology.

    This bounded study is intentionally about proportion and clearance.  Reusing
    the accepted anatomy's regional sampling prevents a procedural tube or global
    shrink-wrap from silently targeting a different limb.  A later manually
    authored garment must be retopologized independently after this fit direction
    is accepted.
    """
    mesh = body.data
    attribute = mesh.attributes.get(".sculpt_face_set")
    require(
        attribute is not None and attribute.domain == "FACE",
        "BodyV3 fit envelope requires the accepted sculpt face sets",
    )
    selected = [
        polygon
        for polygon in mesh.polygons
        if int(attribute.data[polygon.index].value) in face_sets
    ]
    require(selected, f"No BodyV3 faces selected for {name}")
    used = sorted({index for polygon in selected for index in polygon.vertices})
    remap = {source: target for target, source in enumerate(used)}
    buffers = Buffers()
    buffers.vertices = [
        mesh.vertices[index].co.copy()
        + mesh.vertices[index].normal.normalized() * clearance
        for index in used
    ]
    buffers.faces = [tuple(remap[index] for index in polygon.vertices) for polygon in selected]
    obj = create_garment(
        name,
        family,
        buffers,
        material,
        thickness=0.003,
        maximum_p95_clearance=maximum_p95_clearance,
        maximum_clearance=maximum_clearance,
    )
    obj["construction"] = (
        "dense BodyV3 regional surface envelope for fit calibration only; "
        "manual independent retopology is still required"
    )
    obj["innerClearanceMeters"] = clearance
    obj["sourceFaceSets"] = json.dumps(sorted(face_sets))
    return obj


def mesh_integrity(obj: bpy.types.Object) -> dict[str, int]:
    mesh = obj.data
    bm = bmesh.new()
    bm.from_mesh(mesh)
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


def evaluated_triangles(obj: bpy.types.Object) -> int:
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    mesh = evaluated.to_mesh()
    try:
        mesh.calc_loop_triangles()
        return len(mesh.loop_triangles)
    finally:
        evaluated.to_mesh_clear()


def overlap_count(surface: BodySurface, obj: bpy.types.Object) -> int:
    mesh = obj.data
    mesh.calc_loop_triangles()
    tree = BVHTree.FromPolygons(
        [vertex.co.copy() for vertex in mesh.vertices],
        [tuple(triangle.vertices) for triangle in mesh.loop_triangles],
        all_triangles=True,
    )
    return len(surface.tree.overlap(tree))


def percentile(sorted_values: Sequence[float], fraction: float) -> float:
    if not sorted_values:
        return 0.0
    index = min(len(sorted_values) - 1, max(0, round((len(sorted_values) - 1) * fraction)))
    return float(sorted_values[index])


def clearance_audit(surface: BodySurface, obj: bpy.types.Object) -> dict[str, object]:
    mesh = obj.data
    samples: list[Vector] = [vertex.co.copy() for vertex in mesh.vertices]
    samples.extend((mesh.vertices[edge.vertices[0]].co + mesh.vertices[edge.vertices[1]].co) * 0.5 for edge in mesh.edges)
    samples.extend(
        sum((mesh.vertices[index].co for index in polygon.vertices), Vector()) / len(polygon.vertices)
        for polygon in mesh.polygons
    )
    signed = []
    for sample in samples:
        nearest, normal, _distance = surface.nearest(sample)
        signed.append(float((sample - nearest).dot(normal)))
    signed.sort()
    limit = float(obj["maximumP95ClearanceMeters"])
    minimum = signed[0]
    p01 = percentile(signed, 0.01)
    median = percentile(signed, 0.50)
    p95 = percentile(signed, 0.95)
    maximum = signed[-1]
    below_penetration = sum(value < -0.001 for value in signed)
    below_target = sum(value < 0.002 for value in signed)
    above_bulk = sum(value > limit for value in signed)
    maximum_limit = float(obj["maximumClearanceMeters"])
    above_maximum = sum(value > maximum_limit for value in signed)
    within_target = sum(0.002 <= value <= limit for value in signed)
    within_target_fraction = within_target / len(signed)
    triangle_overlaps = overlap_count(surface, obj)
    return {
        "sampleCount": len(signed),
        "minimumSignedClearanceMeters": minimum,
        "p01SignedClearanceMeters": p01,
        "medianSignedClearanceMeters": median,
        "p95SignedClearanceMeters": p95,
        "maximumSignedClearanceMeters": maximum,
        "penetrationLimitMeters": -0.001,
        "targetMinimumClearanceMeters": 0.002,
        "maximumP95ClearanceMeters": limit,
        "samplesBelowPenetrationLimit": below_penetration,
        "samplesBelowTarget": below_target,
        "samplesAboveBulkLimit": above_bulk,
        "maximumClearanceMeters": maximum_limit,
        "samplesAboveMaximumClearance": above_maximum,
        "fractionWithinTwoMillimetersToP95Limit": within_target_fraction,
        "bodyGarmentTriangleOverlapPairs": triangle_overlaps,
        "status": "PASS_CLEARANCE_AND_BULK"
        if (
            below_penetration == 0
            and p01 >= 0.0
            and p95 <= limit
            and above_maximum == 0
            and within_target_fraction >= 0.95
            and triangle_overlaps == 0
        )
        else "REJECT_CLEARANCE_OR_BULK",
        "methodBoundary": "base inner-cage vertices, edge midpoints, and face centroids; outward-only Solidify is excluded from the inner fit surface",
    }


def look_at(obj: bpy.types.Object, target: Vector) -> None:
    obj.rotation_euler = (target - obj.location).to_track_quat("-Z", "Y").to_euler()


def configure_scene(
    body: bpy.types.Object,
    eyes: Sequence[bpy.types.Object],
    garments: Sequence[bpy.types.Object],
) -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.display.shading.curvature_ridge_factor = 1.8
    scene.display.shading.curvature_valley_factor = 1.4
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("BodyV3FitStudyWorld")
    scene.world.color = (0.012, 0.017, 0.024)
    camera_data = bpy.data.cameras.new("BodyV3FitStudyCameraData")
    camera_data.type = "ORTHO"
    camera_data.ortho_scale = 2.18
    camera = bpy.data.objects.new("BodyV3FitStudyCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for obj in (body, *eyes, *garments):
        obj.hide_render = False
    return camera


def render_views(camera: bpy.types.Object, evidence_root: Path) -> list[Path]:
    views = {
        "FrontThreeQuarter": (Vector((3.2, -5.5, 2.45)), Vector((0.0, 0.0, 0.93))),
        "Front": (Vector((0.0, -6.0, 1.15)), Vector((0.0, 0.0, 0.90))),
        "Side": (Vector((6.0, 0.0, 1.15)), Vector((0.0, 0.0, 0.90))),
        "BackThreeQuarter": (Vector((-3.2, 5.5, 2.35)), Vector((0.0, 0.0, 0.93))),
    }
    paths = []
    for label, (location, target) in views.items():
        camera.location = location
        look_at(camera, target)
        path = evidence_root / f"BodyV3ClothingFitStudyV1_{label}.png"
        bpy.context.scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    return paths


def main() -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    args = parse_args()
    output_root = validate_output_root(Path(args.output_root))
    evidence_root = output_root / "Evidence"
    blend_path = output_root / "SessionHumanoidClothingFitStudyV1.blend"
    report_path = output_root / "SessionHumanoidClothingFitStudyV1.report.json"
    review_request_path = output_root / "SessionHumanoidClothingFitStudyV1.review-request.json"
    targets = (blend_path, report_path, review_request_path)
    if not args.allow_overwrite:
        require(not any(path.exists() for path in targets), "Output exists; use a new root or --allow-overwrite")
    output_root.mkdir(parents=True, exist_ok=True)
    evidence_root.mkdir(parents=True, exist_ok=True)
    verified_inputs = verify_inputs()
    body, eyes, anatomy = load_body()
    surface = BodySurface(body)

    body_material = create_material("BodyV3Fit_Body", (0.43, 0.34, 0.29), 0.82)
    eye_material = create_material("BodyV3Fit_Eyes", (0.06, 0.08, 0.10), 0.55)
    tunic_material = create_material("BodyV3Fit_Tunic", (0.105, 0.245, 0.175), 0.92)
    trouser_material = create_material("BodyV3Fit_Trousers", (0.095, 0.115, 0.135), 0.88)
    body.data.materials.clear()
    body.data.materials.append(body_material)
    for eye in eyes:
        eye.data.materials.clear()
        eye.data.materials.append(eye_material)

    garments = (
        build_surface_envelope(
            body,
            "BodyV3Fit_LongSleeveTop",
            "long_sleeve_top",
            body_contract.TORSO_FACE_SETS
            | body_contract.SHOULDER_FACE_SETS
            | body_contract.UPPER_ARM_FACE_SETS
            | body_contract.FOREARM_FACE_SETS,
            0.009,
            tunic_material,
            0.018,
            0.025,
        ),
        build_surface_envelope(
            body,
            "BodyV3Fit_Trousers",
            "trousers",
            body_contract.PELVIS_FACE_SETS
            | body_contract.THIGH_FACE_SETS
            | body_contract.CALF_FACE_SETS,
            0.010,
            trouser_material,
            0.020,
            0.030,
        ),
    )
    bpy.context.view_layer.update()
    component_reports = {}
    failures = []
    for garment in garments:
        integrity = mesh_integrity(garment)
        clearance = clearance_audit(surface, garment)
        cage_triangles = sum(len(polygon.vertices) - 2 for polygon in garment.data.polygons)
        evaluated = evaluated_triangles(garment)
        component_reports[garment.name] = {
            "family": garment["garmentFamily"],
            "construction": garment["construction"],
            "sourceFaceSets": json.loads(garment["sourceFaceSets"]),
            "innerClearanceMeters": float(garment["innerClearanceMeters"]),
            "integrity": integrity,
            "clearance": clearance,
            "cageTriangles": cage_triangles,
            "evaluatedTriangles": evaluated,
            "outwardOnlyThicknessMeters": float(garment["wallThicknessMeters"]),
        }
        if integrity["degenerateFaces"] or integrity["nonManifoldInteriorEdges"]:
            failures.append(f"{garment.name} mesh integrity")
        if clearance["status"] != "PASS_CLEARANCE_AND_BULK":
            failures.append(f"{garment.name} clearance/bulk")

    total_cage = sum(item["cageTriangles"] for item in component_reports.values())
    total_evaluated = sum(item["evaluatedTriangles"] for item in component_reports.values())
    camera = configure_scene(body, eyes, garments)
    evidence_paths = render_views(camera, evidence_root)
    bpy.ops.wm.save_as_mainfile(filepath=str(blend_path), compress=True, check_existing=False)
    report = {
        "schemaVersion": 1,
        "assetId": ASSET_ID,
        "status": "PASS_STRUCTURAL_AND_CLEARANCE_PENDING_MANUAL_FIT_REVIEW"
        if not failures
        else "REJECT_STRUCTURAL_OR_CLEARANCE_FIT_STUDY",
        "scope": "simple fitted long-sleeve top and trousers on accepted masculine BodyV3; not Hunter class art or production topology",
        "bodySource": {
            "blend": relative(BODY_BLEND),
            "blendSha256": EXPECTED_INPUTS[BODY_BLEND],
            "anatomicalRigLandmarksMeters": anatomy.as_json(),
        },
        "verifiedInputs": verified_inputs,
        "script": relative(SCRIPT_PATH),
        "scriptSha256": sha256(SCRIPT_PATH),
        "blend": relative(blend_path),
        "blendSha256": sha256(blend_path),
        "components": component_reports,
        "triangleCounts": {"cageTotal": total_cage, "evaluatedTotal": total_evaluated},
        "evidence": {relative(path): sha256(path) for path in evidence_paths},
        "failures": failures,
        "boundaries": {
            "manualFitReviewRequired": True,
            "bodyDerivedFitEnvelope": True,
            "productionGarmentTopologyAuthored": False,
            "bindPoseOnly": True,
            "masculineVariantOnly": True,
            "garmentDeformationTested": False,
            "manualArtAcceptance": False,
            "hunterMacroAcceptance": False,
            "uvsAuthored": False,
            "texturesAuthored": False,
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
        "status": "PENDING_MANUAL_CLOTHING_FIT_REVIEW" if not failures else "BLOCKED_BY_STRUCTURAL_FAILURES",
        "report": relative(report_path),
        "reportSha256": sha256(report_path),
        "evidence": list(report["evidence"]),
        "reviewQuestions": [
            "Do the sleeves meet the shoulder line and follow the actual elbows/wrists?",
            "Does the fitted top preserve the body's chest, waist, and neck proportions without floating bulk?",
            "Do the trousers follow hips, knees, calves, and ankles rather than reading as pipes?",
            "Do front, three-quarter, and back views read as one fitted base outfit on this body?",
            "Is this static fit direction good enough to retopologize manually before deformation tests?",
        ],
        "promotionAllowed": False,
    }
    review_request_path.write_text(
        json.dumps(review_request, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(report["status"])
    print("FIT_COMPONENTS", len(garments), "CAGE_TRIANGLES", total_cage, "EVALUATED_TRIANGLES", total_evaluated)
    print("EVIDENCE", len(evidence_paths))
    if failures:
        raise RuntimeError("; ".join(failures))


if __name__ == "__main__":
    main()
