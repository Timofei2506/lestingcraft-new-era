using System;
using System.Collections.Generic;

namespace LestingCraft.Gameplay
{
    /// <summary>Spectral class of a procedurally generated star.</summary>
    public enum StarKind { BrownDwarf, RedDwarf, OrangeDwarf, YellowDwarf, WhiteStar, BlueGiant, RedGiant, WhiteDwarf, NeutronStar, BlackHole }

    /// <summary>Surface/atmospheric archetype of a procedurally generated world.</summary>
    public enum WorldClass { Rock, Desert, Ocean, Ice, Lava, Toxic, GasGiant, IceGiant, Barren, Rogue }

    /// <summary>Weather system. Intensity is a separate 0..1 value, so any world can be calm or violent.</summary>
    public enum WeatherKind { Clear, Dust, Rain, Snow, Storm, AcidRain, Ashfall, SolarFlare, Hail, IonStorm }

    /// <summary>Something worth flying to inside a star system.</summary>
    public enum StructureKind { Station, Derelict, AsteroidField, Monolith, Wormhole, PirateOutpost, Comet, Probe, Ruin }

    /// <summary>Stable identity of one 5 light-year sector of the galaxy lattice.</summary>
    public struct UniverseKey : IEquatable<UniverseKey>
    {
        public readonly long X, Y, Z;
        public UniverseKey(long x, long y, long z) { X = x; Y = y; Z = z; }
        public bool Equals(UniverseKey other) { return X == other.X && Y == other.Y && Z == other.Z; }
        public override bool Equals(object obj) { return obj is UniverseKey && Equals((UniverseKey)obj); }
        public override int GetHashCode() { unchecked { return (int)(X * 73856093L ^ Y * 19349663L ^ Z * 83492791L); } }
        public override string ToString() { return X + "/" + Y + "/" + Z; }
    }

    /// <summary>Deterministic, allocation-free xorshift generator: the same key always rebuilds the same universe.</summary>
    public sealed class UniverseRng
    {
        ulong state;
        public UniverseRng(params long[] salt)
        {
            ulong h = 0x9E3779B97F4A7C15UL;
            for (int i = 0; i < salt.Length; i++)
            {
                unchecked { h ^= (ulong)salt[i] + 0x9E3779B97F4A7C15UL + (h << 6) + (h >> 2); h *= 0xBF58476D1CE4E5B9UL; }
            }
            state = h == 0 ? 0x4D595DF4D0F33173UL : h;
            for (int i = 0; i < 4; i++) NextUInt();
        }
        public uint NextUInt()
        {
            unchecked
            {
                state ^= state << 13; state ^= state >> 7; state ^= state << 17;
                return (uint)(state >> 32);
            }
        }
        /// <summary>Uniform in [0,1).</summary>
        public float NextFloat() { return (NextUInt() >> 8) / 16777216f; }
        public double NextDouble() { return (NextUInt() >> 8) / 16777216.0; }
        public float Range(float min, float max) { return min + (max - min) * NextFloat(); }
        public double Range(double min, double max) { return min + (max - min) * NextDouble(); }
        public int Range(int minInclusive, int maxExclusive)
        {
            if (maxExclusive <= minInclusive) return minInclusive;
            return minInclusive + (int)(NextUInt() % (uint)(maxExclusive - minInclusive));
        }
        public bool Chance(float probability) { return NextFloat() < probability; }
        public T Pick<T>(T[] items) { return items[Range(0, items.Length)]; }
    }

    /// <summary>One star and its immediate neighbourhood, rebuilt on demand from its lattice key.</summary>
    public struct UniverseSystem
    {
        public UniverseKey Key;
        public string Name;
        public StarKind Star;
        public float StarR, StarG, StarB;
        public double StarRadiusKm;
        public double TemperatureK;
        public bool Binary;
        public int PlanetCount;
        public bool HasBlackHole;
        public bool HasNebula;
        public bool HasGalaxyCore;
        public float NebulaR, NebulaG, NebulaB;
        /// <summary>Absolute position in the shared double-precision kilometre frame.</summary>
        public SpaceVector AnchorKm;
        public string SpectralCode;
    }

    /// <summary>A world with its own atmosphere, weather and hazard level.</summary>
    public struct UniversePlanet
    {
        public string Name;
        public WorldClass Class;
        public double RadiusKm;
        public double OrbitAu;
        public double OrbitPhaseRad;
        public double OrbitDays;
        public float InclinationDeg;
        public float GravityG;
        public double TemperatureC;
        public WeatherKind Weather;
        public float WeatherIntensity;
        public float AtmosphereDensity;
        public float AtmoR, AtmoG, AtmoB;
        public float SurfaceR, SurfaceG, SurfaceB;
        public float SecondaryR, SecondaryG, SecondaryB;
        public bool HasRings;
        public double RingInnerScale, RingOuterScale;
        public int MoonCount;
        public bool HasLife;
        /// <summary>0 (safe) .. 5 (lethal). Drives spawn pressure for hostile encounters.</summary>
        public int Hazard;
        public int Seed;
    }

    /// <summary>Point of interest: station, wreck, belt, monolith, wormhole, comet, probe or ruin.</summary>
    public struct UniverseStructure
    {
        public StructureKind Kind;
        public string Name;
        public SpaceVector OffsetKm;
        public double SizeKm;
        public float R, G, B;
    }

    /// <summary>A distant galaxy on the sky: name, direction and angular size.</summary>
    public struct UniverseGalaxy
    {
        public string Name;
        public string Type;
        public double X, Y, Z;
        public double DistanceLy;
        public float Size;
        public float R, G, B;
    }

    /// <summary>
    /// Endless procedural universe on a 5 light-year lattice. Every queried sector is derived
    /// from its integer key, so the galaxy is infinite, deterministic and never saved.
    /// This is generation logic only: it never moves the ship and never loads a scene.
    /// </summary>
    public static class ProceduralUniverse
    {
        public const double LightYearKm = 9460730472580800.0;
        public const double SectorLightYears = 5.0;
        public const double SectorKm = SectorLightYears * LightYearKm;

        static readonly string[] StarPrefix =
        {
            "Каэль", "Тарн", "Вега", "Иррен", "Соль", "Мира", "Альт", "Нокс", "Зарр", "Элион",
            "Ксар", "Тейн", "Вирр", "Омик", "Ярра", "Дрейк", "Халл", "Нэйр", "Квен", "Улл"
        };
        static readonly string[] StarSuffix = { "а", "ис", "ор", "ум", "эй", "ан", "ия", "ур", "ос", "ай" };
        static readonly string[] PlanetPrefix =
        {
            "Арк", "Гел", "Дор", "Икс", "Кер", "Лум", "Мор", "Неф", "Орб", "Пир",
            "Рей", "Сал", "Тер", "Улл", "Фер", "Хор", "Цир", "Шад", "Эб", "Юлл"
        };
        static readonly string[] PlanetSuffix =
        {
            "а", "он", "ис", "ум", "ей", "ар", "ия", "ос", "ин", "ус", "ай", "эль", "орн", "ет"
        };
        static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X", "XI", "XII" };

        static readonly Color3[] StarColors =
        {
            new Color3(.62f, .42f, .30f), new Color3(1f, .48f, .28f), new Color3(1f, .72f, .42f),
            new Color3(1f, .94f, .74f), new Color3(.96f, .97f, 1f), new Color3(.62f, .76f, 1f),
            new Color3(1f, .40f, .30f), new Color3(.88f, .92f, 1f), new Color3(.70f, .86f, 1f),
            new Color3(.12f, .10f, .16f)
        };

        struct Color3
        {
            public readonly float R, G, B;
            public Color3(float r, float g, float b) { R = r; G = g; B = b; }
        }

        public static long CellOf(double coordinateKm)
        {
            return (long)Math.Floor(coordinateKm / SectorKm);
        }

        public static UniverseKey KeyAt(SpaceVector positionKm)
        {
            return new UniverseKey(CellOf(positionKm.X), CellOf(positionKm.Y), CellOf(positionKm.Z));
        }

        static string StarName(UniverseRng rng, long x, long y, long z)
        {
            int catalogue = (int)(Math.Abs((x * 31 + y * 17 + z * 7) % 8999) + 1000);
            return rng.Pick(StarPrefix) + rng.Pick(StarSuffix) + "-" + catalogue;
        }

        static string PlanetName(UniverseRng rng)
        {
            return rng.Pick(PlanetPrefix) + rng.Pick(PlanetSuffix);
        }

        /// <summary>Star, planets and sky features of one lattice cell. Pure and deterministic.</summary>
        public static UniverseSystem System(long x, long y, long z)
        {
            var rng = new UniverseRng(x, y, z, 0x5EC702);
            var sys = new UniverseSystem { Key = new UniverseKey(x, y, z) };
            // Anchor stays consistent with SpaceMap.Star so navigation and rendering agree.
            sys.AnchorKm = SpaceMap.Star(x, y, z, 2506);

            float roll = rng.NextFloat();
            if (roll < 0.055f) sys.Star = StarKind.BlackHole;
            else if (roll < 0.085f) sys.Star = StarKind.NeutronStar;
            else if (roll < 0.135f) sys.Star = StarKind.WhiteDwarf;
            else if (roll < 0.205f) sys.Star = StarKind.BrownDwarf;
            else if (roll < 0.375f) sys.Star = StarKind.RedDwarf;
            else if (roll < 0.505f) sys.Star = StarKind.OrangeDwarf;
            else if (roll < 0.655f) sys.Star = StarKind.YellowDwarf;
            else if (roll < 0.765f) sys.Star = StarKind.WhiteStar;
            else if (roll < 0.885f) sys.Star = StarKind.BlueGiant;
            else sys.Star = StarKind.RedGiant;

            var colour = StarColors[(int)sys.Star];
            sys.StarR = colour.R; sys.StarG = colour.G; sys.StarB = colour.B;
            sys.Binary = sys.Star != StarKind.BlackHole && rng.Chance(0.22f);

            switch (sys.Star)
            {
                case StarKind.BlackHole: sys.StarRadiusKm = rng.Range(18.0, 62.0); sys.TemperatureK = 0; break;
                case StarKind.NeutronStar: sys.StarRadiusKm = rng.Range(9.0, 14.0); sys.TemperatureK = rng.Range(600000, 1600000); break;
                case StarKind.WhiteDwarf: sys.StarRadiusKm = rng.Range(6000, 14000); sys.TemperatureK = rng.Range(9000, 36000); break;
                case StarKind.BrownDwarf: sys.StarRadiusKm = rng.Range(58000, 96000); sys.TemperatureK = rng.Range(700, 2400); break;
                case StarKind.RedDwarf: sys.StarRadiusKm = rng.Range(120000, 320000); sys.TemperatureK = rng.Range(2600, 3900); break;
                case StarKind.OrangeDwarf: sys.StarRadiusKm = rng.Range(420000, 620000); sys.TemperatureK = rng.Range(3900, 5200); break;
                case StarKind.YellowDwarf: sys.StarRadiusKm = rng.Range(560000, 780000); sys.TemperatureK = rng.Range(5200, 6200); break;
                case StarKind.WhiteStar: sys.StarRadiusKm = rng.Range(820000, 1400000); sys.TemperatureK = rng.Range(7500, 11500); break;
                case StarKind.BlueGiant: sys.StarRadiusKm = rng.Range(3600000, 9200000); sys.TemperatureK = rng.Range(11000, 31000); break;
                default: sys.StarRadiusKm = rng.Range(24000000, 92000000); sys.TemperatureK = rng.Range(3200, 4700); break;
            }

            sys.SpectralCode = sys.Star == StarKind.BlackHole ? "BH"
                : sys.Star == StarKind.NeutronStar ? "PSR"
                : sys.Star == StarKind.WhiteDwarf ? "D"
                : sys.Star == StarKind.BrownDwarf ? "T"
                : sys.Star == StarKind.RedDwarf ? "M" + rng.Range(1, 9)
                : sys.Star == StarKind.OrangeDwarf ? "K" + rng.Range(1, 8)
                : sys.Star == StarKind.YellowDwarf ? "G" + rng.Range(1, 9)
                : sys.Star == StarKind.WhiteStar ? "A" + rng.Range(1, 9)
                : sys.Star == StarKind.BlueGiant ? "B" + rng.Range(1, 9)
                : "M" + rng.Range(1, 4) + "III";

            sys.Name = StarName(rng, x, y, z);
            // Dead stars rarely keep a planetary family; giants and black holes still may.
            int baseCount = sys.Star == StarKind.BlackHole ? rng.Range(0, 4)
                : sys.Star == StarKind.NeutronStar ? rng.Range(0, 4)
                : sys.Star == StarKind.WhiteDwarf ? rng.Range(0, 5)
                : rng.Range(2, 9);
            sys.PlanetCount = baseCount;
            sys.HasBlackHole = sys.Star == StarKind.BlackHole;
            sys.HasNebula = rng.Chance(0.24f);
            sys.HasGalaxyCore = Math.Abs(x) < 2 && Math.Abs(y) < 2 && Math.Abs(z) < 2;
            if (sys.HasNebula)
            {
                sys.NebulaR = rng.Range(.18f, .85f);
                sys.NebulaG = rng.Range(.12f, .62f);
                sys.NebulaB = rng.Range(.42f, 1f);
            }
            return sys;
        }

        public static UniverseSystem SystemAt(SpaceVector positionKm)
        {
            var key = KeyAt(positionKm);
            return System(key.X, key.Y, key.Z);
        }

        /// <summary>Worlds of one system. Orbit radii, weather and atmosphere all vary per seed.</summary>
        public static UniversePlanet[] Planets(UniverseSystem system)
        {
            int count = Math.Max(0, system.PlanetCount);
            var planets = new UniversePlanet[count];
            if (count == 0) return planets;
            var rng = new UniverseRng(system.Key.X, system.Key.Y, system.Key.Z, 0x9A17E7, 7717);
            double orbit = rng.Range(0.09, 0.55);
            for (int i = 0; i < count; i++)
            {
                var r = new UniverseRng(system.Key.X, system.Key.Y, system.Key.Z, 0x9A17E7, i * 2654435761L + 11);
                var p = new UniversePlanet { Name = PlanetName(r) + " " + Roman[i % Roman.Length] };
                p.Seed = (int)(r.NextUInt() & 0x7ffffff);
                // Hot close worlds, temperate middle, frozen outer: a physically sensible ladder.
                double au = orbit;
                orbit *= r.Range(1.35f, 1.95f);
                if (double.IsNaN(au) || au <= 0) au = 0.3;
                p.OrbitAu = Math.Min(au, 48.0);
                p.OrbitDays = Math.Max(0.4, 365.25 * Math.Pow(p.OrbitAu, 1.5) * r.Range(.82f, 1.24f));
                p.OrbitPhaseRad = r.Range(0.0, Math.PI * 2.0);
                p.InclinationDeg = r.Range(-9f, 9f);

                float cls = r.NextFloat();
                if (p.OrbitAu < 0.45) p.Class = cls < .42f ? WorldClass.Lava : cls < .74f ? WorldClass.Toxic : WorldClass.Rock;
                else if (p.OrbitAu < 0.95) p.Class = cls < .30f ? WorldClass.Desert : cls < .58f ? WorldClass.Rock : cls < .82f ? WorldClass.Ocean : WorldClass.Toxic;
                else if (p.OrbitAu < 3.4) p.Class = cls < .22f ? WorldClass.Rock : cls < .38f ? WorldClass.Desert : cls < .60f ? WorldClass.Ocean : cls < .78f ? WorldClass.Toxic : cls < .92f ? WorldClass.GasGiant : WorldClass.Ice;
                else p.Class = cls < .34f ? WorldClass.Ice : cls < .58f ? WorldClass.IceGiant : cls < .84f ? WorldClass.GasGiant : WorldClass.Barren;
                if (r.Chance(0.03f)) p.Class = WorldClass.Rogue;

                double radius = 0;
                switch (p.Class)
                {
                    case WorldClass.Rock: radius = r.Range(2100, 9100); break;
                    case WorldClass.Desert: radius = r.Range(2600, 11200); break;
                    case WorldClass.Ocean: radius = r.Range(4200, 13800); break;
                    case WorldClass.Ice: radius = r.Range(1800, 9600); break;
                    case WorldClass.Lava: radius = r.Range(2200, 8800); break;
                    case WorldClass.Toxic: radius = r.Range(3100, 12600); break;
                    case WorldClass.GasGiant: radius = r.Range(24000, 78000); break;
                    case WorldClass.IceGiant: radius = r.Range(16000, 38000); break;
                    case WorldClass.Barren: radius = r.Range(580, 2600); break;
                    default: radius = r.Range(1500, 7200); break;
                }
                p.RadiusKm = radius;
                p.GravityG = (float)Math.Max(0.03, Math.Min(4.2, radius / 6371.0 * (p.Class == WorldClass.GasGiant ? 1.9 : 1.0)));

                // Atmosphere: thick for giants and ocean worlds, none for barren rocks and rogues.
                float atmo = p.Class == WorldClass.GasGiant || p.Class == WorldClass.IceGiant ? r.Range(.82f, 1f)
                    : p.Class == WorldClass.Barren || p.Class == WorldClass.Rogue ? 0f
                    : p.Class == WorldClass.Ocean ? r.Range(.45f, .95f)
                    : p.Class == WorldClass.Ice ? r.Range(.05f, .6f)
                    : r.Range(.02f, .72f);
                if (system.Star == StarKind.BlackHole) atmo *= 0.4f;
                p.AtmosphereDensity = atmo;
                if (atmo > 0.01f)
                {
                    // Hue by class, brightness by density: blue oceans, ochre deserts, green poison.
                    float hue = r.NextFloat();
                    if (p.Class == WorldClass.Ocean) { p.AtmoR = .30f; p.AtmoG = .55f + hue * .18f; p.AtmoB = 1f; }
                    else if (p.Class == WorldClass.Desert) { p.AtmoR = 1f; p.AtmoG = .66f + hue * .12f; p.AtmoB = .38f; }
                    else if (p.Class == WorldClass.Toxic) { p.AtmoR = .52f; p.AtmoG = .92f; p.AtmoB = .46f; }
                    else if (p.Class == WorldClass.Lava) { p.AtmoR = 1f; p.AtmoG = .38f; p.AtmoB = .22f; }
                    else if (p.Class == WorldClass.Ice || p.Class == WorldClass.IceGiant) { p.AtmoR = .68f; p.AtmoG = .86f; p.AtmoB = 1f; }
                    else if (p.Class == WorldClass.GasGiant) { p.AtmoR = .92f; p.AtmoG = .76f; p.AtmoB = .58f + hue * .3f; }
                    else { p.AtmoR = .55f + hue * .3f; p.AtmoG = .60f + hue * .2f; p.AtmoB = .72f + hue * .2f; }
                }

                // Temperature from stellar flux; a black hole leaves only tidal leftovers.
                double flux = system.Star == StarKind.BlackHole ? 0.004 : (system.TemperatureK / 5772.0) * (system.TemperatureK / 5772.0) / Math.Max(0.02, p.OrbitAu * p.OrbitAu);
                double equilibrium = 278.0 * Math.Pow(Math.Max(1e-6, flux), 0.25) - 273.15;
                if (p.Class == WorldClass.Lava) equilibrium += r.Range(180, 640);
                if (p.Class == WorldClass.Ice || p.Class == WorldClass.IceGiant) equilibrium -= r.Range(40, 150);
                p.TemperatureC = Math.Max(-273.0, Math.Min(1200.0, equilibrium));

                // Weather: the colder, the more snow; the hotter, the more ash and flare.
                float w = r.NextFloat();
                if (p.AtmosphereDensity < 0.02f) p.Weather = WeatherKind.Clear;
                else if (p.TemperatureC < -25) p.Weather = w < .52f ? WeatherKind.Snow : w < .72f ? WeatherKind.Hail : w < .86f ? WeatherKind.Storm : WeatherKind.Clear;
                else if (p.TemperatureC > 160) p.Weather = w < .40f ? WeatherKind.Ashfall : w < .62f ? WeatherKind.SolarFlare : w < .80f ? WeatherKind.Dust : WeatherKind.Clear;
                else if (p.Class == WorldClass.Toxic) p.Weather = w < .58f ? WeatherKind.AcidRain : w < .80f ? WeatherKind.IonStorm : WeatherKind.Storm;
                else if (p.Class == WorldClass.Desert) p.Weather = w < .64f ? WeatherKind.Dust : w < .84f ? WeatherKind.Storm : WeatherKind.Clear;
                else if (p.Class == WorldClass.Ocean) p.Weather = w < .62f ? WeatherKind.Rain : w < .80f ? WeatherKind.Storm : WeatherKind.Clear;
                else p.Weather = w < .40f ? WeatherKind.Rain : w < .58f ? WeatherKind.Snow : w < .74f ? WeatherKind.Storm : w < .86f ? WeatherKind.Dust : WeatherKind.Clear;
                p.WeatherIntensity = p.Weather == WeatherKind.Clear ? 0f : r.Range(.25f, 1f);

                p.HasRings = (p.Class == WorldClass.GasGiant || p.Class == WorldClass.IceGiant) ? r.Chance(.62f) : r.Chance(.07f);
                if (p.HasRings)
                {
                    p.RingInnerScale = r.Range(1.25, 1.65);
                    p.RingOuterScale = p.RingInnerScale + r.Range(0.5, 1.5);
                }
                p.MoonCount = p.Class == WorldClass.GasGiant || p.Class == WorldClass.IceGiant ? r.Range(1, 7) : r.Range(0, 3);
                p.HasLife = p.AtmosphereDensity > .35f && p.TemperatureC > -18 && p.TemperatureC < 62 && r.Chance(.55f);
                p.Hazard = (p.Class == WorldClass.Lava ? 4 : p.Class == WorldClass.Toxic ? 3 : p.HasLife ? 1 : 2)
                    + (p.WeatherIntensity > .8f ? 1 : 0);
                if (p.HasLife) p.Hazard = Math.Min(p.Hazard, 2);
                p.Hazard = Math.Max(0, Math.Min(5, p.Hazard));

                // Surface palette: readable even from orbit.
                if (p.Class == WorldClass.Ocean) { p.SurfaceR = .10f; p.SurfaceG = .32f; p.SurfaceB = .62f; }
                else if (p.Class == WorldClass.Desert) { p.SurfaceR = .82f; p.SurfaceG = .62f; p.SurfaceB = .34f; }
                else if (p.Class == WorldClass.Ice) { p.SurfaceR = .82f; p.SurfaceG = .90f; p.SurfaceB = .98f; }
                else if (p.Class == WorldClass.Lava) { p.SurfaceR = .34f; p.SurfaceG = .11f; p.SurfaceB = .08f; }
                else if (p.Class == WorldClass.Toxic) { p.SurfaceR = .32f; p.SurfaceG = .46f; p.SurfaceB = .20f; }
                else if (p.Class == WorldClass.GasGiant) { p.SurfaceR = .78f; p.SurfaceG = .62f; p.SurfaceB = .44f; }
                else if (p.Class == WorldClass.IceGiant) { p.SurfaceR = .42f; p.SurfaceG = .72f; p.SurfaceB = .86f; }
                else if (p.Class == WorldClass.Barren) { p.SurfaceR = .48f; p.SurfaceG = .46f; p.SurfaceB = .43f; }
                else { p.SurfaceR = .40f; p.SurfaceG = .38f; p.SurfaceB = .34f; }
                float tint = r.Range(.82f, 1.18f);
                p.SurfaceR = Math.Min(1f, p.SurfaceR * tint);
                p.SurfaceG = Math.Min(1f, p.SurfaceG * tint);
                p.SurfaceB = Math.Min(1f, p.SurfaceB * tint);
                p.SecondaryR = p.SurfaceR * .58f;
                p.SecondaryG = p.SurfaceG * .58f;
                p.SecondaryB = p.SurfaceB * .58f;
                planets[i] = p;
            }
            return planets;
        }

        /// <summary>Deterministic points of interest scattered through the system.</summary>
        public static UniverseStructure[] Structures(UniverseSystem system)
        {
            var rng = new UniverseRng(system.Key.X, system.Key.Y, system.Key.Z, 0x571C7, 991);
            int count = rng.Range(1, 6);
            var list = new List<UniverseStructure>(count);
            double beltAu = rng.Range(1.4, 3.6);
            for (int i = 0; i < count; i++)
            {
                var r = new UniverseRng(system.Key.X, system.Key.Y, system.Key.Z, 0x571C7, i * 7919L + 3);
                var s = new UniverseStructure();
                s.Kind = (StructureKind)r.Range(0, 9);
                double au = beltAu * r.Range(0.55, 1.9);
                double angle = r.Range(0.0, Math.PI * 2.0);
                double height = r.Range(-0.06, 0.06) * au;
                s.OffsetKm = new SpaceVector(Math.Cos(angle) * au * 149597870.7, height * 149597870.7, Math.Sin(angle) * au * 149597870.7);
                s.SizeKm = s.Kind == StructureKind.AsteroidField ? r.Range(90000, 1400000) : r.Range(140, 4200);
                switch (s.Kind)
                {
                    case StructureKind.Station: s.Name = "Станция «" + r.Pick(StarPrefix) + "»"; s.R = .62f; s.G = .78f; s.B = .95f; break;
                    case StructureKind.Derelict: s.Name = "Обломки «" + r.Pick(StarPrefix) + "»"; s.R = .56f; s.G = .50f; s.B = .44f; break;
                    case StructureKind.AsteroidField: s.Name = "Пояс астероидов " + system.Name; s.R = .48f; s.G = .44f; s.B = .40f; break;
                    case StructureKind.Monolith: s.Name = "Монолит " + r.Pick(StarPrefix); s.R = .22f; s.G = .18f; s.B = .30f; break;
                    case StructureKind.Wormhole: s.Name = "Червоточина " + r.Pick(StarPrefix); s.R = .55f; s.G = .30f; s.B = 1f; break;
                    case StructureKind.PirateOutpost: s.Name = "Логово пиратов " + r.Pick(StarPrefix); s.R = .95f; s.G = .32f; s.B = .22f; break;
                    case StructureKind.Comet: s.Name = "Комета " + r.Pick(StarPrefix); s.R = .82f; s.G = .92f; s.B = 1f; break;
                    case StructureKind.Probe: s.Name = "Зонд «" + r.Pick(StarPrefix) + "»"; s.R = .86f; s.G = .86f; s.B = .70f; break;
                    default: s.Name = "Руины " + r.Pick(StarPrefix); s.R = .70f; s.G = .60f; s.B = .40f; break;
                }
                list.Add(s);
            }
            return list.ToArray();
        }

        /// <summary>Star systems within a radius, nearest first. Only touched cells are generated.</summary>
        public static UniverseSystem[] Nearest(SpaceVector positionKm, double radiusLy, int maxCount)
        {
            if (maxCount <= 0) return new UniverseSystem[0];
            if (radiusLy <= 0 || double.IsNaN(radiusLy) || radiusLy > 400) radiusLy = 12;
            double radiusKm = radiusLy * LightYearKm;
            long cx = CellOf(positionKm.X), cy = CellOf(positionKm.Y), cz = CellOf(positionKm.Z);
            int reach = (int)Math.Min(12, Math.Ceiling(radiusKm / SectorKm) + 1);
            var found = new List<KeyValuePair<double, UniverseSystem>>(reach * reach * reach);
            for (long x = cx - reach; x <= cx + reach; x++)
                for (long y = cy - reach; y <= cy + reach; y++)
                    for (long z = cz - reach; z <= cz + reach; z++)
                    {
                        var sys = System(x, y, z);
                        double distance = SpaceVector.Distance(sys.AnchorKm, positionKm);
                        if (distance > radiusKm || distance <= 0.001) continue;
                        found.Add(new KeyValuePair<double, UniverseSystem>(distance, sys));
                    }
            found.Sort((a, b) => a.Key.CompareTo(b.Key));
            int take = Math.Min(maxCount, found.Count);
            var result = new UniverseSystem[take];
            for (int i = 0; i < take; i++) result[i] = found[i].Value;
            return result;
        }

        /// <summary>
        /// Fixed catalogue of galaxies on the sky. Directions are unit vectors in the same
        /// kilometre frame, so they can be drawn as background plates without loading anything.
        /// </summary>
        public static UniverseGalaxy[] Galaxies()
        {
            return new[]
            {
                new UniverseGalaxy { Name = "Млечный Путь", Type = "SBbc", X = 0.94, Y = -0.12, Z = 0.31, DistanceLy = 0.0, Size = 46f, R = .92f, G = .90f, B = .80f },
                new UniverseGalaxy { Name = "Галактика Андромеды (M31)", Type = "SA(s)b", X = -0.62, Y = 0.44, Z = 0.65, DistanceLy = 2537000, Size = 62f, R = .78f, G = .82f, B = 1f },
                new UniverseGalaxy { Name = "Треугольник (M33)", Type = "SA(s)cd", X = 0.28, Y = 0.71, Z = -0.65, DistanceLy = 2730000, Size = 34f, R = .72f, G = .86f, B = .96f },
                new UniverseGalaxy { Name = "Центавр A (NGC 5128)", Type = "S0 pec", X = -0.18, Y = -0.86, Z = 0.48, DistanceLy = 13700000, Size = 28f, R = 1f, G = .84f, B = .62f },
                new UniverseGalaxy { Name = "Водоворот (M51)", Type = "SA(s)bc", X = 0.66, Y = 0.52, Z = 0.54, DistanceLy = 23000000, Size = 24f, R = .86f, G = .92f, B = 1f },
                new UniverseGalaxy { Name = "Сомбреро (M104)", Type = "SA(s)a", X = -0.74, Y = -0.38, Z = -0.55, DistanceLy = 31000000, Size = 20f, R = 1f, G = .90f, B = .70f },
                new UniverseGalaxy { Name = "Антенны (NGC 4038)", Type = "SB(s)m pec", X = 0.36, Y = -0.62, Z = 0.70, DistanceLy = 45000000, Size = 18f, R = .95f, G = .78f, B = .72f },
                new UniverseGalaxy { Name = "Галактика Спирали (Abell 1689)", Type = "cD", X = -0.46, Y = 0.78, Z = -0.42, DistanceLy = 2300000000, Size = 12f, R = .70f, G = .62f, B = 1f }
            };
        }

        public static string StarKindName(StarKind kind)
        {
            switch (kind)
            {
                case StarKind.BrownDwarf: return "Коричневый карлик";
                case StarKind.RedDwarf: return "Красный карлик";
                case StarKind.OrangeDwarf: return "Оранжевый карлик";
                case StarKind.YellowDwarf: return "Жёлтый карлик";
                case StarKind.WhiteStar: return "Белая звезда";
                case StarKind.BlueGiant: return "Голубой гигант";
                case StarKind.RedGiant: return "Красный гигант";
                case StarKind.WhiteDwarf: return "Белый карлик";
                case StarKind.NeutronStar: return "Нейтронная звезда";
                default: return "Чёрная дыра";
            }
        }

        public static string WorldClassName(WorldClass cls)
        {
            switch (cls)
            {
                case WorldClass.Rock: return "каменистый мир";
                case WorldClass.Desert: return "пустынный мир";
                case WorldClass.Ocean: return "океанический мир";
                case WorldClass.Ice: return "ледяной мир";
                case WorldClass.Lava: return "лавовый мир";
                case WorldClass.Toxic: return "токсичный мир";
                case WorldClass.GasGiant: return "газовый гигант";
                case WorldClass.IceGiant: return "ледяной гигант";
                case WorldClass.Barren: return "безжизненный обломок";
                default: return "странствующий мир";
            }
        }

        public static string WeatherName(WeatherKind weather)
        {
            switch (weather)
            {
                case WeatherKind.Dust: return "пыльные бури";
                case WeatherKind.Rain: return "дожди";
                case WeatherKind.Snow: return "снегопады";
                case WeatherKind.Storm: return "штормы";
                case WeatherKind.AcidRain: return "кислотные дожди";
                case WeatherKind.Ashfall: return "пеплопад";
                case WeatherKind.SolarFlare: return "солнечные вспышки";
                case WeatherKind.Hail: return "град";
                case WeatherKind.IonStorm: return "ионные бури";
                default: return "без осадков";
            }
        }

        /// <summary>Short line for the cockpit: class, weather, atmosphere and hazard.</summary>
        public static string Describe(UniversePlanet planet)
        {
            string atmosphere = planet.AtmosphereDensity < 0.02f ? "вакуум"
                : planet.AtmosphereDensity < 0.35f ? "разреженная атмосфера"
                : planet.AtmosphereDensity < 0.75f ? "плотная атмосфера"
                : "сверхплотная атмосфера";
            string weather = planet.Weather == WeatherKind.Clear ? "без осадков"
                : WeatherName(planet.Weather) + " (" + (int)(planet.WeatherIntensity * 100) + "%)";
            return WorldClassName(planet.Class) + " · " + atmosphere + " · " + weather +
                " · " + (int)planet.TemperatureC + " °C · радиация " + planet.Hazard + "/5";
        }
    }
}
