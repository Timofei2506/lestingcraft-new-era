using System;
using LestingCraft.World;
using LestingCraft.Presentation;
// BossVoices: every guardian's hit/death voice, and the guarantee that the six frontier bosses
// do not all grunt like a rock golem.
static class CheckBossVoices {
 static int n=0;
 static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL: "+m);}
 static bool In(float v,float lo,float hi){return v>=lo&&v<=hi;}
 static int Main(){
  var keys=new System.Collections.Generic.HashSet<string>(BossVoices.Keys);
  var voices=new System.Collections.Generic.HashSet<string>();
  for(int i=0;i<DimensionRealm.Count;i++){
   var d=(Dimension)i; var v=BossVoices.For(d);
   A(!string.IsNullOrEmpty(v.Hit)&&!string.IsNullOrEmpty(v.Death),"voice set for "+d);
   A(v.Hit!=v.Death,"hit and death differ for "+d);
   A(In(v.Pitch,.6f,1.4f),"pitch sane for "+d+": "+v.Pitch);
   A(In(v.Gain,.9f,1.3f),"gain sane for "+d);
   voices.Add(v.Hit+"|"+v.Death);
   bool frontier=DimensionRealm.IsDeepFrontier(d);
   A(BossVoices.IsFrontierVoice(d)==frontier,"frontier realms have their own voice: "+d);
   if(frontier)
   {
    // Custom voices must be synthesized by GameAudio.
    A(keys.Contains(v.Hit),"hit clip declared for "+d+": "+v.Hit);
    A(keys.Contains(v.Death),"death clip declared for "+d+": "+v.Death);
   }
   else
   {
    A(v.Hit=="hit_flesh"&&v.Death=="hit_kill","legacy guardians keep the shipped chord: "+d);
   }
  }
  // Six frontier guardians, six distinct voices.
  var frontierVoices=new System.Collections.Generic.HashSet<string>();
  for(int i=DimensionRealm.FirstDeepFrontier;i<DimensionRealm.Count;i++)frontierVoices.Add(BossVoices.For((Dimension)i).Hit);
  A(frontierVoices.Count==6,"six distinct frontier boss voices (got "+frontierVoices.Count+")");
  A(BossVoices.Keys.Length==12,"every frontier voice is a hit+death pair ("+BossVoices.Keys.Length+" clips)");
  foreach(var k in BossVoices.Keys) A(k.StartsWith("boss_"),"frontier clips are namespaced: "+k);

  // Pitch must follow the character: the cinder titan is low, the crystal sovereign is high,
  // and the void guardian is the lowest of all.
  A(BossVoices.For(Dimension.Crystalia).Pitch>BossVoices.For(Dimension.Emberfall).Pitch,"crystal is higher than cinder");
  A(BossVoices.For(Dimension.Nihility).Pitch<BossVoices.For(Dimension.Emberfall).Pitch,"void is lower than cinder");
  A(BossVoices.For(Dimension.Stormreach).Gain>BossVoices.For(Dimension.Crystalia).Gain,"the storm hits louder than glass");

  // The game must actually route boss hits through the table, not through the generic mob sound.
  var game=System.IO.File.ReadAllText("Assets/Scripts/Runtime/PrototypeGame.cs");
  A(game.Contains("GameAudio.PlayBossHit("),"PrototypeGame must call PlayBossHit for world bosses");
  var audio=System.IO.File.ReadAllText("Assets/Scripts/Presentation/GameAudio.cs");
  foreach(var k in BossVoices.Keys) A(audio.Contains("clips[\""+k+"\"]"),"GameAudio must synthesize "+k);
  Console.WriteLine("PASS: "+n+" boss-voice assertions across "+DimensionRealm.Count+" realms, "+voices.Count+" distinct voices.");
  return 0;
 }
}
