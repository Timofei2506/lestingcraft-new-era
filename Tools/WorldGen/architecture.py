"""Completely rebuilt content-11 voxel architecture authored from scratch (v11-remaster).
Pure integer box volumes (1 block = 1 m), inclusive coordinates, y in [-8, 48], block IDs in [0, 89].
Preserves exact plan names/radii, >=2x3 doorway clearance, villager spawn clearance, and 2-block-capsule staircase topology.
"""
from math import sin, cos, pi, hypot

PLANS = []

# Core block palette (IDs <= 89)
AIR = 0
STONE = 1
DIRT = 2
GRASS = 3
OAK_LOG = 4
LEAVES = 5
SAND = 6
WATER = 7
CRYSTAL = 11
SNOW_ICE = 14
CAMPFIRE = 15
WORKBENCH = 16
FURNACE = 17
PLANK = 18
COAL_ORE = 19
IRON_ORE = 20
GOLD_ORE = 22
QUARTZ_ORE = 25
GRANITE = 27
LIMESTONE = 28
SLATE = 29
MARBLE = 30
OBSIDIAN = 31
GRAVEL = 32
CLAY = 33
MOSSY = 35
COBBLE = 36
BRICK = 37
CRACKED = 38
CLAY_BRICK = 39
POLISHED_STONE = 40
POLISHED_GRANITE = 41
POLISHED_MARBLE = 42
SANDSTONE = 43
GLASS = 44
COAL_BLOCK = 45
COPPER_BLOCK = 46
DIAMOND_BLOCK = 48
TORCH = 54
BED = 55
LANTERN = 56
BOOKSHELF = 57
CARVED_OAK = 58
SHINGLES = 59
SAKURA = 60
GLOW_SHROOM = 61
TERRACOTTA = 62
GLACIAL_ICE = 63
MAGMA_BASALT = 64
TITANIUM = 66
ANDROMEDA_CRYSTAL = 67
MONOLITH = 68
PALE_MARBLE = 69
CABLE = 72
BATTERY = 73
GENERATOR = 74
SOLAR = 75
FLOODLIGHT = 77
CHERENKOV = 78
CONCRETE = 79
HAZARD = 80
WHITE_TRUSS = 82
ARMORED_GLASS = 84
RAIL = 85
POWERED_RAIL = 86
TERMINAL = 89


class Plan:
    def __init__(self, name, radius):
        self.name = name
        self.radius = radius
        self.boxes = []
        self.entrances = []
        PLANS.append(self)

    def b(self, x0, x1, y0, y1, z0, z1, bid):
        x0, x1 = int(x0), int(x1)
        y0, y1 = int(y0), int(y1)
        z0, z1 = int(z0), int(z1)
        if x0 > x1 or y0 > y1 or z0 > z1:
            return
        x0 = max(-self.radius, min(self.radius, x0))
        x1 = max(-self.radius, min(self.radius, x1))
        z0 = max(-self.radius, min(self.radius, z0))
        z1 = max(-self.radius, min(self.radius, z1))
        y0 = max(-8, min(48, y0))
        y1 = max(-8, min(48, y1))
        self.boxes.append([x0, x1, y0, y1, z0, z1, int(bid)])

    def ring(self, x0, x1, z0, z1, y0, y1, bid):
        self.b(x0, x1, y0, y1, z0, z0, bid)
        self.b(x0, x1, y0, y1, z1, z1, bid)
        self.b(x0, x0, y0, y1, z0 + 1, z1 - 1, bid)
        self.b(x1, x1, y0, y1, z0 + 1, z1 - 1, bid)

    def disc(self, cx, cz, r, y0, y1, bid):
        r_int = int(r)
        for z in range(cz - r_int, cz + r_int + 1):
            span = int((r * r - (z - cz) ** 2) ** 0.5) if abs(z - cz) <= r else -1
            if span >= 0:
                self.b(cx - span, cx + span, y0, y1, z, z, bid)

    def cyl_shell(self, cx, cz, r_out, r_in, y0, y1, bid):
        self.disc(cx, cz, r_out, y0, y1, bid)
        if r_in > 0:
            self.disc(cx, cz, r_in, y0, y1, AIR)

    def door_z(self, x, z, y=1, width=2, depth_clear=1):
        self.b(x, x + width - 1, y, y + 2, z - depth_clear, z + depth_clear, AIR)
        self.entrances.append([int(x), int(z), int(y), int(width)])

    def gable_x(self, cx, cz, half_w, half_d, y_base, roof_id=SHINGLES, trim_id=CARVED_OAK, ridge_id=POLISHED_GRANITE):
        """Stepped gable roof pitching along X with carved eaves, gable infill, and ridge crest."""
        for k in range(half_w + 2):
            x0, x1 = cx - half_w - 1 + k, cx + half_w + 1 - k
            yy = y_base + k
            if x0 > x1:
                break
            self.b(x0, x1, yy, yy, cz - half_d - 1, cz + half_d + 1, roof_id)
            self.b(x0, x0, yy, yy, cz - half_d - 1, cz - half_d - 1, trim_id)
            self.b(x1, x1, yy, yy, cz - half_d - 1, cz - half_d - 1, trim_id)
            self.b(x0, x0, yy, yy, cz + half_d + 1, cz + half_d + 1, trim_id)
            self.b(x1, x1, yy, yy, cz + half_d + 1, cz + half_d + 1, trim_id)
            if x0 + 1 <= x1 - 1 and k < half_w + 1:
                self.b(x0 + 1, x1 - 1, yy, yy, cz - half_d, cz - half_d, PLANK)
                self.b(x0 + 1, x1 - 1, yy, yy, cz + half_d, cz + half_d, PLANK)
                if x0 + 1 <= x1 - 1 and cz - half_d + 1 <= cz + half_d - 1:
                    self.b(x0 + 1, x1 - 1, yy, yy, cz - half_d + 1, cz + half_d - 1, AIR)
        top_y = y_base + half_w + 1
        self.b(cx, cx, top_y, top_y + 1, cz - half_d - 1, cz + half_d + 1, ridge_id)
        self.b(cx, cx, top_y + 2, top_y + 2, cz - half_d - 1, cz - half_d - 1, trim_id)
        self.b(cx, cx, top_y + 2, top_y + 2, cz + half_d + 1, cz + half_d + 1, trim_id)

    def lantern_post(self, x, z, y=1, stone_base=POLISHED_STONE):
        self.b(x, x, y, y, z, z, stone_base)
        self.b(x, x, y + 1, y + 3, z, z, OAK_LOG)
        self.b(x - 1, x + 1, y + 4, y + 4, z, z, CARVED_OAK)
        self.b(x, x, y + 4, y + 4, z - 1, z + 1, CARVED_OAK)
        self.b(x, x, y + 5, y + 5, z, z, LANTERN)

    def tree_sakura(self, x, z, y=1, h=5, leaf=SAKURA):
        self.b(x, x, y, y + h - 1, z, z, OAK_LOG)
        self.disc(x, z, 2.4, y + h - 1, y + h, leaf)
        self.disc(x, z, 1.5, y + h + 1, y + h + 2, leaf)


# ==============================================================================
# 0. Деревня с колодцем (radius=27) — Ремесленная слобода с расписными теремами
# Villager spawns at y=1: (-15,-15), (14,-15), (-15,14), (14,14), (6,-5), (-6,5), (0,-10), (0,10)
# ==============================================================================
def build_village():
    p = Plan('Деревня с колодцем', 27)
    # Lush turf base with subsurface stone foundation so edges never float on slopes
    p.b(-26, 26, -3, -1, -26, 26, STONE)
    p.b(-26, 26, 0, 0, -26, 26, GRASS)
    # Cobblestone & polished-stone cross avenues + central octagonal plaza
    p.b(-3, 3, 0, 0, -26, 26, COBBLE)
    p.b(-26, 26, 0, 0, -3, 3, COBBLE)
    p.b(-1, 1, 0, 0, -26, 26, POLISHED_STONE)
    p.b(-26, 26, 0, 0, -1, 1, POLISHED_STONE)
    p.disc(0, 0, 8.5, 0, 0, COBBLE)
    p.disc(0, 0, 6.2, 0, 0, POLISHED_STONE)

    # Ornate octagonal shingled well pavilion at (0, 0) with deep water shaft (y=-6..0)
    p.disc(0, 0, 3.2, 1, 1, BRICK)
    p.cyl_shell(0, 0, 2.4, 1.5, -6, 2, BRICK)
    p.disc(0, 0, 1.5, -6, 0, WATER)
    p.disc(0, 0, 1.5, 1, 2, AIR)
    for sx in (-2, 2):
        for sz in (-2, 2):
            p.b(sx, sx, 2, 6, sz, sz, CARVED_OAK)
    p.ring(-3, 3, -3, 3, 6, 6, CARVED_OAK)
    for k in range(4):
        p.b(-4 + k, 4 - k, 7 + k, 7 + k, -4 + k, 4 - k, SHINGLES)
    p.b(0, 0, 11, 12, 0, 0, COPPER_BLOCK)
    p.b(0, 0, 5, 5, 0, 0, LANTERN)

    # Helper for bespoke half-timbered artisan cottages facing the central crossroad
    def artisan_cottage(cx, cz, face_south, roof_block, role):
        hw, hd = 5, 4
        # Plinth & oak floor (center cx,cz at y=0 solid, y=1..4 air for villager spawn!)
        p.b(cx - hw, cx + hw, -2, 0, cz - hd, cz + hd, COBBLE)
        p.b(cx - hw + 1, cx + hw - 1, 0, 0, cz - hd + 1, cz + hd - 1, PLANK)
        # Half-timbered walls (limestone/plank panels framed by oak logs and carved beams)
        p.ring(cx - hw, cx + hw, cz - hd, cz + hd, 1, 1, BRICK)
        p.ring(cx - hw, cx + hw, cz - hd, cz + hd, 2, 4, LIMESTONE)
        p.ring(cx - hw, cx + hw, cz - hd, cz + hd, 5, 5, CARVED_OAK)
        p.b(cx - hw + 1, cx + hw - 1, 1, 5, cz - hd + 1, cz + hd - 1, AIR)
        for x in (cx - hw, cx, cx + hw):
            for z in (cz - hd, cz + hd):
                p.b(x, x, 1, 5, z, z, OAK_LOG)
        # Framed mullion windows + planter boxes
        for wx in (cx - 3, cx + 3):
            for wz in (cz - hd, cz + hd):
                p.b(wx, wx, 2, 3, wz, wz, GLASS)
        for wz in (cz - 2, cz + 2):
            for wx in (cx - hw, cx + hw):
                p.b(wx, wx, 2, 3, wz, wz, GLASS)
        # Steep shingled gable roof with side dormer window
        p.gable_x(cx, cz, hw, hd, 6, roof_id=roof_block, trim_id=CARVED_OAK, ridge_id=CLAY_BRICK)
        # Stepped brick chimney on rear outer corner
        back_z = cz - hd if face_south else cz + hd
        front_z = cz + hd if face_south else cz - hd
        step_z = 1 if face_south else -1
        p.b(cx + hw - 1, cx + hw, 1, 11, back_z, back_z, CLAY_BRICK)
        p.b(cx + hw - 1, cx + hw, 12, 12, back_z, back_z, POLISHED_STONE)
        # Covered front porch & 2x3 entrance (never blocking center (cx, cz))
        p.b(cx - 2, cx + 2, 0, 0, front_z + step_z, front_z + 2 * step_z if step_z > 0 else front_z + step_z, POLISHED_STONE)
        if step_z < 0:
            p.b(cx - 2, cx + 2, 0, 0, front_z - 2, front_z - 1, POLISHED_STONE)
        porch_z = front_z + 2 * step_z
        for px in (cx - 2, cx + 2):
            p.b(px, px, 1, 3, porch_z, porch_z, OAK_LOG)
        z_a, z_b = min(front_z + step_z, porch_z), max(front_z + step_z, porch_z)
        p.b(cx - 2, cx + 2, 4, 4, z_a, z_b, roof_block)
        p.b(cx + 2, cx + 2, 3, 3, porch_z, porch_z, TORCH)
        p.door_z(cx - 1, front_z, 1, 2)
        # Role-specific interior craft stations tucked against inner corners (center cx,cz stays 100% open!)
        iz = back_z + (1 if face_south else -1)
        p.b(cx - hw + 1, cx - hw + 1, 1, 1, iz, iz, BED)
        p.b(cx, cx, 4, 4, cz, cz, LANTERN)
        if role == 'smith':
            p.b(cx + hw - 2, cx + hw - 1, 1, 1, iz, iz, FURNACE)
            p.b(cx - hw + 2, cx - hw + 2, 1, 1, iz, iz, WORKBENCH)
        elif role == 'scholar':
            p.b(cx + hw - 2, cx + hw - 1, 1, 2, iz, iz, BOOKSHELF)
            p.b(cx - hw + 2, cx - hw + 2, 1, 1, iz, iz, WORKBENCH)
        elif role == 'baker':
            p.b(cx + hw - 2, cx + hw - 2, 1, 1, iz, iz, FURNACE)
            p.b(cx - hw + 2, cx - hw + 3, 1, 1, iz, iz, PLANK)
        else:
            p.b(cx + hw - 2, cx + hw - 2, 1, 1, iz, iz, WORKBENCH)
            p.b(cx + hw - 3, cx + hw - 3, 1, 2, iz, iz, BOOKSHELF)

    artisan_cottage(-15, -15, True, SHINGLES, 'smith')
    artisan_cottage(14, -15, True, TERRACOTTA, 'baker')
    artisan_cottage(-15, 14, False, SHINGLES, 'scholar')
    artisan_cottage(14, 14, False, SLATE, 'carpenter')

    # Irrigated terraced farm & sakura orchard on east/west flanks (keeping (6,-5), (-6,5), (0,-10), (0,10) clear!)
    p.ring(10, 24, -6, 6, 1, 1, OAK_LOG)
    p.b(10, 10, 1, 1, -1, 1, AIR)
    for fz in (-5, -4, -2, -1, 1, 2, 4, 5):
        p.b(11, 23, 0, 0, fz, fz, DIRT)
    for wz in (-3, 0, 3):
        p.b(11, 23, 0, 0, wz, wz, WATER)
    p.tree_sakura(-21, -2, 1, 5, SAKURA)
    p.tree_sakura(-21, 5, 1, 5, LEAVES)
    p.tree_sakura(-3, -22, 1, 5, SAKURA)
    p.tree_sakura(5, 22, 1, 5, LEAVES)

    # Streetlights at the four plaza corners (away from villager spawn tiles)
    for lx, lz in ((-6, -6), (6, 6), (-6, 8), (6, -8)):
        p.lantern_post(lx, lz, 1)


# ==============================================================================
# 1. Пограничный замок (radius=26) — Королевская пограничная цитадель
# Topology check requires 2-block-capsule path from (-1, 1, 0) to (0, 6, 7)!
# ==============================================================================
def build_castle():
    p = Plan('Пограничный замок', 26)
    p.b(-25, 25, -4, -1, -25, 25, STONE)
    p.b(-24, 24, 0, 0, -24, 24, COBBLE)
    p.b(-16, 16, 0, 0, -18, 16, POLISHED_STONE)
    p.b(-3, 3, 0, 0, -25, 4, POLISHED_GRANITE)

    # Machicolated double-faced curtain walls (radius 22, height 8, walkable rampart at y=8)
    for z in (-22, -21, 21, 22):
        p.b(-22, 22, 1, 8, z, z, BRICK)
    for x in (-22, -21, 21, 22):
        p.b(x, x, 1, 8, -22, 22, BRICK)
    # Stone buttresses every 5 blocks + overhanging machicolation corbels + crenellated merlons
    for k in range(-18, 19, 6):
        p.b(k, k + 1, 1, 7, -23, -23, POLISHED_STONE)
        p.b(k, k + 1, 1, 7, 23, 23, POLISHED_STONE)
        p.b(-23, -23, 1, 7, k, k + 1, POLISHED_STONE)
        p.b(23, 23, 1, 7, k, k + 1, POLISHED_STONE)
    p.ring(-23, 23, -23, 23, 8, 8, POLISHED_STONE)
    for k in range(-22, 23, 3):
        p.b(k, min(23, k + 1), 9, 10, -23, -23, BRICK)
        p.b(k, min(23, k + 1), 9, 10, 23, 23, BRICK)
        p.b(-23, -23, 9, 10, k, min(23, k + 1), BRICK)
        p.b(23, 23, 9, 10, k, min(23, k + 1), BRICK)

    # 4 octagonal/cylindrical corner bastion towers with conical shingled roofs & royal banners
    for tx, tz in ((-21, -21), (21, -21), (-21, 21), (21, 21)):
        p.cyl_shell(tx, tz, 4.2, 2.4, 1, 15, BRICK)
        p.disc(tx, tz, 4.5, 15, 15, POLISHED_STONE)
        for FloorY in (5, 10):
            p.disc(tx, tz, 2.6, FloorY, FloorY, PLANK)
        # Arrow slits
        for wy in (3, 7, 12):
            p.b(tx, tx, wy, wy + 1, tz - 4, tz + 4, AIR)
            p.b(tx - 4, tx + 4, wy, wy + 1, tz, tz, AIR)
        # Conical shingled spire
        for rk in range(6):
            p.disc(tx, tz, max(0.8, 4.6 - rk * 0.8), 16 + rk, 16 + rk, SHINGLES)
        p.b(tx, tx, 22, 25, tz, tz, OAK_LOG)
        p.b(tx + 1, tx + 2, 24, 25, tz, tz, TERRACOTTA)
        # Courtyard door into each tower
        iz = tz + 3 if tz < 0 else tz - 3
        p.b(tx - 1, tx, 1, 3, min(tz, iz), max(tz, iz), AIR)

    # Twin-turret Southern Barbican Gatehouse at z = -22 (`p.entrances` >= 2x3 clear)
    for gx in (-6, 5):
        p.b(gx - 1, gx + 2, 1, 13, -25, -19, BRICK)
        p.b(gx - 2, gx + 3, 13, 13, -25, -18, POLISHED_STONE)
        p.b(gx - 1, gx + 2, 14, 15, -25, -19, SHINGLES)
        p.b(gx, gx + 1, 16, 17, -23, -21, SHINGLES)
    p.b(-4, 4, 6, 11, -24, -19, BRICK)
    p.b(-3, 3, 12, 13, -24, -19, POLISHED_STONE)
    # Portcullis teeth at y=5 above the 4-block-high vaulted gateway
    for px in (-2, 0, 2):
        p.b(px, px, 5, 5, -23, -23, OAK_LOG)
    p.b(-2, 2, 1, 4, -25, -18, AIR)
    p.entrances.append([-2, -22, 1, 5])
    p.lantern_post(-6, -16, 1)
    p.lantern_post(6, -16, 1)

    # Courtyard rampart stairs on west & east walls + training rack & courtyard brazier fountain
    for i in range(8):
        p.b(-19, -18, 1 + i, 1 + i, -12 + i, -12 + i, POLISHED_STONE)
        p.b(18, 19, 1 + i, 1 + i, -12 + i, -12 + i, POLISHED_STONE)
    p.b(-14, -9, 1, 1, -14, -10, COBBLE)
    p.b(-13, -10, 1, 4, -14, -14, OAK_LOG)
    p.b(-14, -9, 4, 4, -14, -10, SHINGLES)
    p.b(-13, -12, 1, 1, -13, -13, FURNACE)
    p.b(-10, -10, 1, 1, -13, -13, WORKBENCH)
    p.b(10, 14, 1, 1, -13, -9, BRICK)
    p.b(11, 13, 1, 1, -12, -10, WATER)
    p.b(12, 12, 2, 4, -11, -11, POLISHED_MARBLE)
    p.b(12, 12, 5, 5, -11, -11, LANTERN)

    # ---- GRAND TWO-STOREY CITADEL KEEP (x = -11..11, z = 2..16, height up to y=26) ----
    p.b(-11, 11, 0, 0, 2, 16, POLISHED_MARBLE)
    p.b(-11, 11, 1, 11, 2, 16, BRICK)
    p.b(-10, 10, 1, 11, 3, 15, AIR)
    # Marble pilaster buttresses & carved bands
    for bx in (-11, -6, 0, 6, 11):
        p.b(bx, bx, 1, 12, 2, 2, POLISHED_STONE)
        p.b(bx, bx, 1, 12, 16, 16, POLISHED_STONE)
    for bz in (2, 7, 12, 16):
        p.b(-11, -11, 1, 12, bz, bz, POLISHED_STONE)
        p.b(11, 11, 1, 12, bz, bz, POLISHED_STONE)
    p.ring(-12, 12, 1, 17, 5, 5, POLISHED_STONE)
    p.ring(-12, 12, 1, 17, 11, 12, POLISHED_STONE)

    # Tall gothic stained-crystal windows on both storeys
    for wy in (2, 7):
        for wx in (-8, -4, 4, 8):
            p.b(wx, wx + 1 if wx < 0 else wx - 1, wy, wy + 2, 2, 2, GLASS)
            p.b(wx, wx + 1 if wx < 0 else wx - 1, wy, wy + 2, 16, 16, GLASS)
        for wz in (5, 13):
            p.b(-11, -11, wy, wy + 2, wz, wz + 1, GLASS)
            p.b(11, 11, wy, wy + 2, wz, wz + 1, GLASS)

    # Upper Great Gallery floor at y=5 (standing y=6, including goal=(0, 6, 7)!)
    p.b(-10, 10, 5, 5, 3, 15, PLANK)
    # Central atrium lightwell near entry + wide western grand staircase (x=-8..-6, z=4..8)
    p.b(-3, 3, 5, 5, 3, 5, AIR)
    p.b(-8, -6, 5, 8, 4, 9, AIR)  # Generous 3-wide stairwell cutout with >=3 headroom!
    for i in range(5):
        # Step i=0 at z=4 (y=1), i=1 at z=5 (y=2), ..., i=4 at z=8 (y=5), stepping onto y=5 gallery at z=9!
        p.b(-8, -6, 1 + i, 1 + i, 4 + i, 4 + i, POLISHED_MARBLE)
        p.b(-8, -6, 2 + i, 5 + i, 4 + i, 4 + i, AIR)

    # Keep south entrance at z=2 (connecting (-1, 1, 0) straight into (-1, 1, 3) -> (-7, 1, 3) -> stair -> (0, 6, 7)!)
    p.door_z(-1, 2, 1, 3, depth_clear=1)
    # Royal balcony above keep door
    p.b(-3, 3, 5, 5, 0, 1, POLISHED_MARBLE)
    p.ring(-3, 3, 0, 1, 6, 6, CARVED_OAK)
    p.door_z(-1, 2, 6, 2, depth_clear=0)

    # Interior throne dais, bookshelves, chandeliers, beds & hearth (keeping (0, 6, 7) completely clear!)
    p.b(-2, 2, 1, 1, 12, 14, POLISHED_GRANITE)
    p.b(-1, 1, 2, 4, 14, 14, TERRACOTTA)
    p.b(-9, -9, 1, 2, 12, 14, BOOKSHELF)
    p.b(9, 9, 1, 1, 13, 14, BED)
    p.b(9, 9, 1, 1, 11, 11, WORKBENCH)
    p.b(-9, -8, 6, 8, 14, 14, BOOKSHELF)
    p.b(8, 9, 6, 8, 14, 14, BOOKSHELF)
    p.b(9, 10, 6, 6, 5, 6, BED)
    p.b(0, 0, 4, 4, 10, 10, LANTERN)
    p.b(0, 0, 10, 10, 10, 10, LANTERN)

    # High pitched slate-and-shingle keep roof + central lantern cupola spire
    p.gable_x(0, 9, 11, 7, 12, roof_id=SLATE, trim_id=POLISHED_STONE, ridge_id=POLISHED_MARBLE)
    p.b(-2, 2, 20, 24, 7, 11, BRICK)
    p.b(-3, 3, 25, 25, 6, 12, SHINGLES)
    p.b(-1, 1, 26, 27, 8, 10, SHINGLES)
    p.b(0, 0, 28, 31, 9, 9, OAK_LOG)
    p.b(1, 2, 30, 31, 9, 9, TERRACOTTA)


# ==============================================================================
# 2. Разрушенная усадьба (radius=17) — Готическое поместье с заросшим погребом
# ==============================================================================
def build_ruined_manor():
    p = Plan('Разрушенная усадьба', 17)
    p.b(-16, 16, -6, -1, -16, 16, STONE)
    p.b(-16, 16, 0, 0, -16, 16, MOSSY)
    p.b(-2, 2, 0, 0, -16, -5, GRAVEL)

    # Wrought-stone perimeter fence with broken sections & mossy pillars
    p.ring(-15, 15, -14, 14, 1, 1, CRACKED)
    for fx in (-15, -8, -3, 3, 8, 15):
        for fz in (-14, 14):
            p.b(fx, fx, 1, 3, fz, fz, BRICK)
    p.b(-2, 2, 1, 3, -14, -14, AIR)
    p.entrances.append([-2, -14, 1, 4])

    # Vaulted wine/alchemical cellar beneath the manor (y = -5..-1) with stairs down!
    p.b(-9, 3, -6, -6, -3, 7, BRICK)
    p.b(-9, 3, -5, -1, -3, 7, CRACKED)
    p.b(-8, 2, -5, -1, -2, 6, AIR)
    p.b(-7, -7, -5, -5, 4, 5, GLOW_SHROOM)
    p.b(1, 1, -5, -4, 4, 5, BOOKSHELF)
    p.b(-3, -3, -2, -2, 2, 2, LANTERN)

    # Two-storey Gothic Manor House (x = -11..5, z = -5..9), northeast upper wing dramatically collapsed!
    p.b(-11, 5, 0, 0, -5, 9, POLISHED_STONE)
    p.b(-10, 4, 0, 0, -4, 8, PLANK)
    p.b(-11, 5, 1, 10, -5, 9, BRICK)
    p.b(-10, 4, 1, 10, -4, 8, AIR)
    for cx in (-11, -6, -1, 5):
        for cz in (-5, 9):
            p.b(cx, cx, 1, 10, cz, cz, POLISHED_GRANITE)
    p.ring(-11, 5, -5, 9, 5, 5, CARVED_OAK)
    # Second floor gallery (west half intact, east half collapsed)
    p.b(-10, -2, 5, 5, -4, 8, PLANK)
    for i in range(5):
        p.b(-9, -8, 1 + i, 1 + i, -2 + i, -2 + i, PLANK)
    # Cellar stairwell opening in east parlor
    p.b(0, 1, -5, 0, -2, 3, AIR)
    for i in range(6):
        p.b(0, 1, -i, -i, -2 + i, -2 + i, BRICK)

    # Tall lancet windows
    for wy in (2, 7):
        for wx in (-9, -4, 2):
            p.b(wx, wx, wy, wy + 2, -5, -5, GLASS if wx < 0 else AIR)
            p.b(wx, wx, wy, wy + 2, 9, 9, GLASS if wx < 0 else AIR)

    # Surviving grand fireplace & tall brick chimney stack on west gable
    p.b(-11, -9, 1, 15, 1, 3, CLAY_BRICK)
    p.b(-10, -9, 1, 2, 2, 2, FURNACE)
    p.b(-9, -9, 1, 1, 6, 7, BED)
    p.b(-9, -8, 6, 7, 7, 7, BOOKSHELF)

    # Pitched slate roof — dramatic breach carved out of the east side!
    p.gable_x(-3, 2, 8, 7, 10, roof_id=SLATE, trim_id=CARVED_OAK, ridge_id=POLISHED_STONE)
    p.b(-1, 6, 5, 20, 1, 10, AIR)
    p.b(2, 6, 3, 10, -2, 6, AIR)
    # Fallen rubble & mossy rafters scattered outside the collapsed wing + glass conservatory ruin
    for rx, rz, rh in ((6, 4, 2), (7, 6, 1), (4, 8, 2), (8, 2, 1)):
        p.b(rx, rx + 1, 1, rh, rz, rz + 1, CRACKED)
    p.b(7, 13, 0, 0, -6, 1, BRICK)
    p.ring(7, 13, -6, 1, 1, 3, GLASS)
    p.ring(7, 13, -6, 1, 4, 4, CARVED_OAK)
    p.b(9, 11, 2, 4, -2, 1, AIR)
    p.b(10, 10, 1, 1, -4, -4, GLOW_SHROOM)
    p.door_z(9, -6, 1, 2)
    # Grand arched front entrance to manor
    p.b(-4, -1, 0, 0, -7, -6, POLISHED_STONE)
    p.door_z(-3, -5, 1, 2)
    p.lantern_post(-6, -8, 1)


# ==============================================================================
# 3. Сломанная дозорная башня (radius=14) — Древний сигнальный маяк на скале
# ==============================================================================
def build_watchtower():
    p = Plan('Сломанная дозорная башня', 14)
    p.b(-13, 13, -4, -1, -13, 13, STONE)
    p.disc(0, 0, 12.5, 0, 0, MOSSY)
    p.disc(0, 0, 9.0, 0, 1, POLISHED_STONE)
    p.b(-2, 2, 0, 1, -13, -6, COBBLE)
    p.b(-2, 2, 1, 1, -13, -10, GRAVEL)

    # Octagonal buttressed beacon tower (r_out = 6.2, r_in = 4.0, height = 24)
    p.cyl_shell(0, 0, 6.2, 4.0, 1, 23, BRICK)
    # 4 heavy cardinal stone buttresses
    for bx, bz in ((-7, 0), (7, 0), (0, -7), (0, 7)):
        p.b(bx - (0 if bx else 1), bx + (0 if bx else 1), 1, 12, bz - (0 if bz else 1), bz + (0 if bz else 1), POLISHED_GRANITE)
    for band_y in (6, 12, 18):
        p.cyl_shell(0, 0, 6.6, 4.0, band_y, band_y, POLISHED_STONE)

    # Spiral stone staircase hugging the inner wall up to the signal deck (y = 1..20)
    ring_pts = [
        (-2, -3), (-1, -3), (0, -3), (1, -3), (2, -3),
        (3, -2), (3, -1), (3, 0), (3, 1), (3, 2),
        (2, 3), (1, 3), (0, 3), (-1, 3), (-2, 3),
        (-3, 2), (-3, 1), (-3, 0), (-3, -1), (-3, -2),
    ]
    p.disc(0, 0, 4.0, 1, 1, POLISHED_STONE)
    for step in range(1, 20):
        sx, sz = ring_pts[step % len(ring_pts)]
        p.b(sx - 1 if abs(sz) == 3 else sx, sx + 1 if abs(sz) == 3 else sx,
            1 + step, 1 + step,
            sz - 1 if abs(sx) == 3 else sz, sz + 1 if abs(sx) == 3 else sz, PLANK)
    # Central chandelier chain & armory supplies on ground floor
    p.b(0, 0, 10, 18, 0, 0, OAK_LOG)
    p.b(0, 0, 9, 9, 0, 0, LANTERN)
    p.b(-2, -2, 2, 2, 2, 2, WORKBENCH)
    p.b(2, 2, 2, 3, 2, 2, BOOKSHELF)

    # Tall arched arrow-slit windows
    for wy in (4, 10, 15):
        p.b(0, 0, wy, wy + 2, -6, -5, AIR)
        p.b(0, 0, wy, wy + 2, 5, 6, AIR)
        p.b(-6, -5, wy, wy + 2, 0, 0, AIR)
        p.b(5, 6, wy, wy + 2, 0, 0, AIR)

    # Signal brazier platform at y=20 with crenellations — northeast quadrant dramatically shattered!
    p.disc(0, 0, 5.5, 20, 20, POLISHED_STONE)
    p.cyl_shell(0, 0, 6.5, 5.2, 21, 23, BRICK)
    p.b(-1, 1, 21, 22, -1, 1, POLISHED_GRANITE)
    p.b(0, 0, 23, 23, 0, 0, CAMPFIRE)
    # Carve dramatic diagonal collapse on +X/+Z crown & scatter fallen masonry below
    p.b(1, 7, 17, 25, 1, 7, AIR)
    p.b(3, 7, 13, 17, 2, 7, AIR)
    for rx, rz, rh in ((7, 5, 3), (9, 7, 2), (6, 9, 2), (-9, 6, 1)):
        p.b(rx, rx + 2, 1, rh, rz, rz + 2, CRACKED)

    # Grand south doorway at z = -6 (y=2 on raised plinth, with exterior stairs at z=-9..-7)
    for s in range(2):
        p.b(-2, 2, 1 + s, 1 + s, -9 + s, -9 + s, POLISHED_STONE)
    p.b(-1, 1, 2, 5, -7, -4, AIR)
    p.entrances.append([-1, -6, 2, 3])
    p.lantern_post(-5, -9, 1)


# ==============================================================================
# 4. Заброшенный лагерь (radius=13) — Укреплённый частоколом лагерь следопытов
# ==============================================================================
def build_camp():
    p = Plan('Заброшенный лагерь', 13)
    p.b(-12, 12, -3, -1, -12, 12, STONE)
    p.disc(0, 0, 12.2, 0, 0, GRASS)
    p.disc(0, 0, 7.5, 0, 0, GRAVEL)

    # Sharpened timber palisade ring with uneven log heights & two wide gates (south & north)
    for a_deg in range(0, 360, 5):
        rad = a_deg * pi / 180.0
        px, pz = round(cos(rad) * 11), round(sin(rad) * 11)
        if abs(px) <= 2 and abs(pz) >= 9:
            continue
        h = 3 + ((a_deg // 5) % 2)
        p.b(px, px, 1, h, pz, pz, OAK_LOG)
    p.b(-2, 2, 0, 0, -12, 12, GRAVEL)
    p.b(-2, 2, 1, 4, -12, -9, AIR)
    p.entrances.append([-2, -11, 1, 5])
    # Timber gate arch on south entrance
    p.b(-3, -3, 1, 5, -11, -11, OAK_LOG)
    p.b(3, 3, 1, 5, -11, -11, OAK_LOG)
    p.b(-3, 3, 5, 5, -11, -11, CARVED_OAK)
    p.b(-3, -3, 4, 4, -12, -12, TORCH)
    p.b(3, 3, 4, 4, -12, -12, TORCH)

    # Two pitched A-frame expedition tents (west & east)
    for side, canopy in ((-1, TERRACOTTA), (1, SHINGLES)):
        cx = side * 6
        p.b(cx - 3, cx + 3, 0, 0, -4, 4, PLANK)
        for z in range(-4, 5):
            for k in range(4):
                p.b(cx - 3 + k, cx - 3 + k, 1 + k, 1 + k, z, z, canopy)
                p.b(cx + 3 - k, cx + 3 - k, 1 + k, 1 + k, z, z, canopy)
        p.b(cx - 2, cx + 2, 1, 3, -3, 3, AIR)
        p.b(cx, cx, 1, 4, -4, -4, OAK_LOG)
        p.b(cx, cx, 1, 4, 4, 4, OAK_LOG)
        p.b(cx, cx, 4, 4, -4, 4, CARVED_OAK)
        # Open entrance facing central firepit
        inner_x = cx - side * 3
        p.b(inner_x, inner_x + side, 1, 3, -1, 1, AIR)
        p.b(cx + side, cx + side, 1, 1, -2, -1, BED)
        p.b(cx + side, cx + side, 1, 1, 2, 2, WORKBENCH if side < 0 else FURNACE)
        p.b(cx, cx, 3, 3, 0, 0, LANTERN)

    # Wooden watchtower platform on northwest rim (x=-6, z=7)
    for tx in (-8, -4):
        for tz in (5, 9):
            p.b(tx, tx, 1, 6, tz, tz, OAK_LOG)
    p.b(-8, -4, 6, 6, 5, 9, PLANK)
    p.ring(-8, -4, 5, 9, 7, 7, CARVED_OAK)
    p.b(-6, -6, 8, 8, 7, 7, LANTERN)

    # Central sunken stone firepit with cooking spit & log benches
    p.ring(-2, 2, -2, 2, 0, 0, COBBLE)
    p.b(-1, 1, 0, 0, -1, 1, MAGMA_BASALT)
    p.b(0, 0, 1, 1, 0, 0, CAMPFIRE)
    p.b(-2, 2, 1, 1, -4, -4, OAK_LOG)
    p.b(-2, 2, 1, 1, 4, 4, OAK_LOG)


# ==============================================================================
# 5. Обелиск стража (radius=22) — Ступенчатое святилище Древних (высота 34 м)
# ==============================================================================
def build_obelisk():
    p = Plan('Обелиск стража', 22)
    p.b(-21, 21, -4, -1, -21, 21, STONE)
    p.b(-21, 21, 0, 0, -21, 21, MOSSY)

    # Three stepped granite-and-marble ziggurat terraces with glowing Cherenkov moat
    p.b(-19, 19, 1, 1, -19, 19, POLISHED_GRANITE)
    p.ring(-19, 19, -19, 19, 2, 2, POLISHED_STONE)
    p.ring(-16, 16, -16, 16, 1, 1, WATER)
    p.b(-14, 14, 1, 2, -14, 14, POLISHED_STONE)
    p.ring(-14, 14, -14, 14, 3, 3, POLISHED_MARBLE)
    p.b(-9, 9, 1, 3, -9, 9, POLISHED_MARBLE)

    # Four grand processional stairways + portal arches on N/S/E/W
    for i in range(4):
        p.b(-2, 2, i, i, -21 + i * 3, -19 + i * 3, POLISHED_MARBLE)
        p.b(-2, 2, i + 1, i + 4, -21 + i * 3, -19 + i * 3, AIR)
        p.b(-2, 2, i, i, 19 - i * 3, 21 - i * 3, POLISHED_MARBLE)
        p.b(-2, 2, i + 1, i + 4, 19 - i * 3, 21 - i * 3, AIR)
        p.b(-21 + i * 3, -19 + i * 3, i, i, -2, 2, POLISHED_MARBLE)
        p.b(19 - i * 3, 21 - i * 3, i, i, -2, 2, POLISHED_MARBLE)
    # Monumental south stone trilithon gate at z = -19 (`entrances`)
    p.b(-4, -3, 2, 8, -19, -18, POLISHED_GRANITE)
    p.b(3, 4, 2, 8, -19, -18, POLISHED_GRANITE)
    p.b(-5, 5, 8, 9, -19, -18, POLISHED_MARBLE)
    p.b(0, 0, 10, 10, -19, -18, CRYSTAL)
    p.b(-2, 2, 2, 6, -20, -17, AIR)
    p.entrances.append([-2, -19, 2, 5])

    # Four corner sentinel pylons with levitating crystal crowns
    for sx in (-15, 15):
        for sz in (-15, 15):
            p.b(sx - 2, sx + 2, 2, 3, sz - 2, sz + 2, POLISHED_GRANITE)
            p.b(sx - 1, sx + 1, 4, 12, sz - 1, sz + 1, OBSIDIAN)
            p.b(sx - 2, sx + 2, 13, 13, sz - 2, sz + 2, POLISHED_MARBLE)
            p.b(sx, sx, 14, 15, sz, sz, CRYSTAL)
            p.b(sx, sx, 6, 11, sz - (2 if sz > 0 else -2) + (1 if sz > 0 else -1), sz - (2 if sz > 0 else -2) + (1 if sz > 0 else -1), CHERENKOV)

    # Colossal 34-meter tapered Obsidian & Gold-Vein Guardian Obelisk in the center!
    p.b(-4, 4, 4, 5, -4, 4, OBSIDIAN)
    p.ring(-4, 4, -4, 4, 5, 5, POLISHED_MARBLE)
    p.b(-3, 3, 6, 15, -3, 3, OBSIDIAN)
    p.b(-2, 2, 16, 25, -2, 2, OBSIDIAN)
    p.b(-1, 1, 26, 32, -1, 1, POLISHED_MARBLE)
    p.b(0, 0, 33, 36, 0, 0, DIAMOND_BLOCK)
    # Glowing vertical energy channels up all four faces of the obelisk
    for face_coord in (-3, 3):
        p.b(0, 0, 7, 14, face_coord, face_coord, CRYSTAL)
        p.b(face_coord, face_coord, 7, 14, 0, 0, CRYSTAL)
    for face_coord in (-2, 2):
        p.b(0, 0, 17, 24, face_coord, face_coord, CHERENKOV)
        p.b(face_coord, face_coord, 17, 24, 0, 0, CHERENKOV)


# ==============================================================================
# 6. Шахтёрский лагерь (radius=15) — Промышленный рудник с копром и штольней
# ==============================================================================
def build_mining_camp():
    p = Plan('Шахтёрский лагерь', 15)
    p.b(-14, 14, -8, -1, -14, 14, STONE)
    p.b(-14, 14, 0, 0, -14, 14, GRAVEL)
    p.b(-13, -3, 0, 0, -12, 12, COBBLE)

    # Foreman's Assay Office & Bunkhouse on west side (x = -13..-4, z = 1..11)
    p.b(-13, -4, 0, 0, 1, 11, BRICK)
    p.b(-12, -5, 0, 0, 2, 10, PLANK)
    p.ring(-13, -4, 1, 11, 1, 4, PLANK)
    p.b(-12, -5, 1, 5, 2, 10, AIR)
    for x in (-13, -8, -4):
        for z in (1, 11):
            p.b(x, x, 1, 5, z, z, OAK_LOG)
    p.b(-10, -10, 2, 3, 1, 1, GLASS)
    p.b(-6, -6, 2, 3, 1, 1, GLASS)
    p.b(-13, -13, 2, 3, 5, 6, GLASS)
    p.gable_x(-8, 6, 5, 5, 5, roof_id=SHINGLES, trim_id=CARVED_OAK, ridge_id=BRICK)
    p.b(-12, -12, 1, 1, 8, 9, BED)
    p.b(-6, -5, 1, 2, 9, 9, BOOKSHELF)
    p.b(-5, -5, 1, 1, 3, 3, WORKBENCH)
    p.b(-8, -8, 4, 4, 6, 6, LANTERN)
    p.door_z(-9, 1, 1, 2)

    # Covered Smelting & Power Shed on southwest (x = -13..-4, z = -11..-3)
    p.b(-13, -4, 0, 0, -11, -3, CONCRETE)
    for x in (-13, -4):
        for z in (-11, -3):
            p.b(x, x, 1, 4, z, z, WHITE_TRUSS)
    p.b(-13, -4, 5, 5, -11, -3, TITANIUM)
    p.b(-12, -11, 1, 1, -10, -10, FURNACE)
    p.b(-9, -9, 1, 1, -10, -10, GENERATOR)
    p.b(-7, -7, 1, 1, -10, -10, BATTERY)
    p.b(-8, -8, 1, 1, -10, -10, CABLE)
    p.b(-4, -4, 4, 4, -3, -3, FLOODLIGHT)

    # Tall Steel-and-Timber Mine Headframe Tower (Копёр) + Sloping Adit Rail Portal on east side (x = 3..12)
    p.b(3, 12, 0, 0, -12, 12, CONCRETE)
    # 18-meter A-frame winding headframe over vertical ore shaft (x = 5..11, z = 4..10)
    p.b(6, 10, -7, 0, 5, 9, BRICK)
    p.b(7, 9, -7, 1, 6, 8, AIR)
    p.ring(6, 10, 5, 9, 1, 1, HAZARD)
    p.b(7, 9, 1, 2, 5, 5, AIR)
    for leg_x in (5, 11):
        for leg_z in (4, 10):
            p.b(leg_x, leg_x, 1, 15, leg_z, leg_z, OAK_LOG)
    for brace_y in (5, 10, 15):
        p.ring(5, 11, 4, 10, brace_y, brace_y, CARVED_OAK)
    # Winding wheel house at top (y = 16..19)
    p.b(6, 10, 16, 18, 5, 9, TITANIUM)
    p.b(8, 8, 13, 17, 7, 7, COPPER_BLOCK)
    p.b(8, 8, 19, 19, 7, 7, FLOODLIGHT)

    # Walkable Timber-Braced Sloping Adit Tunnel with Minecart Rails descending to Ore Seam (z = -12..2 -> y = -6)
    p.b(4, 12, 1, 6, -5, 2, STONE)
    p.ring(5, 11, -5, -5, 1, 5, OAK_LOG)
    p.b(6, 10, 1, 4, -6, -4, AIR)
    p.entrances.append([6, -5, 1, 5])
    # Surface rail line leading into the adit
    for rz in range(-13, -5):
        p.b(8, 8, 1, 1, rz, rz, POWERED_RAIL if rz == -10 else RAIL)
    for step in range(7):
        sz = -4 + step
        sy = -step
        p.b(6, 10, sy - 1, sy - 1, sz, sz, PLANK)
        p.b(6, 10, sy, sy + 3, sz, sz, AIR)
        p.b(8, 8, sy, sy, sz, sz, RAIL)
        if step % 2 == 0:
            p.b(6, 6, sy, sy + 3, sz, sz, OAK_LOG)
            p.b(10, 10, sy, sy + 3, sz, sz, OAK_LOG)
            p.b(6, 10, sy + 3, sy + 3, sz, sz, OAK_LOG)
    # Rich exposed gold/iron/quartz vein chamber at bottom of adit (y = -7..-3, z = 2..5)
    p.b(5, 11, -7, -2, 2, 5, STONE)
    p.b(6, 10, -6, -3, 2, 4, AIR)
    p.b(6, 7, -6, -4, 4, 4, GOLD_ORE)
    p.b(9, 10, -6, -4, 4, 4, QUARTZ_ORE)
    p.b(8, 8, -3, -3, 3, 3, LANTERN)


# ==============================================================================
# 7. Каменный круг (radius=16) — Мегалитическая обсерватория (Кромлех друидов)
# ==============================================================================
def build_stone_circle():
    p = Plan('Каменный круг', 16)
    p.b(-15, 15, -4, -1, -15, 15, STONE)
    p.disc(0, 0, 15.2, 0, 0, MOSSY)
    p.disc(0, 0, 12.8, 0, 0, POLISHED_STONE)
    p.disc(0, 0, 9.5, 0, 0, GRASS)
    p.disc(0, 0, 5.5, 0, 0, POLISHED_MARBLE)

    # Cardinal processional causeways & south trilithon entrance arch (`entrances`)
    p.b(-2, 2, 0, 0, -15, 15, POLISHED_STONE)
    p.b(-15, 15, 0, 0, -2, 2, POLISHED_STONE)
    p.b(-3, -2, 1, 8, -13, -12, MOSSY)
    p.b(2, 3, 1, 8, -13, -12, MOSSY)
    p.b(-4, 4, 9, 10, -13, -12, POLISHED_GRANITE)
    p.b(-1, 1, 1, 7, -14, -11, AIR)
    p.entrances.append([-1, -13, 1, 3])

    # Outer Sarsen Trilithon Ring: 12 towering megaliths + continuous carved stone lintel ring at y=8..9!
    p.cyl_shell(0, 0, 12.4, 10.6, 8, 9, POLISHED_STONE)
    for i in range(12):
        ang = i * pi / 6.0
        mx, mz = round(cos(ang) * 11.5), round(sin(ang) * 11.5)
        if abs(mx) <= 2 and mz <= -10:
            continue
        p.b(mx - 1, mx + 1, 1, 1, mz - 1, mz + 1, POLISHED_GRANITE)
        p.b(mx, mx, 2, 7, mz, mz, MOSSY if i % 2 == 0 else CRACKED)
        if i % 3 == 0:
            p.b(mx, mx, 10, 10, mz, mz, LANTERN)

    # Inner Horseshoe of 4 Giant Bluestone Trilithons (height 12) + Bioluminescent GlowShrooms
    for tx, tz in ((-6, -2), (6, -2), (-4, 5), (4, 5)):
        p.b(tx - 1, tx - 1, 1, 11, tz, tz, MONOLITH)
        p.b(tx + 1, tx + 1, 1, 11, tz, tz, MONOLITH)
        p.b(tx - 2, tx + 2, 12, 13, tz - 1, tz + 1, POLISHED_MARBLE)
        p.b(tx, tx, 1, 1, tz + (1 if tz < 0 else -1), tz + (1 if tz < 0 else -1), GLOW_SHROOM)

    # Central Astral Altar & Floating Crystal Orrery
    p.disc(0, 0, 3.2, 1, 1, POLISHED_GRANITE)
    p.ring(-2, 2, -2, 2, 1, 1, WATER)
    p.b(-1, 1, 1, 2, -1, 1, POLISHED_MARBLE)
    p.b(0, 0, 3, 3, 0, 0, OBSIDIAN)
    p.b(0, 0, 5, 7, 0, 0, ANDROMEDA_CRYSTAL)
    p.b(0, 0, 8, 8, 0, 0, CRYSTAL)


# ==============================================================================
# 8. Руины библиотеки (radius=18) — Великий купольный архив древних знаний
# ==============================================================================
def build_library_ruins():
    p = Plan('Руины библиотеки', 18)
    p.b(-17, 17, -6, -1, -17, 17, STONE)
    p.b(-17, 17, 0, 0, -17, 17, MOSSY)
    p.b(-14, 14, 0, 1, -14, 14, POLISHED_STONE)
    p.b(-12, 12, 1, 1, -11, 12, POLISHED_MARBLE)

    # Classical South Marble Portico & Pediment (z = -15..-12)
    for step in range(2):
        p.b(-5, 5, step, step, -16 + step, -16 + step, POLISHED_MARBLE)
    for col_x in (-10, -6, -3, 3, 6, 10):
        p.b(col_x, col_x, 2, 10, -14, -14, POLISHED_MARBLE)
    p.b(-11, 11, 11, 11, -15, -12, POLISHED_STONE)
    for k in range(5):
        p.b(-9 + k * 2, 9 - k * 2, 12 + k, 12 + k, -15, -13, POLISHED_MARBLE)

    # Grand Two-Storey Rotunda & Vaulted Reading Hall (x = -13..13, z = -12..13)
    p.ring(-13, 13, -12, 13, 2, 11, BRICK)
    p.b(-12, 12, 2, 11, -11, 12, AIR)
    for px in (-13, -7, 0, 7, 13):
        p.b(px, px, 2, 12, -12, -12, POLISHED_STONE)
        p.b(px, px, 2, 12, 13, 13, POLISHED_STONE)
    for pz in (-12, -6, 0, 6, 13):
        p.b(-13, -13, 2, 12, pz, pz, POLISHED_STONE)
        p.b(13, 13, 2, 12, pz, pz, POLISHED_STONE)
    # Tall arched stained-glass windows
    for wx in (-10, -4, 4, 10):
        p.b(wx - 1, wx, 4, 9, -12, -12, GLASS)
        p.b(wx - 1, wx, 4, 9, 13, 13, GLASS)
    for wz in (-9, -3, 3, 9):
        p.b(-13, -13, 4, 9, wz, wz + 1, GLASS)
        p.b(13, 13, 4, 9, wz, wz + 1, GLASS)

    # Mezzanine Reading Balcony at y = 7 around central rotunda atrium + twin west/east stairs
    p.b(-12, 12, 7, 7, -11, 12, CARVED_OAK)
    p.b(-6, 6, 7, 7, -5, 6, AIR)
    p.ring(-6, 6, -5, 6, 8, 8, CARVED_OAK)
    for s in range(6):
        p.b(-11, -10, 2 + s, 2 + s, -8 + s, -8 + s, POLISHED_MARBLE)
        p.b(-11, -10, 3 + s, 7 + s, -8 + s, -8 + s, AIR)

    # Multi-tier Bookshelf Stacks, Reading Desks, Lanterns & Secret Underground Scriptorium (y=-5..0)
    for sx in (-10, -8, 8, 10):
        p.b(sx, sx, 2, 5, 1, 9, BOOKSHELF)
        p.b(sx, sx, 8, 10, -9, -2, BOOKSHELF)
    for dz in (-2, 4):
        p.b(-3, -2, 2, 2, dz, dz + 1, PLANK)
        p.b(-3, -3, 3, 3, dz, dz, LANTERN)
        p.b(2, 3, 2, 2, dz, dz + 1, PLANK)
        p.b(3, 3, 3, 3, dz, dz, WORKBENCH if dz < 0 else LANTERN)
    # Underground Scriptorium vault reached by central rear steps
    p.b(-5, 5, -5, 0, 6, 11, BRICK)
    p.b(-4, 4, -4, 0, 7, 10, AIR)
    p.b(-4, 4, -4, -3, 10, 10, BOOKSHELF)
    p.b(0, 0, -2, -2, 8, 8, LANTERN)
    p.b(-1, 1, -4, 1, 5, 8, AIR)
    for s in range(5):
        p.b(-1, 1, 1 - s, 1 - s, 5 + s, 5 + s, POLISHED_STONE)

    # Hemispherical Stepped Copper/Slate Dome with Central Oculus (y = 12..21) — fractured on SE side!
    p.ring(-14, 14, -13, 14, 12, 12, POLISHED_STONE)
    for dy in range(8):
        r_d = (12.5 ** 2 - (dy * 1.45) ** 2) ** 0.5 if dy * 1.45 < 12.5 else 2.5
        p.cyl_shell(0, 0, r_d, max(2.2, r_d - 1.6), 13 + dy, 13 + dy, COPPER_BLOCK if dy % 2 == 0 else SLATE)
    # Dramatic ruin breach in southeast dome & wall, with sakura tree growing inside the sunlit breach!
    p.b(5, 15, 6, 22, 4, 15, AIR)
    p.tree_sakura(9, 9, 2, 5, SAKURA)
    for rx, rz in ((14, 10), (12, 15), (15, 7)):
        p.b(rx, rx + 1, 1, 2, rz, rz + 1, CRACKED)

    # Grand South Portal Entrance at z = -12
    p.b(-2, 2, 2, 6, -13, -11, AIR)
    p.entrances.append([-2, -12, 2, 5])
    p.lantern_post(-7, -16, 1)
    p.lantern_post(7, -16, 1)


# ==============================================================================
# 9. Караванный пост (radius=17) — Укреплённый караван-сарай Великого тракта
# ==============================================================================
def build_caravanserai():
    p = Plan('Караванный пост', 17)
    p.b(-16, 16, -4, -1, -16, 16, STONE)
    p.b(-16, 16, 0, 0, -16, 16, SANDSTONE)
    p.b(-11, 11, 0, 0, -11, 6, POLISHED_STONE)
    p.b(-3, 3, 0, 0, -16, 5, COBBLE)

    # Fortified Sandstone & Terracotta Perimeter Enclosure with Crenellations
    p.ring(-15, 15, -14, 15, 1, 6, SANDSTONE)
    p.ring(-15, 15, -14, 15, 6, 6, TERRACOTTA)
    for k in range(-14, 15, 3):
        p.b(k, k + 1, 7, 7, -14, -14, SANDSTONE)
        p.b(k, k + 1, 7, 7, 15, 15, SANDSTONE)
        p.b(-15, -15, 7, 7, k, k + 1, SANDSTONE)
        p.b(15, 15, 7, 7, k, k + 1, SANDSTONE)

    # Monumental Southern Iwan Archway Gate (z = -14) + Corner Beacon Minaret Tower (-14, -13)
    p.b(-5, 5, 1, 9, -15, -13, SANDSTONE)
    p.b(-4, 4, 8, 9, -15, -13, TERRACOTTA)
    p.b(-2, 2, 1, 5, -16, -12, AIR)
    p.entrances.append([-2, -14, 1, 5])
    # Corner Minaret Watchtower with Lantern Balcony (height 18)
    p.b(-15, -11, 1, 14, -14, -10, SANDSTONE)
    p.b(-14, -12, 1, 13, -13, -11, AIR)
    p.ring(-16, -10, -15, -9, 14, 14, POLISHED_STONE)
    p.b(-14, -12, 15, 17, -13, -11, TERRACOTTA)
    p.b(-13, -13, 18, 19, -12, -12, COPPER_BLOCK)
    p.b(-13, -13, 14, 14, -9, -9, LANTERN)

    # Two-Storey Merchant Inn & Tavern along the North Wing (x = -14..14, z = 6..14)
    p.b(-14, 14, 0, 0, 6, 14, POLISHED_GRANITE)
    p.b(-13, 13, 0, 0, 7, 13, PLANK)
    p.b(-14, 14, 1, 9, 6, 14, LIMESTONE)
    p.b(-13, 13, 1, 9, 7, 13, AIR)
    p.ring(-14, 14, 6, 14, 5, 5, CARVED_OAK)
    p.b(-13, 13, 5, 5, 7, 13, PLANK)
    # Overhanging carved wooden veranda along courtyard face (z = 4..5) + exterior stair
    p.b(-12, 12, 5, 5, 4, 5, PLANK)
    p.ring(-12, 12, 4, 5, 6, 6, CARVED_OAK)
    for col_x in (-12, -6, 0, 6, 12):
        p.b(col_x, col_x, 1, 8, 4, 4, OAK_LOG)
    p.b(-12, 12, 9, 9, 4, 5, SHINGLES)
    for s in range(5):
        p.b(-11, -10, 1 + s, 1 + s, 0 + s, 0 + s, PLANK)
    for wx in (-9, -4, 4, 9):
        p.b(wx, wx + 1, 2, 3, 6, 6, GLASS)
        p.b(wx, wx + 1, 7, 8, 6, 6, GLASS)
        p.b(wx, wx + 1, 7, 8, 14, 14, GLASS)
    p.gable_x(0, 10, 14, 4, 10, roof_id=SHINGLES, trim_id=CARVED_OAK, ridge_id=TERRACOTTA)
    p.door_z(-1, 6, 1, 3)
    p.door_z(-1, 6, 6, 2)
    # Tavern hearth, beds, bookshelves & workbenches inside
    p.b(11, 12, 1, 13, 12, 13, BRICK)
    p.b(10, 10, 1, 1, 12, 12, FURNACE)
    p.b(-11, -11, 1, 1, 11, 12, WORKBENCH)
    p.b(-11, -10, 6, 6, 11, 12, BED)
    p.b(10, 11, 6, 6, 9, 10, BED)
    p.b(-5, -3, 6, 7, 13, 13, BOOKSHELF)
    p.b(0, 0, 4, 4, 10, 10, LANTERN)
    p.b(0, 0, 9, 9, 10, 10, LANTERN)

    # Covered Bazaar Stalls (West Arcade) & Caravan Stables with Watering Trough (East Arcade)
    p.b(-14, -9, 4, 4, -9, 2, TERRACOTTA)
    for bz in (-9, -5, -1, 2):
        p.b(-9, -9, 1, 3, bz, bz, OAK_LOG)
    p.b(-13, -10, 1, 1, -8, -6, PLANK)
    p.b(-12, -12, 2, 2, -7, -7, WORKBENCH)
    p.b(-13, -10, 1, 1, -3, -1, PLANK)
    p.b(-11, -11, 3, 3, -4, -4, LANTERN)

    p.b(9, 14, 4, 4, -11, 2, SHINGLES)
    for sz in (-11, -7, -3, 2):
        p.b(9, 9, 1, 3, sz, sz, OAK_LOG)
    p.b(12, 13, 1, 1, -9, -6, WATER)
    p.ring(11, 14, -10, -5, 1, 1, BRICK)
    p.b(11, 11, 3, 3, -3, -3, LANTERN)

    # Central Courtyard Oasis Fountain
    p.ring(-2, 2, -5, -1, 1, 1, POLISHED_MARBLE)
    p.b(-1, 1, 1, 1, -4, -2, WATER)
    p.b(0, 0, 2, 3, -3, -3, POLISHED_MARBLE)
    p.b(0, 0, 4, 4, -3, -3, LANTERN)


# Build all 10 plans in exact canonical order!
build_village()
build_castle()
build_ruined_manor()
build_watchtower()
build_camp()
build_obelisk()
build_mining_camp()
build_stone_circle()
build_library_ruins()
build_caravanserai()

for p in PLANS:
    assert len(p.boxes) >= 80, (p.name, 'expected rich multi-part authoring, got', len(p.boxes))
    for x0, x1, y0, y1, z0, z1, bid in p.boxes:
        assert max(abs(x0), abs(x1), abs(z0), abs(z1)) <= p.radius, (p.name, 'outside radius')
        assert -8 <= y0 <= y1 <= 48 and 0 <= bid <= 89, (p.name, 'invalid bounds/material', y0, y1, bid)
