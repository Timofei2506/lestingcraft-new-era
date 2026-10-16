using System;
using System.Collections.Generic;
namespace LestingCraft.Gameplay
{
 [Serializable] public sealed class AnimalState
 {
  public string id;public int species,health,wander;public float impulseX,impulseZ,fallSpeed;public float x,y,z,yaw,homeX,homeZ,goalX,goalZ,timer,anger,bite;public bool dead;
  public AnimalState Copy()=>(AnimalState)MemberwiseClone();
 }
 [Serializable] public sealed class WildlifeSave {public AnimalState[] animals=new AnimalState[0];public string[] sites=new string[0];}
 [Serializable] public sealed class AnimalLoot {public string id;public int meat,hide;}
 public struct AnimalObserver {public string id;public float x,y,z;public bool alive;}
 public sealed class WildlifeSimulation
 {
  public const int MaxAnimals=256,MaxSites=4096,ActiveLimit=32;
  public readonly List<AnimalState> Animals=new List<AnimalState>();readonly HashSet<string> sites=new HashSet<string>();readonly int seed;
  // NaN means liquid / blocked / unsupported; the caller validates the entire animal footprint.
  public delegate float Ground(float x,float z,float previousY,float radius,float height);
  public WildlifeSimulation(int seed,WildlifeSave saved=null)
  {
   this.seed=seed;
   if(saved!=null){if(saved.sites!=null)foreach(var s in saved.sites)if(s!=null&&sites.Count<MaxSites)sites.Add(s);
    if(saved.animals!=null)foreach(var a in saved.animals)if(a!=null&&a.id!=null&&a.species>=0&&a.species<3&&Animals.Count<MaxAnimals&&Finite(a.x)&&Finite(a.y)&&Finite(a.z)&&Finite(a.yaw)&&Animals.Find(q=>q.id==a.id)==null){var b=a.Copy();b.health=Math.Max(0,Math.Min(MaxHealth(b.species),b.health));b.dead=b.dead||b.health==0;b.homeX=Finite(b.homeX)?b.homeX:b.x;b.homeZ=Finite(b.homeZ)?b.homeZ:b.z;b.goalX=b.x;b.goalZ=b.z;b.timer=0;b.anger=0;b.bite=0;Animals.Add(b);}}
  }
  static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v)&&Math.Abs(v)<32000;
  public static uint Hash(int x,int z,int seed){unchecked{uint h=(uint)seed^(uint)x*374761393u^(uint)z*2246822519u;h=(h^(h>>13))*1274126177u;return h^(h>>16);}}
  public static int MaxHealth(int s)=>s==0?30:s==1?8:40;
  public static float Height(int s)=>s==0?2.2f:s==1?.8f:.95f;
  public static float Radius(int s)=>s==0?.43f:s==1?.21f:.45f;
  public static int Damage(int weapon)=>Weapons.Get(weapon).Damage;
  public static AnimalLoot Loot(AnimalState a)=>new AnimalLoot{id=a.id,meat=a.species==1?1:a.species==0?3:4,hide=a.species==1?1:2};
  public WildlifeSave Save()=>new WildlifeSave{animals=Animals.ConvertAll(a=>a.Copy()).ToArray(),sites=new List<string>(sites).ToArray()};
  public void Spawn(AnimalObserver p,Ground ground)
  {
   int cx=(int)Math.Floor(p.x/24),cz=(int)Math.Floor(p.z/24);
   for(int dz=-2;dz<=2;dz++)for(int dx=-2;dx<=2;dx++)
   {
    int sx=cx+dx,sz=cz+dz;string key=sx+":"+sz;if(sites.Contains(key)||sites.Count>=MaxSites||Animals.Count>=MaxAnimals)continue;
    uint h=Hash(sx,sz,seed^2581);float ax=sx*24+4+h%16+.5f,az=sz*24+4+(h>>8)%16+.5f;
    float distance=(ax-p.x)*(ax-p.x)+(az-p.z)*(az-p.z);if(distance<144||distance>3600)continue;
    sites.Add(key);if(h%2!=0)continue;int species=(int)((h>>16)%3);
    float ay=ground(ax,az,-1,Radius(species),Height(species));if(float.IsNaN(ay))continue;
    Animals.Add(new AnimalState{id="a:"+key,species=species,health=MaxHealth(species),x=ax,y=ay,z=az,homeX=ax,homeZ=az,goalX=ax,goalZ=az,yaw=h%360,timer=1});
   }
  }
  public void Step(float dt,AnimalObserver[] observers,Ground ground,Action<string,float> damage)
  {
   dt=Math.Max(0,Math.Min(.25f,dt));int active=0;
   foreach(var a in Animals)
   {
    if(a.dead)continue;AnimalObserver nearest=default(AnimalObserver);float best=3600;
    foreach(var p in observers)if(p.alive){float d=(a.x-p.x)*(a.x-p.x)+(a.z-p.z)*(a.z-p.z);if(d<best){best=d;nearest=p;}}
    if(nearest.id==null||active++>=ActiveLimit)continue;
    if(Math.Abs(a.impulseX)+Math.Abs(a.impulseZ)>.01f){float ix=a.x+a.impulseX*dt,iz=a.z+a.impulseZ*dt,iy=ground(ix,iz,a.y,Radius(a.species),Height(a.species));if(!float.IsNaN(iy)){a.x=ix;a.z=iz;a.y=iy;}a.impulseX*=(float)Math.Exp(-dt*8);a.impulseZ*=(float)Math.Exp(-dt*8);}
    float footing=ground(a.x,a.z,a.y,Radius(a.species),Height(a.species));if(float.IsNaN(footing)){a.fallSpeed=Math.Max(-24,a.fallSpeed-dt*20);float remaining=-a.fallSpeed*dt;while(remaining>0){float stepDown=Math.Min(.2f,remaining);a.y=Math.Max(0,a.y-stepDown);remaining-=stepDown;float landed=ground(a.x,a.z,a.y,Radius(a.species),Height(a.species));if(!float.IsNaN(landed)){a.y=landed;if(a.fallSpeed< -11)a.health=Math.Max(0,a.health-(int)((-a.fallSpeed-11)*3));a.fallSpeed=0;break;}}if(a.y<=0||a.health<=0){a.dead=true;a.health=0;}continue;}a.fallSpeed=0;
    a.y=footing;a.anger=Math.Max(0,a.anger-dt);a.bite=Math.Max(0,a.bite-dt);a.timer-=dt;
    bool defending=a.species==2&&a.anger>0&&best<196&&Math.Abs(a.y-nearest.y)<8;bool flee=best<(a.species==1?100:64)&&!defending&&Math.Abs(a.y-nearest.y)<8;
    float speed=a.species==1?.75f:.65f;
    if(defending){a.goalX=nearest.x;a.goalZ=nearest.z;speed=3.1f;if(best<2.4f&&Math.Abs(nearest.y-(a.y+1.5f))<2.3f&&a.bite<=0){damage(nearest.id,8);a.bite=1.5f;}}
    else if(flee){float inv=1/(float)Math.Sqrt(Math.Max(.01f,best));a.goalX=a.x+(a.x-nearest.x)*inv*10;a.goalZ=a.z+(a.z-nearest.z)*inv*10;speed=a.species==1?4.2f:3.8f;}
    else if(a.timer<=0){uint h=Hash((int)(a.x*7+a.yaw),(int)(a.z*7),seed^a.health^unchecked(++a.wander*1597334677));double angle=(h%6283)*.001;a.goalX=a.homeX+(float)Math.Sin(angle)*(2+(h>>8)%9);a.goalZ=a.homeZ+(float)Math.Cos(angle)*(2+(h>>8)%9);a.timer=3+(h>>16)%5;}
    float vx=a.goalX-a.x,vz=a.goalZ-a.z,len=(float)Math.Sqrt(vx*vx+vz*vz);if(len<.25f)continue;
    float step=Math.Min(len,speed*dt),nx=a.x+vx/len*step,nz=a.z+vz/len*step;
    float ny=ground(nx,nz,a.y,Radius(a.species),Height(a.species));
    if(float.IsNaN(ny))for(int turn=0;turn<4;turn++){double angle=(turn%2==0?1:-1)*(turn<2?.8:1.55);float rx=(float)(vx*Math.Cos(angle)-vz*Math.Sin(angle)),rz=(float)(vx*Math.Sin(angle)+vz*Math.Cos(angle));nx=a.x+rx/len*step;nz=a.z+rz/len*step;ny=ground(nx,nz,a.y,Radius(a.species),Height(a.species));if(!float.IsNaN(ny))break;}
    if(float.IsNaN(ny)){a.timer=0;a.goalX=a.x;a.goalZ=a.z;continue;}
    a.yaw=(float)(Math.Atan2(nx-a.x,nz-a.z)*180/Math.PI);a.x=nx;a.z=nz;a.y=ny;
   }
  }
  public AnimalLoot Hit(string id,int weapon,float x,float y,float z)
  {
   var a=Animals.Find(q=>q.id==id&&!q.dead);if(a==null)return null;
   float dy=a.y+Height(a.species)*.5f-y;if((a.x-x)*(a.x-x)+dy*dy+(a.z-z)*(a.z-z)>Weapons.Get(weapon).Reach*Weapons.Get(weapon).Reach)return null;
   float push=(float)Math.Sqrt((a.x-x)*(a.x-x)+(a.z-z)*(a.z-z));if(push>.01f){a.impulseX=(a.x-x)/push*3.5f;a.impulseZ=(a.z-z)/push*3.5f;}a.health=Math.Max(0,a.health-Damage(weapon));a.anger=8;a.timer=0;if(a.health>0)return null;a.dead=true;return Loot(a);
  }
 }
}
