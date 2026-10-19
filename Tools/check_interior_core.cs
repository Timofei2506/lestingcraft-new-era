using System;
using LestingCraft.Gameplay;

class ShuttleInteriorCheck
{
    static int n;
    static void Check(bool ok,string label){n++;if(!ok)throw new Exception("FAIL "+n+": "+label);}
    static void Main()
    {
        Check(ShuttleInteriorLayout.Zone(0,0,0)==InteriorZone.Elevator,"lift ground");
        Check(ShuttleInteriorLayout.Zone(0,28,0)==InteriorZone.Elevator,"lift top");
        Check(ShuttleInteriorLayout.Zone(2,28,0)==InteriorZone.Airlock,"airlock");
        Check(ShuttleInteriorLayout.Zone(5,28,0)==InteriorZone.MidDeck,"mid deck");
        Check(ShuttleInteriorLayout.Zone(9,28,0)==InteriorZone.FlightDeck,"flight deck");
        Check(ShuttleInteriorLayout.Zone(40,28,0)==InteriorZone.Outside,"outside");
        Check(ShuttleInteriorLayout.SeatCount==ShuttleCabin.Seats,"six seats match launch manifest");
        for(int i=0;i<6;i++)
        {
            float x,z;ShuttleInteriorLayout.SeatPosition(i,out x,out z);
            Check(ShuttleInteriorLayout.Zone(x,28,z)==InteriorZone.FlightDeck,"seat inside flight deck "+i);
        }
        // Continuous travel; button cannot operate an empty lift, travel time precisely 6 s.
        var lift=new ShuttleElevator();
        Check(!lift.RequestUp(),"empty lift cannot move");
        var s=new ShuttleInteriorSession();s.UpdatePosition(0,0,0);
        Check(s.BoardLift()&&!s.BoardLift(),"board once");
        Check(s.Elevator.RequestUp(),"button up");
        Check(!s.Elevator.Exit()&&!s.Elevator.RequestDown(),"moving interlocks");
        s.Elevator.Advance(3);
        Check(Math.Abs(s.Elevator.Height-14)<1e-4,"3 s = 14 m; no teleport");
        Check(s.Elevator.IsMoving,"still in transit");
        s.Elevator.Advance(3);
        Check(s.Elevator.State==ElevatorState.Top&&Math.Abs(s.Elevator.Height-28)<1e-4,"6 s = 28 m");
        Check(s.Elevator.Exit(),"exit at top");
        Check(!s.Elevator.RequestDown(),"return requires passenger");
        s.UpdatePosition(0,28,0);
        Check(s.BoardLift()&&s.Elevator.RequestDown(),"board and descend");
        s.Elevator.Advance(2);
        Check(Math.Abs(s.Elevator.Height-28f*2f/3f)<.001,"continuous descent");
        s.Elevator.Advance(4);
        Check(s.Elevator.State==ElevatorState.Ground&&Math.Abs(s.Elevator.Height)<1e-4,"bottom after 6s");
        Check(s.Elevator.Exit(),"exit at ground");
        // Do not open outer hatch from ground / during flight; no simultaneously open airlock doors.
        Check(!s.SetDoor(InteriorDoor.Outer,true),"outer needs lift at top");
        s.UpdatePosition(0,0,0);Check(s.BoardLift(),"lift boards");
        s.Elevator.RequestUp();s.Elevator.Advance(6);
        Check(s.SetDoor(InteriorDoor.Outer,true),"open outer at top");
        Check(!s.SetDoor(InteriorDoor.Inner,true),"interlocked inner door");
        Check(s.SetDoor(InteriorDoor.Outer,false),"close outer");
        Check(s.SetDoor(InteriorDoor.Inner,true),"open inner");
        Check(!s.SetDoor(InteriorDoor.Outer,true),"interlocked outer door");
        Check(s.SetDoor(InteriorDoor.Inner,false)&&s.Airlock.Sealed,"sealed after both closed");
        // Cannot sit remotely or through a hatch, must physically be near that seat in the flight deck.
        Check(!s.Sit(0,1),"not in flight deck");
        s.UpdatePosition(11,28,-1);
        Check(s.Sit(0,1),"sit commander in cockpit");
        Check(s.Launch.Cabin.CommanderSeated,"same authoritative launch manifest");
        Check(!s.Sit(1,1),"one active seat per person");
        Check(s.Stand(1),"stand before arming while hatch open");
        Check(!s.Stand(1),"cannot stand twice");
        s.UpdatePosition(11,28,1);
        Check(!s.Sit(0,2),"seat 0 too far");
        Check(s.Sit(1,2),"pilot seat available");
        // All seats usable with no position warp.
        for(int i=0;i<6;i++)
        {
            var crew=new ShuttleInteriorSession();float x,z;ShuttleInteriorLayout.SeatPosition(i,out x,out z);
            crew.UpdatePosition(x,28,z);Check(crew.Sit(i,i+1),"seat "+i);
            Check(crew.Stand(i+1),"stand "+i);
        }
        // Sealed cabin + commander + bus/feed = can arm; lift must also be down before ignition.
        var flight=new ShuttleInteriorSession();
        float cx,cz;ShuttleInteriorLayout.SeatPosition(0,out cx,out cz);
        flight.UpdatePosition(cx,28,cz);
        Check(flight.Sit(0,7),"commander boards launch");
        Check(flight.Launch.SetSwitch(PanelSwitch.Hatch,true),"manually seal test cabin");
        Check(flight.Launch.SetSwitch(PanelSwitch.MainBus,true),"main bus");
        Check(flight.Launch.SetSwitch(PanelSwitch.FuelFeed,true),"fuel feed");
        Check(flight.Launch.SetSwitch(PanelSwitch.Arm,true),"arm");
        Check(flight.ReadyToLaunch,"sealed cabin and lift down");
        Check(!flight.Stand(7),"cannot stand while sealed/armed");
        Check(flight.Launch.Ignite(),"start existing countdown");
        Check(!flight.SetDoor(InteriorDoor.Outer,true),"cannot open outer hatch in countdown");
        Check(!flight.SetDoor(InteriorDoor.Inner,true),"cannot open inner hatch in countdown");
        Check(!flight.Stand(7),"seated during countdown");
        flight.Launch.Advance(LaunchControl.CountdownSeconds+LaunchControl.OrbitInsertionTime);
        Check(flight.Launch.Phase==LaunchPhase.Orbit,"orbital insertion");
        Check(flight.Stand(7),"free to get up and walk after ascent");
        // Guard against negative dt corrupting persistent progress.
        bool threw=false;try{lift.Advance(-1);}catch(ArgumentOutOfRangeException){threw=true;}
        Check(threw,"negative time rejected");
        Console.WriteLine("PASS: "+n+" shuttle interior checks (physical lift, airlock, 3D walkable zones, six seats, launch)");
    }
}
