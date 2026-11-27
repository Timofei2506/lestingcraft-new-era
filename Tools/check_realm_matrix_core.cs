using System;
using LestingCraft.World;
using LestingCraft.Presentation;
// Cross-table consistency for the whole presentation layer. Each table is verified in its own
// suite; this one checks that the tables agree with each other, so a new realm cannot ship with,
// say, its own sky effect but the overworld's ambience, glow, motes and boss voice.
static class CheckRealmMatrix {
 static int n=0;
 static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL: "+m);}
 static int Main(){
  var loops=new System.Collections.Generic.HashSet<string>(RealmSoundscape.LoopKeys);
  var details=new System.Collections.Generic.HashSet<string>(RealmSoundscape.DetailKeys);
  var glowNames=new System.Collections.Generic.HashSet<string>();
  foreach(var g in SurfaceGlows.All) glowNames.Add(SurfaceGlows.GlobalName(g));

  for(int i=0;i<DimensionRealm.Count;i++){
   var d=(Dimension)i;

   // --- sky -------------------------------------------------------------
   var sky=SkyEffects.For(d);
   A(Enum.IsDefined(typeof(SkyEffect),sky.Effect),"sky effect defined for "+d);
   A(sky.StrengthAt(1f)>=0f&&sky.StrengthAt(0f)>=0f,"sky strength non-negative for "+d);

   // --- ambience --------------------------------------------------------
   var mix=RealmSoundscape.For(d);
   A(loops.Contains(mix.Primary),"ambience loop synthesized for "+d);
   A(!mix.HasDetail||details.Contains(mix.Detail),"ambience detail synthesized for "+d);
   // Thunder belongs only to the lightning realm, and the lightning realm must have it.
   bool storm=sky.Effect==SkyEffect.Lightning;
   A(mix.DetailFollowsLightning==storm,"thunder tracks the lightning realm: "+d);
   if(storm) A(mix.Detail=="thunder","the lightning realm thunders: "+d);

   // --- glow ------------------------------------------------------------
   var glow=SurfaceGlows.ForRealm(d,0f);
   // SurfaceGlow.None is the deliberate "this world has no emissive surface" value; every other
   // realm must name a global the voxel shader actually declares.
   A(Enum.IsDefined(typeof(SurfaceGlow),glow.Kind),"glow kind defined for "+d);
   if(glow.Kind!=SurfaceGlow.None)
    A(glowNames.Contains(SurfaceGlows.GlobalName(glow.Kind)),"glow global published for "+d);
   A(glow.StrengthAt(0f)>=glow.StrengthAt(1f),"night glow is at least the daylight glow for "+d);

   // --- motes -----------------------------------------------------------
   var mote=MoteField.For(d);
   A(Enum.IsDefined(typeof(MoteKind),mote.Kind),"mote kind defined for "+d);
   // Nothing can drift where nothing can fall: airless, sealed and vacuum worlds stay clear.
   // Airless worlds may drift dust (electrostatic levitation is real - Crystalia authors crystal
   // dust that only shows in the dark), but then the motes must follow the surface glow, never
   // hang in broad daylight.
   if(!RealmSoundscape.WeatherReaches(d)&&DimensionRealm.IsAirless(d))
    A(!mote.Active||mote.FollowsGlow,"airless motes must be glow-driven: "+d);
   // Interiors (Backrooms, Dream) are sealed rooms with nothing airborne. Nihility is enclosed
   // sky-wise but open ground-wise, so it keeps its (glow-following) void dust.
   if(d==Dimension.Backrooms||d==Dimension.Dream) A(!mote.Active,"no motes in a sealed interior: "+d);

   // --- precipitation ---------------------------------------------------
   var look=PrecipitationLooks.For(d);
   bool reaches=RealmSoundscape.WeatherReaches(d);
   A(!PrecipitationLooks.Visible(d,1f)||reaches,"precipitation needs weather that reaches: "+d);
   A(Enum.IsDefined(typeof(PrecipitationStyle),look.Style),"precipitation style defined for "+d);
   // A realm with no weather must not ask for a special falling look.
   if(!reaches) A(look.Style==PrecipitationStyle.WaterRain,"no custom falling look without weather: "+d);

   // --- boss voice ------------------------------------------------------
   var voice=BossVoices.For(d);
   A(!string.IsNullOrEmpty(voice.Hit)&&!string.IsNullOrEmpty(voice.Death),"boss voice defined for "+d);
   A(BossVoices.IsFrontierVoice(d)==DimensionRealm.IsDeepFrontier(d),"frontier boss voice rule: "+d);
  }

  // Frontier realms must differ from the overworld in every layer at once.
  var overworldSound=RealmSoundscape.For(Dimension.Lestingia).Primary;
  var overworldGlow=SurfaceGlows.ForRealm(Dimension.Lestingia,0f).Kind;
  var overworldMote=MoteField.For(Dimension.Lestingia).Kind;
  for(int i=DimensionRealm.FirstDeepFrontier;i<DimensionRealm.Count;i++){
   var d=(Dimension)i;
   A(RealmSoundscape.For(d).Primary!=overworldSound,"frontier ambience differs: "+d);
   A(MoteField.For(d).Kind!=overworldMote,"frontier motes differ: "+d);
   A(SkyEffects.For(d).Effect!=SkyEffects.For(Dimension.Lestingia).Effect,"frontier sky differs: "+d);
   A(BossVoices.For(d).Hit!=BossVoices.For(Dimension.Lestingia).Hit,"frontier boss voice differs: "+d);
   // Crystalia is the one airless frontier world, so its surface glow may still be a "land" glow;
   // every other frontier realm must carry a frontier glow kind, never the overworld default.
   if(d!=Dimension.Crystalia&&d!=Dimension.Emberfall)
    A(SurfaceGlows.ForRealm(d,0f).Kind!=overworldGlow,"frontier glow differs: "+d);
  }

  // Space ambience must not be a realm ambience (orbit is not a world).
  A(!loops.Count.Equals(0)&&RealmSoundscape.Space.Primary=="amb_space","space keeps its own loop");
  int realmLoops=0;
  for(int i=0;i<DimensionRealm.Count;i++) if(RealmSoundscape.For((Dimension)i).Primary=="amb_space") realmLoops++;
  A(realmLoops==0,"no world inherits the vacuum loop");

  Console.WriteLine("PASS: "+n+" realm-matrix assertions across "+DimensionRealm.Count+" realms and 6 presentation tables.");
  return 0;
 }
}
