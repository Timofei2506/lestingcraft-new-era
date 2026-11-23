using System;

namespace LestingCraft.World
{
    public enum PlanetBody
    {
        Moon = 1,
        Mars = 2,
        Venus = 3,
        Mercury = 4,
        Europa = 5,
        Titan = 6,
        Pluto = 7,
        ProximaB = 8,
        Trappist1e = 9,
        Kepler452b = 10,
        AndromedaPrime = 11,
        Somnia = 12,
        Dream = 13,
        Radia = 14,
        Backrooms = 15
    }

    public enum PlanetSurface : byte
    {
        Regolith, Mare, Ejecta, RedDust, IronRidge, Canyon, SulfurPlain, BasaltFlow, Caldera, ScorchedRock, Crater,
        EuropaIcePlain, EuropaChaosLineae,
        TitanHydroDune, TitanMethaneSea,
        PlutoHeartIce, PlutoGlacialRidge,
        ProximaCrimson, ProximaVent,
        TrappistLush, TrappistTidalBasin,
        KeplerHighland, KeplerGranitePeak,
        AndromedaCrystalField, AndromedaCrystalSpire,
        SomniaMarble, SomniaObsidianPillar,
        DreamPaleFloor, DreamMonolithPillar, DreamLampPost,
        RadiaBasinFloor, RadiaCherenkovStrip, RadiaDamWall, RadiaSlagBank
    }

    public struct PlanetColumn
    {
        public int Height;
        public PlanetSurface Surface;
    }

    public static class PlanetTerrain
    {
        public const int Version = 2;
        public const int RadiaWaterLevel = 50;

        static float N(float x, float z, int seed) { return MoonTerrain.ValueNoise(x, z, seed); }
        static float Clamp(float x, float min, float max) { return Math.Max(min, Math.Min(max, x)); }
        static float Smooth(float x) { x = Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
        static int Floor(float x) { return (int)Math.Floor(x); }

        static uint PlanetHash(int x, int y, int z, int seed)
        {
            uint h = (uint)(seed ^ (x * 374761393) ^ (y * 668265263) ^ (z * 2147483647));
            h = (h ^ (h >> 13)) * 1274126177u;
            return h ^ (h >> 16);
        }

        static bool IsCherenkovStripX(int x)
        {
            int ax = Math.Abs(x);
            return (ax >= 3 && ax <= 4) || (ax >= 10 && ax <= 11) || (ax >= 18 && ax <= 19) || (ax >= 27 && ax <= 28);
        }

        // Exact 3D double-helix tower & white lattice truss pier matching /home/user/uploads/image-1.png!
        static ushort SampleRadiaHelixTower(int x, int y, int z, float baseX, float baseZ, float leanPerMeter)
        {
            if (y < 44 || y > 152) return 0;

            // White steel lattice pier emerging from water behind the dam (y = 44..62)
            if (y <= 62)
            {
                float dx0 = Math.Abs(x - baseX), dz0 = Math.Abs(z - baseZ);
                if (dx0 > 6.6f || dz0 > 6.6f) return 0;
                bool cornerLeg = dx0 >= 4.4f && dz0 >= 4.4f;
                bool crossRing = (y == 49 || y == 55 || y == 61 || y == 62) && (dx0 >= 4.4f || dz0 >= 4.4f || (dx0 <= 1.6f && dz0 <= 1.6f));
                bool xBrace = (dx0 >= 4.4f || dz0 >= 4.4f) && Math.Abs(((x + z + y) % 5 + 5) % 5 - 2) <= 0;
                if (cornerLeg || crossRing || xBrace) return 82;
                if (dx0 <= 1.2f && dz0 <= 1.2f) return 78;
                return 0;
            }

            // Tilted colossal twin double-helix superstructure rising through the storm mist (y = 63..152)
            float t = y - 62f;
            float cx = baseX + t * leanPerMeter;
            float cz = baseZ - t * 0.03f;
            float radius = 6.8f;
            double theta = t * 0.135;

            for (int strand = 0; strand < 2; strand++)
            {
                double ang = theta + strand * Math.PI;
                float sx = cx + (float)Math.Cos(ang) * radius;
                float sz = cz + (float)Math.Sin(ang) * radius;
                float d2 = (x - sx) * (x - sx) + (z - sz) * (z - sz);
                if (d2 <= 5.2f)
                    return d2 <= 1.15f && (y % 4 == 0) ? (ushort)78 : (ushort)79;
            }

            // Horizontal structural cross-girders connecting the two helix strands every 6 meters
            if (y % 6 == 0)
            {
                double ang = theta;
                float ax = cx + (float)Math.Cos(ang) * radius, az = cz + (float)Math.Sin(ang) * radius;
                float bx = cx - (float)Math.Cos(ang) * radius, bz = cz - (float)Math.Sin(ang) * radius;
                float abx = bx - ax, abz = bz - az;
                float proj = Clamp(((x - ax) * abx + (z - az) * abz) / Math.Max(0.01f, abx * abx + abz * abz), 0f, 1f);
                float px = ax + abx * proj, pz = az + abz * proj;
                float distSq = (x - px) * (x - px) + (z - pz) * (z - pz);
                if (distSq <= 1.6f)
                    return Math.Abs(proj - 0.5f) < 0.14f ? (ushort)78 : (ushort)82;
            }
            return 0;
        }

        public static PlanetColumn Column(int x, int z, int seed, PlanetBody body)
        {
            if (body == PlanetBody.Moon)
            {
                var moon = MoonTerrain.Column(x, z, seed);
                return new PlanetColumn
                {
                    Height = moon.Height,
                    Surface = moon.Surface == MoonSurface.Mare ? PlanetSurface.Mare :
                              moon.Surface == MoonSurface.Ejecta ? PlanetSurface.Ejecta : PlanetSurface.Regolith
                };
            }

            float broad = N(x / 220f, z / 220f, seed ^ 17183);
            float medium = N(x / 61f, z / 61f, seed ^ 33749);
            float fine = N(x / 14f, z / 14f, seed ^ 54101);

            if (body == PlanetBody.Radia)
            {
                // Arrival jetty at (0, 51, -12) looking +Z across the dark flooded basin toward the Dam & Twin Helices!
                if (Math.Abs(x) <= 4 && z >= -18 && z <= -7)
                    return new PlanetColumn { Height = 51, Surface = PlanetSurface.RadiaDamWall };

                // Northern Brutalist Concrete Sluice Dam spanning the horizon (z = 68..83) + tall twin central sluice pylons!
                if (z >= 68 && z <= 83 && Math.Abs(x) <= 84)
                {
                    if (Math.Abs(x) >= 6 && Math.Abs(x) <= 10 && z <= 76)
                        return new PlanetColumn { Height = 68, Surface = PlanetSurface.RadiaDamWall };
                    if (z < 70)
                        return new PlanetColumn { Height = 47, Surface = PlanetSurface.RadiaBasinFloor };
                    if (Math.Abs(x) >= 34 && Math.Abs(x) <= 41)
                    {
                        int rampH = 50 + Math.Min(7, (z - 69));
                        return new PlanetColumn { Height = rampH, Surface = PlanetSurface.RadiaDamWall };
                    }
                    bool sluiceRecess = z == 70 && (Math.Abs(x) % 14 >= 5 && Math.Abs(x) % 14 <= 9);
                    return new PlanetColumn
                    {
                        Height = sluiceRecess ? 54 : 56,
                        Surface = PlanetSurface.RadiaDamWall
                    };
                }

                // Boss I (West Helix Plaza: -27, 56) & Boss II (East Helix Plaza: +31, 56) & Boss III (Central Dam Apron: 0, 65)
                float dWest = (x + 27f) * (x + 27f) + (z - 56f) * (z - 56f);
                float dEast = (x - 31f) * (x - 31f) + (z - 56f) * (z - 56f);
                float dCenter = x * x + (z - 65f) * (z - 65f);
                if (dWest <= 81f || dEast <= 81f || dCenter <= 64f)
                    return new PlanetColumn { Height = 51, Surface = PlanetSurface.RadiaDamWall };

                // Submerged parallel rows of glowing cyan Cherenkov fuel blocks under shallow dark water (y = 48..49 < WaterLevel 50)!
                if (z >= -26 && z <= 69 && Math.Abs(x) <= 34)
                {
                    if (IsCherenkovStripX(x) && (((z % 9) + 9) % 9 != 0))
                    {
                        int chH = (z < 10 && Math.Abs(x) <= 12) ? 49 : 48;
                        return new PlanetColumn { Height = chH, Surface = PlanetSurface.RadiaCherenkovStrip };
                    }
                    return new PlanetColumn { Height = 47, Surface = PlanetSurface.RadiaBasinFloor };
                }

                // Outer vitrified trinitite & ash islands where the 10 hostile mutant species prowl around the basin!
                float bank = 48.2f + (broad - 0.42f) * 9.5f + (medium - 0.5f) * 5.2f + (fine - 0.5f) * 2.0f;
                int bh = Math.Max(44, Math.Min(60, (int)Math.Round(bank)));
                if (bh <= 49 && IsCherenkovStripX(x) && z > -60 && z < 70)
                    return new PlanetColumn { Height = 48, Surface = PlanetSurface.RadiaCherenkovStrip };
                return new PlanetColumn
                {
                    Height = bh,
                    Surface = bh >= 50 ? PlanetSurface.RadiaSlagBank : PlanetSurface.RadiaBasinFloor
                };
            }

            if (body == PlanetBody.Dream)
            {
                if (Math.Abs(x) <= 2 && z == 46)
                    return new PlanetColumn
                    {
                        Height = 49,
                        Surface = Math.Abs(x) <= 1 ? PlanetSurface.DreamLampPost : PlanetSurface.DreamPaleFloor
                    };
                if (Math.Abs(x) < 7 && Math.Abs(z) < 9)
                    return new PlanetColumn { Height = 44, Surface = PlanetSurface.DreamPaleFloor };

                int gx = ((x % 16) + 16) % 16, gz = ((z % 16) + 16) % 16;
                uint dh = PlanetHash(Floor(x / 16f), 0, Floor(z / 16f), seed ^ 66613);
                int baseFloor = 44 + (int)Math.Round((broad - .5f) * 6f);
                if (gx <= 1 && gz <= 1)
                    return new PlanetColumn { Height = baseFloor + 9 + (int)(dh % 14), Surface = PlanetSurface.DreamMonolithPillar };
                if (gx == 8 && gz == 8 && (dh % 3) == 0)
                    return new PlanetColumn { Height = baseFloor + 5, Surface = PlanetSurface.DreamLampPost };
                return new PlanetColumn { Height = baseFloor, Surface = PlanetSurface.DreamPaleFloor };
            }

            float h;
            PlanetSurface surface;

            switch (body)
            {
                case PlanetBody.Mars:
                    float fold = Math.Abs(N(x / 300f, z / 170f, seed ^ 16217) - .5f);
                    float canyon = 1 - Smooth((fold - .045f) / .10f);
                    h = 51 + (broad - .5f) * 27 + (medium - .5f) * 14 + (fine - .5f) * 3 - canyon * 18;
                    surface = canyon > .64f ? PlanetSurface.Canyon : h > 51 ? PlanetSurface.IronRidge : PlanetSurface.RedDust;
                    break;
                case PlanetBody.Venus:
                    float flow = N(x / 105f, z / 105f, seed ^ 28411);
                    h = 48 + (broad - .5f) * 29 + (medium - .5f) * 10 + (fine - .5f) * 2;
                    surface = flow > .61f ? PlanetSurface.BasaltFlow : PlanetSurface.SulfurPlain;
                    int vx = Floor(x / 112f), vz = Floor(z / 112f);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            int cx = vx + dx, cz = vz + dz;
                            uint hash = MoonTerrain.Hash(cx, cz, seed ^ 61927);
                            if (hash % 5 != 0) continue;
                            float px = cx * 112 + 24 + (hash >> 8) % 64, pz = cz * 112 + 24 + (hash >> 16) % 64;
                            float d = (float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz));
                            if (d < 17)
                            {
                                float t = d / 17;
                                h += t < .72f ? -11 * (1 - Smooth(t / .72f)) : 5 * (1 - Smooth((t - .72f) / .28f));
                                if (t < .65f) surface = PlanetSurface.Caldera;
                            }
                        }
                    break;
                case PlanetBody.Mercury:
                    h = 43 + (broad - .5f) * 19 + (medium - .5f) * 11 + (fine - .5f) * 2;
                    surface = PlanetSurface.ScorchedRock;
                    int mx = Floor(x / 48f), mz = Floor(z / 48f);
                    for (int dx = -1; dx <= 1; dx++)
                        for (int dz = -1; dz <= 1; dz++)
                        {
                            var c = MoonTerrain.CraterInCell(mx + dx, mz + dz, seed ^ 78221);
                            if (!c.Present) continue;
                            float fx = x + .5f - c.X, fz = z + .5f - c.Z;
                            float d = (float)Math.Sqrt(fx * fx + fz * fz);
                            if (d < c.Radius * 3f)
                            {
                                h += MoonTerrain.CraterDelta(c, d);
                                if (d < c.Radius * 1.3f) surface = PlanetSurface.Crater;
                            }
                        }
                    break;
                case PlanetBody.Europa:
                    float crack = 1 - Smooth(Math.Abs(N(x / 85f, z / 85f, seed ^ 44123) - .5f) / .055f);
                    h = 44 + (broad - .5f) * 14 + (medium - .5f) * 6 + crack * 5;
                    surface = crack > .55f ? PlanetSurface.EuropaChaosLineae : PlanetSurface.EuropaIcePlain;
                    break;
                case PlanetBody.Titan:
                    float wave = (float)Math.Sin(x * .08f + broad * 4f) * 3.5f;
                    h = 44 + (broad - .5f) * 20 + wave + (fine - .5f) * 2;
                    surface = h <= 41 ? PlanetSurface.TitanMethaneSea : PlanetSurface.TitanHydroDune;
                    break;
                case PlanetBody.Pluto:
                    h = 45 + (broad - .5f) * 22 + (medium - .5f) * 8;
                    surface = h > 47 ? PlanetSurface.PlutoGlacialRidge : PlanetSurface.PlutoHeartIce;
                    break;
                case PlanetBody.ProximaB:
                    h = 46 + (broad - .5f) * 26 + (medium - .5f) * 11 + (fine - .5f) * 3;
                    surface = fine > .66f ? PlanetSurface.ProximaVent : PlanetSurface.ProximaCrimson;
                    break;
                case PlanetBody.Trappist1e:
                    h = 45 + (broad - .5f) * 25 + (medium - .5f) * 9;
                    surface = h < 42 ? PlanetSurface.TrappistTidalBasin : PlanetSurface.TrappistLush;
                    break;
                case PlanetBody.Kepler452b:
                    h = 48 + (broad - .5f) * 30 + (medium - .5f) * 14 + (fine - .5f) * 4;
                    surface = h > 53 ? PlanetSurface.KeplerGranitePeak : PlanetSurface.KeplerHighland;
                    break;
                case PlanetBody.AndromedaPrime:
                    uint sh = PlanetHash(x, 0, z, seed ^ 71777);
                    bool spire = sh % 47 == 0;
                    h = 47 + (broad - .5f) * 26 + (medium - .5f) * 12 + (fine - .5f) * 3 + (spire ? 4 + (sh % 4) : 0);
                    surface = spire || fine > .68f ? PlanetSurface.AndromedaCrystalSpire : PlanetSurface.AndromedaCrystalField;
                    break;
                case PlanetBody.Somnia:
                    uint mh = PlanetHash(x, 0, z, seed ^ 90909);
                    bool pillar = mh % 59 == 0;
                    h = 45 + (broad - .5f) * 20 + (medium - .5f) * 9 + (pillar ? 6 + (mh % 5) : 0);
                    surface = pillar || medium > .64f ? PlanetSurface.SomniaObsidianPillar : PlanetSurface.SomniaMarble;
                    break;
                case PlanetBody.Backrooms:
                    h = BackroomsTerrain.FloorTop(x, z, seed);
                    surface = BackroomsTerrain.IsRedRoom(x, z, seed) ? PlanetSurface.Ejecta : PlanetSurface.Regolith;
                    break;
                default:
                    h = 45 + (broad - .5f) * 18;
                    surface = PlanetSurface.Regolith;
                    break;
            }
            return new PlanetColumn { Height = Math.Max(12, Math.Min(105, (int)Math.Round(h))), Surface = surface };
        }

        public static ushort Block(int y, PlanetColumn column, PlanetBody body)
        {
            if (body == PlanetBody.Radia)
            {
                if (y < 0 || y >= 256 || y > column.Height) return 0;
                if (y == 0) return 9;
                if (column.Surface == PlanetSurface.RadiaCherenkovStrip) return y >= column.Height - 1 ? (ushort)78 : (ushort)27;
                if (column.Surface == PlanetSurface.RadiaDamWall) return 79;
                if (column.Surface == PlanetSurface.RadiaSlagBank) return y == column.Height ? (ushort)88 : (ushort)27;
                return 27;
            }
            return Block(0, y, 0, 2506, column, body);
        }

        // Deterministic 3D planetary & interdimensional structures across all non-Radia/Backrooms worlds.
        // Anchored on 128x128 grid cells outside the central landing/boss arena (|ax|>=56 || |az|>=56).
        public static bool TrySamplePlanetaryStructure(int x, int y, int z, int seed, PlanetBody body, out ushort placed)
        {
            placed = 0;
            if (body == PlanetBody.Radia || body == PlanetBody.Backrooms) return false;
            int cellX = Floor(x / 128f), cellZ = Floor(z / 128f);
            uint h = PlanetHash(cellX, (int)body * 13, cellZ, seed ^ 49157);
            if ((h % 10u) >= 7u) return false; // ~70% of outer cells host a landmark
            int ax = cellX * 128 + 36 + (int)((h >> 4) % 56u);
            int az = cellZ * 128 + 36 + (int)((h >> 12) % 56u);
            if (Math.Abs(ax) < 56 && Math.Abs(az) < 56) return false;
            int rx = x - ax, rz = z - az;
            if (Math.Abs(rx) > 13 || Math.Abs(rz) > 13) return false;

            int baseY = Column(ax, az, seed, body).Height;
            int ry = y - baseY;
            if (ry < -8 || ry > 24) return false;
            int adx = Math.Abs(rx), adz = Math.Abs(rz);

            // 1. Airless Worlds (Moon, Mercury): Geodesic Titanium Biodome + Solar Wing & Comms Mast
            if (body == PlanetBody.Moon || body == PlanetBody.Mercury)
            {
                float d2 = rx * rx + rz * rz;
                // Subsurface white-truss & concrete leveling pad (ry = -8..0) + Solar Wing at rx = 8..12
                if (d2 <= 68f)
                {
                    if (ry < 0) { placed = (adx % 3 == 0 || adz % 3 == 0) ? (ushort)82 : (ushort)79; return true; }
                    if (ry == 0) { placed = d2 <= 38f ? (ushort)40 : (ushort)80; return true; }
                }
                // South Pressurized Airlock Corridor (rz = -9..-6, adx <= 2)
                if (rz >= -9 && rz <= -6 && adx <= 2 && ry >= 0 && ry <= 4)
                {
                    if (ry == 0) { placed = 80; return true; }
                    if (adx <= 1 && ry >= 1 && ry <= 3) { placed = 0; return true; }
                    placed = ry == 4 ? (ushort)66 : (ushort)82;
                    return true;
                }
                // Hemispherical Geodesic Biodome (r <= 7.2, ry = 1..7)
                if (ry >= 1 && ry <= 7 && d2 <= 54f)
                {
                    float sphereR = (float)Math.Sqrt(rx * rx + rz * rz + (ry - 0.5f) * (ry - 0.5f));
                    if (sphereR <= 7.3f && sphereR >= 6.0f)
                    {
                        if (rz < -4 && adx <= 1 && ry <= 3) { placed = 0; return true; }
                        bool rib = adx <= 1 || adz <= 1 || ry == 1 || ry == 4;
                        placed = rib ? (ushort)66 : (ushort)84;
                        return true;
                    }
                    if (sphereR < 6.0f)
                    {
                        if (ry == 1 && rx == 0 && rz == 3) { placed = 89; return true; }
                        if (ry == 1 && rx == -3 && rz == 2) { placed = 16; return true; }
                        if (ry == 1 && rx == 3 && rz == 2) { placed = 73; return true; }
                        if (ry == 1 && rx == -3 && rz == -1) { placed = 55; return true; }
                        if (ry == 5 && rx == 0 && rz == 0) { placed = 77; return true; }
                        placed = 0;
                        return true;
                    }
                }
                // Comms Antenna Spire atop dome (rx=0, rz=0, ry=8..14)
                if (rx == 0 && rz == 0 && ry >= 8 && ry <= 14)
                {
                    placed = ry == 14 ? (ushort)77 : (ushort)82;
                    return true;
                }
                // East Solar Power Array Wing (rx = 8..12, adz <= 3)
                if (rx >= 8 && rx <= 12 && adz <= 3 && ry >= -6 && ry <= 2)
                {
                    if (ry < 0) { placed = (rx == 8 || rx == 12) && adz == 3 ? (ushort)82 : (ushort)0; return placed != 0; }
                    if (ry == 0) { placed = 82; return true; }
                    if (ry == 1) { placed = rx == 8 && rz == 0 ? (ushort)72 : (adz <= 2 && rx >= 9 ? (ushort)75 : (ushort)0); return true; }
                    placed = 0;
                    return true;
                }
                return false;
            }

            // 2. Harsh Atmospheric Colonies (Mars, Venus, Titan): Habitation Pod + 18m Lattice Drilling Derrick
            if (body == PlanetBody.Mars || body == PlanetBody.Venus || body == PlanetBody.Titan)
            {
                if (adx <= 11 && adz <= 9)
                {
                    if (ry < 0) { placed = 79; return true; }
                    if (ry == 0) { placed = (adx == 11 || adz == 9) ? (ushort)80 : (ushort)66; return true; }
                    // West Habitation Module (rx = -10..-1, adz <= 6, ry = 1..6)
                    if (rx >= -10 && rx <= -1 && adz <= 6 && ry <= 6)
                    {
                        bool wall = rx == -10 || rx == -1 || adz == 6;
                        if (rz == -6 && rx >= -6 && rx <= -5 && ry <= 3) { placed = 0; return true; }
                        if (ry == 6) { placed = (rx == -10 || rx == -1 || adz == 6) ? (ushort)80 : (ushort)66; return true; }
                        if (wall)
                        {
                            bool win = (ry == 2 || ry == 3) && (rx == -8 || rx == -3 || adz == 3);
                            placed = win ? (ushort)84 : (ry == 1 ? (ushort)80 : (ushort)66);
                            return true;
                        }
                        if (ry == 1 && rx == -9 && rz == 4) { placed = 74; return true; }
                        if (ry == 1 && rx == -7 && rz == 4) { placed = 73; return true; }
                        if (ry == 1 && rx == -3 && rz == 4) { placed = 17; return true; }
                        if (ry == 1 && rx == -9 && rz == -4) { placed = 55; return true; }
                        if (ry == 1 && rx == -3 && rz == -4) { placed = 16; return true; }
                        if (ry == 5 && rx == -5 && rz == 0) { placed = 77; return true; }
                        placed = 0;
                        return true;
                    }
                    // Cable bridge at rz = 0 (rx = 0..2, ry = 1)
                    if (rx >= 0 && rx <= 2 && rz == 0 && ry == 1) { placed = 72; return true; }
                    // East 18m Lattice Drilling Derrick (rx = 3..9, adz <= 3, ry = 1..18)
                    if (rx >= 3 && rx <= 9 && adz <= 3 && ry <= 18)
                    {
                        int drx = Math.Abs(rx - 6);
                        if (drx == 0 && adz == 0 && ry <= 15) { placed = ry == 1 ? (ushort)50 : (ushort)49; return true; }
                        int span = ry < 8 ? 3 : ry < 14 ? 2 : 1;
                        if ((drx == span && adz == span) || (ry % 5 == 0 && (drx == span || adz == span) && drx <= span && adz <= span))
                        {
                            placed = 82;
                            return true;
                        }
                        if (ry == 18 && drx == 0 && adz == 0) { placed = 77; return true; }
                        placed = 0;
                        return true;
                    }
                    if (ry <= 6) { placed = 0; return true; }
                }
                return false;
            }

            // 3. Cryo Worlds (Europa, Pluto): Glacial Cherenkov Research Spire
            if (body == PlanetBody.Europa || body == PlanetBody.Pluto)
            {
                float d = (float)Math.Sqrt(rx * rx + rz * rz);
                if (d <= 8.5f)
                {
                    if (ry < 0) { placed = 63; return true; }
                    if (ry == 0) { placed = d <= 6.5f ? (ushort)66 : (ushort)80; return true; }
                    if (ry <= 5 && d <= 7.2f)
                    {
                        if (rz < -4 && adx <= 1 && ry <= 3) { placed = 0; return true; }
                        if (d >= 5.8f) { placed = (ry == 2 || ry == 3) ? (ushort)84 : (ushort)66; return true; }
                        if (d <= 1.3f) { placed = 78; return true; }
                        if (ry == 1 && rx == -3 && rz == 2) { placed = 52; return true; }
                        if (ry == 1 && rx == 3 && rz == 2) { placed = 89; return true; }
                        placed = 0;
                        return true;
                    }
                    float spireR = 5.5f - (ry - 5) * 0.26f;
                    if (ry >= 6 && ry <= 20 && spireR > 0.6f && d <= spireR)
                    {
                        placed = d <= 1.1f ? (ushort)78 : (ry % 4 == 0 ? (ushort)66 : (ushort)63);
                        return true;
                    }
                    if (ry == 21 && rx == 0 && rz == 0) { placed = 77; return true; }
                }
                return false;
            }

            // 4. Exoplanets & Surreal Realms (ProximaB, Trappist1e, Kepler452b, AndromedaPrime, Somnia, Dream)
            if (adx <= 10 && adz <= 10)
            {
                ushort stoneA = body == PlanetBody.ProximaB ? (ushort)64 :
                                body == PlanetBody.AndromedaPrime ? (ushort)42 :
                                body == PlanetBody.Somnia ? (ushort)68 :
                                body == PlanetBody.Dream ? (ushort)69 : (ushort)41;
                ushort accent = body == PlanetBody.AndromedaPrime ? (ushort)67 :
                                body == PlanetBody.Somnia ? (ushort)31 :
                                body == PlanetBody.Dream ? (ushort)42 : (ushort)30;
                ushort coreGlow = body == PlanetBody.Somnia ? (ushort)78 :
                                  body == PlanetBody.Dream ? (ushort)56 : (ushort)67;

                if (ry < 0) { placed = stoneA; return true; }
                // 3-stepped Ziggurat Terrace (ry = 0..2) with cardinal stairs
                int tier = ry == 0 ? 10 : ry == 1 ? 8 : ry == 2 ? 6 : -1;
                if (tier >= 0)
                {
                    bool stair = (adx <= 1 && adz <= 10 - ry) || (adz <= 1 && adx <= 10 - ry);
                    if (adx <= tier && adz <= tier || stair)
                    {
                        placed = (adx == tier || adz == tier) ? accent : stoneA;
                        return true;
                    }
                    placed = 0;
                    return true;
                }
                // 4 Corner Monolith Columns + Overhead Entablature Ring (ry = 3..11) + Soaring Central Spire/Arch
                if (adx == 5 && adz == 5 && ry >= 3 && ry <= 11)
                {
                    placed = ry == 11 ? (ushort)11 : accent;
                    return true;
                }
                if ((adx == 5 && adz <= 5 || adz == 5 && adx <= 5) && (ry == 9 || ry == 10))
                {
                    placed = stoneA;
                    return true;
                }
                // Twin central Precursor Portal Arches & Levitating Resonance Crystal
                if (ry >= 3 && ry <= 15 && adx == 2 && adz == 0) { placed = accent; return true; }
                if (ry >= 14 && ry <= 16 && adx <= 2 && adz <= 1) { placed = stoneA; return true; }
                if (rx == 0 && rz == 0 && ry >= 6 && ry <= 11) { placed = coreGlow; return true; }
                if (rx == 0 && rz == 0 && ry == 17) { placed = 11; return true; }
                if (ry == 3 && adx == 3 && adz == 3 && (body == PlanetBody.Trappist1e || body == PlanetBody.Kepler452b))
                {
                    placed = 61;
                    return true;
                }
                if (ry <= 12) { placed = 0; return true; }
            }
            return false;
        }

        public static ushort Block(int x, int y, int z, int seed, PlanetColumn column, PlanetBody body)
        {
            if (y < 0 || y >= 256) return 0;
            if (y == 0) return 9;
            if (body == PlanetBody.Backrooms) return BackroomsTerrain.Block(x, y, z, seed, column.Height);

            if (TrySamplePlanetaryStructure(x, y, z, seed, body, out ushort structBlock))
                return structBlock;

            if (body == PlanetBody.Radia)
            {
                // Twin tilted double-helix towers behind the dam (West at x=-32,z=94 leaning +0.135; East at x=+35,z=90 leaning -0.145)
                ushort westHelix = SampleRadiaHelixTower(x, y, z, -32f, 94f, 0.135f);
                if (westHelix != 0) return westHelix;
                ushort eastHelix = SampleRadiaHelixTower(x, y, z, 35f, 90f, -0.145f);
                if (eastHelix != 0) return eastHelix;

                // Central Sluice Purge Terminal & Field Supply Workbench on arrival jetty
                if (x == 0 && z == 73 && y == 57) return 89;
                if ((x == -2 || x == 2) && z == 73 && y == 57) return 77;
                if (x == 3 && z == -14 && y == 52) return 11;
                if (x == -3 && z == -14 && y == 52) return 16;

                if (y > column.Height)
                    return y <= RadiaWaterLevel ? (ushort)7 : (ushort)0;

                if (column.Surface == PlanetSurface.RadiaCherenkovStrip)
                    return y >= column.Height - 1 ? (ushort)78 : (ushort)27;
                if (column.Surface == PlanetSurface.RadiaDamWall)
                {
                    if (y == column.Height && Math.Abs(x) % 8 == 0) return 80;
                    return 79;
                }
                if (column.Surface == PlanetSurface.RadiaSlagBank)
                    return y == column.Height ? (ushort)88 : (ushort)27;
                return 27;
            }

            if (y > column.Height) return 0;

            if (body == PlanetBody.Dream)
            {
                if (column.Surface == PlanetSurface.DreamMonolithPillar)
                    return 68;
                if (column.Surface == PlanetSurface.DreamLampPost)
                    return y == column.Height ? (ushort)56 : y <= 44 ? (ushort)69 : (ushort)4;
                return y == column.Height ? (ushort)69 : (ushort)68;
            }

            if (y < column.Height - 5) return body == PlanetBody.Venus ? (ushort)10 : (ushort)1;
            if (y < column.Height - 2) return body == PlanetBody.Mars ? (ushort)43 : body == PlanetBody.Venus ? (ushort)10 : (ushort)1;
            switch (column.Surface)
            {
                case PlanetSurface.Mare:
                case PlanetSurface.BasaltFlow:
                case PlanetSurface.Caldera: return 10;
                case PlanetSurface.IronRidge: return 43;
                case PlanetSurface.Canyon:
                case PlanetSurface.RedDust:
                case PlanetSurface.SulfurPlain: return 6;
                case PlanetSurface.Crater: return 29;
                case PlanetSurface.EuropaIcePlain:
                case PlanetSurface.PlutoGlacialRidge: return 63;
                case PlanetSurface.EuropaChaosLineae: return 11;
                case PlanetSurface.TitanHydroDune: return 43;
                case PlanetSurface.TitanMethaneSea: return 10;
                case PlanetSurface.PlutoHeartIce: return y == column.Height ? (ushort)14 : (ushort)63;
                case PlanetSurface.ProximaCrimson: return 62;
                case PlanetSurface.ProximaVent: return 64;
                case PlanetSurface.TrappistLush: return y == column.Height ? (ushort)65 : (ushort)2;
                case PlanetSurface.TrappistTidalBasin: return 35;
                case PlanetSurface.KeplerHighland: return y == column.Height ? (ushort)61 : (ushort)35;
                case PlanetSurface.KeplerGranitePeak: return 27;
                case PlanetSurface.AndromedaCrystalField: return 65;
                case PlanetSurface.AndromedaCrystalSpire: return 67;
                case PlanetSurface.SomniaMarble: return 69;
                case PlanetSurface.SomniaObsidianPillar: return 68;
                default: return 32;
            }
        }
    }
}
