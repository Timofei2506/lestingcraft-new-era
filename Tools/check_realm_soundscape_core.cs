using System;
using LestingCraft.World;
using LestingCraft.Presentation;
// RealmSoundscape is engine-free: the whole ambience mix is verifiable without audio hardware.
// It also guarantees that every loop/detail key the table asks for is declared in the key lists
// that GameAudio synthesizes (check_art verifies the same list against the C# synth code).
static class CheckRealmSoundscape {
 static int n=0;
 static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL: "+m);}
 static int Main(){
  var loops=new System.Collections.Generic.HashSet<string>(RealmSoundscape.LoopKeys);
  var details=new System.Collections.Generic.HashSet<string>(RealmSoundscape.DetailKeys);

  for(int i=0;i<DimensionRealm.Count;i++){
   var d=(Dimension)i; var p=RealmSoundscape.For(d);
   A(!string.IsNullOrEmpty(p.Primary),"primary loop set for "+d);
   A(loops.Contains(p.Primary),"primary loop is synthesizable for "+d+": "+p.Primary);
   A(p.PrimaryVolume>0f&&p.PrimaryVolume<=1f,"primary volume in range for "+d);
   if(p.HasSecondary)
   {
    A(loops.Contains(p.Secondary),"secondary loop is synthesizable for "+d+": "+p.Secondary);
    A(p.SecondaryVolume>0f&&p.SecondaryVolume<=1f,"secondary volume in range for "+d);
   }
   if(p.HasDetail) A(details.Contains(p.Detail),"detail one-shot is synthesizable for "+d+": "+p.Detail);
   else A(string.IsNullOrEmpty(p.Detail)||p.DetailPeriod<=0f,"detail without period is inert for "+d);
   // A realm must never ask for the same clip twice: two identical layers just double the gain.
   A(!p.HasSecondary||p.Secondary!=p.Primary,"primary and secondary differ for "+d);
  }

  // The six frontier realms must not share the overworld ambience.
  var realms=new[]{Dimension.Crystalia,Dimension.Emberfall,Dimension.Aquaria,Dimension.Stormreach,Dimension.Verdant,Dimension.Nihility};
  var primaries=new System.Collections.Generic.HashSet<string>();
  foreach(var d in realms){
   var p=RealmSoundscape.For(d);
   primaries.Add(p.Primary);
   A(p.Primary!="amb_wind","frontier realm has its own ambience: "+d);
  }
  A(primaries.Count==6,"six distinct frontier ambiences (got "+primaries.Count+")");

  // Storm thunder must follow the visible lightning strike; other realms must not.
  A(RealmSoundscape.For(Dimension.Stormreach).DetailFollowsLightning,"storm thunder follows lightning");
  A(RealmSoundscape.For(Dimension.Stormreach).Detail=="thunder","storm detail is thunder");
  foreach(var d in realms){ if(d!=Dimension.Stormreach) A(!RealmSoundscape.For(d).DetailFollowsLightning,"only the storm ties detail to lightning: "+d); }

  // The strike window agrees with the shader formula and actually fires sometimes.
  int strikes=0; float maxI=0f;
  for(int i=0;i<4000;i++){
   double clock=i*3.0;
   float intensity=RealmSoundscape.StrikeAt(clock,SkyEffects.Seed(Dimension.Stormreach));
   A(intensity>=0f&&intensity<=1f,"intensity in range");
   if(RealmSoundscape.StrikeBegins(clock,Dimension.Stormreach))strikes++;
   if(intensity>maxI)maxI=intensity;
  }
  A(strikes>0,"the storm actually strikes over time ("+strikes+" starts)");
  A(strikes<200,"a strike is an event, not a steady state ("+strikes+" starts in 4000 samples)");
  A(maxI>.9f,"the strike window reaches full intensity");
  // StrikeBegins fires exactly once per flash edge, not for the whole sustained flash.
  int edges=0, held=0;
  for(int i=0;i<6000;i++){
   if(RealmSoundscape.StrikeBegins(i*1.0,Dimension.Stormreach)) edges++;
   if(RealmSoundscape.StrikeAt(i*1.0,SkyEffects.Seed(Dimension.Stormreach))>=RealmSoundscape.StrikeThreshold) held++;
  }
  // One clap per flash cycle (~93 in-game minutes) is the intended cadence: a storm, not a metronome.
  A(edges>=40&&edges<=90,"one clap per flash cycle ("+edges+" in 6000 minutes)");
  A(held>edges,"the flash itself is held over several minutes ("+held+" held vs "+edges+" edges)");
  A(edges>0,"edge trigger still fires ("+edges+")");
  // Pure function of the clock: same input, same output.
  A(RealmSoundscape.StrikeAt(1234.5,2f)==RealmSoundscape.StrikeAt(1234.5,2f),"strike envelope deterministic");
  // Different worlds must not strike in lockstep.
  int mismatch=0;
  for(int i=0;i<2000;i++){bool x=RealmSoundscape.StrikeBegins(i*1.0,Dimension.Stormreach);bool y=RealmSoundscape.StrikeBegins(i*1.0,Dimension.Nihility);if(x!=y)mismatch++;}
  A(mismatch>10&&mismatch<1990,"different realms strike at different times ("+mismatch+" of 2000)");
  A(RealmSoundscape.ThunderLag>.3f&&RealmSoundscape.ThunderLag<2f,"thunder lag is audible but short");

  // Space ambience is a valid profile too.
  var space=RealmSoundscape.Space;
  A(loops.Contains(space.Primary)&&space.Primary=="amb_space","space uses the space loop");

  Console.WriteLine("PASS: "+n+" soundscape assertions across "+DimensionRealm.Count+" realms, "+loops.Count+" loops, "+details.Count+" details.");
  return 0;
 }
}
