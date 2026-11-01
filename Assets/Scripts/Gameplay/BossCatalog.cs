using System;
using System.Collections.Generic;
using LestingCraft.World;

namespace LestingCraft.Gameplay
{
    /// <summary>Sculpt family used by the world boss rig.</summary>
    public enum BossArchetype
    {
        /// <summary>Broad-shouldered armoured giant (Mars, Venus, Radia).</summary>
        Colossus,
        /// <summary>Long-armed hunter with low centre of mass (Lestingia forests, Titan).</summary>
        Warden,
        /// <summary>Suspended sorcerer with a crown ring (Somnia, Dream, Andromeda).</summary>
        Archon,
        /// <summary>Digitigrade frost titan (Moon, Europa, Pluto).</summary>
        Titan,
        /// <summary>Compact burning core with orbiting shards (Mercury, Proxima B, Kepler-452b).</summary>
        Ember
    }

    /// <summary>Everything the boss rig and the loot table need for one dimension.</summary>
    public readonly struct BossProfile
    {
        public readonly Dimension Dimension;
        public readonly string Name;
        public readonly string Title;
        public readonly BossArchetype Archetype;
        public readonly int MaxHealth;
        public readonly float ArmorR, ArmorG, ArmorB;
        public readonly float TrimR, TrimG, TrimB;
        public readonly float CoreR, CoreG, CoreB;
        public readonly float Scale;
        public readonly int Diamonds, Crystals, Shards;
        public readonly int Aggression; // seconds between attacks, lower is faster

        public BossProfile(Dimension dimension, string name, string title, BossArchetype archetype, int maxHealth,
            float armorR, float armorG, float armorB, float trimR, float trimG, float trimB,
            float coreR, float coreG, float coreB, float scale, int diamonds, int crystals, int shards, int aggression)
        {
            Dimension = dimension; Name = name; Title = title; Archetype = archetype; MaxHealth = maxHealth;
            ArmorR = armorR; ArmorG = armorG; ArmorB = armorB;
            TrimR = trimR; TrimG = trimG; TrimB = trimB;
            CoreR = coreR; CoreG = coreG; CoreB = coreB;
            Scale = scale; Diamonds = diamonds; Crystals = crystals; Shards = shards; Aggression = aggression;
        }
    }

    /// <summary>
    /// One named guardian per dimension, including the ordinary world, so bosses are no longer
    /// limited to Mars/Venus/Mercury. Pure data: no Unity types, safe for headless checks.
    /// </summary>
    /// <summary>Rank-and-file mechanical mobs from the concept art: drones and robots.</summary>
    public enum DroneKind { Scout, Combat, Repair, Ground, Heavy, Guard, Builder }

    public readonly struct DroneProfile
    {
        public readonly DroneKind Kind;
        public readonly string Name;
        public readonly string Codename;
        public readonly int MaxHealth, Damage;
        public readonly float Speed, Size;
        public readonly float ArmorR, ArmorG, ArmorB;
        public readonly float GlowR, GlowG, GlowB;
        public readonly bool Fly;
        public DroneProfile(DroneKind kind, string name, string codename, int maxHealth, int damage,
            float speed, float size, float ar, float ag, float ab, float gr, float gg, float gb, bool fly)
        {
            Kind = kind; Name = name; Codename = codename; MaxHealth = maxHealth; Damage = damage;
            Speed = speed; Size = size; ArmorR = ar; ArmorG = ag; ArmorB = ab; GlowR = gr; GlowG = gg; GlowB = gb; Fly = fly;
        }
    }

    /// <summary>One drone/robot roster shared by worlds: pure data, safe for headless checks.</summary>
    public static class DroneRoster
    {
        static readonly DroneProfile[] Profiles =
        {
            new DroneProfile(DroneKind.Scout, "\u0420\u0430\u0437\u0432\u0435\u0434\u044b\u0432\u0430\u0442\u0435\u043b\u044c\u043d\u044b\u0439 \u0434\u0440\u043e\u043d", "Scout Drone", 30, 4, 4.5f, .70f, .35f, .38f, .42f, .35f, .85f, 1.00f, true),
            new DroneProfile(DroneKind.Combat, "\u0411\u043e\u0435\u0432\u043e\u0439 \u0434\u0440\u043e\u043d", "Combat Drone", 55, 10, 3.2f, .90f, .28f, .30f, .34f, 1.00f, .35f, .15f, true),
            new DroneProfile(DroneKind.Repair, "\u0420\u0435\u043c\u043e\u043d\u0442\u043d\u044b\u0439 \u0434\u0440\u043e\u043d", "Repair Drone", 40, 3, 2.6f, .80f, .55f, .55f, .48f, .40f, 1.00f, .50f, true),
            new DroneProfile(DroneKind.Ground, "\u041d\u0430\u0437\u0435\u043c\u043d\u044b\u0439 \u0440\u043e\u0431\u043e\u0442", "Ground Sentry", 60, 8, 2.2f, 1.00f, .42f, .40f, .36f, .95f, .75f, .25f, false),
            new DroneProfile(DroneKind.Heavy, "\u0422\u044f\u0436\u0451\u043b\u044b\u0439 \u0448\u0442\u0443\u0440\u043c\u043e\u0432\u043e\u0439", "Heavy Walker", 95, 14, 1.6f, 1.35f, .30f, .26f, .24f, 1.00f, .18f, .10f, false),
            new DroneProfile(DroneKind.Guard, "\u041e\u0445\u0440\u0430\u043d\u043d\u044b\u0439 \u0434\u0440\u043e\u043d", "Guard Drone", 70, 9, 2.8f, 1.00f, .38f, .36f, .42f, .60f, .40f, 1.00f, true),
            new DroneProfile(DroneKind.Builder, "\u0421\u0442\u0440\u043e\u0438\u0442\u0435\u043b\u044c\u043d\u044b\u0439 \u0434\u0440\u043e\u043d", "Builder Drone", 45, 5, 2.0f, .90f, .52f, .46f, .34f, 1.00f, .62f, .18f, true)
        };

        public static DroneProfile[] All() { return (DroneProfile[])Profiles.Clone(); }

        public static DroneProfile For(DroneKind kind)
        {
            for (int i = 0; i < Profiles.Length; i++) if (Profiles[i].Kind == kind) return Profiles[i];
            return Profiles[0];
        }

        public static DroneProfile[] SwarmFor(Dimension dimension)
        {
            switch (dimension)
            {
                case Dimension.Lestingia: return new[] { For(DroneKind.Scout), For(DroneKind.Builder) };
                case Dimension.Mercury: return new[] { For(DroneKind.Combat), For(DroneKind.Scout) };
                case Dimension.Mars: return new[] { For(DroneKind.Ground), For(DroneKind.Heavy) };
                case Dimension.Venus: return new[] { For(DroneKind.Combat), For(DroneKind.Ground) };
                case Dimension.Moon: return new[] { For(DroneKind.Scout), For(DroneKind.Repair) };
                case Dimension.Europa: return new[] { For(DroneKind.Repair), For(DroneKind.Ground) };
                case Dimension.Titan: return new[] { For(DroneKind.Scout), For(DroneKind.Combat) };
                case Dimension.Pluto: return new[] { For(DroneKind.Ground), For(DroneKind.Repair) };
                case Dimension.ProximaB: return new[] { For(DroneKind.Heavy), For(DroneKind.Combat) };
                case Dimension.Trappist1e: return new[] { For(DroneKind.Scout), For(DroneKind.Guard) };
                case Dimension.Kepler452b: return new[] { For(DroneKind.Builder), For(DroneKind.Scout) };
                case Dimension.AndromedaPrime: return new[] { For(DroneKind.Guard), For(DroneKind.Combat) };
                case Dimension.Somnia: return new[] { For(DroneKind.Scout), For(DroneKind.Guard) };
                case Dimension.Dream: return new[] { For(DroneKind.Combat), For(DroneKind.Guard) };
                case Dimension.Radia: return new[] { For(DroneKind.Heavy), For(DroneKind.Ground) };
                default: return new[] { For(DroneKind.Guard), For(DroneKind.Scout) };
            }
        }
    }

    public static class BossCatalog
    {
        static readonly BossProfile[] Profiles =
        {
            new BossProfile(Dimension.Lestingia, "Ancient Guardian", "Древний Страж", BossArchetype.Warden,
                320, .20f, .32f, .15f, .45f, .35f, .16f, .55f, .92f, .38f, 1.00f, 6, 8, 12, 4),
            new BossProfile(Dimension.Moon, "Лунный Титанит", "Пылевой страж", BossArchetype.Titan,
                300, .44f, .44f, .50f, .70f, .72f, .78f, .62f, .82f, 1.00f, 1.08f, 5, 6, 10, 4),
            new BossProfile(Dimension.Mars, "Red Colossus", "Алый Колосс", BossArchetype.Colossus,
                260, .55f, .10f, .08f, .25f, .06f, .05f, 1.00f, .35f, .10f, 1.00f, 4, 6, 8, 5),
            new BossProfile(Dimension.Venus, "The Storm Herald", "\u0413\u043b\u0430\u0437 \u0411\u0443\u0440\u0438", BossArchetype.Colossus,
                360, .28f, .30f, .38f, .55f, .60f, .72f, 1.00f, .85f, .35f, 1.06f, 4, 8, 9, 4),
            new BossProfile(Dimension.Mercury, "Solar Core Guardian", "Страж Солнечного Ядра", BossArchetype.Ember,
                380, .18f, .12f, .10f, .98f, .55f, .15f, 1.00f, .42f, .08f, 1.02f, 5, 9, 8, 4),
            new BossProfile(Dimension.Europa, "Ледяной Панцирь Европы", "Треснувший щит", BossArchetype.Titan,
                340, .56f, .72f, .86f, .86f, .94f, 1.00f, .44f, .86f, 1.00f, 1.05f, 5, 8, 10, 4),
            new BossProfile(Dimension.Titan, "Ringed Leviathan", "Кольчатый Левиафан", BossArchetype.Archon,
                360, .35f, .20f, .55f, .85f, .65f, .25f, .75f, .45f, 1.00f, 1.10f, 6, 7, 11, 5),
            new BossProfile(Dimension.Pluto, "Плутоновый Отшельник", "Последний рубеж", BossArchetype.Titan,
                330, .48f, .50f, .58f, .70f, .74f, .86f, .72f, .88f, 1.00f, 1.04f, 5, 7, 10, 5),
            new BossProfile(Dimension.ProximaB, "Проксима-Пожиратель", "Приливный разум", BossArchetype.Ember,
                395, .26f, .13f, .12f, .92f, .32f, .18f, 1.00f, .46f, .16f, 1.12f, 7, 9, 9, 3),
            new BossProfile(Dimension.Trappist1e, "Траппист-Смотритель", "Резонансный хор", BossArchetype.Archon,
                370, .24f, .26f, .36f, .62f, .48f, .92f, .78f, .62f, 1.00f, 1.06f, 6, 9, 8, 4),
            new BossProfile(Dimension.Kepler452b, "Кеплер-Садовник", "Изумрудный купол", BossArchetype.Warden,
                355, .22f, .38f, .22f, .38f, .82f, .46f, .52f, 1.00f, .62f, 1.08f, 6, 10, 10, 4),
            new BossProfile(Dimension.AndromedaPrime, "Элизиумский Владыка", "Корона Андромеды", BossArchetype.Archon,
                420, .30f, .22f, .46f, .76f, .52f, 1.00f, .82f, .52f, 1.00f, 1.14f, 8, 12, 10, 3),
            new BossProfile(Dimension.Somnia, "He Who Heard", "\u0425\u043e\u0437\u044f\u0438\u043d \u0421\u043d\u0430", BossArchetype.Archon,
                420, .18f, .18f, .30f, .52f, .34f, .84f, .72f, .62f, 1.00f, 1.10f, 7, 11, 9, 3),
            new BossProfile(Dimension.Dream, "The First Node", "\u0421\u0435\u0440\u0434\u0446\u0435 \u041c\u0430\u0448\u0438\u043d", BossArchetype.Archon,
                410, .22f, .24f, .30f, .45f, .75f, .95f, .85f, .92f, 1.00f, 1.06f, 7, 12, 8, 3),
            new BossProfile(Dimension.Radia, "Тринититовый Колосс", "Пепел Радиа", BossArchetype.Colossus,
                440, .30f, .32f, .24f, .78f, .82f, .30f, .62f, 1.00f, .38f, 1.16f, 8, 10, 14, 3),
            new BossProfile(Dimension.Backrooms, "The Black Bacteria", "\u0421\u0443\u0449\u043d\u043e\u0441\u0442\u044c 23", BossArchetype.Warden,
                420, .12f, .12f, .14f, .50f, .50f, .55f, .75f, .80f, .95f, 1.02f, 6, 9, 13, 5)
        };

        /// <summary>Earth carries three canon bosses (bible): extras spawn alongside the guardian.</summary>
        public static readonly BossProfile[] ExtraProfiles =
        {
            new BossProfile(Dimension.Lestingia, "The Corrupted AI", "\u0418\u0441\u043a\u0430\u0436\u0451\u043d\u043d\u044b\u0439 \u0418\u0418", BossArchetype.Archon,
                410, .16f, .10f, .28f, .75f, .25f, .95f, .85f, .45f, 1.00f, 1.06f, 7, 12, 8, 3),
            new BossProfile(Dimension.Lestingia, "The Void Horror", "\u041a\u043e\u0448\u043c\u0430\u0440 \u041f\u0443\u0441\u0442\u043e\u0442\u044b", BossArchetype.Archon,
                400, .14f, .11f, .17f, .65f, .60f, .55f, 1.00f, .15f, .10f, 1.10f, 7, 11, 9, 3)
        };

        public static BossProfile[] ExtrasFor(Dimension dimension)
        {
            int n = 0;
            for (int i = 0; i < ExtraProfiles.Length; i++) if (ExtraProfiles[i].Dimension == dimension) n++;
            var list = new BossProfile[n];
            int k = 0;
            for (int i = 0; i < ExtraProfiles.Length; i++) if (ExtraProfiles[i].Dimension == dimension) list[k++] = ExtraProfiles[i];
            return list;
        }

        static readonly Dictionary<Dimension, BossProfile> Lookup = BuildLookup();

        static Dictionary<Dimension, BossProfile> BuildLookup()
        {
            var map = new Dictionary<Dimension, BossProfile>(Profiles.Length);
            for (int i = 0; i < Profiles.Length; i++) map[Profiles[i].Dimension] = Profiles[i];
            return map;
        }

        public static BossProfile[] All() { return (BossProfile[])Profiles.Clone(); }

        public static bool HasBoss(Dimension dimension) { return Lookup.ContainsKey(dimension); }

        public static BossProfile For(Dimension dimension)
        {
            return Lookup.TryGetValue(dimension, out var profile) ? profile : Profiles[0];
        }

        /// <summary>Boss health scales with the world it guards, never below a readable minimum.</summary>
        public static int HealthFor(Dimension dimension, int worldSeed)
        {
            var profile = For(dimension);
            int jitter = (int)((uint)worldSeed % 41u) - 20;
            return Math.Max(180, profile.MaxHealth + jitter);
        }

        /// <summary>Deterministic loot: same boss and seed give the same haul.</summary>
        public static void LootFor(Dimension dimension, int worldSeed, out int diamonds, out int crystals, out int shards)
        {
            var profile = For(dimension);
            uint h = (uint)(worldSeed * 2654435761u) ^ (uint)((int)dimension * 40503u);
            diamonds = profile.Diamonds + (int)(h % 3);
            crystals = profile.Crystals + (int)((h >> 5) % 3);
            shards = profile.Shards + (int)((h >> 11) % 4);
        }

        /// <summary>True once the guardian drops to a third of its health or below.</summary>
        public static bool IsEnraged(int health, int maxHealth)
        {
            return maxHealth > 0 && health * 100L <= maxHealth * 34L;
        }
    }
}
