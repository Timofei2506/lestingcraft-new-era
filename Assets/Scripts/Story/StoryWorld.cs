using Unity.Mathematics;using LestingCraft.World;
namespace LestingCraft.Story
{
 public static class StoryWorld
 {
  public static int Floor(int seed)=>math.max(44,TerrainGenerator.Height(0,0,seed,Dimension.Lestingia));
  public static bool Protected(int x,int z)=>Region(x,z)<=0;
  static int Region(int x,int z)=>math.min(math.max(math.abs(x),math.abs(z-8))-9,math.min(math.min(math.max(math.abs(x+32),math.abs(z+32))-12,math.max(math.abs(x-96),math.abs(z-48))-24),math.min(math.max(math.abs(x-48),math.abs(z+96))-13,math.max(math.abs(x-176),math.abs(z+112))-17)));
  static bool Segment(int x,int z,int ax,int az,int bx,int bz,int r)
  {long dx=bx-ax,dz=bz-az,px=x-ax,pz=z-az,len=dx*dx+dz*dz,t=px*dx+pz*dz;if(t<0)return px*px+pz*pz<=r*r;if(t>len){px=x-bx;pz=z-bz;return px*px+pz*pz<=r*r;}long cross=px*dz-pz*dx;return cross*cross<=r*r*len;}
  static bool Road(int x,int z,int r)=>Segment(x,z,-32,-32,0,8,r)||Segment(x,z,0,8,96,48,r)||Segment(x,z,96,48,48,-96,r)||Segment(x,z,48,-96,176,-112,r)||Segment(x,z,176,-112,96,48,r);
  public static bool TrySample(int x,int y,int z,int seed,out ushort block)
  {
   block=0;if(y<=0||y>=256)return false;int dist=Region(x,z);bool road=Road(x,z,2);if(dist>6&&!Road(x,z,5))return false;
   int baseY=Floor(seed),natural=TerrainGenerator.Height(x,z,seed,Dimension.Lestingia),top=baseY;
   if(dist>0&&!road){int blend=math.min(6,dist);top=(baseY*(6-blend)+natural*blend)/6;}
   if(y<top-3&&y<=natural)return false;
   if(y<=top){block=y==top?(road?(ushort)32:dist<=0?(ushort)3:(ushort)2):y<top-2?(ushort)1:(ushort)2;if(y==baseY&&dist<=0){var plan=StoryPlan.Boxes;for(int i=0;i<plan.Length;i+=7)if(x>=plan[i]&&x<=plan[i+1]&&plan[i+2]<=0&&plan[i+3]>=0&&z>=plan[i+4]&&z<=plan[i+5])block=(ushort)plan[i+6];}return true;}
   if(dist>0||y>baseY+18)return true;
   int ry=y-baseY;var boxes=StoryPlan.Boxes;for(int i=0;i<boxes.Length;i+=7)if(x>=boxes[i]&&x<=boxes[i+1]&&ry>=boxes[i+2]&&ry<=boxes[i+3]&&z>=boxes[i+4]&&z<=boxes[i+5])block=(ushort)boxes[i+6];return true;
  }
 }
}
