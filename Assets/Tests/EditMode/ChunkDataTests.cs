using System;
using NUnit.Framework;
using LestingCraft.World;

namespace LestingCraft.Tests
{
    public class ChunkDataTests
    {
        [Test]
        public void SetGet_Roundtrip_PersistsBlockId()
        {
            var chunk = new ChunkData();

            chunk.SetBlock(1, 2, 3, BlockRegistry.StoneId);

            Assert.AreEqual(BlockRegistry.StoneId, chunk.GetBlock(1, 2, 3));
            Assert.AreEqual(BlockRegistry.AirId, chunk.GetBlock(0, 0, 0), "Соседние ячейки не должны меняться");
        }

        [Test]
        public void OutOfBounds_Throws()
        {
            var chunk = new ChunkData();

            Assert.Throws<ArgumentOutOfRangeException>(() => chunk.SetBlock(-1, 0, 0, BlockRegistry.StoneId));
            Assert.Throws<ArgumentOutOfRangeException>(() => chunk.SetBlock(0, ChunkData.SizeY, 0, BlockRegistry.StoneId));
            Assert.Throws<ArgumentOutOfRangeException>(() => chunk.GetBlock(0, 0, ChunkData.SizeZ));
        }

        [Test]
        public void Fill_And_CountNonAir()
        {
            var chunk = new ChunkData();
            Assert.AreEqual(0, chunk.CountNonAir(), "Новый чанк пуст");

            chunk.Fill(BlockRegistry.DirtId);
            Assert.AreEqual(ChunkData.BlockCount, chunk.CountNonAir());

            chunk.SetBlock(0, 0, 0, BlockRegistry.AirId);
            Assert.AreEqual(ChunkData.BlockCount - 1, chunk.CountNonAir());
        }

        [Test]
        public void IndexOf_IsUnique_ForAllBlocks()
        {
            var seen = new bool[ChunkData.BlockCount];
            for (var y = 0; y < ChunkData.SizeY; y++)
            for (var z = 0; z < ChunkData.SizeZ; z++)
            for (var x = 0; x < ChunkData.SizeX; x++)
            {
                var index = ChunkData.IndexOf(x, y, z);
                Assert.IsFalse(seen[index], $"Коллизия индекса у ({x}, {y}, {z})");
                seen[index] = true;
            }
        }
    }

    public class BlockRegistryTests
    {
        [Test]
        public void Air_IsNotSolid()
        {
            Assert.IsTrue(BlockRegistry.IsAir(BlockRegistry.AirId));
            Assert.IsFalse(BlockRegistry.IsSolid(BlockRegistry.AirId));
        }

        [Test]
        public void Stone_IsSolid()
        {
            Assert.IsTrue(BlockRegistry.IsSolid(BlockRegistry.StoneId));
            Assert.IsFalse(BlockRegistry.IsAir(BlockRegistry.StoneId));
        }

        [Test]
        public void UnknownId_FallsBackToAir()
        {
            Assert.IsTrue(BlockRegistry.IsAir(BlockRegistry.Get(9999).Id));
        }
    }
}
