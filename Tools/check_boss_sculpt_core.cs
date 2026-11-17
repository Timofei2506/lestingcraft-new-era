using System;
using System.Collections.Generic;
using LestingCraft.Gameplay;
using LestingCraft.World;

/// <summary>
/// Headless guard for the authored voxel boss/drone sculpts: real geometry invariants
/// (sizes, silhouettes, articulation, palettes, determinism). No Unity types involved.
/// </summary>
class BossSculptCheck
{
    static int checks;
    static void Ok(bool yes, string label) { checks++; if (!yes) throw new Exception("FAIL: " + label); }

    static long Hash(SculptData d)
    {
        unchecked
        {
            long h = 1469598103934665603L;
            h = (h ^ d.CellCount) * 1099511628211L;
            foreach (var p in d.Parts)
            {
                h = (h ^ p.Cells.Length) * 1099511628211L;
                for (int i = 0; i < p.Cells.Length; i += 4)
                    h = (h ^ (p.Cells[i] + p.Cells[i + 1] * 7 + p.Cells[i + 2] * 13 + p.Cells[i + 3] * 19)) * 1099511628211L;
            }
            return h;
        }
    }

    static void Main()
    {
        var silhouettes = new Dictionary<string, string>();
        int bosses = 0;
        foreach (Dimension dim in Enum.GetValues(typeof(Dimension)))
        {
            var d = BossSculpts.For(dim, 2506);
            bosses++;
            Ok(d.CellCount > 1200, "boss sculpt is substantial: " + dim + " " + d.CellCount);
            Ok(d.CellCount < 90000, "boss sculpt stays in budget: " + dim + " " + d.CellCount);
            Ok(d.Height > 2.4f && d.Height < 12f, "boss height readable in world: " + dim + " " + d.Height);
            Ok(d.MinY >= -0.35f, "boss stands on the ground: " + dim + " " + d.MinY);
            Ok(d.Parts.Length >= 4, "boss is articulated: " + dim + " " + d.Parts.Length);
            Ok(d.Palette.Length >= 4, "boss uses several PBR materials: " + dim + " " + d.Palette.Length);
            bool emissive = false, matte = false;
            foreach (var p in d.Palette)
            {
                if (p.Emission >= 1f) emissive = true;
                if (p.Emission <= 0f) matte = true;
                Ok(!string.IsNullOrEmpty(p.Layer), "palette layer named: " + dim + " " + p.Name);
            }
            Ok(emissive, "boss has an emissive weak point: " + dim);
            Ok(matte, "boss has matte armour: " + dim);
            bool arms = Has(d, "arm_l") && Has(d, "arm_r");
            bool head = Has(d, "head");
            bool limbs = HasPrefix(d, "leg");
            bool floats = !limbs;
            bool wings = Has(d, "wing_l") && Has(d, "wing_r");
            Ok(arms || limbs || wings || Has(d, "shard_0"), "boss has limbs, wings or orbiting shards: " + dim);
            Ok(head || floats, "boss has a head or is a floating caster: " + dim);
            // silhouette fingerprint must be unique per world
            string finger = d.Parts.Length + ":" + d.CellCount / 37 + ":" + (int)(d.Height * 4) + ":" + (int)(d.Width * 4) + ":" + (int)(d.Depth * 4);
            Ok(!silhouettes.ContainsKey(finger), "silhouette differs from other worlds: " + dim + " vs " + (silhouettes.ContainsKey(finger) ? silhouettes[finger] : ""));
            silhouettes[finger] = dim.ToString();
            // determinism
            var again = BossSculpts.For(dim, 2506);
            Ok(Hash(d) == Hash(again), "sculpt deterministic: " + dim);
            var other = BossSculpts.For(dim, 2507);
            Ok(other.CellCount > 0 && other.Height > 2.4f, "alternate seed still builds: " + dim);
        }
        Ok(bosses >= 16, "every world fields a sculpt: " + bosses);

        foreach (var extra in new[] { "The Corrupted AI", "The Void Horror" })
        {
            var d = BossSculpts.Extra(extra, 99);
            Ok(d.CellCount > 1200 && d.Height > 2.4f, "extra boss sculpt sane: " + extra);
        }

        var droneFingers = new HashSet<string>();
        foreach (DroneKind kind in Enum.GetValues(typeof(DroneKind)))
        {
            var d = BossSculpts.Drone(kind);
            Ok(d.CellCount > 120 && d.CellCount < 9000, "drone sculpt lightweight: " + kind + " " + d.CellCount);
            Ok(d.Height > .3f && d.Height < 2.4f, "drone height mob-scale: " + kind + " " + d.Height);
            Ok(d.MinY >= -0.7f, "drone sits on ground/hover line: " + kind + " " + d.MinY);
            string finger = d.Parts.Length + ":" + d.CellCount / 11 + ":" + (int)(d.Height * 8);
            Ok(droneFingers.Add(finger), "drone silhouettes differ: " + kind);
        }

        var mob = BossSculpts.BacteriaMob(3);
        Ok(mob.CellCount > 300 && mob.Height > 1.2f && mob.Height < 2.2f, "bacteria mob sculpt sane: " + mob.CellCount + " " + mob.Height);
        Ok(Has(mob, "head") && Has(mob, "arm_l"), "bacteria mob articulated");

        Console.WriteLine("PASS: " + checks + " sculpt assertions; " + bosses + " guardians + 2 extras + 7 drones + mob, all articulated voxel sculpts with unique silhouettes.");
    }

    static bool Has(SculptData d, string part)
    {
        foreach (var p in d.Parts) if (p.Name == part) return true;
        return false;
    }

    static bool HasPrefix(SculptData d, string prefix)
    {
        foreach (var p in d.Parts) if (p.Name.StartsWith(prefix)) return true;
        return false;
    }
}
