using System;using System.IO;using System.Collections.Generic;using LestingCraft.World;
// Compile with mcs and the actual ChunkData/BlockRegistry/ExpandedContent/VoxelSkylight sources; no Unity dependencies.
public static class CheckSkylightCore
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
 public static void Main(){Light();Console.WriteLine("PASS "+checks+" dynamic sky assertions (sealed/open/closed cave, chunk seams, unload, negative coordinates, volume layout).");}
}
