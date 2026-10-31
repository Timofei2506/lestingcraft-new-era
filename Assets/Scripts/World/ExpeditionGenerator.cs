using Unity.Mathematics;

namespace LestingCraft.World
{
    // Keep existing local versions frozen. Content 10 restores the server catalog/ore path;
    // content 11 selects separately authored architecture without rewriting legacy plans.
    public static class ExpeditionGenerator
    {
        public static bool Reserved(int x, int z) => x >= -110 && x <= 270 && z >= -190 && z <= 158;

        public struct Column
        {
            public int X, Z, Base, Kind, MX, MZ, MB;
            public bool Surface, Mine;
            public int XWorld, ZWorld;

            public ushort Apply(ushort original, int y, int seed) => Apply(original, y, seed, 7);

            public ushort Apply(ushort original, int y, int seed, int contentVersion)
            {
                if (y <= 0 || y >= 256) return original;
                if (Surface && y >= Base - 32 && y <= Base + 48)
                {
                    int ry = y - Base;
                    if(contentVersion>=11)return ArchitecturePlan.Apply(Kind,X,ry,Z);
                    int id = y <= Base ? (y == Base ? 3 : 1) : 0;
                    var boxes = StructurePlan.Boxes;
                    for (int i = StructurePlan.Offsets[Kind]; i < StructurePlan.Offsets[Kind + 1]; i += 7)
                        if (X >= boxes[i] && X <= boxes[i + 1] && ry >= boxes[i + 2] && ry <= boxes[i + 3] && Z >= boxes[i + 4] && Z <= boxes[i + 5])
                            id = boxes[i + 6];
                    if (contentVersion >= 8)
                    {
                        if (id == 15 && ry >= 3) id = 56;
                        if (Kind == 0)
                        {
                            if (id == 46 && ry >= 4) id = 59;
                            if (ry == 1 && ((X == -16 && Z == -13) || (X == 13 && Z == -13) || (X == -16 && Z == 15) || (X == 13 && Z == 15)))
                                id = 55;
                            if ((ry == 1 || ry == 2) && ((X == -13 && Z == -16) || (X == 16 && Z == -16) || (X == -13 && Z == 12) || (X == 16 && Z == 12)))
                                id = 57;
                        }
                    }
                    return (ushort)id;
                }
                if (Mine && y >= MB && y <= MB + 5)
                {
                    int dx = math.abs(MX), dz = math.abs(MZ), dy = y - MB;
                    bool corridor = (dx <= 32 && dz <= 2) || (dz <= 24 && dx <= 2) || (dx <= 5 && dz <= 5);
                    bool alcove=contentVersion>=11 && dx<=30 && dx%12>=5 && dx%12<=7 && dz<=5;
                    if (corridor || alcove)
                    {
                        if(contentVersion>=11)
                        {
                            if(dy==0)return (ushort)(dz>=3?36:18);
                            if(dy==5)return 1;
                            if(dy==1 && dz==0 && dx<=30)return 85;
                            if(alcove && dz==5 && dy==1 && dx%12==6)return 16;
                            if(alcove && dz==5 && dy==2 && dx%12==6)return 56;
                        }
                        if (dy == 0) return 18;
                        if (dy == 5) return 1;
                        bool beam = (dz <= 2 && dx % 6 == 0) || (dx <= 2 && dz % 6 == 0);
                        if (beam && (dy == 4 || dx == 2 || dz == 2)) return 4;
                        if (contentVersion >= 8 && dy == 3 && ((dz == 2 && dx % 12 == 6) || (dx == 2 && dz % 12 == 6))) return 54;
                        return 0;
                    }
                }
                if (y >= 5 && y <= 26 && (original == 1 || original == 13 || (original >= 19 && original <= 30)))
                {
                    uint h = TerrainGenerator.Hash((int)math.floor(XWorld / 3f), (int)math.floor(y / 3f), (int)math.floor(ZWorld / 3f), seed ^ 62801);
                    int a = ((XWorld % 3) + 3) % 3, b = y % 3, c = ((ZWorld % 3) + 3) % 3;
                    if (y <= 22 && h % 251 == 0 && math.abs(a - 1) + math.abs(b - 1) + math.abs(c - 1) <= 1) return 47;
                    if (contentVersion == 9 && h % 137 == 7 && math.abs(a - 1) + math.abs(b - 1) + math.abs(c - 1) <= 2) return 70;
                }
                if(contentVersion>=10 && y>=6 && y<=28 && (original==1||original==13||original>=19&&original<=30))
                {
                    uint u=TerrainGenerator.Hash((int)math.floor(XWorld/3f),(int)math.floor(y/3f),(int)math.floor(ZWorld/3f),seed^51977);
                    int a=((XWorld%3)+3)%3,b=y%3,c=((ZWorld%3)+3)%3;
                    if(u%109==0&&math.abs(a-1)+math.abs(b-1)+math.abs(c-1)<=2)return 70;
                }
                return original;
            }
        }

        public static Column At(int x, int z, int seed, int contentVersion = 7)
        {
            var result = new Column { XWorld = x, ZWorld = z };
            int a = (int)math.floor(x / 192f), b = (int)math.floor(z / 192f);
            uint h = TerrainGenerator.Hash(a, 0, b, seed ^ 71839);
            int ax = a * 192 + 64 + (int)(h % 64), az = b * 192 + 64 + (int)((h >> 8) % 64), kind = (int)((h >> 18) % (contentVersion >= 10 ? 10u : 5u));
            int rx = x - ax, rz = z - az, rot = (int)((h >> 26) % 4);
            int top = TerrainGenerator.Height(ax, az, seed, Dimension.Lestingia);
            if (!Reserved(ax, az) && top >= 44 && top <= 62 && math.abs(rx) <= StructurePlan.Radius[kind] && math.abs(rz) <= StructurePlan.Radius[kind])
            {
                result.Surface = true;
                result.Base = top;
                result.Kind = kind;
                result.X = rot == 0 ? rx : rot == 1 ? rz : rot == 2 ? -rx : -rz;
                result.Z = rot == 0 ? rz : rot == 1 ? -rx : rot == 2 ? -rz : rx;
            }
            a = (int)math.floor(x / 64f); b = (int)math.floor(z / 64f);
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = a + dx, cz = b + dz;
                    h = TerrainGenerator.Hash(cx, 0, cz, seed ^ 33019);
                    if (((cx != 0 || cz != 0) && h % 3 != 0) || h % 2 != 0) continue;
                    ax = cx * 64 + 20 + (int)(h % 24); az = cz * 64 + 20 + (int)((h >> 8) % 24);
                    top = TerrainGenerator.Height(ax, az, seed, Dimension.Lestingia);
                    if (top < 44 || top > 64 || Reserved(ax, az)) continue;
                    rx = x - ax - 15; rz = z - az - 8;
                    if (math.abs(rx) > 32 || math.abs(rz) > 24) continue;
                    result.Mine = true; result.MX = rx; result.MZ = rz; result.MB = math.max(9, top - 24) - 2;
                    break;
                }
            return result;
        }

        public static ushort Sample(int x, int y, int z, int seed, ushort original) => At(x, z, seed).Apply(original, y, seed, 7);
        public static ushort Sample(int x, int y, int z, int seed, ushort original, int contentVersion) => At(x, z, seed, contentVersion).Apply(original, y, seed, contentVersion);
    }
}
