using System;
using UnityEngine;
namespace LestingCraft.Gameplay
{
 [Serializable] public sealed class BodyHealth
 {
  public float[] hp;public float trauma;
  public void Normalize(float legacy){if(hp==null||hp.Length!=6){hp=new float[6];for(int i=0;i<6;i++)hp[i]=Mathf.Clamp(legacy,0,100);}for(int i=0;i<6;i++)hp[i]=float.IsNaN(hp[i])||float.IsInfinity(hp[i])?0:Mathf.Clamp(hp[i],0,100);trauma=Mathf.Clamp(trauma,0,120);}
  // Legacy six-zone save payload is retained for migration only. Gameplay uses SurvivalState.health.
  public bool VitalFailure=>false;
  public float Movement=>1;
  public float Dexterity=>1;
  public bool CanSprint=>true;
  public void Hit(BodyPart part,float amount){Normalize(100);int i=(int)part;hp[i]=Mathf.Max(0,hp[i]-Mathf.Max(0,amount)*(i==0?1.7f:1.4f));trauma=30;}
  public void Systemic(float amount){Normalize(100);for(int i=0;i<6;i++)hp[i]=Mathf.Max(0,hp[i]-amount);trauma=30;}
  public void Recover(float dt,bool nourished){Normalize(100);trauma=Mathf.Max(0,trauma-dt);if(nourished&&trauma<=0)for(int i=0;i<6;i++)hp[i]=Mathf.Min(100,hp[i]+dt*.14f);}
  public bool Treat(){Normalize(100);int at=0;for(int i=1;i<6;i++)if(hp[i]<hp[at])at=i;if(hp[at]>=98)return false;hp[at]=Mathf.Min(100,hp[at]+22);trauma=Mathf.Min(trauma,5);return true;}
 }
}
