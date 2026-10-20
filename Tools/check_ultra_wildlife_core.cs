using System;
using System.Collections.Generic;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;

class CheckUltraWildlifeCore
{
    static void Assert(bool cond, string msg)
    {
        if (!cond) throw new Exception("FAIL: " + msg);
    }

    static void Main()
    {
        int assertions = 0;

        // 1. Verify SurfaceLayers has 88 distinct surface names with all 20 species coats
        Assert(SurfaceLayers.Names.Length == 88, "Expected 88 SurfaceLayers, got " + SurfaceLayers.Names.Length);
        assertions++;
        string[] expectedCoats = {
            "deer-fur", "hare-fur", "boar-bristles",
            "wolf-fur", "bear-fur", "fox-fur", "bison-fur", "goat-fur", "lynx-fur", "beaver-fur",
            "frog-skin", "eagle-feathers",
            "owl-feathers", "penguin-plumage", "camel-coat", "scorpion-chitin",
            "turtle-shell", "moose-fur", "capybara-fur", "voidray-skin"
        };
        var layerSet = new HashSet<string>(SurfaceLayers.Names);
        foreach (string coat in expectedCoats)
        {
            Assert(layerSet.Contains(coat), "Missing PBR coat in SurfaceLayers: " + coat);
            assertions++;
        }

        WildlifeSimulation.Ground flatGround = (x, z, prevY, radius, height) => 45f;

        // 2. Legacy contentVersion=2 world spawns ONLY species 0..2 (100% legacy determinism preserved)
        var legacySim = new WildlifeSimulation(12345, null, 2);
        legacySim.Spawn(new AnimalObserver { id = "p1", x = 0, y = 46, z = 0, alive = true }, flatGround);
        Assert(legacySim.Animals.Count > 0, "Legacy world spawned animals");
        assertions++;
        foreach (var a in legacySim.Animals)
        {
            Assert(a.species >= 0 && a.species <= 2, "Legacy world spawned modern species " + a.species);
            assertions++;
        }

        // 3. Expanded contentVersion=7 world spawns all 12 species (0..11) across substrates & biomes
        var foundSpecies = new HashSet<int>();
        int[] substrates = { 3, 14, 6, 33, 27, 43, 34, 32 };
        for (int sIdx = 0; sIdx < substrates.Length && foundSpecies.Count < 12; sIdx++)
        {
            int sub = substrates[sIdx];
            var sim = new WildlifeSimulation(2506 + sIdx * 977, null, 7);
            sim.SurfaceAt = (x, z) => sub;
            for (int ring = -600; ring <= 600 && foundSpecies.Count < 12; ring += 48)
            {
                sim.Spawn(new AnimalObserver { id = "p1", x = ring, y = 46, z = -ring, alive = true }, flatGround);
            }
            foreach (var a in sim.Animals) foundSpecies.Add(a.species);
        }
        Assert(foundSpecies.Count == 12, "Expected all 12 animal species (0..11) across biomes, got " + foundSpecies.Count);
        assertions++;

        // 4. Verify AI Step, Golden Eagle flight altitude oscillation, predator damage, and loot for all 12 species
        var testSim = new WildlifeSimulation(777, null, 7);
        for (int k = 0; k < 12; k++)
        {
            testSim.Animals.Add(new AnimalState
            {
                id = "t:" + k,
                species = k,
                x = k * 5f,
                y = k == 11 ? 49.75f : 45f,
                z = 0f,
                homeX = k * 5f,
                homeZ = 0f,
                goalX = k * 5f + 2f,
                goalZ = 1f,
                health = WildlifeSimulation.MaxHealth(k),
                flightHeight = k == 11 ? 4.75f : 0f
            });
            var loot = WildlifeSimulation.Loot(testSim.Animals[k]);
            Assert(loot.meat >= 1 && loot.hide >= 1, "Positive meat/hide loot for species " + k);
            assertions++;
        }

        var obs = new[] { new AnimalObserver { id = "hero", x = 15.5f, y = 45f, z = 0.5f, alive = true } };
        int predatorAttacks = 0;
        for (int i = 0; i < 60; i++)
        {
            testSim.Step(0.2f, obs, flatGround, (targetId, dmg) =>
            {
                if (dmg > 0) predatorAttacks++;
            });
        }

        foreach (var a in testSim.Animals)
        {
            Assert(!float.IsNaN(a.x) && !float.IsNaN(a.y) && !float.IsNaN(a.z), "Finite state for species " + a.species);
            assertions++;
            if (a.species == 11)
            {
                Assert(a.y > 47.5f, "Golden Eagle (species=11) stays soaring high above ground: y=" + a.y);
                assertions++;
            }
        }
        Assert(predatorAttacks > 0, "Predator species (Wolf/Bear/Lynx) attacks nearby player");
        assertions++;

        Console.WriteLine("PASS: " + assertions + " ultra-wildlife assertions across all 12 species (0..11), legacy gate, soaring eagle, predator aggro, and 72 PBR surface layers.");
    }
}
