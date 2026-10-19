using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Story;

namespace LestingCraft.Presentation
{
    [Serializable] public sealed class PlanetBossPartDefinition
    {
        public string name;
        public string motion;
        public float[] pivot;
        public int[] cells;
    }
    [Serializable] public sealed class PlanetBossDefinition
    {
        public int version;
        public string slug,name,planet;
        public float unit;
        public GuardianPalette[] palette;
        public PlanetBossPartDefinition[] parts;
    }

    /// <summary>
    /// Real PBR 3D voxel meshes, vertex-tint emission and separately pivoted parts from
    /// Resources/Models/PlanetBoss*.json. Uses the same hidden-face mesher and material
    /// as the already-shipping Root Guardian; one Unity mesh per articulated part.
    /// This is a model + idle rig only, NOT a combat AI, encounter or planet spawner.
    /// </summary>
    public sealed class PlanetBossVisual : MonoBehaviour
    {
        sealed class Joint
        {
            public Transform Transform;
            public string Motion;
            public float Sign;
        }
        readonly List<Mesh> meshes = new List<Mesh>();
        readonly List<Joint> joints = new List<Joint>();
        PlanetBossDefinition model;
        public string Planet { get { return model != null ? model.planet : ""; } }
        public int PartCount { get { return joints.Count; } }
        public int VoxelCount { get; private set; }
        public int ExposedQuads { get; private set; }

        public static PlanetBossVisual Build(Transform parent, string slug)
        {
            if(slug!="Mars"&&slug!="Venus"&&slug!="Mercury")throw new ArgumentException("Unknown planet boss model", "slug");
            var asset=Resources.Load<TextAsset>("Models/PlanetBoss"+slug);
            if(!asset)throw new InvalidOperationException("Planet boss 3D asset missing: "+slug);
            var data=JsonUtility.FromJson<PlanetBossDefinition>(asset.text);
            if(data==null||data.version!=1||data.slug!=slug||data.unit<=0||data.parts==null||data.parts.Length<6||data.palette==null)
                throw new InvalidOperationException("Planet boss 3D asset is invalid: "+slug);
            var material=Resources.Load<Material>("Generated/VoxelObjects");
            if(!material)throw new InvalidOperationException("PBR object material is missing");
            var root=new GameObject(data.name);
            root.transform.SetParent(parent,false);
            var visual=root.AddComponent<PlanetBossVisual>();
            try { visual.Construct(data,material); }
            catch { if(Application.isPlaying)Destroy(root);else DestroyImmediate(root);throw; }
            return visual;
        }

        void Construct(PlanetBossDefinition data, Material material)
        {
            model=data;
            foreach(var part in data.parts)
            {
                if(part==null||part.pivot==null||part.pivot.Length!=3||part.cells==null||part.cells.Length==0||part.cells.Length%4!=0)
                    throw new InvalidOperationException("Invalid planetary boss part: "+data.name);
                // Part cells are relative to pivot; the pivot is a true rig joint in world metres.
                var body=new GuardianDefinition {version=2,name=part.name,unit=data.unit,palette=data.palette,cells=part.cells};
                Color[] colors;byte[] materialIds;
                var mesh=GuardianSculpture.CreateMesh(body,out colors,out materialIds);
                meshes.Add(mesh);
                if(mesh.vertexCount==0||mesh.triangles.Length<3)throw new InvalidOperationException("Empty boss mesh part: "+part.name);
                VoxelCount+=part.cells.Length/4;
                ExposedQuads+=mesh.vertexCount/4;
                var node=new GameObject(part.name,typeof(MeshFilter),typeof(MeshRenderer),typeof(BoxCollider));
                node.transform.SetParent(transform,false);
                node.transform.localPosition=new Vector3(part.pivot[0],part.pivot[1],part.pivot[2])*data.unit;
                node.GetComponent<MeshFilter>().sharedMesh=mesh;
                var renderer=node.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;
                renderer.shadowCastingMode=ShadowCastingMode.On;
                // One cheap hit/collision proxy per moving limb, never per voxel or an
                // invisible box across the entire silhouette and open gaps between legs.
                var hitbox=node.GetComponent<BoxCollider>();
                hitbox.center=mesh.bounds.center;hitbox.size=mesh.bounds.size;
                joints.Add(new Joint {Transform=node.transform,Motion=part.motion,Sign=part.name.StartsWith("L")||part.name.StartsWith("left")?-1f:1f});
            }
        }
        void Update()
        {
            float t=Time.time;
            foreach(var joint in joints)
            {
                float phase=joint.Sign<0?Mathf.PI:0f;
                switch(joint.Motion)
                {
                    case "arm": joint.Transform.localRotation=Quaternion.Euler(0,0,joint.Sign*Mathf.Sin(t*1.1f+phase)*8f);break;
                    case "stride": joint.Transform.localRotation=Quaternion.Euler(Mathf.Sin(t*.65f+phase)*4f,0,0);break;
                    case "mirror": joint.Transform.localRotation=Quaternion.Euler(0,0,joint.Sign*Mathf.Sin(t*.48f+phase)*3f);break;
                    case "tail": joint.Transform.localRotation=Quaternion.Euler(0,Mathf.Sin(t*.8f)*5f,0);break;
                    case "look": joint.Transform.localRotation=Quaternion.Euler(0,Mathf.Sin(t*.6f)*6f,0);break;
                    case "breathe": joint.Transform.localScale=Vector3.one*(1f+.008f*Mathf.Sin(t*1.3f));break;
                }
            }
        }
        void OnDestroy()
        {
            foreach(var mesh in meshes)if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}
            meshes.Clear();
        }
    }
}
