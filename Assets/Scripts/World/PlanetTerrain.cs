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
        Radia = 14
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

        public static ushort Block(int x, int y, int z, int seed, PlanetColumn column, PlanetBody body)
        {
            if (y < 0 || y >= 256) return 0;
            if (y == 0) return 9;

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
