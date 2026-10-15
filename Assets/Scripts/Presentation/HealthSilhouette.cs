using UnityEngine;
using UnityEngine.UI;

namespace LestingCraft.Presentation
{
    /// <summary>Six zones visualize ONE health value. No separate wounds or limb damage simulation.</summary>
    public sealed class HealthSilhouette : MonoBehaviour
    {
        public Image[] Zones {get;private set;}
        public float Health {get;private set;}=100;
        public void Build()
        {
            Zones=new Image[6];
            string[] names={"Head","Torso","Left arm","Right arm","Left leg","Right leg"};
            Vector4[] rects={new Vector4(32,0,27,29),new Vector4(23,35,45,57),new Vector4(5,37,13,66),new Vector4(73,37,13,66),new Vector4(24,98,18,59),new Vector4(49,98,18,59)};
            for(int i=0;i<6;i++)
            {
                var obj=new GameObject(names[i],typeof(RectTransform),typeof(Image));obj.transform.SetParent(transform,false);
                var rt=obj.GetComponent<RectTransform>();rt.anchorMin=rt.anchorMax=new Vector2(0,1);rt.pivot=new Vector2(0,1);rt.anchoredPosition=new Vector2(rects[i].x,-rects[i].y);rt.sizeDelta=new Vector2(rects[i].z,rects[i].w);
                Zones[i]=obj.GetComponent<Image>();Zones[i].sprite=MenuArtwork.Rounded;Zones[i].type=Image.Type.Sliced;Zones[i].raycastTarget=false;
            }
            SetHealth(100);
        }
        public void SetHealth(float hp)
        {
            Health=Mathf.Clamp(hp,0,100);
            var color=Health>50?Color.Lerp(new Color(.9f,.68f,.25f),new Color(.52f,.75f,.57f),(Health-50)/50):Color.Lerp(new Color(.78f,.20f,.16f),new Color(.9f,.68f,.25f),Health/50);
            if(Zones!=null)foreach(var zone in Zones)if(zone)zone.color=color;
        }
    }
}
