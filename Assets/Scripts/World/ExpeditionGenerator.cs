using Unity.Mathematics;
namespace LestingCraft.World
{
 // Version 5 only. No old sampler is modified. Integer structure layout matches server exactly.
 public static class ExpeditionGenerator
 {
  public static bool Reserved(int x,int z)=>x>=-110&&x<=270&&z>=-190&&z<=158;
  public struct Column
  {
   public int X,Z,Base,Kind,MX,MZ,MB;public bool Surface,Mine;
   public ushort Apply(ushort original,int y,int seed)
   {
    if(y<=0||y>=256)return original;
    if(Surface&&y>=Base-32&&y<=Base+48)
    {
     int id=y<=Base?(y==Base?3:1):0;var boxes=StructurePlan.Boxes;for(int i=StructurePlan.Offsets[Kind];i<StructurePlan.Offsets[Kind+1];i+=7)if(X>=boxes[i]&&X<=boxes[i+1]&&y-Base>=boxes[i+2]&&y-Base<=boxes[i+3]&&Z>=boxes[i+4]&&Z<=boxes[i+5])id=boxes[i+6];return (ushort)id;
    }
    if(Mine&&y>=MB&&y<=MB+5)
    {
     int dx=math.abs(MX),dz=math.abs(MZ),dy=y-MB;
     bool corridor=dx<=32&&dz<=2||dz<=24&&dx<=2||dx<=5&&dz<=5;
     if(corridor){if(dy==0)return 18;if(dy==5)return 1;bool beam=dz<=2&&dx%6==0||dx<=2&&dz%6==0;if(beam&&(dy==4||dx==2||dz==2))return 4;return 0;}
    }
    if(y>=5&&y<=22&&(original==1||original==13||original>=19&&original<=30))
    {uint h=TerrainGenerator.Hash((int)math.floor((XWorld)/3f),(int)math.floor(y/3f),(int)math.floor(ZWorld/3f),seed^62801);int a=((XWorld%3)+3)%3,b=y%3,c=((ZWorld%3)+3)%3;if(h%251==0&&math.abs(a-1)+math.abs(b-1)+math.abs(c-1)<=1)return 47;}
    return original;
   }
   public int XWorld,ZWorld;
  }
  public static Column At(int x,int z,int seed)
  {
   var result=new Column{XWorld=x,ZWorld=z};int a=(int)math.floor(x/192f),b=(int)math.floor(z/192f);uint h=TerrainGenerator.Hash(a,0,b,seed^71839);int ax=a*192+64+(int)(h%64),az=b*192+64+(int)((h>>8)%64),kind=(int)((h>>18)%5),rx=x-ax,rz=z-az,rot=(int)((h>>26)%4);int top=TerrainGenerator.Height(ax,az,seed,Dimension.Lestingia);
   if(!Reserved(ax,az)&&top>=44&&top<=62&&math.abs(rx)<=StructurePlan.Radius[kind]&&math.abs(rz)<=StructurePlan.Radius[kind])
   {result.Surface=true;result.Base=top;result.Kind=kind;result.X=rot==0?rx:rot==1?rz:rot==2?-rx:-rz;result.Z=rot==0?rz:rot==1?-rx:rot==2?-rz:rx;}
   // Mines start in the real entrance chambers of nature generation, not sealed random boxes.
   a=(int)math.floor(x/64f);b=(int)math.floor(z/64f);
   for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
   {int cx=a+dx,cz=b+dz;h=TerrainGenerator.Hash(cx,0,cz,seed^33019);if((cx!=0||cz!=0)&&h%3!=0||h%2!=0)continue;ax=cx*64+20+(int)(h%24);az=cz*64+20+(int)((h>>8)%24);top=TerrainGenerator.Height(ax,az,seed,Dimension.Lestingia);if(top<44||top>64||Reserved(ax,az))continue;rx=x-ax-15;rz=z-az-8;if(math.abs(rx)>32||math.abs(rz)>24)continue;result.Mine=true;result.MX=rx;result.MZ=rz;result.MB=math.max(9,top-24)-2;break;}
   return result;
  }
  public static ushort Sample(int x,int y,int z,int seed,ushort original)=>At(x,z,seed).Apply(original,y,seed);
 }
}
