using System;
using System.IO;
using LestingCraft.Gameplay;
class SpaceAssetCheck
{
    static void Check(bool b,string message){if(!b)throw new Exception(message);}
    static void Main()
    {
        string[] names={"Shuttle","LaunchPad","Station"};
        int[] solid={13929,27813,10375};
        for(int j=0;j<names.Length;j++)
        {
            byte[] bytes=File.ReadAllBytes("Assets/Resources/Space/"+names[j]+".bytes");
            var model=SpaceVoxelAsset.Read(bytes);
            Check(model.SolidCount==solid[j],names[j]+" solid cells");
            if(names[j]=="Shuttle")
            {
                var walkable=ShuttleCabinCarving.Make(model);
                Check(walkable.SolidCount==13591,"Derived cabin removes exactly 338 internal/hatch voxels");
                Check(model.SolidCount==13929,"Approved model stays unchanged in memory");
                Check(walkable.At(31,30,33)==0&&walkable.At(31,43,33)==0,"Two decks are hollow");
                Check(walkable.At(31,45,36)==model.At(31,45,36),"Small approved windows retained");
                int surface=0;
                for(int x=0;x<model.X;x++)for(int y=0;y<model.Y;y++)for(int z=0;z<model.Z;z++)
                {
                    if(model.At(x,y,z)==0||walkable.At(x,y,z)!=0)continue;
                    if(model.At(x+1,y,z)==0||model.At(x-1,y,z)==0||model.At(x,y+1,z)==0||
                       model.At(x,y-1,z)==0||model.At(x,y,z+1)==0||model.At(x,y,z-1)==0)
                    {surface++;Check(ShuttleCabinCarving.Remove(x-31,y,z-30)&&y>=28&&y<=30,"Only the hatch pierces exterior");}
                }
                Check(surface==6,"Two-metre portal; no other exterior changes");
                Check(!VerticalShuttleWalk.CanStep(-9.2,29,3,-8.8,29,3,false)&&
                       VerticalShuttleWalk.CanStep(-9.2,29,3,-8.8,29,3,true),"Hatch physically gates short steps");
                Check(!VerticalShuttleWalk.CanStep(-10,29,3,0,43,3,true),"No boarding teleport");
            }
            Check(model.X>=60&&model.Y>=38&&model.Z>=34,names[j]+" is full size");
            Check(model.At(-1,0,0)==0&&model.At(model.X,0,0)==0&&model.At(0,-1,0)==0,"safe outside volume");
            int faces=0;for(int x=0;x<model.X;x++)for(int y=0;y<model.Y;y++)for(int z=0;z<model.Z;z++)
            {
                if(model.At(x,y,z)==0)continue;
                if(model.At(x+1,y,z)==0)faces++;
                if(model.At(x-1,y,z)==0)faces++;
                if(model.At(x,y+1,z)==0)faces++;
                if(model.At(x,y-1,z)==0)faces++;
                if(model.At(x,y,z+1)==0)faces++;
                if(model.At(x,y,z-1)==0)faces++;
            }
            Check(faces>=9000&&faces<50000,names[j]+" exposed 3D faces");
            foreach(var c in model.Palette)Check(c.Name.ToLowerInvariant().IndexOf("crawler",StringComparison.Ordinal)<0,"no crawler model");
            var broken=(byte[])bytes.Clone();broken[0]=0;bool rejected=false;
            try{SpaceVoxelAsset.Read(broken);}catch(InvalidDataException){rejected=true;}
            Check(rejected,"corrupt header rejected");
            broken=(byte[])bytes.Clone();Array.Resize(ref broken,bytes.Length-3);rejected=false;
            try{SpaceVoxelAsset.Read(broken);}catch(EndOfStreamException){rejected=true;}catch(InvalidDataException){rejected=true;}
            Check(rejected,"truncated model rejected");
            Console.WriteLine(names[j]+": "+model.SolidCount+" cells, "+faces*2+" triangles, "+model.Palette.Length+" materials");
        }
        Console.WriteLine("PASS: approved spacecraft 3D geometry/resources decoded correctly");
    }
}
