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
        public const int MaxAnimals = 256, MaxSites = 4096, ActiveLimit = 32;
        public readonly List<AnimalState> Animals = new List<AnimalState>();
        readonly HashSet<string> sites = new HashSet<string>();
        readonly int seed, contentVersion;
        int cursor;

        // The surface callback is deliberately engine-independent. It returns the
        // natural surface block at a spawn location, before clearance is tested.
        public Func<int, int, int> SurfaceAt;

        public delegate float Ground(float x, float z, float previousY, float radius, float height);

        static readonly int[] Health = { 30, 8, 40, 34, 85, 14, 95, 26, 30, 18, 10, 20 };
        static readonly int[] Meat = { 3, 1, 4, 2, 6, 1, 7, 3, 2, 2, 1, 2 };
        static readonly int[] Hide = { 2, 1, 2, 2, 4, 2, 4, 2, 3, 2, 1, 1 };
        static readonly int[] AttackDamage = { 0, 0, 8, 6, 12, 0, 10, 0, 7, 0, 0, 0 };
        static readonly float[] Heights = { 2.2f, .8f, .95f, 1.25f, 1.95f, .9f, 2.05f, 1.65f, 1.05f, .7f, .55f, .9f };
        static readonly float[] Radii = { .43f, .21f, .45f, .38f, .72f, .27f, .76f, .32f, .34f, .35f, .38f, .36f };
        static readonly float[] Lengths = { 1.6f, .65f, 1.6f, 1.9f, 2.3f, 1.5f, 2.6f, 1.35f, 1.35f, 1.5f, .95f, 1.15f };
        static readonly float[] Walk = { .65f, .75f, .65f, 1.15f, .65f, 1.25f, .55f, .9f, 1.05f, .55f, .5f, 2.8f };
        static readonly float[] Run = { 3.8f, 4.2f, 3.8f, 4.6f, 3.3f, 4.8f, 3.4f, 3.6f, 4.4f, 2.1f, 2.7f, 4.5f };
        static readonly int[][] HabitatSpecies =
        {
            new[] { 0, 0, 1, 1, 5, 6, 6, 11 },
            new[] { 0, 0, 1, 2, 2, 3, 4, 5, 8, 11 },
            new[] { 0, 1, 3, 3, 4, 4, 6, 8, 11 },
            new[] { 1, 1, 5, 5, 7, 11 },
            new[] { 2, 2, 9, 9, 9, 10, 10, 10, 11 },
            new[] { 1, 3, 7, 7, 7, 8, 11, 11 }
        };

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
                a.flightHeight = a.species == 11 ? ClampFlight(a.flightHeight) : 0;
                Animals.Add(a);
            }
        }

        bool ValidSpecies(int s) { return s >= 0 && s < (contentVersion >= 7 ? 12 : 3); }
        static bool Finite(float v) { return !float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v) < 32000; }
        static int Index(int s) { return s >= 0 && s < 12 ? s : 2; }
        static float ClampFlight(float h) { return Finite(h) && h >= 3.5f && h <= 6 ? h : 4.75f; }

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

        // Habitat is resolved from the actual biome-authored substrate, rather than
        // a second, drifting copy of the world's biome algorithm. Grass supports
        // both meadow and forest fauna. The deterministic regional mix selects
        // between those two compatible communities.
        public static int Habitat(int surface, int x, int z, int seed)
        {
            if (surface == 33 || surface == 34 || surface == 7) return 4;
            if (surface == 6 || surface == 43) return 3;
            if (surface == 27 || surface == 32 || surface == 1) return 5;
            if (surface == 14) return 2;
            return (int)(Hash((int)Math.Floor(x / 160.0), (int)Math.Floor(z / 160.0), seed ^ 81473) % 2);
        }

        public static int SpeciesForHabitat(uint hash, int habitat)
        {
            int[] choices = HabitatSpecies[Math.Max(0, Math.Min(5, habitat))];
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
            int cx = (int)Math.Floor(p.x / 24), cz = (int)Math.Floor(p.z / 24);
            for (int dz = -2; dz <= 2; dz++)
                for (int dx = -2; dx <= 2; dx++)
                {
                    int sx = cx + dx, sz = cz + dz;
                    string key = sx + ":" + sz;
                    if (sites.Contains(key) || sites.Count >= MaxSites || Animals.Count >= MaxAnimals) continue;
                    uint h = Hash(sx, sz, seed ^ 2581);
                    float ax = sx * 24 + 4 + h % 16 + .5f, az = sz * 24 + 4 + (h >> 8) % 16 + .5f;
                    float distance = (ax - p.x) * (ax - p.x) + (az - p.z) * (az - p.z);
                    if (distance < 144 || distance > 3600) continue;
                    sites.Add(key);
                    if (h % 2 != 0) continue;
                    int species = (int)((h >> 16) % 3);
                    if (contentVersion >= 7)
                    {
                        int ix = (int)Math.Floor(ax), iz = (int)Math.Floor(az);
                        int surface = SurfaceAt == null ? 3 : SurfaceAt(ix, iz);
                        species = SpeciesForHabitat(h, Habitat(surface, ix, iz, seed));
                    }
                    float ay = ground(ax, az, -1, Radius(species), Height(species) + (species == 11 ? 6 : 0));
                    if (!Finite(ay)) continue;
                    float flight = species == 11 ? 4.75f : 0;
                    Animals.Add(new AnimalState
                    {
                        id = "a:" + key, species = species, health = MaxHealth(species),
                        x = ax, y = ay + flight, z = az, homeX = ax, homeZ = az,
                        goalX = ax, goalZ = az, yaw = h % 360, timer = 1, flightHeight = flight
                    });
                }
        }

        float Foot(AnimalState a, float x, float z, float previous, Ground ground)
        {
            float offset = a.species == 11 ? a.flightHeight : 0;
            float y = ground(x, z, previous - offset, Radius(a.species), Height(a.species) + (a.species == 11 ? 6 : 0));
            if (float.IsNaN(y) && a.species == 7)
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
                float best = 3600;
                foreach (AnimalObserver p in observers)
                {
                    if (!p.alive) continue;
                    float d = (a.x - p.x) * (a.x - p.x) + (a.z - p.z) * (a.z - p.z);
                    if (d < best) { best = d; nearest = p; }
                }
                if (nearest.id == null) continue;
                if (active++ >= ActiveLimit) break;
                if (a.species == 11)
                {
                    float old = a.flightHeight;
                    a.flightPhase = (a.flightPhase + dt * .8f) % 6.2831853f;
                    a.flightHeight = 4.75f + 1.25f * (float)Math.Sin(a.flightPhase);
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
                bool hostile = (s == 2 || s == 6 || s == 8) && a.anger > 0 ||
                               s == 3 && (a.anger > 0 || best < 81) ||
                               s == 4 && (a.anger > 0 || best < 36);
                bool defending = hostile && best < 196 && Math.Abs(a.y - nearest.y) < 8;
                bool flee = best < (s == 1 ? 100 : 64) && !defending &&
                            Math.Abs(a.y - nearest.y) < 8 && s != 11;
                float speed = Walk[s];
                if (defending)
                {
                    a.goalX = nearest.x; a.goalZ = nearest.z;
                    speed = s == 2 ? 3.1f : Run[s];
                    if (best < (s == 4 || s == 6 ? 4 : 2.4f) &&
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
                    if (s == 11) range += 8;
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
