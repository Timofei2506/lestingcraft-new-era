using System;
using System.Globalization;
using System.IO;
using System.Text;
using LestingCraft.Gameplay;
using LestingCraft.World;

/// <summary>
/// Offline exporter: dumps the exact shared sculpt data as JSON so the honest software
/// preview (Tools/preview_boss_sculpts.py) renders the same geometry the game meshes.
/// Usage: mono export_boss_sculpts.exe <outdir>
/// </summary>
class ExportBossSculpts
{
    static string F(float v) { return v.ToString("R", CultureInfo.InvariantCulture); }

    static string Json(SculptData d)
    {
        var sb = new StringBuilder();
        sb.Append("{\"name\":").Append(Q(d.Name));
        sb.Append(",\"unit\":").Append(F(d.Unit));
        sb.Append(",\"palette\":[");
        for (int i = 0; i < d.Palette.Length; i++)
        {
            var p = d.Palette[i];
            if (i > 0) sb.Append(',');
            sb.Append("{\"name\":").Append(Q(p.Name))
              .Append(",\"layer\":").Append(Q(p.Layer))
              .Append(",\"tint\":[").Append(F(p.R)).Append(',').Append(F(p.G)).Append(',').Append(F(p.B)).Append(']')
              .Append(",\"emission\":").Append(F(p.Emission)).Append('}');
        }
        sb.Append("],\"parts\":[");
        for (int i = 0; i < d.Parts.Length; i++)
        {
            var p = d.Parts[i];
            if (i > 0) sb.Append(',');
            sb.Append("{\"name\":").Append(Q(p.Name))
              .Append(",\"pivot\":[").Append(F(p.PX)).Append(',').Append(F(p.PY)).Append(',').Append(F(p.PZ)).Append(']')
              .Append(",\"cells\":[");
            for (int k = 0; k < p.Cells.Length; k++)
            {
                if (k > 0) sb.Append(',');
                sb.Append(p.Cells[k]);
            }
            sb.Append("]}");
        }
        sb.Append("],\"light\":[").Append(F(d.LightX)).Append(',').Append(F(d.LightY)).Append(',').Append(F(d.LightZ))
          .Append(',').Append(F(d.LightR)).Append(',').Append(F(d.LightG)).Append(',').Append(F(d.LightB))
          .Append(',').Append(F(d.LightRange)).Append(',').Append(F(d.LightIntensity)).Append(']');
        sb.Append(",\"bbox\":[").Append(F(d.MinX)).Append(',').Append(F(d.MinY)).Append(',').Append(F(d.MinZ))
          .Append(',').Append(F(d.MaxX)).Append(',').Append(F(d.MaxY)).Append(',').Append(F(d.MaxZ)).Append(']');
        sb.Append('}');
        return sb.ToString();
    }

    static string Q(string s)
    {
        var sb = new StringBuilder("\"");
        foreach (char c in s)
        {
            if (c == '"' || c == '\\') sb.Append('\\').Append(c);
            else if (c < 32) sb.Append("\\u").Append(((int)c).ToString("x4"));
            else sb.Append(c);
        }
        return sb.Append('"').ToString();
    }

    static void Main(string[] args)
    {
        string dir = args.Length > 0 ? args[0] : "BossSculptPreview";
        Directory.CreateDirectory(dir);
        int total = 0;
        foreach (Dimension dim in Enum.GetValues(typeof(Dimension)))
        {
            var d = BossSculpts.For(dim, 2506);
            File.WriteAllText(Path.Combine(dir, "boss-" + dim + ".json"), Json(d));
            total += d.CellCount;
            Console.WriteLine(dim + ": " + d.Parts.Length + " parts, " + d.CellCount + " cells, h=" + F(d.Height) + "m");
        }
        foreach (var extra in new[] { "The Corrupted AI", "The Void Horror" })
        {
            var d = BossSculpts.Extra(extra, 99);
            File.WriteAllText(Path.Combine(dir, "boss-extra-" + extra.Replace(" ", "-") + ".json"), Json(d));
            Console.WriteLine(extra + ": " + d.CellCount + " cells");
        }
        foreach (DroneKind kind in Enum.GetValues(typeof(DroneKind)))
        {
            var d = BossSculpts.Drone(kind);
            File.WriteAllText(Path.Combine(dir, "drone-" + kind + ".json"), Json(d));
        }
        var mob = BossSculpts.BacteriaMob(3);
        File.WriteAllText(Path.Combine(dir, "mob-bacteria.json"), Json(mob));
        Console.WriteLine("exported to " + dir + "; boss cells total " + total);
    }
}
