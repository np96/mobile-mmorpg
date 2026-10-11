#!/usr/bin/env python3
"""Create deterministic RGBA derivatives for the authored ability icons.

The opaque RGB originals remain untouched. Each `<Ability>.png` source produces a
same-folder `<Ability>_Transparent.png` derivative. The extraction assumes the
source was composited over its near-black corner matte. It estimates that matte
from four corner blocks, recovers alpha from the strongest normalized RGB channel,
and demattes the foreground color before applying a narrow corner-noise cleanup.
If an unusually dim source cannot produce even one readable core pixel, coverage
is normalized so its strongest foreground point becomes opaque while its soft
silhouette ratios remain intact.

This is deliberately a full-silhouette extraction: it does not rank pixels, cut
away dim authored strokes, infer a content rectangle, or mask the art into a
circle. The true outer two-pixel PNG perimeter is forced clear. A narrowly
guarded pass also removes an isolated inset export-frame line only when at least
two straight sides agree on the same inset; it never clears or fades the enclosed
art. Subtle glows and antialiased edges remain soft.

The implementation intentionally uses only Python's standard library and writes a
minimal, deterministic, non-interlaced RGBA PNG. Use `--check` in validation jobs
to prove every tracked derivative still matches the extraction rule.
"""

from __future__ import annotations

import argparse
import hashlib
import math
import os
from pathlib import Path
import statistics
import struct
import sys
import zlib


PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"
DERIVATIVE_SUFFIX = "_Transparent"
EXPECTED_ICON_COUNT = 108
CORNER_SAMPLE_SIZE = 8
MINIMUM_NOISE_FLOOR = 3
MAXIMUM_NOISE_FLOOR = 12
NOISE_TRANSITION_WIDTH = 14
MINIMUM_READABLE_CORE_ALPHA = 230.0 / 255.0
PERIMETER_ZERO_WIDTH = 2
FRAME_MINIMUM_INSET = 4
FRAME_MAXIMUM_INSET_DIVISOR = 8
FRAME_STRONG_ALPHA_OFFSET = 0.5
FRAME_MINIMUM_LINE_RATIO = 0.80
FRAME_MINIMUM_FRINGE_RATIO = 0.65
FRAME_MAXIMUM_NEIGHBOR_RATIO = 0.40
FRAME_INSET_TOLERANCE = 1


def _paeth(left: int, above: int, upper_left: int) -> int:
    estimate = left + above - upper_left
    left_distance = abs(estimate - left)
    above_distance = abs(estimate - above)
    upper_left_distance = abs(estimate - upper_left)
    if left_distance <= above_distance and left_distance <= upper_left_distance:
        return left
    if above_distance <= upper_left_distance:
        return above
    return upper_left


def _read_rgb_png(path: Path) -> tuple[int, int, list[tuple[int, int, int]]]:
    encoded = path.read_bytes()
    if not encoded.startswith(PNG_SIGNATURE):
        raise ValueError(f"{path} is not a PNG file")

    position = len(PNG_SIGNATURE)
    width = height = bit_depth = color_type = interlace = None
    compressed = bytearray()
    while position < len(encoded):
        if position + 12 > len(encoded):
            raise ValueError(f"{path} has a truncated PNG chunk")
        length = struct.unpack(">I", encoded[position : position + 4])[0]
        chunk_type = encoded[position + 4 : position + 8]
        chunk_data = encoded[position + 8 : position + 8 + length]
        expected_crc = struct.unpack(
            ">I", encoded[position + 8 + length : position + 12 + length]
        )[0]
        actual_crc = zlib.crc32(chunk_type)
        actual_crc = zlib.crc32(chunk_data, actual_crc) & 0xFFFFFFFF
        if expected_crc != actual_crc:
            raise ValueError(f"{path} has an invalid {chunk_type!r} CRC")

        if chunk_type == b"IHDR":
            (
                width,
                height,
                bit_depth,
                color_type,
                compression_method,
                filter_method,
                interlace,
            ) = struct.unpack(">IIBBBBB", chunk_data)
            if compression_method != 0 or filter_method != 0:
                raise ValueError(f"{path} uses an unsupported PNG encoding")
        elif chunk_type == b"IDAT":
            compressed.extend(chunk_data)
        elif chunk_type == b"IEND":
            break
        position += 12 + length

    if width is None or height is None:
        raise ValueError(f"{path} has no IHDR chunk")
    if bit_depth != 8 or color_type != 2 or interlace != 0:
        raise ValueError(
            f"{path} must remain an opaque 8-bit RGB non-interlaced source PNG"
        )

    bytes_per_pixel = 3
    row_size = width * bytes_per_pixel
    filtered = zlib.decompress(bytes(compressed))
    expected_size = height * (row_size + 1)
    if len(filtered) != expected_size:
        raise ValueError(
            f"{path} decoded to {len(filtered)} bytes; expected {expected_size}"
        )

    rows: list[bytearray] = []
    offset = 0
    previous = bytearray(row_size)
    for _ in range(height):
        filter_type = filtered[offset]
        source = filtered[offset + 1 : offset + 1 + row_size]
        offset += row_size + 1
        reconstructed = bytearray(row_size)
        for index, encoded_value in enumerate(source):
            left = reconstructed[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            above = previous[index]
            upper_left = previous[index - bytes_per_pixel] if index >= bytes_per_pixel else 0
            if filter_type == 0:
                predictor = 0
            elif filter_type == 1:
                predictor = left
            elif filter_type == 2:
                predictor = above
            elif filter_type == 3:
                predictor = (left + above) // 2
            elif filter_type == 4:
                predictor = _paeth(left, above, upper_left)
            else:
                raise ValueError(f"{path} uses unsupported PNG filter {filter_type}")
            reconstructed[index] = (encoded_value + predictor) & 0xFF
        rows.append(reconstructed)
        previous = reconstructed

    pixels: list[tuple[int, int, int]] = []
    for row in rows:
        pixels.extend(
            (row[index], row[index + 1], row[index + 2])
            for index in range(0, row_size, bytes_per_pixel)
        )
    return width, height, pixels


def _chunk(chunk_type: bytes, data: bytes) -> bytes:
    checksum = zlib.crc32(chunk_type)
    checksum = zlib.crc32(data, checksum) & 0xFFFFFFFF
    return struct.pack(">I", len(data)) + chunk_type + data + struct.pack(">I", checksum)


def _encode_rgba_png(width: int, height: int, rgba: bytes) -> bytes:
    row_size = width * 4
    scanlines = bytearray()
    for row in range(height):
        scanlines.append(0)
        start = row * row_size
        scanlines.extend(rgba[start : start + row_size])
    header = struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0)
    return b"".join(
        (
            PNG_SIGNATURE,
            _chunk(b"IHDR", header),
            _chunk(b"IDAT", zlib.compress(bytes(scanlines), level=9)),
            _chunk(b"IEND", b""),
        )
    )


def _corner_indices(width: int, height: int) -> list[int]:
    indices: list[int] = []
    for y in range(CORNER_SAMPLE_SIZE):
        for x in range(CORNER_SAMPLE_SIZE):
            indices.extend(
                (
                    y * width + x,
                    y * width + (width - 1 - x),
                    (height - 1 - y) * width + x,
                    (height - 1 - y) * width + (width - 1 - x),
                )
            )
    return indices


def _infer_encoded_frame_line_indices(
    width: int,
    height: int,
    base_alphas: list[float],
    noise_floor: float,
) -> tuple[set[int], tuple[str, ...]]:
    """Find a paired, isolated one-pixel inset frame without clipping artwork.

    Some immutable RGB sources contain an exported UI frame around otherwise
    valid spell art. A frame side is a nearly continuous straight line whose
    immediate inner and outer neighbors are mostly clear. At least two sides
    must agree on a plausible inset before any pixels are removed. This targets
    the encoded line plus only its sub-threshold antialias fringe; it never
    thresholds or fades the interior.
    """

    maximum_inset = min(width, height) // FRAME_MAXIMUM_INSET_DIVISOR
    if maximum_inset < FRAME_MINIMUM_INSET:
        return set(), ()

    strong_threshold = noise_floor + (
        NOISE_TRANSITION_WIDTH * FRAME_STRONG_ALPHA_OFFSET / 255.0
    )
    weak = [alpha > noise_floor for alpha in base_alphas]
    strong = [alpha > strong_threshold for alpha in base_alphas]
    row_counts = [
        sum(strong[y * width : (y + 1) * width])
        for y in range(height)
    ]
    column_counts = [
        sum(strong[y * width + x] for y in range(height))
        for x in range(width)
    ]
    weak_row_counts = [
        sum(weak[y * width : (y + 1) * width])
        for y in range(height)
    ]
    weak_column_counts = [
        sum(weak[y * width + x] for y in range(height))
        for x in range(width)
    ]

    # side, encoded position, distance from its PNG edge, active count
    candidates: list[tuple[str, int, int, int]] = []

    def add_best_vertical(
        side: str,
        positions: range,
        outward_step: int,
    ) -> None:
        minimum_count = int(math.ceil(height * FRAME_MINIMUM_LINE_RATIO))
        maximum_neighbor_count = int(
            math.floor(height * FRAME_MAXIMUM_NEIGHBOR_RATIO)
        )
        viable = []
        for x in positions:
            if column_counts[x] < minimum_count:
                continue
            if column_counts[x + outward_step] > maximum_neighbor_count:
                continue
            if column_counts[x - outward_step] > maximum_neighbor_count:
                continue
            inset = x if side == "L" else width - 1 - x
            viable.append((column_counts[x], x, inset))
        if viable:
            count, position, inset = max(viable)
            candidates.append((side, position, inset, count))

    def add_best_horizontal(
        side: str,
        positions: range,
        outward_step: int,
    ) -> None:
        minimum_count = int(math.ceil(width * FRAME_MINIMUM_LINE_RATIO))
        maximum_neighbor_count = int(
            math.floor(width * FRAME_MAXIMUM_NEIGHBOR_RATIO)
        )
        viable = []
        for y in positions:
            if row_counts[y] < minimum_count:
                continue
            if row_counts[y + outward_step] > maximum_neighbor_count:
                continue
            if row_counts[y - outward_step] > maximum_neighbor_count:
                continue
            inset = y if side == "T" else height - 1 - y
            viable.append((row_counts[y], y, inset))
        if viable:
            count, position, inset = max(viable)
            candidates.append((side, position, inset, count))

    add_best_vertical(
        "L",
        range(FRAME_MINIMUM_INSET, maximum_inset + 1),
        -1,
    )
    add_best_vertical(
        "R",
        range(width - 1 - maximum_inset, width - FRAME_MINIMUM_INSET),
        1,
    )
    add_best_horizontal(
        "T",
        range(FRAME_MINIMUM_INSET, maximum_inset + 1),
        -1,
    )
    add_best_horizontal(
        "B",
        range(height - 1 - maximum_inset, height - FRAME_MINIMUM_INSET),
        1,
    )

    if len(candidates) < 2:
        return set(), ()

    def matching(candidate: tuple[str, int, int, int]) -> list[tuple[str, int, int, int]]:
        return [
            other
            for other in candidates
            if abs(other[2] - candidate[2]) <= FRAME_INSET_TOLERANCE
        ]

    selected = max(
        (matching(candidate) for candidate in candidates),
        key=lambda group: (len(group), sum(candidate[3] for candidate in group)),
    )
    if len(selected) < 2:
        return set(), ()

    span_inset = int(round(statistics.median(candidate[2] for candidate in selected)))
    indices: set[int] = set()
    descriptions: list[str] = []
    for side, position, _, _ in selected:
        descriptions.append(side + str(position))
        if side in ("L", "R"):
            minimum_fringe_count = int(
                math.ceil(height * FRAME_MINIMUM_FRINGE_RATIO)
            )
            frame_columns = [position]
            frame_columns.extend(
                neighbor_x
                for neighbor_x in (position - 1, position + 1)
                if weak_column_counts[neighbor_x] >= minimum_fringe_count
            )
            for y in range(span_inset, height - span_inset):
                indices.update(
                    y * width + frame_x
                    for frame_x in frame_columns
                )
        else:
            minimum_fringe_count = int(
                math.ceil(width * FRAME_MINIMUM_FRINGE_RATIO)
            )
            frame_rows = [position]
            frame_rows.extend(
                neighbor_y
                for neighbor_y in (position - 1, position + 1)
                if weak_row_counts[neighbor_y] >= minimum_fringe_count
            )
            for x in range(span_inset, width - span_inset):
                indices.update(
                    frame_y * width + x
                    for frame_y in frame_rows
                )
    return indices, tuple(descriptions)


def _extract_rgba(
    width: int,
    height: int,
    pixels: list[tuple[int, int, int]],
) -> tuple[bytes, dict[str, object]]:
    corner_indices = _corner_indices(width, height)
    background = tuple(
        int(round(statistics.median(pixels[index][channel] for index in corner_indices)))
        for channel in range(3)
    )

    def matte_alpha(pixel: tuple[int, int, int]) -> float:
        # For C = alpha * F + (1 - alpha) * B and a near-black matte B,
        # max((C - B) / (255 - B)) is the minimum alpha that can reproduce C
        # without forcing a foreground channel above 255. This preserves colored
        # strokes and dim antialiasing equally instead of selecting by luminance.
        return max(
            max(
                0.0,
                (pixel[channel] - background[channel])
                / max(1.0, 255.0 - background[channel]),
            )
            for channel in range(3)
        )

    base_alphas = [matte_alpha(pixel) for pixel in pixels]
    corner_alphas = sorted(base_alphas[index] for index in corner_indices)
    percentile_index = int(math.ceil(0.99 * (len(corner_alphas) - 1)))
    noise_floor = max(
        MINIMUM_NOISE_FLOOR / 255.0,
        min(
            MAXIMUM_NOISE_FLOOR / 255.0,
            corner_alphas[percentile_index] + (2.0 / 255.0),
        ),
    )
    maximum_base_alpha = max(base_alphas)
    if maximum_base_alpha <= noise_floor:
        raise ValueError("icon contains no foreground above its corner matte")
    coverage_alpha_scale = (
        1.0 / maximum_base_alpha
        if maximum_base_alpha < MINIMUM_READABLE_CORE_ALPHA
        else 1.0
    )
    noise_transition = NOISE_TRANSITION_WIDTH / 255.0
    frame_line_indices, frame_lines = _infer_encoded_frame_line_indices(
        width,
        height,
        base_alphas,
        noise_floor,
    )

    rgba = bytearray(width * height * 4)
    transparent_count = 0
    soft_count = 0
    low_soft_count = 0
    mid_soft_count = 0
    readable_core_count = 0
    maximum_recomposition_error = 0
    perimeter_alpha_maximum = 0
    for index, pixel in enumerate(pixels):
        base_alpha = base_alphas[index]
        if base_alpha <= noise_floor:
            alpha_byte = 0
            foreground = (0, 0, 0)
        else:
            # Recover RGB with the physical/base alpha first. Cleanup only
            # changes coverage; using the cleaned alpha for dematting would
            # divide edge colors by an artificially tiny value and create the
            # clipped white/yellow fringe seen in the previous derivatives.
            foreground_channels: list[int] = []
            for channel in range(3):
                unmatted = (
                    pixel[channel]
                    - background[channel] * (1.0 - base_alpha)
                ) / base_alpha
                foreground_channels.append(max(0, min(255, int(round(unmatted)))))
            foreground = tuple(foreground_channels)

            transition = min(
                1.0,
                max(0.0, (base_alpha - noise_floor) / noise_transition),
            )
            cleanup = transition * transition * (3.0 - 2.0 * transition)
            cleaned_alpha = min(
                1.0,
                base_alpha * cleanup * coverage_alpha_scale,
            )
            alpha_byte = max(0, min(255, int(round(cleaned_alpha * 255.0))))

        x = index % width
        y = index // width
        outer_perimeter_distance = min(x, y, width - 1 - x, height - 1 - y)
        if (
            outer_perimeter_distance < PERIMETER_ZERO_WIDTH
            or index in frame_line_indices
        ):
            alpha_byte = 0

        if alpha_byte == 0:
            foreground = (0, 0, 0)
            transparent_count += 1
        else:
            if alpha_byte < 255:
                soft_count += 1
            if alpha_byte < 64:
                low_soft_count += 1
            elif alpha_byte < 230:
                mid_soft_count += 1
            if alpha_byte >= 230:
                readable_core_count += 1

            if outer_perimeter_distance >= PERIMETER_ZERO_WIDTH:
                for channel in range(3):
                    recomposited = int(
                        round(
                            foreground[channel] * base_alpha
                            + background[channel] * (1.0 - base_alpha)
                        )
                    )
                    maximum_recomposition_error = max(
                        maximum_recomposition_error,
                        abs(recomposited - pixel[channel]),
                    )

        if outer_perimeter_distance < PERIMETER_ZERO_WIDTH:
            perimeter_alpha_maximum = max(perimeter_alpha_maximum, alpha_byte)

        destination = index * 4
        rgba[destination] = foreground[0]
        rgba[destination + 1] = foreground[1]
        rgba[destination + 2] = foreground[2]
        rgba[destination + 3] = alpha_byte

    corner_alpha_maximum = max(rgba[index * 4 + 3] for index in corner_indices)
    if corner_alpha_maximum != 0:
        raise ValueError("corner matte did not resolve to alpha zero")
    if perimeter_alpha_maximum != 0:
        raise ValueError("outer two-pixel perimeter did not resolve to alpha zero")
    if transparent_count == 0 or transparent_count == width * height:
        raise ValueError("derived alpha lacks both transparent and visible pixels")
    report: dict[str, object] = {
        "background": background,
        "noise_floor": int(round(noise_floor * 255.0)),
        "maximum_base_alpha": maximum_base_alpha,
        "coverage_alpha_scale": coverage_alpha_scale,
        "transparent_percent": 100.0 * transparent_count / (width * height),
        "soft_percent": 100.0 * soft_count / (width * height),
        "low_soft_percent": 100.0 * low_soft_count / (width * height),
        "mid_soft_percent": 100.0 * mid_soft_count / (width * height),
        "readable_core_percent": 100.0 * readable_core_count / (width * height),
        "corner_alpha_max": corner_alpha_maximum,
        "perimeter_alpha_max": perimeter_alpha_maximum,
        "frame_lines": frame_lines,
        "recompose_error": maximum_recomposition_error,
    }
    return bytes(rgba), report


def _atomic_write(path: Path, data: bytes) -> None:
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_bytes(data)
    os.replace(temporary, path)


def _project_root() -> Path:
    # script.py -> Art -> Tools -> Unity project root
    return Path(__file__).resolve().parent.parent.parent


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--check",
        action="store_true",
        help="Verify tracked derivatives without writing files.",
    )
    parser.add_argument(
        "--project-root",
        type=Path,
        default=_project_root(),
        help="Unity project root; defaults to the repository containing this script.",
    )
    arguments = parser.parse_args()
    project_root = arguments.project_root.resolve()
    session_rpg_assets = project_root / "Assets" / "SessionRpg"
    if not session_rpg_assets.is_dir():
        print(
            f"Expected a Unity project root containing {session_rpg_assets}; "
            "do not pass the Assets folder as --project-root.",
            file=sys.stderr,
        )
        return 1
    icon_folder = session_rpg_assets / "Art" / "UI" / "AbilityIcons"
    sources = sorted(
        path
        for path in icon_folder.glob("*.png")
        if not path.stem.endswith(DERIVATIVE_SUFFIX)
    )
    if len(sources) != EXPECTED_ICON_COUNT:
        print(
            f"Expected {EXPECTED_ICON_COUNT} opaque source icons; found {len(sources)}",
            file=sys.stderr,
        )
        return 1

    mismatches: list[Path] = []
    for source in sources:
        width, height, pixels = _read_rgb_png(source)
        if width != 256 or height != 256:
            raise ValueError(f"{source} must remain 256x256; found {width}x{height}")
        rgba, report = _extract_rgba(width, height, pixels)
        encoded = _encode_rgba_png(width, height, rgba)
        destination = source.with_name(source.stem + DERIVATIVE_SUFFIX + ".png")
        matches = destination.is_file() and destination.read_bytes() == encoded
        if arguments.check:
            if not matches:
                mismatches.append(destination)
        elif not matches:
            _atomic_write(destination, encoded)

        digest = hashlib.sha256(encoded).hexdigest()[:12]
        action = "ok" if matches else "mismatch" if arguments.check else "wrote"
        print(
            f"{source.stem:24} {action:8} rgba sha256={digest} "
            f"bg={report['background']} noise={report['noise_floor']} "
            f"maxA={report['maximum_base_alpha']:.3f} "
            f"alphaScale={report['coverage_alpha_scale']:.3f} "
            f"transparent={report['transparent_percent']:.1f}% "
            f"soft={report['soft_percent']:.1f}% "
            f"low={report['low_soft_percent']:.1f}% "
            f"mid={report['mid_soft_percent']:.1f}% "
            f"core={report['readable_core_percent']:.1f}% "
            f"cornerA={report['corner_alpha_max']} "
            f"edgeA={report['perimeter_alpha_max']} "
            f"frame={','.join(report['frame_lines']) or '-'} "
            f"error<={report['recompose_error']}"
        )

    if mismatches:
        print(
            "Transparent ability-icon derivatives are missing or stale:\n  "
            + "\n  ".join(str(path) for path in mismatches),
            file=sys.stderr,
        )
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
