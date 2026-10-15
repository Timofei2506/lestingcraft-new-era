using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Presentation
{
    /// <summary>Seeded cosmetic grass, batched into the chunk. No blocks, objects, network edits or colliders.</summary>
    public static class SurfaceDetailMesher
    {
        public static void AddGrass(ChunkMeshData mesh,int x,int y,int z,int ox,int oz,int seed)
        {
            uint hash=TerrainGenerator.Hash(ox+x,y,oz+z,seed^9371);
            if(hash%3!=0)return;
            float centerX=x+.22f+((hash>>5)&255)/255f*.56f,centerZ=z+.22f+((hash>>13)&255)/255f*.56f;
            for(int blade=0;blade<4;blade++)
            {
                uint h=TerrainGenerator.Hash(ox+x,blade+17,oz+z,seed);
                float angle=(h&1023)/1023f*Mathf.PI*2,height=.23f+((h>>10)&255)/255f*.32f;
                var side=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(.025f+height*.04f);
                var bend=new Vector3(-side.z,0,side.x)*2;
                var root=new Vector3(centerX+Mathf.Cos(blade*2.4f)*.12f,y+1.005f,centerZ+Mathf.Sin(blade*2.4f)*.12f);
                var mid=root+Vector3.up*height*.58f+bend*.45f;var tip=root+Vector3.up*height+bend;
                var normal=Vector3.Cross(side,Vector3.up).normalized;
                int first=mesh.Vertices.Count;
                var positions=new[]{root-side,root+side,mid-side*.62f,mid+side*.62f,tip};
                var uv=new[]{new Vector2(0,0),new Vector2(1,0),new Vector2(.19f,.58f),new Vector2(.81f,.58f),new Vector2(.5f,1)};
                byte variation=(byte)(210+(h%46));
                for(int v=0;v<5;v++)
                {
                    mesh.Vertices.Add(positions[v]);mesh.Normals.Add(normal);mesh.Tangents.Add(new Vector4(side.normalized.x,0,side.normalized.z,1));
                    mesh.UVs.Add(new Vector3(uv[v].x,uv[v].y,root.y));mesh.Colors.Add(new Color32(variation,255,(byte)(220+h%30),255));
                }
                int[] triangles={0,1,3,0,3,2,2,3,4};foreach(int index in triangles)mesh.DetailTriangles.Add(first+index);
            }
        }
    }
}
