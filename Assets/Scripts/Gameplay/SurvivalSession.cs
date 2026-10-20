using System;
using UnityEngine;
using LestingCraft.World;
using LestingCraft.Runtime;
using LestingCraft.Networking;
using LestingCraft.Presentation;

namespace LestingCraft.Gameplay
{
    public sealed class SurvivalSession : MonoBehaviour
    {
        public SessionSave Data {get;private set;}public PrototypeGame Game;public VoxelWorld World;public FirstPersonController Player;
        public LivingWorld Living {get;private set;}
        public BowCombat Bow {get;private set;}
        public ProfileTracker Progress {get;private set;}
        public WildlifeManager Animals {get;private set;}public DynamicWeather Weather {get;private set;}public bool Sheltered {get;private set;}
        public bool Creative=>Mode==GameMode.Creative;public GameMode Mode=>(GameMode)Data.mode;
        public Inventory Inventory=>Data.player.inventory;public SurvivalState Vitals=>Data.player.survival;
        public string Notice="";public bool Pending=>pending||(Living&&Living.Pending);public float MiningProgress {get;private set;}
        public bool ServerRunEnded;bool pending,wasDead;Vector3Int pendingCell,miningCell;int drop,spend,toolSlot,harvestBlock;float mineTime,saveTime,tick;GameObject grave;
        public void Initialize(PrototypeGame game,VoxelWorld world,FirstPersonController player,int mode,PlayerSave restored=null,SessionSave local=null,double clock=420)
        {
            Game=game;World=world;Player=player;Data=local??new SessionSave{seed=world.Seed,mode=mode,clock=clock,contentVersion=world.ContentVersion};
            if(restored!=null)Data.player=restored;Data.player.Normalize();
            if(local==null&&restored==null)
            {
                if(Creative){foreach(int id in new[]{1,3,4,5,6,44,15,16,17})Inventory.Add(id,Items.Get(id).MaxStack);}
                else if((mode!=3||world.ContentVersion<4)&&!(world.ContentVersion>=5&&mode!=2)){Inventory.Add(110,6);Inventory.Add(111,2);}
            }
            if(local!=null)foreach(var e in Data.blocks)if(e.realm==(int)World.Dimension)World.Edit(e.x,e.y,e.z,(ushort)e.block);
            Living=gameObject.AddComponent<LivingWorld>();Living.Initialize(this);
            Bow=gameObject.AddComponent<BowCombat>();Bow.Session=this;
            Progress=gameObject.AddComponent<ProfileTracker>();Progress.Initialize(this);
            Animals=gameObject.AddComponent<WildlifeManager>();Animals.Initialize(this);
            Weather=gameObject.AddComponent<DynamicWeather>();Weather.Initialize(this,player.Camera);wasDead=Vitals.dead;UpdateGrave();
        }
        public void Mine(Vector3Int cell,float dt)
        {
            if(pending||Vitals.dead||Data.campaign.runEnded){ResetMining();return;}
            ushort block=World.GetBlock(cell.x,cell.y,cell.z);if(block==0||block==9||block==7||block==8||cell.y==0){ResetMining();return;}
            if(!Creative&&ExpandedContent.NeedsPick(block)&&!Items.IsPick(Inventory.Active.item)){Notice="Для этого материала нужна кирка";ResetMining();return;}
            if(!Creative&&block==47&&Inventory.Active.item!=150&&Inventory.Active.item!=151){Notice="Для алмазов нужна железная или алмазная кирка";ResetMining();return;}
            int reward=Items.ToolDrop(block);if(!Creative&&!Inventory.CanAdd(reward,1)){Notice="Инвентарь заполнен";ResetMining();return;}
            Notice="";
            if(cell!=miningCell){miningCell=cell;mineTime=0;}
            float speed=Inventory.Active.item==151?7:Inventory.Active.item==150?5:Items.IsPick(Inventory.Active.item)?3:Inventory.Active.item==102&&block==4?4:1;
            float duration=VoxelRaycast.MiningSeconds(Creative,BlockRegistry.Get(block).Hardness,speed);
            mineTime+=dt*Vitals.body.Dexterity;MiningProgress=Mathf.Clamp01(mineTime/duration);if(mineTime<duration)return;
            mineTime=0;MiningProgress=0;pending=true;pendingCell=cell;drop=Creative?0:reward;harvestBlock=block;spend=0;toolSlot=Inventory.selected;Game.RequestEdit(cell,0);
        }
        public void ResetMining(){mineTime=0;MiningProgress=0;}
        public void Place(Vector3Int cell)
        {
            if(pending||Vitals.dead||Data.campaign.runEnded)return;var item=Items.Get(Inventory.Active.item);
            if(World.Online&&World.ContentVersion<5&&item.Block>=47){Notice="Нужен новый мир версии 5";return;}if(item.Block==0||Inventory.Active.count<=0)return;if(World.Online&&World.ContentVersion<2&&item.Block>18){Notice="Новые материалы доступны в комнате новой версии";return;}if(Living&&Living.Overlaps(cell)){Notice="Здесь житель или кот";return;}if(Animals&&Animals.Overlaps(cell)){Notice="Здесь животное";return;}
            if(World.GetBlock(cell.x,cell.y,cell.z)!=0&&World.GetBlock(cell.x,cell.y,cell.z)!=7){Notice="Место занято";return;}
            pending=true;pendingCell=cell;drop=0;spend=Creative?0:item.Id;toolSlot=-1;Game.RequestEdit(cell,item.Block);
        }
        public void ConfirmEdit(Vector3Int cell)
        {
            if(!pending||cell!=pendingCell)return;
            if(Living)Living.Edited(cell);
            if(drop>0){Progress.Add("mined");Progress.Add("mine_"+harvestBlock);if(World.ContentVersion<6)Inventory.Add(drop,1);else if(!Game.Online)Living.SpawnDrop(drop,1,0,(Vector3)cell+Vector3.one*.5f,Vector3.up*2);if(drop==122&&UnityEngine.Random.value<.25f)Inventory.Add(110,1);if(harvestBlock==3&&UnityEngine.Random.value<.45f)Inventory.Add(113,1);}
            if(spend>0){Progress.Add("placed");Progress.Add("place_"+Items.Get(spend).Block);Inventory.Remove(spend,1);}
            if(toolSlot>=0&&!Creative){int selected=Inventory.selected;Inventory.selected=toolSlot;Inventory.WearTool();Inventory.selected=selected;}
            pending=false;drop=spend=0;Notice="";
        }
        public void RejectEdit(string message){pending=false;Notice=message;ResetMining();}
        public bool CanInteract=>!(Living&&Living.Pending)&&!pending&&!Vitals.dead&&!Data.campaign.runEnded;
        public bool StationNearby(int id,Vector3Int cell)=>CanInteract&&Vector3.Distance(Player.Camera.transform.position,(Vector3)cell+Vector3.one*.5f)<=6.5f&&World.GetBlock(cell.x,cell.y,cell.z)==id;
        public bool Craft(int index)
        {
            if(!CanInteract||index<0||index>=CraftingTable.All.Length)return false;
            Notice="Рецепт выкладывается через книгу в сетку";return false;
        }
        public bool FillRecipe(int index,int width)
        {
            if(World.Online&&World.ContentVersion<4&&index>=0&&index<CraftingTable.All.Length&&CraftingTable.All[index].Output==148){Notice="Для факела нужна комната новых правил";return false;}
            if(!CanInteract||index<0||index>=CraftingTable.All.Length)return false;
            if(World.Online&&World.ContentVersion<7&&CraftingTable.RoomVersionFor(CraftingTable.All[index].Output)==7){Notice="Нужна новая комната версии 7";return false;}
            if(World.Online&&World.ContentVersion<5&&(CraftingTable.All[index].Output>=149||CraftingTable.All[index].Output==48)){Notice="Нужна новая комната версии 5";return false;}
            bool ok=CraftingTable.Fill(Inventory,CraftingTable.All[index],width);
            string costs="";foreach(var cost in Recipes.All[index].inputs)costs+=(costs.Length==0?"":" · ")+Items.Get(cost.item).Name+" ×"+cost.count;
            Notice=ok?"Рецепт выложен — заберите результат":(!CraftingTable.All[index].Fits(width)?"Нужен верстак 3×3. ":"Нужны ресурсы / место. ")+costs;return ok;
        }
        public int TakeCraft(int width,bool shift)
        {
            if(World.Online&&World.ContentVersion<4&&CraftingTable.Match(Inventory.grid,width)?.Output==148){Notice="Для факела нужна комната новых правил";return 0;}
            if(World.Online&&World.ContentVersion<5){var r=CraftingTable.Match(Inventory.grid,width);if(r!=null&&(r.Output>=149||r.Output==48)){Notice="Нужна новая комната версии 5";return 0;}}
            if(World.Online&&World.ContentVersion<7){var r7=CraftingTable.Match(Inventory.grid,width);if(r7!=null&&CraftingTable.RoomVersionFor(r7.Output)==7){Notice="Нужна новая комната версии 7";return 0;}}
            if(!CanInteract)return 0;var recipe=CraftingTable.Match(Inventory.grid,width);int count=CraftingTable.Take(Inventory,width,shift);
            if(count>0){Progress.Add("crafted",count);Progress.Add("craft_"+recipe.Output,count);if(recipe.Output==100||recipe.Output==101)Data.player.milestones|=1;Notice="Создано: "+Items.Get(recipe.Output).Name;}return count;
        }
        public void UseStation(int id,Vector3Int cell)
        {
            if(!CanInteract||!FuelStations.Has(id)||!StationNearby(id,cell))return;
            if(World.Online&&World.ContentVersion<FuelStations.RoomVersion){Notice="Нужна новая комната версии 7";return;}
            Notice=FuelStations.Use(Inventory,id);Inventory.revision++;
        }
        public void UseActive()
        {
            if(!CanInteract)return;var item=Items.Get(Inventory.Active.item);
            if(item.Food>0&&Vitals.hunger<100){Inventory.Remove(item.Id,1);Progress.Add("food");Vitals.Eat(item.Food);Notice="";}
            else if(Equipment.Slot(item.Id)>=0){Notice=Equipment.Equip(Inventory,Inventory.selected)?"Надето: "+item.Name:"Не удалось надеть предмет";}
        }
        public void Transfer(int from,int to){if(CanInteract)Inventory.Transfer(from,to);}
        public void DamagePart(float amount,BodyPart part){if(amount<=0||Vitals.dead||Creative)return;float protection=Equipment.Protection(Inventory,part);Vitals.DamagePart(amount*(1-protection),Mode,part);Equipment.Wear(Inventory,amount,part);}
        public void Damage(float amount){DamagePart(amount,BodyPart.Torso);}
        public void Impact(float amount,BodyPart part,Vector3 direction){DamagePart(amount,part);if(!Creative)Player.Knockback(direction.normalized*Mathf.Min(7,amount*.4f)+Vector3.up*1.4f);}
        public void Respawn()
        {
            if(Mode==GameMode.Hardcore||Data.campaign.runEnded||Living&&Living.Pending)return;
            Vitals.Respawn();wasDead=false;Player.TeleportSpawn();Game.Resume();Checkpoint();
        }
        void Update()
        {
            if(Data==null||!World||!Player||!World.Ready)return;
            if(!Game.Online&&Game.Paused&&!Game.InventoryOpen)return;
            Data.clock+=Time.deltaTime;if(Game.StoryLocked)return;
            tick+=Time.deltaTime;saveTime+=Time.unscaledDeltaTime;
            if(tick>=1)
            {
                float elapsed=Mathf.Min(2,tick);tick=0;
                var stove=Inventory.furnace;var stoveCell=new Vector3Int(stove.x,stove.y,stove.z);
                if(!Living.Pending&&stove.realm==(int)World.Dimension&&World.GetBlock(stove.x,stove.y,stove.z)==17&&Vector3.Distance(Player.transform.position,(Vector3)stoveCell+Vector3.one*.5f)<=6.5f)
                    {int before=stove.output.count;if(stove.Step(elapsed))Inventory.revision++;int made=stove.output.count-before;if(made>0){Progress.Add("smelt",made);Progress.Add("smelt_"+stove.output.item,made);}}
                var eye=Vector3Int.FloorToInt(Player.Camera.transform.position);var bodyCell=Vector3Int.FloorToInt(Player.transform.position+Vector3.up*.8f);
                Vitals.StepWater(elapsed,Mode,World.InFluid(Player.Camera.transform.position,7),World.InFluid(Player.transform.position+Vector3.up*.8f,7));
                Sheltered=Physics.Raycast(Player.Camera.transform.position,Vector3.up,out var hit,16)&&hit.collider.transform.parent==World.transform;
                bool warm=World.NearCampfire(Player.transform.position);if(warm&&Sheltered)Data.player.milestones|=2;
                
                var sample=WeatherSample.At(Data.seed,(int)World.Dimension,Data.clock);
                Vitals.Step(elapsed,Mode,sample.Air+(World.ContentVersion>=7&&World.Dimension==Dimension.Lestingia?BiomeGenerator.TemperatureOffset(BiomeGenerator.At(Mathf.FloorToInt(Player.transform.position.x),Mathf.FloorToInt(Player.transform.position.z),Data.seed)):0),sample.Precipitation,Sheltered,warm,Player.Sprinting,Equipment.Insulation(Inventory));
                var feet=Vector3Int.FloorToInt(Player.transform.position+Vector3.up*.1f);
                if(World.GetBlock(feet.x,feet.y,feet.z)==8){DamagePart(elapsed*12,BodyPart.LeftLeg);DamagePart(elapsed*12,BodyPart.RightLeg);}
                if(!Game.Online)Data.campaign.milestones|=Data.player.milestones;
            }
            if(Vitals.dead&&!wasDead)
            {
                wasDead=true;Progress.Add("deaths");
                if(Mode==GameMode.Hardcore){Data.campaign.runEnded=true;Notice="Хардкор завершён: погиб участник команды";}
                else
                {
                    // One personal recoverable grave; older grave items are forfeited on the next death.
                    Data.player.grave=Inventory.Clone().slots;Data.player.graveExtras=Inventory.ExtraStacks();Data.player.graveX=Player.transform.position.x;Data.player.graveZ=Player.transform.position.z;
                    Data.player.graveY=TerrainGenerator.Height(Mathf.FloorToInt(Data.player.graveX),Mathf.FloorToInt(Data.player.graveZ),Data.seed,World.Dimension)+1.2f;if(World.ContentVersion>=4)Data.player.graveY=Player.transform.position.y+.1f;
                    Data.player.inventory=new Inventory();Notice="Вещи остались у надгробия. Можно возродиться.";UpdateGrave();
                }
                Checkpoint();Game.OpenDeathScreen();
            }
            if(saveTime>20){saveTime=0;Checkpoint();}
            if(grave&&CanInteract&&!Vitals.dead&&Vector3.Distance(Player.transform.position,grave.transform.position)<2)
            {
                // A bag + crafting/cursor/furnace may contain more than 45 stacks. Recover what fits;
                // preserve the rest at the grave instead of requiring an impossible all-at-once transfer.
                int recovered=Inventory.RecoverStacks(Data.player.grave)+Inventory.RecoverStacks(Data.player.graveExtras);
                bool remaining=Array.Exists(Data.player.grave,s=>s.count>0)||Array.Exists(Data.player.graveExtras,s=>s.count>0);
                if(!remaining){Data.player.grave=Array.Empty<ItemStack>();Data.player.graveExtras=Array.Empty<ItemStack>();Destroy(grave);grave=null;Notice="Вещи возвращены";}
                else Notice="Освободите место для оставшихся вещей у надгробия";
                if(recovered>0)Checkpoint();
            }
        }
        void UpdateGrave()
        {
            if(grave)Destroy(grave);if(Data.player.grave==null||Data.player.grave.Length==0)return;
            grave=GameObject.CreatePrimitive(PrimitiveType.Cube);grave.name="Your recoverable grave";grave.transform.SetParent(transform);grave.transform.position=new Vector3(Data.player.graveX,Data.player.graveY,Data.player.graveZ);grave.transform.localScale=new Vector3(.55f,.8f,.22f);
            grave.GetComponent<MeshRenderer>().sharedMaterial=Resources.Load<Material>("Generated/Avatar");
            var light=grave.AddComponent<Light>();light.type=LightType.Point;light.color=new Color(.55f,.72f,1);light.range=4;light.intensity=1;
        }
        public void Checkpoint()
        {
            if(Data==null||!World||Game.IsSurfacePreview)return;
            if(Game.Online){if(!ServerRunEnded)Game.SendSave(Data.player);}
            else {Data.living=Living.Save();Data.blocks=World.ExportEdits();if(Animals)Data.wildlife=Animals.Save();LocalSave.Store(Data);}
        }
    }
}
