using System;using LestingCraft.Gameplay;
// Step 1.1 of stage 1 (space): fuel chain core. Pure C#, no Unity. Run by core.yml.
class Check
{
 static int tests;static void Assert(bool value,string name){tests++;if(!value)throw new Exception(name);}
 static int[] Counts(Inventory inv){var c=new int[256];foreach(var s in inv.slots)if(s.count>0)c[s.item]+=s.count;return c;}
 static int Total(Inventory inv){int n=0;foreach(var s in inv.slots)n+=s.count;return n;}
 static bool Same(int[] a,int[] b){for(int i=0;i<a.Length;i++)if(a[i]!=b[i])return false;return true;}
 static void Main()
 {
  // Registry: every fuel item exists, stacks to 64, is not a block.
  for(int id=170;id<=177;id++){var d=Items.Get(id);Assert(Items.Exists(id)&&!string.IsNullOrEmpty(d.Name)&&d.MaxStack==64&&d.Block==0,"registered item "+id);}
  Assert(Items.Get(FuelChain.RocketFuel).Name=="Ракетное топливо"&&Items.Get(FuelChain.CompressedAir).Name=="Сжатый воздух","fuel item names");
  Assert(!Items.Exists(178)&&!Items.Exists(169),"no stray ids next to the fuel block");

  // Mass balance: conserving processes lose nothing, cryo declares its nitrogen loss.
  Assert(FuelChain.Distillation.InputMass==6&&FuelChain.Distillation.OutputMass==6&&FuelChain.Distillation.Loss==0,"refinery conserves mass");
  Assert(FuelChain.PropellantMix.Loss==0,"mixing conserves mass");
  Assert(FuelChain.Liquefaction.Loss==7,"cryo declares 7 kg N2/Ar loss");

  // Physical ratios (game values vs real figures).
  Assert(Math.Abs(5.0/2.0-2.56)/2.56<0.03,"O/F 2.5 within 3% of RP-1/LOX 2.56");
  Assert(Math.Abs(2.0/9.0-0.232)/0.232<0.05,"air O2 fraction within 5% of 23.2%");

  // Timing per batch.
  Assert(FuelChain.Distillation.Seconds==15f&&FuelChain.Liquefaction.Seconds==20f&&FuelChain.PropellantMix.Seconds==10f,"batch times");

  // Atomicity: short input, unchanged inventory.
  var inv=new Inventory();inv.Add(170,11);
  Assert(FuelChain.MaxBatches(inv,FuelChain.Distillation)==1,"11 crude gives one batch");
  var before=Counts(inv);
  Assert(!FuelChain.Run(inv,FuelChain.Distillation,2)&&Same(before,Counts(inv)),"over-request changes nothing");
  Assert(FuelChain.Run(inv,FuelChain.Distillation,1),"one batch runs");
  Assert(inv.Count(170)==5&&inv.Count(171)==1&&inv.Count(172)==2&&inv.Count(173)==2&&inv.Count(176)==1,"one batch outputs");
  Assert(!FuelChain.Run(inv,FuelChain.Distillation,0)&&!FuelChain.Run(null,FuelChain.Distillation,1)&&!FuelChain.Run(inv,null,1),"invalid calls rejected");

  // Full chain: 12 crude + 27 air -> 7 rocket propellant. Totals must follow the declared loss.
  inv=new Inventory();inv.Add(170,12);inv.Add(177,27);int start=Total(inv);
  Assert(FuelChain.Run(inv,FuelChain.Distillation,2)&&inv.Count(173)==4&&inv.Count(171)==2&&inv.Count(172)==4&&inv.Count(176)==2,"two refinery batches");
  Assert(FuelChain.Run(inv,FuelChain.Liquefaction,3)&&inv.Count(174)==6&&inv.Count(177)==0,"three cryo batches");
  Assert(FuelChain.Run(inv,FuelChain.PropellantMix,1)&&inv.Count(175)==7&&inv.Count(173)==2&&inv.Count(174)==1,"one propellant batch");
  Assert(Total(inv)==start-21,"chain total equals start minus declared loss (3 cryo batches x 7 kg)");
  Assert(FuelChain.MaxBatches(inv,FuelChain.PropellantMix)==0,"one LOX short of a second batch");

  // Randomized: 20000 runs on random bags. Success changes exactly the declared counts; failure changes nothing.
  var random=new Random(1701);int ok=0,failed=0;
  int[] ids={170,171,172,173,174,175,176,177,1,100};
  for(int i=0;i<20000;i++)
  {
   inv=new Inventory();
   for(int k=0;k<ids.Length;k++){int amount=random.Next(0,25);if(amount>0)inv.Add(ids[k],amount);}
   var process=FuelChain.All[random.Next(FuelChain.All.Length)];int batches=random.Next(0,5);
   var snapshot=Counts(inv);var expected=(int[])snapshot.Clone();
   bool success=FuelChain.Run(inv,process,batches);
   if(success)
   {
    ok++;
    foreach(var a in process.Inputs)expected[a.Item]-=a.Count*batches;
    foreach(var a in process.Outputs)expected[a.Item]+=a.Count*batches;
    Assert(Same(expected,Counts(inv)),"random success matches declared counts "+i);
   }
   else
   {
    failed++;
    Assert(Same(snapshot,Counts(inv)),"random failure leaves bag unchanged "+i);
   }
   foreach(var s in inv.slots)Assert(s.count>=0&&s.count<=64&&(s.count>0)==(s.item>0),"valid slot after run "+i);
  }
  Assert(ok>1000&&failed>1000,"random mix covers both outcomes");
  Console.WriteLine("PASS: "+tests+" assertions; 20000 randomized fuel runs ("+ok+" ok, "+failed+" rejected). Pure C# core only, not Unity scene test.");
 }
}
