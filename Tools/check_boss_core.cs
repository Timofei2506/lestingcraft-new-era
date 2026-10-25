using System;
using System.Collections.Generic;
using LestingCraft.Gameplay;
using LestingCraft.World;

/// <summary>
/// Headless guard for the boss catalogue: every world has a guardian, numbers are sane,
/// loot is deterministic and the enrage threshold is correct. No Unity types involved.
/// </summary>
class BossCheck
{
    static int checks;
    static void Ok(bool yes, string label) { checks++; if (!yes) throw new Exception("FAIL: " + label); }

    static void Main()
    {
        var all = BossCatalog.All();
        Ok(all.Length >= 12, "catalogue covers at least 12 worlds: " + all.Length);

        var names = new HashSet<string>();
        var titles = new HashSet<string>();
        foreach (Dimension dimension in Enum.GetValues(typeof(Dimension)))
        {
            Ok(BossCatalog.HasBoss(dimension), "boss exists for " + dimension);
            var profile = BossCatalog.For(dimension);
            Ok(profile.Dimension == dimension, "profile matches dimension " + dimension);
            Ok(!string.IsNullOrEmpty(profile.Name) && profile.Name.Length >= 6, "boss name is descriptive: " + dimension);
            Ok(!string.IsNullOrEmpty(profile.Title), "boss title present: " + dimension);
            Ok(names.Add(profile.Name), "boss names are unique: " + profile.Name);
            Ok(titles.Add(profile.Title), "boss titles are unique: " + profile.Title);
            Ok(profile.MaxHealth >= 250 && profile.MaxHealth <= 600, "health in a playable band: " + dimension);
            Ok(profile.Scale >= 0.95f && profile.Scale <= 1.25f, "scale stays near the rig baseline");
            Ok(profile.Diamonds > 0 && profile.Crystals > 0 && profile.Shards > 0, "loot is never empty: " + dimension);
            Ok(profile.Aggression >= 2 && profile.Aggression <= 8, "attack cadence is playable: " + dimension);
            float armorLum = profile.ArmorR + profile.ArmorG + profile.ArmorB;
            float coreLum = profile.CoreR + profile.CoreG + profile.CoreB;
            Ok(armorLum > 0.3f, "armour is not black: " + dimension);
            Ok(coreLum > armorLum, "weak-point core is brighter than armour: " + dimension);
        }

        // The ordinary world must have a boss: this was the actual gap.
        Ok(BossCatalog.HasBoss(Dimension.Lestingia), "overworld boss exists");
        Ok(BossCatalog.For(Dimension.Lestingia).Archetype == BossArchetype.Warden, "overworld boss is a forest warden");

        // Deterministic health and loot.
        foreach (Dimension dimension in Enum.GetValues(typeof(Dimension)))
        {
            for (int seed = 0; seed < 40; seed++)
            {
                int a = BossCatalog.HealthFor(dimension, seed);
                int b = BossCatalog.HealthFor(dimension, seed);
                Ok(a == b, "health deterministic");
                Ok(a >= 180 && a <= 640, "health bounded: " + a);
                BossCatalog.LootFor(dimension, seed, out int d1, out int c1, out int s1);
                BossCatalog.LootFor(dimension, seed, out int d2, out int c2, out int s2);
                Ok(d1 == d2 && c1 == c2 && s1 == s2, "loot deterministic");
                Ok(d1 > 0 && c1 > 0 && s1 > 0, "loot positive");
            }
        }

        // Enrage at one third health, never at full health.
        foreach (Dimension dimension in Enum.GetValues(typeof(Dimension)))
        {
            int max = BossCatalog.HealthFor(dimension, 777);
            Ok(!BossCatalog.IsEnraged(max, max), "not enraged at full health");
            Ok(BossCatalog.IsEnraged(max / 3, max), "enraged at a third health");
            Ok(!BossCatalog.IsEnraged(max / 2, max), "calm above a third");
            Ok(!BossCatalog.IsEnraged(10, 0), "zero max health is safe");
        }

        // All five archetypes are actually used, so bosses do not look identical.
        var archetypes = new HashSet<BossArchetype>();
        foreach (var profile in all) archetypes.Add(profile.Archetype);
        Ok(archetypes.Count >= 5, "all boss archetypes are used: " + archetypes.Count);

        Console.WriteLine("PASS: " + checks + " boss assertions; " + all.Length + " guardians across " +
            Enum.GetValues(typeof(Dimension)).Length + " dimensions, " + archetypes.Count +
            " archetypes, deterministic loot and enrage. Pure C# core, not a Unity scene run.");
    }
}
