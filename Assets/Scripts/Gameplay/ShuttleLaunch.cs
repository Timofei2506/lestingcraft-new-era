using System;
using System.Collections.Generic;

namespace LestingCraft.Gameplay
{
    /// <summary>Six crew seats of the shuttle orbiter. Seat 0 is the commander. Pure logic, no Unity.</summary>
    public sealed class ShuttleCabin
    {
        public const int Seats = 6;
        readonly int[] seat = new int[Seats];   // 0 = empty, otherwise a crew id > 0

        public int Count { get { int n = 0; foreach (int c in seat) if (c != 0) n++; return n; } }
        public bool CommanderSeated { get { return seat[0] != 0; } }
        public int SeatOf(int crewId)
        {
            if (crewId <= 0) return -1;
            for (int i = 0; i < Seats; i++) if (seat[i] == crewId) return i;
            return -1;
        }
        public bool Board(int seatIndex, int crewId)
        {
            if (crewId <= 0 || seatIndex < 0 || seatIndex >= Seats) return false;
            if (seat[seatIndex] != 0 || SeatOf(crewId) >= 0) return false;
            seat[seatIndex] = crewId;
            return true;
        }
        public bool Leave(int crewId)
        {
            int i = SeatOf(crewId);
            if (i < 0) return false;
            seat[i] = 0;
            return true;
        }
    }

    /// <summary>Cockpit panel switches. Each one has a rule in LaunchControl.SetSwitch.</summary>
    public enum PanelSwitch { MainBus, FuelFeed, Arm, Hatch }

    public enum LaunchPhase { Standby, Ready, Countdown, Ascent, Orbit }

    public enum LaunchEvent { Ignition, SrbSeparation, MainEngineCutoff, ExternalTankSeparation, OrbitInsertion }

    /// <summary>
    /// Launch sequence of the stack: shuttle orbiter on the external tank, two solid rocket boosters.
    /// Times follow the public Space Shuttle profile: SRB burnout and separation about 124 s, MECO about 510 s,
    /// external tank separation about 530 s, orbital insertion burn about 630 s. Game defaults, not a full simulation.
    /// </summary>
    public sealed class LaunchControl
    {
        public const double CountdownSeconds = 10.0;
        public const double SrbSeparationTime = 124.0;
        public const double MainEngineCutoffTime = 510.0;
        public const double ExternalTankSeparationTime = 530.0;
        public const double OrbitInsertionTime = 630.0;

        public LaunchPhase Phase = LaunchPhase.Standby;
        /// <summary>Seconds relative to T-0. Negative during countdown, from -10 to 0.</summary>
        public double T = -CountdownSeconds;
        public readonly ShuttleCabin Cabin = new ShuttleCabin();
        readonly List<LaunchEvent> fired = new List<LaunchEvent>();
        // A crew member may leave a chair without exiting the spacecraft; seat occupancy and crew aboard differ.
        readonly HashSet<int> aboard = new HashSet<int>();
        public bool IsAboard(int crewId) { return aboard.Contains(crewId); }

        bool mainBus, fuelFeed, arm, hatchClosed;

        public bool IsOn(PanelSwitch s)
        {
            switch (s)
            {
                case PanelSwitch.MainBus: return mainBus;
                case PanelSwitch.FuelFeed: return fuelFeed;
                case PanelSwitch.Arm: return arm;
                default: return hatchClosed;
            }
        }
        public IList<LaunchEvent> Fired { get { return fired.AsReadOnly(); } }

        /// <summary>Hatch closed means the cabin is sealed. Boarding needs an open hatch.</summary>
        public bool HatchOpen { get { return !hatchClosed; } }

        /// <summary>Switches are only active before ignition. Returns false when the rule forbids the change.</summary>
        public bool SetSwitch(PanelSwitch s, bool on)
        {
            if (Phase != LaunchPhase.Standby && Phase != LaunchPhase.Ready) return false;
            switch (s)
            {
                case PanelSwitch.MainBus:
                    // Powering the bus down cuts everything that depends on it.
                    mainBus = on;
                    if (!on) { fuelFeed = false; arm = false; }
                    break;
                case PanelSwitch.FuelFeed:
                    if (on && !mainBus) return false;
                    fuelFeed = on;
                    if (!on) arm = false;
                    break;
                case PanelSwitch.Arm:
                    // Arming needs fuel feed, a sealed cabin and a commander in seat 0.
                    if (on && (!fuelFeed || !hatchClosed || !Cabin.CommanderSeated)) return false;
                    arm = on;
                    break;
                case PanelSwitch.Hatch:
                    // Opening the hatch while armed disarms. Closing needs at least one crew member aboard.
                    if (on && Cabin.Count == 0) return false;
                    hatchClosed = on;
                    if (!on) arm = false;
                    break;
            }
            Refresh();
            return true;
        }

        /// <summary>Boarding is only possible with an open hatch, in standby. Commander seat 0 is required before arming.</summary>
        public bool Board(int seatIndex, int crewId)
        {
            if (Phase != LaunchPhase.Standby || hatchClosed) return false;
            if (aboard.Contains(crewId) || !Cabin.Board(seatIndex, crewId)) return false;
            aboard.Add(crewId);
            Refresh();
            return true;
        }

        public bool Leave(int crewId)
        {
            if (Phase != LaunchPhase.Standby || hatchClosed) return false;
            if (!Cabin.Leave(crewId)) return false;
            aboard.Remove(crewId);
            if (!Cabin.CommanderSeated) arm = false;
            Refresh();
            return true;
        }

        /// <summary>Stand up INSIDE a sealed shuttle. This does not disembark. Crew can walk in standby/ready/orbit, not under ascent g-load.</summary>
        public bool Unseat(int crewId)
        {
            if (Phase == LaunchPhase.Countdown || Phase == LaunchPhase.Ascent || !aboard.Contains(crewId)) return false;
            if (!Cabin.Leave(crewId)) return false;
            if (!Cabin.CommanderSeated) arm = false;
            Refresh();
            return true;
        }

        /// <summary>Return to a chair while still aboard, even if the entry hatch is sealed.</summary>
        public bool Reseat(int seatIndex, int crewId)
        {
            if (Phase == LaunchPhase.Countdown || Phase == LaunchPhase.Ascent || !aboard.Contains(crewId)) return false;
            if (!Cabin.Board(seatIndex, crewId)) return false;
            Refresh();
            return true;
        }

        /// <summary>Walk out through the open hatch when standing (not in flight). A boarded crew member instead uses Leave.</summary>
        public bool ExitStanding(int crewId)
        {
            if (Phase != LaunchPhase.Standby || hatchClosed || Cabin.SeatOf(crewId) >= 0) return false;
            return aboard.Remove(crewId);
        }

        /// <summary>Ignition button. Only from Ready. Starts the 10 s countdown.</summary>
        public bool Ignite()
        {
            if (Phase != LaunchPhase.Ready) return false;
            Phase = LaunchPhase.Countdown;
            T = -CountdownSeconds;
            return true;
        }

        /// <summary>Abort button. Only during countdown, before T-0. Returns the stack to standby with arm off.</summary>
        public bool Abort()
        {
            if (Phase != LaunchPhase.Countdown) return false;
            Phase = LaunchPhase.Standby;
            arm = false;
            T = -CountdownSeconds;
            Refresh();
            return true;
        }

        /// <summary>Advances the sequence. Fires every event whose time is reached. Returns the events fired in this call.</summary>
        public List<LaunchEvent> Advance(double dt)
        {
            var out_ = new List<LaunchEvent>();
            while (dt > 1e-12 && (Phase == LaunchPhase.Countdown || Phase == LaunchPhase.Ascent))
            {
                double next = NextEventTime();
                double step = Math.Min(dt, next - T);
                T += step;
                dt -= step;
                if (T >= next - 1e-9)
                {
                    T = next;
                    LaunchEvent e = EventAt(next);
                    fired.Add(e);
                    out_.Add(e);
                    if (e == LaunchEvent.Ignition) Phase = LaunchPhase.Ascent;
                    if (e == LaunchEvent.OrbitInsertion) { Phase = LaunchPhase.Orbit; break; }
                }
            }
            return out_;
        }

        double NextEventTime()
        {
            if (Phase == LaunchPhase.Countdown) return 0.0;
            if (T < SrbSeparationTime) return SrbSeparationTime;
            if (T < MainEngineCutoffTime) return MainEngineCutoffTime;
            if (T < ExternalTankSeparationTime) return ExternalTankSeparationTime;
            return OrbitInsertionTime;
        }

        static LaunchEvent EventAt(double t)
        {
            if (t <= 0.0) return LaunchEvent.Ignition;
            if (t <= SrbSeparationTime + 1e-9) return LaunchEvent.SrbSeparation;
            if (t <= MainEngineCutoffTime + 1e-9) return LaunchEvent.MainEngineCutoff;
            if (t <= ExternalTankSeparationTime + 1e-9) return LaunchEvent.ExternalTankSeparation;
            return LaunchEvent.OrbitInsertion;
        }

        /// <summary>Ready means every condition of the launch holds. Otherwise standby.</summary>
        void Refresh()
        {
            if (Phase != LaunchPhase.Standby && Phase != LaunchPhase.Ready) return;
            bool ready = mainBus && fuelFeed && arm && hatchClosed && Cabin.CommanderSeated;
            Phase = ready ? LaunchPhase.Ready : LaunchPhase.Standby;
        }
    }

    /// <summary>
    /// Mass and thrust of the stack. Approximate public Space Shuttle figures, rounded. Game defaults, not a full simulation.
    /// Boosters: total thrust 27 MN at liftoff, cut to two thirds after 50 s. Main engines: three RS-25 at 2.28 MN each (vacuum).
    /// Main engines throttle between 65 % and 100 % to hold acceleration at or below 3 g as the tank empties.
    /// </summary>
    public static class LaunchProfile
    {
        public const double G0 = 9.80665;
        public const double SrbDryEach = 83000, SrbPropellantEach = 504000;
        public const double EtDry = 35000, EtPropellant = 719000;
        public const double OrbiterMass = 104000;
        public const double SrbThrustTotal = 27e6;
        public const double SrbThrottleStart = 50.0;
        public const double SrbThrustReduction = 2.0 / 3.0;
        public const double MainThrustMax = 3 * 2.28e6;
        public const double MainThrottleFloor = 0.65;
        public const double GLimit = 3.0;

        public static double StackMass(double t)
        {
            double mass = OrbiterMass + EtDry + EtPropellant + 2 * (SrbDryEach + SrbPropellantEach);
            double srbBurn = Math.Min(Math.Max(t, 0.0), LaunchControl.SrbSeparationTime) / LaunchControl.SrbSeparationTime;
            mass -= 2 * SrbPropellantEach * srbBurn;
            if (t >= LaunchControl.SrbSeparationTime) mass -= 2 * SrbDryEach;
            double etBurn = Math.Min(Math.Max(t, 0.0), LaunchControl.MainEngineCutoffTime) / LaunchControl.MainEngineCutoffTime;
            mass -= EtPropellant * etBurn;
            if (t >= LaunchControl.ExternalTankSeparationTime) mass -= EtDry;
            return mass;
        }

        /// <summary>Main engine thrust: full until the 3 g limit forces throttling, never below the floor.</summary>
        public static double MainThrust(double t)
        {
            if (t >= LaunchControl.MainEngineCutoffTime) return 0;
            double limit = GLimit * G0 * StackMass(t);
            return Math.Min(MainThrustMax, Math.Max(MainThrottleFloor * MainThrustMax, limit));
        }

        public static double SrbThrust(double t)
        {
            if (t >= LaunchControl.SrbSeparationTime) return 0;
            return SrbThrustTotal * (t < SrbThrottleStart ? 1.0 : SrbThrustReduction);
        }

        public static double Thrust(double t) { return SrbThrust(t) + MainThrust(t); }

        /// <summary>Acceleration in g along the thrust axis, ignoring gravity and drag.</summary>
        public static double AccelerationG(double t)
        {
            double m = StackMass(t);
            return m > 0 ? Thrust(t) / m / G0 : 0;
        }

        /// <summary>Ideal delta-v of the powered ascent: integral of thrust over mass to MECO. No gravity or drag.</summary>
        public static double IdealDeltaV(double step)
        {
            double dv = 0;
            for (double t = 0; t < LaunchControl.MainEngineCutoffTime; t += step)
            {
                double m = StackMass(t);
                if (m > 0) dv += Thrust(t) / m * step;
            }
            return dv;
        }
    }
}
