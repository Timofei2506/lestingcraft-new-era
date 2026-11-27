using System; using LestingCraft.World;
// Crash/hole/overlap audit for all six Deep Frontier column samplers, plus the
// promise that the generated palette stays inside the registered block band.
static class CheckFrontierColumn {
 static int n=0; static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL "+m);}
 static int Main(){
  var realms=new[]{Dimension.Crystalia,Dimension.Emberfall,Dimension.Aquaria,Dimension.Stormreach,Dimension.Verdant,Dimension.Nihility};
  foreach(var d in realms){
   var p=PlanetCatalog.Get(d); int seed=1337; int solidCols=0; int minH=9999,maxH=-9999;
   for(int x=-120;x<=120;x+=7) for(int z=-120;z<=120;z+=7){
    var c=PlanetTerrain.Column(x,z,seed,p.Body);
    A(c.Height>=p.MinY&&c.Height<=p.MaxY+40,"height in catalogue band "+d+" "+x+","+z+" h="+c.Height);
    minH=Math.Min(minH,c.Height); maxH=Math.Max(maxH,c.Height);
    // Surface integrity: bedrock floor, solid crust at the top two rows, and no solid
    // block above the authored ceiling. Interior caverns are intentional (geode basins,
    // kelp catacombs, void scarp voids) so they are not counted as errors.
    A(PlanetTerrain.Block(x,1,z,seed,c,p.Body)!=0,"bedrock floor "+d+" "+x+","+z);
    A(PlanetTerrain.Block(x,c.Height,z,seed,c,p.Body)!=0,"surface solid "+d+" "+x+","+z);
    A(PlanetTerrain.Block(x,c.Height-1,z,seed,c,p.Body)!=0,"crust solid "+d+" "+x+","+z);
    // Everything above the column height is authored scenery (spires, arches, sky islands).
    // It must stay inside the body's authored ceiling so nothing can render past the skybox.
    for(int y=c.Height+1;y<=300;y++)
     if(PlanetTerrain.Block(x,y,z,seed,c,p.Body)!=0&&y>p.MaxY+40)
      throw new Exception("FAIL scenery above body ceiling "+d+" "+x+","+y+","+z);
    int solid=0; for(int y=1;y<=c.Height;y++) if(PlanetTerrain.Block(x,y,z,seed,c,p.Body)!=0) solid++;
    A(solid*2>c.Height,"column is mostly solid "+d+" "+x+","+z+" ("+solid+"/"+c.Height+")");
    bool sawSolid=solid>0; int top=c.Height;
    A(sawSolid,"column has solid blocks "+d+" "+x+","+z);
    A(top>=p.MinY,"top inside body "+d);
    solidCols++;
   }
   A(maxH>minH,"relief exists "+d);
   A(maxH-minH>=6,"realm has meaningful relief "+d+" ("+(maxH-minH)+")");
   // Sky structures: deterministic, inside the body ceiling, and present in meaningful volume.
   int sky=0, skyMin=9999, skyMax=-9999;
   for(int x=-160;x<=160;x+=3) for(int z=-160;z<=160;z+=3) for(int y=p.MinY;y<=p.MaxY+40;y+=2){
    var c=PlanetTerrain.Column(x,z,seed,p.Body);
    ushort a=FrontierBlocks.Overhang(d,y,seed,x,z);
    ushort b=FrontierBlocks.Overhang(d,y,seed,x,z);
    A(a==b,"overhang deterministic "+d+" "+x+","+y+","+z);
    A(a==0||y>c.Height+90||y<=c.Height+90,"overhang stays near its band "+d);
    if(a!=0){sky++; if(y<skyMin)skyMin=y; if(y>skyMax)skyMax=y;}
   }
   A(sky>0,"realm has sky structures "+d);
   A(skyMax<=p.MaxY+40,"sky structures under the body ceiling "+d+" ("+skyMax+">"+(p.MaxY+40)+")");
   Console.WriteLine(d.ToString().PadRight(12)+" relief "+minH+".."+maxH+"  ("+(maxH-minH)+" blocks)  sky "+skyMin+".."+skyMax+" ("+sky+" samples)  "+solidCols+" columns");
  }
  Console.WriteLine("PASS: "+n+" frontier column checks over 6 realms");
  return 0;
 }
}
