using UnityEngine;
using System.Collections.Generic;
namespace LestingCraft.Presentation
{
    /// <summary>Original block adventurer: cuboid head, torso, arms and legs, textured cloth and equipment.</summary>
    public sealed class CharacterVisual : MonoBehaviour
    {
        Transform leftLeg,rightLeg,leftArm,rightArm;Vector3 previous;float phase;Material material,metal;readonly List<GameObject> extras=new List<GameObject>();string gearKey="";
        GameObject carried;int carriedId=-1;bool resting;int restingVariant;
        public void SetRestPose(int variant){resting=true;restingVariant=variant;ApplyRestPose();}
        void ApplyRestPose(){if(!leftLeg)return;leftLeg.localRotation=Quaternion.Euler(restingVariant==0?-8:12,0,restingVariant==0?-9:13);rightLeg.localRotation=Quaternion.Euler(restingVariant==0?14:-7,0,restingVariant==0?16:-8);leftArm.localRotation=Quaternion.Euler(restingVariant==0?-10:24,0,restingVariant==0?28:-38);rightArm.localRotation=Quaternion.Euler(restingVariant==0?12:-25,0,restingVariant==0?-17:35);}
        public void SetOffhand(int id,bool lighting=true){if(carriedId==id)return;carriedId=id;if(carried)Destroy(carried);if(id!=148||!leftArm)return;carried=new GameObject("Visible left-hand torch");carried.transform.SetParent(leftArm,false);carried.transform.localPosition=new Vector3(0,-.38f,.22f);carried.transform.localRotation=Quaternion.Euler(-20,0,0);carried.transform.localScale=Vector3.one*.75f;carried.AddComponent<TorchVisual>().Build(lighting);}
        public void Build()
        {
            if(material)return;material=BlockGeometry.CharacterMaterial();metal=new Material(material);metal.SetFloat("_Metallic",.65f);metal.SetFloat("_Smoothness",.34f);
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
        public void SetEquipment(int[] ids)
        {
            if(ids==null||ids.Length!=8)ids=new int[8];string key=string.Join(",",ids);if(key==gearKey)return;gearKey=key;
            foreach(var obj in extras)if(obj){if(Application.isPlaying)Destroy(obj);else DestroyImmediate(obj);}extras.Clear();
            if(ids[0]!=0)Gear("Cloth hood",transform,new Vector3(0,1.75f,0),new Vector3(.46f,.08f,.46f),1);
            if(ids[1]!=0){Gear("Warm mantle back",transform,new Vector3(0,1.10f,-.24f),new Vector3(.58f,.67f,.09f),7);Gear("Mantle collar",transform,new Vector3(0,1.34f,-.01f),new Vector3(.59f,.10f,.35f),6);}
            if(ids[2]!=0){Gear("Left trouser shell",leftLeg,new Vector3(0,-.21f,0),new Vector3(.24f,.43f,.27f),2);Gear("Right trouser shell",rightLeg,new Vector3(0,-.21f,0),new Vector3(.24f,.43f,.27f),2);}
            if(ids[3]!=0){Gear("Left leather boot",leftLeg,new Vector3(0,-.57f,.03f),new Vector3(.25f,.36f,.32f),6);Gear("Right leather boot",rightLeg,new Vector3(0,-.57f,.03f),new Vector3(.25f,.36f,.32f),6);}
            if(ids[4]!=0){Gear("Riveted helmet crown",transform,new Vector3(0,1.76f,0),new Vector3(.49f,.10f,.49f),8,true);Gear("Helmet back",transform,new Vector3(0,1.55f,-.24f),new Vector3(.49f,.36f,.06f),8,true);for(int side=-1;side<=1;side+=2)Gear("Helmet cheek",transform,new Vector3(side*.23f,1.55f,0),new Vector3(.06f,.34f,.46f),8,true);}
            if(ids[5]!=0){Gear("Breastplate",transform,new Vector3(0,1.06f,.19f),new Vector3(.52f,.49f,.10f),8,true);Gear("Left pauldron",leftArm,new Vector3(0,-.08f,0),new Vector3(.26f,.19f,.31f),9,true);Gear("Right pauldron",rightArm,new Vector3(0,-.08f,0),new Vector3(.26f,.19f,.31f),9,true);}
            if(ids[6]!=0){Gear("Left leg plate",leftLeg,new Vector3(0,-.20f,.15f),new Vector3(.245f,.38f,.07f),8,true);Gear("Right leg plate",rightLeg,new Vector3(0,-.20f,.15f),new Vector3(.245f,.38f,.07f),8,true);}
            if(ids[7]!=0){Gear("Left armored boot",leftLeg,new Vector3(0,-.59f,.025f),new Vector3(.27f,.32f,.35f),9,true);Gear("Right armored boot",rightLeg,new Vector3(0,-.59f,.025f),new Vector3(.27f,.32f,.35f),9,true);}
        }
        void Gear(string name,Transform parent,Vector3 position,Vector3 scale,int tile,bool armored=false)
        {extras.Add(BlockGeometry.Part(name,parent,position,scale,armored?metal:material,tile).gameObject);}
        void LateUpdate()
        {
            if(!leftLeg)return;if(resting){ApplyRestPose();previous=transform.position;return;}float speed=(transform.position-previous).magnitude/Mathf.Max(.001f,Time.deltaTime);previous=transform.position;
            phase+=Time.deltaTime*Mathf.Clamp(speed*2,0,12);float swing=Mathf.Sin(phase)*Mathf.Clamp(speed*7,0,32);
            leftLeg.localRotation=Quaternion.Euler(swing,0,0);rightLeg.localRotation=Quaternion.Euler(-swing,0,0);leftArm.localRotation=Quaternion.Euler(-swing*.7f,0,0);rightArm.localRotation=Quaternion.Euler(swing*.7f,0,0);
        }
        public void Release(){if(metal){if(Application.isPlaying)Destroy(metal);else DestroyImmediate(metal);metal=null;}if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);material=null;}}
        void OnDestroy(){Release();}
    }
}
