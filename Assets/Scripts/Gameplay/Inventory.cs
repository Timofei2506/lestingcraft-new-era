using System;
using System.Collections.Generic;
namespace LestingCraft.Gameplay
{
    [Serializable] public sealed class ItemStack
    {
        public int item,count,durability;
        public ItemStack Clone()=>new ItemStack{item=item,count=count,durability=durability};
    }
    public readonly struct ItemDefinition
    {
        public readonly int Id,MaxStack,Durability;public readonly string Name;public readonly ushort Block;public readonly float Food,Insulation;
        public ItemDefinition(int id,string name,int max=64,int durability=0,ushort block=0,float food=0,float insulation=0)
        {Id=id;Name=name;MaxStack=max;Durability=durability;Block=block;Food=food;Insulation=insulation;}
    }
    public static class Items
    {
        public const int Torch=148,WoodPick=100,StonePick=101,Axe=102,Sword=103,Coat=104,Berries=110,Bread=111,Planks=120,Stick=121,Fiber=122,Charcoal=123,RoastedBerries=112,Grain=113,Dough=114,Workbench=16,Furnace=17;
        public const int UraniumRod=190,CherenkovCore=191,ElectricLocomotive=192,QuadDrone=193,IndustrialRobot=194,Dosimeter=195,ThermonukeId=666;
        static readonly Dictionary<int,ItemDefinition> all=new Dictionary<int,ItemDefinition>();
        static Items()
        {
            string[] names={"","Камень","Земля","Дёрн","Древесина","Листва","Песок","Вода","Лава","Коренная порода","Базальт","Кристалл","Эфирный камень","Руда","Снег","Костёр"};
            for(ushort i=1;i<=15;i++)all[i]=new ItemDefinition(i,names[i],block:i);
            all[149]=new ItemDefinition(149,"Алмаз");all[150]=new ItemDefinition(150,"Железная кирка",1,450);all[151]=new ItemDefinition(151,"Алмазная кирка",1,1500);all[165]=new ItemDefinition(165,"Стрелы");foreach(var w in Weapons.All)all[w.Id]=new ItemDefinition(w.Id,w.Name,1,w.Durability);
            all[Torch]=new ItemDefinition(Torch,"Факел");
            all[WoodPick]=new ItemDefinition(WoodPick,"Деревянная кирка",1,60);
            all[StonePick]=new ItemDefinition(StonePick,"Каменная кирка",1,140);
            all[Axe]=new ItemDefinition(Axe,"Каменный топор",1,120);
            all[Sword]=new ItemDefinition(Sword,"Каменный меч",1,160);
            all[Coat]=new ItemDefinition(Coat,"Тёплая накидка",1,0,insulation:10);
            all[105]=new ItemDefinition(105,"Рудный шлем",1,180);all[106]=new ItemDefinition(106,"Рудный нагрудник",1,260);all[107]=new ItemDefinition(107,"Рудные поножи",1,220);all[108]=new ItemDefinition(108,"Рудные сапоги",1,180);
            all[130]=new ItemDefinition(130,"Тканевый капюшон",1,insulation:3);all[131]=new ItemDefinition(131,"Походные штаны",1,insulation:4);all[132]=new ItemDefinition(132,"Кожаные ботинки",1,insulation:3);
            all[Berries]=new ItemDefinition(Berries,"Лесные ягоды",32,food:16);
            all[Bread]=new ItemDefinition(Bread,"Походный хлеб",32,food:30);
            all[Workbench]=new ItemDefinition(Workbench,"Верстак",block:16);all[Furnace]=new ItemDefinition(Furnace,"Печь",block:17);
            all[RoastedBerries]=new ItemDefinition(RoastedBerries,"Печёные ягоды",32,food:24);all[Grain]=new ItemDefinition(Grain,"Дикое зерно");all[Dough]=new ItemDefinition(Dough,"Тесто");all[Charcoal]=new ItemDefinition(Charcoal,"Древесный уголь");
            foreach(var b in ExpandedContent.Blocks)all[b.Id]=new ItemDefinition(b.Id,b.Name,block:(ushort)b.Id);
            all[135]=new ItemDefinition(135,"Сырое мясо",32,food:4);all[136]=new ItemDefinition(136,"Сырая шкура");all[138]=new ItemDefinition(138,"Жареное мясо",32,food:28);all[139]=new ItemDefinition(139,"Кожа");
            string[] minerals={"Железный слиток","Медный слиток","Золотой слиток","Серебряный слиток","Оловянный слиток","Кварц","Сера","Уголь"};for(int i=0;i<8;i++)all[140+i]=new ItemDefinition(140+i,minerals[i]);
            all[Planks]=new ItemDefinition(Planks,"Доски",block:18);all[Stick]=new ItemDefinition(Stick,"Палки");all[Fiber]=new ItemDefinition(Fiber,"Волокно");
            all[FuelChain.CrudeOil]=new ItemDefinition(FuelChain.CrudeOil,"Сырая нефть");all[FuelChain.Gasoline]=new ItemDefinition(FuelChain.Gasoline,"Бензин");all[FuelChain.Diesel]=new ItemDefinition(FuelChain.Diesel,"Дизель");all[FuelChain.Kerosene]=new ItemDefinition(FuelChain.Kerosene,"Керосин");
            all[FuelChain.LiquidOxygen]=new ItemDefinition(FuelChain.LiquidOxygen,"Жидкий кислород");all[FuelChain.RocketFuel]=new ItemDefinition(FuelChain.RocketFuel,"Ракетное топливо");all[FuelChain.Residue]=new ItemDefinition(FuelChain.Residue,"Гудрон");all[FuelChain.CompressedAir]=new ItemDefinition(FuelChain.CompressedAir,"Сжатый воздух");
            all[UraniumRod]=new ItemDefinition(UraniumRod,"Урановый стержень ТВЭЛ");
            all[CherenkovCore]=new ItemDefinition(CherenkovCore,"Ядро Черенкова");
            all[ElectricLocomotive]=new ItemDefinition(ElectricLocomotive,"Электровоз «ЭР-Воксель»",1);
            all[QuadDrone]=new ItemDefinition(QuadDrone,"Инженерный квадрокоптер",1);
            all[IndustrialRobot]=new ItemDefinition(IndustrialRobot,"Робот-автоматон «Титан-М»",1);
            all[Dosimeter]=new ItemDefinition(Dosimeter,"Дозиметр «Припять»",1);
            // STRICTLY SECRET WARHEAD: never exposed in CatalogIds, CreativeCatalog, recipes, drops or trades — ONLY /give 666!
            all[ThermonukeId]=new ItemDefinition(ThermonukeId,"Объект 666 · Термоядерная боеголовка",1);
        }
        // Public catalog must equal ItemArtwork.AllIds and strictly exclude secret ThermonukeId (666).
        public static int[] CatalogIds{get{var ids=new List<int>(all.Count);foreach(int k in all.Keys)if(k!=ThermonukeId)ids.Add(k);ids.Sort();return ids.ToArray();}}
        public static bool Exists(int id)=>all.ContainsKey(id);
        public static ItemDefinition Get(int id)=>all.TryGetValue(id,out var item)?item:default;
        public static bool IsPick(int id)=>id==WoodPick||id==StonePick||id==150||id==151;
        public static int ToolDrop(int block)=>block==47?149:block==3?2:block==5?Fiber:block==18?Planks:block==19?147:block;
    }
    [Serializable] public sealed class Inventory
    {
        public const int SlotCount=45,HotbarCount=9;
        public ItemStack offhand=new ItemStack();
        public ItemStack[] equipment=new ItemStack[Equipment.Count];
        public ItemStack[] grid=new ItemStack[9];public ItemStack cursor=new ItemStack();public FurnaceState furnace=new FurnaceState();
        public ItemStack[] slots=new ItemStack[SlotCount];public int selected;public int revision;
        public Inventory(){for(int i=0;i<equipment.Length;i++)equipment[i]=new ItemStack();for(int i=0;i<slots.Length;i++)slots[i]=new ItemStack();for(int i=0;i<9;i++)grid[i]=new ItemStack();}
        static void EnsureStackArray(ref ItemStack[] stacks,int length)
        {
            if(stacks==null)stacks=new ItemStack[length];
            else if(stacks.Length!=length)Array.Resize(ref stacks,length);
            for(int i=0;i<stacks.Length;i++)if(stacks[i]==null)stacks[i]=new ItemStack();
        }
        public ItemStack Active{get{NormalizeNulls();int idx=Math.Max(0,Math.Min(HotbarCount-1,selected));return slots[idx];}}
        public void NormalizeNulls()
        {
            if(offhand==null)offhand=new ItemStack();
            if(cursor==null)cursor=new ItemStack();
            if(furnace==null)furnace=new FurnaceState();
            if(furnace.input==null)furnace.input=new ItemStack();
            if(furnace.fuel==null)furnace.fuel=new ItemStack();
            if(furnace.output==null)furnace.output=new ItemStack();
            EnsureStackArray(ref equipment,Equipment.Count);
            EnsureStackArray(ref grid,9);
            EnsureStackArray(ref slots,SlotCount);
        }
        public int Count(int item){NormalizeNulls();int n=0;foreach(var s in slots)if(s.item==item)n+=s.count;return n;}
        public bool CanAdd(int item,int count)
        {
            NormalizeNulls();
            if(!Items.Exists(item)||count<=0)return false;int room=0,max=Items.Get(item).MaxStack;
            foreach(var s in slots)if(s.count==0)room+=max;else if(s.item==item)room+=max-s.count;
            return room>=count;
        }
        public bool Add(int item,int count)
        {
            if(!CanAdd(item,count))return false;int max=Items.Get(item).MaxStack;
            foreach(var s in slots)if(s.item==item&&s.count>0){int take=Math.Min(max-s.count,count);s.count+=take;count-=take;if(count==0){revision++;return true;}}
            foreach(var s in slots)if(s.count==0){int take=Math.Min(max,count);s.item=item;s.count=take;s.durability=Items.Get(item).Durability;count-=take;if(count==0)break;}
            revision++;return true;
        }
        public bool Remove(int item,int count)
        {
            NormalizeNulls();
            if(count<=0||Count(item)<count)return false;
            foreach(var s in slots)if(s.item==item){int take=Math.Min(count,s.count);s.count-=take;count-=take;if(s.count==0){s.item=0;s.durability=0;}if(count==0)break;}
            revision++;return true;
        }
        public void Transfer(int from,int to)
        {
            NormalizeNulls();
            if(from<0||to<0||from>=SlotCount||to>=SlotCount||from==to)return;
            var a=slots[from];var b=slots[to];
            if(a.count>0&&a.item==b.item&&Items.Get(a.item).MaxStack>1)
            {int amount=Math.Min(a.count,Items.Get(a.item).MaxStack-b.count);a.count-=amount;b.count+=amount;if(a.count==0){a.item=0;a.durability=0;}}
            else {slots[from]=b;slots[to]=a;}revision++;
        }
        public Inventory Clone(){NormalizeNulls();var n=new Inventory();n.offhand=offhand.Clone();for(int i=0;i<SlotCount;i++)n.slots[i]=slots[i].Clone();for(int i=0;i<9;i++)n.grid[i]=grid[i].Clone();for(int i=0;i<Equipment.Count;i++)n.equipment[i]=equipment[i].Clone();n.cursor=cursor.Clone();n.furnace=furnace.Clone();n.selected=selected;n.revision=revision;return n;}
        public static ItemStack Clean(ItemStack s)
        {
            if(s==null||!Items.Exists(s.item)||s.count<=0)return new ItemStack();
            s.count=Math.Min(s.count,Items.Get(s.item).MaxStack);s.durability=Math.Max(0,Math.Min(Items.Get(s.item).Durability,s.durability));return s;
        }
        public bool AddStack(ItemStack stack)
        {
            NormalizeNulls();
            if(stack==null||stack.count<=0)return true;if(!CanAdd(stack.item,stack.count))return false;
            // Non-stackable equipment must retain its actual wear, not be repaired by transfer/save.
            if(Items.Get(stack.item).MaxStack==1){for(int n=0;n<stack.count;n++){int free=Array.FindIndex(slots,s=>s.count==0);slots[free]=new ItemStack{item=stack.item,count=1,durability=stack.durability};}revision++;return true;}
            return Add(stack.item,stack.count);
        }
        public bool ToggleOffhand()
        {
            NormalizeNulls();
            if(offhand.count>0){if(!AddStack(offhand))return false;offhand=new ItemStack();revision++;return true;}
            if((Active.item!=Items.Torch&&Active.item!=54)||Active.count<1)return false;
            offhand=new ItemStack{item=Active.item,count=1};Active.count--;if(Active.count==0){Active.item=0;Active.durability=0;}revision++;return true;
        }
        public int RecoverStacks(ItemStack[] remaining)
        {
            int recovered=0;if(remaining==null)return 0;
            foreach(var stack in remaining)if(stack!=null&&stack.count>0&&AddStack(stack))
            {recovered+=stack.count;stack.count=0;stack.item=0;stack.durability=0;}
            return recovered;
        }
        public ItemStack[] ExtraStacks()
        {NormalizeNulls();var all=new ItemStack[13+Equipment.Count+(offhand.count>0?1:0)];if(offhand.count>0)all[21]=offhand.Clone();for(int i=0;i<Equipment.Count;i++)all[13+i]=equipment[i].Clone();for(int i=0;i<9;i++)all[i]=grid[i].Clone();all[9]=cursor.Clone();all[10]=furnace.input.Clone();all[11]=furnace.fuel.Clone();all[12]=furnace.output.Clone();return all;}
        public void WearTool()
        {if(Active.count>0&&Items.Get(Active.item).Durability>0){Active.durability--;if(Active.durability<=0){Active.count=0;Active.item=0;Active.durability=0;}revision++;}}
        public void Normalize()
        {
            NormalizeNulls();
            for(int i=0;i<SlotCount;i++)
            {if(slots[i]==null)slots[i]=new ItemStack();var s=slots[i];if(!Items.Exists(s.item)||s.count<=0){s.item=0;s.count=0;s.durability=0;}else{s.count=Math.Min(s.count,Items.Get(s.item).MaxStack);s.durability=Math.Max(0,Math.Min(Items.Get(s.item).Durability,s.durability));}}
            for(int i=0;i<Equipment.Count;i++){equipment[i]=Clean(equipment[i]);if(equipment[i].count>0&&!Equipment.Accept(i,equipment[i].item)){if(!AddStack(equipment[i]))throw new InvalidOperationException("Неверный слот экипировки: нет места для восстановления предмета");equipment[i]=new ItemStack();}}
            for(int i=0;i<9;i++)grid[i]=Clean(grid[i]);cursor=Clean(cursor);furnace.Normalize();
            offhand=Clean(offhand);if(offhand.count>0&&((offhand.item!=Items.Torch&&offhand.item!=54)||offhand.count!=1)){if(!AddStack(offhand))throw new InvalidOperationException("Неверная левая рука: нет места для восстановления");offhand=new ItemStack();}
            selected=Math.Max(0,Math.Min(8,selected));
        }
    }
    public readonly struct Ingredient {public readonly int item,count;public Ingredient(int id,int n){item=id;count=n;}}
    public sealed class Recipe
    {
        public readonly string Name;public readonly int output,count;public readonly Ingredient[] inputs;
        public Recipe(string name,int output,int count,params Ingredient[] inputs){Name=name;this.output=output;this.count=count;this.inputs=inputs;}
        public bool Craft(Inventory inventory)
        {
            var copy=inventory.Clone();foreach(var cost in inputs)if(!copy.Remove(cost.item,cost.count))return false;
            if(!copy.Add(output,count))return false;inventory.slots=copy.slots;inventory.revision++;return true;
        }
    }
    public static class Recipes
    {
        public static readonly Recipe[] All=Build();
        static Recipe[] Build()
        {
            var recipes=new Recipe[CraftingTable.All.Length];
            for(int i=0;i<recipes.Length;i++)
            {
                var r=CraftingTable.All[i];var counts=new Dictionary<int,int>();foreach(int item in r.Pattern)if(item>0){if(!counts.ContainsKey(item))counts[item]=0;counts[item]++;}
                var costs=new List<Ingredient>();foreach(var pair in counts)costs.Add(new Ingredient(pair.Key,pair.Value));
                recipes[i]=new Recipe(Items.Get(r.Output).Name+" ×"+r.Count,r.Output,r.Count,costs.ToArray());
            }
            return recipes;
        }
    }
}
