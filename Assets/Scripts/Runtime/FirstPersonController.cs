using UnityEngine;
using UnityEngine.InputSystem;
using LestingCraft.World;

namespace LestingCraft.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public VoxelWorld World;
        public PrototypeGame Game;
        public Camera Camera;
        public bool Active;
        public ushort Selected=BlockRegistry.StoneId;
        CharacterController controller;
        float pitch,vertical;
        Vector3 spawn;
        void Start(){controller=GetComponent<CharacterController>();spawn=transform.position;}
        void Update()
        {
            var keyboard=Keyboard.current;var mouse=Mouse.current;if(keyboard==null||mouse==null||World==null)return;
            if(keyboard.escapeKey.wasPressedThisFrame){Game.TogglePause();return;}
            if(!Active||!World.Ready)return;
            var delta=mouse.delta.ReadValue();pitch=Mathf.Clamp(pitch-delta.y*LestingCraft.Presentation.VisualSettings.Current.Sensitivity,-85,85);
            transform.Rotate(0,delta.x*LestingCraft.Presentation.VisualSettings.Current.Sensitivity,0);Camera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            var move=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
            if(move.sqrMagnitude>1)move.Normalize();
            float speed=keyboard.leftShiftKey.isPressed?8:5;
            if(controller.isGrounded&&vertical<0)vertical=-2;
            if(controller.isGrounded&&keyboard.spaceKey.wasPressedThisFrame)vertical=8;
            vertical=Mathf.Max(vertical-22*Time.deltaTime,-40);
            Vector3 velocity=(transform.right*move.x+transform.forward*move.y)*speed;
            Vector3 target=transform.position+velocity*Time.deltaTime;
            if(!World.Loaded(target)||!World.Contains(Mathf.FloorToInt(target.x),1,Mathf.FloorToInt(target.z)))velocity=Vector3.zero;
            controller.Move((velocity+Vector3.up*vertical)*Time.deltaTime);
            if(transform.position.y<1||transform.position.y>240){controller.enabled=false;transform.position=spawn;controller.enabled=true;vertical=0;}
            if(keyboard.digit1Key.wasPressedThisFrame)Selected=1;
            if(keyboard.digit2Key.wasPressedThisFrame)Selected=2;
            if(keyboard.digit3Key.wasPressedThisFrame)Selected=3;
            if(keyboard.digit4Key.wasPressedThisFrame)Selected=4;
            if(keyboard.digit5Key.wasPressedThisFrame)Selected=5;
            if(keyboard.digit6Key.wasPressedThisFrame)Selected=6;
            if(keyboard.digit7Key.wasPressedThisFrame)Selected=11;
            if(keyboard.digit8Key.wasPressedThisFrame)Selected=12;
            if(keyboard.digit9Key.wasPressedThisFrame)Selected=14;
            if(mouse.leftButton.wasPressedThisFrame||mouse.rightButton.wasPressedThisFrame)
            {
                if(Physics.Raycast(Camera.transform.position,Camera.transform.forward,out var hit,6f))
                {
                    // Only voxel colliders are editable; don't turn player avatars into blocks.
                    if(hit.collider.transform.parent!=World.transform)return;
                    bool place=mouse.rightButton.wasPressedThisFrame;
                    Vector3 point=hit.point+hit.normal*(place?.01f:-.01f);
                    var cell=Vector3Int.FloorToInt(point);
                    if(cell.y==0||!World.Contains(cell.x,cell.y,cell.z))return;
                    if(place&&new Bounds((Vector3)cell+Vector3.one*.5f,Vector3.one).Intersects(controller.bounds))return;
                    Game.RequestEdit(cell,place?Selected:(ushort)0);
                }
            }
        }
    }
}
