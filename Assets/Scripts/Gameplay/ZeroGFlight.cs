using System;
using System.Collections.Generic;

namespace LestingCraft.Gameplay
{
    public struct Vec3d
    {
        public double X, Y, Z;
        public Vec3d(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static readonly Vec3d Zero = new Vec3d(0, 0, 0);
        public static Vec3d operator +(Vec3d a, Vec3d b) { return new Vec3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z); }
        public static Vec3d operator -(Vec3d a, Vec3d b) { return new Vec3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z); }
        public static Vec3d operator *(Vec3d a, double s) { return new Vec3d(a.X * s, a.Y * s, a.Z * s); }
        public static Vec3d operator *(double s, Vec3d a) { return new Vec3d(a.X * s, a.Y * s, a.Z * s); }
        public double Length { get { return Math.Sqrt(X * X + Y * Y + Z * Z); } }
        public Vec3d Normalized { get { double l = Length; return l > 0 ? this * (1.0 / l) : Zero; } }
    }

    /// <summary>A gravitating body. Gravity is GM / r², with r clamped to the radius so the field stays finite.</summary>
    public readonly struct Body
    {
        public readonly string Name;
        public readonly Vec3d Center;
        public readonly double GM;
        public readonly double Radius;
        public Body(string name, Vec3d center, double gm, double radius) { Name = name; Center = center; GM = gm; Radius = radius; }
    }

    /// <summary>Where a body floats. Inside a pressurised station there is no gravity field: the cabin is in free fall.</summary>
    public enum Habitat { OpenSpace, Station }

    /// <summary>A suited player or a loose object in free flight. Mass is dry mass plus jet pack fuel.</summary>
    public sealed class FreeBody
    {
        public Vec3d Position;
        public Vec3d Velocity;
        public double DryMass;
        public double JetFuel;
        public double Mass { get { return DryMass + JetFuel; } }
    }

    /// <summary>
    /// Stage 1.6 zero-gravity model. Pure C#: the harness runs it under mono and the server can mirror it in JS later.
    /// Open space: gravity only from bodies (none far away). Station interior: no gravity, a light cabin-air damping stops drifting.
    /// Suit jet pack: constant thrust per throttle, fuel burn from specific impulse, fuel never goes negative.
    /// </summary>
    public static class ZeroGFlight
    {
        public const double G0 = 9.80665;
        public const double JetThrust = 150.0;
        public const double JetIsp = 70.0;
        public const double JetTankMax = 6.0;
        public const double StationDamping = 0.3;

        public static Vec3d Gravity(Vec3d p, IList<Body> bodies)
        {
            Vec3d a = Vec3d.Zero;
            for (int i = 0; i < bodies.Count; i++)
            {
                Vec3d d = p - bodies[i].Center;
                double r = Math.Max(d.Length, bodies[i].Radius);
                a = a + d * (-bodies[i].GM / (r * r * r));
            }
            return a;
        }

        static void Derivs(Vec3d p, Vec3d v, Habitat habitat, IList<Body> bodies, Vec3d thrustAccel, out Vec3d dp, out Vec3d dv)
        {
            dp = v;
            dv = thrustAccel + (habitat == Habitat.OpenSpace ? Gravity(p, bodies) : Vec3d.Zero);
        }

        /// <summary>
        /// Advances one step with classic RK4. thrustDir is any vector (normalised inside), throttle is 0..1.
        /// Returns the thrust actually applied in newtons this step (fuel may cut it short).
        /// </summary>
        public static double Step(FreeBody b, Habitat habitat, IList<Body> bodies, Vec3d thrustDir, double throttle, double dt)
        {
            double wantN = Math.Max(0.0, Math.Min(1.0, throttle)) * JetThrust;
            double burnKg = Math.Min(wantN / (JetIsp * G0) * dt, Math.Max(0.0, b.JetFuel));
            double thrustN = burnKg / dt * JetIsp * G0;
            Vec3d dir = thrustDir.Normalized;
            if (thrustN <= 0.0 || dir.Length == 0.0) thrustN = 0.0;
            double mass = b.Mass;
            Vec3d thrustAccel = thrustN > 0 ? dir * (thrustN / mass) : Vec3d.Zero;

            Vec3d k1p, k1v, k2p, k2v, k3p, k3v, k4p, k4v;
            Derivs(b.Position, b.Velocity, habitat, bodies, thrustAccel, out k1p, out k1v);
            Derivs(b.Position + k1p * (dt / 2), b.Velocity + k1v * (dt / 2), habitat, bodies, thrustAccel, out k2p, out k2v);
            Derivs(b.Position + k2p * (dt / 2), b.Velocity + k2v * (dt / 2), habitat, bodies, thrustAccel, out k3p, out k3v);
            Derivs(b.Position + k3p * dt, b.Velocity + k3v * dt, habitat, bodies, thrustAccel, out k4p, out k4v);
            b.Position = b.Position + (k1p + k2p * 2 + k3p * 2 + k4p) * (dt / 6);
            b.Velocity = b.Velocity + (k1v + k2v * 2 + k3v * 2 + k4v) * (dt / 6);

            if (habitat == Habitat.Station) b.Velocity = b.Velocity * Math.Exp(-StationDamping * dt);
            b.JetFuel = Math.Max(0.0, b.JetFuel - burnKg);
            return thrustN;
        }
    }
}
