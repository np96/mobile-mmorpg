#!/usr/bin/env python3
"""Build original technical animation proofs for SessionHumanoidRigV1.

The sparse source motion is authored exclusively on animator-facing Rigify
controls retained in the editable .blend.  Every frame is then sampled and baked
onto the separate 66-joint first-party game rig.  The exported FBX contains only
that clean skeleton and ten baked actions; it contains no mesh or DCC helper.
"""

from __future__ import annotations

from dataclasses import dataclass, field
import datetime
import hashlib
import json
import math
from pathlib import Path
import sys

import bpy
from mathutils import Euler, Matrix, Quaternion, Vector

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_session_humanoid_rig_v1 as rig_contract


PROJECT_ROOT = Path(__file__).resolve().parents[3]
SOURCE_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidAnimationProofV1"
OUTPUT_DIR = SOURCE_DIR / "Outputs"
SOURCE_BLEND = SOURCE_DIR / "SessionHumanoidAnimationProofV1.blend"
FBX_PATH = OUTPUT_DIR / "SessionHumanoidAnimationProofV1.fbx"
MANIFEST_PATH = OUTPUT_DIR / "SessionHumanoidAnimationProofV1.manifest.json"
PROVENANCE_PATH = OUTPUT_DIR / "SessionHumanoidAnimationProofV1.PROVENANCE.md"
CLIP_CONTRACT_PATH = SOURCE_DIR / "SessionHumanoidAnimationProofV1.CLIPS.md"
RIG_BLEND = rig_contract.BLEND_PATH
RIG_MANIFEST = rig_contract.MANIFEST_PATH
VERIFIER_PATH = Path(__file__).resolve().with_name("verify_session_humanoid_animation_proof_v1.py")

ASSET_ID = "session-humanoid-animation-proof-v1"
CONTROL_ACTION_PREFIX = "SRC_Rigify_"
FPS = 30
FBX_CREATION_TIMESTAMP = datetime.datetime(2026, 8, 20, 0, 0, 0)
IDENTITY = Matrix.Identity(4)
GAME_RIG_NAME = rig_contract.RIG_OBJECT_NAME
CONTROL_RIG_NAME = rig_contract.RIGIFY_CONTROL_NAME


@dataclass(frozen=True)
class ControlTransform:
    position: tuple[float, float, float] | None = None
    offset: tuple[float, float, float] = (0.0, 0.0, 0.0)
    euler: tuple[float, float, float] = (0.0, 0.0, 0.0)


@dataclass(frozen=True)
class KeyPose:
    frame: int
    controls: dict[str, ControlTransform]


@dataclass(frozen=True)
class ClipSpec:
    name: str
    frame_start: int
    frame_end: int
    loop: bool
    category: str
    keys: tuple[KeyPose, ...]
    contact_frames_left: tuple[int, ...] = field(default_factory=tuple)
    contact_frames_right: tuple[int, ...] = field(default_factory=tuple)


ANIMATED_CONTROLS = (
    "root", "torso", "hips", "chest", "spine_fk", "spine_fk.001",
    "spine_fk.002", "spine_fk.003", "neck", "head",
    "shoulder.L", "shoulder.R", "hand_ik.L", "hand_ik.R",
    "upper_arm_ik_target.L", "upper_arm_ik_target.R",
    "foot_ik.L", "foot_ik.R", "toe_ik.L", "toe_ik.R",
    "thigh_ik_target.L", "thigh_ik_target.R",
)
IK_SWITCH_CONTROLS = (
    "upper_arm_parent.L", "upper_arm_parent.R",
    "thigh_parent.L", "thigh_parent.R",
)
DEFORM_NAMES = tuple(spec.name for spec in rig_contract.BONE_SPECS if spec.deform)


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def vector_list(value: Vector, digits: int = 8) -> list[float]:
    return [round(component, digits) for component in value]


def iter_action_fcurves(action: bpy.types.Action):
    for layer in action.layers:
        for strip in layer.strips:
            if strip.type != "KEYFRAME":
                continue
            for channelbag in strip.channelbags:
                yield from channelbag.fcurves


def set_action_interpolation(action: bpy.types.Action, interpolation: str = "LINEAR") -> None:
    for fcurve in iter_action_fcurves(action):
        for keyframe in fcurve.keyframe_points:
            keyframe.interpolation = interpolation


def transform(
    *,
    position: tuple[float, float, float] | None = None,
    offset: tuple[float, float, float] = (0.0, 0.0, 0.0),
    euler: tuple[float, float, float] = (0.0, 0.0, 0.0),
) -> ControlTransform:
    return ControlTransform(position=position, offset=offset, euler=euler)


COMBAT_LEFT = (0.285, -0.205, 1.335)
COMBAT_RIGHT = (-0.285, -0.190, 1.345)
BOW_READY_LEFT = (0.305, -0.365, 1.395)
BOW_READY_RIGHT = (0.100, -0.340, 1.355)
BOW_HOLD_LEFT = (0.340, -0.480, 1.430)
BOW_HOLD_RIGHT = (-0.120, -0.120, 1.550)
BOW_LOOSE_LEFT = (0.355, -0.500, 1.425)
BOW_LOOSE_RIGHT = (0.140, -0.430, 1.425)


def body_pose(
    *,
    hips=(0.0, 0.0, 0.0),
    hips_rot=(0.0, 0.0, 0.0),
    torso=(0.0, 0.0, 0.0),
    chest=(0.0, 0.0, 0.0),
    head=(0.0, 0.0, 0.0),
    left_hand=COMBAT_LEFT,
    right_hand=COMBAT_RIGHT,
    left_foot=(0.0, 0.0, 0.0),
    right_foot=(0.0, 0.0, 0.0),
    left_shoulder=(0.0, 0.0, 0.0),
    right_shoulder=(0.0, 0.0, 0.0),
) -> dict[str, ControlTransform]:
    return {
        "root": transform(),
        "torso": transform(euler=torso),
        "hips": transform(offset=hips, euler=hips_rot),
        "chest": transform(euler=chest),
        "spine_fk": transform(),
        "spine_fk.001": transform(),
        "spine_fk.002": transform(),
        "spine_fk.003": transform(),
        "neck": transform(),
        "head": transform(euler=head),
        "shoulder.L": transform(euler=left_shoulder),
        "shoulder.R": transform(euler=right_shoulder),
        "hand_ik.L": transform(position=left_hand),
        "hand_ik.R": transform(position=right_hand),
        "upper_arm_ik_target.L": transform(),
        "upper_arm_ik_target.R": transform(),
        "foot_ik.L": transform(offset=left_foot),
        "foot_ik.R": transform(offset=right_foot),
        "toe_ik.L": transform(),
        "toe_ik.R": transform(),
        "thigh_ik_target.L": transform(),
        "thigh_ik_target.R": transform(),
    }


def build_clip_specs() -> tuple[ClipSpec, ...]:
    idle_keys = (
        KeyPose(1, body_pose()),
        KeyPose(13, body_pose(hips=(0.008, 0.0, 0.012), chest=(0.0, 0.018, 0.010), head=(0.0, -0.012, 0.0))),
        KeyPose(25, body_pose(hips=(0.0, 0.0, 0.022), chest=(0.0, 0.0, -0.012))),
        KeyPose(37, body_pose(hips=(-0.008, 0.0, 0.012), chest=(0.0, -0.018, 0.010), head=(0.0, 0.012, 0.0))),
        KeyPose(49, body_pose()),
    )

    locomotion_data = (
        (1,  (-0.012, 0.0, 0.000), (0.0, -0.120, 0.0), (0.0, 0.120, 0.0), (0.320, 0.105, 1.235), (-0.255, -0.215, 1.285), -0.075),
        (4,  (-0.007, 0.0, 0.018), (0.0, -0.040, -0.012), (0.0, 0.040, 0.090), (0.295, 0.055, 1.255), (-0.270, -0.150, 1.275), -0.040),
        (7,  (0.000, 0.0, 0.012), (0.0, 0.040, -0.035), (0.0, -0.040, 0.125), (0.270, -0.025, 1.270), (-0.290, -0.060, 1.255), 0.000),
        (10, (0.008, 0.0, 0.016), (0.0, 0.120, -0.018), (0.0, -0.120, 0.0), (0.250, -0.155, 1.285), (-0.315, 0.060, 1.235), 0.055),
        (13, (0.012, 0.0, 0.000), (0.0, 0.120, 0.0), (0.0, -0.120, 0.0), (0.255, -0.215, 1.285), (-0.320, 0.105, 1.235), 0.075),
        (16, (0.007, 0.0, 0.018), (0.0, 0.040, 0.090), (0.0, -0.040, -0.012), (0.270, -0.150, 1.275), (-0.295, 0.055, 1.255), 0.040),
        (19, (0.000, 0.0, 0.012), (0.0, -0.040, 0.125), (0.0, 0.040, -0.035), (0.290, -0.060, 1.255), (-0.270, -0.025, 1.270), 0.000),
        (22, (-0.008, 0.0, 0.016), (0.0, -0.120, 0.0), (0.0, 0.120, -0.018), (0.315, 0.060, 1.235), (-0.250, -0.155, 1.285), -0.055),
        (25, (-0.012, 0.0, 0.000), (0.0, -0.120, 0.0), (0.0, 0.120, 0.0), (0.320, 0.105, 1.235), (-0.255, -0.215, 1.285), -0.075),
    )
    locomotion_keys = tuple(
        KeyPose(frame, body_pose(
            hips=hips,
            chest=(0.0, twist, 0.0),
            left_foot=left_foot,
            right_foot=right_foot,
            left_hand=left_hand,
            right_hand=right_hand,
        ))
        for frame, hips, left_foot, right_foot, left_hand, right_hand, twist in locomotion_data
    )

    hit_keys = (
        KeyPose(1, body_pose()),
        KeyPose(4, body_pose(hips=(0.0, -0.015, 0.008), chest=(0.06, 0.0, 0.0))),
        KeyPose(8, body_pose(
            hips=(0.025, 0.070, -0.025), torso=(-0.14, 0.08, -0.12),
            chest=(-0.20, -0.16, 0.10), head=(0.12, 0.12, 0.0),
            left_hand=(0.405, 0.025, 1.260), right_hand=(-0.390, 0.020, 1.250),
        )),
        KeyPose(12, body_pose(hips=(-0.010, 0.025, 0.005), chest=(-0.06, 0.04, -0.03))),
        KeyPose(18, body_pose()),
    )

    death_keys = (
        KeyPose(1, body_pose()),
        KeyPose(8, body_pose(
            hips=(0.025, 0.055, -0.030), torso=(-0.18, 0.12, -0.10),
            chest=(-0.24, -0.12, 0.12),
            left_hand=(0.410, 0.020, 1.250), right_hand=(-0.400, 0.010, 1.245),
        )),
        KeyPose(18, body_pose(
            hips=(0.080, 0.080, -0.220), hips_rot=(0.10, 0.30, 0.18),
            torso=(0.28, 0.55, 0.25), chest=(0.22, 0.38, 0.18), head=(0.08, -0.20, 0.10),
            left_hand=(0.350, -0.020, 0.820), right_hand=(-0.250, 0.080, 0.850),
        )),
        KeyPose(30, body_pose(
            hips=(0.150, 0.100, -0.470), hips_rot=(0.18, 0.70, 0.25),
            torso=(0.40, 0.95, 0.30), chest=(0.28, 0.65, 0.22), head=(0.12, -0.28, 0.16),
            left_hand=(0.300, -0.100, 0.300), right_hand=(-0.120, 0.100, 0.330),
        )),
        KeyPose(42, body_pose(
            hips=(0.180, 0.110, -0.620), hips_rot=(0.24, 1.05, 0.32),
            torso=(0.48, 1.18, 0.36), chest=(0.32, 0.82, 0.28), head=(0.16, -0.35, 0.20),
            left_hand=(0.270, -0.150, 0.120), right_hand=(-0.050, 0.120, 0.145),
            left_foot=(0.0, 0.040, 0.0), right_foot=(0.0, -0.020, 0.0),
        )),
    )

    interrupt_keys = (
        KeyPose(1, body_pose(left_hand=(0.250, -0.300, 1.400), right_hand=(-0.245, -0.285, 1.405))),
        KeyPose(4, body_pose(
            hips=(0.0, -0.020, 0.012), chest=(0.10, 0.0, 0.0),
            left_hand=(0.215, -0.350, 1.445), right_hand=(-0.210, -0.340, 1.450),
        )),
        KeyPose(7, body_pose(
            hips=(0.020, 0.060, -0.020), torso=(-0.16, 0.12, 0.08), chest=(-0.24, -0.12, -0.10),
            left_hand=(0.380, 0.010, 1.270), right_hand=(-0.360, 0.030, 1.285),
        )),
        KeyPose(11, body_pose(hips=(-0.010, 0.020, 0.005), chest=(-0.06, 0.03, 0.02))),
        KeyPose(16, body_pose()),
    )

    bow_ready = body_pose(
        left_hand=BOW_READY_LEFT, right_hand=BOW_READY_RIGHT,
        chest=(0.0, -0.055, 0.02), left_shoulder=(0.0, 0.025, 0.0),
    )
    bow_hold = body_pose(
        hips=(0.0, 0.0, 0.008), torso=(0.0, -0.050, 0.0), chest=(0.0, -0.130, 0.025),
        head=(0.0, 0.055, 0.0), left_hand=BOW_HOLD_LEFT, right_hand=BOW_HOLD_RIGHT,
        left_shoulder=(0.0, 0.060, 0.0), right_shoulder=(0.0, -0.045, 0.0),
    )
    bow_loose = body_pose(
        hips=(0.0, 0.0, 0.004), torso=(0.0, 0.035, 0.0), chest=(0.0, 0.105, -0.025),
        head=(0.0, -0.035, 0.0), left_hand=BOW_LOOSE_LEFT, right_hand=BOW_LOOSE_RIGHT,
        left_shoulder=(0.0, 0.070, 0.0), right_shoulder=(0.0, 0.055, 0.0),
    )
    bow_ready_keys = (
        KeyPose(1, bow_ready),
        KeyPose(7, body_pose(
            hips=(0.004, 0.0, 0.008), chest=(0.0, -0.065, 0.025),
            left_hand=(0.308, -0.370, 1.401), right_hand=(0.098, -0.338, 1.360),
        )),
        KeyPose(13, body_pose(
            hips=(0.0, 0.0, 0.014), chest=(0.0, -0.055, 0.018),
            left_hand=(0.305, -0.365, 1.405), right_hand=(0.100, -0.340, 1.365),
        )),
        KeyPose(19, body_pose(
            hips=(-0.004, 0.0, 0.008), chest=(0.0, -0.045, 0.015),
            left_hand=(0.302, -0.360, 1.401), right_hand=(0.102, -0.342, 1.360),
        )),
        KeyPose(25, bow_ready),
    )
    bow_draw_keys = (
        KeyPose(1, bow_ready),
        KeyPose(6, body_pose(
            hips=(0.0, 0.0, 0.004), chest=(0.0, -0.075, 0.020),
            left_hand=(0.320, -0.405, 1.408), right_hand=(0.035, -0.285, 1.410),
        )),
        KeyPose(12, body_pose(
            hips=(0.0, 0.0, 0.006), torso=(0.0, -0.025, 0.0), chest=(0.0, -0.105, 0.022),
            left_hand=(0.332, -0.450, 1.420), right_hand=(-0.045, -0.205, 1.480),
        )),
        KeyPose(20, bow_hold),
    )
    bow_hold_keys = (
        KeyPose(1, bow_hold),
        KeyPose(7, body_pose(
            hips=(0.003, 0.0, 0.012), torso=(0.0, -0.052, 0.0), chest=(0.0, -0.135, 0.027),
            head=(0.0, 0.058, 0.0), left_hand=(0.342, -0.482, 1.434), right_hand=(-0.122, -0.118, 1.554),
        )),
        KeyPose(13, bow_hold),
        KeyPose(19, body_pose(
            hips=(-0.003, 0.0, 0.005), torso=(0.0, -0.048, 0.0), chest=(0.0, -0.125, 0.023),
            head=(0.0, 0.052, 0.0), left_hand=(0.338, -0.478, 1.426), right_hand=(-0.118, -0.122, 1.546),
        )),
        KeyPose(25, bow_hold),
    )
    bow_loose_keys = (
        KeyPose(1, bow_hold),
        KeyPose(3, body_pose(
            hips=(0.0, 0.0, 0.010), torso=(0.0, -0.060, 0.0), chest=(0.0, -0.155, 0.030),
            head=(0.0, 0.065, 0.0), left_hand=(0.345, -0.490, 1.432), right_hand=(-0.150, -0.095, 1.565),
        )),
        KeyPose(5, body_pose(
            hips=(0.0, 0.0, 0.007), torso=(0.0, 0.010, 0.0), chest=(0.0, 0.030, -0.010),
            left_hand=(0.350, -0.495, 1.430), right_hand=(0.025, -0.325, 1.490),
        )),
        KeyPose(10, bow_loose),
    )
    bow_recovery_keys = (
        KeyPose(1, bow_loose),
        KeyPose(6, body_pose(
            hips=(0.0, 0.0, 0.003), chest=(0.0, 0.030, -0.010),
            left_hand=(0.340, -0.465, 1.420), right_hand=(0.125, -0.365, 1.390),
        )),
        KeyPose(12, body_pose(
            hips=(0.0, 0.0, 0.002), chest=(0.0, -0.025, 0.005),
            left_hand=(0.320, -0.405, 1.405), right_hand=(0.115, -0.350, 1.365),
        )),
        KeyPose(18, bow_ready),
    )

    return (
        ClipSpec("CombatIdle", 1, 49, True, "combat_idle", idle_keys, tuple(range(1, 50)), tuple(range(1, 50))),
        ClipSpec("LocomotionForward", 1, 25, True, "directional_in_place_locomotion", locomotion_keys,
                 tuple(range(1, 14)) + tuple(range(22, 26)), tuple(range(1, 5)) + tuple(range(10, 26))),
        ClipSpec("Hit", 1, 18, False, "reaction", hit_keys, tuple(range(1, 19)), tuple(range(1, 19))),
        ClipSpec("Death", 1, 42, False, "reaction", death_keys, (1, 2, 3, 4, 5, 6, 7, 8), (1, 2, 3, 4, 5, 6, 7, 8)),
        ClipSpec("Interrupt", 1, 16, False, "reaction", interrupt_keys, tuple(range(1, 17)), tuple(range(1, 17))),
        ClipSpec("HunterBowReady", 1, 25, True, "hunter_bow", bow_ready_keys, tuple(range(1, 26)), tuple(range(1, 26))),
        ClipSpec("HunterBowDraw", 1, 20, False, "hunter_bow", bow_draw_keys, tuple(range(1, 21)), tuple(range(1, 21))),
        ClipSpec("HunterBowHold", 1, 25, True, "hunter_bow", bow_hold_keys, tuple(range(1, 26)), tuple(range(1, 26))),
        ClipSpec("HunterBowLoose", 1, 10, False, "hunter_bow", bow_loose_keys, tuple(range(1, 11)), tuple(range(1, 11))),
        ClipSpec("HunterBowRecovery", 1, 18, False, "hunter_bow", bow_recovery_keys, tuple(range(1, 19)), tuple(range(1, 19))),
    )


CLIPS = build_clip_specs()
CLIP_BY_NAME = {clip.name: clip for clip in CLIPS}


def validate_static_contract() -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    require(RIG_BLEND.is_file() and RIG_MANIFEST.is_file(), "SessionHumanoidRigV1 source contract is missing")
    require(VERIFIER_PATH.is_file(), "Animation proof verifier is missing")
    require("Assets" not in SOURCE_DIR.parts, "Animation proof must remain outside Unity Assets")
    names = [clip.name for clip in CLIPS]
    require(len(names) == 10 and len(set(names)) == 10, "Expected ten unique proof clips")
    for clip in CLIPS:
        require(clip.keys[0].frame == clip.frame_start, f"{clip.name} lacks a first-frame source key")
        require(clip.keys[-1].frame == clip.frame_end, f"{clip.name} lacks a last-frame source key")
        require(tuple(key.frame for key in clip.keys) == tuple(sorted(key.frame for key in clip.keys)), f"{clip.name} source keys are unordered")
        for key in clip.keys:
            require(set(key.controls) == set(ANIMATED_CONTROLS), f"{clip.name} frame {key.frame} does not define every animated control")


def load_rig_source() -> tuple[bpy.types.Object, bpy.types.Object, dict[str, object]]:
    bpy.ops.wm.open_mainfile(filepath=str(RIG_BLEND))
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.render.fps = FPS
    require(not bpy.data.actions, "Canonical rig source unexpectedly contains actions")
    require(GAME_RIG_NAME in bpy.data.objects and CONTROL_RIG_NAME in bpy.data.objects, "Canonical source lost its game/control rig")
    game_rig = bpy.data.objects[GAME_RIG_NAME]
    control_rig = bpy.data.objects[CONTROL_RIG_NAME]
    require(game_rig.type == "ARMATURE" and control_rig.type == "ARMATURE", "Animation source armatures are invalid")
    require(set(ANIMATED_CONTROLS).issubset(control_rig.pose.bones.keys()), "Required Rigify animator controls are missing")
    require(set(IK_SWITCH_CONTROLS).issubset(control_rig.pose.bones.keys()), "Required Rigify IK switches are missing")
    rig_manifest = json.loads(RIG_MANIFEST.read_text(encoding="utf-8"))
    require(rig_manifest["hierarchy"]["exportedBoneCount"] == 66, "Animation builder requires the 66-joint rig contract")
    return game_rig, control_rig, rig_manifest


def reset_control_pose(control_rig: bpy.types.Object) -> None:
    for pose_bone in control_rig.pose.bones:
        pose_bone.matrix_basis = IDENTITY


def apply_control_transform(control_rig: bpy.types.Object, name: str, command: ControlTransform) -> None:
    pose_bone = control_rig.pose.bones[name]
    rest = pose_bone.bone.matrix_local.copy()
    if command.position is not None:
        displacement = Vector(command.position) - pose_bone.bone.head_local
    else:
        displacement = Vector(command.offset)
    local_rotation = Euler(command.euler, "XYZ").to_matrix().to_4x4()
    pose_bone.rotation_mode = "QUATERNION"
    pose_bone.matrix = Matrix.Translation(displacement) @ rest @ local_rotation


def key_pose_bone_transform(pose_bone: bpy.types.PoseBone, frame: int) -> None:
    pose_bone.keyframe_insert(data_path="location", frame=frame, group=pose_bone.name)
    pose_bone.keyframe_insert(data_path="rotation_quaternion", frame=frame, group=pose_bone.name)
    pose_bone.keyframe_insert(data_path="scale", frame=frame, group=pose_bone.name)


def create_control_action(control_rig: bpy.types.Object, clip: ClipSpec) -> bpy.types.Action:
    action = bpy.data.actions.new(CONTROL_ACTION_PREFIX + clip.name)
    action.use_fake_user = True
    action.use_frame_range = True
    action.frame_start = clip.frame_start
    action.frame_end = clip.frame_end
    action["sessionRole"] = "rigify_control_source_not_exported"
    action["clipName"] = clip.name
    action["loop"] = clip.loop
    control_rig.animation_data_create()
    control_rig.animation_data.action = action
    scene = bpy.context.scene
    for key in clip.keys:
        scene.frame_set(key.frame)
        reset_control_pose(control_rig)
        for switch_name in IK_SWITCH_CONTROLS:
            switch = control_rig.pose.bones[switch_name]
            # This generated Rigify limb blends to its IK constraint at 0 and
            # to the FK chain at 1.  These clips use explicit IK targets.
            switch["IK_FK"] = 0.0
            switch["IK_Stretch"] = 0.0
            switch["pole_vector"] = True
            switch.keyframe_insert(data_path='["IK_FK"]', frame=key.frame, group=switch.name)
            switch.keyframe_insert(data_path='["IK_Stretch"]', frame=key.frame, group=switch.name)
            switch.keyframe_insert(data_path='["pole_vector"]', frame=key.frame, group=switch.name)
        for control_name in ANIMATED_CONTROLS:
            apply_control_transform(control_rig, control_name, key.controls[control_name])
            key_pose_bone_transform(control_rig.pose.bones[control_name], key.frame)
    set_action_interpolation(action, "LINEAR")
    control_rig.animation_data.action = None
    reset_control_pose(control_rig)
    return action


def local_pose_basis(rest_bone: bpy.types.Bone, pose_bone: bpy.types.PoseBone) -> Matrix:
    if rest_bone.parent is None:
        return rest_bone.matrix_local.inverted() @ pose_bone.matrix
    parent_rest = rest_bone.parent.matrix_local
    parent_pose = pose_bone.parent.matrix
    placement = parent_pose @ parent_rest.inverted() @ rest_bone.matrix_local
    return placement.inverted() @ pose_bone.matrix


def clean_basis(rotation: Quaternion, location: Vector | None = None) -> Matrix:
    return Matrix.LocRotScale(
        location if location is not None else Vector((0.0, 0.0, 0.0)),
        rotation.normalized(),
        Vector((1.0, 1.0, 1.0)),
    )


def sample_clean_game_pose(
    game_rig: bpy.types.Object,
    control_rig: bpy.types.Object,
) -> dict[str, Matrix]:
    """Retarget the evaluated Rigify ORG pose onto the clean game rest rig.

    Rigify regenerates ORG bones with control-friendly roll axes that differ
    from the export rig's conventional game-bone rolls.  Copying their local
    quaternion components is therefore not a valid bake.  Instead, apply each
    evaluated ORG bone's world-space delta from rest to the corresponding game
    rest matrix, then rebuild child heads from the clean game hierarchy so no
    Rigify scale, shear, or pole-bias translation leaks into the export.
    """
    depsgraph = bpy.context.evaluated_depsgraph_get()
    evaluated_control = control_rig.evaluated_get(depsgraph)
    pose_by_name: dict[str, Matrix] = {}
    for spec in rig_contract.BONE_SPECS:
        game_rest = game_rig.data.bones[spec.name].matrix_local.copy()
        # The Rigify `hips` animator control is the authoritative pelvis
        # translation/rotation.  ORG-spine is a generated spline result whose
        # head can shift as the torso solver evaluates, so it is unsuitable as
        # the root of a deterministic game-skeleton bake.
        target_name = "hips" if spec.name == "Hips" else rig_contract.DCC_DRIVER_TARGETS.get(spec.name)
        if spec.name == "Root":
            # Identity matrix_basis: every proof clip is deliberately in-place.
            pose_by_name[spec.name] = game_rest
        elif target_name is not None:
            target_rest = control_rig.data.bones[target_name].matrix_local
            target_pose = evaluated_control.pose.bones[target_name].matrix
            desired = target_pose @ target_rest.inverted() @ game_rest
            if spec.name == "Hips":
                desired_head = desired.translation
            else:
                parent_rest = game_rig.data.bones[spec.parent].matrix_local
                parent_placement = pose_by_name[spec.parent] @ parent_rest.inverted() @ game_rest
                desired_head = parent_placement.translation
            pose_by_name[spec.name] = Matrix.LocRotScale(
                desired_head,
                desired.to_quaternion().normalized(),
                Vector((1.0, 1.0, 1.0)),
            )
        else:
            parent_rest = game_rig.data.bones[spec.parent].matrix_local
            pose_by_name[spec.name] = (
                pose_by_name[spec.parent]
                @ parent_rest.inverted()
                @ game_rest
            )
    return pose_by_name


def sample_clip(
    game_rig: bpy.types.Object,
    control_rig: bpy.types.Object,
    control_action: bpy.types.Action,
    clip: ClipSpec,
) -> dict[int, dict[str, Matrix]]:
    control_rig.animation_data.action = control_action
    sampled: dict[int, dict[str, Matrix]] = {}
    for frame in range(clip.frame_start, clip.frame_end + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        sampled[frame] = sample_clean_game_pose(game_rig, control_rig)
    control_rig.animation_data.action = None
    reset_control_pose(control_rig)
    bpy.context.view_layer.update()
    return sampled


def copied_pose(pose: dict[str, Matrix]) -> dict[str, Matrix]:
    return {name: matrix.copy() for name, matrix in pose.items()}


def stabilize_intentionally_identical_endpoints(
    sampled_by_clip: dict[str, dict[int, dict[str, Matrix]]],
) -> None:
    """Canonicalize only source keys that are intentionally the same pose.

    Rigify's procedural IK/spline solvers may retain evaluation history, so two
    identical animator-control key poses can differ slightly depending on the
    frames evaluated immediately before them.  A production bake must not carry
    that solver state into loop seams or action boundaries.  This function does
    not synthesize motion: it copies one authored endpoint to another endpoint
    whose source control dictionary is contractually identical.
    """
    for clip in CLIPS:
        if clip.loop:
            require(clip.keys[0].controls == clip.keys[-1].controls, f"{clip.name} loop controls are not identical")
            samples = sampled_by_clip[clip.name]
            samples[clip.frame_end] = copied_pose(samples[clip.frame_start])

    intended_matches = (
        ("HunterBowReady", -1, "HunterBowDraw", 0),
        ("HunterBowDraw", -1, "HunterBowHold", 0),
        ("HunterBowHold", -1, "HunterBowLoose", 0),
        ("HunterBowLoose", -1, "HunterBowRecovery", 0),
        ("HunterBowRecovery", -1, "HunterBowReady", 0),
    )
    for first_name, first_index, second_name, second_index in intended_matches:
        require(
            CLIP_BY_NAME[first_name].keys[first_index].controls
            == CLIP_BY_NAME[second_name].keys[second_index].controls,
            f"{first_name}->{second_name} source controls are not identical",
        )

    ready_pose = sampled_by_clip["HunterBowReady"][CLIP_BY_NAME["HunterBowReady"].frame_start]
    sampled_by_clip["HunterBowDraw"][CLIP_BY_NAME["HunterBowDraw"].frame_start] = copied_pose(ready_pose)

    full_draw_pose = sampled_by_clip["HunterBowDraw"][CLIP_BY_NAME["HunterBowDraw"].frame_end]
    sampled_by_clip["HunterBowHold"][CLIP_BY_NAME["HunterBowHold"].frame_start] = copied_pose(full_draw_pose)
    sampled_by_clip["HunterBowHold"][CLIP_BY_NAME["HunterBowHold"].frame_end] = copied_pose(full_draw_pose)
    sampled_by_clip["HunterBowLoose"][CLIP_BY_NAME["HunterBowLoose"].frame_start] = copied_pose(full_draw_pose)

    loose_pose = sampled_by_clip["HunterBowLoose"][CLIP_BY_NAME["HunterBowLoose"].frame_end]
    sampled_by_clip["HunterBowRecovery"][CLIP_BY_NAME["HunterBowRecovery"].frame_start] = copied_pose(loose_pose)
    sampled_by_clip["HunterBowRecovery"][CLIP_BY_NAME["HunterBowRecovery"].frame_end] = copied_pose(ready_pose)


def set_constraints_muted(game_rig: bpy.types.Object, muted: bool) -> None:
    for pose_bone in game_rig.pose.bones:
        for constraint in pose_bone.constraints:
            constraint.mute = muted


def create_baked_action(
    game_rig: bpy.types.Object,
    clip: ClipSpec,
    sampled: dict[int, dict[str, Matrix]],
) -> bpy.types.Action:
    set_constraints_muted(game_rig, True)
    action = bpy.data.actions.new(clip.name)
    action.use_fake_user = True
    action.use_frame_range = True
    action.frame_start = clip.frame_start
    action.frame_end = clip.frame_end
    action["sessionRole"] = "clean_66_joint_game_bake"
    action["sourceControlAction"] = CONTROL_ACTION_PREFIX + clip.name
    action["loop"] = clip.loop
    action["rootMotion"] = "in_place_identity_root"
    game_rig.animation_data_create()
    game_rig.animation_data.action = action
    previous_quaternions: dict[str, Quaternion] = {}
    for frame in range(clip.frame_start, clip.frame_end + 1):
        bpy.context.scene.frame_set(frame)
        for pose_bone in game_rig.pose.bones:
            pose_bone.matrix_basis = IDENTITY
            pose_bone.rotation_mode = "QUATERNION"
        bpy.context.view_layer.update()
        for spec in rig_contract.BONE_SPECS:
            pose_bone = game_rig.pose.bones[spec.name]
            rest = game_rig.data.bones[spec.name].matrix_local
            desired = sampled[frame][spec.name]
            if spec.parent is None:
                basis = rest.inverted() @ desired
            else:
                parent_rest = game_rig.data.bones[spec.parent].matrix_local
                parent_pose = sampled[frame][spec.parent]
                placement = parent_pose @ parent_rest.inverted() @ rest
                basis = placement.inverted() @ desired
            location, quaternion, _scale = basis.decompose()
            pose_bone.location = location
            pose_bone.rotation_quaternion = quaternion.normalized()
            pose_bone.scale = Vector((1.0, 1.0, 1.0))
            quaternion = pose_bone.rotation_quaternion.copy()
            previous = previous_quaternions.get(spec.name)
            if previous is not None and quaternion.dot(previous) < 0.0:
                quaternion.negate()
                pose_bone.rotation_quaternion = quaternion
            previous_quaternions[spec.name] = quaternion.copy()
        for spec in rig_contract.BONE_SPECS:
            key_pose_bone_transform(game_rig.pose.bones[spec.name], frame)
    set_action_interpolation(action, "LINEAR")
    game_rig.animation_data.action = None
    for pose_bone in game_rig.pose.bones:
        pose_bone.matrix_basis = IDENTITY
    set_constraints_muted(game_rig, False)
    bpy.context.view_layer.update()
    return action


def evaluate_action_samples(
    game_rig: bpy.types.Object,
    action: bpy.types.Action,
    clip: ClipSpec,
) -> dict[int, dict[str, Vector]]:
    set_constraints_muted(game_rig, True)
    game_rig.animation_data.action = action
    samples: dict[int, dict[str, Vector]] = {}
    for frame in range(clip.frame_start, clip.frame_end + 1):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        evaluated = game_rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
        samples[frame] = {
            name: evaluated.pose.bones[name].head.copy()
            for name in ("Root", "Hips", "Head", "LeftUpperArm", "RightUpperArm",
                         "LeftLowerArm", "RightLowerArm", "LeftHand", "RightHand",
                         "Socket_FootFX_L", "Socket_FootFX_R")
        }
    game_rig.animation_data.action = None
    set_constraints_muted(game_rig, False)
    bpy.context.view_layer.update()
    return samples


def joint_rms(first: dict[str, Vector], second: dict[str, Vector], names=DEFORM_NAMES) -> float:
    return math.sqrt(sum((first[name] - second[name]).length_squared for name in names) / len(names))


def collect_action_metrics(
    game_rig: bpy.types.Object,
    baked_actions: dict[str, bpy.types.Action],
) -> tuple[dict[str, object], dict[str, dict[int, dict[str, Vector]]]]:
    samples_by_clip: dict[str, dict[int, dict[str, Vector]]] = {}
    metrics: dict[str, object] = {}
    foot_rest_z = rig_contract.SPEC_BY_NAME["Socket_FootFX_L"].head[2]
    for clip in CLIPS:
        samples = evaluate_action_samples(game_rig, baked_actions[clip.name], clip)
        samples_by_clip[clip.name] = samples
        maximum_root_travel = max((pose["Root"] - Vector((0.0, 0.0, 0.0))).length for pose in samples.values())
        minimum_grounded_feet = min(
            sum(pose[name].z <= foot_rest_z + 0.025 for name in ("Socket_FootFX_L", "Socket_FootFX_R"))
            for pose in samples.values()
        )
        loop_closure = None
        if clip.loop:
            loop_closure = joint_rms(samples[clip.frame_start], samples[clip.frame_end], tuple(samples[clip.frame_start]))
        metrics[clip.name] = {
            "frames": clip.frame_end - clip.frame_start + 1,
            "loop": clip.loop,
            "maximumRootJointTravelMeters": maximum_root_travel,
            "minimumGroundedFeetPerFrame": minimum_grounded_feet,
            "loopClosureJointRmsMeters": loop_closure,
        }

    idle = samples_by_clip["CombatIdle"]
    idle_hips_z = [pose["Hips"].z for pose in idle.values()]
    metrics["CombatIdle"].update({
        "hipsVerticalAmplitudeMeters": max(idle_hips_z) - min(idle_hips_z),
        "maximumLeftFootTipVerticalDriftMeters": max(abs(pose["Socket_FootFX_L"].z - foot_rest_z) for pose in idle.values()),
        "maximumRightFootTipVerticalDriftMeters": max(abs(pose["Socket_FootFX_R"].z - foot_rest_z) for pose in idle.values()),
    })

    locomotion = samples_by_clip["LocomotionForward"]
    metrics["LocomotionForward"].update({
        "leftFootStrideMeters": max(pose["Socket_FootFX_L"].y for pose in locomotion.values()) - min(pose["Socket_FootFX_L"].y for pose in locomotion.values()),
        "rightFootStrideMeters": max(pose["Socket_FootFX_R"].y for pose in locomotion.values()) - min(pose["Socket_FootFX_R"].y for pose in locomotion.values()),
        "leftFootSwingHeightMeters": max(pose["Socket_FootFX_L"].z for pose in locomotion.values()) - foot_rest_z,
        "rightFootSwingHeightMeters": max(pose["Socket_FootFX_R"].z for pose in locomotion.values()) - foot_rest_z,
        "directionalReuse": "rotate actor heading for eight directions; clip has no root yaw or translation",
    })

    death = samples_by_clip["Death"]
    idle_head_height = samples_by_clip["CombatIdle"][1]["Head"].z
    metrics["Death"].update({
        "finalHeadHeightMeters": death[CLIP_BY_NAME["Death"].frame_end]["Head"].z,
        "finalHeadHeightAsIdlePercent": 100.0 * death[CLIP_BY_NAME["Death"].frame_end]["Head"].z / idle_head_height,
        "finalHipsHeightMeters": death[CLIP_BY_NAME["Death"].frame_end]["Hips"].z,
    })

    draw = samples_by_clip["HunterBowDraw"]
    hold = samples_by_clip["HunterBowHold"]
    loose = samples_by_clip["HunterBowLoose"]
    draw_start = draw[CLIP_BY_NAME["HunterBowDraw"].frame_start]
    draw_end = draw[CLIP_BY_NAME["HunterBowDraw"].frame_end]
    loose_start = loose[CLIP_BY_NAME["HunterBowLoose"].frame_start]
    loose_end = loose[CLIP_BY_NAME["HunterBowLoose"].frame_end]
    shoulder_span = (draw_end["LeftUpperArm"] - draw_end["RightUpperArm"]).length
    right_arm_mean_travel = 0.5 * (
        (loose_end["RightLowerArm"] - loose_start["RightLowerArm"]).length
        + (loose_end["RightHand"] - loose_start["RightHand"]).length
    )
    bow_metrics = {
        "drawHandTravelMeters": (draw_end["RightHand"] - draw_start["RightHand"]).length,
        "fullDrawHandSeparationMeters": (draw_end["LeftHand"] - draw_end["RightHand"]).length,
        "looseDrawHandTravelMeters": (loose_end["RightHand"] - loose_start["RightHand"]).length,
        "holdToLooseRightArmMeanJointTravelMeters": right_arm_mean_travel,
        "holdToLooseRightArmTravelAsShoulderSpanPercent": 100.0 * right_arm_mean_travel / shoulder_span,
        "shoulderSpanMeters": shoulder_span,
        "jointTravelSilhouetteProxy": "mean RightLowerArm/RightHand travel divided by shoulder span",
    }
    metrics["hunterBowSequence"] = bow_metrics

    transitions = (
        ("HunterBowReady", 25, "HunterBowDraw", 1),
        ("HunterBowDraw", 20, "HunterBowHold", 1),
        ("HunterBowHold", 25, "HunterBowLoose", 1),
        ("HunterBowLoose", 10, "HunterBowRecovery", 1),
        ("HunterBowRecovery", 18, "HunterBowReady", 1),
    )
    continuity = {}
    continuity_names = tuple(samples_by_clip["HunterBowReady"][1])
    for first_name, first_frame, second_name, second_frame in transitions:
        key = f"{first_name}->{second_name}"
        continuity[key] = joint_rms(
            samples_by_clip[first_name][first_frame],
            samples_by_clip[second_name][second_frame],
            continuity_names,
        )
    metrics["hunterBowContinuityJointRmsMeters"] = continuity
    return metrics, samples_by_clip


def validate_metrics(metrics: dict[str, object]) -> None:
    for clip in CLIPS:
        clip_metrics = metrics[clip.name]
        require(clip_metrics["maximumRootJointTravelMeters"] <= 0.00002, f"{clip.name} violates identity-root in-place motion")
        if clip.name != "Death":
            require(clip_metrics["minimumGroundedFeetPerFrame"] >= 1, f"{clip.name} has an airborne frame")
        if clip.loop:
            require(clip_metrics["loopClosureJointRmsMeters"] <= 0.002, f"{clip.name} loop does not close")
    idle = metrics["CombatIdle"]
    require(0.006 <= idle["hipsVerticalAmplitudeMeters"] <= 0.050, "Combat idle lacks controlled breathing weight")
    require(idle["maximumLeftFootTipVerticalDriftMeters"] <= 0.025, "Combat idle left foot slides vertically")
    require(idle["maximumRightFootTipVerticalDriftMeters"] <= 0.025, "Combat idle right foot slides vertically")
    locomotion = metrics["LocomotionForward"]
    require(locomotion["leftFootStrideMeters"] >= 0.14 and locomotion["rightFootStrideMeters"] >= 0.14, "Locomotion stride is too small")
    require(locomotion["leftFootSwingHeightMeters"] >= 0.050 and locomotion["rightFootSwingHeightMeters"] >= 0.050, "Locomotion lacks foot clearance")
    death = metrics["Death"]
    require(death["finalHeadHeightAsIdlePercent"] <= 70.0, "Death does not produce a collapsed final pose")
    require(death["finalHipsHeightMeters"] <= 0.60, "Death hips remain standing")
    bow = metrics["hunterBowSequence"]
    require(bow["drawHandTravelMeters"] >= 0.30, "Bow draw lacks useful hand travel")
    require(bow["fullDrawHandSeparationMeters"] >= 0.45, "Bow hold lacks a readable full-draw extension")
    require(bow["looseDrawHandTravelMeters"] >= 0.30, "Bow loose lacks a strong release")
    require(bow["holdToLooseRightArmTravelAsShoulderSpanPercent"] >= 30.0, "Draw-to-loose joint silhouette proxy is below 30%")
    require(max(metrics["hunterBowContinuityJointRmsMeters"].values()) <= 0.025, "Bow sequence transition discontinuity is too large")


def action_curve_report(action: bpy.types.Action) -> dict[str, object]:
    fcurves = list(iter_action_fcurves(action))
    keyed_bones = sorted({
        curve.data_path.split('pose.bones["', 1)[1].split('"]', 1)[0]
        for curve in fcurves
        if 'pose.bones["' in curve.data_path
    })
    return {
        "name": action.name,
        "frameRange": [round(action.frame_range[0]), round(action.frame_range[1])],
        "fcurveCount": len(fcurves),
        "keyframePointCount": sum(len(curve.keyframe_points) for curve in fcurves),
        "keyedBoneCount": len(keyed_bones),
        "keyedBones": keyed_bones,
        "allLinear": all(point.interpolation == "LINEAR" for curve in fcurves for point in curve.keyframe_points),
    }


def write_clip_contract(metrics: dict[str, object]) -> None:
    rows = []
    for clip in CLIPS:
        rows.append(
            f"| `{clip.name}` | {clip.frame_start}-{clip.frame_end} | "
            f"{'yes' if clip.loop else 'no'} | `{clip.category}` |"
        )
    CLIP_CONTRACT_PATH.write_text(
        """# Session Humanoid Animation Proof V1 clip contract

Status: **technical source proof rebaked on the accepted MPFB-body neutral
midpoint rest contract; not a visual animation acceptance; not integrated into
Unity**.

- 30 fps, meter scale, in-place identity `Root` for every sampled frame.
- Sparse source actions animate only Rigify animator controls. Every integer frame
  is baked onto the separate 66-joint game skeleton.
- `LocomotionForward` is one neutral forward cycle intended for eight-direction
  reuse by rotating the actor heading. It contains no authored root yaw/translation.
- Feet use Rigify IK contacts. At least one toe-tip socket remains grounded on
  every non-death frame; the locomotion swing foot clears the contact plane.
- Bow ready/draw/hold/loose/recovery transitions are continuous. Draw and loose
  are gated by hand separation, hand travel, and right-arm travel relative to
  shoulder span. These are joint-silhouette proxies, not a body-render approval.

| Clip | Frames | Loop | Purpose |
|---|---:|---|---|
""" + "\n".join(rows) + "\n\n## Current technical metrics\n\n```json\n"
        + json.dumps(metrics, indent=2, sort_keys=True) + "\n```\n",
        encoding="utf-8",
    )


def write_provenance(rig_manifest: dict[str, object]) -> None:
    PROVENANCE_PATH.write_text(
        f"""# Session Humanoid Animation Proof V1 provenance

Status: **first-party technical animation proof; not integrated into Unity**.

All ten control performances and their numeric key poses are authored in
`build_session_humanoid_animation_proof_v1.py`. No legacy, Quaternius, external,
downloaded, captured, or retargeted clip is imported, inspected, or sampled.

The only skeleton source dependency is the project's own refit
`SessionHumanoidRigV1` Blender contract. Its input manifest SHA-256 for this build is
`{sha256(RIG_MANIFEST)}` and its clean rig FBX SHA-256 is
`{rig_manifest['outputHashes'][rig_contract.FBX_PATH.name]}`.

Sparse actions are authored on the Blender-bundled Rigify animator controls and
retained in the editable source. The builder samples their evaluated motion,
remaps it onto the exact project game rest hierarchy, clamps translation to
`Hips` while keeping `Root` identity, and keys every one of the 66 game joints.
Only those ten baked game actions and that clean skeleton are present in the FBX.

The game rest joints use rig-fit revision
`{rig_manifest['restFit']['revision']}`, numeric landmark artifact SHA-256
`{rig_manifest['restFit']['inputArtifactSha256']}`, and canonical midpoint
proposal SHA-256 `{rig_manifest['restFit']['neutralProposalCanonicalSha256']}`.
The ten performances were regenerated and baked after that refit. They remain a
technical proof until the accepted bodies are bound and visually scanned; this is
not a Unity integration freeze.
""",
        encoding="utf-8",
    )


def save_source_blend(game_rig: bpy.types.Object, control_rig: bpy.types.Object) -> None:
    game_rig.animation_data.action = None
    control_rig.animation_data.action = None
    for pose_bone in game_rig.pose.bones:
        pose_bone.matrix_basis = IDENTITY
    reset_control_pose(control_rig)
    set_constraints_muted(game_rig, False)
    bpy.context.scene.frame_set(1)
    bpy.ops.object.select_all(action="DESELECT")
    control_rig.hide_set(False)
    control_rig.select_set(True)
    bpy.context.view_layer.objects.active = control_rig
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_BLEND), check_existing=False)


def prepare_clean_export_scene(
    game_rig: bpy.types.Object,
    control_actions: dict[str, bpy.types.Action],
    baked_actions: dict[str, bpy.types.Action],
) -> None:
    for pose_bone in game_rig.pose.bones:
        for constraint in list(pose_bone.constraints):
            pose_bone.constraints.remove(constraint)
        pose_bone.matrix_basis = IDENTITY
    for obj in list(bpy.data.objects):
        if obj != game_rig:
            bpy.data.objects.remove(obj, do_unlink=True)
    for action in control_actions.values():
        bpy.data.actions.remove(action)
    require(set(bpy.data.actions) == set(baked_actions.values()), "Export scene contains a non-game action")
    game_rig.animation_data_create()
    # Keep the armature in its exact bind pose while the exporter creates the
    # skeleton nodes. `use_all_actions` discovers the ten actions without an
    # active clip; leaving CombatIdle active would contaminate connected arm
    # joints in the imported FBX rest hierarchy.
    game_rig.animation_data.action = None
    bpy.context.scene.frame_set(0)
    for pose_bone in game_rig.pose.bones:
        pose_bone.matrix_basis = IDENTITY
    game_rig.hide_set(False)
    game_rig.hide_render = False
    game_rig.location = Vector((0.0, 0.0, 0.0))
    game_rig.rotation_euler = (0.0, 0.0, 0.0)
    game_rig.scale = Vector((1.0, 1.0, 1.0))
    bpy.ops.object.select_all(action="DESELECT")
    game_rig.select_set(True)
    bpy.context.view_layer.objects.active = game_rig
    bpy.context.view_layer.update()


def export_animation_fbx(game_rig: bpy.types.Object) -> None:
    from io_scene_fbx import export_fbx_bin, fbx_utils
    original_header_writer = export_fbx_bin.fbx_header_elements
    original_data_from_scene = export_fbx_bin.fbx_data_from_scene
    original_uuid_factory = fbx_utils._key_to_uuid

    def deterministic_header_writer(root, scene_data, time=None):
        return original_header_writer(root, scene_data, time=FBX_CREATION_TIMESTAMP)

    def deterministic_uuid_factory(uuids, key):
        if isinstance(key, int) and 0 <= key < 2**63:
            value = key
        else:
            value = int.from_bytes(hashlib.sha256(repr(key).encode("utf-8")).digest()[:8], "big") & ((1 << 63) - 1)
            value = value or 1
        while value in uuids:
            value = (value + 1) & ((1 << 63) - 1)
            value = value or 1
        return fbx_utils.UUID(value)

    def canonicalize_animations(animations):
        """Return exporter animation records in a content-defined order.

        Blender's FBX exporter gathers animated bones in set iteration order.
        The resulting FBX is semantically identical, but AnimationCurveNode,
        AnimationCurve, and Connection elements can be serialized in a
        different order on otherwise identical clean builds.  Preserve every
        sampled value and only canonicalize the nested exporter containers.
        """
        canonical = []
        for astack_key, alayers, alayer_key, name, frame_start, frame_end in animations:
            ordered_layers = {}
            for object_key, (object_layer_key, curve_nodes) in sorted(
                alayers.items(), key=lambda item: repr(item[0])
            ):
                ordered_nodes = {}
                for fbx_property, (curve_node_key, curves, curve_node_name) in sorted(
                    curve_nodes.items(), key=lambda item: item[0]
                ):
                    ordered_curves = {
                        fbx_item: curve_data
                        for fbx_item, curve_data in sorted(curves.items(), key=lambda item: item[0])
                    }
                    ordered_nodes[fbx_property] = (
                        curve_node_key,
                        ordered_curves,
                        curve_node_name,
                    )
                ordered_layers[object_key] = (object_layer_key, ordered_nodes)
            canonical.append(
                (astack_key, ordered_layers, alayer_key, name, frame_start, frame_end)
            )
        canonical.sort(key=lambda item: (item[3], repr(item[0])))
        return tuple(canonical)

    def canonical_connection_key(connection):
        connection_type, source_id, destination_id, property_name = connection
        return (
            bytes(connection_type),
            int(source_id),
            int(destination_id),
            b"" if property_name is None else bytes(property_name),
        )

    def deterministic_data_from_scene(*args, **kwargs):
        scene_data = original_data_from_scene(*args, **kwargs)
        return scene_data._replace(
            animations=canonicalize_animations(scene_data.animations),
            connections=tuple(sorted(scene_data.connections, key=canonical_connection_key)),
        )

    export_fbx_bin.fbx_header_elements = deterministic_header_writer
    export_fbx_bin.fbx_data_from_scene = deterministic_data_from_scene
    fbx_utils._keys_to_uuids.clear()
    fbx_utils._uuids_to_keys.clear()
    fbx_utils._key_to_uuid = deterministic_uuid_factory
    try:
        bpy.ops.export_scene.fbx(
            filepath=str(FBX_PATH),
            use_selection=True,
            object_types={"ARMATURE"},
            apply_unit_scale=True,
            apply_scale_options="FBX_SCALE_UNITS",
            bake_space_transform=False,
            axis_forward="-Z",
            axis_up="Y",
            add_leaf_bones=False,
            primary_bone_axis="Y",
            secondary_bone_axis="X",
            use_armature_deform_only=False,
            armature_nodetype="ROOT",
            bake_anim=True,
            bake_anim_use_all_bones=True,
            bake_anim_use_nla_strips=False,
            bake_anim_use_all_actions=True,
            bake_anim_force_startend_keying=True,
            bake_anim_step=1.0,
            bake_anim_simplify_factor=0.0,
            use_custom_props=True,
            path_mode="AUTO",
            embed_textures=False,
        )
    finally:
        export_fbx_bin.fbx_header_elements = original_header_writer
        export_fbx_bin.fbx_data_from_scene = original_data_from_scene
        fbx_utils._key_to_uuid = original_uuid_factory
        fbx_utils._keys_to_uuids.clear()
        fbx_utils._uuids_to_keys.clear()
    require(FBX_PATH.is_file() and FBX_PATH.stat().st_size > 100_000, "Animation FBX export failed")


def write_manifest(
    rig_manifest: dict[str, object],
    control_action_reports: dict[str, dict[str, object]],
    baked_action_reports: dict[str, dict[str, object]],
    metrics: dict[str, object],
) -> None:
    manifest = {
        "schemaVersion": 1,
        "assetId": ASSET_ID,
        "status": "PASS_TECHNICAL_ANIMATION_PROOF_NOT_VISUAL_ACCEPTANCE",
        "integrationStatus": "SOURCE_DCC_NOT_IN_UNITY",
        "currentRestPoseStatus": "REFIT_TO_ACCEPTED_MPFB_NEUTRAL_MIDPOINT",
        "externalOrLegacyAnimationDependency": False,
        "legacyClipsImportedRetargetedOrSampled": False,
        "determinismBoundary": {
            "sparseControlDefinitionsDeterministic": True,
            "fullFrameBakeDeterministic": True,
            "fbxUsesFixedTimestampAndDeterministicUuids": True,
            "fbxExpectedByteDeterministicForIdenticalInputs": True,
            "blendBytesDeterministic": False,
            "manifestBytesDeterministic": False,
            "reason": "Blender .blend serialization can vary across identical saves; the manifest snapshots the current blend hash. The animation FBX uses fixed header time, content-derived UUIDs, a fully sampled bake, and content-sorted animation/connection containers.",
        },
        "sourceRig": {
            "assetId": rig_manifest["assetId"],
            "manifestSha256": sha256(RIG_MANIFEST),
            "blendSha256": sha256(RIG_BLEND),
            "fbxSha256": rig_manifest["outputHashes"][rig_contract.FBX_PATH.name],
            "exportedJointCount": 66,
            "restMatrices": "in source rig manifest hierarchy.bones[].restMatrix",
            "restFitRevision": rig_manifest["restFit"]["revision"],
            "restFitInputArtifactSha256": rig_manifest["restFit"]["inputArtifactSha256"],
            "restFitNeutralProposalCanonicalSha256": rig_manifest["restFit"]["neutralProposalCanonicalSha256"],
        },
        "coordinates": rig_manifest["coordinates"],
        "fps": FPS,
        "authoring": {
            "rigifyControlActions": len(control_action_reports),
            "cleanGameBakeActions": len(baked_action_reports),
            "controlActionPrefix": CONTROL_ACTION_PREFIX,
            "animatedControlNames": list(ANIMATED_CONTROLS),
            "ikSwitchControlNames": list(IK_SWITCH_CONTROLS),
            "sparseControlActionsRetainedInBlend": True,
            "allIntegerFramesBakedToGameRig": True,
            "retargetMethod": "evaluated Rigify ORG world-space delta from its rest matrix applied to the clean game rest matrix; child heads rebuilt from the game hierarchy",
            "hipsTransformSource": "Rigify animator control hips; generated ORG-spine translation is not sampled",
            "identicalEndpointStabilization": "copies only loop/transition endpoints whose sparse source-control dictionaries are exactly equal",
            "rebakedAfterRestRigRefit": True,
            "rebakeAfterAnyFutureBoundedRestRigRefit": True,
        },
        "clips": [
            {
                "name": clip.name,
                "frameStart": clip.frame_start,
                "frameEnd": clip.frame_end,
                "sampledFrames": clip.frame_end - clip.frame_start + 1,
                "durationSeconds": round((clip.frame_end - clip.frame_start) / FPS, 6),
                "loop": clip.loop,
                "category": clip.category,
                "sourceAction": control_action_reports[clip.name],
                "bakedAction": baked_action_reports[clip.name],
            }
            for clip in CLIPS
        ],
        "rootMotionContract": {
            "mode": "in_place",
            "rootBone": "Root",
            "rootTranslationMeters": [0.0, 0.0, 0.0],
            "rootRotationEulerRadians": [0.0, 0.0, 0.0],
            "hipsMayTranslate": True,
            "locomotionDirectionalReuse": "rotate actor heading for 8 directions",
        },
        "technicalMetrics": metrics,
        "exportContract": {
            "fbxArmatureObjects": 1,
            "fbxJointCount": 66,
            "fbxMeshObjects": 0,
            "fbxActions": len(baked_action_reports),
            "fbxConstraints": 0,
            "fbxControlMechanismWidgetBones": 0,
            "selectedObjectNames": [GAME_RIG_NAME],
            "fixedCreationTimestamp": FBX_CREATION_TIMESTAMP.isoformat(),
            "deterministicFbxUuidHash": "sha256(repr(exporter-key))[:8] masked to signed int64",
            "canonicalFbxAnimationElementOrdering": True,
            "canonicalFbxConnectionOrdering": True,
        },
        "sourceFiles": {
            str(Path(__file__).resolve().relative_to(PROJECT_ROOT)): sha256(Path(__file__).resolve()),
            str(VERIFIER_PATH.relative_to(PROJECT_ROOT)): sha256(VERIFIER_PATH),
            str(CLIP_CONTRACT_PATH.relative_to(PROJECT_ROOT)): sha256(CLIP_CONTRACT_PATH),
            str(PROVENANCE_PATH.relative_to(PROJECT_ROOT)): sha256(PROVENANCE_PATH),
        },
        "outputHashes": {
            SOURCE_BLEND.name: sha256(SOURCE_BLEND),
            FBX_PATH.name: sha256(FBX_PATH),
            PROVENANCE_PATH.name: sha256(PROVENANCE_PATH),
        },
        "approvalBoundary": {
            "underUnityAssets": False,
            "unityIntegrationPerformed": False,
            "mpfbBodyBound": False,
            "visualSilhouetteScanPerformed": False,
            "manualProductionAnimationAcceptance": False,
            "nextStep": "Bind the accepted MPFB body variants, then perform visual deformation, foot-contact, bow-silhouette, and body-intersection review before any Unity task.",
        },
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> None:
    validate_static_contract()
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    game_rig, control_rig, rig_manifest = load_rig_source()
    control_actions: dict[str, bpy.types.Action] = {}
    sampled_by_clip: dict[str, dict[int, dict[str, Matrix]]] = {}
    baked_actions: dict[str, bpy.types.Action] = {}
    for clip in CLIPS:
        control_action = create_control_action(control_rig, clip)
        control_actions[clip.name] = control_action
        sampled_by_clip[clip.name] = sample_clip(game_rig, control_rig, control_action, clip)
    stabilize_intentionally_identical_endpoints(sampled_by_clip)
    for clip in CLIPS:
        baked_actions[clip.name] = create_baked_action(game_rig, clip, sampled_by_clip[clip.name])
    metrics, _samples = collect_action_metrics(game_rig, baked_actions)
    print("PRELIMINARY_ANIMATION_METRICS", json.dumps(metrics, sort_keys=True))
    validate_metrics(metrics)
    control_action_reports = {name: action_curve_report(action) for name, action in control_actions.items()}
    baked_action_reports = {name: action_curve_report(action) for name, action in baked_actions.items()}
    write_clip_contract(metrics)
    write_provenance(rig_manifest)
    save_source_blend(game_rig, control_rig)
    prepare_clean_export_scene(game_rig, control_actions, baked_actions)
    export_animation_fbx(game_rig)
    write_manifest(rig_manifest, control_action_reports, baked_action_reports, metrics)
    print("SESSION_HUMANOID_ANIMATION_PROOF_V1_BUILD_OK")
    print("CLIPS", len(CLIPS), "SOURCE_ACTIONS", len(control_actions), "BAKED_ACTIONS", len(baked_actions))
    print("FBX", FBX_PATH)
    print("SOURCE_BLEND", SOURCE_BLEND)
    print("BOW_METRICS", json.dumps(metrics["hunterBowSequence"], sort_keys=True))


if __name__ == "__main__":
    main()
