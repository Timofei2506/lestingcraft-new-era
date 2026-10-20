using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Runtime
{
    /// <summary>Same placed pad and orbiter for every client in a room. Both approved
    /// resources are copied into colliding meshes; their source bytes are untouched.
    /// The ramp starts on the existing world surface and ends on the real pad slab.
    /// This is geometry only: opening the hatch and moving the lift require room authority.</summary>
    public sealed class LaunchSiteVisual : MonoBehaviour
    {
        public const int PadX=168,PadZ=168,LiftWorldX=148,RampStartZ=58,RampEndZ=140;
        readonly List<Material> materials=new List<Material>();
        public VerticalShuttleInteriorVisual Cabin {get;private set;}
        public static Vector3Int OfflineSite(int seed)
        {
            int highest=0;
            for(int x=PadX-47;x<=PadX+46;x++)for(int z=PadZ-45;z<=PadZ+44;z++)
                highest=Math.Max(highest,TerrainGenerator.Height(x,z,seed,Dimension.Lestingia));
            for(int x=LiftWorldX-2;x<=LiftWorldX+2;x++)for(int z=RampStartZ;z<=PadZ+3;z++)
                highest=Math.Max(highest,TerrainGenerator.Height(x,z,seed,Dimension.Lestingia));
            return new Vector3Int(PadX,highest+4,PadZ);
        }
        public static LaunchSiteVisual Create(Transform parent,Vector3Int site,int seed)
        {
            if(site.x!=PadX||site.z!=PadZ||site.y<40||site.y>140)
                throw new ArgumentException("Invalid server-owned launch site");
            var node=new GameObject("Shared physical launch site · boarding not active yet");
            node.transform.SetParent(parent,false);node.transform.position=site;
            var visual=node.AddComponent<LaunchSiteVisual>();
            SpaceVoxelVisual.CreateWalkableLaunchPad(node.transform);
            var shuttle=new GameObject("Approved upright Shuttle · pad offset +2 m");
            shuttle.transform.SetParent(node.transform,false);
            shuttle.transform.localPosition=new Vector3(0,2,0);
            visual.Cabin=VerticalShuttleInteriorVisual.Create(shuttle.transform);
            visual.BuildAccess(seed,site.y);
            return visual;
        }
        Transform Box(string name,Vector3 pos,Vector3 size,Material material)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name=name;part.transform.SetParent(transform,false);
            part.transform.localPosition=pos;part.transform.localScale=size;
            part.GetComponent<MeshRenderer>().sharedMaterial=material;
            return part.transform;
        }
        void BuildAccess(int seed,int siteY)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(!shader)throw new InvalidOperationException("Missing launch site shader");
            var rampMaterial=new Material(shader){name="Approach ramp · steel",color=new Color(.38f,.47f,.52f)};
            var supportMaterial=new Material(shader){name="Foundation supports · steel",color=new Color(.25f,.33f,.39f)};
            materials.Add(rampMaterial);materials.Add(supportMaterial);
            const int count=RampEndZ-RampStartZ;
            var top=new float[count+1];
            float start=0;
            for(int x=LiftWorldX-2;x<=LiftWorldX+2;x++)
                start=Mathf.Max(start,TerrainGenerator.Height(x,RampStartZ,seed,Dimension.Lestingia)+1);
            float end=siteY+5;
            for(int i=0;i<=count;i++)
            {
                int z=RampStartZ+i;
                float surface=0;
                for(int x=LiftWorldX-2;x<=LiftWorldX+2;x++)
                    surface=Mathf.Max(surface,TerrainGenerator.Height(x,z,seed,Dimension.Lestingia)+1);
                top[i]=Mathf.Max(surface,Mathf.Lerp(start,end,(float)i/count));
            }
            top[count]=end;
            // Every metre remains a collision-walkable slope (< 46 degrees). Raise
            // earlier sections over intervening hills; never cut or overwrite terrain.
            for(int i=count-1;i>=0;i--)top[i]=Mathf.Max(top[i],top[i+1]-.75f);
            for(int i=1;i<=count;i++)top[i]=Mathf.Max(top[i],top[i-1]-.75f);
            for(int i=0;i<count;i++)
            {
                float a=top[i]-siteY,b=top[i+1]-siteY,delta=b-a;
                var segment=Box("Walkable ramp "+i,
                    new Vector3(LiftWorldX-PadX,(a+b)*.5f-.12f,RampStartZ+i+.5f-PadZ),
                    new Vector3(3.8f,.24f,Mathf.Sqrt(1+delta*delta)+.04f),rampMaterial);
                segment.localRotation=Quaternion.Euler(-Mathf.Atan(delta)*Mathf.Rad2Deg,0,0);
                if(i%8==0)Support("Ramp stilt "+i,LiftWorldX,RampStartZ+i,top[i]-.25f,seed,supportMaterial);
            }
            // Support the elevated pad at its edges instead of erasing the terrain.
            for(int x=PadX-40;x<=PadX+40;x+=10)
                for(int zIndex=0;zIndex<2;zIndex++)Support("Pad edge support",x,zIndex==0?PadZ-37:PadZ+37,siteY+1,seed,supportMaterial);
            for(int z=PadZ-27;z<=PadZ+27;z+=9)
                for(int xIndex=0;xIndex<2;xIndex++)Support("Pad side support",xIndex==0?PadX-40:PadX+40,z,siteY+1,seed,supportMaterial);
        }
        void Support(string label,int x,int z,float top,int seed,Material material)
        {
            float natural=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia)+1;
            if(top<=natural+.2f)return;
            Box(label,new Vector3(x-PadX,(natural+top)*.5f-transform.position.y, z-PadZ),
                new Vector3(.32f,top-natural,.32f),material);
        }
        void OnDestroy(){foreach(var material in materials)if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
    }
}
