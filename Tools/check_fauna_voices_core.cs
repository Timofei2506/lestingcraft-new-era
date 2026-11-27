using System;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
// FaunaVoices: which call a creature species uses, and the guarantee that the six Deep Frontier
// species do not all borrow the grazing-deer grunt.
static class CheckFaunaVoices {
 static int n=0;
 static void A(bool c,string m){n++; if(!c) throw new Exception("FAIL: "+m);}
 static int Main(){
  int count=WildlifeSimulation.SpeciesCount;
  A(count==48,"species count is 48 (got "+count+")");
  var used=new System.Collections.Generic.HashSet<FaunaVoice>();
  for(int s=0;s<count;s++){
   var v=FaunaVoices.For(s);
   A(Enum.IsDefined(typeof(FaunaVoice),v),"species "+s+" has a defined voice");
   used.Add(v);
   var key=FaunaVoices.Key(v);
   A(!string.IsNullOrEmpty(key),"species "+s+" has a clip key");
   if(v!=FaunaVoice.Bird) A(FaunaVoices.IsCreatureCall(key),"species "+s+" maps to a creature call, not an ambience loop: "+key);
  }
  A(used.Count>=8,"fauna vocabulary is varied ("+used.Count+" voices)");

  // Frontier species: six distinct voices, none of them the generic mammal.
  var frontier=new[]{FaunaVoices.PrismCat,FaunaVoices.GeodeLeviathan,FaunaVoices.AshFlicker,
                     FaunaVoices.StormWyrm,FaunaVoices.SporeStag,FaunaVoices.VoidWraith};
  var voices=new System.Collections.Generic.HashSet<FaunaVoice>();
  foreach(int s in frontier){
   A(FaunaVoices.IsFrontier(s),"species "+s+" is a frontier species");
   var v=FaunaVoices.For(s);
   A(v!=FaunaVoice.Mammal,"frontier species "+s+" has its own voice, not the mammal grunt");
   A(v!=FaunaVoice.Reptile&&v!=FaunaVoice.Bird,"frontier species "+s+" is not a legacy voice either");
   voices.Add(v);
   A(FaunaVoices.Key(v).StartsWith("fauna_"),"frontier call is namespaced: "+FaunaVoices.Key(v));
  }
  A(voices.Count==6,"six distinct frontier fauna voices (got "+voices.Count+")");
  A(!FaunaVoices.IsFrontier(41)&&!FaunaVoices.IsFrontier(48),"frontier range is exactly 42..47");
  // Species 48..47 is the last id the simulation can spawn; the frontier six must all be inside it.
  foreach(int s in frontier) A(s < count, "frontier species "+s+" is within SpeciesCount");

  // Legacy fauna keep the shipped calls.
  A(FaunaVoices.For(0)==FaunaVoice.Mammal,"deer stays a mammal");
  A(FaunaVoices.For(11)==FaunaVoice.Reptile,"toad stays a reptile");
  A(FaunaVoices.For(12)==FaunaVoice.Bird,"eagle stays a bird");
  A(FaunaVoices.Key(FaunaVoices.For(12))=="nature_bird","eagle reuses the shipped bird chirp");
  // Ids outside the table must fall back, never throw.
  A(FaunaVoices.For(999)==FaunaVoice.Mammal&&FaunaVoices.For(-5)==FaunaVoice.Mammal,"out-of-range ids fall back to mammal");

  // The game must actually call the voices.
  var manager=System.IO.File.ReadAllText("Assets/Scripts/Gameplay/WildlifeManager.cs");
  A(manager.Contains("GameAudio.PlayFaunaCall("),"WildlifeManager must play ambient fauna calls");
  A(manager.Contains("GameAudio.PlayFaunaThreat("),"WildlifeManager must voice angry animals");
  A(manager.Contains("distance < 26f"),"calls must be distance-gated so the whole map is not audible");
  var audio=System.IO.File.ReadAllText("Assets/Scripts/Presentation/GameAudio.cs");
  foreach(FaunaVoice v in Enum.GetValues(typeof(FaunaVoice))){
   var k=FaunaVoices.Key(v);
   A(audio.Contains("clips[\""+k+"\"]")||k=="nature_bird","GameAudio must synthesize "+k);
  }
  Console.WriteLine("PASS: "+n+" fauna-voice assertions across "+count+" species, "+used.Count+" voices.");
  return 0;
 }
}
