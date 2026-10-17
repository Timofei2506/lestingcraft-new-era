using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.World;
using LestingCraft.Runtime;
namespace LestingCraft.Presentation
{
 // Explicit, deterministic landscape LOD, not an automatic quality reduction.
 // Full-detail streamed chunks/colliders are unchanged. Far mesh is visual, never mineable collision.
 public sealed class FarLandscape:MonoBehaviour
 {
  public VoxelWorld World;Mesh land,water;GameObject landObject,waterObject;Vector2Int center=new Vector2Int(int.MinValue,int.MinValue);int preset=-1,radius=-1;bool building;
  public bool Ready {get;private set;}public int TerrainVertices=>land?land.vertexCount:0;
  void Update()
  {
   if(!World||!World.Player||World.ContentVersion<8||World.Dimension!=Dimension.Lestingia)return;
   var c=new Vector2Int(VoxelWorld.ChunkCoordinate(Mathf.FloorToInt(World.Player.position.x)),VoxelWorld.ChunkCoordinate(Mathf.FloorToInt(World.Player.position.z)));var settings=VisualSettings.Current;
   if(!building&&(c!=center||settings.Preset!=preset||settings.ViewDistance!=radius)){center=c;preset=settings.Preset;radius=settings.ViewDistance;StartCoroutine(Rebuild(c,preset,radius));}
  }
  IEnumerator Rebuild(Vector2Int c,int quality,int nearRadius)
  {
   building=true;Ready=false;int extent=quality==1?640:256,step=8,originX=c.x*16,originZ=c.y*16,side=extent*2/step,storyFloor=LestingCraft.Story.StoryWorld.Floor(World.Seed);
   int[,] heights=new int[side+1,side+1];
   for(int z=0;z<=side;z++)
   {for(int x=0;x<=side;x++){int wx=originX-extent+x*step,wz=originZ-extent+z*step,h=OceanGenerator.Height(wx,wz,World.Seed),floor=storyFloor;if(LestingCraft.Story.StoryWorld.TrySample(wx,floor,wz,World.Seed,out ushort placed)&&BlockRegistry.IsSolid(placed))h=floor;heights[x,z]=h+1;}if(z%8==7)yield return null;}
   var ground=new ChunkMeshData();var sea=new ChunkMeshData();
   int minX=(c.x-nearRadius)*16,maxX=(c.x+nearRadius+1)*16,minZ=(c.y-nearRadius)*16,maxZ=(c.y+nearRadius+1)*16;
   for(int z=0;z<side;z++)
   {
    for(int x=0;x<side;x++)
    {
     int wx=originX-extent+x*step,wz=originZ-extent+z*step;if(wx>=minX&&wx+step<=maxX&&wz>=minZ&&wz+step<=maxZ||!World.Contains(wx,1,wz)||!World.Contains(wx+step-1,1,wz+step-1))continue;
     var kind=OceanGenerator.Biome(wx+step/2,wz+step/2,World.Seed);ushort block=kind==BiomeKind.Sea||kind==BiomeKind.Coast||kind==BiomeKind.Dunes?(ushort)6:kind==BiomeKind.Taiga?(ushort)14:kind==BiomeKind.RockyHighlands?(ushort)32:kind==BiomeKind.Marsh?(ushort)34:(ushort)3;
     float a=heights[x,z],b=heights[x+1,z],d=heights[x,z+1],e=heights[x+1,z+1];
     Quad(ground,new Vector3(wx,a,wz),new Vector3(wx,b,wz)+Vector3.right*step,new Vector3(wx,e,wz)+new Vector3(step,0,step),new Vector3(wx,d,wz)+Vector3.forward*step,SurfaceLayers.ForFace(block,2),false);
     // Mask sea cells against sampled seabed; no infinite blue sheet over dry land.
     if(Mathf.Min(a,b,d,e)<41)Quad(sea,new Vector3(wx,41,wz),new Vector3(wx+step,41,wz),new Vector3(wx+step,41,wz+step),new Vector3(wx,41,wz+step),8,true);
     // Short LOD skirts hide sub-cell height differences along the full-detail streaming hole.
     if(wx+step==minX&&wz>=minZ&&wz<maxZ)Skirt(ground,new Vector3(wx+step,b,wz),new Vector3(wx+step,e,wz+step),SurfaceLayers.ForFace(block,2));
     if(wx==maxX&&wz>=minZ&&wz<maxZ)Skirt(ground,new Vector3(wx,d,wz+step),new Vector3(wx,a,wz),SurfaceLayers.ForFace(block,2));
     if(wz+step==minZ&&wx>=minX&&wx<maxX)Skirt(ground,new Vector3(wx,d,wz+step),new Vector3(wx+step,e,wz+step),SurfaceLayers.ForFace(block,2));
     if(wz==maxZ&&wx>=minX&&wx<maxX)Skirt(ground,new Vector3(wx+step,b,wz),new Vector3(wx,a,wz),SurfaceLayers.ForFace(block,2));
    }if(z%8==7)yield return null;
   }
   var newLand=ground.CreateMesh();var newWater=sea.CreateMesh();newLand.subMeshCount=1;newWater.subMeshCount=1;var waterBounds=newWater.bounds;waterBounds.Expand(.5f);newWater.bounds=waterBounds;newLand.name="Far terrain PBR LOD";newWater.name="Far sea analytic waves LOD";
   if(!landObject){landObject=new GameObject("Distant PBR land",typeof(MeshFilter),typeof(MeshRenderer));landObject.transform.SetParent(transform,false);landObject.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Generated/VoxelPBR");waterObject=new GameObject("Distant blue sea",typeof(MeshFilter),typeof(MeshRenderer));waterObject.transform.SetParent(transform,false);waterObject.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Generated/Water");waterObject.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;}
   landObject.GetComponent<MeshFilter>().sharedMesh=newLand;waterObject.GetComponent<MeshFilter>().sharedMesh=newWater;if(land)Destroy(land);if(water)Destroy(water);land=newLand;water=newWater;building=false;Ready=true;
  }
  static void Skirt(ChunkMeshData data,Vector3 a,Vector3 b,int layer)=>Quad(data,a,b,b-Vector3.up*4,a-Vector3.up*4,layer,false);
  static void Quad(ChunkMeshData data,Vector3 a,Vector3 b,Vector3 c,Vector3 d,int layer,bool liquid)
  {
   int start=data.Vertices.Count;Vector3 normal=Vector3.Cross(d-a,b-a).normalized;Vector3 tangent=(b-a).normalized;
   foreach(var v in new[]{a,b,c,d}){data.Vertices.Add(v);data.Normals.Add(normal);data.Tangents.Add(new Vector4(tangent.x,tangent.y,tangent.z,-1));data.Colors.Add(new Color32(255,255,255,255));data.UVs.Add(new Vector3(v.x,v.z,layer));}
   var triangles=data.OpaqueTriangles;triangles.AddRange(new[]{start,start+3,start+2,start,start+2,start+1});
  }
  void OnDestroy(){if(land)Destroy(land);if(water)Destroy(water);}
 }
}
