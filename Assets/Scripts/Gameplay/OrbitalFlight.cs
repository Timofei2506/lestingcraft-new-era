using System;
namespace LestingCraft.Gameplay
{
    /// <summary>
    /// Stage-1 space flight physics (step 1.2). Pure C#, no Unity, fixed-step RK4 so the server (JS) can reproduce it.
    /// 2D model in the Earth-centred orbital plane, SI units (m, s, kg, N). Earth rotation is ignored.
    /// Gravity is GM/r^2 (not a constant g). Drag: 0.5*rho*v^2*Cd*A with exponential atmosphere.
    /// </summary>
    public static class OrbitalFlight
    {
        public const double G0 = 9.80665;
        public const double GMEarth = 3.986004418e14, REarth = 6.371e6;
        public const double GMMoon = 4.9048695e12, RMoon = 1.7374e6, MoonDistance = 3.844e8;
        public const double Rho0 = 1.225, ScaleHeight = 8500.0, DragCoefficient = 0.3;
        public const double Karman = 100e3;

        /// <summary>Position (m) and velocity (m/s) relative to Earth's centre, plus mass (kg).</summary>
        public struct State
        {
            public double X, Y, Vx, Vy, M;
            public State(double x, double y, double vx, double vy, double m) { X = x; Y = y; Vx = vx; Vy = vy; M = m; }
        }

        /// <summary>Constant prograde thrust. Isp is in seconds (vacuum or effective). Inactive = coasting.</summary>
        public struct Burn
        {
            public double Thrust, Isp;
            public bool Active;
            public static Burn Off { get { return new Burn(); } }
            public Burn(double thrust, double isp) { Thrust = thrust; Isp = isp; Active = thrust > 0 && isp > 0; }
        }

        struct Deriv { public double Dx, Dy, Dvx, Dvy, Dm; }

        public static double Density(double altitude)
        {
            if (altitude < 0) altitude = 0;
            return Rho0 * Math.Exp(-altitude / ScaleHeight);
        }

        public static double CircularSpeed(double gm, double radius) { return Math.Sqrt(gm / radius); }

        /// <summary>Ideal rocket equation: dv = Isp * g0 * ln(m0/m1).</summary>
        public static double Tsiolkovsky(double isp, double m0, double m1) { return isp * G0 * Math.Log(m0 / m1); }

        /// <summary>Departure dv (m/s) for a Hohmann transfer from circular orbit r1 to apogee radius r2 (both about Earth).</summary>
        public static double HohmannDeparture(double r1, double r2)
        {
            double a = (r1 + r2) / 2.0;
            double vPeri = Math.Sqrt(GMEarth * (2.0 / r1 - 1.0 / a));
            return vPeri - CircularSpeed(GMEarth, r1);
        }

        /// <summary>Half-period of the Hohmann ellipse (seconds): time from perigee to apogee.</summary>
        public static double HohmannTime(double r1, double r2)
        {
            double a = (r1 + r2) / 2.0;
            return Math.PI * Math.Sqrt(a * a * a / GMEarth);
        }

        /// <summary>Specific orbital energy (J/kg) about Earth.</summary>
        public static double Energy(State s)
        {
            double r = Math.Sqrt(s.X * s.X + s.Y * s.Y), v2 = s.Vx * s.Vx + s.Vy * s.Vy;
            return 0.5 * v2 - GMEarth / r;
        }

        static void Accel(State s, Burn b, double dragArea, out double ax, out double ay)
        {
            double r2 = s.X * s.X + s.Y * s.Y, r = Math.Sqrt(r2);
            double g = -GMEarth / (r2 * r);
            ax = g * s.X; ay = g * s.Y;
            double v = Math.Sqrt(s.Vx * s.Vx + s.Vy * s.Vy);
            if (v > 0 && dragArea > 0 && s.M > 0)
            {
                double drag = 0.5 * Density(r - REarth) * v * v * DragCoefficient * dragArea / s.M;
                ax -= drag * s.Vx / v; ay -= drag * s.Vy / v;
            }
            if (b.Active && s.M > 0 && v > 0)
            {
                double a = b.Thrust / s.M;
                ax += a * s.Vx / v; ay += a * s.Vy / v;
            }
        }

        static Deriv F(State s, Burn b, double dragArea)
        {
            Deriv d;
            d.Dx = s.Vx; d.Dy = s.Vy;
            Accel(s, b, dragArea, out d.Dvx, out d.Dvy);
            d.Dm = b.Active ? -b.Thrust / (b.Isp * G0) : 0;
            return d;
        }

        static State Add(State s, Deriv d, double h)
        {
            return new State(s.X + h * d.Dx, s.Y + h * d.Dy, s.Vx + h * d.Dvx, s.Vy + h * d.Dvy, s.M + h * d.Dm);
        }

        /// <summary>One classical RK4 step of size dt (seconds). dragArea 0 turns drag off.</summary>
        public static State Rk4(State s, double dt, Burn b, double dragArea)
        {
            var k1 = F(s, b, dragArea);
            var k2 = F(Add(s, k1, dt / 2), b, dragArea);
            var k3 = F(Add(s, k2, dt / 2), b, dragArea);
            var k4 = F(Add(s, k3, dt), b, dragArea);
            return new State(
                s.X + dt / 6 * (k1.Dx + 2 * k2.Dx + 2 * k3.Dx + k4.Dx),
                s.Y + dt / 6 * (k1.Dy + 2 * k2.Dy + 2 * k3.Dy + k4.Dy),
                s.Vx + dt / 6 * (k1.Dvx + 2 * k2.Dvx + 2 * k3.Dvx + k4.Dvx),
                s.Vy + dt / 6 * (k1.Dvy + 2 * k2.Dvy + 2 * k3.Dvy + k4.Dvy),
                s.M + dt / 6 * (k1.Dm + 2 * k2.Dm + 2 * k3.Dm + k4.Dm));
        }

        /// <summary>Integrates for `seconds` with a fixed step; the last step is shortened to land exactly on the end time.</summary>
        public static State Propagate(State s, double seconds, double dt, Burn b, double dragArea)
        {
            if (dt <= 0) throw new ArgumentException("dt must be positive");
            double t = 0;
            while (t < seconds - 1e-9)
            {
                double h = Math.Min(dt, seconds - t);
                s = Rk4(s, h, b, dragArea);
                t += h;
            }
            return s;
        }
    }
}
