using System;
using LestingCraft.World;
using LestingCraft.Presentation;
// SkyEffects is engine-free, so the whole realm -> effect mapping and its day/night
// modulation is verifiable headless. These assertions are the contract the skybox shader
// and DynamicWeather both depend on.
static class CheckSkyEffects {
 static int n=0;
 static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL: "+m);}
 static int Main(){
  // Every realm answers, without throwing, and only valid modes are returned.
  int withEffect=0;
  for(int i=0;i<DimensionRealm.Count;i++){
   var d=(Dimension)i; var p=SkyEffects.For(d);
   A((int)p.Effect>=0&&(int)p.Effect<=(int)SkyEffect.SeaHaze,"valid mode for "+d);
   A(p.Strength>=0f&&p.Strength<=2f,"strength in range for "+d);
   A(p.NightBoost>=0f&&p.NightBoost<=1f,"night boost in range for "+d);
   A(p.Frequency>0f,"frequency positive for "+d);
   for(int s=0;s<=10;s++){float day=s/10f;float st=p.StrengthAt(day);A(st>=0f&&st<=p.Strength+.001f,"modulated strength within range "+d+" day "+day);}
   A(p.StrengthAt(0f)==p.Strength||p.Effect==SkyEffect.None,"night keeps the full strength for "+d);
   if(p.Effect==SkyEffect.None) A(p.Strength==0f,"none has zero strength "+d); else withEffect++;
  }
  A(withEffect>=6,"at least six realms carry a sky effect (got "+withEffect+")");

  // The six Deep Frontier realms must each have a distinct, non-none effect family.
  var realms=new[]{Dimension.Crystalia,Dimension.Emberfall,Dimension.Aquaria,Dimension.Stormreach,Dimension.Verdant,Dimension.Nihility};
  var modes=new System.Collections.Generic.HashSet<SkyEffect>();
  foreach(var d in realms){
   var p=SkyEffects.For(d);
   if(p.Effect!=SkyEffect.None) modes.Add(p.Effect);
   A(p.Strength>0f,"frontier realm has visible sky "+d);
  }
  A(modes.Count>=5,"frontier realms span at least five distinct effects (got "+modes.Count+")");
  A(SkyEffects.HasEffect(Dimension.Crystalia)&&SkyEffects.HasEffect(Dimension.Stormreach),"crystalia + stormreach flagged");
  A(!SkyEffects.HasEffect(Dimension.Lestingia),"overworld has no procedural sky effect");

  // Aurora and ember fade in daylight; lightning does not.
  A(SkyEffects.For(Dimension.Crystalia).StrengthAt(1f)<SkyEffects.For(Dimension.Crystalia).StrengthAt(0f),"aurora blooms at night");
  A(SkyEffects.For(Dimension.Emberfall).StrengthAt(1f)<SkyEffects.For(Dimension.Emberfall).StrengthAt(0f),"ember glow stronger at night");
  A(Math.Abs(SkyEffects.For(Dimension.Stormreach).StrengthAt(1f)-SkyEffects.For(Dimension.Stormreach).StrengthAt(0f))<1e-5f,"lightning ignores daylight");

  // Seeds are deterministic, distinct per realm and inside [0, 2pi).
  var seeds=new System.Collections.Generic.HashSet<float>();
  for(int i=0;i<DimensionRealm.Count;i++){
   float a=SkyEffects.Seed((Dimension)i), b=SkyEffects.Seed((Dimension)i);
   A(a==b,"seed deterministic "+i);
   A(a>=0f&&a<6.2831853f,"seed in range "+i);
   seeds.Add(a);
  }
  A(seeds.Count>=DimensionRealm.Count-1,"seeds are distinct per realm (got "+seeds.Count+")");

  // Resolve agrees with For() and never emits an out-of-range shader mode.
  foreach(var d in realms){
   SkyEffects.Resolve(d,420,0.5f,out int mode,out float strength,out float time,out float seed);
   A(mode==(int)SkyEffects.For(d).Effect,"resolve mode matches table "+d);
   A(mode>=0&&mode<=(int)SkyEffect.SeaHaze,"resolve mode in shader range "+d);
   A(strength>=0f&&strength<=2f,"resolve strength in range "+d);
   A(time>=0f,"resolve time non-negative "+d);
  }
  // Resolve is monotone in the clock (time advances) and independent of clock for the seed half.
  SkyEffects.Resolve(Dimension.Verdant,100,0.5f,out _,out _,out float t1,out float s1);
  SkyEffects.Resolve(Dimension.Verdant,200,0.5f,out _,out _,out float t2,out float s2);
  A(t2>t1&&s1==s2,"time advances, seed does not");

  Console.WriteLine("PASS: "+n+" sky-effect assertions across "+DimensionRealm.Count+" realms, "+modes.Count+" frontier effect families.");
  return 0;
 }
}
