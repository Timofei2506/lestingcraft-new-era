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
            new GridRecipe(114,1,3,113,113,113)
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
        public static ItemStack Get(Inventory inv,int slot)=>slot<45?inv.slots[slot]:slot<54?inv.grid[slot-45]:slot==54?inv.furnace.input:slot==55?inv.furnace.fuel:inv.furnace.output;
        static void Set(Inventory inv,int slot,ItemStack s)
        {if(slot<45)inv.slots[slot]=s;else if(slot<54)inv.grid[slot-45]=s;else if(slot==54)inv.furnace.input=s;else if(slot==55)inv.furnace.fuel=s;else inv.furnace.output=s;}
        static bool Accept(int slot,int item)=>slot!=56&&(slot!=54||FurnaceState.Result(item)>0)&&(slot!=55||FurnaceState.FuelSeconds(item)>0);
        public static void Click(Inventory inv,int slot,bool right,bool shift)
        {
            if(slot<0||slot>56)return;var a=Get(inv,slot);var held=inv.cursor;
            if(shift)
            {
                if(a.count==0)return;
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
                    int amount=Math.Min(right?1:held.count,Items.Get(held.item).MaxStack-a.count);if(amount<=0)return;
                    if(a.count==0){a.item=held.item;a.durability=held.durability;}a.count+=amount;held.count-=amount;if(held.count==0)inv.cursor=new ItemStack();
                }
                else if(!right){Set(inv,slot,held);inv.cursor=a;}
            }
            inv.revision++;
        }
    }
}
