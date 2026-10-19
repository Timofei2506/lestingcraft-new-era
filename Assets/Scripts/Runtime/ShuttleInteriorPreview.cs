using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Gameplay;

namespace LestingCraft.Runtime
{
    /// <summary>Developer-only in-world walkable shuttle cabin prototype. /shuttle-demo offline creative spawns it.
    /// Geometry is intentionally provisional until the owner approves a real production voxel model.
    /// Floors/rails/doors/seats have 3D colliders; the lift carries the real CharacterController continuously.
    /// Not a completed Discovery exterior, not a lunar mission, not network-synchronised.</summary>
    public sealed class ShuttleInteriorPreview : MonoBehaviour
    {
        public ShuttleInteriorSession Session {get;private set;}
        FirstPersonController player;
        Transform platform, outerDoor, innerDoor, cockpitDoor;
        Collider outerCollider, innerCollider, cockpitCollider;
        readonly List<Material> ownedMaterials=new List<Material>();
        readonly Dictionary<ShuttleTarget,string> labels=new Dictionary<ShuttleTarget,string>();
        TextMesh navDisplay,powerDisplay;
        float previousHeight;
        bool seating; float seatT; Vector3 seatFrom,seatTo; bool releaseController;
        public bool IsPlayerSeated {get{return seating || (Session!=null && Session.SeatedSeat>=0);}}
        float outerVisual,innerVisual,cockpitVisual;
        const float DoorSeconds=1.2f;
        const float DeckY=ShuttleInteriorLayout.LiftHeight;

        public void Initialize(FirstPersonController controller)
        {
            player=controller;
            Session=new ShuttleInteriorSession();
            gameObject.name="Prototype · Discovery walkable cabin";
            Material hull=Mat(new Color(.84f,.88f,.92f));
            Material dark=Mat(new Color(.08f,.11f,.15f));
            Material steel=Mat(new Color(.35f,.41f,.48f));
            Material amber=Mat(new Color(.93f,.48f,.12f));
            Material glass=Mat(new Color(.08f,.32f,.43f));
            Material screen=Mat(new Color(.05f,.63f,.64f));
            // Platform, rails and shaft, in real world coordinates. The open ground entrance is on the -X side.
            Box("Left rail",new Vector3(0,14,-1.03f),new Vector3(.1f,29f,.1f),steel);
            Box("Right rail",new Vector3(0,14,1.03f),new Vector3(.1f,29f,.1f),steel);
            for(int h=0;h<=28;h+=4)
            {
                Box("Rail tie left",new Vector3(0,h,-1.03f),new Vector3(.35f,.1f,.2f),dark);
                Box("Rail tie right",new Vector3(0,h,1.03f),new Vector3(.35f,.1f,.2f),dark);
            }
            platform=Box("MOVING LIFT (2×2m)",new Vector3(0,-.15f,0),new Vector3(1.9f,.3f,1.9f),steel);
            Box("Lift safety rear",new Vector3(-.96f,1,0),new Vector3(.12f,1.9f,1.95f),steel).SetParent(platform,true);
            Box("Lift safety left",new Vector3(0,.7f,-.94f),new Vector3(1.9f,1.4f,.1f),steel).SetParent(platform,true);
            Box("Lift safety right",new Vector3(0,.7f,.94f),new Vector3(1.9f,1.4f,.1f),steel).SetParent(platform,true);
            Target("▲ ВВЕРХ · E",new Vector3(.65f,1.05f,-.82f),new Vector3(.2f,.35f,.11f),screen,ShuttleAction.Up,platform);
            Target("▼ ВНИЗ · E",new Vector3(.65f,.65f,-.82f),new Vector3(.2f,.35f,.11f),amber,ShuttleAction.Down,platform);
            var liftTitle=Display("LIFT · E",new Vector3(.3f,1.5f,-.78f));
            liftTitle.transform.SetParent(platform,true);liftTitle.transform.localRotation=Quaternion.identity;liftTitle.text="UP / DOWN";
            // Full 3D navigable decks; camera walks in the same world as the terrain (no teleport).
            Box("Airlock floor",new Vector3(2.3f,DeckY-.17f,0),new Vector3(2.3f,.34f,1.8f),steel);
            Box("Airlock ceiling",new Vector3(2.3f,DeckY+2.4f,0),new Vector3(2.3f,.18f,1.8f),hull);
            Box("Airlock port",new Vector3(2.3f,DeckY+1.1f,-.92f),new Vector3(2.3f,2.3f,.14f),hull);
            Box("Airlock starboard",new Vector3(2.3f,DeckY+1.1f,.92f),new Vector3(2.3f,2.3f,.14f),hull);
            Box("Middeck floor",new Vector3(5.25f,DeckY-.17f,0),new Vector3(3.7f,.34f,4.05f),steel);
            Box("Flightdeck floor",new Vector3(10.3f,DeckY-.17f,0),new Vector3(6.3f,.34f,4.05f),steel);
            for(int i=0;i<2;i++)
            {
                float x=i==0?5.25f:10.3f,w=i==0?3.7f:6.3f;
                Box("Ceiling rib",new Vector3(x,DeckY+2.7f,0),new Vector3(w,.18f,4.05f),hull);
                foreach(int side in new[]{-1,1})
                {
                    Box("Cabin wall liner",new Vector3(x,DeckY+1.3f,side*2.05f),new Vector3(w,2.5f,.13f),hull);
                    for(int j=0;j<3;j++)Box("Rib",new Vector3(x-w*.33f+j*w*.33f,DeckY+1.3f,side*1.96f),new Vector3(.09f,2.4f,.12f),dark);
                    Box("Window",new Vector3(x,DeckY+1.65f,side*1.95f),new Vector3(1.15f,.55f,.05f),glass);
                }
            }
            Box("Nose pressure bulkhead",new Vector3(13.5f,DeckY+1.3f,0),new Vector3(.18f,2.6f,4f),hull);
            outerDoor=Box("Outer hatch · slides up",new Vector3(1.15f,DeckY+1.1f,0),new Vector3(.18f,2.18f,1.55f),amber);
            innerDoor=Box("Inner airlock hatch",new Vector3(3.5f,DeckY+1.1f,0),new Vector3(.18f,2.18f,1.55f),hull);
            cockpitDoor=Box("Cockpit hatch",new Vector3(7.2f,DeckY+1.1f,0),new Vector3(.18f,2.18f,1.55f),hull);
            outerCollider=outerDoor.GetComponent<Collider>();innerCollider=innerDoor.GetComponent<Collider>();cockpitCollider=cockpitDoor.GetComponent<Collider>();
            cockpitCollider.enabled=false; // open on spawn
            Target("ВНЕШНИЙ ЛЮК · E",new Vector3(.95f,DeckY+1.12f,-.83f),new Vector3(.16f,.45f,.16f),screen,ShuttleAction.Outer);
            Target("ВНЕШНИЙ ЛЮК · E",new Vector3(1.43f,DeckY+1.12f,-.70f),new Vector3(.16f,.45f,.12f),screen,ShuttleAction.Outer);
            Target("ВНУТРЕННИЙ ЛЮК · E",new Vector3(3.15f,DeckY+1.1f,-.73f),new Vector3(.14f,.45f,.13f),amber,ShuttleAction.Inner);
            Target("ВНУТРЕННИЙ ЛЮК · E",new Vector3(3.85f,DeckY+1.1f,-.73f),new Vector3(.14f,.45f,.13f),amber,ShuttleAction.Inner);
            Target("ПРОХОД В КАБИНУ · E",new Vector3(6.84f,DeckY+1.15f,-.8f),new Vector3(.14f,.45f,.14f),screen,ShuttleAction.Cockpit);
            Target("ПРОХОД В КАБИНУ · E",new Vector3(7.56f,DeckY+1.15f,-.8f),new Vector3(.14f,.45f,.14f),screen,ShuttleAction.Cockpit);
            // 6 collision seats; aisle stays open at Z=0. Each has visible interact point.
            for(int seat=0;seat<6;seat++)
            {
                float x,z;ShuttleInteriorLayout.SeatPosition(seat,out x,out z);
                Box("Seat "+seat+" cushion",new Vector3(x,DeckY+.46f,z),new Vector3(.75f,.26f,.68f),dark);
                Box("Seat "+seat+" back",new Vector3(x+.36f,DeckY+.96f,z),new Vector3(.15f,1.15f,.68f),dark);
                Box("Seat "+seat+" frame",new Vector3(x,DeckY+.2f,z),new Vector3(.7f,.28f,.6f),steel);
                Target("Кресло "+(seat+1)+" · E",new Vector3(x-.42f,DeckY+1.3f,z),new Vector3(.1f,.25f,.18f),screen,ShuttleAction.Sit,transform,seat);
            }
            // Three-dimensional instrument surfaces (functional prototype; production controls will be separate approved assets).
            Box("Forward instrumentation",new Vector3(12.65f,DeckY+1.15f,0),new Vector3(.5f,1.1f,3.7f),dark);
            Box("MFD left",new Vector3(12.34f,DeckY+1.45f,-.85f),new Vector3(.05f,.5f,.7f),screen);
            Box("MFD right",new Vector3(12.34f,DeckY+1.45f,.85f),new Vector3(.05f,.5f,.7f),screen);
            navDisplay=Display("MFD / mission phase",new Vector3(12.29f,DeckY+1.45f,-.85f));
            powerDisplay=Display("MFD / power & crew",new Vector3(12.29f,DeckY+1.45f,.85f));
            Box("Overhead switch panel",new Vector3(10.5f,DeckY+2.48f,0),new Vector3(2.8f,.12f,2.8f),dark);
            Target("MAIN BUS · E",new Vector3(12.35f,DeckY+1.12f,-1.52f),new Vector3(.1f,.18f,.15f),amber,ShuttleAction.MainBus);
            Target("FUEL FEED · E",new Vector3(12.35f,DeckY+1.12f,-1.05f),new Vector3(.1f,.18f,.15f),amber,ShuttleAction.FuelFeed);
            Target("ARM · E",new Vector3(12.35f,DeckY+1.12f,1.05f),new Vector3(.1f,.18f,.15f),amber,ShuttleAction.Arm);
            Target("IGNITE · E",new Vector3(12.35f,DeckY+1.12f,1.52f),new Vector3(.1f,.18f,.15f),screen,ShuttleAction.Ignite);
            var controlHint=Display("Controls · press E",new Vector3(12.29f,DeckY+2.04f,0));
            controlHint.text="BUS   FUEL  ARM  IGNITE";
            previousHeight=Session.Elevator.Height;
        }

        Material Mat(Color color)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            var material=new Material(shader){color=color};ownedMaterials.Add(material);return material;
        }
        Transform Box(string title,Vector3 center,Vector3 size,Material mat)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=title;
            go.transform.SetParent(transform,false);go.transform.localPosition=center;go.transform.localScale=size;
            go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;
        }
        TextMesh Display(string title,Vector3 position)
        {
            var go=new GameObject(title,typeof(TextMesh));go.transform.SetParent(transform,false);
            go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,-90,0);
            var text=go.GetComponent<TextMesh>();
            text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;
            text.fontSize=48;text.characterSize=.032f;text.color=new Color(.05f,.99f,.86f);
            return text;
        }
        void Target(string title,Vector3 center,Vector3 size,Material mat,ShuttleAction action,Transform parent=null,int index=0)
        {
            var part=Box(title,center,size,mat);
            if(parent!=null && parent!=transform)part.SetParent(parent,true);
            var target=part.gameObject.AddComponent<ShuttleTarget>();target.Action=action;target.Index=index;
            labels[target]=title;
        }
        void LateUpdate()
        {
            if(Session==null || player==null)return;
            // unscaled time preserves actual six-second traversal even when the game changes time scale.
            if(seating)
            {
                seatT=Mathf.Min(1f,seatT+Time.unscaledDeltaTime/.45f);
                float smooth=seatT*seatT*(3f-2f*seatT);
                player.transform.position=Vector3.Lerp(seatFrom,seatTo,smooth);
                if(seatT>=1f)
                {
                    seating=false;
                    if(releaseController)
                    {
                        var motor=player.GetComponent<CharacterController>();motor.enabled=true;
                        releaseController=false;
                    }
                }
            }
            Session.Elevator.Advance(Time.unscaledDeltaTime);
            float height=Session.Elevator.Height;
            platform.localPosition=new Vector3(0,height-.15f,0);
            if(Session.Elevator.Occupied && Mathf.Abs(height-previousHeight)>.0001f)
                player.CarryByPlatform(Vector3.up*(height-previousHeight));
            previousHeight=height;
            if(navDisplay!=null)navDisplay.text="DISCOVERY\n"+Session.Launch.Phase+"\nT "+Session.Launch.T.ToString("F0");
            if(powerDisplay!=null)powerDisplay.text="BUS "+(Session.Launch.IsOn(PanelSwitch.MainBus)?"ON":"OFF")+
                "\nFUEL "+(Session.Launch.IsOn(PanelSwitch.FuelFeed)?"ON":"OFF")+
                "\nSEATS "+Session.Launch.Cabin.Count;
            // Player physically steps off the platform via FPS controls; lift becomes free for the return journey.
            if(Session.Elevator.Occupied && !Session.Elevator.IsMoving)
            {
                var rider=transform.InverseTransformPoint(player.transform.position);
                if(Mathf.Abs(rider.x)>1.08f || Mathf.Abs(rider.z)>1.08f)Session.ExitLift();
            }
            float step=Time.unscaledDeltaTime/DoorSeconds;
            outerVisual=Mathf.MoveTowards(outerVisual,Session.Airlock.OuterOpen?1:0,step);
            innerVisual=Mathf.MoveTowards(innerVisual,Session.Airlock.InnerOpen?1:0,step);
            cockpitVisual=Mathf.MoveTowards(cockpitVisual,Session.Airlock.FlightDeckOpen?1:0,step);
            outerDoor.localPosition=new Vector3(1.15f,DeckY+1.1f+2.4f*outerVisual,0);
            innerDoor.localPosition=new Vector3(3.5f,DeckY+1.1f+2.4f*innerVisual,0);
            cockpitDoor.localPosition=new Vector3(7.2f,DeckY+1.1f+2.4f*cockpitVisual,0);
            outerCollider.enabled=outerVisual<.95f;
            innerCollider.enabled=innerVisual<.95f;
            cockpitCollider.enabled=cockpitVisual<.95f;
        }

        public bool TryInteract(Ray ray,out string notice)
        {
            notice=null;
            if(Session==null||player==null)return false;
            var local=transform.InverseTransformPoint(player.transform.position);
            Session.UpdatePosition(local.x,local.y,local.z);
            RaycastHit hit;
            if(!Physics.Raycast(ray,out hit,3.2f))return false;
            var target=hit.collider.GetComponent<ShuttleTarget>();
            if(target==null||!target.transform.IsChildOf(transform))return false;
            bool ok=false;
            switch(target.Action)
            {
                case ShuttleAction.Up:
                    if(!Session.Elevator.Occupied)Session.BoardLift();
                    ok=Session.Elevator.RequestUp();break;
                case ShuttleAction.Down:
                    if(!Session.Elevator.Occupied)Session.BoardLift();
                    ok=Session.Elevator.RequestDown();break;
                case ShuttleAction.Outer: ok=Session.SetDoor(InteriorDoor.Outer,!Session.Airlock.OuterOpen);break;
                case ShuttleAction.Inner: ok=Session.SetDoor(InteriorDoor.Inner,!Session.Airlock.InnerOpen);break;
                case ShuttleAction.Cockpit: ok=Session.SetDoor(InteriorDoor.FlightDeck,!Session.Airlock.FlightDeckOpen);break;
                case ShuttleAction.Sit:
                    ok=Session.Sit(target.Index,1);
                    if(ok)
                    {
                        float seatX,seatZ;ShuttleInteriorLayout.SeatPosition(target.Index,out seatX,out seatZ);
                        AnimateSeat(transform.TransformPoint(new Vector3(seatX,DeckY+.43f,seatZ)),false);
                    }
                    break;
                case ShuttleAction.MainBus: ok=Session.Launch.SetSwitch(PanelSwitch.MainBus,!Session.Launch.IsOn(PanelSwitch.MainBus));break;
                case ShuttleAction.FuelFeed: ok=Session.Launch.SetSwitch(PanelSwitch.FuelFeed,!Session.Launch.IsOn(PanelSwitch.FuelFeed));break;
                case ShuttleAction.Arm: ok=Session.Launch.SetSwitch(PanelSwitch.Arm,!Session.Launch.IsOn(PanelSwitch.Arm));break;
                case ShuttleAction.Ignite:
                    notice="Двигатели в демонстрационном интерьере отключены: лётный контур пока не подключён";
                    return true;
            }
            notice=ok?labels[target]+" · выполнено":labels[target]+" · сейчас недоступно";
            return true;
        }
        void AnimateSeat(Vector3 target,bool enableOnFinish)
        {
            var motor=player.GetComponent<CharacterController>();
            motor.enabled=false; // temporarily bypass seat cushion collision ONLY during visible .45s animation
            seatFrom=player.transform.position;seatTo=target;seatT=0;seating=true;releaseController=enableOnFinish;
        }
        public bool StandFromSeat(out string notice)
        {
            notice="Вы не сидите в кресле";
            if(!IsPlayerSeated||seating)return false;
            int id=Session.SeatedSeat;float x,z;ShuttleInteriorLayout.SeatPosition(id,out x,out z);
            if(!Session.Stand(1)){notice="Во время разгона из кресла вставать нельзя";return false;}
            // Physically visible rise back into the central aisle, no instant relocation.
            AnimateSeat(transform.TransformPoint(new Vector3(x-.7f,DeckY, z<0?-.42f:.42f)),true);
            notice="Встали; можно ходить по шаттлу";return true;
        }
        void OnDestroy(){foreach(var m in ownedMaterials)if(m)Destroy(m);}
    }

    public enum ShuttleAction { Up, Down, Outer, Inner, Cockpit, Sit, MainBus, FuelFeed, Arm, Ignite }
    public sealed class ShuttleTarget : MonoBehaviour
    {
        public ShuttleAction Action;
        public int Index;
    }
}
