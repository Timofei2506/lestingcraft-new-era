#!/usr/bin/env python3
"""Author completely rebuilt Chapter One / Quietford v11 landmarks from scratch.
Leaves legacy StoryPlan (contentVersion <= 10) 100% untouched while upgrading
all 5 Chapter One sites for contentVersion >= 11 in both Unity C# and Node JS.
"""
from pathlib import Path
import json, math

r = Path(__file__).resolve().parents[1]
boxes = []  # [x0, x1, y0, y1, z0, z1, bid]

AIR = 0
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


def lantern_post(x, z):
    b(x, x, 1, 1, z, z, POLISHED_STONE)
    b(x, x, 2, 3, z, z, OAK_LOG)
    b(x - 1, x + 1, 4, 4, z, z, CARVED_OAK)
    b(x, x, 5, 5, z, z, LANTERN)


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
    b(-37, -27, 1, 6, -37, -28, AIR)
    for px in (-38, -32, -26):
        for pz in (-38, -27):
            b(px, px, 1, 6, pz, pz, OAK_LOG)
    # Gothic mullioned windows
    for wx in (-36, -29):
        b(wx, wx + 1, 3, 4, -38, -38, GLASS)
        b(wx, wx + 1, 3, 4, -27, -27, GLASS)
    # Steep slate roof with rafters, northwest chimney, and dramatic meteor/guardian breach!
    for k in range(7):
        b(-39 + k, -25 - k, 6 + k, 6 + k, -39, -26, SLATE)
    b(-38, -37, 1, 13, -38, -37, CLAY_BRICK)
    b(-37, -37, 1, 1, -36, -36, FURNACE)
    b(-37, -37, 1, 1, -37, -37, WORKBENCH)
    # Carve dramatic diagonal roof & east-wall breach so sky & sunbeams pour onto the scene!
    b(-34, -27, 5, 14, -35, -28, AIR)
    b(-26, -25, 1, 6, -32, -28, AIR)  # East breach opening toward road
    b(-33, -31, 1, 4, -27, -26, AIR)  # South doorway facing player spawn (-32, -30)
    # Low ruined courtyard perimeter wall with wide northeast road exit
    ring(-42, -22, -42, -22, 1, 1, CRACKED)
    b(-35, -22, 1, 3, -24, -22, AIR)
    b(-24, -22, 1, 3, -35, -22, AIR)
    lantern_post(-40, -24)


# ==============================================================================
# SITE 0: COUNTERWEIGHT GATEHOUSE & ARCHED STONE BRIDGE (-9..9, -1..17)
# Clear tiles required: (-8,7), (-3,8), (-1,12), (4,6), barrier at (3, 6..12)
# ==============================================================================
def build_bridge_gatehouse():
    b(-9, 9, 0, 0, 1, 15, POLISHED_STONE)
    b(-9, 9, 0, 0, 5, 13, COBBLE)
    # Crenellated stone bridge parapets on north (z=15) & south (z=3) flanks
    for z_wall in (3, 15):
        b(-9, 9, 1, 2, z_wall, z_wall, BRICK)
        for mx in range(-8, 9, 4):
            b(mx, mx + 1, 3, 3, z_wall, z_wall, POLISHED_STONE)
    # Twin Gatehouse Towers flanking the barrier at x = 3, z = 4..5 and z = 13..14!
    for gz in (4, 13):
        b(2, 5, 1, 8, gz, gz + 1, BRICK)
        ring(1, 6, gz - 1, gz + 2, 8, 8, POLISHED_STONE)
        b(2, 5, 9, 10, gz, gz + 1, SHINGLES)
        b(3, 4, 11, 11, gz, gz + 1, COPPER_BLOCK)
    # Overhead stone gate arch spanning z = 6..12 at y = 7..9 (above raised barrier y=2.3+4=6.3!)
    b(2, 4, 8, 9, 6, 12, POLISHED_STONE)
    b(3, 3, 10, 10, 9, 9, LANTERN)
    lantern_post(-6, 4)
    lantern_post(-6, 14)


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
        b(cx - hw + 1, cx - hw + 1, 1, 1, iz, iz, BED)
        b(cx + hw - 2, cx + hw - 2, 1, 1, iz, iz, FURNACE)
        b(cx - hw + 2, cx - hw + 2, 1, 1, iz, iz, WORKBENCH)
        b(cx + hw - 3, cx + hw - 3, 1, 2, iz, iz, BOOKSHELF)
        b(cx, cx, 4, 4, cz, cz, LANTERN)
        # Wide 3-block open doorway so NPCs inside (e.g. Ilya at 108,60) have clear line of sight!
        b(cx - 1, cx + 1, 1, 3, door_z, door_z, AIR)
        b(cx + 2, cx + 2, 3, 3, door_z + door_dir_z, door_z + door_dir_z, TORCH)

    village_cottage(82, 35, 5, 4, SHINGLES, LIMESTONE, 1)
    village_cottage(109, 35, 5, 4, TERRACOTTA, LIMESTONE, 1)
    village_cottage(82, 62, 5, 4, SLATE, PLANK, -1)
    # Ilya's Forge House at (110, 62) — extra-open front arch (z=58) so (108,60) & (109,60) are wide open!
    village_cottage(110, 62, 5, 4, SHINGLES, BRICK, -1)
    b(107, 111, 1, 3, 58, 58, AIR)
    village_cottage(96, 67, 4, 3, TERRACOTTA, LIMESTONE, -1)

    # Ornate Shingled Village Well at (96, 46) (keeping Vera (96,50), Mira (94,52), checkpoint (96,53) 100% clear!)
    ring(94, 98, 44, 48, 1, 1, POLISHED_GRANITE)
    b(95, 97, 1, 1, 45, 47, WATER)
    for px in (94, 98):
        for pz in (44, 48):
            b(px, px, 2, 5, pz, pz, CARVED_OAK)
    for k in range(3):
        b(93 + k, 99 - k, 6 + k, 6 + k, 43 + k, 49 - k, SHINGLES)
    b(96, 96, 9, 10, 46, 46, COPPER_BLOCK)
    b(96, 96, 5, 5, 46, 46, LANTERN)

    # Covered Artisan Market Stalls on East/West edges of plaza
    for sx, sz, canopy in ((75, 46, TERRACOTTA), (113, 46, SHINGLES)):
        for px in (sx, sx + 4):
            for pz in (sz, sz + 4):
                b(px, px, 1, 3, pz, pz, OAK_LOG)
        b(sx - 1, sx + 5, 4, 4, sz - 1, sz + 5, canopy)
        b(sx + 1, sx + 3, 1, 1, sz + 2, sz + 2, PLANK)
        b(sx + 2, sx + 2, 3, 3, sz + 2, sz + 2, LANTERN)

    # Sakura trees & street lanterns around Quietford square
    for lx, lz in ((88, 48), (105, 48), (90, 39), (102, 39), (90, 56), (102, 56)):
        lantern_post(lx, lz)
    for tx, tz in ((75, 28), (117, 28), (75, 69), (117, 69)):
        b(tx, tx, 1, 5, tz, tz, OAK_LOG)
        b(tx - 2, tx + 2, 5, 6, tz - 2, tz + 2, SAKURA)
        b(tx - 1, tx + 1, 7, 7, tz - 1, tz + 1, SAKURA)


# ==============================================================================
# SITE 3: OLD WATERMILL & RUNIC TERRACE (35..61, -109..-83)
# Clear tiles: (48,-90) Mira/checkpoint; Runes at (42,-87), (48,-87), (54,-87) with y=1..2 stone & y=3 crystal 11!
# ==============================================================================
def build_watermill_and_runes():
    b(36, 60, 0, 0, -108, -84, POLISHED_STONE)
    b(39, 57, 0, 0, -92, -84, POLISHED_MARBLE)
    # Tall Stone-and-Timber Watermill House (x = 41..53, z = -106..-94)
    b(41, 53, 0, 0, -106, -94, BRICK)
    b(42, 52, 0, 0, -105, -95, PLANK)
    ring(41, 53, -106, -94, 1, 3, BRICK)
    ring(41, 53, -106, -94, 4, 7, LIMESTONE)
    b(42, 52, 1, 7, -105, -95, AIR)
    for px in (41, 47, 53):
        for pz in (-106, -94):
            b(px, px, 1, 7, pz, pz, OAK_LOG)
    b(44, 45, 2, 3, -94, -94, GLASS)
    b(49, 50, 2, 3, -94, -94, GLASS)
    b(46, 48, 5, 6, -94, -94, GLASS)
    for k in range(7):
        b(40 + k, 54 - k, 8 + k, 8 + k, -107, -93, SLATE)
    b(47, 47, 15, 15, -107, -93, CARVED_OAK)
    b(42, 43, 1, 15, -106, -105, CLAY_BRICK)
    b(43, 43, 1, 1, -104, -104, FURNACE)
    b(51, 51, 1, 1, -104, -104, WORKBENCH)
    b(47, 47, 6, 6, -100, -100, LANTERN)
    b(46, 48, 1, 3, -94, -94, AIR)  # Mill south door facing Mira at (48, -90)!

    # Grand 3D Timber-and-Copper Paddle Waterwheel + Elevated Stone Aqueduct on East Wall (x = 54..58)
    b(54, 59, 0, 0, -105, -95, WATER)
    b(53, 55, 4, 4, -100, -100, OAK_LOG)  # Axle
    for ang_i in range(12):
        ang = ang_i * math.pi / 6.0
        wy = int(round(4 + math.sin(ang) * 4))
        wz = int(round(-100 + math.cos(ang) * 4))
        if 1 <= wy <= 9:
            b(55, 56, wy, wy, wz, wz, CARVED_OAK if ang_i % 2 == 0 else PLANK)
    # Stone aqueduct flume feeding top of wheel from north
    b(55, 57, 9, 9, -108, -99, BRICK)
    b(56, 56, 10, 10, -108, -100, WATER)

    # Semicircular Runic Sanctuary Terrace framing the 3 Exact Rune Pedestals: (42,-87), (48,-87), (54,-87)
    for rx in (42, 48, 54):
        b(rx - 1, rx + 1, 0, 0, -88, -86, POLISHED_GRANITE)
        b(rx, rx, 1, 2, -87, -87, POLISHED_MARBLE)
        b(rx, rx, 3, 3, -87, -87, CRYSTAL)  # Exact y=3 crystal required by StoryController raycast!
    # Carved monolith pillars behind the runes (at z = -84, never blocking player at z = -90!)
    for px in (39, 45, 51, 57):
        b(px, px, 1, 5, -84, -84, MONOLITH)
        b(px, px, 6, 6, -84, -84, LANTERN)


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
    # Central Guardian Seal Dais (y = 1 ONLY across 174..178, -114..-110 so Guardian at floor+2 is clear!)
    b(173, 179, 1, 1, -115, -109, POLISHED_GRANITE)
    b(174, 178, 1, 1, -114, -110, MARBLE)

    # 8 Towering Curving Stone-and-Root Ribs arching around the outer perimeter (radius ~13.5)
    rib_angles = [25, 65, 115, 155, 205, 245, 295, 335]  # Keeps South approach (270 deg -> z=-98) wide open!
    for deg in rib_angles:
        rad = math.radians(deg)
        for step in range(11):
            r_cur = 13.8 - step * 0.48
            rx = int(round(176 + math.cos(rad) * r_cur))
            rz = int(round(-112 + math.sin(rad) * r_cur))
            ry = 1 + step
            mat = MOSSY if step < 4 else OAK_LOG if step < 8 else MONOLITH
            b(rx, rx + (1 if step < 3 else 0), ry, ry + 1, rz, rz + (1 if step < 3 else 0), mat)
        tip_x = int(round(176 + math.cos(rad) * 9.0))
        tip_z = int(round(-112 + math.sin(rad) * 9.0))
        b(tip_x, tip_x, 10, 10, tip_z, tip_z, GLOW_SHROOM if deg % 2 == 0 else LANTERN)

    # Exact 3 Sacred Brazier Pylons: (168,-112), (184,-112), (176,-104) — y=1..2 marble shaft, y=3 stone bowl!
    for bx, bz in ((168, -112), (184, -112), (176, -104)):
        b(bx - 1, bx + 1, 0, 0, bz - 1, bz + 1, POLISHED_GRANITE)
        b(bx, bx, 1, 2, bz, bz, POLISHED_MARBLE)
        b(bx, bx, 3, 3, bz, bz, BRICK)


build_prologue_outpost()
build_bridge_gatehouse()
build_quietford_village()
build_watermill_and_runes()
build_root_sanctuary()


def sample(x, ry, z):
    val = 0
    for x0, x1, y0, y1, z0, z1, bid in boxes:
        if x0 <= x <= x1 and y0 <= ry <= y1 and z0 <= z <= z1:
            val = bid
    return val


# Verify EVERY interactive & NPC & player checkpoint coordinate has unobstructed standing space!
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
    assert sample(cx, 1, cz) == 0 and sample(cx, 2, cz) == 0, f"Story v11 obstructed at ({cx}, {cz}): y1={sample(cx,1,cz)}, y2={sample(cx,2,cz)}"

# Verify Runes & Braziers & Guardian dais exact heights!
for rx in (42, 48, 54):
    assert sample(rx, 3, -87) == CRYSTAL and sample(rx, 4, -87) == 0, f"Rune pedestal broken at x={rx}"
for bx, bz in ((168, -112), (184, -112), (176, -104)):
    assert sample(bx, 3, bz) != 0 and sample(bx, 4, bz) == 0, f"Brazier broken at ({bx},{bz})"
assert sample(176, 1, -112) != 0 and sample(176, 2, -112) == 0 and sample(176, 3, -112) == 0, "Guardian dais obstructed!"

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
print(f"PASS StoryPlanV11 built: {len(boxes)} boxes across all 5 Chapter One landmarks; all {len(must_be_clear)} NPC/quest clearances verified!")
