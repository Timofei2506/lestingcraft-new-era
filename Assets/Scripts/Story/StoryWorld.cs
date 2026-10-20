using Unity.Mathematics;
using LestingCraft.World;

namespace LestingCraft.Story
{
    public static class StoryWorld
    {
        public static int Floor(int seed) => math.max(44, TerrainGenerator.Height(0, 0, seed, Dimension.Lestingia));
        public static bool Protected(int x, int z) => Region(x, z) <= 0;

        static int Region(int x, int z) => math.min(
            math.max(math.abs(x), math.abs(z - 8)) - 9,
            math.min(
                math.min(math.max(math.abs(x + 32), math.abs(z + 32)) - 12, math.max(math.abs(x - 96), math.abs(z - 48)) - 24),
                math.min(math.max(math.abs(x - 48), math.abs(z + 96)) - 13, math.max(math.abs(x - 176), math.abs(z + 112)) - 17)));

        static bool Segment(int x, int z, int ax, int az, int bx, int bz, int r)
        {
            long dx = bx - ax, dz = bz - az, px = x - ax, pz = z - az, len = dx * dx + dz * dz, t = px * dx + pz * dz;
            if (t < 0) return px * px + pz * pz <= r * r;
            if (t > len) { px = x - bx; pz = z - bz; return px * px + pz * pz <= r * r; }
            long cross = px * dz - pz * dx;
            return cross * cross <= r * r * len;
        }

        static bool Road(int x, int z, int r) =>
            Segment(x, z, -32, -32, 0, 8, r) ||
            Segment(x, z, 0, 8, 96, 48, r) ||
            Segment(x, z, 96, 48, 48, -96, r) ||
            Segment(x, z, 48, -96, 176, -112, r) ||
            Segment(x, z, 176, -112, 96, 48, r);

        static readonly int[][] StreetLampsV8 =
        {
            new[] { 88, 48, 56 }, new[] { 105, 48, 56 }, new[] { 96, 26, 56 }, new[] { 96, 73, 56 },
            new[] { -39, -24, 54 }, new[] { 43, -86, 54 },
            new[] { 80, 48, 56 }, new[] { 112, 48, 56 }, new[] { 96, 38, 56 }, new[] { 96, 58, 56 }
        };

        static readonly int[][] VillageHousesV8 =
        {
            new[] { 82, 36 }, new[] { 109, 35 }, new[] { 82, 61 }, new[] { 110, 62 }, new[] { 96, 67 }, new[] { 48, -96 }
        };

        static int ApplyV8StoryUpgrades(int x, int ry, int z, int id)
        {
            for (int i = 0; i < StreetLampsV8.Length; i++)
            {
                int lx = StreetLampsV8[i][0], lz = StreetLampsV8[i][1], light = StreetLampsV8[i][2];
                if (x == lx && z == lz)
                {
                    if (ry >= 1 && ry <= 3) return 4;
                    if (ry == 4) return 58;
                    if (ry == 5) return light;
                }
                if (math.abs(x - lx) == 1 && z == lz && ry == 4) return 58;
                if (math.abs(x - lx) == 1 && z == lz && ry == 3 && light == 56) return 56;
            }
            for (int i = 0; i < VillageHousesV8.Length; i++)
            {
                int dx = x - VillageHousesV8[i][0], dz = z - VillageHousesV8[i][1];
                if (math.abs(dx) > 4 || math.abs(dz) > 4) continue;
                if (id == 46 && ry >= 4) id = 59;
                if (ry == 1 && dx == -2 && dz == 2) return 55;
                if ((ry == 1 || ry == 2) && dx == 2 && dz == -2) return 57;
                if (ry == 2 && dx == 0 && dz == -4) return 54;
            }
            return id;
        }

        public static bool TrySample(int x, int y, int z, int seed, out ushort block) =>
            TrySample(x, y, z, seed, out block, 7);

        public static bool TrySample(int x, int y, int z, int seed, out ushort block, int contentVersion)
        {
            block = 0;
            if (y <= 0 || y >= 256) return false;
            int dist = Region(x, z);
            bool road = Road(x, z, 2);
            if (dist > 6 && !Road(x, z, 5)) return false;
            int baseY = Floor(seed), natural = TerrainGenerator.Height(x, z, seed, Dimension.Lestingia), top = baseY;
            if (dist > 0 && !road)
            {
                int blend = math.min(6, dist);
                top = (baseY * (6 - blend) + natural * blend) / 6;
            }
            if (y < top - 3 && y <= natural) return false;
            if (y <= top)
            {
                block = y == top ? (road ? (ushort)32 : dist <= 0 ? (ushort)3 : (ushort)2) : y < top - 2 ? (ushort)1 : (ushort)2;
                if (y == baseY && dist <= 0)
                {
                    var plan = StoryPlan.Boxes;
                    for (int i = 0; i < plan.Length; i += 7)
                        if (x >= plan[i] && x <= plan[i + 1] && plan[i + 2] <= 0 && plan[i + 3] >= 0 && z >= plan[i + 4] && z <= plan[i + 5])
                            block = (ushort)plan[i + 6];
                }
                return true;
            }
            if (y > baseY + 18) return true;
            if (dist > 0)
            {
                if (contentVersion >= 8 && dist <= 2)
                    block = (ushort)ApplyV8StoryUpgrades(x, y - baseY, z, 0);
                return true;
            }
            int ry = y - baseY;
            var boxes = StoryPlan.Boxes;
            int id = 0;
            for (int i = 0; i < boxes.Length; i += 7)
                if (x >= boxes[i] && x <= boxes[i + 1] && ry >= boxes[i + 2] && ry <= boxes[i + 3] && z >= boxes[i + 4] && z <= boxes[i + 5])
                    id = boxes[i + 6];
            if (contentVersion >= 8)
                id = ApplyV8StoryUpgrades(x, ry, z, id);
            block = (ushort)id;
            return true;
        }
    }
}
