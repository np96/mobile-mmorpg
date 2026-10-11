#!/usr/bin/env python3
"""Pack the audited Poly Haven fortress surface for Unity URP/Lit.

The third-party files remain immutable source inputs. This script creates one
mobile-sized first-party derivative with Unity's metallic/AO/height/smoothness
channel layout and records the exact source and output hashes.

Run from the game-rpg directory:

    /Applications/Blender.app/Contents/MacOS/Blender \
        --background --factory-startup --python-exit-code 1 \
        --python Tools/Art/Blender/prepare_fortress_pbr_maps.py
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


SCRIPT_VERSION = "1.0.0"
BLENDER_MINIMUM = (5, 2, 0)
OUTPUT_SIZE = 1024
SOURCE_DIRECTORY = Path(
    "Assets/ThirdParty/PolyHaven/StoneBrickWall001"
)
OUTPUT_PATH = Path(
    "Assets/SessionRpg/Art/Environment/Textures/FortressMasonryMask.png"
)
MANIFEST_PATH = Path(
    "SourceArt/Blender/Environment/Textures/FortressMasonryPbr.manifest.json"
)


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


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def load_intake(project_root: Path) -> tuple[dict[str, object], dict[str, Path]]:
    manifest_path = project_root / SOURCE_DIRECTORY / "manifest.json"
    if not manifest_path.is_file():
        raise RuntimeError(f"Poly Haven intake manifest is missing: {manifest_path}")
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    if manifest.get("assetId") != "stone_brick_wall_001":
        raise RuntimeError("Unexpected Poly Haven fortress texture asset ID")

    paths: dict[str, Path] = {}
    for record in manifest.get("files", []):
        role = record.get("role")
        relative_path = record.get("path")
        expected_hash = record.get("sha256")
        if not isinstance(role, str) or not isinstance(relative_path, str):
            continue
        source_path = project_root / relative_path
        if not source_path.is_file():
            raise RuntimeError(f"Audited Poly Haven source is missing: {source_path}")
        actual_hash = sha256(source_path)
        if actual_hash != expected_hash:
            raise RuntimeError(
                f"Poly Haven {role} hash mismatch: expected {expected_hash}, "
                f"found {actual_hash}"
            )
        paths[role] = source_path

    for required_role in ("ambient_occlusion", "roughness", "displacement"):
        if required_role not in paths:
            raise RuntimeError(
                f"Poly Haven intake is missing required {required_role} source"
            )
    return manifest, paths


def read_linear_rgb(path: Path) -> np.ndarray:
    image = bpy.data.images.load(str(path), check_existing=False)
    image.colorspace_settings.name = "Non-Color"
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    rgba = pixels.reshape((height, width, 4))[::-1].copy()
    bpy.data.images.remove(image)
    return np.clip(rgba[:, :, :3], 0.0, 1.0)


def resize_bilinear(source: np.ndarray, size: int) -> np.ndarray:
    source_height, source_width, _ = source.shape
    x = ((np.arange(size, dtype=np.float32) + 0.5) * source_width / size) - 0.5
    y = ((np.arange(size, dtype=np.float32) + 0.5) * source_height / size) - 0.5
    x0 = np.clip(np.floor(x).astype(np.int32), 0, source_width - 1)
    y0 = np.clip(np.floor(y).astype(np.int32), 0, source_height - 1)
    x1 = np.clip(x0 + 1, 0, source_width - 1)
    y1 = np.clip(y0 + 1, 0, source_height - 1)
    x_weight = (x - np.floor(x))[None, :, None]
    y_weight = (y - np.floor(y))[:, None, None]
    upper = (
        source[y0[:, None], x0[None, :]] * (1.0 - x_weight)
        + source[y0[:, None], x1[None, :]] * x_weight
    )
    lower = (
        source[y1[:, None], x0[None, :]] * (1.0 - x_weight)
        + source[y1[:, None], x1[None, :]] * x_weight
    )
    return upper * (1.0 - y_weight) + lower * y_weight


def luminance(rgb: np.ndarray) -> np.ndarray:
    return (
        (rgb[:, :, 0] * 0.2126)
        + (rgb[:, :, 1] * 0.7152)
        + (rgb[:, :, 2] * 0.0722)
    )


def png_chunk(chunk_type: bytes, data: bytes) -> bytes:
    return (
        struct.pack(">I", len(data))
        + chunk_type
        + data
        + struct.pack(">I", zlib.crc32(chunk_type + data) & 0xFFFFFFFF)
    )


def write_png(path: Path, pixels: np.ndarray) -> None:
    height, width, channels = pixels.shape
    if channels != 4:
        raise ValueError("Packed URP mask must be RGBA")
    encoded = np.rint(np.clip(pixels, 0.0, 1.0) * 255.0).astype(np.uint8)
    scanlines = b"".join(b"\x00" + row.tobytes() for row in encoded)
    payload = b"\x89PNG\r\n\x1a\n"
    payload += png_chunk(
        b"IHDR",
        struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0),
    )
    payload += png_chunk(b"IDAT", zlib.compress(scanlines, level=9))
    payload += png_chunk(b"IEND", b"")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(payload)


def main() -> None:
    args = parse_args()
    if bpy.app.version < BLENDER_MINIMUM:
        raise RuntimeError(
            f"Blender 5.2 LTS or newer is required; found {bpy.app.version_string}"
        )

    project_root = args.project_root.resolve()
    intake, sources = load_intake(project_root)
    ao = luminance(resize_bilinear(
        read_linear_rgb(sources["ambient_occlusion"]), OUTPUT_SIZE
    ))
    roughness = luminance(resize_bilinear(
        read_linear_rgb(sources["roughness"]), OUTPUT_SIZE
    ))
    displacement = luminance(resize_bilinear(
        read_linear_rgb(sources["displacement"]), OUTPUT_SIZE
    ))

    # URP/Lit metallic workflow: R metallic, G ambient occlusion, B retained
    # source height, A smoothness. Stone remains dielectric while the independent
    # roughness scan supplies subtle wet/dry response rather than a flat scalar.
    packed = np.stack(
        (
            np.zeros_like(ao),
            np.clip(ao, 0.0, 1.0),
            np.clip(displacement, 0.0, 1.0),
            np.clip(1.0 - roughness, 0.02, 0.42),
        ),
        axis=2,
    )

    output_path = project_root / OUTPUT_PATH
    write_png(output_path, packed)
    manifest_path = project_root / MANIFEST_PATH
    manifest_path.parent.mkdir(parents=True, exist_ok=True)
    record = {
        "schemaVersion": 1,
        "generator": "Tools/Art/Blender/prepare_fortress_pbr_maps.py",
        "generatorVersion": SCRIPT_VERSION,
        "blenderVersion": bpy.app.version_string,
        "sourceIntakeId": intake["intakeId"],
        "sourceAssetId": intake["assetId"],
        "sourceManifest": str(SOURCE_DIRECTORY / "manifest.json"),
        "sourceHashes": {
            role: sha256(path) for role, path in sorted(sources.items())
        },
        "output": {
            "path": str(OUTPUT_PATH),
            "width": OUTPUT_SIZE,
            "height": OUTPUT_SIZE,
            "colorSpace": "linear non-color",
            "channels": {
                "R": "metallic=0",
                "G": "ambient_occlusion",
                "B": "displacement_reference_not_sampled_by_URP_Lit",
                "A": "smoothness=clamp(1-roughness,0.02,0.42)",
            },
            "bytes": output_path.stat().st_size,
            "sha256": sha256(output_path),
        },
    }
    manifest_path.write_text(
        json.dumps(record, indent=2, sort_keys=True) + "\n",
        encoding="utf-8",
    )
    print(json.dumps(record, indent=2, sort_keys=True))


if __name__ == "__main__":
    main()
