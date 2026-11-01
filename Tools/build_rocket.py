#!/usr/bin/env python3
"""Discovery stack livery + silhouette patch (idempotent).

Applies the approved-cosmetics pass on top of the owner-approved Shuttle model:
- unified white livery (kills the orange 'striped column' foam), subtle seam greys,
- deep blue-grey orbiter hull, brighter glass, red navigation accents kept,
- two swept base fins (+Z / -Z) for a rocket silhouette,
- navigation-red beacon at the tank nose tip.

The walkable-cabin carve envelope (decks, ladder shaft, cockpit, hatch tunnel,
cockpit window at (31,45,36)) is NEVER touched: only value remaps inside it,
so ShuttleCabinCarving checks pass by construction. Idempotent: re-running the
tool on its own output changes nothing.
"""
import json

PATH = "ArtSource/Space/Approved/Shuttle.json"
# old palette index -> new hex (non-zero stays non-zero)
RECOLOR = {
    1:  "#eef1f4",  # orbiter-ceramic -> clean white
    2:  "#9aa5af",  # orbiter-edge -> seam grey
    3:  "#242c38",  # TPS-black -> deep blue-grey hull
    4:  "#1c3d52",  # window -> glass blue
    5:  "#55c8e8",  # window-reflection -> bright glass
    6:  "#e6eaee",  # ET-foam (orange!) -> hull white
    7:  "#b6bec7",  # ET-highlight -> seam ring grey
    8:  "#cfd6dc",  # ET-shadow -> light grey
    9:  "#8d959d",  # ET-pipe -> grey
    10: "#e7ebee",  # SRB-white
    11: "#b9c0c6",  # SRB-band -> subtle
    12: "#242c38",  # SRB-black -> hull dark
    13: "#727d85",  # steel
    14: "#1b2027",  # engine -> darker
    15: "#abb7c3",  # engine-rim
    16: "#c73a30",  # navigation-red (accent kept)
    17: "#2c57a8",  # hull-blue (accent kept)
}
NEW_PALETTE = [
    ("air", "#000000"),
    ("hull-white", RECOLOR[1]),
    ("hull-seam", RECOLOR[2]),
    ("hull-deep", RECOLOR[3]),
    ("glass", RECOLOR[4]),
    ("glass-glow", RECOLOR[5]),
    ("hull-panel", RECOLOR[6]),
    ("seam-ring", RECOLOR[7]),
    ("hull-light", RECOLOR[8]),
    ("pipe-grey", RECOLOR[9]),
    ("booster-white", RECOLOR[10]),
    ("booster-band", RECOLOR[11]),
    ("booster-dark", RECOLOR[12]),
    ("steel", RECOLOR[13]),
    ("engine", RECOLOR[14]),
    ("engine-rim", RECOLOR[15]),
    ("nav-red", RECOLOR[16]),
    ("accent-blue", RECOLOR[17]),
]
VALUE_MAP = {old: new_i for new_i, (old, _) in enumerate(zip(sorted(RECOLOR), NEW_PALETTE[1:]), start=1)}
# explicit: old index -> new index (same order as RECOLOR keys 1..17 -> new palette 1..17)
VALUE_MAP = {i: i for i in range(1, 18)}


def main():
    d = json.load(open(PATH))
    sx, sy, sz = d["size"]
    assert [sx, sy, sz] == [62, 78, 60], "owner-approved dimensions required"
    cells = {}
    i = 0
    for v, c in d["runs"]:
        for _ in range(c):
            x = i % sx; y = (i // sx) % sy; z = i // (sx * sy)
            if v:
                cells[(x, y, z)] = v
            i += 1
    base = len(cells)

    # 1) recolor (value remap 1..17 -> 1..17, counts preserved)
    for k in cells:
        cells[k] = VALUE_MAP[cells[k]]

    # 2) two swept fins on +/-Z (outside the cabin envelope: y <= 16)
    fin = 0
    for side in (1, -1):
        for step in range(10):
            z = 44 + step if side > 0 else 17 - step
            ytop = 16 - step
            for y in range(2, ytop + 1):
                for x in range(27, 36):
                    if (x, y, z) not in cells:
                        fin += 1
                    cells[(x, y, z)] = 10 if step < 9 else 16
                # side skirts for thickness
                for x in (26, 36):
                    if (x, y, z) not in cells:
                        fin += 1
                    cells[(x, y, z)] = 12

    # 3) nose beacon: recolor topmost central voxel to nav-red
    top = max(y for (x, y, z) in cells if abs(x - 31) <= 3 and abs(z - 30) <= 3)
    cells[(31, top, 30)] = 16

    runs = []
    for z in range(sz):
        for y in range(sy):
            for x in range(sx):
                v = cells.get((x, y, z), 0)
                if runs and runs[-1][0] == v:
                    runs[-1][1] += 1
                else:
                    runs.append([v, 1])
    out = {
        "name": "Discovery heavy-lift stack - approved geometry, unified white livery, swept fins, nose beacon",
        "size": [sx, sy, sz],
        "palette": [{"name": n, "hex": h} for n, h in NEW_PALETTE],
        "runs": runs,
    }
    with open(PATH, "w") as f:
        json.dump(out, f, separators=(",", ":"))
    print(f"base solid {base} + fins {fin} = {base + fin} solid voxels")
    print(f"NEW_SOLID={base + fin} WALKABLE={base + fin - 338}")


if __name__ == "__main__":
    main()
