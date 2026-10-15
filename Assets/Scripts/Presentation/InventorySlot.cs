using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace LestingCraft.Presentation
{
    public sealed class InventorySlot : MonoBehaviour,IPointerClickHandler,IBeginDragHandler,IDragHandler,IEndDragHandler
    {
        public GameUI Owner;public int Index;bool pickedOnDrag;
        static bool Shift=>Keyboard.current!=null&&(Keyboard.current.leftShiftKey.isPressed||Keyboard.current.rightShiftKey.isPressed);
        public void OnPointerClick(PointerEventData e){if(Owner)Owner.InventoryClick(Index,e.button==PointerEventData.InputButton.Right,Shift);}
        public void OnBeginDrag(PointerEventData e)
        {pickedOnDrag=false;if(e.button!=PointerEventData.InputButton.Left||!Owner)return;pickedOnDrag=Owner.BeginInventoryDrag(Index);}
        public void OnDrag(PointerEventData e){}
        public void OnEndDrag(PointerEventData e)
        {
            if(!pickedOnDrag||!Owner)return;var go=e.pointerCurrentRaycast.gameObject;var target=go?go.GetComponentInParent<InventorySlot>():null;
            if(target&&target.Owner==Owner&&target.Index!=Index)Owner.InventoryClick(target.Index,false,false);pickedOnDrag=false;
        }
    }
}
