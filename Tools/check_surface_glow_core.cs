using System;
using LestingCraft.World;
using LestingCraft.Presentation;
// SurfaceGlows is engine-free: the whole ground-glow policy is verifiable without Unity.
static class CheckSurfaceGlow {
 static int n=0;
 static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL: "+m);}
 static int Main(){
  // Every glow kind has a unique shader global, and None has none.
  A(SurfaceGlows.GlobalName(SurfaceGlow.None)==null,"None has no global");
  var seen=new System.Collections.Generic.HashSet<string>();
  foreach(var k in SurfaceGlows.All){
   var g=SurfaceGlows.GlobalName(k);
   A(!string.IsNullOrEmpty(g),"global exists for "+k);
   A(g.StartsWith("_LCGlow"),"global follows the _LCGlow prefix: "+g);
   A(seen.Add(g),"global is unique: "+g);
   A(SurfaceGlows.All.Length==7,"exactly seven living surfaces");
  }

  // Profiles are sane and monotone in daylight: glow never increases with sunlight.
  foreach(var k in SurfaceGlows.All){
   var p=SurfaceGlows.For(k);
   A(p.Kind==k,"profile kind round-trips "+k);
   A(p.NightStrength>0f,"night glow positive "+k);
   A(p.DayFactor>=0f&&p.DayFactor<=1f,"day factor in range "+k);
   float prev=float.MaxValue;
   for(int i=0;i<=20;i++){
    float st=p.StrengthAt(i/20f);
    A(st>=0f,"glow non-negative "+k);
    A(st<=p.NightStrength+1e-4f,"glow never exceeds night strength "+k);
    A(st<=prev+1e-4f,"glow does not grow with daylight "+k);
    prev=st;
   }
   A(Math.Abs(p.StrengthAt(0f)-p.NightStrength)<1e-4f,"midnight = full night glow "+k);
   A(Math.Abs(p.StrengthAt(1f)-p.NightStrength*p.DayFactor)<1e-4f,"noon = night*dayFactor "+k);
  }
  A(SurfaceGlows.For(SurfaceGlow.None).StrengthAt(0.5f)==0f,"none never glows");

  // Magma and void read in daylight; crystal bands do not.
  A(SurfaceGlows.For(SurfaceGlow.Magma).StrengthAt(1f)>SurfaceGlows.For(SurfaceGlow.Prism).StrengthAt(1f),"magma outshines prism at noon");
  A(SurfaceGlows.For(SurfaceGlow.Void).StrengthAt(1f)>.8f,"void seams persist in daylight");

  // Realm mapping: every frontier realm glows, legacy realms do not.
  var realms=new[]{Dimension.Crystalia,Dimension.Emberfall,Dimension.Aquaria,Dimension.Stormreach,Dimension.Verdant,Dimension.Nihility};
  foreach(var d in realms){
   var p=SurfaceGlows.ForRealm(d,0f);
   A(p.Kind!=SurfaceGlow.None,"frontier realm glows at night "+d);
   A(p.StrengthAt(0f)>0f,"glow strength positive "+d);
  }
  A(SurfaceGlows.ForRealm(Dimension.Lestingia,0f).Kind==SurfaceGlow.None,"overworld ground does not glow");
  A(SurfaceGlows.ForRealm(Dimension.Mars,0f).Kind==SurfaceGlow.None,"mars ground does not glow");

  // Crystalia carries two surfaces and reports the stronger one for the current light.
  A(SurfaceGlows.ForRealm(Dimension.Crystalia,0f).Kind==SurfaceGlow.Prism,"crystalia picks crystal at night");
  var kinds=new System.Collections.Generic.HashSet<SurfaceGlow>();
  foreach(var d in realms) kinds.Add(SurfaceGlows.ForRealm(d,0f).Kind);
  A(kinds.Count>=5,"frontier realms cover at least five glow kinds (got "+kinds.Count+")");

  // WithDaylight bakes a constant so the runtime can hand the shader one float.
  var baked=SurfaceGlows.For(SurfaceGlow.Mycelium).WithDaylight(.5f);
  A(Math.Abs(baked.StrengthAt(0f)-baked.StrengthAt(1f))<1e-5f,"baked profile is constant");
  A(baked.StrengthAt(0f)>0f&&baked.StrengthAt(0f)<SurfaceGlows.For(SurfaceGlow.Mycelium).NightStrength,"baked value is between day and night");

  Console.WriteLine("PASS: "+n+" surface-glow assertions across "+SurfaceGlows.All.Length+" kinds and "+realms.Length+" realms.");
  return 0;
 }
}
