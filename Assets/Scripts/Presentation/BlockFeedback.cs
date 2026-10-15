using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Runtime;

namespace LestingCraft.Presentation
{
    /// <summary>Persistent target outline + growing cracks. No collider, shadow or interaction interception.</summary>
    public sealed class BlockFeedback:MonoBehaviour
    {
        FirstPersonController player;GameObject surface;Material damage;LineRenderer outline;Material line;
        public void Initialize(FirstPersonController owner)
        {
            player=owner;damage=new Material(Shader.Find("LestingCraft/BlockDamage"));
            surface=BlockGeometry.Part("Target cracks",transform,Vector3.zero,Vector3.one*1.004f,damage,0).gameObject;
            var renderer=surface.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            // One continuous 12-edge wire, repeated edges are harmless and avoid twelve objects.
            var obj=new GameObject("Block outline");obj.transform.SetParent(transform,false);outline=obj.AddComponent<LineRenderer>();
            line=new Material(Shader.Find("Universal Render Pipeline/Unlit"));line.SetColor("_BaseColor",new Color(.9f,.88f,.72f,.85f));outline.sharedMaterial=line;
            outline.useWorldSpace=false;outline.widthMultiplier=.009f;outline.shadowCastingMode=ShadowCastingMode.Off;outline.receiveShadows=false;
            var points=new[]{new Vector3(0,0,0),new Vector3(1,0,0),new Vector3(1,1,0),new Vector3(0,1,0),new Vector3(0,0,0),new Vector3(0,0,1),new Vector3(1,0,1),new Vector3(1,0,0),new Vector3(1,0,1),new Vector3(1,1,1),new Vector3(1,1,0),new Vector3(1,1,1),new Vector3(0,1,1),new Vector3(0,1,0),new Vector3(0,1,1),new Vector3(0,0,1)};
            for(int i=0;i<points.Length;i++)points[i]=(points[i]-Vector3.one*.5f)*1.006f;
            outline.positionCount=points.Length;outline.SetPositions(points);surface.SetActive(false);outline.enabled=false;
        }
        void LateUpdate()
        {
            bool active=player&&player.Active&&player.HasTarget&&player.Game&&player.Game.Session;
            if(outline)outline.enabled=active;if(!surface)return;surface.SetActive(active);
            if(!active)return;
            transform.position=(Vector3)player.Target.Cell+Vector3.one*.5f;
            float progress=player.Game.Session.MiningProgress;damage.SetFloat("_Progress",progress);
            var color=Color.Lerp(new Color(.70f,.78f,.65f),new Color(1,.77f,.32f),progress);
            outline.startColor=outline.endColor=color;
        }
        void OnDestroy(){if(damage)Destroy(damage);if(line)Destroy(line);}
    }
}
