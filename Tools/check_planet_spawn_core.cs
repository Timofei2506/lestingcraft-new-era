using System; using LestingCraft.World;
static class CheckSpawn {
 static int a=0; static void A(bool c,string m){a++; if(!c) throw new Exception("FAIL "+m);}
 static int Main(){
  for(int i=0;i<DimensionRealm.Count;i++){
   var d=(Dimension)i; var p=PlanetCatalog.Get(d);
   A(p.Dimension==d,"catalog row "+i);
   A(PlanetSpawn.FloorY(d)>=2,"floor "+i);
   A(PlanetSpawn.CeilingY(d)>PlanetSpawn.FloorY(d),"ceiling>floor "+i);
   for(int g=-40;g<200;g+=7){int y=PlanetSpawn.SpawnY(d,g); A(y>=PlanetSpawn.FloorY(d)&&y<=PlanetSpawn.CeilingY(d),"spawn clamp "+i+" "+g);}
   A(PlanetSpawn.TerminalSpeed(d)>0f,"terminal "+i);
   A(PlanetSpawn.FallDamageScale(d)>=0f,"fall "+i);
   A(!string.IsNullOrEmpty(DimensionRealm.NameRu(d)),"name "+i);
   A(!string.IsNullOrEmpty(DimensionRealm.Code(d)) && DimensionRealm.Code(d).Length==3,"code "+i);
  }
  // Deep Frontier specifics
  A(PlanetCatalog.Get(Dimension.Nihility).Gravity<.15f&&PlanetCatalog.Get(Dimension.Nihility).GravityScale>=.12f,"nihility micro-g");
  A(PlanetSpawn.NeedsSuit(Dimension.Crystalia),"crystalia suit");
  A(!PlanetSpawn.NeedsSuit(Dimension.Verdant),"verdant breathable");
  A(PlanetSpawn.FallDamageScale(Dimension.Nihility)<PlanetSpawn.FallDamageScale(Dimension.Verdant),"nihility soft fall");
  Console.WriteLine("PASS: "+a+" spawn/catalog agreement checks over "+DimensionRealm.Count+" realms");
  return 0;
 }
}
