using System;
using System.Collections.Generic;
using LestingCraft.Gameplay;

// Stage 1.6 harness: zero-gravity model, jet pack and station free fall. Pure C#, runs under mono.
class ZeroGCheck
{
    static int tests;
    static void Check(bool x, string name) { tests++; if (!x) throw new Exception(name); }

    const double EarthGM = 3.986e14, EarthR = 6.371e6, MoonGM = 4.905e12, MoonR = 1.737e6;

    static FreeBody Mk(double mass, double fuel) { return new FreeBody { DryMass = mass, JetFuel = fuel }; }

    static void Main()
    {
        var none = new List<Body>();
        var earth = new List<Body> { new Body("Earth", Vec3d.Zero, EarthGM, EarthR) };

        // 1. No body in range: free drift is a straight line, exactly.
        var drift = Mk(120, 0);
        drift.Velocity = new Vec3d(3, -1, 0.5);
        for (int i = 0; i < 1000; i++) ZeroGFlight.Step(drift, Habitat.OpenSpace, none, Vec3d.Zero, 0, 0.1);
        Check(Math.Abs(drift.Position.X - 300) < 1e-9 && Math.Abs(drift.Position.Y + 100) < 1e-9 && Math.Abs(drift.Position.Z - 50) < 1e-9, "free drift is linear without gravity");
        Check(drift.Velocity.X == 3 && drift.Velocity.Y == -1 && drift.Velocity.Z == 0.5, "free drift keeps velocity");

        // 2. Far from a planet the pull is negligible: at 1 AU from Earth GM/r^2 is ~2e-8 m/s^2.
        double auPull = ZeroGFlight.Gravity(new Vec3d(1.496e11, 0, 0), earth).Length;
        Check(auPull < 1e-6, "pull at 1 AU is negligible, got " + auPull);

        // 3. Station interior: a body at rest near Earth does not fall.
        var floater = Mk(120, 0);
        floater.Position = new Vec3d(EarthR + 4e5, 0, 0);
        for (int i = 0; i < 1000; i++) ZeroGFlight.Step(floater, Habitat.Station, earth, Vec3d.Zero, 0, 0.1);
        Check(Math.Abs(floater.Position.X - (EarthR + 4e5)) < 1e-6 && floater.Velocity.Length < 1e-12, "station interior has no gravity");

        // 4. Same spot in open space does fall (control for check 3).
        var faller = Mk(120, 0);
        faller.Position = new Vec3d(EarthR + 4e5, 0, 0);
        for (int i = 0; i < 100; i++) ZeroGFlight.Step(faller, Habitat.OpenSpace, earth, Vec3d.Zero, 0, 0.1);
        Check(faller.Velocity.X < -1.0, "open space pulls toward Earth, vx " + faller.Velocity.X);

        // 5. Circular orbit at 400 km: after 10 orbits energy drifts by less than 1e-6 relative and radius stays within 100 m.
        double r0 = EarthR + 4e5;
        double v0 = Math.Sqrt(EarthGM / r0);
        var sat = Mk(1000, 0);
        sat.Position = new Vec3d(r0, 0, 0);
        sat.Velocity = new Vec3d(0, v0, 0);
        double e0 = 0.5 * v0 * v0 - EarthGM / r0;
        double period = 2 * Math.PI * r0 / v0;
        double steps = Math.Round(10 * period / 0.5);
        double maxRadiusErr = 0;
        for (int i = 0; i < steps; i++)
        {
            ZeroGFlight.Step(sat, Habitat.OpenSpace, earth, Vec3d.Zero, 0, 0.5);
            double rr = sat.Position.Length;
            if (Math.Abs(rr - r0) > maxRadiusErr) maxRadiusErr = Math.Abs(rr - r0);
        }
        double e1 = 0.5 * sat.Velocity.Length * sat.Velocity.Length - EarthGM / sat.Position.Length;
        double drift1 = Math.Abs(e1 - e0) / Math.Abs(e0);
        Check(drift1 < 1e-6, "orbit energy drift below 1e-6, got " + drift1);
        Check(maxRadiusErr < 100, "orbit radius within 100 m, got " + maxRadiusErr);

        // 6. Moon surface gravity from the body data is 1.62 m/s^2.
        var moon = new List<Body> { new Body("Moon", Vec3d.Zero, MoonGM, MoonR) };
        double gMoon = ZeroGFlight.Gravity(new Vec3d(MoonR, 0, 0), moon).Length;
        Check(Math.Abs(gMoon - 1.62) < 0.01, "lunar surface g 1.62, got " + gMoon);

        // 7. Jet pack: thrust for 1 s from rest in open space. Delta-v matches Tsiolkovsky, fuel burn matches mdot = F/(Isp*g0).
        var pack = Mk(120, ZeroGFlight.JetTankMax);
        double fuel0 = pack.JetFuel;
        double mass0 = pack.Mass;
        for (int i = 0; i < 10; i++) ZeroGFlight.Step(pack, Habitat.OpenSpace, none, new Vec3d(1, 0, 0), 1.0, 0.1);
        double dvSim = pack.Velocity.X;
        double dvIdeal = ZeroGFlight.JetIsp * ZeroGFlight.G0 * Math.Log(mass0 / pack.Mass);
        Check(Math.Abs(dvSim - dvIdeal) / dvIdeal < 0.005, "jet pack delta-v matches Tsiolkovsky, sim " + dvSim + " vs " + dvIdeal);
        double burnExpected = ZeroGFlight.JetThrust / (ZeroGFlight.JetIsp * ZeroGFlight.G0) * 1.0;
        Check(Math.Abs((fuel0 - pack.JetFuel) - burnExpected) < 1e-9, "fuel burn per second matches mdot, got " + (fuel0 - pack.JetFuel));
        Check(Math.Abs(pack.Position.X - 0.5 * dvSim) < 0.05, "position follows constant acceleration");

        // 8. Empty tank: no thrust, fuel never negative, coasting stays constant.
        var dry = Mk(120, 0.05);
        double vBefore = 0;
        for (int i = 0; i < 20; i++) ZeroGFlight.Step(dry, Habitat.OpenSpace, none, new Vec3d(1, 0, 0), 1.0, 0.1);
        vBefore = dry.Velocity.X;
        for (int i = 0; i < 50; i++) ZeroGFlight.Step(dry, Habitat.OpenSpace, none, new Vec3d(1, 0, 0), 1.0, 0.1);
        Check(dry.JetFuel == 0.0, "fuel reaches zero exactly and not below");
        Check(Math.Abs(dry.Velocity.X - vBefore) < 1e-12, "no thrust after the tank is empty");
        double applied = ZeroGFlight.Step(dry, Habitat.OpenSpace, none, new Vec3d(1, 0, 0), 1.0, 0.1);
        Check(applied == 0.0, "empty tank applies no thrust");

        // 9. Station damping: a drifting body slows in cabin air, with no thrust.
        var cabin = Mk(80, 0);
        cabin.Velocity = new Vec3d(2, 0, 0);
        for (int i = 0; i < 100; i++) ZeroGFlight.Step(cabin, Habitat.Station, none, Vec3d.Zero, 0, 0.1);
        double expect = 2 * Math.Exp(-ZeroGFlight.StationDamping * 10.0);
        Check(Math.Abs(cabin.Velocity.X - expect) < 1e-9, "cabin damping exp(-0.3 t), got " + cabin.Velocity.X);

        // 10. Determinism: two identical runs agree bit for bit.
        var a = Mk(120, 6);
        var b = Mk(120, 6);
        a.Position = b.Position = new Vec3d(EarthR + 4e5, 0, 0);
        a.Velocity = b.Velocity = new Vec3d(0, v0 * 0.98, 0.1);
        for (int i = 0; i < 2000; i++)
        {
            ZeroGFlight.Step(a, Habitat.OpenSpace, earth, new Vec3d(0, 0.2, 0), 0.6, 0.25);
            ZeroGFlight.Step(b, Habitat.OpenSpace, earth, new Vec3d(0, 0.2, 0), 0.6, 0.25);
        }
        Check(BitConverter.DoubleToInt64Bits(a.Position.X) == BitConverter.DoubleToInt64Bits(b.Position.X)
            && BitConverter.DoubleToInt64Bits(a.Velocity.Y) == BitConverter.DoubleToInt64Bits(b.Velocity.Y), "bit-exact determinism");

        Console.WriteLine("budget: 1 AU pull " + auPull.ToString("E2") + " m/s^2, orbit energy drift " + drift1.ToString("E2") + ", jet delta-v 1 s " + dvSim.ToString("F3") + " m/s");
        Console.WriteLine("PASS: " + tests + " zero-g checks (drift, station free fall, orbit, jet pack, damping, determinism)");
    }
}
