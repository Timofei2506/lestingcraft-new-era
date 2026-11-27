namespace LestingCraft.World
{
    /// <summary>
    /// Playable worlds. Pure data so generation and checks compile without Unity.
    /// IDs 0..17 are the legacy 1.x ladder and must never be renumbered (they are part of
    /// the network protocol, the save format and every authored structure coordinate).
    /// IDs 18..23 are the "Deep Frontier" batch added in 2.0: six new realms, each with its
    /// own terrain sampler, palette, gravity, atmosphere, wildlife habitat and world boss.
    /// </summary>
    public enum Dimension
    {
        Lestingia = 0,
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
        Backrooms = 15,
        Inferno = 16,
        Paradise = 17,

        // ---- Deep Frontier (schema 2.0) ----
        Crystalia = 18,     // shattered prismic moon: crystal spires, geode caverns, aurora skies
        Emberfall = 19,     // ash world: sky islands of basalt over a burning sea of soot
        Aquaria = 20,       // ocean world: archipelagos, kelp cathedrals, tidal ruins
        Stormreach = 21,    // lightning world: sky-lattice towers over a permanent thunderhead
        Verdant = 22,       // fungal super-Earth: colossal mycelium forests, spore canopies
        Nihility = 23       // broken reality: inverted gravity fragments, void shards
    }

    /// <summary>
    /// Pure, engine-free grouping &amp; classification of realms. Every consumer (generation,
    /// lighting, wildlife, UI, survival) asks these questions instead of comparing enum
    /// literals, so adding a realm never again requires a scattered 40-file edit.
    /// </summary>
    public static class DimensionRealm
    {
        public const int Count = 24;
        public const int FirstDeepFrontier = 18;

        public static bool IsValid(int id) { return id >= 0 && id < Count; }
        public static bool IsValid(Dimension d) { return IsValid((int)d); }

        /// <summary>Worlds whose surface is authored voxel geology with its own sampler.</summary>
        public static bool IsPlanetaryBody(Dimension d)
        {
            return d != Dimension.Lestingia && d != Dimension.Backrooms && d != Dimension.Nihility;
        }

        /// <summary>Realms sealed from the sky: no volumetric cloud layer, no open skydome.</summary>
        public static bool IsEnclosed(Dimension d)
        {
            return d == Dimension.Backrooms || d == Dimension.Dream || d == Dimension.Nihility;
        }

        /// <summary>Airless bodies: vacuum sky, razor horizon, stars visible at noon.</summary>
        public static bool IsAirless(Dimension d)
        {
            switch (d)
            {
                case Dimension.Moon:
                case Dimension.Mercury:
                case Dimension.Europa:
                case Dimension.Pluto:
                case Dimension.Crystalia:
                case Dimension.Nihility:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Worlds with a breathable atmosphere and a survivable surface.</summary>
        public static bool HasBreathableAir(Dimension d)
        {
            switch (d)
            {
                case Dimension.Lestingia:
                case Dimension.Paradise:
                case Dimension.Kepler452b:
                case Dimension.Trappist1e:
                case Dimension.Aquaria:
                case Dimension.Verdant:
                case Dimension.Emberfall:
                case Dimension.Backrooms:
                case Dimension.Somnia:
                case Dimension.Inferno:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>Free-floating low-gravity worlds where 0g EVA is possible.</summary>
        public static bool IsZeroGravityCapable(Dimension d)
        {
            return d == Dimension.Pluto || d == Dimension.Nihility || d == Dimension.Crystalia || d == Dimension.Europa;
        }

        /// <summary>Realms reached only by the Deep Frontier gate (never by the 1.x shuttle list).</summary>
        public static bool IsDeepFrontier(Dimension d) { return (int)d >= FirstDeepFrontier; }

        public static string Code(Dimension d)
        {
            switch (d)
            {
                case Dimension.Lestingia: return "LST";
                case Dimension.Moon: return "LUN";
                case Dimension.Mars: return "MRS";
                case Dimension.Venus: return "VNS";
                case Dimension.Mercury: return "MRC";
                case Dimension.Europa: return "EUR";
                case Dimension.Titan: return "TTN";
                case Dimension.Pluto: return "PLT";
                case Dimension.ProximaB: return "PRX";
                case Dimension.Trappist1e: return "TRP";
                case Dimension.Kepler452b: return "KEP";
                case Dimension.AndromedaPrime: return "AND";
                case Dimension.Somnia: return "SOM";
                case Dimension.Dream: return "DRM";
                case Dimension.Radia: return "RAD";
                case Dimension.Backrooms: return "BKR";
                case Dimension.Inferno: return "INF";
                case Dimension.Paradise: return "PRD";
                case Dimension.Crystalia: return "CRY";
                case Dimension.Emberfall: return "EMB";
                case Dimension.Aquaria: return "AQA";
                case Dimension.Stormreach: return "STM";
                case Dimension.Verdant: return "VRD";
                case Dimension.Nihility: return "NIH";
                default: return "UNK";
            }
        }

        public static string NameRu(Dimension d)
        {
            switch (d)
            {
                case Dimension.Lestingia: return "Лестингия";
                case Dimension.Moon: return "Луна";
                case Dimension.Mars: return "Марс";
                case Dimension.Venus: return "Венера";
                case Dimension.Mercury: return "Меркурий";
                case Dimension.Europa: return "Европа";
                case Dimension.Titan: return "Титан";
                case Dimension.Pluto: return "Плутон";
                case Dimension.ProximaB: return "Проксима b";
                case Dimension.Trappist1e: return "Траппист-1e";
                case Dimension.Kepler452b: return "Кеплер-452b";
                case Dimension.AndromedaPrime: return "Андромеда";
                case Dimension.Somnia: return "Сомния";
                case Dimension.Dream: return "Сон";
                case Dimension.Radia: return "Радиа";
                case Dimension.Backrooms: return "Закулисье";
                case Dimension.Inferno: return "Преисподняя";
                case Dimension.Paradise: return "Эдем";
                case Dimension.Crystalia: return "Кристалия";
                case Dimension.Emberfall: return "Пепельный Водопад";
                case Dimension.Aquaria: return "Аквария";
                case Dimension.Stormreach: return "Грозовой Предел";
                case Dimension.Verdant: return "Вердант";
                case Dimension.Nihility: return "Нихилиум";
                default: return "Неизвестный мир";
            }
        }

    }
}
