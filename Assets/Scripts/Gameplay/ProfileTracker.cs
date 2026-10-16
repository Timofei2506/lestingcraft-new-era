using UnityEngine;using LestingCraft.World;
namespace LestingCraft.Gameplay
{
 public sealed class ProfileTracker:MonoBehaviour
 {
  SurvivalSession s;float tick,walk,sprint;Vector3 previous;bool hasPosition;
  public void Initialize(SurvivalSession session){s=session;}
  public void Add(string key,int value=1,bool maximum=false){if(s&&!s.Creative)s.Game.Accounts?.Record(key,value,maximum);}
  public void Kill(AnimalLoot loot){Add("kills");Add("kill_"+(loot.meat==1?1:loot.meat==3?0:2));}
  void Update()
  {
   if(!s||!s.World.Ready||s.Creative||s.Game.StoryLocked||s.Vitals.dead||!s.Game.Online&&s.Game.Paused&&!s.Game.InventoryOpen){hasPosition=false;return;}
   Vector3 p=s.Player.transform.position;if(hasPosition){float dist=Vector3.Distance(p,previous);if(dist<2){walk+=dist;if(s.Player.Sprinting)sprint+=dist;}}previous=p;hasPosition=true;
   if(walk>=1){int n=(int)walk;Add("walk_m",n);walk-=n;}if(sprint>=1){int n=(int)sprint;Add("sprint_m",n);sprint-=n;}
   tick+=Time.deltaTime;if(tick<1)return;tick-=1;Add("alive_s");if(s.Sheltered)Add("shelter_s");if(s.World.NearCampfire(p))Add("fire_s");
   int x=Mathf.FloorToInt(p.x),y=Mathf.FloorToInt(p.y+.5f),z=Mathf.FloorToInt(p.z);if(s.World.GetBlock(x,y,z)==7)Add("swim_s");int h=TerrainGenerator.Height(x,z,s.World.Seed,s.World.Dimension);if(y<h-5)Add("cave_s");
   var weather=WeatherSample.At(s.World.Seed,(int)s.World.Dimension,s.Data.clock);if(weather.Precipitation>0&&!s.Sheltered)Add("rain_s");double hour=s.Data.clock%1440;if(hour<360||hour>1140)Add("night_s");if(s.Game.Online&&s.Game.PlayerCount>1)Add("coop_s");
   if(s.World.ContentVersion>=5&&s.World.Dimension==Dimension.Lestingia){var c=ExpeditionGenerator.At(x,z,s.World.Seed);if(c.Surface&&y>=c.Base&&y<=c.Base+16)Add("structure_"+c.Kind,1,true);if(c.Mine&&y>=c.MB&&y<=c.MB+5)Add("mine_visit",1,true);}
   var story=s.Game.Story?.State;if(story!=null){for(int stage=2;stage<=Mathf.Min(14,story.stage);stage++)Add("story_"+stage,1,true);if(story.bridgeSolved)Add("bridge",1,true);if(story.runes==3)Add("mill",1,true);if(story.stage>=11)Add("altar",1,true);if(story.pylons==7)Add("pylons",1,true);}
  }
 }
}
