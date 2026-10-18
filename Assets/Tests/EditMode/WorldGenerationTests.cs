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
        [TestCase(Dimension.Lestingia)] [TestCase(Dimension.Ashlands)] [TestCase(Dimension.EchoCrypt)]
        public void GroundedDimensionsHaveProtectedFloor(Dimension dimension)
        {for(int x=-30;x<30;x+=5)Assert.AreEqual(BlockRegistry.BedrockId,TerrainGenerator.Sample(x,0,8,2506,dimension));}
        [TestCase(Dimension.Skyreach)] [TestCase(Dimension.ChaosRift)]
        public void FloatingDimensionsHaveAirBelowStartingIsland(Dimension dimension)
        {Assert.AreEqual(0,TerrainGenerator.Sample(0,0,0,2506,dimension));Assert.AreNotEqual(0,TerrainGenerator.Sample(0,TerrainGenerator.Height(0,0,2506,dimension),0,2506,dimension));}
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
        {for(int d=0;d<5;d++)for(int y=0;y<256;y++)Assert.LessOrEqual(TerrainGenerator.Sample(-13,y,7,-2147483648,(Dimension)d),14);}
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

namespace LestingCraft.Tests
{
    // Версия 2 генератора Лестингии: биомы, океан, реки, руда по глубине.
    public class WorldGenerationV2Tests
    {
        [Test] public void AllLandAndOceanBiomesArePresent()
        {
            var counts = new int[7];
            for (int x = -600; x < 600; x += 6)
            for (int z = -600; z < 600; z += 6)
                counts[(int)TerrainGenerator.Column(x, z, 2506, Dimension.Lestingia).Biome]++;
            for (int b = 0; b < counts.Length; b++) Assert.Greater(counts[b], 0, "biome " + b + " missing");
        }

        [Test] public void BelowSeaLevelOverOceanIsWater()
        {
            bool found = false;
            for (int x = -600; x < 600 && !found; x += 6)
            for (int z = -600; z < 600 && !found; z += 6)
            {
                var col = TerrainGenerator.Column(x, z, 2506, Dimension.Lestingia);
                if (col.Biome != Biome.Ocean) continue;
                found = true;
                Assert.AreEqual(BlockRegistry.WaterId, TerrainGenerator.Sample(x, TerrainGenerator.SeaLevel, z, 2506, Dimension.Lestingia));
            }
            Assert.IsTrue(found, "no ocean column found");
        }

        [Test] public void OreIsMoreFrequentDeepUnderground()
        {
            int deepOre = 0, deepStone = 0, shallowOre = 0, shallowStone = 0;
            for (int x = -200; x < 200; x += 3)
            for (int z = -200; z < 200; z += 3)
            for (int y = 2; y < 16; y++)
            {
                ushort id = TerrainGenerator.Sample(x, y, z, 2506, Dimension.Lestingia);
                if (id == BlockRegistry.OreId) deepOre++; else if (id == BlockRegistry.StoneId) deepStone++;
            }
            for (int x = -200; x < 200; x += 3)
            for (int z = -200; z < 200; z += 3)
            for (int y = 60; y < 90; y++)
            {
                ushort id = TerrainGenerator.Sample(x, y, z, 2506, Dimension.Lestingia);
                if (id == BlockRegistry.OreId) shallowOre++; else if (id == BlockRegistry.StoneId) shallowStone++;
            }
            Assert.Greater((double)deepOre / (deepOre + deepStone + 1), (double)shallowOre / (shallowOre + shallowStone + 1));
        }

        [Test] public void OtherDimensionsKeepVersionOneHeights()
        {
            // Измерения кроме Лестингии не меняются: высота совпадает с формулой версии 1.
            int x = 17, z = -9;
            int expected = 26 + (int)(TerrainGenerator.Noise(x / 55f, z / 55f, 2506) * 38 + TerrainGenerator.Noise(x / 13f, z / 13f, 2506 ^ 731) * 6);
            Assert.AreEqual(expected, TerrainGenerator.Height(x, z, 2506, Dimension.Ashlands));
        }
    }
}
