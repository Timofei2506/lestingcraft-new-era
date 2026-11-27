using System; using LestingCraft.World;
// Verifies the frontier gate/arrival contract for all six 2.0 realms:
// a gate always exists within 96 blocks, gates are deterministic per seed,
// arrival pads are solid, and no realm can strand the player.
static class CheckFrontierGates {
 static int n=0; static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL "+m);}
 static ushort SampleGate(int seed,int x,int y,int z,PlanetBody body){
  var col=PlanetTerrain.Column(x,z,seed,body); return PlanetTerrain.Block(x,y,z,seed,col,body);
 }
 static int Main(){
  var realms=new[]{Dimension.Crystalia,Dimension.Emberfall,Dimension.Aquaria,Dimension.Stormreach,Dimension.Verdant,Dimension.Nihility};
  foreach(var r in realms){
   A(DimensionRealm.IsDeepFrontier(r),"frontier flag "+r);
   for(int seed=1;seed<=5;seed++){
    FrontierTerrainCodes.ArrivalPad(r,seed,out int ax,out int az);
    A(Math.Abs(ax)<=2&&az>=-10&&az<=-6,"arrival pad near spawn "+r);
    A(FrontierTerrainCodes.IsReturnGate(r,ax,az,seed),"arrival pad counts as a return gate "+r);
    A(FrontierTerrainCodes.ArrivalPadBlock()!=0,"pad block "+r);
    A(LestingCraft.Gameplay.ExpandedContent.Has(FrontierTerrainCodes.ReturnGateBlock(r)),"gate block is a registered frontier surface "+r);
    // Walking outward, a gate must appear in every sector.
    for(int sx=-3;sx<=3;sx++) for(int sz=-3;sz<=3;sz++){
     int px=sx*192+96, pz=sz*192+96;
     FrontierTerrainCodes.ReturnGate(r,px,pz,seed,out int gx,out int gz);
     A(Math.Abs(gx-px)<=96&&Math.Abs(gz-pz)<=96,"gate within sector "+r+" "+sx+","+sz);
     A(FrontierTerrainCodes.IsReturnGate(r,gx,gz,seed),"gate self-consistent "+r);
     FrontierTerrainCodes.ReturnGate(r,gx,gz,seed,out int hx,out int hz);
     A(hx==gx&&hz==gz,"gate is a fixed point "+r);
    }
   }
  }
  // gates must differ per realm so two realms never share a portal column
  FrontierTerrainCodes.ReturnGate(Dimension.Crystalia,500,500,7,out int cx1,out int cz1);
  FrontierTerrainCodes.ReturnGate(Dimension.Nihility,500,500,7,out int cx2,out int cz2);
  A(cx1!=cx2||cz1!=cz2,"realm gates are distinct");
  // Gate geometry: pillars solid, lintel solid, throat carved to air, standing spot clear.
  foreach(var r in realms){
   var body=(PlanetBody)(int)r; int seed=99;
   FrontierTerrainCodes.ReturnGate(r,0,0,seed,out int gx,out int gz);
   var col=PlanetTerrain.Column(gx,gz,seed,body);
   int baseY=col.Height+1;
   foreach(int dx in new[]{-2,-1,0,1,2}){
    bool carved; ushort b=FrontierTerrainCodes.GateBlock(r,baseY+2,col,seed,gx+dx,gz,out carved);
    if(Math.Abs(dx)==2) A(b!=0&&!carved,"gate pillar is solid "+r);
    else A(carved,"gate throat is open "+r);
   }
   bool carvedTop; A(FrontierTerrainCodes.GateBlock(r,baseY+5,col,seed,gx,gz,out carvedTop)!=0,"gate lintel solid "+r);
   bool carvedBelow; A(FrontierTerrainCodes.GateBlock(r,baseY-1,col,seed,gx,gz,out carvedBelow)==0,"nothing below gate base "+r);
   bool carvedOutside; A(FrontierTerrainCodes.GateBlock(r,baseY+2,col,seed,gx+3,gz,out carvedOutside)==0,"outside gate footprint "+r);
   FrontierTerrainCodes.GateStandingSpot(r,seed,out int sx,out int sz);
   A(Math.Abs(sx-gx)<=2&&sz==gz+2,"standing spot in front of gate "+r);
   // and the world sampler must actually place the gate blocks
   A(SampleGate(seed,gx,baseY+5,gz,body)!=0,"sampled lintel "+r);
   A(SampleGate(seed,gx,baseY+2,gz,body)==0,"sampled throat is air "+r);
  }
  Console.WriteLine("PASS: "+n+" frontier gate checks across 6 realms");
  return 0;
 }
}
