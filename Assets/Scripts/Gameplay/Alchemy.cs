using System;
using System.Collections.Generic;
namespace LestingCraft.Gameplay
{
    /// <summary>
    /// Alchemy system: combine reagents (and an optional catalyst) into yields.
    /// A catalyst is not consumed by most reactions — it multiplies the yield, so it is a
    /// multiplier rather than an ingredient. Every reaction is exact: all reagent counts must
    /// be available in the inventory. Crafting never deletes items on a failed match.
    ///
    /// Reagents are drawn from existing resources (fibre, charcoal, minerals, fuels, flora) and the
    /// yields feed back into the game (gunpowder, explosives, medicine, refined materials, lights).
    /// </summary>
    public static class Alchemy
    {
        /// <summary>A reagent (item id + required count) feeding one reaction.</summary>
        public struct Reagent { public int Item, Count; public Reagent(int item, int count) { Item = item; Count = count; } }

        /// <summary>
        /// One alchemical reaction. Catalyst 0 = no catalyst allowed/used. Yield is produced per
        /// craft when every reagent is present; a present catalyst multiplies the count (rounded up).
        /// </summary>
        public sealed class Reaction
        {
            public readonly string Name;
            public readonly Reagent[] Reagents;
            public readonly int Catalyst;   // 0 = none
            public readonly int Output;
            public readonly int BaseCount;
            public readonly int BoostedCount;
            public Reaction(string name, Reagent[] reagents, int catalyst, int output, int baseCount, int boostedCount)
            {
                Name = name; Reagents = reagents; Catalyst = catalyst; Output = output;
                BaseCount = baseCount; BoostedCount = boostedCount;
            }
        }

        // Catalyst ids (see Items.cs). They are never consumed by these reactions.
        public const int CatalystAlchemicSalt = 200;   // gentle yield boost
        public const int CatalystMercury = 201;       // strong yield boost
        public const int CatalystCinderAsh = 202;     // unlocks combustion reactions

        public static readonly Reaction[] All =
        {
            // --- Base reagent reactions (no catalyst required) ---
            new Reaction("Серная кислота", new[]{ new Reagent(146,3), new Reagent(123,1) }, 0, 196, 2, 3),
            new Reaction("Нитратная соль", new[]{ new Reagent(146,1), new Reagent(145,2), new Reagent(113,1) }, 0, 197, 2, 3),
            new Reaction("Очищенный кварц", new[]{ new Reagent(145,2), new Reagent(122,1) }, 0, 205, 1, 2),
            new Reaction("Люминесцентная паста", new[]{ new Reagent(145,1), new Reagent(146,1), new Reagent(123,2) }, 0, 207, 1, 2),

            // --- Catalyst-boosted reactions (catalyst multiplies yield, not consumed) ---
            new Reaction("Порох", new[]{ new Reagent(197,2), new Reagent(123,2), new Reagent(146,1) },
                         CatalystAlchemicSalt, 198, 3, 5),
            new Reaction("Лечебная склянка", new[]{ new Reagent(110,3), new Reagent(113,1) },
                         CatalystAlchemicSalt, 199, 1, 2),
            new Reaction("Стимулятор", new[]{ new Reagent(113,2), new Reagent(122,2) },
                         CatalystMercury, 204, 1, 2),
            new Reaction("Философская меркурия", new[]{ new Reagent(196,1), new Reagent(145,2) },
                         0, 201, 1, 1),
            new Reaction("Алхимическая соль", new[]{ new Reagent(146,1), new Reagent(145,1), new Reagent(123,1) },
                         0, 200, 1, 1),
            new Reaction("Циндарный пепел", new[]{ new Reagent(147,2), new Reagent(123,1) },
                         0, 202, 1, 1),

            // --- Catalyst-gated combustion/explosive reactions (need Cinder Ash) ---
            new Reaction("Взрывная смазка", new[]{ new Reagent(198,1), new Reagent(176,1), new Reagent(146,1) },
                         CatalystCinderAsh, 206, 1, 2)
        };

        /// <summary>Total count of an item id across a slot array (0 if absent/null).</summary>
        static int CountIn(ItemStack[] slots, int item)
        {
            int n = 0;
            foreach (var s in slots) if (s != null && s.item == item) n += s.count;
            return n;
        }

        /// <summary>
        /// Find the first reaction whose reagents are all present in the inventory, honouring the
        /// catalyst rule: a reaction that names a catalyst only matches when that catalyst is present.
        /// Returns the reaction and the yield count (boosted if the catalyst is present), or null.
        /// </summary>
        public static bool TryMatch(Inventory inv, out Reaction reaction, out int count)
        {
            reaction = null; count = 0;
            if (inv == null) return false;
            foreach (var r in All)
            {
                bool ok = true;
                foreach (var rg in r.Reagents)
                    if (inv.Count(rg.Item) < rg.Count) { ok = false; break; }
                if (!ok) continue;
                if (r.Catalyst != 0 && inv.Count(r.Catalyst) < 1) continue;
                reaction = r;
                count = r.Catalyst != 0 ? r.BoostedCount : r.BaseCount;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Craft the first matching reaction from the whole inventory. Removes reagents (and the
        /// catalyst if it is consumable for that reaction — none here are), then adds the yield.
        /// Returns the number crafted, or 0 when nothing matches.
        /// </summary>
        public static int Craft(Inventory inv)
        {
            if (!TryMatch(inv, out var reaction, out var count)) return 0;
            foreach (var rg in reaction.Reagents) inv.Remove(rg.Item, rg.Count);
            // Catalysts in this set are reusable multipliers and are NOT consumed.
            inv.Add(reaction.Output, count);
            return count;
        }

        /// <summary>Try every reaction once in order; returns total crafts performed.</summary>
        public static int CraftAll(Inventory inv)
        {
            int total = 0;
            bool progressed = true;
            while (progressed)
            {
                progressed = false;
                foreach (var r in All)
                {
                    bool ok = true;
                    foreach (var rg in r.Reagents) if (inv.Count(rg.Item) < rg.Count) { ok = false; break; }
                    if (!ok) continue;
                    if (r.Catalyst != 0 && inv.Count(r.Catalyst) < 1) continue;
                    int n = r.Catalyst != 0 ? r.BoostedCount : r.BaseCount;
                    foreach (var rg in r.Reagents) inv.Remove(rg.Item, rg.Count);
                    inv.Add(r.Output, n);
                    total += n; progressed = true;
                }
            }
            return total;
        }
    }
}
