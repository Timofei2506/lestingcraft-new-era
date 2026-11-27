using System;

namespace LestingCraft.World
{
    /// <summary>Terrain family a body's sampler follows. Themers and biome sets key off this.</summary>
    public enum PlanetClade
    {
        Layered,        // classic noise-strata (Mars, Venus, Mercury …)
        Cryo,           // cracked ice sheets (Europa, Pluto)
        Dune,           // migrating methane/hydrocarbon dunes (Titan)
        Crystal,        // faceted spires & geode basins (Crystalia)
        SkyIslands,     // floating fragments above a sea (Emberfall)
        Archipelago,    // drowned continents & kelp cathedrals (Aquaria)
        Canopy,         // fungal mega-flora over rolling loam (Verdant)
        Void            // inverted fragments & glass scarps (Nihility)
    }

    /// <summary>How the atmosphere reacts to the day/night clock in both code terms and colour.</summary>
    public enum AtmosphereKind
    {
        Oxygen,         // Earth-like: blue zenith, warm horizon
        ThinRed,        // Martian dust
        Crushing,       // Venusian greenhouse
        Airless,        // vacuum, black sky at noon
        Icy,            // faint blue scatter, high albedo bounce
        Haze,           // thick photochemical smog
        Exotic,         // alien-chemistry sky (violet, emerald, rose)
        Enclosed,       // no sky at all (interior / underworld)
        Storm,          // permanent overcast + lightning
        Aurora          // crystal prism sky with ionised bands
    }

    /// <summary>
    /// Numeric definition of one body: gravity, atmosphere, terrain relief and the
    /// resource band it generates. Everything here is authored data; generators read it,
    /// they never hard-code a body again.
    /// </summary>
    public readonly struct PlanetParameters
    {
        public readonly Dimension Dimension;
        public readonly PlanetBody Body;
        public readonly PlanetClade Clade;
        public readonly AtmosphereKind Sky;

        /// <summary>Surface gravity as a fraction of Lestingia (1.0).</summary>
        public readonly float Gravity;
        /// <summary>Atmosphere pressure in Earth atmospheres (0 = vacuum).</summary>
        public readonly float PressureAtm;
        /// <summary>Mean surface temperature in °C, used by survival and wildlife.</summary>
        public readonly float MeanCelsius;
        /// <summary>Fog density multiplier (1.0 = Lestingia).</summary>
        public readonly float FogScale;
        /// <summary>Volumetric cloud coverage 0..1 before weather modulation.</summary>
        public readonly float Clouds;
        /// <summary>1 = razor-sharp horizon, larger = shorter draw distance.</summary>
        public readonly float HorizonClarity;

        /// <summary>Lowest and highest walkable surface Y the sampler may emit.</summary>
        public readonly int MinY, MaxY, SeaLevel;
        /// <summary>Typical relief in blocks between a basin floor and a ridge crest.</summary>
        public readonly int Relief;
        /// <summary>0..1 how much the terrain folds, i.e. how cliffy the manifold is.</summary>
        public readonly float Folding;
        /// <summary>0..1 ore/resource density multiplier.</summary>
        public readonly float ResourceRichness;
        /// <summary>Seed of the authored sky palette (see AtmosphereLighting).</summary>
        public readonly string Palette;

        public PlanetParameters(Dimension dimension, PlanetBody body, PlanetClade clade, AtmosphereKind sky,
            float gravity, float pressureAtm, float meanCelsius, float fogScale, float clouds,
            float horizonClarity, int minY, int maxY, int seaLevel, int relief, float folding,
            float resourceRichness, string palette)
        {
            Dimension = dimension; Body = body; Clade = clade; Sky = sky;
            Gravity = gravity; PressureAtm = pressureAtm; MeanCelsius = meanCelsius;
            FogScale = fogScale; Clouds = clouds; HorizonClarity = horizonClarity;
            MinY = minY; MaxY = maxY; SeaLevel = seaLevel; Relief = relief;
            Folding = folding; ResourceRichness = resourceRichness; Palette = palette;
        }

        public bool IsVacuum { get { return PressureAtm <= 0.02f; } }
        public bool IsHabitable { get { return PressureAtm >= 0.6f && MeanCelsius > -40f && MeanCelsius < 65f; } }
        public float GravityScale { get { return Gravity < .12f ? .12f : Gravity > 2f ? 2f : Gravity; } }
    }

    /// <summary>
    /// The one table every system consults. Before 2.0 gravity lived in SpaceFlightVisuals,
    /// fog in AtmosphereLighting, breathability in SurvivalSession and height bounds in
    /// each sampler; they could and did drift. Now they are derived from a single row.
    /// </summary>
    public static class PlanetCatalog
    {
        static readonly PlanetParameters[] rows = new PlanetParameters[DimensionRealm.Count];

        static PlanetCatalog()
        {
            //                     dim                    body                    clade               sky                 g     atm   °C    fog  clouds horizon minY maxY sea  relief fold  res  palette
            Set(new PlanetParameters(Dimension.Lestingia,     0,                      PlanetClade.Layered,     AtmosphereKind.Oxygen,   1.00f, 1.00f,  14f, 1.00f, .42f, 1.00f,  4,  96, 40,  34, .35f, 1.00f, "earth"));
            Set(new PlanetParameters(Dimension.Moon,          PlanetBody.Moon,        PlanetClade.Layered,     AtmosphereKind.Airless,  .22f, 0.00f, -20f, 0.02f, .00f, 4.00f, 12,  90,  0,  31, .30f, .55f, "luna"));
            Set(new PlanetParameters(Dimension.Mars,          PlanetBody.Mars,        PlanetClade.Layered,     AtmosphereKind.ThinRed,  .42f, 0.01f, -55f, 1.35f, .10f, 1.30f, 12,  96,  0,  36, .55f, .85f, "mars"));
            Set(new PlanetParameters(Dimension.Venus,         PlanetBody.Venus,       PlanetClade.Layered,     AtmosphereKind.Crushing, .90f, 92.0f, 464f, 4.20f, .96f, .55f,  12,  96,  0,  20, .40f, 1.20f, "venus"));
            Set(new PlanetParameters(Dimension.Mercury,       PlanetBody.Mercury,     PlanetClade.Layered,     AtmosphereKind.Airless,  .40f, 0.00f, 167f, 0.02f, .00f, 4.00f, 12,  90,  0,  16, .48f, 1.35f, "mercury"));
            Set(new PlanetParameters(Dimension.Europa,        PlanetBody.Europa,      PlanetClade.Cryo,        AtmosphereKind.Airless,  .14f, 0.00f, -171f, 0.18f, .00f, 2.50f, 12,  92,  0,  14, .25f, .75f, "europa"));
            Set(new PlanetParameters(Dimension.Titan,         PlanetBody.Titan,       PlanetClade.Dune,        AtmosphereKind.Haze,     .18f, 1.45f, -179f, 3.40f, .72f, .70f,  12,  92, 41,  20, .30f, .90f, "titan"));
            Set(new PlanetParameters(Dimension.Pluto,         PlanetBody.Pluto,       PlanetClade.Cryo,        AtmosphereKind.Icy,      .08f, 0.01f, -229f, 0.20f, .00f, 3.00f, 12,  92,  0,  13, .22f, .70f, "pluto"));
            Set(new PlanetParameters(Dimension.ProximaB,      PlanetBody.ProximaB,    PlanetClade.Layered,     AtmosphereKind.Exotic,   1.07f, 1.10f,  22f, 1.20f, .28f, 1.00f, 12,  96,  0,  40, .62f, 1.10f, "proxima"));
            Set(new PlanetParameters(Dimension.Trappist1e,    PlanetBody.Trappist1e,  PlanetClade.Archipelago, AtmosphereKind.Exotic,   .93f, 1.05f,  11f, .95f, .38f, 1.10f, 12,  96, 42,  26, .30f, 1.00f, "trappist"));
            Set(new PlanetParameters(Dimension.Kepler452b,    PlanetBody.Kepler452b,  PlanetClade.Canopy,      AtmosphereKind.Exotic,   1.60f, 1.35f,  19f, 1.05f, .44f, 1.05f, 12,  104, 0,  44, .58f, 1.25f, "kepler"));
            Set(new PlanetParameters(Dimension.AndromedaPrime, PlanetBody.AndromedaPrime, PlanetClade.Crystal, AtmosphereKind.Aurora, 1.12f, .65f,  -8f, .90f, .34f, 1.20f, 12, 104,  0,  30, .60f, 1.60f, "andromeda"));
            Set(new PlanetParameters(Dimension.Somnia,        PlanetBody.Somnia,      PlanetClade.Void,        AtmosphereKind.Exotic,   .62f, .80f,   4f, 2.60f, .58f, .80f,  12,  96,  0,  36, .50f, .80f, "somnia"));
            Set(new PlanetParameters(Dimension.Dream,         PlanetBody.Dream,       PlanetClade.Void,        AtmosphereKind.Enclosed, .86f, .00f,  10f, 3.80f, .00f, .60f,  12,  96,  0,  24, .15f, .40f, "dream"));
            Set(new PlanetParameters(Dimension.Radia,         PlanetBody.Radia,       PlanetClade.Layered,     AtmosphereKind.Storm,   1.04f, 1.20f,   9f, 2.85f, .94f, .75f,  12,  96, 50,  24, .35f, 1.15f, "radia"));
            Set(new PlanetParameters(Dimension.Backrooms,     PlanetBody.Backrooms,   PlanetClade.Void,        AtmosphereKind.Enclosed, .95f, .90f,  21f, 3.60f, .00f, .45f,  12,  96,  0,  10, .05f, .30f, "backrooms"));
            Set(new PlanetParameters(Dimension.Inferno,       PlanetBody.Inferno,     PlanetClade.Layered,     AtmosphereKind.Enclosed, .94f, 3.10f, 480f, 2.95f, .00f, .60f,  12,  96, 28,  46, .70f, 1.40f, "inferno"));
            Set(new PlanetParameters(Dimension.Paradise,      PlanetBody.Paradise,    PlanetClade.Layered,     AtmosphereKind.Oxygen,   .78f, 1.10f,  23f, .48f, .40f, 1.60f, 12,  96,  0,  40, .40f, 1.30f, "paradise"));
            Set(new PlanetParameters(Dimension.Crystalia,     PlanetBody.Crystalia,   PlanetClade.Crystal,     AtmosphereKind.Aurora,   0.28f, 0.03f, -140f, .12f, .00f, 3.20f, 12, 112, 0, 58, .85f, 1.95f, "crystalia"));
            Set(new PlanetParameters(Dimension.Emberfall,     PlanetBody.Emberfall,   PlanetClade.SkyIslands,  AtmosphereKind.Haze,     .64f, 1.15f,  61f, 2.20f, .62f, .85f,  12, 112, 32, 62, .80f, 1.30f, "emberfall"));
            Set(new PlanetParameters(Dimension.Aquaria,       PlanetBody.Aquaria,     PlanetClade.Archipelago, AtmosphereKind.Oxygen,   .88f, 1.25f,  24f, 1.15f, .52f, .95f,  12, 100, 44, 52, .45f, 1.05f, "aquaria"));
            Set(new PlanetParameters(Dimension.Stormreach,    PlanetBody.Stormreach,  PlanetClade.SkyIslands,  AtmosphereKind.Storm,    .74f, 1.60f,   8f, 3.10f, .98f, .60f,  12, 116,  0, 66, .75f, 1.15f, "stormreach"));
            Set(new PlanetParameters(Dimension.Verdant,       PlanetBody.Verdant,     PlanetClade.Canopy,      AtmosphereKind.Exotic,   .96f, 1.30f,  27f, 1.60f, .48f, .90f,  12, 108, 38, 50, .50f, 1.35f, "verdant"));
            Set(new PlanetParameters(Dimension.Nihility,      PlanetBody.Nihility,    PlanetClade.Void,        AtmosphereKind.Airless,  .10f, 0.00f, -60f, .05f, .00f, 4.00f, 12, 120,  0, 78, .95f, 1.50f, "nihility"));
        }

        static void Set(PlanetParameters p) { rows[(int)p.Dimension] = p; }

        public static PlanetParameters Get(Dimension d)
        {
            int i = (int)d;
            return i >= 0 && i < rows.Length ? rows[i] : rows[0];
        }

        public static PlanetParameters Get(PlanetBody b) { return Get(BodyDimension(b)); }

        public static Dimension BodyDimension(PlanetBody body)
        {
            if (body == 0) return Dimension.Lestingia;
            int id = (int)body;
            return DimensionRealm.IsValid(id) ? (Dimension)id : Dimension.Mars;
        }

        public static PlanetBody BodyOf(Dimension d)
        {
            if (d == Dimension.Lestingia) return 0;
            return DimensionRealm.IsPlanetaryBody(d) ? (PlanetBody)(int)d : 0;
        }

        public static float Gravity(Dimension d) { return Get(d).GravityScale; }
        public static float FogScale(Dimension d) { return Get(d).FogScale; }
        public static float MeanCelsius(Dimension d) { return Get(d).MeanCelsius; }
        public static bool IsVacuum(Dimension d) { return Get(d).IsVacuum; }
        public static AtmosphereKind Sky(Dimension d) { return Get(d).Sky; }
        public static PlanetClade Clade(Dimension d) { return Get(d).Clade; }

        /// <summary>Deep-frontier realms in travel order, for the gate UI and navigation.</summary>
        public static readonly Dimension[] DeepFrontier =
        {
            Dimension.Crystalia, Dimension.Emberfall, Dimension.Aquaria,
            Dimension.Stormreach, Dimension.Verdant, Dimension.Nihility
        };

        public static readonly Dimension[] All = BuildAll();

        static Dimension[] BuildAll()
        {
            var all = new Dimension[DimensionRealm.Count];
            for (int i = 0; i < all.Length; i++) all[i] = (Dimension)i;
            return all;
        }
    }

    /// <summary>
    /// Engine-free spawn planning for every realm. Extracted from the Unity transition code
    /// so /frontier, portals and the HUD all agree on where a player materialises, and so
    /// the headless checks can cover it. No UnityEngine types are referenced here.
    /// </summary>
    public static class PlanetSpawn
    {
        /// <summary>Safety margin above the tallest authored feature a body can produce.</summary>
        public const int Headroom = 6;

        /// <summary>Highest solid column a sampler can emit for this body (catalog max + relief).</summary>
        public static int CeilingY(Dimension d)
        {
            var p = PlanetCatalog.Get(d);
            return Math.Max(p.MaxY, p.SeaLevel) + Headroom;
        }

        /// <summary>Lowest legal player Y: below this the body is vacuum or bedrock.</summary>
        public static int FloorY(Dimension d)
        {
            var p = PlanetCatalog.Get(d);
            return Math.Max(p.MinY, 1) + 1;
        }

        /// <summary>Where a newcomer materialises: above sea level, on solid ground, clamped.</summary>
        public static int SpawnY(Dimension d, int groundHeight)
        {
            int y = groundHeight + 2;
            int floor = FloorY(d);
            int ceiling = CeilingY(d);
            if (y < floor) y = floor;
            if (y > ceiling) y = ceiling;
            return y;
        }

        /// <summary>True when the body needs a pressure suit / sealed cockpit for the player.</summary>
        public static bool NeedsSuit(Dimension d)
        {
            var p = PlanetCatalog.Get(d);
            return p.PressureAtm < .55f || p.Sky == AtmosphereKind.Airless || p.Sky == AtmosphereKind.Crushing;
        }

        /// <summary>Fall damage scale: low gravity lets the player drop further safely.</summary>
        public static float FallDamageScale(Dimension d)
        {
            var p = PlanetCatalog.Get(d);
            return p.Gravity <= .01f ? 0f : Math.Max(.15f, Math.Min(2.2f, p.Gravity));
        }

        /// <summary>Safe descent speed clamp (m/s) for the flight assist on this body.</summary>
        public static float TerminalSpeed(Dimension d)
        {
            var p = PlanetCatalog.Get(d);
            return 22f * (float)Math.Sqrt(Math.Max(.05, p.Gravity));
        }
    }
}
