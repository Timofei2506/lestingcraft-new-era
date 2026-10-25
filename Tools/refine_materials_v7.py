"""Iteration 12. Bare-hand surfaces the player sees in the first minutes are rebuilt
from nine new AI image masters (ArtSource/Textures/Iteration12): stone, cobblestone,
gravel, dirt, grass, bark, planks, sand, leaves. Masonry / ore derivatives stay
consistent with the new stone so adjacent blocks do not clash.

Conventions are unchanged: 512px seamless tiles, albedo JPEG q95 4:4:4,
lossless packed mask (R metallic, G AO, B height, A smoothness), normal maps
estimated from luminance. Run LAST: after refine_materials_v4/v5,
build_materials_v6 and build_living_materials.
"""
from pathlib import Path
import json
import numpy as np
from PIL import Image

R = Path(__file__).resolve().parents[1]
OUT = R / 'Assets/Art/PBR'
SRC = R / 'ArtSource/Textures/Iteration12'
N = 512
rng = np.random.default_rng(1212)
Y, X = np.mgrid[0:N, 0:N]


def master(name, zoom=1.0, roll=0):
    """Centre-crop a master to square, optional zoom-in, then resample to 512."""
    im = Image.open(SRC / (name + '.jpg')).convert('RGB')
    w, h = im.size
    side = int(min(w, h) / zoom)
    x0 = (w - side) // 2
    y0 = (h - side) // 2
    im = im.crop((x0, y0, x0 + side, y0 + side)).resize((N, N), Image.Resampling.LANCZOS)
    a = np.asarray(im, float) / 255
    return np.roll(a, roll, axis=(0, 1)) if roll else a


def gblur(a, sigma):
    """Gaussian blur in the frequency domain: wraps, so tiling stays exact."""
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
    """Periodic fractal noise field in [0,1]."""
    r = np.random.default_rng(seed)
    f = gblur(r.random((N, N)), sigma)
    f = (f - f.min()) / max(f.max() - f.min(), 1e-6)
    return gblur(f, max(sigma * .18, .6))


def tileable(a, band=26):
    """Cross-fade opposite borders, then pin them to the same pixels.

    Seams are removed where features are stochastic; the band is 5% of a tile,
    so it only softens the outermost rows of one block face.
    """
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


def saturate(a, amount):
    lum = a @ np.array([.2126, .7152, .0722])
    return np.clip(lum[..., None] + (a - lum[..., None]) * amount, 0, 1)


def contrast(a, amount, pivot=.5):
    return np.clip((a - pivot) * amount + pivot, 0, 1)


files = []
count = 0


def emit(name, albedo, smooth, metal=0.0, strength=4.0, ao_depth=.9):
    """Write albedo + normal + packed mask for one material layer."""
    global count
    a = np.clip(tileable(np.clip(albedo, 0, 1)), 0, 1)
    lum = a @ np.array([.2126, .7152, .0722])
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
    Image.fromarray(np.uint8(np.round(a * 255))).save(
        OUT / (name + '_albedo.jpg'), quality=95, subsampling=0, optimize=True)
    Image.fromarray(np.uint8(np.clip(normal * .5 + .5, 0, 1) * 255)).save(
        OUT / (name + '_normal.png'), optimize=True)
    Image.fromarray(np.uint8(np.clip(mask, 0, 1) * 255)).save(
        OUT / (name + '_mask.png'), optimize=True)
    for suffix in ('.png', '.png.meta'):
        (OUT / (name + '_albedo' + suffix)).unlink(missing_ok=True)
    files.append(name)
    count += 1


# ---------------------------------------------------------------- hero surfaces
stone = contrast(saturate(gblur(master('stone'), .35), 1.04), 1.16)
cobble = master('cobblestone', zoom=1.9)
gravel = master('gravel', zoom=2.6)
dirt = np.clip(saturate(master('dirt'), 1.05) * 1.16, 0, 1)
grass = contrast(saturate(master('grass'), .62), 1.04) * .95
bark = master('bark')
sand = contrast(saturate(master('sand'), .92), 1.05)
leaves = contrast(saturate(master('leaves'), .9), 1.1)


def n1d(seed, sigma):
    """Periodic 1D noise, used for irregular horizontal edges."""
    v = np.random.default_rng(seed).random(N)
    k = np.exp(-.5 * (np.arange(-int(3 * sigma), int(3 * sigma) + 1) / sigma) ** 2)
    k /= k.sum()
    out = np.convolve(np.concatenate([v, v, v]), k, 'same')[N:2 * N]
    return (out - out.min()) / max(out.max() - out.min(), 1e-6)


def grass_side():
    """Dirt wall with a ragged turf overhang and grass tongues hanging down it."""
    coarse, fine = n1d(7, 9), n1d(9, 1.6)
    edge = .33 + (coarse - .5) * .15 + (fine - .5) * .11 + (fnoise(19, 1.2)[0] - .5) * .05
    edge = edge[None, :] + (fnoise(21, 1.1) - .5) * .05
    mask = np.clip((edge - Y / N) * 42, 0, 1)[..., None]
    crowd = np.clip((fnoise(23, 1.8) - .55) * 3.2, 0, 1)
    tongues = gblur(crowd * np.clip((edge + .12 - Y / N) * 9, 0, 1), .8)[..., None]
    turf = grass * (.86 + fnoise(25, 1.0)[..., None] * .3)
    return np.clip((dirt * (1 - mask) + turf * mask) * (1 - tongues * .35) + turf * tongues * .35, 0, 1)


def planks():
    """Four grain-matched boards with dark seams, pegs and staggered joints."""
    board_h = N // 4
    strip = master('planks')
    wood = strip[int(N * .08):int(N * .92)]
    wood = np.asarray(Image.fromarray(np.uint8(wood * 255)).resize((N, board_h), Image.Resampling.LANCZOS), float) / 255
    out = np.zeros((N, N, 3))
    for i in range(4):
        shift = [0, 187, 94, 311][i]
        band = np.roll(wood, shift, axis=1) * (1 - .05 * (i % 2))
        out[i * board_h:(i + 1) * board_h] = contrast(band, 1.03 + .04 * (i % 2))
    # board seams and a lit lower edge catch the light
    for i in range(4):
        y = i * board_h
        out[y:y + 2] *= .34
        out[y + 2:y + 3] *= .70
        out[(y + board_h - 1):(y + board_h)] *= 1.12
        for x in ([18, 496] if i % 2 == 0 else [256]):
            out[y + 8:y + 11, x % N:(x % N) + 3] = np.array([.30, .22, .14])
    return np.clip(out, 0, 1)


def wood_end():
    """Cut log face: warped growth rings, fibre and radial drying checks.

    Distances are toroidal, so the pattern is periodic by construction.
    """
    def torus(c):
        return np.minimum(np.abs(X - c), N - np.abs(X - c))

    d, e = torus(N // 2), torus(N // 2)
    r = np.sqrt(d * d + e * e) / N
    warp = (fnoise(5, 26) - .5) * .055 + (fnoise(6, 8) - .5) * .018
    rings = np.sin((r + warp) * 2 * np.pi * 9.5) * .5 + .5
    late = np.clip((rings - .55) * 2.6, 0, 1)
    base = np.array([.66, .50, .32])
    dark = np.array([.40, .28, .16])
    heart = np.clip(1 - r * 6.0, 0, 1)[..., None] * .12
    a = base * (1 - late[..., None] * .34) + dark * late[..., None] * .34 - heart
    fibre = .90 + fnoise(9, 1.0) * .11 + (fnoise(10, 4.0) - .5) * .10
    a = a * fibre[..., None]
    theta = np.arctan2(e, d)
    checks = np.clip(1 - np.abs(np.sin(theta * 5 + (fnoise(14, 12) - .5) * 2.4)) / .035, 0, 1)
    checks *= np.clip(r * 1.6, 0, 1) * .55
    return np.clip(a * (1 - checks[..., None] * .55), 0, 1)


emit('stone', stone, .26, strength=6.5)
emit('cobblestone', cobble, .21, strength=7.5)
emit('gravel', gravel, .13, strength=7.0)
emit('dirt', dirt, .12, strength=4.5)
emit('grass', grass, .18, strength=4.0)
emit('grass-side', grass_side(), .16, strength=4.5)
emit('bark', bark, .19, strength=7.0)
emit('wood-end', wood_end(), .27, strength=4.5)
emit('leaves', leaves, .26, strength=5.0)
emit('sand', sand, .15, strength=2.4)
emit('planks', planks(), .28, strength=4.0)

# ------------------------------------------------------------- stone derivatives
def mineral_specks(seed, color, coverage=.30, metallic=0.0):
    """Clustered mineral inclusions: a coarse body plus a fine rim of crystals."""
    field = fnoise(seed, 13.0)
    blobs = np.clip((field - (1 - coverage)) * 7, 0, 1)
    blobs = gblur(blobs, 1.6)
    crystals = np.clip((fnoise(seed + 2, 2.2) - .55) * 4, 0, 1) * blobs
    grain = fnoise(seed + 1, 1.1)
    face = stone * (.94 + grain[..., None] * .16)
    body = np.array(color) * (.78 + grain[..., None] * .42)
    spec = np.clip(body * 1.42 + .10, 0, 1)
    ore = body * (1 - crystals[..., None]) + spec * crystals[..., None]
    a = face * (1 - blobs[..., None]) + ore * blobs[..., None]
    return np.clip(a, 0, 1), gblur(np.clip(blobs + crystals * .6, 0, 1), .8)


for name, seed, color, coverage, smooth, metal in [
    ('ore', 21, [.56, .34, .14], .30, .40, .40),
    ('ore-coal', 22, [.09, .09, .10], .34, .17, 0.0),
    ('ore-copper', 23, [.72, .33, .12], .32, .46, .62),
    ('ore-gold', 24, [.92, .72, .18], .30, .58, .78),
    ('ore-silver', 25, [.86, .88, .91], .28, .52, .74),
    ('ore-tin', 26, [.74, .73, .68], .30, .42, .40),
    ('ore-quartz', 27, [.97, .96, .93], .26, .55, 0.0),
    ('ore-sulfur', 28, [.86, .79, .16], .30, .22, 0.0),
]:
    a, blobs = mineral_specks(seed, color, coverage)
    emit(name, a, smooth, metal=blobs * metal, strength=6.0)

moss_patch = np.clip((fnoise(31, 13) - .42) * 2.6, 0, 1)
moss_fine = np.clip((fnoise(33, 1.4) - .42) * 2.2, 0, 1)
moss = np.clip(moss_patch * (.55 + moss_fine * .75), 0, 1)[..., None]
moss_col = gblur(leaves, .8) * np.array([.70, .88, .52]) * .58
emit('mossy-stone', np.clip(stone * (1 - moss * .85) + moss_col * moss * .95, 0, 1), .16, strength=6.0)


def bricks():
    w, h = 256, 128
    xx = (X + (Y // h % 2) * w // 2) % w
    yy = Y % h
    edge = np.minimum.reduce([xx, w - xx, yy, h - yy])
    bevel = np.clip(edge / 10, .22, 1.06)
    a = stone * bevel[..., None]
    a[edge < 3] *= .34
    a[edge < 7] *= .78
    a *= 1.04
    return np.clip(a * (.9 + fnoise(41, 1.6)[..., None] * .2), 0, 1)


emit('stone-bricks', bricks(), .21, strength=7.0)
emit('polished-stone', contrast(gblur(stone, 1.1), 1.02, pivot=.5), .74, strength=.6)

info = json.loads((OUT / 'materials.json').read_text())
info['version'] = 7
info['iteration12'] = {
    'masters': sorted(str(p.relative_to(R)) for p in SRC.glob('*.jpg')),
    'derived': files, 'resolution': 512,
    'maps': 'AI-derived albedo; normal/metallic/AO/height/smoothness estimated from luminance, not scans',
}
(OUT / 'materials.json').write_text(json.dumps(info, ensure_ascii=False, indent=2))
print('iteration 12:', count, 'material layers rewritten;', len(info['layers']), 'layers in catalog')
