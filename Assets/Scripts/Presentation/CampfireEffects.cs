using UnityEngine;
using UnityEngine.Rendering;
namespace LestingCraft.Presentation
{
 /// <summary>Bounded local flame, rising smoke and fading embers. Deleted with their world fire.</summary>
 public sealed class CampfireEffects:MonoBehaviour
 {
  public ParticleSystem Flame {get;private set;}
  public ParticleSystem Smoke {get;private set;}
  public ParticleSystem Sparks {get;private set;}
  public void Build()
  {
   Flame=Create("Campfire flame",.35f,.6f,.12f,.21f,30,64,"Fire",new Color(1,.35f,.035f,.65f),.18f);
   Smoke=Create("Campfire smoke",2.8f,4.2f,.20f,.38f,7,48,"Smoke",new Color(.32f,.31f,.28f,.38f),.12f);
   Sparks=Create("Campfire sparks",.7f,1.9f,.018f,.045f,10,32,"Fire",new Color(1,.48f,.07f,.9f),.22f);
   var main=Flame.main;main.startSpeed=.48f;
   main=Smoke.main;main.startSpeed=.55f;
   main=Sparks.main;main.startSpeed=new ParticleSystem.MinMaxCurve(.8f,1.7f);main.gravityModifier=.12f;
   var size=Smoke.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.5f),new Keyframe(.6f,2.7f),new Keyframe(1,4.4f)));
   Fade(Flame,new Color(1,.28f,.025f));Fade(Smoke,new Color(.32f,.31f,.28f));Fade(Sparks,new Color(1,.48f,.07f));
   var noise=Smoke.noise;noise.enabled=true;noise.strength=.12f;noise.frequency=.65f;noise.scrollSpeed=.18f;noise.quality=ParticleSystemNoiseQuality.Low;
   Flame.Play();Smoke.Play();Sparks.Play();
  }
  public void SetWind(float x,float z)
  {if(!Smoke)return;var v=Smoke.velocityOverLifetime;v.enabled=true;v.space=ParticleSystemSimulationSpace.World;v.x=x*.35f;v.z=z*.22f;v.y=.25f;}
  ParticleSystem Create(string name,float lifeMin,float lifeMax,float sizeMin,float sizeMax,float rate,int max,string material,Color tint,float radius)
  {
   var obj=new GameObject(name);obj.transform.SetParent(transform,false);var ps=obj.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var m=ps.main;m.loop=true;m.playOnAwake=false;m.simulationSpace=ParticleSystemSimulationSpace.World;m.startLifetime=new ParticleSystem.MinMaxCurve(lifeMin,lifeMax);m.startSize=new ParticleSystem.MinMaxCurve(sizeMin,sizeMax);m.startColor=tint;m.maxParticles=max;m.startRotation=new ParticleSystem.MinMaxCurve(0,Mathf.PI*2);
   var e=ps.emission;e.rateOverTime=rate;var s=ps.shape;s.shapeType=ParticleSystemShapeType.Cone;s.radius=radius;s.angle=14;s.rotation=new Vector3(-90,0,0);
   var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Resources.Load<Material>("Generated/"+material);renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.maxParticleSize=.25f;
   return ps;
  }
  static void Fade(ParticleSystem ps,Color tint)
  {
   var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(tint,0),new GradientColorKey(tint,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.12f),new GradientAlphaKey(.65f,.55f),new GradientAlphaKey(0,1)});
   var c=ps.colorOverLifetime;c.enabled=true;c.color=new ParticleSystem.MinMaxGradient(g);
  }
 }
}
