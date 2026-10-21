"""Era v9 Industrial, Nuclear, Rail, Cybernetics & Irradiated Abyss Asset Builder:
1. Generates 24 high-definition seamless 512x512 PBR material sets (indices 88..111 -> 112 total):
   - 14 industrial / electrical / nuclear / rail / explosive PBR layers (88..101)
   - 10 hostile irradiated-abyss mutant hides & carapaces (102..111 -> 30 species total)
   Each has seamless _albedo.jpg, tangent-space _normal.png (Z > 180), _mask.png [Metallic, AO, Height, Smoothness], .meta, and materials.json registration.
2. Appends 20 new placeable blocks (IDs 70..89 -> 71 ExpandedBlocks, MaxBlock = 89) +
   6 industrial/vehicle/tech items (IDs 190..195) + SECRET THERMONUKE ID 666 (154 total 3D VoxelItems & 384x384 RGBA icons).
"""
from model_io import load_catalog, save_catalog
from pathlib import Path
import hashlib, json, re, uuid
import numpy as np
from PIL import Image

R = Path(__file__).resolve().parents[1]
PBR = R / 'Assets/Art/PBR'
N = 512

V8_NAMES = [
    "stone","dirt","grass","grass-side","bark","wood-end","leaves","sand","water","lava",
    "bedrock","basalt","crystal","aether","ore","snow","planks","workbench-top","workbench-side",
    "furnace-side","furnace-front","furnace-top","campfire-side","campfire-top","ore-copper",
    "ore-coal","ore-gold","ore-silver","ore-tin","ore-quartz","ore-sulfur","granite","limestone",
    "slate","marble","obsidian","gravel","clay","mud","mossy-stone","cobblestone","stone-bricks",
    "cracked-bricks","clay-bricks","polished-stone","sandstone","coal-block","copper-block",
    "polished-granite","polished-marble","wildlife-fur","metal-ingot","deer-fur","hare-fur",
    "boar-bristles","animal-underfur","animal-keratin","diamond-ore","diamond-facets","leather",
    "raw-meat","cooked-meat","cat-fur",
    "wolf-fur","bear-fur","fox-fur","bison-fur","goat-fur","lynx-fur","beaver-fur",
    "frog-skin","eagle-feathers",
    "owl-feathers","penguin-plumage","camel-coat","scorpion-chitin","turtle-shell",
    "moose-fur","capybara-fur","voidray-skin",
    "terracotta-shingles","cherry-blossom","glow-shroom","red-mesa","glacial-ice",
    "magma-basalt","titanium-hull","andromeda-crystal",
]

V9_INDUSTRIAL_LAYERS = [
    "uranium-ore",          # 88
    "uranium-block",        # 89
    "copper-cable",         # 90
    "battery-bank",         # 91
    "generator-casing",     # 92
    "nuclear-reactor",      # 93
    "cherenkov-crystal",    # 94
    "reinforced-concrete",  # 95
    "hazard-steel",         # 96
    "lead-shield",          # 97
    "steel-rail",           # 98
    "powered-rail",         # 99
    "c4-explosive",         # 100
    "thermonuke-666",       # 101
]

V9_MUTANT_COATS = [
    "ash-hound-hide",       # 102 (species 20)
    "cherenkov-leech-skin", # 103 (species 21)
    "rad-scorpion-carapace",# 104 (species 22)
    "mutant-boar-hide",     # 105 (species 23)
    "slag-carapace",        # 106 (species 24)
    "fallout-crow-plumage", # 107 (species 25)
    "gamma-ray-membrane",   # 108 (species 26)
    "bone-stalker-hide",    # 109 (species 27)
    "vitrified-golem-crust",# 110 (species 28)
    "wraith-shroud",        # 111 (species 29)
]

ALL_NAMES = V8_NAMES + V9_INDUSTRIAL_LAYERS + V9_MUTANT_COATS
assert len(ALL_NAMES) == 112 and len(set(ALL_NAMES)) == 112


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
    a[-1] = 0.5 * (a[0] + a[-2])
    a[0] = a[-1]
    a[:, -1] = 0.5 * (a[:, 0] + a[:, -2])
    a[:, 0] = a[:, -1]
    return a


def tileable_noise(seed: int, fx: int, fy: int) -> np.ndarray:
    rng = np.random.default_rng(seed)
    grid = rng.random((fy, fx))
    img = Image.fromarray(np.uint8(grid * 255)).resize((N, N), Image.Resampling.BICUBIC)
    return periodic(np.asarray(img, float) / 255.0)


def fbm(seed: int, base: int = 8, octaves: int = 4) -> np.ndarray:
    acc = np.zeros((N, N), float)
    w_sum = 0.0
    for o in range(octaves):
        w = 0.5 ** o
        f = base * (2 ** o)
        acc += w * tileable_noise(seed + o * 37, min(256, f), min(256, f))
        w_sum += w
    return acc / w_sum


def write_meta(path: Path):
    meta = path.with_name(path.name + '.meta')
    if not meta.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, f'lestingcraft-era-v9-pbr:{path.name}').hex
        meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\n', encoding='utf-8')


def save_pbr_set(name: str, alb: np.ndarray, height_field: np.ndarray, rough: np.ndarray, metallic: float = 0.0, strength: float = 2.4):
    alb_p = periodic(np.clip(alb, 0.02, 0.985))
    h_p = periodic(np.clip(height_field, 0.0, 1.0))
    dx = (np.roll(h_p, -1, axis=1) - np.roll(h_p, 1, axis=1)) * strength
    dy = (np.roll(h_p, -1, axis=0) - np.roll(h_p, 1, axis=0)) * strength
    nz = np.ones_like(h_p)
    norm_len = np.sqrt(dx * dx + dy * dy + nz * nz)
    nx = (-dx / norm_len) * 0.5 + 0.5
    ny = (-dy / norm_len) * 0.5 + 0.5
    nzz = np.clip((nz / norm_len) * 0.5 + 0.5, 0.75, 1.0)
    normal_rgb = np.stack([nx, ny, nzz], axis=-1)

    ao = np.clip(0.72 + 0.28 * h_p, 0.35, 1.0)
    smooth = np.clip(np.full_like(ao, 1.0 - rough) if isinstance(rough, (int, float)) else (1.0 - periodic(rough)), 0.05, 0.95)
    met = np.full_like(ao, metallic) if isinstance(metallic, (int, float)) else np.clip(periodic(metallic), 0.0, 1.0)
    mask_rgba = np.stack([met, ao, h_p, smooth], axis=-1)

    alb_path = PBR / f'{name}_albedo.jpg'
    nrm_path = PBR / f'{name}_normal.png'
    msk_path = PBR / f'{name}_mask.png'
    Image.fromarray(np.uint8(np.clip(alb_p * 255, 0, 255))).save(alb_path, quality=93, subsampling=0)
    Image.fromarray(np.uint8(np.clip(normal_rgb * 255, 0, 255))).save(nrm_path, optimize=True)
    Image.fromarray(np.uint8(np.clip(mask_rgba * 255, 0, 255))).save(msk_path, optimize=True)
    write_meta(alb_path)
    write_meta(nrm_path)
    write_meta(msk_path)


yy, xx = np.mgrid[0:N, 0:N].astype(float) / N

# ==============================================================================
# 1. INDUSTRIAL, ELECTRICAL, NUCLEAR, RAIL & EXPLOSIVE HD PBR MAPS (88..101)
# ==============================================================================

# 88: uranium-ore — dark fractured host basalt with vivid fluorescent uraninite veins
rock_n = fbm(1901, 10, 4)
vein_field = np.abs(fbm(1911, 6, 4) - 0.5)
vein = np.clip(1.0 - vein_field * 9.5, 0.0, 1.0) ** 1.4
nodule = np.clip((tileable_noise(1917, 28, 28) - 0.68) * 3.4, 0.0, 1.0)
u_glow = np.clip(vein * 0.85 + nodule * 0.65, 0.0, 1.0)
save_pbr_set(
    'uranium-ore',
    np.array([0.14, 0.16, 0.18])[None, None, :] * (0.65 + 0.65 * rock_n[:, :, None]) * (1.0 - 0.80 * u_glow[:, :, None])
    + np.array([0.24, 0.96, 0.34])[None, None, :] * u_glow[:, :, None],
    0.55 * rock_n + 0.45 * u_glow,
    0.72 - 0.45 * u_glow,
    metallic=0.15 + 0.55 * u_glow,
)

# 89: uranium-block — heavy machined fissile fuel canister grid
cell_gx = np.abs(((xx * 4.0) % 1.0) - 0.5) * 2.0
cell_gy = np.abs(((yy * 4.0) % 1.0) - 0.5) * 2.0
rod_core = np.clip(1.0 - np.hypot(cell_gx, cell_gy) * 1.35, 0.0, 1.0)
brush = fbm(1921, 32, 3)
save_pbr_set(
    'uranium-block',
    np.array([0.22, 0.27, 0.25])[None, None, :] * (0.8 + 0.3 * brush[:, :, None]) * (1.0 - rod_core[:, :, None])
    + np.array([0.32, 0.95, 0.40])[None, None, :] * rod_core[:, :, None],
    0.35 + 0.45 * rod_core + 0.20 * brush,
    0.32 + 0.18 * brush,
    metallic=0.78,
)

# 90: copper-cable — braided heavy copper busbars + black neoprene jacket + yellow voltage bands
bus_band = (np.abs(xx - 0.5) < 0.20) | (np.abs(yy - 0.5) < 0.20)
core_cu = (np.abs(xx - 0.5) < 0.09) | (np.abs(yy - 0.5) < 0.09)
weave = 0.5 + 0.5 * np.sin(2 * np.pi * (xx * 24 + yy * 24))
alb_cable = np.array([0.11, 0.12, 0.14])[None, None, :] * (0.85 + 0.25 * brush[:, :, None])
alb_cable = np.where(bus_band[:, :, None], np.array([0.18, 0.19, 0.22])[None, None, :], alb_cable)
alb_cable = np.where(core_cu[:, :, None], np.array([0.88, 0.47, 0.20])[None, None, :] * (0.82 + 0.28 * weave[:, :, None]), alb_cable)
save_pbr_set(
    'copper-cable',
    alb_cable,
    0.25 + 0.35 * bus_band.astype(float) + 0.35 * core_cu.astype(float) * weave,
    0.55 - 0.32 * core_cu.astype(float),
    metallic=0.15 + 0.78 * core_cu.astype(float),
)

# 91: battery-bank — industrial LiFePO4 steel cabinet + glowing cyan charge bar segments
border = (xx < 0.07) | (xx > 0.93) | (yy < 0.07) | (yy > 0.93)
led_bar = (np.abs(xx - 0.5) < 0.26) & (yy > 0.20) & (yy < 0.80) & (((yy * 12.0) % 1.0) > 0.28)
cu_term = (yy < 0.16) & ((np.abs(xx - 0.28) < 0.08) | (np.abs(xx - 0.72) < 0.08))
alb_bat = np.array([0.16, 0.20, 0.25])[None, None, :] * (0.84 + 0.26 * brush[:, :, None])
alb_bat = np.where(border[:, :, None], np.array([0.34, 0.39, 0.45])[None, None, :], alb_bat)
alb_bat = np.where(led_bar[:, :, None], np.array([0.12, 0.88, 0.98])[None, None, :], alb_bat)
alb_bat = np.where(cu_term[:, :, None], np.array([0.90, 0.50, 0.18])[None, None, :], alb_bat)
save_pbr_set('battery-bank', alb_bat, 0.4 + 0.35 * border.astype(float) + 0.2 * led_bar.astype(float), 0.34, metallic=0.72)

# 92: generator-casing — heavy turbine louver grill + warm copper stator coils
louvers = (((yy * 16.0) % 1.0) < 0.55) & (xx > 0.14) & (xx < 0.86) & (yy > 0.14) & (yy < 0.86)
coil_peek = louvers & (np.abs(xx - 0.5) < 0.26)
alb_gen = np.array([0.25, 0.27, 0.29])[None, None, :] * (0.82 + 0.30 * brush[:, :, None])
alb_gen = np.where(louvers[:, :, None], np.array([0.09, 0.10, 0.12])[None, None, :], alb_gen)
alb_gen = np.where(coil_peek[:, :, None], np.array([0.78, 0.44, 0.18])[None, None, :], alb_gen)
save_pbr_set('generator-casing', alb_gen, 0.65 - 0.40 * louvers.astype(float), 0.38, metallic=0.80)

# 93: nuclear-reactor — heavy zirconium vessel + brilliant blue Cherenkov coolant window
port_r = np.hypot(xx - 0.5, yy - 0.5)
cher_win = port_r < 0.31
cher_grid = cher_win & ((np.abs(((xx * 10) % 1.0) - 0.5) < 0.10) | (np.abs(((yy * 10) % 1.0) - 0.5) < 0.10))
alb_rx = np.array([0.20, 0.23, 0.27])[None, None, :] * (0.82 + 0.28 * brush[:, :, None])
alb_rx = np.where((port_r < 0.37)[:, :, None], np.array([0.42, 0.47, 0.53])[None, None, :], alb_rx)
alb_rx = np.where(cher_win[:, :, None], np.array([0.10, 0.72, 1.00])[None, None, :] * (1.15 - port_r[:, :, None]), alb_rx)
alb_rx = np.where(cher_grid[:, :, None], np.array([0.14, 0.18, 0.24])[None, None, :], alb_rx)
save_pbr_set('nuclear-reactor', alb_rx, 0.55 - 0.25 * cher_win.astype(float) + 0.25 * cher_grid.astype(float), 0.26, metallic=0.82)

# 94: cherenkov-crystal — exact glowing electric-cyan/blue underwater cubes from user reference photo!
rim_d = np.minimum(np.minimum(xx, 1.0 - xx), np.minimum(yy, 1.0 - yy))
dark_frame = np.clip(1.0 - rim_d * 10.5, 0.0, 1.0)
cryst_n = fbm(1951, 8, 4)
alb_cher = (
    np.array([0.18, 0.68, 0.98])[None, None, :] * (0.78 + 0.42 * cryst_n[:, :, None]) * (1.0 - 0.78 * dark_frame[:, :, None])
    + np.array([0.55, 0.92, 1.00])[None, None, :] * np.clip(1.0 - np.hypot(xx - 0.5, yy - 0.5) * 1.8, 0.0, 1.0)[:, :, None] * 0.45
    + np.array([0.04, 0.11, 0.18])[None, None, :] * dark_frame[:, :, None] * 0.78
)
save_pbr_set('cherenkov-crystal', alb_cher, 0.75 - 0.5 * dark_frame + 0.2 * cryst_n, 0.14 + 0.2 * cryst_n, metallic=0.10)

# 95: reinforced-concrete — weathered brutalist dam concrete with tie-rod holes & seam lines
seam_c = (yy < 0.03) | (np.abs(yy - 0.5) < 0.015) | (xx < 0.02)
conc_n = 0.65 * fbm(1961, 16, 4) + 0.35 * fbm(1967, 64, 3)
tie_hole = (np.hypot((xx * 2 % 1.0) - 0.5, (yy * 2 % 1.0) - 0.5) < 0.045)
alb_conc = np.array([0.44, 0.46, 0.47])[None, None, :] * (0.78 + 0.36 * conc_n[:, :, None])
alb_conc = np.where(seam_c[:, :, None] | tie_hole[:, :, None], alb_conc * 0.55, alb_conc)
save_pbr_set('reinforced-concrete', alb_conc, 0.62 * conc_n + 0.38 * (1.0 - (seam_c | tie_hole).astype(float)), 0.84, metallic=0.04)

# 96: hazard-steel — industrial yellow/black diagonal caution plate with diamond tread
stripe = (((xx + yy) * 5.0) % 1.0) < 0.5
tread = tileable_noise(1971, 64, 64)
alb_haz = np.where(
    stripe[:, :, None],
    np.array([0.90, 0.70, 0.10])[None, None, :] * (0.85 + 0.25 * tread[:, :, None]),
    np.array([0.12, 0.13, 0.15])[None, None, :] * (0.80 + 0.35 * tread[:, :, None]),
)
save_pbr_set('hazard-steel', alb_haz, 0.45 + 0.35 * tread + 0.15 * stripe.astype(float), 0.42, metallic=0.68)

# 97: lead-shield — dense heavy matte plumbeous radiation-shielding tiles with corner rivets
lead_seam = (xx < 0.04) | (xx > 0.96) | (yy < 0.04) | (yy > 0.96) | (np.abs(xx - 0.5) < 0.02)
rivet = np.zeros_like(xx, dtype=bool)
for rx in (0.10, 0.44, 0.56, 0.90):
    for ry in (0.10, 0.90):
        rivet |= np.hypot(xx - rx, yy - ry) < 0.028
alb_lead = np.array([0.28, 0.31, 0.36])[None, None, :] * (0.86 + 0.24 * brush[:, :, None])
alb_lead = np.where(lead_seam[:, :, None], alb_lead * 0.55, alb_lead)
alb_lead = np.where(rivet[:, :, None], alb_lead * 1.28, alb_lead)
save_pbr_set('lead-shield', alb_lead, 0.55 - 0.35 * lead_seam.astype(float) + 0.35 * rivet.astype(float), 0.52, metallic=0.76)

# 98: steel-rail — crushed granite ballast + creosote timber sleepers + polished twin steel rails!
ballast = fbm(1981, 48, 4)
sleeper = (((yy * 5.0) % 1.0) < 0.42) & (xx > 0.08) & (xx < 0.92)
rail_bar = (np.abs(xx - 0.27) < 0.055) | (np.abs(xx - 0.73) < 0.055)
alb_rail = np.array([0.34, 0.33, 0.35])[None, None, :] * (0.65 + 0.65 * ballast[:, :, None])
alb_rail = np.where(sleeper[:, :, None], np.array([0.28, 0.19, 0.12])[None, None, :] * (0.82 + 0.32 * brush[:, :, None]), alb_rail)
alb_rail = np.where(rail_bar[:, :, None], np.array([0.76, 0.80, 0.85])[None, None, :], alb_rail)
save_pbr_set(
    'steel-rail',
    alb_rail,
    0.25 * ballast + 0.30 * sleeper.astype(float) + 0.45 * rail_bar.astype(float),
    0.82 - 0.62 * rail_bar.astype(float),
    metallic=0.88 * rail_bar.astype(float),
)

# 99: powered-rail — twin steel rails + glowing energized copper third-rail bus in center
third_rail = np.abs(xx - 0.50) < 0.065
alb_prail = np.where(third_rail[:, :, None], np.array([0.96, 0.54, 0.16])[None, None, :], alb_rail)
save_pbr_set(
    'powered-rail',
    alb_prail,
    0.25 * ballast + 0.30 * sleeper.astype(float) + 0.45 * (rail_bar | third_rail).astype(float),
    0.78 - 0.58 * (rail_bar | third_rail).astype(float),
    metallic=0.90 * (rail_bar | third_rail).astype(float),
)

# 100: c4-explosive — tactical olive polymer bricks + black strapping + glowing red digital detonator!
strap = (np.abs(yy - 0.28) < 0.05) | (np.abs(yy - 0.72) < 0.05) | (np.abs(xx - 0.5) < 0.04)
det_box = (np.abs(xx - 0.5) < 0.20) & (np.abs(yy - 0.5) < 0.14)
det_led = (np.abs(xx - 0.5) < 0.13) & (np.abs(yy - 0.5) < 0.07)
alb_c4 = np.array([0.36, 0.40, 0.31])[None, None, :] * (0.85 + 0.25 * brush[:, :, None])
alb_c4 = np.where(strap[:, :, None], np.array([0.11, 0.12, 0.13])[None, None, :], alb_c4)
alb_c4 = np.where(det_box[:, :, None], np.array([0.18, 0.20, 0.22])[None, None, :], alb_c4)
alb_c4 = np.where(det_led[:, :, None], np.array([0.98, 0.16, 0.12])[None, None, :], alb_c4)
save_pbr_set('c4-explosive', alb_c4, 0.45 + 0.25 * strap.astype(float) + 0.30 * det_box.astype(float), 0.56, metallic=0.22)

# 101: thermonuke-666 — obsidian-titanium warhead casing with yellow trefoil emblem & crimson core
ang = np.arctan2(yy - 0.5, xx - 0.5)
rad_d = np.hypot(xx - 0.5, yy - 0.5)
sector = ((np.degrees(ang) % 120.0) < 60.0) & (rad_d > 0.10) & (rad_d < 0.30)
center_dot = rad_d < 0.065
trefoil = sector | center_dot
alb_666 = np.array([0.10, 0.11, 0.13])[None, None, :] * (0.84 + 0.32 * brush[:, :, None])
alb_666 = np.where(border[:, :, None], np.array([0.85, 0.14, 0.10])[None, None, :], alb_666)
alb_666 = np.where(trefoil[:, :, None], np.array([0.96, 0.78, 0.10])[None, None, :], alb_666)
save_pbr_set('thermonuke-666', alb_666, 0.45 + 0.35 * trefoil.astype(float) + 0.2 * border.astype(float), 0.28, metallic=0.84)

# ==============================================================================
# 2. TEN HOSTILE IRRADIATED ABYSS MUTANT COATS (102..111)
# ==============================================================================
MUTANT_SPECS = [
    ("ash-hound-hide",        [0.18, 0.19, 0.21], [0.16, 0.78, 1.00], 2001, 0.58, 0.12),
    ("cherenkov-leech-skin",  [0.08, 0.16, 0.26], [0.22, 0.88, 1.00], 2011, 0.20, 0.25),
    ("rad-scorpion-carapace", [0.16, 0.24, 0.18], [0.35, 0.95, 0.42], 2021, 0.30, 0.40),
    ("mutant-boar-hide",      [0.24, 0.22, 0.22], [0.55, 0.75, 0.88], 2031, 0.70, 0.08),
    ("slag-carapace",         [0.13, 0.14, 0.16], [0.18, 0.66, 0.96], 2041, 0.36, 0.55),
    ("fallout-crow-plumage",  [0.10, 0.11, 0.14], [0.38, 0.76, 0.95], 2051, 0.52, 0.15),
    ("gamma-ray-membrane",    [0.12, 0.30, 0.44], [0.48, 0.94, 1.00], 2061, 0.22, 0.20),
    ("bone-stalker-hide",     [0.62, 0.62, 0.58], [0.16, 0.72, 0.96], 2071, 0.64, 0.10),
    ("vitrified-golem-crust", [0.15, 0.18, 0.20], [0.20, 0.85, 1.00], 2081, 0.34, 0.48),
    ("wraith-shroud",         [0.28, 0.31, 0.36], [0.52, 0.90, 1.00], 2091, 0.48, 0.18),
]
for m_name, base_rgb, vein_rgb, m_seed, rough_v, met_v in MUTANT_SPECS:
    mn = fbm(m_seed, 16, 4)
    mv = np.clip(1.0 - np.abs(fbm(m_seed + 5, 7, 3) - 0.5) * 11.0, 0.0, 1.0)
    save_pbr_set(
        m_name,
        np.array(base_rgb)[None, None, :] * (0.68 + 0.65 * mn[:, :, None]) * (1.0 - 0.72 * mv[:, :, None])
        + np.array(vein_rgb)[None, None, :] * mv[:, :, None] * 0.85,
        0.65 * mn + 0.35 * mv,
        rough_v - 0.20 * mv,
        metallic=met_v,
    )

# ==============================================================================
# 3. REGISTER ALL 112 LAYERS IN materials.json & SurfaceLayers.cs
# ==============================================================================
mat_json_path = PBR / 'materials.json'
mat_info = json.loads(mat_json_path.read_text(encoding='utf-8'))
mat_info['layers'] = ALL_NAMES
species_map = dict(mat_info['animalTextures']['species'])
MUTANT_SPECIES_KEYS = [
    ("ash_hound", "ash-hound-hide"),
    ("cherenkov_leech", "cherenkov-leech-skin"),
    ("rad_scorpion", "rad-scorpion-carapace"),
    ("mutant_boar", "mutant-boar-hide"),
    ("slag_carapace", "slag-carapace"),
    ("fallout_crow", "fallout-crow-plumage"),
    ("gamma_ray", "gamma-ray-membrane"),
    ("bone_stalker", "bone-stalker-hide"),
    ("vitrified_golem", "vitrified-golem-crust"),
    ("wraith_stalker", "wraith-shroud"),
]
for sp_key, coat_name in MUTANT_SPECIES_KEYS:
    species_map[sp_key] = coat_name
mat_info['animalTextures']['species'] = species_map
mat_json_path.write_text(json.dumps(mat_info, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

surf_cs_path = R / 'Assets/Scripts/Presentation/SurfaceLayers.cs'
surf_cs = surf_cs_path.read_text(encoding='utf-8')
names_literal = ','.join(f'"{n}"' for n in ALL_NAMES)
surf_cs = re.sub(
    r'public static readonly string\[\] Names=\{[^}]+\};',
    f'public static readonly string[] Names={{{names_literal}}};',
    surf_cs,
    count=1,
)
surf_cs_path.write_text(surf_cs, encoding='utf-8')

# ==============================================================================
# 4. UPDATE ExpandedBlocks.json & ExpandedContent.cs (IDs 19..89 = 71 BLOCKS)
# ==============================================================================
exp_json_path = R / 'Assets/Resources/Models/ExpandedBlocks.json'
prev_blocks = [b for b in json.loads(exp_json_path.read_text(encoding='utf-8'))['blocks'] if b['id'] <= 69]
assert [b['id'] for b in prev_blocks] == list(range(19, 70))

V9_BLOCKS = [
    dict(id=70, name='Урановая руда',             surface='uranium-ore',         hardness=4.2, pick=True,  layer=ALL_NAMES.index('uranium-ore')),
    dict(id=71, name='Урановый топливный блок',   surface='uranium-block',       hardness=5.0, pick=True,  layer=ALL_NAMES.index('uranium-block')),
    dict(id=72, name='Силовой медный кабель',     surface='copper-cable',        hardness=0.9, pick=False, layer=ALL_NAMES.index('copper-cable')),
    dict(id=73, name='Промышленный аккумулятор',  surface='battery-bank',        hardness=3.2, pick=True,  layer=ALL_NAMES.index('battery-bank')),
    dict(id=74, name='Топливный электрогенератор',surface='generator-casing',    hardness=3.5, pick=True,  layer=ALL_NAMES.index('generator-casing')),
    dict(id=75, name='Солнечная электростанция',  surface='titanium-hull',       hardness=2.4, pick=True,  layer=ALL_NAMES.index('titanium-hull')),
    dict(id=76, name='Ядерный реактор ВВЭР',      surface='nuclear-reactor',     hardness=6.5, pick=True,  layer=ALL_NAMES.index('nuclear-reactor')),
    dict(id=77, name='Электрический прожектор',   surface='metal-ingot',         hardness=1.8, pick=True,  layer=ALL_NAMES.index('metal-ingot')),
    dict(id=78, name='Черенковский излучатель',   surface='cherenkov-crystal',   hardness=3.8, pick=True,  layer=ALL_NAMES.index('cherenkov-crystal')),
    dict(id=79, name='Армированный бетон',        surface='reinforced-concrete', hardness=5.5, pick=True,  layer=ALL_NAMES.index('reinforced-concrete')),
    dict(id=80, name='Сигнальная сталь',          surface='hazard-steel',        hardness=4.0, pick=True,  layer=ALL_NAMES.index('hazard-steel')),
    dict(id=81, name='Свинцовая биозащита',       surface='lead-shield',         hardness=4.8, pick=True,  layer=ALL_NAMES.index('lead-shield')),
    dict(id=82, name='Белая ферменная опора',     surface='polished-marble',     hardness=3.4, pick=True,  layer=ALL_NAMES.index('polished-marble')),
    dict(id=83, name='Асфальтобетон',             surface='basalt',              hardness=2.6, pick=True,  layer=ALL_NAMES.index('basalt')),
    dict(id=84, name='Бронированное стекло',      surface='crystal',             hardness=3.0, pick=True,  layer=ALL_NAMES.index('crystal')),
    dict(id=85, name='Стальные рельсы',           surface='steel-rail',          hardness=1.6, pick=True,  layer=ALL_NAMES.index('steel-rail')),
    dict(id=86, name='Контактный электрорельс',   surface='powered-rail',        hardness=1.9, pick=True,  layer=ALL_NAMES.index('powered-rail')),
    dict(id=87, name='Фугасный заряд С-4',        surface='c4-explosive',        hardness=0.8, pick=False, layer=ALL_NAMES.index('c4-explosive')),
    dict(id=88, name='Остеклованный тринитит',    surface='obsidian',            hardness=4.4, pick=True,  layer=ALL_NAMES.index('obsidian')),
    dict(id=89, name='Шлюзовой терминал Затона',  surface='nuclear-reactor',     hardness=9.0, pick=True,  layer=ALL_NAMES.index('nuclear-reactor')),
]

all_blocks = prev_blocks + V9_BLOCKS
exp_json_path.write_text(json.dumps({'blocks': all_blocks}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

exp_cs_path = R / 'Assets/Scripts/Gameplay/ExpandedContent.cs'
exp_cs_old = exp_cs_path.read_text(encoding='utf-8')
block_entries = ',\n   '.join(
    f'new ExpandedBlock({b["id"]},"{b["name"]}","{b["surface"]}",{b["hardness"]}f,{"true" if b["pick"] else "false"},{b["layer"]})'
    for b in all_blocks
)
exp_cs_new = re.sub(r'MaxBlock=\d+', 'MaxBlock=89', exp_cs_old, count=1)
exp_cs_new = re.sub(
    r'public static readonly ExpandedBlock\[\] Blocks=\{.*?\};',
    f'public static readonly ExpandedBlock[] Blocks={{\n   {block_entries}\n  }};',
    exp_cs_new,
    count=1,
    flags=re.S,
)
exp_cs_new = re.sub(r'id<=69', 'id<=89', exp_cs_new)
# Allow smelting Uranium Ore (70) -> Uranium Fuel Rod (190)!
exp_cs_new = re.sub(
    r'public static int Smelt\(int id\)=>([^;]+);',
    'public static int Smelt(int id)=>id==70?190:id>=20&&id<=26?140+id-20:id==6?44:id==33?39:id==135?138:0;',
    exp_cs_new,
)
exp_cs_path.write_text(exp_cs_new, encoding='utf-8')

# ==============================================================================
# 5. SCULPT DETAILED 3D VOXEL MODELS & 384x384 TRANSPARENT ICONS FOR
#    IDs 70..89, 190..195, AND SECRET THERMONUKE 666!
# ==============================================================================
source = (R / 'Tools/build_voxel_items.py').read_text(encoding='utf-8')
ctx = {'__file__': str(R / 'Tools/build_voxel_items.py')}
exec(compile(source.split('serial=[]')[0], str(R / 'Tools/build_voxel_items.py'), 'exec'), ctx)

d = load_catalog(R)
palette = list(zip(d['colors'], d['layers']))
ctx['PALETTE'] = palette
ctx['COLORS'] = [np.array([int(c[i:i+2], 16) / 255 for i in (1, 3, 5)]) for c, l in palette]
ctx['NAMES'] = ALL_NAMES


def pal(color: str, layer_name: str) -> int:
    layer = ALL_NAMES.index(layer_name)
    if (color, layer) not in palette:
        palette.append((color, layer))
        ctx['COLORS'].append(np.array([int(color[i:i+2], 16) / 255 for i in (1, 3, 5)]))
    return palette.index((color, layer))


box = ctx['box']
model = ctx['model']
render = ctx['render']

DARK_STEEL = pal('#252a32', 'metal-ingot')
BRIGHT_STEEL = pal('#aeb8c4', 'metal-ingot')
COPPER_BUS = pal('#e87828', 'copper-cable')
CABLE_JACKET = pal('#1b1e24', 'copper-cable')
HAZARD_YLW = pal('#f2be1a', 'hazard-steel')
RAD_GREEN = pal('#3cf258', 'uranium-ore')
CHER_CYAN = pal('#26b8ff', 'cherenkov-crystal')
CHER_WHITE = pal('#b8f2ff', 'cherenkov-crystal')
RED_ALERT = pal('#f52822', 'c4-explosive')
OLIVE_C4 = pal('#5c664e', 'c4-explosive')
CONC_GREY = pal('#787c80', 'reinforced-concrete')
WOOD_TIE = pal('#593b24', 'planks')
SOLAR_BLUE = pal('#1b499e', 'titanium-hull')

sculpted = []

# Full blocks & custom 3D industrial blocks 70..89 (each > 1,000 cells, bounds strictly in 0..16)
for b in V9_BLOCKS:
    bid = b['id']
    m = model(bid, b['name'])
    MAIN_P = pal('#d8dde3', b['surface'])
    if bid == 72:
        # 3D Copper Power Cable hub with thick cross-conduits & orange copper terminals (>350 voxels = >1,400 ints)
        box(m, 4, 0, 4, 12, 12, 12, CABLE_JACKET)
        box(m, 0, 3, 5, 16, 9, 11, CABLE_JACKET)
        box(m, 5, 3, 0, 11, 9, 16, CABLE_JACKET)
        box(m, 0, 4, 6, 16, 8, 10, COPPER_BUS)
        box(m, 6, 4, 0, 10, 8, 16, COPPER_BUS)
        box(m, 5, 12, 5, 11, 14, 11, COPPER_BUS)
    elif bid in (85, 86):
        # Detailed 3D Railway Track with ballast bed, 3 timber sleepers & 2 steel I-rails (+ energized 3rd rail for 86!)
        box(m, 0, 0, 0, 16, 3, 16, pal('#66686e', 'gravel'))
        for sz in (1, 6, 11):
            box(m, 1, 3, sz, 15, 5, sz + 3, WOOD_TIE)
        box(m, 3, 4, 0, 5, 7, 16, BRIGHT_STEEL)
        box(m, 11, 4, 0, 13, 7, 16, BRIGHT_STEEL)
        if bid == 86:
            box(m, 7, 3, 0, 9, 6, 16, COPPER_BUS)
    elif bid == 87:
        # 3D C-4 Demolition Charge pack (olive bricks, black straps, keypad & glowing red timer LED)
        box(m, 1, 0, 2, 15, 9, 14, OLIVE_C4)
        box(m, 1, 0, 4, 15, 10, 6, DARK_STEEL)
        box(m, 1, 0, 10, 15, 10, 12, DARK_STEEL)
        box(m, 4, 9, 5, 12, 12, 11, DARK_STEEL)
        box(m, 5, 12, 6, 10, 13, 10, RED_ALERT)
        box(m, 11, 9, 7, 12, 16, 8, BRIGHT_STEEL)
    elif bid == 75:
        # 3D Solar Array block with dark steel pedestal & tilted photovoltaic wafer panel
        box(m, 2, 0, 2, 14, 4, 14, DARK_STEEL)
        box(m, 6, 4, 6, 10, 9, 10, BRIGHT_STEEL)
        box(m, 0, 8, 0, 16, 12, 16, DARK_STEEL)
        box(m, 1, 11, 1, 15, 13, 15, SOLAR_BLUE)
    elif bid == 76:
        # 3D VVER Nuclear Reactor Vessel with pressure dome, Cherenkov window & cooling pipes
        box(m, 1, 0, 1, 15, 14, 15, MAIN_P)
        box(m, 3, 3, 0, 13, 11, 16, CHER_CYAN)
        box(m, 0, 3, 3, 16, 11, 13, CHER_CYAN)
        box(m, 3, 14, 3, 13, 16, 13, DARK_STEEL)
        for cx in (2, 12):
            for cz in (2, 12):
                box(m, cx, 0, cz, cx + 2, 16, cz + 2, BRIGHT_STEEL)
    else:
        box(m, 1, 1, 1, 15, 15, 15, MAIN_P)
        if bid == 70:
            for vx, vy, vz in [(2, 3, 0), (10, 8, 1), (4, 11, 14), (0, 6, 7), (14, 5, 9)]:
                box(m, vx, vy, vz, min(16, vx + 3), min(16, vy + 3), min(16, vz + 2), RAD_GREEN)
        elif bid == 78:
            box(m, 0, 0, 0, 16, 16, 16, DARK_STEEL)
            box(m, 1, 1, 1, 15, 15, 15, CHER_CYAN)
            box(m, 4, 4, 4, 12, 12, 12, CHER_WHITE)
    sculpted.append(m)

# 190: Урановый топливный стержень (ТВЭЛ)
m = model(190, 'Урановый стержень ТВЭЛ')
box(m, 5, 0, 5, 11, 2, 11, DARK_STEEL)
box(m, 5, 2, 5, 11, 14, 11, RAD_GREEN)
box(m, 4, 7, 4, 12, 9, 12, BRIGHT_STEEL)
box(m, 5, 14, 5, 11, 16, 11, DARK_STEEL)
sculpted.append(m)

# 191: Ядро Черенкова
m = model(191, 'Ядро Черенкова')
box(m, 3, 1, 3, 13, 15, 13, CHER_CYAN)
box(m, 1, 4, 4, 15, 12, 12, CHER_WHITE)
box(m, 4, 4, 1, 12, 12, 15, CHER_WHITE)
box(m, 2, 0, 2, 14, 2, 14, DARK_STEEL)
box(m, 2, 14, 2, 14, 16, 14, DARK_STEEL)
sculpted.append(m)

# 192: Электровоз «ЭР-Воксель» (3D mini-locomotive model with wheels, cab, headlight & pantograph)
m = model(192, 'Электровоз «ЭР-Воксель»')
box(m, 2, 2, 0, 14, 5, 16, DARK_STEEL)
for wz in (2, 11):
    box(m, 1, 0, wz, 4, 3, wz + 3, BRIGHT_STEEL)
    box(m, 12, 0, wz, 15, 3, wz + 3, BRIGHT_STEEL)
box(m, 2, 5, 1, 14, 12, 15, SOLAR_BLUE)
box(m, 2, 6, 1, 14, 8, 15, HAZARD_YLW)
box(m, 3, 9, 13, 13, 12, 16, CHER_WHITE)
box(m, 6, 12, 4, 10, 15, 11, COPPER_BUS)
sculpted.append(m)

# 193: Инженерный квадрокоптер (Дрон)
m = model(193, 'Инженерный квадрокоптер')
box(m, 4, 4, 4, 12, 10, 12, DARK_STEEL)
box(m, 6, 5, 12, 10, 9, 15, CHER_CYAN)
box(m, 1, 6, 6, 15, 9, 10, BRIGHT_STEEL)
box(m, 6, 6, 1, 10, 9, 15, BRIGHT_STEEL)
for rx, rz in [(0, 0), (10, 0), (0, 10), (10, 10)]:
    box(m, rx, 9, rz, rx + 6, 11, rz + 6, HAZARD_YLW)
sculpted.append(m)

# 194: Шагающий робот-автоматон «Титан-М»
m = model(194, 'Робот-автоматон «Титан-М»')
box(m, 4, 0, 5, 7, 6, 11, DARK_STEEL)
box(m, 9, 0, 5, 12, 6, 11, DARK_STEEL)
box(m, 3, 6, 4, 13, 13, 12, BRIGHT_STEEL)
box(m, 1, 7, 5, 3, 12, 11, HAZARD_YLW)
box(m, 13, 7, 5, 15, 12, 11, HAZARD_YLW)
box(m, 5, 12, 5, 11, 16, 11, DARK_STEEL)
box(m, 5, 13, 10, 11, 15, 12, CHER_CYAN)
sculpted.append(m)

# 195: Дозиметр-радиометр «Припять»
m = model(195, 'Дозиметр «Припять»')
box(m, 3, 1, 4, 13, 15, 11, HAZARD_YLW)
box(m, 4, 9, 10, 12, 14, 12, DARK_STEEL)
box(m, 5, 10, 11, 11, 13, 13, RAD_GREEN)
box(m, 5, 3, 10, 11, 7, 12, RED_ALERT)
sculpted.append(m)

# 666: СЕКРЕТНЫЙ ОБЪЕКТ 666 · ТЕРМОЯДЕРНАЯ БОЕГОЛОВКА (hidden everywhere except /give 666!)
m = model(666, 'Объект 666 · Термоядерная боеголовка')
box(m, 2, 0, 2, 14, 3, 14, HAZARD_YLW)
box(m, 3, 3, 3, 13, 11, 13, pal('#202228', 'thermonuke-666'))
box(m, 4, 11, 4, 12, 14, 12, RED_ALERT)
box(m, 6, 14, 6, 10, 16, 10, HAZARD_YLW)
for fx in (1, 13):
    box(m, fx, 0, 5, fx + 2, 8, 11, RED_ALERT)
sculpted.append(m)

new_ids = {m['id'] for m in sculpted}
d['models'] = [m for m in d['models'] if m['id'] not in new_ids]
for m in sculpted:
    icon = R / f'Assets/Resources/VoxelIcons/Item{m["id"]}.png'
    render(m).quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE).save(icon, optimize=True)
    meta = icon.with_name(icon.name + '.meta')
    if not meta.exists():
        guid = hashlib.md5(f'LestingCraft-VoxelIcon-{m["id"]}'.encode()).hexdigest()
        meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\n', encoding='utf-8')
    cells = []
    for (x, y, z), mat in sorted(m['voxels'].items()):
        cells.extend([x, y, z, mat])
    assert len(cells) > 1000, f'Model {m["id"]} too sparse: {len(cells)} <= 1000'
    d['models'].append(dict(id=m['id'], name=m['name'], cells=cells))

d['models'].sort(key=lambda item: item['id'])
d['colors'] = [c for c, l in palette]
d['layers'] = [l for c, l in palette]
save_catalog(R, d)
print(f'SUCCESS: {len(ALL_NAMES)} PBR layers, {len(all_blocks)} ExpandedBlocks (19..89), {len(d["models"])} VoxelModels + 384px icons (including hidden #666)!')
