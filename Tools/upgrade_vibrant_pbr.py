"""Upgrade LestingCraft PBR albedo maps to a vibrant, sunlit, colorful palette while preserving
photographic micro-detail, exact dimensions, and seamless periodic tiling."""
from pathlib import Path
from PIL import Image, ImageEnhance
import numpy as np
import colorsys

ROOT = Path(__file__).resolve().parents[1]
PBR = ROOT / "Assets" / "Art" / "PBR"


def load_rgb(path: Path) -> np.ndarray:
    return np.asarray(Image.open(path).convert("RGB"), dtype=np.float32) / 255.0


def resolve_albedo(name: str) -> Path:
    jpg = PBR / f"{name}_albedo.jpg"
    return jpg if jpg.exists() else (PBR / f"{name}_albedo.png")


def equalize_axis(a: np.ndarray, axis: int, band: int = 24) -> np.ndarray:
    n = a.shape[axis]
    out = a.copy()
    for i in range(min(band, n // 2)):
        w = (1.0 - i / float(band)) ** 2
        pair_mean = (np.take(out, i, axis=axis) + np.take(out, n - 1 - i, axis=axis)) * 0.5
        left = np.take(out, i, axis=axis) * (1.0 - w) + pair_mean * w
        right = np.take(out, n - 1 - i, axis=axis) * (1.0 - w) + pair_mean * w
        idx = [slice(None)] * a.ndim
        idx[axis] = i
        out[tuple(idx)] = left
        idx[axis] = n - 1 - i
        out[tuple(idx)] = right
    return out


def save_img(path: Path, arr: np.ndarray) -> None:
    seamless = equalize_axis(equalize_axis(np.clip(arr, 0.0, 1.0), axis=1, band=24), axis=0, band=24)
    clipped = np.uint8(np.round(seamless * 255.0))
    clipped[-1, :, :] = clipped[0, :, :]
    clipped[:, -1, :] = clipped[:, 0, :]
    im = Image.fromarray(clipped, mode="RGB")
    if path.suffix.lower() == ".png":
        im.save(path, optimize=True)
    else:
        im.save(path, quality=95, subsampling=0)


def recolor_duotone(
    arr: np.ndarray,
    shadow_rgb: tuple,
    mid_rgb: tuple,
    highlight_rgb: tuple,
    contrast: float = 1.22,
    chroma_keep: float = 0.14,
) -> np.ndarray:
    """Map photographic luminance + local micro-chroma onto a rich 3-stop sunlit gradient.
    Strictly pointwise -> preserves exact periodic tileability (seam_ratio unchanged)."""
    luma = 0.299 * arr[:, :, 0] + 0.587 * arr[:, :, 1] + 0.114 * arr[:, :, 2]
    mean_l = float(luma.mean())
    std_l = max(float(luma.std()), 0.045)
    # Normalized perceptual tone in [0, 1] centered around 0.5
    t = np.clip(0.5 + (luma - mean_l) / (std_l * 3.1) * contrast, 0.0, 1.0)

    sh = np.array(shadow_rgb, dtype=np.float32)
    md = np.array(mid_rgb, dtype=np.float32)
    hi = np.array(highlight_rgb, dtype=np.float32)

    low_mask = (t <= 0.5)[..., None]
    t_low = (t / 0.5)[..., None]
    t_high = ((t - 0.5) / 0.5)[..., None]
    mapped = np.where(low_mask, sh + (md - sh) * t_low, md + (hi - md) * t_high)

    # Preserve subtle natural chroma variation around local luma
    chroma_delta = arr - luma[..., None]
    result = mapped + chroma_delta * chroma_keep
    return np.clip(result, 0.0, 1.0)


def upgrade_mossy_stone(arr: np.ndarray) -> np.ndarray:
    """Crisp sunlit stone on raised cobbles with vibrant emerald moss in crevices and green-leaning regions."""
    luma = 0.299 * arr[:, :, 0] + 0.587 * arr[:, :, 1] + 0.114 * arr[:, :, 2]
    # Crevices + slightly warmer/greener pockets become lush moss
    greenness = (arr[:, :, 1] - 0.5 * (arr[:, :, 0] + arr[:, :, 2]))
    g_norm = (greenness - greenness.mean()) / max(float(greenness.std()), 0.015)
    l_norm = (luma - luma.mean()) / max(float(luma.std()), 0.04)
    moss_mask = np.clip(0.45 + 0.30 * g_norm - 0.28 * l_norm, 0.0, 1.0)[..., None]
    stone_col = recolor_duotone(arr, (0.36, 0.39, 0.43), (0.58, 0.60, 0.63), (0.77, 0.78, 0.80), contrast=1.18, chroma_keep=0.08)
    moss_col = recolor_duotone(arr, (0.14, 0.36, 0.12), (0.28, 0.58, 0.20), (0.48, 0.76, 0.28), contrast=1.20, chroma_keep=0.08)
    return np.clip(stone_col * (1.0 - moss_mask) + moss_col * moss_mask, 0.0, 1.0)


SPECS = {
    # Lush sunlit spring-summer emerald meadow grass (replaces brown-ochre straw!)
    "grass": dict(sh=(0.14, 0.42, 0.11), md=(0.28, 0.64, 0.19), hi=(0.50, 0.82, 0.29), contrast=1.28, keep=0.10),
    # Vibrant sunlit oak canopy leaves
    "leaves": dict(sh=(0.11, 0.36, 0.09), md=(0.25, 0.59, 0.16), hi=(0.47, 0.79, 0.26), contrast=1.32, keep=0.12),
    # Radiant sakura cherry blossoms
    "cherry-blossom": dict(sh=(0.74, 0.34, 0.54), md=(0.94, 0.58, 0.76), hi=(1.00, 0.84, 0.92), contrast=1.20, keep=0.18),
    # Warm sunlit chestnut soil
    "dirt": dict(sh=(0.27, 0.17, 0.10), md=(0.48, 0.33, 0.20), hi=(0.66, 0.48, 0.32), contrast=1.20, keep=0.15),
    "grass-side": dict(sh=(0.27, 0.17, 0.10), md=(0.48, 0.33, 0.20), hi=(0.66, 0.48, 0.32), contrast=1.20, keep=0.15),
    # Warm sunny golden beach sand & desert sandstone
    "sand": dict(sh=(0.67, 0.56, 0.35), md=(0.86, 0.77, 0.53), hi=(0.97, 0.91, 0.72), contrast=1.18, keep=0.12),
    "sandstone": dict(sh=(0.60, 0.47, 0.28), md=(0.81, 0.69, 0.45), hi=(0.95, 0.85, 0.64), contrast=1.24, keep=0.12),
    # Clean crisp sunlit alpine stone & masonry (clear contrast vs green grass & warm wood!)
    "stone": dict(sh=(0.38, 0.40, 0.44), md=(0.58, 0.60, 0.64), hi=(0.77, 0.78, 0.81), contrast=1.22, keep=0.07),
    "cobblestone": dict(sh=(0.33, 0.35, 0.39), md=(0.55, 0.57, 0.61), hi=(0.75, 0.77, 0.80), contrast=1.28, keep=0.08),
    "stone-bricks": dict(sh=(0.36, 0.38, 0.42), md=(0.59, 0.61, 0.65), hi=(0.78, 0.80, 0.83), contrast=1.26, keep=0.08),
    "cracked-bricks": dict(sh=(0.32, 0.34, 0.38), md=(0.53, 0.55, 0.59), hi=(0.73, 0.75, 0.78), contrast=1.26, keep=0.08),
    "polished-stone": dict(sh=(0.44, 0.46, 0.50), md=(0.64, 0.66, 0.70), hi=(0.82, 0.84, 0.87), contrast=1.20, keep=0.07),
    "gravel": dict(sh=(0.33, 0.34, 0.37), md=(0.52, 0.53, 0.56), hi=(0.71, 0.72, 0.74), contrast=1.25, keep=0.10),
    "granite": dict(sh=(0.38, 0.30, 0.28), md=(0.58, 0.48, 0.45), hi=(0.76, 0.67, 0.63), contrast=1.22, keep=0.10),
    # Warm ivory limestone & bright sunlit marble
    "limestone": dict(sh=(0.58, 0.54, 0.45), md=(0.79, 0.75, 0.65), hi=(0.93, 0.90, 0.82), contrast=1.18, keep=0.10),
    "marble": dict(sh=(0.68, 0.68, 0.67), md=(0.86, 0.86, 0.85), hi=(0.97, 0.97, 0.96), contrast=1.16, keep=0.06),
    "polished-marble": dict(sh=(0.70, 0.70, 0.69), md=(0.88, 0.88, 0.87), hi=(0.98, 0.98, 0.97), contrast=1.14, keep=0.06),
    # Cozy warm honey-oak wood & rich chestnut bark
    "planks": dict(sh=(0.46, 0.30, 0.15), md=(0.72, 0.52, 0.29), hi=(0.88, 0.70, 0.45), contrast=1.24, keep=0.14),
    "wood-end": dict(sh=(0.38, 0.24, 0.12), md=(0.64, 0.45, 0.25), hi=(0.82, 0.64, 0.40), contrast=1.26, keep=0.14),
    "bark": dict(sh=(0.22, 0.14, 0.09), md=(0.42, 0.29, 0.18), hi=(0.60, 0.44, 0.29), contrast=1.26, keep=0.12),
    "workbench-top": dict(sh=(0.40, 0.24, 0.11), md=(0.68, 0.46, 0.23), hi=(0.86, 0.66, 0.39), contrast=1.26, keep=0.22),
    "workbench-side": dict(sh=(0.40, 0.24, 0.11), md=(0.68, 0.46, 0.24), hi=(0.86, 0.66, 0.40), contrast=1.24, keep=0.22),
    # Cheerful Mediterranean terracotta shingles & warm clay bricks
    "terracotta-shingles": dict(sh=(0.52, 0.20, 0.12), md=(0.80, 0.37, 0.23), hi=(0.95, 0.56, 0.37), contrast=1.24, keep=0.12),
    "clay-bricks": dict(sh=(0.46, 0.22, 0.15), md=(0.73, 0.39, 0.27), hi=(0.89, 0.57, 0.42), contrast=1.24, keep=0.15),
    "clay": dict(sh=(0.42, 0.34, 0.30), md=(0.63, 0.54, 0.48), hi=(0.79, 0.71, 0.65), contrast=1.18, keep=0.12),
    # Crisp snow, turquoise water & glowing aether
    "snow": dict(sh=(0.74, 0.80, 0.88), md=(0.89, 0.92, 0.96), hi=(0.98, 0.99, 1.00), contrast=1.12, keep=0.06),
    "water": dict(sh=(0.04, 0.38, 0.58), md=(0.12, 0.64, 0.80), hi=(0.36, 0.84, 0.94), contrast=1.22, keep=0.15),
    "aether": dict(sh=(0.12, 0.52, 0.64), md=(0.28, 0.80, 0.88), hi=(0.62, 0.96, 0.99), contrast=1.22, keep=0.15),
}


def main() -> None:
    for name, cfg in SPECS.items():
        path = resolve_albedo(name)
        arr = load_rgb(path)
        out = recolor_duotone(
            arr,
            cfg["sh"],
            cfg["md"],
            cfg["hi"],
            contrast=cfg["contrast"],
            chroma_keep=cfg["keep"],
        )
        save_img(path, out)
        m = out.mean(axis=(0, 1))
        h, s, v = colorsys.rgb_to_hsv(*m)
        print(f"Upgraded {name:22s} -> RGB=({m[0]:.3f},{m[1]:.3f},{m[2]:.3f}) H={h*360:5.1f}° S={s:.2f} V={v:.2f}")

    moss_path = resolve_albedo("mossy-stone")
    moss_out = upgrade_mossy_stone(load_rgb(moss_path))
    save_img(moss_path, moss_out)
    m = moss_out.mean(axis=(0, 1))
    print(f"Upgraded {'mossy-stone':22s} -> RGB=({m[0]:.3f},{m[1]:.3f},{m[2]:.3f})")

    # Also brighten and enrich fallback 2D menu backdrops for EditMode / non-3D fallback
    ui_dir = ROOT / "Assets" / "Resources" / "UI"
    for bg_name in ("MenuBackgroundEra.jpg", "MenuBackground.jpg"):
        bg_path = ui_dir / bg_name
        if bg_path.exists():
            im = Image.open(bg_path).convert("RGB")
            im = ImageEnhance.Brightness(im).enhance(1.18)
            im = ImageEnhance.Color(im).enhance(1.38)
            im = ImageEnhance.Contrast(im).enhance(1.08)
            im.save(bg_path, quality=93, subsampling=0, optimize=True)
            print(f"Upgraded UI fallback {bg_name} ({im.size[0]}x{im.size[1]})")


if __name__ == "__main__":
    main()
