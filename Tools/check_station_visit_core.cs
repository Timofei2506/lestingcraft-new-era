// Standalone Mono check for the exact Unity gameplay sources (no Unity process required).
using System;
using System.Collections.Generic;
using LestingCraft.Gameplay;
class StationVisitCheck
{
    static void Check(bool value,string message){if(!value)throw new Exception(message);}
    static void Main()
    {
        var looks=new HashSet<string>();
        for(int i=0;i<24;i++)
        {
            var a=SettlerAppearance.For("v:story:"+i,i%3),b=SettlerAppearance.For("v:story:"+i,i%3);
            Check(a.Skin==b.Skin&&a.Coat==b.Coat&&a.Hair==b.Hair&&a.Headwear==b.Headwear,"Appearance changed across sessions");
            looks.Add(a.Skin+":"+a.Coat+":"+a.Hair+":"+a.Headwear+":"+a.Detail);
        }
        Check(looks.Count>=12,"Not enough appearance diversity");
        var dock=new StationDocking();var nose=OrbitalStation.Port;
        var forward=new SpaceVector(-1,0,0);var normal=new SpaceVector(1,0,0);var slow=new SpaceVector(0,.1,0);
        Check(!dock.TryLatch(nose+new SpaceVector(3,0,0),slow,forward,normal,true,true),"Distant docking");
        Check(!dock.TryLatch(nose,new SpaceVector(.36,0,0),forward,normal,true,true),"Fast docking");
        Check(!dock.TryLatch(nose,slow,normal,normal,true,true),"Wrong approach direction");
        Check(!dock.TryLatch(nose,slow,forward,normal,true,false),"Unauthorized pilot");
        Check(dock.TryLatch(nose,slow,forward,normal,true,true),"Valid docking rejected");
        Check(!dock.SetHatch(true),"Hatch opened without pressure");dock.Advance(8);
        Check(dock.SetHatch(true),"Pressurized hatch cannot open");
        Check(StationInteriorLayout.CanWalk(-42.2,1,0,-41.8,1,0,true),"Cannot walk across open hatch");
        Check(!StationInteriorLayout.CanWalk(-42.2,1,0,17,1,0,true),"Teleport accepted");
        var inventory=new Inventory();Check(inventory.Add(141,6),"Setup failed");Inventory next;
        Check(!StationAstronauts.TryTrade(dock,false,0,new SpaceVector(5,0,0),inventory,out next),"Unverified crew traded");
        Check(!StationAstronauts.TryTrade(dock,true,0,new SpaceVector(17,0,0),inventory,out next),"Far trader accepted");
        Check(StationAstronauts.TryTrade(dock,true,0,new SpaceVector(5,0,0),inventory,out next),"Valid trade failed");
        Check(inventory.Count(141)==6&&next.Count(141)==0&&next.Count(FuelChain.CompressedAir)==4,"Nonatomic trade");
        Check(!dock.TryUndock(false,true),"Undocked with crew aboard station");
        Check(dock.SetHatch(false)&&dock.TryUndock(true,true),"Sealed undock failed");
        Console.WriteLine("PASS: stable distinct residents; actual approach / pressure / walk / crew / atomic astronaut trade");
    }
}
