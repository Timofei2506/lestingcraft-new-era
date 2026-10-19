using System;
using System.IO;
using LestingCraft.World;

// Stage 1.6 harness: Moon generator. Pure C#, runs under mono.
// Usage: mono moon-check.exe [outDir]. When outDir is given, writes raw maps for a visual check (heights.bin, surface.bin, 512x512).
class MoonCheck
{
    static int tests;
    static void Check(bool x, string name) { tests++; if (!x) throw new Exception(name); }

    const int N = 512;

    static void Main(string[] args)
    {
        const int seed = 1234;

        // Determinism: same inputs give the same column, across repeated calls and a different order.
        var rnd = new Random(7);
        for (int i = 0; i < 20000; i++)
        {
            int x = rnd.Next(-5000, 5000), z = rnd.Next(-5000, 5000);
            var a = MoonTerrain.Column(x, z, seed);
            var b = MoonTerrain.Column(x, z, seed);
            Check(a.Height == b.Height && a.Surface == b.Surface, "determinism at " + x + "," + z);
        }
        Check(MoonTerrain.Column(10, 10, seed).Height != MoonTerrain.Column(10, 10, seed + 1).Height
            || MoonTerrain.Column(20, 30, seed).Height != MoonTerrain.Column(20, 30, seed + 1).Height, "seed changes the map");

        // Area statistics over a 512x512 block.
        var heights = new byte[N * N];
        var surf = new byte[N * N];
        int mare = 0, ejecta = 0, regolith = 0, minH = 999, maxH = -1;
        long sumH = 0;
        for (int z = 0; z < N; z++)
        for (int x = 0; x < N; x++)
        {
            var c = MoonTerrain.Column(x, z, seed);
            heights[z * N + x] = (byte)c.Height;
            surf[z * N + x] = (byte)c.Surface;
            Check(c.Height >= MoonTerrain.MinHeight && c.Height <= MoonTerrain.MaxHeight, "height range");
            if (c.Height < minH) minH = c.Height;
            if (c.Height > maxH) maxH = c.Height;
            sumH += c.Height;
            if (c.Surface == MoonSurface.Mare) mare++;
            else if (c.Surface == MoonSurface.Ejecta) ejecta++;
            else regolith++;
        }
        double mareShare = mare / (double)(N * N);
        Check(mareShare >= 0.15 && mareShare <= 0.45, "mare share 15-45 %, got " + (mareShare * 100).ToString("F1"));
        Check(ejecta > 0 && regolith > 0, "all surface types appear");
        Check(maxH - minH >= 25, "relief at least 25 blocks, got " + (maxH - minH));

        // Crater count in the area (cells overlapping 0..512).
        int craters = 0;
        for (int cx = -1; cx <= 11; cx++)
        for (int cz = -1; cz <= 11; cz++)
        {
            var c = MoonTerrain.CraterInCell(cx, cz, seed);
            if (c.Present && c.X >= 0 && c.X < N && c.Z >= 0 && c.Z < N) craters++;
        }
        Check(craters >= 40 && craters <= 150, "crater count 40-150 in 512x512, got " + craters);

        // Crater shape: centre below ground, rim above, far field zero.
        var cr = new MoonTerrain.Crater();
        for (int cx = 0; cx < 10 && !cr.Present; cx++)
        for (int cz = 0; cz < 10 && !cr.Present; cz++)
        {
            var c = MoonTerrain.CraterInCell(cx, cz, seed);
            if (c.Present && c.Radius > 8f) cr = c;
        }
        Check(cr.Present, "found a crater larger than 8 blocks");
        float centre = MoonTerrain.CraterDelta(cr, 0f);
        float rim = MoonTerrain.CraterDelta(cr, cr.Radius * 1.15f);
        float far = MoonTerrain.CraterDelta(cr, cr.Radius * 3.5f);
        Check(centre < 0f && centre <= -cr.Depth + 1e-4f, "crater centre is a bowl of depth " + cr.Depth);
        Check(rim > 0f, "crater rim is raised");
        Check(far == 0f, "far field is untouched");
        float e1 = MoonTerrain.CraterDelta(cr, cr.Radius * (1f - 1e-4f)), e2 = MoonTerrain.CraterDelta(cr, cr.Radius * (1f + 1e-4f));
        Check(Math.Abs(e1 - e2) < 0.01f, "profile continuous at the crater edge");
        float s1 = MoonTerrain.CraterDelta(cr, cr.Radius * 1.3f * (1f - 1e-4f)), s2 = MoonTerrain.CraterDelta(cr, cr.Radius * 1.3f * (1f + 1e-4f));
        Check(Math.Abs(s1 - s2) < 0.01f, "profile continuous at the rim edge");

        // Smoothness: neighbouring columns differ by at most 4 blocks (steep crater walls included).
        int maxStep = 0;
        for (int z = 0; z < N; z++)
        for (int x = 0; x + 1 < N; x++)
        {
            int d = Math.Abs(heights[z * N + x] - heights[z * N + x + 1]);
            if (d > maxStep) maxStep = d;
        }
        Check(maxStep <= 4, "neighbour height step at most 4, got " + maxStep);

        // Maria are flat plains: the 5th-95th percentile spread of mare heights is small. Single craters are allowed to break the plain.
        var mareHeights = new System.Collections.Generic.List<int>();
        for (int i = 0; i < N * N; i++)
            if (surf[i] == (byte)MoonSurface.Mare) mareHeights.Add(heights[i]);
        mareHeights.Sort();
        int p5 = mareHeights[mareHeights.Count * 5 / 100], p95 = mareHeights[mareHeights.Count * 95 / 100];
        Check(p95 - p5 <= 6, "maria are flat plains (5-95 % spread 6 or less), got " + (p95 - p5));

        if (args.Length > 0)
        {
            Directory.CreateDirectory(args[0]);
            File.WriteAllBytes(Path.Combine(args[0], "heights.bin"), heights);
            File.WriteAllBytes(Path.Combine(args[0], "surface.bin"), surf);
        }

        Console.WriteLine("budget: mare " + (mareShare * 100).ToString("F1") + " %, craters " + craters + ", height " + minH + ".." + maxH + ", mean " + (sumH / (double)(N * N)).ToString("F1") + ", max step " + maxStep);
        Console.WriteLine("PASS: " + tests + " moon checks (determinism, range, mare share, craters, crater shape, smoothness)");
    }
}
