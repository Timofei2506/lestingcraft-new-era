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
        public DynamicWeather Weather {get;private set;}public bool Sheltered {get;private set;}
        public bool Creative=>Mode==GameMode.Creative;public GameMode Mode=>(GameMode)Data.mode;
        public Inventory Inventory=>Data.player.inventory;public SurvivalState Vitals=>Data.player.survival;
        public string Notice="";public bool Pending=>pending;public float MiningProgress {get;private set;}
        public bool ServerRunEnded;bool pending,wasDead;Vector3Int pendingCell,miningCell;int drop,spend,toolSlot,harvestBlock;float mineTime,saveTime,tick;GameObject grave;
        public void Initialize(PrototypeGame game,VoxelWorld world,FirstPersonController player,int mode,PlayerSave restored=null,SessionSave local=null,double clock=420)
        {
            Game=game;World=world;Player=player;Data=local??new SessionSave{seed=world.Seed,mode=mode,clock=clock};
            if(restored!=null)Data.player=restored;Data.player.Normalize();
            if(local==null&&restored==null)
            {
                if(Creative){foreach(int id in new[]{1,2,3,4,5,6,11,12,14,15,16,17,120})Inventory.Add(id,64);foreach(int id in new[]{100,101,102,103,104})Inventory.Add(id,1);}
                else {Inventory.Add(110,6);Inventory.Add(111,2);}
            }
            if(local!=null)foreach(var e in Data.blocks)if(e.realm==(int)World.Dimension)World.Edit(e.x,e.y,e.z,(ushort)e.block);
            Weather=gameObject.AddComponent<DynamicWeather>();Weather.Initialize(this,player.Camera);wasDead=Vitals.dead;UpdateGrave();
        }
        public void Mine(Vector3Int cell,float dt)
        {
            if(pending||Vitals.dead||Data.campaign.runEnded){ResetMining();return;}
            ushort block=World.GetBlock(cell.x,cell.y,cell.z);if(block==0||block==9||block==7||block==8||cell.y==0){ResetMining();return;}
            if(!Creative&&(block==1||block>=10&&block<=13||block==17)&&!Items.IsPick(Inventory.Active.item)){Notice="Для этого материала нужна кирка";ResetMining();return;}
            int reward=Items.ToolDrop(block);if(!Creative&&!Inventory.CanAdd(reward,1)){Notice="Инвентарь заполнен";ResetMining();return;}
            Notice="";
            if(cell!=miningCell){miningCell=cell;mineTime=0;}
            float speed=Items.IsPick(Inventory.Active.item)?3:Inventory.Active.item==102&&block==4?4:1;
            float duration=VoxelRaycast.MiningSeconds(Creative,BlockRegistry.Get(block).Hardness,speed);
            mineTime+=dt;MiningProgress=Mathf.Clamp01(mineTime/duration);if(mineTime<duration)return;
            mineTime=0;MiningProgress=0;pending=true;pendingCell=cell;drop=Creative?0:reward;harvestBlock=block;spend=0;toolSlot=Inventory.selected;Game.RequestEdit(cell,0);
        }
        public void ResetMining(){mineTime=0;MiningProgress=0;}
        public void Place(Vector3Int cell)
        {
            if(pending||Vitals.dead||Data.campaign.runEnded)return;var item=Items.Get(Inventory.Active.item);
            if(item.Block==0||Inventory.Active.count<=0)return;
            if(World.GetBlock(cell.x,cell.y,cell.z)!=0&&World.GetBlock(cell.x,cell.y,cell.z)!=7){Notice="Место занято";return;}
            pending=true;pendingCell=cell;drop=0;spend=Creative?0:item.Id;toolSlot=-1;Game.RequestEdit(cell,item.Block);
        }
        public void ConfirmEdit(Vector3Int cell)
        {
            if(!pending||cell!=pendingCell)return;
            if(drop>0){Inventory.Add(drop,1);if(drop==122&&UnityEngine.Random.value<.25f)Inventory.Add(110,1);if(harvestBlock==3&&UnityEngine.Random.value<.45f)Inventory.Add(113,1);}
            if(spend>0)Inventory.Remove(spend,1);
            if(toolSlot>=0&&!Creative){int selected=Inventory.selected;Inventory.selected=toolSlot;Inventory.WearTool();Inventory.selected=selected;}
            pending=false;drop=spend=0;Notice="";
        }
        public void RejectEdit(string message){pending=false;Notice=message;ResetMining();}
        public bool CanInteract=>!pending&&!Vitals.dead&&!Data.campaign.runEnded;
        public bool StationNearby(int id,Vector3Int cell)=>CanInteract&&Vector3.Distance(Player.Camera.transform.position,(Vector3)cell+Vector3.one*.5f)<=6.5f&&World.GetBlock(cell.x,cell.y,cell.z)==id;
        public bool Craft(int index)
        {
            if(!CanInteract||index<0||index>=CraftingTable.All.Length)return false;
            Notice="Рецепт выкладывается через книгу в сетку";return false;
        }
        public bool FillRecipe(int index,int width)
        {
            if(!CanInteract||index<0||index>=CraftingTable.All.Length)return false;
            bool ok=CraftingTable.Fill(Inventory,CraftingTable.All[index],width);
            string costs="";foreach(var cost in Recipes.All[index].inputs)costs+=(costs.Length==0?"":" · ")+Items.Get(cost.item).Name+" ×"+cost.count;
            Notice=ok?"Рецепт выложен — заберите результат":(!CraftingTable.All[index].Fits(width)?"Нужен верстак 3×3. ":"Нужны ресурсы / место. ")+costs;return ok;
        }
        public int TakeCraft(int width,bool shift)
        {
            if(!CanInteract)return 0;var recipe=CraftingTable.Match(Inventory.grid,width);int count=CraftingTable.Take(Inventory,width,shift);
            if(count>0){if(recipe.Output==100||recipe.Output==101)Data.player.milestones|=1;Notice="Создано: "+Items.Get(recipe.Output).Name;}return count;
        }
        public void UseActive()
        {
            if(pending||Vitals.dead)return;var item=Items.Get(Inventory.Active.item);
            if(item.Food>0&&Vitals.hunger<100){Inventory.Remove(item.Id,1);Vitals.Eat(item.Food);Notice="";}
            else if(item.Id==104){Data.player.equippedCoat=!Data.player.equippedCoat;Notice=Data.player.equippedCoat?"Накидка надета":"Накидка снята";}
        }
        public void Transfer(int from,int to){if(!pending&&!Vitals.dead&&!Data.campaign.runEnded)Inventory.Transfer(from,to);}
        public void Damage(float amount){Vitals.Damage(amount,Mode);}
        public void Respawn()
        {
            if(Mode==GameMode.Hardcore||Data.campaign.runEnded)return;
            Vitals.Respawn();wasDead=false;Player.TeleportSpawn();Game.Resume();Checkpoint();
        }
        void Update()
        {
            if(Data==null||!World||!Player||!World.Ready)return;
            if(!Game.Online&&Game.Paused&&!Game.InventoryOpen)return;
            Data.clock+=Time.deltaTime;tick+=Time.deltaTime;saveTime+=Time.unscaledDeltaTime;
            if(tick>=1)
            {
                float elapsed=Mathf.Min(2,tick);tick=0;
                var stove=Inventory.furnace;var stoveCell=new Vector3Int(stove.x,stove.y,stove.z);
                if(stove.realm==(int)World.Dimension&&World.GetBlock(stove.x,stove.y,stove.z)==17&&Vector3.Distance(Player.transform.position,(Vector3)stoveCell+Vector3.one*.5f)<=6.5f)
                    if(stove.Step(elapsed))Inventory.revision++;
                var eye=Vector3Int.FloorToInt(Player.Camera.transform.position);var bodyCell=Vector3Int.FloorToInt(Player.transform.position+Vector3.up*.8f);
                Vitals.StepWater(elapsed,Mode,World.GetBlock(eye.x,eye.y,eye.z)==7,World.GetBlock(bodyCell.x,bodyCell.y,bodyCell.z)==7);
                Sheltered=Physics.Raycast(Player.Camera.transform.position,Vector3.up,out var hit,16)&&hit.collider.transform.parent==World.transform;
                bool warm=World.NearCampfire(Player.transform.position);if(warm&&Sheltered)Data.player.milestones|=2;
                if(Data.player.equippedCoat&&Inventory.Count(104)==0)Data.player.equippedCoat=false;
                var sample=WeatherSample.At(Data.seed,(int)World.Dimension,Data.clock);
                Vitals.Step(elapsed,Mode,sample.Air,sample.Precipitation,Sheltered,warm,Player.Sprinting,Data.player.equippedCoat?Items.Get(104).Insulation:0);
                var feet=Vector3Int.FloorToInt(Player.transform.position+Vector3.up*.1f);
                if(World.GetBlock(feet.x,feet.y,feet.z)==8)Vitals.Damage(elapsed*24,Mode);
                if(!Game.Online)Data.campaign.milestones|=Data.player.milestones;
            }
            if(Vitals.dead&&!wasDead)
            {
                wasDead=true;
                if(Mode==GameMode.Hardcore){Data.campaign.runEnded=true;Notice="Хардкор завершён: погиб участник команды";}
                else
                {
                    // One personal recoverable grave; older grave items are forfeited on the next death.
                    Data.player.grave=Inventory.Clone().slots;Data.player.graveExtras=Inventory.ExtraStacks();Data.player.graveX=Player.transform.position.x;Data.player.graveZ=Player.transform.position.z;
                    Data.player.graveY=TerrainGenerator.Height(Mathf.FloorToInt(Data.player.graveX),Mathf.FloorToInt(Data.player.graveZ),Data.seed,World.Dimension)+1.2f;
                    Data.player.inventory=new Inventory();Notice="Вещи остались у надгробия. Можно возродиться.";UpdateGrave();
                }
                Checkpoint();Game.OpenDeathScreen();
            }
            if(saveTime>20){saveTime=0;Checkpoint();}
            if(grave&&!Vitals.dead&&Vector3.Distance(Player.transform.position,grave.transform.position)<2)
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
            if(Data==null||!World)return;
            if(Game.Online){if(!ServerRunEnded)Game.SendSave(Data.player);}
            else {Data.blocks=World.ExportEdits();LocalSave.Store(Data);}
        }
    }
}
