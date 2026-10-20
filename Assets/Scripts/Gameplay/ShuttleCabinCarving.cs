using System;

namespace LestingCraft.Gameplay
{
    /// <summary>
    /// Derives an accessible vertical cabin from the immutable owner-approved Shuttle model.
    /// The authored resource is never edited. The only outer opening is a 2 m hatch facing
    /// the launch tower at local X=-9, Y=28..30, Z=2..3; the rest of the outside shell,
    /// small cockpit windows, external tank and both boosters retain their original voxels.
    /// A closed, solid hatch in the interior assembly covers that opening on the pad.
    /// Coordinates are APPROVED MODEL voxel indices (X origin=31, Z origin=30), not
    /// the old horizontal /shuttle-demo prototype's cabin coordinates.
    /// </summary>
    public static class ShuttleCabinCarving
    {
        public const int ModelCenterX=31, ModelCenterZ=30;
        public const int EntryY=28, CockpitY=43;
        public static bool Remove(int x,int y,int z)
        {
            // Two decks and a 3x3 vertical climbing shaft. Keep at least one voxel
            // between the walking volumes and every approved exterior face except hatch.
            bool lower=y>=28&&y<=33&&x>=-2&&x<=2&&z>=1&&z<=5;
            bool ladder=y>=34&&y<=41&&x>=-1&&x<=1&&z>=2&&z<=4;
            bool cockpit=y>=42&&y<=45&&x>=-2&&x<=2&&z>=2&&z<=5;
            bool hatch=y>=28&&y<=30&&x>=-9&&x<=-3&&z>=2&&z<=3;
            return lower||ladder||cockpit||hatch;
        }
        public static SpaceVoxelAsset Make(SpaceVoxelAsset approved)
        {
            if(approved==null||approved.X!=62||approved.Y!=78||approved.Z!=60)
                throw new ArgumentException("Expected the owner-approved Shuttle voxel dimensions");
            return approved.RemoveCells((x,y,z)=>Remove(x-ModelCenterX,y,z-ModelCenterZ));
        }
    }

    /// <summary>Walk volumes in the approved VERTICAL orbiter, in local metres, not the
    /// old horizontal QA cabin. Actual movement must be applied by a collision controller:
    /// this validator checks incremental steps only and never changes a player's position.</summary>
    public static class VerticalShuttleWalk
    {
        public const int SeatCount=2;
        // At this side of the pad, ground walking surface is Y=5; deck top Y=30.
        // With shuttle origin at pad Y=2, ground is local Y=3 and entry is Y=28.
        // X=-19.8 avoids the approved orbiter's mid-height booster (X>=-18).
        public const float LiftTravel=25f;
        public const float LiftGroundY=3f;
        public const float LiftX=-19.8f;
        public static bool Inside(double x,double y,double z)
        {
            if(double.IsNaN(x)||double.IsNaN(y)||double.IsNaN(z))return false;
            bool towerBridge=x>=-12&&x<=-9&&y>=28&&y<=31&&z>=2&&z<=4;
            bool entry=x>=-9&&x<=-2&&y>=28&&y<=31&&z>=2&&z<=4;
            bool lower=x>=-2&&x<=3&&y>=28&&y<=34&&z>=1&&z<=6;
            bool shaft=x>=-1&&x<=2&&y>=33&&y<=42&&z>=2&&z<=5;
            bool cockpit=x>=-2&&x<=3&&y>=42&&y<=46&&z>=2&&z<=6;
            return towerBridge||entry||lower||shaft||cockpit;
        }
        public static bool CanStep(double x,double y,double z,double nx,double ny,double nz,bool hatchOpen)
        {
            if(!Inside(x,y,z)||!Inside(nx,ny,nz))return false;
            double dx=nx-x,dy=ny-y,dz=nz-z;
            if(dx*dx+dy*dy+dz*dz>.55*.55)return false;
            if(!hatchOpen&&((x< -9&&nx>= -9)||(x>= -9&&nx< -9)))return false;
            // A vertical movement across decks must actually stay inside the physical ladder shaft.
            if(Math.Abs(dy)>.12&&(y>=33||ny>=33)&&(x< -1||x>2||nx< -1||nx>2||z<2||z>5||nz<2||nz>5))return false;
            for(int i=1;i<=4;i++)if(!Inside(x+dx*i/4,y+dy*i/4,z+dz*i/4))return false;
            return true;
        }
        public static void Seat(int index,out double x,out double y,out double z)
        {
            if(index<0||index>=SeatCount)throw new ArgumentOutOfRangeException("index");
            x=index==0?-.4:1.4;y=42;z=5.35;
        }
    }
}
