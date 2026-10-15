using UnityEngine;
namespace LestingCraft.Presentation
{
    /// <summary>Original block adventurer: cuboid head, torso, arms and legs, textured cloth and equipment.</summary>
    public sealed class CharacterVisual : MonoBehaviour
    {
        Transform leftLeg,rightLeg,leftArm,rightArm;Vector3 previous;float phase;Material material;
        public void Build()
        {
            material=BlockGeometry.CharacterMaterial();
            Part("Block torso",new Vector3(0,1.04f,0),new Vector3(.48f,.56f,.27f),1,transform);
            Part("Belt",new Vector3(0,.80f,.012f),new Vector3(.50f,.085f,.30f),6,transform);
            Part("Square buckle",new Vector3(0,.80f,.174f),new Vector3(.095f,.07f,.018f),5,transform);
            Part("Backpack",new Vector3(0,1.04f,-.23f),new Vector3(.36f,.43f,.23f),6,transform);
            Part("Backpack flap",new Vector3(0,1.24f,-.25f),new Vector3(.39f,.10f,.25f),7,transform);
            Part("Cubical head",new Vector3(0,1.53f,0),Vector3.one*.40f,0,transform,3);
            Part("Block hood top",new Vector3(0,1.742f,-.015f),new Vector3(.43f,.045f,.43f),1,transform);
            Part("Block hood back",new Vector3(0,1.53f,-.21f),new Vector3(.43f,.40f,.04f),1,transform);
            for(int i=-1;i<=1;i+=2)
            {
                Part("Pack strap",new Vector3(i*.16f,1.07f,.145f),new Vector3(.046f,.51f,.022f),6,transform);
                Part("Square coat clasp",new Vector3(i*.16f,1.20f,.164f),new Vector3(.06f,.043f,.018f),5,transform);
            }
            leftLeg=Leg("Left block leg",-.125f);rightLeg=Leg("Right block leg",.125f);
            leftArm=Arm("Left block arm",-.35f);rightArm=Arm("Right block arm",.35f);previous=transform.position;
        }
        Transform Pivot(string name,Vector3 position)
        {var pivot=new GameObject(name).transform;pivot.SetParent(transform,false);pivot.localPosition=position;return pivot;}
        Transform Leg(string name,float x)
        {
            var pivot=Pivot(name,new Vector3(x,.76f,0));
            Part("Cuboid trousers",new Vector3(0,-.19f,0),new Vector3(.225f,.38f,.25f),2,pivot);
            Part("Cuboid boot",new Vector3(0,-.56f,.018f),new Vector3(.23f,.36f,.29f),4,pivot);
            Part("Boot sole",new Vector3(0,-.733f,.022f),new Vector3(.237f,.028f,.30f),7,pivot);return pivot;
        }
        Transform Arm(string name,float x)
        {
            var pivot=Pivot(name,new Vector3(x,1.30f,0));
            Part("Cuboid sleeve",new Vector3(0,-.20f,0),new Vector3(.20f,.40f,.25f),1,pivot);
            Part("Square cuff",new Vector3(0,-.40f,0),new Vector3(.212f,.065f,.262f),6,pivot);
            Part("Block hand",new Vector3(0,-.51f,0),new Vector3(.195f,.17f,.24f),0,pivot);return pivot;
        }
        Transform Part(string name,Vector3 position,Vector3 scale,int tile,Transform parent,int front=-1)
            =>BlockGeometry.Part(name,parent,position,scale,material,tile,front);
        void LateUpdate()
        {
            if(!leftLeg)return;float speed=(transform.position-previous).magnitude/Mathf.Max(.001f,Time.deltaTime);previous=transform.position;
            phase+=Time.deltaTime*Mathf.Clamp(speed*2,0,12);float swing=Mathf.Sin(phase)*Mathf.Clamp(speed*7,0,32);
            leftLeg.localRotation=Quaternion.Euler(swing,0,0);rightLeg.localRotation=Quaternion.Euler(-swing,0,0);leftArm.localRotation=Quaternion.Euler(-swing*.7f,0,0);rightArm.localRotation=Quaternion.Euler(swing*.7f,0,0);
        }
        public void Release(){if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);material=null;}}
        void OnDestroy(){Release();}
    }
}
