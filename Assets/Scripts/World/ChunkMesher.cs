using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LestingCraft.World
{
    public sealed class ChunkMeshData
    {
        public readonly List<Vector3> Vertices = new List<Vector3>();
        public readonly List<Vector3> Normals = new List<Vector3>();
        public readonly List<Color32> Colors = new List<Color32>();
        public readonly List<int> Triangles = new List<int>();
        public readonly List<int> ColliderTriangles = new List<int>();
        public int FaceCount => Triangles.Count / 6;
        public Mesh CreateMesh(bool collision = false)
        {
            var mesh = new Mesh { indexFormat = IndexFormat.UInt32 };
            mesh.SetVertices(Vertices);
            mesh.SetTriangles(collision ? ColliderTriangles : Triangles, 0);
            if (!collision) { mesh.SetNormals(Normals); mesh.SetColors(Colors); }
            mesh.RecalculateBounds(); return mesh;
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
        public static ChunkMeshData Build(ChunkData chunk, int ox, int oz, Func<int,int,int,ushort> neighbor)
        {
            var data = new ChunkMeshData();
            for(int y=0;y<ChunkData.SizeY;y++) for(int z=0;z<16;z++) for(int x=0;x<16;x++)
            {
                ushort id=chunk.GetBlock(x,y,z); if(id==0)continue;
                bool solid=BlockRegistry.IsSolid(id);
                for(int face=0;face<6;face++)
                {
                    var dir=Directions[face]; int nx=x+dir.x,ny=y+dir.y,nz=z+dir.z;
                    ushort other=ChunkData.InBounds(nx,ny,nz)?chunk.GetBlock(nx,ny,nz):neighbor(ox+nx,ny,oz+nz);
                    if(BlockRegistry.IsSolid(other)||(!solid&&other==id))continue;
                    int start=data.Vertices.Count; var color=Palette(id);
                    if(id==BlockRegistry.GrassId && face!=2)color=Palette(BlockRegistry.DirtId);
                    for(int c=0;c<4;c++) { data.Vertices.Add(new Vector3(x,y,z)+Corners[face][c]);data.Normals.Add(dir);data.Colors.Add(color); }
                    AddFace(data.Triangles,start); if(solid)AddFace(data.ColliderTriangles,start);
                }
            }
            return data;
        }
        static void AddFace(List<int> t,int s) {t.Add(s);t.Add(s+1);t.Add(s+2);t.Add(s);t.Add(s+2);t.Add(s+3);}
    }
}
