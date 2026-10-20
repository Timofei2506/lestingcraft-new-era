using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace LestingCraft.World
{
    public enum Dimension
    {
        Lestingia = 0,
        Moon = 1,
        Mars = 2,
        Venus = 3,
        Mercury = 4,
        Europa = 5,
        Titan = 6,
        Pluto = 7,
        ProximaB = 8,
        Trappist1e = 9,
        Kepler452b = 10,
        AndromedaPrime = 11,
        Somnia = 12,
        Dream = 13
    }

    // Pure integer hash + smooth value noise. Generation version 1 is part of the network protocol.
    public static class TerrainGenerator
    {
        public const int Version = 1;
        public static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)x * 374761393u ^ (uint)y * 668265263u ^ (uint)z * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }
        private static float Unit(int x, int y, int z, int seed) => (Hash(x, y, z, seed) & 65535u) / 65535f;
        public static float Noise(float x, float z, int seed)
        {
            int ix = (int)math.floor(x), iz = (int)math.floor(z);
            float tx = x - ix, tz = z - iz;
            tx = tx * tx * (3 - 2 * tx); tz = tz * tz * (3 - 2 * tz);
            return math.lerp(math.lerp(Unit(ix, 0, iz, seed), Unit(ix + 1, 0, iz, seed), tx),
                math.lerp(Unit(ix, 0, iz + 1, seed), Unit(ix + 1, 0, iz + 1, seed), tx), tz);
        }
        public static int Height(int x, int z, int seed, Dimension dimension)
        {
            if (dimension != Dimension.Lestingia) return PlanetTerrain.Column(x, z, seed, (PlanetBody)dimension).Height;
            float broad = Noise(x / 55f, z / 55f, seed), fine = Noise(x / 13f, z / 13f, seed ^ 731);
            return 30 + (int)(broad * 32 + fine * 9);
        }
        public static bool Island(int x, int z, int seed, Dimension d) => true;

        public static ushort Sample(int x, int y, int z, int seed, Dimension dimension)
        {
            if (y < 0 || y >= ChunkData.SizeY) return BlockRegistry.AirId;
            int height = Height(x, z, seed, dimension);
            return SampleColumn(x, y, z, seed, dimension, height, Island(x, z, seed, dimension));
        }

        public static ushort SampleColumn(int x, int y, int z, int seed, Dimension d, int height, bool island)
        {
            if (d != Dimension.Lestingia)
            {
                var col = PlanetTerrain.Column(x, z, seed, (PlanetBody)d);
                col.Height = height;
                return PlanetTerrain.Block(x, y, z, seed, col, (PlanetBody)d);
            }
            if (!island) return BlockRegistry.AirId;
            if (y < 0) return BlockRegistry.AirId;
            if (y == 0) return BlockRegistry.BedrockId;
            if (y > height)
            {
                if (y <= 40) return BlockRegistry.WaterId;
                if (y <= height + 8 && height > 41 && height < 62)
                    return Tree(x, y, z, seed);
                return BlockRegistry.AirId;
            }
            if (y > 4 && y < height - 4)
            {
                float cave = Noise(x / 9f + y * .12f, z / 9f - y * .09f, seed ^ 910);
                if (cave > .81f) return BlockRegistry.AirId;
            }
            if (y == height) return height <= 41 ? BlockRegistry.SandId : height >= 64 ? BlockRegistry.SnowId : BlockRegistry.GrassId;
            if (y > height - 4) return BlockRegistry.DirtId;
            return Hash(x, y, z, seed) % 143 == 0 ? BlockRegistry.OreId : BlockRegistry.StoneId;
        }

        public static ushort Expanded(ushort original, int x, int y, int z, int seed, Dimension d, int height, int contentVersion)
        {
            if (contentVersion < 2) return original;
            uint region = Hash((int)math.floor(x / 8f), (int)math.floor(y / 6f), (int)math.floor(z / 8f), seed ^ 41011);
            if (d != Dimension.Lestingia) return original;
            if (original == 2) { if (height <= 44 && y >= height - 2) return region % 2 == 0 ? (ushort)33 : (ushort)34; return original; }
            if (original == 6 && region % 4 == 0) return 32;
            if (original != 1 && original != 13) return original;
            uint lode = Hash((int)math.floor(x / 3f), (int)math.floor(y / 3f), (int)math.floor(z / 3f), seed ^ 17711);
            if (original == 13 || lode % 47 == 0)
            {
                int roll = (int)((lode >> 9) % 100); if (roll < 27) return 19; if (roll < 51) return 20; if (roll < 69) return 21;
                if (roll < 76 && y < 24) return 22; if (roll < 83 && y < 34) return 23; if (roll < 92) return 24; return 25;
            }
            if (y < height - 5) { switch (region % 8) { case 0: case 1: return 27; case 2: case 3: return 28; case 4: return 29; case 5: if (y < 28) return 30; break; } }
            return original;
        }

        public static ushort SampleContent(int x, int y, int z, int seed, Dimension d, int contentVersion)
        {
            if (contentVersion >= 4 && d == Dimension.Lestingia &&
                LestingCraft.Story.StoryWorld.TrySample(x, y, z, seed, out ushort placed, contentVersion))
                return placed;
            if (contentVersion >= 3 && d == Dimension.Lestingia)
            {
                ushort raw = contentVersion >= 7 ? BiomeGenerator.Sample(x, y, z, seed, contentVersion) : NatureGenerator.Sample(x, y, z, seed);
                return contentVersion >= 5 ? ExpeditionGenerator.Sample(x, y, z, seed, raw, contentVersion) : raw;
            }
            return Expanded(Sample(x, y, z, seed, d), x, y, z, seed, d, Height(x, z, seed, d), contentVersion);
        }

        private static ushort Tree(int x, int y, int z, int seed)
        {
            int cellX = (int)math.floor(x / 8f), cellZ = (int)math.floor(z / 8f);
            for (int cx = cellX - 1; cx <= cellX + 1; cx++)
                for (int cz = cellZ - 1; cz <= cellZ + 1; cz++)
                {
                    uint hash = Hash(cx, 0, cz, seed ^ 500);
                    if (hash % 4 != 0) continue;
                    int ax = cx * 8 + 2 + (int)(hash % 4), az = cz * 8 + 2 + (int)((hash >> 4) % 4);
                    int h = Height(ax, az, seed, Dimension.Lestingia);
                    if (h <= 41 || h >= 62) continue;
                    int dy = y - h, dx = math.abs(x - ax), dz = math.abs(z - az);
                    if (dx == 0 && dz == 0 && dy >= 1 && dy <= 5) return BlockRegistry.WoodId;
                    if (dy >= 4 && dy <= 7 && dx <= 2 && dz <= 2 && dx + dz < 4) return BlockRegistry.LeavesId;
                }
            return BlockRegistry.AirId;
        }
    }

    [BurstCompile]
    public struct GenerateChunkJob : IJobParallelFor
    {
        public int OriginX, OriginZ, Seed, ContentVersion;
        public Dimension Dimension;
        [NativeDisableParallelForRestriction] public NativeArray<ushort> Blocks;
        public void Execute(int column)
        {
            int x = column % ChunkData.SizeX, z = column / ChunkData.SizeX;
            int wx = OriginX + x, wz = OriginZ + z;
            if (Dimension != Dimension.Lestingia)
            {
                var surface = PlanetTerrain.Column(wx, wz, Seed, (PlanetBody)Dimension);
                for (int y = 0; y < ChunkData.SizeY; y++)
                    Blocks[ChunkData.IndexOf(x, y, z)] = PlanetTerrain.Block(wx, y, wz, Seed, surface, (PlanetBody)Dimension);
                return;
            }
            int h = TerrainGenerator.Height(wx, wz, Seed, Dimension);
            if (ContentVersion >= 3 && Dimension == Dimension.Lestingia)
            {
                var trees = ContentVersion >= 7 ? BiomeGenerator.Trees(wx, wz, Seed, h, ContentVersion) : NatureGenerator.Trees(wx, wz, Seed, h);
                var biome = ContentVersion >= 7 ? BiomeGenerator.At(wx, wz, Seed, ContentVersion) : BiomeKind.MixedForest;
                var extra = ContentVersion >= 5 ? ExpeditionGenerator.At(wx, wz, Seed) : default(ExpeditionGenerator.Column);
                for (int y = 0; y < ChunkData.SizeY; y++)
                    Blocks[ChunkData.IndexOf(x, y, z)] = ContentVersion >= 4 &&
                        LestingCraft.Story.StoryWorld.TrySample(wx, y, wz, Seed, out ushort placed, ContentVersion)
                        ? placed
                        : ContentVersion >= 5
                            ? extra.Apply(ContentVersion >= 7 ? BiomeGenerator.Column(wx, y, wz, Seed, h, trees, biome) : NatureGenerator.Column(wx, y, wz, Seed, h, trees), y, Seed, ContentVersion)
                            : NatureGenerator.Column(wx, y, wz, Seed, h, trees);
                return;
            }
            bool island = TerrainGenerator.Island(wx, wz, Seed, Dimension);
            for (int y = 0; y < ChunkData.SizeY; y++)
            {
                ushort block = TerrainGenerator.SampleColumn(wx, y, wz, Seed, Dimension, h, island);
                Blocks[ChunkData.IndexOf(x, y, z)] = TerrainGenerator.Expanded(block, wx, y, wz, Seed, Dimension, h, ContentVersion);
            }
        }
    }
}
