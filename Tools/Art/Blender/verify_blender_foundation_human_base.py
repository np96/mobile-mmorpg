#!/usr/bin/env python3
"""Round-trip verify the retained Blender Foundation Human Base DCC subset."""

from __future__ import annotations

import bmesh
import bpy
from hashlib import sha256
import json
from pathlib import Path


PROJECT_ROOT = Path(__file__).resolve().parents[3]
SUBSET_ROOT = (
    PROJECT_ROOT
    / "SourceArt/ThirdParty/BlenderFoundation/HumanBaseMeshes_1.4.1/SelectedSubset"
)
SUBSET_BLEND = SUBSET_ROOT / "HumanBaseMeshes_StylizedBodies_v1.4.1.blend"
MANIFEST_PATH = SUBSET_ROOT / "HumanBaseMeshes_StylizedBodies_v1.4.1.manifest.json"
REPORT_PATH = SUBSET_ROOT / "HumanBaseMeshes_StylizedBodies_v1.4.1.verification.json"
EXPECTED = {
    "GEO-body_male_stylized",
    "GEO-body_male_stylized.eye.L",
    "GEO-body_male_stylized.eye.R",
    "GEO-body_female_stylized",
    "GEO-body_female_stylized.eye.L",
    "GEO-body_female_stylized.eye.R",
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


def mesh_metrics(obj: bpy.types.Object) -> dict[str, int]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    result = {
        "vertices": len(mesh.vertices),
        "polygons": len(mesh.polygons),
        "triangles": len(mesh.loop_triangles),
        "boundaryEdges": sum(edge.is_boundary for edge in bm.edges),
        "nonManifoldEdges": sum(not edge.is_manifold for edge in bm.edges),
        "degenerateFaces": sum(face.calc_area() <= 1e-12 for face in bm.faces),
    }
    bm.free()
    return result


def main() -> None:
    require(SUBSET_BLEND.is_file(), f"Missing retained subset: {SUBSET_BLEND}")
    require(MANIFEST_PATH.is_file(), f"Missing subset manifest: {MANIFEST_PATH}")
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    require(
        file_sha256(SUBSET_BLEND) == manifest["subsetBlendSha256"],
        "Retained subset hash changed",
    )
    bpy.ops.wm.open_mainfile(filepath=str(SUBSET_BLEND), load_ui=False)
    actual = {obj.name for obj in bpy.data.objects if obj.type == "MESH"}
    require(actual == EXPECTED, f"Unexpected retained mesh set: {sorted(actual)}")
    require(not bpy.data.armatures, "Subset contains an armature")
    require(not bpy.data.actions, "Subset contains actions")
    require(not bpy.data.materials, "Subset contains materials")
    require(not bpy.data.cameras, "Saved subset contains cameras")
    require(not bpy.data.lights, "Saved subset contains lights")
    require(
        {text.name for text in bpy.data.texts} == {"LICENSE_CC0_1.0.txt", "PROVENANCE.md"},
        "Subset metadata text set changed",
    )

    checks: dict[str, object] = {}
    for name in sorted(EXPECTED):
        obj = bpy.data.objects[name]
        current = mesh_metrics(obj)
        expected = manifest["objects"][name]["topology"]
        for key in current:
            require(current[key] == expected[key], f"{name} {key} changed")
        require([layer.name for layer in obj.data.uv_layers] == ["UVMap"], f"{name} UV0 changed")
        require(not obj.vertex_groups, f"{name} unexpectedly retains skin groups")
        require(not obj.modifiers, f"{name} unexpectedly retains modifiers")
        require(obj.animation_data is None, f"{name} unexpectedly retains animation data")
        require(not obj.material_slots, f"{name} unexpectedly retains material slots")
        checks[name] = current

    report = {
        "schemaVersion": 1,
        "status": "PASS_RETAINED_CC0_DCC_SUBSET_ROUND_TRIP",
        "subsetBlend": str(SUBSET_BLEND.relative_to(PROJECT_ROOT)),
        "subsetBlendSha256": file_sha256(SUBSET_BLEND),
        "objects": checks,
        "blenderVersion": list(bpy.app.version),
    }
    REPORT_PATH.write_text(
        json.dumps(report, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print("BLENDER_FOUNDATION_HUMAN_BASE_SUBSET_ROUND_TRIP_OK")


if __name__ == "__main__":
    main()
