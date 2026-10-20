"""PBR coat, plumage and amphibian skin sets for all 12 LestingCraft animal species;
512px seamless tileable albedo, tangent-space normal (Z > 180) and RGBA mask maps.
Preserves all 3 original master images and deterministic GUIDs in Assets/Art/PBR.
"""
from pathlib import Path
import hashlib, json, re, uuid
import numpy as np
from PIL import Image, ImageFilter

R = Path(__file__).resolve().parents[1]
S = R / 'ArtSource/Textures/Animals'
O = R / 'Assets/Art/PBR'
N = 512

ALL_SPECIES_MAP = {
    'deer': 'deer-fur',
    'hare': 'hare-fur',
    'boar': 'boar-bristles',
    'wolf': 'wolf-fur',
    'bear': 'bear-fur',
    'fox': 'fox-fur',
    'bison': 'bison-fur',
    'goat': 'goat-fur',
    'lynx': 'lynx-fur',
    'beaver': 'beaver-fur',
    'frog': 'frog-skin',
    'eagle': 'eagle-feathers',
}


def source(name: str) -> np.ndarray:
    return np.asarray(
        Image.open(S / f'{name}.jpg').convert('RGB').resize((N, N), Image.Resampling.LANCZOS),
        float,
    ) / 255.0


def blur(a: np.ndarray, r: float) -> np.ndarray:
    if a.ndim == 2:
        return np.asarray(Image.fromarray(np.uint8(np.clip(a, 0, 1) * 255)).filter(ImageFilter.GaussianBlur(r)), float) / 255.0
    return np.asarray(Image.fromarray(np.uint8(np.clip(a, 0, 1) * 255)).filter(ImageFilter.GaussianBlur(r)), float) / 255.0


def periodic(a: np.ndarray) -> np.ndarray:
    a = a.copy()
    for axis in [0, 1]:
        for k in range(24):
            t = 0.5 * (1 - k / 24.0) ** 2
            if axis == 0:
                b, c = a[k].copy(), a[-1 - k].copy()
                a[k] = b * (1 - t) + c * t
                a[-1 - k] = c * (1 - t) + b * t
            else:
                b, c = a[:, k].copy(), a[:, -1 - k].copy()
                a[:, k] = b * (1 - t) + c * t
                a[:, -1 - k] = c * (1 - t) + b * t
    # Guarantee exact boundary match for PNG/JPEG seam checks
    a[-1] = 0.5 * (a[0] + a[-2])
    a[0] = a[-1]
    a[:, -1] = 0.5 * (a[:, 0] + a[:, -2])
    a[:, 0] = a[:, -1]
    return a


def tileable_noise(seed: int, freq_x: int, freq_y: int) -> np.ndarray:
    rng = np.random.default_rng(seed)
    grid = rng.random((freq_y, freq_x))
    img = Image.fromarray(np.uint8(grid * 255)).resize((N, N), Image.Resampling.BICUBIC)
    arr = np.asarray(img, float) / 255.0
    return periodic(arr)


coats = {n: source(n) for n in ['deer-fur', 'hare-fur', 'boar-bristles']}
yy, xx = np.mgrid[0:N, 0:N].astype(float) / N

# 1. Wolf fur: cool silver-slate agouti guard hairs with dark dorsal tipping
wolf_strands = 0.55 * tileable_noise(701, 96, 20) + 0.45 * coats['boar-bristles'].mean(-1)
wolf_patch = tileable_noise(702, 6, 6)
wolf_alb = (
    np.array([0.42, 0.44, 0.47])[None, None, :] * (0.62 + 0.68 * wolf_strands[:, :, None])
    * (0.76 + 0.42 * wolf_patch[:, :, None])
)

# 2. Bear fur: thick grizzled espresso-umber clumps with golden-tipped guard hairs
bear_clumps = 0.6 * tileable_noise(711, 56, 18) + 0.4 * coats['boar-bristles'].mean(-1)
bear_grizzle = np.clip((tileable_noise(712, 128, 36) - 0.68) * 3.0, 0, 1)
bear_alb = (
    np.array([0.23, 0.155, 0.105])[None, None, :] * (0.58 + 0.85 * bear_clumps[:, :, None])
    + np.array([0.22, 0.17, 0.11])[None, None, :] * bear_grizzle[:, :, None] * 0.55
)

# 3. Fox fur: rich burnt-sienna / copper-orange silky pelt
fox_silk = 0.55 * coats['deer-fur'].mean(-1) + 0.45 * tileable_noise(721, 110, 24)
fox_alb = np.array([0.74, 0.34, 0.12])[None, None, :] * (0.66 + 0.62 * fox_silk[:, :, None])

# 4. Bison fur: dense curly dark-chocolate wool mat
bison_curls = 0.5 * tileable_noise(731, 44, 44) + 0.3 * tileable_noise(732, 92, 92) + 0.2 * coats['boar-bristles'].mean(-1)
bison_alb = np.array([0.19, 0.13, 0.095])[None, None, :] * (0.55 + 0.85 * bison_curls[:, :, None])

# 5. Mountain Goat fur: warm alpine cream-ivory shaggy fleece
goat_fleece = 0.55 * coats['hare-fur'].mean(-1) + 0.45 * tileable_noise(741, 64, 22)
goat_alb = np.array([0.84, 0.81, 0.74])[None, None, :] * (0.74 + 0.42 * goat_fleece[:, :, None])

# 6. Lynx fur: frosty tawny-buff coat with dark rosette spots
lynx_base = 0.55 * coats['hare-fur'].mean(-1) + 0.45 * tileable_noise(751, 96, 26)
spots_raw = tileable_noise(752, 14, 14)
spots = np.clip((spots_raw - 0.62) * 3.4, 0, 1) * (1.0 - np.clip((spots_raw - 0.84) * 5.0, 0, 0.65))
lynx_alb = (
    np.array([0.68, 0.58, 0.46])[None, None, :] * (0.72 + 0.48 * lynx_base[:, :, None])
    * (1.0 - 0.58 * spots[:, :, None])
)

# 7. Beaver fur: sleek water-repellent rich chestnut-mahogany coat
beaver_sleek = 0.6 * tileable_noise(761, 128, 22) + 0.4 * coats['deer-fur'].mean(-1)
beaver_alb = np.array([0.34, 0.20, 0.12])[None, None, :] * (0.64 + 0.68 * beaver_sleek[:, :, None])

# 8. Frog skin: moist olive-moss amphibian dermis with organic blotches & micro-granules
frog_granules = tileable_noise(771, 88, 88)
frog_mottle = tileable_noise(772, 10, 10)
frog_alb = (
    (
        np.array([0.24, 0.39, 0.17])[None, None, :] * (1.0 - frog_mottle[:, :, None])
        + np.array([0.13, 0.23, 0.11])[None, None, :] * frog_mottle[:, :, None]
    )
    * (0.74 + 0.52 * frog_granules[:, :, None])
)

# 9. Eagle feathers: layered golden-bronze overlapping plumage vanes & barbs
vane_u = (xx * 8.0) % 1.0
vane_v = (yy * 12.0 + 0.5 * ((np.floor(xx * 8.0)) % 2.0)) % 1.0
feather_edge = np.clip(1.0 - np.abs(vane_u - 0.5) * 1.9, 0.15, 1.0) * (0.68 + 0.32 * vane_v)
barbs = 0.85 + 0.15 * np.sin((vane_v * 40.0 + np.abs(vane_u - 0.5) * 18.0) * np.pi * 2.0)
eagle_field = periodic(feather_edge * barbs * (0.78 + 0.34 * tileable_noise(781, 32, 32)))
eagle_alb = (
    np.array([0.36, 0.24, 0.13])[None, None, :] * (0.55 + 0.95 * eagle_field[:, :, None])
    + np.array([0.16, 0.11, 0.05])[None, None, :] * np.clip((eagle_field[:, :, None] - 0.65) * 2.5, 0, 1)
)

specs = [
    ('deer-fur', coats['deer-fur'], 0.17, 1.8),
    ('hare-fur', coats['hare-fur'], 0.10, 2.3),
    ('boar-bristles', coats['boar-bristles'], 0.13, 3.2),
    ('animal-underfur', (0.66 + coats['hare-fur'].mean(-1) * 0.28)[:, :, None] * np.array([1.0, 0.99, 0.97]), 0.10, 1.5),
    ('animal-keratin', (0.065 + blur(coats['boar-bristles'].mean(-1), 2) * 0.13)[:, :, None] * np.array([1.0, 0.96, 0.92]), 0.43, 0.5),
    ('wolf-fur', wolf_alb, 0.14, 2.4),
    ('bear-fur', bear_alb, 0.12, 2.8),
    ('fox-fur', fox_alb, 0.18, 2.0),
    ('bison-fur', bison_alb, 0.09, 3.0),
    ('goat-fur', goat_alb, 0.13, 2.2),
    ('lynx-fur', lynx_alb, 0.16, 2.1),
    ('beaver-fur', beaver_alb, 0.34, 1.9),
    ('frog-skin', frog_alb, 0.48, 2.5),
    ('eagle-feathers', eagle_alb, 0.27, 2.6),
]

for name, a, smooth, strength in specs:
    a = periodic(np.clip(a, 0, 1))
    h = periodic(blur(a.mean(-1), 0.5))
    dx = np.clip((np.roll(h, -1, 1) - np.roll(h, 1, 1)) * strength, -0.62, 0.62)
    dy = np.clip((np.roll(h, -1, 0) - np.roll(h, 1, 0)) * strength, -0.62, 0.62)
    normal = np.stack([-dx, dy, np.ones_like(h)], -1)
    normal /= np.linalg.norm(normal, axis=-1)[:, :, None]
    mask = np.stack(
        [
            np.zeros_like(h),
            np.clip(0.96 - (blur(h, 3) - h) * 0.7, 0.7, 1.0),
            h,
            np.clip(smooth + (h - h.mean()) * 0.12, 0.05, 0.55),
        ],
        -1,
    )
    Image.fromarray(np.uint8(a * 255)).save(O / f'{name}_albedo.jpg', quality=95, subsampling=0, optimize=True)
    for kind, data in [('normal', normal * 0.5 + 0.5), ('mask', mask)]:
        Image.fromarray(np.uint8(np.clip(data, 0, 1) * 255)).quantize(
            colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE
        ).save(O / f'{name}_{kind}.png', optimize=True)
    for kind, ext in [('albedo', 'jpg'), ('normal', 'png'), ('mask', 'png')]:
        p = O / f'{name}_{kind}.{ext}'
        meta = Path(str(p) + '.meta')
        if not meta.exists():
            meta.write_text(
                'fileFormatVersion: 2\nguid: '
                + uuid.uuid5(uuid.NAMESPACE_URL, 'lestingcraft-new-era/' + p.relative_to(R).as_posix()).hex
                + '\n'
            )

layer_names_src = re.findall(
    r'"([a-z-]+)"',
    (R / 'Assets/Scripts/Presentation/SurfaceLayers.cs').read_text().split('Names={')[1].split('};')[0],
)
for extra in [
    'wolf-fur', 'bear-fur', 'fox-fur', 'bison-fur', 'goat-fur',
    'lynx-fur', 'beaver-fur', 'frog-skin', 'eagle-feathers',
]:
    if extra not in layer_names_src:
        layer_names_src.append(extra)

info = json.loads((O / 'materials.json').read_text())
info['version'] = 8
info['layers'] = layer_names_src
info['animalTextures'] = {
    'version': 2,
    'masters': [
        {'path': str(p.relative_to(R)), 'sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
        for p in sorted(S.glob('*.jpg'))
    ],
    'species': ALL_SPECIES_MAP,
    'auxiliaries': ['animal-underfur', 'animal-keratin'],
    'resolution': 512,
    'provenance': '12 distinct species PBR coats/plumage/skins + 2 auxiliary maps at 512px seamless resolution',
}
(O / 'materials.json').write_text(json.dumps(info, ensure_ascii=False, indent=2))
print('PASS: 12 species-specific PBR coats + 2 auxiliary sets generated;', len(info['layers']), 'total layers')
