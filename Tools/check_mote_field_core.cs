using System;
using LestingCraft.World;
using LestingCraft.Presentation;
// MoteField is the ground-level ambient particle table: engine-free, so every realm's look,
// its weather damping and its glow coupling are verifiable without a renderer.
static class CheckMoteField {
 static int n=0;
 static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL: "+m);}
 static bool In(float v,float lo,float hi){return v>=lo&&v<=hi;}
 static int Main(){
  var kinds=new System.Collections.Generic.HashSet<MoteKind>();
  var frontier=new[]{Dimension.Crystalia,Dimension.Emberfall,Dimension.Aquaria,Dimension.Stormreach,Dimension.Verdant,Dimension.Nihility};
  var frontierLooks=new System.Collections.Generic.HashSet<string>();

  for(int i=0;i<DimensionRealm.Count;i++){
   var d=(Dimension)i; var p=MoteField.For(d);
   A(Enum.IsDefined(typeof(MoteKind),p.Kind),"defined mote kind for "+d);
   if(!p.Active){A(p.Kind==MoteKind.None,"inactive profile has kind None for "+d);continue;}
   A(In(p.Rate,.5f,60f),"rate sane for "+d+": "+p.Rate);
   A(In(p.SizeMin,.002f,.12f)&&p.SizeMax>=p.SizeMin&&p.SizeMax<=.16f,"sizes sane for "+d);
   A(In(p.Vertical,-3f,3f),"vertical drift sane for "+d);
   A(In(p.Lateral,-3f,3f),"lateral drift sane for "+d);
   A(In(p.Box,6f,30f),"spawn box sane for "+d);
   A(In(p.R,.05f,1f)&&In(p.G,.05f,1f)&&In(p.B,.05f,1f),"colour channels sane for "+d);
   A(In(p.Alpha,.1f,1f),"alpha sane for "+d);
   // Motes must actually move: a still mote is a stuck sprite.
   A(Math.Abs(p.Vertical)>0.05f||Math.Abs(p.Lateral)>0.05f,"motes drift for "+d);
   int cap=MoteField.MaxParticles(p);
   A(cap>=8&&cap<=400,"particle budget bounded for "+d+": "+cap);
   kinds.Add(p.Kind);
  }
  A(kinds.Count>=8,"mote vocabulary is varied ("+kinds.Count+" kinds)");

  // Every frontier realm has its own look, and none is inert.
  foreach(var d in frontier){
   var p=MoteField.For(d);
   A(p.Active,"frontier realm has ground motes: "+d);
   frontierLooks.Add(p.Kind+":"+p.R+":"+p.G+":"+p.B+":"+p.Vertical);
  }
  A(frontierLooks.Count==6,"six distinct frontier mote looks (got "+frontierLooks.Count+")");

  // Airless, sealed or vacuum worlds must not have pollen drifting in front of the camera.
  foreach(var d in new[]{Dimension.Moon,Dimension.Mercury,Dimension.Pluto,Dimension.Backrooms,Dimension.Dream})
   A(!MoteField.For(d).Active,"no motes in "+d);

  // Weather damping: a downpour hides the motes, a drizzle does not.
  var verdant=MoteField.For(Dimension.Verdant);
  float calm=MoteField.Resolve(Dimension.Verdant,0f,0f,1f,false).Rate;
  float damp=MoteField.Resolve(Dimension.Verdant,.5f,0f,1f,false).Rate;
  float stormy=MoteField.Resolve(Dimension.Verdant,1f,0f,1f,false).Rate;
  A(Math.Abs(calm-verdant.Rate)<1e-4f,"calm weather keeps full rate");
  A(damp<calm&&damp>0f,"drizzle thins motes but does not erase them");
  A(stormy<damp,"downpour thins them further");
  A(stormy>calm*.15f,"even in a storm some motes survive");
  // Sheltering under a roof restores some of them.
  A(MoteField.Resolve(Dimension.Verdant,1f,0f,1f,true).Rate>stormy,"shelter restores motes in a storm");
  // EmissionRate agrees with Resolve.
  A(Math.Abs(MoteField.EmissionRate(Dimension.Verdant,.3f,.4f,.8f,false)-
             MoteField.Resolve(Dimension.Verdant,.3f,.4f,.8f,false).Rate)<1e-4f,"EmissionRate matches Resolve");
  // Inert realms stay inert whatever the weather.
  A(MoteField.EmissionRate(Dimension.Backrooms,0f,1f,1f,false)==0f,"sealed realm never emits");

  // Wind pushes drift laterally, in the direction the mote already tends towards.
  var calmDrift=MoteField.Resolve(Dimension.Emberfall,0f,0f,1f,false).Lateral;
  var windyDrift=MoteField.Resolve(Dimension.Emberfall,0f,1f,1f,false).Lateral;
  A(windyDrift>calmDrift,"wind stretches lateral drift");
  var vNeg=MoteField.Resolve(Dimension.Aquaria,0f,1f,1f,false).Lateral;
  A(vNeg>MoteField.For(Dimension.Aquaria).Lateral,"negative-drift realm is pushed the same way");

  // Glow coupling: night motes are visibly brighter than noon motes, density unaffected.
  var night=MoteField.Resolve(Dimension.Nihility,0f,0f,1f,false);
  var noon=MoteField.Resolve(Dimension.Nihility,0f,0f,0f,false);
  A(night.Alpha>noon.Alpha*1.5f,"glow-following motes brighten at night");
  A(Math.Abs(night.Rate-noon.Rate)<1e-4f,"glow changes brightness, not density");
  // Non-glow realms ignore the glow value entirely.
  var saltA=MoteField.Resolve(Dimension.Aquaria,0f,0f,1f,false);
  var saltB=MoteField.Resolve(Dimension.Aquaria,0f,0f,0f,false);
  A(saltA.Alpha==saltB.Alpha,"non-glow motes ignore the glow channel");
  // Out-of-range inputs must clamp, not explode.
  var clamped=MoteField.Resolve(Dimension.Emberfall,5f,-3f,9f,false);
  A(In(clamped.Rate,0f,60f)&&In(clamped.Lateral,-3.1f,3.1f)&&In(clamped.Alpha,0f,1f),"inputs clamp safely");

  Console.WriteLine("PASS: "+n+" mote-field assertions across "+DimensionRealm.Count+" realms, "+kinds.Count+" mote kinds.");
  return 0;
 }
}
