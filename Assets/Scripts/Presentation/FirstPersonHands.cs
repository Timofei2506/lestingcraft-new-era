using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LestingCraft.Runtime;

namespace LestingCraft.Presentation
{
    /// <summary>Block-only first-person arm and held voxel objects; no rounded hand or finger primitives.</summary>
    public sealed class FirstPersonHands : MonoBehaviour
    {
        const int Layer=30;
        Camera view,main;Transform hand;FirstPersonController player;UniversalAdditionalCameraData mainData;
        Material character,objects;MeshFilter held;int shownItem=-1;float phase,strike;int previousMask;bool initialized;
        public Camera ViewCamera=>view;
        public void Initialize(FirstPersonController owner,Camera camera)
        {
            if(initialized)Release();initialized=true;
            player=owner;main=camera;previousMask=main.cullingMask;main.cullingMask&=~(1<<Layer);
            var obj=new GameObject("First-person viewmodel camera");obj.transform.SetParent(camera.transform,false);
            view=obj.AddComponent<Camera>();view.cullingMask=1<<Layer;view.nearClipPlane=.01f;view.farClipPlane=3;view.fieldOfView=60;view.allowHDR=true;
            mainData=main.GetUniversalAdditionalCameraData();var data=view.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Overlay;data.renderPostProcessing=false;data.renderShadows=false;mainData.cameraStack.Add(view);
            character=BlockGeometry.CharacterMaterial();objects=new Material(Resources.Load<Material>("Generated/VoxelObjects"));
            hand=new GameObject("Block right arm and held object").transform;hand.SetParent(view.transform,false);
            Part("Cuboid sleeve",new Vector3(0,-.23f,0),new Vector3(.14f,.35f,.16f),1);
            Part("Square cuff",new Vector3(0,-.045f,0),new Vector3(.15f,.055f,.17f),6);
            Part("Cuboid fist",new Vector3(0,.055f,0),new Vector3(.138f,.15f,.16f),0);
            Part("Sleeve stitched panel",new Vector3(.072f,-.18f,0),new Vector3(.008f,.15f,.11f),7);
            var item=new GameObject("Held voxel object",typeof(MeshFilter),typeof(MeshRenderer));item.layer=Layer;item.transform.SetParent(hand,false);
            item.transform.localPosition=new Vector3(-.025f,.24f,.04f);item.transform.localRotation=Quaternion.Euler(-12,-25,-12);item.transform.localScale=Vector3.one*.32f;
            held=item.GetComponent<MeshFilter>();item.GetComponent<MeshRenderer>().sharedMaterial=objects;item.GetComponent<MeshRenderer>().shadowCastingMode=ShadowCastingMode.Off;
            Pose(0);view.enabled=false;
        }
        Transform Part(string name,Vector3 position,Vector3 scale,int tile)
        {
            var part=BlockGeometry.Part(name,hand,position,scale,character,tile,-1,Layer);
            var renderer=part.GetComponent<MeshRenderer>();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return part;
        }
        void LateUpdate()
        {
            if(!view||!player)return;
            view.enabled=player.Active&&player.World&&player.World.Ready;
            if(!view.enabled)return;
            int item=player.Game&&player.Game.Session&&player.Game.Session.Inventory.Active.count>0?player.Game.Session.Inventory.Active.item:0;
            if(item!=shownItem){shownItem=item;held.sharedMesh=item>0?VoxelItemModels.MeshFor(item):null;held.transform.localScale=Vector3.one*(item>=100&&item<=103?.45f:.28f);held.gameObject.SetActive(held.sharedMesh);}

            var keyboard=Keyboard.current;bool walking=keyboard!=null&&(keyboard.wKey.isPressed||keyboard.aKey.isPressed||keyboard.sKey.isPressed||keyboard.dKey.isPressed);
            phase+=Time.deltaTime*(walking?(player.Sprinting?12:8):1.8f);
            var mouse=Mouse.current;if(mouse!=null&&(mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame))strike=1;
            strike=Mathf.MoveTowards(strike,0,Time.deltaTime*4);
            Pose(walking?.012f:.003f);
        }
        void Pose(float bob)
        {
            float swing=Mathf.Sin(strike*Mathf.PI);
            hand.localPosition=new Vector3(.30f+Mathf.Sin(phase)*bob,-.31f+Mathf.Cos(phase*2)*bob-swing*.045f,.51f+swing*.12f);
            hand.localRotation=Quaternion.Euler(-18-swing*24,-12,-12+Mathf.Sin(phase)*bob*150);
        }
        /// <summary>Explicit owner cleanup also works in EditMode and before deferred player destruction.</summary>
        public void Release()
        {
            if(!initialized)return;initialized=false;
            if(mainData&&view)mainData.cameraStack.Remove(view);if(main)main.cullingMask=previousMask;
            if(view){view.enabled=false;if(Application.isPlaying)Destroy(view.gameObject);else DestroyImmediate(view.gameObject);}
            if(character){if(Application.isPlaying)Destroy(character);else DestroyImmediate(character);}if(objects){if(Application.isPlaying)Destroy(objects);else DestroyImmediate(objects);}
            view=null;hand=null;character=null;objects=null;held=null;shownItem=-1;mainData=null;main=null;
        }
        void OnDestroy(){Release();}
    }
}
