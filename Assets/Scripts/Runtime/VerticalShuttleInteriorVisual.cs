using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Gameplay;

namespace LestingCraft.Runtime
{
    /// <summary>Physical fitting for the VERTICAL owner-approved orbiter, not the older
    /// horizontal /shuttle-demo. Floors, hatch, seats and controls have solid colliders.
    /// The local moving lift can carry one colliding CharacterController. Ladder climbing,
    /// controls and network authority still require connection before live multiplayer.</summary>
    public sealed class VerticalShuttleInteriorVisual : MonoBehaviour
    {
        readonly List<Material> materials=new List<Material>();
        Transform hatch,liftPlatform;
        BoxCollider hatchCollider;
        FirstPersonController liftRider;
        float lastLiftHeight;
        public readonly ShuttleElevator Lift=new ShuttleElevator(VerticalShuttleWalk.LiftTravel);
        public bool HatchOpen {get;private set;}

        public static VerticalShuttleInteriorVisual Create(Transform parent)
        {
            var node=new GameObject("Approved orbiter · physically fitted vertical cabin");
            node.transform.SetParent(parent,false);
            var visual=node.AddComponent<VerticalShuttleInteriorVisual>();
            visual.Build();return visual;
        }
        Material Mat(Color color,bool metal=false)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(!shader)throw new InvalidOperationException("Missing cabin shader");
            var mat=new Material(shader){name="Orbiter cabin fitting",color=color};
            mat.SetFloat("_Metallic",metal?.4f:0);mat.SetFloat("_Smoothness",metal?.42f:.16f);
            materials.Add(mat);return mat;
        }
        Transform Box(string name,Vector3 position,Vector3 size,Material material,bool collision=true)
        {
            var part=GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name=name;part.transform.SetParent(transform,false);
            part.transform.localPosition=position;part.transform.localScale=size;
            part.GetComponent<MeshRenderer>().sharedMaterial=material;
            if(!collision)DestroyComponent(part.GetComponent<BoxCollider>());
            return part.transform;
        }
        static void DestroyComponent(UnityEngine.Object item)
        {if(Application.isPlaying)Destroy(item);else DestroyImmediate(item);}
        void Build()
        {
            SpaceVoxelVisual.CreateWalkableShuttle(transform); // real hull mesh and collision; approved source intact
            var floor=Mat(new Color(.25f,.34f,.42f),true);
            var wall=Mat(new Color(.83f,.87f,.91f));
            var dark=Mat(new Color(.08f,.13f,.19f));
            var cyan=Mat(new Color(.10f,.62f,.73f));
            var amber=Mat(new Color(.94f,.51f,.15f));
            // The source hull's voxel at Y=27 is solid; an even collider at Y=28 provides
            // a continuous foot plane through the freshly carved 2 m tower-side opening.
            Box("Physical tower-to-airlock walkway",new Vector3(-5.5f,27.88f,3),new Vector3(7f,.24f,1.90f),floor);
            // A short bridge meets the pad deck at the upper stop, without sealing the lift shaft.
            Box("Tower lift exit bridge",new Vector3(-18.3f,27.88f,3),new Vector3(1.1f,.24f,1.90f),floor);
            Box("Lower crew deck",new Vector3(.5f,27.88f,3.5f),new Vector3(4.85f,.24f,4.85f),floor);
            Box("Lower deck equipment left",new Vector3(-1.62f,28.7f,1.18f),new Vector3(.48f,1.22f,.26f),dark);
            Box("Lower deck equipment right",new Vector3(2.56f,28.7f,1.18f),new Vector3(.48f,1.22f,.26f),dark);
            Box("Pressure tank fitting",new Vector3(2.58f,29.32f,5.55f),new Vector3(.34f,1.60f,.48f),wall);
            // Open centre for a 3 x 3 metre climb; side strips and forward bridge are
            // continuous with the upper deck without sealing off the shaft.
            Box("Cockpit port floor",new Vector3(-1.52f,41.86f,3.5f),new Vector3(.92f,.28f,3.95f),floor);
            Box("Cockpit starboard floor",new Vector3(2.49f,41.86f,3.5f),new Vector3(.92f,.28f,3.95f),floor);
            Box("Cockpit forward bridge",new Vector3(.5f,41.86f,5.46f),new Vector3(4.9f,.28f,1.05f),floor);
            // Landing occupies one side of the shaft. The other side stays open for ascent.
            Box("Cockpit ladder landing",new Vector3(1.25f,41.86f,3.86f),new Vector3(1.28f,.28f,2.35f),floor);
            for(int y=34;y<=41;y++)
            {
                // Decorative ladder rungs have no colliders: player motion must instead
                // use continuous CharacterController.Move, validated inside the shaft.
                Box("Ladder rung "+y,new Vector3(.5f,y+.15f,2.14f),new Vector3(1.40f,.055f,.09f),amber,false);
            }
            Box("Ladder grip left",new Vector3(-.28f,37.2f,2.14f),new Vector3(.07f,8.15f,.07f),wall,false);
            Box("Ladder grip right",new Vector3(1.28f,37.2f,2.14f),new Vector3(.07f,8.15f,.07f),wall,false);
            for(int index=0;index<VerticalShuttleWalk.SeatCount;index++)
            {
                double x,y,z;VerticalShuttleWalk.Seat(index,out x,out y,out z);
                float sx=(float)x,sz=(float)z;
                Box("Crew seat "+index+" cushion",new Vector3(sx,42.42f,sz),new Vector3(.82f,.22f,.62f),dark);
                Box("Crew seat "+index+" back",new Vector3(sx,42.92f,sz-.29f),new Vector3(.83f,1.05f,.13f),dark);
                Box("Crew seat "+index+" restraint",new Vector3(sx,43.2f,sz+.05f),new Vector3(.65f,.10f,.12f),amber);
            }
            Box("Pilot navigation display",new Vector3(-.4f,43.56f,5.72f),new Vector3(.62f,.37f,.08f),cyan);
            Box("Copilot guidance display",new Vector3(1.4f,43.56f,5.72f),new Vector3(.62f,.37f,.08f),cyan);
            Box("Overhead main bus and ignition switches",new Vector3(.5f,44.93f,4.73f),new Vector3(1.45f,.16f,.68f),amber);
            hatch=Box("Closed pressure hatch · server controlled",new Vector3(-9.14f,29.43f,3),new Vector3(.18f,2.86f,1.94f),wall);
            hatchCollider=hatch.GetComponent<BoxCollider>();
            // Platform starts at pad walking surface Y=0 and arrives flush
            // with cabin entry Y=28, clearing the approved booster. No teleport occurs.
            liftPlatform=Box("Moving crew lift",new Vector3(VerticalShuttleWalk.LiftX,-.14f,3),new Vector3(1.9f,.28f,1.9f),floor);
            Box("Lift safety back",new Vector3(VerticalShuttleWalk.LiftX-.93f,.86f,3),new Vector3(.08f,1.9f,1.88f),dark).SetParent(liftPlatform,true);
            Box("Lift safety port",new Vector3(VerticalShuttleWalk.LiftX,.65f,2.08f),new Vector3(1.9f,1.45f,.08f),dark).SetParent(liftPlatform,true);
            Box("Lift safety starboard",new Vector3(VerticalShuttleWalk.LiftX,.65f,3.92f),new Vector3(1.9f,1.45f,.08f),dark).SetParent(liftPlatform,true);
            for(int y=0;y<=28;y+=5)
            {
                Box("Lift guide rear "+y,new Vector3(VerticalShuttleWalk.LiftX-.99f,y+.5f,2.02f),new Vector3(.09f,4.8f,.09f),wall);
                Box("Lift guide front "+y,new Vector3(VerticalShuttleWalk.LiftX-.99f,y+.5f,3.98f),new Vector3(.09f,4.8f,.09f),wall);
            }
            SyncHatch(false);
        }
        /// <summary>Board only if the real CharacterController is standing on the stationary
        /// platform. Future network authority must confirm this before activating both clients.</summary>
        public bool BoardLift(FirstPersonController player)
        {
            if(!player||Lift.IsMoving||Lift.Occupied)return false;
            var p=transform.InverseTransformPoint(player.transform.position);
            if(Mathf.Abs(p.x-VerticalShuttleWalk.LiftX)>.85f||Mathf.Abs(p.z-3f)>.85f||
               Mathf.Abs(p.y-Lift.Height)>1f||!Lift.Board())return false;
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
        void LateUpdate()
        {
            if(!liftPlatform)return;
            Lift.Advance(Time.unscaledDeltaTime);
            float height=Lift.Height,delta=height-lastLiftHeight;
            liftPlatform.localPosition=new Vector3(VerticalShuttleWalk.LiftX,height-.14f,3);
            if(liftRider&&Lift.Occupied&&Mathf.Abs(delta)>.0001f)
                liftRider.CarryByPlatform(transform.up*delta);
            lastLiftHeight=height;
            if(liftRider&&Lift.Occupied&&!Lift.IsMoving)
            {
                var p=transform.InverseTransformPoint(liftRider.transform.position);
                if(Mathf.Abs(p.x-VerticalShuttleWalk.LiftX)>1.15f||Mathf.Abs(p.z-3f)>1.15f||
                   Mathf.Abs(p.y-Lift.Height)>1.35f)
                {Lift.Exit();liftRider=null;}
            }
        }
        /// <summary>Visual/physical reaction to an authoritative door state; does not grant
        /// access, launch a mission, transfer inventory or teleport the player.</summary>
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
