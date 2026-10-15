using UnityEngine;
using LestingCraft.World;
namespace LestingCraft.Presentation
{
 public static class SurfaceDetailMesher
 {
  public static void AddGrass(ChunkMeshData mesh,int x,int y,int z,int ox,int oz,int seed)
  {
   uint hash=TerrainGenerator.Hash(ox+x,y,oz+z,seed^9371);if(hash%7==0)return;
   int count=7+(int)(hash%5);
   for(int blade=0;blade<count;blade++)
   {
    uint h=TerrainGenerator.Hash(ox+x,blade+17,oz+z,seed);float angle=(h&1023)/1023f*Mathf.PI*2;
    float height=.16f+((h>>10)&255)/255f*.30f,width=.009f+((h>>18)&63)/63f*.012f;
    var side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));var forward=new Vector3(-side.z,0,side.x);
    float spread=((h>>24)&127)/127f;var root=new Vector3(x+.10f+((h>>4)&255)/255f*.80f,y+1.003f,z+.10f+((h>>12)&255)/255f*.80f);
    int first=mesh.Vertices.Count;float lean=.06f+spread*.13f;
    for(int segment=0;segment<=4;segment++)
    {
     float t=segment/4f,w=width*(1-t*t);var center=root+Vector3.up*(height*t)+forward*(lean*t*t);
     for(int edge=0;edge<2;edge++)
     {
      mesh.Vertices.Add(center+side*(edge==0?-w:w));mesh.Normals.Add((forward+Vector3.up*(.25f+t*.5f)).normalized);
      mesh.Tangents.Add(new Vector4(side.x,0,side.z,1));mesh.UVs.Add(new Vector3(edge,t,root.y));
      float tint=.78f+(h%100)/450f;mesh.Colors.Add(new Color(tint,1,.84f+(h%17)/100f,1));
     }
     if(segment>0){int a=first+(segment-1)*2;foreach(int k in new[]{a,a+1,a+3,a,a+3,a+2})mesh.DetailTriangles.Add(k);}
    }
   }
  }
 }
}
