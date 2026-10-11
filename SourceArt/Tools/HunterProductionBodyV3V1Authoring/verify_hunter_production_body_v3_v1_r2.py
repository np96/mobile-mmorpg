#!/usr/bin/env python3
"""Independently verify the frozen Hunter BodyV3 R2 source-only checkpoint.

This verifier intentionally imports neither R1 nor R2 authoring code.  It must
only be run after the R2 builder has finished writing its blend, report, manual
review request, and complete evidence set.  The caller must supply the frozen
R2 report and blend SHA-256 values; the pinned report then pins every evidence
image transitively.

The verifier reopens the accepted BodyV3 source and the frozen R2 blend, audits
evaluated geometry in world space, and writes exactly one new JSON report under
R2/IndependentVerification.  It never saves a blend, exports FBX, writes Unity
assets, or grants visual-art acceptance.  A structural pass remains pending an
independent manual review of the renders.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import json
import math
from pathlib import Path
import re
import struct
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree


SCRIPT_PATH = Path(__file__).resolve()
PROJECT_ROOT = SCRIPT_PATH.parents[3]

ASSET_ID = "hunter-production-body-v3-v1-r2"
MODEL_NAME = "HunterProductionBodyV3V1_R2"
R2_ROOT = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R2"
)
R2_BLEND = R2_ROOT / f"{MODEL_NAME}.blend"
R2_REPORT = R2_ROOT / f"{MODEL_NAME}.report.json"
R2_REVIEW_REQUEST = R2_ROOT / f"{MODEL_NAME}.manual-review-request.json"
R2_INTERNAL_REVIEW = R2_ROOT / f"{MODEL_NAME}.internal-review.json"
INDEPENDENT_ROOT = R2_ROOT / "IndependentVerification"
INDEPENDENT_REPORT = (
    INDEPENDENT_ROOT / f"{MODEL_NAME}.independent-verification.json"
)

BODY_BLEND = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/SessionHumanoidBaseV3/SessionHumanoidBaseV3.blend"
)
BODY_BLEND_SHA256 = "ff30baebeb3cb03186cf1663691a65c743af16616c82003f4358b5a41fb08458"
BODY_NAME = "SessionHumanoidBaseV3_Masculine_Body"
BODY_RIG_NAME = "SessionHumanoidBaseV3_Masculine_Rig"
BODY_EYE_NAMES = (
    "SessionHumanoidBaseV3_Masculine_Eye_L",
    "SessionHumanoidBaseV3_Masculine_Eye_R",
)
EXPECTED_EVIDENCE_OBJECT_SOURCES = {
    "EvidenceOnly_HunterBodyV3_AcceptedMasculineBody": BODY_NAME,
    "EvidenceOnly_HunterBodyV3_L": BODY_EYE_NAMES[0],
    "EvidenceOnly_HunterBodyV3_R": BODY_EYE_NAMES[1],
}
GEOMETRY_QUANTIZATION_METERS = 1e-6

R1_FROZEN_HASHES = {
    "SourceArt/Tools/HunterProductionBodyV3V1Authoring/build_hunter_production_body_v3_v1_r1.py":
        "b97d3a44d644ab462e8501b99ea55808f782f4a4fca7b6e5cf646a2da6314644",
    "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1/HunterProductionBodyV3V1_R1.blend":
        "39a6513e37af55026d79e66eaeec7d450b0c45d1f783c2f1b1211864518be7e0",
    "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1/HunterProductionBodyV3V1_R1.report.json":
        "3b0bb6999925c3de4fe25a278be9a77aa67f77288ecaf1a672426a102774d46a",
    "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1/HunterProductionBodyV3V1_R1.manual-review-request.json":
        "fbdd1f3695cb1b131a9f2f404313a770d51718b49aa813d5c6bad31c2e450237",
    "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1/HunterProductionBodyV3V1_R1.internal-review.json":
        "79a9aedcd140690aee0093022c555e2f5f7c4ff0563fbe9fb7234ff99b40ac70",
    "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1/Evidence/HunterProductionBodyV3V1_R1_Back.png":
        "a2c50a89183fd56a7173b6857f9f19e169e45d693eeb523cd78309d57a758991",
    "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1/Evidence/HunterProductionBodyV3V1_R1_Front.png":
        "8a3380b6ecc09cb7608a311dd49da5fa3800fa1c1f4bc9ba60a30ad63ef5b7b4",
    "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1/Evidence/HunterProductionBodyV3V1_R1_FrontThreeQuarter.png":
        "c0e502afe4267321e5728c7dc71d679fc2d277d3e7eea5e96dec3a87873acecd",
    "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1/Evidence/HunterProductionBodyV3V1_R1_HeadClose.png":
        "41f57683684f77ad183e69f1f55a2754be9745255573bd61fd64a2f34d8a5dc1",
    "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1/Evidence/HunterProductionBodyV3V1_R1_ShoulderClose.png":
        "1666ac2a43868036cd52232bfdc7b9efd33f159bdfaaa57782720a5787262223",
    "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1/Evidence/HunterProductionBodyV3V1_R1_Side.png":
        "c8dbadbc9a50c8b04be6162cec7d3605e8263de70079e68e461ba04e2eee21fb",
}

EXPECTED_OBJECT_FAMILIES = {
    "HunterBodyV3_BaseGarments": "base_garments",
    "HunterBodyV3_HoodHat": "hood_hat",
    "HunterBodyV3_Cowl": "cowl",
    "HunterBodyV3_TailoredBrigandine": "brigandine",
    "HunterBodyV3_AsymmetricMantle": "mantle_pauldrons",
    "HunterBodyV3_LayeredPauldrons": "mantle_pauldrons",
    "HunterBodyV3_SplitCape": "split_cape",
    "HunterBodyV3_LimbGear": "limb_gear",
    "HunterBodyV3_Boots": "limb_gear",
    "HunterBodyV3_HarnessBelt": "harness_belt",
    "HunterBodyV3_RecurveBow": "weapon_quiver",
    "HunterBodyV3_QuiverArrows": "weapon_quiver",
    "HunterBodyV3_StaticString": "weapon_quiver",
    "HunterBodyV3_DrawString": "weapon_quiver",
    "HunterBodyV3_NockedArrow": "weapon_quiver",
}

HIDDEN_DEFAULT_REVIEW_OBJECTS = {
    "HunterBodyV3_DrawString",
    "HunterBodyV3_NockedArrow",
}
VISIBLE_DEFAULT_REVIEW_OBJECTS = (
    set(EXPECTED_OBJECT_FAMILIES) - HIDDEN_DEFAULT_REVIEW_OBJECTS
)

FAMILY_TRIANGLE_CEILINGS = {
    "base_garments": 4_200,
    "hood_hat": 1_200,
    "cowl": 1_000,
    "brigandine": 1_800,
    "mantle_pauldrons": 2_600,
    "split_cape": 1_400,
    "limb_gear": 3_000,
    "harness_belt": 1_200,
    "weapon_quiver": 2_400,
}
KIT_TRIANGLE_CEILING = 16_000

BODY_FIT_OBJECTS = {
    "HunterBodyV3_BaseGarments",
    "HunterBodyV3_HoodHat",
    "HunterBodyV3_Cowl",
    "HunterBodyV3_TailoredBrigandine",
    "HunterBodyV3_AsymmetricMantle",
    "HunterBodyV3_LayeredPauldrons",
    "HunterBodyV3_SplitCape",
    "HunterBodyV3_LimbGear",
    "HunterBodyV3_Boots",
    "HunterBodyV3_HarnessBelt",
}
MINIMUM_SIGNED_CLEARANCE_METERS = -0.001

# A lower clearance bound alone can be gamed by floating an oversized shell far
# away from the accepted anatomy.  Fitted layers therefore receive a full-sample
# p95 upper bound.  Pieces whose design legitimately flares away from the body
# receive a p95 bound only inside an independently selected rig-landmark anchor
# band; their unrestricted distribution is still reported in full.
FULL_CLEARANCE_P95_GATES = {
    "HunterBodyV3_BaseGarments": (
        0.025,
        "ordinary fitted base garments must remain close to the accepted BodyV3 anatomy",
    ),
    "HunterBodyV3_TailoredBrigandine": (
        0.045,
        "the fitted brigandine body shell may add armor depth but may not float as a bulk torso primitive",
    ),
    "HunterBodyV3_LimbGear": (
        0.045,
        "bracers and greaves must follow the accepted limbs rather than read as detached tubes",
    ),
    "HunterBodyV3_HarnessBelt": (
        0.065,
        "the outer harness may clear the fitted brigandine but may not float arbitrarily from the body stack",
    ),
}

# Absolute full-object caps are intentionally the same as the frozen R2
# builder's BODY_BULK_LIMITS maxima.  They complement, rather than replace,
# the verifier's independently stricter p95 gates: a small number of vertices
# may no longer escape scrutiny by hiding above the 95th percentile.
FULL_CLEARANCE_ABSOLUTE_MAXIMUM_GATES = {
    "HunterBodyV3_BaseGarments": 0.065,
    "HunterBodyV3_HoodHat": 0.140,
    "HunterBodyV3_Cowl": 0.160,
    "HunterBodyV3_TailoredBrigandine": 0.080,
    "HunterBodyV3_AsymmetricMantle": 0.160,
    "HunterBodyV3_LayeredPauldrons": 0.130,
    "HunterBodyV3_SplitCape": 0.340,
    "HunterBodyV3_LimbGear": 0.090,
    "HunterBodyV3_Boots": 0.140,
    "HunterBodyV3_HarnessBelt": 0.100,
}

ANCHOR_CLEARANCE_P95_GATES = {
    "HunterBodyV3_HoodHat": (
        0.045,
        "hood_lower_neck",
        "the hood may crown and droop, but its lower neck attachment must remain body-related",
    ),
    "HunterBodyV3_Cowl": (
        0.035,
        "cowl_neckline",
        "the cowl may drape outward, but its neckline must remain a close authored attachment",
    ),
    "HunterBodyV3_AsymmetricMantle": (
        0.045,
        "mantle_left_shoulder",
        "the mantle may flare at its hem, but its left-shoulder seat must remain attached",
    ),
    "HunterBodyV3_LayeredPauldrons": (
        0.045,
        "pauldron_shoulders",
        "layered pauldrons may project outward, but their inner shoulder seats may not float",
    ),
    "HunterBodyV3_SplitCape": (
        0.045,
        "cape_upper_back_yoke",
        "cape panels may hang and flare, but the upper-back yoke must remain attached",
    ),
    "HunterBodyV3_Boots": (
        0.045,
        "boot_ankles",
        "boot toe boxes and soles may project, but the ankle seats must follow BodyV3",
    ),
}

MINIMUM_ANCHOR_SAMPLE_COUNT = 24
MINIMUM_ANCHOR_SAMPLE_FRACTION = 0.05
ANCHOR_CLEARANCE_ABSOLUTE_MAXIMUM_METERS = 0.045

EXPECTED_ISLAND_RANGES = {
    "HunterBodyV3_BaseGarments": (6, 6),
    "HunterBodyV3_HoodHat": (1, 1),
    "HunterBodyV3_Cowl": (1, 1),
    "HunterBodyV3_TailoredBrigandine": (4, 4),
    "HunterBodyV3_AsymmetricMantle": (1, 1),
    "HunterBodyV3_LayeredPauldrons": (5, 5),
    "HunterBodyV3_SplitCape": (2, 2),
    "HunterBodyV3_LimbGear": (4, 4),
    "HunterBodyV3_Boots": (4, 4),
    "HunterBodyV3_HarnessBelt": (2, 2),
    "HunterBodyV3_RecurveBow": (1, 1),
    "HunterBodyV3_QuiverArrows": (17, 17),
    "HunterBodyV3_StaticString": (1, 1),
    "HunterBodyV3_DrawString": (1, 1),
    "HunterBodyV3_NockedArrow": (3, 3),
}

# Labels are tied to deterministic construction order in the frozen R2 source.
# They make every disconnected component independently visible in the report
# and let the few legitimate subcomponent attachment crossings be named rather
# than hidden by a whole-object exception.
EXPECTED_ISLAND_LABELS = {
    "HunterBodyV3_BaseGarments": (
        "torso",
        "negativeXSleeve",
        "positiveXSleeve",
        "pelvisBridge",
        "negativeXTrouserLeg",
        "positiveXTrouserLeg",
    ),
    "HunterBodyV3_TailoredBrigandine": (
        "mainArmorShell",
        "upperLapSeam",
        "middleLapSeam",
        "lowerLapSeam",
    ),
    "HunterBodyV3_LayeredPauldrons": (
        "positiveXInnerShell",
        "positiveXMiddleShell",
        "positiveXOuterShell",
        "negativeXInnerShell",
        "negativeXOuterShell",
    ),
    "HunterBodyV3_SplitCape": (
        "positiveXPanel",
        "negativeXPanel",
    ),
    "HunterBodyV3_LimbGear": (
        "negativeXBracer",
        "negativeXGreave",
        "positiveXBracer",
        "positiveXGreave",
    ),
    "HunterBodyV3_Boots": (
        "negativeXBootUpper",
        "negativeXSoleSidewall",
        "positiveXBootUpper",
        "positiveXSoleSidewall",
    ),
    "HunterBodyV3_HarnessBelt": (
        "diagonalHarness",
        "wrapBelt",
    ),
    "HunterBodyV3_QuiverArrows": (
        "quiverBody",
        "quiverRim",
        "arrow0Shaft",
        "arrow0NegativeYVane",
        "arrow0PositiveYVane",
        "arrow1Shaft",
        "arrow1NegativeYVane",
        "arrow1PositiveYVane",
        "arrow2Shaft",
        "arrow2NegativeYVane",
        "arrow2PositiveYVane",
        "arrow3Shaft",
        "arrow3NegativeYVane",
        "arrow3PositiveYVane",
        "arrow4Shaft",
        "arrow4NegativeYVane",
        "arrow4PositiveYVane",
    ),
    "HunterBodyV3_NockedArrow": (
        "shaft",
        "arrowhead",
        "fletching",
    ),
}

# R2 is a source-only macro cage rather than surfaced production topology.  The
# listed objects deliberately retain authored garment apertures or hems.  Their
# boundary counts are independently recomputed and compared with the pinned R2
# report, while true closed volumes must have zero boundary edges.
INTENTIONAL_OPEN_CAGE_BOUNDARIES = {
    "HunterBodyV3_BaseGarments": "neck, waist, wrist, and ankle garment openings",
    "HunterBodyV3_HoodHat": "face aperture and lower hood hem",
    "HunterBodyV3_Cowl": "neck aperture and hanging hem",
    "HunterBodyV3_TailoredBrigandine": "armor shell borders and three separate lap-seam ribbons",
    "HunterBodyV3_SplitCape": "two unsurfaced cloth-panel perimeters",
    "HunterBodyV3_LimbGear": "open-underarm bracers and open-back greaves",
    "HunterBodyV3_Boots": "open sole-sidewall borders retained for the macro cage",
    "HunterBodyV3_HarnessBelt": "source-cage strap and belt borders",
    "HunterBodyV3_QuiverArrows": "open quiver mouth and separately authored rim",
}

CLOSED_VOLUME_OBJECTS = set(EXPECTED_OBJECT_FAMILIES) - set(INTENTIONAL_OPEN_CAGE_BOUNDARIES)

REQUIRED_EVIDENCE_NAMES = {
    f"{MODEL_NAME}_FrontThreeQuarter.png",
    f"{MODEL_NAME}_Front.png",
    f"{MODEL_NAME}_Side.png",
    f"{MODEL_NAME}_Back.png",
    f"{MODEL_NAME}_BackThreeQuarter.png",
    f"{MODEL_NAME}_HeadClose.png",
    f"{MODEL_NAME}_ShoulderClose.png",
}

EXPECTED_FIT_PROXIES = {
    "BodyV3Fit_LongSleeveTop",
    "BodyV3Fit_Trousers",
}


def pair_key(first: str, second: str) -> tuple[str, str]:
    return tuple(sorted((first, second)))


def island_pair_key(
    object_name: str,
    first_label: str,
    second_label: str,
) -> tuple[str, str, str]:
    first, second = sorted((first_label, second_label))
    return object_name, first, second


# These are surface-tuck or attachment relationships, not blanket permission for
# arbitrary intersections.  Counts above the declared ceiling remain failures;
# each nonzero pair must also fall inside its deterministic AABBs declared below.
# Visible-state overlaps remain pending manual review; explicitly mutually
# exclusive weapon-state endpoints receive only the bounded structural check.
DESIGNED_LAYER_EXCEPTIONS = {
    pair_key("HunterBodyV3_BaseGarments", "HunterBodyV3_TailoredBrigandine"): (
        128, "fitted tunic may tuck under the armor shell at hidden borders"
    ),
    pair_key("HunterBodyV3_BaseGarments", "HunterBodyV3_Cowl"): (
        64, "cowl may tuck into the neckline"
    ),
    pair_key("HunterBodyV3_BaseGarments", "HunterBodyV3_SplitCape"): (
        64, "cape yoke may tuck under the upper-back garment"
    ),
    pair_key("HunterBodyV3_BaseGarments", "HunterBodyV3_LimbGear"): (
        128, "only bracer/greave border rings may meet the base cloth"
    ),
    pair_key("HunterBodyV3_BaseGarments", "HunterBodyV3_Boots"): (
        64, "trouser cuffs intentionally tuck into the boots"
    ),
    pair_key("HunterBodyV3_LimbGear", "HunterBodyV3_Boots"): (
        64, "greave and boot borders may meet at the ankle"
    ),
    pair_key("HunterBodyV3_HoodHat", "HunterBodyV3_Cowl"): (
        64, "hood neck edge intentionally tucks into the cowl"
    ),
    pair_key("HunterBodyV3_Cowl", "HunterBodyV3_TailoredBrigandine"): (
        64, "cowl hem may overlap the concealed armor neckline"
    ),
    pair_key("HunterBodyV3_Cowl", "HunterBodyV3_AsymmetricMantle"): (
        64, "cowl and mantle share a concealed shoulder attachment"
    ),
    pair_key("HunterBodyV3_Cowl", "HunterBodyV3_LayeredPauldrons"): (
        96, "cowl may tuck under the shoulder roots"
    ),
    pair_key("HunterBodyV3_Cowl", "HunterBodyV3_SplitCape"): (
        64, "cape yoke may tuck below the rear cowl"
    ),
    pair_key("HunterBodyV3_AsymmetricMantle", "HunterBodyV3_LayeredPauldrons"): (
        128, "pauldron root seats intentionally layer over the fur mantle"
    ),
    pair_key("HunterBodyV3_AsymmetricMantle", "HunterBodyV3_SplitCape"): (
        64, "cape yoke may tuck under the mantle root"
    ),
    pair_key("HunterBodyV3_LayeredPauldrons", "HunterBodyV3_SplitCape"): (
        96, "cape yoke may tuck beneath the pauldron roots"
    ),
    pair_key("HunterBodyV3_TailoredBrigandine", "HunterBodyV3_HarnessBelt"): (
        64, "harness endpoints attach over the armor with concealed tucks"
    ),
    pair_key("HunterBodyV3_Cowl", "HunterBodyV3_HarnessBelt"): (
        32, "upper harness end may terminate beneath the cowl"
    ),
    pair_key("HunterBodyV3_RecurveBow", "HunterBodyV3_StaticString"): (
        32, "static string is attached at the bow nocks"
    ),
    pair_key("HunterBodyV3_RecurveBow", "HunterBodyV3_DrawString"): (
        32, "draw string is attached at the bow nocks"
    ),
    pair_key("HunterBodyV3_RecurveBow", "HunterBodyV3_NockedArrow"): (
        64, "draw-only arrow crosses the bow plane at its rest"
    ),
    pair_key("HunterBodyV3_StaticString", "HunterBodyV3_DrawString"): (
        32, "mutually exclusive string states share bow-nock endpoints"
    ),
    pair_key("HunterBodyV3_DrawString", "HunterBodyV3_NockedArrow"): (
        32, "draw-only arrow and string meet at the nock"
    ),
    pair_key("HunterBodyV3_StaticString", "HunterBodyV3_NockedArrow"): (
        32, "mutually exclusive stored weapon states may share an endpoint"
    ),
}

# Every count-bounded exception is also confined to one or more deterministic
# world-space AABBs.  The verifier tests the complete AABB intersection envelope
# of each overlapping triangle pair; therefore an actual intersection cannot be
# outside a passing envelope.  Values are meters in the frozen R2 world frame.
EXCEPTION_AABBS = {
    "hood_cowl_neck_seam": (
        (-0.175, -0.165, 1.405),
        (0.175, 0.205, 1.535),
        "lower hood hem tucked only into the upper cowl neckline",
    ),
    "base_cowl_neck_seam": (
        (-0.205, -0.195, 1.325),
        (0.205, 0.205, 1.465),
        "base-cloth neckline beneath the lower cowl edge",
    ),
    "cowl_brigandine_neck_seam": (
        (-0.235, -0.235, 1.305),
        (0.235, 0.195, 1.445),
        "cowl hem at the brigandine upper neckline only",
    ),
    "base_cape_yoke_seam": (
        (-0.285, 0.085, 1.285),
        (0.285, 0.255, 1.425),
        "base-cloth upper-back border beneath the cape yoke",
    ),
    "cowl_cape_yoke_seam": (
        (-0.285, 0.085, 1.300),
        (0.285, 0.265, 1.455),
        "rear cowl hem over the cape yoke seam",
    ),
    "base_negative_x_shoulder_join": (
        (-0.275, -0.165, 1.315),
        (-0.145, 0.165, 1.535),
        "negative-X sleeve-to-torso shoulder join",
    ),
    "base_positive_x_shoulder_join": (
        (0.145, -0.165, 1.315),
        (0.275, 0.165, 1.535),
        "positive-X sleeve-to-torso shoulder join",
    ),
    "cowl_negative_x_pauldron_root": (
        (-0.285, -0.145, 1.335),
        (-0.135, 0.145, 1.515),
        "negative-X cowl-to-pauldron root seam",
    ),
    "cowl_positive_x_pauldron_root": (
        (0.135, -0.145, 1.335),
        (0.285, 0.145, 1.515),
        "positive-X cowl-to-pauldron root seam",
    ),
    "cowl_positive_x_mantle_root": (
        (0.045, -0.145, 1.385),
        (0.205, 0.165, 1.535),
        "cowl-to-mantle root on the positive-X shoulder",
    ),
    "mantle_positive_x_cape_yoke": (
        (0.045, 0.065, 1.320),
        (0.285, 0.235, 1.505),
        "positive-X mantle rear root over the cape yoke",
    ),
    "pauldron_negative_x_cape_yoke": (
        (-0.295, 0.055, 1.315),
        (-0.125, 0.225, 1.485),
        "negative-X pauldron root over the cape yoke",
    ),
    "pauldron_positive_x_cape_yoke": (
        (0.125, 0.055, 1.315),
        (0.295, 0.225, 1.485),
        "positive-X pauldron root over the cape yoke",
    ),
    "mantle_positive_x_inner_shell_seat": (
        (0.145, -0.135, 1.360),
        (0.245, 0.145, 1.525),
        "mantle seat beneath the positive-X inner pauldron",
    ),
    "mantle_positive_x_middle_shell_seat": (
        (0.215, -0.130, 1.345),
        (0.310, 0.140, 1.505),
        "mantle seat beneath the positive-X middle pauldron",
    ),
    "mantle_positive_x_outer_shell_seat": (
        (0.275, -0.125, 1.325),
        (0.365, 0.135, 1.485),
        "mantle seat beneath the positive-X outer pauldron",
    ),
    "positive_x_inner_middle_pauldron_lap": (
        (0.220, -0.125, 1.340),
        (0.305, 0.125, 1.505),
        "positive-X inner-to-middle pauldron lap only",
    ),
    "positive_x_middle_outer_pauldron_lap": (
        (0.280, -0.120, 1.320),
        (0.365, 0.120, 1.485),
        "positive-X middle-to-outer pauldron lap only",
    ),
    "negative_x_inner_outer_pauldron_lap": (
        (-0.320, -0.125, 1.325),
        (-0.225, 0.125, 1.505),
        "negative-X inner-to-outer pauldron lap only",
    ),
    "armor_neckline_border": (
        (-0.235, -0.285, 1.170),
        (0.235, 0.205, 1.365),
        "tailored armor neckline border",
    ),
    "armor_negative_x_armhole_border": (
        (-0.345, -0.235, 1.080),
        (-0.175, 0.205, 1.345),
        "negative-X tailored armor armhole border",
    ),
    "armor_positive_x_armhole_border": (
        (0.175, -0.235, 1.080),
        (0.345, 0.205, 1.345),
        "positive-X tailored armor armhole border",
    ),
    "armor_waist_border": (
        (-0.345, -0.285, 0.940),
        (0.345, 0.250, 1.045),
        "tailored armor and base-cloth waist border",
    ),
    "negative_x_bracer_proximal_border": (
        (-0.535, -0.165, 1.270),
        (-0.455, 0.165, 1.565),
        "negative-X bracer proximal border only",
    ),
    "negative_x_bracer_distal_cuff": (
        (-0.755, -0.150, 1.285),
        (-0.675, 0.150, 1.555),
        "negative-X bracer distal cuff only",
    ),
    "positive_x_bracer_proximal_border": (
        (0.455, -0.165, 1.270),
        (0.535, 0.165, 1.565),
        "positive-X bracer proximal border only",
    ),
    "positive_x_bracer_distal_cuff": (
        (0.675, -0.150, 1.285),
        (0.755, 0.150, 1.555),
        "positive-X bracer distal cuff only",
    ),
    "negative_x_greave_lower_border": (
        (-0.285, -0.185, 0.095),
        (-0.025, 0.185, 0.175),
        "negative-X greave lower border only",
    ),
    "negative_x_greave_upper_border": (
        (-0.285, -0.185, 0.440),
        (-0.025, 0.185, 0.525),
        "negative-X greave upper border only",
    ),
    "positive_x_greave_lower_border": (
        (0.025, -0.185, 0.095),
        (0.285, 0.185, 0.175),
        "positive-X greave lower border only",
    ),
    "positive_x_greave_upper_border": (
        (0.025, -0.185, 0.440),
        (0.285, 0.185, 0.525),
        "positive-X greave upper border only",
    ),
    "negative_x_ankle": (
        (-0.270, -0.175, 0.075),
        (-0.025, 0.175, 0.185),
        "negative-X boot, trouser, and greave ankle seat",
    ),
    "positive_x_ankle": (
        (0.025, -0.175, 0.075),
        (0.270, 0.175, 0.185),
        "positive-X boot, trouser, and greave ankle seat",
    ),
    "harness_upper_end": (
        (0.125, -0.205, 1.305),
        (0.225, 0.205, 1.415),
        "concealed upper diagonal-harness termination",
    ),
    "harness_belt_join": (
        (-0.235, -0.260, 0.895),
        (-0.085, 0.235, 1.015),
        "diagonal-harness and wrap-belt waist join",
    ),
    "brigandine_upper_lap": (
        (-0.350, -0.340, 1.245),
        (0.350, 0.190, 1.345),
        "upper front-and-side brigandine lap seam",
    ),
    "brigandine_middle_lap": (
        (-0.350, -0.340, 1.130),
        (0.350, 0.190, 1.230),
        "middle front-and-side brigandine lap seam",
    ),
    "brigandine_lower_lap": (
        (-0.350, -0.340, 1.015),
        (0.350, 0.190, 1.120),
        "lower front-and-side brigandine lap seam",
    ),
    "negative_x_hip_join": (
        (-0.300, -0.240, 0.775),
        (-0.010, 0.240, 0.900),
        "negative-X pelvis-to-trouser-leg join",
    ),
    "positive_x_hip_join": (
        (0.010, -0.240, 0.775),
        (0.300, 0.240, 0.900),
        "positive-X pelvis-to-trouser-leg join",
    ),
    "base_waist_join": (
        (-0.350, -0.300, 0.915),
        (0.350, 0.280, 1.005),
        "tunic-to-pelvis base-garment join",
    ),
    "negative_x_boot_sole_join": (
        (-0.300, -0.310, -0.005),
        (-0.015, 0.175, 0.075),
        "negative-X boot upper-to-sole sidewall join",
    ),
    "positive_x_boot_sole_join": (
        (0.015, -0.310, -0.005),
        (0.300, 0.175, 0.075),
        "positive-X boot upper-to-sole sidewall join",
    ),
    "quiver_mouth": (
        (0.125, 0.295, 1.345),
        (0.340, 0.460, 1.505),
        "open quiver mouth, separate rim, and exiting arrow shafts",
    ),
    "quiver_fletching": (
        (0.115, 0.315, 1.490),
        (0.340, 0.455, 1.765),
        "stored-arrow shaft and paired fletching attachment",
    ),
    "bow_lower_nock": (
        (0.705, -0.230, 0.650),
        (0.825, 0.180, 0.810),
        "lower bow-string nock",
    ),
    "bow_upper_nock": (
        (0.705, -0.230, 1.955),
        (0.825, 0.180, 2.115),
        "upper bow-string nock",
    ),
    "bow_arrow_rest": (
        (0.705, -0.240, 1.300),
        (0.825, 0.100, 1.450),
        "nocked-arrow crossing at the bow rest",
    ),
    "draw_nock": (
        (0.705, 0.145, 1.300),
        (0.825, 0.285, 1.450),
        "draw string and arrow nock",
    ),
    "nocked_arrow_head": (
        (0.700, -0.850, 1.300),
        (0.830, -0.690, 1.450),
        "nocked-arrow shaft and head attachment",
    ),
    "nocked_arrow_fletching": (
        (0.700, 0.070, 1.300),
        (0.830, 0.245, 1.450),
        "nocked-arrow shaft and fletching attachment",
    ),
}

DESIGNED_LAYER_EXCEPTION_ZONES = {
    pair_key("HunterBodyV3_BaseGarments", "HunterBodyV3_TailoredBrigandine"): (
        "armor_neckline_border",
        "armor_negative_x_armhole_border",
        "armor_positive_x_armhole_border",
        "armor_waist_border",
    ),
    pair_key("HunterBodyV3_BaseGarments", "HunterBodyV3_Cowl"): (
        "base_cowl_neck_seam",
    ),
    pair_key("HunterBodyV3_BaseGarments", "HunterBodyV3_SplitCape"): (
        "base_cape_yoke_seam",
    ),
    pair_key("HunterBodyV3_BaseGarments", "HunterBodyV3_LimbGear"): (
        "negative_x_bracer_proximal_border",
        "negative_x_bracer_distal_cuff",
        "positive_x_bracer_proximal_border",
        "positive_x_bracer_distal_cuff",
        "negative_x_greave_lower_border",
        "negative_x_greave_upper_border",
        "positive_x_greave_lower_border",
        "positive_x_greave_upper_border",
    ),
    pair_key("HunterBodyV3_BaseGarments", "HunterBodyV3_Boots"): (
        "negative_x_ankle", "positive_x_ankle"
    ),
    pair_key("HunterBodyV3_LimbGear", "HunterBodyV3_Boots"): (
        "negative_x_ankle", "positive_x_ankle"
    ),
    pair_key("HunterBodyV3_HoodHat", "HunterBodyV3_Cowl"): (
        "hood_cowl_neck_seam",
    ),
    pair_key("HunterBodyV3_Cowl", "HunterBodyV3_TailoredBrigandine"): (
        "cowl_brigandine_neck_seam",
    ),
    pair_key("HunterBodyV3_Cowl", "HunterBodyV3_AsymmetricMantle"): (
        "cowl_positive_x_mantle_root",
    ),
    pair_key("HunterBodyV3_Cowl", "HunterBodyV3_LayeredPauldrons"): (
        "cowl_negative_x_pauldron_root", "cowl_positive_x_pauldron_root"
    ),
    pair_key("HunterBodyV3_Cowl", "HunterBodyV3_SplitCape"): (
        "cowl_cape_yoke_seam",
    ),
    pair_key("HunterBodyV3_AsymmetricMantle", "HunterBodyV3_LayeredPauldrons"): (
        "mantle_positive_x_inner_shell_seat",
        "mantle_positive_x_middle_shell_seat",
        "mantle_positive_x_outer_shell_seat",
    ),
    pair_key("HunterBodyV3_AsymmetricMantle", "HunterBodyV3_SplitCape"): (
        "mantle_positive_x_cape_yoke",
    ),
    pair_key("HunterBodyV3_LayeredPauldrons", "HunterBodyV3_SplitCape"): (
        "pauldron_negative_x_cape_yoke", "pauldron_positive_x_cape_yoke"
    ),
    pair_key("HunterBodyV3_TailoredBrigandine", "HunterBodyV3_HarnessBelt"): (
        "harness_upper_end", "harness_belt_join"
    ),
    pair_key("HunterBodyV3_Cowl", "HunterBodyV3_HarnessBelt"): ("harness_upper_end",),
    pair_key("HunterBodyV3_RecurveBow", "HunterBodyV3_StaticString"): (
        "bow_lower_nock", "bow_upper_nock"
    ),
    pair_key("HunterBodyV3_RecurveBow", "HunterBodyV3_DrawString"): (
        "bow_lower_nock", "bow_upper_nock"
    ),
    pair_key("HunterBodyV3_RecurveBow", "HunterBodyV3_NockedArrow"): ("bow_arrow_rest",),
    pair_key("HunterBodyV3_StaticString", "HunterBodyV3_DrawString"): (
        "bow_lower_nock", "bow_upper_nock"
    ),
    pair_key("HunterBodyV3_DrawString", "HunterBodyV3_NockedArrow"): ("draw_nock",),
    pair_key("HunterBodyV3_StaticString", "HunterBodyV3_NockedArrow"): (
        "bow_arrow_rest", "draw_nock"
    ),
}

# These states can never be rendered together.  Their shared endpoints remain
# subject to the same count and AABB bounds, but a passing attachment is not a
# visible-art warning in the default Static evidence state.
MUTUALLY_EXCLUSIVE_WEAPON_STATE_EXCEPTION_PAIRS = {
    pair_key("HunterBodyV3_StaticString", "HunterBodyV3_DrawString"),
    pair_key("HunterBodyV3_StaticString", "HunterBodyV3_NockedArrow"),
}

INTRA_OBJECT_ISLAND_EXCEPTIONS = {
    island_pair_key("HunterBodyV3_BaseGarments", "torso", "negativeXSleeve"): (
        64,
        "sleeve is authored into the torso shoulder opening",
        ("base_negative_x_shoulder_join",),
    ),
    island_pair_key("HunterBodyV3_BaseGarments", "torso", "positiveXSleeve"): (
        64,
        "sleeve is authored into the torso shoulder opening",
        ("base_positive_x_shoulder_join",),
    ),
    island_pair_key("HunterBodyV3_BaseGarments", "torso", "pelvisBridge"): (
        64, "tunic and pelvis cloth meet at the waist", ("base_waist_join",)
    ),
    island_pair_key("HunterBodyV3_BaseGarments", "pelvisBridge", "negativeXTrouserLeg"): (
        64, "trouser leg meets the pelvis bridge", ("negative_x_hip_join",)
    ),
    island_pair_key("HunterBodyV3_BaseGarments", "pelvisBridge", "positiveXTrouserLeg"): (
        64, "trouser leg meets the pelvis bridge", ("positive_x_hip_join",)
    ),
    island_pair_key("HunterBodyV3_TailoredBrigandine", "mainArmorShell", "upperLapSeam"): (
        64, "upper lap ribbon sits on the continuous armor shell", ("brigandine_upper_lap",)
    ),
    island_pair_key("HunterBodyV3_TailoredBrigandine", "mainArmorShell", "middleLapSeam"): (
        64, "middle lap ribbon sits on the continuous armor shell", ("brigandine_middle_lap",)
    ),
    island_pair_key("HunterBodyV3_TailoredBrigandine", "mainArmorShell", "lowerLapSeam"): (
        64, "lower lap ribbon sits on the continuous armor shell", ("brigandine_lower_lap",)
    ),
    island_pair_key("HunterBodyV3_LayeredPauldrons", "positiveXInnerShell", "positiveXMiddleShell"): (
        64,
        "positive-X inner and middle shells overlap only at their authored lap",
        ("positive_x_inner_middle_pauldron_lap",),
    ),
    island_pair_key("HunterBodyV3_LayeredPauldrons", "positiveXMiddleShell", "positiveXOuterShell"): (
        64,
        "positive-X middle and outer shells overlap only at their authored lap",
        ("positive_x_middle_outer_pauldron_lap",),
    ),
    island_pair_key("HunterBodyV3_LayeredPauldrons", "negativeXInnerShell", "negativeXOuterShell"): (
        64,
        "negative-X inner and outer shells overlap only at their authored lap",
        ("negative_x_inner_outer_pauldron_lap",),
    ),
    island_pair_key("HunterBodyV3_Boots", "negativeXBootUpper", "negativeXSoleSidewall"): (
        128, "negative-X boot upper meets its sole sidewall", ("negative_x_boot_sole_join",)
    ),
    island_pair_key("HunterBodyV3_Boots", "positiveXBootUpper", "positiveXSoleSidewall"): (
        128, "positive-X boot upper meets its sole sidewall", ("positive_x_boot_sole_join",)
    ),
    island_pair_key("HunterBodyV3_HarnessBelt", "diagonalHarness", "wrapBelt"): (
        64, "diagonal harness terminates beneath the wrap belt", ("harness_belt_join",)
    ),
    island_pair_key("HunterBodyV3_QuiverArrows", "quiverBody", "quiverRim"): (
        64, "separate rim meets the open quiver mouth", ("quiver_mouth",)
    ),
    island_pair_key("HunterBodyV3_NockedArrow", "shaft", "arrowhead"): (
        32, "arrowhead is attached to the shaft", ("nocked_arrow_head",)
    ),
    island_pair_key("HunterBodyV3_NockedArrow", "shaft", "fletching"): (
        32, "fletching is attached to the shaft", ("nocked_arrow_fletching",)
    ),
}

for _arrow_index in range(5):
    _shaft = f"arrow{_arrow_index}Shaft"
    _negative_vane = f"arrow{_arrow_index}NegativeYVane"
    _positive_vane = f"arrow{_arrow_index}PositiveYVane"
    INTRA_OBJECT_ISLAND_EXCEPTIONS[
        island_pair_key("HunterBodyV3_QuiverArrows", "quiverBody", _shaft)
    ] = (32, "stored shaft exits through the open quiver mouth", ("quiver_mouth",))
    INTRA_OBJECT_ISLAND_EXCEPTIONS[
        island_pair_key("HunterBodyV3_QuiverArrows", "quiverRim", _shaft)
    ] = (32, "stored shaft passes the separately authored mouth rim", ("quiver_mouth",))
    INTRA_OBJECT_ISLAND_EXCEPTIONS[
        island_pair_key("HunterBodyV3_QuiverArrows", _shaft, _negative_vane)
    ] = (32, "negative-Y fletching vane attaches to its shaft", ("quiver_fletching",))
    INTRA_OBJECT_ISLAND_EXCEPTIONS[
        island_pair_key("HunterBodyV3_QuiverArrows", _shaft, _positive_vane)
    ] = (32, "positive-Y fletching vane attaches to its shaft", ("quiver_fletching",))
    INTRA_OBJECT_ISLAND_EXCEPTIONS[
        island_pair_key("HunterBodyV3_QuiverArrows", _negative_vane, _positive_vane)
    ] = (32, "paired fletching vanes share the shaft attachment", ("quiver_fletching",))

FORBIDDEN_BOUNDARY_TRUE_KEYS = {
    "manualArtAcceptance",
    "uvsAuthored",
    "pbrMapsAuthored",
    "lodsAuthored",
    "fbxWritten",
    "unityIntegrationPerformed",
    "promotionAllowed",
    "garmentWeightsAuthored",
    "garmentDeformationTested",
}

SHA256_PATTERN = re.compile(r"[0-9a-f]{64}")
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
COINCIDENT_VERTEX_TOLERANCE_METERS = 1e-7


@dataclass(frozen=True)
class GeometryFingerprint:
    source_object: str
    evidence_object: str
    vertex_count: int
    triangle_count: int
    world_geometry_sha256: str


@dataclass(frozen=True)
class BodyGeometry:
    vertices: tuple[tuple[float, float, float], ...]
    triangles: tuple[tuple[int, int, int], ...]
    landmarks: dict[str, tuple[float, float, float]]
    evidence_fingerprints: dict[str, GeometryFingerprint]


@dataclass
class IslandSnapshot:
    index: int
    label: str
    vertex_indices: list[int]
    polygon_indices: list[int]
    triangles: list[tuple[int, int, int]]
    triangle_polygon_indices: list[int]
    tree: BVHTree
    bounds: dict[str, list[float]]
    integrity: dict[str, int]
    integrity_examples: dict[str, list[object]]


@dataclass
class MeshSnapshot:
    name: str
    vertices: list[Vector]
    edges: list[tuple[int, int]]
    polygons: list[tuple[int, ...]]
    triangles: list[tuple[int, int, int]]
    tree: BVHTree
    integrity: dict[str, int]
    uv_layer_count: int
    islands: list[IslandSnapshot]


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser(
        description="Verify a completed and externally pinned Hunter BodyV3 R2 checkpoint."
    )
    parser.add_argument("--expected-report-sha256", required=True)
    parser.add_argument("--expected-blend-sha256", required=True)
    args = parser.parse_args(raw)
    for label, value in (
        ("report", args.expected_report_sha256),
        ("blend", args.expected_blend_sha256),
    ):
        if SHA256_PATTERN.fullmatch(value) is None:
            parser.error(f"--expected-{label}-sha256 must be one lowercase SHA-256 value")
    return args


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
    return str(path.resolve().relative_to(PROJECT_ROOT.resolve()))


def project_path(value: str) -> Path:
    path = (PROJECT_ROOT / value).resolve()
    require(path.is_relative_to(PROJECT_ROOT.resolve()), f"Path escapes project: {value}")
    return path


def json_object(path: Path) -> dict[str, object]:
    value = json.loads(path.read_text(encoding="utf-8"))
    require(isinstance(value, dict), f"Expected JSON object: {path}")
    return value


def png_dimensions(path: Path) -> tuple[int, int]:
    with path.open("rb") as stream:
        header = stream.read(24)
    require(len(header) == 24 and header[:8] == PNG_SIGNATURE, f"Invalid PNG: {path}")
    require(header[12:16] == b"IHDR", f"PNG has no leading IHDR: {path}")
    return struct.unpack(">II", header[16:24])


def verify_r1_frozen() -> dict[str, str]:
    verified: dict[str, str] = {}
    for rel, expected in R1_FROZEN_HASHES.items():
        path = project_path(rel)
        require(path.is_file(), f"Frozen R1 file is missing: {rel}")
        actual = sha256(path)
        require(actual == expected, f"Frozen R1 changed: {rel}: {actual}")
        verified[rel] = actual
    return verified


def verify_r2_preflight(
    expected_report_sha256: str,
    expected_blend_sha256: str,
) -> tuple[dict[str, object], dict[str, object], dict[str, str], dict[str, str]]:
    require(R2_ROOT.is_dir(), "R2 output directory does not exist; run only after R2 is complete")
    required = (R2_BLEND, R2_REPORT, R2_REVIEW_REQUEST)
    require(all(path.is_file() for path in required), "R2 blend/report/review request is incomplete")
    require(not R2_INTERNAL_REVIEW.exists(), "R2 already has a manual verdict; verifier expects pending review")
    require(
        not INDEPENDENT_ROOT.exists(),
        "IndependentVerification already exists; pre-verification R2 must be pristine",
    )

    actual_report_sha256 = sha256(R2_REPORT)
    actual_blend_sha256 = sha256(R2_BLEND)
    require(
        actual_report_sha256 == expected_report_sha256,
        f"R2 report does not match the caller pin: {actual_report_sha256}",
    )
    require(
        actual_blend_sha256 == expected_blend_sha256,
        f"R2 blend does not match the caller pin: {actual_blend_sha256}",
    )

    report = json_object(R2_REPORT)
    review = json_object(R2_REVIEW_REQUEST)
    require(report.get("assetId") == ASSET_ID, "R2 report assetId changed")
    require(report.get("blend") == relative(R2_BLEND), "R2 report blend path changed")
    require(report.get("blendSha256") == actual_blend_sha256, "R2 report blend pin changed")
    require(review.get("report") == relative(R2_REPORT), "R2 review report path changed")
    require(review.get("reportSha256") == actual_report_sha256, "R2 review report pin changed")

    evidence = report.get("evidence")
    require(isinstance(evidence, dict) and evidence, "R2 report has no pinned evidence map")
    evidence_hashes: dict[str, str] = {}
    for rel, expected in evidence.items():
        require(isinstance(rel, str) and isinstance(expected, str), "Malformed evidence pin")
        require(SHA256_PATTERN.fullmatch(expected) is not None, f"Malformed evidence SHA: {rel}")
        path = project_path(rel)
        require(path.parent == (R2_ROOT / "Evidence").resolve(), f"Evidence escaped exact R2/Evidence directory: {rel}")
        require(path.is_file(), f"Pinned R2 evidence is missing: {rel}")
        actual = sha256(path)
        require(actual == expected, f"Pinned R2 evidence changed: {rel}: {actual}")
        evidence_hashes[rel] = actual

    reported_names = {Path(rel).name for rel in evidence_hashes}
    require(
        reported_names == REQUIRED_EVIDENCE_NAMES
        and len(evidence_hashes) == len(REQUIRED_EVIDENCE_NAMES),
        "R2 evidence must contain exactly the seven required views; "
        f"missing={sorted(REQUIRED_EVIDENCE_NAMES - reported_names)}, "
        f"extra={sorted(reported_names - REQUIRED_EVIDENCE_NAMES)}",
    )
    disk_pngs = {
        relative(path): sha256(path)
        for path in (R2_ROOT / "Evidence").glob("*.png")
        if path.is_file()
    }
    require(set(disk_pngs) == set(evidence_hashes), "R2 has unpinned or missing evidence PNGs")

    expected_disk_files = {
        R2_BLEND.resolve(),
        R2_REPORT.resolve(),
        R2_REVIEW_REQUEST.resolve(),
        *(project_path(rel) for rel in evidence_hashes),
    }
    actual_disk_files = {
        path.resolve() for path in R2_ROOT.rglob("*") if path.is_file()
    }
    require(
        actual_disk_files == expected_disk_files,
        "Pre-verification R2 disk file set changed; "
        f"missing={sorted(relative(path) for path in expected_disk_files - actual_disk_files)}, "
        f"extra={sorted(relative(path) for path in actual_disk_files - expected_disk_files)}",
    )
    expected_disk_directories = {
        R2_ROOT.resolve(),
        (R2_ROOT / "Evidence").resolve(),
    }
    actual_disk_directories = {
        R2_ROOT.resolve(),
        *(path.resolve() for path in R2_ROOT.rglob("*") if path.is_dir()),
    }
    require(
        actual_disk_directories == expected_disk_directories,
        "Pre-verification R2 disk directory set changed; "
        f"missing={sorted(relative(path) for path in expected_disk_directories - actual_disk_directories)}, "
        f"extra={sorted(relative(path) for path in actual_disk_directories - expected_disk_directories)}",
    )

    review_evidence = review.get("evidence")
    require(isinstance(review_evidence, list), "R2 manual review evidence list is missing")
    require(set(review_evidence) == set(evidence_hashes), "R2 manual review evidence differs from report")

    verified_inputs = report.get("verifiedInputs")
    require(isinstance(verified_inputs, dict) and verified_inputs, "R2 report has no input pins")
    input_hashes: dict[str, str] = {}
    for rel, expected in verified_inputs.items():
        require(isinstance(rel, str) and isinstance(expected, str), "Malformed R2 input pin")
        path = project_path(rel)
        require(path.is_file(), f"Pinned R2 input is missing: {rel}")
        actual = sha256(path)
        require(actual == expected, f"Pinned R2 input changed: {rel}: {actual}")
        input_hashes[rel] = actual

    script_rel = report.get("script")
    script_pin = report.get("scriptSha256")
    require(isinstance(script_rel, str) and isinstance(script_pin, str), "R2 builder pin is missing")
    builder_path = project_path(script_rel)
    require(
        builder_path.parent == SCRIPT_PATH.parent and "r2" in builder_path.stem.lower(),
        "R2 report points outside the dedicated R2 authoring builder",
    )
    require(sha256(builder_path) == script_pin, "Pinned R2 authoring builder changed")

    return report, review, evidence_hashes, input_hashes


def evaluated_world_geometry(
    obj: bpy.types.Object,
    depsgraph: bpy.types.Depsgraph,
) -> tuple[
    tuple[tuple[float, float, float], ...],
    tuple[tuple[int, int, int], ...],
]:
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
    try:
        mesh.calc_loop_triangles()
        matrix = evaluated.matrix_world.copy()
        vertices = tuple(
            tuple(float(component) for component in matrix @ vertex.co)
            for vertex in mesh.vertices
        )
        triangles = tuple(
            tuple(int(index) for index in triangle.vertices)
            for triangle in mesh.loop_triangles
        )
    finally:
        evaluated.to_mesh_clear()
    require(vertices and triangles, f"Evaluated evidence geometry is empty: {obj.name}")
    return vertices, triangles


def quantized_world_geometry_sha256(
    vertices: tuple[tuple[float, float, float], ...],
    triangles: tuple[tuple[int, int, int], ...],
) -> str:
    """Hash material/datablock/index-order-independent world-space geometry."""
    quantized_vertices = [
        tuple(round(component / GEOMETRY_QUANTIZATION_METERS) for component in vertex)
        for vertex in vertices
    ]
    canonical_vertices = sorted(quantized_vertices)
    canonical_triangles = sorted(
        tuple(sorted(quantized_vertices[index] for index in triangle))
        for triangle in triangles
    )
    digest = hashlib.sha256()
    digest.update(b"HunterBodyV3EvidenceWorldGeometryV1\0")
    digest.update(struct.pack("<QQ", len(canonical_vertices), len(canonical_triangles)))
    for vertex in canonical_vertices:
        digest.update(struct.pack("<qqq", *vertex))
    for triangle in canonical_triangles:
        for vertex in triangle:
            digest.update(struct.pack("<qqq", *vertex))
    return digest.hexdigest()


def geometry_fingerprint(
    source_object: str,
    evidence_object: str,
    vertices: tuple[tuple[float, float, float], ...],
    triangles: tuple[tuple[int, int, int], ...],
) -> GeometryFingerprint:
    return GeometryFingerprint(
        source_object=source_object,
        evidence_object=evidence_object,
        vertex_count=len(vertices),
        triangle_count=len(triangles),
        world_geometry_sha256=quantized_world_geometry_sha256(vertices, triangles),
    )


def extract_accepted_body_geometry() -> BodyGeometry:
    require(BODY_BLEND.is_file(), "Accepted BodyV3 blend is missing")
    require(sha256(BODY_BLEND) == BODY_BLEND_SHA256, "Accepted BodyV3 blend changed")
    bpy.ops.wm.open_mainfile(filepath=str(BODY_BLEND), load_ui=False)
    require(BODY_NAME in bpy.data.objects, "Accepted masculine body object is missing")
    require(BODY_RIG_NAME in bpy.data.objects, "Accepted masculine rig object is missing")
    missing_eyes = sorted(set(BODY_EYE_NAMES) - set(bpy.data.objects.keys()))
    require(not missing_eyes, f"Accepted masculine eye objects are missing: {missing_eyes}")
    rig = bpy.data.objects[BODY_RIG_NAME]
    require(rig.type == "ARMATURE", "Accepted BodyV3 rig type changed")
    required_bones = {"LeftUpperArm", "LeftFoot", "LeftToes", "Neck", "Head"}
    missing_bones = sorted(required_bones - set(rig.data.bones.keys()))
    require(not missing_bones, f"Accepted BodyV3 rig landmarks are missing: {missing_bones}")
    for bone in rig.pose.bones:
        bone.matrix_basis = Matrix.Identity(4)
    for key in tuple(rig.keys()):
        if key.startswith("PSC_"):
            rig[key] = 0.0
    rig.update_tag(refresh={"OBJECT"})
    bpy.context.view_layer.update()

    depsgraph = bpy.context.evaluated_depsgraph_get()
    source_geometry: dict[
        str,
        tuple[
            tuple[tuple[float, float, float], ...],
            tuple[tuple[int, int, int], ...],
        ],
    ] = {}
    fingerprints: dict[str, GeometryFingerprint] = {}
    for evidence_name, source_name in EXPECTED_EVIDENCE_OBJECT_SOURCES.items():
        geometry = evaluated_world_geometry(bpy.data.objects[source_name], depsgraph)
        source_geometry[evidence_name] = geometry
        fingerprints[evidence_name] = geometry_fingerprint(
            source_name,
            evidence_name,
            geometry[0],
            geometry[1],
        )
    vertices, triangles = source_geometry[
        "EvidenceOnly_HunterBodyV3_AcceptedMasculineBody"
    ]
    rig_matrix = rig.matrix_world.copy()
    bones = rig.data.bones
    landmark_vectors = {
        "leftShoulder": rig_matrix @ bones["LeftUpperArm"].head_local,
        "leftAnkle": rig_matrix @ bones["LeftFoot"].head_local,
        "leftToes": rig_matrix @ bones["LeftToes"].tail_local,
        "neck": rig_matrix @ bones["Neck"].head_local,
        "headTop": rig_matrix @ bones["Head"].tail_local,
    }
    landmarks = {
        name: tuple(float(component) for component in value)
        for name, value in landmark_vectors.items()
    }
    return BodyGeometry(
        vertices=vertices,
        triangles=triangles,
        landmarks=landmarks,
        evidence_fingerprints=fingerprints,
    )


def connected_islands(bm: bmesh.types.BMesh) -> int:
    remaining = set(bm.verts)
    islands = 0
    while remaining:
        islands += 1
        stack = [remaining.pop()]
        while stack:
            vertex = stack.pop()
            for edge in vertex.link_edges:
                other = edge.other_vert(vertex)
                if other in remaining:
                    remaining.remove(other)
                    stack.append(other)
    return islands


def connected_polygon_islands(polygons: list[tuple[int, ...]]) -> list[list[int]]:
    vertex_polygons: dict[int, set[int]] = {}
    for polygon_index, polygon in enumerate(polygons):
        for vertex_index in polygon:
            vertex_polygons.setdefault(vertex_index, set()).add(polygon_index)
    remaining = set(range(len(polygons)))
    islands: list[list[int]] = []
    while remaining:
        seed = min(remaining)
        remaining.remove(seed)
        stack = [seed]
        island: list[int] = []
        while stack:
            polygon_index = stack.pop()
            island.append(polygon_index)
            neighbors: set[int] = set()
            for vertex_index in polygons[polygon_index]:
                neighbors.update(vertex_polygons[vertex_index] & remaining)
            remaining.difference_update(neighbors)
            stack.extend(sorted(neighbors, reverse=True))
        islands.append(sorted(island))
    islands.sort(key=lambda indices: indices[0])
    return islands


def point_bounds(points: list[Vector]) -> dict[str, list[float]]:
    require(bool(points), "Cannot calculate empty point bounds")
    return {
        axis: [
            float(min(point[axis_index] for point in points)),
            float(max(point[axis_index] for point in points)),
        ]
        for axis, axis_index in (("x", 0), ("y", 1), ("z", 2))
    }


def coincident_vertex_audit(
    vertices: list[Vector],
    vertex_indices: list[int],
) -> tuple[int, list[object]]:
    tolerance = COINCIDENT_VERTEX_TOLERANCE_METERS
    buckets: dict[tuple[int, int, int], list[int]] = {}
    count = 0
    examples: list[object] = []
    for vertex_index in sorted(vertex_indices):
        point = vertices[vertex_index]
        bucket = tuple(math.floor(float(component) / tolerance) for component in point)
        for x_offset in (-1, 0, 1):
            for y_offset in (-1, 0, 1):
                for z_offset in (-1, 0, 1):
                    neighbor = (
                        bucket[0] + x_offset,
                        bucket[1] + y_offset,
                        bucket[2] + z_offset,
                    )
                    for other_index in buckets.get(neighbor, []):
                        if (point - vertices[other_index]).length <= tolerance:
                            count += 1
                            if len(examples) < 8:
                                examples.append({
                                    "vertices": [other_index, vertex_index],
                                    "distanceMeters": float((point - vertices[other_index]).length),
                                    "positionMeters": [float(component) for component in point],
                                })
        buckets.setdefault(bucket, []).append(vertex_index)
    return count, examples


def coincident_triangle_audit(
    vertices: list[Vector],
    triangles: list[tuple[int, int, int]],
) -> tuple[int, list[object]]:
    tolerance = COINCIDENT_VERTEX_TOLERANCE_METERS

    def point_key(point: Vector) -> tuple[int, int, int]:
        return tuple(round(float(component) / tolerance) for component in point)

    by_geometry: dict[tuple[tuple[int, int, int], ...], list[int]] = {}
    for triangle_index, triangle in enumerate(triangles):
        key = tuple(sorted(point_key(vertices[index]) for index in triangle))
        by_geometry.setdefault(key, []).append(triangle_index)
    count = 0
    examples: list[object] = []
    for indices in by_geometry.values():
        if len(indices) < 2:
            continue
        pair_count = len(indices) * (len(indices) - 1) // 2
        count += pair_count
        if len(examples) < 8:
            examples.append({"triangles": indices[:8], "pairCount": pair_count})
    return count, examples


def project_point_2d(point: Vector, dropped_axis: int) -> tuple[float, float]:
    axes = [axis for axis in range(3) if axis != dropped_axis]
    return float(point[axes[0]]), float(point[axes[1]])


def cross_2d(first: tuple[float, float], second: tuple[float, float]) -> float:
    return first[0] * second[1] - first[1] * second[0]


def subtract_2d(
    first: tuple[float, float],
    second: tuple[float, float],
) -> tuple[float, float]:
    return first[0] - second[0], first[1] - second[1]


def point_in_triangle_2d(
    point: tuple[float, float],
    triangle: tuple[tuple[float, float], tuple[float, float], tuple[float, float]],
    tolerance: float = 1e-9,
) -> bool:
    signs = []
    for index in range(3):
        first = triangle[index]
        second = triangle[(index + 1) % 3]
        signs.append(cross_2d(subtract_2d(second, first), subtract_2d(point, first)))
    return (
        all(value >= -tolerance for value in signs)
        or all(value <= tolerance for value in signs)
    )


def segment_intersection_parameters_2d(
    first_start: tuple[float, float],
    first_end: tuple[float, float],
    second_start: tuple[float, float],
    second_end: tuple[float, float],
    tolerance: float = 1e-9,
) -> list[float]:
    first_direction = subtract_2d(first_end, first_start)
    second_direction = subtract_2d(second_end, second_start)
    offset = subtract_2d(second_start, first_start)
    denominator = cross_2d(first_direction, second_direction)
    if abs(denominator) > tolerance:
        first_t = cross_2d(offset, second_direction) / denominator
        second_t = cross_2d(offset, first_direction) / denominator
        if -tolerance <= first_t <= 1.0 + tolerance and -tolerance <= second_t <= 1.0 + tolerance:
            return [max(0.0, min(1.0, first_t))]
        return []

    if abs(cross_2d(offset, first_direction)) > tolerance:
        return []
    denominator_squared = (
        first_direction[0] * first_direction[0]
        + first_direction[1] * first_direction[1]
    )
    if denominator_squared <= tolerance * tolerance:
        return [0.0] if (
            (first_start[0] - second_start[0]) ** 2
            + (first_start[1] - second_start[1]) ** 2
            <= tolerance * tolerance
        ) else []
    start_t = (
        offset[0] * first_direction[0] + offset[1] * first_direction[1]
    ) / denominator_squared
    second_offset = subtract_2d(second_end, first_start)
    end_t = (
        second_offset[0] * first_direction[0]
        + second_offset[1] * first_direction[1]
    ) / denominator_squared
    lower = max(0.0, min(start_t, end_t))
    upper = min(1.0, max(start_t, end_t))
    if lower > upper + tolerance:
        return []
    if abs(upper - lower) <= tolerance:
        return [max(0.0, min(1.0, 0.5 * (lower + upper)))]
    return [max(0.0, lower), min(1.0, upper)]


def point_in_triangle_3d(
    point: Vector,
    first: Vector,
    second: Vector,
    third: Vector,
    tolerance: float = 1e-8,
) -> bool:
    first_edge = second - first
    second_edge = third - first
    relative = point - first
    dot00 = first_edge.dot(first_edge)
    dot01 = first_edge.dot(second_edge)
    dot11 = second_edge.dot(second_edge)
    dot20 = relative.dot(first_edge)
    dot21 = relative.dot(second_edge)
    denominator = dot00 * dot11 - dot01 * dot01
    if abs(denominator) <= tolerance * tolerance:
        return False
    first_weight = (dot11 * dot20 - dot01 * dot21) / denominator
    second_weight = (dot00 * dot21 - dot01 * dot20) / denominator
    return (
        first_weight >= -tolerance
        and second_weight >= -tolerance
        and first_weight + second_weight <= 1.0 + tolerance
    )


def coplanar_segment_triangle_contacts(
    start: Vector,
    end: Vector,
    first: Vector,
    second: Vector,
    third: Vector,
    normal: Vector,
) -> list[Vector]:
    dropped_axis = max(range(3), key=lambda axis: abs(normal[axis]))
    start_2d = project_point_2d(start, dropped_axis)
    end_2d = project_point_2d(end, dropped_axis)
    triangle_2d = tuple(
        project_point_2d(point, dropped_axis)
        for point in (first, second, third)
    )
    parameters: list[float] = []
    if point_in_triangle_2d(start_2d, triangle_2d):
        parameters.append(0.0)
    if point_in_triangle_2d(end_2d, triangle_2d):
        parameters.append(1.0)
    for index in range(3):
        parameters.extend(segment_intersection_parameters_2d(
            start_2d,
            end_2d,
            triangle_2d[index],
            triangle_2d[(index + 1) % 3],
        ))
    unique_parameters: list[float] = []
    for value in sorted(parameters):
        if not unique_parameters or abs(value - unique_parameters[-1]) > 1e-8:
            unique_parameters.append(value)
    return [start.lerp(end, value) for value in unique_parameters]


def segment_triangle_contacts(
    start: Vector,
    end: Vector,
    first: Vector,
    second: Vector,
    third: Vector,
) -> list[Vector]:
    normal = (second - first).cross(third - first)
    normal_length = normal.length
    if normal_length <= 1e-12:
        # Degenerate triangles are an independent integrity failure.  Returning
        # the segment midpoint here also makes this self-audit fail closed.
        return [(start + end) * 0.5]
    normal /= normal_length
    start_distance = normal.dot(start - first)
    end_distance = normal.dot(end - first)
    plane_tolerance = 1e-8
    if abs(start_distance) <= plane_tolerance and abs(end_distance) <= plane_tolerance:
        return coplanar_segment_triangle_contacts(
            start, end, first, second, third, normal
        )
    denominator = start_distance - end_distance
    if abs(denominator) <= plane_tolerance:
        return []
    parameter = start_distance / denominator
    if not -plane_tolerance <= parameter <= 1.0 + plane_tolerance:
        return []
    parameter = max(0.0, min(1.0, parameter))
    contact = start.lerp(end, parameter)
    if point_in_triangle_3d(contact, first, second, third):
        return [contact]
    return []


def one_shared_vertex_crosses_away_from_contact(
    vertices: list[Vector],
    first_triangle: tuple[int, int, int],
    second_triangle: tuple[int, int, int],
    shared_vertex_index: int,
) -> tuple[bool, list[list[float]]]:
    shared_point = vertices[shared_vertex_index]
    first_points = tuple(vertices[index] for index in first_triangle)
    second_points = tuple(vertices[index] for index in second_triangle)
    contacts: list[Vector] = []
    for points, target in (
        (first_points, second_points),
        (second_points, first_points),
    ):
        for index in range(3):
            contacts.extend(segment_triangle_contacts(
                points[index],
                points[(index + 1) % 3],
                target[0],
                target[1],
                target[2],
            ))
    away = [
        point for point in contacts
        if (point - shared_point).length > 1e-7
    ]
    examples = [
        [float(component) for component in point]
        for point in away[:4]
    ]
    return bool(away), examples


def self_overlap_audit(
    vertices: list[Vector],
    tree: BVHTree,
    triangles: list[tuple[int, int, int]],
    triangle_polygon_indices: list[int],
) -> tuple[dict[str, int], dict[str, list[object]]]:
    normalized_pairs = {
        tuple(sorted((int(first), int(second))))
        for first, second in tree.overlap(tree)
        if first != second
    }
    crossings: set[tuple[int, int]] = set()
    edge_neighbors = 0
    same_polygon_pairs = 0
    one_vertex_candidates = 0
    one_vertex_contact_only = 0
    one_vertex_crossings = 0
    contact_only_examples: list[object] = []
    crossing_examples: list[object] = []
    for first, second in sorted(normalized_pairs):
        shared = set(triangles[first]) & set(triangles[second])
        if len(shared) >= 2:
            edge_neighbors += 1
            continue
        if len(shared) == 1:
            one_vertex_candidates += 1
            if triangle_polygon_indices[first] == triangle_polygon_indices[second]:
                # A Blender n-gon is evaluated as multiple loop triangles.  Its
                # own triangulation fan is one source polygon, not a self-cross.
                same_polygon_pairs += 1
                continue
            shared_vertex = next(iter(shared))
            crosses, contacts = one_shared_vertex_crosses_away_from_contact(
                vertices,
                triangles[first],
                triangles[second],
                shared_vertex,
            )
            if crosses:
                crossings.add((first, second))
                one_vertex_crossings += 1
                if len(crossing_examples) < 8:
                    crossing_examples.append({
                        "triangles": [first, second],
                        "sharedVertex": shared_vertex,
                        "contactsAwayFromSharedVertexMeters": contacts,
                    })
            else:
                one_vertex_contact_only += 1
                if len(contact_only_examples) < 8:
                    contact_only_examples.append({
                        "triangles": [first, second],
                        "sharedVertex": shared_vertex,
                        "sharedPositionMeters": [
                            float(component) for component in vertices[shared_vertex]
                        ],
                    })
            continue
        crossings.add((first, second))
        if len(crossing_examples) < 8:
            crossing_examples.append({"triangles": [first, second]})
    return (
        {
            "bvhOverlapPairsNormalized": len(normalized_pairs),
            "topologicalEdgeNeighborPairsIgnored": edge_neighbors,
            "samePolygonTriangulationPairsIgnored": same_polygon_pairs,
            "oneSharedVertexCandidatePairs": one_vertex_candidates,
            "oneSharedVertexContactOnlyPairsIgnored": one_vertex_contact_only,
            "oneSharedVertexCrossingPairs": one_vertex_crossings,
            "selfIntersectionPairsExcludingTrueNeighbors": len(crossings),
        },
        {
            "oneSharedVertexContactOnly": contact_only_examples,
            "selfIntersectionsExcludingTrueNeighbors": crossing_examples,
        },
    )


def island_snapshots(
    object_name: str,
    vertices: list[Vector],
    edges: list[tuple[int, int]],
    polygons: list[tuple[int, ...]],
    triangle_records: list[tuple[tuple[int, int, int], int]],
) -> list[IslandSnapshot]:
    polygon_islands = connected_polygon_islands(polygons)
    expected_labels = EXPECTED_ISLAND_LABELS.get(object_name)
    if expected_labels is not None and len(expected_labels) == len(polygon_islands):
        labels = list(expected_labels)
    else:
        labels = [f"island{index:02d}" for index in range(len(polygon_islands))]

    snapshots: list[IslandSnapshot] = []
    for island_index, polygon_indices in enumerate(polygon_islands):
        polygon_set = set(polygon_indices)
        selected_triangle_records = [
            (triangle, polygon_index)
            for triangle, polygon_index in triangle_records
            if polygon_index in polygon_set
        ]
        triangles = [triangle for triangle, _polygon_index in selected_triangle_records]
        triangle_polygon_indices = [
            polygon_index for _triangle, polygon_index in selected_triangle_records
        ]
        vertex_indices = sorted({
            vertex_index
            for polygon_index in polygon_indices
            for vertex_index in polygons[polygon_index]
        })
        vertex_set = set(vertex_indices)
        island_edges = [
            edge for edge in edges if edge[0] in vertex_set and edge[1] in vertex_set
        ]
        edge_face_counts: dict[tuple[int, int], int] = {}
        for polygon_index in polygon_indices:
            polygon = polygons[polygon_index]
            for cursor, first in enumerate(polygon):
                second = polygon[(cursor + 1) % len(polygon)]
                key = tuple(sorted((first, second)))
                edge_face_counts[key] = edge_face_counts.get(key, 0) + 1
        tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
        duplicate_vertices, duplicate_vertex_examples = coincident_vertex_audit(
            vertices, vertex_indices
        )
        duplicate_triangles, duplicate_triangle_examples = coincident_triangle_audit(
            vertices, triangles
        )
        self_integrity, self_integrity_examples = self_overlap_audit(
            vertices,
            tree,
            triangles,
            triangle_polygon_indices,
        )
        snapshots.append(IslandSnapshot(
            index=island_index,
            label=labels[island_index],
            vertex_indices=vertex_indices,
            polygon_indices=polygon_indices,
            triangles=triangles,
            triangle_polygon_indices=triangle_polygon_indices,
            tree=tree,
            bounds=point_bounds([vertices[index] for index in vertex_indices]),
            integrity={
                "vertices": len(vertex_indices),
                "edges": len(island_edges),
                "faces": len(polygon_indices),
                "triangles": len(triangles),
                "boundaryEdges": sum(1 for count in edge_face_counts.values() if count == 1),
                "coincidentDuplicateVertexPairs": duplicate_vertices,
                "coincidentTrianglePairs": duplicate_triangles,
                **self_integrity,
            },
            integrity_examples={
                "coincidentDuplicateVertices": duplicate_vertex_examples,
                "coincidentTriangles": duplicate_triangle_examples,
                **self_integrity_examples,
            },
        ))
    return snapshots


def mesh_snapshot(obj: bpy.types.Object, depsgraph: bpy.types.Depsgraph) -> MeshSnapshot:
    evaluated = obj.evaluated_get(depsgraph)
    mesh = evaluated.to_mesh(preserve_all_data_layers=True, depsgraph=depsgraph)
    try:
        mesh.calc_loop_triangles()
        matrix = evaluated.matrix_world.copy()
        vertices = [matrix @ vertex.co for vertex in mesh.vertices]
        edges = [tuple(int(index) for index in edge.vertices) for edge in mesh.edges]
        polygons = [tuple(int(index) for index in polygon.vertices) for polygon in mesh.polygons]
        triangle_records = [
            (
                tuple(int(index) for index in tri.vertices),
                int(tri.polygon_index),
            )
            for tri in mesh.loop_triangles
        ]
        triangles = [triangle for triangle, _polygon_index in triangle_records]
        uv_layer_count = len(mesh.uv_layers)

        islands = island_snapshots(
            obj.name,
            vertices,
            edges,
            polygons,
            triangle_records,
        )

        bm = bmesh.new()
        bm.from_mesh(mesh)
        try:
            integrity = {
                "vertices": len(bm.verts),
                "edges": len(bm.edges),
                "faces": len(bm.faces),
                "triangles": len(triangles),
                "boundaryEdges": sum(1 for edge in bm.edges if edge.is_boundary),
                "nonManifoldInteriorEdges": sum(
                    1 for edge in bm.edges if not edge.is_manifold and not edge.is_boundary
                ),
                "wireEdges": sum(1 for edge in bm.edges if not edge.link_faces),
                "isolatedVertices": sum(1 for vertex in bm.verts if not vertex.link_edges),
                "degenerateFaces": sum(1 for face in bm.faces if face.calc_area() <= 1e-10),
                "connectedIslands": connected_islands(bm),
            }
        finally:
            bm.free()
    finally:
        evaluated.to_mesh_clear()
    require(vertices and triangles, f"Evaluated kit mesh is empty: {obj.name}")
    tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
    return MeshSnapshot(
        name=obj.name,
        vertices=vertices,
        edges=edges,
        polygons=polygons,
        triangles=triangles,
        tree=tree,
        integrity=integrity,
        uv_layer_count=uv_layer_count,
        islands=islands,
    )


def material_image_nodes(obj: bpy.types.Object) -> list[str]:
    found: list[str] = []
    for slot in obj.material_slots:
        material = slot.material
        if material is None or not material.use_nodes or material.node_tree is None:
            continue
        for node in material.node_tree.nodes:
            if node.bl_idname in {"ShaderNodeTexImage", "ShaderNodeTexEnvironment"}:
                found.append(f"{material.name}/{node.name}")
    return found


def matrix_world_report(matrix: Matrix) -> tuple[list[list[float]], float]:
    values = [
        [float(matrix[row][column]) for column in range(4)]
        for row in range(4)
    ]
    maximum_identity_error = max(
        abs(values[row][column] - (1.0 if row == column else 0.0))
        for row in range(4)
        for column in range(4)
    )
    return values, maximum_identity_error


def audit_saved_evidence_geometry(
    accepted: BodyGeometry,
    depsgraph: bpy.types.Depsgraph,
    failures: list[str],
) -> dict[str, object]:
    expected_names = set(EXPECTED_EVIDENCE_OBJECT_SOURCES)
    named_evidence = {
        obj.name
        for obj in bpy.data.objects
        if obj.name.startswith("EvidenceOnly_HunterBodyV3_")
    }
    tagged_evidence = {
        obj.name
        for obj in bpy.data.objects
        if obj.get("evidenceOnly") is True
    }
    if named_evidence != expected_names:
        failures.append(
            "saved evidence object-name set changed: "
            f"expected={sorted(expected_names)}, actual={sorted(named_evidence)}"
        )
    if tagged_evidence != expected_names:
        failures.append(
            "saved evidenceOnly tag set changed: "
            f"expected={sorted(expected_names)}, actual={sorted(tagged_evidence)}"
        )

    armature_objects = sorted(
        obj.name for obj in bpy.data.objects if obj.type == "ARMATURE"
    )
    if armature_objects:
        failures.append(
            "armature objects found in frozen R2 evidence scene: "
            + ", ".join(armature_objects)
        )

    object_reports: dict[str, object] = {}
    for evidence_name, source_name in EXPECTED_EVIDENCE_OBJECT_SOURCES.items():
        accepted_fingerprint = accepted.evidence_fingerprints[evidence_name]
        obj = bpy.data.objects.get(evidence_name)
        if obj is None:
            failures.append(f"saved accepted-body evidence object is missing: {evidence_name}")
            continue
        if obj.type != "MESH":
            failures.append(f"saved accepted-body evidence object is not a mesh: {evidence_name}: {obj.type}")
            continue

        matrix_values, matrix_error = matrix_world_report(obj.matrix_world)
        identity_passes = matrix_error <= 1e-9
        if not identity_passes:
            failures.append(
                f"saved accepted-body evidence transform is not identity: "
                f"{evidence_name}: maximumError={matrix_error:.12g}"
            )

        armature_modifiers = sorted(
            modifier.name for modifier in obj.modifiers if modifier.type == "ARMATURE"
        )
        parent_armature = obj.parent is not None and obj.parent.type == "ARMATURE"
        object_animation = obj.animation_data
        mesh_animation = obj.data.animation_data
        object_action = (
            object_animation.action.name
            if object_animation is not None and object_animation.action is not None
            else None
        )
        mesh_action = (
            mesh_animation.action.name
            if mesh_animation is not None and mesh_animation.action is not None
            else None
        )
        nla_track_count = (
            len(object_animation.nla_tracks) if object_animation is not None else 0
        ) + (len(mesh_animation.nla_tracks) if mesh_animation is not None else 0)
        shape_keys_present = obj.data.shape_keys is not None
        if armature_modifiers:
            failures.append(
                f"saved evidence mesh has armature modifiers: {evidence_name}: {armature_modifiers}"
            )
        if parent_armature:
            failures.append(f"saved evidence mesh is armature-parented: {evidence_name}")
        if object_animation is not None or mesh_animation is not None:
            failures.append(
                f"saved evidence mesh has object/mesh animation data: {evidence_name}: "
                f"objectAction={object_action!r}, meshAction={mesh_action!r}, "
                f"nlaTracks={nla_track_count}"
            )
        if shape_keys_present:
            failures.append(f"saved evidence mesh has shape keys: {evidence_name}")
        if obj.get("evidenceOnly") is not True:
            failures.append(f"saved evidence mesh lost evidenceOnly=true: {evidence_name}")
        if obj.get("promotionAllowed") is not False:
            failures.append(f"saved evidence mesh promotionAllowed is not false: {evidence_name}")
        render_visible = not obj.hide_render
        viewport_visible = not obj.hide_get()
        if not render_visible or not viewport_visible:
            failures.append(
                f"saved accepted-body evidence mesh is hidden: {evidence_name}: "
                f"renderVisible={render_visible}, viewportVisible={viewport_visible}"
            )

        vertices, triangles = evaluated_world_geometry(obj, depsgraph)
        actual_fingerprint = geometry_fingerprint(
            source_name,
            evidence_name,
            vertices,
            triangles,
        )
        count_passes = (
            actual_fingerprint.vertex_count == accepted_fingerprint.vertex_count
            and actual_fingerprint.triangle_count == accepted_fingerprint.triangle_count
        )
        hash_passes = (
            actual_fingerprint.world_geometry_sha256
            == accepted_fingerprint.world_geometry_sha256
        )
        if not count_passes:
            failures.append(
                f"saved accepted-body evidence counts differ: {evidence_name}: "
                f"vertices={actual_fingerprint.vertex_count}/"
                f"{accepted_fingerprint.vertex_count}, "
                f"triangles={actual_fingerprint.triangle_count}/"
                f"{accepted_fingerprint.triangle_count}"
            )
        if not hash_passes:
            failures.append(
                f"saved accepted-body evidence world geometry differs: {evidence_name}: "
                f"{actual_fingerprint.world_geometry_sha256} != "
                f"{accepted_fingerprint.world_geometry_sha256}"
            )

        object_reports[evidence_name] = {
            "acceptedSourceObject": source_name,
            "exactEvidenceObjectName": evidence_name,
            "matrixWorld": matrix_values,
            "maximumIdentityMatrixError": matrix_error,
            "identityTransformStatus": "PASS" if identity_passes else "FAIL",
            "acceptedVertexCount": accepted_fingerprint.vertex_count,
            "actualVertexCount": actual_fingerprint.vertex_count,
            "acceptedTriangleCount": accepted_fingerprint.triangle_count,
            "actualTriangleCount": actual_fingerprint.triangle_count,
            "acceptedWorldGeometrySha256": (
                accepted_fingerprint.world_geometry_sha256
            ),
            "actualWorldGeometrySha256": actual_fingerprint.world_geometry_sha256,
            "countsStatus": "PASS" if count_passes else "FAIL",
            "worldGeometryHashStatus": "PASS" if hash_passes else "FAIL",
            "geometryQuantizationMeters": GEOMETRY_QUANTIZATION_METERS,
            "geometryCanonicalization": (
                "sorted quantized world-space vertex multiset plus sorted triangle "
                "coordinate triples; independent of material, datablock, vertex index, "
                "triangle order, and winding"
            ),
            "armatureModifiers": armature_modifiers,
            "parentArmature": parent_armature,
            "objectAction": object_action,
            "meshAction": mesh_action,
            "nlaTrackCount": nla_track_count,
            "shapeKeysPresent": shape_keys_present,
            "evidenceOnly": obj.get("evidenceOnly"),
            "promotionAllowed": obj.get("promotionAllowed"),
            "renderVisible": render_visible,
            "viewportVisible": viewport_visible,
            "materialIndependentComparison": True,
            "status": (
                "PASS_UNCHANGED_ACCEPTED_BODY_EVIDENCE"
                if (
                    identity_passes
                    and not armature_modifiers
                    and not parent_armature
                    and object_animation is None
                    and mesh_animation is None
                    and not shape_keys_present
                    and obj.get("evidenceOnly") is True
                    and obj.get("promotionAllowed") is False
                    and render_visible
                    and viewport_visible
                    and count_passes
                    and hash_passes
                )
                else "FAIL_MUTATED_ACCEPTED_BODY_EVIDENCE"
            ),
        }

    return {
        "status": (
            "PASS_UNCHANGED_ACCEPTED_BODY_AND_EYES"
            if (
                set(object_reports) == expected_names
                and named_evidence == expected_names
                and tagged_evidence == expected_names
                and not armature_objects
                and all(
                    item["status"] == "PASS_UNCHANGED_ACCEPTED_BODY_EVIDENCE"
                    for item in object_reports.values()
                )
            )
            else "FAIL_ACCEPTED_BODY_EVIDENCE_IDENTITY"
        ),
        "expectedEvidenceObjects": dict(EXPECTED_EVIDENCE_OBJECT_SOURCES),
        "namedEvidenceObjects": sorted(named_evidence),
        "taggedEvidenceObjects": sorted(tagged_evidence),
        "sceneArmatureObjects": armature_objects,
        "objects": object_reports,
        "materialComparisonIntentionallyExcluded": True,
    }


def triangle_bounds(
    vertices: list[Vector],
    triangle: tuple[int, int, int],
) -> tuple[Vector, Vector]:
    points = [vertices[index] for index in triangle]
    return (
        Vector(tuple(min(point[axis] for point in points) for axis in range(3))),
        Vector(tuple(max(point[axis] for point in points) for axis in range(3))),
    )


def overlap_envelope(
    first_vertices: list[Vector],
    first_triangle: tuple[int, int, int],
    second_vertices: list[Vector],
    second_triangle: tuple[int, int, int],
) -> tuple[Vector, Vector]:
    first_minimum, first_maximum = triangle_bounds(first_vertices, first_triangle)
    second_minimum, second_maximum = triangle_bounds(second_vertices, second_triangle)
    return (
        Vector(tuple(max(first_minimum[axis], second_minimum[axis]) for axis in range(3))),
        Vector(tuple(min(first_maximum[axis], second_maximum[axis]) for axis in range(3))),
    )


def aabb_contains_envelope(
    zone_name: str,
    minimum: Vector,
    maximum: Vector,
) -> bool:
    lower_values, upper_values, _reason = EXCEPTION_AABBS[zone_name]
    lower = Vector(lower_values)
    upper = Vector(upper_values)
    tolerance = 1e-6
    return all(
        minimum[axis] >= lower[axis] - tolerance
        and maximum[axis] <= upper[axis] + tolerance
        for axis in range(3)
    )


def localized_overlap_audit(
    first_vertices: list[Vector],
    first_triangles: list[tuple[int, int, int]],
    second_vertices: list[Vector],
    second_triangles: list[tuple[int, int, int]],
    overlap_pairs: list[tuple[int, int]],
    zone_names: tuple[str, ...],
) -> dict[str, object]:
    zones = [
        {
            "name": zone_name,
            "minimumMeters": list(EXCEPTION_AABBS[zone_name][0]),
            "maximumMeters": list(EXCEPTION_AABBS[zone_name][1]),
            "reason": EXCEPTION_AABBS[zone_name][2],
        }
        for zone_name in zone_names
    ]
    counts_by_zone = {zone_name: 0 for zone_name in zone_names}
    outside_count = 0
    outside_examples: list[object] = []
    for first_index, second_index in overlap_pairs:
        minimum, maximum = overlap_envelope(
            first_vertices,
            first_triangles[first_index],
            second_vertices,
            second_triangles[second_index],
        )
        matching = [
            zone_name
            for zone_name in zone_names
            if aabb_contains_envelope(zone_name, minimum, maximum)
        ]
        if matching:
            # Assign once in declared order so totals remain deterministic even
            # where two intentionally padded attachment boxes overlap.
            counts_by_zone[matching[0]] += 1
            continue
        outside_count += 1
        if len(outside_examples) < 8:
            outside_examples.append({
                "triangleIndices": [int(first_index), int(second_index)],
                "overlapAabbEnvelopeMeters": {
                    "minimum": [float(component) for component in minimum],
                    "maximum": [float(component) for component in maximum],
                },
            })
    return {
        "method": (
            "the axis-aligned intersection of the two participating triangle AABBs "
            "must be wholly contained in at least one declared tuck/anchor AABB"
        ),
        "allowedZones": zones,
        "localizedOverlapPairs": len(overlap_pairs) - outside_count,
        "localizedCountsByZone": counts_by_zone,
        "outsideAllowedZoneOverlapPairs": outside_count,
        "outsideAllowedZoneExamples": outside_examples,
        "status": "PASS" if outside_count == 0 else "FAIL_OUTSIDE_DESIGNED_ZONE",
    }


def percentile(sorted_values: list[float], fraction: float) -> float:
    require(bool(sorted_values), "Cannot calculate a percentile of an empty sample")
    require(0.0 <= fraction <= 1.0, f"Invalid percentile fraction: {fraction}")
    position = (len(sorted_values) - 1) * fraction
    lower = int(position)
    upper = min(lower + 1, len(sorted_values) - 1)
    blend = position - lower
    return sorted_values[lower] * (1.0 - blend) + sorted_values[upper] * blend


def clearance_distribution(values: list[float]) -> dict[str, float]:
    ordered = sorted(values)
    return {
        "minimumSignedClearanceMeters": ordered[0],
        "p01SignedClearanceMeters": percentile(ordered, 0.01),
        "medianSignedClearanceMeters": percentile(ordered, 0.50),
        "p95SignedClearanceMeters": percentile(ordered, 0.95),
        "maximumSignedClearanceMeters": ordered[-1],
    }


def anchor_contains(
    anchor_name: str,
    sample: Vector,
    landmarks: dict[str, tuple[float, float, float]],
) -> bool:
    neck = Vector(landmarks["neck"])
    shoulder = Vector(landmarks["leftShoulder"])
    ankle = Vector(landmarks["leftAnkle"])

    if anchor_name == "hood_lower_neck":
        return (
            abs(sample.x - neck.x) <= 0.170
            and neck.y - 0.035 <= sample.y <= neck.y + 0.190
            and neck.z + 0.015 <= sample.z <= neck.z + 0.115
        )
    if anchor_name == "cowl_neckline":
        return (
            abs(sample.x - neck.x) <= 0.150
            and abs(sample.y - neck.y) <= 0.145
            and neck.z - 0.010 <= sample.z <= neck.z + 0.045
        )
    if anchor_name == "mantle_left_shoulder":
        return (
            shoulder.x - 0.150 <= sample.x <= shoulder.x - 0.060
            and abs(sample.y - shoulder.y) <= 0.135
            and shoulder.z + 0.020 <= sample.z <= shoulder.z + 0.140
        )
    if anchor_name == "pauldron_shoulders":
        return (
            abs(shoulder.x) - 0.046 <= abs(sample.x) <= abs(shoulder.x) + 0.025
            and abs(sample.y - shoulder.y) <= 0.140
            and shoulder.z - 0.015 <= sample.z <= shoulder.z + 0.130
        )
    if anchor_name == "cape_upper_back_yoke":
        return (
            abs(sample.x - neck.x) <= 0.310
            and neck.y + 0.080 <= sample.y <= neck.y + 0.280
            and neck.z - 0.128 <= sample.z <= neck.z
        )
    if anchor_name == "boot_ankles":
        return (
            abs(abs(sample.x) - abs(ankle.x)) <= 0.130
            and abs(sample.y - ankle.y) <= 0.105
            and ankle.z - 0.060 <= sample.z <= ankle.z + 0.120
        )
    raise RuntimeError(f"Unknown upper-clearance anchor: {anchor_name}")


def anchor_selection_rule(anchor_name: str) -> str:
    rules = {
        "hood_lower_neck": "within 170 mm X, -35/+190 mm Y, and +15/+115 mm Z of the accepted Neck bone head",
        "cowl_neckline": "within 150 mm X, 145 mm Y, and -10/+45 mm Z of the accepted Neck bone head",
        "mantle_left_shoulder": "X from -150 to -60 mm relative to the accepted LeftUpperArm bone head, within 135 mm Y, and +20/+140 mm Z",
        "pauldron_shoulders": "mirrored absolute X from -46 to +25 mm around the accepted LeftUpperArm bone-head radius, within 140 mm Y, and -15/+130 mm Z",
        "cape_upper_back_yoke": "within 310 mm X, +80/+280 mm rear Y, and -128/0 mm Z of the accepted Neck bone head",
        "boot_ankles": "within 130 mm mirrored-X, 105 mm Y, and -60/+120 mm Z of the accepted LeftFoot bone head; forward toe and sole regions are excluded from this upper gate",
    }
    require(anchor_name in rules, f"Unknown upper-clearance anchor rule: {anchor_name}")
    return rules[anchor_name]


def sample_fit(
    body_tree: BVHTree,
    snapshot: MeshSnapshot,
    landmarks: dict[str, tuple[float, float, float]],
) -> dict[str, object]:
    samples: list[tuple[str, Vector]] = [
        ("evaluated world-space vertices", point.copy()) for point in snapshot.vertices
    ]
    samples.extend(
        (
            "evaluated world-space edge midpoints",
            (snapshot.vertices[first] + snapshot.vertices[second]) * 0.5,
        )
        for first, second in snapshot.edges
    )
    for polygon in snapshot.polygons:
        if polygon:
            samples.append(
                (
                    "evaluated world-space face centers",
                    sum((snapshot.vertices[index] for index in polygon), Vector()) / len(polygon),
                )
            )

    records: list[tuple[str, Vector, float]] = []
    sample_type_counts: dict[str, int] = {}
    for sample_type, sample in samples:
        nearest, normal, _index, distance = body_tree.find_nearest(sample)
        require(
            nearest is not None and normal is not None and distance is not None,
            f"Body nearest query failed: {snapshot.name}",
        )
        signed_clearance = float((sample - nearest).dot(normal.normalized()))
        records.append((sample_type, sample, signed_clearance))
        sample_type_counts[sample_type] = sample_type_counts.get(sample_type, 0) + 1

    full_distribution = clearance_distribution([record[2] for record in records])
    result: dict[str, object] = {
        "sampleCount": len(records),
        "sampleCountsByType": sample_type_counts,
        "signedClearanceDistributionMeters": full_distribution,
        **full_distribution,
        "bodySurfaceTriangleOverlapPairs": len(body_tree.overlap(snapshot.tree)),
        "minimumAllowedSignedClearanceMeters": MINIMUM_SIGNED_CLEARANCE_METERS,
        "requiredBodySurfaceTriangleOverlapPairs": 0,
    }
    full_maximum_threshold = FULL_CLEARANCE_ABSOLUTE_MAXIMUM_GATES[snapshot.name]
    full_maximum_actual = full_distribution["maximumSignedClearanceMeters"]
    result["fullObjectAbsoluteMaximumGate"] = {
        "kind": "full_sample_absolute_maximum",
        "reason": (
            "the absolute cap matches the frozen R2 builder and prevents rare "
            "floating or spiked samples from hiding above a percentile gate"
        ),
        "sampleCount": len(records),
        "maximumAllowedSignedClearanceMeters": full_maximum_threshold,
        "actualMaximumSignedClearanceMeters": full_maximum_actual,
        "status": (
            "PASS"
            if full_maximum_actual <= full_maximum_threshold
            else "FAIL_EXCESSIVE_ABSOLUTE_CLEARANCE"
        ),
    }

    if snapshot.name in FULL_CLEARANCE_P95_GATES:
        threshold, reason = FULL_CLEARANCE_P95_GATES[snapshot.name]
        actual = full_distribution["p95SignedClearanceMeters"]
        p95_passes = actual <= threshold
        maximum_passes = full_maximum_actual <= full_maximum_threshold
        result["upperClearanceGate"] = {
            "kind": "full_sample_p95_plus_absolute_maximum",
            "reason": reason,
            "sampleCount": len(records),
            "maximumAllowedP95SignedClearanceMeters": threshold,
            "actualP95SignedClearanceMeters": actual,
            "maximumAllowedAbsoluteSignedClearanceMeters": full_maximum_threshold,
            "actualMaximumSignedClearanceMeters": full_maximum_actual,
            "p95Status": "PASS" if p95_passes else "FAIL_EXCESSIVE_FLOAT_OR_BULK",
            "absoluteMaximumStatus": (
                "PASS" if maximum_passes else "FAIL_EXCESSIVE_ABSOLUTE_CLEARANCE"
            ),
            "status": (
                "PASS"
                if p95_passes and maximum_passes
                else "FAIL_UPPER_CLEARANCE"
            ),
        }
        return result

    threshold, anchor_name, reason = ANCHOR_CLEARANCE_P95_GATES[snapshot.name]
    anchor_records = [
        record for record in records if anchor_contains(anchor_name, record[1], landmarks)
    ]
    minimum_anchor_count = max(
        MINIMUM_ANCHOR_SAMPLE_COUNT,
        math.ceil(len(records) * MINIMUM_ANCHOR_SAMPLE_FRACTION),
    )
    anchor_distribution = (
        clearance_distribution([record[2] for record in anchor_records])
        if anchor_records
        else None
    )
    anchor_p95 = (
        anchor_distribution["p95SignedClearanceMeters"]
        if anchor_distribution is not None
        else None
    )
    if len(anchor_records) < minimum_anchor_count:
        gate_status = "FAIL_INSUFFICIENT_ANCHOR_SAMPLES"
    elif anchor_p95 is None or anchor_distribution is None:
        gate_status = "FAIL_INSUFFICIENT_ANCHOR_SAMPLES"
    elif anchor_p95 > threshold:
        gate_status = "FAIL_EXCESSIVE_FLOAT_OR_BULK"
    elif (
        anchor_distribution["maximumSignedClearanceMeters"]
        > ANCHOR_CLEARANCE_ABSOLUTE_MAXIMUM_METERS
    ):
        gate_status = "FAIL_EXCESSIVE_ANCHOR_ABSOLUTE_CLEARANCE"
    else:
        gate_status = "PASS"
    result["upperClearanceGate"] = {
        "kind": "rig_landmark_anchor_p95_plus_absolute_maximum",
        "reason": reason,
        "anchor": anchor_name,
        "selectionRule": anchor_selection_rule(anchor_name),
        "sampleCount": len(anchor_records),
        "minimumRequiredSampleCount": minimum_anchor_count,
        "minimumRequiredSampleFraction": MINIMUM_ANCHOR_SAMPLE_FRACTION,
        "actualSampleFraction": len(anchor_records) / len(records),
        "signedClearanceDistributionMeters": anchor_distribution,
        "maximumAllowedP95SignedClearanceMeters": threshold,
        "actualP95SignedClearanceMeters": anchor_p95,
        "maximumAllowedAbsoluteSignedClearanceMeters": (
            ANCHOR_CLEARANCE_ABSOLUTE_MAXIMUM_METERS
        ),
        "actualMaximumSignedClearanceMeters": (
            anchor_distribution["maximumSignedClearanceMeters"]
            if anchor_distribution is not None
            else None
        ),
        "status": gate_status,
        "fullObjectDistributionStillReported": True,
        "upperGateExemptRegions": (
            ["forward toe box", "sole and sole sidewall"]
            if snapshot.name == "HunterBodyV3_Boots"
            else []
        ),
    }
    return result


def audit_r2_files(
    evidence_hashes: dict[str, str],
    failures: list[str],
) -> dict[str, object]:
    expected_files = {
        R2_BLEND.resolve(),
        R2_REPORT.resolve(),
        R2_REVIEW_REQUEST.resolve(),
        *(project_path(rel) for rel in evidence_hashes),
    }
    actual_files = {
        path.resolve() for path in R2_ROOT.rglob("*") if path.is_file()
    }
    expected_directories = {
        R2_ROOT.resolve(),
        (R2_ROOT / "Evidence").resolve(),
    }
    actual_directories = {
        R2_ROOT.resolve(),
        *(path.resolve() for path in R2_ROOT.rglob("*") if path.is_dir()),
    }
    missing_files = expected_files - actual_files
    extra_files = actual_files - expected_files
    missing_directories = expected_directories - actual_directories
    extra_directories = actual_directories - expected_directories
    if missing_files or extra_files:
        failures.append(
            "exact pre-verification R2 file-set failure: "
            f"missing={sorted(relative(path) for path in missing_files)}, "
            f"extra={sorted(relative(path) for path in extra_files)}"
        )
    if missing_directories or extra_directories:
        failures.append(
            "exact pre-verification R2 directory-set failure: "
            f"missing={sorted(relative(path) for path in missing_directories)}, "
            f"extra={sorted(relative(path) for path in extra_directories)}"
        )
    return {
        "status": (
            "PASS_EXACT_PREVERIFICATION_DISK_SET"
            if not (
                missing_files
                or extra_files
                or missing_directories
                or extra_directories
            )
            else "FAIL_EXACT_PREVERIFICATION_DISK_SET"
        ),
        "expectedFiles": sorted(relative(path) for path in expected_files),
        "actualFiles": sorted(relative(path) for path in actual_files),
        "missingFiles": sorted(relative(path) for path in missing_files),
        "extraFiles": sorted(relative(path) for path in extra_files),
        "expectedDirectories": sorted(
            relative(path) for path in expected_directories
        ),
        "actualDirectories": sorted(relative(path) for path in actual_directories),
        "missingDirectories": sorted(
            relative(path) for path in missing_directories
        ),
        "extraDirectories": sorted(relative(path) for path in extra_directories),
        "independentVerificationDirectoryAbsentAtAudit": (
            not INDEPENDENT_ROOT.exists()
        ),
        "allowedPreverificationContract": (
            "one pinned blend, one pinned authoring report, one pinned manual-review "
            "request, and exactly seven pinned Evidence PNGs; no other files or directories"
        ),
    }


def verify_report_and_review_contracts(
    report: dict[str, object],
    review: dict[str, object],
    failures: list[str],
) -> dict[str, object]:
    if report.get("status") != "READY_FOR_MANUAL_MACRO_REVIEW":
        failures.append(f"R2 report is not ready for manual review: {report.get('status')}")
    if report.get("failures") not in ([], None):
        failures.append("R2 authoring report still contains structural failures")
    if review.get("status") != "PENDING_MANUAL_MACRO_REVIEW":
        failures.append(f"R2 manual review is not pending: {review.get('status')}")
    if review.get("automaticAcceptanceForbidden") is not True:
        failures.append("R2 manual review does not forbid automatic acceptance")
    if review.get("promotionAllowed") is not False:
        failures.append("R2 manual review unexpectedly allows promotion")

    boundaries = report.get("boundaries")
    if not isinstance(boundaries, dict):
        failures.append("R2 report boundaries are missing")
        boundaries = {}
    for key in sorted(FORBIDDEN_BOUNDARY_TRUE_KEYS):
        if boundaries.get(key) is not False:
            failures.append(f"R2 boundary must remain false: {key}={boundaries.get(key)!r}")
    if boundaries.get("manualReviewRequired") is not True:
        failures.append("R2 report does not require manual art review")
    return dict(boundaries)


def main() -> None:
    require((5, 2, 0) <= bpy.app.version < (5, 3, 0), f"Blender 5.2 is required, found {bpy.app.version_string}")
    require(
        set(FULL_CLEARANCE_P95_GATES) | set(ANCHOR_CLEARANCE_P95_GATES)
        == BODY_FIT_OBJECTS,
        "Every body-fit object must have exactly one upper-clearance gate",
    )
    require(
        not (set(FULL_CLEARANCE_P95_GATES) & set(ANCHOR_CLEARANCE_P95_GATES)),
        "A body-fit object cannot have both full and anchor upper-clearance gates",
    )
    require(
        set(FULL_CLEARANCE_ABSOLUTE_MAXIMUM_GATES) == BODY_FIT_OBJECTS,
        "Every body-fit object must have one full-object absolute maximum gate",
    )
    require(
        VISIBLE_DEFAULT_REVIEW_OBJECTS | HIDDEN_DEFAULT_REVIEW_OBJECTS
        == set(EXPECTED_OBJECT_FAMILIES)
        and not (VISIBLE_DEFAULT_REVIEW_OBJECTS & HIDDEN_DEFAULT_REVIEW_OBJECTS),
        "Default-review visibility partitions must cover the exact kit object set",
    )
    require(
        set(DESIGNED_LAYER_EXCEPTION_ZONES) == set(DESIGNED_LAYER_EXCEPTIONS),
        "Every designed-layer count exception must have deterministic spatial zones",
    )
    require(
        all(
            zone_name in EXCEPTION_AABBS
            for zone_names in DESIGNED_LAYER_EXCEPTION_ZONES.values()
            for zone_name in zone_names
        ),
        "A designed-layer exception references an unknown AABB",
    )
    for object_name, labels in EXPECTED_ISLAND_LABELS.items():
        minimum_islands, maximum_islands = EXPECTED_ISLAND_RANGES[object_name]
        require(
            minimum_islands == maximum_islands == len(labels),
            f"Island label contract differs from island count: {object_name}",
        )
    for (object_name, first_label, second_label), (_maximum, _reason, zone_names) in (
        INTRA_OBJECT_ISLAND_EXCEPTIONS.items()
    ):
        require(object_name in EXPECTED_ISLAND_LABELS, f"Unknown intra-object exception: {object_name}")
        labels = EXPECTED_ISLAND_LABELS[object_name]
        require(
            first_label in labels and second_label in labels and first_label != second_label,
            f"Unknown intra-object island labels: {object_name}: {first_label}/{second_label}",
        )
        require(
            all(zone_name in EXCEPTION_AABBS for zone_name in zone_names),
            f"Unknown intra-object exception AABB: {object_name}: {first_label}/{second_label}",
        )
    args = parse_args()

    # Preflight is deliberately fail-fast and creates no output.  An unpinned or
    # incomplete R2 blend must never be opened and described as independently
    # verified.
    r1_verified = verify_r1_frozen()
    report, review, evidence_hashes, input_hashes = verify_r2_preflight(
        args.expected_report_sha256,
        args.expected_blend_sha256,
    )
    body_geometry = extract_accepted_body_geometry()

    failures: list[str] = []
    warnings: list[str] = []
    boundaries = verify_report_and_review_contracts(report, review, failures)
    file_audit = audit_r2_files(evidence_hashes, failures)

    image_reports: dict[str, dict[str, object]] = {}
    for rel, digest in sorted(evidence_hashes.items()):
        path = project_path(rel)
        width, height = png_dimensions(path)
        image_reports[rel] = {"sha256": digest, "width": width, "height": height}
        if (width, height) != (1600, 1600):
            failures.append(f"review image is not 1600x1600: {rel}: {width}x{height}")

    bpy.ops.wm.open_mainfile(filepath=str(R2_BLEND), load_ui=False)
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    saved_evidence_geometry_audit = audit_saved_evidence_geometry(
        body_geometry,
        depsgraph,
        failures,
    )

    tagged = {
        obj.name: obj
        for obj in bpy.data.objects
        if obj.get("assetId") == ASSET_ID
    }
    expected_names = set(EXPECTED_OBJECT_FAMILIES)
    if set(tagged) != expected_names:
        missing = sorted(expected_names - set(tagged))
        extra = sorted(set(tagged) - expected_names)
        failures.append(f"R2 source-only object set changed; missing={missing}, extra={extra}")

    fit_proxies = {
        obj.name: obj
        for obj in bpy.data.objects
        if bool(obj.get("hiddenAcceptedFitProxy", False))
    }
    if set(fit_proxies) != EXPECTED_FIT_PROXIES:
        failures.append(
            "hidden fit proxy set changed; "
            f"expected={sorted(EXPECTED_FIT_PROXIES)}, actual={sorted(fit_proxies)}"
        )
    for obj in fit_proxies.values():
        if not obj.hide_render or not obj.hide_get():
            failures.append(f"fit proxy is visible: {obj.name}")
        if obj.get("visibleFacesReused") is not False:
            failures.append(f"fit proxy does not forbid visible-face reuse: {obj.name}")
        if obj.get("promotionAllowed") is not False:
            failures.append(f"fit proxy unexpectedly allows promotion: {obj.name}")

    body_tree = BVHTree.FromPolygons(
        [Vector(value) for value in body_geometry.vertices],
        body_geometry.triangles,
        all_triangles=True,
    )

    object_reports: dict[str, dict[str, object]] = {}
    snapshots: dict[str, MeshSnapshot] = {}
    family_triangles = {family: 0 for family in FAMILY_TRIANGLE_CEILINGS}
    for name in sorted(expected_names):
        obj = tagged.get(name)
        if obj is None:
            continue
        if obj.type != "MESH":
            failures.append(f"source-only object is not a mesh: {name}: {obj.type}")
            continue
        family = obj.get("macroFamily")
        expected_family = EXPECTED_OBJECT_FAMILIES[name]
        if family != expected_family:
            failures.append(f"macro family changed: {name}: {family!r} != {expected_family!r}")
            family = expected_family
        if obj.get("sourceOnlyMacro") is not True:
            failures.append(f"sourceOnlyMacro is not true: {name}")
        if obj.get("promotionAllowed") is not False:
            failures.append(f"promotionAllowed is not false: {name}")
        expected_fit = name in BODY_FIT_OBJECTS
        if bool(obj.get("bodyFitAudit", False)) != expected_fit:
            failures.append(f"body-fit contract changed: {name}")
        armature_modifiers = [modifier.name for modifier in obj.modifiers if modifier.type == "ARMATURE"]
        if armature_modifiers:
            failures.append(f"armature modifiers found on source-only object: {name}: {armature_modifiers}")
        if obj.parent is not None and obj.parent.type == "ARMATURE":
            failures.append(f"source-only object is parented to an armature: {name}")
        if len(obj.vertex_groups) != 0:
            failures.append(f"source-only object has vertex groups: {name}: {len(obj.vertex_groups)}")
        if obj.data.shape_keys is not None:
            failures.append(f"source-only object has shape keys: {name}")
        if obj.animation_data is not None:
            failures.append(f"source-only object has animation data: {name}")

        expected_default_visible = name in VISIBLE_DEFAULT_REVIEW_OBJECTS
        render_visible = not obj.hide_render
        viewport_visible = not obj.hide_get()
        if render_visible != expected_default_visible:
            failures.append(
                f"default-review render visibility changed: {name}: "
                f"visible={render_visible}, expected={expected_default_visible}"
            )
        if viewport_visible != expected_default_visible:
            failures.append(
                f"default-review viewport visibility changed: {name}: "
                f"visible={viewport_visible}, expected={expected_default_visible}"
            )

        texture_nodes = material_image_nodes(obj)
        if texture_nodes:
            failures.append(f"image texture nodes found on source-only object: {name}: {texture_nodes}")

        snapshot = mesh_snapshot(obj, depsgraph)
        snapshots[name] = snapshot
        integrity = snapshot.integrity
        if snapshot.uv_layer_count != 0 or len(obj.data.uv_layers) != 0:
            failures.append(f"UV layers found on source-only object: {name}")
        for key in (
            "nonManifoldInteriorEdges",
            "wireEdges",
            "isolatedVertices",
            "degenerateFaces",
        ):
            if integrity[key] != 0:
                failures.append(f"mesh integrity failure: {name}: {key}={integrity[key]}")
        if name in CLOSED_VOLUME_OBJECTS:
            if integrity["boundaryEdges"] != 0:
                failures.append(
                    f"closed source volume has boundary edges: {name}: {integrity['boundaryEdges']}"
                )
            boundary_contract: dict[str, object] = {
                "kind": "closed volume",
                "requiredBoundaryEdges": 0,
            }
        else:
            reason = INTENTIONAL_OPEN_CAGE_BOUNDARIES[name]
            if not 0 < integrity["boundaryEdges"] <= 1_024:
                failures.append(
                    f"intentional source-cage boundary count is implausible: "
                    f"{name}: {integrity['boundaryEdges']}"
                )
            boundary_contract = {
                "kind": "intentional open source-only macro cage",
                "reason": reason,
                "maximumBoundaryEdges": 1_024,
                "surfacedProductionTopologyAccepted": False,
            }
        minimum_islands, maximum_islands = EXPECTED_ISLAND_RANGES[name]
        if not minimum_islands <= integrity["connectedIslands"] <= maximum_islands:
            failures.append(
                f"connected-island count changed: {name}: {integrity['connectedIslands']} "
                f"outside [{minimum_islands},{maximum_islands}]"
            )
        if len(snapshot.islands) != integrity["connectedIslands"]:
            failures.append(
                f"independent polygon-island decomposition differs: {name}: "
                f"{len(snapshot.islands)} != {integrity['connectedIslands']}"
            )
        expected_labels = EXPECTED_ISLAND_LABELS.get(name)
        if expected_labels is not None:
            actual_labels = tuple(island.label for island in snapshot.islands)
            if actual_labels != expected_labels:
                failures.append(
                    f"connected-island label contract changed: {name}: "
                    f"{actual_labels!r} != {expected_labels!r}"
                )

        island_reports: list[dict[str, object]] = []
        for island in snapshot.islands:
            island_reports.append({
                "index": island.index,
                "label": island.label,
                "boundsMeters": island.bounds,
                "integrity": island.integrity,
                "integrityExamples": island.integrity_examples,
            })
            for key in (
                "coincidentDuplicateVertexPairs",
                "coincidentTrianglePairs",
                "selfIntersectionPairsExcludingTrueNeighbors",
            ):
                if island.integrity[key] != 0:
                    failures.append(
                        f"connected-island geometry failure: {name}/{island.label}: "
                        f"{key}={island.integrity[key]}"
                    )

        family_triangles[expected_family] += len(snapshot.triangles)
        item: dict[str, object] = {
            "family": expected_family,
            "triangles": len(snapshot.triangles),
            "integrity": integrity,
            "boundaryContract": boundary_contract,
            "uvLayerCount": snapshot.uv_layer_count,
            "armatureModifierCount": len(armature_modifiers),
            "vertexGroupCount": len(obj.vertex_groups),
            "shapeKeysPresent": obj.data.shape_keys is not None,
            "animationDataPresent": obj.animation_data is not None,
            "imageTextureNodes": texture_nodes,
            "sourceOnlyMacro": obj.get("sourceOnlyMacro") is True,
            "promotionAllowed": obj.get("promotionAllowed"),
            "connectedIslandSnapshots": island_reports,
            "defaultReviewVisibility": {
                "expectedVisible": expected_default_visible,
                "renderVisible": render_visible,
                "viewportVisible": viewport_visible,
                "status": (
                    "PASS"
                    if render_visible == viewport_visible == expected_default_visible
                    else "FAIL"
                ),
            },
        }
        if expected_fit:
            fit = sample_fit(body_tree, snapshot, body_geometry.landmarks)
            item["bodyFit"] = fit
            if fit["minimumSignedClearanceMeters"] < MINIMUM_SIGNED_CLEARANCE_METERS:
                failures.append(
                    f"body penetration exceeds 1 mm: {name}: "
                    f"{fit['minimumSignedClearanceMeters']:.9f} m"
                )
            if fit["bodySurfaceTriangleOverlapPairs"] != 0:
                failures.append(
                    f"body triangle overlaps found: {name}: "
                    f"{fit['bodySurfaceTriangleOverlapPairs']} pairs"
                )
            upper_gate = fit["upperClearanceGate"]
            absolute_gate = fit["fullObjectAbsoluteMaximumGate"]
            if absolute_gate["status"] != "PASS":
                failures.append(
                    f"full-object absolute clearance gate failed: {name}: "
                    f"actual={absolute_gate['actualMaximumSignedClearanceMeters']:.9f} m, "
                    f"maximum={absolute_gate['maximumAllowedSignedClearanceMeters']:.6f} m"
                )
            if upper_gate["status"] != "PASS":
                failures.append(
                    f"upper-clearance gate failed: {name}: {upper_gate['status']}: "
                    f"p95={upper_gate['actualP95SignedClearanceMeters']!r} m, "
                    f"maximum={upper_gate['maximumAllowedP95SignedClearanceMeters']:.6f} m, "
                    f"absolute={upper_gate.get('actualMaximumSignedClearanceMeters')!r} m, "
                    f"absoluteMaximum={upper_gate.get('maximumAllowedAbsoluteSignedClearanceMeters')!r} m, "
                    f"samples={upper_gate['sampleCount']}/"
                    f"{upper_gate.get('minimumRequiredSampleCount', upper_gate['sampleCount'])}"
                )
        object_reports[name] = item

    total_triangles = sum(family_triangles.values())
    for family, maximum in FAMILY_TRIANGLE_CEILINGS.items():
        actual = family_triangles[family]
        if actual > maximum:
            failures.append(f"family triangle ceiling exceeded: {family}: {actual} > {maximum}")
    if total_triangles > KIT_TRIANGLE_CEILING:
        failures.append(f"kit triangle ceiling exceeded: {total_triangles} > {KIT_TRIANGLE_CEILING}")

    reported_objects = report.get("objects")
    if not isinstance(reported_objects, dict):
        failures.append("R2 authoring report has no per-object records")
        reported_objects = {}
    for name, item in object_reports.items():
        reported = reported_objects.get(name)
        if not isinstance(reported, dict):
            failures.append(f"R2 authoring report omitted object: {name}")
            continue
        if reported.get("triangles") != item["triangles"]:
            failures.append(
                f"R2 reported triangle count differs: {name}: "
                f"{reported.get('triangles')} != {item['triangles']}"
            )
        expected_default_visible = name in VISIBLE_DEFAULT_REVIEW_OBJECTS
        if reported.get("visibleInDefaultReview") is not expected_default_visible:
            failures.append(
                f"R2 reported default-review visibility differs: {name}: "
                f"{reported.get('visibleInDefaultReview')!r} != {expected_default_visible!r}"
            )
        reported_integrity = reported.get("integrity")
        if not isinstance(reported_integrity, dict):
            failures.append(f"R2 authoring report omitted mesh integrity: {name}")
            continue
        for key in (
            "vertices",
            "faces",
            "boundaryEdges",
            "nonManifoldInteriorEdges",
            "degenerateFaces",
            "connectedIslands",
        ):
            if reported_integrity.get(key) != item["integrity"][key]:
                failures.append(
                    f"R2 reported mesh integrity differs: {name}: {key}: "
                    f"{reported_integrity.get(key)} != {item['integrity'][key]}"
                )
    if report.get("kitTriangles") != total_triangles:
        failures.append(f"R2 reported kit total differs: {report.get('kitTriangles')} != {total_triangles}")

    intra_object_reports: dict[str, list[dict[str, object]]] = {}
    for object_name, snapshot in sorted(snapshots.items()):
        records: list[dict[str, object]] = []
        for first_index, first_island in enumerate(snapshot.islands):
            for second_island in snapshot.islands[first_index + 1 :]:
                overlap_pairs = [
                    (int(first), int(second))
                    for first, second in first_island.tree.overlap(second_island.tree)
                ]
                exception_key = island_pair_key(
                    object_name,
                    first_island.label,
                    second_island.label,
                )
                exception = INTRA_OBJECT_ISLAND_EXCEPTIONS.get(exception_key)
                record: dict[str, object] = {
                    "islands": [first_island.label, second_island.label],
                    "triangleOverlapPairs": len(overlap_pairs),
                    "explicitSubcomponentException": exception is not None,
                }
                if exception is None:
                    record["requiredMaximumTriangleOverlapPairs"] = 0
                    if overlap_pairs:
                        failures.append(
                            f"unexpected intra-object island crossing: {object_name}: "
                            f"{first_island.label} / {second_island.label}: "
                            f"{len(overlap_pairs)} pairs"
                        )
                else:
                    maximum, reason, zone_names = exception
                    localization = localized_overlap_audit(
                        snapshot.vertices,
                        first_island.triangles,
                        snapshot.vertices,
                        second_island.triangles,
                        overlap_pairs,
                        zone_names,
                    )
                    record.update({
                        "exceptionReason": reason,
                        "exceptionMaximumTriangleOverlapPairs": maximum,
                        "spatialLocalization": localization,
                        "manualVisualReviewStillRequired": bool(overlap_pairs),
                    })
                    if len(overlap_pairs) > maximum:
                        failures.append(
                            f"intra-object subcomponent overlap exceeds bounded exception: "
                            f"{object_name}: {first_island.label} / {second_island.label}: "
                            f"{len(overlap_pairs)} > {maximum} pairs"
                        )
                    if localization["status"] != "PASS":
                        failures.append(
                            f"intra-object subcomponent overlap escaped designed zones: "
                            f"{object_name}: {first_island.label} / {second_island.label}: "
                            f"{localization['outsideAllowedZoneOverlapPairs']} pairs"
                        )
                    if overlap_pairs:
                        warnings.append(
                            f"designed intra-object attachment requires manual review: "
                            f"{object_name}: {first_island.label} / {second_island.label}: "
                            f"{len(overlap_pairs)} pairs"
                        )
                records.append(record)
        intra_object_reports[object_name] = records

    pairwise_reports: list[dict[str, object]] = []
    snapshot_names = sorted(snapshots)
    for first_index, first in enumerate(snapshot_names):
        for second in snapshot_names[first_index + 1 :]:
            key = pair_key(first, second)
            overlap_pairs = [
                (int(first_triangle), int(second_triangle))
                for first_triangle, second_triangle
                in snapshots[first].tree.overlap(snapshots[second].tree)
            ]
            overlaps = len(overlap_pairs)
            exception = DESIGNED_LAYER_EXCEPTIONS.get(key)
            record: dict[str, object] = {
                "objects": [first, second],
                "triangleOverlapPairs": overlaps,
                "designedLayerException": exception is not None,
            }
            if exception is None:
                record["requiredMaximumTriangleOverlapPairs"] = 0
                if overlaps:
                    failures.append(f"unexpected inter-object overlap: {first} / {second}: {overlaps} pairs")
            else:
                maximum, reason = exception
                mutually_exclusive_states = (
                    key in MUTUALLY_EXCLUSIVE_WEAPON_STATE_EXCEPTION_PAIRS
                )
                localization = localized_overlap_audit(
                    snapshots[first].vertices,
                    snapshots[first].triangles,
                    snapshots[second].vertices,
                    snapshots[second].triangles,
                    overlap_pairs,
                    DESIGNED_LAYER_EXCEPTION_ZONES[key],
                )
                record["exceptionReason"] = reason
                record["exceptionMaximumTriangleOverlapPairs"] = maximum
                record["spatialLocalization"] = localization
                record["mutuallyExclusiveWeaponStates"] = mutually_exclusive_states
                record["visibleTogetherInDefaultReview"] = (
                    first in VISIBLE_DEFAULT_REVIEW_OBJECTS
                    and second in VISIBLE_DEFAULT_REVIEW_OBJECTS
                )
                record["manualVisualReviewStillRequired"] = (
                    overlaps > 0 and not mutually_exclusive_states
                )
                if overlaps > maximum:
                    failures.append(
                        f"designed-layer overlap exceeds bounded exception: "
                        f"{first} / {second}: {overlaps} > {maximum} pairs"
                    )
                if localization["status"] != "PASS":
                    failures.append(
                        f"designed-layer overlap escaped deterministic tuck/anchor zones: "
                        f"{first} / {second}: "
                        f"{localization['outsideAllowedZoneOverlapPairs']} pairs"
                    )
                if overlaps > 0 and not mutually_exclusive_states:
                    warnings.append(
                        f"designed-layer overlap requires manual review: {first} / {second}: {overlaps} pairs"
                    )
            pairwise_reports.append(record)

    independent_status = (
        "PASS_INDEPENDENT_STRUCTURAL_VERIFICATION_PENDING_MANUAL_ART_REVIEW"
        if not failures
        else "FAIL_INDEPENDENT_STRUCTURAL_VERIFICATION"
    )
    output = {
        "schemaVersion": 1,
        "assetId": ASSET_ID,
        "status": independent_status,
        "verifier": relative(SCRIPT_PATH),
        "verifierSha256": sha256(SCRIPT_PATH),
        "r2Pins": {
            "report": relative(R2_REPORT),
            "reportSha256": args.expected_report_sha256,
            "blend": relative(R2_BLEND),
            "blendSha256": args.expected_blend_sha256,
            "evidence": evidence_hashes,
            "verifiedInputs": input_hashes,
        },
        "r1FrozenHashesVerified": r1_verified,
        "reportBoundaries": boundaries,
        "fileAudit": file_audit,
        "reviewImages": image_reports,
        "acceptedBodyEvidenceIdentityAudit": saved_evidence_geometry_audit,
        "expectedSourceOnlyObjects": sorted(expected_names),
        "defaultReviewVisibilityPolicy": {
            "requiredVisible": sorted(VISIBLE_DEFAULT_REVIEW_OBJECTS),
            "requiredHiddenButPresent": sorted(HIDDEN_DEFAULT_REVIEW_OBJECTS),
            "rule": (
                "the twelve non-state kit objects plus StaticString must be visible; "
                "DrawString and NockedArrow must remain present but hidden in render and viewport"
            ),
        },
        "objects": object_reports,
        "clearanceUpperGatePolicy": {
            "fullSampleP95": {
                name: {
                    "maximumAllowedMeters": values[0],
                    "reason": values[1],
                }
                for name, values in FULL_CLEARANCE_P95_GATES.items()
            },
            "rigLandmarkAnchorP95": {
                name: {
                    "maximumAllowedMeters": values[0],
                    "absoluteMaximumAllowedMeters": (
                        ANCHOR_CLEARANCE_ABSOLUTE_MAXIMUM_METERS
                    ),
                    "anchor": values[1],
                    "reason": values[2],
                    "selectionRule": anchor_selection_rule(values[1]),
                }
                for name, values in ANCHOR_CLEARANCE_P95_GATES.items()
            },
            "fullObjectAbsoluteMaximum": {
                name: {
                    "maximumAllowedMeters": maximum,
                    "matchesFrozenR2BuilderMaximum": True,
                }
                for name, maximum in FULL_CLEARANCE_ABSOLUTE_MAXIMUM_GATES.items()
            },
            "minimumAnchorSampleCount": MINIMUM_ANCHOR_SAMPLE_COUNT,
            "minimumAnchorSampleFraction": MINIMUM_ANCHOR_SAMPLE_FRACTION,
            "acceptedBodyRigLandmarksWorldMeters": body_geometry.landmarks,
        },
        "triangleCeilings": {
            "kit": {"actual": total_triangles, "maximum": KIT_TRIANGLE_CEILING},
            "families": {
                family: {"actual": family_triangles[family], "maximum": maximum}
                for family, maximum in FAMILY_TRIANGLE_CEILINGS.items()
            },
        },
        "intraObjectConnectedIslandAudit": intra_object_reports,
        "pairwiseInterObjectAudit": pairwise_reports,
        "failures": failures,
        "warnings": warnings,
        "methodLimits": [
            "Preflight and a final pre-write guard fail closed unless R2 contains exactly its pinned blend, authoring report, manual-review request, Evidence directory, and seven pinned PNGs. The verifier creates its own previously absent IndependentVerification directory only after all read-only checks finish.",
            "BVH overlap counts detect intersecting triangle pairs, not nearby nonintersecting surfaces or semantic cloth order.",
            "The saved evidence body and two eyes are compared with freshly evaluated neutral accepted BodyV3 source geometry using a canonical 1 micrometer world-space quantization hash. Materials and mesh datablock identity are intentionally excluded, while counts, object names, identity transforms, flags, armature/action state, shape-key absence, and visible geometry must match.",
            "Bounded designed-layer exceptions are attachment allowances, not proof that the overlap is visually acceptable; every nonzero exception must be spatially confined to a declared tuck/anchor AABB and remains a manual-review item unless the two weapon states are explicitly mutually exclusive.",
            "Static and draw-only weapon states are structurally audited even when mutually exclusive. A bounded shared endpoint between mutually exclusive states is not a default-visible art warning because those states cannot render together.",
            "The spatial exception test confines the complete AABB-intersection envelope of each overlapping triangle pair. This is conservative and deterministic, but it does not calculate the exact triangle-intersection polyline.",
            "Passing a seam/lap AABB only localizes a bounded crossing; it is not proof against a fused shell or visually poor attachment inside that small zone, so the pinned close views still govern art acceptance.",
            "Every pair of disconnected islands inside a multi-island object is audited independently. Coincident vertices, coincident triangles, and same-island BVH overlaps are checked; only true two-vertex edge neighbors and triangles from one source polygon are skipped. One-shared-vertex pairs receive a geometric edge/interior test and fail if they cross away from the common coordinate.",
            "Signed nearest-normal clearance can be ambiguous near sharp concavities; zero body BVH overlap is therefore also mandatory for every body-fit object.",
            "Clearance samples are evaluated world-space vertices, edge midpoints, and face centers; they are topology-weighted rather than uniform-area surface samples. Triangle ceilings reduce, but cannot eliminate, percentile gaming through uneven tessellation.",
            "Upper-clearance p95 gates are paired with absolute full-object maxima, and anchor gates additionally impose a 45 mm maximum. Pinned renders still require manual inspection for silhouette defects that remain inside those numeric caps.",
            "Rig-landmark anchor bands are deterministic geometric selections, not semantically painted attachment zones. Insufficient anchor coverage fails closed, but passing coverage does not prove tailoring or layer order outside the band.",
            "Hood, cowl, mantle, pauldrons, and cape may legitimately flare outside their attachment bands; those regions remain subject to their full-object absolute maxima even though anchor p95 is the tighter attachment test.",
            "Only the boot toe box and sole are exempt from the anchor p95/45 mm maximum gate. The ankle anchor is capped, and every boot sample remains subject to the -1 mm minimum, 140 mm full-object maximum, and whole-boot zero body-overlap requirement.",
            "All upper distances are measured against the naked accepted BodyV3 surface, not against intervening garments; the harness allowance therefore includes intended brigandine layer depth and is not a garment-order proof.",
            "Absence of UV layers and image texture nodes proves no texture-map workflow in the kit, but does not infer the artistic intent of numeric clay material values.",
            "This verifier performs no rig/deformation, animation, UV/PBR, LOD, FBX round-trip, Unity import, performance, device, or visual-quality acceptance.",
        ],
        "decisionBoundary": {
            "manualArtAccepted": False,
            "automaticVisualAcceptanceForbidden": True,
            "promotionAllowed": False,
            "surfaceAuthoringAllowed": False,
            "uvPbrAllowed": False,
            "lodAllowed": False,
            "fbxAllowed": False,
            "unityImportAllowed": False,
            "nextGate": "independent manual inspection of every pinned 1600x1600 R2 review image, but only after this structural verification passes",
        },
    }

    final_disk_guard_failures: list[str] = []
    final_disk_guard = audit_r2_files(
        evidence_hashes,
        final_disk_guard_failures,
    )
    require(
        not final_disk_guard_failures
        and final_disk_guard["status"] == "PASS_EXACT_PREVERIFICATION_DISK_SET",
        "R2 disk set changed during verification; refusing any write: "
        + "; ".join(final_disk_guard_failures),
    )
    require(
        not INDEPENDENT_ROOT.exists(),
        "IndependentVerification appeared during verification; refusing any write",
    )
    INDEPENDENT_ROOT.mkdir(parents=False, exist_ok=False)
    with INDEPENDENT_REPORT.open("x", encoding="utf-8") as stream:
        stream.write(json.dumps(output, indent=2, sort_keys=True) + "\n")
    print(independent_status)
    print("KIT_TRIANGLES", total_triangles, "FAILURES", len(failures), "WARNINGS", len(warnings))
    if failures:
        raise RuntimeError("; ".join(failures))


if __name__ == "__main__":
    main()
