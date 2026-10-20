using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;

namespace LestingCraft.Runtime
{
    /// <summary>Physical walkable station assembly inside the approved voxel exterior.
    /// Not spawned by the game until server-authoritative flight/boarding are connected.
    /// Root position is the station model origin; deck is at authored voxel Y=21.
    /// Hatch changes collider state; it never warps a CharacterController.</summary>
    public sealed class StationInteriorVisual : MonoBehaviour
    {
        const float DeckY=21f, HatchX=-42f;
        readonly List<Material> owned=new List<Material>();
        readonly List<Transform> crew=new List<Transform>();
        BoxCollider hatchCollision;
        Transform hatch;
        bool open;

        public static StationInteriorVisual Create(Transform parent)
        {
            var root=new GameObject("Station · approved exterior / walkable interior");
            root.transform.SetParent(parent,false);
            var visual=root.AddComponent<StationInteriorVisual>();
            visual.Build();return visual;
        }
        Material Mat(Color color,bool metallic=false)
        {
            var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");
            if(!shader)throw new InvalidOperationException("Missing station shader");
            var m=new Material(shader){name="Station · fitted interior"};m.color=color;
            m.SetFloat("_Metallic",metallic?.55f:.08f);m.SetFloat("_Smoothness",metallic?.42f:.18f);
            owned.Add(m);return m;
        }
        Transform Box(string name,Vector3 p,Vector3 scale,Material mat)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;
            obj.transform.SetParent(transform,false);obj.transform.localPosition=p;obj.transform.localScale=scale;
            obj.GetComponent<MeshRenderer>().sharedMaterial=mat;return obj.transform;
        }
        void Build()
        {
            // The source exterior stays unmodified. Mesh collision includes its physical hull,
            // while the fitted deck provides a continuous walk through its hollow central axis.
            SpaceVoxelVisual.Create("Station",transform,true);
            var hull=Mat(new Color(.84f,.88f,.91f)),deck=Mat(new Color(.26f,.35f,.42f),true),
                frame=Mat(new Color(.14f,.20f,.27f),true),accent=Mat(new Color(.23f,.70f,.76f)),
                window=Mat(new Color(.08f,.29f,.43f));
            for(int x=-44;x<20;x+=4)
            {
                int span=Math.Min(4,20-x);
                Box("Bolted walkway · "+x,new Vector3(x+span*.5f,DeckY-.14f,0),new Vector3(span,.28f,2.2f),deck);
                for(int side=-1;side<=1;side+=2)
                {
                    Box("Guard rail",new Vector3(x+span*.5f,DeckY+.66f,side*1.09f),new Vector3(span,.055f,.07f),frame);
                    Box("Lower rail",new Vector3(x+span*.5f,DeckY+.17f,side*1.09f),new Vector3(span,.045f,.065f),frame);
                    Box("Pressurised hull liner",new Vector3(x+span*.5f,DeckY+1.11f,side*1.22f),new Vector3(span,2.24f,.10f),hull);
                }
                Box("Lighting strip",new Vector3(x+span*.5f,DeckY+2.22f,0),new Vector3(span,.04f,.12f),accent);
                Box("Ceiling panel",new Vector3(x+span*.5f,DeckY+2.35f,0),new Vector3(span,.13f,2.5f),hull);
                if(x%8==-4||x%8==4)
                {
                    Box("Rib left",new Vector3(x,DeckY+1.1f,-1.17f),new Vector3(.10f,2.2f,.12f),frame);
                    Box("Rib right",new Vector3(x,DeckY+1.1f,1.17f),new Vector3(.10f,2.2f,.12f),frame);
                    Box("Rib arch",new Vector3(x,DeckY+2.22f,0),new Vector3(.10f,.10f,2.42f),frame);
                }
            }
            // Open docking collar extends beyond the authored -X end. No invisible join/teleport.
            Box("Docking collar rim left",new Vector3(-43,DeckY+1.1f,-1.25f),new Vector3(2,2.2f,.12f),frame);
            Box("Docking collar rim right",new Vector3(-43,DeckY+1.1f,1.25f),new Vector3(2,2.2f,.12f),frame);
            hatch=Box("Sliding pressure hatch",new Vector3(HatchX,DeckY+1.1f,0),new Vector3(.16f,2.2f,2.18f),frame);
            hatchCollision=hatch.GetComponent<BoxCollider>();
            Box("Pressurization control",new Vector3(-42.35f,DeckY+1.15f,-1.12f),new Vector3(.16f,.34f,.17f),accent);
            Box("Docking viewport",new Vector3(HatchX-.10f,DeckY+1.5f,0),new Vector3(.03f,.30f,.38f),window);
            for(int i=0;i<StationAstronauts.Count;i++)
            {
                SpaceVector p=StationAstronauts.Position(i);
                var npc=new GameObject(StationAstronauts.Profession(i));npc.transform.SetParent(transform,false);
                npc.transform.localPosition=new Vector3((float)p.X,DeckY,(float)p.Z);
                npc.transform.localRotation=Quaternion.Euler(0,i==1?45:-65,0);
                npc.AddComponent<CharacterVisual>().Build();
                var collider=npc.AddComponent<BoxCollider>();collider.size=new Vector3(.65f,1.8f,.6f);collider.center=new Vector3(0,.9f,0);
                var suit=Mat(new Color(.92f,.94f,.88f));
                BlockGeometry.Part("Astronaut EVA chest",npc.transform,new Vector3(0,1.08f,.17f),new Vector3(.53f,.51f,.09f),suit,1);
                BlockGeometry.Part("Astronaut sealed helmet",npc.transform,new Vector3(0,1.76f,0),new Vector3(.52f,.13f,.50f),suit,1);
                BlockGeometry.Part("Pressure backpack",npc.transform,new Vector3(0,1.05f,-.32f),new Vector3(.48f,.55f,.18f),suit,1);
                BlockGeometry.Part("Visor glass",npc.transform,new Vector3(0,1.54f,.213f),new Vector3(.37f,.27f,.03f),window,9);
                Box("Instrument rack",new Vector3((float)p.X+1.2f,DeckY+.95f,-1.08f),new Vector3(.7f,1.55f,.23f),frame);
                crew.Add(npc.transform);
            }
            SyncHatch(null);
        }
        public void SyncHatch(StationDocking state)
        {
            open=state!=null&&state.Latched&&state.HatchOpen;
            if(hatch)hatch.localPosition=new Vector3(HatchX,DeckY+(open?3.5f:1.1f),0);
            if(hatchCollision)hatchCollision.enabled=!open;
        }
        /// <summary>Only selects a merchant near a real player and a clear hatch;
        /// caller must send an authenticated trade request to the room, not apply items locally.</summary>
        public bool TryTrader(StationDocking dock,Vector3 localPlayerFeet,Ray worldRay,out int role)
        {
            role=-1;if(dock==null||!dock.Latched||!dock.HatchOpen)return false;
            var p=new Vector3(localPlayerFeet.x,localPlayerFeet.y-DeckY,localPlayerFeet.z);
            if(!StationInteriorLayout.Inside(p.x,p.y,p.z))return false;
            float best=3.5f;
            for(int i=0;i<crew.Count;i++)
            {
                var npc=crew[i];if(Vector3.Distance(localPlayerFeet,npc.localPosition)>4)continue;
                var box=npc.GetComponent<BoxCollider>();
                if(box.bounds.IntersectRay(worldRay,out float d)&&d<best){best=d;role=i;}
            }
            return role>=0;
        }
        void OnDestroy(){foreach(var mat in owned)if(mat){if(Application.isPlaying)Destroy(mat);else DestroyImmediate(mat);}}
    }
}
