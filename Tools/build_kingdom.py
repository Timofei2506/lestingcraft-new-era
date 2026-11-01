#!/usr/bin/env python3
"""Author the Tsardom landmark: a grand castle with a ring settlement and a great
road to Quietford. Emits Assets/Scripts/World/KingdomPlan.cs with bucketed voxel
boxes (16x16 cells, sorted keys) and road capsules sampled at terrain height.

Re-run after editing the layout below. Pure integer boxes; no renders.
"""
from pathlib import Path
import math

r = Path(__file__).resolve().parents[1]
CX, CZ = 420, -340          # world center of the castle keep
CORE = 122                  # flat core radius; blend 122..150
REGION = 150

boxes = []                  # [x0,x1,y0,y1,z0,z1,id]
roads = []                  # (ax,az,bx,bz,radius)


def box(x, y, z, w, h, d, bid):
    boxes.append([x, x + w - 1, y, y + h - 1, z, z + d - 1, bid])


def road(ax, az, bx, bz, rad=3):
    roads.append((ax, az, bx, bz, rad))


# ---------------------------------------------------------------- castle ----
def crenellate(x0, x1, z0, z1, y, bid=37, step=4):
    for x in range(x0, x1 + 1, step):
        box(x, y, z0, 2, 2, 1, bid)
        box(x, y, z1, 2, 2, 1, bid)
    for z in range(z0 + step, z1, step):
        box(x0, y, z, 1, 2, 2, bid)
        box(x1, y, z, 1, 2, 2, bid)


def wall_ring(cx, cz, half, thick, y, h, bid=37):
    box(cx - half, y, cz - half, 2 * half + 1, h, thick, bid)          # north
    box(cx - half, y, cz + half - thick + 1, 2 * half + 1, h, thick, bid)  # south
    box(cx - half, y, cz - half, thick, h, 2 * half + 1, bid)          # west
    box(cx + half - thick + 1, y, cz - half, thick, h, 2 * half + 1, bid)  # east


def tower(cx, cz, half, y, h, bid=37, roof=59, windows=True):
    box(cx - half, y, cz - half, 2 * half + 1, h, 2 * half + 1, bid)
    # decorative band + crown
    box(cx - half - 1, y + h - 3, cz - half - 1, 2 * half + 3, 1, 2 * half + 3, 40)
    box(cx - half - 1, y + h, cz - half - 1, 2 * half + 3, 1, 2 * half + 3, roof)
    for level in range(1, 4):
        s = half + 1 - level * 2
        if s < 1:
            break
        box(cx - s, y + h + level, cz - s, 2 * s + 1, 1, 2 * s + 1, roof)
    box(cx, y + h + 4, cz, 1, 3, 1, 4)
    box(cx + 1, y + h + 5, cz, 1, 2, 1, 62)  # banner
    if windows:
        for wy in (y + 6, y + 11, y + 16):
            if wy + 1 < y + h - 3:
                box(cx - half - 1, wy, cz - 1, 1, 2, 2, 44)
                box(cx + half + 1, wy, cz - 1, 1, 2, 2, 44)
                box(cx - 1, wy, cz - half - 1, 2, 2, 1, 44)
                box(cx - 1, wy, cz + half + 1, 2, 2, 1, 44)


def keep():
    # courtyard pavement with brick borders
    box(-34, 0, -34, 69, 1, 69, 40)
    box(-34, 0, -34, 69, 1, 3, 37)
    box(-34, 0, 32, 69, 1, 3, 37)
    box(-34, 0, -34, 3, 1, 69, 37)
    box(32, 0, -34, 3, 1, 69, 37)
    # curtain wall (gate gap on the south side)
    wall_ring(0, 0, 36, 3, 1, 14)
    crenellate(-36, 36, -36, 36, 15)
    box(-5, 1, 34, 11, 13, 5, 0)  # gate opening
    box(-6, 14, 33, 13, 2, 6, 37)
    # gatehouse turrets
    tower(-9, 33, 3, 1, 18, windows=True)
    tower(9, 33, 3, 1, 18, windows=True)
    box(-4, 1, 36, 2, 6, 1, 54)
    box(3, 1, 36, 2, 6, 1, 54)
    # corner towers
    for tx, tz in ((-34, -34), (34, -34), (-34, 34), (34, 34)):
        tower(tx, tz, 5, 1, 21)
    # ---- central palace ----
    box(-13, 1, -13, 27, 20, 27, 37)              # main body
    box(-13, 1, -13, 27, 1, 27, 40)               # polished plinth
    box(-12, 21, -12, 25, 6, 25, 42)              # marble stage
    box(-8, 27, -8, 17, 5, 17, 30)                # upper marble
    box(-5, 32, -5, 11, 3, 11, 42)
    for lv in range(4):                           # roof pyramid
        s = 6 - lv * 2
        box(-s, 35 + lv, -s, 2 * s + 1, 1, 2 * s + 1, 59)
    box(0, 39, 0, 1, 5, 1, 4)
    box(1, 41, 0, 1, 3, 1, 62)
    # windows: tall slots on each face
    for wy in (4, 9, 14):
        for wx in (-9, -4, 4, 9):
            box(wx, wy, -14, 2, 3, 1, 44)
            box(wx, wy, 14, 2, 3, 1, 44)
            box(-14, wy, wx, 1, 3, 2, 44)
            box(14, wy, wx, 1, 3, 2, 44)
    for wy in (22, 28):
        for wx in (-6, 0, 6):
            box(wx, wy, -13, 2, 3, 1, 44)
            box(wx, wy, 13, 2, 3, 1, 44)
            box(-13, wy, wx, 1, 3, 2, 44)
            box(13, wy, wx, 1, 3, 2, 44)
    # entrance: steps + door + pillars + balcony
    for i in range(4):
        box(-3 - i, 1, 16 + i, 7 + 2 * i, 1, 2, 40)
    box(-2, 1, 13, 5, 6, 1, 0)
    box(-3, 7, 13, 7, 1, 2, 58)
    box(-13, 20, 12, 27, 1, 4, 58)                # balcony ring (south)
    box(-13, 20, -15, 27, 1, 4, 58)
    box(-15, 20, -13, 4, 1, 27, 58)
    box(12, 20, -13, 4, 1, 27, 58)
    # interior floors with lantern halls
    for fy in (7, 13, 19):
        box(-12, fy, -12, 25, 1, 25, 18)
        box(-10, fy + 1, -10, 1, 4, 1, 57)
        box(10, fy + 1, -10, 1, 4, 1, 57)
        box(-10, fy + 1, 10, 1, 4, 1, 57)
        box(10, fy + 1, 10, 1, 4, 1, 57)
        box(-1, fy + 4, -1, 2, 1, 2, 56)
    # throne backdrop
    box(0, 20, -11, 5, 5, 1, 62)
    box(0, 20, -10, 3, 1, 3, 42)
    # ---- courtyard buildings ----
    house(-24, -18, 12, 8, True)   # barracks (west)
    house(22, -18, 12, 8, True)    # stable (east)
    box(22, 1, -14, 12, 1, 1, 0)   # stable open front
    # well
    box(-2, 1, 20, 5, 1, 5, 36)
    box(-1, 1, 21, 3, 1, 3, 7)
    for wx, wz in ((-2, 20), (2, 20), (-2, 24), (2, 24)):
        box(wx, 2, wz, 1, 3, 1, 4)
    box(-3, 5, 19, 7, 1, 7, 59)
    box(0, 6, 22, 1, 1, 1, 56)
    # chapel (north-east corner)
    house(16, -28, 10, 10)
    tower(20, -24, 2, 1, 14, windows=True)
    # lamp posts around the yard
    for lx, lz in ((-18, 0), (18, 0), (0, -22), (0, 28), (-12, 26), (12, 26)):
        box(lx, 1, lz, 1, 4, 1, 4)
        box(lx, 5, lz, 1, 1, 1, 56)


def house(cx, cz, w=9, d=9, flat=False):
    x = cx - w // 2
    z = cz - d // 2
    box(x, 0, z, w, 1, d, 36)
    box(x + 1, 1, z + 1, w - 2, 4, d - 2, 0)
    box(x, 1, z, w, 4, 1, 18)
    box(x, 1, z + d - 1, w, 4, 1, 18)
    box(x, 1, z, 1, 4, d, 18)
    box(x + w - 1, 1, z, 1, 4, d, 18)
    for ax in (x, x + w - 1):
        for az in (z, z + d - 1):
            box(ax, 1, az, 1, 5, 1, 4)
    for az in (z, z + d - 1):
        for ax in (x + 2, x + w - 3):
            box(ax, 2, az, 2, 2, 1, 44)
    box(cx, 1, z + d - 1, 2, 3, 1, 0)      # doorway
    box(cx, 0, z + d, 2, 1, 2, 36)
    box(cx - 1, 4, z + d - 1, 4, 1, 1, 58)  # door lintel
    box(x + w - 3, 1, z + 1, 1, 4, 1, 39)  # chimney
    box(x + w - 3, 5, z + 1, 1, 2, 1, 39)
    if flat:
        for level in range(w // 2 + 1):
            box(x - 1 + level, 5 + level, z - 1, w + 2 - 2 * level, 1, d + 2, 29)
    else:
        for level in range(min(w, d) // 2 + 1):
            box(x - 1 + level, 5 + level, z - 1, w + 2 - 2 * level, 1, d + 2, 59)
    box(x + 1, 1, z + 1, 2, 1, 3, 18)
    box(x + w - 3, 1, z + 1, 2, 1, 2, 15)


def stall(x, z):
    for px, pz in ((x, z), (x + 3, z), (x, z + 2), (x + 3, z + 2)):
        box(px, 1, pz, 1, 3, 1, 4)
    box(x - 1, 4, z - 1, 6, 1, 5, 18)
    box(x, 3, z, 4, 1, 1, 36)
    box(x, 1, z, 1, 1, 1, 45)
    box(x + 3, 1, z + 1, 1, 1, 1, 45)


def tree(x, z, h=5):
    box(x, 1, z, 1, h, 1, 4)
    box(x - 2, h, z - 2, 5, 3, 5, 5)
    box(x - 1, h + 3, z - 1, 3, 2, 3, 5)


# ------------------------------------------------------------ settlement ----
def settlement():
    # 24 houses on three arcs facing the keep
    arc_specs = [(52, 11, 0), (74, 15, 12), (96, 19, 25)]
    count = 0
    for radius, n, offset in arc_specs:
        for i in range(n):
            ang = math.radians(offset + i * 360.0 / n)
            hx = int(round(math.sin(ang) * radius))
            hz = int(round(math.cos(ang) * radius))
            if abs(hx) < 30 and hz > 40:      # keep the great-road corridor open
                continue
            w = 9 if count % 3 else 11
            d = 11 if count % 3 == 1 else 9
            house(hx, hz, w, d)
            count += 1
    # market square (east)
    box(70, 0, -14, 22, 1, 28, 43)
    box(78, 1, -2, 6, 1, 6, 36)
    box(79, 1, -1, 4, 1, 4, 7)
    for px, pz in ((79, -2), (82, -2), (79, 1), (82, 1)):
        box(px, 2, pz, 1, 3, 1, 4)
    box(78, 5, -3, 6, 1, 6, 59)
    box(80, 6, -1, 1, 1, 1, 56)
    for i, sx in enumerate((71, 77, 83)):
        stall(sx, -10)
        stall(sx, 4)
    # blacksmith (south-west)
    box(-78, 0, 48, 20, 1, 16, 36)
    for px, pz in ((-78, 48), (-60, 48), (-78, 62), (-60, 62)):
        box(px, 1, pz, 1, 5, 1, 4)
    box(-79, 6, 47, 22, 1, 18, 59)
    box(-72, 1, 52, 1, 1, 1, 17)
    box(-68, 1, 52, 1, 1, 1, 17)
    box(-70, 2, 52, 3, 1, 1, 27)
    box(-66, 1, 50, 2, 7, 2, 39)  # chimney
    box(-66, 8, 50, 2, 3, 2, 39)
    # chapel (north)
    house(0, -84, 12, 18)
    tower(0, -70, 3, 1, 22)
    box(0, 1, -80, 4, 1, 6, 0)
    box(-1, 1, -88, 3, 1, 4, 55)
    box(2, 1, -88, 3, 1, 4, 55)
    # farms (two plots)
    for fx, fz in ((96, -60), (-96, -20)):
        for row in range(4):
            box(fx - 8, 1, fz + row * 3, 16, 1, 2, 2)
            box(fx - 8, 1, fz + row * 3 + 2, 16, 1, 1, 7)
        for px in range(fx - 9, fx + 10, 3):
            box(px, 1, fz - 2, 1, 2, 1, 4)
            box(px, 1, fz + 13, 1, 2, 1, 4)
    # trees & lamps along the ring
    for i in range(16):
        ang = math.radians(i * 22.5 + 7)
        tx = int(round(math.sin(ang) * 112))
        tz = int(round(math.cos(ang) * 112))
        tree(tx, tz, 4 + (i % 3))
    for i in range(12):
        ang = math.radians(i * 30 + 15)
        lx = int(round(math.sin(ang) * 44))
        lz = int(round(math.cos(ang) * 44))
        if lz > 30 and abs(lx) < 12:
            continue
        box(lx, 1, lz, 1, 4, 1, 4)
        box(lx, 5, lz, 1, 1, 1, 56)
    # road sign near the great road
    box(-10, 1, 52, 1, 3, 1, 4)
    box(-11, 3, 52, 3, 2, 1, 58)


keep()
settlement()

# great road: castle gate -> Quietford well, plus avenues and a ring
road(CX, CZ + 40, 96, 48, 4)
road(CX - 30, CZ, CX + 30, CZ, 3)
road(CX, CZ - 30, CX, CZ + 30, 3)
ring = []
for i in range(8):
    a = math.radians(i * 45)
    ring.append((int(CX + math.sin(a) * 44), int(CZ + math.cos(a) * 44)))
for i in range(8):
    road(ring[i][0], ring[i][1], ring[(i + 1) % 8][0], ring[(i + 1) % 8][1], 3)

# ------------------------------------------------------------------ emit ----
cells = {}
for idx, b in enumerate(boxes):
    for cx in range(b[0] >> 4, (b[1] >> 4) + 1):
        for cz in range(b[4] >> 4, (b[5] >> 4) + 1):
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
for b in boxes:
    flat_boxes.extend(b)

lines = []
lines.append("using Unity.Mathematics;")
lines.append("namespace LestingCraft.World")
lines.append("{")
lines.append("    /// <summary>The Tsardom: a grand castle keep with a ring settlement and the")
lines.append("    /// great road to Quietford. Authored by Tools/build_kingdom.py (bucketed boxes).</summary>")
lines.append("    public static class KingdomPlan")
lines.append("    {")
lines.append(f"        public const int CenterX={CX},CenterZ={CZ},Core={CORE},Region={REGION};")
lines.append("        public static bool Inside(int x,int z)=>math.abs(x-CenterX)<=Region&&math.abs(z-CenterZ)<=Region;")
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

        static int GroundAt(int x,int ry,int z)
        {
            int key=((x>>4)+2048)*4096+((z>>4)+2048);
            int lo=0,hi=CellKeys.Length-1;
            while(lo<=hi){int mid=(lo+hi)/2;int c=CellKeys[mid];if(c<key)lo=mid+1;else if(c>key)hi=mid-1;else{
                int id=0;for(int i=CellStart[mid];i<CellStart[mid+1];i++){int b=BoxIndex[i]*7;
                    if(x>=Boxes[b]&&x<=Boxes[b+1]&&ry>=Boxes[b+2]&&ry<=Boxes[b+3]&&z>=Boxes[b+4]&&z<=Boxes[b+5])id=Boxes[b+6];}
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
            int baseY=math.max(44,TerrainGenerator.Height(CenterX,CenterZ,seed,Dimension.Lestingia));
            int natural2=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia),top=baseY;
            if(dist>0){int blend=math.min(6,dist);top=(baseY*(6-blend)+natural2*blend)/6;}
            if(y<top-3&&y<=natural2)return false;
            if(y<=top)
            {
                block=y==top?(dist<=0?(ushort)40:(ushort)2):y<top-2?(ushort)1:(ushort)2;
                if(y==top&&dist<=0){int g=GroundAt(x,0,z);if(g!=0)block=(ushort)g;}
                return true;
            }
            if(y>baseY+46)return true;
            if(dist>0)return true;
            int ry=y-baseY;
            block=(ushort)GroundAt(x,ry,z);
            return true;
        }
    }
}
""")
out = r / "Assets/Scripts/World/KingdomPlan.cs"
out.write_text("\n".join(lines), encoding="utf-8")
print(f"KingdomPlan.cs: {len(boxes)} boxes, {len(flat)} bucket entries, {len(roads)} road segments, {len(out.read_text(encoding='utf-8'))} bytes")
