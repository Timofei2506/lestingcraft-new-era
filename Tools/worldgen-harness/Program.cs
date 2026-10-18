// Харнесс проверок генерации мира Лестингии (версия 2).
// Запуск: dotnet run -c Release
// Проверяет: детерминизм, совпадение джобы и сэмплера, диапазон блоков, биомы, воду, руду, деревья, скорость.
using System;
using System.Diagnostics;
using System.Linq;
using LestingCraft.World;

static class Program
{
    static int failures = 0;

    static void Check(bool ok, string name, string detail = "")
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")}  {name}  {detail}");
        if (!ok) failures++;
    }

    // Карта сверху для визуальной проверки: dotnet run -c Release -- map out.ppm [size]
    static void WriteMap(string path, int size, int seed)
    {
        var rgb = new byte[size * size * 3];
        int half = size / 2;
        for (int x = -half; x < half; x++)
        for (int z = -half; z < half; z++)
        {
            var col = TerrainGenerator.Column(x, z, seed, Dimension.Lestingia);
            byte r = 0, g = 0, b = 0;
            bool found = false;
            for (int y = Math.Min(ChunkData.SizeY - 1, col.Height + 9); y >= 0 && !found; y--)
            {
                ushort id = TerrainGenerator.SampleColumn(x, y, z, seed, Dimension.Lestingia, col);
                if (id == BlockRegistry.AirId) continue;
                found = true;
                var c = id switch
                {
                    BlockRegistry.WaterId => (40, 110, 200),
                    BlockRegistry.SandId => (222, 200, 130),
                    BlockRegistry.GrassId => (96, 160, 70),
                    BlockRegistry.DirtId => (134, 96, 60),
                    BlockRegistry.SnowId => (240, 245, 250),
                    BlockRegistry.StoneId => (128, 128, 132),
                    BlockRegistry.OreId => (150, 110, 140),
                    BlockRegistry.LeavesId => (40, 110, 50),
                    BlockRegistry.WoodId => (110, 75, 40),
                    _ => (255, 0, 255),
                };
                r = (byte)c.Item1; g = (byte)c.Item2; b = (byte)c.Item3;
                if (id != BlockRegistry.WaterId)
                {
                    double shade = 0.7 + 0.3 * Math.Min(1.0, col.Height / 80.0);
                    r = (byte)(r * shade); g = (byte)(g * shade); b = (byte)(b * shade);
                }
            }
            if (!found) (r, g, b) = (0, 0, 0);
            int idx = ((z + half) * size + (x + half)) * 3;
            rgb[idx] = r; rgb[idx + 1] = g; rgb[idx + 2] = b;
        }
        using var fs = System.IO.File.Create(path);
        var header = System.Text.Encoding.ASCII.GetBytes($"P6\n{size} {size}\n255\n");
        fs.Write(header, 0, header.Length);
        fs.Write(rgb, 0, rgb.Length);
        Console.WriteLine($"Map written: {path} ({size}x{size})");
    }

    static void Main(string[] args)
    {
        const int seed = 2506;
        if (args.Length >= 2 && args[0] == "map")
        {
            WriteMap(args[1], args.Length >= 3 ? int.Parse(args[2]) : 512, seed);
            return;
        }
        Console.WriteLine($"Generator version: {TerrainGenerator.Version}, sea level: {TerrainGenerator.SeaLevel}");

        // 1. Детерминизм
        bool det = true;
        for (int x = -40; x <= 40; x += 3)
        for (int z = -40; z <= 40; z += 3)
        for (int y = 0; y < 100; y += 5)
            if (TerrainGenerator.Sample(x, y, z, seed, Dimension.Lestingia) != TerrainGenerator.Sample(x, y, z, seed, Dimension.Lestingia)) det = false;
        Check(det, "deterministic sampling");

        // 2. Джоба совпадает с сэмплером (включая отрицательные координаты чанка)
        bool jobMatch = true;
        foreach (var (ox, oz) in new[] { (-16, -32), (0, 0), (160, -240), (-512, 512) })
        {
            using var blocks = new Unity.Collections.NativeArray<ushort>(ChunkData.BlockCount, Unity.Collections.Allocator.TempJob);
            var job = new GenerateChunkJob { OriginX = ox, OriginZ = oz, Seed = seed, Dimension = Dimension.Lestingia, Blocks = blocks };
            for (int c = 0; c < 256; c++) job.Execute(c);
            for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
            for (int y = 0; y < ChunkData.SizeY; y++)
                if (TerrainGenerator.Sample(x + ox, y, z + oz, seed, Dimension.Lestingia) != blocks[ChunkData.IndexOf(x, y, z)]) jobMatch = false;
        }
        Check(jobMatch, "chunk job matches sampler at 4 chunk origins");

        // 3. Только известные блоки и защита дна во всех измерениях
        bool ids = true, bedrock = true;
        for (int d = 0; d < 5; d++)
        for (int x = -60; x < 60; x += 3)
        for (int z = -60; z < 60; z += 3)
        for (int y = 0; y < 256; y += 3)
            if (TerrainGenerator.Sample(x, y, z, -2147483648, (Dimension)d) > 14) ids = false;
        for (int x = -200; x < 200; x += 7) for (int z = -200; z < 200; z += 7)
            if (TerrainGenerator.Sample(x, 0, z, seed, Dimension.Lestingia) != BlockRegistry.BedrockId) bedrock = false;
        Check(ids, "no block ids above 14 in any dimension");
        Check(bedrock, "Lestingia has bedrock at y=0 everywhere");

        // 4. Биомы, вода, высоты по большой площади (шаг 4, площадь 2000x2000)
        int step = 4, radius = 1000;
        var biomeCount = new int[7];
        int ocean = 0, columns = 0, minH = 999, maxH = -999;
        long heightSum = 0;
        for (int x = -radius; x < radius; x += step)
        for (int z = -radius; z < radius; z += step)
        {
            var col = TerrainGenerator.Column(x, z, seed, Dimension.Lestingia);
            biomeCount[(int)col.Biome]++;
            columns++;
            if (col.Height < TerrainGenerator.SeaLevel) ocean++;
            minH = Math.Min(minH, col.Height); maxH = Math.Max(maxH, col.Height);
            heightSum += col.Height;
        }
        Console.WriteLine("Biome share:");
        foreach (Biome b in Enum.GetValues(typeof(Biome)))
            Console.WriteLine($"   {b,-10} {100.0 * biomeCount[(int)b] / columns,6:F1}%");
        Console.WriteLine($"   height min={minH} max={maxH} mean={(double)heightSum / columns:F1}; below sea level {100.0 * ocean / columns:F1}%");
        Check(biomeCount.All(c => c > 0), "all 7 biomes present");
        Check(biomeCount.Skip(1).All(c => 100.0 * c / columns >= 3.0), "every land biome covers at least 3%");
        Check(maxH <= 120 && minH >= 1, "heights within 1..120");
        Check(100.0 * ocean / columns > 10 && 100.0 * ocean / columns < 60, "ocean/water share between 10% and 60%");

        // 5. Руда по глубине, деревья и скорость в chunk-выборке (16x16 чанков)
        long[] stone = new long[3], ore = new long[3];
        long wood = 0, leaves = 0;
        var sw = Stopwatch.StartNew();
        int chunks = 0;
        for (int cx = -8; cx < 8; cx++)
        for (int cz = -8; cz < 8; cz++)
        {
            using var blocks = new Unity.Collections.NativeArray<ushort>(ChunkData.BlockCount, Unity.Collections.Allocator.TempJob);
            var job = new GenerateChunkJob { OriginX = cx * 16, OriginZ = cz * 16, Seed = seed, Dimension = Dimension.Lestingia, Blocks = blocks };
            for (int c = 0; c < 256; c++) job.Execute(c);
            chunks++;
            for (int x = 0; x < 16; x++)
            for (int z = 0; z < 16; z++)
            for (int y = 0; y < ChunkData.SizeY; y++)
            {
                ushort id = blocks[ChunkData.IndexOf(x, y, z)];
                if (id == BlockRegistry.WoodId) wood++;
                if (id == BlockRegistry.LeavesId) leaves++;
                int band = y < 20 ? 0 : y < 40 ? 1 : 2;
                if (id == BlockRegistry.StoneId) stone[band]++;
                if (id == BlockRegistry.OreId) ore[band]++;
            }
        }
        sw.Stop();
        double msPerChunk = sw.Elapsed.TotalMilliseconds / chunks;
        double r0 = 100.0 * ore[0] / Math.Max(1, stone[0] + ore[0]);
        double r1 = 100.0 * ore[1] / Math.Max(1, stone[1] + ore[1]);
        double r2 = 100.0 * ore[2] / Math.Max(1, stone[2] + ore[2]);
        Console.WriteLine($"Ore share in stone: y<20 {r0:F2}%  y20-39 {r1:F2}%  y>=40 {r2:F2}%");
        Console.WriteLine($"Chunks sampled: {chunks}; wood blocks {wood}, leaves {leaves} ({(double)wood / chunks:F1} wood/chunk)");
        Console.WriteLine($"Chunk generation (plain C#, single thread): {msPerChunk:F2} ms/chunk");
        Check(wood > 0 && leaves > 0, "trees are generated");
        Check(r0 > r2, "ore is more frequent deep underground");
        Check(msPerChunk < 60, "chunk generation under 60 ms in plain C# (Burst is much faster)", $"({msPerChunk:F2} ms)");

        // 6. Версия генератора
        Check(TerrainGenerator.Version == 2, "generator version is 2");

        Console.WriteLine(failures == 0 ? "ALL CHECKS PASSED" : $"{failures} CHECK(S) FAILED");
        Environment.Exit(failures == 0 ? 0 : 1);
    }
}
