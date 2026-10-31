#!/usr/bin/env python3
"""Make PBR albedo/normal tiles seamless and clamp normal slopes for check_art.py.

The photographic 1024 sets were dropped in with visible wrap seams and steep
normal relief (stored z < 180). This tool repairs them in place:
  * albedo/normal: cross-fade the border band against a half-shifted copy so the
    tile wraps without a hard seam (center detail untouched), then equalize the
    mirrored border rows/columns toward their pair mean so the seam line itself
    is continuous;
  * normal: renormalize and scale XY relief so every stored z stays >= 186
    (uniform convention: all channels map -1..1 via value/127.5-1).

Usage: python Tools/fix_tileable.py [layer ...]   (defaults to the known set)
"""
import sys
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
FOLDER = ROOT / "Assets" / "Art" / "PBR"
DEFAULT_LAYERS = ["stone", "dirt", "grass", "grass-side", "bark",
                  "wood-end", "leaves", "sand", "gravel"]


def blend_pass(a, axis, band):
    """Cross-fade one axis: border pixels blend toward the half-shifted copy."""
    n = a.shape[axis]
    shifted = np.roll(a, n // 2, axis=axis)
    w = np.ones(n, dtype=np.float32)
    ramp = 0.5 * (1 + np.cos(np.linspace(0, np.pi, band)))  # 1 at border -> 0 inside
    w[:band] = ramp
    w[-band:] = ramp[::-1]
    shape = [1] * a.ndim
    shape[axis] = n
    w = w.reshape(shape)
    return a * (1 - w) + shifted * w


def equalize_axis(a, axis, band):
    """Pull mirrored border pairs toward their mean so the wrap line is continuous."""
    n = a.shape[axis]
    out = a.copy()
    for i in range(min(band, n // 2)):
        w = (1.0 - i / band) ** 2
        pair_mean = (np.take(out, i, axis=axis) + np.take(out, n - 1 - i, axis=axis)) * 0.5
        left = np.take(out, i, axis=axis)
        right = np.take(out, n - 1 - i, axis=axis)
        merged_left = left * (1 - w) + pair_mean * w
        merged_right = right * (1 - w) + pair_mean * w
        index = [slice(None)] * a.ndim
        index[axis] = i
        out[tuple(index)] = merged_left
        index[axis] = n - 1 - i
        out[tuple(index)] = merged_right
    return out


def make_tileable(a, band_ratio=0.18, seam_band=24):
    h, w = a.shape[:2]
    a = blend_pass(a, axis=1, band=max(4, int(w * band_ratio)))
    a = blend_pass(a, axis=0, band=max(4, int(h * band_ratio)))
    a = equalize_axis(a, axis=1, band=seam_band)
    a = equalize_axis(a, axis=0, band=seam_band)
    return a


def fix_albedo(path):
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.float32)
    a = make_tileable(a)
    out = Image.fromarray(np.clip(a, 0, 255).astype(np.uint8), "RGB")
    out.save(path, quality=95, subsampling=0)
    return out


def fix_normal(path):
    im = Image.open(path).convert("RGB")
    a = np.asarray(im).astype(np.float32)
    a = make_tileable(a)
    # Uniform convention: all channels map -1..1 via value/127.5-1.
    v = a / 127.5 - 1.0
    x = v[:, :, 0]
    y = v[:, :, 1]
    length = np.sqrt(x * x + y * y + 1e-8)
    limit = float(np.sqrt(1.0 - 0.47 ** 2))  # max |xy| keeping z >= 0.47 (stored >= 187)
    scale = min(1.0, limit / max(1e-4, float(length.max())))
    x = x * scale
    y = y * scale
    z = np.sqrt(np.clip(1.0 - x * x - y * y, 0.0, 1.0))
    out = np.stack([(x + 1.0) * 127.5, (y + 1.0) * 127.5, (z + 1.0) * 127.5], axis=2)
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGB").save(path)
    return float(z.min() * 127.5 + 127.5)


def seam_diff(a):
    return float(np.abs(a[0].astype(float) - a[-1]).mean()), float(
        np.abs(a[:, 0].astype(float) - a[:, -1]).mean())


def main():
    layers = sys.argv[1:] or DEFAULT_LAYERS
    for layer in layers:
        albedo = FOLDER / f"{layer}_albedo.jpg"
        if not albedo.exists():
            albedo = albedo.with_suffix(".png")
        normal = FOLDER / f"{layer}_normal.png"
        if not normal.exists():
            normal = normal.with_suffix(".jpg")
        fixed = fix_albedo(albedo)
        dh, dw = seam_diff(np.asarray(fixed))
        zmin = fix_normal(normal)
        print(f"{layer:12s} seams h={dh:5.2f} w={dw:5.2f}  normal z_min={zmin:6.2f}")
    print("done:", len(layers), "sets repaired")


if __name__ == "__main__":
    main()
