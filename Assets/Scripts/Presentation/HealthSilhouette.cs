using UnityEngine;
using UnityEngine.UI;
namespace LestingCraft.Presentation
{
    // Component name kept for serialized scene compatibility; no body-zone UI remains.
    public sealed class HealthSilhouette : MonoBehaviour
    {
        public Image[] Zones {get;private set;}
        public float Health {get;private set;}=100;
        public void Build()
        {
            var track=new GameObject("HP track",typeof(RectTransform),typeof(Image));
            track.transform.SetParent(transform,false);
            var rt=track.GetComponent<RectTransform>();rt.anchorMin=Vector2.zero;rt.anchorMax=Vector2.one;rt.offsetMin=rt.offsetMax=Vector2.zero;
            track.GetComponent<Image>().color=new Color(.10f,.14f,.16f,.95f);
            var fill=new GameObject("HP fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(track.transform,false);
            Zones=new[]{fill.GetComponent<Image>()};Zones[0].raycastTarget=false;track.GetComponent<Image>().raycastTarget=false;
            SetHealth(100);
        }
        public void SetBody(LestingCraft.Gameplay.SurvivalState state){SetHealth(state.health);}
        public void SetHealth(float hp)
        {
            Health=Mathf.Clamp(hp,0,100);if(Zones==null)return;
            var rt=Zones[0].rectTransform;rt.anchorMin=Vector2.zero;rt.anchorMax=new Vector2(Health/100,1);rt.offsetMin=rt.offsetMax=Vector2.zero;
            Zones[0].color=Health>30?new Color(.55f,.78f,.65f):new Color(.94f,.31f,.25f);
        }
    }
}
