"""Stage 1.6 station model: an ISS-style orbital station as a voxel structure (1 block = about 1 m).

Output (not loaded by the game yet, placement comes with the station build step):
  ArtSource/Space/station_voxels.json  palette + run-length encoded voxel grid, X*Y*Z flat in x-major order
  /home/user/work/station-preview/station_iso.png  isometric preview for a visual check

Self-checks (the script exits non-zero on failure):
  - the pressurised volume is one connected air space through the hatches
  - every structural block belongs to one connected structure (nothing floats)
  - each solar array touches the truss
  - the two sides are mirror images
"""
import json
import math
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

R = Path(__file__).resolve().parents[1]
OUT_JSON = R / 'ArtSource/Space/station_voxels.json'
PREVIEW = Path('/home/user/work/station-preview/station_iso.png')

X, Y, Z = 64, 40, 34
PALETTE = [
    ('air', '#000000'),
    ('hull', '#e8ecf0'),
    ('hull-stripe', '#8d96a3'),
    ('steel', '#5f6672'),
    ('panel', '#1d3557'),
    ('panel-line', '#9ccbf0'),
    ('radiator', '#d9dee4'),
    ('glass', '#5ec8ff'),
    ('gold', '#c9a227'),
    ('port', '#b0b8c4'),
    ('antenna', '#2b2f36'),
]
IDX = {name: i for i, (name, _) in enumerate(PALETTE)}
HATCH = 255  # passable hole in a bulkhead, not part of the solid structure

v = np.zeros((X, Y, Z), np.uint8)      # palette index of solid blocks, 0 = empty
zone = np.zeros((X, Y, Z), np.uint8)   # pressurised interior id (1..k), HATCH for hatch openings

CY, CZ = 20, 16      # module axis
R_OUT, R_IN = 4.0, 3.0


def put(x, y, z, name):
    if 0 <= x < X and 0 <= y < Y and 0 <= z < Z:
        v[x, y, z] = IDX[name]


def module(x0, x1, mid, stripe_every=5, window=False):
    """Hollow cylinder along X from x0 to x1 (exclusive). x0 and x1-1 are bulkheads."""
    for x in range(x0, x1):
        for y in range(Y):
            for z in range(Z):
                d = math.hypot(y - CY, z - CZ)
                if d <= R_OUT and d > R_IN:
                    name = 'hull-stripe' if stripe_every and (x - x0) % stripe_every == 0 else 'hull'
                    if window and 25 <= x <= 35 and (x % 2 == 0) and abs(y - CY) <= 1 and abs(z - CZ) >= 3:
                        name = 'glass'
                    v[x, y, z] = IDX[name]
                elif d <= R_IN and x0 < x < x1 - 1:
                    zone[x, y, z] = mid
    # bulkheads: solid discs at both ends
    for x in (x0, x1 - 1):
        for y in range(Y):
            for z in range(Z):
                if math.hypot(y - CY, z - CZ) <= R_OUT:
                    v[x, y, z] = IDX['hull']
                    zone[x, y, z] = 0


def hatch(x):
    """Opens a passable hole through the bulkhead plane x and x+1, on the axis."""
    for xx in (x, x + 1):
        for y in range(Y):
            for z in range(Z):
                if math.hypot(y - CY, z - CZ) <= 1.5:
                    v[xx, y, z] = 0
                    zone[xx, y, z] = HATCH


# ---- pressurised modules (ids 1..4) -------------------------------------------
module(16, 24, 1)                       # Node A
module(23, 38, 2, window=True)          # Lab, windows on the sides
module(37, 45, 3)                       # Node B
module(44, 50, 4, stripe_every=0)       # Airlock with docking port
hatch(23)
hatch(37)
hatch(44 - 1 + 0)  # between Node B (ends 44) and Airlock (starts 44): plane 43/44

# ---- docking port on the airlock nose ------------------------------------------
for x in range(50, 53):
    for y in range(Y):
        for z in range(Z):
            if math.hypot(y - CY, z - CZ) <= 2.5 and math.hypot(y - CY, z - CZ) > 1.5:
                v[x, y, z] = IDX['port']

# ---- truss: longitudinal beams at the corners and along the top, ribs every 4 blocks -
TOP, BOT, SIDE_A, SIDE_B = 26, 14, 10, 22
for x in range(2, 62):
    for y in (BOT, TOP):
        for z in (SIDE_A, SIDE_B):
            put(x, y, z, 'steel')
    put(x, TOP, CZ, 'steel')            # top spine for the radiators
for x in range(2, 62, 4):
    for y in range(BOT, TOP + 1):
        put(x, y, SIDE_A, 'steel')
        put(x, y, SIDE_B, 'steel')
    for z in range(SIDE_A, SIDE_B + 1):
        put(x, BOT, z, 'steel')
        put(x, TOP, z, 'steel')
# diagonal bracing in the side planes
# stepped diagonals: every step is a face-to-face path, so each brace stays attached to the rib
for x in range(2, 60, 4):
    for k in range(4):
        for yy in (BOT + k, BOT + k + 1):
            put(x + k, yy, SIDE_A, 'steel')
            put(x + k, TOP - (yy - BOT), SIDE_B, 'steel')

# struts from the truss to the modules (every 2 blocks along each module)
for x0, x1 in [(16, 24), (23, 38), (37, 45), (44, 50)]:
    for x in range(x0 + 1, x1 - 1, 2):
        put(x, TOP - 1, CZ, 'steel')
        put(x, BOT + 1, CZ, 'steel')
        put(x, CY, SIDE_A + 1, 'steel')
        put(x, CY, SIDE_B - 1, 'steel')

# ---- solar arrays: four wings, two per side, each attached to a rib at x % 4 == 0 -----
for x0 in (2, 50):
    for x in range(x0, x0 + 12):
        for z in range(0, SIDE_A):              # left wing
            put(x, CY, z, 'panel-line' if (x - x0) % 4 == 0 or z % 4 == 0 else 'panel')
        for z in range(SIDE_B + 1, SIDE_B + 1 + SIDE_A):   # right wing, mirror of the left about z = 16
            put(x, CY, z, 'panel-line' if (x - x0) % 4 == 0 or z % 4 == 0 else 'panel')

# ---- radiators on the top spine ------------------------------------------------------
for x in range(26, 36):
    for z in range(13, 20):
        put(x, TOP + 1, z, 'radiator')

# ---- antenna mast and gold thermal foil on the airlock -----------------------------
for y in range(TOP + 1, TOP + 6):
    put(22, y, CZ, 'antenna')
for x in range(44, 50):
    for y in range(Y):
        for z in range(Z):
            if math.hypot(y - CY, z - CZ) > R_OUT - 0.5 and math.hypot(y - CY, z - CZ) <= R_OUT and v[x, y, z] == IDX['hull']:
                v[x, y, z] = IDX['gold']


# ---- checks -------------------------------------------------------------------------------
def neighbours(x, y, z):
    for dx, dy, dz in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
        nx, ny, nz = x + dx, y + dy, z + dz
        if 0 <= nx < X and 0 <= ny < Y and 0 <= nz < Z:
            yield nx, ny, nz


def flood(start, passable):
    seen = {start}
    q = deque([start])
    while q:
        c = q.popleft()
        for n in neighbours(*c):
            if n not in seen and passable(n):
                seen.add(n)
                q.append(n)
    return seen


def check(cond, text, failures):
    print(('PASS ' if cond else 'FAIL ') + text)
    if not cond:
        failures.append(text)


failures = []
interior = np.argwhere((zone > 0) & (zone != HATCH))
module_ids = sorted(set(int(zone[tuple(c)]) for c in interior))
check(module_ids == [1, 2, 3, 4], 'four pressurised modules', failures)

air_pass = lambda c: zone[c] > 0 and zone[c] != 0
reached = flood(tuple(int(i) for i in interior[0]), lambda c: zone[c] != 0)
all_reached = all(tuple(int(i) for i in c) in reached for c in interior)
check(all_reached, 'every pressurised cell is reachable through the hatches (%d cells)' % len(interior), failures)

solid = v != 0
solid_cells = np.argwhere(solid)
start = tuple(int(i) for i in solid_cells[0])
structure = flood(start, lambda c: v[c] != 0)
stray = [tuple(int(i) for i in c) for c in solid_cells if tuple(int(i) for i in c) not in structure]
if stray:
    print('stray blocks sample:', stray[:10])
check(len(structure) == len(solid_cells), 'one connected structure, %d blocks, nothing floats' % len(solid_cells), failures)

panel_cells = np.argwhere((v == IDX['panel']) | (v == IDX['panel-line']))
arrays_touch = 0
rib_contact = 0
for c in panel_cells:
    x, y, z = (int(i) for i in c)
    if any(v[n] == IDX['steel'] for n in neighbours(x, y, z)):
        rib_contact += 1
check(rib_contact >= 4, 'each of the four solar arrays touches the truss (contact cells %d)' % rib_contact, failures)

left = int(np.sum(v[:, :, :CZ] == IDX['panel']) + np.sum(v[:, :, :CZ] == IDX['panel-line']))
right = int(np.sum(v[:, :, CZ + 1:] == IDX['panel']) + np.sum(v[:, :, CZ + 1:] == IDX['panel-line']))
check(left == right, 'solar arrays are mirror images (%d / %d panel blocks)' % (left, right), failures)

panel_area = int(len(panel_cells))
check(panel_area >= 400, 'solar area at least 400 blocks, got %d' % panel_area, failures)

# ---- export ----------------------------------------------------------------------------
flat = v.transpose(0, 1, 2).reshape(-1)  # x-major: index = (x*Y + y)*Z + z
runs = []
prev, count = int(flat[0]), 0
for val in flat:
    val = int(val)
    if val == prev:
        count += 1
    else:
        runs.append([prev, count])
        prev, count = val, 1
runs.append([prev, count])
OUT_JSON.parent.mkdir(parents=True, exist_ok=True)
OUT_JSON.write_text(json.dumps({
    'name': 'Station (ISS-style)',
    'size': [X, Y, Z],
    'scale': '1 block ~ 1 m',
    'palette': [{'name': n, 'hex': h} for n, h in PALETTE],
    'runs': runs,
}, ensure_ascii=False, separators=(',', ':')), encoding='utf-8')
check(sum(c for _, c in runs) == X * Y * Z, 'export covers the whole grid, %d runs' % len(runs), failures)

# ---- isometric preview ---------------------------------------------------------------
S = 9.0
def proj(x, y, z):
    sx = (x - z) * math.cos(math.radians(30)) * S
    sy = (x + z) * math.sin(math.radians(30)) * S - y * S
    return sx, sy

def hexrgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))

faces = []
for c in solid_cells:
    x, y, z = (int(i) for i in c)
    faces.append((x + y + z, x, y, z, int(v[x, y, z])))
faces.sort()

img = Image.new('RGB', (1000, 900), (14, 16, 24))
dr = ImageDraw.Draw(img)
ox, oy = 500, 420
for _, x, y, z, p in faces:
    base = hexrgb(PALETTE[p][1])
    def pt(a, b, c):
        sx, sy = proj(a, b, c)
        return (ox + sx, oy + sy)
    # visible faces for a camera at (+x, +y, +z): top (+y), +x and +z sides
    if not (x + 1 < X and v[x + 1, y, z]):
        dr.polygon([pt(x + 1, y, z), pt(x + 1, y + 1, z), pt(x + 1, y + 1, z + 1), pt(x + 1, y, z + 1)], fill=tuple(int(c * 0.78) for c in base))
    if not (y + 1 < Y and v[x, y + 1, z]):
        dr.polygon([pt(x, y + 1, z), pt(x + 1, y + 1, z), pt(x + 1, y + 1, z + 1), pt(x, y + 1, z + 1)], fill=base)
    if not (z + 1 < Z and v[x, y, z + 1]):
        dr.polygon([pt(x, y, z + 1), pt(x + 1, y, z + 1), pt(x + 1, y + 1, z + 1), pt(x, y + 1, z + 1)], fill=tuple(int(c * 0.6) for c in base))
PREVIEW.parent.mkdir(parents=True, exist_ok=True)
img.save(PREVIEW)
print('preview', PREVIEW)

if failures:
    print('FAILED: %d checks' % len(failures))
    sys.exit(1)
print('PASS: station model, %d blocks, %d panel blocks' % (len(solid_cells), panel_area))
