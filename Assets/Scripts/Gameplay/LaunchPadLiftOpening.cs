using System;

namespace LestingCraft.Gameplay
{
    /// <summary>Functional openings in a copy of the owner-approved LaunchPad, never
    /// in the source. The crew lift rises between the pad's structural rails; a small
    /// doorway through the tower partition connects the deck to the orbiter hatch.
    /// Pad and Shuttle use the same X/Z axes; shuttle origin is pad Y=2,
    /// while the ground walking surface beside the lift is pad Y=5.</summary>
    public static class LaunchPadLiftOpening
    {
        public const int CenterX=47,CenterZ=45;
        public static bool Remove(int x,int y,int z)
        {
            // Three deck columns over the moving 1.9 x 1.9 m platform.
            bool liftWell=y==29&&x>=-21&&x<=-19&&z>=2&&z<=3;
            // Two-metre-high opening in the existing tower partition at X=-13.
            bool towerDoor=x==-13&&y>=30&&y<=31&&z>=2&&z<=3;
            return liftWell||towerDoor;
        }
        public static SpaceVoxelAsset Make(SpaceVoxelAsset approved)
        {
            if(approved==null||approved.X!=94||approved.Y!=40||approved.Z!=90)
                throw new ArgumentException("Expected the owner-approved LaunchPad voxel dimensions");
            return approved.RemoveCells((x,y,z)=>Remove(x-CenterX,y,z-CenterZ));
        }
    }
}
