using System;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
// SurfaceSounds.Classify decides what a footstep, a mining tick and a block break sound like.
// It is engine-free, so the whole frontier material vocabulary is checkable without audio.
static class CheckSurfaceSounds {
 static int n=0;
 static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL: "+m);}
 static int Main(){
  int max=ExpandedContent.MaxBlock;
  // Every known block must classify to a defined kind and never throw.
  for(ushort id=0;id<=max;id++){
   if(!ExpandedContent.Has(id))continue;
   var k=SurfaceSounds.Classify(id);
   A(Enum.IsDefined(typeof(SurfaceSoundKind),k),"block "+id+" has a defined surface sound");
  }
  // The twelve frontier surfaces each map to a deliberate material, not the default stone.
  var expect=new System.Collections.Generic.Dictionary<ushort,SurfaceSoundKind>{
   {SurfaceSounds.CrystaliaPrism,SurfaceSoundKind.Metal},
   {SurfaceSounds.CrystaliaIce,SurfaceSoundKind.Snow},
   {SurfaceSounds.EmberfallSoot,SurfaceSoundKind.Dirt},
   {SurfaceSounds.EmberfallMagmaShore,SurfaceSoundKind.Stone},
   {SurfaceSounds.AquariaKelp,SurfaceSoundKind.Grass},
   {SurfaceSounds.AquariaReef,SurfaceSoundKind.Stone},
   {SurfaceSounds.AquariaRuin,SurfaceSoundKind.Stone},
   {SurfaceSounds.StormreachGlass,SurfaceSoundKind.Metal},
   {SurfaceSounds.StormreachIron,SurfaceSoundKind.Metal},
   {SurfaceSounds.VerdantMycelium,SurfaceSoundKind.Grass},
   {SurfaceSounds.VerdantSporeLoam,SurfaceSoundKind.Grass},
   {SurfaceSounds.NihilityVoidShard,SurfaceSoundKind.Metal}};
  foreach(var kv in expect){
   A(ExpandedContent.Has(kv.Key),"frontier block "+kv.Key+" exists in the content table");
   A(SurfaceSounds.Classify(kv.Key)==kv.Value,"block "+kv.Key+" sounds like "+kv.Value);
  }
  // The six frontier worlds must not all sound the same underfoot.
  var kinds=new System.Collections.Generic.HashSet<SurfaceSoundKind>();
  foreach(var kv in expect)kinds.Add(kv.Value);
  A(kinds.Count>=4,"frontier surfaces use at least four distinct materials (got "+kinds.Count+")");

  // Locked legacy mappings must not drift - these are shipped sounds.
  A(SurfaceSounds.Classify(7)==SurfaceSoundKind.Water&&SurfaceSounds.Classify(8)==SurfaceSoundKind.Water,"water stays water");
  A(SurfaceSounds.Classify(3)==SurfaceSoundKind.Grass,"grass stays grass");
  A(SurfaceSounds.Classify(4)==SurfaceSoundKind.Wood,"wood stays wood");
  A(SurfaceSounds.Classify(14)==SurfaceSoundKind.Snow,"snow stays snow");
  A(SurfaceSounds.Classify(2)==SurfaceSoundKind.Dirt,"dirt stays dirt");
  A(SurfaceSounds.Classify(54)==SurfaceSoundKind.Metal&&SurfaceSounds.Classify(69)==SurfaceSoundKind.Metal,"metal band stays metal");
  A(SurfaceSounds.Classify(90)==SurfaceSoundKind.Stone,"unknown ids fall back to stone");
  A(SurfaceSounds.Classify(0)==SurfaceSoundKind.Stone,"air falls back to stone");

  // The Unity wrapper must delegate, not keep a divergent copy.
  var audio=System.IO.File.ReadAllText("Assets/Scripts/Presentation/GameAudio.cs");
  var table=System.IO.File.ReadAllText("Assets/Scripts/Presentation/SurfaceSounds.cs");
  A(audio.Contains("return SurfaceSounds.Classify(blockId);"),"GameAudio.ClassifyBlock delegates to the pure table");
  A(table.Contains("case 44: case 23: case 24:"),"the legacy id list lives in the pure table");
  A(!audio.Contains("case 44: case 23: case 24:"),"no second copy of the legacy id list may survive in GameAudio");
  A(!table.Contains("UnityEngine"),"the pure table must not depend on UnityEngine");

  Console.WriteLine("PASS: "+n+" surface-sound assertions across the expanded block table, "+kinds.Count+" frontier materials.");
  return 0;
 }
}
