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
        sealed class Occupancy
        {public readonly HashSet<Vector3Int> Cells=new HashSet<Vector3Int>();public Vector3 Center;public float Unit;}
        static readonly Dictionary<int,Occupancy> occupied=new Dictionary<int,Occupancy>();
        public static bool TracePlaced(int id,Vector3 cell,Vector3 origin,Vector3 direction,float reach,out LestingCraft.Runtime.VoxelRaycast.Hit hit)
        {
            if(!occupied.TryGetValue(id,out var shape))
            {
                shape=new Occupancy();var model=Get(id);Vector3 lo=Vector3.positiveInfinity,hi=Vector3.negativeInfinity;
                for(int i=0;i<model.cells.Length;i+=4){var p=new Vector3Int(model.cells[i],model.cells[i+1],model.cells[i+2]);shape.Cells.Add(p);lo=Vector3.Min(lo,p);hi=Vector3.Max(hi,(Vector3)p+Vector3.one);}
                shape.Center=(lo+hi)*.5f;shape.Unit=PlacedVoxelModel.Scale/Mathf.Max(hi.x-lo.x,Mathf.Max(hi.y-lo.y,hi.z-lo.z));occupied[id]=shape;
            }
            Vector3 source=(origin-cell-PlacedVoxelModel.Offset(id))/shape.Unit+shape.Center;
            bool found=LestingCraft.Runtime.VoxelRaycast.Trace(source,direction,reach/shape.Unit,(x,y,z)=>(ushort)(shape.Cells.Contains(new Vector3Int(x,y,z))?1:0),out var fine);
            hit=new LestingCraft.Runtime.VoxelRaycast.Hit(Vector3Int.RoundToInt(cell),fine.Normal,fine.Distance*shape.Unit);return found;
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
                ColorUtility.TryParseHtmlString(catalog.colors[palette],out var tint);tint.a=palette==32||palette==36?1:palette==12?.08f:palette==13?.025f:0;
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
