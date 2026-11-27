#!/usr/bin/env python3
"""Compare two screenshots with an exact RGBA default and explicit tolerance."""

from __future__ import annotations

import argparse
import hashlib
import json
import sys
from pathlib import Path
from typing import Any

try:
    import cv2
    import numpy as np
except ModuleNotFoundError as error:  # pragma: no cover - exercised by CLI health checks
    print(f"GF_SCREENSHOT_COMPARE_DEPENDENCY_MISSING: {error}", file=sys.stderr)
    raise


def _sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def _load_rgba(path: Path) -> np.ndarray:
    if not path.is_file():
        raise ValueError(f"Image not found: {path}")
    image = cv2.imread(str(path), cv2.IMREAD_UNCHANGED)
    if image is None:
        raise ValueError(f"Image could not be decoded: {path}")
    if image.ndim == 2:
        return cv2.cvtColor(image, cv2.COLOR_GRAY2RGBA)
    if image.ndim != 3:
        raise ValueError(f"Unsupported image shape: {image.shape}")
    if image.shape[2] == 3:
        return cv2.cvtColor(image, cv2.COLOR_BGR2RGBA)
    if image.shape[2] == 4:
        return cv2.cvtColor(image, cv2.COLOR_BGRA2RGBA)
    raise ValueError(f"Unsupported channel count: {image.shape[2]}")


def _validate_region(region: tuple[int, int, int, int] | None, width: int, height: int) -> dict[str, int]:
    if region is None:
        return {"x": 0, "y": 0, "width": width, "height": height}
    x, y, region_width, region_height = region
    if x < 0 or y < 0 or region_width <= 0 or region_height <= 0:
        raise ValueError("Region must have non-negative origin and positive dimensions")
    if x + region_width > width or y + region_height > height:
        raise ValueError("Region is outside the image dimensions")
    return {"x": x, "y": y, "width": region_width, "height": region_height}


def compare_images(
    reference_path: str | Path,
    candidate_path: str | Path,
    threshold: int = 0,
    region: tuple[int, int, int, int] | None = None,
) -> dict[str, Any]:
    if threshold < 0 or threshold > 255:
        raise ValueError("Threshold must be between 0 and 255")

    reference_file = Path(reference_path)
    candidate_file = Path(candidate_path)
    reference = _load_rgba(reference_file)
    candidate = _load_rgba(candidate_file)
    if reference.shape != candidate.shape:
        raise ValueError(
            f"Image dimensions/channels differ: {reference.shape} != {candidate.shape}"
        )

    height, width, _ = reference.shape
    selected_region = _validate_region(region, width, height)
    x = selected_region["x"]
    y = selected_region["y"]
    region_width = selected_region["width"]
    region_height = selected_region["height"]
    reference_slice = reference[y : y + region_height, x : x + region_width]
    candidate_slice = candidate[y : y + region_height, x : x + region_width]

    delta = np.abs(
        reference_slice.astype(np.int16) - candidate_slice.astype(np.int16)
    )
    per_pixel_delta = delta.max(axis=2)
    changed_pixels = int(np.count_nonzero(per_pixel_delta > threshold))
    compared_pixels = int(per_pixel_delta.size)
    max_channel_delta = int(delta.max()) if delta.size else 0
    mean_channel_delta = float(delta.mean()) if delta.size else 0.0

    return {
        "reference": reference_file.name,
        "candidate": candidate_file.name,
        "reference_sha256": _sha256(reference_file),
        "candidate_sha256": _sha256(candidate_file),
        "dimensions": {"width": width, "height": height, "channels": 4},
        "region": selected_region,
        "threshold": threshold,
        "compared_pixels": compared_pixels,
        "changed_pixels": changed_pixels,
        "max_channel_delta": max_channel_delta,
        "mean_channel_delta": mean_channel_delta,
        "passed": changed_pixels == 0,
    }


def write_report(path: str | Path, result: dict[str, Any]) -> None:
    report_path = Path(path)
    report_path.parent.mkdir(parents=True, exist_ok=True)
    report_path.write_text(
        json.dumps(result, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )


def _parse_args(argv: list[str]) -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("reference", type=Path)
    parser.add_argument("candidate", type=Path)
    parser.add_argument("--threshold", type=int, default=0)
    parser.add_argument(
        "--region",
        type=int,
        nargs=4,
        metavar=("X", "Y", "WIDTH", "HEIGHT"),
        help="Compare only an explicit in-bounds region",
    )
    parser.add_argument("--output", type=Path)
    return parser.parse_args(argv)


def main(argv: list[str] | None = None) -> int:
    try:
        args = _parse_args(argv or sys.argv[1:])
        result = compare_images(
            args.reference,
            args.candidate,
            threshold=args.threshold,
            region=tuple(args.region) if args.region else None,
        )
        if args.output:
            write_report(args.output, result)
        else:
            print(json.dumps(result, ensure_ascii=False, indent=2))
        return 0 if result["passed"] else 1
    except (OSError, ValueError) as error:
        print(f"GF_SCREENSHOT_COMPARE_INVALID: {error}", file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
