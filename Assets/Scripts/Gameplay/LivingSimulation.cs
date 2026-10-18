using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.World;
namespace LestingCraft.Gameplay
{
 [Serializable] public sealed class LivingActor {public string id,owner="";public int kind,job;public float x,y,z,yaw,hx,hy,hz,health=40,vy,timer;public bool sitting,working;}
 [Serializable] public sealed class WorldDrop {public string id;public bool blockBody;public int item,count=1,durability;public float x,y,z,vx,vy,vz,age;}
 [Serializable] public sealed class LivingCell {public int x,y,z,block,level;}
 [Serializable] public sealed class LivingSave {public LivingActor[] actors=Array.Empty<LivingActor>();public WorldDrop[] drops=Array.Empty<WorldDrop>();public LivingCell[] cells=Array.Empty<LivingCell>();public string[] sites=Array.Empty<string>();public int serial;}
 public sealed class LivingSimulation
 {
  public readonly List<LivingActor> Actors=new List<LivingActor>();public readonly List<WorldDrop> Drops=new List<WorldDrop>();
  readonly Dictionary<Vector3Int,LivingCell> cells=new Dictionary<Vector3Int,LivingCell>();readonly HashSet<string> sites=new HashSet<string>();readonly Queue<Vector3Int> queue=new Queue<Vector3Int>();readonly HashSet<Vector3Int> queued=new HashSet<Vector3Int>();
  readonly Func<int,int,int,ushort> get;readonly Action<int,int,int,ushort> set;readonly int seed;int serial,fluidPass;float fluidClock;
  static readonly Vector3Int[] D={Vector3Int.down,Vector3Int.right,Vector3Int.left,new Vector3Int(0,0,1),new Vector3Int(0,0,-1),Vector3Int.up};
  public LivingSimulation(int seed,LivingSave save,Func<int,int,int,ushort> get,Action<int,int,int,ushort> set){this.seed=seed;this.get=get;this.set=set;if(save==null)save=new LivingSave();serial=save.serial;if(save.actors!=null)foreach(var a in save.actors)if(a!=null&&Actors.Count<96)Actors.Add(a);if(save.drops!=null)foreach(var d in save.drops)if(d!=null&&Drops.Count<128)Drops.Add(d);if(save.sites!=null)foreach(var s in save.sites)if(s!=null&&sites.Count<256)sites.Add(s);if(save.cells!=null)foreach(var c in save.cells)if(c!=null&&cells.Count<768){var p=new Vector3Int(c.x,c.y,c.z);cells[p]=c;set(c.x,c.y,c.z,(ushort)c.block);Wake(p);}}
  public LivingSave Save()=>new LivingSave{actors=Actors.ToArray(),drops=Drops.ToArray(),cells=new List<LivingCell>(cells.Values).ToArray(),sites=new List<string>(sites).ToArray(),serial=serial};
  public float Height(int x,int y,int z){return cells.TryGetValue(new Vector3Int(x,y,z),out var c)&&c.level>0?Mathf.Max(.125f,1-c.level/8f):1;}
  public void Wake(Vector3Int p){Enqueue(p);foreach(var d in D)Enqueue(p+d);}
  void Enqueue(Vector3Int p){if(p.y<1||p.y>254||queue.Count>=4096||!queued.Add(p))return;queue.Enqueue(p);}
  public void Edited(Vector3Int p){cells.Remove(p);Wake(p);}
  bool Solid(int x,int y,int z)=>BlockRegistry.IsSolid(get(x,y,z));
  bool Clear(float x,float y,float z,float r,float h){for(int ax=Mathf.FloorToInt(x-r);ax<=Mathf.FloorToInt(x+r);ax++)for(int az=Mathf.FloorToInt(z-r);az<=Mathf.FloorToInt(z+r);az++)for(int ay=Mathf.FloorToInt(y+.015f);ay<=Mathf.FloorToInt(y+h-.01f);ay++)if(Solid(ax,ay,az))return false;return true;}
  public void Spawn(float px,float pz)
  {
   if(!sites.Contains("story-village")&&Vector2.Distance(new Vector2(px,pz),new Vector2(96,48))<75){sites.Add("story-village");int y=LestingCraft.Story.StoryWorld.Floor(seed)+1;int[] xx={82,109,82,110,96},zz={36,35,61,62,67};for(int i=0;i<5;i++){float x=xx[i]+.5f,z=zz[i]+.5f;if(Actors.Count<96&&Clear(x,y,z,i==4?.19f:.29f,i==4?.5f:1.8f))Actors.Add(new LivingActor{id="v:story:"+i,kind=i==4?1:0,job=i%3,x=x,y=y,z=z,hx=x,hy=y,hz=z});}}
   int cx=Mathf.FloorToInt(px/192),cz=Mathf.FloorToInt(pz/192);
   for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
   {int a=cx+dx,b=cz+dz;uint hash=TerrainGenerator.Hash(a,0,b,seed^71839);int x=a*192+64+(int)(hash%64),z=b*192+64+(int)((hash>>8)%64);string key=a+":"+b;if(sites.Contains(key)||sites.Count>=256||Actors.Count>90||Vector2.Distance(new Vector2(px,pz),new Vector2(x,z))>92)continue;var col=ExpeditionGenerator.At(x,z,seed);if(!col.Surface||col.Kind!=0)continue;sites.Add(key);int rot=(int)((hash>>26)%4);
    for(int i=0;i<5;i++){int lx=i%2==0?-15:14,lz=i<2?-15:14;if(i==4){lx=6;lz=-5;}int rx=rot==0?lx:rot==1?-lz:rot==2?-lx:lz,rz=rot==0?lz:rot==1?lx:rot==2?-lz:-lx;float fx=x+rx+.5f,fz=z+rz+.5f,fy=col.Base+1;if(!Clear(fx,fy,fz,i==4?.19f:.29f,i==4?.5f:1.8f))continue;Actors.Add(new LivingActor{id="v:"+key+":"+i,kind=i==4?1:0,job=i%3,x=fx,y=fy,z=fz,hx=fx,hy=fy,hz=fz});}
   }
  }
  public WorldDrop Drop(int item,int count,int durability,Vector3 p,Vector3 velocity){if(Drops.Count>=128)return null;var d=new WorldDrop{id="d:"+(++serial),item=item,count=count,durability=durability,x=p.x,y=p.y,z=p.z,vx=velocity.x,vy=velocity.y,vz=velocity.z};Drops.Add(d);return d;}
  public static bool Floats(int item)=>item==4||item==5||item==18||item==120||item==121||item==122||item==136||item==139||item>=110&&item<=114||item==135||item==138;
  void Change(Vector3Int p,int id,int level=0){if(p.y<=0||p.y>=255||Mathf.Abs(p.x)>=32000||Mathf.Abs(p.z)>=32000||LestingCraft.Story.StoryWorld.Protected(p.x,p.z))return;if(!cells.ContainsKey(p)&&cells.Count>=768)return;var c=new LivingCell{x=p.x,y=p.y,z=p.z,block=id,level=level};cells[p]=c;set(p.x,p.y,p.z,(ushort)id);Wake(p);}
  void Fluids()
  {
   fluidPass++;int count=Math.Min(48,queue.Count);for(int i=0;i<count;i++)
   {var p=queue.Dequeue();queued.Remove(p);int id=get(p.x,p.y,p.z);if(id==6||id==32){int below=get(p.x,p.y-1,p.z);if(p.y>1&&(below==0||below==7||below==8)&&cells.Count<765){if(!LestingCraft.Story.StoryWorld.Protected(p.x,p.z)&&Drops.Count<128){var f=Drop(id,1,0,new Vector3(p.x+.5f,p.y,p.z+.5f),Vector3.zero);if(f!=null){f.blockBody=true;Change(p,0);}}}continue;}
    if(id!=7&&id!=8)continue;if(id==8&&fluidPass%3!=0){Enqueue(p);continue;}int limit=id==7?7:3;int level=cells.TryGetValue(p,out var current)?current.level:0;
    bool touched=false;foreach(var dir in D){int n=get(p.x+dir.x,p.y+dir.y,p.z+dir.z);if(n==7&&id==8||n==8&&id==7){if(id==8)Change(p,level==0?31:10);else Change(p+dir,10);touched=true;break;}}if(touched)continue;
    if(level>0){int desired=99,up=get(p.x,p.y+1,p.z);if(up==id)desired=1;foreach(var dir in D){if(dir.y!=0)continue;var q=p+dir;if(get(q.x,q.y,q.z)!=id)continue;int other=cells.TryGetValue(q,out var c)?c.level:0;desired=Math.Min(desired,other+1);}if(desired>limit){Change(p,0);continue;}if(desired!=level){Change(p,id,desired);level=desired;}}
    var down=p+Vector3Int.down;if(p.y>1&&get(down.x,down.y,down.z)==0){Change(down,id,1);Enqueue(p);continue;}
    if(level>=limit)continue;foreach(var dir in D){if(dir.y!=0)continue;var q=p+dir;if(get(q.x,q.y,q.z)==0)Change(q,id,level+1);}
   }
  }
  float Ground(float x,float z,float y,float h){for(int yy=Mathf.FloorToInt(y)+1;yy>=Mathf.FloorToInt(y)-2;yy--)if(Solid(Mathf.FloorToInt(x),yy-1,Mathf.FloorToInt(z))&&Clear(x,yy,z,.28f,h))return yy;return float.NaN;}
  // Bounded A*: walkers can enter doorways instead of steering directly through house walls.
  Vector3 Waypoint(LivingActor a,Vector3 goal)
  {
   var start=new Vector3Int(Mathf.FloorToInt(a.x),Mathf.RoundToInt(a.y),Mathf.FloorToInt(a.z));var open=new List<Vector3Int>{start};var cost=new Dictionary<Vector3Int,int>{{start,0}};var from=new Dictionary<Vector3Int,Vector3Int>();Vector3Int best=start;float bestH=float.MaxValue;float height=a.kind==1?.5f:1.8f;
   for(int k=0;k<256&&open.Count>0;k++){int index=0;float score=float.MaxValue;for(int i=0;i<open.Count;i++){var q=open[i];float s=cost[q]+Mathf.Abs(q.x-goal.x)+Mathf.Abs(q.z-goal.z);if(s<score){score=s;index=i;}}var p=open[index];open.RemoveAt(index);float h=Mathf.Abs(p.x-goal.x)+Mathf.Abs(p.z-goal.z);if(h<bestH){best=p;bestH=h;}if(h<1.2f)break;foreach(var d in D){if(d.y!=0)continue;int x=p.x+d.x,z=p.z+d.z;float gy=Ground(x+.5f,z+.5f,p.y,height);if(float.IsNaN(gy))continue;var q=new Vector3Int(x,(int)gy,z);if(Mathf.Abs(q.x-start.x)>24||Mathf.Abs(q.z-start.z)>24)continue;int n=cost[p]+1;if(cost.TryGetValue(q,out int old)&&old<=n)continue;cost[q]=n;from[q]=p;open.Add(q);}}
   if(best==start)return goal;while(from.TryGetValue(best,out var previous)&&previous!=start)best=previous;return new Vector3(best.x+.5f,best.y,best.z+.5f);
  }
  readonly Dictionary<string,Vector3> waypoints=new Dictionary<string,Vector3>();
  public Action<float,BodyPart> OnImpact;
  public void Step(float dt,double clock,float px,float py,float pz,string owner)
  {
   dt=Mathf.Clamp(dt,0,.1f);fluidClock+=dt;if(fluidClock>=.35f){fluidClock=0;Fluids();}
   for(int i=Drops.Count-1;i>=0;i--){var d=Drops[i];d.age+=dt;if(d.age>600||d.y<0){Drops.RemoveAt(i);continue;}int liquid=get(Mathf.FloorToInt(d.x),Mathf.FloorToInt(d.y),Mathf.FloorToInt(d.z));if(liquid==8&&!d.blockBody){Drops.RemoveAt(i);continue;}bool wet=liquid==7&&!d.blockBody&&(get(Mathf.FloorToInt(d.x),Mathf.FloorToInt(d.y)+1,Mathf.FloorToInt(d.z))==7||d.y-Mathf.Floor(d.y)<Height(Mathf.FloorToInt(d.x),Mathf.FloorToInt(d.y),Mathf.FloorToInt(d.z)));d.vy=Mathf.Clamp(d.vy+dt*(wet?(Floats(d.item)?12:-4):-20),-24,wet?2.3f:12);if(d.blockBody&&d.vy<0&&Mathf.Abs(d.x-px)<.75f&&Mathf.Abs(d.z-pz)<.75f&&d.y>=py+.17f&&d.y+d.vy*dt<=py+.17f){OnImpact?.Invoke(Mathf.Min(65,-d.vy*2),BodyPart.Head);d.blockBody=false;d.y=py+.2f;d.vy=0;}float drag=Mathf.Exp(-dt*(wet?3:.7f));d.vx*=drag;d.vz*=drag;float nx=d.x+d.vx*dt,ny=d.y+d.vy*dt,nz=d.z+d.vz*dt;if(Clear(nx,d.y,nz,d.blockBody?.49f:.13f,d.blockBody?.98f:.26f)){d.x=nx;d.z=nz;}else{d.vx*=-.2f;d.vz*=-.2f;}int segments=Mathf.Max(1,Mathf.CeilToInt(Mathf.Abs(ny-d.y)/.18f));float step=(ny-d.y)/segments;bool landed=false;for(int part=0;part<segments;part++){if(Clear(d.x,d.y+step,d.z,d.blockBody?.49f:.13f,d.blockBody?.98f:.26f))d.y+=step;else{landed=d.vy<0;d.vy=0;d.vx*=.7f;d.vz*=.7f;break;}}if(d.blockBody&&landed){var target=new Vector3Int(Mathf.FloorToInt(d.x),Mathf.FloorToInt(d.y+.01f),Mathf.FloorToInt(d.z));int old=get(target.x,target.y,target.z);if((old==0||old==7||old==8)&&cells.Count<768){Change(target,d.item);Drops.RemoveAt(i);}}}
   float hour=(float)(clock%1200)/50;bool night=hour<5.5f||hour>19;
   foreach(var a in Actors){if(a.kind==1&&a.owner==owner&&!a.sitting&&new Vector2(a.x-px,a.z-pz).sqrMagnitude>576){for(int k=0;k<4;k++){float tx=px+(k%2==0?-2:2),tz=pz+(k<2?-2:2),ty=Ground(tx,tz,py-1.58f,.5f);if(!float.IsNaN(ty)){a.x=tx;a.y=ty;a.z=tz;a.vy=0;break;}}}if(a.health<=0||Mathf.Abs(a.x-px)>96||Mathf.Abs(a.z-pz)>96)continue;float h=a.kind==1?.5f:1.8f,r=a.kind==1?.19f:.29f;a.timer-=dt;Vector3 home=new Vector3(a.hx,a.hy,a.hz),goal=home;
    if(a.kind==1&&a.owner==owner&&!a.sitting){if(Vector2.Distance(new Vector2(a.x,a.z),new Vector2(px,pz))<2)goal=new Vector3(a.x,a.y,a.z);else goal=new Vector3(px,py-1.6f,pz);}
    else if(!night&&!a.sitting){double phase=clock/18+(a.job+1)*2;goal=home+new Vector3(Mathf.Sin((float)phase)*5,0,Mathf.Cos((float)phase*.73f)*5);}
    a.working=a.kind==0&&!night&&Math.Sin(clock*.12+a.job)>.25;
    if(a.working)goal=home;Vector3 waypoint;if(a.timer<=0||!waypoints.TryGetValue(a.id,out waypoint)){waypoint=Waypoint(a,goal);waypoints[a.id]=waypoint;a.timer=.65f;}else waypoint=waypoints[a.id];
    Vector3 velocity=Vector3.ClampMagnitude(new Vector3(waypoint.x-a.x,0,waypoint.z-a.z),1)*(a.kind==1?2.6f:1.05f);if(a.sitting||a.working)velocity=Vector3.zero;
    float nx=a.x+velocity.x*dt,nz=a.z+velocity.z*dt;foreach(var b in Actors)if(b!=a&&b.health>0&&Mathf.Abs(b.y-a.y)<1&&new Vector2(nx-b.x,nz-b.z).sqrMagnitude<(r+(b.kind==1?.19f:.29f))*(r+(b.kind==1?.19f:.29f))){nx=a.x;nz=a.z;break;}
    if(new Vector2(nx-px,nz-pz).sqrMagnitude<(r+.3f)*(r+.3f)&&Mathf.Abs(py-1.6f-a.y)<1.8f){nx=a.x;nz=a.z;}
    if(Clear(nx,a.y,nz,r,h)){a.x=nx;a.z=nz;}else if(Clear(nx,a.y+1,nz,r,h)&&Mathf.Abs(a.vy)<.1f)a.vy=6.8f;
    a.vy=Mathf.Max(-24,a.vy-dt*20);float ny=a.y+a.vy*dt;if(Clear(a.x,ny,a.z,r,h))a.y=ny;else{if(a.vy< -11)a.health=Mathf.Max(0,a.health-(-a.vy-11)*3);if(a.vy<0)a.y=Mathf.Floor(a.y+.01f);a.vy=0;}if(velocity.sqrMagnitude>.01f)a.yaw=Mathf.Atan2(velocity.x,velocity.z)*Mathf.Rad2Deg;
    if(get(Mathf.FloorToInt(a.x),Mathf.FloorToInt(a.y+.1f),Mathf.FloorToInt(a.z))==8)a.health=Mathf.Max(0,a.health-dt*24);
   }
  }
 }
}
