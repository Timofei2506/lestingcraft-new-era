using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Gameplay;

namespace LestingCraft.Runtime
{
    /// <summary>Renders the exact approved 3D voxel source. Does not start missions, move the
    /// spacecraft, spawn a launchpad or substitute a cabin. Use only after gameplay integration.</summary>
    public sealed class SpaceVoxelVisual : MonoBehaviour
    {
        Mesh mesh;
        Material[] materials;
        static readonly Vector3Int[] Directions={Vector3Int.right,Vector3Int.left,Vector3Int.up,Vector3Int.down,
                                                   new Vector3Int(0,0,1),new Vector3Int(0,0,-1)};
        static readonly Vector3[][] Corners={
            new[]{new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(1,1,1),new Vector3(1,0,1)},
            new[]{new Vector3(0,0,1),new Vector3(0,1,1),new Vector3(0,1,0),new Vector3(0,0,0)},
            new[]{new Vector3(0,1,1),new Vector3(1,1,1),new Vector3(1,1,0),new Vector3(0,1,0)},
            new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,0,1),new Vector3(0,0,1)},
            new[]{new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(0,1,1)},
            new[]{new Vector3(1,0,0),new Vector3(0,0,0),new Vector3(0,1,0),new Vector3(1,1,0)}
        };
        public static SpaceVoxelVisual Create(string assetName,Transform parent,bool collision=false)
        {
            if(assetName!="Shuttle"&&assetName!="LaunchPad"&&assetName!="Station")throw new ArgumentException("Unknown approved model");
            var resource=Resources.Load<TextAsset>("Space/"+assetName);
            if(!resource)throw new InvalidOperationException("Missing approved voxel model: "+assetName);
            var data=SpaceVoxelAsset.Read(resource.bytes);
            var obj=new GameObject("Approved space model · "+assetName);
            obj.transform.SetParent(parent,false);
            var visual=obj.AddComponent<SpaceVoxelVisual>();visual.Build(data,collision);
            return visual;
        }
        /// <summary>Derives crew decks, tower hatch, and real panoramic cockpit windows for
        /// runtime flight visibility while keeping ShuttleCabinCarving.Make pure for asset tests.</summary>
        public static SpaceVoxelVisual CreateWalkableShuttle(Transform parent)
        {
            var resource=Resources.Load<TextAsset>("Space/Shuttle");
            if(!resource)throw new InvalidOperationException("Missing approved shuttle model");
            var data=ShuttleCabinCarving.Make(SpaceVoxelAsset.Read(resource.bytes));
            data=data.RemoveCells((gx,y,gz)=>IsRuntimeWindowCutout(gx-ShuttleCabinCarving.ModelCenterX,y,gz-ShuttleCabinCarving.ModelCenterZ));
            var obj=new GameObject("Approved Shuttle · fitted crew volume");
            obj.transform.SetParent(parent,false);
            var visual=obj.AddComponent<SpaceVoxelVisual>();visual.Build(data,true);
            return visual;
        }
        static bool IsRuntimeWindowCutout(int lx,int y,int lz)
        {
            // Panoramic forward & brow cockpit windshield (y=43..47, x=-2..+2, z>=5)
            // plus port & starboard side cockpit windows (y=43..45, |x|>=3, z=3..6)
            // plus starboard lower-deck crew porthole (y=30..31, x>=3, z=3..4).
            if(y>=43&&y<=46&&lx>=-2&&lx<=2&&lz>=5)return true;
            if(y==47&&lx>=-1&&lx<=1&&lz>=4)return true;
            if(y>=43&&y<=45&&(lx<=-3||lx>=3)&&lz>=3&&lz<=6)return true;
            if(y>=30&&y<=31&&lx>=3&&lz>=3&&lz<=4)return true;
            return false;
        }
        /// <summary>Colliding launch pad with a derived lift well and pass-through in
        /// the tower partition. Approved pad bytes remain unchanged; not yet spawned
        /// into a production room or aligned with the game terrain.</summary>
        public static SpaceVoxelVisual CreateWalkableLaunchPad(Transform parent)
        {
            var resource=Resources.Load<TextAsset>("Space/LaunchPad");
            if(!resource)throw new InvalidOperationException("Missing approved launch pad model");
            var data=LaunchPadLiftOpening.Make(SpaceVoxelAsset.Read(resource.bytes));
            var obj=new GameObject("Approved LaunchPad · lift well and tower doorway");
            obj.transform.SetParent(parent,false);
            var visual=obj.AddComponent<SpaceVoxelVisual>();visual.Build(data,true);
            return visual;
        }
        void Build(SpaceVoxelAsset data,bool collision)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();
            var triangles=new List<int>[data.Palette.Length];
            for(int i=0;i<triangles.Length;i++)triangles[i]=new List<int>();
            Vector3 offset=new Vector3(data.X*.5f,0,data.Z*.5f);
            for(int x=0;x<data.X;x++)for(int y=0;y<data.Y;y++)for(int z=0;z<data.Z;z++)
            {
                byte cell=data.At(x,y,z);if(cell==0)continue;
                for(int face=0;face<6;face++)
                {
                    var dir=Directions[face];
                    if(data.At(x+dir.x,y+dir.y,z+dir.z)!=0)continue;
                    int start=vertices.Count;
                    for(int c=0;c<4;c++){vertices.Add(new Vector3(x,y,z)+Corners[face][c]-offset);normals.Add(dir);}
                    var group=triangles[cell];group.Add(start);group.Add(start+1);group.Add(start+2);
                    group.Add(start);group.Add(start+2);group.Add(start+3);
                }
            }
            mesh=new Mesh {name="Approved authored voxels",indexFormat=IndexFormat.UInt32};
            mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.subMeshCount=triangles.Length;
            for(int i=0;i<triangles.Length;i++)mesh.SetTriangles(triangles[i],i,false);
            mesh.RecalculateBounds();
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(!shader)throw new InvalidOperationException("Missing surface shader");
            materials=new Material[data.Palette.Length];
            for(int i=0;i<materials.Length;i++)
            {
                var p=data.Palette[i];var material=new Material(shader){name="Space · "+p.Name};
                bool isWindow=p.Name.IndexOf("window",StringComparison.OrdinalIgnoreCase)>=0;
                if(isWindow)
                {
                    Color glassCol=new Color(0.52f,0.82f,0.96f,0.16f);
                    material.color=glassCol;
                    if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",glassCol);
                    if(material.HasProperty("_Surface"))material.SetFloat("_Surface",1f);
                    if(material.HasProperty("_ZWrite"))material.SetFloat("_ZWrite",0f);
                    material.SetInt("_SrcBlend",5);
                    material.SetInt("_DstBlend",10);
                    material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    material.renderQueue=3000;
                    material.SetFloat("_Smoothness",.88f);
                }
                else
                {
                    material.color=new Color32(p.R,p.G,p.B,p.A);
                    if(p.Name.IndexOf("steel",StringComparison.OrdinalIgnoreCase)>=0)material.SetFloat("_Metallic",.65f);
                    material.SetFloat("_Smoothness",.18f);
                }
                materials[i]=material;
            }
            gameObject.AddComponent<MeshRenderer>().sharedMaterials=materials;
            if(collision)gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;
        }
        void OnDestroy()
        {
            if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
            if(materials!=null)foreach(var material in materials)if(material)
            {if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        }
    }
}
