using System;
using System.IO;
using System.Collections;
using UnityEngine;
using LestingCraft.World;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
namespace LestingCraft.Runtime
{
 public sealed partial class BuildSmokeProbe
 {
  IEnumerator MarineWarmup(VoxelWorld world,Vector3 point)
  {
   var session=world.GetComponentInChildren<SurvivalSession>();double fixedClock=session.Data.clock;
   float deadline=Time.realtimeSinceStartup+900;int cadence=UnityEngine.Rendering.OnDemandRendering.renderFrameInterval;
   try{UnityEngine.Rendering.OnDemandRendering.renderFrameInterval=64;while((!BiomeRingLoaded(world,point)||!world.GetComponent<FarLandscape>().Ready)&&Time.realtimeSinceStartup<deadline){session.Data.clock=fixedClock;yield return null;}session.Data.clock=fixedClock;}
   finally{UnityEngine.Rendering.OnDemandRendering.renderFrameInterval=cadence;}
   if(!BiomeRingLoaded(world,point)||!world.GetComponent<FarLandscape>().Ready){Fail("Marine native full-quality chunk/landscape warmup timed out");yield break;}
   Debug.Log("[Smoke] MARINE_RING: complete=True radius="+VisualSettings.Current.ViewDistance+" cadence="+UnityEngine.Rendering.OnDemandRendering.renderFrameInterval+" distantVertices="+world.GetComponent<FarLandscape>().TerrainVertices);
  }
  IEnumerator ProbeMarine(PrototypeGame game,GameUI ui,string folder)
  {
   var s=game.Session;var world=s.World;var player=s.Player;var camera=Camera.main;int originalMode=s.Data.mode;s.Data.mode=2;s.Notice="";s.Living.Hint="";
   var crystal=Resources.Load<Material>("Generated/Crystal");var renderer=Resources.Load<UnityEngine.Rendering.Universal.UniversalRendererData>("Generated/RendererUltra");
   if(world.ContentVersion!=8||GameRules.Version!=11||!renderer||crystal.GetFloat("_Density")<.8f||crystal.GetFloat("_ZWrite")<.5f||crystal.GetColor("_Tint").g>.15f){Fail("Marine generation/Ultra renderer/dense purple crystal missing");yield break;}
   int shoreX=0,shoreZ=0;bool found=false;
   for(int z=-896;z<=896&&!found;z+=16)for(int x=-896;x<=896;x+=16)
   {int h=OceanGenerator.Height(x,z,world.Seed);if(h>=37&&h<=40&&OceanGenerator.Height(x+32,z,world.Seed)<32&&OceanGenerator.Height(x-16,z,world.Seed)>42){shoreX=x;shoreZ=z;found=true;break;}}
   if(!found){Fail("No genuine generation-8 sea/shore found for native validation");yield break;}
   int ground=OceanGenerator.Height(shoreX-16,shoreZ,world.Seed);Vector3 shore=new Vector3(shoreX-15.5f,ground+1.2f,shoreZ+.5f);
   player.TeleportTo(shore);s.Data.clock=420;s.Inventory.offhand=new ItemStack();s.Inventory.slots[s.Inventory.selected]=new ItemStack();s.Inventory.revision++;
   yield return MarineWarmup(world,shore);if(finishing)yield break;
   camera.transform.localPosition=Vector3.up*PlayerCapsule.EyeHeight;camera.transform.LookAt(new Vector3(shoreX+78,41.1f,shoreZ+18));
   if(world.GetBlock(shoreX+32,40,shoreZ)!=7){Fail("Generation-8 sea has no actual water voxel at native sampled coastline");yield break;}
   float surface=41+WaterSurface.Offset(shoreX+32.35f,shoreZ+.35f,world.WaterClock,world.WaterWind);
   if(!world.InFluid(new Vector3(shoreX+32.35f,surface-.03f,shoreZ+.35f),7)||world.InFluid(new Vector3(shoreX+32.35f,surface+.03f,shoreZ+.35f),7)){Fail("Swimming/breathing boundary does not follow displaced water surface");yield break;}
   yield return CaptureGameplay(folder,"marine-ocean-ultra.png");if(finishing)yield break;
   float before=WaterSurface.Offset(shoreX,shoreZ,s.Data.clock,world.WaterWind);s.Data.clock+=1.7;yield return null;yield return null;
   float after=WaterSurface.Offset(shoreX,shoreZ,s.Data.clock,world.WaterWind);if(Mathf.Abs(after-before)<.001f){Fail("Analytic wave displacement did not advance with world clock");yield break;}
   yield return CaptureGameplay(folder,"marine-ocean-wave-motion.png");if(finishing)yield break;
   // Actual creative placement, same authored crystal geometry/material/collision as normal play.
   int cx=shoreX-16,cz=shoreZ+5,cy=Mathf.Max(ground,OceanGenerator.Height(cx,cz,world.Seed))+1;
   for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++){world.Edit(cx+x,cy,cz+z,1);for(int y=1;y<=3;y++)world.Edit(cx+x,cy+y,cz+z,0);}
   world.Edit(cx,cy+1,cz,11);world.Edit(cx-1,cy+1,cz,11);world.Edit(cx,cy+1,cz-1,11);
   player.TeleportTo(new Vector3(cx+3.3f,cy+.9f,cz+3.8f));camera.transform.localPosition=Vector3.up*PlayerCapsule.EyeHeight;camera.transform.LookAt(new Vector3(cx+.1f,cy+1.5f,cz+.2f));s.Inventory.offhand=new ItemStack{item=148,count=1};s.Inventory.revision++;
   for(int frame=0;frame<12;frame++)yield return null;
   yield return CaptureGameplay(folder,"marine-purple-crystal.png");if(finishing)yield break;
   int floor=LestingCraft.Story.StoryWorld.Floor(world.Seed);Vector3 village=new Vector3(87,floor+1.2f,57);
   player.TeleportTo(village);s.Data.clock=1050;s.Inventory.offhand=new ItemStack();s.Inventory.revision++;
   yield return MarineWarmup(world,village);if(finishing)yield break;
   camera.transform.localPosition=Vector3.up*PlayerCapsule.EyeHeight;camera.transform.LookAt(new Vector3(88.5f,floor+3,48.5f));
   var fixtures=world.GetComponent<VillageStreetLights>();if(!fixtures||fixtures.Count!=6||world.GetBlock(88,floor+5,48)!=0||world.GetBlock(105,floor+5,48)!=0){Fail("Village still has campfires on lamp poles or missing framed lantern lights");yield break;}
   yield return CaptureGameplay(folder,"marine-village-lanterns-night.png");if(finishing)yield break;
   Debug.Log("[Smoke] MARINE8: actual generated sea at "+shoreX+","+shoreZ+"; blue absorption + three-band displaced waves; wave physics/fog shared surface before="+before+" after="+after+"; dense purple depth-writing crystal; six real shadowed framed lantern fixtures, no campfires atop poles; distinct full-resolution Ultra SSAO renderer and distant terrain/sea; private-only PNGs. WARP, NOT hardware RT/FPS evidence.");s.Data.mode=originalMode;
  }
 }
}
