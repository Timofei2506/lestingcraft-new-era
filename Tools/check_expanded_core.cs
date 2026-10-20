using System;
using System.Collections.Generic;
using LestingCraft.Gameplay;
#if TERRAIN_REFERENCE_CHECK
using LestingCraft.World;
#endif
class ExpandedCheck
{
 static int tests;static void Check(bool x,string name){tests++;if(!x)throw new Exception(name);}
 static float Flat(float x,float z,float y,float r,float h)=>50;
 static AnimalObserver Player(float x=0,float z=0)=>new AnimalObserver{id="p",x=x,y=51.62f,z=z,alive=true};
 static AnimalState Animal(int species)=>new AnimalState{id="a:0:0",species=species,health=WildlifeSimulation.MaxHealth(species),x=0,y=50,z=0,homeX=0,homeZ=0};
 static WildlifeSimulation Sim(AnimalState a)=>new WildlifeSimulation(2506,new WildlifeSave{animals=new[]{a},sites=new[]{"0:0"}});
 static void Main(string[] args)
 {
#if TERRAIN_REFERENCE_CHECK
  if(args.Length>0&&args[0]=="--terrain-vectors")
  {var random=new Random(2591);for(int i=0;i<300;i++){int x=random.Next(-64,64),z=random.Next(-64,64),seed=i%3==0?2506:i%3==1?-1971:int.MaxValue;Console.WriteLine(x+","+z+","+seed+","+TerrainGenerator.Height(x,z,seed,Dimension.Lestingia)+","+TerrainGenerator.Sample(x,60,z,seed,Dimension.Lestingia));}return;}
#endif
  Check(ExpandedContent.Blocks.Length==51,"exactly 51 distinct block definitions, including diamonds, stations 49..53, and Era expansion 54..69");
  var layers=new HashSet<int>();foreach(var b in ExpandedContent.Blocks){Check(Items.Exists(b.Id)&&Items.Get(b.Id).Block==b.Id,"placeable item "+b.Id);Check(b.Hardness>0&&b.Layer>0,"hardness / explicit layer");layers.Add(b.Layer);}
  Check(layers.Count>=27,"distinct appearance for every new material except clear glass");
  Check(Items.ToolDrop(19)==147&&FurnaceState.FuelSeconds(147)==40,"coal mining and fuel");
  for(int id=20;id<=26;id++){Check(FurnaceState.Result(id)==140+id-20,"ore metallurgy "+id);var furnace=new FurnaceState{input=new ItemStack{item=id,count=1},fuel=new ItemStack{item=147,count=1}};for(int t=0;t<8;t++)furnace.Step(1);Check(furnace.output.item==140+id-20&&furnace.input.count==0,"timed ore output");}
  Check(FurnaceState.Result(6)==44&&FurnaceState.Result(33)==39&&FurnaceState.Result(135)==138,"glass / bricks / meat");
  var bag=new Inventory();var overflowBag=new Inventory();for(int i=0;i<ExpandedContent.Blocks.Length;i++){var b=ExpandedContent.Blocks[i];var target=i<Inventory.SlotCount?bag:overflowBag;Check(target.Add(b.Id,64),"expanded creative inventory fits "+b.Id);}bag.Normalize();overflowBag.Normalize();for(int i=0;i<ExpandedContent.Blocks.Length;i++){var b=ExpandedContent.Blocks[i];var target=i<Inventory.SlotCount?bag:overflowBag;Check(target.Count(b.Id)==64,"normalization preserves new IDs "+b.Id);}
  foreach(var recipe in CraftingTable.All){Check(Items.Exists(recipe.Output),"known recipe output");foreach(int i in recipe.Pattern)Check(i==0||Items.Exists(i),"known recipe input");}
  var spawn=new WildlifeSimulation(2506);spawn.Spawn(Player(),Flat);int initial=spawn.Animals.Count;Check(initial>0,"actual animal spawning");spawn.Spawn(Player(),Flat);Check(spawn.Animals.Count==initial,"no duplicate spawns");var kinds=new HashSet<int>();foreach(var a in spawn.Animals){kinds.Add(a.species);Check(a.y==50&&a.health>0,"grounded health / spawn");}Check(kinds.Count==3,"three actual species");
  var restored=new WildlifeSimulation(2506,spawn.Save());restored.Spawn(Player(),Flat);Check(restored.Animals.Count==initial,"persisted spawn sites");
  for(int species=0;species<3;species++)
  {
   var a=Animal(species);var sim=Sim(a);int bites=0;sim.Step(.1f,new[]{Player(3)},Flat,(id,d)=>bites++);Check(sim.Animals[0].x<0,"unprovoked species flees "+species);Check(bites==0,"not an enemy on spawn");
   sim=Sim(a);Check(sim.Hit(a.id,103,100,51,0)==null&&sim.Animals[0].health==a.health,"out-of-reach hit rejected");
   AnimalLoot loot=null;for(int i=0;i<20;i++){var l=sim.Hit(a.id,103,1,51,0);if(l!=null){Check(loot==null,"one death drop");loot=l;}}
   Check(loot!=null&&loot.meat>0&&loot.hide>0&&sim.Animals[0].dead,"health -> death -> meat/hide");restored= new WildlifeSimulation(2506,sim.Save());Check(restored.Hit(a.id,103,1,51,0)==null,"dead animal never pays twice after load");
  }
  var boar=Sim(Animal(2));boar.Hit("a:0:0",0,1,51.62f,0);int damage=0;for(int i=0;i<12;i++)boar.Step(.1f,new[]{Player(1)},Flat,(id,d)=>damage+=(int)d);Check(damage==8,"boar defense and cooldown");for(int i=0;i<7;i++)boar.Step(.1f,new[]{Player(1)},Flat,(id,d)=>damage+=(int)d);Check(damage==16,"boar second timed bite");
  var blocked=Sim(Animal(0));float bx=blocked.Animals[0].x,bz=blocked.Animals[0].z;for(int i=0;i<20;i++)blocked.Step(.1f,new[]{Player(2)},(x,z,y,r,h)=>x==0&&z==0?50:float.NaN,(id,d)=>{});Check(blocked.Animals[0].x==bx&&blocked.Animals[0].z==bz,"does not cross unsupported ground");
  spawn= new WildlifeSimulation(2506);for(int i=0;i<200;i++)spawn.Spawn(Player(i*24),Flat);Check(spawn.Animals.Count<=256&&spawn.Save().sites.Length<=4096,"bounded persistent state");
#if TERRAIN_REFERENCE_CHECK
  var found=new HashSet<ushort>();
  for(int x=-64;x<64;x+=3)for(int z=-64;z<64;z+=3)for(int y=0;y<78;y+=2)
  {ushort old=TerrainGenerator.Sample(x,y,z,2506,Dimension.Lestingia);Check(old==TerrainGenerator.SampleContent(x,y,z,2506,Dimension.Lestingia,1),"legacy sample unchanged");ushort next=TerrainGenerator.SampleContent(x,y,z,2506,Dimension.Lestingia,2);found.Add(next);Check(next<=46,"known new terrain ID");if(y==0)Check(next==9,"bedrock protected");}
  foreach(ushort id in new ushort[]{19,20,21,22,23,24,25,27,28,29,30,32,33,34})Check(found.Contains(id),"actual generated new resource "+id);
  Check(BlockRegistry.Get(46).IsSolid&&!BlockRegistry.Occludes(44),"registry / transparent solid glass");
#endif
  Console.WriteLine("PASS: "+tests+" expanded-core assertions; actual inventory/furnace/recipes, deer/hare/boar spawning, flee/defense, health, collision, tombstones and bounded persistence. Not a Unity graphics run.");
 }
}
