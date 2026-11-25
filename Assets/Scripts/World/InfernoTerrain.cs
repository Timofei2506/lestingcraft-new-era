using System;

namespace LestingCraft.World
{
    /// <summary>
    /// The Inferno / Hell Dimension (Dimension.Inferno = 16): a colossal cavernous underworld
    /// beneath reality featuring boiling lava seas (y <= 28), vaulted basalt-and-magma ceilings
    /// (y = 108..125) with hanging stalactites and cascading lavafalls, four distinct volcanic
    /// biomes (Crimson Hellfire Forest, Basalt Pillars, Soul Ash Valley, Magma Rift), floating
    /// magma islands, Obsidian Citadels with bridges & diamond vaults, and glowing Return Portals.
    /// Pure deterministic C# 7.2 math — zero heap allocations, safe for Unity Burst and standalone mcs.
    /// </summary>
    public static class InfernoTerrain
    {
        public const int LavaLevel = 28;
        public const int CeilingBottom = 108;
        public const int CeilingTop = 124;

        const ushort Air = 0;
        const ushort Stone = 1;
        const ushort Lava = 8;
        const ushort Bedrock = 9;
        const ushort Basalt = 10;
        const ushort Crystal = 11;
        const ushort Campfire = 15;
        const ushort GoldOre = 21;
        const ushort SulfurOre = 25;
        const ushort DarkSlate = 29;
        const ushort BonePillar = 30;
        const ushort ThresholdMarble = 40;
        const ushort StoneBrick = 41;
        const ushort DiamondOre = 47;
        const ushort Lantern = 56;
        const ushort GlowShroom = 61;
        const ushort CrimsonRock = 62;
        const ushort MagmaBasalt = 64;
        const ushort SoulCrystal = 67;
        const ushort ObsidianMonolith = 68;
        const ushort SoulAsh = 88;

        public const ushort LavaId = Lava;
        public const ushort BedrockId = Bedrock;
        public const ushort MagmaBasaltId = MagmaBasalt;
        public const ushort ObsidianId = ObsidianMonolith;

        static uint Hash(int a, int b, int c, int seed)
        {
            unchecked
            {
                uint h = (uint)seed;
                h ^= (uint)a * 0x8da6b343u; h = (h << 13) | (h >> 19);
                h ^= (uint)b * 0xd8163841u; h = (h << 17) | (h >> 15);
                h ^= (uint)c * 0xcb1ab31fu; h *= 0x9e3779b1u;
                return h ^ (h >> 16);
            }
        }

        static int FloorDiv(int a, int b)
        {
            int q = a / b;
            int r = a % b;
            return (r != 0 && ((r < 0) != (b < 0))) ? q - 1 : q;
        }

        static float Smooth(float t)
        {
            if (t < 0f) t = 0f;
            else if (t > 1f) t = 1f;
            return t * t * (3f - 2f * t);
        }

        static float ValueNoise(float x, float z, int seed)
        {
            int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
            float fx = Smooth(x - ix), fz = Smooth(z - iz);
            float a = (Hash(ix, 17, iz, seed) & 65535u) / 65535f;
            float b = (Hash(ix + 1, 17, iz, seed) & 65535u) / 65535f;
            float c = (Hash(ix, 17, iz + 1, seed) & 65535u) / 65535f;
            float d = (Hash(ix + 1, 17, iz + 1, seed) & 65535u) / 65535f;
            return (a * (1f - fx) + b * fx) * (1f - fz) + (c * (1f - fx) + d * fx) * fz;
        }

        /// <summary>0 = Crimson Forest, 1 = Basalt Pillars, 2 = Soul Ash Valley, 3 = Magma Rift.</summary>
        public static int BiomeAt(int x, int z, int seed)
        {
            float n1 = ValueNoise(x / 96f, z / 96f, seed ^ 0x4E11);
            float n2 = ValueNoise(x / 84f, z / 84f, seed ^ 0x6F29);
            if (n1 < 0.48f) return n2 < 0.52f ? 0 : 1;
            return n2 < 0.50f ? 2 : 3;
        }

        public static string BiomeTitle(int biome)
        {
            switch (biome)
            {
                case 0: return "Багровый лес";
                case 1: return "Базальтовые столпы";
                case 2: return "Долина пепла душ";
                default: return "Магматический разлом";
            }
        }

        public static bool IsSpawnSanctuary(int x, int z)
        {
            return x * x + z * z <= 14 * 14;
        }

        /// <summary>Every 256x256 region hosts an Obsidian Return Portal (plus the guaranteed one at (0, -8)).</summary>
        public static void ExitPortal(int x, int z, int seed, out int px, out int pz)
        {
            int rx = FloorDiv(x + 128, 256), rz = FloorDiv(z + 128, 256);
            if (rx == 0 && rz == 0)
            {
                px = 0;
                pz = -8;
                return;
            }
            uint h = Hash(rx, 91, rz, seed ^ 0x77E1);
            px = rx * 256 - 90 + (int)(h % 180u);
            pz = rz * 256 - 90 + (int)((h >> 8) % 180u);
        }

        public static bool IsExitThreshold(int x, int z, int seed)
        {
            ExitPortal(x, z, seed, out int px, out int pz);
            return Math.Abs(x - px) <= 1 && Math.Abs(z - pz) <= 1;
        }

        /// <summary>Deterministic Obsidian Citadel center per 192x192 cell (guaranteed near spawn at (68, 52)).</summary>
        public static bool TryGetCitadel(int cellX, int cellZ, int seed, out int cx, out int cz)
        {
            if (cellX == 0 && cellZ == 0)
            {
                cx = 68;
                cz = 52;
                return true;
            }
            uint h = Hash(cellX, 63, cellZ, seed ^ 0xC17A);
            if ((h % 10u) >= 6u) { cx = 0; cz = 0; return false; }
            cx = cellX * 192 + 48 + (int)((h >> 4) % 96u);
            cz = cellZ * 192 + 48 + (int)((h >> 12) % 96u);
            if (Math.Abs(cx) < 34 && Math.Abs(cz) < 34) { cx = 0; cz = 0; return false; }
            return true;
        }

        /// <summary>Walkable surface height (clamped 22..78; basins < 28 are flooded with Lava in 3D Block()).</summary>
        public static int FloorTop(int x, int z, int seed)
        {
            float distOrigin = (float)Math.Sqrt(x * x + z * z);
            if (distOrigin <= 11f) return 42;

            ExitPortal(x, z, seed, out int epx, out int epz);
            if (Math.Abs(x - epx) <= 5 && Math.Abs(z - epz) <= 5) return 42;

            float broad = ValueNoise(x / 68f, z / 68f, seed ^ 0x1F93);
            float ridge = ValueNoise(x / 26f, z / 26f, seed ^ 0x3B71);
            float detail = ValueNoise(x / 11f, z / 11f, seed ^ 0x5D17);

            float raw = 21f + broad * 34f + ridge * 15f + detail * 6f;

            // Blend smoothly up to the arrival sanctuary island around (0,0)
            if (distOrigin < 22f)
            {
                float t = Smooth((22f - distOrigin) / 11f);
                raw = raw * (1f - t) + 42f * t;
            }

            int h = (int)Math.Round(raw);
            if (h < 18) h = 18;
            if (h > 78) h = 78;
            return h;
        }

        public static ushort SurfaceBlockForBiome(int biome, int height)
        {
            if (height <= LavaLevel) return MagmaBasalt;
            switch (biome)
            {
                case 0: return CrimsonRock;
                case 1: return Basalt;
                case 2: return SoulAsh;
                default: return MagmaBasalt;
            }
        }

        static bool TrySampleReturnPortal(int x, int y, int z, int seed, out ushort block)
        {
            block = 0;
            ExitPortal(x, z, seed, out int px, out int pz);
            int dx = x - px, dz = z - pz;
            if (Math.Abs(dx) > 4 || Math.Abs(dz) > 4) return false;
            const int baseY = 42;
            int ry = y - baseY;
            if (ry < -4 || ry > 9) return false;

            if (ry <= 0)
            {
                block = (ry == 0 && Math.Abs(dx) <= 1 && Math.Abs(dz) <= 1) ? ThresholdMarble :
                        (Math.Abs(dx) == 4 || Math.Abs(dz) == 4) ? MagmaBasalt : DarkSlate;
                return true;
            }
            // Corner braziers
            if (Math.Abs(dx) == 3 && Math.Abs(dz) == 3)
            {
                if (ry <= 2) { block = ObsidianMonolith; return true; }
                if (ry == 3) { block = Campfire; return true; }
                return false;
            }
            // Obsidian Portal Arch at dz == 0, |dx| <= 2, ry = 1..7
            if (dz == 0 && Math.Abs(dx) <= 2 && ry <= 7)
            {
                if (Math.Abs(dx) == 2 || ry == 6)
                {
                    block = (ry == 6 && dx == 0) ? SoulCrystal : ObsidianMonolith;
                    return true;
                }
                if (ry == 7)
                {
                    if (Math.Abs(dx) <= 1) { block = dx == 0 ? Lantern : ObsidianMonolith; return true; }
                    return false;
                }
                // Walkable open doorway at ry = 1..3, glowing portal crown at ry = 4..5
                if (ry >= 4)
                {
                    block = SoulCrystal;
                    return true;
                }
                block = Air;
                return true;
            }
            block = Air;
            return true;
        }

        static bool TrySampleCitadel(int x, int y, int z, int seed, out ushort block)
        {
            block = 0;
            int cellX = FloorDiv(x, 192), cellZ = FloorDiv(z, 192);
            if (!TryGetCitadel(cellX, cellZ, seed, out int cx, out int cz)) return false;

            int dx = x - cx, dz = z - cz;
            int adx = Math.Abs(dx), adz = Math.Abs(dz);

            // Extend Nether Bridge walkways along X and Z axes out to +-34m over the lava sea!
            const int deckY = 40;
            if ((adx <= 18 && adz <= 18) == false)
            {
                bool bridgeX = adz <= 2 && adx <= 36;
                bool bridgeZ = adx <= 2 && adz <= 36;
                if (!bridgeX && !bridgeZ) return false;

                int span = bridgeX ? adx : adz;
                int side = bridgeX ? adz : adx;
                // Support piers down to bedrock every 9 blocks
                if (y <= deckY)
                {
                    if (y == deckY) { block = side == 2 ? ObsidianMonolith : DarkSlate; return true; }
                    if (y == deckY - 1) { block = DarkSlate; return true; }
                    if (span % 9 <= 1 && side <= 1) { block = Basalt; return true; }
                    return false;
                }
                if (y == deckY + 1 && side == 2)
                {
                    block = (span % 6 == 0) ? Lantern : StoneBrick;
                    return true;
                }
                if (y >= deckY + 1 && y <= deckY + 4) { block = Air; return true; }
                return false;
            }

            // Main Obsidian Citadel Keep (37x37 footprint)
            if (y < deckY)
            {
                bool pillar = (adx >= 15 || adz >= 15 || (adx <= 4 && adz <= 4) || (adx % 7 <= 1 && adz % 7 <= 1));
                if (y >= deckY - 2) { block = DarkSlate; return true; }
                if (pillar) { block = (y % 5 == 0) ? MagmaBasalt : Basalt; return true; }
                return false;
            }

            int ry = y - deckY;
            if (ry > 18) return false;

            if (ry == 0)
            {
                block = (adx <= 2 && adz <= 2) ? MagmaBasalt : ((adx + adz) % 5 == 0 ? ObsidianMonolith : DarkSlate);
                return true;
            }

            // Outer Curtain Battlements (adx == 18 || adz == 18)
            if (adx == 18 || adz == 18)
            {
                bool gate = (adx <= 2 || adz <= 2) && ry <= 4;
                if (gate) { block = Air; return true; }
                if (ry <= 5) { block = ry == 3 ? MagmaBasalt : DarkSlate; return true; }
                if (ry == 6 && ((dx + dz) & 1) == 0) { block = ObsidianMonolith; return true; }
                if (ry == 7 && adx == 18 && adz == 18) { block = Campfire; return true; }
                block = Air;
                return true;
            }

            // Four Corner Watchtowers (adx >= 13 && adz >= 13)
            if (adx >= 13 && adz >= 13)
            {
                bool wall = adx == 13 || adx == 17 || adz == 13 || adz == 17;
                bool door = (adx == 13 && adz == 15 || adz == 13 && adx == 15) && ry <= 3;
                if (door) { block = Air; return true; }
                if (ry <= 11)
                {
                    if (wall) { block = (ry == 6 || ry == 11) ? ObsidianMonolith : DarkSlate; return true; }
                    if (ry == 6) { block = DarkSlate; return true; }
                    if (ry == 1 && adx == 15 && adz == 15) { block = Lantern; return true; }
                    block = Air;
                    return true;
                }
                if (ry == 12 && wall && ((dx + dz) & 1) == 0) { block = ObsidianMonolith; return true; }
                if (ry == 12 && adx == 15 && adz == 15) { block = Campfire; return true; }
                block = Air;
                return true;
            }

            // Central Throne & Diamond Hell-Vault Keep (adx <= 7 && adz <= 7)
            if (adx <= 7 && adz <= 7)
            {
                bool keepWall = adx == 7 || adz == 7;
                bool keepDoor = ((adx <= 1 && adz == 7) || (adz <= 1 && adx == 7)) && ry <= 4;
                if (keepDoor) { block = Air; return true; }
                if (ry <= 10)
                {
                    if (keepWall) { block = (ry == 5 || ry == 10) ? MagmaBasalt : ObsidianMonolith; return true; }
                    // Central Treasure Altar with Diamond & Soul Crystal!
                    if (adx <= 1 && adz <= 1)
                    {
                        if (ry == 1) { block = (adx == 0 && adz == 0) ? DiamondOre : MagmaBasalt; return true; }
                        if (ry == 2 && adx == 0 && adz == 0) { block = DiamondOre; return true; }
                        if (ry == 3 && adx == 0 && adz == 0) { block = SoulCrystal; return true; }
                    }
                    if (ry == 1 && adx == 5 && adz == 5) { block = Lantern; return true; }
                    if (ry == 9 && adx == 0 && adz == 0) { block = Lantern; return true; }
                    block = Air;
                    return true;
                }
                if (ry <= 15 && adx <= (15 - ry) + 1 && adz <= (15 - ry) + 1)
                {
                    block = ry == 15 ? SoulCrystal : ObsidianMonolith;
                    return true;
                }
                block = Air;
                return true;
            }

            // Courtyard eternal braziers
            if (adx == 10 && adz == 10)
            {
                if (ry == 1) { block = ObsidianMonolith; return true; }
                if (ry == 2) { block = Campfire; return true; }
            }

            block = Air;
            return true;
        }

        public static ushort Block(int x, int y, int z, int seed, int floorTop)
        {
            if (y <= 0) return Bedrock;
            if (y >= CeilingTop) return y == CeilingTop ? Bedrock : Air;

            // 1. Return Portals & Obsidian Citadels take structural priority
            if (TrySampleReturnPortal(x, y, z, seed, out ushort portalBlock))
                return portalBlock;
            if (TrySampleCitadel(x, y, z, seed, out ushort citadelBlock))
                return citadelBlock;

            int biome = BiomeAt(x, z, seed);
            bool inSanctuary = IsSpawnSanctuary(x, z);

            // 2. Vaulted Cavern Ceiling & Hanging Stalactites + Ceiling Crystals (y >= 92)
            float roofNoise = ValueNoise(x / 22f, z / 22f, seed ^ 0x91C3);
            int roofStart = CeilingBottom - (int)(roofNoise * 9f);
            if (y >= roofStart)
            {
                if (y >= CeilingTop - 1) return Bedrock;
                uint rh = Hash(x, y, z, seed ^ 0xA441);
                if (y == roofStart && (rh % 29u) == 0u) return (rh & 1u) == 0u ? Crystal : SoulCrystal;
                return (rh % 7u) == 0u ? MagmaBasalt : Basalt;
            }

            // Hanging basalt/magma stalactites
            uint colHash = Hash(x, 31, z, seed ^ 0x55B2);
            if (!inSanctuary && (colHash % 37u) == 0u)
            {
                int dropLen = 4 + (int)((colHash >> 5) % 9u);
                if (y >= roofStart - dropLen && y < roofStart)
                    return y == roofStart - dropLen ? SoulCrystal : Basalt;
            }

            // 3. Cascading Ceiling Lavafalls (every 64x64 cell outside Spawn Sanctuary)
            if (!inSanctuary)
            {
                int lcx = FloorDiv(x, 64), lcz = FloorDiv(z, 64);
                uint lh = Hash(lcx, 109, lcz, seed ^ 0xFA11);
                if ((lh % 3u) != 0u)
                {
                    int fx = lcx * 64 + 14 + (int)((lh >> 4) % 36u);
                    int fz = lcz * 64 + 14 + (int)((lh >> 11) % 36u);
                    if (!IsSpawnSanctuary(fx, fz) && Math.Abs(x - fx) <= 0 && Math.Abs(z - fz) <= 0 && y >= LavaLevel && y <= roofStart)
                        return Lava;
                }
            }

            // 4. Colossal Floor-to-Ceiling Basalt Pillars (especially dense in Biome 1)
            if (!inSanctuary)
            {
                int pcx = FloorDiv(x, 24), pcz = FloorDiv(z, 24);
                uint ph = Hash(pcx, 73, pcz, seed ^ 0xB45A);
                uint chance = biome == 1 ? 2u : 6u;
                if ((ph % chance) == 0u)
                {
                    int px = pcx * 24 + 6 + (int)((ph >> 4) % 12u);
                    int pz = pcz * 24 + 6 + (int)((ph >> 10) % 12u);
                    float rad = 1.6f + ((ph >> 16) % 3u) * 0.7f;
                    float waist = 1.0f + Math.Abs((y - 64f) / 48f) * 0.65f;
                    float dx = x - px, dz = z - pz;
                    if (dx * dx + dz * dz <= (rad * waist) * (rad * waist))
                        return ((y + x + z) % 7 == 0) ? MagmaBasalt : Basalt;
                }
            }

            // 5. Floating Magma Islands in the upper cavern (y = 66..80)
            if (!inSanctuary && y >= 58 && y <= 84)
            {
                int icx = FloorDiv(x, 96), icz = FloorDiv(z, 96);
                uint ih = Hash(icx, 131, icz, seed ^ 0xF10A);
                if ((ih % 2u) == 0u)
                {
                    int ix = icx * 96 + 24 + (int)((ih >> 4) % 48u);
                    int iz = icz * 96 + 24 + (int)((ih >> 11) % 48u);
                    int deck = 72 + (int)((ih >> 18) % 7u);
                    float dx = x - ix, dz = z - iz;
                    float dist = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (dist <= 11.5f)
                    {
                        int bot = deck - (int)Math.Round((1f - (dist / 12f) * (dist / 12f)) * 9f);
                        if (y >= bot && y <= deck)
                            return y == deck ? MagmaBasalt : (y <= bot + 1 ? Crystal : Basalt);
                        if (y == deck + 1 && (int)dist == 0)
                            return SoulCrystal;
                    }
                }
            }

            // 6. Solid Underworld Floor & Ore Veins (y <= floorTop)
            if (y <= floorTop)
            {
                if (y == floorTop)
                {
                    if (inSanctuary)
                        return ((Math.Abs(x) + Math.Abs(z)) % 4 == 0) ? MagmaBasalt : DarkSlate;
                    return SurfaceBlockForBiome(biome, floorTop);
                }
                if (y >= floorTop - 2)
                    return biome == 0 ? CrimsonRock : biome == 2 ? SoulAsh : Basalt;

                uint ore = Hash(x / 2, y / 2, z / 2, seed ^ 0x0AE7);
                if ((ore % 53u) == 0u) return GoldOre;
                if ((ore % 61u) == 0u) return SulfurOre;
                if (y <= 26 && (ore % 89u) == 0u) return DiamondOre;
                if ((ore % 41u) == 0u) return MagmaBasalt;
                return Basalt;
            }

            // 7. Boiling Lava Sea fills low basins up to LavaLevel (28)
            if (y <= LavaLevel)
                return Lava;

            // 8. Surface Flora & Landmarks above dry ground (floorTop > LavaLevel)
            if (floorTop > LavaLevel && !inSanctuary)
            {
                int dy = y - floorTop;
                // Crimson Hellfire Trees in Biome 0
                if (biome == 0 && dy >= 1 && dy <= 9)
                {
                    int tcx = FloorDiv(x, 8), tcz = FloorDiv(z, 8);
                    for (int oz = -1; oz <= 1; oz++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            uint th = Hash(tcx + ox, 211, tcz + oz, seed ^ 0x7AE3);
                            if ((th % 3u) != 0u) continue;
                            int tx = (tcx + ox) * 8 + 2 + (int)((th >> 3) % 4u);
                            int tz = (tcz + oz) * 8 + 2 + (int)((th >> 7) % 4u);
                            if (IsSpawnSanctuary(tx, tz)) continue;
                            int thTop = FloorTop(tx, tz, seed);
                            if (thTop <= LavaLevel || BiomeAt(tx, tz, seed) != 0) continue;
                            int rdy = y - thTop;
                            int adx = Math.Abs(x - tx), adz = Math.Abs(z - tz);
                            int trunkH = 5 + (int)((th >> 12) % 3u);
                            if (adx == 0 && adz == 0 && rdy >= 1 && rdy <= trunkH)
                                return ObsidianMonolith;
                            if (rdy >= trunkH - 1 && rdy <= trunkH + 2 && adx <= 2 && adz <= 2 && adx + adz <= 3)
                            {
                                if (adx == 0 && adz == 0 && rdy == trunkH + 1) return Lantern;
                                return ((x + y + z) % 5 == 0) ? MagmaBasalt : CrimsonRock;
                            }
                        }
                }

                // Scatter glowing mushrooms, eternal fires & soul-crystal spires on the floor (dy == 1..3)
                if (dy >= 1 && dy <= 3)
                {
                    uint sh = Hash(x, 307, z, seed ^ 0xDE01);
                    if (biome == 0 && dy == 1 && (sh % 29u) == 0u) return GlowShroom;
                    if (biome == 2)
                    {
                        if (dy == 1 && (sh % 43u) == 0u) return Campfire;
                        if ((sh % 67u) == 0u && dy <= 1 + (int)((sh >> 4) % 3u)) return BonePillar;
                        if (dy == 1 && (sh % 79u) == 0u) return SoulCrystal;
                    }
                    if (biome == 3 && dy == 1 && (sh % 47u) == 0u) return Crystal;
                }
            }

            return Air;
        }
    }
}
