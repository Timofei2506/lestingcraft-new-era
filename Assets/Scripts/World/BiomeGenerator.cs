using Unity.Mathematics;
using TreeColumn = LestingCraft.World.NatureGenerator.TreeColumn;

namespace LestingCraft.World
{
    public enum BiomeKind
    {
        Meadow = 0,
        MixedForest = 1,
        Taiga = 2,
        Dunes = 3,
        Marsh = 4,
        RockyHighlands = 5,
        CherryGrove = 6,
        RedMesa = 7,
        GlacialTundra = 8,
        VolcanicBasalt = 9,
        BioluminescentGrove = 10,
        LavenderHighlands = 11
    }

    public static class BiomeGenerator
    {
        public const int Version = 8;
        public static readonly string[] Names =
        {
            "Луга", "Смешанный лес", "Хвойная тайга",
            "Песчаные дюны", "Заболоченный лес", "Каменистые пустоши",
            "Роща сакуры", "Красный каньон", "Ледниковая тундра",
            "Вулканическое плато", "Биолюминесцентная чаща", "Лавандовое нагорье"
        };

        public static string Name(BiomeKind kind)
        {
            int idx = (int)kind;
            return idx >= 0 && idx < Names.Length ? Names[idx] : "Смешанный лес";
        }

        public static float TemperatureOffset(BiomeKind biome)
        {
            switch (biome)
            {
                case BiomeKind.Taiga: return -9f;
                case BiomeKind.Dunes: return 10f;
                case BiomeKind.Marsh: return 3f;
                case BiomeKind.RockyHighlands: return -6f;
                case BiomeKind.CherryGrove: return 2f;
                case BiomeKind.RedMesa: return 8f;
                case BiomeKind.GlacialTundra: return -12f;
                case BiomeKind.VolcanicBasalt: return 14f;
                case BiomeKind.BioluminescentGrove: return 1f;
                case BiomeKind.LavenderHighlands: return -2f;
                default: return 0f;
            }
        }

        static int Div(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;
        static bool Reserved(int x, int z) => x >= -110 && x <= 270 && z >= -190 && z <= 158;

        // Preserves exact 6-biome Voronoi output for contentVersion <= 7, and unlocks all 12 biomes for contentVersion >= 8!
        public static BiomeKind At(int x, int z, int seed) => At(x, z, seed, 7);

        public static BiomeKind At(int x, int z, int seed, int contentVersion)
        {
            if (Reserved(x, z)) return BiomeKind.MixedForest;
            int count = contentVersion >= 8 ? 12 : 6;
            int cx = Div(x, 160), cz = Div(z, 160), best = int.MaxValue;
            BiomeKind kind = BiomeKind.MixedForest;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int a = cx + dx, b = cz + dz;
                    uint h = TerrainGenerator.Hash(a, 0, b, seed ^ 81473);
                    int ax = a * 160 + 32 + (int)(h % 96), az = b * 160 + 32 + (int)((h >> 8) % 96);
                    int rx = x - ax, rz = z - az, d = rx * rx + rz * rz;
                    if (d < best) { best = d; kind = (BiomeKind)((h >> 19) % (uint)count); }
                }
            return kind;
        }

        public static TreeColumn Trees(int x, int z, int seed, int ground) => Trees(x, z, seed, ground, 7);

        public static TreeColumn Trees(int x, int z, int seed, int ground, int contentVersion)
        {
            if (Reserved(x, z)) return NatureGenerator.Trees(x, z, seed, ground);
            ulong wood = 0, leaf = 0;
            int cx = Div(x, 8), cz = Div(z, 8);
            int[] densities = { 45, 680, 760, 0, 390, 25, 620, 15, 20, 0, 520, 180 };
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int a = cx + dx, b = cz + dz;
                    uint t = TerrainGenerator.Hash(a, 0, b, seed ^ 62113);
                    int ax = a * 8 + 1 + (int)(t % 6), az = b * 8 + 1 + (int)((t >> 8) % 6);
                    BiomeKind kind = At(ax, az, seed, contentVersion);
                    int density = densities[(int)kind];
                    if ((t >> 16) % 1000 >= (uint)density) continue;
                    int top = TerrainGenerator.Height(ax, az, seed, Dimension.Lestingia);
                    if (top <= 42 || top >= 67 || NatureGenerator.Entrance(ax, top, az, seed)) continue;
                    bool pine = kind == BiomeKind.Taiga || kind == BiomeKind.LavenderHighlands;
                    int tall = pine ? 12 + (int)(t % 6) :
                               kind == BiomeKind.Marsh ? 6 + (int)(t % 3) :
                               kind == BiomeKind.BioluminescentGrove ? 5 + (int)(t % 4) :
                               8 + (int)(t % 5);
                    int rx = x - ax, rz = z - az, dist = rx * rx + rz * rz;
                    for (int y = top + 1; y <= top + tall + 3; y++)
                    {
                        int bit = y - (ground - 16);
                        if ((uint)bit >= 64) continue;
                        int dy = y - top;
                        bool trunk = rx == 0 && rz == 0 && dy <= tall;
                        if (kind == BiomeKind.Marsh && dy <= 2 && math.abs(rx) + math.abs(rz) == 1) trunk = true;
                        if (trunk) wood |= 1ul << bit;
                        bool canopy;
                        if (pine)
                        {
                            int r = math.min(4, (tall - dy) / 3 + 1);
                            canopy = dy >= 4 && dy <= tall + 1 && dist <= r * r && (dy % 3 != 0 || dist <= 1);
                        }
                        else if (kind == BiomeKind.BioluminescentGrove)
                        {
                            canopy = (dy == tall || dy == tall + 1) && dist <= 5 || (dy == tall + 2 && dist <= 2);
                        }
                        else
                        {
                            int r = kind == BiomeKind.Marsh || kind == BiomeKind.CherryGrove ? 4 : 3, v = dy - (tall - 1);
                            canopy = dy >= tall - 3 && dist + v * v <= r * r;
                        }
                        if (canopy) leaf |= 1ul << bit;
                    }
                }
            return new TreeColumn { Base = ground - 16, Wood = wood, Leaves = leaf };
        }

        public static ushort Column(int x, int y, int z, int seed, int h, TreeColumn trees)
        {
            return Column(x, y, z, seed, h, trees, At(x, z, seed, 7));
        }

        public static ushort Column(int x, int y, int z, int seed, int h, TreeColumn trees, BiomeKind kind)
        {
            if (Reserved(x, z)) return NatureGenerator.Column(x, y, z, seed, h, trees);
            if (y < 0 || y >= ChunkData.SizeY) return 0;
            if (y == 0) return 9;
            if (y > h)
            {
                if (y <= 40) return kind == BiomeKind.GlacialTundra && y == 40 ? BlockRegistry.GlacialIceId : (ushort)7;
                ushort treeBlock = y < h + 48 ? trees.At(y) : (ushort)0;
                if (treeBlock == 5)
                {
                    if (kind == BiomeKind.CherryGrove) return BlockRegistry.CherryLeavesId;
                    if (kind == BiomeKind.BioluminescentGrove) return BlockRegistry.GlowShroomId;
                }
                return treeBlock;
            }
            if (NatureGenerator.Cave(x, y, z, seed, h)) return 0;
            if (h > 41 && y >= h - 3)
            {
                switch (kind)
                {
                    case BiomeKind.Dunes:
                        return y >= h - 1 ? (ushort)6 : (ushort)43;
                    case BiomeKind.Taiga:
                        return y == h ? (ushort)14 : (ushort)2;
                    case BiomeKind.Marsh:
                    {
                        uint pool = TerrainGenerator.Hash(Div(x, 7), 0, Div(z, 7), seed ^ 47119);
                        if (pool % 5 == 0 && y >= h - 1) return 7;
                        return y == h ? (ushort)34 : (ushort)33;
                    }
                    case BiomeKind.RockyHighlands:
                        return y == h ? (h >= 62 ? (ushort)14 : (ushort)32) : (ushort)27;
                    case BiomeKind.CherryGrove:
                        return y == h ? (ushort)3 : (ushort)2;
                    case BiomeKind.RedMesa:
                        return (y + Div(x + z, 11)) % 3 == 0 ? (ushort)43 : BlockRegistry.RedMesaId;
                    case BiomeKind.GlacialTundra:
                    {
                        uint ice = TerrainGenerator.Hash(Div(x, 5), 0, Div(z, 5), seed ^ 55313);
                        return ice % 3 == 0 || y < h ? BlockRegistry.GlacialIceId : (ushort)14;
                    }
                    case BiomeKind.VolcanicBasalt:
                    {
                        uint vent = TerrainGenerator.Hash(Div(x, 6), 0, Div(z, 6), seed ^ 99131);
                        if (vent % 9 == 0) return BlockRegistry.MagmaBasaltId;
                        return y == h && vent % 4 == 0 ? (ushort)31 : (ushort)10;
                    }
                    case BiomeKind.BioluminescentGrove:
                        return y == h ? (ushort)34 : (ushort)2;
                    case BiomeKind.LavenderHighlands:
                    {
                        uint spur = TerrainGenerator.Hash(x, 0, z, seed ^ 31337);
                        if (y == h && spur % 97 == 0) return 11;
                        return y == h ? BlockRegistry.LavenderTurfId : (ushort)2;
                    }
                }
            }
            return NatureGenerator.Column(x, y, z, seed, h, trees);
        }

        public static ushort Sample(int x, int y, int z, int seed) => Sample(x, y, z, seed, 7);

        public static ushort Sample(int x, int y, int z, int seed, int contentVersion)
        {
            if (Reserved(x, z)) return NatureGenerator.Sample(x, y, z, seed);
            int h = TerrainGenerator.Height(x, z, seed, Dimension.Lestingia);
            return Column(x, y, z, seed, h, Trees(x, z, seed, h, contentVersion), At(x, z, seed, contentVersion));
        }
    }
}
