using System.Collections.Generic;
using UnityEngine;

namespace LestingCraft.Presentation
{
    /// <summary>Flat-normal cuboid only. Atlas UVs are inset to avoid bleeding between tiles.</summary>
    public static class BlockGeometry
    {
        public static readonly Vector3Int[] Directions={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
        public static readonly Vector3[][] Corners={
            new[]{new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(1,1,1),new Vector3(1,0,1)},
            new[]{new Vector3(0,0,1),new Vector3(0,1,1),new Vector3(0,1,0),new Vector3(0,0,0)},
            new[]{new Vector3(0,1,1),new Vector3(1,1,1),new Vector3(1,1,0),new Vector3(0,1,0)},
            new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,0,1),new Vector3(0,0,1)},
            new[]{new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,1),new Vector3(0,0,1)},
            new[]{new Vector3(0,0,0),new Vector3(0,1,0),new Vector3(1,1,0),new Vector3(1,0,0)}};
        static readonly Dictionary<int,Mesh> boxes=new Dictionary<int,Mesh>();
        public static Mesh Box(int tile,int front=-1)
        {
            int key=tile*32+front+1;if(boxes.TryGetValue(key,out var existing)&&existing)return existing;
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var tangents=new List<Vector4>();var triangles=new List<int>();
            for(int f=0;f<6;f++)
            {
                int t=f==4&&front>=0?front:tile;Vector2 origin=new Vector2(t%4,3-t/4)*.25f;int start=v.Count;
                var tangent=Corners[f][3]-Corners[f][0];
                for(int k=0;k<4;k++)
                {
                    v.Add(Corners[f][k]-Vector3.one*.5f);n.Add(Directions[f]);
                    float x=k>=2?1:0,y=k==1||k==2?1:0;
                    uv.Add(origin+new Vector2(Mathf.Lerp(.004f,.246f,x),Mathf.Lerp(.004f,.246f,y)));
                    tangents.Add(new Vector4(tangent.x,tangent.y,tangent.z,-1));
                }
                foreach(int k in new[]{0,1,2,0,2,3})triangles.Add(start+k);
            }
            var mesh=new Mesh{name="Block cuboid"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTangents(tangents);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();boxes[key]=mesh;return mesh;
        }
        public static Transform Part(string name,Transform parent,Vector3 position,Vector3 scale,Material material,int tile,int front=-1,int layer=0)
        {
            var obj=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));obj.layer=layer;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;
            obj.GetComponent<MeshFilter>().sharedMesh=Box(tile,front);obj.GetComponent<MeshRenderer>().sharedMaterial=material;return obj.transform;
        }
        public static Material CharacterMaterial()
        {
            var material=new Material(Resources.Load<Material>("Generated/Avatar"));material.name="Block adventurer fabric / skin";material.SetColor("_BaseColor",Color.white);material.SetFloat("_Smoothness",.13f);
            material.SetTexture("_BaseMap",Resources.Load<Texture2D>("Generated/BlockCharacter"));return material;
        }
    }
}
