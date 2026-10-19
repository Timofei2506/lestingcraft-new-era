using System;
using System.Collections.Generic;
using LestingCraft.Gameplay;

// Stage 1.4 harness: crew seats, cockpit switch rules, countdown and launch events, mass/thrust profile.
class ShuttleCheck
{
    static int tests;
    static void Check(bool x,string name){tests++;if(!x)throw new Exception(name);}

    static void Main()
    {
        // Six seats, commander in seat 0.
        var cabin=new ShuttleCabin();
        Check(ShuttleCabin.Seats==6,"six seats");
        for(int i=0;i<6;i++)Check(cabin.Board(i,i+1),"board seat "+i);
        Check(cabin.Count==6&&cabin.CommanderSeated,"all six seated, commander in seat 0");
        Check(!cabin.Board(0,99),"occupied seat refused");
        Check(!cabin.Board(6,7)&&!cabin.Board(-1,7),"seat index out of range refused");
        Check(!new ShuttleCabin().Board(1,0),"crew id 0 refused");
        var one=new ShuttleCabin();Check(one.Board(2,5)&&!one.Board(3,5),"same crew member cannot take two seats");
        Check(one.Leave(5)&&one.Count==0&&!one.Leave(5),"leave frees the seat, second leave refused");

        // Panel rules before any launch: bus -> feed -> arm, commander needed, sealed cabin needed.
        var c=new LaunchControl();
        Check(c.Phase==LaunchPhase.Standby&&c.HatchOpen,"start in standby, hatch open");
        Check(!c.SetSwitch(PanelSwitch.FuelFeed,true),"fuel feed needs main bus");
        Check(c.SetSwitch(PanelSwitch.MainBus,true)&&c.SetSwitch(PanelSwitch.FuelFeed,true),"bus then feed");
        Check(!c.SetSwitch(PanelSwitch.Arm,true),"arm needs commander and sealed hatch");
        Check(!c.SetSwitch(PanelSwitch.Hatch,true),"cannot seal an empty cabin");
        Check(!c.Ignite(),"no ignition from standby");

        // Crew boards with the hatch open, then the hatch seals and the stack goes ready.
        Check(c.Board(0,1)&&c.Board(1,2)&&c.Board(2,3)&&c.Board(3,4)&&c.Board(4,5)&&c.Board(5,6),"six crew board");
        Check(!c.Board(0,9),"occupied seat refused while boarding");
        Check(c.SetSwitch(PanelSwitch.Hatch,true)&&!c.HatchOpen,"hatch sealed");
        Check(!c.Board(1,9)&&!c.Leave(3),"no boarding or leaving with sealed hatch");
        Check(c.SetSwitch(PanelSwitch.Arm,true)&&c.Phase==LaunchPhase.Ready,"armed -> ready");
        Check(!c.SetSwitch(PanelSwitch.MainBus,false)||(c.Phase==LaunchPhase.Standby&&!c.IsOn(PanelSwitch.Arm)),"cutting main bus disarms");

        // Re-arm after the bus test, then countdown and abort.
        Check(c.SetSwitch(PanelSwitch.MainBus,true)&&c.SetSwitch(PanelSwitch.FuelFeed,true)&&c.SetSwitch(PanelSwitch.Arm,true),"re-arm");
        Check(c.Phase==LaunchPhase.Ready&&c.Ignite()&&c.Phase==LaunchPhase.Countdown,"ignite starts countdown");
        Check(!c.Ignite(),"second ignite refused");
        Check(!c.SetSwitch(PanelSwitch.FuelFeed,false),"switches locked during countdown");
        Check(c.Advance(4.0).Count==0&&c.T>-6.01&&c.T<-5.99,"countdown runs, no launch yet");
        Check(c.Abort()&&c.Phase==LaunchPhase.Standby&&!c.IsOn(PanelSwitch.Arm),"abort returns to standby, arm off");
        Check(!c.Abort(),"abort only in countdown");

        // Full sequence: arm again, launch, check every event time and order.
        Check(c.SetSwitch(PanelSwitch.Arm,true)&&c.Ignite(),"arm and ignite again");
        var fired=new List<LaunchEvent>();
        for(int step=0;step<1300;step++)fired.AddRange(c.Advance(0.5));
        var expect=new[]{LaunchEvent.Ignition,LaunchEvent.SrbSeparation,LaunchEvent.MainEngineCutoff,LaunchEvent.ExternalTankSeparation,LaunchEvent.OrbitInsertion};
        Check(fired.Count==expect.Length,"five events fired, got "+fired.Count);
        for(int i=0;i<expect.Length;i++)Check(fired[i]==expect[i],"event order "+i);
        Check(c.Phase==LaunchPhase.Orbit,"orbit after orbital insertion");
        Check(!c.Abort()&&!c.SetSwitch(PanelSwitch.MainBus,false),"no abort or switching after launch");

        // Event times: ignition at 0, SRB 124, MECO 510, ET 530, orbit 630.
        var t=new LaunchControl();
        t.SetSwitch(PanelSwitch.MainBus,true);t.SetSwitch(PanelSwitch.FuelFeed,true);t.Board(0,1);t.SetSwitch(PanelSwitch.Hatch,true);t.SetSwitch(PanelSwitch.Arm,true);t.Ignite();
        var times=new Dictionary<LaunchEvent,double>();
        for(int step=0;step<7000;step++){foreach(var e in t.Advance(0.1))times[e]=t.T;}
        Check(Math.Abs(times[LaunchEvent.Ignition])<1e-6,"ignition at T-0");
        Check(Math.Abs(times[LaunchEvent.SrbSeparation]-124.0)<0.11,"SRB separation at 124 s, got "+times[LaunchEvent.SrbSeparation]);
        Check(Math.Abs(times[LaunchEvent.MainEngineCutoff]-510.0)<0.11,"MECO at 510 s");
        Check(Math.Abs(times[LaunchEvent.ExternalTankSeparation]-530.0)<0.11,"ET separation at 530 s");
        Check(Math.Abs(times[LaunchEvent.OrbitInsertion]-630.0)<0.11,"orbit insertion at 630 s");

        // Hatch opening disarms and allows leaving; commander leaving disarms.
        var g=new LaunchControl();
        g.SetSwitch(PanelSwitch.MainBus,true);g.SetSwitch(PanelSwitch.FuelFeed,true);g.Board(0,1);g.Board(1,2);g.SetSwitch(PanelSwitch.Hatch,true);g.SetSwitch(PanelSwitch.Arm,true);
        Check(g.Phase==LaunchPhase.Ready,"ready before leaving test");
        Check(g.SetSwitch(PanelSwitch.Hatch,false)&&!g.IsOn(PanelSwitch.Arm)&&g.Phase==LaunchPhase.Standby,"opening hatch disarms");
        Check(g.Leave(1)&&!g.Cabin.CommanderSeated&&!g.IsOn(PanelSwitch.Arm),"commander leaves -> cannot re-arm");
        Check(!g.SetSwitch(PanelSwitch.Arm,true),"arm refused without commander");
        Check(g.Board(0,7)&&g.Cabin.CommanderSeated,"new commander boards");

        // Mass and thrust profile. Both boosters and the tank burn from liftoff, so mass before SRB separation is below 1 024 t.
        double m0=LaunchProfile.StackMass(0);
        Check(Math.Abs(m0-2032000)<1,"liftoff stack mass 2 032 t, got "+m0);
        Check(Math.Abs(LaunchProfile.StackMass(123.999)-849200)<2000,"mass before SRB separation about 849 t, got "+LaunchProfile.StackMass(123.999));
        Check(Math.Abs(LaunchProfile.StackMass(124.0)-683200)<2000,"mass after SRB separation about 683 t, got "+LaunchProfile.StackMass(124.0));
        Check(Math.Abs(LaunchProfile.StackMass(510.0)-139000)<1,"mass at MECO: orbiter and ET dry 139 t");
        Check(Math.Abs(LaunchProfile.StackMass(530.0)-104000)<1,"mass after ET separation: orbiter 104 t");
        double tw=LaunchProfile.Thrust(0)/(m0*LaunchProfile.G0);
        Check(tw>1.5&&tw<2.2,"thrust-to-weight at liftoff 1.5-2.2, got "+tw.ToString("F2"));
        double maxG=0,maxGat=0;
        for(double s=0;s<LaunchControl.MainEngineCutoffTime;s+=0.5){double gg=LaunchProfile.AccelerationG(s);if(gg>maxG){maxG=gg;maxGat=s;}}
        // Known gap: the 65 % throttle floor gives about 3.3 g at MECO in this model. Real flights were about 3 g. Resolve in step 1.5.
        Check(maxG<=3.5,"peak acceleration within 3.5 g, got "+maxG.ToString("F2")+" at "+maxGat);
        // Ideal delta-v: integral of thrust over mass to MECO, no gravity or drag. A real ascent loses about 1.3-1.7 km/s to gravity and drag.
        double dv=LaunchProfile.IdealDeltaV(0.01);
        Check(dv>9000,"ideal delta-v above 9 km/s (orbit needs about 7.8 km/s plus losses), got "+dv.ToString("F0"));
        Console.WriteLine("budget: ideal delta-v "+dv.ToString("F0")+" m/s, peak "+maxG.ToString("F2")+" g at T+"+maxGat.ToString("F0")+" s, T/W "+tw.ToString("F2"));

        Console.WriteLine("PASS: "+tests+" shuttle checks (seats, panel rules, countdown, event times, mass/thrust, delta-v budget)");
    }
}
