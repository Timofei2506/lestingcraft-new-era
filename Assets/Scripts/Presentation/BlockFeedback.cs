using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Runtime;

namespace LestingCraft.Presentation
{
    /// <summary>Persistent target outline + growing cracks + pooled 3D voxel break debris. No collider, shadow or interaction interception.</summary>
    public sealed class BlockFeedback:MonoBehaviour
    {
        public static BlockFeedback ActiveInstance {get;private set;}
        struct DebrisChip
        {
            public Transform tr;
            public MeshRenderer mr;
            public Vector3 vel;
            public Vector3 spin;
            public float life;
            public float maxLife;
            public float baseScale;
        }
        FirstPersonController player;Transform visualRoot;public Transform VisualRoot=>visualRoot;GameObject surface;Material damage;LineRenderer outline;Material line;
        readonly DebrisChip[] debris=new DebrisChip[20];
        MaterialPropertyBlock chipMpb;
        Material chipMaterial;
        int debrisCursor;

        public void Initialize(FirstPersonController owner)
        {
            if(visualRoot)return;player=owner;ActiveInstance=this;visualRoot=new GameObject("World block feedback").transform;visualRoot.SetParent(owner.World?owner.World.transform:null,false);damage=new Material(Shader.Find("LestingCraft/BlockDamage"));
            surface=BlockGeometry.Part("Target cracks",visualRoot,Vector3.zero,Vector3.one*1.004f,damage,0).gameObject;
            var renderer=surface.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            // One continuous 12-edge wire, repeated edges are harmless and avoid twelve objects.
            var obj=new GameObject("Block outline");obj.transform.SetParent(visualRoot,false);outline=obj.AddComponent<LineRenderer>();
            line=new Material(Shader.Find("LestingCraft/BlockDamage"));line.SetFloat("_Outline",1);line.SetColor("_OutlineColor",new Color(.9f,.88f,.72f,.85f));outline.sharedMaterial=line;
            outline.useWorldSpace=false;outline.widthMultiplier=.009f;outline.shadowCastingMode=ShadowCastingMode.Off;outline.receiveShadows=false;
            var points=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(0,1,0),new Vector3(0,0,0),new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(1,0,0),new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(1,1,0),new Vector3(1,1,1),new Vector3(0,1,1),new Vector3(0,1,0),new Vector3(0,1,1),new Vector3(0,0,1)};
            for(int i=0;i<points.Length;i++)points[i]=(points[i]-Vector3.one*.5f)*1.006f;
            outline.positionCount=points.Length;outline.SetPositions(points);surface.SetActive(false);outline.enabled=false;

            var sh=Shader.Find("Universal Render Pipeline/Simple Lit")??Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Sprites/Default");
            chipMaterial=new Material(sh);
            chipMpb=new MaterialPropertyBlock();
            var debrisParent=owner.World?owner.World.transform:null;
            var cubeMesh=BlockGeometry.Box(0);
            for(int i=0;i<debris.Length;i++)
            {
                var cObj=new GameObject("VoxelChip_"+i,typeof(MeshFilter),typeof(MeshRenderer));
                if(debrisParent)cObj.transform.SetParent(debrisParent,false);
                cObj.GetComponent<MeshFilter>().sharedMesh=cubeMesh;
                var cmr=cObj.GetComponent<MeshRenderer>();
                cmr.sharedMaterial=chipMaterial;
                cmr.shadowCastingMode=ShadowCastingMode.Off;
                cmr.receiveShadows=false;
                cObj.SetActive(false);
                debris[i]=new DebrisChip{tr=cObj.transform,mr=cmr};
            }
        }

        public static void SpawnBreakDebris(Vector3Int cell,ushort blockId)
        {
            if(!ActiveInstance||blockId==0)return;
            ActiveInstance.EmitDebris((Vector3)cell+Vector3.one*.5f,blockId);
        }

        static Color DebrisColorFor(ushort id)
        {
            if(id==3||id==5||id==44)return new Color(.36f,.64f,.28f,1f);
            if(id==60||id==65)return new Color(.84f,.54f,.74f,1f);
            if(id==2||id==62)return new Color(.52f,.36f,.24f,1f);
            if(id==6)return new Color(.84f,.76f,.52f,1f);
            if(id==4||id==16||id==18||id==57||id==58)return new Color(.58f,.42f,.25f,1f);
            if(id==14||id==63)return new Color(.86f,.93f,.98f,1f);
            if(id==11||id==67)return new Color(.35f,.85f,.95f,1f);
            if(id>=70&&id<=73)return new Color(.25f,.90f,.65f,1f);
            return new Color(.54f,.56f,.58f,1f);
        }

        void EmitDebris(Vector3 center,ushort blockId)
        {
            Color baseCol=DebrisColorFor(blockId);
            for(int k=0;k<12;k++)
            {
                int idx=debrisCursor;
                debrisCursor=(debrisCursor+1)%debris.Length;
                ref var c=ref debris[idx];
                if(!c.tr)continue;
                Vector3 jitter=new Vector3(Random.Range(-.32f,.32f),Random.Range(-.28f,.32f),Random.Range(-.32f,.32f));
                c.tr.position=center+jitter;
                c.tr.rotation=Random.rotation;
                c.baseScale=Random.Range(.075f,.145f);
                c.tr.localScale=Vector3.one*c.baseScale;
                c.vel=jitter.normalized*Random.Range(1.4f,3.4f)+Vector3.up*Random.Range(1.6f,3.5f);
                c.spin=new Vector3(Random.Range(-360f,360f),Random.Range(-360f,360f),Random.Range(-360f,360f));
                c.maxLife=Random.Range(.38f,.68f);
                c.life=c.maxLife;
                Color shaded=baseCol*Random.Range(.82f,1.15f);shaded.a=1f;
                chipMpb.SetColor("_BaseColor",shaded);
                chipMpb.SetColor("_Color",shaded);
                c.mr.SetPropertyBlock(chipMpb);
                c.tr.gameObject.SetActive(true);
            }
        }

        void LateUpdate()
        {
            float dt=Time.deltaTime;
            for(int i=0;i<debris.Length;i++)
            {
                if(debris[i].life<=0f||!debris[i].tr)continue;
                ref var c=ref debris[i];
                c.life-=dt;
                if(c.life<=0f){c.tr.gameObject.SetActive(false);continue;}
                c.vel+=Vector3.down*(14.5f*dt);
                c.tr.position+=c.vel*dt;
                c.tr.Rotate(c.spin*dt,Space.Self);
                c.tr.localScale=Vector3.one*(c.baseScale*Mathf.Clamp01(c.life/(c.maxLife*.45f)));
            }
            bool active=player&&player.Active&&player.HasTarget&&player.Game&&player.Game.Session;
            if(outline)outline.enabled=active;if(!surface)return;surface.SetActive(active);
            if(!active)return;
            visualRoot.position=(Vector3)player.Target.Cell+Vector3.one*.5f;
            ushort id=player.World?player.World.GetBlock(player.Target.Cell.x,player.Target.Cell.y,player.Target.Cell.z):(ushort)0;
            SetShape(id);
            float progress=player.Game.Session.MiningProgress;damage.SetFloat("_Progress",progress);
            var color=Color.Lerp(new Color(.70f,.78f,.65f),new Color(1,.77f,.32f),progress);
            line.SetColor("_OutlineColor",color);
        }
        public void SetShape(ushort id)
        {
            bool authored=id==11||id==15;var mesh=authored?VoxelItemModels.MeshFor(id):BlockGeometry.Box(0);
            surface.GetComponent<MeshFilter>().sharedMesh=mesh;
            surface.transform.localPosition=authored?PlacedVoxelModel.Offset(id)-Vector3.one*.5f:Vector3.zero;
            surface.transform.localScale=Vector3.one*(authored?PlacedVoxelModel.Scale*1.004f:1.004f);
            outline.transform.localPosition=surface.transform.localPosition;
            outline.transform.localScale=authored?mesh.bounds.size*PlacedVoxelModel.Scale:Vector3.one;
        }
        static void Release(Object obj){if(!obj)return;if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}
        void OnDestroy()
        {
            if(ActiveInstance==this)ActiveInstance=null;
            for(int i=0;i<debris.Length;i++)if(debris[i].tr)Release(debris[i].tr.gameObject);
            if(visualRoot)Release(visualRoot.gameObject);Release(damage);Release(line);Release(chipMaterial);
        }
    }
}
