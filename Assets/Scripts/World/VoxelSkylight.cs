using System;using System.Collections.Generic;
namespace LestingCraft.World
{
 // Dynamic 0..15 sky light, not camera-depth fog or constant underground ambient.
 // Unknown chunks are dark boundaries; registration joins the propagation queues.
 public sealed class VoxelSkylight
 {
  struct Cell:IEquatable<Cell>{public int x,y,z;public Cell(int x,int y,int z){this.x=x;this.y=y;this.z=z;}public bool Equals(Cell b)=>x==b.x&&y==b.y&&z==b.z;public override bool Equals(object b)=>b is Cell&&Equals((Cell)b);public override int GetHashCode(){unchecked{return x*734287^y*912931^z*438289;}}}
  sealed class Field {public int x,z;public ChunkData Data;public readonly byte[] Sky=new byte[65536],Source=new byte[65536];}
  readonly Dictionary<long,Field> fields=new Dictionary<long,Field>();readonly Queue<Cell> queue=new Queue<Cell>();readonly HashSet<Cell> queued=new HashSet<Cell>();
  public Action<int,int> Changed;public int Pending=>queue.Count;
  static long Key(int x,int z)=>((long)x<<32)^(uint)z;
  static int Chunk(int x)=>x>=0?x/16:(x-15)/16;
  bool Find(int x,int y,int z,out Field field,out int index){int cx=Chunk(x),cz=Chunk(z);index=y>=0&&y<256?ChunkData.IndexOf(x-cx*16,y,z-cz*16):0;return y>=0&&y<256&&fields.TryGetValue(Key(cx,cz),out field)?true:Missing(out field);}
  static bool Missing(out Field f){f=null;return false;}
  public byte Get(int x,int y,int z){if(y>=256)return 15;if(y<0)return 0;return Find(x,y,z,out var f,out int i)?f.Sky[i]:(byte)0;}
  public static int Loss(ushort id)=>id==0||id==15||id==5?1:id==7?2:id==44?1:id==11?3:16;
  void Add(int x,int y,int z){if(!Find(x,y,z,out var f,out int i))return;var c=new Cell(x,y,z);if(queued.Add(c))queue.Enqueue(c);}
  void Around(int x,int y,int z){Add(x,y,z);Add(x+1,y,z);Add(x-1,y,z);Add(x,y+1,z);Add(x,y-1,z);Add(x,y,z+1);Add(x,y,z-1);}
  void Column(Field f,int x,int z,bool initial)
  {
   int sky=15;for(int y=255;y>=0;y--){int i=ChunkData.IndexOf(x,y,z);ushort id=f.Data.GetBlock(x,y,z);int loss=Loss(id);if(loss>=16)sky=0;else if(loss>1||id==5)sky=Math.Max(0,sky-loss);byte level=(byte)sky;
    if(initial){f.Source[i]=level;f.Sky[i]=level;}else if(f.Source[i]!=level){f.Source[i]=level;Around(f.x*16+x,y,f.z*16+z);}
   }
  }
  public void Register(int cx,int cz,ChunkData data)
  {
   var f=new Field{x=cx,z=cz,Data=data};fields[Key(cx,cz)]=f;
   for(int z=0;z<16;z++)for(int x=0;x<16;x++)Column(f,x,z,true);
   for(int y=0;y<256;y++)for(int z=0;z<16;z++)for(int x=0;x<16;x++)if(f.Source[ChunkData.IndexOf(x,y,z)]<15&&Loss(data.GetBlock(x,y,z))<16)Add(cx*16+x,y,cz*16+z);
   Border(cx,cz);Changed?.Invoke(cx,cz);
  }
  void Border(int cx,int cz){for(int y=0;y<256;y++)for(int a=0;a<16;a++){Add(cx*16-1,y,cz*16+a);Add(cx*16+16,y,cz*16+a);Add(cx*16+a,y,cz*16-1);Add(cx*16+a,y,cz*16+16);}}
  public void Remove(int cx,int cz){fields.Remove(Key(cx,cz));Border(cx,cz);}
  public void Edit(int x,int y,int z){if(!Find(x,y,z,out var f,out int i))return;Column(f,x-f.x*16,z-f.z*16,false);Around(x,y,z);}
  public void CopyVolume(byte[] pixels,int ox,int oy,int oz,int width,int height,int depth)
  {for(int z=0;z<depth;z++)for(int x=0;x<width;x++){int wx=ox+x,wz=oz+z,cx=Chunk(wx),cz=Chunk(wz);fields.TryGetValue(Key(cx,cz),out var f);for(int y=0;y<height;y++){int wy=oy+y;pixels[x+width*(y+height*z)]=(byte)(wy>=256?255:wy<0||f==null?0:f.Sky[ChunkData.IndexOf(wx-cx*16,wy,wz-cz*16)]*17);}}}
  // Main-thread immutable snapshot copy. Only nine dictionary lookups per chunk,
  // rather than a lookup for each of the 82,944 padded voxels.
  public void CopyPaddedChunk(int cx,int cz,byte[] destination)
  {
   if(destination==null||destination.Length!=18*256*18)throw new ArgumentException("Invalid padded skylight buffer");
   Array.Clear(destination,0,destination.Length);
   for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
   {
    if(!fields.TryGetValue(Key(cx+dx,cz+dz),out var f))continue;
    int x0=dx<0?-1:dx>0?16:0,x1=dx==0?15:x0;
    int z0=dz<0?-1:dz>0?16:0,z1=dz==0?15:z0;
    for(int y=0;y<256;y++)for(int z=z0;z<=z1;z++)
     Array.Copy(f.Sky,ChunkData.IndexOf(x0-dx*16,y,z-dz*16),destination,x0+1+18*(z+1+18*y),x1-x0+1);
   }
  }
  public int Process(int budget=32768)
  {
   int processed=0;while(queue.Count>0&&processed++<budget)
   {
    var p=queue.Dequeue();queued.Remove(p);if(!Find(p.x,p.y,p.z,out var f,out int i))continue;int loss=Loss(f.Data.GetBlock(p.x-f.x*16,p.y,p.z-f.z*16)),level=f.Source[i];
    if(loss<16){level=Math.Max(level,Get(p.x+1,p.y,p.z)-loss);level=Math.Max(level,Get(p.x-1,p.y,p.z)-loss);level=Math.Max(level,Get(p.x,p.y+1,p.z)-loss);level=Math.Max(level,Get(p.x,p.y-1,p.z)-loss);level=Math.Max(level,Get(p.x,p.y,p.z+1)-loss);level=Math.Max(level,Get(p.x,p.y,p.z-1)-loss);}else level=0;
    byte next=(byte)Math.Max(0,level);if(f.Sky[i]==next)continue;f.Sky[i]=next;Changed?.Invoke(f.x,f.z);Around(p.x,p.y,p.z);
   }return processed;
  }
 }
}
