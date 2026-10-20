using UnityEngine;
using LestingCraft.Gameplay;
namespace LestingCraft.Presentation
{
 [DefaultExecutionOrder(25)]
 public sealed class SettlerVisual:MonoBehaviour
 {
  CharacterVisual villager;Transform head,tail,workArm;GameObject workTool;Material workMaterial,skinMaterial,hairMaterial,coatMaterial;bool working;int profession;readonly System.Collections.Generic.List<Transform> legs=new System.Collections.Generic.List<Transform>();Vector3 previous;float phase;bool cat;
  // The ID comes from the synchronized actor; appearance is never random or persisted separately.
  public void Build(int kind,int job){Build(kind,job,"v:legacy:"+job);}
  public void Build(int kind,int job,string actorId)
  {
   cat=kind==1;profession=job;previous=transform.position;
   if(!cat)
   {
    var look=SettlerAppearance.For(actorId,job);
    villager=gameObject.AddComponent<CharacterVisual>();villager.Build();
    Color[] skins={new Color(1f,.86f,.72f),new Color(.95f,.72f,.52f),new Color(.76f,.51f,.34f),new Color(.57f,.36f,.25f),new Color(.91f,.66f,.50f)};
    Color[] coats={new Color(.87f,.76f,.54f),new Color(.46f,.73f,.76f),new Color(.58f,.76f,.47f),new Color(.80f,.49f,.40f),new Color(.63f,.57f,.80f),new Color(.74f,.69f,.67f)};
    Color[] hair={new Color(.27f,.19f,.14f),new Color(.68f,.42f,.20f),new Color(.13f,.15f,.18f),new Color(.75f,.68f,.46f),new Color(.45f,.26f,.19f)};
    workMaterial=BlockGeometry.CharacterMaterial();skinMaterial=new Material(workMaterial);skinMaterial.SetColor("_BaseColor",skins[look.Skin]);
    hairMaterial=new Material(workMaterial);hairMaterial.SetColor("_BaseColor",hair[look.Hair]);
    coatMaterial=new Material(workMaterial);coatMaterial.SetColor("_BaseColor",coats[look.Coat]);
    workArm=transform.Find("Right block arm");workTool=new GameObject("Village work tool",typeof(MeshFilter),typeof(MeshRenderer));workTool.transform.SetParent(workArm,false);workTool.transform.localPosition=new Vector3(0,-.42f,.19f);workTool.transform.localScale=Vector3.one*(job==1?.43f:.28f);workTool.GetComponent<MeshFilter>().sharedMesh=VoxelItemModels.MeshFor(job==1?160:job==0?113:139);workTool.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Generated/VoxelObjects");workTool.SetActive(false);
    // Cover the default front-facing skin and repaint the eyes. Change geometry as well as colour.
    BlockGeometry.Part("Individual face",transform,new Vector3(0,1.53f,.205f),new Vector3(.395f,.395f,.018f),skinMaterial,0);
    for(int side=-1;side<=1;side+=2)
    {
     BlockGeometry.Part("Eye",transform,new Vector3(side*.095f,1.57f,.219f),new Vector3(.045f,.045f,.014f),workMaterial,4);
     BlockGeometry.Part("Ear",transform,new Vector3(side*.22f,1.51f,0),new Vector3(.045f,.12f,.095f),skinMaterial,0);
    }
    BlockGeometry.Part("Individual nose",transform,new Vector3(0,1.49f,.244f),new Vector3(.09f,.09f,.085f),skinMaterial,0);
    BlockGeometry.Part("Coloured work coat",transform,new Vector3(0,1.06f,.154f),new Vector3(.44f,.48f,.038f),coatMaterial,1);
    BlockGeometry.Part("Coat hem",transform,new Vector3(0,.80f,.03f),new Vector3(.49f,.11f,.30f),coatMaterial,1);
    if(look.Detail==0)BlockGeometry.Part("Short beard",transform,new Vector3(0,1.355f,.216f),new Vector3(.19f,.065f,.03f),hairMaterial,6);
    else if(look.Detail==1)BlockGeometry.Part("Apron pocket",transform,new Vector3(.1f,.90f,.178f),new Vector3(.15f,.11f,.025f),workMaterial,6);
    else if(look.Detail==2)BlockGeometry.Part("Neck scarf",transform,new Vector3(0,1.32f,.12f),new Vector3(.50f,.09f,.30f),coatMaterial,1);
    if(look.Headwear==0)
    {
     BlockGeometry.Part("Wide hat brim",transform,new Vector3(0,1.78f,0),new Vector3(.62f,.06f,.60f),coatMaterial,job==0?5:6);
     BlockGeometry.Part("Hat crown",transform,new Vector3(0,1.86f,-.02f),new Vector3(.39f,.13f,.39f),coatMaterial,job==0?5:6);
    }
    else if(look.Headwear==1)
    {
     BlockGeometry.Part("Short cap",transform,new Vector3(0,1.78f,0),new Vector3(.44f,.09f,.44f),coatMaterial,1);
     BlockGeometry.Part("Cap peak",transform,new Vector3(0,1.76f,.23f),new Vector3(.35f,.035f,.16f),coatMaterial,1);
    }
    else if(look.Headwear==2)
    {
     BlockGeometry.Part("Side-parted hair",transform,new Vector3(-.04f,1.758f,0),new Vector3(.36f,.07f,.42f),hairMaterial,6);
     BlockGeometry.Part("Side locks",transform,new Vector3(-.19f,1.60f,-.06f),new Vector3(.055f,.26f,.32f),hairMaterial,6);
    }
    else
    {
     BlockGeometry.Part("Long hair back",transform,new Vector3(0,1.50f,-.235f),new Vector3(.40f,.49f,.055f),hairMaterial,6);
     BlockGeometry.Part("Fringe",transform,new Vector3(0,1.745f,.12f),new Vector3(.41f,.07f,.21f),hairMaterial,6);
    }
    return;
   }
   int fur=System.Array.IndexOf(SurfaceLayers.Names,"cat-fur"),pale=55;var body=new AnimalVisual.Part();body.Box(new Vector3(0,.32f,0),new Vector3(.28f,.3f,.58f),Color.white,fur);body.Box(new Vector3(0,.20f,.08f),new Vector3(.19f,.08f,.38f),Color.white,pale);body.Build("Tabby fur and chest",transform,Vector3.zero);
   var h=new AnimalVisual.Part();h.Box(Vector3.zero,new Vector3(.29f,.25f,.25f),Color.white,fur);h.Box(new Vector3(0,-.07f,.15f),new Vector3(.20f,.10f,.1f),Color.white,pale);h.Box(new Vector3(0,-.025f,.211f),new Vector3(.045f,.025f,.015f),new Color(.7f,.42f,.4f),pale);
   for(int side=-1;side<=1;side+=2){h.Box(new Vector3(side*.105f,.16f,0),new Vector3(.075f,.15f,.09f),Color.white,fur);h.Box(new Vector3(side*.105f,.17f,.048f),new Vector3(.04f,.095f,.012f),new Color(.85f,.6f,.59f),pale);h.Box(new Vector3(side*.077f,.025f,.13f),new Vector3(.065f,.049f,.018f),new Color(.54f,.68f,.26f),pale);h.Box(new Vector3(side*.077f,.025f,.142f),new Vector3(.015f,.039f,.012f),Color.black,pale);for(int i=0;i<3;i++)h.Box(new Vector3(side*.16f,-.046f-i*.017f,.17f),new Vector3(.17f,.006f,.006f),new Color(.8f,.8f,.77f),pale);}
   head=h.Build("Square head, ears, whiskers and eyes",transform,new Vector3(0,.48f,.29f)).transform;
   for(int i=0;i<4;i++){var p=new AnimalVisual.Part();p.Box(new Vector3(0,-.10f,0),new Vector3(.085f,.21f,.095f),Color.white,fur);p.Box(new Vector3(0,-.19f,.025f),new Vector3(.09f,.06f,.14f),Color.white,pale);legs.Add(p.Build("Cat paw "+i,transform,new Vector3(i%2==0?-.10f:.10f,.24f,i<2?-.20f:.20f)).transform);}
   tail=new GameObject("Articulated voxel tail").transform;tail.SetParent(transform,false);tail.localPosition=new Vector3(0,.41f,-.27f);Transform joint=tail;for(int i=0;i<5;i++){var p=new AnimalVisual.Part();p.Box(new Vector3(0,0,-.055f),new Vector3(.055f-i*.005f,.055f-i*.005f,.13f),Color.white,fur);joint=p.Build("Tail joint "+i,joint,i==0?Vector3.zero:new Vector3(0,0,-.11f)).transform;joint.localRotation=Quaternion.Euler(-12,0,0);}
  }
  public void Animate(LivingActor a,float dt){working=a.working;if(!cat){if(workTool)workTool.SetActive(working);return;}float speed=(transform.position-previous).magnitude/Mathf.Max(.001f,dt);previous=transform.position;phase+=dt*speed*8;for(int i=0;i<legs.Count;i++)legs[i].localRotation=Quaternion.Euler(a.sitting?(i<2?-70:0):Mathf.Sin(phase+(i==0||i==3?0:Mathf.PI))*Mathf.Min(35,speed*15),0,0);tail.localRotation=Quaternion.Euler(a.sitting?-55:-10,Mathf.Sin(Time.time*1.6f)*18,0);head.localRotation=Quaternion.Euler(a.sitting?8:0,Mathf.Sin(Time.time*.6f)*7,0);}
  void LateUpdate(){if(!cat&&working&&workArm)workArm.localRotation=Quaternion.Euler(profession==1?-45+Mathf.Sin(Time.time*4.8f)*35:-35+Mathf.Sin(Time.time*2.8f)*12,0,0);}
  void OnDestroy(){if(workMaterial)Destroy(workMaterial);if(skinMaterial)Destroy(skinMaterial);if(hairMaterial)Destroy(hairMaterial);if(coatMaterial)Destroy(coatMaterial);}
 }
}
