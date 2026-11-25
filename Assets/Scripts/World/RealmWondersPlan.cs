using Unity.Mathematics;

namespace LestingCraft.World
{
    /// <summary>
    /// Procedural Realm Wonders for Lestingia (contentVersion >= 11):
    /// 1) High Floating Sky Islands & Archipelagos (y ~ 124..196) with inverted crystal-studded rock spires,
    ///    ancient marble sky-shrines, sakura & sunlit oak groves, and cascading sky-to-earth waterfalls
    ///    plunging into turquoise valley pools.
    /// 2) Bustling Frontier & Regional Towns with cobblestone plazas, fountains, lantern-lit boulevards,
    ///    2-story timbered guildhalls, cozy furnished cottages, market stalls, watchtowers, and gardens.
    /// 3) Natural Highland Cliff Waterfalls, Traveler Windmills, and Mossy Stone Wayshrine Arches.
    /// Pure value-type math so Burst-compiled GenerateChunkJob and SampleContent match bit-for-bit.
    /// </summary>
    public static class RealmWondersPlan
    {
        public const int SkyCellSize = 176;
        public const int TownCellSize = 256;
        public const int WonderCellSize = 144;

        public struct ColumnPlan
        {
            public byte Flags; // bit0: sky, bit1: skyWaterfall, bit2: town, bit3: wonder
            public short GroundH;

            // Sky island column parameters
            public short SkyDeckY;
            public short SkyBottomY;
            public byte SkyKind;       // 0 none, 1 main deck, 2 satellite islet
            public byte SkyRimRaise;
            public byte SkyFeature;    // 0 meadow, 1 shrine floor/pillar, 2 spring water, 3 sakura tree, 4 oak tree, 5 crystal spire
            public sbyte SkyRelX, SkyRelZ;
            public short SkyWaterfallTopY;
            public byte SkyPoolDist;   // 0..6 around waterfall landing

            // Town parameters
            public short TownBaseY;
            public sbyte TownRelX, TownRelZ;
            public byte TownRadius;
            public byte TownSeedByte;

            // Scenic wonder parameters (cliff waterfall / windmill / stone arch)
            public short WonderBaseY;
            public sbyte WonderRelX, WonderRelZ;
            public byte WonderKind; // 1 cliff waterfall, 2 windmill, 3 stone arch camp

            public bool HasAny => Flags != 0;

            public bool TrySampleY(int wx, int y, int wz, int seed, out ushort block)
            {
                block = 0;
                if (Flags == 0 || y <= 0 || y >= ChunkData.SizeY) return false;

                // 1. High Floating Sky Island body, shrine & canopy (y >= 92)
                if ((Flags & 1) != 0 && y >= SkyBottomY && y <= SkyDeckY + 18)
                {
                    if (SampleSkyIslandY(wx, y, wz, seed, in this, out block))
                        return true;
                }

                // 2. Sky-to-Ground Cascading Waterfall & Plunge Pool
                if ((Flags & 2) != 0)
                {
                    if (SkyWaterfallTopY > 0 && y >= GroundH - 2 && y <= SkyWaterfallTopY)
                    {
                        block = y == GroundH - 3 ? BlockRegistry.StoneId : BlockRegistry.WaterId;
                        return true;
                    }
                    if (SkyPoolDist > 0 && y >= GroundH - 3 && y <= GroundH + 4)
                    {
                        if (SamplePlungePoolY(wx, y, wz, seed, GroundH, SkyPoolDist, out block))
                            return true;
                    }
                }

                // 3. Procedural Bustling Town (ground level)
                if ((Flags & 4) != 0 && y >= math.min(GroundH, TownBaseY) - 6 && y <= TownBaseY + 24)
                {
                    if (SampleTownY(wx, y, wz, seed, in this, out block))
                        return true;
                }

                // 4. Scenic Cliff Waterfall / Windmill / Stone Arch Wayshrine
                if ((Flags & 8) != 0 && y >= math.min(GroundH, WonderBaseY) - 5 && y <= WonderBaseY + 26)
                {
                    if (SampleWonderY(wx, y, wz, seed, in this, out block))
                        return true;
                }

                return false;
            }
        }

        static int FloorDiv(int a, int b)
        {
            int q = a / b;
            if ((a ^ b) < 0 && q * b != a) q--;
            return q;
        }

        public static bool StoryProtected(int x, int z) =>
            x >= -116 && x <= 276 && z >= -196 && z <= 164;

        public static bool KingdomProtected(int x, int z) =>
            (math.abs(x - KingdomPlan.CenterX) <= KingdomPlan.Region + 14 &&
             math.abs(z - KingdomPlan.CenterZ) <= KingdomPlan.Region + 14) ||
            KingdomPlan.Road(x, z);

        static bool GroundProtected(int x, int z, int seed)
        {
            if (StoryProtected(x, z) || KingdomProtected(x, z)) return true;
            var exp = ExpeditionGenerator.At(x, z, seed, 11);
            return exp.Surface;
        }

        static bool AreaClearForTown(int cx, int cz, int radius, int seed)
        {
            if (cx + radius >= -118 && cx - radius <= 278 && cz + radius >= -198 && cz - radius <= 166)
                return false;
            if (math.abs(cx - KingdomPlan.CenterX) <= KingdomPlan.Region + radius + 16 &&
                math.abs(cz - KingdomPlan.CenterZ) <= KingdomPlan.Region + radius + 16)
                return false;
            if (KingdomPlan.Road(cx, cz) || KingdomPlan.Road(cx - 24, cz) || KingdomPlan.Road(cx + 24, cz))
                return false;
            // Ensure no ExpeditionGenerator monument overlaps this town footprint
            for (int dz = -radius; dz <= radius; dz += 22)
                for (int dx = -radius; dx <= radius; dx += 22)
                    if (ExpeditionGenerator.At(cx + dx, cz + dz, seed, 11).Surface)
                        return false;
            int h0 = TerrainGenerator.Height(cx, cz, seed, Dimension.Lestingia);
            return h0 >= 42 && h0 <= 61;
        }

        // =====================================================================
        // PUBLIC QUERIES FOR LIVING VILLAGERS & WILDLIFE SIMULATION
        // =====================================================================

        public static bool TryGetSkyIslandCenter(int cellX, int cellZ, int seed, out int centerX, out int deckY, out int centerZ, out int radius)
        {
            centerX = deckY = centerZ = radius = 0;
            // Three guaranteed horizon sky-archipelagos right outside spawn's story reserve
            if (cellX == -1 && cellZ == -1)
            {
                centerX = -148; centerZ = -72; deckY = 138; radius = 26;
                return true;
            }
            if (cellX == 0 && cellZ == 1)
            {
                centerX = 56; centerZ = 212; deckY = 148; radius = 27;
                return true;
            }
            if (cellX == -2 && cellZ == 0)
            {
                centerX = -256; centerZ = 76; deckY = 154; radius = 25;
                return true;
            }

            uint h = TerrainGenerator.Hash(cellX, 11, cellZ, seed ^ 0x51C79A);
            if (h % 10 >= 6) return false; // 60% of sky cells contain a floating island archipelago

            centerX = cellX * SkyCellSize + 48 + (int)(h % 80u);
            centerZ = cellZ * SkyCellSize + 48 + (int)((h >> 7) % 80u);
            radius = 21 + (int)((h >> 14) % 8u);
            deckY = 128 + (int)((h >> 19) % 46u);

            if (centerX + radius + 16 >= -116 && centerX - radius - 16 <= 276 &&
                centerZ + radius + 16 >= -196 && centerZ - radius - 16 <= 164)
                return false;
            if (math.abs(centerX - KingdomPlan.CenterX) <= KingdomPlan.Core + 24 &&
                math.abs(centerZ - KingdomPlan.CenterZ) <= KingdomPlan.Core + 24)
                return false;

            return true;
        }

        public static bool TryGetSkySurface(int wx, int wz, int seed, out int surfaceY)
        {
            surfaceY = 0;
            int cellX = FloorDiv(wx, SkyCellSize), cellZ = FloorDiv(wz, SkyCellSize);
            for (int oz = -1; oz <= 1; oz++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (!TryGetSkyIslandCenter(cellX + ox, cellZ + oz, seed, out int cx, out int deckY, out int cz, out int rad))
                        continue;
                    int dx = wx - cx, dz = wz - cz;
                    float dist = math.sqrt(dx * dx + dz * dz);
                    float lobe = 2.6f * math.sin(dx * 0.23f + cz * 0.07f) + 2.1f * math.cos(dz * 0.21f - cx * 0.05f);
                    if (dist <= rad + lobe - 1.5f)
                    {
                        surfaceY = deckY + (dist > rad - 3.5f ? 1 : 0);
                        return true;
                    }
                }
            return false;
        }

        public static bool TryGetTownCenter(int cellX, int cellZ, int seed, out int centerX, out int baseY, out int centerZ, out int radius)
        {
            centerX = baseY = centerZ = radius = 0;
            // Guaranteed frontier towns within a short scenic walk from spawn
            if (cellX == -1 && cellZ == 0)
            {
                centerX = -188; centerZ = 52; radius = 44;
                if (ExpeditionGenerator.At(centerX, centerZ, seed, 11).Surface) centerZ = 96;
                int h = TerrainGenerator.Height(centerX, centerZ, seed, Dimension.Lestingia);
                baseY = math.clamp(h, 43, 58);
                return true;
            }
            if (cellX == 1 && cellZ == 1)
            {
                centerX = 328; centerZ = 248; radius = 46;
                if (ExpeditionGenerator.At(centerX, centerZ, seed, 11).Surface) centerX = 360;
                int h = TerrainGenerator.Height(centerX, centerZ, seed, Dimension.Lestingia);
                baseY = math.clamp(h, 43, 58);
                return true;
            }

            uint hash = TerrainGenerator.Hash(cellX, 29, cellZ, seed ^ 0x37B24D);
            if (hash % 10 >= 6) return false; // 60% of regional cells attempt a town

            centerX = cellX * TownCellSize + 68 + (int)(hash % 120u);
            centerZ = cellZ * TownCellSize + 68 + (int)((hash >> 8) % 120u);
            radius = 42 + (int)((hash >> 16) % 6u);

            if (!AreaClearForTown(centerX, centerZ, radius, seed))
                return false;

            int ground = TerrainGenerator.Height(centerX, centerZ, seed, Dimension.Lestingia);
            baseY = math.clamp(ground, 43, 58);
            return true;
        }

        public static bool TryGetWonderSite(int cellX, int cellZ, int seed, out int centerX, out int baseY, out int centerZ, out int kind)
        {
            centerX = baseY = centerZ = kind = 0;
            // Guaranteed dramatic cliff waterfall right Northwest of spawn so players see ground waterfalls immediately
            if (cellX == -1 && cellZ == -2)
            {
                centerX = -84; centerZ = -232; kind = 1;
                if (!GroundProtected(centerX, centerZ, seed))
                {
                    baseY = math.max(43, TerrainGenerator.Height(centerX, centerZ, seed, Dimension.Lestingia));
                    return true;
                }
            }
            // Guaranteed windmill meadow South-West of spawn
            if (cellX == -1 && cellZ == 1)
            {
                centerX = -132; centerZ = 192; kind = 2;
                if (!GroundProtected(centerX, centerZ, seed))
                {
                    baseY = math.max(43, TerrainGenerator.Height(centerX, centerZ, seed, Dimension.Lestingia));
                    return true;
                }
            }

            uint hash = TerrainGenerator.Hash(cellX, 53, cellZ, seed ^ 0x6A19E3);
            if (hash % 10 >= 7) return false;

            centerX = cellX * WonderCellSize + 36 + (int)(hash % 72u);
            centerZ = cellZ * WonderCellSize + 36 + (int)((hash >> 8) % 72u);
            kind = 1 + (int)((hash >> 16) % 3u); // 1: Cliff Waterfall, 2: Windmill, 3: Mossy Arch Camp

            if (GroundProtected(centerX, centerZ, seed)) return false;

            // Skip if inside any Town footprint
            int tx = FloorDiv(centerX, TownCellSize), tz = FloorDiv(centerZ, TownCellSize);
            for (int oz = -1; oz <= 1; oz++)
                for (int ox = -1; ox <= 1; ox++)
                    if (TryGetTownCenter(tx + ox, tz + oz, seed, out int tcx, out _, out int tcz, out int trad))
                        if (math.abs(centerX - tcx) <= trad + 22 && math.abs(centerZ - tcz) <= trad + 22)
                            return false;

            for (int dz = -14; dz <= 14; dz += 14)
                for (int dx = -14; dx <= 14; dx += 14)
                    if (ExpeditionGenerator.At(centerX + dx, centerZ + dz, seed, 11).Surface)
                        return false;

            int h = TerrainGenerator.Height(centerX, centerZ, seed, Dimension.Lestingia);
            if (h <= 41 || h >= 63) return false;
            baseY = h;
            return true;
        }

        // =====================================================================
        // COLUMN PREPARATION & VOXEL SAMPLING
        // =====================================================================

        public static bool TrySample(int wx, int y, int wz, int seed, out ushort block)
        {
            block = 0;
            if (y <= 0 || y >= ChunkData.SizeY) return false;
            int h = TerrainGenerator.Height(wx, wz, seed, Dimension.Lestingia);
            bool expSurface = ExpeditionGenerator.At(wx, wz, seed, 11).Surface;
            var col = PrepareColumn(wx, wz, seed, h, expSurface);
            return col.TrySampleY(wx, y, wz, seed, out block);
        }

        public static ColumnPlan PrepareColumn(int wx, int wz, int seed, int groundH, bool expeditionSurface)
        {
            var plan = new ColumnPlan { GroundH = (short)groundH };

            // --- A. Sky Islands & Cascading Sky Waterfalls ---
            int scx = FloorDiv(wx, SkyCellSize), scz = FloorDiv(wz, SkyCellSize);
            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (!TryGetSkyIslandCenter(scx + ox, scz + oz, seed, out int icx, out int deckY, out int icz, out int rad))
                        continue;
                    int dx = wx - icx, dz = wz - icz;
                    if (math.abs(dx) > 56 || math.abs(dz) > 56) continue;

                    uint ih = TerrainGenerator.Hash(scx + ox, 77, scz + oz, seed ^ 0x491F);
                    int fallDir = (int)(ih % 4u); // 0:+X, 1:-X, 2:+Z, 3:-Z
                    int lipX = fallDir == 0 ? rad - 2 : fallDir == 1 ? -(rad - 2) : 0;
                    int lipZ = fallDir == 2 ? rad - 2 : fallDir == 3 ? -(rad - 2) : 0;

                    // Waterfall column & valley plunge pool (only if ground below is unprotected)
                    int fallWX = icx + lipX, fallWZ = icz + lipZ;
                    int fdx = wx - fallWX, fdz = wz - fallWZ;
                    int fallDistSq = fdx * fdx + fdz * fdz;
                    if (fallDistSq <= 36 && !expeditionSurface && !StoryProtected(wx, wz) && !KingdomProtected(wx, wz))
                    {
                        if (fdx >= 0 && fdx <= 1 && fdz >= 0 && fdz <= 1)
                        {
                            plan.Flags |= 2;
                            plan.SkyWaterfallTopY = (short)(deckY - 1);
                        }
                        else if (fallDistSq <= 30 && groundH >= 40)
                        {
                            plan.Flags |= 2;
                            plan.SkyPoolDist = (byte)math.max(1, (int)math.ceil(math.sqrt(fallDistSq)));
                        }
                    }

                    // Main Sky Island lobe
                    float dist = math.sqrt(dx * dx + dz * dz);
                    float lobe = 2.6f * math.sin(dx * 0.23f + icz * 0.07f) + 2.1f * math.cos(dz * 0.21f - icx * 0.05f);
                    float effR = rad + lobe;
                    if (dist <= effR)
                    {
                        float norm = dist / math.max(1f, effR);
                        float spireNoise = 3.8f * TerrainGenerator.Noise(wx * 0.16f, wz * 0.16f, seed ^ 0x1A73);
                        int depth = math.max(3, (int)((1f - norm * norm) * (rad * 1.12f) + spireNoise * (1f - norm)));
                        plan.Flags |= 1;
                        plan.SkyKind = 1;
                        plan.SkyDeckY = (short)deckY;
                        plan.SkyBottomY = (short)(deckY - depth);
                        plan.SkyRelX = (sbyte)math.clamp(dx, -120, 120);
                        plan.SkyRelZ = (sbyte)math.clamp(dz, -120, 120);
                        plan.SkyRimRaise = (byte)(norm > 0.80f && math.abs(dx - lipX) + math.abs(dz - lipZ) > 3 ? 1 : 0);

                        // Channel from central spring to waterfall lip
                        bool inAqueduct = (fallDir == 0 && dx >= 0 && dx <= lipX + 1 && math.abs(dz) <= 1)
                                       || (fallDir == 1 && dx <= 0 && dx >= lipX - 1 && math.abs(dz) <= 1)
                                       || (fallDir == 2 && dz >= 0 && dz <= lipZ + 1 && math.abs(dx) <= 1)
                                       || (fallDir == 3 && dz <= 0 && dz >= lipZ - 1 && math.abs(dx) <= 1);
                        if (dx == 0 && dz == 0)
                            plan.SkyFeature = 5; // Central crystal spire rising out of the spring!
                        else if (inAqueduct || (dx * dx + dz * dz <= 5))
                            plan.SkyFeature = 2; // Sacred sky water channel/spring
                        else if (dist <= 7.6f)
                            plan.SkyFeature = 1; // Marble Sky-Shrine pavilion
                        else
                        {
                            // Deterministic sakura & sunlit oak groves around the meadow
                            int gx = FloorDiv(dx + 32, 9), gz = FloorDiv(dz + 32, 9);
                            uint th = TerrainGenerator.Hash(gx, 3, gz, seed ^ (scx * 733 + scz * 911));
                            int tx = gx * 9 - 32 + 4, tz = gz * 9 - 32 + 4;
                            if (th % 3 != 0 && math.abs(dx - tx) <= 2 && math.abs(dz - tz) <= 2 && dist < effR - 3.2f && dist > 9.5f)
                            {
                                plan.SkyFeature = (byte)(th % 2 == 0 ? 3 : 4);
                                plan.SkyRelX = (sbyte)(dx - tx);
                                plan.SkyRelZ = (sbyte)(dz - tz);
                            }
                        }
                        break;
                    }

                    // Satellite Sky Islet beside main island
                    int satAngle = (int)((ih >> 4) % 4u);
                    int satX = icx + (satAngle == 0 ? rad + 15 : satAngle == 1 ? -(rad + 15) : 12);
                    int satZ = icz + (satAngle == 2 ? rad + 15 : satAngle == 3 ? -(rad + 15) : -14);
                    int sdx = wx - satX, sdz = wz - satZ;
                    float sdist = math.sqrt(sdx * sdx + sdz * sdz);
                    float satR = 9.5f + 1.4f * math.sin(sdx * 0.35f + sdz * 0.25f);
                    if (sdist <= satR)
                    {
                        float snorm = sdist / satR;
                        int sdeck = deckY + 8 + (int)((ih >> 9) % 7u);
                        int sdepth = math.max(2, (int)((1f - snorm * snorm) * 12f));
                        plan.Flags |= 1;
                        plan.SkyKind = 2;
                        plan.SkyDeckY = (short)sdeck;
                        plan.SkyBottomY = (short)(sdeck - sdepth);
                        plan.SkyRelX = (sbyte)sdx;
                        plan.SkyRelZ = (sbyte)sdz;
                        plan.SkyFeature = (byte)(sdx == 0 && sdz == 0 ? 5 : (math.abs(sdx) <= 2 && math.abs(sdz) <= 2 && (ih & 1) == 0 ? 3 : 0));
                        break;
                    }
                }
            }

            if (expeditionSurface || StoryProtected(wx, wz) || KingdomProtected(wx, wz))
                return plan;

            // --- B. Bustling Procedural Towns ---
            int tcx = FloorDiv(wx, TownCellSize), tcz = FloorDiv(wz, TownCellSize);
            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (!TryGetTownCenter(tcx + ox, tcz + oz, seed, out int centerX, out int baseY, out int centerZ, out int rad))
                        continue;
                    int dx = wx - centerX, dz = wz - centerZ;
                    if (math.abs(dx) <= rad + 4 && math.abs(dz) <= rad + 4)
                    {
                        plan.Flags |= 4;
                        plan.TownBaseY = (short)baseY;
                        plan.TownRelX = (sbyte)dx;
                        plan.TownRelZ = (sbyte)dz;
                        plan.TownRadius = (byte)rad;
                        plan.TownSeedByte = (byte)(TerrainGenerator.Hash(tcx + ox, 19, tcz + oz, seed) & 255u);
                        return plan;
                    }
                }
            }

            // --- C. Scenic Cliff Waterfalls, Windmills & Stone Arch Camps ---
            int wcx = FloorDiv(wx, WonderCellSize), wcz = FloorDiv(wz, WonderCellSize);
            for (int oz = -1; oz <= 1; oz++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    if (!TryGetWonderSite(wcx + ox, wcz + oz, seed, out int centerX, out int baseY, out int centerZ, out int kind))
                        continue;
                    int dx = wx - centerX, dz = wz - centerZ;
                    if (math.abs(dx) <= 18 && math.abs(dz) <= 18)
                    {
                        plan.Flags |= 8;
                        plan.WonderBaseY = (short)baseY;
                        plan.WonderRelX = (sbyte)dx;
                        plan.WonderRelZ = (sbyte)dz;
                        plan.WonderKind = (byte)kind;
                        return plan;
                    }
                }
            }

            return plan;
        }

        static bool SampleSkyIslandY(int wx, int y, int wz, int seed, in ColumnPlan p, out ushort block)
        {
            block = 0;
            int topY = p.SkyDeckY + p.SkyRimRaise;
            if (y <= topY)
            {
                if (p.SkyFeature == 2 && y == p.SkyDeckY)
                {
                    block = BlockRegistry.WaterId;
                    return true;
                }
                if (p.SkyFeature == 2 && y == p.SkyDeckY - 1)
                {
                    block = 42; // Marble aqueduct basin floor
                    return true;
                }
                if (y == topY)
                {
                    if (p.SkyFeature == 1)
                    {
                        int adx = math.abs(p.SkyRelX), adz = math.abs(p.SkyRelZ);
                        block = (adx + adz) % 2 == 0 ? (ushort)42 : (ushort)41;
                    }
                    else
                    {
                        uint mh = TerrainGenerator.Hash(wx, 1, wz, seed ^ 0x2291);
                        block = mh % 7 == 0 ? BlockRegistry.LavenderTurfId : BlockRegistry.GrassId;
                    }
                    return true;
                }
                if (y >= topY - 2)
                {
                    block = BlockRegistry.DirtId;
                    return true;
                }
                // Embedded glowing crystals along the underside spire tip
                if (y <= p.SkyBottomY + 2)
                {
                    uint ch = TerrainGenerator.Hash(wx, y, wz, seed ^ 0x88E1);
                    block = ch % 3 == 0 ? BlockRegistry.CrystalId
                          : ch % 5 == 0 ? BlockRegistry.AndromedaCrystalId
                          : BlockRegistry.AetherStoneId;
                    return true;
                }
                uint sh = TerrainGenerator.Hash(wx, y, wz, seed ^ 0x44A9);
                block = sh % 6 == 0 ? BlockRegistry.AetherStoneId
                      : sh % 11 == 0 ? BlockRegistry.BasaltId
                      : BlockRegistry.StoneId;
                return true;
            }

            int ry = y - topY;
            // Central Crystal Spire Beacon
            if (p.SkyFeature == 5)
            {
                if (ry >= 1 && ry <= 5) { block = 42; return true; }
                if (ry >= 6 && ry <= 9) { block = BlockRegistry.CrystalId; return true; }
                if (ry == 10) { block = BlockRegistry.LanternBlockId; return true; }
                return false;
            }

            // Marble Sky-Shrine Pavilion (pillars + halo arch + warm lanterns)
            if (p.SkyFeature == 1)
            {
                int dx = p.SkyRelX, dz = p.SkyRelZ;
                int adx = math.abs(dx), adz = math.abs(dz);
                bool pillar = (adx == 5 && adz == 5) || (adx == 6 && adz == 2) || (adx == 2 && adz == 6);
                if (pillar && ry >= 1 && ry <= 6) { block = 42; return true; }
                if (pillar && ry == 7) { block = BlockRegistry.LanternBlockId; return true; }
                float r2 = dx * dx + dz * dz;
                if (ry == 7 && r2 >= 18f && r2 <= 46f) { block = 41; return true; }
                if (ry == 8 && r2 >= 10f && r2 <= 30f) { block = BlockRegistry.AetherStoneId; return true; }
                if (ry == 9 && r2 <= 12f) { block = 46; return true; }
                return false;
            }

            // Sky Sakura or Sunlit Oak Trees
            if (p.SkyFeature == 3 || p.SkyFeature == 4)
            {
                int tx = p.SkyRelX, tz = p.SkyRelZ;
                ushort leaf = p.SkyFeature == 3 ? BlockRegistry.CherryLeavesId : BlockRegistry.LeavesId;
                if (tx == 0 && tz == 0 && ry >= 1 && ry <= 5) { block = BlockRegistry.WoodId; return true; }
                if (ry >= 4 && ry <= 7 && math.abs(tx) + math.abs(tz) <= (ry == 7 ? 2 : 3))
                {
                    block = leaf;
                    return true;
                }
                return false;
            }

            // Occasional glowing sky mushrooms / wildflowers on the meadow
            if (ry == 1 && p.SkyRimRaise == 0)
            {
                uint fh = TerrainGenerator.Hash(wx, 91, wz, seed ^ 0x7713);
                if (fh % 29 == 0) { block = BlockRegistry.GlowShroomId; return true; }
                if (fh % 11 == 0) { block = (ushort)(48 + (fh % 4)); return true; }
            }
            return false;
        }

        static bool SamplePlungePoolY(int wx, int y, int wz, int seed, int groundH, int poolDist, out ushort block)
        {
            block = 0;
            if (poolDist <= 3)
            {
                if (y == groundH - 3) { block = BlockRegistry.StoneId; return true; }
                if (y >= groundH - 2 && y <= groundH) { block = BlockRegistry.WaterId; return true; }
                if (y > groundH) { block = BlockRegistry.AirId; return true; }
            }
            else if (poolDist == 4)
            {
                if (y <= groundH)
                {
                    block = (TerrainGenerator.Hash(wx, 0, wz, seed) & 1) == 0 ? BlockRegistry.StoneId : BlockRegistry.SandId;
                    return true;
                }
                if (y == groundH + 1 && TerrainGenerator.Hash(wx, 5, wz, seed) % 7 == 0)
                {
                    block = BlockRegistry.LanternBlockId;
                    return true;
                }
                if (y > groundH) { block = BlockRegistry.AirId; return true; }
            }
            return false;
        }

        // =====================================================================
        // PROCEDURAL BUSTLING TOWNS & CITIES
        // =====================================================================

        static bool SampleTownY(int wx, int y, int wz, int seed, in ColumnPlan p, out ushort block)
        {
            block = 0;
            int dx = p.TownRelX, dz = p.TownRelZ;
            int adx = math.abs(dx), adz = math.abs(dz);
            int cheb = math.max(adx, adz);
            int rad = p.TownRadius;
            if (cheb > rad + 3) return false;

            int baseY = p.TownBaseY;
            // Smooth perimeter terrace transition (cheb > rad - 4)
            if (cheb > rad - 4)
            {
                float t = math.saturate((cheb - (rad - 4)) / 7f);
                int rimY = (int)math.round(math.lerp(baseY, p.GroundH, t));
                if (y < rimY - 3) return false;
                if (y < rimY) { block = BlockRegistry.StoneId; return true; }
                if (y == rimY) { block = rimY <= 41 ? BlockRegistry.SandId : BlockRegistry.GrassId; return true; }
                if (y <= rimY + 8) { block = BlockRegistry.AirId; return true; }
                return false;
            }

            // Solid stone/earth town foundation down to terrain
            if (y < baseY)
            {
                block = y == baseY - 1 ? BlockRegistry.DirtId : (ushort)37;
                return true;
            }

            int ry = y - baseY;

            // 1. Central Town Plaza & Fountain (|dx| <= 8 && |dz| <= 8)
            if (adx <= 8 && adz <= 8)
            {
                if (ry == 0)
                {
                    block = (adx <= 4 && adz <= 4) ? (ushort)42 : ((adx + adz) % 2 == 0 ? (ushort)40 : (ushort)36);
                    return true;
                }
                // Ornate Central Fountain (|dx| <= 3 && |dz| <= 3)
                if (adx <= 3 && adz <= 3)
                {
                    if (dx == 0 && dz == 0)
                    {
                        if (ry >= 1 && ry <= 4) { block = 42; return true; }
                        if (ry == 5) { block = BlockRegistry.CrystalId; return true; }
                        if (ry == 6) { block = BlockRegistry.LanternBlockId; return true; }
                    }
                    else if (adx == 3 || adz == 3)
                    {
                        if (ry == 1) { block = 41; return true; }
                        if (ry == 2 && adx == 3 && adz == 3) { block = BlockRegistry.LanternBlockId; return true; }
                    }
                    else if (ry == 1)
                    {
                        block = BlockRegistry.WaterId;
                        return true;
                    }
                }
                block = BlockRegistry.AirId;
                return true;
            }

            // 2. Main Cross Boulevards & Outer Ring Street
            bool mainRoad = (adx <= 2 && adz <= rad - 4) || (adz <= 2 && adx <= rad - 4);
            bool ringRoad = (cheb >= 23 && cheb <= 25 && adx <= 25 && adz <= 25);
            if (mainRoad || ringRoad)
            {
                if (ry == 0)
                {
                    block = (adx == 0 || adz == 0) ? (ushort)40 : (ushort)36;
                    return true;
                }
                // Warm Carved-Oak Street Lanterns along the boulevards
                bool lampSpot = (adx == 3 || adz == 3) ? false
                    : ((adx == 2 && adz >= 10 && adz <= 34 && adz % 8 == 2) ||
                       (adz == 2 && adx >= 10 && adx <= 34 && adx % 8 == 2));
                if (lampSpot)
                {
                    if (ry >= 1 && ry <= 3) { block = BlockRegistry.CarvedOakId; return true; }
                    if (ry == 4) { block = BlockRegistry.LanternBlockId; return true; }
                }
                block = BlockRegistry.AirId;
                return true;
            }

            // 3. Four Inner-District Signature Buildings:
            //    NE (+,+): 2-Story Grand Guildhall & Tavern [6..18, 6..16]
            if (dx >= 6 && dx <= 18 && dz >= 6 && dz <= 16)
                return SampleTownHouse(dx - 6, ry, dz - 6, 12, 10, true, 59, 28, 0, out block);

            //    NW (-,+): Baker's & Weaver's Manor [-18..-7, 6..15]
            if (dx >= -18 && dx <= -7 && dz >= 6 && dz <= 15)
                return SampleTownHouse(dx + 18, ry, dz - 6, 11, 9, false, 62, 18, 1, out block);

            //    SE (+,-): Master Blacksmith & Workshop [6..17, -15..-6]
            if (dx >= 6 && dx <= 17 && dz >= -15 && dz <= -6)
                return SampleTownHouse(dx - 6, ry, dz + 15, 11, 9, false, 29, 37, 2, out block);

            //    SW (-,-): Scholar's Library & Apothecary [-18..-7, -15..-6]
            if (dx >= -18 && dx <= -7 && dz >= -15 && dz <= -6)
                return SampleTownHouse(dx + 18, ry, dz + 15, 11, 9, true, 59, 18, 3, out block);

            // 4. Outer District: Cottages, Market Stalls, Watchtower & Irrigated Gardens
            //    North Cottage [5..15, 27..35]
            if (dx >= 5 && dx <= 15 && dz >= 27 && dz <= 35)
                return SampleTownHouse(dx - 5, ry, dz - 27, 10, 8, false, 59, 18, 1, out block);

            //    South Cottage [-15..-5, -35..-27]
            if (dx >= -15 && dx <= -5 && dz >= -35 && dz <= -27)
                return SampleTownHouse(dx + 15, ry, dz + 35, 10, 8, false, 62, 28, 0, out block);

            //    West Watchtower / Belltower [-35..-27, 5..13]
            if (dx >= -35 && dx <= -27 && dz >= 5 && dz <= 13)
                return SampleWatchtower(dx + 31, ry, dz - 9, out block);

            //    East Market Stalls [27..35, 5..17]
            if (dx >= 27 && dx <= 35 && dz >= 5 && dz <= 17)
                return SampleMarketRow(dx - 27, ry, dz - 5, out block);

            //    Irrigated Town Gardens [-35..-27, -19..-6] & [27..35, -19..-6]
            if ((dx >= -35 && dx <= -27 || dx >= 27 && dx <= 35) && dz >= -19 && dz <= -6)
            {
                int gx = dx < 0 ? dx + 35 : dx - 27;
                int gz = dz + 19;
                if (gx == 0 || gx == 8 || gz == 0 || gz == 13)
                {
                    if (ry == 0) { block = BlockRegistry.GrassId; return true; }
                    if (ry == 1 && !(gx == 4 && gz == 13)) { block = BlockRegistry.WoodId; return true; }
                    block = BlockRegistry.AirId;
                    return true;
                }
                if (ry == 0)
                {
                    block = (gx == 4) ? BlockRegistry.WaterId : BlockRegistry.DirtId;
                    return true;
                }
                if (ry == 1 && gx != 4)
                {
                    block = (ushort)(48 + ((gx + gz + p.TownSeedByte) % 4));
                    return true;
                }
                block = BlockRegistry.AirId;
                return true;
            }

            // 5. Town Park Lawn, Sakura Trees & Wildflowers
            if (ry == 0)
            {
                block = BlockRegistry.GrassId;
                return true;
            }
            // Decorative Sakura & Oak trees in park corners (±20, ±20) and (±31, ±31)
            int treeAnchorX = adx >= 28 ? (dx > 0 ? 31 : -31) : (dx > 0 ? 20 : -20);
            int treeAnchorZ = adz >= 28 ? (dz > 0 ? 31 : -31) : (dz > 0 ? 20 : -20);
            int tdx = dx - treeAnchorX, tdz = dz - treeAnchorZ;
            if (math.abs(tdx) <= 2 && math.abs(tdz) <= 2 && (adx >= 18 && adz >= 18))
            {
                bool sakura = ((treeAnchorX + treeAnchorZ + p.TownSeedByte) & 2) == 0;
                if (tdx == 0 && tdz == 0 && ry >= 1 && ry <= 5) { block = BlockRegistry.WoodId; return true; }
                if (ry >= 4 && ry <= 7 && math.abs(tdx) + math.abs(tdz) <= (ry == 7 ? 2 : 3))
                {
                    block = sakura ? BlockRegistry.CherryLeavesId : BlockRegistry.LeavesId;
                    return true;
                }
            }
            if (ry == 1 && TerrainGenerator.Hash(wx, 17, wz, seed) % 14 == 0)
            {
                block = (ushort)(48 + (TerrainGenerator.Hash(wx, 19, wz, seed) % 4));
                return true;
            }
            block = BlockRegistry.AirId;
            return true;
        }

        static bool SampleTownHouse(int lx, int ry, int lz, int w, int d, bool twoStory, ushort roofId, ushort wallId, int doorSide, out ushort block)
        {
            block = BlockRegistry.AirId;
            int wallTop = twoStory ? 8 : 4;

            // Floor slab
            if (ry == 0)
            {
                block = (lx == 0 || lx == w || lz == 0 || lz == d) ? (ushort)37 : BlockRegistry.PlanksId;
                return true;
            }

            // Pitched Roof (overhang by 0 within footprint, stepped gable along X)
            if (ry > wallTop)
            {
                int step = ry - wallTop - 1;
                int half = w / 2;
                if (step <= half + 1)
                {
                    if (lx >= step && lx <= w - step)
                    {
                        if (lx == step || lx == w - step || step >= half)
                        {
                            block = step >= half ? BlockRegistry.CarvedOakId : roofId;
                            return true;
                        }
                        if (lz == 0 || lz == d)
                        {
                            block = BlockRegistry.PlanksId;
                            return true;
                        }
                    }
                }
                // Brick chimney at back corner
                if (lx == w - 1 && lz == d - 1 && ry <= wallTop + half + 3)
                {
                    block = 39;
                    return true;
                }
                return true;
            }

            // Doorway opening (2x3)
            int midX = w / 2, midZ = d / 2;
            bool isDoor = (doorSide == 0 && lz == 0 && math.abs(lx - midX) <= 0 && ry >= 1 && ry <= 3)
                       || (doorSide == 1 && lx == w && math.abs(lz - midZ) <= 0 && ry >= 1 && ry <= 3)
                       || (doorSide == 2 && lz == d && math.abs(lx - midX) <= 0 && ry >= 1 && ry <= 3)
                       || (doorSide == 3 && lx == 0 && math.abs(lz - midZ) <= 0 && ry >= 1 && ry <= 3);
            if (isDoor)
            {
                block = BlockRegistry.AirId;
                return true;
            }

            // Exterior walls, timber beams & glass windows
            bool borderX = lx == 0 || lx == w;
            bool borderZ = lz == 0 || lz == d;
            if (borderX || borderZ)
            {
                if (borderX && borderZ) { block = BlockRegistry.WoodId; return true; }
                if (twoStory && ry == 4) { block = BlockRegistry.CarvedOakId; return true; }
                if (ry == 1) { block = 37; return true; }
                bool windowRow = ry == 2 || ry == 3 || (twoStory && (ry == 6 || ry == 7));
                bool windowPos = (borderZ && (lx == 3 || lx == w - 3)) || (borderX && (lz == 3 || lz == d - 3));
                block = (windowRow && windowPos) ? (ushort)44 : wallId;
                return true;
            }

            // Second-story oak floor & stairwell
            if (twoStory && ry == 4)
            {
                if (lx == 2 && lz >= 2 && lz <= 5) { block = BlockRegistry.AirId; return true; }
                block = BlockRegistry.PlanksId;
                return true;
            }
            if (twoStory && lx == 2 && lz == 1 + ry && ry >= 1 && ry <= 3)
            {
                block = BlockRegistry.PlanksId;
                return true;
            }

            // Cozy Interior Furniture (Workbench, Furnace, Bed, Bookshelves, Warm Lanterns)
            if (ry == 1)
            {
                if (lx == 1 && lz == d - 1) { block = BlockRegistry.BedBlockId; return true; }
                if (lx == w - 1 && lz == d - 1) { block = BlockRegistry.FurnaceId; return true; }
                if (lx == w - 1 && lz == 1) { block = BlockRegistry.WorkbenchId; return true; }
                if (lx == 1 && lz == 1) { block = BlockRegistry.BookshelfId; return true; }
            }
            if (ry == 2 && lx == 1 && lz == 1) { block = BlockRegistry.BookshelfId; return true; }
            if ((ry == wallTop && lx == midX && lz == midZ) || (twoStory && ry == 3 && lx == midX && lz == midZ))
            {
                block = BlockRegistry.LanternBlockId;
                return true;
            }

            block = BlockRegistry.AirId;
            return true;
        }

        static bool SampleWatchtower(int dx, int ry, int dz, out ushort block)
        {
            block = BlockRegistry.AirId;
            int adx = math.abs(dx), adz = math.abs(dz);
            if (ry == 0) { block = 37; return true; }
            if (adx <= 3 && adz <= 3 && ry <= 13)
            {
                if (dz == -3 && dx == 0 && ry >= 1 && ry <= 3) { block = BlockRegistry.AirId; return true; }
                if (adx == 3 || adz == 3)
                {
                    if (adx == 3 && adz == 3) { block = 42; return true; }
                    if ((ry == 5 || ry == 10) && (dx == 0 || dz == 0)) { block = 44; return true; }
                    block = 37;
                    return true;
                }
                if (ry == 6 || ry == 12)
                {
                    block = (dx == -2 && dz == -2) ? BlockRegistry.AirId : BlockRegistry.PlanksId;
                    return true;
                }
                if (ry == 13 && dx == 0 && dz == 0) { block = BlockRegistry.LanternBlockId; return true; }
                return true;
            }
            if (ry == 14 && adx <= 4 && adz <= 4)
            {
                block = (adx == 4 || adz == 4) ? (ushort)41 : BlockRegistry.PlanksId;
                return true;
            }
            if (ry == 15 && adx <= 4 && adz <= 4 && (adx == 4 || adz == 4) && ((dx + dz) & 1) == 0)
            {
                block = 42;
                return true;
            }
            if (ry >= 15 && ry <= 17 && dx == 0 && dz == 0)
            {
                block = ry == 17 ? BlockRegistry.CrystalId : BlockRegistry.LanternBlockId;
                return true;
            }
            return true;
        }

        static bool SampleMarketRow(int lx, int ry, int lz, out ushort block)
        {
            block = BlockRegistry.AirId;
            if (ry == 0) { block = 36; return true; }
            // Two colorful market stalls: lz in [0..5] and lz in [7..12]
            bool stallA = lz >= 0 && lz <= 5;
            bool stallB = lz >= 7 && lz <= 12;
            if (!stallA && !stallB) return true;
            int sz = stallA ? lz : lz - 7;
            if (lx >= 1 && lx <= 6)
            {
                bool post = (lx == 1 || lx == 6) && (sz == 0 || sz == 5);
                if (post && ry >= 1 && ry <= 3) { block = BlockRegistry.WoodId; return true; }
                if (ry == 1 && lx == 2 && sz >= 1 && sz <= 4) { block = BlockRegistry.PlanksId; return true; }
                if (ry == 2 && lx == 2 && sz == 2) { block = BlockRegistry.LanternBlockId; return true; }
                if (ry == 4)
                {
                    block = ((lx + sz) & 1) == 0 ? (stallA ? (ushort)59 : (ushort)62) : (ushort)42;
                    return true;
                }
            }
            return true;
        }

        // =====================================================================
        // SCENIC WONDERS: CLIFF WATERFALLS, WINDMILLS & MOSSY ARCH WAYSHRINES
        // =====================================================================

        static bool SampleWonderY(int wx, int y, int wz, int seed, in ColumnPlan p, out ushort block)
        {
            block = 0;
            int dx = p.WonderRelX, dz = p.WonderRelZ;
            int baseY = p.WonderBaseY;

            // Kind 1: Highland Cliff Waterfall & Turquoise Plunge Lagoon
            if (p.WonderKind == 1)
            {
                // North half (dz <= 1): Raised rocky cliff crag (+15m) with headwater spring
                // South half (dz > 1): Deep turquoise plunge lagoon
                float cragDist = math.sqrt(dx * dx + (dz + 5) * (dz + 5));
                float poolDist = math.sqrt(dx * dx + (dz - 6) * (dz - 6));

                // Cascading waterfall face along dz in [-1..2], |dx| <= 2
                if (math.abs(dx) <= 2 && dz >= -2 && dz <= 2)
                {
                    int fallTop = baseY + 14 - math.max(0, dz + 1);
                    if (y < baseY - 3) return false;
                    if (y == baseY - 3) { block = BlockRegistry.StoneId; return true; }
                    if (dz >= 0 && math.abs(dx) <= 1 && y >= baseY - 2 && y <= fallTop)
                    {
                        block = BlockRegistry.WaterId;
                        return true;
                    }
                    if (dz < 0 && y <= fallTop)
                    {
                        block = (y == fallTop && math.abs(dx) <= 1) ? BlockRegistry.WaterId : BlockRegistry.StoneId;
                        return true;
                    }
                }

                // Rocky cliff crag
                if (cragDist <= 11.5f)
                {
                    float hNorm = 1f - (cragDist / 11.5f);
                    int cliffTop = baseY + (int)(hNorm * 16f + 2f * math.sin(dx * 0.4f));
                    if (y <= cliffTop)
                    {
                        if (cragDist <= 3.2f && y == cliffTop) { block = BlockRegistry.WaterId; return true; }
                        block = y == cliffTop ? BlockRegistry.GrassId
                              : y >= cliffTop - 2 ? BlockRegistry.DirtId
                              : ((TerrainGenerator.Hash(wx, y, wz, seed) % 5 == 0) ? BlockRegistry.BasaltId : BlockRegistry.StoneId);
                        return true;
                    }
                    // Crown sakura tree at (-4, -6)
                    if (math.abs(dx + 4) <= 2 && math.abs(dz + 6) <= 2)
                    {
                        int ry = y - cliffTop;
                        if (dx == -4 && dz == -6 && ry >= 1 && ry <= 4) { block = BlockRegistry.WoodId; return true; }
                        if (ry >= 3 && ry <= 6 && math.abs(dx + 4) + math.abs(dz + 6) <= 3) { block = BlockRegistry.CherryLeavesId; return true; }
                    }
                    return false;
                }

                // Turquoise plunge lagoon
                if (poolDist <= 8.5f)
                {
                    if (poolDist <= 6.2f)
                    {
                        if (y == baseY - 3) { block = BlockRegistry.StoneId; return true; }
                        if (y >= baseY - 2 && y <= baseY) { block = BlockRegistry.WaterId; return true; }
                        if (y > baseY) { block = BlockRegistry.AirId; return true; }
                    }
                    else if (y == baseY)
                    {
                        block = BlockRegistry.SandId;
                        return true;
                    }
                    else if (y == baseY + 1 && math.abs(dx) == 6 && dz == 6)
                    {
                        block = BlockRegistry.LanternBlockId;
                        return true;
                    }
                }
                return false;
            }

            // Kind 2: Scenic Windmill & Golden Meadow
            if (p.WonderKind == 2)
            {
                float r = math.sqrt(dx * dx + dz * dz);
                if (r > 13f) return false;
                if (y < baseY) { block = BlockRegistry.DirtId; return true; }
                int ry = y - baseY;
                if (r <= 4.3f && ry <= 12)
                {
                    if (ry == 0) { block = 37; return true; }
                    if (dz == 4 && dx == 0 && ry >= 1 && ry <= 3) { block = BlockRegistry.AirId; return true; }
                    if (r >= 3.1f)
                    {
                        block = ry <= 4 ? (ushort)37 : BlockRegistry.PlanksId;
                        return true;
                    }
                    if (ry == 6) { block = BlockRegistry.PlanksId; return true; }
                    if (ry == 1 && dx == -2 && dz == -2) { block = BlockRegistry.WorkbenchId; return true; }
                    if (ry == 5 && dx == 0 && dz == 0) { block = BlockRegistry.LanternBlockId; return true; }
                    block = BlockRegistry.AirId;
                    return true;
                }
                // Conical shingle cap
                if (ry >= 13 && ry <= 16 && r <= 5.2f - (ry - 12) * 1.1f)
                {
                    block = BlockRegistry.ShinglesId;
                    return true;
                }
                // Giant Windmill Sails on the +Z facade (dz == 5 || dz == 6 around hub ry = 10)
                if ((dz == 5 || dz == 6) && ry >= 3 && ry <= 17)
                {
                    int sy = ry - 10;
                    if (dz == 5 && dx == 0 && sy == 0) { block = BlockRegistry.CarvedOakId; return true; }
                    if (dz == 6 && ((dx == 0 && math.abs(sy) <= 7) || (sy == 0 && math.abs(dx) <= 7)))
                    {
                        block = BlockRegistry.WoodId;
                        return true;
                    }
                    if (dz == 6 && ((dx == 1 && sy > 0 && sy <= 6) || (dx == -1 && sy < 0 && sy >= -6) ||
                                    (sy == 1 && dx < 0 && dx >= -6) || (sy == -1 && dx > 0 && dx <= 6)))
                    {
                        block = BlockRegistry.PlanksId;
                        return true;
                    }
                }
                if (ry == 0) { block = r <= 5.5f ? (ushort)36 : BlockRegistry.GrassId; return true; }
                if (ry == 1 && dx == 3 && dz == 6) { block = BlockRegistry.LanternBlockId; return true; }
                return false;
            }

            // Kind 3: Ancient Mossy Stone Arch & Traveler Campfire
            if (p.WonderKind == 3)
            {
                float r = math.sqrt(dx * dx + dz * dz);
                if (r > 12f) return false;
                if (y < baseY) { block = BlockRegistry.DirtId; return true; }
                int ry = y - baseY;
                if (ry == 0)
                {
                    block = r <= 4.5f ? (ushort)36 : BlockRegistry.GrassId;
                    return true;
                }
                // Soaring Stone Arch spanning X in [-9..9], Z in [-2..2]
                if (math.abs(dz) <= 1 && math.abs(dx) <= 9 && ry >= 1 && ry <= 12)
                {
                    float archY = 10.5f - (dx * dx) * 0.11f;
                    float innerY = 7.6f - (dx * dx) * 0.12f;
                    if (ry <= archY && (ry >= innerY || math.abs(dx) >= 7))
                    {
                        block = (ry >= archY - 1f) ? BlockRegistry.GrassId
                              : ((ry + dx) % 3 == 0 ? (ushort)41 : (ushort)37);
                        return true;
                    }
                    if (dx == 0 && dz == 0 && ry == 7)
                    {
                        block = BlockRegistry.LanternBlockId;
                        return true;
                    }
                }
                // Explorer Campfire & Log Benches under the arch
                if (ry == 1)
                {
                    if (dx == 0 && dz == 3) { block = BlockRegistry.CampfireId; return true; }
                    if (math.abs(dx) == 2 && dz == 3) { block = BlockRegistry.WoodId; return true; }
                    if (dx == 0 && dz == -3) { block = BlockRegistry.CrystalId; return true; }
                }
                return false;
            }

            return false;
        }
    }
}
