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
        public const int SpeciesCount = 20;
        public const int MaxAnimals = 384, MaxSites = 8192, ActiveLimit = 32, ActiveLimitV8 = 96;
        public readonly List<AnimalState> Animals = new List<AnimalState>();
        readonly HashSet<string> sites = new HashSet<string>();
        readonly int seed, contentVersion;
        int cursor;

        // The surface callback is deliberately engine-independent. It returns the
        // natural surface block at a spawn location, before clearance is tested.
        public Func<int, int, int> SurfaceAt;

        public delegate float Ground(float x, float z, float previousY, float radius, float height);

        // 0..11 original species + 12:Owl, 13:Penguin, 14:Camel, 15:Scorpion, 16:Turtle, 17:Moose, 18:Capybara, 19:VoidRay
        static readonly int[] Health = { 30, 8, 40, 34, 85, 14, 95, 26, 30, 18, 10, 20, 16, 18, 68, 22, 32, 110, 28, 55 };
        static readonly int[] Meat = { 3, 1, 4, 2, 6, 1, 7, 3, 2, 2, 1, 2, 1, 2, 5, 1, 2, 8, 3, 2 };
        static readonly int[] Hide = { 2, 1, 2, 2, 4, 2, 4, 2, 3, 2, 1, 1, 2, 2, 3, 2, 3, 5, 2, 3 };
        static readonly int[] AttackDamage = { 0, 0, 8, 6, 12, 0, 10, 0, 7, 0, 0, 0, 0, 0, 0, 9, 0, 11, 0, 0 };
        static readonly float[] Heights = { 2.2f, .8f, .95f, 1.25f, 1.95f, .9f, 2.05f, 1.65f, 1.05f, .7f, .55f, .9f, .75f, .95f, 2.35f, .55f, .62f, 2.45f, .82f, .85f };
        static readonly float[] Radii = { .43f, .21f, .45f, .38f, .72f, .27f, .76f, .32f, .34f, .35f, .38f, .36f, .30f, .28f, .58f, .36f, .40f, .74f, .36f, .55f };
        static readonly float[] Lengths = { 1.6f, .65f, 1.6f, 1.9f, 2.3f, 1.5f, 2.6f, 1.35f, 1.35f, 1.5f, .95f, 1.15f, .62f, .58f, 2.25f, 1.15f, .95f, 2.55f, 1.15f, 1.65f };
        static readonly float[] Walk = { .65f, .75f, .65f, 1.15f, .65f, 1.25f, .55f, .9f, 1.05f, .55f, .5f, 2.8f, 2.4f, .62f, .72f, .95f, .35f, .60f, .58f, 2.6f };
        static readonly float[] Run = { 3.8f, 4.2f, 3.8f, 4.6f, 3.3f, 4.8f, 3.4f, 3.6f, 4.4f, 2.1f, 2.7f, 4.5f, 4.1f, 2.4f, 3.5f, 3.6f, 1.2f, 3.9f, 2.8f, 4.6f };

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
            new[] { 1, 3, 7, 8, 11, 12, 15, 17, 19 }          // 5: Rocky, Volcanic & Andromeda
        };

        public static bool IsFlyer(int s) { return s == 11 || s == 12 || s == 19; }

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

        bool ValidSpecies(int s) { return s >= 0 && s < (contentVersion >= 8 ? SpeciesCount : contentVersion >= 7 ? 12 : 3); }
        static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) < 32000; }
        static int Index(int s) { return s >= 0 && s < SpeciesCount ? s : 2; }
        static float ClampFlight(float h) { return Finite(h) && h >= 2.8f && h <= 6.5f ? h : 4.5f; }

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

        public static int Habitat(int surface, int x, int z, int seed)
        {
            if (surface == 33 || surface == 34 || surface == 7 || surface == 61) return 4;
            if (surface == 6 || surface == 43 || surface == 62) return 3;
            if (surface == 27 || surface == 32 || surface == 1 || surface == 64 || surface == 67) return 5;
            if (surface == 14 || surface == 63) return 2;
            return (int)(Hash((int)Math.Floor(x / 160.0), (int)Math.Floor(z / 160.0), seed ^ 81473) % 2);
        }

        public static int SpeciesForHabitat(uint hash, int habitat, int contentVersion = 7)
        {
            int[][] table = contentVersion >= 8 ? HabitatSpeciesV8 : HabitatSpeciesV7;
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
            int cellSize = abundantV8 ? 18 : 24;
            int radiusCells = abundantV8 ? 3 : 2;
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
                    float minSq = abundantV8 ? 100f : 144f;
                    float maxSq = abundantV8 ? 5625f : 3600f;
                    if (distance < minSq || distance > maxSq) continue;
                    sites.Add(key);
                    if (abundantV8 ? (h % 6 == 0) : (h % 2 != 0)) continue;
                    int species = (int)((h >> 16) % 3);
                    if (contentVersion >= 7)
                    {
                        int ix = (int)Math.Floor(ax), iz = (int)Math.Floor(az);
                        int surface = SurfaceAt == null ? 3 : SurfaceAt(ix, iz);
                        species = SpeciesForHabitat(h, Habitat(surface, ix, iz, seed), contentVersion);
                    }
                    int herdCount = abundantV8 ? (IsFlyer(species) ? 2 : 2 + (int)(h % 2)) : 1;
                    for (int m = 0; m < herdCount && Animals.Count < maxTotal; m++)
                    {
                        float ox = m == 0 ? 0f : ((int)((h >> (m * 5)) % 7) - 3) * 1.45f;
                        float oz = m == 0 ? 0f : ((int)((h >> (m * 5 + 3)) % 7) - 3) * 1.45f;
                        float mx = ax + ox, mz = az + oz;
                        bool flyer = IsFlyer(species);
                        float ay = ground(mx, mz, -1, Radius(species), Height(species) + (flyer ? 6 : 0));
                        if (!Finite(ay)) continue;
                        float flight = species == 11 ? 4.75f : species == 12 ? 3.65f : species == 19 ? 4.25f : 0;
                        Animals.Add(new AnimalState
                        {
                            id = m == 0 ? ("a:" + key) : ("a:" + key + ":" + m),
                            species = species, health = MaxHealth(species),
                            x = mx, y = ay + flight, z = mz, homeX = mx, homeZ = mz,
                            goalX = mx, goalZ = mz, yaw = (h + (uint)(m * 97)) % 360,
                            timer = 1, flightHeight = flight, flightPhase = m * 1.7f
                        });
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
                    a.flightPhase = (a.flightPhase + dt * (a.species == 19 ? .55f : .8f)) % 6.2831853f;
                    float baseH = a.species == 12 ? 3.65f : a.species == 19 ? 4.25f : 4.75f;
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
                bool hostile = (s == 2 || s == 6 || s == 8 || s == 17) && a.anger > 0 ||
                               (s == 3 || s == 15) && (a.anger > 0 || best < 81) ||
                               s == 4 && (a.anger > 0 || best < 36);
                bool defending = hostile && best < 196 && Math.Abs(a.y - nearest.y) < 8;
                bool flee = best < (s == 1 ? 100 : 64) && !defending &&
                            Math.Abs(a.y - nearest.y) < 8 && !IsFlyer(s) && s != 18;
                float speed = Walk[s];
                if (defending)
                {
                    a.goalX = nearest.x; a.goalZ = nearest.z;
                    speed = s == 2 ? 3.1f : Run[s];
                    if (best < (s == 4 || s == 6 || s == 17 ? 4 : 2.4f) &&
                        Math.Abs(nearest.y - (a.y + 1.5f)) < 2.3f && a.bite <= 0)
                    {
                        if (damage != null) damage(nearest.id, AttackDamage[s]);
                        a.bite = 1.5f;
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
