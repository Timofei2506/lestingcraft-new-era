using System;
using System.Collections.Generic;

namespace LestingCraft.Gameplay
{
    [Serializable]
    public sealed class AnimalState
    {
        public string id;
        public int species, health, wander;
        public float impulseX, impulseZ, fallSpeed;
        public float x, y, z, yaw, homeX, homeZ, goalX, goalZ, timer, anger, bite;
        public float flightHeight, flightPhase;
        public bool dead;
        public AnimalState Copy() { return (AnimalState)MemberwiseClone(); }
    }

    [Serializable]
    public sealed class WildlifeSave
    {
        public AnimalState[] animals = new AnimalState[0];
        public string[] sites = new string[0];
    }

    [Serializable]
    public sealed class AnimalLoot
    {
        public string id;
        public int meat, hide;
    }

    public struct AnimalObserver
    {
        public string id;
        public float x, y, z;
        public bool alive;
    }

    public sealed class WildlifeSimulation
    {
        public const int SpeciesCount = 42;
        public const int MaxAnimals = 384, MaxSites = 8192, ActiveLimit = 32, ActiveLimitV8 = 96;
        public readonly List<AnimalState> Animals = new List<AnimalState>();
        readonly HashSet<string> sites = new HashSet<string>();
        readonly int seed, contentVersion;
        int cursor;

        // The surface callback is deliberately engine-independent. It returns the
        // natural surface block at a spawn location, before clearance is tested.
        public Func<int, int, int> SurfaceAt;
        public Func<int, int, int> SkySurfaceAt;

        public delegate float Ground(float x, float z, float previousY, float radius, float height);

        // 0..11 classic + 12..19 v8 exotic + 20..29 Era v9 Hostile Radia Abyss Mutants:
        // 20:AshHound, 21:CherenkovLeech, 22:RadScorpion, 23:MutantBoar, 24:SlagCrab,
        // 25:FalloutCrow, 26:GammaRay, 27:BoneStalker, 28:VitrifiedGolem, 29:WraithStalker
        static readonly int[] Health =
        {
            30, 8, 40, 34, 85, 14, 95, 26, 30, 18, 10, 20, 16, 18, 68, 22, 32, 110, 28, 55,
            65, 48, 85, 115, 95, 42, 90, 130, 175, 140,
            70, 24, 16, 150,
            88, 115, 58, 92, 36, 95, 120, 28
        };
        static readonly int[] Meat =
        {
            3, 1, 4, 2, 6, 1, 7, 3, 2, 2, 1, 2, 1, 2, 5, 1, 2, 8, 3, 2,
            2, 1, 2, 5, 2, 1, 3, 2, 0, 1,
            4, 1, 1, 9,
            3, 4, 2, 2, 2, 5, 6, 2
        };
        static readonly int[] Hide =
        {
            2, 1, 2, 2, 4, 2, 4, 2, 3, 2, 1, 1, 2, 2, 3, 2, 3, 5, 2, 3,
            3, 2, 4, 4, 4, 2, 4, 5, 6, 5,
            5, 2, 2, 7,
            4, 5, 3, 4, 3, 5, 6, 3
        };
        static readonly int[] AttackDamage =
        {
            0, 0, 8, 6, 12, 0, 10, 0, 7, 0, 0, 0, 0, 0, 0, 9, 0, 11, 0, 0,
            11, 9, 14, 15, 12, 10, 13, 16, 19, 17,
            15, 7, 0, 0,
            14, 11, 12, 15, 0, 0, 0, 0,
            13, 21, 9, 16, 8, 22
        };
        static readonly float[] Heights =
        {
            2.10f, .45f, 1.05f, .96f, 1.72f, .55f, 2.06f, 1.16f, .78f, .40f, .24f, .72f, .57f, .75f, 2.53f, .38f, .34f, 2.78f, .60f, .58f,
            1.35f, .34f, 1.31f, 1.52f, 1.02f, .48f, .58f, 2.73f, 3.14f, 2.70f,
            1.34f, .74f, .60f, 2.50f,
            1.38f, 2.25f, .95f, 1.28f, .86f, 2.20f, 2.15f, .68f
        };
        static readonly float[] Radii =
        {
            .34f, .15f, .36f, .26f, .56f, .16f, .64f, .24f, .22f, .22f, .18f, .48f, .28f, .24f, .50f, .26f, .28f, .82f, .26f, 1.25f,
            .36f, .36f, .82f, .54f, .95f, .28f, 1.25f, .58f, 1.12f, .65f,
            .32f, .70f, .42f, 2.10f,
            .38f, .52f, .42f, .78f, .36f, .38f, .48f, .30f
        };
        static readonly float[] Lengths =
        {
            1.65f, .48f, 1.80f, 1.52f, 2.50f, .98f, 2.62f, .98f, .88f, .98f, .38f, .95f, .42f, .46f, 2.79f, .72f, .78f, 2.70f, .97f, 3.45f,
            2.18f, 1.52f, 2.48f, 2.51f, 1.92f, .68f, 3.45f, 1.95f, 1.30f, 1.10f,
            2.11f, 1.52f, 1.02f, 6.40f,
            2.15f, 1.85f, 1.15f, 2.35f, 1.10f, 1.75f, 2.05f, .82f
        };
        static readonly float[] Walk =
        {
            .65f, .75f, .65f, 1.15f, .65f, 1.25f, .55f, .9f, 1.05f, .55f, .5f, 2.8f, 2.4f, .62f, .72f, .95f, .35f, .60f, .58f, 2.6f,
            1.45f, 1.15f, 1.10f, 1.05f, .85f, 2.9f, 2.7f, 1.35f, .75f, 1.55f,
            1.40f, 1.55f, .45f, .55f,
            1.55f, 1.05f, 2.85f, 1.15f, 2.15f, .95f, 2.65f, 2.75f
        };
        static readonly float[] Run =
        {
            3.8f, 4.2f, 3.8f, 4.6f, 3.3f, 4.8f, 3.4f, 3.6f, 4.4f, 2.1f, 2.7f, 4.5f, 4.1f, 2.4f, 3.5f, 3.6f, 1.2f, 3.9f, 2.8f, 4.6f,
            5.4f, 4.2f, 4.4f, 4.9f, 3.4f, 5.6f, 5.1f, 5.5f, 3.5f, 5.8f,
            5.9f, 4.6f, 1.4f, 2.6f,
            5.8f, 3.8f, 5.4f, 4.5f, 4.2f, 4.6f, 5.2f, 4.8f
        };

        static readonly int[][] HabitatSpeciesV7 =
        {
            new[] { 0, 0, 1, 1, 5, 6, 6, 11 },
            new[] { 0, 0, 1, 2, 2, 3, 4, 5, 8, 11 },
            new[] { 0, 1, 3, 3, 4, 4, 6, 8, 11 },
            new[] { 1, 1, 5, 5, 7, 11 },
            new[] { 2, 2, 9, 9, 9, 10, 10, 10, 11 },
            new[] { 1, 3, 7, 7, 7, 8, 11, 11 }
        };

        static readonly int[][] HabitatSpeciesV8 =
        {
            new[] { 0, 1, 5, 6, 11, 12, 16, 18 },             // 0: Meadow & Sakura glades
            new[] { 0, 1, 2, 3, 4, 5, 8, 11, 12, 17 },        // 1: Mixed & Sakura Forests
            new[] { 0, 1, 3, 4, 6, 8, 12, 13, 13, 17, 17 },   // 2: Snow / Taiga / Glacial Ice
            new[] { 1, 5, 7, 11, 14, 14, 15, 15 },            // 3: Dunes & Red Mesa Canyon
            new[] { 2, 9, 10, 11, 16, 16, 18, 18, 19 },       // 4: Marsh, Glow-Shroom & Water
            new[] { 1, 3, 7, 8, 11, 12, 15, 17, 19 },         // 5: Rocky, Volcanic & Andromeda
            new[] { 20, 21, 22, 23, 24, 25, 26, 27, 28, 29 }  // 6: Radia Irradiated Abyss — 10 hostile mutants!
        };

        // v9 adds four species (30..33) without touching the v8 tables, so existing worlds
        // keep spawning exactly the animals they used to.
        // 30:FrostWolf, 31:GlassSpider, 32:FungusBeetle, 33:SkyWhale
        static readonly int[][] HabitatSpeciesV9 =
        {
            // SkyWhale (33) is not a ground-meadow animal: it spawns only from the soaring
            // sky-island pass below, so it no longer appears in a forest glade.
            new[] { 0, 1, 5, 6, 11, 12, 16, 18, 30, 32 },
            new[] { 0, 1, 2, 3, 4, 5, 8, 11, 12, 17, 30, 31, 32 },
            new[] { 0, 1, 3, 4, 6, 8, 12, 13, 13, 17, 17, 30, 30, 31 },
            new[] { 1, 5, 7, 11, 14, 14, 15, 15, 31, 32 },
            new[] { 2, 9, 10, 11, 16, 16, 18, 18, 19, 31, 32, 32 },
            new[] { 1, 3, 7, 8, 11, 12, 15, 17, 19, 31 },
            new[] { 20, 21, 22, 23, 24, 25, 26, 27, 28, 29 }
        };

        public static bool IsFlyer(int s) { return s == 11 || s == 12 || s == 19 || s == 25 || s == 26 || s == 33 || s == 36 || s == 38 || s == 40 || s == 41 || s == 44 || s == 47; }
        public static bool IsRadiaMutant(int s) { return (s >= 20 && s < 30) || (s >= 34 && s <= 37); }
        public static bool IsInfernoBeast(int s) { return s >= 34 && s <= 37; }
        public static bool IsParadiseCreature(int s) { return s >= 38 && s <= 41; }
        /// <summary>Deep Frontier fauna (Crystalia, Emberfall, Aquaria, Stormreach, Verdant, Nihility).</summary>
        public static bool IsFrontierCreature(int s) { return s >= 42 && s <= 47; }
        public static bool IsAquatic(int s) { return s == 43; }
        public static bool IsAggressive(int s) { return s >= 0 && s < AttackDamage.Length && AttackDamage[s] > 0; }

        public WildlifeSimulation(int seed, WildlifeSave saved = null, int contentVersion = 2)
        {
            this.seed = seed;
            this.contentVersion = contentVersion;
            if (saved == null) return;
            if (saved.sites != null)
                foreach (string site in saved.sites)
                    if (site != null && sites.Count < MaxSites) sites.Add(site);
            if (saved.animals == null) return;
            var ids = new HashSet<string>();
            foreach (AnimalState source in saved.animals)
            {
                if (source == null || source.id == null || !ValidSpecies(source.species) ||
                    Animals.Count >= MaxAnimals || !Finite(source.x) || !Finite(source.y) ||
                    !Finite(source.z) || !Finite(source.yaw) || !ids.Add(source.id)) continue;
                AnimalState a = source.Copy();
                a.health = Math.Max(0, Math.Min(MaxHealth(a.species), a.health));
                a.dead = a.dead || a.health == 0;
                a.homeX = Finite(a.homeX) ? a.homeX : a.x;
                a.homeZ = Finite(a.homeZ) ? a.homeZ : a.z;
                a.goalX = a.x;
                a.goalZ = a.z;
                a.timer = a.anger = a.bite = a.fallSpeed = a.impulseX = a.impulseZ = 0;
                a.flightPhase = Finite(a.flightPhase) ? a.flightPhase : 0;
                a.flightHeight = IsFlyer(a.species) ? ClampFlight(a.flightHeight) : 0;
                Animals.Add(a);
            }
        }

        bool ValidSpecies(int s)
        {
            int limit = contentVersion >= 12 ? SpeciesCount : contentVersion >= 9 ? 42 : contentVersion >= 8 ? 20 : contentVersion >= 7 ? 12 : 3;
            return s >= 0 && s < limit;
        }
        static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) < 32000; }
        static int Index(int s) { return s >= 0 && s < SpeciesCount ? s : 2; }
        static float ClampFlight(float h) { return Finite(h) && h >= 2.8f && h <= 8.5f ? h : 4.5f; }

        public static uint Hash(int x, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)seed ^ (uint)x * 374761393u ^ (uint)z * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                return h ^ (h >> 16);
            }
        }

        public static int MaxHealth(int s) { return Health[Index(s)]; }
        public static float Height(int s) { return Heights[Index(s)]; }
        public static float Radius(int s) { return Radii[Index(s)]; }
        public static float Length(int s) { return Lengths[Index(s)]; }
        public static int Damage(int weapon) { return Weapons.Get(weapon).Damage; }
        public static AnimalLoot Loot(AnimalState a)
        {
            return new AnimalLoot { id = a.id, meat = Meat[Index(a.species)], hide = Hide[Index(a.species)] };
        }

        static readonly int[] SomniaSpecies = { 19, 33, 38, 30, 31, 12 };
        static readonly int[] LushExoSpecies = { 33, 19, 40, 39, 32, 31, 11 };
        static readonly int[] CryoWorldSpecies = { 30, 31, 13, 12, 19 };
        static readonly int[] VolcanicWorldSpecies = { 35, 36, 37 };

        // Deep Frontier habitats (13..18). Species 42..47 are exclusive to these realms.
        // 42:PrismLynx 43:GeodeLeviathan 44:EmberWisp 45:StormRaptor 46:SporeStag 47:VoidWraith
        static readonly int[][] FrontierSpecies =
        {
            new[] { 42, 42, 31, 30, 19 },           // 13 Crystalia
            new[] { 35, 36, 34, 44, 44, 25 },       // 14 Emberfall
            new[] { 43, 43, 16, 18, 19, 21 },       // 15 Aquaria
            new[] { 45, 45, 26, 11, 27 },           // 16 Stormreach
            new[] { 46, 46, 32, 10, 31, 2 },        // 17 Verdant
            new[] { 47, 47, 29, 28, 19 }            // 18 Nihility
        };

        public static int Habitat(int surface, int x, int z, int seed)
        {
            if (surface == -16) return 7;  // Dimension.Inferno sentinel
            if (surface == -17) return 8;  // Dimension.Paradise sentinel
            if (surface == -12) return 9;  // Dimension.Somnia twilight sentinel
            if (surface == -18) return 10; // Lush exoplanets (Kepler452b, Trappist1e, AndromedaPrime)
            if (surface == -19) return 11; // Cryo moons & ice worlds (Titan, Oberon, Triton, ProximaB)
            if (surface == -20) return 12; // Volcanic worlds (JupiterIo, Venus)
            if (surface == -21) return 13; // Crystalia — prismic crystal fields
            if (surface == -22) return 14; // Emberfall — soot shelves & magma shore
            if (surface == -23) return 15; // Aquaria — reefs, kelp cathedrals, abyss
            if (surface == -24) return 16; // Stormreach — lattice plateaus & charge scars
            if (surface == -25) return 17; // Verdant — mycelium loam & cap canopies
            if (surface == -26) return 18; // Nihility — glass scarps & inverted fragments
            if (surface == 78 || surface == 79 || surface == 80 || surface == 82 || surface == 88 || surface == 89) return 6;
            if (surface == 33 || surface == 34 || surface == 7 || surface == 61) return 4;
            if (surface == 6 || surface == 43 || surface == 62) return 3;
            if (surface == 27 || surface == 32 || surface == 1 || surface == 64 || surface == 67) return 5;
            if (surface == 14 || surface == 63) return 2;
            return (int)(Hash((int)Math.Floor(x / 160.0), (int)Math.Floor(z / 160.0), seed ^ 81473) % 2);
        }

        public static int SpeciesForHabitat(uint hash, int habitat, int contentVersion = 7)
        {
            if (habitat == 7 && contentVersion >= 11)
                return 34 + (int)((hash >> 16) % 4u);
            if (habitat == 8 && contentVersion >= 11)
                return 38 + (int)((hash >> 16) % 4u);
            if (habitat == 9 && contentVersion >= 11)
                return SomniaSpecies[(int)((hash >> 16) % (uint)SomniaSpecies.Length)];
            if (habitat == 10 && contentVersion >= 11)
                return LushExoSpecies[(int)((hash >> 16) % (uint)LushExoSpecies.Length)];
            if (habitat == 11 && contentVersion >= 11)
                return CryoWorldSpecies[(int)((hash >> 16) % (uint)CryoWorldSpecies.Length)];
            if (habitat == 12 && contentVersion >= 11)
                return VolcanicWorldSpecies[(int)((hash >> 16) % (uint)VolcanicWorldSpecies.Length)];
            if (habitat >= 13 && habitat <= 18 && contentVersion >= 12)
                return FrontierSpecies[habitat - 13][(int)((hash >> 16) % (uint)FrontierSpecies[habitat - 13].Length)];
            if (habitat == 6 && contentVersion >= 9)
                return 20 + (int)((hash >> 16) % 10u);
            int[][] table = contentVersion >= 9 ? HabitatSpeciesV9
                : contentVersion >= 8 ? HabitatSpeciesV8 : HabitatSpeciesV7;
            int[] choices = table[Math.Max(0, Math.Min(5, habitat))];
            return choices[(int)((hash >> 16) % (uint)choices.Length)];
        }

        public WildlifeSave Save()
        {
            var savedSites = new List<string>(sites);
            savedSites.Sort(StringComparer.Ordinal);
            return new WildlifeSave { animals = Animals.ConvertAll(a => a.Copy()).ToArray(), sites = savedSites.ToArray() };
        }

        public void Spawn(AnimalObserver p, Ground ground)
        {
            if (!p.alive || !Finite(p.x) || !Finite(p.z)) return;
            bool abundantV8 = contentVersion >= 8;
            bool vibrantV11 = contentVersion >= 11;
            int cellSize = vibrantV11 ? 15 : abundantV8 ? 18 : 24;
            int radiusCells = vibrantV11 ? 5 : abundantV8 ? 3 : 2;
            int maxTotal = abundantV8 ? MaxAnimals : 256;
            int cx = (int)Math.Floor(p.x / cellSize), cz = (int)Math.Floor(p.z / cellSize);
            for (int dz = -radiusCells; dz <= radiusCells; dz++)
                for (int dx = -radiusCells; dx <= radiusCells; dx++)
                {
                    int sx = cx + dx, sz = cz + dz;
                    string key = sx + ":" + sz;
                    if (sites.Contains(key) || sites.Count >= MaxSites || Animals.Count >= maxTotal) continue;
                    uint h = Hash(sx, sz, seed ^ 2581);
                    float ax = sx * cellSize + 3 + h % (uint)(cellSize - 6) + .5f;
                    float az = sz * cellSize + 3 + (h >> 8) % (uint)(cellSize - 6) + .5f;
                    float distance = (ax - p.x) * (ax - p.x) + (az - p.z) * (az - p.z);
                    float minSq = vibrantV11 ? 64f : abundantV8 ? 100f : 144f;
                    float maxSq = vibrantV11 ? 7225f : abundantV8 ? 5625f : 3600f;
                    if (distance < minSq || distance > maxSq) continue;
                    sites.Add(key);
                    if (vibrantV11 ? (h % 9 == 0) : abundantV8 ? (h % 6 == 0) : (h % 2 != 0)) continue;
                    int species = (int)((h >> 16) % 3);
                    if (contentVersion >= 7)
                    {
                        int ix = (int)Math.Floor(ax), iz = (int)Math.Floor(az);
                        int surface = SurfaceAt == null ? 3 : SurfaceAt(ix, iz);
                        species = SpeciesForHabitat(h, Habitat(surface, ix, iz, seed), contentVersion);
                    }
                    int herdCount = vibrantV11 ? (IsFlyer(species) ? 2 + (int)(h % 2) : 3 + (int)(h % 3))
                                  : abundantV8 ? (IsFlyer(species) ? 2 : 2 + (int)(h % 2)) : 1;
                    for (int m = 0; m < herdCount && Animals.Count < maxTotal; m++)
                    {
                        float ox = m == 0 ? 0f : ((int)((h >> (m * 5)) % 7) - 3) * 1.45f;
                        float oz = m == 0 ? 0f : ((int)((h >> (m * 5 + 3)) % 7) - 3) * 1.45f;
                        float mx = ax + ox, mz = az + oz;
                        bool flyer = IsFlyer(species);
                        float ay = ground(mx, mz, -1, Radius(species), Height(species) + (flyer ? 6 : 0));
                        if (!Finite(ay)) continue;
                        float flight = species == 33 ? 6.4f : species == 40 ? 5.2f : species == 11 ? 4.75f : species == 36 || species == 41 ? 4.1f : species == 38 ? 3.2f : species == 12 || species == 25 ? 3.65f : species == 19 || species == 26 ? 4.25f : 0;
                        Animals.Add(new AnimalState
                        {
                            id = m == 0 ? ("a:" + key) : ("a:" + key + ":" + m),
                            species = species, health = MaxHealth(species),
                            x = mx, y = ay + flight, z = mz, homeX = mx, homeZ = mz,
                            goalX = mx, goalZ = mz, yaw = (h + (uint)(m * 97)) % 360,
                            timer = 1, flightHeight = flight, flightPhase = m * 1.7f
                        });
                    }

                    // Sky-Island wildlife & soaring SkyWhales / Golden Eagles (contentVersion >= 11)
                    int skyY = vibrantV11 && SkySurfaceAt != null ? SkySurfaceAt((int)Math.Floor(ax), (int)Math.Floor(az)) : -1;
                    if (vibrantV11 && skyY > 40 && Animals.Count < maxTotal - 3)
                    {
                        string skyKey = "sky:" + key;
                        if (!sites.Contains(skyKey) && sites.Count < MaxSites)
                        {
                            sites.Add(skyKey);
                            int[] skyChoices = { 33, 11, 19, 0, 1, 12 };
                            int skySpecies = skyChoices[(int)((h >> 11) % (uint)skyChoices.Length)];
                            bool skyFlyer = IsFlyer(skySpecies);
                            float say = ground(ax, az, skyY + 1f, Radius(skySpecies), Height(skySpecies) + (skyFlyer ? 6f : 0f));
                            if (Finite(say))
                            {
                                float sFlight = skySpecies == 33 ? 7.2f : skySpecies == 11 ? 5.0f : skySpecies == 19 ? 4.5f : skySpecies == 12 ? 3.8f : 0f;
                                Animals.Add(new AnimalState
                                {
                                    id = "a:" + skyKey,
                                    species = skySpecies, health = MaxHealth(skySpecies),
                                    x = ax, y = say + sFlight, z = az, homeX = ax, homeZ = az,
                                    goalX = ax, goalZ = az, yaw = (h * 53u) % 360,
                                    timer = 1, flightHeight = sFlight, flightPhase = 0.8f
                                });
                            }
                        }
                    }
                }
        }

        float Foot(AnimalState a, float x, float z, float previous, Ground ground)
        {
            bool flyer = IsFlyer(a.species);
            float offset = flyer ? a.flightHeight : 0;
            float y = ground(x, z, previous - offset, Radius(a.species), Height(a.species) + (flyer ? 6 : 0));
            if (float.IsNaN(y) && (a.species == 7 || a.species == 15))
                y = ground(x, z, previous + 1, Radius(a.species), Height(a.species));
            return float.IsNaN(y) ? y : y + offset;
        }

        public void Step(float dt, AnimalObserver[] observers, Ground ground, Action<string, float> damage)
        {
            if (observers == null || Animals.Count == 0) return;
            dt = Math.Max(0, Math.Min(.25f, dt));
            int active = 0, count = Animals.Count, start = contentVersion >= 7 ? cursor % count : 0;
            int visited = 0;
            for (; visited < count; visited++)
            {
                AnimalState a = Animals[(start + visited) % count];
                if (a.dead) continue;
                AnimalObserver nearest = default(AnimalObserver);
                float best = contentVersion >= 8 ? 5625f : 3600f;
                foreach (AnimalObserver p in observers)
                {
                    if (!p.alive) continue;
                    float d = (a.x - p.x) * (a.x - p.x) + (a.z - p.z) * (a.z - p.z);
                    if (d < best) { best = d; nearest = p; }
                }
                if (nearest.id == null) continue;
                if (active++ >= (contentVersion >= 8 ? ActiveLimitV8 : ActiveLimit)) break;
                if (IsFlyer(a.species))
                {
                    float old = a.flightHeight;
                    a.flightPhase = (a.flightPhase + dt * (a.species == 19 || a.species == 26 ? .55f : .8f)) % 6.2831853f;
                    float baseH = a.species == 12 || a.species == 25 ? 2.85f : a.species == 19 || a.species == 26 ? 3.15f : 4.75f;
                    a.flightHeight = baseH + 1.15f * (float)Math.Sin(a.flightPhase);
                    a.y += a.flightHeight - old;
                }
                if (Math.Abs(a.impulseX) + Math.Abs(a.impulseZ) > .01f)
                {
                    float ix = a.x + a.impulseX * dt, iz = a.z + a.impulseZ * dt;
                    float iy = Foot(a, ix, iz, a.y, ground);
                    if (!float.IsNaN(iy)) { a.x = ix; a.z = iz; a.y = iy; }
                    a.impulseX *= (float)Math.Exp(-dt * 8);
                    a.impulseZ *= (float)Math.Exp(-dt * 8);
                }
                float footing = Foot(a, a.x, a.z, a.y, ground);
                if (float.IsNaN(footing))
                {
                    a.fallSpeed = Math.Max(-24, a.fallSpeed - dt * 20);
                    float remaining = -a.fallSpeed * dt;
                    while (remaining > 0)
                    {
                        float stepDown = Math.Min(.2f, remaining);
                        a.y = Math.Max(0, a.y - stepDown);
                        remaining -= stepDown;
                        float landed = Foot(a, a.x, a.z, a.y, ground);
                        if (float.IsNaN(landed)) continue;
                        a.y = landed;
                        if (a.fallSpeed < -11)
                            a.health = Math.Max(0, a.health - (int)((-a.fallSpeed - 11) * 3));
                        a.fallSpeed = 0;
                        break;
                    }
                    if (a.y <= 0 || a.health <= 0) { a.dead = true; a.health = 0; }
                    continue;
                }
                a.fallSpeed = 0;
                a.y = footing;
                a.anger = Math.Max(0, a.anger - dt);
                a.bite = Math.Max(0, a.bite - dt);
                a.timer -= dt;
                int s = a.species;
                bool hostile = IsRadiaMutant(s) && (a.anger > 0 || best < 676f) ||
                               s == 6 && (a.anger > 0 || best < 196f) ||
                               (s == 2 || s == 8 || s == 17) && a.anger > 0 ||
                               (s == 3 || s == 15) && (a.anger > 0 || best < 81) ||
                               s == 4 && (a.anger > 0 || best < 36);
                bool defending = hostile && best < (IsRadiaMutant(s) ? 676f : 196f) && Math.Abs(a.y - nearest.y) < 10;
                bool flee = best < (s == 1 ? 100 : 64) && !defending &&
                            Math.Abs(a.y - nearest.y) < 8 && !IsFlyer(s) && s != 18;
                float speed = Walk[s];
                if (defending)
                {
                    a.goalX = nearest.x; a.goalZ = nearest.z;
                    speed = s == 6 ? 4.7f : s == 2 ? 3.1f : Run[s];
                    if (best < (s == 4 || s == 6 || s == 17 || s == 28 ? 5.2f : IsRadiaMutant(s) ? 3.8f : 2.4f) &&
                        Math.Abs(nearest.y - (a.y + 1.5f)) < 3.6f && a.bite <= 0)
                    {
                        if (damage != null) damage(nearest.id, AttackDamage[s]);
                        a.bite = IsRadiaMutant(s) ? 1.15f : 1.5f;
                    }
                }
                else if (flee)
                {
                    float inv = 1 / (float)Math.Sqrt(Math.Max(.01f, best));
                    a.goalX = a.x + (a.x - nearest.x) * inv * 10;
                    a.goalZ = a.z + (a.z - nearest.z) * inv * 10;
                    speed = Run[s];
                }
                else if (a.timer <= 0)
                {
                    uint h = Hash((int)(a.x * 7 + a.yaw), (int)(a.z * 7),
                        seed ^ a.health ^ unchecked(++a.wander * 1597334677));
                    double angle = h % 6283 * .001;
                    float range = 2 + (h >> 8) % 9;
                    if (IsFlyer(s)) range += 8;
                    a.goalX = a.homeX + (float)Math.Sin(angle) * range;
                    a.goalZ = a.homeZ + (float)Math.Cos(angle) * range;
                    a.timer = 3 + (h >> 16) % 5;
                }
                float vx = a.goalX - a.x, vz = a.goalZ - a.z;
                float len = (float)Math.Sqrt(vx * vx + vz * vz);
                if (len < .25f) continue;
                float step = Math.Min(len, speed * dt);
                float nx = a.x + vx / len * step, nz = a.z + vz / len * step;
                float ny = Foot(a, nx, nz, a.y, ground);
                if (float.IsNaN(ny))
                    for (int turn = 0; turn < 4; turn++)
                    {
                        double angle = (turn % 2 == 0 ? 1 : -1) * (turn < 2 ? .8 : 1.55);
                        float rx = (float)(vx * Math.Cos(angle) - vz * Math.Sin(angle));
                        float rz = (float)(vx * Math.Sin(angle) + vz * Math.Cos(angle));
                        nx = a.x + rx / len * step; nz = a.z + rz / len * step;
                        ny = Foot(a, nx, nz, a.y, ground);
                        if (!float.IsNaN(ny)) break;
                    }
                if (float.IsNaN(ny)) { a.timer = 0; a.goalX = a.x; a.goalZ = a.z; continue; }
                a.yaw = (float)(Math.Atan2(nx - a.x, nz - a.z) * 180 / Math.PI);
                a.x = nx; a.z = nz; a.y = ny;
            }
            if (contentVersion >= 7) cursor = (start + Math.Max(1, visited)) % count;
        }

        public AnimalLoot Hit(string id, int weapon, float x, float y, float z)
        {
            AnimalState a = Animals.Find(q => q.id == id && !q.dead);
            if (a == null) return null;
            float dy = a.y + Height(a.species) * .5f - y;
            float reach = Weapons.Get(weapon).Reach;
            if ((a.x - x) * (a.x - x) + dy * dy + (a.z - z) * (a.z - z) > reach * reach) return null;
            float push = (float)Math.Sqrt((a.x - x) * (a.x - x) + (a.z - z) * (a.z - z));
            if (push > .01f)
            {
                a.impulseX = (a.x - x) / push * 3.5f;
                a.impulseZ = (a.z - z) / push * 3.5f;
            }
            a.health = Math.Max(0, a.health - Damage(weapon));
            a.anger = 8; a.timer = 0;
            if (a.health > 0) return null;
            a.dead = true;
            return Loot(a);
        }
    }
}
