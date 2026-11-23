#!/usr/bin/env python3
"""Builds the brand-new Heavy Multi-Stage Orbital Rocket ('Vulkan-Heavy') and Modern Cosmodrome
Launch Complex ('Baikonur-Nova'), completely replacing the legacy winged Shuttle & concrete slab.

Preserves all walkable crew cabin, ladder shaft, twin cockpit seats, portside airlock gantry,
moving tower elevator, and LCVM binary format invariants verified by CI.
"""
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SHUTTLE_PATH = ROOT / "ArtSource/Space/Approved/Shuttle.json"
PAD_PATH = ROOT / "ArtSource/Space/Approved/LaunchPad.json"


def encode_runs(cells, sx, sy, sz):
    runs = []
    # SpaceVoxelAsset.At(x, y, z) indexes cells[(x * Y + y) * Z + z]!
    for x in range(sx):
        for y in range(sy):
            for z in range(sz):
                v = cells.get((x, y, z), 0)
                if runs and runs[-1][0] == v:
                    runs[-1][1] += 1
                else:
                    runs.append([v, 1])
    return runs


def build_heavy_rocket():
    sx, sy, sz = 62, 78, 60
    cx, cz = 31, 30
    palette = [
        ("air", "#000000"),
        ("hull-white", "#eef2f6"),       # 1: primary cryogenic white hull
        ("hull-panel", "#dfe5ec"),       # 2: subtle stepped hull alloy
        ("seam-ring", "#98a4b2"),        # 3: structural tank rings
        ("interstage-dark", "#202733"),  # 4: carbon-composite interstage & TPS
        ("cockpit-window", "#1d4968"),   # 5: transparent spacecraft glass ('window' keyword!)
        ("window-frame", "#5cc9ea"),     # 6: illuminated viewport rim
        ("booster-white", "#e7ecf1"),    # 7: strap-on liquid booster skin
        ("booster-band", "#3a4554"),     # 8: Korolev thrust bands
        ("titanium-grid", "#6f7b88"),    # 9: titanium lattice grid-fins & struts
        ("engine-bell", "#191e26"),      # 10: regeneratively cooled dark nozzle bells
        ("engine-rim", "#c58f58"),       # 11: bronze/inconel turbine manifold rings
        ("cryo-pipe", "#b3bec9"),        # 12: external LOX/CH4 feedline raceways
        ("accent-crimson", "#cf372d"),   # 13: cosmodrome livery red & beacon
        ("accent-cobalt", "#2655a6"),    # 14: deep aerospace livery blue
        ("gold-foil", "#d9a441"),        # 15: Kapton thermal sensor foil
        ("airlock-steel", "#7d8997"),    # 16: pressurized docking collar steel
    ]
    cells = {}

    def set_c(x, y, z, v):
        if 0 <= x < sx and 0 <= y < sy and 0 <= z < sz:
            if v == 0:
                cells.pop((x, y, z), None)
            else:
                cells[(x, y, z)] = v

    # -------------------------------------------------------------------------
    # 1. CENTRAL 9-NOZZLE ENGINE CLUSTER (y = 0..3)
    # -------------------------------------------------------------------------
    nozzle_offsets = [(0.0, 0.0)] + [
        (4.3 * math.cos(k * math.pi / 4.0), 4.3 * math.sin(k * math.pi / 4.0))
        for k in range(8)
    ]
    for y in range(0, 4):
        bell_r = 1.95 - 0.25 * y
        for ox, oz in nozzle_offsets:
            for x in range(cx - 8, cx + 9):
                for z in range(cz - 8, cz + 9):
                    d = math.hypot((x - cx) - ox, (z - cz) - oz)
                    if d <= bell_r:
                        # Hollow nozzle mouth at y=0, solid chamber above
                        if y == 0 and d < bell_r - 0.85:
                            continue
                        set_c(x, y, z, 11 if y == 2 else 10)

    # -------------------------------------------------------------------------
    # 2. HEAVY CRYOGENIC FIRST STAGE CORE (y = 4..26, R = 6.6)
    # -------------------------------------------------------------------------
    for y in range(4, 27):
        r_core = 6.65 if y < 25 else 6.45
        for x in range(cx - 8, cx + 9):
            for z in range(cz - 8, cz + 9):
                dx, dz = x - cx, z - cz
                d = math.hypot(dx, dz)
                if d <= r_core:
                    mat = 1
                    if y in (4, 5, 25, 26):
                        mat = 4  # dark engine skirt & carbon interstage
                    elif y in (10, 16, 21):
                        mat = 3  # structural weld rings
                    elif y in (14, 15) and d >= r_core - 1.2:
                        mat = 14  # cobalt livery band
                    elif y == 23 and d >= r_core - 1.2:
                        mat = 13  # crimson staging stripe
                    elif (x + z) % 7 == 0 and d >= r_core - 1.0:
                        mat = 2
                    set_c(x, y, z, mat)

        # External LOX/CH4 vertical raceways on +/-Z and +/-X + cryogenic stringer ribs
        for sx_dir, sz_dir in [(0, 1), (0, -1), (1, 0), (-1, 0)]:
            for w in (-1, 0, 1):
                px = cx + sx_dir * 7 + (w if sz_dir != 0 else 0)
                pz = cz + sz_dir * 7 + (w if sx_dir != 0 else 0)
                if sx_dir * sx_dir + sz_dir * sz_dir > 0 and abs(w) <= (0 if y % 4 == 0 else 1):
                    set_c(px, y, pz, 12 if y % 5 != 0 else 15)
            # Folded carbon-titanium reusable landing legs stowed along Stage 1 skirt (y = 4..14)
            if 4 <= y <= 14:
                leg_out = 8 if y <= 9 else 7
                lx = cx + sx_dir * leg_out
                lz = cz + sz_dir * leg_out
                set_c(lx, y, lz, 4 if y < 13 else 13)
                if y in (4, 5, 8):
                    for side_w in (-1, 1):
                        set_c(lx + (side_w if sz_dir != 0 else 0), y, lz + (side_w if sx_dir != 0 else 0), 9)
        # Fine aerodynamic stringers on diagonal Stage-1 hull quadrants
        if 6 <= y <= 23 and y % 2 == 0:
            for ang_deg in (25, 65, 115, 155, 205, 245, 295, 335):
                ang = math.radians(ang_deg)
                rx = int(round(cx + 6.9 * math.cos(ang)))
                rz = int(round(cz + 6.9 * math.sin(ang)))
                set_c(rx, y, rz, 2)

    # 4 Deployed Titanium Lattice Grid-Fins at top of Stage 1 (y = 24..25)
    for sx_dir, sz_dir in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        for step in range(7, 11):
            for w in range(-2, 3):
                for y in (24, 25):
                    px = cx + sx_dir * step + (w if sz_dir != 0 else 0)
                    pz = cz + sz_dir * step + (w if sx_dir != 0 else 0)
                    # Waffle lattice pattern
                    if step in (7, 10) or abs(w) == 2 or (step + w + y) % 2 == 0:
                        set_c(px, y, pz, 9)

    # -------------------------------------------------------------------------
    # 3. FOUR SYMMETRICAL DIAGONAL LIQUID STRAP-ON BOOSTERS (y = 0..25)
    #    Positioned at (+/-7.8, +/-7.8) and ending at y=25 so y>=27 crew airlock
    #    and gantry on -X have 100% unobstructed clearance!
    # -------------------------------------------------------------------------
    booster_dirs = [(-1, -1), (-1, 1), (1, -1), (1, 1)]
    for bx_s, bz_s in booster_dirs:
        bcx = cx + bx_s * 7.8
        bcz = cz + bz_s * 7.8
        # Booster twin engine bells (y = 0..2)
        for y in range(0, 3):
            br = 2.5 - 0.25 * y
            for x in range(int(bcx - 4), int(bcx + 5)):
                for z in range(int(bcz - 4), int(bcz + 5)):
                    d = math.hypot(x - bcx, z - bcz)
                    if d <= br:
                        if y == 0 and d < br - 0.9:
                            continue
                        set_c(x, y, z, 11 if y == 2 else 10)

        # Cylindrical booster tank (y = 3..19) + inward-canted conical nosecone (y = 20..25)
        for y in range(3, 26):
            if y <= 19:
                cur_cx, cur_cz = bcx, bcz
                br = 3.55
            else:
                t = (y - 19) / 6.2
                # Nosecone tapers inward toward the core!
                cur_cx = bcx - bx_s * (t * 2.1)
                cur_cz = bcz - bz_s * (t * 2.1)
                br = max(0.85, 3.55 * (1.0 - t * 0.82))
            for x in range(int(cur_cx - 5), int(cur_cx + 6)):
                for z in range(int(cur_cz - 5), int(cur_cz + 6)):
                    d = math.hypot(x - cur_cx, z - cur_cz)
                    if d <= br:
                        mat = 7
                        if y in (3, 4, 19):
                            mat = 8
                        elif y in (11, 12):
                            mat = 13  # crimson Korolev booster accent
                        elif y >= 23:
                            mat = 4   # dark carbon nose tip
                        set_c(x, y, z, mat)

            # Heavy structural cross-struts tying boosters to central core at y = 6..7 and y = 18..19
            if y in (6, 7, 18, 19):
                for s_t in range(4, 9):
                    sx_p = int(round(cx + bx_s * (s_t * 0.72)))
                    sz_p = int(round(cz + bz_s * (s_t * 0.72)))
                    for ox in (-1, 0, 1):
                        set_c(sx_p + ox, y, sz_p, 9)
                        set_c(sx_p, y, sz_p + ox, 9)

        # Swept aerodynamic stabilizer fin on outer corner of each booster (y = 2..13)
        for y in range(2, 14):
            fin_len = int(round((14 - y) * 0.52))
            for f in range(1, fin_len + 1):
                fx = int(round(bcx + bx_s * (2.5 + f * 0.72)))
                fz = int(round(bcz + bz_s * (2.5 + f * 0.72)))
                set_c(fx, y, fz, 13 if f == fin_len else 8)
                # Secondary vertical winglet tip
                if f == fin_len and y <= 8:
                    set_c(fx, y + 1, fz, 13)

    # -------------------------------------------------------------------------
    # 4. UPPER CREWED ORBITAL SPACECRAFT ('Vulkan-Orel', y = 27..76)
    #    Carefully encloses ShuttleCabinCarving.Remove envelope:
    #    - lower:   y=28..33, dx=-2..2, dz=1..5
    #    - ladder:  y=34..41, dx=-1..1, dz=2..4
    #    - cockpit: y=42..45, dx=-2..2, dz=2..5
    #    - hatch:   y=28..30, dx=-9..-3, dz=2..3
    # -------------------------------------------------------------------------
    for y in range(27, 48):
        r_ship = 6.35 if y < 44 else 6.15 - (y - 44) * 0.12
        for x in range(cx - 8, cx + 9):
            for z in range(cz - 8, cz + 9):
                dx, dz = x - cx, z - cz
                # Ensure dz=6 across dx=-2..2 is clean panoramic hull/windshield
                d = math.hypot(dx, dz)
                in_hull = d <= r_ship or (abs(dx) <= 3 and 0 <= dz <= 6 and y <= 46)
                # Trim anything beyond dz = 6 in front of windshield so dz=6 faces open space!
                if y in (43, 44, 45) and abs(dx) <= 2 and dz >= 7:
                    in_hull = False
                if not in_hull:
                    continue
                mat = 1
                if y in (27, 40, 41):
                    mat = 4  # interstage adapter & service-module separation ring
                elif y in (33, 34):
                    mat = 3
                elif dz <= -4 and d >= r_ship - 1.3:
                    mat = 4  # black ceramic belly heatshield tiles on aft/-Z side
                elif y == 37 and d >= r_ship - 1.1:
                    mat = 14
                elif (x + y) % 9 == 0 and d >= r_ship - 1.0:
                    mat = 2
                set_c(x, y, z, mat)

    # Panoramic Flight-Deck Viewport at dz = 6 (z = 36), y = 43..45, dx = -2..2
    # Guaranteed: (31, 45, 36) = index 5 ('cockpit-window'), dz=7 is air (0),
    # and dz=5 is solid before carving so removedSurface stays strictly on the airlock!
    for y in range(42, 47):
        for dx in range(-3, 4):
            if y in (43, 44, 45) and abs(dx) <= 2:
                set_c(cx + dx, y, cz + 6, 5)  # transparent 'cockpit-window'
                set_c(cx + dx, y, cz + 7, 0)  # clear space outside windshield
            else:
                set_c(cx + dx, y, cz + 6, 6 if abs(dx) <= 2 else 4)
                set_c(cx + dx, y, cz + 7, 0)

    # Portside Pressurized Airlock Docking Collar (dx = -9..-4, y = 27..31, dz = 1..4)
    # Ensures ONLY the 6 doorway cells (dx = -9, y = 28..30, dz = 2..3) touch air (dx = -10)!
    for dx in range(-9, -2):
        for y in range(27, 32):
            for dz in range(1, 5):
                is_frame = (dx == -9 and (y in (27, 31) or dz in (1, 4)))
                mat = 13 if is_frame else (16 if dx <= -7 else 1)
                set_c(cx + dx, y, cz + dz, mat)
    # Guarantee open air outside the airlock portal at dx = -10
    for y in range(26, 33):
        for dz in range(0, 6):
            set_c(cx - 10, y, cz + dz, 0)

    # Ensure ALL 344 cells of ShuttleCabinCarving.Remove are solid in approved model
    # AND none of them (except the 6 doorway cells at dx == -9) have an air neighbor!
    for x in range(sx):
        for y in range(sy):
            for z in range(sz):
                dx, dz = x - cx, z - cz
                lower = 28 <= y <= 33 and -2 <= dx <= 2 and 1 <= dz <= 5
                ladder = 34 <= y <= 41 and -1 <= dx <= 1 and 2 <= dz <= 4
                cockpit = 42 <= y <= 45 and -2 <= dx <= 2 and 2 <= dz <= 5
                hatch = 28 <= y <= 30 and -9 <= dx <= -3 and 2 <= dz <= 3
                if lower or ladder or cockpit or hatch:
                    if cells.get((x, y, z), 0) == 0:
                        set_c(x, y, z, 1)

    # 4 Quad RCS Attitude Thruster Pods at y = 47..49 + Service Module Radiator Strakes (y = 29..39)
    for sx_dir, sz_dir in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        for y in (47, 48, 49):
            px, pz = cx + sx_dir * 6, cz + sz_dir * 6
            set_c(px, y, pz, 4 if y != 48 else 15)
    # Starboard (+X) & Aft (-Z) Service Module Radiator / Solar Louver Pods (avoids -X airlock & +Z windshield!)
    for y in range(29, 39):
        for w in (-3, -1, 1, 3):
            set_c(cx + 7, y, cz + w, 14 if y % 2 == 0 else 9)
            set_c(cx + w, y, cz - 7, 12 if y % 3 != 0 else 15)
            if y % 2 == 0:
                set_c(cx + 8, y, cz + w, 14)
                set_c(cx + w, y, cz - 8, 12)
    # Nosecone corrugated thermal expansion strakes (y = 50..64)
    for y in range(50, 65, 2):
        t = (y - 48) / 22.0
        r_s = max(1.5, 5.95 * math.cos(t * math.pi * 0.44)) + 0.65
        for sx_dir, sz_dir in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
            set_c(int(round(cx + sx_dir * r_s)), y, int(round(cz + sz_dir * r_s)), 3)

    # -------------------------------------------------------------------------
    # 5. OGIVE AERODYNAMIC SPACECRAFT NOSECONE & ESCAPE SPIRE (y = 48..76)
    # -------------------------------------------------------------------------
    for y in range(48, 70):
        t = (y - 48) / 22.0
        # Tangent-ogive profile
        r_nose = max(1.15, 5.95 * math.cos(t * math.pi * 0.44))
        for x in range(cx - 7, cx + 8):
            for z in range(cz - 7, cz + 8):
                dx, dz = x - cx, z - cz
                d = math.hypot(dx, dz)
                if d <= r_nose:
                    mat = 1
                    if y >= 66:
                        mat = 4  # black carbon-silicon nose cap
                    elif y in (53, 60):
                        mat = 3
                    elif y == 55 and d >= r_nose - 1.1:
                        mat = 13
                    set_c(x, y, z, mat)

    # Launch Escape / Orbital Docking Tower Spire (y = 70..76) + 4 LES Abort Nozzles (y = 73)
    for y in range(70, 76):
        r_spire = 1.45 if y < 73 else 0.85
        for x in range(cx - 2, cx + 3):
            for z in range(cz - 2, cz + 3):
                if math.hypot(x - cx, z - cz) <= r_spire:
                    set_c(x, y, z, 12 if y < 74 else 4)
    for sx_dir, sz_dir in [(1, 0), (-1, 0), (0, 1), (0, -1)]:
        set_c(cx + sx_dir * 2, 72, cz + sz_dir * 2, 13)
        set_c(cx + sx_dir * 2, 73, cz + sz_dir * 2, 10)
    # Booster hot-staging / separation ullage pods + external booster feedlines
    for bx_s, bz_s in booster_dirs:
        bcx_i, bcz_i = int(round(cx + bx_s * 7.8)), int(round(cz + bz_s * 7.8))
        for y in range(5, 19, 2):
            set_c(bcx_i + bx_s * 4, y, bcz_i, 12)
            set_c(bcx_i, y, bcz_i + bz_s * 4, 12)
    # Top navigation beacon
    set_c(cx, 76, cz, 13)

    # Verify ShuttleCabinCarving invariants right here!
    def is_remove(x, y, z):
        dx, dz = x - cx, z - cz
        return (
            (28 <= y <= 33 and -2 <= dx <= 2 and 1 <= dz <= 5)
            or (34 <= y <= 41 and -1 <= dx <= 1 and 2 <= dz <= 4)
            or (42 <= y <= 45 and -2 <= dx <= 2 and 2 <= dz <= 5)
            or (28 <= y <= 30 and -9 <= dx <= -3 and 2 <= dz <= 3)
        )

    carved_count = 0
    surface_removed = []
    for (x, y, z) in list(cells.keys()):
        if is_remove(x, y, z):
            carved_count += 1
            if any(
                cells.get((x + nx, y + ny, z + nz), 0) == 0
                for nx, ny, nz in [(1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)]
            ):
                surface_removed.append((x - cx, y, z - cz))

    assert carved_count == 344, f"Expected 344 carved cabin cells, got {carved_count}"
    assert len(surface_removed) == 6, f"Expected 6 hatch surface cells, got {len(surface_removed)}: {surface_removed}"
    assert cells.get((31, 45, 36), 0) == 5, "Cockpit window at (31,45,36) missing!"

    out = {
        "name": "Vulkan-Heavy multi-stage orbital launch vehicle & Orel deep-space crewed spacecraft",
        "size": [sx, sy, sz],
        "palette": [{"name": n, "hex": h} for n, h in palette],
        "runs": encode_runs(cells, sx, sy, sz),
    }
    SHUTTLE_PATH.write_text(json.dumps(out, separators=(",", ":")), encoding="utf-8")
    print(f"Rocket built: solid={len(cells)}, carved={carved_count}, fitted={len(cells) - carved_count}, hatch_surface={len(surface_removed)}")
    return len(cells), len(cells) - carved_count


def build_cosmodrome_pad():
    sx, sy, sz = 94, 40, 90
    cx, cz = 47, 45
    palette = [
        ("air", "#000000"),
        ("concrete-apron", "#6e747c"),     # 1: main blast apron concrete
        ("concrete-dark", "#484e56"),      # 2: beveled concrete foundation & Blast walls
        ("refractory-brick", "#2b2625"),   # 3: flame-trench heat-resistant basalt tiles
        ("mount-steel", "#363f4b"),        # 4: heavy octagonal launch mount steel
        ("tower-truss", "#566373"),        # 5: umbilical service tower structural steel
        ("tower-deck", "#84909e"),         # 6: walkable grated steel service decks
        ("hazard-yellow", "#e3b322"),      # 7: cosmodrome safety curbing & clamps
        ("hazard-black", "#1f2329"),       # 8: hazard stripe contrast
        ("cryo-tank-white", "#edf2f7"),    # 9: spherical LOX / LCH4 Horton tanks
        ("cryo-pipe", "#b8c4d0"),          # 10: insulated cryogenic transfer lines
        ("umbilical-red", "#c8382e"),      # 11: swing-arm gantry & aviation beacons
        ("floodlight-glow", "#dceeff"),    # 12: high-mast xenon pad floodlights
        ("blast-deflector", "#3b3532"),    # 13: angled exhaust flame deflector wedge
    ]
    cells = {}

    def set_c(x, y, z, v):
        if 0 <= x < sx and 0 <= y < sy and 0 <= z < sz:
            if v == 0:
                cells.pop((x, y, z), None)
            else:
                cells[(x, y, z)] = v

    # -------------------------------------------------------------------------
    # 1. ELEVATED REINFORCED COSMODROME BLAST APRON (y = 0..4 -> top surface Y = 5!)
    # -------------------------------------------------------------------------
    for y in range(0, 5):
        inset = (4 - y) * 1  # sloped perimeter embankment: wider at y=0, crisp at y=4
        rx = 43 - inset
        rz = 40 - inset
        # Actually wider at bottom (y=0 -> rx=44, y=4 -> rx=40)
        rx = 44 - y
        rz = 41 - y
        for x in range(cx - rx, cx + rx + 1):
            for z in range(cz - rz, cz + rz + 1):
                dx, dz = x - cx, z - cz
                # Chamfered octagonal corners
                if abs(dx) + abs(dz) > rx + rz - 14:
                    continue
                # Deep central Flame Trench running North-South (dx in -6..6, dz in -34..34, y = 1..4)
                if y >= 1 and abs(dx) <= 6 and abs(dz) <= 34:
                    # Sloped flame trench side walls
                    if abs(dx) == 6 and y <= 2:
                        set_c(x, y, z, 3)
                    continue
                mat = 2 if y < 3 else 1
                if y == 4:
                    # Perimeter hazard safety curbing
                    if abs(dx) == rx - 1 or abs(dz) == rz - 1 or abs(dx) == 7 and abs(dz) <= 34:
                        mat = 7 if ((x + z) // 2) % 2 == 0 else 8
                    elif (x % 8 == 0 or z % 8 == 0):
                        mat = 2  # expansion joints on concrete deck
                set_c(x, y, z, mat)

    # Flame trench floor (y = 0) & dual-wedge flame deflector directly beneath rocket (y = 1..3)
    for z in range(cz - 34, cz + 35):
        dz = z - cz
        for x in range(cx - 6, cx + 7):
            set_c(x, 0, z, 3)
            # Central V-wedge exhaust splitter beneath nozzles (dz in -6..6)
            if abs(dz) <= 6:
                h_def = max(0, 3 - abs(dz) // 2)
                for y in range(1, h_def + 1):
                    set_c(x, y, z, 13)

    # -------------------------------------------------------------------------
    # 2. HEAVY OCTAGONAL STEEL LAUNCH TABLE & 4 HYDRAULIC HOLD-DOWN CLAMPS
    #    Spans over the flame trench around (cx, cz) at y = 4..6 with open central
    #    exhaust ring (r < 5.5) so rocket plumes roar straight into the trench!
    # -------------------------------------------------------------------------
    for y in range(3, 7):
        for x in range(cx - 13, cx + 14):
            for z in range(cz - 13, cz + 14):
                dx, dz = x - cx, z - cz
                d = math.hypot(dx, dz)
                if 5.6 <= d <= 12.6 and abs(dx) + abs(dz) <= 18:
                    if y <= 4 or (y == 5 and d <= 11.5) or (y == 6 and 6.2 <= d <= 8.4):
                        mat = 4
                        if y == 6 and (abs(dx) <= 1 or abs(dz) <= 1):
                            mat = 7
                        set_c(x, y, z, mat)

    # 4 Diagonal Hold-Down Clamp Arms cradling the 4 strap-on boosters
    for bx_s, bz_s in [(-1, -1), (-1, 1), (1, -1), (1, 1)]:
        for step in range(9, 13):
            px = int(round(cx + bx_s * step))
            pz = int(round(cz + bz_s * step))
            for y in range(5, 9 if step >= 11 else 7):
                set_c(px, y, pz, 7 if y == 8 else 4)

    # -------------------------------------------------------------------------
    # 3. UMBILICAL & CREW ACCESS SERVICE TOWER (dx = -23..-12, dz = -4..+8, y = 5..39)
    #    Preserves ALL elevator & walkable deck invariants:
    #    - Ground pad surface at (27,4,48) solid, (27,5,48) air (dx=-20, dz=+3)
    #    - Lift shaft clear at dx = -21..-19, dz = 1..4, y = 5..28
    #    - Upper boarding deck solid at y = 29 (surface Y = 30), including (33,29,48) solid / (33,30,48) air
    #    - Exact 6-cell lift well at y=29, dx=-21..-19, dz=2..3 (solid in raw, opened by LaunchPadLiftOpening)
    #    - Exact 4-cell doorway at dx=-13, y=30..31, dz=2..3 (solid in raw, opened by LaunchPadLiftOpening)
    # -------------------------------------------------------------------------
    tx0, tx1 = cx - 23, cx - 13  # x = 24..34
    tz0, tz1 = cz - 3, cz + 7    # z = 42..52 (includes dz = 2..3 -> z = 47..48!)

    # Ensure ground pad under tower & approach (dx = -25..-12, dz = -5..+9) is solid at y=4, clear at y=5
    for x in range(cx - 26, cx - 10):
        for z in range(cz - 6, cz + 10):
            set_c(x, 4, z, 1)

    for y in range(5, 39):
        for x in range(tx0, tx1 + 1):
            for z in range(tz0, tz1 + 1):
                dx, dz = x - cx, z - cz
                # Keep ground approach & elevator shaft interior completely clear for y = 5..28!
                if y <= 28 and -22 <= dx <= -18 and 0 <= dz <= 5:
                    continue
                # Keep upper crew boarding corridor at y = 30..33 clear except the 4 doorway cells at dx = -13!
                if 30 <= y <= 33 and -22 <= dx <= -14 and 1 <= dz <= 4:
                    continue

                # Upper walkable gantry floor at y = 29 (walking height Y = 30!)
                if y == 29:
                    set_c(x, y, z, 6)
                    continue

                # Intermediate structural floors (outside lift shaft) every 6m + roof at y = 35
                if y in (11, 17, 23, 35) and not (-22 <= dx <= -18 and 0 <= dz <= 5):
                    set_c(x, y, z, 6)
                    continue

                # 4 Corner columns + mid-columns + diagonal X-bracing on outer faces of tower
                on_x_face = x in (tx0, tx1)
                on_z_face = z in (tz0, tz1)
                if on_x_face and on_z_face:
                    set_c(x, y, z, 5)  # heavy corner column
                elif (on_x_face and z == cz + 1) or (on_z_face and x == cx - 18):
                    if y <= 28 and x == cx - 18 and z == tz1:
                        set_c(x, y, z, 5)
                    elif not (y <= 28 and z == tz0 and -21 <= dx <= -19):
                        set_c(x, y, z, 5)
                elif on_x_face or on_z_face:
                    # Leave ground entry open on -X / -Z into the lift!
                    if y <= 9 and -22 <= dx <= -18 and 0 <= dz <= 5:
                        continue
                    # Leave rocket-side boarding opening at y = 30..33, dx = -13 clear EXCEPT exact door!
                    if dx == -13 and 30 <= y <= 33 and 1 <= dz <= 4:
                        continue
                    # Safety railings at y = 30..31 around upper deck
                    if y == 30:
                        set_c(x, y, z, 7)
                    # Crisp X-bracing along tower faces
                    elif ((y + x + z) % 6 == 0) or ((y - x - z) % 6 == 0):
                        set_c(x, y, z, 5)

    # Vertical cryogenic riser pipes up the rocket-facing side of the tower
    for y in range(5, 36):
        set_c(cx - 14, y, cz - 2, 10)
        set_c(cx - 14, y, cz + 6, 10)

    # Cantilevered Crew Boarding Gantry Bridge at y = 29 (surface Y = 30) from tower (dx=-13) to rocket airlock (dx=-9)
    for dx in range(-13, -9):
        for dz in range(1, 5):
            set_c(cx + dx, 29, cz + dz, 6)
            # Side safety handrails on dz = 1 and dz = 4 (leaving dz = 2..3 open walkway!)
            if dz in (1, 4):
                set_c(cx + dx, 30, cz + dz, 11)

    # Enforce exact LaunchPadLiftOpening 10-cell contract:
    # 1) 6-cell lift well at y = 29, dx = -21..-19, dz = 2..3 MUST be non-zero in raw, with y=30 above it air (0)
    for dx in range(-21, -18):
        for dz in (2, 3):
            set_c(cx + dx, 29, cz + dz, 6)
            for y_clear in range(30, 34):
                set_c(cx + dx, y_clear, cz + dz, 0)
    # 2) 4-cell tower doorway at dx = -13, y = 30..31, dz = 2..3 MUST be non-zero in raw, with y=32..33 air
    for y_door in (30, 31):
        for dz in (2, 3):
            set_c(cx - 13, y_door, cz + dz, 11)
    for y_clear in (32, 33):
        for dz in (2, 3):
            set_c(cx - 13, y_clear, cz + dz, 0)

    # Guarantee (27,4,48) solid / (27,5,48) air AND (33,29,48) solid / (33,30,48) air!
    set_c(27, 4, 48, 1)
    set_c(27, 5, 48, 0)
    set_c(33, 29, 48, 6)
    set_c(33, 30, 48, 0)

    # Upper Retractable Cryo Umbilical Boom at y = 35..36 + Lightning Mast at y = 36..39
    for dx in range(-13, -6):
        set_c(cx + dx, 35, cz, 11)
        set_c(cx + dx, 36, cz, 10)
    for y in range(36, 40):
        set_c(cx - 18, y, cz + 2, 11 if y == 39 else 5)

    # -------------------------------------------------------------------------
    # 4. CRYOGENIC PROPELLANT FARM (Starboard +X side: Twin Horton Spheres + Pipe Rack)
    # -------------------------------------------------------------------------
    for sphere_z in (cz - 15, cz + 15):
        scx, scy, scz = cx + 27, 13, sphere_z
        # 4 steel support legs from apron (y=5) up to sphere equator (y=13)
        for lx, lz in [(-4, -4), (-4, 4), (4, -4), (4, 4)]:
            for y in range(5, 13):
                set_c(scx + lx, y, scz + lz, 5)
        # Gleaming white Horton cryogenic sphere (R = 6.2) with equatorial ring
        for x in range(scx - 7, scx + 8):
            for y in range(scy - 7, scy + 8):
                for z in range(scz - 7, scz + 8):
                    d = math.sqrt((x - scx) ** 2 + (y - scy) ** 2 + (z - scz) ** 2)
                    if d <= 6.2 and y >= 6:
                        mat = 11 if y == scy and scz < cz else (7 if y == scy else 9)
                        set_c(x, y, z, mat)

    # Insulated Cryogenic Transfer Pipelines running from both spheres to the Launch Mount
    for z in range(cz - 15, cz + 16):
        set_c(cx + 20, 6, z, 10)
        set_c(cx + 20, 7, z, 10)
        if z % 5 == 0:
            set_c(cx + 20, 5, z, 5)
    for x in range(cx + 11, cx + 27):
        set_c(x, 6, cz - 3, 10)
        set_c(x, 6, cz + 3, 10)
        if x % 4 == 0:
            set_c(x, 5, cz - 3, 5)
            set_c(x, 5, cz + 3, 5)

    # -------------------------------------------------------------------------
    # 5. FOUR HIGH-MAST COSMODROME FLOODLIGHT TOWERS AT APRON CORNERS
    # -------------------------------------------------------------------------
    for fx, fz in [(cx - 34, cz - 32), (cx - 34, cz + 32), (cx + 34, cz - 32), (cx + 34, cz + 32)]:
        for y in range(5, 31):
            for ox in (0, 1):
                for oz in (0, 1):
                    if y < 28 and (ox + oz + y) % 2 == 0 or y >= 28:
                        set_c(fx + ox, y, fz + oz, 5)
        # Floodlight bank facing inward toward the rocket
        in_x = -1 if fx > cx else 2
        in_z = -1 if fz > cz else 2
        for y in (29, 30):
            for w in (-1, 0, 1, 2):
                set_c(fx + in_x, y, fz + w, 12)
                set_c(fx + w, y, fz + in_z, 12)
        set_c(fx, 31, fz, 11)

    # Verify LaunchPadLiftOpening invariants right here!
    opening_changes = 0
    for (x, y, z) in list(cells.keys()):
        dx, dz = x - cx, z - cz
        well = y == 29 and -21 <= dx <= -19 and 2 <= dz <= 3
        door = dx == -13 and y in (30, 31) and 2 <= dz <= 3
        if well or door:
            opening_changes += 1
    assert opening_changes == 10, f"Expected 10 lift opening cells, got {opening_changes}"
    assert cells.get((27, 4, 48), 0) != 0 and cells.get((27, 5, 48), 0) == 0, "Ground pad walk check failed!"
    assert cells.get((33, 29, 48), 0) != 0 and cells.get((33, 30, 48), 0) == 0, "Upper tower walk check failed!"
    for p_entry in palette:
        assert "crawler" not in p_entry[0].lower() and "transporter" not in p_entry[0].lower()

    out = {
        "name": "Baikonur-Nova Cosmodrome Complex - flame trench, octagonal launch table, umbilical tower, cryo spheres & floodlights",
        "size": [sx, sy, sz],
        "palette": [{"name": n, "hex": h} for n, h in palette],
        "runs": encode_runs(cells, sx, sy, sz),
    }
    PAD_PATH.write_text(json.dumps(out, separators=(",", ":")), encoding="utf-8")
    print(f"Cosmodrome Pad built: raw_solid={len(cells)}, lift_opened={len(cells) - opening_changes}")
    return len(cells), len(cells) - opening_changes


if __name__ == "__main__":
    r_solid, r_fitted = build_heavy_rocket()
    p_solid, p_opened = build_cosmodrome_pad()
    print(f"SUMMARY: Shuttle raw={r_solid} fitted={r_fitted} | LaunchPad raw={p_solid} opened={p_opened}")
