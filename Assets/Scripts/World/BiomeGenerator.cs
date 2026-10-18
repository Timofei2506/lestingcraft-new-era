using Unity.Mathematics;
namespace LestingCraft.World
{
 // Content 7 only: old samplers and terrain heights remain immutable.
 public enum BiomeKind { Meadow, MixedForest, Taiga, Dunes, Marsh, RockyHighlands }
 public static class BiomeGenerator
 {
  public const int Version=7;
  public static string Name(BiomeKind kind){switch(kind){case BiomeKind.Meadow:return "Луга";case BiomeKind.Taiga:return "Хвойная тайга";case BiomeKind.Dunes:return "Песчаные дюны";case BiomeKind.Marsh:return "Заболоченный лес";case BiomeKind.RockyHighlands:return "Каменистые пустоши";default:return "Смешанный лес";}}
  static int Floor(int x,int size)=>x>=0?x/size:(x-size+1)/size;
  public static BiomeKind At(int x,int z,int seed)
  {
   if(ExpeditionGenerator.Reserved(x,z))return BiomeKind.MixedForest;
   int cx=Floor(x,160),cz=Floor(z,160),best=int.MaxValue,kind=1;
   for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
   {int a=cx+dx,b=cz+dz;uint h=TerrainGenerator.Hash(a,0,b,seed^81473);int ax=a*160+32+(int)(h%96),az=b*160+32+(int)((h>>8)%96),rx=x-ax,rz=z-az,d=rx*rx+rz*rz;if(d<best){best=d;kind=(int)((h>>19)%6);}}
   return (BiomeKind)kind;
  }
  public static float TemperatureOffset(BiomeKind biome)=>biome==BiomeKind.Taiga?-9:biome==BiomeKind.Dunes?10:biome==BiomeKind.Marsh?3:biome==BiomeKind.RockyHighlands?-6:0;
  public static NatureGenerator.TreeColumn Trees(int x,int z,int seed,int height)
  {
   if(ExpeditionGenerator.Reserved(x,z))return NatureGenerator.Trees(x,z,seed,height);
   var result=new NatureGenerator.TreeColumn{Base=height-16};int cx=Floor(x,8),cz=Floor(z,8);
   for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
   {
    int a=cx+dx,b=cz+dz;uint h=TerrainGenerator.Hash(a,0,b,seed^62113);int ax=a*8+1+(int)(h%6),az=b*8+1+(int)((h>>8)%6);var kind=At(ax,az,seed);
    int density=kind==BiomeKind.MixedForest?680:kind==BiomeKind.Taiga?760:kind==BiomeKind.Marsh?390:kind==BiomeKind.Meadow?45:kind==BiomeKind.RockyHighlands?25:0;
    if((h>>16)%1000>=density)continue;int top=TerrainGenerator.Height(ax,az,seed,Dimension.Lestingia);if(top<=42||top>=67||NatureGenerator.Entrance(ax,top,az,seed))continue;
    bool pine=kind==BiomeKind.Taiga;int tall=pine?12+(int)(h%6):kind==BiomeKind.Marsh?6+(int)(h%3):8+(int)(h%5);int rx=x-ax,rz=z-az,dist=rx*rx+rz*rz;
    for(int dy=1;dy<=tall+3;dy++)
    {
     int bit=top+dy-result.Base;if(bit<0||bit>=64)continue;bool wood=rx==0&&rz==0&&dy<=tall;
     if(kind==BiomeKind.Marsh&&dy<=2&&math.abs(rx)+math.abs(rz)==1)wood=true;
     bool leaf;if(pine){int radius=math.min(4,(tall-dy)/3+1);leaf=dy>=4&&dy<=tall+1&&dist<=radius*radius&&(dy%3!=0||dist<=1);}
     else {int radius=kind==BiomeKind.Marsh?4:3,vertical=dy-(tall-1);leaf=dy>=tall-3&&dist+vertical*vertical<=radius*radius;}
     if(wood)result.Wood|=1UL<<bit;else if(leaf)result.Leaves|=1UL<<bit;
    }
   }return result;
  }
  public static ushort Column(int x,int y,int z,int seed,int height,NatureGenerator.TreeColumn trees)=>Column(x,y,z,seed,height,trees,At(x,z,seed));
  public static ushort Column(int x,int y,int z,int seed,int height,NatureGenerator.TreeColumn trees,BiomeKind kind)
  {
   if(ExpeditionGenerator.Reserved(x,z))return NatureGenerator.Column(x,y,z,seed,height,trees);
   if(y<0||y>=256)return 0;if(y==0)return 9;
   if(y>height){if(y<=40)return 7;return trees.At(y);}
   if(NatureGenerator.Cave(x,y,z,seed,height))return 0;
   if(height>41&&y>=height-3)
   {
    if(kind==BiomeKind.Dunes)return y>=height-1?(ushort)6:(ushort)43;
    if(kind==BiomeKind.Taiga)return y==height?(ushort)14:(ushort)2;
    if(kind==BiomeKind.Marsh){uint pool=TerrainGenerator.Hash(Floor(x,7),0,Floor(z,7),seed^47119);if(pool%5==0&&y>=height-1)return 7;return y==height?(ushort)34:(ushort)33;}
    if(kind==BiomeKind.RockyHighlands)return y==height?(height>=62?(ushort)14:(ushort)32):(ushort)27;
   }
   ushort raw=y==height?(height<=41?(ushort)6:height>=64?(ushort)14:(ushort)3):y>height-4?(ushort)2:TerrainGenerator.Hash(x,y,z,seed)%143==0?(ushort)13:(ushort)1;
   return TerrainGenerator.Expanded(raw,x,y,z,seed,Dimension.Lestingia,height,2);
  }
  public static ushort Sample(int x,int y,int z,int seed)
  {if(y<0||y>=256)return 0;int h=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia);var trees=y>h&&y<h+48?Trees(x,z,seed,h):default(NatureGenerator.TreeColumn);return Column(x,y,z,seed,h,trees);}
 }
}
