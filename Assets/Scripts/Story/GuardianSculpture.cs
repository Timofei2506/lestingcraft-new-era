using System;using System.Collections.Generic;using UnityEngine;using UnityEngine.Rendering;using LestingCraft.Presentation;
namespace LestingCraft.Story
{
 [Serializable] public sealed class GuardianPalette {public string name;public int layer;public float[] tint;public float emission;}
 [Serializable] public sealed class GuardianDefinition {public int version;public string name;public float unit;public GuardianPalette[] palette;public int[] cells;}
 /// <summary>One surface-culled, textured voxel sculpture. Data is shared with the honest offline preview.
 /// No terrain-block collage, smooth primitives, per-voxel GameObjects or hidden internal cube faces.</summary>
 public sealed class GuardianMeshData {public List<Vector3> vertices,normals,uv;public List<Vector4> tangents;public List<Color> colors;public List<byte> materials;public List<int> triangles;}
 public sealed class GuardianSculpture:MonoBehaviour
 {
  Mesh mesh;Color[] colors,original;byte[] materials;Light core;bool exposed;bool initialized;
  public static GuardianSculpture Build(Transform parent,Material material)
  {
   var asset=Resources.Load<TextAsset>("Models/RootGuardian");if(!asset)throw new InvalidOperationException("Root Guardian sculpture asset missing");
   var data=JsonUtility.FromJson<GuardianDefinition>(asset.text);if(data==null||data.unit<=0||data.cells==null||data.cells.Length%4!=0||data.palette==null)throw new InvalidOperationException("Invalid Root Guardian sculpture");
   var go=new GameObject(data.name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);var instance=go.AddComponent<GuardianSculpture>();
   instance.mesh=CreateMesh(data,out instance.original,out instance.materials);instance.colors=(Color[])instance.original.Clone();go.GetComponent<MeshFilter>().sharedMesh=instance.mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;
   var lamp=new GameObject("Amber heart / local light");lamp.transform.SetParent(go.transform,false);lamp.transform.localPosition=new Vector3(0,3.17f,.60f);instance.core=lamp.AddComponent<Light>();instance.core.type=LightType.Point;instance.core.range=3.5f;instance.core.color=new Color(1,.38f,.055f);instance.core.shadows=LightShadows.Soft;instance.core.shadowBias=.03f;instance.SetExposed(false);return instance;
  }
  public static Mesh CreateMesh(GuardianDefinition data,out Color[] baseColors,out byte[] materialIds)
  {
   var d=CreateGeometry(data);var mesh=new Mesh{name=data.name,indexFormat=d.vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(d.vertices);mesh.SetNormals(d.normals);mesh.SetUVs(0,d.uv);mesh.SetTangents(d.tangents);mesh.SetColors(d.colors);mesh.SetTriangles(d.triangles,0);mesh.RecalculateBounds();baseColors=d.colors.ToArray();materialIds=d.materials.ToArray();return mesh;
  }
  static float Linear(float v)=>v<=.04045f?v/12.92f:(float)Math.Pow((v+.055f)/1.055f,2.4);
  public static GuardianMeshData CreateGeometry(GuardianDefinition data)
  {
   var occupied=new Dictionary<Vector3Int,int>();for(int i=0;i<data.cells.Length;i+=4){int id=data.cells[i+3];if(id<0||id>=data.palette.Length)throw new InvalidOperationException("Invalid guardian palette index");occupied.Add(new Vector3Int(data.cells[i],data.cells[i+1],data.cells[i+2]),id);}
   var vertices=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector3>();var tangents=new List<Vector4>();var colors=new List<Color>();var ids=new List<byte>();var triangles=new List<int>();
   foreach(var pair in occupied)for(int f=0;f<6;f++)
   {
    var normal=BlockGeometry.Directions[f];if(occupied.ContainsKey(pair.Key+normal))continue;int start=vertices.Count;var p=data.palette[pair.Value];var tint=new Color(Linear(p.tint[0]),Linear(p.tint[1]),Linear(p.tint[2]),1);tint.a=p.emission;
    var tangent=BlockGeometry.Corners[f][3]-BlockGeometry.Corners[f][0];
    for(int k=0;k<4;k++)
    {
     Vector3 v=((Vector3)pair.Key+BlockGeometry.Corners[f][k])*data.unit;vertices.Add(v);normals.Add(normal);colors.Add(tint);ids.Add((byte)pair.Value);
     // Continuous metre-space grain, not a complete log texture repeated on every tiny cube.
     Vector2 t=f<2?new Vector2(v.z,v.y):f<4?new Vector2(v.x,v.z):new Vector2(v.x,v.y);uv.Add(new Vector3(t.x*.8f,t.y*.8f,p.layer));tangents.Add(new Vector4(tangent.x,tangent.y,tangent.z,-1));
    }
    foreach(int i in new[]{0,1,2,0,2,3})triangles.Add(start+i);
   }
   return new GuardianMeshData{vertices=vertices,normals=normals,uv=uv,tangents=tangents,colors=colors,materials=ids,triangles=triangles};
  }
  public void SetExposed(bool value)
  {
   if(initialized&&exposed==value)return;initialized=true;exposed=value;
   for(int i=0;i<colors.Length;i++){colors[i]=original[i];if(materials[i]==4||materials[i]==8||materials[i]==9)colors[i].a=value?original[i].a:original[i].a*.38f;}mesh.colors=colors;
   if(core)core.intensity=value?2.7f:.65f;
  }
  void OnDestroy(){if(mesh)Destroy(mesh);}
 }
}
