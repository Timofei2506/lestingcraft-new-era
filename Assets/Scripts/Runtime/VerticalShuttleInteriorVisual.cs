using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using LestingCraft.Gameplay;
using LestingCraft.Networking;

namespace LestingCraft.Runtime
{
    /// <summary>Physical fitting for the VERTICAL owner-approved orbiter with full transparent
    /// panoramic windshield + side viewports, ergonomic low-profile flight deck, framed crew
    /// seats, mid-deck crew quarters, warm interior lights, airlock hatch, and moving crew lift.</summary>
    public sealed class VerticalShuttleInteriorVisual : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        readonly List<Transform> padEquipment=new List<Transform>();
        Transform hatch,liftPlatform,liftAnchor;
        Vector3 liftBaseLocal,liftRiseLocal;
        BoxCollider hatchCollider;
        FirstPersonController liftRider;
        float lastLiftHeight;
        long lastLiftServerNow;
        public readonly ShuttleElevator Lift=new ShuttleElevator(VerticalShuttleWalk.LiftTravel);
        public readonly CockpitPanel Panel=new CockpitPanel();
        public CockpitInteractiveRig InteractiveRig {get;private set;}
        public bool HatchOpen {get;private set;}

        public static VerticalShuttleInteriorVisual Create(Transform parent)
        {
            var node=new GameObject("Approved orbiter · physically fitted vertical cabin");
            node.transform.SetParent(parent,false);
            var visual=node.AddComponent<VerticalShuttleInteriorVisual>();
            visual.Build();return visual;
        }
        Material Mat(Color color,bool metal=false,float emission=0f)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(!shader)throw new InvalidOperationException("Missing cabin shader");
            var mat=new Material(shader){name="Orbiter cabin fitting",color=color};
            if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",color);
            mat.SetFloat("_Metallic",metal?.52f:0.06f);
            mat.SetFloat("_Smoothness",metal?.48f:.22f);
            if(emission>0.001f)
            {
                mat.EnableKeyword("_EMISSION");
                if(mat.HasProperty("_EmissionColor"))mat.SetColor("_EmissionColor",color*emission);
            }
            materials.Add(mat);return mat;
        }
        Material GlassMat(Color tint,float alpha)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            var mat=new Material(shader){name="Orbiter canopy glass"};
            Color c=new Color(tint.r,tint.g,tint.b,alpha);
            mat.color=c;
            if(mat.HasProperty("_BaseColor"))mat.SetColor("_BaseColor",c);
            if(mat.HasProperty("_Surface"))mat.SetFloat("_Surface",1f);
            if(mat.HasProperty("_ZWrite"))mat.SetFloat("_ZWrite",0f);
            mat.SetInt("_SrcBlend",5);
            mat.SetInt("_DstBlend",10);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue=3000;
            mat.SetFloat("_Metallic",0.08f);
            mat.SetFloat("_Smoothness",0.90f);
            materials.Add(mat);return mat;
        }
        Transform Box(string name,Vector3 position,Vector3 size,Material material,bool collision=true,Quaternion? rot=null)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name=name;part.transform.SetParent(transform,false);
            part.transform.localPosition=position;
            if(rot.HasValue)part.transform.localRotation=rot.Value;
            part.transform.localScale=size;
            part.GetComponent<MeshRenderer>().sharedMaterial=material;
            if(!collision)DestroyComponent(part.GetComponent<BoxCollider>());
            return part.transform;
        }
        Light AddCabinLight(string name,Vector3 localPos,Color color,float range,float intensity)
        {
            var go=new GameObject(name);
            go.transform.SetParent(transform,false);
            go.transform.localPosition=localPos;
            var l=go.AddComponent<Light>();
            l.type=LightType.Point;
            l.color=color;
            l.range=range;
            l.intensity=intensity;
            l.shadows=LightShadows.None;
            return l;
        }
        static void DestroyComponent(UnityEngine.Object item)
        {if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        void Build()
        {
            SpaceVoxelVisual.CreateWalkableShuttle(transform); // real hull mesh with panoramic cockpit window apertures

            var floor=Mat(new Color(.20f,.26f,.33f),true);
            var wall=Mat(new Color(.76f,.81f,.86f));
            var slate=Mat(new Color(.11f,.14f,.19f),true);
            var mullion=Mat(new Color(.07f,.09f,.13f),true);
            var seatFabric=Mat(new Color(.16f,.22f,.30f));
            var seatTrim=Mat(new Color(.88f,.48f,.12f));
            var cyanGlow=Mat(new Color(.18f,.84f,.98f),false,1.35f);
            var warmStrip=Mat(new Color(.96f,.93f,.86f),false,1.65f);
            var glass=GlassMat(new Color(.56f,.86f,.98f),0.10f);

            // =========================================================================
            // 1. TOWER ACCESS WALKWAY & LOWER MID-DECK (Clean ergonomic crew quarters)
            // =========================================================================
            Box("Physical tower-to-airlock walkway",new Vector3(-5.5f,27.88f,3),new Vector3(7f,.24f,1.90f),floor);
            padEquipment.Add(Box("Tower lift exit bridge",new Vector3(-18.3f,27.88f,3),new Vector3(1.1f,.24f,1.90f),floor));
            Box("Lower crew deck",new Vector3(.5f,27.88f,3.5f),new Vector3(4.92f,.24f,4.92f),floor);

            // Aft modular avionics bays (flush against z=1.18 wall)
            for(int bay=0;bay<4;bay++)
            {
                float bx=-1.35f+bay*1.22f;
                Box("MidDeck Avionics Locker "+bay,new Vector3(bx,29.15f,1.22f),new Vector3(1.06f,2.15f,.30f),wall);
                Box("MidDeck Locker Trim "+bay,new Vector3(bx,29.85f,1.38f),new Vector3(.84f,.12f,.03f),bay%2==0?cyanGlow:seatTrim,false);
            }
            // Starboard crew porthole window frame & glass (looking outside at y=30..31, x=3.0, z=3.5)
            Box("MidDeck Porthole Glass",new Vector3(3.04f,30.95f,3.95f),new Vector3(.05f,1.55f,1.65f),glass);
            Box("MidDeck Porthole Frame Top",new Vector3(2.98f,31.76f,3.95f),new Vector3(.12f,.10f,1.80f),mullion,false);
            Box("MidDeck Porthole Frame Bot",new Vector3(2.98f,30.14f,3.95f),new Vector3(.12f,.10f,1.80f),mullion,false);

            // Forward mid-deck supply bench & oxygen canisters (flush against z=5.72)
            Box("MidDeck Supply Bench",new Vector3(.5f,28.42f,5.62f),new Vector3(3.6f,.72f,.48f),slate);
            Box("MidDeck Ceiling Light Strip",new Vector3(.5f,33.78f,3.5f),new Vector3(3.2f,.06f,.18f),warmStrip,false);
            AddCabinLight("MidDeckInteriorLight",new Vector3(.5f,31.8f,3.5f),new Color(0.95f,0.96f,1.0f),9.5f,1.85f);

            // =========================================================================
            // 2. CENTRAL INTER-DECK ACCESS SHAFT & SAFETY LADDER (y=34..41)
            // =========================================================================
            for(int y=34;y<=41;y++)
                Box("Ladder rung "+y,new Vector3(.5f,y+.15f,2.14f),new Vector3(1.24f,.05f,.07f),seatTrim,false);
            Box("Ladder grip left",new Vector3(-.16f,37.4f,2.14f),new Vector3(.06f,8.4f,.06f),wall,false);
            Box("Ladder grip right",new Vector3(1.16f,37.4f,2.14f),new Vector3(.06f,8.4f,.06f),wall,false);

            // =========================================================================
            // 3. UPPER FLIGHT DECK FLOORING & SAFETY RAILING (y=41.86..42.00)
            // =========================================================================
            Box("Cockpit port floor",new Vector3(-1.48f,41.86f,3.85f),new Vector3(1.04f,.28f,4.15f),floor);
            Box("Cockpit starboard floor",new Vector3(2.44f,41.86f,3.85f),new Vector3(1.12f,.28f,4.15f),floor);
            Box("Cockpit forward bridge",new Vector3(.5f,41.86f,5.28f),new Vector3(4.96f,.28f,1.55f),floor);
            Box("Cockpit aft catwalk",new Vector3(.5f,41.86f,1.92f),new Vector3(4.96f,.28f,.55f),floor);
            Box("Cockpit ladder landing",new Vector3(1.32f,41.86f,3.62f),new Vector3(1.22f,.28f,1.95f),floor);
            // Low safety guardrail behind seats so crew see where the shaft opening is
            Box("Shaft Safety Rail Aft",new Vector3(-.28f,42.52f,4.48f),new Vector3(1.32f,.05f,.05f),seatTrim,false);

            // =========================================================================
            // 4. REAL PANORAMIC FRONT WINDSHIELD, BROW WINDOWS & SIDE VIEWPORTS!
            //    Window sill is at y=43.16 (0.48 m BELOW pilot eye height 43.64!) so the
            //    pilot & co-pilot have a wide, crystal-clear view straight ahead & outside!
            // =========================================================================
            // Outer transparent canopy glass panes (collidable barrier keeping crew inside cabin)
            Box("Windshield Front Glass",new Vector3(.50f,44.48f,5.96f),new Vector3(4.92f,2.68f,.04f),glass);
            Box("Windshield Brow Overhead Glass",new Vector3(.50f,46.05f,5.12f),new Vector3(3.10f,.04f,1.72f),glass,true,Quaternion.Euler(22f,0f,0f));
            Box("Viewport Port Glass",new Vector3(-1.98f,44.32f,4.45f),new Vector3(.04f,2.24f,3.05f),glass);
            Box("Viewport Starboard Glass",new Vector3(2.98f,44.32f,4.45f),new Vector3(.04f,2.24f,3.05f),glass);

            // Crisp dark-titanium spacecraft window frame mullions (non-blocking slim ribs)
            Box("Windshield Sill Bar",new Vector3(.50f,43.15f,5.93f),new Vector3(4.98f,.09f,.12f),mullion,false);
            Box("Windshield Header Bar",new Vector3(.50f,45.80f,5.93f),new Vector3(4.98f,.10f,.12f),mullion,false);
            Box("Windshield Center V-Strut",new Vector3(.50f,44.48f,5.93f),new Vector3(.085f,2.66f,.10f),mullion,false);
            Box("Windshield Port A-Pillar",new Vector3(-1.05f,44.48f,5.93f),new Vector3(.075f,2.66f,.10f),mullion,false,Quaternion.Euler(0f,0f,-6f));
            Box("Windshield Starboard A-Pillar",new Vector3(2.05f,44.48f,5.93f),new Vector3(.075f,2.66f,.10f),mullion,false,Quaternion.Euler(0f,0f,6f));
            Box("Viewport Port Sill",new Vector3(-1.95f,43.18f,4.45f),new Vector3(.10f,.08f,3.10f),mullion,false);
            Box("Viewport Starboard Sill",new Vector3(2.95f,43.18f,4.45f),new Vector3(.10f,.08f,3.10f),mullion,false);

            // =========================================================================
            // 5. FULL-WIDTH SPACE SHUTTLE INSTRUMENT GLAIRSHIELD & DASHBOARD (y<=43.15!)
            // =========================================================================
            Box("Main Dashboard Pedestal",new Vector3(.50f,42.34f,5.74f),new Vector3(4.86f,.68f,.44f),slate);
            Box("Main Dashboard Lower Fascia",new Vector3(.50f,42.78f,5.74f),new Vector3(4.86f,.22f,.44f),slate,false);
            Box("Main Dashboard Sloped Face",new Vector3(.50f,42.94f,5.60f),new Vector3(4.84f,.44f,.10f),slate,false,Quaternion.Euler(26f,0f,0f));
            Box("Main Dashboard Glareshield Hood",new Vector3(.50f,43.15f,5.66f),new Vector3(4.88f,.045f,.52f),mullion,false);
            Box("Glareshield Accent Strip",new Vector3(.50f,43.13f,5.41f),new Vector3(4.84f,.016f,.02f),cyanGlow,false);

            // Port & Starboard Side Consoles running cleanly along the side walls below side windows
            Box("Port Side Console",new Vector3(-1.74f,42.48f,4.95f),new Vector3(.44f,.96f,1.85f),slate);
            Box("Starboard Side Console",new Vector3(2.74f,42.48f,4.95f),new Vector3(.44f,.96f,1.85f),slate);

            // Center Pedestal Console between Pilot (x=-0.40) and Co-Pilot (x=+1.40)
            Box("Center Pedestal Base",new Vector3(.50f,42.38f,5.08f),new Vector3(.76f,.76f,1.18f),slate);
            Box("Center Pedestal Top Deck",new Vector3(.50f,42.78f,5.08f),new Vector3(.80f,.06f,1.20f),mullion);

            // Overhead Systems & Caution/Warning Panel (flush above brow at y=45.88, z=4.35)
            Box("Overhead Switch Panel",new Vector3(.50f,45.86f,4.28f),new Vector3(1.90f,.10f,.78f),slate,false,Quaternion.Euler(-16f,0f,0f));
            Box("Overhead Cabin Light Strip",new Vector3(.50f,45.88f,3.55f),new Vector3(2.40f,.04f,.14f),warmStrip,false);
            AddCabinLight("FlightDeckInteriorLight",new Vector3(.50f,45.15f,4.75f),new Color(0.94f,0.97f,1.0f),8.5f,1.75f);

            // =========================================================================
            // 6. SCULPTED SPACECRAFT FLIGHT SEATS (Commander Seat 0 & Pilot Seat 1)
            // =========================================================================
            for(int index=0;index<VerticalShuttleWalk.SeatCount;index++)
            {
                double x,y,z;VerticalShuttleWalk.Seat(index,out x,out y,out z);
                float sx=(float)x,sz=(float)z-0.36f; // Positioned cleanly at z=4.99 facing dashboard z=5.55 & windows z=5.96!
                Box("Crew seat "+index+" pedestal",new Vector3(sx,42.18f,sz),new Vector3(.46f,.36f,.46f),mullion,false);
                Box("Crew seat "+index+" cushion",new Vector3(sx,42.40f,sz+.04f),new Vector3(.68f,.14f,.60f),seatFabric);
                Box("Crew seat "+index+" backrest",new Vector3(sx,42.96f,sz-.24f),new Vector3(.66f,1.02f,.11f),seatFabric,true,Quaternion.Euler(-8f,0f,0f));
                Box("Crew seat "+index+" headrest",new Vector3(sx,43.58f,sz-.30f),new Vector3(.42f,.24f,.12f),slate,false);
                Box("Crew seat "+index+" bolster L",new Vector3(sx-.31f,42.62f,sz+.02f),new Vector3(.07f,.26f,.52f),seatTrim,false);
                Box("Crew seat "+index+" bolster R",new Vector3(sx+.31f,42.62f,sz+.02f),new Vector3(.07f,.26f,.52f),seatTrim,false);
            }

            // =========================================================================
            // 7. SEALED TOWER HATCH & MOVING CREW LIFT
            // =========================================================================
            hatch=Box("Closed pressure hatch · server controlled",new Vector3(-9.14f,29.43f,3),new Vector3(.18f,2.86f,1.94f),wall);
            hatchCollider=hatch.GetComponent<BoxCollider>();
            liftBaseLocal=new Vector3(VerticalShuttleWalk.LiftX,VerticalShuttleWalk.LiftGroundY-.14f,3);
            liftRiseLocal=Vector3.up;liftAnchor=transform;
            liftPlatform=Box("Moving crew lift",liftBaseLocal,new Vector3(1.9f,.28f,1.9f),floor);
            padEquipment.Add(liftPlatform);
            Box("Lift safety back",new Vector3(VerticalShuttleWalk.LiftX-.93f,VerticalShuttleWalk.LiftGroundY+.86f,3),new Vector3(.08f,1.9f,1.88f),slate).SetParent(liftPlatform,true);
            Box("Lift safety port",new Vector3(VerticalShuttleWalk.LiftX,VerticalShuttleWalk.LiftGroundY+.65f,2.08f),new Vector3(1.9f,1.45f,.08f),slate).SetParent(liftPlatform,true);
            Box("Lift safety starboard",new Vector3(VerticalShuttleWalk.LiftX,VerticalShuttleWalk.LiftGroundY+.65f,3.92f),new Vector3(1.9f,1.45f,.08f),slate).SetParent(liftPlatform,true);
            for(int y=3;y<=28;y+=5)
            {
                padEquipment.Add(Box("Lift guide rear "+y,new Vector3(VerticalShuttleWalk.LiftX-.99f,y+.5f,2.02f),new Vector3(.09f,4.8f,.09f),wall));
                padEquipment.Add(Box("Lift guide front "+y,new Vector3(VerticalShuttleWalk.LiftX-.99f,y+.5f,3.98f),new Vector3(.09f,4.8f,.09f),wall));
            }
            InteractiveRig=CockpitInteractiveRig.Create(transform,Panel,new OpenSpaceHelm());
            SyncHatch(false);
        }
        public void BindHelm(OpenSpaceHelm helm)
        {
            if(InteractiveRig&&helm!=null)InteractiveRig.BindHelm(helm);
        }
        public bool AnchorLiftToPad(Transform pad)
        {
            if(!pad||!liftPlatform||liftAnchor!=transform||pad==transform||pad.IsChildOf(transform))return false;
            Vector3 baseWorld=transform.TransformPoint(liftBaseLocal);
            Vector3 riseWorld=transform.TransformVector(Vector3.up);
            if(Mathf.Abs(riseWorld.magnitude-1)>1e-3f)return false;
            foreach(var part in padEquipment)part.SetParent(pad,true);
            liftAnchor=pad;
            liftBaseLocal=pad.InverseTransformPoint(baseWorld);
            liftRiseLocal=pad.InverseTransformVector(riseWorld);
            SetPlatformHeight(Lift.Height);
            return true;
        }
        void SetPlatformHeight(float height)
        {if(liftPlatform)liftPlatform.localPosition=liftBaseLocal+liftRiseLocal*height;}
        public bool BoardLift(FirstPersonController player)
        {
            if(!player||Lift.IsMoving||Lift.Occupied)return false;
            var p=transform.InverseTransformPoint(player.transform.position);
            if(Mathf.Abs(p.x-VerticalShuttleWalk.LiftX)>.85f||Mathf.Abs(p.z-3f)>.85f||
               Mathf.Abs(p.y-(VerticalShuttleWalk.LiftGroundY+Lift.Height))>1f||!Lift.Board())return false;
            liftRider=player;return true;
        }
        public bool RequestUp(FirstPersonController player)
        {
            if(!Lift.Occupied&&!BoardLift(player))return false;
            return liftRider==player&&Lift.RequestUp();
        }
        public bool RequestDown(FirstPersonController player)
        {
            if(!Lift.Occupied&&!BoardLift(player))return false;
            return liftRider==player&&Lift.RequestDown();
        }
        public bool RecallEmptyLift(){return Lift.ReturnEmpty();}
        public bool TryClimb(FirstPersonController player,float direction,float seconds)
        {
            if(!player||float.IsNaN(seconds)||seconds<=0||seconds>.2f||
               (direction!=1f&&direction!= -1f))return false;
            var p=transform.InverseTransformPoint(player.transform.position);
            if(p.x<-1.15f||p.x>1.65f||p.z<1.85f||p.z>3.65f||p.y<27.7f||p.y>42.35f)return false;
            float next=Mathf.Clamp(p.y+direction*Mathf.Min(.75f,4.5f*seconds),28f,42.1f);
            player.CarryByPlatform(transform.up*(next-p.y));
            return true;
        }
        public void SyncLift(NetShuttleLift snapshot,FirstPersonController player,string selfId)
        {
            if(snapshot==null||!snapshot.active||snapshot.serverNow<lastLiftServerNow||!liftPlatform||
               snapshot.progress<0||snapshot.progress>1||float.IsNaN(snapshot.progress))return;
            lastLiftServerNow=snapshot.serverNow;
            bool mine=snapshot.occupied&&snapshot.riderId==selfId&&player;
            if(mine&&!liftRider&&!Lift.Occupied&&!Lift.IsMoving)BoardLift(player);
            if(!mine)liftRider=null;
            var state=snapshot.moving?snapshot.direction=="up"?ElevatorState.Rising:
                snapshot.direction=="down"?ElevatorState.Descending:ElevatorState.Ground:
                snapshot.progress>=1?ElevatorState.Top:ElevatorState.Ground;
            if(snapshot.moving&&state==ElevatorState.Ground)return;
            Lift.Reconcile(snapshot.progress,state,snapshot.occupied);
            float height=Lift.Height,delta=height-lastLiftHeight;
            SetPlatformHeight(height);
            CarryShort(delta);
            lastLiftHeight=height;
            if(liftRider&&Lift.Occupied&&!Lift.IsMoving)
            {
                var p=transform.InverseTransformPoint(liftRider.transform.position);
                if(Mathf.Abs(p.x-VerticalShuttleWalk.LiftX)>1.15f||Mathf.Abs(p.z-3f)>1.15f||
                   Mathf.Abs(p.y-(VerticalShuttleWalk.LiftGroundY+Lift.Height))>1.35f)
                {Lift.Exit();liftRider=null;}
            }
        }
        void CarryShort(float delta)
        {
            if(!liftRider||!Lift.Occupied||Mathf.Abs(delta)<.0001f)return;
            if(Mathf.Abs(delta)>1.25f){liftRider=null;return;}
            int steps=Mathf.CeilToInt(Mathf.Abs(delta)/.20f);
            for(int i=0;i<steps;i++)liftRider.CarryByPlatform(liftAnchor.TransformVector(liftRiseLocal*(delta/steps)));
        }
        void LateUpdate()
        {
            if(!liftPlatform)return;
            Lift.Advance(Time.unscaledDeltaTime);
            float height=Lift.Height,delta=height-lastLiftHeight;
            SetPlatformHeight(height);
            CarryShort(delta);
            lastLiftHeight=height;
        }
        public void SyncHatch(bool open)
        {
            HatchOpen=open;
            if(hatch)hatch.localPosition=new Vector3(-9.14f,open?33.2f:29.43f,3);
            if(hatchCollider)hatchCollider.enabled=!open;
        }
        void OnDestroy()
        {
            foreach(var mat in materials)if(mat)
            {if(Application.isPlaying)Destroy(mat);else DestroyImmediate(mat);}
        }
    }
}
