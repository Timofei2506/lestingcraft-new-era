#!/usr/bin/env python3
"""Reject review frames that are duplicates, blank or flat.

The visual review once reported PASS while frame 08 was a byte-identical copy of
frame 07: the line-up was built after the game returned to the menu, where the
menu camera owns the screen. A green run is not a picture. This gate compares the
frames to each other and to their own pixel statistics, so a silently wrong
capture fails the build instead of shipping.
"""
import sys
from pathlib import Path

from PIL import Image
import numpy as np

MIN_STD = 12.0          # a flat frame (sky, fog, one wall) has almost no variance
MIN_UNIQUE_RATIO = 0.985  # near-duplicate pair detection ignores a few changed pixels


def signature(path):
    image = Image.open(path).convert("RGB").resize((160, 90), Image.Resampling.BILINEAR)
    return np.asarray(image, dtype=np.float32)


def main(directory):
    root = Path(directory)
    frames = sorted(p for p in root.glob("*.png") if p.name[:2].isdigit())
    if not frames:
        print("FAIL review frames: no numbered frames in", root)
        return 1

    problems = []
    stats = {}
    for path in frames:
        array = np.asarray(Image.open(path).convert("RGB"), dtype=np.float32)
        deviation = float(array.std())
        stats[path.name] = array
        if deviation < MIN_STD:
            problems.append(f"{path.name} is flat (std {deviation:.1f} < {MIN_STD})")

    names = list(stats)
    for i in range(len(names)):
        for j in range(i + 1, len(names)):
            a, b = stats[names[i]], stats[names[j]]
            if a.shape != b.shape:
                continue
            different = float((np.abs(a - b).max(axis=2) > 3).mean())
            if 1.0 - different >= MIN_UNIQUE_RATIO:
                problems.append(f"{names[i]} and {names[j]} are the same picture "
                                f"({different * 100:.2f}% of pixels differ)")

    if problems:
        for problem in problems:
            print("FAIL review frames:", problem)
        return 1
    print(f"PASS review frames: {len(frames)} distinct captures, none blank or duplicated. "
          "Pixel check only, not an art review.")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1] if len(sys.argv) > 1 else "Builds/StandaloneLinux64/VisualReview"))
