using Unity.Mathematics;
namespace LestingCraft.World
{
 // Content generation 3. Legacy samplers are deliberately not edited.
 public static class NatureGenerator
 {
  public const int ContentVersion=3;
  public struct TreeColumn
  {public int Base;public ulong Wood,Leaves;public ushort At(int y){int bit=y-Base;if(bit<0||bit>=64)return 0;ulong flag=1UL<<bit;return (Wood&flag)!=0?(ushort)4:(Leaves&flag)!=0?(ushort)5:(ushort)0;}}
  static float U(int x,int y,int z,int seed)=>(TerrainGenerator.Hash(x,y,z,seed)&65535u)/65535f;
  public static float Noise3(float x,float y,float z,int seed)
  {
   int ix=(int)math.floor(x),iy=(int)math.floor(y),iz=(int)math.floor(z);float a=x-ix,b=y-iy,c=z-iz;a=a*a*(3-2*a);b=b*b*(3-2*b);c=c*c*(3-2*c);
   float lo=math.lerp(math.lerp(U(ix,iy,iz,seed),U(ix+1,iy,iz,seed),a),math.lerp(U(ix,iy+1,iz,seed),U(ix+1,iy+1,iz,seed),a),b);
   float hi=math.lerp(math.lerp(U(ix,iy,iz+1,seed),U(ix+1,iy,iz+1,seed),a),math.lerp(U(ix,iy+1,iz+1,seed),U(ix+1,iy+1,iz+1,seed),a),b);return math.lerp(lo,hi,c);
  }
  static float Smooth(float a,float b,float x){float t=math.saturate((x-a)/(b-a));return t*t*(3-2*t);}
  public static float ForestDensity(int x,int z,int seed)
  {
   float n=TerrainGenerator.Noise(x/210f,z/210f,seed^45001)*.72f+TerrainGenerator.Noise(x/73f,z/73f,seed^9191)*.28f;
   float region=Smooth(.37f,.59f,n),start=1-Smooth(60,165,math.sqrt((float)x*x+(float)z*z));
   float clearing=Smooth(.65f,.85f,TerrainGenerator.Noise(x/29f,z/29f,seed^5303));return math.max(region,start)*(1-clearing*.88f);
  }
  public static bool Entrance(int x,int y,int z,int seed)
  {
   int cx=(int)math.floor(x/64f),cz=(int)math.floor(z/64f);
   for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
   {
    int a=cx+dx,b=cz+dz;uint h=TerrainGenerator.Hash(a,0,b,seed^33019);if((a!=0||b!=0)&&h%3!=0)continue;
    int ax=a*64+20+(int)(h%24),az=b*64+20+(int)((h>>8)%24);if(math.abs(x-ax)>29||math.abs(z-az)>23)continue;
    int top=TerrainGenerator.Height(ax,az,seed,Dimension.Lestingia);if(top<44||top>64)continue;int bottom=math.max(9,top-24);
    if(y<bottom-6||y>top+1)continue;float t=math.saturate((top-y)/(float)(top-bottom)),bend=4*t*(1-t);
    float px=ax+15*t+3*bend,pz=az+8*t-3*bend,radius=2.6f+t*1.1f;
    if(y>=bottom&&y<=top+1&&(x-px)*(x-px)+(z-pz)*(z-pz)<radius*radius)return true;
    float qx=(x-ax-15)/10f,qy=(y-bottom)/6f,qz=(z-az-8)/9f;if(qx*qx+qy*qy+qz*qz<1)return true;
   }return false;
  }
  public static bool Cave(int x,int y,int z,int seed,int height)
  {
   if(y<=3||y>height)return false;if(Entrance(x,y,z,seed))return true;if(y>=height-4)return false;
   float warp=(Noise3(x*.025f,y*.028f,z*.025f,seed^7039)-.5f)*8;
   float px=x+warp,pz=z-warp*.7f;
   float a=math.abs(Noise3(px*.061f,y*.073f,pz*.061f,seed^9137)-.5f);
   float b=math.abs(Noise3((px+91)*.057f,y*.067f,(pz-73)*.057f,seed^6301)-.5f);
   float width=.075f+Noise3(x*.019f,y*.023f,z*.019f,seed^8209)*.043f;
   if(a<width&&b<width*.94f)return true;
   float hall=Noise3(x*.030f,y*.047f,z*.030f,seed^4919);return hall>.68f&&Noise3(x*.059f,y*.071f,z*.059f,seed^9989)>.43f;
  }
  public static TreeColumn Trees(int x,int z,int seed,int height)
  {
   var result=new TreeColumn{Base=height-16};if(height<40||height>68)return result;int cx=(int)math.floor(x/8f),cz=(int)math.floor(z/8f);
   for(int dz=-1;dz<=1;dz++)for(int dx=-1;dx<=1;dx++)
   {
    int a=cx+dx,b=cz+dz;uint h=TerrainGenerator.Hash(a,0,b,seed^11549);int ax=a*8+1+(int)(h%6),az=b*8+1+(int)((h>>8)%6);
    float density=ForestDensity(ax,az,seed);if((h>>16)%1000>=density*920)continue;
    int top=TerrainGenerator.Height(ax,az,seed,Dimension.Lestingia);if(top<=42||top>=65||Entrance(ax,top,az,seed))continue;
    int kind=(int)((h>>20)%10),tall=kind<6?8+(int)((h>>4)%5):kind<8?11+(int)((h>>4)%5):13+(int)((h>>4)%6);
    int rx=x-ax,rz=z-az,dist=rx*rx+rz*rz;
    for(int dy=1;dy<=tall+4;dy++)
    {
     int bit=top+dy-result.Base;if(bit<0||bit>=64)continue;bool wood=rx==0&&rz==0&&dy<=tall;
     if(kind<6&&dy==tall-3&&(rx==0&&math.abs(rz)<=2||rz==0&&math.abs(rx)<=2))wood=true;
     bool leaf=false;
     if(kind<6){float ry=(dy-(tall-1))*.88f;float r=3.4f+(h%2)*.7f;leaf=dist+ry*ry<r*r&&dy>=tall-5;}
     else if(kind<8){float ry=(dy-(tall-1))*1.1f;leaf=dist+ry*ry<9&&dy>=tall-4;}
     else {float r=math.min(4.4f,(tall-dy)*.34f+1.1f);leaf=dy>=5&&dy<=tall+1&&dist<r*r&&(dy%3!=0||dist<2);}
     if(wood)result.Wood|=1UL<<bit;else if(leaf)result.Leaves|=1UL<<bit;
    }
   }return result;
  }
  public static ushort Column(int x,int y,int z,int seed,int height,TreeColumn trees)
  {
   if(y<0||y>=256)return 0;if(y==0)return 9;
   if(y>height){if(y<=40)return 7;return trees.At(y);}
   if(Cave(x,y,z,seed,height))return 0;
   ushort raw=y==height?(height<=41?(ushort)6:height>=64?(ushort)14:(ushort)3):y>height-4?(ushort)2:TerrainGenerator.Hash(x,y,z,seed)%143==0?(ushort)13:(ushort)1;
   return TerrainGenerator.Expanded(raw,x,y,z,seed,Dimension.Lestingia,height,2);
  }
  public static ushort Sample(int x,int y,int z,int seed)
  {if(y<0||y>=256)return 0;int h=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia);var trees=y>h&&y<h+48?Trees(x,z,seed,h):default(TreeColumn);return Column(x,y,z,seed,h,trees);}
 }
}
