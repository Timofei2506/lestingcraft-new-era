using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using LestingCraft.Runtime;

namespace LestingCraft.Presentation
{
    /// <summary>Articulated procedural first-person hand; a viewmodel, not a final skinned character asset.</summary>
    public sealed class FirstPersonHands : MonoBehaviour
    {
        const int Layer=30;
        Camera view,main;Transform hand;FirstPersonController player;UniversalAdditionalCameraData mainData;
        Material skin,cloth;float phase,strike;int previousMask;
        public Camera ViewCamera=>view;
        public void Initialize(FirstPersonController owner,Camera camera)
        {
            player=owner;main=camera;previousMask=main.cullingMask;main.cullingMask&=~(1<<Layer);
            var obj=new GameObject("First-person viewmodel camera");obj.transform.SetParent(camera.transform,false);
            view=obj.AddComponent<Camera>();view.cullingMask=1<<Layer;view.nearClipPlane=.01f;view.farClipPlane=3;view.fieldOfView=60;view.allowHDR=true;
            mainData=main.GetUniversalAdditionalCameraData();var data=view.GetUniversalAdditionalCameraData();data.renderType=CameraRenderType.Overlay;data.clearDepth=true;data.renderPostProcessing=false;data.renderShadows=false;mainData.cameraStack.Add(view);
            var baseMaterial=Resources.Load<Material>("Generated/Avatar");
            skin=new Material(baseMaterial);skin.SetColor("_BaseColor",new Color(.63f,.43f,.30f));skin.SetFloat("_Smoothness",.23f);
            cloth=new Material(baseMaterial);cloth.SetColor("_BaseColor",new Color(.15f,.22f,.18f));cloth.SetFloat("_Smoothness",.12f);
            hand=new GameObject("Right hand and sleeve").transform;hand.SetParent(view.transform,false);
            Part("Sleeve",PrimitiveType.Capsule,new Vector3(0,-.25f,-.01f),new Vector3(.13f,.24f,.13f),cloth);
            Part("Cuff",PrimitiveType.Cylinder,new Vector3(0,-.055f,0),new Vector3(.135f,.026f,.135f),cloth);
            Part("Wrist",PrimitiveType.Capsule,new Vector3(0,.005f,0),new Vector3(.085f,.09f,.074f),skin);
            Part("Palm",PrimitiveType.Sphere,new Vector3(0,.095f,.012f),new Vector3(.133f,.173f,.083f),skin);
            for(int i=0;i<4;i++)
            {
                float x=(i-1.5f)*.030f,y=.162f-Mathf.Abs(i-1.5f)*.009f;
                var finger=Part("Finger "+i,PrimitiveType.Capsule,new Vector3(x,y,.039f),new Vector3(.030f,.047f,.031f),skin);
                finger.localRotation=Quaternion.Euler(55,0,(i-1.5f)*-6);
                Part("Knuckle "+i,PrimitiveType.Sphere,new Vector3(x,y-.008f,.017f),Vector3.one*.037f,skin);
            }
            var thumb=Part("Thumb",PrimitiveType.Capsule,new Vector3(-.078f,.080f,.045f),new Vector3(.043f,.063f,.041f),skin);thumb.localRotation=Quaternion.Euler(25,0,-37);
            Pose(0);view.enabled=false;
        }
        Transform Part(string name,PrimitiveType kind,Vector3 position,Vector3 scale,Material material)
        {
            var obj=GameObject.CreatePrimitive(kind);obj.name=name;obj.layer=Layer;obj.transform.SetParent(hand,false);obj.transform.localPosition=position;obj.transform.localScale=scale;
            var collider=obj.GetComponent<Collider>();collider.enabled=false;if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            var renderer=obj.GetComponent<Renderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;return obj.transform;
        }
        void LateUpdate()
        {
            if(!view||!player)return;
            view.enabled=player.Active&&player.World&&player.World.Ready;
            if(!view.enabled)return;
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
        void OnDestroy()
        {
            if(mainData&&view)mainData.cameraStack.Remove(view);if(main)main.cullingMask=previousMask;
            if(view){if(Application.isPlaying)Destroy(view.gameObject);else DestroyImmediate(view.gameObject);}
            if(skin){if(Application.isPlaying)Destroy(skin);else DestroyImmediate(skin);}if(cloth){if(Application.isPlaying)Destroy(cloth);else DestroyImmediate(cloth);}
        }
    }
}
