using Unity.Mathematics;
namespace LestingCraft.World
{
 // Generation 8 only. Integer basin/coast decisions; no changes to versions 1..7.
 public static class OceanGenerator
 {
  public const int Version=8,SeaLevel=40;
  static int Floor(int x,int n)=>x>=0?x/n:(x-n+1)/n;
  public static int Weight(int x,int z,int seed)
  {
   if(ExpeditionGenerator.Reserved(x,z))return 0;
   int cellX=Floor(x,512),cellZ=Floor(z,512),weight=0;
   for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
   {
    int cx=cellX+dx,cz=cellZ+dz;uint hash=TerrainGenerator.Hash(cx,0,cz,seed^90317);
    int ax=cx*512+112+(int)(hash%288),az=cz*512+112+(int)((hash>>9)%288),radius=152+(int)((hash>>20)%72);
    int rx=x-ax,rz=z-az,rr=radius*radius,d=rx*rx+rz*rz;if(d>=rr)continue;
    int t=(rr-d)*1024/rr;int smooth=t*t/1024*(3072-2*t)/1024;weight=math.max(weight,smooth);
   }
   // Smooth approach to protected story terrain, rather than a vertical seam at its boundary.
   int margin=math.max(math.max(-110-x,x-270),math.max(-190-z,z-158));
   return math.min(weight,math.clamp(margin*32,0,1024));
  }
  public static bool StreetLamp(int x,int z)=>(x==-39&&z==-24)||(x==43&&z==-86)||(x==88&&z==48)||(x==105&&z==48)||(x==96&&(z==26||z==72));
  public static ushort StoryBlock(int x,int y,int z,int seed,ushort block){if(!StreetLamp(x,z))return block;int dy=y-LestingCraft.Story.StoryWorld.Floor(seed);return dy>=1&&dy<=3?(ushort)4:dy==4?(ushort)18:dy==5?(ushort)0:block;}
  public static int Height(int x,int z,int seed)
  {int legacy=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia),w=Weight(x,z,seed);return (legacy*(1024-w)+19*w)/1024;}
  public static BiomeKind Biome(int x,int z,int seed)
  {int h=Height(x,z,seed);return h<SeaLevel?BiomeKind.Sea:h<=SeaLevel+3&&Weight(x,z,seed)>0?BiomeKind.Coast:BiomeGenerator.At(x,z,seed);}
  public static ushort Column(int x,int y,int z,int seed,int height,NatureGenerator.TreeColumn trees,BiomeKind kind)
  {
   if(y<0||y>=256)return 0;if(y==0)return 9;
   if(kind==BiomeKind.Sea||kind==BiomeKind.Coast)
   {
    if(y>height)return y<=SeaLevel?(ushort)7:trees.At(y);
    if(NatureGenerator.Cave(x,y,z,seed,height))return 0;
    if(y>=height-3)return y>=height-1?(ushort)6:(ushort)43;
   }
   return BiomeGenerator.Column(x,y,z,seed,height,trees,kind);
  }
  public static ushort Sample(int x,int y,int z,int seed)
  {if(y<0||y>=256)return 0;int h=Height(x,z,seed);var trees=y>h&&y<h+48?BiomeGenerator.Trees(x,z,seed,h,8):default(NatureGenerator.TreeColumn);return Column(x,y,z,seed,h,trees,Biome(x,z,seed));}
 }
}
