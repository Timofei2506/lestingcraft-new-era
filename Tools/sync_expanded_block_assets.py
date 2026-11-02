#!/usr/bin/env python3
"""Append missing expanded-block models/icons from ExpandedBlocks.json.

This is a targeted repair/sync tool: it preserves every existing model and palette
index, appends only missing block definitions, and renders icons with the same
orthographic voxel renderer used by build_voxel_items.py. Run after adding rows to
ExpandedContent.cs / ExpandedBlocks.json and after the PBR maps exist.
"""
from pathlib import Path
import json
import sys
import uuid

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "Tools"))
from model_io import load_catalog, save_catalog  # noqa: E402

blocks = json.loads((ROOT / "Assets/Resources/Models/ExpandedBlocks.json").read_text())["blocks"]
catalog = load_catalog(ROOT)
models = {m["id"]: m for m in catalog["models"]}
palette = list(zip(catalog["colors"], catalog["layers"]))


def palette_index(color: str, layer: int) -> int:
    key = (color, layer)
    if key in palette:
        return palette.index(key)
    palette.append(key)
    return len(palette) - 1


added = []
for block in blocks:
    bid = block["id"]
    if bid in models:
        continue  # Preserve authored multi-material models and stable palette indices.
    color = "#d9f1f5" if bid == 44 else "#ffffff"
    p = palette_index(color, block["layer"])
    cells = []
    for x in range(12):
        for y in range(12):
            for z in range(12):
                cells.extend([x, y, z, p])
    model = {"id": bid, "name": block["name"], "cells": cells}
    catalog["models"].append(model)
    models[bid] = model
    added.append(bid)

if added or len(palette) != len(catalog["colors"]):
    catalog["colors"] = [c for c, _ in palette]
    catalog["layers"] = [l for _, l in palette]
    save_catalog(ROOT, catalog)

icon_dir = ROOT / "Assets/Resources/VoxelIcons"
missing_icons = [m for m in catalog["models"] if not (icon_dir / f"Item{m['id']}.png").exists()]
if missing_icons:
    # Reuse the exact authoring renderer without executing its catalog-writing tail.
    source = (ROOT / "Tools/build_voxel_items.py").read_text()
    ctx = {"__file__": str(ROOT / "Tools/build_voxel_items.py"), "__name__": "voxel_icon_sync"}
    exec(compile(source.split("serial=[]")[0], "voxel-icon-sync", "exec"), ctx)
    ctx["PALETTE"] = palette
    ctx["COLORS"] = [np.array([int(c[i:i + 2], 16) / 255 for i in (1, 3, 5)]) for c in catalog["colors"]]
    template = (icon_dir / "Item89.png.meta").read_text()
    for model in missing_icons:
        voxels = {}
        cells = model["cells"]
        for i in range(0, len(cells), 4):
            voxels[tuple(cells[i:i + 3])] = cells[i + 3]
        image = ctx["render"]({"id": model["id"], "name": model["name"], "voxels": voxels})
        path = icon_dir / f"Item{model['id']}.png"
        image.quantize(colors=256, method=Image.Quantize.FASTOCTREE, dither=Image.Dither.NONE).save(path, optimize=True)
        meta = path.with_suffix(".png.meta")
        if not meta.exists():
            import re
            meta.write_text(re.sub(r"guid: [0-9a-f]{32}", "guid: " + uuid.uuid4().hex, template))

print(f"Expanded block sync: added models {added or 'none'}, rendered icons {[m['id'] for m in missing_icons] or 'none'}, catalog={len(catalog['models'])} models")
