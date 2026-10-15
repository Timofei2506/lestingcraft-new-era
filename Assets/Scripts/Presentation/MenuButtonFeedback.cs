using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace LestingCraft.Presentation
{
    public sealed class MenuButtonFeedback : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler,IPointerDownHandler,IPointerUpHandler
    {
        public Image Border;public Color Tint;bool hovered,selected,pressed;
        public void OnPointerEnter(PointerEventData e){hovered=true;}
        public void OnPointerExit(PointerEventData e){hovered=false;pressed=false;}
        public void OnSelect(BaseEventData e){selected=true;}
        public void OnDeselect(BaseEventData e){selected=false;pressed=false;}
        public void OnPointerDown(PointerEventData e){pressed=true;}
        public void OnPointerUp(PointerEventData e){pressed=false;}
        void Update()
        {
            float blend=1-Mathf.Exp(-Time.unscaledDeltaTime*16),scale=pressed?.986f:hovered||selected?1.012f:1;
            transform.localScale=Vector3.Lerp(transform.localScale,Vector3.one*scale,blend);
            if(Border){var target=Tint;target.a=hovered||selected?.85f:.20f;Border.color=Color.Lerp(Border.color,target,blend);}
        }
    }
}
