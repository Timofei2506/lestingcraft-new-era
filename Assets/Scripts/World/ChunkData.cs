using System;

namespace LestingCraft.World
{
    /// <summary>
    /// Данные одного чанка 16×256×16 блоков. Чистый C#-класс без UnityEngine —
    /// тестируется в EditMode и в будущем уходит целиком в Burst-джобы генерации.
    /// </summary>
    public sealed class ChunkData
    {
        public const int SizeX = 16;
        public const int SizeY = 256;
        public const int SizeZ = 16;

        private readonly ushort[] _blocks;

        public ChunkData()
        {
            _blocks = new ushort[SizeX * SizeY * SizeZ];
        }

        public ChunkData(ushort[] blocks)
        {
            if (blocks == null || blocks.Length != BlockCount) throw new ArgumentException("Invalid chunk size");
            _blocks = blocks;
        }

        public static int BlockCount => SizeX * SizeY * SizeZ;

        public static bool InBounds(int x, int y, int z)
        {
            return x >= 0 && x < SizeX && y >= 0 && y < SizeY && z >= 0 && z < SizeZ;
        }

        public static int IndexOf(int x, int y, int z)
        {
            return x + SizeX * (z + SizeZ * y);
        }

        public ushort GetBlock(int x, int y, int z)
        {
            EnsureInBounds(x, y, z);
            return _blocks[IndexOf(x, y, z)];
        }

        public void SetBlock(int x, int y, int z, ushort blockId)
        {
            EnsureInBounds(x, y, z);
            _blocks[IndexOf(x, y, z)] = blockId;
        }

        /// <summary>Заполняет весь чанк одним блоком.</summary>
        public void Fill(ushort blockId)
        {
            for (var i = 0; i < _blocks.Length; i++)
            {
                _blocks[i] = blockId;
            }
        }

        /// <summary>Считает непустые блоки (всё, кроме воздуха).</summary>
        public int CountNonAir()
        {
            var count = 0;
            for (var i = 0; i < _blocks.Length; i++)
            {
                if (_blocks[i] != BlockRegistry.AirId)
                {
                    count++;
                }
            }

            return count;
        }

        private static void EnsureInBounds(int x, int y, int z)
        {
            if (!InBounds(x, y, z))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    $"Block ({x}, {y}, {z}) is outside chunk {SizeX}x{SizeY}x{SizeZ}");
            }
        }
    }
}
