"""Iteration 13. Water, ice and glass.

Two new photographic masters (ArtSource/Textures/Iteration13): open-water ripples and
micro-fractured crystal. They rebuild the water normal (sampled by LestingCraft/Water as
layer 8), the crystal/glass layer (12, used by the Glass and CrystalGlass shaders) and a
derived glacial ice that keeps the same conventions:
512px seamless tiles, albedo JPEG q95 4:4:4, lossless packed mask
(R metallic, G AO, B height, A smoothness), luminance-derived normals.

Run after refine_materials_v4/v5, build_materials_v6, build_living_materials and
refine_materials_v7. Then re-render icons:
  python Tools/build_voxel_items.py && python Tools/build_era_v9_assets.py
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image

R = Path(__file__).resolve().parents[1]
OUT = R / 'Assets/Art/PBR'
SRC = R / 'ArtSource/Textures/Iteration13'
N = 512
Y, X = np.mgrid[0:N, 0:N]


def master(name, zoom=1.0):
    im = Image.open(SRC / (name + '.jpg')).convert('RGB')
    w, h = im.size
    side = int(min(w, h) / zoom)
    x0, y0 = (w - side) // 2, (h - side) // 2
    im = im.crop((x0, y0, x0 + side, y0 + side)).resize((N, N), Image.Resampling.LANCZOS)
    return np.asarray(im, float) / 255


def gblur(a, sigma):
    """Frequency-domain blur: wraps, so tiles stay seamless."""
    if sigma <= 0:
        return a
    ky = np.fft.fftfreq(a.shape[0])[:, None]
    kx = np.fft.rfftfreq(a.shape[1])[None, :]
    g = np.exp(-2 * np.pi ** 2 * sigma ** 2 * (kx * kx + ky * ky))
    if a.ndim == 2:
        return np.fft.irfft2(np.fft.rfft2(a) * g, s=a.shape)
    out = np.empty_like(a)
    for c in range(a.shape[2]):
        out[:, :, c] = np.fft.irfft2(np.fft.rfft2(a[:, :, c]) * g, s=a.shape[:2])
    return out


def fnoise(seed, sigma):
    r = np.random.default_rng(seed)
    f = gblur(r.random((N, N)), sigma)
    f = (f - f.min()) / max(f.max() - f.min(), 1e-6)
    return gblur(f, max(sigma * .18, .6))


def tileable(a, band=26):
    a = a.copy()
    for axis in (0, 1):
        for k in range(band):
            t = .5 * (1 - k / band) ** 2
            if axis == 0:
                p, q = a[k].copy(), a[-1 - k].copy()
                a[k] = p * (1 - t) + q * t
                a[-1 - k] = q * (1 - t) + p * t
            else:
                p, q = a[:, k].copy(), a[:, -1 - k].copy()
                a[:, k] = p * (1 - t) + q * t
                a[:, -1 - k] = q * (1 - t) + p * t
        if axis == 0:
            m = (a[0] + a[-1]) * .5
            a[0] = m
            a[-1] = m
        else:
            m = (a[:, 0] + a[:, -1]) * .5
            a[:, 0] = m
            a[:, -1] = m
    return a


def contrast(a, amount, pivot=.5):
    return np.clip((a - pivot) * amount + pivot, 0, 1)


files = []


def emit(name, albedo, smooth, metal=0.0, strength=4.0, ao_depth=.9, normal_from=None):
    """Write albedo + normal + packed mask; normals may come from a different field than albedo."""
    a = np.clip(tileable(np.clip(albedo, 0, 1)), 0, 1)
    height_source = a if normal_from is None else np.clip(tileable(normal_from), 0, 1)
    lum = height_source @ np.array([.2126, .7152, .0722])
    h = np.clip(gblur(lum, .8) * .7 + gblur(lum, 3.4) * .3, 0, 1)
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    flat = np.maximum(1, np.sqrt(dx * dx + dy * dy) / 1.15)
    dx /= flat
    dy /= flat
    normal = np.stack([-dx, dy, np.ones_like(h)], -1)
    normal /= np.linalg.norm(normal, axis=-1)[..., None]
    metallic = np.full_like(h, metal) if np.isscalar(metal) else metal
    ao = np.clip(.95 - (gblur(h, 4) - h) * ao_depth, .58, 1)
    mask = np.stack([metallic, ao, h, np.clip(smooth + (h - h.mean()) * .14, .05, .98)], -1)
    Image.fromarray(np.uint8(np.round(a * 255))).save(OUT / (name + '_albedo.jpg'), quality=95, subsampling=0, optimize=True)
    Image.fromarray(np.uint8(np.clip(normal * .5 + .5, 0, 1) * 255)).save(OUT / (name + '_normal.png'), optimize=True)
    Image.fromarray(np.uint8(np.clip(mask, 0, 1) * 255)).save(OUT / (name + '_mask.png'), optimize=True)
    for suffix in ('.png', '.png.meta'):
        (OUT / (name + '_albedo' + suffix)).unlink(missing_ok=True)
    files.append(name)


# ------------------------------------------------------------------------ water
water_source = master('water', zoom=1.15)
# The shader tints and refracts, so albedo stays dark and neutral: the detail lives in the normals.
water_albedo = np.clip(water_source * .38 + np.array([.05, .22, .30]), 0, 1)
emit('water', water_albedo, .95, strength=9.0, ao_depth=.35, normal_from=water_source)

# ------------------------------------------------------------------- ice & glass
crystal_source = master('crystal', zoom=1.1)
crystal_albedo = np.clip(contrast(crystal_source, 1.05, pivot=.82) * .96, 0, 1)
emit('crystal', crystal_albedo, .93, strength=2.2, ao_depth=.45)

ice = np.clip(crystal_source * np.array([.86, .93, 1.0]) * 1.02, 0, 1)
cracks = np.clip((fnoise(5, 2.2) - .70) * 6, 0, 1)[..., None]
ice = np.clip(ice * (1 - cracks * .30), 0, 1)
# Frosted patches so ice does not read as plain white rock.
frost = np.clip((fnoise(6, 7.0) - .55) * 3.2, 0, 1)[..., None]
ice = np.clip(ice * (1 - frost * .18) + frost * .18 * np.array([.88, .94, 1.0]), 0, 1)
emit('glacial-ice', ice, .82, strength=2.6, ao_depth=.5)

info = json.loads((OUT / 'materials.json').read_text())
info['version'] = 8
info['iteration13'] = {
    'masters': sorted(str(p.relative_to(R)) for p in SRC.glob('*.jpg')),
    'derived': files, 'resolution': 512,
    'maps': 'AI-derived albedo; normal/metallic/AO/height/smoothness estimated from luminance, not scans',
    'notes': 'water normals come from the photographic ripple master; crystal feeds both Glass and CrystalGlass shaders',
}
(OUT / 'materials.json').write_text(json.dumps(info, ensure_ascii=False, indent=2))
print('iteration 13:', len(files), 'layers rewritten:', ', '.join(files))
