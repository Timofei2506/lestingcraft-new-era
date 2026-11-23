#!/usr/bin/env python3
"""Completely rebuilt Tsardom / Imperial Capital landmark authored from scratch.
Emits Assets/Scripts/World/KingdomPlan.cs with bucketed voxel boxes (16x16 local cells)
and fixes the local (dx, dz) coordinate lookup in KingdomPlan.TrySample.
"""
from pathlib import Path
import math

r = Path(__file__).resolve().parents[1]
CX, CZ = 420, -340          # world center of the Imperial Citadel
CORE = 122                  # flat core radius; blend 122..150
REGION = 150

boxes = []                  # [x0, x1, y0, y1, z0, z1, bid] in LOCAL [-CORE..CORE] coords
roads = []                  # (ax, az, bx, bz, radius) in WORLD coords

# Block IDs
AIR = 0
DIRT = 2
GRASS = 3
OAK_LOG = 4
LEAVES = 5
WATER = 7
CRYSTAL = 11
CAMPFIRE = 15
WORKBENCH = 16
FURNACE = 17
PLANK = 18
GOLD_ORE = 22
GRANITE = 27
LIMESTONE = 28
SLATE = 29
MARBLE = 30
GRAVEL = 32
MOSSY = 35
COBBLE = 36
BRICK = 37
CLAY_BRICK = 39
POLISHED_STONE = 40
POLISHED_GRANITE = 41
POLISHED_MARBLE = 42
SANDSTONE = 43
GLASS = 44
COAL_BLOCK = 45
COPPER_BLOCK = 46
TORCH = 54
BED = 55
LANTERN = 56
BOOKSHELF = 57
CARVED_OAK = 58
SHINGLES = 59
SAKURA = 60
TERRACOTTA = 62
GENERATOR = 74


def b(x0, x1, y0, y1, z0, z1, bid):
    x0, x1 = int(x0), int(x1)
    y0, y1 = int(y0), int(y1)
    z0, z1 = int(z0), int(z1)
    if x0 > x1 or y0 > y1 or z0 > z1:
        return
    boxes.append([x0, x1, y0, y1, z0, z1, int(bid)])


def ring(x0, x1, z0, z1, y0, y1, bid):
    b(x0, x1, y0, y1, z0, z0, bid)
    b(x0, x1, y0, y1, z1, z1, bid)
    b(x0, x0, y0, y1, z0 + 1, z1 - 1, bid)
    b(x1, x1, y0, y1, z0 + 1, z1 - 1, bid)


def disc(cx, cz, rad, y0, y1, bid):
    ri = int(rad)
    for z in range(cz - ri, cz + ri + 1):
        span = int((rad * rad - (z - cz) ** 2) ** 0.5) if abs(z - cz) <= rad else -1
        if span >= 0:
            b(cx - span, cx + span, y0, y1, z, z, bid)


def cyl_shell(cx, cz, r_out, r_in, y0, y1, bid):
    disc(cx, cz, r_out, y0, y1, bid)
    if r_in > 0:
        disc(cx, cz, r_in, y0, y1, AIR)


def road(ax, az, bx, bz, rad=3):
    roads.append((int(ax), int(az), int(bx), int(bz), int(rad)))


def lantern_post(x, z, y=1):
    """Slender 1x1 wrought-stone and carved-oak street bollard (no T-hammer crossbar)."""
    b(x, x, y, y, z, z, POLISHED_STONE)
    b(x, x, y + 1, y + 2, z, z, CARVED_OAK)
    b(x, x, y + 3, y + 3, z, z, LANTERN)


def tree(x, z, h=5, leaf=LEAVES):
    b(x, x, 1, h, z, z, OAK_LOG)
    disc(x, z, 2.5, h, h + 1, leaf)
    disc(x, z, 1.5, h + 2, h + 3, leaf)


def bastion_tower(cx, cz, rad=5.2, h=22, spire_mat=SHINGLES):
    cyl_shell(cx, cz, rad, rad - 1.8, 1, h, BRICK)
    disc(cx, cz, rad + 0.5, h - 2, h - 2, POLISHED_STONE)
    disc(cx, cz, rad + 0.6, h, h, POLISHED_MARBLE)
    for fy in (1, 7, 13, h - 1):
        disc(cx, cz, rad - 1.6, fy, fy, PLANK)
    for wy in (4, 9, 15):
        if wy + 2 < h - 2:
            b(cx, cx, wy, wy + 1, int(cz - rad), int(cz + rad), GLASS)
            b(int(cx - rad), int(cx + rad), wy, wy + 1, cz, cz, GLASS)
    steps = int(rad) + 2
    for k in range(steps):
        disc(cx, cz, max(0.8, rad + 0.4 - k * 0.8), h + 1 + k, h + 1 + k, spire_mat)
    top_y = h + 1 + steps
    mast_top = min(45, top_y + 3)
    b(cx, cx, top_y, mast_top, cz, cz, OAK_LOG)
    b(cx + 1, cx + 2, mast_top, mast_top, cz, cz, CARVED_OAK)
    b(cx + 1, cx + 2, max(top_y, mast_top - 2), max(top_y, mast_top - 1), cz, cz, TERRACOTTA)


def town_house(cx, cz, hw=5, hd=4, stories=1, roof_id=SHINGLES, wall_id=LIMESTONE, face_z=1):
    """Multi-style town architecture: 1-storey artisan cottage or 2-storey jettied merchant townhouse."""
    wall_h = 4 if stories == 1 else 9
    b(cx - hw, cx + hw, 0, 0, cz - hd, cz + hd, COBBLE)
    b(cx - hw + 1, cx + hw - 1, 0, 0, cz - hd + 1, cz + hd - 1, PLANK)
    ring(cx - hw, cx + hw, cz - hd, cz + hd, 1, 1, BRICK)
    ring(cx - hw, cx + hw, cz - hd, cz + hd, 2, 4, wall_id)
    b(cx - hw + 1, cx + hw - 1, 1, wall_h, cz - hd + 1, cz + hd - 1, AIR)
    for px in (cx - hw, cx, cx + hw):
        for pz in (cz - hd, cz + hd):
            b(px, px, 1, 4, pz, pz, OAK_LOG)
    if stories >= 2:
        # Jettied timber-framed second floor overhanging by 1 block on front & back
        ring(cx - hw, cx + hw, cz - hd - 1, cz + hd + 1, 5, 5, CARVED_OAK)
        b(cx - hw + 1, cx + hw - 1, 5, 5, cz - hd, cz + hd, PLANK)
        ring(cx - hw, cx + hw, cz - hd - 1, cz + hd + 1, 6, 9, PLANK if wall_id == LIMESTONE else LIMESTONE)
        b(cx - hw + 1, cx + hw - 1, 6, 9, cz - hd, cz + hd, AIR)
        for px in (cx - hw, cx, cx + hw):
            for pz in (cz - hd - 1, cz + hd + 1):
                b(px, px, 5, 9, pz, pz, OAK_LOG)
        # Interior stair along side wall (6-connected stringer, leaving center cx,cz clear!)
        for s in range(5):
            sz_step = cz - hd + 1 + min(hd * 2 - 2, s)
            b(cx - hw + 1, cx - hw + 1, max(1, s), 1 + s, sz_step, sz_step, PLANK)
            if s < 4:
                b(cx - hw + 1, cx - hw + 1, 5, 5, cz - hd + 1 + s, cz - hd + 1 + s, AIR)
        for wx in (cx - hw + 2, cx + hw - 2):
            b(wx, wx, 7, 8, cz - hd - 1, cz - hd - 1, GLASS)
            b(wx, wx, 7, 8, cz + hd + 1, cz + hd + 1, GLASS)
    for wx in (cx - hw + 2, cx + hw - 2):
        b(wx, wx, 2, 3, cz - hd, cz - hd, GLASS)
        b(wx, wx, 2, 3, cz + hd, cz + hd, GLASS)
    # Pitched gable roof along X
    rz_ext = hd + (2 if stories >= 2 else 1)
    for k in range(hw + 2):
        x0, x1 = cx - hw - 1 + k, cx + hw + 1 - k
        yy = wall_h + 1 + k
        if x0 > x1:
            break
        b(x0, x1, yy, yy, cz - rz_ext, cz + rz_ext, roof_id)
        if x0 + 1 <= x1 - 1 and k < hw + 1:
            b(x0 + 1, x1 - 1, yy, yy, cz - rz_ext + 1, cz - rz_ext + 1, PLANK)
            b(x0 + 1, x1 - 1, yy, yy, cz + rz_ext - 1, cz + rz_ext - 1, PLANK)
    b(cx, cx, wall_h + hw + 2, wall_h + hw + 2, cz - rz_ext, cz + rz_ext, CARVED_OAK)
    # Brick chimney on rear corner
    back_z = cz - hd if face_z > 0 else cz + hd
    door_wall_z = cz + hd if face_z > 0 else cz - hd
    b(cx + hw - 1, cx + hw, 1, wall_h + hw + 3, back_z, back_z, CLAY_BRICK)
    # Furnishings & grounded lamps tucked into corners (center cx,cz stays 100% open!)
    iz = back_z + (1 if face_z > 0 else -1)
    iz2 = iz + (1 if face_z > 0 else -1)
    b(cx - hw + 2, cx - hw + 2, 1, 1, iz, iz, BED)
    b(cx + hw - 2, cx + hw - 2, 1, 1, iz, iz, FURNACE if (cx + cz) % 2 == 0 else WORKBENCH)
    b(cx + hw - 2, cx + hw - 2, 1, 1, iz2, iz2, CARVED_OAK)
    b(cx + hw - 2, cx + hw - 2, 2, 2, iz2, iz2, LANTERN)
    # 2x3 Doorway + porch step + grounded porch bollard
    b(cx - 1, cx, 1, 3, door_wall_z, door_wall_z, AIR)
    b(cx - 1, cx + 2, 0, 0, door_wall_z + face_z, door_wall_z + face_z, POLISHED_STONE)
    b(cx + 2, cx + 2, 1, 1, door_wall_z + face_z, door_wall_z + face_z, CARVED_OAK)
    b(cx + 2, cx + 2, 2, 2, door_wall_z + face_z, door_wall_z + face_z, LANTERN)


# ==============================================================================
# 1. IMPERIAL CITADEL & CATHEDRAL-PALACE (local -38..38)
# ==============================================================================
def build_imperial_keep():
    # Base lawn across the entire flat core so roads/gardens blend naturally
    b(-CORE, CORE, 0, 0, -CORE, CORE, GRASS)
    # Concentric ring cobblestone boulevards (drawn outer-to-inner with grass parks between!)
    disc(0, 0, 92, 0, 0, COBBLE)
    disc(0, 0, 88, 0, 0, GRASS)
    disc(0, 0, 68, 0, 0, COBBLE)
    disc(0, 0, 64, 0, 0, GRASS)
    disc(0, 0, 46, 0, 0, COBBLE)
    disc(0, 0, 41, 0, 0, GRASS)
    # Grand radial cardinal boulevards crossing all 3 ring avenues
    b(-4, 4, 0, 0, -112, 118, COBBLE)
    b(-112, 112, 0, 0, -4, 4, COBBLE)
    b(-2, 2, 0, 0, -110, 118, POLISHED_STONE)
    b(-110, 110, 0, 0, -2, 2, POLISHED_STONE)

    # Citadel Inner Courtyard Pavement (-35..35)
    b(-35, 35, 0, 0, -35, 35, POLISHED_STONE)
    ring(-35, 35, -35, 35, 0, 0, POLISHED_GRANITE)
    b(-4, 4, 0, 0, -12, 38, POLISHED_MARBLE)

    # Double-faced Machicolated Curtain Walls (x/z = ±36, height = 14)
    for w in (-36, -35, 35, 36):
        b(-36, 36, 1, 14, w, w, BRICK)
        b(w, w, 1, 14, -36, 36, BRICK)
    for k in range(-30, 31, 6):
        b(k, k + 1, 1, 13, -37, -37, POLISHED_STONE)
        b(k, k + 1, 1, 13, 37, 37, POLISHED_STONE)
        b(-37, -37, 1, 13, k, k + 1, POLISHED_STONE)
        b(37, 37, 1, 13, k, k + 1, POLISHED_STONE)
    ring(-37, 37, -37, 37, 14, 14, POLISHED_MARBLE)
    for k in range(-36, 37, 3):
        b(k, min(37, k + 1), 15, 16, -37, -37, BRICK)
        b(k, min(37, k + 1), 15, 16, 37, 37, BRICK)
        b(-37, -37, 15, 16, k, min(37, k + 1), BRICK)
        b(37, 37, 15, 16, k, min(37, k + 1), BRICK)

    # 4 Grand Corner Bastion Towers
    for tx, tz in ((-35, -35), (35, -35), (-35, 35), (35, 35)):
        bastion_tower(tx, tz, rad=5.5, h=22, spire_mat=SHINGLES)

    # Southern Twin-Turret Barbican Gatehouse (z = 33..39; keeping (0, 38) clear for guard spawn!)
    bastion_tower(-9, 36, rad=4.0, h=19, spire_mat=TERRACOTTA)
    bastion_tower(9, 36, rad=4.0, h=19, spire_mat=TERRACOTTA)
    b(-5, 5, 8, 15, 34, 38, BRICK)
    b(-5, 5, 15, 16, 33, 39, POLISHED_MARBLE)
    b(-4, 4, 1, 7, 33, 40, AIR)
    for px in (-3, -1, 1, 3):
        b(px, px, 7, 7, 36, 36, OAK_LOG)
    lantern_post(-5, 41, 1)
    lantern_post(5, 41, 1)

    # ---- 45-METER IMPERIAL CATHEDRAL-PALACE (-15..15, z = -16..14) ----
    b(-15, 15, 1, 1, -16, 14, POLISHED_GRANITE)
    b(-14, 14, 2, 20, -15, 13, BRICK)
    b(-13, 13, 2, 19, -14, 12, AIR)
    # Marble corner & bay buttress pilasters
    for bx in (-14, -8, -3, 3, 8, 14):
        b(bx, bx, 2, 21, -15, -15, POLISHED_MARBLE)
        b(bx, bx, 2, 21, 13, 13, POLISHED_MARBLE)
    for bz in (-15, -9, -3, 3, 8, 13):
        b(-14, -14, 2, 21, bz, bz, POLISHED_MARBLE)
        b(14, 14, 2, 21, bz, bz, POLISHED_MARBLE)
    ring(-15, 15, -16, 14, 8, 8, POLISHED_MARBLE)
    ring(-15, 15, -16, 14, 14, 14, POLISHED_MARBLE)
    ring(-15, 15, -16, 14, 20, 20, POLISHED_MARBLE)

    # Stained-crystal lancet windows + circular rose window above south portico
    for wy in (4, 10, 16):
        for wx in (-11, -6, 5, 10):
            b(wx, wx + 1, wy, wy + 2, -15, -15, GLASS)
            b(wx, wx + 1, wy, wy + 2, 13, 13, GLASS)
        for wz in (-12, -6, 0, 5, 10):
            b(-14, -14, wy, wy + 2, wz, wz + 1, GLASS)
            b(14, 14, wy, wy + 2, wz, wz + 1, GLASS)
    b(-2, 2, 10, 13, 13, 13, GLASS)

    # Grand South Palace Portico & Steps (z = 13..18)
    for s in range(2):
        b(-5 + s, 5 - s, 1, 1 + s, 17 - s * 2, 18 - s * 2, POLISHED_MARBLE)
    for col_x in (-5, -3, 3, 5):
        b(col_x, col_x, 2, 8, 14, 14, POLISHED_MARBLE)
    b(-6, 6, 9, 9, 13, 15, POLISHED_GRANITE)
    b(-2, 2, 2, 7, 12, 15, AIR)

    # Interior Throne Room, Red Carpet, Galleries & Grand Staircases
    b(-13, 13, 1, 1, -14, 12, POLISHED_MARBLE)
    b(-2, 2, 1, 1, -11, 12, TERRACOTTA)
    # Imperial Throne Dais at north end
    b(-4, 4, 2, 2, -13, -9, POLISHED_GRANITE)
    b(-2, 2, 3, 3, -13, -11, POLISHED_MARBLE)
    b(-1, 1, 4, 7, -13, -13, COPPER_BLOCK)
    b(0, 0, 8, 8, -13, -13, CRYSTAL)
    for col_z in (-8, -3, 2, 7):
        b(-6, -6, 2, 13, col_z, col_z, POLISHED_MARBLE)
        b(6, 6, 2, 13, col_z, col_z, POLISHED_MARBLE)
    # Second & Third Choir/Library Galleries with open central atrium & twin 6-connected staircases
    for fy in (8, 14):
        b(-13, 13, fy, fy, -14, 12, PLANK)
        b(-4, 4, fy, fy, -6, 5, AIR)
        ring(-5, 5, -7, 6, fy + 1, fy + 1, CARVED_OAK)
        for sx in (-12, 11):
            b(sx, sx + 1, fy + 1, fy + 3, -12, 8, BOOKSHELF)
    for i in range(7):
        b(-11, -9, max(2, 1 + i), 2 + i, -4 + i, -4 + i, POLISHED_MARBLE)
        b(-11, -9, 3 + i, 6 + i, -4 + i, -4 + i, AIR)
        b(9, 11, max(8, 7 + i), 8 + i, -4 + i, -4 + i, POLISHED_MARBLE)
        b(9, 11, 9 + i, 12 + i, -4 + i, -4 + i, AIR)
    for cx_lamp in (-4, 4):
        b(cx_lamp, cx_lamp, 2, 3, 8, 8, POLISHED_MARBLE)
        b(cx_lamp, cx_lamp, 4, 4, 8, 8, LANTERN)
        for fy in (8, 14):
            b(cx_lamp, cx_lamp, fy + 1, fy + 2, 8, 8, POLISHED_MARBLE)
            b(cx_lamp, cx_lamp, fy + 3, fy + 3, 8, 8, LANTERN)

    # Upper Cathedral Drum (y=21..33) + 4 Corner Turrets + Central Golden-Copper Spire (y=34..45)
    b(-10, 10, 21, 27, -11, 9, MARBLE)
    b(-9, 9, 21, 26, -10, 8, AIR)
    ring(-11, 11, -12, 10, 27, 27, POLISHED_GRANITE)
    for tx, tz in ((-11, -12), (11, -12), (-11, 10), (11, 10)):
        cyl_shell(tx, tz, 2.6, 1.2, 21, 29, POLISHED_MARBLE)
        disc(tx, tz, 2.8, 30, 31, COPPER_BLOCK)
        disc(tx, tz, 1.6, 32, 33, COPPER_BLOCK)
        b(tx, tx, 34, 34, tz, tz, CRYSTAL)
    cyl_shell(0, -1, 6.5, 4.2, 28, 34, POLISHED_MARBLE)
    for k in range(7):
        disc(0, -1, max(1.0, 6.6 - k * 0.85), 35 + k, 35 + k, COPPER_BLOCK if k % 2 == 0 else SHINGLES)
    b(0, 0, 42, 44, -1, -1, COPPER_BLOCK)
    b(0, 0, 45, 45, -1, -1, CRYSTAL)

    # Courtyard Guard Barracks (-25, -18) & Royal Stables (23, -18) — keeping (-24,-18) & (22,-18) open at y=1..3!
    town_house(-24, -18, hw=6, hd=5, stories=1, roof_id=SLATE, wall_id=BRICK, face_z=1)
    town_house(22, -18, hw=6, hd=5, stories=1, roof_id=TERRACOTTA, wall_id=PLANK, face_z=1)
    b(18, 26, 1, 3, -13, -13, AIR)  # Open arched stable bays

    # Courtyard Marble Fountain Plaza at (0, 24)
    disc(0, 24, 4.2, 1, 1, POLISHED_MARBLE)
    disc(0, 24, 3.0, 1, 1, WATER)
    b(0, 0, 1, 4, 24, 24, POLISHED_MARBLE)
    b(0, 0, 5, 5, 24, 24, CRYSTAL)


# ==============================================================================
# 2. OUTER RING CITY / POSAD (local radius 46..118)
# Citizen spawns: (0,38), (79,-2), (-70,54), (0,-74), (-24,-18), (22,-18),
#                 (52,0), (-52,0), (74,-40), (-74,-40), (96,-20), (-96,-20), (16,72), (-16,72)
# ==============================================================================
def build_ring_city():
    # Carefully placed varied houses along the 3 concentric avenues (avoiding citizen promenade tiles!)
    house_sites = [
        # Inner ring (~r=54)
        (38, 38, 5, 4, 2, SHINGLES, LIMESTONE, -1),
        (-38, 38, 5, 4, 2, SLATE, LIMESTONE, -1),
        (38, -38, 5, 4, 2, TERRACOTTA, LIMESTONE, 1),
        (-38, -38, 5, 4, 2, SHINGLES, LIMESTONE, 1),
        (54, 22, 5, 4, 1, SHINGLES, PLANK, -1),
        (-54, 22, 5, 4, 1, TERRACOTTA, PLANK, -1),
        (54, -22, 5, 4, 2, SLATE, LIMESTONE, 1),
        (-54, -22, 5, 4, 2, SHINGLES, LIMESTONE, 1),
        (24, -54, 5, 4, 1, TERRACOTTA, LIMESTONE, 1),
        (-24, -54, 5, 4, 1, SHINGLES, LIMESTONE, 1),
        # Middle ring (~r=78)
        (56, 56, 6, 4, 2, SHINGLES, LIMESTONE, -1),
        (-56, -56, 6, 4, 2, SLATE, LIMESTONE, 1),
        (56, -56, 5, 4, 1, TERRACOTTA, PLANK, 1),
        (32, 76, 5, 4, 2, SLATE, LIMESTONE, -1),
        (-32, 76, 5, 4, 2, SHINGLES, LIMESTONE, -1),
        (32, -78, 5, 4, 1, SHINGLES, PLANK, 1),
        (-32, -78, 5, 4, 1, TERRACOTTA, PLANK, 1),
        (-78, -18, 5, 4, 2, SHINGLES, LIMESTONE, 1),
        (-78, 18, 5, 4, 1, SLATE, LIMESTONE, -1),
        # Outer ring (~r=100)
        (72, 68, 5, 4, 1, SHINGLES, PLANK, -1),
        (-72, 68, 5, 4, 1, TERRACOTTA, PLANK, -1),
        (72, -68, 5, 4, 2, SLATE, LIMESTONE, 1),
        (-72, -68, 5, 4, 2, SHINGLES, LIMESTONE, 1),
        (98, 22, 5, 4, 1, TERRACOTTA, LIMESTONE, -1),
        (44, 96, 5, 4, 1, SHINGLES, PLANK, -1),
        (-44, 96, 5, 4, 1, SLATE, PLANK, -1),
        (20, 98, 5, 4, 2, TERRACOTTA, LIMESTONE, -1),
        (-20, 98, 5, 4, 2, SHINGLES, LIMESTONE, -1),
    ]
    for cx, cz, hw, hd, st, rf, wl, fz in house_sites:
        town_house(cx, cz, hw, hd, st, rf, wl, fz)

    # ---- EASTERN GRAND COVERED BAZAAR (x = 68..92, z = -16..16; (79,-2) is open market promenade!) ----
    b(68, 92, 0, 0, -16, 16, SANDSTONE)
    ring(68, 92, -16, 16, 0, 0, POLISHED_GRANITE)
    # Ornate Bazaar Fountain at (86, 0)
    ring(84, 88, -2, 2, 1, 1, POLISHED_MARBLE)
    b(85, 87, 1, 1, -1, 1, WATER)
    b(86, 86, 1, 4, 0, 0, POLISHED_MARBLE)
    b(86, 86, 5, 5, 0, 0, LANTERN)
    # 6 Covered Market Pavilions on North & South flanks (z = -14..-9 and z = 8..13)
    for sx, canopy in ((70, TERRACOTTA), (78, SHINGLES), (86, TERRACOTTA)):
        for sz, c_dir in ((-13, 1), (9, -1)):
            for px in (sx, sx + 4):
                for pz in (sz, sz + 3):
                    b(px, px, 1, 3, pz, pz, OAK_LOG)
            b(sx - 1, sx + 5, 4, 4, sz - 1, sz + 4, canopy)
            cz_cnt = sz + (3 if c_dir > 0 else 0)
            b(sx + 1, sx + 3, 1, 1, cz_cnt, cz_cnt, PLANK)
            b(sx + 2, sx + 2, 1, 1, sz + 1, sz + 1, WORKBENCH)
            b(sx + 1, sx + 1, 2, 2, cz_cnt, cz_cnt, LANTERN)

    # ---- SOUTH-WEST IMPERIAL FOUNDRY & BLACKSMITH (x = -80..-60, z = 46..62; (-70, 54) open hall!) ----
    b(-80, -60, 0, 0, 46, 62, COBBLE)
    ring(-80, -60, 46, 62, 1, 5, BRICK)
    b(-79, -61, 1, 5, 47, 61, AIR)
    b(-74, -66, 1, 4, 46, 46, AIR)  # Wide open forge archway facing north!
    for k in range(7):
        b(-81 + k, -59 - k, 6 + k, 6 + k, 45, 63, SLATE)
    b(-78, -76, 1, 16, 58, 60, CLAY_BRICK)  # Tall brick foundry smokestack
    b(-78, -76, 1, 2, 57, 57, FURNACE)
    b(-64, -63, 1, 1, 59, 59, GENERATOR)
    b(-63, -63, 1, 1, 50, 51, WATER)
    b(-77, -77, 1, 1, 50, 50, WORKBENCH)
    b(-77, -77, 1, 1, 52, 52, CARVED_OAK)
    b(-77, -77, 2, 2, 52, 52, LANTERN)

    # ---- NORTHERN SANCTUARY CATHEDRAL-CHAPEL (x = -9..9, z = -96..-70; (0, -74) open nave!) ----
    b(-9, 9, 0, 0, -96, -70, POLISHED_MARBLE)
    ring(-9, 9, -96, -70, 1, 8, BRICK)
    b(-8, 8, 1, 8, -95, -71, AIR)
    for pz in range(-94, -71, 6):
        b(-9, -9, 1, 9, pz, pz, POLISHED_STONE)
        b(9, 9, 1, 9, pz, pz, POLISHED_STONE)
        b(-9, -9, 3, 6, pz + 2, pz + 3, GLASS)
        b(9, 9, 3, 6, pz + 2, pz + 3, GLASS)
    for k in range(10):
        if -10 + k <= 10 - k:
            b(-10 + k, 10 - k, 9 + k, 9 + k, -97, -69, SHINGLES)
    # Tall Southern Belfry Spire at (0, -68) with arched portal underneath into nave!
    bastion_tower(0, -68, rad=3.6, h=22, spire_mat=COPPER_BLOCK)
    b(-2, 2, 1, 5, -73, -63, AIR)
    b(-2, 2, 1, 2, -93, -91, POLISHED_GRANITE)
    b(0, 0, 3, 3, -92, -92, POLISHED_MARBLE)
    b(0, 0, 4, 4, -92, -92, CRYSTAL)
    for cx_ch in (-5, 5):
        b(cx_ch, cx_ch, 1, 2, -82, -82, POLISHED_MARBLE)
        b(cx_ch, cx_ch, 3, 3, -82, -82, LANTERN)

    # ---- WESTERN HILLTOP WINDMILL (x = -106..-90, z = 32..46) ----
    cyl_shell(-98, 40, 5.2, 3.2, 1, 16, LIMESTONE)
    disc(-98, 40, 5.2, 0, 0, COBBLE)
    for k in range(6):
        disc(-98, 40, max(1.0, 5.5 - k * 0.85), 17 + k, 17 + k, SHINGLES)
    b(-99, -97, 1, 3, 34, 37, AIR)
    # 4 Grand Orthogonal Pinwheel Windmill Sails on South Face (z = 32..36 — 100% 6-face-connected!)
    b(-98, -98, 13, 13, 32, 36, OAK_LOG)
    b(-106, -90, 13, 13, 32, 32, OAK_LOG)
    b(-98, -98, 5, 21, 32, 32, OAK_LOG)
    b(-106, -100, 14, 15, 32, 32, PLANK)
    b(-96, -90, 11, 12, 32, 32, PLANK)
    b(-97, -96, 15, 21, 32, 32, PLANK)
    b(-100, -99, 5, 11, 32, 32, PLANK)

    # ---- IRRIGATED TERRACED FARMS (Keeping (96,-20) & (-96,-20) clear!) ----
    for fx, fz in ((98, -54), (-102, -46)):
        ring(fx - 9, fx + 9, fz - 8, fz + 8, 1, 1, OAK_LOG)
        b(fx - 1, fx + 1, 1, 1, fz + 8, fz + 8, AIR)
        for rz in range(fz - 7, fz + 8):
            b(fx - 8, fx + 8, 0, 0, rz, rz, WATER if (rz - fz) % 3 == 0 else DIRT)

    # ---- SAKURA & OAK BOULEVARD TREES + WROUGHT STREET LANTERNS ----
    for i in range(20):
        ang = math.radians(i * 18 + 9)
        tx, tz = int(round(math.sin(ang) * 112)), int(round(math.cos(ang) * 112))
        if abs(tx) < 10 or abs(tz) < 10:
            continue
        tree(tx, tz, 5 + (i % 2), SAKURA if i % 2 == 0 else LEAVES)
    for lx, lz in ((-44, -8), (44, -8), (-44, 8), (44, 8), (-8, -44), (8, -44), (-8, 46), (8, 46),
                   (-66, -8), (66, -8), (-8, 66), (8, 66), (76, -20), (-76, -20)):
        lantern_post(lx, lz, 1)


build_imperial_keep()
build_ring_city()

# Verify ALL 14 LivingSimulation citizen/cat spawn coordinates have solid floor (ry=0) and clear headroom (ry=1..2)!
kx_list = [0, 79, -70, 0, -24, 22, 52, -52, 74, -74, 96, -96, 16, -16]
kz_list = [38, -2, 54, -74, -18, -18, 0, 0, -40, -40, -20, -20, 72, 72]


def sample_local(x, ry, z):
    val = 0
    for x0, x1, y0, y1, z0, z1, bid in boxes:
        if x0 <= x <= x1 and y0 <= ry <= y1 and z0 <= z <= z1:
            val = bid
    return val


for idx, (kx, kz) in enumerate(zip(kx_list, kz_list)):
    for dy in (1, 2):
        blk = sample_local(kx, dy, kz)
        assert blk == 0, f"Kingdom citizen #{idx} obstructed at local ({kx}, {dy}, {kz}) by block {blk}!"

# Strict 6-face voxel connectivity & placed-model grounding check across the entire 244x244 capital!
from collections import deque as _deque
_NON_SUPPORT = {11, 15, 16, 52, 54, 55, 56, 61, 72, 73, 74, 75, 77, 85, 86, 87, 89}
_kvox = {}
for x0, x1, y0, y1, z0, z1, bid in boxes:
    for _y in range(y0, y1 + 1):
        for _z in range(z0, z1 + 1):
            for _x in range(x0, x1 + 1):
                if bid == 0:
                    _kvox.pop((_x, _y, _z), None)
                else:
                    _kvox[(_x, _y, _z)] = bid
_vis = set()
_q = _deque()
for (_x, _y, _z), _b in _kvox.items():
    assert _b != CAMPFIRE, f"CAMPFIRE forbidden in KingdomPlan at ({_x}, {_y}, {_z})"
    if _y <= 0 and _b not in _NON_SUPPORT:
        _vis.add((_x, _y, _z))
        _q.append((_x, _y, _z))
while _q:
    _x, _y, _z = _q.popleft()
    if _kvox[(_x, _y, _z)] in _NON_SUPPORT:
        continue
    for _dx, _dy, _dz in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
        _nb = (_x + _dx, _y + _dy, _z + _dz)
        if _nb in _kvox and _nb not in _vis:
            if _kvox[_nb] in _NON_SUPPORT and _dy != 1:
                continue
            _vis.add(_nb)
            _q.append(_nb)
_bad = [(_pos, _kvox[_pos]) for _pos in sorted(_kvox) if _pos not in _vis]
assert not _bad, f"KingdomPlan has {len(_bad)} disconnected/unsupported voxels: {_bad[:12]}"

# Great road: castle gate -> Quietford well, plus avenues and boulevard ring
road(CX, CZ + 40, 96, 48, 4)
road(CX - 30, CZ, CX + 30, CZ, 3)
road(CX, CZ - 30, CX, CZ + 30, 3)
ring_pts = []
for i in range(8):
    a = math.radians(i * 45)
    ring_pts.append((int(CX + math.sin(a) * 44), int(CZ + math.cos(a) * 44)))
for i in range(8):
    road(ring_pts[i][0], ring_pts[i][1], ring_pts[(i + 1) % 8][0], ring_pts[(i + 1) % 8][1], 3)

# ------------------------------------------------------------------ emit ----
cells = {}
for idx, bx in enumerate(boxes):
    for cx in range(bx[0] >> 4, (bx[1] >> 4) + 1):
        for cz in range(bx[4] >> 4, (bx[5] >> 4) + 1):
            key = (cx + 2048) * 4096 + (cz + 2048)
            cells.setdefault(key, []).append(idx)
keys = sorted(cells)
cell_keys, cell_start, flat = [], [], []
for key in keys:
    cell_keys.append(key)
    cell_start.append(len(flat))
    flat.extend(cells[key])
cell_start.append(len(flat))

flat_boxes = []
for bx in boxes:
    flat_boxes.extend(bx)

lines = []
lines.append("using Unity.Mathematics;")
lines.append("namespace LestingCraft.World")
lines.append("{")
lines.append("    /// <summary>The Tsardom: Imperial Cathedral-Keep with a fortified ring city and")
lines.append("    /// the great road to Quietford. Authored by Tools/build_kingdom.py (bucketed local boxes).</summary>")
lines.append("    public static class KingdomPlan")
lines.append("    {")
lines.append(f"        public const int CenterX={CX},CenterZ={CZ},Core={CORE},Region={REGION};")
lines.append("        public static bool Inside(int x,int z)=>math.abs(x-CenterX)<=Region&&math.abs(z-CenterZ)<=Region;")
lines.append("        public static int BaseY(int seed)=>math.max(44,TerrainGenerator.Height(CenterX,CenterZ,seed,Dimension.Lestingia));")
lines.append("        static readonly int[] RoadSegments={%s};" % ",".join(str(v) for seg in roads for v in seg))
lines.append("        static readonly int[] CellKeys={%s};" % ",".join(str(k) for k in cell_keys))
lines.append("        static readonly int[] CellStart={%s};" % ",".join(str(v) for v in cell_start))
lines.append("        static readonly int[] BoxIndex={%s};" % ",".join(str(v) for v in flat))
lines.append("        static readonly int[] Boxes={%s};" % ",".join(str(v) for v in flat_boxes))
lines.append("""
        public static bool Road(int x,int z)
        {
            for(int i=0;i<RoadSegments.Length;i+=5)
            {
                long ax=RoadSegments[i],az=RoadSegments[i+1],bx=RoadSegments[i+2],bz=RoadSegments[i+3],rr=RoadSegments[i+4];
                long dx=bx-ax,dz=bz-az,px=x-ax,pz=z-az,len=dx*dx+dz*dz,t=px*dx+pz*dz;
                if(t<0){if(px*px+pz*pz<=rr*rr)return true;continue;}
                if(t>len){px=x-bx;pz=z-bz;if(px*px+pz*pz<=rr*rr)return true;continue;}
                long cross=px*dz-pz*dx;if(cross*cross<=rr*rr*len)return true;
            }
            return false;
        }

        static int GroundAt(int lx,int ry,int lz)
        {
            int key=((lx>>4)+2048)*4096+((lz>>4)+2048);
            int lo=0,hi=CellKeys.Length-1;
            while(lo<=hi){int mid=(lo+hi)/2;int c=CellKeys[mid];if(c<key)lo=mid+1;else if(c>key)hi=mid-1;else{
                int id=0;for(int i=CellStart[mid];i<CellStart[mid+1];i++){int b=BoxIndex[i]*7;
                    if(lx>=Boxes[b]&&lx<=Boxes[b+1]&&ry>=Boxes[b+2]&&ry<=Boxes[b+3]&&lz>=Boxes[b+4]&&lz<=Boxes[b+5])id=Boxes[b+6];}
                return id;}}
            return 0;
        }

        public static bool TrySample(int x,int y,int z,int seed,out ushort block)
        {
            block=0;
            if(y<=0||y>=256)return false;
            bool road=Road(x,z);
            int dx=x-CenterX,dz=z-CenterZ;
            int dist=math.max(math.abs(dx),math.abs(dz))-Core;
            if(dist>6&&!road)return false;
            if(road&&dist>0)
            {
                // Terrain-following gravel road outside the flat core.
                int natural=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia);
                if(y<=natural){block=y==natural?(ushort)32:y<natural-2?(ushort)1:(ushort)2;return true;}
                return true;
            }
            int baseY=BaseY(seed);
            int natural2=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia),top=baseY;
            if(dist>0){int blend=math.min(6,dist);top=(baseY*(6-blend)+natural2*blend)/6;}
            if(y<top-3&&y<=natural2)return false;
            if(y<=top)
            {
                block=y==top?(dist<=0?(ushort)40:(ushort)2):y<top-2?(ushort)1:(ushort)2;
                if(y==top&&dist<=0){int g=GroundAt(dx,0,dz);if(g!=0)block=(ushort)g;}
                return true;
            }
            if(y>baseY+46)return true;
            if(dist>0)return true;
            int ry=y-baseY;
            block=(ushort)GroundAt(dx,ry,dz);
            return true;
        }
    }
}
""")
out = r / "Assets/Scripts/World/KingdomPlan.cs"
out.write_text("\n".join(lines), encoding="utf-8")
print(f"KingdomPlan.cs rebuilt: {len(boxes)} boxes, {len(flat)} bucket entries, {len(roads)} road segments, {len(out.read_text(encoding='utf-8'))} bytes")
