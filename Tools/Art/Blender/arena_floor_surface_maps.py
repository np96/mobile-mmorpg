#!/usr/bin/env python3
"""Build deterministic first-party arena-floor surface maps with Blender 5.2.

The 1254 px first-party slate painting remains the microdetail source. This script
derives a quiet, mobile-ready 1024 px albedo, tangent-space normal, and packed
surface mask without adding a second mortar pattern or third-party image libraries.
It also derives an explicit full-floor tangent normal from the checked-in macro
albedo and bakes the micro packed mask into the macro UV at the authored repeat and
phase. URP can therefore sample AO/smoothness through its base-map transform while
the independently tiled detail normal stays aligned with the same source features.
Run from the game-rpg directory:

    /Applications/Blender.app/Contents/MacOS/Blender \
        --background --factory-startup --python-exit-code 1 \
        --python Tools/Art/Blender/arena_floor_surface_maps.py
"""

from __future__ import annotations

import argparse
import hashlib
import json
import struct
import sys
import zlib
from pathlib import Path

import bpy
import numpy as np


SCRIPT_VERSION = "3.1.0"
BLENDER_MINIMUM = (5, 2, 0)
OUTPUT_SIZE = 1024
MICRO_SOURCE_RELATIVE_PATH = Path(
    "SourceArt/Blender/Environment/Textures/"
    "ArenaFloorSlateMicrodetailSource.png"
)
MACRO_ALBEDO_RELATIVE_PATH = Path(
    "Assets/SessionRpg/Art/Environment/Textures/"
    "ArenaFloorMacroAlbedo.png"
)
OUTPUT_DIRECTORY_RELATIVE_PATH = Path(
    "Assets/SessionRpg/Art/Environment/Textures"
)
ALBEDO_FILENAME = "ArenaFloorSurfaceAlbedo.png"
NORMAL_FILENAME = "ArenaFloorSurfaceNormal.png"
MASK_FILENAME = "ArenaFloorSurfaceMask.png"
MACRO_NORMAL_FILENAME = "ArenaFloorMacroNormal.png"
MACRO_MASK_FILENAME = "ArenaFloorMacroMask.png"
DEFAULT_MICRO_TILING = 4.6875
DEFAULT_MICRO_OFFSET = (0.47125, 0.64125)


def parse_args() -> argparse.Namespace:
    script_args: list[str] = []
    if "--" in sys.argv:
        script_args = sys.argv[sys.argv.index("--") + 1 :]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--project-root",
        type=Path,
        default=Path(__file__).resolve().parents[3],
    )
    return parser.parse_args(script_args)


def read_source_rgb(path: Path) -> tuple[np.ndarray, int, int]:
    if not path.is_file():
        raise RuntimeError(f"Arena-floor source texture is missing: {path}")
    image = bpy.data.images.load(str(path), check_existing=False)
    image.colorspace_settings.name = "Non-Color"
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    rgba = pixels.reshape((height, width, 4))[::-1].copy()
    bpy.data.images.remove(image)
    return np.clip(rgba[:, :, :3], 0.0, 1.0), width, height


def resize_bilinear_wrap(source: np.ndarray, size: int) -> np.ndarray:
    source_height, source_width, _ = source.shape
    x = ((np.arange(size, dtype=np.float32) + 0.5) * source_width / size) - 0.5
    y = ((np.arange(size, dtype=np.float32) + 0.5) * source_height / size) - 0.5
    x0 = np.floor(x).astype(np.int32) % source_width
    y0 = np.floor(y).astype(np.int32) % source_height
    x1 = (x0 + 1) % source_width
    y1 = (y0 + 1) % source_height
    x_weight = (x - np.floor(x))[None, :, None]
    y_weight = (y - np.floor(y))[:, None, None]
    upper = (
        source[y0[:, None], x0[None, :]] * (1.0 - x_weight) +
        source[y0[:, None], x1[None, :]] * x_weight
    )
    lower = (
        source[y1[:, None], x0[None, :]] * (1.0 - x_weight) +
        source[y1[:, None], x1[None, :]] * x_weight
    )
    return upper * (1.0 - y_weight) + lower * y_weight


def make_seamless(values: np.ndarray, blend_width: int = 96) -> np.ndarray:
    result = values.astype(np.float32, copy=True)
    source = result.copy()
    denominator = max(blend_width - 1, 1)
    for offset in range(blend_width):
        normalized = offset / denominator
        weight = (1.0 - normalized) * (1.0 - normalized)
        opposite = result.shape[1] - 1 - offset
        average = (source[:, offset] + source[:, opposite]) * 0.5
        result[:, offset] = (
            source[:, offset] * (1.0 - weight) + average * weight
        )
        result[:, opposite] = (
            source[:, opposite] * (1.0 - weight) + average * weight
        )
    source = result.copy()
    for offset in range(blend_width):
        normalized = offset / denominator
        weight = (1.0 - normalized) * (1.0 - normalized)
        opposite = result.shape[0] - 1 - offset
        average = (source[offset] + source[opposite]) * 0.5
        result[offset] = source[offset] * (1.0 - weight) + average * weight
        result[opposite] = (
            source[opposite] * (1.0 - weight) + average * weight
        )
    return result


def blur_wrap(values: np.ndarray, iterations: int = 1) -> np.ndarray:
    result = values.astype(np.float32, copy=True)
    weights = (1.0, 4.0, 6.0, 4.0, 1.0)
    offsets = (-2, -1, 0, 1, 2)
    for _ in range(iterations):
        horizontal = sum(
            weight * np.roll(result, offset, axis=1)
            for weight, offset in zip(weights, offsets, strict=True)
        ) / 16.0
        result = sum(
            weight * np.roll(horizontal, offset, axis=0)
            for weight, offset in zip(weights, offsets, strict=True)
        ) / 16.0
    return result


def luminance(values: np.ndarray) -> np.ndarray:
    return (
        (values[:, :, 0] * 0.2126) +
        (values[:, :, 1] * 0.7152) +
        (values[:, :, 2] * 0.0722)
    )


def height_to_tangent_normal(
    height: np.ndarray,
    strength: float,
) -> np.ndarray:
    """Convert top-to-bottom PNG height rows to an OpenGL/+Y tangent normal."""
    derivative_x = (
        np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)
    ) * 0.5
    derivative_y = (
        np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)
    ) * 0.5
    normal_x = -derivative_x * strength
    # Array rows increase down while texture V increases up, hence positive here.
    normal_y = derivative_y * strength
    normal_z = np.ones_like(height)
    normal_length = np.sqrt(
        (normal_x * normal_x) +
        (normal_y * normal_y) +
        (normal_z * normal_z)
    )
    return np.stack(
        (
            (normal_x / normal_length * 0.5) + 0.5,
            (normal_y / normal_length * 0.5) + 0.5,
            (normal_z / normal_length * 0.5) + 0.5,
        ),
        axis=2,
    )


def sample_tiled_uv(
    source: np.ndarray,
    size: int,
    tiling: float,
    offset: tuple[float, float],
) -> np.ndarray:
    """Bake Unity's ``fract(uv * tiling + offset)`` with wrapped bilinear taps."""
    source_height, source_width, _ = source.shape
    target_u = (np.arange(size, dtype=np.float32) + 0.5) / size
    # PNG arrays are top-to-bottom; UV V is bottom-to-top.
    target_v = 1.0 - ((np.arange(size, dtype=np.float32) + 0.5) / size)
    source_u = np.mod((target_u * tiling) + offset[0], 1.0)
    source_v = np.mod((target_v * tiling) + offset[1], 1.0)
    source_x = (source_u * source_width) - 0.5
    source_y = ((1.0 - source_v) * source_height) - 0.5
    x_floor = np.floor(source_x)
    y_floor = np.floor(source_y)
    x0 = x_floor.astype(np.int32) % source_width
    y0 = y_floor.astype(np.int32) % source_height
    x1 = (x0 + 1) % source_width
    y1 = (y0 + 1) % source_height
    x_weight = (source_x - x_floor)[None, :, None]
    y_weight = (source_y - y_floor)[:, None, None]
    upper = (
        source[y0[:, None], x0[None, :]] * (1.0 - x_weight) +
        source[y0[:, None], x1[None, :]] * x_weight
    )
    lower = (
        source[y1[:, None], x0[None, :]] * (1.0 - x_weight) +
        source[y1[:, None], x1[None, :]] * x_weight
    )
    return upper * (1.0 - y_weight) + lower * y_weight


def build_maps(source: np.ndarray) -> tuple[np.ndarray, np.ndarray, np.ndarray]:
    resized = make_seamless(resize_bilinear_wrap(source, OUTPUT_SIZE))
    source_luminance = luminance(resized)
    low, high = np.percentile(source_luminance, (2.0, 98.0))
    normalized = np.clip(
        (source_luminance - low) / max(high - low, 0.0001),
        0.0,
        1.0,
    )
    target_luminance = 0.20 + (np.power(normalized, 0.94) * 0.30)
    chroma = resized - source_luminance[:, :, None]
    albedo = np.clip(
        target_luminance[:, :, None] + (chroma * 0.72),
        0.075,
        0.56,
    )

    broad_surface = blur_wrap(source_luminance, iterations=8)
    surface_detail = source_luminance - broad_surface
    detail_low, detail_high = np.percentile(surface_detail, (4.0, 96.0))
    height = np.clip(
        (surface_detail - detail_low) /
        max(detail_high - detail_low, 0.0001),
        0.0,
        1.0,
    )
    height = blur_wrap(height, iterations=1)
    normal = height_to_tangent_normal(height, strength=3.4)

    neighborhood = blur_wrap(height, iterations=3)
    cavity = np.clip(neighborhood - height, 0.0, 1.0)
    ambient_occlusion = np.clip(
        0.76 + (height * 0.24) - (cavity * 0.18),
        0.68,
        1.0,
    )
    detail_amount = np.clip(
        np.abs(surface_detail) /
        max(float(np.percentile(np.abs(surface_detail), 95.0)), 0.0001),
        0.0,
        1.0,
    )
    roughness = np.clip(
        0.74 + ((1.0 - height) * 0.14) + (detail_amount * 0.035),
        0.70,
        0.93,
    )
    metallic = np.zeros_like(height)
    smoothness = 1.0 - roughness
    packed_mask = np.stack(
        (metallic, ambient_occlusion, height, smoothness),
        axis=2,
    )
    return albedo, normal, packed_mask


def build_macro_normal(macro_albedo: np.ndarray) -> tuple[np.ndarray, np.ndarray]:
    """Derive joint/trim relief without preserving broad painted illumination."""
    macro_luminance = luminance(macro_albedo)
    broad_luminance = blur_wrap(macro_luminance, iterations=12)
    local_relief = macro_luminance - broad_luminance
    relief_low, relief_high = np.percentile(local_relief, (1.5, 98.5))
    height = np.clip(
        (local_relief - relief_low) /
        max(relief_high - relief_low, 0.0001),
        0.0,
        1.0,
    )
    height = blur_wrap(height, iterations=1)
    return height_to_tangent_normal(height, strength=4.2), height


def build_macro_packed_mask(
    micro_packed_mask: np.ndarray,
    macro_albedo: np.ndarray,
    macro_height: np.ndarray,
    micro_tiling: float,
    micro_offset: tuple[float, float],
) -> np.ndarray:
    """Combine phase-aligned micro response with full-floor joint/cavity depth."""
    phase_mask = sample_tiled_uv(
        micro_packed_mask,
        OUTPUT_SIZE,
        micro_tiling,
        micro_offset,
    )
    macro_luminance = luminance(macro_albedo)
    broad_luminance = blur_wrap(macro_luminance, iterations=10)
    cavity_signal = np.clip(
        (broad_luminance - macro_luminance) * 9.0,
        0.0,
        1.0,
    )
    macro_cavity_ao = 0.96 - (np.power(cavity_signal, 0.65) * 0.44)
    combined_ao = np.clip(
        phase_mask[:, :, 1] * macro_cavity_ao,
        0.48,
        0.985,
    )

    source_smoothness = phase_mask[:, :, 3]
    smooth_low, smooth_high = np.percentile(source_smoothness, (2.0, 98.0))
    normalized_smoothness = np.clip(
        (source_smoothness - smooth_low) /
        max(smooth_high - smooth_low, 0.0001),
        0.0,
        1.0,
    )
    authored_smoothness = np.clip(
        0.14 + (normalized_smoothness * 0.16) + (macro_height * 0.02),
        0.14,
        0.32,
    )
    return np.stack(
        (
            np.zeros_like(macro_height),
            combined_ao,
            macro_height,
            authored_smoothness,
        ),
        axis=2,
    )


def png_chunk(chunk_type: bytes, data: bytes) -> bytes:
    return (
        struct.pack(">I", len(data)) +
        chunk_type +
        data +
        struct.pack(">I", zlib.crc32(chunk_type + data) & 0xFFFFFFFF)
    )


def write_png(path: Path, pixels: np.ndarray) -> None:
    if pixels.ndim != 3 or pixels.shape[2] not in (3, 4):
        raise ValueError("PNG pixels must be HxWx3 or HxWx4")
    height, width, channels = pixels.shape
    encoded = np.rint(np.clip(pixels, 0.0, 1.0) * 255.0).astype(np.uint8)
    scanlines = b"".join(b"\x00" + row.tobytes() for row in encoded)
    color_type = 2 if channels == 3 else 6
    payload = b"\x89PNG\r\n\x1a\n"
    payload += png_chunk(
        b"IHDR",
        struct.pack(">IIBBBBB", width, height, 8, color_type, 0, 0, 0),
    )
    payload += png_chunk(b"IDAT", zlib.compress(scanlines, level=9))
    payload += png_chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def channel_ranges(pixels: np.ndarray) -> dict[str, list[float]]:
    return {
        "r_metallic": [
            round(float(pixels[:, :, 0].min()), 4),
            round(float(pixels[:, :, 0].max()), 4),
        ],
        "g_ambient_occlusion": [
            round(float(pixels[:, :, 1].min()), 4),
            round(float(pixels[:, :, 1].max()), 4),
        ],
        "b_height_spare": [
            round(float(pixels[:, :, 2].min()), 4),
            round(float(pixels[:, :, 2].max()), 4),
        ],
        "a_smoothness": [
            round(float(pixels[:, :, 3].min()), 4),
            round(float(pixels[:, :, 3].max()), 4),
        ],
    }


def build_surface_maps(
    project_root: Path,
    *,
    micro_tiling: float = DEFAULT_MICRO_TILING,
    micro_offset: tuple[float, float] = DEFAULT_MICRO_OFFSET,
) -> dict[str, object]:
    micro_source_path = project_root / MICRO_SOURCE_RELATIVE_PATH
    macro_albedo_path = project_root / MACRO_ALBEDO_RELATIVE_PATH
    output_directory = project_root / OUTPUT_DIRECTORY_RELATIVE_PATH
    micro_source, source_width, source_height = read_source_rgb(
        micro_source_path
    )
    macro_albedo, macro_width, macro_height = read_source_rgb(
        macro_albedo_path
    )
    if macro_width != OUTPUT_SIZE or macro_height != OUTPUT_SIZE:
        raise RuntimeError(
            "Arena-floor macro albedo must be the checked-in 1024 px runtime "
            f"composition; found {macro_width}x{macro_height}"
        )
    albedo, normal, packed_mask = build_maps(micro_source)
    macro_normal, macro_height_field = build_macro_normal(macro_albedo)
    macro_packed_mask = build_macro_packed_mask(
        packed_mask,
        macro_albedo,
        macro_height_field,
        micro_tiling,
        micro_offset,
    )
    outputs = (
        ("albedo", output_directory / ALBEDO_FILENAME, albedo, "sRGB"),
        ("normal", output_directory / NORMAL_FILENAME, normal, "Non-Color"),
        (
            "packed_mask",
            output_directory / MASK_FILENAME,
            packed_mask,
            "Non-Color",
        ),
        (
            "macro_normal",
            output_directory / MACRO_NORMAL_FILENAME,
            macro_normal,
            "Non-Color",
        ),
        (
            "macro_packed_mask",
            output_directory / MACRO_MASK_FILENAME,
            macro_packed_mask,
            "Non-Color",
        ),
    )
    records: dict[str, object] = {}
    for key, path, pixels, color_space in outputs:
        write_png(path, pixels)
        records[key] = {
            "path": str(path.relative_to(project_root)),
            "width": OUTPUT_SIZE,
            "height": OUTPUT_SIZE,
            "color_space": color_space,
            "sha256": sha256(path),
            "bytes": path.stat().st_size,
        }
    evidence_directory = (
        project_root.parent / "outputs/art/blender/arena-floor"
    )
    tiling_proof_path = evidence_directory / "ArenaFloorSurfaceTilingProof.png"
    half_albedo = resize_bilinear_wrap(albedo, OUTPUT_SIZE // 2)
    tiling_proof = np.tile(half_albedo, (2, 2, 1))
    write_png(tiling_proof_path, tiling_proof)

    macro_pbr_proof_path = evidence_directory / "ArenaFloorMacroPbrProof.png"
    half_size = OUTPUT_SIZE // 2
    half_normal = resize_bilinear_wrap(macro_normal, half_size)
    half_height = resize_bilinear_wrap(
        macro_height_field[:, :, None].repeat(3, axis=2),
        half_size,
    )
    half_ao = resize_bilinear_wrap(
        macro_packed_mask[:, :, 1, None].repeat(3, axis=2),
        half_size,
    )
    half_smoothness = resize_bilinear_wrap(
        macro_packed_mask[:, :, 3, None].repeat(3, axis=2),
        half_size,
    )
    macro_pbr_proof = np.concatenate(
        (
            np.concatenate((half_normal, half_height), axis=1),
            np.concatenate((half_ao, half_smoothness), axis=1),
        ),
        axis=0,
    )
    write_png(macro_pbr_proof_path, macro_pbr_proof)
    return {
        "schema": 1,
        "generator": "Tools/Art/Blender/arena_floor_surface_maps.py",
        "generator_version": SCRIPT_VERSION,
        "source": {
            "path": str(MICRO_SOURCE_RELATIVE_PATH),
            "width": source_width,
            "height": source_height,
            "original_first_party": True,
            "sha256": sha256(micro_source_path),
        },
        "macro_albedo_input": {
            "path": str(MACRO_ALBEDO_RELATIVE_PATH),
            "width": macro_width,
            "height": macro_height,
            "original_first_party_derivative": True,
            "sha256": sha256(macro_albedo_path),
        },
        "android_max_size": 1024,
        "surface_synthesis": {
            "macro_pattern": "none_mesh_authored_joints_only",
            "source_usage": "first_party_seam_free_slate_microdetail",
            "tile_edge_crossfade_pixels": 96,
            "mortar_or_rune_pattern": False,
            "macro_normal_derivation": (
                "local_luminance_relief_12_pass_wrap_blur_"
                "percentile_1_5_to_98_5"
            ),
            "macro_normal_strength": 4.2,
            "macro_mask_derivation": (
                "phase_aligned_micro_response_times_macro_joint_cavity_ao"
            ),
        },
        "normal_convention": "tangent_space_opengl_positive_y",
        "packed_mask_channels": {
            "r": "metallic",
            "g": "ambient_occlusion",
            "b": "height_spare",
            "a": "smoothness_one_minus_roughness",
        },
        "packed_mask_channel_ranges": channel_ranges(packed_mask),
        "macro_packed_mask_channel_ranges": channel_ranges(
            macro_packed_mask
        ),
        "phase_baked_contract": {
            "source_output": MASK_FILENAME,
            "destination_output": MACRO_MASK_FILENAME,
            "micro_tiling": [micro_tiling, micro_tiling],
            "micro_offset": [micro_offset[0], micro_offset[1]],
            "phase_baked_uv_formula": (
                f"fract(uv * {micro_tiling:g} + "
                f"vec2({micro_offset[0]:g}, {micro_offset[1]:g}))"
            ),
            "sampling": "wrapped_bilinear_texel_center",
            "png_rows": "top_to_bottom_uv_v_inverted_during_bake",
            "ao_and_smoothness_are_direct_phase_baked_source_channels": False,
            "macro_cavity_ao": (
                "phase_micro_ao_times_dark_joint_signal_clamped_0_48_to_0_985"
            ),
            "macro_smoothness_remap": "phase_micro_roughness_to_0_14_0_32",
        },
        "tiling_proof": {
            "path": str(tiling_proof_path.relative_to(project_root.parent)),
            "layout": "two_by_two_repeat",
            "sha256": sha256(tiling_proof_path),
            "bytes": tiling_proof_path.stat().st_size,
        },
        "macro_pbr_proof": {
            "path": str(macro_pbr_proof_path.relative_to(project_root.parent)),
            "layout": "normal_height_ao_smoothness_quadrants",
            "sha256": sha256(macro_pbr_proof_path),
            "bytes": macro_pbr_proof_path.stat().st_size,
        },
        "outputs": records,
    }


def main() -> None:
    args = parse_args()
    if bpy.app.version < BLENDER_MINIMUM:
        raise RuntimeError(
            f"Blender 5.2 LTS or newer is required; found {bpy.app.version_string}"
        )
    manifest = build_surface_maps(args.project_root.resolve())
    print("SESSION_RPG_ARENA_FLOOR_SURFACE_MAPS_OK")
    print(json.dumps(manifest, indent=2))


if __name__ == "__main__":
    main()
