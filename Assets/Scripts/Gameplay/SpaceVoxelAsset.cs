using System;
using System.IO;
using System.Text;

namespace LestingCraft.Gameplay
{
    public struct SpaceVoxelColor
    {
        public byte R,G,B,A;
        public string Name;
    }
    /// <summary>Strict, Unity-independent decoder of approved spacecraft voxel resources.</summary>
    public sealed class SpaceVoxelAsset
    {
        public readonly int X,Y,Z;
        public readonly SpaceVoxelColor[] Palette;
        readonly byte[] cells;
        SpaceVoxelAsset(int x,int y,int z,SpaceVoxelColor[] palette,byte[] cells)
        {X=x;Y=y;Z=z;Palette=palette;this.cells=cells;}
        public byte At(int x,int y,int z)
        {return x<0||x>=X||y<0||y>=Y||z<0||z>=Z? (byte)0:cells[(x*Y+y)*Z+z];}
        public int SolidCount {get {int total=0;foreach(byte value in cells)if(value!=0)total++;return total;}}
        // An in-memory derivative for the traversable shuttle; approved source bytes never change.
        internal SpaceVoxelAsset RemoveCells(Func<int,int,int,bool> remove)
        {
            if(remove==null)throw new ArgumentNullException("remove");
            var derived=(byte[])cells.Clone();
            for(int x=0;x<X;x++)for(int y=0;y<Y;y++)for(int z=0;z<Z;z++)
            {
                int index=(x*Y+y)*Z+z;
                if(derived[index]!=0&&remove(x,y,z))derived[index]=0;
            }
            return new SpaceVoxelAsset(X,Y,Z,Palette,derived);
        }
        public static SpaceVoxelAsset Read(byte[] data)
        {
            if(data==null||data.Length<27||data.Length>5_000_000)throw new InvalidDataException("Invalid model size");
            using(var ms=new MemoryStream(data,false))using(var input=new BinaryReader(ms,Encoding.UTF8))
            {
                if(input.ReadByte()!='L'||input.ReadByte()!='C'||input.ReadByte()!='V'||input.ReadByte()!='M'||input.ReadInt32()!=1)
                    throw new InvalidDataException("Invalid model header");
                int x=input.ReadInt32(),y=input.ReadInt32(),z=input.ReadInt32();
                if(x<1||x>128||y<1||y>128||z<1||z>128)throw new InvalidDataException("Invalid dimensions");
                int count=input.ReadUInt16();if(count<2||count>255)throw new InvalidDataException("Invalid palette count");
                var colors=new SpaceVoxelColor[count];
                for(int i=0;i<count;i++)
                {
                    var color=new SpaceVoxelColor{R=input.ReadByte(),G=input.ReadByte(),B=input.ReadByte(),A=input.ReadByte()};
                    int length=input.ReadByte();color.Name=Encoding.UTF8.GetString(input.ReadBytes(length));
                    if(color.Name.Length==0)throw new InvalidDataException("Unnamed material");colors[i]=color;
                }
                int runCount=input.ReadInt32();
                if(runCount<1||runCount>1_000_000)throw new InvalidDataException("Invalid run count");
                var cells=new byte[x*y*z];int cursor=0;
                for(int i=0;i<runCount;i++)
                {
                    int material=input.ReadByte(),length=input.ReadInt32();
                    if(material>=count||length<=0||length>cells.Length-cursor)throw new InvalidDataException("Invalid RLE span");
                    for(int j=0;j<length;j++)cells[cursor++]=(byte)material;
                }
                if(cursor!=cells.Length||ms.Position!=ms.Length)throw new InvalidDataException("Truncated/trailing model data");
                return new SpaceVoxelAsset(x,y,z,colors,cells);
            }
        }
    }
}
