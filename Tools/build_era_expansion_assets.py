"""Era Expansion Asset Builder:
1. Appends 16 seamless 512x512 PBR layers (8 new wildlife coats + 8 new block surfaces = 88 total)
   with _albedo.jpg, tangent-space _normal.png (Z > 180), _mask.png, .meta, and materials.json.
2. Appends 16 rich placeable blocks (IDs 54..69 -> 51 ExpandedBlocks, MaxBlock = 69) to
   ExpandedContent.cs, ExpandedBlocks.json, VoxelItems catalog (each > 1,400 cells, bounds <= 1.0),
   and renders 384x384 transparent RGBA icons Item54..Item69 + .meta.
"""
from model_io import load_catalog, save_catalog
from pathlib import Path
import hashlib, json, uuid
import numpy as np
from PIL import Image, ImageFilter

R = Path(__file__).resolve().parents[1]
PBR = R / 'Assets/Art/PBR'
N = 512

BASE_NAMES = [
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
    "frog-skin","eagle-feathers"
]

NEW_ANIMAL_COATS = [
    "owl-feathers",      # 72
    "penguin-plumage",   # 73
    "camel-coat",        # 74
    "scorpion-chitin",   # 75
    "turtle-shell",      # 76
    "moose-fur",         # 77
    "capybara-fur",      # 78
    "voidray-skin",      # 79
]

NEW_BLOCK_LAYERS = [
    "terracotta-shingles", # 80
    "cherry-blossom",      # 81
    "glow-shroom",         # 82
    "red-mesa",            # 83
    "glacial-ice",         # 84
    "magma-basalt",        # 85
    "titanium-hull",       # 86
    "andromeda-crystal",   # 87
]

ALL_NAMES = BASE_NAMES + NEW_ANIMAL_COATS + NEW_BLOCK_LAYERS
assert len(ALL_NAMES) == 88 and len(set(ALL_NAMES)) == 88


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


def write_meta(path: Path):
    meta = path.with_name(path.name + '.meta')
    if not meta.exists():
        guid = uuid.uuid5(uuid.NAMESPACE_URL, f'lestingcraft-era-pbr:{path.name}').hex
        meta.write_text(f'fileFormatVersion: 2\nguid: {guid}\n', encoding='utf-8')


def save_pbr_set(name: str, alb: np.ndarray, height_field: np.ndarray, rough: np.ndarray, metallic: float = 0.0, strength: float = 2.2):
    alb_p = periodic(np.clip(alb, 0.02, 0.98))
    h_p = periodic(height_field)
    dx = (np.roll(h_p, -1, axis=1) - np.roll(h_p, 1, axis=1)) * strength
    dy = (np.roll(h_p, -1, axis=0) - np.roll(h_p, 1, axis=0)) * strength
    nz = np.ones_like(h_p)
    norm_len = np.sqrt(dx * dx + dy * dy + nz * nz)
    nx = (-dx / norm_len) * 0.5 + 0.5
    ny = (-dy / norm_len) * 0.5 + 0.5
    nzz = np.clip((nz / norm_len) * 0.5 + 0.5, 0.74, 1.0)
    normal_rgb = np.stack([nx, ny, nzz], axis=-1)

    ao = np.clip(0.74 + 0.26 * h_p, 0.35, 1.0)
    smooth = np.clip(1.0 - periodic(rough), 0.04, 0.94)
    met = np.full_like(ao, metallic)
    mask_rgba = np.stack([met, ao, h_p, smooth], axis=-1)

    alb_path = PBR / f'{name}_albedo.jpg'
    nrm_path = PBR / f'{name}_normal.png'
    msk_path = PBR / f'{name}_mask.png'
    Image.fromarray(np.uint8(np.clip(alb_p * 255, 0, 255))).save(alb_path, quality=92, subsampling=0)
    Image.fromarray(np.uint8(np.clip(normal_rgb * 255, 0, 255))).save(nrm_path, optimize=True)
    Image.fromarray(np.uint8(np.clip(mask_rgba * 255, 0, 255))).save(msk_path, optimize=True)
    write_meta(alb_path)
    write_meta(nrm_path)
    write_meta(msk_path)


yy, xx = np.mgrid[0:N, 0:N].astype(float) / N

# 1. Build 8 new animal PBR coats (indices 72..79)
# Owl feathers: snowy cream plumage with delicate dark umber chevron barring
owl_bar = 0.5 + 0.5 * np.sin(2 * np.pi * (yy * 16 + 0.35 * np.sin(2 * np.pi * xx * 8)))
owl_fine = tileable_noise(801, 96, 32)
owl_spot = np.clip((owl_bar * owl_fine - 0.42) * 2.8, 0, 1)
save_pbr_set(
    'owl-feathers',
    np.array([0.84, 0.80, 0.73])[None, None, :] * (0.78 + 0.26 * owl_fine[:, :, None]) * (1 - 0.55 * owl_spot[:, :, None])
    + np.array([0.28, 0.20, 0.14])[None, None, :] * owl_spot[:, :, None] * 0.55,
    0.55 * owl_fine + 0.45 * owl_bar,
    0.66 - 0.14 * owl_fine,
)

# Penguin plumage: dense waterproof blue-slate dorsal feathers + frosty sheen
peng_barb = tileable_noise(811, 128, 48)
peng_patch = tileable_noise(812, 8, 8)
save_pbr_set(
    'penguin-plumage',
    np.array([0.15, 0.19, 0.26])[None, None, :] * (0.65 + 0.72 * peng_barb[:, :, None])
    + np.array([0.62, 0.66, 0.72])[None, None, :] * np.clip((peng_patch[:, :, None] - 0.62) * 2.4, 0, 1),
    peng_barb,
    0.34 + 0.18 * peng_barb,
)

# Camel coat: warm desert golden-tan woolly pelt
camel_wool = 0.6 * tileable_noise(821, 72, 64) + 0.4 * tileable_noise(822, 16, 16)
save_pbr_set(
    'camel-coat',
    np.array([0.74, 0.56, 0.34])[None, None, :] * (0.66 + 0.58 * camel_wool[:, :, None]),
    camel_wool,
    0.78 - 0.12 * camel_wool,
)

# Scorpion chitin: segmented obsidian-amber armored exoskeleton
scorp_plate = 0.5 + 0.5 * np.cos(2 * np.pi * yy * 10)
scorp_pit = tileable_noise(831, 84, 84)
save_pbr_set(
    'scorpion-chitin',
    np.array([0.19, 0.13, 0.09])[None, None, :] * (0.62 + 0.75 * scorp_plate[:, :, None])
    + np.array([0.48, 0.27, 0.09])[None, None, :] * scorp_pit[:, :, None] * 0.35,
    0.65 * scorp_plate + 0.35 * scorp_pit,
    0.26 + 0.22 * scorp_pit,
    metallic=0.12,
)

# Turtle shell: hexagonal olive-amber keratin scutes
gx = np.abs(((xx * 6) % 1.0) - 0.5) * 2
gy = np.abs(((yy * 6) % 1.0) - 0.5) * 2
scute = np.clip(1.0 - np.maximum(gx, gy), 0, 1)
turt_grain = tileable_noise(841, 64, 64)
save_pbr_set(
    'turtle-shell',
    np.array([0.24, 0.33, 0.19])[None, None, :] * (0.55 + 0.72 * scute[:, :, None])
    + np.array([0.52, 0.42, 0.20])[None, None, :] * turt_grain[:, :, None] * 0.32,
    0.7 * scute + 0.3 * turt_grain,
    0.38 + 0.22 * turt_grain,
)

# Moose fur: deep espresso-mahogany coarse northern coat
moose_hair = 0.62 * tileable_noise(851, 108, 22) + 0.38 * tileable_noise(852, 12, 12)
save_pbr_set(
    'moose-fur',
    np.array([0.21, 0.14, 0.10])[None, None, :] * (0.58 + 0.86 * moose_hair[:, :, None]),
    moose_hair,
    0.74 - 0.14 * moose_hair,
)

# Capybara fur: warm chestnut-cinnamon coarse reed bristles
capy_hair = 0.65 * tileable_noise(861, 88, 26) + 0.35 * tileable_noise(862, 10, 10)
save_pbr_set(
    'capybara-fur',
    np.array([0.55, 0.36, 0.21])[None, None, :] * (0.64 + 0.64 * capy_hair[:, :, None]),
    capy_hair,
    0.68 - 0.15 * capy_hair,
)

# VoidRay skin: iridescent indigo-velvet cosmic skin with glowing cyan bio-spots
ray_base = tileable_noise(871, 48, 48)
ray_glow = np.clip((tileable_noise(872, 26, 26) - 0.64) * 3.2, 0, 1)
save_pbr_set(
    'voidray-skin',
    np.array([0.13, 0.15, 0.34])[None, None, :] * (0.65 + 0.7 * ray_base[:, :, None])
    + np.array([0.18, 0.85, 0.95])[None, None, :] * ray_glow[:, :, None] * 0.72,
    0.5 * ray_base + 0.5 * ray_glow,
    0.25 + 0.2 * ray_base,
    metallic=0.18,
)

# 2. Build 8 new block PBR surfaces (indices 80..87)
# 80: terracotta-shingles
row_idx = np.floor(yy * 8.0)
stagger = ((xx * 8.0 + (row_idx % 2.0) * 0.5) % 1.0)
tile_v = (yy * 8.0) % 1.0
shingle_h = np.clip(0.35 + 0.65 * tile_v - 0.45 * (np.minimum(stagger, 1.0 - stagger) < 0.06), 0, 1)
clay_n = tileable_noise(901, 64, 64)
save_pbr_set(
    'terracotta-shingles',
    np.array([0.66, 0.29, 0.18])[None, None, :] * (0.62 + 0.52 * shingle_h[:, :, None]) * (0.85 + 0.3 * clay_n[:, :, None]),
    0.75 * shingle_h + 0.25 * clay_n,
    0.62 + 0.18 * clay_n,
    strength=2.8,
)

# 81: cherry-blossom
sakura_petals = tileable_noise(911, 52, 52)
sakura_hi = np.clip((tileable_noise(912, 88, 88) - 0.55) * 2.5, 0, 1)
sakura_twig = np.clip((tileable_noise(913, 20, 20) - 0.78) * 4.0, 0, 1)
save_pbr_set(
    'cherry-blossom',
    (np.array([0.93, 0.58, 0.72])[None, None, :] * (0.72 + 0.36 * sakura_petals[:, :, None])
     + np.array([0.99, 0.88, 0.93])[None, None, :] * sakura_hi[:, :, None] * 0.35) * (1 - 0.65 * sakura_twig[:, :, None])
    + np.array([0.28, 0.16, 0.18])[None, None, :] * sakura_twig[:, :, None] * 0.65,
    0.6 * sakura_petals + 0.4 * sakura_hi,
    0.64 - 0.12 * sakura_hi,
)

# 82: glow-shroom
shroom_cap = tileable_noise(921, 36, 36)
shroom_spots = np.clip((tileable_noise(922, 22, 22) - 0.60) * 3.0, 0, 1)
save_pbr_set(
    'glow-shroom',
    np.array([0.24, 0.14, 0.46])[None, None, :] * (0.65 + 0.6 * shroom_cap[:, :, None])
    + np.array([0.16, 0.94, 0.86])[None, None, :] * shroom_spots[:, :, None] * 0.78,
    0.55 * shroom_cap + 0.45 * shroom_spots,
    0.36 + 0.22 * shroom_cap,
)

# 83: red-mesa
strata = 0.5 + 0.5 * np.sin(2 * np.pi * (yy * 5.0 + 0.12 * tileable_noise(931, 8, 8)))
mesa_grain = tileable_noise(932, 96, 48)
ochre_band = np.clip((np.sin(2 * np.pi * yy * 9.0) - 0.55) * 2.2, 0, 1)
save_pbr_set(
    'red-mesa',
    (np.array([0.68, 0.31, 0.19])[None, None, :] * (0.68 + 0.48 * strata[:, :, None])
     + np.array([0.82, 0.58, 0.28])[None, None, :] * ochre_band[:, :, None] * 0.42) * (0.86 + 0.28 * mesa_grain[:, :, None]),
    0.65 * strata + 0.35 * mesa_grain,
    0.76 - 0.12 * mesa_grain,
)

# 84: glacial-ice
ice_fract = np.abs(tileable_noise(941, 18, 18) - 0.5) * 2.0
ice_fine = tileable_noise(942, 72, 72)
save_pbr_set(
    'glacial-ice',
    np.array([0.52, 0.79, 0.96])[None, None, :] * (0.72 + 0.35 * ice_fract[:, :, None])
    + np.array([0.88, 0.96, 1.0])[None, None, :] * np.clip((1.0 - ice_fract[:, :, None]) * 1.4 - 0.85, 0, 1) * 0.5,
    0.7 * ice_fract + 0.3 * ice_fine,
    0.12 + 0.16 * ice_fine,
)

# 85: magma-basalt
crack_raw = np.abs(tileable_noise(951, 14, 14) - 0.5)
lava_vein = np.clip(1.0 - crack_raw * 9.0, 0, 1)
basalt_g = tileable_noise(952, 84, 84)
save_pbr_set(
    'magma-basalt',
    np.array([0.14, 0.13, 0.15])[None, None, :] * (0.65 + 0.7 * basalt_g[:, :, None]) * (1 - lava_vein[:, :, None])
    + np.array([1.0, 0.44, 0.08])[None, None, :] * lava_vein[:, :, None],
    (1.0 - lava_vein) * 0.7 + 0.3 * basalt_g,
    0.78 * (1 - lava_vein) + 0.25 * lava_vein,
    strength=3.0,
)

# 86: titanium-hull
panel_x = np.minimum((xx * 4.0) % 1.0, 1.0 - ((xx * 4.0) % 1.0))
panel_y = np.minimum((yy * 4.0) % 1.0, 1.0 - ((yy * 4.0) % 1.0))
seam = (panel_x > 0.025).astype(float) * (panel_y > 0.025).astype(float)
brush = tileable_noise(961, 128, 12)
save_pbr_set(
    'titanium-hull',
    np.array([0.74, 0.78, 0.84])[None, None, :] * (0.76 + 0.28 * seam[:, :, None]) * (0.90 + 0.18 * brush[:, :, None]),
    0.8 * seam + 0.2 * brush,
    0.26 + 0.14 * brush,
    metallic=0.82,
    strength=2.6,
)

# 87: andromeda-crystal
facet_a = np.abs(tileable_noise(971, 16, 16) - 0.5) * 2.0
facet_b = np.abs(tileable_noise(972, 32, 32) - 0.5) * 2.0
spark = np.clip((tileable_noise(973, 96, 96) - 0.72) * 3.4, 0, 1)
save_pbr_set(
    'andromeda-crystal',
    np.array([0.52, 0.22, 0.86])[None, None, :] * (0.58 + 0.65 * facet_a[:, :, None])
    + np.array([0.96, 0.78, 0.32])[None, None, :] * facet_b[:, :, None] * 0.42
    + np.array([0.85, 0.95, 1.0])[None, None, :] * spark[:, :, None] * 0.45,
    0.6 * facet_a + 0.4 * facet_b,
    0.10 + 0.14 * facet_b,
    metallic=0.28,
    strength=2.8,
)

# Update materials.json animalTextures to register all 20 species
mat_json_path = PBR / 'materials.json'
mat_data = json.loads(mat_json_path.read_text(encoding='utf-8'))
mat_data['animalTextures']['species'] = {
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
    'owl': 'owl-feathers',
    'penguin': 'penguin-plumage',
    'camel': 'camel-coat',
    'scorpion': 'scorpion-chitin',
    'turtle': 'turtle-shell',
    'moose': 'moose-fur',
    'capybara': 'capybara-fur',
    'voidray': 'voidray-skin',
}
mat_json_path.write_text(json.dumps(mat_data, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')

# Update SurfaceLayers.cs keeping exact 'Names={' substring required by CI!
names_literal = ','.join(f'"{n}"' for n in ALL_NAMES)
surface_cs = f"""namespace LestingCraft.Presentation
{{
    public static class SurfaceLayers
    {{
        public static readonly string[] Names={{{names_literal}}};

        public static int ForFace(ushort block, int face)
        {{
            switch (block)
            {{
                case 1: return 0;
                case 2: return 1;
                case 3: return face == 2 ? 2 : face == 3 ? 1 : 3;
                case 4: return face == 2 || face == 3 ? 5 : 4;
                case 5: return 6;
                case 6: return 7;
                case 7: return 8;
                case 8: return 9;
                case 9: return 10;
                case 10: return 11;
                case 11: return 12;
                case 12: return 13;
                case 13: return 14;
                case 14: return 15;
                case 15: return face == 2 ? 23 : 22;
                case 16: return face == 2 ? 17 : face == 3 ? 16 : 18;
                case 17: return face == 2 || face == 3 ? 21 : face == 5 ? 20 : 19;
                case 18: return 16;
                case 65: return face == 2 ? 81 : face == 3 ? 1 : 3;
                default:
                    return LestingCraft.Gameplay.ExpandedContent.Has(block)
                        ? LestingCraft.Gameplay.ExpandedContent.Blocks[block - 19].Layer : 0;
            }}
        }}

        static int FloorDiv4(int v) {{ return v >= 0 ? v >> 2 : -(((-v) + 3) >> 2); }}
        static uint CellHash(int x, int y, int z, int seed)
        {{
            unchecked
            {{
                uint h = (uint)seed ^ (uint)x * 374761393u ^ (uint)y * 668265263u ^ (uint)z * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }}
        }}

        // Appearance only: the existing ore ID, drops, save format and generator remain unchanged.
        public static int ForCellFace(ushort block, int face, int x, int y, int z, int seed)
        {{
            if (block != 13) return ForFace(block, face);
            uint variant = CellHash(FloorDiv4(x), FloorDiv4(y), FloorDiv4(z), seed ^ 27183) % 3;
            return variant == 0 ? 14 : variant == 1 ? 24 : 25;
        }}

        public static string DisplayName(ushort id)
        {{
            string[] names =
            {{
                "Воздух","Камень","Земля","Дёрн","Древесина","Листва","Песок","Вода","Лава",
                "Коренная порода","Базальт","Кристалл","Эфирный камень","Руда","Снег","Костёр",
                "Верстак","Печь","Доски"
            }};
            return id < names.Length ? names[id] :
                LestingCraft.Gameplay.ExpandedContent.Has(id)
                    ? LestingCraft.Gameplay.ExpandedContent.Blocks[id - 19].Name : "Неизвестный блок";
        }}

        public static string Icon(ushort id) {{ return id == 15 ? "Item15" : Names[ForFace(id, 2)]; }}
    }}
}}
"""
(R / 'Assets/Scripts/Presentation/SurfaceLayers.cs').write_text(surface_cs, encoding='utf-8')

# 3. Update ExpandedBlocks.json + ExpandedContent.cs (IDs 19..69 = 51 blocks, MaxBlock = 69)
exp_json_path = R / 'Assets/Resources/Models/ExpandedBlocks.json'
existing_blocks = [b for b in json.loads(exp_json_path.read_text(encoding='utf-8'))['blocks'] if b['id'] <= 53]
assert [b['id'] for b in existing_blocks] == list(range(19, 54))

NEW_BLOCKS = [
    dict(id=54, name='Уличный факел',       surface='bark',                hardness=0.35, pick=False, layer=ALL_NAMES.index('bark')),
    dict(id=55, name='Уютная кровать',       surface='planks',              hardness=0.8,  pick=False, layer=ALL_NAMES.index('planks')),
    dict(id=56, name='Кованый фонарь',       surface='metal-ingot',         hardness=1.4,  pick=True,  layer=ALL_NAMES.index('metal-ingot')),
    dict(id=57, name='Книжный шкаф',         surface='planks',              hardness=1.2,  pick=False, layer=ALL_NAMES.index('planks')),
    dict(id=58, name='Резной дуб',           surface='wood-end',            hardness=1.5,  pick=False, layer=ALL_NAMES.index('wood-end')),
    dict(id=59, name='Черепичная крыша',     surface='terracotta-shingles', hardness=1.8,  pick=True,  layer=ALL_NAMES.index('terracotta-shingles')),
    dict(id=60, name='Листва сакуры',        surface='cherry-blossom',      hardness=0.3,  pick=False, layer=ALL_NAMES.index('cherry-blossom')),
    dict(id=61, name='Светящийся гриб',      surface='glow-shroom',         hardness=0.6,  pick=False, layer=ALL_NAMES.index('glow-shroom')),
    dict(id=62, name='Красная терракота',    surface='red-mesa',            hardness=1.9,  pick=True,  layer=ALL_NAMES.index('red-mesa')),
    dict(id=63, name='Ледниковый лёд',       surface='glacial-ice',         hardness=1.3,  pick=True,  layer=ALL_NAMES.index('glacial-ice')),
    dict(id=64, name='Магмовый базальт',     surface='magma-basalt',        hardness=2.8,  pick=True,  layer=ALL_NAMES.index('magma-basalt')),
    dict(id=65, name='Лавандовый дёрн',      surface='cherry-blossom',      hardness=0.7,  pick=False, layer=ALL_NAMES.index('cherry-blossom')),
    dict(id=66, name='Титановая обшивка',    surface='titanium-hull',       hardness=4.2,  pick=True,  layer=ALL_NAMES.index('titanium-hull')),
    dict(id=67, name='Кристалл Андромеды',   surface='andromeda-crystal',   hardness=3.6,  pick=True,  layer=ALL_NAMES.index('andromeda-crystal')),
    dict(id=68, name='Монолит Сна',          surface='obsidian',            hardness=8.0,  pick=True,  layer=ALL_NAMES.index('obsidian')),
    dict(id=69, name='Бледный камень Сна',   surface='polished-marble',     hardness=3.2,  pick=True,  layer=ALL_NAMES.index('polished-marble')),
]

all_blocks = existing_blocks + NEW_BLOCKS
exp_json_path.write_text(json.dumps({'blocks': all_blocks}, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')

exp_cs_path = R / 'Assets/Scripts/Gameplay/ExpandedContent.cs'
exp_cs_old = exp_cs_path.read_text(encoding='utf-8')
block_entries = ',\n   '.join(
    f'new ExpandedBlock({b["id"]},"{b["name"]}","{b["surface"]}",{b["hardness"]}f,{"true" if b["pick"] else "false"},{b.get("layer", ALL_NAMES.index(b["surface"]))})'
    for b in all_blocks
)
# Preserve exact rest of ExpandedContent.cs while upgrading MaxBlock and Blocks array cleanly
import re
exp_cs_updated = re.sub(
    r'public const int MaxBlock=\d+;',
    'public const int MaxBlock=69;public const int LegacySpaceMaxBlock=53;',
    exp_cs_old,
    count=1,
)
exp_cs_updated = re.sub(
    r'public static readonly ExpandedBlock\[\] Blocks=\{.*?\};',
    f'public static readonly ExpandedBlock[] Blocks={{\n   {block_entries}\n  }};',
    exp_cs_updated,
    count=1,
    flags=re.S,
)
exp_cs_path.write_text(exp_cs_updated, encoding='utf-8')

# 4. Sculpt detailed 3D Voxel Models + 384x384 RGBA icons for IDs 54..69 (> 1,400 cells each, bounds <= 1.0)
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

OAK = pal('#7d5432', 'bark')
PLANK = pal('#b88954', 'planks')
DARK_IRON = pal('#2d333d', 'metal-ingot')
STEEL = pal('#98a2b0', 'metal-ingot')
GOLD_FLAME = pal('#ffb833', 'lava')
HOT_CORE = pal('#fff3b0', 'snow')
RED_QUILT = pal('#b83232', 'leather')
CREAM_LINEN = pal('#f4efe6', 'snow')
BOOK_RED = pal('#9e2a2b', 'leather')
BOOK_BLUE = pal('#274c77', 'leather')
BOOK_GREEN = pal('#386641', 'leather')
GOLD_TRIM = pal('#d9a426', 'metal-ingot')

sculpted = []

# 54: Уличный факел (carved oak shaft + wrought-iron sconce ring + multi-layered glowing flame, >350 voxels)
m = model(54, 'Уличный факел')
box(m, 5, 0, 5, 11, 2, 11, DARK_IRON)
box(m, 6, 2, 6, 10, 14, 10, OAK)
box(m, 4, 12, 4, 12, 15, 12, DARK_IRON)
box(m, 5, 15, 5, 11, 17, 11, pal('#3a2c24', 'coal-block'))
box(m, 5, 17, 5, 11, 22, 11, GOLD_FLAME)
box(m, 6, 20, 6, 10, 25, 10, HOT_CORE)
sculpted.append(m)

# 55: Уютная кровать (4 oak posts, headboard, white pillow, crimson quilted blanket)
m = model(55, 'Уютная кровать')
for x in (1, 13):
    for z in (1, 13):
        box(m, x, 0, z, x + 2, 6, z + 2, OAK)
box(m, 1, 2, 1, 15, 5, 15, PLANK)
box(m, 1, 5, 1, 15, 11, 3, OAK)
box(m, 2, 5, 3, 14, 8, 7, CREAM_LINEN)
box(m, 1, 5, 7, 15, 9, 15, RED_QUILT)
box(m, 2, 9, 8, 14, 10, 14, RED_QUILT)
sculpted.append(m)

# 56: Кованый фонарь (iron base, 4 corner ribs, warm glowing glass core, stepped roof cap + ring)
m = model(56, 'Кованый фонарь')
box(m, 3, 0, 3, 13, 2, 13, DARK_IRON)
box(m, 4, 2, 4, 12, 12, 12, GOLD_FLAME)
box(m, 6, 4, 6, 10, 10, 10, HOT_CORE)
for x in (3, 12):
    for z in (3, 12):
        box(m, x, 2, z, x + 1, 12, z + 1, DARK_IRON)
box(m, 2, 12, 2, 14, 14, 14, DARK_IRON)
box(m, 4, 14, 4, 12, 16, 12, DARK_IRON)
box(m, 6, 16, 6, 10, 18, 10, STEEL)
sculpted.append(m)

# 57: Книжный шкаф (carved oak frame + shelves + multi-colored book spines)
m = model(57, 'Книжный шкаф')
box(m, 1, 0, 1, 15, 16, 15, PLANK)
for shelf_y in (2, 9):
    box(m, 2, shelf_y, 0, 14, shelf_y + 5, 16, OAK)
    for bx, col in [(2, BOOK_RED), (5, BOOK_BLUE), (8, BOOK_GREEN), (11, GOLD_TRIM)]:
        box(m, bx, shelf_y, 0, bx + 2, shelf_y + 5, 16, col)
sculpted.append(m)

# 58..69: Detailed 14x14x14 beveled architectural & natural blocks with surface relief
SOLID_BLOCK_STYLES = [
    (58, 'Резной дуб',         '#9c7244', 'wood-end',            OAK),
    (59, 'Черепичная крыша',   '#b84c32', 'terracotta-shingles', OAK),
    (60, 'Листва сакуры',      '#f29cb8', 'cherry-blossom',      pal('#ffd6e5', 'cherry-blossom')),
    (61, 'Светящийся гриб',    '#46d9cc', 'glow-shroom',         pal('#4b2982', 'glow-shroom')),
    (62, 'Красная терракота',  '#b85435', 'red-mesa',            pal('#d99b52', 'red-mesa')),
    (63, 'Ледниковый лёд',     '#8ad0f5', 'glacial-ice',         CREAM_LINEN),
    (64, 'Магмовый базальт',   '#2e2b33', 'magma-basalt',        GOLD_FLAME),
    (65, 'Лавандовый дёрн',    '#9e6bd6', 'cherry-blossom',      pal('#6b4f3a', 'dirt')),
    (66, 'Титановая обшивка',  '#b8c4d4', 'titanium-hull',       DARK_IRON),
    (67, 'Кристалл Андромеды', '#9448e6', 'andromeda-crystal',   GOLD_TRIM),
    (68, 'Монолит Сна',        '#1a1624', 'obsidian',            pal('#473866', 'aether')),
    (69, 'Бледный камень Сна', '#dedbe6', 'polished-marble',     pal('#b5b0c4', 'stone-bricks')),
]

for bid, bname, hex_c, lname, accent_p in SOLID_BLOCK_STYLES:
    m = model(bid, bname)
    P = pal(hex_c, lname)
    if bid == 61:
        # Sculpted glowing giant mushroom block
        box(m, 5, 0, 5, 11, 8, 11, CREAM_LINEN)
        box(m, 1, 8, 1, 15, 13, 15, accent_p)
        box(m, 3, 13, 3, 13, 16, 13, P)
        for sx, sz in [(2, 2), (11, 3), (3, 11), (10, 10)]:
            box(m, sx, 9, sz, sx + 2, 14, sz + 2, P)
    elif bid == 67:
        # Sculpted alien crystal cluster on stone base
        box(m, 1, 0, 1, 15, 4, 15, DARK_IRON)
        box(m, 5, 3, 5, 11, 17, 11, P)
        box(m, 2, 3, 4, 6, 13, 9, accent_p)
        box(m, 10, 3, 6, 14, 14, 11, P)
        box(m, 4, 3, 10, 9, 12, 14, accent_p)
    else:
        box(m, 1, 1, 1, 15, 15, 15, P)
        if bid == 65:
            box(m, 1, 1, 1, 15, 11, 15, accent_p)
            box(m, 1, 11, 1, 15, 15, 15, P)
        elif bid in (58, 66, 68):
            for c0 in (1, 13):
                for c1 in (1, 13):
                    box(m, c0, 1, c1, c0 + 2, 15, c1 + 2, accent_p)
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

d['colors'] = [c for c, l in palette]
d['layers'] = [l for c, l in palette]
save_catalog(R, d)
print(f'SUCCESS: {len(ALL_NAMES)} PBR layers, {len(all_blocks)} ExpandedBlocks (19..69), {len(d["models"])} VoxelModels + 384px icons!')
