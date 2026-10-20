using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.World;
using LestingCraft.Networking;

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
        Vector3 groundedOrbiterLocal;
        int siteX,siteZ;
        public Transform ShuttleRoot {get;private set;}
        public VerticalShuttleInteriorVisual Cabin {get;private set;}
        public static Vector3Int OfflineSite(int seed)=>OfflineSite(seed,PadX,PadZ);
        public static Vector3Int OfflineSite(int seed,int x,int z)
        {
            if(x < -320||x > 320||z < -260||z > 320)throw new ArgumentException("Launch site outside room");
            int highest=0;
            for(int px=x-47;px<=x+46;px++)for(int pz=z-45;pz<=z+44;pz++)
                highest=Math.Max(highest,TerrainGenerator.Height(px,pz,seed,Dimension.Lestingia));
            for(int px=x-22;px<=x-18;px++)for(int pz=z-110;pz<=z+3;pz++)
                highest=Math.Max(highest,TerrainGenerator.Height(px,pz,seed,Dimension.Lestingia));
            return new Vector3Int(x,highest+4,z);
        }
        public static LaunchSiteVisual Create(Transform parent,Vector3Int site,int seed)
        {
            if(site.x < -320||site.x > 320||site.z < -260||site.z > 320||site.y<40||site.y>140)
                throw new ArgumentException("Invalid server-owned launch site");
            var node=new GameObject("Shared physical launch site · flight not active yet");
            node.transform.SetParent(parent,false);node.transform.position=site;
            var visual=node.AddComponent<LaunchSiteVisual>();visual.siteX=site.x;visual.siteZ=site.z;
            SpaceVoxelVisual.CreateWalkableLaunchPad(node.transform);
            var shuttle=new GameObject("Approved upright Shuttle · pad offset +2 m");
            shuttle.transform.SetParent(node.transform,false);
            shuttle.transform.localPosition=new Vector3(0,2,0);
            visual.ShuttleRoot=shuttle.transform;
            visual.groundedOrbiterLocal=shuttle.transform.localPosition;
            visual.Cabin=VerticalShuttleInteriorVisual.Create(shuttle.transform);
            if(!visual.Cabin.AnchorLiftToPad(node.transform))throw new InvalidOperationException("Pad infrastructure anchor failed");
            visual.BuildAccess(seed,site.y);
            return visual;
        }
        /// <summary>A bounded visual projection of the authoritative double-precision
        /// Earth-Moon position. There is one continuous coordinate frame; metres are
        /// scaled for the camera, never replaced with unrelated sector coordinates.
        /// Ground geometry is NOT parented to ShuttleRoot. The lunar surface still
        /// needs a separate physically walkable client before launch is enabled.</summary>
        public static bool TryMoonFlightOffset(NetSpaceMission mission,out Vector3 offset)
        {
            offset=Vector3.zero;
            if(mission==null||!mission.active||mission.from!="earth"||mission.to!="moon"||
               mission.seconds!=1800||mission.position==null||mission.position.Length!=3||
               double.IsNaN(mission.elapsed)||double.IsInfinity(mission.elapsed)||
               mission.elapsed<0||mission.elapsed>mission.seconds||
               Array.Exists(mission.position,v=>double.IsNaN(v)||double.IsInfinity(v)))return false;
            double x=(mission.position[0]-149597870700d)/1000000d;
            double z=mission.position[2]/1000000d;
            if(Math.Abs(x)>1000||Math.Abs(z)>1000||Math.Abs(mission.position[1])>1000000000d)return false;
            float altitude=2f*Mathf.Min((float)mission.elapsed,630f);
            if(mission.elapsed>1620)altitude*=Mathf.Clamp01((float)((1800-mission.elapsed)/180d));
            offset=new Vector3((float)x,altitude,(float)z);
            return true;
        }
        public bool SyncMoonFlight(NetSpaceMission mission)
        {
            if(!ShuttleRoot||!TryMoonFlightOffset(mission,out var offset))return false;
            ShuttleRoot.localPosition=groundedOrbiterLocal+offset;
            return true;
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
            int padX=siteX,padZ=siteZ,liftWorldX=siteX-20,rampStartZ=siteZ-110,rampEndZ=siteZ-28;
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(!shader)throw new InvalidOperationException("Missing launch site shader");
            var rampMaterial=new Material(shader){name="Approach ramp · steel",color=new Color(.38f,.47f,.52f)};
            var supportMaterial=new Material(shader){name="Foundation supports · steel",color=new Color(.25f,.33f,.39f)};
            materials.Add(rampMaterial);materials.Add(supportMaterial);
            int count=rampEndZ-rampStartZ;
            var top=new float[count+1];
            float start=0;
            for(int x=liftWorldX-2;x<=liftWorldX+2;x++)
                start=Mathf.Max(start,TerrainGenerator.Height(x,rampStartZ,seed,Dimension.Lestingia)+1);
            float end=siteY+5;
            for(int i=0;i<=count;i++)
            {
                int z=rampStartZ+i;
                float surface=0;
                for(int x=liftWorldX-2;x<=liftWorldX+2;x++)
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
                    new Vector3(liftWorldX-padX,(a+b)*.5f-.12f,rampStartZ+i+.5f-padZ),
                    new Vector3(3.8f,.24f,Mathf.Sqrt(1+delta*delta)+.04f),rampMaterial);
                segment.localRotation=Quaternion.Euler(-Mathf.Atan(delta)*Mathf.Rad2Deg,0,0);
                if(i%8==0)Support("Ramp stilt "+i,liftWorldX,rampStartZ+i,top[i]-.25f,seed,supportMaterial);
            }
            // Support the elevated pad at its edges instead of erasing the terrain.
            for(int x=padX-40;x<=padX+40;x+=10)
                for(int zIndex=0;zIndex<2;zIndex++)Support("Pad edge support",x,zIndex==0?padZ-37:padZ+37,siteY+1,seed,supportMaterial);
            for(int z=padZ-27;z<=padZ+27;z+=9)
                for(int xIndex=0;xIndex<2;xIndex++)Support("Pad side support",xIndex==0?padX-40:padX+40,z,siteY+1,seed,supportMaterial);
        }
        void Support(string label,int x,int z,float top,int seed,Material material)
        {
            float natural=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia)+1;
            if(top<=natural+.2f)return;
            Box(label,new Vector3(x-siteX,(natural+top)*.5f-transform.position.y, z-siteZ),
                new Vector3(.32f,top-natural,.32f),material);
        }
        void OnDestroy(){foreach(var material in materials)if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
    }
}
