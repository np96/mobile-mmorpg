#!/usr/bin/env python3
"""Render the accepted Hunter motion on both SessionHumanoidBaseV3 variants.

This is a source-only visual/deformation gate.  It never writes an FBX or a Unity
asset and does not modify either accepted source blend.
"""

from __future__ import annotations

import hashlib
import json
from pathlib import Path
import sys

import bpy
from mathutils import Matrix, Vector


SCRIPT_PATH = Path(__file__).resolve()
PROJECT_ROOT = SCRIPT_PATH.parents[3]
sys.path.insert(0, str(SCRIPT_PATH.parent))
import session_humanoid_base_v3_contract as body_contract
import verify_session_humanoid_animation_proof_v3 as animation_verify


ASSET_ID = "session-humanoid-hunter-motion-v3-review"
OUTPUT_ROOT = PROJECT_ROOT / "SourceArt/Blender/Characters/SessionHumanoidHunterMotionV3Review"
EVIDENCE_ROOT = OUTPUT_ROOT / "Outputs/Evidence"
REPORT_PATH = OUTPUT_ROOT / "Outputs/SessionHumanoidHunterMotionV3.report.json"
MANUAL_REVIEW_PATH = OUTPUT_ROOT / "Outputs/SessionHumanoidHunterMotionV3.manual-review.json"
ANIMATION_BLEND = animation_verify.proof.SOURCE_BLEND
ANIMATION_SOURCE_REVIEW = animation_verify.proof.SOURCE_REVIEW_REPORT_PATH
STAGES = (
    ("Ready", "HunterBowReady", 1),
    ("Draw", "HunterBowDraw", 20),
    ("Hold", "HunterBowHold", 13),
    ("Loose", "HunterBowLoose", 5),
    ("Recovery", "HunterBowRecovery", 18),
)
DEFORMATION_GATES = {
    "minimumTriangleAreaRatio": 0.15,
    "minimumEdgeLengthRatio": 0.25,
    "maximumEdgeLengthRatio": 4.0,
}
MOTION_CORRECTIVE_STRENGTHS = {
    "Shoulder": 0.35,
    "Elbow": 0.0,
    "Wrist": 0.0,
    "Hip": 0.0,
    "Knee": 0.0,
    "Ankle": 0.0,
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise RuntimeError(message)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_and_validate_inputs() -> tuple[dict[str, object], dict[str, object]]:
    require(body_contract.BLEND_PATH.is_file(), "Accepted BodyV3 blend is missing")
    require(body_contract.MANIFEST_PATH.is_file(), "Accepted BodyV3 manifest is missing")
    require(body_contract.MANUAL_REVIEW_PATH.is_file(), "Accepted BodyV3 manual review is missing")
    require(ANIMATION_BLEND.is_file(), "AnimationProofV3 blend is missing")
    require(ANIMATION_SOURCE_REVIEW.is_file(), "AnimationProofV3 source-review report is missing")
    body_manifest = json.loads(body_contract.MANIFEST_PATH.read_text(encoding="utf-8"))
    body_review = json.loads(body_contract.MANUAL_REVIEW_PATH.read_text(encoding="utf-8"))
    animation_review = json.loads(ANIMATION_SOURCE_REVIEW.read_text(encoding="utf-8"))
    require(sha256(body_contract.BLEND_PATH) == body_manifest["blendSha256"], "BodyV3 blend hash changed")
    require(body_review["status"] == "PASS_MANUAL_DEFORMATION_REVIEW", "BodyV3 manual review is not accepted")
    require(
        animation_review["status"]
        == "PASS_SOURCE_ONLY_KINEMATIC_PROOF_PENDING_BODY_MOTION_REVIEW",
        "AnimationProofV3 source-only kinematic review is not accepted",
    )
    require(
        sha256(ANIMATION_BLEND) == animation_review["sourceBlendSha256"],
        "AnimationProofV3 blend hash changed",
    )
    require(
        sha256(animation_verify.proof.PLAN_PATH) == animation_review["controlPlanSha256"],
        "AnimationProofV3 control plan changed after source review",
    )
    bow = animation_review["technicalMetrics"]["hunterBowSequence"]
    require(bow["drawHandTravelMeters"] >= 0.30, "Animation source draw gate failed")
    require(bow["looseDrawHandTravelMeters"] >= 0.30, "Animation source release gate failed")
    require(animation_review["fbxWrittenByThisInvocation"] is False, "Source review unexpectedly wrote FBX")
    return body_manifest, animation_review


def append_animation_source() -> tuple[bpy.types.Object, dict[str, bpy.types.Action]]:
    action_names = tuple(clip_name for _stage, clip_name, _frame in STAGES)
    with bpy.data.libraries.load(str(ANIMATION_BLEND), link=False) as (source, destination):
        require(animation_verify.proof.GAME_RIG_NAME in source.objects, "Animation source game rig is missing")
        require(all(name in source.actions for name in action_names), "Animation source Hunter action is missing")
        destination.objects = [animation_verify.proof.GAME_RIG_NAME]
        destination.actions = list(action_names)
    source_rig = destination.objects[0]
    require(source_rig is not None and source_rig.type == "ARMATURE", "Appended animation rig is invalid")
    if not source_rig.users_collection:
        bpy.context.scene.collection.objects.link(source_rig)
    source_rig.hide_render = True
    source_rig.show_in_front = False
    for pose_bone in source_rig.pose.bones:
        for constraint in pose_bone.constraints:
            constraint.mute = True
    actions = {action.name: action for action in destination.actions}
    require(
        set(actions) == set(action_names),
        f"Appended Hunter action set changed: actual={sorted(actions)} expected={sorted(action_names)}",
    )
    return source_rig, actions


def body_height(body: bpy.types.Object) -> float:
    return max(vertex.co.z for vertex in body.data.vertices) - min(vertex.co.z for vertex in body.data.vertices)


def activate_motion_correctives(rig: bpy.types.Object) -> None:
    for key in tuple(rig.keys()):
        if key.startswith("PSC_"):
            region = next(
                (name for name in MOTION_CORRECTIVE_STRENGTHS if key.endswith(name)),
                None,
            )
            rig[key] = 0.0 if region is None else MOTION_CORRECTIVE_STRENGTHS[region]
    rig.update_tag(refresh={"OBJECT"})


def transfer_stage_pose(
    source_rig: bpy.types.Object,
    action: bpy.types.Action,
    clip_name: str,
    frame: int,
    target_rig: bpy.types.Object,
    height_scale: float,
) -> None:
    body_contract.reset_pose(target_rig)
    animation_verify.proof.bind_action_slot(
        source_rig,
        action,
        clip_name,
        imported_fbx=False,
    )
    bpy.context.scene.frame_set(frame)
    bpy.context.view_layer.update()
    evaluated = source_rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
    for bone_name in animation_verify.rig_contract.SPEC_BY_NAME:
        basis = animation_verify.proof.v2.evaluated_local_basis(source_rig, evaluated, bone_name)
        location, rotation, _scale = basis.decompose()
        if bone_name == "Hips":
            location *= height_scale
        else:
            location = Vector((0.0, 0.0, 0.0))
        target_rig.pose.bones[bone_name].matrix_basis = Matrix.LocRotScale(
            location,
            rotation.normalized(),
            Vector((1.0, 1.0, 1.0)),
        )
    activate_motion_correctives(target_rig)
    bpy.context.view_layer.update()


def configure_render() -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "SINGLE"
    scene.display.shading.single_color = (0.36, 0.46, 0.54)
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.display.shading.curvature_ridge_factor = 2.0
    scene.display.shading.curvature_valley_factor = 1.4
    scene.render.resolution_x = 1100
    scene.render.resolution_y = 1100
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.film_transparent = False
    if scene.world is None:
        scene.world = bpy.data.worlds.new("HunterMotionV3World")
    scene.world.color = (0.012, 0.017, 0.025)
    camera_data = bpy.data.cameras.new("HunterMotionV3CameraData")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("HunterMotionV3Camera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    return camera


def evaluated_bounds(objects: tuple[bpy.types.Object, ...]) -> tuple[Vector, Vector]:
    depsgraph = bpy.context.evaluated_depsgraph_get()
    points: list[Vector] = []
    for obj in objects:
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh(preserve_all_data_layers=False, depsgraph=depsgraph)
        try:
            points.extend(evaluated.matrix_world @ vertex.co for vertex in mesh.vertices)
        finally:
            evaluated.to_mesh_clear()
    require(points, "No evaluated Hunter motion geometry")
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def look_at(camera: bpy.types.Object, target: Vector) -> None:
    camera.rotation_euler = (target - camera.location).to_track_quat("-Z", "Y").to_euler()


def render_stage(
    variant: str,
    stage: str,
    active: tuple[bpy.types.Object, ...],
    camera: bpy.types.Object,
    *,
    close: bool,
) -> Path:
    active_set = set(active)
    for obj in bpy.context.scene.objects:
        if obj.type == "MESH" and not obj.name.startswith("WGT-"):
            obj.hide_render = obj not in active_set
    minimum, maximum = evaluated_bounds(active)
    center = (minimum + maximum) * 0.5
    dimensions = maximum - minimum
    if close:
        center.z = minimum.z + dimensions.z * 0.72
        camera.data.ortho_scale = max(dimensions.x * 0.98, dimensions.y * 1.05, dimensions.z * 0.60)
    else:
        camera.data.ortho_scale = max(dimensions.z * 1.12, dimensions.x * 1.05, dimensions.y * 1.05)
    # The draw hand crosses to the far side of the head during follow-through.
    # Review Loose close-up from the opposite side so the release arm cannot be
    # accepted merely because it is hidden by the head silhouette.
    camera_x = -4.8 if close and stage == "Loose" else 4.8
    camera.location = center + Vector((camera_x, -0.8, 1.05))
    look_at(camera, center + Vector((0.0, 0.0, dimensions.z * (0.02 if not close else 0.0))))
    suffix = "Close" if close else "Full"
    output = EVIDENCE_ROOT / f"SessionHumanoidHunterMotionV3_{variant}_{stage}_{suffix}.png"
    bpy.context.scene.render.filepath = str(output)
    bpy.ops.render.render(write_still=True)
    return output


def deformation_gate(body: bpy.types.Object, rest: list[Vector]) -> tuple[dict[str, object], list[str]]:
    posed = body_contract.evaluated_positions(body)
    metrics = body_contract.deformation_metrics(body, rest, posed)
    failures = []
    if metrics["minimumTriangleAreaRatio"] < DEFORMATION_GATES["minimumTriangleAreaRatio"]:
        failures.append("minimum triangle-area ratio")
    if metrics["minimumEdgeLengthRatio"] < DEFORMATION_GATES["minimumEdgeLengthRatio"]:
        failures.append("minimum edge-length ratio")
    if metrics["maximumEdgeLengthRatio"] > DEFORMATION_GATES["maximumEdgeLengthRatio"]:
        failures.append("maximum edge-length ratio")
    if metrics["degenerateTriangles"]:
        failures.append("degenerate triangles")
    return metrics, failures


def main() -> None:
    require(bpy.app.version >= (5, 2, 0), "Blender 5.2 or newer is required")
    body_manifest, animation_review = load_and_validate_inputs()
    animation_verify.configure()
    bpy.ops.wm.open_mainfile(filepath=str(body_contract.BLEND_PATH), load_ui=False)
    source_rig, actions = append_animation_source()
    EVIDENCE_ROOT.mkdir(parents=True, exist_ok=True)
    camera = configure_render()
    reports: dict[str, object] = {}
    failures: list[str] = []
    evidence_paths: list[Path] = []

    for variant in body_contract.VARIANTS:
        body = bpy.data.objects[body_contract.BODY_NAMES[variant]]
        rig = bpy.data.objects[body_contract.RIG_NAMES[variant]]
        eyes = tuple(bpy.data.objects[name] for name in body_contract.EYE_NAMES[variant])
        body_contract.reset_pose(rig)
        rest = body_contract.evaluated_positions(body)
        scale = body_height(body) / body_contract.CANONICAL_HEIGHT
        stage_reports = []
        for stage, clip_name, frame in STAGES:
            transfer_stage_pose(source_rig, actions[clip_name], clip_name, frame, rig, scale)
            metrics, stage_failures = deformation_gate(body, rest)
            full = render_stage(variant, stage, (body, *eyes), camera, close=False)
            evidence_paths.append(full)
            close_path = None
            if stage in {"Draw", "Loose"}:
                close_path = render_stage(variant, stage, (body, *eyes), camera, close=True)
                evidence_paths.append(close_path)
            if stage_failures:
                failures.append(f"{variant}/{stage}: {', '.join(stage_failures)}")
            stage_reports.append({
                "stage": stage,
                "clip": clip_name,
                "frame": frame,
                "status": "PASS_NUMERIC_DEFORMATION" if not stage_failures else "REJECT_NUMERIC_DEFORMATION",
                "deformationMetrics": metrics,
                "failures": stage_failures,
                "fullEvidence": str(full.relative_to(PROJECT_ROOT)),
                "closeEvidence": None if close_path is None else str(close_path.relative_to(PROJECT_ROOT)),
            })
        body_contract.reset_pose(rig)
        reports[variant] = {
            "heightMeters": body_height(body),
            "retargetMethod": "source game-rig local rotations on variant-fitted rest; Hips translation scaled by body height; no limb scale or automatic IK",
            "poseSpaceCorrectives": {
                "architecture": "accepted sparse shoulder/elbow/wrist/hip/knee correctives",
                "reviewPoseStrengths": MOTION_CORRECTIVE_STRENGTHS,
            },
            "stages": stage_reports,
        }

    report = {
        "schemaVersion": 1,
        "assetId": ASSET_ID,
        "status": "PASS_TWO_VARIANT_MOTION_DEFORMATION_PENDING_MANUAL_REVIEW" if not failures else "REJECT_TWO_VARIANT_MOTION_DEFORMATION",
        "bodySource": {
            "blend": str(body_contract.BLEND_PATH.relative_to(PROJECT_ROOT)),
            "blendSha256": body_manifest["blendSha256"],
            "manualReviewSha256": sha256(body_contract.MANUAL_REVIEW_PATH),
        },
        "animationSource": {
            "blend": str(ANIMATION_BLEND.relative_to(PROJECT_ROOT)),
            "blendSha256": animation_review["sourceBlendSha256"],
            "sourceReviewReport": str(ANIMATION_SOURCE_REVIEW.relative_to(PROJECT_ROOT)),
            "sourceReviewReportSha256": sha256(ANIMATION_SOURCE_REVIEW),
            "canonicalDrawHandTravelMeters": animation_review["technicalMetrics"]["hunterBowSequence"]["drawHandTravelMeters"],
            "canonicalReleaseHandTravelMeters": animation_review["technicalMetrics"]["hunterBowSequence"]["looseDrawHandTravelMeters"],
            "unchangedMinimumDrawAndReleaseGateMeters": 0.30,
            "fbxRoundTripDeferredUntilManualArtAcceptance": True,
        },
        "deformationGates": DEFORMATION_GATES,
        "variants": reports,
        "failures": failures,
        "evidenceHashes": {
            str(path.relative_to(PROJECT_ROOT)): sha256(path)
            for path in evidence_paths
        },
        "script": str(SCRIPT_PATH.relative_to(PROJECT_ROOT)),
        "scriptSha256": sha256(SCRIPT_PATH),
        "manualWeaponGripVisualGate": "NOT_RUN_REQUIRES_ACCEPTED_MANUAL_BOW",
        "unityIntegrationPerformed": False,
        "fbxWritten": False,
        "texturesWritten": False,
        "lodsWritten": False,
    }
    REPORT_PATH.parent.mkdir(parents=True, exist_ok=True)
    REPORT_PATH.write_text(json.dumps(report, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    manual = {
        "schemaVersion": 1,
        "assetId": ASSET_ID,
        "status": "PENDING_MANUAL_MOTION_REVIEW" if not failures else "BLOCKED_BY_NUMERIC_DEFORMATION_FAILURE",
        "reviewScope": [
            "Ready/Draw/Hold/Loose/Recovery silhouette on masculine and feminine variants",
            "shoulder, elbow, wrist, neck, hip, knee, and planted-foot read",
            "release follow-through and recovery continuity",
            "draw and loose upper-body close plates",
        ],
        "weaponGripExcludedUntilManualHunterBowExists": True,
        "promotionAllowed": False,
    }
    MANUAL_REVIEW_PATH.write_text(json.dumps(manual, indent=2, sort_keys=True) + "\n", encoding="utf-8")
    print(report["status"])
    print("MOTION_PLATES", len(evidence_paths))
    if failures:
        raise RuntimeError("; ".join(failures))


if __name__ == "__main__":
    main()
