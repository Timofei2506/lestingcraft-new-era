using System;
using System.Collections.Generic;
using LestingCraft.Gameplay;

// Stage 1.3 harness: station recipes are unambiguous, station logic is atomic and conserves mass.
class StationsCheck
{
    static int tests;
    static void Check(bool x,string name){tests++;if(!x)throw new Exception(name);}

    // All recipes that match a 3x3 grid, at any offset and mirrored (same rule as CraftingTable.Match, but returns every match).
    static List<GridRecipe> AllMatches(ItemStack[] grid)
    {
        var found=new List<GridRecipe>();
        foreach(var recipe in CraftingTable.All)
        {
            if(!recipe.Fits(3))continue;
            bool hit=false;
            for(int y=0;y<=3-recipe.Height&&!hit;y++)for(int x=0;x<=3-recipe.Width&&!hit;x++)for(int mirror=0;mirror<2&&!hit;mirror++)
            {
                bool valid=true;
                for(int gy=0;gy<3&&valid;gy++)for(int gx=0;gx<3;gx++)
                {
                    int rx=gx-x,ry=gy-y,expected=0;
                    if(rx>=0&&rx<recipe.Width&&ry>=0&&ry<recipe.Height)expected=recipe.Pattern[ry*recipe.Width+(mirror==0?rx:recipe.Width-1-rx)];
                    var s=grid[gy*3+gx];int actual=s!=null&&s.count>0?s.item:0;
                    if(actual!=expected){valid=false;break;}
                }
                if(valid)hit=true;
            }
            if(hit)found.Add(recipe);
        }
        return found;
    }

    static ItemStack[] Grid(params int[] cells)
    {
        var grid=new ItemStack[9];
        for(int i=0;i<9;i++)grid[i]=new ItemStack{item=cells[i],count=cells[i]==0?0:1};
        return grid;
    }

    static void Main()
    {
        // Registry: stations are placeable blocks, exist as items, and mine back to themselves.
        for(int id=49;id<=53;id++)
        {
            Check(FuelStations.Has(id),"station "+id);
            Check(Items.Exists(id)&&Items.Get(id).Block==id,"placeable station item "+id);
            Check(Items.ToolDrop(id)==id,"station drops itself "+id);
            Check(Array.IndexOf(Items.CatalogIds,id)>=0,"creative catalog lists "+id);
        }
        Check(!FuelStations.Has(48)&&!FuelStations.Has(54),"station range is exactly 49..53");
        for(int id=170;id<=177;id++)Check(Array.IndexOf(Items.CatalogIds,id)>=0,"fuel item in creative catalog "+id);
        Check(ExpandedContent.Blocks.Length==35&&ExpandedContent.MaxBlock==53,"35 expanded blocks, MaxBlock 53");

        // Recipes: each station grid matches exactly its own recipe at any offset and mirror.
        int[][] shapes=
        {
            new[]{140,121,140, 140,123,140, 140,121,140},
            new[]{140,140,140, 140,17,140, 140,140,140},
            new[]{11,140,11, 140,0,140, 11,140,11},
            new[]{140,0,140, 140,123,140, 140,0,140},
            new[]{123,140,123, 0,121,0, 123,140,123},
        };
        for(int k=0;k<5;k++)
        {
            int station=49+k;
            var hits=AllMatches(Grid(shapes[k]));
            Check(hits.Count==1&&hits[0].Output==station,"unambiguous recipe for station "+station+" (matches: "+hits.Count+")");
            Check(CraftingTable.Match(Grid(shapes[k]),3)?.Output==station,"Match() returns station "+station);
            Check(CraftingTable.RoomVersionFor(station)==7,"room version 7 for station "+station);
            // The same shape must not be made from the 2x2 window of the 3x3 grid either.
            Check(CraftingTable.Match(Grid(shapes[k]),2)==null||CraftingTable.Match(Grid(shapes[k]),2).Output==station,"2x2 craft cannot steal station "+station);
        }
        Check(CraftingTable.RoomVersionFor(48)==5&&CraftingTable.RoomVersionFor(149)==5&&CraftingTable.RoomVersionFor(1)==0,"old room gates unchanged");

        // Oil well: one portion per press, 6 kg, atomic when the bag is full.
        var inv=new Inventory();
        Check(FuelStations.Use(inv,FuelStations.OilWell)=="Скважина: +6 сырой нефти","well message");
        Check(inv.Count(FuelChain.CrudeOil)==6,"one press gives 6 kg crude");
        FuelStations.Use(inv,FuelStations.OilWell);
        Check(inv.Count(FuelChain.CrudeOil)==12,"second press adds another portion");

        // Compressor: 9 kg air per press.
        inv=new Inventory();
        Check(FuelStations.Use(inv,FuelStations.Compressor)=="Компрессор: +9 сжатого воздуха","compressor message");
        Check(inv.Count(FuelChain.CompressedAir)==9,"one press gives 9 kg air");

        // Full bag: no partial portion.
        inv=new Inventory();
        for(int i=0;i<inv.slots.Length;i++)inv.slots[i]=new ItemStack{item=1,count=64,durability=0};
        string full=FuelStations.Use(inv,FuelStations.OilWell);
        Check(full=="Нет места в инвентаре"&&inv.Count(FuelChain.CrudeOil)==0,"full bag gets no crude and no partial portion");

        // Refinery: no crude -> notice, nothing changes.
        inv=new Inventory();
        Check(FuelStations.Use(inv,FuelStations.Refinery)=="Перегонка: не хватает сырья для одной партии","refinery needs crude");
        Check(inv.Count(FuelChain.Kerosene)==0,"refinery no-op without crude");

        // Refinery: 13 kg crude -> 2 whole batches (12 kg), 1 kg stays. Products 1+2+2+1 per batch.
        inv=new Inventory();Check(inv.Add(FuelChain.CrudeOil,13),"seed crude");
        Check(FuelStations.Use(inv,FuelStations.Refinery)=="Перегонка: партий 2","refinery runs max batches");
        Check(inv.Count(FuelChain.CrudeOil)==1&&inv.Count(FuelChain.Gasoline)==2&&inv.Count(FuelChain.Diesel)==4&&inv.Count(FuelChain.Kerosene)==4&&inv.Count(FuelChain.Residue)==2,"refinery products 2 batches");
        int massOut=inv.Count(FuelChain.Gasoline)+inv.Count(FuelChain.Diesel)+inv.Count(FuelChain.Kerosene)+inv.Count(FuelChain.Residue)+inv.Count(FuelChain.CrudeOil);
        Check(massOut==13,"refinery conserves mass (13 kg in, 13 kg in products + leftover)");

        // Cryo unit: 20 kg air -> 2 whole batches of 9 kg, 2 kg air left, 4 kg LOX.
        inv=new Inventory();Check(inv.Add(FuelChain.CompressedAir,20),"seed air");
        Check(FuelStations.Use(inv,FuelStations.Cryo)=="Криоконденсация: партий 2","cryo runs max batches");
        Check(inv.Count(FuelChain.LiquidOxygen)==4&&inv.Count(FuelChain.CompressedAir)==2,"cryo batches");

        // Mixer: 5 kerosene, 12 LOX -> min(2, 2) = 2 batches of (2 kerosene + 5 LOX) -> 14 rocket fuel.
        inv=new Inventory();Check(inv.Add(FuelChain.Kerosene,5)&&inv.Add(FuelChain.LiquidOxygen,12),"seed mixer");
        Check(FuelStations.Use(inv,FuelStations.Mixer)=="Смешение: партий 2","mixer runs max batches");
        Check(inv.Count(FuelChain.RocketFuel)==14&&inv.Count(FuelChain.Kerosene)==1&&inv.Count(FuelChain.LiquidOxygen)==2,"mixer products");

        // Mixer short of LOX: notice only.
        inv=new Inventory();Check(inv.Add(FuelChain.Kerosene,4)&&inv.Add(FuelChain.LiquidOxygen,4),"short mixer");
        Check(FuelStations.Use(inv,FuelStations.Mixer)=="Смешение: не хватает сырья для одной партии","mixer short");
        Check(inv.Count(FuelChain.RocketFuel)==0&&inv.Count(FuelChain.Kerosene)==4,"mixer no-op when short");

        // Non-station ids do nothing.
        Check(FuelStations.Use(new Inventory(),1)=="","non-station id is a no-op");

        Console.WriteLine("PASS: "+tests+" station checks (recipes unambiguous, stations atomic, mass conserved)");
    }
}
