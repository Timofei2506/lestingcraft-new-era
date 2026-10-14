using System.Collections.Generic;

namespace LestingCraft.World
{
    /// <summary>
    /// Определение блока: чистые данные без привязки к рендеру и Unity.
    /// </summary>
    public readonly struct BlockDefinition
    {
        public readonly ushort Id;
        public readonly string Name;
        public readonly float Hardness;
        public readonly bool IsSolid;
        public readonly byte LightLevel;

        public BlockDefinition(ushort id, string name, float hardness, bool isSolid, byte lightLevel = 0)
        {
            Id = id;
            Name = name;
            Hardness = hardness;
            IsSolid = isSolid;
            LightLevel = lightLevel;
        }
    }

    /// <summary>
    /// Реестр блоков мира. Регистрация идемпотентна: повторная регистрация id перезаписывает определение.
    /// </summary>
    public static class BlockRegistry
    {
        public const ushort AirId = 0;
        public const ushort StoneId = 1;
        public const ushort DirtId = 2;
        public const ushort GrassId = 3;
        public const ushort WoodId = 4;
        public const ushort LeavesId = 5;
        public const ushort SandId = 6;
        public const ushort WaterId = 7;
        public const ushort LavaId = 8;
        public const ushort BedrockId = 9;

        private static readonly Dictionary<ushort, BlockDefinition> Blocks = new Dictionary<ushort, BlockDefinition>();

        static BlockRegistry()
        {
            Register(new BlockDefinition(AirId, "Air", 0f, isSolid: false));
            Register(new BlockDefinition(StoneId, "Stone", 1.5f, isSolid: true));
            Register(new BlockDefinition(DirtId, "Dirt", 0.5f, isSolid: true));
            Register(new BlockDefinition(GrassId, "Grass", 0.6f, isSolid: true));
            Register(new BlockDefinition(WoodId, "Wood", 2f, isSolid: true));
            Register(new BlockDefinition(LeavesId, "Leaves", 0.2f, isSolid: true));
            Register(new BlockDefinition(SandId, "Sand", 0.5f, isSolid: true));
            Register(new BlockDefinition(WaterId, "Water", 0f, isSolid: false));
            Register(new BlockDefinition(LavaId, "Lava", 0f, isSolid: false, lightLevel: 15));
            Register(new BlockDefinition(BedrockId, "Bedrock", float.PositiveInfinity, isSolid: true));
        }

        public static void Register(BlockDefinition definition)
        {
            Blocks[definition.Id] = definition;
        }

        public static BlockDefinition Get(ushort id)
        {
            return Blocks.TryGetValue(id, out var definition) ? definition : Blocks[AirId];
        }

        public static bool IsSolid(ushort id)
        {
            return Get(id).IsSolid;
        }

        public static bool IsAir(ushort id)
        {
            return id == AirId;
        }
    }
}
