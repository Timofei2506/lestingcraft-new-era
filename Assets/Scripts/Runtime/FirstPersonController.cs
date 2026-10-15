using UnityEngine;
using UnityEngine.InputSystem;
using LestingCraft.World;
using LestingCraft.Gameplay;

namespace LestingCraft.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public VoxelWorld World;
        public PrototypeGame Game;
        public Camera Camera;
        public bool Active;public bool Sprinting {get;private set;}
        public ushort Selected=BlockRegistry.StoneId;
        CharacterController controller;
        float pitch,vertical;
        Vector3 spawn;
        public void TeleportSpawn(){if(!controller)controller=GetComponent<CharacterController>();controller.enabled=false;transform.position=spawn;controller.enabled=true;vertical=0;}
        void Start(){controller=GetComponent<CharacterController>();spawn=transform.position;}
        void Update()
        {
            var keyboard=Keyboard.current;var mouse=Mouse.current;if(keyboard==null||mouse==null||World==null)return;
            if(keyboard.escapeKey.wasPressedThisFrame){if(Game.InventoryOpen)Game.ToggleInventory();else Game.TogglePause();return;}
            if(keyboard.eKey.wasPressedThisFrame){Game.ToggleInventory();return;}
            if(!Active||!World.Ready){Sprinting=false;return;}
            var delta=mouse.delta.ReadValue();pitch=Mathf.Clamp(pitch-delta.y*LestingCraft.Presentation.VisualSettings.Current.Sensitivity,-85,85);
            transform.Rotate(0,delta.x*LestingCraft.Presentation.VisualSettings.Current.Sensitivity,0);Camera.transform.localRotation=Quaternion.Euler(pitch,0,0);
            var move=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
            if(move.sqrMagnitude>1)move.Normalize();
            bool swimming=World.GetBlock(Mathf.FloorToInt(transform.position.x),Mathf.FloorToInt(transform.position.y+.8f),Mathf.FloorToInt(transform.position.z))==7;
            Sprinting=!swimming&&keyboard.leftShiftKey.isPressed&&move.sqrMagnitude>0&&Game.Session&&Game.Session.Vitals.stamina>5;float speed=swimming?2.6f:Sprinting?8:5;
            if(controller.isGrounded&&vertical<0)vertical=-2;
            if(swimming)vertical=keyboard.spaceKey.isPressed?3:keyboard.leftCtrlKey.isPressed?-3:Mathf.MoveTowards(vertical,-.35f,Time.deltaTime*8);
            else {if(controller.isGrounded&&keyboard.spaceKey.wasPressedThisFrame)vertical=8;vertical=Mathf.Max(vertical-22*Time.deltaTime,-40);}
            Vector3 velocity=(transform.right*move.x+transform.forward*move.y)*speed;
            Vector3 target=transform.position+velocity*Time.deltaTime;
            if(!World.Loaded(target)||!World.Contains(Mathf.FloorToInt(target.x),1,Mathf.FloorToInt(target.z)))velocity=Vector3.zero;
            float impact=vertical;bool groundedBefore=controller.isGrounded;
            PlayerCapsule.UpdateStep(controller,velocity);
            controller.Move((velocity+Vector3.up*vertical)*Time.deltaTime);
            if(!groundedBefore&&controller.isGrounded&&impact< -11&&Game.Session)Game.Session.Damage((-impact-11)*3);
            if(transform.position.y<1||transform.position.y>240){if(Game.Session)Game.Session.Damage(100);controller.enabled=false;transform.position=spawn;controller.enabled=true;vertical=0;}
            if(!Game.Session)return;
            var keys=new[]{keyboard.digit1Key,keyboard.digit2Key,keyboard.digit3Key,keyboard.digit4Key,keyboard.digit5Key,keyboard.digit6Key,keyboard.digit7Key,keyboard.digit8Key,keyboard.digit9Key};
            for(int i=0;i<keys.Length;i++)if(keys[i].wasPressedThisFrame)Game.Session.Inventory.selected=i;
            Selected=Items.Get(Game.Session.Inventory.Active.item).Block;
            if(mouse.rightButton.wasPressedThisFrame)
            {
                if(!keyboard.leftShiftKey.isPressed&&Physics.Raycast(Camera.transform.position,Camera.transform.forward,out var stationHit,6f)&&stationHit.collider.transform.parent==World.transform)
                {
                    var stationCell=Vector3Int.FloorToInt(stationHit.point-stationHit.normal*.01f);int station=World.GetBlock(stationCell.x,stationCell.y,stationCell.z);
                    if(station==16||station==17){Game.OpenStation(station,stationCell);return;}
                }
                if(Selected==0){Game.Session.UseActive();return;}
            }
            if(mouse.leftButton.isPressed||mouse.rightButton.wasPressedThisFrame)
            {
                if(Physics.Raycast(Camera.transform.position,Camera.transform.forward,out var hit,6f)&&hit.collider.transform.parent==World.transform)
                {
                    bool place=mouse.rightButton.wasPressedThisFrame;var cell=Vector3Int.FloorToInt(hit.point+hit.normal*(place?.01f:-.01f));
                    if(cell.y==0||!World.Contains(cell.x,cell.y,cell.z))return;
                    if(place)
                    {
                        if(!new Bounds((Vector3)cell+Vector3.one*.5f,Vector3.one).Intersects(controller.bounds))Game.Session.Place(cell);
                    }
                    else Game.Session.Mine(cell,Time.deltaTime);
                }
                else Game.Session.ResetMining();
            }
            else Game.Session.ResetMining();
        }
    }
}
