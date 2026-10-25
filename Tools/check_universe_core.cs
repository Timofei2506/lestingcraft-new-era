using System;
using System.Collections.Generic;
using LestingCraft.Gameplay;

/// <summary>
/// Headless proof that the endless universe is deterministic, varied and safe to navigate.
/// Compiles with mcs against ProceduralUniverse.cs + SpaceNavigation.cs (no Unity types).
/// </summary>
class UniverseCheck
{
    static int checks;
    static void Ok(bool yes, string label) { checks++; if (!yes) throw new Exception("FAIL: " + label); }

    static void Main()
    {
        // 1. The same key always rebuilds the same system, name and star.
        var a = ProceduralUniverse.System(37, -12, 8);
        var b = ProceduralUniverse.System(37, -12, 8);
        Ok(a.Name == b.Name && a.Star == b.Star && a.PlanetCount == b.PlanetCount && a.AnchorKm.X == b.AnchorKm.X,
            "system determinism");

        // 2. Different cells differ.
        var c = ProceduralUniverse.System(38, -12, 8);
        Ok(a.Name != c.Name || a.Star != c.Star, "cells differ");

        // 3. Anchor matches the navigation lattice (so rendering and flight agree).
        var nav = SpaceMap.Star(37, -12, 8, 2506);
        Ok(SpaceVector.Distance(nav, a.AnchorKm) == 0, "anchor matches SpaceMap.Star");

        // 4. Wide variety of stars, worlds, weathers and structures across a region.
        var kinds = new HashSet<StarKind>();
        var worlds = new HashSet<WorldClass>();
        var weathers = new HashSet<UniverseWeather>();
        var structures = new HashSet<StructureKind>();
        int planets = 0, withAtmosphere = 0, withLife = 0, withRings = 0, gasGiants = 0, blackHoles = 0, nebulae = 0, binaries = 0;

        for (long x = -6; x <= 6; x++)
            for (long y = -6; y <= 6; y += 2)
                for (long z = -4; z <= 4; z += 2)
                {
                    var sys = ProceduralUniverse.System(x, y, z);
                    kinds.Add(sys.Star);
                    if (sys.Star == StarKind.BlackHole) blackHoles++;
                    if (sys.HasNebula) nebulae++;
                    if (sys.Binary) binaries++;
                    Ok(!string.IsNullOrEmpty(sys.Name) && sys.Name.IndexOf('-') > 0, "star name looks like a catalogue id");
                    Ok(sys.StarRadiusKm > 0 && sys.TemperatureK >= 0, "physical star numbers");

                    var list = ProceduralUniverse.Planets(sys);
                    Ok(list.Length == sys.PlanetCount, "planet count matches");
                    double lastOrbit = 0;
                    foreach (var p in list)
                    {
                        planets++;
                        worlds.Add(p.Class);
                        weathers.Add(p.Weather);
                        Ok(!string.IsNullOrEmpty(p.Name), "planet has a name");
                        Ok(p.RadiusKm > 100 && p.RadiusKm < 120000, "planet radius in range");
                        Ok(p.OrbitAu > 0 && p.OrbitAu < 64, "orbit in range");
                        Ok(p.OrbitAu > lastOrbit, "orbits increase outward");
                        lastOrbit = p.OrbitAu;
                        Ok(!double.IsNaN(p.TemperatureC) && p.TemperatureC >= -273 && p.TemperatureC <= 1200, "temperature finite");
                        Ok(p.GravityG > 0 && p.GravityG < 5, "gravity in range");
                        Ok(p.WeatherIntensity >= 0 && p.WeatherIntensity <= 1, "weather intensity 0..1");
                        Ok(p.AtmosphereDensity >= 0 && p.AtmosphereDensity <= 1, "atmosphere density 0..1");
                        Ok(p.Hazard >= 0 && p.Hazard <= 5, "hazard 0..5");
                        Ok(!string.IsNullOrEmpty(ProceduralUniverse.Describe(p)), "describe is non-empty");
                        Ok(p.OrbitPhaseRad >= 0 && p.OrbitPhaseRad < Math.PI * 2.01, "orbit phase");
                        if (p.AtmosphereDensity > 0.02f) withAtmosphere++;
                        if (p.HasLife) withLife++;
                        if (p.HasRings) { withRings++; Ok(p.RingOuterScale > p.RingInnerScale && p.RingInnerScale >= 1, "ring scales ordered"); }
                        if (p.Class == WorldClass.GasGiant) gasGiants++;
                        if (p.Weather == UniverseWeather.Clear ? p.WeatherIntensity == 0 : true) { }
                        if (p.HasLife) Ok(p.TemperatureC > -60 && p.TemperatureC < 90, "life only inside a survivable band");
                    }

                    var points = ProceduralUniverse.Structures(sys);
                    Ok(points.Length >= 1 && points.Length <= 5, "structures per system 1..5");
                    foreach (var s in points)
                    {
                        structures.Add(s.Kind);
                        Ok(!string.IsNullOrEmpty(s.Name), "structure has a name");
                        Ok(s.SizeKm > 0, "structure has size");
                        Ok(!double.IsNaN(s.OffsetKm.X) && !double.IsNaN(s.OffsetKm.Y) && !double.IsNaN(s.OffsetKm.Z), "structure position finite");
                    }
                }

        Ok(kinds.Count >= 8, "at least 8 star classes generated: " + kinds.Count);
        Ok(worlds.Count >= 8, "at least 8 world classes generated: " + worlds.Count);
        Ok(weathers.Count >= 7, "at least 7 weather systems generated: " + weathers.Count);
        Ok(structures.Count >= 8, "at least 8 structure kinds generated: " + structures.Count);
        Ok(blackHoles >= 1, "black holes exist in the lattice");
        Ok(nebulae >= 1, "nebulae exist in the lattice");
        Ok(binaries >= 1, "binary stars exist");
        Ok(withAtmosphere > 0 && withAtmosphere < planets, "some worlds have air, some do not");
        Ok(withLife > 0 && withLife < planets / 2, "life is rare but present");
        Ok(withRings > 0, "ringed worlds exist");
        Ok(gasGiants > 0, "gas giants exist");

        // 5. Nearest() is sorted, bounded and consistent with the lattice.
        var origin = new SpaceVector(149597870.7, 0, 0);
        var near = ProceduralUniverse.Nearest(origin, 30, 6);
        Ok(near.Length == 6, "nearest returns requested count: " + near.Length);
        double previous = -1;
        foreach (var sys in near)
        {
            double d = SpaceVector.Distance(sys.AnchorKm, origin);
            Ok(d <= 30 * ProceduralUniverse.LightYearKm + 1, "nearest stays inside radius");
            Ok(d >= previous, "nearest is sorted ascending");
            previous = d;
        }
        var far = ProceduralUniverse.Nearest(origin, 30, 6);
        for (int i = 0; i < near.Length; i++) Ok(near[i].Name == far[i].Name, "nearest is deterministic");

        // 6. Far-away queries stay finite and cheap (no NaNs at galactic scale).
        var distant = new SpaceVector(4.1e19, -2.7e19, 1.3e19);
        var remote = ProceduralUniverse.SystemAt(distant);
        Ok(!string.IsNullOrEmpty(remote.Name), "remote system has a name");
        Ok(!double.IsNaN(remote.AnchorKm.X) && !double.IsInfinity(remote.AnchorKm.X), "remote anchor finite");
        var remoteNear = ProceduralUniverse.Nearest(distant, 12, 3);
        Ok(remoteNear.Length == 3, "remote neighbourhood generated");
        Ok(ProceduralUniverse.KeyAt(distant).X != 0, "distant key is not the origin cell");

        // 7. Galaxies: fixed catalogue, unit directions, named.
        var galaxies = ProceduralUniverse.Galaxies();
        Ok(galaxies.Length >= 6, "galaxy catalogue size");
        foreach (var g in galaxies)
        {
            double len = Math.Sqrt(g.X * g.X + g.Y * g.Y + g.Z * g.Z);
            Ok(Math.Abs(len - 1) < 0.02, "galaxy direction is normalised: " + g.Name + " " + len);
            Ok(!string.IsNullOrEmpty(g.Name) && !string.IsNullOrEmpty(g.Type), "galaxy is named");
            Ok(g.Size > 0 && g.DistanceLy >= 0, "galaxy size and distance");
        }

        // 8. Russian text used by the cockpit is complete for every enum value.
        foreach (StarKind k in Enum.GetValues(typeof(StarKind))) Ok(!string.IsNullOrEmpty(ProceduralUniverse.StarKindName(k)), "star name ru: " + k);
        foreach (WorldClass w in Enum.GetValues(typeof(WorldClass))) Ok(!string.IsNullOrEmpty(ProceduralUniverse.WorldClassName(w)), "world name ru: " + w);
        foreach (UniverseWeather w in Enum.GetValues(typeof(UniverseWeather))) Ok(!string.IsNullOrEmpty(ProceduralUniverse.WeatherName(w)), "weather name ru: " + w);

        Console.WriteLine("PASS: " + checks + " universe assertions; " + planets + " worlds sampled, " + kinds.Count +
            " star classes, " + worlds.Count + " world classes, " + weathers.Count + " weather systems, " +
            structures.Count + " structure kinds, " + blackHoles + " black holes, " + nebulae + " nebulae. Pure C# core, not a Unity scene run.");
    }
}
