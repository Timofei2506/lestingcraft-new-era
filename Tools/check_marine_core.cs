extern alias previous;
using System;using System.IO;using System.Collections.Generic;using LestingCraft.World;
class CheckMarineCore
{
 static int checks;static void Check(bool b,string name){checks++;if(!b)throw new Exception(name);}
 static uint rng=17102026;static int Next(int n){rng=unchecked(rng*1664525+1013904223);return (int)(rng%(uint)n);}
 static void Main(string[] args)
 {
  var lines=new List<string>();int[] seeds={2506,1,-17,int.MinValue,int.MaxValue};
  for(int i=0;i<12000;i++)
  {
   int seed=seeds[i%5],x=Next(4097)-2048,z=Next(4097)-2048,y=Next(91);
   for(int v=1;v<=7;v++)Check(TerrainGenerator.SampleContent(x,y,z,seed,Dimension.Lestingia,v)==previous::LestingCraft.World.TerrainGenerator.SampleContent(x,y,z,seed,previous::LestingCraft.World.Dimension.Lestingia,v),"Legacy including unvisited v"+v+" "+x+","+y+","+z);
   int h=OceanGenerator.Height(x,z,seed),kind=(int)OceanGenerator.Biome(x,z,seed),id=TerrainGenerator.SampleContent(x,y,z,seed,Dimension.Lestingia,8);Check(h>=19&&h<=72,"bounded sea basin height");Check(id>=0&&id<=48,"registered block");
   lines.Add(seed+","+x+","+y+","+z+","+h+","+OceanGenerator.Weight(x,z,seed)+","+kind+","+id);
   if(i<128)
   {
    var trees=BiomeGenerator.Trees(x,z,seed,h,8);var structure=ExpeditionGenerator.At(x,z,seed,8);var biome=OceanGenerator.Biome(x,z,seed);
    for(int yy=0;yy<128;yy++)
    {ushort fast=LestingCraft.Story.StoryWorld.TrySample(x,yy,z,seed,out ushort placed)?OceanGenerator.StoryBlock(x,yy,z,seed,placed):structure.Apply(OceanGenerator.Column(x,yy,z,seed,h,trees,biome),yy,seed);Check(fast==TerrainGenerator.SampleContent(x,yy,z,seed,Dimension.Lestingia,8),"v8 point/optimized column mismatch "+x+","+yy+","+z);}
   }
  }
  foreach(int seed in seeds)for(int z=-190;z<=158;z+=11)for(int x=-110;x<=270;x+=13)Check(OceanGenerator.Height(x,z,seed)==TerrainGenerator.Height(x,z,seed,Dimension.Lestingia),"story terrain changed");
  int floor=LestingCraft.Story.StoryWorld.Floor(2506);foreach(var p in new[]{new[]{-39,-24},new[]{43,-86},new[]{88,48},new[]{105,48},new[]{96,26}}){Check(TerrainGenerator.SampleContent(p[0],floor+5,p[1],2506,Dimension.Lestingia,7)==15,"legacy lamp must keep original topology");Check(TerrainGenerator.SampleContent(p[0],floor+5,p[1],2506,Dimension.Lestingia,8)==0,"new lamp must not contain a campfire voxel");}
  for(int y=1;y<=4;y++)Check(TerrainGenerator.SampleContent(96,floor+y,72,2506,Dimension.Lestingia,8)==(y==4?18:4),"south lantern must have a real supported pole");
  if(args.Length>0)File.WriteAllLines(args[0],lines);Console.WriteLine("PASS "+checks+" managed marine checks: 84000 exact v1..7 comparisons against 0.3.2; v8 optimized column/point parity; five seeds incl signed extremes; protected terrain; five campfire replacements + repaired south pole. NOT Unity/GPU execution.");
 }
}
