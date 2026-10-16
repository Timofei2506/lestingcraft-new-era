extern alias previous;
using System;using System.IO;using System.Collections.Generic;using LestingCraft.Gameplay;using LestingCraft.World;
class CheckUsabilityCore
{
 static int checks;static void Check(bool b,string message){checks++;if(!b)throw new Exception(message);}
 static uint rng=7102026;static int Next(int n){rng=unchecked(rng*1664525+1013904223);return (int)(rng%(uint)n);}
 static void Main(string[] args)
 {
  var ids=CreativeCatalog.Query("",0);Check(ids.Length==98,"all 98 registered items available");var unique=new HashSet<int>(ids);Check(unique.Count==ids.Length,"no duplicate catalog entries");foreach(int id in ids){Check(Items.Exists(id),"valid catalog ID");var inv=new Inventory();Check(!CreativeCatalog.Take(inv,id,false,false,false)&&inv.cursor.count==0,"survival cannot mint supplies");Check(CreativeCatalog.Take(inv,id,false,false,true),"catalog pickup");Check(inv.cursor.count==Items.Get(id).MaxStack&&inv.cursor.durability==Items.Get(id).Durability,"stack cap and actual durability");Check(CreativeCatalog.Take(inv,id,true,true,true)&&inv.Active.count==1&&inv.Active.item==id,"single/direct hotbar selection");}
  Check(CreativeCatalog.Query("СТЕКЛО",0).Length==1&&CreativeCatalog.Query("44",0)[0]==44,"Russian case-insensitive and exact ID search");Check(CreativeCatalog.Query("несуществующее",0).Length==0,"empty search result");foreach(int id in CreativeCatalog.Query("",1))Check(Items.Get(id).Block>0,"blocks filter");foreach(int id in CreativeCatalog.Query("",4))Check(Equipment.Slot(id)>=0,"equipment filter");
  for(int job=0;job<3;job++){var offer=TradeOffer.ForJob(job);var inv=new Inventory();Check(!TradeOffer.Apply(inv,offer,out var fail)&&fail==null,"no free trades");inv.Add(offer.Input,offer.InputCount);Check(TradeOffer.Apply(inv,offer,out var updated),"valid profession offer");Check(inv.Count(offer.Input)==offer.InputCount,"transaction does not mutate original on preview");Check(updated.Count(offer.Input)==0&&updated.Count(offer.Output)==offer.OutputCount,"trade consumes exactly input and grants output");}
  var full=new Inventory();for(int i=0;i<45;i++)full.slots[i]=new ItemStack{item=1,count=64};full.slots[0]=new ItemStack{item=4,count=9};Check(!TradeOffer.Apply(full,TradeOffer.ForJob(0),out var rejected)&&full.Count(4)==9,"failed full-bag trade is atomic");full.slots[0].count=8;Check(TradeOffer.Apply(full,TradeOffer.ForJob(0),out var freed)&&freed.Count(111)==2,"consuming final stack frees output slot");
  var lines=new List<string>();var biomes=new HashSet<int>();int[] seeds={2506,1,-17,int.MinValue,int.MaxValue};
  for(int i=0;i<12000;i++)
  {
   int seed=seeds[i%seeds.Length],x=Next(4097)-2048,z=Next(4097)-2048,h=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia),y=i%3==0?h+Next(23):Next(82);
   for(int version=1;version<=6;version++)Check(TerrainGenerator.SampleContent(x,y,z,seed,Dimension.Lestingia,version)==previous::LestingCraft.World.TerrainGenerator.SampleContent(x,y,z,seed,previous::LestingCraft.World.Dimension.Lestingia,version),"legacy world including unexplored cells changed: v"+version);
   int biome=(int)BiomeGenerator.At(x,z,seed);biomes.Add(biome);int value=TerrainGenerator.SampleContent(x,y,z,seed,Dimension.Lestingia,7);lines.Add(seed+","+x+","+y+","+z+","+biome+","+value);
   Check(value>=0&&value<=48,"registered generation output");
   if(i<32){var trees=BiomeGenerator.Trees(x,z,seed,h);var column=ExpeditionGenerator.At(x,z,seed);for(int yy=0;yy<100;yy++){ushort optimized=LestingCraft.Story.StoryWorld.TrySample(x,yy,z,seed,out ushort placed)?placed:column.Apply(BiomeGenerator.Column(x,yy,z,seed,h,trees),yy,seed);Check(optimized==TerrainGenerator.SampleContent(x,yy,z,seed,Dimension.Lestingia,7),"point/chunk sampler mismatch");}}
  }
  Check(biomes.Count==6,"all six biomes discoverable");if(args.Length>0)File.WriteAllLines(args[0],lines);Console.WriteLine("PASS "+checks+" actual managed checks: all 98 catalog entries/search/filters/mode gate, atomic profession trades, 72000 legacy generation comparisons to 0.3.1, six new biomes and point/chunk parity. No native Unity or GPU claim.");
 }
}
