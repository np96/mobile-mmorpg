#!/usr/bin/env python3
"""Build the tracked transparent HUD medallion from its black-matte source.

The original image-generation result is preserved outside Unity under
``SourceArt/UI``.  This script reuses the deterministic dematting primitives used
by the ability-icon pipeline, then verifies the frame-specific empty center and
transparent perimeter.  Use ``--check`` in validation work to prove that the
tracked RGBA derivative is current.
"""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import statistics
import sys

from extract_ability_icon_alpha import (
    _encode_rgba_png,
    _read_rgb_png,
)


SOURCE_RELATIVE_PATH = Path("SourceArt/UI/AbilityMedallionFrame_Source.png")
DESTINATION_RELATIVE_PATH = Path(
    "Assets/SessionRpg/Art/UI/Hud/AbilityMedallionFrame.png"
)
EXPECTED_SOURCE_SIZE = 1254
MINIMUM_CLEAR_CENTER_RADIUS_RATIO = 0.30
MINIMUM_CLEAR_PERIMETER = 4
MATTE_NOISE_MAXIMUM = 1
OPAQUE_FOREGROUND_MINIMUM = 12


def _extract_black_matte_rgba(
    width: int,
    height: int,
    pixels: list[tuple[int, int, int]],
) -> tuple[bytes, dict[str, object]]:
    """Recover coverage without bleaching the intentionally dark metal.

    The general icon extractor solves every dim pixel as translucent bright art,
    which is correct for glowing spell paintings but turns blackened iron white.
    This source was deliberately generated over a uniform black matte instead.
    Pixels above a narrow 1..12 edge ramp therefore keep their authored RGB and
    become fully opaque; only the antialiased silhouette is dematted.
    """

    corner_size = 8
    corner_pixels: list[tuple[int, int, int]] = []
    for y in range(corner_size):
        for x in range(corner_size):
            corner_pixels.extend(
                (
                    pixels[(y * width) + x],
                    pixels[(y * width) + (width - 1 - x)],
                    pixels[((height - 1 - y) * width) + x],
                    pixels[((height - 1 - y) * width) + (width - 1 - x)],
                )
            )
    background = tuple(
        int(round(statistics.median(pixel[channel] for pixel in corner_pixels)))
        for channel in range(3)
    )
    if max(background) > MATTE_NOISE_MAXIMUM:
        raise ValueError(f"HUD medallion matte is not near black: {background}")

    rgba = bytearray(width * height * 4)
    transparent_count = 0
    soft_count = 0
    opaque_count = 0
    for index, pixel in enumerate(pixels):
        signal = max(
            max(0, pixel[channel] - background[channel])
            for channel in range(3)
        )
        if signal <= MATTE_NOISE_MAXIMUM:
            coverage = 0.0
        elif signal >= OPAQUE_FOREGROUND_MINIMUM:
            coverage = 1.0
        else:
            normalized = (signal - MATTE_NOISE_MAXIMUM) / float(
                OPAQUE_FOREGROUND_MINIMUM - MATTE_NOISE_MAXIMUM
            )
            coverage = normalized * normalized * (3.0 - (2.0 * normalized))

        x = index % width
        y = index // width
        if min(x, y, width - 1 - x, height - 1 - y) < MINIMUM_CLEAR_PERIMETER:
            coverage = 0.0

        alpha = max(0, min(255, int(round(coverage * 255.0))))
        destination = index * 4
        if alpha == 0:
            transparent_count += 1
            rgba[destination : destination + 4] = bytes((0, 0, 0, 0))
            continue

        if alpha == 255:
            foreground = pixel
            opaque_count += 1
        else:
            soft_count += 1
            foreground = tuple(
                max(
                    0,
                    min(
                        255,
                        int(
                            round(
                                (
                                    pixel[channel]
                                    - (background[channel] * (1.0 - coverage))
                                ) / coverage
                            )
                        ),
                    ),
                )
                for channel in range(3)
            )
        rgba[destination] = foreground[0]
        rgba[destination + 1] = foreground[1]
        rgba[destination + 2] = foreground[2]
        rgba[destination + 3] = alpha

    pixel_count = width * height
    return bytes(rgba), {
        "background": background,
        "transparent_percent": 100.0 * transparent_count / pixel_count,
        "soft_percent": 100.0 * soft_count / pixel_count,
        "opaque_percent": 100.0 * opaque_count / pixel_count,
    }


def _project_root() -> Path:
    return Path(__file__).resolve().parent.parent.parent


def _validate_frame_alpha(width: int, height: int, rgba: bytes) -> None:
    if width != height or width != EXPECTED_SOURCE_SIZE:
        raise ValueError(
            "HUD medallion source must remain "
            f"{EXPECTED_SOURCE_SIZE}x{EXPECTED_SOURCE_SIZE}; found {width}x{height}"
        )

    center_x = width // 2
    center_y = height // 2
    clear_radius = int(width * MINIMUM_CLEAR_CENTER_RADIUS_RATIO)
    for y in range(center_y - clear_radius, center_y + clear_radius + 1):
        for x in range(center_x - clear_radius, center_x + clear_radius + 1):
            offset_x = x - center_x
            offset_y = y - center_y
            if offset_x * offset_x + offset_y * offset_y > clear_radius * clear_radius:
                continue
            if rgba[((y * width) + x) * 4 + 3] != 0:
                raise ValueError("HUD medallion center opening is not fully transparent")

    for y in range(height):
        for x in range(width):
            if min(x, y, width - 1 - x, height - 1 - y) >= MINIMUM_CLEAR_PERIMETER:
                continue
            if rgba[((y * width) + x) * 4 + 3] != 0:
                raise ValueError("HUD medallion lacks its transparent outer gutter")

    alpha_values = rgba[3::4]
    if not any(value == 0 for value in alpha_values):
        raise ValueError("HUD medallion has no transparent pixels")
    if not any(0 < value < 255 for value in alpha_values):
        raise ValueError("HUD medallion has no antialiased soft-alpha edge")
    if not any(value == 255 for value in alpha_values):
        raise ValueError("HUD medallion has no fully opaque metal core")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="Verify the tracked derivative without writing it.",
    )
    parser.add_argument(
        "--project-root",
        type=Path,
        default=_project_root(),
        help="Unity project root.",
    )
    arguments = parser.parse_args()
    project_root = arguments.project_root.resolve()
    source = project_root / SOURCE_RELATIVE_PATH
    destination = project_root / DESTINATION_RELATIVE_PATH
    if not source.is_file():
        print(f"Missing HUD medallion source: {source}", file=sys.stderr)
        return 1

    width, height, pixels = _read_rgb_png(source)
    rgba, report = _extract_black_matte_rgba(width, height, pixels)
    _validate_frame_alpha(width, height, rgba)
    encoded = _encode_rgba_png(width, height, rgba)
    matches = destination.is_file() and destination.read_bytes() == encoded
    if arguments.check and not matches:
        print(
            f"Transparent HUD medallion is missing or stale: {destination}",
            file=sys.stderr,
        )
        return 1
    if not arguments.check and not matches:
        destination.parent.mkdir(parents=True, exist_ok=True)
        temporary = destination.with_name(destination.name + ".tmp")
        temporary.write_bytes(encoded)
        temporary.replace(destination)

    digest = hashlib.sha256(encoded).hexdigest()
    action = "ok" if matches else "wrote"
    print(
        f"{action} {destination.relative_to(project_root)} "
        f"sha256={digest} bg={report['background']} "
        f"transparent={report['transparent_percent']:.1f}% "
        f"soft={report['soft_percent']:.1f}% "
        f"opaque={report['opaque_percent']:.1f}%"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
