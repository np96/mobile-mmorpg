#!/usr/bin/env python3
"""Build the isolated R2 BodyV3 Hunter macro review.

R2 imports the frozen R1 authoring utilities, never the rejected R1 mesh.  All
visible clothing cages below are newly constructed for the accepted masculine
BodyV3 and the user-confirmed hidden fit proxies.  The checkpoint ends at
family-colour clay renders plus a manual-review request; it cannot emit UVs,
PBR maps, LODs, FBX, Unity assets, production binding, or an art PASS.
"""

from __future__ import annotations

import argparse
from dataclasses import dataclass
import hashlib
import importlib.util
import json
import math
from pathlib import Path
import sys
import tempfile
from typing import Sequence

import bmesh
import bpy
from mathutils import Vector
from mathutils.bvhtree import BVHTree


SCRIPT_PATH = Path(__file__).resolve()
SCRIPT_DIR = SCRIPT_PATH.parent
PROJECT_ROOT = SCRIPT_DIR.parents[2]
R1_BUILDER = SCRIPT_DIR / "build_hunter_production_body_v3_v1_r1.py"
INDEPENDENT_VERIFIER = SCRIPT_DIR / "verify_hunter_production_body_v3_v1_r2.py"
R1_REJECT = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R1"
    / "HunterProductionBodyV3V1_R1.internal-review.json"
)

_spec = importlib.util.spec_from_file_location("hunter_body_v3_r1_frozen", R1_BUILDER)
if _spec is None or _spec.loader is None:
    raise RuntimeError(f"Cannot load frozen R1 utilities: {R1_BUILDER}")
base = importlib.util.module_from_spec(_spec)
sys.modules[_spec.name] = base
_spec.loader.exec_module(base)


ASSET_ID = "hunter-production-body-v3-v1-r2"
MODEL_NAME = "HunterProductionBodyV3V1_R2"
OUTPUT_ROOT = (
    PROJECT_ROOT
    / "SourceArt/Blender/Characters/HunterProductionBodyV3V1/Iterations/R2"
)
EXPECTED_KIT_OBJECTS = set(base.EXPECTED_KIT_OBJECTS)
FAMILY_TARGETS = {
    "base_garments": (600, 4_200),
    "hood_hat": (160, 1_200),
    "cowl": (140, 1_000),
    "brigandine": (300, 1_800),
    "mantle_pauldrons": (500, 2_600),
    "split_cape": (250, 1_400),
    "limb_gear": (600, 3_000),
    "harness_belt": (50, 1_200),
    "weapon_quiver": (500, 2_400),
}

BODY_BULK_LIMITS = {
    "HunterBodyV3_BaseGarments": (0.025, 0.065),
    "HunterBodyV3_HoodHat": (0.080, 0.140),
    "HunterBodyV3_Cowl": (0.090, 0.160),
    "HunterBodyV3_TailoredBrigandine": (0.045, 0.080),
    "HunterBodyV3_AsymmetricMantle": (0.100, 0.160),
    "HunterBodyV3_LayeredPauldrons": (0.080, 0.130),
    "HunterBodyV3_SplitCape": (0.300, 0.340),
    "HunterBodyV3_LimbGear": (0.045, 0.090),
    "HunterBodyV3_Boots": (0.080, 0.140),
    "HunterBodyV3_HarnessBelt": (0.065, 0.100),
}

# The following structural contract is copied verbatim from the frozen
# independent verifier.  The builder mirrors it locally so R2 remains absent
# unless the exact verifier-facing island and bounded-overlap gates pass.
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
VISIBLE_DEFAULT_REVIEW_OBJECTS = set(EXPECTED_OBJECT_FAMILIES) - HIDDEN_DEFAULT_REVIEW_OBJECTS

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

AUTHORED_ISLAND_LABELS = EXPECTED_ISLAND_LABELS
COINCIDENT_VERTEX_TOLERANCE_METERS = 1e-7

FIT_TOP_INNER_CLEARANCE = 0.009
FIT_TROUSERS_INNER_CLEARANCE = 0.010

base.ASSET_ID = ASSET_ID
base.MODEL_NAME = MODEL_NAME
base.OUTPUT_ROOT = OUTPUT_ROOT
base.SCRIPT_PATH = SCRIPT_PATH
base.EXPECTED_INPUTS = dict(base.EXPECTED_INPUTS)
base.EXPECTED_INPUTS[R1_BUILDER] = "b97d3a44d644ab462e8501b99ea55808f782f4a4fca7b6e5cf646a2da6314644"
base.EXPECTED_INPUTS[R1_REJECT] = "79a9aedcd140690aee0093022c555e2f5f7c4ff0563fbe9fb7234ff99b40ac70"
base.EXPECTED_INPUTS[INDEPENDENT_VERIFIER] = "5cf3c54b931054358dc958cacf7a74670a29d63fdbae1ed8af71608a443a241b"


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def parse_args() -> argparse.Namespace:
    raw = sys.argv[sys.argv.index("--") + 1 :] if "--" in sys.argv else []
    parser = argparse.ArgumentParser()
    parser.add_argument("--output-root", default=str(OUTPUT_ROOT))
    parser.add_argument(
        "--preflight-only",
        action="store_true",
        help="Run every structural gate without rendering or creating R2.",
    )
    args = parser.parse_args(raw)
    output = Path(args.output_root).expanduser().resolve()
    base.require(output == OUTPUT_ROOT.resolve(), "R2 output path is fixed; fork R3 instead of redirecting or overwriting R2")
    base.require(not output.exists(), "R2 is frozen once its output directory exists; fork R3 instead of overwriting")
    args.output_root = output
    return args


def section_from_surface(
    surface: base.Surface,
    center: Vector,
    directions: Sequence[Vector],
    clearance: float,
    maximum: float,
) -> list[Vector]:
    return [surface.ray(center, direction, maximum) + direction.normalized() * clearance for direction in directions]


def section_from_body(
    surface: base.Surface,
    center: Vector,
    directions: Sequence[Vector],
    clearance: float,
    maximum: float,
) -> list[Vector]:
    points = []
    for direction in directions:
        normalized = direction.normalized()
        location, normal, _index, _distance = surface.tree.ray_cast(center, normalized, maximum)
        if location is None or normal is None:
            probe = center + normalized * min(0.12, maximum * 0.5)
            location, normal, _nearest_distance = surface.nearest(probe)
        points.append(location + normal.normalized() * clearance)
    return points


def section_from_same_side_body(
    surface: base.Surface,
    center: Vector,
    directions: Sequence[Vector],
    side: int,
    clearance: float,
    maximum: float,
) -> list[Vector]:
    """Cast an ordered limb section and reject any opposing-side branch."""
    points = []
    for direction in directions:
        normalized = direction.normalized()
        location, normal, _index, distance = surface.tree.ray_cast(center, normalized, maximum)
        base.require(
            location is not None
            and normal is not None
            and distance is not None
            and 0.002 <= float(distance) <= maximum
            and side * location.x >= 0.008,
            f"Same-side BodyV3 limb ray missed at {tuple(center)} / {tuple(normalized)}",
        )
        points.append(location + normal.normalized() * clearance)
    return points


def torso_proxy_point(
    body_surface: base.Surface,
    fit_surface: base.Surface,
    z: float,
    direction: Vector,
    clearance: float,
) -> Vector:
    """Select the torso branch of a proxy that also contains long sleeves."""
    normalized = direction.normalized()
    if z >= 1.340:
        probe_radius = 0.115
    elif z >= 1.245:
        probe_radius = 0.165
    else:
        probe_radius = 0.145
    probe = Vector((normalized.x * probe_radius, normalized.y * probe_radius, z))
    location, body_normal, _nearest_distance = body_surface.nearest(probe)
    base.require(
        Vector((location.x, location.y, 0.0)).dot(normalized) > 0.025,
        "BodyV3 torso-branch probe crossed the center line",
    )
    proxy_point, _proxy_normal, _proxy_distance = fit_surface.nearest(
        location + body_normal.normalized() * FIT_TOP_INNER_CLEARANCE
    )
    # The accepted envelope selects only the correct torso branch.  Its dense
    # faces/normals are never copied; the production cage remains a sparse
    # independently lofted ring at a declared total body-layer clearance.
    _ = proxy_point
    return location + body_normal.normalized() * clearance


def radial_directions(count: int) -> list[Vector]:
    return [
        Vector((math.cos(2.0 * math.pi * index / count), math.sin(2.0 * math.pi * index / count), 0.0))
        for index in range(count)
    ]


def arm_directions(count: int, *, open_underside: bool = False) -> list[Vector]:
    if not open_underside:
        angles = [2.0 * math.pi * index / count for index in range(count)]
    else:
        gap = math.radians(54.0)
        start = -math.pi / 2.0 + gap / 2.0
        sweep = 2.0 * math.pi - gap
        angles = [start + sweep * index / (count - 1) for index in range(count)]
    return [Vector((0.0, math.cos(angle), math.sin(angle))) for angle in angles]


def leg_directions(count: int, *, open_back: bool = False) -> list[Vector]:
    if not open_back:
        angles = [2.0 * math.pi * index / count for index in range(count)]
    else:
        gap = math.radians(64.0)
        start = math.pi / 2.0 + gap / 2.0
        sweep = 2.0 * math.pi - gap
        angles = [start + sweep * index / (count - 1) for index in range(count)]
    return [Vector((math.cos(angle), math.sin(angle), 0.0)) for angle in angles]


def inner_seam_leg_directions(side: int, count: int) -> list[Vector]:
    """Sample one thigh/calf without casting through the opposite leg."""
    gap = math.radians(72.0)
    inward_angle = math.pi if side > 0 else 0.0
    start = inward_angle + gap / 2.0
    sweep = 2.0 * math.pi - gap
    return [
        Vector((math.cos(start + sweep * index / (count - 1)), math.sin(start + sweep * index / (count - 1)), 0.0))
        for index in range(count)
    ]


def greave_directions(side: int, count: int) -> list[Vector]:
    """Leave a broad inner-leg opening so armor never spans the center gap."""
    gap = math.radians(128.0)
    inward_angle = math.pi if side > 0 else 0.0
    start = inward_angle + gap / 2.0
    sweep = 2.0 * math.pi - gap
    return [
        Vector((math.cos(start + sweep * index / (count - 1)), math.sin(start + sweep * index / (count - 1)), 0.0))
        for index in range(count)
    ]


def closed_grid_shell(
    rows: Sequence[Sequence[Vector]],
    depth: float,
    offset_direction: Vector = Vector((0.0, 0.0, -1.0)),
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    """Give an authored patch real volume without a modifier or flat card edge."""
    direction = offset_direction.normalized()
    base.require(direction.length > 0.0, "Closed grid shell needs a nonzero offset direction")
    top_vertices, top_faces = base.grid(rows)
    bottom_rows = [[point + direction * depth for point in row] for row in rows]
    bottom_vertices, bottom_faces = base.grid(bottom_rows)
    row_count = len(rows)
    width = len(rows[0])
    vertices = top_vertices + bottom_vertices
    bottom_offset = len(top_vertices)
    faces = list(top_faces)
    faces.extend(tuple(bottom_offset + index for index in reversed(face)) for face in bottom_faces)

    perimeter = []
    perimeter.extend((0, column) for column in range(width))
    perimeter.extend((row, width - 1) for row in range(1, row_count))
    perimeter.extend((row_count - 1, column) for column in range(width - 2, -1, -1))
    perimeter.extend((row, 0) for row in range(row_count - 2, 0, -1))
    for index, (row, column) in enumerate(perimeter):
        next_row, next_column = perimeter[(index + 1) % len(perimeter)]
        a = row * width + column
        b = next_row * width + next_column
        faces.append((a, b, bottom_offset + b, bottom_offset + a))
    return vertices, faces


def normal_offset_closed_grid_shell(
    rows: Sequence[Sequence[Vector]],
    depth: float,
    preferred_direction: Vector,
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    """Close a curved patch with a locally parallel second skin."""
    preferred = preferred_direction.normalized()
    base.require(preferred.length > 0.0, "Normal-offset shell needs a preferred direction")
    row_count = len(rows)
    width = len(rows[0])
    offset_rows: list[list[Vector]] = []
    for row_index, row in enumerate(rows):
        offset_row = []
        prior_row = rows[max(0, row_index - 1)]
        next_row = rows[min(row_count - 1, row_index + 1)]
        for column, point in enumerate(row):
            prior_column = row[max(0, column - 1)]
            next_column = row[min(width - 1, column + 1)]
            row_tangent = next_row[column] - prior_row[column]
            column_tangent = next_column - prior_column
            normal = column_tangent.cross(row_tangent)
            if normal.length <= 1e-9:
                normal = preferred.copy()
            else:
                normal.normalize()
                if normal.dot(preferred) < 0.0:
                    normal.negate()
            offset_row.append(point + normal * depth)
        offset_rows.append(offset_row)

    top_vertices, top_faces = base.grid(rows)
    bottom_vertices, bottom_faces = base.grid(offset_rows)
    vertices = top_vertices + bottom_vertices
    bottom_offset = len(top_vertices)
    faces = list(top_faces)
    faces.extend(tuple(bottom_offset + index for index in reversed(face)) for face in bottom_faces)
    perimeter = []
    perimeter.extend((0, column) for column in range(width))
    perimeter.extend((row, width - 1) for row in range(1, row_count))
    perimeter.extend((row_count - 1, column) for column in range(width - 2, -1, -1))
    perimeter.extend((row, 0) for row in range(row_count - 2, 0, -1))
    for index, (row, column) in enumerate(perimeter):
        next_row_index, next_column = perimeter[(index + 1) % len(perimeter)]
        first = row * width + column
        second = next_row_index * width + next_column
        faces.append((first, second, bottom_offset + second, bottom_offset + first))
    return vertices, faces


def closed_between_grids(
    outer_rows: Sequence[Sequence[Vector]],
    inner_rows: Sequence[Sequence[Vector]],
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    """Close two independently authored, identically indexed surface grids."""
    base.require(
        len(outer_rows) >= 2
        and len(outer_rows) == len(inner_rows)
        and len(outer_rows[0]) >= 2
        and all(len(row) == len(outer_rows[0]) for row in outer_rows)
        and all(len(row) == len(outer_rows[0]) for row in inner_rows),
        "Paired closed-grid shell topology mismatch",
    )
    outer_vertices, outer_faces = base.grid(outer_rows)
    inner_vertices, inner_faces = base.grid(inner_rows)
    vertices = outer_vertices + inner_vertices
    inner_offset = len(outer_vertices)
    faces = list(outer_faces)
    faces.extend(tuple(inner_offset + index for index in reversed(face)) for face in inner_faces)

    row_count = len(outer_rows)
    width = len(outer_rows[0])
    perimeter: list[tuple[int, int]] = []
    perimeter.extend((0, column) for column in range(width))
    perimeter.extend((row, width - 1) for row in range(1, row_count))
    perimeter.extend((row_count - 1, column) for column in range(width - 2, -1, -1))
    perimeter.extend((row, 0) for row in range(row_count - 2, 0, -1))
    for index, (row, column) in enumerate(perimeter):
        next_row, next_column = perimeter[(index + 1) % len(perimeter)]
        first = row * width + column
        second = next_row * width + next_column
        faces.append((first, second, inner_offset + second, inner_offset + first))
    return vertices, faces


def shoulder_arc_surface_point(
    body_surface: base.Surface,
    anatomy: base.Anatomy,
    side: int,
    absolute_x: float,
    angle: float,
    clearance: float,
) -> Vector:
    """Sample and offset one ordered upper-shoulder sectional station."""
    origin = Vector((side * absolute_x, anatomy.shoulder.y, anatomy.shoulder.z))
    direction = Vector((0.0, math.cos(angle), math.sin(angle))).normalized()
    location, normal, _index, distance = body_surface.tree.ray_cast(origin, direction, 0.240)
    base.require(
        location is not None
        and normal is not None
        and distance is not None
        and 0.002 <= float(distance) <= 0.180
        and side * location.x >= 0.050,
        f"Accepted shoulder ray missed at side={side}, x={absolute_x:.4f}, angle={angle:.4f}",
    )
    base.require(
        normal.normalized().dot(direction) >= 0.12,
        f"Accepted shoulder exit normal reversed at side={side}, x={absolute_x:.4f}, angle={angle:.4f}",
    )
    # Both skins use the same accepted center-surface ray and a preserved
    # sectional offset.  This keeps their X stations and separation exact and
    # avoids faceted source-triangle normals folding adjacent rows together.
    return location + direction * clearance


def loft_with_end_caps(
    rings: Sequence[Sequence[Vector]],
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    vertices, faces = base.loft(rings, close_ring=True)
    width = len(rings[0])
    end_offset = (len(rings) - 1) * width
    faces.append(tuple(reversed(range(width))))
    faces.append(tuple(end_offset + index for index in range(width)))
    return vertices, faces


def require(condition: bool, message: str) -> None:
    base.require(condition, message)


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


def self_crossing_location_debug(snapshot: MeshSnapshot) -> dict[str, object]:
    records: dict[str, object] = {}
    for island in snapshot.islands:
        count = island.integrity["selfIntersectionPairsExcludingTrueNeighbors"]
        duplicate_vertices = island.integrity["coincidentDuplicateVertexPairs"]
        if count == 0 and duplicate_vertices == 0:
            continue
        examples = []
        for example in island.integrity_examples["selfIntersectionsExcludingTrueNeighbors"]:
            first_index, second_index = example["triangles"]
            minimum, maximum = overlap_envelope(
                snapshot.vertices,
                island.triangles[first_index],
                snapshot.vertices,
                island.triangles[second_index],
            )
            examples.append({
                **example,
                "overlapAabbEnvelopeMeters": {
                    "minimum": [round(float(component), 6) for component in minimum],
                    "maximum": [round(float(component), 6) for component in maximum],
                },
            })
        records[island.label] = {
            "boundsMeters": island.bounds,
            "selfIntersectionPairs": count,
            "coincidentDuplicateVertexPairs": duplicate_vertices,
            "coincidentDuplicateVertices": island.integrity_examples["coincidentDuplicateVertices"],
            "examples": examples,
        }
    return records


def object_overlap_count(first: bpy.types.Object, second: bpy.types.Object) -> int:
    first.data.calc_loop_triangles()
    second.data.calc_loop_triangles()
    first_tree = BVHTree.FromPolygons(
        [vertex.co.copy() for vertex in first.data.vertices],
        [tuple(triangle.vertices) for triangle in first.data.loop_triangles],
        all_triangles=True,
    )
    second_tree = BVHTree.FromPolygons(
        [vertex.co.copy() for vertex in second.data.vertices],
        [tuple(triangle.vertices) for triangle in second.data.loop_triangles],
        all_triangles=True,
    )
    return len(first_tree.overlap(second_tree))


def object_overlap_location_summary(
    first: bpy.types.Object,
    second: bpy.types.Object,
) -> dict[str, object]:
    """Localize an inter-object contact without treating it as an allowance."""
    first.data.calc_loop_triangles()
    second.data.calc_loop_triangles()
    first_vertices = [vertex.co.copy() for vertex in first.data.vertices]
    second_vertices = [vertex.co.copy() for vertex in second.data.vertices]
    first_triangles = [tuple(triangle.vertices) for triangle in first.data.loop_triangles]
    second_triangles = [tuple(triangle.vertices) for triangle in second.data.loop_triangles]
    first_tree = BVHTree.FromPolygons(first_vertices, first_triangles, all_triangles=True)
    second_tree = BVHTree.FromPolygons(second_vertices, second_triangles, all_triangles=True)
    pairs = [(int(a), int(b)) for a, b in first_tree.overlap(second_tree)]
    if not pairs:
        return {"pairCount": 0}

    first_centers = [
        sum((first_vertices[index] for index in first_triangles[a]), Vector()) / 3.0
        for a, _b in pairs
    ]
    second_centers = [
        sum((second_vertices[index] for index in second_triangles[b]), Vector()) / 3.0
        for _a, b in pairs
    ]

    def bounds(points: Sequence[Vector]) -> dict[str, list[float]]:
        return {
            axis: [
                round(min(point[axis_index] for point in points), 6),
                round(max(point[axis_index] for point in points), 6),
            ]
            for axis, axis_index in (("x", 0), ("y", 1), ("z", 2))
        }

    return {
        "pairCount": len(pairs),
        "firstTriangleCenterBoundsMeters": bounds(first_centers),
        "secondTriangleCenterBoundsMeters": bounds(second_centers),
        "firstTriangleIndices": sorted({a for a, _b in pairs})[:24],
        "secondTriangleIndices": sorted({b for _a, b in pairs})[:24],
    }


def body_overlap_location_summary(
    body_surface: base.Surface,
    obj: bpy.types.Object,
) -> dict[str, object]:
    obj.data.calc_loop_triangles()
    other = BVHTree.FromPolygons(
        [vertex.co.copy() for vertex in obj.data.vertices],
        [tuple(triangle.vertices) for triangle in obj.data.loop_triangles],
        all_triangles=True,
    )
    pairs = body_surface.tree.overlap(other)
    if not pairs:
        return {"pairCount": 0}
    triangle_indices = sorted({second for _first, second in pairs})
    centers = []
    for triangle_index in triangle_indices:
        triangle = obj.data.loop_triangles[triangle_index]
        centers.append(
            sum((obj.data.vertices[index].co for index in triangle.vertices), Vector((0.0, 0.0, 0.0))) / 3.0
        )
    return {
        "pairCount": len(pairs),
        "garmentTriangleCount": len(triangle_indices),
        "centerBounds": {
            axis: [round(min(point[axis_index] for point in centers), 5), round(max(point[axis_index] for point in centers), 5)]
            for axis, axis_index in (("x", 0), ("y", 1), ("z", 2))
        },
        "firstCenters": [[round(float(value), 5) for value in point] for point in centers[:8]],
    }


def body_overlap_by_authored_island(
    body_surface: base.Surface,
    obj: bpy.types.Object,
    labels: Sequence[str],
) -> list[dict[str, object]]:
    mesh = obj.data
    mesh.calc_loop_triangles()
    vertex_faces: dict[int, set[int]] = {vertex.index: set() for vertex in mesh.vertices}
    for polygon in mesh.polygons:
        for vertex_index in polygon.vertices:
            vertex_faces[vertex_index].add(polygon.index)
    remaining = set(range(len(mesh.polygons)))
    islands: list[list[int]] = []
    while remaining:
        seed = min(remaining)
        remaining.remove(seed)
        stack = [seed]
        island = []
        while stack:
            polygon_index = stack.pop()
            island.append(polygon_index)
            for vertex_index in mesh.polygons[polygon_index].vertices:
                neighbors = vertex_faces[vertex_index] & remaining
                remaining.difference_update(neighbors)
                stack.extend(neighbors)
        islands.append(sorted(island))
    islands.sort(key=lambda indices: indices[0])
    base.require(len(islands) == len(labels), f"Authored island labels changed for {obj.name}")

    vertices = [vertex.co.copy() for vertex in mesh.vertices]
    results = []
    for label, polygon_indices in zip(labels, islands):
        polygon_set = set(polygon_indices)
        triangles = [
            tuple(triangle.vertices)
            for triangle in mesh.loop_triangles
            if triangle.polygon_index in polygon_set
        ]
        tree = BVHTree.FromPolygons(vertices, triangles, all_triangles=True)
        overlap_pairs = body_surface.tree.overlap(tree)
        overlap_indices = sorted({second for _first, second in overlap_pairs})
        overlap_centers = [
            sum((vertices[index] for index in triangles[triangle_index]), Vector((0.0, 0.0, 0.0))) / 3.0
            for triangle_index in overlap_indices
        ]
        z_histogram: dict[str, int] = {}
        y_histogram: dict[str, int] = {}
        for point in overlap_centers:
            lower = math.floor(point.z * 20.0) / 20.0
            key = f"{lower:.2f}-{lower + 0.05:.2f}"
            z_histogram[key] = z_histogram.get(key, 0) + 1
            lower_y = math.floor(point.y * 20.0) / 20.0
            y_key = f"{lower_y:.2f}-{lower_y + 0.05:.2f}"
            y_histogram[y_key] = y_histogram.get(y_key, 0) + 1
        used = {index for triangle in triangles for index in triangle}
        points = [vertices[index] for index in used]
        results.append({
            "label": label,
            "surfaceTriangleOverlapPairs": len(overlap_pairs),
            "overlappingGarmentTriangles": len(overlap_indices),
            "overlapCenterZHistogram": z_histogram,
            "overlapCenterYHistogram": y_histogram,
            "triangles": len(triangles),
            "boundsMeters": {
                axis: [
                    round(min(point[axis_index] for point in points), 5),
                    round(max(point[axis_index] for point in points), 5),
                ]
                for axis, axis_index in (("x", 0), ("y", 1), ("z", 2))
            },
        })
    return results


def clearance_report(
    body_surface: base.Surface,
    obj: bpy.types.Object,
) -> dict[str, object]:
    samples: list[tuple[float, Vector, str]] = []
    counts: dict[str, int] = {}

    def add_sample(point: Vector, sample_type: str) -> None:
        nearest, normal, _distance = body_surface.nearest(point)
        samples.append((float((point - nearest).dot(normal)), point.copy(), sample_type))
        counts[sample_type] = counts.get(sample_type, 0) + 1

    for vertex in obj.data.vertices:
        add_sample(vertex.co, "evaluated world-space vertices")
    for edge in obj.data.edges:
        first, second = edge.vertices
        add_sample(
            (obj.data.vertices[first].co + obj.data.vertices[second].co) * 0.5,
            "evaluated world-space edge midpoints",
        )
    for polygon in obj.data.polygons:
        if polygon.vertices:
            add_sample(
                sum(
                    (obj.data.vertices[index].co for index in polygon.vertices),
                    Vector((0.0, 0.0, 0.0)),
                )
                / len(polygon.vertices),
                "evaluated world-space face centers",
            )
    samples.sort(key=lambda sample: sample[0])
    values = [sample[0] for sample in samples]

    def percentile(fraction: float) -> float:
        position = (len(values) - 1) * fraction
        lower = int(position)
        upper = min(lower + 1, len(values) - 1)
        blend = position - lower
        return values[lower] * (1.0 - blend) + values[upper] * blend

    return {
        "sampleCount": len(values),
        "sampleCountsByType": counts,
        "minimumSignedClearanceMeters": values[0],
        "p01SignedClearanceMeters": percentile(0.01),
        "medianSignedClearanceMeters": percentile(0.50),
        "p95SignedClearanceMeters": percentile(0.95),
        "maximumSignedClearanceMeters": values[-1],
        "lowestSignedClearanceSamples": [
            {
                "signedClearanceMeters": signed,
                "positionMeters": [round(float(value), 6) for value in point],
            }
            for signed, point, _sample_type in samples[:8]
        ],
        "bodySurfaceTriangleOverlapPairs": body_surface.overlap_count(obj),
        "methodBoundary": "all final macro mesh vertices, edge midpoints, and face centers plus exact BVH surface-triangle overlap; verifier-matched layer-specific upper-bulk and anchor-zone gates apply",
    }


def anchor_zone_selector(zone_name: str):
    selectors = {
        "hood_neck": (
            lambda point: 1.405 <= point.z <= 1.535,
            "1.405m <= z <= 1.535m (open-front neck edge and first hood rise)",
        ),
        "cowl_neck": (
            lambda point: point.z >= 1.395,
            "z >= 1.395m (upper two neck-drape rings)",
        ),
        "mantle_shoulder": (
            lambda point: abs(point.x) <= 0.155 and point.z >= 1.405,
            "abs(x) <= 0.155m and z >= 1.405m (inner shoulder root)",
        ),
        "pauldron_roots": (
            lambda point: abs(point.x) <= 0.245 and point.z >= 1.350,
            "abs(x) <= 0.245m and z >= 1.350m (body-facing shell roots)",
        ),
        "cape_yoke": (
            lambda point: point.z >= 1.300,
            "z >= 1.300m (top two cape-yoke rows)",
        ),
        "boot_ankles": (
            lambda point: point.y >= 0.020 and point.z >= 0.025,
            "y >= 0.020m and z >= 0.025m (heel and ankle attachment stations)",
        ),
    }
    base.require(zone_name in selectors, f"Unknown anchor-zone audit: {zone_name}")
    return selectors[zone_name]


def verifier_anchor_selector(zone_name: str, anatomy: base.Anatomy):
    descriptions = {
        "hood_neck": "within 170 mm X, -35/+190 mm Y, and +15/+115 mm Z of the accepted Neck bone head; front aperture chord excluded",
        "cowl_neck": "within 150 mm X, 145 mm Y, and -10/+45 mm Z of the accepted Neck bone head",
        "mantle_shoulder": "positive-X inner root from -150/-60 mm X, 135 mm Y, and +20/+140 mm Z relative to the accepted LeftUpperArm bone head",
        "pauldron_roots": "mirrored root rows from -46/+25 mm absolute-X, 140 mm Y, and -15/+130 mm Z relative to the accepted LeftUpperArm bone head",
        "cape_yoke": "within 310 mm X, +80/+280 mm rear Y, and -128/0 mm Z of the accepted Neck bone head",
        "boot_ankles": "within 130 mm mirrored-X, 105 mm Y, and -60/+120 mm Z of the accepted LeftFoot bone head; forward toe and sole regions are excluded",
    }
    base.require(zone_name in descriptions, f"Unknown verifier anchor-zone audit: {zone_name}")
    neck = anatomy.neck
    shoulder = anatomy.shoulder
    ankle = anatomy.ankle

    def selected(point: Vector) -> bool:
        if zone_name == "hood_neck":
            return abs(point.x - neck.x) <= 0.170 and neck.y - 0.035 <= point.y <= neck.y + 0.190 and neck.z + 0.015 <= point.z <= neck.z + 0.115
        if zone_name == "cowl_neck":
            return abs(point.x - neck.x) <= 0.150 and abs(point.y - neck.y) <= 0.145 and neck.z - 0.010 <= point.z <= neck.z + 0.045
        if zone_name == "mantle_shoulder":
            return shoulder.x - 0.150 <= point.x <= shoulder.x - 0.060 and abs(point.y - shoulder.y) <= 0.135 and shoulder.z + 0.020 <= point.z <= shoulder.z + 0.140
        if zone_name == "pauldron_roots":
            return abs(shoulder.x) - 0.046 <= abs(point.x) <= abs(shoulder.x) + 0.025 and abs(point.y - shoulder.y) <= 0.140 and shoulder.z - 0.015 <= point.z <= shoulder.z + 0.130
        if zone_name == "cape_yoke":
            return abs(point.x - neck.x) <= 0.310 and neck.y + 0.080 <= point.y <= neck.y + 0.280 and neck.z - 0.128 <= point.z <= neck.z
        return abs(abs(point.x) - abs(ankle.x)) <= 0.130 and abs(point.y - ankle.y) <= 0.105 and ankle.z - 0.060 <= point.z <= ankle.z + 0.120

    return selected, descriptions[zone_name]


def anchor_clearance_report(
    body_surface: base.Surface,
    obj: bpy.types.Object,
    zone_name: str,
    anatomy: base.Anatomy,
) -> dict[str, object]:
    """Audit only the authored attachment band, never a flared free hem.

    The coordinate predicates are intentionally conservative and are recorded
    in the report.  They select the neck/shoulder/yoke/ankle regions where the
    garment must visually attach to BodyV3; silhouette hems remain governed by
    their broader family bulk limits.
    """
    selected, description = verifier_anchor_selector(zone_name, anatomy)

    points: list[tuple[str, int, tuple[int, ...], Vector]] = [
        ("vertex", vertex.index, (vertex.index,), vertex.co.copy())
        for vertex in obj.data.vertices
    ]
    points.extend(
        (
            "edgeMidpoint",
            edge.index,
            tuple(int(index) for index in edge.vertices),
            sum(
                (obj.data.vertices[index].co for index in edge.vertices),
                Vector((0.0, 0.0, 0.0)),
            ) * 0.5,
        )
        for edge in obj.data.edges
    )
    points.extend(
        (
            "faceCenter",
            polygon.index,
            tuple(int(index) for index in polygon.vertices),
            sum(
                (obj.data.vertices[index].co for index in polygon.vertices),
                Vector((0.0, 0.0, 0.0)),
            ) / len(polygon.vertices),
        )
        for polygon in obj.data.polygons
        if polygon.vertices
    )
    records: list[tuple[float, Vector, str, int, tuple[int, ...]]] = []
    for sample_type, sample_index, supports, point in points:
        if not selected(point):
            continue
        nearest, normal, _distance = body_surface.nearest(point)
        records.append((
            float((point - nearest).dot(normal)),
            point.copy(),
            sample_type,
            sample_index,
            supports,
        ))
    records.sort(key=lambda record: record[0])
    values = [record[0] for record in records]
    base.require(values, f"Anchor-zone audit has no samples: {obj.name}")
    minimum_count = max(24, math.ceil(len(points) * 0.05))
    base.require(
        len(values) >= minimum_count,
        f"Too few verifier anchor samples for {obj.name}: {len(values)} < {minimum_count}",
    )
    position = (len(values) - 1) * 0.95
    lower = int(position)
    upper = min(lower + 1, len(values) - 1)
    blend = position - lower
    return {
        "zone": zone_name,
        "selector": description,
        "sampleCount": len(values),
        "minimumRequiredSampleCount": minimum_count,
        "sampleFraction": len(values) / len(points),
        "minimumSignedClearanceMeters": values[0],
        "p95SignedClearanceMeters": values[lower] * (1.0 - blend) + values[upper] * blend,
        "maximumSignedClearanceMeters": values[-1],
        "highestSignedClearanceSamples": [
            {
                "signedClearanceMeters": signed,
                "positionMeters": [round(float(value), 6) for value in point],
                "sampleType": sample_type,
                "sampleIndex": sample_index,
                "supportVertexIndices": list(supports),
            }
            for signed, point, sample_type, sample_index, supports
            in reversed(records[-8:])
        ],
    }


def enforce_verifier_anchor_bulk(
    obj: bpy.types.Object,
    body_surface: base.Surface,
    zone_name: str,
    anatomy: base.Anatomy,
    target_clearance: float,
) -> None:
    """Clamp the complete verifier-selected attachment band, including chords."""
    selected, _description = verifier_anchor_selector(zone_name, anatomy)
    iterations_used = 0
    moved_vertices: set[int] = set()
    contour_support_passes = 0
    audit: dict[str, object] | None = None
    for support_pass in range(5):
        for _iteration in range(16):
            support_excess: dict[int, float] = {}

            def inspect(point: Vector, supports: Sequence[int]) -> None:
                if not selected(point):
                    return
                nearest, normal, _distance = body_surface.nearest(point)
                signed = float((point - nearest).dot(normal))
                excess = signed - target_clearance
                if excess > 1e-7:
                    for index in supports:
                        vertex_index = int(index)
                        support_excess[vertex_index] = max(
                            support_excess.get(vertex_index, 0.0),
                            excess,
                        )

            for vertex in obj.data.vertices:
                inspect(vertex.co, (vertex.index,))
            for edge in obj.data.edges:
                first, second = (int(index) for index in edge.vertices)
                inspect(
                    (obj.data.vertices[first].co + obj.data.vertices[second].co) * 0.5,
                    (first, second),
                )
            for polygon in obj.data.polygons:
                indices = tuple(int(index) for index in polygon.vertices)
                inspect(
                    sum(
                        (obj.data.vertices[index].co for index in indices),
                        Vector((0.0, 0.0, 0.0)),
                    ) / len(indices),
                    indices,
                )
            if not support_excess:
                break
            iterations_used += 1
            moved_vertices.update(support_excess)
            for vertex_index in sorted(support_excess):
                vertex = obj.data.vertices[vertex_index]
                nearest, normal, _distance = body_surface.nearest(vertex.co)
                current = float((vertex.co - nearest).dot(normal))
                # A chord over a concave aperture can float farther than both
                # endpoints.  Move its supports inward while retaining a 6 mm
                # naked-body safety floor.  Any remaining floating chord gets
                # a real contour station in the bounded support pass below.
                desired = max(0.006, current - support_excess[vertex_index] * 1.50)
                vertex.co = nearest + normal * min(target_clearance, desired)
            obj.data.update(calc_edges=False)

        audit = anchor_clearance_report(body_surface, obj, zone_name, anatomy)
        if float(audit["maximumSignedClearanceMeters"]) <= 0.045 + 1e-7:
            break
        if support_pass == 4:
            break
        triangles_before = base.triangle_count(obj)
        add_verifier_anchor_contour_support(
            obj,
            body_surface,
            zone_name,
            anatomy,
            maximum_edges=8,
        )
        if base.triangle_count(obj) == triangles_before:
            break
        contour_support_passes += 1

    base.require(audit is not None, f"Verifier anchor clamp did not audit {obj.name}")
    if float(audit["maximumSignedClearanceMeters"]) > 0.045 + 1e-7:
        print("ANCHOR_CLAMP_DEBUG", obj.name, json.dumps(audit, indent=2))
    base.require(
        float(audit["maximumSignedClearanceMeters"]) <= 0.045 + 1e-7,
        f"Verifier anchor clamp failed for {obj.name}: "
        f"{audit['maximumSignedClearanceMeters']:.6f} m",
    )
    obj["verifierAnchorClamp"] = True
    obj["verifierAnchorClampZone"] = zone_name
    obj["verifierAnchorClampTargetMeters"] = target_clearance
    obj["verifierAnchorClampIterations"] = iterations_used
    obj["verifierAnchorClampMovedVertexCount"] = len(moved_vertices)
    obj["verifierAnchorClampContourSupportPasses"] = contour_support_passes


def add_verifier_anchor_contour_support(
    obj: bpy.types.Object,
    body_surface: base.Surface,
    zone_name: str,
    anatomy: base.Anatomy,
    *,
    maximum_edges: int,
) -> None:
    """Split only the worst floating root chords before clearance clamping."""
    selected, _description = verifier_anchor_selector(zone_name, anatomy)
    edge_scores: dict[int, float] = {}
    edge_lookup = {tuple(sorted(edge.vertices)): edge.index for edge in obj.data.edges}

    def clearance(point: Vector) -> float:
        nearest, normal, _distance = body_surface.nearest(point)
        return float((point - nearest).dot(normal))

    for edge in obj.data.edges:
        first, second = (int(index) for index in edge.vertices)
        midpoint = (obj.data.vertices[first].co + obj.data.vertices[second].co) * 0.5
        if selected(midpoint):
            excess = clearance(midpoint) - 0.045
            if excess > 1e-7:
                edge_scores[edge.index] = max(edge_scores.get(edge.index, 0.0), excess)
    for polygon in obj.data.polygons:
        indices = tuple(int(index) for index in polygon.vertices)
        center = sum(
            (obj.data.vertices[index].co for index in indices),
            Vector((0.0, 0.0, 0.0)),
        ) / len(indices)
        if not selected(center):
            continue
        excess = clearance(center) - 0.045
        if excess <= 1e-7:
            continue
        for index, first in enumerate(indices):
            second = indices[(index + 1) % len(indices)]
            edge_index = edge_lookup[tuple(sorted((first, second)))]
            edge_scores[edge_index] = max(edge_scores.get(edge_index, 0.0), excess)

    chosen = [
        index
        for index, _score in sorted(
            edge_scores.items(),
            key=lambda item: (-item[1], item[0]),
        )[:maximum_edges]
    ]
    if not chosen:
        obj["verifierAnchorContourSupportEdges"] = 0
        return
    triangles_before = base.triangle_count(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    try:
        bm.edges.ensure_lookup_table()
        bmesh.ops.subdivide_edges(
            bm,
            edges=[bm.edges[index] for index in chosen],
            cuts=1,
            use_grid_fill=True,
        )
        bm.to_mesh(obj.data)
    finally:
        bm.free()
    obj.data.validate(verbose=False)
    obj.data.update(calc_edges=True)
    for polygon in obj.data.polygons:
        polygon.use_smooth = True
    obj["verifierAnchorContourSupportEdges"] = len(chosen)
    obj["verifierAnchorContourSupportTrianglesAdded"] = base.triangle_count(obj) - triangles_before


def project_object_anchor_zone(
    obj: bpy.types.Object,
    body_surface: base.Surface,
    zone_name: str,
    clearance: float,
) -> None:
    selector, _description = anchor_zone_selector(zone_name)
    count = 0
    for vertex in obj.data.vertices:
        if not selector(vertex.co):
            continue
        nearest, normal, _distance = body_surface.nearest(vertex.co)
        vertex.co = nearest + normal * clearance
        count += 1
    base.require(count >= 24, f"Too few final anchor vertices for {obj.name}: {count}")
    obj.data.update(calc_edges=False)
    obj["anchorZone"] = zone_name
    obj["anchorClearanceMeters"] = clearance


def push_buffers_outside_body(
    buffers: base.Buffers,
    body_surface: base.Surface,
    clearance: float,
) -> None:
    """Project sparse cage stations to a guaranteed normal-space body offset."""
    for index, point in enumerate(buffers.vertices):
        nearest, normal, _distance = body_surface.nearest(point)
        signed = float((point - nearest).dot(normal))
        if signed < clearance:
            buffers.vertices[index] = nearest + normal * clearance


def project_anchor_points(
    points: Sequence[Vector],
    body_surface: base.Surface,
    clearance: float,
) -> None:
    for point in points:
        nearest, normal, _distance = body_surface.nearest(point)
        target = nearest + normal * clearance
        point.x, point.y, point.z = target.x, target.y, target.z


def refine_and_project_object(
    obj: bpy.types.Object,
    body_surface: base.Surface,
    clearance: float,
) -> None:
    """Add one contour-support pass, then place every new station outside BodyV3."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    try:
        bmesh.ops.subdivide_edges(
            bm,
            edges=list(bm.edges),
            cuts=1,
            use_grid_fill=True,
        )
        bm.to_mesh(obj.data)
    finally:
        bm.free()
    # Only a bounded two-pass correction is allowed after region-specific
    # construction.  Each pass moves a station by at most 6 mm; the upper-bulk
    # gates below reject any result that grows beyond its authored layer band.
    maximum_single_move = 0.006
    maximum_required_move = 0.0
    for _iteration in range(2):
        for vertex in obj.data.vertices:
            nearest, normal, _distance = body_surface.nearest(vertex.co)
            signed = float((vertex.co - nearest).dot(normal))
            required = clearance - signed
            maximum_required_move = max(maximum_required_move, required)
            if required > 0.0:
                vertex.co += normal * min(required, maximum_single_move)
        obj.data.update(calc_edges=False)
    obj.data.validate(verbose=False)
    obj.data.update(calc_edges=True)
    obj["authoredContourRefinementPasses"] = 1
    obj["maximumRequestedPostRefineCorrectionMeters"] = max(0.0, maximum_required_move)


def resolve_body_surface_overlaps(
    obj: bpy.types.Object,
    body_surface: base.Surface,
    clearance: float,
    *,
    maximum_iterations: int,
    maximum_total_move: float,
) -> None:
    """Locally lift only triangles proven to intersect the accepted body.

    This is a bounded component repair after the authored loft, not a global
    shrinkwrap.  It is used for the two known difficult regions: the terminal
    sleeve contour and the boot upper.  The exact BVH overlap gate remains the
    authority and fails closed if the bounded repair cannot reach zero.
    """
    movement = [0.0 for _vertex in obj.data.vertices]
    repaired_triangles: set[int] = set()
    iterations_used = 0
    exhausted = False
    for iteration in range(maximum_iterations):
        obj.data.calc_loop_triangles()
        tree = BVHTree.FromPolygons(
            [vertex.co.copy() for vertex in obj.data.vertices],
            [tuple(triangle.vertices) for triangle in obj.data.loop_triangles],
            all_triangles=True,
        )
        pairs = body_surface.tree.overlap(tree)
        if not pairs:
            break
        iterations_used = iteration + 1
        triangle_indices = {second for _first, second in pairs}
        repaired_triangles.update(triangle_indices)
        vertex_indices = {
            int(vertex_index)
            for triangle_index in triangle_indices
            for vertex_index in obj.data.loop_triangles[triangle_index].vertices
        }
        for vertex_index in sorted(vertex_indices):
            vertex = obj.data.vertices[vertex_index]
            nearest, normal, _distance = body_surface.nearest(vertex.co)
            signed = float((vertex.co - nearest).dot(normal))
            requested = max(clearance - signed, 0.0025)
            remaining = maximum_total_move - movement[vertex_index]
            if remaining <= 1e-8:
                exhausted = True
                continue
            actual = min(requested, 0.006, remaining)
            vertex.co += normal * actual
            movement[vertex_index] += actual
        obj.data.update(calc_edges=False)
        if exhausted:
            break
    remaining_pairs = body_surface.overlap_count(obj)
    if remaining_pairs:
        obj.data.calc_loop_triangles()
        current_tree = BVHTree.FromPolygons(
            [vertex.co.copy() for vertex in obj.data.vertices],
            [tuple(triangle.vertices) for triangle in obj.data.loop_triangles],
            all_triangles=True,
        )
        remaining_triangle_indices = sorted({
            second for _first, second in body_surface.tree.overlap(current_tree)
        })
        triangle_debug = []
        for triangle_index in remaining_triangle_indices[:24]:
            triangle = obj.data.loop_triangles[triangle_index]
            points = [obj.data.vertices[index].co.copy() for index in triangle.vertices]
            triangle_debug.append({
                "triangleIndex": triangle_index,
                "vertexIndices": list(triangle.vertices),
                "points": [[round(float(value), 5) for value in point] for point in points],
                "signedClearances": [
                    round(float((point - nearest).dot(normal)), 6)
                    for point in points
                    for nearest, normal, _distance in [body_surface.nearest(point)]
                ],
            })
        print("BODY_OVERLAP_REPAIR_DEBUG", obj.name)
        print(json.dumps({
            "remaining": body_overlap_location_summary(body_surface, obj),
            "maximumMoveMeters": max(movement, default=0.0),
            "movedVertexCount": sum(value > 0.0 for value in movement),
            "iterations": iterations_used,
            "triangles": triangle_debug,
        }, indent=2))
    base.require(
        remaining_pairs == 0,
        f"Bounded body-overlap repair failed for {obj.name}: {remaining_pairs} pairs remain",
    )
    obj["componentBodyOverlapRepair"] = True
    obj["componentBodyOverlapRepairIterations"] = iterations_used
    obj["componentBodyOverlapRepairTriangleCount"] = len(repaired_triangles)
    obj["componentBodyOverlapRepairMaximumMoveMeters"] = max(movement, default=0.0)


def coincident_geometry_report(obj: bpy.types.Object, tolerance: float = 1e-7) -> dict[str, int]:
    """Detect exact/coincident source geometry that normal integrity misses."""
    scale = 1.0 / tolerance

    def key(point: Vector) -> tuple[int, int, int]:
        return tuple(int(round(float(component) * scale)) for component in point)

    coordinate_keys = [key(vertex.co) for vertex in obj.data.vertices]
    duplicate_vertices = len(coordinate_keys) - len(set(coordinate_keys))
    face_keys = []
    for polygon in obj.data.polygons:
        face_keys.append(tuple(sorted(coordinate_keys[index] for index in polygon.vertices)))
    duplicate_faces = len(face_keys) - len(set(face_keys))
    return {
        "coincidentVertexCount": duplicate_vertices,
        "coincidentFaceCount": duplicate_faces,
    }


def clean_degenerate_geometry(obj: bpy.types.Object) -> None:
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    try:
        bmesh.ops.remove_doubles(bm, verts=list(bm.verts), dist=1e-7)
        bmesh.ops.dissolve_degenerate(bm, edges=list(bm.edges), dist=1e-7)
        bm.to_mesh(obj.data)
    finally:
        bm.free()
    obj.data.validate(verbose=False)
    obj.data.update(calc_edges=True)


def build_base_garments(
    body_surface: base.Surface,
    top_surface: base.Surface,
    trouser_surface: base.Surface,
    anatomy: base.Anatomy,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    buffers = base.Buffers()

    torso_rings = []
    torso_levels = (0.965, 1.035, 1.105, 1.175)
    for z in torso_levels:
        ring = [
            torso_proxy_point(body_surface, top_surface, z, direction, 0.018)
            for direction in radial_directions(18)
        ]
        for index, point in enumerate(ring):
            angle = 2.0 * math.pi * index / len(ring)
            point.z += 0.004 * max(0.0, -math.sin(angle)) * (1.0 - abs(math.cos(angle)))
        torso_rings.append(ring)
    buffers.add(*base.loft(torso_rings, close_ring=True))

    sleeve_stations = (
        0.205, 0.290, 0.380, 0.475, 0.565, 0.625,
        0.655, 0.675,
    )
    directions = arm_directions(10)
    for side in (-1, 1):
        rings = []
        for station_index, x in enumerate(sleeve_stations):
            ring = section_from_surface(
                top_surface,
                Vector((side * x, anatomy.shoulder.y, anatomy.shoulder.z)),
                directions,
                0.015,
                0.34,
            )
            rings.append(ring)
        buffers.add(*base.loft(rings, close_ring=True))

    pelvis_levels = (0.955, 0.915, 0.875, 0.840, 0.825)
    pelvis_rings = [
        section_from_surface(
            trouser_surface,
            Vector((0.0, 0.0, z)),
            radial_directions(18),
            0.008 + 0.001 * ((0.955 - z) / 0.130),
            0.34,
        )
        for z in pelvis_levels
    ]
    buffers.add(*base.loft(pelvis_rings, close_ring=True))

    for side in (-1, 1):
        directions = inner_seam_leg_directions(side, 10)
        rings = []
        for z in (0.825, 0.775, 0.710, 0.640, 0.565, 0.490, 0.415, 0.340, 0.265, 0.190, 0.115):
            groin_ease = 0.036 * max(0.0, min(1.0, (z - 0.710) / 0.115))
            center_x = side * (abs(anatomy.hip.x) + groin_ease + 0.004 * (0.825 - z) / 0.71)
            rings.append(
                section_from_body(
                    body_surface,
                    Vector((center_x, anatomy.hip.y, z)),
                    directions,
                    0.018,
                    0.32,
                )
            )
        buffers.add(*base.loft(rings, close_ring=False))

    obj = base.mesh_object(
        "HunterBodyV3_BaseGarments",
        "base_garments",
        buffers,
        mat,
        "independent sparse cages ray-fit against the user-confirmed hidden BodyV3 envelopes for tunic, full wrist sleeves, pelvis bridge, and fitted legs; no proxy faces, normals, or topology are reused",
        fit_audit=True,
    )
    # All component rings are ordered and already sampled outside their
    # accepted body/fit surfaces.  Finished-mesh normal projection is forbidden
    # here because it folded the wrist rings across themselves.
    resolve_body_surface_overlaps(
        obj,
        body_surface,
        0.017,
        maximum_iterations=8,
        maximum_total_move=0.020,
    )
    obj["sleeveTerminalAbsoluteXMeters"] = 0.675
    obj["wristContinuityViaBracerTerminalAbsoluteXMeters"] = 0.712
    obj["visibleBodyProxyFacesReused"] = False
    obj["acceptedFitProxyReferenceObjects"] = json.dumps([base.FIT_TOP_NAME, base.FIT_TROUSERS_NAME])
    return obj


def build_hood_hat(
    body_surface: base.Surface,
    anatomy: base.Anatomy,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    count = 44
    specs = (
        (1.448, 0.126, 0.115, 0.82, 0.014),
        (1.468, 0.131, 0.120, 0.80, 0.015),
        (1.488, 0.136, 0.125, 0.78, 0.016),
        (1.510, 0.141, 0.132, 0.75, 0.018),
        (1.535, 0.145, 0.138, 0.72, 0.021),
        (1.565, 0.148, 0.145, 0.68, 0.024),
        (1.595, 0.149, 0.149, 0.63, 0.027),
        (1.625, 0.148, 0.151, 0.58, 0.031),
        (1.675, 0.140, 0.145, 0.49, 0.041),
        (1.720, 0.120, 0.128, 0.40, 0.054),
        (1.760, 0.090, 0.104, 0.31, 0.066),
        (1.795, 0.052, 0.064, 0.23, 0.073),
        (1.812, 0.032, 0.038, 0.18, 0.050),
        (1.827, 0.010, 0.012, 0.12, 0.032),
    )
    body_clearances = (
        0.028,
        0.028,
        0.028,
        0.029,
        0.032,
        0.040,
        0.044,
        0.046,
        0.047,
        0.046,
    )
    rings: list[list[Vector]] = []
    for row_index, (z, radius_x, radius_y, opening, back_shift) in enumerate(specs):
        start = -math.pi / 2.0 + opening
        end = 3.0 * math.pi / 2.0 - opening
        ring = []
        for index in range(count):
            angle = start + (end - start) * index / (count - 1)
            back = max(0.0, math.sin(angle))
            side_bias = 0.004 * math.sin(math.pi * row_index / (len(specs) - 1))
            x = radius_x * math.cos(angle) + side_bias
            y = -0.050 + radius_y * math.sin(angle) + back_shift * (0.20 + 0.80 * back)
            if row_index == len(specs) - 1:
                # The final authored arc is a tiny planar crown perimeter.  A
                # single convex cap below closes it without the rejected long
                # apex fan or a visible top hole.
                z_value = z
            else:
                z_value = z - 0.014 * back * (row_index / (len(specs) - 1))
                z_value += 0.004 * math.sin(2.0 * angle + 0.7) * (row_index / (len(specs) - 1))
            if row_index <= 9:
                direction = Vector((math.cos(angle), math.sin(angle), 0.0)).normalized()
                origin = Vector((anatomy.neck.x, anatomy.neck.y, z))
                if row_index >= 2:
                    # The neck landmark can sit just outside the cheek/jaw
                    # contour above the lower neck.  Cast back from a known
                    # outside point on the identical authored radial line,
                    # retaining its angle and Z and always choosing the
                    # outermost head branch.
                    location, normal, _triangle, distance = body_surface.tree.ray_cast(
                        origin + direction * 0.260,
                        -direction,
                        0.520,
                    )
                else:
                    location, normal, _triangle, distance = body_surface.tree.ray_cast(
                        origin,
                        direction,
                        0.260,
                    )
                    if (
                        location is None
                        or normal is None
                        or distance is None
                        or normal.normalized().dot(direction) < 0.08
                    ):
                        location, normal, _triangle, distance = body_surface.tree.ray_cast(
                            origin + direction * 0.260,
                            -direction,
                            0.520,
                        )
                base.require(
                    location is not None
                    and normal is not None
                    and distance is not None
                    and normal.normalized().dot(direction) >= 0.08,
                    f"Ordered BodyV3 hood ray missed at row={row_index}, angle={angle:.4f}",
                )
                ring.append(location + direction * body_clearances[row_index])
            else:
                ring.append(Vector((x, y, z_value)))
        rings.append(ring)

    for row_index, ring in enumerate(rings):
        if row_index <= 9:
            continue
        for point in ring:
            nearest, normal, _distance = body_surface.nearest(point)
            signed = float((point - nearest).dot(normal))
            if signed < 0.024:
                seated = nearest + normal * 0.026
                point.x, point.y, point.z = seated.x, seated.y, seated.z
    vertices, faces = base.loft(rings, close_ring=False)
    last_offset = (len(rings) - 1) * count
    faces.append(tuple(last_offset + index for index in range(count)))
    buffers = base.Buffers()
    buffers.add(vertices, faces)
    obj = base.mesh_object(
        "HunterBodyV3_HoodHat",
        "hood_hat",
        buffers,
        mat,
        "open-front soft hood with fourteen unequal cloth arcs, ten dense ordered BodyV3 neck/head rows, two compact shrinking crown rows, a rear-shifted droop, and a tiny convex crown cap rather than an apex fan",
        fit_audit=True,
    )
    obj["anchorZone"] = "hood_neck"
    obj["anchorClearanceMeters"] = 0.033
    obj["crownClosed"] = True
    obj["crownCapNgonVertices"] = count
    obj["apexFanUsed"] = False
    obj["fabricDroopAuthored"] = True
    obj["literalHatStackedOnHood"] = False
    return obj


def build_cowl(
    body_surface: base.Surface,
    top_surface: base.Surface,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    count = 60
    front_gap = math.radians(52.0)
    start = -math.pi / 2.0 + front_gap / 2.0
    sweep = 2.0 * math.pi - front_gap
    directions = [
        Vector((math.cos(start + sweep * index / (count - 1)), math.sin(start + sweep * index / (count - 1)), 0.0))
        for index in range(count)
    ]
    specs = (
        (1.450, 0.0180, 0.000),
        (1.425, 0.0180, 0.004),
        (1.402, 0.0185, 0.008),
        (1.380, 0.0190, 0.014),
        (1.358, 0.0200, 0.022),
    )

    rings = []
    tucked_points = 0
    for row_index, (base_z, clearance, front_drop) in enumerate(specs):
        strength = row_index / (len(specs) - 1)
        ring = []
        for index, direction in enumerate(directions):
            angle = start + sweep * index / (count - 1)
            front = max(0.0, -math.sin(angle))
            side = abs(math.cos(angle))
            hem = 0.006 * math.sin(5.0 * angle + 0.35) + 0.002 * math.sin(2.0 * angle - 0.20)
            sample_z = base_z - front_drop * front - 0.010 * strength * side + hem * strength
            provisional = torso_proxy_point(
                body_surface,
                top_surface,
                sample_z,
                direction,
                clearance,
            )
            x_weight = max(0.0, min(1.0, (provisional.x - 0.045) / 0.115))
            y_weight = max(0.0, min(1.0, (provisional.y + 0.005) / 0.115))
            tuck_weight = x_weight * x_weight * (3.0 - 2.0 * x_weight)
            tuck_weight *= y_weight * y_weight * (3.0 - 2.0 * y_weight)
            side_weight = max(0.0, min(1.0, (abs(direction.x) - 0.52) / 0.38))
            side_weight = side_weight * side_weight * (3.0 - 2.0 * side_weight)
            rear_weight = max(0.0, min(1.0, (direction.y + 0.20) / 0.75))
            rear_weight = rear_weight * rear_weight * (3.0 - 2.0 * rear_weight)
            lower_weight = max(0.0, min(1.0, (row_index - 1) / 2.0))
            shoulder_release = 0.017 * side_weight * rear_weight * lower_weight
            desired = clearance + shoulder_release - 0.004 * tuck_weight
            point = torso_proxy_point(
                body_surface,
                top_surface,
                sample_z,
                direction,
                desired,
            )
            point += direction * (
                0.0015 * (0.5 + 0.5 * math.sin(3.0 * angle + 0.4)) * strength
            )
            ring.append(point)
            if tuck_weight > 1e-5:
                tucked_points += 1
        rings.append(ring)

    # The accepted torso and T-pose shoulder surfaces change nearest branches
    # across this short positive-shoulder arc.  Preserve the authored angular
    # X/Y stations and distribute the vertical cloth drop over five spans so a
    # single ring edge cannot form a sharp anatomy-cutting chord.
    transition_start = 14
    transition_end = 19
    for row_index in (len(rings) - 2, len(rings) - 1):
        start_z = float(rings[row_index][transition_start].z)
        end_z = float(rings[row_index][transition_end].z)
        for column in range(transition_start + 1, transition_end):
            blend = (column - transition_start) / (transition_end - transition_start)
            blend = blend * blend * (3.0 - 2.0 * blend)
            rings[row_index][column].z = start_z * (1.0 - blend) + end_z * blend

    # The torso and T-pose shoulder branches are adjacent at the lower rear
    # quadrants.  Seat only points that are actually too close before the cage
    # is lofted; never shrinkwrap or mutate the finished open drape.
    for ring in rings:
        for point_index, point in enumerate(ring):
            direction = directions[point_index]
            for _attempt in range(3):
                nearest, normal, _distance = body_surface.nearest(point)
                signed = float((point - nearest).dot(normal))
                if signed >= 0.014:
                    break
                alignment = float(normal.dot(direction))
                if alignment >= 0.12:
                    point += direction * min(0.018, (0.016 - signed) / alignment)
                else:
                    point += normal * min(0.010, 0.016 - signed)

    buffers = base.Buffers()
    buffers.add(*base.loft(rings, close_ring=False))
    obj = base.mesh_object(
        "HunterBodyV3_Cowl",
        "cowl",
        buffers,
        mat,
        "narrow five-stage open-front neck drape with an authored smooth positive-shoulder vertical transition, front weight, irregular curved hem, and no closed lower ring slicing across the shoulders",
        fit_audit=True,
    )
    obj["anchorZone"] = "cowl_neck"
    obj["positiveShoulderMantleTuckVertexCount"] = tucked_points
    obj["positiveShoulderMantleTuckClearanceMeters"] = 0.014
    obj["positiveShoulderSmoothedAngularSpanCount"] = transition_end - transition_start
    obj["separateFromHoodAndMantle"] = True
    obj["anchorClearanceBandMeters"] = [0.014, 0.034]
    return obj


def build_brigandine(
    body_surface: base.Surface,
    top_surface: base.Surface,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    buffers = base.Buffers()
    count = 24
    directions = radial_directions(count)
    levels = (None, 1.180, 1.145, 1.105, 1.065, 1.030, 0.998)
    rings = []
    for level_index, z in enumerate(levels):
        ring = []
        for index, direction in enumerate(directions):
            angle = 2.0 * math.pi * index / count
            front = max(0.0, -math.sin(angle))
            back = max(0.0, math.sin(angle))
            sample_z = (
                1.205 + 0.125 * (front ** 1.45) + 0.075 * (back ** 1.45)
                if z is None
                else z
            )
            point = torso_proxy_point(
                body_surface,
                top_surface,
                sample_z,
                direction,
                0.038 + 0.002 * math.sin(math.pi * level_index / (len(levels) - 1)),
            )
            if z is not None:
                point.z += 0.005 * front
            ring.append(point)
        rings.append(ring)
    buffers.add(*base.loft(rings, close_ring=True))

    # Three short curved lap seams establish four tailored rows without wrapping
    # the torso into four inflated bands.
    seam_angles = [-math.pi / 2.0 - 1.28 + 2.56 * index / 16 for index in range(17)]
    for seam_index, base_z in enumerate((1.292, 1.180, 1.068)):
        rows = []
        for width_sign in (-1.0, 1.0):
            row = []
            for angle in seam_angles:
                direction = Vector((math.cos(angle), math.sin(angle), 0.0))
                side = abs(math.cos(angle))
                z = base_z - 0.010 * side + width_sign * 0.0045
                row.append(
                    torso_proxy_point(
                        body_surface,
                        top_surface,
                        z,
                        direction,
                        0.042 + 0.0010 * seam_index,
                    )
                )
            rows.append(row)
        buffers.add(*base.grid(rows))

    obj = base.mesh_object(
        "HunterBodyV3_TailoredBrigandine",
        "brigandine",
        buffers,
        mat,
        "one waist-shaped shell with a curved high front/back chest yoke and lowered armhole sides, plus three shallow front-and-side lap seams rather than detached torso bands",
        fit_audit=True,
    )
    refine_and_project_object(obj, body_surface, 0.034)
    obj["brigandineRowCount"] = 4
    obj["fullCircumferenceBandCount"] = 0
    obj["visibleBodyProxyFacesReused"] = False
    return obj


def build_mantle(
    body_surface: base.Surface,
    anatomy: base.Anatomy,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    # One asymmetric cloth/fur shoulder patch is authored from ordered
    # accepted-body shoulder arcs.  Its inner and outer skins are sampled
    # independently before their perimeter is closed; no finished closed mesh
    # is projected or collapsed toward the body.
    stations = (
        0.090, 0.105, 0.120, 0.136, 0.148, 0.156,
        0.164, 0.172, 0.182, 0.194, 0.204, 0.214,
    )
    across_count = 11
    outer_rows: list[list[Vector]] = []
    inner_rows: list[list[Vector]] = []
    for station_index, absolute_x in enumerate(stations):
        u = station_index / (len(stations) - 1)
        angle_start = 0.18 + 0.07 * u
        angle_end = math.pi - 0.30 + 0.05 * math.sin(math.pi * u)
        root_release = max(0.0, min(1.0, (absolute_x - 0.160) / 0.054))
        root_release = root_release * root_release * (3.0 - 2.0 * root_release)
        outer_row = []
        inner_row = []
        for across_index in range(across_count):
            v = across_index / (across_count - 1)
            angle = angle_start + (angle_end - angle_start) * v
            crown = math.sin(math.pi * v) ** 1.35
            irregular = 0.5 + 0.5 * math.sin(4.0 * math.pi * v + 0.65 + 0.8 * u)
            inner_clearance = 0.038 + 0.0015 * root_release * crown
            outer_clearance = (
                0.044
                + 0.0004 * crown
                + root_release * (0.0065 * crown + 0.0018 * irregular)
            )
            inner_row.append(shoulder_arc_surface_point(
                body_surface,
                anatomy,
                1,
                absolute_x,
                angle,
                inner_clearance,
            ))
            outer_row.append(shoulder_arc_surface_point(
                body_surface,
                anatomy,
                1,
                absolute_x,
                angle,
                outer_clearance,
            ))
        inner_rows.append(inner_row)
        outer_rows.append(outer_row)

    buffers = base.Buffers()
    buffers.add(*closed_between_grids(outer_rows, inner_rows))
    obj = base.mesh_object(
        "HunterBodyV3_AsymmetricMantle",
        "mantle_pauldrons",
        buffers,
        mat,
        "compact positive-X closed cloth/fur shoulder drape built from twelve ordered accepted-body arcs, independently preserved inner/outer skins, an irregular crown, and unequal curved perimeter hems",
        fit_audit=True,
    )
    obj["anchorZone"] = "mantle_shoulder"
    obj["anchorClearanceMeters"] = 0.035
    obj["furCardsUsed"] = False
    obj["asymmetricSide"] = "Left"
    obj["flatPanelConstruction"] = False
    obj["closedTubeConstruction"] = False
    obj["postClosureProjectionUsed"] = False
    obj["authoredOuterClearanceBandMeters"] = [0.044, 0.0521]
    return obj



def pauldron_shell(
    body_surface: base.Surface,
    anatomy: base.Anatomy,
    side: int,
    layer: int,
    total: int,
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    if total == 3:
        specs = (
            (0.215, 0.255),
            (0.261, 0.320),
            (0.326, 0.392),
        )
    else:
        specs = (
            (0.215, 0.258),
            (0.264, 0.360),
        )
    inner_x, outer_x = specs[layer]
    station_count = 7
    across_count = 11
    outer_rows: list[list[Vector]] = []
    inner_rows: list[list[Vector]] = []
    for station_index in range(station_count):
        u = station_index / (station_count - 1)
        absolute_x = inner_x + (outer_x - inner_x) * u
        # All root-selector samples stay in the strict 35/43 mm band.  Crown
        # relief begins only outside that selector and never depends on a
        # finished-shell projection.
        root_release = max(
            0.0,
            min(1.0, (absolute_x - (abs(anatomy.shoulder.x) + 0.025)) / 0.050),
        )
        root_release = root_release * root_release * (3.0 - 2.0 * root_release)
        angle_start = 0.35 + 0.025 * u
        angle_end = math.pi - 0.35 - 0.020 * u
        outer_row = []
        inner_row = []
        for across_index in range(across_count):
            v = across_index / (across_count - 1)
            angle = angle_start + (angle_end - angle_start) * v
            crown = math.sin(math.pi * v) ** 1.5
            scallop = 0.5 + 0.5 * math.sin(3.0 * math.pi * v + 0.7 * layer + 0.4 * side)
            inner_clearance = 0.039 + 0.001 * root_release * crown
            outer_clearance = 0.044 + root_release * (
                0.0045 * crown * math.sin(math.pi * u)
                + 0.0012 * crown * scallop
            )
            inner_row.append(shoulder_arc_surface_point(
                body_surface,
                anatomy,
                side,
                absolute_x,
                angle,
                inner_clearance,
            ))
            outer_row.append(shoulder_arc_surface_point(
                body_surface,
                anatomy,
                side,
                absolute_x,
                angle,
                outer_clearance,
            ))
        inner_rows.append(inner_row)
        outer_rows.append(outer_row)
    return closed_between_grids(outer_rows, inner_rows)


def build_pauldrons(
    body_surface: base.Surface,
    anatomy: base.Anatomy,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    buffers = base.Buffers()
    for side, total in ((1, 3), (-1, 2)):
        for layer in range(total):
            buffers.add(*pauldron_shell(body_surface, anatomy, side, layer, total))
    obj = base.mesh_object(
        "HunterBodyV3_LayeredPauldrons",
        "mantle_pauldrons",
        buffers,
        mat,
        "five broad closed compound-curved upper-shoulder plates with independently body-derived 39/44 mm root skins, six-millimetre authored seams, and closed perimeters: three positive-X and two negative-X",
        fit_audit=True,
    )
    obj["anchorZone"] = "pauldron_roots"
    obj["anchorClearanceMeters"] = 0.034
    obj["leftShellCount"] = 3
    obj["rightShellCount"] = 2
    obj["maximumAbsoluteXTargetMeters"] = 0.392
    obj["flatCardConstruction"] = False
    obj["fullArmTubeConstruction"] = False
    obj["postClosureProjectionUsed"] = False
    obj["rootInnerOuterClearanceMeters"] = [0.039, 0.044]
    return obj


def build_split_cape(body_surface: base.Surface, mat: bpy.types.Material) -> bpy.types.Object:
    buffers = base.Buffers()
    fold_profiles = {
        1: (0.010, -0.034, 0.050, -0.046, 0.058, -0.040, 0.048, -0.028, 0.012),
        -1: (-0.012, 0.040, -0.052, 0.046, -0.056, 0.043, -0.046, 0.030, -0.010),
    }
    hem_profiles = {
        1: (0.010, -0.012, 0.018, -0.006, 0.026, -0.015, 0.014, -0.008, 0.018),
        -1: (0.018, -0.008, 0.014, -0.015, 0.024, -0.006, 0.018, -0.012, 0.010),
    }
    for side in (1, -1):
        rows = []
        for row_index in range(10):
            v = row_index / 9.0
            center_swing = 0.012 * math.sin(math.pi * v) * side
            inner = 0.052 + 0.012 * v + center_swing
            outer = 0.268 + 0.086 * (v ** 1.25)
            row = []
            for column in range(9):
                u = column / 8.0
                absolute_x = inner + (outer - inner) * u
                yoke_curve = 0.025 * math.sin(math.pi * u) * (1.0 - v)
                fold = fold_profiles[side][column] * (0.15 + 0.85 * v)
                y = 0.150 + 0.070 * (v ** 1.3) + yoke_curve + fold
                z = 1.385 - 0.735 * v
                z -= hem_profiles[side][column] * (v ** 4)
                z += 0.008 * math.sin(math.pi * u) * math.sin(1.3 * math.pi * v)
                row.append(Vector((side * absolute_x, y, z)))
            rows.append(row)
        project_anchor_points([point for row in rows[:2] for point in row], body_surface, 0.035)
        buffers.add(*base.grid(rows))
    push_buffers_outside_body(buffers, body_surface, 0.035)
    obj = base.mesh_object(
        "HunterBodyV3_SplitCape",
        "split_cape",
        buffers,
        mat,
        "two separate cloth panels with a curved yoke, persistent center split, 40-60 mm authored fold depth, increasing back sag and flare, and unequal shaped hems",
        fit_audit=True,
    )
    project_object_anchor_zone(obj, body_surface, "cape_yoke", 0.035)
    obj["capeSectionCount"] = 2
    obj["minimumCenterGapMeters"] = 0.104
    obj["rigidSheetConstruction"] = False
    return obj


def build_limb_gear(
    body_surface: base.Surface,
    top_surface: base.Surface,
    trouser_surface: base.Surface,
    anatomy: base.Anatomy,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    buffers = base.Buffers()
    arm_dirs = arm_directions(12, open_underside=True)
    for side in (-1, 1):
        bracer_rings = []
        bracer_stations = (0.492, 0.535, 0.580, 0.625, 0.670, 0.712)
        for station_index, x in enumerate(bracer_stations):
            ring = section_from_surface(
                top_surface,
                Vector((side * x, anatomy.shoulder.y, anatomy.shoulder.z)),
                arm_dirs,
                0.029 + 0.002 * abs(station_index - 2.5) / 2.5,
                0.30,
            )
            for direction_index, point in enumerate(ring):
                direction = arm_dirs[direction_index]
                crown = max(0.0, -direction.y)
                if station_index == 0:
                    point.x -= side * 0.015 * crown
                elif station_index == len(bracer_stations) - 1:
                    point.x += side * 0.012 * crown
            bracer_rings.append(ring)
        buffers.add(*base.loft(bracer_rings, close_ring=False))

        greave_rings = []
        greave_dirs = greave_directions(side, 12)
        greave_levels = (0.135, 0.205, 0.280, 0.355, 0.425, 0.492)
        for level_index, z in enumerate(greave_levels):
            ring = section_from_same_side_body(
                body_surface,
                Vector((side * abs(anatomy.knee.x), anatomy.knee.y, z)),
                greave_dirs,
                side,
                0.034 + 0.003 * abs(level_index - 2.5) / 2.5,
                0.28,
            )
            for direction_index, point in enumerate(ring):
                front = max(0.0, -greave_dirs[direction_index].y)
                if level_index == 0:
                    point.z -= 0.016 * front
                elif level_index == len(greave_levels) - 1:
                    point.z += 0.025 * front
            base.require(
                all(side * point.x >= 0.010 for point in ring),
                f"Greave section crossed the opposing leg at side={side}, z={z:.3f}",
            )
            greave_rings.append(ring)
        buffers.add(*base.loft(greave_rings, close_ring=False))

    obj = base.mesh_object(
        "HunterBodyV3_LimbGear",
        "limb_gear",
        buffers,
        mat,
        "independent sparse bracers ray-fit to the accepted top envelope and same-leg greaves sampled directly from accepted BodyV3, with open arm undersides and inner leg seams, tapered sections, and hand-shaped front V crowns",
        fit_audit=True,
    )
    # The ordered fit-proxy sections already include the required clearance.
    # Finished open cages are never globally projected because that can fold
    # the opposing greave rims across the center line.
    obj["bracersClosedTubes"] = False
    obj["greavesClosedTubes"] = False
    obj["sameLegSectionSampling"] = True
    obj["postLoftProjectionUsed"] = False
    obj["curvedHemProfiles"] = True
    return obj


def foot_ring(
    body_surface: base.Surface,
    side: int,
    y: float,
    center_x: float,
    center_z: float,
    clearance: float,
    top_gap_radians: float,
    count: int = 24,
) -> list[Vector]:
    base.require(0.0 < top_gap_radians < math.pi, "Boot upper top gap must remain an open cage")
    start = math.pi / 2.0 + top_gap_radians / 2.0
    sweep = 2.0 * math.pi - top_gap_radians
    directions = [
        Vector((math.cos(start + sweep * index / (count - 1)), 0.0, math.sin(start + sweep * index / (count - 1))))
        for index in range(count)
    ]
    center = Vector((side * center_x, y, center_z))
    def cast_radii(origin: Vector) -> list[float | None]:
        result: list[float | None] = []
        for direction in directions:
            location, _normal, _index, distance = body_surface.tree.ray_cast(origin, direction, 0.24)
            valid = (
                location is not None
                and distance is not None
                and 0.002 <= float(distance) <= 0.180
                and side * location.x >= 0.008
            )
            result.append(float(distance) if valid else None)
        return result

    radii = cast_radii(center)
    valid_indices = [index for index, radius in enumerate(radii) if radius is not None]
    if len(valid_indices) < count // 2:
        nearest_point, nearest_normal, nearest_distance = body_surface.nearest(center)
        center = nearest_point - nearest_normal * 0.015
        radii = cast_radii(center)
        valid_indices = [index for index, radius in enumerate(radii) if radius is not None]
    if len(valid_indices) < count // 2:
        print(
            "FOOT_SECTION_DEBUG",
            side,
            y,
            center_x,
            center_z,
            valid_indices,
            [None if value is None else round(value, 5) for value in radii],
            "NEAREST",
            [round(float(value), 5) for value in center],
        )
    base.require(
        len(valid_indices) >= count // 2,
        f"BodyV3 foot section is not a stable star-shaped sample at y={y:.4f}",
    )

    # Preserve angular ordering when a sparse ray lies exactly on the open toe
    # or sole boundary.  Interpolate only its radial distance between the two
    # nearest valid authored directions; never substitute an unrelated nearest
    # body point, which can twist a quad from sole to instep.
    for index, radius in enumerate(radii):
        if radius is not None:
            continue
        previous = next(
            (cursor for cursor in range(index - 1, -1, -1) if radii[cursor] is not None),
            None,
        )
        following = next(
            (cursor for cursor in range(index + 1, count) if radii[cursor] is not None),
            None,
        )
        if previous is None:
            base.require(following is not None, "BodyV3 foot radial interpolation has no valid neighbor")
            radii[index] = float(radii[following])
        elif following is None:
            radii[index] = float(radii[previous])
        else:
            blend = (index - previous) / (following - previous)
            radii[index] = float(radii[previous]) * (1.0 - blend) + float(radii[following]) * blend

    points = []
    for direction, radius in zip(directions, radii):
        base.require(radius is not None, "BodyV3 foot radial interpolation failed")
        point = center + direction * (float(radius) + clearance)
        point.x = side * max(0.012, side * point.x)
        points.append(point)
    return points


def build_boots(
    body_surface: base.Surface,
    anatomy: base.Anatomy,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    buffers = base.Buffers()
    for side in (-1, 1):
        sampled_specs = (
            (0.035, abs(anatomy.ankle.x), 0.086, 0.040, math.radians(110.0)),
            (0.010, abs(anatomy.ankle.x), 0.084, 0.040, math.radians(100.0)),
            (-0.015, abs(anatomy.ankle.x) + 0.002, 0.078, 0.040, math.radians(85.0)),
            (-0.042, abs(anatomy.ankle.x) + 0.006, 0.070, 0.040, math.radians(65.0)),
            (-0.074, abs(anatomy.ankle.x) + 0.006, 0.061, 0.040, math.radians(45.0)),
            (-0.108, abs(anatomy.ankle.x) + 0.008, 0.052, 0.039, math.radians(28.0)),
            (-0.144, abs(anatomy.ankle.x) + 0.010, 0.045, 0.038, math.radians(16.0)),
            (-0.176, abs(anatomy.ankle.x) + 0.012, 0.040, 0.038, math.radians(10.0)),
        )
        sampled_rings = [
            foot_ring(body_surface, side, y, center_x, center_z, clearance, top_gap)
            for y, center_x, center_z, clearance, top_gap in sampled_specs
        ]

        heel_center = Vector((side * abs(anatomy.ankle.x), 0.080, 0.070))
        heel_source_center = Vector((side * sampled_specs[0][1], sampled_specs[0][0], sampled_specs[0][2]))
        heel_ring = [heel_center + (point - heel_source_center) * 0.90 for point in sampled_rings[0]]
        toe_center = Vector((side * abs(anatomy.toes.x), anatomy.toes.y - 0.085, 0.040))
        toe_source_center = Vector((side * sampled_specs[-1][1], sampled_specs[-1][0], sampled_specs[-1][2]))
        toe_ring = [toe_center + (point - toe_source_center) * 0.65 for point in sampled_rings[-1]]
        boot_rings = [heel_ring, *sampled_rings, toe_ring]
        # The heel remains an authored boot opening at macro-cage stage.  Only
        # the forward toe is closed, avoiding an end cap through the accepted
        # heel/ankle anatomy while retaining a rounded toe volume.
        buffers.add(*base.loft(boot_rings, close_ring=False, cap_end=True))

        # A body-derived sole sidewall reads as a continuous sole without a top
        # cap slicing through the enclosed foot.
        footprint_samples = []
        for ring, (y, _center_x, _center_z, _clearance, _top_gap) in zip(sampled_rings, sampled_specs):
            xs = [point.x for point in ring]
            footprint_samples.append((y, min(xs), max(xs)))
        heel_min = min(point.x for point in heel_ring)
        heel_max = max(point.x for point in heel_ring)
        toe_min = min(point.x for point in toe_ring)
        toe_max = max(point.x for point in toe_ring)
        heel_outer = heel_max if side > 0 else heel_min
        heel_inner = heel_min if side > 0 else heel_max
        toe_outer = toe_max if side > 0 else toe_min
        toe_inner = toe_min if side > 0 else toe_max
        outline = [
            Vector((side * abs(anatomy.ankle.x), 0.118, 0.0)),
            Vector((heel_outer + side * 0.008, 0.103, 0.0)),
        ]
        for y, minimum_x, maximum_x in footprint_samples:
            outer_x = maximum_x if side > 0 else minimum_x
            outline.append(Vector((outer_x + side * 0.008, y, 0.0)))
        outline.extend((
            Vector((toe_outer + side * 0.006, anatomy.toes.y - 0.070, 0.0)),
            Vector((side * abs(anatomy.toes.x), anatomy.toes.y - 0.088, 0.0)),
            Vector((toe_inner - side * 0.006, anatomy.toes.y - 0.070, 0.0)),
        ))
        for y, minimum_x, maximum_x in reversed(footprint_samples):
            inner_x = minimum_x if side > 0 else maximum_x
            outline.append(Vector((inner_x - side * 0.008, y, 0.0)))
        outline.append(Vector((heel_inner - side * 0.008, 0.103, 0.0)))
        lower = [Vector((point.x, point.y, 0.005)) for point in outline]
        upper = [Vector((point.x, point.y, 0.019)) for point in outline]
        buffers.add(*base.loft((lower, upper), close_ring=True))

    obj = base.mesh_object(
        "HunterBodyV3_Boots",
        "limb_gear",
        buffers,
        mat,
        "paired boots sampled from eight actual BodyV3 foot and ankle cross-sections, with narrowed heel, raised instep, rounded toe closure, and body-derived sole sidewalls",
        fit_audit=True,
    )
    resolve_body_surface_overlaps(
        obj,
        body_surface,
        0.032,
        maximum_iterations=16,
        maximum_total_move=0.060,
    )
    obj["hardCodedShoeLast"] = False
    obj["anchorZone"] = "boot_ankles"
    obj["anchorClearanceMeters"] = 0.040
    obj["bodyFootSectionCountPerSide"] = 8
    obj["continuousSoleSidewall"] = True
    return obj


def build_harness_belt(
    body_surface: base.Surface,
    brigandine_surface: base.Surface,
    mat: bpy.types.Material,
) -> bpy.types.Object:
    buffers = base.Buffers()
    diagonal_controls = (
        (0.174, 1.366),
        (0.138, 1.318),
        (0.096, 1.268),
        (0.052, 1.216),
        (0.006, 1.163),
        (-0.042, 1.108),
        (-0.090, 1.052),
        (-0.136, 0.995),
        (-0.164, 0.970),
    )
    diagonal = []
    for control_index in range(len(diagonal_controls) - 1):
        first = Vector(diagonal_controls[control_index])
        second = Vector(diagonal_controls[control_index + 1])
        for subdivision in range(4):
            point = first.lerp(second, subdivision / 4.0)
            diagonal.append((float(point.x), float(point.y)))
    diagonal.append(diagonal_controls[-1])

    # Select the frontmost authored layer at every ribbon endpoint.  A real
    # +Y armor ray is used only when it hits a front-facing brigandine surface;
    # misses fall back to the continuous accepted-body reference rather than
    # nearest-point snapping to a detached seam or boundary.  Each row is
    # flattened within 3 mm so a seam hit cannot twist the ribbon across itself.
    diagonal_rows = []
    for index, (x, z) in enumerate(diagonal):
        previous = Vector(diagonal[max(0, index - 1)])
        following = Vector(diagonal[min(len(diagonal) - 1, index + 1)])
        tangent = (following - previous).normalized()
        lateral = Vector((-tangent.y, tangent.x))
        endpoints = []
        for sign in (-1.0, 1.0):
            sample = Vector((x, z)) + lateral * 0.044 * 0.5 * sign
            origin = Vector((sample.x, -0.800, sample.y))
            body_location, body_normal, _body_triangle, body_distance = body_surface.tree.ray_cast(
                origin,
                Vector((0.0, 1.0, 0.0)),
                1.600,
            )
            candidates = []
            if (
                body_location is not None
                and body_normal is not None
                and body_distance is not None
                and body_normal.normalized().dot(Vector((0.0, -1.0, 0.0))) >= 0.05
            ):
                if sample.y <= 1.035:
                    candidates.append(Vector((sample.x, body_location.y - 0.060, sample.y)))
                else:
                    candidates.append(body_location + body_normal.normalized() * 0.040)
            location, normal, _triangle, distance = brigandine_surface.tree.ray_cast(
                origin,
                Vector((0.0, 1.0, 0.0)),
                1.600,
            )
            if (
                sample.y > 1.035
                and
                location is not None
                and normal is not None
                and distance is not None
            ):
                normalized = normal.normalized()
                if normalized.dot(Vector((0.0, -1.0, 0.0))) >= 0.05:
                    candidates.append(location + normalized * 0.010)
                else:
                    candidates.append(location + Vector((0.0, -0.010, 0.0)))
            if not candidates:
                nearest, nearest_normal, _distance = body_surface.nearest(
                    Vector((sample.x, -0.100, sample.y))
                )
                candidates.append(nearest + nearest_normal * 0.040)
            endpoints.append(min(candidates, key=lambda point: point.y))
        row_front_y = min(point.y for point in endpoints)
        for point in endpoints:
            point.y = min(point.y, row_front_y + 0.003)
        diagonal_rows.append(endpoints)
    buffers.add(*base.grid(diagonal_rows))
    belt_rings = [
        base.radial_ring(body_surface, z, 22, 0.052, front_ease=0.002, side_ease=0.001)
        for z in (0.918, 0.970)
    ]
    buffers.add(*base.loft(belt_rings, close_ring=True))
    obj = base.mesh_object(
        "HunterBodyV3_HarnessBelt",
        "harness_belt",
        buffers,
        mat,
        "one continuous flat diagonal harness following the frontmost accepted body-or-brigandine layer at a ten-millimetre armor stand-off, then transitioning onto the continuous body-front belt join with only short square endpoints concealed",
        fit_audit=True,
    )
    obj["roundTubeStrapsUsed"] = False
    obj["pointedStrapEndsUsed"] = False
    armor_clearances = []
    for vertex in list(obj.data.vertices)[: len(diagonal) * 2]:
        if not 0.998 <= vertex.co.z <= 1.330:
            continue
        nearest, normal, _distance = brigandine_surface.nearest(vertex.co)
        armor_clearances.append(float((vertex.co - nearest).dot(normal)))
    base.require(armor_clearances, "Harness has no authored brigandine-layer audit samples")
    obj["bodyFrontFallbackClearanceMeters"] = 0.040
    obj["beltJoinBodyFrontClearanceMeters"] = 0.060
    obj["brigandineSurfaceClearanceMeters"] = 0.010
    obj["brigandineOuterBandMeters"] = [0.038, 0.043]
    obj["brigandineSurfaceUsedForLayerAudit"] = True
    obj["minimumSampledBrigandineSignedClearanceMeters"] = min(armor_clearances)
    obj["diagonalHarnessStationCount"] = len(diagonal)
    obj["wholeSpanBuriedUnderArmor"] = False
    return obj


def catmull_scalar(values: Sequence[tuple[float, float]], t: float) -> float:
    if t <= values[0][0]:
        return values[0][1]
    if t >= values[-1][0]:
        return values[-1][1]
    segment = 0
    for index in range(len(values) - 1):
        if values[index][0] <= t <= values[index + 1][0]:
            segment = index
            break
    t1, p1 = values[segment]
    t2, p2 = values[segment + 1]
    p0 = values[max(0, segment - 1)][1]
    p3 = values[min(len(values) - 1, segment + 2)][1]
    u = (t - t1) / max(1e-8, t2 - t1)
    return 0.5 * (
        (2.0 * p1)
        + (-p0 + p2) * u
        + (2.0 * p0 - 5.0 * p1 + 4.0 * p2 - p3) * u * u
        + (-p0 + 3.0 * p1 - 3.0 * p2 + p3) * u * u * u
    )


def swept_rounded_rectangle(
    points: Sequence[Vector],
    widths: Sequence[float],
    depths: Sequence[float],
) -> tuple[list[Vector], list[tuple[int, ...]]]:
    """Sweep one ordered, nonduplicated eight-station limb section."""
    base.require(len(points) == len(widths) == len(depths), "R2 bow sweep profile mismatch")
    profile = (
        (1.00, 0.42),
        (0.66, 1.00),
        (-0.66, 1.00),
        (-1.00, 0.42),
        (-1.00, -0.42),
        (-0.66, -1.00),
        (0.66, -1.00),
        (1.00, -0.42),
    )
    base.require(len(set(profile)) == len(profile), "R2 bow section contains duplicate stations")
    rings = []
    frame_axis = Vector((1.0, 0.0, 0.0))
    for index, point in enumerate(points):
        previous = points[max(0, index - 1)]
        following = points[min(len(points) - 1, index + 1)]
        tangent = (following - previous).normalized()
        first = tangent.cross(frame_axis).normalized()
        second = tangent.cross(first).normalized()
        rings.append([
            point + first * (widths[index] * first_scale) + second * (depths[index] * second_scale)
            for first_scale, second_scale in profile
        ])
    return base.loft(rings, close_ring=True, cap_start=True, cap_end=True)


def build_bow(mat: bpy.types.Material) -> tuple[bpy.types.Object, tuple[Vector, Vector, Vector]]:
    authored = (
        (-1.00, -0.100, 0.007, 0.004),
        (-0.86, -0.135, 0.009, 0.005),
        (-0.68, -0.195, 0.012, 0.006),
        (-0.48, -0.215, 0.014, 0.007),
        (-0.27, -0.155, 0.017, 0.008),
        (-0.10, -0.090, 0.024, 0.013),
        (0.00, -0.064, 0.033, 0.017),
        (0.10, -0.090, 0.024, 0.013),
        (0.27, -0.155, 0.017, 0.008),
        (0.48, -0.215, 0.014, 0.007),
        (0.68, -0.195, 0.012, 0.006),
        (0.86, -0.135, 0.009, 0.005),
        (1.00, -0.100, 0.007, 0.004),
    )
    y_values = [(row[0], row[1]) for row in authored]
    width_values = [(row[0], row[2]) for row in authored]
    depth_values = [(row[0], row[3]) for row in authored]
    half_length = 1.305 / 2.0
    grip_z = 1.375
    station_count = 41
    ts = [-1.0 + 2.0 * index / (station_count - 1) for index in range(station_count)]
    points = [Vector((0.765, catmull_scalar(y_values, t), grip_z + t * half_length)) for t in ts]
    widths = [max(0.0065, catmull_scalar(width_values, t)) for t in ts]
    depths = [max(0.0038, catmull_scalar(depth_values, t)) for t in ts]
    buffers = base.Buffers()
    buffers.add(*swept_rounded_rectangle(points, widths, depths))
    obj = base.mesh_object(
        "HunterBodyV3_RecurveBow",
        "weapon_quiver",
        buffers,
        mat,
        "1.305 m noncircular recurve resampled through forty-one smooth tapered stations from the retained R1 silhouette direction",
        fit_audit=False,
    )
    obj["lengthMeters"] = 1.305
    obj["futureBindingSocket"] = "Socket_Weapon_L"
    obj["roundTubeConstruction"] = False
    obj["authoredStationCount"] = 41
    return obj, (points[0], points[len(points) // 2], points[-1])


def build_weapon_string_r2(
    name: str,
    bow_points: tuple[Vector, Vector, Vector],
    nock: Vector,
    mat: bpy.types.Material,
    visible: bool,
) -> bpy.types.Object:
    """Build one state string with localized bow-tip attachment segments."""
    lower, _grip, upper = bow_points

    def inset(tip: Vector) -> Vector:
        distance = (nock - tip).length
        base.require(distance > 0.050, "R2 bow string state is too short")
        return tip.lerp(nock, 0.040 / distance)

    def near_nock(tip: Vector) -> Vector:
        distance = (tip - nock).length
        return nock.lerp(tip, 0.040 / distance)

    path = (
        lower,
        inset(lower),
        near_nock(lower),
        nock,
        near_nock(upper),
        inset(upper),
        upper,
    )
    radii = (0.0007, 0.00135, 0.0010, 0.0008, 0.0010, 0.00135, 0.0007)
    rings: list[list[Vector]] = []
    for index, point in enumerate(path):
        previous = path[max(0, index - 1)]
        following = path[min(len(path) - 1, index + 1)]
        tangent = (following - previous).normalized()
        reference = Vector((1.0, 0.0, 0.0))
        first = tangent.cross(reference).normalized()
        second = tangent.cross(first).normalized()
        rings.append([
            point + radii[index] * (
                math.cos(2.0 * math.pi * cursor / 3) * first
                + math.sin(2.0 * math.pi * cursor / 3) * second
            )
            for cursor in range(3)
        ])
    buffers = base.Buffers()
    buffers.add(*base.loft(rings, close_ring=True, cap_start=True, cap_end=True))
    obj = base.mesh_object(
        name,
        "weapon_quiver",
        buffers,
        mat,
        "separate tapered closed weapon-state string with exact bow-tip caps plus 40 mm near-tip and near-draw stations that localize all nock intersections",
        fit_audit=False,
    )
    obj.hide_render = not visible
    obj.hide_set(not visible)
    obj["weaponState"] = "Static" if "Static" in name else "Draw"
    obj["futureBinding"] = "Socket_Weapon_L" if "Static" in name else "Socket_Weapon_L+Socket_Weapon_R"
    obj["bowEndpointStationsExact"] = True
    obj["nearTipStationDistanceMeters"] = 0.040
    obj["closedTipCapsInsideBowNocks"] = True
    obj["endpointRadiusMeters"] = radii[0]
    obj["nockRadiusMeters"] = radii[2]
    return obj


def build_nocked_arrow_r2(mat: bpy.types.Material) -> bpy.types.Object:
    """Author the draw arrow at the shifted nock while preserving 0.30 m draw."""
    nock = Vector((0.765, 0.280, 1.375))
    tip = Vector((0.765, -0.780, 1.375))
    buffers = base.Buffers()
    buffers.add(*base.thin_path((nock, tip), 0.0035, 6))
    head_front = (
        tip + Vector((-0.018, 0.030, 0.000)),
        tip + Vector((0.000, -0.030, 0.000)),
        tip + Vector((0.018, 0.030, 0.000)),
    )
    head_back = tuple(point + Vector((0.0, 0.004, 0.010)) for point in head_front)
    buffers.add(*base.prism(head_front, head_back))
    buffers.add(*base.fletching_prism(Vector((0.765, 0.155, 1.375)), 1.0))
    obj = base.mesh_object(
        "HunterBodyV3_NockedArrow",
        "weapon_quiver",
        buffers,
        mat,
        "separate draw-only nocked arrow aligned to the 0.280 m draw nock with a slim shaft, compact head, and thickened fletching",
        fit_audit=False,
    )
    obj.hide_render = True
    obj.hide_set(True)
    obj["weaponState"] = "DrawOnly"
    obj["futureBindingSocket"] = "Socket_Weapon_R"
    obj["nockPositionMeters"] = [float(value) for value in nock]
    return obj


def build_quiver_arrows(mat: bpy.types.Material) -> bpy.types.Object:
    buffers = base.Buffers()
    count = 14
    outer_rings = []
    for index in range(8):
        t = index / 7.0
        center = Vector((0.105 + 0.142 * t, 0.350 + 0.030 * t, 0.842 + 0.585 * t))
        radius_x = 0.041 + 0.020 * t
        radius_y = 0.030 + 0.012 * t
        ring = []
        for cursor in range(count):
            angle = 2.0 * math.pi * cursor / count
            cosine = math.cos(angle)
            sine = math.sin(angle)
            ring.append(
                center
                + Vector((
                    radius_x * math.copysign(abs(cosine) ** 0.74, cosine),
                    radius_y * sine,
                    0.0,
                ))
            )
        outer_rings.append(ring)
    buffers.add(*base.loft(outer_rings, close_ring=True, cap_start=True))

    # The open rim is authored separately; no Solidify modifier is applied to
    # the combined quiver and already-closed arrow components.
    outer_top = outer_rings[-1]
    center_top = sum(outer_top, Vector((0.0, 0.0, 0.0))) / len(outer_top)
    inner_top = [center_top + (point - center_top) * 0.86 for point in outer_top]
    buffers.add(*base.loft((inner_top, outer_top), close_ring=True))

    arrow_offsets = (
        (-0.036, -0.010),
        (-0.018, 0.010),
        (0.000, -0.012),
        (0.018, 0.009),
        (0.026, -0.006),
    )

    def quiver_center_at(z: float) -> Vector:
        t = (z - 0.842) / 0.585
        return Vector((0.105 + 0.142 * t, 0.350 + 0.030 * t, z))

    for arrow_index, (offset_x, offset_y) in enumerate(arrow_offsets):
        lower_z = 1.340 + 0.006 * (arrow_index % 2)
        upper_z = 1.610 + 0.026 * arrow_index
        offset = Vector((offset_x, offset_y, 0.0))
        lower = quiver_center_at(lower_z) + offset
        upper = quiver_center_at(upper_z) + offset
        buffers.add(*base.thin_path((lower, upper), 0.0028, 6))
        tangent = (upper - lower).normalized()
        vane_center = upper - tangent * 0.056
        for sign in (-1.0, 1.0):
            front = (
                vane_center + Vector((-0.0025, sign * 0.0015, -0.021)),
                vane_center + Vector((-0.0025, sign * 0.018, 0.0)),
                vane_center + Vector((-0.0025, sign * 0.0015, 0.043)),
            )
            back = tuple(point + Vector((0.005, 0.0, 0.0)) for point in front)
            buffers.add(*base.prism(front, back))
    obj = base.mesh_object(
        "HunterBodyV3_QuiverArrows",
        "weapon_quiver",
        buffers,
        mat,
        "tapered open oval back quiver with a separately authored rim and five staggered arrows; closed arrow pieces are never re-solidified",
        fit_audit=False,
    )
    obj["arrowCount"] = 5
    obj["futureBindingSocket"] = "Socket_Back"
    obj["roundCylinderQuiver"] = False
    obj["solidifyModifierUsed"] = False
    return obj


def configure_scene(
    body: bpy.types.Object,
    eyes: Sequence[bpy.types.Object],
    kit: Sequence[bpy.types.Object],
) -> bpy.types.Object:
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_WORKBENCH"
    scene.display.shading.light = "STUDIO"
    scene.display.shading.studio_light = "paint.sl"
    scene.display.shading.color_type = "MATERIAL"
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = "WORLD"
    scene.display.shading.curvature_ridge_factor = 1.7
    scene.display.shading.curvature_valley_factor = 1.3
    scene.render.resolution_x = 1600
    scene.render.resolution_y = 1600
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    if scene.world is None:
        scene.world = bpy.data.worlds.new("HunterBodyV3R2World")
    scene.world.color = (0.010, 0.014, 0.020)
    camera_data = bpy.data.cameras.new("HunterBodyV3R2ReviewCameraData")
    camera_data.type = "ORTHO"
    camera = bpy.data.objects.new("HunterBodyV3R2ReviewCamera", camera_data)
    scene.collection.objects.link(camera)
    scene.camera = camera
    for obj in (body, *eyes, *kit):
        if obj.name not in {"HunterBodyV3_DrawString", "HunterBodyV3_NockedArrow"}:
            obj.hide_render = False
    return camera


def render_evidence(camera: bpy.types.Object, evidence_root: Path) -> list[Path]:
    views = {
        "FrontThreeQuarter": (Vector((3.4, -5.8, 2.35)), Vector((0.0, 0.0, 0.98)), 2.30),
        "BackThreeQuarter": (Vector((3.8, 5.6, 2.20)), Vector((0.0, 0.08, 1.02)), 2.30),
        "Front": (Vector((0.0, -6.0, 1.15)), Vector((0.0, 0.0, 0.98)), 2.30),
        "Side": (Vector((6.0, 0.0, 1.15)), Vector((0.0, 0.0, 0.98)), 2.30),
        "Back": (Vector((0.0, 6.0, 1.15)), Vector((0.0, 0.08, 0.98)), 2.30),
        "HeadClose": (Vector((2.4, -4.5, 1.92)), Vector((0.0, 0.0, 1.635)), 0.67),
        "ShoulderClose": (Vector((3.0, -5.1, 1.78)), Vector((0.060, 0.0, 1.420)), 0.88),
    }
    paths = []
    for label, (location, target, scale) in views.items():
        camera.location = location
        camera.data.ortho_scale = scale
        base.look_at(camera, target)
        path = evidence_root / f"{MODEL_NAME}_{label}.png"
        bpy.context.scene.render.filepath = str(path)
        bpy.ops.render.render(write_still=True)
        paths.append(path)
    return paths


def main() -> None:
    base.require((5, 2, 0) <= bpy.app.version < (5, 3, 0), f"Blender 5.2 is required, found {bpy.app.version_string}")
    args = parse_args()
    output_root = args.output_root
    final_evidence_root = output_root / "Evidence"
    final_blend_path = output_root / f"{MODEL_NAME}.blend"
    final_report_path = output_root / f"{MODEL_NAME}.report.json"
    final_review_path = output_root / f"{MODEL_NAME}.manual-review-request.json"

    verified_inputs = base.verify_inputs()
    r1_review = json.loads(R1_REJECT.read_text(encoding="utf-8"))
    base.require(r1_review["status"] == "REJECT_MANUAL_MACRO_FORM", "R1 is not hash-locked as rejected")
    base.require(r1_review["frozen"] is True, "R1 rejection is not frozen")

    body, eyes, fit_top, fit_trousers, anatomy = base.load_accepted_sources()
    body_surface = base.Surface(body)
    top_surface = base.Surface(fit_top)
    trouser_surface = base.Surface(fit_trousers)

    materials = {
        "body": base.material("HunterBodyV3R2_BodyClay", (0.43, 0.33, 0.27), 0.86),
        "eyes": base.material("HunterBodyV3R2_EyeClay", (0.055, 0.075, 0.085), 0.55),
        "base": base.material("HunterBodyV3R2_BaseClothClay", (0.095, 0.230, 0.155), 0.92),
        "hood": base.material("HunterBodyV3R2_HoodClay", (0.065, 0.160, 0.105), 0.94),
        "cowl": base.material("HunterBodyV3R2_CowlClay", (0.130, 0.290, 0.190), 0.94),
        "armor": base.material("HunterBodyV3R2_ArmorClay", (0.265, 0.190, 0.105), 0.72),
        "fur": base.material("HunterBodyV3R2_FurClay", (0.54, 0.48, 0.39), 0.96),
        "metal": base.material("HunterBodyV3R2_PauldronClay", (0.39, 0.29, 0.16), 0.58),
        "cape": base.material("HunterBodyV3R2_CapeClay", (0.065, 0.190, 0.125), 0.96),
        "leather": base.material("HunterBodyV3R2_LeatherClay", (0.205, 0.105, 0.065), 0.82),
        "bow": base.material("HunterBodyV3R2_BowClay", (0.38, 0.18, 0.075), 0.68),
        "string": base.material("HunterBodyV3R2_StringClay", (0.025, 0.022, 0.018), 0.88),
    }
    body.data.materials.clear()
    body.data.materials.append(materials["body"])
    for eye in eyes:
        eye.data.materials.clear()
        eye.data.materials.append(materials["eyes"])

    kit: list[bpy.types.Object] = []
    kit.append(build_base_garments(body_surface, top_surface, trouser_surface, anatomy, materials["base"]))
    kit.append(build_hood_hat(body_surface, anatomy, materials["hood"]))
    kit.append(build_cowl(body_surface, top_surface, materials["cowl"]))
    brigandine = build_brigandine(body_surface, top_surface, materials["armor"])
    kit.append(brigandine)
    kit.append(build_mantle(body_surface, anatomy, materials["fur"]))
    kit.append(build_pauldrons(body_surface, anatomy, materials["metal"]))
    kit.append(build_split_cape(body_surface, materials["cape"]))
    kit.append(build_limb_gear(body_surface, top_surface, trouser_surface, anatomy, materials["leather"]))
    kit.append(build_boots(body_surface, anatomy, materials["leather"]))
    kit.append(build_harness_belt(body_surface, base.Surface(brigandine), materials["leather"]))
    bow, bow_points = build_bow(materials["bow"])
    kit.append(bow)
    kit.append(build_quiver_arrows(materials["leather"]))
    # Shift both state nocks together so the resting string clears the grip;
    # their relative travel and the accepted 0.30 m ready-to-draw gate stay exact.
    static_nock = Vector((0.765, -0.020, anatomy.shoulder.z))
    draw_nock = Vector((0.765, 0.280, anatomy.shoulder.z))
    base.require(
        abs(float(draw_nock.y - static_nock.y) - 0.300) <= 1e-6,
        "0.30 m draw gate changed",
    )
    kit.append(build_weapon_string_r2("HunterBodyV3_StaticString", bow_points, static_nock, materials["string"], True))
    kit.append(build_weapon_string_r2("HunterBodyV3_DrawString", bow_points, draw_nock, materials["string"], False))
    kit.append(build_nocked_arrow_r2(materials["bow"]))
    base.require({obj.name for obj in kit} == EXPECTED_KIT_OBJECTS, "Fresh R2 kit object contract changed")

    by_name = {obj.name: obj for obj in kit}
    anchor_clamps = {
        "HunterBodyV3_SplitCape": ("cape_yoke", 0.044, 16),
        "HunterBodyV3_Boots": ("boot_ankles", 0.040, 16),
    }
    anchor_p95_limits = {
        "HunterBodyV3_HoodHat": 0.045,
        "HunterBodyV3_Cowl": 0.035,
        "HunterBodyV3_AsymmetricMantle": 0.045,
        "HunterBodyV3_LayeredPauldrons": 0.045,
        "HunterBodyV3_SplitCape": 0.045,
        "HunterBodyV3_Boots": 0.045,
    }
    for name, (zone_name, target, maximum_edges) in anchor_clamps.items():
        intrinsic = anchor_clearance_report(body_surface, by_name[name], zone_name, anatomy)
        if (
            float(intrinsic["maximumSignedClearanceMeters"]) > 0.045 + 1e-7
            or float(intrinsic["p95SignedClearanceMeters"]) > anchor_p95_limits[name] + 1e-7
        ):
            add_verifier_anchor_contour_support(
                by_name[name],
                body_surface,
                zone_name,
                anatomy,
                maximum_edges=maximum_edges,
            )
            enforce_verifier_anchor_bulk(by_name[name], body_surface, zone_name, anatomy, target)
        else:
            by_name[name]["verifierAnchorClamp"] = False
            by_name[name]["verifierAnchorIntrinsicPass"] = True
            by_name[name]["verifierAnchorClampZone"] = zone_name

    # These four forms must pass from their authored open grids/center surfaces.
    # Mutating a finished closed hood, cowl, mantle, or pauldron shell can fold
    # its two skins together, so the strict audits below report any intrinsic
    # failure without applying a post-closure clamp.
    for name, zone_name in (
        ("HunterBodyV3_HoodHat", "hood_neck"),
        ("HunterBodyV3_Cowl", "cowl_neck"),
        ("HunterBodyV3_AsymmetricMantle", "mantle_shoulder"),
        ("HunterBodyV3_LayeredPauldrons", "pauldron_roots"),
    ):
        by_name[name]["verifierAnchorClamp"] = False
        by_name[name]["verifierAnchorIntrinsicOnly"] = True
        by_name[name]["verifierAnchorClampZone"] = zone_name

    base.require(set(EXPECTED_OBJECT_FAMILIES) == EXPECTED_KIT_OBJECTS, "Verifier object-family contract changed")
    base.require(len(EXCEPTION_AABBS) == 51, "Verifier exception AABB contract changed")
    base.require(len(DESIGNED_LAYER_EXCEPTIONS) == 22, "Verifier inter-object exception contract changed")
    base.require(
        set(DESIGNED_LAYER_EXCEPTION_ZONES) == set(DESIGNED_LAYER_EXCEPTIONS),
        "Verifier inter-object exception-zone keys changed",
    )
    base.require(len(INTRA_OBJECT_ISLAND_EXCEPTIONS) == 42, "Verifier intra-object exception contract changed")

    depsgraph = bpy.context.evaluated_depsgraph_get()
    depsgraph.update()
    snapshots = {
        obj.name: mesh_snapshot(obj, depsgraph)
        for obj in kit
    }

    family_reports: dict[str, dict[str, object]] = {}
    object_reports: dict[str, dict[str, object]] = {}
    failures: list[str] = []
    warnings: list[str] = []
    for obj in kit:
        snapshot = snapshots[obj.name]
        integrity = snapshot.integrity
        triangles = len(snapshot.triangles)
        family = str(obj["macroFamily"])
        expected_family = EXPECTED_OBJECT_FAMILIES[obj.name]
        if family != expected_family:
            failures.append(
                f"macro-family contract changed: {obj.name}: {family} != {expected_family}"
            )
        expected_visible = obj.name not in HIDDEN_DEFAULT_REVIEW_OBJECTS
        render_visible = not obj.hide_render
        viewport_visible = not obj.hide_get()
        if render_visible != expected_visible or viewport_visible != expected_visible:
            failures.append(
                f"default-review visibility changed: {obj.name}: "
                f"render={render_visible}, viewport={viewport_visible}, expected={expected_visible}"
            )
        minimum_islands, maximum_islands = EXPECTED_ISLAND_RANGES[obj.name]
        if not minimum_islands <= integrity["connectedIslands"] <= maximum_islands:
            failures.append(
                f"connected-island count changed: {obj.name}: "
                f"{integrity['connectedIslands']} outside [{minimum_islands},{maximum_islands}]"
            )
        if len(snapshot.islands) != integrity["connectedIslands"]:
            failures.append(
                f"polygon-island decomposition differs: {obj.name}: "
                f"{len(snapshot.islands)} != {integrity['connectedIslands']}"
            )
        expected_labels = EXPECTED_ISLAND_LABELS.get(obj.name)
        if expected_labels is not None:
            actual_labels = tuple(island.label for island in snapshot.islands)
            if actual_labels != expected_labels:
                failures.append(
                    f"connected-island labels changed: {obj.name}: "
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
                        f"connected-island geometry failure: {obj.name}/{island.label}: "
                        f"{key}={island.integrity[key]}"
                    )
        if obj.name in CLOSED_VOLUME_OBJECTS:
            if integrity["boundaryEdges"] != 0:
                failures.append(
                    f"closed source volume has boundary edges: {obj.name}: "
                    f"{integrity['boundaryEdges']}"
                )
            boundary_contract: dict[str, object] = {
                "kind": "closed volume",
                "requiredBoundaryEdges": 0,
            }
        else:
            if not 0 < integrity["boundaryEdges"] <= 1_024:
                failures.append(
                    f"intentional source-cage boundary count is implausible: "
                    f"{obj.name}: {integrity['boundaryEdges']}"
                )
            boundary_contract = {
                "kind": "intentional open source-only macro cage",
                "reason": INTENTIONAL_OPEN_CAGE_BOUNDARIES[obj.name],
                "maximumBoundaryEdges": 1_024,
                "surfacedProductionTopologyAccepted": False,
            }
        object_report: dict[str, object] = {
            "family": family,
            "construction": str(obj["construction"]),
            "triangles": triangles,
            "integrity": integrity,
            "uvLayerCount": len(obj.data.uv_layers),
            "armatureModifierCount": sum(modifier.type == "ARMATURE" for modifier in obj.modifiers),
            "promotionAllowed": bool(obj["promotionAllowed"]),
            "visibleInDefaultReview": render_visible,
            "coincidentGeometry": coincident_geometry_report(obj),
            "boundaryContract": boundary_contract,
            "connectedIslandSnapshots": island_reports,
        }
        if object_report["coincidentGeometry"]["coincidentFaceCount"] != 0:
            failures.append(f"{obj.name} has coincident duplicate faces")
        if (
            obj.name == "HunterBodyV3_RecurveBow"
            and object_report["coincidentGeometry"]["coincidentVertexCount"] != 0
        ):
            failures.append("HunterBodyV3_RecurveBow has coincident duplicate cross-section vertices")
        if bool(obj["bodyFitAudit"]):
            fit = clearance_report(body_surface, obj)
            object_report["bodyFit"] = fit
            overlap_pairs = int(fit["bodySurfaceTriangleOverlapPairs"])
            if overlap_pairs > 0:
                object_report["bodyOverlapLocationDebug"] = body_overlap_location_summary(body_surface, obj)
                if obj.name in AUTHORED_ISLAND_LABELS:
                    object_report["bodyOverlapByAuthoredIsland"] = body_overlap_by_authored_island(
                        body_surface,
                        obj,
                        AUTHORED_ISLAND_LABELS[obj.name],
                    )
                failures.append(f"{obj.name} intersects accepted BodyV3 in {overlap_pairs} triangle pairs")
            if float(fit["minimumSignedClearanceMeters"]) < -0.001:
                failures.append(
                    f"{obj.name} minimum signed body clearance "
                    f"{fit['minimumSignedClearanceMeters']:.6f} is below -0.001000 m"
                )
            p95_limit, maximum_limit = BODY_BULK_LIMITS[obj.name]
            fit["p95BulkLimitMeters"] = p95_limit
            fit["absoluteBulkLimitMeters"] = maximum_limit
            if float(fit["p95SignedClearanceMeters"]) > p95_limit:
                failures.append(
                    f"{obj.name} p95 body clearance {fit['p95SignedClearanceMeters']:.6f} exceeds {p95_limit:.6f} m bulk limit"
                )
            if float(fit["maximumSignedClearanceMeters"]) > maximum_limit:
                failures.append(
                    f"{obj.name} maximum body clearance {fit['maximumSignedClearanceMeters']:.6f} exceeds {maximum_limit:.6f} m bulk limit"
                )
            anchor_specs = {
                "HunterBodyV3_HoodHat": ("hood_neck", 0.045),
                "HunterBodyV3_Cowl": ("cowl_neck", 0.035),
                "HunterBodyV3_AsymmetricMantle": ("mantle_shoulder", 0.045),
                "HunterBodyV3_LayeredPauldrons": ("pauldron_roots", 0.045),
                "HunterBodyV3_SplitCape": ("cape_yoke", 0.045),
                "HunterBodyV3_Boots": ("boot_ankles", 0.045),
            }
            if obj.name in anchor_specs:
                zone_name, anchor_p95_limit = anchor_specs[obj.name]
                anchor_maximum_limit = 0.045
                anchor = anchor_clearance_report(body_surface, obj, zone_name, anatomy)
                anchor["p95BulkLimitMeters"] = anchor_p95_limit
                anchor["absoluteBulkLimitMeters"] = anchor_maximum_limit
                anchor["authoredTargetClearanceMeters"] = 0.035
                object_report["anchorZoneBodyFit"] = anchor
                if float(anchor["p95SignedClearanceMeters"]) > anchor_p95_limit:
                    failures.append(
                        f"{obj.name} anchor-zone p95 body clearance "
                        f"{anchor['p95SignedClearanceMeters']:.6f} exceeds {anchor_p95_limit:.6f} m"
                    )
                if float(anchor["maximumSignedClearanceMeters"]) > anchor_maximum_limit:
                    failures.append(
                        f"{obj.name} anchor-zone maximum body clearance "
                        f"{anchor['maximumSignedClearanceMeters']:.6f} exceeds {anchor_maximum_limit:.6f} m"
                    )
        object_reports[obj.name] = object_report
        aggregate = family_reports.setdefault(family, {"objectCount": 0, "triangles": 0, "objects": []})
        aggregate["objectCount"] = int(aggregate["objectCount"]) + 1
        aggregate["triangles"] = int(aggregate["triangles"]) + triangles
        aggregate["objects"].append(obj.name)
        if (
            integrity["degenerateFaces"]
            or integrity["nonManifoldInteriorEdges"]
            or integrity["wireEdges"]
            or integrity["isolatedVertices"]
        ):
            failures.append(f"{obj.name} mesh integrity")
        if len(obj.data.uv_layers) != 0:
            failures.append(f"{obj.name} unexpectedly has UVs")
        if any(modifier.type == "ARMATURE" for modifier in obj.modifiers):
            failures.append(f"{obj.name} unexpectedly has production binding")

    intra_object_report: dict[str, list[dict[str, object]]] = {}
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
        intra_object_report[object_name] = records

    inter_garment_report: dict[str, dict[str, object]] = {}
    ordered_names = sorted(snapshots)
    for first_index, first_name in enumerate(ordered_names):
        for second_name in ordered_names[first_index + 1 :]:
            first_snapshot = snapshots[first_name]
            second_snapshot = snapshots[second_name]
            overlap_pairs = [
                (int(first_triangle), int(second_triangle))
                for first_triangle, second_triangle
                in first_snapshot.tree.overlap(second_snapshot.tree)
            ]
            overlap_count = len(overlap_pairs)
            label = f"{first_name}::{second_name}"
            key = pair_key(first_name, second_name)
            exception = DESIGNED_LAYER_EXCEPTIONS.get(key)
            record = {
                "surfaceTriangleOverlapPairs": overlap_count,
                "designedLayerException": exception is not None,
            }
            if overlap_count:
                record["overlapLocationDebug"] = object_overlap_location_summary(
                    by_name[first_name],
                    by_name[second_name],
                )
            if exception is None:
                record["requiredMaximumTriangleOverlapPairs"] = 0
                if overlap_count > 0:
                    failures.append(
                        f"unexpected inter-object intersection {label}: "
                        f"{overlap_count} triangle pairs"
                    )
            else:
                maximum, reason = exception
                mutually_exclusive_states = key in MUTUALLY_EXCLUSIVE_WEAPON_STATE_EXCEPTION_PAIRS
                localization = localized_overlap_audit(
                    first_snapshot.vertices,
                    first_snapshot.triangles,
                    second_snapshot.vertices,
                    second_snapshot.triangles,
                    overlap_pairs,
                    DESIGNED_LAYER_EXCEPTION_ZONES[key],
                )
                record.update({
                    "exceptionMaximumTriangleOverlapPairs": maximum,
                    "exceptionReason": reason,
                    "spatialLocalization": localization,
                    "mutuallyExclusiveWeaponStates": mutually_exclusive_states,
                    "visibleTogetherInDefaultReview": (
                        first_name in VISIBLE_DEFAULT_REVIEW_OBJECTS
                        and second_name in VISIBLE_DEFAULT_REVIEW_OBJECTS
                    ),
                    "manualVisualReviewStillRequired": (
                        overlap_count > 0 and not mutually_exclusive_states
                    ),
                })
                if overlap_count > maximum:
                    failures.append(
                        f"designed-layer intersection {label}: "
                        f"{overlap_count} > {maximum} triangle pairs"
                    )
                if localization["status"] != "PASS":
                    failures.append(
                        f"designed-layer overlap escaped deterministic tuck/anchor zones: "
                        f"{label}: {localization['outsideAllowedZoneOverlapPairs']} pairs"
                    )
                if overlap_count > 0 and not mutually_exclusive_states:
                    warnings.append(
                        f"designed-layer overlap requires manual review: "
                        f"{label}: {overlap_count} triangle pairs"
                    )
            inter_garment_report[label] = record

    for family, (minimum, maximum) in FAMILY_TARGETS.items():
        total = int(family_reports.get(family, {}).get("triangles", 0))
        if not minimum <= total <= maximum:
            failures.append(f"{family} triangles {total} outside [{minimum},{maximum}]")
    total_triangles = sum(len(snapshot.triangles) for snapshot in snapshots.values())
    if not 4_500 <= total_triangles <= 16_000:
        failures.append(f"kit triangles {total_triangles} outside [4500,16000]")
    if int(family_reports["base_garments"]["triangles"]) > 4_200:
        failures.append("base garment hard cap exceeded 4200 triangles")
    if int(family_reports["weapon_quiver"]["triangles"]) > 2_400:
        failures.append("weapon/quiver hard cap exceeded 2400 triangles")
    forbidden = [obj.name for obj in kit if any(token in obj.name for token in ("Cylinder", "Cube", "Sphere", "Panel", "Scale", "Rivet"))]
    if forbidden:
        failures.append("forbidden primitive/panel naming: " + ", ".join(forbidden))
    if not fit_top.hide_render or not fit_trousers.hide_render:
        failures.append("accepted fit proxies became visible")
    if failures:
        print("ANCHOR_DEBUG", json.dumps({
            name: report["anchorZoneBodyFit"]
            for name, report in object_reports.items()
            if "anchorZoneBodyFit" in report
        }, sort_keys=True))
        print("BODY_OVERLAP_DEBUG", json.dumps({
            name: report["bodyOverlapLocationDebug"]
            for name, report in object_reports.items()
            if "bodyOverlapLocationDebug" in report
        }, sort_keys=True))
        print("FIT_LOW_DEBUG", json.dumps({
            name: {
                "minimumSignedClearanceMeters": report["bodyFit"]["minimumSignedClearanceMeters"],
                "lowestSignedClearanceSamples": report["bodyFit"]["lowestSignedClearanceSamples"],
            }
            for name, report in object_reports.items()
            if "bodyFit" in report
            and float(report["bodyFit"]["minimumSignedClearanceMeters"]) < -0.001
        }, sort_keys=True))
        print("SELF_CROSS_DEBUG", json.dumps({
            name: debug
            for name, snapshot in sorted(snapshots.items())
            if (debug := self_crossing_location_debug(snapshot))
        }, sort_keys=True))
        print("LOCALIZATION_DEBUG", json.dumps({
            "intraObject": {
                object_name: [
                    record
                    for record in records
                    if "spatialLocalization" in record
                    and record["spatialLocalization"]["status"] != "PASS"
                ]
                for object_name, records in intra_object_report.items()
                if any(
                    "spatialLocalization" in record
                    and record["spatialLocalization"]["status"] != "PASS"
                    for record in records
                )
            },
            "interObject": {
                label: record
                for label, record in inter_garment_report.items()
                if "spatialLocalization" in record
                and record["spatialLocalization"]["status"] != "PASS"
            },
        }, sort_keys=True))
        print("MESH_INTEGRITY_DEBUG", json.dumps({
            name: {
                "integrity": report["integrity"],
                "degenerateFaceDetails": [
                    {
                        "polygonIndex": polygon.index,
                        "area": float(polygon.area),
                        "vertices": [
                            [round(float(value), 6) for value in by_name[name].data.vertices[index].co]
                            for index in polygon.vertices
                        ],
                    }
                    for polygon in by_name[name].data.polygons
                    if polygon.area <= 1e-10
                ][:8],
            }
            for name, report in object_reports.items()
            if report["integrity"]["degenerateFaces"]
            or report["integrity"]["nonManifoldInteriorEdges"]
        }, sort_keys=True))
        print("PAIR_CONTACT_DEBUG", json.dumps({
            label: record["overlapLocationDebug"]
            for label, record in inter_garment_report.items()
            if "overlapLocationDebug" in record
        }, sort_keys=True))
        print("BLOCKED_BY_STRUCTURAL_R2_PREFLIGHT")
        print(json.dumps({"failures": failures, "warnings": warnings, "families": family_reports, "objects": object_reports, "interGarment": inter_garment_report}, indent=2))
        raise RuntimeError("; ".join(failures))

    if args.preflight_only:
        print("PASS_STRUCTURAL_R2_PREFLIGHT_NO_WRITE")
        print(json.dumps({
            "kitTriangles": total_triangles,
            "families": family_reports,
            "bodyFit": {
                name: report["bodyFit"]
                for name, report in object_reports.items()
                if "bodyFit" in report
            },
            "warnings": warnings,
        }, indent=2))
        return

    # Stage every artifact beside the final write-once directory.  Only a
    # completely rendered, saved, hashed, and review-pinned checkpoint is moved
    # into R2, so a render/save failure cannot strand a partial frozen iteration.
    output_root.parent.mkdir(parents=True, exist_ok=True)
    with tempfile.TemporaryDirectory(
        prefix=f".{MODEL_NAME}_staging_",
        dir=output_root.parent,
    ) as staging_name:
        stage_root = Path(staging_name)
        stage_evidence_root = stage_root / "Evidence"
        stage_evidence_root.mkdir(parents=False, exist_ok=False)
        stage_blend_path = stage_root / final_blend_path.name
        stage_report_path = stage_root / final_report_path.name
        stage_review_path = stage_root / final_review_path.name

        camera = configure_scene(body, eyes, kit)
        staged_evidence_paths = render_evidence(camera, stage_evidence_root)
        bpy.ops.wm.save_as_mainfile(filepath=str(stage_blend_path), compress=True, check_existing=False)
        evidence_hashes = {
            base.relative(final_evidence_root / path.name): sha256(path)
            for path in staged_evidence_paths
        }

        report = {
            "schemaVersion": 1,
            "assetId": ASSET_ID,
            "status": "READY_FOR_MANUAL_MACRO_REVIEW",
            "scope": "isolated source-only R2 family-colour Hunter macro on accepted masculine BodyV3; no production promotion",
            "iterationLineage": {
                "rejectedPredecessor": base.relative(R1_REJECT),
                "rejectedPredecessorSha256": sha256(R1_REJECT),
                "visibleR1FacesReused": False,
                "retainedDesignDirectionOnly": ["1.305 m recurve silhouette", "tapered five-arrow back quiver"],
            },
            "verifiedInputs": verified_inputs,
            "bodyLineage": "Blender Foundation Human Base Meshes v1.4.1 stylized masculine via accepted SessionHumanoidBaseV3",
            "anatomicalRigLandmarksMeters": anatomy.as_json(),
            "hiddenFitProxy": {
                "blend": base.relative(base.FIT_BLEND),
                "objects": [base.FIT_TOP_NAME, base.FIT_TROUSERS_NAME],
                "visibleFacesReused": False,
                "rendered": False,
                "uses": [
                    "topology-free torso/limb branch selection",
                    "static fit calibration reference",
                ],
                "finalCageStations": "body-derived normal offsets; proxy faces and normals are not copied",
            },
            "script": base.relative(SCRIPT_PATH),
            "scriptSha256": sha256(SCRIPT_PATH),
            "frozenR1UtilityBuilder": base.relative(R1_BUILDER),
            "frozenR1UtilityBuilderSha256": sha256(R1_BUILDER),
            "blend": base.relative(final_blend_path),
            "blendSha256": sha256(stage_blend_path),
            "kitObjectCount": len(kit),
            "kitTriangles": total_triangles,
            "objects": object_reports,
            "families": family_reports,
            "intraObjectConnectedIslandAudit": intra_object_report,
            "interGarmentFit": inter_garment_report,
            "contracts": {
                "hoodCapClosedSoftCrown": True,
                "hoodCowlMantleSeparate": True,
                "brigandineRows": 4,
                "fullCircumferenceBrigandineBands": 0,
                "leftPauldronShells": 3,
                "rightPauldronShells": 2,
                "capeSections": 2,
                "bowLengthMeters": 1.305,
                "bowNonCircularSections": True,
                "bowCrossSectionStationsUnique": True,
                "quiverArrowCount": 5,
                "quiverCombinedSolidifyUsed": False,
                "bootsSampledFromAcceptedBodyFoot": True,
                "staticDrawAndNockedWeaponStatesSeparate": True,
            },
            "evidence": evidence_hashes,
            "failures": [],
            "warnings": warnings,
            "boundaries": {
                "manualArtAcceptance": False,
                "manualReviewRequired": True,
                "masculineBindPoseOnly": True,
                "garmentWeightsAuthored": False,
                "garmentDeformationTested": False,
                "uvsAuthored": False,
                "pbrMapsAuthored": False,
                "lodsAuthored": False,
                "fbxWritten": False,
                "unityIntegrationPerformed": False,
                "promotionAllowed": False,
            },
        }
        stage_report_path.write_text(
            json.dumps(report, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        review_request = {
            "schemaVersion": 1,
            "assetId": ASSET_ID,
            "status": "PENDING_MANUAL_MACRO_REVIEW",
            "report": base.relative(final_report_path),
            "reportSha256": sha256(stage_report_path),
            "evidence": list(report["evidence"]),
            "reviewQuestions": [
                "Does the hood read as soft cloth with a drooping asymmetric crown, a clean face aperture, and no helmet, bob-cap, or top-hole read?",
                "Does the narrow cowl hang vertically from the neck with a curved weighted hem rather than reading as a shoulder disc?",
                "Does the brigandine read as one tailored waist-shaped armor shell with three subtle partial lap seams, without barrel bands, body holes, or fused layers?",
                "Do the compact 3L/2R pauldrons read as nested compound-curved volumes rather than cards, with restrained shoulder width?",
                "Does the mantle read as a compact crowned cloth/fur mass with a curved hem rather than a sharp flat panel?",
                "Do both cape sections show a curved yoke, deep broad folds, increasing back sag and flare, a center gap, and unequal shaped hems rather than rigid sheets?",
                "Do sleeves, trousers, bracers, greaves, and body-derived boots maintain continuous coverage with no wrist/thigh gaps, tubes, boxes, or visible intersections?",
                "Do harness ends disappear cleanly under cowl and belt with no pointed or stray triangles?",
                "Does the smoothed 1.305 m recurve and tapered five-arrow quiver retain the accepted silhouette direction without faceting or cylindrical construction?",
                "Across front, side, back, both three-quarters, head, and shoulder evidence, does the kit read as one manually authored Hunter outfit rather than primitives, panels, or fused shells?",
            ],
            "automaticAcceptanceForbidden": True,
            "nextStepIfAccepted": "freeze this R2 Blend and evidence hashes, then request the separate body-variant bind and pose/deformation gate before any surfacing or Unity work",
            "nextStepIfRejected": "write an explicit R2 reject verdict and fork R3; never overwrite R2 or proceed to surfacing",
            "promotionAllowed": False,
        }
        stage_review_path.write_text(
            json.dumps(review_request, indent=2, sort_keys=True) + "\n",
            encoding="utf-8",
        )
        staged_files = {path.relative_to(stage_root) for path in stage_root.rglob("*") if path.is_file()}
        expected_staged_files = {
            Path(final_blend_path.name),
            Path(final_report_path.name),
            Path(final_review_path.name),
            *(Path("Evidence") / path.name for path in staged_evidence_paths),
        }
        base.require(staged_files == expected_staged_files, "R2 staging file contract changed")
        base.require(
            not output_root.exists(),
            "R2 output appeared during staging; fork R3 instead of replacing it",
        )
        stage_root.rename(output_root)

    print("READY_FOR_MANUAL_MACRO_REVIEW")
    print("KIT_OBJECTS", len(kit), "KIT_TRIANGLES", total_triangles, "EVIDENCE", len(evidence_hashes))


if __name__ == "__main__":
    main()
