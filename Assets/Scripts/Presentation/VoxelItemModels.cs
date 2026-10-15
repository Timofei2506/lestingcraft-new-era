using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace LestingCraft.Presentation
{
    /// <summary>Shared authored voxel coordinates used by the icon exporter AND real held-object meshes.</summary>
    public static class VoxelItemModels
    {
        [Serializable] public sealed class Definition {public int id;public string name;public int[] cells;}
        [Serializable] sealed class Catalog {public int version;public string[] colors;public int[] layers;public Definition[] models;}
        static Catalog catalog;static Dictionary<int,Definition> definitions;static readonly Dictionary<int,Mesh> meshes=new Dictionary<int,Mesh>();
        static void Load()
        {
            if(definitions!=null)return;
            var data=Resources.Load<TextAsset>("Models/VoxelItems");if(!data)throw new InvalidOperationException("Voxel object catalog missing");
            catalog=JsonUtility.FromJson<Catalog>(data.text);definitions=new Dictionary<int,Definition>();foreach(var model in catalog.models)definitions.Add(model.id,model);
        }
        public static Definition Get(int id){Load();return definitions.TryGetValue(id,out var model)?model:null;}
        public static Mesh MeshFor(int id)
        {
            if(meshes.TryGetValue(id,out var cached)&&cached)return cached;var model=Get(id);if(model==null)return null;
            var cells=new Dictionary<Vector3Int,int>();var lo=Vector3.positiveInfinity;var hi=Vector3.negativeInfinity;
            for(int i=0;i<model.cells.Length;i+=4)
            {var pos=new Vector3Int(model.cells[i],model.cells[i+1],model.cells[i+2]);cells.Add(pos,model.cells[i+3]);lo=Vector3.Min(lo,pos);hi=Vector3.Max(hi,(Vector3)pos+Vector3.one);}
            Vector3 center=(lo+hi)*.5f;float scale=1/Mathf.Max(hi.x-lo.x,Mathf.Max(hi.y-lo.y,hi.z-lo.z));
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var colors=new List<Color>();var uvs=new List<Vector3>();var tangents=new List<Vector4>();var triangles=new List<int>();
            foreach(var pair in cells)for(int face=0;face<6;face++)
            {
                if(cells.ContainsKey(pair.Key+BlockGeometry.Directions[face]))continue;int start=vertices.Count,palette=pair.Value;
                ColorUtility.TryParseHtmlString(catalog.colors[palette],out var tint);
                var tangent=BlockGeometry.Corners[face][3]-BlockGeometry.Corners[face][0];
                for(int k=0;k<4;k++)
                {
                    Vector3 pos=(Vector3)pair.Key+BlockGeometry.Corners[face][k];vertices.Add((pos-center)*scale);normals.Add(BlockGeometry.Directions[face]);colors.Add(tint.linear);
                    Vector2 uv=face<2?new Vector2(pos.z,pos.y):face<4?new Vector2(pos.x,pos.z):new Vector2(pos.x,pos.y);
                    uvs.Add(new Vector3(uv.x/12,uv.y/12,catalog.layers[palette]));tangents.Add(new Vector4(tangent.x,tangent.y,tangent.z,-1));
                }
                foreach(int k in new[]{0,1,2,0,2,3})triangles.Add(start+k);
            }
            var mesh=new Mesh{name="Voxel object "+id,indexFormat=vertices.Count>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetColors(colors);mesh.SetUVs(0,uvs);mesh.SetTangents(tangents);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();meshes[id]=mesh;return mesh;
        }
    }
}
