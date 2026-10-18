using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace LestingCraft.Presentation
{
 // Authored flat voxel boxes: no spheres, smooth primitives, painted silhouettes or fake screenshots.
 public sealed class AnimalVisual:MonoBehaviour
 {
  public readonly List<Transform> Legs=new List<Transform>();public Transform Head;Transform body;int species;Vector3 last;float gait;float headY;
  static readonly Vector3[] D={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
  static readonly int[][] C={new[]{1,2,6,5},new[]{4,7,3,0},new[]{7,6,2,3},new[]{0,1,5,4},new[]{5,6,7,4},new[]{0,3,2,1}};
  public sealed class Part
  {
   public readonly List<Vector3> v=new List<Vector3>(),n=new List<Vector3>(),uv=new List<Vector3>();public readonly List<Vector4> t=new List<Vector4>();public readonly List<Color32> c=new List<Color32>();public readonly List<int> tri=new List<int>();
   public void Box(Vector3 center,Vector3 size,Color color,int layer)
   {
    var p=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(0,1,0),new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,1)};
    for(int f=0;f<6;f++){int s=v.Count;Vector3 u=Vector3.Scale(p[C[f][3]]-p[C[f][0]],size),w=Vector3.Scale(p[C[f][1]]-p[C[f][0]],size);Vector3 tangent=u.normalized;for(int i=0;i<4;i++){var q=Vector3.Scale(p[C[f][i]]-Vector3.one*.5f,size)+center;v.Add(q);n.Add(D[f]);uv.Add(new Vector3(i>=2?u.magnitude*3:0,i==1||i==2?w.magnitude*3:0,layer));t.Add(new Vector4(tangent.x,tangent.y,tangent.z,-1));Color32 shade=color;shade.a=0;c.Add(shade);}tri.AddRange(new[]{s,s+1,s+2,s,s+2,s+3});}
   }
   public GameObject Build(string name,Transform parent,Vector3 pos)
   {var mesh=new Mesh{name=name};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetColors(c);mesh.SetUVs(0,uv);mesh.SetTangents(t);mesh.SetTriangles(tri,0);mesh.RecalculateBounds();var g=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Generated/VoxelObjects");g.AddComponent<AnimalMeshLifetime>().Mesh=mesh;return g;}
  }
  public static readonly string[] SpeciesFurNames={"deer-fur","hare-fur","boar-bristles"};
  static int Surface(string name){int layer=System.Array.IndexOf(SurfaceLayers.Names,name);if(layer<0)throw new System.InvalidOperationException("Missing animal material: "+name);return layer;}
  public static int FurLayerForSpecies(int kind){if(kind<0||kind>=SpeciesFurNames.Length)throw new System.ArgumentOutOfRangeException(nameof(kind));return Surface(SpeciesFurNames[kind]);}
  public void Build(int kind)
  {
   species=kind;int fur=FurLayerForSpecies(kind),underfur=Surface("animal-underfur"),keratin=Surface("animal-keratin");
   // Coat colour comes from a distinct generated image, not a generic brown texture tinted per species.
   Color brown=Color.white,cream=new Color(.94f,.89f,.80f),dark=new Color(.25f,.24f,.22f);
   float bodyY=kind==0?.94f:kind==1?.24f:.52f,length=kind==0?1.05f:kind==1?.45f:1.08f,width=kind==0?.48f:kind==1?.3f:.62f,bh=kind==0?.55f:kind==1?.3f:.55f;
   var b=new Part();b.Box(new Vector3(0,bodyY,0),new Vector3(width,bh,length),brown,fur);b.Box(new Vector3(0,bodyY-bh*.38f,.04f),new Vector3(width*.75f,bh*.22f,length*.82f),kind==0?cream:kind==1?Color.white:brown*.85f,kind==2?fur:underfur);
   b.Box(new Vector3(0,bodyY+.04f,-length*.56f),new Vector3(kind==1?.18f:.12f,kind==1?.15f:.1f,.18f),kind==1?Color.white:brown,kind==1?underfur:fur);
   if(kind==0){b.Box(new Vector3(0,1.22f,.4f),new Vector3(.27f,.55f,.29f),brown,fur);for(int i=0;i<4;i++)b.Box(new Vector3((i%2==0?1:-1)*.246f,1.05f,-.35f+(i/2)*.27f),new Vector3(.015f,.055f,.09f),cream,underfur);}
   if(kind==2)for(int i=0;i<8;i++)b.Box(new Vector3(0,.84f,-.45f+i*.12f),new Vector3(.05f,.07f+(i%2)*.035f,.05f),dark,fur);
   body=b.Build("Voxel fur / body",transform,Vector3.zero).transform;
   headY=kind==0?1.4f:kind==1?.38f:.62f;var h=new Part();h.Box(Vector3.zero,new Vector3(kind==0?.25f:kind==1?.25f:.42f,kind==0?.3f:kind==1?.23f:.35f,kind==0?.4f:kind==1?.3f:.46f),brown,fur);
   h.Box(new Vector3(0,-.055f,kind==2?.31f:.22f),new Vector3(kind==2?.29f:.16f,.13f,.22f),kind==2?new Color(.9f,.86f,.8f):Color.white,keratin);
   for(int side=-1;side<=1;side+=2)
   {
    float ex=kind==0?.15f:kind==1?.085f:.21f;h.Box(new Vector3(side*ex,.11f,-.04f),new Vector3(kind==0?.19f:.08f,kind==1?.34f:.14f,.10f),brown,fur);h.Box(new Vector3(side*ex,.13f,.014f),new Vector3(kind==0?.12f:.04f,kind==1?.25f:.07f,.012f),kind==1?new Color(1,.82f,.8f):cream*.85f,underfur);
    h.Box(new Vector3(side*(kind==0?.13f:kind==1?.13f:.217f),.055f,.12f),new Vector3(.018f,.035f,.045f),Color.black,15);h.Box(new Vector3(side*(kind==0?.141f:kind==1?.141f:.228f),.062f,.131f),new Vector3(.006f,.009f,.01f),Color.white,15);
    if(kind==0){h.Box(new Vector3(side*.09f,.36f,-.10f),new Vector3(.045f,.45f,.045f),cream*.65f,15);h.Box(new Vector3(side*.17f,.49f,-.10f),new Vector3(.20f,.04f,.04f),cream*.7f,15);for(int i=0;i<3;i++)h.Box(new Vector3(side*(.09f+i*.08f),.59f+i*.035f,-.10f),new Vector3(.035f,.19f,.035f),cream*.7f,15);}
    if(kind==2)h.Box(new Vector3(side*.16f,-.09f,.31f),new Vector3(.045f,.16f,.045f),cream,15);
   }
   Head=h.Build("Ears, eyes, muzzle / antlers / tusks",transform,new Vector3(0,headY,length*.44f)).transform;
   for(int i=0;i<4;i++)
   {float ly=kind==0?.68f:kind==1?.15f:.32f,ll=kind==0?.65f:kind==1?.19f:.36f;var leg=new Part();leg.Box(new Vector3(0,-ll*.45f,0),new Vector3(kind==0?.09f:.12f,ll,.12f),brown,fur);leg.Box(new Vector3(0,-ll*.91f,.025f),new Vector3(.12f,.09f,kind==1?.22f:.17f),kind==1?cream:Color.white,kind==1?underfur:keratin);Legs.Add(leg.Build("Voxel leg "+i,transform,new Vector3((i%2==0?-1:1)*width*.35f,ly,(i<2?-1:1)*length*.32f)).transform);}
   last=transform.position;
  }
  public void Animate(float dt)
  {
   float speed=new Vector2(transform.position.x-last.x,transform.position.z-last.z).magnitude/Mathf.Max(.001f,dt);last=transform.position;gait+=dt*Mathf.Clamp(speed*6,0,18);
   for(int i=0;i<Legs.Count;i++)Legs[i].localRotation=Quaternion.Euler(Mathf.Sin(gait+(i==0||i==3?0:Mathf.PI))*Mathf.Min(34,speed*12),0,0);
   if(species==1){float hop=Mathf.Abs(Mathf.Sin(gait))*.09f*Mathf.Clamp01(speed);body.localPosition=new Vector3(0,hop,0);var hp=Head.localPosition;hp.y=headY+hop;Head.localPosition=hp;foreach(var leg in Legs){var lp=leg.localPosition;lp.y=.15f+hop;leg.localPosition=lp;}}
   if(Head)Head.localRotation=Quaternion.Euler(Mathf.Sin(Time.time*.7f)*3,Mathf.Sin(Time.time*.4f+headY)*7,0);
  }
 }
 public sealed class AnimalMeshLifetime:MonoBehaviour {public Mesh Mesh;void OnDestroy(){if(Mesh){if(Application.isPlaying)Destroy(Mesh);else DestroyImmediate(Mesh);}}}
}
