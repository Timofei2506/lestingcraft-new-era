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
        public string Name => NameRu;

        public StarSystemDef(StarSystemId id, GalaxyId galaxy, string code, string nameRu,
            string spectral, Color primary, Color secondary, bool binary, float starRadius, string descRu)
        {
            Id = id; Galaxy = galaxy; Code = code; NameRu = nameRu; SpectralType = spectral;
            PrimaryStarColor = primary; SecondaryStarColor = secondary;
            BinaryStar = binary; StarRadius = starRadius; DescriptionRu = descRu;
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
        public readonly bool HasSurface, IsDockableStation;
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
            float gravityG = 1f, bool isStation = false, string summaryRu = "")
        {
            Id = id; WireKey = wireKey; NameRu = nameRu; SummaryRu = summaryRu;
            System = sys; Kind = kind;
            OrbitRadiusMkm = orbitMkm; OrbitSpeedRadPerDay = speedRadDay; RadiusKm = radiusKm;
            ColorR = r; ColorG = g; ColorB = b;
            SecondaryR = r2; SecondaryG = g2; SecondaryB = b2;
            HasSurface = hasSurface; TargetDimension = dim;
            RingInnerScale = ringInnerScale; RingOuterScale = ringOuterScale;
            GravityG = gravityG; IsDockableStation = isStation;
            float angle = (int)id * 1.17f;
            float dist = (float)(orbitMkm * 2.2);
            SpacePosition = new Vector3(Mathf.Cos(angle) * dist, Mathf.Sin(angle * 1.7f) * 24f, Mathf.Sin(angle) * dist);
        }
    }

    public static class CelestialAtlas
    {
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
                new Color(1.0f, 0.91f, 0.62f), Color.black, false, 96f,
                "Родная планетная система: Солнце, все 8 больших планет, Пояс астероидов, Европа, Титан, Плутон и станция «Эгида»."),

            new StarSystemDef(StarSystemId.AlphaCentauri, GalaxyId.MilkyWay, "CEN",
                "Альфа Центавра", "G2V + K1V + M5.5V",
                new Color(1.0f, 0.88f, 0.58f), new Color(1.0f, 0.52f, 0.24f), true, 88f,
                "Ближайшая тройная звёздная система (4,37 св. года) с багровым миром Проксима b."),

            new StarSystemDef(StarSystemId.Trappist1, GalaxyId.MilkyWay, "TRP-1",
                "Система TRAPPIST-1", "M8V",
                new Color(1.0f, 0.34f, 0.20f), Color.black, false, 58f,
                "Ультрахолодный красный карлик с резонансной цепью землеподобных планет."),

            new StarSystemDef(StarSystemId.Sirius, GalaxyId.MilkyWay, "SIR",
                "Сириус A / B", "A1V + DA2",
                new Color(0.74f, 0.88f, 1.0f), new Color(0.92f, 0.97f, 1.0f), true, 112f,
                "Ярчайшая двойная звезда ночного неба и сверхплотный белый карлик Сириус B."),

            new StarSystemDef(StarSystemId.Kepler452, GalaxyId.MilkyWay, "KEP-452",
                "Система Kepler-452", "G2IV",
                new Color(1.0f, 0.86f, 0.52f), Color.black, false, 102f,
                "Древний двойник Солнца и цветущая суперземля Kepler-452b."),

            new StarSystemDef(StarSystemId.AndromedaCore, GalaxyId.Andromeda, "M31-ELY",
                "Элизиум Андромеды", "B8II + F0I",
                new Color(0.76f, 0.52f, 1.0f), new Color(1.0f, 0.82f, 0.42f), true, 128f,
                "Двойной сверхгигант в спиральном рукаве Галактики Андромеды M31 (2,54 млн св. лет)."),

            new StarSystemDef(StarSystemId.AndromedaDreamRift, GalaxyId.Andromeda, "M31-SOM",
                "Разлом Сомнии", "PSR-M31",
                new Color(0.82f, 0.84f, 1.0f), new Color(0.45f, 0.28f, 0.78f), true, 74f,
                "Бледный резонансный пульсар — источник сигнала и монолитов измерения «Сон».")
        };

        public static readonly CelestialBodyDef[] Bodies =
        {
            new CelestialBodyDef(CelestialBodyId.Sun, "sun", "Солнце", StarSystemId.SolarSystem, CelestialBodyKind.Star,
                0.0, 0.0, 696340.0, 1.0f, 0.86f, 0.42f, 1.0f, 0.52f, 0.12f, false, Dimension.Lestingia, 0, 0, 27.9f, false,
                "Звезда класса G2V в центре Солнечной системы."),
            new CelestialBodyDef(CelestialBodyId.Mercury, "mercury", "Меркурий", StarSystemId.SolarSystem, CelestialBodyKind.RockyPlanet,
                57.9, 0.0714, 2439.7, 0.62f, 0.58f, 0.54f, 0.44f, 0.41f, 0.39f, true, Dimension.Mercury, 0, 0, 0.38f, false,
                "Выжженный кратерный мир у самой короны Солнца."),
            new CelestialBodyDef(CelestialBodyId.Venus, "venus", "Венера", StarSystemId.SolarSystem, CelestialBodyKind.RockyPlanet,
                108.2, 0.0279, 6051.8, 0.92f, 0.79f, 0.52f, 0.78f, 0.58f, 0.30f, true, Dimension.Venus, 0, 0, 0.90f, false,
                "Плотная серная атмосфера и базальтовые кальдеры."),
            new CelestialBodyDef(CelestialBodyId.Earth, "earth", "Земля · Лестингия", StarSystemId.SolarSystem, CelestialBodyKind.RockyPlanet,
                149.6, 0.0172, 6371.0, 0.22f, 0.54f, 0.86f, 0.28f, 0.68f, 0.38f, true, Dimension.Lestingia, 0, 0, 1.00f, false,
                "Родной мир: 12 биомов, Тихий Брод и космодром."),
            new CelestialBodyDef(CelestialBodyId.OrbitalStation, "station", "Станция «Эгида»", StarSystemId.SolarSystem, CelestialBodyKind.OrbitalStation,
                150.1, 0.0172, 1.2, 0.82f, 0.86f, 0.92f, 0.28f, 0.62f, 0.92f, false, Dimension.Lestingia, 0, 0, 0f, true,
                "Орбитальная станция на околоземной орбите."),
            new CelestialBodyDef(CelestialBodyId.Moon, "moon", "Луна", StarSystemId.SolarSystem, CelestialBodyKind.Moon,
                150.9, 0.0172, 1737.4, 0.76f, 0.76f, 0.79f, 0.52f, 0.53f, 0.57f, true, Dimension.Moon, 0, 0, 0.16f, false,
                "Спутник Земли с базальтовыми морями и кратерами."),
            new CelestialBodyDef(CelestialBodyId.Mars, "mars", "Марс", StarSystemId.SolarSystem, CelestialBodyKind.RockyPlanet,
                227.9, 0.0091, 3389.5, 0.80f, 0.38f, 0.22f, 0.56f, 0.24f, 0.15f, true, Dimension.Mars, 0, 0, 0.38f, false,
                "Красная планета каньонов и полярных шапок."),
            new CelestialBodyDef(CelestialBodyId.Ceres, "ceres", "Церера · Пояс астероидов", StarSystemId.SolarSystem, CelestialBodyKind.DwarfPlanet,
                413.7, 0.0037, 473.0, 0.55f, 0.53f, 0.50f, 0.38f, 0.37f, 0.35f, true, Dimension.Moon, 1.5, 2.3, 0.03f, false,
                "Карликовая планета посреди главного пояса астероидов."),
            new CelestialBodyDef(CelestialBodyId.Jupiter, "jupiter", "Юпитер", StarSystemId.SolarSystem, CelestialBodyKind.GasGiant,
                778.5, 0.00145, 69911.0, 0.85f, 0.70f, 0.54f, 0.72f, 0.40f, 0.26f, false, Dimension.Europa, 0, 0, 2.53f, false,
                "Крупнейший газовый гигант Солнечной системы."),
            new CelestialBodyDef(CelestialBodyId.Europa, "europa", "Европа", StarSystemId.SolarSystem, CelestialBodyKind.Moon,
                781.2, 0.00145, 1560.8, 0.78f, 0.90f, 0.98f, 0.48f, 0.68f, 0.86f, true, Dimension.Europa, 0, 0, 0.14f, false,
                "Ледяная луна Юпитера со скрытым подлёдным океаном."),
            new CelestialBodyDef(CelestialBodyId.Saturn, "saturn", "Сатурн", StarSystemId.SolarSystem, CelestialBodyKind.GasGiant,
                1433.5, 0.00058, 58232.0, 0.92f, 0.82f, 0.58f, 0.76f, 0.64f, 0.42f, false, Dimension.Titan, 1.35, 2.35, 1.06f, false,
                "Властелин колец из миллиардов ледяных глыб."),
            new CelestialBodyDef(CelestialBodyId.Titan, "titan", "Титан", StarSystemId.SolarSystem, CelestialBodyKind.Moon,
                1438.0, 0.00058, 2574.7, 0.90f, 0.62f, 0.26f, 0.68f, 0.42f, 0.16f, true, Dimension.Titan, 0, 0, 0.18f, false,
                "Оранжевый спутник Сатурна с метановыми озёрами и дюнами."),
            new CelestialBodyDef(CelestialBodyId.Uranus, "uranus", "Уран", StarSystemId.SolarSystem, CelestialBodyKind.IceGiant,
                2872.5, 0.00020, 25362.0, 0.56f, 0.88f, 0.92f, 0.40f, 0.74f, 0.82f, false, Dimension.Pluto, 1.45, 1.85, 0.89f, false,
                "Ледяной гигант с наклонёнными кольцами (98°)."),
            new CelestialBodyDef(CelestialBodyId.Neptune, "neptune", "Нептун", StarSystemId.SolarSystem, CelestialBodyKind.IceGiant,
                4495.1, 0.00010, 24622.0, 0.18f, 0.36f, 0.88f, 0.12f, 0.22f, 0.64f, false, Dimension.Pluto, 0, 0, 1.14f, false,
                "Далёкий кобальтовый гигант сверхзвуковых ветров."),
            new CelestialBodyDef(CelestialBodyId.Pluto, "pluto", "Плутон", StarSystemId.SolarSystem, CelestialBodyKind.DwarfPlanet,
                5906.4, 0.00007, 1188.3, 0.84f, 0.76f, 0.70f, 0.94f, 0.92f, 0.90f, true, Dimension.Pluto, 0, 0, 0.08f, false,
                "Азотно-ледяной мир на краю пояса Койпера."),

            // Extrasolar Star Systems (Milky Way)
            new CelestialBodyDef(CelestialBodyId.ProximaB, "proxima-b", "Проксима Центавра b", StarSystemId.AlphaCentauri, CelestialBodyKind.RockyPlanet,
                75.0, 0.045, 7010.0, 0.78f, 0.34f, 0.28f, 0.32f, 0.66f, 0.62f, true, Dimension.ProximaB, 0, 0, 1.07f, false,
                "Сумеречная экзопланета ближайшей звезды Проксима Центавра."),
            new CelestialBodyDef(CelestialBodyId.CentauriPrime, "centauri-prime", "Центавр Прайм", StarSystemId.AlphaCentauri, CelestialBodyKind.GasGiant,
                310.0, 0.008, 54000.0, 0.88f, 0.66f, 0.38f, 0.54f, 0.34f, 0.22f, false, Dimension.ProximaB, 1.4, 2.1, 1.8f, false,
                "Кольцевой гигант в двойном сиянии Альфы Центавра A и B."),
            new CelestialBodyDef(CelestialBodyId.Trappist1d, "trappist-1d", "TRAPPIST-1d", StarSystemId.Trappist1, CelestialBodyKind.RockyPlanet,
                34.0, 0.09, 5020.0, 0.72f, 0.56f, 0.44f, 0.36f, 0.60f, 0.72f, true, Dimension.Trappist1e, 0, 0, 0.69f, false,
                "Океаническая внутренняя экзопланета системы TRAPPIST-1."),
            new CelestialBodyDef(CelestialBodyId.Trappist1e, "trappist-1e", "TRAPPIST-1e", StarSystemId.Trappist1, CelestialBodyKind.RockyPlanet,
                48.0, 0.06, 5860.0, 0.34f, 0.62f, 0.78f, 0.58f, 0.42f, 0.80f, true, Dimension.Trappist1e, 0, 0, 0.93f, false,
                "Обитаемый мир фиолетовых лугов в системе красного карлика."),
            new CelestialBodyDef(CelestialBodyId.SiriusKrystallos, "sirius-krystallos", "Сириус Кристаллос", StarSystemId.Sirius, CelestialBodyKind.RockyPlanet,
                260.0, 0.011, 7400.0, 0.72f, 0.90f, 1.0f, 0.48f, 0.72f, 0.96f, true, Dimension.Europa, 0, 0, 0.85f, false,
                "Ледяная планета в голубом излучении двойной звезды Сириус."),
            new CelestialBodyDef(CelestialBodyId.Kepler452b, "kepler-452b", "Kepler-452b", StarSystemId.Kepler452, CelestialBodyKind.RockyPlanet,
                156.0, 0.016, 9550.0, 0.26f, 0.72f, 0.62f, 0.22f, 0.42f, 0.78f, true, Dimension.Kepler452b, 0, 0, 1.60f, false,
                "Древняя биолюминесцентная суперземля."),

            // Galaxy 2: Andromeda (M31)
            new CelestialBodyDef(CelestialBodyId.AndromedaPrime, "andromeda-prime", "Элизиум M31", StarSystemId.AndromedaCore, CelestialBodyKind.RockyPlanet,
                185.0, 0.014, 7900.0, 0.64f, 0.36f, 0.94f, 0.96f, 0.80f, 0.36f, true, Dimension.AndromedaPrime, 1.4, 2.0, 1.12f, false,
                "Кристальный мир в рукаве галактики Андромеды M31."),
            new CelestialBodyDef(CelestialBodyId.MirachCitadel, "mirach-citadel", "Цитадель Мирах", StarSystemId.AndromedaCore, CelestialBodyKind.OrbitalStation,
                186.5, 0.014, 2.4, 0.92f, 0.84f, 0.98f, 0.62f, 0.36f, 0.92f, false, Dimension.AndromedaPrime, 0, 0, 0f, true,
                "Древний ретранслятор на орбите Элизиума M31."),
            new CelestialBodyDef(CelestialBodyId.Somnia, "somnia", "Сомния · Колыбель Сна", StarSystemId.AndromedaDreamRift, CelestialBodyKind.RockyPlanet,
                122.0, 0.019, 6680.0, 0.82f, 0.80f, 0.90f, 0.18f, 0.14f, 0.28f, true, Dimension.Somnia, 1.35, 1.9, 0.62f, false,
                "Бледный мир поющих монолитов — разгадка сонного паралича.")
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
            if (b.IsStar || b.OrbitRadiusMkm <= 0.001) return new SpaceVector(0, 0, 0);
            double rKm = b.OrbitRadiusMkm * 1000000.0;
            double ang = (int)id * 1.13 + orbitDays * b.OrbitSpeedRadPerDay;
            double yKm = Math.Sin((int)id * 2.1 + ang) * (rKm * 0.035);
            return new SpaceVector(Math.Cos(ang) * rKm, yKm, Math.Sin(ang) * rKm);
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
        public bool Active;
        public bool ManualControl = true;
        public GalaxyId CurrentGalaxy = GalaxyId.MilkyWay;
        public StarSystemId CurrentSystem = StarSystemId.SolarSystem;
        public CelestialBodyId TargetBody = CelestialBodyId.Moon;
        public SpaceVector PositionKm = new SpaceVector(149650000.0, 0, 0);
        public Quaternion Rotation = Quaternion.identity;
        public Vector3 Velocity = Vector3.zero;
        public float Throttle = 0.45f;
        public bool Boost;
        public bool InHyperjump;
        public float HyperjumpProgress;
        public double OrbitTimeDays = 12.0;

        CelestialBodyId hyperjumpTarget;

        public CelestialBodyDef TargetBodyDef => CelestialAtlas.GetBody(TargetBody);

        public double DistanceToTargetKm()
        {
            var def = CelestialAtlas.GetBody(TargetBody);
            if (def.System != CurrentSystem) return 9.461e12;
            double raw = SpaceVector.Distance(PositionKm, CelestialAtlas.WorldPositionKm(TargetBody, OrbitTimeDays));
            return Math.Max(0.0, raw - def.RadiusKm);
        }

        public bool InLandingRange =>
            Active && !InHyperjump &&
            CelestialAtlas.GetBody(TargetBody).System == CurrentSystem &&
            DistanceToTargetKm() <= Math.Max(45000.0, CelestialAtlas.GetBody(TargetBody).RadiusKm * 8.0);

        public void EnterOrbitAround(CelestialBodyId bodyId)
        {
            var b = CelestialAtlas.GetBody(bodyId);
            CurrentSystem = b.System;
            CurrentGalaxy = CelestialAtlas.GetSystem(CurrentSystem).Galaxy;
            PositionKm = CelestialAtlas.WorldPositionKm(bodyId, OrbitTimeDays) + new SpaceVector(Math.Max(12000.0, b.RadiusKm * 3.0), 1500.0, 0.0);
            Rotation = Quaternion.identity;
            Velocity = Vector3.forward * 25f;
            Throttle = 0.45f;
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
            OrbitTimeDays += dt * 0.0005;

            if (InHyperjump)
            {
                HyperjumpProgress += dt * 0.85f;
                Velocity = Rotation * Vector3.forward * 25000f;
                if (HyperjumpProgress >= 1f)
                {
                    InHyperjump = false;
                    HyperjumpProgress = 0f;
                    Boost = false;
                    Throttle = 0.08f;
                    Velocity = Vector3.zero;
                    var dest = CelestialAtlas.GetBody(hyperjumpTarget);
                    CurrentSystem = dest.System;
                    CurrentGalaxy = CelestialAtlas.GetSystem(CurrentSystem).Galaxy;
                    TargetBody = dest.Id;
                    SpaceVector targetWorld = CelestialAtlas.WorldPositionKm(dest.Id, OrbitTimeDays);
                    PositionKm = targetWorld + new SpaceVector(0.0, dest.RadiusKm * 0.5, -Math.Max(18000.0, dest.RadiusKm * 3.5));
                    Rotation = Quaternion.identity;
                    return;
                }
                return;
            }

            if (ManualControl)
            {
                Quaternion deltaRot = Quaternion.Euler(-pitchInput * 68f * dt, yawInput * 68f * dt, -rollInput * 75f * dt);
                Rotation = (Rotation * deltaRot).normalized;
            }
            else if (TargetBodyDef.System == CurrentSystem)
            {
                SpaceVector toDest = CelestialAtlas.WorldPositionKm(TargetBody, OrbitTimeDays) - PositionKm;
                Vector3 dir = new Vector3((float)toDest.Normalized.X, (float)toDest.Normalized.Y, (float)toDest.Normalized.Z);
                if (dir.sqrMagnitude > 0.001f)
                    Rotation = Quaternion.Slerp(Rotation, Quaternion.LookRotation(dir, Vector3.up), 1f - Mathf.Exp(-dt * 2.8f));
            }

            double kmPerSec = Throttle * (Boost ? 1850000.0 : 220000.0);
            Vector3 fwd = Rotation * Vector3.forward;
            Velocity = fwd * (Throttle * (Boost ? 340f : 95f));
            PositionKm = PositionKm + new SpaceVector(fwd.x * kmPerSec * dt, fwd.y * kmPerSec * dt, fwd.z * kmPerSec * dt);
        }
    }
}
