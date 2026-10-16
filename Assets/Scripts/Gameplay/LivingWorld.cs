using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Presentation;
using LestingCraft.Networking;
namespace LestingCraft.Gameplay
{
 public sealed class LivingWorld:MonoBehaviour
 {
  SurvivalSession s;LivingSimulation sim;LivingSave remote=new LivingSave();readonly Dictionary<Vector3Int,LivingCell> cellCache=new Dictionary<Vector3Int,LivingCell>();readonly Dictionary<string,SettlerVisual> actors=new Dictionary<string,SettlerVisual>();readonly Dictionary<string,GameObject> drops=new Dictionary<string,GameObject>();float tick,spawn,collect,pendingAt;int request;string pendingAction="";public bool Pending{get;private set;}public string Owner="local";
  public string Hint="";
  public bool Enabled=>s&&s.World.ContentVersion>=6&&(int)s.World.Dimension==0;
  public void Initialize(SurvivalSession session){s=session;if(s.World.ContentVersion<6)s.Data.living=new LivingSave();sim=new LivingSimulation(s.World.Seed,s.Data.living,s.World.GetBlock,s.World.Edit);if(s.World.ContentVersion>=6&&s.Data.blocks!=null)for(int i=s.Data.blocks.Length-1;i>=0;i--){var b=s.Data.blocks[i];if(b.realm!=(int)s.World.Dimension)continue;for(int axis=0;axis<7;axis++){int x=b.x+(axis==1?1:axis==2?-1:0),y=b.y+(axis==3?1:axis==4?-1:0),z=b.z+(axis==5?1:axis==6?-1:0);int id=s.World.GetBlock(x,y,z);if(id==6||id==7||id==8||id==32)sim.Wake(new Vector3Int(x,y,z));}}sim.OnImpact=(damage,part)=>s.Impact(damage,part,-s.Player.transform.forward);s.World.FluidHeight=Height;}
  public float Height(int x,int y,int z){if(!Enabled)return 1;if(!s.Game.Online)return sim.Height(x,y,z);return cellCache.TryGetValue(new Vector3Int(x,y,z),out var c)&&c.level>0?Mathf.Max(.125f,1-c.level/8f):1;}
  public LivingSave Save()=>s.Game.Online?remote:sim.Save();
  public void Edited(Vector3Int p){if(Enabled&&!s.Game.Online)sim.Edited(p);}
  public bool CanDrop=>Enabled&&(!s.Game.Online||!Pending);
  public bool SpawnDrop(int item,int count,int durability,Vector3 p,Vector3 velocity){return Enabled&&!s.Game.Online&&sim.Drop(item,count,durability,p,velocity)!=null;}
  public void Apply(NetPacket packet){if(packet.living!=null){remote=packet.living;var previous=new Dictionary<Vector3Int,LivingCell>(cellCache);cellCache.Clear();if(remote.cells!=null)foreach(var c in remote.cells){var key=new Vector3Int(c.x,c.y,c.z);cellCache[key]=c;if(!previous.TryGetValue(key,out var old)||old.block!=c.block||old.level!=c.level)s.World.Edit(c.x,c.y,c.z,(ushort)c.block);}}if(!string.IsNullOrEmpty(packet.ownerTag))Owner=packet.ownerTag;if(packet.type=="living_saved"&&packet.requestId==request){if(packet.save!=null){if(!s.Vitals.dead){s.Data.player.inventory=packet.save.inventory;s.Inventory.Normalize();}else if(s.Mode!=GameMode.Hardcore){s.Data.player.grave=packet.save.inventory.Clone().slots;s.Data.player.graveExtras=packet.save.inventory.ExtraStacks();}}if(pendingAction=="trade"){s.Notice="Обмен подтверждён сервером";s.Progress.Add("trade");}Pending=false;}if(packet.type=="error"&&packet.code=="living_action"){Pending=false;s.Notice=string.IsNullOrEmpty(packet.message)?"Обмен или взаимодействие отклонены сервером":packet.message;}}
  void Send(string action,string id=""){if(Pending)return;s.ResetMining();Pending=true;pendingAction=action;pendingAt=Time.unscaledTime;s.Game.SendStory(new NetPacket{type="living_action",action=action,animal=id,requestId=++request,save=s.Data.player});}
  public void DropActive(){if(!Enabled||Pending||!s.CanInteract)return;var stack=s.Inventory.Active;if(stack.count<=0)return;if(s.Game.Online){Send("drop");return;}float reach=.6f;if(LestingCraft.Runtime.VoxelRaycast.Trace(s.Player.Camera.transform.position,s.Player.Camera.transform.forward,.8f,s.World.GetBlock,out var wall))reach=Mathf.Clamp(wall.Distance-.16f,.02f,.6f);Vector3 p=s.Player.Camera.transform.position+s.Player.Camera.transform.forward*reach;if(SpawnDrop(stack.item,1,stack.durability,p,s.Player.Camera.transform.forward*3+Vector3.up)){stack.count--;if(stack.count<=0){stack.item=0;stack.durability=0;}s.Inventory.revision++;s.Checkpoint();}}
  public bool Interact(Ray ray,float limit,bool use,bool hit)
  {
   if(!Enabled||Pending)return false;LivingActor nearest=null;float best=Mathf.Min(3.5f,limit);var state=Save();foreach(var a in state.actors){if(a.health<=0)continue;var bounds=new Bounds(new Vector3(a.x,a.y+(a.kind==1?.3f:.9f),a.z),a.kind==1?new Vector3(.45f,.65f,.9f):new Vector3(.65f,1.9f,.65f));if(bounds.IntersectRay(ray,out float d)&&d<best){best=d;nearest=a;}}
   if(nearest==null)return false;
   Hint=nearest.kind==1?(nearest.owner==""?"ПКМ · приручить кота сырым мясом":nearest.owner==Owner?"ПКМ · сидеть / следовать":"Чужой домашний кот"):"ПКМ · поговорить / открыть обмен · "+TradeOffer.Profession(nearest.job);
   if(use){if(nearest.kind==0){s.Game.OpenTrade(nearest.id);return true;}if(s.Game.Online){Send(nearest.owner==""?"tame":"sit",nearest.id);return true;}if(nearest.owner==""&&s.Inventory.Count(135)>0){s.Inventory.Remove(135,1);nearest.owner=Owner;nearest.sitting=false;s.Notice="Кот приручён";}else if(nearest.owner==Owner)nearest.sitting=!nearest.sitting;else s.Notice="Нужно сырое мясо";s.Checkpoint();}
   if(hit&&!s.Creative){s.Notice="Жители и домашние коты дружелюбны";}return true;
  }
  public bool TryTrader(string id,out LivingActor actor)
  {
   actor=null;if(!Enabled||!s||s.Vitals.dead||s.Data.campaign.runEnded)return false;
   foreach(var a in Save().actors)if(a.id==id&&a.kind==0&&a.health>0){var eye=s.Player.Camera.transform.position;var delta=new Vector3(a.x,a.y+.9f,a.z)-eye;if(delta.magnitude>3.5f)return false;if(delta.magnitude>.01f&&LestingCraft.Runtime.VoxelRaycast.Trace(eye,delta.normalized,delta.magnitude-.1f,s.World.GetBlock,out var wall))return false;actor=a;return true;}return false;
  }
  public bool TryTrade(string id)
  {if(Pending||!s.CanInteract||!TryTrader(id,out var actor)){s.Notice="Житель недоступен: подойдите ближе";return false;}if(s.Game.Online){Send("trade",id);return true;}bool ok=Trade(actor.job);if(ok)s.Checkpoint();return ok;}
  public bool Trade(int job){if(TradeOffer.Apply(s.Inventory,TradeOffer.ForJob(job),out var inv)){s.Data.player.inventory=inv;s.Notice="Обмен выполнен";s.Progress.Add("trade");return true;}s.Notice="Недостаточно ресурсов или места";return false;}
  public bool Overlaps(Vector3Int p){if(!Enabled)return false;var b=new Bounds((Vector3)p+Vector3.one*.5f,Vector3.one);foreach(var a in Save().actors)if(a.health>0&&b.Intersects(new Bounds(new Vector3(a.x,a.y+.8f,a.z),new Vector3(.6f,a.kind==1?.6f:1.8f,.6f))))return true;return false;}
  void Update()
  {
   if(!Enabled||!s.Game.Online&&s.Game.TradeOpen||!s.World.Ready||s.Game.StoryLocked||s.Data.campaign.runEnded||!s.Game.Online&&s.Game.Paused&&!s.Game.InventoryOpen)return;
   if(Pending&&Time.unscaledTime-pendingAt>8){s.Notice="Ожидание ответа сервера: повторный вход восстановит подтверждённый инвентарь";return;}
   var eye=s.Player.Camera.transform.position;tick+=Time.deltaTime;spawn+=Time.deltaTime;collect+=Time.deltaTime;
   if(!s.Game.Online){if(spawn>2){spawn=0;sim.Spawn(eye.x,eye.z);}int steps=0;while(tick>=.05f&&steps++<5){sim.Step(.05f,s.Data.clock,eye.x,eye.y,eye.z,Owner);tick-=.05f;}}
   var state=Save();if(collect>.45f&&!s.Vitals.dead&&!Pending){collect=0;foreach(var d in state.drops)if(!d.blockBody&&d.age>.6f&&Vector3.Distance(new Vector3(d.x,d.y,d.z),eye-Vector3.up*.8f)<1.8f&&s.Inventory.CanAdd(d.item,d.count)){if(s.Game.Online)Send("pickup",d.id);else{if(s.Inventory.AddStack(new ItemStack{item=d.item,count=d.count,durability=d.durability})){sim.Drops.Remove(d);s.Checkpoint();}}break;}}
   var visible=new HashSet<string>();int count=0;foreach(var a in state.actors){if(a.health<=0||Vector3.SqrMagnitude(new Vector3(a.x,a.y,a.z)-eye)>9216||count++>=32)continue;visible.Add(a.id);if(!actors.TryGetValue(a.id,out var v)){var g=new GameObject(a.kind==1?"Домашний кот":"Житель / профессия "+a.job);g.transform.SetParent(transform);g.transform.position=new Vector3(a.x,a.y,a.z);v=g.AddComponent<SettlerVisual>();v.Build(a.kind,a.job);var collider=g.AddComponent<BoxCollider>();collider.size=a.kind==1?new Vector3(.34f,.55f,.65f):new Vector3(.6f,1.8f,.55f);collider.center=Vector3.up*(a.kind==1?.27f:.9f);actors[a.id]=v;}v.transform.position=Vector3.Lerp(v.transform.position,new Vector3(a.x,a.y,a.z),1-Mathf.Exp(-Time.deltaTime*14));v.transform.rotation=Quaternion.Slerp(v.transform.rotation,Quaternion.Euler(0,a.yaw,0),1-Mathf.Exp(-Time.deltaTime*10));v.Animate(a,Time.deltaTime);}
   var remove=new List<string>();foreach(var pair in actors)if(!visible.Contains(pair.Key)){Destroy(pair.Value.gameObject);remove.Add(pair.Key);}foreach(var id in remove)actors.Remove(id);
   visible.Clear();foreach(var d in state.drops){visible.Add(d.id);if(!drops.TryGetValue(d.id,out var g)){g=new GameObject("Выпавший предмет "+d.item,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(transform);g.GetComponent<MeshFilter>().sharedMesh=VoxelItemModels.MeshFor(d.item);g.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>(d.item==7?"Generated/Water":d.item==44?"Generated/Glass":d.item==11||d.item==145?"Generated/Crystal":"Generated/VoxelObjects");g.transform.localScale=Vector3.one*(d.blockBody?.98f:.26f);drops[d.id]=g;}g.transform.localScale=Vector3.one*(d.blockBody?.98f:.26f);g.transform.position=new Vector3(d.x,d.y+(d.blockBody?.5f:.13f),d.z);if(!d.blockBody)g.transform.Rotate(0,Time.deltaTime*30,0);}remove.Clear();foreach(var pair in drops)if(!visible.Contains(pair.Key)){Destroy(pair.Value);remove.Add(pair.Key);}foreach(var id in remove)drops.Remove(id);
  }
 }
}
