using UnityEngine;
using UnityEngine.EventSystems;

namespace LestingCraft.Presentation
{
    public sealed class ItemHover : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        public GameUI Owner;public int Item;
        public void OnPointerEnter(PointerEventData e){if(Owner&&Item>0)Owner.ShowItemTooltip(Item,e.position);}
        public void OnPointerExit(PointerEventData e){if(Owner)Owner.HideItemTooltip();}
        public void OnSelect(BaseEventData e){if(Owner&&Item>0)Owner.ShowItemTooltip(Item,RectTransformUtility.WorldToScreenPoint(null,transform.position));}
        public void OnDeselect(BaseEventData e){if(Owner)Owner.HideItemTooltip();}
        void OnDisable(){if(Owner)Owner.HideItemTooltip();}
    }
}
