using System.Collections.Generic;using UnityEngine;using UnityEngine.Rendering;using LestingCraft.Runtime;using LestingCraft.Presentation;using LestingCraft.World;
namespace LestingCraft.Story
{
 public sealed class StoryActors:MonoBehaviour
 {
  GuardianSculpture sculpture;VoxelWorld world;float floor;readonly List<Material> costumes=new List<Material>();readonly List<GameObject> roots=new List<GameObject>();GameObject mira,oren,marta,guardian,warning,bridgeGate,vera,ilya;GameObject[] fires=new GameObject[3];GameObject introBeam;GameObject eruption;long rememberedImpact,lastBurst;Vector3 impactPoint;float burstTime=-10;Transform[] weights=new Transform[3];Material material,blood;int oldStage=-1;AudioSource sound;
  public void Initialize(VoxelWorld w)
  {
   world=w;floor=StoryWorld.Floor(w.Seed);material=new Material(Resources.Load<Material>("Generated/VoxelObjects"));blood=new Material(Resources.Load<Material>("Generated/StoryBlood"));blood.SetColor("_BaseColor",new Color(.21f,.027f,.024f));
   oren=Person("Орен",-34,-34,true);marta=Person("Марта",-30,-35,true);vera=Person("Вера",96,50,false);ilya=Person("Илья",108,60,false);mira=Person("Мира",94,52,false);
   foreach(var p in new[]{new Vector2(-34,-34),new Vector2(-30,-35)})for(int i=0;i<13;i++){float x=p.x+Mathf.Sin(i*2.9f)*(.25f+i*.035f),z=p.y+Mathf.Cos(i*3.7f)*(.23f+i*.025f);var decal=StoryMesh.Cube("Dried blood / no wound detail",transform,new Vector3(x,floor+1.015f,z),new Vector3(.12f+(i%3)*.035f,.012f,.13f),1,blood);decal.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;roots.Add(decal);}
   introBeam=StoryMesh.Cube("Collapsed beam pushed aside in the opening",transform,new Vector3(-32,floor+1.65f,-31.2f),new Vector3(3.7f,.30f,.38f),4,material);introBeam.transform.rotation=Quaternion.Euler(0,15,-12);introBeam.SetActive(false);
   Crate(-35,-29);Crate(-28,-33);
   Vector2[] controls={new Vector2(-3,8),new Vector2(-1,12),new Vector2(4,6)};for(int i=0;i<3;i++){var p=controls[i];var root=new GameObject("Counterweight "+new[]{2,3,5}[i]);root.transform.SetParent(transform,false);root.transform.position=new Vector3(p.x,floor+1,p.y);StoryMesh.Cube("Mechanism plinth",root.transform,Vector3.up*.35f,new Vector3(.8f,.7f,.8f),36,material,true);weights[i]=StoryMesh.Cube("Angular lever",root.transform,new Vector3(0,1,0),new Vector3(.18f,.9f,.18f),4,material,true).transform;roots.Add(root);}
   bridgeGate=StoryMesh.Cube("Counterweighted track barrier",transform,new Vector3(3,floor+2.3f,9),new Vector3(.5f,2.6f,5),18,material);bridgeGate.AddComponent<BoxCollider>();roots.Add(bridgeGate);
   foreach(int z in new[]{6,12}){var post=StoryMesh.Cube("Barrier support",transform,new Vector3(3,floor+3,z),new Vector3(.65f,4,.65f),4,material);roots.Add(post);}
   Vector2[] py={new Vector2(168,-112),new Vector2(184,-112),new Vector2(176,-104)};for(int i=0;i<3;i++){fires[i]=new GameObject("Shared brazier flame "+i);fires[i].transform.SetParent(transform,false);fires[i].transform.position=new Vector3(py[i].x+.5f,floor+3.8f,py[i].y+.5f);fires[i].AddComponent<TorchVisual>().Build();fires[i].SetActive(false);}
   Guardian();foreach(var item in roots)item.SetActive(w.Loaded(item.transform.position));guardian.SetActive(false);sound=gameObject.AddComponent<AudioSource>();sound.spatialBlend=0;sound.volume=.24f;
  }
  GameObject Person(string name,float x,float z,bool lying)
  {var root=new GameObject(name);root.transform.SetParent(transform,false);root.transform.position=new Vector3(x,floor+1.03f,z);root.AddComponent<CharacterVisual>().Build();if(lying){root.transform.position+=Vector3.up*.18f;root.transform.rotation=name=="Орен"?Quaternion.Euler(90,-18,0):Quaternion.Euler(-90,24,0);root.transform.localScale=name=="Орен"?new Vector3(1.07f,1.04f,1.03f):new Vector3(.91f,.96f,.94f);root.GetComponent<CharacterVisual>().SetRestPose(name=="Орен"?0:1);root.GetComponent<CharacterVisual>().SetEquipment(new[]{130,104,131,132,0,0,0,0});for(int i=0;i<7;i++)StoryMesh.Cube("Blood on torn clothing",root.transform,new Vector3(-.15f+(i%3)*.11f,1.02f+(i/3)*.085f,name=="Орен"?-.16f:.177f),new Vector3(.10f,.09f,.012f),1,blood,true);StoryMesh.Cube("Bloodied sleeve",root.transform,new Vector3(-.39f,1.1f,.15f),new Vector3(.12f,.19f,.012f),1,blood,true);}else{root.AddComponent<PlayerNameplate>().Initialize(name,world,1.95f);root.GetComponent<CharacterVisual>().SetEquipment(new[]{130,104,131,132,0,0,0,0});}Dress(root,name);roots.Add(root);return root;}
  void Dress(GameObject root,string name)
  {
   var baseRenderer=root.transform.Find("Block torso").GetComponent<MeshRenderer>();var fabric=new Material(baseRenderer.sharedMaterial);fabric.name=name+" / individual textured costume";fabric.SetColor("_BaseColor",name=="Мира"?new Color(1,.65f,.52f):name=="Марта"?new Color(.55f,.72f,.85f):name=="Илья"?new Color(.9f,.7f,.54f):name=="Орен"?new Color(.75f,.76f,.62f):new Color(.9f,.86f,.75f));costumes.Add(fabric);
   foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>()){string part=renderer.name.ToLowerInvariant();if(renderer.sharedMaterial!=blood&&(part.Contains("torso")||part.Contains("sleeve")||part.Contains("hood")||part.Contains("mantle")))renderer.sharedMaterial=fabric;}
   if(name=="Орен"||name=="Марта")
   {
    var skin=new Material(root.transform.Find("Cubical head").GetComponent<MeshRenderer>().sharedMaterial);skin.name=name+" / individual skin";skin.SetColor("_BaseColor",name=="Орен"?new Color(.85f,.74f,.64f):new Color(1,.89f,.83f));costumes.Add(skin);
    foreach(var r in root.GetComponentsInChildren<MeshRenderer>())if(r.name=="Cubical head"||r.name=="Block hand")r.sharedMaterial=skin;
    var hair=new Material(baseRenderer.sharedMaterial);hair.name=name+" / hair";hair.SetColor("_BaseColor",name=="Орен"?new Color(.37f,.33f,.29f):new Color(.35f,.20f,.13f));costumes.Add(hair);
    if(name=="Орен"){BlockGeometry.Part("Oren short angular beard",root.transform,new Vector3(0,1.39f,.224f),new Vector3(.27f,.15f,.04f),hair,7);BlockGeometry.Part("Oren brow",root.transform,new Vector3(0,1.63f,.215f),new Vector3(.28f,.035f,.025f),hair,7);}
    else {foreach(string part in new[]{"Block hood top","Block hood back","Cloth hood"}){var t=root.transform.Find(part);if(t)t.gameObject.SetActive(false);}BlockGeometry.Part("Marta hair crown",root.transform,new Vector3(0,1.74f,-.025f),new Vector3(.43f,.10f,.43f),hair,7);for(int side=-1;side<=1;side+=2)BlockGeometry.Part("Marta short hair side",root.transform,new Vector3(side*.207f,1.56f,-.025f),new Vector3(.065f,.30f,.40f),hair,7);BlockGeometry.Part("Marta travel sash",root.transform,new Vector3(0,1.00f,.161f),new Vector3(.50f,.10f,.025f),fabric,6);}
   }
   if(name=="Илья")BlockGeometry.Part("Square beard",root.transform,new Vector3(0,1.39f,.222f),new Vector3(.24f,.14f,.045f),baseRenderer.sharedMaterial,7);
   if(name=="Мира"){root.transform.Find("Block hood top").gameObject.SetActive(false);root.transform.Find("Block hood back").gameObject.SetActive(false);var skin=root.transform.Find("Cubical head").GetComponent<MeshRenderer>().sharedMaterial;BlockGeometry.Part("Angular hair crown",root.transform,new Vector3(0,1.735f,-.015f),new Vector3(.42f,.10f,.42f),skin,7);for(int i=0;i<5;i++)BlockGeometry.Part("Cuboid braid",root.transform,new Vector3(.19f,1.59f-i*.09f,-.21f),new Vector3(.08f,.11f,.09f),skin,7);}
  }
  void Crate(int x,int z)
  {var root=new GameObject("Searchable supplies");root.transform.SetParent(transform,false);root.transform.position=new Vector3(x+.5f,floor+1,z+.5f);StoryMesh.Cube("Bound wooden chest",root.transform,Vector3.up*.3f,new Vector3(.9f,.6f,.7f),18,material,true);StoryMesh.Cube("Iron latch",root.transform,new Vector3(0,.3f,.365f),new Vector3(.12f,.18f,.035f),46,material,true);roots.Add(root);}
  void Guardian()
  {
   guardian=new GameObject("Страж корней · block-authored guardian",typeof(BoxCollider));guardian.transform.SetParent(transform,false);guardian.transform.position=new Vector3(176.5f,floor+2,-111.5f);var collision=guardian.GetComponent<BoxCollider>();collision.center=new Vector3(0,2.75f,0);collision.size=new Vector3(2.3f,5.5f,1.7f);roots.Add(guardian);
   sculpture=GuardianSculpture.Build(guardian.transform,material);
   eruption=new GameObject("Root eruption / visual only, damage remains authoritative");eruption.transform.SetParent(transform,false);for(int i=0;i<9;i++){float angle=i*2.4f;var root=StoryMesh.Cube("Splintered attacking root",eruption.transform,new Vector3(Mathf.Sin(angle)*i*.18f,1.25f,Mathf.Cos(angle)*i*.18f),new Vector3(.25f+(i%3)*.12f,2.5f+(i%2),.3f),4,material,true);root.transform.localRotation=Quaternion.Euler(i%2==0?18:-20,angle*57.3f,i%2==0?15:-16);}eruption.SetActive(false);
   warning=StoryMesh.Cube("Root strike telegraph",transform,new Vector3(176,floor+1.04f,-112),new Vector3(5,.03f,5),1,blood);warning.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;warning.SetActive(false);
  }
  public void Apply(StoryState state,long now)
  {
   float lift=state.stage<=1?Mathf.SmoothStep(0,1,Mathf.Clamp01((now-state.sceneAt-2000)/9000f)):1;introBeam.SetActive(state.stage<4&&world.Loaded(introBeam.transform.position));introBeam.transform.position=new Vector3(-32+lift*3,floor+1.65f+Mathf.Sin(lift*Mathf.PI)*1.2f-lift*.45f,-31.2f-lift);introBeam.transform.rotation=Quaternion.Euler(lift*12,15+lift*20,-12+lift*9);
   foreach(var root in roots)if(root)root.SetActive(world.Loaded(root.transform.position));bool aliveWitness=state.stage<=3;float breath=aliveWitness?Mathf.Sin(Time.time*2.3f)*.006f:0;
   if(oren)oren.transform.position=new Vector3(-34,floor+1.21f+breath,-34);if(marta)marta.transform.position=new Vector3(-30,floor+1.21f-breath,-35);
   if(mira){Vector3 p=state.stage<6?new Vector3(94,floor+1.03f,52):state.stage<9?new Vector3(48,floor+1.03f,-90):state.stage<10?new Vector3(106,floor+1.03f,59):new Vector3(176,floor+1.18f,-103);mira.transform.position=p;mira.transform.rotation=state.stage>=12?Quaternion.Euler(0,20,90):Quaternion.Euler(0,210,0);mira.GetComponent<PlayerNameplate>().enabled=state.stage<12;}
   for(int i=0;i<3;i++)fires[i].SetActive(state.stage==11&&(state.pylons&(1<<i))!=0&&world.Loaded(fires[i].transform.position));bool atLoss=state.stage>=12&&state.stage<14;vera.transform.position=atLoss?new Vector3(179,floor+1.03f,-100):state.stage==9?new Vector3(109,floor+1.03f,60):new Vector3(96,floor+1.03f,50);ilya.transform.position=state.stage>=12?new Vector3(174,floor+1.03f,-101):new Vector3(108,floor+1.03f,60);
   for(int i=0;i<3;i++)weights[i].localRotation=Quaternion.Euler(0,0,(state.bridgeMask&(1<<i))!=0?42:-35);bridgeGate.transform.position=new Vector3(3,floor+2.3f+(state.bridgeSolved?4:0),9);
   sculpture.SetExposed(state.stage==11&&state.pylons==7);
   bool bossActive=state.stage==10&&state.sceneAt>0||state.stage==11||state.stage==12;guardian.SetActive(bossActive&&world.Loaded(guardian.transform.position));guardian.transform.localScale=state.stage==12?Vector3.one*Mathf.Max(.08f,1-(now-state.sceneAt)/12500f):Vector3.one;
   guardian.transform.rotation=Quaternion.Euler(0,Mathf.Sin(Time.time*.55f)*6,0);warning.SetActive(state.stage==11&&state.impactAt>0);if(warning.activeSelf){warning.transform.position=new Vector3(state.attackX,floor+1.025f,state.attackZ);float r=state.bossPhase==0?5:6.8f;warning.transform.localScale=new Vector3(r,.035f,r);}
   if(rememberedImpact>0&&now>=rememberedImpact&&lastBurst!=rememberedImpact&&state.stage==11){lastBurst=rememberedImpact;burstTime=Time.time;eruption.transform.position=impactPoint;Bell(74);}
   if(state.impactAt>0){rememberedImpact=state.impactAt;impactPoint=new Vector3(state.attackX,floor+1,state.attackZ);}
   float burst=Time.time-burstTime;eruption.SetActive(state.stage==11&&burst<1.1f&&world.Loaded(eruption.transform.position));if(eruption.activeSelf)eruption.transform.localScale=new Vector3(1,Mathf.Max(.01f,Mathf.Sin(Mathf.Clamp01(burst/1.1f)*Mathf.PI)),1);
   if(oldStage!=state.stage){oldStage=state.stage;Bell(state.stage==1?58:state.stage==12?92:state.stage==11?130:220);}
  }
  void Bell(float hz){int n=16000;var samples=new float[n];for(int i=0;i<n;i++){float t=i/16000f;samples[i]=(Mathf.Sin(t*hz*6.283f)+Mathf.Sin(t*hz*9.13f)*.3f)*Mathf.Exp(-t*6)*.18f;}var clip=AudioClip.Create("Original procedural bell / rumble",n,1,16000,false);clip.SetData(samples,0);sound.PlayOneShot(clip);Destroy(clip,2);}
  void OnDestroy(){foreach(var costume in costumes)if(costume)Destroy(costume);if(material)Destroy(material);if(blood)Destroy(blood);}
 }
 public static class StoryMesh
 {
  public static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 size,int block,Material material,bool local=false,float emission=0)
  {
   var m=new Mesh{name=name};var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector3>();var t=new List<Vector4>();var c=new List<Color>();var tri=new List<int>();
   for(int f=0;f<6;f++){int start=v.Count;Vector3 tangent=BlockGeometry.Corners[f][3]-BlockGeometry.Corners[f][0];for(int k=0;k<4;k++){v.Add(BlockGeometry.Corners[f][k]-Vector3.one*.5f);n.Add(BlockGeometry.Directions[f]);uv.Add(new Vector3(k>=2?1:0,k==1||k==2?1:0,SurfaceLayers.ForFace((ushort)block,f)));t.Add(new Vector4(tangent.x,tangent.y,tangent.z,-1));c.Add(new Color(1,1,1,emission));}tri.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});}
   m.SetVertices(v);m.SetNormals(n);m.SetUVs(0,uv);m.SetTangents(t);m.SetColors(c);m.SetTriangles(tri,0);m.RecalculateBounds();var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(parent,false);if(local)obj.transform.localPosition=position;else obj.transform.position=position;obj.transform.localScale=size;obj.GetComponent<MeshFilter>().sharedMesh=m;obj.GetComponent<MeshRenderer>().sharedMaterial=material;obj.AddComponent<StoryMeshLifetime>().Mesh=m;return obj;
  }
 }
 public sealed class StoryMeshLifetime:MonoBehaviour{public Mesh Mesh;void OnDestroy(){if(Mesh)Destroy(Mesh);}}
}
