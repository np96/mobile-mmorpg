#!/usr/bin/env python3
"""Build the first-party Session Humanoid Rig V1 outside Unity Assets.

The stable game/export hierarchy is created numerically and never imports,
appends, inspects, or samples a legacy armature, model, or animation.  The
editable Blender source additionally contains Blender 5.2's bundled Rigify Human
metarig and its generated animator controls.  Those DCC-only helpers drive the
project-owned game rig through explicit constraints and are excluded from the
skeleton-only FBX by selection.
"""

from __future__ import annotations

from dataclasses import dataclass
import datetime
import hashlib
import json
from pathlib import Path
import random
import sys

import bpy
from mathutils import Matrix, Vector


PROJECT_ROOT = Path(__file__).resolve().parents[3]
SOURCE_DIR = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidRigV1"
OUTPUT_DIR = SOURCE_DIR / "Outputs"
BLEND_PATH = SOURCE_DIR / "SessionHumanoidRigV1.blend"
FBX_PATH = OUTPUT_DIR / "SessionHumanoidRigV1.fbx"
MANIFEST_PATH = OUTPUT_DIR / "SessionHumanoidRigV1.manifest.json"
PROVENANCE_PATH = OUTPUT_DIR / "SessionHumanoidRigV1.PROVENANCE.md"
CONTRACT_PATH = SOURCE_DIR / "SessionHumanoidRigV1.CONTRACT.md"
VERIFIER_PATH = Path(__file__).resolve().with_name("verify_session_humanoid_rig_v1.py")
REST_FIT_ARTIFACT_PATH = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/SessionHumanoidBaseV1/Outputs/SessionHumanoidBaseV1_RigFitLandmarks.json"
)
REST_FIT_BUILD_MANIFEST_PATH = REST_FIT_ARTIFACT_PATH.with_name("SessionHumanoidBaseV1_BuildManifest.json")
# This is the retained, body-authoritative landmark input.  The body intake
# report is an audit output and its originating hash is retained inside this
# artifact; the game rig never parses an MPFB armature or body mesh directly.
EXPECTED_REST_FIT_ARTIFACT_SHA256 = "e55a921a1f3f18aa1c08bcbb98e31317a6c6ab2fc710fee36ea37c2c54636e79"
EXPECTED_REST_FIT_PROPOSAL_SHA256 = "032f1cc8bf971d7dc09cfd372ff083e8c292297b027e13a725acf0d1ea959997"
EXPECTED_REST_FIT_BUILD_MANIFEST_SHA256 = "97c6828ed971ee459dff1c1d66275029a3219ce175c53a8be8f6febcea01d4c2"
EXPECTED_REST_FIT_REPORT_SHA256 = "30852bdb824f7c93789955bb5749d663ff20c5850b7a146e28f514da5bdf5f95"
REST_FIT_REVISION = "MPFB_NEUTRAL_MIDPOINT_2026-08-20"

ASSET_ID = "session-humanoid-rig-v1"
RIG_OBJECT_NAME = "SessionHumanoidRigV1"
ARMATURE_DATA_NAME = "SessionHumanoidRigV1_Armature"
BIND_PROBE_NAME = "SessionHumanoidRigV1_BindProbe_NotExported"
RIGIFY_META_NAME = "SessionHumanoidRigV1_RigifyHumanMeta_NotExported"
RIGIFY_CONTROL_NAME = "SessionHumanoidRigV1_RigifyControls_NotExported"
RIGIFY_WIDGET_COLLECTION_NAME = "SessionHumanoidRigV1_RigifyWidgets_NotExported"
RIGIFY_GENERATED_ID = "srhvrigifyv1dcc0"
FBX_CREATION_TIMESTAMP = datetime.datetime(2026, 8, 20, 0, 0, 0)
REFERENCE_HEIGHT_METERS = 1.780
BLENDER_FORWARD = "-Y"
BLENDER_UP = "+Z"
UNITY_FORWARD = "+Z"
UNITY_UP = "+Y"

FORBIDDEN_BONE_TOKENS = (
    "CTRL", "CONTROL", "MCH", "MECH", "ORG", "WGT", "TWEAK", "DEF-",
)


@dataclass(frozen=True)
class BoneSpec:
    name: str
    parent: str | None
    head: tuple[float, float, float]
    tail: tuple[float, float, float]
    deform: bool
    connected: bool
    role: str
    roll_reference: tuple[float, float, float]
    humanoid_slot: str | None = None


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def canonical_json_sha256(value: object) -> str:
    payload = json.dumps(value, sort_keys=True, separators=(",", ":")).encode("utf-8")
    return hashlib.sha256(payload).hexdigest()


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def vector_tuple(value: Vector, digits: int = 6) -> list[float]:
    return [round(component, digits) for component in value]


def matrix_values(value: Matrix, digits: int = 8) -> list[list[float]]:
    return [[round(component, digits) for component in row] for row in value]


def side_name(side: str, stem: str) -> str:
    return side + stem


def load_rest_fit_artifact() -> dict[str, object]:
    require(REST_FIT_ARTIFACT_PATH.is_file(), f"Accepted body-fit artifact is missing: {REST_FIT_ARTIFACT_PATH}")
    require(
        sha256(REST_FIT_ARTIFACT_PATH) == EXPECTED_REST_FIT_ARTIFACT_SHA256,
        "Accepted body-fit artifact hash changed",
    )
    artifact = json.loads(REST_FIT_ARTIFACT_PATH.read_text(encoding="utf-8"))
    require(artifact["schemaVersion"] == 1, "Unexpected body-fit artifact schema")
    require(artifact["asset"] == "SessionHumanoidBaseV1", "Unexpected body-fit artifact asset")
    require(
        artifact["status"] == "BODY_INTAKE_ACCEPTED_RIG_REFIT_INPUT_ONLY_NOT_A_PRODUCTION_EXPORT",
        "Body-fit artifact is not an accepted rig-refit input",
    )
    require(artifact["sourceBoundary"]["assetLicense"] == "CC0-1.0", "Body-fit source license changed")
    require(
        "provisional SessionHumanoidRigV1 FBX/manifest path or hash" in artifact["sourceBoundary"]["excludedByDesign"],
        "Body-fit source boundary no longer excludes the provisional project rig",
    )
    proposal = artifact["neutralMidpointProposal"]
    require(len(proposal["landmarks"]) == 53, "Neutral rest-fit landmark count changed")
    require(
        canonical_json_sha256(proposal) == EXPECTED_REST_FIT_PROPOSAL_SHA256,
        "Neutral rest-fit proposal hash changed",
    )
    require(proposal["orientation"] == "Blender mesh local: meters, +Z up, -Y forward, Left is +X", "Rest-fit axes changed")
    for variant in ("Masculine", "Feminine"):
        fit = artifact["variants"][variant]
        require(not fit["stockPoseModified"] and fit["localPoseCorrection"] is None, f"{variant} rest fit is not the stock pose")
        require(fit["normalizedLandmarkMirrorError"]["maximumErrorMeters"] <= 0.002, f"{variant} landmark symmetry failed")
        require(fit["rigFitAgainstMpfbJointCubes"]["maximumCubeEndpointErrorMeters"] <= 0.001, f"{variant} helper fit failed")
        containment = fit["landmarkSkinContainment"]
        require(containment["status"] == "PASS" and not containment["outsideBeyondTolerance"], f"{variant} joint containment failed")
    require(
        artifact["neutralMidpointProposalCanonicalSha256"] == EXPECTED_REST_FIT_PROPOSAL_SHA256,
        "Body-fit artifact's self-declared proposal hash changed",
    )
    require(REST_FIT_BUILD_MANIFEST_PATH.is_file(), "Body-fit per-build provenance manifest is missing")
    require(
        sha256(REST_FIT_BUILD_MANIFEST_PATH) == EXPECTED_REST_FIT_BUILD_MANIFEST_SHA256,
        "Body-fit per-build provenance manifest hash changed",
    )
    build_manifest = json.loads(REST_FIT_BUILD_MANIFEST_PATH.read_text(encoding="utf-8"))
    require(
        build_manifest["stableRigFitLandmarks"]["sha256"] == EXPECTED_REST_FIT_ARTIFACT_SHA256,
        "Body-fit provenance points at a different stable artifact",
    )
    require(
        build_manifest["stableRigFitLandmarks"]["neutralMidpointProposalCanonicalSha256"]
        == EXPECTED_REST_FIT_PROPOSAL_SHA256,
        "Body-fit provenance points at a different neutral proposal",
    )
    require(
        build_manifest["bodyIntakeReport"]["sha256"] == EXPECTED_REST_FIT_REPORT_SHA256,
        "Body-fit provenance points at a different intake report",
    )
    return artifact, build_manifest


REST_FIT_ARTIFACT, REST_FIT_BUILD_MANIFEST = load_rest_fit_artifact()
REST_FIT_PROPOSAL = REST_FIT_ARTIFACT["neutralMidpointProposal"]


def build_bone_specs() -> tuple[BoneSpec, ...]:
    axial_roll = (0.0, -1.0, 0.0)
    lateral_roll = (0.0, 0.0, 1.0)
    forward_roll = (0.0, 0.0, 1.0)
    landmarks = REST_FIT_PROPOSAL["landmarks"]

    def point(bone_name: str, endpoint: str) -> Vector:
        return Vector(landmarks[bone_name][endpoint])

    def values(vector: Vector) -> tuple[float, float, float]:
        return tuple(float(component) for component in vector)

    def joint(first_bone: str, second_bone: str) -> Vector:
        # Reuse one exact coordinate for every connected pair, even if a future
        # accepted measurement differs by sub-millimeter solver noise.
        return (point(first_bone, "tail") + point(second_bone, "head")) * 0.5

    hips_head = point("pelvis", "head")
    hips_spine = joint("pelvis", "spine_01")
    spine_chest = joint("spine_01", "spine_02")
    chest_upper = joint("spine_02", "spine_03")
    upper_chest_tail = point("spine_03", "tail")
    neck_head = point("neck_01", "head")
    neck_head_joint = joint("neck_01", "head")
    head_tail = point("head", "tail")

    # MPFB's compact game-engine spine intentionally leaves a non-bone torso
    # interval between spine_03.tail and neck_01.head. Preserve both measured
    # endpoints exactly: Unity Humanoid hierarchy does not require display bones
    # to be physically connected. The DCC-only Rigify spine bridges this interval
    # separately so animator controls remain a conventional connected chain.
    specs: list[BoneSpec] = [
        BoneSpec("Root", None, (0.0, 0.0, 0.0), (0.0, 0.0, 0.120), False, False, "game_root", axial_roll),
        BoneSpec("Hips", "Root", values(hips_head), values(hips_spine), True, False, "humanoid", axial_roll, "Hips"),
        BoneSpec("Spine", "Hips", values(hips_spine), values(spine_chest), True, True, "humanoid", axial_roll, "Spine"),
        BoneSpec("Chest", "Spine", values(spine_chest), values(chest_upper), True, True, "humanoid", axial_roll, "Chest"),
        BoneSpec("UpperChest", "Chest", values(chest_upper), values(upper_chest_tail), True, True, "humanoid", axial_roll, "UpperChest"),
        BoneSpec("Neck", "UpperChest", values(neck_head), values(neck_head_joint), True, False, "humanoid", axial_roll, "Neck"),
        BoneSpec("Head", "Neck", values(neck_head_joint), values(head_tail), True, True, "humanoid", axial_roll, "Head"),
        BoneSpec("Jaw", "Head", values(neck_head_joint + Vector((0.0, -0.012, 0.095))), values(neck_head_joint + Vector((0.0, -0.070, 0.022))), True, False, "humanoid", (1.0, 0.0, 0.0), "Jaw"),
        BoneSpec("LeftEye", "Head", values(neck_head_joint + Vector((0.032, -0.070, 0.110))), values(neck_head_joint + Vector((0.032, -0.115, 0.110))), True, False, "humanoid", forward_roll, "LeftEye"),
        BoneSpec("RightEye", "Head", values(neck_head_joint + Vector((-0.032, -0.070, 0.110))), values(neck_head_joint + Vector((-0.032, -0.115, 0.110))), True, False, "humanoid", forward_roll, "RightEye"),
    ]

    finger_stems = {
        "Thumb": "thumb",
        "Index": "index",
        "Middle": "middle",
        "Ring": "ring",
        "Little": "pinky",
    }
    for side, suffix in (("Left", "l"), ("Right", "r")):
        clavicle = f"clavicle_{suffix}"
        upperarm = f"upperarm_{suffix}"
        lowerarm = f"lowerarm_{suffix}"
        hand = f"hand_{suffix}"
        shoulder_joint = joint(clavicle, upperarm)
        elbow_joint = joint(upperarm, lowerarm)
        wrist_joint = joint(lowerarm, hand)
        specs.extend((
            BoneSpec(side_name(side, "Shoulder"), "UpperChest", values(point(clavicle, "head")), values(shoulder_joint), True, False, "humanoid", lateral_roll, side_name(side, "Shoulder")),
            BoneSpec(side_name(side, "UpperArm"), side_name(side, "Shoulder"), values(shoulder_joint), values(elbow_joint), True, True, "humanoid", lateral_roll, side_name(side, "UpperArm")),
            BoneSpec(side_name(side, "LowerArm"), side_name(side, "UpperArm"), values(elbow_joint), values(wrist_joint), True, True, "humanoid", lateral_roll, side_name(side, "LowerArm")),
            BoneSpec(side_name(side, "Hand"), side_name(side, "LowerArm"), values(wrist_joint), values(point(hand, "tail")), True, True, "humanoid", lateral_roll, side_name(side, "Hand")),
        ))

        for finger, mpfb_stem in finger_stems.items():
            source_names = [f"{mpfb_stem}_{index:02d}_{suffix}" for index in (1, 2, 3)]
            finger_points = [point(source_names[0], "head")]
            finger_points.extend(joint(source_names[index], source_names[index + 1]) for index in (0, 1))
            finger_points.append(point(source_names[-1], "tail"))
            parent = side_name(side, "Hand")
            for segment, start, end in zip(
                ("Proximal", "Intermediate", "Distal"),
                finger_points[:-1],
                finger_points[1:],
            ):
                name = side_name(side, finger + segment)
                specs.append(BoneSpec(
                    name,
                    parent,
                    values(start),
                    values(end),
                    True,
                    segment != "Proximal",
                    "humanoid",
                    lateral_roll,
                    name,
                ))
                parent = name

        thigh = f"thigh_{suffix}"
        calf = f"calf_{suffix}"
        foot = f"foot_{suffix}"
        ball = f"ball_{suffix}"
        knee_joint = joint(thigh, calf)
        ankle_joint = joint(calf, foot)
        ball_joint = joint(foot, ball)
        specs.extend((
            BoneSpec(side_name(side, "UpperLeg"), "Hips", values(point(thigh, "head")), values(knee_joint), True, False, "humanoid", axial_roll, side_name(side, "UpperLeg")),
            BoneSpec(side_name(side, "LowerLeg"), side_name(side, "UpperLeg"), values(knee_joint), values(ankle_joint), True, True, "humanoid", axial_roll, side_name(side, "LowerLeg")),
            BoneSpec(side_name(side, "Foot"), side_name(side, "LowerLeg"), values(ankle_joint), values(ball_joint), True, True, "humanoid", forward_roll, side_name(side, "Foot")),
            BoneSpec(side_name(side, "Toes"), side_name(side, "Foot"), values(ball_joint), values(point(ball, "tail")), True, True, "humanoid", forward_roll, side_name(side, "Toes")),
        ))

    pelvis_socket = hips_head + Vector((0.0, -0.040, 0.030))
    upper_chest_axis = neck_head - chest_upper
    chest_anchor = chest_upper + (upper_chest_axis * 0.70)
    camera_anchor = chest_upper + (upper_chest_axis * 0.82)
    left_weapon = point("hand_l", "tail")
    right_weapon = point("hand_r", "tail")
    left_hand_direction = (point("hand_l", "tail") - joint("lowerarm_l", "hand_l")).normalized()
    right_hand_direction = (point("hand_r", "tail") - joint("lowerarm_r", "hand_r")).normalized()
    left_foot_tip = point("ball_l", "tail")
    right_foot_tip = point("ball_r", "tail")
    specs.extend((
        BoneSpec("Socket_GroundFX", "Root", (0.0, 0.0, 0.020), (0.0, -0.100, 0.020), False, False, "socket", forward_roll),
        BoneSpec("Socket_PelvisFX", "Hips", values(pelvis_socket), values(pelvis_socket + Vector((0.0, -0.090, 0.0))), False, False, "socket", forward_roll),
        BoneSpec("Socket_ChestFX", "UpperChest", values(chest_anchor + Vector((0.0, -0.105, 0.0))), values(chest_anchor + Vector((0.0, -0.195, 0.0))), False, False, "socket", forward_roll),
        BoneSpec("Socket_Back", "UpperChest", values(chest_anchor + Vector((0.0, 0.105, 0.010))), values(chest_anchor + Vector((0.0, 0.195, 0.010))), False, False, "socket", forward_roll),
        BoneSpec("Socket_CameraAim", "UpperChest", values(camera_anchor + Vector((0.0, -0.020, 0.0))), values(camera_anchor + Vector((0.0, -0.150, 0.0))), False, False, "socket", forward_roll),
        BoneSpec("Socket_HeadFX", "Head", (0.0, 0.0, 1.780), (0.0, 0.0, 1.880), False, False, "socket", axial_roll),
        BoneSpec("Socket_Weapon_L", "LeftHand", values(left_weapon), values(left_weapon + (left_hand_direction * 0.080)), False, False, "socket", lateral_roll),
        BoneSpec("Socket_Weapon_R", "RightHand", values(right_weapon), values(right_weapon + (right_hand_direction * 0.080)), False, False, "socket", lateral_roll),
        BoneSpec("Socket_FootFX_L", "LeftToes", values(left_foot_tip), values(left_foot_tip + Vector((0.0, -0.090, -0.005))), False, False, "socket", forward_roll),
        BoneSpec("Socket_FootFX_R", "RightToes", values(right_foot_tip), values(right_foot_tip + Vector((0.0, -0.090, -0.005))), False, False, "socket", forward_roll),
    ))
    return tuple(specs)


BONE_SPECS = build_bone_specs()
SPEC_BY_NAME = {spec.name: spec for spec in BONE_SPECS}
HUMANOID_MAP = {
    spec.humanoid_slot: spec.name
    for spec in BONE_SPECS
    if spec.humanoid_slot is not None
}


def build_dcc_driver_targets() -> dict[str, str]:
    """Map stable game bones to the generated Rigify original bones.

    Rigify's ORG bones are the evaluated bridge between its animator-facing FK/IK
    controls and the clean game hierarchy.  Sockets are intentionally omitted:
    they inherit from their already-driven game parents and retain stable local
    offsets.
    """

    targets = {
        "Root": "root",
        # Pelvis translation/rotation comes from the animator-facing Rigify
        # hips control; generated ORG-spine translation includes spline-solver
        # offsets and is not a stable game-root source.
        "Hips": "hips",
        "Spine": "ORG-spine.001",
        "Chest": "ORG-spine.002",
        "UpperChest": "ORG-spine.003",
        "Neck": "ORG-spine.005",
        "Head": "ORG-spine.006",
        "Jaw": "ORG-jaw",
        "LeftEye": "ORG-eye.L",
        "RightEye": "ORG-eye.R",
    }
    finger_stems = {
        "Thumb": "thumb",
        "Index": "f_index",
        "Middle": "f_middle",
        "Ring": "f_ring",
        "Little": "f_pinky",
    }
    for side, suffix in (("Left", "L"), ("Right", "R")):
        targets.update({
            side + "Shoulder": f"ORG-shoulder.{suffix}",
            side + "UpperArm": f"ORG-upper_arm.{suffix}",
            side + "LowerArm": f"ORG-forearm.{suffix}",
            side + "Hand": f"ORG-hand.{suffix}",
            side + "UpperLeg": f"ORG-thigh.{suffix}",
            side + "LowerLeg": f"ORG-shin.{suffix}",
            side + "Foot": f"ORG-foot.{suffix}",
            side + "Toes": f"ORG-toe.{suffix}",
        })
        for game_stem, rigify_stem in finger_stems.items():
            for segment, index in zip(("Proximal", "Intermediate", "Distal"), (1, 2, 3)):
                targets[side + game_stem + segment] = f"ORG-{rigify_stem}.{index:02d}.{suffix}"
    return targets


DCC_DRIVER_TARGETS = build_dcc_driver_targets()


def reset_scene() -> None:
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.context.preferences.filepaths.save_version = 0
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1.0
    scene.render.fps = 30


def validate_static_contract() -> None:
    names = [spec.name for spec in BONE_SPECS]
    require(len(names) == len(set(names)), "Rig contract contains duplicate bone names")
    require(len(HUMANOID_MAP) == 55, f"Expected 55 Unity Humanoid slots, found {len(HUMANOID_MAP)}")
    require(len(BONE_SPECS) == 66, f"Expected 66 exported bones, found {len(BONE_SPECS)}")
    require(sum(spec.deform for spec in BONE_SPECS) == 55, "Expected 55 deform bones")
    require(sum(spec.role == "socket" for spec in BONE_SPECS) == 10, "Expected ten sockets")
    for spec in BONE_SPECS:
        require(spec.parent is None or spec.parent in SPEC_BY_NAME, f"Missing parent for {spec.name}")
        require(Vector(spec.tail) != Vector(spec.head), f"Zero-length bone {spec.name}")
        upper_name = spec.name.upper()
        require(not any(token in upper_name for token in FORBIDDEN_BONE_TOKENS), f"Control/mechanism token in {spec.name}")
        if spec.connected:
            require(Vector(spec.head) == Vector(SPEC_BY_NAME[spec.parent].tail), f"Connected joint mismatch for {spec.name}")
    require(SPEC_BY_NAME["Socket_HeadFX"].head[2] == REFERENCE_HEIGHT_METERS, "Head FX socket no longer marks the 1.78 m body crown")
    for side, other in (("Left", "Right"),):
        for stem in ("Shoulder", "UpperArm", "LowerArm", "Hand", "UpperLeg", "LowerLeg", "Foot", "Toes"):
            left = Vector(SPEC_BY_NAME[side + stem].head)
            right = Vector(SPEC_BY_NAME[other + stem].head)
            require((left - Vector((-right.x, right.y, right.z))).length <= 1e-8, f"Rest fit is not symmetric for {stem}")


def create_armature() -> bpy.types.Object:
    armature = bpy.data.armatures.new(ARMATURE_DATA_NAME)
    rig = bpy.data.objects.new(RIG_OBJECT_NAME, armature)
    bpy.context.scene.collection.objects.link(rig)
    rig.location = Vector((0.0, 0.0, 0.0))
    rig.rotation_euler = (0.0, 0.0, 0.0)
    rig.scale = Vector((1.0, 1.0, 1.0))
    rig.show_in_front = True
    rig.data.display_type = "OCTAHEDRAL"
    rig["assetId"] = ASSET_ID
    rig["referenceHeightMeters"] = REFERENCE_HEIGHT_METERS
    rig["blenderForward"] = BLENDER_FORWARD
    rig["unityForward"] = UNITY_FORWARD

    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    created: dict[str, bpy.types.EditBone] = {}
    for spec in BONE_SPECS:
        bone = armature.edit_bones.new(spec.name)
        bone.head = Vector(spec.head)
        bone.tail = Vector(spec.tail)
        if spec.parent is not None:
            bone.parent = created[spec.parent]
            bone.use_connect = spec.connected
        bone.use_deform = spec.deform
        bone.align_roll(Vector(spec.roll_reference))
        created[spec.name] = bone
    bpy.ops.object.mode_set(mode="OBJECT")

    for spec in BONE_SPECS:
        bone = armature.bones[spec.name]
        bone.use_deform = spec.deform
        bone["sessionRole"] = spec.role
        if spec.humanoid_slot is not None:
            bone["unityHumanoidSlot"] = spec.humanoid_slot
        if spec.role == "socket":
            bone["stableSocket"] = True
    return rig


def fit_rigify_human_metarig(metarig: bpy.types.Object) -> None:
    """Fit Blender's bundled Human metarig to the locked numeric T-pose.

    The remaining facial helper bones are uniformly normalized from the bundled
    profile.  Every body, eye, jaw, hand, and finger bone that drives the game
    rig is then placed deliberately from this project's numeric contract.
    """

    bpy.context.view_layer.objects.active = metarig
    bpy.ops.object.mode_set(mode="EDIT")
    edit_bones = metarig.data.edit_bones
    required = {
        "spine", "spine.001", "spine.002", "spine.003", "spine.004",
        "spine.005", "spine.006", "jaw", "eye.L", "eye.R",
    }
    require(required.issubset(edit_bones.keys()), "Bundled Rigify Human metarig contract changed")

    connected = {bone.name: bone.use_connect for bone in edit_bones}
    for bone in edit_bones:
        bone.use_connect = False

    original_crown = edit_bones["spine.006"].tail.z
    require(original_crown > 1.0, "Rigify Human metarig has an invalid crown landmark")
    normalization = REFERENCE_HEIGHT_METERS / original_crown
    for bone in edit_bones:
        bone.head = bone.head * normalization
        bone.tail = bone.tail * normalization

    def place(name: str, head, tail, roll_reference) -> None:
        bone = edit_bones[name]
        bone.head = Vector(head)
        bone.tail = Vector(tail)
        bone.align_roll(Vector(roll_reference))

    def place_from_game(meta_name: str, game_name: str) -> None:
        spec = SPEC_BY_NAME[game_name]
        place(meta_name, spec.head, spec.tail, spec.roll_reference)

    # Rigify's super-head uses two neck segments followed by the head.  Split the
    # single exported neck only in the DCC layer; the stable game contract does
    # not gain another joint.
    place_from_game("spine", "Hips")
    place_from_game("spine.001", "Spine")
    place_from_game("spine.002", "Chest")
    upper_chest = SPEC_BY_NAME["UpperChest"]
    neck = SPEC_BY_NAME["Neck"]
    # Rigify's super-spine requires a connected chain. Bridge only its DCC
    # spine.003 display bone across the deliberate MPFB spine_03/neck_01
    # interval; the clean game UpperChest and Neck retain the measured tails and
    # disconnected parent relation recorded by the export contract.
    place("spine.003", upper_chest.head, neck.head, upper_chest.roll_reference)
    neck_midpoint = (Vector(neck.head) + Vector(neck.tail)) * 0.5
    place("spine.004", neck.head, neck_midpoint, neck.roll_reference)
    place("spine.005", neck_midpoint, neck.tail, neck.roll_reference)
    place_from_game("spine.006", "Head")
    place_from_game("jaw", "Jaw")
    place_from_game("eye.L", "LeftEye")
    place_from_game("eye.R", "RightEye")

    finger_meta_stems = {
        "Thumb": "thumb",
        "Index": "f_index",
        "Middle": "f_middle",
        "Ring": "f_ring",
        "Little": "f_pinky",
    }
    palm_for_finger = {
        "Index": "palm.01",
        "Middle": "palm.02",
        "Ring": "palm.03",
        "Little": "palm.04",
    }
    for side, suffix in (("Left", "L"), ("Right", "R")):
        place_from_game(f"shoulder.{suffix}", side + "Shoulder")
        # Rigify's IK generator needs a non-zero pole plane.  A 15 mm DCC-only
        # forward bias at elbow and knee supplies it; the separately authored
        # game/export rig remains an exact straight-limb T-pose.
        upper_arm = SPEC_BY_NAME[side + "UpperArm"]
        lower_arm = SPEC_BY_NAME[side + "LowerArm"]
        elbow = Vector(upper_arm.tail)
        elbow.y -= 0.015
        place(f"upper_arm.{suffix}", upper_arm.head, elbow, upper_arm.roll_reference)
        place(f"forearm.{suffix}", elbow, lower_arm.tail, lower_arm.roll_reference)
        place_from_game(f"hand.{suffix}", side + "Hand")
        upper_leg = SPEC_BY_NAME[side + "UpperLeg"]
        lower_leg = SPEC_BY_NAME[side + "LowerLeg"]
        knee = Vector(upper_leg.tail)
        knee.y -= 0.015
        place(f"thigh.{suffix}", upper_leg.head, knee, upper_leg.roll_reference)
        place(f"shin.{suffix}", knee, lower_leg.tail, lower_leg.roll_reference)
        place_from_game(f"foot.{suffix}", side + "Foot")
        place_from_game(f"toe.{suffix}", side + "Toes")

        for game_finger, palm_stem in palm_for_finger.items():
            root = Vector(SPEC_BY_NAME[side + game_finger + "Proximal"].head)
            hand_head = Vector(SPEC_BY_NAME[side + "Hand"].head)
            palm_head = hand_head.lerp(root, 0.35)
            place(
                f"{palm_stem}.{suffix}",
                palm_head,
                root,
                (0.0, 0.0, 1.0),
            )

        for game_finger, meta_stem in finger_meta_stems.items():
            for segment, index in zip(("Proximal", "Intermediate", "Distal"), (1, 2, 3)):
                place_from_game(
                    f"{meta_stem}.{index:02d}.{suffix}",
                    side + game_finger + segment,
                )

    # Restoring connectivity only snaps heads to already-matching parent tails.
    for name, use_connect in connected.items():
        edit_bones[name].use_connect = use_connect
    bpy.ops.object.mode_set(mode="OBJECT")

    metarig.location = Vector((0.0, 0.0, 0.0))
    metarig.rotation_euler = (0.0, 0.0, 0.0)
    metarig.scale = Vector((1.0, 1.0, 1.0))


def add_dcc_driver_constraints(game_rig: bpy.types.Object, control_rig: bpy.types.Object) -> int:
    require(set(DCC_DRIVER_TARGETS) == {"Root", *HUMANOID_MAP.values()}, "DCC target map must cover Root and all 55 Humanoid bones")
    require(
        set(DCC_DRIVER_TARGETS.values()).issubset(control_rig.pose.bones.keys()),
        "Generated Rigify control rig is missing a required ORG target",
    )
    constraint_count = 0
    for game_name, target_name in DCC_DRIVER_TARGETS.items():
        pose_bone = game_rig.pose.bones[game_name]
        if game_name in {"Root", "Hips"}:
            location = pose_bone.constraints.new("COPY_LOCATION")
            location.name = "DCC_Rigify_CopyLocation"
            location.target = control_rig
            location.subtarget = target_name
            location.owner_space = "LOCAL"
            location.target_space = "LOCAL_OWNER_ORIENT"
            location.use_offset = False
            constraint_count += 1
        rotation = pose_bone.constraints.new("COPY_ROTATION")
        rotation.name = "DCC_Rigify_CopyRotation"
        rotation.target = control_rig
        rotation.subtarget = target_name
        rotation.owner_space = "LOCAL"
        # Rigify ORG rolls intentionally differ from conventional export-bone
        # rolls. LOCAL_OWNER_ORIENT converts the target local motion into the
        # owner bone's rest orientation instead of copying quaternion numbers.
        rotation.target_space = "LOCAL_OWNER_ORIENT"
        rotation.mix_mode = "REPLACE"
        constraint_count += 1
    return constraint_count


def create_rigify_authoring_layer(game_rig: bpy.types.Object) -> tuple[bpy.types.Object, bpy.types.Object, tuple[bpy.types.Object, ...], dict[str, object]]:
    """Create the standard animator-facing layer retained only in the .blend."""

    # Rigify uses Python's random module for generated rig IDs and bone-collection
    # UIDs.  Pin it so the authoring graph is structurally reproducible.
    random.seed("session-humanoid-rig-v1-rigify-blender-5.2")
    result = bpy.ops.preferences.addon_enable(module="rigify")
    require("FINISHED" in result, "Could not enable Blender 5.2 bundled Rigify")
    require(hasattr(bpy.ops.object, "armature_human_metarig_add"), "Rigify Human metarig operator is unavailable")

    bpy.ops.object.select_all(action="DESELECT")
    result = bpy.ops.object.armature_human_metarig_add()
    require("FINISHED" in result, "Could not create bundled Rigify Human metarig")
    metarig = bpy.context.object
    require(metarig is not None and metarig.type == "ARMATURE", "Rigify metarig creation returned no armature")
    metarig.name = RIGIFY_META_NAME
    metarig.data.name = RIGIFY_META_NAME + "_Armature"
    metarig["sessionRole"] = "rigify_human_metarig_not_exported"
    metarig["exported"] = False
    metarig["drivesGameRig"] = RIG_OBJECT_NAME
    fit_rigify_human_metarig(metarig)

    bpy.ops.object.select_all(action="DESELECT")
    metarig.hide_set(False)
    metarig.select_set(True)
    bpy.context.view_layer.objects.active = metarig
    bpy.ops.object.mode_set(mode="POSE")
    # Rigify normally mixes wall-clock time into its generated rig ID.  Override
    # only that ID factory during generation so repeated builds retain the same
    # driver/UI identifiers; restore the bundled function immediately afterward.
    import rigify.base_generate as rigify_base_generate
    original_random_id = rigify_base_generate.random_id
    rigify_base_generate.random_id = lambda length=8: (RIGIFY_GENERATED_ID + ("0" * length))[:length]
    try:
        result = bpy.ops.pose.rigify_generate()
    finally:
        rigify_base_generate.random_id = original_random_id
    require("FINISHED" in result, "Rigify Human control generation failed")
    control_rig = bpy.context.object
    require(control_rig is not None and control_rig.type == "ARMATURE", "Rigify generation returned no control rig")
    if control_rig.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")
    control_rig.name = RIGIFY_CONTROL_NAME
    control_rig.data.name = RIGIFY_CONTROL_NAME + "_Armature"
    control_rig["sessionRole"] = "rigify_generated_controls_not_exported"
    control_rig["exported"] = False
    control_rig["drivesGameRig"] = RIG_OBJECT_NAME
    control_rig["sourceMetarig"] = RIGIFY_META_NAME
    control_rig.show_in_front = True
    control_rig.hide_render = True
    require(str(control_rig.data.get("rig_id", "")) == RIGIFY_GENERATED_ID, "Rigify generated a non-deterministic rig id")

    widgets = tuple(
        obj for obj in bpy.context.scene.objects
        if obj.type == "MESH" and obj.name.startswith("WGT-")
    )
    require(widgets, "Rigify generated no animator control widgets")
    for widget in widgets:
        widget["sessionRole"] = "rigify_widget_not_exported"
        widget["exported"] = False
        widget.hide_render = True
    widget_collections = {
        collection
        for widget in widgets
        for collection in widget.users_collection
    }
    if len(widget_collections) == 1:
        next(iter(widget_collections)).name = RIGIFY_WIDGET_COLLECTION_NAME

    constraint_count = add_dcc_driver_constraints(game_rig, control_rig)
    metarig.hide_render = True
    metarig.hide_set(True)
    metarig.select_set(False)
    control_rig.hide_set(False)
    control_rig.select_set(False)
    bpy.context.view_layer.update()

    report = {
        "profile": "Blender 5.2 bundled Rigify Human metarig",
        "deterministicRandomSeed": "session-humanoid-rig-v1-rigify-blender-5.2",
        "metarigObject": metarig.name,
        "metarigBoneCount": len(metarig.data.bones),
        "generatedControlObject": control_rig.name,
        "generatedControlBoneCount": len(control_rig.data.bones),
        "generatedRigId": str(control_rig.data.get("rig_id", "")),
        "widgetObjectCount": len(widgets),
        "driverConstraintCount": constraint_count,
        "drivenGameBoneCount": len(DCC_DRIVER_TARGETS),
        "drivenHumanoidBoneCount": len(HUMANOID_MAP),
        "driverOwnerSpace": "LOCAL",
        "driverTargetSpace": "LOCAL_OWNER_ORIENT",
        "hipsDriverTarget": "hips",
        "socketDriveMode": "inherit from driven game parents",
        "metarigIkPoleBiasMeters": 0.015,
        "gameExportPoseHasPoleBias": False,
        "targetMap": DCC_DRIVER_TARGETS,
        "metarigExported": False,
        "generatedControlsExported": False,
        "widgetsExported": False,
    }
    return metarig, control_rig, widgets, report


def validate_dcc_authoring_layer(
    game_rig: bpy.types.Object,
    metarig: bpy.types.Object,
    control_rig: bpy.types.Object,
    widgets: tuple[bpy.types.Object, ...],
    report: dict[str, object],
) -> None:
    require(not bpy.data.libraries, "Source blend contains a linked or appended library")
    require(metarig.name == RIGIFY_META_NAME, "Rigify metarig name changed")
    require(control_rig.name == RIGIFY_CONTROL_NAME, "Rigify control-rig name changed")
    require(metarig.get("exported") is False, "Rigify metarig is marked exportable")
    require(control_rig.get("exported") is False, "Rigify controls are marked exportable")
    for obj in (metarig, control_rig):
        require(obj.parent is None, f"{obj.name} must remain a scene root")
        require(obj.location.length <= 1e-8, f"{obj.name} location is not identity")
        require(max(abs(value) for value in obj.rotation_euler) <= 1e-8, f"{obj.name} rotation is not identity")
        require(max(abs(value - 1.0) for value in obj.scale) <= 1e-8, f"{obj.name} scale is not identity")
    require(len(metarig.data.bones) >= 150, "Rigify Human metarig is unexpectedly incomplete")
    require(len(control_rig.data.bones) >= 600, "Generated Rigify Human controls are unexpectedly incomplete")
    require(widgets and all(widget.get("exported") is False for widget in widgets), "Rigify widget export metadata is invalid")
    require(all(widget.hide_render for widget in widgets), "A Rigify widget can render")
    require(not bpy.data.actions, "DCC authoring layer unexpectedly contains an action")

    expected_constraint_count = len(DCC_DRIVER_TARGETS) + 2
    actual_constraint_count = sum(len(pose_bone.constraints) for pose_bone in game_rig.pose.bones)
    require(actual_constraint_count == expected_constraint_count, "Game-rig DCC driver constraint count changed")
    for game_name, target_name in DCC_DRIVER_TARGETS.items():
        constraints = list(game_rig.pose.bones[game_name].constraints)
        rotations = [item for item in constraints if item.type == "COPY_ROTATION"]
        locations = [item for item in constraints if item.type == "COPY_LOCATION"]
        require(len(rotations) == 1, f"{game_name} lacks its single Rigify rotation driver")
        require(len(locations) == (1 if game_name in {"Root", "Hips"} else 0), f"{game_name} has the wrong location-driver count")
        for constraint in constraints:
            require(constraint.target == control_rig, f"{game_name} driver targets the wrong object")
            require(constraint.subtarget == target_name, f"{game_name} driver targets the wrong control bone")
            require(constraint.owner_space == "LOCAL" and constraint.target_space == "LOCAL_OWNER_ORIENT", f"{game_name} driver lost owner-oriented local retargeting")
            require(abs(constraint.influence - 1.0) <= 1e-8 and not constraint.mute, f"{game_name} driver is not fully active")
    for spec in BONE_SPECS:
        if spec.role == "socket":
            require(not game_rig.pose.bones[spec.name].constraints, f"Socket {spec.name} must inherit instead of duplicating a DCC driver")

    bpy.context.view_layer.update()
    evaluated = game_rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
    maximum_evaluated_rest_deviation = 0.0
    for spec in BONE_SPECS:
        deviation = (evaluated.pose.bones[spec.name].head - Vector(spec.head)).length
        maximum_evaluated_rest_deviation = max(maximum_evaluated_rest_deviation, deviation)
        require(deviation <= 0.00002, f"Rigify driver changes the neutral joint for {spec.name}: {deviation:.8f} m")
    report["maximumDrivenRestJointDeviationMeters"] = maximum_evaluated_rest_deviation
    report["controlBoneCategories"] = {
        "original": sum(bone.name.startswith("ORG-") for bone in control_rig.data.bones),
        "mechanism": sum(bone.name.startswith("MCH-") for bone in control_rig.data.bones),
        "deform": sum(bone.name.startswith("DEF-") for bone in control_rig.data.bones),
        "animatorFacingAndOther": sum(
            not bone.name.startswith(("ORG-", "MCH-", "DEF-"))
            for bone in control_rig.data.bones
        ),
    }


def create_bind_probe(rig: bpy.types.Object) -> bpy.types.Object:
    vertices = (
        (-0.120, -0.080, 0.900), (0.120, -0.080, 0.900),
        (0.120, 0.080, 0.900), (-0.120, 0.080, 0.900),
        (-0.145, -0.090, 1.120), (0.145, -0.090, 1.120),
        (0.145, 0.090, 1.120), (-0.145, 0.090, 1.120),
    )
    faces = (
        (0, 1, 2, 3), (4, 7, 6, 5),
        (0, 4, 5, 1), (1, 5, 6, 2),
        (2, 6, 7, 3), (4, 0, 3, 7),
    )
    mesh = bpy.data.meshes.new(BIND_PROBE_NAME)
    mesh.from_pydata(vertices, (), faces)
    mesh.update()
    probe = bpy.data.objects.new(BIND_PROBE_NAME, mesh)
    bpy.context.scene.collection.objects.link(probe)
    probe.parent = rig
    probe.matrix_parent_inverse = Matrix.Identity(4)
    probe.matrix_basis = Matrix.Identity(4)
    weights = {
        "Hips": 0.40,
        "Spine": 0.25,
        "LeftUpperLeg": 0.175,
        "RightUpperLeg": 0.175,
    }
    for name, weight in weights.items():
        group = probe.vertex_groups.new(name=name)
        group.add(list(range(len(vertices))), weight, "REPLACE")
    modifier = probe.modifiers.new("SessionHumanoidRigV1_Bind", "ARMATURE")
    modifier.object = rig
    probe.hide_render = True
    probe.hide_set(True)
    probe.display_type = "WIRE"
    probe["exported"] = False
    probe["purpose"] = "four-weight normalized bind validation only"
    return probe


def validate_source_rig(rig: bpy.types.Object, probe: bpy.types.Object) -> dict[str, object]:
    require(rig.parent is None, "Rig object must be a scene root")
    require(rig.location.length <= 1e-8, "Rig object location is not identity")
    require(max(abs(value) for value in rig.rotation_euler) <= 1e-8, "Rig object rotation is not identity")
    require(max(abs(value - 1.0) for value in rig.scale) <= 1e-8, "Rig object scale is not identity")
    require(not rig.constraints, "Rig object contains constraints")
    require(rig.animation_data is None, "Rig contains animation data")
    # Blender stores armature bones in a hierarchy-dependent internal order on
    # leaving Edit Mode.  The authored order is the manifest contract; the DCC
    # and FBX gates enforce the exact name set and parent map instead of relying
    # on that implementation detail.
    require(
        set(bone.name for bone in rig.data.bones) == set(spec.name for spec in BONE_SPECS),
        "Source bone set changed",
    )
    for spec in BONE_SPECS:
        bone = rig.data.bones[spec.name]
        require((bone.parent.name if bone.parent else None) == spec.parent, f"Wrong parent for {spec.name}")
        require((bone.head_local - Vector(spec.head)).length <= 1e-7, f"Wrong head for {spec.name}")
        require((bone.tail_local - Vector(spec.tail)).length <= 1e-7, f"Wrong tail for {spec.name}")
        require(bone.use_deform == spec.deform, f"Wrong deform flag for {spec.name}")
        require(not bone.use_relative_parent, f"Relative parenting enabled on {spec.name}")
        basis = bone.matrix_local.to_3x3()
        require(abs(basis.determinant() - 1.0) <= 1e-5, f"Non-orthonormal rest basis on {spec.name}")
    require(not any(pose_bone.constraints for pose_bone in rig.pose.bones), "Pose constraints are present")

    group_names = {group.index: group.name for group in probe.vertex_groups}
    maximum_weights = 0
    for vertex in probe.data.vertices:
        active = [element for element in vertex.groups if element.weight > 1e-6]
        require(active, f"Bind probe vertex {vertex.index} is unweighted")
        require(len(active) <= 4, f"Bind probe vertex {vertex.index} exceeds four weights")
        require(abs(sum(element.weight for element in active) - 1.0) <= 1e-6, f"Bind probe vertex {vertex.index} is not normalized")
        require(all(group_names[element.group] in rig.data.bones for element in active), "Bind probe references an unknown bone")
        maximum_weights = max(maximum_weights, len(active))
    return {
        "name": probe.name,
        "exported": False,
        "vertices": len(probe.data.vertices),
        "triangles": sum(len(polygon.vertices) - 2 for polygon in probe.data.polygons),
        "maximumWeights": maximum_weights,
        "weightsNormalized": True,
        "armatureModifierTarget": rig.name,
    }


def export_fbx(rig: bpy.types.Object) -> None:
    bpy.ops.object.select_all(action="DESELECT")
    rig.hide_set(False)
    rig.hide_render = False
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    # Blender's exporter otherwise records wall-clock time in the FBX header.
    # Pin that metadata without changing the bundled exporter on disk.
    from io_scene_fbx import export_fbx_bin, fbx_utils
    original_header_writer = export_fbx_bin.fbx_header_elements
    original_uuid_factory = fbx_utils._key_to_uuid

    def deterministic_header_writer(root, scene_data, time=None):
        return original_header_writer(root, scene_data, time=FBX_CREATION_TIMESTAMP)

    def deterministic_uuid_factory(uuids, key):
        if isinstance(key, int) and 0 <= key < 2**63:
            value = key
        else:
            value = int.from_bytes(
                hashlib.sha256(repr(key).encode("utf-8")).digest()[:8],
                byteorder="big",
            ) & ((1 << 63) - 1)
            value = value or 1
        while value in uuids:
            value = (value + 1) & ((1 << 63) - 1)
            value = value or 1
        return fbx_utils.UUID(value)

    export_fbx_bin.fbx_header_elements = deterministic_header_writer
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
            bake_anim=False,
            use_custom_props=True,
            path_mode="AUTO",
            embed_textures=False,
        )
    finally:
        export_fbx_bin.fbx_header_elements = original_header_writer
        fbx_utils._key_to_uuid = original_uuid_factory
        fbx_utils._keys_to_uuids.clear()
        fbx_utils._uuids_to_keys.clear()
    require(FBX_PATH.is_file() and FBX_PATH.stat().st_size > 10_000, "Skeleton FBX export failed")


def bone_contract_record(rig: bpy.types.Object, spec: BoneSpec) -> dict[str, object]:
    bone = rig.data.bones[spec.name]
    return {
        "name": spec.name,
        "parent": spec.parent,
        "headMeters": vector_tuple(bone.head_local),
        "tailMeters": vector_tuple(bone.tail_local),
        "lengthMeters": round(bone.length, 6),
        "restEulerRadians": vector_tuple(Vector(bone.matrix_local.to_euler("XYZ")), 8),
        "connected": spec.connected,
        "deform": spec.deform,
        "role": spec.role,
        "unityHumanoidSlot": spec.humanoid_slot,
        "restMatrix": matrix_values(bone.matrix_local),
    }


def write_contract(rig: bpy.types.Object) -> None:
    rows = []
    for spec in BONE_SPECS:
        bone = rig.data.bones[spec.name]
        rows.append(
            f"| `{spec.name}` | `{spec.parent or '-'}` | "
            f"`{tuple(round(v, 3) for v in bone.head_local)}` | "
            f"`{tuple(round(v, 3) for v in bone.tail_local)}` | "
            f"{'yes' if spec.deform else 'no'} | `{spec.role}` |"
        )
    left_upper_arm = Vector(SPEC_BY_NAME["LeftUpperArm"].head)
    left_fingertip = Vector(SPEC_BY_NAME["LeftMiddleDistal"].tail)
    CONTRACT_PATH.write_text(
        f"""# Session Humanoid Rig V1 contract

Status: first-party DCC source contract; not integrated into Unity.

- Source units: meters (`scale_length = 1`), identity armature object.
- Blender coordinates: `+Z` up, `-Y` forward, left side at `+X`.
- FBX conversion: Blender `-Z Forward / +Y Up`, yielding Unity `+Z`
  forward and `+Y` up.
- Reference ground-to-crown height: `1.780 m`; source and FBX root scale `1`.
- Pose: accepted neutral MPFB-body midpoint T-pose, mirror-symmetric and left at
  `+X`. It deliberately preserves the measured anatomical arm slope instead of
  stretching either production body to a synthetic horizontal line. Left upper-
  arm joint is `{tuple(round(v, 6) for v in left_upper_arm)} m`; left middle
  fingertip joint is `{tuple(round(v, 6) for v in left_fingertip)} m`.
- Rest-fit input: retained coordinates-only
  `SessionHumanoidBaseV1_RigFitLandmarks.json`, SHA-256
  `{EXPECTED_REST_FIT_ARTIFACT_SHA256}`; neutral proposal canonical SHA-256
  `{EXPECTED_REST_FIT_PROPOSAL_SHA256}`. The rig builder does not import the
  measurement armature or body mesh.
- Exported hierarchy: 66 bones = one non-deform `Root`, 55 Unity Humanoid
  deform bones, and ten explicitly non-deform stable sockets.
- The `.blend` retains a fitted Blender 5.2 bundled Rigify Human metarig and its
  generated animator controls. Explicit `LOCAL` owner / `LOCAL_OWNER_ORIENT`
  target constraints drive `Root` and all 55 Humanoid game bones; the ten sockets
  inherit from driven game parents.
- The FBX contains no mesh, action, constraint, Rigify metarig, control,
  mechanism, original, deform-helper, or widget bone. Export selection contains
  only `SessionHumanoidRigV1`. The `.blend` also retains one hidden, non-exported
  normalized four-weight bind probe.

## DCC animation workflow

1. Fit meshes and create weights against `SessionHumanoidRigV1`; never bind a
   shipping mesh to the Rigify-generated `DEF-*` bones.
2. Animate `SessionHumanoidRigV1_RigifyControls_NotExported`. Its `ORG-*` bridge
   drives the project-owned game bones through the authored constraints.
3. Before exporting an animation clip, bake evaluated motion onto a duplicate of
   the game rig, remove its DCC constraints, and export only that duplicate plus
   the accepted mesh. The V1 artifact here intentionally exports no action.
4. Never select or export the metarig, generated Rigify control rig, or `WGT-*`
   widgets. The deterministic verifier rejects any such FBX joint or object.

## Exact bone hierarchy and landmarks

| Bone | Parent | Head m | Tail m | Deform | Role |
|---|---|---:|---:|---|---|
""" + "\n".join(rows) + "\n",
        encoding="utf-8",
    )


def write_provenance() -> None:
    originating_report = REST_FIT_BUILD_MANIFEST["bodyIntakeReport"]["path"]
    originating_report_hash = REST_FIT_BUILD_MANIFEST["bodyIntakeReport"]["sha256"]
    PROVENANCE_PATH.write_text(
        f"""# Session Humanoid Rig V1 provenance

Status: **first-party DCC source; not integrated into Unity**.

The exported hierarchy, conventional Unity Humanoid names, stable sockets, roll
contract, Rigify-to-game mapping, and bind-probe weights are authored by this
project in `build_session_humanoid_rig_v1.py`. Its rest joint positions are
calibrated to the exact mirror-averaged masculine/feminine midpoint retained in
`SessionHumanoidBaseV1_RigFitLandmarks.json` (SHA-256
`{EXPECTED_REST_FIT_ARTIFACT_SHA256}`; canonical proposal SHA-256
`{EXPECTED_REST_FIT_PROPOSAL_SHA256}`). That artifact records the accepted CC0
MPFB 2.0.17 body measurements in grounded 1.780 m mesh-local space. Its
originating intake record is `{originating_report}` with recorded SHA-256
`{originating_report_hash}`.

The rig builder reads only the retained numeric landmark artifact. It does not
import or sample an MPFB armature, body mesh, weight cage, pose, or add-on. The
MPFB temporary `game_engine` measurement rig and GPL add-on code are tooling-only
upstream of the retained CC0 coordinate record and are absent from this `.blend`,
FBX, and every runtime deliverable. No Quaternius, Blender Foundation Human Base,
legacy skeleton, or legacy animation is imported or sampled.

The editable `.blend` uses Blender 5.2's bundled Rigify Human metarig as a
standard DCC control-authoring tool. The script creates the metarig from the
bundled operator, refits its driving body/finger/eye/jaw landmarks to the
first-party numeric contract, deterministically generates controls, and connects
those controls to the separate game rig. No Rigify bone is part of the exported
hierarchy. The metarig, generated control/mechanism/original/deform-helper bones,
and widget meshes are all explicitly marked non-exported.

The FBX is a skeleton-only interchange file. The hidden bind probe exists only in
the editable `.blend` to prove a normalized maximum-four-weight skinning contract
and is deliberately excluded from export.
""",
        encoding="utf-8",
    )


def write_manifest(
    rig: bpy.types.Object,
    bind_probe_report: dict[str, object],
    dcc_authoring_report: dict[str, object],
) -> None:
    deform_names = [spec.name for spec in BONE_SPECS if spec.deform]
    socket_names = [spec.name for spec in BONE_SPECS if spec.role == "socket"]
    all_points = [Vector(spec.head) for spec in BONE_SPECS] + [Vector(spec.tail) for spec in BONE_SPECS]
    minimum = Vector(tuple(min(point[axis] for point in all_points) for axis in range(3)))
    maximum = Vector(tuple(max(point[axis] for point in all_points) for axis in range(3)))
    left_arm_points = [
        Vector(SPEC_BY_NAME["LeftShoulder"].head),
        Vector(SPEC_BY_NAME["LeftUpperArm"].head),
        Vector(SPEC_BY_NAME["LeftLowerArm"].head),
        Vector(SPEC_BY_NAME["LeftHand"].head),
        Vector(SPEC_BY_NAME["LeftHand"].tail),
        Vector(SPEC_BY_NAME["LeftMiddleDistal"].tail),
    ]
    right_arm_points = [
        Vector((-point.x, point.y, point.z))
        for point in left_arm_points
    ]
    left_middle_tip = Vector(SPEC_BY_NAME["LeftMiddleDistal"].tail)
    right_middle_tip = Vector(SPEC_BY_NAME["RightMiddleDistal"].tail)
    source_boundary = REST_FIT_ARTIFACT["sourceBoundary"]
    variant_fit = REST_FIT_ARTIFACT["variants"]
    manifest = {
        "schemaVersion": 1,
        "assetId": ASSET_ID,
        "integrationStatus": "SOURCE_DCC_NOT_IN_UNITY",
        "ownership": "first-party game/export hierarchy and authoring contract, rest-fit to a retained CC0 MPFB body-landmark midpoint; Blender-bundled Rigify is a non-exported DCC authoring tool",
        "externalSkeletonOrModelDependency": False,
        "restProportionCalibrationDependency": "retained CC0 coordinates-only SessionHumanoidBaseV1 body-fit artifact",
        "legacySkeletonImportedOrSampled": False,
        "rigifyUsed": True,
        "rigifyContentInFbx": False,
        "determinismBoundary": {
            "stableLandmarkInputByteDeterministic": True,
            "neutralProposalCanonicalHashPinned": True,
            "fbxUsesFixedTimestampAndDeterministicUuids": True,
            "fbxExpectedByteDeterministicForIdenticalInputs": True,
            "blendBytesDeterministic": False,
            "manifestBytesDeterministic": False,
            "reason": "Blender .blend serialization can vary across identical saves; the manifest snapshots the current blend hash. The skeleton FBX has fixed header time and content-derived UUIDs and is independently compared after repeated builds.",
        },
        "toolchain": {
            "blenderVersion": bpy.app.version_string,
            "rigifySource": "bundled addons_core/rigify",
            "fbxExporter": "Blender bundled io_scene_fbx",
        },
        "coordinates": {
            "unitSystem": "METRIC",
            "metersPerUnit": 1.0,
            "blenderUp": BLENDER_UP,
            "blenderForward": BLENDER_FORWARD,
            "blenderLeft": "+X",
            "fbxAxisForward": "-Z",
            "fbxAxisUp": "+Y",
            "unityUp": UNITY_UP,
            "unityForward": UNITY_FORWARD,
        },
        "dimensions": {
            "referenceGroundToCrownMeters": REFERENCE_HEIGHT_METERS,
            "rootGroundMeters": [0.0, 0.0, 0.0],
            "hipsHeadMeters": vector_tuple(Vector(SPEC_BY_NAME["Hips"].head)),
            "leftShoulderRootMeters": vector_tuple(Vector(SPEC_BY_NAME["LeftShoulder"].head)),
            "leftUpperArmHeadMeters": vector_tuple(Vector(SPEC_BY_NAME["LeftUpperArm"].head)),
            "rightUpperArmHeadMeters": vector_tuple(Vector(SPEC_BY_NAME["RightUpperArm"].head)),
            "leftElbowMeters": vector_tuple(Vector(SPEC_BY_NAME["LeftLowerArm"].head)),
            "rightElbowMeters": vector_tuple(Vector(SPEC_BY_NAME["RightLowerArm"].head)),
            "leftWristMeters": vector_tuple(Vector(SPEC_BY_NAME["LeftHand"].head)),
            "rightWristMeters": vector_tuple(Vector(SPEC_BY_NAME["RightHand"].head)),
            "leftMiddleFingertipJointMeters": vector_tuple(left_middle_tip),
            "rightMiddleFingertipJointMeters": vector_tuple(right_middle_tip),
            "middleFingertipJointSpanXMeters": round(left_middle_tip.x - right_middle_tip.x, 6),
            "neutralBodySurfaceFingertipSpanMeters": round(REST_FIT_PROPOSAL["surfaceFingertipHalfSpanMeters"] * 2.0, 6),
            "leftFootTipMeters": vector_tuple(Vector(SPEC_BY_NAME["Socket_FootFX_L"].head)),
            "rightFootTipMeters": vector_tuple(Vector(SPEC_BY_NAME["Socket_FootFX_R"].head)),
            "neutralArmJointVerticalRangeMeters": round(max(point.z for point in left_arm_points) - min(point.z for point in left_arm_points), 6),
            "neutralArmJointForwardRangeMeters": round(max(point.y for point in left_arm_points) - min(point.y for point in left_arm_points), 6),
            "exportedSkeletonIncludingSocketsBoundsMeters": {
                "minimum": vector_tuple(minimum),
                "maximum": vector_tuple(maximum),
            },
            "classKitClearanceGuideMeters": {
                "status": "authoring clearance guide; body-fit artifact remains authoritative",
                "chest": [0.18, 0.115],
                "waist": [0.14, 0.095],
                "pelvis": [0.155, 0.105],
                "head": [0.075, 0.09],
                "shoulderRadius": 0.085,
                "upperArmRadius": 0.065,
                "forearmRadius": 0.055,
                "thighRadius": 0.085,
                "shinRadius": 0.060,
            },
        },
        "hierarchy": {
            "armatureObjects": 1,
            "exportedBoneCount": len(BONE_SPECS),
            "unityHumanoidDeformBoneCount": len(deform_names),
            "rootBoneCount": 1,
            "socketBoneCount": len(socket_names),
            "orderedBoneNames": [spec.name for spec in BONE_SPECS],
            "deformBoneNames": deform_names,
            "socketBoneNames": socket_names,
            "humanBoneMap": HUMANOID_MAP,
            "bones": [bone_contract_record(rig, spec) for spec in BONE_SPECS],
        },
        "sourceTransform": {
            "location": vector_tuple(rig.location),
            "rotationEulerRadians": vector_tuple(Vector(rig.rotation_euler)),
            "scale": vector_tuple(rig.scale),
            "matrixWorld": matrix_values(rig.matrix_world),
        },
        "tPoseContract": {
            "acceptedBodyMidpointPose": True,
            "exactMirrorSymmetry": True,
            "armsLaterallyExtended": True,
            "preservesMeasuredArmSlope": True,
            "leftArmJointHeadsMeters": [vector_tuple(point) for point in left_arm_points],
            "rightArmJointHeadsMeters": [vector_tuple(point) for point in right_arm_points],
            "leftSideIsPositiveX": True,
            "legsAnatomicallyNeutral": True,
            "feetPointForwardNegativeYInBlender": True,
            "poseActions": 0,
        },
        "restFit": {
            "revision": REST_FIT_REVISION,
            "inputArtifact": str(REST_FIT_ARTIFACT_PATH.relative_to(PROJECT_ROOT)),
            "inputArtifactSha256": EXPECTED_REST_FIT_ARTIFACT_SHA256,
            "neutralProposalCanonicalSha256": EXPECTED_REST_FIT_PROPOSAL_SHA256,
            "perBuildProvenanceManifest": str(REST_FIT_BUILD_MANIFEST_PATH.relative_to(PROJECT_ROOT)),
            "perBuildProvenanceManifestSha256": EXPECTED_REST_FIT_BUILD_MANIFEST_SHA256,
            "originatingBodyIntakeReport": REST_FIT_BUILD_MANIFEST["bodyIntakeReport"]["path"],
            "originatingBodyIntakeReportSha256": REST_FIT_BUILD_MANIFEST["bodyIntakeReport"]["sha256"],
            "originatingCheckpointBlend": REST_FIT_BUILD_MANIFEST["checkpointBlend"]["path"],
            "originatingCheckpointBlendSha256": REST_FIT_BUILD_MANIFEST["checkpointBlend"]["sha256"],
            "checkpointBlendBitwiseDeterministic": REST_FIT_BUILD_MANIFEST["checkpointBlend"]["bitwiseDeterministic"],
            "rule": REST_FIT_PROPOSAL["rule"],
            "orientation": REST_FIT_PROPOSAL["orientation"],
            "landmarkCount": len(REST_FIT_PROPOSAL["landmarks"]),
            "surfaceFingertipHalfSpanMeters": REST_FIT_PROPOSAL["surfaceFingertipHalfSpanMeters"],
            "variantSurfaceFingertipHalfSpansMeters": {
                variant: variant_fit[variant]["surfaceLandmarks"]["fingertipHalfSpanMeters"]
                for variant in ("Masculine", "Feminine")
            },
            "variantStockPoseModified": {
                variant: variant_fit[variant]["stockPoseModified"]
                for variant in ("Masculine", "Feminine")
            },
            "variantLocalPoseCorrection": {
                variant: variant_fit[variant]["localPoseCorrection"]
                for variant in ("Masculine", "Feminine")
            },
            "variantLandmarkContainmentStatus": {
                variant: variant_fit[variant]["landmarkSkinContainment"]["status"]
                for variant in ("Masculine", "Feminine")
            },
            "sourceBoundary": source_boundary,
            "legacyOrFoundationSourceUsed": False,
            "globalOrAxialStretchAppliedToEitherBody": False,
            "authoredGroundRootMeters": [0.0, 0.0, 0.0],
        },
        "bindReadiness": bind_probe_report,
        "dccAuthoring": dcc_authoring_report,
        "exportContract": {
            "fbxContainsArmatures": 1,
            "fbxContainsMeshes": 0,
            "fbxContainsActions": 0,
            "fbxContainsCameras": 0,
            "fbxContainsLights": 0,
            "addLeafBones": False,
            "fixedCreationTimestamp": FBX_CREATION_TIMESTAMP.isoformat(),
            "deterministicFbxUuidHash": "sha256(repr(exporter-key))[:8] masked to signed int64",
            "controlsOrMechanismBones": 0,
            "selectedObjectNames": [RIG_OBJECT_NAME],
            "rigifyMetarigObjects": 0,
            "rigifyControlRigObjects": 0,
            "rigifyWidgetObjects": 0,
            "unitRootScale": True,
            "socketAndDeformRolesAreManifestContract": True,
            "note": "FBX joints do not preserve Blender's use_deform flag; Unity Humanoid mapping uses the explicit 55-bone humanBoneMap and ignores ten named socket joints.",
        },
        "sourceFiles": {
            str(Path(__file__).resolve().relative_to(PROJECT_ROOT)): sha256(Path(__file__).resolve()),
            str(VERIFIER_PATH.relative_to(PROJECT_ROOT)): sha256(VERIFIER_PATH),
            str(CONTRACT_PATH.relative_to(PROJECT_ROOT)): sha256(CONTRACT_PATH),
            str(PROVENANCE_PATH.relative_to(PROJECT_ROOT)): sha256(PROVENANCE_PATH),
            str(REST_FIT_ARTIFACT_PATH.relative_to(PROJECT_ROOT)): sha256(REST_FIT_ARTIFACT_PATH),
            str(REST_FIT_BUILD_MANIFEST_PATH.relative_to(PROJECT_ROOT)): sha256(REST_FIT_BUILD_MANIFEST_PATH),
        },
        "outputHashes": {
            BLEND_PATH.name: sha256(BLEND_PATH),
            FBX_PATH.name: sha256(FBX_PATH),
            PROVENANCE_PATH.name: sha256(PROVENANCE_PATH),
        },
        "approvalBoundary": {
            "underUnityAssets": False,
            "unityIntegrationPerformed": False,
            "nextStep": "Bind the separately accepted CC0 MPFB body variants and first-party class kit, visually scan deformation, then consider Unity Humanoid configuration as a distinct reviewed task.",
        },
    }
    MANIFEST_PATH.write_text(json.dumps(manifest, indent=2, sort_keys=True) + "\n", encoding="utf-8")


def main() -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    require("Assets" not in SOURCE_DIR.parts, "Rig source must remain outside Unity Assets")
    require(VERIFIER_PATH.is_file(), f"Missing round-trip verifier: {VERIFIER_PATH}")
    validate_static_contract()
    SOURCE_DIR.mkdir(parents=True, exist_ok=True)
    OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
    reset_scene()
    rig = create_armature()
    probe = create_bind_probe(rig)
    bind_probe_report = validate_source_rig(rig, probe)
    metarig, control_rig, widgets, dcc_authoring_report = create_rigify_authoring_layer(rig)
    validate_dcc_authoring_layer(rig, metarig, control_rig, widgets, dcc_authoring_report)
    write_contract(rig)
    write_provenance()
    export_fbx(rig)
    probe.hide_set(True)
    rig.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.wm.save_as_mainfile(filepath=str(BLEND_PATH), check_existing=False)
    write_manifest(rig, bind_probe_report, dcc_authoring_report)
    print("SESSION_HUMANOID_RIG_V1_BUILD_OK")
    print("BLEND", BLEND_PATH)
    print("FBX", FBX_PATH)
    print("BONES", len(BONE_SPECS), "DEFORM", len(HUMANOID_MAP), "SOCKETS", sum(spec.role == "socket" for spec in BONE_SPECS))


if __name__ == "__main__":
    main()
