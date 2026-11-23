#!/usr/bin/env python3
"""Author completely rebuilt Chapter One / Quietford v11 landmarks from scratch.
Leaves legacy StoryPlan (contentVersion <= 10) 100% untouched while upgrading
all 5 Chapter One sites for contentVersion >= 11 in both Unity C# and Node JS.
Guarantees 0 6-face-disconnected voxels, 0 unsupported placed 3D models,
0 T-hammer lampposts, and unmistakable silhouettes for Bridge, Watermill, Quietford & Sanctuary.
"""
from pathlib import Path
from collections import deque
import json, math

r = Path(__file__).resolve().parents[1]
boxes = []  # [x0, x1, y0, y1, z0, z1, bid]

AIR = 0
STONE = 1
GRASS = 3
OAK_LOG = 4
LEAVES = 5
WATER = 7
CRYSTAL = 11
CAMPFIRE = 15
WORKBENCH = 16
FURNACE = 17
PLANK = 18
GRANITE = 27
LIMESTONE = 28
SLATE = 29
MARBLE = 30
GRAVEL = 32
MOSSY = 35
COBBLE = 36
BRICK = 37
CRACKED = 38
CLAY_BRICK = 39
POLISHED_STONE = 40
POLISHED_GRANITE = 41
POLISHED_MARBLE = 42
GLASS = 44
COPPER_BLOCK = 46
TORCH = 54
BED = 55
LANTERN = 56
BOOKSHELF = 57
CARVED_OAK = 58
SHINGLES = 59
SAKURA = 60
GLOW_SHROOM = 61
TERRACOTTA = 62
MONOLITH = 68


def b(x0, x1, y0, y1, z0, z1, bid):
    x0, x1 = int(x0), int(x1)
    y0, y1 = int(y0), int(y1)
    z0, z1 = int(z0), int(z1)
    if x0 <= x1 and y0 <= y1 and z0 <= z1:
        boxes.append([x0, x1, y0, y1, z0, z1, int(bid)])


def ring(x0, x1, z0, z1, y0, y1, bid):
    b(x0, x1, y0, y1, z0, z0, bid)
    b(x0, x1, y0, y1, z1, z1, bid)
    b(x0, x0, y0, y1, z0 + 1, z1 - 1, bid)
    b(x1, x1, y0, y1, z0 + 1, z1 - 1, bid)


def lantern_post(x, z, y=1):
    """Slender 1x1 wrought-stone and carved-oak street bollard (no T-hammer crossbar)."""
    b(x, x, y, y, z, z, POLISHED_STONE)
    b(x, x, y + 1, y + 2, z, z, CARVED_OAK)
    b(x, x, y + 3, y + 3, z, z, LANTERN)


# ==============================================================================
# SITE 1: PROLOGUE SHATTERED WATCHPOST (-44..-20, -44..-20)
# Clear tiles required: (-32,-30), (-35,-29), (-28,-33), (-34,-34), (-35,-32), (-32,-34), (-28,-25)
# ==============================================================================
def build_prologue_outpost():
    b(-42, -22, 0, 0, -42, -22, MOSSY)
    b(-39, -25, 0, 0, -39, -25, COBBLE)
    b(-37, -27, 0, 0, -37, -27, PLANK)
    # Stone-brick & half-timbered watchpost lodge (-38..-26, -38..-27)
    ring(-38, -26, -38, -27, 1, 2, BRICK)
    ring(-38, -26, -38, -27, 3, 5, LIMESTONE)
    b(-37, -27, 1, 5, -37, -28, AIR)
    for px in (-38, -32, -26):
        for pz in (-38, -27):
            b(px, px, 1, 5, pz, pz, OAK_LOG)
    # Gothic mullioned windows
    for wx in (-36, -29):
        b(wx, wx + 1, 3, 4, -38, -38, GLASS)
        b(wx, wx + 1, 3, 4, -27, -27, GLASS)
    # Steep slate roof with 2-voxel overlapping courses, northwest chimney & dramatic breach!
    for k in range(7):
        x0, x1 = -39 + k, -25 - k
        b(x0, x1, 6 + k, 6 + k, -39, -26, SLATE)
        if x0 + 2 <= x1 - 2 and k < 6:
            b(x0 + 2, x1 - 2, 6 + k, 6 + k, -37, -28, AIR)
    b(-38, -37, 1, 13, -38, -37, CLAY_BRICK)
    b(-37, -37, 1, 1, -36, -36, FURNACE)
    b(-37, -37, 1, 1, -37, -37, WORKBENCH)
    # Carve dramatic roof & east-wall breach so sky & sunbeams pour onto the scene!
    b(-33, -27, 5, 14, -35, -28, AIR)
    b(-26, -25, 1, 5, -32, -28, AIR)  # East breach opening toward road
    b(-33, -31, 1, 4, -27, -26, AIR)  # South doorway facing player spawn (-32, -30)
    # Low ruined courtyard perimeter wall with wide northeast road exit
    ring(-42, -22, -42, -22, 1, 1, CRACKED)
    b(-35, -22, 1, 3, -24, -22, AIR)
    b(-24, -22, 1, 3, -35, -22, AIR)
    lantern_post(-40, -24)


# ==============================================================================
# SITE 0: ARCHED STONE RIVER BRIDGE & COUNTERWEIGHT GATEHOUSE (-9..9, -1..17)
# Clear tiles required: (-8,7), (-3,8), (-1,12), (4,6), barrier at (3, 6..12)
# ==============================================================================
def build_bridge_gatehouse():
    # Deep river gorge running North-South (z = -1..17) beneath West-East bridge!
    b(-9, 9, -6, 0, -1, 17, BRICK)
    # Terraced grassy riverbanks sloping down toward the water outside the roadway (z <= 3 & z >= 15)
    for z_lo, z_hi in ((-1, 3), (15, 17)):
        b(-9, -8, -6, -1, z_lo, z_hi, STONE)
        b(-9, -8, 0, 0, z_lo, z_hi, GRASS)
        b(-8, -8, -1, 0, z_lo, z_hi, AIR)
        b(-8, -8, -2, -2, z_lo, z_hi, GRASS)
        b(2, 9, -6, -1, z_lo, z_hi, STONE)
        b(2, 9, 0, 0, z_lo, z_hi, GRASS)
        b(2, 3, -1, 0, z_lo, z_hi, AIR)
        b(2, 3, -2, -2, z_lo, z_hi, GRASS)

    # Dressed stone bridge abutments & river quay walls at x = -7 (West) and x = 1 (East)
    b(-7, -7, -5, -3, -1, 17, POLISHED_STONE)
    b(1, 1, -5, -3, -1, 17, POLISHED_STONE)
    b(-7, -7, -2, 0, 4, 14, POLISHED_STONE)
    b(1, 1, -2, 0, 4, 14, POLISHED_STONE)
    # Gravel riverbed (ry=-6) + sparkling flowing river (x=-6..0, ry=-5..-4) + open gorge air (ry=-3..0)
    b(-6, 0, -6, -6, -1, 17, GRAVEL)
    b(-6, 0, -5, -4, -1, 17, WATER)
    b(-6, 0, -3, 0, -1, 17, AIR)
    b(-7, -7, -2, 0, -1, 3, AIR)
    b(-7, -7, -2, 0, 15, 17, AIR)
    b(1, 1, -2, 0, -1, 3, AIR)
    b(1, 1, -2, 0, 15, 17, AIR)

    # True Curved Romanesque Stone Bridge Arch spanning x = -7..1 across z = 5..13!
    # Springing haunches at x = -6 and x = 0 (ry = -2..0, leaving ry=-3 open above ry=-5..-4 water)
    b(-6, -6, -2, 0, 5, 13, POLISHED_STONE)
    b(0, 0, -2, 0, 5, 13, POLISHED_STONE)
    # Upper arch ring at x = -5 and x = -1 (ry = -1..0, leaving ry=-3..-2 open!)
    b(-5, -5, -1, 0, 5, 13, POLISHED_STONE)
    b(-1, -1, -1, 0, 5, 13, POLISHED_STONE)
    # Arch crown & full cobblestone roadway across x = -9..9, z = 5..13 at ry = 0
    # (Leaves x = -4..-2 with 3 FULL BLOCKS of open arch vault ry = -3..-1 over blue water!)
    b(-9, 9, 0, 0, 5, 13, COBBLE)
    b(-9, 9, 0, 0, 5, 5, POLISHED_STONE)
    b(-9, 9, 0, 0, 13, 13, POLISHED_STONE)
    # Widen East Gatehouse Plaza apron at x = 1..6, z = 4..14
    b(1, 6, -5, 0, 4, 14, BRICK)
    b(1, 6, 0, 0, 4, 14, POLISHED_STONE)
    b(1, 6, 0, 0, 5, 13, COBBLE)

    # Low carved stone bridge parapets along z = 5 & z = 13 (x = -9..1) so (-1, 12) stays 100% clear!
    for z_par in (5, 13):
        b(-9, 1, 1, 1, z_par, z_par, POLISHED_STONE)
        for p_x in (-9, -6, 0):
            b(p_x, p_x, 2, 2, z_par, z_par, POLISHED_GRANITE)

    # Slender Medieval Toll Gatehouse Arch on East Bank (x = 2..4, flanking portcullis at x=3, z=6..12)
    # Keeps counterweight (4, 6) 100% clear at ry >= 1, and z=6..12 clear up to ry=7!
    for gz in (4, 13):
        b(2, 4, 1, 8, gz, gz + 1, BRICK)
        b(2, 2, 1, 8, gz, gz + 1, POLISHED_STONE)
        b(4, 4, 1, 8, gz, gz + 1, POLISHED_STONE)
    for z_e in (5, 13):
        b(5, 9, 1, 1, z_e, z_e, POLISHED_STONE)
    # Vaulted stone gate lintel & pitched slate gatehouse roof (ry = 8..11)
    b(2, 4, 8, 8, 4, 14, POLISHED_STONE)
    b(1, 5, 9, 9, 3, 15, SLATE)
    b(2, 4, 10, 10, 4, 14, SLATE)
    b(3, 3, 11, 11, 4, 14, CARVED_OAK)

    # Sleek 1x1 bridge streetlamp bollards on parapet piers
    lantern_post(-6, 5, 2)
    lantern_post(-6, 13, 2)
    lantern_post(8, 5, 1)
    lantern_post(8, 13, 1)


# ==============================================================================
# SITE 2: QUIETFORD ARTISAN VILLAGE (72..120, 24..72)
# Clear tiles required: (96,53), (96,50), (109,60), (97,51), (108,60), (95,51),
# (94,52), (106,59), (82,36), (109,35), (82,61), (110,62), (96,67),
# (90,44), (103,44), (88,56), (104,56)
# ==============================================================================
def build_quietford_village():
    b(73, 119, 0, 0, 25, 71, COBBLE)
    b(86, 106, 0, 0, 41, 58, POLISHED_STONE)

    def village_cottage(cx, cz, hw, hd, roof_id, wall_id, door_dir_z):
        b(cx - hw, cx + hw, 0, 0, cz - hd, cz + hd, BRICK)
        b(cx - hw + 1, cx + hw - 1, 0, 0, cz - hd + 1, cz + hd - 1, PLANK)
        ring(cx - hw, cx + hw, cz - hd, cz + hd, 1, 1, BRICK)
        ring(cx - hw, cx + hw, cz - hd, cz + hd, 2, 4, wall_id)
        ring(cx - hw, cx + hw, cz - hd, cz + hd, 5, 5, CARVED_OAK)
        b(cx - hw + 1, cx + hw - 1, 1, 5, cz - hd + 1, cz + hd - 1, AIR)
        for px in (cx - hw, cx + hw):
            for pz in (cz - hd, cz + hd):
                b(px, px, 1, 5, pz, pz, OAK_LOG)
        for wx in (cx - hw + 2, cx + hw - 2):
            b(wx, wx, 2, 3, cz - hd, cz - hd, GLASS)
            b(wx, wx, 2, 3, cz + hd, cz + hd, GLASS)
        for k in range(hw + 2):
            if cx - hw - 1 + k <= cx + hw + 1 - k:
                b(cx - hw - 1 + k, cx + hw + 1 - k, 6 + k, 6 + k, cz - hd - 1, cz + hd + 1, roof_id)
        b(cx, cx, 6 + hw + 1, 6 + hw + 1, cz - hd - 1, cz + hd + 1, CARVED_OAK)
        back_z = cz - hd if door_dir_z > 0 else cz + hd
        door_z = cz + hd if door_dir_z > 0 else cz - hd
        b(cx + hw - 1, cx + hw, 1, 6 + hw + 2, back_z, back_z, CLAY_BRICK)
        iz = back_z + (1 if door_dir_z > 0 else -1)
        iz_lamp = iz + (1 if door_dir_z > 0 else -1)
        b(cx - hw + 1, cx - hw + 1, 1, 1, iz, iz, BED)
        b(cx + hw - 2, cx + hw - 2, 1, 1, iz, iz, FURNACE)
        b(cx - hw + 2, cx - hw + 2, 1, 1, iz, iz, WORKBENCH)
        b(cx + hw - 3, cx + hw - 3, 1, 2, iz, iz, BOOKSHELF)
        b(cx - hw + 1, cx - hw + 1, 1, 1, iz_lamp, iz_lamp, CARVED_OAK)
        b(cx - hw + 1, cx - hw + 1, 2, 2, iz_lamp, iz_lamp, LANTERN)
        # Wide 3-block open doorway + grounded porch bollard
        b(cx - 1, cx + 1, 1, 3, door_z, door_z, AIR)
        b(cx + 2, cx + 2, 1, 1, door_z + door_dir_z, door_z + door_dir_z, CARVED_OAK)
        b(cx + 2, cx + 2, 2, 2, door_z + door_dir_z, door_z + door_dir_z, LANTERN)

    village_cottage(82, 35, 5, 4, SHINGLES, LIMESTONE, 1)
    village_cottage(109, 35, 5, 4, TERRACOTTA, LIMESTONE, 1)
    village_cottage(82, 62, 5, 4, SLATE, PLANK, -1)
    # Ilya's Forge House at (110, 62) — extra-open front arch (z=58) so (108,60) & (109,60) are wide open!
    village_cottage(110, 62, 5, 4, SHINGLES, BRICK, -1)
    b(107, 111, 1, 3, 58, 58, AIR)
    village_cottage(96, 67, 4, 3, TERRACOTTA, LIMESTONE, -1)
    # Quietford Town Hall Clocktower Belfry rising above the north gable (x=94..98, z=67..71, ry=9..23, all z<=72!)
    ring(94, 98, 67, 71, 9, 15, BRICK)
    b(95, 97, 9, 14, 68, 70, AIR)
    b(95, 97, 15, 15, 68, 70, POLISHED_STONE)  # Solid belfry deck supporting bell lantern!
    b(96, 96, 12, 13, 67, 67, MARBLE)          # Clock face overlooking Quietford plaza!
    for px in (94, 98):
        for pz in (67, 71):
            b(px, px, 16, 18, pz, pz, OAK_LOG)
    b(96, 96, 16, 16, 69, 69, CARVED_OAK)
    b(96, 96, 17, 17, 69, 69, LANTERN)
    b(93, 99, 18, 18, 67, 71, POLISHED_STONE)
    b(93, 99, 19, 19, 66, 72, COPPER_BLOCK)
    b(94, 98, 20, 20, 67, 71, COPPER_BLOCK)
    b(95, 97, 21, 21, 68, 70, COPPER_BLOCK)
    b(96, 96, 22, 23, 69, 69, COPPER_BLOCK)

    # Ornate Shingled Village Well at (96, 46) (keeping Vera (96,50), Mira (94,52), checkpoint (96,53) 100% clear!)
    ring(94, 98, 44, 48, 1, 1, POLISHED_GRANITE)
    b(95, 97, 1, 1, 45, 47, WATER)
    for px in (94, 98):
        for pz in (44, 48):
            b(px, px, 2, 5, pz, pz, CARVED_OAK)
    ring(94, 98, 44, 48, 5, 5, CARVED_OAK)
    for k in range(3):
        b(94 + k, 98 - k, 6 + k, 6 + k, 44 + k, 48 - k, SHINGLES)
    b(96, 96, 9, 10, 46, 46, COPPER_BLOCK)
    b(98, 98, 2, 2, 46, 46, LANTERN)

    # Covered Artisan Market Stalls on East/West edges of plaza
    for sx, sz, canopy in ((75, 46, TERRACOTTA), (113, 46, SHINGLES)):
        for px in (sx, sx + 4):
            for pz in (sz, sz + 4):
                b(px, px, 1, 3, pz, pz, OAK_LOG)
        b(sx - 1, sx + 5, 4, 4, sz - 1, sz + 5, canopy)
        b(sx + 1, sx + 3, 1, 1, sz + 2, sz + 2, PLANK)
        b(sx + 1, sx + 1, 2, 2, sz + 2, sz + 2, LANTERN)

    # Sakura trees & slender 1x1 streetlamp bollards around Quietford square
    for lx, lz in ((88, 48), (105, 48), (90, 39), (102, 39), (90, 56), (102, 56)):
        lantern_post(lx, lz)
    for tx, tz in ((75, 28), (117, 28), (75, 69), (117, 69)):
        b(tx, tx, 1, 5, tz, tz, OAK_LOG)
        b(tx - 2, tx + 2, 5, 6, tz - 2, tz + 2, SAKURA)
        b(tx - 1, tx + 1, 7, 7, tz - 1, tz + 1, SAKURA)


# ==============================================================================
# SITE 3: HALF-TIMBERED WATERMILL, SPOKED WHEEL, AQUEDUCT & RUNIC TERRACE (35..61, -109..-83)
# Clear tiles: (48,-90) Mira/checkpoint; Runes at (42,-87), (48,-87), (54,-87) with y=1..2 stone & y=3 crystal 11!
# ==============================================================================
def build_watermill_and_runes():
    b(36, 60, 0, 0, -108, -84, POLISHED_STONE)
    b(38, 56, 0, 0, -92, -84, POLISHED_MARBLE)

    # Sunken Stone-Lined Millrace Canal on East Flank (x = 53..60, z = -109..-93, ry = -4..0)
    b(53, 60, -4, 0, -109, -93, BRICK)
    b(53, 53, 0, 0, -109, -93, POLISHED_STONE)
    b(60, 60, 0, 0, -109, -93, POLISHED_STONE)
    b(54, 59, -3, -2, -109, -94, WATER)
    b(54, 59, -1, 0, -109, -94, AIR)
    # Timber footbridge across south end of millrace
    b(53, 60, 0, 0, -94, -93, PLANK)
    b(53, 60, 1, 1, -93, -93, CARVED_OAK)

    # Picturesque Half-Timbered Millhouse (x = 40..52, z = -106..-94)
    b(40, 52, 0, 0, -106, -94, BRICK)
    b(41, 51, 0, 0, -105, -95, PLANK)
    ring(40, 52, -106, -94, 1, 3, BRICK)
    ring(40, 52, -106, -94, 4, 4, CARVED_OAK)
    ring(40, 52, -106, -94, 5, 7, LIMESTONE)
    b(41, 51, 1, 7, -105, -95, AIR)
    for px in (40, 46, 52):
        for pz in (-106, -94):
            b(px, px, 1, 7, pz, pz, OAK_LOG)
    b(42, 43, 2, 3, -94, -94, GLASS)
    b(49, 50, 2, 3, -94, -94, GLASS)
    b(45, 47, 5, 6, -94, -94, GLASS)
    # Steep Slate Gable Roof + Projecting Timber Grain-Hoist Beam on South Gable!
    for k in range(8):
        if 39 + k <= 53 - k:
            b(39 + k, 53 - k, 8 + k, 8 + k, -107, -93, SLATE)
    b(46, 46, 15, 15, -107, -92, CARVED_OAK)
    b(46, 46, 11, 11, -94, -92, OAK_LOG)  # Classic miller's sack-hoist beam!
    b(41, 42, 1, 15, -106, -105, CLAY_BRICK)
    # Interior millstone gear, furnace, workbench & grounded lantern
    b(49, 51, 1, 1, -101, -99, POLISHED_GRANITE)
    b(50, 50, 2, 3, -100, -100, OAK_LOG)
    b(42, 42, 1, 1, -104, -104, FURNACE)
    b(50, 50, 1, 1, -104, -104, WORKBENCH)
    b(42, 42, 1, 1, -96, -96, CARVED_OAK)
    b(42, 42, 2, 2, -96, -96, LANTERN)
    b(45, 47, 1, 3, -94, -94, AIR)  # Mill south doorway facing Mira at (48, -90)!

    # 100% 6-Face-Connected 9x9 Spoked Timber Overshot Waterwheel (x = 54..55, center ry=3, z=-100)
    # Compact outer stone axle bearing plinth (x=56, z=-100, ry=-3..2) so 100% of the wheel face is open to view!
    b(56, 56, -3, 2, -100, -100, POLISHED_STONE)
    b(50, 56, 3, 3, -100, -100, OAK_LOG)
    # 4 Orthogonal Timber Spokes
    b(54, 55, -1, 7, -100, -100, OAK_LOG)
    b(54, 55, 3, 3, -104, -96, OAK_LOG)
    # Continuous Face-Sharing Rim & Paddles (every consecutive block shares a full 2D face!)
    b(54, 55, 7, 7, -101, -99, CARVED_OAK)
    b(54, 55, -1, -1, -101, -99, CARVED_OAK)
    b(54, 55, 2, 4, -104, -104, CARVED_OAK)
    b(54, 55, 2, 4, -96, -96, CARVED_OAK)
    for sy in (-1, 1):
        for sz in (-1, 1):
            for dy, dz in ((1, 3), (2, 3), (2, 2), (3, 2), (3, 1)):
                mat = PLANK if (dy + dz) % 2 == 0 else CARVED_OAK
                b(54, 55, 3 + sy * dy, 3 + sy * dy, -100 + sz * dz, -100 + sz * dz, mat)

    # Grounded Stone Aqueduct Flume & Waterfall Spillway feeding the top of the wheel!
    # Rear stone aqueduct pier behind the wheel (z = -109..-106) + corbelled stone arch (z = -105..-103)
    # so zero vertical pillars obstruct the side view of the waterwheel!
    b(54, 56, -3, 8, -109, -106, BRICK)
    b(54, 56, 6, 8, -105, -105, POLISHED_STONE)
    b(54, 56, 7, 8, -104, -103, POLISHED_STONE)
    # Elevated stone flume trough (ry = 8..9, z = -109..-101) + cascading water onto top paddle!
    b(54, 56, 8, 8, -109, -100, POLISHED_STONE)
    b(54, 54, 9, 9, -109, -101, POLISHED_STONE)
    b(56, 56, 9, 9, -109, -101, POLISHED_STONE)
    b(55, 55, 9, 9, -109, -100, WATER)
    b(55, 55, 8, 8, -100, -100, WATER)

    # Semicircular Runic Sanctuary Terrace framing the 3 Exact Rune Pedestals: (42,-87), (48,-87), (54,-87)
    for rx in (42, 48, 54):
        b(rx - 1, rx + 1, 0, 0, -88, -86, POLISHED_GRANITE)
        b(rx, rx, 1, 2, -87, -87, POLISHED_MARBLE)
        b(rx, rx, 3, 3, -87, -87, CRYSTAL)  # Exact y=3 crystal required by StoryController raycast!
    # Carved monolith pillars behind the runes (at z = -84, never blocking player at z = -90!)
    for px in (39, 45, 51, 57):
        b(px, px, 1, 5, -84, -84, MONOLITH)
        b(px, px, 6, 6, -84, -84, LANTERN)
    lantern_post(38, -92)


# ==============================================================================
# SITE 4: ANCIENT ROOT GUARDIAN SANCTUARY (159..193, -129..-95)
# Clear: (176,-96), (176,-99), (176,-103), (179,-100), (174,-101)
# Guardian at (176.5, -111.5) standing on y=2 (so dais y=1 at 174..178, -114..-110)
# 3 Braziers at (168,-112), (184,-112), (176,-104): pedestal y=1..3, flame spawns at y=3.8!
# ==============================================================================
def build_root_sanctuary():
    b(160, 192, 0, 0, -128, -96, MOSSY)
    # Circular marble-and-obsidian ritual arena floor
    for z in range(-126, -97):
        for x in range(162, 191):
            d = math.hypot(x - 176, z + 112)
            if d <= 13.5:
                b(x, x, 0, 0, z, z, POLISHED_STONE if d > 9.5 else POLISHED_MARBLE if d > 4.5 else MONOLITH)
    # Central Guardian Seal Dais (y = 1 ONLY across 173..179, -115..-109 so Guardian at floor+2 is clear!)
    b(173, 179, 1, 1, -115, -109, POLISHED_GRANITE)
    b(174, 178, 1, 1, -114, -110, MARBLE)

    # Northern High Temple Colonnade Apse (x = 170..182, z = -125..-124) framing the Guardian!
    b(169, 183, 1, 1, -126, -123, POLISHED_GRANITE)
    for col_x in (170, 174, 178, 182):
        b(col_x, col_x, 2, 10, -124, -124, POLISHED_MARBLE)
    b(169, 183, 11, 12, -125, -123, POLISHED_STONE)
    for k in range(3):
        b(171 + k * 2, 181 - k * 2, 13 + k, 13 + k, -125, -123, POLISHED_MARBLE)
    b(175, 177, 16, 16, -125, -123, POLISHED_MARBLE)
    b(176, 176, 17, 17, -124, -124, CRYSTAL)

    # 4 Monumental 6-Connected Root-Wrapped Sanctuary Pylons at intercardinal corners (South entrance wide open!)
    for sx in (-1, 1):
        for sz in (-1, 1):
            px, pz = 176 + sx * 10, -112 + sz * 9
            b(px - 1, px + 1, 1, 2, pz - 1, pz + 1, POLISHED_GRANITE)
            b(px, px, 3, 10, pz, pz, MONOLITH)
            # Living ancient oak roots wrapping each pylon face-to-face + corbelled inward arch!
            b(min(px, px - sx), max(px, px - sx), 1, 5, pz, pz, OAK_LOG)
            b(px, px, 1, 5, min(pz, pz - sz), max(pz, pz - sz), OAK_LOG)
            for c_step in range(1, 4):
                ax0, ax1 = px - sx * c_step, px - sx * (c_step - 1)
                b(min(ax0, ax1), max(ax0, ax1), 8 + c_step, 9 + c_step, pz, pz, OAK_LOG)
            tip_x = px - sx * 3
            b(tip_x - 1, tip_x + 1, 11, 11, pz - 1, pz + 1, LEAVES)
            b(px, px, 11, 11, pz, pz, POLISHED_MARBLE)
            b(px, px, 12, 12, pz, pz, LANTERN)
            b(px - sx, px - sx, 6, 6, pz, pz, GLOW_SHROOM)

    # West & East Flanking Trilithon Arches (x = 163 & x = 189, z = -115..-109)
    for side_x in (163, 189):
        for az in (-115, -109):
            b(side_x, side_x, 1, 8, az, az, MOSSY)
        b(side_x, side_x, 9, 10, -116, -108, POLISHED_STONE)
        b(side_x, side_x, 11, 11, -112, -112, LANTERN)

    # Exact 3 Sacred Brazier Pylons: (168,-112), (184,-112), (176,-104) — y=1..2 marble shaft, y=3 stone bowl!
    for bx, bz in ((168, -112), (184, -112), (176, -104)):
        b(bx - 1, bx + 1, 0, 0, bz - 1, bz + 1, POLISHED_GRANITE)
        b(bx, bx, 1, 2, bz, bz, POLISHED_MARBLE)
        b(bx, bx, 3, 3, bz, bz, BRICK)
    lantern_post(172, -97)
    lantern_post(180, -97)


build_prologue_outpost()
build_bridge_gatehouse()
build_quietford_village()
build_watermill_and_runes()
build_root_sanctuary()


def sample(x, ry, z):
    val = 3 if ry == 0 else (1 if ry < 0 else 0)
    for x0, x1, y0, y1, z0, z1, bid in boxes:
        if x0 <= x <= x1 and y0 <= ry <= y1 and z0 <= z <= z1:
            val = bid
    return val


# Verify EVERY interactive & NPC & player checkpoint coordinate has solid floor (ry=0) & clear standing space (ry=1..2)!
must_be_clear = [
    # Prologue
    (-32, -30), (-28, -25), (-35, -29), (-28, -33), (-34, -34), (-35, -32), (-32, -34),
    # Bridge
    (-8, 7), (-3, 8), (-1, 12), (4, 6), (3, 9),
    # Quietford checkpoints, NPCs & 10 LivingSimulation villagers
    (96, 53), (96, 50), (109, 60), (97, 51), (108, 60), (95, 51), (94, 52), (106, 59),
    (82, 36), (109, 35), (82, 61), (110, 62), (96, 67), (90, 44), (103, 44), (88, 56), (104, 56),
    # Mill
    (48, -90), (47, -93),
    # Sanctuary
    (176, -96), (176, -99), (176, -103), (179, -100), (174, -101),
]
for cx, cz in must_be_clear:
    assert sample(cx, 0, cz) != 0, f"Story v11 missing floor at ({cx}, {cz})!"
    assert sample(cx, 1, cz) == 0 and sample(cx, 2, cz) == 0, f"Story v11 obstructed at ({cx}, {cz}): y1={sample(cx,1,cz)}, y2={sample(cx,2,cz)}"

# Verify Bridge portcullis clearance (x=3, z=6..12 clear at ry=1..7) & open arch over water!
for bz in range(6, 13):
    for bry in range(1, 8):
        assert sample(3, bry, bz) == 0, f"Bridge portcullis obstructed at (3, {bry}, {bz})"
assert sample(-3, -4, 9) == WATER and sample(-3, -1, 9) == AIR and sample(-3, 0, 9) == COBBLE, "Bridge river arch broken!"

# Verify Runes & Braziers & Guardian dais exact heights!
for rx in (42, 48, 54):
    assert sample(rx, 3, -87) == CRYSTAL and sample(rx, 4, -87) == 0, f"Rune pedestal broken at x={rx}"
for bx, bz in ((168, -112), (184, -112), (176, -104)):
    assert sample(bx, 3, bz) != 0 and sample(bx, 4, bz) == 0, f"Brazier broken at ({bx},{bz})"
assert sample(176, 1, -112) != 0 and sample(176, 2, -112) == 0 and sample(176, 3, -112) == 0, "Guardian dais obstructed!"

# Strict 6-face voxel connectivity & placed-model support audit across ALL 5 Chapter One sites!
NON_SUPPORT = {11, 15, 16, 52, 54, 55, 56, 61, 72, 73, 74, 75, 77, 85, 86, 87, 89}
SITES = [
    ("Prologue", -44, -20, -44, -20),
    ("Bridge", -9, 9, -1, 17),
    ("Quietford", 72, 120, 24, 72),
    ("Watermill", 35, 61, -109, -83),
    ("Sanctuary", 159, 193, -129, -95),
]
for s_name, sx0, sx1, sz0, sz1 in SITES:
    vox = {}
    for x in range(sx0, sx1 + 1):
        for z in range(sz0, sz1 + 1):
            for ry in range(-6, 25):
                blk = sample(x, ry, z)
                if blk != 0:
                    assert blk != CAMPFIRE, f"{s_name} has forbidden CAMPFIRE at ({x},{ry},{z})"
                    vox[(x, ry, z)] = blk
    vis = set()
    q = deque()
    for (x, ry, z), blk in vox.items():
        if ry == -6 and blk not in NON_SUPPORT:
            vis.add((x, ry, z))
            q.append((x, ry, z))
    while q:
        x, ry, z = q.popleft()
        if vox[(x, ry, z)] in NON_SUPPORT:
            continue
        for dx, dy, dz in ((1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, -1, 0), (0, 0, 1), (0, 0, -1)):
            nb = (x + dx, ry + dy, z + dz)
            if nb in vox and nb not in vis:
                if vox[nb] in NON_SUPPORT and dy != 1:
                    continue
                vis.add(nb)
                q.append(nb)
    bad = [(pos, vox[pos]) for pos in sorted(vox) if pos not in vis]
    assert not bad, f"{s_name} has {len(bad)} disconnected/unsupported voxels: {bad[:10]}"

flat = [n for bx in boxes for n in bx]
cs_path = r / "Assets/Scripts/Story/StoryPlanV11.cs"
cs_path.write_text(
    "// Generated by Tools/build_story_v11.py — Rebuilt Chapter One v11 architecture.\n"
    "namespace LestingCraft.Story\n{\n"
    "    public static class StoryPlanV11\n    {\n"
    "        public static readonly int[] Boxes = {" + ",".join(map(str, flat)) + "};\n"
    "    }\n}\n",
    encoding="utf-8",
)
meta_path = r / "Assets/Scripts/Story/StoryPlanV11.cs.meta"
if not meta_path.exists():
    meta_path.write_text(
        "fileFormatVersion: 2\nguid: 8c3d5e91a24b4f789123456789abcdef\n"
        "MonoImporter:\n  externalObjects: {}\n  serializedVersion: 2\n"
        "  defaultReferences: []\n  executionOrder: 0\n"
        "  icon: {instanceID: 0}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n",
        encoding="utf-8",
    )
js_path = r / "Server/src/story-plan-v11.js"
js_path.write_text(
    "// Generated by Tools/build_story_v11.py — Rebuilt Chapter One v11 architecture.\n"
    "export const boxesV11 = " + json.dumps(flat, separators=(",", ":")) + ";\n",
    encoding="utf-8",
)
print(f"PASS StoryPlanV11 built: {len(boxes)} boxes across all 5 Chapter One landmarks; 0 disconnected voxels; all {len(must_be_clear)} NPC/quest clearances verified!")
