using System;

namespace LestingCraft.Gameplay
{
    /// <summary>Local coordinates, metres: X goes from service tower through airlock to nose; Y is height above launch pad; Z is across the cabin.</summary>
    public enum InteriorZone { Outside, Elevator, Airlock, MidDeck, FlightDeck }
    public enum InteriorDoor { Outer, Inner, FlightDeck }
    public enum ElevatorState { Ground, Rising, Top, Descending }

    public struct InteriorBounds
    {
        public readonly float X0, X1, Y0, Y1, Z0, Z1;
        public InteriorBounds(float x0, float x1, float y0, float y1, float z0, float z1)
        { X0=x0; X1=x1; Y0=y0; Y1=y1; Z0=z0; Z1=z1; }
        public bool Contains(float x, float y, float z)
        { return x>=X0 && x<=X1 && y>=Y0 && y<=Y1 && z>=Z0 && z<=Z1; }
    }

    public static class ShuttleInteriorLayout
    {
        // These are navigable WALK volumes, not visual-only cubes. Rider height is added to the lift floor by the Unity controller.
        public const float LiftHeight=28f;
        public const float LiftTravelSeconds=6f;
        public const float OuterDoorX=1.15f;
        public const int SeatCount=6;
        public static readonly InteriorBounds Lift=new InteriorBounds(-.9f,.9f,0,30.3f,-.9f,.9f);
        public static readonly InteriorBounds Airlock=new InteriorBounds(1.2f,3.4f,28f,30.2f,-.8f,.8f);
        public static readonly InteriorBounds MidDeck=new InteriorBounds(3.4f,7.2f,28f,30.4f,-2f,2f);
        public static readonly InteriorBounds FlightDeck=new InteriorBounds(7.2f,13.5f,28f,31f,-2f,2f);
        public static InteriorZone Zone(float x,float y,float z)
        {
            if(Lift.Contains(x,y,z))return InteriorZone.Elevator;
            if(Airlock.Contains(x,y,z))return InteriorZone.Airlock;
            if(MidDeck.Contains(x,y,z))return InteriorZone.MidDeck;
            if(FlightDeck.Contains(x,y,z))return InteriorZone.FlightDeck;
            return InteriorZone.Outside;
        }
        public static void SeatPosition(int seat,out float x,out float z)
        {
            if(seat<0 || seat>=SeatCount)throw new ArgumentOutOfRangeException("seat");
            x = seat<2 ? 11f : seat<4 ? 9f : 8f;
            z = (seat%2==0) ? -1f : 1f;
        }
    }

    /// <summary>Server-authoritative progress can be advanced in fixed time steps; never moves a player instantaneously.</summary>
    public sealed class ShuttleElevator
    {
        readonly float travelHeight;
        public ShuttleElevator(float height=ShuttleInteriorLayout.LiftHeight)
        {
            if(height<=0||float.IsNaN(height)||float.IsInfinity(height))throw new ArgumentOutOfRangeException("height");
            travelHeight=height;
        }
        public ElevatorState State {get;private set;}
        public float Progress {get;private set;} // 0 at ground, 1 at top
        public bool Occupied {get;private set;}
        public float Height {get{return Progress*travelHeight;}}
        public bool IsMoving {get{return State==ElevatorState.Rising||State==ElevatorState.Descending;}}
        public bool Board()
        {
            if(IsMoving||Occupied)return false;
            Occupied=true;return true;
        }
        public bool Exit()
        {
            if(IsMoving||!Occupied)return false;
            Occupied=false;return true;
        }
        public bool RequestUp()
        {
            if(State!=ElevatorState.Ground||!Occupied)return false;
            State=ElevatorState.Rising;return true;
        }
        public bool RequestDown()
        {
            if(State!=ElevatorState.Top||!Occupied)return false;
            State=ElevatorState.Descending;return true;
        }
        /// <summary>Recall the empty platform so the next crew member can board from ground.
        /// Nobody is carried if the rider already stepped off at the top.</summary>
        public bool ReturnEmpty()
        {
            if(State!=ElevatorState.Top||Occupied)return false;
            State=ElevatorState.Descending;return true;
        }
        public void Advance(float seconds)
        {
            if(seconds<0||float.IsNaN(seconds)||float.IsInfinity(seconds))throw new ArgumentOutOfRangeException("seconds");
            if(!IsMoving)return;
            float step=seconds/ShuttleInteriorLayout.LiftTravelSeconds;
            Progress=State==ElevatorState.Rising?Math.Min(1f,Progress+step):Math.Max(0f,Progress-step);
            if(Progress>=1f)State=ElevatorState.Top;
            if(Progress<=0f)State=ElevatorState.Ground;
        }
    }

    /// <summary>Outer and inner hatch are never open together: actual airlock, not a teleport from lift to seat.</summary>
    public sealed class ShuttleAirlock
    {
        public bool OuterOpen {get;private set;}
        public bool InnerOpen {get;private set;}
        public bool FlightDeckOpen {get;private set;}=true;
        public bool Sealed {get{return !OuterOpen && !InnerOpen;}}
        public bool SetDoor(InteriorDoor door,bool open,bool atTop,bool inFlight)
        {
            if(door==InteriorDoor.Outer)
            {
                if(open && (!atTop||inFlight||InnerOpen))return false;
                OuterOpen=open;return true;
            }
            if(door==InteriorDoor.Inner)
            {
                if(open && (OuterOpen||inFlight))return false;
                InnerOpen=open;return true;
            }
            if(door==InteriorDoor.FlightDeck){FlightDeckOpen=open;return true;}
            return false;
        }
    }

    /// <summary>First functional slice: positions, lift, pressurised airlock and access to six cockpit seats.
    /// Interaction distance is verified in the gameplay core, so a far-away player cannot sit by sending a seat id.
    /// Visual mesh and movement/collision are the Unity presentation layer, never an invisible teleport.</summary>
    public sealed class ShuttleInteriorSession
    {
        public readonly ShuttleElevator Elevator=new ShuttleElevator();
        public readonly ShuttleAirlock Airlock=new ShuttleAirlock();
        public readonly LaunchControl Launch=new LaunchControl();
        public int SeatedSeat {get;private set;}=-1;
        public int CrewId {get;private set;}
        public float X {get;private set;}
        public float Y {get;private set;}
        public float Z {get;private set;}
        public InteriorZone Zone {get{return ShuttleInteriorLayout.Zone(X,Y,Z);}}
        public void UpdatePosition(float x,float y,float z)
        { X=x;Y=y;Z=z; }
        public bool BoardLift()
        { return Zone==InteriorZone.Elevator && Math.Abs(Y-Elevator.Height)<1.5f && Elevator.Board(); }
        public bool ExitLift()
        { return Elevator.Exit(); }
        public bool SetDoor(InteriorDoor door,bool open)
        {
            bool inFlight=Launch.Phase==LaunchPhase.Ascent||Launch.Phase==LaunchPhase.Orbit||Launch.Phase==LaunchPhase.Countdown;
            if(!Airlock.SetDoor(door,open,Elevator.State==ElevatorState.Top,inFlight))return false;
            // Single authoritative seal bit; never silently unlock an armed LaunchControl.
            if(door!=InteriorDoor.FlightDeck && !open && Airlock.Sealed && Launch.Cabin.Count>0)
                Launch.SetSwitch(PanelSwitch.Hatch,true);
            if(door!=InteriorDoor.FlightDeck && open && !inFlight)
                Launch.SetSwitch(PanelSwitch.Hatch,false);
            return true;
        }
        public bool Sit(int seat,int crewId)
        {
            if(SeatedSeat!=-1 || crewId<=0 || seat<0 || seat>=ShuttleInteriorLayout.SeatCount || Zone!=InteriorZone.FlightDeck)return false;
            float sx,sz;ShuttleInteriorLayout.SeatPosition(seat,out sx,out sz);
            if(Math.Abs(X-sx)>1.8f||Math.Abs(Z-sz)>1.3f||Math.Abs(Y-28f)>1.5f)return false;
            if(!(Launch.IsAboard(crewId)?Launch.Reseat(seat,crewId):Launch.Board(seat,crewId)))return false;
            SeatedSeat=seat;CrewId=crewId;return true;
        }
        public bool Stand(int crewId)
        {
            if(SeatedSeat<0||CrewId!=crewId)return false;
            // LaunchControl.Leave means leaving the vehicle; after insertion only relinquish the seat.
            if(Launch.Phase==LaunchPhase.Ascent||Launch.Phase==LaunchPhase.Countdown)return false;
            bool left=Launch.Unseat(crewId);
            if(!left)return false;
            SeatedSeat=-1;CrewId=0;return true;
        }
        public bool ReadyToLaunch {get{return Airlock.Sealed && !Elevator.IsMoving && !Elevator.Occupied && Launch.Phase==LaunchPhase.Ready;}}
    }
}
