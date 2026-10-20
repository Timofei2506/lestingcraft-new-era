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
