using System;
using System.Collections.Generic;
using UnityEngine;
using LestingCraft.World;

namespace LestingCraft.Gameplay
{
    public enum GalaxyId
    {
        MilkyWay = 0,
        Andromeda = 1
    }

    public enum StarSystemId
    {
        SolarSystem = 0,
        AlphaCentauri = 1,
        Trappist1 = 2,
        Sirius = 3,
        Kepler452 = 4,
        AndromedaCore = 5,
        AndromedaDreamRift = 6
    }

    public enum CelestialBodyKind
    {
        Star,
        RockyPlanet,
        GasGiant,
        IceGiant,
        Moon,
        DwarfPlanet,
        OrbitalStation
    }

    public enum CelestialBodyId
    {
        Sun = 0,
        Mercury = 1,
        Venus = 2,
        Earth = 3,
        OrbitalStation = 4,
        Moon = 5,
        Mars = 6,
        Ceres = 7,
        Jupiter = 8,
        Europa = 9,
        Saturn = 10,
        Titan = 11,
        Uranus = 12,
        Neptune = 13,
        Pluto = 14,
        ProximaB = 15,
        CentauriPrime = 16,
        Trappist1d = 17,
        Trappist1e = 18,
        SiriusKrystallos = 19,
        Kepler452b = 20,
        AndromedaPrime = 21,
        MirachCitadel = 22,
        Somnia = 23
    }

    public readonly struct StarSystemDef
    {
        public readonly StarSystemId Id;
        public readonly GalaxyId Galaxy;
        public readonly string Code, NameRu, SpectralType, DescriptionRu;
        public readonly Color PrimaryStarColor, SecondaryStarColor;
        public readonly bool BinaryStar;
        public readonly float StarRadius;
        public readonly double DistanceFromSolLy;
        public string Name => NameRu;

        public StarSystemDef(StarSystemId id, GalaxyId galaxy, string code, string nameRu,
            string spectral, Color primary, Color secondary, bool binary, float starRadius,
            double distanceFromSolLy, string descRu)
        {
            Id = id; Galaxy = galaxy; Code = code; NameRu = nameRu; SpectralType = spectral;
            PrimaryStarColor = primary; SecondaryStarColor = secondary;
            BinaryStar = binary; StarRadius = starRadius;
            DistanceFromSolLy = distanceFromSolLy; DescriptionRu = descRu;
        }
    }

    public readonly struct CelestialBodyDef
    {
        public readonly CelestialBodyId Id;
        public readonly string WireKey, NameRu, SummaryRu;
        public readonly StarSystemId System;
        public readonly CelestialBodyKind Kind;
        public readonly Vector3 SpacePosition;
        public readonly double OrbitRadiusMkm, OrbitSpeedRadPerDay, RadiusKm, RingInnerScale, RingOuterScale;
        public readonly float ColorR, ColorG, ColorB, SecondaryR, SecondaryG, SecondaryB, GravityG;
        public readonly bool HasSurface, IsDockableStation, HasParent;
        public readonly CelestialBodyId ParentBodyId;
        public readonly float InclinationDeg;
        public readonly Dimension TargetDimension;

        public string Name => NameRu;
        public bool IsStar => Kind == CelestialBodyKind.Star;
        public bool HasRings => RingOuterScale > RingInnerScale && RingInnerScale > 0.01;
        public bool Landable => HasSurface || IsDockableStation;
        public float VisualRadius => IsStar ? 92f : HasRings ? 52f : Mathf.Clamp((float)Math.Log10(Math.Max(200.0, RadiusKm)) * 8.5f, 9f, 64f);

        public CelestialBodyDef(
            CelestialBodyId id, string wireKey, string nameRu, StarSystemId sys, CelestialBodyKind kind,
            double orbitMkm, double speedRadDay, double radiusKm,
            float r, float g, float b, float r2, float g2, float b2,
            bool hasSurface, Dimension dim,
            double ringInnerScale = 0.0, double ringOuterScale = 0.0,
            float gravityG = 1f, bool isStation = false, string summaryRu = "",
            CelestialBodyId? parentBody = null, float inclinationDeg = 0f)
        {
            Id = id; WireKey = wireKey; NameRu = nameRu; SummaryRu = summaryRu;
            System = sys; Kind = kind;
            OrbitRadiusMkm = orbitMkm; OrbitSpeedRadPerDay = speedRadDay; RadiusKm = radiusKm;
            ColorR = r; ColorG = g; ColorB = b;
            SecondaryR = r2; SecondaryG = g2; SecondaryB = b2;
            HasSurface = hasSurface; TargetDimension = dim;
            RingInnerScale = ringInnerScale; RingOuterScale = ringOuterScale;
            GravityG = gravityG; IsDockableStation = isStation;
            HasParent = parentBody.HasValue;
            ParentBodyId = parentBody ?? CelestialBodyId.Sun;
            InclinationDeg = inclinationDeg;
            float angle = (int)id * 1.17f;
            float dist = (float)(orbitMkm * 2.2);
            SpacePosition = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle * 1.7f) * 24f, Mathf.Sin(angle) * dist);
        }
    }

    public static class CelestialAtlas
    {
        public const double LightYearKm = 9.4607304725808e12;
        public const double AstronomicalUnitKm = 149597870.7;

        public static readonly string[] GalaxyNames =
        {
            "Млечный Путь",
            "Галактика Андромеды (M31)"
        };

        public static string GalaxyNameRu(GalaxyId g) =>
            GalaxyNames[Mathf.Clamp((int)g, 0, GalaxyNames.Length - 1)];

        public static readonly StarSystemDef[] Systems =
        {
            new StarSystemDef(StarSystemId.SolarSystem, GalaxyId.MilkyWay, "SOL",
                "Солнечная система", "G2V",
                new Color(1.0f, 0.93f, 0.72f), Color.black, false, 96f, 0.0,
                "Реальный астрономический масштаб 1:1 — Солнце, 8 планет, луны на истинных орбитах вокруг своих планет и станция «Эгида» на высоте 420 км."),

            new StarSystemDef(StarSystemId.AlphaCentauri, GalaxyId.MilkyWay, "CEN",
                "Альфа Центавра", "G2V + K1V + M5.5V",
                new Color(1.0f, 0.88f, 0.58f), new Color(1.0f, 0.42f, 0.22f), true, 88f, 4.24,
                "Ближайшая звёздная система (4,24 св. года) с приливным миром Проксима b."),

            new StarSystemDef(StarSystemId.Trappist1, GalaxyId.MilkyWay, "TRP-1",
                "Система TRAPPIST-1", "M8V",
                new Color(1.0f, 0.34f, 0.20f), Color.black, false, 58f, 40.66,
                "Ультрахолодный красный карлик (40,7 св. лет) с тесной резонансной цепью планет."),

            new StarSystemDef(StarSystemId.Sirius, GalaxyId.MilkyWay, "SIR",
                "Сириус A / B", "A1V + DA2",
                new Color(0.74f, 0.88f, 1.0f), new Color(0.92f, 0.97f, 1.0f), true, 112f, 8.60,
                "Ярчайшая бело-голубая двойная звезда ночного неба (8,60 св. лет)."),

            new StarSystemDef(StarSystemId.Kepler452, GalaxyId.MilkyWay, "KEP-452",
                "Система Kepler-452", "G2IV",
                new Color(1.0f, 0.86f, 0.52f), Color.black, false, 102f, 1799.0,
                "Древний двойник Солнца (1 800 св. лет) и изумрудная суперземля Kepler-452b."),

            new StarSystemDef(StarSystemId.AndromedaCore, GalaxyId.Andromeda, "M31-ELY",
                "Элизиум Андромеды", "B8II + F0I",
                new Color(0.76f, 0.52f, 1.0f), new Color(1.0f, 0.82f, 0.42f), true, 128f, 2537000.0,
                "Спиральный рукав Галактики Андромеды M31 (2,54 млн св. лет)."),

            new StarSystemDef(StarSystemId.AndromedaDreamRift, GalaxyId.Andromeda, "M31-SOM",
                "Разлом Сомнии", "PSR-M31",
                new Color(0.82f, 0.84f, 1.0f), new Color(0.45f, 0.28f, 0.78f), true, 74f, 2552000.0,
                "Бледный резонансный пульсар — источник сигнала и монолитов измерения «Сон».")
        };

        // True 1:1 astronomical semi-major axes (million km), true sidereal angular speeds (rad/day),
        // true planetary radii (km), and true parent-planet orbits for stations & moons!
        public static readonly CelestialBodyDef[] Bodies =
        {
            new CelestialBodyDef(CelestialBodyId.Sun, "sun", "Солнце", StarSystemId.SolarSystem, CelestialBodyKind.Star,
                0.0, 0.0, 696340.0, 1.0f, 0.88f, 0.52f, 1.0f, 0.56f, 0.14f, false, Dimension.Lestingia, 0, 0, 27.9f, false,
                "Звезда класса G2V (радиус 696 340 км)."),
            new CelestialBodyDef(CelestialBodyId.Mercury, "mercury", "Меркурий", StarSystemId.SolarSystem, CelestialBodyKind.RockyPlanet,
                57.909, 0.07142, 2439.7, 0.62f, 0.58f, 0.54f, 0.44f, 0.41f, 0.39f, true, Dimension.Mercury, 0, 0, 0.38f, false,
                "57,9 млн км от Солнца · вакуум и раскалённые кратеры.", null, 7.00f),
            new CelestialBodyDef(CelestialBodyId.Venus, "venus", "Венера", StarSystemId.SolarSystem, CelestialBodyKind.RockyPlanet,
                108.209, 0.02796, 6051.8, 0.92f, 0.79f, 0.52f, 0.78f, 0.58f, 0.30f, true, Dimension.Venus, 0, 0, 0.90f, false,
                "108,2 млн км от Солнца · сверхплотная серная атмосфера.", null, 3.39f),
            new CelestialBodyDef(CelestialBodyId.Earth, "earth", "Земля · Лестингия", StarSystemId.SolarSystem, CelestialBodyKind.RockyPlanet,
                149.598, 0.017202, 6371.0, 0.20f, 0.52f, 0.88f, 0.26f, 0.68f, 0.36f, true, Dimension.Lestingia, 0, 0, 1.00f, false,
                "149,6 млн км (1,00 а.е.) · родная планета, 12 биомов и живая фауна.", null, 0.0f),
            new CelestialBodyDef(CelestialBodyId.OrbitalStation, "station", "Станция «Эгида»", StarSystemId.SolarSystem, CelestialBodyKind.OrbitalStation,
                0.006791, 97.38, 1.2, 0.84f, 0.88f, 0.94f, 0.28f, 0.62f, 0.92f, false, Dimension.Lestingia, 0, 0, 0f, true,
                "Низкая околоземная орбита 420 км над Землёй (7,66 км/с).", CelestialBodyId.Earth, 51.6f),
            new CelestialBodyDef(CelestialBodyId.Moon, "moon", "Луна", StarSystemId.SolarSystem, CelestialBodyKind.Moon,
                0.384400, 0.22997, 1737.4, 0.76f, 0.76f, 0.79f, 0.50f, 0.51f, 0.55f, true, Dimension.Moon, 0, 0, 0.165f, false,
                "384 400 км вокруг Земли · вакуум, чёрное звёздное небо и Земля над горизонтом.", CelestialBodyId.Earth, 5.14f),
            new CelestialBodyDef(CelestialBodyId.Mars, "mars", "Марс", StarSystemId.SolarSystem, CelestialBodyKind.RockyPlanet,
                227.939, 0.009146, 3389.5, 0.80f, 0.38f, 0.22f, 0.56f, 0.24f, 0.15f, true, Dimension.Mars, 0, 0, 0.38f, false,
                "227,9 млн км (1,52 а.е.) · пылевая атмосфера, каньоны и синие закаты.", null, 1.85f),
            new CelestialBodyDef(CelestialBodyId.Ceres, "ceres", "Церера · Пояс астероидов", StarSystemId.SolarSystem, CelestialBodyKind.DwarfPlanet,
                413.700, 0.003736, 473.0, 0.55f, 0.53f, 0.50f, 0.38f, 0.37f, 0.35f, true, Dimension.Moon, 1.5, 2.3, 0.03f, false,
                "413,7 млн км (2,77 а.е.) · карликовая планета пояса астероидов.", null, 10.59f),
            new CelestialBodyDef(CelestialBodyId.Jupiter, "jupiter", "Юпитер", StarSystemId.SolarSystem, CelestialBodyKind.GasGiant,
                778.479, 0.001450, 69911.0, 0.85f, 0.70f, 0.54f, 0.72f, 0.40f, 0.26f, false, Dimension.Europa, 0, 0, 2.53f, false,
                "778,5 млн км (5,20 а.е.) · крупнейший газовый гигант.", null, 1.30f),
            new CelestialBodyDef(CelestialBodyId.Europa, "europa", "Европа", StarSystemId.SolarSystem, CelestialBodyKind.Moon,
                0.670900, 1.7693, 1560.8, 0.78f, 0.90f, 0.98f, 0.48f, 0.68f, 0.86f, true, Dimension.Europa, 0, 0, 0.134f, false,
                "670 900 км вокруг Юпитера · ледяная кора и огромный Юпитер в чёрном небе.", CelestialBodyId.Jupiter, 0.47f),
            new CelestialBodyDef(CelestialBodyId.Saturn, "saturn", "Сатурн", StarSystemId.SolarSystem, CelestialBodyKind.GasGiant,
                1433.530, 0.000584, 58232.0, 0.92f, 0.82f, 0.58f, 0.76f, 0.64f, 0.42f, false, Dimension.Titan, 1.35, 2.35, 1.06f, false,
                "1,434 млрд км (9,58 а.е.) · гигант с системой квадратных ледяных колец.", null, 2.49f),
            new CelestialBodyDef(CelestialBodyId.Titan, "titan", "Титан", StarSystemId.SolarSystem, CelestialBodyKind.Moon,
                1.221870, 0.39405, 2574.7, 0.90f, 0.62f, 0.26f, 0.68f, 0.42f, 0.16f, true, Dimension.Titan, 0, 0, 0.14f, false,
                "1 221 870 км вокруг Сатурна · плотная бирюзово-золотая метановая дымка.", CelestialBodyId.Saturn, 0.35f),
            new CelestialBodyDef(CelestialBodyId.Uranus, "uranus", "Уран", StarSystemId.SolarSystem, CelestialBodyKind.IceGiant,
                2870.972, 0.000205, 25362.0, 0.56f, 0.88f, 0.92f, 0.40f, 0.74f, 0.82f, false, Dimension.Pluto, 1.45, 1.85, 0.89f, false,
                "2,871 млрд км (19,2 а.е.) · ледяной гигант с вертикальными кольцами.", null, 0.77f),
            new CelestialBodyDef(CelestialBodyId.Neptune, "neptune", "Нептун", StarSystemId.SolarSystem, CelestialBodyKind.IceGiant,
                4498.396, 0.000104, 24622.0, 0.18f, 0.36f, 0.88f, 0.12f, 0.22f, 0.64f, false, Dimension.Pluto, 0, 0, 1.14f, false,
                "4,498 млрд км (30,1 а.е.) · тёмный кобальтовый гигант на границе тьмы.", null, 1.77f),
            new CelestialBodyDef(CelestialBodyId.Pluto, "pluto", "Плутон", StarSystemId.SolarSystem, CelestialBodyKind.DwarfPlanet,
                5906.380, 0.0000694, 1188.3, 0.84f, 0.76f, 0.70f, 0.94f, 0.92f, 0.90f, true, Dimension.Pluto, 0, 0, 0.063f, false,
                "5,906 млрд км (39,5 а.е.) · криогенные сумерки и синяя азотная дымка.", null, 17.16f),

            // Extrasolar Star Systems (Milky Way) — true physical orbital radii
            new CelestialBodyDef(CelestialBodyId.ProximaB, "proxima-b", "Проксима Центавра b", StarSystemId.AlphaCentauri, CelestialBodyKind.RockyPlanet,
                7.260, 0.5617, 7010.0, 0.78f, 0.34f, 0.28f, 0.32f, 0.66f, 0.62f, true, Dimension.ProximaB, 0, 0, 1.07f, false,
                "7,26 млн км (0,0485 а.е.) от Проксимы Центавра · вечный багровый терминатор.", null, 2.4f),
            new CelestialBodyDef(CelestialBodyId.CentauriPrime, "centauri-prime", "Центавр Прайм", StarSystemId.AlphaCentauri, CelestialBodyKind.GasGiant,
                310.0, 0.008, 54000.0, 0.88f, 0.66f, 0.38f, 0.54f, 0.34f, 0.22f, false, Dimension.ProximaB, 1.4, 2.1, 1.8f, false,
                "Кольцевой гигант в двойном сиянии Альфы Центавра A и B.", null, 3.1f),
            new CelestialBodyDef(CelestialBodyId.Trappist1d, "trappist-1d", "TRAPPIST-1d", StarSystemId.Trappist1, CelestialBodyKind.RockyPlanet,
                3.330, 1.551, 5020.0, 0.72f, 0.56f, 0.44f, 0.36f, 0.60f, 0.72f, true, Dimension.Trappist1e, 0, 0, 0.69f, false,
                "3,33 млн км (0,022 а.е.) от красного карлика TRAPPIST-1.", null, 0.9f),
            new CelestialBodyDef(CelestialBodyId.Trappist1e, "trappist-1e", "TRAPPIST-1e", StarSystemId.Trappist1, CelestialBodyKind.RockyPlanet,
                4.380, 1.030, 5860.0, 0.34f, 0.62f, 0.78f, 0.58f, 0.42f, 0.80f, true, Dimension.Trappist1e, 0, 0, 0.93f, false,
                "4,38 млн км (0,029 а.е.) · фиолетово-лавандовая атмосфера и цепь планет в небе.", null, 0.5f),
            new CelestialBodyDef(CelestialBodyId.SiriusKrystallos, "sirius-krystallos", "Сириус Кристаллос", StarSystemId.Sirius, CelestialBodyKind.RockyPlanet,
                260.0, 0.011, 7400.0, 0.72f, 0.90f, 1.0f, 0.48f, 0.72f, 0.96f, true, Dimension.Europa, 0, 0, 0.85f, false,
                "260 млн км · ледяной мир в ослепительно-голубом свете Сириуса A.", null, 4.2f),
            new CelestialBodyDef(CelestialBodyId.Kepler452b, "kepler-452b", "Kepler-452b", StarSystemId.Kepler452, CelestialBodyKind.RockyPlanet,
                156.5, 0.0163, 9550.0, 0.26f, 0.72f, 0.62f, 0.22f, 0.42f, 0.78f, true, Dimension.Kepler452b, 0, 0, 1.60f, false,
                "156,5 млн км (1,046 а.е.) · изумрудное небо древней суперземли.", null, 1.2f),

            // Galaxy 2: Andromeda (M31)
            new CelestialBodyDef(CelestialBodyId.AndromedaPrime, "andromeda-prime", "Элизиум M31", StarSystemId.AndromedaCore, CelestialBodyKind.RockyPlanet,
                185.0, 0.014, 7900.0, 0.64f, 0.36f, 0.94f, 0.96f, 0.80f, 0.36f, true, Dimension.AndromedaPrime, 1.4, 2.0, 1.12f, false,
                "Кристальный окольцованный мир в рукаве галактики Андромеды M31.", null, 2.8f),
            new CelestialBodyDef(CelestialBodyId.MirachCitadel, "mirach-citadel", "Цитадель Мирах", StarSystemId.AndromedaCore, CelestialBodyKind.OrbitalStation,
                0.009350, 74.5, 2.4, 0.92f, 0.84f, 0.98f, 0.62f, 0.36f, 0.92f, false, Dimension.AndromedaPrime, 0, 0, 0f, true,
                "Низкая орбита 1 450 км над Элизиумом M31.", CelestialBodyId.AndromedaPrime, 18.0f),
            new CelestialBodyDef(CelestialBodyId.Somnia, "somnia", "Сомния · Колыбель Сна", StarSystemId.AndromedaDreamRift, CelestialBodyKind.RockyPlanet,
                122.0, 0.019, 6680.0, 0.82f, 0.80f, 0.90f, 0.18f, 0.14f, 0.28f, true, Dimension.Somnia, 1.35, 1.9, 0.62f, false,
                "Бледный туманный мир пульсара — разгадка сонного паралича.", null, 6.4f)
        };

        public static StarSystemDef GetSystem(StarSystemId id) =>
            Systems[Mathf.Clamp((int)id, 0, Systems.Length - 1)];

        public static CelestialBodyDef GetBody(CelestialBodyId id) =>
            Bodies[Mathf.Clamp((int)id, 0, Bodies.Length - 1)];

        public static CelestialBodyId ParseWireKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return CelestialBodyId.Moon;
            string norm = key.Trim().ToLowerInvariant();
            for (int i = 0; i < Bodies.Length; i++)
                if (Bodies[i].WireKey == norm) return Bodies[i].Id;
            return CelestialBodyId.Moon;
        }

        public static SpaceVector WorldPositionKm(CelestialBodyId id, double orbitDays = 0.0)
        {
            var b = GetBody(id);
            if (b.IsStar || b.OrbitRadiusMkm <= 1e-7) return new SpaceVector(0, 0, 0);
            double rKm = b.OrbitRadiusMkm * 1000000.0;
            double ang = (int)id * 0.93 + orbitDays * b.OrbitSpeedRadPerDay;
            double incRad = b.InclinationDeg * Math.PI / 180.0;
            double x = Math.Cos(ang) * rKm;
            double z = Math.Sin(ang) * rKm * Math.Cos(incRad);
            double y = Math.Sin(ang) * rKm * Math.Sin(incRad);
            SpaceVector local = new SpaceVector(x, y, z);
            if (b.HasParent && b.ParentBodyId != id)
                return WorldPositionKm(b.ParentBodyId, orbitDays) + local;
            return local;
        }

        public static double DistanceBetweenSystemsKm(StarSystemId a, StarSystemId b)
        {
            if (a == b) return 0.0;
            double lyA = GetSystem(a).DistanceFromSolLy;
            double lyB = GetSystem(b).DistanceFromSolLy;
            return Math.Max(0.25, Math.Abs(lyB - lyA)) * LightYearKm;
        }

        public static string FormatDistanceRu(double km)
        {
            if (km >= LightYearKm * 0.5)
            {
                double ly = km / LightYearKm;
                return ly >= 1000.0 ? $"{ly / 1000000.0:0.00} млн св. л." : $"{ly:0.00} св. г.";
            }
            if (km >= 1.0e9) return $"{km / 1.0e9:0.00} млрд км";
            if (km >= 1.0e6) return $"{km / 1.0e6:0.0} млн км";
            return $"{km:0} км";
        }

        public static CelestialBodyId[] BodiesInSystem(StarSystemId system)
        {
            var list = new List<CelestialBodyId>();
            for (int i = 0; i < Bodies.Length; i++)
                if (Bodies[i].System == system) list.Add(Bodies[i].Id);
            return list.ToArray();
        }

        public static StarSystemId[] SystemsInGalaxy(GalaxyId galaxy)
        {
            var list = new List<StarSystemId>();
            for (int i = 0; i < Systems.Length; i++)
                if (Systems[i].Galaxy == galaxy) list.Add(Systems[i].Id);
            return list.ToArray();
        }
    }

    public sealed class OpenSpaceHelm
    {
        // Continuous 1:1 real-time flight without Time Warp buttons (Earth -> Moon cruise = ~33.4 minutes at 185 km/s)
        public static readonly double[] TimeWarpRates = { 1.0 };

        // Calibrated sublight cruise drive: LEO ~7.66 km/s, sustained cislunar cruise 185 km/s (~33.4 min Earth -> Moon)
        public const double MaxSublightSpeedKmS = 185.0;
        public const double MainEngineAccelKmS2 = 0.85;    // reaches full cruise in ~3.6 min smooth burn
        public const double BoostEngineAccelKmS2 = 2.40;   // rapid orbital injection / retro-braking burn

        public bool Active;
        public bool ManualControl = true;
        public GalaxyId CurrentGalaxy = GalaxyId.MilkyWay;
        public StarSystemId CurrentSystem = StarSystemId.SolarSystem;
        public CelestialBodyId TargetBody = CelestialBodyId.Moon;
        public SpaceVector PositionKm = new SpaceVector(149597870.7 + 6821.0, 0, 0);
        public SpaceVector VelocityKmPerSec = new SpaceVector(0, 0, 7.66);
        public Quaternion Rotation = Quaternion.identity;
        public Vector3 Velocity = Vector3.zero;
        public float Throttle = 0.0f;
        public bool Boost;
        public bool RetroBrake;
        public bool InHyperjump;
        public float HyperjumpProgress;
        public double OrbitTimeDays = 12.0;

        int timeWarpIndex;
        CelestialBodyId hyperjumpTarget;

        public int TimeWarpIndex => timeWarpIndex;
        public double TimeWarp => TimeWarpRates[Mathf.Clamp(timeWarpIndex, 0, TimeWarpRates.Length - 1)];
        public double SpeedKmPerSec => VelocityKmPerSec.Magnitude;
        public double SpeedMetersPerSec => SpeedKmPerSec * 1000.0;

        public CelestialBodyDef TargetBodyDef => CelestialAtlas.GetBody(TargetBody);

        public double DistanceToTargetKm()
        {
            var def = CelestialAtlas.GetBody(TargetBody);
            if (def.System != CurrentSystem)
                return CelestialAtlas.DistanceBetweenSystemsKm(CurrentSystem, def.System);
            double raw = SpaceVector.Distance(PositionKm, CelestialAtlas.WorldPositionKm(TargetBody, OrbitTimeDays));
            return Math.Max(0.0, raw - def.RadiusKm);
        }

        public double NearestBodySurfaceDistanceKm(out CelestialBodyId nearestId)
        {
            nearestId = CelestialBodyId.Earth;
            double best = double.MaxValue;
            foreach (var b in CelestialAtlas.Bodies)
            {
                if (b.System != CurrentSystem) continue;
                double d = Math.Max(0.0, SpaceVector.Distance(PositionKm, CelestialAtlas.WorldPositionKm(b.Id, OrbitTimeDays)) - b.RadiusKm);
                if (d < best) { best = d; nearestId = b.Id; }
            }
            return best;
        }

        public int MaxAllowedWarpIndex()
        {
            double nearKm = NearestBodySurfaceDistanceKm(out _);
            if (nearKm < 800.0) return 2;        // <= 800 km low orbit: max 10x
            if (nearKm < 8000.0) return 3;       // <= 8,000 km: max 50x
            if (nearKm < 60000.0) return 4;      // <= 60,000 km: max 100x
            if (nearKm < 450000.0) return 5;     // Earth-Moon space: max 1,000x
            if (nearKm < 6000000.0) return 6;    // Planetary sphere of influence: max 10,000x
            return TimeWarpRates.Length - 1;     // Deep interplanetary space: 100,000x!
        }

        public void SetTimeWarpIndex(int idx)
        {
            timeWarpIndex = Mathf.Clamp(idx, 0, MaxAllowedWarpIndex());
        }

        public double IncreaseTimeWarp()
        {
            SetTimeWarpIndex(timeWarpIndex + 1);
            return TimeWarp;
        }

        public double DecreaseTimeWarp()
        {
            timeWarpIndex = Mathf.Max(0, timeWarpIndex - 1);
            return TimeWarp;
        }

        public void ResetTimeWarp() => timeWarpIndex = 0;

        public bool InLandingRange
        {
            get
            {
                if (!Active || InHyperjump) return false;
                var b = CelestialAtlas.GetBody(TargetBody);
                if (b.System != CurrentSystem) return false;
                double limitKm = b.IsDockableStation ? 3500.0 : Math.Max(6500.0, b.RadiusKm * 3.8);
                return DistanceToTargetKm() <= limitKm;
            }
        }

        public static double RealisticOrbitalSpeedKmS(CelestialBodyDef b)
        {
            if (b.IsDockableStation) return 7.66;
            if (b.HasRings) return 21.5;
            if (b.Kind == CelestialBodyKind.GasGiant || b.Kind == CelestialBodyKind.IceGiant) return 18.0;
            return Math.Max(1.2, 7.905 * Math.Sqrt(Math.Max(0.05, b.GravityG) * Math.Max(0.1, b.RadiusKm / 6371.0)));
        }

        public void EnterOrbitAround(CelestialBodyId bodyId)
        {
            var b = CelestialAtlas.GetBody(bodyId);
            CurrentSystem = b.System;
            CurrentGalaxy = CelestialAtlas.GetSystem(CurrentSystem).Galaxy;
            double altKm = b.IsDockableStation ? 18.0
                         : b.HasRings ? b.RadiusKm * (b.RingOuterScale + 0.55)
                         : Math.Max(220.0, b.RadiusKm * 0.22);
            PositionKm = CelestialAtlas.WorldPositionKm(bodyId, OrbitTimeDays)
                       + new SpaceVector(b.RadiusKm + altKm, b.RadiusKm * 0.08, 0.0);
            double vOrb = RealisticOrbitalSpeedKmS(b);
            Rotation = Quaternion.identity;
            VelocityKmPerSec = new SpaceVector(0.0, 0.0, vOrb);
            Velocity = Vector3.forward * (float)(vOrb * 1000.0);
            Throttle = 0.0f;
            Boost = false;
            RetroBrake = false;
            timeWarpIndex = 0;
            ManualControl = true;
            InHyperjump = false;
            HyperjumpProgress = 0f;
            Active = true;
        }

        public void Disengage()
        {
            Active = false;
            InHyperjump = false;
            HyperjumpProgress = 0f;
            timeWarpIndex = 0;
            Velocity = Vector3.zero;
        }

        public void SelectTarget(CelestialBodyId id) => TargetBody = id;
        public void SetManualControl(bool manual) => ManualControl = manual;
        public void SetThrottle(float t) => Throttle = Mathf.Clamp01(t);

        public CelestialBodyDef CycleTarget(int delta)
        {
            int n = CelestialAtlas.Bodies.Length;
            int idx = (((int)TargetBody + delta) % n + n) % n;
            TargetBody = (CelestialBodyId)idx;
            return CelestialAtlas.Bodies[idx];
        }

        public bool StartHyperjump(CelestialBodyId destination)
        {
            if (!Active) Active = true;
            TargetBody = destination;
            hyperjumpTarget = destination;
            timeWarpIndex = 0;
            InHyperjump = true;
            HyperjumpProgress = 0f;
            return true;
        }

        public bool StartHyperjump(StarSystemId system)
        {
            var list = CelestialAtlas.BodiesInSystem(system);
            CelestialBodyId target = list.Length > 0 ? list[0] : CelestialBodyId.Earth;
            for (int i = 0; i < list.Length; i++)
                if (CelestialAtlas.GetBody(list[i]).Landable) { target = list[i]; break; }
            return StartHyperjump(target);
        }

        public bool JumpToGalaxy(GalaxyId galaxy)
        {
            var systems = CelestialAtlas.SystemsInGalaxy(galaxy);
            return systems.Length > 0 && StartHyperjump(systems[0]);
        }

        public void Step(float dt, float pitchInput, float yawInput, float rollInput)
        {
            if (!Active || dt <= 0f) return;
            dt = Mathf.Min(dt, 0.1f);

            if (InHyperjump)
            {
                HyperjumpProgress += dt * 0.85f;
                Velocity = Rotation * Vector3.forward * 299792f;
                if (HyperjumpProgress >= 1f)
                {
                    InHyperjump = false;
                    HyperjumpProgress = 0f;
                    Boost = false;
                    Throttle = 0.0f;
                    timeWarpIndex = 0;
                    var dest = CelestialAtlas.GetBody(hyperjumpTarget);
                    CurrentSystem = dest.System;
                    CurrentGalaxy = CelestialAtlas.GetSystem(CurrentSystem).Galaxy;
                    TargetBody = dest.Id;
                    double parkDist = dest.IsDockableStation ? 45.0
                                    : dest.HasRings ? dest.RadiusKm * (dest.RingOuterScale + 0.65)
                                    : Math.Max(1400.0, dest.RadiusKm * 2.2);
                    SpaceVector targetWorld = CelestialAtlas.WorldPositionKm(dest.Id, OrbitTimeDays);
                    PositionKm = targetWorld + new SpaceVector(0.0, dest.RadiusKm * 0.18, -parkDist);
                    double vPark = RealisticOrbitalSpeedKmS(dest);
                    VelocityKmPerSec = new SpaceVector(0.0, 0.0, vPark);
                    Velocity = Vector3.forward * (float)(vPark * 1000.0);
                    Rotation = Quaternion.identity;
                    return;
                }
                return;
            }

            if (ManualControl)
            {
                Quaternion deltaRot = Quaternion.Euler(-pitchInput * 58f * dt, yawInput * 58f * dt, -rollInput * 68f * dt);
                Rotation = (Rotation * deltaRot).normalized;
            }
            else if (TargetBodyDef.System == CurrentSystem)
            {
                SpaceVector toDest = CelestialAtlas.WorldPositionKm(TargetBody, OrbitTimeDays) - PositionKm;
                Vector3 dir = new Vector3((float)toDest.Normalized.X, (float)toDest.Normalized.Y, (float)toDest.Normalized.Z);
                if (dir.sqrMagnitude > 0.001f)
                    Rotation = Quaternion.Slerp(Rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-dt * 2.8f));
            }

            Vector3 fwd = Rotation * Vector3.forward;

            // KSP safety auto-drop when entering a planet/moon gravity well so the player never overshoots!
            int maxWarp = MaxAllowedWarpIndex();
            if (timeWarpIndex > maxWarp)
                timeWarpIndex = maxWarp;

            // Real rocket propulsion (1x physical burn or gentle course alignment)
            double accelKmS2 = Throttle * (Boost ? BoostEngineAccelKmS2 : MainEngineAccelKmS2);
            if (accelKmS2 > 1e-6)
            {
                // Steer prograde velocity smoothly toward nose thrust vector while accelerating realistically in km/s
                double nextSpeed = Math.Min(MaxSublightSpeedKmS, SpeedKmPerSec + accelKmS2 * dt * Math.Min(TimeWarp, 5.0));
                SpaceVector thrustDir = new SpaceVector(fwd.x, fwd.y, fwd.z);
                SpaceVector blended = SpeedKmPerSec > 0.05
                    ? (VelocityKmPerSec.Normalized * 0.72 + thrustDir * 0.28).Normalized
                    : thrustDir;
                VelocityKmPerSec = blended * nextSpeed;
            }
            else if (RetroBrake && SpeedKmPerSec > 0.02)
            {
                double nextSpeed = Math.Max(0.05, SpeedKmPerSec - BoostEngineAccelKmS2 * dt * Math.Min(TimeWarp, 5.0));
                VelocityKmPerSec = VelocityKmPerSec.Normalized * nextSpeed;
            }

            double simDt = dt * TimeWarp;
            OrbitTimeDays += simDt / 86400.0;
            PositionKm = PositionKm + VelocityKmPerSec * simDt;
            Velocity = new Vector3((float)VelocityKmPerSec.X, (float)VelocityKmPerSec.Y, (float)VelocityKmPerSec.Z) * 1000f;
        }
    }
}
