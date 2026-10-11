#!/usr/bin/env python3
"""Build the Blender Foundation SessionHumanoidBaseV3 bind candidate.

This is a source/DCC build only.  It converts the selected stylized A-pose bodies
to measured T-poses, derives two rigs from the accepted Humanoid/Rigify template,
adds conventional twist deform bones, paints explicit regional four-weight fields,
and authors sparse property-driven pose-space volume correctives.  It never writes
an FBX or Unity asset; the independent deformation gate runs separately.
"""

from __future__ import annotations

from hashlib import sha256
import json
import math
from pathlib import Path

import bpy
from mathutils import Matrix, Vector

import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
import session_humanoid_base_v3_contract as contract


def activate(obj: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    obj.hide_set(False)
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj


def verify_inputs() -> dict[str, object]:
    for path in (contract.RIG_BLEND, contract.RIG_FBX, contract.RIG_MANIFEST, contract.SUBSET_BLEND, contract.SUBSET_MANIFEST):
        contract.require(path.is_file(), f"Missing frozen input: {path}")
    contract.require(
        contract.file_sha256(contract.RIG_BLEND) == contract.EXPECTED_RIG_BLEND_SHA256,
        "Accepted RigV2 blend changed",
    )
    contract.require(
        contract.file_sha256(contract.RIG_FBX) == contract.EXPECTED_RIG_FBX_SHA256,
        "Accepted RigV2 FBX changed",
    )
    contract.require(
        contract.file_sha256(contract.RIG_MANIFEST) == contract.EXPECTED_RIG_MANIFEST_SHA256,
        "Accepted RigV2 manifest changed",
    )
    contract.require(
        contract.file_sha256(contract.SUBSET_BLEND) == contract.EXPECTED_SUBSET_SHA256,
        "Selected Human Base subset changed; rerun intake review before binding",
    )
    subset_manifest = json.loads(contract.SUBSET_MANIFEST.read_text(encoding="utf-8"))
    contract.require(
        subset_manifest["selectionStatus"] == "SELECTED_FOR_BIND_PROOF_NOT_UNITY_ACCEPTED",
        "Human Base subset selection state changed",
    )
    return subset_manifest


def append_sources() -> dict[str, bpy.types.Object]:
    names = [name for variant in contract.VARIANTS for name in contract.SOURCE_NAMES[variant]]
    with bpy.data.libraries.load(str(contract.SUBSET_BLEND), link=False) as (source, target):
        missing = sorted(set(names) - set(source.objects))
        contract.require(not missing, f"Selected subset is missing {missing}")
        target.objects = names
    collection = bpy.data.collections.new("HumanBaseV3_SelectedSource_NotExported")
    bpy.context.scene.collection.children.link(collection)
    result: dict[str, bpy.types.Object] = {}
    for obj in target.objects:
        contract.require(obj is not None and obj.type == "MESH", "Selected source append failed")
        collection.objects.link(obj)
        obj.hide_render = True
        obj.hide_set(True)
        result[obj.name] = obj
    bpy.context.view_layer.update()
    return result


def source_world_bounds(obj: bpy.types.Object) -> tuple[Vector, Vector]:
    points = [obj.matrix_world @ Vector(corner) for corner in obj.bound_box]
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def world_baked_mesh(source: bpy.types.Object, name: str, offset: Vector) -> bpy.types.Object:
    mesh = source.data.copy()
    mesh.name = name
    mesh.transform(Matrix.Translation(offset) @ source.matrix_world)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    obj.matrix_world = Matrix.Identity(4)
    return obj


def extreme_average(
    mesh: bpy.types.Mesh,
    memberships: list[set[int]],
    face_sets: set[int],
    side: float,
    high: bool,
    fraction: float = 0.10,
) -> Vector:
    indices = [
        vertex.index
        for vertex in mesh.vertices
        if vertex.co.x * side > 0.0 and memberships[vertex.index] & face_sets
    ]
    contract.require(indices, f"No vertices found for face sets {sorted(face_sets)}")
    indices.sort(key=lambda index: abs(mesh.vertices[index].co.x), reverse=high)
    count = max(12, int(math.ceil(len(indices) * fraction)))
    selected = indices[:count]
    return sum((mesh.vertices[index].co for index in selected), Vector()) / len(selected)


def coordinate_extreme_average(
    mesh: bpy.types.Mesh,
    memberships: list[set[int]],
    face_sets: set[int],
    side: float,
    axis: int,
    high: bool,
    fraction: float = 0.12,
) -> Vector:
    indices = [
        vertex.index
        for vertex in mesh.vertices
        if vertex.co.x * side > 0.0 and memberships[vertex.index] & face_sets
    ]
    contract.require(indices, f"No vertices found for face sets {sorted(face_sets)}")
    indices.sort(key=lambda index: mesh.vertices[index].co[axis], reverse=high)
    count = max(10, int(math.ceil(len(indices) * fraction)))
    selected = indices[:count]
    return sum((mesh.vertices[index].co for index in selected), Vector()) / len(selected)


def face_set_center(
    mesh: bpy.types.Mesh,
    memberships: list[set[int]],
    face_set: int,
    side: float,
) -> Vector:
    indices = [
        vertex.index
        for vertex in mesh.vertices
        if vertex.co.x * side > 0.0 and face_set in memberships[vertex.index]
    ]
    contract.require(indices, f"No {('left' if side > 0.0 else 'right')} vertices for face set {face_set}")
    return sum((mesh.vertices[index].co for index in indices), Vector()) / len(indices)


def rotation_map(source_head: Vector, source_tail: Vector, target_head: Vector, target_tail: Vector):
    source_axis = source_tail - source_head
    target_axis = target_tail - target_head
    contract.require(source_axis.length > 1e-5 and target_axis.length > 1e-5, "Arm authoring axis collapsed")
    rotation = source_axis.normalized().rotation_difference(target_axis.normalized())
    scale = target_axis.length / source_axis.length

    def mapped(point: Vector) -> Vector:
        return target_head + rotation @ ((point - source_head) * scale)

    return mapped


def arm_landmarks_and_tpose(body: bpy.types.Object) -> tuple[dict[str, dict[str, Vector]], dict[str, object]]:
    mesh = body.data
    memberships = contract.vertex_face_sets(mesh)
    arms: dict[str, dict[str, Vector]] = {}
    transforms: dict[str, tuple] = {}
    for side_name, side in (("Left", 1.0), ("Right", -1.0)):
        # The upper-arm face set begins below the glenohumeral center in the
        # source A-pose.  Use the outer shoulder-girdle face set for the pivot;
        # otherwise a mathematically horizontal arm sits visibly below the chest.
        source_shoulder = extreme_average(
            mesh,
            memberships,
            contract.SHOULDER_FACE_SETS,
            side,
            True,
            0.35,
        )
        source_elbow_upper = extreme_average(mesh, memberships, contract.UPPER_ARM_FACE_SETS, side, True)
        source_elbow_lower = extreme_average(mesh, memberships, contract.FOREARM_FACE_SETS, side, False)
        source_wrist_lower = extreme_average(mesh, memberships, contract.FOREARM_FACE_SETS, side, True)
        source_wrist_hand = extreme_average(mesh, memberships, contract.HAND_FACE_SETS, side, False)
        source_fingertip = extreme_average(mesh, memberships, contract.HAND_FACE_SETS, side, True, 0.06)
        source_elbow = (source_elbow_upper + source_elbow_lower) * 0.5
        source_wrist = (source_wrist_lower + source_wrist_hand) * 0.5
        upper_length = (source_elbow - source_shoulder).length
        lower_length = (source_wrist - source_elbow).length
        hand_length = (source_fingertip - source_wrist).length
        target_shoulder = source_shoulder.copy()
        target_shoulder.y = (source_shoulder.y + source_elbow.y + source_wrist.y) / 3.0
        target_elbow = Vector((side * (abs(target_shoulder.x) + upper_length), target_shoulder.y, target_shoulder.z))
        target_wrist = Vector((side * (abs(target_elbow.x) + lower_length), target_shoulder.y, target_shoulder.z))
        target_fingertip = Vector((side * (abs(target_wrist.x) + hand_length), target_shoulder.y, target_shoulder.z))
        transforms[side_name] = (
            rotation_map(source_shoulder, source_elbow, target_shoulder, target_elbow),
            rotation_map(source_elbow, source_wrist, target_elbow, target_wrist),
            rotation_map(source_wrist, source_fingertip, target_wrist, target_fingertip),
        )
        arms[side_name] = {
            "sourceShoulder": source_shoulder,
            "sourceElbow": source_elbow,
            "sourceWrist": source_wrist,
            "sourceFingertip": source_fingertip,
            "shoulder": target_shoulder,
            "elbow": target_elbow,
            "wrist": target_wrist,
            "fingertip": target_fingertip,
            "handTail": target_wrist.lerp(target_fingertip, min(0.48, 0.065 / max(hand_length, 1e-6))),
        }

    original = [vertex.co.copy() for vertex in mesh.vertices]
    maximum_displacement = 0.0
    moved_vertices = 0
    for vertex, face_sets in zip(mesh.vertices, memberships):
        side_name = "Left" if vertex.co.x >= 0.0 else "Right"
        upper_map, lower_map, hand_map = transforms[side_name]
        arm = arms[side_name]
        point = original[vertex.index]
        mapped = point.copy()
        if face_sets & contract.HAND_FACE_SETS:
            mapped = hand_map(point)
        elif face_sets & contract.FOREARM_FACE_SETS:
            axis = arm["sourceWrist"] - arm["sourceElbow"]
            t = max(0.0, min(1.0, (point - arm["sourceElbow"]).dot(axis) / axis.length_squared))
            lower_point = lower_map(point)
            hand_point = hand_map(point)
            blend = contract.smoothstep(0.82, 1.0, t)
            mapped = lower_point.lerp(hand_point, blend)
        elif face_sets & contract.UPPER_ARM_FACE_SETS:
            axis = arm["sourceElbow"] - arm["sourceShoulder"]
            t = max(0.0, min(1.0, (point - arm["sourceShoulder"]).dot(axis) / axis.length_squared))
            upper_point = upper_map(point)
            lower_point = lower_map(point)
            shoulder_blend = contract.smoothstep(0.02, 0.20, t)
            elbow_blend = contract.smoothstep(0.82, 1.0, t)
            mapped = point.lerp(upper_point, shoulder_blend).lerp(lower_point, elbow_blend)
        elif face_sets & contract.SHOULDER_FACE_SETS:
            radial = abs(point.x)
            inner = max(0.0, abs(arm["sourceShoulder"].x) - 0.095)
            blend = contract.smoothstep(inner, abs(arm["sourceShoulder"].x) + 0.015, radial)
            mapped = point.lerp(upper_map(point), blend)
        vertex.co = mapped
        displacement = (mapped - point).length
        maximum_displacement = max(maximum_displacement, displacement)
        moved_vertices += displacement > 1e-7
    mesh.update()
    return arms, {
        "method": "explicit face-set regional rigid-chain A-pose to T-pose conversion",
        "movedVertices": moved_vertices,
        "maximumVertexDisplacementMeters": maximum_displacement,
        "topologyChanged": False,
        "arms": {
            side: {name: [round(value, 6) for value in point] for name, point in values.items()}
            for side, values in arms.items()
        },
    }


def prepare_variant(
    variant: str,
    sources: dict[str, bpy.types.Object],
) -> tuple[bpy.types.Object, tuple[bpy.types.Object, bpy.types.Object], dict[str, dict[str, Vector]], dict[str, object]]:
    body_source_name, eye_left_source_name, eye_right_source_name = contract.SOURCE_NAMES[variant]
    body_source = sources[body_source_name]
    minimum, maximum = source_world_bounds(body_source)
    center_x = (minimum.x + maximum.x) * 0.5
    offset = Vector((-center_x, 0.0, -minimum.z))
    body = world_baked_mesh(body_source, contract.BODY_NAMES[variant], offset)
    left_eye = world_baked_mesh(sources[eye_left_source_name], contract.EYE_NAMES[variant][0], offset)
    right_eye = world_baked_mesh(sources[eye_right_source_name], contract.EYE_NAMES[variant][1], offset)
    arms, conversion = arm_landmarks_and_tpose(body)
    body.data.calc_loop_triangles()
    conversion["sourceWorldCenterXMeters"] = center_x
    conversion["sourceGroundZMeters"] = minimum.z
    conversion["sourceHeightMeters"] = maximum.z - minimum.z
    conversion["triangles"] = len(body.data.loop_triangles)
    return body, (left_eye, right_eye), arms, conversion


def similarity_point(point: Vector, old_head: Vector, old_tail: Vector, new_head: Vector, new_tail: Vector) -> Vector:
    old_axis = old_tail - old_head
    new_axis = new_tail - new_head
    rotation = old_axis.normalized().rotation_difference(new_axis.normalized())
    return new_head + rotation @ ((point - old_head) * (new_axis.length / old_axis.length))


def eye_center(eye: bpy.types.Object) -> Vector:
    return sum((vertex.co for vertex in eye.data.vertices), Vector()) / len(eye.data.vertices)


def fit_variant_rig(
    variant: str,
    canonical: bpy.types.Object,
    body: bpy.types.Object,
    eyes: tuple[bpy.types.Object, bpy.types.Object],
    arms: dict[str, dict[str, Vector]],
) -> bpy.types.Object:
    rig = canonical.copy()
    rig.data = canonical.data.copy()
    rig.name = contract.RIG_NAMES[variant]
    rig.data.name = f"{contract.RIG_NAMES[variant]}_Armature"
    bpy.context.collection.objects.link(rig)
    rig.animation_data_clear()
    for pose_bone in rig.pose.bones:
        for constraint_item in list(pose_bone.constraints):
            pose_bone.constraints.remove(constraint_item)

    height = max(vertex.co.z for vertex in body.data.vertices) - min(vertex.co.z for vertex in body.data.vertices)
    scale = height / contract.CANONICAL_HEIGHT
    memberships = contract.vertex_face_sets(body.data)
    activate(rig)
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = rig.data.edit_bones
    for bone in edit_bones:
        bone.head *= scale
        bone.tail *= scale
    scaled_rest = {bone.name: (bone.head.copy(), bone.tail.copy()) for bone in edit_bones}

    for side in ("Left", "Right"):
        arm = arms[side]
        shoulder = edit_bones[f"{side}Shoulder"]
        upper = edit_bones[f"{side}UpperArm"]
        lower = edit_bones[f"{side}LowerArm"]
        hand = edit_bones[f"{side}Hand"]
        side_sign = 1.0 if side == "Left" else -1.0
        shoulder.head = Vector((side_sign * 0.025 * scale, arm["shoulder"].y, arm["shoulder"].z - 0.012 * scale))
        shoulder.tail = arm["shoulder"]
        upper.head = arm["shoulder"]
        upper.tail = arm["elbow"]
        lower.head = arm["elbow"]
        lower.tail = arm["wrist"]
        old_hand_head, old_hand_tail = scaled_rest[f"{side}Hand"]
        hand.head = arm["wrist"]
        hand.tail = arm["handTail"]
        # Fit every phalanx through the center of its official source face set.
        # This preserves the source hand's authored fan and avoids crossing the
        # fingers with a nearest-bone guess inherited from another body.
        for prefix, face_set_chain in contract.FINGER_FACE_SET_CHAINS.items():
            centers = [
                face_set_center(body.data, memberships, face_set, side_sign)
                for face_set in face_set_chain
            ]
            boundaries = (
                centers[0] - (centers[1] - centers[0]) * 0.55,
                centers[0].lerp(centers[1], 0.5),
                centers[1].lerp(centers[2], 0.5),
                centers[2] + (centers[2] - centers[1]) * 0.62,
            )
            for segment, head_point, tail_point in zip(
                contract.FINGER_SEGMENTS,
                boundaries,
                boundaries[1:],
            ):
                bone = edit_bones[f"{side}{prefix}{segment}"]
                bone.head = head_point
                bone.tail = tail_point
        socket_name = f"Socket_Weapon_{'L' if side == 'Left' else 'R'}"
        socket = edit_bones[socket_name]
        old_head, old_tail = scaled_rest[socket_name]
        socket.head = similarity_point(old_head, old_hand_head, old_hand_tail, hand.head, hand.tail)
        socket.tail = similarity_point(old_tail, old_hand_head, old_hand_tail, hand.head, hand.tail)
        legacy = edit_bones[f"{side}ElbowSupport"]
        legacy.head = arm["elbow"]
        legacy.tail = arm["elbow"].lerp(arm["wrist"], 0.32)
        legacy.use_deform = False

        # The feminine and masculine source bodies have materially different
        # leg proportions.  Fit each game-rig chain to its own body instead of
        # uniformly scaling the accepted canonical skeleton.
        hip = coordinate_extreme_average(
            body.data, memberships, contract.THIGH_FACE_SETS, side_sign, 2, True, 0.14
        )
        knee = (
            coordinate_extreme_average(
                body.data, memberships, contract.THIGH_FACE_SETS, side_sign, 2, False, 0.12
            )
            + coordinate_extreme_average(
                body.data, memberships, contract.CALF_FACE_SETS, side_sign, 2, True, 0.12
            )
        ) * 0.5
        ankle = (
            coordinate_extreme_average(
                body.data, memberships, contract.CALF_FACE_SETS, side_sign, 2, False, 0.10
            )
            + face_set_center(body.data, memberships, 288, side_sign)
        ) * 0.5
        ball = (
            coordinate_extreme_average(
                body.data, memberships, contract.MAIN_FOOT_FACE_SETS, side_sign, 1, False, 0.18
            )
            + coordinate_extreme_average(
                body.data, memberships, contract.TOE_FACE_SETS, side_sign, 1, True, 0.18
            )
        ) * 0.5
        toe_tip = coordinate_extreme_average(
            body.data, memberships, contract.TOE_FACE_SETS, side_sign, 1, False, 0.12
        )
        upper_leg = edit_bones[f"{side}UpperLeg"]
        lower_leg = edit_bones[f"{side}LowerLeg"]
        foot = edit_bones[f"{side}Foot"]
        toes = edit_bones[f"{side}Toes"]
        upper_leg.head, upper_leg.tail = hip, knee
        lower_leg.head, lower_leg.tail = knee, ankle
        foot.head, foot.tail = ankle, ball
        toes.head, toes.tail = ball, toe_tip

    left_eye_center = eye_center(eyes[0])
    right_eye_center = eye_center(eyes[1])
    eye_z = (left_eye_center.z + right_eye_center.z) * 0.5
    eye_y = (left_eye_center.y + right_eye_center.y) * 0.5
    head_head = Vector((0.0, eye_y + 0.035 * scale, eye_z - 0.115 * scale))
    head_tail = Vector((0.0, eye_y + 0.04 * scale, height - 0.012 * scale))
    neck = edit_bones["Neck"]
    neck.tail = head_head
    neck.head = head_head - Vector((0.0, -0.012 * scale, 0.095 * scale))
    head = edit_bones["Head"]
    head.head = head_head
    head.tail = head_tail
    jaw = edit_bones["Jaw"]
    jaw.head = Vector((0.0, eye_y + 0.018 * scale, eye_z - 0.035 * scale))
    jaw.tail = Vector((0.0, eye_y - 0.052 * scale, eye_z - 0.115 * scale))
    for name, center in (("LeftEye", left_eye_center), ("RightEye", right_eye_center)):
        bone = edit_bones[name]
        bone.head = center
        bone.tail = center + Vector((0.0, -0.045 * scale, 0.0))
    head_socket = edit_bones["Socket_HeadFX"]
    head_socket.head = Vector((0.0, 0.0, height - 0.005 * scale))
    head_socket.tail = head_socket.head + Vector((0.0, 0.0, 0.10 * scale))

    twist_specs = (
        ("LeftUpperArmTwist", "LeftUpperArm"),
        ("LeftForearmTwist", "LeftLowerArm"),
        ("RightUpperArmTwist", "RightUpperArm"),
        ("RightForearmTwist", "RightLowerArm"),
        ("LeftThighTwist", "LeftUpperLeg"),
        ("LeftCalfTwist", "LeftLowerLeg"),
        ("RightThighTwist", "RightUpperLeg"),
        ("RightCalfTwist", "RightLowerLeg"),
    )
    for twist_name, owner_name in twist_specs:
        owner = edit_bones[owner_name]
        twist = edit_bones.new(twist_name)
        twist.head = owner.head.lerp(owner.tail, 0.30)
        twist.tail = owner.head.lerp(owner.tail, 0.78)
        twist.parent = owner
        twist.use_connect = False
        twist.use_deform = True
        twist.roll = owner.roll
    bpy.ops.object.mode_set(mode="OBJECT")
    for name in contract.LEGACY_SUPPORT_BONES:
        rig.data.bones[name].use_deform = False
    for name in contract.TWIST_BONES:
        rig.data.bones[name].use_deform = True
    for name in contract.TWIST_BONES:
        contract.require(name in rig.pose.bones, f"Missing conventional twist bone {name}")
    rig["sourceHumanoidRig"] = "SessionHumanoidRigV2"
    rig["sourceRigifyControls"] = contract.CANONICAL_CONTROL_NAME
    rig["restFitVariant"] = variant
    rig["twistArchitecture"] = "one conventional deform twist per upper arm, forearm, thigh, and calf"
    return rig


def chain_weights(value: float, anchors: list[tuple[str, float]]) -> dict[str, float]:
    ordered = sorted(anchors, key=lambda item: item[1])
    if value <= ordered[0][1]:
        return {ordered[0][0]: 1.0}
    if value >= ordered[-1][1]:
        return {ordered[-1][0]: 1.0}
    for (left_name, left_value), (right_name, right_value) in zip(ordered, ordered[1:]):
        if left_value <= value <= right_value:
            t = (value - left_value) / max(1e-8, right_value - left_value)
            return {left_name: 1.0 - t, right_name: t}
    raise RuntimeError("Chain interpolation failed")


def finger_weights(point: Vector, rig: bpy.types.Object, side: str) -> dict[str, float]:
    candidates = []
    for prefix in contract.FINGER_PREFIXES:
        for segment in contract.FINGER_SEGMENTS:
            name = f"{side}{prefix}{segment}"
            bone = rig.data.bones[name]
            candidates.append((contract.distance_to_segment(point, bone.head_local, bone.tail_local), name))
    candidates.sort()
    nearest_distance, nearest = candidates[0]
    hand = f"{side}Hand"
    finger_factor = contract.smoothstep(0.025, 0.075, nearest_distance * -1.0 + 0.075)
    return {nearest: 0.78 * finger_factor, hand: 1.0 - 0.78 * finger_factor}


def smooth_weight_rows(
    body: bpy.types.Object,
    rows: list[dict[str, float]],
    passes: int = 16,
    neighbor_blend: float = 0.32,
) -> list[dict[str, float]]:
    """Diffuse only across actual surface neighbors, then retain four weights.

    This is the scripted equivalent of manually smoothing painted boundary loops:
    seed regions remain explicit, and no heat/envelope/automatic-weight operator is
    used.  Opposite-side limb weights are clipped away from the center line so the
    groin and sternum cannot acquire remote arm or leg influences.
    """
    adjacency = [set() for _vertex in body.data.vertices]
    for edge in body.data.edges:
        first, second = edge.vertices
        adjacency[first].add(second)
        adjacency[second].add(first)
    current = [dict(row) for row in rows]
    for _pass in range(passes):
        updated: list[dict[str, float]] = []
        for index, row in enumerate(current):
            neighbors = adjacency[index]
            neighbor_average: dict[str, float] = {}
            if neighbors:
                divisor = float(len(neighbors))
                for neighbor in neighbors:
                    for name, weight in current[neighbor].items():
                        neighbor_average[name] = neighbor_average.get(name, 0.0) + weight / divisor
            names = set(row) | set(neighbor_average)
            blended = {
                name: row.get(name, 0.0) * (1.0 - neighbor_blend)
                + neighbor_average.get(name, 0.0) * neighbor_blend
                for name in names
            }
            x = body.data.vertices[index].co.x
            if x > 0.012:
                blended = {name: weight for name, weight in blended.items() if not name.startswith("Right")}
            elif x < -0.012:
                blended = {name: weight for name, weight in blended.items() if not name.startswith("Left")}
            total = sum(weight for weight in blended.values() if weight > 1e-10)
            contract.require(total > 1e-10, f"Smoothing erased vertex {index}")
            updated.append(
                {name: weight / total for name, weight in blended.items() if weight > 1e-10}
            )
        current = updated
    return [contract.normalize_top_four(row) for row in current]


def smooth_selected_rows(
    body: bpy.types.Object,
    rows: list[dict[str, float]],
    indices: set[int],
    allowed_names: dict[int, set[str]],
    passes: int,
    neighbor_blend: float,
) -> list[dict[str, float]]:
    """Manual cleanup pass for one named joint region."""
    adjacency = [set() for _vertex in body.data.vertices]
    for edge in body.data.edges:
        first, second = edge.vertices
        adjacency[first].add(second)
        adjacency[second].add(first)
    current = [dict(row) for row in rows]
    for _pass in range(passes):
        updated = [dict(row) for row in current]
        for index in indices:
            neighbors = adjacency[index]
            average: dict[str, float] = {}
            for neighbor in neighbors:
                for name, weight in current[neighbor].items():
                    average[name] = average.get(name, 0.0) + weight / max(1, len(neighbors))
            names = allowed_names[index]
            blended = {
                name: current[index].get(name, 0.0) * (1.0 - neighbor_blend)
                + average.get(name, 0.0) * neighbor_blend
                for name in names
            }
            total = sum(max(0.0, value) for value in blended.values())
            contract.require(total > 1e-10, f"Joint cleanup erased vertex {index}")
            updated[index] = {
                name: max(0.0, value) / total
                for name, value in blended.items()
                if value > 1e-10
            }
        current = updated
    return [contract.normalize_top_four(row) for row in current]


def clean_joint_weight_regions(
    body: bpy.types.Object,
    rig: bpy.types.Object,
    memberships: list[set[int]],
    rows: list[dict[str, float]],
) -> list[dict[str, float]]:
    """Apply the authored final polish passes used for the proof plate joints."""
    hip_indices: set[int] = set()
    hip_allowed: dict[int, set[str]] = {}
    thumb_indices: set[int] = set()
    thumb_allowed: dict[int, set[str]] = {}
    thumb_sets = set(contract.FINGER_FACE_SET_CHAINS["Thumb"])
    for vertex, face_sets in zip(body.data.vertices, memberships):
        point = vertex.co
        side = "Left" if point.x >= 0.0 else "Right"
        upper_leg = rig.data.bones[f"{side}UpperLeg"]
        if face_sets & contract.THIGH_FACE_SETS and (point - upper_leg.head_local).length < 0.20:
            hip_indices.add(vertex.index)
            hip_allowed[vertex.index] = {
                "Hips",
                f"{side}UpperLeg",
                f"{side}ThighTwist",
            }
        if face_sets & thumb_sets:
            thumb_indices.add(vertex.index)
            thumb_allowed[vertex.index] = {
                f"{side}Hand",
                f"{side}ThumbProximal",
                f"{side}ThumbIntermediate",
                f"{side}ThumbDistal",
            }
    rows = smooth_selected_rows(
        body, rows, hip_indices, hip_allowed, passes=10, neighbor_blend=0.34
    )
    rows = smooth_selected_rows(
        body, rows, thumb_indices, thumb_allowed, passes=20, neighbor_blend=0.38
    )
    return rows


def author_shoulder_transition_rows(
    body: bpy.types.Object,
    rig: bpy.types.Object,
    memberships: list[set[int]],
    rows: list[dict[str, float]],
) -> list[dict[str, float]]:
    """Paint a continuous chest-to-clavicle-to-arm profile in bone space.

    The medial shoulder surface must remain predominantly UpperChest.  Treating
    the clavicle/Shoulder bone as the static anchor made neighboring neck and
    shoulder rows diverge when an animation combined head aim, torso cant, and
    arm reach.  The profile below brings Shoulder influence in gradually and
    leaves the lateral cap predominantly UpperArm.
    """
    result = [dict(row) for row in rows]
    region = contract.SHOULDER_FACE_SETS | contract.UPPER_ARM_FACE_SETS
    for vertex, face_sets in zip(body.data.vertices, memberships):
        if not face_sets & region:
            continue
        side = "Left" if vertex.co.x >= 0.0 else "Right"
        bone = rig.data.bones[f"{side}UpperArm"]
        axis = bone.tail_local - bone.head_local
        t = (vertex.co - bone.head_local).dot(axis) / max(1e-10, axis.length_squared)
        if t < -0.72 or t > 0.55:
            continue
        socket_motion = 0.34 * contract.smoothstep(-0.28, 0.0, t)
        arm_motion = 0.58 * contract.smoothstep(0.0, 0.50, t)
        moving = min(0.92, socket_motion + arm_motion)
        twist_fraction = 0.08 * contract.smoothstep(0.12, 0.50, t)
        residual = 1.0 - moving
        clavicle_share = 0.28 * contract.smoothstep(-0.62, 0.04, t)
        result[vertex.index] = contract.normalize_top_four(
            {
                f"{side}UpperArm": moving * (1.0 - twist_fraction),
                f"{side}UpperArmTwist": moving * twist_fraction,
                f"{side}Shoulder": residual * clavicle_share,
                "UpperChest": residual * (1.0 - clavicle_share),
            }
        )
    return result


def author_collar_transition_rows(
    body: bpy.types.Object,
    rig: bpy.types.Object,
    memberships: list[set[int]],
    rows: list[dict[str, float]],
) -> list[dict[str, float]]:
    """Remove the torso/shoulder Neck-weight step across the clavicle span.

    The Foundation torso and shoulder are separate sculpt face sets. Surface
    smoothing therefore left the medial torso ring partly on Neck while its
    edge-adjacent shoulder ring was UpperChest-dominant. Combined head aim,
    chest cant, and bow reach exposed that discontinuity even though isolated
    arm plates passed. This explicit fitted-clavicle ramp transfers only the
    lateral torso's Neck share into UpperChest; existing shoulder/arm shares are
    preserved and the inner collar remains free to follow Neck.
    """

    result = [dict(row) for row in rows]
    for vertex, face_sets in zip(body.data.vertices, memberships):
        if not face_sets & contract.TORSO_FACE_SETS or face_sets & contract.HEAD_FACE_SETS:
            continue
        side = "Left" if vertex.co.x >= 0.0 else "Right"
        shoulder = rig.data.bones[f"{side}Shoulder"]
        inner = abs(float(shoulder.head_local.x))
        outer = abs(float(shoulder.tail_local.x))
        span = max(1.0e-6, outer - inner)
        clavicle_t = (abs(float(vertex.co.x)) - inner) / span
        lateral = contract.smoothstep(0.05, 0.32, clavicle_t)
        height_above = float(vertex.co.z - shoulder.tail_local.z) / span
        lateral *= 1.0 - contract.smoothstep(0.10, 0.35, height_above)
        depth_below = float(shoulder.tail_local.z - vertex.co.z) / span
        below_clavicle = contract.smoothstep(0.25, 0.65, depth_below)
        transfer_factor = max(lateral, below_clavicle)
        neck = result[vertex.index].get("Neck", 0.0)
        if neck <= 1.0e-10 or transfer_factor <= 0.0:
            continue
        transferred = neck * transfer_factor
        updated = dict(result[vertex.index])
        updated["Neck"] = neck - transferred
        updated["UpperChest"] = updated.get("UpperChest", 0.0) + transferred
        result[vertex.index] = contract.normalize_top_four(updated)
    return result


def smooth_collar_anchor_shares(
    body: bpy.types.Object,
    memberships: list[set[int]],
    rows: list[dict[str, float]],
    *,
    passes: int = 12,
    neighbor_blend: float = 0.34,
) -> list[dict[str, float]]:
    """Polish only the Neck/UpperChest split without moving arm weights.

    Boundary loops on the sculpted torso vary in height and density, so a
    coordinate ramp alone can leave adjacent triangles with different anchor
    ownership. This surface-neighbor polish freezes every Shoulder/UpperArm,
    Chest, and Spine value, smoothing only how each row's existing anchor share
    is divided between Neck and UpperChest. Head and shoulder neighbors still
    participate as fixed boundary conditions.
    """

    adjacency = [set() for _vertex in body.data.vertices]
    for edge in body.data.edges:
        first, second = edge.vertices
        adjacency[first].add(second)
        adjacency[second].add(first)
    selected = {
        vertex.index
        for vertex, face_sets in zip(body.data.vertices, memberships)
        if face_sets & contract.TORSO_FACE_SETS
        and not face_sets & contract.HEAD_FACE_SETS
        and rows[vertex.index].get("Neck", 0.0)
        + rows[vertex.index].get("UpperChest", 0.0)
        > 0.25
    }
    current = [dict(row) for row in rows]
    for _pass in range(passes):
        updated = [dict(row) for row in current]
        for index in selected:
            row = current[index]
            anchor_total = row.get("Neck", 0.0) + row.get("UpperChest", 0.0)
            if anchor_total <= 1.0e-10:
                continue
            fractions = []
            for neighbor in adjacency[index]:
                neighbor_row = current[neighbor]
                neighbor_total = (
                    neighbor_row.get("Neck", 0.0)
                    + neighbor_row.get("UpperChest", 0.0)
                )
                if neighbor_total > 0.05:
                    fractions.append(neighbor_row.get("Neck", 0.0) / neighbor_total)
            if not fractions:
                continue
            current_fraction = row.get("Neck", 0.0) / anchor_total
            neighbor_fraction = sum(fractions) / len(fractions)
            neck_fraction = (
                current_fraction * (1.0 - neighbor_blend)
                + neighbor_fraction * neighbor_blend
            )
            polished = {
                name: weight
                for name, weight in row.items()
                if name not in {"Neck", "UpperChest"}
            }
            if neck_fraction > 1.0e-10:
                polished["Neck"] = anchor_total * neck_fraction
            if neck_fraction < 1.0 - 1.0e-10:
                polished["UpperChest"] = anchor_total * (1.0 - neck_fraction)
            updated[index] = contract.normalize_top_four(polished)
        current = updated
    return current


def author_thumb_transition_rows(
    body: bpy.types.Object,
    memberships: list[set[int]],
    rows: list[dict[str, float]],
) -> list[dict[str, float]]:
    """Paint fixed, topology-backed rows for the three thumb phalanges."""
    result = [dict(row) for row in rows]
    chain = contract.FINGER_FACE_SET_CHAINS["Thumb"]
    templates = (
        (("Hand", 0.40), ("ThumbProximal", 0.60)),
        (("ThumbProximal", 0.30), ("ThumbIntermediate", 0.70)),
        (("ThumbIntermediate", 0.30), ("ThumbDistal", 0.70)),
    )
    by_face_set = dict(zip(chain, templates))
    for vertex, face_sets in zip(body.data.vertices, memberships):
        hits = [face_set for face_set in chain if face_set in face_sets]
        if not hits:
            continue
        side = "Left" if vertex.co.x >= 0.0 else "Right"
        weights: dict[str, float] = {}
        for face_set in hits:
            for suffix, value in by_face_set[face_set]:
                name = f"{side}{suffix}"
                weights[name] = weights.get(name, 0.0) + value / len(hits)
        result[vertex.index] = contract.normalize_top_four(weights)
    return result


def paint_body_weights(body: bpy.types.Object, rig: bpy.types.Object) -> dict[str, object]:
    memberships = contract.vertex_face_sets(body.data)
    for group in list(body.vertex_groups):
        body.vertex_groups.remove(group)
    weighted_bones = [bone.name for bone in rig.data.bones if bone.use_deform]
    groups = {name: body.vertex_groups.new(name=name) for name in weighted_bones}
    finger_face_to_segment = {
        face_set: (prefix, segment)
        for prefix, chain in contract.FINGER_FACE_SET_CHAINS.items()
        for face_set, segment in zip(chain, contract.FINGER_SEGMENTS)
    }
    rows: list[dict[str, float]] = []
    for vertex, face_sets in zip(body.data.vertices, memberships):
        point = vertex.co
        side = "Left" if point.x >= 0.0 else "Right"
        weights: dict[str, float]
        if face_sets & contract.HAND_FACE_SETS:
            segment_hits = [
                finger_face_to_segment[face_set]
                for face_set in sorted(face_sets)
                if face_set in finger_face_to_segment
            ]
            if segment_hits:
                weights = {}
                for prefix, segment in segment_hits:
                    name = f"{side}{prefix}{segment}"
                    weights[name] = weights.get(name, 0.0) + 1.0 / len(segment_hits)
                    if segment == "Proximal":
                        weights[name] *= 0.72
                        weights[f"{side}Hand"] = weights.get(f"{side}Hand", 0.0) + 0.28 / len(segment_hits)
            else:
                weights = {f"{side}Hand": 1.0}
        elif face_sets & contract.FOREARM_FACE_SETS:
            lower = rig.data.bones[f"{side}LowerArm"]
            axis = lower.tail_local - lower.head_local
            t = max(0.0, min(1.0, (point - lower.head_local).dot(axis) / axis.length_squared))
            shoulder_blend = (1.0 - contract.smoothstep(0.00, 0.24, t)) * 0.58
            hand_blend = contract.smoothstep(0.76, 1.0, t) * 0.58
            twist = contract.smoothstep(0.18, 0.55, t) * (1.0 - contract.smoothstep(0.68, 0.94, t)) * 0.34
            weights = {
                f"{side}UpperArm": shoulder_blend,
                f"{side}LowerArm": 1.0 - twist - hand_blend - shoulder_blend,
                f"{side}ForearmTwist": twist,
                f"{side}Hand": hand_blend,
            }
        elif face_sets & contract.UPPER_ARM_FACE_SETS:
            upper = rig.data.bones[f"{side}UpperArm"]
            axis = upper.tail_local - upper.head_local
            t = max(0.0, min(1.0, (point - upper.head_local).dot(axis) / axis.length_squared))
            shoulder_blend = (1.0 - contract.smoothstep(0.00, 0.25, t)) * 0.68
            elbow_blend = contract.smoothstep(0.76, 1.0, t) * 0.58
            twist = contract.smoothstep(0.20, 0.50, t) * (1.0 - contract.smoothstep(0.66, 0.92, t)) * 0.32
            weights = {
                f"{side}Shoulder": shoulder_blend,
                f"{side}UpperArm": 1.0 - shoulder_blend - elbow_blend - twist,
                f"{side}UpperArmTwist": twist,
                f"{side}LowerArm": elbow_blend,
            }
        elif face_sets & contract.SHOULDER_FACE_SETS and abs(point.x) > 0.055:
            shoulder = rig.data.bones[f"{side}Shoulder"]
            radial = abs(point.x)
            t = contract.smoothstep(abs(shoulder.head_local.x), abs(shoulder.tail_local.x) + 0.035, radial)
            weights = {
                "UpperChest": 1.0 - t * 0.62,
                f"{side}Shoulder": t * 0.38,
                f"{side}UpperArm": t * 0.24,
            }
        elif face_sets & contract.FOOT_FACE_SETS:
            if face_sets & contract.TOE_FACE_SETS:
                weights = {f"{side}Foot": 0.18, f"{side}Toes": 0.82}
            elif face_sets & contract.ANKLE_FACE_SETS:
                weights = {f"{side}LowerLeg": 0.48, f"{side}Foot": 0.52}
            else:
                foot = rig.data.bones[f"{side}Foot"]
                toes = rig.data.bones[f"{side}Toes"]
                toe_factor = contract.smoothstep(
                    0.15,
                    0.90,
                    (foot.tail_local.y - point.y)
                    / max(1e-6, foot.tail_local.y - toes.tail_local.y),
                )
                weights = {f"{side}Foot": 1.0 - 0.42 * toe_factor, f"{side}Toes": 0.42 * toe_factor}
        elif face_sets & contract.CALF_FACE_SETS:
            lower = rig.data.bones[f"{side}LowerLeg"]
            axis = lower.tail_local - lower.head_local
            t = max(0.0, min(1.0, (point - lower.head_local).dot(axis) / axis.length_squared))
            knee_blend = (1.0 - contract.smoothstep(0.00, 0.25, t)) * 0.58
            foot_blend = contract.smoothstep(0.74, 1.0, t) * 0.62
            twist = contract.smoothstep(0.20, 0.48, t) * (1.0 - contract.smoothstep(0.64, 0.90, t)) * 0.30
            weights = {
                f"{side}UpperLeg": knee_blend,
                f"{side}LowerLeg": 1.0 - knee_blend - foot_blend - twist,
                f"{side}CalfTwist": twist,
                f"{side}Foot": foot_blend,
            }
        elif face_sets & contract.THIGH_FACE_SETS:
            upper = rig.data.bones[f"{side}UpperLeg"]
            axis = upper.tail_local - upper.head_local
            t = max(0.0, min(1.0, (point - upper.head_local).dot(axis) / axis.length_squared))
            hip_blend = (1.0 - contract.smoothstep(0.00, 0.40, t)) * 0.88
            knee_blend = contract.smoothstep(0.74, 1.0, t) * 0.58
            twist = contract.smoothstep(0.18, 0.46, t) * (1.0 - contract.smoothstep(0.62, 0.90, t)) * 0.30
            weights = {
                "Hips": hip_blend,
                f"{side}UpperLeg": 1.0 - hip_blend - knee_blend - twist,
                f"{side}ThighTwist": twist,
                f"{side}LowerLeg": knee_blend,
            }
        elif face_sets & contract.HEAD_FACE_SETS:
            head = rig.data.bones["Head"]
            jaw = rig.data.bones["Jaw"]
            front_factor = contract.smoothstep(
                0.015,
                0.085,
                (jaw.head_local.y + 0.045) - point.y,
            )
            jaw_factor = (
                contract.smoothstep(jaw.tail_local.z - 0.025, jaw.head_local.z + 0.025, point.z)
                * front_factor
            )
            jaw_factor = min(0.82, max(0.0, jaw_factor))
            neck_factor = 1.0 - contract.smoothstep(head.head_local.z - 0.045, head.head_local.z + 0.045, point.z)
            weights = {"Neck": neck_factor * (1.0 - jaw_factor), "Head": (1.0 - neck_factor) * (1.0 - jaw_factor), "Jaw": jaw_factor}
        else:
            anchors = [
                ("Hips", rig.data.bones["Hips"].head_local.z),
                ("Spine", rig.data.bones["Spine"].head_local.z),
                ("Chest", rig.data.bones["Chest"].head_local.z),
                ("UpperChest", rig.data.bones["UpperChest"].head_local.z),
                ("Neck", rig.data.bones["Neck"].head_local.z),
            ]
            weights = chain_weights(point.z, anchors)
        rows.append(contract.normalize_top_four(weights))

    rows = smooth_weight_rows(body, rows)
    rows = author_shoulder_transition_rows(body, rig, memberships, rows)
    rows = author_collar_transition_rows(body, rig, memberships, rows)
    rows = smooth_collar_anchor_shares(body, memberships, rows)
    rows = author_thumb_transition_rows(body, memberships, rows)
    rows = clean_joint_weight_regions(body, rig, memberships, rows)
    for vertex, row in zip(body.data.vertices, rows):
        for name, weight in row.items():
            contract.require(name in groups, f"Weight references non-deform bone {name}")
            groups[name].add([vertex.index], weight, "REPLACE")

    maximum = max(len(row) for row in rows)
    minimum_sum = min(sum(row.values()) for row in rows)
    maximum_sum = max(sum(row.values()) for row in rows)
    digest = sha256()
    for index, row in enumerate(rows):
        digest.update(index.to_bytes(4, "little"))
        for name, weight in sorted(row.items()):
            digest.update(name.encode("utf-8") + b"\0")
            digest.update(float(weight).hex().encode("ascii") + b"\n")
    return {
        "method": "explicit anatomical face-set regions, fitted clavicle/collar ramps with twelve anchor-only polish passes, per-phalanx hand maps, and sixteen manually tuned surface-neighbor smoothing passes; Blender automatic weights not used",
        "vertexCount": len(rows),
        "maximumInfluences": maximum,
        "minimumWeightSum": minimum_sum,
        "maximumWeightSum": maximum_sum,
        "weightedBoneCount": len([group for group in groups.values() if group.name in weighted_bones]),
        "weightFieldSha256": digest.hexdigest(),
        "legacyElbowSupportWeights": 0,
        "twistBonesWeighted": list(contract.TWIST_BONES),
    }


def bind_mesh(obj: bpy.types.Object, rig: bpy.types.Object) -> None:
    obj.parent = rig
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.matrix_basis = Matrix.Identity(4)
    modifier = obj.modifiers.new("SessionHumanoidBaseV3_Armature", "ARMATURE")
    modifier.object = rig
    modifier.use_deform_preserve_volume = True


def bind_eyes(eyes: tuple[bpy.types.Object, bpy.types.Object], rig: bpy.types.Object) -> dict[str, object]:
    report = {}
    for eye, bone_name in zip(eyes, ("LeftEye", "RightEye")):
        for group in list(eye.vertex_groups):
            eye.vertex_groups.remove(group)
        group = eye.vertex_groups.new(name=bone_name)
        group.add(list(range(len(eye.data.vertices))), 1.0, "REPLACE")
        bind_mesh(eye, rig)
        report[eye.name] = {"bone": bone_name, "vertices": len(eye.data.vertices), "weight": 1.0}
    return report


def radial_corrective(
    body: bpy.types.Object,
    rig: bpy.types.Object,
    memberships: list[set[int]],
    name: str,
    pivot: Vector,
    axis: Vector,
    radius: float,
    magnitude: float,
    allowed_sets: set[int],
    side: float,
) -> dict[str, object]:
    key = body.shape_key_add(name=name, from_mix=False)
    property_name = f"PSC_{name}"
    rig[property_name] = 0.0
    affected = 0
    maximum = 0.0
    axis = axis.normalized()
    for vertex, face_sets in zip(body.data.vertices, memberships):
        point = vertex.co
        if point.x * side <= 0.0 or not face_sets & allowed_sets:
            continue
        distance = (point - pivot).length
        if distance >= radius:
            continue
        radial = point - pivot - axis * (point - pivot).dot(axis)
        if radial.length <= 1e-7:
            continue
        falloff = (1.0 - distance / radius) ** 2
        offset = radial.normalized() * magnitude * falloff
        key.data[vertex.index].co += offset
        affected += 1
        maximum = max(maximum, offset.length)
    contract.require(affected >= 12, f"Corrective {name} is not a meaningful sparse region")
    fcurve = key.driver_add("value")
    driver = fcurve.driver
    driver.type = "SCRIPTED"
    variable = driver.variables.new()
    variable.name = "control"
    variable.type = "SINGLE_PROP"
    variable.targets[0].id = rig
    variable.targets[0].data_path = f'["{property_name}"]'
    driver.expression = "min(1.0,max(0.0,control))"
    return {
        "shapeKey": key.name,
        "driverProperty": property_name,
        "affectedVertices": affected,
        "maximumDisplacementMeters": maximum,
        "radiusMeters": radius,
        "activation": "explicit pose-space control property driven from the authored pose plan",
    }


def add_correctives(body: bpy.types.Object, rig: bpy.types.Object) -> list[dict[str, object]]:
    body.shape_key_add(name="Basis", from_mix=False)
    memberships = contract.vertex_face_sets(body.data)
    definitions = []
    for side, sign in (("Left", 1.0), ("Right", -1.0)):
        upper_arm = rig.data.bones[f"{side}UpperArm"]
        lower_arm = rig.data.bones[f"{side}LowerArm"]
        upper_leg = rig.data.bones[f"{side}UpperLeg"]
        lower_leg = rig.data.bones[f"{side}LowerLeg"]
        foot = rig.data.bones[f"{side}Foot"]
        definitions.extend(
            (
                (f"{side}Shoulder", upper_arm.head_local, upper_arm.tail_local - upper_arm.head_local, 0.12, 0.006, contract.SHOULDER_FACE_SETS | contract.UPPER_ARM_FACE_SETS),
                (f"{side}Elbow", lower_arm.head_local, lower_arm.tail_local - lower_arm.head_local, 0.085, 0.005, contract.UPPER_ARM_FACE_SETS | contract.FOREARM_FACE_SETS),
                (f"{side}Wrist", lower_arm.tail_local, lower_arm.tail_local - lower_arm.head_local, 0.065, 0.003, contract.FOREARM_FACE_SETS | contract.HAND_FACE_SETS),
                (f"{side}Hip", upper_leg.head_local, upper_leg.tail_local - upper_leg.head_local, 0.13, 0.006, contract.PELVIS_FACE_SETS | contract.THIGH_FACE_SETS),
                (f"{side}Knee", lower_leg.head_local, lower_leg.tail_local - lower_leg.head_local, 0.10, 0.005, contract.THIGH_FACE_SETS | contract.CALF_FACE_SETS),
                (f"{side}Ankle", foot.head_local, foot.tail_local - foot.head_local, 0.075, 0.003, contract.CALF_FACE_SETS | contract.FOOT_FACE_SETS),
            )
        )
    reports = []
    for name, pivot, axis, radius, magnitude, allowed in definitions:
        reports.append(radial_corrective(body, rig, memberships, name, pivot, axis, radius, magnitude, allowed, 1.0 if name.startswith("Left") else -1.0))
    return reports


def mesh_topology(obj: bpy.types.Object) -> dict[str, object]:
    obj.data.calc_loop_triangles()
    return {
        "vertices": len(obj.data.vertices),
        "polygons": len(obj.data.polygons),
        "triangles": len(obj.data.loop_triangles),
        "uvLayers": [layer.name for layer in obj.data.uv_layers],
        "shapeKeys": [key.name for key in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else [],
    }


def main() -> None:
    contract.require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    subset_manifest = verify_inputs()
    contract.OUTPUT_ROOT.mkdir(parents=True, exist_ok=True)
    contract.OUTPUTS.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(contract.RIG_BLEND), load_ui=False)
    canonical = bpy.data.objects.get(contract.CANONICAL_RIG_NAME)
    contract.require(canonical is not None and canonical.type == "ARMATURE", "Accepted canonical game rig is missing")
    contract.require(contract.CANONICAL_META_NAME in bpy.data.objects, "Accepted Rigify metarig is missing")
    contract.require(contract.CANONICAL_CONTROL_NAME in bpy.data.objects, "Accepted Rigify control rig is missing")
    canonical.name = "SessionHumanoidRigV2_AcceptedCanonical_NotExported"
    canonical.hide_render = True
    canonical.hide_set(True)
    bind_probe = bpy.data.objects.get("SessionHumanoidRigV2_BindProbe_NotExported")
    if bind_probe is not None:
        bpy.data.objects.remove(bind_probe, do_unlink=True)
    sources = append_sources()

    variant_reports: dict[str, object] = {}
    for variant in contract.VARIANTS:
        body, eyes, arms, conversion = prepare_variant(variant, sources)
        rig = fit_variant_rig(variant, canonical, body, eyes, arms)
        weights = paint_body_weights(body, rig)
        bind_mesh(body, rig)
        eye_report = bind_eyes(eyes, rig)
        correctives = add_correctives(body, rig)
        contract.reset_pose(rig)
        variant_reports[variant] = {
            "body": body.name,
            "eyes": [eye.name for eye in eyes],
            "rig": rig.name,
            "sourceObjects": list(contract.SOURCE_NAMES[variant]),
            "aPoseToTPose": conversion,
            "topology": mesh_topology(body),
            "weightPainting": weights,
            "eyeBinding": eye_report,
            "correctives": correctives,
            "rig": {
                "name": rig.name,
                "boneCount": len(rig.data.bones),
                "deformBoneCount": sum(bone.use_deform for bone in rig.data.bones),
                "twistBones": list(contract.TWIST_BONES),
                "legacySupportBonesWeighted": False,
                "sourceHumanoidMapping": "SessionHumanoidRigV2",
                "sourceRigifyControlObject": contract.CANONICAL_CONTROL_NAME,
            },
        }
    for source in sources.values():
        bpy.data.objects.remove(source, do_unlink=True)

    bpy.context.scene["productionBodySource"] = "Blender Foundation Human Base Meshes v1.4.1 stylized male/female"
    bpy.context.scene["quaterniusProductionLineage"] = False
    bpy.context.scene["mpfbProductionLineage"] = False
    bpy.context.scene["unityIntegrationAllowed"] = False
    bpy.ops.wm.save_as_mainfile(filepath=str(contract.BLEND_PATH), compress=True)

    manifest = {
        "schemaVersion": 1,
        "asset": "SessionHumanoidBaseV3",
        "status": "BUILT_AWAITING_INDEPENDENT_DEFORMATION_AND_MANUAL_REVIEW",
        "unityIntegrationAllowed": False,
        "source": {
            "selected": "Blender Foundation / Blender Studio Human Base Meshes v1.4.1 stylized bodies",
            "subsetBlend": str(contract.SUBSET_BLEND.relative_to(contract.PROJECT_ROOT)),
            "subsetBlendSha256": contract.file_sha256(contract.SUBSET_BLEND),
            "subsetManifestSha256": contract.file_sha256(contract.SUBSET_MANIFEST),
            "license": "CC0-1.0",
            "quaterniusProductionLineage": False,
            "mpfbProductionLineage": False,
        },
        "acceptedControlBaseline": {
            "rigBlend": str(contract.RIG_BLEND.relative_to(contract.PROJECT_ROOT)),
            "rigBlendSha256": contract.EXPECTED_RIG_BLEND_SHA256,
            "rigFbxSha256": contract.EXPECTED_RIG_FBX_SHA256,
            "rigManifestSha256": contract.EXPECTED_RIG_MANIFEST_SHA256,
            "humanoidMappingPreserved": True,
            "rigifyControlsRetainedForDccOnly": True,
        },
        "deformationArchitecture": {
            "standardTwistChains": list(contract.TWIST_BONES),
            "legacyAdHocElbowSupportsDeform": False,
            "maximumWeightsPerVertex": 4,
            "automaticWeightsUsed": False,
            "poseSpaceCorrectives": "sparse regional shape keys driven by explicit DCC pose-space controls",
        },
        "variants": variant_reports,
        "blend": str(contract.BLEND_PATH.relative_to(contract.PROJECT_ROOT)),
        "blendSha256": contract.file_sha256(contract.BLEND_PATH),
        "nextGate": "independent all-pose deformation proof on both variants",
        "fbxWritten": False,
        "texturesWritten": False,
        "lodsWritten": False,
        "blenderVersion": list(bpy.app.version),
    }
    contract.MANIFEST_PATH.write_text(
        json.dumps(manifest, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print("SESSION_HUMANOID_BASE_V3_BUILD_OK")


if __name__ == "__main__":
    main()
