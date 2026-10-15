using System;using LestingCraft.Gameplay;
class Check
{
 static int tests;static void Assert(bool value,string name){tests++;if(!value)throw new Exception(name);}
 static ItemStack S(int i,int n=1,int d=0)=>new ItemStack{item=i,count=n,durability=d};
 static int Total(Inventory inv){int n=0;foreach(var a in inv.slots)n+=a.count;foreach(var a in inv.ExtraStacks())n+=a.count;return n;}
 static void Main()
 {
  var inv=new Inventory();inv.grid[4]=S(4,7);Assert(CraftingTable.Match(inv.grid,2).Output==120,"translated log");Assert(CraftingTable.Take(inv,2,true)==28,"shift batch");Assert(inv.Count(120)==28&&inv.grid[4].count==0,"shift conservation");
  inv=new Inventory();inv.Add(120,3);inv.Add(121,2);Assert(!CraftingTable.Fill(inv,CraftingTable.All[2],2),"2x2 rejects pick");Assert(CraftingTable.Fill(inv,CraftingTable.All[2],3),"fill pick");Assert(CraftingTable.Take(inv,3,false)==1&&inv.cursor.item==100&&inv.cursor.durability==60,"take tool");
  inv=new Inventory();inv.slots[0]=S(101,1,17);InventoryInteraction.Click(inv,0,false,false);InventoryInteraction.Click(inv,45,false,false);InventoryInteraction.Click(inv,45,false,true);Assert(inv.slots[0].durability==17,"tool wear preserved");
  inv=new Inventory();inv.Add(1,2880);inv.grid[0]=S(4);Assert(CraftingTable.Take(inv,2,true)==0&&inv.grid[0].count==1,"atomic full");Assert(!CraftingTable.ReturnGrid(inv)&&inv.grid[0].count==1,"atomic return");
  var f=new FurnaceState{input=S(110,2),fuel=S(4)};for(int i=0;i<8;i++)f.Step(1);Assert(f.output.item==112&&f.output.count==1&&f.input.count==1&&f.burn==10,"timed cooking");
  inv=new Inventory();for(int i=0;i<30;i++)inv.Add(i%2==0?1:4,12+i);var random=new Random(971);
  int total=Total(inv);
  for(int i=0;i<50000;i++)
  {InventoryInteraction.Click(inv,random.Next(54),random.Next(2)==0,random.Next(4)==0);Assert(Total(inv)==total,"random transfer conservation "+i);foreach(var a in inv.slots)Assert(a.count>=0&&a.count<=64&&(a.count>0)==(a.item>0),"valid main slot");foreach(var a in inv.grid)Assert(a.count>=0&&a.count<=64&&(a.count>0)==(a.item>0),"valid grid slot");}
  Console.WriteLine("PASS: "+tests+" assertions; 50000 randomized split/merge/swap/shift operations, recipe atomicity, tool wear and fuel/timing. Pure C# core only, not Unity scene test.");
 }
}
