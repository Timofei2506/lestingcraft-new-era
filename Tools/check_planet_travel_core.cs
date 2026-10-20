using System;
using LestingCraft.World;
using LestingCraft.Gameplay;

class PlanetTravelCheck
{
    static int checks;
    static void Ok(bool yes,string label){checks++;if(!yes)throw new Exception(label);}
    static void Main()
    {
        foreach(PlanetBody body in Enum.GetValues(typeof(PlanetBody)))
        {
            int min=999,max=0;var surfaces=new System.Collections.Generic.HashSet<PlanetSurface>();
            for(int z=-160;z<160;z+=2)for(int x=-160;x<160;x+=2)
            {
                var c=PlanetTerrain.Column(x,z,7811,body);
                var repeated=PlanetTerrain.Column(x,z,7811,body);
                Ok(c.Height==repeated.Height&&c.Surface==repeated.Surface,"planet column deterministic");
                Ok(c.Height>=12&&c.Height<=105,"bounded planet terrain");
                Ok(PlanetTerrain.Block(c.Height+1,c,body)==0&&PlanetTerrain.Block(0,c,body)==9,"air/bedrock bounds");
                Ok(PlanetTerrain.Block(c.Height,c,body)!=0&&PlanetTerrain.Block(c.Height,c,body)!=7,"walkable dry surface");
                min=Math.Min(min,c.Height);max=Math.Max(max,c.Height);surfaces.Add(c.Surface);
            }
            Ok(max-min>=8,"planet relief is not flat: "+body+" "+(max-min));
            Ok(surfaces.Count>=2,"multiple terrain types: "+body+" "+surfaces.Count);
            bool different=false;
            for(int i=0;i<20;i++)
            {
                var a=PlanetTerrain.Column(i*31-200,i*29-100,7811,body);
                var b=PlanetTerrain.Column(i*31-200,i*29-100,9932,body);
                if(a.Height!=b.Height||a.Surface!=b.Surface)different=true;
            }
            Ok(different,"seed impacts planet");
            Console.WriteLine(body+": height "+min+".."+max+", surface types "+surfaces.Count);
        }
        SpaceVector earth=SpaceMap.Center(SpaceDestination.Earth),moon=SpaceMap.Center(SpaceDestination.Moon);
        Ok(Math.Abs(SpaceVector.Distance(earth,moon)-384400000)<1,"Moon reference distance");
        var star=SpaceMap.Star(230000,-1201,7,487);
        Ok(SpaceVector.Distance(star,SpaceMap.Star(230000,-1201,7,487))==0&&star.X>SpaceMap.LightYear,"stable star far beyond solar system");
        var trip=new SpaceNavigation(earth);
        Ok(!trip.Ignite(new SpaceVector(double.NaN,0,0),1800),"invalid target rejected");
        Ok(!trip.Land()&&trip.Ignite(moon,SpaceNavigation.MoonSeconds),"manual ignition required");
        Ok(!trip.SetAutopilot(true)&&trip.Stage==FlightStage.Ascent,"autopilot not available on launch");
        trip.Step(629,new SpaceVector(0,0,0));
        Ok(trip.Stage==FlightStage.Ascent&&trip.Position.X>earth.X&&trip.Position.X<moon.X,"ascent advances continuously");
        trip.Step(1,new SpaceVector(0,0,0));
        Ok(trip.Stage==FlightStage.Cruise&&trip.SetAutopilot(true),"crew enables cruise autopilot");
        trip.Step(989,new SpaceVector(0,0,0));
        Ok(trip.Stage==FlightStage.Cruise&&!trip.Land(),"cannot land during cruise");
        trip.Step(1,new SpaceVector(0,0,0));
        Ok(trip.Stage==FlightStage.Approach&&trip.Elapsed==1620,"approach starts at minute 27");
        trip.Step(179,new SpaceVector(0,0,0),1);
        Ok(!trip.Land()&&trip.Elapsed==1799,"no landing before 30 real minutes");
        trip.Step(1,new SpaceVector(0,0,0),1);
        Ok(trip.Elapsed==1800&&trip.Stage==FlightStage.LandingReady&&trip.LandingBurn==180&&SpaceVector.Distance(trip.Position,moon)<1,"30 minute arrival, no position jump");
        Ok(trip.Land()&&trip.Stage==FlightStage.Landed,"explicit manual landing");
        var unbraked=new SpaceNavigation(earth);Ok(unbraked.Ignite(moon,1800),"second trip");
        unbraked.Step(1800,new SpaceVector(0,0,0));Ok(!unbraked.Land(),"no automatic landing without burn");
        var manual=new SpaceNavigation(earth);Ok(manual.Ignite(moon,1800),"third trip");
        manual.Step(630,new SpaceVector(0,0,0));manual.Step(300,new SpaceVector(0,0,1));
        Ok(manual.Position.Z>0&&manual.Position.X<moon.X,"manual steering changes continuous trajectory");
        Ok(manual.SetAutopilot(true),"manual pilot can re-engage autopilot");
        manual.Step(870,new SpaceVector(0,0,0),1);
        Ok(SpaceVector.Distance(manual.Position,moon)<1,"guidance corrects course over remaining time");
        Console.WriteLine("PASS: "+checks+" planetary/space-navigation checks. No Unity landing hook is implied.");
    }
}
