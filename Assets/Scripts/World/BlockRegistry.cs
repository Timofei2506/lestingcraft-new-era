using LestingCraft.Gameplay;

namespace LestingCraft.World
{
    public readonly struct BlockDefinition
    {
        public readonly ushort Id;
        public readonly string Name;
        public readonly bool Solid;
        public readonly byte LightEmission;
        public readonly float Hardness;

        public BlockDefinition(ushort id, string name, bool solid, float hardness, byte lightEmission = 0)
        {
            Id = id;
            Name = name;
            Solid = solid;
            Hardness = hardness;
            LightEmission = lightEmission;
        }
    }

    public static class BlockRegistry
    {
        public const ushort AirId = 0, StoneId = 1, DirtId = 2, GrassId = 3, WoodId = 4, LeavesId = 5;
        public const ushort SandId = 6, WaterId = 7, LavaId = 8, BedrockId = 9, BasaltId = 10;
        public const ushort CrystalId = 11, AetherStoneId = 12, OreId = 13, SnowId = 14, CampfireId = 15;
        public const ushort WorkbenchId = 16, FurnaceId = 17, PlanksId = 18;
        public const ushort TorchBlockId = 54, BedBlockId = 55, LanternBlockId = 56, BookshelfId = 57;
        public const ushort CarvedOakId = 58, ShinglesId = 59, CherryLeavesId = 60, GlowShroomId = 61;
        public const ushort RedMesaId = 62, GlacialIceId = 63, MagmaBasaltId = 64, LavenderTurfId = 65;
        public const ushort TitaniumHullId = 66, AndromedaCrystalId = 67, DreamMonolithId = 68, DreamStoneId = 69;

        private static readonly BlockDefinition[] Definitions = BuildDefinitions();

        static BlockDefinition[] BuildDefinitions()
        {
            var list = new BlockDefinition[ExpandedContent.MaxBlock + 1];
            list[0] = new BlockDefinition(0, "Air", false, 0);
            list[1] = new BlockDefinition(1, "Stone", true, 1.8f);
            list[2] = new BlockDefinition(2, "Dirt", true, .65f);
            list[3] = new BlockDefinition(3, "Grass", true, .7f);
            list[4] = new BlockDefinition(4, "Wood", true, 1.3f);
            list[5] = new BlockDefinition(5, "Leaves", true, .25f);
            list[6] = new BlockDefinition(6, "Sand", true, .6f);
            list[7] = new BlockDefinition(7, "Water", false, 0);
            list[8] = new BlockDefinition(8, "Lava", false, 0, 15);
            list[9] = new BlockDefinition(9, "Bedrock", true, -1);
            list[10] = new BlockDefinition(10, "Basalt", true, 2.4f);
            list[11] = new BlockDefinition(11, "Crystal", true, 1.5f, 10);
            list[12] = new BlockDefinition(12, "Aether Stone", true, 2.0f, 4);
            list[13] = new BlockDefinition(13, "Ore", true, 2.6f);
            list[14] = new BlockDefinition(14, "Snow", true, .45f);
            list[15] = new BlockDefinition(15, "Campfire", true, .9f, 14);
            list[16] = new BlockDefinition(16, "Workbench", true, 1.4f);
            list[17] = new BlockDefinition(17, "Furnace", true, 2.2f, 6);
            list[18] = new BlockDefinition(18, "Planks", true, 1.2f);
            foreach (var b in ExpandedContent.Blocks)
            {
                byte light = b.Id == 56 ? (byte)15 :
                             b.Id == 54 ? (byte)14 :
                             b.Id == 67 ? (byte)13 :
                             b.Id == 61 ? (byte)12 :
                             b.Id == 64 ? (byte)9 : (byte)0;
                list[b.Id] = new BlockDefinition((ushort)b.Id, b.Name, true, b.Hardness, light);
            }
            return list;
        }

        public static BlockDefinition Get(ushort id) => id < Definitions.Length ? Definitions[id] : Definitions[0];
        public static bool IsAir(ushort id) => id == 0;
        public static bool IsSolid(ushort id) => id < Definitions.Length && Definitions[id].Solid;
        public static bool IsCustomPlacedModel(ushort id) =>
            id == CampfireId || id == CrystalId || id == TorchBlockId || id == BedBlockId || id == LanternBlockId;
        public static bool Occludes(ushort id) =>
            IsSolid(id) && id != CampfireId && id != CrystalId && id != 44 &&
            id != TorchBlockId && id != BedBlockId && id != LanternBlockId;
    }
}
