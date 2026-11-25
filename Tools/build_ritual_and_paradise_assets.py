"""Builds 3D voxel models, 384x384 transparent icons, ExpandedBlocks.json, and ExpandedContent.cs entries for:
- 92: Саженец дуба (Oak Sapling)
- 93: Саженец сакуры (Sakura Sapling)
- 94: Солнечный одуванчик (Golden Dandelion)
- 95: Алый мак (Crimson Poppy)
- 96: Небесный василёк (Azure Cornflower)
- 97: Лавандовый цветок (Lavender Blossom)
- 196: Костная мука (Bone Meal)
"""
import hashlib
import json
import re
import sys
from pathlib import Path
import numpy as np
from PIL import Image

HERE = Path(__file__).resolve().parent
R = HERE.parent
sys.path.insert(0, str(HERE))
from model_io import load_catalog, save_catalog

# Read authoritative SurfaceLayers.Names
sl_text = (R / "Assets/Scripts/Presentation/SurfaceLayers.cs").read_text(encoding="utf-8")
ALL_NAMES = [n.strip().strip('"') for n in re.search(r"Names=\{(.*?)\};", sl_text, re.S).group(1).split(",")]
assert len(ALL_NAMES) == 114, f"Expected 114 SurfaceLayers, got {len(ALL_NAMES)}"

NEW_BLOCKS = [
    dict(id=92, name="Саженец дуба",        surface="leaves",         hardness=0.2,  pick=False, layer=ALL_NAMES.index("leaves")),
    dict(id=93, name="Саженец сакуры",      surface="cherry-blossom", hardness=0.2,  pick=False, layer=ALL_NAMES.index("cherry-blossom")),
    dict(id=94, name="Солнечный одуванчик", surface="leaves",         hardness=0.15, pick=False, layer=ALL_NAMES.index("leaves")),
    dict(id=95, name="Алый мак",            surface="red-mesa",       hardness=0.15, pick=False, layer=ALL_NAMES.index("red-mesa")),
    dict(id=96, name="Небесный василёк",    surface="crystal",        hardness=0.15, pick=False, layer=ALL_NAMES.index("crystal")),
    dict(id=97, name="Лавандовый цветок",   surface="cherry-blossom", hardness=0.15, pick=False, layer=ALL_NAMES.index("cherry-blossom")),
]

# 1. Update Assets/Resources/Models/ExpandedBlocks.json idempotently
exp_json_path = R / "Assets/Resources/Models/ExpandedBlocks.json"
existing_blocks = json.loads(exp_json_path.read_text(encoding="utf-8"))["blocks"]
by_id = {b["id"]: b for b in existing_blocks}
for nb in NEW_BLOCKS:
    by_id[nb["id"]] = nb
all_blocks = [by_id[k] for k in sorted(by_id)]
max_block = max(b["id"] for b in all_blocks)
exp_json_path.write_text(json.dumps({"blocks": all_blocks}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")

# 2. Update Assets/Scripts/Gameplay/ExpandedContent.cs idempotently
exp_cs_path = R / "Assets/Scripts/Gameplay/ExpandedContent.cs"
exp_cs_old = exp_cs_path.read_text(encoding="utf-8")
block_entries = ",\n   ".join(
    f'new ExpandedBlock({b["id"]},"{b["name"]}","{b["surface"]}",{b["hardness"]}f,{"true" if b["pick"] else "false"},{b["layer"]})'
    for b in all_blocks
)
exp_cs_new = re.sub(r"MaxBlock=\d+", f"MaxBlock={max_block}", exp_cs_old, count=1)
exp_cs_new = re.sub(
    r"public static readonly ExpandedBlock\[\] Blocks=\{.*?\};",
    f"public static readonly ExpandedBlock[] Blocks={{\n   {block_entries}\n  }};",
    exp_cs_new,
    count=1,
    flags=re.S,
)
exp_cs_path.write_text(exp_cs_new, encoding="utf-8")

# 3. Load voxel item rendering helpers without running build_voxel_items top-level export
source = (R / "Tools/build_voxel_items.py").read_text(encoding="utf-8")
ctx = {"__file__": str(R / "Tools/build_voxel_items.py")}
exec(compile(source.split("serial=[]")[0], str(R / "Tools/build_voxel_items.py"), "exec"), ctx)

d = load_catalog(R)
palette = list(zip(d["colors"], d["layers"]))
ctx["PALETTE"] = palette
ctx["COLORS"] = [np.array([int(c[i : i + 2], 16) / 255 for i in (1, 3, 5)]) for c, _ in palette]
ctx["NAMES"] = ALL_NAMES


def pal(color: str, layer_name: str) -> int:
    layer = ALL_NAMES.index(layer_name)
    if (color, layer) not in palette:
        palette.append((color, layer))
        ctx["COLORS"].append(np.array([int(color[i : i + 2], 16) / 255 for i in (1, 3, 5)]))
    return palette.index((color, layer))


box = ctx["box"]
model = ctx["model"]
render = ctx["render"]

SOIL_DARK = pal("#4e3622", "dirt")
TURF_GREEN = pal("#488a32", "grass")
BARK_OAK = pal("#6e4c2e", "bark")
BARK_CHERRY = pal("#54343a", "bark")
LEAF_BRIGHT = pal("#54b33e", "leaves")
LEAF_DEEP = pal("#2e7a24", "leaves")
SAKURA_PINK = pal("#f7a8c4", "cherry-blossom")
SAKURA_ROSE = pal("#df6e98", "cherry-blossom")
GOLD_PETAL = pal("#ffd428", "ore-gold")
AMBER_CORE = pal("#e88b14", "ore-gold")
POPPY_RED = pal("#e42828", "red-mesa")
POPPY_DARK = pal("#781216", "red-mesa")
CORN_BLUE = pal("#3888f5", "crystal")
CORN_LIGHT = pal("#82c8ff", "crystal")
LAV_VIOLET = pal("#b26ef0", "andromeda-crystal")
LAV_PALE = pal("#e2baff", "cherry-blossom")
BONE_WHITE = pal("#f5f1e6", "polished-marble")
BONE_CREAM = pal("#d8d0bc", "limestone")
POUCH_TAN = pal("#8f6842", "leather")

sculpted = []

# 92: Саженец дуба — rooted turf mound + young trunk + lush oak sapling foliage (>380 voxels)
m = model(92, "Саженец дуба")
box(m, 4, 0, 4, 12, 2, 12, SOIL_DARK)
box(m, 5, 1, 5, 11, 3, 11, TURF_GREEN)
box(m, 7, 2, 7, 9, 10, 9, BARK_OAK)
box(m, 5, 6, 7, 11, 8, 9, BARK_OAK)
box(m, 7, 6, 5, 9, 8, 11, BARK_OAK)
box(m, 3, 8, 3, 13, 13, 13, LEAF_DEEP)
box(m, 4, 11, 4, 12, 15, 12, LEAF_BRIGHT)
box(m, 6, 14, 6, 10, 16, 10, LEAF_BRIGHT)
sculpted.append(m)

# 93: Саженец сакуры — rooted mound + dark cherry trunk + pink sakura blossom canopy
m = model(93, "Саженец сакуры")
box(m, 4, 0, 4, 12, 2, 12, SOIL_DARK)
box(m, 5, 1, 5, 11, 3, 11, TURF_GREEN)
box(m, 7, 2, 7, 9, 10, 9, BARK_CHERRY)
box(m, 5, 7, 6, 11, 9, 10, BARK_CHERRY)
box(m, 3, 9, 3, 13, 13, 13, SAKURA_ROSE)
box(m, 4, 11, 4, 12, 15, 12, SAKURA_PINK)
box(m, 6, 14, 6, 10, 16, 10, LAV_PALE)
sculpted.append(m)

# 94: Солнечный одуванчик — green rosette leaves + stalk + radiant golden dandelion head
m = model(94, "Солнечный одуванчик")
box(m, 3, 0, 5, 13, 2, 11, LEAF_DEEP)
box(m, 5, 0, 3, 11, 2, 13, LEAF_DEEP)
box(m, 6, 1, 6, 10, 9, 10, LEAF_BRIGHT)
box(m, 3, 4, 6, 13, 6, 10, LEAF_BRIGHT)
box(m, 3, 9, 3, 13, 13, 13, GOLD_PETAL)
box(m, 4, 12, 4, 12, 15, 12, GOLD_PETAL)
box(m, 6, 13, 6, 10, 16, 10, AMBER_CORE)
sculpted.append(m)

# 95: Алый мак — verdant rosette + stalk + cupped crimson-scarlet poppy petals + dark stamen heart
m = model(95, "Алый мак")
box(m, 3, 0, 5, 13, 2, 11, LEAF_DEEP)
box(m, 5, 0, 3, 11, 2, 13, LEAF_DEEP)
box(m, 6, 1, 6, 10, 9, 10, LEAF_BRIGHT)
box(m, 4, 4, 6, 12, 6, 10, LEAF_DEEP)
box(m, 3, 9, 4, 13, 14, 12, POPPY_RED)
box(m, 4, 9, 3, 12, 14, 13, POPPY_RED)
box(m, 5, 11, 5, 11, 15, 11, POPPY_DARK)
sculpted.append(m)

# 96: Небесный василёк — leafy stem + star-shaped azure cornflower crown
m = model(96, "Небесный василёк")
box(m, 3, 0, 5, 13, 2, 11, LEAF_DEEP)
box(m, 5, 0, 3, 11, 2, 13, LEAF_DEEP)
box(m, 6, 1, 6, 10, 9, 10, LEAF_BRIGHT)
box(m, 6, 4, 3, 10, 6, 13, LEAF_BRIGHT)
box(m, 3, 9, 3, 13, 13, 13, CORN_BLUE)
box(m, 2, 11, 5, 14, 14, 11, CORN_LIGHT)
box(m, 5, 11, 2, 11, 14, 14, CORN_LIGHT)
box(m, 6, 12, 6, 10, 15, 10, BONE_WHITE)
sculpted.append(m)

# 97: Лавандовый цветок — multi-stem fragrant violet-lavender blossom spikes
m = model(97, "Лавандовый цветок")
box(m, 4, 0, 4, 12, 2, 12, LEAF_DEEP)
box(m, 5, 2, 5, 11, 7, 11, LEAF_BRIGHT)
box(m, 4, 6, 4, 12, 12, 12, LAV_VIOLET)
box(m, 5, 10, 5, 11, 15, 11, LAV_PALE)
box(m, 6, 13, 6, 10, 16, 10, LAV_VIOLET)
sculpted.append(m)

# 196: Костная мука — leather pouch brimming with ivory bone-meal crystals
m = model(196, "Костная мука")
box(m, 3, 0, 3, 13, 8, 13, POUCH_TAN)
box(m, 4, 7, 4, 12, 10, 12, BONE_CREAM)
box(m, 4, 9, 4, 12, 13, 12, BONE_WHITE)
box(m, 5, 12, 5, 11, 15, 11, BONE_WHITE)
box(m, 6, 14, 6, 10, 16, 10, BONE_CREAM)
sculpted.append(m)

new_ids = {m["id"] for m in sculpted}
d["models"] = [m for m in d["models"] if m["id"] not in new_ids]

for m in sculpted:
    icon = R / f'Assets/Resources/VoxelIcons/Item{m["id"]}.png'
    render(m).quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE).save(icon, optimize=True)
    meta = icon.with_name(icon.name + ".meta")
    if not meta.exists():
        guid = hashlib.md5(f'LestingCraft-VoxelIcon-{m["id"]}'.encode()).hexdigest()
        meta.write_text(f"fileFormatVersion: 2\nguid: {guid}\n", encoding="utf-8")
    cells = []
    for (x, y, z), mat in sorted(m["voxels"].items()):
        cells.extend([x, y, z, mat])
    assert len(cells) > 1000, f'Model {m["id"]} too sparse: {len(cells)}'
    d["models"].append({"id": m["id"], "name": m["name"], "cells": cells})

d["models"].sort(key=lambda item: item["id"])
d["colors"] = [c for c, _ in palette]
d["layers"] = [l for _, l in palette]
save_catalog(R, d)
print(
    f'SUCCESS: {len(all_blocks)} ExpandedBlocks (19..{max_block}), '
    f'{len(d["models"])} VoxelModels + 384px icons generated cleanly!'
)
