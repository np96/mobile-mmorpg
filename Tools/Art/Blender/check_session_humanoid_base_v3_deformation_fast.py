#!/usr/bin/env python3
"""Fast, non-evidence deformation check used while tuning Body V3.

The release verifier remains the only gate: it independently validates contracts,
renders every plate, and writes the tracked proof report.  This helper deliberately
does neither so local weight-paint iterations do not masquerade as acceptance.
"""

from __future__ import annotations

from pathlib import Path
import sys

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import session_humanoid_base_v3_contract as contract


def group_rows(body: bpy.types.Object, indices: list[int]) -> dict[int, dict[str, float]]:
    names = {group.index: group.name for group in body.vertex_groups}
    return {
        index: {
            names[item.group]: round(float(item.weight), 6)
            for item in body.data.vertices[index].groups
            if item.weight > 1e-8
        }
        for index in indices
    }


def main() -> None:
    bpy.ops.wm.open_mainfile(filepath=str(contract.BLEND_PATH), load_ui=False)
    failures = 0
    for variant in contract.VARIANTS:
        body = bpy.data.objects[contract.BODY_NAMES[variant]]
        rig = bpy.data.objects[contract.RIG_NAMES[variant]]
        contract.reset_pose(rig)
        rest = contract.evaluated_positions(body)
        print(f"FAST_DEFORMATION {variant}")
        for pose in contract.POSES:
            contract.reset_pose(rig)
            pose.apply(rig)
            posed = contract.evaluated_positions(body)
            metrics = contract.deformation_metrics(body, rest, posed)
            rejected = (
                metrics["minimumTriangleAreaRatio"] < pose.minimum_area_ratio
                or metrics["minimumEdgeLengthRatio"] < pose.minimum_edge_ratio
                or metrics["maximumEdgeLengthRatio"] > pose.maximum_edge_ratio
                or metrics["degenerateTriangles"]
            )
            failures += bool(rejected)
            marker = "REJECT" if rejected else "PASS"
            print(
                marker,
                pose.name,
                f"area={metrics['minimumTriangleAreaRatio']:.6f}",
                f"edge=[{metrics['minimumEdgeLengthRatio']:.6f},{metrics['maximumEdgeLengthRatio']:.6f}]",
            )
            if rejected:
                indices = sorted(
                    set(metrics["minimumTriangle"]["vertices"])
                    | set(metrics["minimumEdge"]["vertices"])
                    | set(metrics["maximumEdge"]["vertices"])
                )
                print("  extremes", metrics["minimumTriangle"], metrics["minimumEdge"], metrics["maximumEdge"])
                print("  weights", group_rows(body, indices))
                print(
                    "  correctiveProps",
                    {
                        key: float(rig[key])
                        for key in rig.keys()
                        if key.startswith("PSC_") and float(rig[key]) > 1e-5
                    },
                )
                print(
                    "  correctiveValues",
                    {
                        key.name: float(key.value)
                        for key in body.data.shape_keys.key_blocks
                        if key.name != "Basis" and key.value > 1e-5
                    },
                )
        contract.reset_pose(rig)
    print("FAST_DEFORMATION_FAILURES", failures)


if __name__ == "__main__":
    main()
