using System;
using UnityEngine;
using UnityEngine.InputSystem;
using LestingCraft.World;
using LestingCraft.Gameplay;
using LestingCraft.Presentation;

namespace LestingCraft.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public VoxelWorld World;
        public PrototypeGame Game;
        public Camera Camera;
        public bool Active;
        public bool Sprinting {get;private set;}
        public bool Crouching {get;private set;}
        public float GravityScale = 1f;
        public bool ZeroGActive;
        public ushort Selected=BlockRegistry.StoneId;
        CharacterController controller;
        public bool TargetingAnimal {get;private set;}
        public bool HasTarget {get;private set;}
        public VoxelRaycast.Hit Target {get;private set;}
        float pitch,vertical;
        Vector3 spawn,impulse,zeroGVelocity;
        // Feel-quality movement physics: coyote time (grace window after leaving the
        // ground), jump buffer (queue a jump pressed slightly before landing) and a
        // variable jump height (release space early to cut the ascent).
        const float CoyoteTime = .12f, JumpBuffer = .12f;
        float coyoteTimer, jumpBufferTimer;
        bool wasGrounded, jumpCutApplied, wasSwimming;
        float eyeOffsetCurrent = PlayerCapsule.EyeHeight;
        float landDip, stepAccum, bobPhase;

        public static bool HasFootSupport(float x, float z, int supportY, float r, Func<int, int, int, ushort> getBlock)
        {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    int bx = Mathf.FloorToInt(x + sx * r);
                    int bz = Mathf.FloorToInt(z + sz * r);
                    if (BlockRegistry.IsSolid(getBlock(bx, supportY, bz)))
                        return true;
                }
            return false;
        }

        /// <summary>Safe ledge-walk clamp while crouching: allows leaning to the block rim without falling off.</summary>
        public static Vector3 ClampLedgeStep(Vector3 pos, Vector3 deltaXZ, float halfSupport, Func<int, int, int, ushort> getBlock)
        {
            int supportY = Mathf.FloorToInt(pos.y - 0.15f);
            if (!HasFootSupport(pos.x, pos.z, supportY, halfSupport, getBlock))
                return deltaXZ;
            float dx = deltaXZ.x, dz = deltaXZ.z;
            if (Mathf.Abs(dx) > 1e-5f && !HasFootSupport(pos.x + dx, pos.z, supportY, halfSupport, getBlock))
                dx = 0f;
            if (Mathf.Abs(dz) > 1e-5f && !HasFootSupport(pos.x + dx, pos.z + dz, supportY, halfSupport, getBlock))
                dz = 0f;
            return new Vector3(dx, 0f, dz);
        }

        public void Knockback(Vector3 force){impulse=Vector3.ClampMagnitude(impulse+force,12);}

        /// <summary>Move together with a physical lift; performs CharacterController collision checks every frame.</summary>
        public void CarryByPlatform(Vector3 delta)
        {
            if(!controller)controller=GetComponent<CharacterController>();
            if(controller.enabled)controller.Move(delta);
            vertical=0;
        }

        public void TeleportSpawn(){if(!controller)controller=GetComponent<CharacterController>();controller.enabled=false;transform.position=spawn;controller.enabled=true;vertical=0;zeroGVelocity=Vector3.zero;}
        public void SetCheckpoint(Vector3 p){spawn=p;}
        public void TeleportTo(Vector3 p,bool checkpoint=false){if(!controller)controller=GetComponent<CharacterController>();controller.enabled=false;transform.position=p;controller.enabled=true;vertical=0;zeroGVelocity=Vector3.zero;if(checkpoint)spawn=p;}

        void Start(){controller=GetComponent<CharacterController>();spawn=transform.position;gameObject.AddComponent<LestingCraft.Presentation.BlockFeedback>().Initialize(this);}

        void Update()
        {
            TargetingAnimal=false;if(Game&&Game.Session&&Game.Session.Living)Game.Session.Living.Hint="";
            var keyboard=Keyboard.current;var mouse=Mouse.current;if(keyboard==null||mouse==null||World==null)return;
            if(Game.StoryLocked){if(keyboard.escapeKey.wasPressedThisFrame)Game.TogglePause();Sprinting=false;HasTarget=false;return;}
            if(Game.ChatOpen){Sprinting=false;HasTarget=false;if(keyboard.escapeKey.wasPressedThisFrame)Game.CloseChat();else if(keyboard.enterKey.wasPressedThisFrame)Game.SubmitChat();return;}

            // When seated in shuttle, T toggles 30x Time Warp instead of opening chat! Enter still opens chat anytime (unless isolated in Dream).
            bool seatedInShuttle=(Game.ShuttleDemo&&Game.ShuttleDemo.IsPlayerSeated)||Game.ShuttleSeated;
            bool inDream=Game.InDreamDimension;
            if(!inDream&&!Game.Paused&&World.Ready&&(keyboard.enterKey.wasPressedThisFrame||(!seatedInShuttle&&keyboard.tKey.wasPressedThisFrame)))
            {Game.OpenChat();Game.Session.ResetMining();HasTarget=false;return;}

            if(keyboard.escapeKey.wasPressedThisFrame)
            {
                if(Game.CockpitMouseMode){Game.SetCockpitMouseMode(false);return;}
                if(Game.StationTradeOpen){Game.CloseStationTrade();return;}
                if(Game.InventoryOpen){Game.ToggleInventory();return;}
                Game.TogglePause();return;
            }
            if(Game.Paused&&Game.EditingText)return;

            // === DREAM ANOMALY: SLEEP PARALYSIS -> WALKABLE LIMINAL NIGHTMARE (ZERO INTERACTION) ===
            if(inDream)
            {
                Sprinting=false;HasTarget=false;
                if(Game.Session)Game.Session.ResetMining();
                if(!Active||!World.Ready)return;

                if(Game.Dream.IsParalyzed)
                {
                    // Immobilized in bed/threshold: panicked micro-tremor only, heavy breathing sway
                    var pDelta=mouse.delta.ReadValue()*0.08f;
                    pitch=Mathf.Clamp(pitch-pDelta.y,-22f,-4f);
                    float tremor=Mathf.Sin(Time.time*11.5f)*0.55f*(float)Game.Dream.ParalysisTremor;
                    Camera.transform.localRotation=Quaternion.Euler(pitch+tremor,Mathf.Sin(Time.time*7.3f)*1.4f,tremor*0.6f);
                    vertical=0f;
                    return;
                }

                // Walkable Nightmare phase: full movement & camera look, STRICT ZERO item/block/multiplayer interaction
                var dLook=mouse.delta.ReadValue();
                pitch=Mathf.Clamp(pitch-dLook.y*LestingCraft.Presentation.VisualSettings.Current.Sensitivity,-85,85);
                transform.Rotate(0,dLook.x*LestingCraft.Presentation.VisualSettings.Current.Sensitivity,0);
                Camera.transform.localRotation=Quaternion.Euler(pitch,0,0);

                var dMove=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
                if(dMove.sqrMagnitude>1)dMove.Normalize();
                bool dreamSprint=keyboard.leftShiftKey.isPressed&&dMove.sqrMagnitude>0;
                Sprinting=dreamSprint;
                float dSpeed=dreamSprint?6.2f:4.1f;
                float dGrav=Mathf.Clamp(GravityScale,.12f,2.0f);
                if(controller.isGrounded&&vertical<0)vertical=-2;
                if(controller.isGrounded&&keyboard.spaceKey.wasPressedThisFrame)vertical=7.4f;
                vertical=Mathf.Max(vertical-22f*dGrav*Time.deltaTime,-40f*dGrav);
                Vector3 dVel=(transform.right*dMove.x+transform.forward*dMove.y)*dSpeed;
                PlayerCapsule.UpdateStep(controller,dVel);
                controller.Move((dVel+Vector3.up*vertical)*Time.deltaTime);
                if(transform.position.y<12f){TeleportTo(new Vector3(0.5f,45.2f,0.5f));}
                return;
            }

            if(!Game.Paused&&Active&&Game.ShuttleDemo&&keyboard.leftShiftKey.wasPressedThisFrame&&Game.ShuttleDemo.IsPlayerSeated)
            {
                string note;Game.ShuttleDemo.StandFromSeat(out note);Game.Session.Notice=note;return;
            }
            if(!Game.Paused&&Active&&Game.DockedAtOrbitalStation&&keyboard.zKey.wasPressedThisFrame)
            {
                ZeroGActive=!ZeroGActive;
                if(Game.Session)Game.Session.Notice=ZeroGActive?"Режим невесомости (0g) включён · Space / Ctrl — вверх / вниз":"Магнитные ботинки включены (ходьба по палубе станции)";
            }
            if(!Game.Paused&&Active&&keyboard.f9Key.wasPressedThisFrame&&Game.Session&&Game.Session.Creative)
            {
                Game.QuickPlaceCosmodromeNearPlayer();
                return;
            }
            if(!Game.Paused&&Active&&keyboard.kKey.wasPressedThisFrame)
            {
                Game.ReadNextStoryCodex();
                return;
            }
            if(keyboard.eKey.wasPressedThisFrame&&!(seatedInShuttle&&Game.SpaceHelm.Active))
            {
                if(Game.StationTradeOpen){Game.CloseStationTrade();return;}
                string note;
                if(!Game.Paused&&Active&&Game.ShuttleDemo&&Game.ShuttleDemo.TryInteract(new Ray(Camera.transform.position,Camera.transform.forward),out note))
                {Game.Session.Notice=note;return;}
                if(!Game.Paused&&Active&&Game.TryInteractStation())return;
                if(!Game.Paused&&Active&&Game.TryInteractLaunch())return;
                Game.ToggleInventory();return;
            }
            if(!Active||!World.Ready){Sprinting=false;HasTarget=false;if(Game.Session)Game.Session.ResetMining();return;}

            if(keyboard.fKey.wasPressedThisFrame&&Game.Tech.ToggleMountNearestTrain(transform.position,out string trainNote))
            {
                Game.Session.Notice=trainNote;
                return;
            }
            if(keyboard.vKey.wasPressedThisFrame&&Game.Tech.ToggleDroneFpv(out string droneNote))
            {
                Game.Session.Notice=droneNote;
                return;
            }
            if(Game.Tech.IsRidingTrain||Game.Tech.IsDroneFpv)
            {
                Sprinting=false;HasTarget=false;vertical=0;return;
            }
            if(seatedInShuttle)
            {
                if(keyboard.mKey.wasPressedThisFrame)
                    Game.SetCockpitMouseMode(!Game.CockpitMouseMode);
                bool cursorFree=Game.CockpitMouseMode||keyboard.tabKey.isPressed;
                if(!cursorFree)
                {
                    var sDelta=mouse.delta.ReadValue();
                    pitch=Mathf.Clamp(pitch-sDelta.y*LestingCraft.Presentation.VisualSettings.Current.Sensitivity,-85,85);
                    transform.Rotate(0,sDelta.x*LestingCraft.Presentation.VisualSettings.Current.Sensitivity,0);
                    Camera.transform.localRotation=Quaternion.Euler(pitch,0,0);
                }
                Game.HandleSeatedCockpitMouse(Camera,mouse,cursorFree);
                Game.HandleSeatedShuttleInput(keyboard);
                Sprinting=false;HasTarget=false;vertical=0;return;
            }

            var delta=mouse.delta.ReadValue();pitch=Mathf.Clamp(pitch-delta.y*LestingCraft.Presentation.VisualSettings.Current.Sensitivity,-85,85);
            transform.Rotate(0,delta.x*LestingCraft.Presentation.VisualSettings.Current.Sensitivity,0);Camera.transform.localRotation=Quaternion.Euler(pitch,0,0);

            var cabin=Game.LaunchSite?Game.LaunchSite.Cabin:null;
            float climb=keyboard.spaceKey.isPressed?1f:keyboard.leftCtrlKey.isPressed?-1f:0f;
            if(climb!=0&&cabin&&cabin.TryClimb(this,climb,Mathf.Min(Time.deltaTime,.2f)))
            {Sprinting=false;HasTarget=false;vertical=0;Game.Session.ResetMining();return;}

            var move=new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),(keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
            if(move.sqrMagnitude>1)move.Normalize();

            if(ZeroGActive&&Game.DockedAtOrbitalStation)
            {
                float thrustUp=(keyboard.spaceKey.isPressed?1f:0f)-(keyboard.leftCtrlKey.isPressed?1f:0f);
                Vector3 wish=(Camera.transform.right*move.x+Camera.transform.forward*move.y+Vector3.up*thrustUp)*5.5f;
                zeroGVelocity=Vector3.MoveTowards(zeroGVelocity,wish,Time.deltaTime*14f);
                controller.Move((zeroGVelocity+impulse)*Time.deltaTime);
                impulse=Vector3.MoveTowards(impulse,Vector3.zero,Time.deltaTime*15f);
                vertical=0;Sprinting=false;
                return;
            }

            bool swimming=!Game.DockedAtOrbitalStation&&World.InFluid(transform.position+Vector3.up*.8f,7);
            if(swimming&&!wasSwimming)GameAudio.PlayLand(6f,true);
            wasSwimming=swimming;
            Crouching=!swimming&&!Game.DockedAtOrbitalStation&&(keyboard.cKey.isPressed||keyboard.leftCtrlKey.isPressed);
            Sprinting=Game.Session.Vitals.body.CanSprint&&!swimming&&!Crouching&&keyboard.leftShiftKey.isPressed&&move.sqrMagnitude>0&&Game.Session&&Game.Session.Vitals.stamina>5;
            float speed=swimming?2.6f:Crouching?2.65f:Sprinting?8:5;
            speed*=Game.Session.Vitals.body.Movement;
            float grav=Mathf.Clamp(GravityScale,.12f,2.0f);

            // Coyote time: keep the grounded window open briefly after walking off an edge,
            // so a jump pressed at the very edge still registers. Jump buffer: remember a
            // jump pressed slightly before landing so it fires on the next ground contact.
            if(controller.isGrounded){coyoteTimer=CoyoteTime;wasGrounded=true;}
            else if(wasGrounded){coyoteTimer-=Time.deltaTime;if(coyoteTimer<=0)wasGrounded=false;}
            if(keyboard.spaceKey.wasPressedThisFrame)jumpBufferTimer=JumpBuffer;
            else jumpBufferTimer-=Time.deltaTime;

            float swimBob=0f;
            if(swimming)
            {
                // Buoyancy: vertical velocity eases toward a neutral band; space rises,
                // ctrl dives, otherwise settle with a gentle frame-rate-independent surface bob.
                float buoyTarget=keyboard.spaceKey.isPressed?2.8f:keyboard.leftCtrlKey.isPressed?-2.8f:-.18f;
                vertical=Mathf.MoveTowards(vertical,buoyTarget,Time.deltaTime*8f);
                if(!keyboard.spaceKey.isPressed&&!keyboard.leftCtrlKey.isPressed)
                    swimBob=Mathf.Sin(Time.time*2.4f)*.18f;
            }
            else
            {
                if(controller.isGrounded&&vertical<0)vertical=-2;
                if(jumpBufferTimer>0&&coyoteTimer>0)
                {
                    vertical=8f*Mathf.Lerp(1f,1.35f,1f-Mathf.Clamp01(grav))*Game.Session.Vitals.body.Movement;
                    jumpBufferTimer=0;coyoteTimer=0;wasGrounded=false;jumpCutApplied=false;
                    GameAudio.PlayJump(false);
                }
                // Variable jump height: releasing space early trims apex while still clearing a 1m voxel block.
                if(vertical>6.95f&&!keyboard.spaceKey.isPressed&&!jumpCutApplied){vertical=Mathf.Max(6.95f,vertical*.86f);jumpCutApplied=true;}
                vertical=Mathf.Max(vertical-22f*grav*Time.deltaTime,-40f*grav);
            }
            Vector3 velocity=(transform.right*move.x+transform.forward*move.y)*speed;
            Vector3 movementTarget=transform.position+velocity*Time.deltaTime;
            if(!Game.DockedAtOrbitalStation&&(!World.Loaded(movementTarget)||!World.Contains(Mathf.FloorToInt(movementTarget.x),1,Mathf.FloorToInt(movementTarget.z))))velocity=Vector3.zero;
            if(Crouching&&controller.isGrounded&&!swimming&&velocity.sqrMagnitude>1e-5f)
            {
                Vector3 clampedStep=ClampLedgeStep(transform.position,velocity*Time.deltaTime,0.24f,World.GetBlock);
                velocity=clampedStep/Mathf.Max(1e-5f,Time.deltaTime);
            }
            float impact=vertical;bool groundedBefore=controller.isGrounded;
            Vector3 posBefore=transform.position;
            PlayerCapsule.UpdateStep(controller,velocity);
            controller.Move((velocity+Vector3.up*(vertical+swimBob)+impulse)*Time.deltaTime);impulse=Vector3.MoveTowards(impulse,Vector3.zero,Time.deltaTime*15);
            if(!groundedBefore&&controller.isGrounded&&impact<-3.2f)
            {
                landDip=Mathf.Clamp(-impact*0.013f,0.035f,0.17f);
                GameAudio.PlayLand(-impact,swimming);
            }
            // Footstep cadence + subtle eye height & sprint FOV feel
            float horizDist=new Vector2(transform.position.x-posBefore.x,transform.position.z-posBefore.z).magnitude;
            if((controller.isGrounded||swimming)&&horizDist>0.001f)
            {
                stepAccum+=horizDist;
                bobPhase+=horizDist*3.1f;
                float strideLen=swimming?2.1f:Sprinting?2.35f:Crouching?1.45f:1.85f;
                if(stepAccum>=strideLen)
                {
                    stepAccum=0f;
                    ushort below=swimming?(ushort)7:World.GetBlock(Mathf.FloorToInt(transform.position.x),Mathf.FloorToInt(transform.position.y-0.2f),Mathf.FloorToInt(transform.position.z));
                    GameAudio.PlayFootstep(below,Sprinting,Crouching,Game.DockedAtOrbitalStation);
                }
            }
            else
            {
                stepAccum=Mathf.Min(stepAccum,1.1f);
            }
            if(Camera)
            {
                landDip=Mathf.MoveTowards(landDip,0f,Time.deltaTime*0.65f);
                float targetEye=Crouching?1.42f:PlayerCapsule.EyeHeight;
                eyeOffsetCurrent=Mathf.Lerp(eyeOffsetCurrent,targetEye,1f-Mathf.Exp(-Time.deltaTime*14f));
                float bobY=controller.isGrounded&&horizDist>0.002f?Mathf.Sin(bobPhase)*(Sprinting?0.028f:Crouching?0.010f:0.019f):0f;
                Camera.transform.localPosition=new Vector3(0f,eyeOffsetCurrent-landDip+bobY,0f);
                if(VisualSettings.Current!=null)
                {
                    float baseFov=VisualSettings.Current.FieldOfView;
                    float wantFov=baseFov+(Sprinting?6.5f:Crouching?-2.0f:0f);
                    Camera.fieldOfView=Mathf.Lerp(Camera.fieldOfView,wantFov,1f-Mathf.Exp(-Time.deltaTime*10f));
                }
            }
            float safeImpact=11f/Mathf.Sqrt(grav);
            if(!groundedBefore&&controller.isGrounded&&impact< -safeImpact&&Game.Session){float hurt=(-impact-safeImpact)*1.5f*grav;Game.Session.DamagePart(hurt,BodyPart.LeftLeg);Game.Session.DamagePart(hurt,BodyPart.RightLeg);}
            if(!Game.DockedAtOrbitalStation&&!Game.InMoonFlight&&(transform.position.y<1||transform.position.y>250)){if(Game.Session)Game.Session.Damage(10000);controller.enabled=false;transform.position=spawn;controller.enabled=true;vertical=0;}
            if(!Game.Session||Game.DockedAtOrbitalStation)return;

            var keys=new[]{keyboard.digit1Key,keyboard.digit2Key,keyboard.digit3Key,keyboard.digit4Key,keyboard.digit5Key,keyboard.digit6Key,keyboard.digit7Key,keyboard.digit8Key,keyboard.digit9Key};
            for(int i=0;i<keys.Length;i++)if(keys[i].wasPressedThisFrame)Game.Session.Inventory.selected=i;
            if(keyboard.hKey.wasPressedThisFrame){var inv=Game.Session.Inventory;if(inv.Count(Items.Fiber)>=3&&Game.Session.Vitals.Treat()){inv.Remove(Items.Fiber,3);GameAudio.PlayHealOrEat();Game.Session.Notice="Повязка: восстановлено 22 HP";Game.Session.Checkpoint();}else Game.Session.Notice="Для повязки нужны 3 волокна и повреждение";}
            if(keyboard.gKey.wasPressedThisFrame&&!Game.Session.Inventory.ToggleOffhand())Game.Session.Notice="G · выберите факел на хотбаре; для снятия нужно место в сумке";
            if(keyboard.qKey.wasPressedThisFrame)Game.Session.Living.DropActive();
            if(Game.Session.Living.Pending){Game.Session.ResetMining();return;}
            if(mouse.leftButton.wasPressedThisFrame&&Weapons.Has(Game.Session.Inventory.Active.item)&&Game.Session.Inventory.Active.item!=164)
            {
                GameAudio.PlaySwing();
                WorldVisualEffects.TriggerSlash(Camera.transform.position,Camera.transform.rotation,Game.Session.Inventory.Active.item);
            }
            if(Game.Story&&mouse.leftButton.isPressed&&Game.Story.Attack(new Ray(Camera.transform.position,Camera.transform.forward),6)){TargetingAnimal=true;HasTarget=false;Game.Session.ResetMining();return;}
            if(mouse.leftButton.wasPressedThisFrame&&Game.TryAttackPlanetaryBoss(new Ray(Camera.transform.position,Camera.transform.forward))){TargetingAnimal=true;HasTarget=false;Game.Session.ResetMining();return;}
            Selected=SurvivalSession.PlaceableBlockForItem(Game.Session.Inventory.Active.item,World.ContentVersion);
            HasTarget=VoxelRaycast.Trace(Camera.transform.position,Camera.transform.forward,6,World.GetBlock,out var target);
            Target=target;
            if(Game.LaunchPlacementArmed&&mouse.rightButton.wasPressedThisFrame)
            {Game.Session.ResetMining();if(HasTarget)Game.PlaceLaunchAt(target.Cell);
             else Game.Session.Notice="Наведите прицел на землю рядом с вами";return;}
            if(Game.Session.Creative&&HasTarget&&mouse.middleButton.wasPressedThisFrame){int id=World.GetBlock(target.Cell.x,target.Cell.y,target.Cell.z);Game.Session.Inventory.slots[Game.Session.Inventory.selected]=new ItemStack{item=id==18?120:id,count=64};Game.Session.Inventory.revision++;}
            if(Game.Session.Living.Interact(new Ray(Camera.transform.position,Camera.transform.forward),HasTarget?target.Distance:6,mouse.rightButton.wasPressedThisFrame,mouse.leftButton.wasPressedThisFrame)){TargetingAnimal=true;HasTarget=false;Game.Session.ResetMining();return;}
            if(Game.Session.Inventory.Active.item==164&&!mouse.rightButton.wasPressedThisFrame){HasTarget=false;Game.Session.ResetMining();if(mouse.leftButton.wasPressedThisFrame)Game.Session.Bow.Fire();return;}
            if(Game.Session.Animals&&Game.Session.Animals.Intersect(new Ray(Camera.transform.position,Camera.transform.forward),HasTarget?target.Distance:6,out var animalId))
            {TargetingAnimal=true;HasTarget=false;Game.Session.ResetMining();if(mouse.leftButton.isPressed)Game.Session.Animals.Attack(animalId);return;}
            bool place=mouse.rightButton.wasPressedThisFrame;
            if(place)
            {
                Game.Session.ResetMining();
                if(Game.TryUseIndustrialItemOrBlock(HasTarget,target.Cell,target.Adjacent))return;
                if(!keyboard.leftShiftKey.isPressed&&HasTarget)
                {
                    int station=World.GetBlock(target.Cell.x,target.Cell.y,target.Cell.z);
                    if(station==BlockRegistry.BedBlockId){Game.UseBed(target.Cell);return;}
                    if(station==16||station==17){Game.OpenStation(station,target.Cell);return;}
                    if(FuelStations.Has(station)){Game.UseStation(station,target.Cell);return;}
                }
                if(Selected==0){Game.Session.UseActive();return;}
                if(!HasTarget||target.Normal==Vector3Int.zero)return;
                var cell=target.Adjacent;
                if(cell.y<=0||!World.Contains(cell.x,cell.y,cell.z)||!World.Loaded((Vector3)cell))return;
                if(!VoxelRaycast.OverlapsPlayer(cell,controller.bounds))Game.Session.Place(cell);
                else Game.Session.Notice="Нельзя поставить блок внутри персонажа";
            }
            else if(mouse.leftButton.isPressed&&HasTarget)
            {
                ushort minedId=World.GetBlock(target.Cell.x,target.Cell.y,target.Cell.z);
                if(BlockRegistry.Get(minedId).Hardness>=1.4f)
                {
                    Color sparkCol=minedId==BlockRegistry.CrystalId||minedId==BlockRegistry.AndromedaCrystalId?new Color(.55f,.88f,1f):new Color(1f,.72f,.26f);
                    WorldVisualEffects.EmitImpactSparks((Vector3)target.Cell+Vector3.one*.5f+(Vector3)target.Normal*.52f,target.Normal,sparkCol,10);
                }
                Game.Session.Mine(target.Cell,Mathf.Min(Time.deltaTime,.25f));
            }
            else Game.Session.ResetMining();

            var vfx=WorldVisualEffects.Ensure(Game.transform);
            if(vfx&&Camera)
            {
                vfx.TrackedCamera=Camera.transform;
                vfx.ActiveDimension=World.Dimension;
                vfx.DayClock01=Game.Session.Data!=null?(float)(Game.Session.Data.clock%1.0):0.5f;
                int actIt=Game.Session.Inventory.Active.count>0?Game.Session.Inventory.Active.item:0;
                bool hasOffhand=Game.Session.Inventory.offhand!=null&&Game.Session.Inventory.offhand.count>0;
                vfx.HoldingTorch=hasOffhand||actIt==Items.Torch||actIt==BlockRegistry.TorchBlockId;
            }
        }
    }
}
