using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Presentation;

namespace LestingCraft.World
{
    public sealed class ChunkMeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Vector3> UVs = new List<Vector3>();
        public readonly List<Vector4> Tangents = new List<Vector4>();
        public readonly List<int> OpaqueTriangles = new List<int>();
        public readonly List<int> WaterTriangles = new List<int>();
        public readonly List<int> DetailTriangles = new List<int>();
        public readonly List<Color32> Colors = new List<Color32>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<int> ColliderTriangles = new List<int>();
        public int FaceCount => Triangles.Count / 6;
        public Mesh CreateMesh(bool collision = false)
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(Vertices);
            if(collision)mesh.SetTriangles(ColliderTriangles,0);
            else
            {
                mesh.subMeshCount=3;mesh.SetTriangles(OpaqueTriangles,0);mesh.SetTriangles(WaterTriangles,1);mesh.SetTriangles(DetailTriangles,2);
                mesh.SetNormals(Normals);mesh.SetColors(Colors);mesh.SetUVs(0,UVs);mesh.SetTangents(Tangents);
            }
            mesh.RecalculateBounds();if(!collision&&(DetailTriangles.Count>0||WaterTriangles.Count>0)){var bounds=mesh.bounds;bounds.Expand(.4f);mesh.bounds=bounds;}return mesh;
        }
    }
    public static class ChunkMesher
    {
        static readonly Vector3Int[] Directions = { Vector3Int.right, Vector3Int.left, Vector3Int.up, Vector3Int.down, new Vector3Int(0,0,1), new Vector3Int(0,0,-1) };
        static readonly Vector3[][] Corners = {
            new[]{new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(1,1,1),new Vector3(1,0,1)},
            new[]{new Vector3(0,0,1),new Vector3(0,1,1),new Vector3(0,1,0),new Vector3(0,0,0)},
            new[]{new Vector3(0,1,1),new Vector3(1,1,1),new Vector3(1,1,0),new Vector3(0,1,0)},
            new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,0,1),new Vector3(0,0,1)},
            new[]{new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,1),new Vector3(0,0,1)},
            new[]{new Vector3(0,0,0),new Vector3(0,1,0),new Vector3(1,1,0),new Vector3(1,0,0)}
        };
        public static Color32 Palette(ushort id)
        {
            switch(id)
            {
                case 1:return new Color32(119,133,146,255); case 2:return new Color32(133,91,60,255);
                case 3:return new Color32(105,177,89,255); case 4:return new Color32(111,75,46,255);
                case 5:return new Color32(61,135,76,255); case 6:return new Color32(226,206,143,255);
                case 7:return new Color32(59,137,197,255); case 8:return new Color32(246,105,47,255);
                case 9:return new Color32(39,45,56,255); case 10:return new Color32(79,68,78,255);
                case 11:return new Color32(174,108,224,255); case 12:return new Color32(105,211,216,255);
                case 13:return new Color32(194,160,103,255); case 14:return new Color32(224,236,244,255);
                default:return new Color32(255,0,255,255);
            }
        }
        // Visible unit faces, not greedy yet. Neighbor sampling prevents seams at chunk boundaries.
        public static ChunkMeshData Build(ChunkData chunk, int ox, int oz, Func<int,int,int,ushort> neighbor,int seed=0,bool decorate=false,int detailQuality=0)
        {
            var data = new ChunkMeshData();
            var grassCells=decorate?new List<Vector3Int>():null;
            for(int y=0;y<ChunkData.SizeY;y++) for(int z=0;z<16;z++) for(int x=0;x<16;x++)
            {
                ushort id=chunk.GetBlock(x,y,z); if(id==0)continue;
                bool solid=BlockRegistry.IsSolid(id);
                for(int face=0;face<6;face++)
                {
                    var dir=Directions[face]; int nx=x+dir.x,ny=y+dir.y,nz=z+dir.z;
                    ushort other=ChunkData.InBounds(nx,ny,nz)?chunk.GetBlock(nx,ny,nz):neighbor(ox+nx,ny,oz+nz);
                    if(BlockRegistry.Occludes(other)||(!solid&&other==id))continue;
                    int start=data.Vertices.Count;
                    Vector3 u=Corners[face][3]-Corners[face][0],v=Corners[face][1]-Corners[face][0];
                    int layer=SurfaceLayers.ForFace(id,face);
                    for(int c=0;c<4;c++)
                    {
                        float ux=c>=2?1:0,vy=(c==1||c==2)?1:0;
                        Vector3Int su=Vector3Int.RoundToInt(u*(ux==0?-1:1)),sv=Vector3Int.RoundToInt(v*(vy==0?-1:1));
                        int sideA=Solid(chunk,ox,oz,nx+su.x,ny+su.y,nz+su.z,neighbor);
                        int sideB=Solid(chunk,ox,oz,nx+sv.x,ny+sv.y,nz+sv.z,neighbor);
                        int corner=Solid(chunk,ox,oz,nx+su.x+sv.x,ny+su.y+sv.y,nz+su.z+sv.z,neighbor);
                        int ao=sideA==1&&sideB==1?3:sideA+sideB+corner;
                        byte shade=(byte)(255-ao*30);
                        var cornerPosition=Corners[face][c];if(id==15)cornerPosition.y*=.34f;
                        data.Vertices.Add(new Vector3(x,y,z)+cornerPosition);data.Normals.Add(dir);
                        data.Colors.Add(new Color32(shade,shade,shade,255));data.UVs.Add(new Vector3(ux,vy,layer));
                        data.Tangents.Add(new Vector4(u.x,u.y,u.z,-1));
                    }
                    AddFace(data.Triangles,start);
                    if(id!=15)AddFace(id==BlockRegistry.WaterId?data.WaterTriangles:data.OpaqueTriangles,start);
                    if(solid)AddFace(data.ColliderTriangles,start);
                    if(decorate&&id==BlockRegistry.GrassId&&face==2&&other==0)
                        grassCells.Add(new Vector3Int(x,y,z));
                }
            }
            if(grassCells!=null)foreach(var cell in grassCells)SurfaceDetailMesher.AddGrass(data,cell.x,cell.y,cell.z,ox,oz,seed,detailQuality);
            return data;
        }
        static int Solid(ChunkData chunk,int ox,int oz,int x,int y,int z,Func<int,int,int,ushort> neighbor)
        {return BlockRegistry.Occludes(ChunkData.InBounds(x,y,z)?chunk.GetBlock(x,y,z):neighbor(ox+x,y,oz+z))?1:0;}
        static void AddFace(List<int> t,int s) {t.Add(s);t.Add(s+1);t.Add(s+2);t.Add(s);t.Add(s+2);t.Add(s+3);}
    }
}
