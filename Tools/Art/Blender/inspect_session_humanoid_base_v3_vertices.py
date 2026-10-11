#!/usr/bin/env python3
"""Read-only diagnostics for the BodyV3 vertices named by deformation reports."""

from __future__ import annotations

from pathlib import Path
import sys

import bpy


SCRIPT_PATH = Path(__file__).resolve()
sys.path.insert(0, str(SCRIPT_PATH.parent))
import session_humanoid_base_v3_contract as contract


VERTICES = (15, 63, 69, 70, 75, 76, 191, 192, 3229, 3234, 3237, 3251, 3285, 3291, 3304, 3346, 3347, 3370, 3379, 3380, 3400, 3411, 3414, 3418, 3427, 3430, 3431, 3432, 3434, 3445, 3451, 3461, 3481, 3603, 3772, 3805, 3811,
            9459, 9461, 9500, 9501, 9514, 9519, 9526, 9527, 9988, 10257, 10263, 10514,
            10528, 10529, 9654, 9658, 9708, 9713, 9717)


def main() -> None:
    bpy.ops.wm.open_mainfile(filepath=str(contract.BLEND_PATH), load_ui=False)
    for variant in contract.VARIANTS:
        body = bpy.data.objects[contract.BODY_NAMES[variant]]
        memberships = contract.vertex_face_sets(body.data)
        print("VARIANT", variant)
        rig = bpy.data.objects[contract.RIG_NAMES[variant]]
        for side in ("Left", "Right"):
            shoulder = rig.data.bones[f"{side}Shoulder"]
            upper = rig.data.bones[f"{side}UpperArm"]
            print("BONES", side, "SHOULDER", tuple(shoulder.head_local), tuple(shoulder.tail_local), "UPPER", tuple(upper.head_local), tuple(upper.tail_local))
        for index in VERTICES:
            vertex = body.data.vertices[index]
            side = "Left" if vertex.co.x >= 0.0 else "Right"
            upper = rig.data.bones[f"{side}UpperArm"]
            axis = upper.tail_local - upper.head_local
            upper_t = (vertex.co - upper.head_local).dot(axis) / axis.length_squared
            weights = sorted(
                (
                    body.vertex_groups[item.group].name,
                    round(float(item.weight), 6),
                )
                for item in vertex.groups
            )
            print(
                "VERTEX",
                index,
                "CO",
                tuple(round(float(value), 6) for value in vertex.co),
                "FACE_SETS",
                sorted(memberships[index]),
                "UPPER_T",
                round(float(upper_t), 6),
                "WEIGHTS",
                weights,
            )


if __name__ == "__main__":
    main()
