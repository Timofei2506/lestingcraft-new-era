using System;

namespace LestingCraft.World
{
    /// <summary>
    /// The Paradise / Celestial Eden Dimension (Dimension.Paradise = 17):
    /// Sunlit emerald-and-marble celestial gardens, crystal-azure ambrosia seas,
    /// blossoming sakura & golden-fruit groves, classical marble sanctuaries,
    /// floating cloud isles, and sacred return fountains.
    /// Pure deterministic C# with zero Unity dependencies for headless verification.
    /// </summary>
    public static class ParadiseTerrain
    {
        public const int SeaLevel = 38;
        public const int SpawnY = 52;

        public const ushort BedrockId = 9;
        public const ushort GrassId = 3;
        public const ushort DirtId = 2;
        public const ushort WoodId = 4;
        public const ushort LeavesId = 5;
        public const ushort WaterId = 7;
        public const ushort CrystalId = 11;
        public const ushort AetherStoneId = 12;
        public const ushort SnowId = 14;
        public const ushort GoldOreId = 22;
        public const ushort QuartzOreId = 25;
        public const ushort MarbleId = 30;
        public const ushort PolishedMarbleId = 42;
        public const ushort DiamondBlockId = 48;
        public const ushort LanternId = 56;
        public const ushort CarvedOakId = 58;
        public const ushort SakuraLeavesId = 60;
        public const ushort LavenderTurfId = 65;
        public const ushort AndromedaCrystalId = 67;
        public const ushort DandelionId = 94;
        public const ushort PoppyId = 95;
        public const ushort CornflowerId = 96;
        public const ushort LavenderFlowerId = 97;

        static int FloorDiv(int a, int b) => a >= 0 ? a / b : (a - b + 1) / b;

        static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(seed ^ (x * 374761393) ^ (y * 668265263) ^ (z * 2147483647));
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        static float N(float x, float z, int seed) => MoonTerrain.ValueNoise(x, z, seed);
        static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);

        /// <summary>
        /// 0 = Emerald Eden Gardens, 1 = Marble Terraces of Light,
        /// 2 = Celestial Crystal Groves, 3 = Radiant Cloud Meadows.
        /// </summary>
        public static int BiomeAt(int x, int z, int seed)
        {
            float a = N(x * 0.0085f, z * 0.0085f, seed ^ 0x51A7);
            float b = N(x * 0.0095f + 37f, z * 0.0095f - 19f, seed ^ 0x2B9D);
            if (a < 0.44f && b < 0.56f) return 0;
            if (a >= 0.44f && b < 0.52f) return 1;
            if (a < 0.52f) return 2;
            return 3;
        }

        public static bool IsSpawnSanctuary(int x, int z) => x * x + z * z <= 15 * 15;

        /// <summary>Sacred Return Fountain threshold at (0, -8) or sanctuary portals.</summary>
        public static bool IsExitThreshold(int x, int z, int seed)
        {
            if (Math.Abs(x) <= 1 && z >= -9 && z <= -7) return true;
            ExitPortal(x, z, seed, out int px, out int pz);
            return Math.Abs(x - px) <= 1 && Math.Abs(z - pz) <= 1;
        }

        public static void ExitPortal(int nearX, int nearZ, int seed, out int px, out int pz)
        {
            if (Math.Abs(nearX) < 96 && Math.Abs(nearZ) < 96)
            {
                px = 0;
                pz = -8;
                return;
            }
            int cx = FloorDiv(nearX, 192), cz = FloorDiv(nearZ, 192);
            uint h = Hash(cx, 77, cz, seed ^ 0x7E31);
            px = cx * 192 + 48 + (int)(h % 96u);
            pz = cz * 192 + 48 + (int)((h >> 8) % 96u);
        }

        /// <summary>Deterministic Classical Marble Sanctuary Rotunda per 192x192 sector (guaranteed at (72, 56) in cell (0,0)).</summary>
        public static bool TryGetSanctuary(int cellX, int cellZ, int seed, out int sx, out int sz)
        {
            if (cellX == 0 && cellZ == 0)
            {
                sx = 72;
                sz = 56;
                return true;
            }
            uint h = Hash(cellX, 31, cellZ, seed ^ 0x6A19);
            if ((h % 10u) >= 6u)
            {
                sx = 0;
                sz = 0;
                return false;
            }
            sx = cellX * 192 + 44 + (int)((h >> 4) % 104u);
            sz = cellZ * 192 + 44 + (int)((h >> 12) % 104u);
            return true;
        }

        public static int Height(int x, int z, int seed)
        {
            return FloorTop(x, z, seed);
        }

        public static int FloorTop(int x, int z, int seed)
        {
            int r2 = x * x + z * z;
            if (r2 <= 14 * 14) return SpawnY;

            // Check nearby Classical Marble Sanctuary terrace
            int cx = FloorDiv(x, 192), cz = FloorDiv(z, 192);
            if (TryGetSanctuary(cx, cz, seed, out int sx, out int sz))
            {
                int dx = x - sx, dz = z - sz;
                if (dx * dx + dz * dz <= 16 * 16) return 54;
            }

            float broad = N(x * 0.011f, z * 0.011f, seed ^ 0x18F3);
            float mid = N(x * 0.029f, z * 0.029f, seed ^ 0x3D71);
            float fine = N(x * 0.068f, z * 0.068f, seed ^ 0x74C5);
            int biome = BiomeAt(x, z, seed);

            float h = 34f + broad * 30f + (mid - 0.5f) * 14f + (fine - 0.5f) * 5f;
            if (biome == 1)
            {
                // Stepped white-marble terraces
                h = 42f + (float)Math.Floor((broad * 26f + mid * 8f) / 3f) * 3f;
            }
            else if (biome == 3)
            {
                // Buoyant cloud-meadow plateaus
                h = 46f + broad * 24f + mid * 8f;
            }

            // Smooth blend toward SpawnY around central sanctuary
            if (r2 < 28 * 28)
            {
                float dist = (float)Math.Sqrt(r2);
                float t = Clamp((dist - 14f) / 14f, 0f, 1f);
                t = t * t * (3f - 2f * t);
                h = SpawnY * (1f - t) + h * t;
            }

            int hi = (int)Math.Round(h);
            return hi < 26 ? 26 : (hi > 88 ? 88 : hi);
        }

        static ushort FlowerForHash(uint h)
        {
            switch (h & 3u)
            {
                case 0u: return DandelionId;
                case 1u: return PoppyId;
                case 2u: return CornflowerId;
                default: return LavenderFlowerId;
            }
        }

        static bool TrySampleSanctuaryBlock(int x, int y, int z, int seed, out ushort block)
        {
            block = 0;
            int cx = FloorDiv(x, 192), cz = FloorDiv(z, 192);
            if (!TryGetSanctuary(cx, cz, seed, out int sx, out int sz)) return false;
            int dx = x - sx, dz = z - sz;
            int r2 = dx * dx + dz * dz;
            if (r2 > 15 * 15 || y < 48 || y > 72) return false;

            int baseY = 54;
            if (y <= baseY)
            {
                block = y == baseY
                    ? ((r2 <= 9) ? CrystalId : ((dx + dz) % 4 == 0 ? MarbleId : PolishedMarbleId))
                    : MarbleId;
                return true;
            }
            int ry = y - baseY;
            // Central Sacred Diamond & Crystal Altar
            if (Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1)
            {
                if (ry == 1) { block = PolishedMarbleId; return true; }
                if (ry == 2 && dx == 0 && dz == 0) { block = DiamondBlockId; return true; }
                if (ry == 3 && dx == 0 && dz == 0) { block = LanternId; return true; }
                block = 0;
                return ry <= 10;
            }
            // Ring of 12 Classical Fluted Marble Columns around radius ~11..12
            if (r2 >= 110 && r2 <= 148 && ((Math.Abs(dx) % 4 == 0 && Math.Abs(dz) >= 9) || (Math.Abs(dz) % 4 == 0 && Math.Abs(dx) >= 9)))
            {
                if (ry <= 8) { block = ry == 8 ? GoldOreId : PolishedMarbleId; return true; }
            }
            // Upper Marble Entablature & Open Ocular Dome (ry = 9..13)
            if (ry == 9 && r2 >= 96 && r2 <= 165) { block = PolishedMarbleId; return true; }
            if (ry >= 10 && ry <= 13)
            {
                int domeMin = 96 - (ry - 9) * 18;
                int domeMax = 148 - (ry - 9) * 18;
                if (r2 >= domeMin && r2 <= domeMax)
                {
                    block = ry == 13 ? AndromedaCrystalId : MarbleId;
                    return true;
                }
            }
            // Inner flower ring around the altar
            if (ry == 1 && r2 >= 16 && r2 <= 28 && ((dx + dz) & 1) == 0)
            {
                block = FlowerForHash(Hash(x, 1, z, seed));
                return true;
            }
            block = 0;
            return ry <= 13;
        }

        static bool TrySampleEdenTree(int x, int y, int z, int seed, out ushort block)
        {
            block = 0;
            int gx = FloorDiv(x, 10), gz = FloorDiv(z, 10);
            uint h = Hash(gx, 9, gz, seed ^ 0x4C2E);
            if ((h % 10u) >= 5u) return false;
            int tx = gx * 10 + 3 + (int)((h >> 4) % 4u);
            int tz = gz * 10 + 3 + (int)((h >> 8) % 4u);
            if (tx * tx + tz * tz <= 18 * 18) return false;

            int biome = BiomeAt(tx, tz, seed);
            if (biome == 1 || biome == 3) return false;
            int ground = FloorTop(tx, tz, seed);
            if (ground <= SeaLevel + 1) return false;

            int dx = x - tx, dz = z - tz, ry = y - ground;
            if (Math.Abs(dx) > 3 || Math.Abs(dz) > 3 || ry < 1 || ry > 9) return false;

            bool sakura = ((h >> 12) & 1u) == 0u;
            ushort leaves = sakura ? SakuraLeavesId : LeavesId;
            ushort trunk = sakura ? WoodId : CarvedOakId;
            if (dx == 0 && dz == 0 && ry <= 6) { block = trunk; return true; }
            if (ry >= 4 && ry <= 8)
            {
                int rad = ry == 8 ? 1 : (ry == 4 ? 2 : 3);
                if (Math.Abs(dx) + Math.Abs(dz) <= rad + 1 && Math.Abs(dx) <= rad && Math.Abs(dz) <= rad)
                {
                    if (ry == 4 && Math.Abs(dx) == 1 && dz == 0 && (h & 7u) == 0u)
                        block = LanternId;
                    else
                        block = leaves;
                    return true;
                }
            }
            return false;
        }

        static bool TrySampleFloatingCloudIsle(int x, int y, int z, int seed, out ushort block)
        {
            block = 0;
            if (y < 94 || y > 112) return false;
            if (x * x + z * z < 34 * 34) return false; // Keep central spawn sanctuary open to direct golden sky

            int cx = FloorDiv(x, 96), cz = FloorDiv(z, 96);
            uint h = Hash(cx, 53, cz, seed ^ 0x91D2);
            if ((h % 10u) >= 5u) return false;
            int ix = cx * 96 + 32 + (int)((h >> 4) % 32u);
            int iz = cz * 96 + 32 + (int)((h >> 10) % 32u);
            int dx = x - ix, dz = z - iz;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            float radius = 11f + (h % 5u);
            if (dist > radius) return false;

            float norm = 1f - dist / radius;
            int deckY = 102 + (int)(N(x * 0.09f, z * 0.09f, seed) * 3f);
            int bottomY = deckY - 1 - (int)(norm * 5f);
            if (y == deckY)
            {
                block = dist < radius * 0.65f ? GrassId : SnowId;
                return true;
            }
            if (y == deckY + 1 && dist < radius * 0.5f && (Hash(x, y, z, seed) % 7u) == 0u)
            {
                block = FlowerForHash(Hash(x, 2, z, seed));
                return true;
            }
            if (y >= bottomY && y < deckY)
            {
                block = y >= deckY - 1 ? PolishedMarbleId : SnowId;
                return true;
            }
            return false;
        }

        public static ushort Block(int x, int y, int z, int seed, int floorTop)
        {
            if (y <= 0) return BedrockId;
            if (y >= 256) return 0;

            // Floating Upper Cloud Isles
            if (y >= 94 && TrySampleFloatingCloudIsle(x, y, z, seed, out ushort cloudBlock))
                return cloudBlock;

            // Central Spawn Sanctuary & Sacred Return Fountain around (0, 0)
            int r2 = x * x + z * z;
            if (r2 <= 14 * 14 && y <= SpawnY + 10)
            {
                // Sacred Return Fountain at (0, -8)
                if (Math.Abs(x) <= 2 && z >= -10 && z <= -6)
                {
                    bool innerBasin = Math.Abs(x) <= 1 && z >= -9 && z <= -7;
                    if (y < SpawnY - 1) return MarbleId;
                    if (y == SpawnY - 1) return innerBasin ? AndromedaCrystalId : PolishedMarbleId;
                    if (y == SpawnY) return innerBasin ? WaterId : PolishedMarbleId;
                    if (y >= SpawnY + 1 && y <= SpawnY + 4 && Math.Abs(x) == 2 && (z == -10 || z == -6))
                        return y == SpawnY + 4 ? LanternId : PolishedMarbleId;
                    if (y == SpawnY + 1 && !innerBasin && (Math.Abs(x) == 2 || z == -10 || z == -6))
                        return FlowerForHash((uint)(Math.Abs(x * 3 + z)));
                    return 0;
                }
                if (y < SpawnY) return y >= SpawnY - 2 ? PolishedMarbleId : MarbleId;
                if (y == SpawnY)
                {
                    if (r2 <= 9) return (x == 0 && z == 0) ? GoldOreId : PolishedMarbleId;
                    if (r2 >= 11 * 11 && r2 <= 13 * 13) return GrassId;
                    return ((x + z) & 3) == 0 ? MarbleId : PolishedMarbleId;
                }
                if (y == SpawnY + 1 && r2 >= 11 * 11 && r2 <= 13 * 13 && ((x * 7 + z * 13) & 3) == 0)
                    return FlowerForHash(Hash(x, 3, z, seed));
                if (Math.Abs(x) == 9 && Math.Abs(z) == 9 && y >= SpawnY + 1 && y <= SpawnY + 4)
                    return y == SpawnY + 4 ? LanternId : PolishedMarbleId;
                return 0;
            }

            // Classical Marble Sanctuary Rotundas
            if (TrySampleSanctuaryBlock(x, y, z, seed, out ushort sancBlock))
                return sancBlock;

            int biome = BiomeAt(x, z, seed);

            // Surface & Subsurface
            if (y <= floorTop)
            {
                if (y == floorTop)
                {
                    if (floorTop <= SeaLevel)
                        return ((Hash(x, 0, z, seed) % 6u) == 0u) ? CrystalId : PolishedMarbleId;
                    switch (biome)
                    {
                        case 0: return GrassId;
                        case 1: return ((x + z) % 5 == 0) ? MarbleId : PolishedMarbleId;
                        case 2: return LavenderTurfId;
                        default: return SnowId;
                    }
                }
                if (y >= floorTop - 2)
                {
                    if (biome == 0 || biome == 2) return DirtId;
                    return MarbleId;
                }
                uint oreH = Hash(x, y, z, seed ^ 0x33AF);
                if (y >= 14 && y <= 46)
                {
                    if ((oreH % 43u) == 0u) return GoldOreId;
                    if ((oreH % 37u) == 1u) return QuartzOreId;
                    if ((oreH % 89u) == 2u) return CrystalId;
                }
                return (y < 16) ? AetherStoneId : MarbleId;
            }

            // Ambrosia Sea fill
            if (y <= SeaLevel) return WaterId;

            // Celestial Crystal Spires in biome 2
            if (biome == 2 && floorTop > SeaLevel + 1 && y <= floorTop + 5)
            {
                uint sh = Hash(x, 17, z, seed);
                if ((sh % 47u) == 0u)
                {
                    int spireH = 2 + (int)((sh >> 6) % 4u);
                    if (y <= floorTop + spireH)
                        return y == floorTop + spireH ? AndromedaCrystalId : CrystalId;
                }
            }

            // Blossoming Eden & Sakura Trees
            if (TrySampleEdenTree(x, y, z, seed, out ushort treeBlock))
                return treeBlock;

            // Wild Paradise Flowers on Emerald Gardens & Crystal Groves
            if (y == floorTop + 1 && floorTop > SeaLevel + 1 && (biome == 0 || biome == 2))
            {
                uint fh = Hash(x, 5, z, seed ^ 0x19C7);
                if ((fh % 11u) == 0u) return FlowerForHash(fh >> 4);
            }

            return 0;
        }
    }
}
