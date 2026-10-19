using NUnit.Framework;
using Unity.Collections;
using LestingCraft.World;
using LestingCraft.Runtime;

namespace LestingCraft.Tests
{
    public class WorldGenerationTests
    {
        [Test] public void SeedAndCoordinatesAreDeterministic()
        {
            for(int x=-20;x<=20;x+=4)for(int z=-20;z<=20;z+=4)for(int y=0;y<100;y+=7)
                Assert.AreEqual(TerrainGenerator.Sample(x,y,z,2506,Dimension.Lestingia),TerrainGenerator.Sample(x,y,z,2506,Dimension.Lestingia));
        }
        [Test] public void DifferentSeedsChangeHeight()
        {
            bool changed=false;for(int x=-50;x<50;x++)if(TerrainGenerator.Height(x,17,1,Dimension.Lestingia)!=TerrainGenerator.Height(x,17,902,Dimension.Lestingia))changed=true;
            Assert.IsTrue(changed);
        }
        [TestCase(Dimension.Lestingia)]
        public void GroundedDimensionsHaveProtectedFloor(Dimension dimension)
        {for(int x=-30;x<30;x+=5)Assert.AreEqual(BlockRegistry.BedrockId,TerrainGenerator.Sample(x,0,8,2506,dimension));}

        [Test] public void OutsideHeightReturnsAir()
        {Assert.AreEqual(0,TerrainGenerator.Sample(3,-1,3,2506,Dimension.Lestingia));Assert.AreEqual(0,TerrainGenerator.Sample(3,256,3,2506,Dimension.Lestingia));}
        [Test] public void ColumnJobMatchesSamplerIncludingNegativeChunkCoordinates()
        {
            using(var blocks=new NativeArray<ushort>(ChunkData.BlockCount,Allocator.TempJob))
            {
                var job=new GenerateChunkJob{OriginX=-16,OriginZ=-32,Seed=2506,Dimension=Dimension.Lestingia,Blocks=blocks};
                for(int c=0;c<256;c++)job.Execute(c);
                for(int x=0;x<16;x+=3)for(int z=0;z<16;z+=3)for(int y=0;y<120;y++)
                    Assert.AreEqual(TerrainGenerator.Sample(x-16,y,z-32,2506,Dimension.Lestingia),blocks[ChunkData.IndexOf(x,y,z)]);
            }
        }
        [TestCase(-1,-1)] [TestCase(-16,-1)] [TestCase(-17,-2)] [TestCase(0,0)] [TestCase(15,0)] [TestCase(16,1)]
        public void ChunkMappingUsesFloorForNegativeCoordinates(int world,int chunk)
        {Assert.AreEqual(chunk,VoxelWorld.ChunkCoordinate(world));}
        [Test] public void TerrainNeverProducesUnknownBlocks()
        {for(int d=0;d<1;d++)for(int y=0;y<256;y++)Assert.LessOrEqual(TerrainGenerator.Sample(-13,y,7,-2147483648,(Dimension)d),14);}
    }
    public class ChunkMesherTests
    {
        [Test] public void EmptyChunkHasNoFaces()
        {Assert.AreEqual(0,ChunkMesher.Build(new ChunkData(),0,0,(x,y,z)=>0).FaceCount);}
        [Test] public void SingleBlockHasSixFacesAndCollision()
        {var c=new ChunkData();c.SetBlock(4,4,4,1);var m=ChunkMesher.Build(c,0,0,(x,y,z)=>0);Assert.AreEqual(6,m.FaceCount);Assert.AreEqual(36,m.ColliderTriangles.Count);}
        [Test] public void AdjacentBlocksCullSharedFaces()
        {var c=new ChunkData();c.SetBlock(4,4,4,1);c.SetBlock(5,4,4,1);Assert.AreEqual(10,ChunkMesher.Build(c,0,0,(x,y,z)=>0).FaceCount);}
        [Test] public void NeighborChunkCullsBoundaryFace()
        {var c=new ChunkData();c.SetBlock(15,4,4,1);Assert.AreEqual(5,ChunkMesher.Build(c,0,0,(x,y,z)=>(ushort)(x==16&&y==4&&z==4?1:0)).FaceCount);}
        [Test] public void WaterDoesNotHaveSolidCollision()
        {var c=new ChunkData();c.SetBlock(4,4,4,7);var m=ChunkMesher.Build(c,0,0,(x,y,z)=>0);Assert.AreEqual(6,m.FaceCount);Assert.AreEqual(0,m.ColliderTriangles.Count);}
    }
}
