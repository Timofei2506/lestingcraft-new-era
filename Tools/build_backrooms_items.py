"""Adds the shared voxel object models for Backrooms blocks 90 (wallpaper) and 91 (carpet).
Idempotent: palette entries and models are replaced, never duplicated. Run from anywhere."""
from pathlib import Path
import sys
sys.path.insert(0, str(Path(__file__).resolve().parent))
from model_io import load_catalog, save_catalog
R = Path(__file__).resolve().parents[1]
d = load_catalog(R)
WALL, CARPET = 112, 113  # surface layer indices (SurfaceLayers.cs)

def pal(color, layer):
    for i, (c, l) in enumerate(zip(d['colors'], d['layers'])):
        if c == color and l == layer: return i
    d['colors'].append(color); d['layers'].append(layer); return len(d['colors']) - 1

def h(x, y, z):  # deterministic hash noise 0..1
    n = (x * 73856093) ^ (y * 19349663) ^ (z * 83492791)
    n = (n ^ (n >> 13)) * 1274126177 & 0xFFFFFFFF
    return ((n ^ (n >> 16)) & 0xFFFF) / 65535.0

wall = [pal('#d2b85a', WALL), pal('#c4aa4c', WALL), pal('#e0c870', WALL), pal('#8a6f35', WALL)]
carpet = [pal('#7d6e44', CARPET), pal('#6e6038', CARPET), pal('#8c7d50', CARPET), pal('#4e4528', CARPET)]

def wall_cell(x, y, z):
    if y <= 2: return wall[3]                      # dark baseboard
    if y >= 14: return wall[1]                     # ceiling trim shadow
    if (x + z) % 4 in (0, 1) and h(x, y, z) > .15: return wall[1]   # vinyl ribbing
    return wall[2] if h(x, y, z) > .93 else wall[0]

def carpet_cell(x, y, z):
    r = h(x, y, z)
    if r > .94: return carpet[3]                   # damp stain
    if r > .72: return carpet[2]
    if r < .28: return carpet[1]
    return carpet[0]

for bid, name, fn in [(90, 'Жёлтые обои Уровня 0', wall_cell), (91, 'Влажный ковёр Уровня 0', carpet_cell)]:
    cells = []
    for x in range(1, 15):
        for y in range(1, 15):
            for z in range(1, 15): cells += [x, y, z, fn(x, y, z)]
    d['models'] = [m for m in d['models'] if m['id'] != bid]
    d['models'].append(dict(id=bid, name=name, cells=cells))
d['models'].sort(key=lambda m: m['id'])
save_catalog(R, d)
print('models:', len(d['models']), 'palette:', len(d['colors']))
