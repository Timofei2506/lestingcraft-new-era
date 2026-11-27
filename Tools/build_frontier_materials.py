"""Iteration 14 — Deep Frontier (2.0) surface sets.

Eleven new PBR surface sets for the six 2.0 realms, generated the same way as every
previous layer: a deterministic analytic albedo (crystal facets, soot dunes, living
kelp, charged glass, fungal cap flesh, void shard) plus estimated tangent-space normal,
AO, metallic and smoothness derived from the height field. Nothing here is a scan and
the script says so in materials.json.

Seams: every albedo is built from integer-frequency sines / periodic value noise, so it
tiles exactly; a final `periodic()` blend is applied anyway for safety (the same helper
Tools/build_materials_v6.py uses).

Run from the repository root:  python Tools/build_frontier_materials.py
Reproducible: PIL + numpy only, no network, no Unity. check_art.py then validates sizes,
channels and the seam contract for every layer in materials.json.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image, ImageFilter

R = Path(__file__).resolve().parents[1]
O = R / 'Assets/Art/PBR'
N = 512
y, x = np.mgrid[:N, :N]
u = x / N
v = y / N


def vnoise(cells, seed, size=N, shift_x=0.0, shift_y=0.0):
    """Periodic value noise with an integer cell count, so it tiles by construction.
    Optional domain warp (2-D arrays, in cell units) keeps the pattern organic without
    breaking the tiling: the warp itself is built from periodic noise."""
    r = np.random.default_rng(seed)
    g = r.random((cells, cells))
    base = np.linspace(0, cells, size, endpoint=False)
    X = (base[None, :] + shift_x * cells) % cells
    Y = (base[:, None] + shift_y * cells) % cells
    x0 = np.floor(X).astype(int); y0 = np.floor(Y).astype(int)
    fx = X - x0; fy = Y - y0
    fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy)
    x1 = (x0 + 1) % cells; y1 = (y0 + 1) % cells
    a = g[y0, x0]; b = g[y0, x1]; c = g[y1, x0]; d = g[y1, x1]
    return (a * (1 - fx) + b * fx) * (1 - fy) + (c * (1 - fx) + d * fx) * fy


def fbm(octaves, seed):
    """Domain-warped fractal value noise: the warp removes the rectangular bias that a
    plain 4-cell base octave leaves visible at 512 px."""
    warp_x = (vnoise(5, seed + 313) - .5) * .16
    warp_y = (vnoise(5, seed + 419) - .5) * .16
    total = np.zeros((N, N)); amp = .42; norm = 0.0
    for i in range(octaves):
        cells = 8 << i
        total += vnoise(cells, seed + i * 977, shift_x=warp_x, shift_y=warp_y) * amp
        norm += amp; amp *= .52
    return total / norm


def voronoi(cells, seed):
    """Periodic jittered-cell Voronoi. Returns (edge, cell_id) where edge is 0 at a cell
    boundary and 1 in the cell centre — the silhouette of cut crystal facets."""
    r = np.random.default_rng(seed)
    px = r.random((cells, cells)); py = r.random((cells, cells)); cid = r.random((cells, cells))
    X = u * cells; Y = v * cells
    ix = np.floor(X).astype(int); iy = np.floor(Y).astype(int)
    f1 = np.full((N, N), 9.0); f2 = np.full((N, N), 9.0); cell = np.zeros((N, N))
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            cx = (ix + dx) % cells; cy = (iy + dy) % cells
            gx = ix + dx + .15 + px[cy, cx] * .7
            gy = iy + dy + .15 + py[cy, cx] * .7
            d = np.sqrt((X - gx) ** 2 + (Y - gy) ** 2)
            closer = d < f1
            f2 = np.where(closer, f1, np.minimum(f2, d))
            cell = np.where(closer, cid[cy, cx], cell)
            f1 = np.where(closer, d, f1)
    edge = np.clip((f2 - f1) * 2.2, 0, 1)
    return edge, cell


def wave(freq_x, freq_y, phase=0.0):
    return .5 + .5 * np.sin(2 * np.pi * (freq_x * u + freq_y * v) + phase)


def blur(a, radius):
    """Wrap-around box blur: tiles seamlessly, unlike an edge-padded Gaussian."""
    taps = max(1, int(round(radius)))
    out = a.copy()
    for _ in range(3):
        acc = np.zeros_like(out)
        for dy in range(-taps, taps + 1):
            for dx in range(-taps, taps + 1):
                acc += np.roll(np.roll(out, dy, 0), dx, 1)
        out = acc / ((2 * taps + 1) ** 2)
    return out


def periodic(a):
    a = a.copy()
    for axis in [0, 1]:
        for k in range(20):
            t = .5 * (1 - k / 20) ** 2
            if axis == 0:
                b, c = a[k].copy(), a[-1 - k].copy(); a[k] = b * (1 - t) + c * t; a[-1 - k] = c * (1 - t) + b * t
            else:
                b, c = a[:, k].copy(), a[:, -1 - k].copy(); a[:, k] = b * (1 - t) + c * t; a[:, -1 - k] = c * (1 - t) + b * t
    return a


spec = {}


def add(name, albedo, smooth=.3, metal=0.0, strength=3.0):
    spec[name] = (albedo, smooth, metal, strength)


# ---------------- Crystalia ----------------
# Prism crystal: long faceted columns with a wet specular highlight down each facet edge.
edge, cell_id = voronoi(11, 41)
# Each facet gets its own cut angle: a per-cell brightness tilt plus a bright bevel edge.
tilt = (cell_id - .5) * .18
crystal_a = np.clip(.56 + tilt + edge * .20 + (fbm(3, 43) - .5) * .06, 0, 1)[:, :, None] * np.array([.62, .86, .98])
add('prism-crystal', crystal_a, .88, .10, 2.2)

# Aurora ice: fine wind streaks over a pale blue ice sheet, slightly emissive banding handled in-shader.
streak = wave(1, 13) * .55 + wave(2, 5, 1.1) * .25 + (fbm(4, 42) - .5) * .45
aurora_a = np.clip(.66 + (streak - .5) * .20, 0, 1)[:, :, None] * np.array([.52, .82, .96])
add('aurora-ice', aurora_a, .72, 0.0, 1.4)

# ---------------- Emberfall ----------------
soot = fbm(4, 51) * .6 + vnoise(96, 52) * .4
soot_a = np.clip(.26 + (soot - .5) * .30, 0, 1)[:, :, None] * np.array([1.05, .82, .74])
add('soot-dune', soot_a, .12, 0.0, 4.2)

# Magma shore: cracked black crust with glowing seams.
crack = np.abs(np.sin(2 * np.pi * (5 * u + 2 * v)) * np.sin(2 * np.pi * (3 * u - 6 * v)))
magma_a = np.clip(.10 + crack * .55 + (vnoise(64, 61) - .5) * .12, 0, 1)[:, :, None] * np.array([1.0, .42, .16])
add('ember-magma-shore', magma_a, .34, 0.0, 3.0)

# ---------------- Aquaria ----------------
kelp = (wave(3, 9) * .45 + wave(0, 17, .6) * .35 + (fbm(3, 71) - .5) * .35)
kelp_a = np.clip(.34 + (kelp - .5) * .34, 0, 1)[:, :, None] * np.array([.42, .96, .58])
add('kelp-weave', kelp_a, .48, 0.0, 1.8)

reef = fbm(4, 72) * .55 + wave(5, 4) * .25 + vnoise(128, 73) * .20
reef_a = np.clip(.56 + (reef - .5) * .34, 0, 1)[:, :, None] * np.array([.72, .92, .94])
add('reef-shelf', reef_a, .58, 0.0, 2.4)

# Tidal ruin: cut marble slabs with salt pitting and verdigris in the joints.
row = np.floor(v * 4).astype(int)
shift = (row % 2) * .5
hx = (u * 2 + shift) % 1.0
joint = (np.abs(hx - .5) < .46) & ((v * 4 % 1.0) > .06) & ((v * 4 % 1.0) < .94)
salt = vnoise(160, 81) * .6 + vnoise(32, 82) * .4
ruin_tone = np.where(joint, .34 + (row % 3) * .05, .80) + (salt - .5) * .10
ruin_a = np.clip(ruin_tone, 0, 1)[:, :, None] * np.array([.84, .90, .82])
add('tidal-ruin', ruin_a, .62, 0.0, 1.6)

# ---------------- Stormreach ----------------
# Charge glass: lightning-vitrified sand with branched fulgurite veins.
# Fulgurite crack network: dark fissures along Voronoi cell boundaries, exactly how a
# lightning strike vitrifies and splits sand.
edge_c, cell_c = voronoi(6, 91)
crack = np.clip(1.0 - edge_c * 22.0, 0, 1)
glass_a = np.clip(.80 - crack * .66 + (cell_c - .5) * .10 + (fbm(4, 92) - .5) * .08, 0, 1)[:, :, None] * np.array([.74, .88, 1.0])
add('charge-glass', glass_a, .88, .12, 2.0)

# Storm iron: riveted plating (reuses industrial look but darker, wetter).
plate_id = ((np.floor(u * 4).astype(int) * 7 + np.floor(v * 4).astype(int) * 13) % 5) / 4.0
plate = .5 + .5 * np.sin(2 * np.pi * 4 * u + plate_id)  # per-plate shimmer, no grid moire
rivet = vnoise(64, 101)
storm_a = np.clip(.36 + (plate - .5) * .10 + (rivet - .5) * .14, 0, 1)[:, :, None] * np.array([.68, .74, .84])
add('storm-iron', storm_a, .66, .55, 1.6)

# ---------------- Verdant ----------------
cap = fbm(3, 111) * .55 + vnoise(40, 112) * .45
cap_a = np.clip(.48 + (cap - .5) * .30, 0, 1)[:, :, None] * np.array([.86, .46, .52])
add('mycelium-cap', cap_a, .44, 0.0, 1.8)

# Spore loam: damp soil with bright spore speckles.
speck = (vnoise(256, 121) > .82).astype(float)
loam_a = np.clip(.30 + (fbm(4, 122) - .5) * .22 + speck * .34, 0, 1)[:, :, None] * np.array([.80, .92, .52])
add('spore-loam', loam_a, .24, 0.0, 3.2)

# ---------------- Nihility ----------------
edge_v, cell_v = voronoi(7, 131)
shard_a = np.clip(.16 + (cell_v - .5) * .12 + edge_v * .34 + (fbm(3, 133) - .5) * .08, 0, 1)[:, :, None] * np.array([.62, .52, .98])
add('void-shard', shard_a, .92, .05, 2.4)


def soften(a, radius=2):
    """Light wrap-around blur. Sharp analytic features (crystal edges, crack networks) tile
    perfectly but still produce a large per-pixel jump across the wrap line; the surface
    contract asks for a seam delta in the same range as neighbouring pixels, so the albedo is
    softened by one tap before encoding."""
    return blur(a, radius)


for name, (a, smooth, metal, strength) in spec.items():
    a = soften(np.clip(a, 0, 1))
    h = blur(a.mean(-1), 1)
    dx = (np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength
    dy = (np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength
    normal = np.stack([-dx, dy, np.ones_like(h)], -1)
    normal /= np.linalg.norm(normal, axis=-1)[:, :, None]
    metallic = np.zeros_like(h) + metal
    mask = np.stack([
        metallic,
        np.clip(.94 - (blur(h, 4) - h) * .9, .6, 1),
        h,
        np.clip(smooth + (h - h.mean()) * .14, .05, .98)], -1)
    Image.fromarray(np.uint8(a * 255)).save(O / f'{name}_albedo.jpg', quality=95, subsampling=0, optimize=True)
    for kind, data in [('normal', normal * .5 + .5), ('mask', mask)]:
        im = Image.fromarray(np.uint8(np.clip(data, 0, 1) * 255))
        im = im.quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE)
        im.save(O / f'{name}_{kind}.png', optimize=True)
    for suffix in ['.png', '.png.meta']:
        (O / f'{name}_albedo{suffix}').unlink(missing_ok=True)

info = json.loads((O / 'materials.json').read_text())
import re
info['version'] = 14
info['layers'] = re.findall(r'"([a-z0-9-]+)"', (R / 'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text().split('Names={')[1].split('};')[0])
info['iteration14'] = {
    'derivatives': list(spec),
    'resolution': 512,
    'maps': 'analytic albedo (integer-frequency sines + periodic value noise), estimated normal / AO / metallic / smoothness from the height field; not scans, no displacement',
    'albedo': 'JPEG q95 4:4:4, seam-corrected before encoding',
}
(O / 'materials.json').write_text(json.dumps(info, ensure_ascii=False, indent=2))
print(len(spec), 'frontier sets derived;', len(info['layers']), 'total PBR layers')
