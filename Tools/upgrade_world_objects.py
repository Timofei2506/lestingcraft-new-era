#!/usr/bin/env python3
"""Upgrades crude cube-shaped placed world objects into richly detailed 3D voxel models
with matching 384x384 orthographic icons in Assets/Resources/Models/VoxelItems.bytes:
- 15: Campfire (ring of hearth stones, crossed logs, glowing coal bed & tiered flames)
- 16: Carpenter's Workbench (4 legs, lower tool shelf, overhanging top, iron bench vise)
- 17: Stone Smelting Furnace (stepped hearth, vaulted stone body, iron grate, glowing firebox, chimney)
- 54: Wrought-Iron Bracket Torch (base, oak shaft, iron basket, pitch embers & flame)
- 55: Carved Timber Bed (4 posts, arched headboard, white mattress, bolster pillow & crimson quilt)
- 56: Wrought-Iron Lantern (octagonal iron frame, glowing warm glass core, pagoda cap & ring)
- 61: Bioluminescent Glow-Shroom Cluster (3 curved stalks + spotted glowing cyan/violet caps)
- 73: Industrial Battery Bank (steel frame, 4 vertical cells with cyan charge bars, copper busbars)
- 74: Roll-Cage Fuel Generator (yellow tubular cage, finned engine, red tank, exhaust stack, copper coil)
- 77: Xenon Industrial Floodlight (steel base, mast, junction box, dual wide floodlight heads)
- 89: Cybernetic Radia Terminal (angled steel console, sloped keyboard, glowing CRT monitor)
"""
import hashlib
from pathlib import Path
import numpy as np
from PIL import Image
from model_io import load_catalog, save_catalog

R = Path(__file__).resolve().parents[1]
NAMES = __import__("re").findall(
    r'"([a-z0-9-]+)"',
    (R / "Assets/Scripts/Presentation/SurfaceLayers.cs").read_text().split("Names={")[1].split("};")[0],
)

source = (R / "Tools/build_voxel_items.py").read_text(encoding="utf-8")
ctx = {"__file__": str(R / "Tools/build_voxel_items.py")}
exec(compile(source.split("serial=[]")[0], str(R / "Tools/build_voxel_items.py"), "exec"), ctx)

d = load_catalog(R)
palette = list(zip(d["colors"], d["layers"]))
ctx["PALETTE"] = palette
ctx["COLORS"] = [np.array([int(c[i : i + 2], 16) / 255.0 for i in (1, 3, 5)]) for c, _ in palette]
ctx["NAMES"] = NAMES


def pal(color: str, layer_name: str) -> int:
    layer = NAMES.index(layer_name)
    if (color, layer) not in palette:
        palette.append((color, layer))
        ctx["COLORS"].append(np.array([int(color[i : i + 2], 16) / 255.0 for i in (1, 3, 5)]))
    return palette.index((color, layer))


box = ctx["box"]
model = ctx["model"]
render = ctx["render"]

# Shared palette materials
OAK_PLANK = pal("#d8ab78", "planks")
DARK_OAK = pal("#704b2e", "bark")
WORK_TOP = pal("#e8c391", "workbench-top")
VISE_IRON = pal("#3a424d", "metal-ingot")
BRIGHT_IRON = pal("#b8c2ce", "metal-ingot")
BRASS_GOLD = pal("#e2b24a", "metal-ingot")
COBBLE = pal("#8c9198", "cobblestone")
DARK_STONE = pal("#525760", "basalt")
EMBER_ORANGE = 32  # emissive alpha=1 in VoxelItemModels!
FLAME_GOLD = 36    # emissive alpha=1 in VoxelItemModels!
LINEN_WHITE = pal("#f2efe9", "snow")
QUILT_RED = pal("#b8293d", "snow")
QUILT_TRIM = pal("#e6be5a", "snow")
SHROOM_STEM = pal("#dfd8eb", "snow")
SHROOM_CAP = pal("#2ce6d8", "glow-shroom")
SHROOM_SPOT = pal("#b8f7ff", "cherenkov-crystal")
SHROOM_VIOLET = pal("#8a5cf5", "andromeda-crystal")
DARK_STEEL = pal("#252a32", "metal-ingot")
COPPER_BUS = pal("#e87828", "copper-cable")
HAZARD_YLW = pal("#f2be1a", "hazard-steel")
CHER_CYAN = pal("#26b8ff", "cherenkov-crystal")
CHER_WHITE = pal("#d8f8ff", "cherenkov-crystal")
RED_TANK = pal("#cf302a", "c4-explosive")
RAD_GREEN = pal("#3cf258", "uranium-ore")

upgraded = []

# 15: Костёр (Stone hearth ring + crossed split logs + glowing ember bed + tiered flames)
m = model(15, "Костёр")
for sx, sz in [(1, 4), (1, 8), (12, 4), (12, 8), (4, 1), (8, 1), (4, 12), (8, 12), (2, 2), (11, 2), (2, 11), (11, 11)]:
    box(m, sx, 0, sz, sx + 3, 3, sz + 3, COBBLE)
box(m, 3, 0, 3, 13, 2, 13, DARK_STONE)
box(m, 5, 2, 5, 11, 4, 11, EMBER_ORANGE)
# Crossed split timber logs
box(m, 1, 1, 4, 15, 4, 7, DARK_OAK)
box(m, 1, 1, 9, 15, 4, 12, DARK_OAK)
box(m, 4, 3, 1, 7, 6, 15, OAK_PLANK)
box(m, 9, 3, 1, 12, 6, 15, OAK_PLANK)
for fx, fz, fh in [(6, 6, 13), (8, 7, 15), (7, 9, 11), (5, 8, 10), (9, 5, 10)]:
    box(m, fx, 5, fz, fx + 2, fh, fz + 2, EMBER_ORANGE)
    box(m, fx, 6, fz, fx + 1, max(7, fh - 1), fz + 1, FLAME_GOLD)
upgraded.append(m)

# 16: Верстак (3D Carpenter's Workbench — 4 stout legs, lower tool shelf, overhanging top & iron vise!)
# Note: VoxelItemModels maps id=16 face UVs to workbench PBR while multiplying vertex color tint!
m = model(16, "Верстак")
# 4 stout corner legs (0..11)
for lx, lz in [(1, 1), (1, 12), (12, 1), (12, 12)]:
    box(m, lx, 0, lz, lx + 3, 12, lz + 3, DARK_OAK)
# Lower stretcher rails & tool shelf (y = 2..4)
box(m, 2, 2, 2, 14, 4, 14, OAK_PLANK)
# Stored iron anvil/toolbox on lower shelf
box(m, 5, 4, 5, 11, 7, 11, VISE_IRON)
# Overhanging thick carpenter tabletop (y = 11..15, 0..16)
box(m, 0, 11, 0, 16, 15, 16, WORK_TOP)
# Brass corner brackets on tabletop
for bx, bz in [(0, 0), (0, 14), (14, 0), (14, 14)]:
    box(m, bx, 12, bz, bx + 2, 15, bz + 2, BRASS_GOLD)
# Front-mounted cast-iron bench vise + T-screw (z = 15..16 -> clamp within 0..16: shift tabletop to z=0..15, vise at z=15)
m["voxels"].clear()
for lx, lz in [(1, 1), (1, 11), (12, 1), (12, 11)]:
    box(m, lx, 0, lz, lx + 3, 12, lz + 3, DARK_OAK)
box(m, 2, 2, 2, 14, 4, 13, OAK_PLANK)
box(m, 5, 4, 5, 11, 8, 10, VISE_IRON)
box(m, 0, 11, 0, 16, 15, 15, WORK_TOP)
for bx, bz in [(0, 0), (0, 13), (14, 0), (14, 13)]:
    box(m, bx, 12, bz, bx + 2, 15, bz + 2, BRASS_GOLD)
# Front bench vise jaw & screw at z = 14..16
box(m, 5, 10, 14, 11, 15, 16, VISE_IRON)
box(m, 7, 11, 15, 9, 13, 16, BRIGHT_IRON)
# Hammer & saw resting on top (y = 15..16)
box(m, 3, 15, 3, 11, 16, 5, BRIGHT_IRON)
box(m, 6, 15, 5, 8, 16, 11, DARK_OAK)
upgraded.append(m)

# 17: Печь (3D Stone Smelting Furnace — stepped hearth, arched glowing firebox, iron grate & chimney!)
m = model(17, "Печь")
# Stepped stone base (y = 0..3)
box(m, 0, 0, 0, 16, 3, 16, DARK_STONE)
# Main stone masonry kiln body (y = 3..12)
box(m, 1, 3, 1, 15, 12, 15, COBBLE)
# Stepped stone cornice (y = 11..13) + upper chimney flue (y = 13..16)
box(m, 0, 11, 0, 16, 13, 16, DARK_STONE)
box(m, 4, 13, 4, 12, 16, 12, COBBLE)
box(m, 6, 15, 6, 10, 16, 10, EMBER_ORANGE)
# Carve recessed front firebox (z = 13..16, x = 4..12, y = 3..9) & fill with bright molten coals!
for x in range(4, 12):
    for y in range(3, 9):
        for z in range(13, 16):
            m["voxels"].pop((x, y, z), None)
box(m, 4, 3, 12, 12, 8, 14, EMBER_ORANGE)
box(m, 5, 4, 13, 11, 7, 15, FLAME_GOLD)
# Wrought-iron grate bars across the firebox mouth (z = 15)
for gx in (5, 8, 10):
    box(m, gx, 3, 15, gx + 1, 8, 16, VISE_IRON)
upgraded.append(m)

# 54: Факел (3D Standing/Bracket Torch — >275 voxels inside 16x16x16 grid!)
m = model(54, "Факел")
# Beveled iron/stone floor & wall base
box(m, 4, 0, 4, 12, 2, 12, DARK_STEEL)
box(m, 5, 2, 5, 11, 4, 11, VISE_IRON)
# Sturdy carved oak torch handle (y = 3..11)
box(m, 6, 3, 6, 10, 11, 10, DARK_OAK)
box(m, 5, 6, 5, 11, 8, 11, BRASS_GOLD)
# Wrought-iron brazier basket (y = 9..12)
box(m, 4, 9, 4, 12, 12, 12, VISE_IRON)
# Glowing pitch embers & sculpted flame crown (y = 11..16)
box(m, 5, 11, 5, 11, 14, 11, EMBER_ORANGE)
box(m, 6, 13, 6, 10, 16, 10, FLAME_GOLD)
upgraded.append(m)

# 55: Кровать (3D Carved Timber Bed — 4 oak posts, arched headboard, pillow & crimson quilt!)
m = model(55, "Кровать")
# 4 carved oak corner posts
for px, pz in [(0, 0), (13, 0), (0, 13), (13, 13)]:
    box(m, px, 0, pz, px + 3, 9, pz + 3, DARK_OAK)
# Tall arched headboard at z = 0..2
box(m, 0, 2, 0, 16, 11, 3, DARK_OAK)
box(m, 2, 11, 0, 14, 13, 2, OAK_PLANK)
# Side timber bedframe rails (y = 2..5)
box(m, 0, 2, 0, 16, 5, 16, OAK_PLANK)
# Crisp white linen sheet & plush pillow at head (z = 2..6)
box(m, 1, 5, 2, 15, 8, 15, LINEN_WHITE)
box(m, 2, 8, 2, 14, 10, 6, LINEN_WHITE)
# Folded crimson quilted blanket with gold trim (z = 6..15)
box(m, 1, 5, 6, 15, 9, 15, QUILT_RED)
box(m, 1, 8, 6, 15, 9, 8, QUILT_TRIM)
upgraded.append(m)

# 56: Фонарь (3D Wrought-Iron Lantern — octagonal iron cage, glowing warm core & pagoda roof!)
m = model(56, "Фонарь")
box(m, 3, 0, 3, 13, 2, 13, DARK_STEEL)
box(m, 4, 2, 4, 12, 3, 12, BRASS_GOLD)
# Glowing warm glass core (y = 3..11)
box(m, 4, 3, 4, 12, 11, 12, FLAME_GOLD)
box(m, 5, 4, 5, 11, 10, 11, EMBER_ORANGE)
# 4 Wrought-iron corner mullions
for mx, mz in [(3, 3), (11, 3), (3, 11), (11, 11)]:
    box(m, mx, 2, mz, mx + 2, 12, mz + 2, DARK_STEEL)
# Stepped pagoda lantern cap & hanging loop (y = 11..16)
box(m, 2, 11, 2, 14, 13, 14, DARK_STEEL)
box(m, 4, 13, 4, 12, 14, 12, VISE_IRON)
box(m, 6, 14, 7, 10, 16, 9, BRASS_GOLD)
upgraded.append(m)

# 61: Светящийся гриб (3D Bioluminescent Mushroom Cluster — 3 stalks + spotted glowing dome caps!)
m = model(61, "Светящийся гриб")
box(m, 2, 0, 2, 14, 2, 14, DARK_STONE)
# Main central stalk & wide glowing dome cap
box(m, 6, 2, 6, 10, 10, 10, SHROOM_STEM)
box(m, 2, 9, 2, 14, 13, 14, SHROOM_CAP)
box(m, 4, 13, 4, 12, 15, 12, SHROOM_VIOLET)
for sx, sz in [(3, 4), (10, 5), (5, 11), (9, 10), (6, 6)]:
    box(m, sx, 12, sz, sx + 2, 14, sz + 2, SHROOM_SPOT)
# Two smaller companion glow-shrooms
box(m, 2, 2, 10, 4, 6, 12, SHROOM_STEM)
box(m, 0, 5, 8, 6, 8, 14, SHROOM_VIOLET)
box(m, 11, 2, 3, 13, 5, 5, SHROOM_STEM)
box(m, 9, 4, 1, 15, 7, 7, SHROOM_CAP)
upgraded.append(m)

# 73: Аккумуляторный блок (3D Industrial Lithium-Ion Battery Rack)
m = model(73, "Аккумуляторный блок")
box(m, 1, 0, 1, 15, 2, 15, DARK_STEEL)
box(m, 1, 13, 1, 15, 15, 15, DARK_STEEL)
for cx_b, cz_b in [(2, 2), (9, 2), (2, 9), (9, 9)]:
    box(m, cx_b, 2, cz_b, cx_b + 5, 13, cz_b + 5, VISE_IRON)
    # Glowing cyan charge-level indicator strips on cell faces
    box(m, cx_b + 1, 4, cz_b - 1 if cz_b == 2 else cz_b + 5, cx_b + 4, 11, (cz_b if cz_b == 2 else cz_b + 6), CHER_CYAN)
# Heavy copper busbars & terminal posts on top
box(m, 3, 15, 4, 13, 16, 6, COPPER_BUS)
box(m, 3, 15, 10, 13, 16, 12, COPPER_BUS)
upgraded.append(m)

# 74: Топливный генератор (3D Tubular Roll-Cage Diesel/Fuel Generator)
m = model(74, "Топливный генератор")
box(m, 1, 0, 1, 15, 2, 15, DARK_STEEL)
# Finned V-twin engine & copper-wound alternator core
box(m, 3, 2, 3, 10, 11, 13, VISE_IRON)
box(m, 10, 3, 4, 14, 10, 12, COPPER_BUS)
# Crimson top fuel tank
box(m, 3, 11, 4, 11, 14, 12, RED_TANK)
# Vertical muffler & exhaust stack
box(m, 2, 8, 12, 5, 16, 15, BRIGHT_IRON)
# Yellow tubular safety roll-cage posts & top bars
for rx, rz in [(1, 1), (13, 1), (1, 13), (13, 13)]:
    box(m, rx, 2, rz, rx + 2, 15, rz + 2, HAZARD_YLW)
box(m, 1, 14, 1, 15, 15, 3, HAZARD_YLW)
box(m, 1, 14, 13, 15, 15, 15, HAZARD_YLW)
upgraded.append(m)

# 77: Электрический прожектор (3D Tripod Mast + Dual Xenon Floodlight Heads)
m = model(77, "Электрический прожектор")
# Heavy cross-braced steel base & ballast box
box(m, 2, 0, 2, 14, 2, 14, DARK_STEEL)
box(m, 5, 2, 5, 11, 5, 11, HAZARD_YLW)
# Central steel mast (y = 5..11)
box(m, 6, 5, 6, 10, 11, 10, BRIGHT_IRON)
# Dual wide xenon floodlight housings & glowing lens face (y = 10..16)
box(m, 1, 10, 4, 15, 16, 11, DARK_STEEL)
box(m, 2, 11, 11, 7, 15, 13, CHER_WHITE)
box(m, 9, 11, 11, 14, 15, 13, CHER_WHITE)
upgraded.append(m)

# 89: Терминал Радии (3D Angled Cybernetic Command Console with Glowing CRT)
m = model(89, "Терминал Радии")
box(m, 2, 0, 2, 14, 3, 14, DARK_STEEL)
box(m, 3, 3, 3, 13, 8, 12, VISE_IRON)
# Sloped operator keyboard deck (z = 9..15, y = 7..9)
box(m, 2, 7, 9, 14, 9, 15, DARK_STEEL)
box(m, 4, 9, 10, 10, 10, 14, BRIGHT_IRON)
box(m, 11, 9, 11, 13, 10, 13, RED_TANK)
# Upper hooded CRT monitor enclosure & glowing green/cyan screen (y = 8..16, z = 2..10)
box(m, 2, 8, 2, 14, 16, 10, DARK_STEEL)
box(m, 3, 9, 10, 13, 15, 11, RAD_GREEN)
box(m, 5, 11, 10, 11, 13, 11, CHER_WHITE)
upgraded.append(m)

new_ids = {m["id"] for m in upgraded}
d["models"] = [m for m in d["models"] if m["id"] not in new_ids]
for m in upgraded:
    icon = R / f'Assets/Resources/VoxelIcons/Item{m["id"]}.png'
    render(m).quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE).save(icon, optimize=True)
    meta = icon.with_name(icon.name + ".meta")
    if not meta.exists():
        guid = hashlib.md5(f'LestingCraft-VoxelIcon-{m["id"]}'.encode()).hexdigest()
        meta.write_text(f"fileFormatVersion: 2\nguid: {guid}\n", encoding="utf-8")
    cells = []
    for (x, y, z), mat in sorted(m["voxels"].items()):
        cells.extend([x, y, z, mat])
    assert len(cells) > 1000, f'Model {m["id"]} ({m["name"]}) too sparse: {len(cells)} <= 1000'
    d["models"].append(dict(id=m["id"], name=m["name"], cells=cells))
    print(f'Upgraded 3D object #{m["id"]:3d} ({m["name"]}): {len(m["voxels"])} voxels ({len(cells)} ints)')

d["models"].sort(key=lambda item: item["id"])
d["colors"] = [c for c, _ in palette]
d["layers"] = [l for _, l in palette]
save_catalog(R, d)
print("SUCCESS: All placed 3D world objects & 384px icons rebuilt!")
