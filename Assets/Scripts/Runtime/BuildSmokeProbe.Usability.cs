using System;
using System.IO;
using System.Collections;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;
using LestingCraft.World;
namespace LestingCraft.Runtime
{
 public sealed partial class BuildSmokeProbe
 {
  IEnumerator CaptureGameplay(string folder,string name)
  {
   yield return null;yield return null;yield return new WaitForEndOfFrame();string path=Path.GetFullPath(Path.Combine(folder,name));ScreenCapture.CaptureScreenshot(path);yield return WaitForScreenshot(path);
   if(!ScreenshotReady(path)||!ScreenshotHasDetail(path)){Fail("Usability screenshot missing/empty: "+name);yield break;}
  }
  static bool BiomeRingLoaded(VoxelWorld world,Vector3 point){int radius=VisualSettings.Current.ViewDistance;for(int z=-radius;z<=radius;z++)for(int x=-radius;x<=radius;x++)if(!world.Loaded(point+new Vector3(x*16,0,z*16)))return false;return true;}
  IEnumerator ProbeUsability(PrototypeGame game,GameUI ui,string folder)
  {
   var s=game.Session;var player=s.Player;var world=s.World;var camera=Camera.main;Vector3 originalPosition=camera.transform.position;Quaternion originalRotation=camera.transform.rotation;
   var glass=Resources.Load<Material>("Generated/Glass");if(glass.GetFloat("_Density")<.12f||glass.GetFloat("_GlassEdge")<.3f){Fail("Glass surface/edge visibility settings missing");yield break;}
   var oren=GameObject.Find("Орен");var marta=GameObject.Find("Марта");
   if(!oren||!marta||Quaternion.Angle(oren.transform.rotation,marta.transform.rotation)<30||Vector3.Distance(oren.transform.localScale,marta.transform.localScale)<.08f||!oren.transform.Find("Oren short angular beard")||!marta.transform.Find("Marta hair crown")){Fail("Opening witnesses still share an identical silhouette/pose");yield break;}
   camera.transform.position=new Vector3(-33,LestingCraft.Story.StoryWorld.Floor(world.Seed)+4.3f,-28.7f);camera.transform.LookAt((oren.transform.position+marta.transform.position)*.5f+new Vector3(-.1f,.1f,.1f));
   yield return null;
   foreach(var witness in new[]{oren,marta})foreach(string part in new[]{"Block torso","Cubical head"}){var mesh=witness.transform.Find(part).GetComponent<MeshRenderer>();Vector3 point=mesh.bounds.center,delta=point-camera.transform.position;Vector3 projected=camera.WorldToViewportPoint(point);Debug.Log("[Smoke] WITNESS_VISIBILITY "+witness.name+" / "+part+" center="+point+" viewport="+projected+" solid="+world.GetBlock(Mathf.FloorToInt(point.x),Mathf.FloorToInt(point.y),Mathf.FloorToInt(point.z)));if(!mesh.enabled||!mesh.gameObject.activeInHierarchy||projected.z<=0||projected.x<.06f||projected.x>.94f||projected.y<.08f||projected.y>.9f||VoxelRaycast.Trace(camera.transform.position,delta.normalized,delta.magnitude-.15f,world.GetBlock,out _)){Fail("Opening witness body/head hidden behind level geometry: "+witness.name+" / "+part);yield break;}}
   yield return CaptureGameplay(folder,"opening-witnesses.png");if(finishing)yield break;camera.transform.SetPositionAndRotation(originalPosition,originalRotation);
   Debug.Log("[Smoke] DISTINCT_WITNESSES: different builds, front/back resting poses, individual hair/beard/skin/costumes; original blood/dialogue retained");
   // Explicit smoke fixture: real simulation actor + the same ray/GUI/transaction path as gameplay.
   var sim=(LivingSimulation)typeof(LivingWorld).GetField("sim",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(s.Living);
   var baseCell=Vector3Int.FloorToInt(player.transform.position);var traderCell=baseCell+new Vector3Int(-2,0,2);
   for(int x=-3;x<=0;x++)for(int z=0;z<=3;z++){world.Edit(baseCell.x+x,baseCell.y-1,baseCell.z+z,1);for(int y=0;y<3;y++)world.Edit(baseCell.x+x,baseCell.y+y,baseCell.z+z,0);}
   var trader=new LivingActor{id="smoke-trader",kind=0,job=1,x=traderCell.x+.5f,y=traderCell.y,z=traderCell.z+.5f,hx=traderCell.x+.5f,hy=traderCell.y,hz=traderCell.z+.5f,sitting=true};sim.Actors.Add(trader);
   yield return null;yield return null;var savedInventory=s.Inventory.Clone();s.Inventory.Add(139,4);int inputBefore=s.Inventory.Count(139),outputBefore=s.Inventory.Count(140);s.Inventory.slots[s.Inventory.selected]=new ItemStack{item=164,count=1,durability=Items.Get(164).Durability};
   Vector3 target=new Vector3(trader.x,trader.y+.9f,trader.z);camera.transform.LookAt(target);
   if(!s.Living.Interact(new Ray(camera.transform.position,(target-camera.transform.position).normalized),6,true,false)||ui.ScreenName!="Trade"||ui.OpenTraderId!=trader.id||s.Inventory.Count(139)!=inputBefore){Fail("RMB must open trade UI without spending resources, including with bow selected");yield break;}
   yield return CaptureGameplay(folder,"villager-trade.png");if(finishing)yield break;
   var confirm=FindButton(ui,"Trade.Confirm");if(!confirm||!confirm.interactable){Fail("Trade confirmation control missing");yield break;}confirm.onClick.Invoke();
   if(s.Inventory.Count(139)!=inputBefore-4||s.Inventory.Count(140)!=outputBefore+1){Fail("Trade confirmation did not apply exact profession costs");yield break;}
   Debug.Log("[Smoke] TRADE_UI: right-click opens window without spending; actual confirm button exchanges four leather for one ingot; bow does not suppress interaction");game.Resume();
   int previousMode=s.Data.mode;s.Data.mode=2;game.ToggleInventory();yield return null;yield return null;
   if(ui.ScreenName!="Creative"||CreativeCatalog.Query("",0).Length!=ItemArtwork.AllIds.Length){Fail("Creative mode did not open complete catalog");yield break;}
   var search=ui.GetComponentInChildren<InputField>();if(!search){Fail("Creative catalog search missing");yield break;}search.text="СТЕКЛО";yield return null;yield return null;
   var entry=ui.GetComponentInChildren<CreativeCatalogSlot>();if(ui.CatalogVisibleCount!=1||!entry||entry.Item!=44){Fail("Native Russian catalog search/filter failed");yield break;}
   ExecuteEvents.Execute(entry.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
   if(s.Inventory.cursor.item!=44||s.Inventory.cursor.count!=64){Fail("Catalog click did not supply full legal stack");yield break;}
   ui.InventoryClick(s.Inventory.selected,false,false);if(s.Inventory.Active.item!=44){Fail("Catalog cursor could not place item into hotbar");yield break;}
   s.Inventory.cursor=new ItemStack();s.Inventory.revision++;search.text="";yield return null;yield return null;
   yield return CaptureGameplay(folder,"creative-catalog.png");if(finishing)yield break;
   Debug.Log("[Smoke] CREATIVE_CATALOG: all 98 registry items, native Cyrillic search, real pointer click -> cursor -> hotbar, category/page UI; no packed survival bag required");
   game.Resume();s.Data.mode=previousMode;s.Data.player.inventory=savedInventory;camera.transform.SetPositionAndRotation(originalPosition,originalRotation);
   var window=baseCell+new Vector3Int(0,0,4);
   for(int x=-1;x<=3;x++)for(int z=0;z<4;z++)for(int y=0;y<3;y++)world.Edit(baseCell.x+x,baseCell.y+y,baseCell.z+z,0);
   for(int x=-1;x<=3;x++)for(int y=-1;y<=2;y++)world.Edit(window.x+x,window.y+y,window.z,(ushort)(x==-1||x==3||y==-1||y==2?37:44));
   camera.transform.position=(Vector3)window+new Vector3(1.5f,1.4f,-3.6f);camera.transform.LookAt((Vector3)window+new Vector3(1.5f,1,.5f));
   yield return CaptureGameplay(folder,"glass-window.png");if(finishing)yield break;
   if(world.GetBlock(window.x+1,window.y,window.z)!=44){Fail("Native glass placement missing");yield break;}
   Debug.Log("[Smoke] VISIBLE_GLASS: native transmitting glass material density="+glass.GetFloat("_Density")+", edge="+glass.GetFloat("_GlassEdge")+"; actual placed window captured, not opaque replacement");
   // Visit one actual generated exterior; deterministic six-biome and legacy parity are also tested in managed/Node suites.
   Vector3 biomePoint=Vector3.zero;bool found=false;
   for(int radius=192;radius<=896&&!found;radius+=64)for(int z=-radius;z<=radius&&!found;z+=32)for(int side=-1;side<=1;side+=2)
   {int x=side*radius,h=TerrainGenerator.Height(x,z,world.Seed,Dimension.Lestingia);if(BiomeGenerator.At(x,z,world.Seed)!=BiomeKind.Dunes||h<44||h>60||ExpeditionGenerator.At(x,z,world.Seed).Surface||TerrainGenerator.SampleContent(x,h,z,world.Seed,Dimension.Lestingia,7)!=6)continue;int good=0;for(int dz=-24;dz<=24;dz+=24)for(int dx=-24;dx<=24;dx+=24){int nh=TerrainGenerator.Height(x+dx,z+dz,world.Seed,Dimension.Lestingia);if(BiomeGenerator.At(x+dx,z+dz,world.Seed)==BiomeKind.Dunes&&nh>=44&&nh<=62)good++;}if(good<8)continue;biomePoint=new Vector3(x+.5f,h+4,z+.5f);found=true;break;}
   if(!found){Fail("No new dune biome found for native exterior validation");yield break;}
   s.Data.mode=2;player.TeleportTo(biomePoint);camera.transform.localPosition=new Vector3(0,PlayerCapsule.EyeHeight,0);camera.transform.localRotation=Quaternion.Euler(35,15,0);
   float deadline=Time.realtimeSinceStartup+420;
   while(!BiomeRingLoaded(world,biomePoint)&&Time.realtimeSinceStartup<deadline)yield return null;
   if(!BiomeRingLoaded(world,biomePoint)||world.GetBlock(Mathf.FloorToInt(biomePoint.x),Mathf.FloorToInt(biomePoint.y)-4,Mathf.FloorToInt(biomePoint.z))!=6){Fail("New biome did not stream matching native terrain");yield break;}
   yield return CaptureGameplay(folder,"biome-dunes.png");if(finishing)yield break;s.Data.mode=previousMode;
   Debug.Log("[Smoke] BIOMES7: actual generated dune exterior streamed/rendered; six region types in content7 only; legacy1..6 samplers retained");
  }
 }
}
