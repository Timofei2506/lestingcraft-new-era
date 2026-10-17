using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace LestingCraft.Presentation
{
 // Version-8 authored street fixtures. Campfires inside houses remain campfires.
 // These fixtures are architectural decoration; placeable lantern/fence blocks are a later phase.
 public sealed class VillageStreetLights:MonoBehaviour
 {
  readonly List<Light> lamps=new List<Light>();Mesh metalMesh;Material glow;
  public int Count=>lamps.Count;
  public void Initialize(int seed)
  {
   float floor=LestingCraft.Story.StoryWorld.Floor(seed);
   metalMesh=LanternMesh();glow=new Material(Shader.Find("Universal Render Pipeline/Unlit"));glow.SetColor("_BaseColor",new Color(5.5f,1.7f,.32f));
   int[] positions={-39,-24,43,-86,88,48,105,48,96,26,96,72};
   for(int i=0;i<positions.Length;i+=2)
   {
    var obj=new GameObject("Village framed lantern "+i/2,typeof(MeshFilter),typeof(MeshRenderer));obj.transform.SetParent(transform,false);obj.transform.localPosition=new Vector3(positions[i]+.5f,floor+5.01f,positions[i+1]+.5f);
    obj.GetComponent<MeshFilter>().sharedMesh=metalMesh;obj.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Generated/VoxelObjects");
    var filament=GameObject.CreatePrimitive(PrimitiveType.Cube);filament.name="Warm lantern filament";Destroy(filament.GetComponent<Collider>());filament.transform.SetParent(obj.transform,false);filament.transform.localPosition=new Vector3(0,.26f,0);filament.transform.localScale=new Vector3(.13f,.22f,.13f);filament.GetComponent<MeshRenderer>().sharedMaterial=glow;filament.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
    // Separate source at the filament, not under the opaque base of the pole.
    var source=new GameObject("Lantern light source");source.transform.SetParent(obj.transform,false);source.transform.localPosition=Vector3.up*.26f;var light=source.AddComponent<Light>();light.type=LightType.Point;light.range=13;light.color=new Color(1,.58f,.24f);light.intensity=4.7f;light.shadows=LightShadows.Soft;light.shadowBias=.025f;light.shadowNormalBias=.1f;light.shadowResolution=LightShadowResolution.Medium;lamps.Add(light);
   }
  }
  static Mesh LanternMesh()
  {
   var cube=Instantiate(Resources.GetBuiltinResource<Mesh>("Cube.fbx"));var uv=new List<Vector3>();foreach(var v in cube.uv)uv.Add(new Vector3(v.x,v.y,51));cube.SetUVs(0,uv);
   var parts=new List<CombineInstance>();
   System.Action<Vector3,Vector3> box=(p,s)=>parts.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(p,Quaternion.identity,s)});
   box(new Vector3(0,.035f,0),new Vector3(.38f,.07f,.38f));box(new Vector3(0,.48f,0),new Vector3(.44f,.07f,.44f));box(new Vector3(0,.54f,0),new Vector3(.31f,.07f,.31f));box(new Vector3(0,.60f,0),new Vector3(.07f,.09f,.07f));
   foreach(float x in new[]{-.155f,.155f})foreach(float z in new[]{-.155f,.155f})box(new Vector3(x,.27f,z),new Vector3(.032f,.41f,.032f));
   var result=new Mesh{name="Framed PBR village lantern"};result.CombineMeshes(parts.ToArray(),true,true);Destroy(cube);return result;
  }
  void LateUpdate(){float t=Shader.GetGlobalFloat("_EnvironmentTime");for(int i=0;i<lamps.Count;i++)if(lamps[i])lamps[i].intensity=4.7f+Mathf.Sin(t*8.7f+i)*.1f+Mathf.Sin(t*17.3f+i*.3f)*.035f;}
  void OnDestroy(){if(metalMesh)Destroy(metalMesh);if(glow)Destroy(glow);}
 }
}
