using System;using System.IO;using System.Collections.Generic;using LestingCraft.World;
// Run with Roslyn + actual Unity.Mathematics and runtime managed DLL. No engine calls.
public static class CheckNatureCore
{
 static int checks;static void Check(bool good,string label){checks++;if(!good)throw new Exception(label);}
 static void Drain(VoxelSkylight sky){for(int n=0;n<200&&sky.Pending>0;n++)sky.Process(65536);Check(sky.Pending==0,"light propagation converged");}
 static ChunkData Sealed(){var c=new ChunkData();for(int y=0;y<=16;y++)for(int z=0;z<16;z++)for(int x=0;x<16;x++)c.SetBlock(x,y,z,1);for(int y=4;y<=10;y++)for(int z=4;z<=11;z++)for(int x=4;x<=11;x++)c.SetBlock(x,y,z,0);return c;}
 static void Light()
 {
  var data=Sealed();var sky=new VoxelSkylight();sky.Register(0,0,data);Drain(sky);Check(sky.Get(8,8,8)==0,"sealed cave is dark");Check(sky.Get(8,18,8)==15,"exterior retains sky");
  for(int y=11;y<=16;y++){data.SetBlock(8,y,8,0);sky.Edit(8,y,8);}Drain(sky);Check(sky.Get(8,8,8)==15,"open roof admits direct sky");Check(sky.Get(4,8,4)>0&&sky.Get(4,8,4)<15,"side light attenuates");
  data.SetBlock(8,16,8,1);sky.Edit(8,16,8);Drain(sky);Check(sky.Get(8,8,8)==0,"closing roof removes stale sky");
  for(int x=11;x<16;x++){data.SetBlock(x,8,8,0);sky.Edit(x,8,8);}Drain(sky);Check(sky.Get(15,8,8)==0,"unknown chunk does not leak sky");var outside=new ChunkData();sky.Register(1,0,outside);Drain(sky);Check(sky.Get(15,8,8)==14,"loaded neighbor joins sky across seam");Check(sky.Get(8,8,8)>0,"opening lights cave locally");sky.Remove(1,0);Drain(sky);Check(sky.Get(8,8,8)==0,"unload removes boundary light");
  var negative=new ChunkData();negative.SetBlock(15,20,15,5);sky.Register(-1,-1,negative);Drain(sky);Check(sky.Get(-1,21,-1)==15&&sky.Get(-1,19,-1)==14,"negative cell mapping / leaf attenuation and side fill");
  byte[] volume=new byte[4*4*4];sky.CopyVolume(volume,-2,18,-2,4,4,4);Check(volume[1+4*(3+4)]==255,"GPU volume order x,y,z");
 }
 static uint rng=7139;static int Next(int n){rng=unchecked(rng*1664525+1013904223);return (int)(rng%(uint)n);}
 public static void Main(string[] args)
 {
  Light();int[] seeds={2506,1,42,-17,int.MinValue,int.MaxValue};var rows=new List<string>();int leaves=0,wood=0,air=0,ground=0,entrances=0,maxHeight=0,oldTrees=0;
  for(int i=0;i<12000;i++)
  {
   int seed=seeds[i%seeds.Length],x=Next(1201)-600,z=Next(1201)-600,h=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia),y=i%3==0?h+1+Next(22):Next(75);ushort id=TerrainGenerator.SampleContent(x,y,z,seed,Dimension.Lestingia,3);
   rows.Add("["+x+","+y+","+z+","+seed+","+id+"]");Check(id==NatureGenerator.Column(x,y,z,seed,h,NatureGenerator.Trees(x,z,seed,h)),"optimized column vs point query");
   Check(TerrainGenerator.SampleContent(x,y,z,seed,Dimension.Lestingia,1)==TerrainGenerator.Sample(x,y,z,seed,Dimension.Lestingia),"v1 frozen");Check(TerrainGenerator.SampleContent(x,y,z,seed,Dimension.Lestingia,2)==TerrainGenerator.Expanded(TerrainGenerator.Sample(x,y,z,seed,Dimension.Lestingia),x,y,z,seed,Dimension.Lestingia,h,2),"v2 frozen");
  }
  for(int z=-48;z<48;z++)for(int x=-48;x<48;x++)
  {
   int h=TerrainGenerator.Height(x,z,2506,Dimension.Lestingia);var tree=NatureGenerator.Trees(x,z,2506,h);
   for(int y=4;y<=h+24;y++){ushort id=NatureGenerator.Column(x,y,z,2506,h,tree);if(id==4){wood++;maxHeight=Math.Max(maxHeight,y-h);}if(id==5)leaves++;if(y<h-4){ground++;if(id==0)air++;}if(y>h&&TerrainGenerator.SampleColumn(x,y,z,2506,Dimension.Lestingia,h,true)==4)oldTrees++;}
   if(NatureGenerator.Entrance(x,h,z,2506))entrances++;
   Check(NatureGenerator.Sample(x,0,z,2506)==9,"bedrock preserved");for(int y=1;y<=3;y++)Check(NatureGenerator.Sample(x,y,z,2506)!=0,"protected cave floor");
  }
  Check(wood>oldTrees*2,"forest density substantially increased");Check(maxHeight>=13,"large tree heights");Check(leaves>wood,"leaf crowns");Check(entrances>0,"surface cave entrances");Check(air>ground*.05&&air<ground*.6,"varied cave void fraction");
  if(args.Length>0)File.WriteAllText(args[0],"{\"vectors\":["+string.Join(",",rows)+"]}");
  Console.WriteLine("PASS "+checks+" core checks; 12000 terrain vectors; forest wood="+wood+" legacy="+oldTrees+" leaves="+leaves+" max trunk height="+maxHeight+"; cave air="+air+"/"+ground+"; entrance columns="+entrances+". Real managed code only; not Unity GPU or Burst execution.");
 }
}
