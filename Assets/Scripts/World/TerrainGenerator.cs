using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace LestingCraft.World
{
    public enum Dimension { Lestingia, Ashlands, Skyreach, EchoCrypt, ChaosRift }

    /// <summary>
    /// Биом Лестингии. Значения стабильны: входят в сетевой протокол как байт, не переставлять.
    /// </summary>
    public enum Biome : byte { Ocean = 0, Plains = 1, Forest = 2, Desert = 3, Snow = 4, Swamp = 5, Mountains = 6 }

    /// <summary>Результат одной колонки: вычисляется один раз и переиспользуется для всех y.</summary>
    public struct ColumnInfo
    {
        public int Height;
        public bool Island;
        public Biome Biome;
    }

    /// <summary>
    /// Детерминированная генерация мира. Версия 2 — Лестингия получила океан, биомы,
    /// горы, реки, деревья по биому и руду по глубине. Измерения кроме Лестингии сохраняют
    /// версию-1 формулы. Версия входит в протокол: сервер и клиент должны совпадать.
    /// </summary>
    public static class TerrainGenerator
    {
        public const int Version = 2;

        /// <summary>Уровень воды Лестингии: всё ниже него заполняется водой.</summary>
        public const int SeaLevel = 40;

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

        /// <summary>Фрактальный шум 0..1 (несколько октав value-noise).</summary>
        public static float Fbm(float x, float z, int seed, int octaves)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x * freq, z * freq, unchecked(seed + i * 1013)) * amp;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        /// <summary>Плавный переход 0..1 между a и b.</summary>
        public static float Smooth(float a, float b, float x)
        {
            float t = math.clamp((x - a) / (b - a), 0f, 1f);
            return t * t * (3f - 2f * t);
        }

        /// <summary>Полная информация о колонке (x,z). Единственная точка входа для высоты, острова и биома.</summary>
        public static ColumnInfo Column(int x, int z, int seed, Dimension d)
        {
            if (d == Dimension.Lestingia) return LestingiaColumn(x, z, seed);
            return new ColumnInfo
            {
                Height = LegacyHeight(x, z, seed, d),
                Island = LegacyIsland(x, z, seed, d),
                Biome = Biome.Plains,
            };
        }

        public static int Height(int x, int z, int seed, Dimension dimension) => Column(x, z, seed, dimension).Height;

        public static bool Island(int x, int z, int seed, Dimension d) => Column(x, z, seed, d).Island;

        public static ushort Sample(int x, int y, int z, int seed, Dimension dimension)
        {
            if (y < 0 || y >= ChunkData.SizeY) return BlockRegistry.AirId;
            return SampleColumn(x, y, z, seed, dimension, Column(x, z, seed, dimension));
        }

        private static ColumnInfo LestingiaColumn(int x, int z, int seed)
        {
            // Континенты: ниже ~0.44 — океан, выше ~0.47 — суша, между ними — побережье.
            float continent = Fbm(x / 300f, z / 300f, seed ^ 11, 3);
            float land = Smooth(0.40f, 0.47f, continent);
            float hills = Fbm(x / 44f, z / 44f, seed ^ 59, 3);

            // Горные хребты: ridged-шум, поднимается только внутри материка.
            float ridge = 1f - math.abs(2f * Fbm(x / 150f, z / 150f, seed ^ 41, 3) - 1f);
            float mountain = Smooth(0.60f, 0.72f, continent) * ridge * ridge;

            float landHeight = 38f + hills * 14f + mountain * 40f;
            float oceanHeight = 18f + hills * 9f;
            float h = math.lerp(oceanHeight, landHeight, land);

            // Реки: тонкие извилистые контуры шума, врезаются в рельеф до уровня воды.
            float riverNoise = Fbm(x / 230f, z / 230f, seed ^ 71, 2);
            float river = (1f - Smooth(0.006f, 0.016f, math.abs(riverNoise - 0.5f))) * land;
            h = math.lerp(h, SeaLevel - 5f, river);

            float temperature = Fbm(x / 190f, z / 190f, seed ^ 23, 2);
            float humidity = Fbm(x / 170f, z / 170f, seed ^ 37, 2);
            int height = (int)math.clamp(math.floor(h), 1f, 120f);

            Biome biome;
            if (continent < 0.44f) biome = Biome.Ocean;
            else if (mountain > 0.35f) biome = Biome.Mountains;
            else if (temperature < 0.36f) biome = Biome.Snow;
            else if (temperature > 0.60f && humidity < 0.42f) biome = Biome.Desert;
            else if (humidity > 0.62f && height < SeaLevel + 3) biome = Biome.Swamp;
            else if (humidity > 0.55f) biome = Biome.Forest;
            else biome = Biome.Plains;

            return new ColumnInfo { Height = height, Island = true, Biome = biome };
        }

        // ---- Версия 1: измерения кроме Лестингии (поведение не меняется) ----

        private static int LegacyHeight(int x, int z, int seed, Dimension dimension)
        {
            float broad = Noise(x / 55f, z / 55f, seed), fine = Noise(x / 13f, z / 13f, seed ^ 731);
            switch (dimension)
            {
                case Dimension.Ashlands: return 26 + (int)(broad * 38 + fine * 6);
                case Dimension.Skyreach: return 65 + (int)(broad * 30 + fine * 7);
                case Dimension.EchoCrypt: return 34 + (int)(broad * 22 + fine * 8);
                case Dimension.ChaosRift: return 45 + (int)(broad * 42 + fine * 15);
                default: return 30 + (int)(broad * 32 + fine * 9);
            }
        }

        private static bool LegacyIsland(int x, int z, int seed, Dimension d)
        {
            if (d != Dimension.Skyreach && d != Dimension.ChaosRift) return true;
            // Guaranteed starting island, then separated islands with deterministic borders.
            if (x * x + z * z < 144) return true;
            return Noise(x / 22f, z / 22f, seed ^ 8181) > (d == Dimension.ChaosRift ? .48f : .52f);
        }

        /// <summary>Блок в позиции (x,y,z) при известной колонке. Используется и джобой, и Sample.</summary>
        public static ushort SampleColumn(int x, int y, int z, int seed, Dimension d, ColumnInfo column)
        {
            if (!column.Island) return BlockRegistry.AirId;
            if (d == Dimension.Lestingia) return LestingiaSample(x, y, z, seed, column);
            return LegacySample(x, y, z, seed, d, column.Height);
        }

        private static ushort LegacySample(int x, int y, int z, int seed, Dimension d, int height)
        {
            bool floating = d == Dimension.Skyreach || d == Dimension.ChaosRift;
            int floor = floating ? height - 8 - (int)(Noise(x / 9f, z / 9f, seed ^ 97) * 10) : 0;
            if (y < floor) return BlockRegistry.AirId;
            if (!floating && y == 0) return BlockRegistry.BedrockId;
            if (y > height)
            {
                if (d == Dimension.Ashlands && y <= 38) return BlockRegistry.LavaId;
                if (d == Dimension.EchoCrypt && y <= height + 3 && Hash(x, 0, z, seed) % 53 == 0)
                    return BlockRegistry.CrystalId;
                return BlockRegistry.AirId;
            }
            if (y > floor + 4 && y < height - 4)
            {
                float cave = Noise(x / 9f + y * .12f, z / 9f - y * .09f, seed ^ 910);
                if (cave > .81f) return BlockRegistry.AirId;
            }
            if (d == Dimension.Ashlands) return BlockRegistry.BasaltId;
            if (d == Dimension.EchoCrypt) return Hash(x, y, z, seed) % 121 == 0 ? BlockRegistry.CrystalId : BlockRegistry.BasaltId;
            if (d == Dimension.ChaosRift) return BlockRegistry.CrystalId;
            if (d == Dimension.Skyreach) return y == height ? BlockRegistry.AetherId : BlockRegistry.StoneId;
            return BlockRegistry.StoneId;
        }

        // ---- Версия 2: Лестингия ----

        private static ushort LestingiaSample(int x, int y, int z, int seed, ColumnInfo column)
        {
            int height = column.Height;
            if (y == 0) return BlockRegistry.BedrockId;

            if (y > height)
            {
                if (y <= SeaLevel) return BlockRegistry.WaterId;
                if (y <= height + 8)
                {
                    ushort tree = LestingiaTree(x, y, z, seed);
                    if (tree != BlockRegistry.AirId) return tree;
                }
                return BlockRegistry.AirId;
            }

            if (y > 4 && y < height - 4)
            {
                float cave = Noise(x / 9f + y * .12f, z / 9f - y * .09f, seed ^ 910);
                if (cave > .81f) return BlockRegistry.AirId;
            }

            if (y == height) return TopBlock(column.Biome, height);
            if (y > height - 4) return SubBlock(column.Biome);
            return Hash(x, y, z, seed) % OreChance(y) == 0 ? BlockRegistry.OreId : BlockRegistry.StoneId;
        }

        private static ushort TopBlock(Biome biome, int height)
        {
            switch (biome)
            {
                case Biome.Ocean:
                case Biome.Desert: return BlockRegistry.SandId;
                case Biome.Snow: return BlockRegistry.SnowId;
                case Biome.Mountains: return height >= 72 ? BlockRegistry.SnowId : BlockRegistry.StoneId;
                case Biome.Swamp: return height <= SeaLevel ? BlockRegistry.DirtId : BlockRegistry.GrassId;
                default: return height <= SeaLevel + 1 ? BlockRegistry.SandId : BlockRegistry.GrassId;
            }
        }

        private static ushort SubBlock(Biome biome)
        {
            switch (biome)
            {
                case Biome.Ocean:
                case Biome.Desert: return BlockRegistry.SandId;
                case Biome.Mountains: return BlockRegistry.StoneId;
                default: return BlockRegistry.DirtId;
            }
        }

        /// <summary>Руда глубже встречается чаще: y&lt;20 — 1/60, y&lt;40 — 1/110, выше — 1/170.</summary>
        private static uint OreChance(int y)
        {
            if (y < 20) return 60u;
            if (y < 40) return 110u;
            return 170u;
        }

        private static int TreeChance(Biome biome)
        {
            switch (biome)
            {
                case Biome.Forest: return 70;
                case Biome.Snow: return 35;
                case Biome.Swamp: return 30;
                case Biome.Plains: return 8;
                default: return 0;
            }
        }

        /// <summary>Деревья на сетке 7×7: якорь в каждой ячейке, шанс зависит от биома якоря.</summary>
        private static ushort LestingiaTree(int x, int y, int z, int seed)
        {
            int cellX = (int)math.floor(x / 7f), cellZ = (int)math.floor(z / 7f);
            for (int cx = cellX - 1; cx <= cellX + 1; cx++)
            for (int cz = cellZ - 1; cz <= cellZ + 1; cz++)
            {
                uint hash = Hash(cx, 0, cz, seed ^ 777);
                int ax = cx * 7 + 1 + (int)(hash % 5), az = cz * 7 + 1 + (int)((hash >> 5) % 5);
                ColumnInfo anchor = Column(ax, az, seed, Dimension.Lestingia);
                if ((hash >> 10) % 100 >= (uint)TreeChance(anchor.Biome)) continue;
                if (anchor.Height < SeaLevel + 1 || anchor.Height > 66) continue;

                int dy = y - anchor.Height, dx = math.abs(x - ax), dz = math.abs(z - az);
                if (anchor.Biome == Biome.Snow)
                {
                    // Ель: узкая крона, ствол до 8 блоков.
                    if (dx == 0 && dz == 0 && dy >= 1 && dy <= 8) return BlockRegistry.WoodId;
                    if (dy >= 3 && dy <= 4 && dx <= 2 && dz <= 2 && dx + dz <= 3) return BlockRegistry.LeavesId;
                    if (dy >= 5 && dy <= 6 && dx <= 1 && dz <= 1 && dx + dz <= 1) return BlockRegistry.LeavesId;
                    if (dy == 7 && dx == 0 && dz == 0) return BlockRegistry.LeavesId;
                }
                else
                {
                    // Дуб: как в версии 1.
                    if (dx == 0 && dz == 0 && dy >= 1 && dy <= 5) return BlockRegistry.WoodId;
                    if (dy >= 4 && dy <= 7 && dx <= 2 && dz <= 2 && dx + dz < 4) return BlockRegistry.LeavesId;
                }
            }
            return BlockRegistry.AirId;
        }
    }

    [BurstCompile]
    public struct GenerateChunkJob : IJobParallelFor
    {
        public int OriginX, OriginZ, Seed;
        public Dimension Dimension;
        [NativeDisableParallelForRestriction] public NativeArray<ushort> Blocks;
        public void Execute(int column)
        {
            int x = column % ChunkData.SizeX, z = column / ChunkData.SizeX;
            int wx = OriginX + x, wz = OriginZ + z;
            ColumnInfo info = TerrainGenerator.Column(wx, wz, Seed, Dimension);
            for (int y = 0; y < ChunkData.SizeY; y++)
                Blocks[ChunkData.IndexOf(x, y, z)] = TerrainGenerator.SampleColumn(wx, y, wz, Seed, Dimension, info);
        }
    }
}
