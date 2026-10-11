#!/usr/bin/env python3
"""Non-writing all-frame diagnostics for AnimationProofV3 authoring."""

from __future__ import annotations

import json
from pathlib import Path
import sys

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
import build_session_humanoid_animation_proof_v3 as proof


def main() -> None:
    proof.assert_frozen_inputs()
    plan = json.loads(proof.PLAN_PATH.read_text(encoding="utf-8"))
    clips = proof.build_clip_specs(plan)
    proof.CLIPS = clips
    proof.CLIP_BY_NAME = {clip.name: clip for clip in clips}
    proof.configure_modules(clips)
    game_rig, control_rig, _manifest = proof.v2.load_rig_source_v2()
    sampled = {}
    for clip in clips:
        action = proof.v2.base.create_control_action(control_rig, clip)
        sampled[clip.name] = proof.sample_control_action(game_rig, control_rig, action, clip)
    projection = proof.apply_wrist_safety_projection(game_rig, sampled, plan)
    print("WRIST_SAFETY_PROJECTION", json.dumps(projection, sort_keys=True))
    temporal = proof.apply_hunter_temporal_hand_filter(game_rig, sampled, plan)
    print("HUNTER_TEMPORAL_HAND_FILTER", json.dumps(temporal, sort_keys=True))
    proof.v2.base.stabilize_intentionally_identical_endpoints(sampled)
    actions = {
        clip.name: proof.create_baked_action_v3(game_rig, clip, sampled[clip.name])
        for clip in clips
    }
    metrics, samples = proof.v2.base.collect_action_metrics(game_rig, actions)
    bow = metrics["hunterBowSequence"]
    print("HUNTER_BOW_METRICS", json.dumps(bow, sort_keys=True))
    try:
        proof.v2.base.validate_metrics(metrics)
    except RuntimeError as error:
        print("STANDARD_GATE_REJECT", str(error))
    else:
        print("STANDARD_GATES_PASS")

    limits = plan["orientationLimitsDegrees"]
    violations = []
    worst = {"Left": {"swing": None, "twist": None}, "Right": {"swing": None, "twist": None}}
    worst_step = {"Left": None, "Right": None}
    clip_steps = {}
    clip_step_worst = {}
    bow_rows = []
    proof.v2.base.set_constraints_muted(game_rig, True)
    for clip in clips:
        proof.bind_action_slot(game_rig, actions[clip.name], clip.name)
        previous = {"Left": None, "Right": None}
        clip_steps[clip.name] = {"Left": 0.0, "Right": 0.0}
        clip_step_worst[clip.name] = {"Left": None, "Right": None}
        for frame in range(clip.frame_start, clip.frame_end + 1):
            bpy.context.scene.frame_set(frame)
            bpy.context.view_layer.update()
            evaluated = game_rig.evaluated_get(bpy.context.evaluated_depsgraph_get())
            for side in ("Left", "Right"):
                basis = proof.v2.evaluated_local_basis(game_rig, evaluated, side + "Hand")
                rotation = basis.to_quaternion().normalized()
                swing, twist = proof.swing_twist_degrees(rotation)
                if previous[side] is not None:
                    step = previous[side].rotation_difference(rotation).angle * 180.0 / 3.141592653589793
                    if worst_step[side] is None or step > worst_step[side]["degrees"]:
                        worst_step[side] = {"clip": clip.name, "frame": frame, "degrees": step}
                    clip_steps[clip.name][side] = max(clip_steps[clip.name][side], step)
                    if clip_step_worst[clip.name][side] is None or step > clip_step_worst[clip.name][side]["degrees"]:
                        clip_step_worst[clip.name][side] = {"frame": frame, "degrees": step}
                previous[side] = rotation
                if worst[side]["swing"] is None or swing > worst[side]["swing"]["degrees"]:
                    worst[side]["swing"] = {"clip": clip.name, "frame": frame, "degrees": swing}
                if worst[side]["twist"] is None or abs(twist) > abs(worst[side]["twist"]["degrees"]):
                    worst[side]["twist"] = {"clip": clip.name, "frame": frame, "degrees": twist}
                if swing > limits["maximumHandSwing"] + 1e-4 or abs(twist) > limits["maximumAbsoluteHandTwist"] + 1e-4:
                    violations.append({"clip": clip.name, "frame": frame, "side": side, "swing": swing, "twist": twist})
            if clip.name in {"HunterBowReady", "HunterBowDraw", "HunterBowHold", "HunterBowLoose"}:
                left = evaluated.pose.bones["LeftHand"].head.copy()
                right = evaluated.pose.bones["RightHand"].head.copy()
                shoulder = evaluated.pose.bones["RightUpperArm"].head.copy()
                elbow = evaluated.pose.bones["RightLowerArm"].head.copy()
                bow_rows.append({
                    "clip": clip.name,
                    "frame": frame,
                    "rightElbowFlexDegrees": proof.elbow_flex_degrees(shoulder, elbow, right),
                    "handSeparationMeters": (left - right).length,
                    "rightHandX": right.x,
                })
    game_rig.animation_data.action = None
    proof.v2.base.set_constraints_muted(game_rig, False)
    print("ORIENTATION_WORST", json.dumps(worst, sort_keys=True))
    print("ORIENTATION_WORST_FRAME_STEP", json.dumps(worst_step, sort_keys=True))
    print("ORIENTATION_MAX_STEP_BY_CLIP", json.dumps(clip_steps, sort_keys=True))
    print("ORIENTATION_STEP_WORST_BY_CLIP", json.dumps(clip_step_worst, sort_keys=True))
    print("ORIENTATION_VIOLATIONS", json.dumps(violations, sort_keys=True))
    print("ORIENTATION_VIOLATION_COUNT", len(violations))
    print("BOW_MAX_ELBOW", json.dumps(max(bow_rows, key=lambda row: row["rightElbowFlexDegrees"]), sort_keys=True))
    print("BOW_MIN_SEPARATION", json.dumps(min(bow_rows, key=lambda row: row["handSeparationMeters"]), sort_keys=True))
    print("BOW_ELBOW_VIOLATIONS", json.dumps([row for row in bow_rows if row["rightElbowFlexDegrees"] > plan["bowContract"]["maximumRightElbowFlexDegrees"] + 1e-4], sort_keys=True))


if __name__ == "__main__":
    main()
