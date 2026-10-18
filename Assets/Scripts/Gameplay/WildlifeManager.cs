using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Runtime;
using LestingCraft.World;
using LestingCraft.Networking;
using LestingCraft.Presentation;
namespace LestingCraft.Gameplay
{
 [Serializable] public sealed class AnimalDamage {public int part=1;public string id;public long seq;public float amount;}
 public sealed class WildlifeManager:MonoBehaviour
 {
  SurvivalSession session;WildlifeSimulation simulation;readonly Dictionary<string,AnimalState> remote=new Dictionary<string,AnimalState>();readonly Dictionary<string,AnimalVisual> visuals=new Dictionary<string,AnimalVisual>();readonly List<AnimalLoot> pending=new List<AnimalLoot>();float spawnClock,tickClock,attackAt=-10;long damageSequence;
  sealed class QueuedAttack {public int slot,item;public float at;}
  readonly Dictionary<int,QueuedAttack> queuedAttacks=new Dictionary<int,QueuedAttack>();int requestId;
  bool Enabled=>session&&(int)session.World.Dimension==0&&(!session.Game.Online||session.World.ContentVersion>=2);
  public void Initialize(SurvivalSession s){session=s;simulation=new WildlifeSimulation(s.World.Seed,s.Data.wildlife);}
  public void Reject(int request){queuedAttacks.Remove(request);}
  public WildlifeSave Save()=>simulation.Save();
  IEnumerable<AnimalState> States=>session.Game.Online?(IEnumerable<AnimalState>)remote.Values:simulation.Animals;
  public void Apply(NetPacket packet)
  {
   if(packet.type=="animal_hit"&&packet.id==session.Game.LocalPeerId&&queuedAttacks.TryGetValue(packet.requestId,out var attack))
   {queuedAttacks.Remove(packet.requestId);if(!session.Creative&&attack.slot>=0&&attack.slot<session.Inventory.slots.Length&&session.Inventory.slots[attack.slot].item==attack.item){int selected=session.Inventory.selected;session.Inventory.selected=attack.slot;session.Inventory.WearTool();session.Inventory.selected=selected;}}
   if(packet.animals!=null){remote.Clear();foreach(var a in packet.animals)if(a!=null&&a.id!=null&&!a.dead)remote[a.id]=a;}
   if(packet.loot!=null&&(packet.type=="welcome"||packet.id==session.Game.LocalPeerId))foreach(var l in packet.loot)if(l!=null&&l.id!=null&&!pending.Exists(x=>x.id==l.id))pending.Add(l);
   if(packet.damageEvents!=null)Array.Sort(packet.damageEvents,(a,b)=>a.seq.CompareTo(b.seq));if(packet.damageEvents!=null)foreach(var d in packet.damageEvents)if(d.id==session.Game.LocalPeerId&&d.seq>damageSequence){damageSequence=d.seq;session.Impact(d.amount,(BodyPart)Mathf.Clamp(d.part,0,5),-session.Player.transform.forward);}
  }
  bool AddLoot(AnimalLoot l)
  {
   var inv=session.Inventory.Clone();if(!inv.Add(135,l.meat)||!inv.Add(136,l.hide)){session.Notice="Для мяса и шкуры нужно место в инвентаре";return false;}session.Inventory.slots=inv.slots;session.Inventory.revision++;session.Progress.Kill(l);return true;
  }
  public bool Intersect(Ray ray,float limit,out string id,int weaponOverride=0)
  {
   id=null;float reach=Weapons.Get(weaponOverride>0?weaponOverride:session.Inventory.Active.item).Reach;float nearest=Mathf.Min(reach,limit);if(!Enabled)return false;
   foreach(var a in States)if(!a.dead){var center=new Vector3(a.x,a.y+WildlifeSimulation.Height(a.species)*.5f,a.z);if((center-ray.origin).sqrMagnitude>reach*reach)continue;var inverse=Quaternion.Inverse(Quaternion.Euler(0,a.yaw,0));var localRay=new Ray(inverse*(ray.origin-center),inverse*ray.direction);var bounds=new Bounds(Vector3.zero,new Vector3(WildlifeSimulation.Radius(a.species)*2,WildlifeSimulation.Height(a.species),a.species==1?.65f:1.6f));if(bounds.IntersectRay(localRay,out float distance)&&distance>=0&&distance<nearest){id=a.id;nearest=distance;}}
   return id!=null;
  }
  public bool Overlaps(Vector3Int cell)
  {if(!Enabled)return false;var block=new Bounds((Vector3)cell+Vector3.one*.5f,Vector3.one*.99f);foreach(var a in States)if(!a.dead&&block.Intersects(new Bounds(new Vector3(a.x,a.y+WildlifeSimulation.Height(a.species)*.5f,a.z),new Vector3(WildlifeSimulation.Radius(a.species)*2,WildlifeSimulation.Height(a.species),1.3f))))return true;return false;}
  public int NewArrowShot()=>++requestId;
  public void Attack(string id,bool arrow=false,int arrowRequest=0)
  {
   if(!session.CanInteract||!arrow&&Time.time<attackAt||!Enabled)return;attackAt=Time.time+Weapons.Get(session.Inventory.Active.item).Cooldown/1000f/session.Vitals.body.Dexterity;session.ResetMining();int weapon=arrow?164:session.Inventory.Active.item;
   if(session.Game.Online){if(queuedAttacks.Count>=4)return;int request=arrow?arrowRequest:++requestId;queuedAttacks[request]=new QueuedAttack{slot=arrow?-1:session.Inventory.selected,item=weapon,at=Time.unscaledTime};session.Game.SendAnimalHit(id,weapon==102||weapon==103||Weapons.Has(weapon)?weapon:0,request);return;}
   var a=simulation.Animals.Find(q=>q.id==id&&!q.dead);if(a==null)return;
   if(a.health<=WildlifeSimulation.Damage(weapon)){var copy=session.Inventory.Clone();var loot=WildlifeSimulation.Loot(a);if(!session.Creative&&(!copy.Add(135,loot.meat)||!copy.Add(136,loot.hide))){session.Notice="Освободите место для добычи";return;}}
   var p=session.Player.Camera.transform.position;var result=simulation.Hit(id,weapon,p.x,p.y,p.z);if(!session.Creative&&!arrow)session.Inventory.WearTool();if(result!=null){if(!session.Creative)AddLoot(result);session.Checkpoint();}
  }
  bool Solid(int x,int y,int z)=>BlockRegistry.IsSolid(session.World.GetBlock(x,y,z));
  float Ground(float x,float z,float previousY,float radius,float height)
  {
   var w=session.World;int ix=Mathf.FloorToInt(x),iz=Mathf.FloorToInt(z);if(!w.Contains(ix,1,iz))return float.NaN;
   int natural=TerrainGenerator.Height(ix,iz,w.Seed,w.Dimension)+1;
   int hi=previousY<0?natural+1:Mathf.FloorToInt(previousY)+1,lo=previousY<0?natural-2:Mathf.FloorToInt(previousY)-1;
   for(int y=hi;y>=lo;y--)
   {
    bool valid=true,supported=false;for(int k=0;k<5&&valid;k++)
    {int ax=Mathf.FloorToInt(x+(k==0?0:k%2==0?radius:-radius)),az=Mathf.FloorToInt(z+(k==0?0:k<3?-radius:radius));if(!w.Contains(ax,y,az)){valid=false;break;}bool support=Solid(ax,y-1,az);supported|=support;int below=w.GetBlock(ax,y-1,az);if(!support&&(previousY<0||below==7||below==8||!Solid(ax,y-2,az))){valid=false;break;}if(previousY<0&&(below!=3&&below!=2&&below!=14)){valid=false;break;}for(int cy=y;cy<Mathf.CeilToInt(y+height);cy++)if(w.GetBlock(ax,cy,az)!=0){valid=false;break;}}
    if(valid&&supported&&y>41&&y<100)return y;
   }return float.NaN;
  }
  void Update()
  {
   if(!Enabled||!session.World.Ready)return;
   if(!session.Game.Online&&session.Game.Paused&&!session.Game.InventoryOpen)return;
   if(session.Data.campaign.runEnded)return;
   var expired=new List<int>();foreach(var a in queuedAttacks)if(Time.unscaledTime-a.Value.at>3)expired.Add(a.Key);foreach(int id in expired)queuedAttacks.Remove(id);
   var p=session.Player.Camera.transform.position;
   if(!session.Game.Online)
   {var observer=new AnimalObserver{id="local",x=p.x,y=p.y,z=p.z,alive=!session.Vitals.dead};spawnClock-=Time.deltaTime;tickClock+=Time.deltaTime;if(spawnClock<=0){spawnClock=2;if(observer.alive)simulation.Spawn(observer,Ground);}if(tickClock>=.1f){simulation.Step(Mathf.Min(.25f,tickClock),new[]{observer},Ground,(id,d)=>session.Impact(d,BodyPart.LeftLeg,-session.Player.transform.forward));tickClock=0;}}
   else if(session.CanInteract)for(int i=pending.Count-1;i>=0;i--)
   {var l=pending[i];if(Array.IndexOf(session.Data.player.claimedAnimalLoot,l.id)>=0){pending.RemoveAt(i);continue;}if(!AddLoot(l))break;var ids=new List<string>(session.Data.player.claimedAnimalLoot){l.id};session.Data.player.claimedAnimalLoot=ids.ToArray();pending.RemoveAt(i);}
   var visible=new HashSet<string>();int count=0;
   foreach(var a in States)
   {
    if(a.dead||count>=32||Vector3.SqrMagnitude(new Vector3(a.x,a.y,a.z)-p)>2304)continue;count++;visible.Add(a.id);
    if(!visuals.TryGetValue(a.id,out var v)){var obj=new GameObject(a.species==0?"Лесной олень":a.species==1?"Заяц":"Кабан");obj.transform.SetParent(session.World.transform,false);obj.transform.position=new Vector3(a.x,a.y,a.z);v=obj.AddComponent<AnimalVisual>();v.Build(a.species);var collision=obj.AddComponent<BoxCollider>();collision.center=Vector3.up*WildlifeSimulation.Height(a.species)*.5f;collision.size=new Vector3(WildlifeSimulation.Radius(a.species)*2,WildlifeSimulation.Height(a.species),a.species==1?.65f:1.3f);visuals.Add(a.id,v);}
    v.transform.position=Vector3.Lerp(v.transform.position,new Vector3(a.x,a.y,a.z),1-Mathf.Exp(-Time.deltaTime*12));v.transform.rotation=Quaternion.Slerp(v.transform.rotation,Quaternion.Euler(0,a.yaw,0),1-Mathf.Exp(-Time.deltaTime*9));v.Animate(Time.deltaTime);
   }
   var remove=new List<string>();foreach(var kv in visuals)if(!visible.Contains(kv.Key)){if(kv.Value)Destroy(kv.Value.gameObject);remove.Add(kv.Key);}foreach(string id in remove)visuals.Remove(id);
  }
 }
}
