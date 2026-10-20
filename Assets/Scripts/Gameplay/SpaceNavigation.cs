using System;

namespace LestingCraft.Gameplay
{
    /// <summary>Double precision metres in one continuous heliocentric coordinate frame (not sector IDs).</summary>
    public struct SpaceVector
    {
        public readonly double X,Y,Z;
        public SpaceVector(double x,double y,double z){X=x;Y=y;Z=z;}
        public static SpaceVector operator +(SpaceVector a,SpaceVector b)=>new SpaceVector(a.X+b.X,a.Y+b.Y,a.Z+b.Z);
        public static SpaceVector operator -(SpaceVector a,SpaceVector b)=>new SpaceVector(a.X-b.X,a.Y-b.Y,a.Z-b.Z);
        public static SpaceVector operator *(SpaceVector a,double s)=>new SpaceVector(a.X*s,a.Y*s,a.Z*s);
        public double Length=>Math.Sqrt(X*X+Y*Y+Z*Z);
        public double Magnitude=>Length;
        public SpaceVector Normalized=>Length>0?this*(1/Length):new SpaceVector(0,0,0);
        public static double Distance(SpaceVector a,SpaceVector b)=>(a-b).Length;
    }

    public enum FlightStage { Docked, Ascent, Cruise, Approach, LandingReady, Landed }
    public enum SpaceDestination { Earth, Moon, Mars, Venus, Mercury }
    public static class SpaceMap
    {
        public const double AU=149597870700.0;
        public const double LightYear=9460730472580800.0;
        public static SpaceVector Center(SpaceDestination d)
        {
            switch(d)
            {
                case SpaceDestination.Earth:return new SpaceVector(AU,0,0);
                case SpaceDestination.Moon:return new SpaceVector(AU+384400000.0,0,0);
                case SpaceDestination.Mars:return new SpaceVector(1.524*AU,.18*AU,0);
                case SpaceDestination.Venus:return new SpaceVector(.723*AU,-.24*AU,0);
                case SpaceDestination.Mercury:return new SpaceVector(.387*AU,.09*AU,0);
                default:throw new ArgumentOutOfRangeException("d");
            }
        }
        // Deterministic systems on an unbounded Cartesian lattice. Each star is at a stable actual
        // position; the lattice only indexes generation, never relocates the craft or loads a new sector.
        public static SpaceVector Star(long x,long y,long z,int seed)
        {
            unchecked
            {
                ulong h=(ulong)(uint)seed^((ulong)x*0x9E3779B97F4A7C15UL)^((ulong)y*0xBF58476D1CE4E5B9UL)^((ulong)z*0x94D049BB133111EBUL);
                h=(h^(h>>30))*0xBF58476D1CE4E5B9UL;h=(h^(h>>27))*0x94D049BB133111EBUL;h^=h>>31;
                double cell=5*LightYear;
                return new SpaceVector((x+.15+((h>>0)&65535)/65536.0*.7)*cell,
                    (y+.15+((h>>16)&65535)/65536.0*.7)*cell,
                    (z+.15+((h>>32)&65535)/65536.0*.7)*cell);
            }
        }
    }

    /// <summary>
    /// Mission guidance core. Positions advance through the same double-precision frame on every tick.
    /// This is intentionally NOT OrbitalFlight's real-gravity RK4: a 30-minute lunar trip requires
    /// fictional propulsion. The orbit simulator remains separate; don't call this real orbital physics.
    /// No arrivals, landings or scene changes are performed by this class.
    /// </summary>
    public sealed class SpaceNavigation
    {
        public const double MoonSeconds=1800; // wall-clock seconds from manual ignition to landing opportunity
        public const double AscentSeconds=630;
        public const double ApproachSeconds=180;
        public FlightStage Stage {get;private set;}
        public SpaceVector Position {get;private set;}
        public SpaceVector Target {get;private set;}
        public double Elapsed {get;private set;}
        public double Duration {get;private set;}
        public double LandingBurn {get;private set;}
        public bool Autopilot {get;private set;}
        public SpaceVector Heading {get;private set;}
        public SpaceNavigation(SpaceVector start){Position=start;Target=start;Stage=FlightStage.Docked;}

        public bool Ignite(SpaceVector target,double seconds)
        {
            if(Stage!=FlightStage.Docked||seconds<AscentSeconds+ApproachSeconds+60||double.IsNaN(seconds)||double.IsInfinity(seconds)||
               double.IsNaN(target.X)||double.IsNaN(target.Y)||double.IsNaN(target.Z)||
               double.IsInfinity(target.X)||double.IsInfinity(target.Y)||double.IsInfinity(target.Z)||
               SpaceVector.Distance(Position,target)<1000)return false;
            Target=target;Duration=seconds;Elapsed=0;LandingBurn=0;Autopilot=false;
            Heading=(Target-Position).Normalized;Stage=FlightStage.Ascent;return true;
        }
        public bool SetAutopilot(bool on)
        {
            if(Stage!=FlightStage.Cruise)return false;
            Autopilot=on;return true;
        }
        public void Step(double realSeconds,SpaceVector manualDirection,double landingThrottle=0)
        {
            if(realSeconds<0||double.IsNaN(realSeconds)||double.IsInfinity(realSeconds))throw new ArgumentOutOfRangeException("realSeconds");
            if(Stage==FlightStage.Docked||Stage==FlightStage.Landed||realSeconds==0)return;
            // Consume the exact remaining time, not more. Pausing the game must omit Step entirely.
            double dt=Math.Min(realSeconds,Duration-Elapsed);
            if(dt<=0)return;
            if(Stage==FlightStage.Ascent||Autopilot)Heading=(Target-Position).Normalized;
            else if(manualDirection.Length>.01)Heading=manualDirection.Normalized;
            // In manual flight, releasing the stick preserves the last heading (no hidden autopilot).
            // Fictional drive cruise speed; the position is integrated, never replaced by a destination.
            double speed=SpaceVector.Distance(Position,Target)/Math.Max(dt,Duration-Elapsed);
            Position=Position+Heading*(speed*dt);
            double old=Elapsed;Elapsed+=dt;
            if(old>=Duration-ApproachSeconds||Elapsed>Duration-ApproachSeconds)
            {
                double burnSeconds=Math.Max(0,Elapsed-Math.Max(old,Duration-ApproachSeconds));
                LandingBurn+=burnSeconds*Math.Max(0,Math.Min(1,landingThrottle));
            }
            Stage=Elapsed>=Duration?FlightStage.LandingReady:Elapsed>=Duration-ApproachSeconds?FlightStage.Approach:Elapsed>=AscentSeconds?FlightStage.Cruise:FlightStage.Ascent;
        }
        public bool Land()
        {
            double landingRadius=Math.Max(1000000.0,Target.Length*1e-9);
            if(Stage!=FlightStage.LandingReady||LandingBurn<20||SpaceVector.Distance(Position,Target)>landingRadius)return false;
            Stage=FlightStage.Landed;return true;
        }
    }
}
