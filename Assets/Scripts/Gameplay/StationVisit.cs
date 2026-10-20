using System;

namespace LestingCraft.Gameplay
{
    /// <summary>One port in the same continuous heliocentric frame as the spacecraft.
    /// This fixed first-pass orbit is a gameplay coordinate, not a real ephemeris.</summary>
    public static class OrbitalStation
    {
        public static readonly SpaceVector Center = SpaceMap.Center(SpaceDestination.Earth)+new SpaceVector(6771044,0,0);
        // The visible model origin is Center; its docking collar opens at local (-44,21,0).
        public static readonly SpaceVector Port = Center+new SpaceVector(-44,21,0);
        public const double MaxLatchDistance=2.0, MaxRelativeSpeed=.35, MinAlignment=.96;
        public const double PressurizationSeconds=8;
    }

    /// <summary>Validate an actual approach, pressure cycle and hatch; never repositions a player or craft.
    /// The server must derive positions, velocity, facing and crew membership from its own room state.</summary>
    public sealed class StationDocking
    {
        public bool Latched {get;private set;}
        public bool HatchOpen {get;private set;}
        public double PressureSeconds {get;private set;}
        public bool Pressurized {get{return Latched && PressureSeconds>=OrbitalStation.PressurizationSeconds;}}
        public bool TryLatch(SpaceVector shipNose,SpaceVector relativeVelocity,SpaceVector shipForward,
                             SpaceVector outwardPortNormal,bool sealedShipHatch,bool pilotAuthorized)
        {
            if(Latched||!pilotAuthorized||!sealedShipHatch||!Finite(shipNose)||!Finite(relativeVelocity)||
               !Finite(shipForward)||!Finite(outwardPortNormal)||
               SpaceVector.Distance(shipNose,OrbitalStation.Port)>OrbitalStation.MaxLatchDistance||
               relativeVelocity.Length>OrbitalStation.MaxRelativeSpeed||
               shipForward.Length<.99||outwardPortNormal.Length<.99||
               Dot(shipForward.Normalized,outwardPortNormal.Normalized)>-OrbitalStation.MinAlignment)return false;
            Latched=true;HatchOpen=false;PressureSeconds=0;return true;
        }
        public void Advance(double seconds)
        {
            if(seconds<0||double.IsNaN(seconds)||double.IsInfinity(seconds))throw new ArgumentOutOfRangeException("seconds");
            if(Latched)PressureSeconds=Math.Min(OrbitalStation.PressurizationSeconds,PressureSeconds+seconds);
        }
        public bool SetHatch(bool open)
        {
            if(!Latched||(open&&!Pressurized))return false;
            HatchOpen=open;return true;
        }
        public bool TryUndock(bool allCrewAboard,bool pilotAuthorized)
        {
            if(!Latched||HatchOpen||!allCrewAboard||!pilotAuthorized)return false;
            Latched=false;PressureSeconds=0;return true;
        }
        static double Dot(SpaceVector a,SpaceVector b){return a.X*b.X+a.Y*b.Y+a.Z*b.Z;}
        static bool Finite(SpaceVector v){return !double.IsNaN(v.X)&&!double.IsNaN(v.Y)&&!double.IsNaN(v.Z)&&
            !double.IsInfinity(v.X)&&!double.IsInfinity(v.Y)&&!double.IsInfinity(v.Z);}
    }

    /// <summary>Station-local walking volumes, metres. Players enter from the docking collar at X=-44,
    /// cross the hatch at X=-42 on foot and walk down the central pressurized corridor; no scene-teleport API.</summary>
    public static class StationInteriorLayout
    {
        public static bool Inside(double x,double y,double z)
        {
            if(double.IsNaN(x)||double.IsNaN(y)||double.IsNaN(z))return false;
            if(y<0||y>2.05)return false;
            return x>=-44&&x<=20&&z>=-1.15&&z<=1.15;
        }
        // Validate successive server-side movement samples, not a request to move the player.
        public static bool CanWalk(double x,double y,double z,double nx,double ny,double nz,bool hatchOpen)
        {
            if(!Inside(x,y,z)||!Inside(nx,ny,nz))return false;
            double dx=nx-x,dy=ny-y,dz=nz-z;
            if(dx*dx+dy*dy+dz*dz>.55*.55)return false;
            if(!hatchOpen&&((x< -42&&nx>= -42)||(x>= -42&&nx< -42)))return false;
            for(int i=1;i<=4;i++)if(!Inside(x+dx*i/4,y+dy*i/4,z+dz*i/4))return false;
            return true;
        }
    }

    public static class StationAstronauts
    {
        public const int Count=3;
        public static string Profession(int role)
        {
            switch(role){case 0:return "Бортинженер";case 1:return "Специалист по кислороду";case 2:return "Квартирмейстер";
                default:throw new ArgumentOutOfRangeException("role");}
        }
        public static SpaceVector Position(int role)
        {
            switch(role){case 0:return new SpaceVector(5,.0,0);case 1:return new SpaceVector(11,0,0);
                case 2:return new SpaceVector(17,0,0);default:throw new ArgumentOutOfRangeException("role");}
        }
        public static TradeOffer Offer(int role)
        {
            switch(role){case 0:return new TradeOffer(141,6,FuelChain.CompressedAir,4);
                case 1:return new TradeOffer(FuelChain.CompressedAir,9,FuelChain.LiquidOxygen,2);
                case 2:return new TradeOffer(140,4,Items.Bread,4);
                default:throw new ArgumentOutOfRangeException("role");}
        }
        /// <summary>Same atomic inventory transfer as a home-world villager, with station/crew/range checks.
        /// Called only by an authoritative transaction handler, not directly from UI.</summary>
        public static bool TryTrade(StationDocking dock,bool serverVerifiedCrew,int role,SpaceVector playerLocal,
                                    Inventory inventory,out Inventory updated)
        {
            updated=null;
            if(dock==null||!dock.Latched||!dock.HatchOpen||!serverVerifiedCrew||role<0||role>=Count||
               !StationInteriorLayout.Inside(playerLocal.X,playerLocal.Y,playerLocal.Z)||
               SpaceVector.Distance(playerLocal,Position(role))>4)return false;
            return TradeOffer.Apply(inventory,Offer(role),out updated);
        }
    }
}
