using System;

namespace LestingCraft.World
{
    /// <summary>Shared deterministic noise for the Deep Frontier samplers. No allocations.</summary>
    public static class FrontierNoise
    {
        public static uint Hash(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)x * 374761393u ^ (uint)y * 668265263u ^ (uint)z * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        public static float Unit(int x, int y, int z, int seed) { return (Hash(x, y, z, seed) & 65535u) / 65535f; }

        public static float Smooth(float t) { return t <= 0f ? 0f : t >= 1f ? 1f : t * t * (3f - 2f * t); }

        public static float Clamp(float v, float lo, float hi) { return v < lo ? lo : v > hi ? hi : v; }

        public static int FloorDiv(int a, int b) { int q = a / b; int r = a % b; return (r != 0 && ((r < 0) != (b < 0))) ? q - 1 : q; }

        /// <summary>Bilinear value noise in [0,1].</summary>
        public static float Noise(float x, float z, int seed)
        {
            int ix = (int)Math.Floor(x), iz = (int)Math.Floor(z);
            float tx = Smooth(x - ix), tz = Smooth(z - iz);
            float a = Unit(ix, 0, iz, seed), b = Unit(ix + 1, 0, iz, seed);
            float c = Unit(ix, 0, iz + 1, seed), d = Unit(ix + 1, 0, iz + 1, seed);
            return (a + (b - a) * tx) + ((c + (d - c) * tx) - (a + (b - a) * tx)) * tz;
        }

        /// <summary>Fractal sum in [0,1]; octaves 1..5.</summary>
        public static float Fbm(float x, float z, int seed, int octaves)
        {
            float sum = 0f, amplitude = .5f, total = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += Noise(x, z, seed + i * 7919) * amplitude;
                total += amplitude;
                x *= 2.03f; z *= 2.03f; amplitude *= .5f;
            }
            return total <= 0f ? 0f : sum / total;
        }

        /// <summary>Cellular distance to the nearest feature point in a cell grid (0 = centre).</summary>
        public static float Cell(float x, float z, float cellSize, int seed, out int cellX, out int cellZ)
        {
            float fx = x / cellSize, fz = z / cellSize;
            int ix = (int)Math.Floor(fx), iz = (int)Math.Floor(fz);
            float best = 9e9f; cellX = ix; cellZ = iz;
            for (int dz = -1; dz <= 1; dz++)
                for (int dx = -1; dx <= 1; dx++)
                {
                    int cx = ix + dx, cz = iz + dz;
                    uint h = Hash(cx, 0, cz, seed);
                    float px = cx + .15f + (h & 1023u) / 1023f * .70f;
                    float pz = cz + .15f + ((h >> 10) & 1023u) / 1023f * .70f;
                    float d = (fx - px) * (fx - px) + (fz - pz) * (fz - pz);
                    if (d < best) { best = d; cellX = cx; cellZ = cz; }
                }
            return (float)Math.Sqrt(best);
        }
    }

    /// <summary>
    /// Crystalia (18) — a shattered prismic moon. Sharp faceted spires grow from geode basins,
    /// every facet direction is quantised so the manifold reads as cut crystal rather than dunes.
    /// </summary>
    public static class CrystaliaTerrain
    {
        public const int SeaLevel = 0;
        const int MinY = 12, MaxY = 112;

        public static PlanetColumn Column(int x, int z, int seed)
        {
            float broad = FrontierNoise.Fbm(x * .0060f, z * .0060f, seed ^ 0x1C7A, 4);
            float ridge = 1f - Math.Abs(FrontierNoise.Fbm(x * .0135f, z * .0135f, seed ^ 0x33B1, 3) - .5f) * 2f;
            float facet = FrontierNoise.Fbm(x * .0410f, z * .0410f, seed ^ 0x5DA3, 2);

            // Spires: one tall crystal per ~96 block cell, quantised to 4-block steps.
            FrontierNoise.Cell(x, z, 96f, seed ^ 0x71C5, out int cx, out int cz);
            uint h = FrontierNoise.Hash(cx, 0, cz, seed ^ 0x71C5);
            float centreX = cx * 96f + 20f + (h & 255u) / 255f * 56f;
            float centreZ = cz * 96f + 20f + ((h >> 8) & 255u) / 255f * 56f;
            float dx = x - centreX, dz = z - centreZ;
            float radial = (float)Math.Sqrt(dx * dx + dz * dz);

            float h0 = 30f + broad * 16f + (facet - .5f) * 5f;
            PlanetSurface surface = PlanetSurface.CrystalFrostField;
            float spire = 0f;
            if (radial < 34f && (h % 5u) != 0u)
            {
                float t = 1f - radial / 34f;
                spire = (float)Math.Floor(t * t * (46f + (h >> 16) % 27u) / 4f) * 4f;
                surface = radial < 9f ? PlanetSurface.CrystalGeode : PlanetSurface.CrystalSpire;
            }
            else if (ridge > .70f)
            {
                spire = (float)Math.Floor((ridge - .70f) * 190f / 4f) * 4f;
                surface = PlanetSurface.CrystalSpire;
            }
            else if (broad < .34f)
            {
                h0 -= 10f;
                surface = PlanetSurface.CrystalGeode;
            }
            else if (broad > .70f)
            {
                surface = PlanetSurface.CrystalAuroraIce;
            }

            // Frozen fracture trenches: thin, deep, perfectly straight cuts.
            int trench = Math.Abs((((x * 7 + z * 11) % 211) + 211) % 211);
            if (trench < 3) { h0 -= 12f; if (surface == PlanetSurface.CrystalFrostField) surface = PlanetSurface.CrystalGeode; }

            int height = Clamp(Math.Max(h0 + spire, h0 * .6f), MinY, MaxY);
            return new PlanetColumn { Height = height, Surface = surface };
        }

        static int Clamp(float v, int lo, int hi) { int r = (int)Math.Round(v); return r < lo ? lo : r > hi ? hi : r; }
    }

    /// <summary>
    /// Emberfall (19) — an ash world. Basalt shelves and floating cinder islands hang above a
    /// burning soot sea (Y 32); dense soot storms roll between them.
    /// </summary>
    public static class EmberfallTerrain
    {
        public const int SootSeaLevel = 32;
        const int MinY = 12, MaxY = 112;

        public static PlanetColumn Column(int x, int z, int seed)
        {
            float broad = FrontierNoise.Fbm(x * .0048f, z * .0048f, seed ^ 0x2E91, 4);
            float dunes = FrontierNoise.Fbm(x * .0185f, z * .0185f, seed ^ 0x4C13, 3);
            float fine = FrontierNoise.Noise(x * .070f, z * .070f, seed ^ 0x6A75);

            // Migrating soot dunes below the shelf base; the sea itself is caustic magma-silt.
            float baseH = 30f + broad * 22f + (dunes - .5f) * 9f + (fine - .5f) * 2f;
            PlanetSurface surface = baseH <= SootSeaLevel + 1 ? PlanetSurface.EmberMagmaShore : PlanetSurface.EmberSootDune;
            float height = baseH;

            // Shelves: table mountains with a sheer 8-block cliff edge.
            FrontierNoise.Cell(x, z, 128f, seed ^ 0x1F37, out int sx, out int sz);
            uint sh = FrontierNoise.Hash(sx, 0, sz, seed ^ 0x1F37);
            float sxp = sx * 128f + 24f + (sh & 255u) / 255f * 76f;
            float szp = sz * 128f + 24f + ((sh >> 8) & 255u) / 255f * 76f;
            float sdx = x - sxp, sdz = z - szp;
            float sd = (float)Math.Sqrt(sdx * sdx + sdz * sdz);
            if (sd < 40f && (sh >> 20) % 3u != 0u)
            {
                float t = sd / 40f;
                float shelf = 56f + (sh >> 24) % 17u + (t > .82f ? -8f : 0f);
                if (shelf > height) { height = shelf; surface = PlanetSurface.EmberBasaltShelf; }
            }

            // Floating cinder islands: column-local, so they stream with the chunk like terrain.
            uint ih = FrontierNoise.Hash(FrontierNoise.FloorDiv(x, 24), 0, FrontierNoise.FloorDiv(z, 24), seed ^ 0x5B19);
            if ((ih % 9u) == 0u && broad > .40f)
            {
                float lx = ((x % 24) + 24) % 24 - 12f, lz = ((z % 24) + 24) % 24 - 12f;
                if (lx * lx + lz * lz < 26f && height < 78f)
                {
                    height = 78f + (ih >> 8) % 9u;
                    surface = PlanetSurface.EmberAshIsland;
                }
            }

            int h = (int)Math.Round(height);
            return new PlanetColumn { Height = h < MinY ? MinY : h > MaxY ? MaxY : h, Surface = surface };
        }
    }

    /// <summary>
    /// Aquaria (20) — an ocean world. Drowned continents, kelp cathedrals (tall thin spires that
    /// break the surface) and a deep abyssal trench ring; sea level 44.
    /// </summary>
    public static class AquariaTerrain
    {
        public const int SeaLevel = 44;
        const int MinY = 12, MaxY = 100;

        public static PlanetColumn Column(int x, int z, int seed)
        {
            float continent = FrontierNoise.Fbm(x * .0035f, z * .0035f, seed ^ 0x7A31, 4);
            float reef = FrontierNoise.Fbm(x * .0225f, z * .0225f, seed ^ 0x3E17, 3);
            float mask = continent + (reef - .5f) * .22f;

            float h = 20f + mask * 52f;
            PlanetSurface surface = h < 30f ? PlanetSurface.AquaAbyssTrench
                : h < SeaLevel ? PlanetSurface.AquaReefShelf
                : PlanetSurface.AquaTidalRuin;

            // Kelp cathedrals: narrow towers rising out of the shelf in loose clusters.
            FrontierNoise.Cell(x, z, 40f, seed ^ 0x6D2B, out int kx, out int kz);
            uint kh = FrontierNoise.Hash(kx, 0, kz, seed ^ 0x6D2B);
            if ((kh % 4u) == 0u)
            {
                float px = kx * 40f + 6f + (kh & 63u) / 63f * 28f;
                float pz = kz * 40f + 6f + ((kh >> 6) & 63u) / 63f * 28f;
                float dx = x - px, dz = z - pz;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < 6.5f)
                {
                    float t = 1f - d / 6.5f;
                    h = Math.Max(h, SeaLevel + 3f + t * (16f + (kh >> 16) % 24u));
                    if (d < 2.5f) surface = PlanetSurface.AquaKelpCathedral;
                }
            }

            // Tidal ruins: square cut platforms with 3-high walls in the shallow band.
            int gx = ((x % 37) + 37) % 37, gz = ((z % 37) + 37) % 37;
            if (gx < 3 || gz < 3)
            {
                uint rh = FrontierNoise.Hash(FrontierNoise.FloorDiv(x, 37), 0, FrontierNoise.FloorDiv(z, 37), seed ^ 0x1A87);
                if ((rh & 1u) == 0u && h > SeaLevel - 6f && h < SeaLevel + 5f)
                {
                    h = SeaLevel + 3f;
                    surface = PlanetSurface.AquaTidalRuin;
                }
            }

            int hh = (int)Math.Round(h);
            return new PlanetColumn { Height = hh < MinY ? MinY : hh > MaxY ? MaxY : hh, Surface = surface };
        }
    }

    /// <summary>
    /// Stormreach (21) — a lightning world of sky-lattice plateaus hung over an anvil-cloud
    /// canyon system, scored by charge scars where lightning has vitrified the rock.
    /// </summary>
    public static class StormreachTerrain
    {
        public const int CloudFloor = 34;
        const int MinY = 12, MaxY = 116;

        public static PlanetColumn Column(int x, int z, int seed)
        {
            float plateau = FrontierNoise.Fbm(x * .0042f, z * .0042f, seed ^ 0x5F41, 4);
            float lattice = FrontierNoise.Fbm(x * .0200f, z * .0200f, seed ^ 0x26A9, 3);
            float ridge = 1f - Math.Abs(FrontierNoise.Fbm(x * .0080f, z * .0080f, seed ^ 0x48C7, 3) - .5f) * 2f;

            float h = 52f + plateau * 34f + (lattice - .5f) * 12f + ridge * ridge * 14f;
            PlanetSurface surface = h > 90f ? PlanetSurface.StormAnvilPeak
                : h > 70f ? PlanetSurface.StormLatticePlateau
                : PlanetSurface.StormCumulusShelf;

            // Charge scars: lightning has cut glassy gashes through the plateaus along diagonals.
            int scar = Math.Abs((((x * 5 + z * 13) % 97) + 97) % 97);
            if (scar < 2)
            {
                h -= 7f;
                surface = PlanetSurface.StormChargeScar;
            }

            // Lattice support columns descend to the cloud floor, giving the sky its skyline.
            FrontierNoise.Cell(x, z, 88f, seed ^ 0x7B53, out int px, out int pz);
            uint ph = FrontierNoise.Hash(px, 0, pz, seed ^ 0x7B53);
            if ((ph % 3u) == 0u)
            {
                float cx = px * 88f + 14f + (ph & 127u) / 127f * 60f;
                float cz = pz * 88f + 14f + ((ph >> 7) & 127u) / 127f * 60f;
                float dx = x - cx, dz = z - cz;
                if (dx * dx + dz * dz < 12f && h < 100f)
                {
                    h = Math.Max(h, 100f + (ph >> 16) % 13u);
                    surface = PlanetSurface.StormLatticePlateau;
                }
            }

            int hh = (int)Math.Round(h);
            return new PlanetColumn { Height = hh < MinY ? MinY : hh > MaxY ? MaxY : hh, Surface = surface };
        }
    }

    /// <summary>
    /// Verdant (22) — a fungal super-Earth. Rolling loam is buried under colossal cap canopies
    /// whose stalks are load-bearing geometry, with spore marshes in the low basins.
    /// </summary>
    public static class VerdantTerrain
    {
        public const int MarshLevel = 38;
        const int MinY = 12, MaxY = 108;

        public static PlanetColumn Column(int x, int z, int seed)
        {
            float loam = FrontierNoise.Fbm(x * .0052f, z * .0052f, seed ^ 0x1D63, 4);
            float caps = FrontierNoise.Fbm(x * .0155f, z * .0155f, seed ^ 0x3A95, 3);

            float h = 38f + loam * 30f + (caps - .5f) * 8f;
            PlanetSurface surface = h <= MarshLevel + 1 ? PlanetSurface.VerdantSporeMarsh
                : PlanetSurface.VerdantMyceliumLoam;

            // Colossal mushroom: a 5-block stalk carrying a 40-block cap dome.
            FrontierNoise.Cell(x, z, 112f, seed ^ 0x6C21, out int mx, out int mz);
            uint mh = FrontierNoise.Hash(mx, 0, mz, seed ^ 0x6C21);
            if ((mh % 2u) == 0u)
            {
                float px = mx * 112f + 22f + (mh & 255u) / 255f * 68f;
                float pz = mz * 112f + 22f + ((mh >> 8) & 255u) / 255f * 68f;
                float dx = x - px, dz = z - pz;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                float stalkTop = 74f + (mh >> 16) % 17u;
                if (d < 2.5f) { h = stalkTop; surface = PlanetSurface.VerdantCapCanopy; }
                else if (d < 17f)
                {
                    float dome = stalkTop + 5f * (float)Math.Cos(d / 17f * 1.5708f);
                    if (dome > h) { h = dome; surface = PlanetSurface.VerdantCapCanopy; }
                }
                else if (d < 26f && h < stalkTop - 18f)
                {
                    // Spore roots vault the ground under the cap.
                    h = stalkTop - 18f;
                    surface = PlanetSurface.VerdantRootVault;
                }
            }

            int hh = (int)Math.Round(h);
            return new PlanetColumn { Height = hh < MinY ? MinY : hh > MaxY ? MaxY : hh, Surface = surface };
        }
    }

    /// <summary>
    /// Nihility (23) — broken reality. Inverted fragments (columns that read as hanging islands),
    /// glass scarps and a null basin. The vertical extent is the largest of any realm (12..120)
    /// so the manifold itself feels wrong.
    /// </summary>
    public static class NihilityTerrain
    {
        public const int NullLevel = 44;
        const int MinY = 12, MaxY = 120;

        public static PlanetColumn Column(int x, int z, int seed)
        {
            float shards = FrontierNoise.Fbm(x * .0090f, z * .0090f, seed ^ 0x2B77, 4);
            float scarps = 1f - Math.Abs(FrontierNoise.Fbm(x * .0060f, z * .0060f, seed ^ 0x4F19, 3) - .5f) * 2f;
            float glass = FrontierNoise.Noise(x * .0330f, z * .0330f, seed ^ 0x6E41);

            float h = 20f + shards * 44f + scarps * scarps * 30f + (glass - .5f) * 6f;
            PlanetSurface surface = h < 34f ? PlanetSurface.VoidNullBasin
                : scarps > .78f ? PlanetSurface.VoidGlassScarp
                : PlanetSurface.VoidShardForest;

            // Inverted fragments: quantised steps that hang above the basin, cast in 6-block bands.
            FrontierNoise.Cell(x, z, 64f, seed ^ 0x7C83, out int fx, out int fz);
            uint fh = FrontierNoise.Hash(fx, 0, fz, seed ^ 0x7C83);
            if ((fh % 3u) == 0u)
            {
                float px = fx * 64f + 10f + (fh & 127u) / 127f * 44f;
                float pz = fz * 64f + 10f + ((fh >> 7) & 127u) / 127f * 44f;
                float dx = x - px, dz = z - pz;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < 19f)
                {
                    float t = 1f - d / 19f;
                    float inverted = 96f - (float)Math.Floor(t * 5f) * 6f;
                    if (inverted > h) { h = inverted; surface = PlanetSurface.VoidInvertedFragment; }
                }
            }

            int hh = (int)Math.Round(h);
            return new PlanetColumn { Height = hh < MinY ? MinY : hh > MaxY ? MaxY : hh, Surface = surface };
        }
    }
}

namespace LestingCraft.World
{
    /// <summary>
    /// Solid material lookup for the six Deep Frontier bodies. Kept out of PlanetTerrain's
    /// already large switch so each realm's stratigraphy is readable on its own.
    /// Block ids follow the shipped catalog: 27 granite, 42 polished marble, 47 diamond ore,
    /// 48 diamond block, 60 cherry leaves, 61 glow shroom, 64 magma basalt, 67 andromeda
    /// crystal, 68 dream monolith, 69 pale dream stone, 79 reinforced concrete, 88 trinitite.
    /// </summary>
    public static class FrontierBlocks
    {
        const ushort Air = 0, Stone = 1, Dirt = 2, Grass = 3, Wood = 4, Leaves = 5, Sand = 6, Water = 7, Lava = 8;
        const ushort Bedrock = 9, Basalt = 10, Crystal = 11, Aether = 12, Ore = 13, Snow = 14;
        // Deep Frontier surfaces. They sit in the free id band above the item catalog
        // (19..196) and below the tool ids (140+), so a block id can never overwrite an item
        // definition. Layers: prism-crystal … void-shard (114..125).
        const ushort PrismCrystal = 115, AuroraIce = 116, SootDune = 117, EmberMagmaShore = 118,
            KelpWeave = 119, ReefShelf = 124, TidalRuin = 125, ChargeGlass = 126, StormIron = 127,
            MyceliumCap = 128, SporeLoam = 129, VoidShard = 133;
        const ushort Granite = 27, Limestone = 28, Slate = 29, Marble = 30, Obsidian = 31, Gravel = 32, Clay = 33;
        const ushort MossyStone = 35, Cobblestone = 36, Sandstone = 43, Glass = 44, DiamondOre = 47, DiamondBlock = 48;
        const ushort CherryLeaves = 60, GlowShroom = 61, RedMesa = 62, GlacialIce = 63, MagmaBasalt = 64;
        const ushort TitaniumHull = 66, AndromedaCrystal = 67, DreamMonolith = 68, PaleStone = 69;
        const ushort UraniumOre = 70, ReinforcedConcrete = 79, WhiteTruss = 82, Trinitite = 88;

        static bool Crust(int y, int height, out bool shallow)
        {
            shallow = y >= height - 3;
            return y < height;
        }

        public static ushort Crystalia(int y, PlanetColumn column, int seed, int x, int z)
        {
            if (y > column.Height) return Overhang(Dimension.Crystalia, y, seed, x, z);
            if (y == 0) return Bedrock;
            if (y == column.Height)
            {
                switch (column.Surface)
                {
                    case PlanetSurface.CrystalSpire: return AndromedaCrystal;
                    case PlanetSurface.CrystalGeode: return DiamondOre;
                    case PlanetSurface.CrystalAuroraIce: return AuroraIce;
                    default: return PrismCrystal;
                }
            }
            if (y > column.Height - 4)
                return column.Surface == PlanetSurface.CrystalAuroraIce ? AuroraIce : PrismCrystal;
            if (y > column.Height - 9) return PaleStone;
            // Faceted ore veins inside the crust, richer than any 1.x body.
            uint h = FrontierNoise.Hash(x, y, z, seed ^ 0x2C41);
            if (h % 61u == 0u) return DiamondOre;
            if (h % 23u == 0u) return AndromedaCrystal;
            return y < 34 ? Granite : Slate;
        }

        public static ushort Emberfall(int y, PlanetColumn column, int seed, int x, int z)
        {
            if (y > column.Height)
            {
                if (y <= EmberfallTerrain.SootSeaLevel) return Lava; // soot sea
                ushort sky = Overhang(Dimension.Emberfall, y, seed, x, z);
                return sky;
            }
            if (y == 0) return Bedrock;
            if (y == column.Height)
            {
                switch (column.Surface)
                {
                    case PlanetSurface.EmberMagmaShore: return EmberMagmaShore;
                    case PlanetSurface.EmberBasaltShelf: return Basalt;
                    case PlanetSurface.EmberAshIsland: return SootDune;
                    default: return SootDune;
                }
            }
            if (y > column.Height - 3) return column.Surface == PlanetSurface.EmberBasaltShelf ? Basalt : SootDune;
            if (y > column.Height - 8) return Basalt;
            return y < 30 ? Obsidian : Stone;
        }

        public static ushort Aquaria(int y, PlanetColumn column, int seed, int x, int z)
        {
            if (y > column.Height)
            {
                if (y <= AquariaTerrain.SeaLevel) return Water;
                return Overhang(Dimension.Aquaria, y, seed, x, z);
            }
            if (y == 0) return Bedrock;
            if (y == column.Height)
            {
                switch (column.Surface)
                {
                    case PlanetSurface.AquaKelpCathedral: return KelpWeave;
                    case PlanetSurface.AquaTidalRuin: return TidalRuin;
                    case PlanetSurface.AquaReefShelf: return ReefShelf;
                    case PlanetSurface.AquaAbyssTrench: return Slate;
                    default: return ReefShelf;
                }
            }
            if (y > column.Height - 4) return ReefShelf;
            if (y > column.Height - 10) return Limestone;
            return y < 26 ? Obsidian : Stone;
        }

        public static ushort Stormreach(int y, PlanetColumn column, int seed, int x, int z)
        {
            if (y > column.Height) return Overhang(Dimension.Stormreach, y, seed, x, z);
            if (y == 0) return Bedrock;
            if (y == column.Height)
            {
                switch (column.Surface)
                {
                    case PlanetSurface.StormChargeScar: return ChargeGlass;
                    case PlanetSurface.StormAnvilPeak: return StormIron;
                    case PlanetSurface.StormLatticePlateau: return WhiteTruss;
                    default: return Basalt;
                }
            }
            if (y > column.Height - 3) return Basalt;
            if (y > column.Height - 9) return Slate;
            return Stone;
        }

        public static ushort Verdant(int y, PlanetColumn column, int seed, int x, int z)
        {
            if (y > column.Height) return Overhang(Dimension.Verdant, y, seed, x, z);
            if (y == 0) return Bedrock;
            if (y == column.Height)
            {
                switch (column.Surface)
                {
                    case PlanetSurface.VerdantCapCanopy: return MyceliumCap;
                    case PlanetSurface.VerdantSporeMarsh: return SporeLoam;
                    case PlanetSurface.VerdantRootVault: return Wood;
                    default: return Grass;
                }
            }
            if (y > column.Height - 4) return SporeLoam;
            if (y > column.Height - 9) return Clay;
            return y < 28 ? Granite : Stone;
        }

        public static ushort Nihility(int y, PlanetColumn column, int seed, int x, int z)
        {
            if (y > column.Height) return Overhang(Dimension.Nihility, y, seed, x, z);
            if (y == 0) return Bedrock;
            if (y == column.Height)
            {
                switch (column.Surface)
                {
                    case PlanetSurface.VoidGlassScarp: return VoidShard;
                    case PlanetSurface.VoidInvertedFragment: return DreamMonolith;
                    case PlanetSurface.VoidNullBasin: return Trinitite;
                    default: return Obsidian;
                }
            }
            if (y > column.Height - 3) return Obsidian;
            if (y > column.Height - 7) return PaleStone;
            return y < 40 ? DreamMonolith : Stone;
        }

        // ------------------------------------------------------------------
        // Gates. Every frontier realm carries one deterministic arrival dais
        // (guaranteed near spawn) and one return gate per 192×192 sector, so a
        // player is never stranded and never has to walk more than ~96 blocks.
        // Pure integer maths — no Unity, no allocation.
        // ------------------------------------------------------------------

        static uint Hash(int x, int salt, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + z * 668265263 + seed * 2246822519u + salt * 1274126177);
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        /// <summary>Arrival dais centre for a realm (always the same 2×2 pad near spawn).</summary>
        public static void ArrivalPad(Dimension realm, int seed, out int x, out int z)
        {
            x = 0;
            z = -8;
        }

        /// <summary>
        /// Return gate centre for the 192×192 sector containing (nearX, nearZ). Pure function of
        /// the sector, so it is a fixed point: repeated lookups in the same sector agree, which is
        /// what makes the gate usable as a persistent landmark.
        /// </summary>
        public static void ReturnGate(Dimension realm, int nearX, int nearZ, int seed, out int gx, out int gz)
        {
            int cx = FrontierNoise.FloorDiv(nearX, 192), cz = FrontierNoise.FloorDiv(nearZ, 192);
            unchecked { seed ^= 0x5EA1; }
            uint h = Hash(cx, 300 + (int)realm, cz, seed);
            gx = cx * 192 + 48 + (int)(h % 96u);
            gz = cz * 192 + 48 + (int)((h >> 9) % 96u);
        }

        /// <summary>True on the sector gate, or on the arrival dais while still in the spawn region.</summary>
        public static bool IsReturnGate(Dimension realm, int x, int z, int seed)
        {
            ReturnGate(realm, x, z, seed, out int gx, out int gz);
            if (Math.Abs(x - gx) <= 1 && Math.Abs(z - gz) <= 1) return true;
            if (Math.Abs(x) < 96 && Math.Abs(z) < 96)
            {
                ArrivalPad(realm, seed, out int ax, out int az);
                return Math.Abs(x - ax) <= 1 && Math.Abs(z - az) <= 1;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Overhangs. A column sampler can only ever produce a solid stack, so every
        // "floating" feature (Emberfall sky islands, Stormreach lattice plates, Verdant
        // mushroom caps, Aquaria kelp canopies, Nihility inverted fragments, Crystalia
        // crystal arms) is generated here instead: one deterministic anchor per cell, one
        // ellipsoid per anchor. Cost is one hash per above-surface voxel and no neighbour
        // column lookups, so chunk generation stays cheap.
        // ------------------------------------------------------------------

        struct OverhangSpec
        {
            public int Cell;            // anchor spacing in blocks
            public int Count;           // anchors per cell (0 = none)
            public float Radius;        // horizontal radius
            public int Thickness;       // vertical half-thickness
            public int MinY, MaxY;      // band the feature may occupy
            public ushort Core, Shell;
            public int Salt;
        }

        static OverhangSpec SpecFor(Dimension realm)
        {
            switch (realm)
            {
                case Dimension.Crystalia:
                    // Shards of a shattered moon drifting above the spires.
                    return new OverhangSpec { Cell = 96, Count = 1, Radius = 13f, Thickness = 4, MinY = 84, MaxY = 108, Core = 115, Shell = 116, Salt = 0x9E21 };
                case Dimension.Emberfall:
                    // Basalt sky islands hanging over the soot sea, thick slabs with ash on top.
                    return new OverhangSpec { Cell = 96, Count = 2, Radius = 17f, Thickness = 6, MinY = 52, MaxY = 92, Core = 10, Shell = 117, Salt = 0x9E22 };
                case Dimension.Aquaria:
                    // Kelp canopies: pale fronds floating just under the surface.
                    return new OverhangSpec { Cell = 64, Count = 2, Radius = 10f, Thickness = 3, MinY = 38, MaxY = 74, Core = 119, Shell = 124, Salt = 0x9E23 };
                case Dimension.Stormreach:
                    // Charged lattice plates suspended in the static.
                    return new OverhangSpec { Cell = 96, Count = 2, Radius = 15f, Thickness = 3, MinY = 78, MaxY = 112, Core = 126, Shell = 127, Salt = 0x9E24 };
                case Dimension.Verdant:
                    // Mycelium caps: the whole point of the realm.
                    return new OverhangSpec { Cell = 64, Count = 1, Radius = 14f, Thickness = 5, MinY = 70, MaxY = 96, Core = 128, Shell = 129, Salt = 0x9E25 };
                default:
                    // Nihility: inverted fragments, thin and sharp.
                    return new OverhangSpec { Cell = 64, Count = 2, Radius = 11f, Thickness = 4, MinY = 58, MaxY = 104, Core = 133, Shell = 68, Salt = 0x9E26 };
            }
        }

        /// <summary>
        /// Returns the overhang block occupying (y,x,z) for this realm, or 0. Only called above
        /// the column surface, so ground generation is untouched.
        /// </summary>
        public static ushort Overhang(Dimension realm, int y, int seed, int x, int z)
        {
            var spec = SpecFor(realm);
            if (spec.Count <= 0 || y < spec.MinY || y > spec.MaxY) return 0;
            int cellX = FrontierNoise.FloorDiv(x, spec.Cell), cellZ = FrontierNoise.FloorDiv(z, spec.Cell);
            for (int i = 0; i < spec.Count; i++)
            {
                uint h = FrontierNoise.Hash(cellX, spec.Salt + i * 37, cellZ, seed);
                if (h % 7u == 0u) continue;                 // a few empty cells so the sky is not uniform
                float ax = cellX * spec.Cell + 10f + (h & 255u) / 255f * (spec.Cell - 20f);
                float az = cellZ * spec.Cell + 10f + ((h >> 8) & 255u) / 255f * (spec.Cell - 20f);
                int ay = spec.MinY + (int)((h >> 16) % (uint)Math.Max(1, spec.MaxY - spec.MinY - spec.Thickness));
                int dy = y - ay;
                if (dy < -spec.Thickness || dy > spec.Thickness) continue;
                float t = 1f - Math.Abs(dy) / (float)spec.Thickness;   // 1 at the core, 0 at the rim
                float radius = spec.Radius * (float)Math.Sqrt(Math.Max(0f, t));
                float dx = x - ax, dz = z - az;
                if (dx * dx + dz * dz > radius * radius) continue;
                // Outer third of the radius is the shell material, the middle is the core.
                return dx * dx + dz * dz > radius * radius * .55f ? spec.Shell : spec.Core;
            }
            return 0;
        }
        /// <summary>Footprint classification for a gate cell: whether it is in the pad at all,
        /// whether it is the raised rim, and the pad's plateau height.</summary>
        public struct GatePad
        {
            public bool Plan, Rim;
            public int Base;
        }

        /// <summary>
        /// Resolves the pad for the gate nearest (x,z). The whole 7×5 footprint is levelled to the
        /// lowest natural height inside it, so pillars never hang over a slope and the throat is
        /// always walkable. Deterministic and allocation-free.
        /// </summary>
        public static GatePad GatePlan(Dimension realm, int x, int z, int seed)
        {
            ReturnGate(realm, x, z, seed, out int gx, out int gz);
            int dx = x - gx, dz = z - gz;
            if (Math.Abs(dx) > 3 || Math.Abs(dz) > 2) return default(GatePad);
            int low = int.MaxValue;
            for (int ax = -3; ax <= 3; ax++)
                for (int az = -2; az <= 2; az++)
                {
                    int nat = NaturalHeight(realm, gx + ax, gz + az, seed);
                    if (nat < low) low = nat;
                }
            return new GatePad { Plan = true, Rim = Math.Abs(dx) > 2 || Math.Abs(dz) > 1, Base = low };
        }

        /// <summary>Natural (ungated) surface height for a realm, memo-free.</summary>
        static int NaturalHeight(Dimension realm, int x, int z, int seed)
        {
            switch (realm)
            {
                case Dimension.Crystalia: return CrystaliaTerrain.Column(x, z, seed).Height;
                case Dimension.Emberfall: return EmberfallTerrain.Column(x, z, seed).Height;
                case Dimension.Aquaria: return AquariaTerrain.Column(x, z, seed).Height;
                case Dimension.Stormreach: return StormreachTerrain.Column(x, z, seed).Height;
                case Dimension.Verdant: return VerdantTerrain.Column(x, z, seed).Height;
                default: return NihilityTerrain.Column(x, z, seed).Height;
            }
        }

        /// <summary>
        /// Gate geometry. The arrangement is the same in every realm: a lit 3×4 opening with
        /// two pillars and a lintel, standing on the local surface. Returns the block to place,
        /// or 0 when the cell is outside the structure; <paramref name="carved"/> tells the
        /// caller to punch air through whatever the terrain sampler produced there.
        /// </summary>
        public static ushort GateBlock(Dimension realm, int y, PlanetColumn column, int seed, int x, int z, out bool carved)
        {
            carved = false;
            ReturnGate(realm, x, z, seed, out int gx, out int gz);
            int dx = x - gx, dz = z - gz;
            if (Math.Abs(dx) > 2 || Math.Abs(dz) > 1) return 0;
            int baseY = GatePlan(realm, x, z, seed).Base + 1;
            int ry = y - baseY;
            if (ry < 0 || ry > 6) return 0;
            bool pillar = Math.Abs(dx) == 2;
            if (pillar) return ReturnGateBlock(realm);
            if (ry == 5) return ReturnGateBlock(realm);       // lintel
            carved = true;                                     // open the throat of the gate
            return 0;
        }

        /// <summary>Safe standing position just in front of a gate (never inside the wall).</summary>
        public static void GateStandingSpot(Dimension realm, int seed, out int x, out int z)
        {
            ReturnGate(realm, 0, 0, seed, out int gx, out int gz);
            x = gx;
            z = gz + 2;
        }

        /// <summary>Blocks under the arrival pad: a lit dais so the player lands on solid ground.</summary>
        public static ushort ArrivalPadBlock()
        {
            return PrismCrystal;
        }

        /// <summary>Block palette of the return gate; the realm's own stone, so it reads as native.</summary>
        public static ushort ReturnGateBlock(Dimension realm)
        {
            switch (realm)
            {
                case Dimension.Crystalia: return AuroraIce;
                case Dimension.Emberfall: return EmberMagmaShore;
                case Dimension.Aquaria: return TidalRuin;
                case Dimension.Stormreach: return ChargeGlass;
                case Dimension.Verdant: return MyceliumCap;
                default: return VoidShard;
            }
        }
    }
}

namespace LestingCraft.World
{
    /// <summary>Stable entry points for the frontier gate contract (used by checks and by gameplay).</summary>
    public static class FrontierTerrainCodes
    {
        public static void ArrivalPad(Dimension realm, int seed, out int x, out int z) { FrontierBlocks.ArrivalPad(realm, seed, out x, out z); }
        public static void ReturnGate(Dimension realm, int nearX, int nearZ, int seed, out int gx, out int gz) { FrontierBlocks.ReturnGate(realm, nearX, nearZ, seed, out gx, out gz); }
        public static bool IsReturnGate(Dimension realm, int x, int z, int seed) { return FrontierBlocks.IsReturnGate(realm, x, z, seed); }
        public static ushort GateBlock(Dimension realm, int y, PlanetColumn column, int seed, int x, int z, out bool carved) { return FrontierBlocks.GateBlock(realm, y, column, seed, x, z, out carved); }
        public static void GateStandingSpot(Dimension realm, int seed, out int x, out int z) { FrontierBlocks.GateStandingSpot(realm, seed, out x, out z); }
        public static FrontierBlocks.GatePad GatePlan(Dimension realm, int x, int z, int seed) { return FrontierBlocks.GatePlan(realm, x, z, seed); }
        public static ushort ArrivalPadBlock() { return FrontierBlocks.ArrivalPadBlock(); }
        public static ushort ReturnGateBlock(Dimension realm) { return FrontierBlocks.ReturnGateBlock(realm); }
    }
}
