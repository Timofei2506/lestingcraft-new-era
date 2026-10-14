using UnityEngine;
namespace LestingCraft.Presentation
{
    /// <summary>Original procedural adventurer stand-in with articulated walk; not a final sculpted/skinned character.</summary>
    public sealed class CharacterVisual : MonoBehaviour
    {
        Transform leftLeg,rightLeg,leftArm,rightArm,head;Vector3 previous;float phase;Material cloth,skin,dark;
        public void Build()
        {
            var baseMaterial=Resources.Load<Material>("Generated/Avatar");cloth=new Material(baseMaterial);cloth.SetColor("_BaseColor",new Color(.25f,.32f,.22f));
            skin=new Material(baseMaterial);skin.SetColor("_BaseColor",new Color(.60f,.40f,.27f));skin.SetFloat("_Smoothness",.2f);
            dark=new Material(baseMaterial);dark.SetColor("_BaseColor",new Color(.10f,.085f,.065f));dark.SetFloat("_Smoothness",.18f);
            Part("Coat",PrimitiveType.Capsule,new Vector3(0,1.12f,0),new Vector3(.60f,.42f,.34f),cloth,transform);
            Part("Belt",PrimitiveType.Cube,new Vector3(0,.89f,0),new Vector3(.55f,.06f,.35f),dark,transform);
            Part("Backpack",PrimitiveType.Cube,new Vector3(0,1.16f,-.21f),new Vector3(.36f,.45f,.19f),dark,transform);
            head=Part("Head",PrimitiveType.Sphere,new Vector3(0,1.68f,0),new Vector3(.27f,.34f,.26f),skin,transform);
            Part("Hood",PrimitiveType.Sphere,new Vector3(0,1.78f,-.03f),new Vector3(.31f,.22f,.29f),cloth,transform);
            for(int i=-1;i<=1;i+=2)Part("Eye",PrimitiveType.Sphere,new Vector3(i*.06f,1.71f,.124f),Vector3.one*.025f,dark,transform);
            leftLeg=Limb("Left leg",new Vector3(-.17f,.87f,0),new Vector3(.19f,.37f,.21f),dark);
            rightLeg=Limb("Right leg",new Vector3(.17f,.87f,0),new Vector3(.19f,.37f,.21f),dark);
            leftArm=Limb("Left sleeve",new Vector3(-.37f,1.42f,0),new Vector3(.18f,.30f,.20f),cloth);
            rightArm=Limb("Right sleeve",new Vector3(.37f,1.42f,0),new Vector3(.18f,.30f,.20f),cloth);previous=transform.position;
        }
        Transform Limb(string name,Vector3 anchor,Vector3 scale,Material material)
        {var pivot=new GameObject(name).transform;pivot.SetParent(transform,false);pivot.localPosition=anchor;Part(name+" mesh",PrimitiveType.Capsule,new Vector3(0,-scale.y,0),scale,material,pivot);return pivot;}
        static Transform Part(string name,PrimitiveType kind,Vector3 position,Vector3 scale,Material material,Transform parent)
        {var obj=GameObject.CreatePrimitive(kind);obj.name=name;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=scale;Destroy(obj.GetComponent<Collider>());obj.GetComponent<Renderer>().sharedMaterial=material;return obj.transform;}
        void LateUpdate()
        {
            if(!leftLeg)return;float speed=(transform.position-previous).magnitude/Mathf.Max(.001f,Time.deltaTime);previous=transform.position;
            phase+=Time.deltaTime*Mathf.Clamp(speed*2,0,12);float swing=Mathf.Sin(phase)*Mathf.Clamp(speed*7,0,32);
            leftLeg.localRotation=Quaternion.Euler(swing,0,0);rightLeg.localRotation=Quaternion.Euler(-swing,0,0);leftArm.localRotation=Quaternion.Euler(-swing*.7f,0,0);rightArm.localRotation=Quaternion.Euler(swing*.7f,0,0);
        }
        void OnDestroy(){if(cloth)Destroy(cloth);if(skin)Destroy(skin);if(dark)Destroy(dark);}
    }
}
