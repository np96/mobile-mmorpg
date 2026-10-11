#!/usr/bin/env python3
"""Build the first-party, wrist-safe V3 animation proof on frozen RigV2.

V3 changes only original animator-control performances. It preserves the exact
68-joint RigV2 rest contract, explicitly binds Blender 5.2 Action slots, and
gates every integer frame. SessionHumanoidBaseV3 is pinned as the separately
accepted two-variant deformation body; this asset remains a skeleton-only source
proof until the manually authored bow/grip visual review passes.
"""

from __future__ import annotations

from dataclasses import replace
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Quaternion, Vector


SCRIPT_PATH = Path(__file__).resolve()
PROJECT_ROOT = SCRIPT_PATH.parents[3]
TOOLS_DIR = SCRIPT_PATH.parent
sys.path.insert(0, str(TOOLS_DIR))
import build_session_humanoid_animation_proof_v2 as v2
import build_session_humanoid_rig_v2 as rig_contract


ASSET_ID = "session-humanoid-animation-proof-v3"
SOURCE_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidAnimationProofV3"
OUTPUT_DIR = SOURCE_DIR / "Outputs"
SOURCE_BLEND = SOURCE_DIR / "SessionHumanoidAnimationProofV3.blend"
FBX_PATH = OUTPUT_DIR / "SessionHumanoidAnimationProofV3.fbx"
MANIFEST_PATH = OUTPUT_DIR / "SessionHumanoidAnimationProofV3.manifest.json"
SOURCE_REVIEW_REPORT_PATH = OUTPUT_DIR / "SessionHumanoidAnimationProofV3.source-review.json"
PROVENANCE_PATH = OUTPUT_DIR / "SessionHumanoidAnimationProofV3.PROVENANCE.md"
CLIP_CONTRACT_PATH = SOURCE_DIR / "SessionHumanoidAnimationProofV3.CLIPS.md"
PLAN_PATH = TOOLS_DIR / "session_humanoid_animation_proof_v3_control_plan.json"
VERIFIER_PATH = SCRIPT_PATH.with_name("verify_session_humanoid_animation_proof_v3.py")
EVIDENCE_DIR = PROJECT_ROOT.parent / "outputs/art/blender/session-humanoid-animation-proof-v3"
BODY_BOUNDARY_PATH = EVIDENCE_DIR / "SessionHumanoidAnimationProofV3BodyBoundary.json"
BODY_V3_BLEND = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidBaseV3/SessionHumanoidBaseV3.blend"
BODY_V3_MANIFEST = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidBaseV3/Outputs/SessionHumanoidBaseV3.manifest.json"
BODY_V3_PROOF = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidBaseV3/Outputs/Deformation/SessionHumanoidBaseV3DeformationProof.json"
BODY_V3_MANUAL_REVIEW = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidBaseV3/Outputs/Deformation/SessionHumanoidBaseV3.manual-review.json"
EXPECTED_BODY_V3_BLEND_SHA256 = "ff30baebeb3cb03186cf1663691a65c743af16616c82003f4358b5a41fb08458"
EXPECTED_BODY_V3_MANIFEST_SHA256 = "62aa4ce0ab1dc2caf554f1483bb38c2e01319f40ffa3940f6c6af60d995dd4ea"
EXPECTED_BODY_V3_PROOF_SHA256 = "ef085d15a7dca7e4a0e357c74dd13a60c77969dc1c5db9da9c1843109046e719"
EXPECTED_BODY_V3_MANUAL_REVIEW_SHA256 = "c6a08aca8b84c325031c8d9c85a4083e1de056ca2de2dfe8f9b5abef02c86f43"

RIG_BLEND = rig_contract.BLEND_PATH
RIG_MANIFEST = rig_contract.MANIFEST_PATH
RIG_FBX = rig_contract.FBX_PATH
EXPECTED_RIG_BLEND_SHA256 = "6268387e0e2c8db321b0b292954ebca94422b405c667a6f3ee5ecd8b49f6b783"
EXPECTED_RIG_FBX_SHA256 = "beb653aba9873141418d95b38cfda2151c1852e2e7b0df9aba2eeaf85c9eed66"
EXPECTED_RIG_MANIFEST_SHA256 = "7f8468fea292a1416c5d8ac697fd24652bba84e77808187647cf4eb5f3d3a6e6"

V2_SOURCE_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidAnimationProofV2"
V2_SOURCE_BLEND = V2_SOURCE_DIR / "SessionHumanoidAnimationProofV2.blend"
V2_FBX = V2_SOURCE_DIR / "Outputs/SessionHumanoidAnimationProofV2.fbx"
V2_MANIFEST = V2_SOURCE_DIR / "Outputs/SessionHumanoidAnimationProofV2.manifest.json"
EXPECTED_V2_SOURCE_BLEND_SHA256 = "564a194f078904f63aa541db6bfa478b5b88e8bb69944ca177aaeabdd1e30355"
EXPECTED_V2_FBX_SHA256 = "a0c88e7ca75e94e20227829846e052b66e8a31332eee8d44c6990187f2fa1fe7"
EXPECTED_V2_MANIFEST_SHA256 = "eea8ce8aa45f198fc4b54b80cf88d5f71c78e165897c3002df698e0c3783a043"

ControlTransform = v2.ControlTransform
KeyPose = v2.KeyPose
ClipSpec = v2.ClipSpec
CONTROL_ACTION_PREFIX = v2.CONTROL_ACTION_PREFIX
FPS = v2.FPS
GAME_RIG_NAME = v2.GAME_RIG_NAME
CONTROL_RIG_NAME = v2.CONTROL_RIG_NAME
IDENTITY = Matrix.Identity(4)

ARM_CONTROL_NAMES = (
    "shoulder.L", "shoulder.R", "hand_ik.L", "hand_ik.R",
    "upper_arm_ik_target.L", "upper_arm_ik_target.R",
)
UNCHANGED_GAME_BONES = (
    "Root", "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head", "Jaw",
    "LeftEye", "RightEye", "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
    "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
    "Socket_GroundFX", "Socket_PelvisFX", "Socket_FootFX_L", "Socket_FootFX_R",
)
UNCHANGED_V2_CLIPS = (
    "CombatIdle", "LocomotionForward", "Hit", "Death", "Interrupt",
)
REAUTHORED_HUNTER_CLIPS = (
    "HunterBowReady", "HunterBowDraw", "HunterBowHold", "HunterBowLoose", "HunterBowRecovery",
)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def configure_modules(clips: tuple[ClipSpec, ...]) -> None:
    values = {
        "ASSET_ID": ASSET_ID,
        "SOURCE_DIR": SOURCE_DIR,
        "OUTPUT_DIR": OUTPUT_DIR,
        "SOURCE_BLEND": SOURCE_BLEND,
        "FBX_PATH": FBX_PATH,
        "MANIFEST_PATH": MANIFEST_PATH,
        "PROVENANCE_PATH": PROVENANCE_PATH,
        "CLIP_CONTRACT_PATH": CLIP_CONTRACT_PATH,
        "VERIFIER_PATH": VERIFIER_PATH,
        "CLIPS": clips,
        "CLIP_BY_NAME": {clip.name: clip for clip in clips},
    }
    for module in (v2, v2.base):
        for name, value in values.items():
            setattr(module, name, value)
    v2.configure_base_animation_module()
    v2.CLIPS = clips
    v2.CLIP_BY_NAME = {clip.name: clip for clip in clips}
    v2.base.CLIPS = clips
    v2.base.CLIP_BY_NAME = v2.CLIP_BY_NAME
    v2.base.evaluate_action_samples = evaluate_action_samples_explicit


def transform_from_json(value: dict[str, object]) -> ControlTransform:
    position = value["position"]
    return ControlTransform(
        position=None if position is None else tuple(float(item) for item in position),
        offset=tuple(float(item) for item in value["offset"]),
        euler=tuple(float(item) for item in value["eulerXYZRadians"]),
    )


def apply_ordinary_override(
    controls: dict[str, ControlTransform],
    entry: dict[str, object],
) -> dict[str, ControlTransform]:
    result = dict(controls)
    for name in ("hand_ik.L", "hand_ik.R"):
        source = result[name]
        result[name] = ControlTransform(
            position=tuple(entry["handPosition"][name]),
            offset=source.offset,
            euler=tuple(entry["handEulerRadians"][name]),
        )
    for name in ("upper_arm_ik_target.L", "upper_arm_ik_target.R"):
        source = result[name]
        result[name] = ControlTransform(
            position=tuple(entry["polePosition"][name]),
            offset=source.offset,
            euler=source.euler,
        )
    for name in ("shoulder.L", "shoulder.R"):
        source = result[name]
        result[name] = ControlTransform(
            position=source.position,
            offset=tuple(entry["shoulderOffset"][name]),
            euler=source.euler,
        )
    return result


def build_clip_specs(plan: dict[str, object]) -> tuple[ClipSpec, ...]:
    original_by_name = {clip.name: clip for clip in v2.base.build_clip_specs()}
    ordinary = plan["ordinarySparseKeyOverrides"]
    bow_poses = plan["bowPoses"]
    bow_mapping = plan["bowSparseKeyMapping"]
    clips: list[ClipSpec] = []
    for clip_name in (clip.name for clip in v2.base.build_clip_specs()):
        original = original_by_name[clip_name]
        override_by_frame = {
            int(entry["frame"]): entry
            for entry in ordinary.get(clip_name, [])
        }
        mapping_by_frame = {
            int(frame): value for frame, value in bow_mapping.get(clip_name, {}).items()
        }
        keys: list[KeyPose] = []
        for key in original.keys:
            controls = dict(key.controls)
            if key.frame in override_by_frame:
                controls = apply_ordinary_override(controls, override_by_frame[key.frame])
            if key.frame in mapping_by_frame:
                mapping = mapping_by_frame[key.frame]
                pose_controls = {
                    name: transform_from_json(value)
                    for name, value in bow_poses[mapping["pose"]]["controls"].items()
                }
                names = tuple(pose_controls) if mapping["mode"] == "full" else ARM_CONTROL_NAMES
                for name in names:
                    controls[name] = pose_controls[name]
            require(set(controls) == set(v2.ANIMATED_CONTROLS), f"{clip_name} frame {key.frame} control set changed")
            keys.append(KeyPose(key.frame, controls))
        clips.append(replace(original, keys=tuple(keys)))
    result = tuple(clips)
    by_name = {clip.name: clip for clip in result}
    require(by_name["CombatIdle"].keys[0].controls == by_name["CombatIdle"].keys[-1].controls, "CombatIdle loop changed")
    require(by_name["LocomotionForward"].keys[0].controls == by_name["LocomotionForward"].keys[-1].controls, "Locomotion loop changed")
    require(by_name["HunterBowReady"].keys[0].controls == by_name["HunterBowReady"].keys[-1].controls, "BowReady loop changed")
    require(by_name["HunterBowHold"].keys[0].controls == by_name["HunterBowHold"].keys[-1].controls, "BowHold loop changed")
    transitions = (
        ("HunterBowReady", -1, "HunterBowDraw", 0),
        ("HunterBowDraw", -1, "HunterBowHold", 0),
        ("HunterBowHold", -1, "HunterBowLoose", 0),
        ("HunterBowLoose", -1, "HunterBowRecovery", 0),
        ("HunterBowRecovery", -1, "HunterBowReady", 0),
    )
    for first, first_index, second, second_index in transitions:
        require(by_name[first].keys[first_index].controls == by_name[second].keys[second_index].controls, f"{first}->{second} controls differ")
    return result


def bind_action_slot(
    rig: bpy.types.Object,
    action: bpy.types.Action,
    clip_name: str,
    *,
    imported_fbx: bool | None = None,
) -> dict[str, object]:
    rig.animation_data_create()
    slots = list(action.slots)
    require(len(slots) == 1, f"{clip_name} has {len(slots)} Action slots")
    slot = slots[0]
    require(slot.target_id_type == "OBJECT", f"{clip_name} slot targets {slot.target_id_type}")
    source_identifier = "OB" + rig.name
    expected_identifier = "OBSlot" if imported_fbx else source_identifier
    if imported_fbx is None:
        require(
            slot.identifier in {source_identifier, "OBSlot"},
            f"{clip_name} slot identifier is {slot.identifier}",
        )
    else:
        require(
            slot.identifier == expected_identifier,
            f"{clip_name} slot identifier is {slot.identifier}, expected {expected_identifier}",
        )
    rig.animation_data.action = action
    rig.animation_data.action_slot = slot
    require(rig.animation_data.action_slot == slot, f"{clip_name} Action slot did not bind")
    bpy.context.view_layer.update()
    return {
        "identifier": slot.identifier,
        "targetIdType": slot.target_id_type,
        "sourceOrImportedContract": (
            "imported_fbx_OBSlot" if slot.identifier == "OBSlot" else "source_object_specific_slot"
        ),
    }


def sample_control_action(
    game_rig: bpy.types.Object,
    control_rig: bpy.types.Object,
    action: bpy.types.Action,
    clip: ClipSpec,
) -> dict[int, dict[str, Matrix]]:
    bind_action_slot(control_rig, action, clip.name, imported_fbx=False)
    samples = {}
    for frame in range(clip.frame_start, clip.frame_end + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        samples[frame] = v2.sample_clean_game_pose(game_rig, control_rig)
    control_rig.animation_data.action = None
    v2.base.reset_control_pose(control_rig)
    bpy.context.view_layer.update()
    return samples


def evaluate_action_samples_explicit(
    game_rig: bpy.types.Object,
    action: bpy.types.Action,
    clip: ClipSpec,
) -> dict[int, dict[str, Vector]]:
    v2.base.set_constraints_muted(game_rig, True)
    bind_action_slot(game_rig, action, clip.name)
    samples = {}
    for frame in range(clip.frame_start, clip.frame_end + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        evaluated = game_rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
        samples[frame] = {
            name: evaluated.pose.bones[name].head.copy()
            for name in (
                "Root", "Hips", "Head", "LeftUpperArm", "RightUpperArm",
                "LeftLowerArm", "RightLowerArm", "LeftHand", "RightHand",
                "Socket_FootFX_L", "Socket_FootFX_R",
            )
        }
    game_rig.animation_data.action = None
    v2.base.set_constraints_muted(game_rig, False)
    bpy.context.view_layer.update()
    return samples


def canonical_quaternion(value: Quaternion) -> Quaternion:
    result = value.normalized()
    if result.w < 0.0:
        result.negate()
    return result


def swing_twist_degrees(value: Quaternion) -> tuple[float, float]:
    value = canonical_quaternion(value)
    twist = Quaternion((value.w, 0.0, value.y, 0.0))
    if twist.magnitude <= 1.0e-12:
        twist = Quaternion((1.0, 0.0, 0.0, 0.0))
    else:
        twist.normalize()
    swing = canonical_quaternion(value @ twist.inverted())
    twist_angle = 2.0 * math.atan2(twist.y, twist.w)
    while twist_angle > math.pi:
        twist_angle -= 2.0 * math.pi
    while twist_angle < -math.pi:
        twist_angle += 2.0 * math.pi
    return math.degrees(swing.angle), math.degrees(twist_angle)


def clamp_hand_swing_twist(
    value: Quaternion,
    maximum_swing_degrees: float,
    maximum_twist_degrees: float,
) -> tuple[Quaternion, float]:
    """Project one local hand rotation into the contracted wrist cone."""
    source = canonical_quaternion(value)
    twist = Quaternion((source.w, 0.0, source.y, 0.0))
    if twist.magnitude <= 1.0e-12:
        twist = Quaternion((1.0, 0.0, 0.0, 0.0))
    else:
        twist.normalize()
    swing = canonical_quaternion(source @ twist.inverted())
    swing_limit = math.radians(maximum_swing_degrees)
    if swing.angle > swing_limit:
        swing = Quaternion((1.0, 0.0, 0.0, 0.0)).slerp(swing, swing_limit / swing.angle)
    _swing_degrees, twist_degrees = swing_twist_degrees(source)
    clamped_twist_degrees = max(-maximum_twist_degrees, min(maximum_twist_degrees, twist_degrees))
    twist = Quaternion(Vector((0.0, 1.0, 0.0)), math.radians(clamped_twist_degrees))
    result = canonical_quaternion(swing @ twist)
    return result, math.degrees(source.rotation_difference(result).angle)


def apply_wrist_safety_projection(
    game_rig: bpy.types.Object,
    sampled_by_clip: dict[str, dict[int, dict[str, Matrix]]],
    plan: dict[str, object],
) -> dict[str, object]:
    """Clamp hand local rotations and carry each delta through its descendants.

    The arm/hand joint positions are untouched, so draw and release travel cannot
    be manufactured by this projection.  Finger and socket local bases are also
    preserved because the same armature-space delta is applied to the full hand
    subtree.
    """
    margin = 0.25
    swing_limit = float(plan["orientationLimitsDegrees"]["maximumHandSwing"]) - margin
    twist_limit = float(plan["orientationLimitsDegrees"]["maximumAbsoluteHandTwist"]) - margin
    per_clip: dict[str, object] = {}
    corrected_total = 0
    maximum_correction = 0.0
    for clip in CLIPS:
        corrected = 0
        side_counts = {"Left": 0, "Right": 0}
        clip_maximum = 0.0
        for frame, sampled in sampled_by_clip[clip.name].items():
            for side in ("Left", "Right"):
                hand_name = side + "Hand"
                hand_bone = game_rig.data.bones[hand_name]
                basis = v2.local_basis_from_sample(game_rig, sampled, hand_name)
                location, rotation, scale = basis.decompose()
                clamped, correction = clamp_hand_swing_twist(rotation, swing_limit, twist_limit)
                if correction <= 1.0e-5:
                    continue
                parent = hand_bone.parent
                require(parent is not None, f"{hand_name} unexpectedly has no parent")
                placement = sampled[parent.name] @ parent.matrix_local.inverted() @ hand_bone.matrix_local
                old_hand = sampled[hand_name].copy()
                new_hand = placement @ Matrix.LocRotScale(location, clamped, scale)
                delta = new_hand @ old_hand.inverted()
                descendants = [
                    bone.name
                    for bone in game_rig.data.bones
                    if bone == hand_bone or hand_bone in bone.parent_recursive
                ]
                for name in descendants:
                    sampled[name] = delta @ sampled[name]
                corrected += 1
                side_counts[side] += 1
                clip_maximum = max(clip_maximum, correction)
        corrected_total += corrected
        maximum_correction = max(maximum_correction, clip_maximum)
        per_clip[clip.name] = {
            "correctedHandFrames": corrected,
            "correctedBySide": side_counts,
            "maximumAngularCorrectionDegrees": clip_maximum,
        }
    return {
        "method": "local Hand swing/twist cone projection with armature-space delta propagated through each hand subtree",
        "positionChanges": False,
        "fingerAndSocketLocalBasesPreserved": True,
        "maximumSwingDegreesAfterMargin": swing_limit,
        "maximumAbsoluteTwistDegreesAfterMargin": twist_limit,
        "correctedHandFrames": corrected_total,
        "maximumAngularCorrectionDegrees": maximum_correction,
        "perClip": per_clip,
    }


def apply_hunter_temporal_hand_filter(
    game_rig: bpy.types.Object,
    sampled_by_clip: dict[str, dict[int, dict[str, Matrix]]],
    plan: dict[str, object],
) -> dict[str, object]:
    """Limit only Hunter hand rotation steps while leaving all joint positions intact."""
    step_limit = float(plan["bowContract"]["maximumHandRotationStepDegrees"]) - 0.25
    swing_limit = float(plan["orientationLimitsDegrees"]["maximumHandSwing"]) - 0.25
    twist_limit = float(plan["orientationLimitsDegrees"]["maximumAbsoluteHandTwist"]) - 0.25
    hunter_clips = (
        "HunterBowReady", "HunterBowDraw", "HunterBowHold", "HunterBowLoose", "HunterBowRecovery"
    )
    corrected = 0
    maximum_pre_filter_step = 0.0
    per_clip = {}
    for clip_name in hunter_clips:
        samples = sampled_by_clip[clip_name]
        clip_counts = {"Left": 0, "Right": 0}
        clip_maximum = 0.0
        for side in ("Left", "Right"):
            hand_name = side + "Hand"
            hand_bone = game_rig.data.bones[hand_name]
            previous = None
            for frame in sorted(samples):
                sampled = samples[frame]
                basis = v2.local_basis_from_sample(game_rig, sampled, hand_name)
                location, rotation, scale = basis.decompose()
                rotation.normalize()
                if previous is None:
                    previous = rotation
                    continue
                step = math.degrees(previous.rotation_difference(rotation).angle)
                clip_maximum = max(clip_maximum, step)
                maximum_pre_filter_step = max(maximum_pre_filter_step, step)
                if step <= step_limit + 1.0e-6:
                    previous = rotation
                    continue
                filtered = previous.slerp(rotation, step_limit / step).normalized()
                filtered, _correction = clamp_hand_swing_twist(filtered, swing_limit, twist_limit)
                parent = hand_bone.parent
                require(parent is not None, f"{hand_name} unexpectedly has no parent")
                placement = sampled[parent.name] @ parent.matrix_local.inverted() @ hand_bone.matrix_local
                old_hand = sampled[hand_name].copy()
                new_hand = placement @ Matrix.LocRotScale(location, filtered, scale)
                delta = new_hand @ old_hand.inverted()
                for bone in game_rig.data.bones:
                    if bone == hand_bone or hand_bone in bone.parent_recursive:
                        sampled[bone.name] = delta @ sampled[bone.name]
                previous = filtered
                corrected += 1
                clip_counts[side] += 1
        per_clip[clip_name] = {
            "correctedBySide": clip_counts,
            "maximumPreFilterStepDegrees": clip_maximum,
        }
    return {
        "method": "forward shortest-arc local-Hand quaternion step filter on Hunter bow clips",
        "maximumStepDegreesAfterMargin": step_limit,
        "positionChanges": False,
        "fingerAndSocketLocalBasesPreserved": True,
        "correctedHandFrames": corrected,
        "maximumPreFilterStepDegrees": maximum_pre_filter_step,
        "perClip": per_clip,
    }


def elbow_flex_degrees(shoulder: Vector, elbow: Vector, hand: Vector) -> float:
    return 180.0 - math.degrees((shoulder - elbow).angle(hand - elbow))


def collect_kinematic_metrics(
    game_rig: bpy.types.Object,
    actions: dict[str, bpy.types.Action],
    plan: dict[str, object],
) -> dict[str, object]:
    v2.base.set_constraints_muted(game_rig, True)
    limits = plan["orientationLimitsDegrees"]
    maximum_swing = {"Left": 0.0, "Right": 0.0}
    maximum_twist = {"Left": 0.0, "Right": 0.0}
    worst = {"Left": {}, "Right": {}}
    motion = {}
    maximum_hand_step = {}
    bow_rows = []
    total_frames = 0
    for clip in CLIPS:
        action = actions[clip.name]
        binding = bind_action_slot(game_rig, action, clip.name)
        first_matrices = None
        maximum_motion = 0.0
        moving_bones = set()
        previous_hand_rotation = {"Left": None, "Right": None}
        clip_hand_step = {"Left": 0.0, "Right": 0.0}
        for frame in range(clip.frame_start, clip.frame_end + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            evaluated = game_rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
            current = {bone.name: bone.matrix.copy() for bone in evaluated.pose.bones}
            if first_matrices is None:
                first_matrices = current
            else:
                for name, matrix in current.items():
                    delta = max(
                        abs(float(matrix[row][column]) - float(first_matrices[name][row][column]))
                        for row in range(4) for column in range(4)
                    )
                    maximum_motion = max(maximum_motion, delta)
                    if delta > 1.0e-5:
                        moving_bones.add(name)
            frame_row = {"clip": clip.name, "frame": frame, "sides": {}}
            for side in ("Left", "Right"):
                basis = v2.evaluated_local_basis(game_rig, evaluated, side + "Hand")
                rotation = basis.to_quaternion().normalized()
                swing, twist = swing_twist_degrees(rotation)
                if previous_hand_rotation[side] is not None:
                    step = math.degrees(previous_hand_rotation[side].rotation_difference(rotation).angle)
                    clip_hand_step[side] = max(clip_hand_step[side], step)
                previous_hand_rotation[side] = rotation
                if swing > maximum_swing[side]:
                    maximum_swing[side] = swing
                    worst[side]["swing"] = {"clip": clip.name, "frame": frame, "degrees": swing}
                if abs(twist) > maximum_twist[side]:
                    maximum_twist[side] = abs(twist)
                    worst[side]["twist"] = {"clip": clip.name, "frame": frame, "degrees": twist}
                hand_head = evaluated.pose.bones[side + "Hand"].head.copy()
                frame_row["sides"][side] = {
                    "handSwingDegrees": swing,
                    "handTwistDegrees": twist,
                    "handHead": [float(value) for value in hand_head],
                }
                require(swing <= limits["maximumHandSwing"] + 1.0e-4, f"{clip.name} frame {frame} {side} hand swing {swing}")
                require(abs(twist) <= limits["maximumAbsoluteHandTwist"] + 1.0e-4, f"{clip.name} frame {frame} {side} hand twist {twist}")
            if clip.name in {"HunterBowReady", "HunterBowDraw", "HunterBowHold", "HunterBowLoose"}:
                left = evaluated.pose.bones["LeftHand"].head.copy()
                right = evaluated.pose.bones["RightHand"].head.copy()
                shoulder = evaluated.pose.bones["RightUpperArm"].head.copy()
                elbow = evaluated.pose.bones["RightLowerArm"].head.copy()
                frame_row.update({
                    "handSeparationMeters": (left - right).length,
                    "rightHandX": right.x,
                    "rightElbowFlexDegrees": elbow_flex_degrees(shoulder, elbow, right),
                })
                require(right.x < 0.0, f"{clip.name} frame {frame} draw hand crossed center: {right.x}")
                require(frame_row["rightElbowFlexDegrees"] <= plan["bowContract"]["maximumRightElbowFlexDegrees"] + 1.0e-4, f"{clip.name} frame {frame} elbow flex {frame_row['rightElbowFlexDegrees']}")
                bow_rows.append(frame_row)
            total_frames += 1
        require(maximum_motion > 1.0e-5 and moving_bones, f"{clip.name} evaluated as a rest-pose repeat")
        motion[clip.name] = {
            "binding": binding,
            "maximumBoneMatrixComponentDeltaFromFirstFrame": maximum_motion,
            "movingBoneCount": len(moving_bones),
            "movingBones": sorted(moving_bones),
        }
        maximum_hand_step[clip.name] = clip_hand_step
    game_rig.animation_data.action = None
    v2.base.set_constraints_muted(game_rig, False)
    bpy.context.view_layer.update()
    full_draw = next(row for row in bow_rows if row["clip"] == "HunterBowDraw" and row["frame"] == 20)
    full_draw_key = next(key for key in CLIP_BY_NAME["HunterBowDraw"].keys if key.frame == 20)
    full_draw["handTargetErrorMeters"] = {
        side: (
            Vector(full_draw_key.controls["hand_ik." + ("L" if side == "Left" else "R")].position)
            - Vector(full_draw["sides"][side]["handHead"])
        ).length
        for side in ("Left", "Right")
    }
    require(
        max(full_draw["handTargetErrorMeters"].values())
        <= plan["bowContract"]["maximumHandTargetErrorMeters"] + 1.0e-6,
        f"Full-draw hand target error is {full_draw['handTargetErrorMeters']}",
    )
    require(full_draw["handSeparationMeters"] >= plan["bowContract"]["minimumFullDrawHandSeparationMeters"], "Full draw is too narrow")
    minimum_separation = min(row["handSeparationMeters"] for row in bow_rows)
    require(minimum_separation >= plan["bowContract"]["minimumFullDrawHandSeparationMeters"], f"Bow sequence narrows to {minimum_separation}")
    bow_clips = {
        "HunterBowReady", "HunterBowDraw", "HunterBowHold", "HunterBowLoose", "HunterBowRecovery"
    }
    bow_maximum_step = max(
        value
        for clip_name, sides in maximum_hand_step.items()
        if clip_name in bow_clips
        for value in sides.values()
    )
    require(
        bow_maximum_step <= plan["bowContract"]["maximumHandRotationStepDegrees"] + 1.0e-4,
        f"Hunter bow hand rotation changes {bow_maximum_step} degrees in one frame",
    )
    return {
        "status": "PASS_ALL_248_FRAMES_EXPLICIT_ACTION_SLOT_KINEMATICS",
        "sampledFrames": total_frames,
        "maximumHandSwingDegrees": maximum_swing,
        "maximumAbsoluteHandTwistDegrees": maximum_twist,
        "orientationLimitsDegrees": limits,
        "worstOrientationFrames": worst,
        "evaluatedMotionByClip": motion,
        "maximumHandRotationStepDegreesByClip": maximum_hand_step,
        "maximumHunterBowHandRotationStepDegrees": bow_maximum_step,
        "fullDraw": full_draw,
        "minimumBowSequenceHandSeparationMeters": minimum_separation,
        "maximumBowSequenceRightElbowFlexDegrees": max(row["rightElbowFlexDegrees"] for row in bow_rows),
        "rightDrawHandStayedOnNegativeXSide": all(row["rightHandX"] < 0.0 for row in bow_rows),
        "manualWeaponGripVisualGate": "NOT_RUN",
    }


def collect_support_metrics_explicit(
    game_rig: bpy.types.Object,
    actions: dict[str, bpy.types.Action],
) -> dict[str, object]:
    v2.base.set_constraints_muted(game_rig, True)
    maximum_error = 0.0
    maximum_pivot = 0.0
    maximum_angle = 0.0
    maximum_translation = 0.0
    maximum_scale = 0.0
    per_clip = {}
    for clip in CLIPS:
        bind_action_slot(game_rig, actions[clip.name], clip.name)
        clip_error = 0.0
        clip_pivot = 0.0
        clip_angle = 0.0
        for frame in range(clip.frame_start, clip.frame_end + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            evaluated = game_rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
            for side in ("Left", "Right"):
                lower = v2.evaluated_local_basis(game_rig, evaluated, side + "LowerArm")
                support = v2.evaluated_local_basis(game_rig, evaluated, side + "ElbowSupport")
                location, rotation, scale = support.decompose()
                error = v2.quaternion_error(rotation, v2.half_local_rotation(lower))
                pivot = (evaluated.pose.bones[side + "ElbowSupport"].head - evaluated.pose.bones[side + "LowerArm"].head).length
                clip_error = max(clip_error, error)
                clip_pivot = max(clip_pivot, pivot)
                clip_angle = max(clip_angle, rotation.normalized().angle)
                maximum_translation = max(maximum_translation, location.length)
                maximum_scale = max(maximum_scale, max(abs(value - 1.0) for value in scale))
        maximum_error = max(maximum_error, clip_error)
        maximum_pivot = max(maximum_pivot, clip_pivot)
        maximum_angle = max(maximum_angle, clip_angle)
        per_clip[clip.name] = {
            "maximumMidpointAngularErrorRadians": clip_error,
            "maximumElbowPivotSeparationMeters": clip_pivot,
            "maximumSupportLocalAngleRadians": clip_angle,
        }
    game_rig.animation_data.action = None
    v2.base.set_constraints_muted(game_rig, False)
    bpy.context.view_layer.update()
    require(maximum_error <= 0.00002, f"Support midpoint error {maximum_error}")
    require(maximum_pivot <= 0.00002, f"Support pivot error {maximum_pivot}")
    require(maximum_translation <= 0.00002, f"Support translation {maximum_translation}")
    require(maximum_scale <= 0.00002, f"Support scale {maximum_scale}")
    require(maximum_angle >= 0.10, "Support curves contain no useful motion")
    return {
        "rule": "shortest-arc slerp(identity local pose, LowerArm local pose, 0.5)",
        "maximumMidpointAngularErrorRadians": maximum_error,
        "maximumElbowPivotSeparationMeters": maximum_pivot,
        "maximumSupportLocalAngleRadians": maximum_angle,
        "maximumSupportLocalTranslationMeters": maximum_translation,
        "maximumSupportScaleDeviation": maximum_scale,
        "supportsShowAuthoredMotion": True,
        "perClip": per_clip,
        "runtimeAuthority": "future explicit derivative/runtime support driver; not AnimationProofV3",
    }


def create_baked_action_v3(
    game_rig: bpy.types.Object,
    clip: ClipSpec,
    sampled: dict[int, dict[str, Matrix]],
) -> bpy.types.Action:
    action = v2.create_baked_action(game_rig, clip, sampled)
    action["sessionRole"] = "clean_68_joint_game_bake_v3_wrist_safe_kinematic_proof"
    action["kinematicBodyBoundary"] = "SessionHumanoidBaseV3 passed separate two-variant deformation proof; no Unity body promoted"
    action["actionSlotEvaluationContract"] = "explicit OBJECT slot binding; imported FBX identifier OBSlot"
    return action


def assert_frozen_inputs() -> tuple[dict[str, object], dict[str, object]]:
    expected = (
        (RIG_BLEND, EXPECTED_RIG_BLEND_SHA256),
        (RIG_FBX, EXPECTED_RIG_FBX_SHA256),
        (RIG_MANIFEST, EXPECTED_RIG_MANIFEST_SHA256),
        (V2_SOURCE_BLEND, EXPECTED_V2_SOURCE_BLEND_SHA256),
        (V2_FBX, EXPECTED_V2_FBX_SHA256),
        (V2_MANIFEST, EXPECTED_V2_MANIFEST_SHA256),
        (BODY_V3_BLEND, EXPECTED_BODY_V3_BLEND_SHA256),
        (BODY_V3_MANIFEST, EXPECTED_BODY_V3_MANIFEST_SHA256),
        (BODY_V3_PROOF, EXPECTED_BODY_V3_PROOF_SHA256),
        (BODY_V3_MANUAL_REVIEW, EXPECTED_BODY_V3_MANUAL_REVIEW_SHA256),
    )
    for path, digest in expected:
        require(path.is_file() and sha256(path) == digest, f"Frozen input changed: {path}")
    return (
        json.loads(RIG_MANIFEST.read_text(encoding="utf-8")),
        json.loads(V2_MANIFEST.read_text(encoding="utf-8")),
    )


def write_docs(metrics: dict[str, object]) -> None:
    rows = [
        f"| `{clip.name}` | {clip.frame_start}-{clip.frame_end} | {'yes' if clip.loop else 'no'} | `{clip.category}` |"
        for clip in CLIPS
    ]
    CLIP_CONTRACT_PATH.write_text(
        "# Session Humanoid Animation Proof V3 clip contract\n\n"
        "Status: **technical first-party kinematic proof on frozen RigV2; separate SessionHumanoidBaseV3 two-variant deformation gate passed; manual bow-grip art gate not run; not integrated into Unity**.\n\n"
        "- All ten clips retain their V2 names, ranges, 30 fps timing, identity Root, and 68-joint bake.\n"
        "- Every action is explicitly bound before evaluation: source-created actions require their exact object-specific `OBJECT` slot, while imported FBX actions require exact `OBSlot`/`OBJECT`.\n"
        "- Both game hands are gated on every one of 248 integer frames at no more than 45 degrees swing and 30 degrees absolute local-Y twist.\n"
        "- Bow ready/draw/hold/loose use original same-side-jaw/string-hand poses, explicit grip orientations, and unchanged 0.30 m draw/release gates.\n"
        "- Baked hand rotations receive a disclosed local swing/twist safety projection; hand positions, finger/socket local bases, and draw/release travel are unchanged by it.\n"
        "- SessionHumanoidBaseV3 separately passed 24 numeric deformation plates and manual review. No body or animation has been promoted into Unity.\n\n"
        "| Clip | Frames | Loop | Purpose |\n|---|---:|---|---|\n"
        + "\n".join(rows)
        + "\n\n## Technical metrics\n\n```json\n"
        + json.dumps(metrics, indent=2, sort_keys=True)
        + "\n```\n",
        encoding="utf-8",
    )
    PROVENANCE_PATH.write_text(
        "# Session Humanoid Animation Proof V3 provenance\n\n"
        "Status: **first-party technical kinematic proof; separate BodyV3 deformation accepted; not Unity acceptance**.\n\n"
        "The tracked numeric control plan and this builder are original project sources. No downloaded, captured, legacy, Quaternius, Foundation, MPFB, or retargeted animation is sampled. The unchanged project-owned SessionHumanoidRigV2 is the sole skeleton dependency.\n\n"
        f"Frozen RigV2 FBX SHA-256: `{EXPECTED_RIG_FBX_SHA256}`.\n\n"
        f"Frozen AnimationProofV2 FBX SHA-256: `{EXPECTED_V2_FBX_SHA256}`. V2 remains untouched and is retained only as the semantic baseline.\n\n"
        "The independent bow study established a same-side-jaw sequence, then the source plan captured only its first-party numeric controls. SessionHumanoidBaseV3 now supplies the separately accepted Blender Foundation anatomy, conventional twist chains, manual compact weights, and sparse correctives. The animation remains a clean skeleton source until the weapon-in-hand visual gate is reviewed.\n",
        encoding="utf-8",
    )


def write_body_boundary(plan: dict[str, object]) -> None:
    report = {
        "schemaVersion": 1,
        "status": "PASS_SEPARATE_SESSION_HUMANOID_BASE_V3_TWO_VARIANT_DEFORMATION_GATE",
        "animationAsset": ASSET_ID,
        "acceptedBody": {
            "blend": str(BODY_V3_BLEND.relative_to(PROJECT_ROOT)),
            "blendSha256": EXPECTED_BODY_V3_BLEND_SHA256,
            "manifestSha256": EXPECTED_BODY_V3_MANIFEST_SHA256,
            "deformationProofSha256": EXPECTED_BODY_V3_PROOF_SHA256,
            "manualReviewSha256": EXPECTED_BODY_V3_MANUAL_REVIEW_SHA256,
            "numericPosesPassed": 24,
            "manualStatus": "PASS_MANUAL_DEFORMATION_REVIEW",
        },
        "noBodyOutputsPromoted": True,
        "noUnityAssetsWritten": True,
        "manualWeaponGripVisualGate": plan["bowContract"]["manualWeaponGripVisualGate"],
        "nextSafeStep": "Render the baked Hunter sequence on the accepted BodyV3 variants, then review it with the manually authored production bow before any surfacing, FBX promotion, or Unity integration.",
    }
    EVIDENCE_DIR.mkdir(parents=True, exist_ok=True)
    BODY_BOUNDARY_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def patch_manifest(
    rig_manifest: dict[str, object],
    v2_manifest: dict[str, object],
    plan: dict[str, object],
    metrics: dict[str, object],
) -> None:
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    manifest.update({
        "assetId": ASSET_ID,
        "status": "PASS_TECHNICAL_KINEMATIC_PROOF_BODY_V3_ACCEPTED_MANUAL_GRIP_PENDING",
        "integrationStatus": "SOURCE_DCC_NOT_IN_UNITY",
        "bodyDeformationStatus": "PASS_SEPARATE_SESSION_HUMANOID_BASE_V3_24_POSE_PROOF",
        "manualWeaponGripVisualGate": "NOT_RUN",
    })
    manifest["sourceRig"].update({
        "assetId": rig_manifest["assetId"],
        "manifestSha256": EXPECTED_RIG_MANIFEST_SHA256,
        "blendSha256": EXPECTED_RIG_BLEND_SHA256,
        "fbxSha256": EXPECTED_RIG_FBX_SHA256,
        "exportedJointCount": 68,
    })
    manifest["authoring"].update({
        "controlPlanPath": str(PLAN_PATH.relative_to(PROJECT_ROOT)),
        "controlPlanSha256": sha256(PLAN_PATH),
        "explicitBlender52ActionSlotBinding": True,
        "sourceActionSlotIdentifier": "OB<exact target object name>",
        "importedFbxActionSlotIdentifier": "OBSlot",
        "actionSlotTargetIdType": "OBJECT",
        "ordinaryHandOrientationTarget": "bounded game-Hand local basis",
        "bowPoseSource": "original same-side-jaw pose sequence with explicit hand IK orientation and arm poles",
    })
    manifest["technicalMetrics"] = metrics
    manifest["kinematicGate"] = metrics["kinematics"]
    manifest["v2Baseline"] = {
        "sourceBlendPath": str(V2_SOURCE_BLEND.relative_to(PROJECT_ROOT)),
        "sourceBlendSha256": EXPECTED_V2_SOURCE_BLEND_SHA256,
        "fbxPath": str(V2_FBX.relative_to(PROJECT_ROOT)),
        "fbxSha256": EXPECTED_V2_FBX_SHA256,
        "manifestPath": str(V2_MANIFEST.relative_to(PROJECT_ROOT)),
        "manifestSha256": EXPECTED_V2_MANIFEST_SHA256,
        "filesWrittenByV3Build": False,
        "sameRigRestAnd68JointHierarchy": True,
        "sameTenClipNamesRangesAndTiming": True,
        "unaffectedBoneSemanticComparisonRequired": list(UNCHANGED_GAME_BONES),
        "unaffectedClipSemanticComparisonRequired": list(UNCHANGED_V2_CLIPS),
        "hunterClipsIntentionallyReauthored": list(REAUTHORED_HUNTER_CLIPS),
        "wristSafetyProjectionMayChangeHandSubtreesInAllClips": True,
    }
    manifest["acceptedBodyV3"] = {
        "blendPath": str(BODY_V3_BLEND.relative_to(PROJECT_ROOT)),
        "blendSha256": EXPECTED_BODY_V3_BLEND_SHA256,
        "manifestPath": str(BODY_V3_MANIFEST.relative_to(PROJECT_ROOT)),
        "manifestSha256": EXPECTED_BODY_V3_MANIFEST_SHA256,
        "deformationProofPath": str(BODY_V3_PROOF.relative_to(PROJECT_ROOT)),
        "deformationProofSha256": EXPECTED_BODY_V3_PROOF_SHA256,
        "manualReviewPath": str(BODY_V3_MANUAL_REVIEW.relative_to(PROJECT_ROOT)),
        "manualReviewSha256": EXPECTED_BODY_V3_MANUAL_REVIEW_SHA256,
        "status": "PASS_SEPARATE_TWO_VARIANT_BODY_GATE",
    }
    manifest["exportContract"].update({
        "fbxJointCount": 68,
        "fbxActions": 10,
        "boneFcurveCountPerAction": 680,
        "importedArmatureObjectTransformFcurveCountPerAction": 9,
        "expectedImportedTotalFcurveCountPerAction": 689,
        "explicitActionSlotBindingRequiredForEveryEvaluation": True,
        "all248FramesHandOrientationGated": True,
    })
    manifest["sourceFiles"] = {
        str(SCRIPT_PATH.relative_to(PROJECT_ROOT)): sha256(SCRIPT_PATH),
        str(VERIFIER_PATH.relative_to(PROJECT_ROOT)): sha256(VERIFIER_PATH),
        str(PLAN_PATH.relative_to(PROJECT_ROOT)): sha256(PLAN_PATH),
        str(CLIP_CONTRACT_PATH.relative_to(PROJECT_ROOT)): sha256(CLIP_CONTRACT_PATH),
        str(PROVENANCE_PATH.relative_to(PROJECT_ROOT)): sha256(PROVENANCE_PATH),
        str(Path(v2.__file__).resolve().relative_to(PROJECT_ROOT)): sha256(Path(v2.__file__).resolve()),
        str(Path(v2.base.__file__).resolve().relative_to(PROJECT_ROOT)): sha256(Path(v2.base.__file__).resolve()),
        str(RIG_MANIFEST.relative_to(PROJECT_ROOT)): EXPECTED_RIG_MANIFEST_SHA256,
        str(RIG_FBX.relative_to(PROJECT_ROOT)): EXPECTED_RIG_FBX_SHA256,
        str(V2_MANIFEST.relative_to(PROJECT_ROOT)): EXPECTED_V2_MANIFEST_SHA256,
        str(V2_FBX.relative_to(PROJECT_ROOT)): EXPECTED_V2_FBX_SHA256,
        str(BODY_V3_MANIFEST.relative_to(PROJECT_ROOT)): EXPECTED_BODY_V3_MANIFEST_SHA256,
        str(BODY_V3_PROOF.relative_to(PROJECT_ROOT)): EXPECTED_BODY_V3_PROOF_SHA256,
        str(BODY_V3_MANUAL_REVIEW.relative_to(PROJECT_ROOT)): EXPECTED_BODY_V3_MANUAL_REVIEW_SHA256,
    }
    manifest["outputHashes"] = {
        SOURCE_BLEND.name: sha256(SOURCE_BLEND),
        FBX_PATH.name: sha256(FBX_PATH),
        PROVENANCE_PATH.name: sha256(PROVENANCE_PATH),
        str(BODY_BOUNDARY_PATH.relative_to(PROJECT_ROOT.parent)): sha256(BODY_BOUNDARY_PATH),
    }
    manifest["approvalBoundary"] = {
        "underUnityAssets": False,
        "unityIntegrationPerformed": False,
        "bodyOutputPromoted": False,
        "bodyDeformationArchitectureAccepted": True,
        "manualWeaponGripVisualGate": False,
        "nextStep": "Render on accepted BodyV3 and review the manually authored bow grip and Hunter macro form before any Unity promotion.",
    }
    manifest["frozenV2ManifestStatus"] = v2_manifest["status"]
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    require("Assets" not in SOURCE_DIR.parts, "AnimationProofV3 must remain outside Unity Assets")
    require(PLAN_PATH.is_file() and VERIFIER_PATH.is_file(), "V3 plan/verifier is missing")
    rig_manifest, v2_manifest = assert_frozen_inputs()
    plan = json.loads(PLAN_PATH.read_text(encoding="utf-8"))
    require(plan["schemaVersion"] == 1 and plan["assetId"].endswith("v3-control-plan"), "Wrong V3 control plan")
    clips = build_clip_specs(plan)
    global CLIPS, CLIP_BY_NAME
    CLIPS = clips
    CLIP_BY_NAME = {clip.name: clip for clip in clips}
    configure_modules(clips)
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    game_rig, control_rig, loaded_rig_manifest = v2.load_rig_source_v2()
    require(loaded_rig_manifest["assetId"] == rig_manifest["assetId"], "Loaded wrong rig")
    control_actions = {}
    sampled_by_clip = {}
    for clip in clips:
        action = v2.base.create_control_action(control_rig, clip)
        control_actions[clip.name] = action
        sampled_by_clip[clip.name] = sample_control_action(game_rig, control_rig, action, clip)
    wrist_safety = apply_wrist_safety_projection(game_rig, sampled_by_clip, plan)
    hunter_temporal_filter = apply_hunter_temporal_hand_filter(game_rig, sampled_by_clip, plan)
    v2.base.stabilize_intentionally_identical_endpoints(sampled_by_clip)
    baked_actions = {
        clip.name: create_baked_action_v3(game_rig, clip, sampled_by_clip[clip.name])
        for clip in clips
    }
    standard_metrics, _ = v2.base.collect_action_metrics(game_rig, baked_actions)
    v2.base.validate_metrics(standard_metrics)
    bow_metrics = standard_metrics["hunterBowSequence"]
    require(
        bow_metrics["drawHandTravelMeters"] >= plan["bowContract"]["minimumDrawHandTravelMeters"],
        "V3 draw travel fell below its explicit unchanged plan gate",
    )
    require(
        bow_metrics["looseDrawHandTravelMeters"] >= plan["bowContract"]["minimumReleaseHandTravelMeters"],
        "V3 release travel fell below its explicit unchanged plan gate",
    )
    kinematics = collect_kinematic_metrics(game_rig, baked_actions, plan)
    support = collect_support_metrics_explicit(game_rig, baked_actions)
    metrics = dict(standard_metrics)
    metrics["bendSupport"] = support
    metrics["kinematics"] = kinematics
    metrics["wristSafetyProjection"] = wrist_safety
    metrics["hunterTemporalHandFilter"] = hunter_temporal_filter
    control_reports = {name: v2.base.action_curve_report(action) for name, action in control_actions.items()}
    baked_reports = {name: v2.base.action_curve_report(action) for name, action in baked_actions.items()}
    require(all(report["keyedBoneCount"] == 68 for report in baked_reports.values()), "A V3 action is not a 68-joint bake")
    if "--source-only-review" in sys.argv:
        v2.base.save_source_blend(game_rig, control_rig)
        source_review = {
            "schemaVersion": 1,
            "assetId": ASSET_ID,
            "status": "PASS_SOURCE_ONLY_KINEMATIC_PROOF_PENDING_BODY_MOTION_REVIEW",
            "sourceBlend": str(SOURCE_BLEND.relative_to(PROJECT_ROOT)),
            "sourceBlendSha256": sha256(SOURCE_BLEND),
            "controlPlan": str(PLAN_PATH.relative_to(PROJECT_ROOT)),
            "controlPlanSha256": sha256(PLAN_PATH),
            "builderSha256": sha256(SCRIPT_PATH),
            "technicalMetrics": metrics,
            "sourceActions": len(control_actions),
            "bakedActions": len(baked_actions),
            "jointCount": 68,
            "sampledFrames": kinematics["sampledFrames"],
            "unchangedMinimumDrawAndReleaseGateMeters": plan["bowContract"]["minimumDrawHandTravelMeters"],
            "fbxWrittenByThisInvocation": False,
            "fbxRoundTripPerformedByThisInvocation": False,
            "unityIntegrationPerformed": False,
            "nextGate": "two-variant body-on-motion visual/deformation review, then manual Hunter macro-form",
        }
        OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
        SOURCE_REVIEW_REPORT_PATH.write_text(
            json.dumps(source_review, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        print("SESSION_HUMANOID_ANIMATION_PROOF_V3_SOURCE_REVIEW_OK")
        print("CLIPS 10 SOURCE_ACTIONS 10 BAKED_ACTIONS 10 JOINTS 68 FRAMES", kinematics["sampledFrames"])
        print("SOURCE_REVIEW", SOURCE_REVIEW_REPORT_PATH)
        return
    write_docs(metrics)
    write_body_boundary(plan)
    v2.base.save_source_blend(game_rig, control_rig)
    v2.base.prepare_clean_export_scene(game_rig, control_actions, baked_actions)
    v2.base.export_animation_fbx(game_rig)
    v2.base.write_manifest(rig_manifest, control_reports, baked_reports, metrics)
    patch_manifest(rig_manifest, v2_manifest, plan, metrics)
    print("SESSION_HUMANOID_ANIMATION_PROOF_V3_BUILD_OK")
    print("CLIPS 10 SOURCE_ACTIONS 10 BAKED_ACTIONS 10 JOINTS 68 FRAMES", kinematics["sampledFrames"])
    print("KINEMATICS", json.dumps(kinematics, sort_keys=True))
    print("FBX", FBX_PATH)


if __name__ == "__main__":
    main()
