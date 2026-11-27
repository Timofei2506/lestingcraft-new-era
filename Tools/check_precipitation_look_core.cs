using System;
using LestingCraft.World;
using LestingCraft.Presentation;
// Precipitation looks: which realms have their own falling stuff, and that vacuum/sealed realms
// can never render bright water rain no matter what the weather sample says.
static class CheckPrecipitationLook {
 static int n=0;
 static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL: "+m);}
 static bool In(float v,float lo,float hi){return v>=lo&&v<=hi;}
 static int Main(){
  var seen=new System.Collections.Generic.HashSet<PrecipitationStyle>();
  for(int i=0;i<DimensionRealm.Count;i++){
   var d=(Dimension)i; var look=PrecipitationLooks.For(d);
   seen.Add(look.Style);
   A(Enum.IsDefined(typeof(PrecipitationStyle),look.Style),"defined style for "+d);
   A(In(look.Length,.3f,2f),"length sane for "+d);
   A(In(look.Fall,.2f,1.5f),"fall speed sane for "+d);
   A(In(look.Size,.5f,1.4f),"size sane for "+d);
   A(In(look.R,0f,1f)&&In(look.G,0f,1f)&&In(look.B,0f,1f),"tint sane for "+d);
   A(In(look.Alpha,.2f,.8f),"opacity sane for "+d);
   A(look.Billboard==look.Flakes,"renderer mode derived once for "+d);
   // Precipitation only exists where it can reach the ground.
   bool reaches=RealmSoundscape.WeatherReaches(d);
   A(PrecipitationLooks.Visible(d,0f)==false,"zero precipitation is invisible in "+d);
   if(!reaches) A(!PrecipitationLooks.Visible(d,1f),"no precipitation in "+d+" even in a downpour");
   else A(PrecipitationLooks.Visible(d,.5f),"precipitation visible in "+d);
  }
  // Every style must be reachable from the table; no dead enum rows.
  A(seen.Count==6,"six precipitation styles in use (got "+seen.Count+")");
  foreach(PrecipitationStyle s in Enum.GetValues(typeof(PrecipitationStyle)))
   if(s!=PrecipitationStyle.Snow) A(seen.Contains(s),"style "+s+" must be reachable from For()");

  // Soot must read as soot: dark and slow, not bright blue water.
  var ash=PrecipitationLooks.For(Dimension.Emberfall);
  var water=PrecipitationLooks.For(Dimension.Lestingia);
  A(ash.Style==PrecipitationStyle.Ash,"Emberfall falls as ash");
  A(ash.R<.4f&&ash.G<.4f&&ash.B<.4f,"ash is dark");
  A(ash.Fall<water.Fall,"ash falls slower than rain");
  A(ash.Flakes,"ash tumbles like flakes");
  A(ash.Length<water.Length,"ash does not streak");
  // Monsoon must be the opposite: long, fast, bright.
  var monsoon=PrecipitationLooks.For(Dimension.Aquaria);
  A(monsoon.Fall>water.Fall&&monsoon.Length>water.Length,"monsoon is heavier than overworld rain");
  A(!monsoon.Flakes,"monsoon streaks, it does not tumble");

  // Snow resolution: always flakes, always lighter tint, never the ash speed.
  var snowy=PrecipitationLooks.Resolve(Dimension.Emberfall,true);
  A(snowy.Style==PrecipitationStyle.Snow,"snow resolves to flakes even over ash");
  A(snowy.Flakes,"snow tumbles");
  A(snowy.Fall<PrecipitationLooks.For(Dimension.Emberfall).Fall,"snow falls slower than the realm's rain");
  A(snowy.Alpha>PrecipitationLooks.For(Dimension.Emberfall).Alpha,"snow is more opaque than soot drizzle");
  // Rain path is untouched by Resolve.
  var rainy=PrecipitationLooks.Resolve(Dimension.Aquaria,false);
  A(rainy.Style==PrecipitationStyle.Monsoon&&rainy.Fall==monsoon.Fall,"rain keeps the realm look");

  Console.WriteLine("PASS: "+n+" precipitation-look assertions across "+DimensionRealm.Count+" realms, "+seen.Count+" styles.");
  return 0;
 }
}
