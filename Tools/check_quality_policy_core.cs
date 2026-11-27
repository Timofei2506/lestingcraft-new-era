using System;
using LestingCraft.Presentation;
// VisualQualityPolicy is engine-free by design, so every budget it promises can be checked
// without Unity. These assertions are the contract the renderer relies on.
static class CheckQualityPolicy {
 static int n=0;
 static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL: "+m);}
 static int Main(){
  // Ladder is strictly increasing, starts at Mobile and ends at Extreme.
  var ladder=VisualQualityPolicy.Ladder;
  A(ladder.Length>=6,"ladder has at least six presets");
  A(ladder[0]==VisualQualityPolicy.Mobile,"ladder starts at Mobile");
  A(ladder[ladder.Length-1]==VisualQualityPolicy.Extreme,"ladder ends at Extreme");
  for(int i=1;i<ladder.Length;i++) A(ladder[i]>ladder[i-1],"ladder is strictly increasing at "+i);

  // Every preset yields a monotonic or equal budget on desktop: quality never regresses
  // when the player moves up one step.
  VisualQualityPolicy.Profile prev=VisualQualityPolicy.For(VisualQualityPolicy.Mobile,false);
  for(int i=1;i<ladder.Length;i++){
   var p=VisualQualityPolicy.For(ladder[i],false);
   A(p.DetailQuality>=prev.DetailQuality,"detail monotonic at "+ladder[i]);
   A(p.ShadowResolution>=prev.ShadowResolution,"shadow res monotonic at "+ladder[i]);
   A(p.ShadowCascades>=prev.ShadowCascades,"cascades monotonic at "+ladder[i]);
   A(p.ParallaxSteps>=prev.ParallaxSteps,"parallax monotonic at "+ladder[i]);
   A(p.WaterSteps>=prev.WaterSteps,"water monotonic at "+ladder[i]);
   A(p.GrassRange>=prev.GrassRange,"grass monotonic at "+ladder[i]);
   A(p.CloudSteps>=prev.CloudSteps,"cloud monotonic at "+ladder[i]);
   A(p.FogSteps>=prev.FogSteps,"fog monotonic at "+ladder[i]);
   A(p.DetailRing>=prev.DetailRing,"detail ring monotonic at "+ladder[i]);
   A(p.DetailFadeStart>=prev.DetailFadeStart&&p.DetailFadeEnd>=prev.DetailFadeEnd,"fade monotonic at "+ladder[i]);
   A(p.DetailFadeStart<p.DetailFadeEnd,"fade band is non-empty at "+ladder[i]);
   A(p.ShadowDistanceMultiplier>=prev.ShadowDistanceMultiplier,"shadow distance monotonic at "+ladder[i]);
   prev=p;
  }
  // Mobile caps at Ultra and never asks for more than its budget.
  for(int i=0;i<ladder.Length;i++){
   var m=VisualQualityPolicy.For(ladder[i],true);
   A(m.DetailQuality<=3,"mobile detail capped at Ultra row for "+i);
   A(m.ShadowResolution<=2048,"mobile shadow resolution capped for "+i);
   A(m.GrassRange<=48,"mobile grass budget for "+i);
   A(m.DetailRing<=4,"mobile detail ring for "+i);
   A(m.ShadowCascades<=3,"mobile cascades for "+i);
  }
  // Clamping and migration.
  A(VisualQualityPolicy.ClampPreset(-5,false)==VisualQualityPolicy.Mobile,"clamp low");
  A(VisualQualityPolicy.ClampPreset(99,false)==VisualQualityPolicy.Extreme,"clamp high desktop");
  A(VisualQualityPolicy.ClampPreset(99,true)==VisualQualityPolicy.Ultra,"clamp high mobile");
  A(VisualQualityPolicy.MigratePreset(0,1)==VisualQualityPolicy.High,"legacy 0 -> High");
  A(VisualQualityPolicy.MigratePreset(1,1)==VisualQualityPolicy.Ultra,"legacy 1 -> Ultra");
  A(VisualQualityPolicy.MigratePreset(5,2)==5,"schema 2 ids kept");
  A(VisualQualityPolicy.MigratePreset(8,3)==8,"schema 3 ids kept (clamped later)");
  A(VisualQualityPolicy.SchemaVersion==3,"schema version");
  // View distance and render scale bounds.
  A(VisualQualityPolicy.ClampViewDistance(0,false)==VisualQualityPolicy.MinViewDistance,"view distance floor");
  A(VisualQualityPolicy.ClampViewDistance(99,false)==VisualQualityPolicy.MaxViewDistance(false),"view distance ceiling desktop");
  A(VisualQualityPolicy.ClampViewDistance(99,true)==VisualQualityPolicy.MaxViewDistance(true),"view distance ceiling mobile");
  A(VisualQualityPolicy.MaxViewDistance(true)<VisualQualityPolicy.MaxViewDistance(false),"mobile sees less");
  A(VisualQualityPolicy.ClampRenderScale(.2f,true)>=.65f,"mobile render scale floor");
  A(VisualQualityPolicy.ClampRenderScale(9f,false)<=1.5f,"desktop render scale ceiling");
  // Names are unique and non-empty, so the settings row can never show two identical labels.
  var seen=new System.Collections.Generic.HashSet<string>();
  foreach(int p in ladder){var d=VisualQualityPolicy.DisplayName(p);A(!string.IsNullOrEmpty(d)&&seen.Add(d),"unique display name "+p);A(VisualQualityPolicy.ShortName(p).Length<=8,"short name fits "+p);}
  // Default presets are legal for the platform.
  A(VisualQualityPolicy.ClampPreset(VisualQualityPolicy.DefaultPreset(true),true)==VisualQualityPolicy.DefaultPreset(true),"mobile default legal");
  A(VisualQualityPolicy.ClampPreset(VisualQualityPolicy.DefaultPreset(false),false)==VisualQualityPolicy.DefaultPreset(false),"desktop default legal");
  Console.WriteLine("PASS: "+n+" quality-policy assertions across "+ladder.Length+" presets, desktop + mobile.");
  return 0;
 }
}
