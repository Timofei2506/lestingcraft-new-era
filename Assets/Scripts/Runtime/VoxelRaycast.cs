using System;
using UnityEngine;

namespace LestingCraft.Runtime
{
    /// <summary>Grid traversal against current world data, never stale chunk physics or decorative colliders.</summary>
    public static class VoxelRaycast
    {
        public readonly struct Hit
        {
            public readonly Vector3Int Cell,Normal;public readonly float Distance;
            public Hit(Vector3Int cell,Vector3Int normal,float distance){Cell=cell;Normal=normal;Distance=distance;}
            public Vector3Int Adjacent=>Cell+Normal;
        }
        public static bool Trace(Vector3 origin,Vector3 direction,float reach,Func<int,int,int,ushort> sample,out Hit hit)
        {
            hit=default;if(direction.sqrMagnitude<.000001f||reach<=0)return false;
            direction.Normalize();var cell=Vector3Int.FloorToInt(origin);var normal=Vector3Int.zero;
            int sx=direction.x>0?1:-1,sy=direction.y>0?1:-1,sz=direction.z>0?1:-1;
            float dx=Mathf.Abs(direction.x)<.000001f?float.PositiveInfinity:Mathf.Abs(1/direction.x);
            float dy=Mathf.Abs(direction.y)<.000001f?float.PositiveInfinity:Mathf.Abs(1/direction.y);
            float dz=Mathf.Abs(direction.z)<.000001f?float.PositiveInfinity:Mathf.Abs(1/direction.z);
            float tx=float.IsInfinity(dx)?dx:(direction.x>0?cell.x+1-origin.x:origin.x-cell.x)*dx;
            float ty=float.IsInfinity(dy)?dy:(direction.y>0?cell.y+1-origin.y:origin.y-cell.y)*dy;
            float tz=float.IsInfinity(dz)?dz:(direction.z>0?cell.z+1-origin.z:origin.z-cell.z)*dz;
            float distance=0;
            for(int step=0;step<128&&distance<=reach;step++)
            {
                ushort block=sample(cell.x,cell.y,cell.z);
                if(block==11||block==15)
                {
                    float start=distance,limit=Mathf.Min(reach,Mathf.Min(tx,Mathf.Min(ty,tz)));
                    if(LestingCraft.Presentation.VoxelItemModels.TracePlaced(block,cell,origin+direction*start,direction,Mathf.Max(0,limit-start),out var shaped))
                    {hit=new Hit(cell,shaped.Normal==Vector3Int.zero?normal:shaped.Normal,start+shaped.Distance);return true;}
                }
                else if(block!=0&&block!=7&&block!=8){hit=new Hit(cell,normal,distance);return true;}
                if(tx<=ty&&tx<=tz){distance=tx;tx+=dx;cell.x+=sx;normal=new Vector3Int(-sx,0,0);}
                else if(ty<=tz){distance=ty;ty+=dy;cell.y+=sy;normal=new Vector3Int(0,-sy,0);}
                else {distance=tz;tz+=dz;cell.z+=sz;normal=new Vector3Int(0,0,-sz);}
            }
            return false;
        }
        public static bool OverlapsPlayer(Vector3Int cell,Bounds player)
        {player.Expand(-.035f);return new Bounds((Vector3)cell+Vector3.one*.5f,Vector3.one).Intersects(player);}
        public static float MiningSeconds(bool creative,float hardness,float speed)=>creative?.24f:Mathf.Max(.18f,hardness*.6f/Mathf.Max(1,speed));
    }
}
