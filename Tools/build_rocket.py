#!/usr/bin/env python3
"""Discovery heavy-lift rocket v2: slender 8:1 stack, ogive nose, capsule glass band,
strap-on boosters, thin swept fins, bell nozzles. Exports Shuttle.json (RLE)."""
import json

SX, SY, SZ = 48, 128, 48
CX, CZ = 23.5, 23.5

PALETTE = [
    ("air", "#000000"),
    ("hull-white", "#e9edef"),
    ("hull-shadow", "#c3cad1"),
    ("seam-grey", "#8b939c"),
    ("dark-structure", "#39414a"),
    ("engine-black", "#1b1f26"),
    ("engine-throat", "#5a2418"),
    ("glass-cupola", "#22586e"),
    ("glass-glow", "#3fc9e8"),
    ("accent-red", "#b3282d"),
    ("accent-orange", "#d97b28"),
    ("flag-blue", "#2c57a8"),
]
PAL = {n: i for i, (n, _) in enumerate(PALETTE)}
cells = {}


def put(x, y, z, v):
    x, y, z = int(x), int(y), int(z)
    if 0 <= x < SX and 0 <= y < SY and 0 <= z < SZ:
        cells[(x, y, z)] = PAL[v]


def disc(cx, cz, r, y, v):
    for x in range(int(cx - r), int(cx + r) + 1):
        for z in range(int(cz - r), int(cz + r) + 1):
            if (x - cx) ** 2 + (z - cz) ** 2 <= r * r:
                put(x, y, z, v)


def ring(cx, cz, r, y, v):
    for x in range(int(cx - r), int(cx + r) + 1):
        for z in range(int(cz - r), int(cz + r) + 1):
            d2 = (x - cx) ** 2 + (z - cz) ** 2
            if (r - 1.1) ** 2 <= d2 <= r * r:
                put(x, y, z, v)


# central stack r=7
R = 7.0
for y in range(16, 97):
    disc(CX, CZ, R, y, "hull-white")
    ring(CX, CZ, R, y, "hull-shadow")
    if y % 12 == 0:
        ring(CX, CZ, R, y, "seam-grey")

# service module band
for y in range(16, 27):
    ring(CX, CZ, R, y, "dark-structure")
ring(CX, CZ, R, 26, "seam-grey")

# red accent arcs (two, front half only)
for y0 in (34, 62):
    for y in (y0, y0 + 1):
        for x in range(int(CX - R), int(CX + R) + 1):
            for z in range(int(CZ - R), int(CZ + R) + 1):
                d2 = (x - CX) ** 2 + (z - CZ) ** 2
                if d2 <= R * R and d2 >= (R - 1.1) ** 2 and z > CZ + 1:
                    put(x, y, z, "accent-red")

# flag patch on upper stage
for y in range(72, 76):
    for z in range(int(CZ - 4), int(CZ - 1)):
        put(int(CX + R), y, z, "flag-blue")

# capsule + cupola glass band
for y in range(97, 105):
    disc(CX, CZ, R, y, "hull-shadow")
for y in (100, 101):
    ring(CX, CZ, R, y, "glass-cupola")
    ring(CX, CZ, R - 1, y, "glass-glow")
ring(CX, CZ, R, 99, "seam-grey")

# nose cone: strong ogive 105..127
for y in range(105, 128):
    t = (y - 104) / 23.0
    r = R * max(0.05, (1.0 - t) ** 0.62)
    disc(CX, CZ, r, y, "hull-white")
    if (y - 104) % 8 == 0:
        ring(CX, CZ, max(r, 1.2), y, "seam-grey")
put(CX, 127, CZ, "accent-orange")
put(CX, 126, CZ, "accent-orange")

# engine skirt flare 8..16
for y in range(8, 17):
    t = (16 - y) / 8.0
    r = R + 2.6 * t
    disc(CX, CZ, r, y, "dark-structure")
    ring(CX, CZ, r, y, "seam-grey" if y in (8, 12) else "dark-structure")

# engine bells (4 + center)
for ex, ez in ((CX - 4.5, CZ - 4.5), (CX + 4.5, CZ - 4.5), (CX - 4.5, CZ + 4.5), (CX + 4.5, CZ + 4.5)):
    for y in range(1, 9):
        r = 2.2 + (8 - y) * 0.18
        ring(ex, ez, r, y, "engine-black")
    disc(ex, ez, 2.0, 1, "engine-throat")
for y in range(1, 9):
    ring(CX, CZ, 2.6, y, "engine-black")
disc(CX, CZ, 2.3, 1, "engine-throat")

# thin swept fins x4: blade from body out+down
for sx, sz in ((0, -1), (0, 1), (-1, 0), (1, 0)):
    for step in range(10):
        off = R + step
        ytop = int(15 - step * 1.15)
        for y in range(4, ytop + 1):
            px, pz = CX + sx * off, CZ + sz * off
            put(px, y, pz, "hull-white")
            if sx:
                put(px, y, pz + 1, "hull-shadow")
                put(px, y, pz - 1, "hull-shadow")
            else:
                put(px + 1, y, pz, "hull-shadow")
                put(px - 1, y, pz, "hull-shadow")
    # red tip edge
    put(CX + sx * (R + 9), 4, CZ + sz * (R + 9), "accent-red")

# strap-on boosters
for bx in (CX - 13.5, CX + 13.5):
    BR = 4.5
    for y in range(4, 65):
        disc(bx, CZ, BR, y, "hull-white")
        ring(bx, CZ, BR, y, "hull-shadow")
        if y % 10 == 0:
            ring(bx, CZ, BR, y, "seam-grey")
    for y in range(65, 74):
        t = (y - 64) / 9.0
        disc(bx, CZ, BR * max(0.1, (1 - t) ** 0.7), y, "hull-white")
    put(bx, 74, CZ, "seam-grey")
    for y in range(1, 5):
        ring(bx, CZ, BR + (4 - y) * 0.3, y, "engine-black")
    disc(bx, CZ, BR - 2, 1, "engine-throat")
    for y in (40, 41):
        ring(bx, CZ, BR, y, "accent-red")
    # straps to core
    for y in (24, 56):
        for x in range(int(min(bx, CX)), int(max(bx, CX)) + 1):
            put(x, y, int(CZ), "seam-grey")
            put(x, y + 1, int(CZ), "seam-grey")

# export RLE (x fastest, then y, then z)
runs = []
for z in range(SZ):
    for y in range(SY):
        for x in range(SX):
            v = cells.get((x, y, z), 0)
            if runs and runs[-1][0] == v:
                runs[-1][1] += 1
            else:
                runs.append([v, 1])
out = {
    "name": "Discovery heavy-lift rocket v2 — slender stack, ogive nose, capsule, boosters, swept fins",
    "size": [SX, SY, SZ],
    "palette": [{"name": n, "hex": h} for n, h in PALETTE],
    "runs": runs,
}
with open("/home/user/lc_check/ArtSource/Space/Approved/Shuttle.json", "w") as f:
    json.dump(out, f, separators=(",", ":"))
print("voxels:", len(cells), "runs:", len(runs))
