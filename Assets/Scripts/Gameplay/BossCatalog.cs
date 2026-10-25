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
    public static class BossCatalog
    {
        static readonly BossProfile[] Profiles =
        {
            new BossProfile(Dimension.Lestingia, "Хранитель Первого Леса", "Древень", BossArchetype.Warden,
                320, .20f, .30f, .14f, .46f, .36f, .16f, .55f, .92f, .38f, 1.00f, 6, 8, 12, 4),
            new BossProfile(Dimension.Moon, "Лунный Титанит", "Пылевой страж", BossArchetype.Titan,
                300, .44f, .44f, .50f, .70f, .72f, .78f, .62f, .82f, 1.00f, 1.08f, 5, 6, 10, 4),
            new BossProfile(Dimension.Mars, "Железный Колосс Марса", "Кузнец бурь", BossArchetype.Colossus,
                260, .42f, .16f, .12f, .68f, .34f, .18f, 1.00f, .38f, .12f, 1.00f, 4, 6, 8, 5),
            new BossProfile(Dimension.Venus, "Серный Левиафан Венеры", "Дыхание кислоты", BossArchetype.Colossus,
                320, .32f, .26f, .11f, .85f, .68f, .18f, .95f, .85f, .22f, 1.06f, 4, 8, 9, 4),
            new BossProfile(Dimension.Mercury, "Солнечный Архонт Меркурия", "Вестник вспышек", BossArchetype.Ember,
                380, .18f, .19f, .24f, .92f, .72f, .32f, .35f, .88f, 1.00f, 1.02f, 5, 9, 8, 4),
            new BossProfile(Dimension.Europa, "Ледяной Панцирь Европы", "Треснувший щит", BossArchetype.Titan,
                340, .56f, .72f, .86f, .86f, .94f, 1.00f, .44f, .86f, 1.00f, 1.05f, 5, 8, 10, 4),
            new BossProfile(Dimension.Titan, "Метанный Страж Титана", "Тихий исполин", BossArchetype.Warden,
                360, .40f, .46f, .30f, .78f, .66f, .24f, 1.00f, .74f, .28f, 1.10f, 6, 7, 11, 5),
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
            new BossProfile(Dimension.Somnia, "Сомния-Сновидец", "Разлом сна", BossArchetype.Archon,
                400, .18f, .18f, .30f, .52f, .34f, .84f, .72f, .62f, 1.00f, 1.10f, 7, 11, 9, 3),
            new BossProfile(Dimension.Dream, "Фантом Спирали", "Тень за спиной", BossArchetype.Archon,
                410, .12f, .10f, .18f, .42f, .26f, .72f, .62f, .46f, 1.00f, 1.06f, 7, 12, 8, 3),
            new BossProfile(Dimension.Radia, "Тринититовый Колосс", "Пепел Радиа", BossArchetype.Colossus,
                440, .30f, .32f, .24f, .78f, .82f, .30f, .62f, 1.00f, .38f, 1.16f, 8, 10, 14, 3)
        };

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
