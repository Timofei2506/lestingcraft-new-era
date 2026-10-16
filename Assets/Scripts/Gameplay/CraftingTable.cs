using System;
namespace LestingCraft.Gameplay
{
    public sealed class GridRecipe
    {
        public readonly int Output,Count,Width,Height;public readonly int[] Pattern;
        public GridRecipe(int output,int count,int width,params int[] pattern)
        {Output=output;Count=count;Width=width;Height=pattern.Length/width;Pattern=pattern;}
        public bool Fits(int size)=>Width<=size&&Height<=size;
    }
    /// <summary>Shaped, translated and horizontally mirrored recipes. Transactions never delete items on failure.</summary>
    public static class CraftingTable
    {
        public static readonly GridRecipe[] All={
            new GridRecipe(120,4,1,4),new GridRecipe(121,4,1,120,120),
            new GridRecipe(100,1,3,120,120,120,0,121,0,0,121,0),
            new GridRecipe(101,1,3,1,1,1,0,121,0,0,121,0),
            new GridRecipe(102,1,2,1,1,1,121,0,121),new GridRecipe(103,1,1,1,1,121),
            new GridRecipe(15,1,3,0,121,0,121,1,121,4,4,4),
            new GridRecipe(104,1,3,122,0,122,122,120,122,122,122,122),
            new GridRecipe(16,1,2,120,120,120,120),new GridRecipe(17,1,3,1,1,1,1,0,1,1,1,1),
            new GridRecipe(114,1,3,113,113,113),
            new GridRecipe(130,1,3,122,122,122,122,0,122),new GridRecipe(131,1,3,122,122,122,122,0,122,122,0,122),new GridRecipe(132,1,3,122,0,122,122,0,122),
            new GridRecipe(105,1,3,13,13,13,13,0,13),new GridRecipe(106,1,3,13,0,13,13,13,13,13,13,13),new GridRecipe(107,1,3,13,13,13,13,0,13,13,0,13),new GridRecipe(108,1,3,13,0,13,13,0,13),
new GridRecipe(36,1,1,1),
new GridRecipe(37,4,2,36,36,36,36),
new GridRecipe(38,1,1,37),
new GridRecipe(35,1,2,1,122),
new GridRecipe(40,4,2,1,1,1,1),
new GridRecipe(41,4,2,27,27,27,27),
new GridRecipe(42,4,2,30,30,30,30),
new GridRecipe(43,4,2,6,6,6,6),
new GridRecipe(45,1,3,147,147,147,147,147,147,147,147,147),
new GridRecipe(147,9,1,45),
new GridRecipe(46,1,3,141,141,141,141,141,141,141,141,141),
new GridRecipe(141,9,1,46),
new GridRecipe(139,2,2,136,4,136,4),
new GridRecipe(132,1,3,139,0,139,139,0,139),
            new GridRecipe(105,1,3,140,140,140,140,0,140),new GridRecipe(106,1,3,140,0,140,140,140,140,140,140,140),new GridRecipe(107,1,3,140,140,140,140,0,140,140,0,140),new GridRecipe(108,1,3,140,0,140,140,0,140),
            new GridRecipe(11,1,3,0,145,0,145,143,145,0,145,0),new GridRecipe(15,1,3,0,146,0,147,4,147,4,4,4),new GridRecipe(148,4,1,123,121),
new GridRecipe(150,1,3,140,140,140,0,121,0,0,121,0),new GridRecipe(151,1,3,149,149,149,0,121,0,0,121,0),new GridRecipe(48,1,3,149,149,149,149,149,149,149,149,149),new GridRecipe(149,9,1,48),new GridRecipe(164,1,3,0,121,122,121,0,122,0,121,122),new GridRecipe(165,4,1,1,121,122),new GridRecipe(152,1,1,140,140,121),new GridRecipe(153,1,1,149,149,121),new GridRecipe(154,1,2,140,0,121,122),new GridRecipe(155,1,2,149,0,121,122),new GridRecipe(156,1,3,0,0,140,0,121,0,121,0,0),new GridRecipe(157,1,3,0,0,149,0,121,0,121,0,0),new GridRecipe(158,1,2,140,140,140,121,0,121),new GridRecipe(159,1,2,149,149,149,121,0,121),new GridRecipe(160,1,3,140,140,140,140,121,140,0,121,0),new GridRecipe(161,1,3,149,149,149,149,121,149,0,121,0),new GridRecipe(162,1,3,0,140,0,140,140,140,0,121,0),new GridRecipe(163,1,3,0,149,0,149,149,149,0,121,0)
        };
        public static GridRecipe Match(ItemStack[] grid,int size)
        {
            if(grid==null||grid.Length!=9||(size!=2&&size!=3))return null;
            foreach(var recipe in All)if(recipe.Fits(size))for(int y=0;y<=size-recipe.Height;y++)for(int x=0;x<=size-recipe.Width;x++)for(int mirror=0;mirror<2;mirror++)
            {
                bool valid=true;
                for(int gy=0;gy<3&&valid;gy++)for(int gx=0;gx<3;gx++)
                {
                    int rx=gx-x,ry=gy-y,expected=0;
                    if(rx>=0&&rx<recipe.Width&&ry>=0&&ry<recipe.Height)expected=recipe.Pattern[ry*recipe.Width+(mirror==0?rx:recipe.Width-1-rx)];
                    var s=grid[gy*3+gx];int actual=s!=null&&s.count>0?s.item:0;
                    if(actual!=expected){valid=false;break;}
                }
                if(valid)return recipe;
            }
            return null;
        }
        public static bool ReturnGrid(Inventory inventory)
        {
            var copy=inventory.Clone();foreach(var s in copy.grid)if(!copy.AddStack(s))return false;
            for(int i=0;i<9;i++)copy.grid[i]=new ItemStack();Commit(inventory,copy);return true;
        }
        public static bool Fill(Inventory inventory,GridRecipe recipe,int size)
        {
            if(!recipe.Fits(size))return false;var copy=inventory.Clone();if(!ReturnGrid(copy))return false;
            for(int y=0;y<recipe.Height;y++)for(int x=0;x<recipe.Width;x++)
            {int id=recipe.Pattern[y*recipe.Width+x];if(id==0)continue;if(!copy.Remove(id,1))return false;copy.grid[y*3+x]=new ItemStack{item=id,count=1,durability=Items.Get(id).Durability};}
            Commit(inventory,copy);return true;
        }
        public static int Take(Inventory inventory,int size,bool toInventory)
        {
            int made=0;
            for(int attempt=0;attempt<(toInventory?64:1);attempt++)
            {
                var recipe=Match(inventory.grid,size);if(recipe==null)break;var copy=inventory.Clone();
                if(toInventory){if(!copy.Add(recipe.Output,recipe.Count))break;}
                else
                {
                    var held=copy.cursor;int max=Items.Get(recipe.Output).MaxStack;
                    if(held.count>0&&(held.item!=recipe.Output||held.count+recipe.Count>max))break;
                    if(held.count==0){held.item=recipe.Output;held.durability=Items.Get(recipe.Output).Durability;}held.count+=recipe.Count;
                }
                foreach(var s in copy.grid)if(s.count>0){s.count--;if(s.count==0){s.item=0;s.durability=0;}}
                Commit(inventory,copy);made+=recipe.Count;
            }
            return made;
        }
        static void Commit(Inventory original,Inventory copy)
        {original.slots=copy.slots;original.grid=copy.grid;original.cursor=copy.cursor;original.furnace=copy.furnace;original.revision++;}
    }
    public static class InventoryInteraction
    {
        // 0..44 bag; 45..53 crafting; 54 input, 55 fuel, 56 output.
        public static ItemStack Get(Inventory inv,int slot)=>slot==66?inv.offhand:slot>=58&&slot<66?inv.equipment[slot-58]:slot<45?inv.slots[slot]:slot<54?inv.grid[slot-45]:slot==54?inv.furnace.input:slot==55?inv.furnace.fuel:inv.furnace.output;
        static void Set(Inventory inv,int slot,ItemStack s)
        {if(slot==66)inv.offhand=s;else if(slot>=58&&slot<66)inv.equipment[slot-58]=s;else if(slot<45)inv.slots[slot]=s;else if(slot<54)inv.grid[slot-45]=s;else if(slot==54)inv.furnace.input=s;else if(slot==55)inv.furnace.fuel=s;else inv.furnace.output=s;}
        static bool Accept(int slot,int item)=>slot==66?item==Items.Torch:slot>=58?Equipment.Accept(slot-58,item):slot!=56&&(slot!=54||FurnaceState.Result(item)>0)&&(slot!=55||FurnaceState.FuelSeconds(item)>0);
        public static void Click(Inventory inv,int slot,bool right,bool shift)
        {
            if(slot<0||slot==57||slot>66)return;var a=Get(inv,slot);var held=inv.cursor;
            if(shift)
            {
                if(a.count==0)return;
                if(slot<45&&Equipment.Slot(a.item)>=0){Equipment.Equip(inv,slot);return;}
                if(slot>=45){if(inv.AddStack(a)){Set(inv,slot,new ItemStack());inv.revision++;}return;}
                int first=slot<9?9:0,last=slot<9?45:9;
                for(int pass=0;pass<2;pass++)for(int i=first;i<last&&a.count>0;i++)
                {
                    var target=inv.slots[i];if(pass==0&&target.item!=a.item||pass==1&&target.count!=0)continue;
                    int amount=Math.Min(a.count,Items.Get(a.item).MaxStack-target.count);if(amount<=0)continue;
                    if(target.count==0){target.item=a.item;target.durability=a.durability;}target.count+=amount;a.count-=amount;
                }
                if(a.count==0){a.item=0;a.durability=0;}inv.revision++;return;
            }
            if(held.count==0)
            {
                if(a.count==0)return;int amount=right?(a.count+1)/2:a.count;
                inv.cursor=new ItemStack{item=a.item,count=amount,durability=a.durability};a.count-=amount;
                if(a.count==0)Set(inv,slot,new ItemStack());
            }
            else if(slot==56)
            {
                if(a.item!=held.item||a.count==0)return;int amount=Math.Min(a.count,Items.Get(held.item).MaxStack-held.count);held.count+=amount;a.count-=amount;
                if(a.count==0)Set(inv,slot,new ItemStack());
            }
            else
            {
                if(!Accept(slot,held.item))return;
                if(a.count==0||a.item==held.item&&Items.Get(a.item).MaxStack>1)
                {
                    int amount=Math.Min(right?1:held.count,(slot==66?1:Items.Get(held.item).MaxStack)-a.count);if(amount<=0)return;
                    if(a.count==0){a.item=held.item;a.durability=held.durability;}a.count+=amount;held.count-=amount;if(held.count==0)inv.cursor=new ItemStack();
                }
                else if(!right){Set(inv,slot,held);inv.cursor=a;}
            }
            inv.revision++;
        }
    }
}
